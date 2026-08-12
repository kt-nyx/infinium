using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Application.Runtime;

public static class WindowsHandleRelativeFile
{
    public static FileStream OpenRead(nint directoryHandle, string relativeName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Handle-relative staged input currently requires Windows.");
        }

        if (directoryHandle is 0 or -1)
        {
            throw new ArgumentOutOfRangeException(nameof(directoryHandle));
        }

        ValidateLeafName(relativeName);
        SafeFileHandle file = OpenRelative(
            directoryHandle,
            relativeName,
            GENERIC_READ | SYNCHRONIZE,
            FILE_SHARE_READ,
            FILE_OPEN);
        return new FileStream(file, FileAccess.Read, bufferSize: 4096, isAsync: false);
    }

    public static FileStream CreateNew(nint directoryHandle, string relativeName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Handle-relative staged output currently requires Windows.");
        }

        if (directoryHandle is 0 or -1)
        {
            throw new ArgumentOutOfRangeException(nameof(directoryHandle));
        }

        ValidateLeafName(relativeName);
        SafeFileHandle file = OpenRelative(
            directoryHandle,
            relativeName,
            GENERIC_WRITE | SYNCHRONIZE,
            0,
            FILE_CREATE);
        return new FileStream(file, FileAccess.Write, bufferSize: 4096, isAsync: false);
    }

    public static FileStream OpenOrCreateReadWrite(nint directoryHandle, string relativeName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Handle-relative secure-store access currently requires Windows.");
        }
        if (directoryHandle is 0 or -1)
        {
            throw new ArgumentOutOfRangeException(nameof(directoryHandle));
        }
        ValidateLeafName(relativeName);
        SafeFileHandle file = OpenRelative(
            directoryHandle,
            relativeName,
            GENERIC_READ | GENERIC_WRITE | SYNCHRONIZE,
            FILE_SHARE_READ,
            FILE_OPEN_IF);
        return new FileStream(file, FileAccess.ReadWrite, bufferSize: 4096, isAsync: false);
    }

    private static SafeFileHandle OpenRelative(
        nint directoryHandle,
        string relativeName,
        uint desiredAccess,
        uint shareAccess,
        uint disposition)
    {
        nint nameBuffer = Marshal.StringToHGlobalUni(relativeName);
        nint unicodeStringBuffer = 0;
        try
        {
            UNICODE_STRING unicodeName = new()
            {
                Length = checked((ushort)(relativeName.Length * sizeof(char))),
                MaximumLength = checked((ushort)((relativeName.Length + 1) * sizeof(char))),
                Buffer = nameBuffer,
            };
            unicodeStringBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<UNICODE_STRING>());
            Marshal.StructureToPtr(unicodeName, unicodeStringBuffer, fDeleteOld: false);
            OBJECT_ATTRIBUTES attributes = new()
            {
                Length = Marshal.SizeOf<OBJECT_ATTRIBUTES>(),
                RootDirectory = directoryHandle,
                ObjectName = unicodeStringBuffer,
                Attributes = OBJ_CASE_INSENSITIVE,
            };
            int status = NtCreateFile(
                out SafeFileHandle file,
                desiredAccess,
                ref attributes,
                out _,
                0,
                FILE_ATTRIBUTE_NORMAL,
                shareAccess,
                disposition,
                FILE_NON_DIRECTORY_FILE | FILE_SYNCHRONOUS_IO_NONALERT,
                0,
                0);
            if (status < 0)
            {
                file?.Dispose();
                throw new Win32Exception(
                    checked((int)RtlNtStatusToDosError(status)),
                    "The handle-relative staged output could not be created.");
            }
            return file;
        }
        finally
        {
            if (unicodeStringBuffer != 0)
            {
                Marshal.FreeHGlobal(unicodeStringBuffer);
            }

            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    public static string? TryGetFinalPath(nint handle)
    {
        if (!OperatingSystem.IsWindows() || handle is 0 or -1)
        {
            return null;
        }

        char[] buffer = new char[1024];
        uint length = GetFinalPathNameByHandleW(
            handle,
            buffer,
            checked((uint)buffer.Length),
            FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
        if (length == 0 || length >= buffer.Length)
        {
            return null;
        }

        return new string(buffer, 0, checked((int)length));
    }

    private static void ValidateLeafName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value is "." or ".."
            || value.Contains(':', StringComparison.Ordinal)
            || value.Contains('\0', StringComparison.Ordinal)
            || value.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || value.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A staged output name must be a single typed relative leaf.",
                nameof(value));
        }
    }

    private const uint GENERIC_WRITE = 0x40000000;
    private const uint GENERIC_READ = 0x80000000;
    private const uint SYNCHRONIZE = 0x00100000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    private const uint FILE_OPEN = 1;
    private const uint FILE_CREATE = 2;
    private const uint FILE_OPEN_IF = 3;
    private const uint FILE_SYNCHRONOUS_IO_NONALERT = 0x00000020;
    private const uint FILE_NON_DIRECTORY_FILE = 0x00000040;
    private const uint OBJ_CASE_INSENSITIVE = 0x00000040;
    private const uint FILE_NAME_NORMALIZED = 0x0;
    private const uint VOLUME_NAME_DOS = 0x0;

    [StructLayout(LayoutKind.Sequential)]
    private struct UNICODE_STRING
    {
        public ushort Length;
        public ushort MaximumLength;
        public nint Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OBJECT_ATTRIBUTES
    {
        public int Length;
        public nint RootDirectory;
        public nint ObjectName;
        public uint Attributes;
        public nint SecurityDescriptor;
        public nint SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_STATUS_BLOCK
    {
        public nint Status;
        public nuint Information;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtCreateFile(
        out SafeFileHandle fileHandle,
        uint desiredAccess,
        ref OBJECT_ATTRIBUTES objectAttributes,
        out IO_STATUS_BLOCK ioStatusBlock,
        nint allocationSize,
        uint fileAttributes,
        uint shareAccess,
        uint createDisposition,
        uint createOptions,
        nint eaBuffer,
        uint eaLength);

    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        nint file,
        [Out] char[] filePath,
        uint filePathLength,
        uint flags);
}
