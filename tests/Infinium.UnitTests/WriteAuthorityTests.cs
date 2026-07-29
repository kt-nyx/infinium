using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class WriteAuthorityTests
{
    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void RegisteredProtectedRootRejectsDescendantsAndCaseAliases()
    {
        string root = Temp();
        string protectedRoot = Path.Combine(root, "ProtectedSyntheticRoot");
        try
        {
            Directory.CreateDirectory(protectedRoot);
            using WindowsWriteAuthorityRegistry registry = new([protectedRoot]);
            Assert.HasCount(1, registry.ProtectedRootIdentities);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => new StoragePaths(
                    Path.Combine(protectedRoot, "nested", "product"),
                    registry));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => new StoragePaths(
                    Path.Combine(
                        protectedRoot.ToUpperInvariant(),
                        "NESTED",
                        "PRODUCT"),
                    registry));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void RegisteredProtectedRootRejectsShortNameAliasWhenAvailable()
    {
        string root = Temp();
        string protectedRoot = Path.Combine(root, "Protected Synthetic Root With Long Name");
        try
        {
            Directory.CreateDirectory(protectedRoot);
            string? shortPath = TryGetShortPath(protectedRoot);
            if (string.IsNullOrWhiteSpace(shortPath)
                || string.Equals(shortPath, protectedRoot, StringComparison.OrdinalIgnoreCase))
            {
                Assert.Inconclusive(
                    "NTFS 8.3 short-name generation is unavailable on the test volume.");
            }

            using WindowsWriteAuthorityRegistry registry = new([protectedRoot]);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => new StoragePaths(
                    Path.Combine(shortPath!, "nested", "product"),
                    registry));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void ProductAuthorityRejectsSymbolicLinkTraversalWhenAvailable()
    {
        string root = Temp();
        string protectedRoot = Path.Combine(root, "protected");
        string alias = Path.Combine(root, "alias");
        try
        {
            Directory.CreateDirectory(protectedRoot);
            try
            {
                _ = Directory.CreateSymbolicLink(alias, protectedRoot);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                    or IOException
                    or PlatformNotSupportedException)
            {
                Assert.Inconclusive(
                    $"Directory symbolic links are unavailable: {exception.GetType().Name}.");
            }

            using WindowsWriteAuthorityRegistry registry = new([protectedRoot]);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => new StoragePaths(Path.Combine(alias, "product"), registry));
        }
        finally
        {
            DeleteLink(alias);
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void ProductAuthorityRejectsJunctionTraversalWhenAvailable()
    {
        string root = Temp();
        string protectedRoot = Path.Combine(root, "protected");
        string junction = Path.Combine(root, "junction");
        try
        {
            Directory.CreateDirectory(protectedRoot);
            ProcessStartInfo start = new()
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("/d");
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add("mklink");
            start.ArgumentList.Add("/J");
            start.ArgumentList.Add(junction);
            start.ArgumentList.Add(protectedRoot);
            using Process process = Process.Start(start)
                ?? throw new InvalidOperationException("The junction probe could not start.");
            process.WaitForExit();
            if (process.ExitCode != 0 || !Directory.Exists(junction))
            {
                Assert.Inconclusive(
                    "Directory junction creation is unavailable on the test volume.");
            }

            using WindowsWriteAuthorityRegistry registry = new([protectedRoot]);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => new StoragePaths(Path.Combine(junction, "product"), registry));
        }
        finally
        {
            DeleteLink(junction);
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void ExistingHardLinkedWriteTargetIsRejectedWhenAvailable()
    {
        string root = Temp();
        string protectedRoot = Path.Combine(root, "protected");
        string productRoot = Path.Combine(root, "product");
        try
        {
            Directory.CreateDirectory(protectedRoot);
            string protectedFile = Path.Combine(protectedRoot, "protected.bin");
            File.WriteAllText(protectedFile, "protected");
            using WindowsWriteAuthorityRegistry registry = new([protectedRoot]);
            using StoragePaths paths = new(productRoot, registry);
            paths.Create();
            string hardLink = Path.Combine(paths.Data, "alias.bin");
            if (!CreateHardLinkW(hardLink, protectedFile, 0))
            {
                int error = Marshal.GetLastWin32Error();
                Assert.Inconclusive(
                    $"Hard links are unavailable (Win32 error {error}).");
            }

            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductPath(ProductWriteClass.Data, "alias.bin"));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void PinnedProductRootRejectsReplacementAndDisposedRegistryInvalidatesCapabilities()
    {
        string root = Temp();
        string protectedRoot = Path.Combine(root, "protected");
        string productRoot = Path.Combine(root, "product");
        try
        {
            Directory.CreateDirectory(protectedRoot);
            WindowsWriteAuthorityRegistry registry = new([protectedRoot]);
            using StoragePaths paths = new(productRoot, registry);
            paths.Create();
            Assert.ThrowsExactly<IOException>(
                () => Directory.Move(
                    productRoot,
                    Path.Combine(root, "product-original")));

            registry.Dispose();
            Assert.ThrowsExactly<ObjectDisposedException>(
                () => paths.ResolveProductPath(ProductWriteClass.Data, "new.bin"));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void PinnedProtectedRootDetectsReplacementWhenRenameIsPermitted()
    {
        string root = Temp();
        string protectedRoot = Path.Combine(root, "protected");
        try
        {
            Directory.CreateDirectory(protectedRoot);
            using WindowsWriteAuthorityRegistry registry = new([protectedRoot]);
            string original = Path.Combine(root, "protected-original");
            Directory.Move(protectedRoot, original);
            Directory.CreateDirectory(protectedRoot);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => new StoragePaths(Path.Combine(root, "product"), registry));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void ProductAuthorityIdentitySurvivesCreationOfMissingIntermediateDirectories()
    {
        string root = Temp();
        string productRoot = Path.Combine(root, "missing", "nested", "product");
        try
        {
            Directory.CreateDirectory(root);
            using WindowsWriteAuthorityRegistry registry = new([]);
            using StoragePaths beforeCreation = new(productRoot, registry);
            string authorityIdentity = beforeCreation.AuthorityIdentity;

            Directory.CreateDirectory(Path.GetDirectoryName(productRoot)!);
            beforeCreation.Create();

            using StoragePaths afterCreation = new(
                productRoot.ToUpperInvariant() + Path.DirectorySeparatorChar,
                registry);
            Assert.AreEqual(authorityIdentity, afterCreation.AuthorityIdentity);
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void ProductAuthorityRejectsDeviceAdsAndRecursiveDeleteRequests()
    {
        string root = Temp();
        string protectedRoot = Path.Combine(root, "protected");
        string productRoot = Path.Combine(root, "product");
        try
        {
            Directory.CreateDirectory(protectedRoot);
            using WindowsWriteAuthorityRegistry registry = new([protectedRoot]);
            using StoragePaths paths = new(productRoot, registry);
            paths.Create();
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductPath(ProductWriteClass.Data, "file.bin:stream"));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductPath(
                    ProductWriteClass.Data,
                    @"\\?\C:\device-path.bin"));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductPathForDeletion(
                    ProductWriteClass.Data,
                    string.Empty,
                    recursive: true));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void ClosedWriteClassMappingRejectsUnclassifiedAndEscapingPaths()
    {
        string root = Temp();
        try
        {
            Directory.CreateDirectory(root);
            using WindowsWriteAuthorityRegistry registry = new([]);
            using StoragePaths paths = new(Path.Combine(root, "product"), registry);
            paths.Create();

            Assert.AreEqual(
                Path.Combine(paths.Runtime, RuntimeDescriptorFileName),
                paths.ResolveProductPath(
                    ProductWriteClass.Runtime,
                    RuntimeDescriptorFileName));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => paths.ResolveProductPath(
                    (ProductWriteClass)int.MaxValue,
                    "file.bin"));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductPath(ProductWriteClass.Data, @"..\outside.bin"));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(
                () => paths.ResolveProductPath((ProductWriteClass)999, "file.bin"));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void CreatedProductTreeUsesProtectedPrivateInheritableDacls()
    {
        string root = Temp();
        try
        {
            Directory.CreateDirectory(root);
            using WindowsWriteAuthorityRegistry registry = new([]);
            using StoragePaths paths = new(Path.Combine(root, "product"), registry);
            paths.Create();

            foreach (string path in new[]
                     {
                         paths.ProductRoot,
                         paths.Data,
                         paths.Payloads,
                         paths.Staging,
                         paths.Backups,
                         paths.Runtime,
                         paths.RunOutput,
                     })
            {
                AssertPrivateDirectory(path);
            }
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void ExistingPermissiveProductRootFailsClosedWithoutAclRewrite()
    {
        string root = Temp();
        string productRoot = Path.Combine(root, "product");
        try
        {
            Directory.CreateDirectory(productRoot);
            DirectorySecurity permissive = new DirectoryInfo(productRoot).GetAccessControl();
            permissive.SetAccessRuleProtection(isProtected: false, preserveInheritance: true);
            permissive.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                FileSystemRights.ReadAndExecute,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
            new DirectoryInfo(productRoot).SetAccessControl(permissive);
            string before = new DirectoryInfo(productRoot)
                .GetAccessControl()
                .GetSecurityDescriptorSddlForm(AccessControlSections.All);

            using WindowsWriteAuthorityRegistry registry = new([]);
            using StoragePaths paths = new(productRoot, registry);
            Assert.ThrowsExactly<InvalidOperationException>(paths.Create);

            string after = new DirectoryInfo(productRoot)
                .GetAccessControl()
                .GetSecurityDescriptorSddlForm(AccessControlSections.All);
            Assert.AreEqual(before, after);
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void RetainedClassHandleDetectsRenameAndHandlesReleaseOnDispose()
    {
        string root = Temp();
        string productRoot = Path.Combine(root, "product");
        string movedRoot = Path.Combine(root, "product-moved");
        string movedData = Path.Combine(productRoot, "data-moved");
        try
        {
            Directory.CreateDirectory(root);
            using WindowsWriteAuthorityRegistry registry = new([]);
            StoragePaths paths = new(productRoot, registry);
            paths.Create();

            Directory.Move(paths.Data, movedData);
            Directory.CreateDirectory(paths.Data);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductPath(ProductWriteClass.Data, "new.bin"));

            paths.Dispose();
            Directory.Move(productRoot, movedRoot);
            Assert.IsTrue(Directory.Exists(movedRoot));
        }
        finally
        {
            Delete(root);
        }
    }

    private const string RuntimeDescriptorFileName = "coordinator.v1.json";

    private static void AssertPrivateDirectory(string path)
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier currentUser = identity.User
            ?? throw new InvalidOperationException("The test identity has no SID.");
        SecurityIdentifier localSystem =
            new(WellKnownSidType.LocalSystemSid, null);
        DirectorySecurity security = new DirectoryInfo(path).GetAccessControl();
        Assert.IsTrue(security.AreAccessRulesProtected);

        bool currentUserFullControl = false;
        bool localSystemFullControl = false;
        foreach (FileSystemAccessRule rule in security.GetAccessRules(
                     includeExplicit: true,
                     includeInherited: true,
                     typeof(SecurityIdentifier)))
        {
            SecurityIdentifier sid = (SecurityIdentifier)rule.IdentityReference;
            Assert.IsFalse(rule.IsInherited);
            if (rule.AccessControlType == AccessControlType.Allow)
            {
                Assert.IsTrue(sid.Equals(currentUser) || sid.Equals(localSystem));
                bool fullControl =
                    (rule.FileSystemRights & FileSystemRights.FullControl)
                    == FileSystemRights.FullControl;
                currentUserFullControl |= sid.Equals(currentUser) && fullControl;
                localSystemFullControl |= sid.Equals(localSystem) && fullControl;
            }
        }

        Assert.IsTrue(currentUserFullControl);
        Assert.IsTrue(localSystemFullControl);
    }

    private static string Temp() =>
        Path.Combine(Path.GetTempPath(), $"infinium-write-authority-{Guid.NewGuid():N}");

    private static string? TryGetShortPath(string path)
    {
        char[] buffer = new char[512];
        uint length = GetShortPathNameW(path, buffer, checked((uint)buffer.Length));
        return length is 0 or >= 512
            ? null
            : new string(buffer, 0, checked((int)length));
    }

    private static void DeleteLink(string path)
    {
        if (Directory.Exists(path)
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(path);
        }
    }

    private static void Delete(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathNameW(
        string longPath,
        [Out] char[] shortPath,
        uint bufferLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(
        string fileName,
        string existingFileName,
        nint securityAttributes);
}
