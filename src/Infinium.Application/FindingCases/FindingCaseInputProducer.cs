using Infinium.Analysis.Conclusions;
using Infinium.Domain.Contracts;

namespace Infinium.Application.FindingCases;

public sealed record FindingAnalyzerConclusionFact(
    OpaqueId SourceFactId,
    OpaqueId HypothesisId,
    OpaqueId SharedCauseFactId,
    WorstCredibleConsequence WorstCredibleConsequence,
    string AffectedLocus,
    string CausalCondition,
    IReadOnlyList<string> ApplicabilityPredicates,
    IReadOnlyList<OpaqueId> DefeatingContradictionIds,
    IReadOnlyList<OpaqueId> RetainedNonDefeatingContradictionIds,
    IReadOnlyList<OpaqueId> EvidenceIds,
    RecommendationKind RecommendationKind,
    string Action,
    string Uncertainty,
    string Reversibility,
    IReadOnlyList<string> Risks,
    string Verification,
    IReadOnlyList<OpaqueId> RecommendationEvidenceIds);

public sealed record FindingCaseAnalyzerBuildRequest(
    OpaqueId PromotionPolicyId,
    ContractVersion PromotionPolicyVersion,
    OpaqueId ReconciliationPolicyId,
    ContractVersion ReconciliationPolicyVersion,
    OpaqueId ReconciliationActorId,
    UtcTimestamp AssessmentTime,
    CandidateAnalysisContract CandidateAnalysis,
    IReadOnlyList<FindingAnalyzerConclusionFact> ConclusionFacts,
    IReadOnlyList<TaxonomySubjectFact> TaxonomySubjects,
    IReadOnlyList<TaxonomyClassificationFactContract> RetainedTaxonomyFacts,
    IReadOnlyList<TaxonomyProjectionInputContract> TaxonomyProjectionInputs,
    IReadOnlyList<PriorFindingContract> PriorFindings,
    IReadOnlyList<PriorCaseContract> PriorCases,
    IReadOnlyList<ProducerCompatibilityContract> ProducerCompatibilities,
    IReadOnlyList<RelatedFindingFactContract> RelatedFindingFacts,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries)
{
    public IReadOnlyList<ReconciliationCandidateFactContract> ReconciliationCandidateFacts { get; init; } = [];
}

public sealed record FindingCaseInputBuildRequest(
    OpaqueId PromotionPolicyId,
    ContractVersion PromotionPolicyVersion,
    OpaqueId ReconciliationPolicyId,
    ContractVersion ReconciliationPolicyVersion,
    OpaqueId ReconciliationActorId,
    UtcTimestamp AssessmentTime,
    CandidateAnalysisContract CandidateAnalysis,
    IReadOnlyList<FindingEvidenceFactContract> FindingEvidenceFacts,
    IReadOnlyList<FindingRecommendationFactContract> FindingRecommendationFacts,
    IReadOnlyList<SharedCauseProofContract> SharedCauseProofs,
    IReadOnlyList<TaxonomySubjectFact> TaxonomySubjects,
    IReadOnlyList<TaxonomyClassificationFactContract> RetainedTaxonomyFacts,
    IReadOnlyList<TaxonomyProjectionInputContract> TaxonomyProjectionInputs,
    IReadOnlyList<CoveragePopulationFactContract> CoveragePopulationFacts,
    IReadOnlyList<CoverageMemberFactContract> CoverageMemberFacts,
    IReadOnlyList<CoverageFailureFactContract> CoverageFailureFacts,
    IReadOnlyList<PriorFindingContract> PriorFindings,
    IReadOnlyList<PriorCaseContract> PriorCases,
    IReadOnlyList<ProducerCompatibilityContract> ProducerCompatibilities,
    IReadOnlyList<RelatedFindingFactContract> RelatedFindingFacts,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries)
{
    public IReadOnlyList<ReconciliationCandidateFactContract> ReconciliationCandidateFacts { get; init; } = [];
}

public static class FindingCaseInputProducer
{
    public static FindingCaseInputContract Create(FindingCaseAnalyzerBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Dictionary<OpaqueId, CandidateHypothesisContract> hypotheses = request.CandidateAnalysis.Hypotheses
            .ToDictionary(item => item.HypothesisId);
        Dictionary<OpaqueId, CandidateAnalysisEntryContract> candidates = request.CandidateAnalysis.Candidates
            .ToDictionary(item => item.CandidateId);
        Dictionary<OpaqueId, CandidateDecisionContract> decisions = request.CandidateAnalysis.Decisions
            .ToDictionary(item => item.DecisionId);
        Dictionary<OpaqueId, CandidateAnalyzerBindingContract> bindings = request.CandidateAnalysis.AnalyzerBindings
            .ToDictionary(item => item.AnalyzerId);
        if (request.ConclusionFacts.Select(item => item.SourceFactId).Distinct().Count() != request.ConclusionFacts.Count
            || request.ConclusionFacts.Select(item => item.HypothesisId).Distinct().Count() != request.ConclusionFacts.Count
            || request.ConclusionFacts.Any(item => !hypotheses.ContainsKey(item.HypothesisId))
            || !request.ConclusionFacts.Select(item => item.HypothesisId).ToHashSet().SetEquals(hypotheses.Keys))
        {
            throw new InvalidOperationException(
                "Analyzer conclusion facts must bind one-to-one to every delivered hypothesis; incomplete populations are rejected rather than reported as complete.");
        }

        FindingEvidenceFactContract[] evidence = request.ConclusionFacts.Select(item => new FindingEvidenceFactContract(
            CandidateAnalysisIdentity.StableId("finding-evidence-fact", item.SourceFactId.Value), item.HypothesisId,
            item.WorstCredibleConsequence, item.AffectedLocus, item.CausalCondition,
            item.ApplicabilityPredicates, item.DefeatingContradictionIds,
            item.RetainedNonDefeatingContradictionIds, item.EvidenceIds)).ToArray();
        FindingRecommendationFactContract[] recommendations = request.ConclusionFacts.Select(item =>
            new FindingRecommendationFactContract(
                CandidateAnalysisIdentity.StableId("finding-recommendation-fact", item.SourceFactId.Value),
                item.HypothesisId, item.RecommendationKind, item.Action, item.Uncertainty,
                item.Reversibility, item.Risks, item.Verification, item.RecommendationEvidenceIds)).ToArray();
        SharedCauseProofContract[] proofs = request.ConclusionFacts.GroupBy(item => item.SharedCauseFactId)
            .Select(group =>
            {
                FindingAnalyzerConclusionFact[] members = group.OrderBy(item => item.HypothesisId.Value, StringComparer.Ordinal).ToArray();
                CandidateDecisionContract[] memberDecisions = members.Select(item => decisions[candidates[
                    hypotheses[item.HypothesisId].CandidateId].DecisionId]).ToArray();
                CandidateAnalyzerBindingContract[] memberBindings = memberDecisions.Select(item => bindings[item.AnalyzerId]).ToArray();
                if (members.Select(item => item.CausalCondition).Distinct(StringComparer.Ordinal).Count() != 1
                    || memberDecisions.Select(item => item.AnalyzerId).Distinct().Count() != 1
                    || memberBindings.Select(item => item.SemanticContractVersion).Distinct().Count() != 1
                    || memberBindings.Select(item => item.IdentityContractVersion).Distinct().Count() != 1)
                {
                    throw new InvalidOperationException("Shared-cause source groups require one causal condition and compatible producer contract.");
                }
                return new SharedCauseProofContract(
                    CandidateAnalysisIdentity.StableId("shared-cause-proof", group.Key.Value),
                    members.Select(item => item.HypothesisId).ToArray(), memberBindings[0].AnalyzerFamily,
                    memberBindings[0].SemanticContractVersion, memberBindings[0].IdentityContractVersion,
                    memberDecisions.SelectMany(item => item.Participants)
                        .GroupBy(item => item.ParticipantId.Value, StringComparer.Ordinal)
                        .ToDictionary(item => item.Key, item => item.Select(value => value.Role)
                            .Distinct(StringComparer.Ordinal).Single(), StringComparer.Ordinal),
                    members[0].CausalCondition,
                    string.Join("|", members.Select(item => item.AffectedLocus).Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)),
                    members.SelectMany(item => item.ApplicabilityPredicates).Distinct(StringComparer.Ordinal).ToArray(),
                    FindingCaseIdentity.SharedCauseDependencyClosureId(memberDecisions.SelectMany(item => item.DependencyIds)),
                    members.SelectMany(item => hypotheses[item.HypothesisId].SupportingEvidenceIds).Distinct().ToArray())
                {
                    AnalyzerVersion = memberBindings[0].AnalyzerVersion,
                };
            }).ToArray();
        const string populationId = "delivered-candidate-hypotheses";
        CoveragePopulationFactContract[] populations =
        [
            new CoveragePopulationFactContract(
                CandidateAnalysisIdentity.StableId("finding-coverage-population", request.CandidateAnalysis.OriginatingRunId.Value),
                request.CandidateAnalysis.AnalyzerId, populationId, "delivered candidate hypotheses"),
        ];
        CoverageMemberFactContract[] coverage = request.ConclusionFacts.Select(item => new CoverageMemberFactContract(
            CandidateAnalysisIdentity.StableId("finding-coverage-member", item.SourceFactId.Value),
            request.CandidateAnalysis.AnalyzerId, populationId, "delivered candidate hypotheses",
            item.HypothesisId, CoverageMemberState.Completed, "conclusion source fact produced",
            "none", null, [])).ToArray();
        return Create(new FindingCaseInputBuildRequest(
            request.PromotionPolicyId, request.PromotionPolicyVersion, request.ReconciliationPolicyId,
            request.ReconciliationPolicyVersion, request.ReconciliationActorId, request.AssessmentTime,
            request.CandidateAnalysis, evidence, recommendations, proofs, request.TaxonomySubjects,
            request.RetainedTaxonomyFacts, request.TaxonomyProjectionInputs, populations, coverage, [],
            request.PriorFindings, request.PriorCases, request.ProducerCompatibilities,
            request.RelatedFindingFacts, request.Boundaries)
        {
            ReconciliationCandidateFacts = request.ReconciliationCandidateFacts,
        });
    }

    public static FindingCaseInputContract Create(FindingCaseInputBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        HashSet<OpaqueId> hypotheses = request.CandidateAnalysis.Hypotheses
            .Select(item => item.HypothesisId).ToHashSet();
        if (request.TaxonomySubjects.Any(item => !hypotheses.Contains(item.HypothesisId))
            || request.TaxonomySubjects.Select(item => item.SubjectId).Distinct().Count() != request.TaxonomySubjects.Count)
        {
            throw new InvalidOperationException("Taxonomy source facts must bind one unique delivered hypothesis subject.");
        }

        TaxonomyClassificationFactContract[] taxonomy = request.RetainedTaxonomyFacts
            .Concat(request.TaxonomySubjects.SelectMany(TaxonomyClassificationProducer.Produce))
            .OrderBy(item => item.FactId.Value, StringComparer.Ordinal)
            .ToArray();
        FindingCaseInputContract input = new(
            ContractConstants.FindingCaseInputSchemaId, new ContractVersion(1, 0, 0), new OpaqueId("pending"),
            request.CandidateAnalysis.OriginatingRunId,
            request.PromotionPolicyId, request.PromotionPolicyVersion,
            request.ReconciliationPolicyId, request.ReconciliationPolicyVersion,
            request.ReconciliationActorId, request.AssessmentTime, request.CandidateAnalysis,
            request.FindingEvidenceFacts, request.FindingRecommendationFacts, request.SharedCauseProofs, taxonomy,
            request.TaxonomyProjectionInputs, request.CoveragePopulationFacts, request.CoverageMemberFacts,
            request.CoverageFailureFacts, request.PriorFindings, request.PriorCases,
            request.ProducerCompatibilities, request.RelatedFindingFacts, request.Boundaries)
        {
            ReconciliationCandidateFacts = request.ReconciliationCandidateFacts,
        };
        input = input with { InputId = FindingCaseIdentity.ComputeInputId(input) };
        FindingCaseContractInvariants.Validate(input);
        return input;
    }
}
