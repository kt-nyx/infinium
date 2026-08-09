using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed partial class AuthoritativeStore
{
    internal FindingCasePersistenceReceipt PublishFindingCase(
        FindingCaseContract value,
        ReadOnlyMemory<byte> serializedValue,
        AttemptRecord attempt,
        RunBinding binding,
        DateTimeOffset now)
    {
        Slice5ContractInvariants.Validate(value);
        ArgumentNullException.ThrowIfNull(attempt);
        ValidateBinding(binding);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentAttempt(attempt, transaction);
            if (!StringComparer.Ordinal.Equals(value.OriginatingRunId.Value, attempt.RunId)
                || ScalarLong(
                    """
                    SELECT COUNT(*) FROM runs
                    WHERE run_id = $run AND installation_snapshot_id = $snapshot
                      AND analysis_context_id = $context AND effective_scan_configuration_id = $config
                      AND resolved_input_manifest_id = $manifest;
                    """, transaction,
                    ("$run", attempt.RunId), ("$snapshot", binding.InstallationSnapshotId),
                    ("$context", binding.AnalysisContextId), ("$config", binding.EffectiveScanConfigurationId),
                    ("$manifest", binding.ResolvedInputManifestId)) != 1)
            {
                throw new InvalidOperationException("Finding/case publication dependencies differ from the current immutable attempt binding.");
            }
            EnsureRunExists(value.OriginatingRunId.Value, transaction);
            string payloadId = AdmitCoordinatorPayload(
                serializedValue.Span, "finding-case", value.PayloadId.Value, now, transaction);
            string boundariesJson = JsonSerializer.Serialize(value.Boundaries, ContractJsonSerializer.Options);
            Execute(
                """
                INSERT OR IGNORE INTO finding_case_publications(
                    finding_case_payload_id,run_id,input_id,promotion_policy_id,promotion_policy_version,
                    reconciliation_policy_id,reconciliation_policy_version,boundaries_json,
                    publication_claim_boundary,created_at)
                VALUES ($payload,$run,$input,$promotion,$promotion_version,$reconciliation,
                    $reconciliation_version,$boundaries,$claim,$now);
                """, transaction,
                ("$payload", payloadId), ("$run", value.OriginatingRunId.Value),
                ("$input", value.InputId.Value), ("$promotion", value.PromotionPolicyId.Value),
                ("$promotion_version", value.PromotionPolicyVersion.ToString()),
                ("$reconciliation", value.ReconciliationPolicyId.Value),
                ("$reconciliation_version", value.ReconciliationPolicyVersion.ToString()),
                ("$boundaries", boundariesJson), ("$claim", value.PublicationClaimBoundary),
                ("$now", ToText(now)));
            RequireFindingCaseRow(
                """
                SELECT COUNT(*) FROM finding_case_publications
                WHERE finding_case_payload_id=$payload AND run_id=$run AND input_id=$input
                  AND promotion_policy_id=$promotion AND promotion_policy_version=$promotion_version
                  AND reconciliation_policy_id=$reconciliation
                  AND reconciliation_policy_version=$reconciliation_version
                  AND boundaries_json=$boundaries AND publication_claim_boundary=$claim;
                """, "A finding/case publication resolves to different retained semantics.", transaction,
                ("$payload", payloadId), ("$run", value.OriginatingRunId.Value),
                ("$input", value.InputId.Value), ("$promotion", value.PromotionPolicyId.Value),
                ("$promotion_version", value.PromotionPolicyVersion.ToString()),
                ("$reconciliation", value.ReconciliationPolicyId.Value),
                ("$reconciliation_version", value.ReconciliationPolicyVersion.ToString()),
                ("$boundaries", boundariesJson), ("$claim", value.PublicationClaimBoundary));
            foreach (FindingPromotionAssessmentContract assessment in value.PromotionAssessments)
            {
                string reasonsJson = JsonSerializer.Serialize(assessment.Reasons.Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO finding_promotion_assessments(
                        promotion_assessment_id,run_id,hypothesis_id,state_present,
                        confidence_at_least_plausible,has_supporting_evidence,has_no_defeating_contradictions,
                        has_no_missing_information,severity_closed,identity_closed,conclusion_available,
                        lead_eligible_state,promotion_outcome,
                        reasons_json,assessment_payload_id,created_at)
                    VALUES ($id,$run,$hypothesis,$present,$confidence,$support,$contradictions,
                        $missing,$severity,$identity,$conclusion,$leadEligible,$outcome,$reasons,$payload,$now);
                    """, transaction,
                    ("$id", assessment.AssessmentId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$hypothesis", assessment.HypothesisId.Value), ("$present", assessment.StatePresent ? 1 : 0),
                    ("$confidence", assessment.ConfidenceAtLeastPlausible ? 1 : 0),
                    ("$support", assessment.HasSupportingEvidence ? 1 : 0),
                    ("$contradictions", assessment.HasNoDefeatingContradictions ? 1 : 0),
                    ("$missing", assessment.HasNoMissingInformation ? 1 : 0),
                    ("$severity", assessment.SeverityClosed ? 1 : 0),
                    ("$identity", assessment.IdentityClosed ? 1 : 0),
                    ("$conclusion", assessment.ConclusionAvailable ? 1 : 0),
                    ("$leadEligible", assessment.LeadEligibleState ? 1 : 0),
                    ("$outcome", Kebab(assessment.Outcome)), ("$reasons", reasonsJson),
                    ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM finding_promotion_assessments
                    WHERE promotion_assessment_id=$id AND run_id=$run AND hypothesis_id=$hypothesis
                      AND state_present=$present AND confidence_at_least_plausible=$confidence
                      AND has_supporting_evidence=$support AND has_no_defeating_contradictions=$contradictions
                      AND has_no_missing_information=$missing AND severity_closed=$severity
                      AND identity_closed=$identity AND conclusion_available=$conclusion
                      AND lead_eligible_state=$leadEligible
                      AND promotion_outcome=$outcome AND reasons_json=$reasons AND assessment_payload_id=$payload;
                    """, "A promotion-assessment ID resolves to different retained semantics.", transaction,
                    ("$id", assessment.AssessmentId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$hypothesis", assessment.HypothesisId.Value), ("$present", assessment.StatePresent ? 1 : 0),
                    ("$confidence", assessment.ConfidenceAtLeastPlausible ? 1 : 0),
                    ("$support", assessment.HasSupportingEvidence ? 1 : 0),
                    ("$contradictions", assessment.HasNoDefeatingContradictions ? 1 : 0),
                    ("$missing", assessment.HasNoMissingInformation ? 1 : 0),
                    ("$severity", assessment.SeverityClosed ? 1 : 0),
                    ("$identity", assessment.IdentityClosed ? 1 : 0),
                    ("$conclusion", assessment.ConclusionAvailable ? 1 : 0),
                    ("$leadEligible", assessment.LeadEligibleState ? 1 : 0),
                    ("$outcome", Kebab(assessment.Outcome)),
                    ("$reasons", reasonsJson), ("$payload", payloadId));
            }
            foreach (FindingCaseAbstentionContract abstention in value.Abstentions)
            {
                string requiredJson = JsonSerializer.Serialize(abstention.RequiredInformation.Order(StringComparer.Ordinal).ToArray());
                string evidenceJson = JsonSerializer.Serialize(abstention.EvidenceIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO finding_case_abstentions(
                        abstention_id,run_id,hypothesis_id,reason,required_information_json,
                        evidence_ids_json,abstention_payload_id,created_at)
                    VALUES ($id,$run,$hypothesis,$reason,$required,$evidence,$payload,$now);
                    """, transaction,
                    ("$id", abstention.AbstentionId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$hypothesis", abstention.HypothesisId.Value), ("$reason", abstention.Reason),
                    ("$required", requiredJson), ("$evidence", evidenceJson),
                    ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM finding_case_abstentions
                    WHERE abstention_id=$id AND run_id=$run AND hypothesis_id=$hypothesis
                      AND reason=$reason AND required_information_json=$required
                      AND evidence_ids_json=$evidence AND abstention_payload_id=$payload;
                    """, "An abstention ID resolves to different retained semantics.", transaction,
                    ("$id", abstention.AbstentionId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$hypothesis", abstention.HypothesisId.Value), ("$reason", abstention.Reason),
                    ("$required", requiredJson), ("$evidence", evidenceJson), ("$payload", payloadId));
            }
            foreach (FindingContract finding in value.Findings)
            {
                string identityPayload = AdmitJsonPayload(
                    finding.IdentityEnvelope, "finding-identity-envelope",
                    finding.IdentityEnvelopeId.Value, now, transaction);
                Execute("INSERT OR IGNORE INTO logical_findings(logical_finding_id,created_at) VALUES ($id,$now);",
                    transaction, ("$id", finding.LogicalFindingId.Value), ("$now", ToText(now)));
                Execute(
                    """
                    INSERT OR IGNORE INTO finding_occurrences(
                        finding_occurrence_id,logical_finding_id,run_id,analyzer_family,analyzer_version,
                        semantic_contract_version,identity_contract_version,identity_envelope_payload_id,
                        canonical_signature,dependency_closure_id,created_at)
                    VALUES ($occurrence,$logical,$run,$analyzer,$analyzerVersion,$semantic,$identity,$envelope,$signature,$closure,$now);
                    """, transaction,
                    ("$occurrence", finding.FindingOccurrenceId.Value), ("$logical", finding.LogicalFindingId.Value),
                    ("$run", value.OriginatingRunId.Value), ("$analyzer", finding.IdentityEnvelope.AnalyzerFamily),
                    ("$analyzerVersion", finding.IdentityEnvelope.AnalyzerVersion.ToString()),
                    ("$semantic", finding.IdentityEnvelope.SemanticContractVersion.ToString()),
                    ("$identity", finding.IdentityEnvelope.IdentityContractVersion.ToString()),
                    ("$envelope", identityPayload), ("$signature", finding.IdentityEnvelope.CanonicalSignature.Value),
                    ("$closure", finding.IdentityEnvelope.DependencyClosureId.Value), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM finding_occurrences
                    WHERE finding_occurrence_id=$occurrence AND logical_finding_id=$logical AND run_id=$run
                      AND analyzer_family=$analyzer AND analyzer_version=$analyzerVersion
                      AND semantic_contract_version=$semantic AND identity_contract_version=$identity
                      AND identity_envelope_payload_id=$envelope AND canonical_signature=$signature
                      AND dependency_closure_id=$closure;
                    """, "A finding-occurrence ID resolves to different retained semantics.", transaction,
                    ("$occurrence", finding.FindingOccurrenceId.Value), ("$logical", finding.LogicalFindingId.Value),
                    ("$run", value.OriginatingRunId.Value), ("$analyzer", finding.IdentityEnvelope.AnalyzerFamily),
                    ("$analyzerVersion", finding.IdentityEnvelope.AnalyzerVersion.ToString()),
                    ("$semantic", finding.IdentityEnvelope.SemanticContractVersion.ToString()),
                    ("$identity", finding.IdentityEnvelope.IdentityContractVersion.ToString()), ("$envelope", identityPayload),
                    ("$signature", finding.IdentityEnvelope.CanonicalSignature.Value),
                    ("$closure", finding.IdentityEnvelope.DependencyClosureId.Value));
                Execute(
                    """
                    INSERT OR IGNORE INTO finding_occurrence_details(
                        finding_occurrence_id,candidate_id,confidence,severity,finding_payload_id,created_at)
                    VALUES ($occurrence,$candidate,$confidence,$severity,$payload,$now);
                    """, transaction,
                    ("$occurrence", finding.FindingOccurrenceId.Value), ("$candidate", finding.CandidateId.Value),
                    ("$confidence", Kebab(finding.Confidence)), ("$severity", Kebab(finding.Severity)),
                    ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM finding_occurrence_details
                    WHERE finding_occurrence_id=$occurrence AND candidate_id=$candidate
                      AND confidence=$confidence AND severity=$severity AND finding_payload_id=$payload;
                    """, "A finding-occurrence detail resolves to different retained semantics.", transaction,
                    ("$occurrence", finding.FindingOccurrenceId.Value), ("$candidate", finding.CandidateId.Value),
                    ("$confidence", Kebab(finding.Confidence)), ("$severity", Kebab(finding.Severity)),
                    ("$payload", payloadId));
                string caseIdentityPayload = AdmitJsonPayload(
                    finding.CaseIdentityEnvelope, "case-identity-envelope",
                    finding.CaseIdentityEnvelopeId.Value, now, transaction);
                string findingEvidenceJson = JsonSerializer.Serialize(
                    finding.EvidenceIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                string assignmentIdsJson = JsonSerializer.Serialize(
                    finding.TaxonomyAssignmentIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO finding_case_finding_details(
                        finding_occurrence_id,hypothesis_id,conclusion,evidence_ids_json,
                        case_identity_envelope_payload_id,taxonomy_assignment_ids_json,semantic_fingerprint,
                        supersedes_occurrence_id,detail_payload_id,created_at)
                    VALUES ($occurrence,$hypothesis,$conclusion,$evidence,$case_identity,$assignments,
                        $fingerprint,$supersedes,$payload,$now);
                    """, transaction,
                    ("$occurrence", finding.FindingOccurrenceId.Value), ("$hypothesis", finding.HypothesisId.Value),
                    ("$conclusion", finding.Conclusion), ("$evidence", findingEvidenceJson),
                    ("$case_identity", caseIdentityPayload), ("$assignments", assignmentIdsJson),
                    ("$fingerprint", finding.SemanticFingerprint.Value),
                    ("$supersedes", finding.SupersedesOccurrenceId?.Value),
                    ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM finding_case_finding_details
                    WHERE finding_occurrence_id=$occurrence AND hypothesis_id=$hypothesis
                      AND conclusion=$conclusion AND evidence_ids_json=$evidence
                      AND case_identity_envelope_payload_id=$case_identity
                      AND taxonomy_assignment_ids_json=$assignments AND semantic_fingerprint=$fingerprint
                      AND supersedes_occurrence_id IS $supersedes AND detail_payload_id=$payload;
                    """, "A finding material-detail row resolves to different retained semantics.", transaction,
                    ("$occurrence", finding.FindingOccurrenceId.Value), ("$hypothesis", finding.HypothesisId.Value),
                    ("$conclusion", finding.Conclusion), ("$evidence", findingEvidenceJson),
                    ("$case_identity", caseIdentityPayload), ("$assignments", assignmentIdsJson),
                    ("$fingerprint", finding.SemanticFingerprint.Value),
                    ("$supersedes", finding.SupersedesOccurrenceId?.Value), ("$payload", payloadId));
            }
            foreach (Slice5RecommendationContract recommendation in value.Recommendations)
            {
                string risksJson = JsonSerializer.Serialize(recommendation.Risks.Order(StringComparer.Ordinal).ToArray());
                string evidenceJson = JsonSerializer.Serialize(
                    recommendation.EvidenceIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO finding_case_recommendations(
                        recommendation_id,run_id,finding_occurrence_id,abstention_id,lead_hypothesis_id,
                        recommendation_kind,action,uncertainty,reversibility,verification,risks_json,evidence_ids_json,
                        recommendation_payload_id,created_at)
                    VALUES ($id,$run,$finding,$abstention,$lead,$kind,$action,$uncertainty,$reversibility,
                        $verification,$risks,$evidence,$payload,$now);
                    """, transaction,
                    ("$id", recommendation.RecommendationId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$finding", recommendation.FindingOccurrenceId?.Value),
                    ("$abstention", recommendation.AbstentionId?.Value),
                    ("$lead", recommendation.LeadHypothesisId?.Value),
                    ("$kind", Kebab(recommendation.Kind)), ("$action", recommendation.Action),
                    ("$uncertainty", recommendation.Uncertainty), ("$reversibility", recommendation.Reversibility),
                    ("$verification", recommendation.Verification),
                    ("$risks", risksJson), ("$evidence", evidenceJson),
                    ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM finding_case_recommendations
                    WHERE recommendation_id=$id AND run_id=$run
                      AND finding_occurrence_id IS $finding AND abstention_id IS $abstention
                      AND lead_hypothesis_id IS $lead AND recommendation_kind=$kind
                      AND action=$action AND uncertainty=$uncertainty AND reversibility=$reversibility
                      AND verification=$verification
                      AND risks_json=$risks AND evidence_ids_json=$evidence
                      AND recommendation_payload_id=$payload;
                    """, "A recommendation ID resolves to different retained semantics.", transaction,
                    ("$id", recommendation.RecommendationId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$finding", recommendation.FindingOccurrenceId?.Value),
                    ("$abstention", recommendation.AbstentionId?.Value), ("$lead", recommendation.LeadHypothesisId?.Value),
                    ("$kind", Kebab(recommendation.Kind)), ("$action", recommendation.Action),
                    ("$uncertainty", recommendation.Uncertainty), ("$reversibility", recommendation.Reversibility),
                    ("$verification", recommendation.Verification),
                    ("$risks", risksJson), ("$evidence", evidenceJson), ("$payload", payloadId));
            }
            foreach (Slice5CaseContract @case in value.Cases)
            {
                string identityPayload = AdmitJsonPayload(
                    @case.IdentityEnvelope, "case-identity-envelope",
                    @case.IdentityEnvelopeId.Value, now, transaction);
                Execute("INSERT OR IGNORE INTO logical_cases(logical_case_id,created_at) VALUES ($id,$now);",
                    transaction, ("$id", @case.LogicalCaseId.Value), ("$now", ToText(now)));
                Execute(
                    """
                    INSERT OR IGNORE INTO case_occurrences(
                        case_occurrence_id,logical_case_id,run_id,identity_envelope_payload_id,
                        shared_cause_signature,dependency_closure_id,created_at)
                    VALUES ($occurrence,$logical,$run,$envelope,$signature,$closure,$now);
                    """, transaction,
                    ("$occurrence", @case.CaseOccurrenceId.Value), ("$logical", @case.LogicalCaseId.Value),
                    ("$run", value.OriginatingRunId.Value), ("$envelope", identityPayload),
                    ("$signature", @case.IdentityEnvelope.CanonicalSignature.Value),
                    ("$closure", @case.IdentityEnvelope.DependencyClosureId.Value), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM case_occurrences
                    WHERE case_occurrence_id=$occurrence AND logical_case_id=$logical AND run_id=$run
                      AND identity_envelope_payload_id=$envelope AND shared_cause_signature=$signature
                      AND dependency_closure_id=$closure;
                    """, "A case-occurrence ID resolves to different retained semantics.", transaction,
                    ("$occurrence", @case.CaseOccurrenceId.Value), ("$logical", @case.LogicalCaseId.Value),
                    ("$run", value.OriginatingRunId.Value), ("$envelope", identityPayload),
                    ("$signature", @case.IdentityEnvelope.CanonicalSignature.Value),
                    ("$closure", @case.IdentityEnvelope.DependencyClosureId.Value));
                Execute(
                    """
                    INSERT OR IGNORE INTO case_occurrence_details(
                        case_occurrence_id,case_kind,affects_readiness,case_payload_id,created_at)
                    VALUES ($occurrence,$kind,$readiness,$payload,$now);
                    """, transaction,
                    ("$occurrence", @case.CaseOccurrenceId.Value), ("$kind", Kebab(@case.Kind)),
                    ("$readiness", @case.AffectsReadiness ? 1 : 0), ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM case_occurrence_details
                    WHERE case_occurrence_id=$occurrence AND case_kind=$kind
                      AND affects_readiness=$readiness AND case_payload_id=$payload;
                    """, "A case-occurrence detail resolves to different retained semantics.", transaction,
                    ("$occurrence", @case.CaseOccurrenceId.Value), ("$kind", Kebab(@case.Kind)),
                    ("$readiness", @case.AffectsReadiness ? 1 : 0), ("$payload", payloadId));
                string causeEvidenceJson = JsonSerializer.Serialize(
                    @case.CauseProofEvidenceIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO finding_case_case_details(
                        case_occurrence_id,shared_cause,cause_proof_evidence_ids_json,semantic_fingerprint,
                        supersedes_occurrence_id,detail_payload_id,created_at)
                    VALUES ($occurrence,$cause,$evidence,$fingerprint,$supersedes,$payload,$now);
                    """, transaction,
                    ("$occurrence", @case.CaseOccurrenceId.Value), ("$cause", @case.SharedCause),
                    ("$evidence", causeEvidenceJson), ("$fingerprint", @case.SemanticFingerprint.Value),
                    ("$supersedes", @case.SupersedesOccurrenceId?.Value),
                    ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM finding_case_case_details
                    WHERE case_occurrence_id=$occurrence AND shared_cause=$cause
                      AND cause_proof_evidence_ids_json=$evidence AND semantic_fingerprint=$fingerprint
                      AND supersedes_occurrence_id IS $supersedes AND detail_payload_id=$payload;
                    """, "A case material-detail row resolves to different retained semantics.", transaction,
                    ("$occurrence", @case.CaseOccurrenceId.Value), ("$cause", @case.SharedCause),
                    ("$evidence", causeEvidenceJson), ("$fingerprint", @case.SemanticFingerprint.Value),
                    ("$supersedes", @case.SupersedesOccurrenceId?.Value), ("$payload", payloadId));
                foreach (OpaqueId findingId in @case.FindingOccurrenceIds)
                {
                    InsertMembership(@case.CaseOccurrenceId, "finding-occurrence", findingId, "effect", payloadId, now, transaction);
                }
                foreach (OpaqueId candidateId in @case.CandidateIds)
                {
                    InsertMembership(@case.CaseOccurrenceId, "candidate", candidateId,
                        @case.Kind == CaseOccurrenceKind.LeadOnly ? "lead" : "cause", payloadId, now, transaction);
                }
                foreach (OpaqueId hypothesisId in @case.HypothesisIds)
                {
                    InsertMembership(@case.CaseOccurrenceId, "hypothesis", hypothesisId,
                        @case.Kind == CaseOccurrenceKind.LeadOnly ? "lead" : "cause", payloadId, now, transaction);
                }
            }
            foreach (TaxonomyAssignmentContract assignment in value.TaxonomyAssignments)
            {
                string evidenceJson = JsonSerializer.Serialize(
                    assignment.EvidenceIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                string conditionsJson = JsonSerializer.Serialize(
                    assignment.ApplicabilityConditionIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                string supersedesJson = JsonSerializer.Serialize(
                    assignment.SupersedesAssignmentIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO finding_case_taxonomy_assignments(
                        taxonomy_assignment_id,run_id,subject_kind,subject_id,taxonomy_id,taxonomy_version,
                        axis,facet,taxonomy_code,applicability_state,classification_role,
                        evidence_ids_json,applicability_condition_ids_json,confidence_assessment_id,
                        analyzer_or_adjudicator_id,reason,supersedes_assignment_ids_json,
                        assignment_payload_id,created_at)
                    VALUES ($id,$run,$subject_kind,$subject,$taxonomy,$version,$axis,$facet,$code,$state,$role,
                        $evidence,$conditions,$confidence,$actor,$reason,$supersedes,$payload,$created);
                    """, transaction,
                    ("$id", assignment.AssignmentId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$subject_kind", assignment.SubjectType), ("$subject", assignment.SubjectId.Value),
                    ("$taxonomy", assignment.TaxonomyId), ("$version", assignment.TaxonomyVersion.ToString()),
                    ("$axis", assignment.Axis), ("$facet", assignment.Facet), ("$code", assignment.Code),
                    ("$state", Kebab(assignment.Applicability)),
                    ("$role", assignment.Role is null ? null : Kebab(assignment.Role.Value)),
                    ("$evidence", evidenceJson), ("$conditions", conditionsJson),
                    ("$confidence", assignment.ConfidenceAssessmentId?.Value),
                    ("$actor", assignment.AnalyzerOrAdjudicatorId.Value), ("$reason", assignment.Reason),
                    ("$supersedes", supersedesJson),
                    ("$payload", payloadId), ("$created", assignment.CreatedAt.ToString()));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM finding_case_taxonomy_assignments
                    WHERE taxonomy_assignment_id=$id AND run_id=$run AND subject_kind=$subject_kind
                      AND subject_id=$subject AND taxonomy_id=$taxonomy AND taxonomy_version=$version
                      AND axis=$axis AND facet=$facet AND taxonomy_code IS $code
                      AND applicability_state=$state AND classification_role IS $role
                      AND evidence_ids_json=$evidence AND applicability_condition_ids_json=$conditions
                      AND confidence_assessment_id IS $confidence AND analyzer_or_adjudicator_id=$actor
                      AND reason=$reason AND supersedes_assignment_ids_json=$supersedes
                      AND assignment_payload_id=$payload AND created_at=$created;
                    """, "A taxonomy-assignment ID resolves to different retained semantics.", transaction,
                    ("$id", assignment.AssignmentId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$subject_kind", assignment.SubjectType), ("$subject", assignment.SubjectId.Value),
                    ("$taxonomy", assignment.TaxonomyId), ("$version", assignment.TaxonomyVersion.ToString()),
                    ("$axis", assignment.Axis), ("$facet", assignment.Facet), ("$code", assignment.Code),
                    ("$state", Kebab(assignment.Applicability)),
                    ("$role", assignment.Role is null ? null : Kebab(assignment.Role.Value)),
                    ("$evidence", evidenceJson), ("$conditions", conditionsJson),
                    ("$confidence", assignment.ConfidenceAssessmentId?.Value),
                    ("$actor", assignment.AnalyzerOrAdjudicatorId.Value), ("$reason", assignment.Reason),
                    ("$supersedes", supersedesJson),
                    ("$payload", payloadId), ("$created", assignment.CreatedAt.ToString()));
            }
            foreach (TaxonomyProjectionContract projection in value.TaxonomyProjections)
            {
                string evidenceJson = JsonSerializer.Serialize(
                    projection.EvidenceIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO taxonomy_projection_edges(
                        taxonomy_projection_id,source_assignment_id,projected_assignment_id,
                        mapping_authority_id,evidence_ids_json,reason,projection_payload_id,created_at)
                    VALUES ($id,$source,$projected,$authority,$evidence,$reason,$payload,$now);
                    """, transaction,
                    ("$id", projection.ProjectionId.Value), ("$source", projection.SourceAssignmentId.Value),
                    ("$projected", projection.ProjectedAssignmentId.Value),
                    ("$authority", projection.MappingAuthorityId.Value), ("$evidence", evidenceJson),
                    ("$reason", projection.Reason), ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM taxonomy_projection_edges
                    WHERE taxonomy_projection_id=$id AND source_assignment_id=$source
                      AND projected_assignment_id=$projected AND mapping_authority_id=$authority
                      AND evidence_ids_json=$evidence AND reason=$reason AND projection_payload_id=$payload;
                    """, "A taxonomy-projection ID resolves to different retained semantics.", transaction,
                    ("$id", projection.ProjectionId.Value), ("$source", projection.SourceAssignmentId.Value),
                    ("$projected", projection.ProjectedAssignmentId.Value), ("$authority", projection.MappingAuthorityId.Value),
                    ("$evidence", evidenceJson), ("$reason", projection.Reason), ("$payload", payloadId));
            }
            foreach (FindingCaseGapContract gap in value.Gaps)
            {
                Execute(
                    """
                    INSERT OR IGNORE INTO analysis_gaps(
                        gap_id,run_id,population_id,stage_id,gap_state,replay_effect,
                        conclusion_effect,gap_payload_id,created_at)
                    VALUES ($id,$run,$population,$stage,$state,$replay,$conclusion,$payload,$now);
                    """, transaction,
                    ("$id", gap.GapId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$population", gap.PopulationId), ("$stage", gap.StageId),
                    ("$state", Kebab(gap.State)), ("$replay", Kebab(gap.ReplayEffect)),
                    ("$conclusion", Kebab(gap.ConclusionEffect)), ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM analysis_gaps
                    WHERE gap_id=$id AND run_id=$run AND population_id=$population AND stage_id=$stage
                      AND gap_state=$state AND replay_effect=$replay AND conclusion_effect=$conclusion
                      AND gap_payload_id=$payload;
                    """, "A finding/case gap ID resolves to different retained semantics.", transaction,
                    ("$id", gap.GapId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$population", gap.PopulationId), ("$stage", gap.StageId),
                    ("$state", Kebab(gap.State)), ("$replay", Kebab(gap.ReplayEffect)),
                    ("$conclusion", Kebab(gap.ConclusionEffect)), ("$payload", payloadId));
                string evidenceJson = JsonSerializer.Serialize(
                    gap.EvidenceIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO finding_case_gap_details(
                        gap_id,reason,missing_capability_or_information,evidence_ids_json,
                        detail_payload_id,created_at)
                    VALUES ($id,$reason,$missing,$evidence,$payload,$now);
                    """, transaction,
                    ("$id", gap.GapId.Value), ("$reason", gap.Reason),
                    ("$missing", gap.MissingCapabilityOrInformation), ("$evidence", evidenceJson),
                    ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM finding_case_gap_details
                    WHERE gap_id=$id AND reason=$reason AND missing_capability_or_information=$missing
                      AND evidence_ids_json=$evidence AND detail_payload_id=$payload;
                    """, "A finding/case gap detail resolves to different retained semantics.", transaction,
                    ("$id", gap.GapId.Value), ("$reason", gap.Reason),
                    ("$missing", gap.MissingCapabilityOrInformation), ("$evidence", evidenceJson),
                    ("$payload", payloadId));
            }
            foreach (CoverageContract coverage in value.Coverage)
            {
                string exclusionsJson = JsonSerializer.Serialize(coverage.Exclusions);
                string memberResultsJson = JsonSerializer.Serialize(coverage.MemberResults);
                Execute(
                    """
                    INSERT OR IGNORE INTO analysis_coverage(
                        coverage_result_id,run_id,population_id,coverage_state,denominator,completed,
                        excluded,taxonomy_id,taxonomy_version,coverage_payload_id,created_at,
                        analyzer_id,denominator_label,exclusions_json,member_results_json)
                    VALUES ($id,$run,$population,$state,$denominator,$completed,$excluded,$taxonomy,$version,$payload,$now,
                        $analyzer,$label,$exclusions,$members);
                    """, transaction,
                    ("$id", coverage.CoverageId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$population", coverage.PopulationId), ("$state", Kebab(coverage.State)),
                    ("$denominator", coverage.Denominator), ("$completed", coverage.CompletedCount),
                    ("$excluded", coverage.Exclusions.Count), ("$taxonomy", coverage.TaxonomyId),
                    ("$version", coverage.TaxonomyVersion.ToString()), ("$payload", payloadId), ("$now", ToText(now)),
                    ("$analyzer", coverage.AnalyzerId.Value), ("$label", coverage.DenominatorLabel),
                    ("$exclusions", exclusionsJson), ("$members", memberResultsJson));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM analysis_coverage
                    WHERE coverage_result_id=$id AND run_id=$run AND population_id=$population
                      AND coverage_state=$state AND denominator=$denominator AND completed=$completed
                      AND excluded=$excluded AND taxonomy_id=$taxonomy AND taxonomy_version=$version
                      AND coverage_payload_id=$payload AND analyzer_id=$analyzer
                      AND denominator_label=$label AND exclusions_json=$exclusions AND member_results_json=$members;
                    """, "A coverage-result ID resolves to different retained semantics.", transaction,
                    ("$id", coverage.CoverageId.Value), ("$run", value.OriginatingRunId.Value),
                    ("$population", coverage.PopulationId), ("$state", Kebab(coverage.State)),
                    ("$denominator", coverage.Denominator), ("$completed", coverage.CompletedCount),
                    ("$excluded", coverage.Exclusions.Count), ("$taxonomy", coverage.TaxonomyId),
                    ("$version", coverage.TaxonomyVersion.ToString()), ("$payload", payloadId),
                    ("$analyzer", coverage.AnalyzerId.Value), ("$label", coverage.DenominatorLabel),
                    ("$exclusions", exclusionsJson), ("$members", memberResultsJson));
                foreach (OpaqueId assignmentId in coverage.TaxonomyAssignmentIds)
                {
                    InsertCoverageLink("taxonomy", coverage.CoverageId, assignmentId, null, payloadId, now, transaction);
                }
                foreach (OpaqueId gapId in coverage.GapIds)
                {
                    InsertCoverageLink("gap", coverage.CoverageId, gapId, null, payloadId, now, transaction);
                }
                foreach (OpaqueId failureId in coverage.FailureIds)
                {
                    CoverageFailureFactContract failure = value.CoverageFailures.Single(item => item.FailureId == failureId);
                    InsertCoverageLink("failure", coverage.CoverageId, failureId, failure, payloadId, now, transaction);
                }
            }
            foreach (Slice5ReconciliationContract reconciliation in value.ReconciliationAssessments)
            {
                Execute(
                    """
                    INSERT OR IGNORE INTO reconciliation_assessments(
                        reconciliation_assessment_id,subject_kind,predecessor_occurrence_id,successor_occurrence_id,
                        causal_gate,applicability_gate,dependency_gate,producer_compatibility_gate,
                        outcome,proof_payload_id,policy_version,created_at)
                    VALUES ($id,$subject,$prior,$current,$causal,$applicability,$dependency,$producer,$outcome,$payload,$policy,$now);
                    """, transaction,
                    ("$id", reconciliation.AssessmentId.Value), ("$subject", reconciliation.SubjectKind),
                    ("$prior", reconciliation.PriorOccurrenceId?.Value), ("$current", reconciliation.CurrentOccurrenceId?.Value),
                    ("$causal", Kebab(reconciliation.Gates.Causal)), ("$applicability", Kebab(reconciliation.Gates.Applicability)),
                    ("$dependency", Kebab(reconciliation.Gates.Dependency)), ("$producer", Kebab(reconciliation.Gates.Producer)),
                    ("$outcome", Kebab(reconciliation.Outcome)), ("$payload", payloadId),
                    ("$policy", reconciliation.PolicyVersion.ToString()), ("$now", reconciliation.AssessedAt.ToString()));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM reconciliation_assessments
                    WHERE reconciliation_assessment_id=$id AND subject_kind=$subject
                       AND predecessor_occurrence_id IS $prior AND successor_occurrence_id IS $current
                      AND causal_gate=$causal AND applicability_gate=$applicability
                      AND dependency_gate=$dependency AND producer_compatibility_gate=$producer
                      AND outcome=$outcome AND proof_payload_id=$payload AND policy_version=$policy;
                    """, "A reconciliation-assessment ID resolves to different retained semantics.", transaction,
                    ("$id", reconciliation.AssessmentId.Value), ("$subject", reconciliation.SubjectKind),
                    ("$prior", reconciliation.PriorOccurrenceId?.Value), ("$current", reconciliation.CurrentOccurrenceId?.Value),
                    ("$causal", Kebab(reconciliation.Gates.Causal)),
                    ("$applicability", Kebab(reconciliation.Gates.Applicability)),
                    ("$dependency", Kebab(reconciliation.Gates.Dependency)),
                    ("$producer", Kebab(reconciliation.Gates.Producer)), ("$outcome", Kebab(reconciliation.Outcome)),
                    ("$payload", payloadId), ("$policy", reconciliation.PolicyVersion.ToString()));
                string consideredPayload = AdmitJsonPayload(
                    reconciliation.ConsideredOccurrenceIds, "reconciliation-considered-occurrences",
                    reconciliation.AssessmentId.Value + "-considered", now, transaction);
                string? gapPayload = reconciliation.Gaps.Count == 0 ? null : AdmitJsonPayload(
                    reconciliation.Gaps, "reconciliation-gaps",
                    reconciliation.AssessmentId.Value + "-gaps", now, transaction);
                Execute(
                    """
                    INSERT OR IGNORE INTO reconciliation_details(
                        reconciliation_assessment_id,mechanism,causal_gate,applicability_gate,
                        dependency_gate,producer_gate,gap_payload_id,considered_occurrences_payload_id,
                        created_at)
                    VALUES ($id,$mechanism,$causal,$applicability,$dependency,$producer,$gaps,$considered,$now);
                    """, transaction,
                    ("$id", reconciliation.AssessmentId.Value), ("$causal", Kebab(reconciliation.Gates.Causal)),
                    ("$mechanism", reconciliation.Mechanism),
                    ("$applicability", Kebab(reconciliation.Gates.Applicability)),
                    ("$dependency", Kebab(reconciliation.Gates.Dependency)), ("$producer", Kebab(reconciliation.Gates.Producer)),
                    ("$gaps", gapPayload),
                    ("$considered", consideredPayload), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM reconciliation_details
                    WHERE reconciliation_assessment_id=$id AND mechanism=$mechanism
                      AND causal_gate=$causal AND applicability_gate=$applicability
                      AND dependency_gate=$dependency AND producer_gate=$producer
                      AND gap_payload_id IS $gaps AND considered_occurrences_payload_id=$considered;
                    """, "A reconciliation detail resolves to different retained semantics.", transaction,
                    ("$id", reconciliation.AssessmentId.Value), ("$mechanism", reconciliation.Mechanism),
                    ("$causal", Kebab(reconciliation.Gates.Causal)),
                    ("$applicability", Kebab(reconciliation.Gates.Applicability)),
                    ("$dependency", Kebab(reconciliation.Gates.Dependency)),
                    ("$producer", Kebab(reconciliation.Gates.Producer)),
                    ("$gaps", gapPayload), ("$considered", consideredPayload));
                Execute(
                    """
                    INSERT OR IGNORE INTO reconciliation_metadata(
                        reconciliation_assessment_id,actor_id,policy_id,policy_version,
                        proof_payload_id,visible_by_default,created_at)
                    VALUES ($id,$actor,$policy_id,$policy_version,$payload,$visible,$now);
                    """, transaction,
                    ("$id", reconciliation.AssessmentId.Value), ("$actor", reconciliation.ActorId.Value),
                    ("$policy_id", value.ReconciliationPolicyId.Value),
                    ("$policy_version", reconciliation.PolicyVersion.ToString()), ("$payload", payloadId),
                    ("$visible", reconciliation.VisibleByDefault ? 1 : 0),
                    ("$now", reconciliation.AssessedAt.ToString()));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM reconciliation_metadata
                    WHERE reconciliation_assessment_id=$id AND actor_id=$actor AND policy_id=$policy_id
                      AND policy_version=$policy_version AND proof_payload_id=$payload
                      AND visible_by_default=$visible;
                    """, "A reconciliation metadata row resolves to different retained semantics.", transaction,
                    ("$id", reconciliation.AssessmentId.Value), ("$actor", reconciliation.ActorId.Value),
                    ("$policy_id", value.ReconciliationPolicyId.Value),
                    ("$policy_version", reconciliation.PolicyVersion.ToString()), ("$payload", payloadId),
                    ("$visible", reconciliation.VisibleByDefault ? 1 : 0));
                foreach (OpaqueId evidenceId in reconciliation.ProofEvidenceIds)
                {
                    string linkId = CandidateAnalysisIdentity.StableId(
                        "reconciliation-proof", reconciliation.AssessmentId.Value, evidenceId.Value).Value;
                    Execute(
                        """
                        INSERT OR IGNORE INTO reconciliation_proof_links(
                            reconciliation_proof_link_id,reconciliation_assessment_id,evidence_id,
                            proof_payload_id,created_at)
                        VALUES ($id,$assessment,$evidence,$payload,$now);
                        """, transaction,
                        ("$id", linkId), ("$assessment", reconciliation.AssessmentId.Value),
                        ("$evidence", evidenceId.Value), ("$payload", payloadId), ("$now", ToText(now)));
                    RequireFindingCaseRow(
                        """
                        SELECT COUNT(*) FROM reconciliation_proof_links
                        WHERE reconciliation_proof_link_id=$id AND reconciliation_assessment_id=$assessment
                          AND evidence_id=$evidence AND proof_payload_id=$payload;
                        """, "A reconciliation-proof link resolves to different retained semantics.", transaction,
                        ("$id", linkId), ("$assessment", reconciliation.AssessmentId.Value),
                        ("$evidence", evidenceId.Value), ("$payload", payloadId));
                }
            }
            foreach (Slice5LineageContract lineage in value.LineageEvents)
            {
                Execute(
                    """
                    INSERT OR IGNORE INTO lineage_events(
                        lineage_event_id,subject_kind,event_kind,successor_logical_id,predecessor_occurrence_id,
                        successor_occurrence_id,reconciliation_assessment_id,created_at)
                    VALUES ($id,$subject,$kind,$successor_logical,$prior,$successor,$assessment,$now);
                    """, transaction,
                    ("$id", lineage.EventId.Value),
                    ("$subject", FindSubjectKind(lineage, value)), ("$kind", EventKind(lineage.Kind)),
                    ("$successor_logical", SuccessorLogicalId(lineage, value).Value),
                    ("$prior", lineage.PredecessorIds[0].Value), ("$successor", lineage.SuccessorIds[0].Value),
                    ("$assessment", lineage.ReconciliationAssessmentId?.Value), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM lineage_events
                    WHERE lineage_event_id=$id AND subject_kind=$subject AND event_kind=$kind
                      AND successor_logical_id=$successor_logical
                      AND predecessor_occurrence_id=$prior AND successor_occurrence_id=$successor
                      AND reconciliation_assessment_id IS $assessment;
                    """, "A lineage-event ID resolves to different retained semantics.", transaction,
                    ("$id", lineage.EventId.Value), ("$subject", FindSubjectKind(lineage, value)),
                    ("$kind", EventKind(lineage.Kind)), ("$prior", lineage.PredecessorIds[0].Value),
                    ("$successor_logical", SuccessorLogicalId(lineage, value).Value),
                    ("$successor", lineage.SuccessorIds[0].Value),
                    ("$assessment", lineage.ReconciliationAssessmentId?.Value));
                Execute(
                    """
                    INSERT OR IGNORE INTO lineage_details(
                        lineage_event_id,lineage_kind,proof_payload_id,created_at)
                    VALUES ($id,$kind,$payload,$now);
                    """, transaction,
                    ("$id", lineage.EventId.Value), ("$kind", Kebab(lineage.Kind)),
                    ("$payload", payloadId), ("$now", ToText(now)));
                RequireFindingCaseRow(
                    """
                    SELECT COUNT(*) FROM lineage_details
                    WHERE lineage_event_id=$id AND lineage_kind=$kind AND proof_payload_id=$payload;
                    """, "A lineage detail resolves to different retained semantics.", transaction,
                    ("$id", lineage.EventId.Value), ("$kind", Kebab(lineage.Kind)), ("$payload", payloadId));
                foreach ((string Side, OpaqueId OccurrenceId) edge in
                lineage.PredecessorIds.Select(item => ("predecessor", item))
                    .Concat(lineage.SuccessorIds.Select(item => ("successor", item))))
                {
                    string edgeId = CandidateAnalysisIdentity.StableId(
                        "lineage-event-edge", lineage.EventId.Value, edge.Side, edge.OccurrenceId.Value).Value;
                    Execute(
                        """
                        INSERT OR IGNORE INTO lineage_event_edges(
                            lineage_event_edge_id,lineage_event_id,edge_side,occurrence_id,
                            proof_payload_id,created_at)
                        VALUES ($id,$event,$side,$logical,$payload,$now);
                        """, transaction,
                        ("$id", edgeId), ("$event", lineage.EventId.Value), ("$side", edge.Side),
                        ("$logical", edge.OccurrenceId.Value), ("$payload", payloadId), ("$now", ToText(now)));
                    RequireFindingCaseRow(
                        """
                        SELECT COUNT(*) FROM lineage_event_edges
                        WHERE lineage_event_edge_id=$id AND lineage_event_id=$event
                          AND edge_side=$side AND occurrence_id=$logical AND proof_payload_id=$payload;
                        """, "A lineage-event edge resolves to different retained semantics.", transaction,
                        ("$id", edgeId), ("$event", lineage.EventId.Value), ("$side", edge.Side),
                        ("$logical", edge.OccurrenceId.Value), ("$payload", payloadId));
                }
            }
            transaction.Commit();
            return new FindingCasePersistenceReceipt(
                value.PayloadId.Value, payloadId, value.Findings.Count, value.Recommendations.Count,
                value.Cases.Count, value.ReconciliationAssessments.Count, value.LineageEvents.Count,
                value.TaxonomyAssignments.Count, value.Coverage.Count, value.Gaps.Count);
        }
    }

    public byte[] ReadFindingCasePayload(string payloadId) => ReadCandidateAnalysisPayload(payloadId);

    private string AdmitJsonPayload<T>(
        T value, string kind, string ownerId, DateTimeOffset now, SqliteTransaction transaction) =>
        AdmitCoordinatorPayload(
            JsonSerializer.SerializeToUtf8Bytes(value, ContractJsonSerializer.Options), kind, ownerId, now, transaction);

    private void InsertMembership(
        OpaqueId caseId, string memberKind, OpaqueId memberId, string role,
        string payloadId, DateTimeOffset now, SqliteTransaction transaction)
    {
        string membershipId = CandidateAnalysisIdentity.StableId(
            "case-membership", caseId.Value, memberKind, memberId.Value).Value;
        if (memberKind == "hypothesis")
        {
            Execute(
                """
                INSERT OR IGNORE INTO case_hypothesis_memberships(
                    case_hypothesis_membership_id,case_occurrence_id,hypothesis_id,
                    membership_role,cause_proof_payload_id,created_at)
                VALUES ($id,$case,$member,$role,$payload,$now);
                """, transaction,
                ("$id", membershipId), ("$case", caseId.Value), ("$member", memberId.Value),
                ("$role", role), ("$payload", payloadId), ("$now", ToText(now)));
            RequireFindingCaseRow(
                """
                SELECT COUNT(*) FROM case_hypothesis_memberships
                WHERE case_hypothesis_membership_id=$id AND case_occurrence_id=$case
                  AND hypothesis_id=$member AND membership_role=$role AND cause_proof_payload_id=$payload;
                """, "A case-hypothesis membership ID resolves to different retained semantics.", transaction,
                ("$id", membershipId), ("$case", caseId.Value), ("$member", memberId.Value),
                ("$role", role), ("$payload", payloadId));
            return;
        }
        Execute(
            """
            INSERT OR IGNORE INTO case_memberships(
                case_membership_id,case_occurrence_id,member_kind,member_id,
                membership_role,cause_proof_payload_id,created_at)
            VALUES ($id,$case,$kind,$member,$role,$payload,$now);
            """, transaction,
            ("$id", membershipId),
            ("$case", caseId.Value), ("$kind", memberKind), ("$member", memberId.Value),
            ("$role", role), ("$payload", payloadId), ("$now", ToText(now)));
        RequireFindingCaseRow(
            """
            SELECT COUNT(*) FROM case_memberships
            WHERE case_membership_id=$id AND case_occurrence_id=$case AND member_kind=$kind
              AND member_id=$member AND membership_role=$role AND cause_proof_payload_id=$payload;
            """, "A case-membership ID resolves to different retained semantics.", transaction,
            ("$id", membershipId), ("$case", caseId.Value), ("$kind", memberKind),
            ("$member", memberId.Value), ("$role", role), ("$payload", payloadId));
    }

    private void InsertCoverageLink(
        string kind,
        OpaqueId coverageId,
        OpaqueId targetId,
        CoverageFailureFactContract? failure,
        string payloadId,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        string linkId = CandidateAnalysisIdentity.StableId(
            "coverage-" + kind + "-link", coverageId.Value, targetId.Value).Value;
        if (kind == "taxonomy")
        {
            Execute(
                """
                INSERT OR IGNORE INTO analysis_coverage_taxonomy_links(
                    coverage_taxonomy_link_id,coverage_result_id,taxonomy_assignment_id,link_payload_id,created_at)
                VALUES ($id,$coverage,$target,$payload,$now);
                """, transaction, ("$id", linkId), ("$coverage", coverageId.Value),
                ("$target", targetId.Value), ("$payload", payloadId), ("$now", ToText(now)));
            RequireFindingCaseRow(
                """
                SELECT COUNT(*) FROM analysis_coverage_taxonomy_links
                WHERE coverage_taxonomy_link_id=$id AND coverage_result_id=$coverage
                  AND taxonomy_assignment_id=$target AND link_payload_id=$payload;
                """, "A coverage-taxonomy link resolves to different retained semantics.", transaction,
                ("$id", linkId), ("$coverage", coverageId.Value), ("$target", targetId.Value), ("$payload", payloadId));
            return;
        }
        if (kind == "gap")
        {
            Execute(
                """
                INSERT OR IGNORE INTO analysis_coverage_gap_links(
                    coverage_gap_link_id,coverage_result_id,gap_id,link_payload_id,created_at)
                VALUES ($id,$coverage,$target,$payload,$now);
                """, transaction, ("$id", linkId), ("$coverage", coverageId.Value),
                ("$target", targetId.Value), ("$payload", payloadId), ("$now", ToText(now)));
            RequireFindingCaseRow(
                """
                SELECT COUNT(*) FROM analysis_coverage_gap_links
                WHERE coverage_gap_link_id=$id AND coverage_result_id=$coverage
                  AND gap_id=$target AND link_payload_id=$payload;
                """, "A coverage-gap link resolves to different retained semantics.", transaction,
                ("$id", linkId), ("$coverage", coverageId.Value), ("$target", targetId.Value), ("$payload", payloadId));
            return;
        }
        if (kind != "failure" || failure is null)
        {
            throw new InvalidOperationException("Coverage link kind is not closed.");
        }
        Execute(
            """
            INSERT OR IGNORE INTO analysis_coverage_failure_links(
                coverage_failure_link_id,coverage_result_id,failure_id,failure_code,message,
                retryable,link_payload_id,created_at)
            VALUES ($id,$coverage,$target,$code,$message,$retryable,$payload,$now);
            """, transaction, ("$id", linkId), ("$coverage", coverageId.Value),
            ("$target", targetId.Value), ("$code", failure.FailureCode), ("$message", failure.Message),
            ("$retryable", failure.Retryable ? 1 : 0), ("$payload", payloadId), ("$now", ToText(now)));
        RequireFindingCaseRow(
            """
            SELECT COUNT(*) FROM analysis_coverage_failure_links
            WHERE coverage_failure_link_id=$id AND coverage_result_id=$coverage AND failure_id=$target
              AND failure_code=$code AND message=$message AND retryable=$retryable AND link_payload_id=$payload;
            """, "A coverage-failure link resolves to different retained semantics.", transaction,
            ("$id", linkId), ("$coverage", coverageId.Value), ("$target", targetId.Value),
            ("$code", failure.FailureCode), ("$message", failure.Message),
            ("$retryable", failure.Retryable ? 1 : 0), ("$payload", payloadId));
    }

    private void RequireFindingCaseRow(
        string sql,
        string message,
        SqliteTransaction transaction,
        params (string Name, object? Value)[] parameters)
    {
        if (ScalarLong(sql, transaction, parameters) != 1)
        {
            throw new InvalidDataException(message);
        }
    }

    private static string FindSubjectKind(Slice5LineageContract lineage, FindingCaseContract value)
    {
        if (lineage.ReconciliationAssessmentId is not null)
        {
            return value.ReconciliationAssessments.Single(item =>
                item.AssessmentId == lineage.ReconciliationAssessmentId).SubjectKind;
        }
        return "case";
    }

    private static OpaqueId SuccessorLogicalId(Slice5LineageContract lineage, FindingCaseContract value)
    {
        OpaqueId successor = lineage.SuccessorIds.OrderBy(item => item.Value, StringComparer.Ordinal).First();
        FindingContract? finding = value.Findings.SingleOrDefault(item => item.FindingOccurrenceId == successor);
        if (finding is not null)
        {
            return finding.LogicalFindingId;
        }
        return value.Cases.Single(item => item.CaseOccurrenceId == successor).LogicalCaseId;
    }

    private static string EventKind(LineageKind kind) => kind switch
    {
        LineageKind.Supersedes => "continuation",
        LineageKind.AnalyticalRevision => "revision",
        LineageKind.RelatedFollowUp => "follow-up",
        LineageKind.PromotesLead => "promotion",
        LineageKind.MergeSuccessor => "merge",
        LineageKind.SplitSuccessor => "split",
        LineageKind.CorrectionSuccessor => "correction",
        _ => throw new InvalidOperationException("Lineage kind is not closed."),
    };

    private static string Kebab<T>(T value) where T : struct, Enum =>
        System.Text.Json.JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
}

public sealed record FindingCasePersistenceReceipt(
    string AggregatePayloadId,
    string StoredPayloadId,
    int FindingCount,
    int RecommendationCount,
    int CaseCount,
    int ReconciliationCount,
    int LineageCount,
    int TaxonomyAssignmentCount,
    int CoverageCount,
    int GapCount);
