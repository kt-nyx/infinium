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
    IReadOnlyList<SourceClaimApplicationRegistration> ApplicationLinks,
    string SourceRevisionId,
    DateTimeOffset OccurredAt);

public sealed record SourceClaimApplicationRegistration(string ApplicationLinkId, string AdmittedArtifactId);

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
    IReadOnlyList<string> ApplicationLinkIds);

public partial class AuthoritativeStore
{
    public void RegisterSourceClaimAcquisition(SourceClaimAcquisitionRegistration request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ApplicationLinks.Count is < 1 or > 64
            || request.ApplicationLinks.Select(x => x.ApplicationLinkId).Distinct(StringComparer.Ordinal).Count()
                != request.ApplicationLinks.Count)
        {
            throw new InvalidDataException("Source-claim acquisition requires a bounded unique application-link set.");
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            Execute(
                """
                INSERT INTO evidence_acquisition_runs VALUES(
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
            foreach (SourceClaimApplicationRegistration link in request.ApplicationLinks)
            {
                Execute(
                    "INSERT INTO evidence_acquisition_application_links VALUES($application,$acquisition,$run,$scope,$cost,$artifact,$now);",
                    transaction, ("$application", link.ApplicationLinkId), ("$acquisition", request.AcquisitionRunId),
                    ("$run", request.ParentAnalysisRunId), ("$scope", request.ApplicationScopeId),
                    ("$cost", request.CostAttributionScopeId), ("$artifact", link.AdmittedArtifactId),
                    ("$now", ToText(request.OccurredAt)));
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

    public SourceClaimPersistenceReceipt PersistSourceClaimExtraction(SourceClaimPersistenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ProviderOperationContractInvariants.Validate(request.Document);
        SourceClaimExtractionDocument document = request.Document;
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            foreach (ProviderSemanticAdmissionLinkContract link in document.AdmissionLinks)
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
                      dispatch_fence_id,owner_kind,owner_id,root_subject_id,application_link_id,proposal_kind,
                      payload_id,created_at)
                    VALUES($proposal,$authorization,$operation,$attempt,$request,$response,$fence,$owner_kind,
                      $owner,$root,$application,$kind,$payload,$now);
                    INSERT INTO provider_semantic_validations VALUES(
                      $validation,$proposal,$operation,$response,$owner_kind,$owner,$root,$state,
                      'infinium.host.source-claim-admission/v1',$reason,$now);
                    INSERT INTO provider_semantic_admissions VALUES(
                      $admission,$proposal,$operation,$response,$owner_kind,$owner,$root,$validation,$application,
                      $state,'infinium.host.source-claim-admission/v1',$reason,$artifact,$now);
                    """,
                    transaction,
                    ("$proposal", proposal.ProposalId.Value), ("$authorization", request.AuthorizationId),
                    ("$operation", document.OperationId.Value), ("$attempt", request.ProviderAttemptId),
                    ("$request", request.RequestId), ("$response", request.ResponseRecordId),
                    ("$fence", request.DispatchFenceId), ("$owner_kind", document.OwnerKind),
                    ("$owner", document.OwnerId.Value), ("$root", document.SourceRevisionId.Value),
                    ("$application", link.ApplicationLinkId.Value), ("$kind", kind), ("$payload", payloadId),
                    ("$validation", link.ValidationId.Value), ("$state", ToAdmissionState(proposal.State)),
                    ("$reason", proposal.Reason), ("$admission", link.AdmissionId.Value),
                    ("$artifact", proposal.State == ProposalAdmissionState.Admitted ? payloadId : null),
                    ("$now", ToText(request.OccurredAt)));
            }
            transaction.Commit();
            return new(document.AcquisitionRunId.Value, document.OperationId.Value, document.SourceRevisionId.Value,
                document.ClaimProposals.Count, document.AdmissionLinks.Count,
                document.ApplicationLinkIds.Select(x => x.Value).ToArray());
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
                  admission.application_link_id,admission.state,admission.reason,proposal.payload_id
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
}

public sealed record ProviderSemanticAdmissionReadModel(
    string AdmissionId,
    string ProposalId,
    string OperationId,
    string ResponseRecordId,
    string RootSubjectId,
    string ValidationId,
    string ApplicationLinkId,
    string State,
    string Reason,
    string PayloadId);
