using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Mo2;

internal sealed record WindowsObjectIdentity(
    uint VolumeSerialNumber,
    ulong FileId,
    uint NumberOfLinks,
    FileAttributes Attributes,
    string FinalPath)
{
    public string CanonicalValue =>
        FormattableString.Invariant($"{VolumeSerialNumber:X8}:{FileId:X16}");
}

internal static class WindowsReadOnlyObjectIdentity
{
    public static WindowsObjectIdentity Open(string path, bool directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "MO2 snapshot object identity requires Windows.");
        }

        using SafeFileHandle handle = CreateFileW(
            path,
            FileReadAttributes,
            FileShareRead | FileShareWrite | FileShareDelete,
            0,
            OpenExisting,
            (directory ? FileFlagBackupSemantics : 0) | FileFlagOpenReparsePoint,
            0);
        if (handle.IsInvalid)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The selected Windows object identity could not be opened.");
        }

        return Read(handle);
    }

    public static WindowsObjectIdentity Read(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The selected Windows object identity could not be read.");
        }

        string finalPath = ReadFinalPath(handle);
        ulong fileId = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return new WindowsObjectIdentity(
            information.VolumeSerialNumber,
            fileId,
            information.NumberOfLinks,
            (FileAttributes)information.FileAttributes,
            finalPath);
    }

    private static string ReadFinalPath(SafeFileHandle handle)
    {
        char[] buffer = new char[512];
        while (true)
        {
            uint length = GetFinalPathNameByHandleW(
                handle,
                buffer,
                checked((uint)buffer.Length),
                FileNameNormalized | VolumeNameDos);
            if (length == 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The selected Windows object final path could not be resolved.");
            }

            if (length < buffer.Length)
            {
                string value = new(buffer, 0, checked((int)length));
                return value.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)
                    ? value[4..]
                    : value;
            }

            buffer = new char[checked((int)length + 1)];
        }
    }

    private const uint FileReadAttributes = 0x00000080;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileNameNormalized = 0;
    private const uint VolumeNameDos = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public FileTime CreationTime;
        public FileTime LastAccessTime;
        public FileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle file,
        [Out] char[] filePath,
        uint filePathLength,
        uint flags);
}
