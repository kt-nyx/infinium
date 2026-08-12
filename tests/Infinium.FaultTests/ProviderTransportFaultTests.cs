using System.Text;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
namespace Infinium.Tests;

[TestClass]
[TestCategory("Fault")]
public sealed class ProviderTransportFaultTests
{
    [TestMethod]
    public async Task ProviderTransportOversizedResponseStopsAtExactBoundWithoutRetry()
    {
        await using ProviderLoopbackServer server = new(Encoding.UTF8.GetBytes(new string('x', 65)));
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        OpenAiResponsesResult result = await adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), Encoding.ASCII.GetBytes("sk-size"),
            ProviderAdapterTestData.Limits(responseBytes: 64), "client-size", CancellationToken.None);
        Assert.AreEqual(ProviderResponseState.Oversized, result.State);
        Assert.IsNull(result.RawResponseBytes);
        Assert.AreEqual(1, server.RequestCount);
        Assert.IsFalse(result.RetryPermitted);
        byte[] envelope = OpenAiStagedResponseEnvelope.Create(result);
        Assert.IsTrue(OpenAiStagedResponseEnvelope.TryRead(envelope, out byte[] raw, out byte[] headers));
        Assert.AreEqual(0, raw.Length);
        OpenAiResponsesResult replay = OpenAiStagedResponseEnvelope.Replay(raw, headers, "client-size");
        Assert.AreEqual(ProviderResponseState.Oversized, replay.State);
        Assert.AreEqual(UsageReceiptState.Partial, replay.Usage.ReceiptState);
        Assert.AreEqual(0, replay.SendCount);
        Assert.IsFalse(replay.NetworkUsed);
    }

    [TestMethod]
    public async Task ProviderTransportMalformedResponseIsRetainedAndRejected()
    {
        byte[] malformed = Encoding.UTF8.GetBytes("{not-json");
        await using ProviderLoopbackServer server = new(malformed);
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        OpenAiResponsesResult result = await adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), Encoding.ASCII.GetBytes("sk-malformed"),
            ProviderAdapterTestData.Limits(), "client-malformed", CancellationToken.None);
        Assert.AreEqual(ProviderResponseState.Malformed, result.State);
        CollectionAssert.AreEqual(malformed, result.RawResponseBytes!);
        Assert.IsFalse(result.Admitted);
    }

    [TestMethod]
    public async Task AmbiguousDispatchDeadlineRetainsNoRetryAndOneSend()
    {
        await using ProviderLoopbackServer server = new(
            ProviderAdapterTestData.CompletedResponse(), delay: TimeSpan.FromSeconds(2));
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        OpenAiResponsesResult result = await adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), Encoding.ASCII.GetBytes("sk-timeout"),
            ProviderAdapterTestData.Limits(deadlineMilliseconds: 100), "client-timeout", CancellationToken.None);
        Assert.AreEqual(ProviderResponseState.Unknown, result.State);
        Assert.IsTrue(result.TransportMayHaveStarted);
        Assert.IsFalse(result.RetryPermitted);
        Assert.AreEqual(1, result.SendCount);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), Encoding.ASCII.GetBytes("sk-retry"),
            ProviderAdapterTestData.Limits(), "client-retry", CancellationToken.None));
    }
}
