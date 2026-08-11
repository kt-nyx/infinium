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
        "provider_access_profiles", "provider_generations", "provider_credential_intents",
        "provider_capability_snapshots", "provider_price_snapshots", "provider_price_rules", "evidence_acquisition_runs",
        "evidence_acquisition_parent_links", "evidence_acquisition_application_links",
        "provider_operation_authorizations", "provider_operation_attempts", "provider_requests",
        "provider_reservations", "provider_reservation_scope_items", "provider_dispatch_fences",
        "provider_transport_events", "provider_responses", "provider_usage_entries",
        "provider_settlements", "provider_settlement_adjustments", "provider_semantic_proposals",
        "provider_semantic_admissions", "provider_replay_edges", "provider_operation_projection",
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
            "e3a9ce9b9153da808ffb130b08d5bdd4f291c461f80fbe373c539915a16a03d1",
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
    public void Schema6ProviderOwnershipAndSingletonLiveAttemptRejectCrossGraphSubstitution()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        using SqliteConnection connection = temporary.OpenRaw();
        using SqliteCommand seed = connection.CreateCommand();
        seed.CommandText =
            """
            INSERT INTO payloads VALUES('payload-a','aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',2,'json','retained','provider/a','2026-08-10T00:00:00Z');
            INSERT INTO provider_access_profiles VALUES('profile-a','openai','responses','A','account-a','billing-a','2026-08-10T00:00:00Z');
            INSERT INTO provider_generations VALUES('generation-a','profile-a',1,0,'2026-08-10T00:00:00Z');
            INSERT INTO provider_capability_snapshots VALUES('cap-a','openai','gpt-5.6-sol','default','medium','current_turn','standard',0,0,0,'none',0,'disabled','explicit',0,0,272000,'synthetic-v1','bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb','2026-08-10T00:00:00Z');
            INSERT INTO provider_price_snapshots VALUES('price-a','openai','gpt-5.6-sol','USD','default','synthetic-v1','cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc','2026-08-10T00:00:00Z');
            INSERT INTO provider_price_rules VALUES('price-a','rule-a','standard-under-272k','ordinary-input','input','none','global',1,1,'synthetic-v1');
            INSERT INTO runs VALUES('run-a','install-a','context-a','config-a','manifest-a','created',0,1,1,'2026-08-10T00:00:00Z','2026-08-10T00:00:00Z');
            INSERT INTO job_nodes VALUES('job-a','run-a',NULL,'provider','created',0,'2026-08-10T00:00:00Z','2026-08-10T00:00:00Z');
            INSERT INTO provider_operation_authorizations(
              authorization_id,operation_id,owner_kind,owner_id,analysis_run_id,evidence_acquisition_run_id,job_node_id,
              profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,effective_configuration_id,resolved_input_manifest_id,
              prompt_id,prompt_fingerprint,output_schema_id,output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,
              capability_snapshot_id,price_snapshot_id,settings_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,
              canonical_request_bytes,proved_input_token_bound,coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,
              maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,confirmed_at)
            VALUES('auth-a','operation-a','analysis-run','run-a','run-a',NULL,'job-a','profile-a','generation-a',0,
              'source-claim-extraction','install-a','context-a','config-a','manifest-a','prompt-a',
              'dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd','schema-a',
              '9999999999999999999999999999999999999999999999999999999999999999',
              '6666666666666666666666666666666666666666666666666666666666666666',
              '7777777777777777777777777777777777777777777777777777777777777777','cap-a','price-a',
              '8888888888888888888888888888888888888888888888888888888888888888',
              'test-only-structural-proof','1','proved',65536,73728,1,65536,73728,4096,1048576,1,600000000,120000,'2026-08-10T00:00:00Z');
            INSERT INTO provider_operation_attempts VALUES('attempt-a','operation-a',1,'proposed',1,'2026-08-10T00:00:00Z');
            INSERT INTO provider_requests(request_id,operation_id,provider_attempt_id,request_fingerprint,canonical_request_fingerprint,settings_fingerprint,
              output_schema_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,canonical_request_bytes,
              proved_input_token_bound,payload_id,created_at)
            VALUES('request-a','operation-a','attempt-a','6666666666666666666666666666666666666666666666666666666666666666',
              '7777777777777777777777777777777777777777777777777777777777777777',
              '8888888888888888888888888888888888888888888888888888888888888888',
              '9999999999999999999999999999999999999999999999999999999999999999',
              'test-only-structural-proof','1','proved',65536,73728,'payload-a','2026-08-10T00:00:00Z');
            INSERT INTO provider_reservations VALUES('reservation-a','operation-a','attempt-a','request-a','{}',600000000,'2026-08-10T00:01:00Z','2026-08-10T00:00:00Z');
            INSERT INTO provider_dispatch_fences VALUES('fence-a','auth-a','operation-a','reservation-a','request-a','attempt-a',1,'profile-a','generation-a',0,1,'proved and authorized','2026-08-10T00:00:00Z');
            INSERT INTO provider_transport_events VALUES('transport-a','operation-a','attempt-a','request-a','fence-a','response-staged',1,'2026-08-10T00:00:00Z');
            INSERT INTO provider_responses(response_record_id,operation_id,request_id,provider_attempt_id,dispatch_fence_id,
              raw_response_payload_id,raw_response_fingerprint,raw_response_bytes,http_status,provider_response_id,response_state,
              refusal_code,incomplete_reason,error_code,requested_model,returned_model,requested_service_tier,returned_service_tier,
              reasoning_context,reasoning_mode,prompt_cache_mode,usage_json,validation_state,admission_state,created_at)
            VALUES('response-a','operation-a','request-a','attempt-a','fence-a','payload-a',
              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',2,200,'provider-response-a','completed',
              NULL,NULL,NULL,'gpt-5.6-sol','gpt-5.6-sol','default','default','current_turn','standard','explicit',
              '{"dispatch_count":{"availability":"available","value":1},"input_tokens":{"availability":"available","value":1},"output_tokens":{"availability":"available","value":0},"reasoning_tokens":{"availability":"available","value":0},"cache_read_tokens":{"availability":"available","value":0},"cache_write_tokens":{"availability":"available","value":0},"priced_tool_calls":{"availability":"available","value":0},"calculated_nano_usd":{"availability":"available","value":1},"billing_availability":"available","rate_availability":"available","credit_availability":"unavailable"}',
              'admitted','admitted','2026-08-10T00:00:00Z');
            INSERT INTO provider_usage_entries VALUES('usage-a','operation-a','attempt-a','request-a','fence-a','response-a','{}',1,'validated','2026-08-10T00:00:00Z');
            INSERT INTO provider_settlements VALUES('settlement-a','operation-a','attempt-a','request-a','reservation-a','usage-a','fence-a','settled',600000000,0,'2026-08-10T00:00:00Z');
            INSERT INTO provider_semantic_proposals VALUES('proposal-a','operation-a','attempt-a','request-a','response-a','fence-a','source-claim','payload-a','2026-08-10T00:00:00Z');
            INSERT INTO provider_semantic_admissions VALUES('admission-a','proposal-a','rejected','host-policy','synthetic','artifact-a','2026-08-10T00:00:00Z');
            INSERT INTO provider_replay_edges VALUES('replay-a','operation-a','attempt-a','request-a','response-a','fence-a','retained-response','manifest-a','2026-08-10T00:00:00Z');
            INSERT INTO provider_operation_projection VALUES('operation-a','attempt-a','request-a','fence-a','settled',600000000,1,0,NULL,1,'2026-08-10T00:00:00Z');
            """;
        seed.ExecuteNonQuery();

        using SqliteCommand invalid = connection.CreateCommand();
        invalid.CommandText = "INSERT INTO provider_operation_authorizations SELECT * FROM provider_operation_authorizations WHERE 0;";
        invalid.ExecuteNonQuery();

        string noProofAuthorization =
            """
            INSERT INTO provider_operation_authorizations(
              authorization_id,operation_id,owner_kind,owner_id,analysis_run_id,job_node_id,profile_id,generation_id,revocation_epoch,
              operation_kind,installation_snapshot_id,analysis_context_id,effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,
              output_schema_id,output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,price_snapshot_id,
              settings_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,canonical_request_bytes,proved_input_token_bound,
              coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,
              maximum_calculated_nano_usd,deadline_milliseconds,confirmed_at)
            VALUES('auth-blocked','operation-blocked','analysis-run','run-a','run-a','job-a','profile-a','generation-a',0,'source-claim-extraction',
              'install-a','context-a','config-a','manifest-a','prompt-a','dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd',
              'schema-a','9999999999999999999999999999999999999999999999999999999999999999',
              'abababababababababababababababababababababababababababababababab','acacacacacacacacacacacacacacacacacacacacacacacacacacacacacacac',
              'cap-a','price-a','8888888888888888888888888888888888888888888888888888888888888888',
              'unresolved-openai-responses-framing','authority-required','authority-required',NULL,NULL,1,65536,73728,4096,1048576,1,600000000,120000,'2026-08-10T00:00:00Z');
            """;
        invalid.CommandText = noProofAuthorization;
        Assert.ThrowsExactly<SqliteException>(() => invalid.ExecuteNonQuery());

        Dictionary<string, string> forbiddenDownstream = new(StringComparer.Ordinal)
        {
            ["attempt"] = "INSERT INTO provider_operation_attempts VALUES('attempt-blocked','operation-blocked',1,'proposed',1,'2026-08-10T00:00:00Z');",
            ["request"] = "INSERT INTO provider_requests VALUES('request-blocked','operation-blocked','attempt-blocked','a','b','c','d','p','v','proved',1,1,'payload-a','2026-08-10T00:00:00Z');",
            ["reservation"] = "INSERT INTO provider_reservations VALUES('reservation-blocked','operation-blocked','attempt-blocked','request-blocked','{}',1,'2026-08-10T00:01:00Z','2026-08-10T00:00:00Z');",
            ["fence"] = "INSERT INTO provider_dispatch_fences VALUES('fence-blocked','auth-blocked','operation-blocked','reservation-blocked','request-blocked','attempt-blocked',1,'profile-a','generation-a',0,1,'blocked','2026-08-10T00:00:00Z');",
            ["transport"] = "INSERT INTO provider_transport_events VALUES('transport-blocked','operation-blocked','attempt-blocked','request-blocked','fence-blocked','started',1,'2026-08-10T00:00:00Z');",
            ["response"] = "INSERT INTO provider_responses(response_record_id,operation_id,request_id,provider_attempt_id,dispatch_fence_id,response_state,requested_model,requested_service_tier,reasoning_context,reasoning_mode,prompt_cache_mode,usage_json,validation_state,admission_state,created_at) VALUES('response-blocked','operation-blocked','request-blocked','attempt-blocked','fence-blocked','cancelled','gpt-5.6-sol','default','current_turn','standard','explicit','{\"dispatch_count\":{\"availability\":\"available\",\"value\":0},\"billing_availability\":\"unavailable\",\"rate_availability\":\"unavailable\",\"credit_availability\":\"unavailable\"}','unavailable','unavailable','2026-08-10T00:00:00Z');",
            ["usage"] = "INSERT INTO provider_usage_entries VALUES('usage-blocked','operation-blocked','attempt-blocked','request-blocked','fence-blocked','response-blocked','{}',0,'unavailable','2026-08-10T00:00:00Z');",
            ["settlement"] = "INSERT INTO provider_settlements VALUES('settlement-blocked','operation-blocked','attempt-blocked','request-blocked','reservation-blocked','usage-blocked','fence-blocked','unresolved-hold',0,1,'2026-08-10T00:00:00Z');",
            ["proposal"] = "INSERT INTO provider_semantic_proposals VALUES('proposal-blocked','operation-blocked','attempt-blocked','request-blocked','response-blocked','fence-blocked','gap','payload-a','2026-08-10T00:00:00Z');",
            ["admission"] = "INSERT INTO provider_semantic_admissions VALUES('admission-blocked','proposal-blocked','rejected','host','blocked',NULL,'2026-08-10T00:00:00Z');",
            ["replay"] = "INSERT INTO provider_replay_edges VALUES('replay-blocked','operation-blocked','attempt-blocked','request-blocked','response-blocked','fence-blocked','unavailable','manifest-a','2026-08-10T00:00:00Z');",
            ["projection"] = "INSERT INTO provider_operation_projection VALUES('operation-blocked','attempt-blocked','request-blocked','fence-blocked','reserved',1,0,0,1,1,'2026-08-10T00:00:00Z');",
        };
        foreach ((string name, string sql) in forbiddenDownstream)
        {
            invalid.CommandText = sql;
            Assert.ThrowsExactly<SqliteException>(() => invalid.ExecuteNonQuery(), name);
        }

        invalid.CommandText =
            """
            INSERT INTO provider_responses(response_record_id,operation_id,request_id,provider_attempt_id,dispatch_fence_id,
              raw_response_payload_id,raw_response_fingerprint,raw_response_bytes,http_status,response_state,requested_model,returned_model,
              requested_service_tier,returned_service_tier,reasoning_context,reasoning_mode,prompt_cache_mode,usage_json,validation_state,admission_state,created_at)
            VALUES('response-empty','operation-a','request-a','attempt-a','fence-a','payload-a',
              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',2,200,'completed','gpt-5.6-sol','gpt-5.6-sol',
              'default','default','current_turn','standard','explicit','{}','admitted','admitted','2026-08-10T00:00:00Z');
            """;
        Assert.ThrowsExactly<SqliteException>(() => invalid.ExecuteNonQuery());

        invalid.CommandText =
            """
            INSERT INTO provider_requests(request_id,operation_id,provider_attempt_id,request_fingerprint,canonical_request_fingerprint,
              settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,
              canonical_request_bytes,proved_input_token_bound,payload_id,created_at)
            VALUES('request-second','operation-a','attempt-a','eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee',
              '7777777777777777777777777777777777777777777777777777777777777777',
              '8888888888888888888888888888888888888888888888888888888888888888',
              '9999999999999999999999999999999999999999999999999999999999999999','test-only-structural-proof','1','proved',65536,73728,'payload-a','2026-08-10T00:00:00Z');
            """;
        Assert.ThrowsExactly<SqliteException>(() => invalid.ExecuteNonQuery());
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
