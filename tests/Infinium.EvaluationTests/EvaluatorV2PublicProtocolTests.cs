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
        Assert.HasCount(17, result.Cases);
        Assert.IsTrue(result.Cases.All(item => item.Passed));
        Assert.AreEqual("PASS", result.Cases.Single(item => item.CaseId == "known-correct").ActualTerminal);
        Assert.IsTrue(result.Cases
            .Where(item => item.CaseId is "malformed-candidate-output" or "malformed-oracle" or "tampered-oracle-identity" or "candidate-dependency-drift")
            .All(item => item.ActualTerminal == "EVALUATOR_ERROR"));
    }

    [TestMethod]
    public void CanonicalizerTreatsStableIdentityArraysAsSetsAndContributionsAsSequences()
    {
        const string first = """
            {
              "state": "completed",
              "snapshot": {
                "gaps": [
                  { "gap_id": "b", "denominator": 2 },
                  { "gap_id": "a", "denominator": 1 }
                ],
                "contributions": [
                  { "contribution_id": "first", "source_plugin": "A.esm" },
                  { "contribution_id": "second", "source_plugin": "B.esp" }
                ]
              },
              "failures": []
            }
            """;
        const string reorderedSet = """
            {
              "state": "completed",
              "snapshot": {
                "gaps": [
                  { "gap_id": "a", "denominator": 1 },
                  { "gap_id": "b", "denominator": 2 }
                ],
                "contributions": [
                  { "contribution_id": "first", "source_plugin": "A.esm" },
                  { "contribution_id": "second", "source_plugin": "B.esp" }
                ]
              },
              "failures": []
            }
            """;
        const string reversedSequence = """
            {
              "state": "completed",
              "snapshot": {
                "gaps": [
                  { "gap_id": "a", "denominator": 1 },
                  { "gap_id": "b", "denominator": 2 }
                ],
                "contributions": [
                  { "contribution_id": "second", "source_plugin": "B.esp" },
                  { "contribution_id": "first", "source_plugin": "A.esm" }
                ]
              },
              "failures": []
            }
            """;

        Assert.AreEqual(Flatten(first), Flatten(reorderedSet));
        Assert.AreNotEqual(Flatten(first), Flatten(reversedSequence));
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
                "calibration-results.v1.schema.json"));
            Assert.IsFalse(Directory.Exists(root));
            EvaluatorScorer.WriteSingleResult(
                root,
                "result.json",
                result,
                "calibration-results.v1.schema.json");
            Assert.ThrowsExactly<ResultWriteException>(() => EvaluatorScorer.WriteSingleResult(
                root,
                "result.json",
                result,
                "calibration-results.v1.schema.json"));
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
                "calibration-results.v1.schema.json"));
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
                [],
                "clean");

            EvaluatorScorer.WriteSingleResult(
                root,
                "sanitized-result.json",
                result,
                "sanitized-result.v1.schema.json");

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
                AppContext.BaseDirectory,
                []),
            new CorpusIdentity("public-test", "1.0.0", new string('c', 64), "frozen", "clean"),
            new ExecutionInput(plugins, ["archive_member_read"]));

        CandidateSemanticOutput output = ReflectionCandidateAdapter.Execute(manifest);

        Assert.AreEqual("completed_with_gaps", output.State);
        Assert.IsGreaterThan(0, output.Facts.Count);
        CollectionAssert.AreEqual(
            output.Facts.Select(item => item.FactId).Order(StringComparer.Ordinal).ToArray(),
            output.Facts.Select(item => item.FactId).ToArray());

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

    private static string Flatten(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return EvaluatorProtocol.Serialize(SemanticCanonicalizer.Flatten(document.RootElement)
            .OrderBy(item => item.FactId, StringComparer.Ordinal)
            .ToArray());
    }
}
