using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
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
        ("BETH-MALFORMED-VAL", FixturePartition.Development),
        ("BETH-UNSUPPORTED-VAL", FixturePartition.Validation),
    ];
    private static readonly string[] HeldOutEvaluationIds = ["EVAL-0052"];
    private static readonly string[] HeldOutRegistryProperties =
    [
        "schema_id",
        "schema_version",
        "fixtures",
    ];
    private static readonly string[] HeldOutIdentityProperties = ["byte_length", "sha256"];
    private static readonly string[] HeldOutFixtureProperties =
    [
        "fixture_id",
        "fixture_version",
        "partition",
        "evaluation_ids",
        "purpose",
        "redistribution_class",
        "owner",
        "review_state",
        "created_at",
        "complete_package_retention",
        "public_manifest",
        "input_package",
        "oracle",
        "provenance",
        "replay_dependencies",
        "redistribution",
        "partition_history",
        "custodian_attestation",
    ];
    private static readonly string[] HeldOutAttestationProperties =
    [
        "construction_runs_byte_identical",
        "manual_and_independent_reader_agreed",
        "seven_documents_schema_valid",
        "answer_bearing_execution_mutations_rejected",
        "manifest_fingerprints_match_disclosed_documents",
        "development_fixture_accessed",
        "production_parser_or_output_accessed",
        "third_party_payload_consumed",
        "protected_root_accessed",
        "network_or_billable_provider_used",
        "evaluation_case_pass_claimed",
    ];
    private static readonly string[] EvaluatorPrivateFixtureProperties =
    [
        "fixture_id",
        "fixture_version",
        "partition",
        "evaluation_ids",
        "purpose",
        "classification",
        "owner",
        "review_state",
        "reviewed_at",
        "complete_package_retention",
        "input_package_fingerprint",
        "retained_input_set_fingerprint",
        "oracle_fingerprint",
        "documents",
        "independence_evidence",
        "corrective_authority_id",
        "custodian_attestation",
    ];
    private static readonly string[] EvaluatorPrivateDocumentProperties =
    [
        "public_manifest",
        "execution_input",
        "expected_oracle",
        "provenance",
        "replay_dependencies",
        "redistribution",
        "partition_history",
    ];
    private static readonly string[] EvaluatorPrivateEvidenceProperties =
    [
        "artifact_id",
        "artifact_version",
        "fingerprint",
        "availability",
    ];
    private static readonly string[] EvaluatorPrivateAttestationProperties =
    [
        "two_independent_raw_byte_methods_agreed",
        "two_clean_constructions_byte_identical",
        "seven_root_documents_schema_valid",
        "supplemental_oracle_schema_valid",
        "cross_document_fingerprints_valid",
        "execution_input_isolation_passed",
        "predecessor_package_accessed",
        "prior_reviewer_output_accessed",
        "production_parser_or_output_accessed",
        "mutagen_or_xedit_used",
        "third_party_payload_consumed",
        "protected_root_accessed",
        "network_or_billable_provider_used",
        "evaluation_case_pass_claimed",
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

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    public void HeldOutRegistryRetainsOnlySealedPublicMetadata()
    {
        string semanticRoot = TestRepository.PathFromRoot(
            "test-data",
            "evaluation",
            "m1-semantic");
        string registryPath = Path.Combine(semanticRoot, "held-out-registry.json");
        using BoundedJsonDocumentSnapshot snapshot =
            BoundedJsonDocumentReader.Read(registryPath, 1024 * 1024, maximumDepth: 16);
        JsonElement root = snapshot.Document.RootElement;
        CollectionAssert.AreEquivalent(
            HeldOutRegistryProperties,
            root.EnumerateObject().Select(property => property.Name).ToArray());

        Assert.AreEqual(
            "infinium.evaluation.held-out-fixture-registry/v1",
            root.GetProperty("schema_id").GetString());
        Assert.AreEqual("1", root.GetProperty("schema_version").GetString());
        JsonElement fixture = root.GetProperty("fixtures").EnumerateArray().Single();
        CollectionAssert.AreEquivalent(
            HeldOutFixtureProperties,
            fixture.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("BETH-HO-001", fixture.GetProperty("fixture_id").GetString());
        Assert.AreEqual("held-out", fixture.GetProperty("partition").GetString());
        Assert.AreEqual("sealed", fixture.GetProperty("review_state").GetString());
        Assert.AreEqual(
            "evaluator-private",
            fixture.GetProperty("complete_package_retention").GetString());
        CollectionAssert.AreEqual(
            HeldOutEvaluationIds,
            fixture.GetProperty("evaluation_ids")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());

        foreach (string propertyName in new[]
        {
            "public_manifest",
            "input_package",
            "oracle",
            "provenance",
            "replay_dependencies",
            "redistribution",
            "partition_history",
        })
        {
            JsonElement identity = fixture.GetProperty(propertyName);
            CollectionAssert.AreEquivalent(
                HeldOutIdentityProperties,
                identity.EnumerateObject().Select(property => property.Name).ToArray(),
                propertyName);
            Assert.IsTrue(identity.GetProperty("byte_length").GetInt64() > 0, propertyName);
            string fingerprintText = identity.GetProperty("sha256").GetString()!;
            Sha256Fingerprint fingerprint = new(fingerprintText);
            Assert.AreEqual(fingerprint.Value, fingerprintText, propertyName);
        }

        JsonElement attestation = fixture.GetProperty("custodian_attestation");
        CollectionAssert.AreEquivalent(
            HeldOutAttestationProperties,
            attestation.EnumerateObject().Select(property => property.Name).ToArray());
        foreach (JsonProperty property in attestation.EnumerateObject())
        {
            bool expected = property.Name is
                "construction_runs_byte_identical"
                or "manual_and_independent_reader_agreed"
                or "seven_documents_schema_valid"
                or "answer_bearing_execution_mutations_rejected"
                or "manifest_fingerprints_match_disclosed_documents";
            Assert.AreEqual(expected, property.Value.GetBoolean(), property.Name);
        }

        string rawText = File.ReadAllText(registryPath);
        foreach (string forbidden in new[]
        {
            "\"path\"",
            "\"locator\"",
            "\"payload\"",
            "\"answers\"",
            "C:\\\\",
            "Z:\\\\",
            "../",
        })
        {
            Assert.IsFalse(
                rawText.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                forbidden);
        }

        Assert.IsFalse(
            Directory.Exists(Path.Combine(semanticRoot, "BETH-HO-001")),
            "A public partial held-out package must not be presented as executable.");
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    public void EvaluatorPrivateReplacementRegistryRetainsOnlyBoundPublicMetadata()
    {
        string semanticRoot = TestRepository.PathFromRoot(
            "test-data",
            "evaluation",
            "m1-semantic");
        string registryPath = Path.Combine(
            semanticRoot,
            "evaluator-private-registry.json");
        using BoundedJsonDocumentSnapshot snapshot =
            BoundedJsonDocumentReader.Read(registryPath, 1024 * 1024, maximumDepth: 16);
        JsonElement root = snapshot.Document.RootElement;
        CollectionAssert.AreEquivalent(
            HeldOutRegistryProperties,
            root.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual(
            "infinium.evaluation.evaluator-private-fixture-registry/v1",
            root.GetProperty("schema_id").GetString());
        Assert.AreEqual("1", root.GetProperty("schema_version").GetString());

        JsonElement fixture = root.GetProperty("fixtures").EnumerateArray().Single();
        CollectionAssert.AreEquivalent(
            EvaluatorPrivateFixtureProperties,
            fixture.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual(
            "BETH-MALFORMED-VAL-002",
            fixture.GetProperty("fixture_id").GetString());
        Assert.AreEqual("validation", fixture.GetProperty("partition").GetString());
        Assert.AreEqual("sealed", fixture.GetProperty("review_state").GetString());
        Assert.AreEqual(
            "evaluator-private",
            fixture.GetProperty("complete_package_retention").GetString());
        CollectionAssert.AreEqual(
            HeldOutEvaluationIds,
            fixture.GetProperty("evaluation_ids")
                .EnumerateArray()
                .Select(value => value.GetString())
                .ToArray());

        JsonElement documents = fixture.GetProperty("documents");
        CollectionAssert.AreEquivalent(
            EvaluatorPrivateDocumentProperties,
            documents.EnumerateObject().Select(property => property.Name).ToArray());
        foreach (JsonProperty document in documents.EnumerateObject())
        {
            CollectionAssert.AreEquivalent(
                HeldOutIdentityProperties,
                document.Value.EnumerateObject().Select(property => property.Name).ToArray(),
                document.Name);
            Assert.IsTrue(
                document.Value.GetProperty("byte_length").GetInt64() > 0,
                document.Name);
            string fingerprintText = document.Value.GetProperty("sha256").GetString()!;
            Sha256Fingerprint fingerprint = new(fingerprintText);
            Assert.AreEqual(fingerprint.Value, fingerprintText, document.Name);
        }

        Assert.AreEqual(
            documents.GetProperty("execution_input").GetProperty("sha256").GetString(),
            fixture.GetProperty("input_package_fingerprint").GetString());
        Assert.AreEqual(
            documents.GetProperty("expected_oracle").GetProperty("sha256").GetString(),
            fixture.GetProperty("oracle_fingerprint").GetString());
        _ = new Sha256Fingerprint(
            fixture.GetProperty("retained_input_set_fingerprint").GetString()!);

        JsonElement evidence = fixture.GetProperty("independence_evidence");
        CollectionAssert.AreEquivalent(
            EvaluatorPrivateEvidenceProperties,
            evidence.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual(
            "evaluator-private",
            evidence.GetProperty("availability").GetString());
        _ = new Sha256Fingerprint(evidence.GetProperty("fingerprint").GetString()!);
        Assert.AreEqual(
            "project-owner/user-directed-replacement-review-20260730",
            fixture.GetProperty("corrective_authority_id").GetString());

        using BoundedJsonDocumentSnapshot predecessorHistorySnapshot =
            BoundedJsonDocumentReader.Read(
                Path.Combine(
                    semanticRoot,
                    "BETH-MALFORMED-VAL",
                    FixturePackageReader.PartitionHistoryFileName),
                1024 * 1024,
                maximumDepth: 16);
        JsonElement transition = predecessorHistorySnapshot.Document.RootElement
            .GetProperty("partition_history")
            .EnumerateArray()
            .Last();
        Assert.AreEqual("validation", transition.GetProperty("from").GetString());
        Assert.AreEqual("development", transition.GetProperty("to").GetString());
        Assert.IsTrue(
            transition.GetProperty("change_influenced_implementation").GetBoolean());
        Assert.AreEqual(
            fixture.GetProperty("fixture_id").GetString(),
            transition.GetProperty("replacement_fixture_id").GetString());
        Assert.AreEqual(
            fixture.GetProperty("partition").GetString(),
            transition.GetProperty("replacement_partition").GetString());
        Assert.AreEqual(
            fixture.GetProperty("input_package_fingerprint").GetString(),
            transition.GetProperty("replacement_input_package_fingerprint").GetString());
        Assert.AreEqual(
            fixture.GetProperty("oracle_fingerprint").GetString(),
            transition.GetProperty("replacement_oracle_fingerprint").GetString());
        Assert.AreEqual(
            fixture.GetProperty("corrective_authority_id").GetString(),
            transition.GetProperty("authorized_by").GetString());
        JsonElement transitionEvidence =
            transition.GetProperty("independence_evidence_reference");
        foreach (string propertyName in EvaluatorPrivateEvidenceProperties)
        {
            Assert.AreEqual(
                evidence.GetProperty(propertyName).GetRawText(),
                transitionEvidence.GetProperty(propertyName).GetRawText(),
                propertyName);
        }

        JsonElement attestation = fixture.GetProperty("custodian_attestation");
        CollectionAssert.AreEquivalent(
            EvaluatorPrivateAttestationProperties,
            attestation.EnumerateObject().Select(property => property.Name).ToArray());
        foreach (JsonProperty property in attestation.EnumerateObject())
        {
            bool expected = property.Name is
                "two_independent_raw_byte_methods_agreed"
                or "two_clean_constructions_byte_identical"
                or "seven_root_documents_schema_valid"
                or "supplemental_oracle_schema_valid"
                or "cross_document_fingerprints_valid"
                or "execution_input_isolation_passed";
            Assert.AreEqual(expected, property.Value.GetBoolean(), property.Name);
        }

        string rawText = File.ReadAllText(registryPath);
        foreach (string forbidden in new[]
        {
            "\"path\"",
            "\"locator\"",
            "\"payload\"",
            "\"answers\"",
            "C:\\\\",
            "Z:\\\\",
            "../",
        })
        {
            Assert.IsFalse(
                rawText.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                forbidden);
        }

        Assert.IsFalse(
            Directory.Exists(Path.Combine(semanticRoot, "BETH-MALFORMED-VAL-002")),
            "A public partial evaluator-private package must not be presented as executable.");
    }
}
