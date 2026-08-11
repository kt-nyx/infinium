namespace Infinium.Domain.Contracts;

public enum ProviderAvailabilityState
{
    Unspecified,
    Available,
    Unavailable,
    Unsupported,
    NotApplicable,
    NotUsed,
}

public enum ProviderProfileState
{
    Unspecified,
    PendingEnrollment,
    ActiveUnverified,
    ActiveVerified,
    Replacing,
    Disabled,
    DeletePending,
    Deleted,
    SecureStoreUnavailable,
    RecoveryRequired,
}

public enum ProviderOperationState
{
    Unspecified,
    Proposed,
    Confirmed,
    Reserved,
    Assigned,
    InputBoundBlocked,
    FinalGateAuthorized,
    TransportNotStarted,
    TransportMayHaveStarted,
    ResponseStaged,
    Admitted,
    Rejected,
    Settled,
    UnresolvedHold,
}

public enum ProviderResponseState
{
    Unspecified,
    Completed,
    Refusal,
    Incomplete,
    Failed,
    Queued,
    InProgress,
    Cancelled,
    Malformed,
    Oversized,
    Mismatched,
    Unknown,
}

public enum ProviderOperationKind
{
    Unspecified,
    TransportQualification,
    SourceClaimExtraction,
    CandidateInvestigation,
}

public enum ProposalAdmissionState
{
    Unspecified,
    Proposed,
    Admitted,
    Rejected,
    Abstained,
    Unavailable,
    Unsupported,
    Deleted,
}

public enum ProviderInputBoundProofState
{
    Unspecified,
    AuthorityRequired,
}

public sealed record ProviderInputBoundProofContract(
    string PolicyId,
    string PolicyVersion,
    ProviderInputBoundProofState Status);

public sealed record ProviderIdentityReferenceContract(
    OpaqueId Identity,
    Sha256Fingerprint Fingerprint);

public sealed record ProviderFiniteLimitsContract(
    long MaximumRequestBytes,
    long MaximumInputTokens,
    long MaximumOutputTokens,
    long MaximumRawResponseBytes,
    long MaximumDispatchCount,
    long MaximumCalculatedNanoUsd,
    long DeadlineMilliseconds);

public sealed record ProviderPriceRuleContract(
    OpaqueId RuleId,
    string Provider,
    string Model,
    string ServiceTier,
    string ContextBand,
    string CacheClass,
    string TokenClass,
    string ToolClass,
    string Region,
    string Currency,
    long NumeratorNanoUsd,
    long DenominatorTokens,
    string Revision);

public sealed record ProviderCapabilitySnapshotContract(
    OpaqueId Identity,
    Sha256Fingerprint Fingerprint,
    string Provider,
    string Model,
    string ServiceTier,
    string ReasoningEffort,
    string ReasoningContext,
    string ReasoningMode,
    bool Store,
    bool Background,
    bool Stream,
    string ToolChoice,
    int ToolCount,
    string Truncation,
    string PromptCacheMode,
    bool HasPromptCacheKey,
    bool HasPromptCacheBreakpoint,
    long MaximumContextTokens,
    string Revision);

public sealed record ProviderPriceSnapshotContract(
    OpaqueId Identity,
    Sha256Fingerprint Fingerprint,
    string Provider,
    string Model,
    string ServiceTier,
    string Currency,
    string Revision,
    IReadOnlyList<ProviderPriceRuleContract> Rules);

public sealed record ProviderQuantityContract(
    ProviderAvailabilityState Availability,
    long? Value);

public sealed record ProviderUsageContract(
    ProviderQuantityContract DispatchCount,
    ProviderQuantityContract InputTokens,
    ProviderQuantityContract OutputTokens,
    ProviderQuantityContract TotalTokens,
    ProviderQuantityContract ReasoningTokens,
    ProviderQuantityContract CacheReadTokens,
    ProviderQuantityContract CacheWriteTokens,
    ProviderQuantityContract PricedToolCalls,
    ProviderQuantityContract CalculatedNanoUsd,
    ProviderAvailabilityState BillingAvailability,
    ProviderAvailabilityState RateAvailability,
    ProviderAvailabilityState CreditAvailability);

public sealed record ProviderRateLimitFactContract(
    string Scope,
    string Dimension,
    ProviderAvailabilityState Availability,
    long? Limit,
    long? Remaining,
    UtcTimestamp ObservedAt,
    UtcTimestamp? ResetsAt);

public sealed record ProviderAccessProfileDocument(
    string SchemaId,
    string SchemaVersion,
    OpaqueId ProfileId,
    OpaqueId GenerationId,
    long GenerationOrdinal,
    long RevocationEpoch,
    string Provider,
    string Purpose,
    string DisplayLabel,
    ProviderProfileState LifecycleState,
    ProviderAvailabilityState VerificationState,
    OpaqueId? AccountIdentityId,
    OpaqueId? BillingScopeIdentityId,
    OpaqueId? CapabilitySnapshotId,
    OpaqueId? IntentId,
    string RecoveryDisposition,
    string CleanupDisposition,
    UtcTimestamp RecordedAt);

public sealed record ProviderOperationDocument(
    string SchemaId,
    string SchemaVersion,
    OpaqueId OperationId,
    OpaqueId OwnerId,
    string OwnerKind,
    ProviderOperationKind OperationKind,
    OpaqueId JobNodeId,
    OpaqueId? AttemptId,
    OpaqueId? RequestId,
    OpaqueId ProfileId,
    OpaqueId GenerationId,
    long RevocationEpoch,
    ProviderCapabilitySnapshotContract CapabilitySnapshot,
    ProviderPriceSnapshotContract PriceSnapshot,
    Sha256Fingerprint? SettingsFingerprint,
    Sha256Fingerprint? OutputSchemaFingerprint,
    Sha256Fingerprint? RequestFingerprint,
    ProviderInputBoundProofContract InputBoundProof,
    ProviderFiniteLimitsContract Limits,
    OpaqueId? AuthorizationId,
    OpaqueId? ReservationId,
    OpaqueId? DispatchFenceId,
    ProviderOperationState State,
    string TransportState,
    string ReceiptState,
    ProviderUsageContract Usage,
    string SettlementState,
    string ReplayState,
    UtcTimestamp RecordedAt);

public sealed record ProviderResponseDocument(
    string SchemaId,
    string SchemaVersion,
    OpaqueId ResponseRecordId,
    OpaqueId OperationId,
    OpaqueId RequestId,
    OpaqueId DispatchFenceId,
    ProviderIdentityReferenceContract? RawResponsePayload,
    long? RawResponseBytes,
    long MaximumRawResponseBytes,
    ProviderIdentityReferenceContract? ResponseHeadersPayload,
    long? ResponseHeadersBytes,
    ProviderAvailabilityState ResponseHeadersAvailability,
    int? HttpStatus,
    string? ProviderResponseId,
    string? ProviderRequestId,
    ProviderAvailabilityState ProviderRequestIdAvailability,
    ProviderResponseState State,
    string? RefusalCode,
    string? IncompleteReason,
    string? ErrorCode,
    string RequestedModel,
    string? ReturnedModel,
    string RequestedServiceTier,
    string? ReturnedServiceTier,
    string ReasoningContext,
    string ReasoningMode,
    string PromptCacheMode,
    ProviderUsageContract Usage,
    IReadOnlyList<ProviderRateLimitFactContract> RateLimitFacts,
    ProposalAdmissionState ValidationState,
    ProposalAdmissionState AdmissionState,
    UtcTimestamp RecordedAt);

public sealed record CitationProposalContract(
    OpaqueId ProposalId,
    OpaqueId PassageId,
    string Claim,
    IReadOnlyList<OpaqueId> ConditionIds,
    ProposalAdmissionState State,
    string Reason);

public sealed record SourceClaimExtractionDocument(
    string SchemaId,
    string SchemaVersion,
    OpaqueId AcquisitionRunId,
    OpaqueId OperationId,
    OpaqueId SourceRevisionId,
    IReadOnlyList<OpaqueId> PassageIds,
    string DeclaredPurpose,
    IReadOnlyList<CitationProposalContract> ClaimProposals,
    IReadOnlyList<OpaqueId> ContradictionEvidenceIds,
    IReadOnlyList<string> Abstentions,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<OpaqueId> ValidationIds,
    IReadOnlyList<OpaqueId> ApplicationLinkIds);

public sealed record HypothesisProposalContract(
    OpaqueId ProposalId,
    OpaqueId CandidateId,
    string Hypothesis,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation,
    ProposalAdmissionState State,
    string Reason);

public sealed record CandidateInvestigationDocument(
    string SchemaId,
    string SchemaVersion,
    OpaqueId OperationId,
    OpaqueId CandidateId,
    IReadOnlyList<OpaqueId> ParticipantIds,
    IReadOnlyList<string> ParticipantRoles,
    IReadOnlyList<OpaqueId> CausalPathIds,
    OpaqueId DependencyClosureId,
    IReadOnlyList<OpaqueId> EvidenceIds,
    IReadOnlyList<HypothesisProposalContract> HypothesisProposals,
    IReadOnlyList<string> Abstentions,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<OpaqueId> ValidationIds,
    IReadOnlyList<OpaqueId> AdmissionLinkIds);

public sealed record ProviderExecutionInputDocument(
    string SchemaId,
    string SchemaVersion,
    OpaqueId OperationId,
    OpaqueId OwnerId,
    OpaqueId InstallationSnapshotId,
    OpaqueId AnalysisContextId,
    OpaqueId EffectiveConfigurationId,
    OpaqueId ResolvedInputManifestId,
    OpaqueId ProfileId,
    OpaqueId GenerationId,
    ProviderCapabilitySnapshotContract CapabilitySnapshot,
    ProviderPriceSnapshotContract PriceSnapshot,
    ProviderFiniteLimitsContract Limits,
    OpaqueId PromptId,
    Sha256Fingerprint PromptFingerprint,
    OpaqueId OutputSchemaId,
    Sha256Fingerprint OutputSchemaFingerprint,
    ProviderOperationKind OperationKind,
    Sha256Fingerprint CanonicalRequestFingerprint,
    ProviderInputBoundProofContract InputBoundProof,
    string DispatchAdmission);

public sealed record EffectiveScanConfigurationV2Document(
    string SchemaId,
    string SchemaVersion,
    OpaqueId ConfigurationId,
    OpaqueId LocalConfigurationV1Id,
    Sha256Fingerprint LocalConfigurationV1Fingerprint,
    OpaqueId AccessProfileId,
    OpaqueId GenerationId,
    string Model,
    string ReasoningEffort,
    string ReasoningContext,
    string ReasoningMode,
    bool Store,
    string ServiceTier,
    bool Background,
    bool Stream,
    string ToolChoice,
    int ToolCount,
    string Truncation,
    string PromptCacheMode,
    bool HasPromptCacheKey,
    bool HasPromptCacheBreakpoint,
    ProviderFiniteLimitsContract Limits,
    IReadOnlyList<string> NotUsedBoundaries);

public sealed record ProviderPublicationReferenceContract(
    OpaqueId? OperationId,
    ProviderOperationKind? OperationKind,
    OpaqueId? AcquisitionRunId,
    OpaqueId? AuthorizationId,
    OpaqueId? ResponseId,
    OpaqueId? AdmissionId,
    OpaqueId? UsageEntryId,
    OpaqueId? SettlementId,
    OpaqueId? ReplayEdgeId,
    string Availability,
    bool Live);

public sealed record RunOutputV2Document(
    string SchemaId,
    string SchemaVersion,
    OpaqueId RunId,
    ProviderIdentityReferenceContract LocalRunOutputV1,
    OpaqueId EffectiveConfigurationV2Id,
    IReadOnlyList<ProviderPublicationReferenceContract> ProviderOperations,
    IReadOnlyList<OpaqueId> EvidenceAcquisitionRunIds,
    IReadOnlyList<OpaqueId> CapabilityDriftIds,
    IReadOnlyList<OpaqueId> PriceDriftIds,
    IReadOnlyList<string> ProviderGaps,
    bool ContainsRawTransport,
    bool ContainsSecret);

public sealed record CliSummaryV2Document(
    string SchemaId,
    string SchemaVersion,
    OpaqueId RunId,
    Sha256Fingerprint LocalCliSummaryV1Fingerprint,
    string ProviderState,
    ProviderQuantityContract DispatchCount,
    ProviderQuantityContract InputTokens,
    ProviderQuantityContract OutputTokens,
    ProviderQuantityContract ReasoningTokens,
    ProviderQuantityContract CacheReadTokens,
    ProviderQuantityContract CacheWriteTokens,
    ProviderQuantityContract CalculatedNanoUsd,
    ProviderQuantityContract ReservedNanoUsd,
    bool UnresolvedHold,
    string ReplayState,
    IReadOnlyList<string> Gaps,
    bool ContainsRawTransport,
    bool ContainsSecret);
