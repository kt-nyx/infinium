using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.Documentation;
using Infinium.Application.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;


public static partial class AnalysisPublicationBuilder
{
    private static byte[] SemanticProjection(
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases,
        CancellationToken cancellationToken)
    {
        IOrderedEnumerable<T> Sorted<T>(IEnumerable<T> values) =>
            values.Select(item =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return item;
                })
                .OrderBy(item => JsonSerializer.Serialize(item, SchemaValidatedJsonCodec.JsonOptions), StringComparer.Ordinal);

        string Anchor(object value) => Hash(JsonSerializer.SerializeToUtf8Bytes(
            value, SchemaValidatedJsonCodec.JsonOptions));
        object SemanticEnvelope(IdentityEnvelopeContract value) => new
        {
            value.AnalyzerFamily,
            value.AnalyzerVersion,
            value.SemanticContractVersion,
            value.IdentityContractVersion,
            participants = value.ParticipantsAndRoles.OrderBy(item => item.Key, StringComparer.Ordinal),
            value.CausalCondition,
            value.AffectedLocus,
            applicability = value.ApplicabilityPredicates.OrderBy(item => item, StringComparer.Ordinal),
        };
        string SemanticDependency(OpaqueId id) => id == candidates.DeliveredInputId
                && !StringComparer.Ordinal.Equals(
                    candidates.DeliveredInputId.Value, "candidate-delivered-input-unspecified")
            ? "candidate-delivered-input"
            : id.Value;
        Dictionary<OpaqueId, string> revisionAnchors = documentation.Revisions.ToDictionary(
            item => item.RevisionId,
            item => Anchor(new
            {
                item.SourceId,
                item.SourceKind,
                item.SourceRevision,
                item.ByteFingerprint,
                item.ByteLength,
                item.SupplyingSnapshotId,
                item.RetentionState,
                item.ReplayState,
            }));
        Dictionary<OpaqueId, string> importAnchors = documentation.Imports.ToDictionary(
            item => item.ImportId,
            item => Anchor(new
            {
                revision = revisionAnchors[item.RevisionId],
                item.Mode,
                item.ExtractorId,
                item.LlmInvolvement,
                item.LlmOperation,
                boundaries = item.Boundaries.OrderBy(boundary => boundary.BoundaryId, StringComparer.Ordinal),
            }));
        Dictionary<OpaqueId, string> passageAnchors = documentation.Passages.ToDictionary(
            item => item.PassageId,
            item => Anchor(new
            {
                revision = revisionAnchors[item.RevisionId],
                item.Utf8StartOffset,
                item.Utf8EndOffset,
                item.PassageFingerprint,
                item.State,
            }));
        Dictionary<OpaqueId, string> claimAnchors = documentation.Claims.ToDictionary(
            item => item.ClaimId,
            item => Anchor(new
            {
                producingImport = importAnchors[item.ProducingImportId],
                passage = passageAnchors[item.PassageId],
                item.Kind,
                item.ExactText,
                item.Conditions,
                item.Authority,
                item.Applicability,
                item.ClassificationRole,
                contradictions = item.ContradictingEvidenceIds.OrderBy(id => id.Value),
            }));
        Dictionary<OpaqueId, string> applicationAnchors = documentation.Applications.ToDictionary(
            item => item.ApplicationId,
            item => Anchor(new
            {
                claim = claimAnchors[item.ClaimId],
                item.SubjectId,
                item.SubjectType,
                item.Applicability,
                evidence = item.EvidenceIds.Select(id => claimAnchors.GetValueOrDefault(id, "external:" + id.Value))
                    .OrderBy(value => value, StringComparer.Ordinal),
            }));
        Dictionary<OpaqueId, string> decisionAnchors = candidates.Decisions.ToDictionary(
            item => item.DecisionId,
            item => Anchor(new
            {
                item.PopulationMemberId,
                item.SourceFactId,
                item.Lane,
                item.Disposition,
                participants = item.Participants.OrderBy(value => value.ParticipantId.Value),
                item.JoinKind,
                path = item.Path,
                item.Rationale,
                evidence = item.EvidenceIds.OrderBy(id => id.Value),
                item.AdmissionIndependentOfScore,
                item.OptionalRank,
                item.AnalyzerId,
                item.PolicyId,
                item.ThresholdId,
                item.LimitId,
                dependencies = item.DependencyIds.Select(SemanticDependency)
                    .OrderBy(id => id, StringComparer.Ordinal),
            }));
        Dictionary<OpaqueId, string> candidateAnchors = candidates.Candidates.ToDictionary(
            item => item.CandidateId,
            item => Anchor(new
            {
                decision = decisionAnchors[item.DecisionId],
                item.State,
                item.CausalExplanation,
                supporting = item.SupportingEvidenceIds.OrderBy(id => id.Value),
                contradicting = item.ContradictingEvidenceIds.OrderBy(id => id.Value),
                item.MissingInformation,
                item.Confidence,
                item.ThresholdId,
            }));
        Dictionary<OpaqueId, string> hypothesisAnchors = candidates.Hypotheses.ToDictionary(
            item => item.HypothesisId,
            item => Anchor(new
            {
                candidate = candidateAnchors[item.CandidateId],
                item.State,
                item.ProposedExplanation,
                item.PredictedImpact,
                supporting = item.SupportingEvidenceIds.OrderBy(id => id.Value),
                contradicting = item.ContradictingEvidenceIds.OrderBy(id => id.Value),
                item.MissingInformation,
                item.Confidence,
                item.ThresholdId,
            }));
        Dictionary<OpaqueId, string> occurrenceAnchors = findingCases.Findings
            .ToDictionary(item => item.FindingOccurrenceId, item => "finding:" + Anchor(new
            {
                candidate = candidateAnchors[item.CandidateId],
                hypothesis = hypothesisAnchors[item.HypothesisId],
                item.Conclusion,
                item.Severity,
                item.Confidence,
            }));
        foreach (AnalysisCaseContract item in findingCases.Cases)
        {
            occurrenceAnchors.Add(item.CaseOccurrenceId, "case:" + Anchor(new
            {
                item.Kind,
                candidates = item.CandidateIds.Select(id => candidateAnchors[id]).OrderBy(value => value, StringComparer.Ordinal),
                hypotheses = item.HypothesisIds.Select(id => hypothesisAnchors[id]).OrderBy(value => value, StringComparer.Ordinal),
                item.SharedCause,
                item.AffectsReadiness,
            }));
        }
        string Occurrence(OpaqueId? id) => id is null ? "none"
            : occurrenceAnchors.GetValueOrDefault(id, "external:" + id.Value);
        string Subject(OpaqueId id) => occurrenceAnchors.GetValueOrDefault(id,
            candidateAnchors.GetValueOrDefault(id,
                hypothesisAnchors.GetValueOrDefault(id, "external:" + id.Value)));
        string Evidence(OpaqueId id) => claimAnchors.GetValueOrDefault(id, Subject(id));
        Dictionary<OpaqueId, string> abstentionAnchors = candidates.Abstentions.ToDictionary(
            item => item.AbstentionId,
            item => Anchor(new
            {
                decision = decisionAnchors[item.DecisionId],
                candidate = item.CandidateId is null ? "none" : candidateAnchors[item.CandidateId],
                item.AnalyzerId,
                item.Reason,
                item.RequiredInformation,
            }));
        Dictionary<OpaqueId, string> candidateGapAnchors = candidates.Gaps.ToDictionary(
            item => item.GapId,
            item => Anchor(new
            {
                decision = decisionAnchors[item.DecisionId],
                item.PopulationId,
                item.State,
                item.Reason,
                item.MissingCapabilityOrInformation,
            }));
        Dictionary<OpaqueId, string> taxonomyAnchors = findingCases.TaxonomyAssignments.ToDictionary(
            item => item.AssignmentId,
            item => Anchor(new
            {
                item.TaxonomyId,
                item.TaxonomyVersion,
                item.Axis,
                item.Facet,
                item.Code,
                item.Applicability,
                subject = Subject(item.SubjectId),
                item.SubjectType,
                item.Role,
                evidence = item.EvidenceIds.Select(Evidence).OrderBy(value => value, StringComparer.Ordinal),
                applicabilityConditions = item.ApplicabilityConditionIds.Select(Evidence).OrderBy(value => value, StringComparer.Ordinal),
                item.ConfidenceAssessmentId,
                item.AnalyzerOrAdjudicatorId,
                item.Reason,
            }));
        Dictionary<OpaqueId, string> findingAbstentionAnchors = findingCases.Abstentions.ToDictionary(
            item => item.AbstentionId,
            item => Anchor(new
            {
                hypothesis = hypothesisAnchors[item.HypothesisId],
                item.Reason,
                item.RequiredInformation,
                evidence = item.EvidenceIds.Select(Evidence).OrderBy(value => value, StringComparer.Ordinal),
            }));
        Dictionary<OpaqueId, string> findingGapAnchors = findingCases.Gaps.ToDictionary(
            item => item.GapId,
            item => Anchor(new
            {
                item.PopulationId,
                item.StageId,
                item.State,
                item.ReplayEffect,
                item.ConclusionEffect,
                item.Reason,
                item.MissingCapabilityOrInformation,
                evidence = item.EvidenceIds.Select(Evidence).OrderBy(value => value, StringComparer.Ordinal),
            }));
        Dictionary<OpaqueId, string> coverageFailureAnchors = findingCases.CoverageFailures.ToDictionary(
            item => item.FailureId,
            item => Anchor(new { item.AnalyzerId, item.FailureCode, item.Message, item.Retryable }));
        Dictionary<OpaqueId, OpaqueId> exactContinuationAliases = findingCases.ReconciliationAssessments
            .Where(item => item.PriorOccurrenceId is not null && item.CurrentOccurrenceId is not null
                && item.Outcome == ReconciliationOutcome.ExactContinuation
                && item.Gates.Causal == ReconciliationGateState.ProvenEquivalent
                && item.Gates.Applicability == ReconciliationGateState.ProvenEquivalent
                && item.Gates.Dependency == ReconciliationGateState.ProvenEquivalent
                && item.Gates.Producer == ReconciliationGateState.ProvenEquivalent)
            .ToDictionary(item => item.PriorOccurrenceId!, item => item.CurrentOccurrenceId!);
        string SemanticOccurrence(OpaqueId? id)
        {
            if (id is not null && exactContinuationAliases.TryGetValue(id, out OpaqueId? current))
            {
                id = current;
            }
            return Occurrence(id);
        }
        string CandidateGraphNode(string kind, OpaqueId id) => kind switch
        {
            "candidate" => candidateAnchors[id],
            "candidate-decision" => decisionAnchors[id],
            "hypothesis" => hypothesisAnchors[id],
            "abstention" => abstentionAnchors[id],
            "gap" => candidateGapAnchors[id],
            "candidate-analysis-root" or "execution-input-binding" or "dependency-closure" => kind,
            "analyzer-declaration-binding" or "policy-binding" or "threshold-binding" or "limit-binding" => kind,
            "dependency" => SemanticDependency(id),
            _ => Evidence(id),
        };

        var projection = new
        {
            documentation = new
            {
                revisions = Sorted(documentation.Revisions.Select(item => new
                {
                    anchor = revisionAnchors[item.RevisionId],
                    item.SourceId,
                    item.SourceKind,
                    item.SourceRevision,
                    item.ByteFingerprint,
                    item.ByteLength,
                    item.SupplyingSnapshotId,
                    item.RetentionState,
                    item.ReplayState,
                })),
                imports = Sorted(documentation.Imports.Select(item => new
                {
                    anchor = importAnchors[item.ImportId],
                    revision = revisionAnchors[item.RevisionId],
                    reusedImport = item.ReusedImportId is null ? "none"
                        : importAnchors.GetValueOrDefault(item.ReusedImportId, "retained-prior-import"),
                    item.Mode,
                    item.ExtractorId,
                    item.LlmInvolvement,
                    item.LlmOperation,
                    boundaries = Sorted(item.Boundaries.Select(boundary => new { boundary.BoundaryId, boundary.State, boundary.Reason })),
                })),
                passages = Sorted(documentation.Passages.Select(item => new
                {
                    anchor = passageAnchors[item.PassageId],
                    revision = revisionAnchors[item.RevisionId],
                    item.Utf8StartOffset,
                    item.Utf8EndOffset,
                    item.PassageFingerprint,
                    item.State,
                })),
                claims = Sorted(documentation.Claims.Select(item => new
                {
                    anchor = claimAnchors[item.ClaimId],
                    producingImport = importAnchors[item.ProducingImportId],
                    passage = passageAnchors[item.PassageId],
                    item.Kind,
                    item.ExactText,
                    item.Conditions,
                    item.Authority,
                    item.Applicability,
                    item.ClassificationRole,
                    contradictingEvidence = Sorted(item.ContradictingEvidenceIds.Select(Evidence)),
                })),
                applications = Sorted(documentation.Applications.Select(item => new
                {
                    claim = claimAnchors[item.ClaimId],
                    item.SubjectId,
                    item.SubjectType,
                    item.Applicability,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                })),
                purposes = Sorted(documentation.PurposeAssignments.Select(item => new
                {
                    item.TaxonomyId,
                    item.TaxonomyVersion,
                    item.Axis,
                    item.Facet,
                    item.Code,
                    item.Applicability,
                    item.SubjectId,
                    item.SubjectType,
                    item.Role,
                    claim = claimAnchors[item.ClaimId],
                    application = applicationAnchors[item.ApplicationId],
                    applicabilityConditions = Sorted(item.ApplicabilityConditionIds.Select(Evidence)),
                    item.AnalyzerOrAdjudicatorId,
                    item.Reason,
                })),
                deletionReceipts = Sorted(documentation.DeletionReceipts.Select(item => new
                {
                    revision = revisionAnchors[item.RevisionId],
                    item.DeletedBodyFingerprint,
                    deletedPassages = Sorted(item.DeletedPassageIds.Select(id =>
                        passageAnchors.GetValueOrDefault(id, "deleted:" + id.Value))),
                    retainedPayloads = Sorted(item.IndependentlyRetainedPayloadIds.Select(id => id.Value)),
                    item.ReplayEffect,
                    item.Reason,
                })),
                gaps = Sorted(documentation.Gaps.Select(item => new
                {
                    revision = revisionAnchors[item.RevisionId],
                    claim = item.ClaimId is null ? "none" : claimAnchors[item.ClaimId],
                    application = item.ApplicationId is null ? "none" : applicationAnchors[item.ApplicationId],
                    item.Kind,
                    item.ReplayEffect,
                    item.Reason,
                })),
                failures = Sorted(documentation.Failures.Select(item => new
                {
                    item.FailureCode,
                    item.Message,
                    item.Retryable,
                })),
            },
            candidates = new
            {
                candidates.AnalyzerId,
                candidates.PopulationId,
                candidates.PopulationDenominator,
                deliveredInput = SemanticDependency(candidates.DeliveredInputId),
                candidates.PolicyFingerprint,
                candidates.ThresholdFingerprint,
                candidates.LimitFingerprint,
                candidates.AnalyzerSetFingerprint,
                candidates.PolicyDescriptors,
                candidates.ThresholdDescriptors,
                candidates.LimitDescriptors,
                analyzerBindings = Sorted(candidates.AnalyzerBindings.Select(item => new
                {
                    item.AnalyzerId,
                    item.AnalyzerFamily,
                    item.AnalyzerVersion,
                    item.SemanticContractVersion,
                    item.IdentityContractVersion,
                    item.RulesetVersion,
                    item.DeclarationFingerprint,
                    item.CanonicalDeclarationJson,
                })),
                decisions = Sorted(candidates.Decisions.Select(item => new
                {
                    anchor = decisionAnchors[item.DecisionId],
                    item.PopulationMemberId,
                    item.SourceFactId,
                    item.Lane,
                    item.Disposition,
                    participants = Sorted(item.Participants.Select(participant => new { participant.ParticipantId, participant.Role })),
                    item.JoinKind,
                    path = item.Path,
                    item.Rationale,
                    evidence = Sorted(item.EvidenceIds),
                    item.AdmissionIndependentOfScore,
                    item.OptionalRank,
                    item.AnalyzerId,
                    item.PolicyId,
                    item.ThresholdId,
                    item.LimitId,
                    dependencies = Sorted(item.DependencyIds.Select(SemanticDependency)),
                })),
                entries = Sorted(candidates.Candidates.Select(item => new
                {
                    anchor = candidateAnchors[item.CandidateId],
                    decision = decisionAnchors[item.DecisionId],
                    item.State,
                    item.CausalExplanation,
                    supportingEvidence = Sorted(item.SupportingEvidenceIds),
                    contradictingEvidence = Sorted(item.ContradictingEvidenceIds),
                    item.MissingInformation,
                    item.Confidence,
                    item.ThresholdId,
                    hypothesis = item.HypothesisId is null ? null : hypothesisAnchors[item.HypothesisId],
                    abstention = item.AbstentionId is null ? null : abstentionAnchors[item.AbstentionId],
                })),
                hypotheses = Sorted(candidates.Hypotheses.Select(item => new
                {
                    anchor = hypothesisAnchors[item.HypothesisId],
                    candidate = candidateAnchors[item.CandidateId],
                    item.State,
                    item.ProposedExplanation,
                    item.PredictedImpact,
                    supportingEvidence = Sorted(item.SupportingEvidenceIds),
                    contradictingEvidence = Sorted(item.ContradictingEvidenceIds),
                    item.MissingInformation,
                    item.Confidence,
                    item.ThresholdId,
                })),
                abstentions = Sorted(candidates.Abstentions.Select(item => new
                {
                    anchor = abstentionAnchors[item.AbstentionId],
                    decision = decisionAnchors[item.DecisionId],
                    candidate = item.CandidateId is null ? "none" : candidateAnchors[item.CandidateId],
                    item.AnalyzerId,
                    item.Reason,
                    item.RequiredInformation,
                })),
                gaps = Sorted(candidates.Gaps.Select(item => new
                {
                    decision = decisionAnchors[item.DecisionId],
                    item.PopulationId,
                    item.State,
                    item.Reason,
                    item.MissingCapabilityOrInformation,
                })),
                failures = Sorted(candidates.Failures.Select(item => new
                {
                    item.AnalyzerId,
                    populationMembers = Sorted(item.PopulationMemberIds),
                    item.FailureCode,
                    item.Message,
                    item.Retryable,
                })),
                dependencyEdges = Sorted(candidates.DependencyEdges.Select(item => new
                {
                    item.FromKind,
                    from = CandidateGraphNode(item.FromKind, item.FromId),
                    item.ToKind,
                    to = CandidateGraphNode(item.ToKind, item.ToId),
                    item.EdgeKind,
                })),
                candidates.Counts,
            },
            findings = new
            {
                findingCases.PromotionPolicyId,
                findingCases.PromotionPolicyVersion,
                findingCases.ReconciliationPolicyId,
                findingCases.ReconciliationPolicyVersion,
                promotion = Sorted(findingCases.PromotionAssessments.Select(item => new
                {
                    hypothesis = hypothesisAnchors[item.HypothesisId],
                    item.StatePresent,
                    item.ConfidenceAtLeastPlausible,
                    item.HasSupportingEvidence,
                    item.HasNoDefeatingContradictions,
                    item.HasNoMissingInformation,
                    item.SeverityClosed,
                    item.IdentityClosed,
                    item.ConclusionAvailable,
                    item.LeadEligibleState,
                    item.Outcome,
                    item.Reasons,
                })),
                abstentions = Sorted(findingCases.Abstentions.Select(item => new
                {
                    hypothesis = hypothesisAnchors[item.HypothesisId],
                    item.Reason,
                    item.RequiredInformation,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                })),
                items = Sorted(findingCases.Findings.Select(item => new
                {
                    occurrence = Occurrence(item.FindingOccurrenceId),
                    candidate = candidateAnchors[item.CandidateId],
                    hypothesis = hypothesisAnchors[item.HypothesisId],
                    item.Conclusion,
                    item.Severity,
                    item.Confidence,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                    identity = SemanticEnvelope(item.IdentityEnvelope),
                    caseIdentity = SemanticEnvelope(item.CaseIdentityEnvelope),
                    taxonomyAssignments = Sorted(item.TaxonomyAssignmentIds.Select(id => taxonomyAnchors[id])),
                    supersedes = item.SupersedesOccurrenceId is not null
                        && SemanticOccurrence(item.SupersedesOccurrenceId) == Occurrence(item.FindingOccurrenceId)
                            ? "none" : SemanticOccurrence(item.SupersedesOccurrenceId),
                })),
                recommendations = Sorted(findingCases.Recommendations.Select(item => new
                {
                    item.Kind,
                    finding = SemanticOccurrence(item.FindingOccurrenceId),
                    abstention = item.AbstentionId is null ? "none" : findingAbstentionAnchors[item.AbstentionId],
                    lead = item.LeadHypothesisId is null ? null : hypothesisAnchors[item.LeadHypothesisId],
                    item.Action,
                    item.Uncertainty,
                    item.Reversibility,
                    item.Risks,
                    item.Verification,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                })),
                cases = Sorted(findingCases.Cases.Select(item => new
                {
                    occurrence = Occurrence(item.CaseOccurrenceId),
                    item.Kind,
                    findings = Sorted(item.FindingOccurrenceIds.Select(SemanticOccurrence)),
                    candidates = Sorted(item.CandidateIds.Select(id => candidateAnchors[id])),
                    hypotheses = Sorted(item.HypothesisIds.Select(id => hypothesisAnchors[id])),
                    item.SharedCause,
                    causeProof = Sorted(item.CauseProofEvidenceIds.Select(Evidence)),
                    identity = SemanticEnvelope(item.IdentityEnvelope),
                    supersedes = item.SupersedesOccurrenceId is not null
                        && SemanticOccurrence(item.SupersedesOccurrenceId) == Occurrence(item.CaseOccurrenceId)
                            ? "none" : SemanticOccurrence(item.SupersedesOccurrenceId),
                    item.AffectsReadiness,
                })),
                reconciliation = Sorted(findingCases.ReconciliationAssessments
                    .Where(item => !(item.PriorOccurrenceId is null && item.Outcome == ReconciliationOutcome.NewDistinct)
                        && (item.PriorOccurrenceId is null
                            || !exactContinuationAliases.ContainsKey(item.PriorOccurrenceId)))
                    .Select(item => new
                    {
                        item.SubjectKind,
                        prior = SemanticOccurrence(item.PriorOccurrenceId),
                        current = SemanticOccurrence(item.CurrentOccurrenceId),
                        item.Gates,
                        item.Outcome,
                        item.Gaps,
                        considered = Sorted(item.ConsideredOccurrenceIds.Select(SemanticOccurrence)),
                        proof = Sorted(item.ProofEvidenceIds.Select(Evidence)),
                        item.PolicyVersion,
                        item.Mechanism,
                        item.ActorId,
                        item.VisibleByDefault,
                    })),
                lineage = Sorted(findingCases.LineageEvents
                    .Where(item => !(item.PredecessorIds.Count == 1 && item.SuccessorIds.Count == 1
                        && SemanticOccurrence(item.PredecessorIds[0]) == SemanticOccurrence(item.SuccessorIds[0])))
                    .Select(item => new
                    {
                        item.Kind,
                        predecessors = Sorted(item.PredecessorIds.Select(SemanticOccurrence)),
                        successors = Sorted(item.SuccessorIds.Select(SemanticOccurrence)),
                        reconciliation = item.ReconciliationAssessmentId is null ? "none"
                        : findingCases.ReconciliationAssessments.Single(value => value.AssessmentId == item.ReconciliationAssessmentId).Outcome.ToString(),
                    })),
                taxonomy = Sorted(findingCases.TaxonomyAssignments.Select(item => new
                {
                    item.TaxonomyId,
                    item.TaxonomyVersion,
                    item.Axis,
                    item.Facet,
                    item.Code,
                    item.Applicability,
                    item.SubjectType,
                    item.Role,
                    anchor = taxonomyAnchors[item.AssignmentId],
                    subject = Subject(item.SubjectId),
                    item.ConfidenceAssessmentId,
                    supersedes = Sorted(item.SupersedesAssignmentIds.Select(id => taxonomyAnchors.GetValueOrDefault(id, "external:" + id.Value))),
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                    applicabilityConditions = Sorted(item.ApplicabilityConditionIds.Select(Evidence)),
                    item.AnalyzerOrAdjudicatorId,
                    item.Reason,
                })),
                taxonomyProjections = Sorted(findingCases.TaxonomyProjections.Select(item => new
                {
                    source = taxonomyAnchors[item.SourceAssignmentId],
                    projected = taxonomyAnchors[item.ProjectedAssignmentId],
                    item.MappingAuthorityId,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                    item.Reason,
                })),
                coverage = Sorted(findingCases.Coverage.Select(item => new
                {
                    item.AnalyzerId,
                    item.PopulationId,
                    item.DenominatorLabel,
                    item.Denominator,
                    item.CompletedCount,
                    item.State,
                    item.TaxonomyId,
                    item.TaxonomyVersion,
                    assignments = Sorted(item.TaxonomyAssignmentIds.Select(id => taxonomyAnchors[id])),
                    gaps = Sorted(item.GapIds.Select(id => findingGapAnchors[id])),
                    failures = Sorted(item.FailureIds.Select(id => coverageFailureAnchors[id])),
                    exclusions = Sorted(item.Exclusions.Select(exclusion => new
                    {
                        member = Subject(exclusion.MemberId),
                        exclusion.Reason,
                        exclusion.State,
                    })),
                    members = Sorted(item.MemberResults.Select(member => new
                    {
                        member = Subject(member.MemberId),
                        member.State,
                        member.Reason,
                        member.MissingCapabilityOrInformation,
                        failure = member.FailureId is null ? "none" : coverageFailureAnchors[member.FailureId],
                        gap = member.GapId is null ? "none" : findingGapAnchors[member.GapId],
                        taxonomy = Sorted(member.TaxonomyAssignmentIds.Select(id => taxonomyAnchors[id])),
                    })),
                })),
                coverageFailures = Sorted(findingCases.CoverageFailures.Select(item => new
                {
                    item.AnalyzerId,
                    item.FailureCode,
                    item.Message,
                    item.Retryable,
                })),
                gaps = Sorted(findingCases.Gaps.Select(item => new
                {
                    item.PopulationId,
                    item.StageId,
                    item.State,
                    item.ReplayEffect,
                    item.ConclusionEffect,
                    item.Reason,
                    item.MissingCapabilityOrInformation,
                    evidence = Sorted(item.EvidenceIds.Select(Evidence)),
                })),
                boundaries = Sorted(findingCases.Boundaries.Select(item => new { item.BoundaryId, item.State, item.Reason })),
                findingCases.PublicationClaimBoundary,
            },
        };
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(projection, SchemaValidatedJsonCodec.JsonOptions);
        cancellationToken.ThrowIfCancellationRequested();
        return bytes;
    }

    private static ReplayOutputContract ReplayOutput(RetainedAnalysisPayloadSeal seal, OpaqueId artifactId, string sha) =>
        new(artifactId, seal.SchemaId, ContractVersion.Parse(seal.SchemaVersion), new Sha256Fingerprint(sha), new Sha256Fingerprint(sha));

    private static AnalysisPublishedArtifact Published(RetainedAnalysisPayloadSeal seal, string artifactId, string kind, string closure) =>
        new(artifactId, kind, seal.SchemaId, seal.SchemaVersion, 1, "present", seal.Sha256, seal.ByteLength,
            StableId("provenance", artifactId), closure);

}
