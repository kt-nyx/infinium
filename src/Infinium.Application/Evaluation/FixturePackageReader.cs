using System.Text.Json;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Evaluation;

public enum FixturePartition
{
    Unspecified,
    Development,
    Validation,
    HeldOut,
}

public sealed record ExecutionFixturePackage(
    OpaqueId FixtureId,
    ContractVersion FixtureVersion,
    JsonElement ExecutionInput);

public sealed record EvaluationHarnessFixturePackage(
    OpaqueId FixtureId,
    ContractVersion FixtureVersion,
    FixturePartition Partition,
    JsonElement PublicManifest,
    JsonElement ExecutionInput,
    JsonElement Oracle,
    JsonElement Provenance,
    JsonElement ReplayDependencies,
    JsonElement Redistribution,
    JsonElement PartitionHistory);

public static class FixturePackageReader
{
    private const long MaximumFixtureDocumentBytes = 16 * 1024 * 1024;
    public const string PublicManifestFileName = "public-manifest.json";
    public const string ExecutionInputFileName = "execution-input.json";
    public const string OracleFileName = "expected-oracle.json";
    public const string ProvenanceFileName = "provenance.json";
    public const string ReplayDependenciesFileName = "replay-dependencies.json";
    public const string RedistributionFileName = "redistribution.json";
    public const string PartitionHistoryFileName = "partition-history.json";

    private static readonly string[] RequiredExpectedCollections =
    [
        "expected_observations",
        "expected_deterministic_results",
        "expected_external_claims",
        "expected_application_links",
        "expected_discovery_leads",
        "expected_model_proposals",
        "expected_proposal_admissions",
        "expected_candidates",
        "expected_hypotheses",
        "expected_findings",
        "expected_recommendations",
        "expected_supported_cases",
        "expected_lead_only_cases",
        "expected_abstentions",
        "expected_invalid_inputs",
        "expected_failures",
        "expected_coverage_and_gaps",
    ];

    private static readonly Dictionary<string, HashSet<string>> ExpectedCollectionTypes =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["expected_observations"] = ["observation"],
            ["expected_deterministic_results"] = ["deterministic-result"],
            ["expected_external_claims"] = ["external-claim"],
            ["expected_application_links"] = ["application-link"],
            ["expected_discovery_leads"] = ["discovery-lead"],
            ["expected_model_proposals"] = ["model-proposal"],
            ["expected_proposal_admissions"] = ["proposal-admission"],
            ["expected_candidates"] = ["candidate"],
            ["expected_hypotheses"] = ["hypothesis"],
            ["expected_findings"] = ["finding"],
            ["expected_recommendations"] = ["recommendation"],
            ["expected_supported_cases"] = ["supported-case"],
            ["expected_lead_only_cases"] = ["lead-only-case"],
            ["expected_abstentions"] = ["abstention"],
            ["expected_invalid_inputs"] = ["invalid-input"],
            ["expected_failures"] = ["failure"],
            ["expected_coverage_and_gaps"] = ["coverage", "coverage-gap", "audit-gap"],
        };

    private static readonly Dictionary<string, string> ExpectedCollectionStateNames =
        new(StringComparer.Ordinal)
        {
            ["expected_observations"] = "observations",
            ["expected_deterministic_results"] = "deterministic_results",
            ["expected_external_claims"] = "external_claims",
            ["expected_application_links"] = "application_links",
            ["expected_discovery_leads"] = "discovery_leads",
            ["expected_model_proposals"] = "model_proposals",
            ["expected_proposal_admissions"] = "proposal_admissions",
            ["expected_candidates"] = "candidates",
            ["expected_hypotheses"] = "hypotheses",
            ["expected_findings"] = "findings",
            ["expected_recommendations"] = "recommendations",
            ["expected_supported_cases"] = "supported_cases",
            ["expected_lead_only_cases"] = "lead_only_cases",
            ["expected_abstentions"] = "abstentions",
            ["expected_invalid_inputs"] = "invalid_inputs",
            ["expected_failures"] = "failures",
            ["expected_coverage_and_gaps"] = "coverage_gaps",
        };

    private static readonly HashSet<string> PublicManifestProperties = new(StringComparer.Ordinal)
    {
        "schema_id",
        "schema_version",
        "fixture_id",
        "fixture_version",
        "evaluation_ids",
        "purpose",
        "classification",
        "partition",
        "partition_history",
        "taxonomy_id",
        "taxonomy_version",
        "input_package_fingerprint",
        "oracle_fingerprint",
        "provenance_fingerprint",
        "replay_dependency_fingerprint",
        "redistribution_class",
        "owner",
        "review_state",
        "created_at",
    };

    private static readonly HashSet<string> ExecutionInputProperties = new(StringComparer.Ordinal)
    {
        "fixture_id",
        "fixture_version",
        "installation_snapshot_input",
        "analysis_context_input",
        "effective_scan_configuration",
        "runtime_support_input",
        "mo2_instance_profile_input",
        "plugin_order_input",
        "provider_order_input",
        "source_claim_inputs",
        "analyzer_declarations",
        "tool_library_versions",
        "declared_archive_state",
        "declared_supported_capabilities",
        "declared_unsupported_capabilities",
        "resource_and_time_limits",
        "input_payload_refs",
    };

    private static readonly HashSet<string> OracleProperties = new(StringComparer.Ordinal)
    {
        "fixture_id",
        "fixture_version",
        "oracle_version",
        "independent_authors_and_reviewers",
        "ground_truth_methods",
        "expected_observations",
        "expected_deterministic_results",
        "expected_external_claims",
        "expected_application_links",
        "expected_discovery_leads",
        "expected_model_proposals",
        "expected_proposal_admissions",
        "expected_candidates",
        "expected_hypotheses",
        "expected_findings",
        "expected_recommendations",
        "expected_supported_cases",
        "expected_lead_only_cases",
        "expected_abstentions",
        "expected_invalid_inputs",
        "expected_failures",
        "expected_coverage_and_gaps",
        "expected_collection_states",
        "expected_taxonomy_assignments",
        "expected_replayability",
        "forbidden_claims",
        "known_limits",
        "pre_registered_at",
        "change_history",
    };

    private static readonly HashSet<string> ForbiddenExecutionProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "answer",
        "answers",
        "answer_bearing_notes",
        "expected",
        "expected_answers",
        "expected_candidates",
        "expected_findings",
        "expected_labels",
        "fixture_class",
        "ground_truth",
        "oracle",
        "oracle_fingerprint",
        "oracle_path",
        "positive_negative_boundary_class",
    };

    public static ExecutionFixturePackage ReadExecutionInput(string executionInputPath)
    {
        using BoundedJsonDocumentSnapshot snapshot = ReadDocument(executionInputPath);
        JsonElement root = RequireObject(snapshot.Document.RootElement, ExecutionInputFileName);
        EmbeddedJsonSchemaValidator.Validate(root, "fixture-execution-input.v1.schema.json");
        EnsureOnlyProperties(root, ExecutionInputProperties, ExecutionInputFileName);
        RejectAnswerBearingProperties(root);

        OpaqueId fixtureId = new(RequireString(root, "fixture_id"));
        ContractVersion fixtureVersion = ContractVersion.Parse(RequireString(root, "fixture_version"));
        return new ExecutionFixturePackage(fixtureId, fixtureVersion, root.Clone());
    }

    internal static EvaluationHarnessFixturePackage ReadForEvaluationHarness(string fixtureDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureDirectory);
        string fullDirectory = Path.GetFullPath(fixtureDirectory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new DirectoryNotFoundException($"Fixture directory '{fullDirectory}' does not exist.");
        }

        string publicPath = RequiredFile(fullDirectory, PublicManifestFileName);
        string executionPath = RequiredFile(fullDirectory, ExecutionInputFileName);
        string oraclePath = RequiredFile(fullDirectory, OracleFileName);
        string provenancePath = RequiredFile(fullDirectory, ProvenanceFileName);
        string replayPath = RequiredFile(fullDirectory, ReplayDependenciesFileName);
        string redistributionPath = RequiredFile(fullDirectory, RedistributionFileName);
        string partitionHistoryPath = RequiredFile(fullDirectory, PartitionHistoryFileName);

        using BoundedJsonDocumentSnapshot publicDocument = ReadDocument(publicPath);
        using BoundedJsonDocumentSnapshot executionDocument = ReadDocument(executionPath);
        using BoundedJsonDocumentSnapshot oracleDocument = ReadDocument(oraclePath);
        using BoundedJsonDocumentSnapshot provenanceDocument = ReadDocument(provenancePath);
        using BoundedJsonDocumentSnapshot replayDocument = ReadDocument(replayPath);
        using BoundedJsonDocumentSnapshot redistributionDocument = ReadDocument(redistributionPath);
        using BoundedJsonDocumentSnapshot partitionHistoryDocument = ReadDocument(partitionHistoryPath);

        JsonElement publicManifest = RequireObject(publicDocument.Document.RootElement, PublicManifestFileName);
        JsonElement executionInput = RequireObject(executionDocument.Document.RootElement, ExecutionInputFileName);
        JsonElement oracle = RequireObject(oracleDocument.Document.RootElement, OracleFileName);
        JsonElement provenance = RequireObject(provenanceDocument.Document.RootElement, ProvenanceFileName);
        JsonElement replayDependencies = RequireObject(replayDocument.Document.RootElement, ReplayDependenciesFileName);
        JsonElement redistribution = RequireObject(redistributionDocument.Document.RootElement, RedistributionFileName);
        JsonElement partitionHistory = RequireObject(
            partitionHistoryDocument.Document.RootElement,
            PartitionHistoryFileName);

        EmbeddedJsonSchemaValidator.Validate(publicManifest, "fixture-public-manifest.v1.schema.json");
        EmbeddedJsonSchemaValidator.Validate(executionInput, "fixture-execution-input.v1.schema.json");
        EmbeddedJsonSchemaValidator.Validate(oracle, "fixture-oracle.v1.schema.json");
        EmbeddedJsonSchemaValidator.Validate(provenance, "fixture-provenance.v1.schema.json");
        EmbeddedJsonSchemaValidator.Validate(replayDependencies, "replay-dependencies.v1.schema.json");
        EmbeddedJsonSchemaValidator.Validate(redistribution, "fixture-redistribution.v1.schema.json");
        EmbeddedJsonSchemaValidator.Validate(partitionHistory, "fixture-partition-history.v1.schema.json");
        EnsureOnlyProperties(publicManifest, PublicManifestProperties, PublicManifestFileName);
        EnsureOnlyProperties(executionInput, ExecutionInputProperties, ExecutionInputFileName);
        EnsureOnlyProperties(oracle, OracleProperties, OracleFileName);
        ValidatePublicManifestSchema(publicManifest);
        OpaqueId fixtureId = new(RequireString(publicManifest, "fixture_id"));
        ContractVersion fixtureVersion = ContractVersion.Parse(RequireString(publicManifest, "fixture_version"));
        FixturePartition partition = ParsePartition(RequireString(publicManifest, "partition"));

        ValidateTaxonomy(publicManifest);
        ValidateFingerprint(publicManifest, "input_package_fingerprint", executionDocument.Sha256);
        ValidateFingerprint(publicManifest, "oracle_fingerprint", oracleDocument.Sha256);
        ValidateFingerprint(publicManifest, "provenance_fingerprint", provenanceDocument.Sha256);
        ValidateFingerprint(publicManifest, "replay_dependency_fingerprint", replayDocument.Sha256);
        ValidatePartitionHistory(publicManifest, partitionHistory, partition);
        ValidateIdentity(executionInput, fixtureId, fixtureVersion, ExecutionInputFileName);
        ValidateIdentity(oracle, fixtureId, fixtureVersion, OracleFileName);
        ValidateIdentity(provenance, fixtureId, fixtureVersion, ProvenanceFileName);
        ValidateIdentity(replayDependencies, fixtureId, fixtureVersion, ReplayDependenciesFileName);
        ValidateIdentity(redistribution, fixtureId, fixtureVersion, RedistributionFileName);
        ValidateIdentity(partitionHistory, fixtureId, fixtureVersion, PartitionHistoryFileName);
        ValidateRedistribution(publicManifest, redistribution);
        ValidateOracle(oracle);
        ValidateReplayDependencies(replayDependencies, RequireString(oracle, "expected_replayability"));
        RejectAnswerBearingProperties(executionInput);

        return new EvaluationHarnessFixturePackage(
            fixtureId,
            fixtureVersion,
            partition,
            publicManifest.Clone(),
            executionInput.Clone(),
            oracle.Clone(),
            provenance.Clone(),
            replayDependencies.Clone(),
            redistribution.Clone(),
            partitionHistory.Clone());
    }

    private static void ValidatePublicManifestSchema(JsonElement publicManifest)
    {
        if (!StringComparer.Ordinal.Equals(
                RequireString(publicManifest, "schema_id"),
                ContractConstants.FixturePublicManifestSchemaId)
            || !StringComparer.Ordinal.Equals(RequireString(publicManifest, "schema_version"), "1"))
        {
            throw new InvalidDataException("Fixture public manifest uses an unsupported schema contract.");
        }
    }

    private static void ValidateTaxonomy(JsonElement publicManifest)
    {
        string taxonomyId = RequireString(publicManifest, "taxonomy_id");
        string taxonomyVersion = RequireString(publicManifest, "taxonomy_version");
        if (!StringComparer.Ordinal.Equals(taxonomyId, ContractConstants.TaxonomyId)
            || !StringComparer.Ordinal.Equals(taxonomyVersion, ContractConstants.TaxonomyVersion))
        {
            throw new InvalidDataException(
                $"Fixture must bind {ContractConstants.TaxonomyId}/{ContractConstants.TaxonomyVersion}.");
        }
    }

    private static void ValidateFingerprint(JsonElement manifest, string propertyName, string actualSha256)
    {
        string expectedText = RequireString(manifest, propertyName);
        Sha256Fingerprint expected = new(expectedText);
        if (!StringComparer.Ordinal.Equals(expectedText, expected.Value))
        {
            throw new InvalidDataException($"Manifest fingerprint '{propertyName}' must be lowercase.");
        }

        Sha256Fingerprint actual = new(actualSha256);
        if (expected != actual)
        {
            throw new InvalidDataException(
                $"Manifest fingerprint '{propertyName}' does not match the validated document snapshot.");
        }
    }

    private static void ValidateRedistribution(JsonElement publicManifest, JsonElement redistribution)
    {
        if (!StringComparer.Ordinal.Equals(
                RequireString(publicManifest, "redistribution_class"),
                RequireString(redistribution, "redistribution_class")))
        {
            throw new InvalidDataException(
                "Public and separately retained redistribution classifications do not match.");
        }
    }

    private static void ValidateIdentity(
        JsonElement document,
        OpaqueId expectedFixtureId,
        ContractVersion expectedFixtureVersion,
        string fileName)
    {
        OpaqueId actualFixtureId = new(RequireString(document, "fixture_id"));
        ContractVersion actualFixtureVersion = ContractVersion.Parse(RequireString(document, "fixture_version"));
        if (actualFixtureId != expectedFixtureId || actualFixtureVersion != expectedFixtureVersion)
        {
            throw new InvalidDataException($"Fixture identity in '{fileName}' does not match the public manifest.");
        }
    }

    private static void ValidatePartitionHistory(
        JsonElement publicManifest,
        JsonElement partitionHistoryDocument,
        FixturePartition partition)
    {
        JsonElement publicHistory = RequireArray(publicManifest, "partition_history");
        JsonElement separateHistory = RequireArray(partitionHistoryDocument, "partition_history");
        if (publicHistory.GetArrayLength() == 0 || separateHistory.GetArrayLength() == 0)
        {
            throw new InvalidDataException("Fixture partition history must contain its initial assignment.");
        }

        if (!JsonElement.DeepEquals(publicHistory, separateHistory))
        {
            throw new InvalidDataException("Public and separately retained partition histories do not match.");
        }

        string? priorPartition = null;
        UtcTimestamp? priorTransitionAt = null;
        Sha256Fingerprint currentInputFingerprint = new(
            RequireString(publicManifest, "input_package_fingerprint"));
        Sha256Fingerprint currentOracleFingerprint = new(
            RequireString(publicManifest, "oracle_fingerprint"));
        int index = 0;
        foreach (JsonElement transition in separateHistory.EnumerateArray())
        {
            if (!transition.TryGetProperty("from", out JsonElement from))
            {
                throw new InvalidDataException("Partition history entry is missing 'from'.");
            }

            if (index == 0)
            {
                if (from.ValueKind != JsonValueKind.Null)
                {
                    throw new InvalidDataException("Initial partition-history entry must begin from null.");
                }
            }
            else if (from.ValueKind != JsonValueKind.String
                || !StringComparer.Ordinal.Equals(from.GetString(), priorPartition))
            {
                throw new InvalidDataException("Partition history is not append-only and contiguous.");
            }

            priorPartition = RequireString(transition, "to");
            _ = ParsePartition(priorPartition);
            UtcTimestamp transitionAt = UtcTimestamp.Parse(RequireString(transition, "at"));
            if (priorTransitionAt is not null && transitionAt.Value <= priorTransitionAt.Value)
            {
                throw new InvalidDataException("Partition-history timestamps must be strictly increasing.");
            }

            priorTransitionAt = transitionAt;
            _ = RequireString(transition, "reason");
            bool influencedImplementation = RequireBoolean(
                transition,
                "change_influenced_implementation");
            if (from.ValueKind == JsonValueKind.String)
            {
                string fromPartition = from.GetString()!;
                if (StringComparer.Ordinal.Equals(fromPartition, priorPartition))
                {
                    throw new InvalidDataException("Partition history cannot contain a same-state transition.");
                }

                if (fromPartition == "development"
                    || (fromPartition == "validation" && priorPartition == "held-out")
                    || priorPartition != "development")
                {
                    throw new InvalidDataException(
                        $"Fixture partition transition '{fromPartition}' to '{priorPartition}' is forbidden.");
                }

                if (!influencedImplementation)
                {
                    throw new InvalidDataException(
                        "A known-answer transition to development must record that it influenced implementation.");
                }

                OpaqueId replacementFixtureId = new(RequireString(transition, "replacement_fixture_id"));
                if (replacementFixtureId == new OpaqueId(RequireString(publicManifest, "fixture_id")))
                {
                    throw new InvalidDataException("Replacement fixture must be materially independent by identity.");
                }

                Sha256Fingerprint replacementInputFingerprint = ParseLowercaseFingerprint(
                    transition,
                    "replacement_input_package_fingerprint");
                Sha256Fingerprint replacementOracleFingerprint = ParseLowercaseFingerprint(
                    transition,
                    "replacement_oracle_fingerprint");
                if (replacementInputFingerprint == currentInputFingerprint
                    || replacementOracleFingerprint == currentOracleFingerprint)
                {
                    throw new InvalidDataException(
                        "Replacement fixture input and oracle fingerprints must differ from the known fixture.");
                }

                string replacementPartition = RequireString(transition, "replacement_partition");
                if (replacementPartition is not ("validation" or "held-out"))
                {
                    throw new InvalidDataException(
                        "Replacement fixture must restore validation or held-out coverage.");
                }

                _ = RequireObject(
                    transition.GetProperty("independence_evidence_reference"),
                    "independence_evidence_reference");
                _ = RequireString(transition, "authorized_by");
            }

            index++;
        }

        string finalPartition = priorPartition!;
        if (ParsePartition(finalPartition) != partition)
        {
            throw new InvalidDataException("The current partition does not match the final partition-history entry.");
        }
    }

    private static void ValidateOracle(JsonElement oracle)
    {
        JsonElement reviewers = RequireArray(oracle, "independent_authors_and_reviewers");
        JsonElement methods = RequireArray(oracle, "ground_truth_methods");
        if (reviewers.GetArrayLength() == 0 || methods.GetArrayLength() == 0)
        {
            throw new InvalidDataException("Oracle ground truth requires an independent owner/reviewer and method.");
        }

        HashSet<string> methodIds = new(StringComparer.Ordinal);
        foreach (JsonElement method in methods.EnumerateArray())
        {
            string methodId = RequireString(method, "method_id");
            if (!methodIds.Add(methodId))
            {
                throw new InvalidDataException($"Oracle contains duplicate ground-truth method ID '{methodId}'.");
            }
        }

        HashSet<string> expectedIds = new(StringComparer.Ordinal);
        JsonElement expectedCollectionStates = RequireObject(
            oracle.GetProperty("expected_collection_states"),
            "expected_collection_states");
        foreach (string collection in RequiredExpectedCollections)
        {
            JsonElement expectedItems = RequireArray(oracle, collection);
            HashSet<string> allowedTypes = ExpectedCollectionTypes[collection];
            foreach (JsonElement expectedItem in expectedItems.EnumerateArray())
            {
                string expectedId = RequireString(expectedItem, "expected_id");
                if (!expectedIds.Add(expectedId))
                {
                    throw new InvalidDataException($"Oracle contains duplicate expected item ID '{expectedId}'.");
                }

                string expectedType = RequireString(expectedItem, "expected_type");
                if (!allowedTypes.Contains(expectedType))
                {
                    throw new InvalidDataException(
                        $"Oracle collection '{collection}' cannot contain expected type '{expectedType}'.");
                }

                foreach (JsonElement methodIdElement in RequireArray(
                    expectedItem,
                    "ground_truth_method_ids").EnumerateArray())
                {
                    string referencedMethodId = methodIdElement.GetString()
                        ?? throw new InvalidDataException(
                            "Oracle ground-truth method references must be strings.");
                    if (!methodIds.Contains(referencedMethodId))
                    {
                        throw new InvalidDataException(
                            $"Expected item '{expectedId}' references unknown ground-truth method "
                            + $"'{referencedMethodId}'.");
                    }
                }
            }

            string collectionStateName = ExpectedCollectionStateNames[collection];
            JsonElement collectionState = RequireObject(
                expectedCollectionStates.GetProperty(collectionStateName),
                collectionStateName);
            int stateBearingItemCount = collection == "expected_coverage_and_gaps"
                ? expectedItems.EnumerateArray().Count(
                    item => StringComparer.Ordinal.Equals(
                        RequireString(item, "expected_type"),
                        "coverage-gap"))
                : expectedItems.GetArrayLength();
            ValidateExpectedCollectionState(
                collection,
                stateBearingItemCount,
                RequireString(collectionState, "state"));
        }

        JsonElement taxonomyAssignments = RequireArray(oracle, "expected_taxonomy_assignments");
        HashSet<string> taxonomyAssignmentIds = new(StringComparer.Ordinal);
        foreach (JsonElement assignment in taxonomyAssignments.EnumerateArray())
        {
            string assignmentId = RequireString(assignment, "assignment_id");
            if (!taxonomyAssignmentIds.Add(assignmentId))
            {
                throw new InvalidDataException(
                    $"Oracle contains duplicate taxonomy-assignment ID '{assignmentId}'.");
            }

            string subjectId = RequireString(assignment, "subject_id");
            if (!expectedIds.Contains(subjectId))
            {
                throw new InvalidDataException(
                    $"Taxonomy assignment '{assignmentId}' references unknown expected item '{subjectId}'.");
            }
        }

        _ = RequireArray(oracle, "forbidden_claims");
        _ = RequireArray(oracle, "known_limits");
        _ = RequireString(oracle, "expected_replayability");
        _ = UtcTimestamp.Parse(RequireString(oracle, "pre_registered_at"));
        _ = RequireArray(oracle, "change_history");
    }

    private static void ValidateExpectedCollectionState(
        string collection,
        int expectedItemCount,
        string state)
    {
        bool coherent = state switch
        {
            "populated" => expectedItemCount > 0,
            "empty" or "unsupported" or "not-applicable" or "failed" => expectedItemCount == 0,
            _ => false,
        };
        if (!coherent)
        {
            throw new InvalidDataException(
                $"Oracle collection '{collection}' has state '{state}' with {expectedItemCount} expected items.");
        }
    }

    private static void ValidateReplayDependencies(JsonElement replayDependencies, string oracleReplayability)
    {
        string replayState = RequireString(replayDependencies, "expected_replay_state");
        _ = replayState switch
        {
            "complete-clean" or "boundary-replay" or "audit-only" or "unavailable" => replayState,
            _ => throw new InvalidDataException($"Unknown replay state '{replayState}'."),
        };
        if (!StringComparer.Ordinal.Equals(replayState, oracleReplayability))
        {
            throw new InvalidDataException("Replay dependency and oracle replay states do not match.");
        }

        JsonElement dependencies = RequireArray(replayDependencies, "dependencies");
        foreach (JsonElement dependency in dependencies.EnumerateArray())
        {
            _ = RequireString(dependency, "dependency_id");
            _ = RequireString(dependency, "kind");
            _ = RequireString(dependency, "identity_or_version");
            if (dependency.TryGetProperty("sha256", out JsonElement fingerprint)
                && fingerprint.ValueKind == JsonValueKind.String)
            {
                _ = new Sha256Fingerprint(fingerprint.GetString()!);
            }
            else if (dependency.TryGetProperty("sha256", out _))
            {
                throw new InvalidDataException("Replay dependency SHA-256 must be a string when present.");
            }

            _ = RequireString(dependency, "retention_location_class");
            JsonElement requiredFor = RequireArray(dependency, "required_for");
            if (requiredFor.GetArrayLength() == 0)
            {
                throw new InvalidDataException("Replay dependency must state what it is required for.");
            }

            _ = RequireString(dependency, "availability");
            _ = RequireString(dependency, "permission_and_redistribution");
            _ = RequireString(dependency, "deletion_effect");
        }
    }

    private static void EnsureOnlyProperties(
        JsonElement document,
        HashSet<string> allowedProperties,
        string documentName)
    {
        foreach (JsonProperty property in document.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                throw new InvalidDataException(
                    $"'{documentName}' contains unsupported property '{property.Name}'.");
            }
        }
    }

    private static void RejectAnswerBearingProperties(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (ForbiddenExecutionProperties.Contains(property.Name)
                        || property.Name.StartsWith("expected_", StringComparison.OrdinalIgnoreCase)
                        || property.Name.StartsWith("oracle_", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"Execution input contains answer-bearing property '{property.Name}'.");
                    }

                    RejectAnswerBearingProperties(property.Value);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    RejectAnswerBearingProperties(item);
                }

                break;
        }
    }

    private static FixturePartition ParsePartition(string value)
    {
        return value switch
        {
            "development" => FixturePartition.Development,
            "validation" => FixturePartition.Validation,
            "held-out" => FixturePartition.HeldOut,
            _ => throw new InvalidDataException($"Unknown fixture partition '{value}'."),
        };
    }

    private static string RequiredFile(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException($"Required fixture document '{fileName}' is missing.", path);
    }

    private static BoundedJsonDocumentSnapshot ReadDocument(string path)
    {
        return BoundedJsonDocumentReader.Read(path, MaximumFixtureDocumentBytes, maximumDepth: 64);
    }

    private static JsonElement RequireObject(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"'{name}' must be a JSON object.");
        }

        return value;
    }

    private static JsonElement RequireArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Required array property '{propertyName}' is missing.");
        }

        return value;
    }

    private static string RequireString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"Required string property '{propertyName}' is missing.");
        }

        return value.GetString()!;
    }

    private static bool RequireBoolean(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException($"Required Boolean property '{propertyName}' is missing.");
        }

        return value.GetBoolean();
    }

    private static Sha256Fingerprint ParseLowercaseFingerprint(
        JsonElement parent,
        string propertyName)
    {
        string value = RequireString(parent, propertyName);
        Sha256Fingerprint fingerprint = new(value);
        if (!StringComparer.Ordinal.Equals(value, fingerprint.Value))
        {
            throw new InvalidDataException($"'{propertyName}' must be lowercase.");
        }

        return fingerprint;
    }
}
