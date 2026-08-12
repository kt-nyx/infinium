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
    public async Task HelperPrivateHandleLaunchesExactRepositoryBinaryWithoutStandardProtocolOrRetry()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        Assert.IsTrue(File.Exists(helper), helper);
        OneShotCredentialHelperLauncher launcher = new(helper);
        HelperProcessReceipt result = await launcher.ExecuteAsync(
            HelperTestFrames.Bootstrap(), HelperTestFrames.Assignment(), null, TimeSpan.FromSeconds(20));
        Assert.AreEqual(0, result.ExitCode);
        Assert.AreEqual(HelperOutcomeV2.Completed, result.Receipt.Outcome);
        Assert.AreEqual(2, result.InheritedPrivateHandleCount);
        Assert.AreEqual(0, result.StandardProtocolHandleCount);
        Assert.AreEqual(0, result.ListenerCount);
        Assert.AreEqual(0, result.NetworkOperationCount);
        Assert.AreEqual(0, result.NativeCredentialOperationCount);
        Assert.IsTrue(result.ProcessTreeTerminated);
        Assert.IsFalse(result.RetryAttempted);
        Assert.AreEqual(64, result.BinarySha256.Length);

        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp3-Staging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using AuthoritativeStore store = new(new StoragePaths(Path.Combine(root, "product")));
        CredentialHelperCoordinator coordinator = new(store, launcher);
        CoordinatedHelperReceipt coordinated = await coordinator.ExecuteStageAndAdmitAsync(
            "helper-attempt-1", HelperTestFrames.Bootstrap(), HelperTestFrames.Assignment(), null, BaseTime);
        Assert.IsTrue(coordinated.Staging.StagedBeforeAdmission);
        Assert.IsTrue(coordinated.Staging.CoordinatorOnlyAdmission);
        Assert.IsTrue(File.Exists(Path.Combine(store.Paths.Staging, coordinated.Staging.RelativePath)));
    }

    [TestMethod]
    public async Task CredentialDispatchRequiresFinalGenerationRevocationDeadlineAndBudgetRevalidation()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = new(helper);
        HelperProcessReceipt result = await launcher.ExecuteAsync(
            HelperTestFrames.DispatchBootstrap(), HelperTestFrames.DispatchAssignment(),
            HelperTestFrames.Revalidation(), TimeSpan.FromSeconds(20));
        Assert.AreEqual(HelperOutcomeV2.FailedKnown, result.Receipt.Outcome);
        Assert.IsFalse(result.Receipt.TransportMayHaveStarted);
        Assert.IsFalse(result.RetryAttempted);

        HelperPrivateFrameV2 stale = HelperTestFrames.Revalidation();
        stale.DispatchRevalidation.RevocationEpoch = 1;
        HelperProcessReceipt rejected = await launcher.ExecuteAsync(
            HelperTestFrames.DispatchBootstrap(), HelperTestFrames.DispatchAssignment(), stale, TimeSpan.FromSeconds(20));
        Assert.AreEqual(HelperOutcomeV2.FailedKnown, rejected.Receipt.Outcome);
        Assert.IsFalse(rejected.Receipt.TransportMayHaveStarted);
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
        _ = Transition(store, "verify-2", "profile-life", "generation-2",
            "verify", "active-unverified", "active-verified", BaseTime.AddSeconds(11));

        CredentialProfileProjection deletePending = Transition(store, "delete-1", "profile-life", "generation-2",
            "delete", "active-verified", "delete-pending", BaseTime.AddSeconds(13), incrementRevocation: true);
        Assert.AreEqual(1, deletePending.RevocationEpoch);
        CredentialProfileProjection deleted = Transition(store, "delete-2", "profile-life", "generation-2",
            "delete", "delete-pending", "deleted", BaseTime.AddSeconds(15));
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
        Assert.AreEqual("active-unverified", restored.GetCredentialProfile("profile-recover").LifecycleState);
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
}
