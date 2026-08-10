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
    private void ApplyMigrations()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            var current = Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            if (current > CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Database schema {current} is newer than supported schema {CurrentSchemaVersion}.");
            }

            if (current == 0)
            {
                using var transaction = BeginTransaction();
                Execute(SchemaV1, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    INSERT INTO store_metadata(key, value) VALUES ('schema_version', '1');
                    INSERT INTO store_metadata(key, value) VALUES ('schema_fingerprint', $schema_fingerprint);
                    INSERT INTO store_metadata(key, value) VALUES ('storage_contract_version', '1.0.0');
                    INSERT INTO store_metadata(key, value) VALUES ('sqlite_version', $sqlite_version);
                    INSERT INTO store_metadata(key, value) VALUES ('sqlite_source_id', $sqlite_source);
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S2-0001', 0, 1, $now, $sqlite_source);
                    PRAGMA user_version = 1;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_version", BindingIdentity.Version),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 1)
            {
                using var transaction = BeginTransaction();
                Execute(SchemaV2, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '2' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.1.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S3-0002', 1, 2, $now, $sqlite_source);
                    PRAGMA user_version = 2;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 2)
            {
                using var transaction = BeginTransaction();
                Execute(SchemaV3, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '3' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.2.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S4-0003', 2, 3, $now, $sqlite_source);
                    PRAGMA user_version = 3;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 3)
            {
                ValidateSchema3MigrationSource();
                using var transaction = BeginTransaction();
                Execute(SchemaV4, transaction);
                CreateSchemaV4AppendOnlyTriggers(transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '4' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.3.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S5-0004', 3, 4, $now, $sqlite_source);
                    PRAGMA user_version = 4;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 4)
            {
                ValidateSchema4MigrationSource();
                using var transaction = BeginTransaction();
                Execute(SchemaV5, transaction);
                CreateAppendOnlyTriggers(SchemaV5AppendOnlyTables, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '5' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.4.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S5-WP4-0005', 4, 5, $now, $sqlite_source);
                    PRAGMA user_version = 5;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }
        }
    }

    private void ValidateSchema4MigrationSource()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT key, value FROM store_metadata
                WHERE key IN (
                    'schema_version','schema_fingerprint','storage_contract_version',
                    'sqlite_version','sqlite_source_id');
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata.Add(reader.GetString(0), reader.GetString(1));
            }
        }
        const string schema4Fingerprint = "0e4fbeb821fdd83d86737d60979fa35d9a1300a4d971450c516f66d07ef2231e";
        if (metadata.Count != 5
            || metadata["schema_version"] != "4"
            || metadata["storage_contract_version"] != "1.3.0"
            || metadata["sqlite_version"] != BindingIdentity.Version
            || metadata["sqlite_source_id"] != BindingIdentity.SourceId
            || metadata["schema_fingerprint"] != schema4Fingerprint
            || ComputeSchemaFingerprint(connection) != schema4Fingerprint)
        {
            throw new InvalidOperationException(
                "Schema 4 does not match the exact accepted analysis contract required for finding and case storage migration.");
        }
        using SqliteCommand migration = connection.CreateCommand();
        migration.CommandText =
            """
            SELECT COUNT(*) FROM migration_history
            WHERE migration_id = 'M1-S5-0004' AND from_version = 3 AND to_version = 4
              AND sqlite_source_id = $source;
            """;
        migration.Parameters.AddWithValue("$source", BindingIdentity.SourceId);
        if (Convert.ToInt32(migration.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException("Schema 4 migration provenance is invalid.");
        }
    }

    private void ValidateSchema3MigrationSource()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT key, value FROM store_metadata
                WHERE key IN (
                    'schema_version','schema_fingerprint','storage_contract_version',
                    'sqlite_version','sqlite_source_id');
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata.Add(reader.GetString(0), reader.GetString(1));
            }
        }

        if (metadata.Count != 5
            || metadata["schema_version"] != "3"
            || metadata["storage_contract_version"] != "1.2.0"
            || metadata["sqlite_version"] != BindingIdentity.Version
            || metadata["sqlite_source_id"] != BindingIdentity.SourceId
            || metadata["schema_fingerprint"] != SchemaV3Fingerprint
            || ComputeSchemaFingerprint(connection) != SchemaV3Fingerprint)
        {
            throw new InvalidOperationException(
                "Schema 3 does not match the exact accepted storage contract required for migration.");
        }

        using SqliteCommand migration = connection.CreateCommand();
        migration.CommandText =
            """
            SELECT COUNT(*) FROM migration_history
            WHERE migration_id = 'M1-S4-0003'
              AND from_version = 2
              AND to_version = 3
              AND sqlite_source_id = $source;
            """;
        migration.Parameters.AddWithValue("$source", BindingIdentity.SourceId);
        if (Convert.ToInt32(
                migration.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException(
                "Schema 3 migration provenance is invalid.");
        }
    }

    private void CreateSchemaV4AppendOnlyTriggers(SqliteTransaction transaction)
        => CreateAppendOnlyTriggers(SchemaV4AppendOnlyTables, transaction);

    private void CreateAppendOnlyTriggers(IEnumerable<string> tables, SqliteTransaction transaction)
    {
        foreach (string table in tables)
        {
            Execute(
                $"""
                CREATE TRIGGER {table}_append_only_update
                BEFORE UPDATE ON {table}
                BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
                CREATE TRIGGER {table}_append_only_delete
                BEFORE DELETE ON {table}
                BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
                """,
                transaction);
        }
    }

    private static readonly HashSet<string> RequiredSchemaObjects =
    [
        "index:idx_attempts_run",
        "index:idx_attempts_one_live_per_run",
        "index:idx_candidate_decisions_run_population",
        "index:idx_candidates_run_lane",
        "index:idx_case_memberships_member",
        "index:idx_coverage_run_population",
        "index:idx_dependency_edges_from",
        "index:idx_dependency_edges_to",
        "index:idx_documentation_passages_revision",
        "index:idx_documentation_imports_revision",
        "index:idx_documentation_application_bindings_run",
        "index:idx_documentation_deletion_receipts_revision",
        "index:idx_effect_receipts_run",
        "index:idx_evidence_applications_run",
        "index:idx_evidence_revisions_passage",
        "index:idx_events_run_sequence",
        "index:idx_findings_signature",
        "index:idx_gaps_run_population",
        "index:idx_hypotheses_candidate",
        "index:idx_lineage_successor",
        "index:idx_recommendations_finding",
        "index:idx_reconciliation_successor",
        "index:idx_replay_manifests_run",
        "index:idx_run_outputs_run",
        "index:idx_runs_created",
        "index:idx_runs_dispatch",
        "index:idx_snapshot_capture_dispatch",
        "index:idx_snapshot_capture_one_live_attempt",
        "index:idx_taxonomy_subject",
        "table:analysis_candidates",
        "table:analysis_coverage",
        "table:analysis_coverage_failure_links",
        "table:analysis_coverage_gap_links",
        "table:analysis_coverage_taxonomy_links",
        "table:analysis_dependency_edges",
        "table:analysis_gaps",
        "table:analysis_hypotheses",
        "table:analysis_recommendations",
        "table:analysis_replay_manifests",
        "table:analysis_run_outputs",
        "table:attempts",
        "table:audit_events",
        "table:candidate_decisions",
        "table:case_memberships",
        "table:case_hypothesis_memberships",
        "table:case_occurrence_details",
        "table:case_occurrences",
        "table:checkpoints",
        "table:coordinator_leases",
        "table:documentation_passages",
        "table:documentation_imports",
        "table:documentation_revisions",
        "table:documentation_application_bindings",
        "table:documentation_deletion_receipts",
        "table:documentation_purpose_assignment_details",
        "table:documentation_gap_details",
        "table:durable_commands",
        "table:effect_receipts",
        "table:evidence_application_links",
        "table:evidence_revisions",
        "table:finding_occurrence_details",
        "table:finding_case_abstentions",
        "table:finding_case_case_details",
        "table:finding_case_finding_details",
        "table:finding_case_gap_details",
        "table:finding_case_publications",
        "table:finding_case_recommendations",
        "table:finding_case_taxonomy_assignments",
        "table:finding_promotion_assessments",
        "table:finding_occurrences",
        "table:job_nodes",
        "table:lifecycle_events",
        "table:lineage_details",
        "table:lineage_event_edges",
        "table:lineage_events",
        "table:logical_cases",
        "table:logical_findings",
        "table:migration_history",
        "table:payload_owners",
        "table:payload_backup_pins",
        "table:payloads",
        "table:publication_receipt_payloads",
        "table:publication_receipts",
        "table:reconciliation_assessments",
        "table:reconciliation_details",
        "table:reconciliation_metadata",
        "table:reconciliation_proof_links",
        "table:run_projection",
        "table:run_operations",
        "table:runs",
        "table:store_metadata",
        "table:snapshot_capture_attempts",
        "table:snapshot_capture_operations",
        "table:snapshot_capture_publications",
        "table:taxonomy_assignments",
        "table:taxonomy_projection_edges",
        "trigger:analysis_candidates_append_only_delete",
        "trigger:analysis_candidates_append_only_update",
        "trigger:analysis_coverage_append_only_delete",
        "trigger:analysis_coverage_append_only_update",
        "trigger:analysis_coverage_failure_links_append_only_delete",
        "trigger:analysis_coverage_failure_links_append_only_update",
        "trigger:analysis_coverage_gap_links_append_only_delete",
        "trigger:analysis_coverage_gap_links_append_only_update",
        "trigger:analysis_coverage_taxonomy_links_append_only_delete",
        "trigger:analysis_coverage_taxonomy_links_append_only_update",
        "trigger:analysis_dependency_edges_append_only_delete",
        "trigger:analysis_dependency_edges_append_only_update",
        "trigger:analysis_gaps_append_only_delete",
        "trigger:analysis_gaps_append_only_update",
        "trigger:analysis_hypotheses_append_only_delete",
        "trigger:analysis_hypotheses_append_only_update",
        "trigger:analysis_recommendations_append_only_delete",
        "trigger:analysis_recommendations_append_only_update",
        "trigger:analysis_replay_manifests_append_only_delete",
        "trigger:analysis_replay_manifests_append_only_update",
        "trigger:analysis_run_outputs_append_only_delete",
        "trigger:analysis_run_outputs_append_only_update",
        "trigger:candidate_decisions_append_only_delete",
        "trigger:candidate_decisions_append_only_update",
        "trigger:case_memberships_append_only_delete",
        "trigger:case_memberships_append_only_update",
        "trigger:case_hypothesis_memberships_append_only_delete",
        "trigger:case_hypothesis_memberships_append_only_update",
        "trigger:case_occurrence_details_append_only_delete",
        "trigger:case_occurrence_details_append_only_update",
        "trigger:documentation_passages_append_only_delete",
        "trigger:documentation_passages_append_only_update",
        "trigger:documentation_imports_append_only_delete",
        "trigger:documentation_imports_append_only_update",
        "trigger:documentation_revisions_append_only_delete",
        "trigger:documentation_revisions_append_only_update",
        "trigger:documentation_application_bindings_append_only_delete",
        "trigger:documentation_application_bindings_append_only_update",
        "trigger:documentation_deletion_receipts_append_only_delete",
        "trigger:documentation_deletion_receipts_append_only_update",
        "trigger:documentation_purpose_assignment_details_append_only_delete",
        "trigger:documentation_purpose_assignment_details_append_only_update",
        "trigger:documentation_gap_details_append_only_delete",
        "trigger:documentation_gap_details_append_only_update",
        "trigger:payload_backup_pins_append_only_delete",
        "trigger:payload_backup_pins_append_only_update",
        "trigger:effect_receipts_append_only_delete",
        "trigger:effect_receipts_append_only_update",
        "trigger:evidence_application_links_append_only_delete",
        "trigger:evidence_application_links_append_only_update",
        "trigger:evidence_revisions_append_only_delete",
        "trigger:evidence_revisions_append_only_update",
        "trigger:finding_occurrence_details_append_only_delete",
        "trigger:finding_occurrence_details_append_only_update",
        "trigger:finding_case_abstentions_append_only_delete",
        "trigger:finding_case_abstentions_append_only_update",
        "trigger:finding_case_case_details_append_only_delete",
        "trigger:finding_case_case_details_append_only_update",
        "trigger:finding_case_finding_details_append_only_delete",
        "trigger:finding_case_finding_details_append_only_update",
        "trigger:finding_case_gap_details_append_only_delete",
        "trigger:finding_case_gap_details_append_only_update",
        "trigger:finding_case_publications_append_only_delete",
        "trigger:finding_case_publications_append_only_update",
        "trigger:finding_case_recommendations_append_only_delete",
        "trigger:finding_case_recommendations_append_only_update",
        "trigger:finding_case_taxonomy_assignments_append_only_delete",
        "trigger:finding_case_taxonomy_assignments_append_only_update",
        "trigger:finding_promotion_assessments_append_only_delete",
        "trigger:finding_promotion_assessments_append_only_update",
        "trigger:lineage_details_append_only_delete",
        "trigger:lineage_details_append_only_update",
        "trigger:lineage_event_edges_append_only_delete",
        "trigger:lineage_event_edges_append_only_update",
        "trigger:reconciliation_details_append_only_delete",
        "trigger:reconciliation_details_append_only_update",
        "trigger:reconciliation_metadata_append_only_delete",
        "trigger:reconciliation_metadata_append_only_update",
        "trigger:reconciliation_proof_links_append_only_delete",
        "trigger:reconciliation_proof_links_append_only_update",
        "trigger:taxonomy_assignments_append_only_delete",
        "trigger:taxonomy_assignments_append_only_update",
        "trigger:taxonomy_projection_edges_append_only_delete",
        "trigger:taxonomy_projection_edges_append_only_update",
        "trigger:lifecycle_events_append_only_delete",
        "trigger:lifecycle_events_append_only_update",
        "trigger:audit_events_append_only_delete",
        "trigger:audit_events_append_only_update",
        "trigger:case_occurrences_append_only_delete",
        "trigger:case_occurrences_append_only_update",
        "trigger:checkpoints_append_only_delete",
        "trigger:checkpoints_append_only_update",
        "trigger:durable_commands_append_only_delete",
        "trigger:durable_commands_append_only_update",
        "trigger:finding_occurrences_append_only_delete",
        "trigger:finding_occurrences_append_only_update",
        "trigger:lineage_append_only_delete",
        "trigger:lineage_append_only_update",
        "trigger:publication_receipts_append_only_delete",
        "trigger:publication_receipts_append_only_update",
        "trigger:reconciliation_append_only_delete",
        "trigger:reconciliation_append_only_update",
        "trigger:runs_immutable_binding",
        "trigger:run_operations_immutable",
        "trigger:snapshot_capture_request_immutable",
        "trigger:snapshot_capture_publications_append_only_delete",
        "trigger:snapshot_capture_publications_append_only_update",
    ];

    private static readonly string[] SchemaV4AppendOnlyTables =
    [
        "analysis_candidates",
        "analysis_coverage",
        "analysis_dependency_edges",
        "analysis_gaps",
        "analysis_hypotheses",
        "analysis_recommendations",
        "analysis_replay_manifests",
        "analysis_run_outputs",
        "candidate_decisions",
        "case_memberships",
        "case_occurrence_details",
        "documentation_passages",
        "documentation_imports",
        "documentation_revisions",
        "documentation_application_bindings",
        "documentation_deletion_receipts",
        "documentation_purpose_assignment_details",
        "documentation_gap_details",
        "payload_backup_pins",
        "effect_receipts",
        "evidence_application_links",
        "evidence_revisions",
        "finding_occurrence_details",
        "lineage_details",
        "reconciliation_details",
        "taxonomy_assignments",
    ];

    private static readonly string[] SchemaV5AppendOnlyTables =
    [
        "analysis_coverage_failure_links",
        "analysis_coverage_gap_links",
        "analysis_coverage_taxonomy_links",
        "case_hypothesis_memberships",
        "finding_case_abstentions",
        "finding_case_case_details",
        "finding_case_finding_details",
        "finding_case_gap_details",
        "finding_case_publications",
        "finding_case_recommendations",
        "finding_case_taxonomy_assignments",
        "finding_promotion_assessments",
        "lineage_event_edges",
        "reconciliation_metadata",
        "reconciliation_proof_links",
        "taxonomy_projection_edges",
    ];

    private const string SchemaV5 =
        """
        ALTER TABLE lineage_events ADD COLUMN predecessor_occurrence_id TEXT;
        ALTER TABLE lineage_events ADD COLUMN successor_occurrence_id TEXT;
        ALTER TABLE finding_occurrences ADD COLUMN analyzer_version TEXT NOT NULL DEFAULT 'legacy-unspecified';
        DROP INDEX idx_findings_signature;
        CREATE INDEX idx_findings_signature ON finding_occurrences(
            analyzer_family, analyzer_version, identity_contract_version, canonical_signature);
        ALTER TABLE analysis_coverage ADD COLUMN analyzer_id TEXT NOT NULL DEFAULT 'legacy-unspecified';
        ALTER TABLE analysis_coverage ADD COLUMN denominator_label TEXT NOT NULL DEFAULT 'legacy coverage';
        ALTER TABLE analysis_coverage ADD COLUMN exclusions_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(exclusions_json));
        ALTER TABLE analysis_coverage ADD COLUMN member_results_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(member_results_json));
        CREATE TABLE finding_case_publications(
            finding_case_payload_id TEXT PRIMARY KEY REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            input_id TEXT NOT NULL,
            promotion_policy_id TEXT NOT NULL,
            promotion_policy_version TEXT NOT NULL,
            reconciliation_policy_id TEXT NOT NULL,
            reconciliation_policy_version TEXT NOT NULL,
            boundaries_json TEXT NOT NULL CHECK(json_valid(boundaries_json)),
            publication_claim_boundary TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_promotion_assessments(
            promotion_assessment_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            state_present INTEGER NOT NULL CHECK(state_present IN (0,1)),
            confidence_at_least_plausible INTEGER NOT NULL CHECK(confidence_at_least_plausible IN (0,1)),
            has_supporting_evidence INTEGER NOT NULL CHECK(has_supporting_evidence IN (0,1)),
            has_no_defeating_contradictions INTEGER NOT NULL CHECK(has_no_defeating_contradictions IN (0,1)),
            has_no_missing_information INTEGER NOT NULL CHECK(has_no_missing_information IN (0,1)),
            severity_closed INTEGER NOT NULL CHECK(severity_closed IN (0,1)),
            identity_closed INTEGER NOT NULL CHECK(identity_closed IN (0,1)),
            conclusion_available INTEGER NOT NULL CHECK(conclusion_available IN (0,1)),
            lead_eligible_state INTEGER NOT NULL CHECK(lead_eligible_state IN (0,1)),
            promotion_outcome TEXT NOT NULL CHECK(promotion_outcome IN ('supported-finding','lead-only','abstained')),
            reasons_json TEXT NOT NULL CHECK(json_valid(reasons_json)),
            assessment_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_abstentions(
            abstention_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            reason TEXT NOT NULL,
            required_information_json TEXT NOT NULL CHECK(json_valid(required_information_json)),
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            abstention_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_finding_details(
            finding_occurrence_id TEXT PRIMARY KEY REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            conclusion TEXT NOT NULL,
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            case_identity_envelope_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            taxonomy_assignment_ids_json TEXT NOT NULL CHECK(json_valid(taxonomy_assignment_ids_json)),
            semantic_fingerprint TEXT NOT NULL,
            supersedes_occurrence_id TEXT,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_recommendations(
            recommendation_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            finding_occurrence_id TEXT REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            abstention_id TEXT REFERENCES finding_case_abstentions(abstention_id) ON DELETE RESTRICT,
            lead_hypothesis_id TEXT REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            recommendation_kind TEXT NOT NULL CHECK(recommendation_kind IN (
                'remediation','alternative-remediation','validation','further-investigation','abstention')),
            action TEXT NOT NULL,
            uncertainty TEXT NOT NULL,
            reversibility TEXT NOT NULL,
            verification TEXT NOT NULL,
            risks_json TEXT NOT NULL CHECK(json_valid(risks_json)),
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            recommendation_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((finding_occurrence_id IS NOT NULL) + (abstention_id IS NOT NULL) + (lead_hypothesis_id IS NOT NULL) = 1)
        ) STRICT;
        CREATE TABLE case_hypothesis_memberships(
            case_hypothesis_membership_id TEXT PRIMARY KEY,
            case_occurrence_id TEXT NOT NULL REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            membership_role TEXT NOT NULL CHECK(membership_role IN ('cause','lead')),
            cause_proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(case_occurrence_id,hypothesis_id)
        ) STRICT;
        CREATE TABLE finding_case_case_details(
            case_occurrence_id TEXT PRIMARY KEY REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            shared_cause TEXT NOT NULL,
            cause_proof_evidence_ids_json TEXT NOT NULL CHECK(json_valid(cause_proof_evidence_ids_json)),
            semantic_fingerprint TEXT NOT NULL,
            supersedes_occurrence_id TEXT,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_taxonomy_assignments(
            taxonomy_assignment_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('hypothesis','finding-occurrence','case-occurrence')),
            subject_id TEXT NOT NULL,
            taxonomy_id TEXT NOT NULL,
            taxonomy_version TEXT NOT NULL,
            axis TEXT NOT NULL,
            facet TEXT NOT NULL,
            taxonomy_code TEXT,
            applicability_state TEXT NOT NULL CHECK(applicability_state IN ('assigned','unknown','unsupported','unmapped','not-applicable')),
            classification_role TEXT CHECK(classification_role IN ('declared','observed','predicted','established')),
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            applicability_condition_ids_json TEXT NOT NULL CHECK(json_valid(applicability_condition_ids_json)),
            confidence_assessment_id TEXT,
            analyzer_or_adjudicator_id TEXT NOT NULL,
            reason TEXT NOT NULL,
            supersedes_assignment_ids_json TEXT NOT NULL CHECK(json_valid(supersedes_assignment_ids_json)),
            assignment_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((applicability_state = 'assigned') = (taxonomy_code IS NOT NULL)),
            CHECK(applicability_state <> 'assigned' OR classification_role IS NOT NULL)
        ) STRICT;
        CREATE TABLE taxonomy_projection_edges(
            taxonomy_projection_id TEXT PRIMARY KEY,
            source_assignment_id TEXT NOT NULL REFERENCES finding_case_taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            projected_assignment_id TEXT NOT NULL REFERENCES finding_case_taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            mapping_authority_id TEXT NOT NULL,
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            reason TEXT NOT NULL,
            projection_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_gap_details(
            gap_id TEXT PRIMARY KEY REFERENCES analysis_gaps(gap_id) ON DELETE RESTRICT,
            reason TEXT NOT NULL,
            missing_capability_or_information TEXT NOT NULL,
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_coverage_taxonomy_links(
            coverage_taxonomy_link_id TEXT PRIMARY KEY,
            coverage_result_id TEXT NOT NULL REFERENCES analysis_coverage(coverage_result_id) ON DELETE RESTRICT,
            taxonomy_assignment_id TEXT NOT NULL REFERENCES finding_case_taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            link_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(coverage_result_id,taxonomy_assignment_id)
        ) STRICT;
        CREATE TABLE analysis_coverage_gap_links(
            coverage_gap_link_id TEXT PRIMARY KEY,
            coverage_result_id TEXT NOT NULL REFERENCES analysis_coverage(coverage_result_id) ON DELETE RESTRICT,
            gap_id TEXT NOT NULL REFERENCES analysis_gaps(gap_id) ON DELETE RESTRICT,
            link_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(coverage_result_id,gap_id)
        ) STRICT;
        CREATE TABLE analysis_coverage_failure_links(
            coverage_failure_link_id TEXT PRIMARY KEY,
            coverage_result_id TEXT NOT NULL REFERENCES analysis_coverage(coverage_result_id) ON DELETE RESTRICT,
            failure_id TEXT NOT NULL,
            failure_code TEXT NOT NULL,
            message TEXT NOT NULL,
            retryable INTEGER NOT NULL CHECK(retryable IN (0,1)),
            link_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(coverage_result_id,failure_id)
        ) STRICT;
        CREATE TABLE reconciliation_metadata(
            reconciliation_assessment_id TEXT PRIMARY KEY REFERENCES reconciliation_assessments(reconciliation_assessment_id) ON DELETE RESTRICT,
            actor_id TEXT NOT NULL,
            policy_id TEXT NOT NULL,
            policy_version TEXT NOT NULL,
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            visible_by_default INTEGER NOT NULL CHECK(visible_by_default IN (0,1)),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE reconciliation_proof_links(
            reconciliation_proof_link_id TEXT PRIMARY KEY,
            reconciliation_assessment_id TEXT NOT NULL REFERENCES reconciliation_assessments(reconciliation_assessment_id) ON DELETE RESTRICT,
            evidence_id TEXT NOT NULL,
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(reconciliation_assessment_id,evidence_id)
        ) STRICT;
        CREATE TABLE lineage_event_edges(
            lineage_event_edge_id TEXT PRIMARY KEY,
            lineage_event_id TEXT NOT NULL REFERENCES lineage_events(lineage_event_id) ON DELETE RESTRICT,
            edge_side TEXT NOT NULL CHECK(edge_side IN ('predecessor','successor')),
            occurrence_id TEXT NOT NULL,
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(lineage_event_id,edge_side,occurrence_id)
        ) STRICT;
        """;

    private const string SchemaV4 =
        """
        CREATE TABLE payload_backup_pins(
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            backup_identity TEXT NOT NULL,
            content_sha256 TEXT NOT NULL CHECK(
                length(content_sha256) = 64
                AND content_sha256 NOT GLOB '*[^0-9a-f]*'),
            created_at TEXT NOT NULL,
            PRIMARY KEY(payload_id, backup_identity)
        ) STRICT;
        CREATE TABLE documentation_revisions(
            documentation_revision_id TEXT PRIMARY KEY,
            source_id TEXT NOT NULL,
            source_kind TEXT NOT NULL CHECK(source_kind IN (
                'project-authored-local','fixture')),
            source_revision TEXT NOT NULL,
            supplying_snapshot_id TEXT,
            body_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            content_sha256 TEXT NOT NULL CHECK(
                length(content_sha256) = 64
                AND content_sha256 NOT GLOB '*[^0-9a-f]*'),
            byte_length INTEGER NOT NULL CHECK(byte_length >= 0),
            availability_state TEXT NOT NULL CHECK(availability_state IN (
                'present','partial','unavailable')),
            retention_state TEXT NOT NULL CHECK(retention_state IN (
                'present','partial','unavailable')),
            replay_state TEXT NOT NULL CHECK(replay_state IN (
                'complete-clean','partial','audit-only','unavailable','failed-identity-drift')),
            created_at TEXT NOT NULL,
            UNIQUE(source_id, source_revision, content_sha256),
            CHECK((availability_state = 'present') = (body_payload_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_imports(
            documentation_import_id TEXT PRIMARY KEY,
            import_run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            import_mode TEXT NOT NULL CHECK(import_mode IN (
                'clean-import','retained-reuse')),
            reused_import_id TEXT REFERENCES documentation_imports(documentation_import_id) ON DELETE RESTRICT,
            dependency_closure_id TEXT NOT NULL,
            extractor_id TEXT NOT NULL,
            llm_involvement TEXT NOT NULL CHECK(llm_involvement = 'none'),
            llm_operation TEXT NOT NULL CHECK(llm_operation = 'none'),
            boundaries_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            import_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(import_run_id, documentation_revision_id, import_mode),
            CHECK((import_mode = 'retained-reuse') = (reused_import_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_passages(
            documentation_passage_id TEXT PRIMARY KEY,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            utf8_byte_start INTEGER NOT NULL CHECK(utf8_byte_start >= 0),
            utf8_byte_end INTEGER NOT NULL CHECK(utf8_byte_end > utf8_byte_start),
            passage_sha256 TEXT NOT NULL CHECK(
                length(passage_sha256) = 64
                AND passage_sha256 NOT GLOB '*[^0-9a-f]*'),
            passage_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            availability_state TEXT NOT NULL CHECK(availability_state IN (
                'present','partial','unavailable')),
            created_at TEXT NOT NULL,
            UNIQUE(documentation_revision_id, utf8_byte_start, utf8_byte_end),
            CHECK((availability_state = 'present') = (passage_payload_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE evidence_revisions(
            evidence_revision_id TEXT PRIMARY KEY,
            documentation_passage_id TEXT
                REFERENCES documentation_passages(documentation_passage_id) ON DELETE RESTRICT,
            import_id TEXT NOT NULL
                REFERENCES documentation_imports(documentation_import_id) ON DELETE RESTRICT,
            payload_schema_id TEXT NOT NULL,
            payload_schema_version TEXT NOT NULL,
            evidence_kind TEXT NOT NULL CHECK(evidence_kind IN (
                'local-observation','deterministic-derived','documentation-claim')),
            claim_kind TEXT CHECK(claim_kind IN (
                'declared-purpose','requirement','incompatibility','installation-instruction',
                'priority-instruction','lifecycle-instruction','configuration-instruction',
                'patch-instruction','known-issue')),
            authority_kind TEXT NOT NULL CHECK(authority_kind IN (
                'snapshot-bound-local','deterministic-derived','authoritative-external',
                'corroborated-community','uncorroborated-report','user-statement',
                'test-result','heuristic-or-llm-inference')),
            applicability_state TEXT NOT NULL CHECK(applicability_state IN (
                'applicable','not-applicable','unknown','unsupported','contradicted')),
            classification_role TEXT CHECK(classification_role IN (
                'declared','observed','predicted','established')),
            evidence_state TEXT NOT NULL CHECK(evidence_state IN (
                'admitted','invalid-input','unsupported','unavailable','deleted')),
            evidence_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            contradiction_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK(
                (evidence_kind = 'documentation-claim')
                = (documentation_passage_id IS NOT NULL
                   AND claim_kind IS NOT NULL
                   AND classification_role IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_application_bindings(
            documentation_application_binding_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            subject_id TEXT NOT NULL,
            subject_type TEXT NOT NULL CHECK(subject_type = 'installed-entity'),
            dependency_closure_id TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, analysis_context_id, subject_type, subject_id, dependency_closure_id)
        ) STRICT;
        CREATE TABLE evidence_application_links(
            evidence_application_link_id TEXT PRIMARY KEY,
            evidence_revision_id TEXT NOT NULL
                REFERENCES evidence_revisions(evidence_revision_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            application_binding_id TEXT NOT NULL
                REFERENCES documentation_application_bindings(documentation_application_binding_id) ON DELETE RESTRICT,
            analysis_context_id TEXT NOT NULL,
            subject_id TEXT NOT NULL,
            subject_type TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            application_state TEXT NOT NULL CHECK(application_state IN (
                'applicable','not-applicable','unknown','unsupported','contradicted')),
            application_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(evidence_revision_id, run_id, analysis_context_id, subject_type, subject_id)
        ) STRICT;
        CREATE TABLE documentation_deletion_receipts(
            documentation_deletion_receipt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            deleted_body_sha256 TEXT NOT NULL CHECK(
                length(deleted_body_sha256) = 64
                AND deleted_body_sha256 NOT GLOB '*[^0-9a-f]*'),
            deleted_passage_ids_json TEXT NOT NULL CHECK(json_valid(deleted_passage_ids_json)),
            independently_retained_payload_ids_json TEXT NOT NULL
                CHECK(json_valid(independently_retained_payload_ids_json)),
            replay_effect TEXT NOT NULL CHECK(replay_effect IN ('audit-only','unavailable')),
            receipt_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            reason TEXT NOT NULL,
            deleted_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE candidate_decisions(
            candidate_decision_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            population_id TEXT NOT NULL,
            relationship_id TEXT NOT NULL,
            disposition TEXT NOT NULL CHECK(disposition IN (
                'candidate-admitted','resolved-negative','unsupported','ambiguous',
                'invalid-input','limited','deferred','unprocessed','failed')),
            lane TEXT NOT NULL CHECK(lane IN (
                'deterministic-required','mandatory-evidence','optional-ranked')),
            rule_version TEXT NOT NULL,
            decision_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_candidates(
            candidate_id TEXT PRIMARY KEY,
            candidate_decision_id TEXT NOT NULL UNIQUE
                REFERENCES candidate_decisions(candidate_decision_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            lane TEXT NOT NULL CHECK(lane IN (
                'deterministic-required','mandatory-evidence','optional-ranked')),
            candidate_state TEXT NOT NULL CHECK(candidate_state IN (
                'present','ambiguous','abstained')),
            dependency_closure_id TEXT NOT NULL,
            candidate_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_hypotheses(
            hypothesis_id TEXT PRIMARY KEY,
            candidate_id TEXT NOT NULL REFERENCES analysis_candidates(candidate_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            hypothesis_state TEXT NOT NULL CHECK(hypothesis_state IN (
                'present','ambiguous','partial')),
            confidence TEXT NOT NULL CHECK(confidence IN (
                'speculative-lead','plausible','strongly-supported','confirmed')),
            threshold_id TEXT NOT NULL,
            hypothesis_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_recommendations(
            recommendation_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            finding_occurrence_id TEXT
                REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            abstention_id TEXT,
            recommendation_kind TEXT NOT NULL CHECK(recommendation_kind IN (
                'remediation','alternative-remediation','validation',
                'further-investigation','abstention')),
            recommendation_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((finding_occurrence_id IS NOT NULL) <> (abstention_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE taxonomy_assignments(
            taxonomy_assignment_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN (
                'documentation-revision','evidence-revision','installed-entity','candidate','hypothesis',
                'finding-occurrence','case-occurrence')),
            subject_id TEXT NOT NULL,
            taxonomy_id TEXT NOT NULL,
            taxonomy_version TEXT NOT NULL,
            axis TEXT NOT NULL,
            facet TEXT NOT NULL,
            taxonomy_code TEXT,
            applicability_state TEXT NOT NULL CHECK(applicability_state IN (
                'assigned','unknown','unsupported','unmapped','not-applicable')),
            classification_role TEXT NOT NULL CHECK(classification_role IN (
                'declared','observed','predicted','established')),
            assignment_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((applicability_state = 'assigned') = (taxonomy_code IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_purpose_assignment_details(
            taxonomy_assignment_id TEXT PRIMARY KEY
                REFERENCES taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            evidence_revision_id TEXT NOT NULL
                REFERENCES evidence_revisions(evidence_revision_id) ON DELETE RESTRICT,
            evidence_application_link_id TEXT NOT NULL
                REFERENCES evidence_application_links(evidence_application_link_id) ON DELETE RESTRICT,
            analyzer_or_adjudicator_id TEXT NOT NULL,
            applicability_condition_ids_json TEXT NOT NULL
                CHECK(json_valid(applicability_condition_ids_json)),
            reason TEXT NOT NULL,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE analysis_coverage(
            coverage_result_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            population_id TEXT NOT NULL,
            coverage_state TEXT NOT NULL CHECK(coverage_state IN (
                'completed','completed-with-gaps','failed','skipped-by-configuration',
                'skipped-by-limit','unsupported')),
            denominator INTEGER NOT NULL CHECK(denominator >= 0),
            completed INTEGER NOT NULL CHECK(completed >= 0 AND completed <= denominator),
            excluded INTEGER NOT NULL CHECK(excluded >= 0),
            taxonomy_id TEXT NOT NULL,
            taxonomy_version TEXT NOT NULL,
            coverage_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, population_id)
        ) STRICT;
        CREATE TABLE analysis_gaps(
            gap_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            population_id TEXT NOT NULL,
            stage_id TEXT NOT NULL,
            gap_state TEXT NOT NULL CHECK(gap_state IN (
                'missing-information','missing-capability','missing-dependency','unsupported',
                'failed','limited','unavailable','deleted','audit-gap')),
            replay_effect TEXT NOT NULL CHECK(replay_effect IN (
                'none','partial','audit-only','unavailable')),
            conclusion_effect TEXT NOT NULL CHECK(conclusion_effect IN (
                'none','bounded','abstain','unavailable')),
            gap_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE documentation_gap_details(
            gap_id TEXT PRIMARY KEY REFERENCES analysis_gaps(gap_id) ON DELETE RESTRICT,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            evidence_revision_id TEXT REFERENCES evidence_revisions(evidence_revision_id) ON DELETE RESTRICT,
            evidence_application_link_id TEXT
                REFERENCES evidence_application_links(evidence_application_link_id) ON DELETE RESTRICT,
            gap_kind TEXT NOT NULL CHECK(gap_kind IN (
                'contradiction','deletion','unavailable-source','replay')),
            reason TEXT NOT NULL,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE analysis_dependency_edges(
            dependency_edge_id TEXT NOT NULL,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            from_kind TEXT NOT NULL CHECK(from_kind IN (
                'documentation-import','documentation-revision','passage','evidence-revision',
                'claim-application','taxonomy-assignment','documentation-gap','installed-entity','candidate-analysis-root','candidate-decision','dependency-closure',
                'candidate','hypothesis','abstention','failure','finding-occurrence','recommendation','case-occurrence',
                'coverage','gap','replay-manifest','run-output')),
            from_id TEXT NOT NULL,
            to_kind TEXT NOT NULL CHECK(to_kind IN (
                'snapshot','analysis-context','scan-configuration','resolved-input-manifest',
                'documentation-import','documentation-revision','passage','evidence-revision','claim-application',
                'installed-entity','documentation-evidence','evidence','dependency-closure','dependency','candidate-decision',
                'candidate','hypothesis','finding-occurrence','recommendation','case-occurrence',
                'coverage','gap','source-fact','payload','execution-input-binding','policy-binding','threshold-binding','limit-binding','analyzer-declaration-binding')),
            to_id TEXT NOT NULL,
            edge_kind TEXT NOT NULL CHECK(edge_kind IN (
                'derived-from','supports','supported-by','contradicts','applies','applies-to',
                'depends-on','consumes','conditioned-by','classifies','limits','reuses',
                'member-of','supersedes','produced-by','uses')),
            edge_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            PRIMARY KEY(dependency_edge_id, run_id),
            UNIQUE(run_id, from_kind, from_id, to_kind, to_id, edge_kind)
        ) STRICT;
        CREATE TABLE case_memberships(
            case_membership_id TEXT PRIMARY KEY,
            case_occurrence_id TEXT NOT NULL
                REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            member_kind TEXT NOT NULL CHECK(member_kind IN (
                'finding-occurrence','candidate')),
            member_id TEXT NOT NULL,
            membership_role TEXT NOT NULL CHECK(membership_role IN (
                'cause','effect','support','contradiction','lead')),
            cause_proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(case_occurrence_id, member_kind, member_id)
        ) STRICT;
        CREATE TABLE analysis_replay_manifests(
            replay_manifest_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            replay_mode TEXT NOT NULL CHECK(replay_mode IN (
                'clean','incremental','retained-downstream-replay')),
            replay_state TEXT NOT NULL CHECK(replay_state IN (
                'complete-clean','partial','audit-only','unavailable','failed-identity-drift')),
            auditability_state TEXT NOT NULL CHECK(auditability_state IN (
                'complete','partial','unavailable')),
            semantic_equivalence INTEGER NOT NULL CHECK(semantic_equivalence IN (0,1)),
            compared_run_id TEXT REFERENCES runs(run_id) ON DELETE RESTRICT,
            manifest_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            manifest_sha256 TEXT NOT NULL CHECK(
                length(manifest_sha256) = 64
                AND manifest_sha256 NOT GLOB '*[^0-9a-f]*'),
            created_at TEXT NOT NULL,
            UNIQUE(run_id, replay_manifest_id)
        ) STRICT;
        CREATE TABLE analysis_run_outputs(
            run_output_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            payload_schema_id TEXT NOT NULL,
            payload_schema_version TEXT NOT NULL,
            revision INTEGER NOT NULL CHECK(revision > 0),
            output_state TEXT NOT NULL CHECK(output_state IN (
                'present','partial','unavailable','failed')),
            output_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            output_sha256 TEXT NOT NULL CHECK(
                length(output_sha256) = 64
                AND output_sha256 NOT GLOB '*[^0-9a-f]*'),
            byte_length INTEGER NOT NULL CHECK(byte_length >= 0),
            provenance_id TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            replay_manifest_id TEXT
                REFERENCES analysis_replay_manifests(replay_manifest_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, revision),
            CHECK((output_state = 'present') = (output_payload_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE effect_receipts(
            effect_receipt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            effect_class TEXT NOT NULL CHECK(effect_class IN (
                'database','payload-store','staging','trace','run-output')),
            effect_state TEXT NOT NULL CHECK(effect_state IN (
                'admitted','reconciled','missing','orphaned','invalid','not-used')),
            object_id TEXT NOT NULL,
            receipt_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_occurrence_details(
            finding_occurrence_id TEXT PRIMARY KEY
                REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            candidate_id TEXT NOT NULL REFERENCES analysis_candidates(candidate_id) ON DELETE RESTRICT,
            confidence TEXT NOT NULL CHECK(confidence IN (
                'confirmed','strongly-supported','plausible')),
            severity TEXT NOT NULL CHECK(severity IN (
                'advisory','minor','moderate','major','blocker')),
            finding_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE case_occurrence_details(
            case_occurrence_id TEXT PRIMARY KEY
                REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            case_kind TEXT NOT NULL CHECK(case_kind IN ('supported','lead-only')),
            affects_readiness INTEGER NOT NULL CHECK(affects_readiness IN (0,1)),
            case_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((case_kind = 'supported') OR affects_readiness = 0)
        ) STRICT;
        CREATE TABLE reconciliation_details(
            reconciliation_assessment_id TEXT PRIMARY KEY
                REFERENCES reconciliation_assessments(reconciliation_assessment_id) ON DELETE RESTRICT,
            mechanism TEXT NOT NULL CHECK(mechanism IN ('automatic','reviewed','not-evaluated')),
            causal_gate TEXT NOT NULL CHECK(causal_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            applicability_gate TEXT NOT NULL CHECK(applicability_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            dependency_gate TEXT NOT NULL CHECK(dependency_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            producer_gate TEXT NOT NULL CHECK(producer_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            gap_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            considered_occurrences_payload_id TEXT NOT NULL
                REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE lineage_details(
            lineage_event_id TEXT PRIMARY KEY
                REFERENCES lineage_events(lineage_event_id) ON DELETE RESTRICT,
            lineage_kind TEXT NOT NULL CHECK(lineage_kind IN (
                'supersedes','analytical-revision','related-follow-up','promotes-lead',
                'merge-successor','split-successor','correction-successor')),
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE INDEX idx_documentation_passages_revision
            ON documentation_passages(documentation_revision_id, utf8_byte_start);
        CREATE INDEX idx_documentation_imports_revision
            ON documentation_imports(documentation_revision_id, created_at);
        CREATE INDEX idx_documentation_application_bindings_run
            ON documentation_application_bindings(run_id, subject_type, subject_id);
        CREATE INDEX idx_documentation_deletion_receipts_revision
            ON documentation_deletion_receipts(documentation_revision_id, deleted_at);
        CREATE INDEX idx_evidence_revisions_passage
            ON evidence_revisions(documentation_passage_id, evidence_revision_id);
        CREATE INDEX idx_evidence_applications_run
            ON evidence_application_links(run_id, evidence_revision_id);
        CREATE INDEX idx_candidate_decisions_run_population
            ON candidate_decisions(run_id, population_id, disposition);
        CREATE INDEX idx_candidates_run_lane ON analysis_candidates(run_id, lane, candidate_id);
        CREATE INDEX idx_hypotheses_candidate ON analysis_hypotheses(candidate_id, hypothesis_id);
        CREATE INDEX idx_recommendations_finding
            ON analysis_recommendations(finding_occurrence_id, recommendation_id);
        CREATE INDEX idx_taxonomy_subject
            ON taxonomy_assignments(subject_kind, subject_id, taxonomy_version, axis);
        CREATE INDEX idx_coverage_run_population
            ON analysis_coverage(run_id, population_id, coverage_state);
        CREATE INDEX idx_gaps_run_population ON analysis_gaps(run_id, population_id, gap_state);
        CREATE INDEX idx_dependency_edges_from
            ON analysis_dependency_edges(from_kind, from_id, edge_kind);
        CREATE INDEX idx_dependency_edges_to
            ON analysis_dependency_edges(to_kind, to_id, edge_kind);
        CREATE INDEX idx_case_memberships_member
            ON case_memberships(member_kind, member_id, case_occurrence_id);
        CREATE INDEX idx_replay_manifests_run
            ON analysis_replay_manifests(run_id, replay_state);
        CREATE INDEX idx_run_outputs_run ON analysis_run_outputs(run_id, revision);
        CREATE INDEX idx_effect_receipts_run ON effect_receipts(run_id, effect_class, effect_state);
        """;

    private const string SchemaV3 =
        """
        CREATE TABLE run_operations(
            run_id TEXT PRIMARY KEY REFERENCES runs(run_id) ON DELETE RESTRICT,
            operation_kind TEXT NOT NULL,
            request_json TEXT NOT NULL,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256) = 64),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER run_operations_immutable
        BEFORE UPDATE ON run_operations
        BEGIN SELECT RAISE(ABORT, 'run operations are immutable'); END;
        """;

    private const string SchemaV2 =
        """
        CREATE TABLE snapshot_capture_operations(
            operation_id TEXT PRIMARY KEY,
            durable_command_id TEXT NOT NULL UNIQUE,
            request_json TEXT NOT NULL,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256) = 64),
            initiation_kind TEXT NOT NULL,
            dispatch_deadline TEXT NOT NULL,
            lifecycle_state TEXT NOT NULL
                CHECK(lifecycle_state IN ('Queued','Running','Completed','Failed')),
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            installation_snapshot_id TEXT,
            payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            CHECK (
                (lifecycle_state = 'Completed'
                 AND installation_snapshot_id IS NOT NULL
                 AND payload_id IS NOT NULL)
                OR
                (lifecycle_state <> 'Completed'
                 AND installation_snapshot_id IS NULL
                 AND payload_id IS NULL)
            )
        ) STRICT;
        CREATE TRIGGER snapshot_capture_request_immutable
        BEFORE UPDATE OF durable_command_id, request_json, request_sha256,
                         initiation_kind, dispatch_deadline
        ON snapshot_capture_operations
        BEGIN SELECT RAISE(ABORT, 'snapshot capture requests are immutable'); END;
        CREATE INDEX idx_snapshot_capture_dispatch
            ON snapshot_capture_operations(lifecycle_state, created_at, operation_id);
        CREATE TABLE snapshot_capture_attempts(
            attempt_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL
                REFERENCES snapshot_capture_operations(operation_id) ON DELETE RESTRICT,
            attempt_generation INTEGER NOT NULL CHECK(attempt_generation > 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            lease_acquired_at TEXT NOT NULL,
            lease_expires_at TEXT NOT NULL,
            outcome TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(operation_id, attempt_generation),
            UNIQUE(operation_id, attempt_fencing_token),
            CHECK(lease_expires_at > lease_acquired_at)
        ) STRICT;
        CREATE UNIQUE INDEX idx_snapshot_capture_one_live_attempt
            ON snapshot_capture_attempts(operation_id)
            WHERE outcome = 'running';
        CREATE TABLE snapshot_capture_publications(
            receipt_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL UNIQUE
                REFERENCES snapshot_capture_operations(operation_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL UNIQUE
                REFERENCES snapshot_capture_attempts(attempt_id) ON DELETE RESTRICT,
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            staged_manifest_sha256 TEXT NOT NULL CHECK(length(staged_manifest_sha256) = 64),
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            installation_snapshot_id TEXT NOT NULL UNIQUE,
            published_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER snapshot_capture_publications_append_only_update
        BEFORE UPDATE ON snapshot_capture_publications
        BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER snapshot_capture_publications_append_only_delete
        BEFORE DELETE ON snapshot_capture_publications
        BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        """;

    private const string SchemaV1 =
        """
        CREATE TABLE store_metadata(
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        ) STRICT;
        CREATE TABLE migration_history(
            migration_id TEXT PRIMARY KEY,
            from_version INTEGER NOT NULL,
            to_version INTEGER NOT NULL UNIQUE,
            applied_at TEXT NOT NULL,
            sqlite_source_id TEXT NOT NULL
        ) STRICT;
        CREATE TABLE coordinator_leases(
            coordinator_instance_id TEXT NOT NULL,
            fencing_epoch INTEGER PRIMARY KEY CHECK(fencing_epoch > 0),
            acquired_at TEXT NOT NULL,
            expires_at TEXT NOT NULL,
            CHECK(expires_at > acquired_at)
        ) STRICT;
        CREATE TABLE runs(
            run_id TEXT PRIMARY KEY,
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            effective_scan_configuration_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            lifecycle_state TEXT NOT NULL,
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            durable_sequence INTEGER NOT NULL CHECK(durable_sequence > 0),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER runs_immutable_binding
        BEFORE UPDATE OF installation_snapshot_id, analysis_context_id,
                         effective_scan_configuration_id, resolved_input_manifest_id
        ON runs
        BEGIN SELECT RAISE(ABORT, 'run bindings are immutable'); END;
        CREATE TABLE job_nodes(
            job_node_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            parent_job_node_id TEXT REFERENCES job_nodes(job_node_id) ON DELETE RESTRICT,
            node_kind TEXT NOT NULL,
            lifecycle_state TEXT NOT NULL,
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE lifecycle_events(
            transition_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            job_node_id TEXT NOT NULL REFERENCES job_nodes(job_node_id) ON DELETE RESTRICT,
            record_kind TEXT NOT NULL CHECK(record_kind IN ('requested','observed')),
            policy_version TEXT NOT NULL,
            from_state TEXT NOT NULL,
            to_state TEXT NOT NULL,
            expected_generation INTEGER NOT NULL CHECK(expected_generation >= 0),
            new_generation INTEGER NOT NULL CHECK(new_generation = expected_generation + 1),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            reason TEXT NOT NULL,
            occurred_at TEXT NOT NULL,
            durable_sequence INTEGER NOT NULL CHECK(durable_sequence > 0),
            UNIQUE(run_id, durable_sequence)
        ) STRICT;
        CREATE TABLE durable_commands(
            command_id TEXT PRIMARY KEY,
            command_kind TEXT NOT NULL,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            expected_generation INTEGER NOT NULL CHECK(expected_generation >= 0),
            disposition TEXT NOT NULL,
            resulting_state TEXT NOT NULL,
            transition_id TEXT REFERENCES lifecycle_events(transition_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            start_initiation_kind TEXT,
            start_dispatch_deadline TEXT,
            CHECK (
                (command_kind = 'start'
                 AND ((start_initiation_kind IS NULL AND start_dispatch_deadline IS NULL)
                      OR (start_initiation_kind IS NOT NULL
                          AND start_dispatch_deadline IS NOT NULL)))
                OR
                (command_kind <> 'start'
                 AND start_initiation_kind IS NULL
                 AND start_dispatch_deadline IS NULL)
            )
        ) STRICT;
        CREATE TABLE attempts(
            attempt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            job_node_id TEXT NOT NULL REFERENCES job_nodes(job_node_id) ON DELETE RESTRICT,
            attempt_generation INTEGER NOT NULL CHECK(attempt_generation > 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            lease_acquired_at TEXT NOT NULL,
            lease_expires_at TEXT NOT NULL,
            dispatch_identity TEXT NOT NULL UNIQUE,
            idempotency_identity TEXT NOT NULL UNIQUE,
            retry_safety TEXT NOT NULL,
            outcome TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, attempt_generation),
            UNIQUE(run_id, attempt_fencing_token),
            CHECK(lease_expires_at > lease_acquired_at)
        ) STRICT;
        CREATE TABLE checkpoints(
            checkpoint_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL REFERENCES attempts(attempt_id) ON DELETE RESTRICT,
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            effective_scan_configuration_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            content_sha256 TEXT NOT NULL CHECK(length(content_sha256) = 64),
            completed_partitions_json TEXT NOT NULL,
            pending_and_gaps_json TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE payloads(
            payload_id TEXT PRIMARY KEY,
            content_sha256 TEXT NOT NULL UNIQUE CHECK(length(content_sha256) = 64),
            byte_length INTEGER NOT NULL CHECK(byte_length >= 0),
            codec TEXT NOT NULL,
            retention_state TEXT NOT NULL,
            object_relative_path TEXT NOT NULL UNIQUE,
            admitted_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE payload_owners(
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            owner_kind TEXT NOT NULL,
            owner_id TEXT NOT NULL,
            PRIMARY KEY(payload_id, owner_kind, owner_id)
        ) STRICT;
        CREATE TABLE publication_receipts(
            receipt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL UNIQUE REFERENCES attempts(attempt_id) ON DELETE RESTRICT,
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            staged_manifest_sha256 TEXT NOT NULL CHECK(length(staged_manifest_sha256) = 64),
            published_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE publication_receipt_payloads(
            receipt_id TEXT NOT NULL REFERENCES publication_receipts(receipt_id) ON DELETE RESTRICT,
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            PRIMARY KEY(receipt_id, payload_id)
        ) STRICT;
        CREATE TABLE logical_findings(
            logical_finding_id TEXT PRIMARY KEY,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_occurrences(
            finding_occurrence_id TEXT PRIMARY KEY,
            logical_finding_id TEXT NOT NULL REFERENCES logical_findings(logical_finding_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            analyzer_family TEXT NOT NULL,
            semantic_contract_version TEXT NOT NULL,
            identity_contract_version TEXT NOT NULL,
            identity_envelope_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            canonical_signature TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, finding_occurrence_id)
        ) STRICT;
        CREATE TABLE logical_cases(
            logical_case_id TEXT PRIMARY KEY,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE case_occurrences(
            case_occurrence_id TEXT PRIMARY KEY,
            logical_case_id TEXT NOT NULL REFERENCES logical_cases(logical_case_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            identity_envelope_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            shared_cause_signature TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE reconciliation_assessments(
            reconciliation_assessment_id TEXT PRIMARY KEY,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('finding','case')),
            predecessor_occurrence_id TEXT,
            successor_occurrence_id TEXT,
            causal_gate TEXT NOT NULL,
            applicability_gate TEXT NOT NULL,
            dependency_gate TEXT NOT NULL,
            producer_compatibility_gate TEXT NOT NULL,
            outcome TEXT NOT NULL CHECK(outcome IN (
                'exact-continuation','analytical-revision','related-follow-up','new-distinct',
                'ambiguous','unknown','not-observed','not-evaluated')),
            proof_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            policy_version TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE lineage_events(
            lineage_event_id TEXT PRIMARY KEY,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('finding','case')),
            event_kind TEXT NOT NULL CHECK(event_kind IN (
                'continuation','revision','follow-up','merge','split','supersession',
                'promotion','correction')),
            predecessor_logical_id TEXT,
            successor_logical_id TEXT NOT NULL,
            reconciliation_assessment_id TEXT REFERENCES reconciliation_assessments(reconciliation_assessment_id)
                ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE audit_events(
            audit_event_id TEXT PRIMARY KEY,
            event_kind TEXT NOT NULL,
            object_kind TEXT NOT NULL,
            object_id TEXT NOT NULL,
            detail_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            occurred_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE run_projection(
            run_id TEXT PRIMARY KEY REFERENCES runs(run_id) ON DELETE CASCADE,
            lifecycle_state TEXT NOT NULL,
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            durable_sequence INTEGER NOT NULL CHECK(durable_sequence > 0),
            projection_version INTEGER NOT NULL CHECK(projection_version > 0),
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER lifecycle_events_append_only_update
        BEFORE UPDATE ON lifecycle_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER lifecycle_events_append_only_delete
        BEFORE DELETE ON lifecycle_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER reconciliation_append_only_update
        BEFORE UPDATE ON reconciliation_assessments BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER reconciliation_append_only_delete
        BEFORE DELETE ON reconciliation_assessments BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER lineage_append_only_update
        BEFORE UPDATE ON lineage_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER lineage_append_only_delete
        BEFORE DELETE ON lineage_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER audit_events_append_only_update
        BEFORE UPDATE ON audit_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER audit_events_append_only_delete
        BEFORE DELETE ON audit_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER durable_commands_append_only_update
        BEFORE UPDATE ON durable_commands BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER durable_commands_append_only_delete
        BEFORE DELETE ON durable_commands BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER checkpoints_append_only_update
        BEFORE UPDATE ON checkpoints BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER checkpoints_append_only_delete
        BEFORE DELETE ON checkpoints BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER publication_receipts_append_only_update
        BEFORE UPDATE ON publication_receipts BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER publication_receipts_append_only_delete
        BEFORE DELETE ON publication_receipts BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER finding_occurrences_append_only_update
        BEFORE UPDATE ON finding_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER finding_occurrences_append_only_delete
        BEFORE DELETE ON finding_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER case_occurrences_append_only_update
        BEFORE UPDATE ON case_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER case_occurrences_append_only_delete
        BEFORE DELETE ON case_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE INDEX idx_runs_created ON runs(created_at, run_id);
        CREATE INDEX idx_runs_dispatch ON runs(lifecycle_state, created_at, run_id);
        CREATE INDEX idx_events_run_sequence ON lifecycle_events(run_id, durable_sequence);
        CREATE INDEX idx_attempts_run ON attempts(run_id, attempt_generation);
        CREATE UNIQUE INDEX idx_attempts_one_live_per_run
        ON attempts(run_id) WHERE outcome = 'running';
        CREATE INDEX idx_findings_signature ON finding_occurrences(
            analyzer_family, identity_contract_version, canonical_signature);
        CREATE INDEX idx_reconciliation_successor ON reconciliation_assessments(
            subject_kind, successor_occurrence_id);
        CREATE INDEX idx_lineage_successor ON lineage_events(subject_kind, successor_logical_id);
        """;
}

#pragma warning restore CA1869
#pragma warning restore CA1512
#pragma warning restore IDE0008
