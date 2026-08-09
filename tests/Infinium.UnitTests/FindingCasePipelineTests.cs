using Infinium.Analysis.Candidates;
using Infinium.Analysis.Cases;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class FindingCasePipelineTests
{
    [TestMethod]
    [TestCategory("M1Unit"), TestCategory("M1Cases")]
    [TestProperty("Category", "M1Unit"), TestProperty("Category", "M1Cases")]
    public void FindingPromotionDerivesConclusionsAndKeepsDefeatedHypothesisAsLead()
    {
        FindingCaseContract result = FindingCasePipeline.Execute(CreateInput("run-cases-a"));

        Assert.AreEqual(3, result.PromotionAssessments.Count);
        Assert.AreEqual(2, result.Findings.Count);
        Assert.AreEqual(1, result.Abstentions.Count);
        Assert.AreEqual(1, result.PromotionAssessments.Count(item => item.Outcome == FindingPromotionOutcome.LeadOnly));
        Assert.AreEqual(3, result.Recommendations.Count);
        Assert.IsTrue(result.Recommendations.Any(item => item.LeadHypothesisId is null
            && item.Kind == RecommendationKind.FurtherInvestigation && item.AbstentionId is not null));
        Assert.IsTrue(result.Cases.All(item => !item.AffectsReadiness));
        Assert.AreEqual("no-safety-claim", result.PublicationClaimBoundary);
    }

    [TestMethod]
    [TestCategory("M1Unit"), TestCategory("M1Cases")]
    [TestProperty("Category", "M1Unit"), TestProperty("Category", "M1Cases")]
    public void FindingPromotionRetainsNonDefeatingContradictionWithoutFalseNegative()
    {
        FindingCaseInputContract input = CreateInput("run-nondefeating");
        CandidateHypothesisContract target = input.CandidateAnalysis.Hypotheses.Single(item =>
            item.ContradictingEvidenceIds.Contains(Id("contradiction-lead")));
        FindingEvidenceFactContract fact = input.FindingEvidenceFacts.Single(item => item.HypothesisId == target.HypothesisId);
        input = Reidentify(input with
        {
            FindingEvidenceFacts = input.FindingEvidenceFacts.Select(item => item.FactId == fact.FactId
                ? item with
                {
                    DefeatingContradictionIds = [],
                    RetainedNonDefeatingContradictionIds = [Id("contradiction-lead")],
                } : item).ToArray(),
        });
        FindingCaseContract result = FindingCasePipeline.Execute(input);
        Assert.AreEqual(2, result.Findings.Count);
        FindingPromotionAssessmentContract assessment = result.PromotionAssessments.Single(
            item => item.HypothesisId == target.HypothesisId);
        Assert.IsTrue(assessment.HasNoDefeatingContradictions);
        Assert.IsTrue(assessment.Reasons.All(reason => !reason.Contains("contradiction", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [TestCategory("M1Unit"), TestCategory("M1Cases")]
    [TestProperty("Category", "M1Unit"), TestProperty("Category", "M1Cases")]
    public void CaseGroupingUsesIndependentTypedProofAndIgnoresOrderNamesAndTaxonomyRouting()
    {
        FindingCaseContract baseline = FindingCasePipeline.Execute(CreateInput("run-cases-a"));
        FindingCaseContract permuted = FindingCasePipeline.Execute(CreateInput("run-cases-a", reverse: true));

        Slice5CaseContract supported = baseline.Cases.Single(item => item.Kind == CaseOccurrenceKind.Supported);
        Assert.AreEqual(2, supported.FindingOccurrenceIds.Count);
        Assert.AreEqual(1, baseline.Cases.Count(item => item.Kind == CaseOccurrenceKind.Supported));
        CollectionAssert.AreEquivalent(
            baseline.Cases.Select(item => item.SemanticFingerprint.Value).ToArray(),
            permuted.Cases.Select(item => item.SemanticFingerprint.Value).ToArray());
        Assert.AreEqual(baseline.PayloadId, permuted.PayloadId);

        FindingCaseInputContract split = CreateInput("run-cases-a");
        SharedCauseProofContract shared = split.SharedCauseProofs.Single();
        OpaqueId second = shared.HypothesisIds[1];
        SharedCauseProofContract separate = Proof("distinct", [second], split.CandidateAnalysis, split.FindingEvidenceFacts) with
        {
            CausalCondition = "typed-distinct-cause",
        };
        OpaqueId first = shared.HypothesisIds[0];
        split = Reidentify(split with
        {
            FindingEvidenceFacts = split.FindingEvidenceFacts.Select(item => item.HypothesisId == second
                ? item with { CausalCondition = "typed-distinct-cause" } : item).ToArray(),
            SharedCauseProofs =
            [
                Proof("shared", [first], split.CandidateAnalysis, split.FindingEvidenceFacts),
                separate,
            ],
        });
        Assert.AreEqual(2, FindingCasePipeline.Execute(split).Cases.Count(item => item.Kind == CaseOccurrenceKind.Supported));
    }

    [TestMethod]
    [TestCategory("M1Unit"), TestCategory("M1Cases")]
    [TestProperty("Category", "M1Unit"), TestProperty("Category", "M1Cases")]
    public void LineageReconciliationEmitsAllEightOutcomesAndDoesNotReuseOnePrior()
    {
        FindingCaseContract baseline = FindingCasePipeline.Execute(CreateInput("run-history"));
        PriorFindingContract alpha = PriorFindings(baseline).OrderBy(item => item.FindingOccurrenceId.Value, StringComparer.Ordinal).First();
        AssertOutcome(alpha, ReconciliationOutcome.ExactContinuation);
        AssertOutcome(alpha with { SemanticFingerprint = new Sha256Fingerprint(new string('b', 64)) }, ReconciliationOutcome.AnalyticalRevision);
        AssertOutcome(alpha with { IdentityEnvelope = ReidentifyEnvelope(alpha.IdentityEnvelope with { DependencyClosureId = Id("related-dependency") }) },
            ReconciliationOutcome.RelatedFollowUp);

        PriorFindingContract distinctPrior = alpha with
        {
            IdentityEnvelope = ReidentifyEnvelope(alpha.IdentityEnvelope with { CausalCondition = "typed-distinct-prior" }),
        };
        FindingCaseContract distinct = FindingCasePipeline.Execute(CreateInput("run-current", priorFindings: [distinctPrior]));
        Assert.IsTrue(distinct.ReconciliationAssessments.Any(item => item.Outcome == ReconciliationOutcome.NewDistinct));
        Assert.IsFalse(distinct.ReconciliationAssessments.Any(item => item.Outcome == ReconciliationOutcome.NotObserved));

        PriorFindingContract duplicate = alpha with
        {
            FindingOccurrenceId = Id("prior-duplicate"),
            LogicalFindingId = Id("logical-duplicate"),
        };
        FindingCaseContract ambiguous = FindingCasePipeline.Execute(CreateInput("run-current", priorFindings: [alpha, duplicate]));
        Assert.IsTrue(ambiguous.ReconciliationAssessments.Any(item => item.Outcome == ReconciliationOutcome.Ambiguous));
        Assert.IsFalse(ambiguous.ReconciliationAssessments.Any(item => item.Outcome == ReconciliationOutcome.NotObserved
            && (item.PriorOccurrenceId == alpha.FindingOccurrenceId || item.PriorOccurrenceId == duplicate.FindingOccurrenceId)));

        FindingCaseContract unknown = FindingCasePipeline.Execute(CreateInput("run-current",
            priorFindings: [alpha with { ProofAvailable = false }]));
        Assert.IsTrue(unknown.ReconciliationAssessments.Any(item => item.Outcome == ReconciliationOutcome.Unknown));

        FindingCaseInputContract incomplete = CreateInput("run-current", priorFindings: [distinctPrior]);
        incomplete = Reidentify(incomplete with
        {
            ReconciliationCandidateFacts = incomplete.ReconciliationCandidateFacts.Select(item =>
                item with { PriorOccurrenceIds = [] }).ToArray(),
            CoverageMemberFacts = incomplete.CoverageMemberFacts.Select(item => item with
            {
                State = CoverageMemberState.SkippedByLimit,
                Reason = "bounded limit",
                MissingCapabilityOrInformation = "member not evaluated",
            }).ToArray(),
        });
        Assert.IsTrue(FindingCasePipeline.Execute(incomplete).ReconciliationAssessments
            .Any(item => item.Outcome == ReconciliationOutcome.NotEvaluated));

        CollectionAssert.AreEquivalent(
            Enum.GetValues<ReconciliationOutcome>().Where(item => item != ReconciliationOutcome.Unspecified)
                .Select(item => item.ToString()).ToArray(),
            new[] { ReconciliationOutcome.ExactContinuation, ReconciliationOutcome.AnalyticalRevision,
                ReconciliationOutcome.RelatedFollowUp, ReconciliationOutcome.NewDistinct,
                ReconciliationOutcome.Ambiguous, ReconciliationOutcome.Unknown,
                ReconciliationOutcome.NotObserved, ReconciliationOutcome.NotEvaluated }
                .Select(item => item.ToString()).ToArray());
        return;

        void AssertOutcome(PriorFindingContract prior, ReconciliationOutcome expected)
        {
            FindingCaseInputContract input = CreateInput("run-current", priorFindings: [prior]);
            if (expected == ReconciliationOutcome.RelatedFollowUp)
            {
                ReconciliationCandidateFactContract scope = input.ReconciliationCandidateFacts.Single(item =>
                    input.FindingEvidenceFacts.Single(fact => fact.HypothesisId == item.CurrentHypothesisId).AffectedLocus == "locus-alpha");
                CandidateHypothesisContract hypothesis = input.CandidateAnalysis.Hypotheses.Single(item =>
                    item.HypothesisId == scope.CurrentHypothesisId);
                input = Reidentify(input with
                {
                    RelatedFindingFacts =
                    [
                        new RelatedFindingFactContract(Id("related-fact-alpha"), hypothesis.HypothesisId,
                            prior.FindingOccurrenceId, hypothesis.SupportingEvidenceIds, "Explicit downstream relation."),
                    ],
                });
            }
            FindingCaseContract result = FindingCasePipeline.Execute(input);
            Assert.IsTrue(result.ReconciliationAssessments.Any(item => item.Outcome == expected));
        }
    }

    [TestMethod]
    [TestCategory("M1Unit"), TestCategory("M1Cases")]
    [TestProperty("Category", "M1Unit"), TestProperty("Category", "M1Cases")]
    public void TaxonomyHistoryPreservesAllMergeSourcesAndDoesNotMutateProductAssignments()
    {
        FindingCaseInputContract input = CreateInput("run-taxonomy");
        OpaqueId hypothesis = input.CandidateAnalysis.Hypotheses[0].HypothesisId;
        UtcTimestamp created = new(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero));
        TaxonomyClassificationFactContract[] facts =
        [
            TaxonomyFact("motion", "test.area.motion", hypothesis, created),
            TaxonomyFact("disk", "test.delivery.disk", hypothesis, created),
            TaxonomyFact("stream", "test.delivery.stream", hypothesis, created),
        ];
        TaxonomyProjectionInputContract[] projections =
        [
            Projection(facts[0], "test.area.motion.linear", "map-split-motion"),
            Projection(facts[0], "test.area.motion.angular", "map-split-motion"),
            Projection(facts[1], "test.delivery.external", "map-merge-delivery"),
            Projection(facts[2], "test.delivery.external", "map-merge-delivery"),
        ];
        input = Reidentify(input with { TaxonomyClassificationFacts = facts, TaxonomyProjectionInputs = projections });
        FindingCaseContract result = FindingCasePipeline.Execute(input);

        Assert.AreEqual(6, result.TaxonomyAssignments.Count);
        Assert.AreEqual(4, result.TaxonomyProjections.Count);
        TaxonomyAssignmentContract merged = result.TaxonomyAssignments.Single(item => item.Code == "test.delivery.external");
        Assert.IsTrue(merged.EvidenceIds.Any(item => item.Value == "evidence-disk"));
        Assert.IsTrue(merged.EvidenceIds.Any(item => item.Value == "evidence-stream"));
        Assert.AreEqual(2, result.TaxonomyProjections.Count(item => item.ProjectedAssignmentId == merged.AssignmentId));
        Assert.IsTrue(result.TaxonomyAssignments.Where(item => item.TaxonomyVersion == new ContractVersion(1, 0, 0))
            .All(item => item.SupersedesAssignmentIds.Count == 0));
        Assert.IsTrue(result.TaxonomyAssignments.Where(item => item.TaxonomyVersion == new ContractVersion(2, 0, 0))
            .All(item => item.SupersedesAssignmentIds.Count > 0));
    }

    internal static FindingCaseInputContract CreateInput(
        string runId, bool reverse = false, IReadOnlyList<PriorFindingContract>? priorFindings = null,
        IReadOnlyList<PriorCaseContract>? priorCases = null, bool promoteLead = false)
    {
        CausalJoinPopulationMember[] members =
        [
            Member("alpha", CausalJoinInputState.Complete),
            Member("beta", CausalJoinInputState.Complete),
            Member("lead", promoteLead ? CausalJoinInputState.Complete : CausalJoinInputState.Ambiguous,
                promoteLead ? [] : ["applicability proof"], [Id("contradiction-lead")]),
        ];
        if (reverse)
        {
            Array.Reverse(members);
        }
        CandidateAnalysisContract candidates = CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id(runId), Id("population-cases"), Id("candidate-policy"), Id("candidate-threshold"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [new Source(Id("analyzer-cases"), members)])).Analysis;
        Dictionary<string, CandidateHypothesisContract> hypotheses = candidates.Hypotheses.ToDictionary(
            hypothesis => Suffix(hypothesis, candidates, members), StringComparer.Ordinal);
        FindingEvidenceFactContract[] evidence = hypotheses.Select(item => new FindingEvidenceFactContract(
            Id("finding-fact-" + item.Key), item.Value.HypothesisId, WorstCredibleConsequence.MeaningfulBoundedLoss,
            "locus-" + item.Key, item.Key is "alpha" or "beta" ? "condition-shared" : "condition-lead",
            ["profile-selected", "applicable-" + item.Key],
            item.Key == "lead" && !promoteLead ? [Id("contradiction-lead")] : [],
            [],
            item.Value.SupportingEvidenceIds)).ToArray();
        List<SharedCauseProofContract> proofs =
        [
            Proof("shared", [hypotheses["alpha"].HypothesisId, hypotheses["beta"].HypothesisId], candidates, evidence),
        ];
        if (promoteLead)
        {
            proofs.Add(Proof("lead", [hypotheses["lead"].HypothesisId], candidates, evidence));
        }
        FindingCaseInputContract input = new(
            ContractConstants.FindingCaseInputSchemaId, Version(), Id("pending"), Id(runId),
            Id("promotion-policy-wp4"), Version(), Id("reconciliation-policy-wp4"), Version(),
            Id("reconciliation-actor-wp4"),
            new UtcTimestamp(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)), candidates,
            evidence, RecommendationFacts(evidence), proofs, [], [],
            [new CoveragePopulationFactContract(Id("coverage-population-fact"), Id("analyzer-cases"), "population-cases", "generic members")],
            members.Select(item => new CoverageMemberFactContract(
                Id("coverage-member-" + item.PopulationMemberId.Value), Id("analyzer-cases"), "population-cases",
            "generic members", item.PopulationMemberId, CoverageMemberState.Completed,
                "member completed", "none", null, [])).ToArray(), [], priorFindings ?? [], priorCases ?? [], [], [], Boundaries());
        if (priorFindings is { Count: > 0 })
        {
            input = input with
            {
                ReconciliationCandidateFacts = candidates.Hypotheses.Select(hypothesis =>
                    new ReconciliationCandidateFactContract(Id("candidate-scope-" + hypothesis.HypothesisId.Value),
                        hypothesis.HypothesisId, priorFindings.Select(item => item.FindingOccurrenceId).ToArray())).ToArray(),
            };
        }
        return Reidentify(input);
    }

    internal static FindingCaseInputContract Reidentify(FindingCaseInputContract input) =>
        input with { InputId = FindingCaseIdentity.ComputeInputId(input) };

    internal static IReadOnlyList<FindingRecommendationFactContract> RecommendationFacts(
        IReadOnlyList<FindingEvidenceFactContract> facts) => facts.Select(item => new FindingRecommendationFactContract(
            Id("recommendation-fact-" + item.FactId.Value), item.HypothesisId, RecommendationKind.Validation,
            "Validate the typed causal condition.", "Bounded to supplied typed evidence.",
            "No installed state is changed by analysis.", ["Applicability must remain valid."],
            "Reobserve the affected locus.", item.EvidenceIds)).ToArray();

    internal static IReadOnlyList<PriorFindingContract> PriorFindings(FindingCaseContract value) =>
        value.Findings.Select(item => new PriorFindingContract(
            item.FindingOccurrenceId, item.LogicalFindingId, item.OriginatingRunId,
            item.CandidateId, item.HypothesisId, item.IdentityEnvelope, item.SemanticFingerprint, true,
            ["population-cases"])).ToArray();

    internal static IReadOnlyList<PriorCaseContract> PriorCases(FindingCaseContract value) =>
        value.Cases.Select(item => new PriorCaseContract(
            item.CaseOccurrenceId, item.LogicalCaseId, item.OriginatingRunId, item.Kind,
            item.FindingOccurrenceIds, item.HypothesisIds, item.IdentityEnvelope, item.SemanticFingerprint, true,
            ["population-cases"])).ToArray();

    internal static IdentityEnvelopeContract ReidentifyEnvelope(IdentityEnvelopeContract value) =>
        value with { CanonicalSignature = FindingCaseIdentity.ComputeIdentitySignature(value) };

    internal static IReadOnlyList<ExecutionBoundaryContract> Boundaries() =>
    [
        new("provider", BoundaryUseState.NotUsed, "local deterministic analysis"),
        new("hosted-search", BoundaryUseState.NotUsed, "local deterministic analysis"),
        new("nexus", BoundaryUseState.NotUsed, "local deterministic analysis"),
        new("loot", BoundaryUseState.NotUsed, "not configured"),
    ];

    private static SharedCauseProofContract Proof(
        string suffix,
        IReadOnlyList<OpaqueId> hypothesisIds,
        CandidateAnalysisContract candidates,
        IReadOnlyList<FindingEvidenceFactContract> evidenceFacts)
    {
        CandidateDecisionContract[] decisions = hypothesisIds.Select(hypothesisId =>
        {
            CandidateHypothesisContract hypothesis = candidates.Hypotheses.Single(item => item.HypothesisId == hypothesisId);
            CandidateAnalysisEntryContract candidate = candidates.Candidates.Single(item => item.CandidateId == hypothesis.CandidateId);
            return candidates.Decisions.Single(item => item.DecisionId == candidate.DecisionId);
        }).ToArray();
        CandidateAnalyzerBindingContract binding = candidates.AnalyzerBindings.Single(item => item.AnalyzerId == decisions[0].AnalyzerId);
        return new SharedCauseProofContract(
            Id("cause-proof-" + suffix), hypothesisIds, decisions[0].AnalyzerId.Value,
            binding.SemanticContractVersion, binding.IdentityContractVersion,
            decisions.SelectMany(item => item.Participants).ToDictionary(
                item => item.ParticipantId.Value, item => item.Role, StringComparer.Ordinal),
            "condition-" + suffix,
            string.Join("|", evidenceFacts.Where(item => hypothesisIds.Contains(item.HypothesisId))
                .Select(item => item.AffectedLocus).Order(StringComparer.Ordinal)),
            evidenceFacts.Where(item => hypothesisIds.Contains(item.HypothesisId))
                .SelectMany(item => item.ApplicabilityPredicates).Distinct(StringComparer.Ordinal).ToArray(),
            FindingCaseIdentity.SharedCauseDependencyClosureId(
                decisions.SelectMany(item => item.DependencyIds)),
            decisions.SelectMany(item => candidates.Hypotheses.Single(hypothesis =>
                candidates.Candidates.Single(candidate => candidate.CandidateId == hypothesis.CandidateId).DecisionId == item.DecisionId)
                .SupportingEvidenceIds).Distinct().ToArray());
    }

    private static TaxonomyClassificationFactContract TaxonomyFact(
        string id, string code, OpaqueId hypothesis, UtcTimestamp created) => new(
        Id("taxonomy-fact-" + id), hypothesis, "infinium.test.taxonomy", Version(), "test-axis", "test-facet", code,
        TaxonomyApplicability.Assigned, ClassificationRole.Established, [Id("evidence-" + id)], [Id("taxonomy-scope")],
        null, Id("taxonomy-author"), created, "Synthetic non-product taxonomy fact.");

    private static TaxonomyProjectionInputContract Projection(
        TaxonomyClassificationFactContract source, string code, string authority) => new(
        source.FactId, "infinium.test.taxonomy", new ContractVersion(2, 0, 0), "test-axis", "test-facet", code,
        TaxonomyApplicability.Assigned, Id(authority), [Id("map-evidence-" + source.FactId.Value)],
        "Synthetic non-product taxonomy mapping.");

    private static string Suffix(CandidateHypothesisContract hypothesis, CandidateAnalysisContract candidates,
        IReadOnlyList<CausalJoinPopulationMember> members) => members.Single(item =>
            candidates.Candidates.Single(candidate => candidate.CandidateId == hypothesis.CandidateId).DecisionId
            == candidates.Decisions.Single(decision => decision.PopulationMemberId == item.PopulationMemberId).DecisionId)
            .PopulationMemberId.Value.Replace("member-", string.Empty, StringComparison.Ordinal);

    private static CausalJoinPopulationMember Member(string suffix, CausalJoinInputState state,
        IReadOnlyList<string>? missing = null, IReadOnlyList<OpaqueId>? contradictions = null) => new(
        Id("member-" + suffix), Id("analyzer-cases"), CandidateLane.MandatoryEvidence,
        [new(Id("provider-" + suffix), "source"), new(Id("consumer-" + suffix), "target")],
        "typed-causal-join", [Id("provider-" + suffix), Id("consumer-" + suffix)],
        [Id("dependency-" + suffix)], [Id("evidence-" + suffix)], contradictions ?? [], missing ?? [], state,
        "A typed causal relationship is admitted.", "A bounded effect may be present.")
        { SourceFactId = Id("fact-" + suffix) };

    private static ContractVersion Version() => new(1, 0, 0);
    internal static OpaqueId Id(string value) => new(value);

    private sealed class Source(OpaqueId analyzerId, IReadOnlyList<CausalJoinPopulationMember> members)
        : ICandidatePopulationSource
    {
        public OpaqueId AnalyzerId => analyzerId;
        public AnalyzerDeclarationContract Declaration { get; } = CandidateAnalyzerDeclarations.Create(
            analyzerId, Math.Max(1, members.Count), 1_000_000,
            supportedShapes: members.Select(item => item.JoinKind).Distinct(StringComparer.Ordinal).ToArray());
        public IReadOnlyList<CausalJoinPopulationMember> DeclarePopulation(
            CandidatePopulationContext context, CancellationToken cancellationToken = default) => members;
        public IReadOnlyList<CausalJoinPopulationMember> ConstructPopulation(
            CandidatePopulationContext context, CancellationToken cancellationToken = default) => members;
    }
}
