namespace Infinium.Domain.Contracts;

public enum SnapshotAssuranceState
{
    Unspecified,
    Structural,
    SelectivelyContentSealed,
    FullyByteSealed,
    Inaccessible,
    Ambiguous,
    Unsupported,
    ChangedDuringCapture,
}

public enum EvidenceAuthority
{
    Unspecified,
    SnapshotBoundLocal,
    DeterministicDerived,
    AuthoritativeExternal,
    CorroboratedCommunity,
    UncorroboratedReport,
    UserStatement,
    TestResult,
    HeuristicOrLlmInference,
}

public enum LlmInvolvementState
{
    Unspecified,
    None,
    ProposalRetained,
    ProposalRejected,
    ProposalAdmitted,
}

public enum LlmOperation
{
    Unspecified,
    None,
    SourceClaimExtraction,
    CandidateInvestigation,
}

public enum ProposalValidationState
{
    Unspecified,
    Proposed,
    Rejected,
    Validated,
}

public enum TaxonomyApplicability
{
    Unspecified,
    Assigned,
    Unknown,
    Unsupported,
    Unmapped,
    NotApplicable,
}

public enum ClassificationRole
{
    Unspecified,
    Declared,
    Observed,
    Predicted,
    Established,
}

public enum CoverageState
{
    Unspecified,
    Completed,
    CompletedWithGaps,
    Failed,
    SkippedByConfiguration,
    SkippedByLimit,
    Unsupported,
}

public enum ReplayClass
{
    Unspecified,
    CompleteClean,
    BoundaryReplay,
    AuditOnly,
    Unavailable,
}

public enum AuditabilityState
{
    Unspecified,
    Complete,
    Partial,
    Unavailable,
}

public enum ReadinessScope
{
    Unspecified,
    None,
    ScopeLimited,
    Full,
}

public enum CaseOccurrenceKind
{
    Unspecified,
    Supported,
    LeadOnly,
}

public enum ReconciliationOutcome
{
    Unspecified,
    ExactContinuation,
    AnalyticalRevision,
    RelatedFollowUp,
    NewDistinct,
    Ambiguous,
    Unknown,
    NotObserved,
    NotEvaluated,
}

public sealed record SnapshotPopulationAssurance(
    string Population,
    SnapshotAssuranceState State,
    long DeclaredCount,
    long CapturedCount,
    IReadOnlyList<string> GapReasons);

public sealed record InstallationSnapshotContract(
    OpaqueId SnapshotId,
    ContractVersion SchemaVersion,
    OpaqueId Mo2InstanceId,
    OpaqueId ProfileId,
    Sha256Fingerprint StructuralManifestFingerprint,
    IReadOnlyList<SnapshotPopulationAssurance> Assurance,
    IReadOnlyList<OpaqueId> LocalInstalledEntityIds,
    UtcTimestamp CapturedAt);

public sealed record SemanticAnalysisContextContract(
    OpaqueId ContextId,
    ContractVersion SchemaVersion,
    Sha256Fingerprint CanonicalFingerprint,
    IReadOnlyList<OpaqueId> SemanticInputRevisionIds,
    IReadOnlyDictionary<string, string> Policies);

public enum SettingOrigin
{
    Unspecified,
    Default,
    SavedConfiguration,
    CommandLineOverride,
    SemanticContextOverride,
}

public enum SourceMode
{
    Unspecified,
    LocalFixture,
    RetainedEvidence,
    DisabledUnsupported,
}

public enum ProviderMode
{
    Unspecified,
    Disabled,
    OpenAIDirectSynchronous,
}

public sealed record ArtifactReferenceContract(
    OpaqueId ArtifactId,
    ContractVersion ArtifactVersion,
    Sha256Fingerprint Fingerprint,
    string Availability);

public sealed record EffectiveAnalyzerSettingContract(
    OpaqueId AnalyzerId,
    ContractVersion AnalyzerVersion,
    Sha256Fingerprint DeclarationFingerprint,
    bool Enabled,
    SettingOrigin Origin);

public sealed record EffectiveSourceSettingContract(
    OpaqueId SourceId,
    SourceMode Mode,
    bool Enabled,
    SettingOrigin Origin);

public sealed record EffectiveBudgetSettingContract(
    long MaximumDispatchCount,
    long MaximumInputTokens,
    long MaximumOutputTokens,
    long MaximumHostedSearchCalls,
    long MaximumNanoUsd,
    UtcTimestamp DispatchDeadline,
    SettingOrigin Origin);

public sealed record EffectiveCachePolicyContract(
    string AnalyticalMode,
    string SourceMode,
    string ProviderCacheMode,
    SettingOrigin Origin);

public sealed record EffectiveTracingSettingContract(
    bool Enabled,
    string Level,
    DiagnosticSensitivityLabel SensitivityLabel,
    SettingOrigin Origin);

public sealed record EffectiveCandidateBreadthContract(
    string Mode,
    long MaximumCandidates,
    bool AllPairsLlmComparison,
    SettingOrigin Origin);

public sealed record EffectiveThresholdSettingContract(
    OpaqueId AnalyzerId,
    OpaqueId ThresholdId,
    ContractVersion RulesetVersion,
    SettingOrigin Origin);

public sealed record EffectiveProviderSettingContract(
    ProviderMode Mode,
    string? Model,
    string? ReasoningEffort,
    bool? Store,
    string? ServiceTier,
    bool? Streaming,
    int? MaximumConcurrentLiveDispatches,
    SettingOrigin Origin);

public sealed record EffectiveResourceSettingContract(
    int MaximumGeneralWorkers,
    long MaximumMemoryBytes,
    long MaximumOutputBytes,
    SettingOrigin Origin);

public sealed record SemanticContextOverrideContract(
    OpaqueId OverrideId,
    OpaqueId SubjectId,
    ArtifactReferenceContract ValueArtifact,
    SettingOrigin Origin);

public sealed record EffectiveScanConfigurationContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId ConfigurationId,
    ContractVersion ConfigurationVersion,
    UtcTimestamp ResolvedAt,
    ArtifactReferenceContract? SavedConfigurationReference,
    IReadOnlyList<EffectiveAnalyzerSettingContract> Analyzers,
    IReadOnlyList<EffectiveSourceSettingContract> Sources,
    EffectiveBudgetSettingContract Budgets,
    EffectiveCachePolicyContract CachePolicy,
    EffectiveTracingSettingContract Tracing,
    EffectiveCandidateBreadthContract CandidateBreadth,
    IReadOnlyList<EffectiveThresholdSettingContract> Thresholds,
    EffectiveProviderSettingContract Provider,
    EffectiveResourceSettingContract Resources,
    IReadOnlyList<SemanticContextOverrideContract> SemanticContextOverrides);

public sealed record ResolvedInputContract(
    OpaqueId InputId,
    string Kind,
    string IdentityOrVersion,
    long? ByteLength,
    Sha256Fingerprint? Fingerprint,
    string Availability);

public sealed record ResolvedInputManifestContract(
    OpaqueId ManifestId,
    ContractVersion SchemaVersion,
    Sha256Fingerprint CanonicalFingerprint,
    IReadOnlyList<ResolvedInputContract> Inputs);

public sealed record AnalysisRunContract(
    OpaqueId RunId,
    ContractVersion SchemaVersion,
    OpaqueId SnapshotId,
    OpaqueId AnalysisContextId,
    OpaqueId EffectiveScanConfigurationId,
    OpaqueId ResolvedInputManifestId,
    UtcTimestamp CreatedAt);

public sealed record EvidenceAcquisitionRunContract(
    OpaqueId RunId,
    ContractVersion SchemaVersion,
    OpaqueId AcquisitionConfigurationId,
    OpaqueId ResolvedInputManifestId,
    OpaqueId? ParentAnalysisRunId,
    UtcTimestamp CreatedAt);

public sealed record ArtifactProvenanceContract(
    OpaqueId RevisionId,
    OpaqueId? SupersedesRevisionId,
    OpaqueId OriginatingRunId,
    OpaqueId ProducerId,
    ContractVersion ProducerVersion,
    IReadOnlyList<ArtifactReferenceContract> SourceReferences,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<OpaqueId> DependencyIds,
    Sha256Fingerprint ContentFingerprint,
    UtcTimestamp CreatedAt,
    LlmInvolvementContract LlmInvolvement);

public sealed record LlmInvolvementContract(
    LlmInvolvementState State,
    LlmOperation Operation,
    OpaqueId? InvocationId);

public sealed record ObservationContract(
    OpaqueId ObservationId,
    ContractVersion SchemaVersion,
    EvidenceAuthority Authority,
    ArtifactProvenanceContract Provenance,
    OpaqueId InstallationSnapshotId,
    string Subject,
    string MeasuredFact,
    string Value);

public sealed record DeterministicResultContract(
    OpaqueId ResultId,
    ContractVersion SchemaVersion,
    EvidenceAuthority Authority,
    ArtifactProvenanceContract Provenance,
    string Operation,
    IReadOnlyList<OpaqueId> InputEvidenceIds,
    string ResultType,
    string JsonValue);

public sealed record ExternalClaimContract(
    OpaqueId ClaimId,
    ContractVersion SchemaVersion,
    EvidenceAuthority Authority,
    ArtifactProvenanceContract Provenance,
    OpaqueId AcquisitionRunId,
    OpaqueId SourceRevisionId,
    string ExactPassageOrUnavailableMarker,
    IReadOnlyList<string> ApplicabilityConditions,
    IReadOnlyList<OpaqueId> ApplicationLinkIds);

public sealed record ExternalClaimApplicationLinkContract(
    OpaqueId ApplicationLinkId,
    ContractVersion SchemaVersion,
    OpaqueId ExternalClaimId,
    OpaqueId ConsumingAnalysisRunId,
    OpaqueId SemanticAnalysisContextId,
    string ApplicabilityDecision,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    ArtifactProvenanceContract Provenance);

public sealed record DiscoveryLeadContract(
    OpaqueId LeadId,
    ContractVersion SchemaVersion,
    ArtifactProvenanceContract Provenance,
    string DiscoverySource,
    string CandidateUriOrReference,
    string Reason,
    OpaqueId? AdmittedExternalClaimId);

public sealed record ModelProposalContract(
    OpaqueId ProposalId,
    ContractVersion SchemaVersion,
    ArtifactProvenanceContract Provenance,
    LlmOperation Operation,
    string ProposedArtifactType,
    OpaqueId RawResponseArtifactId,
    IReadOnlyList<OpaqueId> CitedInputIds,
    ProposalValidationState ValidationState,
    IReadOnlyList<string> ValidationFailures);

public sealed record ProposalAdmissionContract(
    OpaqueId AdmissionId,
    ContractVersion SchemaVersion,
    OpaqueId ProposalId,
    OpaqueId AdmittedArtifactId,
    string AdmittedArtifactType,
    OpaqueId HostValidatorId,
    OpaqueId OriginatingRunId,
    UtcTimestamp AdmittedAt);

public sealed record CandidateContract(
    OpaqueId CandidateId,
    ContractVersion SchemaVersion,
    ArtifactProvenanceContract Provenance,
    string AnalyzerId,
    string SelectionLane,
    string SelectionRationale,
    string ScopedPopulation,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    OpaqueId DependencyClosureId);

public sealed record HypothesisContract(
    OpaqueId HypothesisId,
    ContractVersion SchemaVersion,
    ArtifactProvenanceContract Provenance,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation,
    string ProposedExplanation,
    string PredictedImpact,
    string Confidence);

public sealed record FindingConclusionContract(
    OpaqueId FindingId,
    ContractVersion SchemaVersion,
    ArtifactProvenanceContract Provenance,
    string Conclusion,
    string Severity,
    string Confidence,
    IReadOnlyList<OpaqueId> EvidenceIds,
    IReadOnlyList<OpaqueId> TaxonomyAssignmentIds,
    IReadOnlyDictionary<string, string> EffectExtentFacets,
    IReadOnlyList<string> ExpectedSymptoms,
    OpaqueId? RecommendationId);

public sealed record RecommendationContract(
    OpaqueId RecommendationId,
    ContractVersion SchemaVersion,
    ArtifactProvenanceContract Provenance,
    string Kind,
    string ActionOrAbstention,
    IReadOnlyList<OpaqueId> EvidenceIds,
    string Uncertainty,
    string Reversibility,
    IReadOnlyList<string> Risks);

public sealed record CoverageGapContract(
    OpaqueId GapId,
    ContractVersion SchemaVersion,
    ArtifactProvenanceContract Provenance,
    string Population,
    string Reason,
    string MissingCapabilityOrInformation);

public sealed record AbstentionContract(
    OpaqueId AbstentionId,
    ContractVersion SchemaVersion,
    ArtifactProvenanceContract Provenance,
    string StageOrAnalyzer,
    string Reason,
    IReadOnlyList<string> RequiredInformation);

public sealed record InvalidInputContract(
    OpaqueId InvalidInputId,
    ContractVersion SchemaVersion,
    ArtifactProvenanceContract Provenance,
    string InputReference,
    string Reason);

public sealed record FailureContract(
    OpaqueId FailureId,
    ContractVersion SchemaVersion,
    ArtifactProvenanceContract Provenance,
    string StageOrAnalyzer,
    string FailureCode,
    string Message,
    bool Retryable);

public enum CollectionProductionState
{
    Unspecified,
    Populated,
    Empty,
    Unsupported,
    NotApplicable,
    Failed,
}

public sealed record TypedCollectionStateContract(
    string CollectionName,
    CollectionProductionState State,
    string Reason);

public sealed record TaxonomyAssignmentContract(
    OpaqueId AssignmentId,
    string TaxonomyId,
    ContractVersion TaxonomyVersion,
    string Axis,
    string Facet,
    string? Code,
    TaxonomyApplicability Applicability,
    OpaqueId SubjectId,
    string SubjectType,
    ClassificationRole Role,
    IReadOnlyList<OpaqueId> EvidenceIds,
    IReadOnlyList<OpaqueId> ApplicabilityConditionIds,
    OpaqueId? ConfidenceAssessmentId,
    OpaqueId AnalyzerOrAdjudicatorId,
    UtcTimestamp CreatedAt,
    string Reason,
    OpaqueId? SupersedesAssignmentId);

public sealed record DependencyClosureContract(
    OpaqueId ClosureId,
    ContractVersion SchemaVersion,
    IReadOnlyList<ResolvedInputContract> Dependencies,
    Sha256Fingerprint CanonicalFingerprint);

public sealed record ReuseOrApplicationEdgeContract(
    OpaqueId EdgeId,
    string EdgeKind,
    OpaqueId ProducerArtifactId,
    OpaqueId ConsumingRunId,
    OpaqueId DependencyClosureId,
    Sha256Fingerprint ValidityProofFingerprint,
    UtcTimestamp CreatedAt);

public sealed record IdentityEnvelopeContract(
    string AnalyzerFamily,
    ContractVersion SemanticContractVersion,
    ContractVersion IdentityContractVersion,
    IReadOnlyDictionary<string, string> ParticipantsAndRoles,
    string CausalCondition,
    string AffectedLocus,
    IReadOnlyList<string> ApplicabilityPredicates,
    OpaqueId DependencyClosureId,
    Sha256Fingerprint CanonicalSignature);

public sealed record FindingOccurrenceContract(
    OpaqueId OccurrenceId,
    OpaqueId LogicalFindingId,
    long RevisionNumber,
    OpaqueId? SupersedesOccurrenceId,
    OpaqueId OriginatingRunId,
    IdentityEnvelopeContract IdentityEnvelope,
    FindingConclusionContract Conclusion,
    IReadOnlyList<OpaqueId> TaxonomyAssignmentIds);

public sealed record CaseOccurrenceContract(
    OpaqueId OccurrenceId,
    OpaqueId LogicalCaseId,
    long RevisionNumber,
    OpaqueId? SupersedesOccurrenceId,
    OpaqueId OriginatingRunId,
    CaseOccurrenceKind Kind,
    IReadOnlyList<OpaqueId> FindingOccurrenceIds,
    IReadOnlyList<OpaqueId> HypothesisIds,
    string SharedCause);

public sealed record ReconciliationAssessmentContract(
    OpaqueId AssessmentId,
    OpaqueId PriorOccurrenceId,
    OpaqueId CurrentOccurrenceId,
    ReconciliationOutcome Outcome,
    bool CausalEquivalent,
    bool ApplicabilityEquivalent,
    bool DependencyEquivalent,
    bool ProducerCompatible,
    IReadOnlyList<string> Gaps,
    ContractVersion PolicyVersion,
    UtcTimestamp CreatedAt);

public sealed record LineageEventContract(
    OpaqueId EventId,
    string Kind,
    IReadOnlyList<OpaqueId> PredecessorIds,
    IReadOnlyList<OpaqueId> SuccessorIds,
    OpaqueId? ReconciliationAssessmentId,
    UtcTimestamp CreatedAt);

public sealed record CoverageContract(
    OpaqueId CoverageId,
    OpaqueId OriginatingRunId,
    OpaqueId AnalyzerId,
    string PopulationId,
    string DenominatorLabel,
    long Denominator,
    long CompletedCount,
    CoverageState State,
    string TaxonomyId,
    ContractVersion TaxonomyVersion,
    IReadOnlyList<OpaqueId> TaxonomyAssignmentIds,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<OpaqueId> GapIds,
    IReadOnlyList<OpaqueId> FailureIds);

public sealed record ReplayabilityAssessmentContract(
    ReplayClass ReplayClass,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<string> MissingDependencies);

public sealed record AuditabilityAssessmentContract(
    AuditabilityState State,
    IReadOnlyList<string> Gaps);

public sealed record ReadinessPlaceholderContract(
    OpaqueId EvaluationId,
    OpaqueId RunId,
    ReadinessScope Scope,
    OpaqueId? ReadinessPolicyId,
    IReadOnlyList<OpaqueId> DispositionIds,
    UtcTimestamp EvaluatedAt,
    string Reason);
