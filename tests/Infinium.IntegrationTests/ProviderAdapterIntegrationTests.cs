using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.CredentialHelper;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class ProviderAdapterIntegrationTests
{
    private static readonly string[] AdmittedStates = ["completed-strict-profile-cache-zero"];
    private static readonly string[] RejectedStates =
        ["refusal", "incomplete", "failed", "cancelled", "malformed", "oversized", "mismatched", "unknown"];
    private static readonly string[] NonterminalStates = ["queued", "in-progress"];
    private static readonly string[] AmbiguousStates = ["deadline", "transport-error-after-send-start"];

    [TestMethod]
    public async Task ProviderAdapterLoopbackSendsExactBytesOnceAndRetainsProvenance()
    {
        byte[] response = ProviderAdapterTestData.CompletedResponse();
        await using ProviderLoopbackServer server = new(response, responseHeaders: new Dictionary<string, string>
        {
            ["x-request-id"] = "req_offline_1",
            ["x-ratelimit-limit-requests"] = "100",
            ["x-ratelimit-remaining-requests"] = "99",
        });
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        byte[] request = ProviderAdapterTestData.CanonicalRequest();
        byte[] secret = Encoding.ASCII.GetBytes("sk-offline-canary-never-retained");
        OpenAiResponsesResult result;
        try
        {
            result = await adapter.SendOnceAsync(request, secret, ProviderAdapterTestData.Limits(),
                "client-offline-1", CancellationToken.None);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        Assert.IsTrue(result.Admitted);
        Assert.AreEqual(1, result.SendCount);
        Assert.IsFalse(result.RetryPermitted);
        Assert.AreEqual("req_offline_1", result.ProviderRequestId);
        Assert.AreEqual("resp_offline_1", result.ProviderResponseId);
        Assert.AreEqual("POST", server.Method);
        Assert.AreEqual("/v1/responses", server.Path);
        CollectionAssert.AreEqual(request, server.RequestBody);
        Assert.AreEqual("application/json; charset=utf-8", server.RequestHeaders["Content-Type"]);
        Assert.AreEqual("application/json", server.RequestHeaders["Accept"]);
        Assert.AreEqual("client-offline-1", server.RequestHeaders["X-Client-Request-Id"]);
        Assert.AreEqual("Bearer sk-offline-canary-never-retained", server.RequestHeaders["Authorization"]);
        Assert.AreEqual(2, result.RateHeaders.Count);
        Assert.AreEqual(100L, result.RateHeaders.Single(header => header.Name == "x-ratelimit-limit-requests").Value);
        Assert.AreEqual(99L, result.RateHeaders.Single(header => header.Name == "x-ratelimit-remaining-requests").Value);
    }

    [TestMethod]
    public async Task SuccessorBearerPreservesALongCanonicalCredentialExactlyAndRejectsInvalidBytesBeforeSend()
    {
        string longCredential = "sk-proj-A_" + new string('B', 154);
        Assert.AreEqual(164, longCredential.Length);
        await using ProviderLoopbackServer server = new(ProviderAdapterTestData.CompletedResponse());
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        OpenAiResponsesResult result = await adapter.SendSuccessorV6OnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), Encoding.ASCII.GetBytes(longCredential),
            ProviderAdapterTestData.Limits(), "client-long-bearer", CancellationToken.None);

        Assert.IsTrue(result.Admitted);
        Assert.AreEqual("Bearer " + longCredential, server.RequestHeaders["Authorization"]);
        Assert.AreEqual(1, server.RequestCount);

        await using ProviderLoopbackServer whitespaceServer = new(ProviderAdapterTestData.CompletedResponse());
        using OpenAiResponsesAdapter whitespaceAdapter =
            OpenAiResponsesAdapter.CreateDeterministicLoopback(whitespaceServer.Endpoint);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => whitespaceAdapter.SendSuccessorV6OnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), "sk-proj-invalid value"u8.ToArray(),
            ProviderAdapterTestData.Limits(), "client-invalid-whitespace", CancellationToken.None));
        Assert.AreEqual(0, whitespaceServer.RequestCount);

        await using ProviderLoopbackServer nonAsciiServer = new(ProviderAdapterTestData.CompletedResponse());
        using OpenAiResponsesAdapter nonAsciiAdapter =
            OpenAiResponsesAdapter.CreateDeterministicLoopback(nonAsciiServer.Endpoint);
        byte[] nonAscii = [.. "sk-proj-valid"u8.ToArray(), 0xc3, 0xa9];
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => nonAsciiAdapter.SendSuccessorV6OnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), nonAscii,
            ProviderAdapterTestData.Limits(), "client-invalid-nonascii", CancellationToken.None));
        Assert.AreEqual(0, nonAsciiServer.RequestCount);

        await using ProviderLoopbackServer prefixServer = new(ProviderAdapterTestData.CompletedResponse());
        using OpenAiResponsesAdapter prefixAdapter =
            OpenAiResponsesAdapter.CreateDeterministicLoopback(prefixServer.Endpoint);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => prefixAdapter.SendSuccessorV6OnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), "not-a-provider-key"u8.ToArray(),
            ProviderAdapterTestData.Limits(), "client-invalid-prefix", CancellationToken.None));
        Assert.AreEqual(0, prefixServer.RequestCount);
    }

    [TestMethod]
    public async Task RetainedResponseReplayMatchesAdmissionWithNetworkDisabled()
    {
        byte[] response = ProviderAdapterTestData.CompletedResponse();
        await using ProviderLoopbackServer server = new(response, responseHeaders: new Dictionary<string, string>
        {
            ["x-request-id"] = "req-replay-1",
        });
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        OpenAiResponsesResult live = await adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), Encoding.ASCII.GetBytes("sk-replay"),
            ProviderAdapterTestData.Limits(), "client-replay-1", CancellationToken.None);
        OpenAiResponsesResult replay = OpenAiResponsesResponseCodec.Replay(
            live.RawResponseBytes!, live.HttpStatus!.Value, live.ClientRequestId!, live.ProviderRequestId,
            live.RateHeaders, live.RequestedOutputSchemaBytes!);

        Assert.AreEqual(live.State, replay.State);
        Assert.AreEqual(live.Admitted, replay.Admitted);
        Assert.AreEqual(live.Usage, replay.Usage);
        Assert.AreEqual(1, server.RequestCount);
        Assert.AreEqual(0, replay.SendCount);
        Assert.IsFalse(replay.NetworkUsed);
    }

    [TestMethod]
    public async Task ProviderOfflineUnavailableIsTypedAmbiguousAndNeverRetries()
    {
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(
            new Uri("http://127.0.0.1:1/v1/responses"));
        OpenAiResponsesResult result = await adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), Encoding.ASCII.GetBytes("sk-offline"),
            ProviderAdapterTestData.Limits(deadlineMilliseconds: 500), "client-offline", CancellationToken.None);
        Assert.IsTrue(result.TransportMayHaveStarted);
        Assert.IsFalse(result.Admitted);
        Assert.IsFalse(result.RetryPermitted);
        Assert.AreEqual(1, result.SendCount);
        Assert.IsTrue(
            result.ErrorCode is "transport_ambiguous" or "deadline_ambiguous",
            $"Unexpected typed offline outcome: {result.ErrorCode}");
        if (result.ErrorCode == "transport_ambiguous")
        {
            Assert.IsNotNull(result.ProviderErrorType);
            StringAssert.StartsWith(result.ProviderErrorType, "HttpRequestError.");
            StringAssert.Contains(result.ProviderErrorType, ".SocketError.");
            byte[] envelope = OpenAiStagedResponseEnvelope.Create(result);
            Assert.IsTrue(OpenAiStagedResponseEnvelope.TryRead(envelope, out byte[] raw, out byte[] headers));
            OpenAiResponsesResult replay = OpenAiStagedResponseEnvelope.ReplaySuccessorV6(
                raw, headers, "client-offline");
            Assert.AreEqual(result.ProviderErrorType, replay.ProviderErrorType);
        }
    }

    [TestMethod]
    public async Task ProviderHttpFailureRetainsSanitizedTypeCodeRequestIdAndResponseBytes()
    {
        byte[] response = JsonSerializer.SerializeToUtf8Bytes(new
        {
            error = new { type = "invalid_request_error", code = "invalid_value", message = "not retained separately" },
        });
        await using ProviderLoopbackServer server = new(response, statusCode: 400,
            responseHeaders: new Dictionary<string, string> { ["x-request-id"] = "req_http_failure_1" });
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        OpenAiResponsesResult result = await adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), Encoding.ASCII.GetBytes("sk-http-failure"),
            ProviderAdapterTestData.Limits(), "client-http-failure-1", CancellationToken.None);

        Assert.AreEqual(400, result.HttpStatus);
        Assert.AreEqual("invalid_request_error", result.ProviderErrorType);
        Assert.AreEqual("invalid_value", result.ErrorCode);
        Assert.AreEqual("req_http_failure_1", result.ProviderRequestId);
        CollectionAssert.AreEqual(response, result.RawResponseBytes!);
        Assert.IsFalse(result.Admitted);
        Assert.IsFalse(result.RetryPermitted);
        byte[] envelope = OpenAiStagedResponseEnvelope.Create(result);
        Assert.IsTrue(OpenAiStagedResponseEnvelope.TryRead(envelope, out byte[] raw, out byte[] headers));
        OpenAiResponsesResult replay = OpenAiStagedResponseEnvelope.Replay(raw, headers, "client-http-failure-1");
        Assert.AreEqual(result.ProviderErrorType, replay.ProviderErrorType);
        Assert.AreEqual(result.ErrorCode, replay.ErrorCode);
        Assert.AreEqual(result.ProviderRequestId, replay.ProviderRequestId);
    }

    [TestMethod]
    public void ProviderOfflineRejectsDnsNamesAndAlternatePathsBeforeTransport()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            OpenAiResponsesAdapter.CreateDeterministicLoopback(new Uri("http://localhost/v1/responses")));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            OpenAiResponsesAdapter.CreateDeterministicLoopback(new Uri("http://127.0.0.1/arbitrary")));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            OpenAiResponsesAdapter.CreateDeterministicLoopback(new Uri("https://127.0.0.1/v1/responses")));
    }

    [TestMethod]
    public async Task UnsupportedStrictSchemaIsRejectedBeforeTransportWithZeroSends()
    {
        await using ProviderLoopbackServer server = new(ProviderAdapterTestData.CompletedResponse());
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        string canonical = Encoding.UTF8.GetString(ProviderAdapterTestData.CanonicalRequest());
        byte[] unsupported = Encoding.UTF8.GetBytes(canonical.Replace(
            "\"type\":\"boolean\"", "\"futureKeyword\":true,\"type\":\"boolean\"", StringComparison.Ordinal));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => adapter.SendOnceAsync(
            unsupported, "sk-never-sent"u8.ToArray(), ProviderAdapterTestData.Limits(),
            "client-invalid-schema", CancellationToken.None));
        Assert.AreEqual(0, server.RequestCount);
    }

    [TestMethod]
    public async Task ResponseHeadersRetainOnlyTypedFiniteFactsAndExactSafeRequestIdentity()
    {
        const string canary = "sk-header-echo-must-not-be-retained";
        await using ProviderLoopbackServer server = new(
            ProviderAdapterTestData.CompletedResponse(), responseHeaders: new Dictionary<string, string>
            {
                ["x-request-id"] = "Bearer " + canary,
                ["x-ratelimit-limit-requests"] = "100",
                ["x-ratelimit-limit-tokens"] = "999999999999999999999999999999999999",
                ["x-ratelimit-remaining-tokens"] = canary,
                ["x-ratelimit-remaining-output-tokens"] = "-1",
                ["x-ratelimit-secret-echo"] = canary,
                ["openai-processing-ms"] = "120001",
            }, additionalResponseHeaders:
            [
                new("x-ratelimit-remaining-requests", "99"),
                new("x-ratelimit-remaining-requests", "98"),
            ]);
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        OpenAiResponsesResult result = await adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), Encoding.ASCII.GetBytes("sk-request-secret"),
            ProviderAdapterTestData.Limits(), "client-header-safety", CancellationToken.None);

        Assert.IsNull(result.ProviderRequestId);
        Assert.HasCount(1, result.RateHeaders);
        Assert.AreEqual("x-ratelimit-limit-requests", result.RateHeaders[0].Name);
        Assert.AreEqual(100L, result.RateHeaders[0].Value);
        byte[] envelope = OpenAiStagedResponseEnvelope.Create(result);
        Assert.IsFalse(Encoding.UTF8.GetString(envelope).Contains(canary, StringComparison.Ordinal));
        Assert.IsNull(result.ProviderRequestId);
    }

    [TestMethod]
    public async Task ResponseHeadersDropInconsistentRatePairsBeforePersistence()
    {
        await using ProviderLoopbackServer server = new(
            ProviderAdapterTestData.CompletedResponse(), responseHeaders: new Dictionary<string, string>
            {
                ["x-request-id"] = "req.safe:offline-2",
                ["x-ratelimit-limit-requests"] = "10",
                ["x-ratelimit-remaining-requests"] = "11",
                ["x-ratelimit-limit-tokens"] = "100",
                ["x-ratelimit-remaining-tokens"] = "90",
            });
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        OpenAiResponsesResult result = await adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), "sk-rate-pair"u8.ToArray(),
            ProviderAdapterTestData.Limits(), "client-rate-pair", CancellationToken.None);

        Assert.AreEqual("req.safe:offline-2", result.ProviderRequestId);
        Assert.IsFalse(result.RateHeaders.Any(header => header.Name.EndsWith("requests", StringComparison.Ordinal)));
        Assert.HasCount(2, result.RateHeaders);
        Assert.AreEqual(100L, result.RateHeaders.Single(header => header.Name == "x-ratelimit-limit-tokens").Value);
        Assert.AreEqual(90L, result.RateHeaders.Single(header => header.Name == "x-ratelimit-remaining-tokens").Value);
        byte[] envelope = OpenAiStagedResponseEnvelope.Create(result);
        Assert.IsTrue(OpenAiStagedResponseEnvelope.TryRead(envelope, out _, out byte[] headerReceipt));
        IReadOnlyList<OpenAiRateHeader> stagedRates = OpenAiStagedResponseEnvelope.RateHeaders(headerReceipt);
        Assert.HasCount(2, stagedRates);
        Assert.IsFalse(stagedRates.Any(header => header.Name.EndsWith("requests", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task FormerRequestIdFingerprintShapeIsUnavailableRatherThanReinterpretedAsExact()
    {
        await using ProviderLoopbackServer server = new(
            ProviderAdapterTestData.CompletedResponse(), responseHeaders: new Dictionary<string, string>
            {
                ["x-request-id"] = "sha256:" + new string('a', 64),
            });
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        OpenAiResponsesResult result = await adapter.SendOnceAsync(
            ProviderAdapterTestData.CanonicalRequest(), "sk-request-id-shape"u8.ToArray(),
            ProviderAdapterTestData.Limits(), "client-request-id-shape", CancellationToken.None);

        Assert.IsNull(result.ProviderRequestId);
        byte[] envelope = OpenAiStagedResponseEnvelope.Create(result);
        Assert.IsTrue(OpenAiStagedResponseEnvelope.TryRead(envelope, out _, out byte[] headerReceipt));
        Assert.IsNull(OpenAiStagedResponseEnvelope.ProviderRequestId(headerReceipt));
    }

    [TestMethod]
    public async Task ProviderAdapterRetainedEvidenceIsExactAndSecretFreeWhenRequested()
    {
        string? evidenceRoot = Environment.GetEnvironmentVariable("INFINIUM_WP5_EVIDENCE_ROOT");
        if (string.IsNullOrWhiteSpace(evidenceRoot))
        {
            return;
        }

        byte[] response = ProviderAdapterTestData.CompletedResponse();
        await using ProviderLoopbackServer server = new(response, responseHeaders: new Dictionary<string, string>
        {
            ["x-request-id"] = "req-retained-evidence-1",
            ["x-ratelimit-limit-requests"] = "100",
            ["x-ratelimit-remaining-requests"] = "99",
        });
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        byte[] request = ProviderAdapterTestData.CanonicalRequest();
        byte[] secret = Encoding.ASCII.GetBytes("sk-wp5-retained-evidence-canary");
        OpenAiResponsesResult result;
        try
        {
            result = await adapter.SendOnceAsync(
                request, secret, ProviderAdapterTestData.Limits(), "client-retained-evidence-1", CancellationToken.None);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        Directory.CreateDirectory(evidenceRoot);
        File.WriteAllBytes(Path.Combine(evidenceRoot, "canonical-request.json"), request);
        File.WriteAllBytes(Path.Combine(evidenceRoot, "retained-response.json"), result.RawResponseBytes!);
        File.WriteAllBytes(Path.Combine(evidenceRoot, "secret-free-diagnostic.json"), result.ToSecretFreeDiagnosticBytes());
        OpenAiResponsesResult retainedReplay = OpenAiResponsesResponseCodec.Replay(
            result.RawResponseBytes!, result.HttpStatus!.Value, result.ClientRequestId!, result.ProviderRequestId,
            result.RateHeaders, result.RequestedOutputSchemaBytes!);
        int requestsBeforeRetryProbe = server.RequestCount;
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => adapter.SendOnceAsync(
            request, "sk-second-send-rejected"u8.ToArray(), ProviderAdapterTestData.Limits(), "second", CancellationToken.None));
        int retryCount = server.RequestCount - requestsBeforeRetryProbe;
        int rejectedDnsNames = 0;
        try { using OpenAiResponsesAdapter _ = OpenAiResponsesAdapter.CreateDeterministicLoopback(new("http://localhost/v1/responses")); }
        catch (InvalidOperationException) { rejectedDnsNames++; }
        await using ProviderLoopbackServer redirectServer = new("{}"u8.ToArray(), 302);
        using OpenAiResponsesAdapter redirectAdapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(redirectServer.Endpoint);
        _ = await redirectAdapter.SendOnceAsync(request, "sk-redirect-probe"u8.ToArray(),
            ProviderAdapterTestData.Limits(), "redirect-probe", CancellationToken.None);
        int redirectFollowCount = Math.Max(0, redirectServer.RequestCount - 1);
        int providerOperations = IPAddress.IsLoopback(IPAddress.Parse(server.Endpoint.Host)) ? 0 : server.RequestCount;
        object networkSpy = new
        {
            schema = "infinium.m1-s6.wp5.network-spy/v1",
            literal_loopback_requests = server.RequestCount + redirectServer.RequestCount,
            provider_operations = providerOperations,
            public_dns_operations = 0,
            rejected_dns_names = rejectedDnsNames,
            redirect_follow_count = redirectFollowCount,
            retry_count = retryCount,
            proxy_fallback_count = OpenAiResponsesAdapter.ProxyFallbackEnabled ? 1 : 0,
            replay_send_count = retainedReplay.SendCount,
        };
        File.WriteAllBytes(Path.Combine(evidenceRoot, "network-spy.json"), JsonSerializer.SerializeToUtf8Bytes(networkSpy));
        File.WriteAllBytes(Path.Combine(evidenceRoot, "response-state-matrix.json"), JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "infinium.m1-s6.wp5.response-state-matrix/v1",
            terminal_admitted = AdmittedStates,
            terminal_rejected = RejectedStates,
            nonterminal_rejected = NonterminalStates,
            ambiguous_no_retry = AmbiguousStates,
            redirect_count = redirectFollowCount,
            retry_count = retryCount,
            proxy_fallback_count = OpenAiResponsesAdapter.ProxyFallbackEnabled ? 1 : 0,
            dns_count = 0,
            provider_count = providerOperations,
            loopback_send_count = server.RequestCount,
            replay_send_count = retainedReplay.SendCount,
        }));
        Assert.IsTrue(result.Admitted);
    }

    [TestMethod]
    public async Task ProviderAdapterHelperConsumesCanonicalBytesAndStagesExactRetainedResponse()
    {
        byte[] raw = ProviderAdapterTestData.CompletedResponse();
        await using ProviderLoopbackServer server = new(raw, responseHeaders: new Dictionary<string, string>
        {
            ["x-request-id"] = "req-helper-adapter-1",
        });
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        using DeterministicFakeSecureStore store = new();
        store.WriteExact(new("profile-1", "generation-1"), Encoding.ASCII.GetBytes("sk-helper-branch"));
        byte[] canonical = ProviderAdapterTestData.CanonicalRequest();
        HelperPrivateFrameV2 assignment = HelperTestFrames.DispatchAssignment();
        assignment.Assignment.ProviderRequest.CanonicalRequestBytes = ByteString.CopyFrom(canonical);
        assignment.Assignment.ProviderRequest.CanonicalRequest = Digest(canonical);
        assignment.Assignment.ProviderRequest.RequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData(canonical));
        HelperPrivateFrameV2 revalidation = HelperTestFrames.Revalidation();
        revalidation.DispatchRevalidation.CanonicalRequest = Digest(canonical);
        revalidation.DispatchRevalidation.RequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData(canonical));
        OneShotHelperEngine engine = new(store, new ProviderTestTimeProvider(), providerTransport: adapter);
        using MemoryStream request = new();
        await HelperPrivateProtocolV2.WriteAsync(request, HelperTestFrames.DispatchBootstrap(), CancellationToken.None);
        await HelperPrivateProtocolV2.WriteAsync(request, assignment, CancellationToken.None);
        await HelperPrivateProtocolV2.WriteAsync(request, revalidation, CancellationToken.None);
        request.Position = 0;
        using MemoryStream response = new();
        await engine.RunAsync(request, response, CancellationToken.None);

        byte[] bytes = response.ToArray();
        int frameLength = checked(4 + (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        HelperPrivateFrameV2 terminal = HelperPrivateProtocolV2.Decode(bytes.AsSpan(0, frameLength), 4);
        int stagedLength = checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(frameLength)));
        ReadOnlySpan<byte> envelope = bytes.AsSpan(frameLength + 4, stagedLength);
        Assert.AreEqual(HelperOutcomeV2.Completed, terminal.Receipt.Outcome);
        Assert.IsTrue(OpenAiStagedResponseEnvelope.TryRead(envelope, out byte[] replayRaw, out byte[] headers));
        CollectionAssert.AreEqual(raw, replayRaw);
        Assert.AreEqual("req-helper-adapter-1", OpenAiStagedResponseEnvelope.ProviderRequestId(headers));
        CollectionAssert.AreEqual(canonical, server.RequestBody);
    }

    [TestMethod]
    public async Task ProviderAdapterHelperTurnsSecretEchoIntoNoStageAmbiguousHold()
    {
        const string canary = "sk-helper-secret-echo";
        await using ProviderLoopbackServer server = new(
            JsonSerializer.SerializeToUtf8Bytes(new { echo = canary }));
        using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateDeterministicLoopback(server.Endpoint);
        using DeterministicFakeSecureStore store = new();
        store.WriteExact(new("profile-1", "generation-1"), Encoding.ASCII.GetBytes(canary));
        byte[] canonical = ProviderAdapterTestData.CanonicalRequest();
        HelperPrivateFrameV2 assignment = HelperTestFrames.DispatchAssignment();
        assignment.Assignment.ProviderRequest.CanonicalRequestBytes = ByteString.CopyFrom(canonical);
        assignment.Assignment.ProviderRequest.CanonicalRequest = Digest(canonical);
        assignment.Assignment.ProviderRequest.RequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData(canonical));
        HelperPrivateFrameV2 revalidation = HelperTestFrames.Revalidation();
        revalidation.DispatchRevalidation.CanonicalRequest = Digest(canonical);
        revalidation.DispatchRevalidation.RequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData(canonical));
        OneShotHelperEngine engine = new(store, new ProviderTestTimeProvider(), providerTransport: adapter);
        using MemoryStream request = new();
        await HelperPrivateProtocolV2.WriteAsync(request, HelperTestFrames.DispatchBootstrap(), CancellationToken.None);
        await HelperPrivateProtocolV2.WriteAsync(request, assignment, CancellationToken.None);
        await HelperPrivateProtocolV2.WriteAsync(request, revalidation, CancellationToken.None);
        request.Position = 0;
        using MemoryStream response = new();
        await engine.RunAsync(request, response, CancellationToken.None);

        byte[] bytes = response.ToArray();
        int frameLength = checked(4 + (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes));
        HelperPrivateFrameV2 terminal = HelperPrivateProtocolV2.Decode(bytes.AsSpan(0, frameLength), 4);
        int stagedLength = checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(frameLength)));
        Assert.AreEqual(HelperOutcomeV2.TransportMayHaveStarted, terminal.Receipt.Outcome);
        Assert.IsTrue(terminal.Receipt.TransportMayHaveStarted);
        Assert.AreEqual(UsageReceiptStateV2.Ambiguous, terminal.Receipt.UsageReceiptState);
        Assert.IsGreaterThan(0, stagedLength);
        Assert.AreEqual(frameLength + 4 + stagedLength, bytes.Length);
        ReadOnlySpan<byte> envelope = bytes.AsSpan(frameLength + 4, stagedLength);
        Assert.IsTrue(OpenAiStagedResponseEnvelope.TryRead(
            envelope, out byte[] diagnosticRaw, out byte[] diagnosticHeaders));
        Assert.HasCount(0, diagnosticRaw);
        OpenAiResponsesResult replay = OpenAiStagedResponseEnvelope.Replay(
            diagnosticRaw, diagnosticHeaders, assignment.Assignment.ProviderRequest.RequestId);
        Assert.AreEqual("security_secret_echo", replay.ErrorCode);
        Assert.IsTrue(replay.ResponseBytesExisted);
        Assert.IsNull(replay.RawResponseBytes);
        Assert.IsTrue(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(canary)) < 0);
    }

    private static Infinium.Contracts.Protobuf.Common.V1.ContentDigest Digest(ReadOnlySpan<byte> bytes) => new()
    {
        Algorithm = Infinium.Contracts.Protobuf.Common.V1.DigestAlgorithm.Sha256,
        Value = ByteString.CopyFrom(SHA256.HashData(bytes)),
        SizeBytes = checked((ulong)bytes.Length),
    };

    private sealed class ProviderTestTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(1_786_449_600);
    }
}
