using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Application.Evaluation;

internal static class WindowsRetainedArtifactIdentity
{
    internal static void RequireSingleLink(SafeFileHandle handle, string artifactId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            throw new IOException(
                $"Could not inspect retained package artifact '{artifactId}'.",
                new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        if (information.NumberOfLinks != 1)
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' must not be hard linked.");
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation information);

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
