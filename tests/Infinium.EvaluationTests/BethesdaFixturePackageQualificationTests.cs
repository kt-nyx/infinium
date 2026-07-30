using Infinium.Application.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaFixturePackageQualificationTests
{
    private static readonly (string FixtureId, FixturePartition Partition)[] Fixtures =
    [
        ("BETH-NPC-DEV", FixturePartition.Development),
        ("BETH-REFR-DEV", FixturePartition.Development),
        ("BETH-LIGHT-VAL", FixturePartition.Validation),
        ("BETH-MALFORMED-VAL", FixturePartition.Validation),
        ("BETH-UNSUPPORTED-VAL", FixturePartition.Validation),
    ];

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    public void TrackedBethesdaPackagesAreCompleteBoundAndAccepted()
    {
        foreach ((string fixtureId, FixturePartition partition) in Fixtures)
        {
            string directory = TestRepository.PathFromRoot(
                "test-data",
                "evaluation",
                "m1-semantic",
                fixtureId);

            EvaluationHarnessFixturePackage package =
                FixturePackageReader.ReadForEvaluationHarness(directory);

            Assert.AreEqual(fixtureId, package.FixtureId.Value);
            Assert.AreEqual("1.0.0", package.FixtureVersion.ToString());
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
