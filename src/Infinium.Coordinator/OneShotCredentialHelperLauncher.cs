using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Coordinator;

public sealed record HelperProcessReceipt(
    int ProcessId,
    int ExitCode,
    string BinarySha256,
    HelperReceiptV2 Receipt,
    byte[] StagedResponseBytes,
    int InheritedPrivateHandleCount,
    int StandardProtocolHandleCount,
    int ListenerCount,
    int NetworkOperationCount,
    int NativeCredentialOperationCount,
    int ProcessTreeSurvivorCount,
    bool ProcessTreeTerminated,
    bool RetryAttempted,
    byte[]? NativeCallTraceBytes = null,
    byte[]? NativeEntryCleanupBytes = null,
    byte[]? NativeCanaryEvidenceBytes = null,
    bool ContainmentProbeExecuted = false,
    bool ExcludedHandleAccessible = false,
    int ActiveProcessCountBeforeJobClose = 0,
    int TotalContainedProcessCount = 0,
    bool NativeNamespaceReuseBlocked = false,
    string? NativeNamespaceReuseBlockReason = null);

internal sealed class CredentialNativeHelperFailureException(
    NativeHelperFailureEnvelope evidence,
    string assignmentId)
    : Exception("The native helper returned a bounded typed failure envelope.")
{
    internal NativeHelperFailureEnvelope Evidence { get; } = evidence;
    internal string AssignmentId { get; } = assignmentId;
    internal NativeHelperFailureContainmentEvidence? Containment { get; private set; }
    internal void AttachContainment(NativeHelperFailureContainmentEvidence value) => Containment = value;
}

internal sealed class CredentialNativeHelperEvidenceAmbiguityException(
    string assignmentId,
    string validationStage,
    Exception innerException,
    NativeHelperFailureEnvelopeSummary? envelopeSummary = null)
    : Exception("The native helper evidence could not be independently validated.", innerException)
{
    internal string AssignmentId { get; } = assignmentId;
    internal NativeHelperFailureEnvelopeSummary? EnvelopeSummary { get; } = envelopeSummary;
    internal string ValidationStage { get; } = validationStage is
        "terminal-frame" or "staged-response" or "runtime-metrics"
        or "native-failure-envelope-validation" or "native-failure-envelope-read"
        or "test-injected"
            ? validationStage
            : throw new ArgumentOutOfRangeException(
                nameof(validationStage), validationStage, "Unknown native helper evidence validation stage.");
    internal NativeHelperFailureContainmentEvidence? Containment { get; private set; }
    internal void AttachContainment(NativeHelperFailureContainmentEvidence value) => Containment = value;

    internal CredentialNativeHelperEvidenceAmbiguityException(string assignmentId, Exception innerException)
        : this(assignmentId, "test-injected", innerException) { }
}

internal sealed record NativeHelperFailureContainmentEvidence(
    int ProcessId,
    int ExitCode,
    int TotalContainedProcessCount,
    int ActiveProcessCountBeforeJobClose,
    int ProcessTreeSurvivorCount,
    bool ProcessTreeTerminated);

internal sealed record NativeHelperFailureEnvelopeSummary(
    string Stage,
    string Reason,
    int NativeCallTraceUtf8Bytes,
    string? NativeCallTraceSha256,
    int EntryCleanupUtf8Bytes,
    string? EntryCleanupSha256,
    int CanaryEvidenceUtf8Bytes,
    string? CanaryEvidenceSha256);

internal sealed record Wp9NonLiveProbeResult(
    string Mode,
    NativeHelperFailureEnvelope? Envelope,
    string Terminal,
    int ExitCode,
    int Survivors);

public sealed class OneShotCredentialHelperLauncher
{
    private readonly string helperBinary;
    private readonly string expectedBinarySha256;
    private readonly string secureStoreRoot;
    private readonly string? nativeQualificationManifestPath;
    private readonly string? nativeQualificationManifestSha256;
    private readonly string? nativeQualificationManifestId;
    private readonly bool productionProviderTransport;
    private readonly bool wp9ProductionEnrollment;
    private readonly bool wp9CampaignProvider;

    internal TimeSpan OperationTimeout => wp9ProductionEnrollment
        ? TimeSpan.FromMinutes(11)
        : nativeQualificationManifestPath is null
            ? TimeSpan.FromSeconds(30)
            : CredentialNativeQualificationSupervisor.PrimaryPhaseTimeout;
    internal int ExpectedInheritedPrivateHandleCount => nativeQualificationManifestPath is null ? 3 : 2;

    internal async Task<Wp9NonLiveProbeResult> ExecuteWp9NonLiveProbeAsync(
        string mode,
        TimeSpan timeout)
    {
        if (mode is not ("typed" or "eof" or "crash" or "timeout")
            || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromSeconds(5))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        using AnonymousPipeServerStream response = new(
            PipeDirection.In, HandleInheritability.Inheritable, 64 * 1024);
        nint responseHandle = response.ClientSafePipeHandle.DangerousGetHandle();
        string[] arguments = [
            "--wp9-production-nonlive-probe", "--response-handle",
            responseHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--mode", mode,
        ];
        using WindowsContainedWorkerProcess.PrivateHelperProcess contained =
            WindowsContainedWorkerProcess.CreatePrivateHelper(
                helperBinary, arguments, Path.GetDirectoryName(helperBinary)!,
                PrivateHelperEnvironment(), [responseHandle]);
        response.DisposeLocalCopyOfClientHandle();
        contained.Resume();
        using CancellationTokenSource bounded = new(timeout);
        NativeHelperFailureEnvelope? envelope = null;
        string terminal;
        try
        {
            byte[] prefix = new byte[4];
            await response.ReadExactlyAsync(prefix, bounded.Token).ConfigureAwait(false);
            if (!NativeHelperFailureProtocol.IsMagic(prefix))
            {
                throw new InvalidDataException("The WP9 non-live helper probe did not emit the typed failure magic.");
            }
            envelope = await NativeHelperFailureProtocol.ReadAfterMagicAsync(response, bounded.Token)
                .ConfigureAwait(false);
            terminal = "typed-envelope";
        }
        catch (OperationCanceledException)
        {
            terminal = "timeout";
        }
        catch (EndOfStreamException)
        {
            terminal = "eof";
        }
        catch (InvalidDataException)
        {
            terminal = "invalid-frame";
        }
        if (!contained.Process.HasExited)
        {
            contained.Process.Kill(entireProcessTree: true);
            await contained.Process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        int exitCode;
        try { exitCode = contained.ExitCode; }
        catch (OverflowException) { exitCode = -1; }
        (int _, int survivors) = await contained.TerminateRemainingProcessesAndWaitAsync(
            TimeSpan.FromSeconds(2), CancellationToken.None).ConfigureAwait(false);
        contained.CloseJob();
        return new(mode, envelope, terminal, exitCode, survivors);
    }

    public OneShotCredentialHelperLauncher(
        string helperBinary,
        string expectedBinarySha256,
        string secureStoreRoot)
        : this(
            helperBinary,
            expectedBinarySha256,
            secureStoreRoot,
            nativeQualificationManifestPath: null,
            nativeQualificationManifestSha256: null,
            nativeQualificationManifestId: null,
            productionProviderTransport: false,
            wp9ProductionEnrollment: false,
            wp9CampaignProvider: false)
    {
    }

    private OneShotCredentialHelperLauncher(
        string helperBinary,
        string expectedBinarySha256,
        string secureStoreRoot,
        string? nativeQualificationManifestPath,
        string? nativeQualificationManifestSha256,
        string? nativeQualificationManifestId,
        bool productionProviderTransport,
        bool wp9ProductionEnrollment,
        bool wp9CampaignProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helperBinary);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBinarySha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(secureStoreRoot);
        this.helperBinary = Path.GetFullPath(helperBinary);
        this.secureStoreRoot = Path.GetFullPath(secureStoreRoot);
        this.nativeQualificationManifestPath = nativeQualificationManifestPath is null
            ? null
            : Path.GetFullPath(nativeQualificationManifestPath);
        this.nativeQualificationManifestSha256 = nativeQualificationManifestSha256;
        this.nativeQualificationManifestId = nativeQualificationManifestId;
        this.productionProviderTransport = productionProviderTransport;
        this.wp9ProductionEnrollment = wp9ProductionEnrollment;
        this.wp9CampaignProvider = wp9CampaignProvider;
        this.expectedBinarySha256 = expectedBinarySha256.ToLowerInvariant();
        if (!Path.IsPathFullyQualified(this.helperBinary) || !File.Exists(this.helperBinary)
            || !string.Equals(Path.GetFileName(this.helperBinary), "Infinium.CredentialHelper.exe", StringComparison.Ordinal)
            || this.expectedBinarySha256.Length != 64
            || !string.Equals(HashFile(this.helperBinary), this.expectedBinarySha256, StringComparison.Ordinal))
        {
            throw new ArgumentException("The launcher requires the exact fingerprinted repository-built helper executable.", nameof(helperBinary));
        }
        Directory.CreateDirectory(this.secureStoreRoot);
    }

    public static OneShotCredentialHelperLauncher CreateProductionProvider(
        string helperBinary,
        string expectedBinarySha256,
        string secureStoreRoot) => new(
            helperBinary,
            expectedBinarySha256,
            secureStoreRoot,
            nativeQualificationManifestPath: null,
            nativeQualificationManifestSha256: null,
            nativeQualificationManifestId: null,
            productionProviderTransport: true,
            wp9ProductionEnrollment: false,
            wp9CampaignProvider: false);

    internal static OneShotCredentialHelperLauncher CreateWp9CampaignProvider(
        string helperBinary,
        string expectedBinarySha256,
        string credentialManifestPath,
        string credentialManifestSha256,
        string credentialManifestId) => new(
            helperBinary,
            expectedBinarySha256,
            Path.GetDirectoryName(Path.GetFullPath(credentialManifestPath))!,
            Path.GetFullPath(credentialManifestPath),
            credentialManifestSha256,
            credentialManifestId,
            productionProviderTransport: true,
            wp9ProductionEnrollment: false,
            wp9CampaignProvider: true);

    internal static OneShotCredentialHelperLauncher CreateNativeQualification(
        string helperBinary,
        string expectedBinarySha256,
        string acceptedManifestPath,
        string acceptedManifestSha256,
        string acceptedManifestId)
    {
        string manifest = Path.GetFullPath(acceptedManifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedManifestSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(acceptedManifestId);
        string normalizedSha256 = acceptedManifestSha256.ToLowerInvariant();
        if (!File.Exists(manifest))
        {
            throw new FileNotFoundException("The exact accepted native qualification manifest is required.", manifest);
        }
        if (normalizedSha256.Length != 64
            || !normalizedSha256.All(char.IsAsciiHexDigit)
            || !string.Equals(HashFile(manifest), normalizedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The native qualification manifest does not match its exact accepted SHA-256.");
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifest));
        if (!string.Equals(
            document.RootElement.GetProperty("manifest_id").GetString(),
            acceptedManifestId,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException("The native qualification manifest does not match its exact accepted identity.");
        }
        return new(
            helperBinary,
            expectedBinarySha256,
            Path.GetDirectoryName(manifest)
                ?? throw new InvalidDataException("The native qualification manifest has no parent directory."),
            manifest,
            normalizedSha256,
            acceptedManifestId,
            productionProviderTransport: false,
            wp9ProductionEnrollment: false,
            wp9CampaignProvider: false);
    }

    internal static OneShotCredentialHelperLauncher CreateWp9ProductionEnrollment(
        string helperBinary,
        string expectedBinarySha256,
        string acceptedManifestPath,
        string acceptedManifestSha256,
        string acceptedManifestId)
    {
        string manifest = Path.GetFullPath(acceptedManifestPath);
        if (!File.Exists(manifest)
            || acceptedManifestSha256.Length != 64
            || !acceptedManifestSha256.All(char.IsAsciiHexDigit)
            || !string.Equals(HashFile(manifest), acceptedManifestSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The WP9 production enrollment manifest is not the exact accepted artifact.");
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(manifest));
        if (document.RootElement.GetProperty("manifest_id").GetString() != acceptedManifestId
            || document.RootElement.GetProperty("status").GetString() != "ready-for-owner-acceptance")
        {
            throw new InvalidDataException("The WP9 production enrollment manifest identity or status is invalid.");
        }
        return new(
            helperBinary,
            expectedBinarySha256,
            Path.GetDirectoryName(manifest)!,
            manifest,
            acceptedManifestSha256,
            acceptedManifestId,
            productionProviderTransport: false,
            wp9ProductionEnrollment: true,
            wp9CampaignProvider: false);
    }

    internal static OneShotCredentialHelperLauncher CreateSuccessorCredentialReplacement(
        string helperBinary,
        string expectedBinarySha256,
        string acceptedAuthorityPath,
        string acceptedAuthoritySha256,
        string acceptedAuthorityId)
    {
        string authority = Path.GetFullPath(acceptedAuthorityPath);
        if (!File.Exists(authority)
            || acceptedAuthoritySha256.Length != 64
            || !acceptedAuthoritySha256.All(char.IsAsciiHexDigit)
            || !string.Equals(HashFile(authority), acceptedAuthoritySha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The successor credential replacement authority is not exact.");
        }
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(authority));
        string schema = document.RootElement.GetProperty("schema_identity").GetString() ?? "";
        if (schema != "infinium.repository.m1-slice6-successor-credential-replacement-authorization/1.0.0"
            || document.RootElement.GetProperty("authority_id").GetString() != acceptedAuthorityId
            || document.RootElement.GetProperty("status").GetString()
                != "independently-reviewed-ready-for-owner-effect")
        {
            throw new InvalidDataException("The successor credential replacement authority identity or status is invalid.");
        }
        return new(
            helperBinary,
            expectedBinarySha256,
            Path.GetDirectoryName(authority)!,
            authority,
            acceptedAuthoritySha256,
            acceptedAuthorityId,
            productionProviderTransport: false,
            wp9ProductionEnrollment: true,
            wp9CampaignProvider: false);
    }

    public async Task<HelperProcessReceipt> ExecuteAsync(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        HelperPrivateFrameV2? finalRevalidation,
        TimeSpan timeout,
        DateTimeOffset? authoritativeNow = null,
        CancellationToken cancellationToken = default)
    {
        nint sentinel = 0;
        try
        {
            bool native = nativeQualificationManifestPath is not null;
            if (native)
            {
                SECURITY_ATTRIBUTES attributes = new()
                {
                    Length = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
                    InheritHandle = true,
                };
                sentinel = CreateEventW(ref attributes, manualReset: true, initialState: false, null);
                if (sentinel == 0)
                {
                    throw new System.ComponentModel.Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "The native qualification inheritance sentinel could not be created.");
                }
            }
            return await ExecuteCoreAsync(
                bootstrap, assignment, finalRevalidation, timeout, authoritativeNow,
                sentinel, containmentProbe: native, cancellationToken,
                nativeManifestPath: nativeQualificationManifestPath,
                nativeManifestSha256: nativeQualificationManifestSha256,
                nativeManifestId: nativeQualificationManifestId).ConfigureAwait(false);
        }
        finally
        {
            if (sentinel != 0)
            {
                _ = CloseHandle(sentinel);
            }
        }
    }

    internal async Task<HelperProcessReceipt> ExecuteContainmentProbeAsync(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        TimeSpan timeout,
        DateTimeOffset authoritativeNow,
        nint inheritanceSentinel,
        TimeSpan? descendantLifetime = null,
        TimeSpan? postEngineDelay = null,
        CancellationToken cancellationToken = default) => await ExecuteCoreAsync(
            bootstrap, assignment, null, timeout, authoritativeNow,
            inheritanceSentinel, containmentProbe: true, cancellationToken,
            containmentDescendantLifetime: descendantLifetime,
            containmentPostEngineDelay: postEngineDelay).ConfigureAwait(false);

    internal async Task<HelperProcessReceipt> ExecuteNativeContainmentProbeAsync(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        HelperPrivateFrameV2? finalRevalidation,
        DateTimeOffset authoritativeNow,
        nint inheritanceSentinel,
        CancellationToken cancellationToken = default)
    {
        if (nativeQualificationManifestPath is null)
        {
            throw new InvalidOperationException("A native qualification containment probe requires exact accepted manifest authority.");
        }
        return await ExecuteCoreAsync(
            bootstrap,
            assignment,
            finalRevalidation,
            OperationTimeout,
            authoritativeNow,
            inheritanceSentinel,
            containmentProbe: true,
            cancellationToken,
            nativeQualificationManifestPath,
            nativeQualificationManifestSha256,
            nativeQualificationManifestId).ConfigureAwait(false);
    }

    internal void ArmExactDeleteFailure(string profileId, string generationId)
    {
        static bool Valid(string value) => value.Length is > 0 and <= 120
            && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');
        if (!Valid(profileId) || !Valid(generationId))
        {
            throw new InvalidDataException("The injected exact synthetic credential slot is invalid.");
        }
        string path = Path.Combine(secureStoreRoot, "synthetic-secure-store.v1.json");
        JsonObject state = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path))?.AsObject()
                ?? throw new InvalidDataException("The fake secure store is malformed.")
            : new JsonObject();
        JsonArray failures = state["DeleteFailures"] as JsonArray ?? [];
        state["DeleteFailures"] = failures;
        string exactSlot = $"WP3-REAL-CHILD-TARGET-CANARY/{profileId}/{generationId}";
        if (!failures.Any(node => node?.GetValue<string>() == exactSlot))
        {
            failures.Add(exactSlot);
        }
        File.WriteAllText(path, state.ToJsonString());
    }

    private async Task<HelperProcessReceipt> ExecuteCoreAsync(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        HelperPrivateFrameV2? finalRevalidation,
        TimeSpan timeout,
        DateTimeOffset? authoritativeNow,
        nint inheritanceSentinel,
        bool containmentProbe,
        CancellationToken cancellationToken,
        string? nativeManifestPath = null,
        string? nativeManifestSha256 = null,
        string? nativeManifestId = null,
        TimeSpan? containmentDescendantLifetime = null,
        TimeSpan? containmentPostEngineDelay = null)
    {
        TimeSpan maximumTimeout = wp9ProductionEnrollment
            ? TimeSpan.FromMinutes(11)
            : nativeManifestPath is null
                ? TimeSpan.FromMinutes(2)
                : CredentialNativeQualificationSupervisor.PrimaryPhaseTimeout;
        if (timeout <= TimeSpan.Zero || timeout > maximumTimeout)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        ValidateOutboundSequence(bootstrap, assignment, finalRevalidation);
        string launchHash = HashFile(helperBinary);
        if (!string.Equals(launchHash, expectedBinarySha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The exact helper binary changed after launcher construction.");
        }
        DateTimeOffset now = authoritativeNow ?? DateTimeOffset.UtcNow;

        using AnonymousPipeServerStream request = new(PipeDirection.Out, HandleInheritability.Inheritable, 64 * 1024);
        using AnonymousPipeServerStream response = new(PipeDirection.In, HandleInheritability.Inheritable, 64 * 1024);
        nint requestHandle = request.ClientSafePipeHandle.DangerousGetHandle();
        nint responseHandle = response.ClientSafePipeHandle.DangerousGetHandle();
        using SafeFileHandle? storeHandle = nativeManifestPath is null
            ? OpenDirectoryCapability(secureStoreRoot)
            : null;
        nint directoryHandle = storeHandle?.DangerousGetHandle() ?? 0;
        string[] arguments = nativeManifestPath is null
            ? [
                "--request-handle", requestHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--response-handle", responseHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--store-handle", directoryHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--authority-now-unix-ms", now.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ]
            : wp9ProductionEnrollment
            ? [
                "--wp9-production-enrollment-request-handle", requestHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--response-handle", responseHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--manifest", nativeManifestPath,
                "--manifest-sha256", nativeManifestSha256
                    ?? throw new InvalidOperationException("WP9 production enrollment requires the exact accepted manifest SHA-256."),
                "--manifest-id", nativeManifestId
                    ?? throw new InvalidOperationException("WP9 production enrollment requires the exact accepted manifest identity."),
                "--authority-now-unix-ms", now.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ]
            : wp9CampaignProvider
            ? [
                "--wp9-campaign-provider-request-handle", requestHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--response-handle", responseHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--credential-manifest", nativeManifestPath,
                "--credential-manifest-sha256", nativeManifestSha256
                    ?? throw new InvalidOperationException("Campaign provider dispatch requires the exact credential manifest SHA-256."),
                "--credential-manifest-id", nativeManifestId
                    ?? throw new InvalidOperationException("Campaign provider dispatch requires the exact credential manifest identity."),
                "--authority-now-unix-ms", now.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ]
            : [
                "--credential-native-request-handle", requestHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--response-handle", responseHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--manifest", nativeManifestPath,
                "--manifest-sha256", nativeManifestSha256
                    ?? throw new InvalidOperationException("Native qualification requires the exact accepted manifest SHA-256."),
                "--manifest-id", nativeManifestId
                    ?? throw new InvalidOperationException("Native qualification requires the exact accepted manifest identity."),
                "--authority-now-unix-ms", now.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ];
        if (containmentProbe)
        {
            arguments =
            [
                .. arguments,
                "--excluded-handle-probe",
                inheritanceSentinel.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--spawn-containment-probe",
                "1",
            ];
            if (containmentDescendantLifetime is not null || containmentPostEngineDelay is not null)
            {
                int lifetimeMilliseconds = checked((int)(containmentDescendantLifetime
                    ?? TimeSpan.FromSeconds(30)).TotalMilliseconds);
                int delayMilliseconds = checked((int)(containmentPostEngineDelay
                    ?? TimeSpan.Zero).TotalMilliseconds);
                if (lifetimeMilliseconds is < 1 or > 300_000
                    || delayMilliseconds is < 0 or > 300_000)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(containmentDescendantLifetime),
                        "Containment test timings must remain finite and bounded.");
                }
                arguments =
                [
                    .. arguments,
                    "--containment-probe-lifetime-ms",
                    lifetimeMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--post-engine-delay-ms",
                    delayMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ];
            }
        }
        if (nativeManifestPath is null)
        {
            arguments =
            [
                .. arguments,
                "--provider-transport",
                productionProviderTransport ? "production" : "synthetic-qualification",
            ];
        }
        IReadOnlyDictionary<string, string> environment = PrivateHelperEnvironment();

        using WindowsContainedWorkerProcess.PrivateHelperProcess contained =
            WindowsContainedWorkerProcess.CreatePrivateHelper(
                helperBinary,
                arguments,
                Path.GetDirectoryName(helperBinary)!,
                environment,
                nativeManifestPath is null
                    ? [requestHandle, responseHandle, directoryHandle]
                    : [requestHandle, responseHandle]);
        int processId = contained.Process.Id;
        request.DisposeLocalCopyOfClientHandle();
        response.DisposeLocalCopyOfClientHandle();
        contained.Resume();
        using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        try
        {
            await HelperPrivateProtocolV2.WriteAsync(request, bootstrap, bounded.Token).ConfigureAwait(false);
            await HelperPrivateProtocolV2.WriteAsync(request, assignment, bounded.Token).ConfigureAwait(false);
            if (finalRevalidation is not null)
            {
                await HelperPrivateProtocolV2.WriteAsync(request, finalRevalidation, bounded.Token).ConfigureAwait(false);
            }
            HelperPrivateFrameV2 terminal = await ReadQualificationEvidenceAsync(
                assignment.Assignment.AssignmentId,
                "terminal-frame",
                nativeManifestPath is not null,
                () => ReadTerminalOrFailureAsync(
                    response, bootstrap.Bootstrap, assignment.Assignment, nativeManifestPath, processId,
                    finalRevalidation is null ? 3UL : 4UL, bounded.Token)).ConfigureAwait(false);
            byte[] stagedResponse = await ReadQualificationEvidenceAsync(
                assignment.Assignment.AssignmentId,
                "staged-response",
                nativeManifestPath is not null,
                () => ReadStagedResponseAsync(
                    response, bootstrap.Bootstrap, assignment.Assignment, nativeManifestPath, processId, bounded.Token)
                ).ConfigureAwait(false);
            HelperRuntimeMetrics metrics = await ReadQualificationEvidenceAsync(
                assignment.Assignment.AssignmentId,
                "runtime-metrics",
                nativeManifestPath is not null,
                () => ReadMetricsAsync(
                    response, bootstrap.Bootstrap, assignment.Assignment, nativeManifestPath, processId, bounded.Token)
                ).ConfigureAwait(false);
            request.Close();
            await contained.Process.WaitForExitAsync(bounded.Token).ConfigureAwait(false);
            bool expectedContainedCrash = nativeManifestPath is not null
                && assignment.Assignment.AssignmentId ==
                    "wp4-v2/helper-and-coordinator-crash-restart/half-commit"
                && contained.ExitCode == 69;
            if ((!expectedContainedCrash && contained.ExitCode != 0)
                || terminal.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.Receipt)
            {
                throw new InvalidOperationException("The one-shot helper failed without an admissible terminal receipt.");
            }
            int totalContainedProcessCount = contained.TotalProcessCount;
            (int activeBeforeContainmentClose, int survivors) =
                await contained.TerminateRemainingProcessesAndWaitAsync(
                    TimeSpan.FromSeconds(5),
                    bounded.Token).ConfigureAwait(false);
            contained.CloseJob();
            return new(
                processId,
                contained.ExitCode,
                launchHash,
                terminal.Receipt,
                stagedResponse,
                ExpectedInheritedPrivateHandleCount,
                0,
                metrics.ListenerCount,
                metrics.NetworkOperationCount,
                metrics.NativeCredentialOperationCount,
                survivors,
                ValidateContainmentEvidence(
                    containmentProbe, metrics.DescendantPid,
                    totalContainedProcessCount, survivors)
                    && !metrics.ExcludedHandleAccessible,
                false,
                metrics.NativeCallTraceBytes,
                metrics.NativeEntryCleanupBytes,
                metrics.NativeCanaryEvidenceBytes,
                containmentProbe,
                metrics.ExcludedHandleAccessible,
                activeBeforeContainmentClose,
                totalContainedProcessCount,
                metrics.NamespaceReuseBlocked,
                metrics.NamespaceReuseBlockReason);
        }
        catch (Exception failure) when (failure is CredentialNativeHelperFailureException
            or CredentialNativeHelperEvidenceAmbiguityException
            || wp9ProductionEnrollment && (failure is OperationCanceledException or TimeoutException))
        {
            Exception retainedFailure = failure;
            if (wp9ProductionEnrollment && (failure is OperationCanceledException or TimeoutException))
            {
                retainedFailure = NormalizeWp9ProductionTimeout(
                    assignment.Assignment.AssignmentId, failure);
            }
            NativeHelperFailureContainmentEvidence containment;
            try
            {
                if (!contained.Process.HasExited)
                {
                    using CancellationTokenSource helperExit = new(TimeSpan.FromSeconds(5));
                    try
                    {
                        await contained.Process.WaitForExitAsync(helperExit.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Job termination below remains the bounded terminal authority.
                    }
                }
                int measuredExitCode = contained.ExitCode;
                int exitCode = measuredExitCode == 259 ? -1 : measuredExitCode;
                int totalContained = contained.TotalProcessCount;
                (int activeBeforeClose, int survivors) = await contained.TerminateRemainingProcessesAndWaitAsync(
                    TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                contained.CloseJob();
                bool descendantExpected = retainedFailure is CredentialNativeHelperFailureException typed
                    && typed.Evidence.ContainmentDescendantStarted;
                containment = new(
                    processId,
                    exitCode,
                    totalContained,
                    activeBeforeClose,
                    survivors,
                    survivors == 0 && totalContained >= (descendantExpected ? 2 : 1));
            }
            catch (Exception containmentFailure) when (containmentFailure is
                IOException or InvalidOperationException or OperationCanceledException
                or TimeoutException or System.ComponentModel.Win32Exception)
            {
                try { contained.CloseJob(); }
                catch (Exception closeFailure) when (closeFailure is
                    InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // The conservative retained evidence below is terminal authority.
                }
                containment = new(
                    processId,
                    -1,
                    0,
                    0,
                    1,
                    ProcessTreeTerminated: false);
            }
            if (retainedFailure is CredentialNativeHelperFailureException helperFailure)
            {
                helperFailure.AttachContainment(containment);
            }
            else
            {
                ((CredentialNativeHelperEvidenceAmbiguityException)retainedFailure).AttachContainment(containment);
            }
            throw retainedFailure;
        }
        catch
        {
            if (!contained.Process.HasExited)
            {
                contained.Process.Kill(entireProcessTree: true);
                await contained.Process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            throw;
        }
    }

    internal static IReadOnlyDictionary<string, string> PrivateHelperEnvironment() =>
        PrivateHelperEnvironment(Environment.GetFolderPath(Environment.SpecialFolder.Windows));

    internal static IReadOnlyDictionary<string, string> PrivateHelperEnvironment(string? rawSystemRoot)
    {
        if (string.IsNullOrWhiteSpace(rawSystemRoot) || !Path.IsPathFullyQualified(rawSystemRoot))
        {
            throw new InvalidOperationException("The contained helper requires the exact Windows system root.");
        }
        string systemRoot = Path.GetFullPath(rawSystemRoot);
        if (!Path.IsPathFullyQualified(systemRoot) || !Directory.Exists(systemRoot))
        {
            throw new InvalidOperationException("The contained helper requires the exact Windows system root.");
        }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = systemRoot,
            ["DOTNET_EnableDiagnostics"] = "0",
        };
    }

    private static CredentialNativeHelperEvidenceAmbiguityException NormalizeWp9ProductionTimeout(
        string assignmentId,
        Exception failure) => failure is OperationCanceledException or TimeoutException
            ? new CredentialNativeHelperEvidenceAmbiguityException(
                assignmentId, "runtime-metrics", failure)
            : throw new ArgumentOutOfRangeException(nameof(failure));

    internal static CredentialNativeHelperEvidenceAmbiguityException NormalizeWp9ProductionTimeoutForTest(
        string assignmentId,
        Exception failure) => NormalizeWp9ProductionTimeout(assignmentId, failure);

    internal static async Task<T> ReadQualificationEvidenceAsync<T>(
        string assignmentId,
        string validationStage,
        bool retainAmbiguity,
        Func<Task<T>> read)
    {
        try
        {
            return await read().ConfigureAwait(false);
        }
        catch (Exception exception) when (retainAmbiguity
            && exception is (EndOfStreamException or InvalidDataException or JsonException
                or NotSupportedException or FormatException or OverflowException))
        {
            throw new CredentialNativeHelperEvidenceAmbiguityException(
                assignmentId, validationStage, exception);
        }
    }

    private static async Task<HelperRuntimeMetrics> ReadMetricsAsync(
        Stream response,
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        string? nativeManifestPath,
        int helperProcessId,
        CancellationToken cancellationToken)
    {
        byte[] prefix = new byte[4];
        await response.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        if (NativeHelperFailureProtocol.IsMagic(prefix))
        {
            throw await ReadValidatedNativeFailureAsync(
                response, bootstrap, assignment, nativeManifestPath, helperProcessId, cancellationToken).ConfigureAwait(false);
        }
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        byte[] bytes = new byte[NativeHelperRuntimeMetricsProtocol.ValidateLength(length)];
        await response.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        return new(
            root.GetProperty("excluded_handle_accessible").GetBoolean(),
            root.GetProperty("descendant_pid").GetInt32(),
            root.GetProperty("listener_count").GetInt32(),
            root.GetProperty("network_operation_count").GetInt32(),
            root.GetProperty("native_credential_operation_count").GetInt32(),
            root.TryGetProperty("native_call_trace", out JsonElement trace)
                ? JsonSerializer.SerializeToUtf8Bytes(trace)
                : null,
            root.TryGetProperty("entry_cleanup", out JsonElement entryCleanup)
                && entryCleanup.ValueKind != JsonValueKind.Null
                ? JsonSerializer.SerializeToUtf8Bytes(entryCleanup)
                : null,
            root.TryGetProperty("canaries", out JsonElement canaries)
                ? JsonSerializer.SerializeToUtf8Bytes(canaries)
                : null,
            root.TryGetProperty("namespace_reuse_blocked", out JsonElement blocked)
                && blocked.GetBoolean(),
            root.TryGetProperty("namespace_reuse_block_reason", out JsonElement blockReason)
                && blockReason.ValueKind == JsonValueKind.String
                ? blockReason.GetString()
                : null);
    }

    private static async Task<byte[]> ReadStagedResponseAsync(
        Stream response,
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        string? nativeManifestPath,
        int helperProcessId,
        CancellationToken cancellationToken)
    {
        byte[] prefix = new byte[4];
        await response.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        if (NativeHelperFailureProtocol.IsMagic(prefix))
        {
            throw await ReadValidatedNativeFailureAsync(
                response, bootstrap, assignment, nativeManifestPath, helperProcessId, cancellationToken).ConfigureAwait(false);
        }
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        ulong maximum = assignment.Limits?.MaximumStagedOutputBytes ?? 0;
        if (length > maximum || length > HelperPrivateProtocolV2.MaximumStagingBytes)
        {
            throw new InvalidDataException("The helper staged response exceeds its exact bound.");
        }
        byte[] bytes = new byte[length];
        if (length > 0)
        {
            await response.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        return bytes;
    }

    private static async Task<HelperPrivateFrameV2> ReadTerminalOrFailureAsync(
        Stream response,
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        string? nativeManifestPath,
        int helperProcessId,
        ulong expectedSequence,
        CancellationToken cancellationToken)
    {
        byte[] prefix = new byte[4];
        await response.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        if (NativeHelperFailureProtocol.IsMagic(prefix))
        {
            throw await ReadValidatedNativeFailureAsync(
                response, bootstrap, assignment, nativeManifestPath, helperProcessId, cancellationToken).ConfigureAwait(false);
        }
        return await HelperPrivateProtocolV2.ReadAfterPrefixAsync(
            response, prefix, expectedSequence, cancellationToken).ConfigureAwait(false);
    }

    private static Exception ValidateNativeFailure(
        NativeHelperFailureEnvelope evidence,
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        string? nativeManifestPath,
        int helperProcessId)
    {
        try
        {
            CredentialNativeQualificationSupervisor.ValidateNativeHelperFailureEnvelope(
                evidence, bootstrap, assignment, nativeManifestPath, helperProcessId);
            return new CredentialNativeHelperFailureException(evidence, assignment.AssignmentId);
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException
            or KeyNotFoundException or FormatException or OverflowException or ArgumentException
            or InvalidOperationException)
        {
            return new CredentialNativeHelperEvidenceAmbiguityException(
                assignment.AssignmentId, "native-failure-envelope-validation", exception,
                SummarizeFailureEnvelope(evidence));
        }
    }

    internal static Exception ValidateWp9FailureForTest(
        NativeHelperFailureEnvelope evidence,
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        string manifestPath) => ValidateNativeFailure(
            evidence, bootstrap, assignment, manifestPath, helperProcessId: 1);

    private static NativeHelperFailureEnvelopeSummary SummarizeFailureEnvelope(
        NativeHelperFailureEnvelope evidence)
    {
        static (int Length, string? Sha256) Summary(string? value)
        {
            if (value is null) { return (0, null); }
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
            return (bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
        (int traceLength, string? traceHash) = Summary(evidence.NativeCallTraceJson);
        (int entryLength, string? entryHash) = Summary(evidence.EntryCleanupJson);
        (int canaryLength, string? canaryHash) = Summary(evidence.CanaryEvidenceJson);
        return new(evidence.Stage, evidence.Reason, traceLength, traceHash,
            entryLength, entryHash, canaryLength, canaryHash);
    }

    private static async Task<Exception> ReadValidatedNativeFailureAsync(
        Stream response,
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        string? nativeManifestPath,
        int helperProcessId,
        CancellationToken cancellationToken)
    {
        try
        {
            NativeHelperFailureEnvelope evidence = await NativeHelperFailureProtocol.ReadAfterMagicAsync(
                response, cancellationToken).ConfigureAwait(false);
            return ValidateNativeFailure(evidence, bootstrap, assignment, nativeManifestPath, helperProcessId);
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException
            or JsonException or NotSupportedException or FormatException or OverflowException)
        {
            return new CredentialNativeHelperEvidenceAmbiguityException(
                assignment.AssignmentId, "native-failure-envelope-read", exception);
        }
    }

    private static void ValidateOutboundSequence(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        HelperPrivateFrameV2? finalRevalidation)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(assignment);
        if (bootstrap.Sequence != 1 || bootstrap.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.Bootstrap
            || assignment.Sequence != 2 || assignment.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.Assignment)
        {
            throw new InvalidDataException("The helper launch requires one exact bootstrap and immutable assignment.");
        }
        bool dispatch = assignment.Assignment.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch;
        if (dispatch != (finalRevalidation is not null)
            || finalRevalidation is not null && (finalRevalidation.Sequence != 3
                || finalRevalidation.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.DispatchRevalidation))
        {
            throw new InvalidDataException("Provider dispatch requires exactly one final revalidation; credential operations forbid it.");
        }
    }

    private static string HashFile(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    internal static bool ValidateContainmentEvidence(
        bool probeExecuted,
        int reportedDescendantPid,
        int totalContainedProcessCount,
        int activeProcessCountAfterTermination) =>
        activeProcessCountAfterTermination == 0
        && (!probeExecuted || reportedDescendantPid > 0 && totalContainedProcessCount >= 2);

    private static SafeFileHandle OpenDirectoryCapability(string path)
    {
        SafeFileHandle handle = CreateFileW(
            path,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            0,
            OPEN_EXISTING,
            FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
            0);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "The fake secure-store capability could not be opened.");
        }
        return handle;
    }

    private sealed record HelperRuntimeMetrics(
        bool ExcludedHandleAccessible,
        int DescendantPid,
        int ListenerCount,
        int NetworkOperationCount,
        int NativeCredentialOperationCount,
        byte[]? NativeCallTraceBytes,
        byte[]? NativeEntryCleanupBytes,
        byte[]? NativeCanaryEvidenceBytes,
        bool NamespaceReuseBlocked,
        string? NamespaceReuseBlockReason);

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int Length;
        public nint SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)] public bool InheritHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateEventW(
        ref SECURITY_ATTRIBUTES attributes,
        [MarshalAs(UnmanagedType.Bool)] bool manualReset,
        [MarshalAs(UnmanagedType.Bool)] bool initialState,
        string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName, uint desiredAccess, uint shareMode, nint securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, nint templateFile);
}
