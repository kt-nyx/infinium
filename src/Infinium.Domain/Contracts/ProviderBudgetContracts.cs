namespace Infinium.Domain.Contracts;

public enum ProviderBudgetEventKind
{
    Unspecified,
    Reserved,
    ReleasedUndispatched,
    SettledComplete,
    SettledFailedKnown,
    RetainedAmbiguous,
    RetainedPartial,
    RetainedUnavailable,
    SettledOverrun,
    Adjustment,
}

public enum ProviderSimulatorOutcome
{
    Unspecified,
    Completed,
    Refusal,
    Incomplete,
    Failed,
    Queued,
    InProgress,
    Malformed,
    Oversized,
    ReturnedModelMismatch,
    ReturnedTierMismatch,
    RateLimitedWithReset,
    RateLimitedWithoutReset,
    AmbiguousStart,
    KnownUndispatched,
}

public sealed record ProviderBudgetVectorContract(
    long DispatchCount,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    long ReasoningTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long PricedToolCalls,
    long NanoUsd)
{
    public static ProviderBudgetVectorContract Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);

    public static ProviderBudgetVectorContract Add(
        ProviderBudgetVectorContract left,
        ProviderBudgetVectorContract right)
    {
        Validate(left);
        Validate(right);
        return new(
            checked(left.DispatchCount + right.DispatchCount),
            checked(left.InputTokens + right.InputTokens),
            checked(left.OutputTokens + right.OutputTokens),
            checked(left.TotalTokens + right.TotalTokens),
            checked(left.ReasoningTokens + right.ReasoningTokens),
            checked(left.CacheReadTokens + right.CacheReadTokens),
            checked(left.CacheWriteTokens + right.CacheWriteTokens),
            checked(left.PricedToolCalls + right.PricedToolCalls),
            checked(left.NanoUsd + right.NanoUsd));
    }

    public static ProviderBudgetVectorContract Subtract(
        ProviderBudgetVectorContract left,
        ProviderBudgetVectorContract right)
    {
        Validate(left);
        Validate(right);
        ProviderBudgetVectorContract result = new(
            checked(left.DispatchCount - right.DispatchCount),
            checked(left.InputTokens - right.InputTokens),
            checked(left.OutputTokens - right.OutputTokens),
            checked(left.TotalTokens - right.TotalTokens),
            checked(left.ReasoningTokens - right.ReasoningTokens),
            checked(left.CacheReadTokens - right.CacheReadTokens),
            checked(left.CacheWriteTokens - right.CacheWriteTokens),
            checked(left.PricedToolCalls - right.PricedToolCalls),
            checked(left.NanoUsd - right.NanoUsd));
        Validate(result);
        return result;
    }

    public static bool FitsWithin(
        ProviderBudgetVectorContract used,
        ProviderBudgetVectorContract requested,
        ProviderBudgetVectorContract limit)
    {
        Validate(used);
        Validate(requested);
        Validate(limit);
        try
        {
            ProviderBudgetVectorContract combined = Add(used, requested);
            return combined.DispatchCount <= limit.DispatchCount
                && combined.InputTokens <= limit.InputTokens
                && combined.OutputTokens <= limit.OutputTokens
                && combined.TotalTokens <= limit.TotalTokens
                && combined.ReasoningTokens <= limit.ReasoningTokens
                && combined.CacheReadTokens <= limit.CacheReadTokens
                && combined.CacheWriteTokens <= limit.CacheWriteTokens
                && combined.PricedToolCalls <= limit.PricedToolCalls
                && combined.NanoUsd <= limit.NanoUsd;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    public static void Validate(ProviderBudgetVectorContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.DispatchCount < 0 || value.InputTokens < 0 || value.OutputTokens < 0
            || value.TotalTokens < 0 || value.ReasoningTokens < 0
            || value.CacheReadTokens < 0 || value.CacheWriteTokens < 0
            || value.PricedToolCalls < 0 || value.NanoUsd < 0
            || value.TotalTokens != checked(value.InputTokens + value.OutputTokens)
            || value.ReasoningTokens > value.OutputTokens)
        {
            throw new InvalidOperationException(
                "Provider budget vectors require finite non-negative dimensions, total=input+output, and reasoning<=output.");
        }
    }
}

public sealed record ProviderBudgetScopeContract(
    string ScopeKind,
    OpaqueId ScopeId,
    ProviderBudgetVectorContract HardLimit);

public sealed record ProviderReservationAdmissionContract(
    OpaqueId ReservationId,
    OpaqueId OperationId,
    OpaqueId AttemptId,
    OpaqueId RequestId,
    ProviderBudgetVectorContract Reserved,
    IReadOnlyList<ProviderBudgetScopeContract> Scopes,
    UtcTimestamp ExpiresAt,
    UtcTimestamp CreatedAt);

public sealed record ProviderDispatchGateFactsContract(
    OpaqueId AuthorizationId,
    OpaqueId OperationId,
    OpaqueId ReservationId,
    OpaqueId AttemptId,
    OpaqueId RequestId,
    OpaqueId ProfileId,
    OpaqueId GenerationId,
    long RevocationEpoch,
    long CoordinatorFencingEpoch,
    bool OwnerEligible,
    bool PauseRequested,
    bool CancelRequested,
    bool DeletePending,
    bool PriorTransportStart,
    bool AmbiguousTransportStart,
    UtcTimestamp EffectiveGateTime,
    UtcTimestamp Deadline);

public sealed record ProviderBudgetProjectionContract(
    string ScopeKind,
    OpaqueId ScopeId,
    ProviderBudgetVectorContract Reserved,
    ProviderBudgetVectorContract Settled,
    ProviderBudgetVectorContract Unresolved,
    long ProjectionVersion,
    UtcTimestamp UpdatedAt);

public static class ProviderBudgetContractInvariants
{
    private static readonly string[] ScopeKinds =
    [
        "operation",
        "request",
        "evidence-acquisition-run",
        "analysis-run",
        "provider-profile",
        "provider-account",
        "billing-scope",
        "global",
    ];

    public static void Validate(ProviderBudgetScopeContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!ScopeKinds.Contains(value.ScopeKind, StringComparer.Ordinal)
            || string.IsNullOrWhiteSpace(value.ScopeId.Value))
        {
            throw new InvalidOperationException("Provider budget scope must use one exact closed identity kind.");
        }
        ProviderBudgetVectorContract.Validate(value.HardLimit);
    }

    public static void Validate(ProviderReservationAdmissionContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ProviderBudgetVectorContract.Validate(value.Reserved);
        if (value.Reserved.DispatchCount != 1 || value.Reserved.CacheReadTokens != 0
            || value.Reserved.CacheWriteTokens != 0 || value.Reserved.PricedToolCalls != 0
            || value.Scopes.Count is < 7 or > 8
            || value.Scopes.Select(scope => (scope.ScopeKind, scope.ScopeId))
                .Distinct().Count() != value.Scopes.Count
            || value.ExpiresAt.Value <= value.CreatedAt.Value)
        {
            throw new InvalidOperationException("The provider reservation must be cache-off, tool-free, finite, exact, and multi-scope.");
        }
        foreach (ProviderBudgetScopeContract scope in value.Scopes)
        {
            Validate(scope);
        }
    }
}
