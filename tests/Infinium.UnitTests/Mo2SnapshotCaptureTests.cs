using System.Text;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Mo2SnapshotCaptureTests
{
    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Contract")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Contract")]
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
        Assert.IsFalse(result.Gaps.Any(
            gap => gap.Code == "mo2-game-plugin-inventory-unqualified"));

        ModState high = result.Snapshot.Mods.Single(mod => mod.Name == "High");
        ModState low = result.Snapshot.Mods.Single(mod => mod.Name == "Low");
        Assert.AreEqual(ModEnablementState.Enabled, high.Enablement);
        Assert.AreEqual(ModEnablementState.Enabled, low.Enablement);
        Assert.IsTrue(high.Priority > low.Priority);
        Assert.ThrowsExactly<NotSupportedException>(
            () => ((IList<ModState>)result.Snapshot.Mods)[0] = low);

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
    [TestCategory("Unit")]
    [TestCategory("Contract")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Contract")]
    public void SnapshotRetainsRawControlsAndCanonicalStructuralDependencies()
    {
        using SnapshotFixture fixture = new();

        Mo2SnapshotCaptureResult result = fixture.CreateCapture().Capture(fixture.Request);

        Assert.IsNotNull(result.Snapshot);
        Mo2SnapshotDependencyManifest dependencies = result.Snapshot.Dependencies;
        Assert.AreEqual(new ContractVersion(1, 0, 0), dependencies.SchemaVersion);
        Assert.AreEqual(
            result.Snapshot.Contract.StructuralManifestFingerprint,
            dependencies.CanonicalFingerprint);
        Assert.AreEqual("mod-organizer-2", dependencies.ManagerId);
        Assert.AreEqual("Chosen", dependencies.ExplicitSelectedProfileName);
        SnapshotControlObservation modList = dependencies.ControlObservations.Single(
            observation => observation.Role == "modlist");
        CollectionAssert.AreEqual(
            File.ReadAllBytes(fixture.ModListPath),
            Convert.FromBase64String(modList.Base64Bytes));
        Assert.IsTrue(string.Equals(
            Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    Convert.FromBase64String(modList.Base64Bytes))),
            modList.Fingerprint.Value,
            StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(string.IsNullOrWhiteSpace(modList.PhysicalObjectIdentity));
        Assert.IsTrue(dependencies.ControlObservations.Any(
            observation => observation.Role.StartsWith(
                "mod-meta:",
                StringComparison.Ordinal)));
        Assert.IsTrue(dependencies.RootObservations.Any(
            observation => observation.Role == "game-data"));
        Assert.IsTrue(dependencies.StructuralObservations.Any(
            observation => observation.RootRole == "mods"
                           && observation.RelativePath == "Low/Sample.esp"));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<SnapshotControlObservation>)dependencies.ControlObservations)
                .RemoveAt(0));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<SnapshotStructuralObservation>)dependencies.StructuralObservations)
                .RemoveAt(0));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Contract")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Contract")]
    public void IdenticalRecapturesHaveDistinctOccurrenceIdsAndStableFingerprints()
    {
        using SnapshotFixture fixture = new();
        Mo2SnapshotCapture capture = fixture.CreateCapture();

        Mo2SnapshotCaptureResult first = capture.Capture(fixture.Request);
        Mo2SnapshotCaptureResult second = capture.Capture(fixture.Request);

        Assert.IsNotNull(first.Snapshot);
        Assert.IsNotNull(second.Snapshot);
        Assert.AreEqual(
            first.Snapshot.Contract.StructuralManifestFingerprint,
            second.Snapshot.Contract.StructuralManifestFingerprint);
        Assert.AreNotEqual(
            first.Snapshot.Contract.SnapshotId,
            second.Snapshot.Contract.SnapshotId);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Contract")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Contract")]
    public void ExactGamePluginForcesCoreOrderAndClassifiesForeignData()
    {
        using SnapshotFixture fixture = new();
        string gameRoot = Directory.GetParent(fixture.GameDataRoot)!.FullName;
        foreach (string core in new[] { "Skyrim.esm", "Update.esm", "ccExample.esl" })
        {
            File.WriteAllText(
                Path.Combine(fixture.GameDataRoot, core),
                core,
                Encoding.UTF8);
        }

        File.WriteAllText(
            Path.Combine(fixture.GameDataRoot, "Foreign.esp"),
            "foreign",
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(gameRoot, "Skyrim.ccc"),
            "ccExample.esl\n",
            Encoding.UTF8);
        string profile = Path.Combine(fixture.ProfilesRoot, "Chosen");
        File.AppendAllText(
            Path.Combine(profile, "plugins.txt"),
            "*Foreign.esp\n",
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(profile, "loadorder.txt"),
            "Skyrim.esm\nSample.esp\nForeign.esp\n",
            Encoding.UTF8);

        Mo2SnapshotCaptureResult result = fixture.CreateCapture().Capture(fixture.Request);

        Assert.IsNotNull(result.Snapshot);
        PluginState skyrim = result.Snapshot.Plugins.Single(
            plugin => plugin.Name.Equals("Skyrim.esm", StringComparison.OrdinalIgnoreCase));
        PluginState update = result.Snapshot.Plugins.Single(
            plugin => plugin.Name.Equals("Update.esm", StringComparison.OrdinalIgnoreCase));
        PluginState creation = result.Snapshot.Plugins.Single(
            plugin => plugin.Name.Equals("ccExample.esl", StringComparison.OrdinalIgnoreCase));
        PluginState foreign = result.Snapshot.Plugins.Single(
            plugin => plugin.Name.Equals("Foreign.esp", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(
            PluginEnablementState.ForcedEnabledByGamePlugin,
            skyrim.Enablement);
        Assert.AreEqual(PluginClassification.PrimaryGame, skyrim.Classification);
        Assert.AreEqual(0, skyrim.LoadOrder);
        Assert.AreEqual(1, update.LoadOrder);
        Assert.AreEqual(PluginClassification.CreationClubGame, creation.Classification);
        Assert.AreEqual(2, creation.LoadOrder);
        Assert.AreEqual(PluginClassification.ForeignGameData, foreign.Classification);
        Assert.AreEqual(PluginEnablementState.EnabledByProfile, foreign.Enablement);
        Assert.IsTrue(foreign.LoadOrder > creation.LoadOrder);
        Assert.IsFalse(result.Gaps.Any(gap => gap.Code == "duplicate-loadorder-entry"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Security")]
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
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
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
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
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
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void UnknownMapperAndMissingModRemainVisibleGaps()
    {
        using SnapshotFixture fixture = new();
        File.AppendAllText(fixture.ModListPath, "+Missing\n", Encoding.UTF8);
        string unlisted = Directory.CreateDirectory(
            Path.Combine(fixture.ModsRoot, "Unlisted")).FullName;
        File.WriteAllText(Path.Combine(unlisted, "must-not-win.txt"), "unknown", Encoding.UTF8);
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
        ModState unresolved =
            result.Snapshot.Mods.Single(mod => mod.Name == "Unlisted");
        Assert.AreEqual(ModEnablementState.Unresolved, unresolved.Enablement);
        Assert.IsNull(unresolved.Priority);
        Assert.IsFalse(result.Snapshot.LooseProviderChains.Any(
            chain => chain.NormalizedRelativePath == "must-not-win.txt"));
        Assert.IsFalse(result.Snapshot.LooseProviderChains.Any(
            chain => chain.NormalizedRelativePath == "mapped.txt"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
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
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
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
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void SameSizeMetaMutationInvalidatesCapture()
    {
        using SnapshotFixture fixture = new();
        string meta = Path.Combine(fixture.ModsRoot, "High", "meta.ini");
        DateTime originalWrite = File.GetLastWriteTimeUtc(meta);
        Action mutate = () =>
        {
            string text = File.ReadAllText(meta, Encoding.UTF8);
            File.WriteAllText(meta, text.Replace("modid=42", "modid=43"), Encoding.UTF8);
            File.SetLastWriteTimeUtc(meta, originalWrite);
        };

        Mo2SnapshotCaptureResult result =
            fixture.CreateCapture(mutation: mutate).Capture(fixture.Request);

        Assert.AreEqual(SnapshotCaptureState.ChangedDuringCapture, result.State);
        Assert.IsNull(result.Snapshot);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Security")]
    public void HiddenDirectoriesAndConfiguredSkipsNeverContribute()
    {
        using SnapshotFixture fixture = new();
        string high = Path.Combine(fixture.ModsRoot, "High");
        string hiddenDirectory = Directory.CreateDirectory(
            Path.Combine(high, "secret.mohidden")).FullName;
        File.WriteAllText(Path.Combine(hiddenDirectory, "escaped.txt"), "hidden", Encoding.UTF8);
        string customDirectory = Directory.CreateDirectory(
            Path.Combine(high, ".cache")).FullName;
        File.WriteAllText(Path.Combine(customDirectory, "cached.txt"), "hidden", Encoding.UTF8);
        File.WriteAllText(Path.Combine(high, "custom.gone"), "hidden", Encoding.UTF8);
        File.AppendAllText(
            fixture.InstanceIniPath,
            "skip_file_suffixes=.mohidden, .gone\nskip_directories=.git, .cache\n",
            Encoding.UTF8);

        Mo2SnapshotCaptureResult result = fixture.CreateCapture().Capture(fixture.Request);

        Assert.IsNotNull(result.Snapshot);
        Assert.IsFalse(result.Snapshot.LooseProviderChains.Any(chain =>
            chain.NormalizedRelativePath is "secret.mohidden/escaped.txt"
                or ".cache/cached.txt"
                or "custom.gone"));
        Assert.IsTrue(result.Snapshot.PhysicalInventory.Any(entry =>
            entry.RelativePath == "secret.mohidden"
            && entry.Disposition == PhysicalEntryDisposition.HiddenBySuffix));
        Assert.IsTrue(result.Snapshot.PhysicalInventory.Any(entry =>
            entry.RelativePath == ".cache"
            && entry.Disposition == PhysicalEntryDisposition.SkippedDirectory));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Security")]
    public void NestedJunctionIsRecordedAsAGapAndNeverTraversed()
    {
        using SnapshotFixture fixture = new();
        string protectedRoot = Path.Combine(
            Path.GetTempPath(),
            $"Infinium-Snapshot-Protected-{Guid.NewGuid():N}");
        Directory.CreateDirectory(protectedRoot);
        string canary = Path.Combine(protectedRoot, "must-not-enter-snapshot.txt");
        File.WriteAllText(canary, "protected", Encoding.UTF8);
        string junction = Path.Combine(fixture.ModsRoot, "High", "nested-junction");
        try
        {
            TestFileSystem.CreateJunctionOrInconclusive(junction, protectedRoot);

            Mo2SnapshotCaptureResult result =
                fixture.CreateCapture().Capture(fixture.Request);

            Assert.IsNotNull(result.Snapshot);
            Assert.IsTrue(result.Gaps.Any(gap =>
                gap.Code == "reparse-point-unsupported"
                && gap.Reason.Contains("nested-junction", StringComparison.Ordinal)));
            Assert.IsFalse(result.Snapshot.PhysicalInventory.Any(entry =>
                entry.RelativePath.Contains(
                    "must-not-enter-snapshot.txt",
                    StringComparison.Ordinal)));
            Assert.IsFalse(result.Snapshot.LooseProviderChains.Any(chain =>
                chain.NormalizedRelativePath.Contains(
                    "must-not-enter-snapshot.txt",
                    StringComparison.Ordinal)));
            Assert.AreEqual("protected", File.ReadAllText(canary, Encoding.UTF8));
        }
        finally
        {
            TestFileSystem.DeleteJunction(junction);
            Directory.Delete(protectedRoot, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Security")]
    [TestProperty("Category", "Fault")]
    public void NestedDirectoryReplacementWithJunctionCannotRedirectTraversal()
    {
        using SnapshotFixture fixture = new();
        string nested = Directory.CreateDirectory(
            Path.Combine(fixture.ModsRoot, "High", "nested-race")).FullName;
        File.WriteAllText(Path.Combine(nested, "safe.txt"), "safe", Encoding.UTF8);
        string protectedRoot = Path.Combine(
            Path.GetTempPath(),
            $"Infinium-Snapshot-Race-Protected-{Guid.NewGuid():N}");
        Directory.CreateDirectory(protectedRoot);
        string canary = Path.Combine(protectedRoot, "race-canary.txt");
        File.WriteAllText(canary, "protected", Encoding.UTF8);
        string displaced = $"{nested}-displaced";
        int replaced = 0;
        Action<string, string> replace = (rootName, relativePath) =>
        {
            if (rootName != "mods"
                || relativePath != "High/nested-race"
                || Interlocked.Exchange(ref replaced, 1) != 0)
            {
                return;
            }

            Directory.Move(nested, displaced);
            TestFileSystem.CreateJunctionOrInconclusive(nested, protectedRoot);
        };

        try
        {
            Mo2SnapshotCaptureResult result = fixture
                .CreateCapture(beforeEntryOpen: replace)
                .Capture(fixture.Request);

            Assert.AreEqual(1, replaced);
            Assert.AreEqual(SnapshotCaptureState.ChangedDuringCapture, result.State);
            Assert.IsNull(result.Snapshot);
            Assert.IsTrue(result.Gaps.Any(gap =>
                gap.Code == "reparse-point-unsupported"
                && gap.Reason.Contains("nested-race", StringComparison.Ordinal)));
            Assert.AreEqual("protected", File.ReadAllText(canary, Encoding.UTF8));
        }
        finally
        {
            TestFileSystem.DeleteJunction(nested);
            if (Directory.Exists(displaced))
            {
                Directory.Move(displaced, nested);
            }

            Directory.Delete(protectedRoot, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void Mo2StartingDuringCaptureInvalidatesTheAttempt()
    {
        using SnapshotFixture fixture = new();
        Mo2SnapshotCapture capture = fixture.CreateCapture(
            processProbe: new SequenceProcessProbe(false, true));

        Mo2SnapshotCaptureResult result = capture.Capture(fixture.Request);

        Assert.AreEqual(SnapshotCaptureState.ChangedDuringCapture, result.State);
        Assert.IsNull(result.Snapshot);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void ExecutableIdentityChangingDuringCaptureInvalidatesTheAttempt()
    {
        using SnapshotFixture fixture = new();
        Mo2SnapshotCapture capture = fixture.CreateCapture(
            admissionService: new ChangingAdmissionService());

        Mo2SnapshotCaptureResult result = capture.Capture(fixture.Request);

        Assert.AreEqual(SnapshotCaptureState.ChangedDuringCapture, result.State);
        Assert.IsNull(result.Snapshot);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Security")]
    public void UnqualifiedMapperRootIsNeverOpened()
    {
        using SnapshotFixture fixture = new();
        string missingRoot = Path.Combine(fixture.Root, "must-not-be-opened");
        string hash = new('c', 64);
        Mo2SnapshotCaptureRequest request = fixture.Request with
        {
            QualifiedMappings = [new QualifiedMapping("rejected", missingRoot, "", hash)],
            EnabledMapperSha256s = [hash],
        };

        Mo2SnapshotCaptureResult result = fixture.CreateCapture().Capture(request);

        Assert.AreEqual(SnapshotCaptureState.CompletedWithGaps, result.State);
        Assert.IsNotNull(result.Snapshot);
        Assert.IsTrue(result.Gaps.Any(gap => gap.Code == "unknown-or-unqualified-mapper"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void MissingRequiredProfileControlFailsWithoutSnapshot()
    {
        using SnapshotFixture fixture = new();
        File.Delete(Path.Combine(fixture.ProfilesRoot, "Chosen", "plugins.txt"));

        Mo2SnapshotCaptureResult result = fixture.CreateCapture().Capture(fixture.Request);

        Assert.AreEqual(SnapshotCaptureState.Failed, result.State);
        Assert.IsNull(result.Snapshot);
        Assert.IsTrue(result.Gaps.Any(gap => gap.Code == "required-control-file-missing"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Contract")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Contract")]
    public void RenamingPhysicalModDirectoryPreservesEntityIdentity()
    {
        using SnapshotFixture fixture = new();
        Mo2SnapshotCaptureResult first = fixture.CreateCapture().Capture(fixture.Request);
        LocalInstalledEntity before = first.Snapshot!.LocalInstalledEntities.Single(
            entity => entity.SourceHints.Any(hint => hint.RawValue == "42"));
        Directory.Move(
            Path.Combine(fixture.ModsRoot, "High"),
            Path.Combine(fixture.ModsRoot, "Renamed"));
        File.WriteAllText(fixture.ModListPath, "+Renamed\n+Low\n", Encoding.UTF8);

        Mo2SnapshotCaptureResult second = fixture.CreateCapture().Capture(fixture.Request);
        LocalInstalledEntity after = second.Snapshot!.LocalInstalledEntities.Single(
            entity => entity.SourceHints.Any(hint => hint.RawValue == "42"));

        Assert.AreEqual(before.EntityId, after.EntityId);
        Assert.AreNotEqual(before.PhysicalPath, after.PhysicalPath);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void MissingAndUnrecognizedExecutablesFailAdmissionWithoutFallback()
    {
        SupportedExecutableManifests manifests = new();

        ExecutableAdmission missing = manifests.AdmitSkyrim(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.exe"),
            SupportedRuntime());

        Assert.AreEqual(AdmissionState.Indeterminate, missing.State);
        Assert.AreEqual(SupportedExecutableManifests.SkyrimManifestId, missing.ManifestId);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void UnsupportedManagerFailsBeforeAnyPathResolution()
    {
        using SnapshotFixture fixture = new();
        Mo2SnapshotCaptureRequest request = fixture.Request with
        {
            Mo2ExecutablePath = Path.Combine(fixture.Root, "missing-manager.exe"),
            ManagerId = "vortex",
        };

        Mo2SnapshotCaptureResult result = fixture.CreateCapture().Capture(request);

        Assert.AreEqual(SnapshotCaptureState.Failed, result.State);
        Assert.IsNull(result.Snapshot);
        Assert.AreEqual("unsupported-manager", result.Gaps.Single().Code);
    }

    private sealed class SnapshotFixture : IDisposable
    {
        internal SnapshotFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), $"Infinium-Snapshot-{Guid.NewGuid():N}");
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
            string gamePluginDirectory =
                Directory.CreateDirectory(Path.Combine(Root, "plugins")).FullName;
            File.WriteAllText(
                Path.Combine(gamePluginDirectory, "game_skyrimse.dll"),
                "synthetic game plugin",
                Encoding.UTF8);
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
            IReadOnlySet<string>? qualifiedMapperHashes = null,
            IMo2ProcessProbe? processProbe = null,
            IExecutableAdmissionService? admissionService = null,
            Action<string, string>? beforeEntryOpen = null)
        {
            _ = Root;
            return new Mo2SnapshotCapture(
                admissionService ?? new AcceptingManifests(),
                processProbe ?? new FixedProcessProbe(processRunning),
                qualifiedMapperHashes
                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                mutation,
                beforeEntryOpen);
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class AcceptingManifests : IExecutableAdmissionService
    {
        public ExecutableAdmission AdmitMo2(string path)
        {
            return Accepted(SupportedExecutableManifests.Mo2ManifestId, Path.GetFileName(path));
        }

        public ExecutableAdmission AdmitSkyrimGamePlugin(string path)
        {
            return Accepted(
                SupportedExecutableManifests.SkyrimGamePluginManifestId,
                Path.GetFileName(path));
        }

        public ExecutableAdmission AdmitSkyrim(
            string path,
            RuntimeTargetContext context)
        {
            Assert.AreEqual(SupportedRuntime(), context);
            return Accepted(SupportedExecutableManifests.SkyrimManifestId, Path.GetFileName(path));
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

    private sealed class SequenceProcessProbe(params bool[] states) : IMo2ProcessProbe
    {
        private int index;

        public bool IsRunning(string exactExecutablePath)
        {
            Assert.IsTrue(Path.IsPathFullyQualified(exactExecutablePath));
            return states[Math.Min(index++, states.Length - 1)];
        }
    }

    private sealed class ChangingAdmissionService : IExecutableAdmissionService
    {
        private int mo2Calls;

        public ExecutableAdmission AdmitMo2(string path)
        {
            mo2Calls++;
            return Accepted(
                SupportedExecutableManifests.Mo2ManifestId,
                path,
                mo2Calls == 1 ? 'a' : 'b');
        }

        public ExecutableAdmission AdmitSkyrimGamePlugin(string path) =>
            Accepted(
                SupportedExecutableManifests.SkyrimGamePluginManifestId,
                path,
                'd');

        public ExecutableAdmission AdmitSkyrim(
            string path,
            RuntimeTargetContext context) =>
            Accepted(SupportedExecutableManifests.SkyrimManifestId, path, 'c');

        private static ExecutableAdmission Accepted(
            string manifestId,
            string path,
            char hashCharacter) =>
            new(
                AdmissionState.Accepted,
                manifestId,
                new ExecutableIdentity(
                    Path.GetFileName(path),
                    1,
                    new string(hashCharacter, 64),
                    "synthetic",
                    null,
                    null,
                    null,
                    "00000001:0000000000000001"),
                []);
    }

}
