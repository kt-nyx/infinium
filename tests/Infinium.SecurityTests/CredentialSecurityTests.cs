using System.Text;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.CredentialHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialSecurityTests
{
    private static readonly string[] ExpectedSecureStoreMethods =
        ["DeleteExact", "ReadExact", "VerifyExact", "WriteExact"];

    [TestMethod]
    public async Task SecretCanaryNeverCrossesHelperPrivateProtocolReceiptOrDiagnostics()
    {
        byte[] canary = Encoding.UTF8.GetBytes("WP3-SECRET-CANARY-DO-NOT-RETAIN");
        using DeterministicFakeSecureStore store = new();
        store.WriteExact(new("profile-1", "generation-1"), canary);
        OneShotHelperEngine engine = new(store);
        using MemoryStream request = new();
        await HelperPrivateProtocolV2.WriteAsync(request, HelperTestFrames.Bootstrap(), CancellationToken.None);
        await HelperPrivateProtocolV2.WriteAsync(
            request, HelperTestFrames.Assignment(HelperAssignmentKindV2.Verify), CancellationToken.None);
        request.Position = 0;
        using MemoryStream response = new();
        await engine.RunAsync(request, response, CancellationToken.None);
        byte[] bytes = response.ToArray();
        Assert.IsFalse(bytes.AsSpan().IndexOf(canary) >= 0);
        HelperPrivateFrameV2 terminal = HelperPrivateProtocolV2.Decode(bytes, 3);
        Assert.AreEqual(HelperOutcomeV2.Completed, terminal.Receipt.Outcome);
        Assert.IsFalse(terminal.ToString().Contains("CANARY", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CredentialHelperAuthorityHasNoEnumerationArbitraryTargetListenerShellOrAmbientSecretSurface()
    {
        string[] methods = typeof(ISyntheticSecureStore).GetMethods().Select(method => method.Name).Order().ToArray();
        CollectionAssert.AreEqual(ExpectedSecureStoreMethods, methods);
        Assert.IsFalse(methods.Any(name => name.Contains("Enumerate", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Target", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Reveal", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(0, DeterministicFakeSecureStore.NativeOperationCount);
        Assert.AreEqual(0, DeterministicFakeSecureStore.EnumerationCount);
        Assert.ThrowsExactly<ArgumentException>(() => new OneShotCredentialHelperLauncher("cmd.exe"));
    }

    [TestMethod]
    public void CredentialProtocolRejectsSecretAndTargetUnknownFieldsRecursively()
    {
        byte[] canonical = HelperPrivateProtocolV2.Encode(HelperTestFrames.Assignment());
        byte[] payload = canonical[4..];
        // Add unknown length-delimited field 90 to the nested assignment.
        byte[] hostileNested = [.. payload, 0xd2, 0x05, 0x01, 0x78];
        byte[] frame = new byte[hostileNested.Length + 4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(frame, checked((uint)hostileNested.Length));
        hostileNested.CopyTo(frame, 4);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperPrivateProtocolV2.Decode(frame, 2));
    }
}
