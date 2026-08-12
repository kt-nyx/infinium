using System.Text;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Infinium.Persistence;

namespace Infinium.Coordinator;

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
            return OpenAiResponsesResponseCodec.Replay(operation.RawResponseBytes, operation.HttpStatus,
                operation.ClientRequestId, operation.ProviderRequestId);
        }
        return OpenAiStagedResponseEnvelope.Replay(
            operation.RawResponseBytes, operation.ResponseHeadersBytes, operation.ClientRequestId);
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
            persisted?.UsageEntryId,
            kind == ProviderBudgetEventKind.ReleasedUndispatched ? null : persisted?.Actual,
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
