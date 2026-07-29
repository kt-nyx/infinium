using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Persistence;

internal static class WindowsHandleRelativeStorage
{
    private const int MaximumComponents = 64;
    private const int MaximumDeleteEntries = 100_000;

    public static SafeFileHandle OpenOrCreateDirectory(
        SafeFileHandle root,
        WindowsWriteAuthorityRegistry.WindowsObjectIdentity rootIdentity,
        string relativePath,
        bool create,
        byte[]? securityDescriptor = null,
        bool requireDeleteAccess = false)
    {
        string[] components = ValidateRelativePath(relativePath, allowEmpty: true);
        SafeFileHandle current = Duplicate(root);
        string expected = rootIdentity.FinalPath;
        try
        {
            foreach ((string component, int index) in components.Select(
                         (component, index) => (component, index)))
            {
                bool final = index == components.Length - 1;
                SafeFileHandle next = OpenRelative(
                    current,
                    component,
                    FileListDirectory
                        | FileAddFile
                        | FileAddSubdirectory
                        | FileReadAttributes
                        | ReadControl
                        | Synchronize
                        | (final && requireDeleteAccess ? DeleteAccess : 0),
                    FileShareRead | FileShareWrite,
                    create ? FileOpenIf : FileOpen,
                    FileDirectoryFile
                        | FileSynchronousIoNonAlert
                        | FileOpenReparsePoint,
                    final ? securityDescriptor : null);
                current.Dispose();
                current = next;
                expected = Path.Combine(expected, component);
                ValidateOpenedObject(
                    current,
                    rootIdentity.VolumeSerialNumber,
                    expected,
                    requireDirectory: true);
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    public static FileStream CreateNewFile(
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string relativePath)
    {
        (SafeFileHandle parent, string leaf, string expected) =
            OpenParent(capability, relativePath, createParents: true);
        using (parent)
        {
            SafeFileHandle file = OpenRelative(
                parent,
                leaf,
                GenericWrite | FileReadAttributes | Synchronize,
                FileShareRead,
                FileCreate,
                FileNonDirectoryFile
                    | FileSynchronousIoNonAlert
                    | FileOpenReparsePoint,
                securityDescriptor: null);
            try
            {
                ValidateOpenedObject(
                    file,
                    capability.Identity.VolumeSerialNumber,
                    expected,
                    requireDirectory: false);
                return new FileStream(
                    file,
                    FileAccess.Write,
                    bufferSize: 4096,
                    isAsync: false);
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
    }

    public static FileStream OpenReadFile(
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string relativePath)
    {
        (SafeFileHandle parent, string leaf, string expected) =
            OpenParent(capability, relativePath, createParents: false);
        using (parent)
        {
            SafeFileHandle file = OpenRelative(
                parent,
                leaf,
                GenericRead | FileReadAttributes | Synchronize,
                FileShareRead,
                FileOpen,
                FileNonDirectoryFile
                    | FileSynchronousIoNonAlert
                    | FileOpenReparsePoint,
                securityDescriptor: null);
            try
            {
                ValidateOpenedObject(
                    file,
                    capability.Identity.VolumeSerialNumber,
                    expected,
                    requireDirectory: false);
                return new FileStream(
                    file,
                    FileAccess.Read,
                    bufferSize: 4096,
                    isAsync: false);
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
    }

    public static AdmissionSource OpenAdmissionSource(
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string relativePath)
    {
        (SafeFileHandle parent, string leaf, string expected) =
            OpenParent(capability, relativePath, createParents: false);
        using (parent)
        {
            SafeFileHandle file = OpenRelative(
                parent,
                leaf,
                GenericRead | DeleteAccess | FileReadAttributes | Synchronize,
                FileShareRead,
                FileOpen,
                FileNonDirectoryFile
                    | FileSynchronousIoNonAlert
                    | FileOpenReparsePoint,
                securityDescriptor: null);
            try
            {
                ValidateOpenedObject(
                    file,
                    capability.Identity.VolumeSerialNumber,
                    expected,
                    requireDirectory: false);
                return new AdmissionSource(
                    file,
                    capability.Identity.VolumeSerialNumber,
                    expected);
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
    }

    public static SafeFileHandle CreateOrOpenDirectory(
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string relativePath)
    {
        return OpenOrCreateDirectory(
            capability.Handle,
            capability.Identity,
            relativePath,
            create: true);
    }

    public static bool FileExists(
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string relativePath)
    {
        try
        {
            using FileStream ignored = OpenReadFile(capability, relativePath);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public static void DeleteFile(
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string relativePath,
        bool missingIsSuccess = false)
    {
        (SafeFileHandle parent, string leaf, string expected) opened;
        try
        {
            opened = OpenParent(capability, relativePath, createParents: false);
        }
        catch (DirectoryNotFoundException) when (missingIsSuccess)
        {
            return;
        }

        using (opened.parent)
        {
            SafeFileHandle file;
            try
            {
                file = OpenRelative(
                    opened.parent,
                    opened.leaf,
                    DeleteAccess | FileReadAttributes | Synchronize,
                    FileShareRead | FileShareWrite,
                    FileOpen,
                    FileNonDirectoryFile
                        | FileSynchronousIoNonAlert
                        | FileOpenReparsePoint,
                    securityDescriptor: null);
            }
            catch (FileNotFoundException) when (missingIsSuccess)
            {
                return;
            }

            using (file)
            {
                ValidateOpenedObject(
                    file,
                    capability.Identity.VolumeSerialNumber,
                    opened.expected,
                    requireDirectory: false);
                MarkDelete(file);
            }
        }
    }

    public static void CopyFile(
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability sourceCapability,
        string sourceRelativePath,
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability destinationCapability,
        string destinationRelativePath,
        long expectedLength,
        string expectedSha256)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);
        bool destinationCreated = false;
        try
        {
            using FileStream source = OpenReadFile(sourceCapability, sourceRelativePath);
            using FileStream destination =
                CreateNewFile(destinationCapability, destinationRelativePath);
            destinationCreated = true;
            using System.Security.Cryptography.IncrementalHash hash =
                System.Security.Cryptography.IncrementalHash.CreateHash(
                    System.Security.Cryptography.HashAlgorithmName.SHA256);
            byte[] buffer = new byte[64 * 1024];
            long total = 0;
            while (true)
            {
                int read = source.Read(buffer);
                if (read == 0)
                {
                    break;
                }

                total = checked(total + read);
                if (total > expectedLength)
                {
                    throw new InvalidOperationException(
                        "A backup payload exceeds its authoritative byte length.");
                }

                hash.AppendData(buffer, 0, read);
                destination.Write(buffer, 0, read);
            }

            string actualSha256 =
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            if (total != expectedLength
                || !string.Equals(
                    actualSha256,
                    expectedSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A backup payload does not match its authoritative digest.");
            }

            destination.Flush(flushToDisk: true);
        }
        catch
        {
            if (destinationCreated)
            {
                DeleteFile(
                    destinationCapability,
                    destinationRelativePath,
                    missingIsSuccess: true);
            }

            throw;
        }
    }

    public static AdmissionCopyResult PublishAdmissionSource(
        AdmissionSource source,
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability destinationCapability,
        string destinationRelativePath,
        string expectedSha256,
        long expectedLength,
        long maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(source);
        (SafeFileHandle parent, string leaf, string expected) =
            OpenParent(destinationCapability, destinationRelativePath, createParents: true);
        using (parent)
        {
            string temporaryLeaf = $".{leaf}.{Guid.NewGuid():N}.tmp";
            SafeFileHandle temporary = OpenRelative(
                parent,
                temporaryLeaf,
                GenericWrite | DeleteAccess | FileReadAttributes | Synchronize,
                FileShareRead | FileShareWrite,
                FileCreate,
                FileNonDirectoryFile
                    | FileSynchronousIoNonAlert
                    | FileOpenReparsePoint,
                securityDescriptor: null);
            bool published = false;
            try
            {
                string expectedTemporary = Path.Combine(
                    destinationCapability.Identity.FinalPath,
                    Path.GetDirectoryName(destinationRelativePath) ?? string.Empty,
                    temporaryLeaf);
                ValidateOpenedObject(
                    temporary,
                    destinationCapability.Identity.VolumeSerialNumber,
                    expectedTemporary,
                    requireDirectory: false);
                AdmissionCopyResult result;
                using (FileStream destination = new(
                           Duplicate(temporary),
                           FileAccess.Write,
                           bufferSize: 4096,
                           isAsync: false))
                {
                    result = source.CopyToAndHash(destination, maximumBytes);
                    destination.Flush(flushToDisk: true);
                }

                if (result.ByteLength != expectedLength
                    || !string.Equals(
                        result.Sha256,
                        expectedSha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The staged output bytes do not match the declared manifest.");
                }

                SafeFileHandle? existing = TryOpenFile(
                    parent,
                    leaf,
                    destinationCapability,
                    expected);
                using (existing)
                {
                    if (existing is not null)
                    {
                        using FileStream existingStream = new(
                            Duplicate(existing),
                            FileAccess.Read,
                            bufferSize: 4096,
                            isAsync: false);
                        if (existingStream.Length != result.ByteLength
                            || !string.Equals(
                                Convert.ToHexString(
                                        System.Security.Cryptography.SHA256.HashData(
                                            existingStream))
                                    .ToLowerInvariant(),
                                result.Sha256,
                                StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                "A conflicting object already occupies the content-addressed path.");
                        }

                        return result;
                    }
                }

                RenameByHandle(temporary, parent, leaf, replace: false);
                ValidateOpenedObject(
                    temporary,
                    destinationCapability.Identity.VolumeSerialNumber,
                    expected,
                    requireDirectory: false);
                published = true;
                return result;
            }
            finally
            {
                if (!published && !temporary.IsInvalid && !temporary.IsClosed)
                {
                    try
                    {
                        MarkDelete(temporary);
                    }
                    catch (Win32Exception)
                    {
                        // Preserve the admission or collision failure.
                    }
                }

                temporary.Dispose();
            }
        }
    }

    public static void WriteAllBytesAtomic(
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string relativePath,
        ReadOnlySpan<byte> bytes)
    {
        string[] components = ValidateRelativePath(relativePath, allowEmpty: false);
        string leaf = components[^1];
        string parentRelative = string.Join(
            Path.DirectorySeparatorChar,
            components[..^1]);
        using SafeFileHandle parent = OpenOrCreateDirectory(
            capability.Handle,
            capability.Identity,
            parentRelative,
            create: true);
        using SafeFileHandle? existing = TryOpenReplacementTarget(
            parent,
            capability,
            leaf,
            Path.Combine(capability.Identity.FinalPath, parentRelative, leaf));
        string temporaryLeaf = $".{leaf}.{Guid.NewGuid():N}.tmp";
        SafeFileHandle temporary = OpenRelative(
            parent,
            temporaryLeaf,
            GenericWrite | DeleteAccess | FileReadAttributes | Synchronize,
            FileShareRead | FileShareWrite,
            FileCreate,
            FileNonDirectoryFile
                | FileSynchronousIoNonAlert
                | FileOpenReparsePoint,
            securityDescriptor: null);
        bool published = false;
        try
        {
            string expectedTemporary = Path.Combine(
                capability.Identity.FinalPath,
                parentRelative,
                temporaryLeaf);
            ValidateOpenedObject(
                temporary,
                capability.Identity.VolumeSerialNumber,
                expectedTemporary,
                requireDirectory: false);
            using (FileStream stream = new(
                       Duplicate(temporary),
                       FileAccess.Write,
                       bufferSize: 4096,
                       isAsync: false))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (existing is not null)
            {
                ValidateOpenedObject(
                    existing,
                    capability.Identity.VolumeSerialNumber,
                    Path.Combine(
                        capability.Identity.FinalPath,
                        parentRelative,
                        leaf),
                    requireDirectory: false);
                existing.Dispose();
            }

            RenameByHandle(
                temporary,
                parent,
                leaf,
                replace: existing is not null);
            string expectedFinal = Path.Combine(
                capability.Identity.FinalPath,
                parentRelative,
                leaf);
            ValidateOpenedObject(
                temporary,
                capability.Identity.VolumeSerialNumber,
                expectedFinal,
                requireDirectory: false);
            published = true;
        }
        finally
        {
            if (!published && !temporary.IsInvalid && !temporary.IsClosed)
            {
                try
                {
                    MarkDelete(temporary);
                }
                catch (Win32Exception)
                {
                    // Preserve the primary publication failure.
                }
            }

            temporary.Dispose();
        }
    }

    public static void RenameDirectory(
        SafeFileHandle directory,
        SafeFileHandle destinationParent,
        string destinationLeaf)
    {
        ValidateComponent(destinationLeaf);
        RenameByHandle(directory, destinationParent, destinationLeaf, replace: false);
    }

    public static void DeleteDirectoryTree(
        SafeFileHandle directory,
        WindowsWriteAuthorityRegistry.WindowsObjectIdentity identity,
        string configuredPath)
    {
        int deleted = 0;
        DeleteChildren(directory, identity, configuredPath, ref deleted);
        MarkDelete(directory);
    }

    public static void DeleteDirectoryTree(
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string relativePath,
        bool missingIsSuccess = false)
    {
        SafeFileHandle directory;
        try
        {
            directory = OpenOrCreateDirectory(
                capability.Handle,
                capability.Identity,
                relativePath,
                create: false,
                requireDeleteAccess: true);
        }
        catch (DirectoryNotFoundException) when (missingIsSuccess)
        {
            return;
        }
        catch (FileNotFoundException) when (missingIsSuccess)
        {
            return;
        }

        using (directory)
        {
            WindowsWriteAuthorityRegistry.WindowsObjectIdentity identity =
                ReadIdentity(directory);
            DeleteDirectoryTree(
                directory,
                identity,
                Path.Combine(capability.ConfiguredPath, relativePath));
        }
    }

    public static IReadOnlyList<TreeEntryIdentity> CaptureTree(
        SafeFileHandle directory,
        WindowsWriteAuthorityRegistry.WindowsObjectIdentity identity,
        string configuredPath)
    {
        List<TreeEntryIdentity> entries = [];
        int visited = 0;
        CaptureChildren(
            directory,
            identity,
            configuredPath,
            relativePrefix: string.Empty,
            entries,
            ref visited);
        return entries
            .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static WindowsWriteAuthorityRegistry.WindowsObjectIdentity ReadIdentity(
        SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION information))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The opened Windows object identity could not be read.");
        }

        ulong fileId = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        return new WindowsWriteAuthorityRegistry.WindowsObjectIdentity(
            information.VolumeSerialNumber,
            fileId,
            ReadFinalPath(handle),
            information.NumberOfLinks,
            (information.FileAttributes & FileAttributeDirectory) != 0);
    }

    private static void DeleteChildren(
        SafeFileHandle directory,
        WindowsWriteAuthorityRegistry.WindowsObjectIdentity directoryIdentity,
        string configuredPath,
        ref int deleted)
    {
        while (true)
        {
            string[] entries = Directory
                .EnumerateFileSystemEntries(configuredPath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToArray();
            if (entries.Length == 0)
            {
                return;
            }

            foreach (string leaf in entries)
            {
                ValidateComponent(leaf);
                if (++deleted > MaximumDeleteEntries)
                {
                    throw new InvalidOperationException(
                        "The internal product-tree deletion bound was exceeded.");
                }

                SafeFileHandle child;
                try
                {
                    child = OpenRelative(
                        directory,
                        leaf,
                        DeleteAccess
                            | FileListDirectory
                            | FileReadAttributes
                            | Synchronize,
                        FileShareRead | FileShareWrite,
                        FileOpen,
                        FileSynchronousIoNonAlert
                            | FileOpenReparsePoint
                            | FileBackupIntent,
                        securityDescriptor: null);
                }
                catch (FileNotFoundException)
                {
                    continue;
                }

                using (child)
                {
                    WindowsWriteAuthorityRegistry.WindowsObjectIdentity childIdentity =
                        ReadIdentity(child);
                    string expected = Path.Combine(directoryIdentity.FinalPath, leaf);
                    if (childIdentity.VolumeSerialNumber != directoryIdentity.VolumeSerialNumber
                        || !string.Equals(
                            childIdentity.FinalPath,
                            expected,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "An internal deletion target escaped its opened parent.");
                    }

                    uint attributes = GetAttributes(child);
                    bool reparse = (attributes & FileAttributeReparsePoint) != 0;
                    if (childIdentity.IsDirectory && !reparse)
                    {
                        DeleteChildren(
                            child,
                            childIdentity,
                            Path.Combine(configuredPath, leaf),
                            ref deleted);
                    }
                    else if (!childIdentity.IsDirectory && childIdentity.NumberOfLinks != 1)
                    {
                        throw new InvalidOperationException(
                            "A hard-linked internal deletion target is not authorized.");
                    }

                    MarkDelete(child);
                }
            }
        }
    }

    private static void CaptureChildren(
        SafeFileHandle directory,
        WindowsWriteAuthorityRegistry.WindowsObjectIdentity directoryIdentity,
        string configuredPath,
        string relativePrefix,
        List<TreeEntryIdentity> entries,
        ref int visited)
    {
        foreach (string entryPath in Directory.EnumerateFileSystemEntries(configuredPath))
        {
            string leaf = Path.GetFileName(entryPath);
            ValidateComponent(leaf);
            if (++visited > MaximumDeleteEntries)
            {
                throw new InvalidOperationException(
                    "The internal product-tree inspection bound was exceeded.");
            }

            SafeFileHandle child = OpenRelative(
                directory,
                leaf,
                FileListDirectory | FileReadAttributes | Synchronize,
                FileShareRead | FileShareWrite,
                FileOpen,
                FileSynchronousIoNonAlert
                    | FileOpenReparsePoint
                    | FileBackupIntent,
                securityDescriptor: null);
            try
            {
                string expected = Path.Combine(directoryIdentity.FinalPath, leaf);
                WindowsWriteAuthorityRegistry.WindowsObjectIdentity childIdentity =
                    ReadIdentity(child);
                ValidateOpenedObject(
                    child,
                    directoryIdentity.VolumeSerialNumber,
                    expected,
                    requireDirectory: childIdentity.IsDirectory);
                string relative = string.IsNullOrEmpty(relativePrefix)
                    ? leaf
                    : Path.Combine(relativePrefix, leaf);
                if (childIdentity.IsDirectory)
                {
                    entries.Add(
                        new TreeEntryIdentity(
                            relative,
                            childIdentity.VolumeSerialNumber,
                            childIdentity.FileId,
                            childIdentity.NumberOfLinks,
                            IsDirectory: true,
                            ByteLength: 0,
                            Sha256: null));
                    CaptureChildren(
                        child,
                        childIdentity,
                        entryPath,
                        relative,
                        entries,
                        ref visited);
                }
                else
                {
                    child.Dispose();
                    child = OpenRelative(
                        directory,
                        leaf,
                        GenericRead | FileReadAttributes | Synchronize,
                        FileShareRead,
                        FileOpen,
                        FileNonDirectoryFile
                            | FileSynchronousIoNonAlert
                            | FileOpenReparsePoint,
                        securityDescriptor: null);
                    ValidateOpenedObject(
                        child,
                        directoryIdentity.VolumeSerialNumber,
                        expected,
                        requireDirectory: false);
                    WindowsWriteAuthorityRegistry.WindowsObjectIdentity pinnedIdentity =
                        ReadIdentity(child);
                    using FileStream stream = new(
                        Duplicate(child),
                        FileAccess.Read,
                        bufferSize: 64 * 1024,
                        isAsync: false);
                    long byteLength = stream.Length;
                    string sha256 = Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(stream))
                        .ToLowerInvariant();
                    entries.Add(
                        new TreeEntryIdentity(
                            relative,
                            pinnedIdentity.VolumeSerialNumber,
                            pinnedIdentity.FileId,
                            pinnedIdentity.NumberOfLinks,
                            IsDirectory: false,
                            byteLength,
                            sha256));
                }
            }
            finally
            {
                child.Dispose();
            }
        }
    }

    private static (SafeFileHandle parent, string leaf, string expected) OpenParent(
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string relativePath,
        bool createParents)
    {
        string[] components = ValidateRelativePath(relativePath, allowEmpty: false);
        string leaf = components[^1];
        string parentRelative = string.Join(
            Path.DirectorySeparatorChar,
            components[..^1]);
        SafeFileHandle parent = OpenOrCreateDirectory(
            capability.Handle,
            capability.Identity,
            parentRelative,
            createParents);
        string expected = Path.Combine(capability.Identity.FinalPath, relativePath);
        return (parent, leaf, expected);
    }

    private static SafeFileHandle? TryOpenReplacementTarget(
        SafeFileHandle parent,
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string leaf,
        string expected)
    {
        try
        {
            SafeFileHandle existing = OpenRelative(
                parent,
                leaf,
                FileReadAttributes | Synchronize,
                FileShareRead | FileShareWrite | FileShareDelete,
                FileOpen,
                FileNonDirectoryFile
                    | FileSynchronousIoNonAlert
                    | FileOpenReparsePoint,
                securityDescriptor: null);
            try
            {
                ValidateOpenedObject(
                    existing,
                    capability.Identity.VolumeSerialNumber,
                    expected,
                    requireDirectory: false);
                return existing;
            }
            catch
            {
                existing.Dispose();
                throw;
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static SafeFileHandle? TryOpenFile(
        SafeFileHandle parent,
        string leaf,
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability,
        string expected)
    {
        try
        {
            SafeFileHandle file = OpenRelative(
                parent,
                leaf,
                GenericRead | FileReadAttributes | Synchronize,
                FileShareRead,
                FileOpen,
                FileNonDirectoryFile
                    | FileSynchronousIoNonAlert
                    | FileOpenReparsePoint,
                securityDescriptor: null);
            try
            {
                ValidateOpenedObject(
                    file,
                    capability.Identity.VolumeSerialNumber,
                    expected,
                    requireDirectory: false);
                return file;
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static SafeFileHandle OpenRelative(
        SafeFileHandle root,
        string leaf,
        uint desiredAccess,
        uint shareAccess,
        uint createDisposition,
        uint createOptions,
        byte[]? securityDescriptor)
    {
        ValidateComponent(leaf);
        using RelativeName nativeName = new(leaf);
        GCHandle securityPin = default;
        try
        {
            nint securityPointer = 0;
            if (securityDescriptor is not null)
            {
                securityPin = GCHandle.Alloc(securityDescriptor, GCHandleType.Pinned);
                securityPointer = securityPin.AddrOfPinnedObject();
            }

            OBJECT_ATTRIBUTES attributes = new()
            {
                Length = Marshal.SizeOf<OBJECT_ATTRIBUTES>(),
                RootDirectory = root.DangerousGetHandle(),
                ObjectName = nativeName.UnicodeString,
                Attributes = ObjCaseInsensitive,
                SecurityDescriptor = securityPointer,
            };
            int status = NtCreateFile(
                out SafeFileHandle handle,
                desiredAccess,
                ref attributes,
                out _,
                0,
                FileAttributeNormal,
                shareAccess,
                createDisposition,
                createOptions,
                0,
                0);
            if (status < 0)
            {
                handle?.Dispose();
                int error = checked((int)RtlNtStatusToDosError(status));
                Exception exception = error switch
                {
                    ErrorFileNotFound => new FileNotFoundException(
                        "The handle-relative object does not exist.",
                        leaf),
                    ErrorPathNotFound => new DirectoryNotFoundException(
                        "A handle-relative parent directory does not exist."),
                    ErrorFileExists or ErrorAlreadyExists => new IOException(
                        "The handle-relative object already exists.",
                        HResultFromWin32(error)),
                    _ => new Win32Exception(
                        error,
                        "The handle-relative Windows object operation failed."),
                };
                throw exception;
            }

            return handle;
        }
        finally
        {
            if (securityPin.IsAllocated)
            {
                securityPin.Free();
            }
        }
    }

    private static void ValidateOpenedObject(
        SafeFileHandle handle,
        ulong expectedVolume,
        string expectedFinalPath,
        bool requireDirectory)
    {
        WindowsWriteAuthorityRegistry.WindowsObjectIdentity identity = ReadIdentity(handle);
        uint attributes = GetAttributes(handle);
        if ((attributes & FileAttributeReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "Reparse-point product objects are not authorized.");
        }

        if (identity.IsDirectory != requireDirectory)
        {
            throw new InvalidOperationException(
                "The opened product object has the wrong type.");
        }

        if (!identity.IsDirectory && identity.NumberOfLinks != 1)
        {
            throw new InvalidOperationException(
                "Hard-linked product files are not authorized.");
        }

        if (identity.VolumeSerialNumber != expectedVolume
            || !string.Equals(
                identity.FinalPath,
                Path.GetFullPath(expectedFinalPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The opened product object escaped its retained authority.");
        }
    }

    private static uint GetAttributes(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION information))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The opened Windows object attributes could not be read.");
        }

        return information.FileAttributes;
    }

    private static void RenameByHandle(
        SafeFileHandle source,
        SafeFileHandle destinationParent,
        string destinationLeaf,
        bool replace)
    {
        ValidateComponent(destinationLeaf);
        byte[] name = Encoding.Unicode.GetBytes(destinationLeaf);
        int rootOffset = nint.Size == 8 ? 8 : 4;
        int lengthOffset = checked(rootOffset + nint.Size);
        int nameOffset = checked(lengthOffset + sizeof(uint));
        int structureSize = nint.Size == 8 ? 24 : 16;
        int bufferSize = checked(structureSize + name.Length);
        nint buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            Span<byte> zeros = new byte[bufferSize];
            Marshal.Copy(zeros.ToArray(), 0, buffer, zeros.Length);
            Marshal.WriteByte(buffer, replace ? (byte)1 : (byte)0);
            Marshal.WriteIntPtr(
                buffer,
                rootOffset,
                destinationParent.DangerousGetHandle());
            Marshal.WriteInt32(buffer, lengthOffset, name.Length);
            Marshal.Copy(name, 0, buffer + nameOffset, name.Length);
            int status = NtSetInformationFile(
                source,
                out _,
                buffer,
                checked((uint)bufferSize),
                FileRenameInformation);
            if (status < 0)
            {
                int error = checked((int)RtlNtStatusToDosError(status));
                throw new Win32Exception(
                    error,
                    $"The handle-relative product object could not be renamed ({error}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void MarkDelete(SafeFileHandle handle)
    {
        FILE_DISPOSITION_INFO disposition = new() { DeleteFile = true };
        if (!SetFileInformationByHandle(
                handle,
                FileDispositionInfo,
                ref disposition,
                checked((uint)Marshal.SizeOf<FILE_DISPOSITION_INFO>())))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The handle-relative product object could not be deleted.");
        }
    }

    internal static SafeFileHandle Duplicate(SafeFileHandle source)
    {
        nint process = GetCurrentProcess();
        if (!DuplicateHandle(
                process,
                source.DangerousGetHandle(),
                process,
                out SafeFileHandle duplicate,
                0,
                inheritHandle: false,
                DuplicateSameAccess))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The retained directory capability could not be duplicated.");
        }

        return duplicate;
    }

    internal sealed class AdmissionSource : IDisposable
    {
        private readonly SafeFileHandle handle;
        private readonly ulong expectedVolume;
        private readonly string expectedPath;
        private bool deleted;

        internal AdmissionSource(
            SafeFileHandle handle,
            ulong expectedVolume,
            string expectedPath)
        {
            this.handle = handle;
            this.expectedVolume = expectedVolume;
            this.expectedPath = expectedPath;
        }

        public AdmissionCopyResult CopyToAndHash(
            FileStream destination,
            long maximumBytes)
        {
            ArgumentNullException.ThrowIfNull(destination);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);

            Verify();
            using FileStream source = OpenStream();
            using System.Security.Cryptography.IncrementalHash hash =
                System.Security.Cryptography.IncrementalHash.CreateHash(
                    System.Security.Cryptography.HashAlgorithmName.SHA256);
            byte[] buffer = new byte[64 * 1024];
            long total = 0;
            while (true)
            {
                int read = source.Read(buffer);
                if (read == 0)
                {
                    break;
                }

                total = checked(total + read);
                if (total > maximumBytes)
                {
                    throw new InvalidOperationException(
                        "The staged output exceeds its declared bound.");
                }

                hash.AppendData(buffer, 0, read);
                destination.Write(buffer, 0, read);
            }

            Verify();
            return new AdmissionCopyResult(
                total,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }

        public void Delete()
        {
            Verify();
            MarkDelete(handle);
            deleted = true;
        }

        public void Dispose() => handle.Dispose();

        private FileStream OpenStream()
        {
            FileStream stream =
                new(Duplicate(handle), FileAccess.Read, bufferSize: 4096, isAsync: false)
                {
                    Position = 0,
                };
            return stream;
        }

        private void Verify()
        {
            if (deleted)
            {
                throw new InvalidOperationException(
                    "The staged admission source has already been consumed.");
            }

            ValidateOpenedObject(
                handle,
                expectedVolume,
                expectedPath,
                requireDirectory: false);
        }
    }

    internal sealed record AdmissionCopyResult(long ByteLength, string Sha256);

    internal sealed record TreeEntryIdentity(
        string RelativePath,
        ulong VolumeSerialNumber,
        ulong FileId,
        uint NumberOfLinks,
        bool IsDirectory,
        long ByteLength,
        string? Sha256);

    private static string[] ValidateRelativePath(string value, bool allowEmpty)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length == 0)
        {
            return allowEmpty
                ? []
                : throw new ArgumentException(
                    "A product-relative object path is required.",
                    nameof(value));
        }

        if (Path.IsPathFullyQualified(value)
            || value.Contains('\0', StringComparison.Ordinal)
            || value.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Only a typed product-relative object path is allowed.",
                nameof(value));
        }

        string[] components = value.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.None);
        if (components.Length is 0 or > MaximumComponents)
        {
            throw new ArgumentException(
                "The product-relative path has an invalid component count.",
                nameof(value));
        }

        foreach (string component in components)
        {
            ValidateComponent(component);
        }

        return components;
    }

    private static void ValidateComponent(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string deviceStem = value.Split('.')[0];
        if (value.Length > 255
            || value is "." or ".."
            || value.StartsWith(' ')
            || value.EndsWith(' ')
            || value.EndsWith('.')
            || deviceStem.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || deviceStem.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || deviceStem.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || deviceStem.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || (deviceStem.Length == 4
                && (deviceStem.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || deviceStem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && deviceStem[3] is >= '1' and <= '9')
            || value.IndexOfAny(['\0', ':', '*', '?', '"', '<', '>', '|', '\\', '/']) >= 0)
        {
            throw new ArgumentException(
                "A product object name contains unsupported Windows syntax.",
                nameof(value));
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
                    "The final Windows object path could not be resolved.");
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

    private static int HResultFromWin32(int error) =>
        unchecked((int)(0x80070000u | (uint)error));

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

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint DeleteAccess = 0x00010000;
    private const uint ReadControl = 0x00020000;
    private const uint Synchronize = 0x00100000;
    private const uint FileListDirectory = 0x00000001;
    private const uint FileAddFile = 0x00000002;
    private const uint FileAddSubdirectory = 0x00000004;
    private const uint FileReadAttributes = 0x00000080;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint FileAttributeDirectory = 0x00000010;
    private const uint FileAttributeReparsePoint = 0x00000400;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint FileOpen = 1;
    private const uint FileCreate = 2;
    private const uint FileOpenIf = 3;
    private const uint FileDirectoryFile = 0x00000001;
    private const uint FileSynchronousIoNonAlert = 0x00000020;
    private const uint FileNonDirectoryFile = 0x00000040;
    private const uint FileOpenReparsePoint = 0x00200000;
    private const uint FileBackupIntent = 0x00004000;
    private const uint ObjCaseInsensitive = 0x00000040;
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorFileExists = 80;
    private const int ErrorAlreadyExists = 183;
    private const int FileRenameInformation = 10;
    private const int FileDispositionInfo = 4;
    private const uint FileNameNormalized = 0;
    private const uint VolumeNameDos = 0;
    private const uint DuplicateSameAccess = 0x00000002;

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

    [DllImport("ntdll.dll")]
    private static extern int NtSetInformationFile(
        SafeFileHandle fileHandle,
        out IO_STATUS_BLOCK ioStatusBlock,
        nint fileInformation,
        uint length,
        int fileInformationClass);

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

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateHandle(
        nint sourceProcess,
        nint sourceHandle,
        nint targetProcess,
        out SafeFileHandle targetHandle,
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint options);
}
