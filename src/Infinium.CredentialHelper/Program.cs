using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Infinium.CredentialHelper;

if (args is ["--credential-native-qualification", "--manifest", string manifestPath,
    "--evidence", string evidencePath])
{
    return WindowsCredentialNativeQualification.Run(manifestPath, evidencePath);
}

if (args is ["--credential-native-crash-probe", "--manifest", string crashManifestPath,
    "--target-alias", string targetAlias, "--count-evidence", string countEvidencePath])
{
    return WindowsCredentialNativeQualification.RunCrashProbe(crashManifestPath, targetAlias, countEvidencePath);
}

if (args is ["--containment-descendant"])
{
    await Task.Delay(TimeSpan.FromSeconds(30));
    return 0;
}

bool containmentProbe = args.Length == 12;
if (args.Length is not (8 or 12) || args[0] != "--request-handle" || args[2] != "--response-handle"
    || args[4] != "--store-handle" || args[6] != "--authority-now-unix-ms"
    || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[3])
    || !nint.TryParse(args[5], out nint storeHandle) || storeHandle is 0 or -1
    || !long.TryParse(args[7], System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out long authorityNowUnixMs)
    || containmentProbe && (args[8] != "--excluded-handle-probe"
        || !nint.TryParse(args[9], out _) || args[10] != "--spawn-containment-probe" || args[11] != "1"))
{
    Console.Error.WriteLine("The one-shot helper requires two private pipes, one secure-store capability, and authoritative time.");
    return 64;
}

try
{
    bool excludedHandleAccessible = containmentProbe && GetHandleInformation(
        nint.Parse(args[9], System.Globalization.CultureInfo.InvariantCulture), out _);
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
    using AnonymousPipeClientStream request = new(PipeDirection.In, args[1]);
    using AnonymousPipeClientStream response = new(PipeDirection.Out, args[3]);
    using CapabilityBoundFakeSecureStore store = new(storeHandle);
    OneShotHelperEngine engine = new(
        store,
        new FixedUtcTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(authorityNowUnixMs)));
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
