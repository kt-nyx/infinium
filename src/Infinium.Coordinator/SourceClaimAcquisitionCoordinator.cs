using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Coordinator;

public sealed record SourceClaimAdmissionPublication(
    SourceClaimScenarioResult Scenario,
    SourceClaimApplicationResult Applications,
    SourceClaimPersistenceReceipt Persistence,
    byte[] JsonTransparency,
    string HumanTransparency);

internal sealed record SourceClaimCampaignIdentity(
    string ApplicationDecisionId,
    string ValidationId,
    string AdmittedArtifactId,
    string ApplicationLinkId);

public sealed class SourceClaimAcquisitionCoordinator
{
    private readonly AuthoritativeStore store;

    public SourceClaimAcquisitionCoordinator(AuthoritativeStore store) =>
        this.store = store ?? throw new ArgumentNullException(nameof(store));

    public SourceClaimAdmissionPublication AdmitRetainedTranscript(
        SourceClaimExecutionInput input,
        SourceClaimRetainedTranscript transcript,
        string authorizationId,
        string providerAttemptId,
        string requestId,
        string dispatchFenceId,
        DateTimeOffset occurredAt) => AdmitRetainedTranscriptCore(input, transcript,
            authorizationId, providerAttemptId, requestId, dispatchFenceId, occurredAt, null);

    internal SourceClaimAdmissionPublication AdmitRetainedTranscript(
        SourceClaimExecutionInput input,
        SourceClaimRetainedTranscript transcript,
        string authorizationId,
        string providerAttemptId,
        string requestId,
        string dispatchFenceId,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, SourceClaimCampaignIdentity> campaignIdentities,
        IReadOnlyDictionary<string, SourceClaimArtifactAuthority> artifactAuthority,
        IReadOnlyDictionary<string, SourceClaimApplicabilityFactAuthority> applicabilityFacts,
        IReadOnlyDictionary<string, SourceClaimApplicationAuthority> applicationAuthority) =>
        AdmitRetainedTranscriptCore(input, transcript, authorizationId, providerAttemptId,
            requestId, dispatchFenceId, occurredAt, campaignIdentities,
            artifactAuthority, applicabilityFacts, applicationAuthority);

    private SourceClaimAdmissionPublication AdmitRetainedTranscriptCore(
        SourceClaimExecutionInput input,
        SourceClaimRetainedTranscript transcript,
        string authorizationId,
        string providerAttemptId,
        string requestId,
        string dispatchFenceId,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, SourceClaimCampaignIdentity>? campaignIdentities,
        IReadOnlyDictionary<string, SourceClaimArtifactAuthority>? artifactAuthority = null,
        IReadOnlyDictionary<string, SourceClaimApplicabilityFactAuthority>? applicabilityFacts = null,
        IReadOnlyDictionary<string, SourceClaimApplicationAuthority>? applicationAuthority = null)
    {
        if (authorizationId != input.HostAuthorizationId)
        {
            throw new InvalidDataException("Source-claim admission requires the exact host authorization bound by the execution input.");
        }
        SourceClaimAcquisitionResult result = SourceClaimAcquisitionEngine.Execute(input, [transcript]);
        SourceClaimScenarioResult scenario = result.Scenarios.Single();
        SourceClaimApplicationResult applications = new([]);
        Dictionary<string, string> artifactIds = new(StringComparer.Ordinal);
        if (campaignIdentities is not null)
        {
            if (applicationAuthority is null
                || campaignIdentities.Keys.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(transcript.Proposals.Select(item => item.ProposalId)) is false
                || applicationAuthority.Keys.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(campaignIdentities.Keys) is false)
            {
                throw new InvalidDataException(
                    "Source-claim application requires one analysis-owned context for every retained proposal.");
            }
            SourceClaimApplicationContext[] contexts = transcript.Proposals.Select(proposal =>
            {
                SourceClaimCampaignIdentity identity = campaignIdentities[proposal.ProposalId];
                SourceClaimApplicationAuthority authority = applicationAuthority[proposal.ProposalId];
                if (identity.ApplicationLinkId != authority.ApplicationLinkId)
                {
                    throw new InvalidDataException(
                        "Source-claim application identities differ from their analysis authority.");
                }
                return new SourceClaimApplicationContext(proposal.ProposalId,
                    identity.ApplicationDecisionId, identity.ValidationId, identity.ApplicationLinkId,
                    authority.ParentAnalysisRunId, authority.RootSubjectId, authority.ApplicabilityFactIds);
            }).ToArray();
            applications = SourceClaimApplicationAdjudicator.Evaluate(input, scenario, contexts);
            foreach (SourceClaimApplicationDecisionContract decision in applications.Decisions.Where(
                         item => item.DecisionLink.DecisionState == SemanticDecisionState.Admitted))
            {
                SourceClaimCampaignIdentity identity = campaignIdentities[decision.DecisionLink.ProposalId.Value];
                artifactIds.Add(decision.DecisionLink.ProposalId.Value, identity.AdmittedArtifactId);
            }
        }
        SourceClaimPersistenceReceipt persistence = store.PersistSourceClaimExtraction(new(
            scenario.Extraction, authorizationId, transcript.ResponseRecordId, providerAttemptId,
            requestId, dispatchFenceId, occurredAt)
        {
            AdmittedArtifactIdsByProposal = artifactIds,
            AdmittedArtifactAuthorityByProposal = artifactAuthority
                ?? new Dictionary<string, SourceClaimArtifactAuthority>(StringComparer.Ordinal),
            ApplicabilityFactsById = applicabilityFacts
                ?? new Dictionary<string, SourceClaimApplicabilityFactAuthority>(StringComparer.Ordinal),
            ApplicationAuthorityByProposal = applicationAuthority
                ?? new Dictionary<string, SourceClaimApplicationAuthority>(StringComparer.Ordinal),
            ApplicationDecisionsByProposal = applications.Decisions.ToDictionary(
                item => item.DecisionLink.ProposalId.Value, StringComparer.Ordinal),
        });
        return new(scenario, applications, persistence,
            SourceClaimTransparencyRenderer.RenderJson(result, applications),
            SourceClaimTransparencyRenderer.RenderHuman(result, applications));
    }

    public static SourceClaimAcquisitionResult NoModel(
        SourceClaimExecutionInput input,
        SourceClaimRetainedTranscript noModelMarker)
    {
        if (noModelMarker.ModelUsed || noModelMarker.ResponseState != "not-used")
        {
            throw new InvalidDataException("The no-model source-claim path requires the exact not-used marker.");
        }
        return SourceClaimAcquisitionEngine.Execute(input, [noModelMarker]);
    }
}
