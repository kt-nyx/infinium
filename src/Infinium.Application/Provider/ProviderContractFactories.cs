using System.Security.Cryptography;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Provider;

public static class ProviderContractFactories
{
    public static EffectiveScanConfigurationV2Document CreateEffectiveConfigurationV2(
        OpaqueId configurationId,
        OpaqueId localConfigurationV1Id,
        ReadOnlySpan<byte> canonicalLocalConfigurationV1,
        OpaqueId accessProfileId,
        OpaqueId generationId,
        ProviderFiniteLimitsContract limits)
    {
        if (canonicalLocalConfigurationV1.IsEmpty)
        {
            throw new ArgumentException("The frozen local v1 configuration bytes are required.", nameof(canonicalLocalConfigurationV1));
        }

        EffectiveScanConfigurationV2Document result = new(
            ContractConstants.EffectiveScanConfigurationV2SchemaId,
            "1",
            configurationId,
            localConfigurationV1Id,
            Fingerprint(canonicalLocalConfigurationV1),
            accessProfileId,
            generationId,
            "gpt-5.6-sol",
            "medium",
            "current_turn",
            "standard",
            false,
            "default",
            false,
            false,
            "none",
            0,
            "disabled",
            "explicit",
            false,
            false,
            limits,
            ["hosted-search", "nexus", "loot"]);
        ProviderOperationContractInvariants.Validate(result);
        return result;
    }

    public static RunOutputV2Document CreateRunOutputV2Supplement(
        OpaqueId runId,
        OpaqueId localRunOutputV1Id,
        ReadOnlySpan<byte> canonicalLocalRunOutputV1,
        OpaqueId effectiveConfigurationV2Id,
        IReadOnlyList<ProviderPublicationReferenceContract> providerOperations,
        IReadOnlyList<OpaqueId> evidenceAcquisitionRunIds,
        IReadOnlyList<OpaqueId> capabilityDriftIds,
        IReadOnlyList<OpaqueId> priceDriftIds,
        IReadOnlyList<string> providerGaps)
    {
        if (canonicalLocalRunOutputV1.IsEmpty)
        {
            throw new ArgumentException("The frozen local run-output v1 bytes are required.", nameof(canonicalLocalRunOutputV1));
        }

        RunOutputV2Document result = new(
            ContractConstants.RunOutputV2SchemaId,
            "1",
            runId,
            new ProviderIdentityReferenceContract(localRunOutputV1Id, Fingerprint(canonicalLocalRunOutputV1)),
            effectiveConfigurationV2Id,
            providerOperations,
            evidenceAcquisitionRunIds,
            capabilityDriftIds,
            priceDriftIds,
            providerGaps,
            false,
            false);
        ProviderOperationContractInvariants.Validate(result);
        return result;
    }

    public static CliSummaryV2Document CreateCliSummaryV2Supplement(
        OpaqueId runId,
        ReadOnlySpan<byte> canonicalLocalCliSummaryV1,
        ProviderOperationSummaryProjection projection,
        long dispatchCount,
        long inputTokens,
        long outputTokens,
        long reasoningTokens,
        IReadOnlyList<string> gaps)
    {
        ArgumentNullException.ThrowIfNull(projection);
        if (canonicalLocalCliSummaryV1.IsEmpty)
        {
            throw new ArgumentException("The frozen local CLI-summary v1 bytes are required.", nameof(canonicalLocalCliSummaryV1));
        }

        string providerState = ProviderState(projection);
        ProviderQuantityContract absent = new(ProviderAvailabilityState.Unavailable, null);
        bool pending = providerState == "pending";
        bool live = providerState == "live";
        bool hasReservation = projection.State is ProviderOperationState.Reserved or ProviderOperationState.Assigned
            or ProviderOperationState.FinalGateAuthorized or ProviderOperationState.TransportNotStarted;
        CliSummaryV2Document result = new(
            ContractConstants.CliSummaryV2SchemaId,
            "1",
            runId,
            Fingerprint(canonicalLocalCliSummaryV1),
            providerState,
            pending ? absent : Available(dispatchCount),
            pending || live ? absent : Available(inputTokens),
            pending || live ? absent : Available(outputTokens),
            pending || live ? absent : Available(reasoningTokens),
            pending || live ? absent : Available(0),
            pending || live ? absent : Available(0),
            pending || live ? absent : Available(projection.CalculatedNanoUsd),
            hasReservation || live || providerState is "completed" or "failed" or "unresolved"
                ? Available(projection.ReservedNanoUsd)
                : absent,
            projection.UnresolvedHold,
            projection.ReplayState,
            gaps,
            false,
            false);
        ProviderOperationContractInvariants.Validate(result);
        return result;
    }

    public static CliSummaryV2Document CreateProviderNotUsedCliSummaryV2Supplement(
        OpaqueId runId,
        ReadOnlySpan<byte> canonicalLocalCliSummaryV1,
        bool unavailable,
        IReadOnlyList<string> gaps)
    {
        ProviderAvailabilityState availability = unavailable
            ? ProviderAvailabilityState.Unavailable
            : ProviderAvailabilityState.NotUsed;
        ProviderQuantityContract absent = new(availability, null);
        CliSummaryV2Document result = new(
            ContractConstants.CliSummaryV2SchemaId, "1", runId,
            Fingerprint(canonicalLocalCliSummaryV1), unavailable ? "unavailable" : "not-used",
            absent, absent, absent, absent, absent, absent, absent, absent,
            false, "not-available", gaps, false, false);
        ProviderOperationContractInvariants.Validate(result);
        return result;
    }

    private static Sha256Fingerprint Fingerprint(ReadOnlySpan<byte> value) =>
        new(Convert.ToHexStringLower(SHA256.HashData(value)));

    private static ProviderQuantityContract Available(long value) =>
        new(ProviderAvailabilityState.Available, value);

    private static string ProviderState(ProviderOperationSummaryProjection value) => value.State switch
    {
        ProviderOperationState.UnresolvedHold => "unresolved",
        ProviderOperationState.Proposed or ProviderOperationState.Confirmed or ProviderOperationState.Reserved
            or ProviderOperationState.Assigned or ProviderOperationState.InputBoundBlocked or ProviderOperationState.FinalGateAuthorized
            or ProviderOperationState.TransportNotStarted => "pending",
        ProviderOperationState.TransportMayHaveStarted or ProviderOperationState.ResponseStaged => "live",
        ProviderOperationState.Admitted or ProviderOperationState.Settled => "completed",
        ProviderOperationState.Rejected => "failed",
        _ => throw new InvalidOperationException("Provider operation state cannot be projected to a CLI provider state."),
    };
}
