using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public static class TargetedVerificationPersistenceDeclarations
{
    public const int SchemaVersion = 16;
    public const string StorageContractVersion = "1.15.0";
    public const string MigrationId = "targeted-verification-preparation-0016";
    public const string SourceSchemaFingerprint = ResultsPublicationPersistenceDeclarations.SchemaFingerprint;
    public const string SchemaFingerprint = "727285fbdb9a4a91e850a6bfad3749262be75e6388eae14edf9954eed23d783c";
}

public sealed partial class AuthoritativeStore
{
    private const string TargetedVerificationSchema =
        """
        CREATE TABLE targeted_preparation_requests(
            preparation_id TEXT PRIMARY KEY,
            durable_command_id TEXT NOT NULL UNIQUE,
            user_gesture_id TEXT NOT NULL UNIQUE,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),
            request_json TEXT NOT NULL,
            source_run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            source_occurrence_kind TEXT NOT NULL CHECK(source_occurrence_kind IN ('finding','case')),
            source_occurrence_id TEXT NOT NULL,
            confirmed_profile_id TEXT NOT NULL,
            confirmed_profile_revision INTEGER NOT NULL CHECK(confirmed_profile_revision>0),
            saved_configuration_id TEXT NOT NULL,
            saved_configuration_revision INTEGER NOT NULL CHECK(saved_configuration_revision>0),
            analysis_context_id TEXT NOT NULL,
            analysis_context_revision INTEGER NOT NULL CHECK(analysis_context_revision>0),
            analysis_context_fingerprint TEXT NOT NULL CHECK(length(analysis_context_fingerprint)=64),
            capture_operation_id TEXT NOT NULL UNIQUE REFERENCES snapshot_capture_operations(operation_id) ON DELETE RESTRICT,
            initiation_kind TEXT NOT NULL,
            dispatch_deadline TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE INDEX idx_targeted_preparation_source
            ON targeted_preparation_requests(source_run_id,source_occurrence_kind,source_occurrence_id);

        CREATE TABLE targeted_preparation_events(
            event_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            revision INTEGER NOT NULL CHECK(revision>0),
            event_kind TEXT NOT NULL,
            event_sha256 TEXT NOT NULL CHECK(length(event_sha256)=64),
            event_json TEXT NOT NULL,
            projection_json TEXT NOT NULL CHECK(json_valid(projection_json)),
            created_at TEXT NOT NULL,
            UNIQUE(preparation_id,revision)
        ) STRICT;
        CREATE TABLE targeted_preparation_commands(
            command_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            command_kind TEXT NOT NULL CHECK(command_kind='cancel'),
            expected_revision INTEGER NOT NULL CHECK(expected_revision>0),
            user_gesture_id TEXT NOT NULL UNIQUE,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),
            resulting_revision INTEGER NOT NULL CHECK(resulting_revision>0),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE targeted_preparation_projection(
            preparation_id TEXT PRIMARY KEY REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            revision INTEGER NOT NULL CHECK(revision>0),
            lifecycle_state TEXT NOT NULL CHECK(lifecycle_state IN (
                'Queued','CapturingSnapshot','AcquiringEvidence','PreparingPlan','Ready','ReadyWithGaps',
                'Cancelling','Cancelled','Invalidated','Failed','Started')),
            preparation_fingerprint TEXT NOT NULL CHECK(length(preparation_fingerprint)=64),
            terminal_reason TEXT NOT NULL,
            capture_operation_id TEXT REFERENCES snapshot_capture_operations(operation_id) ON DELETE RESTRICT,
            target_snapshot_id TEXT,
            evidence_acquisition_id TEXT,
            plan_id TEXT,
            plan_fingerprint TEXT CHECK(plan_fingerprint IS NULL OR length(plan_fingerprint)=64),
            startable INTEGER NOT NULL CHECK(startable IN (0,1)),
            limited INTEGER NOT NULL CHECK(limited IN (0,1)),
            last_event_id TEXT NOT NULL REFERENCES targeted_preparation_events(event_id) ON DELETE RESTRICT,
            updated_at TEXT NOT NULL
        ) STRICT;

        CREATE TABLE targeted_snapshot_links(
            link_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            capture_operation_id TEXT NOT NULL REFERENCES snapshot_capture_operations(operation_id) ON DELETE RESTRICT,
            target_snapshot_id TEXT NOT NULL,
            snapshot_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            snapshot_fingerprint TEXT NOT NULL CHECK(length(snapshot_fingerprint)=64),
            source_structural_fingerprint TEXT NOT NULL CHECK(length(source_structural_fingerprint)=64),
            target_structural_fingerprint TEXT NOT NULL CHECK(length(target_structural_fingerprint)=64),
            structural_comparison TEXT NOT NULL CHECK(structural_comparison IN ('equivalent','changed')),
            confirmed_profile_revision INTEGER NOT NULL CHECK(confirmed_profile_revision>0),
            created_at TEXT NOT NULL,
            UNIQUE(preparation_id,target_snapshot_id)
        ) STRICT;

        CREATE TABLE semantic_acquisition_runs(
            acquisition_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL UNIQUE REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            target_snapshot_id TEXT NOT NULL,
            operation_kind TEXT NOT NULL CHECK(operation_kind='bethesda-semantic-extraction'),
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),
            request_json TEXT NOT NULL,
            producer_id TEXT NOT NULL,
            producer_version TEXT NOT NULL,
            support_manifest_id TEXT NOT NULL,
            enumeration_policy_id TEXT NOT NULL,
            enumeration_policy_version TEXT NOT NULL,
            sealed_input_fingerprint TEXT NOT NULL CHECK(length(sealed_input_fingerprint)=64),
            dispatch_deadline TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE semantic_acquisition_jobs(
            job_id TEXT PRIMARY KEY,
            acquisition_id TEXT NOT NULL UNIQUE REFERENCES semantic_acquisition_runs(acquisition_id) ON DELETE RESTRICT,
            job_kind TEXT NOT NULL CHECK(job_kind='semantic-extraction-root'),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE semantic_acquisition_commands(
            command_id TEXT PRIMARY KEY,
            acquisition_id TEXT NOT NULL REFERENCES semantic_acquisition_runs(acquisition_id) ON DELETE RESTRICT,
            command_kind TEXT NOT NULL CHECK(command_kind IN ('start','cancel','retry','recover','delete-preview')),
            expected_generation INTEGER NOT NULL CHECK(expected_generation>=0),
            user_gesture_id TEXT,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256)=64),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE semantic_acquisition_events(
            event_id TEXT PRIMARY KEY,
            acquisition_id TEXT NOT NULL REFERENCES semantic_acquisition_runs(acquisition_id) ON DELETE RESTRICT,
            sequence INTEGER NOT NULL CHECK(sequence>0),
            event_kind TEXT NOT NULL,
            generation INTEGER NOT NULL CHECK(generation>=0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch>0),
            event_json TEXT NOT NULL,
            projection_json TEXT NOT NULL CHECK(json_valid(projection_json)),
            created_at TEXT NOT NULL,
            UNIQUE(acquisition_id,sequence)
        ) STRICT;
        CREATE TABLE semantic_acquisition_projection(
            acquisition_id TEXT PRIMARY KEY REFERENCES semantic_acquisition_runs(acquisition_id) ON DELETE RESTRICT,
            lifecycle_state TEXT NOT NULL CHECK(lifecycle_state IN (
                'Queued','Running','Cancelling','Cancelled','Retrying','Completed','CompletedWithGaps','Failed','Invalidated')),
            generation INTEGER NOT NULL CHECK(generation>=0),
            durable_sequence INTEGER NOT NULL CHECK(durable_sequence>0),
            progress_completed INTEGER NOT NULL CHECK(progress_completed>=0),
            progress_denominator INTEGER NOT NULL CHECK(progress_denominator>=0),
            active_attempt_id TEXT,
            terminal_reason TEXT NOT NULL,
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE semantic_acquisition_attempts(
            attempt_id TEXT PRIMARY KEY,
            acquisition_id TEXT NOT NULL REFERENCES semantic_acquisition_runs(acquisition_id) ON DELETE RESTRICT,
            attempt_generation INTEGER NOT NULL CHECK(attempt_generation>0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch>0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token>0),
            sealed_input_fingerprint TEXT NOT NULL CHECK(length(sealed_input_fingerprint)=64),
            lease_expires_at TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(acquisition_id,attempt_generation),
            UNIQUE(acquisition_id,attempt_fencing_token)
        ) STRICT;
        CREATE TABLE semantic_acquisition_checkpoints(
            checkpoint_id TEXT PRIMARY KEY,
            acquisition_id TEXT NOT NULL REFERENCES semantic_acquisition_runs(acquisition_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL REFERENCES semantic_acquisition_attempts(attempt_id) ON DELETE RESTRICT,
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token>0),
            target_snapshot_id TEXT NOT NULL,
            sealed_input_fingerprint TEXT NOT NULL CHECK(length(sealed_input_fingerprint)=64),
            checkpoint_sha256 TEXT NOT NULL CHECK(length(checkpoint_sha256)=64),
            checkpoint_json TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE semantic_acquisition_progress(
            progress_id TEXT PRIMARY KEY,
            acquisition_id TEXT NOT NULL REFERENCES semantic_acquisition_runs(acquisition_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL REFERENCES semantic_acquisition_attempts(attempt_id) ON DELETE RESTRICT,
            completed INTEGER NOT NULL CHECK(completed>=0),
            denominator INTEGER NOT NULL CHECK(denominator>=0 AND completed<=denominator),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE semantic_acquisition_publications(
            publication_id TEXT PRIMARY KEY,
            acquisition_id TEXT NOT NULL UNIQUE REFERENCES semantic_acquisition_runs(acquisition_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL REFERENCES semantic_acquisition_attempts(attempt_id) ON DELETE RESTRICT,
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token>0),
            target_snapshot_id TEXT NOT NULL,
            semantic_output_id TEXT NOT NULL UNIQUE,
            payload_id TEXT NOT NULL UNIQUE REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            payload_sha256 TEXT NOT NULL CHECK(length(payload_sha256)=64),
            payload_byte_length INTEGER NOT NULL CHECK(payload_byte_length>0),
            staged_manifest_sha256 TEXT NOT NULL CHECK(length(staged_manifest_sha256)=64),
            provenance_json TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE semantic_acquisition_application_links(
            link_id TEXT PRIMARY KEY,
            acquisition_id TEXT NOT NULL REFERENCES semantic_acquisition_runs(acquisition_id) ON DELETE RESTRICT,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            successor_run_id TEXT REFERENCES runs(run_id) ON DELETE RESTRICT,
            semantic_output_id TEXT NOT NULL,
            use_kind TEXT NOT NULL CHECK(use_kind IN ('preparation-prerequisite','successor-input')),
            created_at TEXT NOT NULL,
            UNIQUE(acquisition_id,use_kind,successor_run_id)
        ) STRICT;

        CREATE TABLE targeted_scope_roots(
            root_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            member_id TEXT NOT NULL,
            root_json TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(preparation_id,member_id)
        ) STRICT;
        CREATE TABLE targeted_scope_members(
            member_row_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            scope_id TEXT NOT NULL,
            member_id TEXT NOT NULL,
            member_kind TEXT NOT NULL,
            stable_identity TEXT NOT NULL,
            mandatory INTEGER NOT NULL CHECK(mandatory IN (0,1)),
            member_json TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(preparation_id,member_id)
        ) STRICT;
        CREATE TABLE targeted_scope_dependencies(
            edge_row_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            scope_id TEXT NOT NULL,
            edge_id TEXT NOT NULL,
            from_member_id TEXT NOT NULL,
            to_member_id TEXT NOT NULL,
            relation TEXT NOT NULL,
            edge_json TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(preparation_id,edge_id)
        ) STRICT;
        CREATE TABLE targeted_correlation_rows(
            row_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            coverage_id TEXT NOT NULL,
            scope_member_id TEXT NOT NULL,
            status TEXT NOT NULL CHECK(status IN (
                'MatchedExecutable','ChangedCorrelated','ProvenAbsent','ProvenNotApplicable','Ambiguous',
                'Unsupported','Inaccessible','Malformed','MissingRequiredProof')),
            correlation_qualified INTEGER NOT NULL CHECK(correlation_qualified IN (0,1)),
            processing_qualified INTEGER NOT NULL CHECK(processing_qualified IN (0,1)),
            row_json TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(preparation_id,scope_member_id)
        ) STRICT;
        CREATE TABLE targeted_reuse_decisions(
            decision_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            artifact_kind TEXT NOT NULL,
            artifact_id TEXT NOT NULL,
            disposition TEXT NOT NULL CHECK(disposition IN ('recompute','reuse-with-proof')),
            proof_fingerprint TEXT NOT NULL CHECK(length(proof_fingerprint)=64),
            decision_json TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE targeted_verification_plans(
            plan_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL UNIQUE REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            plan_payload_id TEXT NOT NULL UNIQUE REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            plan_fingerprint TEXT NOT NULL UNIQUE CHECK(length(plan_fingerprint)=64),
            scope_id TEXT NOT NULL,
            scope_fingerprint TEXT NOT NULL CHECK(length(scope_fingerprint)=64),
            coverage_id TEXT NOT NULL,
            coverage_fingerprint TEXT NOT NULL CHECK(length(coverage_fingerprint)=64),
            resolved_manifest_id TEXT NOT NULL,
            startable INTEGER NOT NULL CHECK(startable IN (0,1)),
            limited INTEGER NOT NULL CHECK(limited IN (0,1)),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE targeted_operation_inputs(
            input_row_id TEXT PRIMARY KEY,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            input_kind TEXT NOT NULL,
            input_id TEXT NOT NULL,
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            input_sha256 TEXT NOT NULL CHECK(length(input_sha256)=64),
            created_at TEXT NOT NULL,
            UNIQUE(preparation_id,input_kind,input_id)
        ) STRICT;
        CREATE TABLE targeted_start_admissions(
            admission_id TEXT PRIMARY KEY,
            targeted_verification_id TEXT NOT NULL UNIQUE,
            preparation_id TEXT NOT NULL UNIQUE REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            command_id TEXT NOT NULL UNIQUE REFERENCES durable_commands(command_id) ON DELETE RESTRICT,
            user_gesture_id TEXT NOT NULL UNIQUE,
            start_request_sha256 TEXT NOT NULL CHECK(length(start_request_sha256)=64),
            submission_fingerprint TEXT NOT NULL CHECK(length(submission_fingerprint)=64),
            successor_run_id TEXT NOT NULL UNIQUE REFERENCES runs(run_id) ON DELETE RESTRICT,
            managed_operation_kind TEXT NOT NULL CHECK(managed_operation_kind='managed-analysis-v1'),
            managed_operation_fingerprint TEXT NOT NULL CHECK(length(managed_operation_fingerprint)=64),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE targeted_initiation_lineage(
            lineage_id TEXT PRIMARY KEY,
            targeted_verification_id TEXT NOT NULL UNIQUE REFERENCES targeted_start_admissions(targeted_verification_id) ON DELETE RESTRICT,
            source_run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            source_occurrence_id TEXT NOT NULL,
            successor_run_id TEXT NOT NULL UNIQUE REFERENCES runs(run_id) ON DELETE RESTRICT,
            preparation_id TEXT NOT NULL REFERENCES targeted_preparation_requests(preparation_id) ON DELETE RESTRICT,
            target_snapshot_id TEXT NOT NULL,
            evidence_acquisition_id TEXT NOT NULL REFERENCES semantic_acquisition_runs(acquisition_id) ON DELETE RESTRICT,
            managed_operation_fingerprint TEXT NOT NULL CHECK(length(managed_operation_fingerprint)=64),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE targeted_result_links(
            link_id TEXT PRIMARY KEY,
            targeted_verification_id TEXT NOT NULL REFERENCES targeted_start_admissions(targeted_verification_id) ON DELETE RESTRICT,
            source_occurrence_id TEXT NOT NULL,
            successor_occurrence_id TEXT,
            relationship TEXT NOT NULL CHECK(relationship IN (
                'Exact','Revision','Related','Ambiguous','Distinct','NotObserved','NotEvaluated')),
            assessment_id TEXT,
            proof_json TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;

        CREATE TRIGGER targeted_preparation_requests_append_only_update BEFORE UPDATE ON targeted_preparation_requests BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_preparation_requests_append_only_delete BEFORE DELETE ON targeted_preparation_requests BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_preparation_events_append_only_update BEFORE UPDATE ON targeted_preparation_events BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_preparation_events_append_only_delete BEFORE DELETE ON targeted_preparation_events BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_preparation_commands_append_only_update BEFORE UPDATE ON targeted_preparation_commands BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_preparation_commands_append_only_delete BEFORE DELETE ON targeted_preparation_commands BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_snapshot_links_append_only_update BEFORE UPDATE ON targeted_snapshot_links BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_snapshot_links_append_only_delete BEFORE DELETE ON targeted_snapshot_links BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_runs_append_only_update BEFORE UPDATE ON semantic_acquisition_runs BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_runs_append_only_delete BEFORE DELETE ON semantic_acquisition_runs BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_jobs_append_only_update BEFORE UPDATE ON semantic_acquisition_jobs BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_jobs_append_only_delete BEFORE DELETE ON semantic_acquisition_jobs BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_commands_append_only_update BEFORE UPDATE ON semantic_acquisition_commands BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_commands_append_only_delete BEFORE DELETE ON semantic_acquisition_commands BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_events_append_only_update BEFORE UPDATE ON semantic_acquisition_events BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_events_append_only_delete BEFORE DELETE ON semantic_acquisition_events BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_attempts_append_only_update BEFORE UPDATE ON semantic_acquisition_attempts BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_attempts_append_only_delete BEFORE DELETE ON semantic_acquisition_attempts BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_checkpoints_append_only_update BEFORE UPDATE ON semantic_acquisition_checkpoints BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_checkpoints_append_only_delete BEFORE DELETE ON semantic_acquisition_checkpoints BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_progress_append_only_update BEFORE UPDATE ON semantic_acquisition_progress BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_progress_append_only_delete BEFORE DELETE ON semantic_acquisition_progress BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_publications_append_only_update BEFORE UPDATE ON semantic_acquisition_publications BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_publications_append_only_delete BEFORE DELETE ON semantic_acquisition_publications BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_application_links_append_only_update BEFORE UPDATE ON semantic_acquisition_application_links BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER semantic_acquisition_application_links_append_only_delete BEFORE DELETE ON semantic_acquisition_application_links BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_scope_roots_append_only_update BEFORE UPDATE ON targeted_scope_roots BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_scope_roots_append_only_delete BEFORE DELETE ON targeted_scope_roots BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_scope_members_append_only_update BEFORE UPDATE ON targeted_scope_members BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_scope_members_append_only_delete BEFORE DELETE ON targeted_scope_members BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_scope_dependencies_append_only_update BEFORE UPDATE ON targeted_scope_dependencies BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_scope_dependencies_append_only_delete BEFORE DELETE ON targeted_scope_dependencies BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_correlation_rows_append_only_update BEFORE UPDATE ON targeted_correlation_rows BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_correlation_rows_append_only_delete BEFORE DELETE ON targeted_correlation_rows BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_reuse_decisions_append_only_update BEFORE UPDATE ON targeted_reuse_decisions BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_reuse_decisions_append_only_delete BEFORE DELETE ON targeted_reuse_decisions BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_verification_plans_append_only_update BEFORE UPDATE ON targeted_verification_plans BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_verification_plans_append_only_delete BEFORE DELETE ON targeted_verification_plans BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_operation_inputs_append_only_update BEFORE UPDATE ON targeted_operation_inputs BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_operation_inputs_append_only_delete BEFORE DELETE ON targeted_operation_inputs BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_start_admissions_append_only_update BEFORE UPDATE ON targeted_start_admissions BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_start_admissions_append_only_delete BEFORE DELETE ON targeted_start_admissions BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_initiation_lineage_append_only_update BEFORE UPDATE ON targeted_initiation_lineage BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_initiation_lineage_append_only_delete BEFORE DELETE ON targeted_initiation_lineage BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_result_links_append_only_update BEFORE UPDATE ON targeted_result_links BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        CREATE TRIGGER targeted_result_links_append_only_delete BEFORE DELETE ON targeted_result_links BEGIN SELECT RAISE(ABORT,'append-only history'); END;
        """;

    private void ApplyTargetedVerificationMigration()
    {
        string source = ComputeSchemaFingerprint(connection);
        if (!StringComparer.Ordinal.Equals(source, TargetedVerificationPersistenceDeclarations.SourceSchemaFingerprint))
        {
            throw new InvalidOperationException(
                "The targeted-verification migration source does not match the accepted schema-15 contract.");
        }
        using SqliteCommand preflight = connection.CreateCommand();
        preflight.CommandText =
            "SELECT (SELECT COUNT(*) FROM targeted_verifications) + "
            + "(SELECT COUNT(*) FROM run_operations WHERE operation_kind='targeted-verification');";
        if (Convert.ToInt64(preflight.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 0)
        {
            throw new InvalidOperationException(
                "Legacy targeted-verification state is incompatible with the preparation-first contract.");
        }

        using SqliteTransaction transaction = BeginTransaction();
        Execute(TargetedVerificationSchema, transaction);
        string fingerprint = ComputeSchemaFingerprint(connection, transaction);
        if (!StringComparer.Ordinal.Equals(fingerprint, TargetedVerificationPersistenceDeclarations.SchemaFingerprint))
        {
            throw new InvalidOperationException(
                $"The targeted-verification schema bytes do not match the accepted schema-16 contract ({fingerprint}).");
        }
        Execute(
            """
            UPDATE store_metadata SET value='16' WHERE key='schema_version';
            UPDATE store_metadata SET value='1.15.0' WHERE key='storage_contract_version';
            UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
            INSERT INTO migration_history(migration_id,from_version,to_version,applied_at,sqlite_source_id)
            VALUES ($id,15,16,$now,$sqlite);
            PRAGMA user_version=16;
            """, transaction,
            ("$fingerprint", fingerprint),
            ("$id", TargetedVerificationPersistenceDeclarations.MigrationId),
            ("$now", ToText(DateTimeOffset.UtcNow)),
            ("$sqlite", BindingIdentity.SourceId));
        transaction.Commit();
    }

    private void ValidateTargetedVerificationMigration()
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM migration_history WHERE migration_id=$id AND from_version=15 AND to_version=16;";
        command.Parameters.AddWithValue("$id", TargetedVerificationPersistenceDeclarations.MigrationId);
        long count = Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        string fingerprint = ComputeSchemaFingerprint(connection);
        using SqliteCommand metadata = connection.CreateCommand();
        metadata.CommandText = "SELECT value FROM store_metadata WHERE key='schema_fingerprint';";
        string stored = (string)(metadata.ExecuteScalar()
            ?? throw new InvalidOperationException("The schema fingerprint metadata is missing."));
        if (count != 1 || !StringComparer.Ordinal.Equals(fingerprint, stored)
            || !StringComparer.Ordinal.Equals(fingerprint, TargetedVerificationPersistenceDeclarations.SchemaFingerprint))
        {
            throw new InvalidOperationException("The targeted-verification migration is incomplete or drifted.");
        }
    }
}
