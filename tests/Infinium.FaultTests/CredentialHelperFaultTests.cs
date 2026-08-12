using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.CredentialHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialHelperFaultTests
{
    [TestMethod]
    public async Task HelperUnavailableStoreReturnsTypedTerminalWithoutNativeFallback()
    {
        using DeterministicFakeSecureStore store = new() { Available = false };
        OneShotHelperEngine engine = new(store);
        using MemoryStream request = await CredentialRequestAsync(HelperAssignmentKindV2.Verify);
        using MemoryStream response = new();
        await engine.RunAsync(request, response, CancellationToken.None);
        HelperPrivateFrameV2 terminal = HelperPrivateProtocolV2.Decode(response.ToArray(), 3);
        Assert.AreEqual(HelperOutcomeV2.Unavailable, terminal.Receipt.Outcome);
        Assert.IsFalse(terminal.Receipt.TransportMayHaveStarted);
        Assert.AreEqual(0, DeterministicFakeSecureStore.NativeOperationCount);
    }

    [TestMethod]
    public async Task HelperCrashOrMalformedBootstrapTerminatesOneProcessTreeWithoutRetry()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = new(helper);
        HelperPrivateFrameV2 malformedAssignment = HelperTestFrames.Assignment();
        malformedAssignment.Assignment.AssignmentKind = HelperAssignmentKindV2.Unspecified;
        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => launcher.ExecuteAsync(
            HelperTestFrames.Bootstrap(), malformedAssignment, null, TimeSpan.FromSeconds(10)));
    }

    [TestMethod]
    public async Task CredentialStaleGenerationAndRevocationNeverStartsTransportOrRetries()
    {
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        OneShotCredentialHelperLauncher launcher = new(helper);
        HelperPrivateFrameV2 stale = HelperTestFrames.Revalidation();
        stale.DispatchRevalidation.GenerationId.Value = "stale-generation";
        stale.DispatchRevalidation.RevocationEpoch = 99;
        HelperProcessReceipt receipt = await launcher.ExecuteAsync(
            HelperTestFrames.DispatchBootstrap(), HelperTestFrames.DispatchAssignment(), stale, TimeSpan.FromSeconds(10));
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
}
