using System.Buffers.Binary;
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
    bool ContainmentProbeExecuted = false,
    bool ExcludedHandleAccessible = false,
    int ActiveProcessCountBeforeJobClose = 0,
    int TotalContainedProcessCount = 0);

/// <summary>
/// Starts the credential helper exactly once with only its two private pipes and
/// an exact handle to the local secure-store directory. The launcher does not
/// pass credentials through arguments, environment variables, or inherited
/// standard streams.
/// </summary>
public sealed class OneShotCredentialHelperLauncher
{
    private readonly string helperBinary;
    private readonly string expectedBinarySha256;
    private readonly string secureStoreRoot;
    private readonly bool liveProviderTransport;
    private readonly TimeSpan operationTimeout = TimeSpan.FromSeconds(30);
    private readonly int expectedInheritedPrivateHandleCount = 3;

    internal TimeSpan OperationTimeout => operationTimeout;
    internal int ExpectedInheritedPrivateHandleCount => expectedInheritedPrivateHandleCount;
    internal string ReviewedBinarySha256 => expectedBinarySha256;

    public OneShotCredentialHelperLauncher(
        string helperBinary,
        string expectedBinarySha256,
        string secureStoreRoot)
        : this(helperBinary, expectedBinarySha256, secureStoreRoot, liveProviderTransport: false)
    {
    }

    private OneShotCredentialHelperLauncher(
        string helperBinary,
        string expectedBinarySha256,
        string secureStoreRoot,
        bool liveProviderTransport)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helperBinary);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBinarySha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(secureStoreRoot);
        this.helperBinary = Path.GetFullPath(helperBinary);
        this.secureStoreRoot = Path.GetFullPath(secureStoreRoot);
        this.liveProviderTransport = liveProviderTransport;
        this.expectedBinarySha256 = expectedBinarySha256.ToLowerInvariant();
        if (!Path.IsPathFullyQualified(this.helperBinary)
            || !File.Exists(this.helperBinary)
            || !string.Equals(
                Path.GetFileName(this.helperBinary),
                "Infinium.CredentialHelper.exe",
                StringComparison.Ordinal)
            || this.expectedBinarySha256.Length != 64
            || !this.expectedBinarySha256.All(char.IsAsciiHexDigit)
            || !string.Equals(HashFile(this.helperBinary), this.expectedBinarySha256, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The launcher requires the exact fingerprinted repository-built helper executable.",
                nameof(helperBinary));
        }
        Directory.CreateDirectory(this.secureStoreRoot);
    }

    public static OneShotCredentialHelperLauncher CreateProductionProvider(
        string helperBinary,
        string expectedBinarySha256,
        string secureStoreRoot) =>
        new(helperBinary, expectedBinarySha256, secureStoreRoot, liveProviderTransport: true);

    public Task<HelperProcessReceipt> ExecuteAsync(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        HelperPrivateFrameV2? finalRevalidation,
        TimeSpan timeout,
        DateTimeOffset? authoritativeNow = null,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(
            bootstrap,
            assignment,
            finalRevalidation,
            timeout,
            authoritativeNow ?? DateTimeOffset.UtcNow,
            inheritanceSentinel: 0,
            containmentProbe: false,
            descendantLifetime: null,
            postEngineDelay: null,
            cancellationToken);

    internal Task<HelperProcessReceipt> ExecuteContainmentProbeAsync(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        TimeSpan timeout,
        DateTimeOffset authoritativeNow,
        nint inheritanceSentinel,
        TimeSpan? descendantLifetime = null,
        TimeSpan? postEngineDelay = null,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(
            bootstrap,
            assignment,
            finalRevalidation: null,
            timeout,
            authoritativeNow,
            inheritanceSentinel,
            containmentProbe: true,
            descendantLifetime,
            postEngineDelay,
            cancellationToken);

    internal void ArmExactDeleteFailure(string profileId, string generationId)
    {
        static bool Valid(string value) => value.Length is > 0 and <= 120
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
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
        string exactSlot = $"CAPABILITY-BOUND-STORE-TARGET-CANARY/{profileId}/{generationId}";
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
        DateTimeOffset authoritativeNow,
        nint inheritanceSentinel,
        bool containmentProbe,
        TimeSpan? descendantLifetime,
        TimeSpan? postEngineDelay,
        CancellationToken cancellationToken)
    {
        if (timeout <= TimeSpan.Zero || timeout > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        ValidateOutboundSequence(bootstrap, assignment, finalRevalidation);
        string launchHash = HashFile(helperBinary);
        if (!string.Equals(launchHash, expectedBinarySha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The exact helper binary changed after launcher construction.");
        }

        using AnonymousPipeServerStream request =
            new(PipeDirection.Out, HandleInheritability.Inheritable, 64 * 1024);
        using AnonymousPipeServerStream response =
            new(PipeDirection.In, HandleInheritability.Inheritable, 64 * 1024);
        nint requestHandle = request.ClientSafePipeHandle.DangerousGetHandle();
        nint responseHandle = response.ClientSafePipeHandle.DangerousGetHandle();
        using SafeFileHandle storeHandle = OpenDirectoryCapability(secureStoreRoot);
        nint directoryHandle = storeHandle.DangerousGetHandle();
        List<string> arguments =
        [
            "--request-handle", requestHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--response-handle", responseHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--store-handle", directoryHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--authority-now-unix-ms",
            authoritativeNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
        ];
        if (containmentProbe)
        {
            arguments.AddRange(
            [
                "--excluded-handle-probe",
                inheritanceSentinel.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--spawn-containment-probe", "1",
            ]);
            if (descendantLifetime is not null || postEngineDelay is not null)
            {
                int lifetimeMilliseconds = checked((int)(descendantLifetime ?? TimeSpan.FromSeconds(30)).TotalMilliseconds);
                int delayMilliseconds = checked((int)(postEngineDelay ?? TimeSpan.Zero).TotalMilliseconds);
                if (lifetimeMilliseconds is < 1 or > 300_000 || delayMilliseconds is < 0 or > 300_000)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(descendantLifetime),
                        "Containment test timings must remain finite and bounded.");
                }
                arguments.AddRange(
                [
                    "--containment-probe-lifetime-ms",
                    lifetimeMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--post-engine-delay-ms",
                    delayMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ]);
            }
        }
        arguments.AddRange(
        [
            "--provider-transport",
            liveProviderTransport ? "production" : "synthetic-qualification",
        ]);

        using WindowsContainedWorkerProcess.PrivateHelperProcess contained =
            WindowsContainedWorkerProcess.CreatePrivateHelper(
                helperBinary,
                [.. arguments],
                Path.GetDirectoryName(helperBinary)!,
                PrivateHelperEnvironment(),
                [requestHandle, responseHandle, directoryHandle]);
        int processId = contained.Process.Id;
        request.DisposeLocalCopyOfClientHandle();
        response.DisposeLocalCopyOfClientHandle();
        contained.Resume();
        using CancellationTokenSource bounded =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(timeout);
        try
        {
            await HelperPrivateProtocolV2.WriteAsync(request, bootstrap, bounded.Token).ConfigureAwait(false);
            await HelperPrivateProtocolV2.WriteAsync(request, assignment, bounded.Token).ConfigureAwait(false);
            if (finalRevalidation is not null)
            {
                await HelperPrivateProtocolV2.WriteAsync(request, finalRevalidation, bounded.Token).ConfigureAwait(false);
            }
            ulong terminalSequence = finalRevalidation is null ? 3UL : 4UL;
            HelperPrivateFrameV2 terminal =
                await HelperPrivateProtocolV2.ReadAsync(response, terminalSequence, bounded.Token).ConfigureAwait(false);
            byte[] stagedResponse = await ReadStagedResponseAsync(
                response,
                assignment.Assignment,
                bounded.Token).ConfigureAwait(false);
            HelperRuntimeMetrics metrics = await ReadMetricsAsync(response, bounded.Token).ConfigureAwait(false);
            request.Close();
            await contained.Process.WaitForExitAsync(bounded.Token).ConfigureAwait(false);
            if (contained.ExitCode != 0
                || terminal.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.Receipt)
            {
                throw new InvalidOperationException(
                    "The one-shot helper failed without an admissible terminal receipt.");
            }
            int totalContained = contained.TotalProcessCount;
            (int activeBeforeClose, int survivors) =
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
                    containmentProbe,
                    metrics.DescendantPid,
                    totalContained,
                    survivors) && !metrics.ExcludedHandleAccessible,
                RetryAttempted: false,
                ContainmentProbeExecuted: containmentProbe,
                ExcludedHandleAccessible: metrics.ExcludedHandleAccessible,
                ActiveProcessCountBeforeJobClose: activeBeforeClose,
                TotalContainedProcessCount: totalContained);
        }
        catch
        {
            if (!contained.Process.HasExited)
            {
                contained.Process.Kill(entireProcessTree: true);
                await contained.Process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            try
            {
                _ = await contained.TerminateRemainingProcessesAndWaitAsync(
                    TimeSpan.FromSeconds(5),
                    CancellationToken.None).ConfigureAwait(false);
                contained.CloseJob();
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                    or System.ComponentModel.Win32Exception
                    or IOException)
            {
                // The original failure remains the useful result.
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
        if (!Directory.Exists(systemRoot))
        {
            throw new InvalidOperationException("The contained helper requires the exact Windows system root.");
        }
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = systemRoot,
            ["DOTNET_EnableDiagnostics"] = "0",
        };
    }

    private static async Task<byte[]> ReadStagedResponseAsync(
        Stream response,
        HelperAssignmentV2 assignment,
        CancellationToken cancellationToken)
    {
        byte[] lengthBytes = new byte[4];
        await response.ReadExactlyAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
        ulong maximum = assignment.Limits?.MaximumStagedOutputBytes ?? 0;
        if (length > maximum || length > HelperPrivateProtocolV2.MaximumStagingBytes)
        {
            throw new InvalidDataException("The helper staged response exceeds its exact bound.");
        }
        byte[] bytes = new byte[length];
        if (bytes.Length > 0)
        {
            await response.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        }
        return bytes;
    }

    private static async Task<HelperRuntimeMetrics> ReadMetricsAsync(
        Stream response,
        CancellationToken cancellationToken)
    {
        byte[] lengthBytes = new byte[4];
        await response.ReadExactlyAsync(lengthBytes, cancellationToken).ConfigureAwait(false);
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(lengthBytes);
        if (length is 0 or > 64 * 1024)
        {
            throw new InvalidDataException("The helper runtime metrics frame exceeds its exact bound.");
        }
        byte[] bytes = new byte[length];
        await response.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        return new(
            root.GetProperty("excluded_handle_accessible").GetBoolean(),
            root.GetProperty("descendant_pid").GetInt32(),
            root.GetProperty("listener_count").GetInt32(),
            root.GetProperty("network_operation_count").GetInt32(),
            root.GetProperty("native_credential_operation_count").GetInt32());
    }

    private static void ValidateOutboundSequence(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        HelperPrivateFrameV2? finalRevalidation)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(assignment);
        if (bootstrap.Sequence != 1
            || bootstrap.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.Bootstrap
            || assignment.Sequence != 2
            || assignment.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.Assignment)
        {
            throw new InvalidDataException(
                "The helper launch requires one exact bootstrap and immutable assignment.");
        }
        bool dispatch = assignment.Assignment.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch;
        if (dispatch != (finalRevalidation is not null)
            || finalRevalidation is not null
                && (finalRevalidation.Sequence != 3
                    || finalRevalidation.PayloadCase
                        != HelperPrivateFrameV2.PayloadOneofCase.DispatchRevalidation))
        {
            throw new InvalidDataException(
                "Provider dispatch requires exactly one final revalidation; credential operations forbid it.");
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
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            0,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            0);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "The fake secure-store capability could not be opened.");
        }
        return handle;
    }

    private sealed record HelperRuntimeMetrics(
        bool ExcludedHandleAccessible,
        int DescendantPid,
        int ListenerCount,
        int NetworkOperationCount,
        int NativeCredentialOperationCount);

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x1;
    private const uint FileShareWrite = 0x2;
    private const uint OpenExisting = 3;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagBackupSemantics = 0x02000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);
}
