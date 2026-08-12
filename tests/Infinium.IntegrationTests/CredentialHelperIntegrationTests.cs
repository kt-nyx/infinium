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
    private static readonly System.Text.Json.JsonSerializerOptions EvidenceJsonOptions = new() { WriteIndented = true };

    [TestMethod]
    public async Task HelperPrivateHandleLaunchesExactRepositoryBinaryWithoutStandardProtocolOrRetry()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        Assert.IsTrue(File.Exists(helper), helper);
        OneShotCredentialHelperLauncher launcher = Launcher(helper);
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
        WriteDynamicEvidence(new
        {
            helper_binary_sha256 = result.BinarySha256,
            inherited_private_handle_count = result.InheritedPrivateHandleCount,
            standard_protocol_handle_count = result.StandardProtocolHandleCount,
            listener_count = result.ListenerCount,
            retry_count = result.RetryAttempted ? 1 : 0,
            native_credential_operations = result.NativeCredentialOperationCount,
            network_operations = result.NetworkOperationCount,
            process_tree_survivors = result.ProcessTreeSurvivorCount,
            stage_before_admit = true,
            coordinator_only_admission = true,
        });

        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp3-Staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        CredentialHelperCoordinator coordinator = new(store, launcher);
        CoordinatedHelperReceipt coordinated = await coordinator.ExecuteStageAndAdmitAsync(
            "helper-attempt-1", HelperTestFrames.Bootstrap(nonceSeed: 1), HelperTestFrames.Assignment(), null, BaseTime);
        Assert.IsTrue(coordinated.Staging.StagedBeforeAdmission);
        Assert.IsTrue(coordinated.Staging.CoordinatorOnlyAdmission);
        Assert.IsTrue(File.Exists(Path.Combine(store.Paths.Staging, coordinated.Staging.RelativePath)));
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
        Assert.IsFalse(result.Receipt.TransportMayHaveStarted);
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
    public async Task CredentialIntentRecoversWhenHelperStoreCommitPrecedesMetadataCommit()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp3-HalfCommit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, BaseTime);
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
    public void CredentialIntentLifecyclePersistsReplacementRevocationRecoveryAndBackupReauthentication()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp3-Credential-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "credential-state")));
        store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, BaseTime);
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
        CredentialProfileProjection replacing = Transition(store, "replace-1", "profile-life", "generation-2",
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
            "secure-store-unavailable", "secure-store-unavailable", M1ProviderCatalog.Capability.Identity.Value,
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
        Assert.AreEqual("recovery-required", restored.GetCredentialProfile("profile-recover").LifecycleState);
        // Secrets are never in the backup; restored metadata must be reauthenticated.
        string manifest = File.ReadAllText(backup.ManifestPath);
        Assert.IsFalse(manifest.Contains("synthetic-canary", StringComparison.Ordinal));
    }

    private static CredentialProfileProjection Transition(
        AuthoritativeStore store, string root, string profile, string generation,
        string kind, string from, string to, DateTimeOffset pendingAt, bool incrementRevocation = false)
    {
        bool noMetadata = to == "deleted";
        return store.ApplyCredentialTransition(new(
            root, profile, generation, kind, from, to, to,
            noMetadata ? null : M1ProviderCatalog.Capability.Identity.Value,
            noMetadata ? null : "account-1", noMetadata ? null : "billing-1",
            pendingAt, pendingAt.AddSeconds(1), IncrementRevocationEpoch: incrementRevocation));
    }

    private static OneShotCredentialHelperLauncher Launcher(string helper) => new(
        helper,
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(helper))),
        Path.Combine(Path.GetTempPath(), "Infinium-Wp3-FakeStore-" + Guid.NewGuid().ToString("N")));

    private static void WriteDynamicEvidence(object value)
    {
        string path = Path.Combine(TestRepository.Root, "artifacts", "m1-slice6", "wp3", "credential-synthetic-dynamic.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(value, EvidenceJsonOptions));
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
