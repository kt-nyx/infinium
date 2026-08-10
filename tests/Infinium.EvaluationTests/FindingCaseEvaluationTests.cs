using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.Cases;
using Infinium.Application.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class FindingCaseEvaluationTests
{
    private const string ExpectedTruthSha256 = "6f395a3de625c8d72ea8bd73b70aedede213e9533b9299ebed931f7b4eb5b3d2";
    private static readonly string[] ExpectedReconciliationOutcomes =
    [
        "exact-continuation", "analytical-revision", "related-follow-up", "new-distinct",
        "ambiguous", "unknown", "not-observed", "not-evaluated",
    ];
    private static readonly string[] ExpectedTestTaxonomySources =
        ["test-v1-assignment-motion", "test-v1-assignment-disk", "test-v1-assignment-stream"];

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Cases")]
    public void CaseGroupingFrozenIndependentTruthIsAnswerIsolatedAndExercisesFalseMergeAndSplitGuards()
    {
        using JsonDocument truth = LoadTruth(out string sha256);
        Assert.AreEqual(ExpectedTruthSha256, sha256);
        JsonElement package = truth.RootElement.GetProperty("packages").GetProperty("causal_conclusions");
        JsonElement factual = package.GetProperty("answer_free_factual_inputs");

        AssertNoAnswerKeys(factual);
        CausalEvaluationRun baseline = ExecuteCausalPackage(factual, metamorphic: false);
        CausalEvaluationRun metamorphic = ExecuteCausalPackage(factual, metamorphic: true);

        JsonElement expected = package.GetProperty("expected_typed_output");
        Assert.AreEqual(3, expected.GetProperty("exact_counts").GetProperty("findings").GetInt32());
        Assert.AreEqual(2, expected.GetProperty("exact_counts").GetProperty("supported_cases").GetInt32());
        Assert.AreEqual(1, expected.GetProperty("exact_counts").GetProperty("lead_only_cases").GetInt32());
        Assert.AreEqual(5, expected.GetProperty("negative_guards").EnumerateObject().Count());
        Assert.IsFalse(expected.GetProperty("metamorphic_expectation").GetProperty("membership_changed").GetBoolean());

        JsonElement counts = expected.GetProperty("exact_counts");
        Assert.AreEqual(counts.GetProperty("findings").GetInt32(), baseline.Output.Findings.Count);
        Assert.AreEqual(counts.GetProperty("abstentions").GetInt32(), baseline.Output.Abstentions.Count);
        Assert.AreEqual(counts.GetProperty("recommendations").GetInt32(), baseline.Output.Recommendations.Count);
        Assert.AreEqual(counts.GetProperty("supported_cases").GetInt32(),
            baseline.Output.Cases.Count(item => item.Kind == CaseOccurrenceKind.Supported));
        Assert.AreEqual(counts.GetProperty("lead_only_cases").GetInt32(),
            baseline.Output.Cases.Count(item => item.Kind == CaseOccurrenceKind.LeadOnly));
        Assert.AreEqual(0, baseline.Output.Cases.Count(item => item.Kind == CaseOccurrenceKind.LeadOnly && item.AffectsReadiness));

        Dictionary<string, JsonElement> expectedFindings = expected.GetProperty("findings").EnumerateArray()
            .ToDictionary(item => item.GetProperty("source_hypothesis_id").GetString()!, StringComparer.Ordinal);
        CollectionAssert.AreEquivalent(expectedFindings.Keys.ToArray(), baseline.FindingSourceHypotheses().ToArray());
        foreach (FindingContract finding in baseline.Output.Findings)
        {
            string source = baseline.SourceHypothesis(finding.HypothesisId);
            JsonElement oracle = expectedFindings[source];
            Assert.AreEqual(oracle.GetProperty("severity").GetString(), Kebab(finding.Severity));
            Assert.AreEqual(oracle.GetProperty("confidence").GetString(), Kebab(finding.Confidence));
            Assert.AreEqual(oracle.GetProperty("causal_condition_id").GetString(), finding.IdentityEnvelope.CausalCondition);
            Assert.AreEqual(oracle.GetProperty("expected_symptoms")[0].GetString(), finding.Conclusion);
            Assert.AreEqual("generic-causal-conclusion-analyzer", finding.IdentityEnvelope.AnalyzerFamily);
            Assert.AreEqual("1.0.2", finding.IdentityEnvelope.AnalyzerVersion.ToString());
            Assert.AreEqual("1.0.0", finding.IdentityEnvelope.SemanticContractVersion.ToString());
            Assert.AreEqual("1.0.0", finding.IdentityEnvelope.IdentityContractVersion.ToString());
            Assert.AreEqual(finding.IdentityEnvelope.CanonicalSignature,
                FindingCaseIdentity.ComputeIdentitySignature(finding.IdentityEnvelope));
            CollectionAssert.AreEquivalent(
                oracle.GetProperty("evidence_refs").EnumerateArray().Select(item => item.GetString()).ToArray(),
                finding.EvidenceIds.Select(item => item.Value).ToArray());
        }

        Dictionary<string, string> sourceByOracleFinding = expected.GetProperty("findings").EnumerateArray()
            .ToDictionary(item => item.GetProperty("finding_occurrence_id").GetString()!,
                item => item.GetProperty("source_hypothesis_id").GetString()!, StringComparer.Ordinal);
        Dictionary<string, string> sourceByOracleAbstention = expected.GetProperty("abstentions").EnumerateArray()
            .ToDictionary(item => item.GetProperty("abstention_id").GetString()!,
                item => item.GetProperty("source_hypothesis_id").GetString()!, StringComparer.Ordinal);
        foreach (JsonElement oracle in expected.GetProperty("recommendations").EnumerateArray())
        {
            string basisType = oracle.GetProperty("basis_type").GetString()!;
            string source = basisType == "finding"
                ? sourceByOracleFinding[oracle.GetProperty("basis_id").GetString()!]
                : sourceByOracleAbstention[oracle.GetProperty("basis_id").GetString()!];
            FindingRecommendationContract[] matches = basisType == "finding"
                ? baseline.Output.Recommendations.Where(item => item.FindingOccurrenceId is not null
                    && baseline.SourceHypothesis(baseline.Output.Findings.Single(finding =>
                        finding.FindingOccurrenceId == item.FindingOccurrenceId).HypothesisId) == source).ToArray()
                : baseline.Output.Recommendations.Where(item => item.AbstentionId is not null
                    && baseline.SourceHypothesis(baseline.Output.Abstentions.Single(abstention =>
                        abstention.AbstentionId == item.AbstentionId).HypothesisId) == source).ToArray();
            Assert.AreEqual(1, matches.Length, $"No exact recommendation basis for {basisType}/{source}. "
                + string.Join(", ", baseline.Output.Recommendations.Select(item =>
                    $"{item.Kind}:{item.FindingOccurrenceId?.Value}:{item.AbstentionId?.Value}:{item.LeadHypothesisId?.Value}")));
            FindingRecommendationContract actual = matches[0];
            Assert.AreEqual(basisType == "finding" ? RecommendationKind.Remediation : RecommendationKind.FurtherInvestigation,
                actual.Kind);
            Assert.AreEqual(oracle.GetProperty("action").GetString(), actual.Action);
            Assert.AreEqual(oracle.GetProperty("uncertainty").GetString(), actual.Uncertainty);
            Assert.AreEqual(oracle.GetProperty("reversibility").GetString(), actual.Reversibility);
            Assert.AreEqual(oracle.GetProperty("verification").GetString(), actual.Verification);
            CollectionAssert.AreEqual(oracle.GetProperty("risks").EnumerateArray().Select(item => item.GetString()).ToArray(),
                actual.Risks.ToArray());
            CollectionAssert.AreEquivalent(oracle.GetProperty("evidence_refs").EnumerateArray()
                .Select(item => item.GetString()).ToArray(), actual.EvidenceIds.Select(item => item.Value).ToArray());
        }

        string[] expectedSupported = expected.GetProperty("supported_cases").EnumerateArray()
            .Select(item => GroupKey(item.GetProperty("hypothesis_members").EnumerateArray()
                .Select(value => value.GetString()!))).Order(StringComparer.Ordinal).ToArray();
        string[] expectedLeads = expected.GetProperty("lead_only_cases").EnumerateArray()
            .Select(item => GroupKey(item.GetProperty("hypothesis_members").EnumerateArray()
                .Select(value => value.GetString()!))).Order(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(expectedSupported, baseline.CaseGroups(CaseOccurrenceKind.Supported));
        CollectionAssert.AreEqual(expectedLeads, baseline.CaseGroups(CaseOccurrenceKind.LeadOnly));
        CollectionAssert.AreEqual(expectedSupported, metamorphic.CaseGroups(CaseOccurrenceKind.Supported));
        CollectionAssert.AreEqual(expectedLeads, metamorphic.CaseGroups(CaseOccurrenceKind.LeadOnly));
        foreach (JsonElement oracle in expected.GetProperty("supported_cases").EnumerateArray())
        {
            string group = GroupKey(oracle.GetProperty("hypothesis_members").EnumerateArray()
                .Select(item => item.GetString()!));
            AnalysisCaseContract actual = baseline.Output.Cases.Single(item => item.Kind == CaseOccurrenceKind.Supported
                && GroupKey(item.HypothesisIds.Select(baseline.SourceHypothesis)) == group);
            Assert.AreEqual(oracle.GetProperty("case_kind").GetString(), Kebab(actual.Kind));
            Assert.AreEqual(oracle.GetProperty("identity_envelope")
                .GetProperty("causal_condition_or_shared_cause_pattern").GetString(), actual.SharedCause);
            Assert.AreEqual(actual.SharedCause, actual.IdentityEnvelope.CausalCondition);
            CollectionAssert.AreEquivalent(oracle.GetProperty("hypothesis_members").EnumerateArray()
                    .Select(item => item.GetString()).ToArray(),
                actual.HypothesisIds.Select(baseline.SourceHypothesis).ToArray());
            Assert.AreEqual(oracle.GetProperty("finding_members").GetArrayLength(), actual.FindingOccurrenceIds.Count);
            Assert.IsTrue(actual.CauseProofEvidenceIds.Count > 0);
        }
        Dictionary<string, FindingContract> metamorphicFindings = metamorphic.Output.Findings
            .ToDictionary(item => metamorphic.SourceHypothesis(item.HypothesisId), StringComparer.Ordinal);
        foreach (FindingContract finding in baseline.Output.Findings)
        {
            FindingContract renamed = metamorphicFindings[baseline.SourceHypothesis(finding.HypothesisId)];
            Assert.AreEqual(finding.LogicalFindingId, renamed.LogicalFindingId);
            Assert.AreEqual(finding.SemanticFingerprint, renamed.SemanticFingerprint);
            Assert.AreEqual(finding.IdentityEnvelope.CanonicalSignature, renamed.IdentityEnvelope.CanonicalSignature);
        }
        Assert.AreEqual(0, baseline.Output.Cases.Count(item => item.Kind == CaseOccurrenceKind.LeadOnly
            && item.FindingOccurrenceIds.Count > 0));
        CollectionAssert.AreEquivalent(
            expected.GetProperty("abstentions").EnumerateArray()
                .Select(item => item.GetProperty("source_hypothesis_id").GetString()).ToArray(),
            baseline.Output.Abstentions.Select(item => baseline.SourceHypothesis(item.HypothesisId)).ToArray());
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Cases")]
    public void CoveragePresentationClosesEveryPopulationStateWithoutCombinedPercentageOrSafetyClaim()
    {
        using JsonDocument truth = LoadTruth(out _);
        JsonElement package = truth.RootElement.GetProperty("packages").GetProperty("coverage_boundaries");
        JsonElement factual = package.GetProperty("answer_free_factual_inputs");
        JsonElement expected = package.GetProperty("expected_typed_output");
        AssertNoAnswerKeys(factual);
        FindingCaseContract output = FindingCaseFixtureProductAdapter.ExecuteCoverage(factual);
        Assert.IsFalse(expected.GetProperty("presentation_prohibitions").GetProperty("combined_analyzed_percentage").GetBoolean());
        Assert.IsFalse(expected.GetProperty("presentation_prohibitions").GetProperty("combined_safety_percentage").GetBoolean());
        Assert.AreEqual(0, expected.GetProperty("exact_counts").GetProperty("safety_guarantees").GetInt32());
        Assert.AreEqual(expected.GetProperty("exact_counts").GetProperty("coverage_matrix_populations").GetInt32(), output.Coverage.Count);
        foreach (JsonElement oracle in expected.GetProperty("coverage_matrix").EnumerateArray())
        {
            CoverageContract actual = output.Coverage.Single(item => item.PopulationId == oracle.GetProperty("population_id").GetString());
            Assert.AreEqual(oracle.GetProperty("denominator").GetInt64(), actual.Denominator);
            Assert.AreEqual(oracle.GetProperty("completed_count").GetInt64(), actual.CompletedCount);
            Assert.AreEqual(oracle.GetProperty("population_status").GetString(), Kebab(actual.State));
            Dictionary<string, string> expectedMembers = oracle.GetProperty("member_states").EnumerateObject()
                .ToDictionary(item => item.Name, item => item.Value.GetString()!, StringComparer.Ordinal);
            Dictionary<string, string> actualMembers = actual.MemberResults.ToDictionary(
                item => item.MemberId.Value, item => Kebab(item.State), StringComparer.Ordinal);
            CollectionAssert.AreEqual(
                expectedMembers.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}|{item.Value}").ToArray(),
                actualMembers.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}|{item.Value}").ToArray());
            Assert.AreEqual($"{actual.CompletedCount}/{actual.Denominator}",
                expected.GetProperty("coverage_completion_ratios").GetProperty(actual.PopulationId).GetString());
            Assert.AreEqual(ContractConstants.TaxonomyId, actual.TaxonomyId);
            Assert.AreEqual(ContractConstants.TaxonomyVersion, actual.TaxonomyVersion.ToString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(actual.AnalyzerId.Value));
            Assert.IsFalse(string.IsNullOrWhiteSpace(actual.DenominatorLabel));
            CollectionAssert.AreEquivalent(
                oracle.GetProperty("gap_ids").EnumerateArray().Select(item => item.GetString()).ToArray(),
                actual.GapIds.Select(item => item.Value).ToArray());
            CollectionAssert.AreEquivalent(
                oracle.GetProperty("failure_ids").EnumerateArray().Select(item => item.GetString()).ToArray(),
                actual.FailureIds.Select(item => item.Value).ToArray());
            if (oracle.TryGetProperty("exclusions", out JsonElement exclusions))
            {
                string[] expectedExclusions = exclusions.EnumerateObject()
                    .Select(item => $"{item.Name}|{item.Value.GetString()}").Order(StringComparer.Ordinal).ToArray();
                string[] actualExclusions = actual.Exclusions
                    .Select(item => $"{item.MemberId.Value}|{item.Reason}").Order(StringComparer.Ordinal).ToArray();
                CollectionAssert.AreEqual(expectedExclusions, actualExclusions);
            }
        }
        JsonElement[] expectedVariants = expected.GetProperty("boundary_variants").EnumerateArray().ToArray();
        JsonElement[] factualVariants = factual.GetProperty("boundary_variant_facts").EnumerateArray().ToArray();
        Assert.AreEqual(expected.GetProperty("exact_counts").GetProperty("boundary_variants").GetInt32(), expectedVariants.Length);
        foreach (JsonElement expectedVariant in expectedVariants)
        {
            string variantId = expectedVariant.GetProperty("variant_id").GetString()!;
            JsonElement factualVariant = factualVariants.Single(item => item.GetProperty("variant_id").GetString() == variantId);
            FindingCaseContract variant = FindingCaseFixtureProductAdapter.ExecuteCoverageVariant(factualVariant);
            Assert.AreEqual(expectedVariant.GetProperty("finding_count").GetInt32(), variant.Findings.Count, variantId);
            Assert.AreEqual(expectedVariant.GetProperty("supported_case_count").GetInt32(),
                variant.Cases.Count(item => item.Kind == CaseOccurrenceKind.Supported), variantId);
            Assert.AreEqual(expectedVariant.GetProperty("lead_only_case_count").GetInt32(),
                variant.Cases.Count(item => item.Kind == CaseOccurrenceKind.LeadOnly), variantId);
            Assert.AreEqual("no-safety-claim", variant.PublicationClaimBoundary, variantId);
            Assert.IsTrue(variant.Boundaries.All(item => item.State == BoundaryUseState.NotUsed), variantId);
            foreach (JsonElement expectedPopulation in expectedVariant.GetProperty("population_results").EnumerateArray())
            {
                CoverageContract actual = variant.Coverage.Single(item =>
                    item.PopulationId == expectedPopulation.GetProperty("population_id").GetString());
                Assert.AreEqual(expectedPopulation.GetProperty("denominator").GetInt64(), actual.Denominator, variantId);
                Assert.AreEqual(expectedPopulation.GetProperty("completed_count").GetInt64(), actual.CompletedCount, variantId);
                Assert.AreEqual(expectedPopulation.GetProperty("status").GetString(), Kebab(actual.State), variantId);
                if (expectedPopulation.TryGetProperty("member_states", out JsonElement memberStates))
                {
                    CollectionAssert.AreEqual(memberStates.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal)
                            .Select(item => $"{item.Name}|{item.Value.GetString()}").ToArray(),
                        actual.MemberResults.OrderBy(item => item.MemberId.Value, StringComparer.Ordinal)
                            .Select(item => $"{item.MemberId.Value}|{Kebab(item.State)}").ToArray(), variantId);
                }
                if (expectedPopulation.TryGetProperty("gap_ids", out JsonElement gapIds))
                {
                    CollectionAssert.AreEquivalent(gapIds.EnumerateArray().Select(item => item.GetString()).ToArray(),
                        actual.GapIds.Select(item => item.Value).ToArray(), variantId);
                }
                if (expectedPopulation.TryGetProperty("failure_ids", out JsonElement failureIds))
                {
                    CollectionAssert.AreEquivalent(failureIds.EnumerateArray().Select(item => item.GetString()).ToArray(),
                        actual.FailureIds.Select(item => item.Value).ToArray(), variantId);
                }
                if (expectedPopulation.TryGetProperty("exclusions", out JsonElement expectedExclusions))
                {
                    CollectionAssert.AreEqual(expectedExclusions.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal)
                            .Select(item => $"{item.Name}|{item.Value.GetString()}").ToArray(),
                        actual.Exclusions.OrderBy(item => item.MemberId.Value, StringComparer.Ordinal)
                            .Select(item => $"{item.MemberId.Value}|{item.Reason}").ToArray(), variantId);
                }
            }
        }
        Assert.AreEqual("no-safety-claim", output.PublicationClaimBoundary);
    }

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

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Cases")]
    public void CaseReconciliationExecutesMemberFirstDecisionEightContinuityAndRejectsLookalikeMerge()
    {
        using JsonDocument truth = LoadTruth(out _);
        JsonElement package = truth.RootElement.GetProperty("packages").GetProperty("reconciliation_lineage");
        JsonElement factual = package.GetProperty("answer_free_factual_inputs");
        JsonElement oracle = package.GetProperty("expected_typed_output").GetProperty("case_reconciliation");
        AssertNoAnswerKeys(factual);
        CaseReconciliationExecution execution = FindingCaseFixtureProductAdapter.ExecuteCaseReconciliation(factual);
        AnalysisCaseContract priorCase = execution.Prior.Cases.Single(item => item.Kind == CaseOccurrenceKind.Supported);
        AnalysisCaseContract currentCase = execution.Current.Cases.Single(item => item.Kind == CaseOccurrenceKind.Supported);
        OccurrenceReconciliationContract continuity = execution.Current.ReconciliationAssessments.Single(item =>
            item.SubjectKind == "case" && item.CurrentOccurrenceId == currentCase.CaseOccurrenceId);
        Assert.AreEqual("exact-continuation", Kebab(continuity.Outcome), string.Join(" | ",
            execution.Current.ReconciliationAssessments.Select(item =>
                $"{item.SubjectKind}:{item.Outcome}:{item.PriorOccurrenceId?.Value}->{item.CurrentOccurrenceId?.Value}:{string.Join(',', item.Gaps)}")));
        Assert.AreEqual(priorCase.LogicalCaseId, currentCase.LogicalCaseId);
        Assert.AreNotEqual(0, priorCase.FindingOccurrenceIds.Count);
        Assert.IsFalse(priorCase.FindingOccurrenceIds.SequenceEqual(currentCase.FindingOccurrenceIds));
        OpaqueId[] memberAssessmentIds = execution.Current.ReconciliationAssessments
            .Where(item => item.SubjectKind == "finding").Select(item => item.AssessmentId).ToArray();
        Assert.IsTrue(memberAssessmentIds.All(continuity.ProofEvidenceIds.Contains));
        Assert.AreEqual(3, oracle.GetProperty("exact_counts").GetProperty("case_occurrences").GetInt32());

        OccurrenceReconciliationContract[] actualMembers = execution.Current.ReconciliationAssessments
            .Where(item => item.SubjectKind == "finding")
            .Concat(execution.Lookalike.ReconciliationAssessments.Where(item => item.SubjectKind == "finding"))
            .ToArray();
        JsonElement[] oracleMembers = oracle.GetProperty("member_finding_assessments").EnumerateArray().ToArray();
        Assert.AreEqual(oracle.GetProperty("exact_counts").GetProperty("member_finding_assessments").GetInt32(),
            actualMembers.Length);
        CollectionAssert.AreEquivalent(oracleMembers.Select(item => item.GetProperty("outcome").GetString()).ToArray(),
            actualMembers.Select(item => Kebab(item.Outcome)).ToArray());
        string[] expectedMemberGates = oracleMembers.Select(CanonicalOracleReconciliation).Order(StringComparer.Ordinal).ToArray();
        string[] actualMemberGates = actualMembers.Select(CanonicalReconciliation).Order(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(expectedMemberGates, actualMemberGates,
            string.Join(Environment.NewLine, expectedMemberGates.Select(item => "EXPECTED " + item)
                .Concat(actualMemberGates.Select(item => "ACTUAL " + item))));

        AnalysisCaseContract lookalikeCase = execution.Lookalike.Cases.Single(item => item.Kind == CaseOccurrenceKind.Supported);
        OccurrenceReconciliationContract rejected = execution.Lookalike.ReconciliationAssessments.Single(item =>
            item.SubjectKind == "case" && item.CurrentOccurrenceId == lookalikeCase.CaseOccurrenceId);
        Assert.AreEqual(ReconciliationOutcome.NewDistinct, rejected.Outcome,
            $"Gates={rejected.Gates}; gaps={string.Join(',', rejected.Gaps)}; considered={string.Join(',', rejected.ConsideredOccurrenceIds)}");
        Assert.IsNull(rejected.PriorOccurrenceId);
        Assert.AreNotEqual(priorCase.LogicalCaseId, lookalikeCase.LogicalCaseId);
        OccurrenceReconciliationContract[] actualCases = [continuity, rejected];
        JsonElement[] oracleCases = oracle.GetProperty("case_assessments").EnumerateArray().ToArray();
        Assert.AreEqual(oracle.GetProperty("exact_counts").GetProperty("case_reconciliation_assessments").GetInt32(),
            actualCases.Length);
        CollectionAssert.AreEqual(oracleCases.Select(CanonicalOracleReconciliation).Order(StringComparer.Ordinal).ToArray(),
            actualCases.Select(CanonicalReconciliation).Order(StringComparer.Ordinal).ToArray());
        Assert.AreEqual(priorCase.IdentityEnvelope.CausalCondition, currentCase.IdentityEnvelope.CausalCondition);
        Assert.AreNotEqual(priorCase.IdentityEnvelope.CausalCondition, lookalikeCase.IdentityEnvelope.CausalCondition);
        Assert.AreEqual("no-safety-claim", execution.Current.PublicationClaimBoundary);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Cases")]
    public void TaxonomyHistoryPreservesProductAssignmentAndUsesExplicitNonProductMappingProvenance()
    {
        using JsonDocument truth = LoadTruth(out _);
        JsonElement package = truth.RootElement.GetProperty("packages").GetProperty("taxonomy_history");
        JsonElement factual = package.GetProperty("answer_free_factual_inputs");
        JsonElement expected = package.GetProperty("expected_typed_output");
        AssertNoAnswerKeys(factual);
        TaxonomyExecution execution = FindingCaseFixtureProductAdapter.ExecuteTaxonomy(factual);
        FindingCaseContract value = execution.Output;
        Assert.AreEqual(69, expected.GetProperty("exact_counts").GetProperty("product_assignment_records").GetInt32());
        Assert.AreEqual(0, expected.GetProperty("exact_counts").GetProperty("product_assignment_mutations").GetInt32());
        Assert.AreEqual("0.1.0", expected.GetProperty("non_product_test_taxonomy_projection")
            .GetProperty("product_taxonomy_version_after_projection").GetString());
        TaxonomyAssignmentContract[] product = value.TaxonomyAssignments
            .Where(item => item.TaxonomyId == ContractConstants.TaxonomyId).ToArray();
        string[] expectedProduct = expected.GetProperty("product_assignments_by_subject").EnumerateObject()
            .SelectMany(subject => subject.Value.EnumerateArray().Select(item => TaxonomyKey(
                subject.Name, item.GetProperty("axis").GetString()!, item.GetProperty("facet").GetString()!,
                item.GetProperty("code").ValueKind == JsonValueKind.String ? item.GetProperty("code").GetString() : null,
                item.GetProperty("applicability_state").GetString()!,
                item.GetProperty("classification_role").ValueKind == JsonValueKind.String
                    ? item.GetProperty("classification_role").GetString() : null)))
            .Order(StringComparer.Ordinal).ToArray();
        string[] actualProduct = product.Select(item => TaxonomyKey(
                execution.SubjectByHypothesis[item.SubjectId], item.Axis, item.Facet, item.Code,
                Kebab(item.Applicability), item.Role is null ? null : Kebab(item.Role.Value)))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.AreEqual(69, product.Length, string.Join(Environment.NewLine,
            expectedProduct.Except(actualProduct, StringComparer.Ordinal).Select(item => "MISSING " + item)
                .Concat(actualProduct.Except(expectedProduct, StringComparer.Ordinal).Select(item => "EXTRA " + item))));
        CollectionAssert.AreEqual(expectedProduct, actualProduct);
        Assert.AreEqual(13, product.Count(item => item.Role is null));
        Assert.AreEqual(56, product.Count(item => item.Role is not null));
        Assert.IsTrue(product.All(item => item.TaxonomyVersion.ToString() == ContractConstants.TaxonomyVersion));

        TaxonomyAssignmentContract[] testV1 = value.TaxonomyAssignments.Where(item =>
            item.TaxonomyId == "infinium.test.taxonomy" && item.TaxonomyVersion == new ContractVersion(1, 0, 0)).ToArray();
        TaxonomyAssignmentContract[] testV2 = value.TaxonomyAssignments.Where(item =>
            item.TaxonomyId == "infinium.test.taxonomy" && item.TaxonomyVersion == new ContractVersion(2, 0, 0)).ToArray();
        Assert.AreEqual(3, testV1.Length);
        Assert.AreEqual(3, testV2.Length);
        Assert.AreEqual(4, value.TaxonomyProjections.Count);
        CollectionAssert.AreEquivalent(
            ExpectedTestTaxonomySources,
            testV1.Select(item => item.AssignmentId.Value).ToArray());
        Assert.IsTrue(value.TaxonomyProjections.All(item => item.EvidenceIds.Count > 0));
        JsonElement mappingOracle = expected.GetProperty("non_product_test_taxonomy_projection");
        Dictionary<string, JsonElement> expectedDerived = mappingOracle.GetProperty("derived_assignments")
            .EnumerateArray().ToDictionary(item => item.GetProperty("code").GetString()!, StringComparer.Ordinal);
        Dictionary<string, TaxonomyAssignmentContract> actualDerived = testV2
            .ToDictionary(item => item.Code!, StringComparer.Ordinal);
        CollectionAssert.AreEquivalent(expectedDerived.Keys.ToArray(), actualDerived.Keys.ToArray());
        Dictionary<string, TaxonomyAssignmentContract> sourceById = testV1.ToDictionary(item => item.AssignmentId.Value,
            StringComparer.Ordinal);
        foreach ((string code, JsonElement oracle) in expectedDerived)
        {
            TaxonomyAssignmentContract actual = actualDerived[code];
            Assert.AreEqual(oracle.GetProperty("taxonomy_id").GetString(), actual.TaxonomyId, code);
            Assert.AreEqual(oracle.GetProperty("taxonomy_version").GetString(), actual.TaxonomyVersion.ToString(), code);
            Assert.AreEqual(oracle.GetProperty("axis").GetString(), actual.Axis, code);
            Assert.AreEqual(oracle.GetProperty("facet").GetString(), actual.Facet, code);
            Assert.AreEqual(oracle.GetProperty("applicability_state").GetString(), Kebab(actual.Applicability), code);
            Assert.AreEqual(oracle.GetProperty("classification_role").GetString(), Kebab(actual.Role!.Value), code);
            CollectionAssert.AreEquivalent(oracle.GetProperty("supersedes_assignment_ids").EnumerateArray()
                    .Select(item => item.GetString()).ToArray(),
                actual.SupersedesAssignmentIds.Select(item => item.Value).ToArray(), code);
            CollectionAssert.AreEquivalent(oracle.GetProperty("evidence_refs").EnumerateArray()
                    .Select(item => item.GetString()).ToArray(),
                actual.EvidenceIds.Select(item => item.Value).ToArray(), code);
            CollectionAssert.AreEquivalent(oracle.GetProperty("applicability_condition_refs").EnumerateArray()
                    .Select(item => item.GetString()).ToArray(),
                actual.ApplicabilityConditionIds.Select(item => item.Value).ToArray(), code);
            Assert.IsTrue(actual.SupersedesAssignmentIds.All(item => sourceById.ContainsKey(item.Value)), code);
        }
        string[] expectedEdges = mappingOracle.GetProperty("mapping_edges").EnumerateArray()
            .Select(edge => $"{edge[0].GetString()}|{expectedDerived.Values.Single(item =>
                item.GetProperty("assignment_id").GetString() == edge[1].GetString()).GetProperty("code").GetString()}")
            .Order(StringComparer.Ordinal).ToArray();
        string[] actualEdges = value.TaxonomyProjections.Select(edge =>
                $"{edge.SourceAssignmentId.Value}|{testV2.Single(item => item.AssignmentId == edge.ProjectedAssignmentId).Code}")
            .Order(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(expectedEdges, actualEdges);
        Assert.IsTrue(value.TaxonomyProjections.All(edge =>
            edge.MappingAuthorityId.Value is "test-map-split-motion" or "test-map-merge-delivery"));
        Assert.AreEqual(0, mappingOracle.GetProperty("raw_evidence_mutations").GetArrayLength());
        Assert.AreEqual(0, mappingOracle.GetProperty("product_assignment_mutations").GetArrayLength());
        Assert.IsTrue(expected.GetProperty("forbidden_inferences_absent").EnumerateArray().Any());
        foreach (JsonProperty subjectNegatives in expected.GetProperty("per_subject_negative_absences").EnumerateObject())
        {
            TaxonomyAssignmentContract[] subjectAssignments = value.TaxonomyAssignments.Where(item =>
                execution.SubjectByHypothesis.GetValueOrDefault(item.SubjectId) == subjectNegatives.Name).ToArray();
            foreach (JsonElement negative in subjectNegatives.Value.EnumerateArray())
            {
                string kind = negative.GetProperty("kind").GetString()!;
                if (negative.TryGetProperty("code", out JsonElement codeElement))
                {
                    string code = codeElement.GetString()!;
                    Assert.IsFalse(subjectAssignments.Any(item => item.Code == code
                        && (!negative.TryGetProperty("classification_role", out JsonElement role)
                            || Kebab(item.Role!.Value) == role.GetString())), $"Forbidden taxonomy inference: {subjectNegatives.Name}/{code}");
                }
                if (kind == "product-taxonomy-mutation")
                {
                    Assert.AreEqual(0, subjectAssignments.Count(item => item.TaxonomyId == ContractConstants.TaxonomyId));
                }
                if (kind == "historical-rewrite")
                {
                    Assert.IsTrue(subjectAssignments.All(item => item.TaxonomyVersion.ToString() == ContractConstants.TaxonomyVersion));
                }
            }
        }
        Assert.IsTrue(value.TaxonomyAssignments.All(item => !string.IsNullOrWhiteSpace(item.Reason)
            && !string.IsNullOrWhiteSpace(item.SubjectType)
            && !string.IsNullOrWhiteSpace(item.AnalyzerOrAdjudicatorId.Value)));
        CollectionAssert.AreEqual(
            FindingCaseJsonCodec.Serialize(value),
            FindingCaseJsonCodec.Serialize(FindingCaseJsonCodec.Deserialize(FindingCaseJsonCodec.Serialize(value))));
        Assert.IsNull(typeof(TaxonomyAssignmentContract).GetProperty("Severity"));
    }

    private static CausalEvaluationRun ExecuteCausalPackage(JsonElement factual, bool metamorphic)
    {
        JsonElement identityFacts = factual.GetProperty("case_occurrence_identity_contract_facts");
        string analyzer = identityFacts.GetProperty("analyzer_family").GetString()!;
        Dictionary<string, string> roles = identityFacts.GetProperty("typed_participant_role_facts")
            .EnumerateArray().ToDictionary(
                item => item.GetProperty("participant_id").GetString()!,
                item => item.GetProperty("causal_role").GetString()!, StringComparer.Ordinal);
        Dictionary<string, JsonElement> facts = factual.GetProperty("hypothesis_facts").EnumerateArray()
            .ToDictionary(item => item.GetProperty("hypothesis_id").GetString()!, StringComparer.Ordinal);
        JsonElement variant = factual.GetProperty("metamorphic_variant");
        string[] order = metamorphic
            ? variant.GetProperty("hypothesis_order").EnumerateArray().Select(item => item.GetString()!).ToArray()
            : facts.Keys.ToArray();
        List<CausalJoinPopulationMember> members = [];
        foreach (string factualHypothesisId in order)
        {
            JsonElement fact = facts[factualHypothesisId];
            string[] participantOrder = metamorphic
                ? variant.GetProperty("participant_orders").GetProperty(factualHypothesisId)
                    .EnumerateArray().Select(item => item.GetString()!).ToArray()
                : fact.GetProperty("participant_order").EnumerateArray().Select(item => item.GetString()!).ToArray();
            _ = metamorphic
                ? variant.GetProperty("display_renames").GetProperty(factualHypothesisId).GetString()
                : fact.GetProperty("display_name").GetString();
            const string rationale = "Typed causal hypothesis.";
            string[] missing = fact.GetProperty("missing_information").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            string[] contradictions = fact.GetProperty("contradicting_evidence_refs").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            CandidateParticipantContract[] typedParticipants = participantOrder
                .Select(id => new CandidateParticipantContract(Id(id), roles[id]))
                .Concat(participantOrder.Length == 1
                    ? [new CandidateParticipantContract(Id("unresolved-consumer-" + factualHypothesisId), "unresolved-target")]
                    : [])
                .ToArray();
            members.Add(new CausalJoinPopulationMember(
                Id(factualHypothesisId), Id(analyzer), CandidateLane.MandatoryEvidence,
                typedParticipants,
                fact.GetProperty("causal_condition_id").GetString()!, typedParticipants.Select(item => item.ParticipantId)
                    .OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
                fact.GetProperty("dependency_members").EnumerateArray().Select(item => Id(item.GetString()!)).ToArray(),
                fact.GetProperty("supporting_evidence_refs").EnumerateArray().Select(item => Id(item.GetString()!)).ToArray(),
                contradictions.Select(Id).ToArray(), missing,
                missing.Length == 0 && contradictions.Length == 0 ? CausalJoinInputState.Complete : CausalJoinInputState.Ambiguous,
                rationale, fact.GetProperty("predicted_impact").GetString()!)
            {
                SourceFactId = Id("source-" + factualHypothesisId),
            });
        }

        CandidateAnalysisContract candidates = CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id(factual.GetProperty("run_fact_id").GetString()!), Id("causal-fixture-population"),
            Id(factual.GetProperty("promotion_rule_facts").GetProperty("rule_id").GetString()!),
            Id("causal-fixture-threshold"), CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [new CausalFixtureSource(Id(analyzer), members)])).Analysis;
        Dictionary<string, OpaqueId> generatedHypotheses = [];
        CandidateAnalysisEntryContract[] adjustedCandidates = candidates.Candidates.Select(candidate =>
        {
            CandidateDecisionContract decision = candidates.Decisions.Single(item => item.DecisionId == candidate.DecisionId);
            string factualId = decision.PopulationMemberId.Value;
            AnalysisConfidence confidence = ParseConfidence(facts[factualId].GetProperty("confidence").GetString()!);
            if (candidate.HypothesisId is not null)
            {
                generatedHypotheses.Add(factualId, candidate.HypothesisId);
            }
            return candidate with { Confidence = confidence };
        }).ToArray();
        candidates = candidates with
        {
            Candidates = adjustedCandidates,
            Hypotheses = candidates.Hypotheses.Select(hypothesis =>
            {
                string factualId = generatedHypotheses.Single(item => item.Value == hypothesis.HypothesisId).Key;
                return hypothesis with { Confidence = ParseConfidence(facts[factualId].GetProperty("confidence").GetString()!) };
            }).ToArray(),
        };
        candidates = candidates with { PayloadId = CandidateAnalysisIdentity.ComputePayloadId(candidates) };
        CandidateAnalysisContractInvariants.Validate(candidates);

        FindingEvidenceFactContract[] evidenceFacts = facts.Where(pair => generatedHypotheses.ContainsKey(pair.Key)).Select(pair =>
        {
            JsonElement fact = pair.Value;
            string consequence = fact.TryGetProperty("worst_credible_consequence_fact", out JsonElement value)
                ? value.GetString()! : string.Empty;
            WorstCredibleConsequence severityBasis = consequence.Contains("meaningful but bounded", StringComparison.Ordinal)
                ? WorstCredibleConsequence.MeaningfulBoundedLoss
                : consequence.Contains("important", StringComparison.Ordinal)
                    ? WorstCredibleConsequence.ImportantRequirementFailure
                    : WorstCredibleConsequence.Unspecified;
            return new FindingEvidenceFactContract(
                Id("finding-fact-" + pair.Key), generatedHypotheses[pair.Key], severityBasis,
                fact.GetProperty("affected_locus_id").GetString()!,
                fact.GetProperty("causal_condition_id").GetString()!,
                fact.GetProperty("applicability_predicates").EnumerateArray().Select(item => item.GetString()!).ToArray(),
                fact.GetProperty("contradicting_evidence_refs").EnumerateArray().Select(item => Id(item.GetString()!)).ToArray(),
                [], fact.GetProperty("supporting_evidence_refs").EnumerateArray().Select(item => Id(item.GetString()!)).ToArray());
        }).ToArray();
        CandidateAnalyzerBindingContract binding = candidates.AnalyzerBindings.Single();
        SharedCauseProofContract[] proofs = factual.GetProperty("causal_proof_facts").EnumerateArray().Select(proof =>
        {
            string condition = proof.GetProperty("condition_id").GetString()!;
            string[] loci = proof.GetProperty("loci").EnumerateArray().Select(item => item.GetString()!).ToArray();
            OpaqueId[] hypothesisIds = facts.Where(pair =>
                    generatedHypotheses.ContainsKey(pair.Key)
                    &&
                    pair.Value.GetProperty("causal_condition_id").GetString() == condition
                    && loci.Contains(pair.Value.GetProperty("affected_locus_id").GetString(), StringComparer.Ordinal))
                .Select(pair => generatedHypotheses[pair.Key]).ToArray();
            CandidateDecisionContract[] proofDecisions = hypothesisIds.Select(hypothesisId =>
            {
                CandidateHypothesisContract hypothesis = candidates.Hypotheses.Single(value => value.HypothesisId == hypothesisId);
                CandidateAnalysisEntryContract candidate = candidates.Candidates.Single(value => value.CandidateId == hypothesis.CandidateId);
                return candidates.Decisions.Single(value => value.DecisionId == candidate.DecisionId);
            }).ToArray();
            Dictionary<string, string> participants = proofDecisions.SelectMany(item => item.Participants)
                .GroupBy(item => item.ParticipantId.Value, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Select(item => item.Role)
                    .Distinct(StringComparer.Ordinal).Single(), StringComparer.Ordinal);
            HashSet<OpaqueId> hypothesisEvidence = hypothesisIds.SelectMany(hypothesisId =>
                candidates.Hypotheses.Single(item => item.HypothesisId == hypothesisId).SupportingEvidenceIds).ToHashSet();
            return new SharedCauseProofContract(
                Id(proof.GetProperty("proof_id").GetString()!), hypothesisIds, analyzer,
                binding.SemanticContractVersion, binding.IdentityContractVersion, participants, condition,
                string.Join("|", loci.Order(StringComparer.Ordinal)),
                evidenceFacts.Where(item => hypothesisIds.Contains(item.HypothesisId))
                    .SelectMany(item => item.ApplicabilityPredicates).Distinct(StringComparer.Ordinal).ToArray(),
                FindingCaseIdentity.SharedCauseDependencyClosureId(hypothesisIds.SelectMany(hypothesisId =>
                {
                    CandidateHypothesisContract hypothesis = candidates.Hypotheses.Single(value => value.HypothesisId == hypothesisId);
                    CandidateAnalysisEntryContract candidate = candidates.Candidates.Single(value => value.CandidateId == hypothesis.CandidateId);
                    return candidates.Decisions.Single(value => value.DecisionId == candidate.DecisionId).DependencyIds;
                })),
                proof.GetProperty("supporting_evidence_refs").EnumerateArray().Select(item => Id(item.GetString()!))
                    .Where(hypothesisEvidence.Contains).ToArray())
            {
                AnalyzerVersion = binding.AnalyzerVersion,
            };
        }).ToArray();
        FindingRecommendationFactContract[] recommendationFacts = facts
            .Where(pair => generatedHypotheses.ContainsKey(pair.Key))
            .Select(pair =>
            {
                JsonElement fact = pair.Value;
                string locus = fact.GetProperty("affected_locus_id").GetString()!;
                string[] dependencies = fact.GetProperty("dependency_members").EnumerateArray()
                    .Select(item => item.GetString()!).ToArray();
                string? consumer = dependencies.FirstOrDefault(item => item.StartsWith("consumer-", StringComparison.Ordinal));
                string? dependency = dependencies.FirstOrDefault(item => !item.StartsWith("consumer-", StringComparison.Ordinal));
                string[] missing = fact.GetProperty("missing_information").EnumerateArray()
                    .Select(item => item.GetString()!).ToArray();
                bool abstention = missing.Length > 0 || fact.GetProperty("contradicting_evidence_refs").GetArrayLength() > 0;
                bool restoreDependency = fact.GetProperty("causal_condition_id").GetString()!
                    .Contains("missing-dependency", StringComparison.Ordinal);
                OpaqueId[] evidence = fact.GetProperty("supporting_evidence_refs").EnumerateArray()
                    .Concat(fact.GetProperty("contradicting_evidence_refs").EnumerateArray())
                    .Select(item => Id(item.GetString()!)).ToArray();
                return new FindingRecommendationFactContract(
                    Id("recommendation-fact-" + pair.Key), generatedHypotheses[pair.Key],
                    abstention ? RecommendationKind.FurtherInvestigation : RecommendationKind.Remediation,
                    abstention
                        ? "obtain consumer applicability and dependency-closure evidence"
                        : restoreDependency ? $"restore {dependency}" : $"replace the stale effective value at {locus}",
                    abstention ? "material applicability contradiction"
                        : restoreDependency ? "none within supplied facts" : "bounded to declared locus and dependencies",
                    abstention ? "not-applicable; investigation only"
                        : restoreDependency ? "remove restored dependency" : "restore prior effective value",
                    abstention
                        ? ["premature remediation could change an inapplicable locus"]
                        : restoreDependency ? ["dependency version remains independently unverified"]
                        : ["consumer intent must remain applicable"],
                    abstention ? "resolve both missing-information items before promotion"
                        : restoreDependency ? $"verify {consumer} admission" : $"reobserve {locus} and {consumer}",
                    evidence);
            }).ToArray();
        FindingAnalyzerConclusionFact[] sourceFacts = evidenceFacts.Select(fact =>
        {
            FindingRecommendationFactContract recommendation = recommendationFacts.Single(item =>
                item.HypothesisId == fact.HypothesisId);
            SharedCauseProofContract? proof = proofs.SingleOrDefault(item => item.HypothesisIds.Contains(fact.HypothesisId));
            return new FindingAnalyzerConclusionFact(
                fact.FactId, fact.HypothesisId,
                proof?.ProofId ?? Id("source-cause-" + fact.CausalCondition), fact.WorstCredibleConsequence,
                fact.AffectedLocus, fact.CausalCondition, fact.ApplicabilityPredicates,
                fact.DefeatingContradictionIds, fact.RetainedNonDefeatingContradictionIds, fact.EvidenceIds,
                recommendation.Kind, recommendation.Action, recommendation.Uncertainty,
                recommendation.Reversibility, recommendation.Risks, recommendation.Verification,
                recommendation.EvidenceIds);
        }).ToArray();
        FindingCaseInputContract input = FindingCaseInputProducer.Create(new FindingCaseAnalyzerBuildRequest(
            Id(factual.GetProperty("promotion_rule_facts").GetProperty("rule_id").GetString()!),
            new ContractVersion(1, 0, 0), Id("fixture-reconciliation-policy"), new ContractVersion(1, 0, 3),
            Id("fixture-reconciliation-actor"),
            new UtcTimestamp(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)), candidates,
            sourceFacts, [], [], [], [], [], [], [],
            [
                new("provider", BoundaryUseState.NotUsed, "answer-free local fixture"),
                new("hosted-search", BoundaryUseState.NotUsed, "answer-free local fixture"),
                new("nexus", BoundaryUseState.NotUsed, "answer-free local fixture"),
                new("loot", BoundaryUseState.NotUsed, "not configured"),
            ]));
        FindingCaseContract output = FindingCasePipeline.Execute(input);
        Dictionary<OpaqueId, string> sourceByGenerated = generatedHypotheses.ToDictionary(item => item.Value, item => item.Key);
        return new CausalEvaluationRun(output, sourceByGenerated);
    }

    private static AnalysisConfidence ParseConfidence(string value) => value switch
    {
        "plausible" => AnalysisConfidence.Plausible,
        "strongly-supported" => AnalysisConfidence.StronglySupported,
        "confirmed" => AnalysisConfidence.Confirmed,
        _ => throw new InvalidDataException("Fixture confidence is outside the accepted closed set."),
    };

    private static ReconciliationGateState ParseGate(string value) => value switch
    {
        "proven" => ReconciliationGateState.ProvenEquivalent,
        "failed" => ReconciliationGateState.ProvenDifferent,
        "ambiguous" => ReconciliationGateState.Ambiguous,
        "unknown" => ReconciliationGateState.Unknown,
        "not-evaluated" => ReconciliationGateState.NotEvaluated,
        _ => throw new InvalidDataException("Fixture reconciliation gate is outside the accepted closed set."),
    };

    private static string Kebab<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());

    private static string ReconciliationSemanticKey(ReconciliationObservation value) => string.Join("|",
        value.CurrentFactId, string.Join(",", value.PriorOccurrenceIds.Order(StringComparer.Ordinal)),
        Kebab(value.Outcome), value.Gates.Causal, value.Gates.Applicability, value.Gates.Dependency,
        value.Gates.Producer, string.Join(",", value.ProofEvidenceIds.Order(StringComparer.Ordinal)),
        string.Join(",", value.Gaps.Order(StringComparer.Ordinal)));

    private static string CanonicalOracleReconciliation(JsonElement value)
    {
        JsonElement gates = value.GetProperty("gates");
        return string.Join("|", value.GetProperty("outcome").GetString(),
            ParseGate(gates.GetProperty("causal-equivalence").GetString()!),
            ParseGate(gates.GetProperty("applicability-equivalence").GetString()!),
            ParseGate(gates.GetProperty("dependency-equivalence").GetString()!),
            ParseGate(gates.GetProperty("producer-contract-compatibility").GetString()!));
    }

    private static string CanonicalReconciliation(OccurrenceReconciliationContract value) => string.Join("|",
        Kebab(value.Outcome), value.Gates.Causal, value.Gates.Applicability, value.Gates.Dependency,
        value.Gates.Producer);

    private static string GroupKey(IEnumerable<string> values) =>
        string.Join("|", values.Order(StringComparer.Ordinal));

    private static string TaxonomyKey(
        string subject,
        string axis,
        string facet,
        string? code,
        string applicability,
        string? role) => string.Join("|", subject, axis, facet, code ?? "<null>", applicability, role ?? "<null>");

    private sealed record CausalEvaluationRun(
        FindingCaseContract Output,
        IReadOnlyDictionary<OpaqueId, string> SourceByGeneratedHypothesis)
    {
        public string SourceHypothesis(OpaqueId generated) => SourceByGeneratedHypothesis[generated];

        public IEnumerable<string> FindingSourceHypotheses() =>
            Output.Findings.Select(item => SourceHypothesis(item.HypothesisId));

        public string[] CaseGroups(CaseOccurrenceKind kind) => Output.Cases.Where(item => item.Kind == kind)
            .Select(item => GroupKey(item.HypothesisIds.Select(SourceHypothesis)))
            .Order(StringComparer.Ordinal).ToArray();
    }

    private sealed class CausalFixtureSource(OpaqueId analyzerId, IReadOnlyList<CausalJoinPopulationMember> members)
        : ICandidatePopulationSource
    {
        public OpaqueId AnalyzerId => analyzerId;

        public AnalyzerDeclarationContract Declaration { get; } = CandidateAnalyzerDeclarations.Create(
            analyzerId, Math.Max(1, members.Count), 1_000_000,
            supportedShapes: members.Select(item => item.JoinKind).Distinct(StringComparer.Ordinal).ToArray()) with
        {
            AnalyzerFamily = analyzerId.Value,
            AnalyzerVersion = new ContractVersion(1, 0, 2),
        };

        public IReadOnlyList<CausalJoinPopulationMember> DeclarePopulation(
            CandidatePopulationContext context, CancellationToken cancellationToken = default) => members;

        public IReadOnlyList<CausalJoinPopulationMember> ConstructPopulation(
            CandidatePopulationContext context, CancellationToken cancellationToken = default) => members;
    }

    private static JsonDocument LoadTruth(out string sha256)
    {
        string path = Path.Combine(
            FindRepositoryRoot(), "fixtures", "public", "findings-cases",
            "finding-case-independent-truth.v1.0.3.json");
        byte[] bytes = File.ReadAllBytes(path);
        sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return JsonDocument.Parse(bytes);
    }

    private static void AssertNoAnswerKeys(JsonElement element)
    {
        HashSet<string> forbidden = new(StringComparer.Ordinal)
        {
            "expected_typed_output", "finding_occurrence_id", "case_occurrence_id",
            "reconciliation_outcome", "lineage_event_id",
        };
        Visit(element);
        return;

        void Visit(JsonElement current)
        {
            if (current.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in current.EnumerateObject())
                {
                    Assert.IsFalse(forbidden.Contains(property.Name), $"Answer-bearing key reached product input: {property.Name}");
                    Visit(property.Value);
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in current.EnumerateArray())
                {
                    Visit(item);
                }
            }
        }
    }

    private static FindingRecommendationFactContract[] RecommendationFacts(
        IReadOnlyList<FindingEvidenceFactContract> facts) => facts.Select(item => new FindingRecommendationFactContract(
            Id("recommendation-fact-" + item.FactId.Value), item.HypothesisId, RecommendationKind.Validation,
            "Validate the typed causal condition.", "Bounded to supplied typed evidence.",
            "No installed state is changed by analysis.", ["Applicability must remain valid."],
            "Reobserve the affected locus.", item.EvidenceIds)).ToArray();

    private static FindingCaseGapContract Gap(string id, FindingGapState state, GapConclusionEffect conclusion) => new(
        Id(id), "population-" + id, "stage-cases", state, GapReplayEffect.Partial,
        conclusion, "Typed synthetic coverage gap.", "Missing synthetic capability or information.", []);

    private static CoverageContract Coverage(
        string id, long denominator, long completed, CoverageState state,
        IReadOnlyList<OpaqueId> gaps, IReadOnlyList<OpaqueId> failures, IReadOnlyList<string> exclusions) => new(
        Id("coverage-" + id), Id("run-coverage"), Id("analyzer-coverage"), "population-" + id,
        "generic population members", denominator, completed, state,
        ContractConstants.TaxonomyId, ContractVersion.Parse(ContractConstants.TaxonomyVersion),
        [], exclusions.Select((reason, index) => new CoverageExclusionContract(
            Id($"excluded-{id}-{index}"), reason,
            state == CoverageState.SkippedByLimit
                ? CoverageMemberState.SkippedByLimit : CoverageMemberState.SkippedByConfiguration)).ToArray(), gaps, failures);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Infinium repository root was not found.");
    }

    private static OpaqueId Id(string value) => new(value);
}
