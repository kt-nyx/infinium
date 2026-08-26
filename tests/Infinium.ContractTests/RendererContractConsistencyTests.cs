using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class RendererContractConsistencyTests
{
    private const string Session = "host_session_0001";
    private const string Request = "request_identity_0001";
    private const string Gesture = "gesture_identity_001";

    public static IEnumerable<object[]> RegisteredMessages()
    {
        foreach (RendererMessageDefinition definition in RendererOperationRegistry.GetDefinitions())
        {
            yield return [definition.Operation, definition.MessageKind, definition.PayloadShape];
        }
    }

    public static IEnumerable<object[]> ResponseOutcomes()
    {
        foreach (string operation in new[] { "application.bootstrap", "application.cancel" })
        {
            foreach (string outcome in new[]
            {
                "accepted", "rejected", "conflict", "unsupported", "unavailable",
                "cancelled", "indeterminate", "resync-required",
            })
            {
                yield return [operation, outcome];
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(RegisteredMessages))]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void RegistrySchemaAndRuntimeAcceptTheSameClosedMessages(
        string operation,
        string messageKind,
        string payloadShape)
    {
        Assert.AreEqual(ExpectedPayloadShape(operation, messageKind), payloadShape);
        byte[] envelope = CreateEnvelope(operation, messageKind);
        byte[] schema = File.ReadAllBytes(TestRepository.PathFromRoot(
            "contracts", "json-schema", "renderer-envelope.v1.schema.json"));
        ActiveRepositoryJsonSchemaValidator.Validate(envelope, schema, "renderer-envelope.v1.schema.json");

        RendererEnvelope accepted = new RendererContractValidator(Session).ValidateAndAdvance(envelope);
        Assert.AreEqual(operation, accepted.Operation);
        Assert.AreEqual(messageKind, accepted.MessageKind);
        Assert.AreEqual(messageKind == "request" && operation == "application.cancel", accepted.GestureId is not null);
    }

    [TestMethod]
    [DynamicData(nameof(RegisteredMessages))]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void EveryRegisteredMessageRejectsMalformedAndMutatedPayloads(
        string operation,
        string messageKind,
        string payloadShape)
    {
        Assert.AreEqual(ExpectedPayloadShape(operation, messageKind), payloadShape);
        JsonObject unknownField = ParseObject(CreateEnvelope(operation, messageKind));
        unknownField["payload"]!["unexpected"] = true;
        AssertRejected(unknownField);

        JsonObject missingField = ParseObject(CreateEnvelope(operation, messageKind));
        string requiredPayloadField = (operation, messageKind) switch
        {
            ("application.bootstrap", "request") => "maximum_recent_runs",
            ("application.bootstrap", "response") => "outcome",
            ("application.cancel", "request") => "target_request_id",
            ("application.cancel", "response") => "outcome",
            ("application.resync-required", "event") => "current_projection_version",
            _ => throw new AssertFailedException("The registry exposed an untested message."),
        };
        missingField["payload"]!.AsObject().Remove(requiredPayloadField);
        AssertRejected(missingField);

        JsonObject malformedPayload = ParseObject(CreateEnvelope(operation, messageKind));
        malformedPayload["payload"] = "not-an-object";
        AssertRejected(malformedPayload);

        JsonObject wrongKind = ParseObject(CreateEnvelope(operation, messageKind));
        wrongKind["message_kind"] = messageKind == "event" ? "request" : "event";
        AssertRejected(wrongKind);
    }

    [TestMethod]
    [DynamicData(nameof(ResponseOutcomes))]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ResponsesRepresentTypedSuccessAndNonSuccessWithoutGestureProof(string operation, string outcome)
    {
        JsonObject response = CreateBaseEnvelope(operation, "response", sequence: 1, requestId: Request);
        response["payload"] = CreateResponsePayload(operation, outcome);
        RendererEnvelope accepted = new RendererContractValidator(Session)
            .ValidateAndAdvance(JsonSerializer.SerializeToUtf8Bytes(response));
        Assert.AreEqual(outcome, accepted.Outcome);
        Assert.IsNull(accepted.GestureId);

        JsonObject mismatchedOutcome = ParseObject(JsonSerializer.SerializeToUtf8Bytes(response));
        if (outcome == "accepted")
        {
            mismatchedOutcome["payload"]!["error"] = Error("unsupported");
        }
        else
        {
            mismatchedOutcome["payload"]!["error"]!["code"] = "unknown";
        }

        AssertRejected(mismatchedOutcome);

        response["gesture_proof"] = new JsonObject { ["gesture_id"] = Gesture };
        AssertRejected(response);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void GestureRequestAndSequenceStateCommitOnlyAfterCompleteValidation()
    {
        RendererContractValidator validator = new(Session);
        _ = validator.ValidateAndAdvance(CreateCancelRequest(1, Request, Gesture));

        const string secondRequest = "request_identity_0002";
        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(
            CreateCancelRequest(2, secondRequest, Gesture)));
        _ = validator.ValidateAndAdvance(CreateCancelRequest(2, secondRequest, "gesture_identity_002"));

        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(
            CreateCancelRequest(3, secondRequest, "gesture_identity_003")));

        const string thirdRequest = "request_identity_0003";
        const string thirdGesture = "gesture_identity_003";
        JsonObject malformed = ParseObject(CreateCancelRequest(3, thirdRequest, thirdGesture));
        malformed["payload"]!["unexpected"] = true;
        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(
            JsonSerializer.SerializeToUtf8Bytes(malformed)));
        _ = validator.ValidateAndAdvance(CreateCancelRequest(3, thirdRequest, thirdGesture));

        const string fourthRequest = "request_identity_0004";
        const string fourthGesture = "gesture_identity_004";
        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(
            CreateCancelRequest(5, fourthRequest, fourthGesture)));
        _ = validator.ValidateAndAdvance(CreateCancelRequest(4, fourthRequest, fourthGesture));

        JsonObject correlatedResponse = CreateBaseEnvelope(
            "application.cancel", "response", sequence: 5, requestId: fourthRequest);
        correlatedResponse["payload"] = CreateResponsePayload("application.cancel", "accepted");
        _ = validator.ValidateAndAdvance(JsonSerializer.SerializeToUtf8Bytes(correlatedResponse));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void MalformedOversizedUnknownAndAuthorityBearingInputFailsClosed()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => new RendererContractValidator(Session)
            .ValidateAndAdvance("{"u8.ToArray()));
        Assert.ThrowsExactly<InvalidDataException>(() => new RendererContractValidator(Session)
            .ValidateAndAdvance(new byte[ProtocolConstants.MaximumMessageBytes + 1]));

        foreach (string field in new[]
        {
            "path", "sql", "command", "command_line", "url", "credential",
            "provider_request", "filesystem", "coordinator_proxy",
        })
        {
            JsonObject envelope = ParseObject(CreateEnvelope("application.bootstrap", "request"));
            envelope[field] = "forbidden";
            AssertRejected(envelope);
        }

        JsonObject unknownOperation = ParseObject(CreateEnvelope("application.bootstrap", "request"));
        unknownOperation["operation"] = "generic.invoke";
        AssertRejected(unknownOperation);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProtobufUnknownAndUnsupportedStatesCannotBecomeSuccess()
    {
        UserOperationReceipt unsupported = Receipt(
            OperationDisposition.Unsupported,
            ApplicationErrorCode.Unsupported);
        ApplicationContractValidator.Validate(unsupported);
        Assert.AreNotEqual(OperationDisposition.Accepted, unsupported.Disposition);

        foreach ((OperationDisposition disposition, ApplicationErrorCode code) in new[]
        {
            (OperationDisposition.Unavailable, ApplicationErrorCode.Unavailable),
            (OperationDisposition.Cancelled, ApplicationErrorCode.Cancelled),
            (OperationDisposition.Indeterminate, ApplicationErrorCode.Indeterminate),
        })
        {
            UserOperationReceipt nonSuccess = Receipt(disposition, code);
            ApplicationContractValidator.Validate(nonSuccess);
            Assert.AreNotEqual(OperationDisposition.Accepted, nonSuccess.Disposition);
        }

        UserOperationReceipt resync = Receipt(
            OperationDisposition.ResyncRequired,
            ApplicationErrorCode.ResyncRequired);
        resync.Error.CurrentProjectionVersion = new ProjectionVersion { Value = "2" };
        ApplicationContractValidator.Validate(resync);

        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.Validate(
            Receipt((OperationDisposition)999, ApplicationErrorCode.Unknown)));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.Validate(
            Receipt(OperationDisposition.Rejected, ApplicationErrorCode.Unknown)));

        UserOperationReceipt disguisedFailure = Receipt(
            OperationDisposition.Accepted,
            ApplicationErrorCode.Unsupported);
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.Validate(disguisedFailure));

        UserOperationReceipt numericConflict = Receipt(
            OperationDisposition.Conflict,
            ApplicationErrorCode.Conflict);
        numericConflict.Conflict = new RevisionConflict
        {
            Expected = new RevisionToken { OpaqueValue = "revision-old" },
            Current = new RevisionToken { OpaqueValue = "revision-current" },
            Disposition = (ConflictDisposition)999,
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.Validate(numericConflict));

        CancellationReceipt unsupportedCancellation = new()
        {
            RequestId = Request,
            TargetRequestId = "target_request_0001",
            Disposition = CancellationDisposition.Unsupported,
            Error = new ApplicationContractError
            {
                Code = ApplicationErrorCode.Unsupported,
                InertDetail = "Cancellation is unsupported.",
            },
        };
        ApplicationContractValidator.Validate(unsupportedCancellation);
        Assert.AreNotEqual(CancellationDisposition.TransportOnly, unsupportedCancellation.Disposition);
    }

    private static UserOperationReceipt Receipt(
        OperationDisposition disposition,
        ApplicationErrorCode errorCode) => new()
        {
            RequestId = Request,
            ReceiptId = "receipt_identity_001",
            Disposition = disposition,
            Error = new ApplicationContractError
            {
                Code = errorCode,
                InertDetail = "The operation did not succeed.",
            },
        };

    private static byte[] CreateEnvelope(string operation, string messageKind)
    {
        JsonObject envelope = CreateBaseEnvelope(operation, messageKind, sequence: 1, requestId: Request);
        envelope["payload"] = (operation, messageKind) switch
        {
            ("application.bootstrap", "request") => new JsonObject { ["maximum_recent_runs"] = 20 },
            ("application.bootstrap", "response") => CreateResponsePayload(operation, "accepted"),
            ("application.cancel", "request") => new JsonObject { ["target_request_id"] = "target_request_0001" },
            ("application.cancel", "response") => CreateResponsePayload(operation, "accepted"),
            ("application.resync-required", "event") => new JsonObject
            {
                ["outcome"] = "resync-required",
                ["error"] = Error("resync-required"),
                ["current_projection_version"] = "2",
            },
            _ => throw new AssertFailedException("The registry exposed an untested message."),
        };

        if (operation == "application.cancel" && messageKind == "request")
        {
            envelope["gesture_proof"] = new JsonObject { ["gesture_id"] = Gesture };
        }

        if (operation == "application.resync-required" && messageKind == "event")
        {
            envelope.Remove("request_id");
            envelope["subscription_id"] = "subscription_00001";
            envelope["revision"] = "2";
        }

        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }

    private static byte[] CreateCancelRequest(
        ulong sequence,
        string requestId,
        string gestureId)
    {
        JsonObject envelope = CreateBaseEnvelope("application.cancel", "request", sequence, requestId);
        envelope["gesture_proof"] = new JsonObject { ["gesture_id"] = gestureId };
        envelope["payload"] = new JsonObject { ["target_request_id"] = "target_request_0001" };
        return JsonSerializer.SerializeToUtf8Bytes(envelope);
    }

    private static JsonObject CreateBaseEnvelope(
        string operation,
        string messageKind,
        ulong sequence,
        string requestId)
    {
        return new JsonObject
        {
            ["contract_version"] = ProtocolConstants.RendererContractVersion,
            ["message_kind"] = messageKind,
            ["session_id"] = Session,
            ["sequence"] = sequence,
            ["request_id"] = requestId,
            ["operation"] = operation,
        };
    }

    private static JsonObject CreateResponsePayload(string operation, string outcome)
    {
        if (outcome == "accepted")
        {
            return operation == "application.bootstrap"
                ? new JsonObject { ["outcome"] = "accepted", ["bootstrap"] = Bootstrap() }
                : new JsonObject { ["outcome"] = "accepted" };
        }

        JsonObject payload = new()
        {
            ["outcome"] = outcome,
            ["error"] = Error(outcome == "rejected" ? "invalid-argument" : outcome),
        };
        if (outcome == "conflict")
        {
            payload["conflict"] = new JsonObject
            {
                ["expected_revision"] = "revision-old",
                ["current_revision"] = "revision-current",
                ["disposition"] = "stale-revision",
            };
        }

        if (outcome == "resync-required")
        {
            payload["current_projection_version"] = "2";
        }

        return payload;
    }

    private static JsonObject Error(string code) => new()
    {
        ["code"] = code,
        ["inert_detail"] = "The operation did not succeed.",
        ["retry_may_be_safe"] = false,
    };

    private static string ExpectedPayloadShape(string operation, string messageKind) =>
        (operation, messageKind) switch
        {
            ("application.bootstrap", "request") => "bootstrap-request",
            ("application.bootstrap", "response") => "bootstrap-response",
            ("application.cancel", "request") => "cancel-request",
            ("application.cancel", "response") => "cancel-response",
            ("application.resync-required", "event") => "resync-required-event",
            _ => throw new AssertFailedException("The registry exposed an untested message."),
        };

    private static JsonObject Bootstrap() => new()
    {
        ["application_contract_version"] = "1.9.0",
        ["domain_contract_version"] = "1.3.0",
        ["storage_contract_version"] = "1.14.0",
        ["renderer_contract_version"] = "1.1.0",
        ["coordinator_health"] = "healthy",
        ["configuration_availability"] = "unavailable",
        ["capabilities"] = new JsonArray(),
        ["recent_runs"] = new JsonArray(),
        ["projection_version"] = "1",
        ["coordinator_instance_id"] = "coordinator-instance",
        ["coordinator_fencing_epoch"] = 1,
    };

    private static JsonObject ParseObject(byte[] bytes) =>
        JsonNode.Parse(bytes)?.AsObject() ?? throw new AssertFailedException("Expected a JSON object.");

    private static void AssertRejected(JsonObject envelope) =>
        Assert.ThrowsExactly<InvalidDataException>(() => new RendererContractValidator(Session)
            .ValidateAndAdvance(JsonSerializer.SerializeToUtf8Bytes(envelope)));
}
