using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Infinium.Persistence;

public sealed class WindowsWriteAuthorityRegistry : IDisposable
{
    private readonly Lock gate = new();
    private readonly IReadOnlyList<ProtectedRootEntry> protectedRoots;
    private bool disposed;

    public WindowsWriteAuthorityRegistry(IEnumerable<string> protectedRootPaths)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Opened-object write authority currently requires Windows.");
        }

        ArgumentNullException.ThrowIfNull(protectedRootPaths);
        List<ProtectedRootEntry> entries = [];
        try
        {
            foreach (string path in protectedRootPaths)
            {
                string normalized = NormalizeLocalAbsolutePath(path, nameof(protectedRootPaths));
                RejectExistingReparseAncestors(normalized);
                if (!Directory.Exists(normalized))
                {
                    throw new DirectoryNotFoundException(
                        $"Protected root '{normalized}' does not exist.");
                }

                SafeFileHandle handle = OpenPinnedDirectory(normalized);
                WindowsObjectIdentity identity;
                try
                {
                    identity = ReadIdentity(handle);
                }
                catch
                {
                    handle.Dispose();
                    throw;
                }

                if (!identity.IsDirectory)
                {
                    handle.Dispose();
                    throw new InvalidOperationException(
                        "A protected root must be an opened directory object.");
                }

                if (entries.Any(entry => entry.Identity.SameObject(identity)))
                {
                    handle.Dispose();
                    continue;
                }

                entries.Add(new ProtectedRootEntry(normalized, handle, identity));
            }

            protectedRoots = entries.AsReadOnly();
        }
        catch
        {
            foreach (ProtectedRootEntry entry in entries)
            {
                entry.Handle.Dispose();
            }

            throw;
        }
    }

    public IReadOnlyList<string> ProtectedRootIdentities
    {
        get
        {
            lock (gate)
            {
                ThrowIfDisposed();
                return protectedRoots
                    .Select(entry => entry.Identity.StableIdentity)
                    .ToArray();
            }
        }
    }

    internal ProductRootCapability CaptureProductRoot(string productRoot)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            EnsureProtectedRootsCurrent();
            string normalized = NormalizeLocalAbsolutePath(productRoot, nameof(productRoot));
            RejectExistingReparseAncestors(normalized);
            string? requestedParent = Path.GetDirectoryName(normalized);
            if (string.IsNullOrWhiteSpace(requestedParent))
            {
                throw new InvalidOperationException(
                    "A volume root cannot be used as the product root.");
            }

            string anchorPath = FindNearestExistingDirectory(requestedParent);
            SafeFileHandle? anchorHandle = null;
            SafeFileHandle? rootHandle = null;
            try
            {
                anchorHandle = OpenPinnedDirectory(anchorPath);
                WindowsObjectIdentity anchor = ReadIdentity(anchorHandle);
                if (!anchor.IsDirectory)
                {
                    throw new InvalidOperationException(
                        "The product-root authority anchor must be a directory.");
                }

                string relative = Path.GetRelativePath(anchorPath, normalized);
                if (relative == ".")
                {
                    relative = string.Empty;
                }

                string expectedFinalPath = CombineFinalPath(anchor.FinalPath, relative);
                RejectProtectedOverlap(expectedFinalPath, anchor.VolumeSerialNumber);
                WindowsObjectIdentity? rootIdentity = null;
                if (Directory.Exists(normalized))
                {
                    rootHandle = OpenPinnedDirectory(normalized);
                    rootIdentity = ReadIdentity(rootHandle);
                    ValidateBoundRoot(normalized, expectedFinalPath, anchor, rootIdentity);
                    RejectProtectedOverlap(
                        rootIdentity.FinalPath,
                        rootIdentity.VolumeSerialNumber);
                }

                string authorityIdentity = FormattableString.Invariant(
                    $"winvol:{anchor.VolumeSerialNumber:x16}:path:{expectedFinalPath.ToUpperInvariant()}");
                ProductRootCapability capability = new(
                    normalized,
                    anchorPath,
                    anchorHandle,
                    anchor,
                    expectedFinalPath,
                    rootHandle,
                    rootIdentity,
                    authorityIdentity);
                anchorHandle = null;
                rootHandle = null;
                return capability;
            }
            finally
            {
                rootHandle?.Dispose();
                anchorHandle?.Dispose();
            }
        }
    }

    internal void BindCreatedProductRoot(ProductRootCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        lock (gate)
        {
            ThrowIfDisposed();
            EnsureProtectedRootsCurrent();
            EnsureAnchorCurrent(capability);
            RejectExistingReparseAncestors(capability.ProductRoot);
            if (!Directory.Exists(capability.ProductRoot))
            {
                throw new InvalidOperationException(
                    "The product root was not created at its authorized location.");
            }

            SafeFileHandle rootHandle = OpenPinnedDirectory(capability.ProductRoot);
            try
            {
                WindowsObjectIdentity root = ReadIdentity(rootHandle);
                ValidateBoundRoot(
                    capability.ProductRoot,
                    capability.ExpectedFinalPath,
                    capability.AnchorIdentity,
                    root);
                RejectProtectedOverlap(root.FinalPath, root.VolumeSerialNumber);
                capability.BindRoot(rootHandle, root);
                rootHandle = null!;
            }
            finally
            {
                rootHandle?.Dispose();
            }
        }
    }

    internal string AuthorizeProductPath(
        ProductRootCapability capability,
        ProductWriteClass writeClass,
        string candidatePath)
    {
        ArgumentNullException.ThrowIfNull(capability);
        lock (gate)
        {
            ThrowIfDisposed();
            EnsureProtectedRootsCurrent();
            EnsureAnchorCurrent(capability);
            WindowsObjectIdentity root = EnsureRootCurrent(capability);
            string candidate = NormalizeLocalAbsolutePath(candidatePath, nameof(candidatePath));
            ClassDirectoryCapability classDirectory =
                EnsureClassDirectoryCurrent(capability, writeClass);
            if (!IsEqualOrDescendant(candidate, classDirectory.ConfiguredPath))
            {
                throw new InvalidOperationException(
                    "The requested write is outside its authorized product write class.");
            }

            RejectExistingReparseAncestors(candidate);
            string existingPath = FindNearestExistingObject(candidate);
            using SafeFileHandle existingHandle = OpenExisting(existingPath);
            WindowsObjectIdentity existing = ReadIdentity(existingHandle);
            if (existing.VolumeSerialNumber != root.VolumeSerialNumber)
            {
                throw new InvalidOperationException(
                    "Cross-volume write authority is rejected.");
            }

            if (!existing.IsDirectory && existing.NumberOfLinks > 1)
            {
                throw new InvalidOperationException(
                    "Unexpected hard-linked write targets are rejected.");
            }

            if (!IsEqualOrDescendant(existing.FinalPath, classDirectory.Identity.FinalPath))
            {
                throw new InvalidOperationException(
                    "The final opened write object escapes its product write class.");
            }

            RejectProtectedWrite(existing);
            return candidate;
        }
    }

    internal void BindClassDirectory(
        ProductRootCapability capability,
        ProductWriteClass writeClass,
        string configuredPath)
    {
        ArgumentNullException.ThrowIfNull(capability);
        lock (gate)
        {
            ThrowIfDisposed();
            EnsureProtectedRootsCurrent();
            _ = EnsureRootCurrent(capability);
            string normalized = NormalizeLocalAbsolutePath(configuredPath, nameof(configuredPath));
            string expectedPath = GetExpectedClassPath(capability.ProductRoot, writeClass);
            if (!string.Equals(normalized, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The class directory does not match the closed product write-class mapping.");
            }

            RejectExistingReparseAncestors(normalized);
            if (!Directory.Exists(normalized))
            {
                throw new InvalidOperationException(
                    "The product write-class directory does not exist.");
            }

            SafeFileHandle handle = OpenPinnedDirectory(normalized);
            try
            {
                WindowsObjectIdentity identity = ReadIdentity(handle);
                WindowsObjectIdentity root = capability.RootIdentity
                    ?? throw new InvalidOperationException(
                        "The product-root capability is not bound.");
                if (!identity.IsDirectory
                    || identity.VolumeSerialNumber != root.VolumeSerialNumber
                    || !IsEqualOrDescendant(identity.FinalPath, root.FinalPath))
                {
                    throw new InvalidOperationException(
                        "The product write-class directory escaped the product root.");
                }

                RejectProtectedWrite(identity);
                capability.BindClass(
                    writeClass,
                    new ClassDirectoryCapability(writeClass, normalized, handle, identity));
                handle = null!;
            }
            finally
            {
                handle?.Dispose();
            }
        }
    }

    internal void VerifyBoundProductRoot(ProductRootCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        lock (gate)
        {
            ThrowIfDisposed();
            EnsureProtectedRootsCurrent();
            EnsureAnchorCurrent(capability);
            _ = EnsureRootCurrent(capability);
        }
    }

    internal void AuthorizeDelete(
        ProductRootCapability capability,
        string candidatePath,
        bool recursive)
    {
        if (recursive)
        {
            throw new InvalidOperationException(
                "Generic recursive deletion requests are not authorized.");
        }

        ProductWriteClass writeClass = capability.GetClassForPath(candidatePath);
        _ = AuthorizeProductPath(capability, writeClass, candidatePath);
    }

    internal ClassDirectoryCapability GetCurrentClassCapability(
        ProductRootCapability capability,
        ProductWriteClass writeClass)
    {
        ArgumentNullException.ThrowIfNull(capability);
        lock (gate)
        {
            ThrowIfDisposed();
            EnsureProtectedRootsCurrent();
            EnsureAnchorCurrent(capability);
            _ = EnsureRootCurrent(capability);
            return EnsureClassDirectoryCurrent(capability, writeClass);
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

            foreach (ProtectedRootEntry entry in protectedRoots)
            {
                entry.Handle.Dispose();
            }

            disposed = true;
        }
    }

    private void EnsureProtectedRootsCurrent()
    {
        foreach (ProtectedRootEntry entry in protectedRoots)
        {
            RejectExistingReparseAncestors(entry.ConfiguredPath);
            if (!Directory.Exists(entry.ConfiguredPath))
            {
                throw new InvalidOperationException(
                    "A protected-root capability became stale.");
            }

            using SafeFileHandle currentHandle = OpenExisting(entry.ConfiguredPath);
            WindowsObjectIdentity current = ReadIdentity(currentHandle);
            if (!entry.Identity.SameObject(current)
                || !entry.Identity.SameFinalPath(current))
            {
                throw new InvalidOperationException(
                    "A protected-root capability became stale or was replaced.");
            }
        }
    }

    private static void EnsureAnchorCurrent(ProductRootCapability capability)
    {
        RejectExistingReparseAncestors(capability.AnchorPath);
        if (!Directory.Exists(capability.AnchorPath))
        {
            throw new InvalidOperationException(
                "The product-root authority anchor became stale.");
        }

        using SafeFileHandle currentHandle = OpenExisting(capability.AnchorPath);
        WindowsObjectIdentity current = ReadIdentity(currentHandle);
        if (!capability.AnchorIdentity.SameObject(current)
            || !capability.AnchorIdentity.SameFinalPath(current))
        {
            throw new InvalidOperationException(
                "The product-root authority anchor was replaced.");
        }
    }

    private static ClassDirectoryCapability EnsureClassDirectoryCurrent(
        ProductRootCapability capability,
        ProductWriteClass writeClass)
    {
        ClassDirectoryCapability expected = capability.GetClass(writeClass);
        RejectExistingReparseAncestors(expected.ConfiguredPath);
        if (!Directory.Exists(expected.ConfiguredPath))
        {
            throw new InvalidOperationException(
                "A product write-class capability became stale.");
        }

        using SafeFileHandle currentHandle = OpenExisting(expected.ConfiguredPath);
        WindowsObjectIdentity current = ReadIdentity(currentHandle);
        if (!expected.Identity.SameObject(current)
            || !expected.Identity.SameFinalPath(current))
        {
            throw new InvalidOperationException(
                "A product write-class capability became stale or was replaced.");
        }

        return expected;
    }

    private static WindowsObjectIdentity EnsureRootCurrent(
        ProductRootCapability capability)
    {
        WindowsObjectIdentity expected = capability.RootIdentity
            ?? throw new InvalidOperationException(
                "The product-root capability has not been bound to an opened object.");
        RejectExistingReparseAncestors(capability.ProductRoot);
        if (!Directory.Exists(capability.ProductRoot))
        {
            throw new InvalidOperationException(
                "The product-root capability became stale.");
        }

        using SafeFileHandle currentHandle = OpenExisting(capability.ProductRoot);
        WindowsObjectIdentity current = ReadIdentity(currentHandle);
        if (!expected.SameObject(current) || !expected.SameFinalPath(current))
        {
            throw new InvalidOperationException(
                "The product-root capability became stale or was replaced.");
        }

        return current;
    }

    private void RejectProtectedOverlap(string finalPath, ulong volumeSerialNumber)
    {
        foreach (ProtectedRootEntry entry in protectedRoots)
        {
            if (entry.Identity.VolumeSerialNumber == volumeSerialNumber
                && (IsEqualOrDescendant(finalPath, entry.Identity.FinalPath)
                    || IsEqualOrDescendant(entry.Identity.FinalPath, finalPath)))
            {
                throw new InvalidOperationException(
                    "Product-root authority overlaps a registered protected root.");
            }
        }
    }

    private void RejectProtectedWrite(WindowsObjectIdentity candidate)
    {
        foreach (ProtectedRootEntry entry in protectedRoots)
        {
            if (entry.Identity.VolumeSerialNumber == candidate.VolumeSerialNumber
                && (entry.Identity.SameObject(candidate)
                    || IsEqualOrDescendant(
                        candidate.FinalPath,
                        entry.Identity.FinalPath)))
            {
                throw new InvalidOperationException(
                    "The final opened write object enters a protected root.");
            }
        }
    }

    private static void ValidateBoundRoot(
        string configuredPath,
        string expectedFinalPath,
        WindowsObjectIdentity anchor,
        WindowsObjectIdentity root)
    {
        if (!root.IsDirectory
            || root.VolumeSerialNumber != anchor.VolumeSerialNumber
            || !string.Equals(
                Path.TrimEndingDirectorySeparator(root.FinalPath),
                Path.TrimEndingDirectorySeparator(expectedFinalPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Product root '{configuredPath}' did not resolve to its authorized Windows object.");
        }
    }

    private static string FindNearestExistingDirectory(string path)
    {
        string? current = path;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current))
            {
                return current;
            }

            if (File.Exists(current))
            {
                throw new InvalidOperationException(
                    "A product-root ancestor is not a directory.");
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        throw new InvalidOperationException(
            "No existing local directory anchors the requested product root.");
    }

    private static string FindNearestExistingObject(string path)
    {
        string? current = path;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current) || File.Exists(current))
            {
                return current;
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }

        throw new InvalidOperationException(
            "No existing object anchors the requested write.");
    }

    private static void RejectExistingReparseAncestors(string path)
    {
        string? current = path;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if ((Directory.Exists(current) || File.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "Reparse-point or mount-point write paths are not authorized.");
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }
    }

    private static string NormalizeLocalAbsolutePath(string path, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path, parameterName);
        if (!Path.IsPathFullyQualified(path)
            || path.StartsWith(@"\\?\", StringComparison.Ordinal)
            || path.StartsWith(@"\\.\", StringComparison.Ordinal)
            || path.StartsWith(@"\??\", StringComparison.Ordinal)
            || path.StartsWith(@"\\", StringComparison.Ordinal)
            || path.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Only ordinary fully-qualified local DOS paths are authorized.",
                parameterName);
        }

        string fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        int colon = fullPath.IndexOf(':');
        if (colon != 1 || fullPath.IndexOf(':', colon + 1) >= 0)
        {
            throw new ArgumentException(
                "Device and alternate-data-stream path syntax is rejected.",
                parameterName);
        }

        return fullPath;
    }

    private static string CombineFinalPath(string anchorFinalPath, string relative)
    {
        if (string.IsNullOrEmpty(relative))
        {
            return Path.TrimEndingDirectorySeparator(anchorFinalPath);
        }

        return Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.Combine(anchorFinalPath, relative)));
    }

    private static bool IsEqualOrDescendant(string candidate, string root)
    {
        string normalizedCandidate = Path.TrimEndingDirectorySeparator(candidate);
        string normalizedRoot = Path.TrimEndingDirectorySeparator(root);
        return string.Equals(
                normalizedCandidate,
                normalizedRoot,
                StringComparison.OrdinalIgnoreCase)
            || normalizedCandidate.StartsWith(
                normalizedRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
    }

    private static SafeFileHandle OpenExisting(string path)
    {
        SafeFileHandle handle = CreateFileW(
            path,
            FILE_READ_ATTRIBUTES,
            FileShare.ReadWrite | FileShare.Delete,
            0,
            FileMode.Open,
            FILE_FLAG_BACKUP_SEMANTICS,
            0);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error, "The Windows object identity could not be opened.");
        }

        return handle;
    }

    private static SafeFileHandle OpenPinnedDirectory(string path)
    {
        SafeFileHandle handle = CreateFileW(
            path,
            FILE_READ_ATTRIBUTES,
            FileShare.ReadWrite,
            0,
            FileMode.Open,
            FILE_FLAG_BACKUP_SEMANTICS,
            0);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(
                error,
                "The Windows directory capability could not be pinned.");
        }

        return handle;
    }

    private static string GetExpectedClassPath(
        string productRoot,
        ProductWriteClass writeClass) =>
        Path.Combine(
            productRoot,
            writeClass switch
            {
                ProductWriteClass.Data => "data",
                ProductWriteClass.Payload => "payloads",
                ProductWriteClass.AttemptStaging => "staging",
                ProductWriteClass.Backup => "backups",
                ProductWriteClass.Runtime => "runtime",
                ProductWriteClass.RunOutput => "run-output",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(writeClass),
                    writeClass,
                    "Unknown product write class."),
            });

    private static WindowsObjectIdentity ReadIdentity(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION information))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The Windows volume/file identity could not be read.");
        }

        string finalPath = ReadFinalPath(handle);
        ulong fileId = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
        bool directory = (information.FileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
        return new WindowsObjectIdentity(
            information.VolumeSerialNumber,
            fileId,
            Path.TrimEndingDirectorySeparator(finalPath),
            information.NumberOfLinks,
            directory);
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
                FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
            if (length == 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The final Windows object path could not be resolved.");
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    internal sealed class ProductRootCapability(
        string productRoot,
        string anchorPath,
        SafeFileHandle anchorHandle,
        WindowsObjectIdentity anchorIdentity,
        string expectedFinalPath,
        SafeFileHandle? rootHandle,
        WindowsObjectIdentity? rootIdentity,
        string authorityIdentity) : IDisposable
    {
        private readonly Dictionary<ProductWriteClass, ClassDirectoryCapability> classes = [];
        private bool disposed;

        public string ProductRoot { get; } = productRoot;
        public string AnchorPath { get; } = anchorPath;
        public SafeFileHandle AnchorHandle { get; } = anchorHandle;
        public WindowsObjectIdentity AnchorIdentity { get; } = anchorIdentity;
        public string ExpectedFinalPath { get; } = expectedFinalPath;
        public SafeFileHandle? RootHandle { get; private set; } = rootHandle;
        public WindowsObjectIdentity? RootIdentity { get; private set; } = rootIdentity;
        public string AuthorityIdentity { get; } = authorityIdentity;

        public void BindRoot(SafeFileHandle handle, WindowsObjectIdentity identity)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (RootIdentity is not null && !RootIdentity.SameObject(identity))
            {
                throw new InvalidOperationException(
                    "A bound product-root capability cannot be rebound.");
            }

            if (RootHandle is not null)
            {
                handle.Dispose();
                return;
            }

            RootHandle = handle;
            RootIdentity = identity;
        }

        public void BindClass(
            ProductWriteClass writeClass,
            ClassDirectoryCapability capability)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (classes.TryGetValue(writeClass, out ClassDirectoryCapability? existing))
            {
                capability.Dispose();
                if (!existing.Identity.SameObject(capability.Identity))
                {
                    throw new InvalidOperationException(
                        "A product write-class capability cannot be rebound.");
                }

                return;
            }

            classes.Add(writeClass, capability);
        }

        public ClassDirectoryCapability GetClass(ProductWriteClass writeClass)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return classes.TryGetValue(writeClass, out ClassDirectoryCapability? capability)
                ? capability
                : throw new InvalidOperationException(
                    "The product write-class capability has not been bound.");
        }

        public ProductWriteClass GetClassForPath(string path)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ClassDirectoryCapability? match = classes.Values.SingleOrDefault(
                item => IsEqualOrDescendant(path, item.ConfiguredPath));
            return match?.WriteClass
                ?? throw new InvalidOperationException(
                    "The delete target is not in a classified product write directory.");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            foreach (ClassDirectoryCapability capability in classes.Values)
            {
                capability.Dispose();
            }

            RootHandle?.Dispose();
            AnchorHandle.Dispose();
            disposed = true;
        }
    }

    internal sealed class ClassDirectoryCapability(
        ProductWriteClass writeClass,
        string configuredPath,
        SafeFileHandle handle,
        WindowsObjectIdentity identity) : IDisposable
    {
        public ProductWriteClass WriteClass { get; } = writeClass;
        public string ConfiguredPath { get; } = configuredPath;
        public SafeFileHandle Handle { get; } = handle;
        public WindowsObjectIdentity Identity { get; } = identity;

        public void Dispose() => Handle.Dispose();
    }

    internal sealed record WindowsObjectIdentity(
        ulong VolumeSerialNumber,
        ulong FileId,
        string FinalPath,
        uint NumberOfLinks,
        bool IsDirectory)
    {
        public string StableIdentity =>
            FormattableString.Invariant($"winobj:{VolumeSerialNumber:x16}:{FileId:x16}");

        public bool SameObject(WindowsObjectIdentity other) =>
            VolumeSerialNumber == other.VolumeSerialNumber && FileId == other.FileId;

        public bool SameFinalPath(WindowsObjectIdentity other) =>
            string.Equals(FinalPath, other.FinalPath, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ProtectedRootEntry(
        string ConfiguredPath,
        SafeFileHandle Handle,
        WindowsObjectIdentity Identity);

    private const uint FILE_READ_ATTRIBUTES = 0x00000080;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    private const uint FILE_NAME_NORMALIZED = 0x0;
    private const uint VOLUME_NAME_DOS = 0x0;

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
        out BY_HANDLE_FILE_INFORMATION information);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle file,
        [Out] char[] filePath,
        uint filePathLength,
        uint flags);
}
