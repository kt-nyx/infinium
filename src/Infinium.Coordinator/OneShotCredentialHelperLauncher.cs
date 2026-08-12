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
    bool RetryAttempted);

public sealed class OneShotCredentialHelperLauncher
{
    private readonly string helperBinary;
    private readonly string expectedBinarySha256;
    private readonly string secureStoreRoot;

    public OneShotCredentialHelperLauncher(
        string helperBinary,
        string expectedBinarySha256,
        string secureStoreRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helperBinary);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBinarySha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(secureStoreRoot);
        this.helperBinary = Path.GetFullPath(helperBinary);
        this.secureStoreRoot = Path.GetFullPath(secureStoreRoot);
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

    public async Task<HelperProcessReceipt> ExecuteAsync(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        HelperPrivateFrameV2? finalRevalidation,
        TimeSpan timeout,
        DateTimeOffset? authoritativeNow = null,
        CancellationToken cancellationToken = default) => await ExecuteCoreAsync(
            bootstrap, assignment, finalRevalidation, timeout, authoritativeNow,
            inheritanceSentinel: 0, containmentProbe: false, cancellationToken).ConfigureAwait(false);

    internal async Task<HelperProcessReceipt> ExecuteContainmentProbeAsync(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        TimeSpan timeout,
        DateTimeOffset authoritativeNow,
        nint inheritanceSentinel,
        CancellationToken cancellationToken = default) => await ExecuteCoreAsync(
            bootstrap, assignment, null, timeout, authoritativeNow,
            inheritanceSentinel, containmentProbe: true, cancellationToken).ConfigureAwait(false);

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
        DateTimeOffset now = authoritativeNow ?? DateTimeOffset.UtcNow;

        using AnonymousPipeServerStream request = new(PipeDirection.Out, HandleInheritability.Inheritable, 64 * 1024);
        using AnonymousPipeServerStream response = new(PipeDirection.In, HandleInheritability.Inheritable, 64 * 1024);
        using SafeFileHandle storeHandle = OpenDirectoryCapability(secureStoreRoot);
        nint requestHandle = request.ClientSafePipeHandle.DangerousGetHandle();
        nint responseHandle = response.ClientSafePipeHandle.DangerousGetHandle();
        nint directoryHandle = storeHandle.DangerousGetHandle();
        string[] arguments =
        [
            "--request-handle", requestHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--response-handle", responseHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--store-handle", directoryHandle.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
        }
        Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_EnableDiagnostics"] = "0",
        };

        using WindowsContainedWorkerProcess.PrivateHelperProcess contained =
            WindowsContainedWorkerProcess.CreatePrivateHelper(
                helperBinary,
                arguments,
                Path.GetDirectoryName(helperBinary)!,
                environment,
                [requestHandle, responseHandle, directoryHandle]);
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
            HelperPrivateFrameV2 terminal = await HelperPrivateProtocolV2.ReadAsync(
                response, finalRevalidation is null ? 3UL : 4UL, bounded.Token).ConfigureAwait(false);
            byte[] stagedResponse = await ReadStagedResponseAsync(response, assignment.Assignment, bounded.Token)
                .ConfigureAwait(false);
            HelperRuntimeMetrics metrics = await ReadMetricsAsync(response, bounded.Token).ConfigureAwait(false);
            request.Close();
            await contained.Process.WaitForExitAsync(bounded.Token).ConfigureAwait(false);
            if (contained.ExitCode != 0 || terminal.PayloadCase != HelperPrivateFrameV2.PayloadOneofCase.Receipt)
            {
                throw new InvalidOperationException("The one-shot helper failed without an admissible terminal receipt.");
            }
            int activeBeforeContainmentClose = contained.ActiveProcessCount;
            Process? descendant = metrics.DescendantPid > 0
                ? Process.GetProcessById(metrics.DescendantPid)
                : null;
            contained.CloseJob();
            if (descendant is not null)
            {
                await descendant.WaitForExitAsync(bounded.Token).ConfigureAwait(false);
                descendant.Dispose();
            }
            int survivors = metrics.DescendantPid > 0 && IsProcessAlive(metrics.DescendantPid) ? 1 : 0;
            return new(
                processId,
                contained.ExitCode,
                launchHash,
                terminal.Receipt,
                stagedResponse,
                3,
                0,
                metrics.ListenerCount,
                metrics.NetworkOperationCount,
                metrics.NativeCredentialOperationCount,
                survivors,
                survivors == 0 && (!containmentProbe || activeBeforeContainmentClose >= 1)
                    && !metrics.ExcludedHandleAccessible,
                false);
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

    private static async Task<HelperRuntimeMetrics> ReadMetricsAsync(
        Stream response,
        CancellationToken cancellationToken)
    {
        byte[] prefix = new byte[4];
        await response.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        if (length is 0 or > 4096)
        {
            throw new InvalidDataException("The helper runtime measurement record is out of bounds.");
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

    private static async Task<byte[]> ReadStagedResponseAsync(
        Stream response,
        HelperAssignmentV2 assignment,
        CancellationToken cancellationToken)
    {
        byte[] prefix = new byte[4];
        await response.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
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

    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private sealed record HelperRuntimeMetrics(
        bool ExcludedHandleAccessible,
        int DescendantPid,
        int ListenerCount,
        int NetworkOperationCount,
        int NativeCredentialOperationCount);

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName, uint desiredAccess, uint shareMode, nint securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, nint templateFile);
}
