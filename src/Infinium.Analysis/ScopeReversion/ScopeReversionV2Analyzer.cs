using Infinium.Domain.Contracts;

namespace Infinium.Analysis.ScopeReversion;

public static class ScopeReversionV2Analyzer
{
    public static readonly ExecutionBoundaryContract[] NotUsedBoundaries =
    [
        new("archive", BoundaryUseState.NotUsed, "only the admitted extracted handoff is read; archives are never opened"),
        new("credential", BoundaryUseState.NotUsed, "local deterministic analysis"),
        new("evaluator-private", BoundaryUseState.NotUsed, "ordinary product conformance only"),
        new("hosted-search", BoundaryUseState.NotUsed, "local deterministic analysis"),
        new("loot", BoundaryUseState.NotUsed, "no load-order tooling or external advice"),
        new("network", BoundaryUseState.NotUsed, "local deterministic analysis"),
        new("nexus", BoundaryUseState.NotUsed, "no acquisition"),
        new("provider", BoundaryUseState.NotUsed, "no model or provider call"),
        new("publication", BoundaryUseState.NotUsed, "local receipt only"),
        new("push", BoundaryUseState.NotUsed, "local repository work only"),
        new("semantic-oracle", BoundaryUseState.NotUsed, "developer-owned product conformance"),
    ];

    public static ScopeReversionV2AnalysisContract Execute(
        ScopeReversionV2WorkAssignmentContract assignment,
        IReadOnlyList<ScopeReversionV2PartitionTransitionContract>? partitionTransitions = null)
    {
        ScopeReversionV2Contract.Validate(assignment);
        List<ScopeReversionV2DecisionContract> decisions = [];
        List<ScopeReversionCandidateContract> candidates = [];
        List<ScopeReversionHypothesisContract> hypotheses = [];
        List<ScopeReversionV2FindingContract> findings = [];
        List<ScopeReversionV2CaseContract> cases = [];
        List<ScopeReversionRecommendationContract> recommendations = [];
        List<ScopeReversionGapContract> gaps = [];

        Dictionary<OpaqueId, ScopeReversionV2MemberContract> memberById = assignment.Members.ToDictionary(item => item.MemberId);
        Dictionary<OpaqueId, ScopeReversionV2SourceDecisionContract> sourceById = assignment.SourceDecisions.ToDictionary(item => item.DecisionId);
        foreach (ScopeReversionV2SubjectContract subject in assignment.Subjects)
        {
            ScopeReversionV2MemberContract[] members = subject.OrderedMemberIds.Select(id => memberById[id]).ToArray();
            ScopeReversionV2SourceDecisionContract[] sourceDecisions = members
                .SelectMany(member => member.SourceDecisionIds).Distinct().Select(id => sourceById[id]).ToArray();
            bool admitted = sourceDecisions.All(item => item.SupportState == SemanticSupportState.Supported
                && item.ApplicabilityState == SemanticApplicabilityState.Applicable
                && item.DecisionState == SemanticDecisionState.Admitted);
            bool comparable = members.All(item => item.PriorEffectiveState.State is ScopeValueState.Present or ScopeValueState.Absent
                && item.WinningState.State is ScopeValueState.Present or ScopeValueState.Absent);
            bool allChanged = comparable && members.All(item => !StringComparer.Ordinal.Equals(
                item.PriorEffectiveState.ComparableValue, item.WinningState.ComparableValue));
            bool allRestored = comparable && members.All(item => StringComparer.Ordinal.Equals(
                item.PriorEffectiveState.ComparableValue, item.WinningState.ComparableValue));
            bool purposeChanged = members.All(item => item.WinningPurposeChangeObserved);
            ScopeReversionDisposition disposition;
            ScopeTransitionKind transition;
            ScopeCoverageRelation coverageRelation;
            string rationale;
            if (!comparable)
            {
                disposition = ScopeReversionDisposition.Unsupported;
                transition = ScopeTransitionKind.Unsupported;
                coverageRelation = ScopeCoverageRelation.Undecidable;
                rationale = "A required prior or winning relation is unavailable; lower-layer facts and the exact gap remain visible.";
            }
            else if (!admitted)
            {
                disposition = ScopeReversionDisposition.Abstained;
                transition = allRestored ? ScopeTransitionKind.Unchanged : ScopeTransitionKind.Changed;
                coverageRelation = ScopeCoverageRelation.Undecidable;
                rationale = "Source support, exact local applicability, or host admission is not closed; no conclusion is published.";
            }
            else if (allChanged && purposeChanged)
            {
                disposition = ScopeReversionDisposition.SupportedFinding;
                transition = ScopeTransitionKind.Changed;
                coverageRelation = ScopeCoverageRelation.DoesNotCoverTransition;
                rationale = "Every separately evidenced subject member shares one closed dependency cause, and the supported winning purpose does not cover the lost relation.";
            }
            else if (allRestored)
            {
                disposition = ScopeReversionDisposition.ResolvedNegative;
                transition = ScopeTransitionKind.Unchanged;
                coverageRelation = ScopeCoverageRelation.CoversTransition;
                rationale = "The matched control retains the winning purpose change while restoring the established relation; it is not a safety claim.";
            }
            else
            {
                disposition = ScopeReversionDisposition.Abstained;
                transition = ScopeTransitionKind.Unresolved;
                coverageRelation = ScopeCoverageRelation.Undecidable;
                rationale = "The cohort does not have one uniform causal transition, so false cause sharing is rejected.";
            }

            OpaqueId decisionId = ScopeReversionV2Contract.StableId("scope-v2-decision", assignment.OriginatingRunId.Value, subject.SubjectId.Value);
            OpaqueId candidateId = ScopeReversionV2Contract.StableId("scope-v2-candidate", decisionId.Value);
            OpaqueId[] evidence = members.SelectMany(item => item.EvidenceIds)
                .Concat(sourceDecisions.SelectMany(item => item.EvidenceIds)).Distinct()
                .OrderBy(item => item.Value, StringComparer.Ordinal).ToArray();
            decisions.Add(new ScopeReversionV2DecisionContract(
                decisionId, subject.SubjectId, transition, coverageRelation, disposition, rationale,
                subject.OrderedMemberIds, evidence));
            candidates.Add(new ScopeReversionCandidateContract(
                candidateId, decisionId, subject.SubjectId,
                disposition switch
                {
                    ScopeReversionDisposition.SupportedFinding => ScopeCandidateState.Present,
                    ScopeReversionDisposition.ResolvedNegative => ScopeCandidateState.ResolvedNegative,
                    _ => ScopeCandidateState.Ambiguous,
                },
                rationale, evidence, [], disposition is ScopeReversionDisposition.Abstained or ScopeReversionDisposition.Unsupported
                    ? subject.ClaimGaps : []));
            hypotheses.Add(new ScopeReversionHypothesisContract(
                ScopeReversionV2Contract.StableId("scope-v2-hypothesis", candidateId.Value), candidateId,
                disposition switch
                {
                    ScopeReversionDisposition.SupportedFinding => ScopeHypothesisState.Present,
                    ScopeReversionDisposition.ResolvedNegative => ScopeHypothesisState.ResolvedRejected,
                    _ => ScopeHypothesisState.Abstained,
                },
                rationale, evidence, [], disposition is ScopeReversionDisposition.Abstained or ScopeReversionDisposition.Unsupported
                    ? subject.ClaimGaps : []));

            if (disposition == ScopeReversionDisposition.SupportedFinding)
            {
                OpaqueId findingId = ScopeReversionV2Contract.StableId("scope-v2-finding", candidateId.Value);
                findings.Add(new ScopeReversionV2FindingContract(
                    findingId, candidateId, subject.SubjectId, FindingSeverity.Moderate,
                    AnalysisConfidence.StronglySupported,
                    "A supported winning purpose change restores an older or absent relationship outside that bounded purpose.",
                    subject.PredictedSymptom, evidence));
                OpaqueId caseId = ScopeReversionV2Contract.StableId("scope-v2-case", subject.SharedDependencyCauseId.Value);
                cases.Add(new ScopeReversionV2CaseContract(
                    caseId, ScopeReversionV2Contract.StableId("scope-v2-logical-case", subject.SharedDependencyCauseId.Value),
                    candidateId, findingId, subject.SubjectId, subject.SharedDependencyCauseId, "supported",
                    subject.OrderedMemberIds, evidence));
                recommendations.Add(new ScopeReversionRecommendationContract(
                    ScopeReversionV2Contract.StableId("scope-v2-recommendation", findingId.Value), findingId,
                    subject.Recommendation, "apply as a reversible local patch or winner correction", subject.Validation, evidence));
            }
            foreach (string gap in subject.ClaimGaps.Concat(members.SelectMany(item => item.Gaps)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                gaps.Add(new ScopeReversionGapContract(
                    ScopeReversionV2Contract.StableId("scope-v2-gap", subject.SubjectId.Value, gap), subject.SubjectId,
                    subject.Kind == ScopeReversionV2SubjectKind.ActorCohort ? "actor-controlled-real" : "reference-controlled-real",
                    ScopeGapFailureState.Gap, gap, gap));
            }
        }

        ScopeReversionV2CoverageContract[] coverage = BuildCoverage(assignment, decisions);
        ScopeReversionV2AnalysisContract result = new(
            ScopeReversionV2Contract.SchemaId, ScopeReversionV2Contract.SchemaVersion,
            new OpaqueId("scope-v2-pending"), assignment.OriginatingRunId, assignment.AssignmentId,
            assignment.SnapshotId, assignment.ContextId, assignment.ConfigurationId, assignment.ExecutionInputId,
            assignment.InputHandoffId, assignment.InputManifestFingerprint,
            assignment.PublicManifests, assignment.ControlledInputs, assignment.Analyzer, assignment.PartitionRole,
            assignment.Subjects, assignment.Members, assignment.SourceDecisions, assignment.Taxonomy,
            decisions.OrderBy(item => item.SubjectId.Value, StringComparer.Ordinal).ToArray(),
            candidates.OrderBy(item => item.MemberId.Value, StringComparer.Ordinal).ToArray(),
            hypotheses.OrderBy(item => item.CandidateId.Value, StringComparer.Ordinal).ToArray(),
            findings.OrderBy(item => item.SubjectId.Value, StringComparer.Ordinal).ToArray(),
            cases.OrderBy(item => item.SubjectId.Value, StringComparer.Ordinal).ToArray(),
            recommendations.OrderBy(item => item.RecommendationId.Value, StringComparer.Ordinal).ToArray(),
            gaps.OrderBy(item => item.GapId.Value, StringComparer.Ordinal).ToArray(), coverage,
            (partitionTransitions ?? []).OrderBy(item => item.TransitionId.Value, StringComparer.Ordinal).ToArray(),
            assignment.Boundaries, ScopeReversionV2Contract.ExactClaimBoundary);
        result = result with { PayloadId = ScopeReversionV2Contract.ComputePayloadId(result) };
        ScopeReversionV2Contract.Validate(result);
        return result;
    }

    private static ScopeReversionV2CoverageContract[] BuildCoverage(
        ScopeReversionV2WorkAssignmentContract assignment,
        IReadOnlyList<ScopeReversionV2DecisionContract> decisions)
    {
        List<ScopeReversionV2CoverageContract> coverage = [];
        Dictionary<OpaqueId, ScopeReversionV2MemberContract> members = assignment.Members.ToDictionary(item => item.MemberId);
        Dictionary<OpaqueId, ScopeReversionV2DecisionContract> decisionsBySubject = decisions.ToDictionary(item => item.SubjectId);
        foreach (IGrouping<string, ScopeReversionV2SubjectContract> group in assignment.Subjects
            .GroupBy(subject => DomainLane(subject, subject.OrderedMemberIds.Select(id => members[id]).ToArray()), StringComparer.Ordinal))
        {
            OpaqueId[] ids = group.SelectMany(item => item.OrderedMemberIds).OrderBy(item => item.Value, StringComparer.Ordinal).ToArray();
            Add(group.Key, ids, id => DecisionState(decisionsBySubject[members[id].SubjectId], members[id].Gaps.Count > 0));
        }
        Add("projection", assignment.Members.Select(item => item.MemberId), id =>
        {
            ScopeReversionV2MemberContract member = members[id];
            bool comparable = member.PriorEffectiveState.State is ScopeValueState.Present or ScopeValueState.Absent
                && member.WinningState.State is ScopeValueState.Present or ScopeValueState.Absent;
            return comparable ? (member.Gaps.Count == 0 ? CoverageBucket.Completed : CoverageBucket.CompletedWithGaps)
                : CoverageBucket.Unsupported;
        });
        Dictionary<OpaqueId, ScopeReversionV2SourceDecisionContract> sources = assignment.SourceDecisions.ToDictionary(item => item.DecisionId);
        Add("purpose", sources.Keys, id => sources[id].DecisionState == SemanticDecisionState.Admitted
            ? CoverageBucket.Completed : CoverageBucket.CompletedWithGaps);
        Dictionary<OpaqueId, ScopeReversionV2TaxonomyReferenceContract> taxonomy = assignment.Taxonomy.ToDictionary(item => item.AssignmentId);
        Add("taxonomy", taxonomy.Keys, id => taxonomy[id].Applicability is TaxonomyApplicability.Assigned or TaxonomyApplicability.NotApplicable
            ? CoverageBucket.Completed : CoverageBucket.Unsupported);
        Add("analyzer", assignment.Subjects.Select(item => item.SubjectId), id =>
            DecisionState(decisionsBySubject[id], assignment.Subjects.Single(item => item.SubjectId == id).ClaimGaps.Count > 0));
        Add("persistence", assignment.Subjects.Select(item => item.SubjectId), id => DecisionState(decisionsBySubject[id], hasGaps: false));
        Add("replay", assignment.Subjects.Select(item => item.SubjectId), id => DecisionState(decisionsBySubject[id], hasGaps: false));
        return coverage.OrderBy(item => item.PopulationId, StringComparer.Ordinal).ToArray();

        void Add(string populationId, IEnumerable<OpaqueId> populationMembers, Func<OpaqueId, CoverageBucket> state)
        {
            OpaqueId[] ids = populationMembers.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray();
            coverage.Add(new ScopeReversionV2CoverageContract(
                populationId, ids.LongLength,
                ids.LongCount(id => state(id) == CoverageBucket.Completed),
                ids.LongCount(id => state(id) == CoverageBucket.CompletedWithGaps),
                ids.LongCount(id => state(id) == CoverageBucket.Unsupported),
                ids.LongCount(id => state(id) == CoverageBucket.Failed), ids));
        }
    }

    private static string DomainLane(
        ScopeReversionV2SubjectContract subject,
        IReadOnlyList<ScopeReversionV2MemberContract> members)
    {
        bool comparable = members.All(item => item.PriorEffectiveState.State is ScopeValueState.Present or ScopeValueState.Absent
            && item.WinningState.State is ScopeValueState.Present or ScopeValueState.Absent);
        string lane = comparable && members.All(item => StringComparer.Ordinal.Equals(
            item.PriorEffectiveState.ComparableValue, item.WinningState.ComparableValue))
            ? "control"
            : comparable && members.All(item => !StringComparer.Ordinal.Equals(
                item.PriorEffectiveState.ComparableValue, item.WinningState.ComparableValue))
                ? "positive"
                : "unresolved";
        return (subject.Kind == ScopeReversionV2SubjectKind.ActorCohort ? "actor-" : "reference-") + lane;
    }

    private static CoverageBucket DecisionState(ScopeReversionV2DecisionContract decision, bool hasGaps) =>
        decision.Disposition switch
        {
            ScopeReversionDisposition.SupportedFinding or ScopeReversionDisposition.ResolvedNegative =>
                hasGaps ? CoverageBucket.CompletedWithGaps : CoverageBucket.Completed,
            ScopeReversionDisposition.Failed => CoverageBucket.Failed,
            _ => CoverageBucket.Unsupported,
        };

    private enum CoverageBucket
    {
        Completed,
        CompletedWithGaps,
        Unsupported,
        Failed,
    }
}
