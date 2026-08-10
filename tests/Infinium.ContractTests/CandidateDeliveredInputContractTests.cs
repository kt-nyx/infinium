using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Infinium.Application.Candidates;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateDeliveredInputContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void DeliveredInputAndExpansionRoundTripThroughClosedSchemas()
    {
        CandidateDeliveredExpansionContract expansion = Expansion();
        byte[] expansionBytes = CandidateDeliveredExpansionJsonCodec.Serialize(expansion);
        CandidateDeliveredExpansionContract expansionRoundTrip = CandidateDeliveredExpansionJsonCodec.Deserialize(expansionBytes);
        Assert.AreEqual(expansion.ExpansionId, expansionRoundTrip.ExpansionId);
        Assert.AreEqual(expansion.SubjectCount, expansionRoundTrip.SubjectCount);
        CollectionAssert.AreEqual(expansionBytes, CandidateDeliveredExpansionJsonCodec.Serialize(expansionRoundTrip));

        CandidateDeliveredInputContract input = CandidateDeliveredInputExpander.Expand(expansion);
        byte[] inputBytes = CandidateDeliveredInputJsonCodec.Serialize(input);
        CandidateDeliveredInputContract inputRoundTrip = CandidateDeliveredInputJsonCodec.Deserialize(inputBytes);
        Assert.AreEqual(input.PayloadId, inputRoundTrip.PayloadId);
        Assert.AreEqual("candidate-link-fact-s000-n00000000", input.LinkFacts[0].FactId.Value);
        Assert.AreEqual("candidate-facegen-fact-s000-n00000000", input.FaceGenFacts[0].FactId.Value);
        Assert.AreEqual("candidate-documentation-fact-s000-n00000000", input.DocumentationFacts[0].FactId.Value);
        Assert.AreEqual("candidate-coverage-gap-fact-s000-n00000000", input.CoverageGapFacts[0].FactId.Value);
        Assert.AreEqual(8, input.LinkFacts.Count);
        Assert.AreEqual(4, input.FaceGenFacts.Count);
        Assert.AreEqual(4, input.DocumentationFacts.Count);
        Assert.AreEqual(2, input.CoverageGapFacts.Count);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Mutation")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Mutation")]
    public void DeliveredInputRejectsAnswersUnknownFieldsAndSemanticDrift()
    {
        CandidateDeliveredInputContract input = CandidateDeliveredInputExpander.Expand(Expansion());
        byte[] bytes = CandidateDeliveredInputJsonCodec.Serialize(input);
        JsonObject root = JsonNode.Parse(bytes)!.AsObject();
        root["expected_candidates"] = 3;
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateDeliveredInputJsonCodec.Deserialize(
            System.Text.Encoding.UTF8.GetBytes(root.ToJsonString())));

        JsonObject expansionRoot = JsonNode.Parse(CandidateDeliveredExpansionJsonCodec.Serialize(Expansion()))!.AsObject();
        expansionRoot["documentation_series"]!.AsArray()[0]!["patterns"]!.AsArray()[0]!["lane"] = "mandatory-evidence";
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateDeliveredExpansionJsonCodec.Deserialize(
            System.Text.Encoding.UTF8.GetBytes(expansionRoot.ToJsonString())));

        CandidateDeliveredInputContract malformed = input with
        {
            FaceGenFacts = input.FaceGenFacts.Select((item, index) => index == 0
                ? item with { MeshProviderParticipantId = null }
                : item).ToArray(),
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => CandidateDeliveredContractInvariants.Validate(malformed));

        CandidateDeliveredExpansionContract oversized = Expansion() with
        {
            SubjectCount = CandidateDeliveredContractInvariants.MaximumFacts,
            LinkSeries = Enumerable.Range(0, 2).Select(index => new CandidateDeliveredLinkSeriesContract(
                1, $"Field{index}", null,
                [new(CandidateDeliveredLinkState.Null, CandidateDeliveredLinkState.Null, null, null)])).ToArray(),
            FaceGenSeries = [],
            DocumentationSeries = [],
            CoverageGapSeries = [],
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => CandidateDeliveredContractInvariants.Validate(oversized));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Scale")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Scale")]
    public void DeliveredExpansionUsesOneRecipeForBoundedMaterializationAndStreamingStress()
    {
        CandidateDeliveredExpansionContract small = Expansion();
        CandidateDeliveredExpansionMeasurement first = CandidateDeliveredInputExpander.Measure(small);
        CandidateDeliveredExpansionMeasurement second = CandidateDeliveredInputExpander.Measure(small);
        Assert.AreEqual(first, second);
        Assert.AreEqual(18L, first.TotalFacts);
        Assert.AreEqual(first.FactStreamFingerprint,
            CandidateDeliveredInputExpander.Measure(small with { ExpansionId = Id("different-artifact-binding") })
                .FactStreamFingerprint);

        CandidateDeliveredExpansionContract stress = small with { SubjectCount = 200_000 };
        CandidateDeliveredExpansionMeasurement measured = CandidateDeliveredInputExpander.Measure(stress);
        Assert.AreEqual(450_000L, measured.TotalFacts);
        Assert.ThrowsExactly<InvalidOperationException>(() => CandidateDeliveredInputExpander.Expand(stress));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void FrozenCandidatePackagesHaveExactClosedManifestsAndProductInputs()
    {
        Dictionary<string, string> expectedManifests = new(StringComparer.Ordinal)
        {
            ["CAND-SEMANTIC-DEV-v1"] = "9b5ef698f342428efc9499085b081bfa2f9284db3c9fc287547a7f7dd2fc507d",
            ["CAND-SCALE-VAL-v1"] = "e95930df3ef536a6f0bfd72d5195fd1d17fda2fe9fd6488423818edbf35c8d06",
            ["CAND-STRESS-DEV-v1"] = "b552521f3b5f6efac23a204fc7269b0cc59b15a1231baa81dce9d5164b35f72f",
        };
        string root = Path.Combine(FindRepositoryRoot(),
            "fixtures", "public", "candidates");
        CollectionAssert.AreEquivalent(expectedManifests.Keys.ToArray(),
            Directory.GetDirectories(root).Select(Path.GetFileName).ToArray());

        foreach ((string packageName, string manifestSha) in expectedManifests)
        {
            string directory = Path.Combine(root, packageName);
            string manifestPath = Path.Combine(directory, PublicFixturePackageReader.PublicManifestFileName);
            Assert.AreEqual(manifestSha, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(manifestPath))));
            Assert.AreEqual(9, Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length);
            CandidatePublicFixturePackage package = CandidateFixturePackageReader.Read(directory);
            Assert.AreEqual(packageName, package.Package.FixtureId.Value);
            Assert.AreEqual(new ContractVersion(1, 0, 0), package.Package.FixtureVersion);
            Assert.AreEqual(packageName.Contains("SCALE-VAL", StringComparison.Ordinal)
                ? FixturePartition.Validation : FixturePartition.Development, package.Package.Partition);
            Assert.AreEqual(packageName.Contains("SEMANTIC", StringComparison.Ordinal), package.DeliveredInput is not null);
            Assert.AreEqual(!packageName.Contains("SEMANTIC", StringComparison.Ordinal), package.DeliveredExpansion is not null);
        }
    }

    internal static CandidateDeliveredExpansionContract Expansion() => new(
        ContractConstants.CandidateDeliveredExpansionSchemaId,
        CandidateDeliveredInputIdentity.Version,
        Id("candidate-delivered-expansion-v1"),
        Id("run-candidate-delivered"),
        Id("snapshot-candidate-delivered"),
        Id("context-candidate-delivered"),
        Id("configuration-candidate-delivered"),
        8,
        [new(1, "Race", null,
        [
            new(CandidateDeliveredLinkState.Resolved, CandidateDeliveredLinkState.Resolved, 1, 2),
            new(CandidateDeliveredLinkState.Null, CandidateDeliveredLinkState.Null, null, null),
            new(CandidateDeliveredLinkState.Unresolved, CandidateDeliveredLinkState.Unresolved, null, null),
        ])],
        [new(2,
        [
            new(CandidateDeliveredFaceGenApplicability.Applicable, CandidateDeliveredAssetAvailability.Present, true,
                CandidateDeliveredAssetAvailability.Present, true, 2, 3),
            new(CandidateDeliveredFaceGenApplicability.NotApplicable, CandidateDeliveredAssetAvailability.Unknown, false,
                CandidateDeliveredAssetAvailability.Unknown, false, 0, 0),
            new(CandidateDeliveredFaceGenApplicability.Unknown, CandidateDeliveredAssetAvailability.Unknown, false,
                CandidateDeliveredAssetAvailability.Unknown, false, 0, 0),
        ])],
        [new(2,
        [
            new(ClaimApplicabilityState.Applicable, true, true, false),
            new(ClaimApplicabilityState.NotApplicable, true, true, false),
            new(ClaimApplicabilityState.Unknown, true, true, true),
            new(ClaimApplicabilityState.Applicable, false, true, false),
        ])],
        [new(4, "unsupported decoder", "The delivered decoder does not cover this factual population.")]);

    private static OpaqueId Id(string value) => new(value);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Infinium.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
