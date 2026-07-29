using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable CA1838 // CreateProcessW requires a mutable command-line buffer.

namespace Infinium.Application.Runtime;

public static class DetachedProcessLauncher
{
    public static int Start(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Detached launch currently requires Windows.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentNullException.ThrowIfNull(environment);

        SECURITY_ATTRIBUTES attributes = new()
        {
            Length = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            InheritHandle = true,
        };
        nint nul = CreateFileW(
            "NUL",
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            ref attributes,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            0);
        if (nul == INVALID_HANDLE_VALUE)
        {
            throw new InvalidOperationException("The detached null stream could not be opened.");
        }

        string commandLine = string.Join(
            " ",
            new[] { executable }.Concat(arguments).Select(QuoteWindowsArgument));
        nint environmentBlock = BuildEnvironmentBlock(environment);
        nuint attributeListSize = 0;
        _ = InitializeProcThreadAttributeList(0, 1, 0, ref attributeListSize);
        nint attributeList = Marshal.AllocHGlobal(checked((int)attributeListSize));
        nint inheritedHandleList = Marshal.AllocHGlobal(nint.Size);
        try
        {
            if (!InitializeProcThreadAttributeList(
                attributeList,
                1,
                0,
                ref attributeListSize))
            {
                throw new InvalidOperationException("The restricted handle list could not be initialized.");
            }

            Marshal.WriteIntPtr(inheritedHandleList, nul);
            if (!UpdateProcThreadAttribute(
                attributeList,
                0,
                PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
                inheritedHandleList,
                checked((nuint)nint.Size),
                0,
                0))
            {
                throw new InvalidOperationException("The restricted handle list could not be populated.");
            }

            STARTUPINFOEX startup = new()
            {
                StartupInfo = new STARTUPINFO
                {
                    Size = Marshal.SizeOf<STARTUPINFOEX>(),
                    Flags = STARTF_USESTDHANDLES,
                    StandardInput = nul,
                    StandardOutput = nul,
                    StandardError = nul,
                },
                AttributeList = attributeList,
            };
            bool created = CreateProcessW(
                executable,
                new StringBuilder(commandLine),
                0,
                0,
                inheritHandles: true,
                CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT | EXTENDED_STARTUPINFO_PRESENT,
                environmentBlock,
                workingDirectory,
                ref startup,
                out PROCESS_INFORMATION process);
            if (!created)
            {
                throw new InvalidOperationException(
                    $"Detached process creation failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }

            try
            {
                return checked((int)process.ProcessId);
            }
            finally
            {
                CloseHandle(process.Thread);
                CloseHandle(process.Process);
            }
        }
        finally
        {
            DeleteProcThreadAttributeList(attributeList);
            Marshal.FreeHGlobal(inheritedHandleList);
            Marshal.FreeHGlobal(attributeList);
            Marshal.FreeHGlobal(environmentBlock);
            CloseHandle(nul);
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

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint STARTF_USESTDHANDLES = 0x00000100;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private static readonly nint PROC_THREAD_ATTRIBUTE_HANDLE_LIST = new(0x00020002);
    private static readonly nint INVALID_HANDLE_VALUE = new(-1);

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
        public ushort Reserved2Length;
        public nint Reserved2;
        public nint StandardInput;
        public nint StandardOutput;
        public nint StandardError;
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
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public nint AttributeList;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        ref SECURITY_ATTRIBUTES securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

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

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}

#pragma warning restore CA1838
