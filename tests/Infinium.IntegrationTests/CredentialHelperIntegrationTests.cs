using System.Runtime.InteropServices;
using Infinium.Application.Provider;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialHelperIntegrationTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void PrivateHelperEnvironmentRetainsOnlyRequiredWindowsAndDiagnosticsBindings()
    {
        IReadOnlyDictionary<string, string> environment =
            OneShotCredentialHelperLauncher.PrivateHelperEnvironment();

        Assert.HasCount(2, environment);
        Assert.AreEqual(
            Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows)),
            environment["SystemRoot"]);
        Assert.AreEqual("0", environment["DOTNET_EnableDiagnostics"]);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            OneShotCredentialHelperLauncher.PrivateHelperEnvironment(""));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            OneShotCredentialHelperLauncher.PrivateHelperEnvironment("   "));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            OneShotCredentialHelperLauncher.PrivateHelperEnvironment("relative-windows"));
    }

    [TestMethod]
    public async Task HelperPrivateHandleLaunchesExactRepositoryBinaryWithoutStandardProtocolOrRetry()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        Assert.IsTrue(File.Exists(helper), helper);
        string secureStoreRoot = Path.Combine(Path.GetTempPath(), "Infinium-CapabilityBoundChildStore-" + Guid.NewGuid().ToString("N"));
        OneShotCredentialHelperLauncher launcher = new(
            helper,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(helper))),
            secureStoreRoot);
        HelperProcessReceipt result = await launcher.ExecuteAsync(
            HelperTestFrames.Bootstrap(), HelperTestFrames.Assignment(), null, TimeSpan.FromSeconds(20), BaseTime);
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(HelperOutcomeV2.Completed, result.Receipt.Outcome);
        Assert.AreEqual(3, result.InheritedPrivateHandleCount);
        Assert.AreEqual(0, result.StandardProtocolHandleCount);
        Assert.AreEqual(0, result.ListenerCount);
        Assert.AreEqual(0, result.NetworkOperationCount);
        Assert.AreEqual(0, result.NativeCredentialOperationCount);
        Assert.IsTrue(result.ProcessTreeTerminated);
        Assert.IsFalse(result.RetryAttempted);
        Assert.AreEqual(64, result.BinarySha256.Length);
        string root = Path.Combine(Path.GetTempPath(), "Infinium-CredentialStaging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        CredentialHelperCoordinator coordinator = new(store, launcher);
        CoordinatedHelperReceipt coordinated = await coordinator.ExecuteStageAndAdmitAsync(
            "helper-attempt-1", HelperTestFrames.Bootstrap(nonceSeed: 1), HelperTestFrames.Assignment(), null, BaseTime);
        Assert.IsTrue(coordinated.Staging.StagedBeforeAdmission);
        Assert.IsTrue(coordinated.Staging.CoordinatorOnlyAdmission);
        Assert.IsTrue(File.Exists(Path.Combine(store.Paths.Staging, coordinated.Staging.RelativePath)));
        byte[] secretCanary = "INFINIUM-HELPER-TEST-SECRET"u8.ToArray();
        byte[] targetCanary = "CAPABILITY-BOUND-STORE-TARGET-CANARY"u8.ToArray();
        byte[] secureStoreBytes = File.ReadAllBytes(Path.Combine(secureStoreRoot, "synthetic-secure-store.v1.json"));
        Assert.IsGreaterThanOrEqualTo(0, secureStoreBytes.AsSpan().IndexOf(targetCanary));
        using (System.Text.Json.JsonDocument secureStoreDocument = System.Text.Json.JsonDocument.Parse(secureStoreBytes))
        {
            byte[] realChildSecret = Convert.FromBase64String(
                secureStoreDocument.RootElement.GetProperty("Values").EnumerateObject().Single().Value.GetString()!);
            Assert.IsGreaterThanOrEqualTo(0, realChildSecret.AsSpan().IndexOf(secretCanary));
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(realChildSecret);
        }
        string productRoot = Path.Combine(root, "product");
        string stagingRoot = store.Paths.Staging;
        store.Dispose();
        (int secretCanaryMatches, int targetCanaryMatches) = ScanCanaries(
            productRoot, secretCanary, targetCanary);
        string mutation = Path.Combine(stagingRoot, "canary-leak-mutation.bin");
        File.WriteAllBytes(mutation, [.. secretCanary, .. targetCanary]);
        (int mutatedSecretMatches, int mutatedTargetMatches) = ScanCanaries(
            productRoot, secretCanary, targetCanary);
        File.Delete(mutation);
        bool canaryMutationRejected = mutatedSecretMatches > 0 && mutatedTargetMatches > 0;
        Assert.AreEqual(0, secretCanaryMatches);
        Assert.AreEqual(0, targetCanaryMatches);
        Assert.IsTrue(canaryMutationRejected);
    }

    [TestMethod]
    public async Task CredentialDispatchRequiresFinalGenerationRevocationDeadlineAndBudgetRevalidation()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = Launcher(helper);
        HelperProcessReceipt enrollment = await launcher.ExecuteAsync(
            HelperTestFrames.Bootstrap(nonceSeed: 2), HelperTestFrames.Assignment(), null,
            TimeSpan.FromSeconds(20), BaseTime);
        Assert.AreEqual(HelperOutcomeV2.Completed, enrollment.Receipt.Outcome);
        HelperProcessReceipt verification = await launcher.ExecuteAsync(
            HelperTestFrames.Bootstrap(nonceSeed: 3), HelperTestFrames.Assignment(HelperAssignmentKindV2.Verify), null,
            TimeSpan.FromSeconds(20), BaseTime);
        Assert.AreEqual(HelperOutcomeV2.Completed, verification.Receipt.Outcome,
            "The capability-bound fake store must persist across separate one-shot helper launches.");

        HelperProcessReceipt result = await launcher.ExecuteAsync(
            HelperTestFrames.DispatchBootstrap(4), HelperTestFrames.DispatchAssignment(),
            HelperTestFrames.Revalidation(), TimeSpan.FromSeconds(20), BaseTime);
        Assert.AreEqual(HelperOutcomeV2.Completed, result.Receipt.Outcome);
        Assert.IsGreaterThan(0, result.StagedResponseBytes.Length);
        Assert.IsTrue(result.Receipt.TransportMayHaveStarted);
        Assert.IsFalse(result.RetryAttempted);

        HelperPrivateFrameV2 stale = HelperTestFrames.Revalidation();
        stale.DispatchRevalidation.RevocationEpoch = 1;
        HelperProcessReceipt rejected = await launcher.ExecuteAsync(
            HelperTestFrames.DispatchBootstrap(5), HelperTestFrames.DispatchAssignment(), stale, TimeSpan.FromSeconds(20), BaseTime);
        Assert.AreEqual(HelperOutcomeV2.FailedKnown, rejected.Receipt.Outcome);
        Assert.IsFalse(rejected.Receipt.TransportMayHaveStarted);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => launcher.ExecuteAsync(
            HelperTestFrames.Bootstrap(nonceSeed: 2), HelperTestFrames.Assignment(), null,
            TimeSpan.FromSeconds(20), BaseTime));
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => launcher.ExecuteAsync(
            HelperTestFrames.Bootstrap(nonceSeed: 6), HelperTestFrames.Assignment(), null,
            TimeSpan.FromSeconds(20), BaseTime.AddMinutes(2)));
    }

    [TestMethod]
    public async Task HelperPrivateHandleListExcludesUnrelatedInheritableHandleAndJobKillsDescendant()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = Launcher(helper);
        SECURITY_ATTRIBUTES attributes = new()
        {
            Length = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            InheritHandle = true,
        };
        nint sentinel = CreateEventW(ref attributes, manualReset: true, initialState: false, null);
        Assert.AreNotEqual(0, sentinel);
        try
        {
            HelperProcessReceipt receipt = await launcher.ExecuteContainmentProbeAsync(
                HelperTestFrames.Bootstrap(nonceSeed: 44), HelperTestFrames.Assignment(),
                TimeSpan.FromSeconds(20), BaseTime, sentinel);
            Assert.IsTrue(receipt.ProcessTreeTerminated);
            Assert.AreEqual(0, receipt.ProcessTreeSurvivorCount);
            Assert.AreEqual(3, receipt.InheritedPrivateHandleCount);
        }
        finally
        {
            _ = CloseHandle(sentinel);
        }
    }

    [TestMethod]
    public async Task ContainmentAcceptsNaturallyExitedProbeAfterSlowHelperResponseWithoutPidReopen()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = Launcher(helper);
        SECURITY_ATTRIBUTES attributes = new()
        {
            Length = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            InheritHandle = true,
        };
        nint sentinel = CreateEventW(ref attributes, manualReset: true, initialState: false, null);
        Assert.AreNotEqual(0, sentinel);
        try
        {
            HelperProcessReceipt receipt = await launcher.ExecuteContainmentProbeAsync(
                HelperTestFrames.Bootstrap(nonceSeed: 45),
                HelperTestFrames.Assignment(),
                TimeSpan.FromSeconds(20),
                BaseTime,
                sentinel,
                descendantLifetime: TimeSpan.FromMilliseconds(50),
                postEngineDelay: TimeSpan.FromMilliseconds(250));
            Assert.IsTrue(receipt.ContainmentProbeExecuted);
            Assert.AreEqual(0, receipt.ActiveProcessCountBeforeJobClose);
            Assert.IsGreaterThanOrEqualTo(2, receipt.TotalContainedProcessCount);
            Assert.IsTrue(receipt.ProcessTreeTerminated);
            Assert.AreEqual(0, receipt.ProcessTreeSurvivorCount);
            Assert.AreEqual(0, receipt.NativeCredentialOperationCount);
            Assert.AreEqual(0, receipt.NetworkOperationCount);
        }
        finally
        {
            _ = CloseHandle(sentinel);
        }
    }

    [TestMethod]
    public async Task ContainmentTerminatesLiveProbeThroughJobAndProvesZeroActiveProcesses()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = Launcher(helper);
        SECURITY_ATTRIBUTES attributes = new()
        {
            Length = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            InheritHandle = true,
        };
        nint sentinel = CreateEventW(ref attributes, manualReset: true, initialState: false, null);
        Assert.AreNotEqual(0, sentinel);
        try
        {
            HelperProcessReceipt receipt = await launcher.ExecuteContainmentProbeAsync(
                HelperTestFrames.Bootstrap(nonceSeed: 46),
                HelperTestFrames.Assignment(),
                TimeSpan.FromSeconds(20),
                BaseTime,
                sentinel,
                descendantLifetime: TimeSpan.FromSeconds(5));
            Assert.IsGreaterThanOrEqualTo(1, receipt.ActiveProcessCountBeforeJobClose);
            Assert.IsGreaterThanOrEqualTo(2, receipt.TotalContainedProcessCount);
            Assert.IsTrue(receipt.ProcessTreeTerminated);
            Assert.AreEqual(0, receipt.ProcessTreeSurvivorCount);
            Assert.AreEqual(0, receipt.NativeCredentialOperationCount);
            Assert.AreEqual(0, receipt.NetworkOperationCount);
        }
        finally
        {
            _ = CloseHandle(sentinel);
        }
    }

    [TestMethod]
    public async Task VerifiedEnrollmentPublishesExactActiveVerifiedGenerationWithoutDispatch()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-VerifiedEnrollment-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        store.PublishProviderCatalog(OpenAiProviderProfileCatalog.Capability, OpenAiProviderProfileCatalog.Price, BaseTime);
        _ = store.BeginCredentialEnrollment(
            "profile-verified", "generation-verified", "verified enrollment", BaseTime.AddSeconds(1),
            "account-wp9", "billing-wp9");
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = Launcher(helper);
        CredentialHelperCoordinator coordinator = new(store, launcher);

        (CoordinatedHelperReceipt receipt, CredentialProfileProjection projection) =
            await coordinator.ExecuteVerifiedEnrollmentAsync(
                "wp9-verified-enrollment-attempt",
                CredentialBootstrap("profile-verified", "generation-verified", 121),
                CredentialAssignment(
                    "profile-verified", "generation-verified", HelperAssignmentKindV2.Enroll,
                    "wp9-production-profile/enroll-and-verify"),
                BaseTime.AddSeconds(2));

        Assert.AreEqual(HelperOutcomeV2.Completed, receipt.Process.Receipt.Outcome);
        Assert.AreEqual("active-verified", projection.LifecycleState);
        Assert.AreEqual("available", projection.VerificationState);
        Assert.AreEqual("generation-verified", projection.GenerationId);
        Assert.AreEqual(0, receipt.Process.NetworkOperationCount);
        Assert.AreEqual(0, receipt.Process.NativeCredentialOperationCount);
        Assert.AreEqual(0, receipt.Process.StagedResponseBytes.Length);
        Assert.IsFalse(receipt.Process.RetryAttempted);
    }

    [TestMethod]
    public void ContainmentEvidenceRejectsReportedPidWithoutJobMembershipHistory()
    {
        Assert.IsFalse(OneShotCredentialHelperLauncher.ValidateContainmentEvidence(
            probeExecuted: true, reportedDescendantPid: 1234,
            totalContainedProcessCount: 1, activeProcessCountAfterTermination: 0));
        Assert.IsTrue(OneShotCredentialHelperLauncher.ValidateContainmentEvidence(
            probeExecuted: true, reportedDescendantPid: 1234,
            totalContainedProcessCount: 2, activeProcessCountAfterTermination: 0));
        Assert.IsFalse(OneShotCredentialHelperLauncher.ValidateContainmentEvidence(
            probeExecuted: true, reportedDescendantPid: 1234,
            totalContainedProcessCount: 2, activeProcessCountAfterTermination: 1));
    }

    [TestMethod]
    public async Task CredentialIntentRecoversWhenHelperStoreCommitPrecedesMetadataCommit()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-CredentialHalfCommit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        store.PublishProviderCatalog(OpenAiProviderProfileCatalog.Capability, OpenAiProviderProfileCatalog.Price, BaseTime);
        _ = store.BeginCredentialEnrollment(
            "profile-half", "generation-half", "Half commit", BaseTime.AddSeconds(1), "account-1", "billing-1");
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = Launcher(helper);
        CredentialHelperCoordinator coordinator = new(store, launcher);
        HelperPrivateFrameV2 bootstrap = CredentialBootstrap("profile-half", "generation-half", 81);
        HelperPrivateFrameV2 enrollment = CredentialAssignment(
            "profile-half", "generation-half", HelperAssignmentKindV2.Enroll, "half-enroll");
        HelperProcessReceipt halfCommitted = await launcher.ExecuteAsync(
            bootstrap, enrollment, null, TimeSpan.FromSeconds(20), BaseTime.AddSeconds(2));
        Assert.AreEqual(HelperOutcomeV2.Completed, halfCommitted.Receipt.Outcome);
        Assert.AreEqual("pending-enrollment", store.GetCredentialProfile("profile-half").LifecycleState);

        HelperPrivateFrameV2 recoveryBootstrap = CredentialBootstrap("profile-half", "generation-half", 82);
        HelperPrivateFrameV2 recovery = CredentialAssignment(
            "profile-half", "generation-half", HelperAssignmentKindV2.Recover, "half-recover");
        (_, CredentialProfileProjection projection) = await coordinator.ExecuteCredentialTransitionAsync(
            "half-recover-attempt", recoveryBootstrap, recovery, BaseTime.AddSeconds(4));
        Assert.AreEqual("active-unverified", projection.LifecycleState);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CredentialDeletePersistsRevocationBeforeHelperAndCompletesAfterRestart()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-CredentialDeleteRestart-" + Guid.NewGuid().ToString("N"));
        string productRoot = Path.Combine(root, "product");
        string fakeStoreRoot = Path.Combine(root, "fake-secure-store");
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = new(
            helper,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(helper))),
            fakeStoreRoot);

        using (AuthoritativeStore store = new(new StoragePaths(productRoot)))
        {
            store.PublishProviderCatalog(OpenAiProviderProfileCatalog.Capability, OpenAiProviderProfileCatalog.Price, BaseTime);
            _ = store.BeginCredentialEnrollment(
                "profile-delete", "generation-delete", "Delete restart", BaseTime.AddSeconds(1),
                "account-1", "billing-1");
            CredentialHelperCoordinator coordinator = new(store, launcher);
            (_, CredentialProfileProjection enrolled) = await coordinator.ExecuteCredentialTransitionAsync(
                "delete-enroll-attempt",
                CredentialBootstrap("profile-delete", "generation-delete", 83),
                CredentialAssignment(
                    "profile-delete", "generation-delete", HelperAssignmentKindV2.Enroll, "delete-enroll"),
                BaseTime.AddSeconds(2));
            Assert.AreEqual("active-unverified", enrolled.LifecycleState);
            (_, CredentialProfileProjection verified) = await coordinator.ExecuteCredentialTransitionAsync(
                "delete-verify-attempt",
                CredentialBootstrap("profile-delete", "generation-delete", 84),
                CredentialAssignment(
                    "profile-delete", "generation-delete", HelperAssignmentKindV2.Verify, "delete-verify"),
                BaseTime.AddSeconds(4));
            Assert.AreEqual("active-verified", verified.LifecycleState);

            await Assert.ThrowsExactlyAsync<IOException>(() => coordinator.ExecuteCredentialTransitionWithFaultAsync(
                "delete-crash-attempt",
                CredentialBootstrap("profile-delete", "generation-delete", 85),
                CredentialAssignment(
                    "profile-delete", "generation-delete", HelperAssignmentKindV2.Delete, "delete-crash"),
                BaseTime.AddSeconds(6),
                CredentialLifecycleFaultPoint.AfterDeletePendingBeforeHelper));
            CredentialProfileProjection pending = store.GetCredentialProfile("profile-delete");
            Assert.AreEqual("delete-pending", pending.LifecycleState);
            Assert.AreEqual("pending", pending.CleanupDisposition);
            Assert.AreEqual(1, pending.RevocationEpoch);
        }

        using AuthoritativeStore restarted = new(new StoragePaths(productRoot));
        CredentialProfileProjection restartedPending = restarted.GetCredentialProfile("profile-delete");
        Assert.AreEqual("delete-pending", restartedPending.LifecycleState);
        Assert.AreEqual(1, restartedPending.RevocationEpoch);
        CredentialHelperCoordinator recovery = new(restarted, launcher);

        HelperPrivateFrameV2 malformedDelete = CredentialAssignment(
            "profile-delete", "generation-delete", HelperAssignmentKindV2.Delete, "delete-malformed");
        malformedDelete.Sequence = 9;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => recovery.ExecuteCredentialTransitionAsync(
            "delete-malformed-attempt",
            CredentialBootstrap("profile-delete", "generation-delete", 86),
            malformedDelete,
            BaseTime.AddSeconds(8)));
        CredentialProfileProjection failed = restarted.GetCredentialProfile("profile-delete");
        Assert.AreEqual("delete-pending", failed.LifecycleState);
        Assert.AreEqual("failed", failed.CleanupDisposition);
        Assert.AreEqual(1, failed.RevocationEpoch);

        launcher.ArmExactDeleteFailure("profile-delete", "generation-delete");
        (CoordinatedHelperReceipt unavailableReceipt, CredentialProfileProjection unavailable) =
            await recovery.ExecuteCredentialTransitionAsync(
                "delete-unavailable-attempt",
                CredentialBootstrap("profile-delete", "generation-delete", 87),
                CredentialAssignment(
                    "profile-delete", "generation-delete", HelperAssignmentKindV2.Delete, "delete-unavailable"),
                BaseTime.AddSeconds(10));
        Assert.AreEqual(HelperOutcomeV2.Unavailable, unavailableReceipt.Process.Receipt.Outcome);
        Assert.AreEqual("delete-pending", unavailable.LifecycleState);
        Assert.AreEqual("failed", unavailable.CleanupDisposition);
        Assert.AreEqual(1, unavailable.RevocationEpoch);

        (CoordinatedHelperReceipt helperReceipt, CredentialProfileProjection deleted) =
            await recovery.ExecuteCredentialTransitionAsync(
                "delete-restart-attempt",
                CredentialBootstrap("profile-delete", "generation-delete", 88),
                CredentialAssignment(
                    "profile-delete", "generation-delete", HelperAssignmentKindV2.Delete, "delete-restart"),
                BaseTime.AddSeconds(12));
        Assert.AreEqual(HelperOutcomeV2.Completed, helperReceipt.Process.Receipt.Outcome);
        Assert.AreEqual("deleted", deleted.LifecycleState);
        Assert.AreEqual("confirmed", deleted.CleanupDisposition);
        Assert.AreEqual(1, deleted.RevocationEpoch);

        using System.Text.Json.JsonDocument secureStore = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(fakeStoreRoot, "synthetic-secure-store.v1.json")));
        Assert.AreEqual(0, secureStore.RootElement.GetProperty("Values").EnumerateObject().Count());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task CredentialDeleteCommitsDeletedOnlyAfterConfirmedAbsenceInSameCall()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-CredentialDeleteComplete-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        store.PublishProviderCatalog(OpenAiProviderProfileCatalog.Capability, OpenAiProviderProfileCatalog.Price, BaseTime);
        _ = store.BeginCredentialEnrollment(
            "profile-delete-complete", "generation-delete-complete", "Delete complete", BaseTime.AddSeconds(1),
            "account-1", "billing-1");
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        CredentialHelperCoordinator coordinator = new(store, Launcher(helper));
        _ = await coordinator.ExecuteCredentialTransitionAsync(
            "delete-complete-enroll-attempt",
            CredentialBootstrap("profile-delete-complete", "generation-delete-complete", 89),
            CredentialAssignment(
                "profile-delete-complete", "generation-delete-complete",
                HelperAssignmentKindV2.Enroll, "delete-complete-enroll"),
            BaseTime.AddSeconds(2));
        _ = await coordinator.ExecuteCredentialTransitionAsync(
            "delete-complete-verify-attempt",
            CredentialBootstrap("profile-delete-complete", "generation-delete-complete", 90),
            CredentialAssignment(
                "profile-delete-complete", "generation-delete-complete",
                HelperAssignmentKindV2.Verify, "delete-complete-verify"),
            BaseTime.AddSeconds(4));

        (CoordinatedHelperReceipt receipt, CredentialProfileProjection deleted) =
            await coordinator.ExecuteCredentialTransitionAsync(
                "delete-complete-attempt",
                CredentialBootstrap("profile-delete-complete", "generation-delete-complete", 91),
                CredentialAssignment(
                    "profile-delete-complete", "generation-delete-complete",
                    HelperAssignmentKindV2.Delete, "delete-complete"),
                BaseTime.AddSeconds(6));
        Assert.AreEqual(HelperOutcomeV2.Completed, receipt.Process.Receipt.Outcome);
        Assert.AreEqual("deleted", deleted.LifecycleState);
        Assert.AreEqual("confirmed", deleted.CleanupDisposition);
        Assert.AreEqual(1, deleted.RevocationEpoch);
        Assert.IsNull(deleted.AccountIdentityId);
        Assert.IsNull(deleted.BillingScopeIdentityId);
        Assert.IsNull(deleted.CapabilitySnapshotId);
    }

    [TestMethod]
    public void AtomicCredentialReplacementCreatesFreshGenerationAndMakesPredecessorIneligible()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Replacement-Atomic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string product = Path.Combine(root, "product");
        try
        {
            BackupArtifact backup;
            using (AuthoritativeStore store = new(new StoragePaths(product)))
            {
                store.PublishProviderCatalog(OpenAiProviderProfileCatalog.Capability, OpenAiProviderProfileCatalog.Price, BaseTime);
                _ = store.BeginCredentialEnrollment("profile-atomic", "generation-1", "Atomic", BaseTime.AddSeconds(1),
                    "account-1", "billing-1");
                _ = Transition(store, "atomic-enroll", "profile-atomic", "generation-1", "enroll",
                    "pending-enrollment", "active-unverified", BaseTime.AddSeconds(2));
                _ = Transition(store, "atomic-verify", "profile-atomic", "generation-1", "verify",
                    "active-unverified", "active-verified", BaseTime.AddSeconds(4));

                CredentialProfileProjection replacing = store.BeginCredentialReplacement(
                    "atomic-replace", "profile-atomic", "generation-1", "generation-2", 2,
                    BaseTime.AddSeconds(6));
                Assert.AreEqual("generation-1", replacing.GenerationId);
                Assert.AreEqual("replacing", replacing.LifecycleState);
                Assert.AreEqual("unavailable", replacing.VerificationState);
                Assert.IsTrue(store.CredentialGenerationExists("profile-atomic", "generation-2"));
                Assert.IsFalse(store.CredentialGenerationExists("profile-atomic", "generation-3"));
                Assert.ThrowsExactly<InvalidOperationException>(() => store.BeginCredentialReplacement(
                    "atomic-replace-stale", "profile-atomic", "generation-1", "generation-3", 3,
                    BaseTime.AddSeconds(8)));
                Assert.IsFalse(store.CredentialGenerationExists("profile-atomic", "generation-3"));
                backup = store.CreateBackup("AtomicReplacement", BaseTime.AddSeconds(10));
            }
            string restoredRoot = Path.Combine(root, "restored");
            using (StoragePaths restoredPaths = new(restoredRoot))
            {
                AuthoritativeStore.RestoreBackup(backup, restoredPaths);
            }
            using (AuthoritativeStore restored = new(new StoragePaths(restoredRoot)))
            {
                CredentialProfileProjection restoredProjection = restored.GetCredentialProfile("profile-atomic");
                Assert.AreEqual("recovery-required", restoredProjection.LifecycleState);
                Assert.IsTrue(restored.CredentialGenerationExists("profile-atomic", "generation-2"));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void CredentialIntentLifecyclePersistsReplacementRevocationRecoveryAndBackupReauthentication()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-CredentialLifecycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "credential-state")));
        store.PublishProviderCatalog(OpenAiProviderProfileCatalog.Capability, OpenAiProviderProfileCatalog.Price, BaseTime);
        CredentialProfileProjection pending = store.BeginCredentialEnrollment(
            "profile-life", "generation-1", "Synthetic", BaseTime.AddSeconds(1), "account-1", "billing-1");
        Assert.AreEqual("pending-enrollment", pending.LifecycleState);

        CredentialProfileProjection active = Transition(store, "activate-1", "profile-life", "generation-1",
            "enroll", "pending-enrollment", "active-unverified", BaseTime.AddSeconds(2));
        Assert.AreEqual("active-unverified", active.LifecycleState);
        CredentialProfileProjection verified = Transition(store, "verify-1", "profile-life", "generation-1",
            "verify", "active-unverified", "active-verified", BaseTime.AddSeconds(4));
        Assert.AreEqual("active-verified", verified.LifecycleState);

        store.AddCredentialGeneration("profile-life", "generation-2", 2, verified.RevocationEpoch, BaseTime.AddSeconds(6));
        CredentialProfileProjection replacing = Transition(store, "replace-1", "profile-life", "generation-1",
            "replace", "active-verified", "replacing", BaseTime.AddSeconds(7));
        Assert.AreEqual("replacing", replacing.LifecycleState);
        CredentialProfileProjection replacement = Transition(store, "replace-2", "profile-life", "generation-2",
            "replace", "replacing", "active-unverified", BaseTime.AddSeconds(9));
        Assert.AreEqual("generation-2", replacement.GenerationId);
        CredentialProfileProjection secondVerified = Transition(store, "verify-2", "profile-life", "generation-2",
            "verify", "active-unverified", "active-verified", BaseTime.AddSeconds(11));
        CredentialProfileProjection disabled = Transition(store, "disable-1", "profile-life", "generation-2",
            "disable", secondVerified.LifecycleState, "disabled", BaseTime.AddSeconds(13));
        Assert.AreEqual("disabled", disabled.LifecycleState);

        CredentialProfileProjection deletePending = Transition(store, "delete-1", "profile-life", "generation-2",
            "delete", "disabled", "delete-pending", BaseTime.AddSeconds(15), incrementRevocation: true);
        Assert.AreEqual(1, deletePending.RevocationEpoch);
        CredentialProfileProjection deleted = Transition(store, "delete-2", "profile-life", "generation-2",
            "delete", "delete-pending", "deleted", BaseTime.AddSeconds(17));
        Assert.AreEqual("deleted", deleted.LifecycleState);
        Assert.IsNull(deleted.AccountIdentityId);

        store.BeginCredentialEnrollment(
            "profile-recover", "generation-r1", "Recovery", BaseTime.AddSeconds(20), "account-1", "billing-1");
        CredentialProfileProjection unavailable = store.ApplyCredentialTransition(new(
            "unavailable-1", "profile-recover", "generation-r1", "enroll", "pending-enrollment",
            "secure-store-unavailable", "secure-store-unavailable", OpenAiProviderProfileCatalog.Capability.Identity.Value,
            "account-1", "billing-1", BaseTime.AddSeconds(21), BaseTime.AddSeconds(22), SecureStoreUnavailable: true));
        Assert.AreEqual("secure-store-unavailable", unavailable.LifecycleState);
        CredentialProfileProjection recovered = Transition(store, "recover-1", "profile-recover", "generation-r1",
            "recover", "secure-store-unavailable", "active-unverified", BaseTime.AddSeconds(23));
        Assert.AreEqual("active-unverified", recovered.LifecycleState);

        BackupArtifact backup = store.CreateBackup("CredentialSynthetic", BaseTime.AddSeconds(30));
        string restoreRoot = Path.Combine(root, "credential-restored");
        StoragePaths restoredPaths = new(restoreRoot);
        AuthoritativeStore.RestoreBackup(backup, restoredPaths);
        using AuthoritativeStore restored = new(new StoragePaths(restoreRoot));
        Assert.AreEqual("deleted", restored.GetCredentialProfile("profile-life").LifecycleState);
        CredentialProfileProjection recoveryRequired = restored.GetCredentialProfile("profile-recover");
        Assert.AreEqual("recovery-required", recoveryRequired.LifecycleState);
        DateTimeOffset restoredNow = recoveryRequired.UpdatedAt.AddSeconds(1);
        Microsoft.Data.Sqlite.SqliteException sameGeneration =
            Assert.ThrowsExactly<Microsoft.Data.Sqlite.SqliteException>(() => Transition(
            restored, "restore-same-generation-rejected", "profile-recover", "generation-r1",
            "recover", "recovery-required", "active-unverified", restoredNow));
        Assert.AreEqual(19, sameGeneration.SqliteErrorCode);
        Assert.AreEqual(1811, sameGeneration.SqliteExtendedErrorCode);
        Assert.AreEqual(
            "SQLite Error 19: 'restored credential recovery cannot reactivate the restored generation'.",
            sameGeneration.Message);
        restored.AddCredentialGeneration(
            "profile-recover", "generation-r2", recoveryRequired.GenerationOrdinal + 1,
            recoveryRequired.RevocationEpoch, restoredNow.AddSeconds(2));
        CredentialProfileProjection reentered = Transition(
            restored, "restore-fresh-reentry", "profile-recover", "generation-r2",
            "recover", "recovery-required", "active-unverified", restoredNow.AddSeconds(4));
        Assert.AreEqual("generation-r2", reentered.GenerationId);
        Assert.IsGreaterThan(recoveryRequired.GenerationOrdinal, reentered.GenerationOrdinal);
        // Secrets are never in the backup; restored metadata must be reauthenticated.
        string manifest = File.ReadAllText(backup.ManifestPath);
        Assert.IsFalse(manifest.Contains("synthetic-canary", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RestoreAdvancesRecoveryTransitionBeyondFutureCredentialAuthorityTime()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-CredentialRestoreClock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        DateTimeOffset futureAuthority = DateTimeOffset.UtcNow.AddHours(1);
        BackupArtifact backup;
        CredentialProfileProjection active;
        using (AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "credential-state"))))
        {
            store.PublishProviderCatalog(OpenAiProviderProfileCatalog.Capability, OpenAiProviderProfileCatalog.Price, futureAuthority);
            _ = store.BeginCredentialEnrollment(
                "profile-future", "generation-future-1", "Future authority",
                futureAuthority.AddSeconds(1), "account-future", "billing-future");
            DateTimeOffset activationAt = futureAuthority.AddSeconds(2);
            active = store.ApplyCredentialTransition(new(
                "activate-future", "profile-future", "generation-future-1",
                "enroll", "pending-enrollment", "active-unverified", "active-unverified",
                OpenAiProviderProfileCatalog.Capability.Identity.Value, "account-future", "billing-future",
                activationAt, activationAt.AddTicks(1)));

            DateTimeOffset regressedWallClock = DateTimeOffset.UtcNow;
            Microsoft.Data.Sqlite.SqliteException reproduced =
                Assert.ThrowsExactly<Microsoft.Data.Sqlite.SqliteException>(() => store.ApplyCredentialTransition(new(
                    "restore-clock-regression", "profile-future", "generation-future-1",
                    "recover", "active-unverified", "recovery-required", "recovery-required",
                    OpenAiProviderProfileCatalog.Capability.Identity.Value, "account-future", "billing-future",
                    regressedWallClock, regressedWallClock.AddTicks(1), IncrementRevocationEpoch: true)));
            Assert.AreEqual(19, reproduced.SqliteErrorCode);
            Assert.AreEqual(1811, reproduced.SqliteExtendedErrorCode);
            Assert.AreEqual(
                "SQLite Error 19: 'provider credential lifecycle time regression'.",
                reproduced.Message);

            backup = store.CreateBackup("CredentialFutureAuthority", futureAuthority.AddSeconds(10));
        }

        StoragePaths restoredPaths = new(Path.Combine(root, "credential-restored"));
        AuthoritativeStore.RestoreBackup(backup, restoredPaths);
        using AuthoritativeStore restored = new(restoredPaths);
        CredentialProfileProjection recoveryRequired = restored.GetCredentialProfile("profile-future");
        Assert.AreEqual("recovery-required", recoveryRequired.LifecycleState);
        Assert.IsGreaterThan(active.UpdatedAt, recoveryRequired.UpdatedAt);
        Assert.AreEqual(active.RevocationEpoch + 1, recoveryRequired.RevocationEpoch);

        restored.AddCredentialGeneration(
            "profile-future", "generation-future-2", recoveryRequired.GenerationOrdinal + 1,
            recoveryRequired.RevocationEpoch, recoveryRequired.UpdatedAt.AddTicks(1));
        DateTimeOffset reauthenticationAt = recoveryRequired.UpdatedAt.AddTicks(2);
        CredentialProfileProjection reauthenticated = restored.ApplyCredentialTransition(new(
            "restore-future-fresh-reentry", "profile-future", "generation-future-2",
            "recover", "recovery-required", "active-unverified", "active-unverified",
            OpenAiProviderProfileCatalog.Capability.Identity.Value, "account-future", "billing-future",
            reauthenticationAt, reauthenticationAt.AddTicks(1)));
        Assert.AreEqual("generation-future-2", reauthenticated.GenerationId);
        Assert.AreEqual("active-unverified", reauthenticated.LifecycleState);
    }

    private static CredentialProfileProjection Transition(
        AuthoritativeStore store, string root, string profile, string generation,
        string kind, string from, string to, DateTimeOffset pendingAt, bool incrementRevocation = false)
    {
        bool noMetadata = to == "deleted";
        return store.ApplyCredentialTransition(new(
            root, profile, generation, kind, from, to, to,
            noMetadata ? null : OpenAiProviderProfileCatalog.Capability.Identity.Value,
            noMetadata ? null : "account-1", noMetadata ? null : "billing-1",
            pendingAt, pendingAt.AddSeconds(1), IncrementRevocationEpoch: incrementRevocation));
    }

    private static OneShotCredentialHelperLauncher Launcher(string helper) => new(
        helper,
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(helper))),
        Path.Combine(Path.GetTempPath(), "Infinium-CredentialFakeStore-" + Guid.NewGuid().ToString("N")));

    private static (int Secret, int Target) ScanCanaries(
        string root,
        ReadOnlySpan<byte> secret,
        ReadOnlySpan<byte> target)
    {
        int secretMatches = 0;
        int targetMatches = 0;
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            byte[] bytes = File.ReadAllBytes(path);
            secretMatches += bytes.AsSpan().IndexOf(secret) >= 0 ? 1 : 0;
            targetMatches += bytes.AsSpan().IndexOf(target) >= 0 ? 1 : 0;
        }
        return (secretMatches, targetMatches);
    }

    private static HelperPrivateFrameV2 CredentialBootstrap(string profile, string generation, byte nonce)
    {
        HelperPrivateFrameV2 frame = HelperTestFrames.Bootstrap(nonceSeed: nonce);
        frame.Bootstrap.Credential.AccessProfileId.Value = profile;
        frame.Bootstrap.Credential.GenerationId.Value = generation;
        return frame;
    }

    private static HelperPrivateFrameV2 CredentialAssignment(
        string profile, string generation, HelperAssignmentKindV2 kind, string identity)
    {
        HelperPrivateFrameV2 frame = HelperTestFrames.Assignment(kind);
        frame.Assignment.AccessProfileId.Value = profile;
        frame.Assignment.GenerationId.Value = generation;
        frame.Assignment.Credential.AccessProfileId.Value = profile;
        frame.Assignment.Credential.GenerationId.Value = generation;
        frame.Assignment.AssignmentId = identity;
        frame.Assignment.CommandId = "command-1";
        return frame;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SECURITY_ATTRIBUTES
    {
        public int Length;
        public nint SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)] public bool InheritHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateEventW(
        ref SECURITY_ATTRIBUTES attributes,
        [MarshalAs(UnmanagedType.Bool)] bool manualReset,
        [MarshalAs(UnmanagedType.Bool)] bool initialState,
        string? name);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}
