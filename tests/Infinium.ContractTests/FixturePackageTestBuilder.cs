using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

internal sealed class FixturePackageTestBuilder : IDisposable
{
    private readonly string directory;

    internal FixturePackageTestBuilder()
    {
        directory = Path.Combine(Path.GetTempPath(), "infinium-slice1-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        WriteValidPackage();
    }

    internal string DirectoryPath => directory;

    internal string FilePath(string fileName) => Path.Combine(directory, fileName);

    internal void RemovePublicProperty(string propertyName)
    {
        MutateObject(FixturePackageReader.PublicManifestFileName, root => root.Remove(propertyName));
    }

    internal void RemoveOracleProperty(string propertyName)
    {
        MutateObject(FixturePackageReader.OracleFileName, root => root.Remove(propertyName));
        RefreshFingerprint("oracle_fingerprint", FixturePackageReader.OracleFileName);
    }

    internal void AddExecutionProperty(string propertyName, JsonNode value)
    {
        MutateObject(FixturePackageReader.ExecutionInputFileName, root => root[propertyName] = value);
        RefreshFingerprint("input_package_fingerprint", FixturePackageReader.ExecutionInputFileName);
    }

    internal void AddNestedExecutionProperty(
        string parentPropertyName,
        string propertyName,
        JsonNode value)
    {
        MutateObject(
            FixturePackageReader.ExecutionInputFileName,
            root => root[parentPropertyName]!.AsObject()[propertyName] = value);
        RefreshFingerprint("input_package_fingerprint", FixturePackageReader.ExecutionInputFileName);
    }

    internal void RemoveExecutionProperty(string propertyName)
    {
        MutateObject(FixturePackageReader.ExecutionInputFileName, root => root.Remove(propertyName));
        RefreshFingerprint("input_package_fingerprint", FixturePackageReader.ExecutionInputFileName);
    }

    internal void SetPublicString(string propertyName, string value)
    {
        MutateObject(FixturePackageReader.PublicManifestFileName, root => root[propertyName] = value);
    }

    internal void AddKnownAnswerTransitionWithoutReplacement()
    {
        JsonArray history = new()
        {
            PartitionTransition(null, "development", "initial registration", false),
            PartitionTransition("development", "validation", "independent validation admission", false),
            PartitionTransition("validation", "development", "result influenced implementation", true),
        };
        MutateObject(
            FixturePackageReader.PublicManifestFileName,
            root =>
            {
                root["partition"] = "development";
                root["partition_history"] = history.DeepClone();
            });
        MutateObject(
            FixturePackageReader.PartitionHistoryFileName,
            root => root["partition_history"] = history.DeepClone());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private void WriteValidPackage()
    {
        JsonObject execution = new()
        {
            ["fixture_id"] = "fixture-development-1",
            ["fixture_version"] = "1.0.0",
            ["installation_snapshot_input"] = EmptyInputComponent(),
            ["analysis_context_input"] = EmptyInputComponent(),
            ["effective_scan_configuration"] = ArtifactReference("effective-configuration"),
            ["runtime_support_input"] = EmptyInputComponent(),
            ["mo2_instance_profile_input"] = EmptyInputComponent(),
            ["plugin_order_input"] = EmptyInputComponent(),
            ["provider_order_input"] = EmptyInputComponent(),
            ["source_claim_inputs"] = new JsonArray(),
            ["analyzer_declarations"] = new JsonArray(),
            ["tool_library_versions"] = new JsonArray(),
            ["declared_archive_state"] = new JsonObject
            {
                ["state"] = "unsupported",
                ["reason"] = "Archive support is outside the M1 baseline.",
            },
            ["declared_supported_capabilities"] = new JsonArray(),
            ["declared_unsupported_capabilities"] = new JsonArray
            {
                new JsonObject
                {
                    ["capability_id"] = "archive-positive-facegen",
                    ["reason"] = "Archive-positive behavior is not supported.",
                },
            },
            ["resource_and_time_limits"] = new JsonObject
            {
                ["wall_time_ms"] = 1_000,
                ["memory_bytes"] = 1_048_576,
                ["input_bytes"] = 0,
                ["output_bytes"] = 1_048_576,
            },
            ["input_payload_refs"] = new JsonArray(),
        };
        WriteJson(FixturePackageReader.ExecutionInputFileName, execution);

        JsonObject oracle = new()
        {
            ["fixture_id"] = "fixture-development-1",
            ["fixture_version"] = "1.0.0",
            ["oracle_version"] = "1.0.0",
            ["independent_authors_and_reviewers"] = new JsonArray("independent-reviewer"),
            ["ground_truth_methods"] = new JsonArray
            {
                new JsonObject
                {
                    ["method_id"] = "project-authored-method",
                    ["method"] = "Independent project-authored contract fixture review.",
                    ["evidence_references"] = new JsonArray
                    {
                        ArtifactReference("independent-ground-truth-evidence"),
                    },
                    ["independent_of_system_under_test"] = true,
                },
            },
            ["expected_observations"] = new JsonArray(),
            ["expected_deterministic_results"] = new JsonArray(),
            ["expected_external_claims"] = new JsonArray(),
            ["expected_application_links"] = new JsonArray(),
            ["expected_discovery_leads"] = new JsonArray(),
            ["expected_model_proposals"] = new JsonArray(),
            ["expected_proposal_admissions"] = new JsonArray(),
            ["expected_candidates"] = new JsonArray(),
            ["expected_hypotheses"] = new JsonArray(),
            ["expected_findings"] = new JsonArray(),
            ["expected_recommendations"] = new JsonArray(),
            ["expected_supported_cases"] = new JsonArray(),
            ["expected_lead_only_cases"] = new JsonArray(),
            ["expected_abstentions"] = new JsonArray(),
            ["expected_invalid_inputs"] = new JsonArray(),
            ["expected_coverage_and_gaps"] = new JsonArray(),
            ["expected_taxonomy_assignments"] = new JsonArray(),
            ["expected_replayability"] = "complete-clean",
            ["forbidden_claims"] = new JsonArray(),
            ["known_limits"] = new JsonArray("contract-only fixture"),
            ["pre_registered_at"] = DateTimeOffset.UnixEpoch.ToString("O"),
            ["change_history"] = new JsonArray(),
        };
        WriteJson(FixturePackageReader.OracleFileName, oracle);

        WriteJson(
            FixturePackageReader.ProvenanceFileName,
            new JsonObject
            {
                ["fixture_id"] = "fixture-development-1",
                ["fixture_version"] = "1.0.0",
                ["created_by"] = "project-authored-test",
            });
        WriteJson(
            FixturePackageReader.ReplayDependenciesFileName,
            new JsonObject
            {
                ["fixture_id"] = "fixture-development-1",
                ["fixture_version"] = "1.0.0",
                ["expected_replay_state"] = "complete-clean",
                ["dependencies"] = new JsonArray(),
            });
        WriteJson(
            FixturePackageReader.RedistributionFileName,
            new JsonObject
            {
                ["fixture_id"] = "fixture-development-1",
                ["fixture_version"] = "1.0.0",
                ["redistribution_class"] = "project-authored",
            });

        JsonArray history = new()
        {
            new JsonObject
            {
                ["from"] = null,
                ["to"] = "development",
                ["at"] = DateTimeOffset.UnixEpoch.ToString("O"),
                ["reason"] = "initial registration",
                ["change_influenced_implementation"] = false,
            },
        };
        WriteJson(
            FixturePackageReader.PartitionHistoryFileName,
            new JsonObject
            {
                ["fixture_id"] = "fixture-development-1",
                ["fixture_version"] = "1.0.0",
                ["partition_history"] = history.DeepClone(),
            });

        JsonObject manifest = new()
        {
            ["schema_id"] = "infinium.evaluation.fixture-public-manifest/v1",
            ["schema_version"] = "1",
            ["fixture_id"] = "fixture-development-1",
            ["fixture_version"] = "1.0.0",
            ["evaluation_ids"] = new JsonArray("EVAL-0065"),
            ["purpose"] = "contract-only reader fixture",
            ["classification"] = "boundary",
            ["partition"] = "development",
            ["partition_history"] = history,
            ["taxonomy_id"] = ContractConstants.TaxonomyId,
            ["taxonomy_version"] = ContractConstants.TaxonomyVersion,
            ["input_package_fingerprint"] = Fingerprint(FixturePackageReader.ExecutionInputFileName),
            ["oracle_fingerprint"] = Fingerprint(FixturePackageReader.OracleFileName),
            ["provenance_fingerprint"] = Fingerprint(FixturePackageReader.ProvenanceFileName),
            ["replay_dependency_fingerprint"] = Fingerprint(FixturePackageReader.ReplayDependenciesFileName),
            ["redistribution_class"] = "project-authored",
            ["owner"] = "evaluation-owner",
            ["review_state"] = "reviewed",
            ["created_at"] = DateTimeOffset.UnixEpoch.ToString("O"),
        };
        WriteJson(FixturePackageReader.PublicManifestFileName, manifest);
    }

    private static JsonObject EmptyInputComponent()
    {
        return new JsonObject
        {
            ["state"] = "empty",
            ["reason"] = "Not required by this contract-only fixture.",
        };
    }

    private static JsonObject ArtifactReference(string artifactId)
    {
        return new JsonObject
        {
            ["artifact_id"] = artifactId,
            ["artifact_version"] = "1.0.0",
            ["fingerprint"] = new string('0', 64),
            ["availability"] = "retained",
        };
    }

    private static JsonObject PartitionTransition(
        string? from,
        string to,
        string reason,
        bool influencedImplementation)
    {
        return new JsonObject
        {
            ["from"] = from,
            ["to"] = to,
            ["at"] = DateTimeOffset.UnixEpoch.ToString("O"),
            ["reason"] = reason,
            ["change_influenced_implementation"] = influencedImplementation,
        };
    }

    private void RefreshFingerprint(string propertyName, string targetFileName)
    {
        MutateObject(
            FixturePackageReader.PublicManifestFileName,
            root => root[propertyName] = Fingerprint(targetFileName));
    }

    private void MutateObject(string fileName, Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode.Parse(File.ReadAllText(FilePath(fileName)))!.AsObject();
        mutation(root);
        WriteJson(fileName, root);
    }

    private void WriteJson(string fileName, JsonNode node)
    {
        File.WriteAllText(
            FilePath(fileName),
            node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private string Fingerprint(string fileName)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FilePath(fileName)))).ToLowerInvariant();
    }
}
