using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.Win32.SafeHandles;

namespace Infinium.PublicFixtures;

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

public sealed record PublicFixturePackage(
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

internal sealed record RetainedArtifactSnapshot(
    string ArtifactId,
    string ArtifactVersion,
    string Availability,
    bool HasDeclaredByteLength,
    long? DeclaredByteLength,
    ReadOnlyMemory<byte> Bytes,
    string Sha256)
{
    internal long ByteLength => Bytes.Length;
}

internal sealed record RetainedArtifactValidationTestOptions(
    int? MaximumReferenceCount = null,
    long? MaximumAggregateBytes = null,
    long? MaximumArtifactBytes = null,
    Action<string>? BeforeScopePin = null,
    Action<string>? BeforeArtifactOpen = null,
    Action? AfterArtifactsSnapshotted = null);

public static class PublicFixturePackageReader
{
    private const long MaximumFixtureDocumentBytes = 16 * 1024 * 1024;
    private const long MaximumRetainedArtifactBytes = 64 * 1024 * 1024;
    private const long MaximumRetainedAggregateBytes = 64 * 1024 * 1024;
    private const int MaximumRetainedArtifactReferences = 4_096;
    private const string InputArtifactPrefix = "inputs/";
    private const string OracleArtifactPrefix = "oracle/";
    private const string TaxonomyProjectionArtifactId = "oracle/taxonomy-projections.json";
    private const string TaxonomySubjectBindingsArtifactId = "inputs/taxonomy-subject-bindings.json";
    private const string TaxonomyAcceptedOrderArtifactId = "inputs/snapshot/accepted-order.json";
    private const string BethesdaConstructionManifestArtifactId =
        "inputs/construction-manifest.json";
    private const string BethesdaCaseMatrixArtifactId = "inputs/case-matrix.json";
    private const string BethesdaEffectiveScanConfigurationArtifactId =
        "inputs/effective-scan-configuration.json";
    public const string PublicManifestFileName = "public-manifest.json";
    public const string ExecutionInputFileName = "execution-input.json";
    public const string OracleFileName = "expected-oracle.json";
    public const string ProvenanceFileName = "provenance.json";
    public const string ReplayDependenciesFileName = "replay-dependencies.json";
    public const string RedistributionFileName = "redistribution.json";
    public const string PartitionHistoryFileName = "partition-history.json";

    private static readonly HashSet<string> RequiredRootDocumentNames = new(StringComparer.Ordinal)
    {
        PublicManifestFileName,
        ExecutionInputFileName,
        OracleFileName,
        ProvenanceFileName,
        ReplayDependenciesFileName,
        RedistributionFileName,
        PartitionHistoryFileName,
    };

    private static readonly Dictionary<string, bool> AcceptedBethesdaFixtureProfiles =
        new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["BETH-NPC-DEV"] = true,
            ["BETH-REFR-DEV"] = true,
            ["BETH-LIGHT-VAL"] = false,
            ["BETH-MALFORMED-VAL"] = false,
            ["BETH-UNSUPPORTED-VAL"] = true,
        };

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
        "expected_documentation_revisions",
        "expected_passages",
        "expected_candidate_decisions",
        "expected_reconciliation_assessments",
        "expected_lineage_events",
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
            ["expected_documentation_revisions"] = ["documentation-revision"],
            ["expected_passages"] = ["passage"],
            ["expected_candidate_decisions"] = ["candidate-decision"],
            ["expected_reconciliation_assessments"] = ["reconciliation-assessment"],
            ["expected_lineage_events"] = ["lineage-event"],
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
        "accepted_order_construction_input",
        "analysis_context_input",
        "effective_scan_configuration",
        "analysis_execution_input",
        "case_matrix_input",
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
        "expected_documentation_revisions",
        "expected_passages",
        "expected_candidate_decisions",
        "expected_reconciliation_assessments",
        "expected_lineage_events",
        "expected_coverage_and_gaps",
        "expected_collection_states",
        "expected_taxonomy_assignments",
        "expected_replayability",
        "expected_replay_manifest",
        "expected_not_used_boundaries",
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
        ActiveJsonSchemaValidator.Validate(root, "fixture-execution-input.v1.schema.json");
        EnsureOnlyProperties(root, ExecutionInputProperties, ExecutionInputFileName);
        RejectAnswerBearingProperties(root);

        OpaqueId fixtureId = new(RequireString(root, "fixture_id"));
        ContractVersion fixtureVersion = ContractVersion.Parse(RequireString(root, "fixture_version"));
        return new ExecutionFixturePackage(fixtureId, fixtureVersion, root.Clone());
    }

    public static PublicFixturePackage Read(string fixtureDirectory) =>
        Read(fixtureDirectory, testOptions: null);

    internal static PublicFixturePackage Read(
        string fixtureDirectory,
        RetainedArtifactValidationTestOptions? testOptions)
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

        ActiveJsonSchemaValidator.Validate(publicManifest, "fixture-public-manifest.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(executionInput, "fixture-execution-input.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(oracle, "fixture-oracle.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(provenance, "fixture-provenance.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(replayDependencies, "replay-dependencies.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(redistribution, "fixture-redistribution.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(partitionHistory, "fixture-partition-history.v1.schema.json");
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
        RetainedArtifactBudget retainedArtifactBudget = new(
            testOptions?.MaximumReferenceCount ?? MaximumRetainedArtifactReferences,
            testOptions?.MaximumAggregateBytes ?? MaximumRetainedAggregateBytes);
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> inputSnapshots =
            ValidateRetainedArtifactReferences(
                executionInput,
                fullDirectory,
                InputArtifactPrefix,
                ExecutionInputFileName,
                retainedArtifactBudget,
                testOptions);
        Dictionary<string, RetainedArtifactSnapshot> oracleSnapshots =
            ValidateRetainedArtifactReferences(
                oracle,
                fullDirectory,
                OracleArtifactPrefix,
                OracleFileName,
                retainedArtifactBudget,
                testOptions);
        ValidateOracleArtifactClosure(fullDirectory, oracleSnapshots.Keys);
        bool hasBethesdaByteOracle = oracleSnapshots.ContainsKey(
            PublicBethesdaOracleValidator.ArtifactId);
        JsonElement? acceptedOrderConstructionReference = null;
        if (AcceptedBethesdaFixtureProfiles.TryGetValue(
                fixtureId.Value,
                out bool requiresTaxonomyArtifacts))
        {
            ValidateRequiredBethesdaArtifacts(
                requiresTaxonomyArtifacts,
                inputSnapshots,
                oracleSnapshots);
            acceptedOrderConstructionReference = ValidateBethesdaExecutionControls(
                fixtureId,
                fixtureVersion,
                executionInput,
                inputSnapshots);
            ValidateInputArtifactClosure(fullDirectory, inputSnapshots.Keys);
        }
        ValidateTaxonomySubjectContract(
            fixtureId,
            fixtureVersion,
            inputSnapshots,
            oracleSnapshots,
            acceptedOrderConstructionReference);
        if (hasBethesdaByteOracle)
        {
            ValidateRootDocumentClosure(fullDirectory);
            ValidateInputByteBudget(executionInput, inputSnapshots);
        }

        testOptions?.AfterArtifactsSnapshotted?.Invoke();
        PublicBethesdaOracleValidator.Validate(
            executionInput,
            oracle,
            fixtureId,
            fixtureVersion,
            inputSnapshots,
            oracleSnapshots);

        return new PublicFixturePackage(
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

    private static void ValidateRequiredBethesdaArtifacts(
        bool requiresTaxonomyArtifacts,
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> inputSnapshots,
        Dictionary<string, RetainedArtifactSnapshot> oracleSnapshots)
    {
        if (!oracleSnapshots.ContainsKey(PublicBethesdaOracleValidator.ArtifactId))
        {
            throw new InvalidDataException(
                "Accepted Bethesda fixture packages must retain the independent byte oracle.");
        }

        bool hasTaxonomyProjection = oracleSnapshots.ContainsKey(TaxonomyProjectionArtifactId);
        bool hasTaxonomyBindings = inputSnapshots.ContainsKey(TaxonomySubjectBindingsArtifactId);
        if (hasTaxonomyProjection != requiresTaxonomyArtifacts
            || hasTaxonomyBindings != requiresTaxonomyArtifacts)
        {
            throw new InvalidDataException(
                "Accepted Bethesda fixture package taxonomy artifacts do not match its registry profile.");
        }
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

            if (!ExpectedCollectionStateNames.TryGetValue(collection, out string? collectionStateName))
            {
                continue;
            }

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

                    if (property.NameEquals("artifact_id")
                        && property.Value.ValueKind == JsonValueKind.String
                        && property.Value.GetString() is string artifactId)
                    {
                        RejectAnswerBearingArtifactId(artifactId);
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

    private static void RejectAnswerBearingArtifactId(string artifactId)
    {
        string normalized = artifactId.Replace('\\', '/');
        if (normalized.StartsWith('/')
            || normalized.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || (normalized.Length >= 2
                && char.IsAsciiLetter(normalized[0])
                && normalized[1] == ':'))
        {
            throw new InvalidDataException(
                $"Execution input contains private filesystem artifact locator '{artifactId}'.");
        }

        if (normalized.StartsWith(OracleArtifactPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Execution input contains oracle artifact ID '{artifactId}'.");
        }

        foreach (string segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] tokens = segment.Split(
                ['.', '-', '_', ' '],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Any(
                    token => token.Equals("answer", StringComparison.OrdinalIgnoreCase)
                        || token.Equals("answers", StringComparison.OrdinalIgnoreCase)
                        || token.Equals("oracle", StringComparison.OrdinalIgnoreCase)
                        || token.Equals("expected", StringComparison.OrdinalIgnoreCase))
                || segment.Contains("answer-bearing", StringComparison.OrdinalIgnoreCase)
                || segment.Contains("ground-truth", StringComparison.OrdinalIgnoreCase)
                || segment.Contains("ground_truth", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Execution input contains answer-bearing artifact ID '{artifactId}'.");
            }
        }
    }

    private static Dictionary<string, RetainedArtifactSnapshot>
        ValidateRetainedArtifactReferences(
        JsonElement element,
        string fixtureDirectory,
        string requiredPrefix,
        string documentName,
        RetainedArtifactBudget budget,
        RetainedArtifactValidationTestOptions? testOptions)
    {
        Dictionary<string, RetainedArtifactSnapshot> snapshots =
            new(StringComparer.OrdinalIgnoreCase);
        CollectRetainedArtifactReferences(
            element,
            fixtureDirectory,
            requiredPrefix,
            documentName,
            budget,
            testOptions,
            snapshots);
        return snapshots;
    }

    private static void CollectRetainedArtifactReferences(
        JsonElement element,
        string fixtureDirectory,
        string requiredPrefix,
        string documentName,
        RetainedArtifactBudget budget,
        RetainedArtifactValidationTestOptions? testOptions,
        Dictionary<string, RetainedArtifactSnapshot> snapshots)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("artifact_id", out JsonElement artifactIdElement)
                    && artifactIdElement.ValueKind == JsonValueKind.String
                    && artifactIdElement.GetString() is string artifactId
                    && artifactId.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    if (!artifactId.StartsWith(requiredPrefix, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            $"Package-relative artifact '{artifactId}' in '{documentName}' "
                            + "must use the canonical scoped prefix.");
                    }

                    if (StringComparer.OrdinalIgnoreCase.Equals(
                            artifactId,
                            PublicBethesdaOracleValidator.ArtifactId)
                        && !StringComparer.Ordinal.Equals(
                            artifactId,
                            PublicBethesdaOracleValidator.ArtifactId))
                    {
                        throw new InvalidDataException(
                            "The supplemental Bethesda oracle artifact ID must use "
                            + "its canonical casing.");
                    }

                    budget.AddReference(artifactId);
                    if (snapshots.TryGetValue(artifactId, out RetainedArtifactSnapshot? existingSnapshot))
                    {
                        ValidateRepeatedRetainedArtifactReference(
                            element,
                            artifactId,
                            documentName,
                            existingSnapshot);
                    }
                    else
                    {
                        RetainedArtifactSnapshot snapshot = ValidateRetainedArtifactReference(
                            element,
                            fixtureDirectory,
                            artifactId,
                            requiredPrefix,
                            documentName,
                            budget,
                            testOptions);
                        snapshots.Add(artifactId, snapshot);
                    }
                }

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    CollectRetainedArtifactReferences(
                        property.Value,
                        fixtureDirectory,
                        requiredPrefix,
                        documentName,
                        budget,
                        testOptions,
                        snapshots);
                }

                break;
            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    CollectRetainedArtifactReferences(
                        item,
                        fixtureDirectory,
                        requiredPrefix,
                        documentName,
                        budget,
                        testOptions,
                        snapshots);
                }

                break;
        }
    }

    private static void ValidateRepeatedRetainedArtifactReference(
        JsonElement artifactReference,
        string artifactId,
        string documentName,
        RetainedArtifactSnapshot snapshot)
    {
        ValidateRetainedArtifactReferenceMetadata(
            artifactReference,
            artifactId,
            documentName,
            snapshot,
            "does not exactly match its first retained reference.");
    }

    private static void ValidateRetainedArtifactReferenceMetadata(
        JsonElement artifactReference,
        string artifactId,
        string documentName,
        RetainedArtifactSnapshot snapshot,
        string mismatchReason)
    {
        string artifactVersion = RequireString(artifactReference, "artifact_version");
        string availability = RequireString(artifactReference, "availability");
        string fingerprint = RequireString(artifactReference, "fingerprint");
        bool hasDeclaredByteLength = artifactReference.TryGetProperty(
            "byte_length",
            out JsonElement byteLengthElement);
        long? declaredByteLength = null;
        long parsedByteLength = 0;
        if (hasDeclaredByteLength
            && (byteLengthElement.ValueKind != JsonValueKind.Number
                || !byteLengthElement.TryGetInt64(out parsedByteLength)))
        {
            throw new InvalidDataException(
                $"Retained artifact reference '{artifactId}' in '{documentName}' "
                + "has an invalid declared byte length.");
        }
        else if (hasDeclaredByteLength)
        {
            declaredByteLength = parsedByteLength;
        }

        if (!StringComparer.Ordinal.Equals(artifactId, snapshot.ArtifactId)
            || !StringComparer.Ordinal.Equals(artifactVersion, snapshot.ArtifactVersion)
            || !StringComparer.Ordinal.Equals(availability, snapshot.Availability)
            || !StringComparer.Ordinal.Equals(fingerprint, snapshot.Sha256)
            || hasDeclaredByteLength != snapshot.HasDeclaredByteLength
            || declaredByteLength != snapshot.DeclaredByteLength)
        {
            throw new InvalidDataException(
                $"Retained artifact reference '{artifactId}' in '{documentName}' "
                + mismatchReason);
        }
    }

    private static RetainedArtifactSnapshot ValidateRetainedArtifactReference(
        JsonElement artifactReference,
        string fixtureDirectory,
        string artifactId,
        string requiredPrefix,
        string documentName,
        RetainedArtifactBudget budget,
        RetainedArtifactValidationTestOptions? testOptions)
    {
        string artifactVersion = RequireString(artifactReference, "artifact_version");
        string availability = RequireString(artifactReference, "availability");
        if (!StringComparer.Ordinal.Equals(availability, "retained"))
        {
            throw new InvalidDataException(
                $"Package-relative artifact '{artifactId}' in '{documentName}' must be retained.");
        }

        string[] segments = artifactId.Split('/', StringSplitOptions.None);
        if (artifactId.Contains('\\', StringComparison.Ordinal)
            || artifactId.Contains(':', StringComparison.Ordinal)
            || artifactId.StartsWith('/')
            || segments.Any(IsUnsafeArtifactPathSegment))
        {
            throw new InvalidDataException(
                $"Package-relative artifact '{artifactId}' in '{documentName}' is unsafe.");
        }

        string fullDirectory = Path.GetFullPath(fixtureDirectory);
        string scopedRoot = Path.GetFullPath(
            Path.Combine(fullDirectory, requiredPrefix.TrimEnd('/')));
        string artifactPath = Path.GetFullPath(
            Path.Combine(fullDirectory, artifactId.Replace('/', Path.DirectorySeparatorChar)));
        string scopedRootPrefix = scopedRoot + Path.DirectorySeparatorChar;
        if (!artifactPath.StartsWith(scopedRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Package-relative artifact '{artifactId}' escapes its retained package scope.");
        }

        EnsureNoReparsePoint(scopedRoot, artifactPath, artifactId);
        if (!File.Exists(artifactPath))
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' is missing.");
        }

        testOptions?.BeforeScopePin?.Invoke(artifactId);
        using SafeFileHandle? scopedRootHandle =
            WindowsRetainedArtifactIdentity.OpenPinnedDirectory(scopedRoot, artifactId);
        testOptions?.BeforeArtifactOpen?.Invoke(artifactId);
        using FileStream stream = new(
            artifactPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);
        WindowsRetainedArtifactIdentitySnapshot initialIdentity =
            WindowsRetainedArtifactIdentity.RequireContainedSingleLink(
                stream.SafeFileHandle,
                scopedRootHandle,
                artifactId);
        long maximumArtifactBytes =
            testOptions?.MaximumArtifactBytes ?? MaximumRetainedArtifactBytes;
        if (stream.Length > maximumArtifactBytes)
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' exceeds the byte bound.");
        }

        long maximumReadableBytes = Math.Min(maximumArtifactBytes, budget.RemainingBytes);
        if (stream.Length > maximumReadableBytes)
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' exceeds the aggregate byte bound.");
        }

        byte[] bytes = ReadBoundedRetainedArtifact(stream, artifactId, maximumReadableBytes);
        WindowsRetainedArtifactIdentity.RequireUnchanged(
            stream.SafeFileHandle,
            initialIdentity,
            artifactId);
        budget.AddBytes(bytes.LongLength, artifactId);
        string actualFingerprint = Convert.ToHexStringLower(SHA256.HashData(bytes));
        string expectedFingerprint = RequireString(artifactReference, "fingerprint");
        if (!StringComparer.Ordinal.Equals(expectedFingerprint, actualFingerprint))
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' does not match its fingerprint.");
        }

        bool hasDeclaredByteLength = artifactReference.TryGetProperty(
            "byte_length",
            out JsonElement byteLengthElement);
        long? declaredByteLength = null;
        long parsedByteLength = 0;
        if (hasDeclaredByteLength
            && (byteLengthElement.ValueKind != JsonValueKind.Number
                || !byteLengthElement.TryGetInt64(out parsedByteLength)
                || parsedByteLength != bytes.LongLength))
        {
            throw new InvalidDataException(
                $"Retained package artifact '{artifactId}' does not match its declared byte length.");
        }
        else if (hasDeclaredByteLength)
        {
            declaredByteLength = parsedByteLength;
        }

        return new RetainedArtifactSnapshot(
            artifactId,
            artifactVersion,
            availability,
            hasDeclaredByteLength,
            declaredByteLength,
            bytes,
            actualFingerprint);
    }

    private static bool IsUnsafeArtifactPathSegment(string segment)
    {
        if (segment is "" or "." or ".."
            || segment.EndsWith(' ')
            || segment.EndsWith('.')
            || segment.Contains('*', StringComparison.Ordinal)
            || segment.Contains('?', StringComparison.Ordinal))
        {
            return true;
        }

        string deviceName = segment.Split('.', 2)[0];
        return deviceName.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("CONIN$", StringComparison.OrdinalIgnoreCase)
            || deviceName.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase)
            || (deviceName.Length == 4
                && (deviceName.StartsWith("COM", StringComparison.OrdinalIgnoreCase)
                    || deviceName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                && deviceName[3] is >= '1' and <= '9');
    }

    private static byte[] ReadBoundedRetainedArtifact(
        FileStream stream,
        string artifactId,
        long maximumBytes)
    {
        using MemoryStream buffer = new(
            checked((int)Math.Min(stream.Length, 1024 * 1024)));
        byte[] block = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            int read = stream.Read(block);
            if (read == 0)
            {
                break;
            }

            total = checked(total + read);
            if (total > maximumBytes)
            {
                throw new InvalidDataException(
                    $"Retained package artifact '{artifactId}' exceeds the byte bound while being read.");
            }

            buffer.Write(block, 0, read);
        }

        return buffer.ToArray();
    }

    private static JsonElement ValidateBethesdaExecutionControls(
        OpaqueId fixtureId,
        ContractVersion fixtureVersion,
        JsonElement executionInput,
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> inputSnapshots)
    {
        ValidateNotApplicableExecutionRole(executionInput, "installation_snapshot_input");
        ValidateNotApplicableExecutionRole(executionInput, "plugin_order_input");

        JsonElement acceptedOrderComponent = RequireObject(
            executionInput.GetProperty("accepted_order_construction_input"),
            "accepted_order_construction_input");
        if (!StringComparer.Ordinal.Equals(
                RequireString(acceptedOrderComponent, "state"),
                "provided")
            || !acceptedOrderComponent.TryGetProperty(
                "artifact",
                out JsonElement acceptedOrderReference))
        {
            throw new InvalidDataException(
                "Bethesda execution input must provide the accepted-order construction receipt.");
        }
        ValidateRequiredControlReference(
            acceptedOrderReference,
            TaxonomyAcceptedOrderArtifactId,
            inputSnapshots);

        JsonElement scanConfigurationReference = RequireObject(
            executionInput.GetProperty("effective_scan_configuration"),
            "effective_scan_configuration");
        ValidateRequiredControlReference(
            scanConfigurationReference,
            BethesdaEffectiveScanConfigurationArtifactId,
            inputSnapshots);

        JsonElement caseMatrixComponent = RequireObject(
            executionInput.GetProperty("case_matrix_input"),
            "case_matrix_input");
        if (!StringComparer.Ordinal.Equals(
                RequireString(caseMatrixComponent, "state"),
                "provided")
            || !caseMatrixComponent.TryGetProperty("artifact", out JsonElement caseMatrixReference))
        {
            throw new InvalidDataException(
                "Bethesda execution input must provide an explicit retained case matrix.");
        }
        ValidateRequiredControlReference(
            caseMatrixReference,
            BethesdaCaseMatrixArtifactId,
            inputSnapshots);

        RetainedArtifactSnapshot acceptedOrderSnapshot =
            inputSnapshots[TaxonomyAcceptedOrderArtifactId];
        using BoundedJsonDocumentSnapshot acceptedOrder = BoundedJsonDocumentReader.Parse(
            acceptedOrderSnapshot.Bytes,
            TaxonomyAcceptedOrderArtifactId,
            maximumDepth: 32);
        JsonElement acceptedOrderRoot = RequireObject(
            acceptedOrder.Document.RootElement,
            TaxonomyAcceptedOrderArtifactId);
        ActiveJsonSchemaValidator.Validate(
            acceptedOrderRoot,
            "bethesda-accepted-order-construction-input.v1.schema.json");
        ValidateIdentity(
            acceptedOrderRoot,
            fixtureId,
            fixtureVersion,
            TaxonomyAcceptedOrderArtifactId);
        if (!StringComparer.Ordinal.Equals(
                RequireString(acceptedOrderRoot, "source_basis"),
                "accepted-slice-3.5-construction-manifest-and-retained-input-seals")
            || !StringComparer.Ordinal.Equals(
                RequireString(acceptedOrderRoot, "selected_profile_name"),
                fixtureId.Value))
        {
            throw new InvalidDataException(
                "Bethesda accepted-order construction receipt has an unsupported source basis or profile identity.");
        }
        ValidateAcceptedOrderReceiptContents(acceptedOrderRoot, inputSnapshots);

        RetainedArtifactSnapshot configurationSnapshot =
            inputSnapshots[BethesdaEffectiveScanConfigurationArtifactId];
        using BoundedJsonDocumentSnapshot configuration = BoundedJsonDocumentReader.Parse(
            configurationSnapshot.Bytes,
            BethesdaEffectiveScanConfigurationArtifactId,
            maximumDepth: 64);
        JsonElement configurationRoot = RequireObject(
            configuration.Document.RootElement,
            BethesdaEffectiveScanConfigurationArtifactId);
        ActiveJsonSchemaValidator.Validate(
            configurationRoot,
            "effective-scan-configuration.v1.schema.json");
        if (configurationRoot.TryGetProperty("cases", out _)
            || configurationRoot.TryGetProperty("scenarios", out _))
        {
            throw new InvalidDataException(
                "Effective scan configuration cannot carry evaluation cases or scenarios.");
        }

        RetainedArtifactSnapshot matrixSnapshot = inputSnapshots[BethesdaCaseMatrixArtifactId];
        using BoundedJsonDocumentSnapshot matrix = BoundedJsonDocumentReader.Parse(
            matrixSnapshot.Bytes,
            BethesdaCaseMatrixArtifactId,
            maximumDepth: 32);
        JsonElement matrixRoot = RequireObject(
            matrix.Document.RootElement,
            BethesdaCaseMatrixArtifactId);
        ActiveJsonSchemaValidator.Validate(
            matrixRoot,
            "bethesda-case-matrix.v1.schema.json");
        ValidateIdentity(matrixRoot, fixtureId, fixtureVersion, BethesdaCaseMatrixArtifactId);

        HashSet<string> scenarioIds = new(StringComparer.Ordinal);
        foreach (JsonElement scenario in RequireArray(matrixRoot, "cases").EnumerateArray())
        {
            string scenarioId = RequireString(scenario, "scenario_id");
            if (!scenarioIds.Add(scenarioId))
            {
                throw new InvalidDataException(
                    $"Bethesda case matrix contains duplicate scenario '{scenarioId}'.");
            }

            foreach (JsonElement artifactIdElement in RequireArray(
                         scenario,
                         "input_artifact_ids").EnumerateArray())
            {
                string artifactId = artifactIdElement.GetString()
                    ?? throw new InvalidDataException(
                        $"Bethesda scenario '{scenarioId}' has a non-string input artifact ID.");
                if (!inputSnapshots.TryGetValue(
                        artifactId,
                        out RetainedArtifactSnapshot? scenarioInput)
                    || !StringComparer.Ordinal.Equals(
                        artifactId,
                        scenarioInput.ArtifactId))
                {
                    throw new InvalidDataException(
                        $"Bethesda scenario '{scenarioId}' references unsealed input '{artifactId}'.");
                }
                if (StringComparer.Ordinal.Equals(artifactId, BethesdaCaseMatrixArtifactId)
                    || StringComparer.Ordinal.Equals(
                        artifactId,
                        BethesdaEffectiveScanConfigurationArtifactId))
                {
                    throw new InvalidDataException(
                        $"Bethesda scenario '{scenarioId}' cannot execute a control document.");
                }
            }
        }

        JsonElement payloadReferences = RequireArray(executionInput, "input_payload_refs");
        HashSet<string> payloadIds = new(StringComparer.Ordinal);
        HashSet<string> payloadAliases = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement reference in payloadReferences.EnumerateArray())
        {
            string artifactId = RequireString(reference, "artifact_id");
            if (!payloadAliases.Add(artifactId) || !payloadIds.Add(artifactId))
            {
                throw new InvalidDataException(
                    $"Execution input payload contains duplicate artifact '{artifactId}'.");
            }
            if (!inputSnapshots.TryGetValue(
                    artifactId,
                    out RetainedArtifactSnapshot? snapshot))
            {
                throw new InvalidDataException(
                    $"Execution input payload contains unresolved artifact '{artifactId}'.");
            }
            ValidateRetainedArtifactReferenceMetadata(
                reference,
                artifactId,
                ExecutionInputFileName,
                snapshot,
                "does not exactly match its retained input snapshot.");
        }

        if (!payloadIds.SetEquals(inputSnapshots.Keys))
        {
            throw new InvalidDataException(
                "Execution input payload references must exactly cover every retained input once.");
        }

        return acceptedOrderReference.Clone();
    }

    private static void ValidateNotApplicableExecutionRole(
        JsonElement executionInput,
        string roleName)
    {
        JsonElement role = RequireObject(executionInput.GetProperty(roleName), roleName);
        if (!StringComparer.Ordinal.Equals(RequireString(role, "state"), "not-applicable")
            || role.TryGetProperty("artifact", out _))
        {
            throw new InvalidDataException(
                $"Canonical Bethesda execution role '{roleName}' must be explicitly not applicable.");
        }
    }

    private static void ValidateAcceptedOrderReceiptContents(
        JsonElement receipt,
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> inputSnapshots)
    {
        if (!inputSnapshots.TryGetValue(
                BethesdaConstructionManifestArtifactId,
                out RetainedArtifactSnapshot? constructionManifest)
            || !StringComparer.Ordinal.Equals(
                RequireString(receipt, "construction_manifest_fingerprint"),
                constructionManifest.Sha256))
        {
            throw new InvalidDataException(
                "Bethesda accepted-order receipt does not match the retained construction manifest.");
        }

        using BoundedJsonDocumentSnapshot construction = BoundedJsonDocumentReader.Parse(
            constructionManifest.Bytes,
            BethesdaConstructionManifestArtifactId,
            maximumDepth: 64);
        JsonElement constructionRoot = RequireObject(
            construction.Document.RootElement,
            BethesdaConstructionManifestArtifactId);
        if (!StringComparer.Ordinal.Equals(
                RequireString(constructionRoot, "schema"),
                "infinium.bethesda-fixture-construction")
            || constructionRoot.GetProperty("schema_version").GetInt32() != 1
            || !StringComparer.Ordinal.Equals(
                RequireString(constructionRoot, "package_id"),
                RequireString(receipt, "fixture_id")))
        {
            throw new InvalidDataException(
                "Bethesda retained construction manifest identity is invalid.");
        }
        Dictionary<string, string> constructedPlugins = new(StringComparer.Ordinal);
        Dictionary<string, string> constructedIsolated = new(StringComparer.Ordinal);
        List<string> constructedPluginOrder = [];
        List<string> constructedIsolatedOrder = [];
        HashSet<string> constructionAliases = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement file in RequireArray(constructionRoot, "files").EnumerateArray())
        {
            string relativePath = RequireString(file, "path");
            string artifactId = $"inputs/{relativePath}";
            if (!IsBethesdaPluginArtifact(artifactId)
                || (!relativePath.StartsWith("plugins/", StringComparison.Ordinal)
                    && !relativePath.StartsWith("mutations/", StringComparison.Ordinal)))
            {
                continue;
            }
            if (!constructionAliases.Add(artifactId))
            {
                throw new InvalidDataException(
                    "Bethesda construction manifest contains duplicate plugin artifacts.");
            }
            Dictionary<string, string> destination = relativePath.StartsWith(
                "plugins/",
                StringComparison.Ordinal)
                ? constructedPlugins
                : constructedIsolated;
            destination.Add(artifactId, RequireString(file, "sha256"));
            (ReferenceEquals(destination, constructedPlugins)
                ? constructedPluginOrder
                : constructedIsolatedOrder).Add(artifactId);
        }
        StringComparer constructionOrderComparer = StringComparer.Create(
            CultureInfo.GetCultureInfo("en"),
            ignoreCase: false);
        constructedPluginOrder.Sort(constructionOrderComparer);
        constructedIsolatedOrder.Sort(constructionOrderComparer);

        JsonElement providerOrder = RequireArray(receipt, "provider_order");
        JsonElement pluginOrder = RequireArray(receipt, "plugin_order");
        if (providerOrder.GetArrayLength() == 0
            || providerOrder.GetArrayLength() != pluginOrder.GetArrayLength())
        {
            throw new InvalidDataException(
                "Bethesda accepted-order receipt provider/plugin cardinality is invalid.");
        }

        Dictionary<string, JsonElement> providers = new(StringComparer.Ordinal);
        HashSet<string> providerAliases = new(StringComparer.OrdinalIgnoreCase);
        HashSet<int> priorities = [];
        HashSet<string> providerArtifacts = new(StringComparer.Ordinal);
        HashSet<string> providerArtifactAliases = new(StringComparer.OrdinalIgnoreCase);
        int expectedPriority = 0;
        string providerIdPrefix = RequireString(receipt, "fixture_id").ToLowerInvariant();
        foreach (JsonElement provider in providerOrder.EnumerateArray())
        {
            string providerId = RequireString(provider, "provider_id");
            int priority = provider.GetProperty("priority").GetInt32();
            string artifactId = RequireString(provider, "source_artifact_id");
            string expectedProviderId = $"{providerIdPrefix}-provider-{expectedPriority:D2}";
            if (!StringComparer.Ordinal.Equals(providerId, expectedProviderId)
                || !providerAliases.Add(providerId)
                || !providers.TryAdd(providerId, provider)
                || !priorities.Add(priority)
                || priority != expectedPriority++
                || priority >= constructedPluginOrder.Count
                || !StringComparer.Ordinal.Equals(
                    artifactId,
                    constructedPluginOrder[priority])
                || !providerArtifactAliases.Add(artifactId)
                || !providerArtifacts.Add(artifactId))
            {
                throw new InvalidDataException(
                    "Bethesda accepted-order receipt contains duplicate provider identity or order.");
            }
            ValidateAcceptedOrderArtifactSeal(
                artifactId,
                RequireString(provider, "source_sha256"),
                inputSnapshots,
                constructedPlugins,
                "provider");
        }
        if (!priorities.SetEquals(Enumerable.Range(0, priorities.Count)))
        {
            throw new InvalidDataException(
                "Bethesda accepted-order provider priorities must be contiguous from zero.");
        }

        HashSet<string> pluginArtifacts = new(StringComparer.Ordinal);
        HashSet<string> pluginArtifactAliases = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> pluginNames = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> usedProviderIds = new(StringComparer.Ordinal);
        HashSet<int> loadOrders = [];
        int expectedLoadOrder = 0;
        foreach (JsonElement plugin in pluginOrder.EnumerateArray())
        {
            int loadOrder = plugin.GetProperty("load_order").GetInt32();
            string fileName = RequireString(plugin, "file_name");
            string artifactId = RequireString(plugin, "artifact_id");
            string providerId = RequireString(plugin, "provider_id");
            string pluginSha256 = RequireString(plugin, "sha256");
            string artifactFileName = artifactId[(artifactId.LastIndexOf('/') + 1)..];
            if (!loadOrders.Add(loadOrder)
                || loadOrder != expectedLoadOrder++
                || loadOrder >= constructedPluginOrder.Count
                || !StringComparer.Ordinal.Equals(
                    artifactId,
                    constructedPluginOrder[loadOrder])
                || !pluginNames.Add(fileName)
                || !pluginArtifactAliases.Add(artifactId)
                || !pluginArtifacts.Add(artifactId)
                || !usedProviderIds.Add(providerId)
                || !StringComparer.Ordinal.Equals(fileName, artifactFileName)
                || !providers.TryGetValue(providerId, out JsonElement provider)
                || provider.GetProperty("priority").GetInt32() != loadOrder
                || !StringComparer.Ordinal.Equals(
                    RequireString(provider, "source_artifact_id"),
                    artifactId)
                || !StringComparer.Ordinal.Equals(
                    RequireString(provider, "source_sha256"),
                    pluginSha256))
            {
                throw new InvalidDataException(
                    "Bethesda accepted-order plugin/provider cross-reference is invalid.");
            }
            ValidateAcceptedOrderArtifactSeal(
                artifactId,
                pluginSha256,
                inputSnapshots,
                constructedPlugins,
                "plugin");
        }
        if (!loadOrders.SetEquals(Enumerable.Range(0, loadOrders.Count))
            || !providerArtifacts.SetEquals(pluginArtifacts)
            || !providers.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(usedProviderIds))
        {
            throw new InvalidDataException(
                "Bethesda accepted-order provider/plugin order is not an exact contiguous bijection.");
        }

        HashSet<string> isolatedArtifacts = new(StringComparer.Ordinal);
        HashSet<string> isolatedAliases = new(StringComparer.OrdinalIgnoreCase);
        int isolatedIndex = 0;
        foreach (JsonElement isolated in RequireArray(
                     receipt,
                     "isolated_capture_variants").EnumerateArray())
        {
            string artifactId = RequireString(isolated, "artifact_id");
            if (!isolatedAliases.Add(artifactId)
                || !isolatedArtifacts.Add(artifactId)
                || pluginArtifacts.Contains(artifactId)
                || isolatedIndex >= constructedIsolatedOrder.Count
                || !StringComparer.Ordinal.Equals(
                    artifactId,
                    constructedIsolatedOrder[isolatedIndex++]))
            {
                throw new InvalidDataException(
                    "Bethesda accepted-order receipt contains duplicate or selected isolated input.");
            }
            ValidateAcceptedOrderArtifactSeal(
                artifactId,
                RequireString(isolated, "sha256"),
                inputSnapshots,
                constructedIsolated,
                "isolated capture");
        }
        HashSet<string> expectedIsolatedArtifacts = inputSnapshots.Keys
            .Where(IsBethesdaPluginArtifact)
            .Except(pluginArtifacts, StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        if (!isolatedArtifacts.SetEquals(expectedIsolatedArtifacts))
        {
            throw new InvalidDataException(
                "Bethesda accepted-order isolated inputs do not exactly cover unselected plugin payloads.");
        }
        if (!pluginArtifacts.SetEquals(constructedPlugins.Keys)
            || !isolatedArtifacts.SetEquals(constructedIsolated.Keys))
        {
            throw new InvalidDataException(
                "Bethesda accepted-order selected and isolated inputs do not match the construction manifest.");
        }

        JsonElement captureBinding = JsonSerializer.SerializeToElement(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["providers"] = providerOrder.Clone(),
                ["plugin_order"] = pluginOrder.Clone(),
            });
        string actualCaptureBinding = PublicBethesdaOracleValidator.ComputeCanonicalFingerprint(
            captureBinding);
        if (!StringComparer.Ordinal.Equals(
                RequireString(receipt, "expected_capture_binding_fingerprint"),
                actualCaptureBinding))
        {
            throw new InvalidDataException(
                "Bethesda accepted-order capture-binding fingerprint is stale.");
        }
    }

    private static void ValidateAcceptedOrderArtifactSeal(
        string artifactId,
        string declaredSha256,
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> inputSnapshots,
        Dictionary<string, string> constructionSeals,
        string role)
    {
        if (!IsBethesdaPluginArtifact(artifactId)
            || !inputSnapshots.TryGetValue(artifactId, out RetainedArtifactSnapshot? snapshot)
            || !constructionSeals.TryGetValue(artifactId, out string? constructionSha256)
            || !StringComparer.Ordinal.Equals(snapshot.ArtifactId, artifactId)
            || !StringComparer.Ordinal.Equals(snapshot.Sha256, declaredSha256)
            || !StringComparer.Ordinal.Equals(constructionSha256, declaredSha256))
        {
            throw new InvalidDataException(
                $"Bethesda accepted-order {role} artifact '{artifactId}' is not exactly retained and sealed.");
        }
    }

    private static bool IsBethesdaPluginArtifact(string artifactId)
    {
        if (!artifactId.StartsWith(InputArtifactPrefix, StringComparison.Ordinal))
        {
            return false;
        }
        string fileName = artifactId[(artifactId.LastIndexOf('/') + 1)..];
        string extension = Path.GetExtension(fileName);
        return extension.Equals(".esm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".esp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".esl", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateRequiredControlReference(
        JsonElement reference,
        string requiredArtifactId,
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> inputSnapshots)
    {
        string artifactId = RequireString(reference, "artifact_id");
        if (!StringComparer.Ordinal.Equals(artifactId, requiredArtifactId)
            || !inputSnapshots.TryGetValue(
                requiredArtifactId,
                out RetainedArtifactSnapshot? snapshot))
        {
            throw new InvalidDataException(
                $"Bethesda execution control must reference '{requiredArtifactId}'.");
        }
        ValidateRetainedArtifactReferenceMetadata(
            reference,
            requiredArtifactId,
            ExecutionInputFileName,
            snapshot,
            "does not exactly match its retained control snapshot.");
    }

    private static void ValidateInputArtifactClosure(
        string fixtureDirectory,
        IEnumerable<string> referencedArtifactIds)
    {
        string inputRoot = Path.GetFullPath(Path.Combine(fixtureDirectory, "inputs"));
        if (!Directory.Exists(inputRoot)
            || (File.GetAttributes(inputRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "Fixture input root must be a retained non-reparse directory.");
        }

        HashSet<string> referenced = referencedArtifactIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> physical = new(StringComparer.Ordinal);
        Stack<string> pending = new();
        pending.Push(inputRoot);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        "Fixture input closure cannot contain reparse points.");
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!Directory.EnumerateFileSystemEntries(entry).Any())
                    {
                        throw new InvalidDataException(
                            "Fixture input closure cannot contain empty directories.");
                    }
                    pending.Push(entry);
                    continue;
                }

                physical.Add(Path.GetRelativePath(fixtureDirectory, entry)
                    .Replace(Path.DirectorySeparatorChar, '/'));
            }
        }

        if (!physical.SetEquals(referenced))
        {
            string missing = string.Join(", ", referenced.Except(physical, StringComparer.Ordinal));
            string extra = string.Join(", ", physical.Except(referenced, StringComparer.Ordinal));
            throw new InvalidDataException(
                $"Fixture input reference closure is not exact. Missing: [{missing}]. Unreferenced: [{extra}].");
        }
    }

    private static void ValidateInputByteBudget(
        JsonElement executionInput,
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> inputSnapshots)
    {
        long declaredInputBytes = executionInput
            .GetProperty("resource_and_time_limits")
            .GetProperty("input_bytes")
            .GetInt64();
        long retainedInputBytes = inputSnapshots.Values.Aggregate(
            0L,
            static (total, snapshot) => checked(total + snapshot.ByteLength));
        if (declaredInputBytes != retainedInputBytes)
        {
            throw new InvalidDataException(
                "Execution input resource_and_time_limits.input_bytes must exactly equal "
                + "the retained input payload byte total.");
        }
    }

    private static void EnsureNoReparsePoint(
        string scopedRoot,
        string artifactPath,
        string artifactId)
    {
        string? current = scopedRoot;
        while (current is not null)
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                FileAttributes attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        $"Retained package artifact '{artifactId}' crosses a reparse point.");
                }
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(current, artifactPath))
            {
                break;
            }

            string relative = Path.GetRelativePath(current, artifactPath);
            string? nextSegment = relative.Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (nextSegment is null)
            {
                break;
            }

            current = Path.Combine(current, nextSegment);
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

    private static void ValidateRootDocumentClosure(string directory)
    {
        string[] rootDocuments = Directory
            .EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToArray();
        if (rootDocuments.Length != RequiredRootDocumentNames.Count
            || rootDocuments.Any(name => !RequiredRootDocumentNames.Contains(name)))
        {
            throw new InvalidDataException(
                "Fixture root must contain exactly the seven required fixture documents.");
        }
    }

    private static void ValidateOracleArtifactClosure(
        string fixtureDirectory,
        IEnumerable<string> referencedArtifactIds)
    {
        string oracleRoot = Path.GetFullPath(Path.Combine(fixtureDirectory, "oracle"));
        if (!Directory.Exists(oracleRoot))
        {
            if (referencedArtifactIds.Any())
            {
                throw new InvalidDataException("Fixture oracle directory is missing.");
            }

            return;
        }
        if ((File.GetAttributes(oracleRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Fixture oracle root cannot be a reparse point.");
        }

        HashSet<string> referenced = referencedArtifactIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> physical = new(StringComparer.Ordinal);
        Stack<string> pending = new();
        pending.Push(oracleRoot);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Fixture oracle closure cannot contain reparse points.");
                }

                if ((attributes & FileAttributes.Directory) != 0)
                {
                    if (!Directory.EnumerateFileSystemEntries(entry).Any())
                    {
                        throw new InvalidDataException("Fixture oracle closure cannot contain empty directories.");
                    }

                    pending.Push(entry);
                    continue;
                }

                string relative = Path.GetRelativePath(fixtureDirectory, entry)
                    .Replace(Path.DirectorySeparatorChar, '/');
                physical.Add(relative);
            }
        }

        if (!physical.SetEquals(referenced))
        {
            string missing = string.Join(", ", referenced.Except(physical, StringComparer.Ordinal));
            string extra = string.Join(", ", physical.Except(referenced, StringComparer.Ordinal));
            throw new InvalidDataException(
                $"Fixture oracle reference closure is not exact. Missing: [{missing}]. Unreferenced: [{extra}].");
        }
    }

    private static void ValidateTaxonomySubjectContract(
        OpaqueId fixtureId,
        ContractVersion fixtureVersion,
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> inputSnapshots,
        Dictionary<string, RetainedArtifactSnapshot> oracleSnapshots,
        JsonElement? acceptedOrderConstructionReference)
    {
        bool hasProjections = oracleSnapshots.TryGetValue(
            TaxonomyProjectionArtifactId,
            out RetainedArtifactSnapshot? projectionSnapshot);
        bool hasBindings = inputSnapshots.TryGetValue(
            TaxonomySubjectBindingsArtifactId,
            out RetainedArtifactSnapshot? bindingSnapshot);
        if (hasProjections != hasBindings)
        {
            throw new InvalidDataException(
                "Taxonomy projections and exact subject bindings must be retained together.");
        }

        if (!hasProjections)
        {
            return;
        }

        using BoundedJsonDocumentSnapshot projections = BoundedJsonDocumentReader.Parse(
            projectionSnapshot!.Bytes,
            TaxonomyProjectionArtifactId,
            maximumDepth: 64);
        using BoundedJsonDocumentSnapshot bindings = BoundedJsonDocumentReader.Parse(
            bindingSnapshot!.Bytes,
            TaxonomySubjectBindingsArtifactId,
            maximumDepth: 64);
        JsonElement projectionRoot = RequireObject(
            projections.Document.RootElement,
            TaxonomyProjectionArtifactId);
        JsonElement bindingRoot = RequireObject(
            bindings.Document.RootElement,
            TaxonomySubjectBindingsArtifactId);
        ActiveJsonSchemaValidator.Validate(projectionRoot, "taxonomy-projections.v1.schema.json");
        ActiveJsonSchemaValidator.Validate(bindingRoot, "taxonomy-subject-bindings.v1.schema.json");
        ValidateIdentity(projectionRoot, fixtureId, fixtureVersion, TaxonomyProjectionArtifactId);
        ValidateIdentity(bindingRoot, fixtureId, fixtureVersion, TaxonomySubjectBindingsArtifactId);
        ValidateTaxonomyDocumentIdentity(projectionRoot, TaxonomyProjectionArtifactId);
        ValidateTaxonomyDocumentIdentity(bindingRoot, TaxonomySubjectBindingsArtifactId);
        ValidateTaxonomySourceArtifacts(
            projectionRoot,
            inputSnapshots,
            oracleSnapshots,
            acceptedOrderConstructionReference);

        HashSet<string> sealedSubjectIds = new(StringComparer.Ordinal);
        foreach (JsonElement subject in RequireArray(projectionRoot, "subjects").EnumerateArray())
        {
            string subjectId = RequireString(subject, "subject_id");
            if (!sealedSubjectIds.Add(subjectId))
            {
                throw new InvalidDataException($"Duplicate sealed taxonomy subject '{subjectId}'.");
            }

            JsonElement canonicalValue = RequireObject(
                subject.GetProperty("canonical_value"),
                "canonical_value");
            if (!StringComparer.Ordinal.Equals(
                    subjectId,
                    RequireString(canonicalValue, "subject_id")))
            {
                throw new InvalidDataException(
                    $"Sealed taxonomy subject '{subjectId}' has a mismatched canonical subject ID.");
            }

            if (!StringComparer.Ordinal.Equals(
                    fixtureId.Value,
                    RequireString(canonicalValue, "source_package_id")))
            {
                throw new InvalidDataException(
                    $"Sealed taxonomy subject '{subjectId}' has a mismatched source package.");
            }

            string declaredFingerprint = RequireString(subject, "canonical_value_fingerprint");
            string actualFingerprint = PublicBethesdaOracleValidator.ComputeCanonicalFingerprint(
                canonicalValue);
            if (!StringComparer.Ordinal.Equals(declaredFingerprint, actualFingerprint))
            {
                throw new InvalidDataException(
                    $"Sealed taxonomy subject '{subjectId}' has a stale canonical fingerprint.");
            }
        }

        HashSet<string> boundSubjectIds = new(StringComparer.Ordinal);
        HashSet<string> productionSubjectIds = new(StringComparer.Ordinal);
        foreach (JsonElement binding in RequireArray(bindingRoot, "bindings").EnumerateArray())
        {
            string sealedSubjectId = RequireString(binding, "sealed_subject_id");
            string productionSubjectId = RequireString(
                binding,
                "production_subject_participant_id");
            if (!boundSubjectIds.Add(sealedSubjectId))
            {
                throw new InvalidDataException(
                    $"Duplicate taxonomy binding for sealed subject '{sealedSubjectId}'.");
            }

            if (!productionSubjectIds.Add(productionSubjectId))
            {
                throw new InvalidDataException(
                    $"Duplicate taxonomy binding target '{productionSubjectId}'.");
            }
        }

        if (!sealedSubjectIds.SetEquals(boundSubjectIds))
        {
            throw new InvalidDataException(
                "Taxonomy bindings must map every sealed subject exactly once and no unexpected subject.");
        }
    }

    private static void ValidateTaxonomySourceArtifacts(
        JsonElement projectionRoot,
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> inputSnapshots,
        Dictionary<string, RetainedArtifactSnapshot> oracleSnapshots,
        JsonElement? acceptedOrderConstructionReference)
    {
        HashSet<string> expectedSourceIds = new(StringComparer.Ordinal)
        {
            TaxonomyAcceptedOrderArtifactId,
            PublicBethesdaOracleValidator.ArtifactId,
        };
        HashSet<string> actualSourceIds = new(StringComparer.Ordinal);
        HashSet<string> sourceAliases = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonElement source in RequireArray(
                     projectionRoot,
                     "source_artifacts").EnumerateArray())
        {
            string artifactId = RequireString(source, "artifact_id");
            if (!sourceAliases.Add(artifactId))
            {
                throw new InvalidDataException(
                    $"Taxonomy projection contains duplicate source artifact '{artifactId}'.");
            }

            _ = actualSourceIds.Add(artifactId);
            RetainedArtifactSnapshot snapshot = artifactId.StartsWith(
                InputArtifactPrefix,
                StringComparison.OrdinalIgnoreCase)
                ? inputSnapshots.TryGetValue(artifactId, out RetainedArtifactSnapshot? input)
                    ? input
                    : throw new InvalidDataException(
                        $"Taxonomy projection source artifact '{artifactId}' is not retained input.")
                : artifactId.StartsWith(OracleArtifactPrefix, StringComparison.OrdinalIgnoreCase)
                    ? oracleSnapshots.TryGetValue(artifactId, out RetainedArtifactSnapshot? oracle)
                        ? oracle
                        : throw new InvalidDataException(
                            $"Taxonomy projection source artifact '{artifactId}' is not retained oracle evidence.")
                    : throw new InvalidDataException(
                        $"Taxonomy projection source artifact '{artifactId}' is outside retained package scope.");
            ValidateRetainedArtifactReferenceMetadata(
                source,
                artifactId,
                TaxonomyProjectionArtifactId,
                snapshot,
                "does not exactly match its retained source snapshot.");
            if (StringComparer.Ordinal.Equals(artifactId, TaxonomyAcceptedOrderArtifactId))
            {
                if (acceptedOrderConstructionReference is not JsonElement roleReference)
                {
                    throw new InvalidDataException(
                        "Taxonomy projections require an explicit accepted-order construction input role.");
                }
                ValidateExactArtifactReference(
                    source,
                    roleReference,
                    "Taxonomy accepted-order source must exactly equal the normalized execution role reference.");
            }
        }

        if (!actualSourceIds.SetEquals(expectedSourceIds))
        {
            throw new InvalidDataException(
                "Taxonomy projection source artifacts must be exactly the accepted-order receipt "
                + "and independent byte facts.");
        }
    }

    private static void ValidateExactArtifactReference(
        JsonElement actual,
        JsonElement expected,
        string mismatchReason)
    {
        string[] requiredProperties =
        [
            "artifact_id",
            "artifact_version",
            "fingerprint",
            "availability",
        ];
        foreach (string property in requiredProperties)
        {
            if (!StringComparer.Ordinal.Equals(
                    RequireString(actual, property),
                    RequireString(expected, property)))
            {
                throw new InvalidDataException(mismatchReason);
            }
        }

        bool actualHasByteLength = actual.TryGetProperty("byte_length", out JsonElement actualBytes);
        bool expectedHasByteLength = expected.TryGetProperty("byte_length", out JsonElement expectedBytes);
        if (actualHasByteLength != expectedHasByteLength
            || (actualHasByteLength && actualBytes.GetInt64() != expectedBytes.GetInt64()))
        {
            throw new InvalidDataException(mismatchReason);
        }
    }

    private static void ValidateTaxonomyDocumentIdentity(JsonElement document, string documentName)
    {
        if (!StringComparer.Ordinal.Equals(
                RequireString(document, "taxonomy_id"),
                ContractConstants.TaxonomyId)
            || !StringComparer.Ordinal.Equals(
                RequireString(document, "taxonomy_version"),
                ContractConstants.TaxonomyVersion))
        {
            throw new InvalidDataException(
                $"'{documentName}' has an unsupported taxonomy identity.");
        }
    }

    private sealed class RetainedArtifactBudget(int maximumReferences, long maximumBytes)
    {
        private int references;
        private long bytes;

        internal long RemainingBytes => maximumBytes - bytes;

        internal void AddReference(string artifactId)
        {
            references = checked(references + 1);
            if (references > maximumReferences)
            {
                throw new InvalidDataException(
                    $"Retained package artifact '{artifactId}' exceeds the reference-count bound.");
            }
        }

        internal void AddBytes(long byteCount, string artifactId)
        {
            bytes = checked(bytes + byteCount);
            if (bytes > maximumBytes)
            {
                throw new InvalidDataException(
                    $"Retained package artifact '{artifactId}' exceeds the aggregate byte bound.");
            }
        }
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
