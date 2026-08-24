using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed record RetainedProviderReplayProvenance(
    string OperationId,
    string AuthorizationId,
    string SemanticOperationId,
    string SemanticAuthorizationId,
    string ResponseRecordId,
    string SemanticResponseRecordId,
    string RawResponsePayloadId,
    string RawResponseSha256,
    long RawResponseByteLength,
    string CanonicalRequestSha256,
    string RequestedModelAvailability,
    string? RequestedModel,
    string? ReturnedModel,
    string UsageEntryId,
    string UsageAvailability,
    string InputTokensAvailability,
    long? InputTokens,
    string OutputTokensAvailability,
    long? OutputTokens,
    string TotalTokensAvailability,
    long? TotalTokens,
    string CalculatedNanoUsdAvailability,
    long? CalculatedNanoUsd);

public sealed record RetainedSourceDecisionReplayResult(
    string AcquisitionRunId,
    string ParentAnalysisRunId,
    string InstallationSnapshotId,
    string AnalysisContextId,
    string ProposalId,
    string ProposalOrExtractionState,
    string SourceRevisionId,
    string PassageId,
    string AdmittedArtifactSha256,
    string SourceApplicationLinkId,
    string SourceApplicationAuthorityState,
    string SupportState,
    string ApplicabilityState,
    string HostDecisionState,
    string RootSubjectId,
    string AdmittedArtifactId,
    IReadOnlyList<string> ApplicabilityFactIds,
    string CandidateOutcomeId,
    string CandidateInputPayloadId,
    string CandidateInputSha256,
    long CandidateInputByteLength,
    string CandidateTranscriptId,
    string CandidateTranscriptPayloadId,
    string CandidateResultPayloadId,
    string CandidateResponseFingerprint,
    RetainedProviderReplayProvenance SourceProvider,
    RetainedProviderReplayProvenance CandidateProvider,
    bool ConsumedByCandidateInvestigation,
    bool CurrentSemanticAuthority,
    bool AppliesToSlice7Subjects,
    string Slice7ApplicabilityReason);

public sealed partial class AuthoritativeStore
{
    public RetainedSourceDecisionReplayResult ReplayRetainedSourceDecisionChain(
        string acquisitionRunId,
        string candidateOutcomeId,
        IReadOnlyCollection<string> slice7SubjectIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acquisitionRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateOutcomeId);
        ArgumentNullException.ThrowIfNull(slice7SubjectIds);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT proposal.proposal_id,admission.admission_id,application.application_link_id,
                  admission.support_state,admission.applicability_state,admission.decision_state,
                  proposal.root_subject_id,admission.admitted_artifact_id,
                  authority.input_payload_id,payload.content_sha256,payload.byte_length,
                  proposal.operation_id,proposal.response_record_id,
                  authority.source_revision_id,authority.passage_id,authority.content_sha256,
                  outcome.operation_id,outcome.transcript_id,outcome.response_record_id,
                  outcome.response_fingerprint,outcome.transcript_payload_id,outcome.result_payload_id,
                  acquisition.parent_analysis_run_id,acquisition.installation_snapshot_id,
                  acquisition.analysis_context_id
                FROM candidate_evidence_authority authority
                JOIN candidate_investigation_outcomes outcome
                  ON outcome.outcome_id=authority.outcome_id
                JOIN evidence_acquisition_runs acquisition
                  ON acquisition.acquisition_run_id=authority.source_acquisition_id
                JOIN provider_semantic_proposals proposal
                  ON proposal.proposal_id=authority.source_proposal_id
                 AND proposal.owner_kind='evidence-acquisition-run'
                 AND proposal.owner_id=authority.source_acquisition_id
                JOIN provider_semantic_admissions admission
                  ON admission.admission_id=authority.source_admission_id
                 AND admission.proposal_id=proposal.proposal_id
                 AND admission.admitted_artifact_id=authority.admitted_artifact_id
                JOIN evidence_acquisition_application_links application
                  ON application.application_link_id=authority.source_application_link_id
                 AND application.acquisition_run_id=authority.source_acquisition_id
                 AND application.admission_id=authority.source_admission_id
                 AND application.admitted_artifact_id=authority.admitted_artifact_id
                JOIN payloads payload ON payload.payload_id=authority.input_payload_id
                WHERE authority.source_acquisition_id=$acquisition
                  AND authority.outcome_id=$outcome
                  AND authority.root_kind='persisted-source-claim-application'
                  AND authority.source_application_decision_id IS NULL;
                """;
            command.Parameters.AddWithValue("$acquisition", acquisitionRunId);
            command.Parameters.AddWithValue("$outcome", candidateOutcomeId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidDataException("The exact retained historical WP10-to-WP11 source-decision chain is unavailable.");
            }
            string proposalId = reader.GetString(0);
            string sourceAdmissionId = reader.GetString(1);
            string applicationLinkId = reader.GetString(2);
            string support = reader.GetString(3);
            string applicability = reader.GetString(4);
            string hostDecision = reader.GetString(5);
            string rootSubject = reader.GetString(6);
            string artifactId = reader.GetString(7);
            string inputPayloadId = reader.GetString(8);
            string inputSha = reader.GetString(9);
            long inputLength = reader.GetInt64(10);
            string sourceOperationId = reader.GetString(11);
            string sourceResponseRecordId = reader.GetString(12);
            string sourceRevisionId = reader.GetString(13);
            string passageId = reader.GetString(14);
            string admittedArtifactSha256 = reader.GetString(15);
            string candidateOperationId = reader.GetString(16);
            string candidateTranscriptId = reader.GetString(17);
            string candidateResponseRecordId = reader.IsDBNull(18)
                ? throw new InvalidDataException("The retained WP11 outcome has no semantic response identity.")
                : reader.GetString(18);
            string candidateResponseFingerprint = reader.GetString(19);
            string candidateTranscriptPayloadId = reader.GetString(20);
            string candidateResultPayloadId = reader.GetString(21);
            string parentAnalysisRunId = reader.GetString(22);
            string installationSnapshotId = reader.GetString(23);
            string analysisContextId = reader.GetString(24);
            if (reader.Read())
            {
                throw new InvalidDataException("The exact retained historical WP10-to-WP11 source-decision chain is ambiguous.");
            }
            reader.Close();

            HistoricalProviderSemanticPayloadReadModel historicalPayload =
                ReadHistoricalSourceClaimPayload(acquisitionRunId, sourceAdmissionId);
            using JsonDocument historical = JsonDocument.Parse(historicalPayload.Payload);
            JsonElement historicalRoot = historical.RootElement;
            JsonElement proposal = historicalRoot.GetProperty("ClaimProposals").EnumerateArray().SingleOrDefault(item =>
                item.GetProperty("ProposalId").GetProperty("Value").GetString() == proposalId);
            if (proposal.ValueKind == JsonValueKind.Undefined)
            {
                throw new InvalidDataException("The retained source proposal is absent from its exact historical payload.");
            }
            string historicalOperationId = historicalRoot.GetProperty("OperationId").GetProperty("Value").GetString()!;
            string historicalRevisionId = historicalRoot.GetProperty("SourceRevisionId").GetProperty("Value").GetString()!;
            string historicalPassageId = proposal.GetProperty("PassageId").GetProperty("Value").GetString()!;
            bool passageRetained = historicalRoot.GetProperty("PassageIds").EnumerateArray().Any(item =>
                item.GetProperty("Value").GetString() == historicalPassageId);
            if (historicalOperationId != sourceOperationId
                || historicalRevisionId != sourceRevisionId
                || historicalPassageId != passageId
                || !passageRetained)
            {
                throw new InvalidDataException("The retained source proposal provenance drifted from its historical application link.");
            }
            SemanticProposalState proposalState = (SemanticProposalState)proposal.GetProperty("State").GetInt32();
            if (!Enum.IsDefined(proposalState) || proposalState == SemanticProposalState.Unspecified)
            {
                throw new InvalidDataException("The retained source proposal has no explicit historical extraction state.");
            }
            string extractionState = JsonNamingPolicy.KebabCaseLower.ConvertName(proposalState.ToString());

            ValidateAdmittedSourceArtifactPayload(artifactId);
            byte[] inputBytes = ReadRetainedPayloadBytes(inputPayloadId)
                ?? throw new InvalidDataException("The retained WP11 input payload bytes are unavailable.");
            if (inputBytes.LongLength != inputLength
                || Convert.ToHexStringLower(SHA256.HashData(inputBytes)) != inputSha)
            {
                throw new InvalidDataException("The retained WP11 input payload identity drifted.");
            }
            RetainedProviderReplayProvenance sourceProvider = ReadRetainedProviderReplayProvenance(sourceOperationId);
            RetainedProviderReplayProvenance candidateProvider = ReadRetainedProviderReplayProvenance(candidateOperationId);
            if (sourceProvider.SemanticResponseRecordId != sourceResponseRecordId
                || candidateProvider.SemanticResponseRecordId != candidateResponseRecordId
                || candidateProvider.RawResponseSha256 != candidateResponseFingerprint)
            {
                throw new InvalidDataException("The retained successor provider response provenance drifted from the semantic chain.");
            }
            if (applicability != "not-evaluated" || hostDecision != "abstained")
            {
                throw new InvalidDataException("Migrated historical Slice 6 evidence unexpectedly acquired current applicability authority.");
            }
            bool applies = slice7SubjectIds.Contains(rootSubject, StringComparer.Ordinal);
            return new RetainedSourceDecisionReplayResult(
                acquisitionRunId,
                parentAnalysisRunId,
                installationSnapshotId,
                analysisContextId,
                proposalId,
                extractionState,
                sourceRevisionId,
                passageId,
                admittedArtifactSha256,
                applicationLinkId,
                "historical-audit-only",
                support,
                applicability,
                hostDecision,
                rootSubject,
                artifactId,
                [],
                candidateOutcomeId,
                inputPayloadId,
                inputSha,
                inputLength,
                candidateTranscriptId,
                candidateTranscriptPayloadId,
                candidateResultPayloadId,
                candidateResponseFingerprint,
                sourceProvider,
                candidateProvider,
                true,
                false,
                applies,
                applies
                    ? "The retained historical source identity exactly matches a Slice 7 subject, but remains non-authorizing audit provenance."
                    : "The retained historical source identity belongs to a different root and remains non-authorizing audit provenance; it is not applicable to the Slice 7 synthetic subjects.");
        }
    }

    private RetainedProviderReplayProvenance ReadRetainedProviderReplayProvenance(string semanticOperationId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT operation.operation_id,operation.authorization_id,
              operation.semantic_authorization_id,binding.semantic_response_record_id,
              response.response_record_id,response.raw_response_payload_id,response.raw_response_sha256,
              response.raw_response_bytes,operation.canonical_request_sha256,response.returned_model,
              response.usage_entry_id,response.usage_available,response.input_tokens,response.output_tokens,
              response.total_tokens,response.calculated_nano_usd
            FROM m1_slice6_successor_v6_semantic_response_bindings binding
            JOIN m1_slice6_successor_v6_operations operation
              ON operation.operation_id=binding.transport_operation_id
             AND operation.authorization_id=binding.transport_authorization_id
             AND operation.semantic_operation_id=binding.semantic_operation_id
             AND operation.semantic_authorization_id=binding.semantic_authorization_id
            JOIN m1_slice6_successor_v6_responses response
              ON response.operation_id=operation.operation_id
             AND response.authorization_id=operation.authorization_id
             AND response.response_record_id=binding.transport_response_record_id
            WHERE binding.semantic_operation_id=$operation;
            """;
        command.Parameters.AddWithValue("$operation", semanticOperationId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidDataException("The retained semantic operation has no exact successor response binding.");
        }
        string transportOperationId = reader.GetString(0);
        string transportAuthorizationId = reader.GetString(1);
        string semanticAuthorizationId = reader.GetString(2);
        string semanticResponseRecordId = reader.GetString(3);
        string transportResponseRecordId = reader.GetString(4);
        string rawResponsePayloadId = reader.GetString(5);
        string rawResponseSha256 = reader.GetString(6);
        long rawResponseByteLength = reader.GetInt64(7);
        string canonicalRequestSha256 = reader.GetString(8);
        string? returnedModel = reader.IsDBNull(9) ? null : reader.GetString(9);
        string usageEntryId = reader.GetString(10);
        bool usageAvailable = reader.GetInt64(11) == 1;
        long? inputTokens = usageAvailable ? reader.GetInt64(12) : null;
        long? outputTokens = usageAvailable ? reader.GetInt64(13) : null;
        long? totalTokens = usageAvailable ? reader.GetInt64(14) : null;
        long? calculatedNanoUsd = usageAvailable ? reader.GetInt64(15) : null;
        if (reader.Read())
        {
            throw new InvalidDataException("The retained semantic successor response binding is ambiguous.");
        }
        reader.Close();
        byte[] rawResponse = ReadRetainedPayloadBytes(rawResponsePayloadId)
            ?? throw new InvalidDataException("The retained successor raw response bytes are unavailable.");
        if (rawResponse.LongLength != rawResponseByteLength
            || Convert.ToHexStringLower(SHA256.HashData(rawResponse)) != rawResponseSha256)
        {
            throw new InvalidDataException("The retained successor raw response identity drifted.");
        }
        string availability = usageAvailable ? "available" : "unavailable";
        return new RetainedProviderReplayProvenance(
            transportOperationId,
            transportAuthorizationId,
            semanticOperationId,
            semanticAuthorizationId,
            transportResponseRecordId,
            semanticResponseRecordId,
            rawResponsePayloadId,
            rawResponseSha256,
            rawResponseByteLength,
            canonicalRequestSha256,
            "external-retained-artifact",
            null,
            returnedModel,
            usageEntryId,
            availability,
            availability,
            inputTokens,
            availability,
            outputTokens,
            availability,
            totalTokens,
            availability,
            calculatedNanoUsd);
    }

}
