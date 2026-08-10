using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

public sealed partial class FixtureSchemaIntegrityTests
{
    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Evaluation")]
    [TestCategory("Security")]
    public void BethesdaCaseMatrixAndScanConfigurationHaveDistinctAcceptedRoles()
    {
        string root = TestRepository.PathFromRoot(
            "fixtures", "public", "bethesda", "BETH-UNSUPPORTED-VAL");
        _ = PublicFixturePackageReader.Read(root);

        JsonObject configuration = ReadObject(
            root,
            "inputs/effective-scan-configuration.json");
        Assert.IsFalse(configuration.ContainsKey("cases"));
        Assert.IsFalse(configuration.ContainsKey("scenarios"));

        JsonObject matrix = ReadObject(root, "inputs/case-matrix.json");
        Assert.AreEqual(
            "infinium.evaluation.bethesda-case-matrix/v1",
            matrix["schema_id"]!.GetValue<string>());
        Assert.IsGreaterThan(0, matrix["cases"]!.AsArray().Count);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Fault")]
    [TestCategory("Security")]
    public void BethesdaExecutionControlsRejectMissingSwappedDuplicateUnsealedAndStaleBindings()
    {
        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution.Remove("case_matrix_input");
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            JsonNode? configuration = execution["effective_scan_configuration"];
            execution["effective_scan_configuration"] =
                execution["case_matrix_input"]!["artifact"]!.DeepClone();
            execution["case_matrix_input"]!["artifact"] = configuration!.DeepClone();
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            JsonNode duplicate = execution["input_payload_refs"]!.AsArray()[0]!.DeepClone();
            execution["input_payload_refs"]!.AsArray().Add(duplicate);
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertCaseMatrixMutationRejected(matrix =>
            matrix["cases"]!.AsArray()[0]!["input_artifact_ids"]!.AsArray()
                .Add("inputs/unsealed.esp"));
        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution["case_matrix_input"]!["artifact"]!["fingerprint"] =
                new string('0', 64);
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertTaxonomyPackageMutationRejected(root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution["input_payload_refs"]!.AsArray().RemoveAt(0);
            WriteExecutionAndResealManifest(root, execution);
        });
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Fault")]
    [TestCategory("Security")]
    public void BethesdaAcceptedOrderConstructionRoleRejectsDowngradeSubstitutionAndReceiptDrift()
    {
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution.Remove("accepted_order_construction_input");
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution["accepted_order_construction_input"] = new JsonObject
            {
                ["state"] = "not-applicable",
                ["reason"] = "Declaration-downgrade probe.",
            };
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution["accepted_order_construction_input"] =
                execution["installation_snapshot_input"]!.DeepClone();
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution["accepted_order_construction_input"] =
                execution["plugin_order_input"]!.DeepClone();
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution["installation_snapshot_input"] =
                execution["accepted_order_construction_input"]!.DeepClone();
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution["plugin_order_input"] =
                execution["accepted_order_construction_input"]!.DeepClone();
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution["accepted_order_construction_input"]!["artifact"] =
                execution["case_matrix_input"]!["artifact"]!.DeepClone();
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution["accepted_order_construction_input"]!["artifact"] = new JsonObject
            {
                ["artifact_id"] = "inputs/snapshot/unsealed.json",
                ["artifact_version"] = "1.3.0",
                ["fingerprint"] = new string('0', 64),
                ["availability"] = "retained",
            };
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            JsonObject acceptedReference = execution["accepted_order_construction_input"]!
                ["artifact"]!.AsObject();
            acceptedReference["fingerprint"] = new string('0', 64);
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            JsonObject acceptedPayloadReference = execution["input_payload_refs"]!.AsArray()
                .Select(item => item!.AsObject())
                .Single(item => item["artifact_id"]!.GetValue<string>()
                    == "inputs/snapshot/accepted-order.json");
            execution["input_payload_refs"]!.AsArray().Add(acceptedPayloadReference.DeepClone());
            WriteExecutionAndResealManifest(root, execution);
        });
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            string executionPath = Path.Combine(
                root,
                PublicFixturePackageReader.ExecutionInputFileName);
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            string duplicateValue = execution["accepted_order_construction_input"]!
                .ToJsonString();
            string executionJson = execution.ToJsonString();
            executionJson = executionJson.Replace(
                "\"accepted_order_construction_input\":",
                $"\"accepted_order_construction_input\":{duplicateValue},"
                    + "\"accepted_order_construction_input\":",
                StringComparison.Ordinal);
            File.WriteAllText(executionPath, executionJson);
            JsonObject manifest = ReadObject(
                root,
                PublicFixturePackageReader.PublicManifestFileName);
            manifest["input_package_fingerprint"] = Sha256(executionPath);
            WriteJson(
                Path.Combine(root, PublicFixturePackageReader.PublicManifestFileName),
                manifest);
        });

        AssertAcceptedOrderReceiptMutationRejected(receipt =>
            receipt["schema_id"] = "infinium.evaluation.wrong-accepted-order-input/v1");
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
            receipt["fixture_id"] = "BETH-WRONG-DEV");
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
            receipt["fixture_version"] = "1.2.0");
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
            receipt["source_basis"] = "installation-snapshot");
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
            receipt["construction_manifest_fingerprint"] = new string('0', 64));
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
            receipt["provider_order"]!.AsArray()[0]!["source_sha256"] = new string('0', 64));
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
        {
            JsonArray providers = receipt["provider_order"]!.AsArray();
            providers[0]!["provider_id"] = "changed-provider";
            receipt["plugin_order"]!.AsArray()[0]!["provider_id"] = "changed-provider";
            RefreshAcceptedOrderCaptureFingerprint(receipt);
        });
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
        {
            receipt["plugin_order"] = new JsonArray(receipt["plugin_order"]!.AsArray()
                .Reverse()
                .Select(item => item!.DeepClone())
                .ToArray());
            receipt["provider_order"] = new JsonArray(receipt["provider_order"]!.AsArray()
                .Reverse()
                .Select(item => item!.DeepClone())
                .ToArray());
            RefreshAcceptedOrderCaptureFingerprint(receipt);
        });
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
            receipt["plugin_order"]!.AsArray()[0]!["sha256"] = new string('0', 64));
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
            receipt["isolated_capture_variants"]!.AsArray()[0]!["sha256"] =
                new string('0', 64));
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
            receipt["isolated_capture_variants"]!.AsArray().RemoveAt(0));
        AssertAcceptedOrderReceiptMutationRejected(receipt =>
            receipt["expected_capture_binding_fingerprint"] = new string('0', 64));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Fault")]
    [TestCategory("Security")]
    public void AcceptedBethesdaIdentityRejectsFullyResealedProtectedArtifactDowngrades()
    {
        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            const string artifactId = "oracle/independent-byte-facts.json";
            JsonObject oracle = ReadObject(root, PublicFixturePackageReader.OracleFileName);
            RemoveArtifactReferences(oracle, artifactId);
            File.Delete(Path.Combine(root, artifactId.Replace('/', Path.DirectorySeparatorChar)));
            WriteRootOracleAndResealManifest(root, oracle);

            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            JsonObject constructionReference = execution["input_payload_refs"]!.AsArray()
                .Select(item => item!.AsObject())
                .Single(item => item["artifact_id"]!.GetValue<string>()
                    == "inputs/construction-manifest.json");
            execution["effective_scan_configuration"] = constructionReference.DeepClone();
            execution["case_matrix_input"] = new JsonObject
            {
                ["state"] = "not-applicable",
                ["reason"] = "Resealed downgrade probe.",
            };
            WriteExecutionAndResealManifest(root, execution);
        });

        AssertBethesdaPackageMutationRejected("BETH-UNSUPPORTED-VAL", root =>
        {
            RemoveRetainedInputAndReseal(root, "inputs/taxonomy-subject-bindings.json");
            const string artifactId = "oracle/taxonomy-projections.json";
            JsonObject oracle = ReadObject(root, PublicFixturePackageReader.OracleFileName);
            RemoveArtifactReferences(oracle, artifactId);
            File.Delete(Path.Combine(root, artifactId.Replace('/', Path.DirectorySeparatorChar)));
            WriteRootOracleAndResealManifest(root, oracle);
        });

        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            RemoveRetainedInputAndReseal(root, "inputs/case-matrix.json");
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            execution["case_matrix_input"] = new JsonObject
            {
                ["state"] = "not-applicable",
                ["reason"] = "Resealed downgrade probe.",
            };
            WriteExecutionAndResealManifest(root, execution);
        });

        AssertBethesdaPackageMutationRejected("BETH-LIGHT-VAL", root =>
        {
            RemoveRetainedInputAndReseal(
                root,
                "inputs/effective-scan-configuration.json");
            JsonObject execution = ReadObject(root, PublicFixturePackageReader.ExecutionInputFileName);
            JsonObject constructionReference = execution["input_payload_refs"]!.AsArray()
                .Select(item => item!.AsObject())
                .Single(item => item["artifact_id"]!.GetValue<string>()
                    == "inputs/construction-manifest.json");
            execution["effective_scan_configuration"] = constructionReference.DeepClone();
            WriteExecutionAndResealManifest(root, execution);
        });
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Fault")]
    public void BethesdaCaseMatrixRejectsWrongSchemaNonListCasesDuplicateIdsUnsupportedOperationsAndArityErrors()
    {
        AssertCaseMatrixMutationRejected(matrix =>
            matrix["schema_id"] = "infinium.evaluation.wrong-case-matrix/v1");
        AssertCaseMatrixMutationRejected(matrix =>
            matrix["cases"] = new JsonObject());
        AssertCaseMatrixMutationRejected(matrix =>
        {
            JsonArray cases = matrix["cases"]!.AsArray();
            cases[1]!["scenario_id"] = cases[0]!["scenario_id"]!.GetValue<string>();
        });
        AssertCaseMatrixMutationRejected(matrix =>
            matrix["cases"]!.AsArray()[0]!["operation"] = "score");
        AssertCaseMatrixMutationRejected(matrix =>
        {
            JsonObject scenario = matrix["cases"]!.AsArray()[0]!.AsObject();
            scenario["operation"] = "compare";
            scenario["input_artifact_ids"]!.AsArray().RemoveAt(0);
        });
        AssertCaseMatrixMutationRejected(matrix =>
        {
            JsonObject scenario = matrix["cases"]!.AsArray()[0]!.AsObject();
            scenario["operation"] = "request";
            scenario["input_artifact_ids"]!.AsArray()
                .Add("inputs/plugins/UnsupportedNpcField.esp");
        });
        AssertCaseMatrixMutationRejected(matrix =>
            matrix["cases"]!.AsArray()[0]!["input_artifact_ids"] = new JsonArray());
        AssertCaseMatrixMutationRejected(matrix =>
        {
            JsonObject scenario = matrix["cases"]!.AsArray()[0]!.AsObject();
            scenario["operation"] = "request";
            scenario["input_artifact_ids"] =
                new JsonArray("inputs/plugins/UnsupportedFamily.esp");
        });
    }

}
