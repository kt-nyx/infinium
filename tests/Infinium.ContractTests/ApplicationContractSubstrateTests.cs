using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Infinium.Application.Evaluation;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ApplicationContractSubstrateTests
{
    private const string Session = "host_session_0001";
    private const string Request = "request_identity_0001";

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void BootstrapAndCommonPrimitivesRoundTripWithIndependentVersions()
    {
        Assert.AreEqual(ProtocolConstants.ContractVersion,
            ProtocolConstants.Compatibility.ApplicationContract.Value);
        Assert.AreEqual(ProtocolConstants.DomainContractVersion,
            ProtocolConstants.Compatibility.DomainContract.Value);
        Assert.AreEqual(ProtocolConstants.StorageContractVersion,
            ProtocolConstants.Compatibility.StorageContract.Value);
        Assert.AreEqual(
            AuthoritativeStore.CurrentStorageContractVersion,
            ProtocolConstants.StorageContractVersion);

        GetApplicationBootstrapRequest request = new()
        {
            RendererContractVersion = new SemanticVersion { Value = "1.0.0" },
            MaximumRecentRuns = 20,
            ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
        };
        GetApplicationBootstrapRequest decoded =
            ApplicationContractValidator.ParseBootstrapRequest(request.ToByteArray());
        Assert.AreEqual(request, decoded);

        UserOperationReceipt receipt = new()
        {
            RequestId = Request,
            ReceiptId = "receipt_identity_001",
            Disposition = OperationDisposition.Conflict,
            Conflict = new RevisionConflict
            {
                Expected = new RevisionToken { OpaqueValue = "revision-old" },
                Current = new RevisionToken { OpaqueValue = "revision-current" },
                Disposition = ConflictDisposition.StaleRevision,
            },
        };
        ApplicationContractValidator.Validate(receipt);
        Assert.AreEqual(receipt, UserOperationReceipt.Parser.ParseFrom(receipt.ToByteArray()));

        ApplicationContractValidator.Validate(new CancellationRequest
        {
            RequestId = "cancel_request_0001",
            TargetRequestId = Request,
        });

        ApplicationBootstrap bootstrap = new()
        {
            Compatibility = new ContractCompatibility
            {
                ApplicationContract = new SemanticVersion { Value = ProtocolConstants.ContractVersion },
                DomainContract = new SemanticVersion { Value = ProtocolConstants.DomainContractVersion },
                StorageContract = new SemanticVersion { Value = ProtocolConstants.StorageContractVersion },
            },
            RendererContractVersion = new SemanticVersion { Value = ProtocolConstants.RendererContractVersion },
            CoordinatorHealth = HealthState.Healthy,
            Configuration = new ConfigurationAvailability { Availability = Availability.Unavailable },
            ProjectionVersion = new ProjectionVersion { Value = "1" },
            CoordinatorInstanceId = new CoordinatorInstanceId { Value = "coordinator-instance" },
            CoordinatorFencingEpoch = 1,
        };
        bootstrap.Capabilities.Add(new ApplicationCapabilityState
        {
            Capability = ApplicationCapability.Bootstrap,
            Availability = Availability.Available,
            InertReason = "Bootstrap projection is active.",
        });
        bootstrap.RecentRuns.Add(new RunSummary
        {
            RunId = new RunId { Value = "run-1" },
            LifecycleState = LifecycleState.Completed,
            LifecycleGeneration = 2,
        });
        byte[] rendererResponse = RendererBootstrapAdapter.BuildResponse(
            bootstrap,
            Session,
            sequence: 1,
            requestId: Request);
        using JsonDocument renderer = JsonDocument.Parse(rendererResponse);
        Assert.AreEqual("run-1", renderer.RootElement.GetProperty("payload")
            .GetProperty("bootstrap").GetProperty("recent_runs")[0]
            .GetProperty("run_id").GetString());
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void BootstrapCodecFailsClosedForDefaultsVersionsBoundsAndUnknownFields()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ApplicationContractValidator.Validate(new GetApplicationBootstrapRequest()));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.Validate(
            new GetApplicationBootstrapRequest
            {
                RendererContractVersion = new SemanticVersion { Value = "2.0.0" },
                MaximumRecentRuns = 1,
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.Validate(
            new GetApplicationBootstrapRequest
            {
                RendererContractVersion = new SemanticVersion { Value = "1.0.0" },
                MaximumRecentRuns = 21,
            }));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ApplicationContractValidator.ParseBootstrapRequest([0xff]));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ApplicationContractValidator.ParseBootstrapRequest(
                new byte[ProtocolConstants.MaximumMessageBytes + 1]));

        GetApplicationBootstrapRequest valid = new()
        {
            RendererContractVersion = new SemanticVersion { Value = "1.0.0" },
            MaximumRecentRuns = 1,
        };
        byte[] withUnknown = [.. valid.ToByteArray(), 0x98, 0x06, 0x01];
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ApplicationContractValidator.ParseBootstrapRequest(withUnknown));

        UserOperationReceipt unknown = new()
        {
            RequestId = Request,
            ReceiptId = "receipt_identity_001",
            Disposition = (OperationDisposition)999,
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.Validate(unknown));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void RendererRegistryIsClosedStrictAndDeterministic()
    {
        byte[] registry = RendererOperationRegistry.GetCanonicalInput();
        Assert.AreEqual(
            "c302d6cc6728cdab1d65a618313b357d828af01a41069156bc1bfca52433d9ac",
            RendererOperationRegistry.GetCanonicalSha256());
        byte[] schema = File.ReadAllBytes(TestRepository.PathFromRoot(
            "contracts", "json-schema", "renderer-operation-registry.v1.schema.json"));
        ActiveRepositoryJsonSchemaValidator.Validate(
            registry,
            schema,
            "renderer-operation-registry.v1.schema.json");

        string text = Encoding.UTF8.GetString(registry);
        foreach (string denied in new[]
        {
            "path", "sql", "command", "url", "credential", "provider_request", "filesystem", "coordinator_proxy",
        })
        {
            StringAssert.Contains(text, $"\"{denied}\"");
        }

        JsonNode changed = JsonNode.Parse(registry) ?? throw new AssertFailedException();
        changed["operations"]!.AsArray().Add(new JsonObject
        {
            ["operation"] = "generic.invoke",
            ["message_kinds"] = new JsonArray("request"),
            ["native_target"] = "GenericProxy",
            ["gesture"] = "not-required",
            ["maturity"] = "producer-consumer-validated",
            ["owner"] = "frontend-application-contract-foundation",
        });
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ActiveRepositoryJsonSchemaValidator.Validate(
                JsonSerializer.SerializeToUtf8Bytes(changed), schema, "changed-registry"));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void RendererEnvelopeRejectsMalformedOversizedReplayOrderSessionAndUnknownInput()
    {
        RendererContractValidator validator = new(Session);
        RendererEnvelope first = validator.ValidateAndAdvance(Envelope(
            sequence: 1,
            operation: "application.bootstrap",
            payload: "\"maximum_recent_runs\":20"));
        Assert.AreEqual("application.bootstrap", first.Operation);

        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(Envelope(
            sequence: 1,
            operation: "application.bootstrap",
            payload: "\"maximum_recent_runs\":20")));
        Assert.ThrowsExactly<InvalidDataException>(() => validator.ValidateAndAdvance(Envelope(
            sequence: 3,
            operation: "application.bootstrap",
            payload: "\"maximum_recent_runs\":20")));

        RendererContractValidator wrongSession = new(Session);
        Assert.ThrowsExactly<InvalidDataException>(() => wrongSession.ValidateAndAdvance(Envelope(
            sequence: 1,
            operation: "application.bootstrap",
            payload: "\"maximum_recent_runs\":20",
            session: "another_session_001")));
        Assert.ThrowsExactly<InvalidDataException>(() => new RendererContractValidator(Session)
            .ValidateAndAdvance("{"u8.ToArray()));
        Assert.ThrowsExactly<InvalidDataException>(() => new RendererContractValidator(Session)
            .ValidateAndAdvance(new byte[ProtocolConstants.MaximumMessageBytes + 1]));

        string unknownField = Encoding.UTF8.GetString(Envelope(
            sequence: 1,
            operation: "application.bootstrap",
            payload: "\"maximum_recent_runs\":20"))
            .Replace("\"payload\"", "\"path\":\"C:/escape\",\"payload\"", StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidDataException>(() => new RendererContractValidator(Session)
            .ValidateAndAdvance(Encoding.UTF8.GetBytes(unknownField)));

        string unknownOperation = Encoding.UTF8.GetString(Envelope(
            sequence: 1,
            operation: "application.bootstrap",
            payload: "\"maximum_recent_runs\":20"))
            .Replace("application.bootstrap", "generic.invoke", StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidDataException>(() => new RendererContractValidator(Session)
            .ValidateAndAdvance(Encoding.UTF8.GetBytes(unknownOperation)));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void RendererEnvelopeRepresentsCancellationAndTypedResyncWithoutDurableAuthority()
    {
        RendererContractValidator cancellation = new(Session);
        RendererEnvelope cancelled = cancellation.ValidateAndAdvance(JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                contract_version = "1.0.0",
                message_kind = "request",
                session_id = Session,
                sequence = 1,
                request_id = "cancel_request_0001",
                operation = "application.cancel",
                gesture_proof = new { gesture_id = "gesture_identity_01" },
                payload = new { target_request_id = Request },
            }));
        Assert.AreEqual("application.cancel", cancelled.Operation);

        RendererContractValidator eventStream = new(Session);
        RendererEnvelope resync = eventStream.ValidateAndAdvance(JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                contract_version = "1.0.0",
                message_kind = "event",
                session_id = Session,
                sequence = 1,
                subscription_id = "subscription_00001",
                operation = "application.resync-required",
                payload = new { current_projection_version = "2" },
            }));
        Assert.AreEqual("application.resync-required", resync.Operation);
    }

    private static byte[] Envelope(
        ulong sequence,
        string operation,
        string payload,
        string session = Session)
    {
        JsonObject payloadObject = JsonNode.Parse("{" + payload + "}")!.AsObject();
        return JsonSerializer.SerializeToUtf8Bytes(new JsonObject
        {
            ["contract_version"] = "1.0.0",
            ["message_kind"] = "request",
            ["session_id"] = session,
            ["sequence"] = sequence,
            ["request_id"] = Request,
            ["operation"] = operation,
            ["payload"] = payloadObject,
        });
    }
}
