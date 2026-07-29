using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class PlatformSubstrateEvaluationTests
{
    private static readonly string[] PlatformFamilies =
    [
        "M1-PLAT-LIFECYCLE-v1",
        "M1-PLAT-LINEAGE-v1",
        "M1-PLAT-WRITES-v1",
        "M1-PLAT-PERSIST-v1",
        "M1-PLAT-IPC-v1",
    ];

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void SliceTwoFixturePackageDeclaresEveryExercisedPlatformFamilyWithoutAnswers()
    {
        using JsonDocument document = TestRepository.ReadJson(
            "test-data",
            "evaluation",
            "m1-platform",
            "slice2-substrate-fixture.v1.json");
        JsonElement root = document.RootElement;
        Assert.AreEqual(
            "M1-PLAT-SLICE2-SUBSTRATE-v1",
            root.GetProperty("fixturePackageId").GetString());
        JsonElement isolation = root.GetProperty("answerIsolation");
        Assert.IsFalse(isolation.GetProperty("groundTruthEmbeddedInProduction").GetBoolean());
        Assert.IsFalse(isolation.GetProperty("realModNamesPresent").GetBoolean());
        Assert.IsFalse(isolation.GetProperty("fixtureSpecificProductionRulesAllowed").GetBoolean());
        string[] families = root.GetProperty("families")
            .EnumerateArray()
            .Select(family => family.GetProperty("id").GetString()!)
            .ToArray();
        CollectionAssert.AreEquivalent(
            PlatformFamilies,
            families);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    public void PersistFixtureBackupRestoreProjectionAndPayloadRoundTrip()
    {
        string sourceRoot = Temp("persist-source");
        string restoreRoot = Temp("persist-restore");
        try
        {
            BackupArtifact backup;
            RunRecord completed;
            using (AuthoritativeStore store = new(new StoragePaths(sourceRoot)))
            {
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
                RunRecord running = store.Transition(
                    "running-a",
                    queued.RunId,
                    queued.Generation,
                    LifecycleState.Running,
                    authority.FencingEpoch,
                    "fixture dispatch",
                    DateTimeOffset.UtcNow);
                AttemptRecord attempt = store.CreateAttempt(
                    queued.RunId,
                    authority.FencingEpoch,
                    TimeSpan.FromMinutes(2),
                    DateTimeOffset.UtcNow);
                string directory = Path.Combine(store.Paths.Staging, attempt.AttemptId);
                Directory.CreateDirectory(directory);
                byte[] bytes = "generic synthetic staged output"u8.ToArray();
                File.WriteAllBytes(Path.Combine(directory, "result.bin"), bytes);
                string sha = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                PayloadAdmission admission = store.AdmitStagedPayload(
                    attempt,
                    "result.bin",
                    sha,
                    4096,
                    DateTimeOffset.UtcNow);
                Assert.AreEqual(sha, admission.Sha256);
                completed = store.Transition(
                    "completed-a",
                    queued.RunId,
                    running.Generation,
                    LifecycleState.Completed,
                    authority.FencingEpoch,
                    "fixture publication complete",
                    DateTimeOffset.UtcNow);
                store.RebuildProjections(DateTimeOffset.UtcNow);
                backup = store.CreateBackup("fixture", DateTimeOffset.UtcNow);
            }

            AuthoritativeStore.RestoreBackup(backup, new StoragePaths(restoreRoot));
            using AuthoritativeStore restored = new(new StoragePaths(restoreRoot));
            RunRecord restoredRun = restored.GetRun(completed.RunId);
            Assert.AreEqual(completed.State, restoredRun.State);
            Assert.AreEqual(completed.Binding, restoredRun.Binding);
            Assert.IsEmpty(restored.ReconcilePayloadStore());
        }
        finally
        {
            Delete(sourceRoot);
            Delete(restoreRoot);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void LineageFixtureSchemaRetainsTypedAppendOnlySubstrate()
    {
        string root = Temp("lineage");
        try
        {
            using AuthoritativeStore store = new(new StoragePaths(root));
            string[] names = store.GetTableNames().ToArray();
            string[] required =
            [
                "logical_findings",
                "finding_occurrences",
                "logical_cases",
                "case_occurrences",
                "reconciliation_assessments",
                "lineage_events",
                "audit_events",
            ];
            foreach (string table in required)
            {
                CollectionAssert.Contains(names, table);
            }
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    public void WritesFixtureRejectsTraversalRelativeAndProtectedRoots()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new StoragePaths("relative-root"));
        Assert.ThrowsExactly<InvalidOperationException>(
            () => new StoragePaths(@"C:\Games\Skyrim Special Edition"));

        string root = Temp("writes");
        try
        {
            StoragePaths paths = new(root);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductRelative(@"payloads\..\..\outside.bin"));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductRelative(@"\\?\C:\outside.bin"));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductRelative(@"payloads\file.bin:stream"));
        }
        finally
        {
            Delete(root);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    public void PersistFixtureRejectsNewerSchemaAndTamperedBackup()
    {
        string newerRoot = Temp("newer-schema");
        string backupRoot = Temp("tampered-backup");
        try
        {
            StoragePaths newerPaths = new(newerRoot);
            newerPaths.Create();
            SqliteRuntimeIdentity.InitializeNativeProvider();
            using (SqliteConnection connection = new($"Data Source={newerPaths.Database};Pooling=False"))
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "PRAGMA user_version = 2;";
                command.ExecuteNonQuery();
            }

            Assert.ThrowsExactly<InvalidOperationException>(
                () => new AuthoritativeStore(newerPaths));

            BackupArtifact backup;
            using (AuthoritativeStore store = new(new StoragePaths(backupRoot)))
            {
                backup = store.CreateBackup("tamper", DateTimeOffset.UtcNow);
            }

            using (FileStream stream = new(
                backup.DatabasePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.None))
            {
                stream.WriteByte(0x7f);
            }

            Assert.ThrowsExactly<InvalidOperationException>(
                () => AuthoritativeStore.RestoreBackup(
                    backup,
                    new StoragePaths(Temp("tampered-target"))));
        }
        finally
        {
            Delete(newerRoot);
            Delete(backupRoot);
        }
    }

    private static RunBinding Binding(string suffix) =>
        new($"snapshot-{suffix}", $"context-{suffix}", $"config-{suffix}", $"manifest-{suffix}");

    private static string Temp(string kind) =>
        Path.Combine(Path.GetTempPath(), $"infinium-{kind}-{Guid.NewGuid():N}");

    private static void Delete(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
