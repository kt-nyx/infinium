using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Google.Protobuf;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AppProviderAvailabilityState = Infinium.Contracts.Protobuf.Application.V1.ProviderAvailabilityState;
using AppProviderProfileLifecycleState = Infinium.Contracts.Protobuf.Application.V1.ProviderProfileLifecycleState;
using DomainProviderAvailabilityState = Infinium.Domain.Contracts.ProviderAvailabilityState;
using DomainProviderOperationKind = Infinium.Domain.Contracts.ProviderOperationKind;
using V1Frame = Infinium.Contracts.Protobuf.Helper.V1.HelperPrivateFrame;
using V2Assignment = Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentV2;
using V2AssignmentKind = Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentKindV2;
using V2Bootstrap = Infinium.Contracts.Protobuf.Helper.V2.HelperBootstrapV2;
using V2Disposition = Infinium.Contracts.Protobuf.Helper.V2.DispatchDispositionV2;
using V2Frame = Infinium.Contracts.Protobuf.Helper.V2.HelperPrivateFrameV2;
using V2InputBoundProof = Infinium.Contracts.Protobuf.Helper.V2.InputBoundProofV2;
using V2InputBoundProofStatus = Infinium.Contracts.Protobuf.Helper.V2.InputBoundProofStatusV2;
using V2Limits = Infinium.Contracts.Protobuf.Helper.V2.HelperLimitsV2;
using V2OperationKind = Infinium.Contracts.Protobuf.Helper.V2.ProviderOperationKindV2;
using V2Outcome = Infinium.Contracts.Protobuf.Helper.V2.HelperOutcomeV2;
using V2ProviderRequest = Infinium.Contracts.Protobuf.Helper.V2.ProviderRequestV2;
using V2Receipt = Infinium.Contracts.Protobuf.Helper.V2.HelperReceiptV2;
using V2Revalidation = Infinium.Contracts.Protobuf.Helper.V2.DispatchRevalidationV2;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderContractJsonCodecTests
{
    private static readonly Sha256Fingerprint Fingerprint = new(new string('a', 64));
    private static readonly UtcTimestamp RecordedAt = UtcTimestamp.Parse("2026-08-10T00:00:00.0000000+00:00");
    private static readonly ProviderFiniteLimitsContract Limits = new(
        65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000);
    private static readonly ProviderUsageContract Usage = new(
        Q(1), Q(32), Q(16), Q(4), Q(0), Q(0), Q(0), Q(42),
        DomainProviderAvailabilityState.Available,
        DomainProviderAvailabilityState.Available,
        DomainProviderAvailabilityState.Unavailable);

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderContractsRoundTripCanonicallyAcrossAllNineSchemas()
    {
        AssertCanonical(
            new ProviderAccessProfileDocument(
                ContractConstants.ProviderAccessProfileSchemaId, "1", Id("profile-1"), Id("generation-1"), 1, 0,
                "openai", "responses", "Synthetic profile", ProviderProfileState.ActiveVerified,
                DomainProviderAvailabilityState.Available, Id("account-1"), Id("billing-1"), Id("capability-1"),
                Id("intent-1"), "not-required", "not-requested", RecordedAt),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeAccessProfile);
        AssertCanonical(
            new ProviderOperationDocument(
                ContractConstants.ProviderOperationSchemaId, "1", Id("operation-1"), Id("run-1"), "analysis-run",
                DomainProviderOperationKind.SourceClaimExtraction, Id("job-1"), null, null, Id("profile-1"), Id("generation-1"), 0,
                Capability(), Price(), null, null, null, BlockedProof(), Limits,
                null, null, null, ProviderOperationState.InputBoundBlocked,
                "not-started", "not-available", CancelledUsage(), "not-started", "not-available", RecordedAt),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeOperation);
        AssertCanonical(
            new ProviderResponseDocument(
                ContractConstants.ProviderResponseSchemaId, "1", Id("response-1"), Id("operation-1"), Id("request-1"),
                Ref("payload-1"), 512, 200, "provider-response-1", ProviderResponseState.Completed,
                null, null, null, "gpt-5.6-sol", "gpt-5.6-sol", "default", "default", "current_turn",
                "standard", "explicit", Usage, ProposalAdmissionState.Admitted, ProposalAdmissionState.Admitted, RecordedAt),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeResponse);
        AssertCanonical(
            new SourceClaimExtractionDocument(
                ContractConstants.SourceClaimExtractionSchemaId, "1", Id("acquisition-1"), Id("operation-1"),
                Id("source-1"), [Id("passage-1")], "Synthetic contract-shape example",
                [new(Id("proposal-1"), Id("passage-1"), "Synthetic proposed claim", [], ProposalAdmissionState.Proposed, "Requires host validation")],
                [], ["No semantic truth is supplied"], ["Host validation pending"], [], []),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeSourceClaimExtraction);
        AssertCanonical(
            new CandidateInvestigationDocument(
                ContractConstants.CandidateInvestigationSchemaId, "1", Id("operation-2"), Id("candidate-1"),
                [Id("participant-1")], ["synthetic-input"], [], Id("closure-1"), [],
                [new(Id("hypothesis-1"), Id("candidate-1"), "Synthetic untrusted hypothesis", [], [],
                    ["Independent evidence"], ProposalAdmissionState.Proposed, "Requires host validation")],
                ["No semantic truth is supplied"], ["Evidence absent"], [], []),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeCandidateInvestigation);
        AssertCanonical(
            new ProviderExecutionInputDocument(
                ContractConstants.ProviderExecutionInputSchemaId, "1", Id("operation-1"), Id("run-1"), Id("snapshot-1"),
                Id("context-1"), Id("config-2"), Id("manifest-1"), Id("profile-1"), Id("generation-1"),
                Capability(), Price(), Limits, Id("prompt-1"), Fingerprint, Id("schema-1"),
                Fingerprint, DomainProviderOperationKind.SourceClaimExtraction, Fingerprint, BlockedProof(), "blocked-authority-required"),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeExecutionInput);
        AssertCanonical(
            new EffectiveScanConfigurationV2Document(
                ContractConstants.EffectiveScanConfigurationV2SchemaId, "1", Id("config-2"), Id("config-1"), Fingerprint,
                Id("profile-1"), Id("generation-1"), "gpt-5.6-sol", "medium", "current_turn", "standard",
                false, "default", false, false, "none", 0, "disabled", "explicit", false, false, Limits,
                ["hosted-search", "nexus", "loot"]),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeEffectiveConfigurationV2);
        AssertCanonical(
            new RunOutputV2Document(
                ContractConstants.RunOutputV2SchemaId, "1", Id("run-1"), Ref("run-output-v1"), Id("config-2"),
                [new(Id("operation-1"), DomainProviderOperationKind.SourceClaimExtraction, Id("acquisition-1"), Id("authorization-1"), Id("response-1"),
                    Id("admission-1"), Id("usage-1"), Id("settlement-1"), Id("replay-1"), "retained", false)],
                [Id("acquisition-1")], [], [], ["Synthetic gap"], false, false),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeRunOutputV2);
        AssertCanonical(
            new CliSummaryV2Document(
                ContractConstants.CliSummaryV2SchemaId, "1", Id("run-1"), Fingerprint, "completed", Q(1), Q(32), Q(16), Q(4),
                Q(0), Q(0), Q(42), Q(100), false, "retained-response", ["Synthetic gap"], false, false),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeCliSummaryV2);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderResponseRoundTripsRetainedFailureAndCancelledAbsenceExactly()
    {
        AssertCanonical(
            new ProviderResponseDocument(
                ContractConstants.ProviderResponseSchemaId, "1", Id("response-failed"), Id("operation-failed"), Id("request-failed"),
                Ref("payload-failed"), 768, 429, "provider-response-failed", ProviderResponseState.Failed,
                null, null, "rate-limit", "gpt-5.6-sol", "gpt-5.6-sol", "default", "default", "current_turn",
                "standard", "explicit", Usage, ProposalAdmissionState.Rejected, ProposalAdmissionState.Rejected, RecordedAt),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeResponse);
        AssertCanonical(
            new ProviderResponseDocument(
                ContractConstants.ProviderResponseSchemaId, "1", Id("response-cancelled"), Id("operation-cancelled"), Id("request-cancelled"),
                null, null, null, null, ProviderResponseState.Cancelled,
                null, null, null, "gpt-5.6-sol", null, "default", null, "current_turn",
                "standard", "explicit", CancelledUsage(), ProposalAdmissionState.Unavailable, ProposalAdmissionState.Unavailable, RecordedAt),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeResponse);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void ProviderSchemasForbidSecretAndRawTransportFieldsStructurally()
    {
        foreach (string schema in SchemaNames)
        {
            byte[] original = File.ReadAllBytes(TestRepository.PathFromRoot("contracts", "json-schema", schema));
            using JsonDocument document = JsonDocument.Parse(original);
            JsonElement properties = document.RootElement.GetProperty("properties");
            foreach (string forbidden in new[] { "credential_target", "provider_secret", "authorization_header", "secret_bytes", "raw_headers" })
            {
                Assert.IsFalse(properties.TryGetProperty(forbidden, out _), $"{schema}:{forbidden}");
            }

            using JsonDocument sample = JsonDocument.Parse("{\"schema_id\":\"invalid\",\"credential_target\":\"forbidden\"}");
            Assert.ThrowsExactly<InvalidDataException>(() =>
                Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(sample.RootElement, schema));
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderConditionalSchemasRejectCrossStateAndCrossKindSubstitution()
    {
        JsonObject examples = JsonNode.Parse(File.ReadAllText(TestRepository.PathFromRoot(
            "fixtures", "public", "contracts", "provider-wp1", "contract-examples.v1.json")))!
            ["examples"]!.AsObject();
        JsonObject operation = examples["provider-operation.v1.schema.json"]!.DeepClone().AsObject();
        operation["state"] = "proposed";
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeOperation(
            System.Text.Encoding.UTF8.GetBytes(operation.ToJsonString())));

        operation["usage"]!["dispatch_count"]!["value"] = 1;
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeOperation(
            System.Text.Encoding.UTF8.GetBytes(operation.ToJsonString())));

        operation = examples["provider-operation.v1.schema.json"]!.DeepClone().AsObject();
        operation["operation_kind"] = "transport-qualification";
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeOperation(
            System.Text.Encoding.UTF8.GetBytes(operation.ToJsonString())));

        JsonObject response = examples["provider-response.v1.schema.json"]!.DeepClone().AsObject();
        response.Remove("returned_model");
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeResponse(
            System.Text.Encoding.UTF8.GetBytes(response.ToJsonString())));

        JsonObject output = examples["run-output.v2.schema.json"]!.DeepClone().AsObject();
        output["provider_operations"]![0]!["operation_kind"] = "transport-qualification";
        Assert.ThrowsExactly<InvalidOperationException>(() => ProviderContractJsonCodecs.DeserializeRunOutputV2(
            System.Text.Encoding.UTF8.GetBytes(output.ToJsonString())));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void HelperV2ProtocolFingerprintIsExactAndV1RemainsDecodableAuthority()
    {
        string v2 = TestRepository.PathFromRoot("contracts", "protobuf", "infinium", "helper", "v2", "helper.proto");
        string v1 = TestRepository.PathFromRoot("contracts", "protobuf", "infinium", "helper", "v1", "helper.proto");
        Assert.IsTrue(File.Exists(v1));
        Assert.AreEqual(HelperProtocolV2Constants.SchemaFingerprintSha256, HelperV2TransitiveFingerprint());
        Assert.AreEqual(
            ProtobufContractSetFingerprint(),
            Convert.ToHexStringLower(Infinium.Application.Runtime.ProtocolConstants.Version.SchemaFingerprintSha256.Span));
        StringAssert.Contains(File.ReadAllText(v1), "package infinium.helper.v1;");
        StringAssert.Contains(File.ReadAllText(v2), "package infinium.helper.v2;");

        V1Frame v1Frame = new() { Sequence = 7 };
        Assert.AreEqual(7UL, HelperProtocolV2Codec.DecodeV1(v1Frame.ToByteArray()).Sequence);
        V2Frame v2Frame = ValidV2BootstrapFrame();
        Assert.AreEqual(8UL, HelperProtocolV2Codec.Decode(v2Frame.ToByteArray()).Sequence);
        Assert.AreNotEqual(HelperProtocolV2Constants.SchemaFingerprintSha256, ProtobufContractSetFingerprint());
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void HelperV2DecoderRejectsNestedUnknownNumericAndCrossFieldContradictions()
    {
        V2Frame valid = ValidV2BootstrapFrame();
        byte[] topLevelUnknown = valid.ToByteArray().Concat(new byte[] { 0x98, 0x06, 0x01 }).ToArray();
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(topLevelUnknown));

        byte[] nested = valid.Bootstrap.ToByteArray().Concat(new byte[] { 0x98, 0x06, 0x01 }).ToArray();
        using MemoryStream malformed = new();
        malformed.Write([0x08, 0x08, 0x12, 0x20]);
        malformed.Write(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256));
        malformed.WriteByte(0x52);
        malformed.WriteByte((byte)nested.Length);
        malformed.Write(nested);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(malformed.ToArray()));

        V2Frame numeric = new()
        {
            Sequence = 9,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Receipt = new V2Receipt
            {
                OperationId = new OperationId { Value = "operation-1" },
                AttemptId = new AttemptId { Value = "attempt-1" },
                Outcome = (V2Outcome)999,
                AssignmentKind = V2AssignmentKind.ProviderDispatch,
            },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(numeric.ToByteArray()));
        numeric.Receipt.Outcome = V2Outcome.Completed;
        numeric.Receipt.TransportMayHaveStarted = false;
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(numeric.ToByteArray()));

        V2Frame contradictoryAssignment = new()
        {
            Sequence = 11,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Assignment = new V2Assignment
            {
                OperationId = new OperationId { Value = "operation-1" },
                AttemptId = new AttemptId { Value = "attempt-1" },
                AccessProfileId = new ProviderAccessProfileId { Value = "profile-1" },
                GenerationId = new CredentialGenerationId { Value = "generation-1" },
                GenerationOrdinal = 1,
                AssignmentKind = V2AssignmentKind.Enroll,
                OperationKind = V2OperationKind.SourceClaimExtraction,
                Limits = new V2Limits(),
                ProviderRequest = new V2ProviderRequest(),
            },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(contradictoryAssignment.ToByteArray()));

        V2Frame credentialAssignment = new()
        {
            Sequence = 12,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Assignment = new V2Assignment
            {
                OperationId = new OperationId { Value = "credential-operation" },
                AttemptId = new AttemptId { Value = "credential-attempt" },
                AccessProfileId = new ProviderAccessProfileId { Value = "profile-1" },
                GenerationId = new CredentialGenerationId { Value = "generation-1" },
                GenerationOrdinal = 1,
                AssignmentKind = V2AssignmentKind.Enroll,
            },
        };
        Assert.AreEqual(V2AssignmentKind.Enroll, HelperProtocolV2Codec.Decode(credentialAssignment.ToByteArray()).Assignment.AssignmentKind);
        byte[] requestBytes = [1, 2, 3];
        V2Frame blockedDispatch = credentialAssignment.Clone();
        blockedDispatch.Assignment.AssignmentKind = V2AssignmentKind.ProviderDispatch;
        blockedDispatch.Assignment.OperationKind = V2OperationKind.SourceClaimExtraction;
        blockedDispatch.Assignment.Limits = HelperLimits();
        blockedDispatch.Assignment.ProviderRequest = new V2ProviderRequest
        {
            DispatchId = new DispatchId { Value = "dispatch-1" },
            CanonicalRequestBytes = ByteString.CopyFrom(requestBytes),
            CanonicalRequest = Digest(requestBytes),
            CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
            PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
            ReservationGroupId = new ReservationGroupId { Value = "reservation-1" },
            DispatchDeadline = new Instant { UnixSeconds = 1 },
            EndpointIdentity = Infinium.Contracts.Protobuf.Helper.V2.ProviderEndpointV2.OpenaiResponses,
            InputBoundProof = new V2InputBoundProof
            {
                PolicyId = "unresolved-openai-responses-framing",
                PolicyVersion = "authority-required",
                Status = V2InputBoundProofStatus.AuthorityRequired,
            },
        };
        Assert.ThrowsExactly<NotSupportedException>(() => HelperProtocolV2Codec.Decode(blockedDispatch.ToByteArray()));
        blockedDispatch.Assignment.ProviderRequest.CanonicalRequest.Value = ByteString.CopyFrom(new byte[32]);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(blockedDispatch.ToByteArray()));
        V2Frame credentialReceipt = new()
        {
            Sequence = 13,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Receipt = new V2Receipt
            {
                OperationId = new OperationId { Value = "credential-operation" },
                AttemptId = new AttemptId { Value = "credential-attempt" },
                AssignmentKind = V2AssignmentKind.Enroll,
                Outcome = V2Outcome.Completed,
                NonSecretReceipt = Digest(),
            },
        };
        Assert.IsNull(HelperProtocolV2Codec.Decode(credentialReceipt.ToByteArray()).Receipt.RawResponse);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void HelperV2FinalRevalidationRetainsEveryAcceptedBindingAndApplicationWireIsBounded()
    {
        V2Frame frame = new()
        {
            Sequence = 10,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            DispatchRevalidation = new V2Revalidation
            {
                DispatchId = new DispatchId { Value = "dispatch-1" },
                AttemptId = new AttemptId { Value = "attempt-1" },
                CoordinatorFencingEpoch = 1,
                AccessProfileId = new ProviderAccessProfileId { Value = "profile-1" },
                GenerationId = new CredentialGenerationId { Value = "generation-1" },
                RevocationEpoch = 0,
                ReservationGroupId = new ReservationGroupId { Value = "reservation-1" },
                CanonicalRequest = Digest(),
                AuthorizedOnce = false,
                Disposition = V2Disposition.Rejected,
                AccountIdentityId = new ProviderAccountIdentityId { Value = "account-1" },
                BillingScopeIdentityId = new BillingScopeIdentityId { Value = "billing-1" },
                EffectiveConfigurationId = "configuration-1",
                CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
                PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
                Settings = Digest(),
                OutputSchema = Digest(),
                OperationKind = V2OperationKind.SourceClaimExtraction,
                InputBoundProof = new V2InputBoundProof
                {
                    PolicyId = "unresolved-openai-responses-framing",
                    PolicyVersion = "authority-required",
                    Status = V2InputBoundProofStatus.AuthorityRequired,
                },
                DispatchDeadline = new Instant { UnixSeconds = 1 },
                Limits = HelperLimits(),
            },
        };
        Assert.AreEqual("account-1", HelperProtocolV2Codec.Decode(frame.ToByteArray()).DispatchRevalidation.AccountIdentityId.Value);
        frame.DispatchRevalidation.PriceSnapshotId = null;
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(frame.ToByteArray()));

        ListProviderBudgetRequest query = new() { ScopeKind = "global", ScopeId = "global", RequestedPageSize = 100 };
        Assert.IsTrue(query.CalculateSize() < ProtocolConstants.MaximumMessageBytes);
        byte[] replayBytes = [4, 5, 6];
        ProviderReplayPayload replay = new()
        {
            OperationId = new OperationId { Value = "operation-1" },
            RetainedResponseId = "response-1",
            ReplayState = ProviderReplayState.RetainedResponse,
            DependencyManifestId = "manifest-1",
            NetworkPermitted = false,
            InstallationSnapshotId = "install-1",
            AnalysisContextId = "context-1",
            EffectiveConfigurationId = "config-1",
            ResolvedInputManifestId = "manifest-1",
            PromptId = "prompt-1",
            PromptFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
            OutputSchemaId = "schema-1",
            OutputSchemaFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
            CanonicalRequestBytes = ByteString.CopyFrom(replayBytes),
            CanonicalRequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData(replayBytes)),
            SettingsFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
            ProfileId = new ProviderAccessProfileId { Value = "profile-1" },
            GenerationId = new CredentialGenerationId { Value = "generation-1" },
            CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
            PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
            Limits = ApplicationLimits(),
            DispatchDeadline = new Instant { UnixSeconds = 1 },
            AuthorizationId = "authorization-1",
            AttemptId = new AttemptId { Value = "attempt-1" },
            RequestId = "request-1",
            ReservationId = new ReservationGroupId { Value = "reservation-1" },
            DispatchFenceId = new DispatchId { Value = "fence-1" },
            UsageEntryId = "usage-1",
            SettlementId = "settlement-1",
            ReplayEdgeId = "replay-1",
            OperationKind = Infinium.Contracts.Protobuf.Application.V1.ProviderOperationKind.SourceClaimExtraction,
        };
        Assert.IsFalse(ProviderReplayPayload.Parser.ParseFrom(replay.ToByteArray()).NetworkPermitted);

        SubmitProviderOperationRequest submit = new()
        {
            OperationId = new OperationId { Value = "operation-1" },
            ProfileId = new ProviderAccessProfileId { Value = "profile-1" },
            GenerationId = new CredentialGenerationId { Value = "generation-1" },
            OperationKind = Infinium.Contracts.Protobuf.Application.V1.ProviderOperationKind.SourceClaimExtraction,
            CanonicalRequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData(replayBytes)),
            CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
            PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
            RevocationEpoch = 7,
            SettingsFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
            OutputSchemaId = "schema-1",
            OutputSchemaFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
            Limits = ApplicationLimits(),
            OwnerKind = "analysis-run",
            OwnerId = "run-1",
            JobNodeId = "job-1",
            InputBoundProofStatus = InputBoundProofStatus.AuthorityRequired,
            InputBoundPolicyId = "unresolved-openai-responses-framing",
            InputBoundPolicyVersion = "authority-required",
            InstallationSnapshotId = "install-1",
            AnalysisContextId = "context-1",
            EffectiveConfigurationId = "config-1",
            ResolvedInputManifestId = "manifest-1",
            PromptId = "prompt-1",
            PromptFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
            CanonicalRequestBody = ByteString.CopyFrom(replayBytes),
            DispatchDeadline = new Instant { UnixSeconds = 1 },
            CoordinatorFencingEpoch = 1,
        };
        SubmitProviderOperationRequest submitRoundTrip = SubmitProviderOperationRequest.Parser.ParseFrom(submit.ToByteArray());
        Assert.ThrowsExactly<NotSupportedException>(() => ApplicationProviderContractValidator.Validate(submitRoundTrip));
        Assert.ThrowsExactly<NotSupportedException>(() => ApplicationProviderContractValidator.RequireDispatchAdmission(submitRoundTrip));
        submitRoundTrip.OperationKind = (Infinium.Contracts.Protobuf.Application.V1.ProviderOperationKind)999;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(submitRoundTrip));

        ApplicationProviderContractValidator.Validate(query);
        query.RequestedPageSize = 101;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(query));
        query.RequestedPageSize = 1;
        query.After = new PageCursor { OpaqueValue = ByteString.CopyFrom(new byte[513]) };
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(query));

        SubmitProviderEnrollmentRequest enrollment = new()
        {
            ProfileId = new ProviderAccessProfileId { Value = "profile-1" },
            GenerationId = new CredentialGenerationId { Value = "generation-1" },
            Provider = "openai",
            Purpose = "responses",
            DisplayLabel = "Synthetic",
        };
        ApplicationProviderContractValidator.Validate(enrollment);
        enrollment.Provider = "unknown";
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(enrollment));

        ProviderProfilePayload profile = new()
        {
            ProfileId = new ProviderAccessProfileId { Value = "profile-1" },
            GenerationId = new CredentialGenerationId { Value = "generation-1" },
            LifecycleState = AppProviderProfileLifecycleState.ActiveVerified,
            VerificationState = AppProviderAvailabilityState.Available,
        };
        ApplicationProviderContractValidator.Validate(profile);
        profile.VerificationState = (AppProviderAvailabilityState)999;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(profile));

        ProviderOperationPayload reserved = new()
        {
            OperationId = new OperationId { Value = "operation-1" },
            ProfileId = new ProviderAccessProfileId { Value = "profile-1" },
            GenerationId = new CredentialGenerationId { Value = "generation-1" },
            CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
            PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
            OperationKind = Infinium.Contracts.Protobuf.Application.V1.ProviderOperationKind.SourceClaimExtraction,
            State = ProviderOperationLifecycleState.Reserved,
            SettlementState = ProviderSettlementState.NotStarted,
            ReplayState = ProviderReplayState.NotAvailable,
            InputBoundProofStatus = InputBoundProofStatus.Proved,
            CanonicalRequestBytes = 3,
            ProvedInputTokenBound = 3,
            OwnerKind = "analysis-run",
            OwnerId = "run-1",
            JobNodeId = "job-1",
            AuthorizationId = "authorization-1",
            AttemptId = new AttemptId { Value = "attempt-1" },
            RequestId = "request-1",
            ReservationId = new ReservationGroupId { Value = "reservation-1" },
            InputTokens = UnavailableApplicationQuantity(),
            OutputTokens = UnavailableApplicationQuantity(),
            CalculatedNanoUsd = UnavailableApplicationQuantity(),
            DispatchCount = UnavailableApplicationQuantity(),
            ReasoningTokens = UnavailableApplicationQuantity(),
            CacheReadTokens = UnavailableApplicationQuantity(),
            CacheWriteTokens = UnavailableApplicationQuantity(),
            ReservedNanoUsd = new OptionalProviderQuantity { Availability = AppProviderAvailabilityState.Available, Value = 5 },
        };
        ApplicationProviderContractValidator.Validate(reserved);
        reserved.SettlementState = ProviderSettlementState.Settled;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(reserved));
        reserved.SettlementState = ProviderSettlementState.NotStarted;
        reserved.OperationKind = (Infinium.Contracts.Protobuf.Application.V1.ProviderOperationKind)999;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(reserved));

        ApplicationProviderContractValidator.Validate(replay);
        replay.NetworkPermitted = true;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(replay));
        replay.NetworkPermitted = false;
        replay.ReplayState = ProviderReplayState.NotAvailable;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(replay));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderTraceabilityInventoryResolvesEveryFieldAcrossAllSixSeams()
    {
        using JsonDocument inventory = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
            "docs", "plans", "milestones", "m1", "slices", "s6", "wp1-contract-traceability.v1.json")));
        JsonElement[] contracts = inventory.RootElement.GetProperty("contracts").EnumerateArray().ToArray();
        Assert.AreEqual(9, contracts.Length);
        CollectionAssert.AreEquivalent(
            SchemaNames,
            contracts.Select(x => x.GetProperty("schema").GetString()!).ToArray());

        string migration = File.ReadAllText(TestRepository.PathFromRoot(
            "src", "Infinium.Persistence", "AuthoritativeStore.Migrations.cs"));
        foreach (JsonElement contract in contracts)
        {
            string schemaName = contract.GetProperty("schema").GetString()!;
            using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(
                TestRepository.PathFromRoot("contracts", "json-schema", schemaName)));
            HashSet<string> declared = [];
            CollectDeclaredFields(schema.RootElement, string.Empty, declared);
            JsonElement[] mappings = contract.GetProperty("field_mappings").EnumerateArray().ToArray();
            string[] mappedPaths = mappings.Select(x => x.GetProperty("path").GetString()!).ToArray();
            Assert.AreEqual(mappedPaths.Length, mappedPaths.Distinct(StringComparer.Ordinal).Count(), schemaName);
            HashSet<string> mapped = mappedPaths.ToHashSet(StringComparer.Ordinal);
            Assert.IsTrue(declared.SetEquals(mapped), schemaName);

            foreach (JsonElement mapping in mappings)
            {
                string path = mapping.GetProperty("path").GetString()!;
                string[] authority = mapping.GetProperty("authorities").EnumerateArray().Select(x => x.GetString()!).ToArray();
                Assert.IsTrue(authority.Length > 0 && authority.All(AcceptedAuthorityIds.Contains), $"{schemaName}:{path}:authority");
                ResolveSourceSymbol(mapping.GetProperty("producer"), schemaName, path, "producer");
                ResolveSourceSymbol(mapping.GetProperty("consumer"), schemaName, path, "consumer");
                ResolvePersistence(mapping.GetProperty("persistence"), migration, schemaName, path);
                ResolveProjection(mapping.GetProperty("output"), schemaName, path, "output");
                ResolveProjection(mapping.GetProperty("replay"), schemaName, path, "replay");
            }
        }

        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "job_node_id", "provider_operation_authorizations.job_node_id");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "request_id", "provider_requests.request_id");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "raw_response_payload",
            "provider_responses.raw_response_payload_id", "provider_responses.raw_response_fingerprint");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "$defs.capabilitySnapshot.model", "provider_capability_snapshots.model", "ADR-0020");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "$defs.priceSnapshot.model", "provider_price_snapshots.model", "ADR-0020");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "$defs.priceRule.rule_id", "provider_price_rules.rule_id", "ADR-0020");
    }

    private static void ResolveSourceSymbol(JsonElement seam, string schema, string path, string kind)
    {
        string file = seam.GetProperty("file").GetString()!;
        string symbol = seam.GetProperty("symbol").GetString()!;
        string absolute = TestRepository.PathFromRoot(file.Split('/'));
        Assert.IsTrue(File.Exists(absolute), $"{schema}:{path}:{kind}:{file}");
        StringAssert.Contains(File.ReadAllText(absolute), symbol, $"{schema}:{path}:{kind}:{symbol}");
    }

    private static void ResolvePersistence(JsonElement seam, string migration, string schema, string path)
    {
        bool mapped = seam.TryGetProperty("table_column", out JsonElement tableColumn)
            || seam.TryGetProperty("table_columns", out tableColumn);
        bool omitted = seam.TryGetProperty("not_persisted_reason", out JsonElement reason)
            && !string.IsNullOrWhiteSpace(reason.GetString());
        Assert.AreNotEqual(mapped, omitted, $"{schema}:{path}:persistence-choice");
        if (!mapped)
        {
            return;
        }
        string[] columns = tableColumn.ValueKind == JsonValueKind.Array
            ? tableColumn.EnumerateArray().Select(x => x.GetString()!).ToArray()
            : [tableColumn.GetString()!];
        foreach (string column in columns)
        {
            string[] parts = column.Split('.', 2);
            Assert.AreEqual(2, parts.Length, $"{schema}:{path}:table.column");
            Match table = Regex.Match(migration, $@"CREATE TABLE {Regex.Escape(parts[0])}\((?<body>.*?)\) STRICT;", RegexOptions.Singleline);
            Assert.IsTrue(table.Success, $"{schema}:{path}:{parts[0]}");
            Assert.IsTrue(Regex.IsMatch(table.Groups["body"].Value, $@"(?m)^\s*{Regex.Escape(parts[1])}\s+"),
                $"{schema}:{path}:{column}");
        }
    }

    private static void ResolveProjection(JsonElement seam, string schema, string path, string kind)
    {
        bool mapped = seam.TryGetProperty("file", out JsonElement file);
        bool omitted = seam.TryGetProperty("omission_reason", out JsonElement reason)
            && !string.IsNullOrWhiteSpace(reason.GetString());
        Assert.AreNotEqual(mapped, omitted, $"{schema}:{path}:{kind}-choice");
        if (!mapped)
        {
            return;
        }
        string proto = File.ReadAllText(TestRepository.PathFromRoot(file.GetString()!.Split('/')));
        string message = seam.GetProperty("message").GetString()!;
        string field = seam.GetProperty("field").GetString()!;
        Match block = Regex.Match(proto, $@"message\s+{Regex.Escape(message)}\s*\{{(?<body>.*?)^\}}", RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(block.Success, $"{schema}:{path}:{kind}:{message}");
        Assert.IsTrue(Regex.IsMatch(block.Groups["body"].Value, $@"(?m)^\s*(?:optional\s+|repeated\s+)?[\w.]+\s+{Regex.Escape(field)}\s*="),
            $"{schema}:{path}:{kind}:{message}.{field}");
    }

    private static void AssertTraceMapping(JsonElement[] contracts, string schema, string path, string tableColumn, string? secondOrAuthority = null)
    {
        JsonElement mapping = contracts.Single(x => x.GetProperty("schema").GetString() == schema)
            .GetProperty("field_mappings").EnumerateArray().Single(x => x.GetProperty("path").GetString() == path);
        JsonElement persistence = mapping.GetProperty("persistence");
        string[] columns = persistence.TryGetProperty("table_columns", out JsonElement multiple)
            ? multiple.EnumerateArray().Select(x => x.GetString()!).ToArray()
            : [persistence.GetProperty("table_column").GetString()!];
        CollectionAssert.Contains(columns, tableColumn, $"{schema}:{path}");
        if (secondOrAuthority is not null && secondOrAuthority.Contains('.', StringComparison.Ordinal))
        {
            CollectionAssert.Contains(columns, secondOrAuthority, $"{schema}:{path}");
        }
        else if (secondOrAuthority is not null)
        {
            Assert.IsTrue(mapping.GetProperty("authorities").EnumerateArray().Any(x => x.GetString() == secondOrAuthority), $"{schema}:{path}:{secondOrAuthority}");
        }
    }

    private static readonly string[] SchemaNames =
    [
        "provider-access-profile.v1.schema.json", "provider-operation.v1.schema.json",
        "provider-response.v1.schema.json", "source-claim-extraction.v1.schema.json",
        "candidate-investigation.v1.schema.json", "provider-execution-input.v1.schema.json",
        "effective-scan-configuration.v2.schema.json", "run-output.v2.schema.json",
        "cli-summary.v2.schema.json",
    ];

    private static readonly HashSet<string> AcceptedAuthorityIds =
    [
        "AI-001", "AI-002", "AI-003", "AI-004", "AI-005", "AI-006", "AI-007",
        "OPS-001", "OPS-002", "SCAN-003", "SCAN-004", "SCAN-005", "AUTH-002",
        "SEC-001", "SEC-002", "SEC-003", "SEC-004", "EVID-001", "EVID-002", "EVID-004", "EVID-007",
        "SNAP-005", "SNAP-006", "ADR-0001", "ADR-0002", "ADR-0013", "ADR-0015", "ADR-0016",
        "ADR-0017", "ADR-0018", "ADR-0019", "ADR-0020", "ADR-0021", "ADR-0022", "ADR-0023", "ADR-0025",
    ];

    private static void AssertCanonical<T>(T value, Func<T, byte[]> serialize, Deserialize<T> deserialize)
    {
        byte[] first = serialize(value);
        T roundTrip = deserialize(first);
        byte[] second = serialize(roundTrip);
        CollectionAssert.AreEqual(first, second);
    }

    private static ProviderIdentityReferenceContract Ref(string id) => new(Id(id), Fingerprint);
    private static ProviderCapabilitySnapshotContract Capability() => new(
        Id("capability-1"), Fingerprint, "openai", "gpt-5.6-sol", "default", "medium", "current_turn",
        "standard", false, false, false, "none", 0, "disabled", "explicit", false, false, 272_000, "synthetic-v1");
    private static ProviderPriceSnapshotContract Price() => new(
        Id("price-1"), Fingerprint, "openai", "gpt-5.6-sol", "default", "USD", "synthetic-v1",
        [new(Id("rule-1"), "openai", "gpt-5.6-sol", "default", "standard-under-272k", "ordinary-input",
            "input", "none", "global", "USD", 1, 1, "synthetic-v1")]);
    private static ProviderQuantityContract Q(long value) => new(DomainProviderAvailabilityState.Available, value);
    private static ProviderUsageContract CancelledUsage() => new(
        Q(0), U(), U(), U(), U(), U(), U(), U(),
        DomainProviderAvailabilityState.Unavailable,
        DomainProviderAvailabilityState.Unavailable,
        DomainProviderAvailabilityState.Unavailable);
    private static ProviderQuantityContract U() => new(DomainProviderAvailabilityState.Unavailable, null);
    private static OptionalProviderQuantity UnavailableApplicationQuantity() =>
        new() { Availability = AppProviderAvailabilityState.Unavailable };
    private static ProviderInputBoundProofContract BlockedProof() => new(
        ProviderOperationContractInvariants.LocalInputBoundPolicyId,
        ProviderOperationContractInvariants.LocalInputBoundPolicyVersion,
        ProviderInputBoundProofState.AuthorityRequired, null, null);
    private static OpaqueId Id(string value) => new(value);
    private delegate T Deserialize<T>(ReadOnlySpan<byte> bytes);

    private static void CollectDeclaredFields(JsonElement node, string prefix, HashSet<string> fields)
    {
        if (node.TryGetProperty("properties", out JsonElement properties))
        {
            foreach (JsonProperty property in properties.EnumerateObject())
            {
                string path = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                fields.Add(path);
                CollectDeclaredFields(property.Value, path, fields);
            }
        }
        if (node.TryGetProperty("$defs", out JsonElement definitions))
        {
            foreach (JsonProperty definition in definitions.EnumerateObject())
            {
                CollectDeclaredFields(definition.Value, $"$defs.{definition.Name}", fields);
            }
        }
    }

    private static string ProtobufContractSetFingerprint()
    {
        string root = TestRepository.PathFromRoot("contracts", "protobuf");
        using MemoryStream bytes = new();
        foreach (string path in Directory.EnumerateFiles(root, "*.proto", SearchOption.AllDirectories)
                     .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            bytes.Write(System.Text.Encoding.UTF8.GetBytes(relative + "\n"));
            bytes.Write(File.ReadAllBytes(path));
        }
        return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
    }

    private static string HelperV2TransitiveFingerprint()
    {
        string root = TestRepository.PathFromRoot("contracts", "protobuf");
        string[] relativePaths =
        [
            "infinium/common/v1/common.proto",
            "infinium/domain/v1/identities.proto",
            "infinium/helper/v2/helper.proto",
        ];
        using MemoryStream bytes = new();
        foreach (string relative in relativePaths)
        {
            bytes.Write(System.Text.Encoding.UTF8.GetBytes(relative + "\n"));
            bytes.Write(File.ReadAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))));
        }
        return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
    }

    private static V2Frame ValidV2BootstrapFrame() => new()
    {
        Sequence = 8,
        ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
        Bootstrap = new V2Bootstrap
        {
            OperationId = new OperationId { Value = "operation-1" },
            AttemptId = new AttemptId { Value = "attempt-1" },
            CoordinatorFencingEpoch = 1,
            OneUseNonceFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
        },
    };

    private static ContentDigest Digest() => new()
    {
        Algorithm = DigestAlgorithm.Sha256,
        Value = ByteString.CopyFrom(new byte[32]),
        SizeBytes = 1,
    };

    private static ContentDigest Digest(byte[] bytes) => new()
    {
        Algorithm = DigestAlgorithm.Sha256,
        Value = ByteString.CopyFrom(SHA256.HashData(bytes)),
        SizeBytes = (ulong)bytes.Length,
    };

    private static V2Limits HelperLimits() => new()
    {
        MaximumFrameBytes = HelperProtocolV2Constants.MaximumFrameBytes,
        MaximumRequestBytes = 65_536,
        MaximumResponseBytes = 1_048_576,
        MaximumStagedOutputBytes = 1_048_576,
        MaximumInputTokens = 73_728,
        MaximumOutputTokens = 4_096,
        MaximumCalculatedNanoUsd = 600_000_000,
        MaximumDuration = new DurationMillis { Value = 120_000 },
        MaximumDispatchCount = 1,
    };

    private static ProviderOperationLimits ApplicationLimits() => new()
    {
        MaximumRequestBytes = 65_536,
        MaximumInputTokens = 73_728,
        MaximumOutputTokens = 4_096,
        MaximumRawResponseBytes = 1_048_576,
        MaximumDispatchCount = 1,
        MaximumCalculatedNanoUsd = 600_000_000,
        DeadlineMilliseconds = 120_000,
    };
}
