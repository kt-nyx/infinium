using System.Text;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class RunOutputJsonCodecTests
{
    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void StableRunOutputRoundTripsThroughItsPublishedSchema()
    {
        RunOutputContract expected = CreateValidOutput();

        byte[] json = RunOutputJsonCodec.Serialize(expected);
        RunOutputContract actual = RunOutputJsonCodec.Deserialize(json);

        Assert.AreEqual(expected.SchemaId, actual.SchemaId);
        Assert.AreEqual(expected.RunId, actual.RunId);
        Assert.HasCount(1, actual.Observations);
        Assert.AreEqual("observation-1", actual.Observations[0].ArtifactId);
        Assert.AreEqual("populated", actual.CollectionStates["observations"].State);
        CollectionAssert.AreEqual(json, RunOutputJsonCodec.Serialize(actual));
        string text = Encoding.UTF8.GetString(json);
        StringAssert.Contains(text, "\"schema_id\": \"infinium.run-output/v1\"");
        StringAssert.Contains(text, "\"artifact_type\": \"observation\"");
        Assert.IsFalse(text.Contains("SchemaId", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Fault")]
    public void StableRunOutputRejectsCollectionTypeDriftAndDuplicateKeys()
    {
        string json = Encoding.UTF8.GetString(RunOutputJsonCodec.Serialize(CreateValidOutput()));
        string wrongType = json.Replace(
            "\"artifact_type\": \"observation\"",
            "\"artifact_type\": \"finding\"",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidDataException>(
            () => RunOutputJsonCodec.Deserialize(Encoding.UTF8.GetBytes(wrongType)));

        string duplicateSchemaId = json.Replace(
            "\"schema_id\": \"infinium.run-output/v1\",",
            "\"schema_id\": \"infinium.run-output/v1\", \"schema_id\": \"infinium.run-output/v1\",",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidDataException>(
            () => RunOutputJsonCodec.Deserialize(Encoding.UTF8.GetBytes(duplicateSchemaId)));
    }

    private static RunOutputContract CreateValidOutput()
    {
        string fingerprint = new('a', 64);
        ArtifactReferenceDocumentContract Reference(string id) =>
            new(id, "1.0.0", fingerprint, "retained");
        ArtifactProvenanceDocumentContract provenance = new(
            "analyzer-1",
            "1.0.0",
            "run-1",
            [Reference("snapshot-1")],
            [],
            [],
            new LlmInvolvementDocumentContract("none", "none", null));
        TypedArtifactDocumentContract observation = new(
            "observation-1",
            1,
            "observation",
            "present",
            Reference("observation-payload-1"),
            provenance);
        string[] collectionNames =
        [
            "observations",
            "deterministic_results",
            "external_claims",
            "application_links",
            "discovery_leads",
            "model_proposals",
            "proposal_admissions",
            "candidates",
            "hypotheses",
            "findings",
            "recommendations",
            "supported_cases",
            "lead_only_cases",
            "abstentions",
            "invalid_inputs",
            "coverage_gaps",
            "failures",
        ];
        Dictionary<string, RunOutputCollectionStateContract> collectionStates =
            collectionNames.ToDictionary(
                name => name,
                name => new RunOutputCollectionStateContract(
                    name == "observations" ? "populated" : "empty",
                    name == "observations" ? "one retained observation" : "none produced"),
                StringComparer.Ordinal);
        CoverageDocumentContract coverage = new(
            "coverage-1",
            "analyzer-1",
            "population-1",
            "eligible inputs",
            1,
            1,
            "completed",
            ContractConstants.TaxonomyId,
            ContractConstants.TaxonomyVersion,
            [],
            [],
            [],
            []);

        return new RunOutputContract(
            ContractConstants.RunOutputSchemaId,
            "1",
            "run-1",
            "analysis",
            "completed",
            new string('b', 40),
            "1970-01-01T00:00:00.0000000+00:00",
            "1970-01-01T00:00:01.0000000+00:00",
            Reference("snapshot-1"),
            Reference("context-1"),
            Reference("configuration-1"),
            Reference("input-manifest-1"),
            ContractConstants.TaxonomyId,
            ContractConstants.TaxonomyVersion,
            [Reference("analyzer-declaration-1")],
            [observation],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            collectionStates,
            [],
            [coverage],
            [new ExcludedCapabilityDocumentContract("runtime-assets", "unsupported", "outside M1")],
            new ReadinessDocumentContract("no-readiness-evaluation", "none", true),
            new ReplayabilityDocumentContract(
                "complete",
                "audit-only",
                Reference("dependency-manifest-1"),
                []),
            new AuditabilityDocumentContract("complete", []),
            fingerprint,
            []);
    }
}
