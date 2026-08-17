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
        => AdmitRetainedTranscript(input, transcript, authorizationId, providerAttemptId, requestId,
            dispatchFenceId, occurredAt, null, null);

    internal CandidateInvestigationAdmissionPublication AdmitRetainedTranscript(
        CandidateInvestigationExecutionInput input,
        CandidateInvestigationRetainedTranscript transcript,
        string authorizationId,
        string providerAttemptId,
        string requestId,
        string dispatchFenceId,
        DateTimeOffset occurredAt,
        byte[]? exactCampaignV2Input,
        M1Slice6CampaignEvidenceRoot? campaignRoot,
        M1Slice6CampaignLocalObservation? localObservation = null)
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
                campaignRoot?.Kind == M1Slice6CampaignEvidenceRootKind.FrozenHostEvidence
                    ? string.Empty : evidence.SourceAcquisitionId,
                campaignRoot?.Kind == M1Slice6CampaignEvidenceRootKind.FrozenHostEvidence
                    ? string.Empty : evidence.SourceAdmissionId,
                campaignRoot?.Kind == M1Slice6CampaignEvidenceRootKind.FrozenHostEvidence
                    ? string.Empty : evidence.SourceApplicationLinkId,
                evidence.SourceRevisionId, evidence.PassageId, evidence.Relationship,
                evidence.Availability, evidence.ContentSha256)
            {
                RootKind = campaignRoot?.Kind == M1Slice6CampaignEvidenceRootKind.FrozenHostEvidence
                    ? "frozen-host-evidence" : "persisted-source-claim-application",
                ProposalId = campaignRoot?.ProposalId ?? string.Empty,
                AdmittedArtifactId = campaignRoot?.AdmittedArtifactId ?? string.Empty,
                EvidenceRootId = campaignRoot?.EvidenceRootId ?? string.Empty,
                ApplicabilityRecordId = campaignRoot?.ApplicabilityRecordId ?? string.Empty,
                LocalObservationId = localObservation?.ObservationId ?? string.Empty,
                LocalObservationSha256 = localObservation?.TextSha256 ?? string.Empty,
            }).ToArray(),
            exactCampaignV2Input ?? System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                input, SourceClaimContextMinimizer.JsonOptions),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(transcript, SourceClaimContextMinimizer.JsonOptions),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(scenario.Investigation),
            authorizationId,
            transcript.ModelUsed ? transcript.ResponseRecordId : null,
            transcript.ModelUsed ? providerAttemptId : null,
            transcript.ModelUsed ? requestId : null,
            transcript.ModelUsed ? dispatchFenceId : null,
            occurredAt)
        {
            NormalizedProductInputPayload = exactCampaignV2Input is null ? null
                : System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(input, SourceClaimContextMinimizer.JsonOptions),
        };
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
        bool campaignV2;
        using (System.Text.Json.JsonDocument inputDocument = System.Text.Json.JsonDocument.Parse(retained.InputPayload))
        {
            string? schemaId = inputDocument.RootElement.GetProperty("schema_id").GetString();
            campaignV2 = schemaId == "infinium.llm.candidate-investigation-execution-input/v2";
            input = schemaId switch
            {
                "infinium.llm.candidate-investigation-execution-input/v2" =>
                    M1Slice6CampaignV2InputAdapter.ReadCandidate(
                        System.Text.Encoding.UTF8.GetString(retained.InputPayload)).ProductInput,
                "infinium.llm.candidate-investigation-execution-input/v1" =>
                    System.Text.Json.JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
                        retained.InputPayload, SourceClaimContextMinimizer.JsonOptions)
                    ?? throw new InvalidDataException("The retained candidate-investigation input is invalid."),
                _ => throw new InvalidDataException("The retained candidate-investigation input identity is invalid."),
            };
        }
        if (campaignV2)
        {
            store.ValidateCandidateEvidenceAuthority(retained.OutcomeId, contextId, retained.InputPayload);
        }
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
