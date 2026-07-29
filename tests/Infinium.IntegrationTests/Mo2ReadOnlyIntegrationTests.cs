using System.Security.Cryptography;
using System.Text;
using Infinium.Mo2;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Mo2ReadOnlyIntegrationTests
{
    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public void ExplicitCaptureIsReadOnlyAcrossEveryDeclaredProtectedRoot()
    {
        using IntegrationFixture fixture = new();
        string before = FingerprintTree(fixture.Root);
        using WindowsWriteAuthorityRegistry authority = new(
            [fixture.InstanceRoot, fixture.GameRoot]);
        IReadOnlyList<string> identitiesBefore = authority.ProtectedRootIdentities;
        Mo2SnapshotCapture capture = fixture.CreateCapture();

        string afterConstruction = FingerprintTree(fixture.Root);
        Mo2SnapshotCaptureResult result = capture.Capture(fixture.Request);
        string afterCapture = FingerprintTree(fixture.Root);

        Assert.AreEqual(before, afterConstruction);
        Assert.AreEqual(before, afterCapture);
        CollectionAssert.AreEqual(
            identitiesBefore.ToArray(),
            authority.ProtectedRootIdentities.ToArray());
        Assert.AreEqual(SnapshotCaptureState.Completed, result.State);
        Assert.IsNotNull(result.Snapshot);
        Assert.IsFalse(result.Snapshot.Mo2OrUsvfsLaunched);
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Security")]
    public void ProtectedRootReadOnlyCapturePassesTheSecurityGate()
    {
        ExplicitCaptureIsReadOnlyAcrossEveryDeclaredProtectedRoot();
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestProperty("Category", "M1Integration")]
    public void NoPassiveEventCreatesASnapshotOrStartsAProcess()
    {
        using IntegrationFixture fixture = new();
        int processCountBefore = System.Diagnostics.Process
            .GetProcessesByName("ModOrganizer")
            .Length;
        Mo2SnapshotCapture capture = fixture.CreateCapture();

        Assert.AreEqual(0, fixture.ProcessProbeCallCount);
        Assert.AreEqual(
            processCountBefore,
            System.Diagnostics.Process.GetProcessesByName("ModOrganizer").Length);

        Mo2SnapshotCaptureResult result = capture.Capture(fixture.Request);

        Assert.AreEqual(1, fixture.ProcessProbeCallCount);
        Assert.IsNotNull(result.Snapshot);
        Assert.AreEqual(
            processCountBefore,
            System.Diagnostics.Process.GetProcessesByName("ModOrganizer").Length);
    }

    private static string FingerprintTree(string root)
    {
        StringBuilder canonical = new();
        foreach (string path in Directory
                     .EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(root, path), StringComparer.OrdinalIgnoreCase)
                     .ThenBy(path => Path.GetRelativePath(root, path), StringComparer.Ordinal))
        {
            FileAttributes attributes = File.GetAttributes(path);
            string relative = Path.GetRelativePath(root, path);
            canonical.Append(relative)
                .Append('|')
                .Append((long)attributes)
                .Append('|');
            if ((attributes & FileAttributes.Directory) == 0)
            {
                FileInfo info = new(path);
                canonical.Append(info.Length)
                    .Append('|')
                    .Append(info.CreationTimeUtc.Ticks)
                    .Append('|')
                    .Append(info.LastWriteTimeUtc.Ticks)
                    .Append('|')
                    .Append(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            }

            canonical.AppendLine();
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private sealed class IntegrationFixture : IDisposable
    {
        private readonly CountingProbe probe;

        internal IntegrationFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"Infinium-Slice3-Integration-{Guid.NewGuid():N}");
            InstanceRoot = Directory.CreateDirectory(Path.Combine(Root, "instance")).FullName;
            string profiles = Directory.CreateDirectory(Path.Combine(InstanceRoot, "profiles")).FullName;
            string profile = Directory.CreateDirectory(Path.Combine(profiles, "Explicit")).FullName;
            string mods = Directory.CreateDirectory(Path.Combine(InstanceRoot, "mods")).FullName;
            string mod = Directory.CreateDirectory(Path.Combine(mods, "Provider")).FullName;
            string overwrite = Directory.CreateDirectory(Path.Combine(InstanceRoot, "overwrite")).FullName;
            GameRoot = Directory.CreateDirectory(Path.Combine(Root, "game")).FullName;
            string data = Directory.CreateDirectory(Path.Combine(GameRoot, "Data")).FullName;
            string instanceIni = Path.Combine(InstanceRoot, "ModOrganizer.ini");
            string mo2 = Path.Combine(Root, "ModOrganizer.exe");
            string skyrim = Path.Combine(GameRoot, "SkyrimSE.exe");

            File.WriteAllText(
                instanceIni,
                "[General]\n"
                + "selected_profile=Explicit\n"
                + "gameName=Skyrim Special Edition\n"
                + $"gamePath={GameRoot.Replace('\\', '/')}\n"
                + "[Settings]\n"
                + $"base_directory={InstanceRoot.Replace('\\', '/')}\n",
                Encoding.UTF8);
            File.WriteAllText(Path.Combine(profile, "modlist.txt"), "+Provider\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(profile, "plugins.txt"), "*P.esp\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(profile, "loadorder.txt"), "P.esp\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(mod, "P.esp"), "plugin", Encoding.UTF8);
            File.WriteAllText(Path.Combine(data, "base.txt"), "base", Encoding.UTF8);
            File.WriteAllText(mo2, "synthetic", Encoding.UTF8);
            File.WriteAllText(skyrim, "synthetic", Encoding.UTF8);

            Request = new Mo2SnapshotCaptureRequest(
                mo2,
                InstanceRoot,
                instanceIni,
                profiles,
                mods,
                overwrite,
                data,
                skyrim,
                "Explicit",
                new RuntimeTargetContext("windows-x64", "steam", "489830"),
                [],
                []);
            probe = new CountingProbe();
        }

        internal string Root { get; }

        internal string InstanceRoot { get; }

        internal string GameRoot { get; }

        internal int ProcessProbeCallCount => probe.CallCount;

        internal Mo2SnapshotCaptureRequest Request { get; }

        internal Mo2SnapshotCapture CreateCapture()
        {
            return new Mo2SnapshotCapture(
                new AcceptingManifests(),
                probe,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                null);
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class CountingProbe : IMo2ProcessProbe
    {
        internal int CallCount { get; private set; }

        public bool IsRunning(string exactExecutablePath)
        {
            Assert.IsTrue(File.Exists(exactExecutablePath));
            CallCount++;
            return false;
        }
    }

    private sealed class AcceptingManifests : SupportedExecutableManifests
    {
        public override ExecutableAdmission AdmitMo2(string path)
        {
            return Accepted(Mo2ManifestId, path);
        }

        public override ExecutableAdmission AdmitSkyrim(
            string path,
            RuntimeTargetContext context)
        {
            Assert.AreEqual("windows-x64", context.Platform);
            return Accepted(SkyrimManifestId, path);
        }

        private static ExecutableAdmission Accepted(string manifestId, string path)
        {
            return new ExecutableAdmission(
                AdmissionState.Accepted,
                manifestId,
                new ExecutableIdentity(
                    Path.GetFileName(path),
                    1,
                    new string('a', 64),
                    "synthetic",
                    null,
                    null,
                    null),
                []);
        }
    }
}
