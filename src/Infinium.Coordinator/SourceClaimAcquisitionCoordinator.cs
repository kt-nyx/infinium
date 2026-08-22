using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Coordinator;

public sealed record SourceClaimAdmissionPublication(
    SourceClaimScenarioResult Scenario,
    SourceClaimPersistenceReceipt Persistence,
    byte[] JsonTransparency,
    string HumanTransparency);

internal sealed record SourceClaimCampaignIdentity(
    string AdmissionId, string AdmittedArtifactId, string ApplicationLinkId);

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
        DateTimeOffset occurredAt) => AdmitRetainedTranscriptCore(input, transcript, authorizationId,
            providerAttemptId, requestId, dispatchFenceId, occurredAt, null);

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
            requestId, dispatchFenceId, occurredAt, campaignIdentities, artifactAuthority, applicabilityFacts,
            applicationAuthority);

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
        Dictionary<string, string> artifactIds = new(StringComparer.Ordinal);
        if (campaignIdentities is not null)
        {
            SourceClaimAdmissionCorrelationContract[] correlations = scenario.Extraction.AdmissionCorrelations
                .Select(correlation =>
                {
                    if (correlation.DecisionState != SemanticDecisionState.Admitted)
                    {
                        return correlation;
                    }
                    if (!campaignIdentities.TryGetValue(correlation.ProposalId.Value,
                            out SourceClaimCampaignIdentity? identity))
                    {
                        throw new InvalidDataException(
                            "Campaign source admission has no exact host-owned identity tuple.");
                    }
                    artifactIds.Add(correlation.ProposalId.Value, identity.AdmittedArtifactId);
                    return correlation with { AdmissionId = new OpaqueId(identity.AdmissionId) };
                }).ToArray();
            SourceClaimExtractionDocument extraction = scenario.Extraction with
            {
                AdmissionCorrelations = correlations,
            };
            scenario = scenario with { Extraction = extraction };
            result = result with { Scenarios = [scenario] };
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
        });
        return new(scenario, persistence, SourceClaimTransparencyRenderer.RenderJson(result),
            SourceClaimTransparencyRenderer.RenderHuman(result));
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
