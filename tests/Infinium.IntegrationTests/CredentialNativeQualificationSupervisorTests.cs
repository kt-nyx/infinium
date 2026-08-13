using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.CredentialHelper;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialNativeQualificationSupervisorTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedScenarioIds =
    [
        "interactive-entry-submit",
        "interactive-entry-cancel",
        "credential-size-boundaries",
        "secure-store-unavailable",
        "replacement",
        "revoke-delete",
        "helper-and-coordinator-crash-restart",
        "backup-restore-reauthentication",
        "fake-provider-dispatch",
    ];
    private static readonly string[] ExpectedManualAssignmentIds =
    [
        "wp4-v2/interactive-entry-submit/submit",
        "wp4-v2/interactive-entry-cancel/cancel",
        "wp4-v2/backup-restore-reauthentication/restored-new-generation",
    ];
    private static readonly string[] ExpectedRetainedCanarySurfaceNames =
    [
        "final credential-native evidence JSON",
        "final human summary",
        "CredentialNative gate stdout",
        "CredentialNative gate stderr",
    ];

    [TestMethod]
    [TestCategory("Integration")]
    public async Task SupervisorCapturesRealCoordinatorLifecycleStagingAndContainmentEvidence()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Supervisor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, BaseTime);
        _ = store.BeginCredentialEnrollment(
            "profile-supervised",
            "generation-supervised",
            "Supervised qualification proof",
            BaseTime.AddSeconds(1),
            "account-1",
            "billing-1");
        CredentialHelperCoordinator coordinator = new(store, Launcher(Path.Combine(root, "fake-store")));
        using CredentialNativeQualificationSupervisor supervisor = new(
            coordinator,
            expectedInheritedPrivateHandleCount: 3);

        CredentialNativeQualificationPhaseEvidence enrolled =
            await supervisor.ExecuteCredentialTransitionPhaseAsync(
                "revoke-delete",
                "enroll",
                "supervised-enroll-attempt",
                CredentialBootstrap("profile-supervised", "generation-supervised", 111),
                CredentialAssignment(
                    "profile-supervised",
                    "generation-supervised",
                    HelperAssignmentKindV2.Enroll,
                    "supervised-enroll"),
                BaseTime.AddSeconds(2));
        CredentialNativeQualificationPhaseEvidence deleted =
            await supervisor.ExecuteCredentialTransitionPhaseAsync(
                "revoke-delete",
                "precommit-revocation-delete-confirmed",
                "supervised-delete-attempt",
                CredentialBootstrap("profile-supervised", "generation-supervised", 112),
                CredentialAssignment(
                    "profile-supervised",
                    "generation-supervised",
                    HelperAssignmentKindV2.Delete,
                    "supervised-delete"),
                BaseTime.AddSeconds(4));

        Assert.AreEqual(HelperOutcomeV2.Completed, enrolled.Outcome);
        Assert.AreEqual("active-unverified", enrolled.Lifecycle?.LifecycleState);
        Assert.IsTrue(enrolled.Staging.StagedBeforeAdmission);
        Assert.IsTrue(enrolled.Staging.CoordinatorOnlyAdmission);
        Assert.AreEqual(3, enrolled.Process.InheritedPrivateHandleCount);
        Assert.AreEqual(0, enrolled.Process.NetworkOperationCount);
        Assert.AreEqual(0, enrolled.Process.ProcessTreeSurvivorCount);
        Assert.IsTrue(enrolled.Process.ProcessTreeTerminated);
        Assert.AreEqual("deleted", deleted.Lifecycle?.LifecycleState);
        Assert.AreEqual("confirmed", deleted.Lifecycle?.CleanupDisposition);
        Assert.AreEqual(1L, deleted.Lifecycle?.RevocationEpoch);
        Assert.IsTrue(deleted.Lifecycle?.ProjectionVersion > enrolled.Lifecycle?.ProjectionVersion);
    }

    [TestMethod]
    public async Task CleanupAmbiguityIsTerminalAndBlocksEveryLaterCoordinatorPhase()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Ambiguity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        CredentialHelperCoordinator coordinator = new(store, Launcher(Path.Combine(root, "fake-store")));
        using CredentialNativeQualificationSupervisor supervisor = new(
            coordinator,
            expectedInheritedPrivateHandleCount: 3);

        supervisor.RecordCleanupAmbiguity("replacement", "predecessor-cleanup-absence-uncertain");
        CredentialNativeQualificationEvidence terminal = supervisor.CaptureTerminalFailure();

        Assert.IsTrue(terminal.CleanupAmbiguous);
        Assert.IsTrue(terminal.NamespaceBlocked);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            supervisor.ExecuteCredentialTransitionPhaseAsync(
                "revoke-delete",
                "must-not-run",
                "must-not-run-attempt",
                HelperTestFrames.Bootstrap(nonceSeed: 113),
                HelperTestFrames.Assignment(),
                BaseTime));
        Assert.ThrowsExactly<InvalidOperationException>(() => supervisor.CompleteSuccessfulRun());
    }

    [TestMethod]
    public void NativePrimaryCleanupAmbiguitySignalTerminatesBeforeAnyLaterPhase()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-NativeAmbiguity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        using CredentialNativeQualificationSupervisor supervisor = new(
            new CredentialHelperCoordinator(store, Launcher(Path.Combine(root, "fake-store"))),
            expectedInheritedPrivateHandleCount: 2);
        HelperProcessReceipt process = new(
            42, 0, new string('a', 64), new HelperReceiptV2(), [], 2, 0, 0, 0, 0, 0, true, false,
            NativeNamespaceReuseBlocked: true,
            NativeNamespaceReuseBlockReason: "cleanup-outcome-ambiguous-or-failed");

        Assert.ThrowsExactly<CredentialNativeCleanupAmbiguityException>(() =>
            supervisor.RejectNativeNamespaceBlockForTest(
                process,
                "wp4-v2/replacement/replacement-recovered"));
        CredentialNativeQualificationEvidence terminal = supervisor.CaptureTerminalFailure();
        Assert.IsTrue(terminal.CleanupAmbiguous);
        Assert.IsTrue(terminal.NamespaceBlocked);
    }

    [TestMethod]
    public void SupervisorUsesExactFiniteDeadlinePartitionAndRequiresAllManifestScenarios()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(1650), CredentialNativeQualificationSupervisor.PrimaryPhaseTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(120), CredentialNativeQualificationSupervisor.CleanupReserve);
        Assert.AreEqual(TimeSpan.FromSeconds(30), CredentialNativeQualificationSupervisor.EvidenceReserve);
        Assert.AreEqual(
            CredentialNativeQualificationSupervisor.OuterWallClockTimeout,
            CredentialNativeQualificationSupervisor.PrimaryPhaseTimeout
                + CredentialNativeQualificationSupervisor.CleanupReserve
                + CredentialNativeQualificationSupervisor.EvidenceReserve);
        CollectionAssert.AreEquivalent(
            ExpectedScenarioIds,
            CredentialNativeQualificationSupervisor.RequiredScenarioIds.ToArray());
    }

    [TestMethod]
    public void ManualEntryEvidenceIsDerivedOnlyFromTheExactClosedPhaseContract()
    {
        CredentialNativeQualificationPhaseV2[] manual = CredentialNativeQualificationPhasesV2.Definitions
            .Where(item => item.SecretMode == CredentialNativeQualificationSecretModeV2.Manual)
            .ToArray();
        CollectionAssert.AreEqual(
            ExpectedManualAssignmentIds,
            manual.Select(item => item.AssignmentId).ToArray());
        foreach (CredentialNativeQualificationPhaseV2 phase in CredentialNativeQualificationPhasesV2.Definitions)
        {
            Assert.AreEqual(
                phase.SecretMode == CredentialNativeQualificationSecretModeV2.Manual,
                CredentialNativeQualificationSupervisor.RequiresManualEntryEvidence(
                    phase.AssignmentId, phase.AssignmentKind));
        }
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CredentialNativeQualificationSupervisor.RequiresManualEntryEvidence(
                "wp4-v2/not-authorized/interactive-cancel-restored-new-generation",
                HelperAssignmentKindV2.Enroll));
    }

    [TestMethod]
    public void RetainedCanaryInventoryCoversFinalOutputsAndGateStreamsWithoutWeakening()
    {
        CredentialNativeRetainedSurfaceEvidence[] inventory =
            CredentialNativeQualificationRunner.RetainedSurfaceInventory(123).ToArray();
        CollectionAssert.AreEqual(
            ExpectedRetainedCanarySurfaceNames,
            inventory.Select(item => item.Name).ToArray());
        Assert.IsTrue(inventory.All(item => item.SecretCanaryProof == "structurally-absent"
            && !string.IsNullOrWhiteSpace(item.Basis)));
        Assert.AreEqual("byte-scanned-utf8-and-utf16le", inventory[1].RawTargetCanaryProof);
        Assert.AreEqual(123, inventory[1].ByteCount);
        Assert.IsTrue(inventory.Where((_, index) => index != 1)
            .All(item => item.RawTargetCanaryProof == "structurally-absent" && item.ByteCount == 0));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            CredentialNativeQualificationRunner.ValidateRetainedSurfaceInventory(inventory[..^1]));
        CredentialNativeRetainedSurfaceEvidence[] weakened = [.. inventory];
        weakened[0] = weakened[0] with { RawTargetCanaryProof = "not-checked" };
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CredentialNativeQualificationRunner.ValidateRetainedSurfaceInventory(weakened));

        const string rawTarget = "Infinium:wp4-profile:g001";
        Assert.AreEqual(0, CredentialNativeQualificationRunner.CountRawTargetMatches(
            "ordinary retained output"u8, [rawTarget]));
        Assert.AreEqual(1, CredentialNativeQualificationRunner.CountRawTargetMatches(
            [.. "prefix"u8.ToArray(), .. Encoding.UTF8.GetBytes(rawTarget)], [rawTarget]));
        Assert.AreEqual(1, CredentialNativeQualificationRunner.CountRawTargetMatches(
            [.. "prefix"u8.ToArray(), .. Encoding.Unicode.GetBytes(rawTarget)], [rawTarget]));
    }

    [TestMethod]
    public void CanonicalPerProcessTraceRetainsCallsAndRejectsCountOrFreePairingDrift()
    {
        string fingerprint = new('a', 64);
        CredentialNativeCallTraceEntry[] trace =
        [
            new(1, "CredWriteW", fingerprint, "replacement", "success", null, null),
            new(2, "CredReadW", fingerprint, "replacement", "success", 17, null),
            new(3, "CredFree", fingerprint, "replacement", "released", null, 17),
            new(4, "CredDeleteW", fingerprint, "replacement", "success", null, null),
            new(5, "CredReadW", fingerprint, "replacement", "ERROR_NOT_FOUND", null, null),
        ];
        byte[] canonical = JsonSerializer.SerializeToUtf8Bytes(trace);

        IReadOnlyList<CredentialNativeCallTraceEntry> retained =
            CredentialNativeQualificationSupervisor.ParseAndValidateTrace(
                canonical,
                reportedOperationCount: trace.Length,
                requireTrace: true);

        Assert.AreEqual(trace.Length, retained.Count);
        Assert.AreEqual("CredFree", retained[2].Operation);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CredentialNativeQualificationSupervisor.ParseAndValidateTrace(
                canonical,
                reportedOperationCount: trace.Length - 1,
                requireTrace: true));
        byte[] missingFree = JsonSerializer.SerializeToUtf8Bytes(trace.Where(item => item.Operation != "CredFree"));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CredentialNativeQualificationSupervisor.ParseAndValidateTrace(
                missingFree,
                reportedOperationCount: trace.Length - 1,
                requireTrace: true));
    }

    [TestMethod]
    public void LauncherTimeoutIsThirtySecondsOrdinarilyAnd1650SecondsOnlyForExactNativeManifest()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Timeout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        OneShotCredentialHelperLauncher ordinary = Launcher(Path.Combine(root, "fake-store"));
        Assert.AreEqual(TimeSpan.FromSeconds(30), ordinary.OperationTimeout);

        const string manifestId = "infinium.m1-s6.wp4.credential-native-authorization/test-only";
        string manifestPath = Path.Combine(root, "manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new { manifest_id = manifestId }));
        string manifestSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(manifestPath)));
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher native = OneShotCredentialHelperLauncher.CreateNativeQualification(
            helper,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helper))),
            manifestPath,
            manifestSha256,
            manifestId);

        Assert.AreEqual(TimeSpan.FromSeconds(1650), native.OperationTimeout);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public async Task NativeManifestParserFailureReturnsKnownZeroEnvelopeWithoutThirtySecondPipeHold()
    {
        string repository = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Native-Failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string source = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-authorization.v2.json");
        JsonNode manifest = JsonNode.Parse(File.ReadAllBytes(source))
            ?? throw new InvalidDataException("The test manifest is malformed.");
        manifest["schema_identity"] = "infinium.repository.wp4-credential-native-authorization/1.1.0";
        string path = Path.Combine(root, "manifest.json");
        File.WriteAllText(path, manifest.ToJsonString());
        string id = manifest["manifest_id"]!.GetValue<string>();
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = OneShotCredentialHelperLauncher.CreateNativeQualification(
            helper, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helper))), path, sha256, id);
        HelperPrivateFrameV2 bootstrap = HelperTestFrames.Bootstrap(nonceSeed: 91);
        HelperPrivateFrameV2 assignment = HelperTestFrames.Assignment();

        Stopwatch elapsed = Stopwatch.StartNew();
        CredentialNativeHelperFailureException failure = await Assert.ThrowsExactlyAsync<CredentialNativeHelperFailureException>(
            () => launcher.ExecuteAsync(bootstrap, assignment, null, TimeSpan.FromSeconds(5), BaseTime));
        elapsed.Stop();

        Assert.IsLessThan(TimeSpan.FromSeconds(5), elapsed.Elapsed);
        Assert.AreEqual("manifest-validation", failure.Evidence.Stage);
        Assert.AreEqual("manifest-rejected", failure.Evidence.Reason);
        Assert.IsTrue(failure.Evidence.CallCountsKnown);
        Assert.AreEqual(0, failure.Evidence.Total);
        Assert.AreEqual("[]", failure.Evidence.NativeCallTraceJson);
        Assert.IsNull(failure.Evidence.EntryCleanupJson);
        Assert.IsNull(failure.Evidence.CanaryEvidenceJson);
        Assert.IsFalse(failure.Evidence.NamespaceReuseBlocked);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public async Task RunnerWritesConsumedBlockedArtifactForPreStoreKnownZeroWithoutClaimingAbsence()
    {
        string repository = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Native-Runner-Failure-" + Guid.NewGuid().ToString("N"));
        string output = Path.Combine(root, "output");
        Directory.CreateDirectory(output);
        string source = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-authorization.v2.json");
        JsonNode manifest = JsonNode.Parse(File.ReadAllBytes(source))
            ?? throw new InvalidDataException("The test manifest is malformed.");
        manifest["schema_identity"] = "infinium.repository.wp4-credential-native-authorization/1.1.0";
        string path = Path.Combine(root, "manifest.json");
        File.WriteAllText(path, manifest.ToJsonString());

        await Assert.ThrowsExactlyAsync<CredentialNativePrimaryFailureException>(() =>
            CredentialNativeQualificationRunner.RunAsync(path, output));

        string artifactPath = Path.Combine(output, "credential-native-primary-failure.v2.json");
        Assert.IsTrue(File.Exists(artifactPath));
        using JsonDocument artifact = JsonDocument.Parse(File.ReadAllBytes(artifactPath));
        Assert.AreEqual("failed-primary-effect-free-store-state-unobserved",
            artifact.RootElement.GetProperty("status").GetString());
        Assert.IsFalse(artifact.RootElement.GetProperty("cleanup_confirmed").GetBoolean());
        Assert.IsFalse(artifact.RootElement.GetProperty("absence_confirmed").GetBoolean());
        Assert.IsFalse(artifact.RootElement.GetProperty("whole_namespace_absence_confirmed").GetBoolean());
        Assert.AreEqual("none-store-state-unobserved",
            artifact.RootElement.GetProperty("absence_scope").GetString());
        Assert.AreEqual(0, artifact.RootElement.GetProperty("absence_target_fingerprints").GetArrayLength());
        Assert.IsTrue(artifact.RootElement.GetProperty("namespace_blocked").GetBoolean());
        Assert.AreEqual("consumed-never-reuse",
            artifact.RootElement.GetProperty("namespace_disposition").GetString());
        Assert.AreEqual("source-proven-pre-store-known-zero",
            artifact.RootElement.GetProperty("cleanup_disposition").GetString());
        Assert.IsTrue(artifact.RootElement.GetProperty("helper_failure_containment")
            .GetProperty("process_tree_terminated").GetBoolean());
        Assert.AreEqual(0, artifact.RootElement.GetProperty("helper_failure_containment")
            .GetProperty("process_tree_survivor_count").GetInt32());
    }

    [TestMethod]
    public void CleanupAmbiguityArtifactAcceptsAssignmentAndUnknownContextsWithoutClaimingCleanup()
    {
        foreach ((string context, string reason) in new[]
        {
            ("wp4-v2/interactive-entry-submit/cleanup", "cleanup-helper-failure"),
            ("wp4-v2/interactive-entry-submit/preflight", "helper-failure-evidence-invalid"),
            ("qualification-primary-failure", "primary-failure-cleanup-unproven"),
        })
        {
            CredentialNativeCleanupAmbiguityException ambiguity = new(context, reason);
            using JsonDocument artifact = JsonDocument.Parse(
                CredentialNativeQualificationRunner.SerializeCleanupAmbiguityArtifactForTest(
                    "manifest/test", new string('a', 64), ambiguity));
            Assert.AreEqual("failed-cleanup-ambiguous",
                artifact.RootElement.GetProperty("status").GetString(), context);
            Assert.AreEqual(context,
                artifact.RootElement.GetProperty("assignment_id").GetString(), context);
            Assert.IsFalse(artifact.RootElement.GetProperty("cleanup_confirmed").GetBoolean(), context);
            Assert.IsFalse(artifact.RootElement.GetProperty("whole_namespace_absence_confirmed").GetBoolean(), context);
            Assert.IsTrue(artifact.RootElement.GetProperty("namespace_blocked").GetBoolean(), context);
            Assert.AreEqual("consumed-never-reuse",
                artifact.RootElement.GetProperty("namespace_disposition").GetString(), context);
            Assert.AreEqual(0, artifact.RootElement.GetProperty("later_native_calls").GetInt32(), context);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task RunnerCleanupLoopEmitsTerminalBlockedArtifactForEveryFailureFamily()
    {
        NativeHelperFailureEnvelope boundedEvidence = new(
            "engine-execution", "invalid-operation", true,
            0, 0, 0, 0, 0, true, 0, 0, true, 0, 0, 0,
            "[]", null, null, false, false, 0, false, null);
        (string Name, Exception Failure, string Reason)[] cases =
        [
            ("generic", new InvalidOperationException("synthetic cleanup failure"), "cleanup-phase-failed"),
            ("helper", new CredentialNativeHelperFailureException(
                boundedEvidence, "wp4-v2/interactive-entry-submit/cleanup"), "cleanup-helper-failure"),
            ("ambiguous", new CredentialNativeHelperEvidenceAmbiguityException(
                "wp4-v2/interactive-entry-submit/cleanup",
                new InvalidDataException("synthetic malformed envelope")), "cleanup-helper-evidence-invalid"),
        ];

        foreach ((string name, Exception injected, string expectedReason) in cases)
        {
            string root = Path.Combine(
                Path.GetTempPath(), "Infinium-Wp4-Cleanup-Loop-" + name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            await Assert.ThrowsExactlyAsync<CredentialNativeCleanupAmbiguityException>(() =>
                CredentialNativeQualificationRunner.RunCleanupFailureWithArtifactsForTestAsync(
                    root,
                    Launcher(Path.Combine(root, "fake-store")),
                    AllTargetIdentities(),
                    BaseTime,
                    injected));

            string artifactPath = Path.Combine(root, "credential-native-cleanup-ambiguity.v2.json");
            Assert.IsTrue(File.Exists(artifactPath), name);
            using JsonDocument artifact = JsonDocument.Parse(File.ReadAllBytes(artifactPath));
            Assert.AreEqual("failed-cleanup-ambiguous",
                artifact.RootElement.GetProperty("status").GetString(), name);
            Assert.AreEqual("wp4-v2/interactive-entry-submit/cleanup",
                artifact.RootElement.GetProperty("assignment_id").GetString(), name);
            Assert.AreEqual(expectedReason,
                artifact.RootElement.GetProperty("reason").GetString(), name);
            Assert.IsFalse(artifact.RootElement.GetProperty("cleanup_confirmed").GetBoolean(), name);
            Assert.IsFalse(artifact.RootElement.GetProperty("whole_namespace_absence_confirmed").GetBoolean(), name);
            Assert.IsTrue(artifact.RootElement.GetProperty("namespace_blocked").GetBoolean(), name);
            Assert.AreEqual("consumed-never-reuse",
                artifact.RootElement.GetProperty("namespace_disposition").GetString(), name);
            Assert.AreEqual(0, artifact.RootElement.GetProperty("later_native_calls").GetInt32(), name);
            Assert.AreEqual(0, DeterministicFakeSecureStore.NativeOperationCount, name);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public async Task NativeHelperFailureEnvelopeRoundTripsKnownPostCallEvidenceWithoutRawExceptionText()
    {
        string fingerprint = new('a', 64);
        string trace = JsonSerializer.Serialize(new[]
        {
            new CredentialNativeCallTraceEntry(1, "CredWriteW", fingerprint, "synthetic-assignment", "success", null, null),
            new CredentialNativeCallTraceEntry(2, "CredReadW", fingerprint, "synthetic-assignment", "success", 1, null),
            new CredentialNativeCallTraceEntry(3, "CredFree", fingerprint, "synthetic-assignment", "released", null, 1),
            new CredentialNativeCallTraceEntry(4, "CredDeleteW", fingerprint, "synthetic-assignment", "success", null, null),
            new CredentialNativeCallTraceEntry(5, "CredReadW", fingerprint, "synthetic-assignment", "ERROR_NOT_FOUND", null, null),
        });
        string canaries = JsonSerializer.Serialize(new CredentialNativeCanaryEvidence(
            0, 0, ["utf-8", "utf-16le"],
            [
                new("private protocol request", "private-pipe-bytes", 0, 0, 0),
                new("private protocol partial response", "private-pipe-bytes", 0, 0, 0),
                new("native call trace", "canonical-trace-bytes", trace.Length, 0, 0),
                new("process command line", "captured-text", 0, 0, 0),
                new("process environment names", "captured-text", 0, 0, 0),
            ]));
        NativeHelperFailureEnvelope expected = new(
            "evidence-collection", "win32-failure", true,
            1, 2, 1, 1, 5,
            true, 0, 0, true, 0, 0, 0, trace, null, canaries,
            false, false, 0, false, null);
        using MemoryStream stream = new();
        await NativeHelperFailureProtocol.WriteAsync(stream, expected, CancellationToken.None);
        stream.Position = 0;
        byte[] magic = new byte[4];
        await stream.ReadExactlyAsync(magic);
        Assert.IsTrue(NativeHelperFailureProtocol.IsMagic(magic));
        NativeHelperFailureEnvelope actual = await NativeHelperFailureProtocol.ReadAfterMagicAsync(
            stream, CancellationToken.None);
        Assert.AreEqual(expected, actual);
        Assert.IsFalse(Encoding.UTF8.GetString(stream.ToArray()).Contains("synthetic exception", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public async Task NativeFailureProbeClearsRealPipeInheritanceAndRetainsValidatedNonzeroEvidence()
    {
        string repository = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifestPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-authorization.v2.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement target = manifest.RootElement.GetProperty("disposable_namespace").GetProperty("targets")[0];
        string profile = target.GetProperty("access_profile_id").GetString()!;
        string generation = target.GetProperty("generation_id").GetString()!;
        string fingerprint = target.GetProperty("target_fingerprint_sha256").GetString()!;
        const string assignmentId = "wp4-v2/interactive-entry-submit/preflight";
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        using AnonymousPipeServerStream request = new(
            PipeDirection.Out, HandleInheritability.Inheritable, 64 * 1024);
        using AnonymousPipeServerStream response = new(
            PipeDirection.In, HandleInheritability.Inheritable, 64 * 1024);
        ProcessStartInfo start = new(helper)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("--native-failure-envelope-test-probe");
        start.ArgumentList.Add("--request-handle");
        start.ArgumentList.Add(request.GetClientHandleAsString());
        start.ArgumentList.Add("--response-handle");
        start.ArgumentList.Add(response.GetClientHandleAsString());
        start.ArgumentList.Add("--assignment-id");
        start.ArgumentList.Add(assignmentId);
        start.ArgumentList.Add("--target-fingerprint");
        start.ArgumentList.Add(fingerprint);
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The failure-envelope probe did not start.");
        request.DisposeLocalCopyOfClientHandle();
        response.DisposeLocalCopyOfClientHandle();
        int descendantPid = 0;
        try
        {
            byte[] magic = new byte[4];
            await response.ReadExactlyAsync(magic);
            Assert.IsTrue(NativeHelperFailureProtocol.IsMagic(magic));
            NativeHelperFailureEnvelope envelope = await NativeHelperFailureProtocol.ReadAfterMagicAsync(
                response, CancellationToken.None);
            descendantPid = envelope.ContainmentDescendantProcessId;
            HelperPrivateFrameV2 bootstrapFrame = HelperTestFrames.Bootstrap(nonceSeed: 92);
            bootstrapFrame.Bootstrap.Credential = new()
            {
                AccessProfileId = new() { Value = profile },
                GenerationId = new() { Value = generation },
            };
            HelperPrivateFrameV2 assignmentFrame = HelperTestFrames.Assignment();
            assignmentFrame.Assignment.AssignmentId = assignmentId;
            assignmentFrame.Assignment.AssignmentKind = HelperAssignmentKindV2.Verify;
            assignmentFrame.Assignment.AccessProfileId = new() { Value = profile };
            assignmentFrame.Assignment.GenerationId = new() { Value = generation };
            CredentialNativeQualificationSupervisor.ValidateNativeHelperFailureEnvelope(
                envelope, bootstrapFrame.Bootstrap, assignmentFrame.Assignment, manifestPath, process.Id);
            Assert.AreEqual(1, envelope.CredReadW);
            Assert.AreEqual(1, envelope.Total);
            Assert.IsTrue(envelope.ContainmentDescendantStarted);

            using CancellationTokenSource exitDeadline = new(TimeSpan.FromSeconds(5));
            await process.WaitForExitAsync(exitDeadline.Token);
            Assert.AreEqual(71, process.ExitCode);
            byte[] eof = new byte[1];
            using CancellationTokenSource eofDeadline = new(TimeSpan.FromSeconds(2));
            Assert.AreEqual(0, await response.ReadAsync(eof, eofDeadline.Token));
        }
        finally
        {
            if (descendantPid > 0)
            {
                try
                {
                    using Process descendant = Process.GetProcessById(descendantPid);
                    if (!descendant.HasExited)
                    {
                        descendant.Kill(entireProcessTree: true);
                        await descendant.WaitForExitAsync();
                    }
                }
                catch (ArgumentException)
                {
                    // The bounded descendant already exited.
                }
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public void NativeFailureOracleOnlyAdmitsExactPreflightAbsenceAndClosedFailureSurfaces()
    {
        string repository = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifestPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-authorization.v2.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement target = manifest.RootElement.GetProperty("disposable_namespace").GetProperty("targets")[0];
        string profile = target.GetProperty("access_profile_id").GetString()!;
        string generation = target.GetProperty("generation_id").GetString()!;
        string fingerprint = target.GetProperty("target_fingerprint_sha256").GetString()!;
        const string assignmentId = "wp4-v2/interactive-entry-submit/preflight";
        HelperPrivateFrameV2 bootstrap = HelperTestFrames.Bootstrap(nonceSeed: 93);
        bootstrap.Bootstrap.Credential = new()
        {
            AccessProfileId = new() { Value = profile },
            GenerationId = new() { Value = generation },
        };
        HelperPrivateFrameV2 assignment = HelperTestFrames.Assignment();
        assignment.Assignment.AssignmentId = assignmentId;
        assignment.Assignment.AssignmentKind = HelperAssignmentKindV2.Verify;
        assignment.Assignment.AccessProfileId = new() { Value = profile };
        assignment.Assignment.GenerationId = new() { Value = generation };
        string trace = JsonSerializer.Serialize(new[]
        {
            new CredentialNativeCallTraceEntry(
                1, "CredReadW", fingerprint, assignmentId, "ERROR_NOT_FOUND", null, null),
        });
        CredentialNativeCanaryEvidence canaries = new(
            0, 0, ["utf-8", "utf-16le"],
            [
                new("private protocol request", "private-pipe-bytes", 100, 0, 0),
                new("private protocol partial response", "private-pipe-bytes", 0, 0, 0),
                new("native call trace", "canonical-trace-bytes", Encoding.UTF8.GetByteCount(trace), 0, 0),
                new("process command line", "captured-text", 100, 0, 0),
                new("process environment names", "captured-text", 100, 0, 0),
            ]);
        NativeHelperFailureEnvelope valid = new(
            "engine-execution", "invalid-operation", true,
            0, 1, 0, 0, 1, true, 0, 0, true, 0, 0, 0,
            trace, null, JsonSerializer.Serialize(canaries),
            false, true, 42, false, null);

        void Accept(NativeHelperFailureEnvelope value) =>
            CredentialNativeQualificationSupervisor.ValidateNativeHelperFailureEnvelope(
                value, bootstrap.Bootstrap, assignment.Assignment, manifestPath, Environment.ProcessId);
        void Reject(NativeHelperFailureEnvelope value) => Assert.ThrowsExactly<InvalidDataException>(() => Accept(value));

        Accept(valid);
        string writeTrace = JsonSerializer.Serialize(new[]
        {
            new CredentialNativeCallTraceEntry(
                1, "CredWriteW", fingerprint, assignmentId, "success", null, null),
        });
        Reject(valid with
        {
            CredWriteW = 1,
            CredReadW = 0,
            NativeCallTraceJson = writeTrace,
        });
        Reject(valid with { ListenerCount = 1 });
        Reject(valid with { NetworkOperationCount = 1 });
        Reject(valid with { DnsOperationCount = 1 });
        Reject(valid with { ProviderOperationCount = 1 });
        Reject(valid with { BillableOperationCount = 1 });
        Reject(valid with { ExternalEffectFactsKnown = false });
        Reject(valid with { NativeCallTraceJson = "[]", CredReadW = 0, Total = 0 });
        for (int index = 0; index < canaries.ScannedSurfaces.Count; index++)
        {
            CredentialNativeCanarySurfaceEvidence[] reduced = canaries.ScannedSurfaces
                .Where((_, itemIndex) => itemIndex != index)
                .ToArray();
            Reject(valid with
            {
                CanaryEvidenceJson = JsonSerializer.Serialize(canaries with { ScannedSurfaces = reduced }),
            });
        }
        CredentialNativeCanarySurfaceEvidence[] renamed = canaries.ScannedSurfaces.ToArray();
        renamed[0] = renamed[0] with { Kind = "unbound-surface" };
        Reject(valid with
        {
            CanaryEvidenceJson = JsonSerializer.Serialize(canaries with { ScannedSurfaces = renamed }),
        });
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public void NativeFailureOracleBindsFailedManualUiCleanupToExactHelperAndActionState()
    {
        const int helperProcessId = 49152;
        const string assignmentId = "wp4-v2/interactive-entry-submit/submit";
        string repository = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifestPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-authorization.v2.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement target = manifest.RootElement.GetProperty("disposable_namespace").GetProperty("targets")
            .EnumerateArray().Single(item => item.GetProperty("alias").GetString() == "interactive-primary");
        string profile = target.GetProperty("access_profile_id").GetString()!;
        string generation = target.GetProperty("generation_id").GetString()!;
        HelperPrivateFrameV2 bootstrap = CredentialBootstrap(profile, generation, 94);
        HelperPrivateFrameV2 assignment = CredentialAssignment(
            profile, generation, HelperAssignmentKindV2.Enroll, assignmentId);
        string emptyTrace = "[]";
        CredentialNativeCanaryEvidence canaries = new(
            0, 0, ["utf-8", "utf-16le"],
            [
                new("private protocol request", "private-pipe-bytes", 100, 0, 0),
                new("private protocol partial response", "private-pipe-bytes", 0, 0, 0),
                new("native call trace", "canonical-trace-bytes", 2, 0, 0),
                new("process command line", "captured-text", 100, 0, 0),
                new("process environment names", "captured-text", 100, 0, 0),
            ]);
        CredentialNativeEntryReadinessEvidence failedReadiness = FailedReadinessCleanup().Readiness! with
        {
            OwnerProcessId = helperProcessId,
        };
        CredentialNativeEntryCleanupEvidence failedCleanup = new(
            false, true, true, true, true, true,
            failedReadiness, null, null, null, 1, 2);
        NativeHelperFailureEnvelope valid = new(
            "engine-execution", "invalid-data", true,
            0, 0, 0, 0, 0, true, 0, 0, true, 0, 0, 0,
            emptyTrace, JsonSerializer.Serialize(failedCleanup), JsonSerializer.Serialize(canaries),
            true, true, 42, false, null);

        void Accept(NativeHelperFailureEnvelope value) =>
            CredentialNativeQualificationSupervisor.ValidateNativeHelperFailureEnvelope(
                value, bootstrap.Bootstrap, assignment.Assignment, manifestPath, helperProcessId);
        void Reject(NativeHelperFailureEnvelope value) => Assert.ThrowsExactly<InvalidDataException>(() => Accept(value));

        Accept(valid);
        Reject(valid with { EntryCleanupJson = JsonSerializer.Serialize(failedCleanup with { InitialBlank = true }) });
        Reject(valid with { EntryCleanupJson = JsonSerializer.Serialize(failedCleanup with { Terminal = false }) });
        Reject(valid with
        {
            EntryCleanupJson = JsonSerializer.Serialize(failedCleanup with
            {
                Readiness = failedReadiness with { OwnerProcessId = helperProcessId + 1 },
            }),
        });
        Reject(valid with
        {
            CredDeleteW = 1,
            Total = 1,
            NativeCallTraceJson = JsonSerializer.Serialize(new[]
            {
                new CredentialNativeCallTraceEntry(
                    1, "CredDeleteW",
                    target.GetProperty("target_fingerprint_sha256").GetString()!,
                    assignmentId, "success", null, null),
            }),
        });
        Reject(valid with
        {
            CredWriteW = 1,
            Total = 1,
            NativeCallTraceJson = JsonSerializer.Serialize(new[]
            {
                new CredentialNativeCallTraceEntry(
                    1, "CredWriteW",
                    target.GetProperty("target_fingerprint_sha256").GetString()!,
                    assignmentId, "success", null, null),
            }),
        });

        CredentialNativeEntryReadinessEvidence ready = failedReadiness with
        {
            Foreground = true,
            EditFocused = true,
            ReadinessElapsedMilliseconds = 10,
        };
        CredentialNativeEntryCleanupEvidence admittedAction = failedCleanup with
        {
            InitialBlank = true,
            Readiness = ready,
            ActionReadiness = ready,
            Action = "submit",
            ActionSource = "editkey",
        };
        Accept(valid with { EntryCleanupJson = JsonSerializer.Serialize(admittedAction) });
        Reject(valid with
        {
            EntryCleanupJson = JsonSerializer.Serialize(admittedAction with
            {
                Action = "cancel",
                ActionSource = "submitbutton",
                ActionReadiness = ready with { EditFocused = false, SubmitFocused = true },
            }),
        });
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public async Task NativeHelperFailureProtocolRejectsUnknownOversizedAndTruncatedEvidence()
    {
        NativeHelperFailureEnvelope knownZero = new(
            "manifest-validation", "manifest-rejected", true,
            0, 0, 0, 0, 0, true, 0, 0, true, 0, 0, 0, "[]", null, null,
            false, false, 0, false, null);
        using MemoryStream valid = new();
        await NativeHelperFailureProtocol.WriteAsync(valid, knownZero, CancellationToken.None);
        byte[] truncatedBytes = valid.ToArray()[..^1];
        using MemoryStream truncated = new(truncatedBytes);
        byte[] magic = new byte[4];
        await truncated.ReadExactlyAsync(magic);
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() =>
            NativeHelperFailureProtocol.ReadAfterMagicAsync(truncated, CancellationToken.None));

        NativeHelperFailureEnvelope unknownWithTrace = knownZero with
        {
            Stage = "handle-inheritance",
            Reason = "handle-inheritance-failure",
            CallCountsKnown = false,
        };
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            NativeHelperFailureProtocol.WriteAsync(new MemoryStream(), unknownWithTrace, CancellationToken.None));

        NativeHelperFailureEnvelope oversized = knownZero with
        {
            NativeCallTraceJson = "[\"" + new string('x', NativeHelperFailureProtocol.MaximumBytes) + "\"]",
        };
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            NativeHelperFailureProtocol.WriteAsync(new MemoryStream(), oversized, CancellationToken.None));

        byte[] invalidLength = new byte[8];
        "NHF2"u8.CopyTo(invalidLength);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            invalidLength.AsSpan(4), checked((uint)NativeHelperFailureProtocol.MaximumBytes + 1U));
        using MemoryStream overlongFrame = new(invalidLength);
        await overlongFrame.ReadExactlyAsync(magic);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            NativeHelperFailureProtocol.ReadAfterMagicAsync(overlongFrame, CancellationToken.None));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public async Task NativeHelperFailureProtocolRetainsManifestMaximumCanonicalEvidenceWithinFrameBound()
    {
        const string assignment = "wp4-v2/interactive-entry-submit/submit";
        string fingerprint = new('a', 64);
        List<CredentialNativeCallTraceEntry> trace = [];
        long sequence = 0;
        for (int index = 0; index < 9; index++)
        {
            trace.Add(new(++sequence, "CredWriteW", fingerprint, assignment, "success", null, null));
        }
        for (long allocation = 1; allocation <= 28; allocation++)
        {
            trace.Add(new(++sequence, "CredReadW", fingerprint, assignment, "success", allocation, null));
            trace.Add(new(++sequence, "CredFree", fingerprint, assignment, "released", null, allocation));
        }
        for (int index = 0; index < 50; index++)
        {
            trace.Add(new(++sequence, "CredReadW", fingerprint, assignment,
                "ERROR_NOT_FOUND", null, null));
        }
        for (int index = 0; index < 9; index++)
        {
            trace.Add(new(++sequence, "CredDeleteW", fingerprint, assignment, "success", null, null));
        }
        Assert.HasCount(124, trace);
        string traceJson = JsonSerializer.Serialize(trace);
        CredentialNativeEntryReadinessEvidence readiness = new(
            Environment.ProcessId, 1, Process.GetCurrentProcess().SessionId, new string('b', 64),
            true, true, true, true, true, true, true, true, true,
            true, true, "submit", Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                "Enter a disposable test value, then click Submit. Never use a real credential."))),
            true, true, true, true, true, true, true, false,
            true, true, true, false, true, true, 10_000, 9_999);
        string entryJson = JsonSerializer.Serialize(new CredentialNativeEntryCleanupEvidence(
            true, true, true, true, true, true, readiness, readiness,
            "editkey", "submit", 0, 0));
        string canaryJson = JsonSerializer.Serialize(new CredentialNativeCanaryEvidence(
            0, 0, ["utf-8", "utf-16le"],
            [
                new("private protocol request", "private-pipe-bytes", 49_152, 0, 0),
                new("private protocol partial response", "private-pipe-bytes", 49_152, 0, 0),
                new("native call trace", "canonical-trace-bytes", Encoding.UTF8.GetByteCount(traceJson), 0, 0),
                new("process command line", "captured-text", 4_096, 0, 0),
                new("process environment names", "captured-text", 16_384, 0, 0),
            ]));
        NativeHelperFailureEnvelope maximum = new(
            "evidence-collection", "controlled-failure", true,
            9, 78, 9, 28, 124, true, 0, 0, true, 0, 0, 0,
            traceJson, entryJson, canaryJson, true, true, 42, false, null);

        using MemoryStream frame = new();
        await NativeHelperFailureProtocol.WriteAsync(frame, maximum, CancellationToken.None);
        Assert.IsLessThanOrEqualTo(NativeHelperFailureProtocol.MaximumBytes + 8, frame.Length);
        frame.Position = 4;
        NativeHelperFailureEnvelope retained = await NativeHelperFailureProtocol.ReadAfterMagicAsync(
            frame, CancellationToken.None);
        Assert.AreEqual(maximum, retained);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task FullRunnerExercisesAllClosedPhasesThroughFakeContainedHelpersWithoutNativeEffects()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-FullRunner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Dictionary<string, (string ProfileId, string GenerationId)> targets = new(StringComparer.Ordinal)
        {
            ["interactive-primary"] = ("wp4-interactive-primary", "g001"),
            ["interactive-cancel"] = ("wp4-interactive-cancel", "g001"),
            ["size-valid"] = ("wp4-size-valid", "g001"),
            ["size-oversize"] = ("wp4-size-oversize", "g001"),
            ["unavailable-store"] = ("wp4-unavailable", "g001"),
            ["replacement-old"] = ("wp4-replacement", "g001"),
            ["replacement-new"] = ("wp4-replacement", "g002"),
            ["revoke-delete"] = ("wp4-revoke-delete", "g001"),
            ["crash-restart"] = ("wp4-crash-restart", "g001"),
            ["backup-old"] = ("wp4-backup", "g001"),
            ["backup-new"] = ("wp4-backup", "g002"),
            ["fake-dispatch"] = ("wp4-fake-dispatch", "g001"),
        };
        CredentialNativeQualificationEvidence evidence =
            await CredentialNativeQualificationRunner.RunWithLauncherForTestAsync(
                root,
                Launcher(Path.Combine(root, "fake-store")),
                targets,
                BaseTime);

        Assert.AreEqual(9, evidence.Scenarios.Count);
        Assert.AreEqual(0, evidence.NativeCallCounts.Total);
        Assert.IsFalse(evidence.CleanupAmbiguous);
        Assert.IsFalse(evidence.NamespaceBlocked);
        Assert.AreEqual(0, DeterministicFakeSecureStore.NativeOperationCount);
        CollectionAssert.AreEquivalent(
            CredentialNativeQualificationPhasesV2.Definitions.Select(item => item.AssignmentId).ToArray(),
            evidence.Scenarios.SelectMany(item => item.Phases).Select(item => item.AssignmentId).ToArray());
        Assert.IsNotNull(evidence.Scenarios.Single(item => item.ScenarioId == "fake-provider-dispatch")
            .Phases.Single(item => item.PhaseId == "final-gate-dispatch-stage-admit-settle").Dispatch);
        string canonical = CredentialNativeQualificationRunner.SerializeEvidenceForTest(evidence);
        StringAssert.Contains(canonical, "\"outcome\": \"Completed\"");
        StringAssert.Contains(canonical, "\"assignment_kind\": \"ProviderDispatch\"");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task PrimaryFailureRetainsTypedCauseAndSuccessfulCleanupEvidence()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-PrimaryFailure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Dictionary<string, (string ProfileId, string GenerationId)> targets = new(StringComparer.Ordinal)
        {
            ["interactive-primary"] = ("wp4-interactive-primary", "g001"),
            ["interactive-cancel"] = ("wp4-interactive-cancel", "g001"),
            ["size-valid"] = ("wp4-size-valid", "g001"),
            ["size-oversize"] = ("wp4-size-oversize", "g001"),
            ["unavailable-store"] = ("wp4-unavailable", "g001"),
            ["replacement-old"] = ("wp4-replacement", "g001"),
            ["replacement-new"] = ("wp4-replacement", "g002"),
            ["revoke-delete"] = ("wp4-revoke-delete", "g001"),
            ["crash-restart"] = ("wp4-crash-restart", "g001"),
            ["backup-old"] = ("wp4-backup", "g001"),
            ["backup-new"] = ("wp4-backup", "g002"),
            ["fake-dispatch"] = ("wp4-fake-dispatch", "g001"),
        };

        CredentialNativePrimaryFailureException failure =
            await Assert.ThrowsExactlyAsync<CredentialNativePrimaryFailureException>(() =>
                CredentialNativeQualificationRunner.RunWithLauncherForTestAsync(
                    root,
                    Launcher(Path.Combine(root, "fake-store")),
                    targets,
                    BaseTime,
                    new InvalidDataException("synthetic typed primary failure")));

        Assert.AreEqual(nameof(InvalidDataException), failure.FailureType);
        Assert.AreEqual("exact-target-cleanup-confirmed", failure.CleanupDisposition);
        Assert.IsFalse(failure.Evidence.CleanupAmbiguous);
        Assert.IsFalse(failure.Evidence.NamespaceBlocked);
        CredentialNativeQualificationPhaseEvidence cleanup = failure.Evidence.Scenarios
            .Single(item => item.ScenarioId == "interactive-entry-submit")
            .Phases.Single(item => item.PhaseId == "cleanup");
        Assert.AreEqual(HelperOutcomeV2.Completed, cleanup.Outcome);
        Assert.AreEqual(0, DeterministicFakeSecureStore.NativeOperationCount);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            CredentialNativeQualificationRunner.SerializePrimaryFailureArtifactForTest(
                "manifest/test", new string('a', 64), failure));
        CredentialNativePrimaryFailureException sourceProven = new(
            failure.FailureType,
            "source-proven-pre-store-known-zero",
            failure.Evidence,
            failure.InnerException!);
        using (JsonDocument artifact = JsonDocument.Parse(
            CredentialNativeQualificationRunner.SerializePrimaryFailureArtifactForTest(
                "manifest/test", new string('a', 64), sourceProven)))
        {
            Assert.AreEqual("failed-primary-effect-free-store-state-unobserved",
                artifact.RootElement.GetProperty("status").GetString());
            Assert.IsFalse(artifact.RootElement.GetProperty("cleanup_confirmed").GetBoolean());
            Assert.IsFalse(artifact.RootElement.GetProperty("absence_confirmed").GetBoolean());
            Assert.IsFalse(artifact.RootElement.GetProperty("whole_namespace_absence_confirmed").GetBoolean());
            Assert.IsTrue(artifact.RootElement.GetProperty("namespace_blocked").GetBoolean());
            Assert.AreEqual("none-store-state-unobserved",
                artifact.RootElement.GetProperty("absence_scope").GetString());
            Assert.AreEqual("source-proven-pre-store-known-zero",
                artifact.RootElement.GetProperty("cleanup_disposition").GetString());
            Assert.IsGreaterThan(0,
                artifact.RootElement.GetProperty("evidence").GetProperty("scenarios").GetArrayLength());
        }
        CredentialNativePrimaryFailureException unproven = new(
            failure.FailureType,
            "scenario-admission-is-not-cleanup-proof",
            failure.Evidence,
            failure.InnerException!);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CredentialNativeQualificationRunner.SerializePrimaryFailureArtifactForTest(
                "manifest/test", new string('a', 64), unproven));
    }

    [TestMethod]
    public void ManualFailureClassificationNeverMislabelsPostWriteOrCompletedEvidenceAsPrewrite()
    {
        Assert.AreEqual("failed-prewrite-ui-readiness",
            CredentialNativeQualificationSupervisor.ClassifyManualPhaseFailure(
                HelperOutcomeV2.FailedKnown, initialBlank: false,
                readinessPresent: true, credWriteCount: 0, nativeCallTotal: 0));
        foreach ((HelperOutcomeV2 outcome, bool? initialBlank, bool readiness, int writes, int total) in new[]
        {
            (HelperOutcomeV2.Completed, (bool?)false, true, 0, 0),
            (HelperOutcomeV2.FailedKnown, (bool?)true, true, 0, 0),
            (HelperOutcomeV2.FailedKnown, (bool?)false, false, 0, 0),
            (HelperOutcomeV2.FailedKnown, (bool?)false, true, 1, 1),
            (HelperOutcomeV2.FailedKnown, (bool?)false, true, 0, 1),
        })
        {
            Assert.AreEqual("failed-manual-phase-evidence-validation",
                CredentialNativeQualificationSupervisor.ClassifyManualPhaseFailure(
                    outcome, initialBlank, readiness, writes, total));
        }

        Assert.IsTrue(CredentialNativeQualificationSupervisor.IsActionSourceBindingValid("submit", "submitbutton"));
        Assert.IsTrue(CredentialNativeQualificationSupervisor.IsActionSourceBindingValid("cancel", "cancelbutton"));
        Assert.IsTrue(CredentialNativeQualificationSupervisor.IsActionSourceBindingValid("cancel", "editkey"));
        Assert.IsFalse(CredentialNativeQualificationSupervisor.IsActionSourceBindingValid("cancel", "submitbutton"));
        Assert.IsFalse(CredentialNativeQualificationSupervisor.IsActionSourceBindingValid("submit", "cancelbutton"));
    }

    [TestMethod]
    public void FailedManualReceiptRetainsLastBriefUiReadinessSnapshotBeforePhaseOracleRejects()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Retained-Ui-Failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        using CredentialNativeQualificationSupervisor supervisor = new(
            new CredentialHelperCoordinator(store, Launcher(Path.Combine(root, "fake-store"))),
            expectedInheritedPrivateHandleCount: 2);
        CredentialNativeEntryReadinessEvidence readiness = new(
            OwnerProcessId: 49152,
            OwnerThreadId: 37,
            OwnerSessionId: System.Diagnostics.Process.GetCurrentProcess().SessionId,
            DesktopNameSha256: new string('a', 64),
            InteractiveInputDesktop: true,
            DesktopObjectMatches: true,
            OwnerProcessMatches: true,
            OwnerThreadMatches: true,
            TopLevelWindow: true,
            WindowVisible: true,
            WindowEnabled: true,
            WindowNotCloaked: true,
            WindowIntersectsActiveMonitor: true,
            InstructionOwned: true,
            InstructionVisible: true,
            InstructionMode: "submit",
            InstructionFingerprintSha256: Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                "Enter a disposable test value, then click Submit. Never use a real credential."))),
            EditOwned: true,
            EditVisible: true,
            EditEnabled: true,
            EditMasked: true,
            SubmitOwned: true,
            SubmitVisible: true,
            SubmitEnabled: true,
            SubmitFocused: false,
            CancelOwned: true,
            CancelVisible: true,
            CancelEnabled: true,
            CancelFocused: false,
            Foreground: false,
            EditFocused: true,
            ReadinessDeadlineMilliseconds: 10_000,
            ReadinessElapsedMilliseconds: 9_999);
        CredentialNativeEntryCleanupEvidence cleanup = new(
            InitialBlank: false,
            Terminal: true,
            WindowDestroyed: true,
            BuffersCleared: true,
            ThreadJoined: true,
            ClipboardMessagesBlocked: false,
            Readiness: readiness,
            ActionReadiness: null,
            ActionSource: null,
            Action: null);
        HelperReceiptV2 receipt = new()
        {
            Outcome = HelperOutcomeV2.FailedKnown,
            AssignmentId = "wp4-v2/interactive-entry-submit/submit",
        };
        HelperProcessReceipt process = new(
            ProcessId: readiness.OwnerProcessId,
            ExitCode: 0,
            BinarySha256: new string('b', 64),
            Receipt: receipt,
            StagedResponseBytes: [],
            InheritedPrivateHandleCount: 2,
            StandardProtocolHandleCount: 0,
            ListenerCount: 0,
            NetworkOperationCount: 0,
            NativeCredentialOperationCount: 0,
            ProcessTreeSurvivorCount: 0,
            ProcessTreeTerminated: true,
            RetryAttempted: false,
            NativeCallTraceBytes: JsonSerializer.SerializeToUtf8Bytes(Array.Empty<CredentialNativeCallTraceEntry>()),
            NativeEntryCleanupBytes: JsonSerializer.SerializeToUtf8Bytes(cleanup),
            ContainmentProbeExecuted: true,
            TotalContainedProcessCount: 2);
        HelperStagingReceipt staging = new(
            "interactive-entry-submit-submit-attempt",
            "staging/interactive-entry-submit-submit-attempt/helper-receipt.v2.pb",
            223,
            new string('c', 64),
            null,
            0,
            null,
            StagedBeforeAdmission: true,
            CoordinatorOnlyAdmission: true);
        HelperAssignmentV2 assignment = HelperTestFrames.Assignment(HelperAssignmentKindV2.Enroll).Assignment;
        assignment.AssignmentId = receipt.AssignmentId;

        supervisor.ObserveThenRejectManualPhaseForTest(
            assignment,
            new CoordinatedHelperReceipt(process, staging),
            CredentialNativeManualValidationStage.ManualUi,
            "readiness-foreground-false");

        CredentialNativeFailedManualPhaseEvidence retained = supervisor.FailedManualPhaseForTest
            ?? throw new AssertFailedException("The failed manual phase was not retained.");
        Assert.AreEqual(HelperOutcomeV2.FailedKnown, retained.Outcome);
        Assert.AreEqual(0, retained.CredWriteW);
        Assert.AreEqual(0, retained.NativeCallTotal);
        Assert.AreEqual("failed-prewrite-ui-readiness", retained.Disposition);
        Assert.IsFalse(retained.EntryCleanup?.Readiness?.Foreground);
        Assert.IsTrue(retained.EntryCleanup?.Readiness?.WindowVisible);
        Assert.AreEqual(9_999, retained.EntryCleanup?.Readiness?.ReadinessElapsedMilliseconds);
        Assert.AreEqual(staging.Sha256, retained.Staging.ReceiptSha256);
        Assert.IsTrue(retained.Staging.StagedBeforeAdmission);
        Assert.AreEqual(nameof(CredentialNativeManualValidationStage.ManualUi), retained.ValidationStage);
        Assert.AreEqual("readiness-foreground-false", retained.ValidationReason);
    }

    [TestMethod]
    public void ProductionCaptureRetainsRawManualSnapshotAcrossEveryEarlyValidationFamily()
    {
        const string profile = "wp4-retained-failure";
        const string generation = "g001";
        const string assignmentId = "wp4-v2/interactive-entry-submit/submit";
        const string expectedFingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        HelperPrivateFrameV2 bootstrap = CredentialBootstrap(profile, generation, 119);
        HelperPrivateFrameV2 assignment = CredentialAssignment(
            profile, generation, HelperAssignmentKindV2.Enroll, assignmentId);
        CredentialNativeEntryCleanupEvidence cleanup = FailedReadinessCleanup();
        byte[] validTrace = JsonSerializer.SerializeToUtf8Bytes(Array.Empty<CredentialNativeCallTraceEntry>());
        byte[] validCanary = JsonSerializer.SerializeToUtf8Bytes(new CredentialNativeCanaryEvidence(
            0, 0, ["utf-8", "utf-16le"],
            [new("private protocol request", "private-pipe-bytes", 0, 0, 0)]));
        HelperReceiptV2 failedReceipt = new() { Outcome = HelperOutcomeV2.FailedKnown, AssignmentId = assignmentId };
        HelperReceiptV2 completedReceipt = new() { Outcome = HelperOutcomeV2.Completed, AssignmentId = assignmentId };
        HelperStagingReceipt staging = new(
            "retained-failure-attempt", "staging/retained/helper-receipt.v2.pb", 223,
            new string('c', 64), null, 0, null, true, true);
        HelperProcessReceipt valid = new(
            49152, 0, new string('b', 64), failedReceipt, [], 2, 0, 0, 0, 0, 0, true, false,
            validTrace, JsonSerializer.SerializeToUtf8Bytes(cleanup), validCanary,
            true, false, 0, 2);
        CredentialNativeCallTraceEntry wrongTarget = new(
            1, "CredReadW", new string('d', 64), assignmentId, "ERROR_NOT_FOUND", null, null);

        (string Name, HelperProcessReceipt Process, CredentialNativeManualValidationStage Stage)[] cases =
        [
            ("process", valid with { ProcessTreeSurvivorCount = 1 }, CredentialNativeManualValidationStage.ProcessJobBoundary),
            ("trace-json", valid with { NativeCallTraceBytes = "{"u8.ToArray() }, CredentialNativeManualValidationStage.NativeTrace),
            ("trace-null", valid with { NativeCallTraceBytes = "[null]"u8.ToArray(), NativeCredentialOperationCount = 1 }, CredentialNativeManualValidationStage.NativeTrace),
            ("trace-sequence", valid with { NativeCallTraceBytes = JsonSerializer.SerializeToUtf8Bytes<CredentialNativeCallTraceEntry[]>([wrongTarget with { Sequence = 2 }]), NativeCredentialOperationCount = 1 }, CredentialNativeManualValidationStage.NativeTrace),
            ("target", valid with { NativeCallTraceBytes = JsonSerializer.SerializeToUtf8Bytes<CredentialNativeCallTraceEntry[]>([wrongTarget]), NativeCredentialOperationCount = 1 }, CredentialNativeManualValidationStage.ExactTarget),
            ("canary-json", valid with { NativeCanaryEvidenceBytes = "{"u8.ToArray() }, CredentialNativeManualValidationStage.Canary),
            ("canary-semantic", valid with { NativeCanaryEvidenceBytes = JsonSerializer.SerializeToUtf8Bytes(new CredentialNativeCanaryEvidence(1, 0, ["utf-8", "utf-16le"], [new("x", "y", 0, 1, 0)])) }, CredentialNativeManualValidationStage.Canary),
            ("entry-json", valid with { NativeEntryCleanupBytes = "{"u8.ToArray() }, CredentialNativeManualValidationStage.ManualUi),
            ("entry-negative-terminal-count", valid with { NativeEntryCleanupBytes = JsonSerializer.SerializeToUtf8Bytes(cleanup with { PreReadinessTerminalMessages = -1 }) }, CredentialNativeManualValidationStage.ManualUi),
            ("entry-negative-ignored-count", valid with { NativeEntryCleanupBytes = JsonSerializer.SerializeToUtf8Bytes(cleanup with { PreReadinessIgnoredMessages = -1 }) }, CredentialNativeManualValidationStage.ManualUi),
            ("expected-outcome-later-ui", valid with { Receipt = completedReceipt }, CredentialNativeManualValidationStage.ManualUi),
        ];

        foreach ((string name, HelperProcessReceipt process, CredentialNativeManualValidationStage stage) in cases)
        {
            string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Capture-Mutation-" + name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
            using CredentialNativeQualificationSupervisor supervisor = new(
                new CredentialHelperCoordinator(store, Launcher(Path.Combine(root, "fake-store"))),
                expectedInheritedPrivateHandleCount: 2,
                targetFingerprints: new Dictionary<string, string> { [profile + "/" + generation] = expectedFingerprint });

            Assert.ThrowsExactly<InvalidDataException>(() => supervisor.CaptureManualPhaseForTest(
                "submit", bootstrap, assignment, new CoordinatedHelperReceipt(process, staging)));

            CredentialNativeFailedManualPhaseEvidence retained = supervisor.FailedManualPhaseForTest
                ?? throw new AssertFailedException(name + " did not retain manual evidence.");
            Assert.AreEqual(stage.ToString(), retained.ValidationStage, name);
            Assert.AreEqual(process.NativeEntryCleanupBytes?.Length ?? 0, retained.EntryCleanupByteLength, name);
            Assert.AreEqual(process.NativeCallTraceBytes?.Length ?? 0, retained.NativeTraceByteLength, name);
            Assert.AreEqual(process.NativeCredentialOperationCount, retained.ReportedNativeOperationCount, name);
            Assert.AreEqual(process.ProcessTreeSurvivorCount, retained.ProcessTreeSurvivorCount, name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(retained.ValidationReason), name);
            Assert.AreEqual(stage switch
            {
                CredentialNativeManualValidationStage.ProcessJobBoundary => "process-job-boundary-rejected",
                CredentialNativeManualValidationStage.NativeTrace => "native-trace-rejected",
                CredentialNativeManualValidationStage.ExactTarget => "trace-target-binding-rejected",
                CredentialNativeManualValidationStage.Canary => "canary-evidence-rejected",
                CredentialNativeManualValidationStage.ManualUi when name == "entry-json" => "entry-cleanup-json-rejected",
                CredentialNativeManualValidationStage.ManualUi => "entry-cleanup-semantic-rejected",
                _ => throw new AssertFailedException("Unexpected validation stage."),
            }, retained.ValidationReason, name);
            if (!retained.TraceCanonical)
            {
                Assert.AreEqual(-1, retained.CredWriteW, name);
                Assert.AreEqual("failed-manual-phase-trace-unvalidated", retained.Disposition, name);
            }
        }
    }

    [TestMethod]
    public void MaximumSizeCaptureRetainsCompletedRawTraceBeforePostStoreOracleRejects()
    {
        const string profile = "wp4-size-valid";
        const string generation = "g001";
        const string assignmentId = "wp4-v2/credential-size-boundaries/maximum";
        const string fingerprint = "abababababababababababababababababababababababababababababababab";
        HelperPrivateFrameV2 bootstrap = CredentialBootstrap(profile, generation, 121);
        HelperPrivateFrameV2 assignment = CredentialAssignment(
            profile, generation, HelperAssignmentKindV2.Enroll, assignmentId);
        CredentialNativeCallTraceEntry[] trace =
        [
            new(1, "CredWriteW", fingerprint, assignmentId, "success", null, null),
            new(2, "CredReadW", fingerprint, assignmentId, "success", 1, null),
            new(3, "CredFree", fingerprint, assignmentId, "released", null, 1),
        ];
        CredentialNativeCanaryEvidence rejectedCanary = new(
            1, 0, ["utf-8", "utf-16le"],
            [new("private protocol response", "private-pipe-bytes", 223, 1, 0)]);
        HelperProcessReceipt process = new(
            ProcessId: 50121,
            ExitCode: 0,
            BinarySha256: new string('b', 64),
            Receipt: new HelperReceiptV2
            {
                Outcome = HelperOutcomeV2.Completed,
                AssignmentId = assignmentId,
            },
            StagedResponseBytes: [],
            InheritedPrivateHandleCount: 2,
            StandardProtocolHandleCount: 0,
            ListenerCount: 0,
            NetworkOperationCount: 0,
            NativeCredentialOperationCount: trace.Length,
            ProcessTreeSurvivorCount: 0,
            ProcessTreeTerminated: true,
            RetryAttempted: false,
            NativeCallTraceBytes: JsonSerializer.SerializeToUtf8Bytes(trace),
            NativeEntryCleanupBytes: null,
            NativeCanaryEvidenceBytes: JsonSerializer.SerializeToUtf8Bytes(rejectedCanary),
            ContainmentProbeExecuted: true,
            ExcludedHandleAccessible: false,
            ActiveProcessCountBeforeJobClose: 0,
            TotalContainedProcessCount: 2);
        HelperStagingReceipt staging = new(
            "credential-size-boundaries-maximum-attempt",
            "staging/credential-size-boundaries-maximum-attempt/helper-receipt.v2.pb",
            217,
            new string('c', 64),
            null,
            0,
            null,
            StagedBeforeAdmission: true,
            CoordinatorOnlyAdmission: true);
        CredentialProfileProjection projection = new(
            profile,
            generation,
            GenerationOrdinal: 1,
            RevocationEpoch: 0,
            LifecycleState: "active-unverified",
            VerificationState: "unavailable",
            CapabilitySnapshotId: "capability",
            AccountIdentityId: "account",
            BillingScopeIdentityId: "billing",
            IntentId: "credential-size-boundaries-maximum-attempt-credential-transition:terminal",
            RecoveryDisposition: "not-required",
            CleanupDisposition: "not-requested",
            ProjectionVersion: 2,
            UpdatedAt: BaseTime);
        string root = Path.Combine(
            Path.GetTempPath(), "Infinium-Wp4-Maximum-Retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        using CredentialNativeQualificationSupervisor supervisor = new(
            new CredentialHelperCoordinator(store, Launcher(Path.Combine(root, "fake-store"))),
            expectedInheritedPrivateHandleCount: 2,
            targetFingerprints: new Dictionary<string, string>
            {
                [profile + "/" + generation] = fingerprint,
            });

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            supervisor.CapturePhaseForTest(
                "maximum",
                bootstrap,
                assignment,
                new CoordinatedHelperReceipt(process, staging),
                projection));
        Assert.AreEqual(
            CredentialNativeManualValidationStage.Canary,
            failure.Data[nameof(CredentialNativeManualValidationStage)]);

        CredentialNativeRejectedPhaseEvidence retained = supervisor.RejectedPhaseForTest
            ?? throw new AssertFailedException("The rejected maximum-size phase was discarded.");
        Assert.AreEqual(assignmentId, retained.AssignmentId);
        Assert.AreEqual("maximum", retained.PhaseId);
        Assert.AreEqual(profile, retained.AssignmentProfileId);
        Assert.AreEqual(generation, retained.AssignmentGenerationId);
        Assert.AreEqual(profile, retained.BootstrapProfileId);
        Assert.AreEqual(generation, retained.BootstrapGenerationId);
        CollectionAssert.AreEqual(new[] { fingerprint }, retained.ResolvedAllowedTargetFingerprints.ToArray());
        Assert.AreEqual(HelperOutcomeV2.Completed, retained.Outcome);
        Assert.AreEqual(3, retained.ReportedNativeOperationCount);
        Assert.AreEqual(1, retained.CanonicalCallCounts?.CredWriteW);
        Assert.AreEqual(1, retained.CanonicalCallCounts?.CredReadW);
        Assert.AreEqual(0, retained.CanonicalCallCounts?.CredDeleteW);
        Assert.AreEqual(1, retained.CanonicalCallCounts?.CredFree);
        Assert.AreEqual(3, retained.CanonicalCallCounts?.Total);
        Assert.IsTrue(retained.CanonicalCallTrace?.Select(item => item.Operation)
            .SequenceEqual(["CredWriteW", "CredReadW", "CredFree"], StringComparer.Ordinal));
        Assert.AreEqual(staging.Sha256, retained.Staging.ReceiptSha256);
        Assert.IsTrue(retained.Staging.StagedBeforeAdmission);
        Assert.AreEqual("active-unverified", retained.Lifecycle?.LifecycleState);
        Assert.AreEqual(projection.IntentId, retained.Lifecycle?.IntentId);
        Assert.AreEqual("canonical-validated", retained.NativeTraceParseResult);
        Assert.AreEqual("parsed", retained.CanaryParseResult);
        Assert.AreEqual(1, retained.Canaries?.SecretMatches);
        Assert.AreEqual(nameof(CredentialNativeManualValidationStage.Canary), retained.ValidationStage);
        Assert.AreEqual("canary-evidence-rejected", retained.ValidationReason);
        Assert.AreEqual(0, retained.ProcessTreeSurvivorCount);
        Assert.IsTrue(retained.ProcessTreeTerminated);

        Assert.ThrowsExactly<InvalidDataException>(() => supervisor.CapturePhaseForTest(
            "maximum",
            bootstrap,
            assignment,
            new CoordinatedHelperReceipt(
                process with { ProcessTreeSurvivorCount = 1 },
                staging),
            projection));
        Assert.AreEqual(nameof(CredentialNativeManualValidationStage.Canary),
            supervisor.RejectedPhaseForTest?.ValidationStage,
            "A later cleanup/capture rejection must not overwrite the first rejected phase.");

        CredentialNativeQualificationEvidence snapshot = supervisor.SnapshotForTest();
        Assert.AreEqual(0, snapshot.NativeCallCounts.Total,
            "Rejected phase calls must not be double-counted as admitted phase calls.");
        Assert.AreEqual(0, snapshot.Scenarios.Count);
        Assert.AreEqual(3, snapshot.RejectedPhase?.CanonicalCallCounts?.Total);

        CredentialNativeQualificationEvidence terminal = new(
            BaseTime,
            BaseTime.AddSeconds(1),
            1650,
            120,
            30,
            1800,
            NamespaceBlocked: true,
            CleanupAmbiguous: false,
            new(0, 0, 0, 0, 0),
            StaleGate: null,
            FailedManualPhase: null,
            RejectedPhase: retained,
            Scenarios: []);
        using JsonDocument serialized = JsonDocument.Parse(
            CredentialNativeQualificationRunner.SerializeEvidenceForTest(terminal));
        JsonElement retainedJson = serialized.RootElement.GetProperty("rejected_phase");
        Assert.AreEqual("maximum", retainedJson.GetProperty("phase_id").GetString());
        Assert.AreEqual(profile, retainedJson.GetProperty("assignment_profile_id").GetString());
        Assert.AreEqual(generation, retainedJson.GetProperty("assignment_generation_id").GetString());
        Assert.AreEqual(profile, retainedJson.GetProperty("bootstrap_profile_id").GetString());
        Assert.AreEqual(generation, retainedJson.GetProperty("bootstrap_generation_id").GetString());
        Assert.AreEqual(fingerprint,
            retainedJson.GetProperty("resolved_allowed_target_fingerprints")[0].GetString());
        Assert.AreEqual(3,
            retainedJson.GetProperty("canonical_call_counts").GetProperty("total").GetInt32());
        Assert.AreEqual(3, retainedJson.GetProperty("canonical_call_trace").GetArrayLength());
        Assert.AreEqual("Canary", retainedJson.GetProperty("validation_stage").GetString());
        Assert.AreEqual("canary-evidence-rejected",
            retainedJson.GetProperty("validation_reason").GetString());
    }

    [TestMethod]
    public void RejectedPhaseObservationRetainsMalformedPayloadFactsWithoutThrowingBeforeTypedStage()
    {
        const string profile = "wp4-size-valid";
        const string generation = "g001";
        const string assignmentId = "wp4-v2/credential-size-boundaries/maximum";
        const string fingerprint = "abababababababababababababababababababababababababababababababab";
        HelperPrivateFrameV2 bootstrap = CredentialBootstrap(profile, generation, 122);
        HelperPrivateFrameV2 assignment = CredentialAssignment(
            profile, generation, HelperAssignmentKindV2.Enroll, assignmentId);
        byte[] validTrace = "[]"u8.ToArray();
        byte[] validCanary = JsonSerializer.SerializeToUtf8Bytes(new CredentialNativeCanaryEvidence(
            0, 0, ["utf-8", "utf-16le"],
            [new("private protocol request", "private-pipe-bytes", 0, 0, 0)]));
        JsonNode nullCanaryNode = JsonNode.Parse(validCanary)
            ?? throw new AssertFailedException("The canary fixture did not parse.");
        nullCanaryNode["RawTargetEncodings"] = null;
        byte[] nullCanary = JsonSerializer.SerializeToUtf8Bytes(nullCanaryNode);
        HelperProcessReceipt valid = new(
            50122, 0, new string('b', 64),
            new HelperReceiptV2 { Outcome = HelperOutcomeV2.Completed, AssignmentId = assignmentId },
            [], 2, 0, 0, 0, 0, 0, true, false,
            validTrace, null, validCanary, true, false, 0, 2);
        HelperStagingReceipt staging = new(
            "maximum-malformed", "staging/maximum-malformed/helper-receipt.v2.pb",
            217, new string('c', 64), null, 0, null, true, true);
        (string Name, HelperProcessReceipt Process, CredentialNativeManualValidationStage Stage, string Parse)[] cases =
        [
            ("trace-null", valid with
                {
                    NativeCallTraceBytes = "[null]"u8.ToArray(),
                    NativeCredentialOperationCount = 1,
                }, CredentialNativeManualValidationStage.NativeTrace, "malformed-InvalidDataException"),
            ("trace-null-property", valid with
                {
                    NativeCallTraceBytes = "[{\"sequence\":1,\"operation\":null,\"targetFingerprintSha256\":null,\"scenario\":null,\"result\":null}]"u8.ToArray(),
                    NativeCredentialOperationCount = 1,
                }, CredentialNativeManualValidationStage.NativeTrace, "malformed-InvalidDataException"),
            ("canary-json", valid with
                {
                    NativeCanaryEvidenceBytes = "{"u8.ToArray(),
                }, CredentialNativeManualValidationStage.Canary, "malformed-JsonException"),
            ("canary-null-required", valid with
                {
                    NativeCanaryEvidenceBytes = nullCanary,
                }, CredentialNativeManualValidationStage.Canary, "parsed"),
            ("entry-json", valid with
                {
                    NativeEntryCleanupBytes = "{"u8.ToArray(),
                }, CredentialNativeManualValidationStage.ManualUi, "malformed-JsonException"),
        ];

        foreach ((string name, HelperProcessReceipt process, CredentialNativeManualValidationStage stage, string parse) in cases)
        {
            string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Rejected-Payload-" + name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
            using CredentialNativeQualificationSupervisor supervisor = new(
                new CredentialHelperCoordinator(store, Launcher(Path.Combine(root, "fake-store"))),
                expectedInheritedPrivateHandleCount: 2,
                targetFingerprints: new Dictionary<string, string>
                {
                    [profile + "/" + generation] = fingerprint,
                });
            Assert.ThrowsExactly<InvalidDataException>(() => supervisor.CapturePhaseForTest(
                "maximum", bootstrap, assignment, new CoordinatedHelperReceipt(process, staging)));
            CredentialNativeRejectedPhaseEvidence retained = supervisor.RejectedPhaseForTest
                ?? throw new AssertFailedException(name + " discarded its raw observation.");
            Assert.AreEqual(stage.ToString(), retained.ValidationStage, name);
            Assert.AreEqual(process.NativeCallTraceBytes?.Length ?? 0, retained.NativeTraceByteLength, name);
            Assert.AreEqual(process.NativeEntryCleanupBytes?.Length ?? 0, retained.EntryCleanupByteLength, name);
            Assert.AreEqual(process.NativeCanaryEvidenceBytes?.Length ?? 0, retained.CanaryByteLength, name);
            if (stage == CredentialNativeManualValidationStage.NativeTrace)
            {
                Assert.AreEqual(parse, retained.NativeTraceParseResult, name);
                Assert.IsNotNull(retained.NativeTraceSha256, name);
            }
            else if (stage == CredentialNativeManualValidationStage.Canary)
            {
                Assert.AreEqual(parse, retained.CanaryParseResult, name);
                Assert.IsNotNull(retained.CanarySha256, name);
            }
            else
            {
                Assert.AreEqual(parse, retained.EntryCleanupParseResult, name);
                Assert.IsNotNull(retained.EntryCleanupSha256, name);
            }
        }

        CredentialNativeEntryCleanupEvidence entry = FailedReadinessCleanup();
        JsonNode nullEntryNode = JsonNode.Parse(JsonSerializer.SerializeToUtf8Bytes(entry))
            ?? throw new AssertFailedException("The entry fixture did not parse.");
        nullEntryNode["Readiness"]!["DesktopNameSha256"] = null;
        byte[] nullEntry = JsonSerializer.SerializeToUtf8Bytes(nullEntryNode);
        HelperPrivateFrameV2 manualBootstrap = CredentialBootstrap("wp4-interactive-primary", generation, 124);
        HelperPrivateFrameV2 manualAssignment = CredentialAssignment(
            "wp4-interactive-primary", generation, HelperAssignmentKindV2.Enroll,
            "wp4-v2/interactive-entry-submit/submit");
        string manualRoot = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Rejected-Entry-Null-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(manualRoot);
        using AuthoritativeStore manualStore = new(new StoragePaths(Path.Combine(manualRoot, "product")));
        using CredentialNativeQualificationSupervisor manualSupervisor = new(
            new CredentialHelperCoordinator(manualStore, Launcher(Path.Combine(manualRoot, "fake-store"))),
            expectedInheritedPrivateHandleCount: 2,
            targetFingerprints: new Dictionary<string, string>
            {
                ["wp4-interactive-primary/" + generation] = fingerprint,
            });
        HelperProcessReceipt manualProcess = valid with
        {
            Receipt = new HelperReceiptV2
            {
                Outcome = HelperOutcomeV2.FailedKnown,
                AssignmentId = manualAssignment.Assignment.AssignmentId,
            },
            NativeEntryCleanupBytes = nullEntry,
        };
        Assert.ThrowsExactly<InvalidDataException>(() => manualSupervisor.CapturePhaseForTest(
            "submit", manualBootstrap, manualAssignment,
            new CoordinatedHelperReceipt(manualProcess, staging)));
        CredentialNativeRejectedPhaseEvidence manualRetained = manualSupervisor.RejectedPhaseForTest
            ?? throw new AssertFailedException("The semantic-null entry was discarded.");
        Assert.AreEqual(nameof(CredentialNativeManualValidationStage.ManualUi), manualRetained.ValidationStage);
        Assert.AreEqual("entry-cleanup-semantic-rejected", manualRetained.ValidationReason);
        Assert.AreEqual("parsed", manualRetained.EntryCleanupParseResult);
        Assert.AreEqual(nullEntry.Length, manualRetained.EntryCleanupByteLength);
        Assert.IsNotNull(manualRetained.EntryCleanupSha256);
        Assert.AreEqual(0, manualSupervisor.SnapshotForTest().Scenarios.Count);
        _ = CredentialNativeQualificationRunner.SerializeEvidenceForTest(manualSupervisor.SnapshotForTest());
    }

    [TestMethod]
    public void PreflightOracleRejectsBeforeScenarioAdmissionAndAggregateCounting()
    {
        const string profile = "wp4-size-valid";
        const string generation = "g001";
        const string assignmentId = "wp4-v2/credential-size-boundaries/preflight-maximum";
        const string fingerprint = "abababababababababababababababababababababababababababababababab";
        HelperPrivateFrameV2 bootstrap = CredentialBootstrap(profile, generation, 123);
        HelperPrivateFrameV2 assignment = CredentialAssignment(
            profile, generation, HelperAssignmentKindV2.Verify, assignmentId);
        CredentialNativeCallTraceEntry[] mutatingTrace =
        [
            new(1, "CredWriteW", fingerprint, assignmentId, "success", null, null),
        ];
        HelperProcessReceipt process = new(
            50123, 0, new string('b', 64),
            new HelperReceiptV2 { Outcome = HelperOutcomeV2.FailedKnown, AssignmentId = assignmentId },
            [], 2, 0, 0, 0, 1, 0, true, false,
            JsonSerializer.SerializeToUtf8Bytes(mutatingTrace),
            null,
            JsonSerializer.SerializeToUtf8Bytes(new CredentialNativeCanaryEvidence(
                0, 0, ["utf-8", "utf-16le"],
                [new("private protocol request", "private-pipe-bytes", 0, 0, 0)])),
            true, false, 0, 2);
        HelperStagingReceipt staging = new(
            assignmentId + "-preflight", "none", 0, new string('0', 64),
            null, 0, null, false, true);
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Preflight-Oracle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        using CredentialNativeQualificationSupervisor supervisor = new(
            new CredentialHelperCoordinator(store, Launcher(Path.Combine(root, "fake-store"))),
            expectedInheritedPrivateHandleCount: 2,
            targetFingerprints: new Dictionary<string, string>
            {
                [profile + "/" + generation] = fingerprint,
            });

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            supervisor.CapturePreflightPhaseForTest(
                "credential-size-boundaries",
                "preflight-maximum",
                bootstrap,
                assignment,
                new CoordinatedHelperReceipt(process, staging)));
        Assert.AreEqual("preflight-absence-oracle-rejected",
            failure.Data["CredentialNativeManualValidationReasonCode"]);
        CredentialNativeQualificationEvidence snapshot = supervisor.SnapshotForTest();
        Assert.AreEqual(0, snapshot.Scenarios.Count);
        Assert.AreEqual(0, snapshot.NativeCallCounts.Total);
        Assert.AreEqual(1, snapshot.RejectedPhase?.CanonicalCallCounts?.CredWriteW);
        Assert.AreEqual("preflight-absence-oracle-rejected", snapshot.RejectedPhase?.ValidationReason);
    }

    [TestMethod]
    public void CleanupAbsenceOracleSetsAmbiguityBeforeAdmissionAndAggregateCounting()
    {
        const string profile = "wp4-interactive-primary";
        const string generation = "g001";
        const string assignmentId = "wp4-v2/interactive-entry-submit/cleanup";
        const string fingerprint = "abababababababababababababababababababababababababababababababab";
        HelperPrivateFrameV2 bootstrap = CredentialBootstrap(profile, generation, 125);
        HelperPrivateFrameV2 assignment = CredentialAssignment(
            profile, generation, HelperAssignmentKindV2.Delete, assignmentId);
        CredentialNativeCallTraceEntry[] incompleteTrace =
        [
            new(1, "CredDeleteW", fingerprint, assignmentId, "success", null, null),
        ];
        HelperProcessReceipt process = new(
            50125, 0, new string('b', 64),
            new HelperReceiptV2 { Outcome = HelperOutcomeV2.Completed, AssignmentId = assignmentId },
            [], 2, 0, 0, 0, 1, 0, true, false,
            JsonSerializer.SerializeToUtf8Bytes(incompleteTrace),
            null,
            JsonSerializer.SerializeToUtf8Bytes(new CredentialNativeCanaryEvidence(
                0, 0, ["utf-8", "utf-16le"],
                [new("private protocol request", "private-pipe-bytes", 0, 0, 0)])),
            true, false, 0, 2);
        HelperStagingReceipt staging = new(
            "cleanup-incomplete", "staging/cleanup-incomplete/helper-receipt.v2.pb",
            224, new string('c', 64), null, 0, null, true, true);
        CredentialProfileProjection projection = new(
            profile, generation, 1, 1, "deleted", "unavailable",
            null, null, null, "cleanup-incomplete:terminal", "not-required", "confirmed", 3, BaseTime);
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Cleanup-Oracle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        using CredentialNativeQualificationSupervisor supervisor = new(
            new CredentialHelperCoordinator(store, Launcher(Path.Combine(root, "fake-store"))),
            expectedInheritedPrivateHandleCount: 2,
            targetFingerprints: new Dictionary<string, string>
            {
                [profile + "/" + generation] = fingerprint,
            });

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(() =>
            supervisor.CaptureCleanupPhaseForTest(
                "interactive-entry-submit", "cleanup", bootstrap, assignment,
                new CoordinatedHelperReceipt(process, staging), projection));
        Assert.AreEqual("cleanup-absence-oracle-rejected",
            failure.Data["CredentialNativeManualValidationReasonCode"]);
        CredentialNativeQualificationEvidence snapshot = supervisor.SnapshotForTest();
        Assert.IsTrue(snapshot.CleanupAmbiguous);
        Assert.IsTrue(snapshot.NamespaceBlocked);
        Assert.AreEqual(0, snapshot.Scenarios.Count);
        Assert.AreEqual(0, snapshot.NativeCallCounts.Total);
        Assert.AreEqual(1, snapshot.RejectedPhase?.CanonicalCallCounts?.CredDeleteW);
        Assert.AreEqual("cleanup-absence-oracle-rejected", snapshot.RejectedPhase?.ValidationReason);
    }

    private static OneShotCredentialHelperLauncher Launcher(string fakeStoreRoot)
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        return new(
            helper,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helper))),
            fakeStoreRoot);
    }

    private static Dictionary<string, (string ProfileId, string GenerationId)> AllTargetIdentities() =>
        new(StringComparer.Ordinal)
        {
            ["interactive-primary"] = ("wp4-interactive-primary", "g001"),
            ["interactive-cancel"] = ("wp4-interactive-cancel", "g001"),
            ["size-valid"] = ("wp4-size-valid", "g001"),
            ["size-oversize"] = ("wp4-size-oversize", "g001"),
            ["unavailable-store"] = ("wp4-unavailable", "g001"),
            ["replacement-old"] = ("wp4-replacement", "g001"),
            ["replacement-new"] = ("wp4-replacement", "g002"),
            ["revoke-delete"] = ("wp4-revoke-delete", "g001"),
            ["crash-restart"] = ("wp4-crash-restart", "g001"),
            ["backup-old"] = ("wp4-backup", "g001"),
            ["backup-new"] = ("wp4-backup", "g002"),
            ["fake-dispatch"] = ("wp4-fake-dispatch", "g001"),
        };

    private static CredentialNativeEntryCleanupEvidence FailedReadinessCleanup()
    {
        CredentialNativeEntryReadinessEvidence readiness = new(
            49152, 37, System.Diagnostics.Process.GetCurrentProcess().SessionId,
            new string('a', 64), true, true, true, true, true, true, true, true, true,
            true, true, "submit",
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                "Enter a disposable test value, then click Submit. Never use a real credential."))),
            true, true, true, true, true, true, true, false,
            true, true, true, false, false, true, 10_000, 9_999);
        return new(false, true, true, true, true, false, readiness, null, null, null);
    }

    private static HelperPrivateFrameV2 CredentialBootstrap(string profile, string generation, byte nonce)
    {
        HelperPrivateFrameV2 frame = HelperTestFrames.Bootstrap(nonceSeed: nonce);
        frame.Bootstrap.Credential.AccessProfileId.Value = profile;
        frame.Bootstrap.Credential.GenerationId.Value = generation;
        return frame;
    }

    private static HelperPrivateFrameV2 CredentialAssignment(
        string profile,
        string generation,
        HelperAssignmentKindV2 kind,
        string assignmentId)
    {
        HelperPrivateFrameV2 frame = HelperTestFrames.Assignment(kind);
        frame.Assignment.AssignmentId = assignmentId;
        frame.Assignment.AccessProfileId.Value = profile;
        frame.Assignment.GenerationId.Value = generation;
        frame.Assignment.Credential.AccessProfileId.Value = profile;
        frame.Assignment.Credential.GenerationId.Value = generation;
        return frame;
    }
}
