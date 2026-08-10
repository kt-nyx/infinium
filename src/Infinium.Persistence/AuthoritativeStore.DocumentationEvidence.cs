using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

#pragma warning disable IDE0008 // SQL transaction code is clearer with local type inference.
#pragma warning disable CA1512 // Guard clauses use parameter-specific messages.
#pragma warning disable CA1869 // The backup serializer is not a hot path.

namespace Infinium.Persistence;


public sealed partial class AuthoritativeStore
{
    internal void AdmitDocumentationApplicationTargets(
        IReadOnlyList<DocumentationApplicationTargetContract> targets,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(targets);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            foreach (DocumentationApplicationTargetContract target in targets)
            {
                EnsureRunExists(target.ConsumingRunId.Value, transaction);
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM runs
                    WHERE run_id = $run
                      AND installation_snapshot_id = $snapshot
                      AND analysis_context_id = $context
                      AND resolved_input_manifest_id = $manifest;
                    """,
                    "A documentation application target does not belong to the consuming run's immutable snapshot mapping.",
                    transaction,
                    ("$run", target.ConsumingRunId.Value),
                    ("$snapshot", target.InstallationSnapshotId.Value),
                    ("$context", target.AnalysisContextId.Value),
                    ("$manifest", target.ResolvedInputManifestId.Value));
                string bindingId = StableDocumentationId(
                    "docbinding",
                    target.ConsumingRunId.Value,
                    target.InstallationSnapshotId.Value,
                    target.AnalysisContextId.Value,
                    target.ResolvedInputManifestId.Value,
                    target.SubjectType,
                    target.SubjectId.Value,
                    target.DependencyClosureId.Value).Value;
                Execute(
                    """
                    INSERT OR IGNORE INTO documentation_application_bindings(
                        documentation_application_binding_id, run_id, installation_snapshot_id,
                        analysis_context_id, resolved_input_manifest_id, subject_id, subject_type,
                        dependency_closure_id, created_at)
                    VALUES ($binding, $run, $snapshot, $context, $manifest, $subject, $subject_type, $closure, $now);
                    """,
                    transaction,
                    ("$binding", bindingId),
                    ("$run", target.ConsumingRunId.Value),
                    ("$snapshot", target.InstallationSnapshotId.Value),
                    ("$context", target.AnalysisContextId.Value),
                    ("$manifest", target.ResolvedInputManifestId.Value),
                    ("$subject", target.SubjectId.Value),
                    ("$subject_type", target.SubjectType),
                    ("$closure", target.DependencyClosureId.Value),
                    ("$now", ToText(now)));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM documentation_application_bindings
                    WHERE documentation_application_binding_id = $binding
                      AND run_id = $run
                      AND installation_snapshot_id = $snapshot
                      AND analysis_context_id = $context
                      AND resolved_input_manifest_id = $manifest
                      AND subject_id = $subject
                      AND subject_type = $subject_type
                      AND dependency_closure_id = $closure;
                    """,
                    "A documentation application target identity resolves to different admitted mapping semantics.",
                    transaction,
                    ("$binding", bindingId),
                    ("$run", target.ConsumingRunId.Value),
                    ("$snapshot", target.InstallationSnapshotId.Value),
                    ("$context", target.AnalysisContextId.Value),
                    ("$manifest", target.ResolvedInputManifestId.Value),
                    ("$subject", target.SubjectId.Value),
                    ("$subject_type", target.SubjectType),
                    ("$closure", target.DependencyClosureId.Value));
            }
            transaction.Commit();
        }
    }

    internal DocumentationEvidenceContract PrepareDocumentationDeletionEvidence(
        DocumentationEvidenceContract evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        DocumentationEvidenceContractInvariants.Validate(evidence);
        if (evidence.DeletionReceipts.Count == 0)
        {
            return evidence;
        }

        lock (gate)
        {
            List<DocumentationDeletionReceiptContract> receipts = [];
            foreach (DocumentationDeletionReceiptContract receipt in evidence.DeletionReceipts)
            {
                Dictionary<string, HashSet<(string Kind, string Id)>> payloadOwners =
                    ReadDocumentationDeletionPayloadOwners(receipt);
                List<OpaqueId> independentlyRetained = [];
                foreach ((string targetPayloadId, HashSet<(string Kind, string Id)> owners) in payloadOwners)
                {
                    HashSet<(string Kind, string Id)> permitted = new()
                    {
                        ("documentation-revision", receipt.RevisionId.Value),
                    };
                    permitted.UnionWith(receipt.DeletedPassageIds.Select(item =>
                        ("documentation-passage", item.Value)));
                    if (owners.Any(owner => !permitted.Contains(owner)))
                    {
                        independentlyRetained.Add(DocumentationPayloadSemanticId(targetPayloadId, null));
                    }
                    else if (HasDocumentationBackupPin(targetPayloadId, null))
                    {
                        independentlyRetained.Add(DocumentationPayloadSemanticId(targetPayloadId, null));
                    }
                }
                OpaqueId[] retainedIds = independentlyRetained
                    .Distinct()
                    .OrderBy(item => item.Value, StringComparer.Ordinal)
                    .ToArray();
                OpaqueId receiptId = StableDocumentationId(
                    "docdelete",
                    receipt.OriginatingRunId.Value,
                    receipt.RevisionId.Value,
                    receipt.DeletedBodyFingerprint.Value,
                    CanonicalDocumentation(receipt.DeletedPassageIds.Select(item => item.Value)),
                    CanonicalDocumentation(retainedIds.Select(item => item.Value)),
                    receipt.DeletedAt.ToString(),
                    receipt.Reason);
                receipts.Add(receipt with
                {
                    ReceiptId = receiptId,
                    IndependentlyRetainedPayloadIds = retainedIds,
                });
            }
            receipts = receipts.OrderBy(item => item.ReceiptId.Value, StringComparer.Ordinal).ToList();
            DocumentationEvidenceContract prepared = evidence with
            {
                PayloadId = new OpaqueId("docevidence-pending"),
                DeletionReceipts = receipts,
            };
            prepared = prepared with
            {
                PayloadId = DocumentationEvidenceIdentity.ComputePayloadId(prepared),
            };
            DocumentationEvidenceContractInvariants.Validate(prepared);
            return prepared;
        }
    }

    internal DocumentationEvidencePersistenceReceipt PublishDocumentationEvidence(
        DocumentationEvidenceContract evidence,
        ReadOnlyMemory<byte>? sourceBytes,
        ReadOnlyMemory<byte> serializedEvidence,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        DocumentationEvidenceContractInvariants.Validate(evidence);
        byte[] canonicalEvidence = JsonSerializer.SerializeToUtf8Bytes(evidence, DocumentationPayloadJsonOptions);
        if (!serializedEvidence.Span.SequenceEqual(canonicalEvidence))
        {
            throw new InvalidDataException(
                "Serialized documentation evidence must be the canonical encoding of the published contract object.");
        }
        if (evidence.Revisions.Count != 1 || evidence.Imports.Count != 1)
        {
            throw new InvalidOperationException("Documentation publication accepts one revision/import transaction at a time.");
        }

        DocumentationRevisionContract revision = evidence.Revisions[0];
        DocumentationImportContract import = evidence.Imports[0];
        bool cleanImport = import.Mode == DocumentationImportMode.CleanImport;
        if ((cleanImport && revision.RetentionState == AnalysisResultState.Present) != sourceBytes.HasValue)
        {
            throw new InvalidOperationException("Retained source bytes must match the revision retention state.");
        }
        if (sourceBytes is { } bytes
            && (bytes.Length != revision.ByteLength
                || !StringComparer.Ordinal.Equals(Hash(bytes.Span), revision.ByteFingerprint.Value)))
        {
            throw new InvalidDataException("Published source bytes do not match the admitted revision identity.");
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureRunExists(evidence.OriginatingRunId.Value, transaction);
            foreach (ClaimApplicationContract application in evidence.Applications)
            {
                EnsureRunExists(application.ConsumingRunId.Value, transaction);
                string admittedContext = ScalarString(
                    "SELECT analysis_context_id FROM runs WHERE run_id = $run;",
                    transaction,
                    ("$run", application.ConsumingRunId.Value));
                if (!StringComparer.Ordinal.Equals(admittedContext, application.AnalysisContextId.Value))
                {
                    throw new InvalidDataException(
                        "A documentation application context does not match its consuming run binding.");
                }
            }

            string evidencePayloadId = AdmitCoordinatorPayload(
                serializedEvidence.Span,
                "documentation-evidence",
                evidence.PayloadId.Value,
                now,
                transaction);
            string? bodyPayloadId = sourceBytes is null
                ? ScalarStringOrNull(
                    "SELECT body_payload_id FROM documentation_revisions WHERE documentation_revision_id = $revision;",
                    transaction,
                    ("$revision", revision.RevisionId.Value))
                : AdmitCoordinatorPayload(
                    sourceBytes.Value.Span,
                    "documentation-revision",
                    revision.RevisionId.Value,
                    now,
                    transaction);
            if (!cleanImport && bodyPayloadId is null)
            {
                throw new InvalidOperationException("Retained reuse requires the original retained source payload.");
            }

            Execute(
                """
                INSERT OR IGNORE INTO documentation_revisions(
                    documentation_revision_id, source_id, source_kind, source_revision,
                    supplying_snapshot_id, body_payload_id, content_sha256, byte_length,
                    availability_state, retention_state, replay_state, created_at)
                VALUES (
                    $revision, $source, $source_kind, $source_revision,
                    $snapshot, $body, $sha, $length, $availability, $retention, $replay, $now);
                """,
                transaction,
                ("$revision", revision.RevisionId.Value),
                ("$source", revision.SourceId.Value),
                ("$source_kind", SourceKindToken(revision.SourceKind)),
                ("$source_revision", revision.SourceRevision),
                ("$snapshot", revision.SupplyingSnapshotId?.Value),
                ("$body", bodyPayloadId),
                ("$sha", revision.ByteFingerprint.Value),
                ("$length", revision.ByteLength),
                ("$availability", ResultStateToken(revision.RetentionState)),
                ("$retention", ResultStateToken(revision.RetentionState)),
                ("$replay", ReplayStateToken(revision.ReplayState)),
                ("$now", ToText(now)));
            RequireSingleDocumentationRow(
                """
                SELECT COUNT(*) FROM documentation_revisions
                WHERE documentation_revision_id = $revision
                  AND source_id = $source
                  AND source_kind = $source_kind
                  AND source_revision = $source_revision
                  AND supplying_snapshot_id IS $snapshot
                  AND body_payload_id IS $body
                  AND content_sha256 = $sha
                  AND byte_length = $length
                  AND availability_state = $availability
                  AND retention_state = $retention
                  AND replay_state = $replay;
                """,
                "A retained documentation revision ID resolves to different source identity or state.",
                transaction,
                ("$revision", revision.RevisionId.Value),
                ("$source", revision.SourceId.Value),
                ("$source_kind", SourceKindToken(revision.SourceKind)),
                ("$source_revision", revision.SourceRevision),
                ("$snapshot", revision.SupplyingSnapshotId?.Value),
                ("$body", bodyPayloadId),
                ("$sha", revision.ByteFingerprint.Value),
                ("$length", revision.ByteLength),
                ("$availability", ResultStateToken(revision.RetentionState)),
                ("$retention", ResultStateToken(revision.RetentionState)),
                ("$replay", ReplayStateToken(revision.ReplayState)));
            Execute(
                """
                INSERT INTO documentation_imports(
                    documentation_import_id, import_run_id, documentation_revision_id,
                    import_mode, reused_import_id, dependency_closure_id, extractor_id,
                    llm_involvement, llm_operation, boundaries_payload_id,
                    import_payload_id, created_at)
                VALUES (
                    $import, $import_run, $revision, $mode, $reused_import, $closure, $extractor,
                    'none', 'none', $payload, $payload, $now);
                """,
                transaction,
                ("$import", import.ImportId.Value),
                ("$import_run", import.ImportRunId.Value),
                ("$revision", revision.RevisionId.Value),
                ("$mode", ImportModeToken(import.Mode)),
                ("$reused_import", import.ReusedImportId?.Value),
                ("$closure", import.DependencyClosureId.Value),
                ("$extractor", import.ExtractorId.Value),
                ("$payload", evidencePayloadId),
                ("$now", import.CreatedAt.ToString()));

            Dictionary<OpaqueId, string> passagePayloadIds = [];
            foreach (DocumentationPassageContract passage in evidence.Passages)
            {
                string passagePayloadId;
                if (sourceBytes is { } retainedSource)
                {
                    if (passage.Utf8EndOffset > retainedSource.Length)
                    {
                        throw new InvalidDataException("A documentation passage lies outside the retained source bytes.");
                    }
                    ReadOnlyMemory<byte> passageBytes = retainedSource[
                        checked((int)passage.Utf8StartOffset)..checked((int)passage.Utf8EndOffset)];
                    if (!StringComparer.Ordinal.Equals(Hash(passageBytes.Span), passage.PassageFingerprint.Value))
                    {
                        throw new InvalidDataException("A documentation passage fingerprint does not match its exact UTF-8 byte slice.");
                    }
                    passagePayloadId = AdmitCoordinatorPayload(
                        passageBytes.Span,
                        "documentation-passage",
                        passage.PassageId.Value,
                        now,
                        transaction);
                }
                else
                {
                    passagePayloadId = ScalarString(
                        "SELECT passage_payload_id FROM documentation_passages WHERE documentation_passage_id = $passage;",
                        transaction,
                        ("$passage", passage.PassageId.Value));
                }
                passagePayloadIds.Add(passage.PassageId, passagePayloadId);
                Execute(
                    """
                    INSERT OR IGNORE INTO documentation_passages(
                        documentation_passage_id, documentation_revision_id,
                        utf8_byte_start, utf8_byte_end, passage_sha256,
                        passage_payload_id, availability_state, created_at)
                    VALUES ($passage, $revision, $start, $end, $sha, $payload, 'present', $now);
                    """,
                    transaction,
                    ("$passage", passage.PassageId.Value),
                    ("$revision", passage.RevisionId.Value),
                    ("$start", passage.Utf8StartOffset),
                    ("$end", passage.Utf8EndOffset),
                    ("$sha", passage.PassageFingerprint.Value),
                    ("$payload", passagePayloadId),
                    ("$now", ToText(now)));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM documentation_passages
                    WHERE documentation_passage_id = $passage
                      AND documentation_revision_id = $revision
                      AND utf8_byte_start = $start
                      AND utf8_byte_end = $end
                      AND passage_sha256 = $sha
                      AND passage_payload_id = $payload
                      AND availability_state = 'present';
                    """,
                    "A retained documentation passage ID resolves to different bytes or range.",
                    transaction,
                    ("$passage", passage.PassageId.Value),
                    ("$revision", passage.RevisionId.Value),
                    ("$start", passage.Utf8StartOffset),
                    ("$end", passage.Utf8EndOffset),
                    ("$sha", passage.PassageFingerprint.Value),
                    ("$payload", passagePayloadId));
            }

            foreach (DocumentationClaimContract claim in evidence.Claims)
            {
                if (cleanImport)
                {
                    Execute(
                    """
                    INSERT OR IGNORE INTO evidence_revisions(
                        evidence_revision_id, documentation_passage_id, import_id,
                        payload_schema_id, payload_schema_version, evidence_kind,
                        claim_kind, authority_kind, applicability_state,
                        classification_role, evidence_state, evidence_payload_id,
                        contradiction_payload_id, created_at)
                    VALUES (
                        $claim, $passage, $import, $schema, '1.0.0', 'documentation-claim',
                        $kind, $authority, $applicability, $role, 'admitted', $payload,
                        $contradiction, $now);
                    """,
                    transaction,
                    ("$claim", claim.ClaimId.Value),
                    ("$passage", claim.PassageId.Value),
                    ("$import", claim.ProducingImportId.Value),
                    ("$schema", ContractConstants.DocumentationEvidenceSchemaId),
                    ("$kind", ClaimKindToken(claim.Kind)),
                    ("$authority", EvidenceAuthorityToken(claim.Authority)),
                    ("$applicability", ApplicabilityToken(claim.Applicability)),
                    ("$role", ClassificationRoleToken(claim.ClassificationRole)),
                    ("$payload", evidencePayloadId),
                    ("$contradiction", claim.ContradictingEvidenceIds.Count == 0 ? null : evidencePayloadId),
                        ("$now", ToText(now)));
                }
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM evidence_revisions
                    WHERE evidence_revision_id = $claim
                      AND documentation_passage_id = $passage
                      AND import_id = $import
                      AND payload_schema_id = $schema
                      AND payload_schema_version = '1.0.0'
                      AND evidence_kind = 'documentation-claim'
                      AND claim_kind = $kind
                      AND authority_kind = $authority
                      AND applicability_state = $applicability
                      AND classification_role = $role
                      AND evidence_state = 'admitted'
                      AND (($has_contradiction = 0 AND contradiction_payload_id IS NULL)
                           OR ($has_contradiction = 1 AND contradiction_payload_id IS NOT NULL));
                    """,
                    "A retained documentation claim ID resolves to different claim semantics.",
                    transaction,
                    ("$claim", claim.ClaimId.Value),
                    ("$passage", claim.PassageId.Value),
                    ("$import", claim.ProducingImportId.Value),
                    ("$schema", ContractConstants.DocumentationEvidenceSchemaId),
                    ("$kind", ClaimKindToken(claim.Kind)),
                    ("$authority", EvidenceAuthorityToken(claim.Authority)),
                    ("$applicability", ApplicabilityToken(claim.Applicability)),
                    ("$role", ClassificationRoleToken(claim.ClassificationRole)),
                    ("$has_contradiction", claim.ContradictingEvidenceIds.Count == 0 ? 0 : 1));
            }

            foreach (ClaimApplicationContract application in evidence.Applications)
            {
                string bindingId = ScalarString(
                    """
                    SELECT documentation_application_binding_id
                    FROM documentation_application_bindings
                    WHERE run_id = $run
                      AND analysis_context_id = $context
                      AND subject_id = $subject
                      AND subject_type = $subject_type
                      AND dependency_closure_id = $closure;
                    """,
                    transaction,
                    ("$run", application.ConsumingRunId.Value),
                    ("$context", application.AnalysisContextId.Value),
                    ("$subject", application.SubjectId.Value),
                    ("$subject_type", application.SubjectType),
                    ("$closure", application.DependencyClosureId.Value));
                Execute(
                    """
                    INSERT OR IGNORE INTO evidence_application_links(
                        evidence_application_link_id, evidence_revision_id, run_id,
                        application_binding_id, analysis_context_id, subject_id, subject_type,
                        dependency_closure_id, application_state,
                        application_payload_id, created_at)
                    VALUES ($application, $claim, $run, $binding, $context, $subject, $subject_type, $closure, $state, $payload, $now);
                    """,
                    transaction,
                    ("$application", application.ApplicationId.Value),
                    ("$claim", application.ClaimId.Value),
                    ("$run", application.ConsumingRunId.Value),
                    ("$binding", bindingId),
                    ("$context", application.AnalysisContextId.Value),
                    ("$subject", application.SubjectId.Value),
                    ("$subject_type", application.SubjectType),
                    ("$closure", application.DependencyClosureId.Value),
                    ("$state", ApplicabilityToken(application.Applicability)),
                    ("$payload", evidencePayloadId),
                    ("$now", ToText(now)));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM evidence_application_links
                    WHERE evidence_application_link_id = $application
                      AND evidence_revision_id = $claim
                      AND run_id = $run
                      AND application_binding_id = $binding
                      AND analysis_context_id = $context
                      AND subject_id = $subject
                      AND subject_type = $subject_type
                      AND dependency_closure_id = $closure
                      AND application_state = $state;
                    """,
                    "A retained claim-application ID resolves to different application semantics.",
                    transaction,
                    ("$application", application.ApplicationId.Value),
                    ("$claim", application.ClaimId.Value),
                    ("$run", application.ConsumingRunId.Value),
                    ("$binding", bindingId),
                    ("$context", application.AnalysisContextId.Value),
                    ("$subject", application.SubjectId.Value),
                    ("$subject_type", application.SubjectType),
                    ("$closure", application.DependencyClosureId.Value),
                    ("$state", ApplicabilityToken(application.Applicability)));
            }

            foreach (DocumentationPurposeAssignmentContract assignment in evidence.PurposeAssignments)
            {
                ClaimApplicationContract application = evidence.Applications.Single(item =>
                    item.SubjectId == assignment.SubjectId
                    && StringComparer.Ordinal.Equals(item.SubjectType, assignment.SubjectType)
                    && item.ClaimId == assignment.ClaimId
                    && item.ApplicationId == assignment.ApplicationId);
                Execute(
                    """
                    INSERT OR IGNORE INTO taxonomy_assignments(
                        taxonomy_assignment_id, run_id, subject_kind, subject_id,
                        taxonomy_id, taxonomy_version, axis, facet, taxonomy_code,
                        applicability_state, classification_role, assignment_payload_id, created_at)
                    VALUES (
                        $assignment, $run, $subject_kind, $subject, $taxonomy, $version,
                        $axis, $facet, $code, 'assigned', 'declared', $payload, $now);
                    """,
                    transaction,
                    ("$assignment", assignment.AssignmentId.Value),
                    ("$run", application.ConsumingRunId.Value),
                    ("$subject_kind", assignment.SubjectType),
                    ("$subject", assignment.SubjectId.Value),
                    ("$taxonomy", assignment.TaxonomyId),
                    ("$version", assignment.TaxonomyVersion.ToString()),
                    ("$axis", assignment.Axis),
                    ("$facet", assignment.Facet),
                    ("$code", assignment.Code),
                    ("$payload", evidencePayloadId),
                    ("$now", assignment.CreatedAt.ToString()));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM taxonomy_assignments
                    WHERE taxonomy_assignment_id = $assignment
                      AND run_id = $run
                      AND subject_kind = $subject_kind
                      AND subject_id = $subject
                      AND taxonomy_id = $taxonomy
                      AND taxonomy_version = $version
                      AND axis = $axis
                      AND facet = $facet
                      AND taxonomy_code = $code
                      AND applicability_state = 'assigned'
                      AND classification_role = 'declared';
                    """,
                    "A retained purpose-assignment ID resolves to different taxonomy semantics.",
                    transaction,
                    ("$assignment", assignment.AssignmentId.Value),
                    ("$run", application.ConsumingRunId.Value),
                    ("$subject_kind", assignment.SubjectType),
                    ("$subject", assignment.SubjectId.Value),
                    ("$taxonomy", assignment.TaxonomyId),
                    ("$version", assignment.TaxonomyVersion.ToString()),
                    ("$axis", assignment.Axis),
                    ("$facet", assignment.Facet),
                    ("$code", assignment.Code));
                string conditionIdsJson = JsonSerializer.Serialize(
                    assignment.ApplicabilityConditionIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO documentation_purpose_assignment_details(
                        taxonomy_assignment_id, evidence_revision_id, evidence_application_link_id,
                        analyzer_or_adjudicator_id, applicability_condition_ids_json, reason, detail_payload_id)
                    VALUES ($assignment, $claim, $application, $analyzer, $conditions, $reason, $payload);
                    """,
                    transaction,
                    ("$assignment", assignment.AssignmentId.Value),
                    ("$claim", assignment.ClaimId.Value),
                    ("$application", assignment.ApplicationId.Value),
                    ("$analyzer", assignment.AnalyzerOrAdjudicatorId.Value),
                    ("$conditions", conditionIdsJson),
                    ("$reason", assignment.Reason),
                    ("$payload", evidencePayloadId));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM documentation_purpose_assignment_details
                    WHERE taxonomy_assignment_id = $assignment
                      AND evidence_revision_id = $claim
                      AND evidence_application_link_id = $application
                      AND analyzer_or_adjudicator_id = $analyzer
                      AND applicability_condition_ids_json = $conditions
                      AND reason = $reason;
                    """,
                    "A documentation purpose assignment resolves to different evidence or derivation semantics.",
                    transaction,
                    ("$assignment", assignment.AssignmentId.Value),
                    ("$claim", assignment.ClaimId.Value),
                    ("$application", assignment.ApplicationId.Value),
                    ("$analyzer", assignment.AnalyzerOrAdjudicatorId.Value),
                    ("$conditions", conditionIdsJson),
                    ("$reason", assignment.Reason));
            }

            foreach (DocumentationGapContract gap in evidence.Gaps)
            {
                Execute(
                    """
                    INSERT OR IGNORE INTO analysis_gaps(
                        gap_id, run_id, population_id, stage_id, gap_state,
                        replay_effect, conclusion_effect, gap_payload_id, created_at)
                    VALUES (
                        $gap, $run, 'documentation-evidence', 'documentation-import',
                        $state, $replay, $conclusion, $payload, $now);
                    """,
                    transaction,
                    ("$gap", gap.GapId.Value),
                    ("$run", evidence.OriginatingRunId.Value),
                    ("$state", gap.Kind switch
                    {
                        DocumentationGapKind.Deletion => "deleted",
                        DocumentationGapKind.UnavailableSource => "unavailable",
                        DocumentationGapKind.Replay => "audit-gap",
                        _ => "missing-information",
                    }),
                    ("$replay", ReplayEffectToken(gap.ReplayEffect)),
                    ("$conclusion", gap.Kind == DocumentationGapKind.Contradiction
                        || gap.ReplayEffect == ReplayState.Partial ? "bounded" : "unavailable"),
                    ("$payload", evidencePayloadId),
                    ("$now", ToText(now)));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM analysis_gaps
                    WHERE gap_id = $gap
                      AND run_id = $run
                      AND population_id = 'documentation-evidence'
                      AND stage_id = 'documentation-import'
                      AND gap_state = $state
                      AND replay_effect = $replay
                      AND conclusion_effect = $conclusion
                      AND created_at = $created;
                    """,
                    "A documentation gap ID resolves to different provenance or replay semantics.",
                    transaction,
                    ("$gap", gap.GapId.Value),
                    ("$run", gap.OriginatingRunId.Value),
                    ("$state", gap.Kind switch
                    {
                        DocumentationGapKind.Deletion => "deleted",
                        DocumentationGapKind.UnavailableSource => "unavailable",
                        DocumentationGapKind.Replay => "audit-gap",
                        _ => "missing-information",
                    }),
                    ("$replay", ReplayEffectToken(gap.ReplayEffect)),
                    ("$conclusion", gap.Kind == DocumentationGapKind.Contradiction
                        || gap.ReplayEffect == ReplayState.Partial ? "bounded" : "unavailable"),
                    ("$created", gap.CreatedAt.ToString()));
                Execute(
                    """
                    INSERT OR IGNORE INTO documentation_gap_details(
                        gap_id, documentation_revision_id, evidence_revision_id,
                        evidence_application_link_id, gap_kind, reason, detail_payload_id)
                    VALUES ($gap, $revision, $claim, $application, $kind, $reason, $payload);
                    """,
                    transaction,
                    ("$gap", gap.GapId.Value),
                    ("$revision", gap.RevisionId.Value),
                    ("$claim", gap.ClaimId?.Value),
                    ("$application", gap.ApplicationId?.Value),
                    ("$kind", DocumentationGapKindToken(gap.Kind)),
                    ("$reason", gap.Reason),
                    ("$payload", evidencePayloadId));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM documentation_gap_details
                    WHERE gap_id = $gap
                      AND documentation_revision_id = $revision
                      AND evidence_revision_id IS $claim
                      AND evidence_application_link_id IS $application
                      AND gap_kind = $kind
                      AND reason = $reason;
                    """,
                    "A documentation gap ID resolves to different exact gap semantics.",
                    transaction,
                    ("$gap", gap.GapId.Value),
                    ("$revision", gap.RevisionId.Value),
                    ("$claim", gap.ClaimId?.Value),
                    ("$application", gap.ApplicationId?.Value),
                    ("$kind", DocumentationGapKindToken(gap.Kind)),
                    ("$reason", gap.Reason));
            }

            List<string> deletedObjectPaths = [];
            foreach (DocumentationDeletionReceiptContract receipt in evidence.DeletionReceipts)
            {
                string passageIdsJson = JsonSerializer.Serialize(
                    receipt.DeletedPassageIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                string retainedIdsJson = JsonSerializer.Serialize(
                    receipt.IndependentlyRetainedPayloadIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO documentation_deletion_receipts(
                        documentation_deletion_receipt_id, run_id, documentation_revision_id,
                        deleted_body_sha256, deleted_passage_ids_json,
                        independently_retained_payload_ids_json, replay_effect,
                        receipt_payload_id, reason, deleted_at)
                    VALUES ($receipt, $run, $revision, $sha, $passages, $retained, $replay, $payload, $reason, $deleted);
                    """,
                    transaction,
                    ("$receipt", receipt.ReceiptId.Value),
                    ("$run", receipt.OriginatingRunId.Value),
                    ("$revision", receipt.RevisionId.Value),
                    ("$sha", receipt.DeletedBodyFingerprint.Value),
                    ("$passages", passageIdsJson),
                    ("$retained", retainedIdsJson),
                    ("$replay", ReplayEffectToken(receipt.ReplayEffect)),
                    ("$payload", evidencePayloadId),
                    ("$reason", receipt.Reason),
                    ("$deleted", receipt.DeletedAt.ToString()));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM documentation_deletion_receipts
                    WHERE documentation_deletion_receipt_id = $receipt
                      AND run_id = $run
                      AND documentation_revision_id = $revision
                      AND deleted_body_sha256 = $sha
                      AND deleted_passage_ids_json = $passages
                      AND independently_retained_payload_ids_json = $retained
                      AND replay_effect = $replay
                      AND reason = $reason
                      AND deleted_at = $deleted;
                    """,
                    "A documentation deletion receipt ID resolves to different deletion semantics.",
                    transaction,
                    ("$receipt", receipt.ReceiptId.Value),
                    ("$run", receipt.OriginatingRunId.Value),
                    ("$revision", receipt.RevisionId.Value),
                    ("$sha", receipt.DeletedBodyFingerprint.Value),
                    ("$passages", passageIdsJson),
                    ("$retained", retainedIdsJson),
                    ("$replay", ReplayEffectToken(receipt.ReplayEffect)),
                    ("$reason", receipt.Reason),
                    ("$deleted", receipt.DeletedAt.ToString()));

                HashSet<string> deletionPayloadIds = passagePayloadIds
                    .Where(item => receipt.DeletedPassageIds.Contains(item.Key))
                    .Select(item => item.Value)
                    .ToHashSet(StringComparer.Ordinal);
                if (bodyPayloadId is not null)
                {
                    deletionPayloadIds.Add(bodyPayloadId);
                }
                deletionPayloadIds.Remove(evidencePayloadId);
                HashSet<string> expectedRetainedIdentities = deletionPayloadIds
                    .Where(payloadId =>
                        HasExternalDocumentationDeletionOwner(
                            payloadId,
                            receipt.RevisionId.Value,
                            receipt.DeletedPassageIds.Select(item => item.Value),
                            transaction)
                        || HasDocumentationBackupPin(payloadId, transaction))
                    .Select(payloadId => DocumentationPayloadSemanticId(payloadId, transaction).Value)
                    .ToHashSet(StringComparer.Ordinal);
                if (!expectedRetainedIdentities.SetEquals(
                        receipt.IndependentlyRetainedPayloadIds.Select(item => item.Value)))
                {
                    throw new InvalidDataException(
                        "A documentation deletion receipt does not exactly identify independently retained payloads.");
                }
                foreach (string deletionPayloadId in deletionPayloadIds)
                {
                    if (HasExternalDocumentationDeletionOwner(
                            deletionPayloadId,
                            receipt.RevisionId.Value,
                            receipt.DeletedPassageIds.Select(item => item.Value),
                            transaction))
                    {
                        continue;
                    }
                    RequireDeletionPayloadOwners(
                        deletionPayloadId,
                        receipt.RevisionId.Value,
                        receipt.DeletedPassageIds.Select(item => item.Value),
                        transaction);
                    string objectPath = ScalarString(
                        "SELECT object_relative_path FROM payloads WHERE payload_id = $payload AND retention_state = 'retained';",
                        transaction,
                        ("$payload", deletionPayloadId));
                    Execute(
                        "UPDATE payloads SET retention_state = 'deleted' WHERE payload_id = $payload AND retention_state = 'retained';",
                        transaction,
                        ("$payload", deletionPayloadId));
                    deletedObjectPaths.Add(objectPath);
                }
            }

            InsertDocumentationDependencyEdges(evidence, evidencePayloadId, now, transaction);
            RequireExactDocumentationDependencySets(evidence, transaction);
            transaction.Commit();
            foreach (string objectPath in deletedObjectPaths.Distinct(StringComparer.Ordinal))
            {
                Paths.DeleteFile(
                    ProductWriteClass.Payload,
                    objectPath["payloads/".Length..].Replace('/', Path.DirectorySeparatorChar),
                    missingIsSuccess: true);
            }
            return new DocumentationEvidencePersistenceReceipt(
                evidence.PayloadId.Value,
                evidencePayloadId,
                revision.RevisionId.Value,
                import.ImportId.Value,
                evidence.Claims.Count,
                evidence.Applications.Count,
                evidence.PurposeAssignments.Count,
                evidence.DeletionReceipts.Count,
                evidence.Gaps.Count);
        }
    }

    public byte[] ReadDocumentationEvidencePayload(string payloadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT object_relative_path, content_sha256, byte_length FROM payloads WHERE payload_id = $payload;";
            command.Parameters.AddWithValue("$payload", payloadId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"Payload '{payloadId}' does not exist.");
            }

            string relativePath = reader.GetString(0);
            string expectedSha = reader.GetString(1);
            long expectedLength = reader.GetInt64(2);
            using FileStream stream = Paths.OpenReadFile(
                ProductWriteClass.Payload,
                relativePath["payloads/".Length..].Replace('/', Path.DirectorySeparatorChar));
            if (expectedLength > int.MaxValue)
            {
                throw new InvalidDataException("Documentation evidence payload exceeds the readback bound.");
            }
            byte[] bytes = new byte[checked((int)expectedLength)];
            stream.ReadExactly(bytes);
            if (!StringComparer.Ordinal.Equals(Hash(bytes), expectedSha))
            {
                throw new InvalidDataException("Documentation evidence payload failed readback identity validation.");
            }
            return bytes;
        }
    }

    private void RequireSingleDocumentationRow(
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

    private string AdmitCoordinatorPayload(
        ReadOnlySpan<byte> bytes,
        string ownerKind,
        string ownerId,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        if (bytes.Length > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("Documentation payload exceeds the 64 MiB coordinator admission bound.");
        }
        string sha = Hash(bytes);
        string classRelativePath = Path.Combine(sha[..2], sha[2..4], sha);
        string? existingRetentionState = ScalarStringOrNull(
            "SELECT retention_state FROM payloads WHERE content_sha256 = $sha;",
            transaction,
            ("$sha", sha));
        if (existingRetentionState is not null
            && !StringComparer.Ordinal.Equals(existingRetentionState, "retained"))
        {
            throw new InvalidOperationException(
                "A deleted content-addressed payload cannot be silently resurrected by publication.");
        }
        if (Paths.FileExists(ProductWriteClass.Payload, classRelativePath))
        {
            using FileStream existing = Paths.OpenReadFile(ProductWriteClass.Payload, classRelativePath);
            if (existing.Length != bytes.Length || !StringComparer.Ordinal.Equals(HashStream(existing), sha))
            {
                throw new InvalidDataException("An existing content-addressed payload does not match its path identity.");
            }
        }
        else
        {
            Paths.WriteAllBytesAtomic(ProductWriteClass.Payload, classRelativePath, bytes);
        }

        string proposedPayloadId = Guid.NewGuid().ToString("N");
        string objectRelativePath = "payloads/" + classRelativePath.Replace('\\', '/');
        Execute(
            """
            INSERT INTO payloads(
                payload_id, content_sha256, byte_length, codec, retention_state,
                object_relative_path, admitted_at)
            VALUES ($payload, $sha, $length, 'identity', 'retained', $path, $now)
            ON CONFLICT(content_sha256) DO NOTHING;
            """,
            transaction,
            ("$payload", proposedPayloadId),
            ("$sha", sha),
            ("$length", bytes.Length),
            ("$path", objectRelativePath),
            ("$now", ToText(now)));
        string payloadId = ScalarString(
            "SELECT payload_id FROM payloads WHERE content_sha256 = $sha AND byte_length = $length AND object_relative_path = $path;",
            transaction,
            ("$sha", sha),
            ("$length", bytes.Length),
            ("$path", objectRelativePath));
        Execute(
            "INSERT OR IGNORE INTO payload_owners(payload_id, owner_kind, owner_id) VALUES ($payload, $kind, $owner);",
            transaction,
            ("$payload", payloadId),
            ("$kind", ownerKind),
            ("$owner", ownerId));
        return payloadId;
    }

    private void InsertDocumentationDependencyEdges(
        DocumentationEvidenceContract evidence,
        string payloadId,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        DocumentationRevisionContract revision = evidence.Revisions.Single();
        InsertDocumentationEdge(
            evidence.OriginatingRunId.Value,
            "documentation-import",
            evidence.Imports.Single().ImportId.Value,
            "documentation-revision",
            revision.RevisionId.Value,
            "consumes",
            payloadId,
            now,
            transaction);
        if (evidence.Imports.Single().ReusedImportId is { } reusedImportId)
        {
            InsertDocumentationEdge(
                evidence.OriginatingRunId.Value,
                "documentation-import",
                evidence.Imports.Single().ImportId.Value,
                "documentation-import",
                reusedImportId.Value,
                "reuses",
                payloadId,
                now,
                transaction);
        }
        if (revision.SupplyingSnapshotId is { } supplyingSnapshotId)
        {
            InsertDocumentationEdge(
                evidence.OriginatingRunId.Value,
                "documentation-revision",
                revision.RevisionId.Value,
                "snapshot",
                supplyingSnapshotId.Value,
                "depends-on",
                payloadId,
                now,
                transaction);
        }
        foreach (DocumentationPassageContract passage in evidence.Passages)
        {
            InsertDocumentationEdge(
                evidence.OriginatingRunId.Value,
                "passage",
                passage.PassageId.Value,
                "documentation-revision",
                passage.RevisionId.Value,
                "derived-from",
                payloadId,
                now,
                transaction);
        }
        foreach (DocumentationClaimContract claim in evidence.Claims)
        {
            InsertDocumentationEdge(
                evidence.OriginatingRunId.Value,
                "evidence-revision",
                claim.ClaimId.Value,
                "passage",
                claim.PassageId.Value,
                "derived-from",
                payloadId,
                now,
                transaction);
            foreach (OpaqueId contradiction in claim.ContradictingEvidenceIds)
            {
                if (!evidence.Claims.Any(item => item.ClaimId == contradiction))
                {
                    throw new InvalidOperationException("Documentation contradiction edge is dangling.");
                }
                InsertDocumentationEdge(
                    evidence.OriginatingRunId.Value,
                    "evidence-revision",
                    claim.ClaimId.Value,
                    "evidence-revision",
                    contradiction.Value,
                    "contradicts",
                    payloadId,
                    now,
                    transaction);
            }
        }
        foreach (ClaimApplicationContract application in evidence.Applications)
        {
            if (!application.EvidenceIds.All(id => evidence.Claims.Any(item => item.ClaimId == id)))
            {
                throw new InvalidOperationException("Documentation application evidence edge is dangling.");
            }
            InsertDocumentationEdge(
                application.ConsumingRunId.Value,
                "claim-application",
                application.ApplicationId.Value,
                "analysis-context",
                application.AnalysisContextId.Value,
                "applies",
                payloadId,
                now,
                transaction);
            InsertDocumentationEdge(
                application.ConsumingRunId.Value,
                "claim-application",
                application.ApplicationId.Value,
                application.SubjectType,
                application.SubjectId.Value,
                "applies-to",
                payloadId,
                now,
                transaction);
            foreach (OpaqueId evidenceId in application.EvidenceIds)
            {
                InsertDocumentationEdge(
                    application.ConsumingRunId.Value,
                    "claim-application",
                    application.ApplicationId.Value,
                    "evidence-revision",
                    evidenceId.Value,
                    "supported-by",
                    payloadId,
                    now,
                    transaction);
            }
        }
        foreach (DocumentationPurposeAssignmentContract assignment in evidence.PurposeAssignments)
        {
            ClaimApplicationContract application = evidence.Applications.Single(item =>
                item.ApplicationId == assignment.ApplicationId);
            InsertDocumentationEdge(
                application.ConsumingRunId.Value,
                "taxonomy-assignment",
                assignment.AssignmentId.Value,
                "claim-application",
                assignment.ApplicationId.Value,
                "derived-from",
                payloadId,
                now,
                transaction);
            InsertDocumentationEdge(
                application.ConsumingRunId.Value,
                "taxonomy-assignment",
                assignment.AssignmentId.Value,
                "evidence-revision",
                assignment.ClaimId.Value,
                "supported-by",
                payloadId,
                now,
                transaction);
            InsertDocumentationEdge(
                application.ConsumingRunId.Value,
                "taxonomy-assignment",
                assignment.AssignmentId.Value,
                assignment.SubjectType,
                assignment.SubjectId.Value,
                "classifies",
                payloadId,
                now,
                transaction);
            foreach (OpaqueId conditionId in assignment.ApplicabilityConditionIds)
            {
                InsertDocumentationEdge(
                    application.ConsumingRunId.Value,
                    "taxonomy-assignment",
                    assignment.AssignmentId.Value,
                    "evidence-revision",
                    conditionId.Value,
                    "conditioned-by",
                    payloadId,
                    now,
                    transaction);
            }
        }
        foreach (DocumentationGapContract gap in evidence.Gaps)
        {
            InsertDocumentationEdge(
                evidence.OriginatingRunId.Value,
                "documentation-gap",
                gap.GapId.Value,
                "documentation-revision",
                gap.RevisionId.Value,
                "limits",
                payloadId,
                now,
                transaction);
            if (gap.ClaimId is { } claimId)
            {
                InsertDocumentationEdge(
                    evidence.OriginatingRunId.Value,
                    "documentation-gap",
                    gap.GapId.Value,
                    "evidence-revision",
                    claimId.Value,
                    "limits",
                    payloadId,
                    now,
                    transaction);
            }
            if (gap.ApplicationId is { } applicationId)
            {
                InsertDocumentationEdge(
                    evidence.OriginatingRunId.Value,
                    "documentation-gap",
                    gap.GapId.Value,
                    "claim-application",
                    applicationId.Value,
                    "limits",
                    payloadId,
                    now,
                    transaction);
            }
        }
    }

    private void RequireExactDocumentationDependencySets(
        DocumentationEvidenceContract evidence,
        SqliteTransaction transaction)
    {
        foreach (DocumentationClaimContract claim in evidence.Claims)
        {
            RequireExactDocumentationEdgeTargets(
                evidence.OriginatingRunId.Value,
                "evidence-revision",
                claim.ClaimId.Value,
                "evidence-revision",
                "contradicts",
                claim.ContradictingEvidenceIds.Select(item => item.Value),
                transaction);
        }
        foreach (ClaimApplicationContract application in evidence.Applications)
        {
            RequireExactDocumentationEdgeTargets(
                application.ConsumingRunId.Value,
                "claim-application",
                application.ApplicationId.Value,
                "evidence-revision",
                "supported-by",
                application.EvidenceIds.Select(item => item.Value),
                transaction);
        }
        foreach (DocumentationPurposeAssignmentContract assignment in evidence.PurposeAssignments)
        {
            ClaimApplicationContract application = evidence.Applications.Single(item =>
                item.ApplicationId == assignment.ApplicationId);
            RequireExactDocumentationEdgeTargets(
                application.ConsumingRunId.Value,
                "taxonomy-assignment",
                assignment.AssignmentId.Value,
                "evidence-revision",
                "conditioned-by",
                assignment.ApplicabilityConditionIds.Select(item => item.Value),
                transaction);
        }
    }

    private void RequireDeletionPayloadOwners(
        string payloadId,
        string revisionId,
        IEnumerable<string> passageIds,
        SqliteTransaction transaction)
    {
        HashSet<(string Kind, string Id)> permitted = new()
        {
            ("documentation-revision", revisionId),
        };
        permitted.UnionWith(passageIds.Select(item => ("documentation-passage", item)));
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT owner_kind, owner_id FROM payload_owners WHERE payload_id = $payload;";
        command.Parameters.AddWithValue("$payload", payloadId);
        using SqliteDataReader reader = command.ExecuteReader();
        bool found = false;
        while (reader.Read())
        {
            found = true;
            if (!permitted.Contains((reader.GetString(0), reader.GetString(1))))
            {
                throw new InvalidOperationException(
                    "A documentation deletion receipt must identify payloads independently retained by another owner.");
            }
        }
        if (!found)
        {
            throw new InvalidDataException("A documentation deletion target has no admitted payload owner.");
        }
    }

    private Dictionary<string, HashSet<(string Kind, string Id)>> ReadDocumentationDeletionPayloadOwners(
        DocumentationDeletionReceiptContract receipt)
    {
        HashSet<string> targetPayloadIds = [];
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT body_payload_id FROM documentation_revisions WHERE documentation_revision_id = $revision;";
            command.Parameters.AddWithValue("$revision", receipt.RevisionId.Value);
            object? value = command.ExecuteScalar();
            if (value is string payloadId)
            {
                targetPayloadIds.Add(payloadId);
            }
        }
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT documentation_passage_id, passage_payload_id
                FROM documentation_passages
                WHERE documentation_revision_id = $revision;
                """;
            command.Parameters.AddWithValue("$revision", receipt.RevisionId.Value);
            using SqliteDataReader reader = command.ExecuteReader();
            HashSet<string> passageIds = receipt.DeletedPassageIds
                .Select(item => item.Value)
                .ToHashSet(StringComparer.Ordinal);
            while (reader.Read())
            {
                if (passageIds.Contains(reader.GetString(0)) && !reader.IsDBNull(1))
                {
                    targetPayloadIds.Add(reader.GetString(1));
                }
            }
        }
        return targetPayloadIds.ToDictionary(
            payloadId => payloadId,
            payloadId => ReadPayloadOwners(payloadId, null),
            StringComparer.Ordinal);
    }

    private bool HasExternalDocumentationDeletionOwner(
        string payloadId,
        string revisionId,
        IEnumerable<string> passageIds,
        SqliteTransaction transaction)
    {
        HashSet<(string Kind, string Id)> permitted = new()
        {
            ("documentation-revision", revisionId),
        };
        permitted.UnionWith(passageIds.Select(item => ("documentation-passage", item)));
        return ReadPayloadOwners(payloadId, transaction).Any(owner => !permitted.Contains(owner));
    }

    private bool HasDocumentationBackupPin(
        string payloadId,
        SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT COUNT(*) FROM payload_backup_pins WHERE payload_id = $payload;";
        command.Parameters.AddWithValue("$payload", payloadId);
        return Convert.ToInt64(
            command.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private HashSet<(string Kind, string Id)> ReadPayloadOwners(
        string payloadId,
        SqliteTransaction? transaction)
    {
        HashSet<(string Kind, string Id)> owners = [];
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT owner_kind, owner_id FROM payload_owners WHERE payload_id = $payload;";
        command.Parameters.AddWithValue("$payload", payloadId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            owners.Add((reader.GetString(0), reader.GetString(1)));
        }
        return owners;
    }

    private OpaqueId DocumentationPayloadSemanticId(
        string payloadId,
        SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT content_sha256 FROM payloads WHERE payload_id = $payload;";
        command.Parameters.AddWithValue("$payload", payloadId);
        object? value = command.ExecuteScalar();
        if (value is not string sha || sha.Length != 64)
        {
            throw new InvalidDataException("A documentation deletion payload identity is missing.");
        }
        return new OpaqueId("payloadsha-" + sha[..32]);
    }

    private static OpaqueId StableDocumentationId(string prefix, params string[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            hash.AppendData(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(bytes.Length)));
            hash.AppendData(bytes);
        }
        return new OpaqueId(prefix + "-" + Convert.ToHexStringLower(hash.GetHashAndReset())[..32]);
    }

    private static string CanonicalDocumentation(IEnumerable<string> values) =>
        string.Concat(values.Order(StringComparer.Ordinal).Select(value =>
            FormattableString.Invariant($"{Encoding.UTF8.GetByteCount(value)}:{value}")));

    private void RequireExactDocumentationEdgeTargets(
        string runId,
        string fromKind,
        string fromId,
        string toKind,
        string edgeKind,
        IEnumerable<string> expectedTargets,
        SqliteTransaction transaction)
    {
        string[] targets = expectedTargets.Order(StringComparer.Ordinal).ToArray();
        long actualCount = ScalarLong(
            """
            SELECT COUNT(*) FROM analysis_dependency_edges
            WHERE run_id = $run AND from_kind = $from_kind AND from_id = $from
              AND to_kind = $to_kind AND edge_kind = $edge_kind;
            """,
            transaction,
            ("$run", runId),
            ("$from_kind", fromKind),
            ("$from", fromId),
            ("$to_kind", toKind),
            ("$edge_kind", edgeKind));
        if (actualCount != targets.Length)
        {
            throw new InvalidDataException("A documentation identity resolves to a different dependency-edge set.");
        }
        foreach (string target in targets)
        {
            RequireSingleDocumentationRow(
                """
                SELECT COUNT(*) FROM analysis_dependency_edges
                WHERE run_id = $run AND from_kind = $from_kind AND from_id = $from
                  AND to_kind = $to_kind AND to_id = $to AND edge_kind = $edge_kind;
                """,
                "A documentation identity resolves to a different dependency-edge target.",
                transaction,
                ("$run", runId),
                ("$from_kind", fromKind),
                ("$from", fromId),
                ("$to_kind", toKind),
                ("$to", target),
                ("$edge_kind", edgeKind));
        }
    }

    private void InsertDocumentationEdge(
        string runId,
        string fromKind,
        string fromId,
        string toKind,
        string toId,
        string edgeKind,
        string payloadId,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        string material = string.Join("\n", runId, fromKind, fromId, toKind, toId, edgeKind);
        string edgeId = "depedge-" + Hash(Encoding.UTF8.GetBytes(material))[..32];
        Execute(
            """
            INSERT OR IGNORE INTO analysis_dependency_edges(
                dependency_edge_id, run_id, from_kind, from_id, to_kind, to_id,
                edge_kind, edge_payload_id, created_at)
            VALUES ($edge, $run, $from_kind, $from, $to_kind, $to, $kind, $payload, $now);
            """,
            transaction,
            ("$edge", edgeId),
            ("$run", runId),
            ("$from_kind", fromKind),
            ("$from", fromId),
            ("$to_kind", toKind),
            ("$to", toId),
            ("$kind", edgeKind),
            ("$payload", payloadId),
            ("$now", ToText(now)));
        RequireSingleDocumentationRow(
            """
            SELECT COUNT(*) FROM analysis_dependency_edges
            WHERE dependency_edge_id = $edge
              AND run_id = $run
              AND from_kind = $from_kind
              AND from_id = $from
              AND to_kind = $to_kind
              AND to_id = $to
              AND edge_kind = $kind;
            """,
            "A documentation dependency-edge ID resolves to different traversal semantics.",
            transaction,
            ("$edge", edgeId),
            ("$run", runId),
            ("$from_kind", fromKind),
            ("$from", fromId),
            ("$to_kind", toKind),
            ("$to", toId),
            ("$kind", edgeKind));
    }

    private static string SourceKindToken(DocumentationSourceKind value) => value switch
    {
        DocumentationSourceKind.ProjectAuthoredLocal => "project-authored-local",
        DocumentationSourceKind.Fixture => "fixture",
        _ => throw new InvalidOperationException("Documentation source kind is not closed."),
    };

    private static string ImportModeToken(DocumentationImportMode value) => value switch
    {
        DocumentationImportMode.CleanImport => "clean-import",
        DocumentationImportMode.RetainedReuse => "retained-reuse",
        _ => throw new InvalidOperationException("Documentation import mode is not closed."),
    };

    private static string ResultStateToken(AnalysisResultState value) => value switch
    {
        AnalysisResultState.Present => "present",
        AnalysisResultState.Partial => "partial",
        AnalysisResultState.Unavailable => "unavailable",
        _ => throw new InvalidOperationException("Documentation retention state is not persistable."),
    };

    private static string ReplayStateToken(ReplayState value) => value switch
    {
        ReplayState.CompleteClean => "complete-clean",
        ReplayState.Partial => "partial",
        ReplayState.AuditOnly => "audit-only",
        ReplayState.Unavailable => "unavailable",
        ReplayState.FailedIdentityDrift => "failed-identity-drift",
        _ => throw new InvalidOperationException("Documentation replay state is not closed."),
    };

    private static string ReplayEffectToken(ReplayState value) => value switch
    {
        ReplayState.CompleteClean => "none",
        ReplayState.Partial => "partial",
        ReplayState.AuditOnly => "audit-only",
        ReplayState.Unavailable or ReplayState.FailedIdentityDrift => "unavailable",
        _ => throw new InvalidOperationException("Documentation replay effect is not closed."),
    };

    private static string DocumentationGapKindToken(DocumentationGapKind value) => value switch
    {
        DocumentationGapKind.Contradiction => "contradiction",
        DocumentationGapKind.Deletion => "deletion",
        DocumentationGapKind.UnavailableSource => "unavailable-source",
        DocumentationGapKind.Replay => "replay",
        _ => throw new InvalidOperationException("Documentation gap kind is not closed."),
    };

    private static string ClaimKindToken(ClaimKind value) => value switch
    {
        ClaimKind.DeclaredPurpose => "declared-purpose",
        ClaimKind.Requirement => "requirement",
        ClaimKind.Incompatibility => "incompatibility",
        ClaimKind.InstallationInstruction => "installation-instruction",
        ClaimKind.PriorityInstruction => "priority-instruction",
        ClaimKind.LifecycleInstruction => "lifecycle-instruction",
        ClaimKind.ConfigurationInstruction => "configuration-instruction",
        ClaimKind.PatchInstruction => "patch-instruction",
        ClaimKind.KnownIssue => "known-issue",
        _ => throw new InvalidOperationException("Documentation claim kind is not closed."),
    };

    private static string EvidenceAuthorityToken(EvidenceAuthority value) => value switch
    {
        EvidenceAuthority.SnapshotBoundLocal => "snapshot-bound-local",
        EvidenceAuthority.DeterministicDerived => "deterministic-derived",
        EvidenceAuthority.AuthoritativeExternal => "authoritative-external",
        EvidenceAuthority.CorroboratedCommunity => "corroborated-community",
        EvidenceAuthority.UncorroboratedReport => "uncorroborated-report",
        EvidenceAuthority.UserStatement => "user-statement",
        EvidenceAuthority.TestResult => "test-result",
        EvidenceAuthority.HeuristicOrLlmInference => "heuristic-or-llm-inference",
        _ => throw new InvalidOperationException("Evidence authority is not closed."),
    };

    private static string ApplicabilityToken(ClaimApplicabilityState value) => value switch
    {
        ClaimApplicabilityState.Applicable => "applicable",
        ClaimApplicabilityState.NotApplicable => "not-applicable",
        ClaimApplicabilityState.Unknown => "unknown",
        ClaimApplicabilityState.Unsupported => "unsupported",
        ClaimApplicabilityState.Contradicted => "contradicted",
        _ => throw new InvalidOperationException("Claim applicability is not closed."),
    };

    private static string ClassificationRoleToken(ClassificationRole value) => value switch
    {
        ClassificationRole.Declared => "declared",
        ClassificationRole.Observed => "observed",
        ClassificationRole.Predicted => "predicted",
        ClassificationRole.Established => "established",
        _ => throw new InvalidOperationException("Classification role is not closed."),
    };

    private static JsonSerializerOptions CreateDocumentationPayloadJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new PersistenceOpaqueIdJsonConverter());
        options.Converters.Add(new PersistenceContractVersionJsonConverter());
        options.Converters.Add(new PersistenceSha256FingerprintJsonConverter());
        options.Converters.Add(new PersistenceUtcTimestampJsonConverter());
        return options;
    }

    private sealed class PersistenceOpaqueIdJsonConverter : JsonConverter<OpaqueId>
    {
        public override OpaqueId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("Opaque ID must be a string."));

        public override void Write(Utf8JsonWriter writer, OpaqueId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class PersistenceContractVersionJsonConverter : JsonConverter<ContractVersion>
    {
        public override ContractVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ContractVersion.Parse(reader.GetString() ?? throw new JsonException("Contract version must be a string."));

        public override void Write(Utf8JsonWriter writer, ContractVersion value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    private sealed class PersistenceSha256FingerprintJsonConverter : JsonConverter<Sha256Fingerprint>
    {
        public override Sha256Fingerprint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("SHA-256 must be a string."));

        public override void Write(Utf8JsonWriter writer, Sha256Fingerprint value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class PersistenceUtcTimestampJsonConverter : JsonConverter<UtcTimestamp>
    {
        public override UtcTimestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            UtcTimestamp.Parse(reader.GetString() ?? throw new JsonException("UTC timestamp must be a string."));

        public override void Write(Utf8JsonWriter writer, UtcTimestamp value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

}

#pragma warning restore CA1869
#pragma warning restore CA1512
#pragma warning restore IDE0008
