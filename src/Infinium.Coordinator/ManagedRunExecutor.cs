using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Application.Analysis;
using Infinium.Application.Runtime;
using Infinium.Bethesda;
using Infinium.Mo2;
using Infinium.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using LifecycleState = Infinium.Domain.Contracts.LifecycleState;

#pragma warning disable CA1848 // Failures are exceptional and retain structured run identity.

namespace Infinium.Coordinator;

public sealed class ManagedRunExecutor(
    CoordinatorRuntime runtime,
    WorkerBootstrapRegistry workerBootstraps,
    ILogger<ManagedRunExecutor> logger)
{
    private const string BethesdaSemanticOperation = "bethesda-semantic-v1";
    private const string AnalysisV1Operation = "analysis-v1";
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly Lock gate = new();
    private bool pumpRunning;

    public void Schedule(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            if (!pumpRunning)
            {
                pumpRunning = true;
                _ = Task.Run(DrainAsync);
            }
        }
    }

    public Task ExecuteBethesdaSemanticAsync(
        string runId,
        IReadOnlyList<BethesdaUnsupportedCapability> requestedUnsupportedCapabilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(requestedUnsupportedCapabilities);
        ManagedBethesdaSemanticIntent intent = new(requestedUnsupportedCapabilities);
        _ = runtime.Store.RegisterRunOperation(
            runId,
            BethesdaSemanticOperation,
            JsonSerializer.Serialize(intent),
            DateTimeOffset.UtcNow);
        return ExecuteCoreAsync(runId);
    }

    public Task ExecuteAnalysisV1Async(string runId, AnalysisV1WorkAssignment assignment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        AnalysisPublicationBuilder.ValidateAssignment(assignment);
        if (assignment.ExecutionInput.RunId.Value != runId)
        {
            throw new InvalidOperationException("The bounded analysis-v1 assignment belongs to another run.");
        }
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(assignment);
        if (bytes.LongLength > AnalysisV1WorkAssignment.MaximumAssignmentBytes)
        {
            throw new InvalidOperationException("The analysis-v1 assignment exceeds its serialized bound.");
        }
        _ = runtime.Store.RegisterRunOperation(
            runId, AnalysisV1Operation, Encoding.UTF8.GetString(bytes), DateTimeOffset.UtcNow);
        return ExecuteCoreAsync(runId);
    }

    public void RecoverAtStartup()
    {
        DateTimeOffset? afterCreatedAt = null;
        string? afterRunId = null;
        while (true)
        {
            IReadOnlyList<RunRecord> page = runtime.Store.ListNonTerminalRuns(
                100,
                afterCreatedAt,
                afterRunId);
            if (page.Count == 0)
            {
                return;
            }

            foreach (RunRecord run in page)
            {
                RunRecord current = run;
                if (run.State is LifecycleState.Running
                    or LifecycleState.Waiting)
                {
                    runtime.Store.SettleLiveAttempts(
                        run.RunId,
                        "interrupted-by-coordinator-recovery",
                        runtime.Authority.FencingEpoch);
                    AnalysisV1WorkAssignment? recoveryAnalysis = ResolveAnalysisAssignment(run);
                    bool committedPublication = runtime.Store.HasRecoverablePublication(run.RunId)
                        && (recoveryAnalysis is null
                            || runtime.Store.GetAnalysisSemanticFingerprint(run.RunId) is not null);
                    if (committedPublication)
                    {
                        _ = runtime.Store.Transition(
                            Guid.NewGuid().ToString("N"),
                            run.RunId,
                            run.Generation,
                            LifecycleState.Completed,
                            runtime.Authority.FencingEpoch,
                            "coordinator recovery finalized a committed publication",
                            DateTimeOffset.UtcNow);
                        continue;
                    }

                    current = runtime.Store.Transition(
                        Guid.NewGuid().ToString("N"),
                        run.RunId,
                        run.Generation,
                        LifecycleState.Retrying,
                        runtime.Authority.FencingEpoch,
                        "coordinator recovery fenced the interrupted attempt",
                        DateTimeOffset.UtcNow);
                }
                else if (run.State == LifecycleState.Pausing)
                {
                    runtime.Store.SettleLiveAttempts(
                        run.RunId,
                        "paused-at-recovery-boundary",
                        runtime.Authority.FencingEpoch);
                    _ = runtime.Store.Transition(
                        Guid.NewGuid().ToString("N"),
                        run.RunId,
                        run.Generation,
                        LifecycleState.Paused,
                        runtime.Authority.FencingEpoch,
                        "coordinator recovery observed the safe pause boundary",
                        DateTimeOffset.UtcNow);
                    continue;
                }
                else if (run.State == LifecycleState.Cancelling)
                {
                    runtime.Store.SettleLiveAttempts(
                        run.RunId,
                        "cancelled-at-recovery-boundary",
                        runtime.Authority.FencingEpoch);
                    AnalysisV1WorkAssignment? cancellationAnalysis = ResolveAnalysisAssignment(run);
                    if (cancellationAnalysis is null)
                    {
                        _ = runtime.Store.Transition(
                            Guid.NewGuid().ToString("N"),
                            run.RunId,
                            run.Generation,
                            LifecycleState.Cancelled,
                            runtime.Authority.FencingEpoch,
                            "coordinator recovery observed cancellation",
                            DateTimeOffset.UtcNow);
                    }
                    else
                    {
                        DateTimeOffset cancellationTime = DateTimeOffset.UtcNow;
                        AnalysisCancellationPublicationAdmission admission =
                            runtime.Store.PrepareCancelledAnalysisPublication(
                                run.RunId, runtime.Authority.FencingEpoch,
                                CoordinatorTerminalReceiptBytes(
                                    cancellationAnalysis, run.RunId,
                                    "coordinator-recovery-cancellation-output-only"),
                                cancellationTime);
                        _ = AnalysisExecutionPhase.PublishTerminalFallback(
                            runtime.Store, cancellationAnalysis, admission.Attempt, run.Binding,
                            admission.ValidationReceiptPayloadId, AnalysisTerminalOutcome.Cancelled,
                            "coordinator recovery published cancellation output", cancellationTime);
                    }
                    continue;
                }

                if (current.State is LifecycleState.Queued or LifecycleState.Retrying)
                {
                    Schedule(current.RunId);
                }
            }

            RunRecord last = page[^1];
            afterCreatedAt = last.CreatedAt;
            afterRunId = last.RunId;
        }
    }

    private async Task DrainAsync()
    {
        while (true)
        {
            RunRecord? next = runtime.Store.GetNextDispatchableRun();
            if (next is null)
            {
                lock (gate)
                {
                    next = runtime.Store.GetNextDispatchableRun();
                    if (next is null)
                    {
                        pumpRunning = false;
                        return;
                    }
                }
            }

            await ExecuteCoreAsync(next.RunId).ConfigureAwait(false);
        }
    }

    private async Task ExecuteCoreAsync(string runId)
    {
        AnalysisV1WorkAssignment? activeAnalysis = null;
        AttemptRecord? activeAttempt = null;
        string? activeValidationReceiptPayloadId = null;
        try
        {
            RunRecord queued = runtime.Store.GetRun(runId);
            if (queued.State is not (LifecycleState.Queued or LifecycleState.Retrying))
            {
                return;
            }

            ManagedBethesdaSemanticAssignment? bethesdaAssignment =
                ResolveBethesdaAssignment(queued);
            AnalysisV1WorkAssignment? analysisAssignment = ResolveAnalysisAssignment(queued);
            activeAnalysis = analysisAssignment;

            DispatchAdmission dispatch = runtime.Store.DispatchAttempt(
                Guid.NewGuid().ToString("N"),
                runId,
                queued.Generation,
                runtime.Authority.FencingEpoch,
                TimeSpan.FromMinutes(2),
                DateTimeOffset.UtcNow);
            AttemptRecord attempt = dispatch.Attempt;
            activeAttempt = attempt;
            using AttemptStagingAuthority staging =
                runtime.Store.Paths.CreateAttemptStagingDirectory(attempt.AttemptId);
            runtime.Store.RecordAuditEvent(
                "attempt-staging-created",
                "attempt",
                attempt.AttemptId,
                DateTimeOffset.UtcNow);
            bool isBethesda = bethesdaAssignment is not null;
            bool isAnalysis = analysisAssignment is not null;
            ManagedWorkerBootstrap bootstrap = new(
                1,
                Guid.NewGuid().ToString("N"),
                runtime.Authority.InstanceId,
                runtime.Authority.FencingEpoch,
                runId,
                attempt.AttemptId,
                attempt.AttemptFencingToken,
                runtime.Descriptor.WorkerPipe,
                0,
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"),
                0,
                isBethesda ? "bethesda-semantic.v2.json"
                    : isAnalysis ? "analysis-v1-validation-receipt.json" : "slice2-substrate.v1.json",
                isBethesda ? 64L * 1024 * 1024 : isAnalysis ? 1024 * 1024 : 65_536,
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                DateTimeOffset.UtcNow.AddMinutes(isBethesda ? 2 : 1),
                isBethesda ? ManagedWorkerOperationKind.BethesdaSemanticExtraction
                    : isAnalysis ? ManagedWorkerOperationKind.AnalysisV1
                    : ManagedWorkerOperationKind.SubstrateValidation,
                "1.0.0",
                null,
                bethesdaAssignment,
                analysisAssignment);
            ManagedWorkerResult result = await LaunchWorkerAsync(
                bootstrap,
                staging.Handle).ConfigureAwait(false);
            if (bethesdaAssignment is not null)
            {
                byte[] stagedBytes = runtime.Store.ReadRunStagedPayload(
                    attempt,
                    result.OutputRelativeName,
                    result.Sha256,
                    result.ByteLength,
                    bootstrap.MaximumOutputBytes);
                _ = BethesdaSemanticPublicationValidator.DeserializeAndValidate(
                    stagedBytes,
                    bethesdaAssignment,
                    bootstrap.MaximumOutputBytes);
            }
            PayloadAdmission validationAdmission = runtime.Store.AdmitStagedPayload(
                attempt,
                result.OutputRelativeName,
                result.Sha256,
                result.ByteLength,
                result.ManifestSha256,
                bootstrap.MaximumOutputBytes,
                DateTimeOffset.UtcNow,
                isAnalysis ? null : Guid.NewGuid().ToString("N"),
                bootstrap.StagedArtifactId);
            activeValidationReceiptPayloadId = isAnalysis ? validationAdmission.PayloadId : null;
            if (analysisAssignment is not null)
            {
                byte[] receiptBytes = runtime.Store.ReadCandidateAnalysisPayload(validationAdmission.PayloadId);
                AnalysisWorkerValidationReceipt receipt = JsonSerializer.Deserialize<AnalysisWorkerValidationReceipt>(receiptBytes, StrictJson)
                    ?? throw new InvalidDataException("The analysis-v1 worker validation receipt is malformed.");
                if (receipt.AssignmentId != analysisAssignment.AssignmentId
                    || receipt.RunId != runId
                    || receipt.Disposition != "validated-for-coordinator-publication-only"
                    || receipt.ValidatedInputs.Count != 3
                    || receipt.ExternalEffects.Values.Any(value => value != "not-used"))
                {
                    throw new InvalidDataException("The analysis-v1 worker validation receipt differs from its launch authority.");
                }
                RunRecord beforePublication = runtime.Store.GetRun(runId);
                AnalysisV1WorkAssignment finalAssignment = beforePublication.State == LifecycleState.Cancelling
                    ? analysisAssignment with
                    {
                        TerminalOutcome = AnalysisTerminalOutcome.Cancelled,
                        TerminalReason = "analysis cancelled at the coordinator publication boundary",
                    }
                    : analysisAssignment;
                _ = AnalysisExecutionPhase.Execute(
                    runtime.Store, finalAssignment, attempt, beforePublication.Binding,
                    validationAdmission.PayloadId, DateTimeOffset.UtcNow);
            }
        }
        catch (WorkerStoppedAtSafeBoundaryException)
        {
            RunRecord current = runtime.Store.GetRun(runId);
            if (current.State == LifecycleState.Cancelling
                && activeAnalysis is not null
                && activeAttempt is not null)
            {
                PublishAnalysisTerminalFallback(
                    activeAnalysis, activeAttempt, activeValidationReceiptPayloadId,
                    current, AnalysisTerminalOutcome.Cancelled,
                    "analysis cancelled at a managed-worker safe boundary");
            }
            else if (current.State is LifecycleState.Pausing or LifecycleState.Cancelling)
            {
                ObserveSafeBoundary(current);
            }
        }
        catch (AnalysisIdentityDriftException exception)
        {
            logger.LogError(exception, "Analysis identity drift invalidated run {RunId}.", runId);
            try
            {
                RunRecord current = runtime.Store.GetRun(runId);
                if (!LifecyclePolicy.IsTerminal(current.State))
                {
                    runtime.Store.Transition(
                        Guid.NewGuid().ToString("N"), runId, current.Generation,
                        LifecycleState.InvalidatedByChangedInput, runtime.Authority.FencingEpoch,
                        "analysis-v1 retained dependency identity drift", DateTimeOffset.UtcNow);
                }
            }
            catch (Exception transitionException)
            {
                logger.LogError(transitionException, "Failed to persist identity drift for run {RunId}.", runId);
            }
        }
        catch (AnalysisOutputLimitException exception)
        {
            logger.LogError(exception, "Analysis output limit was reached for run {RunId}.", runId);
            try
            {
                RunRecord current = runtime.Store.GetRun(runId);
                if (activeAnalysis is not null
                    && activeAttempt is not null
                    && current.State is LifecycleState.Running or LifecycleState.Waiting)
                {
                    PublishAnalysisTerminalFallback(
                        activeAnalysis, activeAttempt, activeValidationReceiptPayloadId,
                        current, AnalysisTerminalOutcome.LimitReached,
                        "analysis-v1 coordinator publication limit reached");
                }
            }
            catch (Exception transitionException)
            {
                logger.LogError(transitionException, "Failed to publish limit output for run {RunId}.", runId);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Managed worker execution failed for run {RunId}.", runId);
            try
            {
                RunRecord current = runtime.Store.GetRun(runId);
                if (current.State == LifecycleState.Pausing)
                {
                    ObserveSafeBoundary(current);
                }
                else if (current.State == LifecycleState.Cancelling
                    && activeAnalysis is not null
                    && activeAttempt is not null)
                {
                    PublishAnalysisTerminalFallback(
                        activeAnalysis, activeAttempt, activeValidationReceiptPayloadId,
                        current, AnalysisTerminalOutcome.Cancelled,
                        "analysis cancelled after managed execution failure");
                }
                else if (current.State == LifecycleState.Cancelling)
                {
                    ObserveSafeBoundary(current);
                }
                else if (activeAnalysis is not null
                    && activeAttempt is not null
                    && current.State is LifecycleState.Running or LifecycleState.Waiting)
                {
                    PublishAnalysisTerminalFallback(
                        activeAnalysis, activeAttempt, activeValidationReceiptPayloadId,
                        current, AnalysisTerminalOutcome.Failed,
                        "analysis-v1 worker or publication execution failed");
                }
                else if (!LifecyclePolicy.IsTerminal(current.State))
                {
                    runtime.Store.Transition(
                        Guid.NewGuid().ToString("N"),
                        runId,
                        current.Generation,
                        LifecycleState.Failed,
                        runtime.Authority.FencingEpoch,
                        "managed worker execution failed",
                        DateTimeOffset.UtcNow);
                }
            }
            catch (Exception transitionException)
            {
                logger.LogError(
                    transitionException,
                    "Failed to persist worker failure for run {RunId}.",
                    runId);
            }
        }
    }

    private void PublishAnalysisTerminalFallback(
        AnalysisV1WorkAssignment assignment,
        AttemptRecord attempt,
        string? existingValidationReceiptPayloadId,
        RunRecord current,
        AnalysisTerminalOutcome outcome,
        string reason)
    {
        string validationPayloadId = existingValidationReceiptPayloadId
            ?? runtime.Store.AdmitAnalysisCoordinatorFailureReceipt(
                attempt,
                CoordinatorTerminalReceiptBytes(
                    assignment, attempt.RunId, "coordinator-terminal-fallback-only"),
                DateTimeOffset.UtcNow);
        _ = AnalysisExecutionPhase.PublishTerminalFallback(
            runtime.Store, assignment, attempt, current.Binding, validationPayloadId,
            outcome, reason, DateTimeOffset.UtcNow);
    }

    private static byte[] CoordinatorTerminalReceiptBytes(
        AnalysisV1WorkAssignment assignment,
        string runId,
        string disposition) =>
        JsonSerializer.SerializeToUtf8Bytes(new AnalysisWorkerValidationReceipt(
            1,
            assignment.AssignmentId,
            runId,
            [assignment.DocumentationEvidence, assignment.CandidateAnalysis, assignment.FindingCase],
            checked(assignment.DocumentationEvidence.ByteLength
                + assignment.CandidateAnalysis.ByteLength
                + assignment.FindingCase.ByteLength),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = "not-used",
                ["model"] = "not-used",
                ["credential"] = "not-used",
                ["live"] = "not-used",
                ["billable"] = "not-used",
            },
            disposition));

    internal async Task<ManagedWorkerResult> LaunchWorkerAsync(
        ManagedWorkerBootstrap bootstrap,
        SafeFileHandle stagingDirectory)
    {
        string workerAssembly = Path.Combine(AppContext.BaseDirectory, "Infinium.Worker.dll");
        if (!File.Exists(workerAssembly))
        {
            throw new FileNotFoundException("The managed worker assembly is unavailable.", workerAssembly);
        }

        string dotnet = Path.GetFullPath(Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(),
            "..",
            "..",
            "..",
            "dotnet.exe"));
        Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
        };
        using WindowsContainedWorkerProcess contained = WindowsContainedWorkerProcess.Create(
            dotnet,
            [workerAssembly, "execute"],
            AppContext.BaseDirectory,
            environment,
            stagingDirectory);
        Process process = contained.Process;
        ManagedWorkerBootstrap boundBootstrap =
            bootstrap with
            {
                ExpectedProcessId = process.Id,
                InheritedStagingDirectoryHandle =
                    contained.InheritedStagingDirectoryHandle.ToInt64(),
            };
        workerBootstraps.Register(boundBootstrap);
        try
        {
            byte[] bootstrapBytes = JsonSerializer.SerializeToUtf8Bytes(boundBootstrap);
            contained.Resume();
            await contained.BootstrapInput.WriteAsync(bootstrapBytes).ConfigureAwait(false);
            contained.BootstrapInput.Close();
            Task<string> outputTask = ReadBoundedAsync(
                contained.StandardOutput,
                16_384,
                "worker receipt");
            Task<string> errorTask = ReadBoundedAsync(
                contained.StandardError,
                4_096,
                "worker diagnostics");
            TimeSpan workerTimeout =
                boundBootstrap.OperationKind is ManagedWorkerOperationKind.Mo2SnapshotCapture
                    or ManagedWorkerOperationKind.BethesdaSemanticExtraction
                    ? TimeSpan.FromMinutes(2)
                    : TimeSpan.FromSeconds(30);
            using CancellationTokenSource timeout = new(workerTimeout);
            Task waitTask = process.WaitForExitAsync(timeout.Token);
            List<Task> pending = [waitTask, outputTask, errorTask];
            while (!waitTask.IsCompleted)
            {
                Task completed = await Task.WhenAny(pending).ConfigureAwait(false);
                await completed.ConfigureAwait(false);
                if (completed != waitTask)
                {
                    pending.Remove(completed);
                }
            }

            await waitTask.ConfigureAwait(false);
            string output = await outputTask.ConfigureAwait(false);
            string error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode == 3)
            {
                workerBootstraps.GetAcceptedCancellation(boundBootstrap.BootstrapId);
                throw new WorkerStoppedAtSafeBoundaryException();
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Managed worker exited with code {process.ExitCode}: {Bounded(error)}");
            }

            ManagedWorkerResult result = JsonSerializer.Deserialize<ManagedWorkerResult>(output)
                ?? throw new InvalidOperationException("The worker receipt is malformed.");
            ManagedWorkerResult accepted =
                workerBootstraps.GetAcceptedResult(boundBootstrap.BootstrapId);
            if (!string.Equals(result.BootstrapId, boundBootstrap.BootstrapId, StringComparison.Ordinal)
                || !string.Equals(result.AttemptId, boundBootstrap.AttemptId, StringComparison.Ordinal)
                || result.CoordinatorFencingEpoch != boundBootstrap.CoordinatorFencingEpoch
                || result.AttemptFencingToken != boundBootstrap.AttemptFencingToken
                || result.ByteLength > boundBootstrap.MaximumOutputBytes
                || result != accepted)
            {
                throw new InvalidOperationException("The worker receipt is stale or outside its authority.");
            }

            return result;
        }
        catch
        {
            workerBootstraps.Abandon(boundBootstrap.BootstrapId);
            throw;
        }
    }

    private ManagedBethesdaSemanticAssignment? ResolveBethesdaAssignment(RunRecord run)
    {
        RunOperationRecord? operation = runtime.Store.GetRunOperation(run.RunId);
        if (operation is null)
        {
            return null;
        }

        if (string.Equals(operation.OperationKind, AnalysisV1Operation, StringComparison.Ordinal))
        {
            return null;
        }
        if (!string.Equals(operation.OperationKind, BethesdaSemanticOperation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The durable run operation kind is unsupported by this executor.");
        }

        string actualRequestSha256 = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(operation.RequestJson)));
        if (!string.Equals(actualRequestSha256, operation.RequestSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The durable Bethesda operation request failed identity validation.");
        }

        ManagedBethesdaSemanticIntent intent =
            JsonSerializer.Deserialize<ManagedBethesdaSemanticIntent>(operation.RequestJson, StrictJson)
            ?? throw new InvalidOperationException(
                "The durable Bethesda operation request is malformed.");
        byte[] snapshotBytes = runtime.Store.ReadPublishedSnapshotPayload(
            run.Binding.InstallationSnapshotId,
            64 * 1024 * 1024);
        Mo2SnapshotCaptureResult accepted =
            JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(snapshotBytes, StrictJson)
            ?? throw new InvalidOperationException(
                "The authoritative installation snapshot payload is malformed.");
        if (accepted.State is not (
                SnapshotCaptureState.Completed
                or SnapshotCaptureState.CompletedWithGaps)
            || accepted.Snapshot?.Contract.SnapshotId.Value != run.Binding.InstallationSnapshotId)
        {
            throw new InvalidOperationException(
                "The authoritative installation snapshot does not match the run binding.");
        }

        return SealBethesdaAssignment(new ManagedBethesdaSemanticAssignment(
            accepted,
            intent.RequestedUnsupportedCapabilities));
    }

    private AnalysisV1WorkAssignment? ResolveAnalysisAssignment(RunRecord run)
    {
        RunOperationRecord? operation = runtime.Store.GetRunOperation(run.RunId);
        if (operation is null || operation.OperationKind == BethesdaSemanticOperation)
        {
            return null;
        }
        if (operation.OperationKind != AnalysisV1Operation)
        {
            throw new InvalidOperationException("The durable run operation kind is unsupported by this executor.");
        }
        string actualSha = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(operation.RequestJson)));
        if (actualSha != operation.RequestSha256)
        {
            throw new AnalysisIdentityDriftException("The durable analysis-v1 assignment failed identity validation.");
        }
        AnalysisV1WorkAssignment assignment = JsonSerializer.Deserialize<AnalysisV1WorkAssignment>(operation.RequestJson, StrictJson)
            ?? throw new InvalidDataException("The durable analysis-v1 assignment is malformed.");
        AnalysisPublicationBuilder.ValidateAssignment(assignment);
        if (assignment.ExecutionInput.RunId.Value != run.RunId
            || assignment.ExecutionInput.InstallationSnapshot.ArtifactId.Value != run.Binding.InstallationSnapshotId
            || assignment.AnalysisContextId != run.Binding.AnalysisContextId
            || assignment.ExecutionInput.EffectiveConfiguration.ArtifactId.Value != run.Binding.EffectiveScanConfigurationId
            || assignment.ExecutionInput.ResolvedInputManifest.ArtifactId.Value != run.Binding.ResolvedInputManifestId)
        {
            throw new AnalysisIdentityDriftException("The durable analysis-v1 assignment differs from the immutable run binding.");
        }
        return assignment;
    }

    private static ManagedBethesdaSemanticAssignment SealBethesdaAssignment(
        ManagedBethesdaSemanticAssignment assignment)
    {
        Mo2InstallationSnapshot snapshot = assignment.AcceptedSnapshot.Snapshot
            ?? throw new InvalidOperationException("The accepted snapshot is absent.");
        List<ManagedBethesdaPluginSeal> seals = [];
        long totalBytes = 0;
        foreach (PluginState plugin in snapshot.Plugins
                     .Where(plugin => plugin.Enabled)
                     .OrderBy(plugin => plugin.LoadOrder))
        {
            LooseProviderChain chain = snapshot.LooseProviderChains.Single(candidate =>
                string.Equals(
                    candidate.NormalizedRelativePath,
                    plugin.Name,
                    StringComparison.OrdinalIgnoreCase));
            string path = Path.GetFullPath(chain.Winner.PhysicalPath);
            long byteLength;
            string sha256;
            using (FileStream stream = new(
                       path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       64 * 1024,
                       FileOptions.SequentialScan))
            {
                byteLength = stream.Length;
                totalBytes = checked(totalBytes + byteLength);
                if (byteLength <= 0 || totalBytes > 64L * 1024 * 1024)
                {
                    throw new InvalidOperationException(
                        "The aggregate coordinator-sealed Bethesda input exceeds its authority.");
                }

                sha256 = Convert.ToHexStringLower(SHA256.HashData(stream));
            }

            seals.Add(new ManagedBethesdaPluginSeal(
                plugin.Name,
                plugin.LoadOrder!.Value,
                path,
                byteLength,
                sha256));
        }

        return assignment with { PluginSeals = seals };
    }

    private static string Bounded(string value) => value.Length <= 512 ? value : value[..512];

    internal static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        int maximumUtf8Bytes,
        string description)
    {
        char[] buffer = new char[1024];
        StringBuilder result = new();
        int byteCount = 0;
        while (true)
        {
            int read = await reader.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0)
            {
                return result.ToString();
            }

            byteCount = checked(byteCount + Encoding.UTF8.GetByteCount(buffer.AsSpan(0, read)));
            if (byteCount > maximumUtf8Bytes)
            {
                throw new InvalidOperationException($"The {description} exceeds its bound.");
            }

            result.Append(buffer, 0, read);
        }
    }

    private void ObserveSafeBoundary(RunRecord requested)
    {
        bool pausing = requested.State == LifecycleState.Pausing;
        runtime.Store.SettleLiveAttempts(
            requested.RunId,
            pausing ? "paused-at-safe-boundary" : "cancelled-at-safe-boundary",
            runtime.Authority.FencingEpoch);
        runtime.Store.Transition(
            Guid.NewGuid().ToString("N"),
            requested.RunId,
            requested.Generation,
            pausing ? LifecycleState.Paused : LifecycleState.Cancelled,
            runtime.Authority.FencingEpoch,
            pausing
                ? "managed worker acknowledged the pause safe boundary"
                : "managed worker acknowledged the cancellation safe boundary",
            DateTimeOffset.UtcNow);
    }
}

#pragma warning restore CA1848

internal sealed class WorkerStoppedAtSafeBoundaryException : Exception;
