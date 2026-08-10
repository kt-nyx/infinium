using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaFixturePackageQualificationTests
{
    private static readonly (string FixtureId, FixturePartition Partition)[] Fixtures =
    [
        ("BETH-NPC-DEV", FixturePartition.Development),
        ("BETH-REFR-DEV", FixturePartition.Development),
        ("BETH-LIGHT-VAL", FixturePartition.Development),
        ("BETH-MALFORMED-VAL", FixturePartition.Development),
        ("BETH-UNSUPPORTED-VAL", FixturePartition.Development),
    ];

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Evaluation")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Security")]
    public void TrackedBethesdaPackagesLoadThroughCurrentPublicFixtureContracts()
    {
        foreach ((string fixtureId, FixturePartition partition) in Fixtures)
        {
            string directory = TestRepository.PathFromRoot(
                "fixtures",
                "public",
                "bethesda",
                fixtureId);

            PublicFixturePackage package = PublicFixturePackageReader.Read(directory);

            Assert.AreEqual(fixtureId, package.FixtureId.Value);
            Assert.AreEqual("1.4.0", package.FixtureVersion.ToString());
            Assert.AreEqual(partition, package.Partition);
            Assert.AreEqual(
                "accepted",
                package.PublicManifest.GetProperty("review_state").GetString());
            Assert.IsTrue(
                package.PublicManifest
                    .GetProperty("evaluation_ids")
                    .EnumerateArray()
                    .Any(value => value.GetString() == "EVAL-0052"));
        }
    }
}
