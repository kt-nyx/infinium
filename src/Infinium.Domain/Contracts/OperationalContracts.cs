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

public enum LifecycleTransitionRecordKind
{
    Unspecified,
    Requested,
    Observed,
    Unknown,
    Unsupported,
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

public enum RetrySafety
{
    Unspecified,
    SafeWithNewAttempt,
    RequiresReconciliation,
    NotSafe,
}

public enum AttemptOutcome
{
    Unspecified,
    Pending,
    Running,
    CompletedStaged,
    CompletedWithGapsStaged,
    FailedKnown,
    AbortUnknown,
    Cancelled,
    RejectedStale,
}

public enum ProviderKind
{
    Unspecified,
    OpenAi,
}

public enum CredentialPurpose
{
    Unspecified,
    OpenAiResponses,
}

public enum ProviderEndpoint
{
    Unspecified,
    OpenAiResponsesV1,
}

public enum ProviderProfileLifecycleState
{
    Unspecified,
    Active,
    Replacing,
    Revoked,
    Deleted,
}

public enum ProviderVerificationState
{
    Unspecified,
    Unverified,
    Verified,
    Failed,
}

public enum BudgetLimitScopeKind
{
    Unspecified,
    Request,
    Operation,
    Owner,
    ProviderProfile,
    ProviderAccount,
    BillingScope,
    Global,
}

public enum OperationalFactAvailability
{
    Unspecified,
    Available,
    Unavailable,
}

public enum UsageReceiptState
{
    Unspecified,
    NotDispatched,
    Complete,
    Partial,
    FailedKnown,
    Ambiguous,
    Unavailable,
}

public enum StagedArtifactKind
{
    Unspecified,
    TypedResult,
    Checkpoint,
    Diagnostic,
    ApprovedReadOnlyToolOutput,
    ProviderResponse,
    NonSecretReceipt,
}

public abstract record OperationOwnerContract
{
    public abstract OpaqueId OwnerId { get; }
}

public sealed record AnalysisRunOwnerContract(OpaqueId AnalysisRunId) : OperationOwnerContract
{
    public override OpaqueId OwnerId => AnalysisRunId;
}

public sealed record EvidenceAcquisitionRunOwnerContract(OpaqueId EvidenceAcquisitionRunId) : OperationOwnerContract
{
    public override OpaqueId OwnerId => EvidenceAcquisitionRunId;
}

public sealed record MaintenanceOperationOwnerContract(OpaqueId MaintenanceOperationId) : OperationOwnerContract
{
    public override OpaqueId OwnerId => MaintenanceOperationId;
}

public sealed record LifecycleTransitionContract(
    OpaqueId TransitionId,
    OperationOwnerContract Owner,
    OpaqueId JobNodeId,
    LifecycleTransitionRecordKind RecordKind,
    ContractVersion PolicyVersion,
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

public sealed record AttemptLeaseContract(
    long AttemptFencingToken,
    UtcTimestamp AcquiredAt,
    UtcTimestamp ExpiresAt);

public sealed record AttemptContract(
    OpaqueId AttemptId,
    OperationOwnerContract Owner,
    OpaqueId JobNodeId,
    long AttemptGeneration,
    long CoordinatorFencingEpoch,
    AttemptLeaseContract Lease,
    OpaqueId DispatchIdentity,
    OpaqueId IdempotencyIdentity,
    RetrySafety RetrySafety,
    AttemptOutcome Outcome,
    UtcTimestamp CreatedAt);

public sealed record VersionedComponentContract(
    OpaqueId Identity,
    ContractVersion Version);

public sealed record CheckpointContract(
    OpaqueId CheckpointId,
    OperationOwnerContract Owner,
    OpaqueId JobNodeId,
    OpaqueId AttemptId,
    OpaqueId InstallationSnapshotId,
    OpaqueId AnalysisContextId,
    OpaqueId EffectiveScanConfigurationId,
    IReadOnlyList<OpaqueId> SourceRevisionIds,
    IReadOnlyList<VersionedComponentContract> ToolVersions,
    IReadOnlyList<VersionedComponentContract> ModelVersions,
    IReadOnlyList<VersionedComponentContract> AnalyzerVersions,
    IReadOnlyList<VersionedComponentContract> SchemaVersions,
    OpaqueId DependencyClosureId,
    IReadOnlyList<OpaqueId> UpstreamArtifactIds,
    IReadOnlyList<string> CompletedPartitions,
    IReadOnlyList<string> PendingAndGapStates,
    long ProgressPopulationRevision,
    IReadOnlyList<OpaqueId> AccountingReferences,
    Sha256Fingerprint ContentFingerprint,
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
    ProviderKind Provider,
    CredentialPurpose Purpose,
    string DisplayLabel,
    long CredentialGeneration,
    long RevocationEpoch,
    OpaqueId ProviderAccountIdentityId,
    OpaqueId BillingScopeIdentityId,
    ProviderProfileLifecycleState LifecycleState,
    ProviderVerificationState VerificationState,
    OpaqueId CapabilitySnapshotId);

public sealed record ProviderResponseBoundsContract(
    long MaximumResponseBytes,
    long MaximumInputTokens,
    long MaximumOutputAndReasoningTokens,
    long MaximumPricedToolCalls,
    long MaximumCalculatedNanoUsd);

public sealed record ProviderRequestAssignmentContract(
    OpaqueId AssignmentId,
    OperationOwnerContract Owner,
    OpaqueId JobNodeId,
    OpaqueId AttemptId,
    OpaqueId ProviderProfileId,
    long CredentialGeneration,
    long RevocationEpoch,
    ProviderKind Provider,
    CredentialPurpose Purpose,
    ProviderEndpoint Endpoint,
    OpaqueId ProviderAccountIdentityId,
    OpaqueId BillingScopeIdentityId,
    OpaqueId RequestIdentity,
    Sha256Fingerprint ExactRequestFingerprint,
    OpaqueId EffectiveScanConfigurationId,
    OpaqueId CapabilitySnapshotId,
    OpaqueId PriceSnapshotId,
    OpaqueId BudgetReservationId,
    ProviderResponseBoundsContract ResponseBounds,
    UtcTimestamp DispatchDeadline,
    OpaqueId StagingIdentity);

public sealed record ProviderUsageQuantitiesContract(
    long DispatchCount,
    long InputTokens,
    long OutputTokens,
    long TotalTokens,
    long ReasoningTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long PricedToolCalls);

public sealed record CalculatedCostContract(long NanoUsd);

public sealed record ProviderBillingFactContract(
    OperationalFactAvailability Availability,
    long? BilledNanoUsd);

public sealed record RateLimitFactContract(
    OperationalFactAvailability Availability,
    long? RemainingRequests,
    UtcTimestamp? ResetsAt);

public sealed record ProviderCreditFactContract(
    OperationalFactAvailability Availability,
    long? RemainingNanoUsd);

public sealed record BudgetLimitScopeContract(
    BudgetLimitScopeKind Kind,
    OpaqueId ScopeId);

public sealed record BudgetReservationContract(
    OpaqueId ReservationId,
    OperationOwnerContract Owner,
    OpaqueId JobNodeId,
    OpaqueId AttemptId,
    OpaqueId RequestIdentity,
    OpaqueId EffectiveScanConfigurationId,
    OpaqueId CapabilitySnapshotId,
    OpaqueId PriceSnapshotId,
    ProviderUsageQuantitiesContract WorstCaseUsage,
    CalculatedCostContract WorstCaseCalculatedCost,
    IReadOnlyList<BudgetLimitScopeContract> ApplicableLimitScopes,
    UtcTimestamp CreatedAt,
    UtcTimestamp ExpiresAt);

public sealed record DispatchFenceContract(
    OpaqueId DispatchFenceId,
    OpaqueId ReservationId,
    OperationOwnerContract Owner,
    OpaqueId JobNodeId,
    OpaqueId AttemptId,
    long CoordinatorFencingEpoch,
    long AttemptGeneration,
    long AttemptFencingToken,
    long CredentialGeneration,
    long RevocationEpoch,
    UtcTimestamp Deadline,
    bool Authorized,
    string DecisionReason,
    UtcTimestamp EvaluatedAt);

public sealed record UsageLedgerEntryContract(
    OpaqueId EntryId,
    OperationOwnerContract Owner,
    OpaqueId JobNodeId,
    OpaqueId AttemptId,
    OpaqueId RequestIdentity,
    OpaqueId EffectiveScanConfigurationId,
    ProviderUsageQuantitiesContract ProviderUsage,
    UsageReceiptState UsageReceiptState,
    CalculatedCostContract CalculatedCost,
    ProviderBillingFactContract ProviderBilling,
    RateLimitFactContract RateLimit,
    ProviderCreditFactContract ProviderCredit,
    SettlementState Settlement,
    OpaqueId CapabilitySnapshotId,
    OpaqueId PriceSnapshotId,
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

public sealed record StagedOutputSlotContract(
    OpaqueId StagedArtifactId,
    StagedArtifactKind Kind,
    string TypedRelativeName,
    long MaximumBytes,
    bool Required);

public sealed record WorkerAssignmentContract(
    OpaqueId AssignmentId,
    OperationOwnerContract Owner,
    OpaqueId JobNodeId,
    OpaqueId AttemptId,
    long CoordinatorFencingEpoch,
    long AttemptFencingToken,
    OpaqueId InputManifestId,
    OpaqueId StagingAreaId,
    IReadOnlyList<StagedOutputSlotContract> AllowedOutputs,
    UtcTimestamp Deadline);

public sealed record StagedOutputContract(
    OpaqueId StagedArtifactId,
    StagedArtifactKind Kind,
    string TypedRelativeName,
    Sha256Fingerprint ContentFingerprint,
    long ByteLength,
    ContractVersion SchemaVersion);

public sealed record StagedOutputManifestContract(
    OpaqueId AssignmentId,
    OpaqueId AttemptId,
    OpaqueId StagingAreaId,
    long CoordinatorFencingEpoch,
    long AttemptFencingToken,
    IReadOnlyList<StagedOutputContract> Outputs,
    Sha256Fingerprint ManifestFingerprint);
