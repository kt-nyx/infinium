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

    private static OneShotCredentialHelperLauncher Launcher(string fakeStoreRoot)
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        return new(
            helper,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helper))),
            fakeStoreRoot);
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
