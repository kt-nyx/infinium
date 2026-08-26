using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public static class ResultsPublicationPersistenceDeclarations
{
    public const int SchemaVersion = 15;
    public const string StorageContractVersion = "1.14.0";
    public const string MigrationId = "result-publication-and-export-deletion-0015";
    public const string SourceSchemaFingerprint = ResultsReviewPersistenceDeclarations.SchemaFingerprint;
    public const string SchemaFingerprint = "a64750491c8cd7e79d96e3190710b4b0c71c6377a83df2a5e25df0bc554f7b1f";
}

public sealed partial class AuthoritativeStore
{
    private const string ResultsPublicationSchema =
        """
        CREATE TABLE finding_report_publications(
            report_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            report_state TEXT NOT NULL CHECK(report_state IN (
                'supported-finding','resolved-negative','abstention','failure','limited','coverage-gap')),
            finding_occurrence_id TEXT,
            case_occurrence_id TEXT,
            subject_id TEXT NOT NULL,
            analyzer_id TEXT NOT NULL,
            inert_title TEXT NOT NULL,
            inert_conclusion TEXT NOT NULL,
            report_payload_id TEXT NOT NULL UNIQUE REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            report_payload_sha256 TEXT NOT NULL CHECK(length(report_payload_sha256)=64),
            source_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            source_payload_sha256 TEXT NOT NULL CHECK(length(source_payload_sha256)=64),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE INDEX idx_finding_report_queue
            ON finding_report_publications(run_id,report_state,report_id);

        CREATE TABLE structured_export_events(
            event_id TEXT PRIMARY KEY,
            idempotency_key TEXT NOT NULL UNIQUE,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),
            export_id TEXT NOT NULL REFERENCES structured_exports(export_id) ON DELETE RESTRICT,
            revision INTEGER NOT NULL CHECK(revision > 0),
            event_kind TEXT NOT NULL CHECK(event_kind IN ('created','deletion-requested','deleted')),
            created_at TEXT NOT NULL,
            UNIQUE(export_id,revision)
        ) STRICT;
        CREATE TABLE structured_export_projection(
            export_id TEXT PRIMARY KEY REFERENCES structured_exports(export_id) ON DELETE RESTRICT,
            revision INTEGER NOT NULL CHECK(revision > 0),
            state TEXT NOT NULL CHECK(state IN ('active','deletion-pending','deleted')),
            last_event_id TEXT NOT NULL REFERENCES structured_export_events(event_id) ON DELETE RESTRICT,
            deleted_at TEXT,
            updated_at TEXT NOT NULL
        ) STRICT;

        CREATE TRIGGER finding_report_publications_append_only_update
        BEFORE UPDATE ON finding_report_publications BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER finding_report_publications_append_only_delete
        BEFORE DELETE ON finding_report_publications BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER structured_export_events_append_only_update
        BEFORE UPDATE ON structured_export_events BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER structured_export_events_append_only_delete
        BEFORE DELETE ON structured_export_events BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        """;

    private void ApplyResultsPublicationMigration()
    {
        string source = ComputeSchemaFingerprint(connection);
        if (!StringComparer.Ordinal.Equals(source, ResultsPublicationPersistenceDeclarations.SourceSchemaFingerprint))
        {
            throw new InvalidOperationException(
                "The result-publication migration source does not match the accepted schema-14 contract.");
        }

        using SqliteTransaction transaction = BeginTransaction();
        Execute(ResultsPublicationSchema, transaction);
        Execute(
            """
            INSERT INTO structured_export_events(
                event_id,idempotency_key,request_sha256,export_id,revision,event_kind,created_at)
            SELECT export_id || '-created', 'created-' || export_id, request_sha256,
                   export_id, 1, 'created', created_at
            FROM structured_exports;
            INSERT INTO structured_export_projection(
                export_id,revision,state,last_event_id,deleted_at,updated_at)
            SELECT export_id,1,'active',export_id || '-created',NULL,created_at
            FROM structured_exports;
            """,
            transaction);
        string fingerprint = ComputeSchemaFingerprint(connection, transaction);
        if (!StringComparer.Ordinal.Equals(fingerprint, ResultsPublicationPersistenceDeclarations.SchemaFingerprint))
        {
            throw new InvalidOperationException(
                $"The result-publication schema bytes do not match the accepted schema-15 contract ({fingerprint}).");
        }
        Execute(
            """
            UPDATE store_metadata SET value='15' WHERE key='schema_version';
            UPDATE store_metadata SET value='1.14.0' WHERE key='storage_contract_version';
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            INSERT INTO migration_history(migration_id,from_version,to_version,applied_at,sqlite_source_id)
            VALUES ($id,14,15,$now,$sqlite);
            PRAGMA user_version=15;
            """,
            transaction,
            ("$fingerprint", fingerprint),
            ("$id", ResultsPublicationPersistenceDeclarations.MigrationId),
            ("$now", ToText(DateTimeOffset.UtcNow)),
            ("$sqlite", BindingIdentity.SourceId));
        transaction.Commit();
    }

    private void ValidateResultsPublicationMigration()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(*) FROM migration_history
            WHERE migration_id=$id AND from_version=14 AND to_version=15;
            """;
        command.Parameters.AddWithValue("$id", ResultsPublicationPersistenceDeclarations.MigrationId);
        long count = Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        string fingerprint = ComputeSchemaFingerprint(connection);
        using SqliteCommand metadata = connection.CreateCommand();
        metadata.CommandText = "SELECT value FROM store_metadata WHERE key='schema_fingerprint';";
        string stored = (string)(metadata.ExecuteScalar()
            ?? throw new InvalidOperationException("The schema fingerprint metadata is missing."));
        if (count != 1
            || !StringComparer.Ordinal.Equals(fingerprint, stored)
            || !StringComparer.Ordinal.Equals(fingerprint, ResultsPublicationPersistenceDeclarations.SchemaFingerprint))
        {
            throw new InvalidOperationException("The result-publication migration is incomplete or drifted.");
        }
    }
}
