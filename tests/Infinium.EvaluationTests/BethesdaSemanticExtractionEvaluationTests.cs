using Infinium.Bethesda;
using Infinium.Mo2;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaSemanticExtractionEvaluationTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
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
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
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
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
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
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
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
            BethesdaFaceGenApplicability.NotApplicableDeletedWinner);
        CollectionAssert.Contains(
            templateWinner.Snapshot.FaceGen.Select(fact => fact.Applicability).ToArray(),
            BethesdaFaceGenApplicability.NotApplicableTemplateTraits);
        Assert.IsFalse(result.Gaps.Any(gap => gap.Population == "face-gen-applicability:deleted"));
        Assert.IsFalse(templateWinner.Gaps.Any(gap => gap.Population == "face-gen-applicability:template"));
        BethesdaFaceGenFact lightOrigin = result.Snapshot.FaceGen.Single(fact => fact.OriginPlugin == "04-LightActors.esl");
        Assert.IsTrue(lightOrigin.Mesh.NormalizedRelativePath.Contains("/04-lightactors.esl/00000800.nif", StringComparison.Ordinal));
        Assert.AreEqual(
            "05-LightWinner.esp",
            result.Snapshot.Winners.Values.Single(winner => winner.Identity.ParticipantId == lightOrigin.NpcParticipantId).SourcePlugin);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
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
        Assert.AreEqual(BethesdaAssetAvailability.Present, paired.Mesh.Availability);
        Assert.AreEqual(BethesdaAssetAvailability.Present, paired.Tint.Availability);
        Assert.IsFalse(paired.Mesh.ExactAbsenceKnown);
        Assert.IsFalse(paired.Tint.ExactAbsenceKnown);
        Assert.AreEqual(1, paired.Mesh.ProviderParticipantIds.Count);
        Assert.AreEqual(2, paired.Tint.ProviderParticipantIds.Count);
        Assert.AreEqual(paired.Mesh.ProviderParticipantIds[^1], paired.Mesh.WinnerParticipantId);
        Assert.AreEqual(2, pair.Snapshot.Taxonomy.Count(item =>
            item.SubjectParticipantId.EndsWith(paired.Mesh.NormalizedRelativePath, StringComparison.Ordinal)));
        Assert.AreEqual(2, pair.Snapshot.Taxonomy.Count(item =>
            item.SubjectParticipantId.EndsWith(paired.Tint.NormalizedRelativePath, StringComparison.Ordinal)));

        BethesdaSemanticExtractionResult partial = new BethesdaSemanticExtractor().Extract(
            WithFaceGenAssets(source, target, includeMesh: true, includeTint: false, archivePopulationSupported: false));
        BethesdaFaceGenFact partialFact = partial.Snapshot!.FaceGen.Single(fact => fact.NpcParticipantId == target.NpcParticipantId);
        Assert.IsTrue(partialFact.Mesh.Present);
        Assert.IsFalse(partialFact.Tint.Present);
        Assert.AreEqual(BethesdaAssetAvailability.Unknown, partialFact.Tint.Availability);
        Assert.IsFalse(partialFact.Tint.ExactAbsenceKnown);
        Assert.IsTrue(partial.Gaps.Any(gap => gap.Population == "face-gen-archive-assets"));

        BethesdaSemanticExtractionResult exactMissing = new BethesdaSemanticExtractor().Extract(
            WithFaceGenAssets(source, target, includeMesh: false, includeTint: false, archivePopulationSupported: true));
        BethesdaFaceGenFact missing = exactMissing.Snapshot!.FaceGen.Single(fact => fact.NpcParticipantId == target.NpcParticipantId);
        Assert.IsFalse(missing.Mesh.Present);
        Assert.IsFalse(missing.Tint.Present);
        Assert.AreEqual(BethesdaAssetAvailability.Unknown, missing.Mesh.Availability);
        Assert.AreEqual(BethesdaAssetAvailability.Unknown, missing.Tint.Availability);
        Assert.IsFalse(missing.Mesh.ExactAbsenceKnown);
        Assert.IsFalse(missing.Tint.ExactAbsenceKnown);
        Assert.IsTrue(exactMissing.Snapshot.FaceGen.SelectMany(fact => new[] { fact.Mesh, fact.Tint })
            .All(asset => asset.Availability != BethesdaAssetAvailability.Absent));
        Assert.IsFalse(exactMissing.Gaps.Any(gap => gap.Population == "face-gen-archive-assets"));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Fault")]
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
            string fixtureRoot = TestRepository.PathFromRoot("fixtures", "public", "bethesda", "BETH-LIGHT-VAL");
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
            LooseProvider[] declared = normalizedPath.EndsWith(".nif", StringComparison.Ordinal)
                ? [winner]
                : [first, winner];
            chains.Add(new LooseProviderChain(normalizedPath, declared, winner));
        }
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Fault")]
    public void Eval0052MalformedPopulationFailsAtomically()
    {
        string mutationRoot = TestRepository.PathFromRoot(
            "fixtures", "public", "bethesda", "BETH-MALFORMED-VAL", "inputs", "mutations");
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
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
    public void Eval0052UnsupportedPopulationKeepsLayeredGapsAndFixedCoverageRows()
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
        CollectionAssert.AreEqual(
            BethesdaSemanticContract.CoveragePopulations.ToArray(),
            result.Snapshot.Coverage.Select(row => row.Population).ToArray());
        Assert.IsTrue(result.Gaps.Any(gap => gap.Population.StartsWith("unsupported-records:", StringComparison.Ordinal)));
        Assert.IsTrue(result.Gaps.Any(gap => gap.Population.StartsWith("unsupported-fields:", StringComparison.Ordinal)));
        Assert.IsTrue(result.Gaps.Any(gap => gap.Population == "localized-strings"));
        Assert.IsTrue(result.Gaps.Any(gap => gap.Population == "automatic-environment-discovery"));
        Assert.IsTrue(result.Snapshot.Taxonomy.Any(item =>
            item.Axis == "affected-game-system-or-content-area" && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Unsupported));
        Assert.IsTrue(result.Snapshot.Taxonomy.Any(item =>
            item.Axis == "consequence-type" && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Unknown));
        Assert.IsFalse(result.Snapshot.Taxonomy.Any(item => item.SubjectType == "provider-topology"));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
    public void Eval0052FixedCoverageRetainsCompletedZeroDenominatorRows()
    {
        BethesdaSemanticSnapshot snapshot = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.CreateSelected("BETH-NPC-DEV", ["00-Pad.esm"])).Snapshot!;

        CollectionAssert.AreEqual(
            BethesdaSemanticContract.CoveragePopulations.ToArray(),
            snapshot.Coverage.Select(row => row.Population).ToArray());
        BethesdaCoveragePopulation[] zeroRows = snapshot.Coverage
            .Where(row => row.Denominator == 0)
            .ToArray();
        Assert.IsGreaterThanOrEqualTo(6, zeroRows.Length);
        Assert.IsTrue(zeroRows.All(row => row.Completed == 0
            && row.State == Infinium.Domain.Contracts.CoverageState.Completed));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Contract")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Contract")]
    public void Eval0052ProducesNoLaterSliceConclusionCollections()
    {
        Type snapshotType = typeof(BethesdaSemanticSnapshot);
        string[] forbidden = ["Candidate", "Hypothesis", "Finding", "Case", "Recommendation"];
        Assert.IsFalse(snapshotType.GetProperties().Any(property =>
            forbidden.Any(token => property.Name.Contains(token, StringComparison.OrdinalIgnoreCase))));
    }
}
