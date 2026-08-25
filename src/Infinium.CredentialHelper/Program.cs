using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.CredentialHelper;

if (args.Length == 4
    && args[0] == "--development-provider-invocation"
    && args[1] == "--manifest"
    && args[3] is "--offline" or "--live")
{
    try
    {
        bool live = args[^1] == "--live";
        DevelopmentProviderInvocationManifest manifest =
            DevelopmentProviderInvocationManifestCodec.Read(args[2]);
        byte[] evidence = await DevelopmentProviderInvocationRunner.RunAsync(
            manifest,
            live,
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
        await Console.OpenStandardOutput().WriteAsync(evidence);
        await Console.Out.WriteLineAsync();
        return 0;
    }
    catch (Exception exception) when (
        exception is IOException
            or JsonException
            or InvalidDataException
            or InvalidOperationException
            or OperationCanceledException
            or PlatformNotSupportedException
            or System.ComponentModel.Win32Exception)
    {
        Console.Error.WriteLine(
            $"Development provider invocation failed with typed non-secret error: {exception.GetType().Name}");
        return 68;
    }
}

if (args is ["--containment-descendant", string descendantDelay]
    && int.TryParse(descendantDelay, out int descendantDelayMilliseconds)
    && descendantDelayMilliseconds is >= 1 and <= 300_000)
{
    await Task.Delay(TimeSpan.FromMilliseconds(descendantDelayMilliseconds));
    return 0;
}

bool providerTransportSelected = args.Length >= 10 && args[^2] == "--provider-transport"
    && args[^1] is "production" or "synthetic-qualification";
bool productionProviderTransport = providerTransportSelected && args[^1] == "production";
bool syntheticQualificationTransport = providerTransportSelected && args[^1] == "synthetic-qualification";
string[] helperArgs = providerTransportSelected ? args[..^2] : args;
bool containmentProbe = helperArgs.Length is 12 or 16;
bool configuredContainmentTiming = helperArgs.Length == 16;
int containmentLifetimeMilliseconds = 30_000;
int postEngineDelayMilliseconds = 0;
if (!providerTransportSelected || helperArgs.Length is not (8 or 12 or 16)
    || helperArgs[0] != "--request-handle" || helperArgs[2] != "--response-handle"
    || helperArgs[4] != "--store-handle" || helperArgs[6] != "--authority-now-unix-ms"
    || string.IsNullOrWhiteSpace(helperArgs[1]) || string.IsNullOrWhiteSpace(helperArgs[3])
    || !nint.TryParse(helperArgs[5], out nint storeHandle) || storeHandle is 0 or -1
    || !long.TryParse(helperArgs[7], System.Globalization.NumberStyles.None,
        System.Globalization.CultureInfo.InvariantCulture, out long authorityNowUnixMs)
    || containmentProbe && (helperArgs[8] != "--excluded-handle-probe"
        || !nint.TryParse(helperArgs[9], out _) || helperArgs[10] != "--spawn-containment-probe" || helperArgs[11] != "1")
    || configuredContainmentTiming && (helperArgs[12] != "--containment-probe-lifetime-ms"
        || !int.TryParse(helperArgs[13], out containmentLifetimeMilliseconds)
        || containmentLifetimeMilliseconds is < 1 or > 300_000
        || helperArgs[14] != "--post-engine-delay-ms"
        || !int.TryParse(helperArgs[15], out postEngineDelayMilliseconds)
        || postEngineDelayMilliseconds is < 0 or > 300_000))
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
        string lifetime = (configuredContainmentTiming ? containmentLifetimeMilliseconds : 30_000)
            .ToString(System.Globalization.CultureInfo.InvariantCulture);
        ProcessStartInfo descendantStart = new(Environment.ProcessPath!, $"--containment-descendant {lifetime}")
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
    if (configuredContainmentTiming && postEngineDelayMilliseconds > 0)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(postEngineDelayMilliseconds), deadline.Token);
    }
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
