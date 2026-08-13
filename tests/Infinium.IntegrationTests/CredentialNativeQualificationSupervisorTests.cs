using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
        Assert.IsFalse(failure.Evidence.CleanupAmbiguous);
        Assert.IsFalse(failure.Evidence.NamespaceBlocked);
        CredentialNativeQualificationPhaseEvidence cleanup = failure.Evidence.Scenarios
            .Single(item => item.ScenarioId == "interactive-entry-submit")
            .Phases.Single(item => item.PhaseId == "cleanup");
        Assert.AreEqual(HelperOutcomeV2.Completed, cleanup.Outcome);
        Assert.AreEqual(0, DeterministicFakeSecureStore.NativeOperationCount);
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

    private static OneShotCredentialHelperLauncher Launcher(string fakeStoreRoot)
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        return new(
            helper,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helper))),
            fakeStoreRoot);
    }

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
