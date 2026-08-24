using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class SourceClaimTransparencyEvaluationTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    public void HistoricalSourceClaimPackagesRemainByteBoundAuditVisibleAndNonAuthorizing()
    {
        foreach (string packageId in new[] { "S6-CLAIM-DEV-v1", "S6-CLAIM-VAL-v1" })
        {
            string root = Path.Combine(RepositoryRoot(), "fixtures", "public", "provider", "source-claims", packageId);
            SourceClaimHistoricalAuditReceipt receipt = SourceClaimHistoricalAudit.Verify(root);
            Assert.AreEqual(packageId, receipt.PackageId);
            Assert.IsTrue(receipt.HistoricalFileCount > 0);
            Assert.AreEqual(64, receipt.ManifestSha256.Length);
            Assert.AreEqual(64, receipt.OracleSha256.Length);
            Assert.AreEqual(64, receipt.RetainedTranscriptsSha256.Length);
            Assert.IsFalse(receipt.CurrentSemanticAuthority);
        }
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void HistoricalValidationClaimAuthorityIsExplicitlyReclassifiedWithoutChangingRetainedManifest()
    {
        string root = Path.Combine(RepositoryRoot(), "fixtures", "public", "provider", "source-claims",
            "S6-CLAIM-VAL-v1");
        SourceClaimHistoricalAuditReceipt receipt = SourceClaimHistoricalAudit.Verify(root);
        Assert.AreEqual("0f95265340873dc4abb083c6f857db9e8786c6e1ba36da385f07c876afe1c13f",
            receipt.ManifestSha256);
        Assert.IsFalse(receipt.CurrentSemanticAuthority);

        string reclassification = File.ReadAllText(Path.Combine(root, "reclassification.v1.json"));
        StringAssert.Contains(reclassification,
            "historical-development-evidence-clean-break-semantic-contract");
        Assert.IsFalse(File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts", "json-schema",
                "source-claim-extraction.v1.schema.json"))
            .Contains("finding", StringComparison.OrdinalIgnoreCase));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
