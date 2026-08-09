using Infinium.Domain.Contracts;

namespace Infinium.Analysis.Conclusions;

public static class FindingConclusionProducer
{
    public static IReadOnlyDictionary<OpaqueId, FindingConclusionAssessmentContract> Produce(
        FindingCaseInputContract input)
    {
        Dictionary<OpaqueId, CandidateAnalysisEntryContract> candidates = input.CandidateAnalysis.Candidates
            .ToDictionary(item => item.CandidateId);
        Dictionary<OpaqueId, CandidateDecisionContract> decisions = input.CandidateAnalysis.Decisions
            .ToDictionary(item => item.DecisionId);
        Dictionary<OpaqueId, CandidateAnalyzerBindingContract> bindings = input.CandidateAnalysis.AnalyzerBindings
            .ToDictionary(item => item.AnalyzerId);
        Dictionary<OpaqueId, FindingEvidenceFactContract> facts = input.FindingEvidenceFacts
            .ToDictionary(item => item.HypothesisId);
        Dictionary<OpaqueId, FindingRecommendationFactContract> recommendations = input.FindingRecommendationFacts
            .ToDictionary(item => item.HypothesisId);
        Dictionary<OpaqueId, FindingConclusionAssessmentContract> results = [];

        foreach (CandidateHypothesisContract hypothesis in input.CandidateAnalysis.Hypotheses)
        {
            if (!facts.TryGetValue(hypothesis.HypothesisId, out FindingEvidenceFactContract? fact)
                || !candidates.TryGetValue(hypothesis.CandidateId, out CandidateAnalysisEntryContract? candidate)
                || !decisions.TryGetValue(candidate.DecisionId, out CandidateDecisionContract? decision)
                || !bindings.TryGetValue(decision.AnalyzerId, out CandidateAnalyzerBindingContract? binding)
                || !recommendations.TryGetValue(hypothesis.HypothesisId, out FindingRecommendationFactContract? recommendation))
            {
                continue;
            }

            IdentityEnvelopeContract identity = new(
                binding.AnalyzerFamily,
                binding.SemanticContractVersion,
                binding.IdentityContractVersion,
                decision.Participants.ToDictionary(item => item.ParticipantId.Value, item => item.Role, StringComparer.Ordinal),
                fact.CausalCondition,
                fact.AffectedLocus,
                fact.ApplicabilityPredicates.Order(StringComparer.Ordinal).ToArray(),
                FindingCaseIdentity.SharedCauseDependencyClosureId(decision.DependencyIds),
                new Sha256Fingerprint(new string('0', 64)))
            {
                AnalyzerVersion = binding.AnalyzerVersion,
            };
            identity = identity with { CanonicalSignature = FindingCaseIdentity.ComputeIdentitySignature(identity) };
            FindingSeverity severity = fact.WorstCredibleConsequence switch
            {
                WorstCredibleConsequence.MaintenanceOnly => FindingSeverity.Advisory,
                WorstCredibleConsequence.LocalizedLowImpact => FindingSeverity.Minor,
                WorstCredibleConsequence.MeaningfulBoundedLoss => FindingSeverity.Moderate,
                WorstCredibleConsequence.ImportantRequirementFailure => FindingSeverity.Major,
                WorstCredibleConsequence.UsefulPlaythroughBlocked => FindingSeverity.Blocker,
                _ => FindingSeverity.Unspecified,
            };
            results.Add(hypothesis.HypothesisId, new FindingConclusionAssessmentContract(
                CandidateAnalysisIdentity.StableId("finding-conclusion", input.OriginatingRunId.Value, fact.FactId.Value),
                hypothesis.HypothesisId, severity, identity, identity,
                recommendation.Kind, recommendation.Action, recommendation.Uncertainty,
                recommendation.Reversibility, recommendation.Risks, recommendation.Verification,
                []));
        }
        return results;
    }
}
