using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed record SourceClaimAcquisitionRegistration(
    string AcquisitionRunId,
    string InstallationSnapshotId,
    string AnalysisContextId,
    string EffectiveConfigurationId,
    string ResolvedInputManifestId,
    string ParentAnalysisRunId,
    string ApplicationScopeId,
    string CostAttributionScopeId,
    string JobNodeId,
    string CommandId,
    string ParentLinkId,
    string SourceRevisionId,
    DateTimeOffset OccurredAt);

public sealed record SourceClaimPersistenceRequest(
    SourceClaimExtractionDocument Document,
    string AuthorizationId,
    string ResponseRecordId,
    string ProviderAttemptId,
    string RequestId,
    string DispatchFenceId,
    DateTimeOffset OccurredAt);

public sealed record SourceClaimPersistenceReceipt(
    string AcquisitionRunId,
    string OperationId,
    string SourceRevisionId,
    int ProposalCount,
    int AdmissionCount,
    IReadOnlyList<string> AdmissionCorrelationIds);

public sealed record SourceClaimConsumptionRequest(
    string ApplicationLinkId,
    string AcquisitionRunId,
    string AdmissionId,
    string ParentAnalysisRunId,
    string ApplicationScopeId,
    string CostAttributionScopeId,
    DateTimeOffset OccurredAt);

public sealed record SourceClaimConsumptionReceipt(
    string ApplicationLinkId,
    string AcquisitionRunId,
    string AdmissionId,
    string AdmittedArtifactId);

public sealed record SourceClaimApplicationReadModel(
    string ApplicationLinkId,
    string AcquisitionRunId,
    string AdmissionId,
    string ParentAnalysisRunId,
    string ApplicationScopeId,
    string CostAttributionScopeId,
    string AdmittedArtifactId);

public partial class AuthoritativeStore
{
    public void RegisterSourceClaimAcquisition(SourceClaimAcquisitionRegistration request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            Execute(
                """
                INSERT OR IGNORE INTO evidence_acquisition_runs VALUES(
                  $acquisition,$snapshot,$context,$configuration,$manifest,$run,$scope,$cost,'running',$now);
                INSERT INTO evidence_acquisition_job_nodes VALUES($job,$acquisition,'source-claim-extraction','running',$now);
                INSERT INTO evidence_acquisition_commands VALUES($command,$acquisition,'provider-operation',$now,'recorded');
                INSERT INTO provider_command_bindings VALUES($command,'evidence-acquisition-run',$acquisition,$now);
                INSERT INTO evidence_acquisition_parent_links VALUES($parent,$acquisition,$run,'initiated-by',NULL,$now);
                """,
                transaction,
                ("$acquisition", request.AcquisitionRunId), ("$snapshot", request.InstallationSnapshotId),
                ("$context", request.AnalysisContextId), ("$configuration", request.EffectiveConfigurationId),
                ("$manifest", request.ResolvedInputManifestId), ("$run", request.ParentAnalysisRunId),
                ("$scope", request.ApplicationScopeId), ("$cost", request.CostAttributionScopeId),
                ("$job", request.JobNodeId), ("$command", request.CommandId), ("$parent", request.ParentLinkId),
                ("$now", ToText(request.OccurredAt)));
            using (SqliteCommand acquisition = connection.CreateCommand())
            {
                acquisition.Transaction = transaction;
                acquisition.CommandText =
                    """
                    SELECT 1 FROM evidence_acquisition_runs
                    WHERE acquisition_run_id=$acquisition AND installation_snapshot_id=$snapshot
                      AND analysis_context_id=$context AND effective_configuration_id=$configuration
                      AND resolved_input_manifest_id=$manifest AND parent_analysis_run_id=$run
                      AND application_scope_id=$scope AND cost_attribution_scope_id=$cost;
                    """;
                acquisition.Parameters.AddWithValue("$acquisition", request.AcquisitionRunId);
                acquisition.Parameters.AddWithValue("$snapshot", request.InstallationSnapshotId);
                acquisition.Parameters.AddWithValue("$context", request.AnalysisContextId);
                acquisition.Parameters.AddWithValue("$configuration", request.EffectiveConfigurationId);
                acquisition.Parameters.AddWithValue("$manifest", request.ResolvedInputManifestId);
                acquisition.Parameters.AddWithValue("$run", request.ParentAnalysisRunId);
                acquisition.Parameters.AddWithValue("$scope", request.ApplicationScopeId);
                acquisition.Parameters.AddWithValue("$cost", request.CostAttributionScopeId);
                if (acquisition.ExecuteScalar() is null)
                {
                    throw new InvalidDataException("Source-claim acquisition registration contradicts the retained acquisition root.");
                }
            }
            using SqliteCommand source = connection.CreateCommand();
            source.Transaction = transaction;
            source.CommandText = "SELECT 1 FROM documentation_revisions WHERE documentation_revision_id=$revision;";
            source.Parameters.AddWithValue("$revision", request.SourceRevisionId);
            if (source.ExecuteScalar() is null)
            {
                throw new InvalidOperationException("Source-claim acquisition requires an exact retained documentation revision.");
            }
            transaction.Commit();
        }
    }

    public SourceClaimConsumptionReceipt ConsumeAdmittedSourceClaim(SourceClaimConsumptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            using SqliteCommand admitted = connection.CreateCommand();
            admitted.Transaction = transaction;
            admitted.CommandText =
                """
                SELECT admission.admitted_artifact_id
                FROM provider_semantic_admissions admission
                JOIN evidence_acquisition_runs acquisition
                  ON acquisition.acquisition_run_id=admission.owner_id
                WHERE admission.admission_id=$admission
                  AND admission.owner_kind='evidence-acquisition-run'
                  AND admission.owner_id=$acquisition
                  AND admission.state='admitted'
                  AND admission.admitted_artifact_id IS NOT NULL
                  AND acquisition.parent_analysis_run_id=$run
                  AND acquisition.application_scope_id=$scope
                  AND acquisition.cost_attribution_scope_id=$cost
                  AND admission.created_at <= $now;
                """;
            admitted.Parameters.AddWithValue("$admission", request.AdmissionId);
            admitted.Parameters.AddWithValue("$acquisition", request.AcquisitionRunId);
            admitted.Parameters.AddWithValue("$run", request.ParentAnalysisRunId);
            admitted.Parameters.AddWithValue("$scope", request.ApplicationScopeId);
            admitted.Parameters.AddWithValue("$cost", request.CostAttributionScopeId);
            admitted.Parameters.AddWithValue("$now", ToText(request.OccurredAt));
            string admittedArtifactId = admitted.ExecuteScalar() as string
                ?? throw new InvalidDataException(
                    "Source-claim consumption requires an exact admitted artifact owned by the acquisition.");
            Execute(
                """
                INSERT INTO evidence_acquisition_application_links VALUES(
                  $application,$acquisition,$admission,$run,$scope,$cost,$artifact,$now);
                """,
                transaction,
                ("$application", request.ApplicationLinkId), ("$acquisition", request.AcquisitionRunId),
                ("$admission", request.AdmissionId),
                ("$run", request.ParentAnalysisRunId), ("$scope", request.ApplicationScopeId),
                ("$cost", request.CostAttributionScopeId), ("$artifact", admittedArtifactId),
                ("$now", ToText(request.OccurredAt)));
            transaction.Commit();
            return new(request.ApplicationLinkId, request.AcquisitionRunId, request.AdmissionId, admittedArtifactId);
        }
    }

    public IReadOnlyList<SourceClaimApplicationReadModel> ReadSourceClaimApplicationLinks(string acquisitionRunId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acquisitionRunId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT application_link_id,acquisition_run_id,admission_id,analysis_run_id,application_scope_id,
                  cost_attribution_scope_id,admitted_artifact_id
                FROM evidence_acquisition_application_links
                WHERE acquisition_run_id=$acquisition
                ORDER BY application_link_id;
                """;
            command.Parameters.AddWithValue("$acquisition", acquisitionRunId);
            using SqliteDataReader reader = command.ExecuteReader();
            List<SourceClaimApplicationReadModel> results = [];
            while (reader.Read())
            {
                results.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.GetString(6)));
            }
            return results;
        }
    }

    public SourceClaimPersistenceReceipt PersistSourceClaimExtraction(SourceClaimPersistenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ProviderOperationContractInvariants.Validate(request.Document);
        SourceClaimExtractionDocument document = request.Document;
        if (document.AdmissionCorrelations.Any(link => link.AuthorizationId.Value != request.AuthorizationId
                || link.ResponseRecordId.Value != request.ResponseRecordId))
        {
            throw new InvalidDataException("Source-claim persistence requires exact authorization and response identities on every admission link.");
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            using (SqliteCommand authority = connection.CreateCommand())
            {
                authority.Transaction = transaction;
                authority.CommandText =
                    """
                    SELECT 1 FROM provider_operation_authorizations authorization
                    JOIN provider_responses response
                      ON response.authorization_id=authorization.authorization_id
                     AND response.operation_id=authorization.operation_id
                    WHERE authorization.authorization_id=$authorization
                      AND authorization.operation_id=$operation
                      AND authorization.owner_kind=$owner_kind AND authorization.owner_id=$owner
                      AND authorization.operation_kind='source-claim-extraction'
                      AND response.response_record_id=$response
                      AND response.provider_attempt_id=$attempt AND response.request_id=$request
                      AND response.dispatch_fence_id=$fence;
                    """;
                authority.Parameters.AddWithValue("$authorization", request.AuthorizationId);
                authority.Parameters.AddWithValue("$operation", document.OperationId.Value);
                authority.Parameters.AddWithValue("$owner_kind", document.OwnerKind);
                authority.Parameters.AddWithValue("$owner", document.OwnerId.Value);
                authority.Parameters.AddWithValue("$response", request.ResponseRecordId);
                authority.Parameters.AddWithValue("$attempt", request.ProviderAttemptId);
                authority.Parameters.AddWithValue("$request", request.RequestId);
                authority.Parameters.AddWithValue("$fence", request.DispatchFenceId);
                if (authority.ExecuteScalar() is null)
                {
                    throw new InvalidDataException("Source-claim persistence requires the exact retained authorization and response authority.");
                }
            }
            foreach (SourceClaimAdmissionCorrelationContract link in document.AdmissionCorrelations)
            {
                CitationProposalContract proposal = document.ClaimProposals.Single(x => x.ProposalId == link.ProposalId);
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(document);
                string payloadId = AdmitCoordinatorPayload(
                    payload, "source-claim-extraction", proposal.ProposalId.Value, request.OccurredAt, transaction);
                string kind = proposal.State is ProposalAdmissionState.Abstained ? "abstention"
                    : proposal.State is ProposalAdmissionState.Unsupported or ProposalAdmissionState.Unavailable
                        or ProposalAdmissionState.Deleted ? "gap" : "source-claim";
                Execute(
                    """
                    INSERT INTO provider_semantic_proposals(
                      proposal_id,authorization_id,operation_id,provider_attempt_id,request_id,response_record_id,
                      dispatch_fence_id,owner_kind,owner_id,root_subject_id,semantic_link_id,proposal_kind,
                      payload_id,created_at)
                    VALUES($proposal,$authorization,$operation,$attempt,$request,$response,$fence,$owner_kind,
                      $owner,$root,$correlation,$kind,$payload,$now);
                    INSERT INTO provider_semantic_validations VALUES(
                      $validation,$proposal,$operation,$response,$owner_kind,$owner,$root,$state,
                      'infinium.host.source-claim-admission/v1',$reason,$now);
                    INSERT INTO provider_semantic_admissions VALUES(
                      $admission,$proposal,$operation,$response,$owner_kind,$owner,$root,$validation,$correlation,
                      $state,'infinium.host.source-claim-admission/v1',$reason,$artifact,$now);
                    """,
                    transaction,
                    ("$proposal", proposal.ProposalId.Value), ("$authorization", request.AuthorizationId),
                    ("$operation", document.OperationId.Value), ("$attempt", request.ProviderAttemptId),
                    ("$request", request.RequestId), ("$response", request.ResponseRecordId),
                    ("$fence", request.DispatchFenceId), ("$owner_kind", document.OwnerKind),
                    ("$owner", document.OwnerId.Value), ("$root", document.SourceRevisionId.Value),
                    ("$correlation", link.AdmissionCorrelationId.Value), ("$kind", kind), ("$payload", payloadId),
                    ("$validation", link.ValidationId.Value), ("$state", ToAdmissionState(proposal.State)),
                    ("$reason", proposal.Reason), ("$admission", link.AdmissionId.Value),
                    ("$artifact", proposal.State == ProposalAdmissionState.Admitted ? payloadId : null),
                    ("$now", ToText(request.OccurredAt)));
            }
            transaction.Commit();
            return new(document.AcquisitionRunId.Value, document.OperationId.Value, document.SourceRevisionId.Value,
                document.ClaimProposals.Count, document.AdmissionCorrelations.Count,
                document.AdmissionCorrelationIds.Select(x => x.Value).ToArray());
        }
    }

    public IReadOnlyList<ProviderSemanticAdmissionReadModel> ReadSourceClaimAdmissions(string acquisitionRunId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acquisitionRunId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT admission.admission_id,admission.proposal_id,admission.operation_id,
                  admission.response_record_id,admission.root_subject_id,admission.validation_id,
                  admission.semantic_link_id,admission.state,admission.reason,proposal.payload_id
                FROM provider_semantic_admissions admission
                JOIN provider_semantic_proposals proposal ON proposal.proposal_id=admission.proposal_id
                WHERE admission.owner_kind='evidence-acquisition-run' AND admission.owner_id=$owner
                ORDER BY admission.admission_id;
                """;
            command.Parameters.AddWithValue("$owner", acquisitionRunId);
            using SqliteDataReader reader = command.ExecuteReader();
            List<ProviderSemanticAdmissionReadModel> results = [];
            while (reader.Read())
            {
                results.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                    reader.GetString(8), reader.GetString(9)));
            }
            return results;
        }
    }

    public SourceClaimExtractionDocument ReadSourceClaimExtraction(string acquisitionRunId, string admissionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acquisitionRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(admissionId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT proposal.payload_id FROM provider_semantic_admissions admission
                JOIN provider_semantic_proposals proposal ON proposal.proposal_id=admission.proposal_id
                WHERE admission.admission_id=$admission AND admission.owner_kind='evidence-acquisition-run'
                  AND admission.owner_id=$owner;
                """;
            command.Parameters.AddWithValue("$admission", admissionId);
            command.Parameters.AddWithValue("$owner", acquisitionRunId);
            string payloadId = command.ExecuteScalar() as string
                ?? throw new KeyNotFoundException("The exact source-claim extraction admission does not exist.");
            byte[] bytes = ReadRetainedPayloadBytes(payloadId)
                ?? throw new InvalidDataException("The retained source-claim extraction payload is unavailable.");
            SourceClaimExtractionDocument document = JsonSerializer.Deserialize<SourceClaimExtractionDocument>(bytes)
                ?? throw new InvalidDataException("The retained source-claim extraction payload is invalid.");
            ProviderOperationContractInvariants.Validate(document);
            return document;
        }
    }

    private static string ToAdmissionState(ProposalAdmissionState state) => state switch
    {
        ProposalAdmissionState.Admitted => "admitted",
        ProposalAdmissionState.Rejected => "rejected",
        ProposalAdmissionState.Abstained => "abstained",
        ProposalAdmissionState.Unavailable => "unavailable",
        ProposalAdmissionState.Unsupported => "unsupported",
        ProposalAdmissionState.Deleted => "deleted",
        _ => throw new InvalidDataException("Source-claim persistence requires an explicit terminal admission state."),
    };

    public CandidateInvestigationPersistenceReceipt PersistCandidateInvestigation(
        CandidateInvestigationPersistenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ProviderOperationContractInvariants.Validate(request.Document);
        CandidateInvestigationDocument document = request.Document;
        CandidateHostContextSnapshot hostContext = ValidateCandidatePersistenceRequest(request);
        if (document.AdmissionLinks.Any(link => link.AuthorizationId.Value != request.AuthorizationId
                || link.ResponseRecordId.Value != request.ResponseRecordId)
            || request.ResponseRecordId is null && document.HypothesisProposals.Count != 0)
        {
            throw new InvalidDataException("Candidate investigation requires exact authorization and response identities.");
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            using (SqliteCommand authority = connection.CreateCommand())
            {
                authority.Transaction = transaction;
                authority.CommandText =
                    """
                    SELECT 1 FROM provider_operation_authorizations authorization
                    JOIN analysis_candidates candidate
                      ON candidate.candidate_id=$candidate AND candidate.run_id=authorization.owner_id
                    WHERE authorization.authorization_id=$authorization
                      AND authorization.operation_id=$operation
                      AND authorization.owner_kind='analysis-run' AND authorization.owner_id=$owner
                      AND authorization.operation_kind='candidate-investigation'
                      AND authorization.prompt_id=$prompt
                      AND authorization.prompt_fingerprint=$prompt_fingerprint
                      AND ($response IS NULL OR EXISTS(
                        SELECT 1 FROM provider_responses response
                        WHERE response.authorization_id=authorization.authorization_id
                          AND response.operation_id=authorization.operation_id
                          AND response.response_record_id=$response
                          AND response.provider_attempt_id=$attempt AND response.request_id=$request
                          AND response.dispatch_fence_id=$fence));
                    """;
                authority.Parameters.AddWithValue("$candidate", document.CandidateId.Value);
                authority.Parameters.AddWithValue("$authorization", request.AuthorizationId);
                authority.Parameters.AddWithValue("$operation", document.OperationId.Value);
                authority.Parameters.AddWithValue("$owner", document.OwnerId.Value);
                authority.Parameters.AddWithValue("$prompt", hostContext.PromptId);
                authority.Parameters.AddWithValue("$prompt_fingerprint", hostContext.PromptFingerprint);
                authority.Parameters.AddWithValue("$response", (object?)request.ResponseRecordId ?? DBNull.Value);
                authority.Parameters.AddWithValue("$attempt", (object?)request.ProviderAttemptId ?? DBNull.Value);
                authority.Parameters.AddWithValue("$request", (object?)request.RequestId ?? DBNull.Value);
                authority.Parameters.AddWithValue("$fence", (object?)request.DispatchFenceId ?? DBNull.Value);
                if (authority.ExecuteScalar() is null)
                {
                    throw new InvalidDataException("Candidate persistence requires exact retained analysis, authorization, response, and candidate authority.");
                }
            }
            ValidateCandidateHostContext(request, hostContext, transaction);
            foreach (CandidateEvidenceProvenanceBinding binding in request.EvidenceBindings)
            {
                ValidateCandidateEvidenceBinding(request, binding, transaction);
            }
            string inputPayloadId = AdmitCoordinatorPayload(
                request.InputPayload, "candidate-investigation-input", request.OutcomeId, request.OccurredAt, transaction);
            string transcriptPayloadId = AdmitCoordinatorPayload(
                request.TranscriptPayload, "candidate-investigation-transcript", request.OutcomeId, request.OccurredAt, transaction);
            byte[] documentPayload = JsonSerializer.SerializeToUtf8Bytes(document);
            if (!documentPayload.AsSpan().SequenceEqual(request.ResultPayload))
            {
                throw new InvalidDataException("Candidate outcome payload is not the exact canonical retained investigation document.");
            }
            string resultPayloadId = AdmitCoordinatorPayload(
                request.ResultPayload, "candidate-investigation-outcome", request.OutcomeId, request.OccurredAt, transaction);
            Execute(
                """
                INSERT INTO candidate_investigation_outcomes VALUES(
                  $outcome,$authorization,$operation,$owner,$candidate,$hypothesis,$context,$transcript,$response,$fingerprint,
                  $transcript_state,$disposition,$replay_state,$input_payload,$transcript_payload,$result_payload,$now);
                """,
                transaction,
                ("$outcome", request.OutcomeId), ("$authorization", request.AuthorizationId),
                ("$operation", document.OperationId.Value), ("$owner", document.OwnerId.Value),
                ("$candidate", document.CandidateId.Value), ("$hypothesis", request.HypothesisId),
                ("$context", request.ContextId),
                ("$transcript", request.TranscriptId), ("$response", request.ResponseRecordId),
                ("$fingerprint", request.ResponseFingerprint), ("$transcript_state", request.TranscriptState),
                ("$disposition", request.Disposition), ("$replay_state", request.ReplayState),
                ("$input_payload", inputPayloadId), ("$transcript_payload", transcriptPayloadId),
                ("$result_payload", resultPayloadId), ("$now", ToText(request.OccurredAt)));
            foreach (ProviderSemanticAdmissionLinkContract link in document.AdmissionLinks)
            {
                HypothesisProposalContract proposal = document.HypothesisProposals.Single(x => x.ProposalId == link.ProposalId);
                string payloadId = resultPayloadId;
                string kind = proposal.State is ProposalAdmissionState.Abstained ? "abstention"
                    : proposal.State is ProposalAdmissionState.Unsupported or ProposalAdmissionState.Unavailable
                        or ProposalAdmissionState.Deleted ? "gap" : "candidate-hypothesis";
                Execute(
                    """
                    INSERT INTO provider_semantic_proposals(
                      proposal_id,authorization_id,operation_id,provider_attempt_id,request_id,response_record_id,
                      dispatch_fence_id,owner_kind,owner_id,root_subject_id,semantic_link_id,proposal_kind,
                      payload_id,created_at)
                    VALUES($proposal,$authorization,$operation,$attempt,$request,$response,$fence,'analysis-run',
                      $owner,$candidate,$application,$kind,$payload,$now);
                    INSERT INTO provider_semantic_validations VALUES(
                      $validation,$proposal,$operation,$response,'analysis-run',$owner,$candidate,$state,
                      'infinium.host.candidate-investigation-admission/v1',$reason,$now);
                    INSERT INTO provider_semantic_admissions VALUES(
                      $admission,$proposal,$operation,$response,'analysis-run',$owner,$candidate,$validation,$application,
                      $state,'infinium.host.candidate-investigation-admission/v1',$reason,$artifact,$now);
                    """,
                    transaction,
                    ("$proposal", proposal.ProposalId.Value), ("$authorization", request.AuthorizationId),
                    ("$operation", document.OperationId.Value), ("$attempt", request.ProviderAttemptId),
                    ("$request", request.RequestId), ("$response", request.ResponseRecordId),
                    ("$fence", request.DispatchFenceId), ("$owner", document.OwnerId.Value),
                    ("$candidate", document.CandidateId.Value), ("$application", link.ApplicationLinkId.Value),
                    ("$kind", kind), ("$payload", payloadId), ("$validation", link.ValidationId.Value),
                    ("$state", ToAdmissionState(proposal.State)), ("$reason", proposal.Reason),
                    ("$admission", link.AdmissionId.Value),
                    ("$artifact", proposal.State == ProposalAdmissionState.Admitted ? payloadId : null),
                    ("$now", ToText(request.OccurredAt)));
            }
            transaction.Commit();
            return new(document.AnalysisRunId.Value, document.OperationId.Value, document.CandidateId.Value,
                document.HypothesisProposals.Count, document.AdmissionLinks.Count,
                document.AdmissionLinkIds.Select(x => x.Value).ToArray(), request.OutcomeId);
        }
    }

    private static CandidateHostContextSnapshot ValidateCandidatePersistenceRequest(
        CandidateInvestigationPersistenceRequest request)
    {
        string?[] responseEnvelope =
        [request.ResponseRecordId, request.ProviderAttemptId, request.RequestId, request.DispatchFenceId];
        bool allAbsent = responseEnvelope.All(value => value is null);
        bool allPresent = responseEnvelope.All(value => !string.IsNullOrWhiteSpace(value));
        if (!allAbsent && !allPresent)
        {
            throw new InvalidDataException(
                "Candidate persistence requires an all-present retained response envelope or an all-absent no-model envelope.");
        }
        CandidateInvestigationDocument document = request.Document;
        if (request.EvidenceBindings.Count == 0
            || request.EvidenceBindings.Select(x => x.EvidenceId).Distinct(StringComparer.Ordinal).Count()
                != request.EvidenceBindings.Count
            || !document.EvidenceIds.Select(x => x.Value).ToHashSet(StringComparer.Ordinal)
                .SetEquals(request.EvidenceBindings.Select(x => x.EvidenceId)))
        {
            throw new InvalidDataException("Candidate persistence requires one unique exact provenance binding for every retained evidence identity.");
        }
        byte[] exactDocument = JsonSerializer.SerializeToUtf8Bytes(document);
        if (!exactDocument.AsSpan().SequenceEqual(request.ResultPayload))
        {
            throw new InvalidDataException("Candidate outcome payload is not the exact canonical retained investigation document.");
        }
        using JsonDocument input = JsonDocument.Parse(request.InputPayload, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 64,
        });
        JsonElement root = input.RootElement;
        ValidateCandidateInputShape(root);
        JsonElement[] contexts = root.GetProperty("contexts").EnumerateArray()
            .Where(x => Text(x, "context_id") == request.ContextId).ToArray();
        if (contexts.Length != 1
            || Text(root, "operation_id") != document.OperationId.Value
            || Text(root, "owner_id") != document.OwnerId.Value
            || Text(root, "analysis_run_id") != document.AnalysisRunId.Value)
        {
            throw new InvalidDataException("Candidate persistence input does not bind one exact retained host context.");
        }
        JsonElement context = contexts[0];
        string[] participantIds = Strings(context, "participant_ids");
        string[] participantRoles = Strings(context, "participant_roles");
        string[] causalPathIds = Strings(context, "causal_path_ids");
        CandidateHostEvidenceSnapshot[] evidence = context.GetProperty("evidence").EnumerateArray()
            .Select(item => new CandidateHostEvidenceSnapshot(
                Text(item, "evidence_id"), Text(item, "evidence_application_link_id"),
                Text(item, "source_acquisition_id"), Text(item, "source_admission_id"),
                Text(item, "source_application_link_id"), Text(item, "source_revision_id"),
                Text(item, "passage_id"), Text(item, "relationship"), Text(item, "availability"),
                Text(item, "content_sha256"))).ToArray();
        if (Text(context, "candidate_id") != document.CandidateId.Value
            || Text(context, "hypothesis_id") != request.HypothesisId
            || !participantIds.SequenceEqual(document.ParticipantIds.Select(x => x.Value), StringComparer.Ordinal)
            || !participantRoles.SequenceEqual(document.ParticipantRoles, StringComparer.Ordinal)
            || !causalPathIds.SequenceEqual(document.CausalPathIds.Select(x => x.Value), StringComparer.Ordinal)
            || Text(context, "dependency_closure_id") != document.DependencyClosureId.Value
            || !evidence.Select(x => x.EvidenceId).SequenceEqual(document.EvidenceIds.Select(x => x.Value), StringComparer.Ordinal)
            || evidence.Length != request.EvidenceBindings.Count)
        {
            throw new InvalidDataException("Candidate persistence document drifts from its exact retained host context.");
        }
        foreach (CandidateEvidenceProvenanceBinding binding in request.EvidenceBindings)
        {
            CandidateHostEvidenceSnapshot expected = evidence.Single(x => x.EvidenceId == binding.EvidenceId);
            if (binding != new CandidateEvidenceProvenanceBinding(
                    expected.EvidenceId, expected.EvidenceApplicationLinkId, expected.SourceAcquisitionId,
                    expected.SourceAdmissionId, expected.SourceApplicationLinkId, expected.SourceRevisionId,
                    expected.PassageId, expected.Relationship, expected.Availability, expected.ContentSha256))
            {
                throw new InvalidDataException("Candidate evidence binding drifts from its exact retained input provenance.");
            }
        }
        ValidateCandidateTranscriptPayload(request, root, context);
        return new(request.HypothesisId, Text(context, "hypothesis"), participantIds, participantRoles,
            causalPathIds, Text(context, "dependency_closure_id"), evidence,
            Text(root, "prompt_id"), Text(root, "prompt_fingerprint"));
    }

    private static void ValidateCandidateInputShape(JsonElement input)
    {
        string[] exactRootProperties = ["schema_id", "schema_version", "package_id", "operation_id",
            "host_authorization_id", "owner_kind", "owner_id", "analysis_run_id", "application_scope_id",
            "cost_attribution_scope_id", "prompt_id", "prompt_fingerprint", "contexts"];
        JsonElement[] contexts = input.GetProperty("contexts").EnumerateArray().ToArray();
        if (!HasExactProperties(input, exactRootProperties)
            || Text(input, "schema_id") != "infinium.llm.candidate-investigation-execution-input/v1"
            || Text(input, "schema_version") != "1"
            || Text(input, "owner_kind") != "analysis-run"
            || Text(input, "owner_id") != Text(input, "analysis_run_id")
            || Text(input, "prompt_id") != "infinium.m1-s6.candidate-investigation-prompt/v1"
            || !IsSha256(Text(input, "prompt_fingerprint"))
            || !Nonempty(Text(input, "package_id")) || !Nonempty(Text(input, "operation_id"))
            || !Nonempty(Text(input, "host_authorization_id")) || !Nonempty(Text(input, "owner_id"))
            || !Nonempty(Text(input, "analysis_run_id")) || !Nonempty(Text(input, "application_scope_id"))
            || !Nonempty(Text(input, "cost_attribution_scope_id"))
            || contexts.Length is < 2 or > 32
            || !Unique(StringsBy(contexts, "context_id"))
            || !Unique(StringsBy(contexts, "candidate_id"))
            || !Unique(StringsBy(contexts, "hypothesis_id")))
        {
            throw new InvalidDataException("Candidate persistence input is not the closed execution-input contract.");
        }
        string[] exactContextProperties = ["context_id", "candidate_id", "hypothesis_id", "hypothesis",
            "participant_ids", "participant_roles", "causal_path_ids", "dependency_closure_id", "evidence"];
        string[] exactEvidenceProperties = ["evidence_id", "evidence_application_link_id", "source_acquisition_id",
            "source_admission_id", "source_application_link_id", "source_revision_id", "passage_id", "relationship",
            "availability", "content_sha256"];
        JsonElement[] allEvidence = contexts.SelectMany(item => item.GetProperty("evidence").EnumerateArray()).ToArray();
        if (contexts.Any(item => !HasExactProperties(item, exactContextProperties)
                || !Nonempty(Text(item, "context_id")) || !Nonempty(Text(item, "candidate_id"))
                || !Nonempty(Text(item, "hypothesis_id")) || !Nonempty(Text(item, "hypothesis"))
                || !Nonempty(Text(item, "dependency_closure_id"))
                || Strings(item, "participant_ids").Length is < 1 or > 32
                || Strings(item, "participant_ids").Length != Strings(item, "participant_roles").Length
                || !Unique(Strings(item, "participant_ids"))
                || Strings(item, "causal_path_ids").Length is < 1 or > 64
                || !Unique(Strings(item, "causal_path_ids"))
                || Strings(item, "participant_roles").Any(role => !Nonempty(role))
                || item.GetProperty("evidence").GetArrayLength() is < 1 or > 64)
            || !Unique(StringsBy(allEvidence, "evidence_id"))
            || allEvidence.Any(item => !HasExactProperties(item, exactEvidenceProperties)
                || exactEvidenceProperties.Take(7).Any(name => !Nonempty(Text(item, name)))
                || Text(item, "relationship") is not ("supporting" or "contradicting" or "neutral")
                || Text(item, "availability") is not ("available" or "deleted" or "unavailable")
                || !IsSha256(Text(item, "content_sha256"))))
        {
            throw new InvalidDataException("Candidate persistence input contains an invalid context or evidence contract.");
        }
    }

    private static void ValidateCandidateTranscriptPayload(
        CandidateInvestigationPersistenceRequest request,
        JsonElement input,
        JsonElement context)
    {
        using JsonDocument transcriptDocument = JsonDocument.Parse(request.TranscriptPayload, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 64,
        });
        JsonElement transcript = transcriptDocument.RootElement;
        ValidateCandidateTranscriptShape(transcript);
        bool modelUsed = Boolean(transcript, "model_used");
        if (Text(input, "host_authorization_id") != request.AuthorizationId
            || Text(input, "application_scope_id") != request.ApplicationScopeId
            || Text(input, "cost_attribution_scope_id") != request.CostAttributionScopeId
            || Text(input, "owner_kind") != request.Document.OwnerKind
            || Text(input, "prompt_id") != Text(transcript, "prompt_id")
            || Text(input, "prompt_fingerprint") != Text(transcript, "prompt_fingerprint")
            || Text(transcript, "transcript_id") != request.TranscriptId
            || Text(transcript, "operation_id") != request.Document.OperationId.Value
            || Text(transcript, "context_id") != request.ContextId
            || Text(transcript, "response_state") != request.TranscriptState
            || Text(transcript, "response_fingerprint") != request.ResponseFingerprint
            || modelUsed != (request.ResponseRecordId is not null)
            || modelUsed && Text(transcript, "response_record_id") != request.ResponseRecordId)
        {
            throw new InvalidDataException("Candidate persistence metadata drifts from its exact retained input or transcript envelope.");
        }

        JsonElement[] transcriptProposals = transcript.GetProperty("proposals").EnumerateArray().ToArray();
        if (request.TranscriptState == "completed")
        {
            if (transcriptProposals.Length != request.Document.HypothesisProposals.Count)
            {
                throw new InvalidDataException("Completed candidate transcript does not bind every retained proposal exactly.");
            }
            foreach (HypothesisProposalContract proposal in request.Document.HypothesisProposals)
            {
                JsonElement retained = ExactObject(transcript.GetProperty("proposals"), "proposal_id", proposal.ProposalId.Value);
                (ProposalAdmissionState State, string Reason, string ApplicationLinkId) expectedProposal =
                    ExpectedCandidateProposal(context, retained);
                ProviderSemanticAdmissionLinkContract admission = request.Document.AdmissionLinks.Single(x =>
                    x.ProposalId == proposal.ProposalId);
                if (Text(retained, "candidate_id") != request.Document.CandidateId.Value
                    || Text(retained, "hypothesis_id") != request.HypothesisId
                    || Text(retained, "hypothesis") != proposal.Hypothesis
                    || !Strings(retained, "supporting_evidence_ids").SequenceEqual(
                        proposal.SupportingEvidenceIds.Select(x => x.Value), StringComparer.Ordinal)
                    || !Strings(retained, "contradicting_evidence_ids").SequenceEqual(
                        proposal.ContradictingEvidenceIds.Select(x => x.Value), StringComparer.Ordinal)
                    || !Strings(retained, "missing_information").SequenceEqual(proposal.MissingInformation, StringComparer.Ordinal)
                    || proposal.State != expectedProposal.State || proposal.Reason != expectedProposal.Reason
                    || admission.ApplicationLinkId.Value != expectedProposal.ApplicationLinkId
                    || !request.Document.ValidationIds.Contains(new("validation-" + proposal.ProposalId.Value))
                    || !request.Document.AdmissionLinkIds.Contains(new("admission-" + proposal.ProposalId.Value)))
                {
                    throw new InvalidDataException("Candidate proposal content drifts from its exact retained transcript.");
                }
            }
            if (request.Document.HypothesisProposals.Count == 0
                    && (Strings(transcript, "abstentions").Length == 0 || Strings(transcript, "gaps").Length == 0)
                || !Strings(transcript, "abstentions").SequenceEqual(request.Document.Abstentions, StringComparer.Ordinal)
                || !Strings(transcript, "gaps").SequenceEqual(request.Document.Gaps, StringComparer.Ordinal))
            {
                throw new InvalidDataException("Candidate abstention or gap content drifts from its exact retained transcript.");
            }
        }
        else if (transcriptProposals.Length != 0 || request.Document.HypothesisProposals.Count != 0
            || request.Document.AdmissionLinks.Count != 0)
        {
            throw new InvalidDataException("Non-completed candidate transcript cannot append semantic proposals or admissions.");
        }
        if (request.TranscriptState != "completed")
        {
            string reason = request.TranscriptState == "not-used" ? "model-not-used"
                : request.TranscriptState == "unavailable" ? "provider-unavailable"
                : "provider-response-" + request.TranscriptState;
            string[] retainedAbstentions = Strings(transcript, "abstentions");
            string[] retainedGaps = Strings(transcript, "gaps");
            string[] expectedAbstentions = retainedAbstentions.Length > 0 ? retainedAbstentions
                : request.TranscriptState == "refusal" ? [reason] : [];
            string[] expectedGaps = retainedGaps.Length > 0 ? retainedGaps : [reason];
            if (!expectedAbstentions.SequenceEqual(request.Document.Abstentions, StringComparer.Ordinal)
                || !expectedGaps.SequenceEqual(request.Document.Gaps, StringComparer.Ordinal))
            {
                throw new InvalidDataException("Candidate terminal abstention or gap content is not derived from its transcript.");
            }
        }

        (string Disposition, string ReplayState) expected = ExpectedCandidateOutcome(
            request.TranscriptState, modelUsed, request.Document, transcriptProposals);
        if (request.Disposition != expected.Disposition || request.ReplayState != expected.ReplayState
            || Text(context, "candidate_id") != request.Document.CandidateId.Value)
        {
            throw new InvalidDataException("Candidate terminal outcome state does not derive from its retained transcript and document.");
        }
    }

    private static void ValidateCandidateTranscriptShape(JsonElement transcript)
    {
        string[] exactTranscriptProperties = ["transcript_id", "operation_id", "context_id", "response_record_id",
            "response_state", "response_fingerprint", "prompt_id", "prompt_fingerprint", "proposals", "abstentions",
            "gaps", "model_used"];
        JsonElement[] proposals = transcript.GetProperty("proposals").EnumerateArray().ToArray();
        string[] exactProposalProperties = ["proposal_id", "candidate_id", "hypothesis_id", "hypothesis",
            "supporting_evidence_ids", "contradicting_evidence_ids", "missing_information", "authority_category",
            "state", "reason"];
        if (!HasExactProperties(transcript, exactTranscriptProperties)
            || Text(transcript, "prompt_id") != "infinium.m1-s6.candidate-investigation-prompt/v1"
            || Text(transcript, "response_state") is not ("completed" or "malformed" or "refusal" or "incomplete"
                or "drift" or "not-used" or "unavailable")
            || !IsSha256(Text(transcript, "response_fingerprint"))
            || !IsSha256(Text(transcript, "prompt_fingerprint"))
            || !Nonempty(Text(transcript, "transcript_id")) || !Nonempty(Text(transcript, "operation_id"))
            || !Nonempty(Text(transcript, "context_id")) || !Nonempty(Text(transcript, "response_record_id"))
            || Strings(transcript, "abstentions").Any(value => !Nonempty(value))
            || Strings(transcript, "gaps").Any(value => !Nonempty(value))
            || Strings(transcript, "abstentions").Length > 64 || Strings(transcript, "gaps").Length > 64
            || proposals.Length > 64 || !Unique(StringsBy(proposals, "proposal_id"))
            || proposals.Any(item => !HasExactProperties(item, exactProposalProperties)
                || !Nonempty(Text(item, "proposal_id")) || !Nonempty(Text(item, "candidate_id"))
                || !Nonempty(Text(item, "hypothesis_id")) || !Nonempty(Text(item, "hypothesis"))
                || !Nonempty(Text(item, "reason"))
                || Text(item, "authority_category") is not ("informational" or "protected-effect-request")
                || Text(item, "state") is not ("proposed" or "unsupported" or "abstained" or "unavailable")
                || Strings(item, "supporting_evidence_ids").Length > 64
                || Strings(item, "contradicting_evidence_ids").Length > 64
                || Strings(item, "missing_information").Length > 64
                || !UniqueAllowEmpty(Strings(item, "supporting_evidence_ids"))
                || !UniqueAllowEmpty(Strings(item, "contradicting_evidence_ids"))
                || Strings(item, "missing_information").Any(value => !Nonempty(value))))
        {
            throw new InvalidDataException("Candidate persistence transcript is not the closed retained-transcript contract.");
        }
    }

    private static (ProposalAdmissionState State, string Reason, string ApplicationLinkId) ExpectedCandidateProposal(
        JsonElement context,
        JsonElement proposal)
    {
        Dictionary<string, JsonElement> evidence = context.GetProperty("evidence").EnumerateArray()
            .ToDictionary(item => Text(item, "evidence_id"), StringComparer.Ordinal);
        string[] supporting = Strings(proposal, "supporting_evidence_ids");
        string[] contradicting = Strings(proposal, "contradicting_evidence_ids");
        string[] referencedIds = supporting.Concat(contradicting).ToArray();
        string applicationLinkId = referencedIds.Where(evidence.ContainsKey)
            .Select(id => Text(evidence[id], "evidence_application_link_id")).FirstOrDefault()
            ?? Text(context.GetProperty("evidence")[0], "evidence_application_link_id");
        if (string.IsNullOrWhiteSpace(Text(proposal, "proposal_id"))
            || Text(proposal, "candidate_id") != Text(context, "candidate_id")
            || Text(proposal, "hypothesis_id") != Text(context, "hypothesis_id")
            || Text(proposal, "hypothesis") != Text(context, "hypothesis")
            || supporting.Intersect(contradicting, StringComparer.Ordinal).Any()
            || referencedIds.Any(id => !evidence.ContainsKey(id)))
        {
            return (ProposalAdmissionState.Rejected, "candidate-hypothesis-or-evidence-identity-rejected", applicationLinkId);
        }
        if (Text(proposal, "authority_category") == "protected-effect-request")
        {
            return (ProposalAdmissionState.Rejected, "model-proposed-forbidden-authority", applicationLinkId);
        }
        if (Text(proposal, "authority_category") != "informational")
        {
            return (ProposalAdmissionState.Rejected, "unknown-authority-category", applicationLinkId);
        }
        if (supporting.Any(id => Text(evidence[id], "relationship") != "supporting")
            || contradicting.Any(id => Text(evidence[id], "relationship") != "contradicting"))
        {
            return (ProposalAdmissionState.Rejected, "evidence-relationship-mismatch", applicationLinkId);
        }
        JsonElement[] referenced = referencedIds.Select(id => evidence[id]).ToArray();
        if (referenced.Any(item => Text(item, "availability") == "deleted"))
        {
            return (ProposalAdmissionState.Deleted, "referenced-evidence-deleted", applicationLinkId);
        }
        if (referenced.Any(item => Text(item, "availability") == "unavailable")
            || Text(proposal, "state") == "unavailable")
        {
            return (ProposalAdmissionState.Unavailable, "referenced-evidence-unavailable", applicationLinkId);
        }
        if (Text(proposal, "state") == "unsupported")
        {
            return (ProposalAdmissionState.Unsupported, "proposal-declared-unsupported", applicationLinkId);
        }
        if (Text(proposal, "state") == "abstained" || contradicting.Length > 0)
        {
            return (ProposalAdmissionState.Abstained,
                contradicting.Length > 0 ? "contradicting-evidence-requires-abstention" : "proposal-declared-abstained",
                applicationLinkId);
        }
        if (Text(proposal, "state") != "proposed")
        {
            return (ProposalAdmissionState.Rejected, "unknown-proposal-state", applicationLinkId);
        }
        if (supporting.Length == 0)
        {
            return (ProposalAdmissionState.Rejected, "supporting-evidence-absent", applicationLinkId);
        }
        string[] knownContradictions = evidence.Values
            .Where(item => Text(item, "relationship") == "contradicting" && Text(item, "availability") == "available")
            .Select(item => Text(item, "evidence_id")).ToArray();
        if (knownContradictions.Except(contradicting, StringComparer.Ordinal).Any())
        {
            return (ProposalAdmissionState.Abstained, "known-contradiction-omitted", applicationLinkId);
        }
        return (ProposalAdmissionState.Admitted, "exact-candidate-hypothesis-evidence-links-admitted", applicationLinkId);
    }

    private static (string Disposition, string ReplayState) ExpectedCandidateOutcome(
        string transcriptState,
        bool modelUsed,
        CandidateInvestigationDocument document,
        IReadOnlyList<JsonElement> transcriptProposals)
    {
        if (!modelUsed)
        {
            return transcriptState == "not-used" ? ("not-used", "not-applicable")
                : transcriptState == "unavailable" ? ("unavailable-provider", "unavailable")
                : throw new InvalidDataException("Candidate no-model transcript has an invalid terminal state.");
        }
        if (transcriptState != "completed")
        {
            return transcriptState == "drift" ? ("rejected-identity-drift", "failed-identity-drift")
                : transcriptState is "malformed" or "refusal" or "incomplete"
                    ? ("rejected-" + transcriptState, "retained-response")
                    : throw new InvalidDataException("Candidate response transcript has an invalid terminal state.");
        }
        if (document.HypothesisProposals.Count == 0)
        {
            return ("empty-abstained", "retained-response");
        }
        string disposition = document.HypothesisProposals.Any(x => x.Reason == "model-proposed-forbidden-authority")
            ? "rejected-hostile-authority"
            : document.HypothesisProposals.Any(x => x.State == ProposalAdmissionState.Deleted)
                ? "rejected-deleted-audit-only"
            : document.HypothesisProposals.Any(x => x.State == ProposalAdmissionState.Abstained)
                ? document.HypothesisProposals.Any(x => x.ContradictingEvidenceIds.Count > 0)
                    ? "rejected-contradiction-abstained" : "rejected-explicit-abstention"
            : document.HypothesisProposals.Any(x => x.State == ProposalAdmissionState.Unsupported)
                ? transcriptProposals.Any(x => Text(x, "state") == "proposed")
                    ? "rejected-matched-negative" : "rejected-unsupported"
            : document.HypothesisProposals.Any(x => x.State == ProposalAdmissionState.Unavailable) ? "rejected-unavailable"
            : document.HypothesisProposals.Any(x => x.State == ProposalAdmissionState.Rejected)
                && transcriptProposals.Any(x => Text(x, "state") == "proposed"
                    && Strings(x, "supporting_evidence_ids").Length == 0)
                ? "rejected-matched-negative"
            : document.HypothesisProposals.All(x => x.State == ProposalAdmissionState.Admitted)
                ? document.HypothesisProposals.Any(x => x.MissingInformation.Count > 0)
                    ? "accepted-conditional" : "accepted"
                : "rejected";
        return (disposition, document.HypothesisProposals.Any(x => x.State == ProposalAdmissionState.Deleted)
            ? "audit-only" : "retained-response");
    }

    private void ValidateCandidateHostContext(
        CandidateInvestigationPersistenceRequest request,
        CandidateHostContextSnapshot context,
        SqliteTransaction transaction)
    {
        using SqliteCommand identity = connection.CreateCommand();
        identity.Transaction = transaction;
        identity.CommandText =
            """
            SELECT candidate.candidate_payload_id,hypothesis.hypothesis_payload_id,
                   candidate.candidate_decision_id,candidate.dependency_closure_id,decision.decision_payload_id
            FROM analysis_candidates candidate
            JOIN candidate_decisions decision
              ON decision.candidate_decision_id=candidate.candidate_decision_id
             AND decision.run_id=candidate.run_id
            JOIN analysis_hypotheses hypothesis
              ON hypothesis.hypothesis_id=$hypothesis AND hypothesis.candidate_id=candidate.candidate_id
             AND hypothesis.run_id=candidate.run_id
            WHERE candidate.candidate_id=$candidate AND candidate.run_id=$run;
            """;
        identity.Parameters.AddWithValue("$hypothesis", context.HypothesisId);
        identity.Parameters.AddWithValue("$candidate", request.Document.CandidateId.Value);
        identity.Parameters.AddWithValue("$run", request.Document.AnalysisRunId.Value);
        string payloadId;
        string decisionId;
        using (SqliteDataReader reader = identity.ExecuteReader())
        {
            if (!reader.Read() || reader.GetString(0) != reader.GetString(1)
                || reader.GetString(0) != reader.GetString(4)
                || reader.GetString(3) != context.DependencyClosureId)
            {
                throw new InvalidDataException("Candidate investigation does not bind one exact durable Slice 5 candidate and hypothesis.");
            }
            payloadId = reader.GetString(0);
            decisionId = reader.GetString(2);
            if (reader.Read())
            {
                throw new InvalidDataException("Candidate investigation host identity is ambiguous.");
            }
        }
        byte[] bytes = ReadRetainedPayloadBytes(payloadId)
            ?? throw new InvalidDataException("Candidate investigation durable Slice 5 payload is unavailable.");
        using JsonDocument payload = JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 64,
        });
        JsonElement root = payload.RootElement;
        JsonElement candidate = ExactObject(root.GetProperty("candidates"), "candidate_id", request.Document.CandidateId.Value);
        JsonElement hypothesis = ExactObject(root.GetProperty("hypotheses"), "hypothesis_id", context.HypothesisId);
        JsonElement decision = ExactObject(root.GetProperty("decisions"), "decision_id", decisionId);
        string[] participantIds = decision.GetProperty("participants").EnumerateArray()
            .Select(x => Text(x, "participant_id")).ToArray();
        string[] participantRoles = decision.GetProperty("participants").EnumerateArray()
            .Select(x => Text(x, "role")).ToArray();
        string[] supporting = context.Evidence.Where(x => x.Relationship == "supporting").Select(x => x.EvidenceId).ToArray();
        string[] contradicting = context.Evidence.Where(x => x.Relationship == "contradicting").Select(x => x.EvidenceId).ToArray();
        string[] dependencyIds = Strings(decision, "dependency_ids");
        string[] retainedClosure = root.GetProperty("dependency_edges").EnumerateArray()
            .Where(edge => Text(edge, "from_kind") == "dependency-closure"
                && Text(edge, "from_id") == context.DependencyClosureId
                && Text(edge, "to_kind") == "dependency" && Text(edge, "edge_kind") == "depends-on")
            .Select(edge => Text(edge, "to_id")).ToArray();
        if (Text(root, "originating_run_id") != request.Document.AnalysisRunId.Value
            || Text(candidate, "decision_id") != decisionId
            || Text(candidate, "hypothesis_id") != context.HypothesisId
            || Text(hypothesis, "candidate_id") != request.Document.CandidateId.Value
            || Text(hypothesis, "proposed_explanation") != context.Hypothesis
            || Text(decision, "dependency_closure_id") != context.DependencyClosureId
            || !participantIds.SequenceEqual(context.ParticipantIds, StringComparer.Ordinal)
            || !participantRoles.SequenceEqual(context.ParticipantRoles, StringComparer.Ordinal)
            || !Strings(decision, "path").SequenceEqual(context.CausalPathIds, StringComparer.Ordinal)
            || !Strings(hypothesis, "supporting_evidence_ids").ToHashSet(StringComparer.Ordinal).SetEquals(supporting)
            || !Strings(hypothesis, "contradicting_evidence_ids").ToHashSet(StringComparer.Ordinal).SetEquals(contradicting)
            || !dependencyIds.ToHashSet(StringComparer.Ordinal).SetEquals(retainedClosure))
        {
            throw new InvalidDataException("Candidate investigation context drifts from durable Slice 5 candidate semantics.");
        }
    }

    private static JsonElement ExactObject(JsonElement array, string identityProperty, string identity)
    {
        JsonElement[] matches = array.EnumerateArray().Where(x => Text(x, identityProperty) == identity).ToArray();
        return matches.Length == 1 ? matches[0]
            : throw new InvalidDataException("Durable candidate payload does not contain one exact required identity.");
    }

    private static string Text(JsonElement element, string property) =>
        element.GetProperty(property).GetString()
        ?? throw new InvalidDataException("Retained candidate context contains a null identity or semantic value.");

    private static string[] Strings(JsonElement element, string property) =>
        element.GetProperty(property).EnumerateArray().Select(x => x.GetString()
            ?? throw new InvalidDataException("Retained candidate context contains a null list value.")).ToArray();

    private static bool Boolean(JsonElement element, string property) =>
        element.GetProperty(property).ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException("Retained candidate context contains a non-boolean value."),
        };

    private static bool HasExactProperties(JsonElement element, IReadOnlyCollection<string> names) =>
        element.ValueKind == JsonValueKind.Object
        && element.EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.Ordinal)
            .SetEquals(names);

    private static string[] StringsBy(IEnumerable<JsonElement> elements, string property) =>
        elements.Select(item => Text(item, property)).ToArray();

    private static bool Unique(IEnumerable<string> values)
    {
        string[] items = values.ToArray();
        return items.All(Nonempty) && items.Distinct(StringComparer.Ordinal).Count() == items.Length;
    }

    private static bool UniqueAllowEmpty(IEnumerable<string> values)
    {
        string[] items = values.ToArray();
        return items.All(Nonempty) && items.Distinct(StringComparer.Ordinal).Count() == items.Length;
    }

    private static bool Nonempty(string value) => !string.IsNullOrWhiteSpace(value);

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private sealed record CandidateHostEvidenceSnapshot(
        string EvidenceId,
        string EvidenceApplicationLinkId,
        string SourceAcquisitionId,
        string SourceAdmissionId,
        string SourceApplicationLinkId,
        string SourceRevisionId,
        string PassageId,
        string Relationship,
        string Availability,
        string ContentSha256);

    private sealed record CandidateHostContextSnapshot(
        string HypothesisId,
        string Hypothesis,
        IReadOnlyList<string> ParticipantIds,
        IReadOnlyList<string> ParticipantRoles,
        IReadOnlyList<string> CausalPathIds,
        string DependencyClosureId,
        IReadOnlyList<CandidateHostEvidenceSnapshot> Evidence,
        string PromptId,
        string PromptFingerprint);

    private void ValidateCandidateEvidenceBinding(
        CandidateInvestigationPersistenceRequest request,
        CandidateEvidenceProvenanceBinding binding,
        SqliteTransaction transaction)
    {
        using SqliteCommand source = connection.CreateCommand();
        source.Transaction = transaction;
        source.CommandText =
            """
            SELECT proposal.payload_id FROM evidence_acquisition_application_links application
            JOIN evidence_acquisition_runs acquisition ON acquisition.acquisition_run_id=application.acquisition_run_id
            JOIN provider_semantic_admissions admission
              ON admission.admission_id=application.admission_id
             AND admission.owner_kind='evidence-acquisition-run'
             AND admission.owner_id=application.acquisition_run_id
             AND admission.admitted_artifact_id=application.admitted_artifact_id
             AND admission.state='admitted'
            JOIN provider_semantic_proposals proposal ON proposal.proposal_id=admission.proposal_id
             AND proposal.payload_id=application.admitted_artifact_id
            WHERE application.application_link_id=$source_application
              AND application.acquisition_run_id=$acquisition AND application.admission_id=$admission
              AND application.analysis_run_id=$run AND application.application_scope_id=$scope
              AND application.cost_attribution_scope_id=$cost
              AND acquisition.parent_analysis_run_id=$run;
            """;
        source.Parameters.AddWithValue("$source_application", binding.SourceApplicationLinkId);
        source.Parameters.AddWithValue("$acquisition", binding.SourceAcquisitionId);
        source.Parameters.AddWithValue("$admission", binding.SourceAdmissionId);
        source.Parameters.AddWithValue("$run", request.Document.AnalysisRunId.Value);
        source.Parameters.AddWithValue("$scope", request.ApplicationScopeId);
        source.Parameters.AddWithValue("$cost", request.CostAttributionScopeId);
        string sourcePayloadId = source.ExecuteScalar() as string
            ?? throw new InvalidDataException("Candidate evidence does not bind one exact admitted WP6 source-acquisition chain.");
        byte[] sourceBytes = ReadRetainedPayloadBytes(sourcePayloadId)
            ?? throw new InvalidDataException("Candidate evidence source-acquisition payload is unavailable.");
        SourceClaimExtractionDocument sourceDocument = JsonSerializer.Deserialize<SourceClaimExtractionDocument>(sourceBytes)
            ?? throw new InvalidDataException("Candidate evidence source-acquisition payload is invalid.");
        if (sourceDocument.SourceRevisionId.Value != binding.SourceRevisionId
            || !sourceDocument.PassageIds.Any(passage => passage.Value == binding.PassageId))
        {
            throw new InvalidDataException("Candidate evidence revision or passage does not match its admitted WP6 artifact.");
        }

        using SqliteCommand evidence = connection.CreateCommand();
        evidence.Transaction = transaction;
        evidence.CommandText =
            """
            SELECT revision.evidence_state,payload.content_sha256,
              (SELECT CASE WHEN COUNT(*)=0 THEN ''
                    WHEN COUNT(DISTINCT deletion.replay_effect)=1 THEN MIN(deletion.replay_effect)
                    ELSE 'ambiguous' END
                FROM documentation_deletion_receipts deletion,
                json_each(deletion.deleted_passage_ids_json) deleted_passage
                WHERE deletion.run_id=application.run_id
                  AND deletion.documentation_revision_id=passage.documentation_revision_id
                  AND deleted_passage.value=passage.documentation_passage_id)
            FROM evidence_application_links application
            JOIN evidence_revisions revision ON revision.evidence_revision_id=application.evidence_revision_id
            JOIN documentation_passages passage ON passage.documentation_passage_id=revision.documentation_passage_id
            JOIN payloads payload ON payload.payload_id=revision.evidence_payload_id
            WHERE application.evidence_application_link_id=$evidence_application
              AND application.evidence_revision_id=$evidence AND application.run_id=$run
              AND passage.documentation_passage_id=$passage
              AND passage.documentation_revision_id=$revision
              AND payload.content_sha256=$fingerprint;
            """;
        evidence.Parameters.AddWithValue("$evidence_application", binding.EvidenceApplicationLinkId);
        evidence.Parameters.AddWithValue("$evidence", binding.EvidenceId);
        evidence.Parameters.AddWithValue("$run", request.Document.AnalysisRunId.Value);
        evidence.Parameters.AddWithValue("$passage", binding.PassageId);
        evidence.Parameters.AddWithValue("$revision", binding.SourceRevisionId);
        evidence.Parameters.AddWithValue("$fingerprint", binding.ContentSha256);
        using SqliteDataReader evidenceReader = evidence.ExecuteReader();
        if (!evidenceReader.Read())
        {
            throw new InvalidDataException("Candidate evidence does not bind its exact Slice 5 application and retained evidence payload.");
        }
        string evidenceState = evidenceReader.GetString(0);
        string deletionReplayEffect = evidenceReader.GetString(2);
        string durableAvailability = deletionReplayEffect == "audit-only" ? "deleted"
            : deletionReplayEffect == "unavailable" ? "unavailable"
            : deletionReplayEffect == "ambiguous"
                ? throw new InvalidDataException("Candidate evidence deletion authority is ambiguous.")
            : evidenceState == "deleted" ? "deleted"
            : evidenceState == "unavailable" ? "unavailable"
            : evidenceState == "admitted" ? "available"
            : throw new InvalidDataException("Candidate evidence state is not durably available to investigation.");
        if (evidenceReader.Read() || binding.Availability != durableAvailability)
        {
            throw new InvalidDataException("Candidate evidence availability drifts from durable evidence or deletion authority.");
        }
    }

    public IReadOnlyList<ProviderSemanticAdmissionReadModel> ReadCandidateInvestigationAdmissions(
        string analysisRunId,
        string candidateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT admission.admission_id,admission.proposal_id,admission.operation_id,
                  admission.response_record_id,admission.root_subject_id,admission.validation_id,
                  admission.semantic_link_id,admission.state,admission.reason,proposal.payload_id
                FROM provider_semantic_admissions admission
                JOIN provider_semantic_proposals proposal ON proposal.proposal_id=admission.proposal_id
                WHERE admission.owner_kind='analysis-run' AND admission.owner_id=$owner
                  AND admission.root_subject_id=$candidate
                ORDER BY admission.admission_id;
                """;
            command.Parameters.AddWithValue("$owner", analysisRunId);
            command.Parameters.AddWithValue("$candidate", candidateId);
            using SqliteDataReader reader = command.ExecuteReader();
            List<ProviderSemanticAdmissionReadModel> results = [];
            while (reader.Read())
            {
                results.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                    reader.GetString(8), reader.GetString(9)));
            }
            return results;
        }
    }

    public IReadOnlyList<ProviderSemanticAdmissionReadModel> ReadCandidateInvestigationAdmissionsForOperation(
        string analysisRunId,
        string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT admission.admission_id,admission.proposal_id,admission.operation_id,
                  admission.response_record_id,admission.root_subject_id,admission.validation_id,
                  admission.semantic_link_id,admission.state,admission.reason,proposal.payload_id
                FROM provider_semantic_admissions admission
                JOIN provider_semantic_proposals proposal ON proposal.proposal_id=admission.proposal_id
                WHERE admission.owner_kind='analysis-run' AND admission.owner_id=$owner
                  AND admission.operation_id=$operation
                ORDER BY admission.admission_id;
                """;
            command.Parameters.AddWithValue("$owner", analysisRunId);
            command.Parameters.AddWithValue("$operation", operationId);
            using SqliteDataReader reader = command.ExecuteReader();
            List<ProviderSemanticAdmissionReadModel> results = [];
            while (reader.Read())
            {
                results.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                    reader.GetString(8), reader.GetString(9)));
            }
            return results;
        }
    }

    public CandidateInvestigationDocument ReadCandidateInvestigation(
        string analysisRunId,
        string candidateId,
        string admissionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(admissionId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT proposal.payload_id FROM provider_semantic_admissions admission
                JOIN provider_semantic_proposals proposal ON proposal.proposal_id=admission.proposal_id
                WHERE admission.admission_id=$admission AND admission.owner_kind='analysis-run'
                  AND admission.owner_id=$owner AND admission.root_subject_id=$candidate;
                """;
            command.Parameters.AddWithValue("$admission", admissionId);
            command.Parameters.AddWithValue("$owner", analysisRunId);
            command.Parameters.AddWithValue("$candidate", candidateId);
            string payloadId = command.ExecuteScalar() as string
                ?? throw new KeyNotFoundException("The exact retained candidate investigation does not exist.");
            byte[] bytes = ReadRetainedPayloadBytes(payloadId)
                ?? throw new InvalidDataException("The retained candidate-investigation payload is unavailable.");
            CandidateInvestigationDocument document = JsonSerializer.Deserialize<CandidateInvestigationDocument>(bytes)
                ?? throw new InvalidDataException("The retained candidate-investigation payload is invalid.");
            ProviderOperationContractInvariants.Validate(document);
            return document;
        }
    }

    public CandidateInvestigationOutcomeReadModel ReadCandidateInvestigationOutcome(
        string analysisRunId,
        string operationId,
        string contextId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contextId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT outcome_id,candidate_id,hypothesis_id,transcript_id,response_record_id,response_fingerprint,
                  transcript_state,disposition,replay_state,input_payload_id,transcript_payload_id,result_payload_id
                FROM candidate_investigation_outcomes
                WHERE owner_id=$owner AND operation_id=$operation AND context_id=$context;
                """;
            command.Parameters.AddWithValue("$owner", analysisRunId);
            command.Parameters.AddWithValue("$operation", operationId);
            command.Parameters.AddWithValue("$context", contextId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException("The exact retained candidate-investigation outcome does not exist.");
            }
            string inputPayloadId = reader.GetString(9);
            string transcriptPayloadId = reader.GetString(10);
            string resultPayloadId = reader.GetString(11);
            string outcomeId = reader.GetString(0);
            string candidateId = reader.GetString(1);
            string hypothesisId = reader.GetString(2);
            string transcriptId = reader.GetString(3);
            string? responseRecordId = reader.IsDBNull(4) ? null : reader.GetString(4);
            string responseFingerprint = reader.GetString(5);
            string transcriptState = reader.GetString(6);
            string disposition = reader.GetString(7);
            string replayState = reader.GetString(8);
            reader.Close();
            return new(outcomeId, analysisRunId, operationId, candidateId, hypothesisId, contextId,
                transcriptId, responseRecordId, responseFingerprint, transcriptState, disposition, replayState, inputPayloadId, transcriptPayloadId,
                resultPayloadId, ReadRetainedPayloadBytes(inputPayloadId)!, ReadRetainedPayloadBytes(transcriptPayloadId)!,
                ReadRetainedPayloadBytes(resultPayloadId)!);
        }
    }

    public IReadOnlyList<CandidateInvestigationOutcomeIdentityReadModel> ReadCandidateInvestigationOutcomesForOperation(
        string analysisRunId,
        string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT outcome_id,context_id,response_record_id,transcript_state,disposition,replay_state,result_payload_id
                FROM candidate_investigation_outcomes
                WHERE owner_id=$owner AND operation_id=$operation ORDER BY context_id;
                """;
            command.Parameters.AddWithValue("$owner", analysisRunId);
            command.Parameters.AddWithValue("$operation", operationId);
            using SqliteDataReader reader = command.ExecuteReader();
            List<CandidateInvestigationOutcomeIdentityReadModel> results = [];
            while (reader.Read())
            {
                results.Add(new(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6)));
            }
            return results;
        }
    }

    public CandidateInvestigationNoResponsePublicationReadModel ReadCandidateInvestigationNoResponsePublication(
        string analysisRunId,
        string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisRunId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT outcome.disposition,outcome.transcript_state,authorization.effective_configuration_id
                FROM candidate_investigation_outcomes outcome
                JOIN provider_operation_authorizations authorization
                  ON authorization.authorization_id=outcome.authorization_id
                 AND authorization.operation_id=outcome.operation_id
                WHERE outcome.owner_id=$owner AND outcome.operation_id=$operation
                  AND outcome.response_record_id IS NULL;
                """;
            command.Parameters.AddWithValue("$owner", analysisRunId);
            command.Parameters.AddWithValue("$operation", operationId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException("The exact no-response candidate outcome does not exist.");
            }
            CandidateInvestigationNoResponsePublicationReadModel result =
                new(reader.GetString(0), reader.GetString(1), reader.GetString(2));
            if (reader.Read())
            {
                throw new InvalidDataException("No-response candidate publication must bind one exact terminal outcome.");
            }
            return result;
        }
    }
}

public sealed record ProviderSemanticAdmissionReadModel(
    string AdmissionId,
    string ProposalId,
    string OperationId,
    string ResponseRecordId,
    string RootSubjectId,
    string ValidationId,
    string AdmissionCorrelationId,
    string State,
    string Reason,
    string PayloadId);

public sealed record CandidateInvestigationPersistenceRequest(
    CandidateInvestigationDocument Document,
    string OutcomeId,
    string ContextId,
    string HypothesisId,
    string TranscriptId,
    string ResponseFingerprint,
    string TranscriptState,
    string Disposition,
    string ReplayState,
    string ApplicationScopeId,
    string CostAttributionScopeId,
    IReadOnlyList<CandidateEvidenceProvenanceBinding> EvidenceBindings,
    byte[] InputPayload,
    byte[] TranscriptPayload,
    byte[] ResultPayload,
    string AuthorizationId,
    string? ResponseRecordId,
    string? ProviderAttemptId,
    string? RequestId,
    string? DispatchFenceId,
    DateTimeOffset OccurredAt);

public sealed record CandidateEvidenceProvenanceBinding(
    string EvidenceId,
    string EvidenceApplicationLinkId,
    string SourceAcquisitionId,
    string SourceAdmissionId,
    string SourceApplicationLinkId,
    string SourceRevisionId,
    string PassageId,
    string Relationship,
    string Availability,
    string ContentSha256);

public sealed record CandidateInvestigationPersistenceReceipt(
    string AnalysisRunId,
    string OperationId,
    string CandidateId,
    int ProposalCount,
    int AdmissionCount,
    IReadOnlyList<string> AdmissionIds,
    string OutcomeId);

public sealed record CandidateInvestigationOutcomeReadModel(
    string OutcomeId,
    string AnalysisRunId,
    string OperationId,
    string CandidateId,
    string HypothesisId,
    string ContextId,
    string TranscriptId,
    string? ResponseRecordId,
    string ResponseFingerprint,
    string TranscriptState,
    string Disposition,
    string ReplayState,
    string InputPayloadId,
    string TranscriptPayloadId,
    string ResultPayloadId,
    byte[] InputPayload,
    byte[] TranscriptPayload,
    byte[] ResultPayload);

public sealed record CandidateInvestigationOutcomeIdentityReadModel(
    string OutcomeId,
    string ContextId,
    string? ResponseRecordId,
    string TranscriptState,
    string Disposition,
    string ReplayState,
    string ResultPayloadId);

public sealed record CandidateInvestigationNoResponsePublicationReadModel(
    string Disposition,
    string TranscriptState,
    string EffectiveConfigurationId);
