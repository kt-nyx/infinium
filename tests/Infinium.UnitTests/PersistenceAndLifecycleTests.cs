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
        Assert.AreEqual(23L, reader.GetInt64(1));
        Assert.AreEqual(40L, reader.GetInt64(2));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void Schema6ProviderPersistenceBackupRestoreRoundTripRetainsMigrationAndProjectionContract()
    {
        using TemporaryStore source = new();
        BackupArtifact backup;
        using (AuthoritativeStore store = source.Open())
        {
            SeedProviderReplayAuthorization(source.Root);
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
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id='M1-S6-0006' AND from_version=5 AND to_version=6;
                """;
            Assert.AreEqual(1L, (long)command.ExecuteScalar()!);
            command.CommandText =
                """
                SELECT a.profile_id,a.generation_id,a.operation_kind,a.installation_snapshot_id,a.analysis_context_id,
                       a.effective_configuration_id,a.resolved_input_manifest_id,a.prompt_id,a.output_schema_id,
                       a.request_fingerprint,a.canonical_request_fingerprint,a.capability_snapshot_id,a.price_snapshot_id,
                       a.settings_fingerprint,a.input_bound_policy_id,a.input_bound_policy_version,a.input_bound_proof_status,
                       a.canonical_request_bytes,a.proved_input_token_bound,a.maximum_request_bytes,a.maximum_input_tokens,
                       a.maximum_output_tokens,a.maximum_raw_response_bytes,a.maximum_dispatch_count,a.maximum_calculated_nano_usd,
                       a.deadline_milliseconds,r.provider_attempt_id,r.request_id,r.payload_id,v.reservation_id,f.dispatch_fence_id,
                       f.authorized,t.transport_event_id,p.response_record_id,p.raw_response_payload_id,p.raw_response_fingerprint,
                       p.raw_response_bytes,p.http_status,p.provider_response_id,p.requested_model,p.returned_model,
                       p.requested_service_tier,p.returned_service_tier,p.reasoning_context,p.reasoning_mode,p.prompt_cache_mode,
                       u.usage_entry_id,s.settlement_id,q.proposal_id,e.replay_edge_id,o.state
                FROM provider_operation_authorizations a
                JOIN provider_requests r ON r.operation_id=a.operation_id
                JOIN provider_reservations v ON v.operation_id=r.operation_id AND v.provider_attempt_id=r.provider_attempt_id AND v.request_id=r.request_id
                JOIN provider_dispatch_fences f ON f.operation_id=r.operation_id AND f.provider_attempt_id=r.provider_attempt_id AND f.request_id=r.request_id
                JOIN provider_transport_events t ON t.operation_id=f.operation_id AND t.provider_attempt_id=f.provider_attempt_id AND t.request_id=f.request_id AND t.dispatch_fence_id=f.dispatch_fence_id
                JOIN provider_responses p ON p.operation_id=f.operation_id AND p.provider_attempt_id=f.provider_attempt_id AND p.request_id=f.request_id AND p.dispatch_fence_id=f.dispatch_fence_id
                JOIN provider_usage_entries u ON u.response_record_id=p.response_record_id
                JOIN provider_settlements s ON s.usage_entry_id=u.usage_entry_id AND s.dispatch_fence_id=f.dispatch_fence_id
                JOIN provider_semantic_proposals q ON q.response_record_id=p.response_record_id AND q.dispatch_fence_id=f.dispatch_fence_id
                JOIN provider_replay_edges e ON e.response_record_id=p.response_record_id AND e.dispatch_fence_id=f.dispatch_fence_id
                JOIN provider_operation_projection o ON o.operation_id=a.operation_id AND o.dispatch_fence_id=f.dispatch_fence_id
                WHERE a.operation_id='operation-restore';
                """;
            using SqliteDataReader replay = command.ExecuteReader();
            Assert.IsTrue(replay.Read());
            string[] expected =
            [
                "profile-restore", "generation-restore", "source-claim-extraction", "install-restore", "context-restore",
                "config-restore", "manifest-restore", "prompt-restore", "schema-restore", new('e', 64), new('1', 64),
                "cap-restore", "price-restore", new('f', 64), "test-only-structural-proof", "1", "proved",
            ];
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.AreEqual(expected[index], replay.GetString(index));
            }
            long[] numbers = [65_536, 73_728, 65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000];
            for (int index = 0; index < numbers.Length; index++)
            {
                Assert.AreEqual(numbers[index], replay.GetInt64(17 + index));
            }
            string[] graph = ["attempt-restore", "request-restore", "payload-restore", "reservation-restore", "fence-restore"];
            for (int index = 0; index < graph.Length; index++)
            {
                Assert.AreEqual(graph[index], replay.GetString(26 + index));
            }
            Assert.AreEqual(1L, replay.GetInt64(31));
            string[] retained = ["transport-restore", "response-restore", "payload-restore", "44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a"];
            for (int index = 0; index < retained.Length; index++)
            {
                Assert.AreEqual(retained[index], replay.GetString(32 + index));
            }
            Assert.AreEqual(2L, replay.GetInt64(36));
            Assert.AreEqual(200L, replay.GetInt64(37));
            string[] responseAndGraph = ["provider-response-restore", "gpt-5.6-sol", "gpt-5.6-sol", "default", "default",
                "current_turn", "standard", "explicit", "usage-restore", "settlement-restore", "proposal-restore", "replay-restore", "settled"];
            for (int index = 0; index < responseAndGraph.Length; index++)
            {
                Assert.AreEqual(responseAndGraph[index], replay.GetString(38 + index));
            }
            Assert.IsFalse(replay.Read());
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
               WHERE schema='main' AND name NOT LIKE 'sqlite_%' AND strict=0),
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

    private static void ExecuteRaw(string productRoot, string sql)
    {
        using SqliteConnection connection = OpenRaw(productRoot);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void SeedProviderReplayAuthorization(string productRoot)
    {
        const string payloadSha = "44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a";
        string payloadPath = Path.Combine(productRoot, "payloads", payloadSha[..2], payloadSha[2..4], payloadSha);
        Directory.CreateDirectory(Path.GetDirectoryName(payloadPath)!);
        File.WriteAllBytes(payloadPath, "{}"u8.ToArray());
        using SqliteConnection connection = OpenRaw(productRoot);
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA foreign_keys=ON;
            INSERT INTO provider_access_profiles VALUES('profile-restore','openai','responses','Restore','account-restore','billing-restore','2026-08-10T00:00:00Z');
            INSERT INTO provider_generations VALUES('generation-restore','profile-restore',1,0,'2026-08-10T00:00:00Z');
            INSERT INTO provider_capability_snapshots VALUES('cap-restore','openai','gpt-5.6-sol','default','medium','current_turn','standard',0,0,0,'none',0,'disabled','explicit',0,0,272000,'synthetic-v1','aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa','2026-08-10T00:00:00Z');
            INSERT INTO provider_price_snapshots VALUES('price-restore','openai','gpt-5.6-sol','USD','default','synthetic-v1','bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb','2026-08-10T00:00:00Z');
            INSERT INTO provider_price_rules VALUES('price-restore','rule-restore','standard-under-272k','ordinary-input','input','none','global',1,1,'synthetic-v1');
            INSERT INTO runs VALUES('run-restore','install-restore','context-restore','config-restore','manifest-restore','created',0,1,1,'2026-08-10T00:00:00Z','2026-08-10T00:00:00Z');
            INSERT INTO job_nodes VALUES('job-restore','run-restore',NULL,'provider','created',0,'2026-08-10T00:00:00Z','2026-08-10T00:00:00Z');
            INSERT INTO payloads VALUES('payload-restore','44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a',2,'json','retained','payloads/44/13/44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a','2026-08-10T00:00:00Z');
            INSERT INTO provider_operation_authorizations(
              authorization_id,operation_id,owner_kind,owner_id,analysis_run_id,evidence_acquisition_run_id,job_node_id,
              profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,effective_configuration_id,resolved_input_manifest_id,
              prompt_id,prompt_fingerprint,output_schema_id,output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,price_snapshot_id,settings_fingerprint,
              input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,canonical_request_bytes,proved_input_token_bound,coordinator_fencing_epoch,
              maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,confirmed_at) VALUES
              ('auth-restore','operation-restore','analysis-run','run-restore','run-restore',NULL,'job-restore','profile-restore','generation-restore',0,'source-claim-extraction','install-restore','context-restore','config-restore','manifest-restore','prompt-restore','cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc','schema-restore','dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd','eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee','1111111111111111111111111111111111111111111111111111111111111111','cap-restore','price-restore','ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff','test-only-structural-proof','1','proved',65536,73728,1,65536,73728,4096,1048576,1,600000000,120000,'2026-08-10T00:00:00Z');
            INSERT INTO provider_operation_attempts VALUES('attempt-restore','operation-restore',1,'proposed',1,'2026-08-10T00:00:00Z');
            INSERT INTO provider_requests VALUES('request-restore','operation-restore','attempt-restore','eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee','1111111111111111111111111111111111111111111111111111111111111111','ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff','dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd','test-only-structural-proof','1','proved',65536,73728,'payload-restore','2026-08-10T00:00:00Z');
            INSERT INTO provider_reservations VALUES('reservation-restore','operation-restore','attempt-restore','request-restore','{}',600000000,'2026-08-10T00:01:00Z','2026-08-10T00:00:00Z');
            INSERT INTO provider_dispatch_fences VALUES('fence-restore','auth-restore','operation-restore','reservation-restore','request-restore','attempt-restore',1,'profile-restore','generation-restore',0,1,'proved','2026-08-10T00:00:00Z');
            INSERT INTO provider_transport_events VALUES('transport-restore','operation-restore','attempt-restore','request-restore','fence-restore','response-staged',1,'2026-08-10T00:00:00Z');
            INSERT INTO provider_responses(response_record_id,operation_id,request_id,provider_attempt_id,dispatch_fence_id,raw_response_payload_id,
              raw_response_fingerprint,raw_response_bytes,http_status,provider_response_id,response_state,requested_model,returned_model,
              requested_service_tier,returned_service_tier,reasoning_context,reasoning_mode,prompt_cache_mode,usage_json,validation_state,admission_state,created_at)
            VALUES('response-restore','operation-restore','request-restore','attempt-restore','fence-restore','payload-restore',
              '44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a',2,200,'provider-response-restore','completed',
              'gpt-5.6-sol','gpt-5.6-sol','default','default','current_turn','standard','explicit',
              '{"dispatch_count":{"availability":"available","value":1},"input_tokens":{"availability":"available","value":1},"output_tokens":{"availability":"available","value":0},"reasoning_tokens":{"availability":"available","value":0},"cache_read_tokens":{"availability":"available","value":0},"cache_write_tokens":{"availability":"available","value":0},"priced_tool_calls":{"availability":"available","value":0},"calculated_nano_usd":{"availability":"available","value":1},"billing_availability":"available","rate_availability":"available","credit_availability":"unavailable"}',
              'rejected','rejected','2026-08-10T00:00:00Z');
            INSERT INTO provider_usage_entries VALUES('usage-restore','operation-restore','attempt-restore','request-restore','fence-restore','response-restore','{}',1,'validated','2026-08-10T00:00:00Z');
            INSERT INTO provider_settlements VALUES('settlement-restore','operation-restore','attempt-restore','request-restore','reservation-restore','usage-restore','fence-restore','settled',600000000,0,'2026-08-10T00:00:00Z');
            INSERT INTO provider_semantic_proposals VALUES('proposal-restore','operation-restore','attempt-restore','request-restore','response-restore','fence-restore','source-claim','payload-restore','2026-08-10T00:00:00Z');
            INSERT INTO provider_semantic_admissions VALUES('admission-restore','proposal-restore','rejected','host-policy','synthetic',NULL,'2026-08-10T00:00:00Z');
            INSERT INTO provider_replay_edges VALUES('replay-restore','operation-restore','attempt-restore','request-restore','response-restore','fence-restore','retained-response','manifest-restore','2026-08-10T00:00:00Z');
            INSERT INTO provider_operation_projection VALUES('operation-restore','attempt-restore','request-restore','fence-restore','settled',600000000,1,0,NULL,1,'2026-08-10T00:00:00Z');
            """;
        command.ExecuteNonQuery();
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
