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
        ["ConsumeOneUseNonce", "DeleteExact", "ReadExact", "VerifyExact", "WriteExact"];

    [TestMethod]
    public async Task SecretCanaryNeverCrossesHelperPrivateProtocolReceiptOrDiagnostics()
    {
        byte[] canary = Encoding.UTF8.GetBytes("CREDENTIAL-SECRET-CANARY-DO-NOT-RETAIN");
        using DeterministicFakeSecureStore store = new();
        store.WriteExact(new("profile-1", "generation-1"), canary);
        OneShotHelperEngine engine = new(store, new FrozenTimeProvider(
            new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero)));
        using MemoryStream request = new();
        await HelperPrivateProtocolV2.WriteAsync(request, HelperTestFrames.Bootstrap(), CancellationToken.None);
        await HelperPrivateProtocolV2.WriteAsync(
            request, HelperTestFrames.Assignment(HelperAssignmentKindV2.Verify), CancellationToken.None);
        request.Position = 0;
        using MemoryStream response = new();
        await engine.RunAsync(request, response, CancellationToken.None);
        byte[] bytes = response.ToArray();
        Assert.IsFalse(bytes.AsSpan().IndexOf(canary) >= 0);
        int frameLength = checked(4 + (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        HelperPrivateFrameV2 terminal = HelperPrivateProtocolV2.Decode(bytes.AsSpan(0, frameLength), 3);
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
        Assert.ThrowsExactly<ArgumentException>(() => new OneShotCredentialHelperLauncher(
            "cmd.exe", new string('0', 64), Path.GetTempPath()));
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

    [TestMethod]
    public void CredentialHelperRejectsSameNameBinarySubstitutionByPinnedFingerprint()
    {
        string helper = TestRepository.PathFromRoot(
            "src", "Infinium.CredentialHelper", "bin", "Release", "net10.0", "Infinium.CredentialHelper.exe");
        string expected = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(helper)));
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Credential-Substitute-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string substitute = Path.Combine(root, "Infinium.CredentialHelper.exe");
        File.Copy(helper, substitute);
        using (FileStream stream = File.OpenWrite(substitute))
        {
            stream.Position = stream.Length;
            stream.WriteByte(0);
        }
        Assert.ThrowsExactly<ArgumentException>(() => new OneShotCredentialHelperLauncher(
            substitute, expected, Path.Combine(root, "store")));
    }

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
