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

internal static class M1Slice6SuccessorAuthorityLoader
{
    internal static readonly string[] SupplementLimitations =
    [
        "actual-adapter-send-count-unverified",
        "credential-read-free-trace-not-independently-retained",
        "exact-containment-predicate-unavailable",
    ];
    internal const string CampaignSchema = "infinium.repository.m1-slice6-successor-campaign-authorization/5.0.0";
    internal const string StageSchema = "infinium.repository.m1-slice6-successor-stage-attempt/5.0.0";
    internal const string RuntimeSchema = "infinium.provider.effect-runtime-authority/v2";
    internal const string AttemptEvidenceSchemaV1 = "infinium.m1-s6.successor-attempt-evidence/v1";
    internal const string AttemptEvidenceSchema = "infinium.m1-s6.successor-attempt-evidence/v2";
    internal const string AttemptEvidenceSupplementSchema =
        "infinium.m1-s6.successor-attempt-evidence-supplement/v1";
    internal const string RecoveryEvidenceSchema =
        "infinium.m1-s6.successor-authoritative-recovery/v1";
    internal const string IndependentReviewSchemaV1 =
        "infinium.repository.m1-slice6-successor-independent-review/1.0.0";
    internal const string IndependentReviewSchema =
        "infinium.repository.m1-slice6-successor-independent-review/2.0.0";

    internal static M1Slice6SuccessorIndependentReview Review(
        string path, string kind, string subjectId, string subjectSha256, bool? correctionRequired,
        string? failureEvidenceId = null, string? failureEvidenceSha256 = null)
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
        string expectedReviewSchema = supplementReview ? IndependentReviewSchema : IndependentReviewSchemaV1;
        string schemaPath = Path.Combine(repository, "contracts", "repository",
            supplementReview ? "m1-slice6-successor-independent-review.v2.schema.json"
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

    internal static M1Slice6SuccessorCampaignAuthority Campaign(string path, string expectedSha)
    {
        (byte[] bytes, string sha) = ExactBytes(path, expectedSha);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        Exact(root, "schema_identity", "campaign_id", "status", "prepared_at_utc", "expires_at_utc",
            "owner_amendment", "terminal_predecessor", "credential_inheritance", "limits", "ordered_stages");
        if (Text(root, "schema_identity") != CampaignSchema || Text(root, "status") != "owner-authorized-reviewed-and-admitted")
        {
            throw new InvalidDataException("The successor campaign is not exact admitted v5 authority.");
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
        Exact(limits, "maximum_possible_starts_per_stage", "terminal_wp9_possible_starts",
            "maximum_slice_nano_usd", "terminal_conservative_nano_usd", "maximum_successor_nano_usd",
            "automatic_retry", "parallel_calls", "first_structurally_valid_response");
        JsonElement[] stages = root.GetProperty("ordered_stages").EnumerateArray().ToArray();
        if (stages.Length != 3)
        { throw new InvalidDataException("The successor campaign stage order is not exact."); }
        for (int index = 0; index < stages.Length; index++)
        {
            JsonElement stage = stages[index];
            Exact(stage, "ordinal", "work_package", "maximum_lineage_starts",
                "maximum_successor_starts", "maximum_nano_usd_per_start");
            long expectedCost = index == 0 ? 140_000_000 : 600_000_000;
            int expectedSuccessorStarts = index == 0 ? 4 : 5;
            if (stage.GetProperty("ordinal").GetInt32() != index + 1
                || Text(stage, "work_package") != "WP" + (index + 9)
                || stage.GetProperty("maximum_lineage_starts").GetInt32() != 5
                || stage.GetProperty("maximum_successor_starts").GetInt32() != expectedSuccessorStarts
                || stage.GetProperty("maximum_nano_usd_per_start").GetInt64() != expectedCost)
            { throw new InvalidDataException("The successor campaign stage limits or order changed."); }
        }
        if (Text(terminal, "final_event_hash") != M1Slice6SuccessorCampaignLedger.RequiredTerminalEventHash
            || terminal.GetProperty("possible_starts").GetInt32() != 1
            || terminal.GetProperty("conservative_nano_usd").GetInt64() != M1Slice6SuccessorCampaignLedger.PriorConservativeNanoUsd
            || !terminal.GetProperty("immutable").GetBoolean()
            || limits.GetProperty("maximum_possible_starts_per_stage").GetInt32() != 5
            || limits.GetProperty("terminal_wp9_possible_starts").GetInt32() != 1
            || limits.GetProperty("maximum_slice_nano_usd").GetInt64() != M1Slice6SuccessorCampaignLedger.SliceMaximumNanoUsd
            || limits.GetProperty("terminal_conservative_nano_usd").GetInt64() != M1Slice6SuccessorCampaignLedger.PriorConservativeNanoUsd
            || limits.GetProperty("maximum_successor_nano_usd").GetInt64() != M1Slice6SuccessorCampaignLedger.SuccessorMaximumNanoUsd
            || limits.GetProperty("automatic_retry").GetBoolean() || limits.GetProperty("parallel_calls").GetBoolean()
            || Text(limits, "first_structurally_valid_response") != "permanent-stage-authority-stop-further-provider-starts")
        {
            throw new InvalidDataException("The successor limits differ from the exact owner amendment.");
        }
        string repository = FindRepositoryRoot(path);
        string amendmentPath = ResolveRepositoryPath(repository, Text(amendment, "path"));
        string accessPath = ResolveRepositoryPath(repository, Text(credential, "access_authority_path"));
        if (HashFile(amendmentPath) != Text(amendment, "sha256")
            || Text(amendment, "owner_acceptance_ceremony") != "satisfied-by-exact-owner-message-no-second-ceremony"
            || HashFile(accessPath) != Text(credential, "access_authority_sha256"))
        {
            throw new InvalidDataException("The successor owner amendment or credential-access authority is stale.");
        }
        string retainedProductStateRoot;
        string retainedSafetyProjection;
        string retainedSnapshotOriginSha256;
        using (JsonDocument access = JsonDocument.Parse(File.ReadAllBytes(accessPath)))
        {
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
            Exact(accessAmendment, "path", "sha256");
            JsonElement boundary = accessRoot.GetProperty("per_attempt_boundary");
            Exact(boundary, "masked_helper_only", "exact_native_call_order", "maximum_reads",
                "maximum_frees", "maximum_writes", "maximum_deletes", "enumeration", "exposure", "replacement");
            string acceptancePath = ResolveRepositoryPath(repository, Text(accepted, "acceptance_path"));
            using JsonDocument acceptanceDocument = JsonDocument.Parse(File.ReadAllBytes(acceptancePath));
            JsonElement acceptedEvidence = acceptanceDocument.RootElement.GetProperty("evidence");
            string[] nativeOrder = boundary.GetProperty("exact_native_call_order").EnumerateArray()
                .Select(item => item.GetString() ?? "").ToArray();
            if (Text(accessRoot, "authority_id") != Text(credential, "access_authority_id")
                || Text(accessRoot, "schema_identity") != "infinium.repository.m1-slice6-successor-credential-access/1.0.0"
                || Text(accessRoot, "status") != "reviewed-and-admitted"
                || Text(retained, "manifest_id") != Text(credential, "manifest_id")
                || Text(retained, "manifest_sha256") != Text(credential, "manifest_sha256")
                || Text(accessAmendment, "path") != Text(amendment, "path")
                || Text(accessAmendment, "sha256") != Text(amendment, "sha256")
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
            retainedProductStateRoot, retainedSnapshotOriginSha256, retainedSafetyProjection, expiry);
    }

    internal static (M1Slice6CampaignStageAuthority Authority, M1Slice6SuccessorAttemptIdentity Attempt)
        Stage(string path, string expectedSha, M1Slice6SuccessorCampaignAuthority campaign,
            M1Slice6SuccessorRuntimeAuthority runtime)
    {
        (byte[] bytes, string sha) = ExactBytes(path, expectedSha);
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
        Exact(binding, "campaign_id", "campaign_manifest_sha256", "credential_manifest_id", "credential_manifest_sha256");
        if (Text(binding, "campaign_id") != campaign.CampaignId
            || Text(binding, "campaign_manifest_sha256") != campaign.ManifestSha256
            || Text(binding, "credential_manifest_id") != campaign.CredentialManifestId
            || Text(binding, "credential_manifest_sha256") != campaign.CredentialManifestSha256)
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
            || runtime.OwnerAmendmentId != campaign.OwnerAmendmentId
            || runtime.OwnerAmendmentSha256 != campaign.OwnerAmendmentSha256
            || runtime.OwnerDecisionId != campaign.OwnerAmendmentId
            || runtime.OwnerDecisionSha256 != campaign.OwnerAmendmentSha256
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
        M1Slice6CampaignStageLimits limits = M1Slice6CampaignStageLimits.For(stage);
        JsonElement limitsNode = root.GetProperty("limits");
        Exact(limitsNode, "maximum_request_bytes", "maximum_input_tokens", "maximum_output_tokens",
            "maximum_raw_response_bytes", "maximum_nano_usd", "deadline_milliseconds");
        long[] actualLimits = limitsNode.EnumerateObject().Select(value => value.Value.GetInt64()).ToArray();
        long[] exactLimits = [limits.MaximumRequestBytes, limits.MaximumInputTokens, limits.MaximumOutputTokens,
            limits.MaximumRawResponseBytes, limits.MaximumNanoUsd, limits.DeadlineMilliseconds];
        ProviderFiniteLimitsContract proofLimits = new(limits.MaximumRequestBytes, limits.MaximumInputTokens,
            limits.MaximumOutputTokens, limits.MaximumRawResponseBytes, 1, limits.MaximumNanoUsd,
            limits.DeadlineMilliseconds);
        ProviderInputBoundEvidence proof = OpenAiResponsesInputBoundPolicy.Prove(operation, canonical, proofLimits);
        if (Hash(canonical) != Text(canonicalNode, "sha256")
            || canonical.LongLength != canonicalNode.GetProperty("bytes").GetInt64()
            || canonicalNode.GetProperty("proved_input_tokens").GetInt64() != proof.ConservativeInputTokenUpperBound
            || canonicalNode.GetProperty("maximum_output_tokens").GetInt64() != limits.MaximumOutputTokens
            || !actualLimits.SequenceEqual(exactLimits))
        {
            throw new InvalidDataException("The successor canonical request proof or limits are stale.");
        }
        OpenAiResponsesCanonicalSerializer.ValidateExactProfile(canonical, limits.MaximumOutputTokens);
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
        return (new(M1Slice6AuthorityContractVersion.SuccessorV5, attempt.StageManifestId, sha, stage,
            Text(stageNode, "work_package"), operation, "successor-v5-owner-authorized",
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
        string coordinatorPath, string helperPath, DateTimeOffset now)
    {
        (byte[] bytes, string sha) = ExactBytes(path, expectedSha);
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
            || now.Offset != TimeSpan.Zero || now < notBefore || now >= expiry || notBefore >= expiry
            || Path.GetFullPath(coordinatorPath) != expectedCoordinator
            || Path.GetFullPath(helperPath) != expectedHelper
            || HashFile(coordinatorPath) != Text(candidate, "coordinator_sha256")
            || HashFile(helperPath) != Text(candidate, "helper_sha256")
            || HashFile(runtimeCandidatePath) != Text(candidate, "candidate_sha256")
            || HashFile(Path.Combine(productState, "successor-snapshot-origin.v1.json"))
                != Text(execution, "product_state_snapshot_origin_sha256")
            || ComputeProductStateCheckpointSha256(productState)
                != Text(execution, "product_state_checkpoint_sha256")
            || HashFile(reviewPath) != Text(review, "evidence_sha256")
            || HashFile(ownerDecisionPath) != Text(owner, "decision_sha256")
            || !revision.Success || revision.Groups["sha"].Value != implementationCommit)
        {
            throw new InvalidDataException("The successor runtime authority is stale, expired, or broader than one start.");
        }
        byte[] runtimeCandidateBytes = File.ReadAllBytes(runtimeCandidatePath);
        ActiveRepositoryJsonSchemaValidator.Validate(runtimeCandidateBytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-runtime-candidate.v1.schema.json")),
            "infinium.repository.m1-slice6-successor-runtime-candidate/1.0.0");
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
        M1Slice6SuccessorIndependentReview typedReview = Review(reviewPath, "runtime-attempt",
            Text(candidate, "candidate_id"), Text(candidate, "candidate_sha256"), false);
        if (typedReview.ReviewId != Text(review, "evidence_id"))
        { throw new InvalidDataException("The runtime authority review identity is stale."); }
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
        if (command.ExecuteScalar() is not string version || version != "7")
        { throw new InvalidDataException("The evolved successor product state is not exact schema 7."); }
        command.CommandText = "SELECT value FROM store_metadata WHERE key='schema_fingerprint';";
        if (command.ExecuteScalar() is not string fingerprint
            || fingerprint != ProviderPersistenceDeclarations.SuccessorAttemptSchemaFingerprint)
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
    private static string FindRepositoryRoot(string path)
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
    };

    internal static async Task<int> RunAttemptAsync(string campaignPath, string campaignSha,
        string stagePath, string stageSha, string credentialPath, string credentialSha,
        string runtimePath, string runtimeSha, string ledgerPath, string safetyStateRoot,
        string helperPath, string helperSha, string evidencePath, CancellationToken cancellationToken)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string coordinatorPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The coordinator executable path is unavailable.");
        M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath, campaignSha);
        if (now >= campaign.ExpiresAtUtc || M1Slice6SuccessorAuthorityLoader.HashFile(credentialPath) != credentialSha
            || credentialSha != campaign.CredentialManifestSha256 || M1Slice6SuccessorAuthorityLoader.HashFile(helperPath) != helperSha)
        {
            throw new InvalidDataException("Successor campaign, credential, or helper binding is stale.");
        }
        M1Slice6SuccessorRuntimeAuthority runtime = M1Slice6SuccessorAuthorityLoader.Runtime(
            runtimePath, runtimeSha, coordinatorPath, helperPath, now);
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
        M1Slice6SuccessorCampaignLedger ledger = new(ledgerPath, campaign.CampaignId,
            campaign.ManifestSha256, campaign.TerminalCampaignId, campaign.TerminalEventHash, now);
        (M1Slice6CampaignStageAuthority authority, M1Slice6SuccessorAttemptIdentity attempt) =
            M1Slice6SuccessorAuthorityLoader.Stage(stagePath, stageSha, campaign, runtime);
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
        M1Slice6CampaignAccountingAdmission admission = accounting.PrepareSuccessor(
            authority, campaignIdentity, attempt, now.AddTicks(1));
        long reservation = admission.ReservedNanoUsd;
        if (reservation <= 0 || reservation > authority.Limits.MaximumNanoUsd)
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
            or IOException or Microsoft.Data.Sqlite.SqliteException)
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

    internal static void InitializeCampaign(string campaignPath, string campaignSha, string ledgerPath,
        string reviewPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath, campaignSha);
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

    internal static void AcceptAttempt(string campaignPath, string campaignSha, string ledgerPath,
        string evidencePath, string reviewPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath, campaignSha);
        M1Slice6SuccessorCampaignLedger ledger = new(ledgerPath, campaign.CampaignId, campaign.ManifestSha256,
            campaign.TerminalCampaignId, campaign.TerminalEventHash, now);
        M1Slice6SuccessorAttemptIdentity attempt = ledger.Current.Attempt
            ?? throw new InvalidOperationException("The successor ledger has no attempt evidence handoff.");
        bool recovery = ledger.Current.State == M1Slice6SuccessorCampaignState.AuthoritativeRecoveryHandoff;
        string evidenceId = recovery ? "successor-authoritative-recovery-" + attempt.AttemptId
            : "successor-attempt-evidence-" + attempt.AttemptId;
        string evidenceSha = recovery
            ? ValidateRecoveryEvidence(campaignPath, campaign, ledger, attempt, evidencePath)
            : ValidateAttemptEvidence(campaignPath, campaign, ledger, attempt, evidencePath);
        M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
            reviewPath, "attempt-evidence", evidenceId, evidenceSha, false);
        ledger.AcceptAttemptEvidence(attempt, evidenceId, evidenceSha,
            review.ReviewId, review.ManifestSha256, now.AddTicks(1));
    }

    internal static void AcceptAttemptSupplement(string campaignPath, string campaignSha,
        string ledgerPath, string originalEvidencePath, string normalizedEvidencePath,
        string supplementPath, string reviewPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign =
            M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath, campaignSha);
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
        string stagePath, string stageSha, string credentialPath, string credentialSha,
        string runtimePath, string runtimeSha, string ledgerPath, string originalEvidencePath,
        string recoveryPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign =
            M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath, campaignSha);
        M1Slice6SuccessorRuntimeAuthority runtime = M1Slice6SuccessorAuthorityLoader.Runtime(
            runtimePath, runtimeSha, Environment.ProcessPath
                ?? throw new InvalidOperationException("The coordinator executable path is unavailable."),
            Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "Infinium.CredentialHelper.exe"), now);
        (M1Slice6CampaignStageAuthority authority, M1Slice6SuccessorAttemptIdentity attempt) =
            M1Slice6SuccessorAuthorityLoader.Stage(stagePath, stageSha, campaign, runtime);
        M1Slice6SuccessorCampaignLedger ledger = new(ledgerPath, campaign.CampaignId,
            campaign.ManifestSha256, campaign.TerminalCampaignId, campaign.TerminalEventHash, now);
        if (ledger.Current.State != M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff
            || ledger.Current.Attempt != attempt || ledger.Current.FailureDisposition.Length != 0
            || attempt.Stage == M1Slice6CampaignStage.Qualification)
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

    internal static void AcceptCorrectionReview(string campaignPath, string campaignSha,
        string ledgerPath, string reviewPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath, campaignSha);
        M1Slice6SuccessorCampaignLedger ledger = new(ledgerPath, campaign.CampaignId, campaign.ManifestSha256,
            campaign.TerminalCampaignId, campaign.TerminalEventHash, now);
        M1Slice6SuccessorAttemptIdentity attempt = ledger.Current.Attempt
            ?? throw new InvalidOperationException("The accepted failure has no exact attempt identity.");
        M1Slice6SuccessorCampaignLedgerEntry failure = ledger.Entries.Last(entry =>
            entry.Attempt?.AttemptId == attempt.AttemptId
            && entry.State == M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff);
        M1Slice6SuccessorIndependentReview review = M1Slice6SuccessorAuthorityLoader.Review(
            reviewPath, "offline-correction", failure.EvidenceId, failure.EvidenceSha256, null,
            failure.EvidenceId, failure.EvidenceSha256);
        if (review.DefectId is null || ledger.Entries.Any(entry =>
                entry.State == M1Slice6SuccessorCampaignState.CorrectionReviewed
                && entry.FailureDisposition == "defect:" + review.DefectId))
        { throw new InvalidOperationException("The same defect recurred after reviewed diagnosis/correction."); }
        ledger.RecordOfflineCorrectionReview(review.ReviewId, review.ManifestSha256,
            review.DefectId, now.AddTicks(1));
    }

    internal static void CompleteComposedEvidence(string campaignPath, string campaignSha,
        string ledgerPath, string composedPath, string reviewPath, DateTimeOffset now)
    {
        M1Slice6SuccessorCampaignAuthority campaign = M1Slice6SuccessorAuthorityLoader.Campaign(campaignPath, campaignSha);
        M1Slice6SuccessorCampaignLedger ledger = new(ledgerPath, campaign.CampaignId, campaign.ManifestSha256,
            campaign.TerminalCampaignId, campaign.TerminalEventHash, now);
        if (ledger.Current.State is not (M1Slice6SuccessorCampaignState.StageAccepted
                or M1Slice6SuccessorCampaignState.ComposedEvidenceHandoff)
            || !ledger.Current.Wp9Authoritative || !ledger.Current.Wp10Authoritative
            || !ledger.Current.Wp11Authoritative || ledger.Current.SuccessorOutstandingReservedNanoUsd != 0)
        { throw new InvalidOperationException("C3 requires three independently accepted first-valid stage responses."); }
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(composedPath));
        string composedSha = M1Slice6SuccessorAuthorityLoader.Hash(bytes);
        string repository = Path.GetDirectoryName(Path.GetFullPath(campaignPath))!;
        while (!File.Exists(Path.Combine(repository, "Infinium.sln")))
        { repository = Directory.GetParent(repository)?.FullName ?? throw new InvalidDataException("C3 has no contract root."); }
        using JsonDocument campaignDocument = JsonDocument.Parse(File.ReadAllBytes(campaignPath));
        string credentialRelative = M1Slice6SuccessorAuthorityLoader.Text(
            campaignDocument.RootElement.GetProperty("credential_inheritance"), "manifest_path");
        string credentialPath = Path.GetFullPath(Path.Combine(repository,
            credentialRelative.Replace('/', Path.DirectorySeparatorChar)));
        using M1Slice6CampaignSqliteProviderAccounting c3Accounting = new(campaign.ProductStateRoot,
            credentialPath, campaign.CredentialManifestSha256, now);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(Path.Combine(repository,
            "contracts", "repository", "m1-slice6-successor-composed-evidence.v1.schema.json")),
            "infinium.m1-s6.successor-composed-evidence/v1");
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        string evidenceId = M1Slice6SuccessorAuthorityLoader.Text(root, "evidence_id");
        if (ledger.Current.State == M1Slice6SuccessorCampaignState.ComposedEvidenceHandoff)
        {
            if (ledger.Current.EvidenceId != evidenceId || ledger.Current.EvidenceSha256 != composedSha)
            { throw new InvalidDataException("C3 resumed with different composed evidence bytes."); }
            M1Slice6SuccessorIndependentReview resumedReview = M1Slice6SuccessorAuthorityLoader.Review(
                reviewPath, "composed-closeout", evidenceId, composedSha, false);
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
        M1Slice6SuccessorCampaignLedgerEntry[] handoffs = ledger.Entries
            .Where(entry => entry.State == M1Slice6SuccessorCampaignState.AttemptEvidenceHandoff).ToArray();
        JsonElement[] attempts = root.GetProperty("attempts").EnumerateArray().ToArray();
        if (attempts.Length != handoffs.Length)
        { throw new InvalidDataException("C3 omitted or invented successor attempts."); }
        string composedDirectory = Path.GetDirectoryName(Path.GetFullPath(composedPath))!;
        for (int index = 0; index < handoffs.Length; index++)
        {
            M1Slice6SuccessorCampaignLedgerEntry handoff = handoffs[index];
            int handoffIndex = ledger.Entries.ToList().IndexOf(handoff);
            M1Slice6SuccessorCampaignLedgerEntry? reviewEntry = ledger.Entries.Skip(handoffIndex + 1)
                .FirstOrDefault(entry => entry.Attempt?.AttemptId == handoff.Attempt?.AttemptId
                    && entry.State is (M1Slice6SuccessorCampaignState.AttemptFailureAccepted
                        or M1Slice6SuccessorCampaignState.StageAccepted));
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
        M1Slice6SuccessorCampaignLedgerEntry[] authoritative = ledger.Entries
            .Where(entry => entry.State == M1Slice6SuccessorCampaignState.StageAccepted)
            .Select(entry => ledger.Entries[ledger.Entries.ToList().IndexOf(entry) - 1]).ToArray();
        if (authorities.Length != 3 || authoritative.Length != 3)
        { throw new InvalidDataException("C3 requires exactly one authoritative response per stage."); }
        for (int index = 0; index < 3; index++)
        {
            JsonElement item = authorities[index];
            M1Slice6SuccessorCampaignLedgerEntry exact = authoritative[index];
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
            if (exact.State == M1Slice6SuccessorCampaignState.AuthoritativeRecoveryHandoff)
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
            || accounting.GetProperty("successor_cumulative_reserved_nano_usd").GetInt64() != ledger.Current.SuccessorCumulativeReservedNanoUsd
            || accounting.GetProperty("successor_settled_nano_usd").GetInt64() != ledger.Current.SuccessorSettledNanoUsd
            || accounting.GetProperty("successor_unresolved_nano_usd").GetInt64() != ledger.Current.SuccessorUnresolvedNanoUsd
            || accounting.GetProperty("successor_outstanding_reserved_nano_usd").GetInt64() != 0
            || accounting.GetProperty("slice_total_committed_nano_usd").GetInt64()
                != checked(ledger.Current.PriorConservativeNanoUsd + ledger.Current.SuccessorCumulativeReservedNanoUsd))
        { throw new InvalidDataException("C3 accounting differs from the append-only ledger."); }
        ledger.RecordComposedEvidence(evidenceId, composedSha, now.AddTicks(1));
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
                ? "m1-slice6-successor-attempt-evidence.v1.schema.json"
                : "m1-slice6-successor-attempt-evidence.v2.schema.json"
            : throw new InvalidDataException(historicalNormalizedV1
                ? "The normalized historical evidence is not exact v1."
                : "Fresh successor attempt evidence must use the v2 diagnostic contract.");
        string schemaPath = Path.Combine(repository, "contracts", "repository",
            schemaFile);
        ActiveRepositoryJsonSchemaValidator.Validate(bytes, File.ReadAllBytes(schemaPath),
            expectedSchema);
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

    private static string ValidateRecoveryEvidence(string campaignPath,
        M1Slice6SuccessorCampaignAuthority campaign, M1Slice6SuccessorCampaignLedger ledger,
        M1Slice6SuccessorAttemptIdentity attempt, string evidencePath)
    {
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(evidencePath));
        string sha = ValidateRecoverySchema(campaignPath, bytes);
        if (ledger.Current.EvidenceSha256 != sha)
        { throw new InvalidDataException("Recovery evidence bytes differ from the ledger handoff."); }
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        JsonElement semantic = root.GetProperty("semantic");
        string expected = attempt.Stage == M1Slice6CampaignStage.SourceClaimExtraction
            ? "infinium.host.source-claim-admission/v1"
            : "infinium.host.candidate-investigation-admission/v1";
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
            schema = M1Slice6SuccessorAuthorityLoader.AttemptEvidenceSchema,
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
            retained_response_bytes = response?.RawResponseBytes?.LongLength,
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
            || !receiptAvailable && (failed.Length != 1 || failed[0] != "helper-receipt-unavailable"
                || helperOutcome != "receipt-unavailable" || parsed is not null || send is not null || dns is not null
                || topSend is not null || topDns is not null)
            || !receiptAvailable && observation.EnumerateObject().Any(property =>
                property.Name is not ("receipt_available" or "helper_outcome" or "failed_predicate_ids")
                && property.Value.ValueKind != JsonValueKind.Null)
            || !receiptAvailable && (hasTrace || hasCanary)
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
