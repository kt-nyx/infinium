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

public sealed record ProviderUsageContract(
    long DispatchCount,
    long InputTokens,
    long OutputTokens,
    long ReasoningTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long PricedToolCalls,
    long CalculatedNanoUsd,
    ProviderAvailabilityState BillingAvailability,
    ProviderAvailabilityState RateAvailability,
    ProviderAvailabilityState CreditAvailability);

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
    OpaqueId AccountIdentityId,
    OpaqueId BillingScopeIdentityId,
    OpaqueId CapabilitySnapshotId,
    OpaqueId IntentId,
    string RecoveryDisposition,
    string CleanupDisposition,
    UtcTimestamp RecordedAt);

public sealed record ProviderOperationDocument(
    string SchemaId,
    string SchemaVersion,
    OpaqueId OperationId,
    OpaqueId OwnerId,
    string OwnerKind,
    OpaqueId JobNodeId,
    OpaqueId AttemptId,
    OpaqueId RequestId,
    OpaqueId ProfileId,
    OpaqueId GenerationId,
    long RevocationEpoch,
    ProviderIdentityReferenceContract CapabilitySnapshot,
    ProviderIdentityReferenceContract PriceSnapshot,
    Sha256Fingerprint SettingsFingerprint,
    Sha256Fingerprint OutputSchemaFingerprint,
    Sha256Fingerprint RequestFingerprint,
    ProviderFiniteLimitsContract Limits,
    OpaqueId AuthorizationId,
    OpaqueId ReservationId,
    OpaqueId DispatchFenceId,
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
    ProviderIdentityReferenceContract RawResponsePayload,
    long RawResponseBytes,
    int HttpStatus,
    string? ProviderResponseId,
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
    ProviderIdentityReferenceContract CapabilitySnapshot,
    ProviderIdentityReferenceContract PriceSnapshot,
    ProviderFiniteLimitsContract Limits,
    OpaqueId PromptId,
    Sha256Fingerprint PromptFingerprint,
    OpaqueId OutputSchemaId,
    Sha256Fingerprint OutputSchemaFingerprint,
    string OperationKind,
    Sha256Fingerprint CanonicalRequestFingerprint);

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
    OpaqueId OperationId,
    OpaqueId AcquisitionRunId,
    OpaqueId AuthorizationId,
    OpaqueId ResponseId,
    OpaqueId AdmissionId,
    OpaqueId UsageEntryId,
    OpaqueId SettlementId,
    OpaqueId ReplayEdgeId,
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
    long DispatchCount,
    long InputTokens,
    long OutputTokens,
    long ReasoningTokens,
    long CacheReadTokens,
    long CacheWriteTokens,
    long CalculatedNanoUsd,
    long ReservedNanoUsd,
    bool UnresolvedHold,
    string ReplayState,
    IReadOnlyList<string> Gaps,
    bool ContainsRawTransport,
    bool ContainsSecret);
