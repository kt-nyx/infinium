using System.Security.Cryptography;
using System.Text;

namespace Infinium.Domain.Contracts;

public enum ScopeReversionV2SubjectKind
{
    Unspecified,
    ActorCohort,
    PlacedReference,
}

public enum ScopeReversionV2PartitionRole
{
    Unspecified,
    ControlledRealValidation,
    ControlledRealDevelopment,
}

public sealed record ScopeReversionV2PublicManifestReferenceContract(
    string RepositoryPath,
    long ByteLength,
    Sha256Fingerprint Sha256);

public sealed record ScopeReversionV2ControlledInputReferenceContract(
    string RelativePath,
    string Role,
    long ByteLength,
    Sha256Fingerprint Sha256);

public sealed record ScopeReversionV2SourceDecisionContract(
    OpaqueId DecisionId,
    OpaqueId OriginatingRunId,
    string SourceRegistryId,
    string SourceRevision,
    string PassageId,
    Sha256Fingerprint PassageFingerprint,
    string PublicManifestPath,
    UtcTimestamp RetrievalTime,
    SemanticProposalState ProposalState,
    SemanticSupportState SupportState,
    SemanticApplicabilityState ApplicabilityState,
    SemanticDecisionState DecisionState,
    Sha256Fingerprint LocalFactFingerprint,
    IReadOnlyList<OpaqueId> SubjectIds,
    IReadOnlyList<string> Fields,
    IReadOnlyList<OpaqueId> EvidenceIds,
    string Reason);

public sealed record ScopeReversionV2TaxonomyReferenceContract(
    OpaqueId AssignmentId,
    OpaqueId OriginatingRunId,
    string TaxonomyId,
    ContractVersion TaxonomyVersion,
    OpaqueId SubjectId,
    string Axis,
    string Facet,
    string? Code,
    TaxonomyApplicability Applicability,
    ClassificationRole Role,
    IReadOnlyList<OpaqueId> EvidenceIds,
    string Reason);

public sealed record ScopeReversionV2MemberContract(
    OpaqueId MemberId,
    OpaqueId SubjectId,
    string AdapterId,
    string FeatureDimension,
    ScopeContributionStateContract PriorEffectiveState,
    ScopeContributionStateContract WinningState,
    bool WinningPurposeChangeObserved,
    IReadOnlyList<OpaqueId> SourceDecisionIds,
    OpaqueId DependencyCauseId,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<OpaqueId> EvidenceIds,
    IReadOnlyList<string> ResidualFacts,
    IReadOnlyList<string> Gaps);

public sealed record ScopeReversionV2SubjectContract(
    OpaqueId SubjectId,
    ScopeReversionV2SubjectKind Kind,
    IReadOnlyList<OpaqueId> OrderedMemberIds,
    OpaqueId SharedDependencyCauseId,
    string AffectedLocus,
    string PredictedSymptom,
    string Recommendation,
    string Validation,
    IReadOnlyList<string> ClaimGaps);

public sealed record ScopeReversionV2WorkAssignmentContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId AssignmentId,
    OpaqueId OriginatingRunId,
    OpaqueId SnapshotId,
    OpaqueId ContextId,
    OpaqueId ConfigurationId,
    OpaqueId ExecutionInputId,
    ScopeReversionAnalyzerBindingContract Analyzer,
    string InputHandoffId,
    Sha256Fingerprint InputManifestFingerprint,
    IReadOnlyList<ScopeReversionV2PublicManifestReferenceContract> PublicManifests,
    IReadOnlyList<ScopeReversionV2ControlledInputReferenceContract> ControlledInputs,
    ScopeReversionV2PartitionRole PartitionRole,
    IReadOnlyList<ScopeReversionV2SubjectContract> Subjects,
    IReadOnlyList<ScopeReversionV2MemberContract> Members,
    IReadOnlyList<ScopeReversionV2SourceDecisionContract> SourceDecisions,
    IReadOnlyList<ScopeReversionV2TaxonomyReferenceContract> Taxonomy,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries);

public sealed record ScopeReversionV2DecisionContract(
    OpaqueId DecisionId,
    OpaqueId SubjectId,
    ScopeTransitionKind Transition,
    ScopeCoverageRelation PurposeCoverage,
    ScopeReversionDisposition Disposition,
    string Rationale,
    IReadOnlyList<OpaqueId> MemberIds,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeReversionV2FindingContract(
    OpaqueId FindingId,
    OpaqueId CandidateId,
    OpaqueId SubjectId,
    FindingSeverity Severity,
    AnalysisConfidence Confidence,
    string Conclusion,
    string PredictedSymptom,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeReversionV2CaseContract(
    OpaqueId CaseId,
    OpaqueId LogicalCaseId,
    OpaqueId CandidateId,
    OpaqueId? FindingId,
    OpaqueId SubjectId,
    OpaqueId SharedDependencyCauseId,
    string State,
    IReadOnlyList<OpaqueId> MemberIds,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record ScopeReversionV2CoverageContract(
    string PopulationId,
    long Denominator,
    long Completed,
    long CompletedWithGaps,
    long Unsupported,
    long Failed,
    IReadOnlyList<OpaqueId> MemberIds);

public sealed record ScopeReversionV2PartitionTransitionContract(
    OpaqueId TransitionId,
    string CaseId,
    Sha256Fingerprint InputManifestFingerprint,
    string CandidateIdentity,
    ScopeReversionV2PartitionRole FromRole,
    ScopeReversionV2PartitionRole ToRole,
    string Reason,
    UtcTimestamp RecordedAt);

public sealed record ScopeReversionV2AnalysisContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PayloadId,
    OpaqueId OriginatingRunId,
    OpaqueId AssignmentId,
    OpaqueId SnapshotId,
    OpaqueId ContextId,
    OpaqueId ConfigurationId,
    OpaqueId ExecutionInputId,
    string InputHandoffId,
    Sha256Fingerprint InputManifestFingerprint,
    IReadOnlyList<ScopeReversionV2PublicManifestReferenceContract> PublicManifests,
    IReadOnlyList<ScopeReversionV2ControlledInputReferenceContract> ControlledInputs,
    ScopeReversionAnalyzerBindingContract Analyzer,
    ScopeReversionV2PartitionRole PartitionRole,
    IReadOnlyList<ScopeReversionV2SubjectContract> Subjects,
    IReadOnlyList<ScopeReversionV2MemberContract> Members,
    IReadOnlyList<ScopeReversionV2SourceDecisionContract> SourceDecisions,
    IReadOnlyList<ScopeReversionV2TaxonomyReferenceContract> Taxonomy,
    IReadOnlyList<ScopeReversionV2DecisionContract> Decisions,
    IReadOnlyList<ScopeReversionCandidateContract> Candidates,
    IReadOnlyList<ScopeReversionHypothesisContract> Hypotheses,
    IReadOnlyList<ScopeReversionV2FindingContract> Findings,
    IReadOnlyList<ScopeReversionV2CaseContract> Cases,
    IReadOnlyList<ScopeReversionRecommendationContract> Recommendations,
    IReadOnlyList<ScopeReversionGapContract> Gaps,
    IReadOnlyList<ScopeReversionV2CoverageContract> Coverage,
    IReadOnlyList<ScopeReversionV2PartitionTransitionContract> PartitionTransitions,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries,
    string PublicationClaimBoundary);

public static class ScopeReversionV2Contract
{
    public const string SchemaId = "infinium.analysis.scope-reversion/v2";
    public static readonly ContractVersion SchemaVersion = new(2, 0, 0);
    public const string TaxonomyId = "infinium.skyrim-se.mod-impact-taxonomy";
    public static readonly ContractVersion TaxonomyVersion = new(0, 1, 0);
    public const string ActorAdapterId = "infinium.scope-reversion.adapter.actor-cohort";
    public const string ReferenceAdapterId = "infinium.scope-reversion.adapter.placed-reference";
    public const string ExactClaimBoundary =
        "For the exact admitted controlled-real members and reported populations, the category-neutral deterministic local analyzer distinguishes one shared actor-cohort cause and one placed-reference cause from their matched restored-relation controls. It preserves residual facts and gaps and makes no claim of broad compatibility, patch safety, runtime correctness, completeness, precision, recall, or production readiness.";

    private static readonly string[] ExactBoundaryIds =
    [
        "archive", "credential", "evaluator-private", "hosted-search", "loot", "network", "nexus",
        "provider", "publication", "push", "semantic-oracle",
    ];

    private static readonly string[] RequiredCoverageIds =
    [
        "analyzer", "persistence", "projection", "purpose", "replay", "taxonomy",
    ];

    public static void Validate(ScopeReversionV2WorkAssignmentContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaId != SchemaId || value.SchemaVersion != SchemaVersion
            || value.PartitionRole == ScopeReversionV2PartitionRole.Unspecified
            || string.IsNullOrWhiteSpace(value.InputHandoffId) || value.InputHandoffId.Length > 256
            || value.PublicManifests.Count is < 1 or > 16 || value.ControlledInputs.Count is < 1 or > 128
            || value.Subjects.Count is < 1 or > 64 || value.Members.Count is < 1 or > 256
            || value.SourceDecisions.Count is < 1 or > 128 || value.Taxonomy.Count is < 1 or > 1024
            || !HasCanonicalAnalyzerBinding(value.Analyzer)
            || value.AssignmentId != ComputeAssignmentId(value.OriginatingRunId, value.SnapshotId,
                value.ContextId, value.ConfigurationId, value.ExecutionInputId,
                value.InputHandoffId, value.InputManifestFingerprint, value.PublicManifests, value.ControlledInputs,
                value.Subjects, value.Members,
                value.SourceDecisions, value.Taxonomy)
            || !CanonicalBoundaries(value.Boundaries))
        {
            throw new InvalidDataException("The scope-reversion v2 assignment identity, version, bounds, or isolation boundary is invalid.");
        }

        RequireUnique(value.Subjects.Select(item => item.SubjectId), "subject");
        RequireUnique(value.Members.Select(item => item.MemberId), "member");
        RequireUnique(value.SourceDecisions.Select(item => item.DecisionId), "source decision");
        RequireUnique(value.Taxonomy.Select(item => item.AssignmentId), "taxonomy assignment");
        RequireSorted(value.Subjects.Select(item => item.SubjectId.Value), "subjects");
        RequireSorted(value.Members.Select(item => item.MemberId.Value), "members");
        RequireSorted(value.SourceDecisions.Select(item => item.DecisionId.Value), "source decisions");
        RequireSorted(value.Taxonomy.Select(item => item.AssignmentId.Value), "taxonomy assignments");
        RequireSorted(value.PublicManifests.Select(item => item.RepositoryPath), "public manifests");
        RequireSorted(value.ControlledInputs.Select(item => item.RelativePath), "controlled inputs");
        if (value.PublicManifests.Select(item => item.RepositoryPath).Distinct(StringComparer.Ordinal).Count() != value.PublicManifests.Count
            || value.PublicManifests.Any(item => string.IsNullOrWhiteSpace(item.RepositoryPath)
                || Path.IsPathFullyQualified(item.RepositoryPath) || item.ByteLength < 1)
            || value.ControlledInputs.Select(item => item.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != value.ControlledInputs.Count
            || value.ControlledInputs.Any(item => string.IsNullOrWhiteSpace(item.RelativePath)
                || Path.IsPathFullyQualified(item.RelativePath) || item.RelativePath.Contains("..", StringComparison.Ordinal)
                || item.Role is not ("official-master" or "positive-plugin-or-asset" or "matched-patch-control" or "required-extraction-dependency")
                || item.ByteLength < 1))
        {
            throw new InvalidDataException("The scope-reversion v2 manifest or controlled-input provenance is invalid.");
        }

        Dictionary<OpaqueId, ScopeReversionV2MemberContract> members = value.Members.ToDictionary(item => item.MemberId);
        HashSet<OpaqueId> subjects = value.Subjects.Select(item => item.SubjectId).ToHashSet();
        HashSet<OpaqueId> sourceDecisions = value.SourceDecisions.Select(item => item.DecisionId).ToHashSet();
        HashSet<string> publicManifestPaths = value.PublicManifests.Select(item => item.RepositoryPath).ToHashSet(StringComparer.Ordinal);
        foreach (ScopeReversionV2SubjectContract subject in value.Subjects)
        {
            if (subject.Kind == ScopeReversionV2SubjectKind.Unspecified || subject.OrderedMemberIds.Count is < 1 or > 16
                || subject.Kind == ScopeReversionV2SubjectKind.ActorCohort && subject.OrderedMemberIds.Count != 2
                || subject.Kind == ScopeReversionV2SubjectKind.PlacedReference && subject.OrderedMemberIds.Count != 1
                || subject.OrderedMemberIds.Distinct().Count() != subject.OrderedMemberIds.Count
                || subject.OrderedMemberIds.Any(id => !members.TryGetValue(id, out ScopeReversionV2MemberContract? member)
                    || member.SubjectId != subject.SubjectId || member.DependencyCauseId != subject.SharedDependencyCauseId
                    || subject.Kind == ScopeReversionV2SubjectKind.ActorCohort && member.AdapterId != ActorAdapterId
                    || subject.Kind == ScopeReversionV2SubjectKind.PlacedReference && member.AdapterId != ReferenceAdapterId))
            {
                throw new InvalidDataException("A scope-reversion v2 subject contains dangling, duplicate, or false shared-cause members.");
            }
        }
        foreach (ScopeReversionV2MemberContract member in value.Members)
        {
            if (!subjects.Contains(member.SubjectId)
                || member.AdapterId is not (ActorAdapterId or ReferenceAdapterId)
                || member.SourceDecisionIds.Count == 0 || member.SourceDecisionIds.Any(id => !sourceDecisions.Contains(id))
                || member.DependencyIds.Count == 0 || member.EvidenceIds.Count == 0
                || member.PriorEffectiveState.EvidenceIds.Count == 0 || member.WinningState.EvidenceIds.Count == 0
                || (member.PriorEffectiveState.State == ScopeValueState.Present) != (member.PriorEffectiveState.ComparableValue is not null)
                || (member.WinningState.State == ScopeValueState.Present) != (member.WinningState.ComparableValue is not null))
            {
                throw new InvalidDataException("A scope-reversion v2 member is invalid or has dangling provenance.");
            }
            RequireSorted(member.SourceDecisionIds.Select(item => item.Value), "member source decisions");
            RequireSorted(member.DependencyIds.Select(item => item.Value), "member dependencies");
            RequireSorted(member.EvidenceIds.Select(item => item.Value), "member evidence");
            RequireSorted(member.ResidualFacts, "member residual facts");
            RequireSorted(member.Gaps, "member gaps");
        }
        foreach (ScopeReversionV2SourceDecisionContract decision in value.SourceDecisions)
        {
            if (decision.OriginatingRunId != value.OriginatingRunId || decision.ProposalState == SemanticProposalState.Unspecified
                || !publicManifestPaths.Contains(decision.PublicManifestPath)
                || decision.SupportState == SemanticSupportState.Unspecified || decision.ApplicabilityState == SemanticApplicabilityState.Unspecified
                || decision.DecisionState == SemanticDecisionState.Unspecified || decision.EvidenceIds.Count == 0
                || decision.SubjectIds.Count == 0 || decision.SubjectIds.Any(id => !subjects.Contains(id))
                || decision.DecisionState == SemanticDecisionState.Admitted
                    && (decision.SupportState != SemanticSupportState.Supported
                        || decision.ApplicabilityState != SemanticApplicabilityState.Applicable))
            {
                throw new InvalidDataException("Source support, local applicability, and host admission are not independently valid.");
            }
            RequireSorted(decision.SubjectIds.Select(item => item.Value), "source-decision subjects");
            RequireSorted(decision.Fields, "source-decision fields");
            RequireSorted(decision.EvidenceIds.Select(item => item.Value), "source-decision evidence");
        }
        foreach (ScopeReversionV2TaxonomyReferenceContract assignment in value.Taxonomy)
        {
            if (assignment.OriginatingRunId != value.OriginatingRunId || !subjects.Contains(assignment.SubjectId)
                || assignment.TaxonomyId != TaxonomyId || assignment.TaxonomyVersion != TaxonomyVersion
                || assignment.Applicability == TaxonomyApplicability.Unspecified || assignment.Role == ClassificationRole.Unspecified
                || (assignment.Applicability == TaxonomyApplicability.Assigned) != (assignment.Code is not null)
                || assignment.EvidenceIds.Count == 0)
            {
                throw new InvalidDataException("A scope-reversion v2 taxonomy reference is invalid or cross-run.");
            }
        }
    }

    public static void Validate(ScopeReversionV2AnalysisContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ScopeReversionV2WorkAssignmentContract assignment = new(
            value.SchemaId, value.SchemaVersion, value.AssignmentId, value.OriginatingRunId,
            value.SnapshotId, value.ContextId, value.ConfigurationId, value.ExecutionInputId, value.Analyzer,
            value.InputHandoffId, value.InputManifestFingerprint, value.PublicManifests, value.ControlledInputs,
            value.PartitionRole, value.Subjects, value.Members,
            value.SourceDecisions, value.Taxonomy, value.Boundaries);
        Validate(assignment);
        if (value.PayloadId != ComputePayloadId(value) || value.PublicationClaimBoundary != ExactClaimBoundary
            || value.Decisions.Count != value.Subjects.Count || value.Candidates.Count != value.Subjects.Count
            || value.Hypotheses.Count != value.Subjects.Count
            || value.Decisions.Select(item => item.SubjectId).OrderBy(item => item.Value, StringComparer.Ordinal)
                .SequenceEqual(value.Subjects.Select(item => item.SubjectId), EqualityComparer<OpaqueId>.Default) is false)
        {
            throw new InvalidDataException("The scope-reversion v2 output identity or subject projection is invalid.");
        }
        RequireUnique(value.Decisions.Select(item => item.DecisionId), "decision");
        RequireUnique(value.Candidates.Select(item => item.CandidateId), "candidate");
        RequireUnique(value.Hypotheses.Select(item => item.HypothesisId), "hypothesis");
        RequireUnique(value.Findings.Select(item => item.FindingId), "finding");
        RequireUnique(value.Cases.Select(item => item.CaseId), "case");
        RequireUnique(value.Recommendations.Select(item => item.RecommendationId), "recommendation");
        RequireUnique(value.PartitionTransitions.Select(item => item.TransitionId), "partition transition");
        RequireSorted(value.Decisions.Select(item => item.SubjectId.Value), "decisions");
        RequireSorted(value.Coverage.Select(item => item.PopulationId), "coverage");
        RequireSorted(value.PartitionTransitions.Select(item => item.TransitionId.Value), "partition transitions");
        string[] coverageIds = value.Coverage.Select(item => item.PopulationId).ToArray();
        if (coverageIds.Distinct(StringComparer.Ordinal).Count() != coverageIds.Length
            || RequiredCoverageIds.Any(required => !coverageIds.Contains(required, StringComparer.Ordinal)))
        {
            throw new InvalidDataException("The scope-reversion v2 output omits a required coverage population.");
        }
        Dictionary<OpaqueId, ScopeReversionV2DecisionContract> decisions = value.Decisions.ToDictionary(item => item.SubjectId);
        Dictionary<OpaqueId, ScopeReversionCandidateContract> candidates = value.Candidates.ToDictionary(item => item.MemberId);
        Dictionary<OpaqueId, ScopeReversionCandidateContract> candidatesById = value.Candidates.ToDictionary(item => item.CandidateId);
        Dictionary<OpaqueId, ScopeReversionHypothesisContract> hypotheses = value.Hypotheses.ToDictionary(item => item.CandidateId);
        HashSet<OpaqueId> findingIds = value.Findings.Select(item => item.FindingId).ToHashSet();
        long positives = value.Decisions.Count(item => item.Disposition == ScopeReversionDisposition.SupportedFinding);
        if (value.Findings.Count != positives || value.Cases.Count != positives || value.Recommendations.Count != positives
            || value.Candidates.Any(item => !decisions.TryGetValue(item.MemberId, out ScopeReversionV2DecisionContract? decision)
                || item.DecisionId != decision.DecisionId)
            || value.Findings.Any(item => item.Severity != FindingSeverity.Moderate
                || item.Confidence != AnalysisConfidence.StronglySupported
                || !candidatesById.TryGetValue(item.CandidateId, out ScopeReversionCandidateContract? candidate)
                || candidate.MemberId != item.SubjectId || candidate.State != ScopeCandidateState.Present)
            || value.Candidates.Any(item => !hypotheses.TryGetValue(item.CandidateId, out ScopeReversionHypothesisContract? hypothesis)
                || hypothesis.State != (item.State switch
                {
                    ScopeCandidateState.Present => ScopeHypothesisState.Present,
                    ScopeCandidateState.ResolvedNegative => ScopeHypothesisState.ResolvedRejected,
                    _ => ScopeHypothesisState.Abstained,
                }))
            || value.Cases.Any(item => !decisions.TryGetValue(item.SubjectId, out ScopeReversionV2DecisionContract? decision)
                || !candidates.ContainsKey(item.SubjectId)
                || item.FindingId is OpaqueId findingId && !findingIds.Contains(findingId)
                || item.CandidateId != candidates[item.SubjectId].CandidateId
                || decision.Disposition != ScopeReversionDisposition.SupportedFinding || item.FindingId is null
                || !item.MemberIds.SequenceEqual(value.Subjects.Single(subject => subject.SubjectId == item.SubjectId).OrderedMemberIds))
            || value.Recommendations.Any(item => !findingIds.Contains(item.FindingId))
            || value.Taxonomy.GroupBy(item => item.SubjectId).Any(group => group.Select(item => item.Axis).Distinct(StringComparer.Ordinal).Count() < 4)
            || value.Coverage.Any(item => item.Denominator != item.Completed + item.CompletedWithGaps + item.Unsupported + item.Failed
                || item.Denominator != item.MemberIds.Count
                || item.MemberIds.Distinct().Count() != item.MemberIds.Count
                || !item.MemberIds.Select(id => id.Value).SequenceEqual(
                    item.MemberIds.Select(id => id.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            || value.PartitionRole == ScopeReversionV2PartitionRole.ControlledRealValidation && value.PartitionTransitions.Count != 0
            || value.PartitionRole == ScopeReversionV2PartitionRole.ControlledRealDevelopment
                && (value.PartitionTransitions.Count == 0 || value.PartitionTransitions.Any(item =>
                    item.InputManifestFingerprint != value.InputManifestFingerprint
                    || item.FromRole != ScopeReversionV2PartitionRole.ControlledRealValidation
                    || item.ToRole != ScopeReversionV2PartitionRole.ControlledRealDevelopment
                    || string.IsNullOrWhiteSpace(item.CaseId)
                    || string.IsNullOrWhiteSpace(item.CandidateIdentity)
                    || string.IsNullOrWhiteSpace(item.Reason))))
        {
            throw new InvalidDataException("The scope-reversion v2 promotion, shared-case, taxonomy, or coverage semantics are inconsistent.");
        }
    }

    public static OpaqueId ComputeAssignmentId(OpaqueId runId, OpaqueId snapshotId, OpaqueId contextId,
        OpaqueId configurationId, OpaqueId executionInputId, string handoffId, Sha256Fingerprint manifestFingerprint,
        IReadOnlyList<ScopeReversionV2PublicManifestReferenceContract> publicManifests,
        IReadOnlyList<ScopeReversionV2ControlledInputReferenceContract> controlledInputs,
        IReadOnlyList<ScopeReversionV2SubjectContract> subjects, IReadOnlyList<ScopeReversionV2MemberContract> members,
        IReadOnlyList<ScopeReversionV2SourceDecisionContract> sourceDecisions,
        IReadOnlyList<ScopeReversionV2TaxonomyReferenceContract> taxonomy) =>
        StableId("scope-v2-assignment", runId.Value, snapshotId.Value, contextId.Value,
            configurationId.Value, executionInputId.Value, handoffId, manifestFingerprint.Value,
            ContractJsonSerializer.Fingerprint(new
            {
                PublicManifests = publicManifests,
                ControlledInputs = controlledInputs,
                Subjects = subjects,
                Members = members,
                SourceDecisions = sourceDecisions,
                Taxonomy = taxonomy,
            }).Value);

    public static OpaqueId ComputePayloadId(ScopeReversionV2AnalysisContract value) => StableId(
        "scope-v2-payload", ContractJsonSerializer.Fingerprint(new
        {
            value.SchemaId, value.SchemaVersion, value.OriginatingRunId, value.AssignmentId,
            value.SnapshotId, value.ContextId, value.ConfigurationId, value.ExecutionInputId,
            value.InputHandoffId, value.InputManifestFingerprint, value.PublicManifests, value.ControlledInputs,
            value.Analyzer, value.PartitionRole, value.Subjects, value.Members,
            value.SourceDecisions, value.Taxonomy, value.Decisions, value.Candidates, value.Hypotheses,
            value.Findings, value.Cases,
            value.Recommendations, value.Gaps, value.Coverage, value.PartitionTransitions, value.Boundaries,
            value.PublicationClaimBoundary,
        }).Value);

    public static OpaqueId StableId(string prefix, params string[] parts)
    {
        string canonical = string.Join("\u001f", parts.Prepend(prefix));
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        return new OpaqueId(prefix + "-" + hash[..32]);
    }

    private static bool HasCanonicalAnalyzerBinding(ScopeReversionAnalyzerBindingContract analyzer) =>
        analyzer.AnalyzerFamily == "infinium.scope-reversion"
        && analyzer.AnalyzerId == "infinium.scope-reversion.local"
        && analyzer.AnalyzerVersion == new ContractVersion(2, 0, 0)
        && analyzer.SemanticContractVersion == new ContractVersion(2, 0, 0)
        && analyzer.IdentityContractVersion == new ContractVersion(2, 0, 0)
        && analyzer.RulesetVersion == new ContractVersion(1, 0, 0)
        && analyzer.Maturity == AnalyzerMaturity.Experimental
        && analyzer.CanonicalDeclarationJson.Length is >= 2 and <= 65_536
        && Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(analyzer.CanonicalDeclarationJson))) == analyzer.DeclarationFingerprint.Value;

    private static bool CanonicalBoundaries(IReadOnlyList<ExecutionBoundaryContract> boundaries) =>
        boundaries.Count == ExactBoundaryIds.Length
        && boundaries.All(item => item.State == BoundaryUseState.NotUsed)
        && boundaries.Select(item => item.BoundaryId).SequenceEqual(ExactBoundaryIds, StringComparer.Ordinal);

    private static void RequireUnique(IEnumerable<OpaqueId> values, string label)
    {
        string[] ids = values.Select(item => item.Value).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            throw new InvalidDataException($"Scope-reversion v2 {label} identities must be unique.");
        }
    }

    private static void RequireSorted(IEnumerable<string> values, string label)
    {
        string[] ids = values.ToArray();
        if (!ids.SequenceEqual(ids.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new InvalidDataException($"Scope-reversion v2 {label} must use canonical ordinal order.");
        }
    }
}
