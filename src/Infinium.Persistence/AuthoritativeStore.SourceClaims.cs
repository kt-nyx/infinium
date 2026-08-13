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
        if (document.AdmissionLinks.Any(link => link.AuthorizationId.Value != request.AuthorizationId
                || link.ResponseRecordId.Value != request.ResponseRecordId))
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
                    JOIN provider_responses response
                      ON response.authorization_id=authorization.authorization_id
                     AND response.operation_id=authorization.operation_id
                    JOIN analysis_candidates candidate
                      ON candidate.candidate_id=$candidate AND candidate.run_id=authorization.owner_id
                    WHERE authorization.authorization_id=$authorization
                      AND authorization.operation_id=$operation
                      AND authorization.owner_kind='analysis-run' AND authorization.owner_id=$owner
                      AND authorization.operation_kind='candidate-investigation'
                      AND response.response_record_id=$response
                      AND response.provider_attempt_id=$attempt AND response.request_id=$request
                      AND response.dispatch_fence_id=$fence;
                    """;
                authority.Parameters.AddWithValue("$candidate", document.CandidateId.Value);
                authority.Parameters.AddWithValue("$authorization", request.AuthorizationId);
                authority.Parameters.AddWithValue("$operation", document.OperationId.Value);
                authority.Parameters.AddWithValue("$owner", document.OwnerId.Value);
                authority.Parameters.AddWithValue("$response", request.ResponseRecordId);
                authority.Parameters.AddWithValue("$attempt", request.ProviderAttemptId);
                authority.Parameters.AddWithValue("$request", request.RequestId);
                authority.Parameters.AddWithValue("$fence", request.DispatchFenceId);
                if (authority.ExecuteScalar() is null)
                {
                    throw new InvalidDataException("Candidate persistence requires exact retained analysis, authorization, response, and candidate authority.");
                }
            }
            foreach (ProviderSemanticAdmissionLinkContract link in document.AdmissionLinks)
            {
                HypothesisProposalContract proposal = document.HypothesisProposals.Single(x => x.ProposalId == link.ProposalId);
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(document);
                string payloadId = AdmitCoordinatorPayload(
                    payload, "candidate-investigation", proposal.ProposalId.Value, request.OccurredAt, transaction);
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
                document.AdmissionLinkIds.Select(x => x.Value).ToArray());
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
    string AuthorizationId,
    string ResponseRecordId,
    string ProviderAttemptId,
    string RequestId,
    string DispatchFenceId,
    DateTimeOffset OccurredAt);

public sealed record CandidateInvestigationPersistenceReceipt(
    string AnalysisRunId,
    string OperationId,
    string CandidateId,
    int ProposalCount,
    int AdmissionCount,
    IReadOnlyList<string> AdmissionIds);
