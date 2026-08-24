using Infinium.Domain.Contracts;

namespace Infinium.Analysis.ScopeReversion;

public sealed record ScopeReversionTotalityState(
    ScopeTransitionKind Transition,
    ScopeSupportState Support,
    ScopeApplicabilityState Applicability,
    bool WinningPurposeChangeObserved,
    ScopeCoverageRelation PurposeCoverage,
    ScopeContradictionState Contradiction,
    ScopeCausalClosureState CausalClosure,
    ScopePublicationEligibility PublicationEligibility,
    CoverageMemberState Coverage,
    ScopeGapFailureState GapFailure);

public sealed record ScopeReversionDispositionRule(
    string RuleId,
    ScopeReversionDisposition Disposition,
    Func<ScopeReversionTotalityState, bool> Matches);

public static class ScopeReversionAnalyzer
{
    private static readonly bool[] BooleanStates = [false, true];

    private static readonly ScopeReversionDisposition[] ClosedDispositions =
        Enum.GetValues<ScopeReversionDisposition>().Where(item => item != ScopeReversionDisposition.Unspecified).ToArray();

    private static readonly IReadOnlyList<ScopeReversionDispositionRule> Rules = ClosedDispositions
        .Select(disposition => new ScopeReversionDispositionRule(
            "scope-reversion-" + disposition.ToString().ToLowerInvariant(),
            disposition,
            state => ClassifyCore(state) == disposition))
        .ToArray();

    static ScopeReversionAnalyzer() => ValidateTotality(Rules);

    public static ScopeReversionAnalysisContract Execute(ScopeReversionWorkAssignmentContract assignment)
    {
        ScopeReversionContractInvariants.Validate(assignment);
        if (assignment.Analyzer.AnalyzerFamily != ScopeReversionAnalyzerDeclaration.AnalyzerFamily
            || assignment.Analyzer.AnalyzerId != ScopeReversionAnalyzerDeclaration.AnalyzerId
            || assignment.Analyzer.AnalyzerVersion != new ContractVersion(1, 0, 0)
            || assignment.Analyzer.SemanticContractVersion != new ContractVersion(1, 0, 0)
            || assignment.Analyzer.IdentityContractVersion != new ContractVersion(1, 0, 0)
            || assignment.Analyzer.RulesetVersion != new ContractVersion(1, 0, 0))
        {
            throw new InvalidDataException("Scope-reversion assignment admits a mixed or drifted analyzer identity.");
        }

        ScopeReversionMemberContract[] enabledMembers = assignment.Members
            .Where(item => assignment.Configuration.EnabledAdapterIds.Contains(item.AdapterId, StringComparer.Ordinal))
            .OrderBy(item => item.MemberId.Value, StringComparer.Ordinal)
            .ToArray();
        if (enabledMembers.Any(item => DeriveTransition(item) == ScopeTransitionKind.Invalid))
        {
            throw new InvalidDataException("Malformed scope-reversion input is rejected atomically before semantic output.");
        }

        List<ScopeReversionDecisionContract> decisions = [];
        List<ScopeReversionCandidateContract> candidates = [];
        List<ScopeReversionHypothesisContract> hypotheses = [];
        List<ScopeReversionContradictionContract> contradictions = [];
        List<ScopeReversionAbstentionContract> abstentions = [];
        List<ScopeReversionGapContract> gaps = [];
        List<ScopeReversionFailureContract> failures = [];
        List<ScopeReversionFindingContract> findings = [];
        List<ScopeReversionCaseContract> cases = [];
        List<ScopeReversionRecommendationContract> recommendations = [];
        List<ScopeReversionTaxonomyFactContract> taxonomy = [];
        List<ScopeReversionDependencyEdgeContract> dependencyEdges = [];

        foreach (ScopeReversionMemberContract member in enabledMembers)
        {
            ScopeTransitionKind transition = DeriveTransition(member);
            ScopeCoverageRelation purposeCoverage = DerivePurposeCoverage(member);
            ScopeReversionTotalityState totality = new(
                transition,
                member.Purpose.Support,
                member.Purpose.Applicability,
                member.Purpose.WinningPurposeChangeObserved,
                purposeCoverage,
                member.Contradiction,
                member.CausalClosure.State,
                member.PublicationEligibility,
                member.CoverageState,
                member.GapFailureState);
            ScopeReversionDisposition disposition = Classify(totality);
            OpaqueId decisionId = ScopeReversionIdentity.StableId(
                "scope-decision", assignment.OriginatingRunId.Value, member.MemberId.Value,
                transition.ToString(), purposeCoverage.ToString(), disposition.ToString());
            OpaqueId candidateId = ScopeReversionIdentity.StableId("scope-candidate", decisionId.Value);
            OpaqueId hypothesisId = ScopeReversionIdentity.StableId("scope-hypothesis", candidateId.Value);
            OpaqueId[] evidence = Evidence(member);
            decisions.Add(new ScopeReversionDecisionContract(
                decisionId,
                member.MemberId,
                transition,
                purposeCoverage,
                disposition,
                Rationale(disposition, transition, purposeCoverage),
                member.CausalClosure.DependencyClosureId,
                evidence));

            if (disposition is ScopeReversionDisposition.SupportedFinding
                or ScopeReversionDisposition.ResolvedNegative
                or ScopeReversionDisposition.Abstained)
            {
                IReadOnlyList<string> missing = disposition == ScopeReversionDisposition.Abstained
                    ? MissingInformation(totality)
                    : [];
                ScopeCandidateState candidateState = disposition switch
                {
                    ScopeReversionDisposition.SupportedFinding => ScopeCandidateState.Present,
                    ScopeReversionDisposition.ResolvedNegative => ScopeCandidateState.ResolvedNegative,
                    _ => ScopeCandidateState.Ambiguous,
                };
                ScopeHypothesisState hypothesisState = disposition switch
                {
                    ScopeReversionDisposition.SupportedFinding => ScopeHypothesisState.Present,
                    ScopeReversionDisposition.ResolvedNegative => ScopeHypothesisState.ResolvedRejected,
                    _ => ScopeHypothesisState.Abstained,
                };
                OpaqueId[] contradicting = disposition == ScopeReversionDisposition.ResolvedNegative
                    ? member.Purpose.EvidenceIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray()
                    : [];
                candidates.Add(new ScopeReversionCandidateContract(
                    candidateId, decisionId, member.MemberId, candidateState,
                    member.DomainInterpretation.Explanation, evidence, contradicting, missing));
                hypotheses.Add(new ScopeReversionHypothesisContract(
                    hypothesisId, candidateId, hypothesisState,
                    "The winning contribution may have reverted an established feature outside its supported purpose.",
                    evidence, contradicting, missing));

                if (disposition == ScopeReversionDisposition.ResolvedNegative)
                {
                    contradictions.Add(new ScopeReversionContradictionContract(
                        ScopeReversionIdentity.StableId("scope-contradiction", candidateId.Value),
                        candidateId,
                        member.Contradiction,
                        member.Contradiction == ScopeContradictionState.IntentionalChange
                            ? "Retained evidence establishes that the observed transition is intentional or harmless."
                            : "The observed transition does not satisfy the complete supported reversion shape.",
                        contradicting));
                }
                else if (disposition == ScopeReversionDisposition.Abstained)
                {
                    abstentions.Add(new ScopeReversionAbstentionContract(
                        ScopeReversionIdentity.StableId("scope-abstention", candidateId.Value),
                        candidateId, hypothesisId,
                        "Required comparison, purpose, applicability, causal, or coverage information is not closed.",
                        missing, evidence));
                }
                else
                {
                    Promote(member, assignment.OriginatingRunId, candidateId, hypothesisId, evidence,
                        findings, cases, recommendations);
                }

                AddTaxonomy(member, disposition, evidence, taxonomy);
            }

            if (disposition is ScopeReversionDisposition.Unsupported
                or ScopeReversionDisposition.Abstained
                or ScopeReversionDisposition.Limited
                or ScopeReversionDisposition.Unpublishable)
            {
                gaps.Add(new ScopeReversionGapContract(
                    ScopeReversionIdentity.StableId("scope-gap", decisionId.Value),
                    member.MemberId,
                    PopulationPrefix(member) + "-transition",
                    disposition == ScopeReversionDisposition.Limited ? ScopeGapFailureState.Limited : ScopeGapFailureState.Gap,
                    Rationale(disposition, transition, purposeCoverage),
                    member.Issue ?? string.Join("; ", MissingInformation(totality).DefaultIfEmpty("unsupported semantic region"))));
            }
            if (disposition == ScopeReversionDisposition.Failed)
            {
                failures.Add(new ScopeReversionFailureContract(
                    ScopeReversionIdentity.StableId("scope-failure", decisionId.Value),
                    member.MemberId, "local-analyzer-failure",
                    member.Issue ?? "The local analyzer failed at a bounded member boundary.", false));
            }

            foreach (OpaqueId dependencyId in member.CausalClosure.DependencyIds)
            {
                dependencyEdges.Add(new ScopeReversionDependencyEdgeContract(
                    ScopeReversionIdentity.StableId("scope-edge", decisionId.Value, dependencyId.Value),
                    "decision", decisionId, "dependency", dependencyId, "depends-on"));
            }
            foreach (OpaqueId evidenceId in evidence)
            {
                dependencyEdges.Add(new ScopeReversionDependencyEdgeContract(
                    ScopeReversionIdentity.StableId("scope-edge", decisionId.Value, evidenceId.Value, "evidence"),
                    "decision", decisionId, "evidence", evidenceId, "derived-from"));
            }
        }

        ScopeReversionCoverageContract[] coverage = BuildCoverage(assignment, decisions);
        ScopeReversionCountsContract counts = new(
            decisions.Count, decisions.Count, candidates.Count, hypotheses.Count, contradictions.Count,
            abstentions.Count, gaps.Count, failures.Count,
            decisions.Count(item => item.Disposition == ScopeReversionDisposition.SupportedFinding),
            decisions.Count(item => item.Disposition == ScopeReversionDisposition.ResolvedNegative),
            decisions.Count(item => item.Disposition == ScopeReversionDisposition.Unsupported),
            decisions.Count(item => item.Disposition == ScopeReversionDisposition.InvalidInput),
            decisions.Count(item => item.Disposition == ScopeReversionDisposition.Limited),
            decisions.Count(item => item.Disposition == ScopeReversionDisposition.Unpublishable),
            findings.Count, cases.Count, recommendations.Count);
        ScopeReversionTaxonomyFactContract[] canonicalTaxonomy = taxonomy
            .OrderBy(item => item.TaxonomyFactId.Value, StringComparer.Ordinal).ToArray();
        ScopeReversionDependencyEdgeContract[] canonicalDependencyEdges = dependencyEdges
            .OrderBy(item => item.EdgeId.Value, StringComparer.Ordinal).ToArray();
        ScopeReversionAnalysisContract result = new(
            ContractConstants.ScopeReversionSchemaId,
            new ContractVersion(1, 0, 0),
            new OpaqueId("scope-reversion-pending"),
            assignment.OriginatingRunId,
            assignment.AssignmentId,
            assignment.InputFingerprint,
            assignment.Analyzer,
            decisions,
            candidates,
            hypotheses,
            contradictions,
            abstentions,
            gaps,
            failures,
            findings,
            cases,
            recommendations,
            canonicalTaxonomy,
            coverage,
            canonicalDependencyEdges,
            counts,
            assignment.Boundaries,
            ScopeReversionContractInvariants.ExactClaimBoundary);
        result = result with { PayloadId = ScopeReversionContractInvariants.ComputePayloadId(result) };
        ScopeReversionContractInvariants.Validate(result);
        return result;
    }

    public static ScopeReversionDisposition Classify(ScopeReversionTotalityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ScopeReversionDispositionRule[] matches = Rules.Where(rule => rule.Matches(state)).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException("Scope-reversion totality table has a gap or overlap.");
        }
        return matches[0].Disposition;
    }

    public static void ValidateTotality(IReadOnlyList<ScopeReversionDispositionRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        foreach (ScopeReversionTotalityState state in EnumerateStates())
        {
            int matchCount = rules.Count(rule => rule.Matches(state));
            if (matchCount != 1)
            {
                throw new InvalidOperationException(
                    $"Scope-reversion totality state '{state}' matched {matchCount} dispositions.");
            }
        }
    }

    public static ScopeTransitionKind DeriveTransition(ScopeReversionMemberContract member)
    {
        ScopeValueState prior = member.PriorEffectiveState.State;
        ScopeValueState winner = member.WinningState.State;
        if (prior == ScopeValueState.Invalid || winner == ScopeValueState.Invalid)
        {
            return ScopeTransitionKind.Invalid;
        }
        if (prior == ScopeValueState.Unsupported || winner == ScopeValueState.Unsupported)
        {
            return ScopeTransitionKind.Unsupported;
        }
        if (prior == ScopeValueState.Unresolved || winner == ScopeValueState.Unresolved)
        {
            return ScopeTransitionKind.Unresolved;
        }
        if (prior == ScopeValueState.Absent && winner == ScopeValueState.Present)
        {
            return ScopeTransitionKind.Created;
        }
        if (prior == ScopeValueState.Present && winner == ScopeValueState.Absent)
        {
            return ScopeTransitionKind.Absent;
        }
        if (prior == ScopeValueState.Absent && winner == ScopeValueState.Absent)
        {
            return ScopeTransitionKind.Unchanged;
        }
        return StringComparer.Ordinal.Equals(
            member.PriorEffectiveState.ComparableValue,
            member.WinningState.ComparableValue)
            ? ScopeTransitionKind.Unchanged : ScopeTransitionKind.Changed;
    }

    public static ScopeCoverageRelation DerivePurposeCoverage(ScopeReversionMemberContract member)
    {
        if (member.Purpose.Support != ScopeSupportState.Supported
            || member.Purpose.Applicability != ScopeApplicabilityState.Applicable)
        {
            return ScopeCoverageRelation.Undecidable;
        }
        if (member.Contradiction == ScopeContradictionState.Defeating)
        {
            return ScopeCoverageRelation.Conflicts;
        }
        if (member.Purpose.CoveredDimensions.Contains(member.FeatureDimension, StringComparer.Ordinal)
            || member.Purpose.IntentionalTransitionDimensions.Contains(member.FeatureDimension, StringComparer.Ordinal))
        {
            return ScopeCoverageRelation.CoversTransition;
        }
        return ScopeCoverageRelation.DoesNotCoverTransition;
    }

    private static ScopeReversionDisposition ClassifyCore(ScopeReversionTotalityState state)
    {
        if (state.Transition == ScopeTransitionKind.Invalid)
        {
            return ScopeReversionDisposition.InvalidInput;
        }
        if (state.GapFailure == ScopeGapFailureState.Failed || state.Coverage == CoverageMemberState.Failed)
        {
            return ScopeReversionDisposition.Failed;
        }
        if (state.GapFailure == ScopeGapFailureState.Limited || state.Coverage == CoverageMemberState.SkippedByLimit)
        {
            return ScopeReversionDisposition.Limited;
        }
        if (state.PublicationEligibility == ScopePublicationEligibility.Ineligible)
        {
            return ScopeReversionDisposition.Unpublishable;
        }
        if (state.Transition == ScopeTransitionKind.Unsupported
            || state.Support == ScopeSupportState.Unsupported
            || state.Coverage is CoverageMemberState.Unsupported or CoverageMemberState.SkippedByConfiguration)
        {
            return ScopeReversionDisposition.Unsupported;
        }
        if (state.GapFailure == ScopeGapFailureState.Gap
            || state.Transition == ScopeTransitionKind.Unresolved
            || state.Support is ScopeSupportState.Unavailable or ScopeSupportState.NotEvaluated
            || state.Applicability is ScopeApplicabilityState.ConditionalUnestablished
                or ScopeApplicabilityState.Unknown or ScopeApplicabilityState.NotEvaluated
            || state.PurposeCoverage == ScopeCoverageRelation.Undecidable
            || state.Contradiction == ScopeContradictionState.Unknown
            || state.CausalClosure == ScopeCausalClosureState.Open
            || state.Coverage == CoverageMemberState.CompletedWithGaps)
        {
            return ScopeReversionDisposition.Abstained;
        }
        if (state.Support == ScopeSupportState.Contradicted
            || state.Applicability == ScopeApplicabilityState.NotApplicable
            || !state.WinningPurposeChangeObserved
            || state.Contradiction is ScopeContradictionState.IntentionalChange or ScopeContradictionState.Defeating
            || state.PurposeCoverage is ScopeCoverageRelation.CoversTransition or ScopeCoverageRelation.Conflicts
            || state.Transition is ScopeTransitionKind.Unchanged or ScopeTransitionKind.Created)
        {
            return ScopeReversionDisposition.ResolvedNegative;
        }
        return ScopeReversionDisposition.SupportedFinding;
    }

    private static IEnumerable<ScopeReversionTotalityState> EnumerateStates()
    {
        IEnumerable<T> Values<T>() where T : struct, Enum => Enum.GetValues<T>()
            .Where(item => Convert.ToInt32(item, System.Globalization.CultureInfo.InvariantCulture) != 0);
        return
            from transition in Values<ScopeTransitionKind>()
            from support in Values<ScopeSupportState>()
            from applicability in Values<ScopeApplicabilityState>()
            from winningPurposeChangeObserved in BooleanStates
            from relation in Values<ScopeCoverageRelation>()
            from contradiction in Values<ScopeContradictionState>()
            from closure in Values<ScopeCausalClosureState>()
            from publication in Values<ScopePublicationEligibility>()
            from coverage in Values<CoverageMemberState>()
            from gap in Values<ScopeGapFailureState>()
            select new ScopeReversionTotalityState(
                transition, support, applicability, winningPurposeChangeObserved,
                relation, contradiction, closure, publication, coverage, gap);
    }

    private static OpaqueId[] Evidence(ScopeReversionMemberContract member) =>
        member.PriorEffectiveState.EvidenceIds
            .Concat(member.WinningState.EvidenceIds)
            .Concat(member.Purpose.EvidenceIds)
            .Concat(member.CausalClosure.EvidenceIds)
            .Distinct()
            .OrderBy(item => item.Value, StringComparer.Ordinal)
            .ToArray();

    private static string Rationale(
        ScopeReversionDisposition disposition,
        ScopeTransitionKind transition,
        ScopeCoverageRelation coverage) => disposition switch
        {
            ScopeReversionDisposition.SupportedFinding =>
                $"The closed {transition} transition is outside the supported applicable purpose ({coverage}) and has no defeating contradiction.",
            ScopeReversionDisposition.ResolvedNegative =>
                $"The {transition} transition is covered, contradicted, unchanged, created, harmless, or otherwise resolved negative ({coverage}).",
            ScopeReversionDisposition.Abstained => "Required comparison, purpose, applicability, causal, or coverage information is unresolved.",
            ScopeReversionDisposition.Unsupported => "The semantic field, support state, or configured population is outside accepted support.",
            ScopeReversionDisposition.InvalidInput => "The neutral input is malformed or internally inconsistent.",
            ScopeReversionDisposition.Failed => "The bounded local analyzer failed for this population member.",
            ScopeReversionDisposition.Limited => "The bounded local analyzer reached an admitted limit.",
            ScopeReversionDisposition.Unpublishable => "The member is not eligible for publication under the admitted assignment.",
            _ => throw new InvalidOperationException("Unspecified scope-reversion disposition."),
        };

    private static string[] MissingInformation(ScopeReversionTotalityState state)
    {
        List<string> missing = [];
        if (state.Transition == ScopeTransitionKind.Unresolved)
        {
            missing.Add("comparable prior and winning states");
        }
        if (state.Support is ScopeSupportState.Unavailable or ScopeSupportState.NotEvaluated)
        {
            missing.Add("supported purpose claim");
        }
        if (state.Applicability is ScopeApplicabilityState.ConditionalUnestablished
            or ScopeApplicabilityState.Unknown or ScopeApplicabilityState.NotEvaluated)
        {
            missing.Add("purpose applicability decision");
        }
        if (state.PurposeCoverage == ScopeCoverageRelation.Undecidable)
        {
            missing.Add("purpose coverage relation");
        }
        if (state.Contradiction == ScopeContradictionState.Unknown)
        {
            missing.Add("contradiction disposition");
        }
        if (state.CausalClosure == ScopeCausalClosureState.Open)
        {
            missing.Add("closed causal dependency identity");
        }
        if (state.Coverage == CoverageMemberState.CompletedWithGaps
            || state.GapFailure == ScopeGapFailureState.Gap)
        {
            missing.Add("complete contributing population coverage");
        }
        return missing.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static void Promote(
        ScopeReversionMemberContract member,
        OpaqueId runId,
        OpaqueId candidateId,
        OpaqueId hypothesisId,
        IReadOnlyList<OpaqueId> evidence,
        List<ScopeReversionFindingContract> findings,
        List<ScopeReversionCaseContract> cases,
        List<ScopeReversionRecommendationContract> recommendations)
    {
        OpaqueId logicalFindingId = ScopeReversionIdentity.StableId(
            "scope-logical-finding", member.SubjectId.Value, member.FeatureDimension,
            member.CausalClosure.DependencyClosureId.Value);
        OpaqueId findingId = ScopeReversionIdentity.StableId("scope-finding", runId.Value, logicalFindingId.Value);
        OpaqueId logicalCaseId = ScopeReversionIdentity.StableId(
            "scope-logical-case", member.CausalClosure.DependencyClosureId.Value);
        OpaqueId caseId = ScopeReversionIdentity.StableId(
            "scope-case", runId.Value, logicalCaseId.Value, findingId.Value);
        findings.Add(new ScopeReversionFindingContract(
            findingId, candidateId, hypothesisId, member.MemberId,
            member.DomainInterpretation.Explanation,
            FindingSeverity.Moderate,
            AnalysisConfidence.StronglySupported,
            member.DomainInterpretation.Symptom,
            member.DomainInterpretation.BoundedExtent,
            evidence,
            logicalFindingId));
        cases.Add(new ScopeReversionCaseContract(
            caseId, logicalCaseId, findingId, candidateId, hypothesisId,
            member.CausalClosure.DependencyClosureId,
            member.DomainInterpretation.Explanation,
            true,
            evidence));
        recommendations.Add(new ScopeReversionRecommendationContract(
            ScopeReversionIdentity.StableId("scope-recommendation", findingId.Value),
            findingId,
            member.DomainInterpretation.Recommendation,
            "local and reversible by restoring only the affected relationship",
            member.DomainInterpretation.Validation,
            evidence));
    }

    private static void AddTaxonomy(
        ScopeReversionMemberContract member,
        ScopeReversionDisposition disposition,
        IReadOnlyList<OpaqueId> evidence,
        List<ScopeReversionTaxonomyFactContract> taxonomy)
    {
        ScopeTaxonomyApplicability applicability = disposition switch
        {
            ScopeReversionDisposition.SupportedFinding or ScopeReversionDisposition.ResolvedNegative => ScopeTaxonomyApplicability.Applicable,
            ScopeReversionDisposition.Abstained => ScopeTaxonomyApplicability.Unknown,
            _ => ScopeTaxonomyApplicability.Unsupported,
        };
        Add("purpose", member.DomainInterpretation.PurposeTaxonomyCode, applicability, "purpose", "supported purpose classification");
        Add("observed-change", member.DomainInterpretation.ObservedTaxonomyCode, applicability, "observed", "observed transition classification");
        if (disposition == ScopeReversionDisposition.ResolvedNegative)
        {
            Add("consequence", null, ScopeTaxonomyApplicability.NotApplicable, "consequence", "resolved negative has no problem consequence");
            Add("extent", null, ScopeTaxonomyApplicability.NotApplicable, "extent", "resolved negative has no problem effect extent");
        }
        else
        {
            Add("consequence", member.DomainInterpretation.ConsequenceTaxonomyCode, applicability, "consequence", "bounded consequence classification");
            Add("extent", member.DomainInterpretation.ExtentTaxonomyCode, applicability, "extent", "bounded effect-extent classification");
        }

        void Add(string axis, string? code, ScopeTaxonomyApplicability state, string role, string reason) =>
            taxonomy.Add(new ScopeReversionTaxonomyFactContract(
                ScopeReversionIdentity.StableId("scope-taxonomy", member.MemberId.Value, axis),
                member.MemberId, axis, code, state, role, reason, evidence));
    }

    private static ScopeReversionCoverageContract[] BuildCoverage(
        ScopeReversionWorkAssignmentContract assignment,
        IReadOnlyList<ScopeReversionDecisionContract> decisions)
    {
        List<ScopeReversionCoverageContract> result = [];
        foreach (IGrouping<string, ScopeReversionMemberContract> domain in assignment.Members
            .GroupBy(item => item.DomainInterpretation.CoveragePopulationPrefix, StringComparer.Ordinal)
            .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            AddDomain(domain.Key, domain.ToArray());
        }
        AddPopulation("publication-replay", "scope-reversion publication and replay members", assignment.Members);
        return result.OrderBy(item => item.PopulationId, StringComparer.Ordinal).ToArray();

        void AddDomain(string prefix, IReadOnlyCollection<ScopeReversionMemberContract> members)
        {
            AddPopulation(prefix + "-transition", "qualified " + prefix + " members transition", members);
            AddPopulation(prefix + "-purpose-applicability", "qualified " + prefix + " members purpose/applicability", members);
            AddPopulation(prefix + "-conclusion-taxonomy", "qualified " + prefix + " members conclusion/taxonomy", members);
        }

        void AddPopulation(string id, string label, IReadOnlyCollection<ScopeReversionMemberContract> members)
        {
            bool enabled(string adapter) => assignment.Configuration.EnabledAdapterIds.Contains(adapter, StringComparer.Ordinal);
            long Count(CoverageMemberState state) => members.LongCount(member => enabled(member.AdapterId) && EffectiveState(member) == state);
            result.Add(new ScopeReversionCoverageContract(
                id, label, members.Count,
                Count(CoverageMemberState.Completed),
                Count(CoverageMemberState.CompletedWithGaps),
                Count(CoverageMemberState.Failed),
                members.LongCount(member => !enabled(member.AdapterId)),
                Count(CoverageMemberState.SkippedByLimit),
                Count(CoverageMemberState.Unsupported),
                members.Select(item => item.MemberId).OrderBy(item => item.Value, StringComparer.Ordinal).ToArray()));
        }

        CoverageMemberState EffectiveState(ScopeReversionMemberContract member)
        {
            ScopeReversionDisposition? disposition = decisions.SingleOrDefault(item => item.MemberId == member.MemberId)?.Disposition;
            return disposition switch
            {
                ScopeReversionDisposition.Failed => CoverageMemberState.Failed,
                ScopeReversionDisposition.Limited => CoverageMemberState.SkippedByLimit,
                ScopeReversionDisposition.Unsupported => CoverageMemberState.Unsupported,
                ScopeReversionDisposition.Abstained => CoverageMemberState.CompletedWithGaps,
                ScopeReversionDisposition.Unpublishable => CoverageMemberState.CompletedWithGaps,
                _ => member.CoverageState,
            };
        }
    }

    private static string PopulationPrefix(ScopeReversionMemberContract member) =>
        member.DomainInterpretation.CoveragePopulationPrefix;
}
