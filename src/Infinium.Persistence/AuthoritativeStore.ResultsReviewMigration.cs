using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public static class ResultsReviewPersistenceDeclarations
{
    public const int SchemaVersion = 14;
    public const string StorageContractVersion = "1.13.0";
    public const string MigrationId = "results-review-workflow-0014";
    public const string SourceSchemaFingerprint = ApplicationSetupPersistenceDeclarations.SchemaFingerprint;
    public const string SchemaFingerprint = "ca3b9b41dde2ed93ea3f86cee9ece3bd4c28705e23da4a76794db6437d8968ba";
}

public sealed partial class AuthoritativeStore
{
    private const string ResultsReviewSchema =
        """
        CREATE TABLE result_projection_items(
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            item_id TEXT NOT NULL,
            logical_id TEXT NOT NULL,
            case_occurrence_id TEXT,
            item_kind TEXT NOT NULL CHECK(item_kind IN (
                'supported-case','lead-only-case','finding','abstention','failure','coverage-gap')),
            inert_summary TEXT NOT NULL,
            severity TEXT NOT NULL,
            confidence TEXT NOT NULL,
            analyzer_id TEXT NOT NULL,
            analyzer_version TEXT NOT NULL,
            subject_ids_json TEXT NOT NULL CHECK(json_valid(subject_ids_json)),
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            source_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            source_payload_sha256 TEXT NOT NULL CHECK(length(source_payload_sha256)=64),
            created_at TEXT NOT NULL,
            PRIMARY KEY(run_id,item_id)
        ) STRICT;
        CREATE INDEX idx_result_projection_queue
            ON result_projection_items(run_id,item_kind,item_id);
        CREATE INDEX idx_result_projection_severity
            ON result_projection_items(run_id,item_kind,severity,item_id);

        CREATE TABLE review_events(
            event_id TEXT PRIMARY KEY,
            idempotency_key TEXT NOT NULL UNIQUE,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('finding','case')),
            subject_occurrence_id TEXT NOT NULL,
            revision INTEGER NOT NULL CHECK(revision > 0),
            event_kind TEXT NOT NULL CHECK(event_kind IN (
                'disposition','suppression','annotation','remove-annotation','carryover')),
            disposition TEXT NOT NULL CHECK(disposition IN (
                'unreviewed','investigating','action-required','resolved','accepted-as-is',
                'not-applicable','false-positive')),
            suppressed INTEGER NOT NULL CHECK(suppressed IN (0,1)),
            annotation TEXT NOT NULL,
            source_event_id TEXT REFERENCES review_events(event_id) ON DELETE RESTRICT,
            continuity_assessment_id TEXT REFERENCES reconciliation_assessments(reconciliation_assessment_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(subject_occurrence_id,revision)
        ) STRICT;
        CREATE TABLE review_projection(
            subject_occurrence_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('finding','case')),
            revision INTEGER NOT NULL CHECK(revision > 0),
            disposition TEXT NOT NULL,
            suppressed INTEGER NOT NULL CHECK(suppressed IN (0,1)),
            annotation TEXT NOT NULL,
            last_event_id TEXT NOT NULL REFERENCES review_events(event_id) ON DELETE RESTRICT,
            updated_at TEXT NOT NULL
        ) STRICT;

        CREATE TABLE assumption_events(
            event_id TEXT PRIMARY KEY,
            idempotency_key TEXT NOT NULL UNIQUE,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),
            assumption_id TEXT NOT NULL,
            profile_id TEXT NOT NULL,
            revision INTEGER NOT NULL CHECK(revision > 0),
            event_kind TEXT NOT NULL CHECK(event_kind IN ('create','edit','confirm','remove','revalidate')),
            origin TEXT NOT NULL CHECK(origin IN ('inferred','user-provided')),
            confirmation TEXT NOT NULL CHECK(confirmation IN ('unconfirmed','user-confirmed')),
            subject TEXT NOT NULL,
            value TEXT NOT NULL,
            scope TEXT NOT NULL,
            dependency_ids_json TEXT NOT NULL CHECK(json_valid(dependency_ids_json)),
            effective INTEGER NOT NULL CHECK(effective IN (0,1)),
            analysis_context_id TEXT NOT NULL,
            predecessor_event_id TEXT REFERENCES assumption_events(event_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(assumption_id,revision)
        ) STRICT;
        CREATE TABLE assumption_projection(
            assumption_id TEXT PRIMARY KEY,
            profile_id TEXT NOT NULL,
            revision INTEGER NOT NULL CHECK(revision > 0),
            origin TEXT NOT NULL,
            confirmation TEXT NOT NULL,
            subject TEXT NOT NULL,
            value TEXT NOT NULL,
            scope TEXT NOT NULL,
            dependency_ids_json TEXT NOT NULL CHECK(json_valid(dependency_ids_json)),
            effective INTEGER NOT NULL CHECK(effective IN (0,1)),
            analysis_context_id TEXT NOT NULL,
            last_event_id TEXT NOT NULL REFERENCES assumption_events(event_id) ON DELETE RESTRICT,
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE INDEX idx_assumption_projection_profile
            ON assumption_projection(profile_id,assumption_id);

        CREATE TABLE targeted_verifications(
            verification_id TEXT PRIMARY KEY,
            idempotency_key TEXT NOT NULL UNIQUE,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),
            source_run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            successor_run_id TEXT NOT NULL UNIQUE REFERENCES runs(run_id) ON DELETE RESTRICT,
            source_finding_occurrence_id TEXT,
            source_case_occurrence_id TEXT,
            exact_scope_ids_json TEXT NOT NULL CHECK(json_valid(exact_scope_ids_json)),
            user_gesture_id TEXT NOT NULL,
            readiness_boundary TEXT NOT NULL CHECK(readiness_boundary IN ('scope-limited','no-readiness')),
            state TEXT NOT NULL CHECK(state IN ('manually-initiated','already-accepted')),
            created_at TEXT NOT NULL,
            CHECK((source_finding_occurrence_id IS NOT NULL) + (source_case_occurrence_id IS NOT NULL) >= 1)
        ) STRICT;

        CREATE TABLE structured_exports(
            export_id TEXT PRIMARY KEY,
            idempotency_key TEXT NOT NULL UNIQUE,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            sharing_class TEXT NOT NULL CHECK(sharing_class='LocalPrivateExport'),
            schema_identity TEXT NOT NULL,
            generator_identity TEXT NOT NULL,
            selection_manifest_json TEXT NOT NULL CHECK(json_valid(selection_manifest_json)),
            selection_manifest_sha256 TEXT NOT NULL CHECK(length(selection_manifest_sha256)=64),
            artifact_relative_path TEXT NOT NULL UNIQUE,
            artifact_sha256 TEXT NOT NULL CHECK(length(artifact_sha256)=64),
            artifact_bytes INTEGER NOT NULL CHECK(artifact_bytes > 0),
            created_at TEXT NOT NULL
        ) STRICT;

        CREATE TRIGGER review_events_append_only_update
        BEFORE UPDATE ON review_events BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER review_events_append_only_delete
        BEFORE DELETE ON review_events BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER assumption_events_append_only_update
        BEFORE UPDATE ON assumption_events BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER assumption_events_append_only_delete
        BEFORE DELETE ON assumption_events BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_verifications_append_only_update
        BEFORE UPDATE ON targeted_verifications BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_verifications_append_only_delete
        BEFORE DELETE ON targeted_verifications BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER structured_exports_append_only_update
        BEFORE UPDATE ON structured_exports BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER structured_exports_append_only_delete
        BEFORE DELETE ON structured_exports BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        """;

    private void ApplyResultsReviewMigration()
    {
        string source = ComputeSchemaFingerprint(connection);
        if (!StringComparer.Ordinal.Equals(source, ResultsReviewPersistenceDeclarations.SourceSchemaFingerprint))
        {
            throw new InvalidOperationException(
                "The results/review migration source does not match the accepted schema-13 contract.");
        }

        using SqliteTransaction transaction = BeginTransaction();
        Execute(ResultsReviewSchema, transaction);
        string fingerprint = ComputeSchemaFingerprint(connection, transaction);
        if (!StringComparer.Ordinal.Equals(fingerprint, ResultsReviewPersistenceDeclarations.SchemaFingerprint))
        {
            throw new InvalidOperationException(
                "The results/review schema bytes do not match the accepted schema-14 contract.");
        }
        Execute(
            """
            UPDATE store_metadata SET value='14' WHERE key='schema_version';
            UPDATE store_metadata SET value='1.13.0' WHERE key='storage_contract_version';
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            INSERT INTO migration_history(migration_id,from_version,to_version,applied_at,sqlite_source_id)
            VALUES ($id,13,14,$now,$sqlite);
            PRAGMA user_version=14;
            """,
            transaction,
            ("$fingerprint", fingerprint),
            ("$id", ResultsReviewPersistenceDeclarations.MigrationId),
            ("$now", ToText(DateTimeOffset.UtcNow)),
            ("$sqlite", BindingIdentity.SourceId));
        transaction.Commit();
    }

    private void ValidateResultsReviewMigration()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*) FROM migration_history
            WHERE migration_id=$id AND from_version=13 AND to_version=14;
            """;
        command.Parameters.AddWithValue("$id", ResultsReviewPersistenceDeclarations.MigrationId);
        long count = Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        string fingerprint = ComputeSchemaFingerprint(connection);
        using SqliteCommand metadata = connection.CreateCommand();
        metadata.CommandText = "SELECT value FROM store_metadata WHERE key='schema_fingerprint';";
        string stored = (string)(metadata.ExecuteScalar()
            ?? throw new InvalidOperationException("The schema fingerprint metadata is missing."));
        if (count != 1
            || !StringComparer.Ordinal.Equals(fingerprint, stored)
            || !StringComparer.Ordinal.Equals(fingerprint, ResultsReviewPersistenceDeclarations.SchemaFingerprint))
        {
            throw new InvalidOperationException("The results/review migration is incomplete or drifted.");
        }
    }
}
