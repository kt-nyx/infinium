using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Application.Runtime;
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
                    if (runtime.Store.HasRecoverablePublication(run.RunId))
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
                    _ = runtime.Store.Transition(
                        Guid.NewGuid().ToString("N"),
                        run.RunId,
                        run.Generation,
                        LifecycleState.Cancelled,
                        runtime.Authority.FencingEpoch,
                        "coordinator recovery observed cancellation",
                        DateTimeOffset.UtcNow);
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
        try
        {
            RunRecord queued = runtime.Store.GetRun(runId);
            if (queued.State is not (LifecycleState.Queued or LifecycleState.Retrying))
            {
                return;
            }

            DispatchAdmission dispatch = runtime.Store.DispatchAttempt(
                Guid.NewGuid().ToString("N"),
                runId,
                queued.Generation,
                runtime.Authority.FencingEpoch,
                TimeSpan.FromMinutes(2),
                DateTimeOffset.UtcNow);
            AttemptRecord attempt = dispatch.Attempt;
            using AttemptStagingAuthority staging =
                runtime.Store.Paths.CreateAttemptStagingDirectory(attempt.AttemptId);
            runtime.Store.RecordAuditEvent(
                "attempt-staging-created",
                "attempt",
                attempt.AttemptId,
                DateTimeOffset.UtcNow);
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
                "slice2-substrate.v1.json",
                65_536,
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                DateTimeOffset.UtcNow.AddMinutes(1));
            ManagedWorkerResult result = await LaunchWorkerAsync(
                bootstrap,
                staging.Handle).ConfigureAwait(false);
            runtime.Store.AdmitStagedPayload(
                attempt,
                result.OutputRelativeName,
                result.Sha256,
                result.ByteLength,
                result.ManifestSha256,
                bootstrap.MaximumOutputBytes,
                DateTimeOffset.UtcNow,
                Guid.NewGuid().ToString("N"),
                bootstrap.StagedArtifactId);
        }
        catch (WorkerStoppedAtSafeBoundaryException)
        {
            RunRecord current = runtime.Store.GetRun(runId);
            if (current.State is LifecycleState.Pausing or LifecycleState.Cancelling)
            {
                ObserveSafeBoundary(current);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Managed worker execution failed for run {RunId}.", runId);
            try
            {
                RunRecord current = runtime.Store.GetRun(runId);
                if (current.State is LifecycleState.Pausing or LifecycleState.Cancelling)
                {
                    ObserveSafeBoundary(current);
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

    private async Task<ManagedWorkerResult> LaunchWorkerAsync(
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
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
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
