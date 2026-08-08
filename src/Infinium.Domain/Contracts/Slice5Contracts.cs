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
    Abstained,
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
    Sha256Fingerprint ByteFingerprint,
    long ByteLength,
    OpaqueId ImportRunId,
    OpaqueId? SupplyingSnapshotId,
    Slice5ResultState RetentionState,
    ReplayState ReplayState);

public sealed record DocumentationPassageContract(
    OpaqueId PassageId,
    OpaqueId RevisionId,
    long Utf8StartOffset,
    long Utf8EndOffset,
    Sha256Fingerprint PassageFingerprint,
    Slice5ResultState State);

public sealed record DocumentationClaimContract(
    OpaqueId ClaimId,
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
    OpaqueId DependencyClosureId,
    ClaimApplicabilityState Applicability,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record DocumentationEvidenceContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PayloadId,
    OpaqueId OriginatingRunId,
    IReadOnlyList<DocumentationRevisionContract> Revisions,
    IReadOnlyList<DocumentationPassageContract> Passages,
    IReadOnlyList<DocumentationClaimContract> Claims,
    IReadOnlyList<ClaimApplicationContract> Applications,
    IReadOnlyList<TaxonomyAssignmentContract> PurposeAssignments,
    IReadOnlyList<CoverageGapContract> Gaps,
    IReadOnlyList<FailureContract> Failures);

public sealed record CandidateParticipantContract(OpaqueId ParticipantId, string Role);

public sealed record CandidateDecisionContract(
    OpaqueId DecisionId,
    OpaqueId PopulationMemberId,
    CandidateLane Lane,
    CandidateDecisionDisposition Disposition,
    IReadOnlyList<CandidateParticipantContract> Participants,
    string JoinKind,
    IReadOnlyList<OpaqueId> Path,
    OpaqueId DependencyClosureId,
    string Rationale,
    IReadOnlyList<OpaqueId> EvidenceIds,
    bool AdmissionIndependentOfScore,
    long? OptionalRank);

public sealed record CandidateAnalysisEntryContract(
    OpaqueId CandidateId,
    OpaqueId DecisionId,
    Slice5ResultState State,
    string CausalExplanation,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation,
    AnalysisConfidence Confidence,
    OpaqueId ThresholdId);

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
    IReadOnlyList<AbstentionContract> Abstentions,
    IReadOnlyList<CoverageGapContract> Gaps,
    IReadOnlyList<FailureContract> Failures);

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
    public static void Validate(DocumentationEvidenceContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.DocumentationEvidenceSchemaId);
        RequireUnique(value.Revisions.Select(item => item.RevisionId), "documentation revisions");
        RequireUnique(value.Passages.Select(item => item.PassageId), "documentation passages");
        RequireUnique(value.Claims.Select(item => item.ClaimId), "documentation claims");
        RequireUnique(value.Applications.Select(item => item.ApplicationId), "claim applications");
        HashSet<OpaqueId> revisions = value.Revisions.Select(item => item.RevisionId).ToHashSet();
        HashSet<OpaqueId> passages = value.Passages.Select(item => item.PassageId).ToHashSet();
        HashSet<OpaqueId> claims = value.Claims.Select(item => item.ClaimId).ToHashSet();
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
                || claim.Kind == ClaimKind.Unspecified
                || claim.Authority == EvidenceAuthority.Unspecified
                || claim.Applicability == ClaimApplicabilityState.Unspecified
                || claim.ClassificationRole == ClassificationRole.Unspecified
                || (claim.Kind == ClaimKind.DeclaredPurpose && claim.ClassificationRole != ClassificationRole.Declared))
            {
                throw new InvalidOperationException("Claims require admitted passages and closed authority, applicability, kind, and role states.");
            }
        }
        foreach (ClaimApplicationContract application in value.Applications)
        {
            if (!claims.Contains(application.ClaimId)
                || application.Applicability == ClaimApplicabilityState.Unspecified)
            {
                throw new InvalidOperationException(
                    "Claim applications require an existing claim and a closed applicability state.");
            }
        }
    }

    public static void Validate(CandidateAnalysisContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.CandidateAnalysisSchemaId);
        if (value.PopulationDenominator < 0 || value.Decisions.Count != value.PopulationDenominator)
        {
            throw new InvalidOperationException("Every candidate population member requires exactly one eligible decision.");
        }
        RequireUnique(value.Decisions.Select(item => item.DecisionId), "candidate decisions");
        RequireUnique(value.Decisions.Select(item => item.PopulationMemberId), "candidate population members");
        RequireUnique(value.Candidates.Select(item => item.CandidateId), "candidates");
        RequireUnique(value.Candidates.Select(item => item.DecisionId), "candidate decision references");
        Dictionary<OpaqueId, CandidateDecisionContract> decisions = value.Decisions.ToDictionary(item => item.DecisionId);
        foreach (CandidateDecisionContract decision in value.Decisions)
        {
            if (decision.Lane == CandidateLane.Unspecified
                || decision.Disposition == CandidateDecisionDisposition.Unspecified
                || decision.Participants.Count < 2
                || decision.Participants.Select(item => item.Role).Distinct(StringComparer.Ordinal).Count() != decision.Participants.Count
                || (decision.Lane is CandidateLane.DeterministicRequired or CandidateLane.MandatoryEvidence
                    && !decision.AdmissionIndependentOfScore)
                || (decision.Lane != CandidateLane.OptionalRanked && decision.OptionalRank is not null))
            {
                throw new InvalidOperationException("Candidate decisions require closed lane/disposition, canonical roles, and score-independent mandatory admission.");
            }
        }
        HashSet<OpaqueId> admittedDecisionIds = value.Decisions
            .Where(item => item.Disposition == CandidateDecisionDisposition.CandidateAdmitted)
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
                || decision.Disposition != CandidateDecisionDisposition.CandidateAdmitted
                || candidate.State == Slice5ResultState.Unspecified
                || candidate.Confidence == AnalysisConfidence.Unspecified)
            {
                throw new InvalidOperationException("Candidates require one admitted decision and explicit state/confidence.");
            }
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
        if (value.Mode == ReplayMode.Unspecified
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

    private static void RequireUnique(IEnumerable<OpaqueId> ids, string description)
    {
        OpaqueId[] materialized = ids.ToArray();
        if (materialized.Distinct().Count() != materialized.Length)
        {
            throw new InvalidOperationException($"{description} must use unique opaque IDs.");
        }
    }
}
