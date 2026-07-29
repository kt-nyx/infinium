using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;

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
    byte[] bootstrapBytes = await ReadBoundedAsync(Console.OpenStandardInput(), 16_384)
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
    }).ResponseAsync.ConfigureAwait(false);
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
        }).ResponseAsync.ConfigureAwait(false);
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
    }).ResponseAsync.ConfigureAwait(false);
    if (progress.Disposition != WorkerReceiptDisposition.AcceptedForStagingOnly)
    {
        throw new InvalidOperationException("The coordinator rejected worker progress.");
    }

    await Task.Delay(2_000).ConfigureAwait(false);
    PollControlResponse control = await client.PollControlAsync(new PollControlRequest
    {
        AttemptId = new AttemptId { Value = bootstrap.AttemptId },
        CoordinatorFencingEpoch = checked((ulong)bootstrap.CoordinatorFencingEpoch),
        AttemptFencingToken = checked((ulong)bootstrap.AttemptFencingToken),
    }).ResponseAsync.ConfigureAwait(false);
    if (control.Control == WorkerControl.CancelAtSafeBoundary)
    {
        WorkerTerminalReceiptResponse cancelled =
            await client.SubmitTerminalReceiptAsync(new WorkerTerminalReceipt
            {
                AttemptId = new AttemptId { Value = bootstrap.AttemptId },
                CoordinatorFencingEpoch =
                    checked((ulong)bootstrap.CoordinatorFencingEpoch),
                AttemptFencingToken = checked((ulong)bootstrap.AttemptFencingToken),
                Outcome = WorkerTerminalOutcome.Cancelled,
            }).ResponseAsync.ConfigureAwait(false);
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

    if (control.Control != WorkerControl.Continue)
    {
        throw new InvalidOperationException("The coordinator withdrew the worker authority.");
    }

    byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
    {
        schemaVersion = 1,
        kind = "m1-slice2-substrate",
        bootstrap.RunId,
        bootstrap.AttemptId,
        coordinatorFencingEpoch = bootstrap.CoordinatorFencingEpoch,
        attemptFencingToken = bootstrap.AttemptFencingToken,
    });
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
            payload.LongLength));
    int canonicalManifestByteLength = ManagedWorkerManifest.GetCanonicalBytes(
        bootstrap.StagedArtifactId,
        bootstrap.OutputRelativeName,
        sha,
        payload.LongLength).Length;
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
            Value = ManagedWorkerManifest.OutputSchemaVersion,
        },
    });
    SubmitStagedOutputResponse staged = await client.SubmitStagedOutputAsync(
        new SubmitStagedOutputRequest { Manifest = manifest })
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
            Outcome = WorkerTerminalOutcome.CompletedStaged,
            StagingReceiptId = staged.StagingReceiptId,
        }).ResponseAsync.ConfigureAwait(false);
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
        || bootstrap.MaximumOutputBytes is <= 0 or > 65_536
        || bootstrap.ExpiresAt <= DateTimeOffset.UtcNow
        || Convert.FromBase64String(bootstrap.OneUseNonceBase64).Length != 32)
    {
        throw new InvalidOperationException("The private bootstrap is invalid or expired.");
    }

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
        || slot.Kind != StagedArtifactKind.TypedResult)
    {
        throw new InvalidOperationException("The worker assignment exceeds its launch authority.");
    }
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
