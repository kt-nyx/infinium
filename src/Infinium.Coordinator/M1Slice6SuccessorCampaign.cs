using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Coordinator;

internal sealed record M1Slice6SuccessorCampaignAuthority(
    string CampaignId, string ManifestSha256, string TerminalCampaignId, string TerminalEventHash,
    string OwnerAmendmentId, string OwnerAmendmentSha256,
    string CredentialAccessAuthorityId, string CredentialAccessAuthoritySha256,
    string CredentialManifestId, string CredentialManifestSha256, string CredentialProfileId,
    string CredentialGenerationId, string CredentialTargetFingerprintSha256,
    string StageSourcesPath, string StageSourcesSha256,
    string? ActiveLedgerPath, string? PredecessorLedgerPath,
    string ProductStateRoot, string ProductStateSnapshotOriginSha256, string SafetyIdentifierProjection,
    DateTimeOffset ExpiresAtUtc);

internal sealed record M1Slice6SuccessorRuntimeAuthority(
    string AuthorityId, string ManifestSha256, string CampaignId, string CampaignManifestSha256,
    string StageManifestId, string StageManifestSha256, string AttemptId, int AttemptOrdinal,
    string RequestId, string ReservationId, string DispatchFenceId, string Kind,
    string PredecessorEventHash, string PredecessorEvidenceId, string PredecessorEvidenceSha256,
    string CredentialAccessAuthorityId, string CredentialAccessAuthoritySha256,
    string ImplementationCommit, string CoordinatorSha256, string HelperSha256,
    string ReviewEvidenceId, string ReviewEvidenceSha256, string OwnerDecisionId,
    string OwnerDecisionSha256, string OwnerAmendmentId, string OwnerAmendmentSha256,
    string OutputRoot, string LedgerPath, string EvidencePath, string SafetyStateRoot,
    string ProductStateSnapshotOriginSha256, string ProductStateCheckpointSha256,
    DateTimeOffset NotBeforeUtc, DateTimeOffset ExpiresAtUtc);

internal sealed record M1Slice6SuccessorIndependentReview(
    string ReviewId, string Kind, string SubjectId, string SubjectSha256,
    bool CorrectionRequired, string? DefectId, string? DiagnosisDisposition,
    string? FailureEvidenceId, string? FailureEvidenceSha256,
    string? CandidateCommit, string ManifestSha256);

internal sealed record M1Slice6HardBudgetAuthority(
    string AmendmentId, string ManifestSha256, string CampaignId,
    string CampaignManifestSha256, string PredecessorLedgerPath,
    string PredecessorLedgerSha256, string PredecessorEventHash,
    long HistoricalCommittedNanoUsd, long MaximumSliceNanoUsd,
    DateTimeOffset ExpiresAtUtc);

internal static class M1Slice6SuccessorAuthorityLoader
{
    internal static readonly string[] SupplementLimitations =
    [
        "actual-adapter-send-count-unverified",
        "credential-read-free-trace-not-independently-retained",
        "exact-containment-predicate-unavailable",
    ];
    internal const string CampaignSchema = "infinium.repository.m1-slice6-successor-campaign-authorization/6.0.0";
    internal const string CampaignSchemaV7 = "infinium.repository.m1-slice6-successor-campaign-authorization/7.0.0";
    internal const string StageSchema = "infinium.repository.m1-slice6-successor-stage-attempt/6.0.0";
    internal const string RuntimeCandidateSchema = "infinium.repository.m1-slice6-successor-runtime-candidate/2.0.0";
    internal const string RuntimeSchema = "infinium.provider.effect-runtime-authority/v3";
    internal const string HardBudgetAmendmentSchema =
        "infinium.repository.m1-slice6-development-campaign-amendment/2.0.0";
    internal const string AttemptEvidenceSchemaV1 = "infinium.m1-s6.successor-attempt-evidence/v1";
    internal const string HistoricalNormalizedAttemptEvidenceSchema =
        "infinium.m1-s6.successor-attempt-evidence-normalized-view/v1";
    internal const string AttemptEvidenceSchema = "infinium.m1-s6.successor-attempt-evidence/v2";
    internal const string AttemptEvidenceSchemaV3 = "infinium.m1-s6.successor-attempt-evidence/v3";
    internal const string AttemptEvidenceSupplementSchema =
        "infinium.m1-s6.successor-attempt-evidence-supplement/v1";
    internal const string RecoveryEvidenceSchema =
        "infinium.m1-s6.successor-authoritative-recovery/v1";
    internal const string IndependentReviewSchemaV1 =
        "infinium.repository.m1-slice6-successor-independent-review/1.0.0";
    internal const string IndependentReviewSchema =
        "infinium.repository.m1-slice6-successor-independent-review/2.0.0";
    internal const string IndependentReviewSchemaV3 =
        "infinium.repository.m1-slice6-successor-independent-review/3.0.0";

    internal static M1Slice6SuccessorIndependentReview Review(
        string path, string kind, string subjectId, string subjectSha256, bool? correctionRequired,
        string? failureEvidenceId = null, string? failureEvidenceSha256 = null,
        bool successorV6 = false)
    {
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(path));
        string repository = FindRepositoryRoot(path);
        string reviewSchema;
        try
        {
            using JsonDocument identityDocument = JsonDocument.Parse(bytes);
            reviewSchema = Text(identityDocument.RootElement, "schema_identity");
        }
        catch (JsonException exception)
        { throw new InvalidDataException("The independent review is not valid closed JSON.", exception); }
        bool supplementReview = kind == "attempt-evidence-supplement";
        bool hardBudgetReview = kind == "hard-budget-amendment";
        string expectedReviewSchema = hardBudgetReview || successorV6 ? IndependentReviewSchemaV3
            : supplementReview ? IndependentReviewSchema : IndependentReviewSchemaV1;
        string schemaPath = Path.Combine(repository, "contracts", "repository",
            hardBudgetReview || successorV6 ? "m1-slice6-successor-independent-review.v3.schema.json"
                : supplementReview ? "m1-slice6-successor-independent-review.v2.schema.json"
                : "m1-slice6-successor-independent-review.v1.schema.json");
        if (reviewSchema != expectedReviewSchema)
        { throw new InvalidDataException("The independent review schema version is not valid for this subject kind."); }
        try
        {
            ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(schemaPath), expectedReviewSchema);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The independent review is not valid closed JSON.", exception);
        }
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema_identity", "review_id", "review_kind", "verdict", "reviewer_id",
            "independent", "provider_effect_used", "subject", "correction", "findings", "reviewed_at_utc");
        JsonElement subject = root.GetProperty("subject");
        Exact(subject, "id", "sha256");
        JsonElement correction = root.GetProperty("correction");
        Exact(correction, "required", "defect_id", "diagnosis_disposition",
            "failure_evidence_id", "failure_evidence_sha256", "candidate_commit");
        bool actualCorrectionRequired = correction.GetProperty("required").GetBoolean();
        string? defectId = NullableText(correction, "defect_id");
        string? diagnosisDisposition = NullableText(correction, "diagnosis_disposition");
        string? actualFailureId = NullableText(correction, "failure_evidence_id");
        string? actualFailureSha = NullableText(correction, "failure_evidence_sha256");
        string? candidateCommit = NullableText(correction, "candidate_commit");
        if (Text(root, "schema_identity") != expectedReviewSchema
            || Text(root, "review_kind") != kind || Text(root, "verdict") != "accept"
            || !root.GetProperty("independent").GetBoolean()
            || root.GetProperty("provider_effect_used").GetBoolean()
            || Text(subject, "id") != subjectId || Text(subject, "sha256") != subjectSha256
            || (correctionRequired.HasValue && actualCorrectionRequired != correctionRequired.Value)
            || actualFailureId != failureEvidenceId || actualFailureSha != failureEvidenceSha256
            || actualCorrectionRequired != (candidateCommit is not null)
            || ((failureEvidenceId is null) != (defectId is null || diagnosisDisposition is null))
            || (failureEvidenceId is null && (defectId is not null || diagnosisDisposition is not null))
            || (failureEvidenceId is not null
                && diagnosisDisposition is not ("external-transient-no-correction" or "local-defect-corrected"))
            || (diagnosisDisposition == "local-defect-corrected") != actualCorrectionRequired
            || !root.GetProperty("findings").EnumerateArray().Select(item => item.GetString()!).SequenceEqual(
                supplementReview
                    ? SupplementLimitations
                    : Array.Empty<string>(), StringComparer.Ordinal))
        {
            throw new InvalidDataException("The independent review does not accept the exact closed successor subject.");
        }
        _ = Utc(Text(root, "reviewed_at_utc"));
        return new(Text(root, "review_id"), kind, subjectId, subjectSha256, actualCorrectionRequired,
            defectId, diagnosisDisposition, actualFailureId, actualFailureSha, candidateCommit, Hash(bytes));
    }

    internal static M1Slice6HardBudgetAuthority HardBudgetAmendment(
        string path, string expectedSha, M1Slice6SuccessorCampaignAuthority campaign)
    {
        (byte[] bytes, string sha) = ExactBytes(path, expectedSha);
        string repository = FindRepositoryRoot(path);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-development-campaign-amendment.v2.schema.json")),
            HardBudgetAmendmentSchema);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema_identity", "amendment_id", "status", "authorized_at_utc",
            "expires_at_utc", "owner_authority", "campaign_binding", "immutable_predecessor", "budget",
            "execution_policy", "stop_conditions");
        JsonElement ownerAuthority = root.GetProperty("owner_authority");
        Exact(ownerAuthority, "task_authority_id", "source", "issued_by", "scope",
            "hard_cost_limit_nano_usd");
        JsonElement binding = root.GetProperty("campaign_binding");
        Exact(binding, "campaign_id", "campaign_manifest_sha256");
        JsonElement predecessor = root.GetProperty("immutable_predecessor");
        Exact(predecessor, "ledger_path", "ledger_sha256", "final_event_hash",
            "historical_committed_nano_usd", "immutable");
        JsonElement budget = root.GetProperty("budget");
        Exact(budget, "maximum_slice_nano_usd", "lower_internal_budget_permitted",
            "per_stage_start_limit", "per_attempt_cost_limit", "parallel_calls");
        JsonElement policy = root.GetProperty("execution_policy");
        Exact(policy, "fresh_identity_per_attempt", "one_start_per_attempt", "sequential_attempts",
            "automatic_retry", "first_structurally_valid_semantic_result", "ordinary_failures");
        string[] stops = root.GetProperty("stop_conditions").EnumerateArray()
            .Select(item => item.GetString() ?? "").ToArray();
        string predecessorPath = ResolveRepositoryPath(repository, Text(predecessor, "ledger_path"));
        DateTimeOffset expiry = Utc(Text(root, "expires_at_utc"));
        if (Text(root, "schema_identity") != HardBudgetAmendmentSchema
            || Text(root, "status") != "owner-authorized"
            || Text(ownerAuthority, "task_authority_id")
                != "infinium.owner-task/20260821-m1-s6-c2b-c3-hard-budget-10usd"
            || Text(ownerAuthority, "source") != "current-task-owner-message"
            || Text(ownerAuthority, "issued_by") != "repository-owner"
            || Text(ownerAuthority, "scope") != "M1-S6-C2B-C2C-C2D-C3"
            || ownerAuthority.GetProperty("hard_cost_limit_nano_usd").GetInt64() != 10_000_000_000
            || Text(binding, "campaign_id")
                != "infinium.m1-s6.successor-campaign/a4f66e58-6456-4c90-a6e2-20260820c2b1"
            || Text(binding, "campaign_manifest_sha256")
                != "ff0a8a1cd499f5639c85fa7d43737643dc4b3494643d150b72d2772fc2fc18ef"
            || HashFile(predecessorPath) != Text(predecessor, "ledger_sha256")
            || Text(predecessor, "final_event_hash")
                != "be76dfd4d47b33e97f8585c66c951df7184b9995e02e9539e2c5bdf2ee089f2b"
            || !predecessor.GetProperty("immutable").GetBoolean()
            || predecessor.GetProperty("historical_committed_nano_usd").GetInt64() != 250_080_000
            || budget.GetProperty("maximum_slice_nano_usd").GetInt64()
                != M1Slice6SuccessorCampaignLedgerV3.SliceMaximumNanoUsd
            || budget.GetProperty("lower_internal_budget_permitted").GetBoolean()
            || budget.GetProperty("per_stage_start_limit").ValueKind != JsonValueKind.Null
            || budget.GetProperty("per_attempt_cost_limit").ValueKind != JsonValueKind.Null
            || budget.GetProperty("parallel_calls").GetBoolean()
            || !policy.GetProperty("fresh_identity_per_attempt").GetBoolean()
            || !policy.GetProperty("one_start_per_attempt").GetBoolean()
            || !policy.GetProperty("sequential_attempts").GetBoolean()
            || policy.GetProperty("automatic_retry").GetBoolean()
            || Text(policy, "first_structurally_valid_semantic_result")
                != "permanent-authoritative-stage-result"
            || Text(policy, "ordinary_failures")
                != "diagnose-correct-review-and-fresh-attempt"
            || !stops.SequenceEqual([
                "hard-budget-exhausted-before-viable-result",
                "secret-or-private-answer-breach",
                "trustworthy-retained-evidence-unpreservable",
                "accepted-product-meaning-change-outside-slice6-required",
                "c3-owner-ready"
            ], StringComparer.Ordinal))
        {
            throw new InvalidDataException("The Slice 6 hard-budget amendment is stale or broadened.");
        }
        _ = Utc(Text(root, "authorized_at_utc"));
        return new(Text(root, "amendment_id"), sha, campaign.CampaignId, campaign.ManifestSha256,
            predecessorPath, Text(predecessor, "ledger_sha256"), Text(predecessor, "final_event_hash"),
            predecessor.GetProperty("historical_committed_nano_usd").GetInt64(),
            budget.GetProperty("maximum_slice_nano_usd").GetInt64(), expiry);
    }

    internal static M1Slice6SuccessorCampaignAuthority Campaign(
        string path, string expectedSha, bool requireRolloverBaseline = false)
    {
        (byte[] bytes, string sha) = ExactBytes(path, expectedSha);
        string repository = FindRepositoryRoot(path);
        using (JsonDocument identity = JsonDocument.Parse(bytes))
        {
            if (Text(identity.RootElement, "schema_identity") == CampaignSchemaV7)
            {
                return CampaignV7(path, bytes, sha, repository, requireRolloverBaseline);
            }
        }
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-campaign-authorization.v6.schema.json")),
            CampaignSchema);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema_identity", "campaign_id", "status", "prepared_at_utc", "expires_at_utc",
            "predecessor_campaign", "owner_amendment", "terminal_predecessor",
            "credential_inheritance", "stage_sources", "limits", "ordered_stages");
        if (Text(root, "schema_identity") != CampaignSchema || Text(root, "status") != "owner-authorized-reviewed-and-admitted")
        {
            throw new InvalidDataException("The successor campaign is not exact admitted v6 authority.");
        }
        JsonElement terminal = root.GetProperty("terminal_predecessor");
        Exact(terminal, "campaign_id", "final_event_hash", "possible_starts", "conservative_nano_usd", "immutable");
        JsonElement credential = root.GetProperty("credential_inheritance");
        Exact(credential, "access_authority_id", "access_authority_path", "access_authority_sha256",
            "manifest_id", "manifest_path", "manifest_sha256", "profile_id", "generation_id",
            "target_fingerprint_sha256", "permitted_boundary");
        JsonElement amendment = root.GetProperty("owner_amendment");
        Exact(amendment, "amendment_id", "path", "sha256", "owner_acceptance_ceremony");
        JsonElement limits = root.GetProperty("limits");
        Exact(limits, "maximum_slice_nano_usd", "historical_committed_nano_usd", "remaining_nano_usd",
            "per_stage_start_limit", "per_attempt_cost_limit",
            "automatic_retry", "parallel_calls", "first_structurally_valid_response");
        JsonElement stageSources = root.GetProperty("stage_sources");
        Exact(stageSources, "source_set_id", "path", "sha256");
        JsonElement[] stages = root.GetProperty("ordered_stages").EnumerateArray().ToArray();
        if (stages.Length != 3)
        { throw new InvalidDataException("The successor campaign stage order is not exact."); }
        for (int index = 0; index < stages.Length; index++)
        {
            JsonElement stage = stages[index];
            Exact(stage, "ordinal", "work_package", "operation");
            string expectedOperation = index switch
            { 0 => "Qualification", 1 => "SourceClaimExtraction", _ => "CandidateInvestigation" };
            if (stage.GetProperty("ordinal").GetInt32() != index + 1
                || Text(stage, "work_package") != "WP" + (index + 9)
                || Text(stage, "operation") != expectedOperation)
            { throw new InvalidDataException("The successor campaign stage limits or order changed."); }
        }
        if (Text(terminal, "final_event_hash") != M1Slice6SuccessorCampaignLedgerV3.RequiredTerminalEventHash
            || terminal.GetProperty("possible_starts").GetInt32() != 1
            || terminal.GetProperty("conservative_nano_usd").GetInt64() != M1Slice6SuccessorCampaignLedgerV3.PriorConservativeNanoUsd
            || !terminal.GetProperty("immutable").GetBoolean()
            || limits.GetProperty("maximum_slice_nano_usd").GetInt64() != M1Slice6SuccessorCampaignLedgerV3.SliceMaximumNanoUsd
            || limits.GetProperty("historical_committed_nano_usd").GetInt64() != 250_080_000
            || limits.GetProperty("remaining_nano_usd").GetInt64() != 9_749_920_000
            || limits.GetProperty("per_stage_start_limit").ValueKind != JsonValueKind.Null
            || limits.GetProperty("per_attempt_cost_limit").ValueKind != JsonValueKind.Null
            || limits.GetProperty("automatic_retry").GetBoolean() || limits.GetProperty("parallel_calls").GetBoolean()
            || Text(limits, "first_structurally_valid_response") != "permanent-stage-authority-stop-further-provider-starts")
        {
            throw new InvalidDataException("The successor limits differ from the exact owner amendment.");
        }
        JsonElement predecessorCampaign = root.GetProperty("predecessor_campaign");
        Exact(predecessorCampaign, "campaign_id", "path", "sha256", "historical_only");
        string predecessorCampaignPath = ResolveRepositoryPath(repository,
            Text(predecessorCampaign, "path"));
        string amendmentPath = ResolveRepositoryPath(repository, Text(amendment, "path"));
        string accessPath = ResolveRepositoryPath(repository, Text(credential, "access_authority_path"));
        string stageSourcesPath = ResolveRepositoryPath(repository, Text(stageSources, "path"));
        if (Text(predecessorCampaign, "campaign_id")
                != "infinium.m1-s6.successor-campaign/a4f66e58-6456-4c90-a6e2-20260820c2b1"
            || HashFile(predecessorCampaignPath) != Text(predecessorCampaign, "sha256")
            || Text(predecessorCampaign, "sha256")
                != "ff0a8a1cd499f5639c85fa7d43737643dc4b3494643d150b72d2772fc2fc18ef"
            || !predecessorCampaign.GetProperty("historical_only").GetBoolean()
            || HashFile(amendmentPath) != Text(amendment, "sha256")
            || Text(amendment, "owner_acceptance_ceremony") != "satisfied-by-current-owner-task-message"
            || HashFile(accessPath) != Text(credential, "access_authority_sha256")
            || Text(stageSources, "source_set_id")
                != "infinium.m1-s6.successor-stage-sources/20260821-v6"
            || HashFile(stageSourcesPath) != Text(stageSources, "sha256"))
        {
            throw new InvalidDataException("The successor owner amendment or credential-access authority is stale.");
        }
        byte[] stageSourceBytes = File.ReadAllBytes(stageSourcesPath);
        ActiveRepositoryJsonSchemaValidator.Validate(stageSourceBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-stage-sources.v1.schema.json")),
            "infinium.repository.m1-slice6-successor-stage-sources/1.0.0");
        using (JsonDocument stageSourceDocument = JsonDocument.Parse(stageSourceBytes))
        {
            JsonElement sourceRoot = stageSourceDocument.RootElement;
            Exact(sourceRoot, "schema_identity", "source_set_id", "stages");
            JsonElement[] sourceStages = sourceRoot.GetProperty("stages").EnumerateArray().ToArray();
            if (Text(sourceRoot, "source_set_id") != Text(stageSources, "source_set_id")
                || sourceStages.Length != 3)
            { throw new InvalidDataException("The successor stage source set identity or count changed."); }
            for (int index = 0; index < sourceStages.Length; index++)
            {
                JsonElement sourceStage = sourceStages[index];
                Exact(sourceStage, "ordinal", "operation", "request_source", "validation_package");
                if (sourceStage.GetProperty("ordinal").GetInt32() != index + 1
                    || Text(sourceStage, "operation") != Text(stages[index], "operation"))
                { throw new InvalidDataException("The successor stage source order changed."); }
                RequireSourceFile(repository, sourceStage.GetProperty("request_source"));
                JsonElement package = sourceStage.GetProperty("validation_package");
                Exact(package, "package_id", "manifest", "product_input", "predecessor_manifest",
                    "oracle", "deterministic_oracle_result_sha256", "semantic_use");
                foreach (string name in new[] { "manifest", "product_input", "predecessor_manifest", "oracle" })
                { RequireSourceFile(repository, package.GetProperty(name)); }
            }
        }
        string retainedProductStateRoot;
        string retainedSafetyProjection;
        string retainedSnapshotOriginSha256;
        using (JsonDocument access = JsonDocument.Parse(File.ReadAllBytes(accessPath)))
        {
            ActiveRepositoryJsonSchemaValidator.Validate(File.ReadAllBytes(accessPath),
                File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                    "m1-slice6-successor-credential-access.v2.schema.json")),
                "infinium.repository.m1-slice6-successor-credential-access/2.0.0");
            JsonElement accessRoot = access.RootElement;
            Exact(accessRoot, "schema_identity", "authority_id", "status", "expires_at_utc",
                "owner_amendment", "retained_profile", "retained_product_state",
                "accepted_enrollment_evidence", "per_attempt_boundary");
            JsonElement retained = accessRoot.GetProperty("retained_profile");
            Exact(retained, "manifest_id", "manifest_path", "manifest_sha256", "profile_id",
                "generation_id", "target_fingerprint_sha256");
            JsonElement productState = accessRoot.GetProperty("retained_product_state");
            Exact(productState, "source_root_absolute", "successor_root_absolute", "snapshot_origin_path",
                "snapshot_origin_sha256", "safety_identifier_projection", "reuse_disposition");
            JsonElement accepted = accessRoot.GetProperty("accepted_enrollment_evidence");
            Exact(accepted, "evidence_id", "evidence_sha256", "acceptance_path", "acceptance_sha256");
            JsonElement accessAmendment = accessRoot.GetProperty("owner_amendment");
            Exact(accessAmendment, "amendment_id", "path", "sha256");
            JsonElement boundary = accessRoot.GetProperty("per_attempt_boundary");
            Exact(boundary, "masked_helper_only", "exact_native_call_order", "maximum_reads",
                "maximum_frees", "maximum_writes", "maximum_deletes", "enumeration", "exposure", "replacement");
            string acceptancePath = ResolveRepositoryPath(repository, Text(accepted, "acceptance_path"));
            using JsonDocument acceptanceDocument = JsonDocument.Parse(File.ReadAllBytes(acceptancePath));
            JsonElement acceptedEvidence = acceptanceDocument.RootElement.GetProperty("evidence");
            string[] nativeOrder = boundary.GetProperty("exact_native_call_order").EnumerateArray()
                .Select(item => item.GetString() ?? "").ToArray();
            if (Text(accessRoot, "authority_id") != Text(credential, "access_authority_id")
                || Text(accessRoot, "schema_identity") != "infinium.repository.m1-slice6-successor-credential-access/2.0.0"
                || Text(accessRoot, "status") != "reviewed-and-admitted"
                || Text(retained, "manifest_id") != Text(credential, "manifest_id")
                || Text(retained, "manifest_sha256") != Text(credential, "manifest_sha256")
                || Text(accessAmendment, "path") != Text(amendment, "path")
                || Text(accessAmendment, "sha256") != Text(amendment, "sha256")
                || Text(accessAmendment, "amendment_id") != Text(amendment, "amendment_id")
                || HashFile(acceptancePath) != Text(accepted, "acceptance_sha256")
                || Text(acceptedEvidence, "accepted_success_sha256") != Text(accepted, "evidence_sha256")
                || Text(productState, "reuse_disposition") != "verified-successor-snapshot-only-source-immutable"
                || !ProductUserSafetyIdentifier.IsValidProjection(Text(productState, "safety_identifier_projection"))
                || Utc(Text(accessRoot, "expires_at_utc")) < Utc(Text(root, "expires_at_utc"))
                || !boundary.GetProperty("masked_helper_only").GetBoolean()
                || boundary.GetProperty("maximum_reads").GetInt32() != 1
                || boundary.GetProperty("maximum_frees").GetInt32() != 1
                || boundary.GetProperty("maximum_writes").GetInt32() != 0
                || boundary.GetProperty("maximum_deletes").GetInt32() != 0
                || !nativeOrder.SequenceEqual(["CredReadW", "CredFree"], StringComparer.Ordinal)
                || Text(boundary, "enumeration") != "prohibited"
                || Text(boundary, "exposure") != "prohibited"
                || Text(boundary, "replacement") != "prohibited")
            { throw new InvalidDataException("The successor credential-access boundary changed."); }
            string sourceProductStateRoot = Path.GetFullPath(Text(productState, "source_root_absolute"));
            string productStateRoot = Path.GetFullPath(Text(productState, "successor_root_absolute"));
            if (!Path.IsPathFullyQualified(sourceProductStateRoot) || !Directory.Exists(sourceProductStateRoot)
                || !Path.IsPathFullyQualified(productStateRoot) || !Directory.Exists(productStateRoot)
                || sourceProductStateRoot.Equals(productStateRoot, StringComparison.OrdinalIgnoreCase))
            { throw new InvalidDataException("The retained source or distinct successor product-state root is unavailable."); }
            string originPath = Path.GetFullPath(Path.Combine(productStateRoot, Text(productState, "snapshot_origin_path")));
            if (!originPath.StartsWith(productStateRoot.TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || HashFile(originPath) != Text(productState, "snapshot_origin_sha256"))
            { throw new InvalidDataException("The successor product-state snapshot origin is stale."); }
            ValidateProductStateSnapshot(repository, originPath, sourceProductStateRoot, productStateRoot);
            retainedProductStateRoot = productStateRoot;
            retainedSnapshotOriginSha256 = Text(productState, "snapshot_origin_sha256");
            retainedSafetyProjection = Text(productState, "safety_identifier_projection");
        }
        string credentialPath = ResolveRepositoryPath(repository, Text(credential, "manifest_path"));
        if (HashFile(credentialPath) != Text(credential, "manifest_sha256"))
        {
            throw new InvalidDataException("The inherited retained credential manifest bytes are stale.");
        }
        using JsonDocument credentialDocument = JsonDocument.Parse(File.ReadAllBytes(credentialPath));
        JsonElement profile = credentialDocument.RootElement.GetProperty("profile");
        if (Text(credentialDocument.RootElement, "manifest_id") != Text(credential, "manifest_id")
            || Text(profile, "access_profile_id") != Text(credential, "profile_id")
            || Text(profile, "generation_id") != Text(credential, "generation_id")
            || Text(profile, "target_fingerprint_sha256") != Text(credential, "target_fingerprint_sha256")
            || Text(credential, "permitted_boundary") != "masked-helper-exact-CredReadW-CredFree-only")
        {
            throw new InvalidDataException("The inherited credential identity or read/free boundary changed.");
        }
        DateTimeOffset expiry = Utc(Text(root, "expires_at_utc"));
        return new(Text(root, "campaign_id"), sha, Text(terminal, "campaign_id"),
            Text(terminal, "final_event_hash"), Text(amendment, "amendment_id"),
            Text(amendment, "sha256"), Text(credential, "access_authority_id"),
            Text(credential, "access_authority_sha256"), Text(credential, "manifest_id"),
            Text(credential, "manifest_sha256"), Text(credential, "profile_id"),
            Text(credential, "generation_id"), Text(credential, "target_fingerprint_sha256"),
            stageSourcesPath, Text(stageSources, "sha256"),
            null, null,
            retainedProductStateRoot, retainedSnapshotOriginSha256, retainedSafetyProjection, expiry);
    }

    private static M1Slice6SuccessorCampaignAuthority CampaignV7(
        string path, byte[] bytes, string sha, string repository, bool requireRolloverBaseline)
    {
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-campaign-authorization.v7.schema.json")),
            CampaignSchemaV7);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema_identity", "campaign_id", "status", "prepared_at_utc", "expires_at_utc",
            "predecessor_campaign", "owner_amendment", "terminal_predecessor", "ledger_predecessor",
            "active_ledger", "credential_inheritance", "stage_sources", "limits", "ordered_stages");
        JsonElement predecessor = root.GetProperty("predecessor_campaign");
        JsonElement amendment = root.GetProperty("owner_amendment");
        JsonElement terminal = root.GetProperty("terminal_predecessor");
        JsonElement ledger = root.GetProperty("ledger_predecessor");
        JsonElement activeLedger = root.GetProperty("active_ledger");
        JsonElement credential = root.GetProperty("credential_inheritance");
        JsonElement sources = root.GetProperty("stage_sources");
        JsonElement limits = root.GetProperty("limits");
        Exact(predecessor, "id", "path", "sha256", "historical_only");
        Exact(amendment, "id", "path", "sha256");
        Exact(terminal, "campaign_id", "final_event_hash", "possible_starts",
            "conservative_nano_usd", "immutable");
        Exact(ledger, "path", "sha256", "final_sequence", "final_event_hash",
            "wp9_possible_starts", "wp10_possible_starts", "wp11_possible_starts",
            "wp9_authoritative", "wp10_authoritative", "wp11_authoritative",
            "successor_cumulative_reserved_nano_usd", "successor_unresolved_nano_usd",
            "successor_settled_nano_usd", "successor_outstanding_reserved_nano_usd",
            "committed_nano_usd", "immutable");
        Exact(activeLedger, "path", "schema_identity", "genesis_sequence", "single_writer", "forks");
        Exact(credential, "access_authority_id", "access_authority_path", "access_authority_sha256",
            "manifest_id", "manifest_path", "manifest_sha256", "profile_id", "generation_id",
            "generation_ordinal", "target_fingerprint_sha256", "permitted_boundary");
        Exact(sources, "source_set_id", "path", "sha256");
        Exact(limits, "maximum_slice_nano_usd", "historical_committed_nano_usd",
            "remaining_nano_usd", "per_stage_start_limit", "per_attempt_cost_limit",
            "automatic_retry", "parallel_calls", "first_structurally_valid_response");
        JsonElement[] stages = root.GetProperty("ordered_stages").EnumerateArray().ToArray();
        if (Text(root, "status") != "owner-authorized-reviewed-and-admitted-generation-2"
            || stages.Length != 3
            || limits.GetProperty("maximum_slice_nano_usd").GetInt64() != 10_000_000_000
            || limits.GetProperty("historical_committed_nano_usd").GetInt64() != 910_560_000
            || limits.GetProperty("remaining_nano_usd").GetInt64() != 9_089_440_000
            || limits.GetProperty("per_stage_start_limit").ValueKind != JsonValueKind.Null
            || limits.GetProperty("per_attempt_cost_limit").ValueKind != JsonValueKind.Null
            || limits.GetProperty("automatic_retry").GetBoolean()
            || limits.GetProperty("parallel_calls").GetBoolean()
            || Text(limits, "first_structurally_valid_response")
                != "permanent-stage-authority-stop-further-provider-starts")
        {
            throw new InvalidDataException("The generation-2 campaign limits are not exact.");
        }
        for (int index = 0; index < stages.Length; index++)
        {
            Exact(stages[index], "ordinal", "work_package", "operation");
            string operation = index switch
            { 0 => "Qualification", 1 => "SourceClaimExtraction", _ => "CandidateInvestigation" };
            if (stages[index].GetProperty("ordinal").GetInt32() != index + 1
                || Text(stages[index], "work_package") != $"WP{index + 9}"
                || Text(stages[index], "operation") != operation)
            { throw new InvalidDataException("The generation-2 campaign stage order changed."); }
        }

        string predecessorPath = ResolveRepositoryPath(repository, Text(predecessor, "path"));
        if (Text(predecessor, "id") != "infinium.m1-s6.successor-campaign-v6/20260821-hard-budget"
            || HashFile(predecessorPath) != Text(predecessor, "sha256")
            || !predecessor.GetProperty("historical_only").GetBoolean())
        { throw new InvalidDataException("The generation-2 campaign predecessor is stale."); }
        _ = Campaign(predecessorPath, Text(predecessor, "sha256"));

        string amendmentPath = ResolveRepositoryPath(repository, Text(amendment, "path"));
        if (Text(amendment, "id") != "infinium.m1-s6.hard-budget-continuation/20260821-c2b-c3"
            || HashFile(amendmentPath) != Text(amendment, "sha256")
            || Text(amendment, "sha256")
                != "a79502da0ebea9ded5f6b10b72ad70f8b482d9de28e97e3bd09541936683a5b3")
        { throw new InvalidDataException("The generation-2 campaign owner amendment is stale."); }

        string predecessorLedgerPath = ResolveRepositoryPath(repository, Text(ledger, "path"));
        string activeLedgerPath = ResolveRepositoryPath(repository, Text(activeLedger, "path"));
        if (HashFile(predecessorLedgerPath) != Text(ledger, "sha256")
            || ledger.GetProperty("final_sequence").GetInt64() != 39
            || Text(ledger, "final_event_hash")
                != "b5292326a65791731614a6ab75a0b838de121ea721d01d5489f4c2897f88dcc0"
            || ledger.GetProperty("wp9_possible_starts").GetInt32() != 8
            || ledger.GetProperty("wp10_possible_starts").GetInt32() != 0
            || ledger.GetProperty("wp11_possible_starts").GetInt32() != 0
            || ledger.GetProperty("wp9_authoritative").GetBoolean()
            || ledger.GetProperty("wp10_authoritative").GetBoolean()
            || ledger.GetProperty("wp11_authoritative").GetBoolean()
            || ledger.GetProperty("successor_cumulative_reserved_nano_usd").GetInt64() != 770_560_000
            || ledger.GetProperty("successor_unresolved_nano_usd").GetInt64() != 770_560_000
            || ledger.GetProperty("successor_settled_nano_usd").GetInt64() != 0
            || ledger.GetProperty("successor_outstanding_reserved_nano_usd").GetInt64() != 0
            || ledger.GetProperty("committed_nano_usd").GetInt64() != 910_560_000
            || !ledger.GetProperty("immutable").GetBoolean()
            || Text(activeLedger, "schema_identity")
                != "infinium.repository.m1-slice6-successor-campaign-ledger-entry/4.0.0"
            || activeLedger.GetProperty("genesis_sequence").GetInt64() != 40
            || !activeLedger.GetProperty("single_writer").GetBoolean()
            || Text(activeLedger, "forks") != "prohibited"
            || activeLedgerPath.Equals(predecessorLedgerPath, StringComparison.OrdinalIgnoreCase))
        { throw new InvalidDataException("The generation-2 campaign does not bind the exact v3 ledger tail."); }
        M1Slice6SuccessorCampaignLedgerV3 predecessorLedger = new(
            predecessorLedgerPath,
            "infinium.m1-s6.successor-campaign-v6/20260821-hard-budget",
            Text(predecessor, "sha256"),
            M1Slice6SuccessorCampaignLedgerV3.RequiredTerminalCampaignId,
            M1Slice6SuccessorCampaignLedgerV3.RequiredTerminalEventHash,
            8,
            "be76dfd4d47b33e97f8585c66c951df7184b9995e02e9539e2c5bdf2ee089f2b",
            "infinium.m1-s6.hard-budget-continuation/20260821-c2b-c3",
            "a79502da0ebea9ded5f6b10b72ad70f8b482d9de28e97e3bd09541936683a5b3",
            "infinium.m1-s6.hard-budget-amendment-review/20260821-c2b-c3",
            "cd655e7711c85a9cb746a3a2dcd7baa126378f0451711bee37ddf5ac35bfe103",
            2, 0, 0, false, false, false, 110_080_000, 110_080_000, 0,
            DateTimeOffset.UtcNow);
        if (predecessorLedger.Current.Sequence != 39
            || predecessorLedger.Current.EventHash != Text(ledger, "final_event_hash")
            || predecessorLedger.CommittedNanoUsd != 910_560_000)
        { throw new InvalidDataException("The generation-2 campaign predecessor ledger failed full validation."); }

        string sourcePath = ResolveRepositoryPath(repository, Text(sources, "path"));
        if (Text(sources, "source_set_id") != "infinium.m1-s6.successor-stage-sources/20260821-v6"
            || HashFile(sourcePath) != Text(sources, "sha256"))
        { throw new InvalidDataException("The generation-2 stage sources are stale."); }

        string accessPath = ResolveRepositoryPath(repository, Text(credential, "access_authority_path"));
        byte[] accessBytes = File.ReadAllBytes(accessPath);
        if (Hash(accessBytes) != Text(credential, "access_authority_sha256"))
        { throw new InvalidDataException("The generation-2 credential-access bytes are stale."); }
        ActiveRepositoryJsonSchemaValidator.Validate(accessBytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-credential-access.v3.schema.json")),
            "infinium.repository.m1-slice6-successor-credential-access/3.0.0");
        using JsonDocument accessDocument = JsonDocument.Parse(accessBytes);
        JsonElement access = accessDocument.RootElement;
        Exact(access, "schema_identity", "authority_id", "status", "expires_at_utc", "owner_amendment",
            "predecessor_access", "active_profile", "replacement_closure", "retained_product_state",
            "per_attempt_boundary");
        JsonElement active = access.GetProperty("active_profile");
        JsonElement closure = access.GetProperty("replacement_closure");
        JsonElement productState = access.GetProperty("retained_product_state");
        JsonElement boundary = access.GetProperty("per_attempt_boundary");
        if (Text(access, "authority_id") != Text(credential, "access_authority_id")
            || Text(access, "status") != "reviewed-and-admitted-active-verified-generation-2"
            || Text(active, "manifest_id") != Text(credential, "manifest_id")
            || Text(active, "manifest_sha256") != Text(credential, "manifest_sha256")
            || Text(active, "profile_id") != Text(credential, "profile_id")
            || Text(active, "generation_id") != Text(credential, "generation_id")
            || active.GetProperty("generation_ordinal").GetInt32() != 2
            || Text(active, "target_fingerprint_sha256") != Text(credential, "target_fingerprint_sha256")
            || Text(credential, "permitted_boundary") != "masked-helper-exact-CredReadW-CredFree-only"
            || !boundary.GetProperty("masked_helper_only").GetBoolean()
            || boundary.GetProperty("maximum_reads").GetInt32() != 1
            || boundary.GetProperty("maximum_frees").GetInt32() != 1
            || boundary.GetProperty("maximum_writes").GetInt32() != 0
            || boundary.GetProperty("maximum_deletes").GetInt32() != 0
            || !boundary.GetProperty("exact_native_call_order").EnumerateArray()
                .Select(item => item.GetString()!).SequenceEqual(["CredReadW", "CredFree"], StringComparer.Ordinal)
            || Text(boundary, "enumeration") != "prohibited"
            || Text(boundary, "exposure") != "prohibited"
            || Text(boundary, "replacement") != "prohibited"
            || Utc(Text(access, "expires_at_utc")) < Utc(Text(root, "expires_at_utc")))
        { throw new InvalidDataException("The generation-2 credential-access boundary changed."); }

        string evidencePath = ResolveRepositoryPath(repository, Text(closure, "evidence_path"));
        string reviewPath = ResolveRepositoryPath(repository, Text(closure, "review_path"));
        byte[] evidenceBytes = File.ReadAllBytes(evidencePath);
        if (Hash(evidenceBytes) != Text(closure, "evidence_sha256")
            || HashFile(reviewPath) != Text(closure, "review_sha256"))
        { throw new InvalidDataException("The accepted replacement evidence or review is stale."); }
        ActiveRepositoryJsonSchemaValidator.Validate(evidenceBytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-credential-replacement-evidence.v2.schema.json")),
            "infinium.repository.m1-slice6-successor-credential-replacement-evidence/2.0.0");
        using (JsonDocument evidenceDocument = JsonDocument.Parse(evidenceBytes))
        {
            JsonElement evidence = evidenceDocument.RootElement;
            JsonElement evidenceProfile = evidence.GetProperty("profile");
            JsonElement evidenceState = evidence.GetProperty("product_state");
            if (Text(evidence, "evidence_id") != Text(closure, "evidence_id")
                || Text(evidence, "status") != "passed-active-verified-predecessor-absent"
                || Text(evidenceProfile, "final_generation_id") != Text(credential, "generation_id")
                || evidenceProfile.GetProperty("final_generation_ordinal").GetInt32() != 2
                || Text(evidenceProfile, "final_lifecycle_state") != "active-verified"
                || Text(evidenceProfile, "final_verification_state") != "available"
                || Text(evidenceState, "checkpoint_after_sha256")
                    != Text(productState, "current_checkpoint_sha256"))
            { throw new InvalidDataException("The replacement evidence does not prove active generation 2."); }
        }
        _ = Review(reviewPath, "attempt-evidence", Text(closure, "evidence_id"),
            Text(closure, "evidence_sha256"), false, successorV6: true);

        string credentialPath = ResolveRepositoryPath(repository, Text(credential, "manifest_path"));
        byte[] credentialBytes = File.ReadAllBytes(credentialPath);
        if (Hash(credentialBytes) != Text(credential, "manifest_sha256"))
        { throw new InvalidDataException("The generation-2 credential manifest bytes are stale."); }
        ActiveRepositoryJsonSchemaValidator.Validate(credentialBytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "wp9-production-profile-authorization.v5.schema.json")),
            "infinium.repository.wp9-production-profile-authorization/5.0.0");
        using (JsonDocument credentialDocument = JsonDocument.Parse(credentialBytes))
        {
            JsonElement credentialRoot = credentialDocument.RootElement;
            JsonElement profile = credentialRoot.GetProperty("profile");
            if (Text(credentialRoot, "manifest_id") != Text(credential, "manifest_id")
                || Text(profile, "access_profile_id") != Text(credential, "profile_id")
                || Text(profile, "generation_id") != Text(credential, "generation_id")
                || profile.GetProperty("generation_ordinal").GetInt32() != 2
                || Text(profile, "target_fingerprint_sha256") != Text(credential, "target_fingerprint_sha256"))
            { throw new InvalidDataException("The generation-2 credential manifest identity changed."); }
        }

        string sourceProductRoot = Path.GetFullPath(Text(productState, "source_root_absolute"));
        string productRoot = Path.GetFullPath(Text(productState, "successor_root_absolute"));
        string originPath = Path.GetFullPath(Path.Combine(productRoot, Text(productState, "snapshot_origin_path")));
        if (!Directory.Exists(sourceProductRoot) || !Directory.Exists(productRoot)
            || sourceProductRoot.Equals(productRoot, StringComparison.OrdinalIgnoreCase)
            || !originPath.StartsWith(productRoot.TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || HashFile(originPath) != Text(productState, "snapshot_origin_sha256"))
        { throw new InvalidDataException("The generation-2 product-state lineage is stale."); }
        ValidateProductStateSnapshot(repository, originPath, sourceProductRoot, productRoot);
        CredentialProfileProjection projection =
            AuthoritativeStore.ReadCredentialProfileProjectionReadOnly(productRoot, Text(credential, "profile_id"));
        if (requireRolloverBaseline
            && projection.GenerationId == Text(credential, "generation_id"))
        {
            if (projection.GenerationOrdinal != 2 || projection.LifecycleState != "active-verified"
                || projection.VerificationState != "available" || projection.CleanupDisposition != "not-requested"
                || ComputeProductStateCheckpointSha256(productRoot)
                    != Text(productState, "current_checkpoint_sha256"))
            { throw new InvalidDataException("The generation-2 product-state projection is not exact active-verified authority."); }
        }

        _ = Utc(Text(root, "prepared_at_utc"));
        DateTimeOffset expiry = Utc(Text(root, "expires_at_utc"));
        M1Slice6SuccessorCampaignAuthority authority = new(Text(root, "campaign_id"), sha, Text(terminal, "campaign_id"),
            Text(terminal, "final_event_hash"), Text(amendment, "id"), Text(amendment, "sha256"),
            Text(credential, "access_authority_id"), Text(credential, "access_authority_sha256"),
            Text(credential, "manifest_id"), Text(credential, "manifest_sha256"),
            Text(credential, "profile_id"), Text(credential, "generation_id"),
            Text(credential, "target_fingerprint_sha256"), sourcePath, Text(sources, "sha256"),
            activeLedgerPath, predecessorLedgerPath,
            productRoot, Text(productState, "snapshot_origin_sha256"),
            Text(productState, "safety_identifier_projection"), expiry);
        if (!requireRolloverBaseline)
        {
            return authority;
        }
        return ActiveDevelopmentCredential(repository, authority, projection);
    }

    private static M1Slice6SuccessorCampaignAuthority ActiveDevelopmentCredential(
        string repository, M1Slice6SuccessorCampaignAuthority campaign,
        CredentialProfileProjection projection)
    {
        const string continuationId = "infinium.m1-s6.development-continuation/20260821";
        const string manifestRelative =
            "docs/plans/milestones/m1/slices/s6/m1-slice6-successor-credential-replacement-generation-3-authorization.v2.json";
        const string evidenceRelative =
            "artifacts/m1-slice6/development-credential-continuation/7d47963a0ef5d48c3454d00dbfdf58fd32034058-aa2ef2ccf24647ddab632b2a1d3e7f43/replacement-evidence.v3.json";
        string continuationPath = Path.Combine(repository, "docs", "plans", "milestones", "m1",
            "slices", "s6", "development-continuation.md");
        string manifestPath = ResolveRepositoryPath(repository, manifestRelative);
        string evidencePath = ResolveRepositoryPath(repository, evidenceRelative);
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        byte[] evidenceBytes = File.ReadAllBytes(evidencePath);
        if (HashFile(continuationPath) != "04462e111693defc9f37c95b31a6bea2fe8fac6f2dd40e344ff6a4f1ec5b739b"
            || Hash(manifestBytes) != "d4a93a3e09c2a5e6c489a795ecec971d2b4aae4dd5796be65b55326f0d59504a"
            || Hash(evidenceBytes) != "4016db9308160991b43a49beb3682abed332df0039dcf9bde3916d2469533ccf")
        { throw new InvalidDataException("The practical development credential closure is stale."); }
        ActiveRepositoryJsonSchemaValidator.Validate(manifestBytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-credential-replacement-authorization.v2.schema.json")),
            "infinium.repository.m1-slice6-successor-credential-replacement-authorization/2.0.0");
        ActiveRepositoryJsonSchemaValidator.Validate(evidenceBytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-credential-replacement-evidence.v3.schema.json")),
            "infinium.m1-s6.successor-credential-replacement-evidence/v3");
        using JsonDocument manifestDocument = JsonDocument.Parse(manifestBytes);
        using JsonDocument evidenceDocument = JsonDocument.Parse(evidenceBytes);
        JsonElement manifest = manifestDocument.RootElement;
        JsonElement profile = manifest.GetProperty("profile");
        JsonElement evidence = evidenceDocument.RootElement;
        JsonElement evidenceProfile = evidence.GetProperty("profile");
        JsonElement action = evidence.GetProperty("effect").GetProperty("entry_evidence")
            .GetProperty("ActionSnapshot");
        string profileId = Text(profile, "access_profile_id");
        string generationId = Text(profile, "successor_generation_id");
        string fingerprint = Text(profile, "successor_target_fingerprint_sha256");
        if (Text(manifest, "authority_id")
                != "infinium.m1-s6.successor-credential-replacement-generation-3/a942ce52-81b3-4363-ac16-2e16745f326f"
            || Text(manifest, "status") != "independently-reviewed-ready-for-owner-effect"
            || profile.GetProperty("successor_generation_ordinal").GetInt32() != 3
            || Text(evidence, "status") != "passed-active-verified-predecessor-absent"
            || Text(evidenceProfile, "final_generation_id") != generationId
            || evidenceProfile.GetProperty("final_generation_ordinal").GetInt32() != 3
            || Text(evidenceProfile, "final_lifecycle_state") != "active-verified"
            || Text(evidenceProfile, "final_verification_state") != "available"
            || Text(action, "Action") != "submit"
            || action.GetProperty("CurrentCharacterLength").GetInt32() != 164
            || !action.GetProperty("Admitted").GetBoolean()
            || projection.ProfileId != profileId || projection.GenerationId != generationId
            || projection.GenerationOrdinal != 3 || projection.LifecycleState != "active-verified"
            || projection.VerificationState != "available" || projection.CleanupDisposition != "not-requested")
        { throw new InvalidDataException("The practical development credential is not exact active generation 3."); }
        return campaign with
        {
            CredentialAccessAuthorityId = continuationId,
            CredentialAccessAuthoritySha256 = HashFile(continuationPath),
            CredentialManifestId = Text(manifest, "authority_id"),
            CredentialManifestSha256 = Hash(manifestBytes),
            CredentialProfileId = profileId,
            CredentialGenerationId = generationId,
            CredentialTargetFingerprintSha256 = fingerprint,
        };
    }

    internal static M1Slice6SuccessorCampaignAuthority HistoricalCampaign(
        string path, string expectedSha)
    {
        (byte[] bytes, string sha) = ExactBytes(path, expectedSha);
        if (sha != "ff0a8a1cd499f5639c85fa7d43737643dc4b3494643d150b72d2772fc2fc18ef")
        { throw new InvalidDataException("The historical v5 campaign bytes changed."); }
        string repository = FindRepositoryRoot(path);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-campaign-authorization.v5.schema.json")),
            "infinium.repository.m1-slice6-successor-campaign-authorization/5.0.0");
        string activePath = Path.Combine(repository, "docs", "plans", "milestones", "m1",
            "slices", "s6", "m1-slice6-successor-campaign-authorization.v6.json");
        M1Slice6SuccessorCampaignAuthority active = Campaign(activePath, HashFile(activePath));
        return active with
        {
            CampaignId = "infinium.m1-s6.successor-campaign/a4f66e58-6456-4c90-a6e2-20260820c2b1",
            ManifestSha256 = sha,
        };
    }

    internal static (M1Slice6CampaignStageAuthority Authority, M1Slice6SuccessorAttemptIdentity Attempt)
        Stage(string path, string expectedSha, M1Slice6SuccessorCampaignAuthority campaign,
            M1Slice6HardBudgetAuthority hardBudget, M1Slice6SuccessorRuntimeAuthority runtime)
    {
        (byte[] bytes, string sha) = ExactBytes(path, expectedSha);
        string stageRepository = FindRepositoryRoot(path);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(Path.Combine(stageRepository,
            "contracts", "repository", "m1-slice6-successor-stage-attempt.v6.schema.json")),
            StageSchema);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema_identity", "manifest_id", "status", "campaign_binding", "stage", "attempt",
            "predecessor_evidence", "canonical_request", "transport", "limits", "safety_identifier",
            "validation_package", "execution");
        if (Text(root, "schema_identity") != StageSchema || Text(root, "status") != "reviewed-and-admitted")
        {
            throw new InvalidDataException("The successor stage attempt is not reviewed and admitted.");
        }
        JsonElement binding = root.GetProperty("campaign_binding");
        Exact(binding, "campaign_id", "campaign_manifest_sha256", "credential_manifest_id",
            "credential_manifest_sha256", "hard_budget_amendment_id", "hard_budget_amendment_sha256");
        if (Text(binding, "campaign_id") != campaign.CampaignId
            || Text(binding, "campaign_manifest_sha256") != campaign.ManifestSha256
            || Text(binding, "credential_manifest_id") != campaign.CredentialManifestId
            || Text(binding, "credential_manifest_sha256") != campaign.CredentialManifestSha256
            || Text(binding, "hard_budget_amendment_id") != hardBudget.AmendmentId
            || Text(binding, "hard_budget_amendment_sha256") != hardBudget.ManifestSha256)
        {
            throw new InvalidDataException("The successor stage campaign or credential binding is stale.");
        }
        JsonElement stageNode = root.GetProperty("stage");
        Exact(stageNode, "ordinal", "work_package", "operation");
        int stageOrdinal = stageNode.GetProperty("ordinal").GetInt32();
        M1Slice6CampaignStage stage = stageOrdinal switch
        {
            1 => M1Slice6CampaignStage.Qualification,
            2 => M1Slice6CampaignStage.SourceClaimExtraction,
            3 => M1Slice6CampaignStage.CandidateInvestigation,
            _ => throw new InvalidDataException("The successor stage ordinal is not closed."),
        };
        ProviderOperationKind operation = stage switch
        {
            M1Slice6CampaignStage.Qualification => ProviderOperationKind.TransportQualification,
            M1Slice6CampaignStage.SourceClaimExtraction => ProviderOperationKind.SourceClaimExtraction,
            _ => ProviderOperationKind.CandidateInvestigation,
        };
        if (Text(stageNode, "work_package") != "WP" + (8 + stageOrdinal)
            || Text(stageNode, "operation") != stage.ToString())
        {
            throw new InvalidDataException("The successor stage work package or operation was swapped.");
        }
        JsonElement attemptNode = root.GetProperty("attempt");
        Exact(attemptNode, "ordinal", "attempt_id", "runtime_authority_id",
            "request_id", "reservation_id", "dispatch_fence_id");
        int attemptOrdinal = attemptNode.GetProperty("ordinal").GetInt32();
        M1Slice6SuccessorAttemptIdentity attempt = new(stage, attemptOrdinal, Text(attemptNode, "attempt_id"),
            Text(root, "manifest_id"), sha, Text(attemptNode, "runtime_authority_id"),
            runtime.ManifestSha256, Text(attemptNode, "request_id"),
            Text(attemptNode, "reservation_id"), Text(attemptNode, "dispatch_fence_id"));
        if (runtime.CampaignId != campaign.CampaignId || runtime.CampaignManifestSha256 != campaign.ManifestSha256
            || runtime.StageManifestId != attempt.StageManifestId || runtime.StageManifestSha256 != sha
            || runtime.AttemptId != attempt.AttemptId || runtime.AttemptOrdinal != attempt.AttemptOrdinal
            || runtime.AuthorityId != attempt.RuntimeAuthorityId
            || runtime.RequestId != attempt.RequestId || runtime.ReservationId != attempt.ReservationId
            || runtime.DispatchFenceId != attempt.DispatchFenceId
            || runtime.CredentialAccessAuthorityId != campaign.CredentialAccessAuthorityId
            || runtime.CredentialAccessAuthoritySha256 != campaign.CredentialAccessAuthoritySha256
            || runtime.OwnerAmendmentId != hardBudget.AmendmentId
            || runtime.OwnerAmendmentSha256 != hardBudget.ManifestSha256
            || runtime.OwnerDecisionId != hardBudget.AmendmentId
            || runtime.OwnerDecisionSha256 != hardBudget.ManifestSha256
            || runtime.ProductStateSnapshotOriginSha256 != campaign.ProductStateSnapshotOriginSha256)
        {
            throw new InvalidDataException("The stage attempt and one-start runtime authority are not exact peers.");
        }
        JsonElement canonicalNode = root.GetProperty("canonical_request");
        Exact(canonicalNode, "path", "sha256", "bytes", "proved_input_tokens", "maximum_output_tokens");
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string canonicalPath = Path.GetFullPath(Path.Combine(directory,
            Text(canonicalNode, "path").Replace('/', Path.DirectorySeparatorChar)));
        if (!canonicalPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The canonical request escaped its attempt directory.");
        }
        byte[] canonical = File.ReadAllBytes(canonicalPath);
        JsonElement limitsNode = root.GetProperty("limits");
        Exact(limitsNode, "maximum_request_bytes", "maximum_input_tokens", "maximum_output_tokens",
            "maximum_raw_response_bytes", "calculated_reservation_nano_usd", "deadline_milliseconds");
        M1Slice6CampaignStageLimits limits = new(
            limitsNode.GetProperty("maximum_request_bytes").GetInt64(),
            limitsNode.GetProperty("maximum_input_tokens").GetInt64(),
            limitsNode.GetProperty("maximum_output_tokens").GetInt64(),
            limitsNode.GetProperty("maximum_raw_response_bytes").GetInt64(),
            limitsNode.GetProperty("calculated_reservation_nano_usd").GetInt64(),
            limitsNode.GetProperty("deadline_milliseconds").GetInt64());
        if (limits.MaximumRequestBytes is < 1 or > 1_000_000
            || limits.MaximumInputTokens is < 1 or > 922_000
            || limits.MaximumOutputTokens is < 1 or > 128_000
            || limits.MaximumRawResponseBytes is < 1 or > 1_048_576
            || limits.MaximumNanoUsd is < 1 or > 9_749_920_000
            || limits.DeadlineMilliseconds is < 1 or > 900_000)
        {
            throw new InvalidDataException("The v6 attempt exceeds provider or active helper feasibility.");
        }
        ProviderFiniteLimitsContract proofLimits = new(limits.MaximumRequestBytes, limits.MaximumInputTokens,
            limits.MaximumOutputTokens, limits.MaximumRawResponseBytes, 1, limits.MaximumNanoUsd,
            limits.DeadlineMilliseconds);
        ProviderInputBoundEvidence proof = OpenAiResponsesInputBoundPolicy.ProveSuccessorV6(operation, canonical, proofLimits);
        long calculatedReservation = M1Slice6SuccessorPricing.Calculate(proofLimits);
        if (Hash(canonical) != Text(canonicalNode, "sha256")
            || canonical.LongLength != canonicalNode.GetProperty("bytes").GetInt64()
            || canonicalNode.GetProperty("proved_input_tokens").GetInt64() != proof.ConservativeInputTokenUpperBound
            || canonicalNode.GetProperty("maximum_output_tokens").GetInt64() != limits.MaximumOutputTokens
            || calculatedReservation != limits.MaximumNanoUsd)
        {
            throw new InvalidDataException("The successor canonical request proof or limits are stale.");
        }
        OpenAiResponsesCanonicalSerializer.ValidateSuccessorV6Profile(canonical, limits.MaximumOutputTokens);
        JsonElement transport = root.GetProperty("transport");
        Exact(transport, "provider", "endpoint", "maximum_provider_starts", "maximum_dns_resolutions",
            "automatic_retry", "parallel");
        if (Text(transport, "provider") != "openai" || Text(transport, "endpoint") != "https://api.openai.com/v1/responses"
            || transport.GetProperty("maximum_provider_starts").GetInt32() != 1
            || transport.GetProperty("maximum_dns_resolutions").GetInt32() != 1
            || transport.GetProperty("automatic_retry").GetBoolean() || transport.GetProperty("parallel").GetBoolean())
        {
            throw new InvalidDataException("A successor runtime may authorize only one non-retrying provider start.");
        }
        JsonElement safety = root.GetProperty("safety_identifier");
        Exact(safety, "projection", "raw_seed_present");
        string projection = Text(safety, "projection");
        if (!ProductUserSafetyIdentifier.IsValidProjection(projection) || safety.GetProperty("raw_seed_present").GetBoolean())
        {
            throw new InvalidDataException("The successor safety identifier is raw or invalid.");
        }
        JsonElement validation = root.GetProperty("validation_package");
        Exact(validation, "package_id", "manifest_path", "manifest_sha256", "product_input_path",
            "product_input_bytes", "product_input_sha256", "predecessor_manifest_path",
            "predecessor_manifest_bytes", "predecessor_manifest_sha256", "oracle_path", "oracle_sha256",
            "deterministic_oracle_result_sha256", "semantic_use");
        string repository = FindRepositoryRoot(path);
        RequireRepositoryFile(repository, validation, "manifest_path", "manifest_sha256", null);
        RequireRepositoryFile(repository, validation, "product_input_path", "product_input_sha256", "product_input_bytes");
        RequireRepositoryFile(repository, validation, "predecessor_manifest_path", "predecessor_manifest_sha256", "predecessor_manifest_bytes");
        RequireRepositoryFile(repository, validation, "oracle_path", "oracle_sha256", null);
        JsonElement predecessor = root.GetProperty("predecessor_evidence");
        Exact(predecessor, "event_hash", "evidence_id", "evidence_sha256");
        if (runtime.PredecessorEventHash != Text(predecessor, "event_hash")
            || runtime.PredecessorEvidenceId != Text(predecessor, "evidence_id")
            || runtime.PredecessorEvidenceSha256 != Text(predecessor, "evidence_sha256"))
        { throw new InvalidDataException("The stage and runtime durable predecessors differ."); }
        string runtimeKind = stage switch
        {
            M1Slice6CampaignStage.Qualification => "transport-qualification",
            M1Slice6CampaignStage.SourceClaimExtraction => "source-claim-extraction",
            _ => "candidate-investigation",
        };
        if (runtime.Kind != runtimeKind)
        { throw new InvalidDataException("The successor runtime kind differs from its exact stage."); }
        JsonElement execution = root.GetProperty("execution");
        Exact(execution, "provider_request_permitted", "requires_durable_admission", "requires_typed_runtime_authority",
            "automatic_retry", "first_structurally_valid_response_stops_stage");
        if (!execution.GetProperty("provider_request_permitted").GetBoolean()
            || !execution.GetProperty("requires_durable_admission").GetBoolean()
            || !execution.GetProperty("requires_typed_runtime_authority").GetBoolean()
            || execution.GetProperty("automatic_retry").GetBoolean()
            || !execution.GetProperty("first_structurally_valid_response_stops_stage").GetBoolean())
        {
            throw new InvalidDataException("The successor attempt execution contract was broadened.");
        }
        return (new(M1Slice6AuthorityContractVersion.SuccessorV6, attempt.StageManifestId, sha, stage,
            Text(stageNode, "work_package"), operation, "successor-v6-hard-budget-owner-authorized",
            Text(predecessor, "evidence_id"), Text(predecessor, "evidence_sha256"), canonicalPath,
            Text(canonicalNode, "sha256"), canonical, proof.ConservativeInputTokenUpperBound, limits,
            projection, Text(validation, "package_id"), Text(validation, "manifest_path"),
            Text(validation, "manifest_sha256"), Text(validation, "product_input_path"),
            validation.GetProperty("product_input_bytes").GetInt64(), Text(validation, "product_input_sha256"),
            Text(validation, "predecessor_manifest_path"), validation.GetProperty("predecessor_manifest_bytes").GetInt64(),
            Text(validation, "predecessor_manifest_sha256"), Text(validation, "oracle_path"),
            Text(validation, "oracle_sha256"), Text(validation, "deterministic_oracle_result_sha256"),
            validation.GetProperty("semantic_use").GetBoolean(), Text(predecessor, "event_hash")), attempt);
    }

    internal static M1Slice6SuccessorRuntimeAuthority Runtime(string path, string expectedSha,
        string coordinatorPath, string helperPath, M1Slice6HardBudgetAuthority hardBudget,
        DateTimeOffset now, bool requireEffectAdmission = true,
        bool requireExecutingAssemblyIdentity = true)
    {
        (byte[] bytes, string sha) = ExactBytes(path, expectedSha);
        string schemaRepository = FindRepositoryRoot(path);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(Path.Combine(schemaRepository,
            "contracts", "json-schema", "provider-effect-runtime-authority.v3.schema.json")), RuntimeSchema);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema_identity", "authority_id", "scope", "kind", "status",
            "subject_manifest", "campaign", "predecessor", "attempt", "credential_access",
            "candidate_binding", "review", "owner_decision", "owner_amendment",
            "not_before_utc", "expires_at_utc", "execution", "limits");
        JsonElement subject = root.GetProperty("subject_manifest");
        JsonElement campaign = root.GetProperty("campaign");
        JsonElement predecessor = root.GetProperty("predecessor");
        JsonElement attempt = root.GetProperty("attempt");
        JsonElement credential = root.GetProperty("credential_access");
        JsonElement candidate = root.GetProperty("candidate_binding");
        JsonElement review = root.GetProperty("review");
        JsonElement owner = root.GetProperty("owner_decision");
        JsonElement amendment = root.GetProperty("owner_amendment");
        JsonElement execution = root.GetProperty("execution");
        JsonElement limits = root.GetProperty("limits");
        Exact(subject, "id", "sha256"); Exact(campaign, "id", "sha256");
        Exact(predecessor, "ledger_event_hash", "evidence_id", "evidence_sha256");
        Exact(attempt, "attempt_id", "attempt_ordinal", "request_id", "reservation_id", "dispatch_fence_id");
        Exact(credential, "id", "sha256");
        Exact(candidate, "candidate_id", "candidate_path", "candidate_sha256",
            "implementation_commit", "coordinator_sha256", "helper_sha256");
        Exact(review, "evidence_id", "evidence_path", "evidence_sha256");
        Exact(owner, "decision_id", "decision_path", "decision_sha256"); Exact(amendment, "id", "sha256");
        Exact(execution, "output_root_relative", "ledger_path_relative", "evidence_path_relative",
            "product_state_root_absolute", "product_state_snapshot_origin_sha256",
            "product_state_checkpoint_sha256",
            "coordinator_path_relative", "helper_path_relative");
        Exact(limits, "helper_launches", "credential_native_calls", "provider_starts",
            "dns_resolutions", "billable_operations", "automatic_retry");
        DateTimeOffset notBefore = Utc(Text(root, "not_before_utc"));
        DateTimeOffset expiry = Utc(Text(root, "expires_at_utc"));
        string repository = FindRepositoryRoot(path);
        string outputRoot = ResolveRepositoryPath(repository, Text(execution, "output_root_relative"));
        string ledgerPath = ResolveRepositoryPath(repository, Text(execution, "ledger_path_relative"));
        string evidencePath = ResolveRepositoryPath(repository, Text(execution, "evidence_path_relative"));
        string expectedCoordinator = ResolveRepositoryPath(repository, Text(execution, "coordinator_path_relative"));
        string expectedHelper = ResolveRepositoryPath(repository, Text(execution, "helper_path_relative"));
        string reviewPath = ResolveRepositoryPath(repository, Text(review, "evidence_path"));
        string runtimeCandidatePath = ResolveRepositoryPath(repository, Text(candidate, "candidate_path"));
        string ownerDecisionPath = ResolveRepositoryPath(repository, Text(owner, "decision_path"));
        string productState = Path.GetFullPath(Text(execution, "product_state_root_absolute"));
        string informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        Match revision = Regex.Match(informational, @"\+(?<sha>[0-9a-f]{40})$");
        string implementationCommit = Text(candidate, "implementation_commit");
        bool developmentContinuation = Text(review, "evidence_id")
            == "infinium.m1-s6.development-continuation/20260821";
        string developmentContinuationPath = Path.Combine(repository, "docs", "plans", "milestones", "m1",
            "slices", "s6", "development-continuation.md");
        if (Text(root, "schema_identity") != RuntimeSchema
            || Text(root, "scope") != "external-effect"
            || Text(root, "status") != "reviewed-and-owner-accepted"
            || Text(root, "kind") is not ("transport-qualification" or "source-claim-extraction" or "candidate-investigation")
            || limits.GetProperty("helper_launches").GetInt32() != 1
            || limits.GetProperty("credential_native_calls").GetInt32() != 2
            || limits.GetProperty("provider_starts").GetInt32() != 1
            || limits.GetProperty("dns_resolutions").GetInt32() != 1
            || limits.GetProperty("billable_operations").GetInt32() != 1
            || limits.GetProperty("automatic_retry").GetBoolean()
            || Text(owner, "decision_id") != hardBudget.AmendmentId
            || Text(owner, "decision_sha256") != hardBudget.ManifestSha256
            || Text(amendment, "id") != hardBudget.AmendmentId
            || Text(amendment, "sha256") != hardBudget.ManifestSha256
            || now.Offset != TimeSpan.Zero || notBefore >= expiry
            || expiry > hardBudget.ExpiresAtUtc
            || requireEffectAdmission && (now < notBefore || now >= expiry)
            || Path.GetFullPath(coordinatorPath) != expectedCoordinator
            || Path.GetFullPath(helperPath) != expectedHelper
            || HashFile(coordinatorPath) != Text(candidate, "coordinator_sha256")
            || HashFile(helperPath) != Text(candidate, "helper_sha256")
            || HashFile(runtimeCandidatePath) != Text(candidate, "candidate_sha256")
            || HashFile(Path.Combine(productState, "successor-snapshot-origin.v1.json"))
                != Text(execution, "product_state_snapshot_origin_sha256")
            || requireEffectAdmission && ComputeProductStateCheckpointSha256(productState)
                != Text(execution, "product_state_checkpoint_sha256")
            || HashFile(reviewPath) != Text(review, "evidence_sha256")
            || developmentContinuation
                && (reviewPath != Path.GetFullPath(developmentContinuationPath)
                    || Text(review, "evidence_sha256")
                        != "04462e111693defc9f37c95b31a6bea2fe8fac6f2dd40e344ff6a4f1ec5b739b")
            || HashFile(ownerDecisionPath) != Text(owner, "decision_sha256")
            || requireExecutingAssemblyIdentity
                && (!revision.Success || revision.Groups["sha"].Value != implementationCommit))
        {
            throw new InvalidDataException("The successor runtime authority is stale, expired, or broader than one start.");
        }
        byte[] runtimeCandidateBytes = File.ReadAllBytes(runtimeCandidatePath);
        ActiveRepositoryJsonSchemaValidator.Validate(runtimeCandidateBytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-runtime-candidate.v2.schema.json")),
            RuntimeCandidateSchema);
        using JsonDocument runtimeCandidateDocument = JsonDocument.Parse(runtimeCandidateBytes);
        JsonElement runtimeCandidate = runtimeCandidateDocument.RootElement;
        Exact(runtimeCandidate, "schema_identity", "candidate_id", "campaign", "subject_manifest",
            "predecessor", "attempt", "credential_access", "implementation_commit",
            "coordinator_sha256", "helper_sha256", "execution", "limits");
        if (Text(runtimeCandidate, "candidate_id") != Text(candidate, "candidate_id")
            || Text(runtimeCandidate, "implementation_commit") != implementationCommit
            || Text(runtimeCandidate, "coordinator_sha256") != Text(candidate, "coordinator_sha256")
            || Text(runtimeCandidate, "helper_sha256") != Text(candidate, "helper_sha256")
            || runtimeCandidate.GetProperty("campaign").GetRawText() != campaign.GetRawText()
            || runtimeCandidate.GetProperty("subject_manifest").GetRawText() != subject.GetRawText()
            || runtimeCandidate.GetProperty("predecessor").GetRawText() != predecessor.GetRawText()
            || runtimeCandidate.GetProperty("attempt").GetRawText() != attempt.GetRawText()
            || runtimeCandidate.GetProperty("credential_access").GetRawText() != credential.GetRawText()
            || runtimeCandidate.GetProperty("execution").GetRawText() != execution.GetRawText()
            || runtimeCandidate.GetProperty("limits").GetRawText() != limits.GetRawText())
        { throw new InvalidDataException("The independently reviewed runtime candidate differs from runtime authority."); }
        if (!developmentContinuation)
        {
            M1Slice6SuccessorIndependentReview typedReview = Review(reviewPath, "runtime-attempt",
                Text(candidate, "candidate_id"), Text(candidate, "candidate_sha256"), false,
                successorV6: true);
            if (typedReview.ReviewId != Text(review, "evidence_id"))
            { throw new InvalidDataException("The runtime authority review identity is stale."); }
        }
        return new(Text(root, "authority_id"), sha, Text(campaign, "id"), Text(campaign, "sha256"),
            Text(subject, "id"), Text(subject, "sha256"), Text(attempt, "attempt_id"),
            attempt.GetProperty("attempt_ordinal").GetInt32(), Text(attempt, "request_id"),
            Text(attempt, "reservation_id"), Text(attempt, "dispatch_fence_id"), Text(root, "kind"),
            Text(predecessor, "ledger_event_hash"), Text(predecessor, "evidence_id"),
            Text(predecessor, "evidence_sha256"), Text(credential, "id"), Text(credential, "sha256"),
            implementationCommit, Text(candidate, "coordinator_sha256"), Text(candidate, "helper_sha256"),
            Text(review, "evidence_id"), Text(review, "evidence_sha256"), Text(owner, "decision_id"),
            Text(owner, "decision_sha256"), Text(amendment, "id"), Text(amendment, "sha256"),
            outputRoot, ledgerPath, evidencePath, productState,
            Text(execution, "product_state_snapshot_origin_sha256"),
            Text(execution, "product_state_checkpoint_sha256"), notBefore, expiry);
    }

    internal static M1Slice6SuccessorRuntimeAuthority RuntimeForRecovery(string path,
        string expectedSha, M1Slice6HardBudgetAuthority hardBudget, DateTimeOffset now)
    {
        (byte[] bytes, _) = ExactBytes(path, expectedSha);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement execution = document.RootElement.GetProperty("execution");
        string repository = FindRepositoryRoot(path);
        string coordinator = ResolveRepositoryPath(repository,
            Text(execution, "coordinator_path_relative"));
        string helper = ResolveRepositoryPath(repository, Text(execution, "helper_path_relative"));
        return Runtime(path, expectedSha, coordinator, helper, hardBudget, now,
            requireEffectAdmission: false, requireExecutingAssemblyIdentity: false);
    }

    private static string ResolveRepositoryPath(string repository, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)
            || relative.Contains('\\') || relative.Split('/').Any(part => part is "" or "." or ".."))
        { throw new InvalidDataException("Successor runtime path is not exact repository-relative authority."); }
        string resolved = Path.GetFullPath(Path.Combine(repository,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!resolved.StartsWith(repository + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        { throw new InvalidDataException("Successor runtime path escaped the executing repository."); }
        return resolved;
    }

    private static void ValidateProductStateSnapshot(string repository, string originPath,
        string sourceRoot, string destinationRoot)
    {
        byte[] bytes = File.ReadAllBytes(originPath);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-product-state-snapshot-origin.v1.schema.json")),
            "infinium.m1-s6.successor-product-state-snapshot-origin/v1");
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema", "source_root", "destination_root", "recorded_after_copy_at_utc",
            "source_post_copy_verified", "files");
        if (Path.GetFullPath(Text(root, "source_root")) != sourceRoot
            || Path.GetFullPath(Text(root, "destination_root")) != destinationRoot
            || !root.GetProperty("source_post_copy_verified").GetBoolean())
        { throw new InvalidDataException("The successor snapshot roots or source proof changed."); }
        JsonElement[] files = root.GetProperty("files").EnumerateArray().ToArray();
        foreach (JsonElement file in files)
        {
            Exact(file, "path", "bytes", "sha256");
            string relative = Text(file, "path");
            string source = Path.GetFullPath(Path.Combine(sourceRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            string destination = Path.GetFullPath(Path.Combine(destinationRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!source.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !destination.StartsWith(destinationRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(source) || new FileInfo(source).Length != file.GetProperty("bytes").GetInt64()
                || HashFile(source) != Text(file, "sha256"))
            { throw new InvalidDataException("The immutable retained source differs from the snapshot origin."); }
        }
        string database = Path.Combine(destinationRoot, "data", "infinium.sqlite3");
        string initialDatabaseSha = Text(files.Single(file => Text(file, "path") == "data/infinium.sqlite3"), "sha256");
        string wal = Path.Combine(destinationRoot, "data", "infinium.sqlite3-wal");
        string initialWalSha = Text(files.Single(file => Text(file, "path") == "data/infinium.sqlite3-wal"), "sha256");
        bool initialDatabaseState = File.Exists(database) && File.Exists(wal)
            && HashFile(database) == initialDatabaseSha && HashFile(wal) == initialWalSha;
        if (initialDatabaseState)
        {
            // SQLite may rewrite the shared-memory coordination file during a read-only
            // open. It is transient physical state and is excluded from the reviewed
            // logical checkpoint; the immutable database, WAL, and retained files remain
            // exact for an initial snapshot.
            foreach (JsonElement file in files.Where(file =>
                         Text(file, "path") != "data/infinium.sqlite3-shm"))
            {
                string destination = Path.Combine(destinationRoot,
                    Text(file, "path").Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(destination) || new FileInfo(destination).Length != file.GetProperty("bytes").GetInt64()
                    || HashFile(destination) != Text(file, "sha256"))
                { throw new InvalidDataException("The initial successor snapshot differs from its byte manifest."); }
            }
            return;
        }
        foreach (JsonElement file in files.Where(file => !Text(file, "path").StartsWith("data/infinium.sqlite3", StringComparison.Ordinal)))
        {
            string destination = Path.Combine(destinationRoot,
                Text(file, "path").Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(destination) || HashFile(destination) != Text(file, "sha256"))
            { throw new InvalidDataException("An immutable successor snapshot file changed after admission."); }
        }
        using SqliteConnection connection = new($"Data Source={database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM store_metadata WHERE key='schema_version';";
        if (command.ExecuteScalar() is not string version || version is not ("7" or "8"))
        { throw new InvalidDataException("The successor product state is not an exact schema-7 predecessor or schema-8 v6 state."); }
        command.CommandText = "SELECT value FROM store_metadata WHERE key='schema_fingerprint';";
        string expectedFingerprint = version == "7"
            ? ProviderPersistenceDeclarations.SuccessorAttemptSchemaFingerprint
            : ProviderPersistenceDeclarations.SuccessorV6PersistenceSchemaFingerprint;
        if (command.ExecuteScalar() is not string fingerprint || fingerprint != expectedFingerprint)
        { throw new InvalidDataException("The evolved successor product-state fingerprint changed."); }
        command.CommandText = "PRAGMA quick_check;";
        if (command.ExecuteScalar() is not string integrity || integrity != "ok")
        { throw new InvalidDataException("The evolved successor product state failed read-only integrity validation."); }
    }

    private static void RequireRepositoryFile(string repository, JsonElement node, string pathName,
        string shaName, string? bytesName)
    {
        string path = ResolveRepositoryPath(repository, Text(node, pathName));
        byte[] bytes = File.ReadAllBytes(path);
        if (Hash(bytes) != Text(node, shaName) || bytesName is not null && bytes.LongLength != node.GetProperty(bytesName).GetInt64())
        { throw new InvalidDataException("A successor validation package file is stale."); }
    }

    private static (byte[] Bytes, string Sha) ExactBytes(string path, string expectedSha)
    {
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(path));
        string sha = Hash(bytes);
        return sha == expectedSha ? (bytes, sha) : throw new InvalidDataException("Authority bytes differ from the expected digest.");
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static string HashFile(string path) => Hash(File.ReadAllBytes(Path.GetFullPath(path)));

    private static void RequireSourceFile(string repository, JsonElement source)
    {
        Exact(source, "path", "bytes", "sha256");
        string path = ResolveRepositoryPath(repository, Text(source, "path"));
        FileInfo info = new(path);
        if (!info.Exists || info.Length != source.GetProperty("bytes").GetInt64()
            || HashFile(path) != Text(source, "sha256"))
        { throw new InvalidDataException("A successor stage source file is stale."); }
    }
    internal static string ComputeProductStateCheckpointSha256(string productStateRoot)
    {
        productStateRoot = Path.GetFullPath(productStateRoot).TrimEnd(Path.DirectorySeparatorChar);
        string sourceDatabase = Path.Combine(productStateRoot, "data", "infinium.sqlite3");
        if (!File.Exists(sourceDatabase))
        { throw new InvalidDataException("The reviewed product-state checkpoint database is absent."); }
        string temporaryRoot = Path.Combine(Path.GetTempPath(),
            "infinium-m1s6-state-checkpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            string snapshotDatabase = Path.Combine(temporaryRoot, "checkpoint.sqlite3");
            using (SqliteConnection source = new(new SqliteConnectionStringBuilder
            { DataSource = sourceDatabase, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            using (SqliteConnection destination = new(new SqliteConnectionStringBuilder
            { DataSource = snapshotDatabase, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
            {
                source.Open();
                using SqliteCommand check = source.CreateCommand();
                check.CommandText = "PRAGMA quick_check;";
                if (check.ExecuteScalar() is not string integrity || integrity != "ok")
                { throw new InvalidDataException("The reviewed product-state checkpoint failed integrity validation."); }
                destination.Open();
                source.BackupDatabase(destination);
            }
            using IncrementalHash digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            AppendCheckpointFact(digest, "infinium.m1-s6.product-state-checkpoint/v1");
            AppendCheckpointFact(digest, new FileInfo(snapshotDatabase).Length.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            AppendCheckpointFact(digest, HashFile(snapshotDatabase));
            string[] mutableDatabaseFiles =
            [
                Path.GetFullPath(sourceDatabase),
                Path.GetFullPath(sourceDatabase + "-wal"),
                Path.GetFullPath(sourceDatabase + "-shm"),
            ];
            foreach (string file in Directory.EnumerateFiles(productStateRoot, "*", SearchOption.AllDirectories)
                .Select(Path.GetFullPath)
                .Where(path => !mutableDatabaseFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetRelativePath(productStateRoot, path).Replace('\\', '/'), StringComparer.Ordinal))
            {
                string relative = Path.GetRelativePath(productStateRoot, file).Replace('\\', '/');
                AppendCheckpointFact(digest, relative);
                AppendCheckpointFact(digest, new FileInfo(file).Length.ToString(
                    System.Globalization.CultureInfo.InvariantCulture));
                AppendCheckpointFact(digest, HashFile(file));
            }
            return Convert.ToHexStringLower(digest.GetHashAndReset());
        }
        finally
        {
            if (Directory.Exists(temporaryRoot)) { Directory.Delete(temporaryRoot, recursive: true); }
        }
    }

    private static void AppendCheckpointFact(IncrementalHash digest, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        digest.AppendData(BitConverter.GetBytes(bytes.Length));
        digest.AppendData(bytes);
    }
    internal static string Text(JsonElement node, string name) => node.GetProperty(name).GetString()
        ?? throw new InvalidDataException("An authority text field is absent.");
    private static string? NullableText(JsonElement node, string name) =>
        node.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Text(node, name);
    internal static DateTimeOffset Utc(string value)
    {
        DateTimeOffset result = DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        return result.Offset == TimeSpan.Zero ? result : throw new InvalidDataException("An authority timestamp is not UTC.");
    }
    internal static void Exact(JsonElement node, params string[] names)
    {
        if (node.ValueKind != JsonValueKind.Object
            || !node.EnumerateObject().Select(property => property.Name).SequenceEqual(names, StringComparer.Ordinal))
        { throw new InvalidDataException("An authority object is not recursively closed or ordered."); }
    }
    internal static string FindRepositoryRoot(string path)
    {
        DirectoryInfo? cursor = new(Path.GetDirectoryName(Path.GetFullPath(path))!);
        while (cursor is not null && !Directory.Exists(Path.Combine(cursor.FullName, ".git"))
            && !File.Exists(Path.Combine(cursor.FullName, ".git")))
        { cursor = cursor.Parent; }
        return cursor?.FullName ?? throw new InvalidDataException("Successor authority requires an exact Git worktree.");
    }
}

internal static class M1Slice6SuccessorCampaignRunner
{
    private sealed record RetainedAttemptArtifacts(
        string CanonicalRequestPath, string CanonicalRequestSha256,
        string? RawResponsePath, string? RawResponseSha256,
        string? ResponseHeadersPath, string? ResponseHeadersSha256,
        string? NativeTracePath, string? NativeTraceSha256,
        string? CanaryEvidencePath, string? CanaryEvidenceSha256);

    private static readonly JsonSerializerOptions EvidenceJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        NewLine = "\n",
    };

    internal static async Task<int> RunAttemptAsync(string campaignPath, string campaignSha,
        string amendmentPath, string amendmentSha,
        string stagePath, string stageSha, string credentialPath, string credentialSha,
        string runtimePath, string runtimeSha, string ledgerPath, string safetyStateRoot,
        string helperPath, string helperSha, string evidencePath, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string coordinatorPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The coordinator executable path is unavailable.");
        M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(
            campaignPath, campaignSha, requireRolloverBaseline: true);
        M1Slice6HardBudgetAuthority hardBudget = M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(
            amendmentPath, amendmentSha, campaign);
        if (now >= campaign.ExpiresAtUtc || now >= hardBudget.ExpiresAtUtc
            || M1Slice6SuccessorAuthorityLoader.HashFile(credentialPath) != credentialSha
            || credentialSha != campaign.CredentialManifestSha256 || M1Slice6SuccessorAuthorityLoader.HashFile(helperPath) != helperSha)
        {
            throw new InvalidDataException("Successor campaign, credential, or helper binding is stale.");
        }
        M1Slice6SuccessorRuntimeAuthority runtime = M1Slice6SuccessorAuthorityLoader.Runtime(
            runtimePath, runtimeSha, coordinatorPath, helperPath, hardBudget, now);
        ledgerPath = Path.GetFullPath(ledgerPath);
        evidencePath = Path.GetFullPath(evidencePath);
        safetyStateRoot = Path.GetFullPath(safetyStateRoot);
        if (runtime.LedgerPath != ledgerPath || runtime.EvidencePath != evidencePath
            || runtime.SafetyStateRoot != safetyStateRoot
            || !safetyStateRoot.Equals(campaign.ProductStateRoot, StringComparison.OrdinalIgnoreCase)
            || runtime.HelperSha256 != helperSha)
        {
            throw new InvalidDataException("The runtime effect roots differ from the exact command invocation.");
        }
        if (!IsContained(runtime.OutputRoot, ledgerPath) || !IsContained(runtime.OutputRoot, evidencePath)
            || IsContained(runtime.OutputRoot, safetyStateRoot)
            || IsContained(safetyStateRoot, runtime.OutputRoot)
            || runtime.OutputRoot.Equals(safetyStateRoot, StringComparison.OrdinalIgnoreCase)
            || runtime.OutputRoot.Contains("infinium-c2a-execution", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Successor fresh evidence roots are not disjoint from retained product state or terminal execution roots.");
        }
        M1Slice6SuccessorCampaignLedgerV3 ledger = OpenActiveLedger(
            campaign, hardBudget, ledgerPath, now, requireExisting: true);
        (M1Slice6CampaignStageAuthority authority, M1Slice6SuccessorAttemptIdentity attempt) =
            M1Slice6SuccessorAuthorityLoader.Stage(stagePath, stageSha, campaign, hardBudget, runtime);
        if (authority.SafetyIdentifierProjection != campaign.SafetyIdentifierProjection)
        { throw new InvalidDataException("The stage safety projection differs from retained product-state authority."); }
        if (authority.PredecessorEventHash != ledger.Current.EventHash
            || authority.PredecessorEvidenceId != ledger.Current.EvidenceId
            || authority.PredecessorEvidenceSha256 != ledger.Current.EvidenceSha256)
        {
            throw new InvalidDataException("The successor stage does not bind the current accepted ledger predecessor.");
        }
        ProductUserSafetyIdentifierStateStore safety = new(safetyStateRoot);
        string projection = safety.GetRequiredProjection(authority.SafetyIdentifierProjection);
        if (projection != authority.SafetyIdentifierProjection)
        { throw new InvalidDataException("The exact retained safety projection is unavailable."); }
        M1Slice6CampaignProductionStageBoundary boundary = new(helperPath, helperSha,
            credentialPath, credentialSha, campaign.CredentialManifestId);
        PreflightEvidence(evidencePath, authority, attempt, runtime);
        M1Slice6CampaignIdentity campaignIdentity = new(campaign.CampaignId,
            campaign.ManifestSha256, campaign.ManifestSha256, new string('0', 40),
            campaign.CredentialManifestId, campaign.CredentialManifestSha256,
            campaign.CredentialProfileId, campaign.CredentialGenerationId,
            campaign.CredentialTargetFingerprintSha256);
        using M1Slice6CampaignSqliteProviderAccounting accounting = new(safetyStateRoot,
            credentialPath, credentialSha, now);
        M1Slice6CampaignAccountingAdmission admission = accounting.PrepareSuccessorV6(
            authority, campaignIdentity, attempt, now.AddTicks(1));
        long reservation = admission.ReservedNanoUsd;
        if (reservation <= 0 || reservation != authority.Limits.MaximumNanoUsd)
        {
            accounting.ReleaseBeforePossibleStart(admission, now.AddTicks(2));
            throw new InvalidDataException("Successor SQLite admission returned an invalid exact reservation.");
        }
        try
        {
            ledger.ReserveAttempt(attempt, reservation, now.AddTicks(2));
        }
        catch
        {
            accounting.ReleaseBeforePossibleStart(admission, now.AddTicks(3));
            throw;
        }
        bool started = false;
        bool sqlitePossibleStartLatched = false;
        M1Slice6CampaignStageBoundaryResult? result = null;
        RetainedAttemptArtifacts? retainedArtifacts = null;
        M1Slice6CampaignBoundaryFailureReceipt? failureReceipt = null;
        string failure = "";
        bool terminalSafety = false;
        M1Slice6SuccessorAccountingPersistence? persistence = null;
        try
        {
            result = await boundary.ExecuteOnceAsync(authority, admission, (startedAt, _) =>
            {
                if (started || sqlitePossibleStartLatched) { throw new InvalidDataException("A successor attempt tried to latch twice."); }
                // The append-only campaign latch is deliberately first. If the SQLite latch then
                // fails, the conservative possible start remains consumed and no helper launches.
                // This ordering cannot leave a provider start visible only in SQLite.
                ledger.LatchPossibleStart(attempt, startedAt);
                started = true;
                accounting.RecordPossibleStart(admission, startedAt.AddTicks(1));
                sqlitePossibleStartLatched = true;
                return Task.CompletedTask;
            }, cancellationToken).ConfigureAwait(false);
            // Secret-scanned response and helper evidence are durably retained before any
            // fallible SQLite settlement, replay, or semantic admission work begins.
            retainedArtifacts = RetainAttemptArtifacts(authority, result, evidencePath);
            failure = Failure(result.Response);
            terminalSafety = result.Response.ErrorCode == "security_secret_echo";
            if (terminalSafety) { failure = "safety-isolation-breach"; }
        }
        catch (M1Slice6CampaignBoundaryEvidenceException exception) when (started)
        {
            failureReceipt = exception.Receipt;
            failure = exception.TerminalSafety ? "safety-isolation-breach" : "helper-evidence-failure";
            terminalSafety = exception.TerminalSafety;
        }
        catch (M1Slice6CampaignSafetyIsolationException) when (started)
        {
            failure = "safety-isolation-breach";
            terminalSafety = true;
        }
        catch (Exception exception) when (started && exception is IOException or InvalidDataException
            or InvalidOperationException or OperationCanceledException or TimeoutException)
        {
            failure = "helper-evidence-failure";
        }
        catch (Exception exception) when (!started && exception is IOException or InvalidDataException
            or InvalidOperationException or OperationCanceledException or TimeoutException)
        {
            failure = "prestart-failure";
            terminalSafety = exception is M1Slice6CampaignSafetyIsolationException;
        }
        if (!started)
        {
            accounting.ReleaseBeforePossibleStart(admission, DateTimeOffset.UtcNow);
            byte[] prestartEvidence = Evidence(campaign, authority, attempt, runtime, admission,
                null, terminalSafety ? "safety-isolation-breach" : "prestart-failure",
                reservation, 0, 0, null, failureReceipt,
                retainedArtifacts ?? RetainAttemptArtifacts(authority, null, evidencePath, failureReceipt));
            WriteNew(evidencePath, [.. prestartEvidence, (byte)'\n']);
            string prestartSha = M1Slice6SuccessorAuthorityLoader.HashFile(evidencePath);
            if (terminalSafety)
            {
                ledger.RecordPreStartTerminalSafetyStop(attempt,
                    "successor-attempt-evidence-" + attempt.AttemptId, prestartSha,
                    reservation, DateTimeOffset.UtcNow);
                return 88;
            }
            ledger.RecordPreStartRelease(attempt, "successor-attempt-evidence-" + attempt.AttemptId,
                prestartSha, reservation, DateTimeOffset.UtcNow);
            return 82;
        }
        bool structurallyValid = result is not null && failure.Length == 0;
        retainedArtifacts ??= RetainAttemptArtifacts(authority, result, evidencePath, failureReceipt);
        try
        {
            persistence = result is null
                ? accounting.RetainSuccessorAmbiguousStart(admission, DateTimeOffset.UtcNow)
                : accounting.PersistSuccessorAttempt(admission, authority, result, structurallyValid);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException
            or IOException or KeyNotFoundException or Microsoft.Data.Sqlite.SqliteException)
        {
            persistence = accounting.ConvergeSuccessorPersistenceFailure(admission, DateTimeOffset.UtcNow);
        }
        long settled = persistence.SettledNanoUsd;
        long unresolved = persistence.UnresolvedNanoUsd;
        byte[] evidenceBytes = Evidence(campaign, authority, attempt, runtime, admission, result,
            failure, reservation, settled, unresolved, persistence, failureReceipt, retainedArtifacts);
        WriteNew(evidencePath, [.. evidenceBytes, (byte)'\n']);
        string evidenceSha = M1Slice6SuccessorAuthorityLoader.HashFile(evidencePath);
        if (terminalSafety)
        {
            ledger.RecordTerminalSafetyStop(attempt,
                "successor-attempt-evidence-" + attempt.AttemptId, evidenceSha,
                reservation, settled, unresolved, DateTimeOffset.UtcNow);
            return 88;
        }
        ledger.RecordAttemptEvidence(attempt, "successor-attempt-evidence-" + attempt.AttemptId,
            evidenceSha, failure, structurallyValid, reservation, settled, unresolved,
            DateTimeOffset.UtcNow);
        return structurallyValid ? 0 : 82;
    }

    internal static void InitializeHardBudget(string campaignPath, string campaignSha,
        string amendmentPath, string amendmentSha, string ledgerPath, string reviewPath,
        DateTimeOffset now)
    {
        if (File.Exists(Path.GetFullPath(ledgerPath)))
        { throw new InvalidOperationException("The hard-budget ledger path is not fresh."); }
        M1Slice6SuccessorCampaignAuthority campaign =
            M1Slice6SuccessorAuthorityLoader.Campaign(
                campaignPath, campaignSha, requireRolloverBaseline: true);
        if (campaign.ActiveLedgerPath is not null)
        {
            throw new InvalidOperationException(
                "Campaign v7 must be initialized only through the credential-rollover ledger entry point.");
        }
        M1Slice6HardBudgetAuthority hardBudget =
            M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(amendmentPath, amendmentSha, campaign);
        if (now >= hardBudget.ExpiresAtUtc)
        { throw new InvalidOperationException("The hard-budget amendment is expired."); }
        M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
            reviewPath, "hard-budget-amendment",
            hardBudget.AmendmentId, hardBudget.ManifestSha256, false);
        _ = OpenHardBudgetLedger(campaign, hardBudget, ledgerPath, now,
            requireExisting: false, review);
    }

    internal static M1Slice6SuccessorCampaignLedgerV3 OpenHardBudgetLedger(
        M1Slice6SuccessorCampaignAuthority campaign, M1Slice6HardBudgetAuthority hardBudget,
        string ledgerPath, DateTimeOffset now, bool requireExisting,
        M1Slice6SuccessorIndependentReview? amendmentReview = null)
    {
        if (requireExisting && !File.Exists(Path.GetFullPath(ledgerPath)))
        { throw new InvalidOperationException("The independently admitted hard-budget ledger is absent."); }
        M1Slice6SuccessorCampaignLedger predecessor = new(hardBudget.PredecessorLedgerPath,
            "infinium.m1-s6.successor-campaign/a4f66e58-6456-4c90-a6e2-20260820c2b1",
            "ff0a8a1cd499f5639c85fa7d43737643dc4b3494643d150b72d2772fc2fc18ef",
            campaign.TerminalCampaignId,
            campaign.TerminalEventHash, now);
        if (predecessor.Current.Sequence != 8
            || predecessor.Current.EventHash != hardBudget.PredecessorEventHash
            || predecessor.Current.State != M1Slice6SuccessorCampaignState.CorrectionReviewed
            || checked(predecessor.Current.PriorConservativeNanoUsd
                + predecessor.Current.SuccessorUnresolvedNanoUsd
                + predecessor.Current.SuccessorSettledNanoUsd
                + predecessor.Current.SuccessorOutstandingReservedNanoUsd)
                != hardBudget.HistoricalCommittedNanoUsd)
        { throw new InvalidDataException("The immutable v2 predecessor ledger is not exact."); }
        M1Slice6SuccessorCampaignLedgerV3 ledger = new(ledgerPath, campaign.CampaignId,
            campaign.ManifestSha256, campaign.TerminalCampaignId, campaign.TerminalEventHash,
            predecessor.Current.Sequence, predecessor.Current.EventHash, hardBudget.AmendmentId,
            hardBudget.ManifestSha256, amendmentReview?.ReviewId,
            amendmentReview?.ManifestSha256, predecessor.Current.Wp9PossibleStarts,
            predecessor.Current.Wp10PossibleStarts, predecessor.Current.Wp11PossibleStarts,
            predecessor.Current.Wp9Authoritative, predecessor.Current.Wp10Authoritative,
            predecessor.Current.Wp11Authoritative,
            predecessor.Current.SuccessorCumulativeReservedNanoUsd,
            predecessor.Current.SuccessorUnresolvedNanoUsd,
            predecessor.Current.SuccessorSettledNanoUsd, now);
        if (!ledger.HardBudgetAuthorityActive)
        { throw new InvalidDataException("The v3 ledger lacks durable hard-budget authority."); }
        return ledger;
    }

    internal static M1Slice6SuccessorCampaignLedgerV3 OpenActiveLedger(
        M1Slice6SuccessorCampaignAuthority campaign, M1Slice6HardBudgetAuthority hardBudget,
        string ledgerPath, DateTimeOffset now, bool requireExisting)
    {
        if (campaign.CampaignId
            != "infinium.m1-s6.successor-campaign-v7/3e457821-389a-4ea8-a4c0-aed9da3b5966")
        { return OpenHardBudgetLedger(campaign, hardBudget, ledgerPath, now, requireExisting); }
        ledgerPath = Path.GetFullPath(ledgerPath);
        if (campaign.ActiveLedgerPath is null || campaign.PredecessorLedgerPath is null
            || !ledgerPath.Equals(campaign.ActiveLedgerPath, StringComparison.OrdinalIgnoreCase))
        { throw new InvalidDataException("The v4 ledger path differs from the single active campaign path."); }
        if (requireExisting && !File.Exists(ledgerPath))
        { throw new InvalidOperationException("The independently admitted credential-rollover ledger is absent."); }
        M1Slice6SuccessorCampaignLedgerV3 ledger = new(ledgerPath, campaign.CampaignId,
            campaign.ManifestSha256, campaign.PredecessorLedgerPath,
            "9a1bbb048445f3eb969e16b894f8b9d8347cba5ab89c9d3c83be66e33fda5a25",
            "infinium.m1-s6.successor-credential-replacement-evidence/0dd95374-f9e1-400a-888d-ffd56f680214",
            "4778cb8e9275c34a5eab70d32635261f5ebf9eda75247960e7389e01fe448feb",
            null, null, now);
        if (!ledger.HardBudgetAuthorityActive
            || ledger.Current.Sequence < 40 || ledger.CommittedNanoUsd > 10_000_000_000)
        { throw new InvalidDataException("The v4 ledger lacks durable rollover and hard-budget authority."); }
        return ledger;
    }

    internal static void InitializeCredentialRolloverLedger(
        string campaignPath, string campaignSha, string ledgerPath, string reviewPath,
        DateTimeOffset now)
    {
        ledgerPath = Path.GetFullPath(ledgerPath);
        if (File.Exists(ledgerPath))
        { throw new InvalidOperationException("The credential-rollover ledger path is not fresh."); }
        M1Slice6SuccessorCampaignAuthority campaign =
            M1Slice6SuccessorAuthorityLoader.Campaign(
                campaignPath, campaignSha, requireRolloverBaseline: true);
        if (campaign.CampaignId
            != "infinium.m1-s6.successor-campaign-v7/3e457821-389a-4ea8-a4c0-aed9da3b5966"
            || now >= campaign.ExpiresAtUtc
            || campaign.ActiveLedgerPath is null || campaign.PredecessorLedgerPath is null
            || !ledgerPath.Equals(campaign.ActiveLedgerPath, StringComparison.OrdinalIgnoreCase))
        { throw new InvalidDataException("Credential-rollover initialization requires live campaign v7."); }
        M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
            reviewPath, "campaign-authority", campaign.CampaignId, campaign.ManifestSha256,
            false, successorV6: true);
        M1Slice6SuccessorCampaignLedgerV3 ledger = new(ledgerPath, campaign.CampaignId,
            campaign.ManifestSha256, campaign.PredecessorLedgerPath,
            "9a1bbb048445f3eb969e16b894f8b9d8347cba5ab89c9d3c83be66e33fda5a25",
            "infinium.m1-s6.successor-credential-replacement-evidence/0dd95374-f9e1-400a-888d-ffd56f680214",
            "4778cb8e9275c34a5eab70d32635261f5ebf9eda75247960e7389e01fe448feb",
            review.ReviewId, review.ManifestSha256, now);
        if (ledger.Current.State != M1Slice6SuccessorCampaignV3State.CredentialAuthorityRolledOver
            || ledger.Current.Sequence != 40 || ledger.CommittedNanoUsd != 910_560_000)
        { throw new InvalidDataException("The credential-rollover ledger genesis is not exact."); }
    }

    internal static void InitializeCampaign(string campaignPath, string campaignSha, string ledgerPath,
        string reviewPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(
            campaignPath, campaignSha, requireRolloverBaseline: true);
        if (campaign.ActiveLedgerPath is not null)
        {
            throw new InvalidOperationException(
                "Campaign v7 must be initialized only through the credential-rollover ledger entry point.");
        }
        M1Slice6SuccessorCampaignLedger ledger = new(ledgerPath, campaign.CampaignId, campaign.ManifestSha256,
            campaign.TerminalCampaignId, campaign.TerminalEventHash, now);
        if (ledger.Current.State == M1Slice6SuccessorCampaignState.Ready)
        {
            M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
                reviewPath, "campaign-authority", campaign.CampaignId, campaign.ManifestSha256, false);
            ledger.RecordIndependentReview(review.ReviewId, review.ManifestSha256, now.AddTicks(1));
            ledger.Admit(now.AddTicks(2));
        }
    }

    internal static void AcceptAttempt(string campaignPath, string campaignSha,
        string amendmentPath, string amendmentSha, string ledgerPath,
        string evidencePath, string reviewPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(
            campaignPath, campaignSha, requireRolloverBaseline: true);
        M1Slice6HardBudgetAuthority hardBudget = M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(
            amendmentPath, amendmentSha, campaign);
        M1Slice6SuccessorCampaignLedgerV3 ledger = OpenActiveLedger(
            campaign, hardBudget, ledgerPath, now, requireExisting: true);
        M1Slice6SuccessorAttemptIdentity attempt = ledger.Current.Attempt
            ?? throw new InvalidOperationException("The successor ledger has no attempt evidence handoff.");
        bool recovery = ledger.Current.State == M1Slice6SuccessorCampaignV3State.AuthoritativeRecoveryHandoff;
        string evidenceId = recovery ? "successor-authoritative-recovery-" + attempt.AttemptId
            : "successor-attempt-evidence-" + attempt.AttemptId;
        string evidenceSha = recovery
            ? ValidateRecoveryEvidence(campaignPath, campaign, ledger, attempt, evidencePath)
            : ValidateAttemptEvidence(campaignPath, campaign, ledger, attempt, evidencePath);
        bool developmentContinuation = campaign.CredentialAccessAuthorityId
            == "infinium.m1-s6.development-continuation/20260821";
        M1Slice6SuccessorIndependentReview? review = developmentContinuation ? null
            : M1Slice6SuccessorAuthorityLoader.Review(
                reviewPath, "attempt-evidence", evidenceId, evidenceSha, false, successorV6: true);
        if (developmentContinuation
            && (Path.GetFullPath(reviewPath) != Path.GetFullPath(Path.Combine(
                    M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(campaignPath), "docs", "plans",
                    "milestones", "m1", "slices", "s6", "development-continuation.md"))
                || M1Slice6SuccessorAuthorityLoader.HashFile(reviewPath)
                    != campaign.CredentialAccessAuthoritySha256))
        { throw new InvalidDataException("Development attempt acceptance requires the exact owner continuation."); }
        ledger.AcceptAttemptEvidence(attempt, evidenceId, evidenceSha,
            developmentContinuation ? campaign.CredentialAccessAuthorityId : review!.ReviewId,
            developmentContinuation ? campaign.CredentialAccessAuthoritySha256 : review!.ManifestSha256,
            now.AddTicks(1));
    }

    internal static void AcceptAttemptSupplement(string campaignPath, string campaignSha,
        string ledgerPath, string originalEvidencePath, string normalizedEvidencePath,
        string supplementPath, string reviewPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign =
            M1Slice6SuccessorAuthorityLoader.HistoricalCampaign(campaignPath, campaignSha);
        M1Slice6SuccessorCampaignLedger ledger = new(ledgerPath, campaign.CampaignId,
            campaign.ManifestSha256, campaign.TerminalCampaignId, campaign.TerminalEventHash, now);
        M1Slice6SuccessorAttemptIdentity attempt = ledger.Current.Attempt
            ?? throw new InvalidOperationException("The successor ledger has no attempt evidence handoff.");
        if (ledger.Current.State != M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff)
        { throw new InvalidOperationException("A supplement requires the exact pending attempt evidence handoff."); }
        byte[] originalBytes = File.ReadAllBytes(Path.GetFullPath(originalEvidencePath));
        string originalDirectory = Path.GetDirectoryName(Path.GetFullPath(originalEvidencePath))!;
        if (!string.Equals(originalDirectory, Path.GetDirectoryName(Path.GetFullPath(normalizedEvidencePath)),
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(originalDirectory, Path.GetDirectoryName(Path.GetFullPath(supplementPath)),
                StringComparison.OrdinalIgnoreCase))
        { throw new InvalidDataException("The immutable evidence, normalized view, and supplement must share one retained directory."); }
        string originalSha = M1Slice6SuccessorAuthorityLoader.Hash(originalBytes);
        if (originalSha != ledger.Current.EvidenceSha256)
        { throw new InvalidDataException("The immutable original evidence differs from the ledger handoff."); }

        byte[] normalizedBytes = File.ReadAllBytes(Path.GetFullPath(normalizedEvidencePath));
        string normalizedSha = M1Slice6SuccessorAuthorityLoader.Hash(normalizedBytes);
        string repository = RepositoryRoot(campaignPath);
        _ = ValidateAttemptEvidence(campaignPath, campaign, ledger, attempt,
            normalizedEvidencePath, historicalNormalizedV1: true);
        string[] normalizedFields = ["response_id", "usage_entry_id", "replay_edge_id", "semantic_failure_code"];
        JsonNode expectedNormalized = NormalizeKnownV1AbsentValues(originalBytes);
        JsonNode actualNormalized = JsonNode.Parse(normalizedBytes)
            ?? throw new InvalidDataException("The normalized attempt evidence is empty.");
        if (!JsonNode.DeepEquals(expectedNormalized, actualNormalized))
        { throw new InvalidDataException("The normalized evidence changes facts beyond the exact absent-value repair."); }

        byte[] supplementBytes = File.ReadAllBytes(Path.GetFullPath(supplementPath));
        string supplementSha = M1Slice6SuccessorAuthorityLoader.Hash(supplementBytes);
        ActiveRepositoryJsonSchemaValidator.Validate(supplementBytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-attempt-evidence-supplement.v1.schema.json")),
            M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSupplementSchema);
        using JsonDocument supplementDocument = JsonDocument.Parse(supplementBytes);
        JsonElement supplement = supplementDocument.RootElement;
        M1Slice6SuccessorAuthorityLoader.Exact(supplement, "schema", "supplement_id",
            "campaign_id", "attempt_id", "original_evidence_id", "original_evidence_sha256",
            "normalized_evidence_path", "normalized_evidence_sha256", "normalized_fields",
            "accepted_claims", "limitations",
            "provider_effect_used", "created_at_utc");
        string supplementId = M1Slice6SuccessorAuthorityLoader.Text(supplement, "supplement_id");
        string[] fields = supplement.GetProperty("normalized_fields").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
        JsonElement acceptedClaims = supplement.GetProperty("accepted_claims");
        M1Slice6SuccessorAuthorityLoader.Exact(acceptedClaims, "possible_start_and_accounting",
            "actual_adapter_send_count", "exact_containment_predicate", "credential_read_free_trace");
        string[] limitations = supplement.GetProperty("limitations").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
        if (M1Slice6SuccessorAuthorityLoader.Text(supplement, "campaign_id") != campaign.CampaignId
            || M1Slice6SuccessorAuthorityLoader.Text(supplement, "attempt_id") != attempt.AttemptId
            || M1Slice6SuccessorAuthorityLoader.Text(supplement, "original_evidence_id") != ledger.Current.EvidenceId
            || M1Slice6SuccessorAuthorityLoader.Text(supplement, "original_evidence_sha256") != originalSha
            || M1Slice6SuccessorAuthorityLoader.Text(supplement, "normalized_evidence_path")
                != Path.GetFileName(normalizedEvidencePath)
            || M1Slice6SuccessorAuthorityLoader.Text(supplement, "normalized_evidence_sha256") != normalizedSha
            || !fields.SequenceEqual(normalizedFields, StringComparer.Ordinal)
            || M1Slice6SuccessorAuthorityLoader.Text(acceptedClaims, "possible_start_and_accounting") != "accepted"
            || M1Slice6SuccessorAuthorityLoader.Text(acceptedClaims, "actual_adapter_send_count") != "unverified"
            || M1Slice6SuccessorAuthorityLoader.Text(acceptedClaims, "exact_containment_predicate") != "unavailable"
            || M1Slice6SuccessorAuthorityLoader.Text(acceptedClaims, "credential_read_free_trace")
                != "not-independently-retained"
            || !limitations.SequenceEqual(M1Slice6SuccessorAuthorityLoader.SupplementLimitations,
                StringComparer.Ordinal)
            || supplement.GetProperty("provider_effect_used").GetBoolean())
        { throw new InvalidDataException("The supplement does not bind the exact immutable evidence repair."); }
        _ = M1Slice6SuccessorAuthorityLoader.Utc(
            M1Slice6SuccessorAuthorityLoader.Text(supplement, "created_at_utc"));
        M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
            reviewPath, "attempt-evidence-supplement", supplementId, supplementSha, false);
        ledger.AcceptAttemptEvidence(attempt, ledger.Current.EvidenceId, originalSha,
            review.ReviewId, review.ManifestSha256, now.AddTicks(1));
    }

    internal static void RecoverAuthoritativeAttempt(string campaignPath, string campaignSha,
        string amendmentPath, string amendmentSha,
        string stagePath, string stageSha, string credentialPath, string credentialSha,
        string runtimePath, string runtimeSha, string ledgerPath, string originalEvidencePath,
        string recoveryPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign =
            M1Slice6SuccessorAuthorityLoader.Campaign(
                campaignPath, campaignSha, requireRolloverBaseline: true);
        M1Slice6HardBudgetAuthority hardBudget = M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(
            amendmentPath, amendmentSha, campaign);
        M1Slice6SuccessorRuntimeAuthority runtime =
            M1Slice6SuccessorAuthorityLoader.RuntimeForRecovery(
                runtimePath, runtimeSha, hardBudget, now);
        recoveryPath = Path.GetFullPath(recoveryPath);
        (ledgerPath, originalEvidencePath, _) = ValidateRecoveryRoots(runtime, campaign,
            credentialPath, credentialSha, ledgerPath, originalEvidencePath, recoveryPath);
        (M1Slice6CampaignStageAuthority authority, M1Slice6SuccessorAttemptIdentity attempt) =
            M1Slice6SuccessorAuthorityLoader.Stage(stagePath, stageSha, campaign, hardBudget, runtime);
        M1Slice6SuccessorCampaignLedgerV3 ledger = OpenActiveLedger(
            campaign, hardBudget, ledgerPath, now, requireExisting: true);
        if (ledger.Current.State != M1Slice6SuccessorCampaignV3State.AttemptEvidenceHandoff
            || ledger.Current.Attempt != attempt || ledger.Current.FailureDisposition.Length != 0)
        { throw new InvalidOperationException("Offline recovery requires one exact first-valid semantic handoff."); }
        byte[] originalBytes = File.ReadAllBytes(Path.GetFullPath(originalEvidencePath));
        string originalSha = M1Slice6SuccessorAuthorityLoader.Hash(originalBytes);
        if (originalSha != ledger.Current.EvidenceSha256)
        { throw new InvalidDataException("Offline recovery original evidence differs from the ledger handoff."); }
        using JsonDocument originalDocument = JsonDocument.Parse(originalBytes);
        JsonElement original = originalDocument.RootElement;
        JsonElement accountingNode = original.GetProperty("accounting");
        JsonElement artifacts = original.GetProperty("retained_artifacts");
        string originalDirectory = Path.GetDirectoryName(Path.GetFullPath(originalEvidencePath))!;
        string rawPath = Path.Combine(originalDirectory,
            M1Slice6SuccessorAuthorityLoader.Text(artifacts, "raw_response_path"));
        string headersPath = Path.Combine(originalDirectory,
            M1Slice6SuccessorAuthorityLoader.Text(artifacts, "response_headers_path"));
        string rawSha = M1Slice6SuccessorAuthorityLoader.HashFile(rawPath);
        string headersSha = M1Slice6SuccessorAuthorityLoader.HashFile(headersPath);
        if (rawSha != M1Slice6SuccessorAuthorityLoader.Text(artifacts, "raw_response_sha256")
            || headersSha != M1Slice6SuccessorAuthorityLoader.Text(artifacts, "response_headers_sha256"))
        { throw new InvalidDataException("Offline recovery retained response sidecars are stale."); }
        using M1Slice6CampaignSqliteProviderAccounting accounting = new(campaign.ProductStateRoot,
            credentialPath, credentialSha, now);
        string recoverableResponseId = accountingNode.GetProperty("response_id").ValueKind == JsonValueKind.String
            ? M1Slice6SuccessorAuthorityLoader.Text(accountingNode, "response_id")
            : "m1s6-successor-" + attempt.AttemptId + "-response";
        M1Slice6SuccessorAccountingPersistence recovered = accounting.RecoverSuccessorSemantic(
            authority, M1Slice6SuccessorAuthorityLoader.Text(accountingNode, "operation_id"),
            M1Slice6SuccessorAuthorityLoader.Text(accountingNode, "authorization_id"),
            attempt.AttemptId, attempt.RequestId, attempt.ReservationId, attempt.DispatchFenceId,
            recoverableResponseId, campaign.CampaignId,
            File.ReadAllBytes(rawPath), File.ReadAllBytes(headersPath), now.AddTicks(1));
        M1Slice6CampaignSemanticAdmissionReceipt semantic = recovered.Semantic
            ?? throw new InvalidDataException("Offline retained-response recovery did not complete semantic admission.");
        string recoveryId = "successor-authoritative-recovery-" + attempt.AttemptId;
        byte[] recoveryBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = M1Slice6SuccessorAuthorityLoader.RecoveryEvidenceSchema,
            recovery_id = recoveryId,
            campaign_id = campaign.CampaignId,
            campaign_manifest_sha256 = campaign.ManifestSha256,
            stage = attempt.Stage.ToString(),
            attempt_id = attempt.AttemptId,
            original_evidence_id = ledger.Current.EvidenceId,
            original_evidence_sha256 = ledger.Current.EvidenceSha256,
            provider_effect_used = false,
            raw_response_sha256 = rawSha,
            response_headers_sha256 = headersSha,
            accounting = new
            {
                authorization_id = M1Slice6SuccessorAuthorityLoader.Text(accountingNode, "authorization_id"),
                operation_id = M1Slice6SuccessorAuthorityLoader.Text(accountingNode, "operation_id"),
                attempt_id = attempt.AttemptId,
                request_id = attempt.RequestId,
                dispatch_fence_id = attempt.DispatchFenceId,
                response_id = recovered.ResponseId,
                usage_entry_id = recovered.UsageEntryId,
                settlement_id = recovered.SettlementId,
                replay_edge_id = recovered.ReplayEdgeId,
            },
            semantic = new
            {
                validation_id = semantic.ValidationId,
                disposition = semantic.Disposition,
                result_sha256 = semantic.ResultSha256,
                proposal_count = semantic.ProposalCount,
                admission_count = semantic.AdmissionCount,
                provenance = semantic.Provenance,
            },
            recovered_at_utc = now.AddTicks(2),
        }, EvidenceJson);
        WriteNew(Path.GetFullPath(recoveryPath), [.. recoveryBytes, (byte)'\n']);
        string recoverySha = ValidateRecoverySchema(campaignPath, File.ReadAllBytes(recoveryPath));
        ledger.RecordAuthoritativeRecoveryEvidence(attempt, recoveryId, recoverySha, now.AddTicks(3));
    }

    internal static void RecoverStartedAmbiguousAttempt(string campaignPath, string campaignSha,
        string amendmentPath, string amendmentSha,
        string stagePath, string stageSha, string credentialPath, string credentialSha,
        string runtimePath, string runtimeSha, string ledgerPath, string evidencePath,
        DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign =
            M1Slice6SuccessorAuthorityLoader.Campaign(
                campaignPath, campaignSha, requireRolloverBaseline: true);
        M1Slice6HardBudgetAuthority hardBudget = M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(
            amendmentPath, amendmentSha, campaign);
        M1Slice6SuccessorRuntimeAuthority runtime =
            M1Slice6SuccessorAuthorityLoader.RuntimeForRecovery(
                runtimePath, runtimeSha, hardBudget, now);
        (ledgerPath, evidencePath, _) = ValidateRecoveryRoots(runtime, campaign,
            credentialPath, credentialSha, ledgerPath, evidencePath);
        (M1Slice6CampaignStageAuthority authority, M1Slice6SuccessorAttemptIdentity attempt) =
            M1Slice6SuccessorAuthorityLoader.Stage(stagePath, stageSha, campaign, hardBudget, runtime);
        M1Slice6SuccessorCampaignLedgerV3 ledger = OpenActiveLedger(
            campaign, hardBudget, ledgerPath, now, requireExisting: true);
        if (ledger.Current.State == M1Slice6SuccessorCampaignV3State.AttemptEvidenceHandoff
            && ledger.Current.Attempt == attempt && File.Exists(Path.GetFullPath(evidencePath)))
        {
            _ = ValidateAttemptEvidence(campaignPath, campaign, ledger, attempt, evidencePath);
            return;
        }
        if (ledger.Current.State != M1Slice6SuccessorCampaignV3State.AttemptStarted
            || ledger.Current.Attempt != attempt || attempt.Stage != M1Slice6CampaignStage.Qualification)
        {
            throw new InvalidOperationException(
                "Started-failure recovery requires the exact pending WP9 possible start.");
        }
        string directory = Path.GetDirectoryName(evidencePath)
            ?? throw new InvalidDataException("Started-failure evidence path has no directory.");
        string stem = Path.GetFileNameWithoutExtension(evidencePath);
        string requestName = stem + ".canonical-request.json";
        string headersName = stem + ".response-headers.json";
        string traceName = stem + ".native-trace.json";
        string canaryName = stem + ".canaries.json";
        string preflightName = stem + ".preflight.json";
        string rawPath = Path.Combine(directory, stem + ".raw-response.bin");
        if (File.Exists(rawPath))
        { throw new InvalidDataException("Ambiguous started-failure recovery cannot discard response bytes."); }
        byte[] request = ReadExactRetained(Path.Combine(directory, requestName));
        byte[] headers = ReadExactRetained(Path.Combine(directory, headersName));
        byte[] trace = ReadExactRetained(Path.Combine(directory, traceName));
        byte[] canaries = ReadExactRetained(Path.Combine(directory, canaryName));
        byte[] preflight = ReadExactRetained(Path.Combine(directory, preflightName));
        if (M1Slice6SuccessorAuthorityLoader.Hash(request) != authority.CanonicalRequestSha256
            || !request.AsSpan().SequenceEqual(authority.CanonicalRequest))
        { throw new InvalidDataException("Started-failure recovery request bytes are stale."); }
        ValidateRecoveredPreflight(preflight, attempt, authority, runtime);
        M1Slice6CampaignBoundaryFailureReceipt failureReceipt = RecoveredAmbiguousReceipt(
            headers, trace, canaries, attempt, campaign.CredentialTargetFingerprintSha256);
        RetainedAttemptArtifacts artifacts = new(requestName, authority.CanonicalRequestSha256,
            null, null, headersName, M1Slice6SuccessorAuthorityLoader.Hash(headers),
            traceName, M1Slice6SuccessorAuthorityLoader.Hash(trace),
            canaryName, M1Slice6SuccessorAuthorityLoader.Hash(canaries));
        string prefix = "m1s6-successor-v6-" + attempt.AttemptId;
        string operationId = prefix + "-transport-operation";
        string authorizationId = prefix + "-transport-authorization";
        long reserved = ledger.Current.SuccessorOutstandingReservedNanoUsd;
        if (reserved <= 0 || reserved != authority.Limits.MaximumNanoUsd)
        { throw new InvalidDataException("Started-failure recovery reservation is not exact."); }
        M1Slice6CampaignAccountingAdmission admission = new(authorizationId, operationId,
            attempt.AttemptId, attempt.RequestId, attempt.ReservationId, attempt.DispatchFenceId,
            0, now, now, "", "", ReservedNanoUsd: reserved);
        using M1Slice6CampaignSqliteProviderAccounting accounting = new(campaign.ProductStateRoot,
            credentialPath, credentialSha, now);
        M1Slice6SuccessorAccountingPersistence persistence =
            accounting.RecoverSuccessorV6AmbiguousStart(operationId, authorizationId,
                attempt.AttemptId, attempt.RequestId, attempt.ReservationId,
                attempt.DispatchFenceId, now.AddTicks(1));
        byte[] evidenceBytes = Evidence(campaign, authority, attempt, runtime, admission, null,
            "transport-ambiguous", reserved, 0, persistence.UnresolvedNanoUsd, persistence,
            failureReceipt, artifacts);
        string fullEvidencePath = Path.GetFullPath(evidencePath);
        byte[] exactEvidence = [.. evidenceBytes, (byte)'\n'];
        string repository = RepositoryRoot(campaignPath);
        ActiveRepositoryJsonSchemaValidator.Validate(exactEvidence,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-successor-attempt-evidence.v3.schema.json")),
            M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSchemaV3);
        if (File.Exists(fullEvidencePath))
        {
            if (!File.ReadAllBytes(fullEvidencePath).AsSpan().SequenceEqual(exactEvidence))
            { throw new InvalidDataException("Started-failure recovery evidence path is stale."); }
        }
        else
        {
            WriteNew(fullEvidencePath, exactEvidence);
        }
        string evidenceSha = M1Slice6SuccessorAuthorityLoader.HashFile(fullEvidencePath);
        ledger.RecordAttemptEvidence(attempt, "successor-attempt-evidence-" + attempt.AttemptId,
            evidenceSha, "transport-ambiguous", structurallyValid: false, reserved, 0,
            persistence.UnresolvedNanoUsd, now.AddTicks(2));
        _ = ValidateAttemptEvidence(campaignPath, campaign, ledger, attempt, fullEvidencePath);
    }

    internal static void AcceptCorrectionReview(string campaignPath, string campaignSha,
        string amendmentPath, string amendmentSha, string ledgerPath, string reviewPath,
        DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(
            campaignPath, campaignSha, requireRolloverBaseline: true);
        M1Slice6HardBudgetAuthority hardBudget = M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(
            amendmentPath, amendmentSha, campaign);
        M1Slice6SuccessorCampaignLedgerV3 ledger = OpenActiveLedger(
            campaign, hardBudget, ledgerPath, now, requireExisting: true);
        M1Slice6SuccessorAttemptIdentity attempt = ledger.Current.Attempt
            ?? throw new InvalidOperationException("The accepted failure has no exact attempt identity.");
        M1Slice6SuccessorCampaignLedgerV3Entry failure = ledger.Entries.Last(entry =>
            entry.Attempt?.AttemptId == attempt.AttemptId
            && entry.State == M1Slice6SuccessorCampaignV3State.AttemptEvidenceHandoff);
        bool developmentContinuation = campaign.CredentialAccessAuthorityId
            == "infinium.m1-s6.development-continuation/20260821";
        if (developmentContinuation)
        {
            string expected = Path.Combine(M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(campaignPath),
                "docs", "plans", "milestones", "m1", "slices", "s6", "development-continuation.md");
            if (Path.GetFullPath(reviewPath) != Path.GetFullPath(expected)
                || M1Slice6SuccessorAuthorityLoader.HashFile(reviewPath)
                    != campaign.CredentialAccessAuthoritySha256)
            { throw new InvalidDataException("Development correction acceptance requires the exact owner continuation."); }
            ledger.RecordOfflineCorrectionReview(campaign.CredentialAccessAuthorityId,
                campaign.CredentialAccessAuthoritySha256, "credential-input-truncation-fixed", now.AddTicks(1));
            return;
        }
        M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
            reviewPath, "offline-correction", failure.EvidenceId, failure.EvidenceSha256, null,
            failure.EvidenceId, failure.EvidenceSha256, successorV6: true);
        if (review.DefectId is null)
        { throw new InvalidOperationException("The accepted failure review has no defect identity."); }
        ledger.RecordOfflineCorrectionReview(review.ReviewId, review.ManifestSha256,
            review.DefectId, now.AddTicks(1));
    }

    internal static void CompleteComposedEvidence(string campaignPath, string campaignSha,
        string amendmentPath, string amendmentSha, string ledgerPath,
        string composedPath, string reviewPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(
            campaignPath, campaignSha, requireRolloverBaseline: true);
        M1Slice6HardBudgetAuthority hardBudget = M1Slice6SuccessorAuthorityLoader.HardBudgetAmendment(
            amendmentPath, amendmentSha, campaign);
        M1Slice6SuccessorCampaignLedgerV3 ledger = OpenActiveLedger(
            campaign, hardBudget, ledgerPath, now, requireExisting: true);
        if (ledger.Current.State is not (M1Slice6SuccessorCampaignV3State.StageAccepted
                or M1Slice6SuccessorCampaignV3State.ComposedEvidenceHandoff)
            || !ledger.Current.Wp9Authoritative || !ledger.Current.Wp10Authoritative
            || !ledger.Current.Wp11Authoritative || ledger.Current.SuccessorOutstandingReservedNanoUsd != 0)
        { throw new InvalidOperationException("C3 requires three independently accepted first-valid stage responses."); }
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(composedPath));
        string composedSha = M1Slice6SuccessorAuthorityLoader.Hash(bytes);
        string repository = Path.GetDirectoryName(Path.GetFullPath(campaignPath))!;
        while (!File.Exists(Path.Combine(repository, "Infinium.sln")))
        { repository = Directory.GetParent(repository)?.FullName ?? throw new InvalidDataException("C3 has no contract root."); }
        string credentialPath = ActiveCredentialManifestPath(repository, campaignPath, campaign);
        using M1Slice6CampaignSqliteProviderAccounting c3Accounting = new(campaign.ProductStateRoot,
            credentialPath, campaign.CredentialManifestSha256, now);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-composed-evidence.v2.schema.json")),
            "infinium.m1-s6.successor-composed-evidence/v2");
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        string evidenceId = M1Slice6SuccessorAuthorityLoader.Text(root, "evidence_id");
        if (ledger.Current.State == M1Slice6SuccessorCampaignV3State.ComposedEvidenceHandoff)
        {
            if (ledger.Current.EvidenceId != evidenceId || ledger.Current.EvidenceSha256 != composedSha)
            { throw new InvalidDataException("C3 resumed with different composed evidence bytes."); }
            M1Slice6SuccessorIndependentReview resumedReview = M1Slice6SuccessorAuthorityLoader.Review(
                reviewPath, "composed-closeout", evidenceId, composedSha, false, successorV6: true);
            ledger.Complete(evidenceId, composedSha, resumedReview.ReviewId,
                resumedReview.ManifestSha256, now.AddTicks(1));
            return;
        }
        if (M1Slice6SuccessorAuthorityLoader.Text(root, "campaign_id") != campaign.CampaignId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "campaign_manifest_sha256") != campaign.ManifestSha256
            || M1Slice6SuccessorAuthorityLoader.Text(root, "terminal_campaign_id") != campaign.TerminalCampaignId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "terminal_event_hash") != campaign.TerminalEventHash
            || M1Slice6SuccessorAuthorityLoader.Text(root, "ledger_precompletion_event_hash") != ledger.Current.EventHash)
        { throw new InvalidDataException("C3 campaign or precompletion ledger binding is stale."); }
        string composedDirectory = Path.GetDirectoryName(Path.GetFullPath(composedPath))!;
        JsonElement predecessorLedger = root.GetProperty("predecessor_ledger");
        JsonElement successorLedger = root.GetProperty("successor_ledger");
        RequireC3FileBinding(composedDirectory, predecessorLedger,
            hardBudget.PredecessorLedgerPath, hardBudget.PredecessorLedgerSha256, 8,
            hardBudget.PredecessorEventHash);
        RequireC3FileBinding(composedDirectory, successorLedger, Path.GetFullPath(ledgerPath),
            M1Slice6SuccessorAuthorityLoader.HashFile(ledgerPath), ledger.Current.Sequence,
            ledger.Current.EventHash);
        (string Kind, string Relative, string Sha)[] inherited =
        [
            ("attempt-2-original", "wp9-attempt-2/attempt-evidence-correction-3.v1.json", "c642571f81670346e56e61902306df982a235d591bd0da50ccb2082e6d20690e"),
            ("attempt-2-supplement", "wp9-attempt-2/attempt-evidence-correction-3.supplement.v1.json", "0a3c1786c86b0516f536f624f406033e1b956feba7592d76b1d0afe4b7a2aca2"),
            ("attempt-2-normalized-view", "wp9-attempt-2/attempt-evidence-correction-3.normalized.v1.json", "687041eb6432ec58d3b77c5450500ccd4d675cd58194607763c4e177ebcf917b"),
            ("attempt-2-supplement-review", "wp9-attempt-2/attempt-evidence-correction-3.supplement-review.v2.json", "4d39c51dbc73edd362e8ddc33f5da7c657a2f572161da2f352c93f0fe3138b48"),
            ("attempt-2-offline-correction-review", "wp9-attempt-2/attempt-evidence-correction-3.offline-correction-review.v1.json", "c30cfebd841978fca83fd379413067f52f35f186d65ae1e6b315b9ecbde435e0"),
        ];
        JsonElement[] inheritedNodes = root.GetProperty("inherited_evidence").EnumerateArray().ToArray();
        if (inheritedNodes.Length != inherited.Length)
        { throw new InvalidDataException("C3 inherited evidence chronology is incomplete."); }
        for (int index = 0; index < inherited.Length; index++)
        {
            JsonElement item = inheritedNodes[index];
            string exactPath = Path.GetFullPath(Path.Combine(composedDirectory,
                inherited[index].Relative.Replace('/', Path.DirectorySeparatorChar)));
            if (M1Slice6SuccessorAuthorityLoader.Text(item, "kind") != inherited[index].Kind
                || M1Slice6SuccessorAuthorityLoader.Text(item, "path") != inherited[index].Relative
                || M1Slice6SuccessorAuthorityLoader.Text(item, "sha256") != inherited[index].Sha
                || M1Slice6SuccessorAuthorityLoader.HashFile(exactPath) != inherited[index].Sha)
            { throw new InvalidDataException("C3 inherited attempt-2 evidence changed or was reordered."); }
        }
        M1Slice6SuccessorCampaignLedgerV3Entry[] handoffs = ledger.Entries
            .Where(entry => entry.State == M1Slice6SuccessorCampaignV3State.AttemptEvidenceHandoff).ToArray();
        JsonElement[] attempts = root.GetProperty("attempts").EnumerateArray().ToArray();
        if (attempts.Length != handoffs.Length)
        { throw new InvalidDataException("C3 omitted or invented successor attempts."); }
        for (int index = 0; index < handoffs.Length; index++)
        {
            M1Slice6SuccessorCampaignLedgerV3Entry handoff = handoffs[index];
            int handoffIndex = ledger.Entries.ToList().IndexOf(handoff);
            M1Slice6SuccessorCampaignLedgerV3Entry? reviewEntry = ledger.Entries.Skip(handoffIndex + 1)
                .FirstOrDefault(entry => entry.Attempt?.AttemptId == handoff.Attempt?.AttemptId
                    && entry.State is (M1Slice6SuccessorCampaignV3State.AttemptFailureAccepted
                        or M1Slice6SuccessorCampaignV3State.StageAccepted));
            if (handoffIndex < 0 || reviewEntry is null)
            { throw new InvalidDataException("C3 attempt handoff has no independent review successor."); }
            JsonElement attempt = attempts[index];
            string? failure = attempt.GetProperty("failure_disposition").ValueKind == JsonValueKind.Null
                ? null : M1Slice6SuccessorAuthorityLoader.Text(attempt, "failure_disposition");
            if (handoff.Attempt is null
                || M1Slice6SuccessorAuthorityLoader.Text(attempt, "attempt_id") != handoff.Attempt.AttemptId
                || M1Slice6SuccessorAuthorityLoader.Text(attempt, "stage") != handoff.Attempt.Stage.ToString()
                || attempt.GetProperty("attempt_ordinal").GetInt32() != handoff.Attempt.AttemptOrdinal
                || M1Slice6SuccessorAuthorityLoader.Text(attempt, "evidence_id") != handoff.EvidenceId
                || M1Slice6SuccessorAuthorityLoader.Text(attempt, "evidence_sha256") != handoff.EvidenceSha256
                || M1Slice6SuccessorAuthorityLoader.Text(attempt, "review_id") != reviewEntry.EvidenceId
                || M1Slice6SuccessorAuthorityLoader.Text(attempt, "review_sha256") != reviewEntry.EvidenceSha256
                || failure != (handoff.FailureDisposition.Length == 0 ? null : handoff.FailureDisposition)
                || attempt.GetProperty("authoritative").GetBoolean() != (handoff.FailureDisposition.Length == 0))
            { throw new InvalidDataException("C3 attempt chronology differs from the append-only ledger."); }
            string relative = M1Slice6SuccessorAuthorityLoader.Text(attempt, "evidence_path");
            string evidence = Path.GetFullPath(Path.Combine(composedDirectory, relative));
            if (Path.IsPathFullyQualified(relative)
                || !evidence.StartsWith(composedDirectory.TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || M1Slice6SuccessorAuthorityLoader.HashFile(evidence) != handoff.EvidenceSha256)
            { throw new InvalidDataException("C3 attempt evidence escaped or changed."); }
        }
        JsonElement[] authorities = root.GetProperty("authoritative_stages").EnumerateArray().ToArray();
        M1Slice6SuccessorCampaignLedgerV3Entry[] authoritative = ledger.Entries
            .Where(entry => entry.State == M1Slice6SuccessorCampaignV3State.StageAccepted)
            .Select(entry => ledger.Entries[ledger.Entries.ToList().IndexOf(entry) - 1]).ToArray();
        if (authorities.Length != 3 || authoritative.Length != 3)
        { throw new InvalidDataException("C3 requires exactly one authoritative response per stage."); }
        for (int index = 0; index < 3; index++)
        {
            JsonElement item = authorities[index];
            M1Slice6SuccessorCampaignLedgerV3Entry exact = authoritative[index];
            if (exact.Attempt is null || M1Slice6SuccessorAuthorityLoader.Text(item, "stage") != exact.Attempt.Stage.ToString()
                || M1Slice6SuccessorAuthorityLoader.Text(item, "attempt_id") != exact.Attempt.AttemptId
                || M1Slice6SuccessorAuthorityLoader.Text(item, "evidence_id") != exact.EvidenceId
                || M1Slice6SuccessorAuthorityLoader.Text(item, "evidence_sha256") != exact.EvidenceSha256)
            { throw new InvalidDataException("C3 authoritative stage selection is not the first-valid ledger latch."); }
            string authorityRelative = M1Slice6SuccessorAuthorityLoader.Text(item, "evidence_path");
            string authorityEvidencePath = Path.GetFullPath(Path.Combine(composedDirectory, authorityRelative));
            if (Path.IsPathFullyQualified(authorityRelative)
                || !authorityEvidencePath.StartsWith(composedDirectory.TrimEnd(Path.DirectorySeparatorChar)
                    + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || M1Slice6SuccessorAuthorityLoader.HashFile(authorityEvidencePath) != exact.EvidenceSha256)
            { throw new InvalidDataException("C3 authoritative evidence escaped or changed."); }
            JsonElement matchingAttempt = attempts.Single(value =>
                M1Slice6SuccessorAuthorityLoader.Text(value, "attempt_id") == exact.Attempt.AttemptId);
            string evidencePath = Path.GetFullPath(Path.Combine(composedDirectory,
                M1Slice6SuccessorAuthorityLoader.Text(matchingAttempt, "evidence_path")));
            using JsonDocument attemptEvidence = JsonDocument.Parse(File.ReadAllBytes(evidencePath));
            JsonElement attemptRoot = attemptEvidence.RootElement;
            JsonElement attemptAccounting = attemptRoot.GetProperty("accounting");
            JsonElement artifact = attemptRoot.GetProperty("retained_artifacts");
            string rawSha = M1Slice6SuccessorAuthorityLoader.Text(artifact, "raw_response_sha256");
            JsonElement semantic = attemptAccounting;
            M1Slice6CampaignSemanticProvenance provenance = M1Slice6CampaignSemanticProvenance.Empty;
            if (exact.State == M1Slice6SuccessorCampaignV3State.AuthoritativeRecoveryHandoff)
            {
                using JsonDocument recovery = JsonDocument.Parse(File.ReadAllBytes(authorityEvidencePath));
                semantic = recovery.RootElement.GetProperty("semantic");
                attemptAccounting = recovery.RootElement.GetProperty("accounting");
                rawSha = M1Slice6SuccessorAuthorityLoader.Text(recovery.RootElement, "raw_response_sha256");
            }
            if (exact.Attempt.Stage != M1Slice6CampaignStage.Qualification)
            {
                JsonElement p = semantic.GetProperty("provenance");
                provenance = new(M1Slice6SuccessorAuthorityLoader.Text(p, "source_acquisition_id"),
                    M1Slice6SuccessorAuthorityLoader.Text(p, "source_admission_id"),
                    M1Slice6SuccessorAuthorityLoader.Text(p, "admitted_artifact_id"),
                    M1Slice6SuccessorAuthorityLoader.Text(p, "source_application_link_id"),
                    M1Slice6SuccessorAuthorityLoader.Text(p, "evidence_application_link_id"),
                    M1Slice6SuccessorAuthorityLoader.Text(p, "candidate_id"),
                    M1Slice6SuccessorAuthorityLoader.Text(p, "hypothesis_id"));
            }
            c3Accounting.ValidateSuccessorC3Attempt(exact.Attempt.Stage,
                M1Slice6SuccessorAuthorityLoader.Text(attemptAccounting, "operation_id"),
                M1Slice6SuccessorAuthorityLoader.Text(attemptAccounting, "authorization_id"),
                M1Slice6SuccessorAuthorityLoader.Text(attemptAccounting, "response_id"),
                M1Slice6SuccessorAuthorityLoader.Text(attemptAccounting, "usage_entry_id"),
                M1Slice6SuccessorAuthorityLoader.Text(attemptAccounting, "settlement_id"),
                M1Slice6SuccessorAuthorityLoader.Text(attemptAccounting, "replay_edge_id"), rawSha, provenance);
        }
        JsonElement accounting = root.GetProperty("accounting");
        if (accounting.GetProperty("terminal_conservative_nano_usd").GetInt64() != ledger.Current.PriorConservativeNanoUsd
            || accounting.GetProperty("historical_committed_nano_usd").GetInt64()
                != hardBudget.HistoricalCommittedNanoUsd
            || accounting.GetProperty("successor_cumulative_reserved_nano_usd").GetInt64() != ledger.Current.SuccessorCumulativeReservedNanoUsd
            || accounting.GetProperty("successor_settled_nano_usd").GetInt64() != ledger.Current.SuccessorSettledNanoUsd
            || accounting.GetProperty("successor_unresolved_nano_usd").GetInt64() != ledger.Current.SuccessorUnresolvedNanoUsd
            || accounting.GetProperty("successor_outstanding_reserved_nano_usd").GetInt64() != 0
            || accounting.GetProperty("slice_total_committed_nano_usd").GetInt64()
                != ledger.CommittedNanoUsd)
        { throw new InvalidDataException("C3 accounting differs from the append-only ledger."); }
        ledger.RecordComposedEvidence(evidenceId, composedSha, now.AddTicks(1));
    }

    internal static string ActiveCredentialManifestPath(string repository, string campaignPath,
        M1Slice6SuccessorCampaignAuthority campaign)
    {
        if (campaign.CredentialAccessAuthorityId
            == "infinium.m1-s6.development-continuation/20260821")
        {
            return Path.GetFullPath(Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
                "m1-slice6-successor-credential-replacement-generation-3-authorization.v2.json"));
        }
        using JsonDocument campaignDocument = JsonDocument.Parse(File.ReadAllBytes(campaignPath));
        string credentialRelative = M1Slice6SuccessorAuthorityLoader.Text(
            campaignDocument.RootElement.GetProperty("credential_inheritance"), "manifest_path");
        return Path.GetFullPath(Path.Combine(repository,
            credentialRelative.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void RequireC3FileBinding(string composedDirectory, JsonElement binding,
        string expectedPath, string expectedSha, long expectedTailSequence, string expectedTailHash)
    {
        string relative = M1Slice6SuccessorAuthorityLoader.Text(binding, "path");
        string resolved = Path.GetFullPath(Path.Combine(composedDirectory,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        if (Path.IsPathFullyQualified(relative)
            || !resolved.StartsWith(composedDirectory.TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !resolved.Equals(Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase)
            || M1Slice6SuccessorAuthorityLoader.Text(binding, "sha256") != expectedSha
            || M1Slice6SuccessorAuthorityLoader.HashFile(resolved) != expectedSha
            || binding.GetProperty("tail_sequence").GetInt64() != expectedTailSequence
            || M1Slice6SuccessorAuthorityLoader.Text(binding, "tail_event_hash") != expectedTailHash)
        { throw new InvalidDataException("C3 ledger lineage binding is stale or escaped."); }
    }

    internal static string ValidateAttemptEvidence(string campaignPath,
        M1Slice6SuccessorCampaignAuthority campaign, M1Slice6SuccessorCampaignLedger ledger,
        M1Slice6SuccessorAttemptIdentity attempt, string evidencePath,
        bool historicalNormalizedV1 = false)
    {
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(evidencePath));
        string sha = M1Slice6SuccessorAuthorityLoader.Hash(bytes);
        if (ledger.Current.State != M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff
            || !historicalNormalizedV1 && ledger.Current.EvidenceSha256 != sha)
        { throw new InvalidDataException("Attempt evidence bytes differ from the durable handoff."); }
        string repository = RepositoryRoot(campaignPath);
        using JsonDocument identityDocument = JsonDocument.Parse(bytes);
        string schemaIdentity = M1Slice6SuccessorAuthorityLoader.Text(identityDocument.RootElement, "schema");
        string expectedSchema = historicalNormalizedV1
            ? M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSchemaV1
            : M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSchema;
        string schemaFile = schemaIdentity == expectedSchema
            ? historicalNormalizedV1
                ? "m1-slice6-successor-attempt-evidence-normalized-view.v1.schema.json"
                : "m1-slice6-successor-attempt-evidence.v2.schema.json"
            : throw new InvalidDataException(historicalNormalizedV1
                ? "The normalized historical evidence is not exact v1."
                : "Fresh successor attempt evidence must use the v2 diagnostic contract.");
        string schemaPath = Path.Combine(repository, "contracts", "repository",
            schemaFile);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(schemaPath),
            historicalNormalizedV1
                ? M1Slice6SuccessorAuthorityLoader.HistoricalNormalizedAttemptEvidenceSchema
                : expectedSchema);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        string expectedStatus = string.IsNullOrEmpty(ledger.Current.FailureDisposition)
            ? "first-structurally-valid-response-authoritative" : "failure-review-pending";
        if (M1Slice6SuccessorAuthorityLoader.Text(root, "campaign_id") != campaign.CampaignId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "campaign_manifest_sha256") != campaign.ManifestSha256
            || M1Slice6SuccessorAuthorityLoader.Text(root, "attempt_id") != attempt.AttemptId
            || root.GetProperty("attempt_ordinal").GetInt32() != attempt.AttemptOrdinal
            || M1Slice6SuccessorAuthorityLoader.Text(root, "stage_manifest_id") != attempt.StageManifestId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "stage_manifest_sha256") != attempt.StageManifestSha256
            || M1Slice6SuccessorAuthorityLoader.Text(root, "runtime_authority_id") != attempt.RuntimeAuthorityId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "runtime_authority_sha256") != attempt.RuntimeAuthoritySha256
            || M1Slice6SuccessorAuthorityLoader.Text(root, "status") != expectedStatus
            || (root.GetProperty("failure_disposition").ValueKind == JsonValueKind.Null ? ""
                : M1Slice6SuccessorAuthorityLoader.Text(root, "failure_disposition"))
                != ledger.Current.FailureDisposition
            || root.GetProperty("retry_permitted").GetBoolean())
        { throw new InvalidDataException("Attempt evidence identity or durable disposition is stale."); }
        JsonElement accounting = root.GetProperty("accounting");
        if (M1Slice6SuccessorAuthorityLoader.Text(accounting, "attempt_id") != attempt.AttemptId
            || M1Slice6SuccessorAuthorityLoader.Text(accounting, "request_id") != attempt.RequestId
            || M1Slice6SuccessorAuthorityLoader.Text(accounting, "reservation_id") != attempt.ReservationId
            || M1Slice6SuccessorAuthorityLoader.Text(accounting, "dispatch_fence_id") != attempt.DispatchFenceId)
        { throw new InvalidDataException("Attempt evidence accounting identities are not exact."); }
        JsonElement artifacts = root.GetProperty("retained_artifacts");
        string directory = Path.GetDirectoryName(Path.GetFullPath(evidencePath))!;
        RequireArtifact(artifacts, directory, "canonical_request_path", "canonical_request_sha256", required: true);
        RequireArtifact(artifacts, directory, "raw_response_path", "raw_response_sha256", required: false);
        RequireArtifact(artifacts, directory, "response_headers_path", "response_headers_sha256", required: false);
        RequireArtifact(artifacts, directory, "native_trace_path", "native_trace_sha256", required: false);
        RequireArtifact(artifacts, directory, "canary_evidence_path", "canary_evidence_sha256", required: false);
        if (!historicalNormalizedV1)
        {
            ValidateHelperBoundaryObservation(root, root.GetProperty("helper_boundary_observation"),
                artifacts, ledger.Current.FailureDisposition);
        }
        long reserved = root.GetProperty("reserved_nano_usd").GetInt64();
        long settled = root.GetProperty("settled_nano_usd").GetInt64();
        long unresolved = root.GetProperty("unresolved_hold_nano_usd").GetInt64();
        int handoffIndex = ledger.Entries.ToList().FindLastIndex(entry =>
            entry.State == M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff
            && entry.Attempt?.AttemptId == attempt.AttemptId);
        if (handoffIndex <= 0)
        { throw new InvalidDataException("Attempt evidence has no exact started accounting predecessor."); }
        M1Slice6SuccessorCampaignLedgerEntry started = ledger.Entries[handoffIndex - 1];
        M1Slice6SuccessorCampaignLedgerEntry handoff = ledger.Entries[handoffIndex];
        long exactSettled = handoff.SuccessorSettledNanoUsd - started.SuccessorSettledNanoUsd;
        long exactUnresolved = handoff.SuccessorUnresolvedNanoUsd - started.SuccessorUnresolvedNanoUsd;
        string expectedWorkPackage = attempt.Stage switch
        {
            M1Slice6CampaignStage.Qualification => "WP9",
            M1Slice6CampaignStage.SourceClaimExtraction => "WP10",
            M1Slice6CampaignStage.CandidateInvestigation => "WP11",
            _ => "",
        };
        if (M1Slice6SuccessorAuthorityLoader.Text(root, "stage") != attempt.Stage.ToString()
            || M1Slice6SuccessorAuthorityLoader.Text(root, "work_package") != expectedWorkPackage
            || reserved <= 0 || reserved != started.SuccessorOutstandingReservedNanoUsd
            || settled != exactSettled || unresolved != exactUnresolved
            || settled < 0 || unresolved < 0 || settled + unresolved > reserved)
        { throw new InvalidDataException("Attempt evidence accounting exceeds its exact reservation."); }
        if (string.IsNullOrEmpty(ledger.Current.FailureDisposition))
        {
            string expectedSemantic = attempt.Stage switch
            {
                M1Slice6CampaignStage.Qualification => "qualification-nonsemantic",
                M1Slice6CampaignStage.SourceClaimExtraction => "infinium.host.source-claim-admission/v1",
                M1Slice6CampaignStage.CandidateInvestigation => "infinium.host.candidate-investigation-admission/v1",
                _ => "",
            };
            if (!accounting.GetProperty("response_persisted").GetBoolean()
                || accounting.GetProperty("semantic_failure_code").ValueKind != JsonValueKind.Null
                || accounting.GetProperty("response_id").ValueKind != JsonValueKind.String
                || accounting.GetProperty("usage_entry_id").ValueKind != JsonValueKind.String
                || accounting.GetProperty("settlement_id").ValueKind != JsonValueKind.String
                || accounting.GetProperty("replay_edge_id").ValueKind != JsonValueKind.String
                || M1Slice6SuccessorAuthorityLoader.Text(accounting, "semantic_validation_id") != expectedSemantic
                || root.GetProperty("transport_disposition").GetString() != "response-received"
                || root.GetProperty("response_bytes_existed").ValueKind != JsonValueKind.True
                || unresolved != 0)
            {
                throw new InvalidDataException(
                    "A first-valid response cannot be accepted before exact SQLite replay and semantic admission.");
            }
        }
        return sha;
    }

    internal static string ValidateAttemptEvidence(string campaignPath,
        M1Slice6SuccessorCampaignAuthority campaign, M1Slice6SuccessorCampaignLedgerV3 ledger,
        M1Slice6SuccessorAttemptIdentity attempt, string evidencePath)
    {
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(evidencePath));
        string sha = M1Slice6SuccessorAuthorityLoader.Hash(bytes);
        if (ledger.Current.State != M1Slice6SuccessorCampaignV3State.AttemptEvidenceHandoff
            || ledger.Current.EvidenceSha256 != sha)
        { throw new InvalidDataException("Attempt evidence bytes differ from the durable handoff."); }
        string repository = RepositoryRoot(campaignPath);
        using JsonDocument identityDocument = JsonDocument.Parse(bytes);
        string schemaIdentity = M1Slice6SuccessorAuthorityLoader.Text(identityDocument.RootElement, "schema");
        string expectedSchema = M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSchemaV3;
        string schemaFile = schemaIdentity == expectedSchema
            ? "m1-slice6-successor-attempt-evidence.v3.schema.json"
            : throw new InvalidDataException("Fresh v6 successor attempt evidence must use v3.");
        string schemaPath = Path.Combine(repository, "contracts", "repository",
            schemaFile);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(schemaPath), expectedSchema);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        string expectedStatus = string.IsNullOrEmpty(ledger.Current.FailureDisposition)
            ? "first-structurally-valid-response-authoritative" : "failure-review-pending";
        if (M1Slice6SuccessorAuthorityLoader.Text(root, "campaign_id") != campaign.CampaignId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "campaign_manifest_sha256") != campaign.ManifestSha256
            || M1Slice6SuccessorAuthorityLoader.Text(root, "attempt_id") != attempt.AttemptId
            || root.GetProperty("attempt_ordinal").GetInt32() != attempt.AttemptOrdinal
            || M1Slice6SuccessorAuthorityLoader.Text(root, "stage_manifest_id") != attempt.StageManifestId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "stage_manifest_sha256") != attempt.StageManifestSha256
            || M1Slice6SuccessorAuthorityLoader.Text(root, "runtime_authority_id") != attempt.RuntimeAuthorityId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "runtime_authority_sha256") != attempt.RuntimeAuthoritySha256
            || M1Slice6SuccessorAuthorityLoader.Text(root, "status") != expectedStatus
            || (root.GetProperty("failure_disposition").ValueKind == JsonValueKind.Null ? ""
                : M1Slice6SuccessorAuthorityLoader.Text(root, "failure_disposition"))
                != ledger.Current.FailureDisposition
            || root.GetProperty("retry_permitted").GetBoolean())
        { throw new InvalidDataException("Attempt evidence identity or durable disposition is stale."); }
        JsonElement accounting = root.GetProperty("accounting");
        if (M1Slice6SuccessorAuthorityLoader.Text(accounting, "attempt_id") != attempt.AttemptId
            || M1Slice6SuccessorAuthorityLoader.Text(accounting, "request_id") != attempt.RequestId
            || M1Slice6SuccessorAuthorityLoader.Text(accounting, "reservation_id") != attempt.ReservationId
            || M1Slice6SuccessorAuthorityLoader.Text(accounting, "dispatch_fence_id") != attempt.DispatchFenceId)
        { throw new InvalidDataException("Attempt evidence accounting identities are not exact."); }
        JsonElement artifacts = root.GetProperty("retained_artifacts");
        string directory = Path.GetDirectoryName(Path.GetFullPath(evidencePath))!;
        RequireArtifact(artifacts, directory, "canonical_request_path", "canonical_request_sha256", required: true);
        RequireArtifact(artifacts, directory, "raw_response_path", "raw_response_sha256", required: false);
        RequireArtifact(artifacts, directory, "response_headers_path", "response_headers_sha256", required: false);
        RequireArtifact(artifacts, directory, "native_trace_path", "native_trace_sha256", required: false);
        RequireArtifact(artifacts, directory, "canary_evidence_path", "canary_evidence_sha256", required: false);
        ValidateHelperBoundaryObservation(root, root.GetProperty("helper_boundary_observation"),
            artifacts, ledger.Current.FailureDisposition);
        long reserved = root.GetProperty("reserved_nano_usd").GetInt64();
        long settled = root.GetProperty("settled_nano_usd").GetInt64();
        long unresolved = root.GetProperty("unresolved_hold_nano_usd").GetInt64();
        int handoffIndex = ledger.Entries.ToList().FindLastIndex(entry =>
            entry.State == M1Slice6SuccessorCampaignV3State.AttemptEvidenceHandoff
            && entry.Attempt?.AttemptId == attempt.AttemptId);
        if (handoffIndex <= 0)
        { throw new InvalidDataException("Attempt evidence has no exact started accounting predecessor."); }
        M1Slice6SuccessorCampaignLedgerV3Entry started = ledger.Entries[handoffIndex - 1];
        M1Slice6SuccessorCampaignLedgerV3Entry handoff = ledger.Entries[handoffIndex];
        long exactSettled = handoff.SuccessorSettledNanoUsd - started.SuccessorSettledNanoUsd;
        long exactUnresolved = handoff.SuccessorUnresolvedNanoUsd - started.SuccessorUnresolvedNanoUsd;
        string expectedWorkPackage = attempt.Stage switch
        {
            M1Slice6CampaignStage.Qualification => "WP9",
            M1Slice6CampaignStage.SourceClaimExtraction => "WP10",
            M1Slice6CampaignStage.CandidateInvestigation => "WP11",
            _ => "",
        };
        if (M1Slice6SuccessorAuthorityLoader.Text(root, "stage") != attempt.Stage.ToString()
            || M1Slice6SuccessorAuthorityLoader.Text(root, "work_package") != expectedWorkPackage
            || reserved <= 0 || reserved != started.SuccessorOutstandingReservedNanoUsd
            || settled != exactSettled || unresolved != exactUnresolved
            || settled < 0 || unresolved < 0 || settled + unresolved > reserved)
        { throw new InvalidDataException("Attempt evidence accounting exceeds its exact reservation."); }
        if (string.IsNullOrEmpty(ledger.Current.FailureDisposition))
        {
            string expectedSemantic = attempt.Stage switch
            {
                M1Slice6CampaignStage.Qualification => "qualification-nonsemantic",
                M1Slice6CampaignStage.SourceClaimExtraction => "infinium.host.source-claim-admission/v1",
                M1Slice6CampaignStage.CandidateInvestigation => "infinium.host.candidate-investigation-admission/v1",
                _ => "",
            };
            if (!accounting.GetProperty("response_persisted").GetBoolean()
                || accounting.GetProperty("semantic_failure_code").ValueKind != JsonValueKind.Null
                || accounting.GetProperty("response_id").ValueKind != JsonValueKind.String
                || accounting.GetProperty("usage_entry_id").ValueKind != JsonValueKind.String
                || accounting.GetProperty("settlement_id").ValueKind != JsonValueKind.String
                || accounting.GetProperty("replay_edge_id").ValueKind != JsonValueKind.String
                || M1Slice6SuccessorAuthorityLoader.Text(accounting, "semantic_validation_id") != expectedSemantic
                || root.GetProperty("transport_disposition").GetString() != "response-received"
                || root.GetProperty("response_bytes_existed").ValueKind != JsonValueKind.True
                || unresolved != 0)
            {
                throw new InvalidDataException(
                    "A first-valid response cannot be accepted before exact SQLite replay and semantic admission.");
            }
        }
        return sha;
    }

    private static string ValidateRecoveryEvidence(string campaignPath,
        M1Slice6SuccessorCampaignAuthority campaign, M1Slice6SuccessorCampaignLedgerV3 ledger,
        M1Slice6SuccessorAttemptIdentity attempt, string evidencePath)
    {
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(evidencePath));
        string sha = ValidateRecoverySchema(campaignPath, bytes);
        if (ledger.Current.EvidenceSha256 != sha)
        { throw new InvalidDataException("Recovery evidence bytes differ from the ledger handoff."); }
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        JsonElement semantic = root.GetProperty("semantic");
        string expected = attempt.Stage switch
        {
            M1Slice6CampaignStage.Qualification => "qualification-nonsemantic",
            M1Slice6CampaignStage.SourceClaimExtraction => "infinium.host.source-claim-admission/v1",
            M1Slice6CampaignStage.CandidateInvestigation => "infinium.host.candidate-investigation-admission/v1",
            _ => "",
        };
        if (M1Slice6SuccessorAuthorityLoader.Text(root, "campaign_id") != campaign.CampaignId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "campaign_manifest_sha256") != campaign.ManifestSha256
            || M1Slice6SuccessorAuthorityLoader.Text(root, "attempt_id") != attempt.AttemptId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "stage") != attempt.Stage.ToString()
            || root.GetProperty("provider_effect_used").GetBoolean()
            || M1Slice6SuccessorAuthorityLoader.Text(semantic, "validation_id") != expected)
        { throw new InvalidDataException("Recovery evidence does not close the exact semantic attempt."); }
        return sha;
    }

    private static string ValidateRecoverySchema(string campaignPath, byte[] bytes)
    {
        string repository = Path.GetDirectoryName(Path.GetFullPath(campaignPath))!;
        while (!File.Exists(Path.Combine(repository, "Infinium.sln")))
        { repository = Directory.GetParent(repository)?.FullName ?? throw new InvalidDataException("Recovery has no contract root."); }
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-authoritative-recovery.v1.schema.json")),
            M1Slice6SuccessorAuthorityLoader.RecoveryEvidenceSchema);
        return M1Slice6SuccessorAuthorityLoader.Hash(bytes);
    }

    private static void RequireArtifact(JsonElement artifacts, string directory,
        string pathName, string shaName, bool required)
    {
        JsonElement pathNode = artifacts.GetProperty(pathName);
        JsonElement shaNode = artifacts.GetProperty(shaName);
        if (pathNode.ValueKind == JsonValueKind.Null || shaNode.ValueKind == JsonValueKind.Null)
        {
            if (required || pathNode.ValueKind != shaNode.ValueKind)
            { throw new InvalidDataException("Attempt evidence has an incomplete artifact binding."); }
            return;
        }
        string relative = pathNode.GetString()!;
        string path = Path.GetFullPath(Path.Combine(directory, relative));
        if (Path.IsPathFullyQualified(relative)
            || !path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || M1Slice6SuccessorAuthorityLoader.HashFile(path) != shaNode.GetString())
        { throw new InvalidDataException("Attempt evidence artifact escaped or changed."); }
    }

    private static string Failure(OpenAiResponsesResult response)
    {
        if (response.State == ProviderResponseState.Completed && response.Admitted
            && response.HttpStatus is >= 200 and <= 299 && response.RawResponseBytes is { Length: > 0 }
            && !string.IsNullOrWhiteSpace(response.ProviderResponseId)
            && !string.IsNullOrWhiteSpace(response.ClientRequestId)
            && response.ErrorCode is null && response.RefusalCode is null && response.IncompleteReason is null)
        { return ""; }
        return response.State switch
        {
            ProviderResponseState.Refusal => "provider-refused",
            ProviderResponseState.Malformed or ProviderResponseState.Mismatched => "provider-malformed",
            ProviderResponseState.Incomplete or ProviderResponseState.Cancelled => "provider-incomplete",
            ProviderResponseState.Oversized => "provider-oversized",
            ProviderResponseState.Unknown when response.RawResponseBytes is null => "transport-ambiguous",
            _ => "provider-failed",
        };
    }

    private static byte[] Evidence(M1Slice6SuccessorCampaignAuthority campaign,
        M1Slice6CampaignStageAuthority authority, M1Slice6SuccessorAttemptIdentity attempt,
        M1Slice6SuccessorRuntimeAuthority runtime, M1Slice6CampaignAccountingAdmission admission,
        M1Slice6CampaignStageBoundaryResult? result, string failure, long reserved, long settled,
        long unresolved, M1Slice6SuccessorAccountingPersistence? persistence,
        M1Slice6CampaignBoundaryFailureReceipt? failureReceipt, RetainedAttemptArtifacts artifacts)
    {
        OpenAiResponsesResult? response = result?.Response;
        object evidence = new
        {
            schema = M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSchemaV3,
            status = failure.Length == 0 ? "first-structurally-valid-response-authoritative" : "failure-review-pending",
            campaign_id = campaign.CampaignId,
            campaign_manifest_sha256 = campaign.ManifestSha256,
            stage = authority.Stage.ToString(),
            work_package = authority.WorkPackage,
            attempt_id = attempt.AttemptId,
            attempt_ordinal = attempt.AttemptOrdinal,
            stage_manifest_id = attempt.StageManifestId,
            stage_manifest_sha256 = attempt.StageManifestSha256,
            runtime_authority_id = runtime.AuthorityId,
            runtime_authority_sha256 = runtime.ManifestSha256,
            failure_stage = response?.FailureStage ?? failureReceipt?.FailureStage ?? "helper-evidence",
            failure_disposition = failure.Length == 0 ? null : failure,
            transport_disposition = response?.TransportDisposition ?? failureReceipt?.TransportDisposition ?? "helper-evidence-failure",
            http_status = response?.HttpStatus ?? failureReceipt?.HttpStatus,
            provider_error_type = response?.ProviderErrorType ?? failureReceipt?.ProviderErrorType,
            provider_error_code = response?.RawResponseBytes is null
                ? failureReceipt?.ProviderErrorCode : response.ErrorCode,
            local_failure_code = response?.RawResponseBytes is null
                ? response?.ErrorCode ?? failureReceipt?.LocalFailureCode : failureReceipt?.LocalFailureCode,
            provider_response_id = response?.ProviderResponseId ?? failureReceipt?.ProviderResponseId,
            client_request_id = response?.ClientRequestId ?? failureReceipt?.ClientRequestId ?? admission.RequestId,
            provider_request_id = response?.ProviderRequestId ?? failureReceipt?.ProviderRequestId,
            response_bytes_existed = response is null ? failureReceipt?.ResponseBytesExisted : response.ResponseBytesExisted,
            response_bytes_observed_lower_bound = response?.ResponseBytesObservedLowerBound ?? failureReceipt?.ResponseBytesObservedLowerBound,
            retained_response_bytes = response?.RawResponseBytes?.LongLength
                ?? (failureReceipt?.ResponseBytesExisted == false ? 0 : null),
            provider_send_count = response?.SendCount ?? failureReceipt?.ProviderSendCount,
            dns_resolution_count = result?.DnsResolutionCount ?? failureReceipt?.DnsResolutionCount,
            retry_permitted = false,
            reserved_nano_usd = reserved,
            settled_nano_usd = settled,
            unresolved_hold_nano_usd = unresolved,
            usage = response?.Usage,
            rate_facts = response?.RateHeaders.Select(item => new { name = item.Name, value = item.Value }).ToArray() ?? [],
            helper_boundary_observation = result?.HelperBoundaryObservation
                ?? failureReceipt?.HelperBoundaryObservation
                ?? M1Slice6CampaignProductionStageBoundary.UnavailableHelperObservation(),
            retained_artifacts = new
            {
                canonical_request_path = artifacts.CanonicalRequestPath,
                canonical_request_sha256 = artifacts.CanonicalRequestSha256,
                raw_response_path = artifacts.RawResponsePath,
                raw_response_sha256 = artifacts.RawResponseSha256,
                response_headers_path = artifacts.ResponseHeadersPath,
                response_headers_sha256 = artifacts.ResponseHeadersSha256,
                native_trace_path = artifacts.NativeTracePath,
                native_trace_sha256 = artifacts.NativeTraceSha256,
                canary_evidence_path = artifacts.CanaryEvidencePath,
                canary_evidence_sha256 = artifacts.CanaryEvidenceSha256,
            },
            accounting = new
            {
                authorization_id = admission.AuthorizationId,
                operation_id = admission.OperationId,
                attempt_id = admission.AttemptId,
                request_id = admission.RequestId,
                reservation_id = admission.ReservationId,
                dispatch_fence_id = admission.DispatchFenceId,
                response_id = NonEmpty(persistence?.ResponseId),
                usage_entry_id = NonEmpty(persistence?.UsageEntryId),
                settlement_id = NonEmpty(persistence?.SettlementId),
                replay_edge_id = NonEmpty(persistence?.ReplayEdgeId),
                response_persisted = persistence?.ResponsePersisted ?? false,
                semantic_validation_id = persistence?.Semantic?.ValidationId,
                semantic_disposition = persistence?.Semantic?.Disposition,
                semantic_result_sha256 = persistence?.Semantic?.ResultSha256,
                semantic_provenance = persistence?.Semantic?.Provenance
                    ?? M1Slice6CampaignSemanticProvenance.Empty,
                semantic_failure_code = NonEmpty(persistence?.SemanticFailureCode),
            },
        };
        return JsonSerializer.SerializeToUtf8Bytes(evidence, EvidenceJson);
    }

    private static RetainedAttemptArtifacts RetainAttemptArtifacts(
        M1Slice6CampaignStageAuthority authority, M1Slice6CampaignStageBoundaryResult? result,
        string evidencePath, M1Slice6CampaignBoundaryFailureReceipt? failureReceipt = null)
    {
        string directory = Path.GetDirectoryName(evidencePath)!;
        string stem = Path.GetFileNameWithoutExtension(evidencePath);
        string requestName = stem + ".canonical-request.json";
        string requestPath = Path.Combine(directory, requestName);
        if (!File.Exists(requestPath)
            || M1Slice6SuccessorAuthorityLoader.HashFile(requestPath) != authority.CanonicalRequestSha256)
        { throw new InvalidDataException("The preflight-retained canonical request is absent or stale."); }
        static (string? Name, string? Sha) Retain(string directory, string name, byte[]? bytes)
        {
            if (bytes is not { Length: > 0 }) { return (null, null); }
            WriteNew(Path.Combine(directory, name), bytes);
            return (name, M1Slice6SuccessorAuthorityLoader.Hash(bytes));
        }
        (string? rawName, string? rawSha) = Retain(directory, stem + ".raw-response.bin",
            result?.Response.RawResponseBytes ?? failureReceipt?.SafeRawResponseBytes);
        (string? headersName, string? headersSha) = Retain(directory, stem + ".response-headers.json",
            result?.ResponseHeadersBytes ?? failureReceipt?.SafeResponseHeadersBytes);
        (string? traceName, string? traceSha) = Retain(directory, stem + ".native-trace.json",
            result?.NativeCallTraceBytes ?? failureReceipt?.ValidatedNativeCallTraceBytes);
        (string? canaryName, string? canarySha) = Retain(directory, stem + ".canaries.json",
            result?.CanaryEvidenceBytes ?? failureReceipt?.ValidatedCanaryEvidenceBytes);
        return new(requestName, authority.CanonicalRequestSha256, rawName, rawSha,
            headersName, headersSha, traceName, traceSha, canaryName, canarySha);
    }

    private static string? NonEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static byte[] ReadExactRetained(string path)
    {
        string full = Path.GetFullPath(path);
        if (!File.Exists(full) || Path.GetFileName(full) != Path.GetFileName(path))
        { throw new InvalidDataException("A started-failure retained sidecar is absent."); }
        return File.ReadAllBytes(full);
    }

    private static void ValidateRecoveredPreflight(byte[] bytes,
        M1Slice6SuccessorAttemptIdentity attempt, M1Slice6CampaignStageAuthority authority,
        M1Slice6SuccessorRuntimeAuthority runtime)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        M1Slice6SuccessorAuthorityLoader.Exact(root, "schema", "attempt_id",
            "stage_manifest_sha256", "runtime_authority_sha256", "disposition");
        if (M1Slice6SuccessorAuthorityLoader.Text(root, "schema")
                != "infinium.m1-s6.successor-evidence-preflight/v1"
            || M1Slice6SuccessorAuthorityLoader.Text(root, "attempt_id") != attempt.AttemptId
            || M1Slice6SuccessorAuthorityLoader.Text(root, "stage_manifest_sha256")
                != authority.ManifestSha256
            || M1Slice6SuccessorAuthorityLoader.Text(root, "runtime_authority_sha256")
                != runtime.ManifestSha256
            || M1Slice6SuccessorAuthorityLoader.Text(root, "disposition")
                != "fresh-paths-created-before-possible-start")
        { throw new InvalidDataException("Started-failure preflight evidence is stale."); }
    }

    internal static M1Slice6CampaignBoundaryFailureReceipt RecoveredAmbiguousReceipt(
        byte[] headers, byte[] trace, byte[] canaries, M1Slice6SuccessorAttemptIdentity attempt,
        string targetFingerprint)
    {
        using JsonDocument document = JsonDocument.Parse(headers);
        JsonElement root = document.RootElement;
        M1Slice6SuccessorAuthorityLoader.Exact(root, "schema", "state", "failure_stage",
            "transport_disposition", "http_status", "response_bytes_existed",
            "response_bytes_observed_lower_bound", "retained_response_bytes",
            "provider_response_id", "provider_request_id", "returned_model",
            "returned_service_tier", "refusal_code", "incomplete_reason",
            "provider_error_type", "provider_error_code", "local_failure_code",
            "requested_output_schema", "usage", "dns_resolution_count", "network_used",
            "send_count", "headers");
        JsonElement usage = root.GetProperty("usage");
        M1Slice6SuccessorAuthorityLoader.Exact(usage, "Availability", "DispatchCount",
            "InputTokens", "OutputTokens", "TotalTokens", "ReasoningTokens",
            "CacheReadTokens", "CacheWriteTokens", "PricedToolCalls", "CalculatedNanoUsd",
            "BillingAvailability", "RateAvailability", "CreditAvailability", "ReceiptState");
        foreach (string quantityName in new[] { "DispatchCount", "InputTokens", "OutputTokens",
            "TotalTokens", "ReasoningTokens", "CacheReadTokens", "CacheWriteTokens",
            "PricedToolCalls", "CalculatedNanoUsd" })
        {
            JsonElement quantity = usage.GetProperty(quantityName);
            M1Slice6SuccessorAuthorityLoader.Exact(quantity, "Availability", "Value");
            if (quantity.GetProperty("Availability").GetInt32() != 2
                || quantity.GetProperty("Value").ValueKind != JsonValueKind.Null)
            { throw new InvalidDataException("Ambiguous response usage is not wholly unavailable."); }
        }
        string[] nullFields = ["http_status", "provider_response_id", "provider_request_id",
            "returned_model", "returned_service_tier", "refusal_code", "incomplete_reason",
            "provider_error_code", "requested_output_schema"];
        JsonElement providerErrorTypeElement = root.GetProperty("provider_error_type");
        string? providerErrorType = providerErrorTypeElement.ValueKind == JsonValueKind.Null
            ? null
            : M1Slice6SuccessorAuthorityLoader.Text(root, "provider_error_type");
        string[] providerErrorParts = providerErrorType?.Split('.') ?? [];
        bool httpErrorValid = providerErrorParts.Length is 2 or 4
            && providerErrorParts[0] == "HttpRequestError"
            && Enum.TryParse(providerErrorParts[1], ignoreCase: false, out HttpRequestError parsedError)
            && Enum.IsDefined(parsedError)
            && providerErrorParts[1] == parsedError.ToString();
        bool socketErrorValid = providerErrorParts.Length == 2
            || (providerErrorParts.Length == 4
                && providerErrorParts[2] == "SocketError"
                && Enum.TryParse(providerErrorParts[3], ignoreCase: false, out SocketError parsedSocketError)
                && Enum.IsDefined(parsedSocketError)
                && parsedSocketError != SocketError.Success
                && providerErrorParts[3] == parsedSocketError.ToString());
        bool providerErrorTypeValid = providerErrorType is null || (httpErrorValid && socketErrorValid);
        if (M1Slice6SuccessorAuthorityLoader.Text(root, "schema")
                != "infinium.openai.response-headers/v2"
            || root.GetProperty("state").GetInt32() != (int)ProviderResponseState.Unknown
            || M1Slice6SuccessorAuthorityLoader.Text(root, "failure_stage") != "provider-transport"
            || M1Slice6SuccessorAuthorityLoader.Text(root, "transport_disposition")
                != "may-have-started-no-response"
            || nullFields.Any(name => root.GetProperty(name).ValueKind != JsonValueKind.Null)
            || !providerErrorTypeValid
            || root.GetProperty("response_bytes_existed").GetBoolean()
            || root.GetProperty("response_bytes_observed_lower_bound").GetInt64() != 0
            || root.GetProperty("retained_response_bytes").GetInt64() != 0
            || M1Slice6SuccessorAuthorityLoader.Text(root, "local_failure_code") != "transport_ambiguous"
            || usage.GetProperty("Availability").GetInt32() != 2
            || usage.GetProperty("BillingAvailability").GetInt32() != 2
            || usage.GetProperty("RateAvailability").GetInt32() != 2
            || usage.GetProperty("CreditAvailability").GetInt32() != 2
            || usage.GetProperty("ReceiptState").GetInt32() != 5
            || root.GetProperty("dns_resolution_count").GetInt32() != 1
            || !root.GetProperty("network_used").GetBoolean()
            || root.GetProperty("send_count").GetInt32() != 1
            || root.GetProperty("headers").GetArrayLength() != 0)
        { throw new InvalidDataException("Retained ambiguous response header evidence is not exact."); }
        ValidateRecoveredCredentialTrace(trace, targetFingerprint);
        ValidateRecoveredCanaries(canaries);
        M1Slice6HelperBoundaryObservation observation = new(false, null, "Unavailable", true,
            null, null, null, null, "may-have-started-no-response", true, 1, 1,
            null, null, null, null, null, null, null, null, null, 2,
            ["helper-receipt-unavailable"]);
        return new("provider-transport", "may-have-started-no-response", "transport_ambiguous",
            null, providerErrorType, null, null, attempt.RequestId, null, false, 0, 1, 1,
            null, headers, observation, trace, canaries);
    }

    private static void ValidateRecoveredCredentialTrace(byte[] bytes, string targetFingerprint)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement[] calls = document.RootElement.EnumerateArray().ToArray();
        if (calls.Length != 2) { throw new InvalidDataException("Recovered credential trace is incomplete."); }
        foreach (JsonElement call in calls)
        {
            M1Slice6SuccessorAuthorityLoader.Exact(call, "Sequence", "Operation",
                "TargetFingerprintSha256", "Scenario", "Result", "AllocationId", "PairedAllocationId");
        }
        long allocation = calls[0].GetProperty("AllocationId").GetInt64();
        if (calls[0].GetProperty("Sequence").GetInt32() != 1
            || M1Slice6SuccessorAuthorityLoader.Text(calls[0], "Operation") != "CredReadW"
            || M1Slice6SuccessorAuthorityLoader.Text(calls[0], "Result") != "success"
            || calls[0].GetProperty("PairedAllocationId").ValueKind != JsonValueKind.Null
            || calls[1].GetProperty("Sequence").GetInt32() != 2
            || M1Slice6SuccessorAuthorityLoader.Text(calls[1], "Operation") != "CredFree"
            || M1Slice6SuccessorAuthorityLoader.Text(calls[1], "Result") != "released"
            || calls[1].GetProperty("AllocationId").ValueKind != JsonValueKind.Null
            || calls[1].GetProperty("PairedAllocationId").GetInt64() != allocation
            || allocation <= 0
            || calls.Any(call => M1Slice6SuccessorAuthorityLoader.Text(call, "Scenario")
                != "m1-s6-campaign-provider-dispatch")
            || calls.Any(call => M1Slice6SuccessorAuthorityLoader.Text(
                call, "TargetFingerprintSha256") != targetFingerprint))
        { throw new InvalidDataException("Recovered credential trace is not exact read/free evidence."); }
    }

    private static void ValidateRecoveredCanaries(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        M1Slice6SuccessorAuthorityLoader.Exact(root, "SecretMatches", "RawTargetMatches",
            "RawTargetEncodings", "ScannedSurfaces");
        string[] encodings = root.GetProperty("RawTargetEncodings").EnumerateArray()
            .Select(value => value.GetString()!).ToArray();
        JsonElement[] surfaces = root.GetProperty("ScannedSurfaces").EnumerateArray().ToArray();
        string[] names = ["private protocol request", "private protocol response", "native call trace",
            "process command line", "process environment names"];
        string[] kinds = ["private-pipe-bytes", "private-pipe-bytes", "canonical-trace-bytes",
            "captured-text", "captured-text"];
        if (root.GetProperty("SecretMatches").GetInt32() != 0
            || root.GetProperty("RawTargetMatches").GetInt32() != 0
            || !encodings.SequenceEqual(["utf-8", "utf-16le"], StringComparer.Ordinal)
            || surfaces.Length != names.Length)
        { throw new InvalidDataException("Recovered canary evidence is incomplete or matched protected bytes."); }
        for (int index = 0; index < surfaces.Length; index++)
        {
            M1Slice6SuccessorAuthorityLoader.Exact(surfaces[index], "Name", "Kind", "ByteCount",
                "SecretMatches", "RawTargetMatches");
            if (M1Slice6SuccessorAuthorityLoader.Text(surfaces[index], "Name") != names[index]
                || M1Slice6SuccessorAuthorityLoader.Text(surfaces[index], "Kind") != kinds[index]
                || surfaces[index].GetProperty("ByteCount").GetInt64() <= 0
                || surfaces[index].GetProperty("SecretMatches").GetInt32() != 0
                || surfaces[index].GetProperty("RawTargetMatches").GetInt32() != 0)
            { throw new InvalidDataException("Recovered canary surface is stale or matched protected bytes."); }
        }
    }

    internal static JsonNode NormalizeKnownV1AbsentValues(byte[] originalBytes)
    {
        JsonNode originalNode = JsonNode.Parse(originalBytes)
            ?? throw new InvalidDataException("The original attempt evidence is empty.");
        JsonNode normalized = originalNode.DeepClone();
        JsonObject accounting = normalized["accounting"]?.AsObject()
            ?? throw new InvalidDataException("The original attempt accounting is absent.");
        foreach (string field in new[] { "response_id", "usage_entry_id", "replay_edge_id", "semantic_failure_code" })
        {
            if (accounting[field]?.GetValue<string>() != "")
            { throw new InvalidDataException("The supplement may normalize only the four known empty accounting fields."); }
            accounting[field] = null;
        }
        return normalized;
    }

    private static string RepositoryRoot(string path)
    {
        string repository = Path.GetDirectoryName(Path.GetFullPath(path))!;
        while (!File.Exists(Path.Combine(repository, "Infinium.sln")))
        {
            repository = Directory.GetParent(repository)?.FullName
                ?? throw new InvalidDataException("Successor evidence has no repository contract root.");
        }
        return repository;
    }

    internal static void ValidateHelperBoundaryObservation(JsonElement root, JsonElement observation,
        JsonElement artifacts, string failureDisposition)
    {
        string[] failed = observation.GetProperty("failed_predicate_ids").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
        if (failed.Distinct(StringComparer.Ordinal).Count() != failed.Length
            || !failed.SequenceEqual(failed.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        { throw new InvalidDataException("Helper failed-predicate IDs are not deterministic and unique."); }
        bool receiptAvailable = observation.GetProperty("receipt_available").GetBoolean();
        int? send = observation.GetProperty("adapter_send_count").ValueKind == JsonValueKind.Null
            ? null : observation.GetProperty("adapter_send_count").GetInt32();
        int? dns = observation.GetProperty("adapter_dns_resolution_count").ValueKind == JsonValueKind.Null
            ? null : observation.GetProperty("adapter_dns_resolution_count").GetInt32();
        bool? parsed = observation.GetProperty("staged_envelope_parsed").ValueKind == JsonValueKind.Null
            ? null : observation.GetProperty("staged_envelope_parsed").GetBoolean();
        bool securityFailure = failureDisposition == "safety-isolation-breach";
        bool helperFailure = failureDisposition == "helper-evidence-failure";
        bool hasTrace = artifacts.GetProperty("native_trace_path").ValueKind == JsonValueKind.String;
        bool hasCanary = artifacts.GetProperty("canary_evidence_path").ValueKind == JsonValueKind.String;
        static bool? Boolean(JsonElement node, string name) => node.GetProperty(name).ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
        static int? Integer(JsonElement node, string name) =>
            node.GetProperty(name).ValueKind == JsonValueKind.Number
                ? node.GetProperty(name).GetInt32() : null;
        List<string> expected = [];
        int? topSend = root.GetProperty("provider_send_count").ValueKind == JsonValueKind.Null
            ? null : root.GetProperty("provider_send_count").GetInt32();
        int? topDns = root.GetProperty("dns_resolution_count").ValueKind == JsonValueKind.Null
            ? null : root.GetProperty("dns_resolution_count").GetInt32();
        string topTransport = M1Slice6SuccessorAuthorityLoader.Text(root, "transport_disposition");
        string helperOutcome = M1Slice6SuccessorAuthorityLoader.Text(observation, "helper_outcome");
        string? adapterTransport = observation.GetProperty("adapter_transport_disposition").ValueKind
            == JsonValueKind.Null ? null
                : M1Slice6SuccessorAuthorityLoader.Text(observation, "adapter_transport_disposition");
        bool recoveredPartialReceipt = !receiptAvailable
            && failureDisposition == "transport-ambiguous"
            && helperOutcome == "Unavailable";
        if (receiptAvailable)
        {
            int? stagedBytes = Integer(observation, "staged_envelope_bytes");
            bool? transport = Boolean(observation, "transport_may_have_started");
            bool? retry = Boolean(observation, "retry_attempted");
            bool? network = Boolean(observation, "adapter_network_used");
            int? listeners = Integer(observation, "listener_snapshot_count");
            int? tcp = Integer(observation, "tcp_non_listener_snapshot_count");
            bool? probe = Boolean(observation, "containment_probe_executed");
            int? total = Integer(observation, "total_contained_process_count");
            int? survivors = Integer(observation, "process_tree_survivor_count");
            bool? terminated = Boolean(observation, "process_tree_terminated");
            bool? excluded = Boolean(observation, "excluded_handle_accessible");
            int? exitCode = Integer(observation, "helper_exit_code");
            int? active = Integer(observation, "active_contained_process_count_before_job_close");
            int? native = Integer(observation, "native_credential_operation_count");
            if (new int?[] { stagedBytes, listeners, tcp, total, survivors, exitCode, active, native }
                    .Any(value => value is null)
                || transport is null || retry is null || parsed is null || network is null
                || probe is null || terminated is null || excluded is null)
            { throw new InvalidDataException("An available helper receipt has null raw containment facts."); }
            if (transport == false) { expected.Add("transport-may-have-started-false"); }
            if (retry == true) { expected.Add("retry-attempted"); }
            if (stagedBytes == 0) { expected.Add("staged-envelope-empty"); }
            else if (parsed == false) { expected.Add("staged-envelope-invalid"); }
            if (send != 1) { expected.Add("adapter-send-count-not-one"); }
            if (network != true) { expected.Add("adapter-network-used-false"); }
            if (dns != 1) { expected.Add("adapter-dns-count-not-one"); }
            if (listeners != 0) { expected.Add("listener-snapshot-nonzero"); }
            if (tcp is < 0 or > 1) { expected.Add("tcp-snapshot-count-out-of-range"); }
            if (probe != true) { expected.Add("containment-probe-missing"); }
            if (total < 2) { expected.Add("contained-process-count-too-small"); }
            if (survivors != 0) { expected.Add("process-tree-survivor-present"); }
            if (terminated != true) { expected.Add("process-tree-not-terminated"); }
            if (excluded == true) { expected.Add("excluded-handle-accessible"); }
            expected.Sort(StringComparer.Ordinal);
        }
        if (receiptAvailable && parsed != (observation.GetProperty("staged_raw_bytes").ValueKind == JsonValueKind.Number
                && observation.GetProperty("staged_header_bytes").ValueKind == JsonValueKind.Number)
            || receiptAvailable && !failed.SequenceEqual(expected, StringComparer.Ordinal)
            || failureDisposition.Length == 0 && failed.Length != 0
            || receiptAvailable && helperFailure && failed.Length == 0
            || receiptAvailable && !helperFailure && !securityFailure && failed.Length != 0
            || receiptAvailable && (topSend != send || topDns != dns)
            || receiptAvailable && helperOutcome == "receipt-unavailable"
            || receiptAvailable && helperOutcome is not ("Unspecified" or "Completed" or "FailedKnown"
                or "TransportMayHaveStarted" or "Unavailable" or "Cancelled" or "Oversized" or "Malformed")
            || receiptAvailable && adapterTransport is not null && topTransport != adapterTransport
            || receiptAvailable && adapterTransport is null
                && Boolean(observation, "transport_may_have_started") == false && topTransport != "pre-send-known"
            || receiptAvailable && adapterTransport is null
                && Boolean(observation, "transport_may_have_started") == true
                && topTransport != "may-have-started-no-response"
            || !receiptAvailable && !recoveredPartialReceipt
                && (failed.Length != 1 || failed[0] != "helper-receipt-unavailable"
                || helperOutcome != "receipt-unavailable" || parsed is not null || send is not null || dns is not null
                || topSend is not null || topDns is not null)
            || !receiptAvailable && !recoveredPartialReceipt && observation.EnumerateObject().Any(property =>
                property.Name is not ("receipt_available" or "helper_outcome" or "failed_predicate_ids")
                && property.Value.ValueKind != JsonValueKind.Null)
            || !receiptAvailable && !recoveredPartialReceipt && (hasTrace || hasCanary)
            || recoveredPartialReceipt && (failed.Length != 1
                || failed[0] != "helper-receipt-unavailable" || !hasTrace || !hasCanary
                || topSend != 1 || topDns != 1 || send != 1 || dns != 1
                || topTransport != "may-have-started-no-response"
                || adapterTransport != "may-have-started-no-response"
                || Boolean(observation, "transport_may_have_started") != true
                || Boolean(observation, "adapter_network_used") != true
                || Integer(observation, "native_credential_operation_count") != 2
                || observation.EnumerateObject().Any(property => property.Name is
                    ("helper_exit_code" or "staged_envelope_bytes" or "staged_envelope_parsed"
                    or "staged_raw_bytes" or "staged_header_bytes" or "retry_attempted"
                    or "listener_snapshot_count" or "tcp_non_listener_snapshot_count"
                    or "containment_probe_executed" or "active_contained_process_count_before_job_close"
                    or "total_contained_process_count" or "process_tree_survivor_count"
                    or "process_tree_terminated" or "excluded_handle_accessible")
                    && property.Value.ValueKind != JsonValueKind.Null))
            || (send is < 0 or > 1) || (dns is < 0 or > 1)
            || (securityFailure && (hasTrace || hasCanary))
            || (!securityFailure && receiptAvailable && (!hasTrace || !hasCanary)))
        { throw new InvalidDataException("Helper-boundary observation or validated security sidecars are inconsistent."); }
    }

    private static void PreflightEvidence(string evidencePath,
        M1Slice6CampaignStageAuthority authority, M1Slice6SuccessorAttemptIdentity attempt,
        M1Slice6SuccessorRuntimeAuthority runtime)
    {
        string directory = Path.GetDirectoryName(evidencePath)
            ?? throw new InvalidDataException("Successor evidence path has no exact directory.");
        Directory.CreateDirectory(directory);
        string stem = Path.GetFileNameWithoutExtension(evidencePath);
        string[] paths =
        [
            evidencePath,
            Path.Combine(directory, stem + ".raw-response.bin"),
            Path.Combine(directory, stem + ".response-headers.json"),
            Path.Combine(directory, stem + ".canonical-request.json"),
            Path.Combine(directory, stem + ".native-trace.json"),
            Path.Combine(directory, stem + ".canaries.json"),
            Path.Combine(directory, stem + ".preflight.json"),
        ];
        if (paths.Any(File.Exists) || paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length)
        { throw new InvalidDataException("Successor evidence paths are not fresh and disjoint."); }
        byte[] marker = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "infinium.m1-s6.successor-evidence-preflight/v1",
            attempt_id = attempt.AttemptId,
            stage_manifest_sha256 = authority.ManifestSha256,
            runtime_authority_sha256 = runtime.ManifestSha256,
            disposition = "fresh-paths-created-before-possible-start",
        }, EvidenceJson);
        WriteNew(Path.Combine(directory, stem + ".canonical-request.json"), authority.CanonicalRequest);
        WriteNew(paths[^1], [.. marker, (byte)'\n']);
    }

    private static long Required(ProviderQuantityContract quantity, string name) =>
        quantity.Availability == ProviderAvailabilityState.Available && quantity.Value is >= 0
            ? quantity.Value.Value : throw new InvalidDataException("Exact " + name + " is unavailable.");

    private static (string LedgerPath, string EvidencePath, string ProductStateRoot)
        ValidateRecoveryRoots(M1Slice6SuccessorRuntimeAuthority runtime,
            M1Slice6SuccessorCampaignAuthority campaign, string credentialPath,
            string credentialSha, string ledgerPath, string evidencePath,
            string? recoveryOutputPath = null)
    {
        ledgerPath = Path.GetFullPath(ledgerPath);
        evidencePath = Path.GetFullPath(evidencePath);
        string productStateRoot = Path.GetFullPath(campaign.ProductStateRoot);
        recoveryOutputPath = recoveryOutputPath is null
            ? null : Path.GetFullPath(recoveryOutputPath);
        if (M1Slice6SuccessorAuthorityLoader.HashFile(credentialPath) != credentialSha
            || credentialSha != campaign.CredentialManifestSha256
            || runtime.LedgerPath != ledgerPath || runtime.EvidencePath != evidencePath
            || runtime.SafetyStateRoot != productStateRoot
            || !runtime.SafetyStateRoot.Equals(campaign.ProductStateRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Recovery roots or credential binding differ from runtime authority.");
        }
        if (!IsContained(runtime.OutputRoot, ledgerPath)
            || !IsContained(runtime.OutputRoot, evidencePath)
            || recoveryOutputPath is not null
                && (!IsContained(runtime.OutputRoot, recoveryOutputPath)
                    || recoveryOutputPath.Equals(evidencePath, StringComparison.OrdinalIgnoreCase))
            || IsContained(runtime.OutputRoot, productStateRoot)
            || IsContained(productStateRoot, runtime.OutputRoot)
            || runtime.OutputRoot.Equals(productStateRoot, StringComparison.OrdinalIgnoreCase)
            || runtime.OutputRoot.Contains("infinium-c2a-execution", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Recovery evidence roots are not exact, contained, fresh, and disjoint.");
        }
        return (ledgerPath, evidencePath, productStateRoot);
    }

    private static bool IsContained(string root, string path)
    {
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        path = Path.GetFullPath(path);
        return path.StartsWith(root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            4096, FileOptions.WriteThrough);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }
}
