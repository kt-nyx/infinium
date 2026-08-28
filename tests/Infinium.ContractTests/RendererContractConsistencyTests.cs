using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class RendererContractConsistencyTests
{
    private const string Session = "host_session_0001";
    private const string Request = "request_identity_0001";
    private const string Gesture = "gesture_identity_001";
    private static readonly string[] ExpectedProjectionCodecParameterPairs =
    [
        "GetApplicationBootstrapRequest->GetApplicationBootstrapResponse",
        "GetProgressRequest->GetProgressResponse",
        "GetResultDetailRequest->GetResultDetailResponse",
        "ListResultItemsRequest->ListResultItemsResponse",
        "SubscribeEventsRequest->ApplicationEvent",
    ];

    public static IEnumerable<object[]> RegisteredMessages()
    {
        foreach (RendererMessageDefinition definition in RendererOperationRegistry.GetDefinitions())
        {
            yield return [definition.Operation, definition.MessageKind, definition.PayloadShape];
        }
    }

    public static IEnumerable<object[]> ResponseOutcomes()
    {
        foreach (string operation in new[]
        {
            "application.bootstrap", "results.list", "results.detail", "progress.read", "application.cancel",
        })
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
            ("transport.session.establish", "event") => "origin",
            ("transport.session.establish", "request") => "renderer_registry_sha256",
            ("transport.gesture.grant", "event") => "gesture_id",
            ("application.bootstrap", "request") => "maximum_recent_runs",
            ("application.bootstrap", "response") => "outcome",
            ("results.list", "request") => "run_id",
            ("results.list", "response") => "outcome",
            ("results.detail", "request") => "item_id",
            ("results.detail", "response") => "outcome",
            ("progress.read", "request") => "run_id",
            ("progress.read", "response") => "outcome",
            ("progress.subscribe", "request") => "subscription_id",
            ("progress.subscribe", "event") => "outcome",
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
    public void GestureReplayAndSequenceStateCommitOnlyAfterCompleteValidation()
    {
        RendererContractValidator validator = new(Session);
        _ = validator.ValidateAndAdvance(CreateCancelRequest(1, Request, Gesture));

        const string secondRequest = "request_identity_0002";
        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(
            CreateCancelRequest(2, secondRequest, Gesture)));
        _ = validator.ValidateAndAdvance(CreateCancelRequest(2, secondRequest, "gesture_identity_002"));

        _ = validator.ValidateAndAdvance(CreateCancelRequest(3, secondRequest, "gesture_identity_003"));

        const string thirdRequest = "request_identity_0003";
        const string thirdGesture = "gesture_identity_004";
        JsonObject malformed = ParseObject(CreateCancelRequest(4, thirdRequest, thirdGesture));
        malformed["payload"]!["unexpected"] = true;
        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(
            JsonSerializer.SerializeToUtf8Bytes(malformed)));
        _ = validator.ValidateAndAdvance(CreateCancelRequest(4, thirdRequest, thirdGesture));

        const string fourthRequest = "request_identity_0004";
        const string fourthGesture = "gesture_identity_005";
        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(
            CreateCancelRequest(6, fourthRequest, fourthGesture)));
        _ = validator.ValidateAndAdvance(CreateCancelRequest(5, fourthRequest, fourthGesture));

        JsonObject correlatedResponse = CreateBaseEnvelope(
            "application.cancel", "response", sequence: 5, requestId: fourthRequest);
        correlatedResponse["payload"] = CreateResponsePayload("application.cancel", "accepted");
        _ = validator.ValidateAndAdvance(JsonSerializer.SerializeToUtf8Bytes(correlatedResponse));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    public void HostEventSequenceRequiresExactNextValueAndRejectedGapDoesNotCommit()
    {
        RendererContractValidator validator = new(Session);
        _ = validator.ValidateAndAdvance(CreateEnvelope("transport.session.establish", "event"));

        JsonObject forwardGap = ParseObject(CreateEnvelope("transport.gesture.grant", "event"));
        forwardGap["sequence"] = "3";
        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(
            JsonSerializer.SerializeToUtf8Bytes(forwardGap)));

        forwardGap["sequence"] = "2";
        _ = validator.ValidateAndAdvance(JsonSerializer.SerializeToUtf8Bytes(forwardGap));
        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(
            JsonSerializer.SerializeToUtf8Bytes(forwardGap)));
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
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void GeneratedCatalogAndNativeAdapterSignaturesAreExhaustiveAndLossless()
    {
        CollectionAssert.AreEquivalent(
            RendererOperationRegistry.GetDefinitions()
                .Select(value => $"{value.Operation}:{value.MessageKind}:{value.PayloadShape}").ToArray(),
            GeneratedRendererOperationCatalog.Messages
                .Select(value => $"{value.Operation}:{value.MessageKind}:{value.PayloadShape}").ToArray());
        using JsonDocument registry = JsonDocument.Parse(RendererOperationRegistry.GetCanonicalInput());
        using JsonDocument rendererSchema = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
            "contracts", "json-schema", "renderer-envelope.v1.schema.json")));
        Assert.IsTrue(JsonElement.DeepEquals(
            rendererSchema.RootElement.GetProperty("x-infinium-registry"), registry.RootElement));
        Assert.AreEqual(
            GeneratedRendererOperationCatalog.RendererContractVersion,
            registry.RootElement.GetProperty("renderer_contract_version").GetString());

        Dictionary<string, (Type Request, Type Response)> unary = new(StringComparer.Ordinal)
        {
            ["GetApplicationBootstrapAsync"] = (typeof(GetApplicationBootstrapRequest), typeof(GetApplicationBootstrapResponse)),
            ["ListResultItemsAsync"] = (typeof(ListResultItemsRequest), typeof(ListResultItemsResponse)),
            ["GetResultDetailAsync"] = (typeof(GetResultDetailRequest), typeof(GetResultDetailResponse)),
            ["GetProgressAsync"] = (typeof(GetProgressRequest), typeof(GetProgressResponse)),
        };
        foreach ((string name, (Type request, Type response)) in unary)
        {
            System.Reflection.MethodInfo method = typeof(IGeneratedRendererApplicationClient).GetMethod(name)!;
            Assert.AreEqual(request, method.GetParameters()[0].ParameterType);
            Assert.AreEqual(typeof(Task<>).MakeGenericType(response), method.ReturnType);
        }

        System.Reflection.MethodInfo stream = typeof(IGeneratedRendererApplicationClient)
            .GetMethod(nameof(IGeneratedRendererApplicationClient.SubscribeEventsAsync))!;
        Assert.AreEqual(typeof(SubscribeEventsRequest), stream.GetParameters()[0].ParameterType);
        Assert.AreEqual(typeof(IAsyncEnumerable<ApplicationEvent>), stream.ReturnType);
        string[] codecPairs = typeof(IGeneratedRendererProjectionCodec).GetMethods()
            .Select(method => string.Join("->", method.GetParameters().Select(parameter => parameter.ParameterType.Name)))
            .Order(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(ExpectedProjectionCodecParameterPairs, codecPairs);

        JsonObject overflow = ParseObject(CreateEnvelope("progress.read", "response"));
        overflow["payload"]!["progress"]!["durable_event_sequence"] = "18446744073709551616";
        AssertRejected(overflow);
        JsonObject oversizedIdentity = ParseObject(CreateEnvelope("results.list", "response"));
        oversizedIdentity["payload"]!["page"]!["items"]![0]!["item_id"] =
            string.Concat(Enumerable.Repeat("🙂", 41));
        AssertRejected(oversizedIdentity);
        JsonObject maximumCursor = ParseObject(CreateEnvelope("results.list", "request"));
        maximumCursor["payload"]!["after_cursor"] = new string('A', 10_923);
        _ = new RendererContractValidator(Session).ValidateAndAdvance(JsonSerializer.SerializeToUtf8Bytes(maximumCursor));
        JsonObject oversizedCursor = ParseObject(CreateEnvelope("results.list", "request"));
        oversizedCursor["payload"]!["after_cursor"] = new string('A', 10_924);
        AssertRejected(oversizedCursor);
        JsonObject nonCanonicalCursor = ParseObject(CreateEnvelope("results.list", "request"));
        nonCanonicalCursor["payload"]!["after_cursor"] = "AB";
        AssertRejected(nonCanonicalCursor);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void NativeApplicationProjectionsRoundTripThroughEveryGeneratedRendererCodec()
    {
        RendererApplicationProjectionCodec codec = new();
        GetApplicationBootstrapRequest bootstrapRequest = new()
        {
            RendererContractVersion = new SemanticVersion { Value = ProtocolConstants.RendererContractVersion },
            MaximumRecentRuns = 20,
            ExpectedProjectionVersion = new ProjectionVersion { Value = "revision-old" },
        };
        foreach (ApplicationErrorCode code in new[]
        {
            ApplicationErrorCode.Conflict, ApplicationErrorCode.Unsupported, ApplicationErrorCode.Unavailable,
            ApplicationErrorCode.Cancelled, ApplicationErrorCode.Indeterminate, ApplicationErrorCode.ResyncRequired,
        })
        {
            ApplicationContractError error = new()
            {
                Code = code,
                InertDetail = "The operation did not succeed.",
                RetryMayBeSafe = false,
                CurrentRevision = new RevisionToken { OpaqueValue = "revision-current" },
                CurrentProjectionVersion = new ProjectionVersion { Value = "2" },
            };
            ValidateProjected("application.bootstrap", "response", codec.Project(
                bootstrapRequest, new GetApplicationBootstrapResponse { Error = error }));
        }
        Assert.ThrowsExactly<InvalidDataException>(() => codec.Project(
            new GetApplicationBootstrapRequest(),
            new GetApplicationBootstrapResponse
            {
                Error = new ApplicationContractError
                {
                    Code = ApplicationErrorCode.Conflict,
                    InertDetail = "Conflict.",
                    CurrentRevision = new RevisionToken { OpaqueValue = "revision-current" },
                },
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => codec.Project(
            bootstrapRequest,
            new GetApplicationBootstrapResponse
            {
                Error = new ApplicationContractError { Code = ApplicationErrorCode.Unknown, InertDetail = "Unknown." },
            }));
        ResultItemSummary summary = new()
        {
            ItemId = "item 1",
            RunId = new RunId { Value = "run 1" },
            Kind = ResultItemKind.Finding,
            LogicalId = "logical 1",
            InertSummary = "<img src=x onerror=alert(1)>",
            Severity = "informational",
            Confidence = "supported",
            AnalyzerId = "analyzer",
            AnalyzerVersion = new SemanticVersion { Value = "1.0.0" },
        };
        ListResultItemsRequest listRequest = new()
        {
            RunId = new RunId { Value = "run 1" },
            RequestedPageSize = 100,
            Sort = ResultItemSort.IdentityAscending,
        };
        listRequest.Kinds.Add(ResultItemKind.Finding);
        ListResultItemsResponse listResponse = new()
        {
            Page = new ResultItemPage { HasMore = false, ProjectionVersion = new ProjectionVersion { Value = "1" } },
        };
        listResponse.Page.Items.Add(summary);
        ValidateProjected("results.list", "response", codec.Project(listRequest, listResponse));

        GetResultDetailRequest detailRequest = new()
        {
            RunId = new RunId { Value = "run 1" },
            Kind = ResultItemKind.Finding,
            ItemId = "item 1",
            ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
        };
        ResultDetail detail = new()
        {
            Summary = summary,
            InertConclusion = "Conclusion.",
            InertCause = "Cause.",
            SourcePayloadId = "payload 1",
            SourcePayloadSha256 = new string('0', 64),
            ProjectionVersion = new ProjectionVersion { Value = "1" },
        };
        detail.EvidenceIds.Add("evidence 1");
        detail.SubjectIds.Add("subject 1");
        ValidateProjected("results.detail", "response", codec.Project(detailRequest, new GetResultDetailResponse { Detail = detail }));

        ProgressSnapshot progress = ProjectionProgress();
        GetProgressRequest progressRequest = new()
        {
            RunId = new RunId { Value = "run 1" },
            ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
        };
        ValidateProjected("progress.read", "response", codec.Project(progressRequest, new GetProgressResponse { Progress = progress }));

        SubscribeEventsRequest subscribeRequest = new()
        {
            SubscriptionId = new SubscriptionId { Value = "subscription_00001" },
            RequestedQueueItems = 64,
        };
        subscribeRequest.RunScope.Add(new RunId { Value = "run 1" });
        foreach (ApplicationEvent applicationEvent in ProjectionEvents(progress))
        {
            ValidateProjected("progress.subscribe", "event", codec.Project(subscribeRequest, applicationEvent));
        }

        ListResultItemsResponse missingPageCursor = new()
        {
            Page = new ResultItemPage { HasMore = true, ProjectionVersion = new ProjectionVersion { Value = "1" } },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => codec.Project(listRequest, missingPageCursor));
        ListResultItemsResponse spuriousPageCursor = new()
        {
            Page = new ResultItemPage
            {
                HasMore = false,
                ProjectionVersion = new ProjectionVersion { Value = "1" },
                Next = new PageCursor { OpaqueValue = Google.Protobuf.ByteString.CopyFromUtf8("next") },
            },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => codec.Project(listRequest, spuriousPageCursor));
        ListResultItemsResponse mismatchedRun = listResponse.Clone();
        mismatchedRun.Page.Items[0].RunId = new RunId { Value = "different run" };
        Assert.ThrowsExactly<InvalidDataException>(() => codec.Project(listRequest, mismatchedRun));
        ListResultItemsResponse unrequestedKind = listResponse.Clone();
        unrequestedKind.Page.Items[0].Kind = ResultItemKind.Failure;
        Assert.ThrowsExactly<InvalidDataException>(() => codec.Project(listRequest, unrequestedKind));
        ApplicationEvent mismatchedSubscription = ProjectionEvents(progress).First();
        mismatchedSubscription.SubscriptionId = new SubscriptionId { Value = "different_subscription" };
        Assert.ThrowsExactly<InvalidDataException>(() => codec.Project(subscribeRequest, mismatchedSubscription));
        ApplicationEvent mismatchedProgressRun = ProjectionEvents(progress).First();
        mismatchedProgressRun.Progress.RunId = new RunId { Value = "different run" };
        Assert.ThrowsExactly<InvalidDataException>(() => codec.Project(subscribeRequest, mismatchedProgressRun));
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
            ("transport.session.establish", "event") => new JsonObject
            {
                ["outcome"] = "accepted",
                ["origin"] = "https://app.infinium.invalid",
                ["renderer_contract_version"] = ProtocolConstants.RendererContractVersion,
                ["renderer_registry_version"] = GeneratedRendererOperationCatalog.RegistryVersion,
                ["renderer_registry_sha256"] = GeneratedRendererOperationCatalog.RegistrySha256,
            },
            ("transport.session.establish", "request") => new JsonObject
            {
                ["renderer_registry_version"] = GeneratedRendererOperationCatalog.RegistryVersion,
                ["renderer_registry_sha256"] = GeneratedRendererOperationCatalog.RegistrySha256,
            },
            ("transport.gesture.grant", "event") => new JsonObject
            {
                ["outcome"] = "accepted",
                ["gesture_id"] = "gesture_identity_001",
                ["target_request_id"] = "target_request_0001",
                ["operation"] = "application.cancel",
            },
            ("application.bootstrap", "request") => new JsonObject { ["maximum_recent_runs"] = 20 },
            ("application.bootstrap", "response") => CreateResponsePayload(operation, "accepted"),
            ("results.list", "request") => ResultListRequest(),
            ("results.list", "response") => CreateResponsePayload(operation, "accepted"),
            ("results.detail", "request") => new JsonObject { ["run_id"] = "run 1", ["kind"] = "finding", ["item_id"] = "item 1" },
            ("results.detail", "response") => CreateResponsePayload(operation, "accepted"),
            ("progress.read", "request") => new JsonObject { ["run_id"] = "run 1" },
            ("progress.read", "response") => CreateResponsePayload(operation, "accepted"),
            ("progress.subscribe", "request") => new JsonObject { ["subscription_id"] = "subscription_00001", ["run_id"] = "run 1", ["requested_queue_items"] = 64 },
            ("progress.subscribe", "event") => new JsonObject { ["outcome"] = "accepted", ["event_kind"] = "progress", ["metadata"] = EventMetadata(), ["progress"] = Progress() },
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

        if (messageKind == "event")
        {
            envelope.Remove("request_id");
            if (!operation.StartsWith("transport.", StringComparison.Ordinal))
            {
                envelope["subscription_id"] = "subscription_00001";
                envelope["revision"] = "2";
            }
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
            ["sequence"] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["request_id"] = requestId,
            ["operation"] = operation,
        };
    }

    private static JsonObject CreateResponsePayload(string operation, string outcome)
    {
        if (outcome == "accepted")
        {
            return operation switch
            {
                "application.bootstrap" => new JsonObject { ["outcome"] = "accepted", ["bootstrap"] = Bootstrap() },
                "results.list" => new JsonObject { ["outcome"] = "accepted", ["page"] = new JsonObject { ["items"] = new JsonArray(ResultSummary()), ["has_more"] = false, ["projection_version"] = "1" } },
                "results.detail" => new JsonObject { ["outcome"] = "accepted", ["detail"] = ResultDetail() },
                "progress.read" => new JsonObject { ["outcome"] = "accepted", ["progress"] = Progress() },
                "application.cancel" => new JsonObject { ["outcome"] = "accepted" },
                _ => throw new AssertFailedException("The registry exposed an untested response operation."),
            };
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
            ("transport.session.establish", "event") => "session-initialization-event",
            ("transport.session.establish", "request") => "session-acknowledgement-request",
            ("transport.gesture.grant", "event") => "gesture-grant-event",
            ("application.bootstrap", "request") => "bootstrap-request",
            ("application.bootstrap", "response") => "bootstrap-response",
            ("results.list", "request") => "result-list-request",
            ("results.list", "response") => "result-list-response",
            ("results.detail", "request") => "result-detail-request",
            ("results.detail", "response") => "result-detail-response",
            ("progress.read", "request") => "progress-request",
            ("progress.read", "response") => "progress-response",
            ("progress.subscribe", "request") => "progress-subscription-request",
            ("progress.subscribe", "event") => "progress-event",
            ("application.cancel", "request") => "cancel-request",
            ("application.cancel", "response") => "cancel-response",
            ("application.resync-required", "event") => "resync-required-event",
            _ => throw new AssertFailedException("The registry exposed an untested message."),
        };

    private static JsonObject Bootstrap() => new()
    {
        ["application_contract_version"] = "1.13.0",
        ["domain_contract_version"] = "1.6.0",
        ["storage_contract_version"] = "1.16.0",
        ["renderer_contract_version"] = ProtocolConstants.RendererContractVersion,
        ["coordinator_health"] = "healthy",
        ["configuration_availability"] = "unavailable",
        ["capabilities"] = new JsonArray(),
        ["recent_runs"] = new JsonArray(),
        ["projection_version"] = "1",
        ["coordinator_instance_id"] = "coordinator-instance",
        ["coordinator_fencing_epoch"] = "1",
    };

    private static JsonObject ResultListRequest() => new()
    {
        ["run_id"] = "run 1",
        ["kinds"] = new JsonArray("supported-case", "lead-only-case", "finding", "abstention", "failure", "coverage-gap"),
        ["search_text"] = string.Empty,
        ["sort"] = "identity-ascending",
        ["requested_page_size"] = 100,
    };

    private static JsonObject ResultSummary() => new()
    {
        ["item_id"] = "item 1",
        ["run_id"] = "run 1",
        ["kind"] = "finding",
        ["logical_id"] = "logical 1",
        ["inert_summary"] = "<img src=x onerror=alert(1)>",
        ["severity"] = "informational",
        ["confidence"] = "supported",
        ["analyzer_id"] = "analyzer",
        ["analyzer_version"] = "1.0.0",
    };

    private static JsonObject ResultDetail() => new()
    {
        ["summary"] = ResultSummary(),
        ["inert_conclusion"] = "Conclusion.",
        ["inert_cause"] = "Cause.",
        ["evidence_ids"] = new JsonArray("evidence 1"),
        ["contradicting_evidence_ids"] = new JsonArray(),
        ["recommendation_ids"] = new JsonArray(),
        ["taxonomy_assignment_ids"] = new JsonArray(),
        ["finding_occurrence_ids"] = new JsonArray(),
        ["hypothesis_ids"] = new JsonArray(),
        ["inert_uncertainty"] = new JsonArray(),
        ["inert_gaps"] = new JsonArray(),
        ["source_payload_id"] = "payload 1",
        ["source_payload_sha256"] = new string('0', 64),
        ["subject_ids"] = new JsonArray("subject 1"),
        ["projection_version"] = "1",
    };

    private static JsonObject Progress() => new()
    {
        ["run_id"] = "run 1",
        ["lifecycle_state"] = "completed-with-gaps",
        ["progress"] = new JsonObject
        {
            ["denominator_state"] = "known",
            ["population_revision"] = "18446744073709551615",
            ["total_units"] = new JsonObject { ["availability"] = "available", ["value"] = "18446744073709551615" },
            ["completed_units"] = "1",
            ["reused_units"] = "0",
            ["queued_units"] = "0",
            ["running_units"] = "0",
            ["failed_units"] = "0",
            ["skipped_units"] = "0",
            ["unsupported_units"] = "0",
            ["limited_units"] = "0",
            ["invalidated_units"] = "0",
            ["gap_units"] = "1",
        },
        ["cost"] = new JsonObject
        {
            ["reserved_nano_usd"] = new JsonObject { ["availability"] = "unavailable" },
            ["calculated_actual_nano_usd"] = new JsonObject { ["availability"] = "unsupported" },
            ["provider_input_tokens"] = new JsonObject { ["availability"] = "unknown" },
            ["provider_output_tokens"] = new JsonObject { ["availability"] = "unavailable" },
            ["provider_reasoning_tokens"] = new JsonObject { ["availability"] = "unavailable" },
            ["provider_dispatch_count"] = new JsonObject { ["availability"] = "unavailable" },
            ["provider_tool_call_count"] = new JsonObject { ["availability"] = "unavailable" },
            ["has_unresolved_hold"] = false,
        },
        ["projection_version"] = "1",
        ["durable_event_sequence"] = "18446744073709551615",
        ["observed_at"] = "2026-08-27T12:00:00Z",
    };

    private static JsonObject EventMetadata() => new()
    {
        ["coordinator_instance_id"] = "coordinator 1",
        ["coordinator_fencing_epoch"] = "18446744073709551615",
        ["subscription_id"] = "subscription_00001",
        ["durable_event_sequence"] = "18446744073709551615",
        ["projection_version"] = "1",
        ["run_scope"] = "run 1",
        ["resume_cursor"] = "Y3Vyc29y",
    };

    private static ProgressSnapshot ProjectionProgress() => new()
    {
        RunId = new RunId { Value = "run 1" },
        LifecycleState = LifecycleState.CompletedWithGaps,
        Progress = new ProgressSummary
        {
            DenominatorState = ProgressDenominatorState.Known,
            PopulationRevision = ulong.MaxValue,
            TotalUnits = new OptionalUInt64 { Availability = AvailabilityState.Available, Value = ulong.MaxValue },
            CompletedUnits = 1,
            GapUnits = 1,
        },
        Cost = new CostSummary
        {
            ReservedNanoUsd = new OptionalInt64 { Availability = AvailabilityState.Available, Value = long.MinValue },
            CalculatedActualNanoUsd = new OptionalInt64 { Availability = AvailabilityState.Unsupported },
            ProviderInputTokens = new OptionalUInt64 { Availability = AvailabilityState.Unknown },
            ProviderOutputTokens = new OptionalUInt64 { Availability = AvailabilityState.Unavailable },
            ProviderReasoningTokens = new OptionalUInt64 { Availability = AvailabilityState.Unavailable },
            ProviderDispatchCount = new OptionalUInt64 { Availability = AvailabilityState.Unavailable },
            ProviderToolCallCount = new OptionalUInt64 { Availability = AvailabilityState.Unavailable },
        },
        ProjectionVersion = new ProjectionVersion { Value = "1" },
        DurableEventSequence = ulong.MaxValue,
        ObservedAt = new Instant { UnixSeconds = 1_788_000_000, Nanoseconds = 123_456_700 },
    };

    private static IEnumerable<ApplicationEvent> ProjectionEvents(ProgressSnapshot progress)
    {
        ApplicationEvent Event(EventKind kind) => new()
        {
            CoordinatorInstanceId = new CoordinatorInstanceId { Value = "coordinator 1" },
            CoordinatorFencingEpoch = ulong.MaxValue,
            SubscriptionId = new SubscriptionId { Value = "subscription_00001" },
            DurableEventSequence = ulong.MaxValue,
            ProjectionVersion = new ProjectionVersion { Value = "1" },
            Kind = kind,
            RunScope = new RunId { Value = "run 1" },
            ResumeCursor = new EventCursor { OpaqueValue = Google.Protobuf.ByteString.CopyFromUtf8("resume") },
        };

        ApplicationEvent progressEvent = Event(EventKind.Progress);
        progressEvent.Progress = progress;
        yield return progressEvent;

        ApplicationEvent lifecycleEvent = Event(EventKind.LifecycleChanged);
        lifecycleEvent.LifecycleChanged = new LifecycleChanged
        {
            PreviousState = LifecycleState.Running,
            CurrentState = LifecycleState.CompletedWithGaps,
            LifecycleGeneration = ulong.MaxValue,
            TransitionId = new DurableTransitionId { Value = "transition 1" },
            TransitionRecordKind = LifecycleTransitionRecordKind.Observed,
            LifecyclePolicyVersion = new SemanticVersion { Value = "1.0.0" },
        };
        yield return lifecycleEvent;

        ApplicationEvent invalidatedEvent = Event(EventKind.ProjectionInvalidated);
        invalidatedEvent.ProjectionInvalidated = new ProjectionInvalidated
        {
            CurrentProjectionVersion = new ProjectionVersion { Value = "2" },
            Reason = ResyncReason.ProjectionRebuilt,
        };
        yield return invalidatedEvent;

        ApplicationEvent resyncEvent = Event(EventKind.ResyncRequired);
        resyncEvent.ResyncRequired = new ResyncRequired
        {
            CurrentProjectionVersion = new ProjectionVersion { Value = "2" },
            Reason = ResyncReason.CoordinatorRestart,
        };
        yield return resyncEvent;
    }

    private static void ValidateProjected(string operation, string messageKind, JsonElement payload)
    {
        JsonObject envelope = new()
        {
            ["contract_version"] = ProtocolConstants.RendererContractVersion,
            ["message_kind"] = messageKind,
            ["session_id"] = Session,
            ["sequence"] = "1",
            ["operation"] = operation,
            ["payload"] = JsonNode.Parse(payload.GetRawText()),
        };
        if (messageKind == "event")
        {
            envelope["subscription_id"] = "subscription_00001";
            envelope["revision"] = "1";
        }
        else
        {
            envelope["request_id"] = Request;
        }
        byte[] schema = File.ReadAllBytes(TestRepository.PathFromRoot(
            "contracts", "json-schema", "renderer-envelope.v1.schema.json"));
        ActiveRepositoryJsonSchemaValidator.Validate(
            JsonSerializer.SerializeToUtf8Bytes(envelope), schema, "renderer-envelope.v1.schema.json");
    }

    private static JsonObject ParseObject(byte[] bytes) =>
        JsonNode.Parse(bytes)?.AsObject() ?? throw new AssertFailedException("Expected a JSON object.");

    private static void AssertRejected(JsonObject envelope) =>
        Assert.ThrowsExactly<InvalidDataException>(() => new RendererContractValidator(Session)
            .ValidateAndAdvance(JsonSerializer.SerializeToUtf8Bytes(envelope)));
}
