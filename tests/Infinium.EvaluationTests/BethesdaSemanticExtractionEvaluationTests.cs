using Infinium.Bethesda;
using Infinium.Mo2;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaSemanticExtractionEvaluationTests
{
    private static readonly string[] ExpectedUnsupportedPopulations =
    [
        "record_family",
        "record_field",
        "localized_string_resolution",
        "archive_member_read",
        "automatic_environment_discovery",
    ];

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void Eval0052NpcFixtureProducesCanonicalWinnerAndAllowlistedFields()
    {
        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV"));

        Assert.IsTrue(
            result.State is BethesdaExtractionState.Completed or BethesdaExtractionState.CompletedWithGaps,
            string.Join("; ", result.Failures.Select(failure => $"{failure.Code}:{failure.Input}:{failure.Message}")));
        Assert.IsNotNull(result.Snapshot);
        Assert.AreEqual(7, result.Snapshot.Plugins.Count);
        Assert.AreEqual(2, result.Snapshot.Races.Count);
        Assert.IsTrue(result.Snapshot.Npcs.Count > 0);
        Assert.IsTrue(result.Snapshot.OverrideChains.Values.Any(chain => chain.Contributions.Count >= 3));
        Assert.IsTrue(result.Snapshot.OverrideChains.Values.Any(chain => chain.Winner.Deleted));
        Assert.IsTrue(result.Snapshot.OverrideChains.Values.Any(chain => chain.Contributions.Any(item => item.Compressed)));
        Assert.IsTrue(result.Snapshot.NpcContributions.Any(npc => npc.AiData is not null));
        Assert.IsTrue(result.Snapshot.NpcContributions.Any(npc => npc.Packages.Count >= 2));
        Assert.IsTrue(result.Snapshot.NpcContributions.Any(npc => npc.HeadParts.Count >= 2));
        Assert.IsTrue(result.Snapshot.Links.Any(link => link.Field == "RNAM" && link.State == BethesdaLinkState.Resolved));
        Assert.IsTrue(result.Snapshot.Taxonomy.Any(item => item.Code == "area.actors.ai-packages"));
        Assert.IsTrue(result.Snapshot.Taxonomy.Any(item => item.Code == "area.actors.appearance-identity"));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void Eval0052RefrFixtureProducesRelationsPlacementsAndDeletedWinner()
    {
        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create("BETH-REFR-DEV"));

        Assert.IsTrue(
            result.State is BethesdaExtractionState.Completed or BethesdaExtractionState.CompletedWithGaps,
            string.Join("; ", result.Failures.Select(failure => failure.Message)));
        Assert.IsNotNull(result.Snapshot);
        Assert.IsTrue(result.Snapshot.PlacedReferences.Count > 0);
        Assert.IsTrue(result.Snapshot.PlacedReferenceContributions.Any(reference => reference.LinkedReferences.Count >= 2));
        Assert.IsTrue(result.Snapshot.PlacedReferenceContributions.Any(reference => reference.LocationReference?.State == BethesdaLinkState.Resolved));
        Assert.IsTrue(result.Snapshot.PlacedReferenceContributions.Any(reference => reference.Owner?.State == BethesdaLinkState.Resolved));
        Assert.IsTrue(result.Snapshot.PlacedReferenceContributions.Any(reference => reference.Placement is not null));
        Assert.IsTrue(result.Snapshot.OverrideChains.Values.Any(chain => chain.Winner.Deleted));
        Assert.IsTrue(result.Snapshot.Taxonomy.Any(item => item.Code == "area.world.placed-objects-activation"));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void Eval0052LightFixtureRetainsFullAndLightOrigins()
    {
        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create("BETH-LIGHT-VAL"));

        Assert.IsTrue(
            result.State is BethesdaExtractionState.Completed or BethesdaExtractionState.CompletedWithGaps,
            string.Join("; ", result.Failures.Select(failure => failure.Message)));
        Assert.IsNotNull(result.Snapshot);
        Assert.IsTrue(result.Snapshot.Plugins.Any(plugin => plugin.MasterStyle == BethesdaMasterStyle.Light));
        Assert.IsTrue(result.Snapshot.Plugins.Any(plugin => plugin.MasterStyle == BethesdaMasterStyle.Full));
        Assert.IsTrue(result.Snapshot.Winners.Values.Any(winner => winner.Identity.OriginLocalId is >= 0x800 and <= 0xFFF));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void Eval0052FaceGenUsesQualifiedApplicabilityAndExplicitCoverageGaps()
    {
        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV"));
        BethesdaSemanticExtractionResult templateWinner = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.CreateSelected(
                "BETH-NPC-DEV",
                ["00-Pad.esm", "01-Actors.esm", "02-Behavior.esp", "03-Appearance.esp"],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["03-Appearance.esp"] = "inputs/mutations/master-order-reindexed/03-Appearance.esp",
                }));

        Assert.IsNotNull(result.Snapshot);
        Assert.IsNotNull(templateWinner.Snapshot);
        CollectionAssert.Contains(
            result.Snapshot.FaceGen.Select(fact => fact.Applicability).ToArray(),
            BethesdaFaceGenApplicability.CoverageGapDeletedWinner);
        CollectionAssert.Contains(
            templateWinner.Snapshot.FaceGen.Select(fact => fact.Applicability).ToArray(),
            BethesdaFaceGenApplicability.CoverageGapTemplateTraits);
        Assert.IsTrue(result.Gaps.Any(gap => gap.GapId.Contains(":facegen-deleted-", StringComparison.Ordinal)));
        Assert.IsTrue(templateWinner.Gaps.Any(gap => gap.GapId.Contains(":facegen-template-", StringComparison.Ordinal)));
        BethesdaFaceGenFact lightOrigin = result.Snapshot.FaceGen.Single(fact => fact.OriginPlugin == "04-LightActors.esl");
        Assert.IsTrue(lightOrigin.Mesh.NormalizedRelativePath.Contains("/04-lightactors.esl/00000800.nif", StringComparison.Ordinal));
        Assert.AreEqual(
            "05-LightWinner.esp",
            result.Snapshot.Winners.Values.Single(winner => winner.Identity.ParticipantId == lightOrigin.NpcParticipantId).SourcePlugin);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void Eval0052FaceGenLooseProvidersRetainWinnerPairsAndArchiveQualifiedAbsence()
    {
        BethesdaSemanticRequest source = BethesdaSemanticTestSnapshot.CreateSelected(
            "BETH-NPC-DEV",
            ["00-Pad.esm", "01-Actors.esm"]);
        BethesdaSemanticSnapshot baseline = new BethesdaSemanticExtractor().Extract(source).Snapshot!;
        BethesdaFaceGenFact target = baseline.FaceGen.Single(fact =>
            fact.OriginPlugin == "01-Actors.esm"
            && fact.OriginLocalId == 0x800);
        Assert.AreEqual(BethesdaFaceGenApplicability.Applicable, target.Applicability);

        BethesdaSemanticExtractionResult pair = new BethesdaSemanticExtractor().Extract(
            WithFaceGenAssets(source, target, includeMesh: true, includeTint: true, archivePopulationSupported: true));
        BethesdaFaceGenFact paired = pair.Snapshot!.FaceGen.Single(fact => fact.NpcParticipantId == target.NpcParticipantId);
        Assert.IsTrue(paired.Mesh.Present);
        Assert.IsTrue(paired.Tint.Present);
        Assert.IsTrue(paired.Mesh.ExactAbsenceKnown);
        Assert.IsTrue(paired.Tint.ExactAbsenceKnown);
        Assert.AreEqual(2, paired.Mesh.ProviderParticipantIds.Count);
        Assert.AreEqual(paired.Mesh.ProviderParticipantIds[^1], paired.Mesh.WinnerParticipantId);

        BethesdaSemanticExtractionResult partial = new BethesdaSemanticExtractor().Extract(
            WithFaceGenAssets(source, target, includeMesh: true, includeTint: false, archivePopulationSupported: false));
        BethesdaFaceGenFact partialFact = partial.Snapshot!.FaceGen.Single(fact => fact.NpcParticipantId == target.NpcParticipantId);
        Assert.IsTrue(partialFact.Mesh.Present);
        Assert.IsFalse(partialFact.Tint.Present);
        Assert.IsFalse(partialFact.Tint.ExactAbsenceKnown);
        Assert.IsTrue(partial.Gaps.Any(gap => gap.GapId == "gap:infinium-bethesda:facegen-archive-unknown"));

        BethesdaSemanticExtractionResult exactMissing = new BethesdaSemanticExtractor().Extract(
            WithFaceGenAssets(source, target, includeMesh: false, includeTint: false, archivePopulationSupported: true));
        BethesdaFaceGenFact missing = exactMissing.Snapshot!.FaceGen.Single(fact => fact.NpcParticipantId == target.NpcParticipantId);
        Assert.IsFalse(missing.Mesh.Present);
        Assert.IsFalse(missing.Tint.Present);
        Assert.IsTrue(missing.Mesh.ExactAbsenceKnown);
        Assert.IsTrue(missing.Tint.ExactAbsenceKnown);
        Assert.IsFalse(exactMissing.Gaps.Any(gap => gap.Population == "archive_member_read"));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    public void Eval0052RejectsEveryInvalidLightOriginAndReferenceBoundary()
    {
        Dictionary<string, string>[] replacements =
        [
            new(StringComparer.OrdinalIgnoreCase) { ["03-Consumer.esp"] = "inputs/mutations/Consumer-LightReferenceOutOfRange.esp" },
            new(StringComparer.OrdinalIgnoreCase) { ["02-Flagged.esp"] = "inputs/mutations/FlaggedEsp-AboveLightMaximum.esp" },
            new(StringComparer.OrdinalIgnoreCase) { ["02-Flagged.esp"] = "inputs/mutations/FlaggedEsp-BelowObjectRange.esp" },
            new(StringComparer.OrdinalIgnoreCase) { ["01-Native.esl"] = "inputs/mutations/Native-AboveLightMaximum.esl" },
            new(StringComparer.OrdinalIgnoreCase) { ["01-Native.esl"] = "inputs/mutations/Native-BelowObjectRange.esl" },
            new(StringComparer.OrdinalIgnoreCase) { ["01-Native.esl"] = "inputs/mutations/Native-HeaderFlagRemoved.esl" },
        ];

        foreach (Dictionary<string, string> replacement in replacements)
        {
            BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(
                BethesdaSemanticTestSnapshot.Create("BETH-LIGHT-VAL", replacement));
            Assert.AreEqual(BethesdaExtractionState.InvalidInput, result.State, replacement.Values.Single());
            Assert.IsNull(result.Snapshot, replacement.Values.Single());
        }

        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"infinium-esl-header-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            string fixtureRoot = TestRepository.PathFromRoot("test-data", "evaluation", "m1-semantic", "BETH-LIGHT-VAL");
            using System.Text.Json.JsonDocument receipt = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(
                Path.Combine(fixtureRoot, "inputs", "snapshot", "accepted-order.json")));
            List<(string Name, int Order, string Path, Infinium.Domain.Contracts.OpaqueId Entity)> plugins = [];
            foreach (System.Text.Json.JsonElement item in receipt.RootElement.GetProperty("plugin_order").EnumerateArray())
            {
                string name = item.GetProperty("file_name").GetString()!;
                string source = name == "01-Native.esl"
                    ? Path.Combine(fixtureRoot, "inputs", "mutations", "Native-HeaderFlagRemoved.esl")
                    : Path.GetFullPath(Path.Combine([fixtureRoot, .. item.GetProperty("artifact_id").GetString()!.Split('/')]));
                string target = Path.Combine(temporaryRoot, name);
                File.Copy(source, target);
                int order = item.GetProperty("load_order").GetInt32();
                plugins.Add((name, order, target, new Infinium.Domain.Contracts.OpaqueId($"fixture-provider-{order:D3}")));
            }

            BethesdaSemanticExtractionResult exactHeaderFailure = new BethesdaSemanticExtractor().Extract(
                BethesdaSemanticTestSnapshot.Create(plugins));
            Assert.AreEqual(BethesdaExtractionState.InvalidInput, exactHeaderFailure.State);
            Assert.HasCount(1, exactHeaderFailure.Failures);
            Assert.AreEqual("esl-header-flag-missing", exactHeaderFailure.Failures[0].Code);
            Assert.AreEqual("01-Native.esl", exactHeaderFailure.Failures[0].Input);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static BethesdaSemanticRequest WithFaceGenAssets(
        BethesdaSemanticRequest request,
        BethesdaFaceGenFact target,
        bool includeMesh,
        bool includeTint,
        bool archivePopulationSupported)
    {
        Mo2InstallationSnapshot snapshot = request.AcceptedSnapshot.Snapshot!;
        LocalInstalledEntity[] providers = snapshot.LocalInstalledEntities.Take(2).ToArray();
        Assert.AreEqual(2, providers.Length);
        List<LooseProviderChain> chains = [.. snapshot.LooseProviderChains];
        Add(target.Mesh.NormalizedRelativePath, includeMesh);
        Add(target.Tint.NormalizedRelativePath, includeTint);
        return request with
        {
            AcceptedSnapshot = request.AcceptedSnapshot with
            {
                Snapshot = snapshot with
                {
                    LooseProviderChains = chains,
                    ArchiveMemberPopulationSupported = archivePopulationSupported,
                },
            },
        };

        void Add(string normalizedPath, bool included)
        {
            if (!included)
            {
                return;
            }

            LooseProvider first = new(
                providers[0].EntityId,
                providers[0].Kind,
                providers[0].PhysicalPath,
                100);
            LooseProvider winner = new(
                providers[1].EntityId,
                providers[1].Kind,
                providers[1].PhysicalPath,
                101);
            chains.Add(new LooseProviderChain(normalizedPath, [first, winner], winner));
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    public void Eval0052MalformedPopulationFailsAtomically()
    {
        string mutationRoot = TestRepository.PathFromRoot(
            "test-data", "evaluation", "m1-semantic", "BETH-MALFORMED-VAL", "inputs", "mutations");
        string[] malformedPaths = Directory.EnumerateFiles(mutationRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".esm" or ".esp" or ".esl")
            .Where(path => !Path.GetFileName(path).StartsWith("ChangedDuringRead-", StringComparison.Ordinal))
            .ToArray();
        Assert.IsTrue(malformedPaths.Length >= 15);

        foreach (string path in malformedPaths)
        {
            BethesdaSemanticRequest request = BethesdaSemanticTestSnapshot.Create(
            [
                (Path.GetFileName(path), 0, path, new Infinium.Domain.Contracts.OpaqueId("malformed-provider")),
            ]);
            BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(request);
            Assert.AreEqual(BethesdaExtractionState.InvalidInput, result.State, path);
            Assert.IsNull(result.Snapshot, path);
            Assert.AreEqual(1, result.Failures.Count, path);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void Eval0052UnsupportedPopulationKeepsFiveLabeledDenominators()
    {
        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create(
                "BETH-UNSUPPORTED-VAL",
                unsupportedCapabilities:
                [
                    BethesdaUnsupportedCapability.LocalizedStringResolution,
                    BethesdaUnsupportedCapability.ArchiveMemberRead,
                    BethesdaUnsupportedCapability.AutomaticEnvironmentDiscovery,
                ]));

        Assert.AreEqual(
            BethesdaExtractionState.CompletedWithGaps,
            result.State,
            string.Join("; ", result.Failures.Select(failure => $"{failure.Code}: {failure.Message}")));
        Assert.IsNotNull(result.Snapshot);
        CollectionAssert.IsSubsetOf(
            ExpectedUnsupportedPopulations,
            result.Gaps.Select(gap => gap.Population).Distinct().ToArray());
        Assert.IsTrue(result.Snapshot.Taxonomy.Any(item =>
            item.Axis == "affected-game-system-or-content-area" && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Unsupported));
        Assert.IsTrue(result.Snapshot.Taxonomy.Any(item =>
            item.Axis == "consequence-type" && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Unknown));
        Assert.IsTrue(result.Snapshot.Taxonomy.Any(item =>
            item.Axis == "effect-extent" && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.NotApplicable));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Contract")]
    public void Eval0052ProducesNoLaterSliceConclusionCollections()
    {
        Type snapshotType = typeof(BethesdaSemanticSnapshot);
        string[] forbidden = ["Candidate", "Hypothesis", "Finding", "Case", "Recommendation"];
        Assert.IsFalse(snapshotType.GetProperties().Any(property =>
            forbidden.Any(token => property.Name.Contains(token, StringComparison.OrdinalIgnoreCase))));
    }
}
