using System.Text.Json;
using Infinium.EvaluatorV2;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
[TestCategory("M1Evaluation")]
[TestProperty("Category", "M1Evaluation")]
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
            "prepared-integral-token-semantic-number",
        ];
        Assert.IsTrue(passing.All(id => result.Cases.Single(item => item.CaseId == id).ActualTerminal == "PASS"));

        string[] contractFailures =
        [
            "semantic-integer-non-integral-rejected",
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
    [TestCategory("M1Security")]
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
                "calibration-results.v3.schema.json"));
            Assert.IsFalse(Directory.Exists(root));
            EvaluatorScorer.WriteSingleResult(
                root,
                "result.json",
                result,
                "calibration-results.v3.schema.json");
            Assert.ThrowsExactly<ResultWriteException>(() => EvaluatorScorer.WriteSingleResult(
                root,
                "result.json",
                result,
                "calibration-results.v3.schema.json"));
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
    [TestCategory("M1Security")]
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
                "calibration-results.v3.schema.json"));
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
                "sanitized-result.v3.schema.json");

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
    [TestCategory("M1Security")]
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
    [TestCategory("M1Security")]
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
        string fixtureRoot = TestRepository.PathFromRoot("test-data", "evaluation", "m1-semantic", "BETH-NPC-DEV");
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
        string candidatePath = Path.Combine(AppContext.BaseDirectory, "Infinium.Bethesda.dll");
        ArtifactIdentity candidateIdentity = EvaluatorProtocol.Identity(candidatePath);
        EvaluatorFileIdentity[] candidateFiles = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
            .Select(path =>
            {
                ArtifactIdentity identity = EvaluatorProtocol.Identity(path);
                return new EvaluatorFileIdentity(Path.GetFileName(path), identity.ByteLength, identity.Sha256);
            })
            .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
            .ToArray();
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
            new CandidateIdentity(new('a', 40), candidatePath, candidateIdentity, AppContext.BaseDirectory, candidateFiles),
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
        AssertFact(output,
            "links/contribution%3A0001%3A01-actors.esm%3A00000800%3A01-actors.esm%3Arnam%3Avalue%3A0000/target_form_key",
            "form_key", "string", "00000810:01-actors.esm");
        AssertFact(output, "face_gen/record%3A00000850%3A01-actors.esm/mesh/present", "face_gen", "boolean", "true");
        AssertFact(output, "face_gen/record%3A00000850%3A01-actors.esm/mesh/provider_participant_ids/0001", "face_gen", "string", "facegen-provider-winner");
        AssertFact(output, "face_gen/record%3A00000850%3A01-actors.esm/mesh/winner_participant_id", "face_gen", "string", "facegen-provider-winner");
        AssertFact(output, "gaps/gap%3Ainfinium-bethesda%3Aarchive-member-read/missing_capability", "gap", "string", "archive-activation-and-member-precedence");
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
        AssertFact(partial, "face_gen/record%3A00000850%3A01-actors.esm/mesh/present", "face_gen", "boolean", "true");
        AssertFact(partial, "face_gen/record%3A00000850%3A01-actors.esm/tint/present", "face_gen", "boolean", "false");
        AssertFact(partial, "face_gen/record%3A00000850%3A01-actors.esm/tint/exact_absence_known", "face_gen", "boolean", "false");
        CandidateSemanticOutput exactAbsence = ReflectionCandidateAdapter.Execute(partialManifest with
        {
            Execution = partialManifest.Execution with { ArchiveMemberPopulationSupported = true },
        });
        AssertFact(exactAbsence, "face_gen/record%3A00000850%3A01-actors.esm/tint/exact_absence_known", "face_gen", "boolean", "true");
        Assert.IsTrue(output.Facts.Any(fact => fact.FactType == "taxonomy"
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
        Assert.AreEqual("completed_with_gaps", output.State);
        AssertFact(output, "override_chains/00000840%3A01-world.esm/winner_contribution_id", "winner", "string",
            "contribution:0005:05-deletedwinner.esp:00000840:01-world.esm");
        AssertFact(output,
            "placed_reference_contributions/contribution%3A0003%3A03-placement.esp%3A00000840%3A01-world.esm/owner/target_form_key",
            "form_key", "string", "00000810:01-world.esm");
        AssertFact(output,
            "placed_reference_contributions/contribution%3A0003%3A03-placement.esp%3A00000840%3A01-world.esm/owner/state",
            "ownership", "string", "resolved");
        AssertFact(output,
            "placed_reference_contributions/contribution%3A0003%3A03-placement.esp%3A00000840%3A01-world.esm/placement/position/x",
            "placement", "number", "10");
        AssertFact(output,
            "placed_reference_contributions/contribution%3A0003%3A03-placement.esp%3A00000840%3A01-world.esm/placement/rotation/x",
            "placement", "number", "0.1");
        Assert.IsFalse(output.Facts.Any(fact => fact.FactId.Contains("reason", StringComparison.OrdinalIgnoreCase)
                                               || fact.FactId.Contains("message", StringComparison.OrdinalIgnoreCase)));
        AssertAggregateScoring(CreatePublicManifest("BETH-REFR-DEV"), output);
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

    private static ExecutionManifest CreatePublicManifest(string fixtureId)
    {
        string fixtureRoot = TestRepository.PathFromRoot("test-data", "evaluation", "m1-semantic", fixtureId);
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
        string candidatePath = Path.Combine(AppContext.BaseDirectory, "Infinium.Bethesda.dll");
        ArtifactIdentity candidate = EvaluatorProtocol.Identity(candidatePath);
        EvaluatorFileIdentity[] candidateFiles = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll").Select(path =>
        {
            ArtifactIdentity identity = EvaluatorProtocol.Identity(path);
            return new EvaluatorFileIdentity(Path.GetFileName(path), identity.ByteLength, identity.Sha256);
        }).OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
        return new ExecutionManifest(
            EvaluatorProtocol.ManifestSchema,
            EvaluatorProtocol.ProtocolId,
            new CandidateIdentity(new('a', 40), candidatePath, candidate, AppContext.BaseDirectory, candidateFiles),
            new EvaluatorIdentity(new('b', 40), EvaluatorProtocol.ProtocolId, EvaluatorProtocol.ScorerId, EvaluatorProtocol.ScorerVersion,
                EvaluatorProtocol.AdapterId, EvaluatorProtocol.AdapterVersion, EvaluatorProtocol.ProjectionId, EvaluatorProtocol.ProjectionVersion,
                AppContext.BaseDirectory, []),
            new CorpusIdentity("public-test", "1.0.0", new('c', 64), "frozen", "clean"),
            new ExecutionInput(plugins, [], false, ["archive_member_read"]));
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
