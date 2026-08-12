using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.CredentialHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialHelperFaultTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
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
        Path.Combine(Path.GetTempPath(), "Infinium-Wp3-FakeStore-" + Guid.NewGuid().ToString("N")));

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
