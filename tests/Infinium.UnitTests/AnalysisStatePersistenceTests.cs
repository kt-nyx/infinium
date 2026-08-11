using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class AnalysisStatePersistenceTests
{
    private static readonly string[] AnalysisFindingCaseTables =
    [
        "finding_case_publications",
        "finding_promotion_assessments",
        "finding_case_abstentions",
        "finding_case_finding_details",
        "finding_case_recommendations",
        "case_hypothesis_memberships",
        "finding_case_case_details",
        "finding_case_taxonomy_assignments",
        "taxonomy_projection_edges",
        "finding_case_gap_details",
        "analysis_coverage_taxonomy_links",
        "analysis_coverage_gap_links",
        "analysis_coverage_failure_links",
        "reconciliation_metadata",
        "reconciliation_proof_links",
        "lineage_event_edges",
    ];

    private static readonly string[] ProviderSchema6TablesInCreationOrder =
    [
        "provider_access_profiles", "provider_generations", "provider_credential_intents", "provider_credential_intent_events",
        "provider_capability_snapshots", "provider_price_snapshots", "provider_price_rules", "evidence_acquisition_runs",
        "provider_effective_scan_configurations_v2", "evidence_acquisition_job_nodes", "evidence_acquisition_attempts",
        "evidence_acquisition_commands", "provider_command_bindings",
        "evidence_acquisition_parent_links", "evidence_acquisition_application_links",
        "provider_operation_blocks", "provider_operation_authorizations", "provider_operation_attempts", "provider_requests",
        "provider_reservations", "provider_reservation_scope_items", "provider_dispatch_fences",
        "provider_transport_events", "provider_responses", "provider_usage_entries", "provider_rate_limit_facts",
        "provider_response_finalizations",
        "provider_settlements", "provider_settlement_adjustments", "provider_semantic_proposals",
        "provider_semantic_validations", "provider_semantic_admissions", "provider_replay_edges",
        "provider_run_output_v2_bindings", "provider_operation_projection",
        "provider_profile_projection", "provider_budget_projection",
    ];

    private static readonly string[] AnalysisTables =
    [
        "analysis_candidates",
        "analysis_coverage",
        "analysis_coverage_failure_links",
        "analysis_coverage_gap_links",
        "analysis_coverage_taxonomy_links",
        "analysis_dependency_edges",
        "analysis_gaps",
        "analysis_hypotheses",
        "analysis_recommendations",
        "analysis_replay_manifests",
        "analysis_run_outputs",
        "candidate_decisions",
        "case_memberships",
        "case_hypothesis_memberships",
        "case_occurrence_details",
        "documentation_passages",
        "documentation_imports",
        "documentation_revisions",
        "documentation_application_bindings",
        "documentation_deletion_receipts",
        "documentation_purpose_assignment_details",
        "documentation_gap_details",
        "effect_receipts",
        "evidence_application_links",
        "evidence_revisions",
        "finding_occurrence_details",
        "finding_case_abstentions",
        "finding_case_case_details",
        "finding_case_finding_details",
        "finding_case_gap_details",
        "finding_case_publications",
        "finding_case_recommendations",
        "finding_case_taxonomy_assignments",
        "finding_promotion_assessments",
        "lineage_details",
        "lineage_event_edges",
        "payload_backup_pins",
        "reconciliation_details",
        "reconciliation_metadata",
        "reconciliation_proof_links",
        "taxonomy_assignments",
        "taxonomy_projection_edges",
    ];

    private static readonly string[] AnalysisIndexes =
    [
        "idx_candidate_decisions_run_population",
        "idx_candidates_run_lane",
        "idx_case_memberships_member",
        "idx_coverage_run_population",
        "idx_dependency_edges_from",
        "idx_dependency_edges_to",
        "idx_documentation_passages_revision",
        "idx_documentation_imports_revision",
        "idx_documentation_application_bindings_run",
        "idx_documentation_deletion_receipts_revision",
        "idx_effect_receipts_run",
        "idx_evidence_applications_run",
        "idx_evidence_revisions_passage",
        "idx_gaps_run_population",
        "idx_hypotheses_candidate",
        "idx_recommendations_finding",
        "idx_replay_manifests_run",
        "idx_run_outputs_run",
        "idx_taxonomy_subject",
    ];

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Cases")]
    public void AnalysisStateModelSchema6HasExactMigrationContractAndObjects()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        Assert.AreEqual(6, store.GetSchemaVersion());

        using SqliteConnection connection = temporary.OpenRaw();
        Assert.AreEqual("6", ScalarText(connection, "PRAGMA user_version;"));
        Assert.AreEqual(
            "6",
            ScalarText(
                connection,
                "SELECT value FROM store_metadata WHERE key = 'schema_version';"));
        Assert.AreEqual(
            "1.5.0",
            ScalarText(
                connection,
                "SELECT value FROM store_metadata WHERE key = 'storage_contract_version';"));
        Assert.AreEqual(
            "6667a2aa5be306dda20da7d09e18910507e3de09db2cc8ad9f1c0627f5ca56d0",
            ScalarText(
                connection,
                "SELECT value FROM store_metadata WHERE key = 'schema_fingerprint';"));
        Assert.AreEqual(
            "4|5",
            ScalarText(
                connection,
                """
                SELECT CAST(from_version AS TEXT) || '|' || CAST(to_version AS TEXT)
                FROM migration_history
                WHERE migration_id = 'M1-S5-WP4-0005';
                """));
        Assert.AreEqual(
            "5|6",
            ScalarText(
                connection,
                """
                SELECT CAST(from_version AS TEXT) || '|' || CAST(to_version AS TEXT)
                FROM migration_history
                WHERE migration_id = 'M1-S6-0006';
                """));

        CollectionAssert.AreEquivalent(
            AnalysisTables,
            SchemaNames(connection, "table", AnalysisTables));
        CollectionAssert.AreEquivalent(
            AnalysisIndexes,
            SchemaNames(connection, "index", AnalysisIndexes));

        foreach (string table in AnalysisTables)
        {
            Assert.AreEqual(
                1L,
                ScalarInt64(
                    connection,
                    "SELECT strict FROM pragma_table_list WHERE schema = 'main' AND name = $name;",
                    ("$name", table)),
                $"{table} must be STRICT.");
            Assert.AreEqual(
                2L,
                ScalarInt64(
                    connection,
                    """
                    SELECT COUNT(*) FROM sqlite_schema
                    WHERE type = 'trigger' AND tbl_name = $name
                      AND name IN ($update_name, $delete_name);
                    """,
                    ("$name", table),
                    ("$update_name", table + "_append_only_update"),
                    ("$delete_name", table + "_append_only_delete")),
                $"{table} must be append-only.");
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Cases")]
    public void AnalysisStateModelMigratesAcceptedSchema5ForwardAndRefusesNewerSchema()
    {
        using TemporaryStore migration = new();
        using (AuthoritativeStore store = migration.Open())
        {
            Assert.AreEqual(6, store.GetSchemaVersion());
        }

        using (SqliteConnection connection = migration.OpenRaw())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                string.Join(
                    Environment.NewLine,
                    ProviderSchema6TablesInCreationOrder.Reverse().Select(table => $"DROP TABLE {table};"))
                +
                """

                DROP INDEX idx_payload_identity_size;
                DELETE FROM migration_history WHERE migration_id = 'M1-S6-0006';
                UPDATE store_metadata SET value = '5' WHERE key = 'schema_version';
                UPDATE store_metadata SET value = '1.4.0'
                    WHERE key = 'storage_contract_version';
                UPDATE store_metadata SET value =
                    'e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d'
                    WHERE key = 'schema_fingerprint';
                PRAGMA user_version = 5;
                """;
            command.ExecuteNonQuery();
        }

        using (AuthoritativeStore migrated = migration.Open())
        {
            Assert.AreEqual(6, migrated.GetSchemaVersion());
        }

        using (SqliteConnection connection = migration.OpenRaw())
        {
            Assert.AreEqual(
                1L,
                ScalarInt64(
                    connection,
                    "SELECT COUNT(*) FROM migration_history WHERE migration_id = 'M1-S6-0006';"));
        }

        using TemporaryStore newer = new();
        using (AuthoritativeStore store = newer.Open())
        {
            Assert.AreEqual(6, store.GetSchemaVersion());
        }

        using (SqliteConnection connection = newer.OpenRaw())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA user_version = 7;";
            command.ExecuteNonQuery();
        }

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(newer.Open);
        Assert.IsNotNull(exception.InnerException);
        StringAssert.Contains(
            exception.InnerException.Message,
            "Database schema 7 is newer than supported schema 6.");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void AnalysisStateModelRejectsOpenStatesAndMutation()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        using SqliteConnection connection = temporary.OpenRaw();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO documentation_revisions(
                documentation_revision_id, source_id, source_kind, source_revision,
                content_sha256, byte_length, availability_state, retention_state,
                replay_state, created_at)
            VALUES (
                'revision-invalid', 'source-a', 'invented-source', '1',
                $sha, 0, 'unavailable', 'partial', 'audit-only', $now);
            """;
        command.Parameters.AddWithValue("$sha", new string('a', 64));
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO documentation_revisions(
                documentation_revision_id, source_id, source_kind, source_revision,
                content_sha256, byte_length, availability_state, retention_state,
                replay_state, created_at)
            VALUES (
                'revision-valid', 'source-a', 'fixture', '1',
                $sha, 0, 'unavailable', 'partial', 'audit-only', $now);
            """;
        command.ExecuteNonQuery();
        command.CommandText =
            "UPDATE documentation_revisions SET source_revision = '2' WHERE documentation_revision_id = 'revision-valid';";
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            "DELETE FROM documentation_revisions WHERE documentation_revision_id = 'revision-valid';";
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6ProviderAuthorityBlockRejectsEveryDownstreamBypass()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        using SqliteConnection connection = temporary.OpenRaw();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO provider_access_profiles VALUES('profile-a','openai','responses','A','account-a','billing-a','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_generations VALUES('generation-a','profile-a',1,0,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_capability_snapshots VALUES('cap-a','openai','gpt-5.6-sol','default','medium','current_turn','standard',0,0,0,'none',0,'disabled','explicit',0,0,272000,'synthetic-v1','bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES('intent-enroll-a','profile-a','generation-a','enroll','pending','none','pending-enrollment','none','not-applicable',NULL,NULL,NULL,'not-required','not-requested','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-enroll-a-v1','root-enroll-a','intent-enroll-a',1,NULL,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_profile_projection VALUES('profile-a','generation-a',0,'pending-enrollment','not-applicable',NULL,NULL,NULL,'intent-enroll-a','not-required','not-requested',1,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES('intent-activate-a-pending','profile-a','generation-a','enroll','pending','pending-enrollment','active-unverified','pending-enrollment','unavailable','account-a','billing-a','cap-a','not-required','not-requested','2026-08-10T00:00:00.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-activate-a-v1','root-activate-a','intent-activate-a-pending',1,NULL,'2026-08-10T00:00:00.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES('intent-activate-a','profile-a','generation-a','enroll','completed','pending-enrollment','active-unverified','active-unverified','unavailable','account-a','billing-a','cap-a','not-required','not-requested','2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-activate-a-v2','root-activate-a','intent-activate-a',2,'event-activate-a-v1','2026-08-10T00:00:01.0000000+00:00');
            UPDATE provider_profile_projection SET lifecycle_state='active-unverified',verification_state='unavailable',capability_snapshot_id='cap-a',account_identity_id='account-a',billing_scope_identity_id='billing-a',intent_id='intent-activate-a',projection_version=2,updated_at='2026-08-10T00:00:01.0000000+00:00' WHERE profile_id='profile-a';
            INSERT INTO provider_credential_intents VALUES('intent-verify-a-pending','profile-a','generation-a','verify','pending','active-unverified','active-verified','active-unverified','available','account-a','billing-a','cap-a','not-required','not-requested','2026-08-10T00:00:01.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-verify-a-v1','root-verify-a','intent-verify-a-pending',1,NULL,'2026-08-10T00:00:01.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES('intent-verify-a','profile-a','generation-a','verify','completed','active-unverified','active-verified','active-verified','available','account-a','billing-a','cap-a','not-required','not-requested','2026-08-10T00:00:02.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-verify-a-v2','root-verify-a','intent-verify-a',2,'event-verify-a-v1','2026-08-10T00:00:02.0000000+00:00');
            UPDATE provider_profile_projection SET lifecycle_state='active-verified',verification_state='available',intent_id='intent-verify-a',projection_version=3,updated_at='2026-08-10T00:00:02.0000000+00:00' WHERE profile_id='profile-a';
            INSERT INTO provider_price_snapshots VALUES('price-a','openai','gpt-5.6-sol','USD','default','synthetic-v1','cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_price_rules VALUES('price-a','rule-a','standard-under-272k','ordinary-input','input','none','global',1,1,'synthetic-v1');
            INSERT INTO runs VALUES('run-a','install-a','context-a','config-a','manifest-a','created',0,1,1,'2026-08-10T00:00:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO job_nodes VALUES('job-a','run-a',NULL,'provider','created',0,'2026-08-10T00:00:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO durable_commands VALUES('command-a','provider','run-a',0,'recorded','created',NULL,'2026-08-10T00:00:00.0000000+00:00',NULL,NULL);
            INSERT INTO evidence_acquisition_runs VALUES('acquisition-a','install-a','context-a','config-a','manifest-a','run-a','application-a','cost-a','created','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO evidence_acquisition_job_nodes VALUES('acquisition-job-a','acquisition-a','provider','created','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO evidence_acquisition_commands VALUES('acquisition-command-a','acquisition-a','provider-operation','2026-08-10T00:00:00.0000000+00:00','recorded');
            INSERT INTO provider_command_bindings VALUES('acquisition-command-a','evidence-acquisition-run','acquisition-a','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_effective_scan_configurations_v2 VALUES('config-v2-a','config-a','abababababababababababababababababababababababababababababababab','asserted-retained-v1-identity','profile-a','generation-a','gpt-5.6-sol','medium','current_turn','standard',0,'default',0,0,'none',0,'disabled','explicit',0,0,65536,73728,4096,1048576,1,600000000,120000,'["hosted-search","nexus","loot"]','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO evidence_acquisition_parent_links VALUES('parent-a','acquisition-a','run-a','initiated-by',NULL,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO payloads VALUES('request-payload-a','aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',1024,'application/json','retained','provider/request-a.json','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_operation_blocks(
              operation_id,owner_kind,owner_id,job_node_id,command_id,requested_at,confirmed_at,
              installation_snapshot_id,analysis_context_id,effective_configuration_id,resolved_input_manifest_id,
              profile_id,generation_id,revocation_epoch,operation_kind,capability_snapshot_id,price_snapshot_id,
              prompt_id,prompt_fingerprint,output_schema_id,output_schema_fingerprint,request_fingerprint,
              canonical_request_payload_id,canonical_request_fingerprint,canonical_request_bytes,settings_fingerprint,
              input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,maximum_request_bytes,
              maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,
              maximum_calculated_nano_usd,deadline_milliseconds,dispatch_deadline_utc,coordinator_fencing_epoch,state,recorded_at)
            VALUES('operation-a','evidence-acquisition-run','acquisition-a','acquisition-job-a','acquisition-command-a',
              '2026-08-10T00:00:00.0000000+00:00','2026-08-10T00:00:01.0000000+00:00','install-a','context-a','config-v2-a','manifest-a',
              'profile-a','generation-a',0,'source-claim-extraction','cap-a','price-a','prompt-a',
              'dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd','schema-a',
              'eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee',
              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa','request-payload-a',
              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',1024,
              '9999999999999999999999999999999999999999999999999999999999999999',
              'unresolved-openai-responses-framing','authority-required','authority-required',65536,73728,4096,
              1048576,1,600000000,120000,'2026-08-10T00:02:00.0000000+00:00',1,'input-bound-blocked','2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_operation_projection VALUES('operation-a','input-bound-blocked',0,0,0,1,'2026-08-10T00:00:00.0000000+00:00');
            """;
        command.ExecuteNonQuery();
        Assert.AreEqual(1L, ScalarInt64(connection, "SELECT COUNT(*) FROM provider_operation_blocks;"));
        Assert.AreEqual("input-bound-blocked", ScalarText(connection, "SELECT state FROM provider_operation_projection;"));

        command.CommandText = "INSERT INTO provider_operation_authorizations(authorization_id) VALUES('auth-bypass');";
        SqliteException blocked = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(blocked.Message, "accepted local input-bound policy required");

        command.CommandText =
            """
            INSERT INTO provider_operation_authorizations(
              authorization_id,operation_id,owner_kind,owner_id,analysis_run_id,job_node_id,command_id,requested_at,profile_id,generation_id,
              revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,effective_configuration_id,
              resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,output_schema_fingerprint,
              request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,price_snapshot_id,settings_fingerprint,
              input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,
              coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,
              maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,
              deadline_milliseconds,dispatch_deadline_utc,confirmed_at)
            VALUES('auth-fake','operation-a','analysis-run','run-a','run-a','job-a','command-a','2026-08-10T00:00:00.0000000+00:00','profile-a','generation-a',0,
              'source-claim-extraction','install-a','context-a','config-v2-a','manifest-a','prompt-a',
              'dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd','schema-a',
              'eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee',
              'ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff',
              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa','cap-a','price-a',
              '9999999999999999999999999999999999999999999999999999999999999999',
              'invented-policy','invented-version','invented-proof',1,65536,73728,4096,1048576,1,600000000,120000,
              '2026-08-10T00:02:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        (string Table, string Sql)[] bypasses =
        [
            ("provider_operation_attempts", "INSERT INTO provider_operation_attempts VALUES('attempt-bypass','operation-a',1,'proposed',1,'2026-08-10T00:00:00.0000000+00:00');"),
            ("provider_requests", """
                INSERT INTO provider_requests(
                  request_id,operation_id,provider_attempt_id,request_fingerprint,canonical_request_fingerprint,
                  settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,input_bound_policy_version,
                  input_bound_proof_status,payload_id,payload_fingerprint,payload_bytes,created_at)
                VALUES('request-bypass','operation-a','attempt-bypass',
                  '1111111111111111111111111111111111111111111111111111111111111111',
                  '2222222222222222222222222222222222222222222222222222222222222222',
                  '3333333333333333333333333333333333333333333333333333333333333333',
                  '4444444444444444444444444444444444444444444444444444444444444444',
                  'unresolved-openai-responses-framing','authority-required','authority-required','payload-bypass',
                  '5555555555555555555555555555555555555555555555555555555555555555',1,'2026-08-10T00:00:00.0000000+00:00');
                """),
            ("provider_reservations", "INSERT INTO provider_reservations VALUES('reservation-bypass','operation-a','attempt-bypass','request-bypass','{\"dispatch_count\":1,\"input_tokens\":1,\"output_tokens\":1,\"reasoning_tokens\":0,\"cache_read_tokens\":0,\"cache_write_tokens\":0,\"priced_tool_calls\":0,\"calculated_nano_usd\":1}',1,1,1,0,0,0,0,1,'2026-08-10T00:02:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');"),
            ("provider_reservation_scope_items", "INSERT INTO provider_reservation_scope_items VALUES('scope-bypass','reservation-bypass','operation','operation-a','{}',0);"),
            ("provider_dispatch_fences", "INSERT INTO provider_dispatch_fences VALUES('fence-bypass','auth-bypass','operation-a','reservation-bypass','request-bypass','attempt-bypass',1,'profile-a','generation-a',0,1,'synthetic bypass','2026-08-10T00:00:00.0000000+00:00');"),
            ("provider_transport_events", "INSERT INTO provider_transport_events VALUES('transport-bypass','operation-a','attempt-bypass','request-bypass','fence-bypass','not-started',1,'2026-08-10T00:00:00.0000000+00:00');"),
            ("provider_responses", """
                INSERT INTO provider_responses(
                  response_record_id,operation_id,request_id,provider_attempt_id,dispatch_fence_id,
                  maximum_raw_response_bytes,response_headers_availability,provider_request_id_availability,
                  response_state,requested_model,requested_service_tier,reasoning_context,reasoning_mode,
                  prompt_cache_mode,validation_state,admission_state,created_at)
                VALUES('response-bypass','operation-a','request-bypass','attempt-bypass','fence-bypass',1048576,
                  'unavailable','unavailable','cancelled','gpt-5.6-sol','default','current_turn','standard','explicit',
                  'unavailable','unavailable','2026-08-10T00:00:00.0000000+00:00');
                """),
            ("provider_usage_entries", """
                INSERT INTO provider_usage_entries(
                  usage_entry_id,operation_id,provider_attempt_id,request_id,dispatch_fence_id,response_record_id,
                  dispatch_count_availability,dispatch_count,input_tokens_availability,output_tokens_availability,
                  total_tokens_availability,reasoning_tokens_availability,cache_read_tokens_availability,
                  cache_write_tokens_availability,priced_tool_calls_availability,calculated_nano_usd_availability,
                  billing_availability,rate_availability,credit_availability,receipt_state,created_at)
                VALUES('usage-bypass','operation-a','attempt-bypass','request-bypass','fence-bypass','response-bypass',
                  'available',0,'unavailable','unavailable','unavailable','unavailable','unavailable','unavailable',
                  'unavailable','unavailable','unavailable','unavailable','unavailable','cancelled','2026-08-10T00:00:00.0000000+00:00');
                """),
            ("provider_rate_limit_facts", "INSERT INTO provider_rate_limit_facts VALUES('rate-bypass','usage-bypass','request','requests','unavailable',NULL,NULL,'2026-08-10T00:00:00.0000000+00:00',NULL);"),
            ("provider_settlements", "INSERT INTO provider_settlements VALUES('settlement-bypass','operation-a','attempt-bypass','request-bypass','reservation-bypass',NULL,'fence-bypass','unresolved-hold',0,1,'2026-08-10T00:00:00.0000000+00:00');"),
            ("provider_settlement_adjustments", "INSERT INTO provider_settlement_adjustments VALUES('adjustment-bypass','settlement-bypass',0,'owner','synthetic bypass','2026-08-10T00:00:00.0000000+00:00');"),
            ("provider_semantic_proposals", "INSERT INTO provider_semantic_proposals VALUES('proposal-bypass','operation-a','attempt-bypass','request-bypass','response-bypass','fence-bypass','gap','payload-bypass','2026-08-10T00:00:00.0000000+00:00');"),
            ("provider_semantic_admissions", "INSERT INTO provider_semantic_admissions VALUES('admission-bypass','proposal-bypass','admitted','host-policy','synthetic bypass',NULL,'2026-08-10T00:00:00.0000000+00:00');"),
            ("provider_replay_edges", "INSERT INTO provider_replay_edges VALUES('replay-bypass','operation-a',NULL,NULL,NULL,NULL,'unavailable',NULL,'2026-08-10T00:00:00.0000000+00:00');"),
            ("provider_operation_projection", "INSERT INTO provider_operation_projection VALUES('operation-bypass','input-bound-blocked',0,0,0,1,'2026-08-10T00:00:00.0000000+00:00');"),
            ("provider_budget_projection", "INSERT INTO provider_budget_projection VALUES('operation','operation-a',0,0,0,1,'2026-08-10T00:00:00.0000000+00:00');"),
        ];
        foreach ((string table, string sql) in bypasses)
        {
            command.CommandText = sql;
            Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery(), table);
        }
        Assert.AreEqual(0L, ScalarInt64(connection, "SELECT COUNT(*) FROM provider_operation_authorizations;"));
        foreach (string table in bypasses.Select(x => x.Table).Except(["provider_operation_projection"]))
        {
            Assert.AreEqual(0L, ScalarInt64(connection, $"SELECT COUNT(*) FROM {table};"), table);
        }
        Assert.AreEqual(1L, ScalarInt64(connection, "SELECT COUNT(*) FROM provider_operation_projection;"));
    }
    private static string[] SchemaNames(
        SqliteConnection connection,
        string type,
        IReadOnlyCollection<string> expectedNames)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_schema WHERE type = $type ORDER BY name;";
        command.Parameters.AddWithValue("$type", type);
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> names = [];
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names.Where(expectedNames.Contains).ToArray();
    }

    private static string ScalarText(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(
            command.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static long ScalarInt64(
        SqliteConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return Convert.ToInt64(
            command.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class TemporaryStore : IDisposable
    {
        private readonly string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-analysis-storage-{Guid.NewGuid():N}");

        public AuthoritativeStore Open() => new(new StoragePaths(root));

        public SqliteConnection OpenRaw()
        {
            SqliteRuntimeIdentity.InitializeNativeProvider();
            using StoragePaths paths = new(root);
            SqliteConnection connection = new(new SqliteConnectionStringBuilder
            {
                DataSource = paths.Database,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
            }.ToString());
            connection.Open();
            using SqliteCommand foreignKeys = connection.CreateCommand();
            foreignKeys.CommandText = "PRAGMA foreign_keys = ON;";
            foreignKeys.ExecuteNonQuery();
            return connection;
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
