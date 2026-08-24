using System.Text.Json;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class SourceClaimFixtureIntegrityTests
{
    [TestMethod]
    [TestCategory("Contract")]
    public void SourceClaimAnswerIsolationIsRecursiveButTreatsPassageTextAsInert()
    {
        using JsonDocument inert = JsonDocument.Parse(
            """{"passages":[{"text":"An inert passage may literally mention oracle and expected_answer."}],"safe":"data"}""");
        SourceClaimFixtureReader.AssertAnswerFreeForContractTest(inert.RootElement);

        using JsonDocument hostileKey = JsonDocument.Parse("""{"nested":{"expected_answer":"x"}}""");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            SourceClaimFixtureReader.AssertAnswerFreeForContractTest(hostileKey.RootElement));
        using JsonDocument hostileValue = JsonDocument.Parse("""{"nested":{"value":"oracle authority"}}""");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            SourceClaimFixtureReader.AssertAnswerFreeForContractTest(hostileValue.RootElement));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void HistoricalSourceClaimAuditReadsOnlyImmutableManifestAndRawByteIdentities()
    {
        SourceClaimHistoricalAuditReceipt receipt = SourceClaimHistoricalAudit.Verify(
            PackageRoot("S6-CLAIM-DEV-v1"));
        Assert.AreEqual("S6-CLAIM-DEV-v1", receipt.PackageId);
        Assert.AreEqual(5, receipt.HistoricalFileCount);
        Assert.AreEqual(64, receipt.ManifestSha256.Length);
        Assert.AreEqual(64, receipt.OracleSha256.Length);
        Assert.AreEqual(64, receipt.RetainedTranscriptsSha256.Length);
        Assert.IsFalse(receipt.CurrentSemanticAuthority);
    }

    private static string PackageRoot(string package) => Path.Combine(
        TestRepository.Root, "fixtures", "public", "provider", "source-claims", package);
}
