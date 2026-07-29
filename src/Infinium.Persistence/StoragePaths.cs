using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

#pragma warning disable IDE0008 // Path normalization locals are self-describing.

namespace Infinium.Persistence;

public sealed class StoragePaths : IDisposable
{
    private static readonly Lazy<WindowsWriteAuthorityRegistry> DefaultWriteAuthority =
        new(() => new WindowsWriteAuthorityRegistry([]));
    private static readonly string[] ProtectedLeafNames =
    [
        "Skyrim Special Edition",
        "ModOrganizer",
        "Mod Organizer 2",
    ];

    private readonly WindowsWriteAuthorityRegistry? writeAuthority;
    private readonly WindowsWriteAuthorityRegistry.ProductRootCapability? productCapability;
    private bool disposed;

    public StoragePaths(string productRoot)
        : this(
            productRoot,
            OperatingSystem.IsWindows() ? DefaultWriteAuthority.Value : null)
    {
    }

    public StoragePaths(
        string productRoot,
        WindowsWriteAuthorityRegistry? writeAuthority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productRoot);
        if (!Path.IsPathFullyQualified(productRoot))
        {
            throw new ArgumentException("The product root must be an absolute path.", nameof(productRoot));
        }

        ProductRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(productRoot));
        RejectProtectedRoot(ProductRoot);
        RejectExistingRootReparse(ProductRoot);
        if (OperatingSystem.IsWindows() && writeAuthority is null)
        {
            throw new ArgumentNullException(
                nameof(writeAuthority),
                "Windows product storage requires opened-object write authority.");
        }

        this.writeAuthority = writeAuthority;
        productCapability = writeAuthority?.CaptureProductRoot(ProductRoot);
        AuthorityIdentity = productCapability?.AuthorityIdentity
            ?? GetAuthorityIdentity(ProductRoot);
        Data = Path.Combine(ProductRoot, "data");
        Payloads = Path.Combine(ProductRoot, "payloads");
        Staging = Path.Combine(ProductRoot, "staging");
        Backups = Path.Combine(ProductRoot, "backups");
        Runtime = Path.Combine(ProductRoot, "runtime");
        RunOutput = Path.Combine(ProductRoot, "run-output");
        Database = Path.Combine(Data, "infinium.sqlite3");
    }

    public string ProductRoot { get; }
    public string AuthorityIdentity { get; }
    public string Data { get; }
    public string Payloads { get; }
    public string Staging { get; }
    public string Backups { get; }
    public string Runtime { get; }
    public string RunOutput { get; }
    public string Database { get; }
    internal bool HasBoundProductRoot => productCapability?.RootIdentity is not null;

    public void Create()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string? parent = Path.GetDirectoryName(ProductRoot);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
        {
            throw new InvalidOperationException(
                "The product-root parent must already exist and be selected explicitly.");
        }

        if (OperatingSystem.IsWindows() && writeAuthority is not null)
        {
            byte[] descriptor = BuildPrivateDirectorySecurity()
                .GetSecurityDescriptorBinaryForm();
            writeAuthority.CreateOrBindProductRoot(productCapability!, descriptor);
            VerifyPrivateDirectory(ProductRoot);
            writeAuthority.VerifyBoundProductRoot(productCapability!);
            foreach ((ProductWriteClass writeClass, string path) in GetClassDirectories())
            {
                writeAuthority.CreateOrBindClassDirectory(
                    productCapability!,
                    writeClass,
                    path,
                    descriptor);
                VerifyPrivateDirectory(path);
                _ = writeAuthority.AuthorizeProductPath(
                    productCapability!,
                    writeClass,
                    path);
            }

            return;
        }

        CreateOrVerifyPrivateDirectory(ProductRoot);
        foreach ((_, string path) in GetClassDirectories())
        {
            CreateOrVerifyPrivateDirectory(path);
            if (OperatingSystem.IsWindows())
            {
                VerifyPrivateDirectory(path);
            }
        }
    }

    public AttemptStagingAuthority CreateAttemptStagingDirectory(string attemptId)
    {
        ValidateOpaqueLeaf(attemptId, nameof(attemptId));
        WindowsWriteAuthorityRegistry.ClassDirectoryCapability capability =
            GetBoundWriteClassCapability(ProductWriteClass.AttemptStaging);
        SafeFileHandle handle = WindowsHandleRelativeStorage.CreateOrOpenDirectory(
            capability,
            attemptId);
        return new AttemptStagingAuthority(handle);
    }

    public void WriteCoordinatorRuntimeDescriptor(ReadOnlySpan<byte> bytes)
    {
        const int MaximumDescriptorBytes = 32 * 1024;
        if (bytes.Length is 0 or > MaximumDescriptorBytes)
        {
            throw new InvalidOperationException(
                "The runtime descriptor has an invalid serialized size.");
        }

        WriteAllBytesAtomic(
            ProductWriteClass.Runtime,
            "coordinator.v1.json",
            bytes);
    }

    internal FileStream CreateNewFile(
        ProductWriteClass writeClass,
        string relativePath) =>
        WindowsHandleRelativeStorage.CreateNewFile(
            GetBoundWriteClassCapability(writeClass),
            relativePath);

    internal FileStream OpenReadFile(
        ProductWriteClass writeClass,
        string relativePath) =>
        WindowsHandleRelativeStorage.OpenReadFile(
            GetBoundWriteClassCapability(writeClass),
            relativePath);

    internal WindowsHandleRelativeStorage.AdmissionSource OpenAdmissionSource(
        ProductWriteClass writeClass,
        string relativePath) =>
        WindowsHandleRelativeStorage.OpenAdmissionSource(
            GetBoundWriteClassCapability(writeClass),
            relativePath);

    internal WindowsHandleRelativeStorage.AdmissionCopyResult PublishAdmissionSource(
        WindowsHandleRelativeStorage.AdmissionSource source,
        string destinationRelativePath,
        string expectedSha256,
        long expectedLength,
        long maximumBytes) =>
        WindowsHandleRelativeStorage.PublishAdmissionSource(
            source,
            GetBoundWriteClassCapability(ProductWriteClass.Payload),
            destinationRelativePath,
            expectedSha256,
            expectedLength,
            maximumBytes);

    internal bool FileExists(ProductWriteClass writeClass, string relativePath) =>
        WindowsHandleRelativeStorage.FileExists(
            GetBoundWriteClassCapability(writeClass),
            relativePath);

    internal void DeleteFile(
        ProductWriteClass writeClass,
        string relativePath,
        bool missingIsSuccess = false) =>
        WindowsHandleRelativeStorage.DeleteFile(
            GetBoundWriteClassCapability(writeClass),
            relativePath,
            missingIsSuccess);

    internal void CopyFile(
        ProductWriteClass sourceClass,
        string sourceRelativePath,
        ProductWriteClass destinationClass,
        string destinationRelativePath,
        long expectedLength,
        string expectedSha256) =>
        WindowsHandleRelativeStorage.CopyFile(
            GetBoundWriteClassCapability(sourceClass),
            sourceRelativePath,
            GetBoundWriteClassCapability(destinationClass),
            destinationRelativePath,
            expectedLength,
            expectedSha256);

    internal void DeleteDirectoryTree(
        ProductWriteClass writeClass,
        string relativePath,
        bool missingIsSuccess = false) =>
        WindowsHandleRelativeStorage.DeleteDirectoryTree(
            GetBoundWriteClassCapability(writeClass),
            relativePath,
            missingIsSuccess);

    internal void CopyExternalFileIntoProduct(
        ProductWriteClass destinationClass,
        string destinationRelativePath,
        string sourcePath,
        long expectedLength,
        string expectedSha256)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedLength);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);
        using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        bool created = false;
        try
        {
            using FileStream destination =
                CreateNewFile(destinationClass, destinationRelativePath);
            created = true;
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
                        "A restore source grew beyond its validated byte length.");
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
                    "A restore source changed after backup validation.");
            }

            destination.Flush(flushToDisk: true);
        }
        catch
        {
            if (created)
            {
                DeleteFile(destinationClass, destinationRelativePath, missingIsSuccess: true);
            }

            throw;
        }
    }

    internal void WriteAllBytesAtomic(
        ProductWriteClass writeClass,
        string relativePath,
        ReadOnlySpan<byte> bytes) =>
        WindowsHandleRelativeStorage.WriteAllBytesAtomic(
            GetBoundWriteClassCapability(writeClass),
            relativePath,
            bytes);

    internal void PublishFrom(
        StoragePaths staging,
        IReadOnlyList<PublicationFileExpectation>? expectedFiles = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(staging);
        if (writeAuthority is null
            || productCapability is null
            || !ReferenceEquals(writeAuthority, staging.writeAuthority)
            || staging.productCapability is null)
        {
            throw new InvalidOperationException(
                "Restore publication requires one shared opened-object authority registry.");
        }

        writeAuthority.PublishProductRoot(
            staging.productCapability,
            productCapability,
            expectedFiles ?? []);
    }

    internal StoragePaths CreateRestoreStagingPaths()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string parent = Path.GetDirectoryName(ProductRoot)
            ?? throw new InvalidOperationException(
                "The restore target must have a parent directory.");
        string stagingRoot = Path.Combine(
            parent,
            $".{Path.GetFileName(ProductRoot)}.restore-{Guid.NewGuid():N}.tmp");
        return new StoragePaths(stagingRoot, writeAuthority);
    }

    internal void DeleteProductTree()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (writeAuthority is null || productCapability is null)
        {
            throw new InvalidOperationException(
                "Product-tree deletion requires opened-object authority.");
        }

        writeAuthority.DeleteProductRoot(productCapability);
    }

    public string ResolveProductPath(
        ProductWriteClass writeClass,
        string relativeArtifactPath)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(relativeArtifactPath);
        if (Path.IsPathFullyQualified(relativeArtifactPath)
            || relativeArtifactPath.Contains(':', StringComparison.Ordinal)
            || relativeArtifactPath.Contains('\0', StringComparison.Ordinal)
            || relativeArtifactPath.Split(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException(
                "Only normalized write-class-relative paths are authorized.");
        }

        string classRoot = GetClassDirectory(writeClass);
        string result = string.IsNullOrEmpty(relativeArtifactPath)
            ? classRoot
            : Path.GetFullPath(Path.Combine(classRoot, relativeArtifactPath));
        if (!IsEqualOrDescendant(result, classRoot))
        {
            throw new InvalidOperationException(
                "The resolved path escapes its product write class.");
        }

        RejectReparseAncestors(result);
        return writeAuthority?.AuthorizeProductPath(
                productCapability!,
                writeClass,
                result)
            ?? result;
    }

    public string ResolveProductPathForDeletion(
        ProductWriteClass writeClass,
        string relativeArtifactPath,
        bool recursive)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        string result = ResolveProductPath(writeClass, relativeArtifactPath);
        if (writeAuthority is null)
        {
            if (recursive)
            {
                throw new InvalidOperationException(
                    "Generic recursive deletion requests are not authorized.");
            }

            return result;
        }

        writeAuthority.AuthorizeDelete(productCapability!, result, recursive);
        return result;
    }

    internal WindowsWriteAuthorityRegistry.ClassDirectoryCapability
        GetBoundWriteClassCapability(ProductWriteClass writeClass)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (writeAuthority is null || productCapability is null)
        {
            throw new PlatformNotSupportedException(
                "Opened-object write authority currently requires Windows.");
        }

        return writeAuthority.GetCurrentClassCapability(productCapability, writeClass);
    }

    private static void ValidateOpaqueLeaf(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 128
            || value is "." or ".."
            || value.IndexOfAny(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ':', '\0'])
                >= 0)
        {
            throw new ArgumentException(
                "The identifier is not a valid single product object name.",
                parameterName);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        productCapability?.Dispose();
        disposed = true;
        GC.SuppressFinalize(this);
    }

    ~StoragePaths()
    {
        productCapability?.Dispose();
    }

    private IEnumerable<(ProductWriteClass WriteClass, string Path)> GetClassDirectories()
    {
        yield return (ProductWriteClass.Data, Data);
        yield return (ProductWriteClass.Payload, Payloads);
        yield return (ProductWriteClass.AttemptStaging, Staging);
        yield return (ProductWriteClass.Backup, Backups);
        yield return (ProductWriteClass.Runtime, Runtime);
        yield return (ProductWriteClass.RunOutput, RunOutput);
    }

    private string GetClassDirectory(ProductWriteClass writeClass) =>
        writeClass switch
        {
            ProductWriteClass.Data => Data,
            ProductWriteClass.Payload => Payloads,
            ProductWriteClass.AttemptStaging => Staging,
            ProductWriteClass.Backup => Backups,
            ProductWriteClass.Runtime => Runtime,
            ProductWriteClass.RunOutput => RunOutput,
            _ => throw new ArgumentOutOfRangeException(
                nameof(writeClass),
                writeClass,
                "Unknown product write class."),
        };

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

    private static void CreateOrVerifyPrivateDirectory(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
            return;
        }

        if (Directory.Exists(path))
        {
            VerifyPrivateDirectory(path);
            return;
        }

        DirectorySecurity security = BuildPrivateDirectorySecurity();
        DirectoryInfo directory = new(path);
        directory.Create(security);
        VerifyPrivateDirectory(path);
    }

    [SupportedOSPlatform("windows")]
    private static DirectorySecurity BuildPrivateDirectorySecurity()
    {
        SecurityIdentifier currentUser = GetCurrentUserSid();
        DirectorySecurity security = new();
        security.SetOwner(currentUser);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        InheritanceFlags inheritance =
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            inheritance,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            inheritance,
            PropagationFlags.None,
            AccessControlType.Allow));
        return security;
    }

    [SupportedOSPlatform("windows")]
    private static void VerifyPrivateDirectory(string path)
    {
        SecurityIdentifier currentUser = GetCurrentUserSid();
        SecurityIdentifier localSystem =
            new(WellKnownSidType.LocalSystemSid, null);
        DirectorySecurity security = new DirectoryInfo(path).GetAccessControl(
            AccessControlSections.Owner | AccessControlSections.Access);
        if (!security.AreAccessRulesProtected)
        {
            throw new InvalidOperationException(
                "The product directory must have a protected private DACL.");
        }

        SecurityIdentifier owner = security.GetOwner(typeof(SecurityIdentifier))
            as SecurityIdentifier
            ?? throw new InvalidOperationException(
                "The product directory owner identity is unavailable.");
        if (!owner.Equals(currentUser) && !owner.Equals(localSystem))
        {
            throw new InvalidOperationException(
                "The product directory owner is not an authorized principal.");
        }

        bool currentUserFullControl = false;
        bool localSystemFullControl = false;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true,
                     includeInherited: true,
                     typeof(SecurityIdentifier)))
        {
            SecurityIdentifier sid = (SecurityIdentifier)rule.IdentityReference;
            if (rule.IsInherited
                || (rule.AccessControlType == AccessControlType.Allow
                    && !sid.Equals(currentUser)
                    && !sid.Equals(localSystem))
                || (rule.AccessControlType == AccessControlType.Deny
                    && (sid.Equals(currentUser) || sid.Equals(localSystem))))
            {
                throw new InvalidOperationException(
                    "The product directory DACL is not private.");
            }

            bool fullControl =
                (rule.FileSystemRights & FileSystemRights.FullControl)
                == FileSystemRights.FullControl;
            currentUserFullControl |=
                rule.AccessControlType == AccessControlType.Allow
                && sid.Equals(currentUser)
                && fullControl;
            localSystemFullControl |=
                rule.AccessControlType == AccessControlType.Allow
                && sid.Equals(localSystem)
                && fullControl;
        }

        if (!currentUserFullControl || !localSystemFullControl)
        {
            throw new InvalidOperationException(
                "The product directory DACL lacks required private authority.");
        }
    }

    [SupportedOSPlatform("windows")]
    private static SecurityIdentifier GetCurrentUserSid()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return identity.User
            ?? throw new InvalidOperationException(
                "The current Windows identity has no SID.");
    }

    private static void RejectProtectedRoot(string path)
    {
        string[] segments = path.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        if (segments.Any(segment => ProtectedLeafNames.Contains(
            segment,
            StringComparer.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "A protected game or mod-manager root or descendant cannot be a product root.");
        }
    }

    private static void RejectExistingRootReparse(string path)
    {
        string? current = path;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (Directory.Exists(current)
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    "Reparse-point product roots or ancestors are not authorized.");
            }

            string? parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent;
        }
    }

    private static string GetAuthorityIdentity(string path)
    {
        string? parent = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
        {
            using SafeFileHandle handle = CreateFileW(
                parent,
                0,
                FileShare.ReadWrite | FileShare.Delete,
                0,
                FileMode.Open,
                FileFlagsAndAttributes.BackupSemantics,
                0);
            if (handle.IsInvalid
                || !GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION information))
            {
                throw new InvalidOperationException(
                    $"The product-root parent identity is unavailable ({Marshal.GetLastWin32Error()}).");
            }

            string parentIdentity = FormattableString.Invariant(
                $"winobj:{information.VolumeSerialNumber:x8}:{information.FileIndexHigh:x8}{information.FileIndexLow:x8}");
            return parentIdentity
                + ":child:"
                + Path.GetFileName(path).ToUpperInvariant();
        }

        return "winpath:" + path.ToUpperInvariant();
    }

    private void RejectReparseAncestors(string path)
    {
        string relative = Path.GetRelativePath(ProductRoot, path);
        string current = ProductRoot;
        if (Directory.Exists(current)
            && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Reparse-point product roots are not authorized.");
        }

        foreach (string segment in relative.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((Directory.Exists(current) || File.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Reparse-point write paths are not authorized.");
            }
        }
    }

    [Flags]
    private enum FileFlagsAndAttributes : uint
    {
        BackupSemantics = 0x02000000,
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

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        nint securityAttributes,
        FileMode creationDisposition,
        FileFlagsAndAttributes flagsAndAttributes,
        nint templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out BY_HANDLE_FILE_INFORMATION information);
}

public sealed class AttemptStagingAuthority : IDisposable
{
    internal AttemptStagingAuthority(SafeFileHandle handle)
    {
        Handle = handle;
    }

    public SafeFileHandle Handle { get; }

    public void Dispose() => Handle.Dispose();
}

internal sealed record PublicationFileExpectation(
    string RelativePath,
    long ByteLength,
    string Sha256);

#pragma warning restore IDE0008
