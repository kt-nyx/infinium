using static Infinium.Domain.Contracts.AnalysisContractInvariantHelpers;

namespace Infinium.Domain.Contracts;

public static partial class FindingCaseContractInvariants
{
    public static void Validate(FindingCaseContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.FindingCaseSchemaId);
        if (!StringComparer.Ordinal.Equals(value.PublicationClaimBoundary, "no-safety-claim")
            || value.PromotionPolicyVersion != new ContractVersion(1, 0, 0)
            || value.ReconciliationPolicyVersion.Major < 1)
        {
            throw new InvalidOperationException("Finding and case publication is informational and must carry the exact no-safety-claim boundary.");
        }
        RequireUnique(value.PromotionAssessments.Select(item => item.AssessmentId), "promotion assessments");
        RequireUnique(value.PromotionAssessments.Select(item => item.HypothesisId), "promoted hypotheses");
        RequireUnique(value.Abstentions.Select(item => item.AbstentionId), "finding/case abstentions");
        RequireUnique(value.Findings.Select(item => item.FindingOccurrenceId), "finding occurrences");
        RequireUnique(value.Findings.Select(item => item.LogicalFindingId), "current logical findings");
        RequireUnique(value.Recommendations.Select(item => item.RecommendationId), "recommendations");
        RequireUnique(value.Cases.Select(item => item.CaseOccurrenceId), "case occurrences");
        RequireUnique(value.Cases.Select(item => item.LogicalCaseId), "current logical cases");
        RequireUnique(value.ReconciliationAssessments.Select(item => item.AssessmentId), "reconciliation assessments");
        RequireUnique(value.LineageEvents.Select(item => item.EventId), "lineage events");
        HashSet<OpaqueId> findings = value.Findings.Select(item => item.FindingOccurrenceId).ToHashSet();
        HashSet<OpaqueId> promotionHypotheses = value.PromotionAssessments.Select(item => item.HypothesisId).ToHashSet();
        HashSet<OpaqueId> taxonomyAssignments = value.TaxonomyAssignments.Select(item => item.AssignmentId).ToHashSet();
        foreach (FindingContract finding in value.Findings)
        {
            if (finding.Confidence is AnalysisConfidence.Unspecified or AnalysisConfidence.SpeculativeLead
                || finding.Severity == FindingSeverity.Unspecified
                || finding.OriginatingRunId != value.OriginatingRunId
                || !promotionHypotheses.Contains(finding.HypothesisId)
                || value.PromotionAssessments.Single(item => item.HypothesisId == finding.HypothesisId).Outcome
                    != FindingPromotionOutcome.SupportedFinding
                || finding.EvidenceIds.Count == 0
                || finding.IdentityEnvelopeId != FindingCaseIdentity.EnvelopeId(finding.IdentityEnvelope)
                || finding.CaseIdentityEnvelopeId != FindingCaseIdentity.EnvelopeId(finding.CaseIdentityEnvelope)
                || finding.SemanticFingerprint != FindingCaseIdentity.FindingSemanticFingerprint(
                    finding.Conclusion, finding.Severity, finding.Confidence,
                    finding.IdentityEnvelope, finding.TaxonomyAssignmentIds.Select(id =>
                        FindingCaseIdentity.TaxonomySemanticDescriptor(value.TaxonomyAssignments.Single(item =>
                            item.AssignmentId == id))))
                || finding.TaxonomyAssignmentIds.Any(id => !taxonomyAssignments.Contains(id)
                    || value.TaxonomyAssignments.Single(item => item.AssignmentId == id).SubjectType != "finding-occurrence"
                    || value.TaxonomyAssignments.Single(item => item.AssignmentId == id).SubjectId != finding.FindingOccurrenceId))
            {
                throw new InvalidOperationException("A finding requires plausible-or-better support, closed severity/identity, evidence, and exact semantic identity.");
            }
            FindingCaseContractInvariants.ValidateIdentity(finding.IdentityEnvelope, allowOpen: false);
            FindingCaseContractInvariants.ValidateIdentity(finding.CaseIdentityEnvelope, allowOpen: false);
        }
        foreach (FindingPromotionAssessmentContract assessment in value.PromotionAssessments)
        {
            if (assessment.Outcome == FindingPromotionOutcome.Unspecified
                || assessment.Outcome != FindingCaseContractInvariants.ExpectedPromotionOutcome(
                    assessment.StatePresent, assessment.ConfidenceAtLeastPlausible,
                    assessment.HasSupportingEvidence, assessment.HasNoDefeatingContradictions,
                    assessment.HasNoMissingInformation, assessment.SeverityClosed, assessment.IdentityClosed,
                    assessment.ConclusionAvailable, assessment.LeadEligibleState)
                || (assessment.Outcome != FindingPromotionOutcome.SupportedFinding && assessment.Reasons.Count == 0))
            {
                throw new InvalidOperationException("Promotion outcomes must be the exact result of every closed threshold predicate.");
            }
        }
        HashSet<OpaqueId> abstentionIds = value.Abstentions.Select(item => item.AbstentionId).ToHashSet();
        if (value.Abstentions.Select(item => item.HypothesisId).Distinct().Count() != value.Abstentions.Count
            || value.Abstentions.Any(item => item.EvidenceIds.Count == 0 || string.IsNullOrWhiteSpace(item.Reason)))
        {
            throw new InvalidOperationException("Each below-threshold hypothesis requires one evidence-bound abstention record.");
        }
        HashSet<OpaqueId> leadHypotheses = value.PromotionAssessments
            .Where(item => item.Outcome == FindingPromotionOutcome.LeadOnly)
            .Select(item => item.HypothesisId).ToHashSet();
        HashSet<OpaqueId> nonSupportedHypotheses = value.PromotionAssessments
            .Where(item => item.Outcome != FindingPromotionOutcome.SupportedFinding)
            .Select(item => item.HypothesisId).ToHashSet();
        if (!nonSupportedHypotheses.SetEquals(value.Abstentions.Select(item => item.HypothesisId))
            || value.PromotionAssessments.Where(item => item.Outcome == FindingPromotionOutcome.SupportedFinding)
                .Any(item => value.Findings.Count(finding => finding.HypothesisId == item.HypothesisId) != 1))
        {
            throw new InvalidOperationException("Promotion, finding, and abstention ledgers must be one-to-one and outcome-consistent.");
        }
        foreach (FindingRecommendationContract recommendation in value.Recommendations)
        {
            bool findingReference = recommendation.FindingOccurrenceId is not null;
            bool abstentionReference = recommendation.AbstentionId is not null;
            bool leadReference = recommendation.LeadHypothesisId is not null;
            if (recommendation.Kind == RecommendationKind.Unspecified
                || new[] { findingReference, abstentionReference, leadReference }.Count(item => item) != 1
                || (findingReference && !findings.Contains(recommendation.FindingOccurrenceId!))
                || (findingReference && recommendation.Kind is RecommendationKind.Abstention
                    or RecommendationKind.FurtherInvestigation)
                || (abstentionReference && (!abstentionIds.Contains(recommendation.AbstentionId!)
                    || recommendation.Kind is not (RecommendationKind.Abstention or RecommendationKind.FurtherInvestigation)))
                || (leadReference && (!leadHypotheses.Contains(recommendation.LeadHypothesisId!)
                    || recommendation.Kind != RecommendationKind.FurtherInvestigation))
                || string.IsNullOrWhiteSpace(recommendation.Action)
                || string.IsNullOrWhiteSpace(recommendation.Uncertainty)
                || string.IsNullOrWhiteSpace(recommendation.Reversibility)
                || string.IsNullOrWhiteSpace(recommendation.Verification)
                || recommendation.Risks.Count == 0 || recommendation.Risks.Any(string.IsNullOrWhiteSpace)
                || recommendation.EvidenceIds.Count == 0)
            {
                throw new InvalidOperationException("Recommendations require exactly one kind-consistent finding, lead, or abstention reference.");
            }
        }
        if (value.Findings.Any(item => value.Recommendations.Count(recommendation =>
                recommendation.FindingOccurrenceId == item.FindingOccurrenceId) != 1)
            || value.Abstentions.Any(item =>
                value.Recommendations.Count(recommendation => recommendation.AbstentionId == item.AbstentionId) != 1)
            || leadHypotheses.Any(id => value.Recommendations.Count(recommendation =>
                recommendation.Kind == RecommendationKind.FurtherInvestigation
                && recommendation.AbstentionId == value.Abstentions.Single(item => item.HypothesisId == id).AbstentionId) != 1))
        {
            throw new InvalidOperationException("Every supported finding, terminal abstention, and retained lead requires one recommendation.");
        }
        Dictionary<OpaqueId, FindingContract> findingById = value.Findings.ToDictionary(item => item.FindingOccurrenceId);
        foreach (AnalysisCaseContract @case in value.Cases)
        {
            bool supported = @case.Kind == CaseOccurrenceKind.Supported;
            if (@case.Kind == CaseOccurrenceKind.Unspecified
                || @case.OriginatingRunId != value.OriginatingRunId
                || @case.CauseProofEvidenceIds.Count == 0
                || (supported && (@case.FindingOccurrenceIds.Count == 0 || !@case.FindingOccurrenceIds.All(findings.Contains)))
                || (!supported && @case.FindingOccurrenceIds.Count != 0)
                || @case.HypothesisIds.Count == 0
                || (supported && !@case.HypothesisIds.ToHashSet().SetEquals(
                    @case.FindingOccurrenceIds.Select(id => findingById[id].HypothesisId)))
                || (supported && !@case.CandidateIds.ToHashSet().SetEquals(
                    @case.FindingOccurrenceIds.Select(id => findingById[id].CandidateId)))
                || (!supported && (@case.CandidateIds.Count == 0
                    || @case.CandidateIds.Distinct().Count() != @case.CandidateIds.Count
                    || @case.CandidateIds.Count != @case.HypothesisIds.Count))
                || (!supported && @case.HypothesisIds.Any(id => !leadHypotheses.Contains(id)))
                || !StringComparer.Ordinal.Equals(@case.SharedCause, @case.IdentityEnvelope.CausalCondition)
                || @case.IdentityEnvelopeId != FindingCaseIdentity.EnvelopeId(@case.IdentityEnvelope)
                || @case.SemanticFingerprint != FindingCaseIdentity.CaseSemanticFingerprint(
                    @case.Kind, @case.IdentityEnvelope,
                    @case.FindingOccurrenceIds.Select(id => findingById[id].LogicalFindingId))
                || @case.AffectsReadiness)
            {
                throw new InvalidOperationException("Supported and lead-only cases require separate, causally proven memberships and readiness effects.");
            }
            FindingCaseContractInvariants.ValidateIdentity(@case.IdentityEnvelope, allowOpen: false);
            if (supported && @case.FindingOccurrenceIds.Any(id =>
                    findingById[id].CaseIdentityEnvelope.CanonicalSignature != @case.IdentityEnvelope.CanonicalSignature))
            {
                throw new InvalidOperationException("Supported cases may group only exact typed cause, locus, applicability, dependency, and producer identity.");
            }
        }
        if (value.Findings.Any(finding => value.Cases.Count(@case =>
                @case.Kind == CaseOccurrenceKind.Supported
                && @case.FindingOccurrenceIds.Contains(finding.FindingOccurrenceId)) != 1))
        {
            throw new InvalidOperationException("Every supported finding must belong to exactly one supported causal case.");
        }
        if (leadHypotheses.Any(hypothesisId => value.Cases.Count(@case =>
                @case.Kind == CaseOccurrenceKind.LeadOnly
                && @case.HypothesisIds.Contains(hypothesisId)) != 1))
        {
            throw new InvalidOperationException("Every retained lead must belong to exactly one lead-only causal case.");
        }
        foreach (OccurrenceReconciliationContract reconciliation in value.ReconciliationAssessments)
        {
            bool allEquivalent = reconciliation.Gates.Causal == ReconciliationGateState.ProvenEquivalent
                && reconciliation.Gates.Applicability == ReconciliationGateState.ProvenEquivalent
                && reconciliation.Gates.Dependency == ReconciliationGateState.ProvenEquivalent
                && reconciliation.Gates.Producer == ReconciliationGateState.ProvenEquivalent;
            bool noCurrent = reconciliation.CurrentOccurrenceId is null;
            bool noPrior = reconciliation.PriorOccurrenceId is null;
            bool currentResolves = noCurrent || reconciliation.SubjectKind switch
            {
                "finding" => findings.Contains(reconciliation.CurrentOccurrenceId!),
                "case" => value.Cases.Any(item => item.CaseOccurrenceId == reconciliation.CurrentOccurrenceId),
                _ => false,
            };
            if (reconciliation.Outcome is ReconciliationOutcome.Unspecified
                || reconciliation.SubjectKind is not ("finding" or "case")
                || reconciliation.ConsideredOccurrenceIds.Count == 0
                || reconciliation.ConsideredOccurrenceIds.Distinct().Count() != reconciliation.ConsideredOccurrenceIds.Count
                || !currentResolves
                || (!noCurrent && !reconciliation.ConsideredOccurrenceIds.Contains(reconciliation.CurrentOccurrenceId!))
                || (!noPrior && !reconciliation.ConsideredOccurrenceIds.Contains(reconciliation.PriorOccurrenceId!))
                || reconciliation.PolicyVersion.Major < 1
                || !StringComparer.Ordinal.Equals(reconciliation.Mechanism, "automatic")
                || (reconciliation.ProofEvidenceIds.Count == 0
                    && reconciliation.Outcome != ReconciliationOutcome.NotEvaluated)
                || !reconciliation.VisibleByDefault
                || reconciliation.PolicyVersion != value.ReconciliationPolicyVersion
                || (reconciliation.Outcome == ReconciliationOutcome.NewDistinct) != noPrior
                || (reconciliation.Outcome is ReconciliationOutcome.Ambiguous or ReconciliationOutcome.Unknown
                    && noPrior)
                || (reconciliation.Outcome is ReconciliationOutcome.NotObserved or ReconciliationOutcome.NotEvaluated
                    && noPrior)
                || (reconciliation.Outcome is ReconciliationOutcome.ExactContinuation or ReconciliationOutcome.AnalyticalRevision
                    && (!allEquivalent || noCurrent || noPrior))
                || (reconciliation.Outcome == ReconciliationOutcome.RelatedFollowUp
                    && (allEquivalent || noCurrent || noPrior
                        || FindingCaseContractInvariants.HasUnknownGate(reconciliation.Gates)))
                || (reconciliation.Outcome == ReconciliationOutcome.NewDistinct
                    && (noCurrent || (!FindingCaseContractInvariants.HasDifferentGate(reconciliation.Gates)
                        && !(reconciliation.Gates.Causal == ReconciliationGateState.NotEvaluated
                            && reconciliation.Gates.Applicability == ReconciliationGateState.NotEvaluated
                            && reconciliation.Gates.Dependency == ReconciliationGateState.NotEvaluated
                            && reconciliation.Gates.Producer == ReconciliationGateState.NotEvaluated)
                        && !(noPrior
                            && reconciliation.Gates.Causal == ReconciliationGateState.NotEvaluated
                            && reconciliation.Gates.Applicability == ReconciliationGateState.ProvenEquivalent
                            && reconciliation.Gates.Dependency == ReconciliationGateState.ProvenEquivalent
                            && reconciliation.Gates.Producer == ReconciliationGateState.ProvenEquivalent))))
                || (reconciliation.Outcome == ReconciliationOutcome.Ambiguous
                    && (reconciliation.ConsideredOccurrenceIds.Count < 2 || noCurrent))
                || (reconciliation.Outcome == ReconciliationOutcome.Unknown
                    && (!FindingCaseContractInvariants.HasUnknownGate(reconciliation.Gates) || noCurrent))
                || (reconciliation.Outcome is ReconciliationOutcome.NotObserved or ReconciliationOutcome.NotEvaluated) != noCurrent
                || (reconciliation.Outcome is ReconciliationOutcome.Ambiguous or ReconciliationOutcome.Unknown
                        or ReconciliationOutcome.NotObserved or ReconciliationOutcome.NotEvaluated
                    && reconciliation.Gaps.Count == 0))
            {
                throw new InvalidOperationException("Continuity requires all four independently proven reconciliation gates.");
            }
        }
        ReconciliationOutcome[] reuseOutcomes =
        [
            ReconciliationOutcome.ExactContinuation,
            ReconciliationOutcome.AnalyticalRevision,
            ReconciliationOutcome.RelatedFollowUp,
        ];
        if (value.ReconciliationAssessments.Where(item => reuseOutcomes.Contains(item.Outcome))
                .GroupBy(item => (item.SubjectKind, item.PriorOccurrenceId)).Any(group => group.Count() != 1)
            || value.ReconciliationAssessments.Where(item => reuseOutcomes.Contains(item.Outcome))
                .GroupBy(item => (item.SubjectKind, item.CurrentOccurrenceId)).Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException("Automatic reconciliation reuse must remain globally unique one-to-one per subject kind.");
        }
        HashSet<OpaqueId> reconciliationIds = value.ReconciliationAssessments.Select(item => item.AssessmentId).ToHashSet();
        foreach (OccurrenceLineageContract lineage in value.LineageEvents)
        {
            OccurrenceReconciliationContract? assessment = lineage.ReconciliationAssessmentId is null ? null
                : value.ReconciliationAssessments.Single(item => item.AssessmentId == lineage.ReconciliationAssessmentId);
            ReconciliationOutcome? requiredOutcome = lineage.Kind switch
            {
                LineageKind.AnalyticalRevision => ReconciliationOutcome.AnalyticalRevision,
                LineageKind.RelatedFollowUp => ReconciliationOutcome.RelatedFollowUp,
                _ => null,
            };
            bool successorsResolve = lineage.SuccessorIds.All(id => findings.Contains(id)
                || value.Cases.Any(item => item.CaseOccurrenceId == id));
            bool validLeadPromotion = lineage.Kind != LineageKind.PromotesLead
                || (lineage.ReconciliationAssessmentId is null
                    && lineage.PredecessorIds.Count == 1
                    && lineage.SuccessorIds.Count == 1
                    && value.Cases.Any(item => item.CaseOccurrenceId == lineage.SuccessorIds[0]
                        && item.Kind == CaseOccurrenceKind.Supported
                        && item.SupersedesOccurrenceId == lineage.PredecessorIds[0]));
            if (lineage.Kind == LineageKind.Unspecified
                || lineage.PredecessorIds.Count == 0
                || lineage.SuccessorIds.Count == 0
                || (lineage.ReconciliationAssessmentId is not null
                    && !reconciliationIds.Contains(lineage.ReconciliationAssessmentId))
                || !successorsResolve
                || (requiredOutcome is not null && (assessment?.Outcome != requiredOutcome
                    || !lineage.PredecessorIds.Contains(assessment.PriorOccurrenceId!)
                    || !lineage.SuccessorIds.Contains(assessment.CurrentOccurrenceId!)))
                || !validLeadPromotion)
            {
                throw new InvalidOperationException("Append-only lineage events require closed predecessor, successor, and reconciliation identity.");
            }
        }
        FindingCaseContractInvariants.ValidateTaxonomy(value.TaxonomyAssignments);
        RequireUnique(value.TaxonomyProjections.Select(item => item.ProjectionId), "taxonomy projections");
        Dictionary<OpaqueId, TaxonomyAssignmentContract> assignmentById = value.TaxonomyAssignments
            .ToDictionary(item => item.AssignmentId);
        foreach (TaxonomyProjectionContract projection in value.TaxonomyProjections)
        {
            if (!taxonomyAssignments.Contains(projection.SourceAssignmentId)
                || !taxonomyAssignments.Contains(projection.ProjectedAssignmentId)
                || projection.SourceAssignmentId == projection.ProjectedAssignmentId
                || assignmentById[projection.SourceAssignmentId].SubjectId != assignmentById[projection.ProjectedAssignmentId].SubjectId
                || !StringComparer.Ordinal.Equals(assignmentById[projection.SourceAssignmentId].SubjectType,
                    assignmentById[projection.ProjectedAssignmentId].SubjectType)
                || projection.EvidenceIds.Count == 0
                || StringComparer.Ordinal.Equals(projection.MappingAuthorityId.Value, "unspecified")
                || string.IsNullOrWhiteSpace(projection.Reason))
            {
                throw new InvalidOperationException("Taxonomy history projections require distinct source/target assignments and explicit mapping provenance.");
            }
        }
        foreach (TaxonomyAssignmentContract assignment in value.TaxonomyAssignments)
        {
            OpaqueId[] incoming = value.TaxonomyProjections
                .Where(item => item.ProjectedAssignmentId == assignment.AssignmentId)
                .Select(item => item.SourceAssignmentId).OrderBy(item => item.Value, StringComparer.Ordinal).ToArray();
            OpaqueId[] declared = assignment.SupersedesAssignmentIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray();
            bool subjectResolves = assignment.SubjectType switch
            {
                "finding-occurrence" => findings.Contains(assignment.SubjectId),
                "hypothesis" => promotionHypotheses.Contains(assignment.SubjectId),
                _ => false,
            };
            if (!subjectResolves || !incoming.SequenceEqual(declared)
                || declared.Any(id => id == assignment.AssignmentId || !assignmentById.ContainsKey(id)
                    || assignmentById[id].SubjectId != assignment.SubjectId
                    || !StringComparer.Ordinal.Equals(assignmentById[id].SubjectType, assignment.SubjectType)))
            {
                throw new InvalidOperationException("Taxonomy assignments require a resolved subject and an exact closed projection predecessor set.");
            }
        }
        FindingCaseContractInvariants.ValidateCoverage(value.Coverage, value.Gaps, value.TaxonomyAssignments);
        HashSet<OpaqueId> coverageFailures = value.CoverageFailures.Select(item => item.FailureId).ToHashSet();
        RequireUnique(value.CoverageFailures.Select(item => item.FailureId), "coverage failures");
        HashSet<OpaqueId> referencedGaps = value.Coverage.SelectMany(item => item.GapIds).ToHashSet();
        HashSet<OpaqueId> referencedFailures = value.Coverage.SelectMany(item => item.FailureIds).ToHashSet();
        if (value.Coverage.Any(item => item.OriginatingRunId != value.OriginatingRunId
                || item.FailureIds.Any(id => !coverageFailures.Contains(id))
                || item.GapIds.Any(id => value.Gaps.Single(gap => gap.GapId == id).PopulationId != item.PopulationId)
                || item.FailureIds.Any(id => value.CoverageFailures.Single(failure => failure.FailureId == id).AnalyzerId != item.AnalyzerId)
                || value.Gaps.Where(gap => item.GapIds.Contains(gap.GapId)).Any(gap =>
                    gap.EvidenceIds.Any(memberId => !item.MemberResults.Any(member => member.MemberId == memberId))))
            || !referencedGaps.SetEquals(value.Gaps.Select(item => item.GapId))
            || !referencedFailures.SetEquals(coverageFailures))
        {
            throw new InvalidOperationException("Coverage gaps and failures must be exactly referenced by their run, population, analyzer, and member ledger.");
        }
        ExecutionBoundaryContractInvariants.ValidateProductCapabilities(value.Boundaries, requireNotUsed: true);
        if (value.PayloadId != FindingCaseIdentity.ComputePayloadId(value))
        {
            throw new InvalidOperationException("Finding/case payload identity must cover the exact aggregate semantics.");
        }
    }

}
