using System.Text;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Infinium.Persistence;

namespace Infinium.Coordinator;

public sealed record ProviderTerminalPublicationArtifacts(
    ProviderRunOutputV2BindingReceipt Binding,
    RunOutputV2Document RunOutputV2,
    CliSummaryV2Document CliSummaryV2);

public sealed class ProviderAccountingCoordinator
{
    private readonly AuthoritativeStore store;

    public ProviderAccountingCoordinator(AuthoritativeStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void PublishExactCatalog(DateTimeOffset now) =>
        store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, now);

    public void ConfigureLimits(
        long coordinatorFencingEpoch,
        IReadOnlyList<ProviderBudgetScopeContract> scopes,
        DateTimeOffset now) =>
        store.ConfigureProviderBudgetScopes(coordinatorFencingEpoch, scopes, now);

    public ProviderReservationAdmissionContract Reserve(
        long coordinatorFencingEpoch,
        ProviderBudgetReservationRequest request) =>
        store.ReserveProviderBudget(coordinatorFencingEpoch, request);

    public ProviderDispatchGateReceipt FinalGate(ProviderDispatchGateRequest request) =>
        store.AuthorizeProviderDispatch(request);

    public DeterministicProviderTranscript Simulate(
        ProviderDispatchGateReceipt gate,
        string operationId,
        string attemptId,
        string requestId,
        ProviderSimulatorOutcome outcome,
        ProviderFiniteLimitsContract limits,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(gate);
        bool ambiguous = outcome == ProviderSimulatorOutcome.AmbiguousStart;
        if (outcome != ProviderSimulatorOutcome.KnownUndispatched)
        {
            store.RecordProviderTransportStart(
                operationId,
                attemptId,
                requestId,
                gate.DispatchFenceId,
                ambiguous,
                now);
        }
        return DeterministicProviderSimulator.Execute(outcome, limits, new UtcTimestamp(now));
    }

    public ProviderBudgetSettlementReceipt Settle(ProviderBudgetSettlementRequest request) =>
        store.SettleProviderBudget(request);

    public ProviderOperationSummaryProjection QueryOperation(ProviderOperationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ProviderOperationReadModel operation = store.ReadProviderOperation(query.OperationId.Value);
        return new(new OpaqueId(operation.OperationId), operation.State, "openai", "gpt-5.6-sol",
            operation.ReservedNanoUsd, operation.CalculatedNanoUsd, operation.UnresolvedHold,
            query.IncludeReplay ? operation.ReplayState : "not-available", []);
    }

    public OpenAiResponsesResult Replay(ProviderReplayQuery query)
    {
        ProviderApplicationContractInvariants.Validate(query);
        ProviderOperationReadModel operation = store.ReadProviderOperation(query.OperationId.Value);
        if (query.RetainedResponseId is not null
            && query.RetainedResponseId.Value != operation.ResponseId)
        {
            throw new InvalidOperationException("The replay query cross-bound a different retained response.");
        }
        if (operation.ResponseHeadersBytes is null)
        {
            return OpenAiResponsesResponseCodec.Replay(operation.RawResponseBytes ?? [], operation.HttpStatus,
                operation.ClientRequestId, operation.ProviderRequestId);
        }
        return OpenAiStagedResponseEnvelope.Replay(
            operation.RawResponseBytes ?? [], operation.ResponseHeadersBytes, operation.ClientRequestId);
    }

    public ProviderTerminalPublicationArtifacts PublishTerminalV2(
        OpaqueId runId,
        OpaqueId localRunOutputV1Id,
        byte[] canonicalLocalRunOutputV1,
        byte[] canonicalLocalCliSummaryV1,
        OpaqueId operationId,
        DateTimeOffset createdAt)
    {
        ProviderOperationReadModel operation = store.ReadProviderOperation(operationId.Value);
        ProviderOperationKind operationKind = operation.OperationKind switch
        {
            "transport-qualification" => ProviderOperationKind.TransportQualification,
            "source-claim-extraction" => ProviderOperationKind.SourceClaimExtraction,
            "candidate-investigation" => ProviderOperationKind.CandidateInvestigation,
            _ => throw new InvalidDataException("The persisted provider operation kind is outside the closed WP5 profile."),
        };
        OpaqueId? acquisitionRunId = null;
        OpaqueId? admissionId = null;
        IReadOnlyList<OpaqueId> acquisitionRunIds = [];
        if (operationKind == ProviderOperationKind.SourceClaimExtraction)
        {
            if (operation.OwnerKind != "evidence-acquisition-run")
            {
                throw new InvalidDataException("Source-claim publication requires evidence-acquisition ownership.");
            }
            IReadOnlyList<ProviderSemanticAdmissionReadModel> admissions = store.ReadSourceClaimAdmissions(operation.OwnerId);
            if (admissions.Count == 0 || admissions.Any(x => x.OperationId != operation.OperationId
                || x.ResponseRecordId != operation.ResponseId))
            {
                throw new InvalidDataException("Source-claim publication requires exact retained semantic admissions.");
            }
            acquisitionRunId = new(operation.OwnerId);
            acquisitionRunIds = [acquisitionRunId];
            admissionId = new(admissions.OrderByDescending(x => x.DecisionState == "admitted")
                .ThenBy(x => x.AdmissionId, StringComparer.Ordinal).First().AdmissionId);
        }
        else if (operationKind == ProviderOperationKind.CandidateInvestigation)
        {
            if (operation.OwnerKind != "analysis-run")
            {
                throw new InvalidDataException("Candidate publication requires analysis-run ownership.");
            }
            IReadOnlyList<ProviderSemanticAdmissionReadModel> admissions =
                store.ReadCandidateInvestigationAdmissionsForOperation(operation.OwnerId, operation.OperationId);
            IReadOnlyList<CandidateInvestigationOutcomeIdentityReadModel> outcomes =
                store.ReadCandidateInvestigationOutcomesForOperation(operation.OwnerId, operation.OperationId);
            if (outcomes.Count == 0 || outcomes.Any(x => x.ResponseRecordId != operation.ResponseId)
                || admissions.Any(x => x.ResponseRecordId != operation.ResponseId))
            {
                throw new InvalidDataException("Candidate publication requires exact retained terminal outcomes and any semantic admissions.");
            }
            if (admissions.Count > 0)
            {
                admissionId = new(admissions.OrderByDescending(x => x.DecisionState == "admitted")
                    .ThenBy(x => x.AdmissionId, StringComparer.Ordinal).First().AdmissionId);
            }
        }
        ProviderRunOutputV2BindingReceipt binding = store.BindProviderRunOutputV2(
            runId.Value, operation.EffectiveConfigurationId, canonicalLocalRunOutputV1, createdAt);
        ProviderPublicationReferenceContract publication = new(
            operationId, operationKind, acquisitionRunId, new(operation.AuthorizationId), new(operation.ResponseId), admissionId,
            new(operation.UsageEntryId), operation.SettlementId is null ? null : new(operation.SettlementId),
            new(operation.ReplayEdgeId), "live", true,
            OpenAiResponsesCanonicalSerializer.InputBoundPolicyId,
            OpenAiResponsesCanonicalSerializer.InputBoundPolicyVersion,
            new(operation.AuthorizationId));
        RunOutputV2Document runOutput = ProviderContractFactories.CreateRunOutputV2Supplement(
            runId, localRunOutputV1Id, canonicalLocalRunOutputV1, new(operation.EffectiveConfigurationId),
            [publication], acquisitionRunIds, [], [], []);
        OpenAiResponsesResult replay = Replay(new(operationId, new(operation.ResponseId), false));
        ProviderOperationSummaryProjection projection = QueryOperation(new(operationId, true, true, true));
        CliSummaryV2Document cli = ProviderContractFactories.CreateTerminalCliSummaryV2Supplement(
            runId, canonicalLocalCliSummaryV1, projection, replay.Usage, new(operation.AuthorizationId), []);
        return new(binding, runOutput, cli);
    }

    public ProviderTerminalPublicationArtifacts PublishCandidateNoResponseV2(
        OpaqueId runId,
        OpaqueId localRunOutputV1Id,
        byte[] canonicalLocalRunOutputV1,
        byte[] canonicalLocalCliSummaryV1,
        OpaqueId operationId,
        DateTimeOffset createdAt)
    {
        CandidateInvestigationNoResponsePublicationReadModel outcome =
            store.ReadCandidateInvestigationNoResponsePublication(runId.Value, operationId.Value);
        bool unavailable = outcome.TranscriptState == "unavailable";
        if (!unavailable && outcome.TranscriptState != "not-used")
        {
            throw new InvalidDataException("Candidate no-response publication requires a not-used or unavailable terminal outcome.");
        }
        ProviderRunOutputV2BindingReceipt binding = store.BindProviderRunOutputV2(
            runId.Value, outcome.EffectiveConfigurationId, canonicalLocalRunOutputV1, createdAt);
        ProviderPublicationReferenceContract publication = new(
            null, null, null, null, null, null, null, null, null,
            unavailable ? "unavailable" : "not-used", false);
        RunOutputV2Document runOutput = ProviderContractFactories.CreateRunOutputV2Supplement(
            runId, localRunOutputV1Id, canonicalLocalRunOutputV1, new(outcome.EffectiveConfigurationId),
            [publication], [], [], [], [outcome.Disposition]);
        CliSummaryV2Document cli = ProviderContractFactories.CreateProviderNotUsedCliSummaryV2Supplement(
            runId, canonicalLocalCliSummaryV1, unavailable, [outcome.Disposition]);
        return new(binding, runOutput, cli);
    }

    public ProviderBudgetSettlementReceipt SimulatePersistAndSettle(
        ProviderDispatchGateReceipt gate,
        string authorizationId,
        string operationId,
        string reservationId,
        string attemptId,
        string requestId,
        string identityPrefix,
        ProviderSimulatorOutcome outcome,
        ProviderFiniteLimitsContract limits,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identityPrefix);
        DeterministicProviderTranscript transcript = Simulate(
            gate, operationId, attemptId, requestId, outcome, limits, now);
        ProviderSimulationPersistenceReceipt? persisted = null;
        if (outcome != ProviderSimulatorOutcome.AmbiguousStart)
        {
            persisted = store.PersistProviderSimulation(new(
                identityPrefix + ":response",
                identityPrefix + ":usage",
                identityPrefix + ":receipt",
                identityPrefix + ":finalization",
                authorizationId,
                operationId,
                reservationId,
                attemptId,
                requestId,
                gate.DispatchFenceId,
                transcript.ResponseState,
                transcript.HttpStatus ?? 0,
                transcript.ReturnedModel,
                transcript.ReturnedServiceTier,
                transcript.ErrorCode,
                transcript.RefusalCode,
                transcript.IncompleteReason,
                transcript.Usage,
                transcript.RateFacts,
                CreateDeterministicRawResponse(transcript),
                now));
        }

        ProviderBudgetEventKind kind = outcome == ProviderSimulatorOutcome.AmbiguousStart
            ? ProviderBudgetEventKind.RetainedAmbiguous
            : persisted!.SettlementKind;
        return store.SettleProviderBudget(new(
            identityPrefix + ":settlement",
            reservationId,
            kind,
            kind is ProviderBudgetEventKind.ReleasedUndispatched or ProviderBudgetEventKind.RetainedPartial
                or ProviderBudgetEventKind.RetainedUnavailable ? null : persisted?.UsageEntryId,
            kind is ProviderBudgetEventKind.ReleasedUndispatched or ProviderBudgetEventKind.RetainedPartial
                or ProviderBudgetEventKind.RetainedUnavailable ? null : persisted?.Actual,
            now));
    }

    private static byte[]? CreateDeterministicRawResponse(DeterministicProviderTranscript transcript)
    {
        if (!transcript.RawResponseAvailable)
        {
            return null;
        }
        byte[] prefix = Encoding.UTF8.GetBytes(
            $"simulated:{transcript.Outcome}:{transcript.ResponseState}:{transcript.HttpStatus}");
        int length = checked((int)transcript.RawResponseBytes);
        if (prefix.Length > length)
        {
            throw new InvalidOperationException("The deterministic simulator response identity exceeds its retained byte claim.");
        }
        byte[] result = new byte[length];
        prefix.CopyTo(result, 0);
        Array.Fill(result, (byte)' ', prefix.Length, result.Length - prefix.Length);
        return result;
    }
}
