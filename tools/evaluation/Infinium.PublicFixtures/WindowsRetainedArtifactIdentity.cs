using System.ComponentModel;
using System.Runtime.InteropServices;
using Infinium.Application.Evaluation;
using Microsoft.Win32.SafeHandles;

namespace Infinium.PublicFixtures;

internal readonly record struct WindowsRetainedArtifactIdentitySnapshot(
    uint VolumeSerialNumber,
    ulong FileId,
    ulong ByteLength,
    ulong LastWriteTime,
    uint LinkCount);

internal static class WindowsRetainedArtifactIdentity
{
    internal static SafeFileHandle? OpenPinnedDirectory(string path, string artifactId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        SafeFileHandle handle = CreateFileW(
            path,
            FILE_READ_ATTRIBUTES,
            FileShare.ReadWrite | FileShare.Delete,
            0,
            FileMode.Open,
            FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
            0);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastPInvokeError();
            handle.Dispose();
            throw new IOException(
                $"Could not pin the retained package scope for '{artifactId}'.",
                new Win32Exception(error));
        }

        return handle;
    }

    internal static WindowsRetainedArtifactIdentitySnapshot RequireContainedSingleLink(
        SafeFileHandle artifactHandle,
        SafeFileHandle? scopedRootHandle,
        string artifactId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return default;
        }

        ByHandleFileInformation artifact = ReadInformation(artifactHandle, artifactId);
        if ((artifact.FileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' must be a regular file.");
        }

        if (artifact.NumberOfLinks != 1)
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' must not be hard linked.");
        }

        if (scopedRootHandle is null || scopedRootHandle.IsInvalid)
        {
            throw new InvalidDataException(
                $"Could not pin the retained package scope for '{artifactId}'.");
        }

        ByHandleFileInformation root = ReadInformation(scopedRootHandle, artifactId);
        if ((root.FileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0
            || (root.FileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0
            || root.VolumeSerialNumber != artifact.VolumeSerialNumber)
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' escapes its retained package scope.");
        }

        string rootPath = Path.TrimEndingDirectorySeparator(ReadFinalPath(scopedRootHandle, artifactId));
        string artifactPath = Path.TrimEndingDirectorySeparator(ReadFinalPath(artifactHandle, artifactId));
        if (!artifactPath.StartsWith(
                rootPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' escapes its retained package scope.");
        }

        return CreateSnapshot(artifact);
    }

    internal static void RequireUnchanged(
        SafeFileHandle artifactHandle,
        WindowsRetainedArtifactIdentitySnapshot initial,
        string artifactId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        WindowsRetainedArtifactIdentitySnapshot current =
            CreateSnapshot(ReadInformation(artifactHandle, artifactId));
        if (current != initial)
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' changed while being read.");
        }
    }

    private static WindowsRetainedArtifactIdentitySnapshot CreateSnapshot(
        ByHandleFileInformation information) =>
        new(
            information.VolumeSerialNumber,
            ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow,
            ((ulong)information.FileSizeHigh << 32) | information.FileSizeLow,
            ((ulong)information.LastWriteTime.HighDateTime << 32)
                | information.LastWriteTime.LowDateTime,
            information.NumberOfLinks);

    private static ByHandleFileInformation ReadInformation(
        SafeFileHandle handle,
        string artifactId)
    {
        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw new IOException(
                $"Could not inspect retained package artifact '{artifactId}'.",
                new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        return information;
    }

    private static string ReadFinalPath(SafeFileHandle handle, string artifactId)
    {
        char[] buffer = new char[512];
        while (true)
        {
            uint length = GetFinalPathNameByHandleW(
                handle,
                buffer,
                checked((uint)buffer.Length),
                FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
            if (length == 0)
            {
                throw new IOException(
                    $"Could not resolve the final retained package path for '{artifactId}'.",
                    new Win32Exception(Marshal.GetLastPInvokeError()));
            }

            if (length < buffer.Length)
            {
                string value = new(buffer, 0, checked((int)length));
                if (value.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
                {
                    return @"\\" + value[8..];
                }

                return value.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)
                    ? value[4..]
                    : value;
            }

            buffer = new char[checked((int)length + 1)];
        }
    }

    private const uint FILE_READ_ATTRIBUTES = 0x00000080;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
    private const uint FILE_NAME_NORMALIZED = 0x0;
    private const uint VOLUME_NAME_DOS = 0x0;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        nint securityAttributes,
        FileMode creationDisposition,
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

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        internal uint FileAttributes;
        internal FileTime CreationTime;
        internal FileTime LastAccessTime;
        internal FileTime LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        internal uint LowDateTime;
        internal uint HighDateTime;
    }
}
