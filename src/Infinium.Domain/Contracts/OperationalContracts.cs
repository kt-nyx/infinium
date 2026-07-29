namespace Infinium.Domain.Contracts;

public enum LifecycleState
{
    Unspecified,
    Queued,
    Running,
    Waiting,
    Retrying,
    Pausing,
    Paused,
    Cancelling,
    Cancelled,
    Completed,
    CompletedWithGaps,
    Failed,
    LimitReached,
    InvalidatedByChangedInput,
}

public enum ProcessRole
{
    Unspecified,
    ApplicationClient,
    Coordinator,
    GeneralWorker,
    CredentialProviderHelper,
}

public enum SettlementState
{
    Unspecified,
    Reserved,
    Dispatched,
    Completed,
    FailedKnown,
    AbortUnknown,
    Overrun,
    Unresolved,
}

public sealed record LifecycleTransitionContract(
    OpaqueId TransitionId,
    OpaqueId OwnerRunId,
    OpaqueId JobNodeId,
    LifecycleState From,
    LifecycleState To,
    long ExpectedGeneration,
    long NewGeneration,
    long CoordinatorFencingEpoch,
    UtcTimestamp OccurredAt,
    string Reason);

public sealed record CoordinatorLeaseContract(
    OpaqueId CoordinatorInstanceId,
    long FencingEpoch,
    UtcTimestamp AcquiredAt,
    UtcTimestamp ExpiresAt);

public sealed record AttemptContract(
    OpaqueId AttemptId,
    OpaqueId OwnerRunId,
    OpaqueId JobNodeId,
    long CoordinatorFencingEpoch,
    string DispatchIdentity,
    bool RetrySafe,
    UtcTimestamp CreatedAt);

public sealed record CheckpointContract(
    OpaqueId CheckpointId,
    OpaqueId OwnerRunId,
    OpaqueId JobNodeId,
    OpaqueId AttemptId,
    OpaqueId DependencyClosureId,
    Sha256Fingerprint ContentFingerprint,
    IReadOnlyList<string> CompletedPartitions,
    IReadOnlyList<string> PendingAndGapStates,
    UtcTimestamp CreatedAt);

public sealed record PublicationReceiptContract(
    OpaqueId ReceiptId,
    OpaqueId AttemptId,
    long CoordinatorFencingEpoch,
    IReadOnlyList<OpaqueId> AdmittedArtifactIds,
    Sha256Fingerprint StagedManifestFingerprint,
    UtcTimestamp PublishedAt);

public sealed record PayloadManifestContract(
    OpaqueId PayloadId,
    Sha256Fingerprint ContentFingerprint,
    long ByteLength,
    string Codec,
    string RetentionState,
    IReadOnlyList<OpaqueId> LogicalOwnerIds);

public sealed record ProviderAccessProfileContract(
    OpaqueId ProfileId,
    ContractVersion SchemaVersion,
    string Provider,
    string Purpose,
    long CredentialGeneration,
    long RevocationEpoch,
    string LifecycleState,
    OpaqueId CapabilitySnapshotId);

public sealed record ProviderRequestAssignmentContract(
    OpaqueId AssignmentId,
    OpaqueId OwnerRunId,
    OpaqueId AttemptId,
    OpaqueId ProviderProfileId,
    long CredentialGeneration,
    long RevocationEpoch,
    string Provider,
    string Purpose,
    string EndpointShape,
    OpaqueId BudgetReservationId,
    UtcTimestamp DispatchDeadline,
    OpaqueId StagingIdentity);

public sealed record ConsumptionVectorContract(
    long DispatchCount,
    long InputTokens,
    long OutputAndReasoningTokens,
    long PricedToolCalls,
    long NanoUsd);

public sealed record BudgetReservationContract(
    OpaqueId ReservationId,
    OpaqueId AttemptId,
    ConsumptionVectorContract WorstCase,
    IReadOnlyList<OpaqueId> ApplicableLimitScopeIds,
    UtcTimestamp CreatedAt);

public sealed record DispatchFenceContract(
    OpaqueId DispatchFenceId,
    OpaqueId ReservationId,
    OpaqueId AttemptId,
    long CoordinatorFencingEpoch,
    long AttemptGeneration,
    long CredentialGeneration,
    long RevocationEpoch,
    UtcTimestamp Deadline,
    bool Authorized,
    string DecisionReason,
    UtcTimestamp EvaluatedAt);

public sealed record UsageLedgerEntryContract(
    OpaqueId EntryId,
    OpaqueId AttemptId,
    ConsumptionVectorContract Actual,
    SettlementState Settlement,
    OpaqueId CapabilitySnapshotId,
    OpaqueId PriceCatalogId,
    UtcTimestamp RecordedAt);

public sealed record ProcessBootstrapContract(
    OpaqueId CoordinatorInstanceId,
    long FencingEpoch,
    ProcessRole ExpectedRole,
    OpaqueId AssignmentId,
    Sha256Fingerprint OneUseNonceFingerprint,
    ContractVersion ProtocolVersion,
    UtcTimestamp ExpiresAt);

public sealed record ProtocolNegotiationContract(
    ContractVersion ProtocolVersion,
    ContractVersion DomainContractVersion,
    ContractVersion StorageContractVersion,
    OpaqueId CoordinatorInstanceId,
    long FencingEpoch,
    IReadOnlyList<string> CapabilityFlags,
    Sha256Fingerprint InstanceNonceFingerprint);

public sealed record KeysetCursorContract(
    string OpaqueCursor,
    Sha256Fingerprint QueryShapeFingerprint,
    ContractVersion ProjectionVersion,
    int MaximumPageSize);

public sealed record EventCursorContract(
    OpaqueId CoordinatorInstanceId,
    long FencingEpoch,
    OpaqueId SubscriptionId,
    long DurableSequence,
    ContractVersion ProjectionVersion,
    string Scope);

public sealed record WorkerAssignmentContract(
    OpaqueId AssignmentId,
    OpaqueId AttemptId,
    long FencingEpoch,
    OpaqueId InputManifestId,
    string AttemptStagingRelativeName,
    long MaximumOutputBytes,
    UtcTimestamp Deadline);

public sealed record StagedOutputManifestContract(
    OpaqueId AssignmentId,
    OpaqueId AttemptId,
    IReadOnlyList<PayloadManifestContract> Payloads,
    Sha256Fingerprint ManifestFingerprint);
