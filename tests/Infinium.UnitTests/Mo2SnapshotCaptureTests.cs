using System.Text;
using Infinium.Mo2;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Mo2SnapshotCaptureTests
{
    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Contract")]
    public void ExplicitProfileReconstructsEnabledProviderAndPluginState()
    {
        using SnapshotFixture fixture = new();
        Mo2SnapshotCapture capture = fixture.CreateCapture();

        Mo2SnapshotCaptureResult result = capture.Capture(fixture.Request);

        Assert.AreEqual(SnapshotCaptureState.Completed, result.State);
        Assert.IsNotNull(result.Snapshot);
        Assert.AreEqual("OtherProfile", result.Snapshot.SavedProfileHint);
        Assert.AreEqual("Chosen", Path.GetFileName(result.Snapshot.ProfileRoot));
        Assert.IsFalse(result.Snapshot.Mo2OrUsvfsLaunched);
        Assert.IsFalse(result.Snapshot.ArchiveMemberPopulationSupported);

        ModState high = result.Snapshot.Mods.Single(mod => mod.Name == "High");
        ModState low = result.Snapshot.Mods.Single(mod => mod.Name == "Low");
        Assert.IsTrue(high.Enabled);
        Assert.IsTrue(low.Enabled);
        Assert.IsTrue(high.Priority > low.Priority);

        LooseProviderChain shared = result.Snapshot.LooseProviderChains.Single(
            chain => chain.NormalizedRelativePath.Equals(
                "textures/shared.txt",
                StringComparison.OrdinalIgnoreCase));
        CollectionAssert.AreEqual(
            new[] { low.LocalInstalledEntityId, high.LocalInstalledEntityId },
            shared.Providers.Select(provider => provider.LocalInstalledEntityId).ToArray());
        Assert.AreEqual(high.LocalInstalledEntityId, shared.Winner.LocalInstalledEntityId);

        PluginState plugin = result.Snapshot.Plugins.Single();
        Assert.AreEqual("Sample.esp", plugin.Name);
        Assert.IsTrue(plugin.Enabled);
        Assert.AreEqual(0, plugin.LoadOrder);
        Assert.AreEqual(low.LocalInstalledEntityId, plugin.WinningLocalInstalledEntityId);
        Assert.AreEqual("correlated", plugin.CorrelationState);

        LocalInstalledEntity highEntity = result.Snapshot.LocalInstalledEntities.Single(
            entity => entity.EntityId == high.LocalInstalledEntityId);
        Assert.AreEqual(
            "42",
            highEntity.SourceHints.Single(hint => hint.Key == "general/modid").RawValue);
        Assert.IsTrue(highEntity.SourceHints.All(
            hint => hint.Authority == "mutable-mo2-meta-ini-hint"));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void HiddenManagementAndDisabledContentNeverEnterEffectiveData()
    {
        using SnapshotFixture fixture = new();
        Directory.CreateDirectory(Path.Combine(fixture.ModsRoot, "Disabled"));
        File.WriteAllText(
            Path.Combine(fixture.ModsRoot, "Disabled", "disabled.txt"),
            "disabled",
            Encoding.UTF8);
        File.AppendAllText(fixture.ModListPath, "-Disabled\n", Encoding.UTF8);

        Mo2SnapshotCaptureResult result = fixture.CreateCapture().Capture(fixture.Request);

        Assert.AreEqual(SnapshotCaptureState.Completed, result.State);
        Assert.IsNotNull(result.Snapshot);
        Assert.IsFalse(result.Snapshot.LooseProviderChains.Any(
            chain => chain.NormalizedRelativePath == "disabled.txt"));
        Assert.IsFalse(result.Snapshot.LooseProviderChains.Any(
            chain => chain.NormalizedRelativePath is "meta.ini" or "readme.txt"));
        Assert.IsTrue(result.Snapshot.PhysicalInventory.Any(
            entry => entry.RelativePath.EndsWith(".mohidden", StringComparison.OrdinalIgnoreCase)
                     && entry.Disposition == PhysicalEntryDisposition.HiddenBySuffix));
        Assert.IsTrue(result.Snapshot.PhysicalInventory.Any(
            entry => entry.RelativePath.Equals("meta.ini", StringComparison.OrdinalIgnoreCase)
                     && entry.Disposition == PhysicalEntryDisposition.Mo2ManagementContent));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Fault")]
    public void RunningMo2FailsClosedBeforeReadingProfileState()
    {
        using SnapshotFixture fixture = new();
        Mo2SnapshotCapture capture = fixture.CreateCapture(processRunning: true);

        Mo2SnapshotCaptureResult result = capture.Capture(fixture.Request);

        Assert.AreEqual(SnapshotCaptureState.Failed, result.State);
        Assert.IsNull(result.Snapshot);
        Assert.IsTrue(result.Gaps.Any(gap => gap.Code == "mo2-not-quiescent"));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Fault")]
    public void SameSizeControlMutationInvalidatesCapture()
    {
        using SnapshotFixture fixture = new();
        DateTime originalWrite = File.GetLastWriteTimeUtc(fixture.ModListPath);
        Action mutate = () =>
        {
            byte[] bytes = File.ReadAllBytes(fixture.ModListPath);
            int index = Array.IndexOf(bytes, (byte)'+');
            Assert.IsTrue(index >= 0);
            bytes[index] = (byte)'-';
            File.WriteAllBytes(fixture.ModListPath, bytes);
            File.SetLastWriteTimeUtc(fixture.ModListPath, originalWrite);
        };
        Mo2SnapshotCapture capture = fixture.CreateCapture(mutation: mutate);

        Mo2SnapshotCaptureResult result = capture.Capture(fixture.Request);

        Assert.AreEqual(SnapshotCaptureState.ChangedDuringCapture, result.State);
        Assert.IsNull(result.Snapshot);
        Assert.IsTrue(result.Gaps.Any(gap => gap.Code == "changed-during-capture"));
    }

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Fault")]
    public void SameSizeControlMutationPassesTheFaultGate()
    {
        SameSizeControlMutationInvalidatesCapture();
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Fault")]
    public void UnknownMapperAndMissingModRemainVisibleGaps()
    {
        using SnapshotFixture fixture = new();
        File.AppendAllText(fixture.ModListPath, "+Missing\n", Encoding.UTF8);
        string mapperRoot = Directory.CreateDirectory(
            Path.Combine(fixture.Root, "mapper")).FullName;
        File.WriteAllText(Path.Combine(mapperRoot, "mapped.txt"), "mapped", Encoding.UTF8);
        string mapperHash = new('a', 64);
        Mo2SnapshotCaptureRequest request = fixture.Request with
        {
            QualifiedMappings =
            [
                new QualifiedMapping("mapper-one", mapperRoot, "", mapperHash),
            ],
            EnabledMapperSha256s = [mapperHash],
        };

        Mo2SnapshotCaptureResult result = fixture.CreateCapture().Capture(request);

        Assert.AreEqual(SnapshotCaptureState.CompletedWithGaps, result.State);
        Assert.IsNotNull(result.Snapshot);
        Assert.IsTrue(result.Gaps.Any(gap => gap.Code == "listed-mod-missing"));
        Assert.IsTrue(result.Gaps.Any(gap => gap.Code == "unknown-or-unqualified-mapper"));
        Assert.IsFalse(result.Snapshot.LooseProviderChains.Any(
            chain => chain.NormalizedRelativePath == "mapped.txt"));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void QualifiedMapperContributesOnlyItsDeclaredVirtualPrefix()
    {
        using SnapshotFixture fixture = new();
        string mapperRoot = Directory.CreateDirectory(
            Path.Combine(fixture.Root, "qualified-mapper")).FullName;
        string physical = Path.Combine(mapperRoot, "mapped.txt");
        File.WriteAllText(physical, "mapped", Encoding.UTF8);
        string mapperHash = new('b', 64);
        Mo2SnapshotCaptureRequest request = fixture.Request with
        {
            QualifiedMappings =
            [
                new QualifiedMapping("mapper-one", mapperRoot, "virtual", mapperHash),
            ],
            EnabledMapperSha256s = [mapperHash],
        };

        Mo2SnapshotCaptureResult result = fixture
            .CreateCapture(
                qualifiedMapperHashes: new HashSet<string>(
                    [mapperHash],
                    StringComparer.OrdinalIgnoreCase))
            .Capture(request);

        Assert.AreEqual(SnapshotCaptureState.Completed, result.State);
        Assert.IsNotNull(result.Snapshot);
        LooseProviderChain mapped = result.Snapshot.LooseProviderChains.Single(
            chain => chain.NormalizedRelativePath == "virtual/mapped.txt");
        Assert.AreEqual(LooseProviderKind.QualifiedMapper, mapped.Winner.Kind);
        Assert.AreEqual(physical, mapped.Winner.PhysicalPath);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void LocalInstalledIdentityDoesNotCollapseMutableSourceHints()
    {
        using SnapshotFixture fixture = new();
        Mo2SnapshotCaptureResult first = fixture.CreateCapture().Capture(fixture.Request);
        Assert.IsNotNull(first.Snapshot);
        LocalInstalledEntity before = first.Snapshot.LocalInstalledEntities.Single(
            entity => entity.SourceHints.Any(hint => hint.Key == "general/modid"));

        File.WriteAllText(
            Path.Combine(fixture.ModsRoot, "High", "meta.ini"),
            "[General]\nmodid=9001\nversion=9.0\n",
            Encoding.UTF8);
        Mo2SnapshotCaptureResult second = fixture.CreateCapture().Capture(fixture.Request);
        Assert.IsNotNull(second.Snapshot);
        LocalInstalledEntity after = second.Snapshot.LocalInstalledEntities.Single(
            entity => entity.PhysicalPath == before.PhysicalPath);

        Assert.AreEqual(before.EntityId, after.EntityId);
        Assert.AreEqual(
            "9001",
            after.SourceHints.Single(hint => hint.Key == "general/modid").RawValue);
        Assert.AreEqual("mutable-mo2-meta-ini-hint", after.SourceHints[0].Authority);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void MissingAndUnrecognizedExecutablesFailAdmissionWithoutFallback()
    {
        SupportedExecutableManifests manifests = new();

        ExecutableAdmission missing = manifests.AdmitSkyrim(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.exe"),
            SupportedRuntime());

        Assert.AreEqual(AdmissionState.Indeterminate, missing.State);
        Assert.AreEqual(SupportedExecutableManifests.SkyrimManifestId, missing.ManifestId);
    }

    private sealed class SnapshotFixture : IDisposable
    {
        internal SnapshotFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"Infinium-Slice3-{Guid.NewGuid():N}");
            InstanceRoot = Directory.CreateDirectory(Path.Combine(Root, "instance")).FullName;
            ProfilesRoot = Directory.CreateDirectory(Path.Combine(InstanceRoot, "profiles")).FullName;
            ModsRoot = Directory.CreateDirectory(Path.Combine(InstanceRoot, "mods")).FullName;
            OverwriteRoot = Directory.CreateDirectory(Path.Combine(InstanceRoot, "overwrite")).FullName;
            GameDataRoot = Directory.CreateDirectory(Path.Combine(Root, "game", "Data")).FullName;
            string chosen = Directory.CreateDirectory(
                Path.Combine(ProfilesRoot, "Chosen")).FullName;
            Directory.CreateDirectory(Path.Combine(ProfilesRoot, "OtherProfile"));

            string low = Directory.CreateDirectory(Path.Combine(ModsRoot, "Low")).FullName;
            string high = Directory.CreateDirectory(Path.Combine(ModsRoot, "High")).FullName;
            Directory.CreateDirectory(Path.Combine(low, "textures"));
            Directory.CreateDirectory(Path.Combine(high, "textures"));
            File.WriteAllText(Path.Combine(low, "textures", "shared.txt"), "low", Encoding.UTF8);
            File.WriteAllText(Path.Combine(high, "textures", "shared.txt"), "high", Encoding.UTF8);
            File.WriteAllText(Path.Combine(low, "Sample.esp"), "plugin", Encoding.UTF8);
            File.WriteAllText(Path.Combine(high, "hidden.txt.mohidden"), "hidden", Encoding.UTF8);
            File.WriteAllText(Path.Combine(high, "readme.txt"), "docs", Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(high, "meta.ini"),
                "[General]\nmodid=42\nversion=1.0\n",
                Encoding.UTF8);
            File.WriteAllText(Path.Combine(GameDataRoot, "physical.txt"), "data", Encoding.UTF8);
            File.WriteAllText(Path.Combine(OverwriteRoot, "generated.txt"), "overwrite", Encoding.UTF8);

            Mo2ExecutablePath = Path.Combine(Root, "ModOrganizer.exe");
            SkyrimExecutablePath = Path.Combine(Root, "game", "SkyrimSE.exe");
            InstanceIniPath = Path.Combine(InstanceRoot, "ModOrganizer.ini");
            File.WriteAllText(
                InstanceIniPath,
                "[General]\n"
                + "selected_profile=@ByteArray(OtherProfile)\n"
                + "gameName=Skyrim Special Edition\n"
                + $"gamePath={Path.GetDirectoryName(SkyrimExecutablePath)!.Replace('\\', '/')}\n"
                + "[Settings]\n"
                + $"base_directory={InstanceRoot.Replace('\\', '/')}\n",
                Encoding.UTF8);
            ModListPath = Path.Combine(chosen, "modlist.txt");
            File.WriteAllText(ModListPath, "+High\n+Low\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(chosen, "plugins.txt"), "*Sample.esp\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(chosen, "loadorder.txt"), "Sample.esp\n", Encoding.UTF8);
            File.WriteAllText(Path.Combine(chosen, "archives.txt"), "Sample.bsa\n", Encoding.UTF8);

            File.WriteAllText(Mo2ExecutablePath, "synthetic executable", Encoding.UTF8);
            File.WriteAllText(SkyrimExecutablePath, "synthetic executable", Encoding.UTF8);
            Request = new Mo2SnapshotCaptureRequest(
                Mo2ExecutablePath,
                InstanceRoot,
                InstanceIniPath,
                ProfilesRoot,
                ModsRoot,
                OverwriteRoot,
                GameDataRoot,
                SkyrimExecutablePath,
                "Chosen",
                SupportedRuntime(),
                [],
                []);
        }

        internal string Root { get; }

        internal string InstanceRoot { get; }

        internal string ProfilesRoot { get; }

        internal string ModsRoot { get; }

        internal string OverwriteRoot { get; }

        internal string GameDataRoot { get; }

        internal string InstanceIniPath { get; }

        internal string ModListPath { get; }

        internal string Mo2ExecutablePath { get; }

        internal string SkyrimExecutablePath { get; }

        internal Mo2SnapshotCaptureRequest Request { get; }

        internal Mo2SnapshotCapture CreateCapture(
            bool processRunning = false,
            Action? mutation = null,
            IReadOnlySet<string>? qualifiedMapperHashes = null)
        {
            _ = Root;
            return new Mo2SnapshotCapture(
                new AcceptingManifests(),
                new FixedProcessProbe(processRunning),
                qualifiedMapperHashes
                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                mutation);
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class AcceptingManifests : SupportedExecutableManifests
    {
        public override ExecutableAdmission AdmitMo2(string path)
        {
            return Accepted(Mo2ManifestId, Path.GetFileName(path));
        }

        public override ExecutableAdmission AdmitSkyrim(
            string path,
            RuntimeTargetContext context)
        {
            Assert.AreEqual(SupportedRuntime(), context);
            return Accepted(SkyrimManifestId, Path.GetFileName(path));
        }

        private static ExecutableAdmission Accepted(string manifestId, string fileName)
        {
            return new ExecutableAdmission(
                AdmissionState.Accepted,
                manifestId,
                new ExecutableIdentity(fileName, 1, new string('a', 64), "test", null, null, null),
                []);
        }
    }

    private static RuntimeTargetContext SupportedRuntime()
    {
        return new RuntimeTargetContext("windows-x64", "steam", "489830");
    }

    private sealed class FixedProcessProbe(bool running) : IMo2ProcessProbe
    {
        public bool IsRunning(string exactExecutablePath)
        {
            Assert.IsTrue(Path.IsPathFullyQualified(exactExecutablePath));
            return running;
        }
    }
}
