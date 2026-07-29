using System.Security.Cryptography;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class PersistenceAndLifecycleTests
{
    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
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
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void LifecyclePolicyCoversPauseResumeCancelAndTerminalClosure()
    {
        LifecyclePolicy.EnsureAllowed(LifecycleState.Queued, LifecycleState.Running);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Running, LifecycleState.Pausing);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Pausing, LifecycleState.Paused);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Paused, LifecycleState.Queued);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Running, LifecycleState.Cancelling);
        LifecyclePolicy.EnsureAllowed(LifecycleState.Cancelling, LifecycleState.Cancelled);

        foreach (LifecycleState terminal in Enum.GetValues<LifecycleState>().Where(LifecyclePolicy.IsTerminal))
        {
            Assert.ThrowsExactly<InvalidOperationException>(
                () => LifecyclePolicy.EnsureAllowed(terminal, LifecycleState.Running));
        }
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void DurableCommandsAreIdempotentAndRunBindingsRemainImmutable()
    {
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
            "coordinator-a",
            DateTimeOffset.UtcNow,
            TimeSpan.FromMinutes(5));
        RunBinding binding = Binding("a");
        RunRecord first = store.CreateRun(
            "command-a",
            "run-a",
            binding,
            authority.FencingEpoch,
            DateTimeOffset.UtcNow);
        RunRecord duplicate = store.CreateRun(
            "command-a",
            "run-other",
            binding,
            authority.FencingEpoch,
            DateTimeOffset.UtcNow);

        Assert.AreEqual(first.RunId, duplicate.RunId);
        Assert.AreEqual(binding, duplicate.Binding);
        Assert.ThrowsExactly<InvalidOperationException>(() => store.CreateRun(
            "command-a",
            "run-other",
            Binding("other"),
            authority.FencingEpoch,
            DateTimeOffset.UtcNow));
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
    [TestCategory("M1Unit")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Fault")]
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
            1024,
            DateTimeOffset.UtcNow));
        Assert.IsEmpty(Directory.EnumerateFiles(store.Paths.Payloads, "*", SearchOption.AllDirectories));

        AttemptRecord stale = attempt with { AttemptFencingToken = attempt.AttemptFencingToken + 1 };
        string actual = Convert.ToHexString(SHA256.HashData("{}"u8.ToArray())).ToLowerInvariant();
        Assert.ThrowsExactly<InvalidOperationException>(() => store.AdmitStagedPayload(
            stale,
            "result.json",
            actual,
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
        store.SettleLiveAttempts(queued.RunId, "cancelled-at-safe-boundary");
        Assert.ThrowsExactly<InvalidOperationException>(() => store.AdmitStagedPayload(
            attempt,
            "result.json",
            actual,
            1024,
            DateTimeOffset.UtcNow));
        Assert.IsEmpty(Directory.EnumerateFiles(store.Paths.Payloads, "*", SearchOption.AllDirectories));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Fault")]
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

    private static RunBinding Binding(string suffix) =>
        new($"snapshot-{suffix}", $"context-{suffix}", $"config-{suffix}", $"manifest-{suffix}");

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
