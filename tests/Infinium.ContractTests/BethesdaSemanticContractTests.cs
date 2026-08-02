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
        Assert.AreEqual("1.0.0", result.Snapshot.SchemaVersion.ToString());
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
}
