using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Infinium.CredentialHelper;

if (args is ["--credential-native-recovery", "--manifest", string recoveryManifest,
    "--manifest-sha256", string recoverySha, "--manifest-id", string recoveryId,
    "--evidence", string recoveryEvidence])
{
    try
    {
        return WindowsCredentialNativeRecovery.Run(recoveryManifest, recoverySha, recoveryId, recoveryEvidence);
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException
        or InvalidOperationException or System.ComponentModel.Win32Exception)
    {
        Console.Error.WriteLine($"Native recovery terminated with typed non-secret failure: {exception.GetType().Name}");
        return 70;
    }
}

if (args is ["--credential-native-qualification", "--manifest", string manifestPath,
    "--evidence", string evidencePath])
{
    _ = manifestPath;
    _ = evidencePath;
    Console.Error.WriteLine("The consumed WP4 v1 native qualification entry point is terminally disabled.");
    return 66;
}

if (args is ["--credential-native-crash-probe", "--manifest", string crashManifestPath,
    "--target-alias", string targetAlias, "--count-evidence", string countEvidencePath])
{
    _ = crashManifestPath;
    _ = targetAlias;
    _ = countEvidencePath;
    Console.Error.WriteLine("The consumed WP4 v1 crash probe is terminally disabled.");
    return 66;
}

if (args is ["--credential-native-request-handle", string nativeRequestHandle,
    "--response-handle", string nativeResponseHandle,
    "--manifest", string nativeManifestPath,
    "--manifest-sha256", string nativeManifestSha256,
    "--manifest-id", string nativeManifestId,
    "--authority-now-unix-ms", string nativeAuthorityNow, .. string[] nativeOptions]
    && long.TryParse(nativeAuthorityNow, System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out long nativeAuthorityNowUnixMs))
{
    try
    {
        nint excludedHandleProbe = 0;
        bool spawnContainmentProbe = false;
        if (nativeOptions is ["--excluded-handle-probe", string nativeExcluded,
            "--spawn-containment-probe", "1"]
            && nint.TryParse(nativeExcluded, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out nint parsedExcluded))
        {
            excludedHandleProbe = parsedExcluded;
            spawnContainmentProbe = true;
        }
        else if (nativeOptions.Length != 0)
        {
            throw new InvalidDataException("Native qualification helper options are invalid.");
        }
        bool excludedHandleAccessible = excludedHandleProbe != 0
            && GetHandleInformation(excludedHandleProbe, out _);
        Process? descendant = null;
        if (spawnContainmentProbe)
        {
            descendant = Process.Start(new ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { "--containment-descendant" },
            });
        }
        using AnonymousPipeClientStream request = new(PipeDirection.In, nativeRequestHandle);
        using AnonymousPipeClientStream response = new(PipeDirection.Out, nativeResponseHandle);
        using RecordingReadStream recordedRequest = new(request);
        using RecordingWriteStream recordedResponse = new(response);
        using WindowsCredentialManagerStore store =
            WindowsCredentialManagerStore.FromAcceptedManifest(
                nativeManifestPath,
                nativeManifestSha256,
                nativeManifestId);
        using NativeQualificationSecretSource secretSource = new();
        OneShotHelperEngine engine = new(
            store,
            new FixedUtcTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(nativeAuthorityNowUnixMs)),
            secretSource,
            allowSyntheticProviderDispatch: true);
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(1650));
        await engine.RunAsync(recordedRequest, recordedResponse, deadline.Token);
        (int listeners, int networkOperations) = NetworkMeasurement.MeasureCurrentProcessTcp();
        byte[] traceBytes = JsonSerializer.SerializeToUtf8Bytes(store.CallTrace);
        NativeRawTargetCanary[] rawTargets = store.RawTargetCanaries.ToArray();
        NativeCanaryEvidence canaries;
        try
        {
            canaries = secretSource.ScanAndClear(
            [
                new("private protocol request", "private-pipe-bytes", recordedRequest.CapturedBytes),
                new("private protocol response", "private-pipe-bytes", recordedResponse.CapturedBytes),
                new("native call trace", "canonical-trace-bytes", traceBytes),
                NativeCanarySurface.FromText("process command line", Environment.CommandLine),
                NativeCanarySurface.FromText(
                    "process environment names",
                    string.Join('\n', Environment.GetEnvironmentVariables().Keys.Cast<object>()
                        .Select(value => value.ToString()).Order(StringComparer.Ordinal))),
            ],
            rawTargets);
        }
        finally
        {
            foreach (NativeRawTargetCanary target in rawTargets)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(target.Bytes);
            }
        }
        byte[] metrics = JsonSerializer.SerializeToUtf8Bytes(new
        {
            excluded_handle_accessible = excludedHandleAccessible,
            descendant_pid = descendant?.Id ?? 0,
            listener_count = listeners,
            network_operation_count = networkOperations,
            native_credential_operation_count = store.CallCounts.Total,
            native_call_trace = store.CallTrace,
            entry_cleanup = secretSource.EntryEvidence,
            canaries,
            namespace_reuse_blocked = store.NamespaceReuseBlocked,
            namespace_reuse_block_reason = store.NamespaceReuseBlockReason,
        });
        await response.WriteAsync(BitConverter.GetBytes(checked((uint)metrics.Length)), deadline.Token);
        await response.WriteAsync(metrics, deadline.Token);
        await response.FlushAsync(deadline.Token);
        return secretSource.LastPhase is { ScenarioId: "helper-and-coordinator-crash-restart", PhaseId: "half-commit" }
            ? 69
            : 0;
    }
    catch (Exception exception) when (exception is IOException or InvalidDataException
        or OperationCanceledException or System.ComponentModel.Win32Exception)
    {
        Console.Error.WriteLine($"Native helper terminated with typed non-secret failure: {exception.GetType().Name}");
        return 68;
    }
}

if (args is ["--containment-descendant"])
{
    await Task.Delay(TimeSpan.FromSeconds(30));
    return 0;
}

bool providerTransportSelected = args.Length >= 10 && args[^2] == "--provider-transport"
    && args[^1] is "production" or "synthetic-qualification";
bool productionProviderTransport = providerTransportSelected && args[^1] == "production";
bool syntheticQualificationTransport = providerTransportSelected && args[^1] == "synthetic-qualification";
string[] helperArgs = providerTransportSelected ? args[..^2] : args;
bool containmentProbe = helperArgs.Length == 12;
if (!providerTransportSelected || helperArgs.Length is not (8 or 12)
    || helperArgs[0] != "--request-handle" || helperArgs[2] != "--response-handle"
    || helperArgs[4] != "--store-handle" || helperArgs[6] != "--authority-now-unix-ms"
    || string.IsNullOrWhiteSpace(helperArgs[1]) || string.IsNullOrWhiteSpace(helperArgs[3])
    || !nint.TryParse(helperArgs[5], out nint storeHandle) || storeHandle is 0 or -1
    || !long.TryParse(helperArgs[7], System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out long authorityNowUnixMs)
    || containmentProbe && (helperArgs[8] != "--excluded-handle-probe"
        || !nint.TryParse(helperArgs[9], out _) || helperArgs[10] != "--spawn-containment-probe" || helperArgs[11] != "1"))
{
    Console.Error.WriteLine("The one-shot helper requires two private pipes, one secure-store capability, and authoritative time.");
    return 64;
}

try
{
    bool excludedHandleAccessible = containmentProbe && GetHandleInformation(
        nint.Parse(helperArgs[9], System.Globalization.CultureInfo.InvariantCulture), out _);
    Process? descendant = null;
    if (containmentProbe)
    {
        ProcessStartInfo descendantStart = new(Environment.ProcessPath!, "--containment-descendant")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        descendantStart.Environment.Clear();
        descendant = Process.Start(descendantStart)
            ?? throw new InvalidOperationException("The containment descendant probe could not start.");
    }
    using AnonymousPipeClientStream request = new(PipeDirection.In, helperArgs[1]);
    using AnonymousPipeClientStream response = new(PipeDirection.Out, helperArgs[3]);
    using CapabilityBoundFakeSecureStore store = new(storeHandle);
    using Infinium.OpenAI.OpenAiResponsesAdapter? providerTransport = productionProviderTransport
        ? Infinium.OpenAI.OpenAiResponsesAdapter.CreateProduction()
        : null;
    OneShotHelperEngine engine = new(
        store,
        new FixedUtcTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(authorityNowUnixMs)),
        providerTransport: providerTransport,
        allowSyntheticProviderDispatch: syntheticQualificationTransport);
    using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30));
    await engine.RunAsync(request, response, deadline.Token);
    (int listenerCount, int networkOperationCount) = NetworkMeasurement.MeasureCurrentProcessTcp();
    byte[] metrics = JsonSerializer.SerializeToUtf8Bytes(new
    {
        excluded_handle_accessible = excludedHandleAccessible,
        descendant_pid = descendant?.Id ?? 0,
        listener_count = listenerCount,
        network_operation_count = networkOperationCount,
        native_credential_operation_count = CapabilityBoundFakeSecureStore.NativeCredentialOperationCount,
    });
    await response.WriteAsync(BitConverter.GetBytes(checked((uint)metrics.Length)), deadline.Token);
    await response.WriteAsync(metrics, deadline.Token);
    await response.FlushAsync(deadline.Token);
    return 0;
}

catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException)
{
    Console.Error.WriteLine($"Helper terminated with typed non-secret failure: {exception.GetType().Name}");
    return 65;
}

[DllImport("kernel32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool GetHandleInformation(nint handle, out uint flags);

file sealed class FixedUtcTimeProvider(DateTimeOffset value) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => value;
}

file sealed class RecordingWriteStream(Stream inner) : Stream
{
    private readonly MemoryStream capture = new();
    internal byte[] CapturedBytes => capture.ToArray();
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
    public override void Write(byte[] buffer, int offset, int count)
    {
        capture.Write(buffer, offset, count);
        inner.Write(buffer, offset, count);
    }
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        capture.Write(buffer);
        inner.Write(buffer);
    }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        capture.Write(buffer.Span);
        return inner.WriteAsync(buffer, cancellationToken);
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing) { capture.Dispose(); }
        base.Dispose(disposing);
    }
}

file sealed class RecordingReadStream(Stream inner) : Stream
{
    private readonly MemoryStream capture = new();
    internal byte[] CapturedBytes => capture.ToArray();
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = inner.Read(buffer, offset, count);
        capture.Write(buffer, offset, read);
        return read;
    }
    public override int Read(Span<byte> buffer)
    {
        int read = inner.Read(buffer);
        capture.Write(buffer[..read]);
        return read;
    }
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        int read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        capture.Write(buffer.Span[..read]);
        return read;
    }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing) { capture.Dispose(); }
        base.Dispose(disposing);
    }
}

file static class NetworkMeasurement
{
    internal static (int Listeners, int Operations) MeasureCurrentProcessTcp()
    {
        int processId = Environment.ProcessId;
        (int v4Listeners, int v4Operations) = Measure(2, 24, 20, 0, processId);
        (int v6Listeners, int v6Operations) = Measure(23, 56, 52, 48, processId);
        return (v4Listeners + v6Listeners, v4Operations + v6Operations);
    }

    private static (int Listeners, int Operations) Measure(
        int addressFamily,
        int rowSize,
        int processOffset,
        int stateOffset,
        int processId)
    {
        int size = 0;
        _ = GetExtendedTcpTable(0, ref size, true, addressFamily, 5, 0);
        if (size <= 4)
        {
            return (0, 0);
        }
        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, true, addressFamily, 5, 0) != 0)
            {
                throw new InvalidOperationException("The helper TCP ownership table could not be measured.");
            }
            int rows = Marshal.ReadInt32(buffer);
            int listeners = 0;
            int operations = 0;
            for (int index = 0; index < rows; index++)
            {
                int row = checked(4 + index * rowSize);
                if (Marshal.ReadInt32(buffer, row + processOffset) != processId)
                {
                    continue;
                }
                int state = Marshal.ReadInt32(buffer, row + stateOffset);
                if (state == 2)
                {
                    listeners++;
                }
                else
                {
                    operations++;
                }
            }
            return (listeners, operations);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(
        nint tcpTable,
        ref int size,
        [MarshalAs(UnmanagedType.Bool)] bool order,
        int addressFamily,
        int tableClass,
        uint reserved);
}
