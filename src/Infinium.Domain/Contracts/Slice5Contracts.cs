namespace Infinium.Domain.Contracts;

public enum Slice5ResultState
{
    Unspecified,
    Present,
    ResolvedNegative,
    Missing,
    InvalidInput,
    Unsupported,
    Ambiguous,
    Partial,
    Abstained,
    NotApplicable,
    NotUsed,
    Failed,
    Cancelled,
    LimitReached,
    Unavailable,
    Unknown,
}

public enum EvidenceLayer
{
    Unspecified,
    Structural,
    Observed,
    Decoded,
    Resolved,
    Semantic,
}

public enum ClaimKind
{
    Unspecified,
    DeclaredPurpose,
    Requirement,
    Incompatibility,
    InstallationInstruction,
    PriorityInstruction,
    LifecycleInstruction,
    ConfigurationInstruction,
    PatchInstruction,
    KnownIssue,
}

public enum ClaimApplicabilityState
{
    Unspecified,
    Applicable,
    NotApplicable,
    Unknown,
    Unsupported,
    Contradicted,
}

public enum DocumentationSourceKind
{
    Unspecified,
    ProjectAuthoredLocal,
    Fixture,
}

public enum DocumentationSourceAvailability
{
    Unspecified,
    Present,
    Deleted,
    Unavailable,
}

public enum DocumentationImportMode
{
    Unspecified,
    CleanImport,
    RetainedReuse,
}

public enum DocumentationGapKind
{
    Unspecified,
    Contradiction,
    Deletion,
    UnavailableSource,
    Replay,
}

public enum CandidateLane
{
    Unspecified,
    DeterministicRequired,
    MandatoryEvidence,
    OptionalRanked,
}

public enum CandidateDecisionDisposition
{
    Unspecified,
    CandidateAdmitted,
    ResolvedNegative,
    Unsupported,
    Ambiguous,
    InvalidInput,
    Limited,
    Deferred,
    Unprocessed,
    Abstained,
    Failed,
}

public enum AnalysisConfidence
{
    Unspecified,
    SpeculativeLead,
    Plausible,
    StronglySupported,
    Confirmed,
}

public enum FindingSeverity
{
    Unspecified,
    Advisory,
    Minor,
    Moderate,
    Major,
    Blocker,
}

public enum RecommendationKind
{
    Unspecified,
    Remediation,
    AlternativeRemediation,
    Validation,
    FurtherInvestigation,
    Abstention,
}

public enum ReconciliationGateState
{
    Unspecified,
    ProvenEquivalent,
    ProvenDifferent,
    Ambiguous,
    Unknown,
    NotEvaluated,
}

public enum LineageKind
{
    Unspecified,
    Supersedes,
    AnalyticalRevision,
    RelatedFollowUp,
    PromotesLead,
    MergeSuccessor,
    SplitSuccessor,
    CorrectionSuccessor,
}

public enum ReplayMode
{
    Unspecified,
    Clean,
    Incremental,
    RetainedDownstreamReplay,
}

public enum ReplayState
{
    Unspecified,
    CompleteClean,
    Partial,
    AuditOnly,
    Unavailable,
    FailedIdentityDrift,
}

public enum BoundaryUseState
{
    Unspecified,
    Used,
    NotUsed,
    Unsupported,
}

public sealed record Slice5ArtifactReferenceContract(
    OpaqueId ArtifactId,
    string SchemaId,
    ContractVersion SchemaVersion,
    long Revision,
    Slice5ResultState State,
    Sha256Fingerprint Fingerprint,
    long ByteLength,
    OpaqueId ProvenanceId,
    OpaqueId DependencyClosureId);

public sealed record DocumentationRevisionContract(
    OpaqueId RevisionId,
    OpaqueId SourceId,
    DocumentationSourceKind SourceKind,
    string SourceRevision,
    Sha256Fingerprint ByteFingerprint,
    long ByteLength,
    OpaqueId? SupplyingSnapshotId,
    Slice5ResultState RetentionState,
    ReplayState ReplayState);

public sealed record DocumentationImportContract(
    OpaqueId ImportId,
    OpaqueId ImportRunId,
    OpaqueId RevisionId,
    DocumentationImportMode Mode,
    OpaqueId? ReusedImportId,
    OpaqueId DependencyClosureId,
    OpaqueId ExtractorId,
    LlmInvolvementState LlmInvolvement,
    LlmOperation LlmOperation,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries,
    UtcTimestamp CreatedAt);

public sealed record DocumentationPassageContract(
    OpaqueId PassageId,
    OpaqueId RevisionId,
    long Utf8StartOffset,
    long Utf8EndOffset,
    Sha256Fingerprint PassageFingerprint,
    Slice5ResultState State);

public sealed record DocumentationClaimContract(
    OpaqueId ClaimId,
    OpaqueId ProducingImportId,
    OpaqueId PassageId,
    ClaimKind Kind,
    string ExactText,
    IReadOnlyList<string> Conditions,
    EvidenceAuthority Authority,
    ClaimApplicabilityState Applicability,
    ClassificationRole ClassificationRole,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds);

public sealed record ClaimApplicationContract(
    OpaqueId ApplicationId,
    OpaqueId ClaimId,
    OpaqueId ConsumingRunId,
    OpaqueId AnalysisContextId,
    OpaqueId SubjectId,
    string SubjectType,
    OpaqueId DependencyClosureId,
    ClaimApplicabilityState Applicability,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record DocumentationGapContract(
    OpaqueId GapId,
    OpaqueId OriginatingRunId,
    DocumentationGapKind Kind,
    OpaqueId RevisionId,
    OpaqueId? ClaimId,
    OpaqueId? ApplicationId,
    ReplayState ReplayEffect,
    string Reason,
    UtcTimestamp CreatedAt);

public sealed record DocumentationDeletionReceiptContract(
    OpaqueId ReceiptId,
    OpaqueId OriginatingRunId,
    OpaqueId RevisionId,
    Sha256Fingerprint DeletedBodyFingerprint,
    IReadOnlyList<OpaqueId> DeletedPassageIds,
    IReadOnlyList<OpaqueId> IndependentlyRetainedPayloadIds,
    ReplayState ReplayEffect,
    UtcTimestamp DeletedAt,
    string Reason);

public sealed record DocumentationPurposeAssignmentContract(
    OpaqueId AssignmentId,
    string TaxonomyId,
    ContractVersion TaxonomyVersion,
    string Axis,
    string Facet,
    string Code,
    TaxonomyApplicability Applicability,
    OpaqueId SubjectId,
    string SubjectType,
    ClassificationRole Role,
    OpaqueId ClaimId,
    OpaqueId ApplicationId,
    IReadOnlyList<OpaqueId> ApplicabilityConditionIds,
    OpaqueId AnalyzerOrAdjudicatorId,
    UtcTimestamp CreatedAt,
    string Reason);

public sealed record DocumentationFailureContract(
    OpaqueId FailureId,
    string FailureCode,
    string Message,
    bool Retryable);

public sealed record DocumentationEvidenceContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PayloadId,
    OpaqueId OriginatingRunId,
    IReadOnlyList<DocumentationRevisionContract> Revisions,
    IReadOnlyList<DocumentationImportContract> Imports,
    IReadOnlyList<DocumentationPassageContract> Passages,
    IReadOnlyList<DocumentationClaimContract> Claims,
    IReadOnlyList<ClaimApplicationContract> Applications,
    IReadOnlyList<DocumentationPurposeAssignmentContract> PurposeAssignments,
    IReadOnlyList<DocumentationDeletionReceiptContract> DeletionReceipts,
    IReadOnlyList<DocumentationGapContract> Gaps,
    IReadOnlyList<DocumentationFailureContract> Failures);

public sealed record CandidateParticipantContract(OpaqueId ParticipantId, string Role);

public sealed record CandidateDecisionContract(
    OpaqueId DecisionId,
    OpaqueId PopulationMemberId,
    OpaqueId SourceFactId,
    CandidateLane Lane,
    CandidateDecisionDisposition Disposition,
    IReadOnlyList<CandidateParticipantContract> Participants,
    string JoinKind,
    IReadOnlyList<OpaqueId> Path,
    OpaqueId DependencyClosureId,
    string Rationale,
    IReadOnlyList<OpaqueId> EvidenceIds,
    bool AdmissionIndependentOfScore,
    long? OptionalRank)
{
    public OpaqueId AnalyzerId { get; init; } = new("analyzer-unspecified");

    public OpaqueId PolicyId { get; init; } = new("policy-unspecified");

    public OpaqueId ThresholdId { get; init; } = new("threshold-unspecified");

    public OpaqueId LimitId { get; init; } = new("limit-unspecified");

    public IReadOnlyList<OpaqueId> DependencyIds { get; init; } = [];
}

public sealed record CandidateAnalysisEntryContract(
    OpaqueId CandidateId,
    OpaqueId DecisionId,
    Slice5ResultState State,
    string CausalExplanation,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation,
    AnalysisConfidence Confidence,
    OpaqueId ThresholdId)
{
    public OpaqueId? HypothesisId { get; init; }

    public OpaqueId? AbstentionId { get; init; }
}

public sealed record CandidateHypothesisContract(
    OpaqueId HypothesisId,
    OpaqueId CandidateId,
    Slice5ResultState State,
    string ProposedExplanation,
    string PredictedImpact,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation,
    AnalysisConfidence Confidence,
    OpaqueId ThresholdId);

public sealed record CandidateAbstentionContract(
    OpaqueId AbstentionId,
    OpaqueId DecisionId,
    OpaqueId? CandidateId,
    OpaqueId AnalyzerId,
    string Reason,
    IReadOnlyList<string> RequiredInformation);

public sealed record CandidateGapContract(
    OpaqueId GapId,
    OpaqueId DecisionId,
    OpaqueId PopulationId,
    Slice5ResultState State,
    string Reason,
    string MissingCapabilityOrInformation);

public sealed record CandidateFailureContract(
    OpaqueId FailureId,
    OpaqueId AnalyzerId,
    IReadOnlyList<OpaqueId> PopulationMemberIds,
    string FailureCode,
    string Message,
    bool Retryable);

public sealed record CandidateDependencyEdgeContract(
    OpaqueId EdgeId,
    string FromKind,
    OpaqueId FromId,
    string ToKind,
    OpaqueId ToId,
    string EdgeKind);

public sealed record CandidateAnalyzerBindingContract(
    OpaqueId AnalyzerId,
    ContractVersion AnalyzerVersion,
    ContractVersion RulesetVersion,
    Sha256Fingerprint DeclarationFingerprint,
    string CanonicalDeclarationJson);

public sealed record CandidatePopulationCountsContract(
    long Population,
    long DeterministicRequired,
    long MandatoryEvidence,
    long OptionalRanked,
    long CandidateAdmitted,
    long Hypotheses,
    long Abstentions,
    long Gaps,
    long Failures,
    long ResolvedNegative,
    long Unsupported,
    long Ambiguous,
    long InvalidInput,
    long Limited,
    long Deferred,
    long Unprocessed);

public sealed record CandidateAnalysisContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PayloadId,
    OpaqueId OriginatingRunId,
    OpaqueId AnalyzerId,
    OpaqueId PopulationId,
    long PopulationDenominator,
    IReadOnlyList<CandidateDecisionContract> Decisions,
    IReadOnlyList<CandidateAnalysisEntryContract> Candidates,
    IReadOnlyList<CandidateAbstentionContract> Abstentions,
    IReadOnlyList<CandidateGapContract> Gaps,
    IReadOnlyList<CandidateFailureContract> Failures)
{
    public OpaqueId PolicyId { get; init; } = new("policy-unspecified");

    public OpaqueId ThresholdId { get; init; } = new("threshold-unspecified");

    public OpaqueId LimitId { get; init; } = new("limit-unspecified");

    public OpaqueId ExecutionInputId { get; init; } = new("execution-input-unspecified");

    public OpaqueId AnalysisRootId { get; init; } = new("candidate-analysis-root-unspecified");

    public Sha256Fingerprint ExecutionInputFingerprint { get; init; } = new(new string('0', 64));

    public Sha256Fingerprint PolicyFingerprint { get; init; } = new(new string('0', 64));

    public Sha256Fingerprint ThresholdFingerprint { get; init; } = new(new string('0', 64));

    public Sha256Fingerprint LimitFingerprint { get; init; } = new(new string('0', 64));

    public Sha256Fingerprint AnalyzerSetFingerprint { get; init; } = new(new string('0', 64));

    public IReadOnlyList<CandidateAnalyzerBindingContract> AnalyzerBindings { get; init; } = [];

    public IReadOnlyList<string> ExecutionInputDescriptors { get; init; } = [];

    public IReadOnlyList<string> PolicyDescriptors { get; init; } = [];

    public IReadOnlyList<string> ThresholdDescriptors { get; init; } = [];

    public IReadOnlyList<string> LimitDescriptors { get; init; } = [];

    public IReadOnlyList<CandidateHypothesisContract> Hypotheses { get; init; } = [];

    public IReadOnlyList<CandidateDependencyEdgeContract> DependencyEdges { get; init; } = [];

    public CandidatePopulationCountsContract Counts { get; init; } = new(
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record FindingContract(
    OpaqueId FindingOccurrenceId,
    OpaqueId LogicalFindingId,
    OpaqueId OriginatingRunId,
    OpaqueId CandidateId,
    string Conclusion,
    FindingSeverity Severity,
    AnalysisConfidence Confidence,
    IReadOnlyList<OpaqueId> EvidenceIds,
    OpaqueId IdentityEnvelopeId,
    OpaqueId? SupersedesOccurrenceId);

public sealed record Slice5RecommendationContract(
    OpaqueId RecommendationId,
    RecommendationKind Kind,
    OpaqueId? FindingOccurrenceId,
    OpaqueId? AbstentionId,
    string Action,
    string Uncertainty,
    string Reversibility,
    IReadOnlyList<string> Risks,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record Slice5CaseContract(
    OpaqueId CaseOccurrenceId,
    OpaqueId LogicalCaseId,
    OpaqueId OriginatingRunId,
    CaseOccurrenceKind Kind,
    IReadOnlyList<OpaqueId> FindingOccurrenceIds,
    IReadOnlyList<OpaqueId> CandidateIds,
    string SharedCause,
    IReadOnlyList<OpaqueId> CauseProofEvidenceIds,
    bool AffectsReadiness);

public sealed record ReconciliationGatesContract(
    ReconciliationGateState Causal,
    ReconciliationGateState Applicability,
    ReconciliationGateState Dependency,
    ReconciliationGateState Producer);

public sealed record Slice5ReconciliationContract(
    OpaqueId AssessmentId,
    OpaqueId PriorOccurrenceId,
    OpaqueId CurrentOccurrenceId,
    ReconciliationGatesContract Gates,
    ReconciliationOutcome Outcome,
    IReadOnlyList<string> Gaps);

public sealed record Slice5LineageContract(
    OpaqueId EventId,
    LineageKind Kind,
    IReadOnlyList<OpaqueId> PredecessorIds,
    IReadOnlyList<OpaqueId> SuccessorIds,
    OpaqueId? ReconciliationAssessmentId);

public sealed record FindingCaseContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PayloadId,
    OpaqueId OriginatingRunId,
    IReadOnlyList<FindingContract> Findings,
    IReadOnlyList<Slice5RecommendationContract> Recommendations,
    IReadOnlyList<Slice5CaseContract> Cases,
    IReadOnlyList<Slice5ReconciliationContract> ReconciliationAssessments,
    IReadOnlyList<Slice5LineageContract> LineageEvents,
    IReadOnlyList<TaxonomyAssignmentContract> TaxonomyAssignments,
    IReadOnlyList<CoverageContract> Coverage,
    IReadOnlyList<CoverageGapContract> Gaps);

public sealed record ReplayDependencyNodeContract(
    OpaqueId DependencyId,
    string Kind,
    ContractVersion Version,
    Sha256Fingerprint Fingerprint,
    Slice5ResultState State);

public sealed record ReplayDependencyEdgeContract(OpaqueId From, OpaqueId To);

public sealed record ReplayOutputContract(
    OpaqueId ArtifactId,
    string SchemaId,
    ContractVersion SchemaVersion,
    Sha256Fingerprint SemanticFingerprint,
    Sha256Fingerprint ByteFingerprint);

public sealed record AnalysisReplayContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId ReplayManifestId,
    OpaqueId OriginatingRunId,
    ReplayMode Mode,
    ReplayState ReplayState,
    AuditabilityState AuditabilityState,
    IReadOnlyList<ReplayDependencyNodeContract> Dependencies,
    IReadOnlyList<ReplayDependencyEdgeContract> Edges,
    IReadOnlyList<ReplayOutputContract> Outputs,
    IReadOnlyList<OpaqueId> MissingDependencyIds,
    IReadOnlyList<OpaqueId> CoverageGapIds,
    bool SemanticallyEquivalent,
    OpaqueId? ComparedRunId);

public sealed record ExecutionBoundaryContract(string BoundaryId, BoundaryUseState State, string Reason);

public static class ExecutionBoundaryContractInvariants
{
    private static readonly HashSet<string> ProductCapabilityIds = new(StringComparer.Ordinal)
    {
        "provider",
        "hosted-search",
        "nexus",
        "loot",
    };

    public static void ValidateProductCapabilities(
        IReadOnlyList<ExecutionBoundaryContract> boundaries,
        bool requireNotUsed)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        HashSet<string> actualIds = boundaries.Select(item => item.BoundaryId).ToHashSet(StringComparer.Ordinal);
        if (boundaries.Count != ProductCapabilityIds.Count
            || !actualIds.SetEquals(ProductCapabilityIds)
            || boundaries.Any(item => item.State == BoundaryUseState.Unspecified)
            || (requireNotUsed && boundaries.Any(item => item.State != BoundaryUseState.NotUsed)))
        {
            throw new InvalidOperationException(
                "Execution boundaries must declare exactly the four product capabilities with closed states.");
        }
    }
}

public sealed record AnalysisExecutionLimitsContract(
    long MaximumEntities,
    long MaximumEdges,
    long MaximumTruthRows,
    long MaximumOutputItems,
    long MaximumWallTimeMilliseconds);

public sealed record AnalysisExecutionInputContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId ExecutionInputId,
    OpaqueId RunId,
    ArtifactReferenceContract InstallationSnapshot,
    ArtifactReferenceContract BethesdaSemanticInput,
    IReadOnlyList<ArtifactReferenceContract> SourceInputs,
    IReadOnlyList<ArtifactReferenceContract> AnalyzerDeclarations,
    ArtifactReferenceContract EffectiveConfiguration,
    ArtifactReferenceContract ResolvedInputManifest,
    ReplayMode Mode,
    OpaqueId? PriorRunId,
    long Seed,
    AnalysisExecutionLimitsContract Limits,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries);

public static class Slice5ContractInvariants
{
    private static readonly HashSet<string> DocumentationPurposeCodes = new(StringComparer.Ordinal)
    {
        "purpose.add-expand",
        "purpose.replace-overhaul",
        "purpose.modify-tune",
        "purpose.fix-restore",
        "purpose.integrate-patch",
        "purpose.configure-expose-choice",
        "purpose.generate-precompute",
        "purpose.provide-runtime-framework",
        "purpose.provide-tool-workflow",
        "purpose.remove-disable",
    };

    public static void Validate(DocumentationEvidenceContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.DocumentationEvidenceSchemaId);
        RequireUnique(value.Revisions.Select(item => item.RevisionId), "documentation revisions");
        RequireUnique(value.Imports.Select(item => item.ImportId), "documentation imports");
        RequireUnique(value.Passages.Select(item => item.PassageId), "documentation passages");
        RequireUnique(value.Claims.Select(item => item.ClaimId), "documentation claims");
        RequireUnique(value.Applications.Select(item => item.ApplicationId), "claim applications");
        HashSet<OpaqueId> revisions = value.Revisions.Select(item => item.RevisionId).ToHashSet();
        HashSet<OpaqueId> passages = value.Passages.Select(item => item.PassageId).ToHashSet();
        HashSet<OpaqueId> claims = value.Claims.Select(item => item.ClaimId).ToHashSet();
        HashSet<OpaqueId> producingImports = value.Imports
            .SelectMany(item => item.ReusedImportId is null
                ? new[] { item.ImportId }
                : new[] { item.ImportId, item.ReusedImportId })
            .OfType<OpaqueId>()
            .ToHashSet();
        foreach (DocumentationRevisionContract revision in value.Revisions)
        {
            if (revision.SourceKind == DocumentationSourceKind.Unspecified
                || string.IsNullOrWhiteSpace(revision.SourceRevision)
                || revision.ByteLength < 0
                || revision.RetentionState is not (Slice5ResultState.Present or Slice5ResultState.Partial or Slice5ResultState.Unavailable)
                || revision.ReplayState == ReplayState.Unspecified
                || (revision.SourceKind == DocumentationSourceKind.ProjectAuthoredLocal
                    && revision.SupplyingSnapshotId is null))
            {
                throw new InvalidOperationException("Documentation revisions require closed source/revision, local supplying-snapshot, retention, and replay state.");
            }
        }
        foreach (DocumentationImportContract import in value.Imports)
        {
            if (!revisions.Contains(import.RevisionId)
                || import.ImportRunId != value.OriginatingRunId
                || import.Mode == DocumentationImportMode.Unspecified
                || (import.Mode == DocumentationImportMode.CleanImport && import.ReusedImportId is not null)
                || (import.Mode == DocumentationImportMode.RetainedReuse
                    && (import.ReusedImportId is null || import.ReusedImportId == import.ImportId))
                || import.LlmInvolvement != LlmInvolvementState.None
                || import.LlmOperation != LlmOperation.None)
            {
                throw new InvalidOperationException("Documentation imports require an admitted revision, closed mode, and explicit llm = none.");
            }
            ExecutionBoundaryContractInvariants.ValidateProductCapabilities(import.Boundaries, requireNotUsed: true);
        }
        if (!revisions.SetEquals(value.Imports.Select(item => item.RevisionId)))
        {
            throw new InvalidOperationException("Every documentation revision requires at least one explicit import or retained-reuse record.");
        }
        foreach (DocumentationPassageContract passage in value.Passages)
        {
            if (!revisions.Contains(passage.RevisionId)
                || passage.Utf8StartOffset < 0
                || passage.Utf8EndOffset <= passage.Utf8StartOffset)
            {
                throw new InvalidOperationException("Passages require an existing revision and a non-empty UTF-8 byte range.");
            }
        }
        foreach (DocumentationClaimContract claim in value.Claims)
        {
            if (!passages.Contains(claim.PassageId)
                || !producingImports.Contains(claim.ProducingImportId)
                || claim.Kind == ClaimKind.Unspecified
                || claim.Authority != EvidenceAuthority.AuthoritativeExternal
                || claim.Applicability == ClaimApplicabilityState.Unspecified
                || claim.ClassificationRole == ClassificationRole.Unspecified
                || (claim.Kind == ClaimKind.DeclaredPurpose && claim.ClassificationRole != ClassificationRole.Declared)
                || !claim.ContradictingEvidenceIds.All(claims.Contains)
                || claim.ContradictingEvidenceIds.Contains(claim.ClaimId)
                || claim.ContradictingEvidenceIds.Distinct().Count() != claim.ContradictingEvidenceIds.Count)
            {
                throw new InvalidOperationException("Claims require admitted passages and closed authority, applicability, kind, and role states.");
            }
        }
        foreach (ClaimApplicationContract application in value.Applications)
        {
            if (!claims.Contains(application.ClaimId)
                || application.Applicability == ClaimApplicabilityState.Unspecified
                || !StringComparer.Ordinal.Equals(application.SubjectType, "installed-entity")
                || !application.EvidenceIds.All(claims.Contains)
                || !application.EvidenceIds.Contains(application.ClaimId)
                || application.EvidenceIds.Distinct().Count() != application.EvidenceIds.Count)
            {
                throw new InvalidOperationException(
                    "Claim applications require an existing claim and a closed applicability state.");
            }
        }
        HashSet<OpaqueId> applications = value.Applications.Select(item => item.ApplicationId).ToHashSet();
        RequireUnique(value.PurposeAssignments.Select(item => item.AssignmentId), "documentation purpose assignments");
        foreach (DocumentationPurposeAssignmentContract assignment in value.PurposeAssignments)
        {
            DocumentationClaimContract? purposeClaim = value.Claims.SingleOrDefault(item => item.ClaimId == assignment.ClaimId);
            if (assignment.TaxonomyId != ContractConstants.TaxonomyId
                || assignment.Role != ClassificationRole.Declared
                || assignment.Axis != "declared-purpose-and-intended-feature-area"
                || assignment.Facet != "purpose-kind"
                || assignment.TaxonomyVersion != new ContractVersion(0, 1, 0)
                || assignment.Applicability != TaxonomyApplicability.Assigned
                || !DocumentationPurposeCodes.Contains(assignment.Code)
                || !StringComparer.Ordinal.Equals(assignment.SubjectType, "installed-entity")
                || !claims.Contains(assignment.ClaimId)
                || !applications.Contains(assignment.ApplicationId)
                || purposeClaim is null
                || purposeClaim.Kind != ClaimKind.DeclaredPurpose
                || purposeClaim.Authority != EvidenceAuthority.AuthoritativeExternal
                || purposeClaim.ClassificationRole != ClassificationRole.Declared
                || purposeClaim.Applicability != ClaimApplicabilityState.Applicable
                || !assignment.ApplicabilityConditionIds.All(claims.Contains)
                || assignment.ApplicabilityConditionIds.Distinct().Count()
                    != assignment.ApplicabilityConditionIds.Count
                || !value.Applications.Any(item =>
                    item.ApplicationId == assignment.ApplicationId
                    && item.ClaimId == assignment.ClaimId
                    && item.SubjectId == assignment.SubjectId
                    && StringComparer.Ordinal.Equals(item.SubjectType, assignment.SubjectType)
                    && item.Applicability == ClaimApplicabilityState.Applicable))
            {
                throw new InvalidOperationException("Purpose assignments require declared-purpose taxonomy authority and admitted claim evidence.");
            }
        }
        RequireUnique(value.Gaps.Select(item => item.GapId), "documentation gaps");
        foreach (DocumentationGapContract gap in value.Gaps)
        {
            if (gap.Kind == DocumentationGapKind.Unspecified
                || gap.OriginatingRunId != value.OriginatingRunId
                || !revisions.Contains(gap.RevisionId)
                || (gap.ClaimId is not null && !claims.Contains(gap.ClaimId))
                || (gap.ApplicationId is not null && !applications.Contains(gap.ApplicationId))
                || gap.ReplayEffect == ReplayState.Unspecified
                || string.IsNullOrWhiteSpace(gap.Reason))
            {
                throw new InvalidOperationException("Documentation gaps require admitted references and closed gap/replay semantics.");
            }
        }
        RequireUnique(value.DeletionReceipts.Select(item => item.ReceiptId), "documentation deletion receipts");
        if (value.DeletionReceipts.Count != 0
            && !value.Imports.Any(item => item.Mode == DocumentationImportMode.RetainedReuse))
        {
            throw new InvalidOperationException(
                "Documentation deletion receipts require retained-reuse provenance over prior admitted evidence.");
        }
        foreach (DocumentationDeletionReceiptContract receipt in value.DeletionReceipts)
        {
            if (receipt.OriginatingRunId != value.OriginatingRunId
                || !revisions.Contains(receipt.RevisionId)
                || receipt.DeletedPassageIds.Distinct().Count() != receipt.DeletedPassageIds.Count
                || !receipt.DeletedPassageIds.All(passages.Contains)
                || receipt.IndependentlyRetainedPayloadIds.Distinct().Count()
                    != receipt.IndependentlyRetainedPayloadIds.Count
                || receipt.ReplayEffect is not (ReplayState.AuditOnly or ReplayState.Unavailable)
                || string.IsNullOrWhiteSpace(receipt.Reason))
            {
                throw new InvalidOperationException("Documentation deletion receipts require exact retained identity and replay effects.");
            }
        }
        if (value.Gaps.Any(item => item.Kind == DocumentationGapKind.Deletion)
            != (value.DeletionReceipts.Count != 0))
        {
            throw new InvalidOperationException("Documentation deletion gaps and receipts must be emitted together.");
        }
        if (value.Claims.Any(claim =>
                (claim.Applicability == ClaimApplicabilityState.Contradicted
                 || claim.ContradictingEvidenceIds.Count != 0)
                && !value.Gaps.Any(gap =>
                    gap.Kind == DocumentationGapKind.Contradiction
                    && gap.ClaimId == claim.ClaimId))
            || value.Applications.Any(application =>
                application.Applicability == ClaimApplicabilityState.Contradicted
                && !value.Gaps.Any(gap =>
                    gap.Kind == DocumentationGapKind.Contradiction
                    && gap.ApplicationId == application.ApplicationId)))
        {
            throw new InvalidOperationException("Contradicted documentation claims and applications require explicit contradiction gaps.");
        }
        RequireUnique(value.Failures.Select(item => item.FailureId), "documentation failures");
        if (value.Failures.Any(item =>
                string.IsNullOrWhiteSpace(item.FailureCode)
                || item.FailureCode.Length > 128
                || string.IsNullOrWhiteSpace(item.Message)
                || item.Message.Length > 512))
        {
            throw new InvalidOperationException("Documentation failures require a code and bounded diagnostic message.");
        }
        if (DocumentationEvidenceIdentity.ComputePayloadId(value) != value.PayloadId)
        {
            throw new InvalidOperationException(
                "Documentation evidence payload identity must cover the exact aggregate semantics.");
        }
    }

    public static void Validate(CandidateAnalysisContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.CandidateAnalysisSchemaId);
        ArgumentNullException.ThrowIfNull(value.Counts);
        if (value.PopulationDenominator < 0 || value.Decisions.Count != value.PopulationDenominator)
        {
            throw new InvalidOperationException("Every candidate population member requires exactly one eligible decision.");
        }
        RequireUnique(value.Decisions.Select(item => item.DecisionId), "candidate decisions");
        RequireUnique(value.Decisions.Select(item => item.PopulationMemberId), "candidate population members");
        RequireUnique(value.Candidates.Select(item => item.CandidateId), "candidates");
        RequireUnique(value.Candidates.Select(item => item.DecisionId), "candidate decision references");
        RequireUnique(value.Hypotheses.Select(item => item.HypothesisId), "candidate hypotheses");
        RequireUnique(value.Hypotheses.Select(item => item.CandidateId), "hypothesis candidate references");
        RequireUnique(value.Abstentions.Select(item => item.AbstentionId), "candidate abstentions");
        RequireUnique(value.Gaps.Select(item => item.GapId), "candidate gaps");
        RequireUnique(value.Gaps.Select(item => item.DecisionId), "candidate gap decision references");
        RequireUnique(value.Failures.Select(item => item.FailureId), "candidate failures");
        RequireUnique(value.DependencyEdges.Select(item => item.EdgeId), "candidate dependency edges");
        RequireUnique(value.AnalyzerBindings.Select(item => item.AnalyzerId), "candidate analyzer bindings");
        HashSet<OpaqueId> boundAnalyzers = value.AnalyzerBindings.Select(item => item.AnalyzerId).ToHashSet();
        Sha256Fingerprint expectedAnalyzerSetFingerprint = CandidateAnalysisIdentity.StructuralHash(
            value.AnalyzerBindings.Select(item =>
                $"{item.AnalyzerId.Value}:{item.DeclarationFingerprint.Value}"));
        bool invalidDescriptors = new[]
            {
                value.ExecutionInputDescriptors,
                value.PolicyDescriptors,
                value.ThresholdDescriptors,
                value.LimitDescriptors,
            }
            .Any(items => items.Count is 0 or > 512
                || items.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 4096));
        if (value.AnalyzerBindings.Count == 0
            || value.AnalysisRootId != CandidateAnalysisIdentity.StableId(
                "candidate-analysis-root", value.OriginatingRunId.Value, value.PopulationId.Value,
                value.ExecutionInputFingerprint.Value, value.PolicyFingerprint.Value,
                value.ThresholdFingerprint.Value, value.LimitFingerprint.Value, value.AnalyzerSetFingerprint.Value)
            || value.Decisions.Any(item => !boundAnalyzers.Contains(item.AnalyzerId))
            || value.AnalyzerSetFingerprint != expectedAnalyzerSetFingerprint
            || value.AnalyzerBindings.Any(item => string.IsNullOrWhiteSpace(item.CanonicalDeclarationJson)
                || item.CanonicalDeclarationJson.Length > 65536
                || item.DeclarationFingerprint != CandidateAnalysisIdentity.StructuralHash([item.CanonicalDeclarationJson]))
            || invalidDescriptors
            || value.ExecutionInputFingerprint != CandidateAnalysisIdentity.StructuralHash(value.ExecutionInputDescriptors)
            || value.PolicyFingerprint != CandidateAnalysisIdentity.StructuralHash(value.PolicyDescriptors)
            || value.ThresholdFingerprint != CandidateAnalysisIdentity.StructuralHash(value.ThresholdDescriptors)
            || value.LimitFingerprint != CandidateAnalysisIdentity.StructuralHash(value.LimitDescriptors))
        {
            throw new InvalidOperationException("Candidate analysis requires exact execution, analyzer, policy, threshold, and limit semantic bindings.");
        }
        Dictionary<OpaqueId, CandidateDecisionContract> decisions = value.Decisions.ToDictionary(item => item.DecisionId);
        foreach (CandidateDecisionContract decision in value.Decisions)
        {
            if (decision.Lane == CandidateLane.Unspecified
                || decision.Disposition == CandidateDecisionDisposition.Unspecified
                || decision.Disposition == CandidateDecisionDisposition.Abstained
                || StringComparer.Ordinal.Equals(decision.SourceFactId.Value, "source-fact-unspecified")
                || decision.Participants.Count > 16
                || (decision.Disposition is not (CandidateDecisionDisposition.InvalidInput or CandidateDecisionDisposition.Failed)
                    && decision.Participants.Count < 2)
                || decision.Participants.Select(item => item.Role).Distinct(StringComparer.Ordinal).Count() != decision.Participants.Count
                || decision.Participants.Any(item => string.IsNullOrWhiteSpace(item.Role)
                    || item.Role.Length > 128
                    || !IsAsciiToken(item.Role))
                || string.IsNullOrWhiteSpace(decision.JoinKind)
                || decision.JoinKind.Length > 128
                || !IsAsciiToken(decision.JoinKind)
                || decision.Path.Count > 64
                || (decision.Disposition is not (CandidateDecisionDisposition.InvalidInput or CandidateDecisionDisposition.Failed)
                    && decision.Path.Count == 0)
                || (decision.Disposition is not (CandidateDecisionDisposition.InvalidInput or CandidateDecisionDisposition.Failed)
                    && decision.Participants.Any(item => !decision.Path.Contains(item.ParticipantId)))
                || decision.EvidenceIds.Count > 128
                || (decision.Disposition is not (CandidateDecisionDisposition.InvalidInput or CandidateDecisionDisposition.Failed)
                    && decision.EvidenceIds.Count == 0)
                || decision.EvidenceIds.Distinct().Count() != decision.EvidenceIds.Count
                || decision.DependencyIds.Count > 128
                || (decision.Disposition is not (CandidateDecisionDisposition.InvalidInput or CandidateDecisionDisposition.Failed)
                    && decision.DependencyIds.Count == 0)
                || decision.DependencyIds.Distinct().Count() != decision.DependencyIds.Count
                || decision.DependencyClosureId != CandidateAnalysisIdentity.StableId(
                    "candidate-closure",
                    decision.DependencyIds.Select(item => item.Value).Prepend(decision.PopulationMemberId.Value).ToArray())
                || decision.PolicyId != value.PolicyId
                || decision.ThresholdId != value.ThresholdId
                || decision.LimitId != value.LimitId
                || string.IsNullOrWhiteSpace(decision.Rationale)
                || decision.Rationale.Length > 4096
                || decision.AdmissionIndependentOfScore !=
                    (decision.Lane is CandidateLane.DeterministicRequired or CandidateLane.MandatoryEvidence)
                || (decision.Disposition == CandidateDecisionDisposition.Limited
                    && decision.Lane != CandidateLane.OptionalRanked)
                || (decision.Lane != CandidateLane.OptionalRanked && decision.OptionalRank is not null)
                || (decision.Lane == CandidateLane.OptionalRanked && decision.OptionalRank is null or <= 0))
            {
                throw new InvalidOperationException("Candidate decisions require closed lane/disposition, canonical roles, and score-independent mandatory admission.");
            }
        }
        HashSet<OpaqueId> admittedDecisionIds = value.Decisions
            .Where(item => item.Disposition is CandidateDecisionDisposition.CandidateAdmitted
                or CandidateDecisionDisposition.Ambiguous)
            .Select(item => item.DecisionId)
            .ToHashSet();
        HashSet<OpaqueId> candidateDecisionIds = value.Candidates.Select(item => item.DecisionId).ToHashSet();
        if (!admittedDecisionIds.SetEquals(candidateDecisionIds))
        {
            throw new InvalidOperationException("Every admitted decision requires exactly one candidate, and no other decision may own a candidate.");
        }
        foreach (CandidateAnalysisEntryContract candidate in value.Candidates)
        {
            if (!decisions.TryGetValue(candidate.DecisionId, out CandidateDecisionContract? decision)
                || decision.Disposition is not (CandidateDecisionDisposition.CandidateAdmitted
                    or CandidateDecisionDisposition.Ambiguous)
                || candidate.State is not (Slice5ResultState.Present or Slice5ResultState.Ambiguous or Slice5ResultState.Abstained)
                || candidate.Confidence == AnalysisConfidence.Unspecified
                || candidate.ThresholdId != value.ThresholdId
                || string.IsNullOrWhiteSpace(candidate.CausalExplanation)
                || candidate.CausalExplanation.Length > 4096
                || candidate.SupportingEvidenceIds.Count > 128
                || candidate.ContradictingEvidenceIds.Count > 128
                || candidate.MissingInformation.Count > 32
                || candidate.MissingInformation.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 1024)
                || candidate.SupportingEvidenceIds.Distinct().Count() != candidate.SupportingEvidenceIds.Count
                || candidate.ContradictingEvidenceIds.Distinct().Count() != candidate.ContradictingEvidenceIds.Count
                || !candidate.SupportingEvidenceIds.ToHashSet().SetEquals(decision.EvidenceIds)
                || (candidate.HypothesisId is not null) != value.Hypotheses.Any(item => item.CandidateId == candidate.CandidateId)
                || candidate.HypothesisId is null
                || (candidate.AbstentionId is not null) != value.Abstentions.Any(item => item.CandidateId == candidate.CandidateId)
                || (candidate.AbstentionId is not null) != (candidate.MissingInformation.Count != 0)
                || (candidate.State == Slice5ResultState.Abstained) != (candidate.MissingInformation.Count != 0)
                || (candidate.State == Slice5ResultState.Ambiguous) != (candidate.MissingInformation.Count == 0
                    && candidate.ContradictingEvidenceIds.Count != 0)
                || (candidate.State == Slice5ResultState.Present) != (candidate.MissingInformation.Count == 0
                    && candidate.ContradictingEvidenceIds.Count == 0))
            {
                throw new InvalidOperationException("Candidates require one admitted decision and explicit, closed hypothesis/abstention linkage.");
            }
        }
        Dictionary<OpaqueId, CandidateAnalysisEntryContract> candidates = value.Candidates.ToDictionary(item => item.CandidateId);
        foreach (CandidateHypothesisContract hypothesis in value.Hypotheses)
        {
            if (!candidates.TryGetValue(hypothesis.CandidateId, out CandidateAnalysisEntryContract? candidate)
                || candidate.HypothesisId != hypothesis.HypothesisId
                || hypothesis.State is not (Slice5ResultState.Present or Slice5ResultState.Ambiguous or Slice5ResultState.Partial)
                || hypothesis.Confidence == AnalysisConfidence.Unspecified
                || hypothesis.ThresholdId != value.ThresholdId
                || string.IsNullOrWhiteSpace(hypothesis.ProposedExplanation)
                || hypothesis.ProposedExplanation.Length > 4096
                || string.IsNullOrWhiteSpace(hypothesis.PredictedImpact)
                || hypothesis.PredictedImpact.Length > 4096
                || hypothesis.SupportingEvidenceIds.Count > 128
                || hypothesis.ContradictingEvidenceIds.Count > 128
                || hypothesis.MissingInformation.Count > 32
                || !hypothesis.SupportingEvidenceIds.ToHashSet().SetEquals(candidate.SupportingEvidenceIds)
                || !hypothesis.ContradictingEvidenceIds.ToHashSet().SetEquals(candidate.ContradictingEvidenceIds)
                || !hypothesis.MissingInformation.SequenceEqual(candidate.MissingInformation, StringComparer.Ordinal)
                || (candidate.State == Slice5ResultState.Abstained
                    ? hypothesis.State != Slice5ResultState.Partial
                    : hypothesis.State != candidate.State))
            {
                throw new InvalidOperationException("Every hypothesis requires one linked candidate and closed evidence-bound state.");
            }
        }
        foreach (CandidateAbstentionContract abstention in value.Abstentions)
        {
            if (!decisions.TryGetValue(abstention.DecisionId, out CandidateDecisionContract? abstentionDecision)
                || abstention.AnalyzerId != abstentionDecision.AnalyzerId
                || (abstention.CandidateId is not null
                    && (!candidates.TryGetValue(abstention.CandidateId, out CandidateAnalysisEntryContract? candidate)
                        || candidate.DecisionId != abstention.DecisionId
                        || candidate.AbstentionId != abstention.AbstentionId
                        || !abstention.RequiredInformation.SequenceEqual(candidate.MissingInformation, StringComparer.Ordinal)))
                || (abstention.CandidateId is null
                    && abstentionDecision.Disposition is not (CandidateDecisionDisposition.Abstained
                        or CandidateDecisionDisposition.Unsupported))
                || string.IsNullOrWhiteSpace(abstention.Reason)
                || abstention.Reason.Length > 4096
                || abstention.RequiredInformation.Count is 0 or > 32
                || abstention.RequiredInformation.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 1024))
            {
                throw new InvalidOperationException("Abstentions require a decision, optional linked candidate, reason, and required information.");
            }
        }
        HashSet<OpaqueId> unsupportedDecisionIds = value.Decisions
            .Where(item => item.Disposition == CandidateDecisionDisposition.Unsupported)
            .Select(item => item.DecisionId)
            .ToHashSet();
        OpaqueId[] unsupportedAbstentionDecisionIds = value.Abstentions
            .Where(item => item.CandidateId is null)
            .Select(item => item.DecisionId)
            .ToArray();
        if (unsupportedAbstentionDecisionIds.Distinct().Count() != unsupportedAbstentionDecisionIds.Length
            || !unsupportedDecisionIds.SetEquals(unsupportedAbstentionDecisionIds))
        {
            throw new InvalidOperationException("Unsupported decisions and candidate-less abstentions must correspond exactly.");
        }
        foreach (CandidateGapContract gap in value.Gaps)
        {
            if (!decisions.TryGetValue(gap.DecisionId, out CandidateDecisionContract? gapDecision)
                || gap.PopulationId != value.PopulationId
                || gap.State is Slice5ResultState.Unspecified or Slice5ResultState.Present
                || (gapDecision.Disposition switch
                {
                    CandidateDecisionDisposition.CandidateAdmitted or CandidateDecisionDisposition.Ambiguous => gap.State != Slice5ResultState.Missing,
                    CandidateDecisionDisposition.Unsupported => gap.State != Slice5ResultState.Unsupported,
                    CandidateDecisionDisposition.Limited or CandidateDecisionDisposition.Unprocessed => gap.State != Slice5ResultState.LimitReached,
                    CandidateDecisionDisposition.Deferred => gap.State != Slice5ResultState.Partial,
                    CandidateDecisionDisposition.Failed => gap.State != Slice5ResultState.Failed,
                    _ => true,
                })
                || (gapDecision.Disposition is CandidateDecisionDisposition.CandidateAdmitted
                        or CandidateDecisionDisposition.Ambiguous
                    && !value.Candidates.Any(candidate =>
                        candidate.DecisionId == gap.DecisionId
                        && candidate.State == Slice5ResultState.Abstained
                        && candidate.MissingInformation.Contains(
                            gap.MissingCapabilityOrInformation,
                            StringComparer.Ordinal)))
                || string.IsNullOrWhiteSpace(gap.Reason)
                || gap.Reason.Length > 4096
                || string.IsNullOrWhiteSpace(gap.MissingCapabilityOrInformation)
                || gap.MissingCapabilityOrInformation.Length > 1024)
            {
                throw new InvalidOperationException("Candidate gaps require a closed non-present state and a population decision.");
            }
        }
        foreach (CandidateFailureContract failure in value.Failures)
        {
            if (failure.PopulationMemberIds.Count is 0 or > 1024
                || failure.PopulationMemberIds.Any(id => value.Decisions.All(decision => decision.PopulationMemberId != id))
                || failure.PopulationMemberIds.Any(id => value.Decisions.Single(decision => decision.PopulationMemberId == id).AnalyzerId != failure.AnalyzerId)
                || string.IsNullOrWhiteSpace(failure.FailureCode)
                || string.IsNullOrWhiteSpace(failure.Message)
                || failure.Message.Length > 512)
            {
                throw new InvalidOperationException("Candidate failures require bounded diagnostics and affected population members.");
            }
        }
        HashSet<OpaqueId> failedMemberIds = value.Decisions
            .Where(item => item.Disposition == CandidateDecisionDisposition.Failed)
            .Select(item => item.PopulationMemberId)
            .ToHashSet();
        OpaqueId[] retainedFailedMemberIds = value.Failures
            .SelectMany(item => item.PopulationMemberIds)
            .ToArray();
        if (retainedFailedMemberIds.Distinct().Count() != retainedFailedMemberIds.Length
            || !failedMemberIds.SetEquals(retainedFailedMemberIds))
        {
            throw new InvalidOperationException("Failed decisions and retained failure diagnostics must correspond exactly.");
        }
        HashSet<OpaqueId> requiredGapDecisionIds = value.Decisions
            .Where(item => item.Disposition is CandidateDecisionDisposition.Unsupported
                or CandidateDecisionDisposition.Limited
                or CandidateDecisionDisposition.Unprocessed
                or CandidateDecisionDisposition.Deferred)
            .Select(item => item.DecisionId)
            .ToHashSet();
        HashSet<OpaqueId> retainedRequiredGapDecisionIds = value.Gaps
            .Where(item => requiredGapDecisionIds.Contains(item.DecisionId))
            .Select(item => item.DecisionId)
            .ToHashSet();
        if (!requiredGapDecisionIds.SetEquals(retainedRequiredGapDecisionIds))
        {
            throw new InvalidOperationException("Unsupported, limited, deferred, and unprocessed decisions require explicit gaps.");
        }
        HashSet<CandidateDependencyEdgeContract> expectedEdges = [];
        expectedEdges.Add(CandidateEdge("candidate-analysis-root", value.AnalysisRootId, "execution-input-binding",
            CandidateAnalysisIdentity.StableId("candidate-execution-input-binding", value.ExecutionInputId.Value, value.ExecutionInputFingerprint.Value), "uses"));
        expectedEdges.Add(CandidateEdge("candidate-analysis-root", value.AnalysisRootId, "policy-binding",
            CandidateAnalysisIdentity.StableId("candidate-policy-binding", value.PolicyId.Value, value.PolicyFingerprint.Value), "uses"));
        expectedEdges.Add(CandidateEdge("candidate-analysis-root", value.AnalysisRootId, "threshold-binding",
            CandidateAnalysisIdentity.StableId("candidate-threshold-binding", value.ThresholdId.Value, value.ThresholdFingerprint.Value), "uses"));
        expectedEdges.Add(CandidateEdge("candidate-analysis-root", value.AnalysisRootId, "limit-binding",
            CandidateAnalysisIdentity.StableId("candidate-limit-binding", value.LimitId.Value, value.LimitFingerprint.Value), "uses"));
        foreach (CandidateAnalyzerBindingContract analyzerBinding in value.AnalyzerBindings)
        {
            expectedEdges.Add(CandidateEdge("candidate-analysis-root", value.AnalysisRootId, "analyzer-declaration-binding",
                CandidateAnalysisIdentity.StableId("candidate-analyzer-binding", analyzerBinding.AnalyzerId.Value,
                    analyzerBinding.AnalyzerVersion.ToString(), analyzerBinding.RulesetVersion.ToString(), analyzerBinding.DeclarationFingerprint.Value), "uses"));
        }
        foreach (CandidateDecisionContract decision in value.Decisions)
        {
            expectedEdges.Add(CandidateEdge("candidate-decision", decision.DecisionId, "source-fact", decision.SourceFactId, "derived-from"));
            expectedEdges.Add(CandidateEdge("candidate-decision", decision.DecisionId, "dependency-closure", decision.DependencyClosureId, "depends-on"));
            foreach (OpaqueId dependencyId in decision.DependencyIds)
            {
                expectedEdges.Add(CandidateEdge("dependency-closure", decision.DependencyClosureId, "dependency", dependencyId, "depends-on"));
            }
            foreach (OpaqueId evidenceId in decision.EvidenceIds)
            {
                expectedEdges.Add(CandidateEdge("candidate-decision", decision.DecisionId, "evidence", evidenceId, "derived-from"));
            }
        }
        foreach (CandidateAnalysisEntryContract candidate in value.Candidates)
        {
            expectedEdges.Add(CandidateEdge("candidate", candidate.CandidateId, "candidate-decision", candidate.DecisionId, "derived-from"));
            foreach (OpaqueId evidenceId in candidate.SupportingEvidenceIds)
            {
                expectedEdges.Add(CandidateEdge("candidate", candidate.CandidateId, "evidence", evidenceId, "supports"));
            }
            foreach (OpaqueId evidenceId in candidate.ContradictingEvidenceIds)
            {
                expectedEdges.Add(CandidateEdge("candidate", candidate.CandidateId, "evidence", evidenceId, "contradicts"));
            }
        }
        foreach (CandidateHypothesisContract hypothesis in value.Hypotheses)
        {
            expectedEdges.Add(CandidateEdge("hypothesis", hypothesis.HypothesisId, "candidate", hypothesis.CandidateId, "derived-from"));
            foreach (OpaqueId evidenceId in hypothesis.SupportingEvidenceIds)
            {
                expectedEdges.Add(CandidateEdge("hypothesis", hypothesis.HypothesisId, "evidence", evidenceId, "supports"));
            }
            foreach (OpaqueId evidenceId in hypothesis.ContradictingEvidenceIds)
            {
                expectedEdges.Add(CandidateEdge("hypothesis", hypothesis.HypothesisId, "evidence", evidenceId, "contradicts"));
            }
        }
        foreach (CandidateAbstentionContract abstention in value.Abstentions)
        {
            expectedEdges.Add(CandidateEdge("abstention", abstention.AbstentionId, "candidate-decision", abstention.DecisionId, "derived-from"));
        }
        foreach (CandidateGapContract gap in value.Gaps)
        {
            expectedEdges.Add(CandidateEdge("gap", gap.GapId, "candidate-decision", gap.DecisionId, "derived-from"));
        }
        foreach (CandidateFailureContract failure in value.Failures)
        {
            foreach (OpaqueId memberId in failure.PopulationMemberIds)
            {
                OpaqueId decisionId = value.Decisions.Single(item => item.PopulationMemberId == memberId).DecisionId;
                expectedEdges.Add(CandidateEdge("failure", failure.FailureId, "candidate-decision", decisionId, "derived-from"));
            }
        }
        if (!expectedEdges.SetEquals(value.DependencyEdges))
        {
            throw new InvalidOperationException("Candidate dependency edges must exactly close every typed output and evidence reference.");
        }
        CandidatePopulationCountsContract actual = CandidateAnalysisCounts.Compute(value);
        if (actual != value.Counts)
        {
            throw new InvalidOperationException("Candidate population and output counts must exactly match the decision ledger.");
        }
        if (CandidateAnalysisIdentity.ComputePayloadId(value) != value.PayloadId)
        {
            throw new InvalidOperationException("Candidate analysis payload identity must cover the exact aggregate semantics.");
        }
    }

    public static void Validate(FindingCaseContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.FindingCaseSchemaId);
        RequireUnique(value.Findings.Select(item => item.FindingOccurrenceId), "finding occurrences");
        HashSet<OpaqueId> findings = value.Findings.Select(item => item.FindingOccurrenceId).ToHashSet();
        foreach (FindingContract finding in value.Findings)
        {
            if (finding.Confidence is AnalysisConfidence.Unspecified or AnalysisConfidence.SpeculativeLead
                || finding.Severity == FindingSeverity.Unspecified)
            {
                throw new InvalidOperationException("A finding requires plausible-or-better support and closed severity.");
            }
        }
        foreach (Slice5CaseContract @case in value.Cases)
        {
            bool supported = @case.Kind == CaseOccurrenceKind.Supported;
            if (@case.Kind == CaseOccurrenceKind.Unspecified
                || @case.CauseProofEvidenceIds.Count == 0
                || (supported && (@case.FindingOccurrenceIds.Count == 0 || !@case.FindingOccurrenceIds.All(findings.Contains)))
                || (!supported && @case.FindingOccurrenceIds.Count != 0)
                || (!supported && @case.AffectsReadiness))
            {
                throw new InvalidOperationException("Supported and lead-only cases require separate, causally proven memberships and readiness effects.");
            }
        }
        foreach (Slice5ReconciliationContract reconciliation in value.ReconciliationAssessments)
        {
            bool allEquivalent = reconciliation.Gates.Causal == ReconciliationGateState.ProvenEquivalent
                && reconciliation.Gates.Applicability == ReconciliationGateState.ProvenEquivalent
                && reconciliation.Gates.Dependency == ReconciliationGateState.ProvenEquivalent
                && reconciliation.Gates.Producer == ReconciliationGateState.ProvenEquivalent;
            if (reconciliation.Outcome is ReconciliationOutcome.Unspecified
                || (reconciliation.Outcome is ReconciliationOutcome.ExactContinuation or ReconciliationOutcome.AnalyticalRevision
                    && !allEquivalent))
            {
                throw new InvalidOperationException("Continuity requires all four independently proven reconciliation gates.");
            }
        }
    }

    public static void Validate(AnalysisReplayContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.AnalysisReplaySchemaId);
        RequireUnique(value.Dependencies.Select(item => item.DependencyId), "replay dependencies");
        HashSet<OpaqueId> dependencies = value.Dependencies.Select(item => item.DependencyId).ToHashSet();
        if (value.Edges.Any(edge => edge.From == edge.To || !dependencies.Contains(edge.From) || !dependencies.Contains(edge.To)))
        {
            throw new InvalidOperationException("Replay dependency edges must connect distinct admitted nodes.");
        }
        bool requiresComparedRun = value.Mode is ReplayMode.Incremental or ReplayMode.RetainedDownstreamReplay;
        if (value.Mode == ReplayMode.Unspecified
            || (requiresComparedRun && value.ComparedRunId is null)
            || (!requiresComparedRun && value.ComparedRunId is not null))
        {
            throw new InvalidOperationException("Replay manifests require a mode-consistent compared-run binding.");
        }
        if (value.ReplayState == ReplayState.CompleteClean
            && (value.MissingDependencyIds.Count != 0
                || value.CoverageGapIds.Count != 0
                || !value.SemanticallyEquivalent
                || value.AuditabilityState != AuditabilityState.Complete))
        {
            throw new InvalidOperationException("Complete-clean replay requires complete dependencies, audit, and semantic equivalence.");
        }
    }

    public static void Validate(AnalysisExecutionInputContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.AnalysisExecutionInputSchemaId);
        ExecutionBoundaryContractInvariants.ValidateProductCapabilities(value.Boundaries, requireNotUsed: false);
        RequireUnique(value.SourceInputs.Select(item => item.ArtifactId), "analysis execution source inputs");
        RequireUnique(value.AnalyzerDeclarations.Select(item => item.ArtifactId), "analysis execution analyzer declarations");
        ArtifactReferenceContract[] references =
        [
            value.InstallationSnapshot,
            value.BethesdaSemanticInput,
            .. value.SourceInputs,
            .. value.AnalyzerDeclarations,
            value.EffectiveConfiguration,
            value.ResolvedInputManifest,
        ];
        if (value.Mode == ReplayMode.Unspecified
            || value.Seed < 0
            || value.SourceInputs.Count > 128
            || value.AnalyzerDeclarations.Count > 128
            || references.Any(item => string.IsNullOrWhiteSpace(item.Availability)
                || item.Availability.Length > 128
                || item.Availability is not ("retained" or "externally-reacquirable" or "evaluator-private" or "unavailable"))
            || value.Limits.MaximumEntities is < 1 or > 1_000_000
            || value.Limits.MaximumEdges is < 1 or > 2_000_000
            || value.Limits.MaximumTruthRows is < 1 or > 100_000
            || value.Limits.MaximumOutputItems is < 1 or > 100_000
            || value.Limits.MaximumWallTimeMilliseconds is < 1 or > 120_000
            || (value.Mode is ReplayMode.Incremental or ReplayMode.RetainedDownstreamReplay) != (value.PriorRunId is not null))
        {
            throw new InvalidOperationException("Execution inputs require finite limits, closed boundaries, and mode-consistent prior-run binding.");
        }
    }

    private static void RequireHeader(string schemaId, ContractVersion schemaVersion, string expectedSchemaId)
    {
        if (!StringComparer.Ordinal.Equals(schemaId, expectedSchemaId) || schemaVersion.Major != 1)
        {
            throw new InvalidOperationException($"Payload must bind {expectedSchemaId} major v1.");
        }
    }

    private static bool IsAsciiToken(string value) => value.Length != 0
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or ':' or '/' or '-');

    private static CandidateDependencyEdgeContract CandidateEdge(
        string fromKind,
        OpaqueId fromId,
        string toKind,
        OpaqueId toId,
        string edgeKind) => new(
        CandidateAnalysisIdentity.StableId("candidate-edge", fromKind, fromId.Value, toKind, toId.Value, edgeKind),
        fromKind,
        fromId,
        toKind,
        toId,
        edgeKind);

    private static void RequireUnique(IEnumerable<OpaqueId> ids, string description)
    {
        OpaqueId[] materialized = ids.ToArray();
        if (materialized.Distinct().Count() != materialized.Length)
        {
            throw new InvalidOperationException($"{description} must use unique opaque IDs.");
        }
    }
}
