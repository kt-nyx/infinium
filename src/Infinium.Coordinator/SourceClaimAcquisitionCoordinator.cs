using Infinium.Application.Provider;
using Infinium.Persistence;

namespace Infinium.Coordinator;

public sealed record SourceClaimAdmissionPublication(
    SourceClaimScenarioResult Scenario,
    SourceClaimPersistenceReceipt Persistence,
    byte[] JsonTransparency,
    string HumanTransparency);

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
        DateTimeOffset occurredAt)
    {
        SourceClaimAcquisitionResult result = SourceClaimAcquisitionEngine.Execute(input, [transcript]);
        SourceClaimScenarioResult scenario = result.Scenarios.Single();
        SourceClaimPersistenceReceipt persistence = store.PersistSourceClaimExtraction(new(
            scenario.Extraction, authorizationId, transcript.ResponseRecordId, providerAttemptId,
            requestId, dispatchFenceId, occurredAt));
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
