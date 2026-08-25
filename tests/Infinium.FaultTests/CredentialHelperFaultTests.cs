using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.CredentialHelper;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialHelperFaultTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] ExpectedRecoveredReplacementSlots =
        ["CAPABILITY-BOUND-STORE-TARGET-CANARY/profile-replace-fault/generation-2"];
    [TestMethod]
    public async Task HelperUnavailableStoreReturnsTypedTerminalWithoutNativeFallback()
    {
        using DeterministicFakeSecureStore store = new() { Available = false };
        OneShotHelperEngine engine = new(store, new FrozenTimeProvider(BaseTime));
        using MemoryStream request = await CredentialRequestAsync(HelperAssignmentKindV2.Verify);
        using MemoryStream response = new();
        await engine.RunAsync(request, response, CancellationToken.None);
        byte[] responseBytes = response.ToArray();
        int frameLength = checked(4 + (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(responseBytes));
        HelperPrivateFrameV2 terminal = HelperPrivateProtocolV2.Decode(responseBytes.AsSpan(0, frameLength), 3);
        Assert.AreEqual(HelperOutcomeV2.Unavailable, terminal.Receipt.Outcome);
        Assert.IsFalse(terminal.Receipt.TransportMayHaveStarted);
        Assert.AreEqual(0, DeterministicFakeSecureStore.NativeOperationCount);
    }

    [TestMethod]
    public async Task HelperCrashOrMalformedBootstrapTerminatesOneProcessTreeWithoutRetry()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = Launcher(helper);
        HelperPrivateFrameV2 malformedAssignment = HelperTestFrames.Assignment();
        malformedAssignment.Assignment.AssignmentKind = HelperAssignmentKindV2.Unspecified;
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => launcher.ExecuteAsync(
            HelperTestFrames.Bootstrap(), malformedAssignment, null, TimeSpan.FromSeconds(10), BaseTime));
    }

    [TestMethod]
    public async Task CredentialStaleGenerationAndRevocationNeverStartsTransportOrRetries()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = Launcher(helper);
        HelperPrivateFrameV2 stale = HelperTestFrames.Revalidation();
        stale.DispatchRevalidation.GenerationId.Value = "stale-generation";
        stale.DispatchRevalidation.RevocationEpoch = 99;
        HelperProcessReceipt receipt = await launcher.ExecuteAsync(
            HelperTestFrames.DispatchBootstrap(), HelperTestFrames.DispatchAssignment(), stale, TimeSpan.FromSeconds(10), BaseTime);
        Assert.AreEqual(HelperOutcomeV2.FailedKnown, receipt.Receipt.Outcome);
        Assert.IsFalse(receipt.Receipt.TransportMayHaveStarted);
        Assert.IsFalse(receipt.RetryAttempted);
        Assert.IsTrue(receipt.ProcessTreeTerminated);
    }

    [TestMethod]
    public async Task CredentialReplacementDeleteFaultRetainsExactCleanupAcrossRestartAndBackupUntilConfirmed()
    {
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Credential-ReplacementCleanup-" + Guid.NewGuid().ToString("N"));
        string productRoot = Path.Combine(root, "product");
        string fakeStoreRoot = Path.Combine(root, "fake-secure-store");
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = new(
            helper,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(helper))),
            fakeStoreRoot);
        BackupArtifact backup;
        using (AuthoritativeStore state = new(new StoragePaths(productRoot)))
        {
            state.PublishProviderCatalog(OpenAiProviderProfileCatalog.Capability, OpenAiProviderProfileCatalog.Price, BaseTime);
            _ = state.BeginCredentialEnrollment(
                "profile-replace-fault", "generation-1", "Replacement fault", BaseTime.AddSeconds(1),
                "account-1", "billing-1");
            CredentialHelperCoordinator coordinator = new(state, launcher);
            _ = await ExecuteLifecycle(
                coordinator, "enroll", HelperAssignmentKindV2.Enroll,
                "generation-1", "generation-1", 1, 71, BaseTime.AddSeconds(2));
            _ = await ExecuteLifecycle(
                coordinator, "verify", HelperAssignmentKindV2.Verify,
                "generation-1", "generation-1", 1, 72, BaseTime.AddSeconds(4));
            state.AddCredentialGeneration(
                "profile-replace-fault", "generation-2", 2, 0, BaseTime.AddSeconds(6));
            launcher.ArmExactDeleteFailure("profile-replace-fault", "generation-1");

            (CoordinatedHelperReceipt failedHelper, CredentialProfileProjection pending) = await ExecuteLifecycle(
                coordinator, "replace-fault", HelperAssignmentKindV2.Replace,
                "generation-1", "generation-2", 2, 73, BaseTime.AddSeconds(7));
            Assert.AreEqual(HelperOutcomeV2.Unavailable, failedHelper.Process.Receipt.Outcome);
            Assert.AreEqual("delete-pending", pending.LifecycleState);
            Assert.AreEqual("generation-1", pending.GenerationId);
            Assert.AreEqual("failed", pending.CleanupDisposition);

            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => ExecuteLifecycle(
                coordinator, "premature-recovery", HelperAssignmentKindV2.Recover,
                "generation-2", "generation-2", 2, 74, BaseTime.AddSeconds(9)));
            Assert.AreEqual("delete-pending", state.GetCredentialProfile("profile-replace-fault").LifecycleState);
            backup = state.CreateBackup("CredentialReplacementCleanup", BaseTime.AddSeconds(11));
        }

        using (AuthoritativeStore restarted = new(new StoragePaths(productRoot)))
        {
            CredentialProfileProjection persisted = restarted.GetCredentialProfile("profile-replace-fault");
            Assert.AreEqual("delete-pending", persisted.LifecycleState);
            Assert.AreEqual("generation-1", persisted.GenerationId);
            Assert.AreEqual("failed", persisted.CleanupDisposition);
        }

        StoragePaths restoredPaths = new(Path.Combine(root, "restored-product"));
        AuthoritativeStore.RestoreBackup(backup, restoredPaths);
        using AuthoritativeStore restored = new(restoredPaths);
        CredentialProfileProjection restoredPending = restored.GetCredentialProfile("profile-replace-fault");
        Assert.AreEqual("delete-pending", restoredPending.LifecycleState);
        Assert.AreEqual("generation-1", restoredPending.GenerationId);
        Assert.AreEqual("failed", restoredPending.CleanupDisposition);

        CredentialHelperCoordinator recoveryCoordinator = new(restored, launcher);
        (CoordinatedHelperReceipt recoveredHelper, CredentialProfileProjection recovered) = await ExecuteLifecycle(
            recoveryCoordinator, "cleanup-recovery", HelperAssignmentKindV2.Recover,
            "generation-1", "generation-2", 2, 75, BaseTime.AddSeconds(13));
        Assert.AreEqual(HelperOutcomeV2.Completed, recoveredHelper.Process.Receipt.Outcome);
        Assert.AreEqual("active-unverified", recovered.LifecycleState);
        Assert.AreEqual("generation-2", recovered.GenerationId);
        Assert.AreEqual("not-requested", recovered.CleanupDisposition);
        (_, CredentialProfileProjection verified) = await ExecuteLifecycle(
            recoveryCoordinator, "verify-recovered", HelperAssignmentKindV2.Verify,
            "generation-2", "generation-2", 2, 76, BaseTime.AddSeconds(15));
        Assert.AreEqual("active-verified", verified.LifecycleState);

        using System.Text.Json.JsonDocument secureStore = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(fakeStoreRoot, "synthetic-secure-store.v1.json")));
        string[] slots = secureStore.RootElement.GetProperty("Values").EnumerateObject()
            .Select(property => property.Name).ToArray();
        CollectionAssert.AreEqual(
            ExpectedRecoveredReplacementSlots,
            slots);
    }

    private static async Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)> ExecuteLifecycle(
        CredentialHelperCoordinator coordinator,
        string identity,
        HelperAssignmentKindV2 kind,
        string bootstrapGeneration,
        string assignmentGeneration,
        ulong generationOrdinal,
        byte nonce,
        DateTimeOffset now)
    {
        HelperPrivateFrameV2 bootstrap = HelperTestFrames.Bootstrap(nonceSeed: nonce);
        bootstrap.Bootstrap.Credential.AccessProfileId.Value = "profile-replace-fault";
        bootstrap.Bootstrap.Credential.GenerationId.Value = bootstrapGeneration;
        HelperPrivateFrameV2 assignment = HelperTestFrames.Assignment(kind);
        assignment.Assignment.AccessProfileId.Value = "profile-replace-fault";
        assignment.Assignment.GenerationId.Value = assignmentGeneration;
        assignment.Assignment.GenerationOrdinal = generationOrdinal;
        assignment.Assignment.Credential.AccessProfileId.Value = "profile-replace-fault";
        assignment.Assignment.Credential.GenerationId.Value = assignmentGeneration;
        assignment.Assignment.AssignmentId = identity + "-assignment";
        assignment.Assignment.CommandId = identity + "-command";
        bootstrap.Bootstrap.CommandId = assignment.Assignment.CommandId;
        return await coordinator.ExecuteCredentialTransitionAsync(
            identity + "-attempt", bootstrap, assignment, now);
    }

    private static async Task<MemoryStream> CredentialRequestAsync(HelperAssignmentKindV2 kind)
    {
        MemoryStream request = new();
        await HelperPrivateProtocolV2.WriteAsync(request, HelperTestFrames.Bootstrap(), CancellationToken.None);
        await HelperPrivateProtocolV2.WriteAsync(request, HelperTestFrames.Assignment(kind), CancellationToken.None);
        request.Position = 0;
        return request;
    }

    private static OneShotCredentialHelperLauncher Launcher(string helper) => new(
        helper,
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(helper))),
        Path.Combine(Path.GetTempPath(), "Infinium-Credential-FakeStore-" + Guid.NewGuid().ToString("N")));

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
