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
        CandidateInvestigationRetainedTranscript transcript)
        => CandidateInvestigationEngine.Execute(input, [transcript]).Scenarios.Single();

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
        CandidateInvestigationContextInput retainedContext = input.Contexts.Single(context =>
            context.ContextId == scenario.ContextId);
        CandidateInvestigationPersistenceRequest persistence = new(
            scenario.Investigation, "outcome-" + transcript.TranscriptId, scenario.ContextId, scenario.HypothesisId,
            transcript.TranscriptId,
            transcript.ResponseFingerprint, transcript.ResponseState, scenario.Disposition, scenario.ReplayState,
            input.ApplicationScopeId, input.CostAttributionScopeId,
            retainedContext.Evidence.Select(evidence => new CandidateEvidenceProvenanceBinding(
                evidence.EvidenceId, evidence.EvidenceApplicationLinkId,
                evidence.SourceAcquisitionId, evidence.SourceAdmissionId,
                evidence.SourceApplicationDecisionId, evidence.SourceApplicationLinkId,
                evidence.SourceRevisionId, evidence.PassageId, evidence.Relationship,
                evidence.Availability, evidence.ContentSha256)
            {
                RootKind = evidence.RootKind,
                EvidenceRootId = evidence.EvidenceRootId,
                ApplicabilityRecordId = evidence.ApplicabilityRecordId,
            }).ToArray(),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(input, SourceClaimContextMinimizer.JsonOptions),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(transcript, SourceClaimContextMinimizer.JsonOptions),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(scenario.Investigation),
            authorizationId,
            transcript.ModelUsed ? transcript.ResponseRecordId : null,
            transcript.ModelUsed ? providerAttemptId : null,
            transcript.ModelUsed ? requestId : null,
            transcript.ModelUsed ? dispatchFenceId : null,
            occurredAt);
        CandidateInvestigationPersistenceReceipt receipt = store.PersistCandidateInvestigation(persistence);
        CandidateInvestigationResult result = new(CandidateInvestigationPromptV1.Id, CandidateInvestigationPromptV1.Fingerprint,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                CandidateInvestigationContextMinimizer.CreateManifest(input))), [scenario], false, false, false);
        return new(scenario, receipt, CandidateInvestigationTransparencyRenderer.RenderJson(result),
            CandidateInvestigationTransparencyRenderer.RenderHuman(result));
    }

    public CandidateInvestigationScenarioResult ReplayRetained(
        string analysisRunId,
        string operationId,
        string contextId)
    {
        CandidateInvestigationOutcomeReadModel retained =
            store.ReadCandidateInvestigationOutcome(analysisRunId, operationId, contextId);
        CandidateInvestigationExecutionInput input;
        using (System.Text.Json.JsonDocument inputDocument = System.Text.Json.JsonDocument.Parse(retained.InputPayload))
        {
            string? schemaId = inputDocument.RootElement.GetProperty("schema_id").GetString();
            input = schemaId switch
            {
                "infinium.llm.candidate-investigation-execution-input/v1" =>
                    System.Text.Json.JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
                        retained.InputPayload, SourceClaimContextMinimizer.JsonOptions)
                    ?? throw new InvalidDataException("The retained candidate-investigation input is invalid."),
                _ => throw new InvalidDataException("The retained candidate-investigation input identity is invalid."),
            };
        }
        foreach (CandidateEvidenceInput evidence in input.Contexts.Single(item => item.ContextId == contextId).Evidence)
        {
            if (evidence.RootKind == "persisted-source-claim-application")
            {
                store.ValidateSourceClaimApplicationPayloads(
                    evidence.SourceApplicationLinkId, evidence.SourceAcquisitionId,
                    evidence.SourceApplicationDecisionId);
            }
            else
            {
                store.ValidateFrozenHostCandidateEvidencePayloads(
                    analysisRunId, evidence.EvidenceApplicationLinkId, evidence.EvidenceId,
                    evidence.SourceRevisionId, evidence.ContentSha256);
            }
        }
        store.ValidateCandidateSourceApplicationPayloads(retained.OutcomeId);
        CandidateInvestigationRetainedTranscript transcript =
            System.Text.Json.JsonSerializer.Deserialize<CandidateInvestigationRetainedTranscript>(
                retained.TranscriptPayload, SourceClaimContextMinimizer.JsonOptions)
            ?? throw new InvalidDataException("The retained candidate-investigation transcript is invalid.");
        if (transcript.TranscriptId != retained.TranscriptId
            || transcript.ResponseFingerprint != retained.ResponseFingerprint
            || transcript.ResponseRecordId != retained.ResponseRecordId && retained.ResponseRecordId is not null)
        {
            throw new InvalidDataException("The retained candidate transcript does not match its authoritative outcome identity.");
        }
        CandidateInvestigationScenarioResult replay = CandidateInvestigationEngine.Execute(input, [transcript]).Scenarios.Single();
        byte[] replayDocument = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(replay.Investigation);
        if (!replayDocument.AsSpan().SequenceEqual(retained.ResultPayload)
            || replay.Disposition != retained.Disposition || replay.ReplayState != retained.ReplayState)
        {
            throw new InvalidDataException("The retained candidate outcome does not replay to its authoritative bytes.");
        }
        return replay;
    }
}
