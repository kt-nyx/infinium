using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Runtime;
using Infinium.Persistence;
using Microsoft.Extensions.Logging;
using LifecycleState = Infinium.Domain.Contracts.LifecycleState;

#pragma warning disable CA1848 // Failures are exceptional and retain structured run identity.

namespace Infinium.Coordinator;

public sealed class ManagedRunExecutor(
    CoordinatorRuntime runtime,
    WorkerBootstrapRegistry workerBootstraps,
    ILogger<ManagedRunExecutor> logger)
{
    private readonly Dictionary<string, Task> active = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    public void Schedule(string runId)
    {
        lock (gate)
        {
            if (!active.ContainsKey(runId))
            {
                active[runId] = Task.Run(() => ExecuteAsync(runId));
            }
        }
    }

    public void RecoverAtStartup()
    {
        foreach (RunRecord run in runtime.Store.ListNonTerminalRuns())
        {
            RunRecord current = run;
            if (run.State is LifecycleState.Running
                or LifecycleState.Waiting)
            {
                runtime.Store.SettleLiveAttempts(
                    run.RunId,
                    "interrupted-by-coordinator-recovery");
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
    }

    private async Task ExecuteAsync(string runId)
    {
        try
        {
            RunRecord queued = runtime.Store.GetRun(runId);
            if (queued.State is not (LifecycleState.Queued or LifecycleState.Retrying))
            {
                return;
            }

            RunRecord running = runtime.Store.Transition(
                Guid.NewGuid().ToString("N"),
                runId,
                queued.Generation,
                LifecycleState.Running,
                runtime.Authority.FencingEpoch,
                "managed worker dispatch",
                DateTimeOffset.UtcNow);
            AttemptRecord attempt = runtime.Store.CreateAttempt(
                runId,
                runtime.Authority.FencingEpoch,
                TimeSpan.FromMinutes(2),
                DateTimeOffset.UtcNow);
            string stagingDirectory = runtime.Store.Paths.ResolveProductRelative(
                Path.Combine("staging", attempt.AttemptId));
            Directory.CreateDirectory(stagingDirectory);
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
                stagingDirectory,
                "slice2-substrate.v1.json",
                65_536,
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                DateTimeOffset.UtcNow.AddMinutes(1));
            ManagedWorkerResult result = await LaunchWorkerAsync(bootstrap).ConfigureAwait(false);
            runtime.Store.AdmitStagedPayload(
                attempt,
                result.OutputRelativeName,
                result.Sha256,
                bootstrap.MaximumOutputBytes,
                DateTimeOffset.UtcNow);

            RunRecord current = runtime.Store.GetRun(runId);
            if (current.State == LifecycleState.Running)
            {
                runtime.Store.Transition(
                    Guid.NewGuid().ToString("N"),
                    runId,
                    current.Generation,
                    LifecycleState.Completed,
                    runtime.Authority.FencingEpoch,
                    "managed worker output admitted and published",
                    DateTimeOffset.UtcNow);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Managed worker execution failed for run {RunId}.", runId);
            try
            {
                RunRecord current = runtime.Store.GetRun(runId);
                if (!LifecyclePolicy.IsTerminal(current.State))
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
        finally
        {
            lock (gate)
            {
                active.Remove(runId);
            }
        }
    }

    private async Task<ManagedWorkerResult> LaunchWorkerAsync(
        ManagedWorkerBootstrap bootstrap)
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
        ProcessStartInfo start = new()
        {
            FileName = dotnet,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        start.ArgumentList.Add(workerAssembly);
        start.ArgumentList.Add("execute");
        start.Environment.Clear();
        start.Environment["SystemRoot"] = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        start.Environment["DOTNET_NOLOGO"] = "1";
        start.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The managed worker could not be launched.");
        using WorkerJobObject job = WorkerJobObject.CreateAndAssign(process);
        ManagedWorkerBootstrap boundBootstrap =
            bootstrap with { ExpectedProcessId = process.Id };
        workerBootstraps.Register(boundBootstrap);
        try
        {
            byte[] bootstrapBytes = JsonSerializer.SerializeToUtf8Bytes(boundBootstrap);
            await process.StandardInput.BaseStream.WriteAsync(bootstrapBytes).ConfigureAwait(false);
            process.StandardInput.Close();
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            string output = await outputTask.ConfigureAwait(false);
            string error = await errorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Managed worker exited with code {process.ExitCode}: {Bounded(error)}");
            }

            if (output.Length > 16_384)
            {
                throw new InvalidOperationException("The worker receipt exceeds its bound.");
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
}

#pragma warning restore CA1848

internal sealed class WorkerJobObject : IDisposable
{
    private readonly nint handle;

    private WorkerJobObject(nint handle) => this.handle = handle;

    public static WorkerJobObject CreateAndAssign(Process process)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new WorkerJobObject(0);
        }

        nint handle = CreateJobObjectW(0, null);
        if (handle == 0)
        {
            throw new InvalidOperationException("A worker Job Object could not be created.");
        }

        JOBOBJECT_EXTENDED_LIMIT_INFORMATION information = new();
        information.BasicLimitInformation.LimitFlags =
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_PROCESS_MEMORY;
        information.ProcessMemoryLimit = 256u * 1024u * 1024u;
        int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        nint buffer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(information, buffer, fDeleteOld: false);
            if (!SetInformationJobObject(handle, 9, buffer, checked((uint)length))
                || !AssignProcessToJobObject(handle, process.Handle))
            {
                throw new InvalidOperationException("The worker could not be contained in its Job Object.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return new WorkerJobObject(handle);
    }

    public void Dispose()
    {
        if (handle != 0)
        {
            CloseHandle(handle);
        }
    }

    private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObjectW(nint attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        nint job,
        int informationClass,
        nint information,
        uint informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(nint job, nint process);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}
