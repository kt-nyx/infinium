using Infinium.Application.Provider;
using Infinium.Persistence;

namespace Infinium.Coordinator;

public static class CandidateInvestigationCoordinator
{
    public static CandidateInvestigationResult ExecuteRetained(
        CandidateInvestigationExecutionInput input,
        IReadOnlyList<CandidateInvestigationRetainedTranscript> transcripts)
        => CandidateInvestigationEngine.Execute(input, transcripts);

    public static CandidateInvestigationScenarioResult ReplayRetained(
        CandidateInvestigationExecutionInput input,
        CandidateInvestigationRetainedTranscript transcript,
        string retainedResponseFingerprint)
        => CandidateInvestigationEngine.Replay(input, transcript, retainedResponseFingerprint);

    public static CandidateInvestigationResult NoModel(
        CandidateInvestigationExecutionInput input,
        CandidateInvestigationRetainedTranscript transcript)
    {
        if (transcript.ResponseState != "not-used" || transcript.ModelUsed)
        {
            throw new InvalidDataException("The no-model path requires the exact retained not-used transcript.");
        }
        return CandidateInvestigationEngine.Execute(input, [transcript]);
    }
}

public sealed record CandidateInvestigationAdmissionPublication(
    CandidateInvestigationScenarioResult Scenario,
    CandidateInvestigationPersistenceReceipt Persistence,
    byte[] JsonTransparency,
    string HumanTransparency);

public sealed class DurableCandidateInvestigationCoordinator(AuthoritativeStore store)
{
    private readonly AuthoritativeStore store = store ?? throw new ArgumentNullException(nameof(store));

    public CandidateInvestigationAdmissionPublication AdmitRetainedTranscript(
        CandidateInvestigationExecutionInput input,
        CandidateInvestigationRetainedTranscript transcript,
        string authorizationId,
        string providerAttemptId,
        string requestId,
        string dispatchFenceId,
        DateTimeOffset occurredAt)
    {
        if (authorizationId != input.HostAuthorizationId)
        {
            throw new InvalidDataException("Candidate admission requires the exact host authorization bound by the execution input.");
        }
        CandidateInvestigationScenarioResult scenario = CandidateInvestigationEngine.Execute(input, [transcript]).Scenarios.Single();
        CandidateInvestigationPersistenceReceipt receipt = store.PersistCandidateInvestigation(new(
            scenario.Investigation, authorizationId, transcript.ResponseRecordId, providerAttemptId, requestId,
            dispatchFenceId, occurredAt));
        CandidateInvestigationResult result = new(CandidateInvestigationPromptV1.Id, CandidateInvestigationPromptV1.Fingerprint,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                CandidateInvestigationContextMinimizer.CreateManifest(input))), [scenario], false, false, false);
        return new(scenario, receipt, CandidateInvestigationTransparencyRenderer.RenderJson(result),
            CandidateInvestigationTransparencyRenderer.RenderHuman(result));
    }
}
