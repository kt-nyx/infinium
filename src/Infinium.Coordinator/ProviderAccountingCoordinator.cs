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
}
