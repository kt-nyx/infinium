using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.FindingCases;
using Infinium.Application.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

public sealed partial class FindingCaseEvaluationTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Cases")]
    public void LineageReconciliationFrozenPackageExecutesAllEightOutcomesThroughProductPolicy()
    {
        using JsonDocument truth = LoadTruth(out _);
        JsonElement package = truth.RootElement.GetProperty("packages").GetProperty("reconciliation_lineage");
        JsonElement factual = package.GetProperty("answer_free_factual_inputs");
        JsonElement expected = package.GetProperty("expected_typed_output");
        AssertNoAnswerKeys(factual);
        ReconciliationExecution execution = FindingCaseFixtureProductAdapter.ExecuteReconciliation(factual);
        IReadOnlyList<ReconciliationObservation> actual = execution.Observations;
        JsonElement[] currentOccurrences = expected.GetProperty("current_finding_occurrences").EnumerateArray().ToArray();
        JsonElement[] currentFacts = factual.GetProperty("current_analytical_facts").EnumerateArray().ToArray();
        Assert.AreEqual(currentFacts.Length + 1, currentOccurrences.Length);
        Dictionary<string, string> sourceByOccurrence = currentOccurrences.Take(currentFacts.Length)
            .Select((item, index) => new
            {
                Occurrence = item.GetProperty("occurrence_id").GetString()!,
                Fact = currentFacts[index].GetProperty("fact_id").GetString()!,
            }).ToDictionary(item => item.Occurrence, item => item.Fact, StringComparer.Ordinal);
        JsonElement[] oracleAssessments = expected.GetProperty("reconciliation_assessments").EnumerateArray().ToArray();
        Assert.AreEqual(8, oracleAssessments.Length);
        Assert.AreEqual(8, actual.Count);
        foreach (JsonElement oracle in oracleAssessments)
        {
            string currentFact = oracle.TryGetProperty("current_occurrence_id", out JsonElement currentOccurrence)
                && currentOccurrence.ValueKind == JsonValueKind.String
                ? sourceByOccurrence[currentOccurrence.GetString()!] : string.Empty;
            string[] priorIds = oracle.GetProperty("prior_occurrence_ids").EnumerateArray()
                .Select(item => item.GetString()!).Order(StringComparer.Ordinal).ToArray();
            ReconciliationObservation observation = actual.Single(item =>
                item.CurrentFactId == currentFact && item.PriorOccurrenceIds.Order(StringComparer.Ordinal).SequenceEqual(priorIds));
            Assert.AreEqual(oracle.GetProperty("outcome").GetString(), Kebab(observation.Outcome),
                $"Reconciliation mismatch for {currentFact} with priors {string.Join(",", priorIds)}. "
                + $"Gates={observation.Gates} Proof={string.Join(",", observation.ProofEvidenceIds)}");
            Assert.AreEqual("1.0.2", observation.PolicyVersion.ToString());
            Assert.AreEqual("deterministic-reconciliation-policy-generic-1", observation.ActorId.Value);
            Assert.IsTrue(observation.VisibleByDefault);
            JsonElement gates = oracle.GetProperty("gates");
            Assert.AreEqual(ParseGate(gates.GetProperty("causal-equivalence").GetString()!), observation.Gates.Causal, currentFact);
            Assert.AreEqual(ParseGate(gates.GetProperty("applicability-equivalence").GetString()!), observation.Gates.Applicability, currentFact);
            Assert.AreEqual(ParseGate(gates.GetProperty("dependency-equivalence").GetString()!), observation.Gates.Dependency, currentFact);
            Assert.AreEqual(ParseGate(gates.GetProperty("producer-contract-compatibility").GetString()!), observation.Gates.Producer, currentFact);
            string[] expectedConsidered = oracle.GetProperty("considered_occurrence_ids").EnumerateArray()
                .Select(item => sourceByOccurrence.GetValueOrDefault(item.GetString()!, item.GetString()!))
                .Order(StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(expectedConsidered, observation.ConsideredOccurrenceIds.Order(StringComparer.Ordinal).ToArray());
            CollectionAssert.AreEquivalent(oracle.GetProperty("proof_refs").EnumerateArray()
                .Select(item => item.GetString()).ToArray(), observation.ProofEvidenceIds.ToArray(),
                $"Proof mismatch for {currentFact}: {string.Join(',', observation.ProofEvidenceIds)}");
            CollectionAssert.AreEquivalent(oracle.GetProperty("gaps").EnumerateArray()
                .Select(item => item.GetString()).ToArray(), observation.Gaps.ToArray());
        }
        CollectionAssert.AreEquivalent(
            ExpectedReconciliationOutcomes,
            actual.Select(item => Kebab(item.Outcome)).ToArray());

        Dictionary<string, JsonElement> currentFactById = currentFacts.ToDictionary(
            item => item.GetProperty("fact_id").GetString()!, StringComparer.Ordinal);
        foreach ((string factId, OpaqueId occurrenceId) in execution.OccurrenceByFact)
        {
            FindingContract finding = execution.Output.Findings.Single(item => item.FindingOccurrenceId == occurrenceId);
            JsonElement fact = currentFactById[factId];
            Assert.AreEqual(fact.GetProperty("producer_family").GetString(), finding.IdentityEnvelope.AnalyzerFamily, factId);
            Assert.AreEqual(fact.GetProperty("producer_version").GetString(), finding.IdentityEnvelope.AnalyzerVersion.ToString(), factId);
            Assert.AreEqual(fact.GetProperty("cause").GetString(), finding.IdentityEnvelope.CausalCondition, factId);
            Assert.AreEqual(fact.GetProperty("affected_locus_id").GetString(), finding.IdentityEnvelope.AffectedLocus, factId);
            CollectionAssert.AreEquivalent(fact.GetProperty("applicability").EnumerateArray()
                    .Select(item => item.GetString()).ToArray(),
                finding.IdentityEnvelope.ApplicabilityPredicates.ToArray(), factId);
        }
        JsonElement[] expectedLineage = expected.GetProperty("lineage_events").EnumerateArray()
            .Where(item => item.GetProperty("kind").GetString() != "promotes-lead").ToArray();
        Assert.AreEqual(expectedLineage.Length, execution.Output.LineageEvents.Count);
        foreach (JsonElement oracle in expectedLineage)
        {
            string expectedSuccessor = oracle.GetProperty("successor_occurrence_ids")[0].GetString()!;
            string factId = sourceByOccurrence[expectedSuccessor];
            OpaqueId actualSuccessor = execution.OccurrenceByFact[factId];
            OccurrenceLineageContract lineage = execution.Output.LineageEvents.Single(item =>
                item.SuccessorIds.Contains(actualSuccessor));
            string expectedKind = oracle.GetProperty("kind").GetString()!;
            Assert.AreEqual(expectedKind == "supersedes-finding-revision"
                ? LineageKind.AnalyticalRevision : LineageKind.RelatedFollowUp, lineage.Kind, factId);
            CollectionAssert.AreEqual(oracle.GetProperty("predecessor_occurrence_ids").EnumerateArray()
                    .Select(item => item.GetString()).ToArray(),
                lineage.PredecessorIds.Select(item => item.Value).ToArray(), factId);
            Assert.IsNotNull(lineage.ReconciliationAssessmentId, factId);
        }
        ReconciliationExecution reordered = FindingCaseFixtureProductAdapter.ExecuteReconciliation(factual, reverseCurrentOrder: true);
        string[] baselineSemantics = execution.Observations.Select(ReconciliationSemanticKey)
            .Order(StringComparer.Ordinal).ToArray();
        string[] reorderedSemantics = reordered.Observations.Select(ReconciliationSemanticKey)
            .Order(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(baselineSemantics, reorderedSemantics);
        foreach ((string factId, OpaqueId occurrenceId) in execution.OccurrenceByFact)
        {
            FindingContract baselineFinding = execution.Output.Findings.Single(item => item.FindingOccurrenceId == occurrenceId);
            FindingContract reorderedFinding = reordered.Output.Findings.Single(item =>
                item.FindingOccurrenceId == reordered.OccurrenceByFact[factId]);
            Assert.AreEqual(baselineFinding.LogicalFindingId, reorderedFinding.LogicalFindingId, factId);
        }
        Assert.AreEqual(0, expected.GetProperty("review_state").GetProperty("carryover_events").GetArrayLength());
        Assert.IsTrue(execution.Output.ReconciliationAssessments.All(item => item.VisibleByDefault));
        Assert.AreEqual(0, expected.GetProperty("append_only_history")
            .GetProperty("rewritten_or_deleted_prior_records").GetArrayLength());

        JsonElement leadOracle = expected.GetProperty("lead_promotion");
        LeadPromotionExecution promotion = FindingCaseFixtureProductAdapter.ExecuteLeadPromotion(
            factual.GetProperty("lead_promotion_facts"));
        AnalysisCaseContract priorLead = promotion.Prior.Cases.Single(item => item.Kind == CaseOccurrenceKind.LeadOnly);
        AnalysisCaseContract supportedSuccessor = promotion.Current.Cases.Single(item =>
            item.Kind == CaseOccurrenceKind.Supported);
        FindingContract successorFinding = promotion.Current.Findings.Single();
        OccurrenceLineageContract promotionLineage = promotion.Current.LineageEvents.Single(item =>
            item.Kind == LineageKind.PromotesLead);
        Assert.AreEqual(leadOracle.GetProperty("prior_case_occurrence").GetProperty("kind").GetString(),
            Kebab(priorLead.Kind));
        Assert.AreEqual(leadOracle.GetProperty("successor_case_occurrence").GetProperty("kind").GetString(),
            Kebab(supportedSuccessor.Kind));
        Assert.AreEqual(priorLead.CaseOccurrenceId, supportedSuccessor.SupersedesOccurrenceId);
        Assert.AreNotEqual(priorLead.LogicalCaseId, supportedSuccessor.LogicalCaseId);
        Assert.AreEqual(priorLead.CaseOccurrenceId, promotionLineage.PredecessorIds.Single());
        Assert.AreEqual(supportedSuccessor.CaseOccurrenceId, promotionLineage.SuccessorIds.Single());
        Assert.IsNull(promotionLineage.ReconciliationAssessmentId);
        Assert.AreEqual(successorFinding.FindingOccurrenceId, supportedSuccessor.FindingOccurrenceIds.Single());
        Assert.IsNull(typeof(AnalysisCaseContract).GetProperty("ReviewState"));
    }

}
