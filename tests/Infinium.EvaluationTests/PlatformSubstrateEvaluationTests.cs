using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Infinium.EvaluatorV2.LegacyV1;
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
    public void SliceTwoFixturePackageLoadsThroughAcceptedContractWithExplicitCoverageGap()
    {
        EvaluationHarnessFixturePackage package = FixturePackageReader.ReadForEvaluationHarness(
            SliceTwoFixtureDirectory);

        Assert.AreEqual("M1-PLAT-SLICE2-SUBSTRATE-v1", package.FixtureId.Value);
        Assert.AreEqual(FixturePartition.Development, package.Partition);
        string[] families = package.ExecutionInput
            .GetProperty("declared_supported_capabilities")
            .EnumerateArray()
            .Select(family => family.GetString()!)
            .ToArray();
        CollectionAssert.AreEquivalent(PlatformFamilies, families);

        string[] expectedFamilies = package.Oracle
            .GetProperty("expected_deterministic_results")
            .EnumerateArray()
            .Select(expected => expected.GetProperty("subject_id").GetString()!)
            .ToArray();
        CollectionAssert.AreEquivalent(PlatformFamilies, expectedFamilies);
        foreach (JsonElement expected in package.Oracle
                     .GetProperty("expected_deterministic_results")
                     .EnumerateArray())
        {
            string subject = expected.GetProperty("subject_id").GetString()!;
            Assert.AreEqual(
                Fingerprint($"{subject}:plan-declared-slice2-substrate-present"),
                expected.GetProperty("canonical_value_fingerprint").GetString(),
                subject);
        }

        JsonElement planEvidence = package.Oracle
            .GetProperty("ground_truth_methods")[0]
            .GetProperty("evidence_references")[0];
        Assert.AreEqual(
            FileFingerprint(Path.Combine(
                TestRepository.Root,
                "docs",
                "plans",
                "milestones",
                "M1-backend-semantic-proof.md")),
            planEvidence.GetProperty("fingerprint").GetString());
        JsonElement gap = package.Oracle
            .GetProperty("expected_coverage_and_gaps")
            .EnumerateArray()
            .Single();
        Assert.AreEqual("complete-M1-evaluation-case", gap.GetProperty("subject_id").GetString());
        Assert.AreEqual("coverage-gap", gap.GetProperty("expected_type").GetString());
        Assert.AreEqual(
            Fingerprint("complete-M1-evaluation-case:coverage-gap-present"),
            gap.GetProperty("canonical_value_fingerprint").GetString());
        Assert.IsTrue(
            package.Oracle.GetProperty("forbidden_claims")
                .EnumerateArray()
                .Any(claim =>
                    claim.GetProperty("claim_type").GetString()
                    == "complete-evaluation-case-passed"));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void SliceTwoFixturePackageRejectsAnswerBearingExecutionMutationAfterFingerprintRefresh()
    {
        string directory = CopySliceTwoFixture();
        try
        {
            string executionPath = Path.Combine(directory, FixturePackageReader.ExecutionInputFileName);
            JsonObject execution = JsonNode.Parse(File.ReadAllText(executionPath))!.AsObject();
            execution["expected_labels"] = new JsonArray("answer");
            File.WriteAllText(
                executionPath,
                execution.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            RefreshManifestFingerprint(directory, "input_package_fingerprint", executionPath);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(directory));
        }
        finally
        {
            Delete(directory);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void SliceTwoFixturePackageRejectsOracleTampering()
    {
        string directory = CopySliceTwoFixture();
        try
        {
            File.AppendAllText(
                Path.Combine(directory, FixturePackageReader.OracleFileName),
                Environment.NewLine);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(directory));
        }
        finally
        {
            Delete(directory);
        }
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
                    bytes.LongLength,
                    new string('1', 64),
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

            using (StoragePaths restorePaths = new(restoreRoot))
            {
                AuthoritativeStore.RestoreBackup(backup, restorePaths);
            }

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
        Assert.ThrowsExactly<InvalidOperationException>(
            () => new StoragePaths(
                @"C:\Games\Skyrim Special Edition\mods\SomeMod\InfiniumData"));

        string root = Temp("writes");
        try
        {
            using StoragePaths paths = new(root);
            using StoragePaths aliasPaths =
                new(root.ToUpperInvariant() + Path.DirectorySeparatorChar);
            Assert.AreEqual(
                paths.AuthorityIdentity,
                aliasPaths.AuthorityIdentity);
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductPath(
                    ProductWriteClass.Payload,
                    @"..\..\outside.bin"));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductPath(
                    ProductWriteClass.Payload,
                    @"\\?\C:\outside.bin"));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => paths.ResolveProductPath(
                    ProductWriteClass.Payload,
                    @"file.bin:stream"));
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
            using StoragePaths newerPaths = new(newerRoot);
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

            using StoragePaths tamperedTarget = new(Temp("tampered-target"));
            Assert.ThrowsExactly<InvalidOperationException>(
                () => AuthoritativeStore.RestoreBackup(backup, tamperedTarget));
        }
        finally
        {
            Delete(newerRoot);
            Delete(backupRoot);
        }
    }

    private static RunBinding Binding(string suffix) =>
        new($"snapshot-{suffix}", $"context-{suffix}", $"config-{suffix}", $"manifest-{suffix}");

    private static string SliceTwoFixtureDirectory =>
        Path.Combine(
            TestRepository.Root,
            "test-data",
            "evaluation",
            "m1-platform",
            "M1-PLAT-SLICE2-SUBSTRATE-v1");

    private static string CopySliceTwoFixture()
    {
        string target = Temp("slice2-fixture");
        Directory.CreateDirectory(target);
        foreach (string sourcePath in Directory.EnumerateFiles(SliceTwoFixtureDirectory))
        {
            File.Copy(sourcePath, Path.Combine(target, Path.GetFileName(sourcePath)));
        }

        return target;
    }

    private static void RefreshManifestFingerprint(
        string directory,
        string propertyName,
        string targetPath)
    {
        string manifestPath = Path.Combine(directory, FixturePackageReader.PublicManifestFileName);
        JsonObject manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest[propertyName] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(targetPath)))
            .ToLowerInvariant();
        File.WriteAllText(
            manifestPath,
            manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Fingerprint(string value) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static string FileFingerprint(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

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
