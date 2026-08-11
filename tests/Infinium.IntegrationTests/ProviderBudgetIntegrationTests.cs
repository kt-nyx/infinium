using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderBudgetIntegrationTests
{
    private static readonly DateTimeOffset BaseTime =
        DateTimeOffset.Parse("2026-08-10T00:00:00.0000000+00:00", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly string[] OutputGaps =
        ["provider-billing-unavailable", "prepaid-credit-unavailable"];

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderReservationAndDispatchFenceUseTheRealAtomicSqlitePath()
    {
        using BudgetContext context = BudgetContext.Create();
        ProviderReservationAdmissionContract admission = context.Store.ReserveProviderBudget(1, context.Request);
        Assert.AreEqual("reservation-settlement", admission.ReservationId.Value);
        foreach (ProviderBudgetScopeContract scope in context.Scopes)
        {
            ProviderBudgetProjectionContract projection = context.Store.GetProviderBudgetProjection(scope.ScopeKind, scope.ScopeId.Value);
            Assert.AreEqual(context.Vector, projection.Reserved);
        }

        ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        Assert.IsTrue(fence.Authorized);
        Assert.AreEqual("exact-final-gate-authorized", fence.DecisionReason);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderReservationConcurrentConnectionsCommitExactlyOneVectorWithoutPartialDebit()
    {
        using BudgetContext context = BudgetContext.Create();
        using AuthoritativeStore contender = new(new StoragePaths(context.Root));
        using ManualResetEventSlim start = new(initialState: false);
        ProviderBudgetReservationRequest first = context.Request with { ReservationId = "reservation-race-a" };
        ProviderBudgetReservationRequest second = context.Request with { ReservationId = "reservation-race-b" };
        Exception? firstError = null;
        Exception? secondError = null;
        Task firstTask = Task.Run(() =>
        {
            start.Wait();
            try { _ = context.Store.ReserveProviderBudget(1, first); }
            catch (Exception error) { firstError = error; }
        });
        Task secondTask = Task.Run(() =>
        {
            start.Wait();
            try { _ = contender.ReserveProviderBudget(1, second); }
            catch (Exception error) { secondError = error; }
        });
        start.Set();
        Task.WaitAll(firstTask, secondTask);

        Assert.AreEqual(1, new[] { firstError, secondError }.Count(error => error is null));
        Exception loser = firstError ?? secondError!;
        StringAssert.Contains(loser.Message, "exhausted");
        foreach (ProviderBudgetScopeContract scope in context.Scopes)
        {
            Assert.AreEqual(context.Vector,
                context.Store.GetProviderBudgetProjection(scope.ScopeKind, scope.ScopeId.Value).Reserved);
        }
        using SqliteConnection database = new($"Data Source={context.Store.Paths.Database};Mode=ReadOnly;Pooling=False");
        database.Open();
        using SqliteCommand command = database.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM provider_budget_events WHERE event_kind='reserved';";
        Assert.AreEqual(8L, (long)command.ExecuteScalar()!);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementReleasesKnownUndispatchedAndRebuildsEqualProjection()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        _ = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        ProviderBudgetSettlementReceipt receipt = context.Store.SettleProviderBudget(
            new("settlement-undispatched", "reservation-settlement",
                ProviderBudgetEventKind.ReleasedUndispatched, null, null, BaseTime.AddSeconds(6)));
        Assert.AreEqual(context.Vector, receipt.Released);
        Assert.AreEqual(ProviderBudgetVectorContract.Zero, receipt.Settled);
        Assert.IsFalse(receipt.RetryPermitted);

        IReadOnlyList<ProviderBudgetProjectionContract> rebuilt =
            context.Store.RebuildProviderBudgetProjections(BaseTime.AddSeconds(7));
        Assert.AreEqual(8, rebuilt.Count);
        Assert.IsTrue(rebuilt.All(item => item.Reserved == ProviderBudgetVectorContract.Zero
            && item.Settled == ProviderBudgetVectorContract.Zero
            && item.Unresolved == ProviderBudgetVectorContract.Zero));
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementRetainsAmbiguousStartInFullAndNeverRetries()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        context.Store.RecordProviderTransportStart(
            "operation-restore", "attempt-settlement", "request-settlement", fence.DispatchFenceId,
            ambiguous: true, BaseTime.AddSeconds(6));
        ProviderBudgetSettlementReceipt receipt = context.Store.SettleProviderBudget(
            new("settlement-ambiguous", "reservation-settlement",
                ProviderBudgetEventKind.RetainedAmbiguous, null, null, BaseTime.AddSeconds(7)));
        Assert.AreEqual(context.Vector, receipt.Unresolved);
        Assert.AreEqual(ProviderBudgetVectorContract.Zero, receipt.Released);
        Assert.IsFalse(receipt.RetryPermitted);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementCoversCompleteFailedPartialUnavailableAndOverrunWithoutFallbackOrRetry()
    {
        ProviderBudgetVectorContract expectedUsage = new(1, 8, 4, 12, 1, 0, 0, 0, 80);
        foreach (ProviderBudgetEventKind kind in new[]
                 {
                     ProviderBudgetEventKind.SettledComplete,
                     ProviderBudgetEventKind.SettledFailedKnown,
                 })
        {
            using BudgetContext context = BudgetContext.Create();
            _ = context.Store.ReserveProviderBudget(1, context.Request);
            ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
            context.Store.RecordProviderTransportStart(
                "operation-restore", "attempt-settlement", "request-settlement", fence.DispatchFenceId,
                ambiguous: false, BaseTime.AddSeconds(6));
            context.SeedActualUsage(expectedUsage, failedKnown: kind == ProviderBudgetEventKind.SettledFailedKnown);
            ProviderBudgetSettlementReceipt receipt = context.Store.SettleProviderBudget(
                new("settlement-" + kind, "reservation-settlement", kind,
                    "usage-settlement", expectedUsage, BaseTime.AddSeconds(10)));
            Assert.AreEqual(expectedUsage, receipt.Settled);
            Assert.AreEqual(ProviderBudgetVectorContract.Zero, receipt.Unresolved);
            Assert.IsFalse(receipt.RetryPermitted);
            Assert.AreEqual((1L, 1L), context.CountOwnedAndAttachedUsageRollups());
            Assert.ThrowsExactly<InvalidOperationException>(() => context.Store.SettleProviderBudget(
                new("settlement-duplicate", "reservation-settlement", kind,
                    "usage-settlement", expectedUsage, BaseTime.AddSeconds(11))));
        }

        foreach (ProviderBudgetEventKind kind in new[]
                 {
                     ProviderBudgetEventKind.RetainedPartial,
                     ProviderBudgetEventKind.RetainedUnavailable,
                 })
        {
            using BudgetContext context = BudgetContext.Create();
            _ = context.Store.ReserveProviderBudget(1, context.Request);
            ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
            context.Store.RecordProviderTransportStart(
                "operation-restore", "attempt-settlement", "request-settlement", fence.DispatchFenceId,
                ambiguous: false, BaseTime.AddSeconds(6));
            ProviderBudgetSettlementReceipt receipt = context.Store.SettleProviderBudget(
                new("settlement-" + kind, "reservation-settlement", kind,
                    null, null, BaseTime.AddSeconds(7)));
            Assert.AreEqual(context.Vector, receipt.Unresolved);
            Assert.AreEqual(ProviderBudgetVectorContract.Zero, receipt.Settled);
            Assert.IsFalse(receipt.RetryPermitted);
        }

        using (BudgetContext context = BudgetContext.Create())
        {
            ProviderBudgetVectorContract overrun = new(1, 11, 5, 16, 2, 0, 0, 0, 101);
            _ = context.Store.ReserveProviderBudget(1, context.Request);
            ProviderDispatchGateReceipt fence = context.Store.AuthorizeProviderDispatch(context.GateRequest);
            context.Store.RecordProviderTransportStart(
                "operation-restore", "attempt-settlement", "request-settlement", fence.DispatchFenceId,
                ambiguous: false, BaseTime.AddSeconds(6));
            context.SeedActualUsage(overrun, failedKnown: false);
            ProviderBudgetSettlementReceipt receipt = context.Store.SettleProviderBudget(
                new("settlement-overrun", "reservation-settlement", ProviderBudgetEventKind.SettledOverrun,
                    "usage-settlement", overrun, BaseTime.AddSeconds(10)));
            Assert.AreEqual(overrun, receipt.Settled);
            Assert.IsFalse(receipt.RetryPermitted);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderBudgetReplayAndHumanJsonOutputCreateNoNewDebitOrDispatch()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        _ = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        _ = context.Store.SettleProviderBudget(new(
            "settlement-replay", "reservation-settlement", ProviderBudgetEventKind.ReleasedUndispatched,
            null, null, BaseTime.AddSeconds(6)));
        (long eventsBefore, long fencesBefore) = context.CountLedgerRoots();

        ProviderBudgetProjectionContract rebuilt = context.Store
            .RebuildProviderBudgetProjections(BaseTime.AddSeconds(7))
            .Single(item => item.ScopeKind == "operation");
        ProviderBudgetOutputDocument output = ProviderBudgetOutputRenderer.CreateDocument(
            rebuilt, OutputGaps);
        string json = ProviderBudgetOutputRenderer.RenderJson(output);
        string human = ProviderBudgetOutputRenderer.RenderHuman(output);
        (long eventsAfter, long fencesAfter) = context.CountLedgerRoots();

        Assert.AreEqual(eventsBefore, eventsAfter, "Replay must not append a new debit event.");
        Assert.AreEqual(fencesBefore, fencesAfter, "Replay must not authorize another dispatch.");
        StringAssert.Contains(json, "\"network_used\": false");
        StringAssert.Contains(json, "\"credential_accessed\": false");
        StringAssert.Contains(human, "Execution mode: simulated-nonnetwork");
        StringAssert.Contains(human, "provider-billing-unavailable");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderReservationRejectsPausedOwnerBeforeAnyDebit()
    {
        using BudgetContext context = BudgetContext.Create("paused");
        InvalidOperationException error = Assert.ThrowsExactly<InvalidOperationException>(
            () => context.Store.ReserveProviderBudget(1, context.Request));
        StringAssert.Contains(error.Message, "authorized request and attempt root");
        Assert.AreEqual((0L, 0L), context.CountLedgerRoots());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderCatalogPublicationIsImmutableAndIdempotent()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp2-Catalog-" + Guid.NewGuid().ToString("N"));
        try
        {
            using AuthoritativeStore store = new(new StoragePaths(root));
            store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, BaseTime);
            store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, BaseTime.AddSeconds(1));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void UsageSettlementBackupRestoreRetainsAuthoritativeEventsAndRebuildableProjection()
    {
        using BudgetContext context = BudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        _ = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        _ = context.Store.SettleProviderBudget(new(
            "settlement-backup", "reservation-settlement", ProviderBudgetEventKind.ReleasedUndispatched,
            null, null, BaseTime.AddSeconds(6)));
        BackupArtifact backup = context.Store.CreateBackup("Wp2Budget", BaseTime.AddSeconds(7));
        string restoredRoot = Path.Combine(Path.GetTempPath(), "Infinium-Wp2-Restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (StoragePaths targetPaths = new(restoredRoot))
            {
                AuthoritativeStore.RestoreBackup(backup, targetPaths);
            }
            using (StoragePaths restoredPaths = new(restoredRoot))
            using (AuthoritativeStore restored = new(restoredPaths))
            {
                ProviderBudgetProjectionContract before = context.Store.GetProviderBudgetProjection(
                    "operation", "operation-restore");
                ProviderBudgetProjectionContract after = restored.GetProviderBudgetProjection(
                    "operation", "operation-restore");
                Assert.AreEqual(before.Reserved, after.Reserved);
                Assert.AreEqual(before.Settled, after.Settled);
                Assert.AreEqual(before.Unresolved, after.Unresolved);
                Assert.AreEqual(8, restored.RebuildProviderBudgetProjections(BaseTime.AddSeconds(8)).Count);
            }
        }
        finally
        {
            if (Directory.Exists(restoredRoot))
            {
                DeleteDirectoryWithRetry(restoredRoot);
            }
        }
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 9)
            {
                Thread.Sleep(25 * (attempt + 1));
            }
        }
    }

    private sealed class BudgetContext : IDisposable
    {
        private BudgetContext(string root, AuthoritativeStore store, ProviderBudgetVectorContract vector,
            IReadOnlyList<ProviderBudgetScopeContract> scopes)
        {
            Root = root;
            Store = store;
            Vector = vector;
            Scopes = scopes;
            Request = new("reservation-settlement", "operation-restore", "attempt-settlement", "request-settlement",
                vector, scopes, BaseTime.AddSeconds(90), BaseTime.AddSeconds(4));
            GateRequest = new("fence-settlement", "authorization-settlement", "operation-restore",
                "reservation-settlement", "attempt-settlement", "request-settlement", "profile-restore",
                "generation-restore", 0, 1, BaseTime.AddSeconds(5));
        }

        public string Root { get; }
        public AuthoritativeStore Store { get; }
        public ProviderBudgetVectorContract Vector { get; }
        public IReadOnlyList<ProviderBudgetScopeContract> Scopes { get; }
        public ProviderBudgetReservationRequest Request { get; }
        public ProviderDispatchGateRequest GateRequest { get; }

        public static BudgetContext Create(string lifecycleState = "running")
        {
            string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp2-" + Guid.NewGuid().ToString("N"));
            AuthoritativeStore store = new(new StoragePaths(root));
            try
            {
                PersistenceAndLifecycleTests.SeedProviderAuthorityBlock(root, lifecycleState);
                using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = AuthorizationSql;
                Assert.AreEqual(3, command.ExecuteNonQuery());

                CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                    "wp2-integration", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
                Assert.AreEqual(1L, authority.FencingEpoch);
                ProviderBudgetVectorContract vector = new(1, 10, 5, 15, 2, 0, 0, 0, 100);
                string[] kinds = ["request", "operation", "evidence-acquisition-run", "analysis-run",
                    "provider-profile", "provider-account", "billing-scope", "global"];
                string[] ids = ["request-settlement", "operation-restore", "acquisition-restore", "run-restore",
                    "profile-restore", "account-restore", "billing-restore", "provider-global"];
                ProviderBudgetScopeContract[] scopes = kinds.Zip(ids,
                    (kind, id) => new ProviderBudgetScopeContract(kind, new OpaqueId(id), vector)).ToArray();
                store.ConfigureProviderBudgetScopes(1, scopes, BaseTime.AddSeconds(3));
                return new(root, store, vector, scopes);
            }
            catch
            {
                store.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Store.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        public (long Events, long Fences) CountLedgerRoots()
        {
            using SqliteConnection database = new($"Data Source={Store.Paths.Database};Mode=ReadOnly;Pooling=False");
            database.Open();
            using SqliteCommand command = database.CreateCommand();
            command.CommandText =
                "SELECT (SELECT COUNT(*) FROM provider_budget_events), (SELECT COUNT(*) FROM provider_dispatch_fences);";
            using SqliteDataReader reader = command.ExecuteReader();
            Assert.IsTrue(reader.Read());
            return (reader.GetInt64(0), reader.GetInt64(1));
        }

        public (long Owner, long Attached) CountOwnedAndAttachedUsageRollups()
        {
            using SqliteConnection database = new($"Data Source={Store.Paths.Database};Mode=ReadOnly;Pooling=False");
            database.Open();
            using SqliteCommand command = database.CreateCommand();
            command.CommandText =
                """
                SELECT
                  COUNT(CASE WHEN attribution_kind='owner' THEN 1 END),
                  COUNT(CASE WHEN attribution_kind='attached-pre-cutoff' AND dispatch_sequence_cutoff=2 THEN 1 END)
                FROM provider_usage_rollup_references WHERE usage_entry_id='usage-settlement';
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            Assert.IsTrue(reader.Read());
            return (reader.GetInt64(0), reader.GetInt64(1));
        }

        public void SeedActualUsage(ProviderBudgetVectorContract actual, bool failedKnown)
        {
            using SqliteConnection database = new($"Data Source={Store.Paths.Database};Pooling=False");
            database.Open();
            using SqliteCommand command = database.CreateCommand();
            command.Parameters.AddWithValue("$dispatch", actual.DispatchCount);
            command.Parameters.AddWithValue("$input", actual.InputTokens);
            command.Parameters.AddWithValue("$output", actual.OutputTokens);
            command.Parameters.AddWithValue("$total", actual.TotalTokens);
            command.Parameters.AddWithValue("$reasoning", actual.ReasoningTokens);
            command.Parameters.AddWithValue("$cache_read", actual.CacheReadTokens);
            command.Parameters.AddWithValue("$cache_write", actual.CacheWriteTokens);
            command.Parameters.AddWithValue("$tools", actual.PricedToolCalls);
            command.Parameters.AddWithValue("$nano", actual.NanoUsd);
            command.Parameters.AddWithValue("$response_state", failedKnown ? "failed" : "completed");
            command.Parameters.AddWithValue("$receipt_state", failedKnown ? "failed-known" : "complete");
            command.Parameters.AddWithValue("$error_availability", failedKnown ? "available" : "unavailable");
            command.Parameters.AddWithValue("$error_code", failedKnown ? "simulated-known-failure" : DBNull.Value);
            command.Parameters.AddWithValue("$validation", failedKnown ? "rejected" : "proposed");
            command.CommandText =
                """
                INSERT INTO provider_transport_events VALUES(
                  'fence-settlement:response','operation-restore','attempt-settlement','request-settlement','fence-settlement',
                  'response-staged',3,'2026-08-10T00:00:07.0000000+00:00');
                INSERT INTO provider_responses(
                  response_record_id,availability,usage_availability,authorization_id,operation_id,owner_kind,owner_id,
                  request_id,provider_attempt_id,reservation_id,dispatch_fence_id,operation_kind,maximum_input_tokens,maximum_output_tokens,
                  maximum_calculated_nano_usd,raw_response_availability,raw_response_payload_id,raw_response_fingerprint,
                  raw_response_bytes,maximum_raw_response_bytes,response_headers_availability,http_status_availability,
                  http_status,provider_response_id_availability,client_request_id,client_request_id_availability,
                  provider_request_id_availability,billing_evidence_availability,response_state,refusal_availability,
                  incomplete_availability,error_availability,error_code,requested_model,returned_model,returned_model_availability,
                  requested_service_tier,returned_service_tier,returned_service_tier_availability,reasoning_context,
                  reasoning_mode,prompt_cache_mode,billing_availability,rate_availability,expected_rate_limit_fact_count,
                  credit_availability,validation_state,admission_state,created_at)
                SELECT 'response-settlement','available','available','authorization-settlement',operation_id,owner_kind,owner_id,
                  'request-settlement','attempt-settlement','reservation-settlement','fence-settlement',operation_kind,maximum_input_tokens,
                  maximum_output_tokens,maximum_calculated_nano_usd,'available','request-payload-restore',request_fingerprint,
                  1024,maximum_raw_response_bytes,'unavailable','available',200,'unavailable','client-request-settlement',
                  'available','unavailable','unavailable',$response_state,'unavailable','unavailable',$error_availability,$error_code,
                  'gpt-5.6-sol','gpt-5.6-sol','available','default','default','available','current_turn','standard','explicit','unavailable',
                  'unavailable',0,'unavailable',$validation,$validation,'2026-08-10T00:00:08.0000000+00:00'
                FROM provider_operation_authorizations WHERE authorization_id='authorization-settlement';
                INSERT INTO provider_usage_entries(
                  usage_entry_id,receipt_id,availability,operation_id,provider_attempt_id,request_id,dispatch_fence_id,response_record_id,
                  dispatch_count_availability,dispatch_count,input_tokens_availability,input_tokens,output_tokens_availability,
                  output_tokens,total_tokens_availability,total_tokens,reasoning_tokens_availability,reasoning_tokens,
                  cache_read_tokens_availability,cache_read_tokens,cache_write_tokens_availability,cache_write_tokens,
                  priced_tool_calls_availability,priced_tool_calls,calculated_nano_usd_availability,calculated_nano_usd,
                  billing_availability,rate_availability,credit_availability,receipt_state,created_at)
                VALUES('usage-settlement','receipt-settlement','available','operation-restore','attempt-settlement','request-settlement','fence-settlement',
                  'response-settlement','available',$dispatch,'available',$input,'available',$output,'available',$total,
                  'available',$reasoning,'available',$cache_read,'available',$cache_write,'available',$tools,'available',$nano,
                  'unavailable','unavailable','unavailable',$receipt_state,'2026-08-10T00:00:09.0000000+00:00');
                """;
            Assert.AreEqual(3, command.ExecuteNonQuery());
        }
    }

    private const string AuthorizationSql =
        """
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
        """;
}
