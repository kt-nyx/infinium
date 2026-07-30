using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using Infinium.Mo2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Tests;

[SupportedOSPlatform("windows")]
internal static class WindowsProtectedRootCanary
{
    private const int FindStreamInfoStandard = 0;
    private const int FileAttributeTagInfo = 9;
    private const int ErrorHandleEof = 38;
    private const uint FileReadAttributes = 0x00000080;
    private const uint DeleteAccess = 0x00010000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private static readonly nint InvalidHandleValue = new(-1);

    internal static IReadOnlyDictionary<string, string> Capture(
        IReadOnlyList<string> roots)
    {
        return roots
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                path => path,
                FingerprintTree,
                StringComparer.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<string> CaptureTargetProcesses()
    {
        HashSet<string> targetNames = new(
            ["ModOrganizer", "SkyrimSE", "usvfs_proxy_x86", "usvfs_proxy_x64", "usvfs_proxy"],
            StringComparer.OrdinalIgnoreCase);
        System.Diagnostics.Process[] processes =
            System.Diagnostics.Process.GetProcesses();
        try
        {
            return processes
                .Where(process => targetNames.Contains(process.ProcessName))
                .Select(process => $"{process.Id}|{process.ProcessName}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            foreach (System.Diagnostics.Process process in processes)
            {
                process.Dispose();
            }
        }
    }

    internal static IReadOnlyList<string> ObserveReleasedRootHandles(
        IReadOnlyList<string> roots)
    {
        return roots.Select(ObserveExclusiveRenameEquivalentOpen).ToArray();
    }

    private static string FingerprintTree(string root)
    {
        StringBuilder canonical = new();
        foreach (string path in EnumerateWithoutFollowingReparses(root)
                     .OrderBy(
                         path => Path.GetRelativePath(root, path),
                         StringComparer.OrdinalIgnoreCase)
                     .ThenBy(
                         path => Path.GetRelativePath(root, path),
                         StringComparer.Ordinal))
        {
            FileAttributes attributes = File.GetAttributes(path);
            bool directory = (attributes & FileAttributes.Directory) != 0;
            FileSystemInfo info = directory
                ? new DirectoryInfo(path)
                : new FileInfo(path);
            WindowsObjectIdentity identity =
                WindowsReadOnlyObjectIdentity.Open(path, directory);
            string reparseIdentity = (attributes & FileAttributes.ReparsePoint) != 0
                ? ReadReparseIdentity(path, directory)
                : "not-reparse";
            canonical.Append(Path.GetRelativePath(root, path))
                .Append('|')
                .Append((long)attributes)
                .Append('|')
                .Append(info.CreationTimeUtc.Ticks)
                .Append('|')
                .Append(info.LastWriteTimeUtc.Ticks)
                .Append('|')
                .Append(identity.CanonicalValue)
                .Append('|')
                .Append(identity.NumberOfLinks)
                .Append('|')
                .Append(reparseIdentity)
                .Append('|')
                .Append(GetSddl(info, directory))
                .Append('|')
                .Append((attributes & FileAttributes.ReparsePoint) == 0
                    ? FingerprintAlternateStreams(path)
                    : "not-enumerated-for-reparse-object")
                .Append('|');
            if (!directory)
            {
                FileInfo file = (FileInfo)info;
                using FileStream stream = new(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read | FileShare.Write | FileShare.Delete);
                canonical.Append(file.Length)
                    .Append('|')
                    .Append(Convert.ToHexString(SHA256.HashData(stream)));
            }

            canonical.AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static List<string> EnumerateWithoutFollowingReparses(
        string root)
    {
        List<string> result = [root];
        Stack<string> directories = new();
        directories.Push(root);
        while (directories.Count > 0)
        {
            string directory = directories.Pop();
            foreach (string child in Directory.EnumerateFileSystemEntries(
                         directory,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                result.Add(child);
                FileAttributes attributes = File.GetAttributes(child);
                if ((attributes & FileAttributes.Directory) != 0
                    && (attributes & FileAttributes.ReparsePoint) == 0)
                {
                    directories.Push(child);
                }
            }
        }

        return result;
    }

    private static string GetSddl(FileSystemInfo info, bool directory)
    {
        FileSystemSecurity security = directory
            ? ((DirectoryInfo)info).GetAccessControl()
            : ((FileInfo)info).GetAccessControl();
        return security.GetSecurityDescriptorSddlForm(AccessControlSections.All);
    }

    private static string FingerprintAlternateStreams(string path)
    {
        List<string> streams = [];
        nint find = FindFirstStreamW(
            path,
            FindStreamInfoStandard,
            out Win32FindStreamData data,
            0);
        if (find == InvalidHandleValue)
        {
            int error = Marshal.GetLastWin32Error();
            return error == ErrorHandleEof
                ? string.Empty
                : throw new System.ComponentModel.Win32Exception(
                    error,
                    "Protected-root alternate streams could not be enumerated.");
        }

        try
        {
            do
            {
                string streamName = data.StreamName;
                if (!string.Equals(streamName, "::$DATA", StringComparison.OrdinalIgnoreCase))
                {
                    string streamPath = $"{path}{streamName}";
                    using FileStream stream = new(
                        streamPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read | FileShare.Write | FileShare.Delete);
                    streams.Add(
                        $"{streamName}|{data.StreamSize}|"
                        + Convert.ToHexString(SHA256.HashData(stream)));
                }
            }
            while (FindNextStreamW(find, out data));

            int error = Marshal.GetLastWin32Error();
            if (error != ErrorHandleEof)
            {
                throw new System.ComponentModel.Win32Exception(
                    error,
                    "Protected-root alternate stream enumeration failed.");
            }
        }
        finally
        {
            _ = FindClose(find);
        }

        return string.Join(
            ';',
            streams.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
    }

    private static string ReadReparseIdentity(string path, bool directory)
    {
        using SafeFileHandle handle = CreateFileW(
            path,
            FileReadAttributes,
            FileShareRead | FileShareWrite | FileShareDelete,
            0,
            OpenExisting,
            FileFlagOpenReparsePoint | (directory ? FileFlagBackupSemantics : 0),
            0);
        if (handle.IsInvalid)
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "The protected-root reparse object could not be opened.");
        }

        if (!GetFileInformationByHandleEx(
                handle,
                FileAttributeTagInfo,
                out FileAttributeTagInformation tag,
                checked((uint)Marshal.SizeOf<FileAttributeTagInformation>())))
        {
            throw new System.ComponentModel.Win32Exception(
                Marshal.GetLastWin32Error(),
                "The protected-root reparse tag could not be read.");
        }

        FileSystemInfo info = directory
            ? new DirectoryInfo(path)
            : new FileInfo(path);
        string target = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
            ?? throw new InvalidDataException(
                "A protected reparse object has no resolvable target.");
        return FormattableString.Invariant(
            $"tag={tag.ReparseTag:X8}|target={Path.GetFullPath(target)}");
    }

    private static string ObserveExclusiveRenameEquivalentOpen(string path)
    {
        using SafeFileHandle handle = CreateFileW(
            path,
            DeleteAccess | FileReadAttributes,
            shareMode: 0,
            0,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            0);
        if (handle.IsInvalid)
        {
            throw new AssertFailedException(
                "Protected root retained a handle that prevents rename-equivalent "
                + $"exclusive access: {path} (Win32 {Marshal.GetLastWin32Error()}).");
        }

        WindowsObjectIdentity identity = WindowsReadOnlyObjectIdentity.Read(handle);
        return FormattableString.Invariant(
            $"{path}|{identity.CanonicalValue}|exclusive-delete-open");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Win32FindStreamData
    {
        public long StreamSize;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
        public string StreamName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileAttributeTagInformation
    {
        public uint FileAttributes;
        public uint ReparseTag;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint FindFirstStreamW(
        string fileName,
        int infoLevel,
        out Win32FindStreamData findStreamData,
        uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindNextStreamW(
        nint findStream,
        out Win32FindStreamData findStreamData);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FindClose(nint findFile);

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
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        out FileAttributeTagInformation fileInformation,
        uint bufferSize);
}
