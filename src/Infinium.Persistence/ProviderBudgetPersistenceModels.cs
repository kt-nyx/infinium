using Infinium.Domain.Contracts;

namespace Infinium.Persistence;

public sealed record ProviderBudgetReservationRequest(
    string ReservationId,
    string OperationId,
    string AttemptId,
    string RequestId,
    ProviderBudgetVectorContract Reserved,
    IReadOnlyList<ProviderBudgetScopeContract> Scopes,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

public sealed record ProviderDispatchGateRequest(
    string DispatchFenceId,
    string AuthorizationId,
    string OperationId,
    string ReservationId,
    string AttemptId,
    string RequestId,
    string ProfileId,
    string GenerationId,
    long RevocationEpoch,
    long CoordinatorFencingEpoch,
    DateTimeOffset EvaluatedAt);

public sealed record ProviderBudgetSettlementRequest(
    string SettlementId,
    string ReservationId,
    ProviderBudgetEventKind Kind,
    string? UsageEntryId,
    ProviderBudgetVectorContract? Actual,
    DateTimeOffset OccurredAt);

public sealed record ProviderDispatchGateReceipt(
    string DispatchFenceId,
    string ReservationId,
    long CoordinatorFencingEpoch,
    DateTimeOffset EffectiveGateTime,
    DateTimeOffset Deadline,
    bool Authorized,
    string DecisionReason);

public sealed record ProviderDispatchAuthoritySnapshot(
    ProviderDispatchGateRequest Gate,
    string AccountIdentityId,
    string BillingScopeIdentityId,
    long GenerationOrdinal,
    string OperationKind,
    string EffectiveConfigurationId,
    string CapabilitySnapshotId,
    string PriceSnapshotId,
    string RequestFingerprintSha256,
    string CanonicalRequestFingerprintSha256,
    long CanonicalRequestBytes,
    string SettingsFingerprintSha256,
    string OutputSchemaFingerprintSha256,
    string InputBoundPolicyId,
    string InputBoundPolicyVersion,
    string InputBoundProofStatus,
    long MaximumRequestBytes,
    long MaximumInputTokens,
    long MaximumOutputTokens,
    long MaximumRawResponseBytes,
    long MaximumDispatchCount,
    long MaximumCalculatedNanoUsd,
    long DeadlineMilliseconds,
    DateTimeOffset DispatchDeadline,
    DateTimeOffset ConfirmedAt);


public sealed record ProviderBudgetSettlementReceipt(
    string SettlementId,
    string ReservationId,
    ProviderBudgetEventKind Kind,
    ProviderBudgetVectorContract Released,
    ProviderBudgetVectorContract Settled,
    ProviderBudgetVectorContract Unresolved,
    bool RetryPermitted);

internal enum ProviderBudgetFaultPoint
{
    None,
    AfterReservationRootBeforeScopeEvents,
}

public sealed record ProviderSimulationPersistenceRequest(
    string ResponseId,
    string UsageEntryId,
    string ReceiptId,
    string FinalizationId,
    string AuthorizationId,
    string OperationId,
    string ReservationId,
    string AttemptId,
    string RequestId,
    string DispatchFenceId,
    ProviderResponseState ResponseState,
    int HttpStatus,
    string? ReturnedModel,
    string? ReturnedServiceTier,
    string? ErrorCode,
    string? RefusalCode,
    string? IncompleteReason,
    ProviderUsageContract Usage,
    IReadOnlyList<ProviderRateLimitFactContract> RateFacts,
    byte[]? RawResponseBytes,
    DateTimeOffset OccurredAt,
    byte[]? ResponseHeadersBytes = null,
    string? ProviderResponseId = null,
    string? ProviderRequestId = null,
    bool? Admitted = null);

public sealed record ProviderSimulationPersistenceReceipt(
    string ResponseId,
    string UsageEntryId,
    ProviderBudgetVectorContract Actual,
    ProviderBudgetEventKind SettlementKind);

public sealed record ProviderOperationReadModel(
    string OperationId,
    ProviderOperationState State,
    long ReservedNanoUsd,
    long CalculatedNanoUsd,
    bool UnresolvedHold,
    string ReplayState,
    string ResponseId,
    int HttpStatus,
    string ClientRequestId,
    string? ProviderRequestId,
    string? ProviderResponseId,
    byte[]? RawResponseBytes,
    byte[]? ResponseHeadersBytes,
    string ReplayEdgeId,
    string AuthorizationId,
    string OperationKind,
    string EffectiveConfigurationId,
    string UsageEntryId,
    string? SettlementId);

public sealed record ProviderRunOutputV2BindingReceipt(
    string RunId,
    string EffectiveConfigurationV2Id,
    string LocalRunOutputV1PayloadId,
    string LocalRunOutputV1Fingerprint,
    long LocalRunOutputV1Bytes);
