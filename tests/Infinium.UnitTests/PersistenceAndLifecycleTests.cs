using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class PersistenceAndLifecycleTests
{
    private static readonly string[] ProviderProjectionNames =
    [
        "provider_operation_projection",
        "provider_budget_projection",
        "provider_profile_projection",
    ];

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6ProviderPersistenceAndBackupRestoreDeclarationsAreClosed()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();

        Assert.AreEqual(6, store.GetSchemaVersion());
        CollectionAssert.AreEquivalent(
            ProviderProjectionNames,
            ProviderPersistenceDeclarations.RebuildableProjections.ToArray());
        CollectionAssert.Contains(
            ProviderPersistenceDeclarations.StructurallyExcludedClasses.ToArray(),
            "provider-secret-bytes");

        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
              (SELECT COUNT(*) FROM migration_history
               WHERE migration_id='M1-S6-0006' AND from_version=5 AND to_version=6),
              (SELECT COUNT(*) FROM sqlite_schema
               WHERE type='table' AND name LIKE 'provider_%'),
              (SELECT COUNT(*) FROM sqlite_schema
               WHERE type='trigger' AND name LIKE 'provider_%_append_only_%');
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(1L, reader.GetInt64(0));
        Assert.AreEqual(36L, reader.GetInt64(1));
        Assert.AreEqual(66L, reader.GetInt64(2));
        reader.Close();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_schema WHERE type='trigger' AND name='provider_usage_operation_ceiling_guard';";
        Assert.AreEqual(0L, (long)command.ExecuteScalar()!);
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='table' AND name='provider_usage_entries';";
        string usageTableSql = (string)command.ExecuteScalar()!;
        StringAssert.Contains(usageTableSql, "reasoning_tokens <= output_tokens");
        StringAssert.Contains(usageTableSql, "dispatch_count BETWEEN 0 AND 2");
        StringAssert.Contains(usageTableSql, "input_tokens BETWEEN 0 AND 147456");
        StringAssert.Contains(usageTableSql, "output_tokens BETWEEN 0 AND 8192");
        StringAssert.Contains(usageTableSql, "total_tokens BETWEEN 0 AND 155648");
        StringAssert.Contains(usageTableSql, "priced_tool_calls BETWEEN 0 AND 64");
        StringAssert.Contains(usageTableSql, "calculated_nano_usd BETWEEN 0 AND 1200000000");
        StringAssert.Contains(usageTableSql, "receipt_id TEXT NOT NULL UNIQUE");
        foreach (string receiptState in new[] { "not-dispatched", "complete", "partial", "failed-known", "ambiguous", "unavailable" })
        {
            StringAssert.Contains(usageTableSql, $"'{receiptState}'");
        }
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='trigger' AND name='provider_usage_response_totality_guard';";
        string receiptTranslationSql = (string)command.ExecuteScalar()!;
        foreach (string responseState in new[]
                 {
                     "completed", "refusal", "incomplete", "failed", "queued", "in-progress", "malformed",
                     "oversized", "mismatched", "unknown", "cancelled",
                 })
        {
            StringAssert.Contains(receiptTranslationSql, $"'{responseState}'", responseState);
        }
        StringAssert.Contains(receiptTranslationSql, "r.response_state IN ('completed','refusal','mismatched') AND NEW.receipt_state = 'complete'");
        StringAssert.Contains(receiptTranslationSql, "r.response_state = 'malformed' AND NEW.receipt_state IN ('complete','partial')");
        StringAssert.Contains(receiptTranslationSql, "r.response_state IN ('incomplete','queued','in-progress') AND NEW.receipt_state = 'partial'");
        StringAssert.Contains(receiptTranslationSql, "r.response_state = 'failed' AND NEW.receipt_state = 'failed-known'");
        StringAssert.Contains(receiptTranslationSql, "r.response_state = 'unknown' AND NEW.receipt_state = 'ambiguous'");
        StringAssert.Contains(receiptTranslationSql, "r.response_state = 'oversized' AND NEW.receipt_state IN ('complete','partial')");
        StringAssert.Contains(receiptTranslationSql,
            "r.response_state = 'cancelled' AND r.availability = 'unavailable' AND NEW.receipt_state = 'not-dispatched'");
        StringAssert.Contains(receiptTranslationSql,
            "r.response_state = 'cancelled' AND r.availability = 'available'");
        StringAssert.Contains(receiptTranslationSql,
            "NEW.receipt_state IN ('complete','partial','failed-known')");
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='table' AND name='provider_reservations';";
        string reservationSql = (string)command.ExecuteScalar()!;
        foreach (string dimension in new[]
                 {
                     "reserved_dispatch_count", "reserved_input_tokens", "reserved_output_tokens",
                     "reserved_reasoning_tokens", "reserved_cache_read_tokens", "reserved_cache_write_tokens",
                     "reserved_priced_tool_calls", "maximum_nano_usd",
                 })
        {
            StringAssert.Contains(reservationSql, dimension);
        }
        StringAssert.Contains(reservationSql, "usage_json = json_object");
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='table' AND name='provider_semantic_validations';";
        string validationSql = (string)command.ExecuteScalar()!;
        StringAssert.Contains(validationSql,
            "FOREIGN KEY(proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id)");
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='table' AND name='provider_semantic_admissions';";
        string admissionSql = (string)command.ExecuteScalar()!;
        StringAssert.Contains(admissionSql,
            "FOREIGN KEY(validation_id,proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id,state)");
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='trigger' AND name='provider_semantic_admission_application_guard';";
        string admissionGuardSql = (string)command.ExecuteScalar()!;
        StringAssert.Contains(admissionGuardSql, "link.evidence_application_link_id = NEW.semantic_link_id");
        StringAssert.Contains(admissionGuardSql, "candidate.candidate_id = NEW.root_subject_id AND candidate.run_id = NEW.owner_id");
        Assert.IsFalse(admissionGuardSql.Contains("evidence_acquisition_application_links", StringComparison.Ordinal));
        command.CommandText =
            "SELECT sql FROM sqlite_schema WHERE type='trigger' AND name='evidence_acquisition_application_admitted_artifact_guard';";
        string consumptionGuardSql = (string)command.ExecuteScalar()!;
        StringAssert.Contains(consumptionGuardSql, "admission.owner_kind='evidence-acquisition-run'");
        StringAssert.Contains(consumptionGuardSql, "admission.state='admitted'");
        StringAssert.Contains(consumptionGuardSql, "admission.admitted_artifact_id=NEW.admitted_artifact_id");
        StringAssert.Contains(consumptionGuardSql, "admission.created_at <= NEW.created_at");
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='trigger' AND name='provider_cancelled_response_operation_root_guard';";
        string cancellationRootSql = (string)command.ExecuteScalar()!;
        StringAssert.Contains(cancellationRootSql, "provider_operation_authorizations a");
        StringAssert.Contains(cancellationRootSql, "provider_operation_attempts attempt");
        StringAssert.Contains(cancellationRootSql, "provider_requests request");
        StringAssert.Contains(cancellationRootSql, "provider_reservations reservation");
        StringAssert.Contains(cancellationRootSql, "provider_transport_events e");
        StringAssert.Contains(cancellationRootSql, "WHERE e.operation_id = NEW.operation_id");
        Assert.IsFalse(cancellationRootSql.Contains("e.occurred_at <= NEW.created_at", StringComparison.Ordinal));
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='trigger' AND name='provider_response_transport_binding_guard';";
        string responseAuthoritySql = (string)command.ExecuteScalar()!;
        StringAssert.Contains(responseAuthoritySql, "a.authorization_id = NEW.authorization_id");
        StringAssert.Contains(responseAuthoritySql, "a.maximum_raw_response_bytes = NEW.maximum_raw_response_bytes");
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='trigger' AND name='provider_usage_response_totality_guard';";
        StringAssert.Contains((string)command.ExecuteScalar()!, "r.created_at <= NEW.created_at");
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='trigger' AND name='provider_semantic_proposal_root_guard';";
        string proposalRootSql = (string)command.ExecuteScalar()!;
        StringAssert.Contains(proposalRootSql, "d.created_at <= NEW.created_at");
        StringAssert.Contains(proposalRootSql, "link.created_at <= NEW.created_at");
        StringAssert.Contains(proposalRootSql, "c.created_at <= NEW.created_at");
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type='trigger' AND name='provider_profile_projection_exact_root_insert_guard';";
        StringAssert.Contains((string)command.ExecuteScalar()!,
            "deleted provider profile projection requires exact completed delete event chain");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6ProviderPersistenceBackupRestoreRetainsOnlyBlockedAuthorityState()
    {
        using TemporaryStore source = new();
        BackupArtifact backup;
        using (AuthoritativeStore store = source.Open())
        {
            SeedProviderAuthorityBlock(source.Root);
            backup = store.CreateBackup("Schema6Provider", DateTimeOffset.UtcNow);
        }

        string targetParent = Path.Combine(Path.GetTempPath(), $"infinium-schema6-restore-{Guid.NewGuid():N}");
        string targetRoot = Path.Combine(targetParent, "product");
        Directory.CreateDirectory(targetParent);
        try
        {
            using (StoragePaths target = new(targetRoot))
            {
                AuthoritativeStore.RestoreBackup(backup, target);
            }
            using AuthoritativeStore restored = new(new StoragePaths(targetRoot));
            Assert.AreEqual(6, restored.GetSchemaVersion());
            using SqliteConnection connection = new($"Data Source={restored.Paths.Database};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT b.profile_id,b.generation_id,b.operation_kind,b.capability_snapshot_id,b.price_snapshot_id,
                       b.input_bound_policy_id,b.input_bound_policy_version,b.input_bound_proof_status,
                       b.maximum_request_bytes,b.maximum_input_tokens,b.maximum_output_tokens,
                       b.maximum_raw_response_bytes,b.maximum_dispatch_count,b.maximum_calculated_nano_usd,
                       b.deadline_milliseconds,b.state,p.state,p.reserved_nano_usd,p.calculated_nano_usd,p.unresolved_hold
                FROM provider_operation_blocks b
                JOIN provider_operation_projection p ON p.operation_id=b.operation_id
                WHERE b.operation_id='operation-restore';
                """;
            using SqliteDataReader blocked = command.ExecuteReader();
            Assert.IsTrue(blocked.Read());
            string[] textExpected = ["profile-restore", "generation-restore", "source-claim-extraction", "cap-restore",
                "price-restore", "unresolved-openai-responses-framing", "authority-required", "authority-required"];
            for (int index = 0; index < textExpected.Length; index++)
            {
                Assert.AreEqual(textExpected[index], blocked.GetString(index));
            }
            long[] numericExpected = [65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000];
            for (int index = 0; index < numericExpected.Length; index++)
            {
                Assert.AreEqual(numericExpected[index], blocked.GetInt64(8 + index));
            }
            Assert.AreEqual("input-bound-blocked", blocked.GetString(15));
            Assert.AreEqual("input-bound-blocked", blocked.GetString(16));
            Assert.AreEqual(0L, blocked.GetInt64(17));
            Assert.AreEqual(0L, blocked.GetInt64(18));
            Assert.AreEqual(0L, blocked.GetInt64(19));
            Assert.IsFalse(blocked.Read());
            blocked.Close();

            string[] downstream = ["provider_operation_authorizations", "provider_operation_attempts", "provider_requests",
                "provider_reservations", "provider_dispatch_fences", "provider_transport_events", "provider_responses",
                "provider_usage_entries", "provider_rate_limit_facts", "provider_settlements", "provider_semantic_proposals",
                "provider_semantic_admissions", "provider_replay_edges"];
            foreach (string table in downstream)
            {
                command.CommandText = $"SELECT COUNT(*) FROM {table};";
                Assert.AreEqual(0L, (long)command.ExecuteScalar()!, table);
            }
        }
        finally
        {
            if (Directory.Exists(targetParent))
            {
                Directory.Delete(targetParent, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6ProvedInputBoundPolicyIsPinnedAtAuthorizationAndRequestPersistenceBoundaries()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DROP TRIGGER provider_authority_release_required;";
        command.ExecuteNonQuery();

        const string insertAuthorization =
            """
            INSERT INTO provider_operation_authorizations(
              authorization_id,operation_id,owner_kind,owner_id,evidence_acquisition_run_id,job_node_id,command_id,
              requested_at,profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,
              analysis_context_id,effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,
              output_schema_id,output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,
              capability_snapshot_id,price_snapshot_id,settings_fingerprint,input_bound_policy_id,
              input_bound_policy_version,input_bound_proof_status,coordinator_fencing_epoch,maximum_request_bytes,
              maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,
              maximum_calculated_nano_usd,deadline_milliseconds,dispatch_deadline_utc,confirmed_at)
            SELECT $authorizationId,operation_id,owner_kind,owner_id,owner_id,job_node_id,command_id,requested_at,
              profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
              effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
              output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
              price_snapshot_id,settings_fingerprint,$policyId,$policyVersion,'proved',coordinator_fencing_epoch,
              maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,
              maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,dispatch_deadline_utc,confirmed_at
            FROM provider_operation_blocks WHERE operation_id='operation-restore';
            """;
        command.CommandText = insertAuthorization;
        command.Parameters.AddWithValue("$authorizationId", "authorization-drift");
        command.Parameters.AddWithValue("$policyId", "attacker-policy");
        command.Parameters.AddWithValue("$policyVersion", "v999");
        SqliteException rejectedAuthorization = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(rejectedAuthorization.Message,
            "input_bound_policy_id = 'openai-responses-o200k-byte-envelope'");

        command.Parameters["$authorizationId"].Value = "authorization-approved";
        command.Parameters["$policyId"].Value = "openai-responses-o200k-byte-envelope";
        command.Parameters["$policyVersion"].Value = "v1";
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.Parameters.Clear();
        command.CommandText =
            """
            INSERT INTO provider_operation_attempts VALUES(
              'attempt-approved','operation-restore',1,'proposed',1,'2026-08-10T00:00:02.0000000+00:00');
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());

        const string insertRequest =
            """
            INSERT INTO provider_requests(
              request_id,client_request_id,operation_id,provider_attempt_id,request_fingerprint,
              canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,
              input_bound_policy_version,input_bound_proof_status,payload_id,payload_fingerprint,payload_bytes,created_at)
            SELECT $requestId,$clientRequestId,operation_id,'attempt-approved',request_fingerprint,
              canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,$policyId,$policyVersion,
              input_bound_proof_status,'request-payload-restore',request_fingerprint,1024,
              '2026-08-10T00:00:03.0000000+00:00'
            FROM provider_operation_authorizations WHERE authorization_id='authorization-approved';
            """;
        command.CommandText = insertRequest;
        command.Parameters.AddWithValue("$requestId", "request-drift");
        command.Parameters.AddWithValue("$clientRequestId", "client-request-drift");
        command.Parameters.AddWithValue("$policyId", "attacker-policy");
        command.Parameters.AddWithValue("$policyVersion", "v999");
        SqliteException rejectedRequest = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(rejectedRequest.Message,
            "input_bound_policy_id = 'openai-responses-o200k-byte-envelope'");

        command.Parameters["$requestId"].Value = "request-approved";
        command.Parameters["$clientRequestId"].Value = "client-request-approved";
        command.Parameters["$policyId"].Value = "openai-responses-o200k-byte-envelope";
        command.Parameters["$policyVersion"].Value = "v1";
        Assert.AreEqual(1, command.ExecuteNonQuery());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6ProviderRelationalRootsRejectCrossBindingFingerprintAndTimeRegression()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        foreach ((string state, string outcome, string verification, string recovery) in new[]
                 {
                     ("completed", "disabled", "unavailable", "not-required"),
                     ("failed", "active-verified", "available", "not-required"),
                     ("cancelled", "active-verified", "available", "not-required"),
                     ("unavailable", "secure-store-unavailable", "unavailable", "unavailable"),
                 })
        {
            command.CommandText = $"""
                INSERT INTO provider_credential_intents VALUES(
                  'orphan-disable-{state}','profile-restore','generation-restore','disable','{state}',
                  'active-verified','disabled','{outcome}','{verification}','account-restore','billing-restore','cap-restore',
                  '{recovery}','not-requested','2026-08-10T00:00:02.5000000+00:00');
                """;
            SqliteException orphan = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery(), state);
            StringAssert.Contains(orphan.Message, "requires one exact open pending v1 root", state);
        }
        command.CommandText =
            """
            INSERT INTO durable_commands VALUES('command-owner-mismatch','provider','run-restore',0,'recorded','created',NULL,'2026-08-10T00:00:03.0000000+00:00',NULL,NULL);
            INSERT INTO durable_commands VALUES('command-fingerprint-mismatch','provider','run-restore',0,'recorded','created',NULL,'2026-08-10T00:00:04.0000000+00:00',NULL,NULL);
            INSERT INTO durable_commands VALUES('command-deadline-mismatch','provider','run-restore',0,'recorded','created',NULL,'2026-08-10T00:00:05.0000000+00:00',NULL,NULL);
            INSERT INTO evidence_acquisition_commands VALUES('command-owner-mismatch','acquisition-restore','provider-operation','2026-08-10T00:00:00.0000000+00:00','recorded');
            INSERT INTO evidence_acquisition_commands VALUES('command-fingerprint-mismatch','acquisition-restore','provider-operation','2026-08-10T00:00:00.0000000+00:00','recorded');
            INSERT INTO evidence_acquisition_commands VALUES('command-deadline-mismatch','acquisition-restore','provider-operation','2026-08-10T00:00:00.0000000+00:00','recorded');
            INSERT INTO evidence_acquisition_commands VALUES('command-request-time-mismatch','acquisition-restore','provider-operation','2026-08-10T00:00:03.0000000+00:00','recorded');
            INSERT INTO evidence_acquisition_commands VALUES('command-config-mismatch','acquisition-restore','provider-operation','2026-08-10T00:00:00.0000000+00:00','recorded');
            INSERT INTO evidence_acquisition_commands VALUES('command-malformed-instant','acquisition-restore','provider-operation','2026-08-10T00:00:00.0000000+00:00','recorded');
            INSERT INTO provider_command_bindings VALUES('command-owner-mismatch','evidence-acquisition-run','acquisition-restore','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_command_bindings VALUES('command-fingerprint-mismatch','evidence-acquisition-run','acquisition-restore','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_command_bindings VALUES('command-deadline-mismatch','evidence-acquisition-run','acquisition-restore','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_command_bindings VALUES('command-request-time-mismatch','evidence-acquisition-run','acquisition-restore','2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_command_bindings VALUES('command-config-mismatch','evidence-acquisition-run','acquisition-restore','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_command_bindings VALUES('command-malformed-instant','evidence-acquisition-run','acquisition-restore','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_generations VALUES('generation-config-other','profile-restore',2,0,'2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_effective_scan_configurations_v2
            SELECT 'config-v2-other-generation',local_configuration_v1_id,local_configuration_v1_fingerprint,
              local_configuration_v1_provenance,profile_id,'generation-config-other',model,reasoning_effort,reasoning_context,reasoning_mode,store,service_tier,
              background,stream,tool_choice,tool_count,truncation,prompt_cache_mode,has_prompt_cache_key,
              has_prompt_cache_breakpoint,maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,
              maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,
              not_used_boundaries_json,created_at
            FROM provider_effective_scan_configurations_v2 WHERE configuration_id='config-v2-restore';
            """;
        command.ExecuteNonQuery();

        string insertFromValid =
            """
            INSERT INTO provider_operation_blocks
            SELECT $operationId,owner_kind,$ownerId,job_node_id,$commandId,$requestedAt,confirmed_at,
              installation_snapshot_id,analysis_context_id,$effectiveConfigurationId,resolved_input_manifest_id,
              profile_id,generation_id,revocation_epoch,operation_kind,capability_snapshot_id,price_snapshot_id,
              prompt_id,prompt_fingerprint,output_schema_id,output_schema_fingerprint,$requestFingerprint,
              canonical_request_payload_id,canonical_request_fingerprint,canonical_request_bytes,settings_fingerprint,
              input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,maximum_request_bytes,
              maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,
              maximum_calculated_nano_usd,deadline_milliseconds,$dispatchDeadline,coordinator_fencing_epoch,state,recorded_at
            FROM provider_operation_blocks WHERE operation_id='operation-restore';
            """;
        command.CommandText = insertFromValid;
        command.Parameters.AddWithValue("$operationId", "operation-owner-mismatch");
        command.Parameters.AddWithValue("$ownerId", "acquisition-other");
        command.Parameters.AddWithValue("$commandId", "command-owner-mismatch");
        command.Parameters.AddWithValue("$requestedAt", "2026-08-10T00:00:00.0000000+00:00");
        command.Parameters.AddWithValue("$effectiveConfigurationId", "config-v2-restore");
        command.Parameters.AddWithValue("$requestFingerprint", Convert.ToHexStringLower(SHA256.HashData(new byte[1024])));
        command.Parameters.AddWithValue("$dispatchDeadline", "2026-08-10T00:02:00.0000000+00:00");
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.Parameters["$operationId"].Value = "operation-fingerprint-mismatch";
        command.Parameters["$ownerId"].Value = "acquisition-restore";
        command.Parameters["$commandId"].Value = "command-fingerprint-mismatch";
        command.Parameters["$requestFingerprint"].Value = new string('f', 64);
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.Parameters["$operationId"].Value = "operation-deadline-mismatch";
        command.Parameters["$commandId"].Value = "command-deadline-mismatch";
        command.Parameters["$requestFingerprint"].Value = Convert.ToHexStringLower(SHA256.HashData(new byte[1024]));
        command.Parameters["$dispatchDeadline"].Value = "2026-08-10T00:02:01.0000000+00:00";
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.Parameters["$operationId"].Value = "operation-request-time-mismatch";
        command.Parameters["$commandId"].Value = "command-request-time-mismatch";
        command.Parameters["$dispatchDeadline"].Value = "2026-08-10T00:02:00.0000000+00:00";
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.Parameters["$operationId"].Value = "operation-config-mismatch";
        command.Parameters["$commandId"].Value = "command-config-mismatch";
        command.Parameters["$effectiveConfigurationId"].Value = "config-v2-other-generation";
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.Parameters["$operationId"].Value = "operation-malformed-instant";
        command.Parameters["$commandId"].Value = "command-malformed-instant";
        command.Parameters["$effectiveConfigurationId"].Value = "config-v2-restore";
        command.Parameters["$requestedAt"].Value = "2026-99-99T99:99:99.0000000+00:00";
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            UPDATE provider_profile_projection
            SET projection_version=projection_version,updated_at='2026-08-10T00:00:03.0000000+00:00'
            WHERE profile_id='profile-restore';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO provider_generations VALUES(
              'generation-other','profile-restore',2,0,'2026-08-10T00:00:03.0000000+00:00');
            UPDATE provider_profile_projection
            SET generation_id='generation-other',projection_version=4,updated_at='2026-08-10T00:00:03.0000000+00:00'
            WHERE profile_id='profile-restore';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO provider_access_profiles VALUES(
              'profile-other','openai','responses','Other','account-other','billing-other','2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_generations VALUES(
              'generation-cross-profile','profile-other',1,0,'2026-08-10T00:00:03.0000000+00:00');
            UPDATE provider_profile_projection
            SET generation_id='generation-cross-profile',projection_version=4,updated_at='2026-08-10T00:00:03.0000000+00:00'
            WHERE profile_id='profile-restore';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-time-regression','profile-restore','generation-restore','verify','completed',
              'active-unverified','active-verified','active-verified','available','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:01.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6CredentialReplacementAndCancellationMatrixIsTruthful()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO provider_access_profiles VALUES('profile-enroll-cancel','openai','responses','Enroll cancellation',NULL,NULL,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_generations VALUES('generation-enroll-cancel','profile-enroll-cancel',1,0,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-enroll-cancel-pending','profile-enroll-cancel','generation-enroll-cancel','enroll','pending',
              'none','pending-enrollment','none','not-applicable',NULL,NULL,NULL,'not-required','not-requested','2026-08-10T00:00:00.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-enroll-cancel-v1','root-enroll-cancel','intent-enroll-cancel-pending',1,NULL,'2026-08-10T00:00:00.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-enroll-cancel','profile-enroll-cancel','generation-enroll-cancel','enroll','cancelled',
              'none','pending-enrollment','none','not-applicable',NULL,NULL,NULL,'not-required','not-requested','2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-enroll-cancel-v2','root-enroll-cancel','intent-enroll-cancel',2,'event-enroll-cancel-v1','2026-08-10T00:00:01.0000000+00:00');

            INSERT INTO provider_access_profiles VALUES('profile-verify-cancel','openai','responses','Verify cancellation','account-verify','billing-verify','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_generations VALUES('generation-verify-cancel','profile-verify-cancel',1,0,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-verify-enroll-pending','profile-verify-cancel','generation-verify-cancel','enroll','pending',
              'none','pending-enrollment','none','not-applicable',NULL,NULL,NULL,'not-required','not-requested','2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-verify-enroll-v1','root-verify-enroll','intent-verify-enroll-pending',1,NULL,'2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_profile_projection VALUES(
              'profile-verify-cancel','generation-verify-cancel',0,'pending-enrollment','not-applicable',NULL,NULL,NULL,
              'intent-verify-enroll-pending','not-required','not-requested',1,'2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-verify-enroll-transition','profile-verify-cancel','generation-verify-cancel','enroll','pending',
              'pending-enrollment','active-unverified','pending-enrollment','unavailable','account-verify','billing-verify','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:01.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-verify-activate-v1','root-verify-activate','intent-verify-enroll-transition',1,NULL,'2026-08-10T00:00:01.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-verify-enroll-complete','profile-verify-cancel','generation-verify-cancel','enroll','completed',
              'pending-enrollment','active-unverified','active-unverified','unavailable','account-verify','billing-verify','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:02.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-verify-activate-v2','root-verify-activate','intent-verify-enroll-complete',2,'event-verify-activate-v1','2026-08-10T00:00:02.0000000+00:00');
            UPDATE provider_profile_projection SET lifecycle_state='active-unverified',verification_state='unavailable',
              capability_snapshot_id='cap-restore',account_identity_id='account-verify',billing_scope_identity_id='billing-verify',
              intent_id='intent-verify-enroll-complete',projection_version=2,updated_at='2026-08-10T00:00:02.0000000+00:00'
              WHERE profile_id='profile-verify-cancel';
            INSERT INTO provider_credential_intents VALUES(
              'intent-verify-cancel-pending','profile-verify-cancel','generation-verify-cancel','verify','pending',
              'active-unverified','active-verified','active-unverified','available','account-verify','billing-verify','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:02.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-verify-cancel-v1','root-verify-cancel','intent-verify-cancel-pending',1,NULL,'2026-08-10T00:00:02.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-verify-cancel','profile-verify-cancel','generation-verify-cancel','verify','cancelled',
              'active-unverified','active-verified','active-unverified','unavailable','account-verify','billing-verify','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-verify-cancel-v2','root-verify-cancel','intent-verify-cancel',2,'event-verify-cancel-v1','2026-08-10T00:00:03.0000000+00:00');

            INSERT INTO provider_access_profiles VALUES('profile-recover-cancel','openai','responses','Recover cancellation','account-recover','billing-recover','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_generations VALUES('generation-recover-cancel','profile-recover-cancel',1,0,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-recover-enroll-pending','profile-recover-cancel','generation-recover-cancel','enroll','pending',
              'none','pending-enrollment','none','not-applicable',NULL,NULL,NULL,'not-required','not-requested','2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-recover-enroll-v1','root-recover-enroll','intent-recover-enroll-pending',1,NULL,'2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_profile_projection VALUES(
              'profile-recover-cancel','generation-recover-cancel',0,'pending-enrollment','not-applicable',NULL,NULL,NULL,
              'intent-recover-enroll-pending','not-required','not-requested',1,'2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-recover-transition-pending','profile-recover-cancel','generation-recover-cancel','enroll','pending',
              'pending-enrollment','recovery-required','pending-enrollment','unavailable','account-recover','billing-recover','cap-restore',
              'required','not-requested','2026-08-10T00:00:01.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-recover-transition-v1','root-recover-transition','intent-recover-transition-pending',1,NULL,'2026-08-10T00:00:01.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-recover-required','profile-recover-cancel','generation-recover-cancel','enroll','completed',
              'pending-enrollment','recovery-required','recovery-required','unavailable','account-recover','billing-recover','cap-restore',
              'required','not-requested','2026-08-10T00:00:02.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-recover-transition-v2','root-recover-transition','intent-recover-required',2,'event-recover-transition-v1','2026-08-10T00:00:02.0000000+00:00');
            UPDATE provider_profile_projection SET lifecycle_state='recovery-required',verification_state='unavailable',
              capability_snapshot_id='cap-restore',account_identity_id='account-recover',billing_scope_identity_id='billing-recover',
              intent_id='intent-recover-required',recovery_disposition='required',projection_version=2,updated_at='2026-08-10T00:00:02.0000000+00:00'
              WHERE profile_id='profile-recover-cancel';
            INSERT INTO provider_credential_intents VALUES(
              'intent-recover-cancel-pending','profile-recover-cancel','generation-recover-cancel','recover','pending',
              'recovery-required','active-unverified','recovery-required','unavailable','account-recover','billing-recover','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:02.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-recover-cancel-v1','root-recover-cancel','intent-recover-cancel-pending',1,NULL,'2026-08-10T00:00:02.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-recover-cancel','profile-recover-cancel','generation-recover-cancel','recover','cancelled',
              'recovery-required','active-unverified','recovery-required','unavailable','account-recover','billing-recover','cap-restore',
              'required','not-requested','2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-recover-cancel-v2','root-recover-cancel','intent-recover-cancel',2,'event-recover-cancel-v1','2026-08-10T00:00:03.0000000+00:00');

            INSERT INTO provider_generations VALUES('generation-replacement','profile-restore',2,0,'2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-replace-cancel-pending','profile-restore','generation-restore','replace','pending',
              'active-verified','replacing','active-verified','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:03.1000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-replace-cancel-v1','root-replace-cancel','intent-replace-cancel-pending',1,NULL,'2026-08-10T00:00:03.1000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-replace-cancel','profile-restore','generation-restore','replace','cancelled',
              'active-verified','replacing','active-verified','available','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:03.2000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-replace-cancel-v2','root-replace-cancel','intent-replace-cancel',2,'event-replace-cancel-v1','2026-08-10T00:00:03.2000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-disable-cancel-pending','profile-restore','generation-restore','disable','pending',
              'active-verified','disabled','active-verified','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:04.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-disable-cancel-v1','root-disable-cancel','intent-disable-cancel-pending',1,NULL,'2026-08-10T00:00:04.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-disable-cancel','profile-restore','generation-restore','disable','cancelled',
              'active-verified','disabled','active-verified','available','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:04.1000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-disable-cancel-v2','root-disable-cancel','intent-disable-cancel',2,'event-disable-cancel-v1','2026-08-10T00:00:04.1000000+00:00');
            """;
        command.ExecuteNonQuery();

        command.CommandText = "SELECT count(*) FROM provider_credential_intents WHERE intent_state='cancelled';";
        Assert.AreEqual(5L, (long)command.ExecuteScalar()!);
        command.CommandText =
            "SELECT sum(projection_version) FROM provider_profile_projection WHERE profile_id IN ('profile-restore','profile-verify-cancel','profile-recover-cancel');";
        Assert.AreEqual(7L, (long)command.ExecuteScalar()!);

        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-illegal-cancelled-transition','profile-restore','generation-restore','delete','cancelled',
              'active-verified','disabled','active-verified','available','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:10.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-arbitrary-failed-outcome','profile-restore','generation-replacement','replace','failed',
              'active-verified','replacing','recovery-required','unavailable','account-restore','billing-restore','cap-restore',
              'required','not-requested','2026-08-10T00:00:10.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-arbitrary-unavailable-outcome','profile-verify-cancel','generation-verify-cancel','verify','unavailable',
              'active-unverified','active-verified','active-verified','available','account-verify','billing-verify','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:10.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-replace-complete-pending','profile-restore','generation-replacement','replace','pending',
              'active-verified','active-verified','active-verified','available','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:04.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-replace-complete-v1','root-replace-complete','intent-replace-complete-pending',1,NULL,'2026-08-10T00:00:04.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-replace-complete','profile-restore','generation-replacement','replace','completed',
              'active-verified','active-verified','active-verified','available','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:05.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-replace-complete-v2','root-replace-complete','intent-replace-complete',2,'event-replace-complete-v1','2026-08-10T00:00:05.0000000+00:00');
            UPDATE provider_profile_projection SET generation_id='generation-replacement',lifecycle_state='active-verified',
              verification_state='available',intent_id='intent-replace-complete',projection_version=4,
              updated_at='2026-08-10T00:00:05.0000000+00:00' WHERE profile_id='profile-restore';
            INSERT INTO evidence_acquisition_commands VALUES(
              'command-old-generation-after-replace','acquisition-restore','provider-operation','2026-08-10T00:00:00.0000000+00:00','recorded');
            INSERT INTO provider_command_bindings VALUES(
              'command-old-generation-after-replace','evidence-acquisition-run','acquisition-restore','2026-08-10T00:00:00.0000000+00:00');
            """;
        command.ExecuteNonQuery();
        command.CommandText = "SELECT generation_id FROM provider_profile_projection WHERE profile_id='profile-restore';";
        Assert.AreEqual("generation-replacement", (string)command.ExecuteScalar()!);

        command.CommandText =
            """
            INSERT INTO provider_operation_blocks
            SELECT 'operation-old-generation-after-replace',owner_kind,owner_id,job_node_id,'command-old-generation-after-replace',
              requested_at,confirmed_at,installation_snapshot_id,analysis_context_id,effective_configuration_id,
              resolved_input_manifest_id,profile_id,generation_id,revocation_epoch,operation_kind,capability_snapshot_id,
              price_snapshot_id,prompt_id,prompt_fingerprint,output_schema_id,output_schema_fingerprint,request_fingerprint,
              canonical_request_payload_id,canonical_request_fingerprint,canonical_request_bytes,settings_fingerprint,
              input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,maximum_request_bytes,
              maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,
              maximum_calculated_nano_usd,deadline_milliseconds,dispatch_deadline_utc,coordinator_fencing_epoch,state,recorded_at
            FROM provider_operation_blocks WHERE operation_id='operation-restore';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6CancelledDeletePermitsRetryFailedDeleteWedgesAndUnavailableOutcomesMaterialize()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-cancel-pending','profile-restore','generation-restore','delete','pending',
              'active-verified','delete-pending','active-verified','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','pending','2026-08-10T00:00:02.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-delete-cancel-v1','root-delete-cancel','intent-delete-cancel-pending',1,NULL,'2026-08-10T00:00:02.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-cancelled','profile-restore','generation-restore','delete','cancelled',
              'active-verified','delete-pending','active-verified','available','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-delete-cancel-v2','root-delete-cancel','intent-delete-cancelled',2,'event-delete-cancel-v1','2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-retry','profile-restore','generation-restore','delete','pending',
              'active-verified','delete-pending','active-verified','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','pending','2026-08-10T00:00:04.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-delete-retry-v1','root-delete-retry','intent-delete-retry',1,NULL,'2026-08-10T00:00:04.0000000+00:00');
            UPDATE provider_profile_projection SET lifecycle_state='delete-pending',verification_state='unavailable',
              intent_id='intent-delete-retry',cleanup_disposition='pending',projection_version=4,
              updated_at='2026-08-10T00:00:04.0000000+00:00' WHERE profile_id='profile-restore';
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-failed-pending','profile-restore','generation-restore','delete','pending',
              'delete-pending','delete-pending','delete-pending','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','failed','2026-08-10T00:00:04.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-delete-failed-v1','root-delete-failed','intent-delete-failed-pending',1,NULL,'2026-08-10T00:00:04.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-failed','profile-restore','generation-restore','delete','failed',
              'delete-pending','delete-pending','delete-pending','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','failed','2026-08-10T00:00:05.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-delete-failed-v2','root-delete-failed','intent-delete-failed',2,'event-delete-failed-v1','2026-08-10T00:00:05.0000000+00:00');
            UPDATE provider_profile_projection SET intent_id='intent-delete-failed',cleanup_disposition='failed',
              projection_version=5,updated_at='2026-08-10T00:00:05.0000000+00:00' WHERE profile_id='profile-restore';

            INSERT INTO provider_access_profiles VALUES(
              'profile-unavailable','openai','responses','Unavailable projection','account-unavailable','billing-unavailable',
              '2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_generations VALUES(
              'generation-unavailable','profile-unavailable',1,0,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-recovery-materialized-pending','profile-unavailable','generation-unavailable','enroll','pending',
              'none','pending-enrollment','none','not-applicable',NULL,NULL,NULL,
              'not-required','not-requested','2026-08-10T00:00:00.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-recovery-materialized-v1','root-recovery-materialized','intent-recovery-materialized-pending',1,NULL,'2026-08-10T00:00:00.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-recovery-materialized','profile-unavailable','generation-unavailable','enroll','unavailable',
              'none','pending-enrollment','recovery-required','unavailable','account-unavailable','billing-unavailable','cap-restore',
              'required','not-requested','2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-recovery-materialized-v2','root-recovery-materialized','intent-recovery-materialized',2,'event-recovery-materialized-v1','2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_profile_projection VALUES(
              'profile-unavailable','generation-unavailable',0,'recovery-required','unavailable','cap-restore',
              'account-unavailable','billing-unavailable','intent-recovery-materialized','required','not-requested',1,
              '2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-secure-store-materialized-pending','profile-unavailable','generation-unavailable','recover','pending',
              'recovery-required','active-unverified','recovery-required','unavailable','account-unavailable','billing-unavailable','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:01.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-secure-store-materialized-v1','root-secure-store-materialized','intent-secure-store-materialized-pending',1,NULL,'2026-08-10T00:00:01.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-secure-store-materialized','profile-unavailable','generation-unavailable','recover','unavailable',
              'recovery-required','active-unverified','secure-store-unavailable','unavailable','account-unavailable','billing-unavailable','cap-restore',
              'unavailable','not-requested','2026-08-10T00:00:02.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-secure-store-materialized-v2','root-secure-store-materialized','intent-secure-store-materialized',2,'event-secure-store-materialized-v1','2026-08-10T00:00:02.0000000+00:00');
            UPDATE provider_profile_projection SET lifecycle_state='secure-store-unavailable',
              intent_id='intent-secure-store-materialized',recovery_disposition='unavailable',projection_version=2,
              updated_at='2026-08-10T00:00:02.0000000+00:00' WHERE profile_id='profile-unavailable';
            """;
        command.ExecuteNonQuery();
        command.CommandText =
            "SELECT lifecycle_state FROM provider_profile_projection WHERE profile_id='profile-unavailable';";
        Assert.AreEqual("secure-store-unavailable", (string)command.ExecuteScalar()!);

        command.CommandText =
            """
            UPDATE provider_profile_projection SET lifecycle_state='deleted',verification_state='unavailable',
              capability_snapshot_id=NULL,account_identity_id=NULL,billing_scope_identity_id=NULL,intent_id=NULL,
              cleanup_disposition='confirmed',projection_version=6,
              updated_at='2026-08-10T00:00:05.5000000+00:00' WHERE profile_id='profile-restore';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-completed-pending','profile-restore','generation-restore','delete','pending',
              'delete-pending','deleted','delete-pending','unavailable',NULL,NULL,NULL,
              'not-required','confirmed','2026-08-10T00:00:06.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES(
              'event-delete-completed-v1','root-delete-completed','intent-delete-completed-pending',1,NULL,
              '2026-08-10T00:00:06.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-completed','profile-restore','generation-restore','delete','completed',
              'delete-pending','deleted','deleted','unavailable',NULL,NULL,NULL,
              'not-required','confirmed','2026-08-10T00:00:07.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES(
              'event-delete-completed-v2','root-delete-completed','intent-delete-completed',2,
              'event-delete-completed-v1','2026-08-10T00:00:07.0000000+00:00');
            UPDATE provider_profile_projection SET lifecycle_state='deleted',verification_state='unavailable',
              capability_snapshot_id=NULL,account_identity_id=NULL,billing_scope_identity_id=NULL,intent_id=NULL,
              cleanup_disposition='confirmed',projection_version=6,
              updated_at='2026-08-10T00:00:07.0000000+00:00' WHERE profile_id='profile-restore';
            """;
        Assert.AreEqual(5, command.ExecuteNonQuery());
        command.CommandText =
            "SELECT lifecycle_state || ':' || coalesce(intent_id,'redacted') FROM provider_profile_projection WHERE profile_id='profile-restore';";
        Assert.AreEqual("deleted:redacted", (string)command.ExecuteScalar()!);

        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-reactivate-after-delete-failure','profile-restore','generation-restore','recover','pending',
              'delete-pending','active-verified','delete-pending','available','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:08.0000000+00:00');
            """;
        SqliteException wedged = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(wedged.Message, "delete-pending provider profile cannot reactivate");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Schema6CredentialIntentPendingToTerminalIsAnAppendOnlyVersionedEventChain()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-disable-chain-pending','profile-restore','generation-restore','disable','pending',
              'active-verified','disabled','active-verified','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:03.0000000+00:00');
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_credential_intent_events VALUES(
              'intent-event-disable-early','intent-root-disable','intent-disable-chain-pending',1,NULL,
              '2026-08-10T00:00:02.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_credential_intent_events VALUES(
              'intent-event-disable-v1','intent-root-disable','intent-disable-chain-pending',1,NULL,
              '2026-08-10T00:00:03.0000000+00:00');
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-disable-chain-completed','profile-restore','generation-restore','disable','completed',
              'active-verified','disabled','disabled','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:04.0000000+00:00');
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-disable-chain-failed-too','profile-restore','generation-restore','disable','failed',
              'active-verified','disabled','active-verified','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:04.1000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_credential_intent_events VALUES(
              'intent-event-disable-v2','intent-root-disable','intent-disable-chain-completed',2,'intent-event-disable-v1',
              '2026-08-10T00:00:04.0000000+00:00');
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());

        command.CommandText =
            """
            UPDATE provider_profile_projection SET lifecycle_state='disabled',verification_state='unavailable',
              intent_id='intent-disable-chain-completed',projection_version=4,
              updated_at='2026-08-10T00:00:04.0000000+00:00' WHERE profile_id='profile-restore';
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-recover-no-event','profile-restore','generation-restore','recover','completed',
              'disabled','active-unverified','active-unverified','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:06.0000000+00:00');
            UPDATE provider_profile_projection SET lifecycle_state='active-unverified',verification_state='unavailable',
              intent_id='intent-recover-no-event',projection_version=5,
              updated_at='2026-08-10T00:00:06.0000000+00:00' WHERE profile_id='profile-restore';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            UPDATE provider_credential_intent_events SET event_version=3
            WHERE intent_event_id='intent-event-disable-v2';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO provider_credential_intent_events VALUES(
              'intent-event-disable-rebind','intent-root-other','intent-disable-chain-completed',2,'intent-event-disable-v1',
              '2026-08-10T00:00:04.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-disable-chain-late','profile-restore','generation-restore','disable','completed',
              'active-verified','disabled','disabled','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:05.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES(
              'intent-event-disable-v3','intent-root-disable','intent-disable-chain-late',3,'intent-event-disable-v2',
              '2026-08-10T00:00:05.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            DELETE FROM provider_credential_intent_events
            WHERE intent_event_id='intent-event-disable-v2';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void Schema6ResponseFinalizationRequiresOneUsageRowAndClosedRateCardinality()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys=OFF; DROP TRIGGER provider_cancelled_response_operation_root_guard;";
        command.ExecuteNonQuery();

        command.CommandText =
            """
            INSERT INTO provider_responses(
              response_record_id,availability,usage_availability,authorization_id,operation_id,owner_kind,owner_id,
              request_id,provider_attempt_id,reservation_id,operation_kind,
              maximum_input_tokens,maximum_output_tokens,maximum_calculated_nano_usd,
              raw_response_availability,maximum_raw_response_bytes,response_headers_availability,
              http_status_availability,provider_response_id_availability,client_request_id_availability,
              provider_request_id_availability,billing_evidence_availability,response_state,
              refusal_availability,incomplete_availability,error_availability,requested_model,
              returned_model_availability,requested_service_tier,returned_service_tier_availability,
              reasoning_context,reasoning_mode,prompt_cache_mode,billing_availability,rate_availability,
              expected_rate_limit_fact_count,credit_availability,validation_state,admission_state,created_at)
            VALUES(
              'response-cancelled','unavailable','unavailable','authorization-cancelled',$operation_id,'evidence-acquisition-run',$owner_id,
              'request-cancelled','attempt-cancelled','reservation-cancelled','source-claim-extraction',
              $maximum_input_tokens,4096,600000000,'unavailable',1048576,$response_headers_availability,'unavailable','unavailable','unavailable',
              'unavailable','unavailable','cancelled','unavailable','unavailable','unavailable','gpt-5.6-sol',
              'unavailable','default','unavailable','current_turn','standard','explicit','unavailable','unavailable',
              0,'unavailable','unavailable','unavailable','2026-08-10T00:00:01.0000000+00:00');
            """;
        command.Parameters.AddWithValue("$operation_id", "operation-restore");
        command.Parameters.AddWithValue("$owner_id", "acquisition-restore");
        command.Parameters.AddWithValue("$response_headers_availability", "unavailable");
        command.Parameters.AddWithValue("$maximum_input_tokens", 73728);
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.Parameters.Clear();
        command.CommandText =
            """
            INSERT INTO provider_response_finalizations VALUES(
              'finalization-cancelled','response-cancelled','usage-cancelled','unavailable','unavailable',
              '2026-08-10T00:00:01.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO provider_usage_entries(
              usage_entry_id,receipt_id,availability,operation_id,provider_attempt_id,request_id,response_record_id,
              dispatch_count_availability,dispatch_count,input_tokens_availability,output_tokens_availability,
              total_tokens_availability,reasoning_tokens_availability,cache_read_tokens_availability,
              cache_write_tokens_availability,priced_tool_calls_availability,calculated_nano_usd_availability,
              billing_availability,rate_availability,credit_availability,receipt_state,created_at)
            VALUES(
              'usage-cancelled','receipt-cancelled','unavailable','operation-restore','attempt-cancelled','request-cancelled','response-cancelled','available',0,
              'unavailable','unavailable','unavailable','unavailable','unavailable','unavailable','unavailable',
              'unavailable','unavailable','unavailable','unavailable','not-dispatched',
              '2026-08-10T00:00:01.0000000+00:00');
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_rate_limit_facts VALUES(
              'rate-cancelled','usage-cancelled','request','requests','available',100,99,
              '2026-08-10T00:00:01.0000000+00:00',NULL);
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_response_finalizations VALUES(
              'finalization-cancelled','response-cancelled','usage-cancelled','unavailable','unavailable',
              '2026-08-10T00:00:02.0000000+00:00');
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_response_finalizations VALUES(
              'finalization-cancelled-duplicate','response-cancelled','usage-cancelled','unavailable','unavailable',
              '2026-08-10T00:00:02.5000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO provider_responses(
              response_record_id,availability,usage_availability,operation_id,owner_kind,owner_id,operation_kind,
              maximum_input_tokens,maximum_output_tokens,maximum_calculated_nano_usd,
              raw_response_availability,maximum_raw_response_bytes,response_headers_availability,
              http_status_availability,provider_response_id_availability,client_request_id_availability,
              provider_request_id_availability,billing_evidence_availability,response_state,
              refusal_availability,incomplete_availability,error_availability,requested_model,
              returned_model_availability,requested_service_tier,returned_service_tier_availability,
              reasoning_context,reasoning_mode,prompt_cache_mode,billing_availability,rate_availability,
              expected_rate_limit_fact_count,credit_availability,validation_state,admission_state,created_at)
            VALUES(
              'response-cancelled-inversion','unavailable','unavailable','operation-restore','evidence-acquisition-run','acquisition-restore','source-claim-extraction',
              73728,4096,600000000,'unavailable',1048576,'unavailable','unavailable','unavailable','unavailable',
              'unavailable','unavailable','cancelled','unavailable','unavailable','unavailable','gpt-5.6-sol',
              'unavailable','default','unavailable','current_turn','standard','explicit','unavailable','unavailable',
              0,'unavailable','unavailable','unavailable','2026-08-10T00:00:03.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_usage_entries(
              usage_entry_id,receipt_id,availability,operation_id,response_record_id,
              dispatch_count_availability,dispatch_count,input_tokens_availability,output_tokens_availability,
              total_tokens_availability,reasoning_tokens_availability,cache_read_tokens_availability,
              cache_write_tokens_availability,priced_tool_calls_availability,calculated_nano_usd_availability,
              billing_availability,rate_availability,credit_availability,receipt_state,created_at)
            VALUES(
              'usage-cancelled-inversion','receipt-cancelled-inversion','unavailable','operation-restore','response-cancelled-inversion','available',0,
              'unavailable','unavailable','unavailable','unavailable','unavailable','unavailable','unavailable',
              'unavailable','unavailable','unavailable','unavailable','not-dispatched',
              '2026-08-10T00:00:02.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        foreach (string lateEventSql in new[]
                 {
                     "INSERT INTO provider_operation_attempts(operation_id,created_at) VALUES('operation-restore','2026-08-10T00:00:05.0000000+00:00');",
                     "INSERT INTO provider_requests(operation_id,created_at) VALUES('operation-restore','2026-08-10T00:00:05.0000000+00:00');",
                     "INSERT INTO provider_reservations(operation_id,expires_at,created_at) VALUES('operation-restore','2026-08-10T00:01:05.0000000+00:00','2026-08-10T00:00:05.0000000+00:00');",
                     "INSERT INTO provider_dispatch_fences(operation_id,evaluated_at) VALUES('operation-restore','2026-08-10T00:00:05.0000000+00:00');",
                     "INSERT INTO provider_transport_events(operation_id,occurred_at) VALUES('operation-restore','2026-08-10T00:00:05.0000000+00:00');",
                 })
        {
            command.CommandText = lateEventSql;
            AssertCancelledTerminal(command);
        }
        command.CommandText = "PRAGMA foreign_keys=OFF; DROP TRIGGER provider_response_transport_binding_guard;";
        command.ExecuteNonQuery();

        command.CommandText =
            """
            INSERT INTO provider_responses(
              response_record_id,availability,usage_availability,authorization_id,operation_id,owner_kind,owner_id,request_id,
              provider_attempt_id,reservation_id,dispatch_fence_id,operation_kind,maximum_input_tokens,maximum_output_tokens,
              maximum_calculated_nano_usd,raw_response_availability,raw_response_payload_id,
              raw_response_fingerprint,raw_response_bytes,maximum_raw_response_bytes,response_headers_availability,
              http_status_availability,http_status,provider_response_id_availability,client_request_id_availability,
              provider_request_id_availability,billing_evidence_availability,response_state,refusal_availability,
              incomplete_availability,error_availability,requested_model,returned_model_availability,returned_model,
              requested_service_tier,returned_service_tier_availability,returned_service_tier,reasoning_context,
              reasoning_mode,prompt_cache_mode,billing_availability,rate_availability,expected_rate_limit_fact_count,
              credit_availability,validation_state,admission_state,created_at)
            VALUES(
              'response-completed','available','available','authorization-1','operation-completed','analysis-run','run-1','request-1',
              'attempt-1','reservation-1','fence-1','candidate-investigation',73728,4096,600000000,'available','raw-1',
              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',128,1048576,'unavailable',
              'available',200,'unavailable','unavailable','unavailable','unavailable','completed','unavailable',
              'unavailable','unavailable','gpt-5.6-sol','available','gpt-5.6-sol','default','available','default',
              'current_turn','standard','explicit','unavailable','available',2,'unavailable','proposed','proposed',
              '2026-08-10T00:01:00.0000000+00:00');
            INSERT INTO provider_usage_entries(
              usage_entry_id,receipt_id,availability,operation_id,provider_attempt_id,request_id,dispatch_fence_id,
              response_record_id,dispatch_count_availability,dispatch_count,input_tokens_availability,input_tokens,
              output_tokens_availability,output_tokens,total_tokens_availability,total_tokens,
              reasoning_tokens_availability,reasoning_tokens,cache_read_tokens_availability,cache_read_tokens,
              cache_write_tokens_availability,cache_write_tokens,priced_tool_calls_availability,priced_tool_calls,
              calculated_nano_usd_availability,calculated_nano_usd,billing_availability,rate_availability,
              credit_availability,receipt_state,created_at)
            VALUES(
              'usage-completed','receipt-completed','available','operation-completed','attempt-1','request-1','fence-1',
              'response-completed','available',1,'available',10,'available',5,'available',15,'available',2,
              'available',0,'available',0,'available',0,'available',100,'unavailable','available','unavailable',
              'complete','2026-08-10T00:01:01.0000000+00:00');
            """;
        Assert.AreEqual(2, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_response_finalizations VALUES(
              'finalization-completed','response-completed','usage-completed','admitted','admitted',
              '2026-08-10T00:00:59.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_rate_limit_facts VALUES(
              'rate-before-response','usage-completed','request','requests','available',100,99,
              '2026-08-10T00:00:59.0000000+00:00',NULL);
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_rate_limit_facts VALUES(
              'rate-before-usage','usage-completed','request','requests','available',100,99,
              '2026-08-10T00:01:00.5000000+00:00',NULL);
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_rate_limit_facts VALUES(
              'rate-completed','usage-completed','request','requests','available',100,99,
              '2026-08-10T00:01:01.0000000+00:00',NULL);
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_response_finalizations VALUES(
              'finalization-completed','response-completed','usage-completed','admitted','admitted',
              '2026-08-10T00:01:02.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_rate_limit_facts VALUES(
              'rate-completed-second','usage-completed','project','input-tokens','available',1000,900,
              '2026-08-10T00:01:03.0000000+00:00',NULL);
            INSERT INTO provider_response_finalizations VALUES(
              'finalization-completed','response-completed','usage-completed','admitted','admitted',
              '2026-08-10T00:01:02.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_response_finalizations VALUES(
              'finalization-completed','response-completed','usage-completed','admitted','admitted',
              '2026-08-10T00:01:04.0000000+00:00');
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_rate_limit_facts VALUES(
              'rate-after-finalization','usage-completed','project','requests','available',100,99,
              '2026-08-10T00:01:03.0000000+00:00',NULL);
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_usage_entries(
              usage_entry_id,receipt_id,availability,operation_id,provider_attempt_id,request_id,dispatch_fence_id,
              response_record_id,dispatch_count_availability,dispatch_count,input_tokens_availability,input_tokens,
              output_tokens_availability,output_tokens,total_tokens_availability,total_tokens,
              reasoning_tokens_availability,reasoning_tokens,cache_read_tokens_availability,cache_read_tokens,
              cache_write_tokens_availability,cache_write_tokens,priced_tool_calls_availability,priced_tool_calls,
              calculated_nano_usd_availability,calculated_nano_usd,billing_availability,rate_availability,
              credit_availability,receipt_state,created_at)
            VALUES(
              'usage-completed-late','receipt-completed-late','available','operation-completed','attempt-late','request-1','fence-1',
              'response-completed','available',1,'available',10,'available',5,'available',15,'available',2,
              'available',0,'available',0,'available',0,'available',100,'unavailable','available','unavailable',
              'complete','2026-08-10T00:01:03.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            DELETE FROM provider_rate_limit_facts WHERE rate_limit_fact_id='rate-completed';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            DROP TRIGGER provider_authority_release_required;
            DROP TRIGGER authorization_owner_job_guard;
            INSERT INTO provider_operation_authorizations(
              authorization_id,operation_id,owner_kind,owner_id,analysis_run_id,job_node_id,command_id,requested_at,
              profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
              effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
              output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
              price_snapshot_id,settings_fingerprint,input_bound_policy_id,input_bound_policy_version,
              input_bound_proof_status,coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,
              maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,
              deadline_milliseconds,dispatch_deadline_utc,confirmed_at)
            VALUES(
              'authorization-1','operation-completed','analysis-run','run-1','run-1','job-1','command-1',
              '2026-08-10T00:00:00.0000000+00:00','profile-1','generation-1',0,'candidate-investigation',
              'install-1','context-1','configuration-1','manifest-1','prompt-1',
              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa','schema-1',
              'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
              'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc',
              'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc',
              'capability-1','price-1','dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd',
              'openai-responses-o200k-byte-envelope','v1','proved',1,65536,73728,4096,1048576,1,600000000,120000,
              '2026-08-10T00:02:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO analysis_candidates VALUES(
              'candidate-future','decision-future','run-1','mandatory-evidence','present','closure-1','payload-1',
              '2026-08-10T00:01:06.0000000+00:00');
            INSERT INTO analysis_candidates VALUES(
              'candidate-early','decision-early','run-1','mandatory-evidence','present','closure-1','payload-1',
              '2026-08-10T00:01:03.0000000+00:00');
            INSERT INTO analysis_candidates VALUES(
              'candidate-valid','decision-valid','run-1','mandatory-evidence','present','closure-1','payload-1',
              '2026-08-10T00:01:03.0000000+00:00');
            INSERT INTO evidence_application_links VALUES(
              'application-early','evidence-1','run-1','binding-1','context-1','subject-1','plugin','closure-1',
              'applicable','payload-1','2026-08-10T00:01:03.0000000+00:00');
            INSERT INTO evidence_application_links VALUES(
              'application-future','evidence-2','run-1','binding-2','context-1','subject-2','plugin','closure-1',
              'applicable','payload-1','2026-08-10T00:01:06.0000000+00:00');
            INSERT INTO evidence_application_links VALUES(
              'application-valid','evidence-3','run-1','binding-3','context-1','subject-3','plugin','closure-1',
              'applicable','payload-1','2026-08-10T00:01:03.0000000+00:00');
            """;
        Assert.AreEqual(7, command.ExecuteNonQuery());
        command.CommandText = "PRAGMA foreign_keys=ON;";
        command.ExecuteNonQuery();
        command.CommandText = "PRAGMA foreign_keys;";
        Assert.AreEqual(1L, (long)command.ExecuteScalar()!);
        command.CommandText =
            """
            INSERT INTO provider_responses(
              response_record_id,availability,usage_availability,operation_id,owner_kind,owner_id,operation_kind,
              maximum_input_tokens,maximum_output_tokens,maximum_calculated_nano_usd,
              raw_response_availability,maximum_raw_response_bytes,response_headers_availability,
              http_status_availability,provider_response_id_availability,client_request_id_availability,
              provider_request_id_availability,billing_evidence_availability,response_state,
              refusal_availability,incomplete_availability,error_availability,requested_model,
              returned_model_availability,requested_service_tier,returned_service_tier_availability,
              reasoning_context,reasoning_mode,prompt_cache_mode,billing_availability,rate_availability,
              expected_rate_limit_fact_count,credit_availability,validation_state,admission_state,created_at)
            VALUES(
              'response-cancelled-authorized','unavailable','unavailable','operation-completed','analysis-run','run-1','candidate-investigation',
              73728,4096,600000000,'unavailable',1048576,'unavailable','unavailable','unavailable','unavailable',
              'unavailable','unavailable','cancelled','unavailable','unavailable','unavailable','gpt-5.6-sol',
              'unavailable','default','unavailable','current_turn','standard','explicit','unavailable','unavailable',
              0,'unavailable','unavailable','unavailable','2026-08-10T00:01:05.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText = "PRAGMA foreign_keys=OFF;";
        command.ExecuteNonQuery();
        command.CommandText =
            """
            INSERT INTO provider_semantic_proposals(
              proposal_id,authorization_id,operation_id,provider_attempt_id,request_id,response_record_id,
              dispatch_fence_id,owner_kind,owner_id,root_subject_id,semantic_link_id,proposal_kind,payload_id,created_at)
            VALUES('proposal-future-candidate','authorization-1','operation-completed','attempt-1','request-1','response-completed',
              'fence-1','analysis-run','run-1','candidate-future','application-early','candidate-hypothesis','payload-1',
              '2026-08-10T00:01:05.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText = command.CommandText
            .Replace("proposal-future-candidate", "proposal-future-application", StringComparison.Ordinal)
            .Replace("candidate-future", "candidate-early", StringComparison.Ordinal)
            .Replace("application-early", "application-future", StringComparison.Ordinal);
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText = command.CommandText
            .Replace("proposal-future-application", "proposal-valid-roots", StringComparison.Ordinal)
            .Replace("candidate-early", "candidate-valid", StringComparison.Ordinal)
            .Replace("application-future", "application-valid", StringComparison.Ordinal);
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_operation_authorizations(
              authorization_id,operation_id,owner_kind,owner_id,evidence_acquisition_run_id,job_node_id,command_id,requested_at,
              profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
              effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
              output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
              price_snapshot_id,settings_fingerprint,input_bound_policy_id,input_bound_policy_version,
              input_bound_proof_status,coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,
              maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,
              deadline_milliseconds,dispatch_deadline_utc,confirmed_at)
            VALUES(
              'authorization-source','operation-source','evidence-acquisition-run','acquisition-restore','acquisition-restore',
              'job-source','command-source','2026-08-10T00:00:00.0000000+00:00','profile-1','generation-1',0,
              'source-claim-extraction','install-1','context-1','configuration-1','manifest-1','prompt-1',
              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa','schema-1',
              'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',
              'eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee',
              'eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee',
              'capability-1','price-1','dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd',
              'openai-responses-o200k-byte-envelope','v1','proved',1,65536,73728,4096,1048576,1,600000000,120000,
              '2026-08-10T00:02:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_responses(
              response_record_id,availability,usage_availability,authorization_id,operation_id,owner_kind,owner_id,request_id,
              provider_attempt_id,reservation_id,dispatch_fence_id,operation_kind,maximum_input_tokens,maximum_output_tokens,
              maximum_calculated_nano_usd,raw_response_availability,raw_response_payload_id,
              raw_response_fingerprint,raw_response_bytes,maximum_raw_response_bytes,response_headers_availability,
              http_status_availability,http_status,provider_response_id_availability,client_request_id_availability,
              provider_request_id_availability,billing_evidence_availability,response_state,refusal_availability,
              incomplete_availability,error_availability,requested_model,returned_model_availability,returned_model,
              requested_service_tier,returned_service_tier_availability,returned_service_tier,reasoning_context,
              reasoning_mode,prompt_cache_mode,billing_availability,rate_availability,expected_rate_limit_fact_count,
              credit_availability,validation_state,admission_state,created_at)
            VALUES(
              'response-source','available','available','authorization-source','operation-source','evidence-acquisition-run',
              'acquisition-restore','request-source','attempt-source','reservation-source','fence-source','source-claim-extraction',73728,4096,
              600000000,'available','raw-source','ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff',
              128,1048576,'unavailable','available',200,'unavailable','unavailable','unavailable','unavailable','completed',
              'unavailable','unavailable','unavailable','gpt-5.6-sol','available','gpt-5.6-sol','default','available','default',
              'current_turn','standard','explicit','unavailable','unavailable',0,'unavailable','proposed','proposed',
              '2026-08-10T00:01:10.0000000+00:00');
            INSERT INTO provider_usage_entries(
              usage_entry_id,receipt_id,availability,operation_id,provider_attempt_id,request_id,dispatch_fence_id,response_record_id,
              dispatch_count_availability,dispatch_count,input_tokens_availability,input_tokens,output_tokens_availability,
              output_tokens,total_tokens_availability,total_tokens,reasoning_tokens_availability,reasoning_tokens,
              cache_read_tokens_availability,cache_read_tokens,cache_write_tokens_availability,cache_write_tokens,
              priced_tool_calls_availability,priced_tool_calls,calculated_nano_usd_availability,calculated_nano_usd,
              billing_availability,rate_availability,credit_availability,receipt_state,created_at)
            VALUES(
              'usage-source','receipt-source','available','operation-source','attempt-source','request-source','fence-source','response-source',
              'available',1,'available',10,'available',5,'available',15,'available',2,'available',0,'available',0,
              'available',0,'available',100,'unavailable','unavailable','unavailable','complete',
              '2026-08-10T00:01:11.0000000+00:00');
            INSERT INTO provider_response_finalizations VALUES(
              'finalization-source','response-source','usage-source','admitted','admitted',
              '2026-08-10T00:01:12.0000000+00:00');
            INSERT INTO documentation_revisions VALUES(
              'source-future','source-1','fixture','1',NULL,NULL,
              'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',0,'unavailable','unavailable','unavailable',
              '2026-08-10T00:01:15.0000000+00:00');
            INSERT INTO documentation_revisions VALUES(
              'source-valid','source-2','fixture','1',NULL,NULL,
              'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb',0,'unavailable','unavailable','unavailable',
              '2026-08-10T00:01:10.0000000+00:00');
            """;
        Assert.AreEqual(6, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO evidence_acquisition_application_links VALUES(
              'source-application-premature','acquisition-restore','admission-premature','run-restore',
              'application-restore','cost-restore','payload-1',
              '2026-08-10T00:01:10.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_semantic_proposals(
              proposal_id,authorization_id,operation_id,provider_attempt_id,request_id,response_record_id,
              dispatch_fence_id,owner_kind,owner_id,root_subject_id,semantic_link_id,proposal_kind,payload_id,created_at)
            VALUES('proposal-future-source','authorization-source','operation-source','attempt-source','request-source','response-source',
              'fence-source','evidence-acquisition-run','acquisition-restore','source-future','provider-returned-application',
              'source-claim','payload-1','2026-08-10T00:01:14.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText = command.CommandText
            .Replace("proposal-future-source", "proposal-valid-source-roots", StringComparison.Ordinal)
            .Replace("source-future", "source-valid", StringComparison.Ordinal);
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_semantic_validations VALUES(
              'validation-valid-source','proposal-valid-source-roots','operation-source','response-source',
              'evidence-acquisition-run','acquisition-restore','source-valid','admitted','policy-1','synthetic',
              '2026-08-10T00:01:15.0000000+00:00');
            INSERT INTO provider_semantic_admissions VALUES(
              'admission-valid-source','proposal-valid-source-roots','operation-source','response-source',
              'evidence-acquisition-run','acquisition-restore','source-valid','validation-valid-source',
              'provider-returned-application','admitted','policy-1','synthetic','payload-1',
              '2026-08-10T00:01:16.0000000+00:00');
            INSERT INTO evidence_acquisition_application_links VALUES(
              'consumer-application-valid','acquisition-restore','admission-valid-source','run-restore',
              'application-restore','cost-restore','payload-1',
              '2026-08-10T00:01:17.0000000+00:00');
            """;
        Assert.AreEqual(3, command.ExecuteNonQuery());
        command.CommandText =
            """
            DROP TRIGGER provider_semantic_proposal_root_guard;
            DROP TRIGGER provider_semantic_admission_application_guard;
            INSERT INTO provider_semantic_proposals(
              proposal_id,authorization_id,operation_id,provider_attempt_id,request_id,response_record_id,
              dispatch_fence_id,owner_kind,owner_id,root_subject_id,semantic_link_id,proposal_kind,payload_id,created_at)
            VALUES('proposal-chronology','authorization-1','operation-completed','attempt-1','request-1','response-completed',
              'fence-1','analysis-run','run-1','candidate-1','application-1','candidate-hypothesis','payload-1',
              '2026-08-10T00:01:03.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_semantic_proposals(
              proposal_id,authorization_id,operation_id,provider_attempt_id,request_id,response_record_id,
              dispatch_fence_id,owner_kind,owner_id,root_subject_id,semantic_link_id,proposal_kind,payload_id,created_at)
            VALUES('proposal-chronology','authorization-1','operation-completed','attempt-1','request-1','response-completed',
              'fence-1','analysis-run','run-1','candidate-1','application-1','candidate-hypothesis','payload-1',
              '2026-08-10T00:01:05.0000000+00:00');
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_semantic_validations(
              validation_id,proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id,
              state,host_policy_id,reason,created_at)
            VALUES('validation-chronology','proposal-chronology','operation-completed','response-completed',
              'analysis-run','run-1','candidate-1','admitted','policy-1','synthetic',
              '2026-08-10T00:01:04.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText = command.CommandText.Replace("00:01:04", "00:01:06", StringComparison.Ordinal);
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_semantic_admissions(
              admission_id,proposal_id,operation_id,response_record_id,owner_kind,owner_id,root_subject_id,
              validation_id,semantic_link_id,state,host_policy_id,reason,admitted_artifact_id,created_at)
            VALUES('admission-chronology','proposal-chronology','operation-completed','response-completed',
              'analysis-run','run-1','candidate-1','validation-chronology','application-1','admitted',
              'policy-1','synthetic',NULL,'2026-08-10T00:01:05.0000000+00:00');
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText = command.CommandText.Replace("00:01:05", "00:01:07", StringComparison.Ordinal);
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            DELETE FROM provider_response_finalizations WHERE finalization_id='finalization-completed';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6SettlementRetainsObservedUsageBelowEqualAndAboveReservation()
    {
        AssertSqlSettlement(1, 9, 4, 1, 0, 0, 0, 90, "settled", succeeds: true);
        AssertSqlSettlement(1, 10, 5, 2, 0, 0, 0, 100, "settled", succeeds: true);
        AssertSqlSettlement(2, 10, 5, 2, 0, 0, 0, 100, "overrun", succeeds: true);
        AssertSqlSettlement(1, 11, 5, 2, 0, 0, 0, 100, "overrun", succeeds: true);
        AssertSqlSettlement(1, 10, 6, 2, 0, 0, 0, 100, "overrun", succeeds: true);
        AssertSqlSettlement(1, 10, 5, 3, 0, 0, 0, 100, "overrun", succeeds: true);
        AssertSqlSettlement(1, 10, 5, 2, 1, 0, 0, 100, "overrun", succeeds: true);
        AssertSqlSettlement(1, 10, 5, 2, 0, 1, 0, 100, "overrun", succeeds: true);
        AssertSqlSettlement(1, 10, 5, 2, 0, 0, 1, 100, "overrun", succeeds: true);
        AssertSqlSettlement(1, 10, 5, 2, 0, 0, 0, 101, "overrun", succeeds: true);
        AssertSqlSettlement(2, 10, 5, 2, 0, 0, 0, 100, "settled", succeeds: false);
        AssertSqlSettlement(1, 11, 5, 2, 0, 0, 0, 100, "settled", succeeds: false);
        AssertSqlSettlement(1, 10, 5, 2, 0, 0, 0, 100, "overrun", succeeds: false);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6CancelledReservedUndispatchedOperationReleasesItsExactReservation()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA foreign_keys=ON;
            DROP TRIGGER provider_authority_release_required;
            INSERT INTO provider_operation_authorizations(
              authorization_id,operation_id,owner_kind,owner_id,evidence_acquisition_run_id,job_node_id,command_id,requested_at,
              profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
              effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
              output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
              price_snapshot_id,settings_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,
              coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,
              maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,
              dispatch_deadline_utc,confirmed_at)
            SELECT 'authorization-cancelled-reserved',operation_id,owner_kind,owner_id,owner_id,job_node_id,command_id,requested_at,
              profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
              effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
              output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
              price_snapshot_id,settings_fingerprint,'openai-responses-o200k-byte-envelope','v1','proved',coordinator_fencing_epoch,
              maximum_request_bytes,20,10,maximum_raw_response_bytes,maximum_dispatch_count,200,deadline_milliseconds,
              dispatch_deadline_utc,confirmed_at
            FROM provider_operation_blocks WHERE operation_id='operation-restore';
            INSERT INTO provider_operation_attempts VALUES(
              'attempt-cancelled-reserved','operation-restore',1,'proposed',1,'2026-08-10T00:00:02.0000000+00:00');
            INSERT INTO provider_requests(
              request_id,client_request_id,operation_id,provider_attempt_id,request_fingerprint,
              canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,
              input_bound_policy_version,input_bound_proof_status,payload_id,payload_fingerprint,payload_bytes,created_at)
            SELECT 'request-cancelled-reserved','client-request-cancelled-reserved',operation_id,
              'attempt-cancelled-reserved',request_fingerprint,canonical_request_fingerprint,settings_fingerprint,
              output_schema_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,
              'request-payload-restore',request_fingerprint,1024,'2026-08-10T00:00:03.0000000+00:00'
            FROM provider_operation_authorizations WHERE authorization_id='authorization-cancelled-reserved';
            INSERT INTO provider_reservations VALUES(
              'reservation-cancelled-reserved','operation-restore','attempt-cancelled-reserved','request-cancelled-reserved',
              '{"dispatch_count":1,"input_tokens":10,"output_tokens":5,"total_tokens":15,"reasoning_tokens":2,"cache_read_tokens":0,"cache_write_tokens":0,"priced_tool_calls":0,"calculated_nano_usd":100}',
              1,10,5,2,0,0,0,100,'2026-08-10T00:01:30.0000000+00:00','2026-08-10T00:00:04.0000000+00:00');
            INSERT INTO provider_reservation_scope_items VALUES(
              'scope-cancelled-reserved','reservation-cancelled-reserved','operation','operation-restore',
              '{"dispatch_count":1,"input_tokens":10,"output_tokens":5,"total_tokens":15,"reasoning_tokens":2,"cache_read_tokens":0,"cache_write_tokens":0,"priced_tool_calls":0,"calculated_nano_usd":100}',100);
            INSERT INTO provider_responses(
              response_record_id,availability,usage_availability,authorization_id,operation_id,owner_kind,owner_id,
              request_id,provider_attempt_id,reservation_id,operation_kind,maximum_input_tokens,maximum_output_tokens,
              maximum_calculated_nano_usd,raw_response_availability,maximum_raw_response_bytes,response_headers_availability,
              http_status_availability,provider_response_id_availability,client_request_id_availability,
              provider_request_id_availability,billing_evidence_availability,response_state,refusal_availability,
              incomplete_availability,error_availability,requested_model,returned_model_availability,requested_service_tier,
              returned_service_tier_availability,reasoning_context,reasoning_mode,prompt_cache_mode,billing_availability,
              rate_availability,expected_rate_limit_fact_count,credit_availability,validation_state,admission_state,created_at)
            SELECT 'response-cancelled-reserved','unavailable','unavailable','authorization-cancelled-reserved',operation_id,
              owner_kind,owner_id,'request-cancelled-reserved','attempt-cancelled-reserved','reservation-cancelled-reserved',
              operation_kind,maximum_input_tokens,maximum_output_tokens,maximum_calculated_nano_usd,'unavailable',
              maximum_raw_response_bytes,'unavailable','unavailable','unavailable','unavailable','unavailable','unavailable',
              'cancelled','unavailable','unavailable','unavailable','gpt-5.6-sol','unavailable','default','unavailable',
              'current_turn','standard','explicit','unavailable','unavailable',0,'unavailable','unavailable','unavailable',
              '2026-08-10T00:00:05.0000000+00:00'
            FROM provider_operation_authorizations WHERE authorization_id='authorization-cancelled-reserved';
            INSERT INTO provider_usage_entries(
              usage_entry_id,receipt_id,availability,operation_id,provider_attempt_id,request_id,response_record_id,
              dispatch_count_availability,dispatch_count,input_tokens_availability,output_tokens_availability,
              total_tokens_availability,reasoning_tokens_availability,cache_read_tokens_availability,
              cache_write_tokens_availability,priced_tool_calls_availability,calculated_nano_usd_availability,
              billing_availability,rate_availability,credit_availability,receipt_state,created_at)
            VALUES('usage-cancelled-reserved','receipt-cancelled-reserved','unavailable','operation-restore','attempt-cancelled-reserved',
              'request-cancelled-reserved','response-cancelled-reserved','available',0,'unavailable','unavailable',
              'unavailable','unavailable','unavailable','unavailable','unavailable','unavailable','unavailable',
              'unavailable','unavailable','not-dispatched','2026-08-10T00:00:06.0000000+00:00');
            INSERT INTO provider_response_finalizations VALUES(
              'finalization-cancelled-reserved','response-cancelled-reserved','usage-cancelled-reserved','unavailable',
              'unavailable','2026-08-10T00:00:07.0000000+00:00');
            """;
        command.ExecuteNonQuery();

        const string validSettlement =
            """
            INSERT INTO provider_settlements VALUES(
              'settlement-cancelled-reserved','operation-restore','attempt-cancelled-reserved',
              'request-cancelled-reserved','reservation-cancelled-reserved','usage-cancelled-reserved',NULL,
              'settled',100,0,'2026-08-10T00:00:08.0000000+00:00');
            """;
        command.CommandText = validSettlement.Replace("'settled',100,0", "'settled',99,1", StringComparison.Ordinal);
        SqliteException strandedHold = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(strandedHold.Message, "exactly partition the reservation");

        command.CommandText = validSettlement.Replace(
            "'reservation-cancelled-reserved','usage-cancelled-reserved'",
            "'reservation-mismatched','usage-cancelled-reserved'",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

        command.CommandText = validSettlement;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            SELECT count(*) FROM provider_settlement_vector_partitions
            WHERE settlement_id='settlement-cancelled-reserved'
              AND state='settled'
              AND released_dispatch_count=reserved_dispatch_count
              AND released_input_tokens=reserved_input_tokens
              AND released_output_tokens=reserved_output_tokens
              AND released_total_tokens=reserved_total_tokens
              AND released_reasoning_tokens=reserved_reasoning_tokens
              AND released_cache_read_tokens=reserved_cache_read_tokens
              AND released_cache_write_tokens=reserved_cache_write_tokens
              AND released_priced_tool_calls=reserved_priced_tool_calls
              AND released_nano_usd=reserved_nano_usd
              AND retained_dispatch_count=0 AND retained_input_tokens=0 AND retained_output_tokens=0
              AND retained_total_tokens=0 AND retained_reasoning_tokens=0 AND retained_cache_read_tokens=0
              AND retained_cache_write_tokens=0 AND retained_priced_tool_calls=0 AND retained_hold_nano_usd=0;
            """;
        Assert.AreEqual(1L, (long)command.ExecuteScalar()!);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6ProjectionTimesAreCanonicalSevenDigitUtcAndAdvanceMonotonically()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA foreign_keys=ON;
            UPDATE provider_operation_projection
            SET projection_version=2,updated_at='2026-08-10T00:00:00.1239999+00:00'
            WHERE operation_id='operation-restore';
            UPDATE provider_operation_projection
            SET projection_version=3,updated_at='2026-08-10T00:00:00.9999999+00:00'
            WHERE operation_id='operation-restore';
            UPDATE provider_profile_projection
            SET projection_version=4,updated_at='2026-08-10T00:00:02.9999999+00:00'
            WHERE profile_id='profile-restore';
            """;
        Assert.AreEqual(3, command.ExecuteNonQuery());

        command.CommandText =
            """
            UPDATE provider_operation_projection
            SET projection_version=4,updated_at='2026-08-10T00:00:00.9999998+00:00'
            WHERE operation_id='operation-restore';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            UPDATE provider_operation_projection
            SET projection_version=4,updated_at='2026-08-10T00:00:01.000000A+00:00'
            WHERE operation_id='operation-restore';
            """;
        SqliteException malformed = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(malformed.Message, "non-canonical UTC authority timestamp");
        foreach (string invalidUpdate in new[]
                 {
                     "0000-01-01T00:00:00.0000000+00:00",
                     "2026-08-10T24:00:00.0000000+00:00",
                 })
        {
            command.CommandText =
                """
                UPDATE provider_operation_projection
                SET projection_version=4,updated_at=$invalid
                WHERE operation_id='operation-restore';
                """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$invalid", invalidUpdate);
            malformed = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
            StringAssert.Contains(malformed.Message, "non-canonical UTC authority timestamp");
        }

        command.CommandText =
            """
            INSERT INTO provider_budget_limits VALUES(
              'operation','operation-restore',0,0,0,0,0,0,0,0,0,
              'local-hard-limit','2026-08-10T00:00:00.9999998+00:00');
            INSERT INTO provider_budget_projection VALUES(
              'operation','operation-restore',
              0,0,0,0,0,0,0,0,0,
              0,0,0,0,0,0,0,0,0,
              0,0,0,0,0,0,0,0,0,
              1,'2026-08-10T00:00:00.9999999+00:00');
            UPDATE provider_budget_projection
            SET projection_version=2,updated_at='2026-08-10T00:00:01.0000000+00:00'
            WHERE scope_kind='operation' AND scope_id='operation-restore';
            """;
        Assert.AreEqual(3, command.ExecuteNonQuery());
        command.CommandText =
            """
            UPDATE provider_budget_projection
            SET projection_version=2,updated_at='2026-08-10T00:00:02.0000000+00:00'
            WHERE scope_kind='operation' AND scope_id='operation-restore';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        command.CommandText =
            """
            UPDATE provider_profile_projection
            SET projection_version=5,updated_at='2026-08-10T00:00:03.0000000Z'
            WHERE profile_id='profile-restore';
            """;
        malformed = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(malformed.Message, "non-canonical UTC authority timestamp");
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6ProfileProjectionRejectsStaleGlobalCredentialEvent()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA foreign_keys=ON;
            INSERT INTO provider_credential_intents VALUES(
              'intent-disable-latest','profile-restore','generation-restore','disable','pending',
              'active-verified','disabled','active-verified','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES(
              'event-disable-latest-v1','root-disable-latest','intent-disable-latest',1,NULL,
              '2026-08-10T00:00:03.0000000+00:00');
            """;
        Assert.AreEqual(2, command.ExecuteNonQuery());
        command.CommandText =
            """
            UPDATE provider_profile_projection
            SET projection_version=4,updated_at='2026-08-10T00:00:04.0000000+00:00'
            WHERE profile_id='profile-restore';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());

    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6CredentialSequenceAdvancesProfileEventProjectionAndCancelledDeleteRoot()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();

        foreach (string createdAt in new[]
                 {
                     "2026-08-10T00:00:02.0000000+00:00",
                     "2026-08-10T00:00:01.9999999+00:00",
                 })
        {
            command.CommandText =
                $"""
                INSERT INTO provider_credential_intents VALUES(
                  'intent-equal-or-rollback-{createdAt[19]}','profile-restore','generation-restore','disable','pending',
                  'active-verified','disabled','active-verified','unavailable','account-restore','billing-restore','cap-restore',
                  'not-required','not-requested','{createdAt}');
                """;
            SqliteException rejected = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
            StringAssert.Contains(rejected.Message, "provider credential lifecycle time regression");
        }

        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-disable-sequence-pending','profile-restore','generation-restore','disable','pending',
              'active-verified','disabled','active-verified','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:03.0000000+00:00');
            """;
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            """
            INSERT INTO provider_credential_intent_events VALUES(
              'event-disable-sequence-wrong-time','root-disable-sequence','intent-disable-sequence-pending',1,NULL,
              '2026-08-10T00:00:03.0000001+00:00');
            """;
        SqliteException wrongTime = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(wrongTime.Message, "event time must equal");
        command.CommandText =
            """
            INSERT INTO provider_credential_intent_events VALUES(
              'event-disable-sequence-v1','root-disable-sequence','intent-disable-sequence-pending',1,NULL,
              '2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-disable-sequence-complete','profile-restore','generation-restore','disable','completed',
              'active-verified','disabled','disabled','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:03.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES(
              'event-disable-sequence-v2','root-disable-sequence','intent-disable-sequence-complete',2,
              'event-disable-sequence-v1','2026-08-10T00:00:03.5000000+00:00');
            """;
        Assert.AreEqual(3, command.ExecuteNonQuery());

        command.CommandText =
            """
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-before-projection','profile-restore','generation-restore','delete','pending',
              'disabled','delete-pending','disabled','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','pending','2026-08-10T00:00:04.0000000+00:00');
            """;
        SqliteException staleProjection = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(staleProjection.Message, "predecessor mismatch");

        command.CommandText =
            """
            UPDATE provider_profile_projection
            SET lifecycle_state='disabled',verification_state='unavailable',intent_id='intent-disable-sequence-complete',
              projection_version=4,updated_at='2026-08-10T00:00:03.5000000+00:00'
            WHERE profile_id='profile-restore';
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-cancel-pending','profile-restore','generation-restore','delete','pending',
              'disabled','delete-pending','disabled','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','pending','2026-08-10T00:00:04.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES(
              'event-delete-cancel-v1','root-delete-cancel','intent-delete-cancel-pending',1,NULL,
              '2026-08-10T00:00:04.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-cancelled','profile-restore','generation-restore','delete','cancelled',
              'disabled','delete-pending','disabled','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','not-requested','2026-08-10T00:00:04.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES(
              'event-delete-cancel-v2','root-delete-cancel','intent-delete-cancelled',2,
              'event-delete-cancel-v1','2026-08-10T00:00:04.5000000+00:00');
            UPDATE provider_profile_projection
            SET intent_id='intent-delete-cancelled',projection_version=5,
              updated_at='2026-08-10T00:00:04.5000000+00:00'
            WHERE profile_id='profile-restore';
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-after-cancel','profile-restore','generation-restore','delete','pending',
              'disabled','delete-pending','disabled','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','pending','2026-08-10T00:00:05.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES(
              'event-delete-after-cancel-v1','root-delete-after-cancel','intent-delete-after-cancel',1,NULL,
              '2026-08-10T00:00:05.0000000+00:00');
            """;
        Assert.AreEqual(8, command.ExecuteNonQuery());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6ProfileProjectionCannotReactivateAfterDeletePending()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA foreign_keys=ON;
            INSERT INTO provider_credential_intents VALUES(
              'intent-delete-terminal','profile-restore','generation-restore','delete','pending',
              'active-verified','delete-pending','active-verified','unavailable','account-restore','billing-restore','cap-restore',
              'not-required','pending','2026-08-10T00:00:03.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES(
              'event-delete-terminal-v1','root-delete-terminal','intent-delete-terminal',1,NULL,
              '2026-08-10T00:00:03.0000000+00:00');
            UPDATE provider_profile_projection
            SET lifecycle_state='delete-pending',verification_state='unavailable',intent_id='intent-delete-terminal',
              cleanup_disposition='pending',projection_version=4,updated_at='2026-08-10T00:00:03.0000000+00:00'
            WHERE profile_id='profile-restore';
            """;
        Assert.AreEqual(3, command.ExecuteNonQuery());
        command.CommandText =
            """
            UPDATE provider_profile_projection
            SET lifecycle_state='active-verified',verification_state='available',intent_id='intent-verify-restore',
              cleanup_disposition='not-requested',projection_version=5,updated_at='2026-08-10T00:00:04.0000000+00:00'
            WHERE profile_id='profile-restore';
            """;
        Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6EveryAuthorityTimestampFamilyRejectsMalformedAndNonCanonicalUtcText()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        (string Table, string Column)[] timestamps =
        [
            ("provider_access_profiles", "created_at"),
            ("provider_effective_scan_configurations_v2", "created_at"),
            ("evidence_acquisition_runs", "created_at"),
            ("evidence_acquisition_commands", "requested_at"),
            ("provider_operation_authorizations", "confirmed_at"),
            ("provider_transport_events", "occurred_at"),
            ("provider_responses", "created_at"),
            ("provider_usage_entries", "created_at"),
            ("provider_rate_limit_facts", "observed_at"),
            ("provider_rate_limit_facts", "resets_at"),
            ("provider_settlements", "created_at"),
            ("provider_semantic_proposals", "created_at"),
            ("provider_replay_edges", "created_at"),
            ("provider_profile_projection", "updated_at"),
        ];
        foreach ((string table, string column) in timestamps)
        {
            foreach (string invalid in new[]
                     {
                         "2026-99-99T99:99:99.0000000+00:00",
                         "0000-01-01T00:00:00.0000000+00:00",
                         "2026-02-29T00:00:00.0000000+00:00",
                         "2026-04-31T00:00:00.0000000+00:00",
                         "2026-08-10T24:00:00.0000000+00:00",
                         "2026-08-10T23:60:00.0000000+00:00",
                         "2026-08-10T23:59:60.0000000+00:00",
                         "2026-08-10T00:00:00.0000000Z",
                     })
            {
                command.CommandText = $"INSERT INTO {table}({column}) VALUES($invalid);";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("$invalid", invalid);
                SqliteException rejected = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery(), $"{table}.{column}");
                StringAssert.Contains(rejected.Message, "non-canonical UTC authority timestamp", $"{table}.{column}");
            }
        }

        command.CommandText =
            "INSERT INTO provider_access_profiles VALUES('profile-min-year','openai','responses','Minimum year',NULL,NULL,'0001-01-01T00:00:00.0000000+00:00');";
        Assert.AreEqual(1, command.ExecuteNonQuery());
        command.CommandText =
            "INSERT INTO provider_access_profiles VALUES('profile-leap-year','openai','responses','Leap year',NULL,NULL,'2000-02-29T23:59:59.9999999+00:00');";
        Assert.AreEqual(1, command.ExecuteNonQuery());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ExactPatchedSqliteBindingAndStrictForeignKeySchemaAreRequired()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();

        Assert.AreEqual(SqliteRuntimeIdentity.RequiredVersion, store.BindingIdentity.Version);
        Assert.AreEqual(SqliteRuntimeIdentity.RequiredSourceId, store.BindingIdentity.SourceId);
        Assert.AreEqual(
            SqliteRuntimeIdentity.RequiredWinX64NativeSha256,
            store.BindingIdentity.NativeSha256);
        CollectionAssert.Contains(store.BindingIdentity.CompileOptions.ToArray(), "THREADSAFE=1");
        Assert.AreEqual(AuthoritativeStore.CurrentSchemaVersion, store.GetSchemaVersion());

        using SqliteConnection connection = new($"Data Source={store.Paths.Database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
              (SELECT COUNT(*) FROM pragma_table_list
               WHERE schema='main' AND type='table' AND name NOT LIKE 'sqlite_%' AND strict=0),
              (SELECT foreign_keys FROM pragma_foreign_keys);
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        Assert.IsTrue(reader.Read());
        Assert.AreEqual(0L, reader.GetInt64(0));
        Assert.AreEqual(1L, reader.GetInt64(1));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6AuthorizationDeadlinesUseExactHundredNanosecondArithmeticAcrossBoundaries()
    {
        (string Confirmed, string Deadline, bool Accepted)[] cases =
        [
            ("2026-08-10T00:00:00.0000000+00:00", "2026-08-10T00:00:00.0010000+00:00", true),
            ("2026-08-10T00:00:00.0000000+00:00", "2026-08-10T00:00:00.0010001+00:00", false),
            ("2026-08-10T00:00:00.9999000+00:00", "2026-08-10T00:00:01.0009000+00:00", false),
            ("2026-08-10T23:59:59.9999000+00:00", "2026-08-11T00:00:00.0009000+00:00", false),
        ];
        foreach ((string confirmed, string deadline, bool accepted) in cases)
        {
            using TemporaryStore temporary = new();
            using AuthoritativeStore store = temporary.Open();
            SeedProviderAuthorityBlock(temporary.Root);
            using SqliteConnection connection = OpenRaw(temporary.Root);
            using SqliteCommand command = connection.CreateCommand();
            command.Parameters.AddWithValue("$confirmed", confirmed);
            command.Parameters.AddWithValue("$deadline", deadline);
            command.CommandText =
                """
                DROP TRIGGER provider_authority_release_required;
                INSERT INTO provider_operation_authorizations(
                  authorization_id,operation_id,owner_kind,owner_id,evidence_acquisition_run_id,job_node_id,command_id,requested_at,
                  profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
                  effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
                  output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
                  price_snapshot_id,settings_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,
                  coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,
                  maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,
                  dispatch_deadline_utc,confirmed_at)
                SELECT 'authorization-deadline',operation_id,owner_kind,owner_id,owner_id,job_node_id,command_id,requested_at,
                  profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
                  effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
                  output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
                  price_snapshot_id,settings_fingerprint,'openai-responses-o200k-byte-envelope','v1','proved',coordinator_fencing_epoch,
                  maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,
                  maximum_dispatch_count,maximum_calculated_nano_usd,1,$deadline,$confirmed
                FROM provider_operation_blocks WHERE operation_id='operation-restore';
                """;
            if (accepted)
            {
                Assert.AreEqual(1, command.ExecuteNonQuery(), $"{confirmed}->{deadline}");
            }
            else
            {
                Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery(), $"{confirmed}->{deadline}");
            }
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void LifecyclePolicyCoversPauseResumeCancelAndTerminalClosure()
    {
        LifecyclePolicy.EnsureAllowed(LifecycleState.Queued, LifecycleState.Running);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Running, LifecycleState.Pausing);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Pausing, LifecycleState.Paused);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Paused, LifecycleState.Queued);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Running, LifecycleState.Cancelling);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Cancelling, LifecycleState.Cancelled);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Waiting, LifecycleState.Completed);

        foreach (LifecycleState terminal in Enum.GetValues<LifecycleState>().Where(LifecyclePolicy.IsTerminal))
        {
            Assert.ThrowsExactly<InvalidOperationException>(
                () => LifecyclePolicy.EnsureAllowed(terminal, LifecycleState.Running));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void DurableCommandsAreIdempotentAndRunBindingsRemainImmutable()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset acceptedDeadline = now.AddMinutes(1);
        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "coordinator-a",
            now,
            TimeSpan.FromMinutes(5));
        RunBinding binding = Binding("a");
        RunRecord first = store.CreateRun(
            "command-a",
            "run-a",
            binding,
            authority.FencingEpoch,
            now,
            "CliUserAction",
            acceptedDeadline);
        RunRecord duplicate = store.CreateRun(
            "command-a",
            "run-other",
            binding,
            authority.FencingEpoch,
            now.AddMinutes(2),
            "CliUserAction",
            now.AddMinutes(7));

        Assert.AreEqual(first.RunId, duplicate.RunId);
        Assert.AreEqual(binding, duplicate.Binding);
        DurableCommandRecord acceptedStart = store.GetDurableCommand("command-a");
        Assert.AreEqual("start", acceptedStart.CommandKind);
        Assert.AreEqual(0L, acceptedStart.ExpectedGeneration);
        Assert.AreEqual(binding, acceptedStart.RunBinding);
        Assert.AreEqual("CliUserAction", acceptedStart.StartInitiationKind);
        Assert.AreEqual(acceptedDeadline, acceptedStart.StartDispatchDeadline);
        Assert.ThrowsExactly<InvalidOperationException>(() => store.CreateRun(
            "command-a",
            "run-other",
            Binding("other"),
            authority.FencingEpoch,
            now.AddMinutes(2),
            "CliUserAction",
            now.AddMinutes(7)));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.CreateRun(
            "command-a",
            "run-other",
            binding,
            authority.FencingEpoch,
            now.AddMinutes(2),
            "DesktopUserGesture",
            now.AddMinutes(7)));
        RunRecord running = store.Transition(
            "transition-a",
            first.RunId,
            first.Generation,
            LifecycleState.Running,
            authority.FencingEpoch,
            "test dispatch",
            DateTimeOffset.UtcNow);
        Assert.AreEqual(1L, running.Generation);
        Assert.AreEqual(binding, running.Binding);
        Assert.AreEqual(
            running,
            store.Transition(
                "transition-a",
                first.RunId,
                first.Generation,
                LifecycleState.Running,
                authority.FencingEpoch,
                "idempotent replay",
                DateTimeOffset.UtcNow));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.Transition(
            "transition-a",
            first.RunId,
            running.Generation,
            LifecycleState.Pausing,
            authority.FencingEpoch,
            "rebound command",
            DateTimeOffset.UtcNow));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.Transition(
            "transition-b",
            first.RunId,
            0,
            LifecycleState.Completed,
            authority.FencingEpoch,
            "stale compare and swap",
            DateTimeOffset.UtcNow));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void StaleAttemptCannotPublishAndDigestMismatchLeavesNoAuthority()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "coordinator-a",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));
        RunRecord queued = store.CreateRun(
            "command-a",
            "run-a",
            Binding("a"),
            authority.FencingEpoch,
            DateTimeOffset.UtcNow);
        _ = store.Transition(
            "transition-a",
            queued.RunId,
            queued.Generation,
            LifecycleState.Running,
            authority.FencingEpoch,
            "test dispatch",
            DateTimeOffset.UtcNow);
        AttemptRecord attempt = store.CreateAttempt(
            queued.RunId,
            authority.FencingEpoch,
            TimeSpan.FromMinutes(2),
            DateTimeOffset.UtcNow);
        string directory = Path.Combine(store.Paths.Staging, attempt.AttemptId);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "result.json"), "{}");

        Assert.ThrowsExactly<InvalidOperationException>(() => store.AdmitStagedPayload(
            attempt,
            "result.json",
            new string('0', 64),
            2,
            new string('1', 64),
            1024,
            DateTimeOffset.UtcNow));
        Assert.IsEmpty(Directory.EnumerateFiles(store.Paths.Payloads, "*", SearchOption.AllDirectories));

        AttemptRecord stale = attempt with { AttemptFencingToken = attempt.AttemptFencingToken + 1 };
        string actual = Convert.ToHexString(SHA256.HashData("{}"u8.ToArray())).ToLowerInvariant();
        Assert.ThrowsExactly<InvalidOperationException>(() => store.AdmitStagedPayload(
            stale,
            "result.json",
            actual,
            2,
            new string('1', 64),
            1024,
            DateTimeOffset.UtcNow));
        Assert.IsEmpty(Directory.EnumerateFiles(store.Paths.Payloads, "*", SearchOption.AllDirectories));

        RunRecord running = store.GetRun(queued.RunId);
        _ = store.Transition(
            "cancel-a",
            queued.RunId,
            running.Generation,
            LifecycleState.Cancelling,
            authority.FencingEpoch,
            "cancel requested",
            DateTimeOffset.UtcNow);
        store.SettleLiveAttempts(
            queued.RunId,
            "cancelled-at-safe-boundary",
            authority.FencingEpoch);
        Assert.ThrowsExactly<InvalidOperationException>(() => store.AdmitStagedPayload(
            attempt,
            "result.json",
            actual,
            2,
            new string('1', 64),
            1024,
            DateTimeOffset.UtcNow));
        Assert.IsEmpty(Directory.EnumerateFiles(store.Paths.Payloads, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void LedgerPersistsPauseResumeCancelAndNeverReopensTerminalState()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "coordinator-a",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));
        RunRecord current = store.CreateRun(
            "start-a",
            "run-a",
            Binding("a"),
            authority.FencingEpoch,
            DateTimeOffset.UtcNow);
        current = Transition(store, authority, current, LifecycleState.Running);
        current = Transition(store, authority, current, LifecycleState.Pausing);
        current = Transition(store, authority, current, LifecycleState.Paused);
        current = Transition(store, authority, current, LifecycleState.Queued);
        current = Transition(store, authority, current, LifecycleState.Cancelling);
        current = Transition(store, authority, current, LifecycleState.Cancelled);

        Assert.AreEqual(LifecycleState.Cancelled, store.GetRun(current.RunId).State);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => Transition(store, authority, current, LifecycleState.Running));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void CoordinatorLeaseRenewalRejectsUnacquiredStaleAndExpiredEpochs()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using (TemporaryStore temporary = new())
        using (AuthoritativeStore store = temporary.Open())
        {
            CoordinatorAuthority first = store.AcquireCoordinatorAuthority(
                "coordinator-a",
                now,
                TimeSpan.FromMinutes(5));
            CoordinatorAuthority renewed = store.RenewCoordinatorAuthority(
                first.FencingEpoch,
                now.AddMinutes(1),
                TimeSpan.FromMinutes(5));
            Assert.AreEqual(first.InstanceId, renewed.InstanceId);
            Assert.AreEqual(first.FencingEpoch, renewed.FencingEpoch);
            Assert.IsGreaterThan(first.ExpiresAt, renewed.ExpiresAt);

            Assert.ThrowsExactly<InvalidOperationException>(
                () => store.RenewCoordinatorAuthority(
                    first.FencingEpoch + 100,
                    now.AddMinutes(1),
                    TimeSpan.FromMinutes(5)));

            Assert.ThrowsExactly<InvalidOperationException>(
                () => store.AcquireCoordinatorAuthority(
                    "coordinator-b",
                    now.AddMinutes(1),
                    TimeSpan.FromMinutes(5)));
            CoordinatorAuthority second =
                store.AcquireCoordinatorAuthorityAfterProcessExclusion(
                "coordinator-b",
                now.AddMinutes(1),
                TimeSpan.FromMinutes(5));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => store.RenewCoordinatorAuthority(
                    first.FencingEpoch,
                    now.AddMinutes(2),
                    TimeSpan.FromMinutes(5)));
            Assert.AreEqual(
                second.FencingEpoch,
                store.RenewCoordinatorAuthority(
                    second.FencingEpoch,
                    now.AddMinutes(2),
                    TimeSpan.FromMinutes(5)).FencingEpoch);
        }

        using (TemporaryStore expiredTemporary = new())
        using (AuthoritativeStore expiredStore = expiredTemporary.Open())
        {
            CoordinatorAuthority expired = expiredStore.AcquireCoordinatorAuthority(
                "coordinator-expired",
                now.AddMinutes(-10),
                TimeSpan.FromMinutes(1));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => expiredStore.RenewCoordinatorAuthority(
                    expired.FencingEpoch,
                    now,
                    TimeSpan.FromMinutes(5)));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void NewCoordinatorEpochGloballyFencesEveryOlderMutationAndAttempt()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        CoordinatorAuthority first = store.AcquireCoordinatorAuthority(
            "coordinator-a",
            now,
            TimeSpan.FromMinutes(5));
        RunRecord queued = store.CreateRun(
            "command-a",
            "run-a",
            Binding("a"),
            first.FencingEpoch,
            now);
        RunRecord running = store.Transition(
            "running-a",
            queued.RunId,
            queued.Generation,
            LifecycleState.Running,
            first.FencingEpoch,
            "first dispatch",
            now);
        AttemptRecord firstAttempt = store.CreateAttempt(
            queued.RunId,
            first.FencingEpoch,
            TimeSpan.FromMinutes(2),
            now);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => store.AcquireCoordinatorAuthority(
                "coordinator-b",
                now.AddSeconds(1),
                TimeSpan.FromMinutes(5)));
        CoordinatorAuthority second =
            store.AcquireCoordinatorAuthorityAfterProcessExclusion(
            "coordinator-b",
            now.AddSeconds(1),
            TimeSpan.FromMinutes(5));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.CreateRun(
            "stale-create",
            "run-stale",
            Binding("stale"),
            first.FencingEpoch,
            now));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.CreateRun(
            "future-create",
            "run-future",
            Binding("future"),
            second.FencingEpoch + 100,
            now));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.Transition(
            "stale-transition",
            running.RunId,
            running.Generation,
            LifecycleState.Waiting,
            first.FencingEpoch,
            "stale authority",
            now));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.SettleLiveAttempts(
            running.RunId,
            "stale settlement",
            first.FencingEpoch));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.AddCheckpoint(
            "stale-checkpoint",
            firstAttempt,
            running.Binding,
            "closure",
            new string('a', 64),
            "[]",
            "{}",
            now));

        store.SettleLiveAttempts(
            running.RunId,
            "recovered by current authority",
            second.FencingEpoch);
        AttemptRecord secondAttempt = store.CreateAttempt(
            running.RunId,
            second.FencingEpoch,
            TimeSpan.FromMinutes(2),
            now.AddSeconds(1));
        Assert.AreEqual(second.FencingEpoch, secondAttempt.CoordinatorFencingEpoch);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void CheckpointAdmissionRequiresCurrentUniqueAttemptBindingAndBoundedJson()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "coordinator-a",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));
        RunBinding binding = Binding("a");
        RunRecord queued = store.CreateRun(
            "command-a",
            "run-a",
            binding,
            authority.FencingEpoch,
            DateTimeOffset.UtcNow);
        _ = store.Transition(
            "running-a",
            queued.RunId,
            queued.Generation,
            LifecycleState.Running,
            authority.FencingEpoch,
            "test dispatch",
            DateTimeOffset.UtcNow);
        AttemptRecord first = store.CreateAttempt(
            queued.RunId,
            authority.FencingEpoch,
            TimeSpan.FromMinutes(2),
            DateTimeOffset.UtcNow);

        Assert.ThrowsExactly<InvalidOperationException>(() => store.CreateAttempt(
            queued.RunId,
            authority.FencingEpoch,
            TimeSpan.FromMinutes(2),
            DateTimeOffset.UtcNow));
        Assert.ThrowsExactly<InvalidOperationException>(() => store.AddCheckpoint(
            "checkpoint-binding",
            first,
            Binding("other"),
            "closure-a",
            new string('a', 64),
            "[]",
            "{}",
            DateTimeOffset.UtcNow));
        Assert.ThrowsExactly<ArgumentException>(() => store.AddCheckpoint(
            "checkpoint-json",
            first,
            binding,
            "closure-a",
            new string('a', 64),
            "[",
            "{}",
            DateTimeOffset.UtcNow));
        Assert.ThrowsExactly<ArgumentException>(() => store.AddCheckpoint(
            "checkpoint-oversized",
            first,
            binding,
            "closure-a",
            new string('a', 64),
            JsonSerializer.Serialize(new string('x', 70_000)),
            "{}",
            DateTimeOffset.UtcNow));

        store.SettleLiveAttempts(
            queued.RunId,
            "superseded",
            authority.FencingEpoch);
        AttemptRecord current = store.CreateAttempt(
            queued.RunId,
            authority.FencingEpoch,
            TimeSpan.FromMinutes(2),
            DateTimeOffset.UtcNow);
        Assert.ThrowsExactly<InvalidOperationException>(() => store.AddCheckpoint(
            "checkpoint-stale",
            current with
            {
                AttemptFencingToken = current.AttemptFencingToken - 1,
            },
            binding,
            "closure-a",
            new string('a', 64),
            "[]",
            "{}",
            DateTimeOffset.UtcNow));

        store.AddCheckpoint(
            "checkpoint-current",
            current,
            binding,
            "closure-a",
            new string('a', 64),
            "[]",
            """{"pending":[],"gaps":[]}""",
            DateTimeOffset.UtcNow);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void ExistingStoreRefusesMetadataAndSchemaIdentityDrift()
    {
        using (TemporaryStore metadataTemporary = new())
        {
            using (AuthoritativeStore store = metadataTemporary.Open())
            {
                Assert.AreEqual(AuthoritativeStore.CurrentSchemaVersion, store.GetSchemaVersion());
            }

            ExecuteRaw(
                metadataTemporary.Root,
                "UPDATE store_metadata SET value = 'substituted' WHERE key = 'sqlite_source_id';");
            Assert.ThrowsExactly<InvalidOperationException>(() => metadataTemporary.Open());
        }

        using (TemporaryStore schemaTemporary = new())
        {
            using (AuthoritativeStore store = schemaTemporary.Open())
            {
                Assert.AreEqual(AuthoritativeStore.CurrentSchemaVersion, store.GetSchemaVersion());
            }

            ExecuteRaw(schemaTemporary.Root, "DROP INDEX idx_attempts_run;");
            Assert.ThrowsExactly<InvalidOperationException>(() => schemaTemporary.Open());
        }

        using (TemporaryStore definitionTemporary = new())
        {
            using (AuthoritativeStore store = definitionTemporary.Open())
            {
                Assert.AreEqual(AuthoritativeStore.CurrentSchemaVersion, store.GetSchemaVersion());
            }

            ExecuteRaw(
                definitionTemporary.Root,
                "ALTER TABLE runs ADD COLUMN unrecognized_schema_drift TEXT;");
            Assert.ThrowsExactly<InvalidOperationException>(() => definitionTemporary.Open());
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void RestoreRejectsIncompleteOrMismatchedManifestWithoutCreatingTarget()
    {
        using TemporaryStore source = new();
        BackupArtifact backup = CreatePayloadBackup(source);
        string originalManifest = File.ReadAllText(backup.ManifestPath);
        string target = Path.Combine(
            Path.GetTempPath(),
            $"infinium-restore-target-{Guid.NewGuid():N}");
        try
        {
            JsonObject missingPayload = ParseManifest(originalManifest);
            missingPayload["payloads"] = new JsonArray();
            File.WriteAllText(backup.ManifestPath, missingPayload.ToJsonString());
            using (StoragePaths targetPaths = new(target))
            {
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => AuthoritativeStore.RestoreBackup(backup, targetPaths));
            }

            Assert.IsFalse(Directory.Exists(target));

            JsonObject wrongLength = ParseManifest(originalManifest);
            wrongLength["payloads"]![0]!["byteLength"] = 1;
            File.WriteAllText(backup.ManifestPath, wrongLength.ToJsonString());
            using (StoragePaths targetPaths = new(target))
            {
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => AuthoritativeStore.RestoreBackup(backup, targetPaths));
            }

            Assert.IsFalse(Directory.Exists(target));

            JsonObject wrongSchema = ParseManifest(originalManifest);
            wrongSchema["schemaVersion"] = AuthoritativeStore.CurrentSchemaVersion + 1;
            File.WriteAllText(backup.ManifestPath, wrongSchema.ToJsonString());
            using (StoragePaths targetPaths = new(target))
            {
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => AuthoritativeStore.RestoreBackup(backup, targetPaths));
            }

            Assert.IsFalse(Directory.Exists(target));
        }
        finally
        {
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Security")]
    public void FixedWriteClassesBackupAndRestoreHaveDurableAuditRecords()
    {
        using TemporaryStore source = new();
        BackupArtifact backup = CreatePayloadBackup(source);
        string target = Path.Combine(
            Path.GetTempPath(),
            $"infinium-audited-restore-{Guid.NewGuid():N}");
        try
        {
            using (SqliteConnection sourceAudit = OpenRaw(source.Root))
            using (SqliteCommand command = sourceAudit.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT
                        COUNT(DISTINCT CASE
                            WHEN event_kind = 'write-class-authority-bound'
                            THEN object_id END),
                        COUNT(CASE WHEN event_kind = 'backup-created' THEN 1 END)
                    FROM audit_events;
                    """;
                using SqliteDataReader reader = command.ExecuteReader();
                Assert.IsTrue(reader.Read());
                Assert.AreEqual(6L, reader.GetInt64(0));
                Assert.AreEqual(1L, reader.GetInt64(1));
            }

            using (StoragePaths targetPaths = new(target))
            {
                AuthoritativeStore.RestoreBackup(backup, targetPaths);
            }

            using SqliteConnection restoredAudit = OpenRaw(target);
            using SqliteCommand restored = restoredAudit.CreateCommand();
            restored.CommandText =
                "SELECT COUNT(*) FROM audit_events WHERE event_kind = 'restore-completed';";
            Assert.AreEqual(
                1L,
                Convert.ToInt64(
                    restored.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            if (Directory.Exists(target))
            {
                Directory.Delete(target, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Fault")]
    public void BackupIdentityCollisionCannotOverwriteAnExistingBackup()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        BackupArtifact first = store.CreateBackup("collision", now);
        string originalSha = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(first.DatabasePath)))
            .ToLowerInvariant();

        Assert.ThrowsExactly<IOException>(
            () => store.CreateBackup("collision", now));
        Assert.AreEqual(
            originalSha,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(first.DatabasePath)))
                .ToLowerInvariant());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Security")]
    [TestProperty("Category", "Fault")]
    public void BackupRejectsCorruptCasBytesAndRemovesThePartialBundle()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "coordinator-corrupt-backup",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));
        RunRecord queued = store.CreateRun(
            "command-corrupt-backup",
            "run-corrupt-backup",
            Binding("corrupt-backup"),
            authority.FencingEpoch,
            DateTimeOffset.UtcNow);
        _ = store.Transition(
            "running-corrupt-backup",
            queued.RunId,
            queued.Generation,
            LifecycleState.Running,
            authority.FencingEpoch,
            "backup dispatch",
            DateTimeOffset.UtcNow);
        AttemptRecord attempt = store.CreateAttempt(
            queued.RunId,
            authority.FencingEpoch,
            TimeSpan.FromMinutes(2),
            DateTimeOffset.UtcNow);
        string staging = Path.Combine(store.Paths.Staging, attempt.AttemptId);
        Directory.CreateDirectory(staging);
        byte[] original = "synthetic backup payload"u8.ToArray();
        File.WriteAllBytes(Path.Combine(staging, "result.bin"), original);
        string sha256 = Convert.ToHexString(SHA256.HashData(original))
            .ToLowerInvariant();
        PayloadAdmission admission = store.AdmitStagedPayload(
            attempt,
            "result.bin",
            sha256,
            original.LongLength,
            sha256,
            4096,
            DateTimeOffset.UtcNow);
        File.WriteAllBytes(
            Path.Combine(
                store.Paths.ProductRoot,
                admission.RelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)),
            "corrupted backup payload"u8.ToArray());

        Assert.ThrowsExactly<InvalidOperationException>(
            () => store.CreateBackup("corrupt", DateTimeOffset.UtcNow));
        Assert.IsEmpty(
            Directory.GetFileSystemEntries(
                store.Paths.Backups,
                "*",
                SearchOption.AllDirectories));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void ImmutableAuthorityTablesInstallUpdateAndDeleteGuards()
    {
        using TemporaryStore temporary = new();
        using (AuthoritativeStore store = temporary.Open())
        {
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                "coordinator-a",
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5));
            _ = store.CreateRun(
                "command-a",
                "run-a",
                Binding("a"),
                authority.FencingEpoch,
                DateTimeOffset.UtcNow);
        }

        string[] protectedTables =
        [
            "audit_events",
            "case_occurrences",
            "checkpoints",
            "durable_commands",
            "finding_occurrences",
            "publication_receipts",
        ];
        using SqliteConnection connection = OpenRaw(temporary.Root);
        foreach (string table in protectedTables)
        {
            using SqliteCommand triggers = connection.CreateCommand();
            triggers.CommandText =
                """
                SELECT COUNT(*)
                FROM sqlite_schema
                WHERE type = 'trigger'
                  AND tbl_name = $table
                  AND name IN ($update, $delete);
                """;
            triggers.Parameters.AddWithValue("$table", table);
            triggers.Parameters.AddWithValue("$update", table + "_append_only_update");
            triggers.Parameters.AddWithValue("$delete", table + "_append_only_delete");
            Assert.AreEqual(
                2L,
                Convert.ToInt64(
                    triggers.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture));
        }

        using SqliteCommand mutate = connection.CreateCommand();
        mutate.CommandText =
            "UPDATE durable_commands SET disposition = 'rejected' WHERE command_id = 'command-a';";
        Assert.ThrowsExactly<SqliteException>(() => mutate.ExecuteNonQuery());
        mutate.CommandText = "DELETE FROM durable_commands WHERE command_id = 'command-a';";
        Assert.ThrowsExactly<SqliteException>(() => mutate.ExecuteNonQuery());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void PayloadAdmissionRequiresExactLengthAndRetainsCanonicalManifestDigest()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "coordinator-a",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));
        RunRecord queued = store.CreateRun(
            "command-a",
            "run-a",
            Binding("a"),
            authority.FencingEpoch,
            DateTimeOffset.UtcNow);
        _ = store.Transition(
            "transition-a",
            queued.RunId,
            queued.Generation,
            LifecycleState.Running,
            authority.FencingEpoch,
            "test dispatch",
            DateTimeOffset.UtcNow);
        AttemptRecord attempt = store.CreateAttempt(
            queued.RunId,
            authority.FencingEpoch,
            TimeSpan.FromMinutes(2),
            DateTimeOffset.UtcNow);
        string directory = Path.Combine(store.Paths.Staging, attempt.AttemptId);
        Directory.CreateDirectory(directory);
        byte[] bytes = "bounded generic output"u8.ToArray();
        File.WriteAllBytes(Path.Combine(directory, "result.bin"), bytes);
        string sha256 =
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string manifestSha256 = new('a', 64);

        using (FileStream activeWriter = new(
                   Path.Combine(directory, "result.bin"),
                   FileMode.Open,
                   FileAccess.Write,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.Throws<Win32Exception>(() => store.AdmitStagedPayload(
                attempt,
                "result.bin",
                sha256,
                bytes.LongLength,
                manifestSha256,
                4096,
                DateTimeOffset.UtcNow));
        }

        Assert.ThrowsExactly<InvalidOperationException>(() => store.AdmitStagedPayload(
            attempt,
            "result.bin",
            sha256,
            bytes.LongLength + 1,
            manifestSha256,
            4096,
            DateTimeOffset.UtcNow));
        Assert.IsFalse(store.HasRecoverablePublication(queued.RunId));
        Assert.IsTrue(File.Exists(Path.Combine(directory, "result.bin")));

        Assert.Throws<SqliteException>(() => store.AdmitStagedPayload(
            attempt,
            "result.bin",
            sha256,
            bytes.LongLength,
            manifestSha256,
            4096,
            DateTimeOffset.UtcNow,
            completionCommandId: "command-a"));
        Assert.IsTrue(File.Exists(Path.Combine(directory, "result.bin")));
        string orphanedCasPath = Path.Combine(
            store.Paths.Payloads,
            sha256[..2],
            sha256[2..4],
            sha256);
        Assert.IsTrue(File.Exists(orphanedCasPath));
        CollectionAssert.AreEqual(bytes, File.ReadAllBytes(orphanedCasPath));

        PayloadAdmission admitted = store.AdmitStagedPayload(
            attempt,
            "result.bin",
            sha256,
            bytes.LongLength,
            manifestSha256,
            4096,
            DateTimeOffset.UtcNow,
            stagedArtifactId: "artifact-a");

        Assert.AreEqual(bytes.LongLength, admitted.ByteLength);
        Assert.AreEqual(manifestSha256, admitted.StagedManifestSha256);
        Assert.IsTrue(store.HasRecoverablePublication(queued.RunId));
        using SqliteConnection auditConnection = OpenRaw(temporary.Root);
        using SqliteCommand audit = auditConnection.CreateCommand();
        audit.CommandText =
            """
            SELECT COUNT(*)
            FROM audit_events
            WHERE (event_kind = 'attempt-staging-accepted'
                   AND object_kind = 'attempt'
                   AND object_id = $attempt)
               OR (event_kind = 'payload-published'
                   AND object_kind = 'payload'
                   AND object_id = $payload
                   AND detail_payload_id = $payload)
               OR (event_kind = 'staged-artifact-accepted'
                   AND object_kind = 'artifact'
                   AND object_id = 'artifact-a');
            """;
        audit.Parameters.AddWithValue("$attempt", attempt.AttemptId);
        audit.Parameters.AddWithValue("$payload", admitted.PayloadId);
        Assert.AreEqual(
            3L,
            Convert.ToInt64(
                audit.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Unit")]
    [TestProperty("Category", "Fault")]
    public void DispatchAndPublicationRaceAtomicallyWithLifecycleCommands()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "coordinator-atomic",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));

        for (int index = 0; index < 12; index++)
        {
            string suffix = $"atomic-{index}";
            RunRecord queued = store.CreateRun(
                $"start-{suffix}",
                $"run-{suffix}",
                Binding(suffix),
                authority.FencingEpoch,
                DateTimeOffset.UtcNow);
            using ManualResetEventSlim start = new(initialState: false);
            DispatchAdmission? dispatch = null;
            Exception? dispatchFailure = null;
            RunRecord? pausing = null;
            Exception? pauseFailure = null;
            Task dispatchTask = Task.Run(() =>
            {
                start.Wait();
                try
                {
                    dispatch = store.DispatchAttempt(
                        $"dispatch-{suffix}",
                        queued.RunId,
                        queued.Generation,
                        authority.FencingEpoch,
                        TimeSpan.FromMinutes(2),
                        DateTimeOffset.UtcNow);
                }
                catch (Exception exception)
                {
                    dispatchFailure = exception;
                }
            });
            Task pauseTask = Task.Run(() =>
            {
                start.Wait();
                try
                {
                    pausing = store.Transition(
                        $"pause-{suffix}",
                        queued.RunId,
                        queued.Generation,
                        LifecycleState.Pausing,
                        authority.FencingEpoch,
                        "concurrent pause",
                        DateTimeOffset.UtcNow);
                }
                catch (Exception exception)
                {
                    pauseFailure = exception;
                }
            });
            start.Set();
            Task.WaitAll(dispatchTask, pauseTask);

            Assert.AreEqual(dispatch is null, pausing is not null);
            if (dispatch is not null)
            {
                Assert.IsNotNull(pauseFailure);
                Assert.AreEqual(LifecycleState.Running, store.GetRun(queued.RunId).State);
                Assert.IsTrue(store.HasLiveAttempts(queued.RunId));
            }
            else
            {
                Assert.IsNotNull(dispatchFailure);
                Assert.AreEqual(LifecycleState.Pausing, store.GetRun(queued.RunId).State);
                Assert.IsFalse(store.HasLiveAttempts(queued.RunId));
                continue;
            }

            AttemptRecord attempt = dispatch.Attempt;
            string directory = Path.Combine(store.Paths.Staging, attempt.AttemptId);
            Directory.CreateDirectory(directory);
            byte[] bytes = Encoding.UTF8.GetBytes($"atomic publication {index}");
            File.WriteAllBytes(Path.Combine(directory, "result.bin"), bytes);
            string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            RunRecord running = dispatch.Run;
            using ManualResetEventSlim publishStart = new(initialState: false);
            PayloadAdmission? admission = null;
            Exception? admissionFailure = null;
            RunRecord? publicationPause = null;
            Task publicationTask = Task.Run(() =>
            {
                publishStart.Wait();
                try
                {
                    admission = store.AdmitStagedPayload(
                        attempt,
                        "result.bin",
                        sha,
                        bytes.LongLength,
                        sha,
                        4096,
                        DateTimeOffset.UtcNow,
                        $"complete-{suffix}");
                }
                catch (Exception exception)
                {
                    admissionFailure = exception;
                }
            });
            Task publicationPauseTask = Task.Run(() =>
            {
                publishStart.Wait();
                try
                {
                    publicationPause = store.Transition(
                        $"publication-pause-{suffix}",
                        running.RunId,
                        running.Generation,
                        LifecycleState.Pausing,
                        authority.FencingEpoch,
                        "concurrent publication pause",
                        DateTimeOffset.UtcNow);
                }
                catch (InvalidOperationException)
                {
                }
            });
            publishStart.Set();
            Task.WaitAll(publicationTask, publicationPauseTask);

            Assert.AreEqual(admission is null, publicationPause is not null);
            if (admission is not null)
            {
                Assert.AreEqual(LifecycleState.Completed, store.GetRun(queued.RunId).State);
                Assert.IsTrue(store.HasRecoverablePublication(queued.RunId));
                Assert.IsFalse(store.HasLiveAttempts(queued.RunId));
            }
            else
            {
                Assert.IsNotNull(admissionFailure);
                Assert.AreEqual(LifecycleState.Pausing, store.GetRun(queued.RunId).State);
                Assert.IsTrue(store.HasLiveAttempts(queued.RunId));
            }
        }
    }

    private static RunBinding Binding(string suffix) =>
        new($"snapshot-{suffix}", $"context-{suffix}", $"config-{suffix}", $"manifest-{suffix}");

    private static BackupArtifact CreatePayloadBackup(TemporaryStore temporary)
    {
        using AuthoritativeStore store = temporary.Open();
        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "coordinator-backup",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));
        RunRecord queued = store.CreateRun(
            "command-backup",
            "run-backup",
            Binding("backup"),
            authority.FencingEpoch,
            DateTimeOffset.UtcNow);
        _ = store.Transition(
            "running-backup",
            queued.RunId,
            queued.Generation,
            LifecycleState.Running,
            authority.FencingEpoch,
            "backup dispatch",
            DateTimeOffset.UtcNow);
        AttemptRecord attempt = store.CreateAttempt(
            queued.RunId,
            authority.FencingEpoch,
            TimeSpan.FromMinutes(2),
            DateTimeOffset.UtcNow);
        string staging = Path.Combine(store.Paths.Staging, attempt.AttemptId);
        Directory.CreateDirectory(staging);
        byte[] bytes = "synthetic backup payload"u8.ToArray();
        File.WriteAllBytes(Path.Combine(staging, "result.bin"), bytes);
        string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        _ = store.AdmitStagedPayload(
            attempt,
            "result.bin",
            sha,
            bytes.LongLength,
            sha,
            4096,
            DateTimeOffset.UtcNow);
        return store.CreateBackup("unit", DateTimeOffset.UtcNow);
    }

    private static JsonObject ParseManifest(string value) =>
        JsonNode.Parse(value)?.AsObject()
        ?? throw new InvalidOperationException("The test backup manifest is missing.");

    public static void SeedProviderAuthorityBlock(string productRoot, string lifecycleState = "created")
    {
        byte[] canonicalRequest = new byte[1024];
        string canonicalRequestSha256 = Convert.ToHexStringLower(SHA256.HashData(canonicalRequest));
        string payloadDirectory = Path.Combine(
            productRoot,
            "payloads",
            canonicalRequestSha256[..2],
            canonicalRequestSha256[2..4]);
        Directory.CreateDirectory(payloadDirectory);
        File.WriteAllBytes(Path.Combine(payloadDirectory, canonicalRequestSha256), canonicalRequest);
        using SqliteConnection connection = OpenRaw(productRoot);
        using SqliteCommand command = connection.CreateCommand();
        command.Parameters.AddWithValue("$canonicalRequestSha256", canonicalRequestSha256);
        command.Parameters.AddWithValue("$providerLifecycleState", lifecycleState);
        command.CommandText =
            """
            PRAGMA foreign_keys=ON;
            INSERT INTO provider_access_profiles VALUES('profile-restore','openai','responses','Restore','account-restore','billing-restore','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_generations VALUES('generation-restore','profile-restore',1,0,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_capability_snapshots VALUES('cap-restore','openai','gpt-5.6-sol','default','medium','current_turn','standard',0,0,0,'none',0,'disabled','explicit',0,0,272000,'synthetic-v1','aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES('intent-enroll-restore','profile-restore','generation-restore','enroll','pending','none','pending-enrollment','none','not-applicable',NULL,NULL,NULL,'not-required','not-requested','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-enroll-restore-v1','root-enroll-restore','intent-enroll-restore',1,NULL,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_profile_projection VALUES('profile-restore','generation-restore',0,'pending-enrollment','not-applicable',NULL,NULL,NULL,'intent-enroll-restore','not-required','not-requested',1,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_credential_intents VALUES('intent-activate-restore-pending','profile-restore','generation-restore','enroll','pending','pending-enrollment','active-unverified','pending-enrollment','unavailable','account-restore','billing-restore','cap-restore','not-required','not-requested','2026-08-10T00:00:00.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-activate-restore-v1','root-activate-restore','intent-activate-restore-pending',1,NULL,'2026-08-10T00:00:00.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES('intent-activate-restore','profile-restore','generation-restore','enroll','completed','pending-enrollment','active-unverified','active-unverified','unavailable','account-restore','billing-restore','cap-restore','not-required','not-requested','2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-activate-restore-v2','root-activate-restore','intent-activate-restore',2,'event-activate-restore-v1','2026-08-10T00:00:01.0000000+00:00');
            UPDATE provider_profile_projection SET lifecycle_state='active-unverified',verification_state='unavailable',capability_snapshot_id='cap-restore',account_identity_id='account-restore',billing_scope_identity_id='billing-restore',intent_id='intent-activate-restore',projection_version=2,updated_at='2026-08-10T00:00:01.0000000+00:00' WHERE profile_id='profile-restore';
            INSERT INTO provider_credential_intents VALUES('intent-verify-restore-pending','profile-restore','generation-restore','verify','pending','active-unverified','active-verified','active-unverified','available','account-restore','billing-restore','cap-restore','not-required','not-requested','2026-08-10T00:00:01.5000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-verify-restore-v1','root-verify-restore','intent-verify-restore-pending',1,NULL,'2026-08-10T00:00:01.5000000+00:00');
            INSERT INTO provider_credential_intents VALUES('intent-verify-restore','profile-restore','generation-restore','verify','completed','active-unverified','active-verified','active-verified','available','account-restore','billing-restore','cap-restore','not-required','not-requested','2026-08-10T00:00:02.0000000+00:00');
            INSERT INTO provider_credential_intent_events VALUES('event-verify-restore-v2','root-verify-restore','intent-verify-restore',2,'event-verify-restore-v1','2026-08-10T00:00:02.0000000+00:00');
            UPDATE provider_profile_projection SET lifecycle_state='active-verified',verification_state='available',intent_id='intent-verify-restore',projection_version=3,updated_at='2026-08-10T00:00:02.0000000+00:00' WHERE profile_id='profile-restore';
            INSERT INTO provider_price_snapshots VALUES('price-restore','openai','gpt-5.6-sol','USD','default','synthetic-v1','bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_price_rules VALUES('price-restore','rule-restore','standard-under-272k','ordinary-input','input','none','global',5000,1,'synthetic-v1');
            INSERT INTO provider_price_rules VALUES('price-restore','rule-restore-output','standard-under-272k','none','output','none','global',30000,1,'synthetic-v1');
            INSERT INTO runs VALUES('run-restore','install-restore','context-restore','config-restore','manifest-restore','created',0,1,1,'2026-08-10T00:00:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO job_nodes VALUES('job-restore','run-restore',NULL,'provider','created',0,'2026-08-10T00:00:00.0000000+00:00','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO durable_commands VALUES('command-restore','provider','run-restore',0,'recorded','created',NULL,'2026-08-10T00:00:00.0000000+00:00',NULL,NULL);
            INSERT INTO evidence_acquisition_runs VALUES('acquisition-restore','install-restore','context-restore','config-restore','manifest-restore','run-restore','application-restore','cost-restore',$providerLifecycleState,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO evidence_acquisition_job_nodes VALUES('acquisition-job-restore','acquisition-restore','provider',$providerLifecycleState,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO evidence_acquisition_commands VALUES('acquisition-command-restore','acquisition-restore','provider-operation','2026-08-10T00:00:00.0000000+00:00','recorded');
            INSERT INTO provider_command_bindings VALUES('acquisition-command-restore','evidence-acquisition-run','acquisition-restore','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_effective_scan_configurations_v2 VALUES('config-v2-restore','config-restore','abababababababababababababababababababababababababababababababab','asserted-retained-v1-identity','profile-restore','generation-restore','gpt-5.6-sol','medium','current_turn','standard',0,'default',0,0,'none',0,'disabled','explicit',0,0,65536,73728,4096,1048576,1,600000000,120000,'["hosted-search","nexus","loot"]','2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO evidence_acquisition_parent_links VALUES('parent-restore','acquisition-restore','run-restore','initiated-by',NULL,'2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO payloads VALUES('request-payload-restore',$canonicalRequestSha256,1024,'application/json','retained',
              'payloads/' || substr($canonicalRequestSha256,1,2) || '/' || substr($canonicalRequestSha256,3,2) || '/' || $canonicalRequestSha256,
              '2026-08-10T00:00:00.0000000+00:00');
            INSERT INTO provider_operation_blocks(
              operation_id,owner_kind,owner_id,job_node_id,command_id,requested_at,confirmed_at,
              installation_snapshot_id,analysis_context_id,effective_configuration_id,resolved_input_manifest_id,
              profile_id,generation_id,revocation_epoch,operation_kind,capability_snapshot_id,price_snapshot_id,
              prompt_id,prompt_fingerprint,output_schema_id,output_schema_fingerprint,request_fingerprint,
              canonical_request_payload_id,canonical_request_fingerprint,canonical_request_bytes,settings_fingerprint,
              input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,maximum_request_bytes,
              maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,
              maximum_calculated_nano_usd,deadline_milliseconds,dispatch_deadline_utc,coordinator_fencing_epoch,state,recorded_at)
            VALUES('operation-restore','evidence-acquisition-run','acquisition-restore','acquisition-job-restore','acquisition-command-restore',
              '2026-08-10T00:00:00.0000000+00:00','2026-08-10T00:00:01.0000000+00:00','install-restore','context-restore','config-v2-restore','manifest-restore',
              'profile-restore','generation-restore',0,'source-claim-extraction','cap-restore','price-restore','prompt-restore',
              'dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd','schema-restore',
              'eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee',
              $canonicalRequestSha256,'request-payload-restore',
              $canonicalRequestSha256,1024,
              '9999999999999999999999999999999999999999999999999999999999999999',
              'unresolved-openai-responses-framing','authority-required','authority-required',65536,73728,4096,
              1048576,1,600000000,120000,'2026-08-10T00:02:00.0000000+00:00',1,'input-bound-blocked','2026-08-10T00:00:01.0000000+00:00');
            INSERT INTO provider_operation_projection VALUES(
              'operation-restore','input-bound-blocked',0,0,0,1,'2026-08-10T00:00:00.0000000+00:00');
            """;
        command.ExecuteNonQuery();
    }

    private static void AssertSqlSettlement(
        long dispatchCount,
        long inputTokens,
        long outputTokens,
        long reasoningTokens,
        long cacheReadTokens,
        long cacheWriteTokens,
        long pricedToolCalls,
        long calculatedNanoUsd,
        string state,
        bool succeeds)
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        SeedProviderAuthorityBlock(temporary.Root);
        using SqliteConnection connection = OpenRaw(temporary.Root);
        using SqliteCommand command = connection.CreateCommand();
        command.Parameters.AddWithValue("$dispatch_count", dispatchCount);
        command.Parameters.AddWithValue("$input_tokens", inputTokens);
        command.Parameters.AddWithValue("$output_tokens", outputTokens);
        command.Parameters.AddWithValue("$total_tokens", inputTokens + outputTokens);
        command.Parameters.AddWithValue("$reasoning_tokens", reasoningTokens);
        command.Parameters.AddWithValue("$cache_read_tokens", cacheReadTokens);
        command.Parameters.AddWithValue("$cache_write_tokens", cacheWriteTokens);
        command.Parameters.AddWithValue("$priced_tool_calls", pricedToolCalls);
        command.Parameters.AddWithValue("$calculated_nano_usd", calculatedNanoUsd);
        command.Parameters.AddWithValue("$settlement_state", state);
        command.CommandText =
            """
            PRAGMA foreign_keys=ON;
            DROP TRIGGER provider_authority_release_required;
            INSERT INTO provider_operation_authorizations(
              authorization_id,operation_id,owner_kind,owner_id,evidence_acquisition_run_id,job_node_id,command_id,requested_at,
              profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
              effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
              output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
              price_snapshot_id,settings_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,
              coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,
              maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,
              dispatch_deadline_utc,confirmed_at)
            SELECT 'authorization-settlement',operation_id,owner_kind,owner_id,owner_id,job_node_id,command_id,requested_at,
              profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
              effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
              output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
              price_snapshot_id,settings_fingerprint,'openai-responses-o200k-byte-envelope','v1','proved',coordinator_fencing_epoch,
              maximum_request_bytes,20,10,maximum_raw_response_bytes,maximum_dispatch_count,200,deadline_milliseconds,
              dispatch_deadline_utc,confirmed_at
            FROM provider_operation_blocks WHERE operation_id='operation-restore';
            INSERT INTO provider_operation_attempts VALUES(
              'attempt-settlement','operation-restore',1,'proposed',1,'2026-08-10T00:00:02.0000000+00:00');
            INSERT INTO provider_requests(
              request_id,client_request_id,operation_id,provider_attempt_id,request_fingerprint,
              canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,
              input_bound_policy_version,input_bound_proof_status,payload_id,payload_fingerprint,payload_bytes,created_at)
            SELECT 'request-settlement','client-request-settlement',operation_id,'attempt-settlement',request_fingerprint,
              canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,
              input_bound_policy_version,input_bound_proof_status,'request-payload-restore',request_fingerprint,1024,
              '2026-08-10T00:00:03.0000000+00:00'
            FROM provider_operation_authorizations WHERE authorization_id='authorization-settlement';
            INSERT INTO provider_reservations VALUES(
              'reservation-settlement','operation-restore','attempt-settlement','request-settlement',
              '{"dispatch_count":1,"input_tokens":10,"output_tokens":5,"total_tokens":15,"reasoning_tokens":2,"cache_read_tokens":0,"cache_write_tokens":0,"priced_tool_calls":0,"calculated_nano_usd":100}',
              1,10,5,2,0,0,0,100,
              '2026-08-10T00:01:30.0000000+00:00','2026-08-10T00:00:04.0000000+00:00');
            INSERT INTO provider_reservation_scope_items VALUES(
              'scope-settlement','reservation-settlement','operation','operation-restore',
              '{"dispatch_count":1,"input_tokens":10,"output_tokens":5,"total_tokens":15,"reasoning_tokens":2,"cache_read_tokens":0,"cache_write_tokens":0,"priced_tool_calls":0,"calculated_nano_usd":100}',100);
            INSERT INTO provider_dispatch_fences VALUES(
              'fence-settlement','authorization-settlement','operation-restore','reservation-settlement','request-settlement',
              'attempt-settlement',1,'profile-restore','generation-restore',0,1,'synthetic admitted fence',
              '2026-08-10T00:00:05.0000000+00:00');
            INSERT INTO provider_transport_events VALUES(
              'transport-settlement-1','operation-restore','attempt-settlement','request-settlement','fence-settlement',
              'not-started',1,'2026-08-10T00:00:06.0000000+00:00');
            INSERT INTO provider_transport_events VALUES(
              'transport-settlement-2','operation-restore','attempt-settlement','request-settlement','fence-settlement',
              'started',2,'2026-08-10T00:00:07.0000000+00:00');
            INSERT INTO provider_transport_events VALUES(
              'transport-settlement-3','operation-restore','attempt-settlement','request-settlement','fence-settlement',
              'response-staged',3,'2026-08-10T00:00:08.0000000+00:00');
            INSERT INTO provider_responses(
              response_record_id,availability,usage_availability,authorization_id,operation_id,owner_kind,owner_id,
              request_id,provider_attempt_id,reservation_id,dispatch_fence_id,operation_kind,maximum_input_tokens,maximum_output_tokens,
              maximum_calculated_nano_usd,raw_response_availability,raw_response_payload_id,raw_response_fingerprint,
              raw_response_bytes,maximum_raw_response_bytes,response_headers_availability,http_status_availability,
              http_status,provider_response_id_availability,client_request_id,client_request_id_availability,
              provider_request_id_availability,billing_evidence_availability,response_state,refusal_availability,
              incomplete_availability,error_availability,requested_model,returned_model,returned_model_availability,
              requested_service_tier,returned_service_tier,returned_service_tier_availability,reasoning_context,
              reasoning_mode,prompt_cache_mode,billing_availability,rate_availability,expected_rate_limit_fact_count,
              credit_availability,validation_state,admission_state,created_at)
            SELECT 'response-settlement','available','available','authorization-settlement',operation_id,owner_kind,owner_id,
              'request-settlement','attempt-settlement','reservation-settlement','fence-settlement',operation_kind,maximum_input_tokens,
              maximum_output_tokens,maximum_calculated_nano_usd,'available','request-payload-restore',request_fingerprint,
              1024,maximum_raw_response_bytes,'unavailable','available',200,'unavailable','client-request-settlement',
              'available','unavailable','unavailable','completed','unavailable','unavailable','unavailable','gpt-5.6-sol',
              'gpt-5.6-sol','available','default','default','available','current_turn','standard','explicit','unavailable',
              'unavailable',0,'unavailable','proposed','proposed','2026-08-10T00:00:09.0000000+00:00'
            FROM provider_operation_authorizations WHERE authorization_id='authorization-settlement';
            INSERT INTO provider_usage_entries(
              usage_entry_id,receipt_id,availability,operation_id,provider_attempt_id,request_id,dispatch_fence_id,response_record_id,
              dispatch_count_availability,dispatch_count,input_tokens_availability,input_tokens,output_tokens_availability,
              output_tokens,total_tokens_availability,total_tokens,reasoning_tokens_availability,reasoning_tokens,
              cache_read_tokens_availability,cache_read_tokens,cache_write_tokens_availability,cache_write_tokens,
              priced_tool_calls_availability,priced_tool_calls,calculated_nano_usd_availability,calculated_nano_usd,
              billing_availability,rate_availability,credit_availability,receipt_state,created_at)
            VALUES('usage-settlement','receipt-settlement','available','operation-restore','attempt-settlement','request-settlement','fence-settlement',
              'response-settlement','available',$dispatch_count,'available',$input_tokens,'available',$output_tokens,'available',$total_tokens,
              'available',$reasoning_tokens,'available',$cache_read_tokens,'available',$cache_write_tokens,
              'available',$priced_tool_calls,'available',$calculated_nano_usd,
              'unavailable','unavailable','unavailable','complete','2026-08-10T00:00:10.0000000+00:00');
            """;
        Assert.AreEqual(11, command.ExecuteNonQuery());
        bool policyViolation = dispatchCount > 1 || cacheReadTokens > 0 || cacheWriteTokens > 0 || pricedToolCalls > 0;
        if (succeeds && policyViolation)
        {
            command.CommandText =
                """
                INSERT INTO provider_response_finalizations VALUES(
                  'finalization-settlement','response-settlement','usage-settlement','admitted','admitted',
                  '2026-08-10T00:00:10.5000000+00:00');
                """;
            SqliteException admittedOverrun = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
            StringAssert.Contains(admittedOverrun.Message, "exact rate/admission state");
            command.CommandText = command.CommandText
                .Replace("'admitted','admitted'", "'rejected','rejected'", StringComparison.Ordinal);
            Assert.AreEqual(1, command.ExecuteNonQuery());
        }
        command.CommandText =
            """
            INSERT INTO provider_reservation_scope_items VALUES(
              'scope-settlement-mismatch','reservation-settlement','global','global','{}',100);
            """;
        SqliteException scopeMismatch = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(scopeMismatch.Message, "exact operation reservation vector");
        command.CommandText =
            """
            INSERT INTO provider_settlements VALUES(
              'settlement-observed','operation-restore','attempt-settlement','request-settlement','reservation-settlement',
              'usage-settlement','fence-settlement',$settlement_state,100,0,'2026-08-10T00:00:11.0000000+00:00');
            """;
        if (succeeds)
        {
            string validSettlement = command.CommandText;
            command.CommandText = validSettlement.Replace(
                ",$settlement_state,100,0,",
                ",$settlement_state,99,0,",
                StringComparison.Ordinal);
            SqliteException amountMismatch = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
            StringAssert.Contains(amountMismatch.Message, "exactly partition the reservation");
            command.CommandText = validSettlement;
            Assert.AreEqual(1, command.ExecuteNonQuery());
            command.CommandText =
                """
                SELECT count(*) FROM provider_settlement_vector_partitions
                WHERE settlement_id='settlement-observed'
                  AND reserved_dispatch_count = released_dispatch_count + retained_dispatch_count
                  AND reserved_input_tokens = released_input_tokens + retained_input_tokens
                  AND reserved_output_tokens = released_output_tokens + retained_output_tokens
                  AND reserved_total_tokens = released_total_tokens + retained_total_tokens
                  AND reserved_reasoning_tokens = released_reasoning_tokens + retained_reasoning_tokens
                  AND reserved_cache_read_tokens = released_cache_read_tokens + retained_cache_read_tokens
                  AND reserved_cache_write_tokens = released_cache_write_tokens + retained_cache_write_tokens
                  AND reserved_priced_tool_calls = released_priced_tool_calls + retained_priced_tool_calls
                  AND reserved_nano_usd = released_nano_usd + retained_hold_nano_usd;
                """;
            Assert.AreEqual(1L, (long)command.ExecuteScalar()!);
        }
        else
        {
            Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        }
    }

    private static void AssertCancelledTerminal(SqliteCommand command)
    {
        SqliteException exception = Assert.ThrowsExactly<SqliteException>(() => command.ExecuteNonQuery());
        StringAssert.Contains(exception.Message, "cancelled provider operation is terminal");
    }

    private static SqliteConnection OpenRaw(string productRoot)
    {
        SqliteRuntimeIdentity.InitializeNativeProvider();
        using StoragePaths paths = new(productRoot);
        SqliteConnection connection = new(
            new SqliteConnectionStringBuilder
            {
                DataSource = paths.Database,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
            }.ToString());
        connection.Open();
        return connection;
    }

    private static void ExecuteRaw(string productRoot, string sql)
    {
        using SqliteConnection connection = OpenRaw(productRoot);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static RunRecord Transition(
        AuthoritativeStore store,
        CoordinatorAuthority authority,
        RunRecord current,
        LifecycleState target) =>
        store.Transition(
            Guid.NewGuid().ToString("N"),
            current.RunId,
            current.Generation,
            target,
            authority.FencingEpoch,
            $"test transition to {target}",
            DateTimeOffset.UtcNow);

    private sealed class TemporaryStore : IDisposable
    {
        public TemporaryStore()
        {
            Root = Path.Combine(Path.GetTempPath(), $"infinium-persistence-{Guid.NewGuid():N}");
        }

        public string Root { get; }

        public AuthoritativeStore Open() => new(new StoragePaths(Root));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
