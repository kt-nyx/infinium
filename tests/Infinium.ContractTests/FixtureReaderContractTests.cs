using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.EvaluatorV2.LegacyV1;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class FixtureReaderContractTests
{
    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Contract")]
    public void CompleteHarnessPackageValidatesFingerprintsIdentityPartitionAndOracle()
    {
        using FixturePackageTestBuilder fixture = new();

        EvaluationHarnessFixturePackage package = FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath);

        Assert.AreEqual("fixture-development-1", package.FixtureId.Value);
        Assert.AreEqual(FixturePartition.Development, package.Partition);
        Assert.AreEqual(ContractConstants.TaxonomyVersion, package.PublicManifest.GetProperty("taxonomy_version").GetString());
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void ExecutionReaderReturnsOnlyExecutionInput()
    {
        using FixturePackageTestBuilder fixture = new();

        ExecutionFixturePackage package = FixturePackageReader.ReadExecutionInput(
            fixture.FilePath(FixturePackageReader.ExecutionInputFileName));

        Assert.AreEqual("fixture-development-1", package.FixtureId.Value);
        Assert.IsFalse(package.ExecutionInput.TryGetProperty("oracle_fingerprint", out _));
        Assert.IsFalse(package.ExecutionInput.TryGetProperty("expected_findings", out _));
    }

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Fault")]
    public void HarnessReaderRefusesMissingFingerprint()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemovePublicProperty("oracle_fingerprint");

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Fault")]
    public void HarnessReaderRefusesMissingPartition()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemovePublicProperty("partition");

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Fault")]
    public void HarnessReaderRefusesMissingGroundTruth()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemoveOracleProperty("ground_truth_methods");

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Fault")]
    public void HarnessReaderRefusesMissingExpectedGapDeclaration()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemoveOracleProperty("expected_coverage_and_gaps");

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Fault")]
    public void HarnessReaderRefusesMissingTaxonomyVersion()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemovePublicProperty("taxonomy_version");

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Fault")]
    public void HarnessReaderRefusesUnsupportedPublicSchemaVersion()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.SetPublicString("schema_version", "2");

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Security")]
    public void ClosedExecutionSchemaRejectsNestedAnswerBearingProperties()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.AddNestedExecutionProperty(
            "installation_snapshot_input",
            "expected_findings",
            new JsonArray("the hidden answer"));

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadExecutionInput(
                fixture.FilePath(FixturePackageReader.ExecutionInputFileName)));
    }

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Fault")]
    [TestProperty("Category", "M1Contract")]
    public void ExecutionReaderRejectsMissingSchemaRequiredField()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.RemoveExecutionProperty("resource_and_time_limits");

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadExecutionInput(
                fixture.FilePath(FixturePackageReader.ExecutionInputFileName)));
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Security")]
    public void ExecutionReaderRejectsArbitraryExtensionObjects()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.AddExecutionProperty("metadata", new JsonObject { ["note"] = "not answer bearing" });

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadExecutionInput(
                fixture.FilePath(FixturePackageReader.ExecutionInputFileName)));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void PartitionVocabularyIsClosedAndCannotRelabelKnownAnswers()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.SetPublicString("partition", "known-answer-promoted-to-held-out");

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    public void KnownAnswerTransitionRequiresIndependentReplacementCoverage()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.AddKnownAnswerTransitionWithoutReplacement();

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }
}
