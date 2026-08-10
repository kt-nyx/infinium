using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Analysis;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Bethesda;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using StagedArtifactKind = Infinium.Contracts.Protobuf.Worker.V1.StagedArtifactKind;

if (args.Length == 1
    && string.Equals(args[0], "containment-probe", StringComparison.Ordinal))
{
    try
    {
        byte[] probeBootstrap = await ReadBoundedAsync(
            Console.OpenStandardInput(),
            4096).ConfigureAwait(false);
        using JsonDocument probeDocument = JsonDocument.Parse(probeBootstrap);
        JsonElement probeRoot = probeDocument.RootElement;
        nint stagingHandle = new(
            probeRoot.GetProperty("stagingHandle").GetInt64());
        string outputName = probeRoot.GetProperty("outputName").GetString()
            ?? throw new InvalidOperationException("The probe output name is missing.");
        nint includedHandle = new(
            probeRoot.GetProperty("includedHandle").GetInt64());
        nint excludedHandle = new(
            probeRoot.GetProperty("excludedHandle").GetInt64());
        if (!WorkerNativeMethods.IsProcessInJob(
                WorkerNativeMethods.GetCurrentProcess(),
                0,
                out bool contained))
        {
            throw new InvalidOperationException("The worker Job membership could not be inspected.");
        }

        byte[] probe = JsonSerializer.SerializeToUtf8Bytes(new
        {
            jobContainedAtEntry = contained,
            includedHandlePath = WindowsHandleRelativeFile.TryGetFinalPath(includedHandle),
            excludedHandlePath = WindowsHandleRelativeFile.TryGetFinalPath(excludedHandle),
        });
        using FileStream output =
            WindowsHandleRelativeFile.CreateNew(stagingHandle, outputName);
        output.Write(probe);
        output.Flush(flushToDisk: true);
        return 0;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(Bounded(exception.Message));
        return 1;
    }
}

if (args.Length != 1 || !string.Equals(args[0], "execute", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Infinium.Worker is coordinator-launched only.");
    return 2;
}

try
{
    byte[] bootstrapBytes = await ReadBoundedAsync(Console.OpenStandardInput(), 16 * 1024 * 1024)
        .ConfigureAwait(false);
    ManagedWorkerBootstrap bootstrap = JsonSerializer.Deserialize<ManagedWorkerBootstrap>(bootstrapBytes)
        ?? throw new InvalidOperationException("The private bootstrap is malformed.");
    Validate(bootstrap);

    using Grpc.Net.Client.GrpcChannel channel =
        NamedPipeGrpcChannel.Create(bootstrap.WorkerPipe);
    WorkerService.WorkerServiceClient client = new(channel);
    HandshakeResponse handshake = await client.NegotiateAsync(new WorkerHandshakeRequest
    {
        BootstrapId = bootstrap.BootstrapId,
        OneUseNonce = ByteString.CopyFrom(
            Convert.FromBase64String(bootstrap.OneUseNonceBase64)),
        ExpectedAttemptId = new AttemptId { Value = bootstrap.AttemptId },
        SupportedProtocol = new ProtocolVersionRange
        {
            Major = ProtocolConstants.Major,
            MinimumMinor = ProtocolConstants.Minor,
            MaximumMinor = ProtocolConstants.Minor,
        },
        Compatibility = ProtocolConstants.Compatibility,
        ObservedCoordinatorFencingEpoch =
            checked((ulong)bootstrap.CoordinatorFencingEpoch),
        ProcessId = checked((uint)Environment.ProcessId),
    }, deadline: GetRpcDeadline(bootstrap)).ResponseAsync.ConfigureAwait(false);
    if (handshake.Disposition != HandshakeDisposition.Accepted
        || handshake.BoundEndpointRole != EndpointRole.GeneralWorker)
    {
        throw new InvalidOperationException("The coordinator rejected the worker launch binding.");
    }

    ReceiveAssignmentResponse assignmentResponse =
        await client.ReceiveAssignmentAsync(new ReceiveAssignmentRequest
        {
            BootstrapId = bootstrap.BootstrapId,
            ExpectedAttemptId = new AttemptId { Value = bootstrap.AttemptId },
            ObservedCoordinatorFencingEpoch =
                checked((ulong)bootstrap.CoordinatorFencingEpoch),
        }, deadline: GetRpcDeadline(bootstrap)).ResponseAsync.ConfigureAwait(false);
    WorkerAssignment assignment = assignmentResponse.Assignment
        ?? throw new InvalidOperationException("The coordinator did not issue an assignment.");
    ValidateAssignment(bootstrap, assignment);

    WorkerProgressReceipt progress = await client.ReportProgressAsync(new WorkerProgress
    {
        AttemptId = new AttemptId { Value = bootstrap.AttemptId },
        CoordinatorFencingEpoch = checked((ulong)bootstrap.CoordinatorFencingEpoch),
        AttemptFencingToken = checked((ulong)bootstrap.AttemptFencingToken),
        ProgressSequence = 1,
        CompletedWorkUnits = 0,
        TotalWorkUnits = new OptionalUInt64
        {
            Availability = AvailabilityState.Available,
            Value = 1,
        },
        InertStatusText = "Executing bounded substrate fixture.",
    }, deadline: GetRpcDeadline(bootstrap)).ResponseAsync.ConfigureAwait(false);
    if (progress.Disposition != WorkerReceiptDisposition.AcceptedForStagingOnly)
    {
        throw new InvalidOperationException("The coordinator rejected worker progress.");
    }

    await Task.Delay(
        bootstrap.OperationKind == ManagedWorkerOperationKind.AnalysisV1 ? 10 : 2_000)
        .ConfigureAwait(false);
    PollControlResponse control = await client.PollControlAsync(new PollControlRequest
    {
        AttemptId = new AttemptId { Value = bootstrap.AttemptId },
        CoordinatorFencingEpoch = checked((ulong)bootstrap.CoordinatorFencingEpoch),
        AttemptFencingToken = checked((ulong)bootstrap.AttemptFencingToken),
    }, deadline: GetRpcDeadline(bootstrap)).ResponseAsync.ConfigureAwait(false);
    if (control.Control == WorkerControl.CancelAtSafeBoundary
        && bootstrap.OperationKind != ManagedWorkerOperationKind.AnalysisV1)
    {
        WorkerTerminalReceiptResponse cancelled =
            await client.SubmitTerminalReceiptAsync(new WorkerTerminalReceipt
            {
                AttemptId = new AttemptId { Value = bootstrap.AttemptId },
                CoordinatorFencingEpoch =
                    checked((ulong)bootstrap.CoordinatorFencingEpoch),
                AttemptFencingToken = checked((ulong)bootstrap.AttemptFencingToken),
                Outcome = WorkerTerminalOutcome.Cancelled,
            }, deadline: GetRpcDeadline(bootstrap)).ResponseAsync.ConfigureAwait(false);
        if (cancelled.Disposition is not (
                WorkerReceiptDisposition.AcceptedForStagingOnly
                or WorkerReceiptDisposition.Duplicate)
            || !cancelled.QueuedForCoordinatorValidation)
        {
            throw new InvalidOperationException(
                "The coordinator rejected the safe-boundary acknowledgement.");
        }

        return 3;
    }

    if (control.Control != WorkerControl.Continue
        && !(control.Control == WorkerControl.CancelAtSafeBoundary
            && bootstrap.OperationKind == ManagedWorkerOperationKind.AnalysisV1))
    {
        throw new InvalidOperationException("The coordinator withdrew the worker authority.");
    }

    Mo2SnapshotCaptureResult? snapshotResult = null;
    BethesdaSemanticExtractionResult? bethesdaResult = null;
    byte[] payload;
    if (assignment.Operation.Kind == WorkerOperationKind.CaptureMo2Snapshot)
    {
        Mo2SnapshotCaptureAssignment capture = assignment.Operation.Mo2SnapshotCapture
            ?? throw new InvalidOperationException(
                "The typed MO2 snapshot assignment is missing.");
        Mo2SnapshotCaptureRequest captureRequest = new(
            capture.Mo2ExecutablePath,
            capture.InstanceRoot,
            capture.InstanceIniPath,
            capture.ProfilesRoot,
            capture.ModsRoot,
            capture.OverwriteRoot,
            capture.GameDataRoot,
            capture.SkyrimExecutablePath,
            capture.SelectedProfileName,
            new RuntimeTargetContext(
                capture.Platform,
                capture.DistributionChannel,
                capture.ApplicationId),
            capture.QualifiedMappings.Select(mapping => new QualifiedMapping(
                mapping.MappingId,
                mapping.SourceRoot,
                mapping.VirtualPrefix,
                mapping.MapperSha256)).ToArray(),
            capture.EnabledMapperSha256.ToArray());
        TimeSpan remaining = bootstrap.ExpiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "The snapshot capture assignment expired before execution.");
        }

        using CancellationTokenSource captureDeadline = new(remaining);
        snapshotResult = new Mo2SnapshotCapture().Capture(
            captureRequest,
            captureDeadline.Token);
        if (snapshotResult.State is not (
                SnapshotCaptureState.Completed
                or SnapshotCaptureState.CompletedWithGaps)
            || snapshotResult.Snapshot is null)
        {
            throw new InvalidOperationException(
                "The MO2 snapshot capture did not produce a publishable snapshot.");
        }

        payload = JsonSerializer.SerializeToUtf8Bytes(snapshotResult);
    }
    else if (assignment.Operation.Kind == WorkerOperationKind.BuildTypedIndex)
    {
        ManagedBethesdaSemanticAssignment semantic = bootstrap.BethesdaSemanticExtraction
            ?? throw new InvalidOperationException(
                "The typed Bethesda semantic assignment is missing.");
        TimeSpan remaining = bootstrap.ExpiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "The Bethesda semantic assignment expired before execution.");
        }

        using CancellationTokenSource semanticDeadline = new(remaining);
        bethesdaResult = new BethesdaSemanticExtractor().Extract(
            new BethesdaSemanticRequest(
                semantic.AcceptedSnapshot,
                semantic.RequestedUnsupportedCapabilities),
            semanticDeadline.Token);
        if (bethesdaResult.State is not (
                BethesdaExtractionState.Completed
                or BethesdaExtractionState.CompletedWithGaps)
            || bethesdaResult.Snapshot is null)
        {
            throw new InvalidOperationException(
                "Bethesda semantic extraction did not produce a publishable staged result.");
        }

        payload = JsonSerializer.SerializeToUtf8Bytes(bethesdaResult);
    }
    else if (assignment.Operation.Kind == WorkerOperationKind.ValidateStagedArtifact)
    {
        payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            kind = "staged-artifact-validation",
            bootstrap.RunId,
            bootstrap.AttemptId,
            coordinatorFencingEpoch = bootstrap.CoordinatorFencingEpoch,
            attemptFencingToken = bootstrap.AttemptFencingToken,
        });
    }
    else if (assignment.Operation.Kind == WorkerOperationKind.RunDeclaredAnalyzer
        && bootstrap.OperationKind == ManagedWorkerOperationKind.AnalysisV1)
    {
        AnalysisV1WorkAssignment analysis = bootstrap.AnalysisV1
            ?? throw new InvalidOperationException("The bounded analysis-v1 assignment is missing.");
        AnalysisPublicationBuilder.ValidateAssignment(analysis);
        RetainedAnalysisPayloadSeal[] seals = ValidateAnalysisInputs(bootstrap, assignment, analysis);
        payload = JsonSerializer.SerializeToUtf8Bytes(new AnalysisWorkerValidationReceipt(
            1,
            analysis.AssignmentId,
            analysis.ExecutionInput.RunId.Value,
            seals,
            seals.Sum(item => item.ByteLength),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = "not-used",
                ["model"] = "not-used",
                ["credential"] = "not-used",
                ["live"] = "not-used",
                ["billable"] = "not-used",
            },
            "validated-for-coordinator-publication-only"));
    }
    else
    {
        throw new InvalidOperationException("The worker operation is unsupported.");
    }
    if (payload.LongLength > bootstrap.MaximumOutputBytes)
    {
        throw new InvalidOperationException("The staged payload exceeds its authority.");
    }

    using (FileStream output = WindowsHandleRelativeFile.CreateNew(
        new nint(bootstrap.InheritedStagingDirectoryHandle),
        bootstrap.OutputRelativeName))
    {
        output.Write(payload);
        output.Flush(flushToDisk: true);
    }
    string sha = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    ByteString shaBytes = ByteString.CopyFrom(Convert.FromHexString(sha));
    ByteString manifestDigest = ByteString.CopyFrom(
        ManagedWorkerManifest.ComputeDigest(
            bootstrap.StagedArtifactId,
        bootstrap.OutputRelativeName,
        sha,
        payload.LongLength,
        bootstrap.OutputSchemaVersion));
    int canonicalManifestByteLength = ManagedWorkerManifest.GetCanonicalBytes(
        bootstrap.StagedArtifactId,
        bootstrap.OutputRelativeName,
        sha,
        payload.LongLength,
        bootstrap.OutputSchemaVersion).Length;
    StagedOutputManifest manifest = new()
    {
        StagingAreaId = new StagingAreaId { Value = bootstrap.StagingAreaId },
        AttemptId = new AttemptId { Value = bootstrap.AttemptId },
        CoordinatorFencingEpoch = checked((ulong)bootstrap.CoordinatorFencingEpoch),
        AttemptFencingToken = checked((ulong)bootstrap.AttemptFencingToken),
        ManifestDigest = new ContentDigest
        {
            Algorithm = DigestAlgorithm.Sha256,
            Value = manifestDigest,
            SizeBytes = checked((ulong)canonicalManifestByteLength),
        },
    };
    manifest.Outputs.Add(new StagedOutput
    {
        StagedArtifactId = new StagedArtifactId { Value = bootstrap.StagedArtifactId },
        Kind = StagedArtifactKind.TypedResult,
        TypedRelativeName = bootstrap.OutputRelativeName,
        Content = new ContentDigest
        {
            Algorithm = DigestAlgorithm.Sha256,
            Value = shaBytes,
            SizeBytes = checked((ulong)payload.LongLength),
        },
        SchemaVersion = new SemanticVersion
        {
            Value = bootstrap.OutputSchemaVersion,
        },
    });
    SubmitStagedOutputResponse staged = await client.SubmitStagedOutputAsync(
        new SubmitStagedOutputRequest { Manifest = manifest },
        deadline: GetRpcDeadline(bootstrap))
        .ResponseAsync.ConfigureAwait(false);
    if (staged.Disposition is not (
            WorkerReceiptDisposition.AcceptedForStagingOnly
            or WorkerReceiptDisposition.Duplicate)
        || string.IsNullOrWhiteSpace(staged.StagingReceiptId))
    {
        throw new InvalidOperationException("The coordinator rejected the staged manifest.");
    }

    WorkerTerminalReceiptResponse terminal =
        await client.SubmitTerminalReceiptAsync(new WorkerTerminalReceipt
        {
            AttemptId = new AttemptId { Value = bootstrap.AttemptId },
            CoordinatorFencingEpoch =
                checked((ulong)bootstrap.CoordinatorFencingEpoch),
            AttemptFencingToken = checked((ulong)bootstrap.AttemptFencingToken),
            Outcome = snapshotResult?.State == SnapshotCaptureState.CompletedWithGaps
                || bethesdaResult?.State == BethesdaExtractionState.CompletedWithGaps
                ? WorkerTerminalOutcome.CompletedWithGapsStaged
                : WorkerTerminalOutcome.CompletedStaged,
            StagingReceiptId = staged.StagingReceiptId,
        }, deadline: GetRpcDeadline(bootstrap)).ResponseAsync.ConfigureAwait(false);
    if (terminal.Disposition is not (
            WorkerReceiptDisposition.AcceptedForStagingOnly
            or WorkerReceiptDisposition.Duplicate)
        || !terminal.QueuedForCoordinatorValidation)
    {
        throw new InvalidOperationException("The coordinator rejected the terminal receipt.");
    }

    ManagedWorkerResult result = new(
        1,
        bootstrap.BootstrapId,
        bootstrap.AttemptId,
        bootstrap.CoordinatorFencingEpoch,
        bootstrap.AttemptFencingToken,
        bootstrap.OutputRelativeName,
        sha,
        payload.LongLength,
        Convert.ToHexString(manifestDigest.Span).ToLowerInvariant());
    Console.Write(JsonSerializer.Serialize(result));
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(Bounded(exception.Message));
    return 1;
}

static void Validate(ManagedWorkerBootstrap bootstrap)
{
    if (bootstrap.SchemaVersion != 1
        || string.IsNullOrWhiteSpace(bootstrap.BootstrapId)
        || string.IsNullOrWhiteSpace(bootstrap.CoordinatorInstanceId)
        || bootstrap.CoordinatorFencingEpoch <= 0
        || string.IsNullOrWhiteSpace(bootstrap.RunId)
        || string.IsNullOrWhiteSpace(bootstrap.AttemptId)
        || bootstrap.AttemptFencingToken <= 0
        || string.IsNullOrWhiteSpace(bootstrap.WorkerPipe)
        || bootstrap.ExpectedProcessId <= 0
        || string.IsNullOrWhiteSpace(bootstrap.StagingAreaId)
        || string.IsNullOrWhiteSpace(bootstrap.StagedArtifactId)
        || bootstrap.InheritedStagingDirectoryHandle <= 0
        || string.IsNullOrWhiteSpace(bootstrap.OutputRelativeName)
        || Path.IsPathFullyQualified(bootstrap.OutputRelativeName)
        || bootstrap.OutputRelativeName.Contains(':', StringComparison.Ordinal)
        || bootstrap.OutputRelativeName.Contains(Path.DirectorySeparatorChar)
        || bootstrap.OutputRelativeName.Contains(Path.AltDirectorySeparatorChar)
        || bootstrap.MaximumOutputBytes is <= 0 or > 64 * 1024 * 1024
        || (bootstrap.OperationKind == ManagedWorkerOperationKind.Mo2SnapshotCapture
            && bootstrap.Mo2SnapshotCapture is null)
        || (bootstrap.OperationKind == ManagedWorkerOperationKind.BethesdaSemanticExtraction
            && bootstrap.BethesdaSemanticExtraction is null)
        || (bootstrap.OperationKind == ManagedWorkerOperationKind.AnalysisV1
            && (bootstrap.AnalysisV1 is null
                || bootstrap.AnalysisInputRelativeNames is null
                || bootstrap.AnalysisInputRelativeNames.Count != 3))
        || (bootstrap.OperationKind != ManagedWorkerOperationKind.Mo2SnapshotCapture
            && bootstrap.Mo2SnapshotCapture is not null)
        || (bootstrap.OperationKind != ManagedWorkerOperationKind.BethesdaSemanticExtraction
            && bootstrap.BethesdaSemanticExtraction is not null)
        || (bootstrap.OperationKind != ManagedWorkerOperationKind.AnalysisV1
            && (bootstrap.AnalysisV1 is not null || bootstrap.AnalysisInputRelativeNames is not null))
        || bootstrap.ExpiresAt <= DateTimeOffset.UtcNow
        || Convert.FromBase64String(bootstrap.OneUseNonceBase64).Length != 32)
    {
        throw new InvalidOperationException("The private bootstrap is invalid or expired.");
    }

}

static DateTime GetRpcDeadline(ManagedWorkerBootstrap bootstrap)
{
    DateTime localBound = DateTime.UtcNow.AddSeconds(5);
    DateTime bootstrapBound = bootstrap.ExpiresAt.UtcDateTime;
    return localBound < bootstrapBound ? localBound : bootstrapBound;
}

static void ValidateAssignment(
    ManagedWorkerBootstrap bootstrap,
    WorkerAssignment assignment)
{
    StagedOutputSlot? slot = assignment.StagingAuthority?.AllowedOutputs.SingleOrDefault();
    if (assignment.AttemptId?.Value != bootstrap.AttemptId
        || assignment.CoordinatorFencingEpoch
            != checked((ulong)bootstrap.CoordinatorFencingEpoch)
        || assignment.AttemptFencingToken
            != checked((ulong)bootstrap.AttemptFencingToken)
        || assignment.StagingAuthority?.StagingAreaId?.Value != bootstrap.StagingAreaId
        || assignment.StagingAuthority.InheritedStagingHandleSlot != 1
        || assignment.Limits?.MaximumTotalOutputBytes
            != checked((ulong)bootstrap.MaximumOutputBytes)
        || assignment.Limits.MaximumSingleOutputBytes
            != checked((ulong)bootstrap.MaximumOutputBytes)
        || assignment.Limits.MaximumStagedOutputs != 1
        || slot?.StagedArtifactId?.Value != bootstrap.StagedArtifactId
        || slot.TypedRelativeName != bootstrap.OutputRelativeName
        || slot.MaximumBytes != checked((ulong)bootstrap.MaximumOutputBytes)
        || slot.Kind != StagedArtifactKind.TypedResult
        || (bootstrap.OperationKind == ManagedWorkerOperationKind.Mo2SnapshotCapture
            && (assignment.Operation?.Kind != WorkerOperationKind.CaptureMo2Snapshot
                || assignment.Operation.Mo2SnapshotCapture is null
                || assignment.Operation.AdapterOrAnalyzerId
                    != "infinium.mo2-static-reconstruction"
                || assignment.Operation.AdapterOrAnalyzerVersion?.Value != "3.0.0"))
        || (bootstrap.OperationKind == ManagedWorkerOperationKind.SubstrateValidation
            && assignment.Operation?.Kind != WorkerOperationKind.ValidateStagedArtifact)
        || (bootstrap.OperationKind == ManagedWorkerOperationKind.BethesdaSemanticExtraction
            && (assignment.Operation?.Kind != WorkerOperationKind.BuildTypedIndex
                || assignment.Operation.AdapterOrAnalyzerId
                    != BethesdaSemanticExtractor.ProducerId
                || assignment.Operation.AdapterOrAnalyzerVersion?.Value
                    != BethesdaSemanticExtractor.ProducerVersion
                || assignment.Limits?.MaximumTotalInputBytes != 64UL * 1024 * 1024
                || assignment.Limits.MaximumDuration?.Value != 120_000))
        || (bootstrap.OperationKind == ManagedWorkerOperationKind.AnalysisV1
            && (assignment.Operation?.Kind != WorkerOperationKind.RunDeclaredAnalyzer
                || assignment.Operation.AdapterOrAnalyzerId
                    != "infinium.analysis-v1-publication-validator"
                || assignment.Operation.AdapterOrAnalyzerVersion?.Value != "1.0.0"
                || assignment.Inputs.Count != 3
                || assignment.Inputs.Any(input => input.Kind != WorkerInputKind.ImmutablePayload)
                || assignment.Inputs.Any(input => input.InheritedReadHandleSlot != 1)
                || !assignment.Inputs.Select(input => input.LogicalName)
                    .SequenceEqual(bootstrap.AnalysisInputRelativeNames!, StringComparer.Ordinal)
                || assignment.Limits?.MaximumTotalInputBytes
                    != checked((ulong)bootstrap.AnalysisV1!.MaximumInputBytes)
                || assignment.Limits.MaximumWorkUnits != 3
                || assignment.Limits.MaximumDuration?.Value
                    != checked((ulong)bootstrap.AnalysisV1.ExecutionInput.Limits.MaximumWallTimeMilliseconds))))
    {
        throw new InvalidOperationException("The worker assignment exceeds its launch authority.");
    }
}

static RetainedAnalysisPayloadSeal[] ValidateAnalysisInputs(
    ManagedWorkerBootstrap bootstrap,
    WorkerAssignment assignment,
    AnalysisV1WorkAssignment analysis)
{
    RetainedAnalysisPayloadSeal[] expected =
        [analysis.DocumentationEvidence, analysis.CandidateAnalysis, analysis.FindingCase];
    string[] names = bootstrap.AnalysisInputRelativeNames?.ToArray()
        ?? throw new InvalidOperationException("The analysis-v1 staged input names are absent.");
    if (names.Length != expected.Length || assignment.Inputs.Count != expected.Length)
    {
        throw new InvalidOperationException("The analysis-v1 immutable input set is incomplete.");
    }
    long total = 0;
    for (int index = 0; index < expected.Length; index++)
    {
        RetainedAnalysisPayloadSeal seal = expected[index];
        WorkerInput authority = assignment.Inputs[index];
        if (authority.PayloadId?.Value != seal.PayloadId
            || authority.Content?.Algorithm != DigestAlgorithm.Sha256
            || !authority.Content.Value.Span.SequenceEqual(Convert.FromHexString(seal.Sha256))
            || authority.Content.SizeBytes != checked((ulong)seal.ByteLength)
            || authority.LogicalName != names[index]
            || authority.InheritedReadHandleSlot != 1)
        {
            throw new InvalidOperationException("The analysis-v1 worker input authority differs from its immutable seal.");
        }
        using FileStream input = WindowsHandleRelativeFile.OpenRead(
            new nint(bootstrap.InheritedStagingDirectoryHandle), names[index]);
        if (input.Length != seal.ByteLength || seal.ByteLength > analysis.MaximumInputBytes)
        {
            throw new InvalidDataException("The staged analysis-v1 input length differs from its immutable seal.");
        }
        byte[] bytes = new byte[checked((int)seal.ByteLength)];
        input.ReadExactly(bytes);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != seal.Sha256)
        {
            throw new InvalidDataException("The staged analysis-v1 input digest differs from its immutable seal.");
        }
        switch (seal.SchemaId)
        {
            case ContractConstants.DocumentationEvidenceSchemaId:
                DocumentationEvidenceContract documentation = DocumentationEvidenceJsonCodec.Deserialize(bytes);
                if (!bytes.AsSpan().SequenceEqual(DocumentationEvidenceJsonCodec.Serialize(documentation)))
                {
                    throw new InvalidDataException("The staged documentation evidence payload identity is not canonical.");
                }
                break;
            case ContractConstants.CandidateAnalysisSchemaId:
                CandidateAnalysisContract candidates = CandidateAnalysisJsonCodec.Deserialize(bytes);
                if (!bytes.AsSpan().SequenceEqual(CandidateAnalysisJsonCodec.Serialize(candidates)))
                {
                    throw new InvalidDataException("The staged candidate analysis payload identity is not canonical.");
                }
                break;
            case ContractConstants.FindingCaseSchemaId:
                FindingCaseContract findings = FindingCaseJsonCodec.Deserialize(bytes);
                if (!bytes.AsSpan().SequenceEqual(FindingCaseJsonCodec.Serialize(findings)))
                {
                    throw new InvalidDataException("The staged finding and case analysis payload identity is not canonical.");
                }
                break;
            default:
                throw new InvalidDataException("The staged analysis-v1 input schema is not allowlisted.");
        }
        total = checked(total + seal.ByteLength);
    }
    if (total > analysis.MaximumInputBytes)
    {
        throw new InvalidDataException("The staged analysis-v1 input set exceeds its total byte authority.");
    }
    return expected;
}

static async Task<byte[]> ReadBoundedAsync(Stream stream, int maximumBytes)
{
    using MemoryStream buffer = new(maximumBytes);
    byte[] chunk = new byte[4096];
    while (true)
    {
        int read = await stream.ReadAsync(chunk).ConfigureAwait(false);
        if (read == 0)
        {
            return buffer.ToArray();
        }

        if (buffer.Length + read > maximumBytes)
        {
            throw new InvalidOperationException("The private bootstrap exceeds its bound.");
        }

        buffer.Write(chunk, 0, read);
    }
}

static string Bounded(string value) => value.Length <= 512 ? value : value[..512];

internal static class WorkerNativeMethods
{
    [DllImport("kernel32.dll")]
    public static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsProcessInJob(
        nint process,
        nint job,
        [MarshalAs(UnmanagedType.Bool)] out bool result);
}
