namespace Infinium.Domain.Contracts;

public sealed record FindingPromotionAssessmentContract(
    OpaqueId AssessmentId,
    OpaqueId HypothesisId,
    bool StatePresent,
    bool ConfidenceAtLeastPlausible,
    bool HasSupportingEvidence,
    bool HasNoDefeatingContradictions,
    bool HasNoMissingInformation,
    bool SeverityClosed,
    bool IdentityClosed,
    bool ConclusionAvailable,
    bool LeadEligibleState,
    FindingPromotionOutcome Outcome,
    IReadOnlyList<string> Reasons);

public sealed record FindingEvidenceFactContract(
    OpaqueId FactId,
    OpaqueId HypothesisId,
    WorstCredibleConsequence WorstCredibleConsequence,
    string AffectedLocus,
    string CausalCondition,
    IReadOnlyList<string> ApplicabilityPredicates,
    IReadOnlyList<OpaqueId> DefeatingContradictionIds,
    IReadOnlyList<OpaqueId> RetainedNonDefeatingContradictionIds,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record FindingRecommendationFactContract(
    OpaqueId FactId,
    OpaqueId HypothesisId,
    RecommendationKind Kind,
    string Action,
    string Uncertainty,
    string Reversibility,
    IReadOnlyList<string> Risks,
    string Verification,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record SharedCauseProofContract(
    OpaqueId ProofId,
    IReadOnlyList<OpaqueId> HypothesisIds,
    string AnalyzerFamily,
    ContractVersion SemanticContractVersion,
    ContractVersion IdentityContractVersion,
    IReadOnlyDictionary<string, string> ParticipantsAndRoles,
    string CausalCondition,
    string AffectedLocus,
    IReadOnlyList<string> ApplicabilityPredicates,
    OpaqueId DependencyClosureId,
    IReadOnlyList<OpaqueId> EvidenceIds)
{
    public ContractVersion AnalyzerVersion { get; init; } = new(1, 0, 0);
}

public sealed record TaxonomyClassificationFactContract(
    OpaqueId FactId,
    OpaqueId HypothesisId,
    string TaxonomyId,
    ContractVersion TaxonomyVersion,
    string Axis,
    string Facet,
    string? Code,
    TaxonomyApplicability Applicability,
    ClassificationRole? Role,
    IReadOnlyList<OpaqueId> EvidenceIds,
    IReadOnlyList<OpaqueId> ApplicabilityConditionIds,
    OpaqueId? ConfidenceAssessmentId,
    OpaqueId AnalyzerOrAdjudicatorId,
    UtcTimestamp CreatedAt,
    string Reason,
    OpaqueId? SourceAssignmentId = null,
    OpaqueId? SupersedesAssignmentId = null);

public sealed record CoverageFailureFactContract(
    OpaqueId FailureId,
    OpaqueId AnalyzerId,
    string FailureCode,
    string Message,
    bool Retryable);

public sealed record CoveragePopulationFactContract(
    OpaqueId FactId,
    OpaqueId AnalyzerId,
    string PopulationId,
    string DenominatorLabel)
{
    public IReadOnlyList<OpaqueId> EvidenceIds { get; init; } = [];
}

public sealed record CoverageMemberFactContract(
    OpaqueId FactId,
    OpaqueId AnalyzerId,
    string PopulationId,
    string DenominatorLabel,
    OpaqueId MemberId,
    CoverageMemberState State,
    string Reason,
    string MissingCapabilityOrInformation,
    OpaqueId? FailureId,
    IReadOnlyList<OpaqueId> TaxonomyClassificationFactIds,
    OpaqueId? GapId = null);

public sealed record ProducerCompatibilityContract(
    OpaqueId CompatibilityId,
    string PriorAnalyzerFamily,
    ContractVersion PriorSemanticContractVersion,
    ContractVersion PriorIdentityContractVersion,
    string CurrentAnalyzerFamily,
    ContractVersion CurrentSemanticContractVersion,
    ContractVersion CurrentIdentityContractVersion,
    bool Compatible,
    IReadOnlyList<OpaqueId> EvidenceIds)
{
    public ContractVersion PriorAnalyzerVersion { get; init; } = new(1, 0, 0);
    public ContractVersion CurrentAnalyzerVersion { get; init; } = new(1, 0, 0);
}

public sealed record RelatedFindingFactContract(
    OpaqueId FactId,
    OpaqueId CurrentHypothesisId,
    OpaqueId PriorOccurrenceId,
    IReadOnlyList<OpaqueId> EvidenceIds,
    string Reason);

public sealed record ReconciliationCandidateFactContract(
    OpaqueId FactId,
    OpaqueId CurrentHypothesisId,
    IReadOnlyList<OpaqueId> PriorOccurrenceIds);

public sealed record FindingConclusionAssessmentContract(
    OpaqueId AssessmentId,
    OpaqueId HypothesisId,
    FindingSeverity Severity,
    IdentityEnvelopeContract IdentityEnvelope,
    IdentityEnvelopeContract CaseIdentityEnvelope,
    RecommendationKind RecommendationKind,
    string Action,
    string Uncertainty,
    string Reversibility,
    IReadOnlyList<string> Risks,
    string Verification,
    IReadOnlyList<OpaqueId> TaxonomyAssignmentIds);

public sealed record PriorFindingContract(
    OpaqueId FindingOccurrenceId,
    OpaqueId LogicalFindingId,
    OpaqueId OriginatingRunId,
    OpaqueId CandidateId,
    OpaqueId HypothesisId,
    IdentityEnvelopeContract IdentityEnvelope,
    Sha256Fingerprint SemanticFingerprint,
    bool ProofAvailable,
    IReadOnlyList<string> ApplicablePopulationIds);

public sealed record PriorCaseContract(
    OpaqueId CaseOccurrenceId,
    OpaqueId LogicalCaseId,
    OpaqueId OriginatingRunId,
    CaseOccurrenceKind Kind,
    IReadOnlyList<OpaqueId> FindingOccurrenceIds,
    IReadOnlyList<OpaqueId> HypothesisIds,
    IdentityEnvelopeContract IdentityEnvelope,
    Sha256Fingerprint SemanticFingerprint,
    bool ProofAvailable,
    IReadOnlyList<string> ApplicablePopulationIds);

public sealed record TaxonomyProjectionInputContract(
    OpaqueId SourceClassificationFactId,
    string TargetTaxonomyId,
    ContractVersion TargetTaxonomyVersion,
    string TargetAxis,
    string TargetFacet,
    string? TargetCode,
    TaxonomyApplicability TargetApplicability,
    OpaqueId MappingAuthorityId,
    IReadOnlyList<OpaqueId> EvidenceIds,
    string Reason,
    ClassificationRole? TargetRole = null,
    OpaqueId? TargetAnalyzerOrAdjudicatorId = null,
    UtcTimestamp? ProjectionCreatedAt = null);

public sealed record FindingCaseGapContract(
    OpaqueId GapId,
    string PopulationId,
    string StageId,
    FindingGapState State,
    GapReplayEffect ReplayEffect,
    GapConclusionEffect ConclusionEffect,
    string Reason,
    string MissingCapabilityOrInformation,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record FindingCaseInputContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId InputId,
    OpaqueId OriginatingRunId,
    OpaqueId PromotionPolicyId,
    ContractVersion PromotionPolicyVersion,
    OpaqueId ReconciliationPolicyId,
    ContractVersion ReconciliationPolicyVersion,
    OpaqueId ReconciliationActorId,
    UtcTimestamp AssessmentTime,
    CandidateAnalysisContract CandidateAnalysis,
    IReadOnlyList<FindingEvidenceFactContract> FindingEvidenceFacts,
    IReadOnlyList<FindingRecommendationFactContract> FindingRecommendationFacts,
    IReadOnlyList<SharedCauseProofContract> SharedCauseProofs,
    IReadOnlyList<TaxonomyClassificationFactContract> TaxonomyClassificationFacts,
    IReadOnlyList<TaxonomyProjectionInputContract> TaxonomyProjectionInputs,
    IReadOnlyList<CoveragePopulationFactContract> CoveragePopulationFacts,
    IReadOnlyList<CoverageMemberFactContract> CoverageMemberFacts,
    IReadOnlyList<CoverageFailureFactContract> CoverageFailureFacts,
    IReadOnlyList<PriorFindingContract> PriorFindings,
    IReadOnlyList<PriorCaseContract> PriorCases,
    IReadOnlyList<ProducerCompatibilityContract> ProducerCompatibilities,
    IReadOnlyList<RelatedFindingFactContract> RelatedFindingFacts,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries)
{
    public IReadOnlyList<ReconciliationCandidateFactContract> ReconciliationCandidateFacts { get; init; } = [];
}

public sealed record FindingCaseAbstentionContract(
    OpaqueId AbstentionId,
    OpaqueId HypothesisId,
    string Reason,
    IReadOnlyList<string> RequiredInformation,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record FindingContract(
    OpaqueId FindingOccurrenceId,
    OpaqueId LogicalFindingId,
    OpaqueId OriginatingRunId,
    OpaqueId CandidateId,
    OpaqueId HypothesisId,
    string Conclusion,
    FindingSeverity Severity,
    AnalysisConfidence Confidence,
    IReadOnlyList<OpaqueId> EvidenceIds,
    OpaqueId IdentityEnvelopeId,
    IdentityEnvelopeContract IdentityEnvelope,
    OpaqueId CaseIdentityEnvelopeId,
    IdentityEnvelopeContract CaseIdentityEnvelope,
    IReadOnlyList<OpaqueId> TaxonomyAssignmentIds,
    Sha256Fingerprint SemanticFingerprint,
    OpaqueId? SupersedesOccurrenceId);

public sealed record FindingRecommendationContract(
    OpaqueId RecommendationId,
    RecommendationKind Kind,
    OpaqueId? FindingOccurrenceId,
    OpaqueId? AbstentionId,
    OpaqueId? LeadHypothesisId,
    string Action,
    string Uncertainty,
    string Reversibility,
    IReadOnlyList<string> Risks,
    string Verification,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record AnalysisCaseContract(
    OpaqueId CaseOccurrenceId,
    OpaqueId LogicalCaseId,
    OpaqueId OriginatingRunId,
    CaseOccurrenceKind Kind,
    IReadOnlyList<OpaqueId> FindingOccurrenceIds,
    IReadOnlyList<OpaqueId> CandidateIds,
    IReadOnlyList<OpaqueId> HypothesisIds,
    string SharedCause,
    IReadOnlyList<OpaqueId> CauseProofEvidenceIds,
    OpaqueId IdentityEnvelopeId,
    IdentityEnvelopeContract IdentityEnvelope,
    Sha256Fingerprint SemanticFingerprint,
    OpaqueId? SupersedesOccurrenceId,
    bool AffectsReadiness);

public sealed record ReconciliationGatesContract(
    ReconciliationGateState Causal,
    ReconciliationGateState Applicability,
    ReconciliationGateState Dependency,
    ReconciliationGateState Producer);

public sealed record OccurrenceReconciliationContract(
    OpaqueId AssessmentId,
    string SubjectKind,
    OpaqueId? PriorOccurrenceId,
    OpaqueId? CurrentOccurrenceId,
    ReconciliationGatesContract Gates,
    ReconciliationOutcome Outcome,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<OpaqueId> ConsideredOccurrenceIds,
    IReadOnlyList<OpaqueId> ProofEvidenceIds,
    ContractVersion PolicyVersion,
    string Mechanism,
    OpaqueId ActorId,
    UtcTimestamp AssessedAt,
    bool VisibleByDefault);

public sealed record OccurrenceLineageContract(
    OpaqueId EventId,
    LineageKind Kind,
    IReadOnlyList<OpaqueId> PredecessorIds,
    IReadOnlyList<OpaqueId> SuccessorIds,
    OpaqueId? ReconciliationAssessmentId);

public sealed record TaxonomyProjectionContract(
    OpaqueId ProjectionId,
    OpaqueId SourceAssignmentId,
    OpaqueId ProjectedAssignmentId,
    OpaqueId MappingAuthorityId,
    IReadOnlyList<OpaqueId> EvidenceIds,
    string Reason);

public sealed record FindingCaseContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PayloadId,
    OpaqueId OriginatingRunId,
    OpaqueId InputId,
    OpaqueId PromotionPolicyId,
    ContractVersion PromotionPolicyVersion,
    OpaqueId ReconciliationPolicyId,
    ContractVersion ReconciliationPolicyVersion,
    IReadOnlyList<FindingPromotionAssessmentContract> PromotionAssessments,
    IReadOnlyList<FindingCaseAbstentionContract> Abstentions,
    IReadOnlyList<FindingContract> Findings,
    IReadOnlyList<FindingRecommendationContract> Recommendations,
    IReadOnlyList<AnalysisCaseContract> Cases,
    IReadOnlyList<OccurrenceReconciliationContract> ReconciliationAssessments,
    IReadOnlyList<OccurrenceLineageContract> LineageEvents,
    IReadOnlyList<TaxonomyAssignmentContract> TaxonomyAssignments,
    IReadOnlyList<TaxonomyProjectionContract> TaxonomyProjections,
    IReadOnlyList<CoverageContract> Coverage,
    IReadOnlyList<CoverageFailureFactContract> CoverageFailures,
    IReadOnlyList<FindingCaseGapContract> Gaps,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries,
    string PublicationClaimBoundary);
