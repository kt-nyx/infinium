using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Infinium.Domain.Contracts;

public enum ScopeValueState
{
    Unspecified,
    Present,
    Absent,
    Unresolved,
    Unsupported,
    Invalid,
}

public enum ScopeTransitionKind
{
    Unspecified,
    Unchanged,
    Changed,
    Created,
    Absent,
    Unresolved,
    Unsupported,
    Invalid,
}

public enum ScopeSupportState
{
    Unspecified,
    Supported,
    Unsupported,
    Contradicted,
    Unavailable,
    NotEvaluated,
}

public enum ScopeApplicabilityState
{
    Unspecified,
    Applicable,
    ConditionalUnestablished,
    NotApplicable,
    Unknown,
    NotEvaluated,
}

public enum ScopeCoverageRelation
{
    Unspecified,
    CoversTransition,
    DoesNotCoverTransition,
    Conflicts,
    Undecidable,
}

public enum ScopeContradictionState
{
    Unspecified,
    None,
    IntentionalChange,
    Defeating,
    Unknown,
}

public enum ScopeCausalClosureState
{
    Unspecified,
    Closed,
    Open,
}

public enum ScopePublicationEligibility
{
    Unspecified,
    Eligible,
    Ineligible,
}

public enum ScopeGapFailureState
{
    Unspecified,
    None,
    Gap,
    Failed,
    Limited,
}

public enum ScopeReversionDisposition
{
    Unspecified,
    SupportedFinding,
    ResolvedNegative,
    Abstained,
    Unsupported,
    InvalidInput,
    Failed,
    Limited,
    Unpublishable,
}

public enum ScopeCandidateState
{
    Unspecified,
    Present,
    ResolvedNegative,
    Ambiguous,
}

public enum ScopeHypothesisState
{
    Unspecified,
    Present,
    ResolvedRejected,
    Abstained,
}

public enum ScopeTaxonomyApplicability
{
    Unspecified,
    Applicable,
    NotApplicable,
    Unknown,
    Unsupported,
}

public sealed record ScopeContributionStateContract(
    ScopeValueState State,
    string? ComparableValue,
    OpaqueId ContributionId,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopePurposeContract(
    ScopeSupportState Support,
    ScopeApplicabilityState Applicability,
    bool WinningPurposeChangeObserved,
    IReadOnlyList<string> CoveredDimensions,
    IReadOnlyList<string> IntentionalTransitionDimensions,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeDomainInterpretationContract(
    string DomainId,
    string CoveragePopulationPrefix,
    string SurfaceCode,
    string Explanation,
    string AffectedLocus,
    string Symptom,
    string BoundedExtent,
    string Recommendation,
    string Validation,
    string PurposeTaxonomyCode,
    string ObservedTaxonomyCode,
    string ConsequenceTaxonomyCode,
    string ExtentTaxonomyCode);

public sealed record ScopeCausalClosureContract(
    ScopeCausalClosureState State,
    OpaqueId DependencyClosureId,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeReversionMemberContract(
    OpaqueId MemberId,
    string AdapterId,
    OpaqueId SubjectId,
    string FeatureDimension,
    ScopeContributionStateContract PriorEffectiveState,
    ScopeContributionStateContract WinningState,
    ScopePurposeContract Purpose,
    ScopeContradictionState Contradiction,
    ScopeCausalClosureContract CausalClosure,
    ScopePublicationEligibility PublicationEligibility,
    CoverageMemberState CoverageState,
    ScopeGapFailureState GapFailureState,
    string? Issue,
    ScopeDomainInterpretationContract DomainInterpretation);

public sealed record ScopeReversionAnalyzerBindingContract(
    string AnalyzerFamily,
    string AnalyzerId,
    ContractVersion AnalyzerVersion,
    ContractVersion SemanticContractVersion,
    ContractVersion IdentityContractVersion,
    ContractVersion RulesetVersion,
    Sha256Fingerprint DeclarationFingerprint,
    string CanonicalDeclarationJson,
    AnalyzerMaturity Maturity);

public sealed record ScopeReversionSourceBindingContract(
    OpaqueId ArtifactId,
    string SchemaId,
    ContractVersion SchemaVersion,
    Sha256Fingerprint Fingerprint,
    string Availability);

public sealed record ScopeReversionConfigurationContract(
    OpaqueId ConfigurationId,
    Sha256Fingerprint Fingerprint,
    IReadOnlyList<string> RegisteredAdapterIds,
    IReadOnlyList<string> EnabledAdapterIds,
    long MaximumMembers,
    long MaximumOutputItems,
    long MaximumWallTimeMilliseconds);

public sealed record ScopeReversionWorkAssignmentContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId AssignmentId,
    OpaqueId OriginatingRunId,
    ScopeReversionAnalyzerBindingContract Analyzer,
    ScopeReversionConfigurationContract Configuration,
    IReadOnlyList<ScopeReversionSourceBindingContract> Sources,
    IReadOnlyList<ScopeReversionMemberContract> Members,
    Sha256Fingerprint InputFingerprint,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries);

public sealed record ScopeReversionDecisionContract(
    OpaqueId DecisionId,
    OpaqueId MemberId,
    ScopeTransitionKind Transition,
    ScopeCoverageRelation PurposeCoverage,
    ScopeReversionDisposition Disposition,
    string Rationale,
    OpaqueId DependencyClosureId,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeReversionCandidateContract(
    OpaqueId CandidateId,
    OpaqueId DecisionId,
    OpaqueId MemberId,
    ScopeCandidateState State,
    string CausalExplanation,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation);

public sealed record ScopeReversionHypothesisContract(
    OpaqueId HypothesisId,
    OpaqueId CandidateId,
    ScopeHypothesisState State,
    string Explanation,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation);

public sealed record ScopeReversionContradictionContract(
    OpaqueId ContradictionId,
    OpaqueId CandidateId,
    ScopeContradictionState State,
    string Reason,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeReversionAbstentionContract(
    OpaqueId AbstentionId,
    OpaqueId CandidateId,
    OpaqueId HypothesisId,
    string Reason,
    IReadOnlyList<string> RequiredInformation,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeReversionGapContract(
    OpaqueId GapId,
    OpaqueId MemberId,
    string PopulationId,
    ScopeGapFailureState State,
    string Reason,
    string MissingCapabilityOrInformation);

public sealed record ScopeReversionFailureContract(
    OpaqueId FailureId,
    OpaqueId MemberId,
    string FailureCode,
    string Message,
    bool Retryable);

public sealed record ScopeReversionFindingContract(
    OpaqueId FindingId,
    OpaqueId CandidateId,
    OpaqueId HypothesisId,
    OpaqueId MemberId,
    string Conclusion,
    FindingSeverity Severity,
    AnalysisConfidence Confidence,
    string Symptom,
    string BoundedExtent,
    IReadOnlyList<OpaqueId> EvidenceIds,
    OpaqueId LogicalIdentityId);

public sealed record ScopeReversionCaseContract(
    OpaqueId CaseId,
    OpaqueId LogicalCaseId,
    OpaqueId FindingId,
    OpaqueId CandidateId,
    OpaqueId HypothesisId,
    OpaqueId DependencyClosureId,
    string SharedCause,
    bool AffectsReadiness,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeReversionRecommendationContract(
    OpaqueId RecommendationId,
    OpaqueId FindingId,
    string Action,
    string Reversibility,
    string Validation,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeReversionTaxonomyFactContract(
    OpaqueId TaxonomyFactId,
    OpaqueId MemberId,
    string Axis,
    string? Code,
    ScopeTaxonomyApplicability Applicability,
    string Role,
    string Reason,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeReversionCoverageContract(
    string PopulationId,
    string DenominatorLabel,
    long Denominator,
    long Completed,
    long CompletedWithGaps,
    long Failed,
    long SkippedByConfiguration,
    long SkippedByLimit,
    long Unsupported,
    IReadOnlyList<OpaqueId> MemberIds);

public sealed record ScopeReversionDependencyEdgeContract(
    OpaqueId EdgeId,
    string FromKind,
    OpaqueId FromId,
    string ToKind,
    OpaqueId ToId,
    string EdgeKind);

public sealed record ScopeReversionCountsContract(
    long Population,
    long Decisions,
    long Candidates,
    long Hypotheses,
    long Contradictions,
    long Abstentions,
    long Gaps,
    long Failures,
    long SupportedFindings,
    long ResolvedNegative,
    long Unsupported,
    long InvalidInput,
    long Limited,
    long Unpublishable,
    long Findings,
    long Cases,
    long Recommendations);

public sealed record ScopeReversionAnalysisContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PayloadId,
    OpaqueId OriginatingRunId,
    OpaqueId AssignmentId,
    Sha256Fingerprint InputFingerprint,
    ScopeReversionAnalyzerBindingContract Analyzer,
    IReadOnlyList<ScopeReversionDecisionContract> Decisions,
    IReadOnlyList<ScopeReversionCandidateContract> Candidates,
    IReadOnlyList<ScopeReversionHypothesisContract> Hypotheses,
    IReadOnlyList<ScopeReversionContradictionContract> Contradictions,
    IReadOnlyList<ScopeReversionAbstentionContract> Abstentions,
    IReadOnlyList<ScopeReversionGapContract> Gaps,
    IReadOnlyList<ScopeReversionFailureContract> Failures,
    IReadOnlyList<ScopeReversionFindingContract> Findings,
    IReadOnlyList<ScopeReversionCaseContract> Cases,
    IReadOnlyList<ScopeReversionRecommendationContract> Recommendations,
    IReadOnlyList<ScopeReversionTaxonomyFactContract> Taxonomy,
    IReadOnlyList<ScopeReversionCoverageContract> Coverage,
    IReadOnlyList<ScopeReversionDependencyEdgeContract> DependencyEdges,
    ScopeReversionCountsContract Counts,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries,
    string PublicationClaimBoundary);

public static class ScopeReversionContractInvariants
{
    private static readonly string[] ExactAdapterIds =
    [
        "infinium.scope-reversion.adapter.actor-ai-facegen",
        "infinium.scope-reversion.adapter.refr-link-placement",
    ];

    private static readonly string[] ExactBoundaryIds =
    [
        "hosted-search", "loot", "nexus", "provider",
    ];

    private static readonly string[] ExactCoveragePopulationIds =
    [
        "actor-conclusion-taxonomy", "actor-purpose-applicability", "actor-transition",
        "publication-replay",
        "reference-conclusion-taxonomy", "reference-purpose-applicability", "reference-transition",
    ];

    public const string ExactClaimBoundary =
        "For the exact members and coverage populations reported by this run, the category-neutral deterministic local analyzer distinguishes only closed supported scope-incongruent reversions, preserves resolved intentional or harmless changes as negatives, abstains on ambiguity, and makes no broader safety, compatibility, completeness, or production-readiness claim.";

    public static void Validate(ScopeReversionWorkAssignmentContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaId != ContractConstants.ScopeReversionSchemaId
            || value.SchemaVersion != new ContractVersion(1, 0, 0)
            || value.AssignmentId != ComputeAssignmentId(value.OriginatingRunId, value.InputFingerprint)
            || value.Configuration.MaximumMembers is < 1 or > 1_000_000
            || value.Configuration.MaximumOutputItems is < 1 or > 4_000_000
            || value.Configuration.MaximumWallTimeMilliseconds is < 1 or > 3_600_000
            || value.Members.Count > value.Configuration.MaximumMembers
            || value.Sources.Count is < 1 or > 1024
            || value.Boundaries.Count is < 1 or > 32
            || value.Boundaries.Any(item => item.State != BoundaryUseState.NotUsed)
            || !value.Boundaries.Select(item => item.BoundaryId).Order(StringComparer.Ordinal)
                .SequenceEqual(ExactBoundaryIds, StringComparer.Ordinal)
            || !value.Configuration.RegisteredAdapterIds.SequenceEqual(ExactAdapterIds, StringComparer.Ordinal)
            || value.Sources.Any(item => item.Availability != "retained"
                || string.IsNullOrWhiteSpace(item.SchemaId))
            || value.Members.Any(item => !ExactAdapterIds.Contains(item.AdapterId, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Scope-reversion assignment identity, bounds, or local-only boundary is invalid.");
        }

        RequireUnique(value.Sources.Select(item => item.ArtifactId), "source");
        RequireUnique(value.Members.Select(item => item.MemberId), "member");
        RequireSorted(value.Sources.Select(item => item.ArtifactId.Value), "sources");
        RequireSorted(value.Members.Select(item => item.MemberId.Value), "members");
        RequireSorted(value.Configuration.EnabledAdapterIds, "enabled adapters");
        RequireSorted(value.Configuration.RegisteredAdapterIds, "registered adapters");
        if (!HasCanonicalAnalyzerBinding(value.Analyzer)
            || value.Configuration.Fingerprint != ComputeConfigurationFingerprint(value.Configuration)
            || value.InputFingerprint != ComputeInputFingerprint(
                value.OriginatingRunId,
                value.Configuration,
                value.Sources,
                value.Members,
                value.Analyzer.DeclarationFingerprint)
            || value.Configuration.EnabledAdapterIds.Any(item =>
                !value.Configuration.RegisteredAdapterIds.Contains(item, StringComparer.Ordinal))
            || value.Members.Any(item => !value.Configuration.RegisteredAdapterIds.Contains(item.AdapterId, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("A scope-reversion adapter is not admitted by the exact configuration.");
        }

        foreach (ScopeReversionMemberContract member in value.Members)
        {
            Validate(member);
        }
    }

    public static void Validate(ScopeReversionAnalysisContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaId != ContractConstants.ScopeReversionSchemaId
            || value.SchemaVersion != new ContractVersion(1, 0, 0)
            || value.AssignmentId != ComputeAssignmentId(value.OriginatingRunId, value.InputFingerprint)
            || value.PayloadId != ComputePayloadId(value)
            || value.PublicationClaimBoundary != ExactClaimBoundary
            || value.Boundaries.Count is < 1 or > 32
            || value.Boundaries.Any(item => item.State != BoundaryUseState.NotUsed)
            || !value.Boundaries.Select(item => item.BoundaryId).Order(StringComparer.Ordinal)
                .SequenceEqual(ExactBoundaryIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Scope-reversion analysis identity, claim boundary, or external boundary is invalid.");
        }

        RequireUnique(value.Decisions.Select(item => item.DecisionId), "decision");
        RequireUnique(value.Candidates.Select(item => item.CandidateId), "candidate");
        RequireUnique(value.Hypotheses.Select(item => item.HypothesisId), "hypothesis");
        RequireUnique(value.Contradictions.Select(item => item.ContradictionId), "contradiction");
        RequireUnique(value.Abstentions.Select(item => item.AbstentionId), "abstention");
        RequireUnique(value.Gaps.Select(item => item.GapId), "gap");
        RequireUnique(value.Failures.Select(item => item.FailureId), "failure");
        RequireUnique(value.Findings.Select(item => item.FindingId), "finding");
        RequireUnique(value.Cases.Select(item => item.CaseId), "case");
        RequireUnique(value.Recommendations.Select(item => item.RecommendationId), "recommendation");
        RequireUnique(value.Taxonomy.Select(item => item.TaxonomyFactId), "taxonomy fact");
        RequireUnique(value.DependencyEdges.Select(item => item.EdgeId), "dependency edge");
        RequireSorted(value.Decisions.Select(item => item.MemberId.Value), "decisions");
        RequireSorted(value.Candidates.Select(item => item.MemberId.Value), "candidates");
        RequireSorted(value.Taxonomy.Select(item => item.TaxonomyFactId.Value), "taxonomy facts");
        RequireSorted(value.Coverage.Select(item => item.PopulationId), "coverage");
        RequireSorted(value.DependencyEdges.Select(item => item.EdgeId.Value), "dependency edges");

        HashSet<OpaqueId> decisionIds = value.Decisions.Select(item => item.DecisionId).ToHashSet();
        HashSet<OpaqueId> candidateIds = value.Candidates.Select(item => item.CandidateId).ToHashSet();
        HashSet<OpaqueId> hypothesisIds = value.Hypotheses.Select(item => item.HypothesisId).ToHashSet();
        HashSet<OpaqueId> findingIds = value.Findings.Select(item => item.FindingId).ToHashSet();
        Dictionary<OpaqueId, ScopeReversionDecisionContract> decisionById = value.Decisions.ToDictionary(item => item.DecisionId);
        Dictionary<OpaqueId, ScopeReversionCandidateContract> candidateById = value.Candidates.ToDictionary(item => item.CandidateId);
        Dictionary<OpaqueId, ScopeReversionHypothesisContract> hypothesisById = value.Hypotheses.ToDictionary(item => item.HypothesisId);
        Dictionary<OpaqueId, ScopeReversionFindingContract> findingById = value.Findings.ToDictionary(item => item.FindingId);
        if (!HasCanonicalAnalyzerBinding(value.Analyzer)
            || value.Candidates.Any(item => !decisionById.TryGetValue(item.DecisionId, out ScopeReversionDecisionContract? decision)
                || decision.MemberId != item.MemberId)
            || value.Hypotheses.Any(item => !candidateById.ContainsKey(item.CandidateId))
            || value.Contradictions.Any(item => !candidateIds.Contains(item.CandidateId))
            || value.Abstentions.Any(item => !candidateById.ContainsKey(item.CandidateId)
                || !hypothesisById.TryGetValue(item.HypothesisId, out ScopeReversionHypothesisContract? hypothesis)
                || hypothesis.CandidateId != item.CandidateId)
            || value.Findings.Any(item => !candidateById.TryGetValue(item.CandidateId, out ScopeReversionCandidateContract? candidate)
                || candidate.MemberId != item.MemberId
                || !hypothesisById.TryGetValue(item.HypothesisId, out ScopeReversionHypothesisContract? hypothesis)
                || hypothesis.CandidateId != item.CandidateId)
            || value.Cases.Any(item => !findingById.TryGetValue(item.FindingId, out ScopeReversionFindingContract? finding)
                || finding.CandidateId != item.CandidateId
                || finding.HypothesisId != item.HypothesisId)
            || value.Recommendations.Any(item => !findingIds.Contains(item.FindingId)))
        {
            throw new InvalidDataException("Scope-reversion output contains a dangling typed reference.");
        }

        long positives = value.Decisions.Count(item => item.Disposition == ScopeReversionDisposition.SupportedFinding);
        long negatives = value.Decisions.Count(item => item.Disposition == ScopeReversionDisposition.ResolvedNegative);
        long abstained = value.Decisions.Count(item => item.Disposition == ScopeReversionDisposition.Abstained);
        HashSet<OpaqueId> positiveCandidates = value.Candidates.Where(item =>
                decisionById[item.DecisionId].Disposition == ScopeReversionDisposition.SupportedFinding)
            .Select(item => item.CandidateId).ToHashSet();
        HashSet<OpaqueId> negativeCandidates = value.Candidates.Where(item =>
                decisionById[item.DecisionId].Disposition == ScopeReversionDisposition.ResolvedNegative)
            .Select(item => item.CandidateId).ToHashSet();
        HashSet<OpaqueId> abstainedCandidates = value.Candidates.Where(item =>
                decisionById[item.DecisionId].Disposition == ScopeReversionDisposition.Abstained)
            .Select(item => item.CandidateId).ToHashSet();
        if (value.Findings.Count != positives
            || value.Cases.Count != positives
            || value.Recommendations.Count != positives
            || value.Findings.Any(item => item.Severity != FindingSeverity.Moderate
                || item.Confidence != AnalysisConfidence.StronglySupported)
            || value.Cases.Any(item => !item.AffectsReadiness)
            || value.Candidates.Count(item => item.State == ScopeCandidateState.ResolvedNegative) != negatives
            || value.Candidates.Count(item => item.State == ScopeCandidateState.Ambiguous) != abstained
            || value.Candidates.Any(item => item.State != (decisionById[item.DecisionId].Disposition switch
            {
                ScopeReversionDisposition.SupportedFinding => ScopeCandidateState.Present,
                ScopeReversionDisposition.ResolvedNegative => ScopeCandidateState.ResolvedNegative,
                ScopeReversionDisposition.Abstained => ScopeCandidateState.Ambiguous,
                _ => ScopeCandidateState.Unspecified,
            }))
            || value.Hypotheses.Any(item => item.State != (decisionById[candidateById[item.CandidateId].DecisionId].Disposition switch
            {
                ScopeReversionDisposition.SupportedFinding => ScopeHypothesisState.Present,
                ScopeReversionDisposition.ResolvedNegative => ScopeHypothesisState.ResolvedRejected,
                ScopeReversionDisposition.Abstained => ScopeHypothesisState.Abstained,
                _ => ScopeHypothesisState.Unspecified,
            }))
            || value.Contradictions.Count != negatives
            || !value.Contradictions.Select(item => item.CandidateId).ToHashSet().SetEquals(negativeCandidates)
            || value.Abstentions.Count != abstained
            || !value.Abstentions.Select(item => item.CandidateId).ToHashSet().SetEquals(abstainedCandidates)
            || !value.Findings.Select(item => item.CandidateId).ToHashSet().SetEquals(positiveCandidates)
            || value.Cases.Select(item => item.FindingId).Distinct().Count() != value.Findings.Count
            || value.Recommendations.Select(item => item.FindingId).Distinct().Count() != value.Findings.Count
            || value.Taxonomy.Count != (positives + negatives + abstained) * 4
            || value.Taxonomy.GroupBy(item => item.MemberId).Any(group =>
                group.Count() != 4 || group.Select(item => item.Axis).Distinct(StringComparer.Ordinal).Count() != 4)
            || value.Taxonomy.Any(item => !value.Decisions.Any(decision => decision.MemberId == item.MemberId
                && decision.Disposition is ScopeReversionDisposition.SupportedFinding
                    or ScopeReversionDisposition.ResolvedNegative or ScopeReversionDisposition.Abstained))
            || value.Gaps.Any(item => !value.Decisions.Any(decision => decision.MemberId == item.MemberId))
            || value.Failures.Any(item => !value.Decisions.Any(decision => decision.MemberId == item.MemberId))
            || value.DependencyEdges.Any(item => item.FromKind != "decision"
                || !decisionIds.Contains(item.FromId)
                || item.ToKind is not ("dependency" or "evidence"))
            || value.Decisions.Any(decision =>
                !value.DependencyEdges.Where(edge => edge.FromId == decision.DecisionId && edge.ToKind == "evidence")
                    .Select(edge => edge.ToId).ToHashSet().SetEquals(decision.EvidenceIds)
                || !value.DependencyEdges.Any(edge => edge.FromId == decision.DecisionId && edge.ToKind == "dependency"))
            || value.Cases.Any(item =>
                candidateById[item.CandidateId].DecisionId is OpaqueId decisionId
                && (decisionById[decisionId].DependencyClosureId != item.DependencyClosureId
                    || item.LogicalCaseId != StableId("scope-logical-case", item.DependencyClosureId.Value)))
            || value.Taxonomy.GroupBy(item => item.MemberId).Any(group =>
                !group.Select(item => item.Axis).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(["purpose", "observed-change", "consequence", "extent"])
                || group.Any(item => (item.Code is null) !=
                    (item.Applicability == ScopeTaxonomyApplicability.NotApplicable)))
            || !ValidateCoverage(value)
            || value.Decisions.Where(item => item.Disposition != ScopeReversionDisposition.SupportedFinding)
                .Any(item => value.Findings.Any(finding => finding.MemberId == item.MemberId)))
        {
            throw new InvalidDataException("Scope-reversion promotion, negative retention, or abstention semantics are inconsistent.");
        }

        ScopeReversionCountsContract expected = new(
            value.Decisions.Count,
            value.Decisions.Count,
            value.Candidates.Count,
            value.Hypotheses.Count,
            value.Contradictions.Count,
            value.Abstentions.Count,
            value.Gaps.Count,
            value.Failures.Count,
            positives,
            negatives,
            value.Decisions.Count(item => item.Disposition == ScopeReversionDisposition.Unsupported),
            value.Decisions.Count(item => item.Disposition == ScopeReversionDisposition.InvalidInput),
            value.Decisions.Count(item => item.Disposition == ScopeReversionDisposition.Limited),
            value.Decisions.Count(item => item.Disposition == ScopeReversionDisposition.Unpublishable),
            value.Findings.Count,
            value.Cases.Count,
            value.Recommendations.Count);
        if (value.Counts != expected)
        {
            throw new InvalidDataException("Scope-reversion counts do not equal the typed collections.");
        }
    }

    private static void Validate(ScopeReversionMemberContract member)
    {
        if (member.AdapterId.Length is < 1 or > 128
            || member.FeatureDimension.Length is < 1 or > 128
            || member.CoverageState == CoverageMemberState.Unspecified
            || member.PublicationEligibility == ScopePublicationEligibility.Unspecified
            || member.Contradiction == ScopeContradictionState.Unspecified
            || member.CausalClosure.State == ScopeCausalClosureState.Unspecified
            || member.GapFailureState == ScopeGapFailureState.Unspecified
            || member.PriorEffectiveState.State == ScopeValueState.Unspecified
            || member.WinningState.State == ScopeValueState.Unspecified
            || member.Purpose.Support == ScopeSupportState.Unspecified
            || member.Purpose.Applicability == ScopeApplicabilityState.Unspecified)
        {
            throw new InvalidDataException("Scope-reversion member contains an unspecified or unbounded state.");
        }
        if (member.CausalClosure.DependencyIds.Count == 0
            || member.CausalClosure.EvidenceIds.Count == 0
            || member.PriorEffectiveState.EvidenceIds.Count == 0
            || member.WinningState.EvidenceIds.Count == 0
            || member.Purpose.EvidenceIds.Count == 0)
        {
            throw new InvalidDataException("Scope-reversion members require retained dependency and evidence provenance.");
        }
        if ((member.PriorEffectiveState.State == ScopeValueState.Present) != (member.PriorEffectiveState.ComparableValue is not null)
            || (member.WinningState.State == ScopeValueState.Present) != (member.WinningState.ComparableValue is not null)
            || (member.GapFailureState == ScopeGapFailureState.None) != (member.Issue is null))
        {
            throw new InvalidDataException("Scope-reversion value or gap/failure state is internally inconsistent.");
        }
        RequireUnique(member.CausalClosure.DependencyIds, "dependency");
        RequireUnique(member.CausalClosure.EvidenceIds, "causal evidence");
        RequireUnique(member.PriorEffectiveState.EvidenceIds, "prior evidence");
        RequireUnique(member.WinningState.EvidenceIds, "winning evidence");
        RequireUnique(member.Purpose.EvidenceIds, "purpose evidence");
        RequireSorted(member.CausalClosure.DependencyIds.Select(item => item.Value), "dependencies");
        RequireSorted(member.CausalClosure.EvidenceIds.Select(item => item.Value), "causal evidence");
        RequireSorted(member.PriorEffectiveState.EvidenceIds.Select(item => item.Value), "prior evidence");
        RequireSorted(member.WinningState.EvidenceIds.Select(item => item.Value), "winning evidence");
        RequireSorted(member.Purpose.EvidenceIds.Select(item => item.Value), "purpose evidence");
        RequireSorted(member.Purpose.CoveredDimensions, "purpose dimensions");
        RequireSorted(member.Purpose.IntentionalTransitionDimensions, "intentional dimensions");
    }

    private static void RequireUnique(IEnumerable<OpaqueId> values, string label)
    {
        string[] ids = values.Select(item => item.Value).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            throw new InvalidDataException($"Scope-reversion {label} identities must be unique.");
        }
    }

    private static bool ValidateCoverage(ScopeReversionAnalysisContract value)
    {
        string[] populationIds = value.Coverage.Select(item => item.PopulationId).ToArray();
        if (!populationIds.SequenceEqual(populationIds.Order(StringComparer.Ordinal), StringComparer.Ordinal)
            || !populationIds.Contains("publication-replay", StringComparer.Ordinal)
            || populationIds.Any(item => !ExactCoveragePopulationIds.Contains(item, StringComparer.Ordinal)))
        {
            return false;
        }
        ScopeReversionCoverageContract publication = value.Coverage.Single(item =>
            item.PopulationId == "publication-replay");
        HashSet<OpaqueId> publicationMembers = publication.MemberIds.ToHashSet();
        if (!value.Decisions.Select(item => item.MemberId).ToHashSet().IsSubsetOf(publicationMembers)
            || value.Coverage.Any(item => !item.MemberIds.ToHashSet().IsSubsetOf(publicationMembers)))
        {
            return false;
        }
        foreach (string prefix in new[] { "actor", "reference" })
        {
            ScopeReversionCoverageContract[] domain = value.Coverage.Where(item =>
                item.PopulationId.StartsWith(prefix + "-", StringComparison.Ordinal)).ToArray();
            if (domain.Length is not 0 and not 3
                || domain.Length == 3 && domain.Skip(1).Any(item => !item.MemberIds.SequenceEqual(domain[0].MemberIds)))
            {
                return false;
            }
        }
        foreach (ScopeReversionCoverageContract coverage in value.Coverage)
        {
            if (coverage.Denominator != coverage.MemberIds.Count
                || coverage.Denominator != coverage.Completed + coverage.CompletedWithGaps + coverage.Failed
                    + coverage.SkippedByConfiguration + coverage.SkippedByLimit + coverage.Unsupported
                || coverage.MemberIds.Distinct().Count() != coverage.MemberIds.Count
                || !coverage.MemberIds.Select(item => item.Value)
                    .SequenceEqual(coverage.MemberIds.Select(item => item.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    public static Sha256Fingerprint ComputeConfigurationFingerprint(ScopeReversionConfigurationContract configuration) =>
        ContractJsonSerializer.Fingerprint(new
        {
            configuration.ConfigurationId,
            configuration.RegisteredAdapterIds,
            configuration.EnabledAdapterIds,
            configuration.MaximumMembers,
            configuration.MaximumOutputItems,
            configuration.MaximumWallTimeMilliseconds,
        });

    public static Sha256Fingerprint ComputeInputFingerprint(
        OpaqueId originatingRunId,
        ScopeReversionConfigurationContract configuration,
        IReadOnlyList<ScopeReversionSourceBindingContract> sources,
        IReadOnlyList<ScopeReversionMemberContract> members,
        Sha256Fingerprint declarationFingerprint) => ContractJsonSerializer.Fingerprint(new
        {
            OriginatingRunId = originatingRunId,
            Configuration = configuration,
            Sources = sources,
            Members = members,
            DeclarationFingerprint = declarationFingerprint,
        });

    public static OpaqueId ComputeAssignmentId(OpaqueId originatingRunId, Sha256Fingerprint inputFingerprint) =>
        StableId("scope-assignment", originatingRunId.Value, inputFingerprint.Value);

    public static OpaqueId ComputePayloadId(ScopeReversionAnalysisContract value) => StableId(
        "scope-reversion",
        ContractJsonSerializer.Fingerprint(new
        {
            value.SchemaId,
            value.SchemaVersion,
            value.OriginatingRunId,
            value.AssignmentId,
            value.InputFingerprint,
            value.Analyzer,
            value.Decisions,
            value.Candidates,
            value.Hypotheses,
            value.Contradictions,
            value.Abstentions,
            value.Gaps,
            value.Failures,
            value.Findings,
            value.Cases,
            value.Recommendations,
            value.Taxonomy,
            value.Coverage,
            value.DependencyEdges,
            value.Counts,
            value.Boundaries,
            value.PublicationClaimBoundary,
        }).Value);

    private static bool HasCanonicalAnalyzerBinding(ScopeReversionAnalyzerBindingContract analyzer)
    {
        if (analyzer.AnalyzerFamily != "infinium.scope-reversion"
            || analyzer.AnalyzerId != "infinium.scope-reversion.local"
            || analyzer.AnalyzerVersion != new ContractVersion(1, 0, 0)
            || analyzer.SemanticContractVersion != new ContractVersion(1, 0, 0)
            || analyzer.IdentityContractVersion != new ContractVersion(1, 0, 0)
            || analyzer.RulesetVersion != new ContractVersion(1, 0, 0)
            || analyzer.Maturity != AnalyzerMaturity.Experimental
            || analyzer.CanonicalDeclarationJson.Length is < 2 or > 65_536)
        {
            return false;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(analyzer.CanonicalDeclarationJson);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != analyzer.DeclarationFingerprint.Value)
        {
            return false;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64,
                });
            JsonElement root = document.RootElement;
            JsonElement operation = root.GetProperty("operation_requirements");
            string[] boundaryIds = root.GetProperty("not_used_boundaries").EnumerateArray()
                .Where(item => item.GetProperty("state").GetString() == "not-used")
                .Select(item => item.GetProperty("boundary_id").GetString() ?? string.Empty)
                .Order(StringComparer.Ordinal)
                .ToArray();
            bool declaresScopePayload = root.GetProperty("payload_contracts").EnumerateArray().Any(item =>
                item.GetProperty("schema_id").GetString() == ContractConstants.ScopeReversionSchemaId
                && item.GetProperty("schema_version").GetString() == "1.0.0"
                && item.GetProperty("required").GetBoolean());
            return root.GetProperty("schema_id").GetString() == ContractConstants.AnalyzerDeclarationSchemaId
                && root.GetProperty("schema_version").GetString() == "1"
                && root.GetProperty("analyzer_family").GetString() == analyzer.AnalyzerFamily
                && root.GetProperty("analyzer_id").GetString() == analyzer.AnalyzerId
                && root.GetProperty("analyzer_version").GetString() == analyzer.AnalyzerVersion.ToString()
                && root.GetProperty("semantic_contract_version").GetString() == analyzer.SemanticContractVersion.ToString()
                && root.GetProperty("identity_contract_version").GetString() == analyzer.IdentityContractVersion.ToString()
                && root.GetProperty("ruleset_version").GetString() == analyzer.RulesetVersion.ToString()
                && root.GetProperty("maturity").GetString() == "Experimental"
                && root.GetProperty("state_model_version").GetString() == "1.0.0"
                && operation.GetProperty("mode").GetString() == "local-only"
                && !operation.GetProperty("network_required").GetBoolean()
                && !operation.GetProperty("llm_required").GetBoolean()
                && !operation.GetProperty("provider_required").GetBoolean()
                && boundaryIds.SequenceEqual(ExactBoundaryIds, StringComparer.Ordinal)
                && declaresScopePayload;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return false;
        }
    }

    private static OpaqueId StableId(string prefix, params string[] parts)
    {
        string canonical = string.Join("\u001f", parts.Prepend(prefix));
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new OpaqueId(prefix + "-" + hash[..32]);
    }

    private static void RequireSorted(IEnumerable<string> values, string label)
    {
        string[] ids = values.ToArray();
        if (!ids.SequenceEqual(ids.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Scope-reversion {label} must use canonical ordinal order.");
        }
    }
}
