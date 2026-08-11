using Infinium.Domain.Contracts;

namespace Infinium.Application.Provider;

public sealed record BeginProviderEnrollmentIntentCommand(
    OpaqueId CommandId,
    OpaqueId ProfileId,
    OpaqueId GenerationId,
    string Provider,
    string Purpose,
    string DisplayLabel,
    UtcTimestamp RequestedAt);

public sealed record SelectAndConfirmProviderOperationCommand(
    OpaqueId CommandId,
    OpaqueId OperationId,
    OpaqueId ProfileId,
    OpaqueId GenerationId,
    long RevocationEpoch,
    Sha256Fingerprint RequestFingerprint,
    Sha256Fingerprint CapabilityFingerprint,
    Sha256Fingerprint PriceFingerprint,
    ProviderFiniteLimitsContract Limits,
    UtcTimestamp ConfirmedAt);

public sealed record ProviderProfileQuery(
    OpaqueId ProfileId,
    bool IncludeHistoricalGenerations);

public sealed record ProviderOperationQuery(
    OpaqueId OperationId,
    bool IncludeUsage,
    bool IncludeSettlement,
    bool IncludeReplay);

public sealed record ProviderBudgetQuery(
    OpaqueId ScopeId,
    string ScopeKind,
    int MaximumItems);

public sealed record ProviderReplayQuery(
    OpaqueId OperationId,
    OpaqueId RetainedResponseId,
    bool NetworkPermitted);

public sealed record ProviderOperationSummaryProjection(
    OpaqueId OperationId,
    ProviderOperationState State,
    string Provider,
    string Model,
    long ReservedNanoUsd,
    long CalculatedNanoUsd,
    bool UnresolvedHold,
    string ReplayState,
    IReadOnlyList<string> Gaps);

public static class ProviderApplicationContractInvariants
{
    public const int MaximumBudgetQueryItems = 100;

    public static void Validate(ProviderBudgetQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.ScopeKind is not ("operation" or "evidence-acquisition-run" or "analysis-run" or "provider-profile" or "provider-account" or "global")
            || query.MaximumItems is <= 0 or > MaximumBudgetQueryItems)
        {
            throw new InvalidOperationException("Provider budget queries must use a closed scope and finite page bound.");
        }
    }

    public static void Validate(ProviderReplayQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.NetworkPermitted)
        {
            throw new InvalidOperationException("Retained-response replay never permits a provider request.");
        }
    }
}
