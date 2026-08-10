using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.EvaluatorV2;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Evaluation")]
[TestProperty("Category", "Evaluation")]
public sealed class EvaluatorV2PublicProtocolTests
{
    [TestMethod]
    public void PublicCalibrationDiscriminatesEveryDeclaredMutation()
    {
        CalibrationResults result = CalibrationSuite.Run();
        CalibrationResults repeated = CalibrationSuite.Run();

        Assert.IsTrue(result.Passed);
        Assert.AreEqual(EvaluatorProtocol.Serialize(result), EvaluatorProtocol.Serialize(repeated));
        Assert.IsGreaterThanOrEqualTo(17, result.Cases.Count);
        Assert.IsTrue(result.Cases.All(item => item.Passed));
        Assert.AreEqual("PASS", result.Cases.Single(item => item.CaseId == "known-correct").ActualTerminal);
        Assert.IsTrue(result.Cases
            .Where(item => item.CaseId is "malformed-oracle" or "tampered-oracle-identity" or "candidate-dependency-drift")
            .All(item => item.ActualTerminal == "EVALUATOR_ERROR"));
        Assert.AreEqual("FAIL", result.Cases.Single(item => item.CaseId == "candidate-output-contract").ActualTerminal);
        Assert.AreEqual("FAIL", result.Cases.Single(item => item.CaseId == "candidate-execution-failure").ActualTerminal);
    }

    [TestMethod]
    public void TypedFactValidationFollowsDeclaredSemanticTypeThroughTheScorer()
    {
        CalibrationResults result = CalibrationSuite.Run();
        string[] passing =
        [
            "integral-token-semantic-number-boundary",
            "semantic-number-equivalent-token-shapes",
            "semantic-number-non-integral",
            "semantic-integer-decimal-token",
            "semantic-integer-exponent-token",
            "oracle-semantic-integer-decimal-token",
            "oracle-semantic-integer-exponent-token",
            "prepared-integral-token-semantic-number",
        ];
        Assert.IsTrue(passing.All(id => result.Cases.Single(item => item.CaseId == id).ActualTerminal == "PASS"));

        string[] contractFailures =
        [
            "semantic-integer-non-integral-rejected",
            "semantic-integer-out-of-range-rejected",
            "semantic-number-string-rejected",
            "semantic-number-boolean-rejected",
            "semantic-number-null-rejected",
            "semantic-number-object-rejected",
            "semantic-number-array-rejected",
        ];
        Assert.IsTrue(contractFailures.All(id =>
        {
            CalibrationCaseResult item = result.Cases.Single(candidate => candidate.CaseId == id);
            return item.ActualTerminal == "FAIL"
                && item.ObservedFailureCategories!.SequenceEqual(["candidate_output_contract"], StringComparer.Ordinal);
        }));

        CalibrationCaseResult semanticMismatch = result.Cases.Single(item => item.CaseId == "semantic-type-mismatch-not-equal");
        Assert.AreEqual("FAIL", semanticMismatch.ActualTerminal);
        Assert.HasCount(1, semanticMismatch.ObservedFailureCategories!);
        Assert.AreEqual("placement", semanticMismatch.ObservedFailureCategories![0]);
    }

    [TestMethod]
    public void CanonicalizerUsesIdFirstFormKeysAndNormalizesEmbeddedIdentities()
    {
        Assert.AreEqual("00000042:base.esm", SemanticCanonicalizer.CanonicalFormKey("00000042:Base.esm"));
        Assert.AreEqual("record:00000801:light.esl", SemanticCanonicalizer.CanonicalIdentity("record:00000801:Light.esl"));
        Assert.ThrowsExactly<CandidateOutputException>(() => SemanticCanonicalizer.CanonicalFormKey("Base.esm:00000042"));
    }

    [TestMethod]
    public void OracleAuthorityMatrixAndProjectorDeclareTheSameActiveFactFamilies()
    {
        string matrix = File.ReadAllText(TestRepository.PathFromRoot(
            "docs", "evaluation", "m1-slice4-heldout-oracle-authority-matrix.md"));
        const string marker = "<!-- active-fact-families: ";
        int start = matrix.IndexOf(marker, StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, start);
        start += marker.Length;
        int end = matrix.IndexOf(" -->", start, StringComparison.Ordinal);
        Assert.IsGreaterThan(start, end);
        string[] declared = matrix[start..end].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        CollectionAssert.AreEqual(
            SemanticCanonicalizer.IncludedFactFamilies.Order(StringComparer.Ordinal).ToArray(),
            declared.Order(StringComparer.Ordinal).ToArray());
    }

    [TestMethod]
    public void ProjectionIgnoresInternalAnswerIdsAndPreservesOnlyAuthorizedBoundaries()
    {
        ExecutionManifest manifest = CreatePublicManifest("BETH-NPC-DEV");
        JsonElement raw = ReflectionCandidateAdapter.ExecuteRawForTests(manifest);
        string baseline = ProjectedJson(JsonNode.Parse(raw.GetRawText())!);

        foreach (Action<JsonObject> mutation in new Action<JsonObject>[]
                 {
                     root => FirstTaxonomy(root)["assignment_id"] = "taxonomy:changed-internal-id",
                     root => FirstTaxonomy(root)["analyzer_or_adjudicator_id"] = "analyzer:changed-internal-id",
                     root => FirstTaxonomy(root)["evidence_fields"] = new JsonArray("evidence:changed-internal-id"),
                     root => FirstAiData(root)["aggression"] = 255,
                 })
        {
            JsonObject changed = JsonNode.Parse(raw.GetRawText())!.AsObject();
            mutation(changed);
            Assert.AreEqual(baseline, ProjectedJson(changed));
        }

        JsonObject removedAi = JsonNode.Parse(raw.GetRawText())!.AsObject();
        JsonObject npc = removedAi["snapshot"]!["npc_contributions"]!.AsArray()
            .Select(item => item!.AsObject())
            .First(item => item["ai_data"] is not null);
        npc["ai_data"] = null;
        Assert.AreNotEqual(baseline, ProjectedJson(removedAi));

        JsonObject failedA = JsonNode.Parse(raw.GetRawText())!.AsObject();
        failedA["snapshot"] = null;
        failedA["failures"] = JsonNode.Parse("[{\"code\":\"first-internal-code\"}]");
        JsonObject failedB = JsonNode.Parse(failedA.ToJsonString())!.AsObject();
        failedB["failures"]![0]!["code"] = "second-internal-code";
        Assert.AreEqual(ProjectedJson(failedA), ProjectedJson(failedB));
        Assert.AreNotEqual(baseline, ProjectedJson(failedA));

        JsonObject duplicateTaxonomy = JsonNode.Parse(raw.GetRawText())!.AsObject();
        JsonObject duplicate = JsonNode.Parse(FirstTaxonomy(duplicateTaxonomy).ToJsonString())!.AsObject();
        duplicate["assignment_id"] = "taxonomy:distinct-product-id";
        duplicateTaxonomy["snapshot"]!["taxonomy"]!.AsArray().Add(duplicate);
        Assert.ThrowsExactly<CandidateOutputException>(() => ProjectedJson(duplicateTaxonomy));
    }

    [TestMethod]
    [TestCategory("Security")]
    public void ResultWriterRejectsEscapeAndOverwrite()
    {
        string root = Path.Combine(Path.GetTempPath(), $"infinium-v2-writer-{Guid.NewGuid():N}");
        try
        {
            CalibrationResults result = CalibrationSuite.Run();
            Assert.ThrowsExactly<ResultWriteException>(() => EvaluatorScorer.WriteSingleResult(
                root,
                "../escape.json",
                result,
                "calibration-results.v4.schema.json"));
            Assert.IsFalse(Directory.Exists(root));
            EvaluatorScorer.WriteSingleResult(
                root,
                "result.json",
                result,
                "calibration-results.v4.schema.json");
            Assert.ThrowsExactly<ResultWriteException>(() => EvaluatorScorer.WriteSingleResult(
                root,
                "result.json",
                result,
                "calibration-results.v4.schema.json"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Security")]
    public void ResultWriterRejectsReparsePointAncestorsWhenAvailable()
    {
        string root = Path.Combine(Path.GetTempPath(), $"infinium-v2-alias-{Guid.NewGuid():N}");
        string target = Path.Combine(root, "target");
        string alias = Path.Combine(root, "alias");
        Directory.CreateDirectory(target);
        try
        {
            try
            {
                Directory.CreateSymbolicLink(alias, target);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                Assert.Inconclusive("Directory symbolic links are unavailable on this host.");
                return;
            }

            CalibrationResults result = CalibrationSuite.Run();
            Assert.ThrowsExactly<ResultWriteException>(() => EvaluatorScorer.WriteSingleResult(
                Path.Combine(alias, "result"),
                "result.json",
                result,
                "calibration-results.v4.schema.json"));
        }
        finally
        {
            if (Directory.Exists(alias))
            {
                Directory.Delete(alias);
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void PassAttestationRetainsAndValidatesRequiredNullFailureStage()
    {
        string root = Path.Combine(Path.GetTempPath(), $"infinium-v2-pass-{Guid.NewGuid():N}");
        try
        {
            SanitizedResult result = new(
                EvaluatorProtocol.SanitizedSchema,
                EvaluatorProtocol.ProtocolId,
                EvaluatorProtocol.ProjectionId,
                EvaluatorProtocol.ProjectionVersion,
                new('a', 40),
                3,
                new('b', 64),
                new('c', 40),
                new('d', 64),
                EvaluatorProtocol.ScorerId,
                EvaluatorProtocol.ScorerVersion,
                EvaluatorProtocol.AdapterId,
                EvaluatorProtocol.AdapterVersion,
                "public-calibration",
                "1.0.0",
                new('e', 64),
                "PASS",
                null,
                new AssertionCounts(1, 1, 0),
                new MemberCounts(1, 1, 0, 0),
                [],
                "clean");

            EvaluatorScorer.WriteSingleResult(
                root,
                "sanitized-result.json",
                result,
                "sanitized-result.v4.schema.json");

            string json = File.ReadAllText(Path.Combine(root, "sanitized-result.json"));
            StringAssert.Contains(json, "\"failure_stage\": null", StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Security")]
    public void ProductionSourcesContainNoEvaluatorFixtureOrPartitionPolicy()
    {
        string sourceRoot = TestRepository.PathFromRoot("src");
        string[] forbidden = ["BETH-", "FixturePartition", "FixturePackage", "held-out", "held_out"];
        string[] violations = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => forbidden.Any(token => File.ReadAllText(path).Contains(token, StringComparison.OrdinalIgnoreCase)))
            .Select(path => Path.GetRelativePath(sourceRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(0, violations);
    }

    [TestMethod]
    [TestCategory("Security")]
    public void EvaluatorIdentityBindsExecutingRootDependenciesAndProtocolBytes()
    {
        EvaluatorFileIdentity[] files = EvaluatorScorer.RequiredEvaluatorFiles.Select(relative =>
        {
            ArtifactIdentity identity = EvaluatorProtocol.Identity(Path.Combine(
                AppContext.BaseDirectory,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            return new EvaluatorFileIdentity(relative, identity.ByteLength, identity.Sha256);
        }).ToArray();
        EvaluatorIdentity identity = new(
            new('a', 40),
            EvaluatorProtocol.ProtocolId,
            EvaluatorProtocol.ScorerId,
            EvaluatorProtocol.ScorerVersion,
            EvaluatorProtocol.AdapterId,
            EvaluatorProtocol.AdapterVersion,
            EvaluatorProtocol.ProjectionId,
            EvaluatorProtocol.ProjectionVersion,
            AppContext.BaseDirectory,
            files);

        EvaluatorScorer.ValidateEvaluatorIdentityForTests(identity);
        Assert.ThrowsExactly<InvalidDataException>(() => EvaluatorScorer.ValidateEvaluatorIdentityForTests(
            identity with { Root = Path.GetTempPath() }));
        Assert.ThrowsExactly<InvalidDataException>(() => EvaluatorScorer.ValidateEvaluatorIdentityForTests(
            identity with { Files = files.Where(file => file.RelativePath != "Infinium.Mo2.dll").ToArray() }));
        Assert.ThrowsExactly<InvalidDataException>(() => EvaluatorScorer.ValidateEvaluatorIdentityForTests(
            identity with
            {
                Files = files.Select(file => file.RelativePath == "protocol/protocol.json"
                    ? file with { Sha256 = new('0', 64) }
                    : file).ToArray(),
            }));
    }

    [TestMethod]
    public void ReflectionAdapterExecutesTheDeclaredCandidateArtifact()
    {
        string fixtureRoot = TestRepository.PathFromRoot("test-data", "public-fixtures", "bethesda", "BETH-NPC-DEV");
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(fixtureRoot, "inputs", "snapshot", "accepted-order.json")));
        PluginExecutionInput[] plugins = receipt.RootElement.GetProperty("plugin_order")
            .EnumerateArray()
            .OrderBy(item => item.GetProperty("load_order").GetInt32())
            .Select(item =>
            {
                string name = item.GetProperty("file_name").GetString()!;
                string relative = item.GetProperty("artifact_id").GetString()!;
                string path = Path.GetFullPath(Path.Combine([fixtureRoot, .. relative.Split('/')]));
                ArtifactIdentity identity = EvaluatorProtocol.Identity(path);
                int order = item.GetProperty("load_order").GetInt32();
                return new PluginExecutionInput(name, order, $"evaluator-provider-{order:D3}", path, identity.ByteLength, identity.Sha256);
            })
            .ToArray();
        CandidateIdentity candidate = PublicCandidate();
        LooseProviderExecutionInput lower = new(
            "facegen-provider-low",
            "regular_mod",
            10,
            plugins[0].Path,
            plugins[0].ByteLength,
            plugins[0].Sha256);
        LooseProviderExecutionInput winner = new(
            "facegen-provider-winner",
            "overwrite",
            20,
            plugins[1].Path,
            plugins[1].ByteLength,
            plugins[1].Sha256);
        LooseProviderChainExecutionInput[] looseChains =
        [
            new("meshes/actors/character/facegendata/facegeom/01-actors.esm/00000850.nif", [lower, winner], winner.LocalInstalledEntityId),
            new("textures/actors/character/facegendata/facetint/01-actors.esm/00000850.dds", [lower, winner], winner.LocalInstalledEntityId),
        ];
        ExecutionManifest manifest = new(
            EvaluatorProtocol.ManifestSchema,
            EvaluatorProtocol.ProtocolId,
            candidate,
            new EvaluatorIdentity(
                new('b', 40),
                EvaluatorProtocol.ProtocolId,
                EvaluatorProtocol.ScorerId,
                EvaluatorProtocol.ScorerVersion,
                EvaluatorProtocol.AdapterId,
                EvaluatorProtocol.AdapterVersion,
                EvaluatorProtocol.ProjectionId,
                EvaluatorProtocol.ProjectionVersion,
                AppContext.BaseDirectory,
                []),
            new CorpusIdentity("public-test", "1.0.0", new string('c', 64), "frozen", "clean"),
            new ExecutionInput(plugins, looseChains, false, ["archive_member_read"]));

        CandidateSemanticOutput output = ReflectionCandidateAdapter.Execute(manifest);

        Assert.AreEqual("completed_with_gaps", output.State);
        Assert.IsGreaterThan(0, output.Facts.Count);
        CollectionAssert.AreEqual(
            output.Facts.Select(item => item.FactId).Order(StringComparer.Ordinal).ToArray(),
            output.Facts.Select(item => item.FactId).ToArray());
        AssertFact(output, "plugins/0000/plugin_name", "plugin", "string", "00-pad.esm");
        AssertFact(output, "plugins/0004/master_style", "plugin", "string", "light");
        AssertMatchingFact(output,
            fact => fact.FactId.StartsWith("npc_contributions/", StringComparison.Ordinal)
                    && fact.FactId.EndsWith("/race/target_form_key", StringComparison.Ordinal),
            "form_key", "string", "00000810:01-actors.esm");
        AssertFact(output, "face_gen/00000850%3A01-actors.esm/mesh/present", "face_gen", "boolean", "true");
        AssertFact(output, "face_gen/00000850%3A01-actors.esm/mesh/provider_ids/0001", "face_gen", "string", "facegen-provider-winner");
        AssertFact(output, "face_gen/00000850%3A01-actors.esm/mesh/winner_provider_id", "face_gen", "string", "facegen-provider-winner");
        Assert.IsFalse(output.Facts.Any(fact => fact.FactId.StartsWith("gaps/", StringComparison.Ordinal)
                                               && fact.FactId.EndsWith("/missing_capability", StringComparison.Ordinal)
                                               && fact.Value.ValueKind == JsonValueKind.String
                                               && fact.Value.GetString() == "archive-activation-and-member-precedence"));
        AssertHeldOutBoundary(output);
        Assert.IsFalse(output.Facts.Any(fact => fact.FactId.Contains("reason", StringComparison.OrdinalIgnoreCase)
                                               || fact.FactId.Contains("message", StringComparison.OrdinalIgnoreCase)
                                               || fact.FactId.Contains("snapshot_authorized_path", StringComparison.OrdinalIgnoreCase)
                                               || fact.FactId.Contains("dependency_fingerprint", StringComparison.OrdinalIgnoreCase)
                                               || fact.Value.ValueKind == JsonValueKind.String
                                               && fact.Value.GetString() is string value
                                               && Path.IsPathFullyQualified(value)));
        ExecutionManifest partialManifest = manifest with
        {
            Execution = manifest.Execution with { LooseProviderChains = [looseChains[0]] },
        };
        CandidateSemanticOutput partial = ReflectionCandidateAdapter.Execute(partialManifest);
        AssertFact(partial, "face_gen/00000850%3A01-actors.esm/mesh/present", "face_gen", "boolean", "true");
        AssertFact(partial, "face_gen/00000850%3A01-actors.esm/tint/present", "face_gen", "boolean", "false");
        AssertFact(partial, "face_gen/00000850%3A01-actors.esm/tint/exact_absence_known", "face_gen", "boolean", "false");
        CandidateSemanticOutput exactAbsence = ReflectionCandidateAdapter.Execute(partialManifest with
        {
            Execution = partialManifest.Execution with { ArchiveMemberPopulationSupported = true },
        });
        AssertFact(exactAbsence, "face_gen/00000850%3A01-actors.esm/tint/exact_absence_known", "face_gen", "boolean", "false");
        Assert.IsFalse(output.Facts.Any(fact => fact.FactType == "taxonomy"
                                                && fact.FactId.EndsWith("/subject_type", StringComparison.Ordinal)
                                                && fact.Value.ValueKind == JsonValueKind.String
                                                && fact.Value.GetString() == "provider-topology"));
        AssertAggregateScoring(manifest, output);

        ExecutionManifest undeclaredDependency = manifest with
        {
            Candidate = manifest.Candidate with
            {
                Files = manifest.Candidate.Files
                    .Where(file => !string.Equals(file.RelativePath, "Infinium.Domain.dll", StringComparison.Ordinal))
                    .ToArray(),
            },
        };
        Assert.ThrowsExactly<FileNotFoundException>(() =>
            ReflectionCandidateAdapter.Execute(undeclaredDependency));
    }

    [TestMethod]
    public void ReflectionAdapterProjectsExactRefrOwnershipPlacementAndWinnerFacts()
    {
        CandidateSemanticOutput output = ReflectionCandidateAdapter.Execute(CreatePublicManifest("BETH-REFR-DEV"));
        Assert.AreEqual("completed", output.State);
        AssertFact(output, "override_chains/00000840%3A01-world.esm/winner/source_plugin", "winner", "string", "05-deletedwinner.esp");
        AssertFact(output, "override_chains/00000840%3A01-world.esm/winner/deleted", "winner", "boolean", "true");
        AssertMatchingFact(output, fact => fact.FactId.StartsWith("placed_reference_contributions/", StringComparison.Ordinal)
                                               && fact.FactId.EndsWith("/owner/target_form_key", StringComparison.Ordinal),
            "form_key", "string", "00000810:01-world.esm");
        AssertMatchingFact(output, fact => fact.FactId.StartsWith("placed_reference_contributions/", StringComparison.Ordinal)
                                               && fact.FactId.EndsWith("/owner/state", StringComparison.Ordinal),
            "ownership", "string", "resolved");
        AssertMatchingFact(output, fact => fact.FactId.StartsWith("placed_reference_contributions/", StringComparison.Ordinal)
                                               && fact.FactId.EndsWith("/placement/position/x", StringComparison.Ordinal),
            "placement", "number", "10");
        AssertMatchingFact(output, fact => fact.FactId.StartsWith("placed_reference_contributions/", StringComparison.Ordinal)
                                               && fact.FactId.EndsWith("/placement/rotation/x", StringComparison.Ordinal),
            "placement", "number", "0.1");
        AssertHeldOutBoundary(output);
        Assert.IsFalse(output.Facts.Any(fact => fact.FactId.Contains("reason", StringComparison.OrdinalIgnoreCase)
                                               || fact.FactId.Contains("message", StringComparison.OrdinalIgnoreCase)));
        AssertAggregateScoring(CreatePublicManifest("BETH-REFR-DEV"), output);
    }

    [TestMethod]
    public void PublicAdapterFixturesTogetherExerciseEveryDeclaredFactFamily()
    {
        CandidateSemanticOutput npc = ReflectionCandidateAdapter.Execute(CreatePublicManifest("BETH-NPC-DEV"));
        CandidateSemanticOutput refr = ReflectionCandidateAdapter.Execute(CreatePublicManifest("BETH-REFR-DEV"));
        string[] observed = npc.Facts.Concat(refr.Facts)
            .Select(fact => fact.FactId.Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            SemanticCanonicalizer.IncludedFactFamilies.Order(StringComparer.Ordinal).ToArray(),
            observed);
    }

    private static void AssertFact(CandidateSemanticOutput output, string id, string type, string valueType, string value)
    {
        SemanticFact fact = output.Facts.Single(item => item.FactId == id);
        Assert.AreEqual(type, fact.FactType);
        Assert.AreEqual(valueType, fact.ValueType);
        Assert.AreEqual(value, fact.Value.ValueKind == JsonValueKind.String
            ? fact.Value.GetString()
            : fact.Value.GetRawText());
    }

    private static void AssertMatchingFact(
        CandidateSemanticOutput output,
        Func<SemanticFact, bool> predicate,
        string type,
        string valueType,
        string value)
    {
        Assert.IsTrue(output.Facts.Any(fact =>
            predicate(fact)
            && fact.FactType == type
            && fact.ValueType == valueType
            && value == (fact.Value.ValueKind == JsonValueKind.String
                ? fact.Value.GetString()
                : fact.Value.GetRawText())));
    }

    private static void AssertHeldOutBoundary(CandidateSemanticOutput output)
    {
        string[] forbidden =
        [
            "contribution_id", "participant_id", "winner_contribution_id", "assignment_id",
            "analyzer_or_adjudicator_id", "evidence_fields", "gap_id", "gap_ids", "denominator_label",
            "failures/", "/ai_data/",
        ];
        Assert.IsFalse(output.Facts.Any(fact => forbidden.Any(token =>
            fact.FactId.Contains(token, StringComparison.OrdinalIgnoreCase))));
        AssertFact(output, "result/snapshot_present", "state", "boolean", "true");
        AssertFact(output, "result/failure_present", "state", "boolean", "false");
    }

    private static JsonObject FirstTaxonomy(JsonObject root) =>
        root["snapshot"]!["taxonomy"]!.AsArray()[0]!.AsObject();

    private static JsonObject FirstAiData(JsonObject root) =>
        root["snapshot"]!["npc_contributions"]!.AsArray()
            .Select(item => item!.AsObject())
            .Select(item => item["ai_data"])
            .First(item => item is not null)!
            .AsObject();

    private static string ProjectedJson(JsonNode root)
    {
        using JsonDocument document = JsonDocument.Parse(root.ToJsonString());
        return JsonSerializer.Serialize(SemanticCanonicalizer.Project(document.RootElement), EvaluatorProtocol.JsonOptions);
    }

    private static ExecutionManifest CreatePublicManifest(string fixtureId)
    {
        string fixtureRoot = TestRepository.PathFromRoot("test-data", "public-fixtures", "bethesda", fixtureId);
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixtureRoot, "inputs", "snapshot", "accepted-order.json")));
        PluginExecutionInput[] plugins = receipt.RootElement.GetProperty("plugin_order").EnumerateArray()
            .OrderBy(item => item.GetProperty("load_order").GetInt32()).Select(item =>
            {
                string relative = item.GetProperty("artifact_id").GetString()!;
                string path = Path.GetFullPath(Path.Combine([fixtureRoot, .. relative.Split('/')]));
                ArtifactIdentity identity = EvaluatorProtocol.Identity(path);
                int order = item.GetProperty("load_order").GetInt32();
                return new PluginExecutionInput(item.GetProperty("file_name").GetString()!, order, $"public-provider-{order:D3}", path, identity.ByteLength, identity.Sha256);
            }).ToArray();
        CandidateIdentity candidate = PublicCandidate();
        return new ExecutionManifest(
            EvaluatorProtocol.ManifestSchema,
            EvaluatorProtocol.ProtocolId,
            candidate,
            new EvaluatorIdentity(new('b', 40), EvaluatorProtocol.ProtocolId, EvaluatorProtocol.ScorerId, EvaluatorProtocol.ScorerVersion,
                EvaluatorProtocol.AdapterId, EvaluatorProtocol.AdapterVersion, EvaluatorProtocol.ProjectionId, EvaluatorProtocol.ProjectionVersion,
                AppContext.BaseDirectory, []),
            new CorpusIdentity("public-test", "1.0.0", new('c', 64), "frozen", "clean"),
            new ExecutionInput(plugins, [], false, ["archive_member_read"]));
    }

    private static CandidateIdentity PublicCandidate()
    {
        string? overrideRoot = Environment.GetEnvironmentVariable("INFINIUM_EVALUATOR_CANDIDATE_ROOT");
        string root = string.IsNullOrWhiteSpace(overrideRoot)
            ? AppContext.BaseDirectory
            : Path.GetFullPath(overrideRoot);
        string path = Path.Combine(root, "Infinium.Bethesda.dll");
        ArtifactIdentity artifact = EvaluatorProtocol.Identity(path);
        EvaluatorFileIdentity[] files = Directory.EnumerateFiles(root, "*.dll")
            .Select(file =>
            {
                ArtifactIdentity identity = EvaluatorProtocol.Identity(file);
                return new EvaluatorFileIdentity(Path.GetFileName(file), identity.ByteLength, identity.Sha256);
            })
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
        return new CandidateIdentity(
            string.IsNullOrWhiteSpace(overrideRoot)
                ? new string('a', 40)
                : "98fe8a5a173116427bf78077673fd10e8d018103",
            path,
            artifact,
            root,
            files);
    }

    private static void AssertAggregateScoring(ExecutionManifest source, CandidateSemanticOutput candidate)
    {
        string root = Path.Combine(Path.GetTempPath(), $"infinium-v2-aggregate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            EvaluatorFileIdentity[] evaluatorFiles = EvaluatorScorer.RequiredEvaluatorFiles
                .Select(relative =>
                {
                    string path = Path.Combine(AppContext.BaseDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
                    ArtifactIdentity identity = EvaluatorProtocol.Identity(path);
                    return new EvaluatorFileIdentity(relative, identity.ByteLength, identity.Sha256);
                }).ToArray();
            ExpectedSemanticOutput expected = new(
                EvaluatorProtocol.ExpectedSchema,
                EvaluatorProtocol.ProtocolId,
                EvaluatorProtocol.ProjectionId,
                EvaluatorProtocol.ProjectionVersion,
                "public-aggregate",
                "1.0.0",
                candidate.State,
                candidate.Facts.Select(fact => fact.ValueType == "number"
                    && fact.Value.ValueKind == JsonValueKind.Number
                    && fact.Value.TryGetInt64(out long integral)
                        ? fact with { Value = JsonValue($"{integral}.0") }
                        : fact).ToArray());
            string passOracle = Path.Combine(root, "pass-oracle.json");
            File.WriteAllText(passOracle, EvaluatorProtocol.Serialize(expected), new System.Text.UTF8Encoding(false));
            ExpectedSemanticOutput mutation = expected with
            {
                Facts = expected.Facts.Select(fact => fact.FactId == "plugins/0000/plugin_name"
                    ? fact with { Value = EvaluatorProtocol.Primitive("wrong.esm") }
                    : fact).ToArray(),
            };
            string failOracle = Path.Combine(root, "fail-oracle.json");
            File.WriteAllText(failOracle, EvaluatorProtocol.Serialize(mutation), new System.Text.UTF8Encoding(false));
            EvaluatorIdentity evaluator = source.Evaluator with { Root = AppContext.BaseDirectory, Files = evaluatorFiles };

            CorpusExecutionManifest one = new(
                EvaluatorProtocol.CorpusManifestSchema,
                EvaluatorProtocol.ProtocolId,
                source.Candidate,
                evaluator,
                new CorpusIdentity("public-aggregate", "1.0.0", new('0', 64), "frozen", "clean"),
                [new CorpusExecutionMember("private-member-a", source.Execution, passOracle)]);
            one = one with { Corpus = one.Corpus with { Sha256 = EvaluatorScorer.CorpusFingerprint(one) } };
            string onePath = Path.Combine(root, "one.json");
            File.WriteAllText(onePath, EvaluatorProtocol.Serialize(one), new System.Text.UTF8Encoding(false));
            CorpusScoreOutcome oneOutcome = EvaluatorScorer.ScoreCorpus(onePath);
            Assert.AreEqual("PASS", oneOutcome.Result.TerminalResult);
            Assert.AreEqual(1, oneOutcome.Result.MemberCounts.Total);

            CorpusExecutionManifest renamed = one with
            {
                Members = [new CorpusExecutionMember("private-member-renamed", source.Execution, passOracle)],
            };
            string renamedPath = Path.Combine(root, "renamed.json");
            File.WriteAllText(renamedPath, EvaluatorProtocol.Serialize(renamed), new System.Text.UTF8Encoding(false));
            CorpusScoreOutcome renamedOutcome = EvaluatorScorer.ScoreCorpus(renamedPath);
            Assert.AreEqual("EVALUATOR_ERROR", renamedOutcome.Result.TerminalResult);
            Assert.HasCount(1, renamedOutcome.Result.FailureCategories!);
            Assert.AreEqual("oracle", renamedOutcome.Result.FailureCategories![0]);

            CorpusExecutionManifest multiple = one with
            {
                Members =
                [
                    new CorpusExecutionMember("private-member-a", source.Execution, passOracle),
                    new CorpusExecutionMember("private-member-b", source.Execution, failOracle),
                ],
                Corpus = one.Corpus with { Sha256 = new('0', 64) },
            };
            multiple = multiple with { Corpus = multiple.Corpus with { Sha256 = EvaluatorScorer.CorpusFingerprint(multiple) } };
            string multiplePath = Path.Combine(root, "multiple.json");
            File.WriteAllText(multiplePath, EvaluatorProtocol.Serialize(multiple), new System.Text.UTF8Encoding(false));
            CorpusScoreOutcome multipleOutcome = EvaluatorScorer.ScoreCorpus(multiplePath);
            Assert.AreEqual("FAIL", multipleOutcome.Result.TerminalResult);
            Assert.AreEqual(2, multipleOutcome.Result.MemberCounts.Total);
            Assert.AreEqual(1, multipleOutcome.Result.MemberCounts.Failed);
            string sanitized = EvaluatorProtocol.Serialize(multipleOutcome.Result);
            Assert.IsFalse(sanitized.Contains("private-member", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static JsonElement JsonValue(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
