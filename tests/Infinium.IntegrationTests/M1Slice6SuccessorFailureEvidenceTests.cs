using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Integration")]
public sealed class M1Slice6SuccessorFailureEvidenceTests
{
    private static readonly JsonSerializerOptions SnakeJson = new()
    { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private static readonly string[] ProtocolOutcomes = ["Unspecified", "Completed", "FailedKnown",
        "TransportMayHaveStarted", "Unavailable", "Cancelled", "Oversized", "Malformed",
        "receipt-unavailable"];
    [TestMethod]
    public void HelperObservationSeparatesAdapterDispatchFromTerminalTcpSnapshot()
    {
        HelperProcessReceipt receipt = Receipt(Response(), tcpSnapshot: 0);
        M1Slice6HelperBoundaryObservation observation = Observe(receipt);
        Assert.AreEqual(0, observation.TcpNonListenerSnapshotCount);
        Assert.AreEqual(1, observation.AdapterSendCount);
        Assert.AreEqual(1, observation.AdapterDnsResolutionCount);
        Assert.AreEqual("may-have-started-no-response", observation.AdapterTransportDisposition);
        Assert.IsTrue(observation.AdapterNetworkUsed.GetValueOrDefault());
        Assert.AreEqual(0, observation.FailedPredicateIds.Count);
    }

    [TestMethod]
    public void ContradictoryProcessReceiptRetainsAndAcceptsExactAdapterTransportFacts()
    {
        HelperProcessReceipt process = Receipt(Response(), 0) with { Receipt = Receipt(Response(), 0).Receipt.Clone() };
        process.Receipt.TransportMayHaveStarted = false;
        M1Slice6HelperBoundaryObservation observation = Observe(process);
        CollectionAssert.Contains(observation.FailedPredicateIds.ToArray(), "transport-may-have-started-false");
        Assert.AreEqual("may-have-started-no-response", observation.AdapterTransportDisposition);
        M1Slice6CampaignBoundaryFailureReceipt failure =
            M1Slice6CampaignProductionStageBoundary.FailureReceipt(process, "request-test",
                "helper-containment-invalid", "helper-evidence", true, observation, true);
        Assert.AreEqual("may-have-started-no-response", failure.TransportDisposition);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(new
        {
            provider_send_count = failure.ProviderSendCount,
            dns_resolution_count = failure.DnsResolutionCount,
            transport_disposition = failure.TransportDisposition,
            helper_boundary_observation = observation,
        }, SnakeJson));
        using JsonDocument artifacts = JsonDocument.Parse("""
            {"native_trace_path":"trace.json","canary_evidence_path":"canary.json"}
            """);
        M1Slice6SuccessorCampaignRunner.ValidateHelperBoundaryObservation(document.RootElement,
            document.RootElement.GetProperty("helper_boundary_observation"), artifacts.RootElement,
            "helper-evidence-failure");
    }

    [TestMethod]
    public void UnavailableReceiptRejectsNonNullTopLevelDispatchCounts()
    {
        M1Slice6HelperBoundaryObservation observation =
            M1Slice6CampaignProductionStageBoundary.UnavailableHelperObservation();
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(new
        {
            provider_send_count = (int?)1,
            dns_resolution_count = (int?)1,
            transport_disposition = "helper-evidence-failure",
            helper_boundary_observation = observation,
        }, SnakeJson));
        using JsonDocument artifacts = JsonDocument.Parse("""
            {"native_trace_path":null,"canary_evidence_path":null}
            """);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6SuccessorCampaignRunner.ValidateHelperBoundaryObservation(document.RootElement,
                document.RootElement.GetProperty("helper_boundary_observation"), artifacts.RootElement,
                "helper-evidence-failure"));
    }

    [TestMethod]
    public void HelperOutcomeEvidenceContractIsClosedToProtocolValuesAndUnavailableMarker()
    {
        using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
            "contracts", "repository", "m1-slice6-successor-attempt-evidence.v2.schema.json")));
        string[] actual = schema.RootElement.GetProperty("$defs").GetProperty("helperBoundaryObservation")
            .GetProperty("properties").GetProperty("helper_outcome").GetProperty("enum")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        CollectionAssert.AreEqual(ProtocolOutcomes, actual);
    }

    [TestMethod]
    public void EveryHelperContainmentPredicateHasAnExactDeterministicFailureId()
    {
        OpenAiResponsesResult response = Response();
        HelperProcessReceipt baseline = Receipt(response, tcpSnapshot: 0);
        (HelperProcessReceipt Receipt, string Expected)[] cases =
        [
            (baseline with { Receipt = baseline.Receipt.Clone() }, "transport-may-have-started-false"),
            (baseline with { RetryAttempted = true }, "retry-attempted"),
            (baseline with { ListenerCount = 1 }, "listener-snapshot-nonzero"),
            (baseline with { NetworkOperationCount = 2 }, "tcp-snapshot-count-out-of-range"),
            (baseline with { ContainmentProbeExecuted = false }, "containment-probe-missing"),
            (baseline with { TotalContainedProcessCount = 1 }, "contained-process-count-too-small"),
            (baseline with { ProcessTreeSurvivorCount = 1 }, "process-tree-survivor-present"),
            (baseline with { ProcessTreeTerminated = false }, "process-tree-not-terminated"),
            (baseline with { ExcludedHandleAccessible = true }, "excluded-handle-accessible"),
            (baseline with { StagedResponseBytes = OpenAiStagedResponseEnvelope.Create(response with { SendCount = 0 }) },
                "adapter-send-count-not-one"),
            (baseline with { StagedResponseBytes = OpenAiStagedResponseEnvelope.Create(response with { NetworkUsed = false }) },
                "adapter-network-used-false"),
            (baseline with { StagedResponseBytes = OpenAiStagedResponseEnvelope.Create(response with { DnsResolutionCount = 0 }) },
                "adapter-dns-count-not-one"),
        ];
        cases[0].Receipt.Receipt.TransportMayHaveStarted = false;
        foreach ((HelperProcessReceipt value, string expected) in cases)
        {
            M1Slice6HelperBoundaryObservation observation = Observe(value);
            CollectionAssert.Contains(observation.FailedPredicateIds.ToArray(), expected);
        }

        M1Slice6HelperBoundaryObservation combined = Observe(baseline with
        {
            RetryAttempted = true,
            ListenerCount = 1,
            ExcludedHandleAccessible = true,
        });
        string[] expectedCombined =
        [
            "excluded-handle-accessible", "listener-snapshot-nonzero", "retry-attempted",
        ];
        CollectionAssert.AreEqual(expectedCombined, combined.FailedPredicateIds.ToArray());
    }

    [TestMethod]
    public void ValidatedTraceAndCanaryAreRetainedOnlyAfterTheirSecurityGate()
    {
        byte[] trace = "validated-trace"u8.ToArray();
        byte[] canary = "validated-canary"u8.ToArray();
        HelperProcessReceipt process = Receipt(Response(), 0) with
        { NativeCallTraceBytes = trace, NativeCanaryEvidenceBytes = canary };
        M1Slice6HelperBoundaryObservation observation = Observe(process);
        M1Slice6CampaignBoundaryFailureReceipt retained =
            M1Slice6CampaignProductionStageBoundary.FailureReceipt(process, "request-test",
                "helper-containment-invalid", "helper-evidence", true, observation, true);
        CollectionAssert.AreEqual(trace, retained.ValidatedNativeCallTraceBytes);
        CollectionAssert.AreEqual(canary, retained.ValidatedCanaryEvidenceBytes);
        Assert.IsNotNull(retained.SafeResponseHeadersBytes);
        Assert.AreEqual(1, retained.ProviderSendCount);

        byte[] secretTrace = "must-not-retain-secret"u8.ToArray();
        M1Slice6CampaignBoundaryFailureReceipt rejected =
            M1Slice6CampaignProductionStageBoundary.FailureReceipt(
                process with { NativeCallTraceBytes = secretTrace }, "request-test",
                "security-isolation-evidence", "helper-evidence", false, null, false);
        Assert.IsNull(rejected.ValidatedNativeCallTraceBytes);
        Assert.IsNull(rejected.ValidatedCanaryEvidenceBytes);
        Assert.IsNull(rejected.SafeRawResponseBytes);
        Assert.IsNull(rejected.SafeResponseHeadersBytes);
        string serialized = Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(rejected));
        Assert.IsFalse(serialized.Contains("must-not-retain-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MalformedEnvelopeLengthStillRetainsObservationAndValidatedSecuritySidecars()
    {
        HelperProcessReceipt valid = Receipt(Response(), 0);
        byte[] malformed = valid.StagedResponseBytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(7), int.MaxValue);
        HelperProcessReceipt process = valid with { StagedResponseBytes = malformed };
        M1Slice6HelperBoundaryObservation observation =
            M1Slice6CampaignProductionStageBoundary.ObserveHelper(process, "request-test");
        CollectionAssert.Contains(observation.FailedPredicateIds.ToArray(), "staged-envelope-invalid");
        M1Slice6CampaignBoundaryFailureReceipt failure =
            M1Slice6CampaignProductionStageBoundary.FailureReceipt(process, "request-test",
                "helper-containment-invalid", "helper-evidence", true, observation, true);
        Assert.AreEqual(observation, failure.HelperBoundaryObservation);
        CollectionAssert.AreEqual(process.NativeCallTraceBytes, failure.ValidatedNativeCallTraceBytes);
        CollectionAssert.AreEqual(process.NativeCanaryEvidenceBytes, failure.ValidatedCanaryEvidenceBytes);
        Assert.IsNull(failure.SafeRawResponseBytes);
        Assert.IsNull(failure.SafeResponseHeadersBytes);
    }

    [TestMethod]
    public void MalformedEnvelopeHeaderStillRetainsObservationAndValidatedSecuritySidecars()
    {
        HelperProcessReceipt valid = Receipt(Response(), 0);
        Assert.IsTrue(OpenAiStagedResponseEnvelope.TryRead(valid.StagedResponseBytes,
            out byte[] raw, out byte[] headers));
        headers[0] = (byte)'!';
        byte[] malformed = new byte[7 + 8 + raw.Length + headers.Length];
        "INFWP5\0"u8.CopyTo(malformed);
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(7), raw.Length);
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(11), headers.Length);
        raw.CopyTo(malformed.AsSpan(15));
        headers.CopyTo(malformed.AsSpan(15 + raw.Length));
        HelperProcessReceipt process = valid with { StagedResponseBytes = malformed };
        M1Slice6HelperBoundaryObservation observation =
            M1Slice6CampaignProductionStageBoundary.ObserveHelper(process, "request-test");
        CollectionAssert.Contains(observation.FailedPredicateIds.ToArray(), "staged-envelope-invalid");
        M1Slice6CampaignBoundaryFailureReceipt failure =
            M1Slice6CampaignProductionStageBoundary.FailureReceipt(process, "request-test",
                "helper-containment-invalid", "helper-evidence", true, observation, true);
        Assert.AreEqual("may-have-started-no-response", failure.TransportDisposition);
        CollectionAssert.AreEqual(process.NativeCallTraceBytes, failure.ValidatedNativeCallTraceBytes);
        CollectionAssert.AreEqual(process.NativeCanaryEvidenceBytes, failure.ValidatedCanaryEvidenceBytes);
    }

    [TestMethod]
    public void HistoricalV1NormalizationChangesOnlyFourKnownAbsentAccountingValues()
    {
        byte[] original = """
            {"schema":"infinium.m1-s6.successor-attempt-evidence/v1","sentinel":"unchanged","accounting":{"response_id":"","usage_entry_id":"","settlement_id":"settlement-retained","replay_edge_id":"","semantic_failure_code":""}}
            """u8.ToArray();
        byte[] originalCopy = original.ToArray();
        JsonElement normalized = M1Slice6SuccessorCampaignRunner
            .NormalizeKnownV1AbsentValues(original).AsObject().AsValueKindElement();
        CollectionAssert.AreEqual(originalCopy, original);
        Assert.AreEqual("unchanged", normalized.GetProperty("sentinel").GetString());
        JsonElement accounting = normalized.GetProperty("accounting");
        foreach (string field in new[] { "response_id", "usage_entry_id", "replay_edge_id", "semantic_failure_code" })
        { Assert.AreEqual(JsonValueKind.Null, accounting.GetProperty(field).ValueKind); }
        Assert.AreEqual("settlement-retained", accounting.GetProperty("settlement_id").GetString());

        byte[] changedFact = """
            {"accounting":{"response_id":"not-empty","usage_entry_id":"","replay_edge_id":"","semantic_failure_code":""}}
            """u8.ToArray();
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6SuccessorCampaignRunner.NormalizeKnownV1AbsentValues(changedFact));
    }

    private static M1Slice6HelperBoundaryObservation Observe(HelperProcessReceipt process)
    {
        bool parsed = OpenAiStagedResponseEnvelope.TryRead(process.StagedResponseBytes,
            out byte[] raw, out byte[] headers);
        OpenAiResponsesResult? response = parsed
            ? OpenAiStagedResponseEnvelope.Replay(raw, headers, "request-test") : null;
        return M1Slice6CampaignProductionStageBoundary.ObserveHelper(
            process, parsed, raw, headers, response);
    }

    private static HelperProcessReceipt Receipt(OpenAiResponsesResult response, int tcpSnapshot)
    {
        return new(1234, 0, new string('a', 64), new HelperReceiptV2
        {
            Outcome = HelperOutcomeV2.TransportMayHaveStarted,
            TransportMayHaveStarted = true,
        }, OpenAiStagedResponseEnvelope.Create(response), 2, 2, 0, tcpSnapshot, 2, 0,
            true, false, "trace"u8.ToArray(), null, "canary"u8.ToArray(),
            true, false, 2, 2);
    }

    private static OpenAiResponsesResult Response()
    {
        ProviderQuantityContract absent = new(ProviderAvailabilityState.Unavailable, null);
        ProviderUsageContract usage = new(ProviderAvailabilityState.Available,
            new(ProviderAvailabilityState.Available, 1), absent, absent, absent, absent,
            absent, absent, absent, absent, ProviderAvailabilityState.Unavailable,
            ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable,
            UsageReceiptState.Partial);
        return new(ProviderResponseState.Unknown, true, false, null, null, null,
            "request-test", null, null, null, null, null, "transport_timeout", usage,
            [], false, "provider-transport", true, 1)
        {
            DnsResolutionCount = 1,
            ResponseBytesExisted = false,
            ResponseBytesObservedLowerBound = 0,
        };
    }
}

file static class JsonNodeTestExtensions
{
    internal static JsonElement AsValueKindElement(this System.Text.Json.Nodes.JsonNode node) =>
        JsonDocument.Parse(node.ToJsonString()).RootElement.Clone();
}
