using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

#pragma warning disable CA1838 // CreateProcessW requires a mutable command-line buffer.

namespace Infinium.Coordinator;

internal sealed class WindowsContainedWorkerProcess : IDisposable
{
    internal sealed class PrivateHelperProcess : IDisposable
    {
        private nint job;
        private nint thread;
        private nint processHandle;
        private bool resumed;

        internal PrivateHelperProcess(Process process, nint processHandle, nint thread, nint job)
        {
            Process = process;
            this.processHandle = processHandle;
            this.thread = thread;
            this.job = job;
        }

        internal Process Process { get; }
        internal int ActiveProcessCount => QueryActiveProcessCount(job);
        internal int TotalProcessCount => QueryProcessCounts(job).Total;

        internal async Task<(int ActiveBeforeTermination, int ActiveAfterTermination)>
            TerminateRemainingProcessesAndWaitAsync(
                TimeSpan timeout,
                CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

            int activeBeforeTermination = QueryProcessCounts(job).Active;
            if (activeBeforeTermination == 0)
            {
                return (0, 0);
            }

            if (!TerminateJobObject(job, 1))
            {
                int activeAfterRace = QueryProcessCounts(job).Active;
                if (activeAfterRace != 0)
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "The helper Job Object could not terminate its remaining contained processes.");
                }
                return (activeBeforeTermination, 0);
            }

            using CancellationTokenSource bounded =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            bounded.CancelAfter(timeout);
            while (true)
            {
                int activeAfterTermination = QueryProcessCounts(job).Active;
                if (activeAfterTermination == 0)
                {
                    return (activeBeforeTermination, 0);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(10), bounded.Token).ConfigureAwait(false);
            }
        }

        internal void CloseJob()
        {
            CloseIfValid(job);
            job = 0;
        }
        internal int ExitCode
        {
            get
            {
                if (!GetExitCodeProcess(processHandle, out uint exitCode))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "The helper exit code could not be measured.");
                }
                return checked((int)exitCode);
            }
        }

        internal void Resume()
        {
            if (resumed || ResumeThread(thread) == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The contained helper could not be resumed.");
            }
            resumed = true;
            CloseHandle(thread);
            thread = 0;
        }

        public void Dispose()
        {
            CloseIfValid(thread);
            thread = 0;
            CloseJob();
            CloseIfValid(processHandle);
            processHandle = 0;
            Process.Dispose();
        }
    }

    private readonly nint jobHandle;
    private readonly nint stagingDirectoryHandle;
    private nint primaryThreadHandle;
    private bool resumed;

    private WindowsContainedWorkerProcess(
        Process process,
        nint primaryThreadHandle,
        nint jobHandle,
        nint stagingDirectoryHandle,
        FileStream bootstrapInput,
        StreamReader standardOutput,
        StreamReader standardError)
    {
        Process = process;
        this.primaryThreadHandle = primaryThreadHandle;
        this.jobHandle = jobHandle;
        this.stagingDirectoryHandle = stagingDirectoryHandle;
        BootstrapInput = bootstrapInput;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    public Process Process { get; }
    public FileStream BootstrapInput { get; }
    public StreamReader StandardOutput { get; }
    public StreamReader StandardError { get; }
    public nint InheritedStagingDirectoryHandle => stagingDirectoryHandle;

    public static WindowsContainedWorkerProcess Create(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        SafeFileHandle stagingDirectory,
        IReadOnlyList<nint>? additionalInheritedHandles = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Contained worker launch currently requires Windows.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(stagingDirectory);
        if (additionalInheritedHandles?.Count > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(additionalInheritedHandles),
                "At most eight additional inherited handles are allowed.");
        }

        SECURITY_ATTRIBUTES inheritable = new()
        {
            Length = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            InheritHandle = true,
        };
        if (!DuplicateHandle(
                GetCurrentProcess(),
                stagingDirectory.DangerousGetHandle(),
                GetCurrentProcess(),
                out nint stagingHandle,
                0,
                inheritHandle: true,
                DUPLICATE_SAME_ACCESS))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The staging directory authority could not be opened.");
        }

        nint parentInput = 0;
        nint childInput = 0;
        nint parentOutput = 0;
        nint childOutput = 0;
        nint parentError = 0;
        nint childError = 0;
        nint attributeList = 0;
        nint inheritedHandleList = 0;
        nint jobHandleList = 0;
        nint environmentBlock = 0;
        nint processHandle = 0;
        nint threadHandle = 0;
        nint jobHandle = 0;
        Dictionary<nint, uint> originalAdditionalHandleFlags = [];
        try
        {
            jobHandle = CreateConfiguredJob();
            CreateDirectedPipe(
                parentReads: false,
                ref inheritable,
                out parentInput,
                out childInput);
            CreateDirectedPipe(
                parentReads: true,
                ref inheritable,
                out parentOutput,
                out childOutput);
            CreateDirectedPipe(
                parentReads: true,
                ref inheritable,
                out parentError,
                out childError);

            nint[] handles =
            [
                childInput,
                childOutput,
                childError,
                stagingHandle,
                .. additionalInheritedHandles ?? [],
            ];
            foreach (nint handle in additionalInheritedHandles ?? [])
            {
                if (!GetHandleInformation(handle, out uint flags))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "An additional inherited handle is invalid.");
                }

                originalAdditionalHandleFlags.TryAdd(handle, flags);
            }

            foreach (nint handle in handles)
            {
                if (handle is 0 or -1
                    || !SetHandleInformation(
                        handle,
                        HANDLE_FLAG_INHERIT,
                        HANDLE_FLAG_INHERIT))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "A declared inherited handle is invalid.");
                }
            }

            nuint attributeListSize = 0;
            _ = InitializeProcThreadAttributeList(0, 2, 0, ref attributeListSize);
            attributeList = Marshal.AllocHGlobal(checked((int)attributeListSize));
            if (!InitializeProcThreadAttributeList(
                attributeList,
                2,
                0,
                ref attributeListSize))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The worker handle-list attribute could not be initialized.");
            }

            inheritedHandleList =
                Marshal.AllocHGlobal(checked(handles.Length * nint.Size));
            for (int index = 0; index < handles.Length; index++)
            {
                Marshal.WriteIntPtr(
                    inheritedHandleList,
                    checked(index * nint.Size),
                    handles[index]);
            }

            if (!UpdateProcThreadAttribute(
                attributeList,
                0,
                PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
                inheritedHandleList,
                checked((nuint)(handles.Length * nint.Size)),
                0,
                0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The worker handle-list attribute could not be populated.");
            }

            jobHandleList = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(jobHandleList, jobHandle);
            if (!UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    PROC_THREAD_ATTRIBUTE_JOB_LIST,
                    jobHandleList,
                    checked((nuint)nint.Size),
                    0,
                    0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The worker Job Object attribute could not be populated.");
            }

            STARTUPINFOEX startup = new()
            {
                StartupInfo = new STARTUPINFO
                {
                    Size = Marshal.SizeOf<STARTUPINFOEX>(),
                    Flags = STARTF_USESTDHANDLES,
                    StandardInput = childInput,
                    StandardOutput = childOutput,
                    StandardError = childError,
                },
                AttributeList = attributeList,
            };
            environmentBlock = BuildEnvironmentBlock(environment);
            string commandLine = string.Join(
                " ",
                new[] { executable }.Concat(arguments).Select(QuoteWindowsArgument));
            bool created = CreateProcessW(
                executable,
                new StringBuilder(commandLine),
                0,
                0,
                inheritHandles: true,
                CREATE_SUSPENDED
                    | CREATE_NO_WINDOW
                    | CREATE_UNICODE_ENVIRONMENT
                    | EXTENDED_STARTUPINFO_PRESENT,
                environmentBlock,
                workingDirectory,
                ref startup,
                out PROCESS_INFORMATION processInformation);
            if (!created)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The suspended managed worker could not be created.");
            }

            processHandle = processInformation.Process;
            threadHandle = processInformation.Thread;

            CloseHandle(childInput);
            childInput = 0;
            CloseHandle(childOutput);
            childOutput = 0;
            CloseHandle(childError);
            childError = 0;

            Process managedProcess =
                Process.GetProcessById(checked((int)processInformation.ProcessId));
            CloseHandle(processHandle);
            processHandle = 0;

            FileStream input = new(
                new SafeFileHandle(parentInput, ownsHandle: true),
                FileAccess.Write,
                bufferSize: 4096,
                isAsync: false);
            parentInput = 0;
            StreamReader output = CreateReader(parentOutput);
            parentOutput = 0;
            StreamReader error = CreateReader(parentError);
            parentError = 0;
            WindowsContainedWorkerProcess result = new(
                managedProcess,
                threadHandle,
                jobHandle,
                stagingHandle,
                input,
                output,
                error);
            threadHandle = 0;
            jobHandle = 0;
            stagingHandle = 0;
            return result;
        }
        catch
        {
            if (processHandle != 0)
            {
                _ = TerminateProcess(processHandle, 1);
            }

            throw;
        }
        finally
        {
            if (attributeList != 0)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (inheritedHandleList != 0)
            {
                Marshal.FreeHGlobal(inheritedHandleList);
            }

            if (jobHandleList != 0)
            {
                Marshal.FreeHGlobal(jobHandleList);
            }

            if (environmentBlock != 0)
            {
                Marshal.FreeHGlobal(environmentBlock);
            }

            CloseIfValid(parentInput);
            CloseIfValid(childInput);
            CloseIfValid(parentOutput);
            CloseIfValid(childOutput);
            CloseIfValid(parentError);
            CloseIfValid(childError);
            CloseIfValid(processHandle);
            CloseIfValid(threadHandle);
            CloseIfValid(jobHandle);
            CloseIfValid(stagingHandle);
            foreach ((nint handle, uint flags) in originalAdditionalHandleFlags)
            {
                _ = SetHandleInformation(
                    handle,
                    HANDLE_FLAG_INHERIT,
                    flags & HANDLE_FLAG_INHERIT);
            }
        }
    }

    internal static PrivateHelperProcess CreatePrivateHelper(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        IReadOnlyList<nint> inheritedHandles)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Contained helper launch requires Windows.");
        }
        if (inheritedHandles.Count is 0 or > 4 || inheritedHandles.Any(handle => handle is 0 or -1))
        {
            throw new ArgumentOutOfRangeException(nameof(inheritedHandles));
        }

        nint attributeList = 0;
        nint inheritedHandleList = 0;
        nint jobHandleList = 0;
        nint environmentBlock = 0;
        nint processHandle = 0;
        nint threadHandle = 0;
        nint jobHandle = 0;
        Dictionary<nint, uint> originalFlags = [];
        try
        {
            jobHandle = CreateConfiguredJob();
            foreach (nint handle in inheritedHandles)
            {
                if (!GetHandleInformation(handle, out uint flags)
                    || !SetHandleInformation(handle, HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "A declared helper handle is invalid.");
                }
                originalFlags.Add(handle, flags);
            }

            nuint attributeListSize = 0;
            _ = InitializeProcThreadAttributeList(0, 2, 0, ref attributeListSize);
            attributeList = Marshal.AllocHGlobal(checked((int)attributeListSize));
            if (!InitializeProcThreadAttributeList(attributeList, 2, 0, ref attributeListSize))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The helper attribute list could not be initialized.");
            }
            inheritedHandleList = Marshal.AllocHGlobal(checked(inheritedHandles.Count * nint.Size));
            for (int index = 0; index < inheritedHandles.Count; index++)
            {
                Marshal.WriteIntPtr(inheritedHandleList, checked(index * nint.Size), inheritedHandles[index]);
            }
            if (!UpdateProcThreadAttribute(attributeList, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
                    inheritedHandleList, checked((nuint)(inheritedHandles.Count * nint.Size)), 0, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The helper private handle list could not be populated.");
            }
            jobHandleList = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(jobHandleList, jobHandle);
            if (!UpdateProcThreadAttribute(attributeList, 0, PROC_THREAD_ATTRIBUTE_JOB_LIST,
                    jobHandleList, checked((nuint)nint.Size), 0, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The helper Job Object could not be attached.");
            }

            STARTUPINFOEX startup = new()
            {
                StartupInfo = new STARTUPINFO { Size = Marshal.SizeOf<STARTUPINFOEX>() },
                AttributeList = attributeList,
            };
            environmentBlock = BuildEnvironmentBlock(environment);
            string commandLine = string.Join(" ", new[] { executable }.Concat(arguments).Select(QuoteWindowsArgument));
            if (!CreateProcessW(executable, new StringBuilder(commandLine), 0, 0, inheritHandles: true,
                    CREATE_SUSPENDED | CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT | EXTENDED_STARTUPINFO_PRESENT,
                    environmentBlock, workingDirectory, ref startup, out PROCESS_INFORMATION processInformation))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "The exact contained helper could not be created.");
            }
            processHandle = processInformation.Process;
            threadHandle = processInformation.Thread;
            Process process = Process.GetProcessById(checked((int)processInformation.ProcessId));
            PrivateHelperProcess result = new(process, processHandle, threadHandle, jobHandle);
            processHandle = 0;
            threadHandle = 0;
            jobHandle = 0;
            return result;
        }
        finally
        {
            if (attributeList != 0)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
            if (inheritedHandleList != 0)
            {
                Marshal.FreeHGlobal(inheritedHandleList);
            }
            if (jobHandleList != 0)
            {
                Marshal.FreeHGlobal(jobHandleList);
            }
            if (environmentBlock != 0)
            {
                Marshal.FreeHGlobal(environmentBlock);
            }
            CloseIfValid(processHandle);
            CloseIfValid(threadHandle);
            CloseIfValid(jobHandle);
            foreach ((nint handle, uint flags) in originalFlags)
            {
                _ = SetHandleInformation(handle, HANDLE_FLAG_INHERIT, flags & HANDLE_FLAG_INHERIT);
            }
        }
    }

    public void Resume()
    {
        if (resumed || primaryThreadHandle == 0)
        {
            throw new InvalidOperationException("The managed worker has already been resumed.");
        }

        if (ResumeThread(primaryThreadHandle) == uint.MaxValue)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The contained worker could not be resumed.");
        }

        resumed = true;
        CloseHandle(primaryThreadHandle);
        primaryThreadHandle = 0;
    }

    public void Dispose()
    {
        BootstrapInput.Dispose();
        StandardOutput.Dispose();
        StandardError.Dispose();
        Process.Dispose();
        CloseIfValid(primaryThreadHandle);
        primaryThreadHandle = 0;
        CloseIfValid(jobHandle);
        CloseIfValid(stagingDirectoryHandle);
    }

    private static StreamReader CreateReader(nint handle) =>
        new(
            new FileStream(
                new SafeFileHandle(handle, ownsHandle: true),
                FileAccess.Read,
                bufferSize: 4096,
                isAsync: false),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: false);

    private static void CreateDirectedPipe(
        bool parentReads,
        ref SECURITY_ATTRIBUTES inheritable,
        out nint parent,
        out nint child)
    {
        if (!CreatePipe(out nint read, out nint write, ref inheritable, 0))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "A private worker pipe could not be created.");
        }

        parent = parentReads ? read : write;
        child = parentReads ? write : read;
        if (!SetHandleInformation(parent, HANDLE_FLAG_INHERIT, 0))
        {
            CloseHandle(read);
            CloseHandle(write);
            parent = 0;
            child = 0;
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "A coordinator pipe handle could not be made private.");
        }
    }

    private static nint CreateConfiguredJob()
    {
        nint job = CreateJobObjectW(0, null);
        if (job == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "A worker Job Object could not be created.");
        }

        JOBOBJECT_EXTENDED_LIMIT_INFORMATION information = new();
        information.BasicLimitInformation.LimitFlags =
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            | JOB_OBJECT_LIMIT_ACTIVE_PROCESS
            | JOB_OBJECT_LIMIT_PROCESS_MEMORY
            | JOB_OBJECT_LIMIT_JOB_MEMORY;
        information.BasicLimitInformation.ActiveProcessLimit = 4;
        information.ProcessMemoryLimit = 256u * 1024u * 1024u;
        information.JobMemoryLimit = 256u * 1024u * 1024u;
        int size = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(information, buffer, fDeleteOld: false);
            if (!SetInformationJobObject(job, 9, buffer, checked((uint)size)))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The worker Job Object limits could not be configured.");
            }

            return job;
        }
        catch
        {
            CloseHandle(job);
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static nint BuildEnvironmentBlock(IReadOnlyDictionary<string, string> environment)
    {
        string block = string.Join(
            '\0',
            environment
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Key}={pair.Value}"))
            + "\0\0";
        return Marshal.StringToHGlobalUni(block);
    }

    private static string QuoteWindowsArgument(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        if (!value.Any(character => char.IsWhiteSpace(character) || character == '"'))
        {
            return value;
        }

        StringBuilder result = new("\"");
        int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\')
            {
                slashes++;
            }
            else if (character == '"')
            {
                result.Append('\\', checked(slashes * 2 + 1));
                result.Append('"');
                slashes = 0;
            }
            else
            {
                result.Append('\\', slashes);
                result.Append(character);
                slashes = 0;
            }
        }

        result.Append('\\', checked(slashes * 2));
        result.Append('"');
        return result.ToString();
    }

    private static void CloseIfValid(nint handle)
    {
        if (handle is not (0 or -1))
        {
            CloseHandle(handle);
        }
    }

    private static int QueryActiveProcessCount(nint job)
        => QueryProcessCounts(job).Active;

    private static (int Active, int Total) QueryProcessCounts(nint job)
    {
        JOBOBJECT_BASIC_ACCOUNTING_INFORMATION information = new();
        int size = Marshal.SizeOf<JOBOBJECT_BASIC_ACCOUNTING_INFORMATION>();
        if (!QueryInformationJobObject(job, 1, ref information, checked((uint)size), out _))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The helper Job Object could not be measured.");
        }
        return (checked((int)information.ActiveProcesses), checked((int)information.TotalProcesses));
    }

    private const uint DUPLICATE_SAME_ACCESS = 0x00000002;
    private const uint HANDLE_FLAG_INHERIT = 0x00000001;
    private const uint STARTF_USESTDHANDLES = 0x00000100;
    private const uint CREATE_SUSPENDED = 0x00000004;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint JOB_OBJECT_LIMIT_PROCESS_MEMORY = 0x00000100;
    private const uint JOB_OBJECT_LIMIT_JOB_MEMORY = 0x00000200;
    private const uint JOB_OBJECT_LIMIT_ACTIVE_PROCESS = 0x00000008;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
    private static readonly nint PROC_THREAD_ATTRIBUTE_HANDLE_LIST = new(0x00020002);
    private static readonly nint PROC_THREAD_ATTRIBUTE_JOB_LIST = new(0x0002000D);

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int Length;
        public nint SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int Size;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public uint X;
        public uint Y;
        public uint XSize;
        public uint YSize;
        public uint XCountChars;
        public uint YCountChars;
        public uint FillAttribute;
        public uint Flags;
        public ushort ShowWindow;
        public nint Reserved2;
        public nint StandardInput;
        public nint StandardOutput;
        public nint StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public nint AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint Process;
        public nint Thread;
        public uint ProcessId;
        public uint ThreadId;
    }

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

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_ACCOUNTING_INFORMATION
    {
        public long TotalUserTime;
        public long TotalKernelTime;
        public long ThisPeriodTotalUserTime;
        public long ThisPeriodTotalKernelTime;
        public uint TotalPageFaultCount;
        public uint TotalProcesses;
        public uint ActiveProcesses;
        public uint TotalTerminatedProcesses;
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(
        nint sourceProcess,
        nint sourceHandle,
        nint targetProcess,
        out nint targetHandle,
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint options);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreatePipe(
        out nint readPipe,
        out nint writePipe,
        ref SECURITY_ATTRIBUTES pipeAttributes,
        uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(nint handle, uint mask, uint flags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetHandleInformation(nint handle, out uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessW(
        string applicationName,
        StringBuilder commandLine,
        nint processAttributes,
        nint threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        nint environment,
        string currentDirectory,
        ref STARTUPINFOEX startupInfo,
        out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InitializeProcThreadAttributeList(
        nint attributeList,
        int attributeCount,
        int flags,
        ref nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateProcThreadAttribute(
        nint attributeList,
        uint flags,
        nint attribute,
        nint value,
        nuint size,
        nint previousValue,
        nint returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(nint attributeList);

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
    private static extern bool QueryInformationJobObject(
        nint job,
        int informationClass,
        ref JOBOBJECT_BASIC_ACCOUNTING_INFORMATION information,
        uint informationLength,
        out uint returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateJobObject(nint job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint ResumeThread(nint thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TerminateProcess(nint process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(nint process, out uint exitCode);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}

#pragma warning restore CA1838
