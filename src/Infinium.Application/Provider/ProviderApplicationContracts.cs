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
    ProviderOperationKind OperationKind,
    string OwnerKind,
    OpaqueId OwnerId,
    OpaqueId JobNodeId,
    OpaqueId InstallationSnapshotId,
    OpaqueId AnalysisContextId,
    OpaqueId EffectiveConfigurationId,
    OpaqueId ResolvedInputManifestId,
    OpaqueId ProfileId,
    OpaqueId GenerationId,
    long RevocationEpoch,
    OpaqueId CapabilitySnapshotId,
    Sha256Fingerprint RequestFingerprint,
    Sha256Fingerprint CanonicalRequestFingerprint,
    ReadOnlyMemory<byte> CanonicalRequest,
    long CanonicalRequestBytes,
    Sha256Fingerprint CapabilityFingerprint,
    OpaqueId PriceSnapshotId,
    Sha256Fingerprint PriceFingerprint,
    Sha256Fingerprint SettingsFingerprint,
    OpaqueId PromptId,
    Sha256Fingerprint PromptFingerprint,
    OpaqueId OutputSchemaId,
    Sha256Fingerprint OutputSchemaFingerprint,
    ProviderInputBoundProofContract InputBoundProof,
    ProviderFiniteLimitsContract Limits,
    UtcTimestamp DispatchDeadline,
    long CoordinatorFencingEpoch,
    UtcTimestamp RequestedAt,
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
    OpaqueId? RetainedResponseId,
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
        if (query.ScopeKind is not ("request" or "operation" or "evidence-acquisition-run" or "analysis-run" or "provider-profile" or "provider-account" or "billing-scope" or "global")
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

    public static void Validate(SelectAndConfirmProviderOperationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ProviderOperationContractInvariants.Validate(command.OperationKind, command.Limits);
        OpenAiResponsesInputBoundPolicy.ValidateProofIdentity(command.InputBoundProof);
        if (command.OwnerKind is not ("analysis-run" or "evidence-acquisition-run")
            || command.RevocationEpoch < 0 || command.CoordinatorFencingEpoch <= 0
            || command.CanonicalRequestBytes <= 0 || command.CanonicalRequestBytes > command.Limits.MaximumRequestBytes
            || command.CanonicalRequest.Length != command.CanonicalRequestBytes
            || command.RequestFingerprint != command.CanonicalRequestFingerprint
            || !System.Security.Cryptography.SHA256.HashData(command.CanonicalRequest.Span)
                .AsSpan().SequenceEqual(Convert.FromHexString(command.RequestFingerprint.Value))
            || command.RequestedAt.Value > command.ConfirmedAt.Value
            || command.DispatchDeadline.Value <= command.ConfirmedAt.Value
            || command.DispatchDeadline.Value - command.ConfirmedAt.Value
                > TimeSpan.FromMilliseconds(command.Limits.DeadlineMilliseconds)
            || command.DispatchDeadline.Value - command.RequestedAt.Value
                > TimeSpan.FromMilliseconds(command.Limits.DeadlineMilliseconds))
        {
            throw new InvalidOperationException("Provider confirmation must retain exact owner, snapshot, configuration, proof, deadline, and fencing bindings.");
        }
        ProviderInputBoundEvidence evidence = OpenAiResponsesInputBoundPolicy.Prove(
            command.OperationKind,
            command.CanonicalRequest,
            command.Limits);
        if (evidence.Proof != command.InputBoundProof
            || evidence.CanonicalRequestFingerprint != command.CanonicalRequestFingerprint)
        {
            throw new InvalidOperationException("Provider confirmation input-bound evidence does not match the exact canonical request.");
        }
    }
}
