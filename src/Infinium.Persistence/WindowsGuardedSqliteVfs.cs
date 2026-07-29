using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Persistence;

/// <summary>
/// A narrow SQLite VFS shim that establishes opened-object authority before
/// delegating byte I/O and locking to SQLite's supported win32 VFS.
/// </summary>
internal sealed class WindowsGuardedSqliteVfs : IDisposable
{
    private const int SqliteOk = 0;
    private const int SqliteCantOpen = 14;
    private const int SqliteIoErrDelete = 2570;
    private const int SqliteOpenCreate = 0x00000004;
    private const int SqliteFcntlPersistWal = 10;

    private readonly Lock gate = new();
    private readonly string databasePath;
    private readonly string databaseLeaf;
    private readonly string vfsName;
    private readonly WindowsWriteAuthorityRegistry.ClassDirectoryCapability classCapability;
    private readonly Dictionary<string, GuardedFile> guardedFiles =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly nint parentVfs;
    private readonly VfsOpen parentOpen;
    private readonly VfsAccess parentAccess;
    private readonly VfsFullPathname parentFullPathname;
    private readonly VfsOpen open;
    private readonly VfsDelete delete;
    private readonly VfsAccess access;
    private readonly VfsFullPathname fullPathname;
    private nint vfsNameBuffer;
    private nint vfsBuffer;
    private Exception? lastCallbackError;
    private string? lastCallbackDetail;
    private bool disposed;

    public WindowsGuardedSqliteVfs(
        StoragePaths paths,
        ProductWriteClass writeClass,
        string databaseLeaf)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The guarded SQLite VFS currently requires Windows.");
        }

        ArgumentNullException.ThrowIfNull(paths);
        ValidateLeaf(databaseLeaf);
        this.databaseLeaf = databaseLeaf;
        databasePath = paths.ResolveProductPath(writeClass, databaseLeaf);
        classCapability = paths.GetBoundWriteClassCapability(writeClass);
        vfsName = $"infinium-guard-{Guid.NewGuid():N}";

        parentVfs = sqlite3_vfs_find(0);
        if (parentVfs == 0)
        {
            throw new InvalidOperationException("SQLite's default VFS is unavailable.");
        }

        SqliteVfs native = Marshal.PtrToStructure<SqliteVfs>(parentVfs);
        if (native.Version is < 1 or > 3
            || native.OsFileSize <= 0
            || native.Open == 0
            || native.Delete == 0
            || native.Access == 0
            || native.FullPathname == 0)
        {
            throw new InvalidOperationException(
                "SQLite's default VFS has an unsupported shape.");
        }

        string parentName = Marshal.PtrToStringUTF8(native.Name)
            ?? throw new InvalidOperationException(
                "SQLite's default VFS has no stable identity.");
        if (!string.Equals(parentName, "win32", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The guarded SQLite VFS requires the exact win32 parent, not '{parentName}'.");
        }

        parentOpen = Marshal.GetDelegateForFunctionPointer<VfsOpen>(native.Open);
        parentAccess = Marshal.GetDelegateForFunctionPointer<VfsAccess>(native.Access);
        parentFullPathname =
            Marshal.GetDelegateForFunctionPointer<VfsFullPathname>(native.FullPathname);
        open = Open;
        delete = Delete;
        access = Access;
        fullPathname = FullPathname;

        vfsNameBuffer = Marshal.StringToCoTaskMemUTF8(vfsName);
        native.Name = vfsNameBuffer;
        native.Next = 0;
        native.Open = Marshal.GetFunctionPointerForDelegate(open);
        native.Delete = Marshal.GetFunctionPointerForDelegate(delete);
        native.Access = Marshal.GetFunctionPointerForDelegate(access);
        native.FullPathname = Marshal.GetFunctionPointerForDelegate(fullPathname);
        vfsBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<SqliteVfs>());
        Marshal.StructureToPtr(native, vfsBuffer, fDeleteOld: false);
        int result = sqlite3_vfs_register(vfsBuffer, makeDefault: 0);
        if (result != SqliteOk)
        {
            DisposeNativeRegistration();
            throw new InvalidOperationException(
                $"The guarded SQLite VFS could not be registered ({result}).");
        }
    }

    public string Name
    {
        get
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return vfsName;
        }
    }

    public Exception? LastCallbackError
    {
        get
        {
            lock (gate)
            {
                return lastCallbackError;
            }
        }
    }

    public string? LastCallbackDetail
    {
        get
        {
            lock (gate)
            {
                return lastCallbackDetail;
            }
        }
    }

    public static void EnablePersistentWal(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        SQLitePCL.sqlite3 handle = connection.Handle
            ?? throw new InvalidOperationException("SQLite connection is not open.");
        int enabled = 1;
        nint main = Marshal.StringToCoTaskMemUTF8("main");
        int result;
        try
        {
            result = sqlite3_file_control(
                handle.DangerousGetHandle(),
                main,
                SqliteFcntlPersistWal,
                ref enabled);
        }
        finally
        {
            Marshal.FreeCoTaskMem(main);
        }

        if (result != SqliteOk || enabled != 1)
        {
            throw new InvalidOperationException(
                $"SQLite persistent-WAL control could not be enabled ({result}).");
        }
    }

    public void VerifyAllGuards()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            foreach (GuardedFile guarded in guardedFiles.Values)
            {
                guarded.Verify(classCapability);
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            if (vfsBuffer != 0)
            {
                _ = sqlite3_vfs_unregister(vfsBuffer);
            }

            foreach (GuardedFile guarded in guardedFiles.Values)
            {
                guarded.Dispose();
            }

            guardedFiles.Clear();
            DisposeNativeRegistration();
            disposed = true;
        }
    }

    private int Open(
        nint vfs,
        nint name,
        nint file,
        int flags,
        nint outputFlags)
    {
        try
        {
            string path = ReadSqlitePath(name);
            string leaf = GetAuthorizedLeaf(path);
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                EnsureGuarded(leaf, createIfMissing: (flags & SqliteOpenCreate) != 0);
            }

            int result = parentOpen(parentVfs, name, file, flags, outputFlags);
            RecordCallbackDetail($"open:{leaf}:flags={flags}:result={result}");
            return result;
        }
        catch (Exception exception)
        {
            RecordCallbackError(exception);
            return SqliteCantOpen;
        }
    }

    private int Delete(nint vfs, nint name, int syncDirectory)
    {
        try
        {
            string path = ReadSqlitePath(name);
            string leaf = GetAuthorizedLeaf(path);
            if (string.Equals(leaf, databaseLeaf, StringComparison.OrdinalIgnoreCase))
            {
                return SqliteIoErrDelete;
            }

            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                GuardedFile guarded = EnsureGuarded(leaf, createIfMissing: false);
                guarded.Delete(classCapability);
                guardedFiles.Remove(leaf);
                guarded.Dispose();
            }

            return SqliteOk;
        }
        catch (FileNotFoundException)
        {
            return SqliteOk;
        }
        catch (Exception exception)
        {
            RecordCallbackError(exception);
            return SqliteIoErrDelete;
        }
    }

    private int Access(nint vfs, nint name, int flags, nint result)
    {
        try
        {
            _ = GetAuthorizedLeaf(ReadSqlitePath(name));
            return parentAccess(parentVfs, name, flags, result);
        }
        catch (Exception exception)
        {
            RecordCallbackError(exception);
            if (result != 0)
            {
                Marshal.WriteInt32(result, 0);
            }

            return SqliteCantOpen;
        }
    }

    private int FullPathname(nint vfs, nint name, int outputLength, nint output)
    {
        try
        {
            string path = ReadSqlitePath(name);
            if (!string.Equals(
                    Path.GetFullPath(path),
                    databasePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"SQLite requested unexpected main filename '{path}'.");
            }

            return parentFullPathname(parentVfs, name, outputLength, output);
        }
        catch (Exception exception)
        {
            RecordCallbackError(exception);
            return SqliteCantOpen;
        }
    }

    private GuardedFile EnsureGuarded(string leaf, bool createIfMissing)
    {
        if (guardedFiles.TryGetValue(leaf, out GuardedFile? existing))
        {
            existing.Verify(classCapability);
            return existing;
        }

        GuardedFile guarded = GuardedFile.Open(
            classCapability,
            leaf,
            createIfMissing);
        guardedFiles.Add(leaf, guarded);
        return guarded;
    }

    private string GetAuthorizedLeaf(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.Equals(
                directory,
                classCapability.ConfiguredPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "SQLite requested a file outside its authorized write class.");
        }

        string leaf = Path.GetFileName(fullPath);
        if (!string.Equals(leaf, databaseLeaf, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(leaf, databaseLeaf + "-wal", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(leaf, databaseLeaf + "-shm", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(leaf, databaseLeaf + "-journal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "SQLite requested an undeclared auxiliary file.");
        }

        return leaf;
    }

    private static string ReadSqlitePath(nint value)
    {
        if (value == 0)
        {
            throw new InvalidOperationException(
                "On-disk SQLite temporary files are not authorized.");
        }

        return Marshal.PtrToStringUTF8(value)
            ?? throw new InvalidOperationException("SQLite supplied an invalid filename.");
    }

    private static void ValidateLeaf(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value is "." or ".."
            || value.Contains(':', StringComparison.Ordinal)
            || value.Contains('\0', StringComparison.Ordinal)
            || value.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || value.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The SQLite database name must be one typed relative leaf.",
                nameof(value));
        }
    }

    private void RecordCallbackError(Exception exception)
    {
        lock (gate)
        {
            lastCallbackError ??= exception;
        }
    }

    private void RecordCallbackDetail(string detail)
    {
        lock (gate)
        {
            lastCallbackDetail = detail;
        }
    }

    private void DisposeNativeRegistration()
    {
        if (vfsBuffer != 0)
        {
            Marshal.FreeHGlobal(vfsBuffer);
            vfsBuffer = 0;
        }

        if (vfsNameBuffer != 0)
        {
            Marshal.FreeCoTaskMem(vfsNameBuffer);
            vfsNameBuffer = 0;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VfsOpen(
        nint vfs,
        nint name,
        nint file,
        int flags,
        nint outputFlags);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VfsDelete(nint vfs, nint name, int syncDirectory);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VfsAccess(nint vfs, nint name, int flags, nint result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VfsFullPathname(
        nint vfs,
        nint name,
        int outputLength,
        nint output);

    [StructLayout(LayoutKind.Sequential)]
    private struct SqliteVfs
    {
        public int Version;
        public int OsFileSize;
        public int MaximumPathname;
        public nint Next;
        public nint Name;
        public nint ApplicationData;
        public nint Open;
        public nint Delete;
        public nint Access;
        public nint FullPathname;
        public nint DynamicLibraryOpen;
        public nint DynamicLibraryError;
        public nint DynamicLibrarySymbol;
        public nint DynamicLibraryClose;
        public nint Randomness;
        public nint Sleep;
        public nint CurrentTime;
        public nint GetLastError;
        public nint CurrentTimeInt64;
        public nint SetSystemCall;
        public nint GetSystemCall;
        public nint NextSystemCall;
    }

    private sealed class GuardedFile(
        string leafName,
        SafeFileHandle handle,
        ulong volumeSerialNumber,
        ulong fileId,
        string finalPath) : IDisposable
    {
        public string LeafName { get; } = leafName;

        public static GuardedFile Open(
            WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
            string leaf,
            bool createIfMissing)
        {
            using RelativeName nativeName = new(leaf);
            OBJECT_ATTRIBUTES attributes = new()
            {
                Length = Marshal.SizeOf<OBJECT_ATTRIBUTES>(),
                RootDirectory = capability.Handle.DangerousGetHandle(),
                ObjectName = nativeName.UnicodeString,
                Attributes = ObjCaseInsensitive,
            };
            int status = NtCreateFile(
                out SafeFileHandle handle,
                FileReadAttributes | Synchronize,
                ref attributes,
                out _,
                0,
                FileAttributeNormal,
                FileShareRead | FileShareWrite,
                createIfMissing ? FileOpenIf : FileOpen,
                FileNonDirectoryFile
                    | FileSynchronousIoNonAlert
                    | FileOpenReparsePoint,
                0,
                0);
            if (status < 0)
            {
                handle?.Dispose();
                int error = checked((int)RtlNtStatusToDosError(status));
                if (error is ErrorFileNotFound or ErrorPathNotFound)
                {
                    throw new FileNotFoundException(
                        "The authorized SQLite file does not exist.",
                        leaf);
                }

                throw new Win32Exception(
                    error,
                    "The authorized SQLite file could not be opened relative to its class handle.");
            }

            try
            {
                FileIdentity identity = ReadIdentity(handle);
                ValidateIdentity(capability, leaf, identity);
                GuardedFile result = new(
                    leaf,
                    handle,
                    identity.VolumeSerialNumber,
                    identity.FileId,
                    identity.FinalPath);
                handle = null!;
                return result;
            }
            finally
            {
                handle?.Dispose();
            }
        }

        public void Verify(
            WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability)
        {
            FileIdentity current = ReadIdentity(handle);
            ValidateIdentity(capability, LeafName, current);
            if (current.VolumeSerialNumber != volumeSerialNumber
                || current.FileId != fileId
                || !string.Equals(current.FinalPath, finalPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A guarded SQLite object changed identity.");
            }
        }

        public void Delete(
            WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability)
        {
            Verify(capability);
            handle.Dispose();
            using RelativeName nativeName = new(LeafName);
            OBJECT_ATTRIBUTES attributes = new()
            {
                Length = Marshal.SizeOf<OBJECT_ATTRIBUTES>(),
                RootDirectory = capability.Handle.DangerousGetHandle(),
                ObjectName = nativeName.UnicodeString,
                Attributes = ObjCaseInsensitive,
            };
            int status = NtCreateFile(
                out SafeFileHandle deleteHandle,
                FileReadAttributes | DeleteAccess | Synchronize,
                ref attributes,
                out _,
                0,
                FileAttributeNormal,
                FileShareRead | FileShareWrite | FileShareDelete,
                FileOpen,
                FileNonDirectoryFile
                    | FileSynchronousIoNonAlert
                    | FileOpenReparsePoint,
                0,
                0);
            if (status < 0)
            {
                deleteHandle?.Dispose();
                throw new Win32Exception(
                    checked((int)RtlNtStatusToDosError(status)),
                    "The guarded SQLite auxiliary file could not be reopened for deletion.");
            }

            using (deleteHandle)
            {
                FileIdentity identity = ReadIdentity(deleteHandle);
                ValidateIdentity(capability, LeafName, identity);
                if (identity.VolumeSerialNumber != volumeSerialNumber
                    || identity.FileId != fileId)
                {
                    throw new InvalidOperationException(
                        "A guarded SQLite auxiliary changed before deletion.");
                }

                FILE_DISPOSITION_INFO disposition = new() { DeleteFile = true };
                if (!SetFileInformationByHandle(
                        deleteHandle,
                        FileDispositionInfo,
                        ref disposition,
                        checked((uint)Marshal.SizeOf<FILE_DISPOSITION_INFO>())))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "The guarded SQLite auxiliary file could not be deleted by handle.");
                }
            }
        }

        public void Dispose() => handle.Dispose();

        private static void ValidateIdentity(
            WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
            string leaf,
            FileIdentity identity)
        {
            if ((identity.Attributes & FileAttributeReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "Reparse-point SQLite files are not authorized.");
            }

            if ((identity.Attributes & FileAttributeDirectory) != 0)
            {
                throw new InvalidOperationException(
                    "A SQLite file resolved to a directory.");
            }

            if (identity.NumberOfLinks != 1)
            {
                throw new InvalidOperationException(
                    "Hard-linked SQLite files are not authorized.");
            }

            if (identity.VolumeSerialNumber != capability.Identity.VolumeSerialNumber)
            {
                throw new InvalidOperationException(
                    "A SQLite file escaped to another volume.");
            }

            string expected = Path.Combine(capability.Identity.FinalPath, leaf);
            if (!string.Equals(
                    identity.FinalPath,
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "A SQLite file escaped its opened write-class directory.");
            }
        }

        private static FileIdentity ReadIdentity(SafeFileHandle handle)
        {
            if (!GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION information))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The guarded SQLite file identity could not be read.");
            }

            string path = ReadFinalPath(handle);
            ulong fileId = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
            return new FileIdentity(
                information.VolumeSerialNumber,
                fileId,
                information.NumberOfLinks,
                information.FileAttributes,
                path);
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
                        "The guarded SQLite file path could not be resolved.");
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

        private sealed class RelativeName : IDisposable
        {
            private readonly nint nameBuffer;
            private readonly nint unicodeStringBuffer;

            public RelativeName(string value)
            {
                nameBuffer = Marshal.StringToHGlobalUni(value);
                UNICODE_STRING name = new()
                {
                    Length = checked((ushort)(value.Length * sizeof(char))),
                    MaximumLength = checked((ushort)((value.Length + 1) * sizeof(char))),
                    Buffer = nameBuffer,
                };
                unicodeStringBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<UNICODE_STRING>());
                Marshal.StructureToPtr(name, unicodeStringBuffer, fDeleteOld: false);
            }

            public nint UnicodeString => unicodeStringBuffer;

            public void Dispose()
            {
                Marshal.FreeHGlobal(unicodeStringBuffer);
                Marshal.FreeHGlobal(nameBuffer);
            }
        }

        private sealed record FileIdentity(
            ulong VolumeSerialNumber,
            ulong FileId,
            uint NumberOfLinks,
            uint Attributes,
            string FinalPath);
    }

    private const uint FileReadAttributes = 0x00000080;
    private const uint DeleteAccess = 0x00010000;
    private const uint Synchronize = 0x00100000;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint FileOpen = 1;
    private const uint FileOpenIf = 3;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint ObjCaseInsensitive = 0x00000040;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int FileDispositionInfo = 4;
    private const uint FileNameNormalized = 0;
    private const uint VolumeNameDos = 0;

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

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public FILETIME CreationTime;
        public FILETIME LastAccessTime;
        public FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_DISPOSITION_INFO
    {
        [MarshalAs(UnmanagedType.Bool)]
        public bool DeleteFile;
    }

    [DllImport("e_sqlite3", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint sqlite3_vfs_find(nint name);

    [DllImport("e_sqlite3", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_vfs_register(nint vfs, int makeDefault);

    [DllImport("e_sqlite3", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_vfs_unregister(nint vfs);

    [DllImport("e_sqlite3", CallingConvention = CallingConvention.Cdecl)]
    private static extern int sqlite3_file_control(
        nint database,
        nint databaseName,
        int operation,
        ref int argument);

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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out BY_HANDLE_FILE_INFORMATION information);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int fileInformationClass,
        ref FILE_DISPOSITION_INFO fileInformation,
        uint bufferSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle file,
        [Out] char[] filePath,
        uint filePathLength,
        uint flags);
}
