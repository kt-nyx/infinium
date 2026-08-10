using Infinium.Analysis.Conclusions;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.FindingCases;

public static class FindingCasePipeline
{
    public static FindingCaseContract Execute(FindingCaseInputContract input)
    {
        ArgumentNullException.ThrowIfNull(input);
        FindingCaseContractInvariants.Validate(input);

        IReadOnlyDictionary<OpaqueId, FindingConclusionAssessmentContract> conclusions =
            FindingConclusionProducer.Produce(input);
        Dictionary<OpaqueId, FindingEvidenceFactContract> evidenceFacts = input.FindingEvidenceFacts
            .ToDictionary(item => item.HypothesisId);
        Dictionary<OpaqueId, SharedCauseProofContract[]> proofsByHypothesis = input.SharedCauseProofs
            .SelectMany(proof => proof.HypothesisIds.Select(id => (id, proof)))
            .GroupBy(item => item.id)
            .ToDictionary(group => group.Key, group => group.Select(item => item.proof).ToArray());
        List<FindingPromotionAssessmentContract> promotions = [];
        List<FindingCaseAbstentionContract> abstentions = [];
        List<FindingDraft> supported = [];
        List<LeadDraft> leads = [];
        List<FindingRecommendationContract> recommendations = [];

        foreach (CandidateHypothesisContract hypothesis in input.CandidateAnalysis.Hypotheses
                     .OrderBy(item => item.HypothesisId.Value, StringComparer.Ordinal))
        {
            conclusions.TryGetValue(hypothesis.HypothesisId, out FindingConclusionAssessmentContract? conclusion);
            evidenceFacts.TryGetValue(hypothesis.HypothesisId, out FindingEvidenceFactContract? fact);
            proofsByHypothesis.TryGetValue(hypothesis.HypothesisId, out SharedCauseProofContract[]? causeProofs);
            bool state = hypothesis.State == AnalysisResultState.Present;
            bool confidence = hypothesis.Confidence is AnalysisConfidence.Plausible
                or AnalysisConfidence.StronglySupported or AnalysisConfidence.Confirmed;
            bool support = hypothesis.SupportingEvidenceIds.Count > 0 && fact?.EvidenceIds.Count > 0
                && fact.EvidenceIds.All(hypothesis.SupportingEvidenceIds.Contains);
            bool noDefeating = fact is not null && fact.DefeatingContradictionIds.Count == 0;
            bool noMissing = hypothesis.MissingInformation.Count == 0;
            bool severity = conclusion?.Severity is FindingSeverity.Advisory or FindingSeverity.Minor
                or FindingSeverity.Moderate or FindingSeverity.Major or FindingSeverity.Blocker;
            bool identity = conclusion is not null && causeProofs?.Length == 1;
            bool conclusionAvailable = conclusion is not null;
            bool leadEligibleState = hypothesis.State is not (AnalysisResultState.Failed or AnalysisResultState.Unsupported);
            FindingPromotionOutcome outcome = FindingCaseContractInvariants.ExpectedPromotionOutcome(
                state, confidence, support, noDefeating, noMissing, severity, identity,
                conclusionAvailable, leadEligibleState);
            string[] reasons = PromotionReasons(state, confidence, support, noDefeating, noMissing, severity, identity);
            OpaqueId promotionId = CandidateAnalysisIdentity.StableId(
                "finding-promotion", input.OriginatingRunId.Value, input.PromotionPolicyId.Value,
                hypothesis.HypothesisId.Value);
            promotions.Add(new FindingPromotionAssessmentContract(
                promotionId, hypothesis.HypothesisId, state, confidence, support, noDefeating,
                noMissing, severity, identity, conclusionAvailable, leadEligibleState, outcome, reasons));

            if (outcome == FindingPromotionOutcome.Abstained || conclusion is null)
            {
                OpaqueId abstentionId = CandidateAnalysisIdentity.StableId(
                    "finding-case-abstention", input.OriginatingRunId.Value, hypothesis.HypothesisId.Value);
                FindingCaseAbstentionContract abstention = new(
                    abstentionId, hypothesis.HypothesisId,
                    reasons.Length == 0 ? "The conclusion input is not closed." : string.Join("; ", reasons),
                    hypothesis.MissingInformation.Concat(reasons).Distinct(StringComparer.Ordinal).ToArray(),
                    hypothesis.SupportingEvidenceIds.Concat(hypothesis.ContradictingEvidenceIds).Distinct().ToArray());
                abstentions.Add(abstention);
                recommendations.Add(new FindingRecommendationContract(
                    CandidateAnalysisIdentity.StableId("recommendation", abstentionId.Value),
                    RecommendationKind.Abstention, null, abstentionId, null,
                    conclusion?.Action ?? "Obtain the missing typed evidence before drawing a conclusion.",
                    conclusion?.Uncertainty ?? abstention.Reason,
                    conclusion?.Reversibility ?? "Investigation changes no installed state.",
                    conclusion?.Risks ?? ["Premature remediation may be inapplicable."],
                    conclusion?.Verification ?? "Resolve the missing information before promotion.",
                    conclusion is null ? abstention.EvidenceIds : input.FindingRecommendationFacts
                        .Single(item => item.HypothesisId == hypothesis.HypothesisId).EvidenceIds));
                continue;
            }
            if (outcome == FindingPromotionOutcome.LeadOnly)
            {
                FindingCaseAbstentionContract leadAbstention = new(
                    CandidateAnalysisIdentity.StableId(
                        "finding-case-abstention", input.OriginatingRunId.Value, hypothesis.HypothesisId.Value),
                    hypothesis.HypothesisId,
                    reasons.Length == 0 ? "The lead does not satisfy the supported-finding threshold." : string.Join("; ", reasons),
                    hypothesis.MissingInformation.Concat(reasons).Distinct(StringComparer.Ordinal).ToArray(),
                    hypothesis.SupportingEvidenceIds.Concat(hypothesis.ContradictingEvidenceIds).Distinct().ToArray());
                abstentions.Add(leadAbstention);
                leads.Add(new LeadDraft(hypothesis, conclusion));
                recommendations.Add(new FindingRecommendationContract(
                    CandidateAnalysisIdentity.StableId("recommendation", promotionId.Value),
                    RecommendationKind.FurtherInvestigation, null, leadAbstention.AbstentionId, null,
                    conclusion.Action, conclusion.Uncertainty, conclusion.Reversibility,
                    conclusion.Risks, conclusion.Verification,
                    input.FindingRecommendationFacts.Single(item => item.HypothesisId == hypothesis.HypothesisId).EvidenceIds));
                continue;
            }
            supported.Add(new FindingDraft(
                hypothesis, conclusion, causeProofs![0], CandidateAnalysisIdentity.StableId(
                    "finding-occurrence", input.OriginatingRunId.Value, hypothesis.HypothesisId.Value)));
        }

        Dictionary<OpaqueId, OpaqueId> findingOccurrenceByHypothesis = supported
            .ToDictionary(item => item.Hypothesis.HypothesisId, item => item.OccurrenceId);
        (List<TaxonomyAssignmentContract> assignments, List<TaxonomyProjectionContract> projections,
            Dictionary<OpaqueId, IReadOnlyList<OpaqueId>> taxonomyByHypothesis,
            Dictionary<OpaqueId, OpaqueId> assignmentByFact) = BuildTaxonomy(input, findingOccurrenceByHypothesis);

        (List<FindingContract> findings, List<OccurrenceReconciliationContract> findingReconciliations,
            List<OccurrenceLineageContract> findingLineage) = ReconcileFindings(
                input, supported, taxonomyByHypothesis, assignments.ToDictionary(item => item.AssignmentId));
        foreach (FindingContract finding in findings)
        {
            FindingConclusionAssessmentContract conclusion = conclusions[finding.HypothesisId];
            recommendations.Add(new FindingRecommendationContract(
                CandidateAnalysisIdentity.StableId("recommendation", finding.FindingOccurrenceId.Value),
                conclusion.RecommendationKind, finding.FindingOccurrenceId, null, null,
                conclusion.Action, conclusion.Uncertainty, conclusion.Reversibility,
                conclusion.Risks, conclusion.Verification,
                input.FindingRecommendationFacts.Single(item => item.HypothesisId == finding.HypothesisId).EvidenceIds));
        }

        List<CaseDraft> caseDrafts = BuildCaseDrafts(input, findings, leads);
        (List<AnalysisCaseContract> cases, List<OccurrenceReconciliationContract> caseReconciliations,
            List<OccurrenceLineageContract> caseLineage) = ReconcileCases(
                input, caseDrafts, findings, findingReconciliations);
        (List<CoverageContract> coverage, List<FindingCaseGapContract> gaps) =
            BuildCoverage(input, assignmentByFact);

        FindingCaseContract result = new(
            ContractConstants.FindingCaseSchemaId, new ContractVersion(1, 0, 0), new OpaqueId("pending"),
            input.OriginatingRunId, input.InputId, input.PromotionPolicyId, input.PromotionPolicyVersion,
            input.ReconciliationPolicyId, input.ReconciliationPolicyVersion,
            promotions, abstentions, findings,
            recommendations.OrderBy(item => item.RecommendationId.Value, StringComparer.Ordinal).ToArray(),
            cases,
            findingReconciliations.Concat(caseReconciliations).OrderBy(item => item.AssessmentId.Value, StringComparer.Ordinal).ToArray(),
            findingLineage.Concat(caseLineage).OrderBy(item => item.EventId.Value, StringComparer.Ordinal).ToArray(),
            assignments, projections, coverage, input.CoverageFailureFacts, gaps, input.Boundaries,
            "no-safety-claim");
        result = result with { PayloadId = FindingCaseIdentity.ComputePayloadId(result) };
        FindingCaseContractInvariants.Validate(result);
        return result;
    }

    private static (List<FindingContract>, List<OccurrenceReconciliationContract>, List<OccurrenceLineageContract>)
        ReconcileFindings(
            FindingCaseInputContract input,
            IReadOnlyList<FindingDraft> drafts,
            IReadOnlyDictionary<OpaqueId, IReadOnlyList<OpaqueId>> taxonomyByHypothesis,
            Dictionary<OpaqueId, TaxonomyAssignmentContract> taxonomyAssignments)
    {
        List<CurrentFinding> current = drafts.OrderBy(item => item.OccurrenceId.Value, StringComparer.Ordinal)
            .Select(draft =>
            {
                IReadOnlyList<OpaqueId> taxonomy = taxonomyByHypothesis.GetValueOrDefault(draft.Hypothesis.HypothesisId, []);
                string conclusion = draft.Hypothesis.PredictedImpact;
                Sha256Fingerprint semantic = FindingCaseIdentity.FindingSemanticFingerprint(
                    conclusion, draft.Conclusion.Severity, draft.Hypothesis.Confidence,
                    draft.Conclusion.IdentityEnvelope,
                    taxonomy.Select(id => FindingCaseIdentity.TaxonomySemanticDescriptor(taxonomyAssignments[id])));
                return new CurrentFinding(draft, conclusion, taxonomy, semantic);
            }).ToList();
        Dictionary<(OpaqueId Current, OpaqueId Prior), GateEvaluation> matrix = [];
        foreach (CurrentFinding item in current)
        {
            foreach (PriorFindingContract prior in input.PriorFindings)
            {
                matrix[(item.Draft.OccurrenceId, prior.FindingOccurrenceId)] = Gates(
                    prior.IdentityEnvelope, item.Draft.Conclusion.IdentityEnvelope,
                    prior.ProofAvailable, input.ProducerCompatibilities);
            }
        }

        Dictionary<OpaqueId, PriorFindingContract[]> candidatesByCurrent = current.ToDictionary(
            item => item.Draft.OccurrenceId,
            item =>
            {
                ReconciliationCandidateFactContract? scope = input.ReconciliationCandidateFacts.SingleOrDefault(
                    fact => fact.CurrentHypothesisId == item.Draft.Hypothesis.HypothesisId);
                return (scope is null
                        ? []
                        : input.PriorFindings.Where(prior => scope.PriorOccurrenceIds.Contains(prior.FindingOccurrenceId)).ToArray())
                    .OrderBy(prior => prior.FindingOccurrenceId.Value, StringComparer.Ordinal).ToArray();
            });
        Dictionary<OpaqueId, int> exactPriorDegree = input.PriorFindings.ToDictionary(
            prior => prior.FindingOccurrenceId,
            prior => current.Count(item => candidatesByCurrent[item.Draft.OccurrenceId].Contains(prior)
                && AllEquivalent(matrix[(item.Draft.OccurrenceId, prior.FindingOccurrenceId)].Gates)));
        Dictionary<OpaqueId, int> relatedPriorDegree = input.PriorFindings.ToDictionary(
            prior => prior.FindingOccurrenceId,
            prior => current.Count(item =>
            {
                PriorFindingContract[] scoped = candidatesByCurrent[item.Draft.OccurrenceId];
                bool hasExact = scoped.Any(candidate =>
                    AllEquivalent(matrix[(item.Draft.OccurrenceId, candidate.FindingOccurrenceId)].Gates));
                ReconciliationGatesContract gates = matrix[(item.Draft.OccurrenceId, prior.FindingOccurrenceId)].Gates;
                return !hasExact && scoped.Contains(prior) && prior.ProofAvailable && !AllEquivalent(gates) && NoUnknown(gates)
                    && input.RelatedFindingFacts.Any(relation =>
                        relation.CurrentHypothesisId == item.Draft.Hypothesis.HypothesisId
                        && relation.PriorOccurrenceId == prior.FindingOccurrenceId);
            }));
        HashSet<OpaqueId> accountedPrior = [];
        List<FindingContract> findings = [];
        List<OccurrenceReconciliationContract> reconciliations = [];
        List<OccurrenceLineageContract> lineage = [];
        foreach (CurrentFinding item in current)
        {
            PriorFindingContract[] candidatePriors = candidatesByCurrent[item.Draft.OccurrenceId];
            OpaqueId[] considered = candidatePriors.Select(prior => prior.FindingOccurrenceId)
                .Append(item.Draft.OccurrenceId).Distinct().ToArray();
            PriorFindingContract[] exact = candidatePriors.Where(prior =>
                AllEquivalent(matrix[(item.Draft.OccurrenceId, prior.FindingOccurrenceId)].Gates)).ToArray();
            PriorFindingContract[] related = exact.Length == 0 ? candidatePriors.Where(prior =>
            {
                ReconciliationGatesContract gates = matrix[(item.Draft.OccurrenceId, prior.FindingOccurrenceId)].Gates;
                return prior.ProofAvailable && !AllEquivalent(gates) && NoUnknown(gates)
                    && input.RelatedFindingFacts.Any(relation =>
                        relation.CurrentHypothesisId == item.Draft.Hypothesis.HypothesisId
                        && relation.PriorOccurrenceId == prior.FindingOccurrenceId);
            }).ToArray() : [];
            PriorFindingContract[] unknown = exact.Length == 0 && related.Length == 0
                ? candidatePriors.Where(prior => !prior.ProofAvailable
                    || HasUnknown(matrix[(item.Draft.OccurrenceId, prior.FindingOccurrenceId)].Gates)).ToArray() : [];
            PriorFindingContract? continuation = exact.Length == 1
                && exactPriorDegree[exact[0].FindingOccurrenceId] == 1 ? exact[0] : null;
            OpaqueId logicalId = continuation?.LogicalFindingId ?? CandidateAnalysisIdentity.StableId(
                "logical-finding-allocation", input.OriginatingRunId.Value, item.Draft.OccurrenceId.Value);
            findings.Add(new FindingContract(
                item.Draft.OccurrenceId, logicalId, input.OriginatingRunId, item.Draft.Hypothesis.CandidateId,
                item.Draft.Hypothesis.HypothesisId, item.Conclusion, item.Draft.Conclusion.Severity,
                item.Draft.Hypothesis.Confidence, item.Draft.Hypothesis.SupportingEvidenceIds,
                FindingCaseIdentity.EnvelopeId(item.Draft.Conclusion.IdentityEnvelope), item.Draft.Conclusion.IdentityEnvelope,
                FindingCaseIdentity.EnvelopeId(ToIdentity(item.Draft.CauseProof)), ToIdentity(item.Draft.CauseProof),
                item.TaxonomyIds, item.Semantic, continuation?.FindingOccurrenceId));

            if (continuation is not null)
            {
                accountedPrior.Add(continuation.FindingOccurrenceId);
                ReconciliationOutcome outcome = continuation.SemanticFingerprint == item.Semantic
                    ? ReconciliationOutcome.ExactContinuation : ReconciliationOutcome.AnalyticalRevision;
                AddAssessment("finding", continuation.FindingOccurrenceId, item.Draft.OccurrenceId,
                    matrix[(item.Draft.OccurrenceId, continuation.FindingOccurrenceId)], outcome,
                    considered, item.Draft.Hypothesis.SupportingEvidenceIds,
                    input, reconciliations, lineage, continuation.LogicalFindingId, logicalId);
            }
            else if (exact.Length > 0)
            {
                foreach (PriorFindingContract prior in exact)
                {
                    accountedPrior.Add(prior.FindingOccurrenceId);
                }

                AddAssessment("finding", exact[0].FindingOccurrenceId, item.Draft.OccurrenceId,
                    matrix[(item.Draft.OccurrenceId, exact[0].FindingOccurrenceId)], ReconciliationOutcome.Ambiguous,
                    exact.Length > 1 ? considered : current.Where(candidate =>
                            candidatesByCurrent[candidate.Draft.OccurrenceId].Contains(exact[0])
                            && AllEquivalent(matrix[(candidate.Draft.OccurrenceId, exact[0].FindingOccurrenceId)].Gates))
                        .Select(candidate => candidate.Draft.OccurrenceId).Append(exact[0].FindingOccurrenceId).Distinct().ToArray(),
                    item.Draft.Hypothesis.SupportingEvidenceIds,
                    input, reconciliations, lineage, null, logicalId);
            }
            else if (related.Length == 1 && relatedPriorDegree[related[0].FindingOccurrenceId] == 1)
            {
                PriorFindingContract prior = related[0];
                accountedPrior.Add(prior.FindingOccurrenceId);
                AddAssessment("finding", prior.FindingOccurrenceId, item.Draft.OccurrenceId,
                    matrix[(item.Draft.OccurrenceId, prior.FindingOccurrenceId)], ReconciliationOutcome.RelatedFollowUp,
                    considered, item.Draft.Hypothesis.SupportingEvidenceIds.Concat(
                        input.RelatedFindingFacts.Single(relation =>
                            relation.CurrentHypothesisId == item.Draft.Hypothesis.HypothesisId
                            && relation.PriorOccurrenceId == prior.FindingOccurrenceId).EvidenceIds).Distinct().ToArray(),
                    input, reconciliations, lineage, prior.LogicalFindingId, logicalId);
            }
            else if (related.Length > 0)
            {
                foreach (PriorFindingContract prior in related)
                {
                    accountedPrior.Add(prior.FindingOccurrenceId);
                }

                AddAssessment("finding", related[0].FindingOccurrenceId, item.Draft.OccurrenceId,
                    matrix[(item.Draft.OccurrenceId, related[0].FindingOccurrenceId)], ReconciliationOutcome.Ambiguous,
                    related.Length > 1 ? considered : current.Where(candidate =>
                            candidatesByCurrent[candidate.Draft.OccurrenceId].Contains(related[0])
                            && input.RelatedFindingFacts.Any(relation =>
                                relation.CurrentHypothesisId == candidate.Draft.Hypothesis.HypothesisId
                                && relation.PriorOccurrenceId == related[0].FindingOccurrenceId))
                        .Select(candidate => candidate.Draft.OccurrenceId).Append(related[0].FindingOccurrenceId).Distinct().ToArray(),
                    item.Draft.Hypothesis.SupportingEvidenceIds.Concat(input.RelatedFindingFacts.Where(relation =>
                            relation.CurrentHypothesisId == item.Draft.Hypothesis.HypothesisId
                            && related.Any(prior => prior.FindingOccurrenceId == relation.PriorOccurrenceId))
                        .SelectMany(relation => relation.EvidenceIds)).Distinct().ToArray(),
                    input, reconciliations, lineage, null, logicalId);
            }
            else if (unknown.Length > 0)
            {
                foreach (PriorFindingContract prior in unknown)
                {
                    accountedPrior.Add(prior.FindingOccurrenceId);
                }

                AddAssessment("finding", unknown[0].FindingOccurrenceId, item.Draft.OccurrenceId,
                    matrix[(item.Draft.OccurrenceId, unknown[0].FindingOccurrenceId)], ReconciliationOutcome.Unknown,
                    considered, item.Draft.Hypothesis.SupportingEvidenceIds,
                    input, reconciliations, lineage, null, logicalId);
            }
            else if (candidatePriors.Length > 0)
            {
                PriorFindingContract prior = candidatePriors[0];
                foreach (PriorFindingContract candidate in candidatePriors)
                {
                    accountedPrior.Add(candidate.FindingOccurrenceId);
                }
                AddAssessment("finding", null, item.Draft.OccurrenceId,
                    matrix[(item.Draft.OccurrenceId, prior.FindingOccurrenceId)], ReconciliationOutcome.NewDistinct,
                    considered,
                    item.Draft.Hypothesis.SupportingEvidenceIds, input, reconciliations, lineage, null, logicalId);
            }
            else
            {
                AddAssessment("finding", null, item.Draft.OccurrenceId,
                    new GateEvaluation(new ReconciliationGatesContract(
                        ReconciliationGateState.NotEvaluated,
                        ReconciliationGateState.ProvenEquivalent,
                        ReconciliationGateState.ProvenEquivalent,
                        ReconciliationGateState.ProvenEquivalent), []), ReconciliationOutcome.NewDistinct,
                    [item.Draft.OccurrenceId], item.Draft.Hypothesis.SupportingEvidenceIds,
                    input, reconciliations, lineage, null, logicalId);
            }
        }
        AddAbsenceAssessments("finding", input.PriorFindings, value => value.FindingOccurrenceId,
            value => value.ApplicablePopulationIds, accountedPrior, input, reconciliations);
        return (findings, reconciliations, lineage);
    }

    private static List<CaseDraft> BuildCaseDrafts(
        FindingCaseInputContract input, IReadOnlyList<FindingContract> findings, IReadOnlyList<LeadDraft> leads)
    {
        Dictionary<OpaqueId, FindingContract> findingByHypothesis = findings.ToDictionary(item => item.HypothesisId);
        List<CaseDraft> drafts = [];
        foreach (SharedCauseProofContract proof in input.SharedCauseProofs.OrderBy(item => item.ProofId.Value, StringComparer.Ordinal))
        {
            FindingContract[] members = proof.HypothesisIds.Where(findingByHypothesis.ContainsKey)
                .Select(id => findingByHypothesis[id]).OrderBy(item => item.FindingOccurrenceId.Value, StringComparer.Ordinal).ToArray();
            if (members.Length == 0)
            {
                continue;
            }

            IdentityEnvelopeContract identity = ToIdentity(proof);
            drafts.Add(new CaseDraft(
                CandidateAnalysisIdentity.StableId("case-occurrence", input.OriginatingRunId.Value, "supported", proof.ProofId.Value),
                CaseOccurrenceKind.Supported, identity,
                members.Select(item => item.FindingOccurrenceId).ToArray(),
                members.Select(item => item.CandidateId).Distinct().ToArray(),
                members.Select(item => item.HypothesisId).ToArray(), proof.EvidenceIds));
        }
        Dictionary<OpaqueId, LeadDraft> leadByHypothesis = leads.ToDictionary(item => item.Hypothesis.HypothesisId);
        HashSet<OpaqueId> groupedLeads = [];
        foreach (SharedCauseProofContract proof in input.SharedCauseProofs.OrderBy(item => item.ProofId.Value, StringComparer.Ordinal))
        {
            LeadDraft[] members = proof.HypothesisIds.Where(leadByHypothesis.ContainsKey)
                .Select(id => leadByHypothesis[id]).OrderBy(item => item.Hypothesis.HypothesisId.Value, StringComparer.Ordinal)
                .ToArray();
            if (members.Length == 0)
            {
                continue;
            }
            foreach (LeadDraft member in members)
            {
                groupedLeads.Add(member.Hypothesis.HypothesisId);
            }
            drafts.Add(new CaseDraft(
                CandidateAnalysisIdentity.StableId("case-occurrence", input.OriginatingRunId.Value, "lead-only", proof.ProofId.Value),
                CaseOccurrenceKind.LeadOnly, ToIdentity(proof), [],
                members.Select(item => item.Hypothesis.CandidateId).Distinct().ToArray(),
                members.Select(item => item.Hypothesis.HypothesisId).ToArray(), proof.EvidenceIds));
        }
        foreach (LeadDraft lead in leads.Where(item => !groupedLeads.Contains(item.Hypothesis.HypothesisId))
                     .OrderBy(item => item.Hypothesis.HypothesisId.Value, StringComparer.Ordinal))
        {
            drafts.Add(new CaseDraft(
                CandidateAnalysisIdentity.StableId("case-occurrence", input.OriginatingRunId.Value, "lead-only", lead.Hypothesis.HypothesisId.Value),
                CaseOccurrenceKind.LeadOnly, lead.Conclusion.IdentityEnvelope, [], [lead.Hypothesis.CandidateId],
                [lead.Hypothesis.HypothesisId], lead.Hypothesis.SupportingEvidenceIds));
        }
        return drafts;
    }

    private static (List<AnalysisCaseContract>, List<OccurrenceReconciliationContract>, List<OccurrenceLineageContract>)
        ReconcileCases(
            FindingCaseInputContract input,
            IReadOnlyList<CaseDraft> drafts,
            IReadOnlyList<FindingContract> findings,
            IReadOnlyList<OccurrenceReconciliationContract> findingAssessments)
    {
        List<AnalysisCaseContract> cases = [];
        List<OccurrenceReconciliationContract> reconciliations = [];
        List<OccurrenceLineageContract> lineage = [];
        HashSet<OpaqueId> accountedPrior = [];
        Dictionary<(OpaqueId Current, OpaqueId Prior), GateEvaluation> matrix = [];
        Dictionary<(OpaqueId Current, OpaqueId Prior), bool> memberClosure = [];
        foreach (CaseDraft draft in drafts)
        {
            foreach (PriorCaseContract prior in input.PriorCases)
            {
                GateEvaluation evaluation = Gates(prior.IdentityEnvelope, draft.IdentityEnvelope,
                    prior.ProofAvailable, input.ProducerCompatibilities, caseIdentity: true);
                OccurrenceReconciliationContract[] memberAssessments = RelevantMemberAssessments(prior, draft, findingAssessments);
                bool supportedToLead = prior.Kind == CaseOccurrenceKind.Supported
                    && draft.Kind == CaseOccurrenceKind.LeadOnly;
                bool memberFirstClosed = (draft.Kind == CaseOccurrenceKind.LeadOnly
                        && prior.Kind == CaseOccurrenceKind.LeadOnly)
                    || (prior.Kind == CaseOccurrenceKind.LeadOnly && draft.Kind == CaseOccurrenceKind.Supported)
                    || MemberSetHasClosedAssessmentCoverage(prior, draft, memberAssessments);
                memberClosure[(draft.OccurrenceId, prior.CaseOccurrenceId)] = memberFirstClosed;
                if (supportedToLead)
                {
                    evaluation = evaluation with { Gates = evaluation.Gates with { Causal = ReconciliationGateState.ProvenDifferent } };
                }
                matrix[(draft.OccurrenceId, prior.CaseOccurrenceId)] = evaluation;
            }
        }

        Dictionary<OpaqueId, int> exactPriorDegree = input.PriorCases.ToDictionary(
            prior => prior.CaseOccurrenceId,
            prior => drafts.Count(draft => memberClosure[(draft.OccurrenceId, prior.CaseOccurrenceId)]
                && AllEquivalent(matrix[(draft.OccurrenceId, prior.CaseOccurrenceId)].Gates)));
        Dictionary<OpaqueId, int> relatedPriorDegree = input.PriorCases.ToDictionary(
            prior => prior.CaseOccurrenceId,
            prior => drafts.Count(draft =>
            {
                ReconciliationGatesContract candidate = matrix[(draft.OccurrenceId, prior.CaseOccurrenceId)].Gates;
                return memberClosure[(draft.OccurrenceId, prior.CaseOccurrenceId)]
                    && candidate.Causal == ReconciliationGateState.ProvenEquivalent
                    && !AllEquivalent(candidate) && NoUnknown(candidate);
            }));
        foreach (CaseDraft draft in drafts.OrderBy(item => item.OccurrenceId.Value, StringComparer.Ordinal))
        {
            PriorCaseContract[] exact = input.PriorCases.Where(prior =>
                memberClosure[(draft.OccurrenceId, prior.CaseOccurrenceId)]
                && AllEquivalent(matrix[(draft.OccurrenceId, prior.CaseOccurrenceId)].Gates)).ToArray();
            PriorCaseContract? matched = exact.Length == 1 && exactPriorDegree[exact[0].CaseOccurrenceId] == 1 ? exact[0] : null;
            bool promotesLead = matched?.Kind == CaseOccurrenceKind.LeadOnly && draft.Kind == CaseOccurrenceKind.Supported;
            OpaqueId logicalId = matched is not null && !promotesLead ? matched.LogicalCaseId
                : CandidateAnalysisIdentity.StableId("logical-case-allocation", input.OriginatingRunId.Value,
                    draft.OccurrenceId.Value);
            IReadOnlyList<OpaqueId> semanticMembers = draft.Kind == CaseOccurrenceKind.Supported
                ? draft.FindingOccurrenceIds.Select(id => findings.Single(finding => finding.FindingOccurrenceId == id).LogicalFindingId).ToArray()
                : [];
            Sha256Fingerprint semantic = FindingCaseIdentity.CaseSemanticFingerprint(
                draft.Kind, draft.IdentityEnvelope, semanticMembers);
            cases.Add(new AnalysisCaseContract(
                draft.OccurrenceId, logicalId, input.OriginatingRunId, draft.Kind,
                draft.FindingOccurrenceIds, draft.CandidateIds, draft.HypothesisIds,
                draft.IdentityEnvelope.CausalCondition, draft.CauseProofEvidenceIds,
                FindingCaseIdentity.EnvelopeId(draft.IdentityEnvelope), draft.IdentityEnvelope,
                semantic, matched?.CaseOccurrenceId, AffectsReadiness: false));

            if (matched is not null)
            {
                accountedPrior.Add(matched.CaseOccurrenceId);
                OccurrenceReconciliationContract[] members = RelevantMemberAssessments(matched, draft, findingAssessments);
                if (promotesLead)
                {
                    lineage.Add(new OccurrenceLineageContract(
                        CandidateAnalysisIdentity.StableId("lineage", "case", matched.CaseOccurrenceId.Value,
                            draft.OccurrenceId.Value, LineageKind.PromotesLead.ToString()),
                        LineageKind.PromotesLead, [matched.CaseOccurrenceId], [draft.OccurrenceId], null));
                }
                else
                {
                    AddAssessment("case", matched.CaseOccurrenceId, draft.OccurrenceId,
                        matrix[(draft.OccurrenceId, matched.CaseOccurrenceId)], ReconciliationOutcome.ExactContinuation,
                        [matched.CaseOccurrenceId, draft.OccurrenceId], draft.CauseProofEvidenceIds
                            .Concat(members.Select(item => item.AssessmentId)).Distinct().ToArray(),
                        input, reconciliations, lineage, matched.LogicalCaseId, logicalId);
                }
            }
            else if (exact.Length > 0)
            {
                foreach (PriorCaseContract prior in exact)
                {
                    accountedPrior.Add(prior.CaseOccurrenceId);
                }

                AddAssessment("case", exact[0].CaseOccurrenceId, draft.OccurrenceId,
                    matrix[(draft.OccurrenceId, exact[0].CaseOccurrenceId)], ReconciliationOutcome.Ambiguous,
                    exact.Length > 1
                        ? exact.Select(item => item.CaseOccurrenceId).Append(draft.OccurrenceId).ToArray()
                        : drafts.Where(candidate => AllEquivalent(matrix[(candidate.OccurrenceId, exact[0].CaseOccurrenceId)].Gates))
                            .Select(candidate => candidate.OccurrenceId).Append(exact[0].CaseOccurrenceId).Distinct().ToArray(),
                    draft.CauseProofEvidenceIds,
                    input, reconciliations, lineage, null, logicalId);
            }
            else
            {
                PriorCaseContract[] unknown = input.PriorCases.Where(prior =>
                    HasUnknown(matrix[(draft.OccurrenceId, prior.CaseOccurrenceId)].Gates)).ToArray();
                if (unknown.Length > 0)
                {
                    foreach (PriorCaseContract prior in unknown)
                    {
                        accountedPrior.Add(prior.CaseOccurrenceId);
                    }

                    AddAssessment("case", unknown[0].CaseOccurrenceId, draft.OccurrenceId,
                        matrix[(draft.OccurrenceId, unknown[0].CaseOccurrenceId)], ReconciliationOutcome.Unknown,
                        unknown.Select(item => item.CaseOccurrenceId).Append(draft.OccurrenceId).ToArray(), draft.CauseProofEvidenceIds,
                        input, reconciliations, lineage, null, logicalId);
                }
                else if (input.PriorCases.Count > 0)
                {
                    PriorCaseContract[] related = input.PriorCases.Where(prior =>
                    {
                        ReconciliationGatesContract candidate = matrix[(draft.OccurrenceId, prior.CaseOccurrenceId)].Gates;
                        return memberClosure[(draft.OccurrenceId, prior.CaseOccurrenceId)]
                            && candidate.Causal == ReconciliationGateState.ProvenEquivalent
                            && !AllEquivalent(candidate) && NoUnknown(candidate);
                    }).ToArray();
                    if (related.Length > 1 || (related.Length == 1
                        && relatedPriorDegree[related[0].CaseOccurrenceId] > 1))
                    {
                        foreach (PriorCaseContract prior in related)
                        {
                            accountedPrior.Add(prior.CaseOccurrenceId);
                        }
                        AddAssessment("case", related[0].CaseOccurrenceId, draft.OccurrenceId,
                            matrix[(draft.OccurrenceId, related[0].CaseOccurrenceId)], ReconciliationOutcome.Ambiguous,
                            related.Length > 1
                                ? related.Select(item => item.CaseOccurrenceId).Append(draft.OccurrenceId).Distinct().ToArray()
                                : drafts.Where(candidate =>
                                        memberClosure[(candidate.OccurrenceId, related[0].CaseOccurrenceId)]
                                        && matrix[(candidate.OccurrenceId, related[0].CaseOccurrenceId)].Gates.Causal
                                            == ReconciliationGateState.ProvenEquivalent)
                                    .Select(candidate => candidate.OccurrenceId).Append(related[0].CaseOccurrenceId).Distinct().ToArray(),
                            draft.CauseProofEvidenceIds,
                            input, reconciliations, lineage, null, logicalId);
                    }
                    else
                    {
                        PriorCaseContract prior = related.Length == 1 ? related[0]
                            : input.PriorCases.OrderBy(item => item.CaseOccurrenceId.Value, StringComparer.Ordinal).First();
                        GateEvaluation evaluation = matrix[(draft.OccurrenceId, prior.CaseOccurrenceId)];
                        ReconciliationOutcome outcome = related.Length == 1
                            ? ReconciliationOutcome.RelatedFollowUp : ReconciliationOutcome.NewDistinct;
                        if (outcome == ReconciliationOutcome.RelatedFollowUp)
                        {
                            accountedPrior.Add(prior.CaseOccurrenceId);
                        }
                        AddAssessment("case", outcome == ReconciliationOutcome.NewDistinct ? null : prior.CaseOccurrenceId,
                            draft.OccurrenceId, evaluation, outcome,
                            outcome == ReconciliationOutcome.NewDistinct
                                ? [draft.OccurrenceId]
                                : [prior.CaseOccurrenceId, draft.OccurrenceId],
                            draft.CauseProofEvidenceIds,
                            input, reconciliations, lineage,
                            outcome == ReconciliationOutcome.RelatedFollowUp ? prior.LogicalCaseId : null, logicalId);
                    }
                }
                else
                {
                    AddAssessment("case", null, draft.OccurrenceId,
                        new GateEvaluation(EmptyGates(), []), ReconciliationOutcome.NewDistinct,
                        [draft.OccurrenceId], draft.CauseProofEvidenceIds,
                        input, reconciliations, lineage, null, logicalId);
                }
            }
        }
        AddAbsenceAssessments("case", input.PriorCases, value => value.CaseOccurrenceId,
            value => value.ApplicablePopulationIds, accountedPrior, input, reconciliations);
        return (cases, reconciliations, lineage);
    }

    private static OccurrenceReconciliationContract[] RelevantMemberAssessments(
        PriorCaseContract prior, CaseDraft current, IReadOnlyList<OccurrenceReconciliationContract> assessments) =>
        assessments.Where(item => item.SubjectKind == "finding"
            && ((item.PriorOccurrenceId is not null && prior.FindingOccurrenceIds.Contains(item.PriorOccurrenceId))
                || (item.CurrentOccurrenceId is not null && current.FindingOccurrenceIds.Contains(item.CurrentOccurrenceId))))
            .ToArray();

    private static bool MemberSetHasClosedAssessmentCoverage(
        PriorCaseContract prior, CaseDraft current, IReadOnlyList<OccurrenceReconciliationContract> assessments)
    {
        bool closed = assessments.All(item => item.Outcome switch
        {
            ReconciliationOutcome.ExactContinuation or ReconciliationOutcome.AnalyticalRevision =>
                item.PriorOccurrenceId is not null && prior.FindingOccurrenceIds.Contains(item.PriorOccurrenceId)
                && item.CurrentOccurrenceId is not null && current.FindingOccurrenceIds.Contains(item.CurrentOccurrenceId),
            ReconciliationOutcome.NewDistinct => item.PriorOccurrenceId is null
                && item.CurrentOccurrenceId is not null && current.FindingOccurrenceIds.Contains(item.CurrentOccurrenceId),
            ReconciliationOutcome.NotObserved => item.PriorOccurrenceId is not null
                && prior.FindingOccurrenceIds.Contains(item.PriorOccurrenceId) && item.CurrentOccurrenceId is null,
            _ => false,
        });
        return closed
            && prior.FindingOccurrenceIds.All(id => assessments.Count(item => item.PriorOccurrenceId == id) == 1)
            && current.FindingOccurrenceIds.All(id => assessments.Count(item => item.CurrentOccurrenceId == id) == 1);
    }

    private static void AddAssessment(
        string subjectKind, OpaqueId? priorId, OpaqueId currentId, GateEvaluation evaluation,
        ReconciliationOutcome outcome, OpaqueId[] considered,
        IReadOnlyList<OpaqueId> proof, FindingCaseInputContract input,
        List<OccurrenceReconciliationContract> assessments, List<OccurrenceLineageContract> lineage,
        OpaqueId? priorLogicalId, OpaqueId currentLogicalId, bool promotesLead = false)
    {
        ReconciliationGatesContract gates = evaluation.Gates;
        List<string> gaps = [];
        if (outcome == ReconciliationOutcome.Ambiguous)
        {
            int candidateCount = Math.Max(2, considered.Length - 1);
            string count = candidateCount == 2 ? "two" : candidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            gaps.Add($"{count} fully proven prior candidates prevent automatic reuse");
        }

        if (outcome == ReconciliationOutcome.Unknown)
        {
            gaps.Add(subjectKind == "finding"
                ? "retained causal proof missing"
                : "required continuity or member-finding proof unavailable");
        }

        OpaqueId assessmentId = CandidateAnalysisIdentity.StableId(
            "reconciliation", subjectKind, priorId?.Value ?? "no-predecessor", currentId.Value, outcome.ToString(),
            input.ReconciliationPolicyId.Value, input.ReconciliationPolicyVersion.ToString());
        assessments.Add(new OccurrenceReconciliationContract(
            assessmentId, subjectKind, priorId, currentId, gates, outcome, gaps,
            considered.Distinct().OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
            proof.Distinct().DefaultIfEmpty(priorId ?? currentId).ToArray(), input.ReconciliationPolicyVersion,
            "automatic", input.ReconciliationActorId, input.AssessmentTime,
            VisibleByDefault: true));
        if ((outcome is ReconciliationOutcome.AnalyticalRevision or ReconciliationOutcome.RelatedFollowUp
                || promotesLead) && priorId is not null)
        {
            LineageKind kind = promotesLead ? LineageKind.PromotesLead
                : outcome == ReconciliationOutcome.AnalyticalRevision ? LineageKind.AnalyticalRevision
                : LineageKind.RelatedFollowUp;
            lineage.Add(new OccurrenceLineageContract(
                CandidateAnalysisIdentity.StableId("lineage", subjectKind, priorId.Value, currentId.Value, kind.ToString()),
                kind, [priorId], [currentId], assessmentId));
        }
    }

    private static void AddAbsenceAssessments<T>(
        string subjectKind, IReadOnlyList<T> priors, Func<T, OpaqueId> id,
        Func<T, IReadOnlyList<string>> populations, HashSet<OpaqueId> accounted,
        FindingCaseInputContract input, List<OccurrenceReconciliationContract> assessments)
    {
        Dictionary<string, CoverageMemberFactContract[]> members = input.CoverageMemberFacts
            .GroupBy(item => item.PopulationId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (T prior in priors.Where(item => !accounted.Contains(id(item))).OrderBy(item => id(item).Value, StringComparer.Ordinal))
        {
            IReadOnlyList<string> applicable = populations(prior);
            bool observed = applicable.Count > 0 && applicable.All(population =>
                input.CoveragePopulationFacts.Any(definition => definition.PopulationId == population)
                && input.CoveragePopulationFacts.Single(definition => definition.PopulationId == population).EvidenceIds.Count > 0
                && members.TryGetValue(population, out CoverageMemberFactContract[]? populationMembers)
                && populationMembers.Length > 0
                && populationMembers.All(member => member.State == CoverageMemberState.Completed));
            ReconciliationOutcome outcome = observed ? ReconciliationOutcome.NotObserved : ReconciliationOutcome.NotEvaluated;
            OpaqueId priorId = id(prior);
            assessments.Add(new OccurrenceReconciliationContract(
                CandidateAnalysisIdentity.StableId("reconciliation", subjectKind, priorId.Value, outcome.ToString()),
                subjectKind, priorId, null, observed
                    ? EmptyGates() with { Applicability = ReconciliationGateState.ProvenEquivalent }
                    : EmptyGates(), outcome,
                observed ? ["absence is not verified resolution"] :
                ["applicable analysis " + applicable.SelectMany(population =>
                        members.GetValueOrDefault(population, [])).Select(member => member.Reason)
                    .FirstOrDefault(reason => !string.IsNullOrWhiteSpace(reason)) ?? "not evaluated"], [priorId],
                input.CoveragePopulationFacts.Where(item => applicable.Contains(item.PopulationId))
                    .SelectMany(item => item.EvidenceIds)
                    .Concat(input.CoveragePopulationFacts.Where(item => applicable.Contains(item.PopulationId))
                        .Any(item => item.EvidenceIds.Count > 0) ? [] : applicable.SelectMany(population => members.GetValueOrDefault(population, []))
                        .Select(item => item.FactId)).Distinct().ToArray(), input.ReconciliationPolicyVersion, "automatic",
                input.ReconciliationActorId, input.AssessmentTime, VisibleByDefault: true));
        }
    }

    private static GateEvaluation Gates(
        IdentityEnvelopeContract prior, IdentityEnvelopeContract current, bool proofAvailable,
        IReadOnlyList<ProducerCompatibilityContract> compatibilities,
        bool caseIdentity = false)
    {
        ReconciliationGateState producer;
        IReadOnlyList<OpaqueId> producerEvidence = [];
        if (prior.AnalyzerFamily == current.AnalyzerFamily
            && prior.AnalyzerVersion == current.AnalyzerVersion
            && prior.SemanticContractVersion == current.SemanticContractVersion
            && prior.IdentityContractVersion == current.IdentityContractVersion)
        {
            producer = ReconciliationGateState.ProvenEquivalent;
        }
        else
        {
            ProducerCompatibilityContract? declared = compatibilities.SingleOrDefault(item =>
                item.PriorAnalyzerFamily == prior.AnalyzerFamily
                && item.PriorAnalyzerVersion == prior.AnalyzerVersion
                && item.PriorSemanticContractVersion == prior.SemanticContractVersion
                && item.PriorIdentityContractVersion == prior.IdentityContractVersion
                && item.CurrentAnalyzerFamily == current.AnalyzerFamily
                && item.CurrentAnalyzerVersion == current.AnalyzerVersion
                && item.CurrentSemanticContractVersion == current.SemanticContractVersion
                && item.CurrentIdentityContractVersion == current.IdentityContractVersion);
            producer = declared is null ? ReconciliationGateState.Unknown
                : declared.Compatible ? ReconciliationGateState.ProvenEquivalent : ReconciliationGateState.ProvenDifferent;
            producerEvidence = declared?.EvidenceIds ?? [];
        }
        return new GateEvaluation(new ReconciliationGatesContract(
            proofAvailable
                ? Equal(caseIdentity ? prior.CausalCondition
                        : prior.CausalCondition + "\n" + prior.AffectedLocus + "\n" + ParticipantDescriptor(prior),
                    caseIdentity ? current.CausalCondition
                        : current.CausalCondition + "\n" + current.AffectedLocus + "\n" + ParticipantDescriptor(current))
                : ReconciliationGateState.Unknown,
            Equal(string.Join("\n", prior.ApplicabilityPredicates.Order(StringComparer.Ordinal)),
                string.Join("\n", current.ApplicabilityPredicates.Order(StringComparer.Ordinal))),
            Equal(prior.DependencyClosureId.Value, current.DependencyClosureId.Value), producer), producerEvidence);
    }

    private static (List<TaxonomyAssignmentContract>, List<TaxonomyProjectionContract>,
        Dictionary<OpaqueId, IReadOnlyList<OpaqueId>>, Dictionary<OpaqueId, OpaqueId>) BuildTaxonomy(
        FindingCaseInputContract input, IReadOnlyDictionary<OpaqueId, OpaqueId> findingOccurrences)
    {
        List<TaxonomyAssignmentContract> assignments = [];
        Dictionary<OpaqueId, OpaqueId> assignmentByFact = [];
        Dictionary<OpaqueId, List<OpaqueId>> byHypothesis = [];
        foreach (TaxonomyClassificationFactContract fact in input.TaxonomyClassificationFacts
                     .OrderBy(item => item.FactId.Value, StringComparer.Ordinal))
        {
            OpaqueId subjectId = findingOccurrences.GetValueOrDefault(fact.HypothesisId, fact.HypothesisId);
            string subjectType = findingOccurrences.ContainsKey(fact.HypothesisId) ? "finding-occurrence" : "hypothesis";
            OpaqueId assignmentId = fact.SourceAssignmentId
                ?? CandidateAnalysisIdentity.StableId("taxonomy-assignment", fact.FactId.Value, subjectId.Value);
            assignmentByFact.Add(fact.FactId, assignmentId);
            byHypothesis.TryAdd(fact.HypothesisId, []);
            byHypothesis[fact.HypothesisId].Add(assignmentId);
            assignments.Add(new TaxonomyAssignmentContract(
                assignmentId, fact.TaxonomyId, fact.TaxonomyVersion, fact.Axis, fact.Facet, fact.Code,
                fact.Applicability, subjectId, subjectType, fact.Role, fact.EvidenceIds,
                fact.ApplicabilityConditionIds, fact.ConfidenceAssessmentId, fact.AnalyzerOrAdjudicatorId,
                fact.CreatedAt, fact.Reason,
                fact.SupersedesAssignmentId is null ? [] : [fact.SupersedesAssignmentId]));
        }
        List<TaxonomyProjectionContract> projections = [];
        foreach (TaxonomyClassificationFactContract fact in input.TaxonomyClassificationFacts
                     .Where(item => item.SupersedesAssignmentId is not null))
        {
            OpaqueId targetId = assignmentByFact[fact.FactId];
            OpaqueId sourceId = fact.SupersedesAssignmentId!;
            projections.Add(new TaxonomyProjectionContract(
                CandidateAnalysisIdentity.StableId("taxonomy-direct-supersession", sourceId.Value, targetId.Value),
                sourceId, targetId, fact.AnalyzerOrAdjudicatorId, fact.EvidenceIds,
                fact.Reason));
        }
        IEnumerable<IGrouping<(OpaqueId subject, string TargetTaxonomyId, ContractVersion TargetTaxonomyVersion, string TargetAxis, string TargetFacet, string? TargetCode, TaxonomyApplicability TargetApplicability), TaxonomyProjectionInputContract>> groups = input.TaxonomyProjectionInputs.GroupBy(item =>
        {
            TaxonomyClassificationFactContract source = input.TaxonomyClassificationFacts.Single(fact => fact.FactId == item.SourceClassificationFactId);
            OpaqueId subject = findingOccurrences.GetValueOrDefault(source.HypothesisId, source.HypothesisId);
            return (subject, item.TargetTaxonomyId, item.TargetTaxonomyVersion, item.TargetAxis, item.TargetFacet,
                item.TargetCode, item.TargetApplicability);
        });
        foreach (IGrouping<(OpaqueId subject, string TargetTaxonomyId, ContractVersion TargetTaxonomyVersion, string TargetAxis, string TargetFacet, string? TargetCode, TaxonomyApplicability TargetApplicability), TaxonomyProjectionInputContract> group in groups)
        {
            TaxonomyClassificationFactContract[] sources = group.Select(item => input.TaxonomyClassificationFacts.Single(
                    fact => fact.FactId == item.SourceClassificationFactId))
                .OrderBy(item => item.FactId.Value, StringComparer.Ordinal).ToArray();
            if (sources.Select(item => item.Role).Distinct().Count() != 1
                || sources.Select(item => item.ConfidenceAssessmentId).Distinct().Count() != 1
                || group.Select(item => item.TargetRole ?? sources[0].Role).Distinct().Count() != 1
                || group.Select(item => item.TargetAnalyzerOrAdjudicatorId ?? sources[0].AnalyzerOrAdjudicatorId).Distinct().Count() != 1
                || group.Select(item => item.ProjectionCreatedAt ?? sources[0].CreatedAt).Distinct().Count() != 1)
            {
                throw new InvalidOperationException(
                    "Merged taxonomy projections require one explicit role, confidence, actor, and creation-time adjudication.");
            }
            TaxonomyClassificationFactContract firstSource = sources[0];
            OpaqueId targetId = CandidateAnalysisIdentity.StableId("taxonomy-assignment-projection",
                group.Key.subject.Value, group.Key.TargetTaxonomyId, group.Key.TargetTaxonomyVersion.ToString(),
                group.Key.TargetAxis, group.Key.TargetFacet, group.Key.TargetCode ?? group.Key.TargetApplicability.ToString());
            OpaqueId[] evidence = group.SelectMany(item => item.EvidenceIds)
                .Concat(group.Select(item => input.TaxonomyClassificationFacts.Single(
                    fact => fact.FactId == item.SourceClassificationFactId)).SelectMany(item => item.EvidenceIds))
                .Distinct().OrderBy(item => item.Value, StringComparer.Ordinal).ToArray();
            OpaqueId[] conditions = sources.SelectMany(item => item.ApplicabilityConditionIds)
                .Distinct().OrderBy(item => item.Value, StringComparer.Ordinal).ToArray();
            assignments.Add(new TaxonomyAssignmentContract(
                targetId, group.Key.TargetTaxonomyId, group.Key.TargetTaxonomyVersion,
                group.Key.TargetAxis, group.Key.TargetFacet, group.Key.TargetCode, group.Key.TargetApplicability,
                group.Key.subject, findingOccurrences.Values.Contains(group.Key.subject) ? "finding-occurrence" : "hypothesis",
                group.First().TargetRole ?? firstSource.Role, evidence, conditions, firstSource.ConfidenceAssessmentId,
                group.First().TargetAnalyzerOrAdjudicatorId ?? firstSource.AnalyzerOrAdjudicatorId,
                group.First().ProjectionCreatedAt ?? firstSource.CreatedAt,
                string.Join("; ", group.Select(item => item.Reason)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
                sources.Select(item => assignmentByFact[item.FactId]).Distinct().ToArray()));
            foreach (TaxonomyProjectionInputContract item in group)
            {
                projections.Add(new TaxonomyProjectionContract(
                    CandidateAnalysisIdentity.StableId("taxonomy-projection", assignmentByFact[item.SourceClassificationFactId].Value,
                        targetId.Value, item.MappingAuthorityId.Value), assignmentByFact[item.SourceClassificationFactId], targetId,
                    item.MappingAuthorityId, item.EvidenceIds, item.Reason));
            }
        }
        return (assignments, projections,
            byHypothesis.ToDictionary(item => item.Key, item => (IReadOnlyList<OpaqueId>)item.Value), assignmentByFact);
    }

    private static (List<CoverageContract>, List<FindingCaseGapContract>) BuildCoverage(
        FindingCaseInputContract input, Dictionary<OpaqueId, OpaqueId> assignmentByFact)
    {
        List<CoverageContract> coverage = [];
        List<FindingCaseGapContract> gaps = [];
        foreach (CoveragePopulationFactContract population in input.CoveragePopulationFacts
                     .OrderBy(item => item.PopulationId, StringComparer.Ordinal))
        {
            CoverageMemberFactContract[] members = input.CoverageMemberFacts
                .Where(item => item.PopulationId == population.PopulationId).ToArray();
            foreach (IGrouping<OpaqueId, CoverageMemberFactContract> gapGroup in members
                         .Where(item => item.State != CoverageMemberState.Completed
                             && item.State != CoverageMemberState.SkippedByConfiguration)
                         .Where(item => item.GapId is not null)
                         .GroupBy(item => item.GapId!))
            {
                CoverageMemberFactContract[] gapMembers = gapGroup.ToArray();
                (FindingGapState gapState, GapReplayEffect replayEffect, GapConclusionEffect conclusionEffect) =
                    FindingCaseContractInvariants.ExpectedCoverageGapShape(
                        gapMembers.Select(item => item.State).ToArray());
                gaps.Add(new FindingCaseGapContract(
                    gapGroup.Key, population.PopulationId,
                    population.AnalyzerId.Value, gapState, replayEffect, conclusionEffect,
                    string.Join("; ", gapMembers.Select(item => item.Reason).Distinct(StringComparer.Ordinal)),
                    string.Join("; ", gapMembers.Select(item => item.MissingCapabilityOrInformation).Distinct(StringComparer.Ordinal)),
                    gapMembers.Select(item => item.MemberId).ToArray()));
            }
            long completed = members.Count(item => item.State == CoverageMemberState.Completed);
            CoverageState state = FindingCaseContractInvariants.ExpectedCoverageState(
                members.Select(item => item.State).ToArray());
            OpaqueId[] gapIds = gaps.Where(item => item.PopulationId == population.PopulationId).Select(item => item.GapId).ToArray();
            coverage.Add(new CoverageContract(
                CandidateAnalysisIdentity.StableId("finding-case-coverage", input.OriginatingRunId.Value, population.PopulationId),
                input.OriginatingRunId, population.AnalyzerId, population.PopulationId, population.DenominatorLabel,
                members.Length, completed, state, ContractConstants.TaxonomyId,
                ContractVersion.Parse(ContractConstants.TaxonomyVersion),
                members.SelectMany(item => item.TaxonomyClassificationFactIds).Where(assignmentByFact.ContainsKey)
                    .Select(id => assignmentByFact[id]).Distinct().ToArray(),
                members.Where(item => item.State is CoverageMemberState.SkippedByConfiguration or CoverageMemberState.SkippedByLimit)
                    .Select(item => new CoverageExclusionContract(item.MemberId, item.Reason, item.State)).ToArray(),
                gapIds, members.Where(item => item.FailureId is not null).Select(item => item.FailureId!).ToArray())
            {
                MemberResults = members.OrderBy(item => item.MemberId.Value, StringComparer.Ordinal)
                    .Select(item => new CoverageMemberResultContract(
                        item.MemberId, item.State, item.Reason, item.MissingCapabilityOrInformation,
                        item.GapId, item.FailureId,
                        item.TaxonomyClassificationFactIds.Where(assignmentByFact.ContainsKey)
                            .Select(id => assignmentByFact[id]).Distinct().ToArray())).ToArray(),
            });
        }
        return (coverage, gaps);
    }

    private static IdentityEnvelopeContract ToIdentity(SharedCauseProofContract proof)
    {
        IdentityEnvelopeContract value = new(
            proof.AnalyzerFamily, proof.SemanticContractVersion, proof.IdentityContractVersion,
            proof.ParticipantsAndRoles, proof.CausalCondition, proof.AffectedLocus,
            proof.ApplicabilityPredicates, proof.DependencyClosureId, new Sha256Fingerprint(new string('0', 64)))
        {
            AnalyzerVersion = proof.AnalyzerVersion,
        };
        return value with { CanonicalSignature = FindingCaseIdentity.ComputeIdentitySignature(value) };
    }

    private static string ParticipantDescriptor(IdentityEnvelopeContract value) => string.Join("\n",
        value.ParticipantsAndRoles.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}:{item.Value}"));
    private static ReconciliationGateState Equal(string prior, string current) => prior == current
        ? ReconciliationGateState.ProvenEquivalent : ReconciliationGateState.ProvenDifferent;
    private static bool AllEquivalent(ReconciliationGatesContract value) => value.Causal == ReconciliationGateState.ProvenEquivalent
        && value.Applicability == ReconciliationGateState.ProvenEquivalent
        && value.Dependency == ReconciliationGateState.ProvenEquivalent
        && value.Producer == ReconciliationGateState.ProvenEquivalent;
    private static bool HasUnknown(ReconciliationGatesContract value) => value.Causal == ReconciliationGateState.Unknown
        || value.Applicability == ReconciliationGateState.Unknown || value.Dependency == ReconciliationGateState.Unknown
        || value.Producer == ReconciliationGateState.Unknown;
    private static bool NoUnknown(ReconciliationGatesContract value) => !HasUnknown(value);
    private static ReconciliationGatesContract EmptyGates() => new(
        ReconciliationGateState.NotEvaluated, ReconciliationGateState.NotEvaluated,
        ReconciliationGateState.NotEvaluated, ReconciliationGateState.NotEvaluated);
    private static ReconciliationGatesContract UnknownGates() => new(
        ReconciliationGateState.Unknown, ReconciliationGateState.Unknown,
        ReconciliationGateState.Unknown, ReconciliationGateState.Unknown);
    private static string[] PromotionReasons(params bool[] predicates)
    {
        string[] names = ["state is not present", "confidence is below plausible", "supporting evidence is absent",
            "a defeating contradiction remains", "required information is missing", "severity is not grounded",
            "typed finding/shared-cause identity is not closed"];
        return predicates.Select((value, index) => (value, index)).Where(item => !item.value)
            .Select(item => names[item.index]).ToArray();
    }

    private sealed record FindingDraft(CandidateHypothesisContract Hypothesis,
        FindingConclusionAssessmentContract Conclusion, SharedCauseProofContract CauseProof, OpaqueId OccurrenceId);
    private sealed record LeadDraft(CandidateHypothesisContract Hypothesis, FindingConclusionAssessmentContract Conclusion);
    private sealed record CurrentFinding(FindingDraft Draft, string Conclusion,
        IReadOnlyList<OpaqueId> TaxonomyIds, Sha256Fingerprint Semantic);
    private sealed record CaseDraft(OpaqueId OccurrenceId, CaseOccurrenceKind Kind,
        IdentityEnvelopeContract IdentityEnvelope, IReadOnlyList<OpaqueId> FindingOccurrenceIds,
        IReadOnlyList<OpaqueId> CandidateIds, IReadOnlyList<OpaqueId> HypothesisIds,
        IReadOnlyList<OpaqueId> CauseProofEvidenceIds);
    private sealed record GateEvaluation(
        ReconciliationGatesContract Gates,
        IReadOnlyList<OpaqueId> ProducerEvidenceIds);
}
