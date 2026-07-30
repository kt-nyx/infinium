using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Mo2;

internal sealed record WindowsObjectIdentity(
    uint VolumeSerialNumber,
    ulong FileId,
    uint NumberOfLinks,
    FileAttributes Attributes,
    string FinalPath,
    long ByteLength,
    long LastWriteUtcTicks)
{
    public string CanonicalValue =>
        FormattableString.Invariant($"{VolumeSerialNumber:X8}:{FileId:X16}");
}

internal sealed class WindowsReadOnlyObjectHandle(
    SafeFileHandle handle,
    WindowsObjectIdentity identity) : IDisposable
{
    public SafeFileHandle Handle { get; } = handle;

    public WindowsObjectIdentity Identity { get; } = identity;

    public void Dispose() => Handle.Dispose();
}

internal sealed record WindowsReadOnlyDirectoryEntry(string Name, bool IsDirectory);

internal static class WindowsReadOnlyObjectIdentity
{
    public static WindowsObjectIdentity Open(string path, bool directory)
    {
        using WindowsReadOnlyObjectHandle opened = OpenHandle(path, directory);
        return opened.Identity;
    }

    public static WindowsReadOnlyObjectHandle OpenHandle(string path, bool directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "MO2 snapshot object identity requires Windows.");
        }

        SafeFileHandle handle = CreateFileW(
            path,
            FileReadAttributes | (directory ? FileListDirectory : 0),
            FileShareRead | FileShareWrite | FileShareDelete,
            0,
            OpenExisting,
            (directory ? FileFlagBackupSemantics : 0) | FileFlagOpenReparsePoint,
            0);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The selected Windows object identity could not be opened.");
        }

        try
        {
            return new WindowsReadOnlyObjectHandle(handle, Read(handle));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public static WindowsReadOnlyObjectHandle OpenRelative(
        WindowsReadOnlyObjectHandle parent,
        string leaf,
        bool directory)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ValidateLeaf(leaf);
        using RelativeName nativeName = new(leaf);
        OBJECT_ATTRIBUTES attributes = new()
        {
            Length = Marshal.SizeOf<OBJECT_ATTRIBUTES>(),
            RootDirectory = parent.Handle.DangerousGetHandle(),
            ObjectName = nativeName.UnicodeString,
            Attributes = ObjCaseInsensitive,
        };
        int status = NtCreateFile(
            out SafeFileHandle handle,
            FileReadAttributes
                | Synchronize
                | (directory ? FileListDirectory : 0),
            ref attributes,
            out _,
            0,
            FileAttributeNormal,
            FileShareRead | FileShareWrite | FileShareDelete,
            FileOpen,
            FileOpenReparsePoint
                | FileSynchronousIoNonAlert
                | (directory ? FileDirectoryFile : FileNonDirectoryFile),
            0,
            0);
        if (status < 0)
        {
            handle?.Dispose();
            int error = checked((int)RtlNtStatusToDosError(status));
            throw new Win32Exception(
                error,
                "The handle-relative snapshot object could not be opened.");
        }

        try
        {
            return new WindowsReadOnlyObjectHandle(handle, Read(handle));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public static IReadOnlyList<WindowsReadOnlyDirectoryEntry> EnumerateChildren(
        WindowsReadOnlyObjectHandle directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if ((directory.Identity.Attributes & FileAttributes.Directory) == 0)
        {
            throw new ArgumentException(
                "Directory enumeration requires an opened directory.",
                nameof(directory));
        }

        const int bufferSize = 64 * 1024;
        byte[] buffer = new byte[bufferSize];
        List<WindowsReadOnlyDirectoryEntry> result = [];
        bool restart = true;
        while (true)
        {
            bool success = GetFileInformationByHandleEx(
                directory.Handle,
                restart ? FileIdBothDirectoryRestartInfo : FileIdBothDirectoryInfo,
                buffer,
                checked((uint)buffer.Length));
            restart = false;
            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ErrorNoMoreFiles)
                {
                    return result;
                }

                throw new Win32Exception(
                    error,
                    "The opened snapshot directory could not be enumerated.");
            }

            int offset = 0;
            while (true)
            {
                const int fileNameOffset = 104;
                int next = BitConverter.ToInt32(buffer, offset);
                int nameByteLength = BitConverter.ToInt32(buffer, offset + 60);
                FileAttributes attributes =
                    (FileAttributes)BitConverter.ToInt32(buffer, offset + 56);
                if (nameByteLength < 0
                    || (nameByteLength & 1) != 0
                    || offset + fileNameOffset + nameByteLength > buffer.Length)
                {
                    throw new InvalidDataException(
                        "Windows returned a malformed directory-information record.");
                }

                string name = Encoding.Unicode.GetString(
                    buffer,
                    offset + fileNameOffset,
                    nameByteLength);
                if (name is not "." and not "..")
                {
                    ValidateLeaf(name);
                    result.Add(new WindowsReadOnlyDirectoryEntry(
                        name,
                        (attributes & FileAttributes.Directory) != 0));
                }

                if (next == 0)
                {
                    break;
                }

                if (next < fileNameOffset || offset + next >= buffer.Length)
                {
                    throw new InvalidDataException(
                        "Windows returned an invalid directory-information offset.");
                }

                offset += next;
            }
        }
    }

    public static FileStream OpenStableRelativeRead(
        string rootPath,
        string relativePath)
    {
        string[] components = ValidateRelativePath(relativePath);
        WindowsReadOnlyObjectHandle current = OpenHandle(rootPath, directory: true);
        try
        {
            ValidateContainedObject(current.Identity, Path.GetFullPath(rootPath), null);
            for (int index = 0; index < components.Length - 1; index++)
            {
                WindowsReadOnlyObjectHandle next =
                    OpenRelative(current, components[index], directory: true);
                try
                {
                    ValidateContainedObject(
                        next.Identity,
                        Path.Combine(current.Identity.FinalPath, components[index]),
                        current.Identity.VolumeSerialNumber);
                }
                catch
                {
                    next.Dispose();
                    throw;
                }

                current.Dispose();
                current = next;
            }

            string leaf = components[^1];
            WindowsReadOnlyObjectHandle file =
                OpenRelativeForRead(current, leaf);
            try
            {
                ValidateContainedObject(
                    file.Identity,
                    Path.Combine(current.Identity.FinalPath, leaf),
                    current.Identity.VolumeSerialNumber);
                if (file.Identity.NumberOfLinks != 1)
                {
                    throw new InvalidDataException(
                        "Hard-linked snapshot control files are outside the qualified read surface.");
                }

                return new FileStream(
                    file.Handle,
                    FileAccess.Read,
                    bufferSize: 64 * 1024,
                    isAsync: false);
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
        finally
        {
            current.Dispose();
        }
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
            finalPath,
            checked(((long)information.FileSizeHigh << 32) | information.FileSizeLow),
            DateTime.FromFileTimeUtc(
                    checked(((long)information.LastWriteTime.HighDateTime << 32)
                            | information.LastWriteTime.LowDateTime))
                .Ticks);
    }

    public static void ValidateContainedObject(
        WindowsObjectIdentity identity,
        string expectedPath,
        uint? expectedVolume)
    {
        if ((identity.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "Reparse points are outside the qualified snapshot surface.");
        }

        if (expectedVolume is not null
            && identity.VolumeSerialNumber != expectedVolume.Value)
        {
            throw new InvalidDataException(
                "A handle-relative snapshot object crossed its retained volume.");
        }

        if (!string.Equals(
                identity.FinalPath,
                Path.GetFullPath(expectedPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "A handle-relative snapshot object escaped its opened parent.");
        }
    }

    private static WindowsReadOnlyObjectHandle OpenRelativeForRead(
        WindowsReadOnlyObjectHandle parent,
        string leaf)
    {
        ValidateLeaf(leaf);
        using RelativeName nativeName = new(leaf);
        OBJECT_ATTRIBUTES attributes = new()
        {
            Length = Marshal.SizeOf<OBJECT_ATTRIBUTES>(),
            RootDirectory = parent.Handle.DangerousGetHandle(),
            ObjectName = nativeName.UnicodeString,
            Attributes = ObjCaseInsensitive,
        };
        int status = NtCreateFile(
            out SafeFileHandle handle,
            GenericRead | FileReadAttributes | Synchronize,
            ref attributes,
            out _,
            0,
            FileAttributeNormal,
            FileShareRead,
            FileOpen,
            FileOpenReparsePoint
                | FileSynchronousIoNonAlert
                | FileNonDirectoryFile,
            0,
            0);
        if (status < 0)
        {
            handle?.Dispose();
            int error = checked((int)RtlNtStatusToDosError(status));
            throw new Win32Exception(
                error,
                "The handle-relative snapshot file could not be opened for stable reading.");
        }

        try
        {
            return new WindowsReadOnlyObjectHandle(handle, Read(handle));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static string[] ValidateRelativePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || Path.IsPathFullyQualified(value)
            || value.StartsWith(@"\\", StringComparison.Ordinal)
            || value.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A safe relative snapshot path is required.");
        }

        string[] components = value.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 0)
        {
            throw new InvalidDataException("A relative snapshot path cannot be empty.");
        }

        foreach (string component in components)
        {
            ValidateLeaf(component);
        }

        return components;
    }

    private static void ValidateLeaf(string leaf)
    {
        if (string.IsNullOrWhiteSpace(leaf)
            || leaf is "." or ".."
            || leaf.IndexOfAny(
                [
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar,
                    Path.VolumeSeparatorChar,
                ]) >= 0
            || leaf.Contains('\0'))
        {
            throw new InvalidDataException(
                "A handle-relative snapshot component is invalid.");
        }
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
    private const uint FileListDirectory = 0x00000001;
    private const uint GenericRead = 0x80000000;
    private const uint Synchronize = 0x00100000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileOpen = 1;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint ObjCaseInsensitive = 0x00000040;
    private const int FileIdBothDirectoryInfo = 10;
    private const int FileIdBothDirectoryRestartInfo = 11;
    private const int ErrorNoMoreFiles = 18;
    private const uint FileNameNormalized = 0;
    private const uint VolumeNameDos = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_STATUS_BLOCK
    {
        public nint Status;
        public nuint Information;
    }

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

    private sealed class RelativeName : IDisposable
    {
        private readonly nint text;
        private readonly nint structure;

        public RelativeName(string value)
        {
            text = Marshal.StringToHGlobalUni(value);
            UNICODE_STRING unicode = new()
            {
                Length = checked((ushort)(value.Length * sizeof(char))),
                MaximumLength = checked((ushort)((value.Length + 1) * sizeof(char))),
                Buffer = text,
            };
            structure = Marshal.AllocHGlobal(Marshal.SizeOf<UNICODE_STRING>());
            Marshal.StructureToPtr(unicode, structure, fDeleteOld: false);
        }

        public nint UnicodeString => structure;

        public void Dispose()
        {
            Marshal.FreeHGlobal(structure);
            Marshal.FreeHGlobal(text);
        }
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
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        [Out] byte[] fileInformation,
        uint bufferSize);

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

    [DllImport("ntdll.dll")]
    private static extern int NtCreateFile(
        out SafeFileHandle fileHandle,
        uint desiredAccess,
        ref OBJECT_ATTRIBUTES objectAttributes,
        out IO_STATUS_BLOCK ioStatusBlock,
        long allocationSize,
        uint fileAttributes,
        uint shareAccess,
        uint createDisposition,
        uint createOptions,
        nint eaBuffer,
        uint eaLength);

    [DllImport("ntdll.dll")]
    private static extern uint RtlNtStatusToDosError(int status);
}
