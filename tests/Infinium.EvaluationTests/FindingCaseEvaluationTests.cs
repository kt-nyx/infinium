using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.FindingCases;
using Infinium.Application.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed partial class FindingCaseEvaluationTests
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
            TestRepository.Root, "fixtures", "public", "findings-cases",
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

    private static OpaqueId Id(string value) => new(value);
}
