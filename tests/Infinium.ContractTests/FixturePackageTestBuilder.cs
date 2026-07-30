using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

internal sealed class FixturePackageTestBuilder : IDisposable
{
    private static readonly JsonSerializerOptions IndentedJsonOptions =
        new() { WriteIndented = true };
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
            PartitionTransition(
                null,
                "validation",
                DateTimeOffset.UnixEpoch,
                "initial registration",
                false),
            PartitionTransition(
                "validation",
                "development",
                DateTimeOffset.UnixEpoch.AddSeconds(1),
                "result influenced implementation",
                true),
        };
        SetPartitionHistory("development", history);
    }

    internal void AddOracleExpectedItem(
        string collection,
        string expectedId,
        string expectedType,
        string groundTruthMethodId = "project-authored-method")
    {
        MutateObject(
            FixturePackageReader.OracleFileName,
            root =>
            {
                root[collection]!.AsArray().Add(
                new JsonObject
                {
                    ["expected_id"] = expectedId,
                    ["subject_id"] = "subject-1",
                    ["expected_type"] = expectedType,
                    ["expected_state"] = "present",
                    ["ground_truth_method_ids"] = new JsonArray(groundTruthMethodId),
                    ["canonical_value_fingerprint"] = new string('1', 64),
                });
                if (collection != "expected_coverage_and_gaps"
                    || expectedType == "coverage-gap")
                {
                    root["expected_collection_states"]!
                        .AsObject()[ExpectedCollectionStateName(collection)]!
                        .AsObject()["state"] = "populated";
                }
            });
        RefreshFingerprint("oracle_fingerprint", FixturePackageReader.OracleFileName);
    }

    internal void SetExpectedCollectionState(string collectionStateName, string state)
    {
        MutateObject(
            FixturePackageReader.OracleFileName,
            root => root["expected_collection_states"]!
                .AsObject()[collectionStateName]!
                .AsObject()["state"] = state);
        RefreshFingerprint("oracle_fingerprint", FixturePackageReader.OracleFileName);
    }

    internal void AddTaxonomyAssignment(string assignmentId, string subjectExpectedId)
    {
        MutateObject(
            FixturePackageReader.OracleFileName,
            root => root["expected_taxonomy_assignments"]!.AsArray().Add(
                new JsonObject
                {
                    ["assignment_id"] = assignmentId,
                    ["taxonomy_id"] = ContractConstants.TaxonomyId,
                    ["taxonomy_version"] = ContractConstants.TaxonomyVersion,
                    ["subject_type"] = "expected-item",
                    ["subject_id"] = subjectExpectedId,
                    ["axis"] = "technical-surface",
                    ["facet"] = "plugin-data",
                    ["code"] = "surface.plugin-data",
                    ["applicability_state"] = "assigned",
                    ["classification_role"] = "established",
                    ["evidence_references"] = new JsonArray(),
                    ["applicability_condition_references"] = new JsonArray(),
                    ["reason"] = "Contract integrity test assignment.",
                    ["derivation_provenance"] = Provenance(),
                }));
        RefreshFingerprint("oracle_fingerprint", FixturePackageReader.OracleFileName);
    }

    internal void AddDuplicateGroundTruthMethod()
    {
        MutateObject(
            FixturePackageReader.OracleFileName,
            root => root["ground_truth_methods"]!.AsArray().Add(
                root["ground_truth_methods"]![0]!.DeepClone()));
        RefreshFingerprint("oracle_fingerprint", FixturePackageReader.OracleFileName);
    }

    internal void AddRetainedInputArtifact(string relativePath, byte[] bytes)
    {
        WriteRetainedArtifact(relativePath, bytes);
        MutateObject(
            FixturePackageReader.ExecutionInputFileName,
            root =>
            {
                root["input_payload_refs"]!.AsArray().Add(
                    ArtifactReference(relativePath, Fingerprint(relativePath)));
                JsonObject limits = root["resource_and_time_limits"]!.AsObject();
                limits["input_bytes"] =
                    limits["input_bytes"]!.GetValue<long>() + bytes.LongLength;
            });
        RefreshFingerprint("input_package_fingerprint", FixturePackageReader.ExecutionInputFileName);
    }

    internal void AddRetainedOracleArtifact(string relativePath, byte[] bytes)
    {
        WriteRetainedArtifact(relativePath, bytes);
        MutateObject(
            FixturePackageReader.OracleFileName,
            root => root["ground_truth_methods"]![0]!["evidence_references"]!
                .AsArray()
                .Add(ArtifactReference(relativePath, Fingerprint(relativePath))));
        RefreshFingerprint("oracle_fingerprint", FixturePackageReader.OracleFileName);
    }

    internal byte[] AddSupplementalBethesdaOracle(string inputArtifactId, byte[] inputBytes)
    {
        AddRetainedInputArtifact(inputArtifactId, inputBytes);
        MutateObject(
            FixturePackageReader.OracleFileName,
            root => root["ground_truth_methods"]!.AsArray().Add(
                new JsonObject
                {
                    ["method_id"] = "independent-byte-review",
                    ["method"] = "Second independent byte-level review.",
                    ["evidence_references"] = new JsonArray
                    {
                        ArtifactReference("public-format-reference"),
                    },
                    ["independent_of_system_under_test"] = true,
                }));

        JsonObject supplemental = new()
        {
            ["schema_id"] = "infinium.evaluation.bethesda-byte-oracle/v1",
            ["schema_version"] = "1",
            ["fixture_id"] = "fixture-development-1",
            ["fixture_version"] = "1.0.0",
            ["oracle_artifact_version"] = "1.0.0",
            ["canonicalization"] = "infinium-canonical-json-sha256/v1",
            ["independent_authors_and_reviewers"] = new JsonArray("independent-reviewer"),
            ["ground_truth_method_ids"] =
                new JsonArray("project-authored-method", "independent-byte-review"),
            ["format_evidence"] = new JsonArray
            {
                ArtifactReference("public-format-reference"),
            },
            ["files"] = new JsonArray
            {
                new JsonObject
                {
                    ["artifact_id"] = inputArtifactId,
                    ["byte_length"] = inputBytes.LongLength,
                    ["sha256"] = Convert.ToHexStringLower(SHA256.HashData(inputBytes)),
                    ["provider_id"] = "test-provider",
                    ["scenario_memberships"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["scenario_id"] = "test-scenario",
                            ["plugin_order"] = 0,
                        },
                    },
                    ["masters_state"] = "observed",
                    ["masters"] = new JsonArray(),
                    ["esl_flag_state"] = "observed",
                    ["esl_flag"] = false,
                    ["byte_coverage"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["span_id"] = "entire-file",
                            ["offset_space"] = "physical-file",
                            ["offset"] = 0,
                            ["length"] = inputBytes.LongLength,
                            ["classification"] = "opaque",
                        },
                    },
                },
            },
            ["facts"] = new JsonArray(),
            ["mutation_expectations"] = new JsonArray(),
            ["limits"] = new JsonObject
            {
                ["maximum_input_bytes"] = inputBytes.LongLength,
                ["maximum_records"] = 1,
                ["maximum_subrecords_per_record"] = 1,
                ["maximum_group_depth"] = 1,
                ["maximum_decompressed_bytes"] = inputBytes.LongLength,
            },
            ["review_state"] = "accepted",
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            supplemental,
            IndentedJsonOptions);
        AddRetainedOracleArtifact(BethesdaByteOracleValidator.ArtifactId, bytes);
        return bytes;
    }

    internal void AddRetainedInputReference(string relativePath, string fingerprint)
    {
        MutateObject(
            FixturePackageReader.ExecutionInputFileName,
            root => root["input_payload_refs"]!.AsArray().Add(
                ArtifactReference(relativePath, fingerprint)));
        RefreshFingerprint("input_package_fingerprint", FixturePackageReader.ExecutionInputFileName);
    }

    internal void ReplaceRetainedOracleArtifactAndRefreshReference(
        string relativePath,
        byte[] bytes)
    {
        WriteRetainedArtifact(relativePath, bytes);
        string fingerprint = Fingerprint(relativePath);
        MutateObject(
            FixturePackageReader.OracleFileName,
            root =>
            {
                foreach (JsonNode? method in root["ground_truth_methods"]!.AsArray())
                {
                    foreach (JsonNode? reference in
                             method!["evidence_references"]!.AsArray())
                    {
                        if (StringComparer.Ordinal.Equals(
                                reference!["artifact_id"]!.GetValue<string>(),
                                relativePath))
                        {
                            reference["fingerprint"] = fingerprint;
                        }
                    }
                }
            });
        RefreshFingerprint("oracle_fingerprint", FixturePackageReader.OracleFileName);
    }

    internal void RenameRetainedOracleReference(string fromArtifactId, string toArtifactId)
    {
        MutateObject(
            FixturePackageReader.OracleFileName,
            root =>
            {
                foreach (JsonNode? method in root["ground_truth_methods"]!.AsArray())
                {
                    foreach (JsonNode? reference in
                             method!["evidence_references"]!.AsArray())
                    {
                        if (StringComparer.Ordinal.Equals(
                                reference!["artifact_id"]!.GetValue<string>(),
                                fromArtifactId))
                        {
                            reference["artifact_id"] = toArtifactId;
                        }
                    }
                }
            });
        RefreshFingerprint("oracle_fingerprint", FixturePackageReader.OracleFileName);
    }

    internal void SetDeclaredInputBytes(long inputBytes)
    {
        MutateObject(
            FixturePackageReader.ExecutionInputFileName,
            root => root["resource_and_time_limits"]!["input_bytes"] = inputBytes);
        RefreshFingerprint("input_package_fingerprint", FixturePackageReader.ExecutionInputFileName);
    }

    internal void MutateRetainedArtifact(string relativePath, byte[] bytes)
    {
        WriteRetainedArtifact(relativePath, bytes);
    }

    internal void AddProvenanceProperty(string propertyName, JsonNode value)
    {
        MutateObject(FixturePackageReader.ProvenanceFileName, root => root[propertyName] = value);
        RefreshFingerprint("provenance_fingerprint", FixturePackageReader.ProvenanceFileName);
    }

    internal void SetRedistributionClass(string redistributionClass)
    {
        MutateObject(
            FixturePackageReader.RedistributionFileName,
            root => root["redistribution_class"] = redistributionClass);
    }

    internal void SetPartitionHistory(string currentPartition, JsonArray history)
    {
        MutateObject(
            FixturePackageReader.PublicManifestFileName,
            root =>
            {
                root["partition"] = currentPartition;
                root["partition_history"] = history.DeepClone();
            });
        MutateObject(
            FixturePackageReader.PartitionHistoryFileName,
            root => root["partition_history"] = history.DeepClone());
    }

    internal static JsonObject PartitionTransition(
        string? from,
        string to,
        DateTimeOffset at,
        string reason,
        bool influencedImplementation,
        bool includeReplacement = false,
        string? replacementInputFingerprint = null,
        string? replacementOracleFingerprint = null)
    {
        JsonObject transition = new()
        {
            ["from"] = from,
            ["to"] = to,
            ["at"] = at.ToString("O"),
            ["reason"] = reason,
            ["change_influenced_implementation"] = influencedImplementation,
        };
        if (includeReplacement)
        {
            transition["replacement_fixture_id"] = "materially-independent-replacement";
            transition["replacement_partition"] = "held-out";
            transition["replacement_input_package_fingerprint"] =
                replacementInputFingerprint ?? new string('2', 64);
            transition["replacement_oracle_fingerprint"] =
                replacementOracleFingerprint ?? new string('3', 64);
            transition["independence_evidence_reference"] =
                ArtifactReference("replacement-independence-review");
            transition["authorized_by"] = "evaluation-owner";
        }

        return transition;
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
            ["expected_failures"] = new JsonArray(),
            ["expected_coverage_and_gaps"] = new JsonArray(),
            ["expected_collection_states"] = EmptyCollectionStates(),
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

    private static JsonObject EmptyCollectionStates()
    {
        JsonObject states = new();
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
        foreach (string collectionName in collectionNames)
        {
            states[collectionName] = new JsonObject
            {
                ["state"] = "empty",
                ["reason"] = "No output is expected from this contract-only fixture.",
            };
        }

        return states;
    }

    private static string ExpectedCollectionStateName(string collection)
    {
        return collection switch
        {
            "expected_observations" => "observations",
            "expected_deterministic_results" => "deterministic_results",
            "expected_external_claims" => "external_claims",
            "expected_application_links" => "application_links",
            "expected_discovery_leads" => "discovery_leads",
            "expected_model_proposals" => "model_proposals",
            "expected_proposal_admissions" => "proposal_admissions",
            "expected_candidates" => "candidates",
            "expected_hypotheses" => "hypotheses",
            "expected_findings" => "findings",
            "expected_recommendations" => "recommendations",
            "expected_supported_cases" => "supported_cases",
            "expected_lead_only_cases" => "lead_only_cases",
            "expected_abstentions" => "abstentions",
            "expected_invalid_inputs" => "invalid_inputs",
            "expected_failures" => "failures",
            "expected_coverage_and_gaps" => "coverage_gaps",
            _ => throw new ArgumentOutOfRangeException(nameof(collection)),
        };
    }

    private static JsonObject Provenance()
    {
        return new JsonObject
        {
            ["producer_id"] = "independent-oracle-author",
            ["producer_version"] = "1.0.0",
            ["originating_run_id"] = "oracle-construction-run",
            ["source_references"] = new JsonArray(),
            ["supporting_evidence_references"] = new JsonArray(),
            ["contradicting_evidence_references"] = new JsonArray(),
            ["llm_involvement"] = new JsonObject
            {
                ["state"] = "none",
                ["operation"] = "none",
            },
        };
    }

    private static JsonObject ArtifactReference(
        string artifactId,
        string? fingerprint = null)
    {
        return new JsonObject
        {
            ["artifact_id"] = artifactId,
            ["artifact_version"] = "1.0.0",
            ["fingerprint"] = fingerprint ?? new string('0', 64),
            ["availability"] = "retained",
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
            node.ToJsonString(IndentedJsonOptions));
    }

    private void WriteRetainedArtifact(string relativePath, byte[] bytes)
    {
        string path = FilePath(relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private string Fingerprint(string fileName)
    {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FilePath(fileName)))).ToLowerInvariant();
    }
}
