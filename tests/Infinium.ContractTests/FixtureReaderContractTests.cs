using System.Text.Json.Nodes;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class FixtureReaderContractTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Contract")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Contract")]
    public void CompleteHarnessPackageValidatesFingerprintsIdentityPartitionAndOracle()
    {
        using FixturePackageTestBuilder fixture = new();

        PublicFixturePackage package = PublicFixturePackageReader.Read(fixture.DirectoryPath);

        Assert.AreEqual("fixture-development-1", package.FixtureId.Value);
        Assert.AreEqual(FixturePartition.Development, package.Partition);
        Assert.AreEqual(ContractConstants.TaxonomyVersion, package.PublicManifest.GetProperty("taxonomy_version").GetString());
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void ExecutionReaderReturnsOnlyExecutionInput()
    {
        using FixturePackageTestBuilder fixture = new();

        ExecutionFixturePackage package = PublicFixturePackageReader.ReadExecutionInput(
            fixture.FilePath(PublicFixturePackageReader.ExecutionInputFileName));

        Assert.AreEqual("fixture-development-1", package.FixtureId.Value);
        Assert.IsFalse(package.ExecutionInput.TryGetProperty("oracle_fingerprint", out _));
        Assert.IsFalse(package.ExecutionInput.TryGetProperty("expected_findings", out _));
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestProperty("Category", "Fault")]
    public void HarnessReaderRefusesMissingFingerprint()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemovePublicProperty("oracle_fingerprint");

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.Read(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestProperty("Category", "Fault")]
    public void HarnessReaderRefusesMissingPartition()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemovePublicProperty("partition");

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.Read(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestProperty("Category", "Fault")]
    public void HarnessReaderRefusesMissingGroundTruth()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemoveOracleProperty("ground_truth_methods");

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.Read(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestProperty("Category", "Fault")]
    public void HarnessReaderRefusesMissingExpectedGapDeclaration()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemoveOracleProperty("expected_coverage_and_gaps");

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.Read(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestProperty("Category", "Fault")]
    public void HarnessReaderRefusesMissingTaxonomyVersion()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemovePublicProperty("taxonomy_version");

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.Read(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestProperty("Category", "Fault")]
    public void HarnessReaderRefusesUnsupportedPublicSchemaVersion()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.SetPublicString("schema_version", "2");

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.Read(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("Security")]
    [TestProperty("Category", "Security")]
    public void ClosedExecutionSchemaRejectsNestedAnswerBearingProperties()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.AddNestedExecutionProperty(
            "installation_snapshot_input",
            "expected_findings",
            new JsonArray("the hidden answer"));

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.ReadExecutionInput(
                fixture.FilePath(PublicFixturePackageReader.ExecutionInputFileName)));
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestCategory("Contract")]
    [TestProperty("Category", "Fault")]
    [TestProperty("Category", "Contract")]
    public void ExecutionReaderRejectsMissingSchemaRequiredField()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemoveExecutionProperty("resource_and_time_limits");

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.ReadExecutionInput(
                fixture.FilePath(PublicFixturePackageReader.ExecutionInputFileName)));
    }

    [TestMethod]
    [TestCategory("Security")]
    [TestProperty("Category", "Security")]
    public void ExecutionReaderRejectsArbitraryExtensionObjects()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.AddExecutionProperty("metadata", new JsonObject { ["note"] = "not answer bearing" });

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.ReadExecutionInput(
                fixture.FilePath(PublicFixturePackageReader.ExecutionInputFileName)));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
    public void PartitionVocabularyIsClosedAndCannotRelabelKnownAnswers()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.SetPublicString("partition", "known-answer-promoted-to-held-out");

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.Read(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Fault")]
    public void KnownAnswerTransitionRequiresIndependentReplacementCoverage()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.AddKnownAnswerTransitionWithoutReplacement();

        Assert.ThrowsExactly<InvalidDataException>(
            () => PublicFixturePackageReader.Read(fixture.DirectoryPath));
    }
}
