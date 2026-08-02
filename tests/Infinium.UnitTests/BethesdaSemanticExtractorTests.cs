using Infinium.Bethesda;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaSemanticExtractorTests
{
    private static readonly string[] ExpectedRepeatedPackages =
    [
        "00000831:01-Actors.esm",
        "00000830:01-Actors.esm",
    ];

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void DependencyClosureChangesWhenOnePluginByteChanges()
    {
        BethesdaSemanticExtractionResult baseline = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV"));
        BethesdaSemanticExtractionResult mutation = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create(
                "BETH-NPC-DEV",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["02-Behavior.esp"] = "inputs/mutations/one-byte-aidt/02-Behavior.esp",
                }));

        Assert.IsNotNull(baseline.Snapshot);
        Assert.IsNotNull(mutation.Snapshot);
        Assert.AreNotEqual(
            baseline.Snapshot.DependencyFingerprint,
            mutation.Snapshot.DependencyFingerprint);
        Assert.AreNotEqual(
            baseline.Snapshot.Plugins.Single(plugin => plugin.PluginName == "02-Behavior.esp").Sha256,
            mutation.Snapshot.Plugins.Single(plugin => plugin.PluginName == "02-Behavior.esp").Sha256);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CanonicalParticipantSurvivesCompressionAndRecordReordering()
    {
        BethesdaSemanticExtractionResult baseline = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV"));
        BethesdaSemanticExtractionResult uncompressed = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create(
                "BETH-NPC-DEV",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["03-Appearance.esp"] = "inputs/mutations/uncompressed/03-Appearance.esp",
                }));
        BethesdaSemanticExtractionResult reordered = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create(
                "BETH-NPC-DEV",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["01-Actors.esm"] = "inputs/mutations/record-order/01-Actors.esm",
                }));

        Assert.IsNotNull(baseline.Snapshot);
        Assert.IsNotNull(uncompressed.Snapshot);
        Assert.IsNotNull(reordered.Snapshot);
        CollectionAssert.AreEquivalent(
            baseline.Snapshot.Winners.Keys.ToArray(),
            uncompressed.Snapshot.Winners.Keys.ToArray());
        CollectionAssert.AreEquivalent(
            baseline.Snapshot.Winners.Keys.ToArray(),
            reordered.Snapshot.Winners.Keys.ToArray());
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void MasterTableReindexingPreservesCanonicalLinksOnlyWhenBytesAreReindexed()
    {
        BethesdaSemanticExtractionResult reindexed = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create(
                "BETH-NPC-DEV",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["03-Appearance.esp"] = "inputs/mutations/master-order-reindexed/03-Appearance.esp",
                }));
        BethesdaSemanticExtractionResult unreindexed = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create(
                "BETH-NPC-DEV",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["03-Appearance.esp"] = "inputs/mutations/master-order-unreindexed/03-Appearance.esp",
                }));

        Assert.IsNotNull(reindexed.Snapshot);
        Assert.IsNotNull(unreindexed.Snapshot);
        string[] reindexedTargets = AppearanceTargets(reindexed.Snapshot);
        string[] unreindexedTargets = AppearanceTargets(unreindexed.Snapshot);
        CollectionAssert.Contains(reindexedTargets, "00000850:01-Actors.esm");
        CollectionAssert.Contains(reindexedTargets, "00000810:01-Actors.esm");
        CollectionAssert.Contains(unreindexedTargets, "00000850:00-Pad.esm");
        CollectionAssert.Contains(unreindexedTargets, "00000810:00-Pad.esm");
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void RepeatedSubrecordOrderIsRetainedAsSemanticEvidence()
    {
        BethesdaSemanticExtractionResult baseline = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV"));
        string root = Path.Combine(Path.GetTempPath(), $"infinium-bethesda-order-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string packagePath = Path.Combine(root, "02-Behavior.esp");
            string headPartPath = Path.Combine(root, "03-Appearance.esp");
            File.Copy(
                TestRepository.PathFromRoot("test-data", "evaluation", "m1-semantic", "BETH-NPC-DEV", "inputs", "mutations", "Behavior-RepeatedPKIDOrder.esp"),
                packagePath);
            File.Copy(
                TestRepository.PathFromRoot("test-data", "evaluation", "m1-semantic", "BETH-NPC-DEV", "inputs", "mutations", "Appearance-RepeatedPNAMOrder.esp"),
                headPartPath);
            BethesdaSemanticExtractionResult packages = new BethesdaSemanticExtractor().Extract(
                BethesdaSemanticTestSnapshot.Create(
                    "BETH-NPC-DEV",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["02-Behavior.esp"] = packagePath,
                    }));
            BethesdaSemanticExtractionResult headParts = new BethesdaSemanticExtractor().Extract(
                BethesdaSemanticTestSnapshot.Create(
                    "BETH-NPC-DEV",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["03-Appearance.esp"] = headPartPath,
                    }));

            Assert.IsNotNull(baseline.Snapshot);
            Assert.IsNotNull(packages.Snapshot, string.Join("; ", packages.Failures.Select(failure => $"{failure.Code}: {failure.Message}")));
            Assert.IsNotNull(headParts.Snapshot, string.Join("; ", headParts.Failures.Select(failure => $"{failure.Code}: {failure.Message}")));
            string[] changedPackages = packages.Snapshot.NpcContributions
                .Single(npc => npc.Contribution.SourcePlugin == "02-Behavior.esp")
                .Packages.Select(link => link.TargetFormKey!).ToArray();
            BethesdaLinkFact[] changedHeadParts = headParts.Snapshot.NpcContributions
                .Single(npc => npc.Contribution.SourcePlugin == "03-Appearance.esp")
                .HeadParts.ToArray();
            CollectionAssert.AreEqual(
                ExpectedRepeatedPackages,
                changedPackages);
            Assert.AreEqual(3, changedHeadParts.Length);
            Assert.AreEqual(BethesdaLinkState.Null, changedHeadParts[1].State);
            CollectionAssert.AreEqual(
                Enumerable.Range(0, changedPackages.Length).ToArray(),
                packages.Snapshot.NpcContributions
                    .Single(npc => npc.Contribution.SourcePlugin == "02-Behavior.esp")
                    .Packages.Select(link => link.Ordinal).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void NonAcceptedSnapshotCannotProducePartialAuthority()
    {
        BethesdaSemanticRequest accepted = BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV");
        BethesdaSemanticRequest rejected = accepted with
        {
            AcceptedSnapshot = accepted.AcceptedSnapshot with
            {
                State = Infinium.Mo2.SnapshotCaptureState.Failed,
            },
        };

        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(rejected);

        Assert.AreEqual(BethesdaExtractionState.InvalidInput, result.State);
        Assert.IsNull(result.Snapshot);
        Assert.AreEqual("snapshot-not-accepted", result.Failures.Single().Code);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void UnsupportedCapabilityRequestMustUseUniqueClosedValues()
    {
        BethesdaSemanticRequest source = BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV");
        BethesdaSemanticExtractionResult duplicate = new BethesdaSemanticExtractor().Extract(source with
        {
            RequestedUnsupportedCapabilities =
            [
                BethesdaUnsupportedCapability.ArchiveMemberRead,
                BethesdaUnsupportedCapability.ArchiveMemberRead,
            ],
        });
        BethesdaSemanticExtractionResult undefined = new BethesdaSemanticExtractor().Extract(source with
        {
            RequestedUnsupportedCapabilities = [(BethesdaUnsupportedCapability)99],
        });

        Assert.AreEqual(BethesdaExtractionState.InvalidInput, duplicate.State);
        Assert.IsNull(duplicate.Snapshot);
        Assert.AreEqual("unsupported-capability-request-invalid", duplicate.Failures.Single().Code);
        Assert.AreEqual(BethesdaExtractionState.InvalidInput, undefined.State);
        Assert.IsNull(undefined.Snapshot);
        Assert.AreEqual("unsupported-capability-request-invalid", undefined.Failures.Single().Code);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void ValidCompressedRecordPopulationIsRejectedBeforeExceedingAggregateAuthorityBound()
    {
        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor(maximumDecompressedBytes: 1).Extract(
            BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV"));

        Assert.AreEqual(BethesdaExtractionState.InvalidInput, result.State);
        Assert.IsNull(result.Snapshot);
        Assert.AreEqual("decompressed-bytes-over-limit", result.Failures.Single().Code);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [DataRow(true, BethesdaLinkState.Resolved, true, false, false, BethesdaFaceGenApplicability.CoverageGapDeletedWinner)]
    [DataRow(false, BethesdaLinkState.Unresolved, null, false, false, BethesdaFaceGenApplicability.CoverageGapUnresolvedRace)]
    [DataRow(false, BethesdaLinkState.Resolved, false, false, false, BethesdaFaceGenApplicability.InapplicableRaceWithoutFaceGenHead)]
    [DataRow(false, BethesdaLinkState.Resolved, true, true, false, BethesdaFaceGenApplicability.CoverageGapTemplateSource)]
    [DataRow(false, BethesdaLinkState.Resolved, true, false, true, BethesdaFaceGenApplicability.CoverageGapTemplateTraits)]
    [DataRow(false, BethesdaLinkState.Resolved, true, false, false, BethesdaFaceGenApplicability.Applicable)]
    public void FaceGenApplicabilityUsesClosedPrecedenceMatrix(
        bool deleted,
        BethesdaLinkState raceState,
        bool? raceFaceGenHead,
        bool usesTemplate,
        bool templatesTraits,
        BethesdaFaceGenApplicability expected)
    {
        Assert.AreEqual(
            expected,
            BethesdaSemanticExtractor.DetermineFaceGenApplicability(
                deleted,
                raceState,
                raceFaceGenHead,
                usesTemplate,
                templatesTraits));
    }

    private static string[] AppearanceTargets(BethesdaSemanticSnapshot snapshot) =>
        snapshot.NpcContributions
            .Where(npc => npc.Contribution.SourcePlugin == "03-Appearance.esp")
            .SelectMany(npc => new BethesdaLinkFact?[] { npc.Template, npc.Race }
                .Concat(npc.HeadParts)
                .Append(npc.HairColor))
            .Where(link => link is not null)
            .Select(link => link!)
            .Where(link => link.TargetFormKey is not null)
            .Select(link => link.TargetFormKey!)
            .ToArray();
}
