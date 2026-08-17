using System.Text.Json.Nodes;
using Infinium.Application.Provider;
using Infinium.Coordinator;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class M1Slice6CampaignV2InputAdapterTests
{
    [TestMethod]
    public void FrozenV2InputsNormalizeToProductContractsAndRetainDistinctRootKinds()
    {
        string sourceJson = File.ReadAllText(TestRepository.PathFromRoot("fixtures", "public", "provider",
            "source-claims", "S6-CLAIM-LIVE-VAL-v2", "execution-input.v2.json"));
        string candidateJson = File.ReadAllText(TestRepository.PathFromRoot("fixtures", "public", "provider",
            "candidate-investigations", "S6-CANDIDATE-LIVE-VAL-v2", "execution-input.v2.json"));

        SourceClaimExecutionInput source = M1Slice6CampaignV2InputAdapter.ReadSourceClaim(sourceJson);
        M1Slice6CampaignCandidateInput candidate = M1Slice6CampaignV2InputAdapter.ReadCandidate(candidateJson);

        Assert.AreEqual("wp10-acquisition-live-val-v2", source.AcquisitionRunId);
        Assert.AreEqual(9, source.Passages.Count);
        Assert.AreEqual("wp11-analysis-live-val-v2", candidate.ProductInput.AnalysisRunId);
        Assert.AreEqual(2, candidate.RootsByContext.Count);
        Assert.AreEqual(M1Slice6CampaignEvidenceRootKind.PersistedSourceClaimApplication,
            candidate.RootsByContext["relay-gate-context-a"].Kind);
        Assert.AreEqual(M1Slice6CampaignEvidenceRootKind.FrozenHostEvidence,
            candidate.RootsByContext["relay-gate-context-b"].Kind);
        CollectionAssert.AreEqual(System.Text.Encoding.UTF8.GetBytes(candidateJson), candidate.ExactV2Bytes);
    }

    [TestMethod]
    public void CandidateAdapterRejectsV1CrossBindingSwappedOrParallelRoots()
    {
        byte[] bytes = File.ReadAllBytes(TestRepository.PathFromRoot("fixtures", "public", "provider",
            "candidate-investigations", "S6-CANDIDATE-LIVE-VAL-v2", "execution-input.v2.json"));

        Reject(root => root["schema_id"] = "infinium.llm.candidate-investigation-execution-input/v1");
        Reject(root => root["package_id"] = "");
        Reject(root => root["contexts"]![1]!["evidence"]![0]!["host_bindings"] =
            root["contexts"]![0]!["evidence"]![0]!["host_bindings"]!.DeepClone());
        Reject(root => root["contexts"]![1]!["evidence"]![0]!["host_evidence"] = null);
        Reject(root => root["contexts"]![0]!["evidence"]![0]!["host_evidence"] =
            root["contexts"]![1]!["evidence"]![0]!["host_evidence"]!.DeepClone());

        void Reject(Action<JsonObject> mutation)
        {
            JsonObject root = JsonNode.Parse(bytes)!.AsObject();
            mutation(root);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6CampaignV2InputAdapter.ReadCandidate(root.ToJsonString()));
        }
    }

    [TestMethod]
    public void ProductAssembliesContainNoFrozenFixtureIdentityOrExpectedSemanticTruth()
    {
        string sourceRoot = TestRepository.PathFromRoot("src");
        string[] forbidden =
        [
            "LiveSemanticV2WordingNormalizer",
            "relay-",
            "S6-CLAIM-LIVE-VAL-v2",
            "S6-CANDIDATE-LIVE-VAL-v2",
            "LLM-CLAIM-LIVE-VAL-v2",
            "LLM-INVESTIGATE-LIVE-VAL-v2",
            "PROV-LIVE-COMPOSED-VAL-v2",
            "fixtures/public",
            "The exact admitted source evidence supports the bounded hypothesis.",
            "Observation without an exchange declaration does not establish the requested capability.",
            "The shared local observation is conditional on active exchange",
        ];
        foreach (string path in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            foreach (string token in forbidden)
            {
                Assert.DoesNotContain(token, text, StringComparison.Ordinal,
                    Path.GetRelativePath(sourceRoot, path));
            }
        }

        string[] productReferences = typeof(M1Slice6CampaignStageCoordinator).Assembly
            .GetReferencedAssemblies().Select(reference => reference.Name!).Concat(
                typeof(SourceClaimContextMinimizer).Assembly.GetReferencedAssemblies()
                    .Select(reference => reference.Name!)).ToArray();
        Assert.IsFalse(productReferences.Any(reference =>
            reference.Contains("PublicFixtures", StringComparison.Ordinal)
            || reference.Contains("Tests", StringComparison.Ordinal)));
        string tooling = File.ReadAllText(TestRepository.PathFromRoot("fixtures", "tooling",
            "Infinium.PublicFixtures", "LiveSemanticV2TypedOracleVerifier.cs"));
        Assert.Contains("LiveSemanticV2WordingNormalizer", tooling, StringComparison.Ordinal);
    }
}
