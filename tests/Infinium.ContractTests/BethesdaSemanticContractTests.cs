using System.Text.Json;
using Infinium.Bethesda;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaSemanticContractTests
{
    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void ResultUsesClosedTypedCollectionsAndPinnedProducerIdentity()
    {
        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create("BETH-REFR-DEV"));

        Assert.IsNotNull(result.Snapshot);
        Assert.AreEqual(BethesdaSemanticExtractor.ProducerId, result.Snapshot.ProducerId);
        Assert.AreEqual(BethesdaSemanticExtractor.ProducerVersion, result.Snapshot.ProducerVersion);
        Assert.AreEqual("2.0.0", result.Snapshot.SchemaVersion.ToString());
        Assert.IsNotNull(result.Snapshot.OverrideChains);
        Assert.IsNotNull(result.Snapshot.Winners);
        Assert.IsNotNull(result.Snapshot.NpcContributions);
        Assert.IsNotNull(result.Snapshot.RaceContributions);
        Assert.IsNotNull(result.Snapshot.PlacedReferenceContributions);
        Assert.IsNotNull(result.Snapshot.AllowlistedFields);
        Assert.IsNotNull(result.Snapshot.ResolvedParticipants);
        Assert.IsNotNull(result.Snapshot.Npcs);
        Assert.IsNotNull(result.Snapshot.Races);
        Assert.IsNotNull(result.Snapshot.PlacedReferences);
        Assert.IsNotNull(result.Snapshot.Links);
        Assert.IsNotNull(result.Snapshot.ReverseLinks);
        Assert.IsNotNull(result.Snapshot.FaceGen);
        Assert.IsNotNull(result.Snapshot.Coverage);
        Assert.IsNotNull(result.Snapshot.Gaps);
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void CleanBreakTransportUsesExactV2FaceGenVocabularyAndFixedCoverageRows()
    {
        BethesdaSemanticSnapshot snapshot = new BethesdaSemanticExtractor().Extract(
            BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV")).Snapshot!;

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(snapshot));
        JsonElement faceGen = json.RootElement.GetProperty("FaceGen")[0];
        string applicability = faceGen.GetProperty("Applicability").GetString()!;
        string[] exactVocabulary =
        {
            "applicable",
            "not_applicable_deleted_winner",
            "unknown_template_traits_decision",
            "not_applicable_template_traits",
            "unknown_race",
            "not_applicable_race_without_face_gen_head",
        };
        Assert.IsTrue(exactVocabulary.Contains(applicability, StringComparer.Ordinal));
        CollectionAssert.AreEquivalent(
            exactVocabulary,
            Enum.GetValues<BethesdaFaceGenApplicability>()
                .Select(value => JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value))!)
                .ToArray());
        Assert.IsTrue(new[] { "present", "absent", "unknown" }.Contains(
            faceGen.GetProperty("Mesh").GetProperty("Availability").GetString()!,
            StringComparer.Ordinal));
        CollectionAssert.AreEqual(
            BethesdaSemanticContract.CoveragePopulations.ToArray(),
            snapshot.Coverage.Select(row => row.Population).ToArray());
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void AvailabilityTransportMappingIsExactForAllThreeStates()
    {
        Assert.AreEqual((true, false), BethesdaSemanticContract.AssetTransport(BethesdaAssetAvailability.Present));
        Assert.AreEqual((false, true), BethesdaSemanticContract.AssetTransport(BethesdaAssetAvailability.Absent));
        Assert.AreEqual((false, false), BethesdaSemanticContract.AssetTransport(BethesdaAssetAvailability.Unknown));
    }
}
