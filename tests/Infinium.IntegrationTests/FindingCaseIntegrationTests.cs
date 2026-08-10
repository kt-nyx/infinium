using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class FindingCaseIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Cases")]
    public void CasePublicationPersistsExactAggregateOccurrencesCoverageAndCanonicalPayload()
    {
        using CandidateStoreContext context = new();
        CandidateAnalysisPhaseResult candidates = PublishCandidates(context, "run-candidate", promoteLead: false);
        FindingCaseAnalysisPhaseResult phase = FindingCaseAnalysisPhase.Execute(
            context.Store, Input(candidates.Pipeline.Analysis), context.Attempt, context.Binding, DateTimeOffset.UtcNow);

        byte[] readback = context.Store.ReadFindingCasePayload(phase.Receipt.StoredPayloadId);
        CollectionAssert.AreEqual(phase.SerializedPayload, readback);
        Assert.AreEqual(2L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_occurrences"));
        Assert.AreEqual(2L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "case_occurrences"));
        Assert.AreEqual(1L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "analysis_coverage"));
        Assert.AreEqual(3L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_promotion_assessments"));
        Assert.AreEqual(5L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "case_memberships"));
        Assert.AreEqual(3L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "case_hypothesis_memberships"));
        Assert.AreEqual(1L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_case_publications"));
        Assert.AreEqual(1L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_case_abstentions"));
        Assert.AreEqual(2L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_case_finding_details"));
        Assert.AreEqual(3L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_case_recommendations"));
        Assert.AreEqual(2L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_case_case_details"));
        Assert.AreEqual(3L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_case_taxonomy_assignments"));
        Assert.AreEqual(3L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "analysis_coverage_taxonomy_links"));
        Assert.AreEqual(phase.Analysis.PromotionAssessments.Count(item => item.ConclusionAvailable),
            ScalarInt64(context.Paths.Database,
                "SELECT COUNT(*) FROM finding_promotion_assessments WHERE conclusion_available = 1;"));
        Assert.AreEqual(phase.Analysis.PromotionAssessments.Count(item => item.LeadEligibleState),
            ScalarInt64(context.Paths.Database,
                "SELECT COUNT(*) FROM finding_promotion_assessments WHERE lead_eligible_state = 1;"));
        Assert.AreEqual(phase.Analysis.Findings.Count,
            ScalarInt64(context.Paths.Database,
                "SELECT COUNT(*) FROM finding_occurrences WHERE analyzer_version = '1.0.0';"));
        Assert.AreEqual(1L, ScalarInt64(context.Paths.Database,
            "SELECT COUNT(*) FROM analysis_coverage WHERE analyzer_id <> '' AND denominator_label <> '' AND member_results_json <> '[]';"));
        Assert.AreEqual(phase.Analysis.PayloadId, FindingCaseJsonCodec.Deserialize(readback).PayloadId);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Cases")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Cases")]
    [TestProperty("Category", "Fault")]
    public void CasePublicationRejectsStaleAttemptBeforeAnyAggregateRowIsCommitted()
    {
        using CandidateStoreContext context = new();
        CandidateAnalysisPhaseResult candidates = PublishCandidates(context, "run-candidate", promoteLead: false);
        _ = context.StartRecoveryAttempt(DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.ThrowsExactly<InvalidOperationException>(() => FindingCaseAnalysisPhase.Execute(
            context.Store, Input(candidates.Pipeline.Analysis), context.Attempt, context.Binding,
            DateTimeOffset.UtcNow.AddSeconds(2)));

        Assert.AreEqual(0L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_promotion_assessments"));
        Assert.AreEqual(0L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_occurrences"));
        Assert.AreEqual(0L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "case_occurrences"));
        Assert.AreEqual(0L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "analysis_coverage"));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Cases")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Cases")]
    [TestProperty("Category", "Fault")]
    public void CasePublicationRejectsSameIdentityWithDifferentSemanticsAndRollsBackWholeRetry()
    {
        using CandidateStoreContext context = new();
        CandidateAnalysisPhaseResult candidates = PublishCandidates(context, "run-candidate", promoteLead: false);
        FindingCaseInputContract baselineInput = Input(candidates.Pipeline.Analysis);
        FindingCaseAnalysisPhaseResult baseline = FindingCaseAnalysisPhase.Execute(
            context.Store, baselineInput, context.Attempt, context.Binding, DateTimeOffset.UtcNow);
        FindingEvidenceFactContract alpha = baselineInput.FindingEvidenceFacts.Single(item =>
            item.AffectedLocus == "locus-alpha");
        FindingCaseInputContract changed = baselineInput with
        {
            FindingEvidenceFacts = baselineInput.FindingEvidenceFacts
                .Select(item => item == alpha
                    ? item with { WorstCredibleConsequence = WorstCredibleConsequence.ImportantRequirementFailure }
                    : item)
                .ToArray(),
        };
        changed = changed with { InputId = FindingCaseIdentity.ComputeInputId(changed) };

        Assert.ThrowsExactly<InvalidDataException>(() => FindingCaseAnalysisPhase.Execute(
            context.Store, changed, context.Attempt, context.Binding, DateTimeOffset.UtcNow.AddSeconds(1)));

        Assert.AreEqual(baseline.Analysis.Findings.Count,
            CandidatePipelineIntegrationTests.Count(context.Paths.Database, "finding_occurrences"));
        Assert.AreEqual(baseline.Analysis.Cases.Count,
            CandidatePipelineIntegrationTests.Count(context.Paths.Database, "case_occurrences"));
        Assert.AreEqual(1L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "analysis_coverage"));
        Assert.AreEqual(baseline.Analysis.PayloadId, FindingCaseJsonCodec.Deserialize(
            context.Store.ReadFindingCasePayload(baseline.Receipt.StoredPayloadId)).PayloadId);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Cases")]
    public void LineageReconciliationPromotesLeadBySuccessorWithoutRelabelOrReviewCarryover()
    {
        using CandidateStoreContext context = new();
        CandidateAnalysisPhaseResult firstCandidates = PublishCandidates(context, "run-candidate", promoteLead: false);
        FindingCaseAnalysisPhaseResult first = FindingCaseAnalysisPhase.Execute(
            context.Store, Input(firstCandidates.Pipeline.Analysis), context.Attempt, context.Binding, DateTimeOffset.UtcNow);
        AnalysisCaseContract priorLead = first.Analysis.Cases.Single(item => item.Kind == CaseOccurrenceKind.LeadOnly);

        const string successorRun = "run-cases-successor";
        AttemptRecord successorAttempt = context.CreateRunAttempt(successorRun, DateTimeOffset.UtcNow.AddSeconds(1));
        CandidateAnalysisPhaseResult successorCandidates = CandidateAnalysisPhase.Execute(
            context.Store, Request(successorRun, promoteLead: true), successorAttempt, context.Binding, DateTimeOffset.UtcNow.AddSeconds(2));
        FindingCaseInputContract successorInput = Input(
            successorCandidates.Pipeline.Analysis,
            first.Analysis.Findings.Select(PriorFinding).ToArray(),
            first.Analysis.Cases.Select(PriorCase).ToArray(),
            promoteLead: true);
        FindingCaseAnalysisPhaseResult successor = FindingCaseAnalysisPhase.Execute(
            context.Store, successorInput, successorAttempt, context.Binding, DateTimeOffset.UtcNow.AddSeconds(3));

        OccurrenceLineageContract promotion = successor.Analysis.LineageEvents.Single(item => item.Kind == LineageKind.PromotesLead);
        Assert.IsNull(promotion.ReconciliationAssessmentId);
        AnalysisCaseContract supportedSuccessor = successor.Analysis.Cases.Single(item =>
            item.SupersedesOccurrenceId == priorLead.CaseOccurrenceId);
        Assert.AreNotEqual(priorLead.LogicalCaseId, supportedSuccessor.LogicalCaseId);
        Assert.AreEqual(priorLead.CaseOccurrenceId, promotion.PredecessorIds.Single());
        Assert.AreEqual(supportedSuccessor.CaseOccurrenceId, promotion.SuccessorIds.Single());
        Assert.IsNull(typeof(AnalysisCaseContract).GetProperty("ReviewState"));
        Assert.AreEqual(
            successor.Analysis.LineageEvents.Count,
            CandidatePipelineIntegrationTests.Count(context.Paths.Database, "lineage_events"));
    }

    private static CandidateAnalysisPhaseResult PublishCandidates(
        CandidateStoreContext context, string runId, bool promoteLead) =>
        CandidateAnalysisPhase.Execute(
            context.Store, Request(runId, promoteLead), context.Attempt,
            context.Binding, DateTimeOffset.UtcNow);

    private static long ScalarInt64(string database, string sql)
    {
        using SqliteConnection connection = new($"Data Source={database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static CandidatePipelineRequest Request(string runId, bool promoteLead)
    {
        CausalJoinPopulationMember lead = CandidatePipelineIntegrationTests.Member(
            "lead", inputState: promoteLead ? CausalJoinInputState.Complete : CausalJoinInputState.Ambiguous);
        if (!promoteLead)
        {
            lead = lead with { ContradictingEvidenceIds = [CandidatePipelineIntegrationTests.Id("contradiction-lead")] };
        }
        return CandidatePipelineIntegrationTests.Request(
            [CandidatePipelineIntegrationTests.Member("alpha"), CandidatePipelineIntegrationTests.Member("beta"), lead],
            runId, "population-cases");
    }

    internal static FindingCaseInputContract Input(
        CandidateAnalysisContract candidates,
        IReadOnlyList<PriorFindingContract>? priorFindings = null,
        IReadOnlyList<PriorCaseContract>? priorCases = null,
        bool promoteLead = false)
    {
        Dictionary<OpaqueId, string> suffixes = candidates.Decisions.ToDictionary(
            item => item.DecisionId,
            item => item.PopulationMemberId.Value.Replace("member-", string.Empty, StringComparison.Ordinal));
        List<FindingEvidenceFactContract> facts = [];
        Dictionary<string, CandidateHypothesisContract> hypotheses = [];
        foreach (CandidateHypothesisContract hypothesis in candidates.Hypotheses)
        {
            string suffix = suffixes[candidates.Candidates.Single(item => item.CandidateId == hypothesis.CandidateId).DecisionId];
            hypotheses.Add(suffix, hypothesis);
            facts.Add(new FindingEvidenceFactContract(
                CandidatePipelineIntegrationTests.Id("finding-fact-" + suffix), hypothesis.HypothesisId,
                WorstCredibleConsequence.MeaningfulBoundedLoss, "locus-" + suffix,
                suffix is "alpha" or "beta" ? "shared-cause" : "typed-causal-join",
                ["profile-selected", "applicable-" + suffix],
                suffix == "lead" && !promoteLead ? [CandidatePipelineIntegrationTests.Id("contradiction-lead")] : [],
                [],
                hypothesis.SupportingEvidenceIds));
        }
        List<SharedCauseProofContract> proofs =
        [
            new(CandidatePipelineIntegrationTests.Id("cause-proof-shared"),
                [hypotheses["alpha"].HypothesisId, hypotheses["beta"].HypothesisId],
                candidates.AnalyzerId.Value,
                candidates.AnalyzerBindings.Single().SemanticContractVersion,
                candidates.AnalyzerBindings.Single().IdentityContractVersion,
                candidates.Decisions.Where(item => item.PopulationMemberId.Value is "member-alpha" or "member-beta")
                    .SelectMany(item => item.Participants).ToDictionary(
                        item => item.ParticipantId.Value, item => item.Role, StringComparer.Ordinal),
                "shared-cause", "locus-alpha|locus-beta",
                ["profile-selected", "applicable-alpha", "applicable-beta"],
                FindingCaseIdentity.SharedCauseDependencyClosureId(candidates.Decisions
                    .Where(item => item.PopulationMemberId.Value is "member-alpha" or "member-beta")
                    .SelectMany(item => item.DependencyIds)),
                hypotheses["alpha"].SupportingEvidenceIds
                    .Concat(hypotheses["beta"].SupportingEvidenceIds).Distinct().ToArray()),
        ];
        if (promoteLead)
        {
            CandidateHypothesisContract lead = hypotheses["lead"];
            CandidateAnalysisEntryContract candidate = candidates.Candidates.Single(item => item.CandidateId == lead.CandidateId);
            CandidateDecisionContract decision = candidates.Decisions.Single(item => item.DecisionId == candidate.DecisionId);
            CandidateAnalyzerBindingContract binding = candidates.AnalyzerBindings.Single(item => item.AnalyzerId == decision.AnalyzerId);
            proofs.Add(new SharedCauseProofContract(
                CandidatePipelineIntegrationTests.Id("cause-proof-lead"), [lead.HypothesisId], decision.AnalyzerId.Value,
                 binding.SemanticContractVersion, binding.IdentityContractVersion,
                 decision.Participants.ToDictionary(item => item.ParticipantId.Value, item => item.Role, StringComparer.Ordinal),
                 decision.JoinKind, "locus-lead", ["profile-selected", "applicable-lead"],
                 FindingCaseIdentity.SharedCauseDependencyClosureId(decision.DependencyIds),
                 lead.SupportingEvidenceIds));
        }
        TaxonomyClassificationFactContract[] taxonomyFacts = hypotheses
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new TaxonomyClassificationFactContract(
                CandidatePipelineIntegrationTests.Id("taxonomy-fact-" + item.Key), item.Value.HypothesisId,
                "infinium.test.taxonomy", Version(), "impact", "effect", "bounded-effect",
                TaxonomyApplicability.Assigned, ClassificationRole.Established,
                [CandidatePipelineIntegrationTests.Id("taxonomy-evidence-" + item.Key)],
                [CandidatePipelineIntegrationTests.Id("taxonomy-scope")], null,
                CandidatePipelineIntegrationTests.Id("taxonomy-author"),
                new UtcTimestamp(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)),
                "Synthetic persisted taxonomy fact."))
            .ToArray();
        FindingCaseInputContract input = new(
            ContractConstants.FindingCaseInputSchemaId, Version(), CandidatePipelineIntegrationTests.Id("pending"),
            candidates.OriginatingRunId, CandidatePipelineIntegrationTests.Id("promotion-policy-finding_case"), Version(),
            CandidatePipelineIntegrationTests.Id("reconciliation-policy-finding_case"), Version(),
            CandidatePipelineIntegrationTests.Id("reconciliation-actor-finding_case"),
            new UtcTimestamp(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)),
            candidates, facts, RecommendationFacts(facts), proofs, taxonomyFacts, [],
            [new CoveragePopulationFactContract(CandidatePipelineIntegrationTests.Id("coverage-population"), candidates.AnalyzerId,
                "candidate-hypotheses", "admitted hypotheses")],
            candidates.Hypotheses.Select(item => new CoverageMemberFactContract(
                CandidatePipelineIntegrationTests.Id("coverage-member-" + item.HypothesisId.Value), candidates.AnalyzerId,
                "candidate-hypotheses", "admitted hypotheses", item.HypothesisId, CoverageMemberState.Completed,
                "completed", "none", null,
                [taxonomyFacts.Single(fact => fact.HypothesisId == item.HypothesisId).FactId])).ToArray(),
            [], priorFindings ?? [], priorCases ?? [], [], [],
            [
                new("provider", BoundaryUseState.NotUsed, "local deterministic analysis"),
                new("hosted-search", BoundaryUseState.NotUsed, "local deterministic analysis"),
                new("nexus", BoundaryUseState.NotUsed, "local deterministic analysis"),
                new("loot", BoundaryUseState.NotUsed, "not configured"),
            ]);
        if (priorFindings is { Count: > 0 })
        {
            input = input with
            {
                ReconciliationCandidateFacts = candidates.Hypotheses.Select(hypothesis =>
                    new ReconciliationCandidateFactContract(
                        CandidatePipelineIntegrationTests.Id("candidate-scope-" + hypothesis.HypothesisId.Value),
                        hypothesis.HypothesisId,
                        priorFindings.Select(item => item.FindingOccurrenceId).ToArray())).ToArray(),
            };
        }
        return input with { InputId = FindingCaseIdentity.ComputeInputId(input) };
    }

    internal static PriorFindingContract PriorFinding(FindingContract value) => new(
        value.FindingOccurrenceId, value.LogicalFindingId, value.OriginatingRunId,
        value.CandidateId, value.HypothesisId, value.IdentityEnvelope, value.SemanticFingerprint, true,
        ["candidate-hypotheses"]);

    internal static PriorCaseContract PriorCase(AnalysisCaseContract value) => new(
        value.CaseOccurrenceId, value.LogicalCaseId, value.OriginatingRunId, value.Kind,
        value.FindingOccurrenceIds, value.HypothesisIds, value.IdentityEnvelope, value.SemanticFingerprint, true,
        ["candidate-hypotheses"]);

    private static IdentityEnvelopeContract Identity(string cause, string locus, string dependency, string analyzer)
    {
        IdentityEnvelopeContract value = new(
            analyzer, Version(), Version(),
            new Dictionary<string, string> { ["provider"] = "source", ["consumer"] = "target" },
            cause, locus, ["profile-selected"], CandidatePipelineIntegrationTests.Id(dependency),
            new Sha256Fingerprint(new string('0', 64)));
        return value with { CanonicalSignature = FindingCaseIdentity.ComputeIdentitySignature(value) };
    }

    private static FindingRecommendationFactContract[] RecommendationFacts(
        IReadOnlyList<FindingEvidenceFactContract> facts) => facts.Select(item => new FindingRecommendationFactContract(
            CandidatePipelineIntegrationTests.Id("recommendation-fact-" + item.FactId.Value), item.HypothesisId,
            RecommendationKind.Validation, "Validate the typed causal condition.",
            "Bounded to supplied typed evidence.", "No installed state is changed by analysis.",
            ["Applicability must remain valid."], "Reobserve the affected locus.", item.EvidenceIds)).ToArray();

    private static ContractVersion Version() => new(1, 0, 0);
}
