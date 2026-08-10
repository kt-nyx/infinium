using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.Conclusions;
using Infinium.Analysis.FindingCases;
using Infinium.Application.FindingCases;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

internal static partial class FindingCaseFixtureProductAdapter
{
    public static LeadPromotionExecution ExecuteLeadPromotion(JsonElement factual)
    {
        JsonElement priorFact = factual.GetProperty("prior_run_fact");
        JsonElement currentFact = factual.GetProperty("current_hypothesis");
        const string analyzer = "generic-reconciliation-analyzer";
        ContractVersion version = new(1, 0, 2);

        (CandidateAnalysisContract Candidates, FindingEvidenceFactContract Fact, SharedCauseProofContract Proof) Build(
            JsonElement fact, string runId, bool lead)
        {
            string hypothesisId = Text(fact, "hypothesis_id");
            OpaqueId[] evidence = fact.GetProperty("supporting_evidence_refs").EnumerateArray()
                .Select(item => Id(item.GetString()!)).ToArray();
            string[] missing = fact.GetProperty("missing_information").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            string dependency = fact.GetProperty("dependency_members")[0].GetString()!;
            string locus = Text(fact, "affected_locus_id");
            CausalJoinPopulationMember member = new(
                Id(hypothesisId), Id(analyzer), CandidateLane.MandatoryEvidence,
                [new CandidateParticipantContract(Id(dependency), "required-dependency"),
                    new CandidateParticipantContract(Id(locus), "affected-locus")],
                Text(fact, "causal_condition_id"), [Id(dependency), Id(locus)], [Id(dependency)], evidence, [], missing,
                lead ? CausalJoinInputState.Ambiguous : CausalJoinInputState.Complete,
                "Typed lead-promotion hypothesis.", "bounded functional consequence")
            {
                SourceFactId = Id("source-" + runId),
            };
            CandidateAnalysisContract candidates = Candidates(runId, analyzer, [member], version);
            CandidateHypothesisContract hypothesis = candidates.Hypotheses.Single();
            FindingEvidenceFactContract findingFact = new(
                Id("finding-evidence-" + runId), hypothesis.HypothesisId,
                WorstCredibleConsequence.MeaningfulBoundedLoss, locus, Text(fact, "causal_condition_id"),
                fact.GetProperty("applicability_predicates").EnumerateArray()
                    .Select(item => item.GetString()!).ToArray(), [], [], evidence);
            return (candidates, findingFact, SingleProof(hypothesis, candidates, findingFact.CausalCondition,
                findingFact.AffectedLocus, findingFact.ApplicabilityPredicates, evidence));
        }

        (CandidateAnalysisContract Candidates, FindingEvidenceFactContract Fact, SharedCauseProofContract Proof) priorRun =
            Build(priorFact, Text(priorFact, "run_fact_id"), lead: true);
        FindingCaseContract prior = FindingCasePipeline.Execute(Reidentify(Input(
            priorRun.Candidates, [priorRun.Fact], [priorRun.Proof], [], [], [], [], [], [], [], [])));
        AnalysisCaseContract priorLead = prior.Cases.Single(item => item.Kind == CaseOccurrenceKind.LeadOnly);
        PriorCaseContract priorCase = new(
            priorLead.CaseOccurrenceId, priorLead.LogicalCaseId, priorLead.OriginatingRunId, priorLead.Kind,
            priorLead.FindingOccurrenceIds, priorLead.HypothesisIds, priorLead.IdentityEnvelope,
            priorLead.SemanticFingerprint, true, []);

        (CandidateAnalysisContract Candidates, FindingEvidenceFactContract Fact, SharedCauseProofContract Proof) currentRun =
            Build(currentFact, "current-run-promo", lead: false);
        FindingCaseContract current = FindingCasePipeline.Execute(Reidentify(Input(
            currentRun.Candidates, [currentRun.Fact], [currentRun.Proof], [], [], [], [], [], [], [priorCase], [])));
        return new LeadPromotionExecution(prior, current);
    }

    public static CaseReconciliationExecution ExecuteCaseReconciliation(JsonElement factual)
    {
        JsonElement caseFacts = factual.GetProperty("case_reconciliation_facts");
        JsonElement priorRun = caseFacts.GetProperty("prior_run");
        JsonElement currentRun = caseFacts.GetProperty("continuing_run");
        JsonElement lookalikeRun = caseFacts.GetProperty("lookalike_run");
        FindingCaseContract prior = FindingCasePipeline.Execute(Reidentify(CaseInput(priorRun, [], [], [], [], [], null)));
        Dictionary<string, OpaqueId> priorOccurrenceByCondition = priorRun.GetProperty("condition_facts").EnumerateArray()
            .ToDictionary(condition => Text(condition, "condition_fact_id"), condition =>
            {
                string locus = Text(condition, "affected_locus_id");
                return prior.Findings.Single(finding => finding.IdentityEnvelope.AffectedLocus == locus)
                    .FindingOccurrenceId;
            }, StringComparer.Ordinal);
        PriorFindingContract[] priorFindings = prior.Findings.Select(item => new PriorFindingContract(
            item.FindingOccurrenceId, item.LogicalFindingId, item.OriginatingRunId, item.CandidateId, item.HypothesisId,
            item.IdentityEnvelope, item.SemanticFingerprint, true, ["case-applicable-analysis"])).ToArray();
        PriorCaseContract[] priorCases = prior.Cases.Select(item => new PriorCaseContract(
            item.CaseOccurrenceId, item.LogicalCaseId, item.OriginatingRunId, item.Kind,
            item.FindingOccurrenceIds, item.HypothesisIds, item.IdentityEnvelope, item.SemanticFingerprint,
            true, ["case-applicable-analysis"])).ToArray();
        OpaqueId coverageAnalyzer = Id("case-coverage-analyzer");
        CoveragePopulationFactContract[] populations =
        [
            new(Id("case-coverage-population"), coverageAnalyzer,
                "case-applicable-analysis", "applicable case members")
            {
                EvidenceIds = [Id("case-coverage-proof")],
            },
        ];
        CoverageMemberFactContract[] coverageMembers =
        [
            new(Id("case-coverage-member"), coverageAnalyzer, "case-applicable-analysis",
                "applicable case members", Id("case-analysis-member"), CoverageMemberState.Completed,
                "completed", "none", null, []),
        ];
        ProducerCompatibilityContract[] compatibility =
        [
            Compatibility(prior.Findings[0].IdentityEnvelope, currentRun, "case-finding-producer-compatibility"),
        ];
        FindingCaseContract current = FindingCasePipeline.Execute(Reidentify(CaseInput(
            currentRun, priorFindings, priorCases, compatibility, populations, coverageMembers,
            priorOccurrenceByCondition)));
        FindingCaseContract lookalike = FindingCasePipeline.Execute(Reidentify(CaseInput(
            lookalikeRun, priorFindings, priorCases, compatibility, populations, coverageMembers,
            priorOccurrenceByCondition)));
        return new CaseReconciliationExecution(prior, current, lookalike);
    }

}
