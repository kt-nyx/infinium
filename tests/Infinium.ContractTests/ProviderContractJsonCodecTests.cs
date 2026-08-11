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
using V2CredentialSubject = Infinium.Contracts.Protobuf.Helper.V2.CredentialSubjectV2;
using V2DispatchSubject = Infinium.Contracts.Protobuf.Helper.V2.ProviderDispatchSubjectV2;
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
    private static readonly DateTimeOffset HelperNow = DateTimeOffset.Parse("2026-08-10T00:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture);
    private static readonly ProviderFiniteLimitsContract Limits = new(
        65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000);
    private static readonly ProviderUsageContract Usage = new(
        DomainProviderAvailabilityState.Available, Q(1), Q(32), Q(16), Q(48), Q(4), Q(0), Q(0), Q(0), Q(42),
        DomainProviderAvailabilityState.Unavailable,
        DomainProviderAvailabilityState.Unavailable,
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
                ContractConstants.ProviderOperationSchemaId, "1", Id("operation-1"), Id("acquisition-1"), "evidence-acquisition-run",
                DomainProviderOperationKind.SourceClaimExtraction, Id("job-1"), Id("command-1"), Id("snapshot-1"),
                Id("context-1"), Id("config-2"), Id("manifest-1"), Id("profile-1"), Id("generation-1"), 0,
                Capability(), Price(), Id("prompt-1"), Fingerprint, Id("schema-1"), Fingerprint, Fingerprint,
                Ref("canonical-request-1"), 1024, Fingerprint, BlockedProof(), Limits,
                ProviderOperationState.InputBoundBlocked,
                "not-started", "not-available", CancelledUsage(), "not-started", "not-available", RecordedAt,
                RecordedAt, UtcTimestamp.Parse("2026-08-10T00:02:00.0000000+00:00"), 1, RecordedAt),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeOperation);
        AssertCanonical(
            new ProviderResponseDocument(
                SchemaId: ContractConstants.ProviderResponseSchemaId, SchemaVersion: "1", ResponseRecordId: Id("response-1"),
                OperationId: Id("operation-1"), OwnerKind: "evidence-acquisition-run", OwnerId: Id("acquisition-1"),
                AuthorizationId: null, RequestId: null, DispatchFenceId: null,
                OperationKind: DomainProviderOperationKind.SourceClaimExtraction, Limits: Limits, InputBoundProof: BlockedProof(),
                Availability: DomainProviderAvailabilityState.Unavailable, RawResponseAvailability: DomainProviderAvailabilityState.Unavailable,
                RawResponsePayload: null, RawResponseBytes: null, MaximumRawResponseBytes: 1_048_576,
                OverflowObservedExcessBytes: null,
                ResponseHeadersPayload: null, ResponseHeadersBytes: null, ResponseHeadersAvailability: DomainProviderAvailabilityState.Unavailable,
                HttpStatus: null, HttpStatusAvailability: DomainProviderAvailabilityState.Unavailable,
                ProviderResponseId: null, ProviderResponseIdAvailability: DomainProviderAvailabilityState.Unavailable,
                ClientRequestId: null, ClientRequestIdAvailability: DomainProviderAvailabilityState.Unavailable,
                ProviderRequestId: null, ProviderRequestIdAvailability: DomainProviderAvailabilityState.Unavailable,
                State: ProviderResponseState.Unknown, RefusalCode: null, RefusalAvailability: DomainProviderAvailabilityState.Unavailable,
                IncompleteReason: null, IncompleteAvailability: DomainProviderAvailabilityState.Unavailable,
                ErrorCode: null, ErrorAvailability: DomainProviderAvailabilityState.Unavailable,
                RequestedModel: "gpt-5.6-sol", ReturnedModel: null, ReturnedModelAvailability: DomainProviderAvailabilityState.Unavailable,
                RequestedServiceTier: "default", ReturnedServiceTier: null, ReturnedServiceTierAvailability: DomainProviderAvailabilityState.Unavailable,
                ReasoningContext: "current_turn", ReasoningMode: "standard", PromptCacheMode: "explicit", Usage: CancelledUsage(),
                RateLimitFacts: [], BillingEvidencePayload: null, BillingEvidenceAvailability: DomainProviderAvailabilityState.Unavailable,
                ValidationState: ProposalAdmissionState.Unavailable, AdmissionState: ProposalAdmissionState.Unavailable, RecordedAt: RecordedAt),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeResponse);
        AssertCanonical(
            new SourceClaimExtractionDocument(
                ContractConstants.SourceClaimExtractionSchemaId, "1", Id("acquisition-1"), Id("operation-1"),
                "evidence-acquisition-run", Id("acquisition-1"), Id("run-1"), Id("application-scope-1"), Id("cost-scope-1"),
                Id("source-1"), [Id("passage-1")], "Synthetic contract-shape example",
                [new(Id("proposal-1"), Id("passage-1"), "Synthetic proposed claim", [], ProposalAdmissionState.Proposed, "Requires host validation")],
                [], ["No semantic truth is supplied"], ["Host validation pending"], [], [], []),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeSourceClaimExtraction);
        AssertCanonical(
            new CandidateInvestigationDocument(
                ContractConstants.CandidateInvestigationSchemaId, "1", Id("operation-2"), "analysis-run", Id("run-1"), Id("run-1"), Id("candidate-1"),
                [Id("participant-1")], ["synthetic-input"], [], Id("closure-1"), [],
                [new(Id("hypothesis-1"), Id("candidate-1"), "Synthetic untrusted hypothesis", [], [],
                    ["Independent evidence"], ProposalAdmissionState.Proposed, "Requires host validation")],
                ["No semantic truth is supplied"], ["Evidence absent"], [], [], []),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeCandidateInvestigation);
        AssertCanonical(
            new ProviderExecutionInputDocument(
                ContractConstants.ProviderExecutionInputSchemaId, "1", Id("operation-1"), "evidence-acquisition-run",
                Id("run-1"), Id("job-1"), Id("command-1"), Id("snapshot-1"),
                Id("context-1"), Id("config-2"), Id("manifest-1"), Id("profile-1"), Id("generation-1"),
                Capability(), Price(), Limits, Id("prompt-1"), Fingerprint, Id("schema-1"),
                Fingerprint, DomainProviderOperationKind.SourceClaimExtraction, Fingerprint, BlockedProof(), "blocked-authority-required"),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeExecutionInput);
        AssertCanonical(
            new EffectiveScanConfigurationV2Document(
                ContractConstants.EffectiveScanConfigurationV2SchemaId, "1", Id("config-2"), Id("config-1"), Fingerprint,
                "asserted-retained-v1-identity", Id("profile-1"), Id("generation-1"), "gpt-5.6-sol", "medium", "current_turn", "standard",
                false, "default", false, false, "none", 0, "disabled", "explicit", false, false, Limits,
                ["hosted-search", "nexus", "loot"]),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeEffectiveConfigurationV2);
        AssertCanonical(
            new RunOutputV2Document(
                ContractConstants.RunOutputV2SchemaId, "1", Id("run-1"), Ref("run-output-v1"), Id("config-2"),
                [new(Id("operation-1"), DomainProviderOperationKind.SourceClaimExtraction, Id("acquisition-1"), null, null,
                    null, null, null, null, "blocked", false)],
                [Id("acquisition-1")], [], [], ["Synthetic gap"], false, false),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeRunOutputV2);
        AssertCanonical(
            new CliSummaryV2Document(
                ContractConstants.CliSummaryV2SchemaId, "1", Id("run-1"), Fingerprint, "blocked", U(), U(), U(), U(),
                U(), U(), U(), U(), false, "not-available", ["Synthetic gap"], false, false),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeCliSummaryV2);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderResponseRejectsEverySyntheticTransportStateBeforeAuthority()
    {
        JsonObject response = JsonNode.Parse(File.ReadAllText(TestRepository.PathFromRoot(
            "fixtures", "public", "contracts", "provider-wp1", "contract-examples.v1.json")))!
            ["examples"]!["provider-response.v1.schema.json"]!.DeepClone().AsObject();
        foreach (string state in new[] { "completed", "refusal", "incomplete", "failed", "queued", "in-progress", "cancelled", "malformed", "oversized", "mismatched" })
        {
            response["state"] = state;
            Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeResponse(
                System.Text.Encoding.UTF8.GetBytes(response.ToJsonString())), state);
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderFutureResponseShapeIsProofQualifiedButCurrentRuntimeRejectsIt()
    {
        JsonObject response = JsonNode.Parse(File.ReadAllText(TestRepository.PathFromRoot(
            "fixtures", "public", "contracts", "provider-wp1", "contract-examples.v1.json")))!
            ["examples"]!["provider-response.v1.schema.json"]!.DeepClone().AsObject();
        response["authorization_id"] = "authorization-1";
        response["request_id"] = "request-1";
        response["dispatch_fence_id"] = "fence-1";
        response["input_bound_proof"] = new JsonObject
        {
            ["policy_id"] = "future-accepted-policy",
            ["policy_version"] = "future-accepted-version",
            ["status"] = "proved",
        };
        response["availability"] = "available";
        response["raw_response_availability"] = "available";
        response["raw_response_payload"] = IdentityReference("raw-response-1");
        response["raw_response_bytes"] = 512;
        response["response_headers_payload"] = IdentityReference("response-headers-1");
        response["response_headers_bytes"] = 128;
        response["response_headers_availability"] = "available";
        response["http_status_availability"] = "available";
        response["http_status"] = 200;
        response["provider_response_id"] = "provider-response-1";
        response["provider_response_id_availability"] = "available";
        response["client_request_id"] = "client-request-1";
        response["client_request_id_availability"] = "available";
        response["provider_request_id"] = "provider-request-1";
        response["provider_request_id_availability"] = "available";
        response["state"] = "completed";
        response["returned_model"] = "gpt-5.6-sol";
        response["returned_model_availability"] = "available";
        response["returned_service_tier"] = "default";
        response["returned_service_tier_availability"] = "available";
        response["validation_state"] = "admitted";
        response["admission_state"] = "admitted";
        response["usage"]!["dispatch_count"]!["value"] = 1;
        response["usage"]!["availability"] = "available";
        response["usage"]!["input_tokens"] = new JsonObject { ["availability"] = "available", ["value"] = 32 };
        response["usage"]!["output_tokens"] = new JsonObject { ["availability"] = "available", ["value"] = 16 };
        response["usage"]!["total_tokens"] = new JsonObject { ["availability"] = "available", ["value"] = 48 };
        response["usage"]!["reasoning_tokens"] = new JsonObject { ["availability"] = "available", ["value"] = 4 };
        response["usage"]!["cache_read_tokens"] = new JsonObject { ["availability"] = "available", ["value"] = 0 };
        response["usage"]!["cache_write_tokens"] = new JsonObject { ["availability"] = "available", ["value"] = 0 };
        response["usage"]!["priced_tool_calls"] = new JsonObject { ["availability"] = "available", ["value"] = 0 };
        response["usage"]!["calculated_nano_usd"] = new JsonObject { ["availability"] = "available", ["value"] = 42 };

        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(response.ToJsonString());
        using (JsonDocument document = JsonDocument.Parse(bytes))
        {
            Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(
                document.RootElement, "provider-response.v1.schema.json");
        }
        Assert.ThrowsExactly<NotSupportedException>(() => ProviderContractJsonCodecs.DeserializeResponse(bytes));

        JsonObject exactLimitUsage = response.DeepClone().AsObject();
        exactLimitUsage["limits"]!["maximum_input_tokens"] = 32;
        exactLimitUsage["limits"]!["maximum_output_tokens"] = 16;
        exactLimitUsage["limits"]!["maximum_calculated_nano_usd"] = 42;
        Assert.ThrowsExactly<NotSupportedException>(() => ProviderContractJsonCodecs.DeserializeResponse(
            System.Text.Encoding.UTF8.GetBytes(exactLimitUsage.ToJsonString())));
        JsonObject observedOverrun = exactLimitUsage.DeepClone().AsObject();
        observedOverrun["limits"]!["maximum_input_tokens"] = 16;
        observedOverrun["limits"]!["maximum_output_tokens"] = 8;
        observedOverrun["limits"]!["maximum_calculated_nano_usd"] = 40;
        byte[] observedOverrunBytes = System.Text.Encoding.UTF8.GetBytes(observedOverrun.ToJsonString());
        using (JsonDocument document = JsonDocument.Parse(observedOverrunBytes))
        {
            Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(
                document.RootElement, "provider-response.v1.schema.json");
        }
        Assert.ThrowsExactly<NotSupportedException>(() => ProviderContractJsonCodecs.DeserializeResponse(observedOverrunBytes));

        JsonObject oversized = response.DeepClone().AsObject();
        oversized["state"] = "oversized";
        oversized["raw_response_availability"] = "unavailable";
        oversized.Remove("raw_response_payload");
        oversized.Remove("raw_response_bytes");
        oversized["overflow_observed_excess_bytes"] = 1;
        oversized["validation_state"] = "rejected";
        oversized["admission_state"] = "rejected";
        byte[] oversizedBytes = System.Text.Encoding.UTF8.GetBytes(oversized.ToJsonString());
        using (JsonDocument document = JsonDocument.Parse(oversizedBytes))
        {
            Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(
                document.RootElement, "provider-response.v1.schema.json");
        }
        oversized["overflow_observed_excess_bytes"] = 2;
        Assert.ThrowsExactly<InvalidDataException>(() =>
        {
            using JsonDocument document = JsonDocument.Parse(oversized.ToJsonString());
            Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(
                document.RootElement, "provider-response.v1.schema.json");
        });

        JsonObject wrongQualification = response.DeepClone().AsObject();
        wrongQualification["operation_kind"] = "transport-qualification";
        Assert.ThrowsExactly<InvalidDataException>(() =>
        {
            using JsonDocument document = JsonDocument.Parse(wrongQualification.ToJsonString());
            Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(
                document.RootElement, "provider-response.v1.schema.json");
        });
        JsonObject qualificationLimits = wrongQualification["limits"]!.AsObject();
        wrongQualification["owner_kind"] = "analysis-run";
        qualificationLimits["maximum_request_bytes"] = 16_384;
        qualificationLimits["maximum_input_tokens"] = 20_480;
        qualificationLimits["maximum_output_tokens"] = 256;
        qualificationLimits["maximum_raw_response_bytes"] = 262_144;
        qualificationLimits["maximum_calculated_nano_usd"] = 140_000_000;
        qualificationLimits["deadline_milliseconds"] = 60_000;
        wrongQualification["maximum_raw_response_bytes"] = 262_144;
        using (JsonDocument document = JsonDocument.Parse(wrongQualification.ToJsonString()))
        {
            Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(
                document.RootElement, "provider-response.v1.schema.json");
        }
        JsonObject wrongSemanticOwner = response.DeepClone().AsObject();
        wrongSemanticOwner["owner_kind"] = "analysis-run";
        Assert.ThrowsExactly<InvalidDataException>(() =>
        {
            using JsonDocument document = JsonDocument.Parse(wrongSemanticOwner.ToJsonString());
            Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(
                document.RootElement, "provider-response.v1.schema.json");
        });
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

        operation["owner_kind"] = "analysis-run";
        operation["limits"]!["maximum_request_bytes"] = 16_384;
        operation["limits"]!["maximum_input_tokens"] = 20_480;
        operation["limits"]!["maximum_output_tokens"] = 256;
        operation["limits"]!["maximum_raw_response_bytes"] = 262_144;
        operation["limits"]!["maximum_calculated_nano_usd"] = 140_000_000;
        operation["limits"]!["deadline_milliseconds"] = 60_000;
        using (JsonDocument qualification = JsonDocument.Parse(operation.ToJsonString()))
        {
            Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(
                qualification.RootElement, "provider-operation.v1.schema.json");
        }
        JsonObject missingQualificationLimit = operation.DeepClone().AsObject();
        missingQualificationLimit["limits"]!.AsObject().Remove("maximum_output_tokens");
        Assert.ThrowsExactly<InvalidDataException>(() =>
        {
            using JsonDocument missing = JsonDocument.Parse(missingQualificationLimit.ToJsonString());
            Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(
                missing.RootElement, "provider-operation.v1.schema.json");
        });
        operation["limits"]!["unexpected_limit"] = 1;
        Assert.ThrowsExactly<InvalidDataException>(() =>
        {
            using JsonDocument extra = JsonDocument.Parse(operation.ToJsonString());
            Infinium.Application.Evaluation.ActiveJsonSchemaValidator.Validate(
                extra.RootElement, "provider-operation.v1.schema.json");
        });

        JsonObject response = examples["provider-response.v1.schema.json"]!.DeepClone().AsObject();
        response["state"] = "completed";
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeResponse(
            System.Text.Encoding.UTF8.GetBytes(response.ToJsonString())));

        JsonObject profile = examples["provider-access-profile.v1.schema.json"]!.DeepClone().AsObject();
        profile["lifecycle_state"] = "replacing";
        profile["verification_state"] = "unavailable";
        _ = ProviderContractJsonCodecs.DeserializeAccessProfile(
            System.Text.Encoding.UTF8.GetBytes(profile.ToJsonString()));
        profile["verification_state"] = "available";
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeAccessProfile(
            System.Text.Encoding.UTF8.GetBytes(profile.ToJsonString())));
        profile["lifecycle_state"] = "delete-pending";
        profile["verification_state"] = "unavailable";
        profile["cleanup_disposition"] = "failed";
        _ = ProviderContractJsonCodecs.DeserializeAccessProfile(
            System.Text.Encoding.UTF8.GetBytes(profile.ToJsonString()));

        JsonObject output = examples["run-output.v2.schema.json"]!.DeepClone().AsObject();
        output["provider_operations"]![0]!["operation_kind"] = "transport-qualification";
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeRunOutputV2(
            System.Text.Encoding.UTF8.GetBytes(output.ToJsonString())));
        output = examples["run-output.v2.schema.json"]!.DeepClone().AsObject();
        output["provider_operations"]![0]!["response_id"] = "response-invented";
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeRunOutputV2(
            System.Text.Encoding.UTF8.GetBytes(output.ToJsonString())));
        output = examples["run-output.v2.schema.json"]!.DeepClone().AsObject();
        output["provider_operations"]![0]!["accepted_input_bound_policy_version"] = "invented";
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeRunOutputV2(
            System.Text.Encoding.UTF8.GetBytes(output.ToJsonString())));

        JsonObject cli = examples["cli-summary.v2.schema.json"]!.DeepClone().AsObject();
        cli["accepted_input_bound_policy_version"] = "invented";
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeCliSummaryV2(
            System.Text.Encoding.UTF8.GetBytes(cli.ToJsonString())));

        JsonObject configuration = examples["effective-scan-configuration.v2.schema.json"]!.DeepClone().AsObject();
        configuration.Remove("local_configuration_v1_provenance");
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeEffectiveConfigurationV2(
            System.Text.Encoding.UTF8.GetBytes(configuration.ToJsonString())));
        configuration["local_configuration_v1_provenance"] = "content-validated-by-fingerprint-length";
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeEffectiveConfigurationV2(
            System.Text.Encoding.UTF8.GetBytes(configuration.ToJsonString())));
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
        Assert.AreEqual(8UL, DecodeBootstrap(v2Frame).Sequence);
        V2Frame reboundBootstrap = v2Frame.Clone();
        reboundBootstrap.Bootstrap.CommandId = "command-other";
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeBootstrap(reboundBootstrap));
        reboundBootstrap = v2Frame.Clone();
        reboundBootstrap.Bootstrap.ProviderDispatch.OperationId.Value = "operation-other";
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeBootstrap(reboundBootstrap));
        reboundBootstrap = v2Frame.Clone();
        reboundBootstrap.Bootstrap.CoordinatorFencingEpoch = 2;
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeBootstrap(reboundBootstrap));
        reboundBootstrap = v2Frame.Clone();
        reboundBootstrap.Bootstrap.OneUseNonceFingerprintSha256 = ByteString.CopyFrom(Enumerable.Repeat((byte)1, 32).ToArray());
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeBootstrap(reboundBootstrap));
        reboundBootstrap = v2Frame.Clone();
        reboundBootstrap.Bootstrap.ExpiresAt = ToInstant(HelperNow.AddSeconds(119));
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeBootstrap(reboundBootstrap));
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
            v2Frame.ToByteArray(), HelperNow, expectedCommandId: "command-1",
            expectedOperationId: "operation-1", expectedAttemptId: "attempt-1",
            expectedCoordinatorFencingEpoch: 1,
            expectedMaximumFrameBytes: (ulong)v2Frame.CalculateSize() - 1,
            expectedOneUseNonceFingerprintSha256: new byte[32], expectedBootstrapExpiresAt: FutureInstant()));
        Assert.AreNotEqual(HelperProtocolV2Constants.SchemaFingerprintSha256, ProtobufContractSetFingerprint());
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void HelperV2DecoderRejectsNestedUnknownNumericAndCrossFieldContradictions()
    {
        V2Frame valid = ValidV2BootstrapFrame();
        V2Frame expired = valid.Clone();
        expired.Bootstrap.ExpiresAt = ToInstant(HelperNow);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(expired.ToByteArray(), HelperNow));
        byte[] topLevelUnknown = valid.ToByteArray().Concat(new byte[] { 0x98, 0x06, 0x01 }).ToArray();
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(topLevelUnknown, HelperNow));

        byte[] nested = valid.Bootstrap.ToByteArray().Concat(new byte[] { 0x98, 0x06, 0x01 }).ToArray();
        using MemoryStream malformed = new();
        malformed.Write([0x08, 0x08, 0x12, 0x20]);
        malformed.Write(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256));
        malformed.WriteByte(0x52);
        malformed.WriteByte((byte)nested.Length);
        malformed.Write(nested);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(malformed.ToArray(), HelperNow));

        V2Frame numeric = new()
        {
            Sequence = 9,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Receipt = new V2Receipt
            {
                ProviderDispatch = DispatchSubject(),
                Outcome = (V2Outcome)999,
                AssignmentKind = V2AssignmentKind.ProviderDispatch,
            },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(numeric.ToByteArray(), HelperNow));
        numeric.Receipt.Outcome = V2Outcome.Completed;
        numeric.Receipt.TransportMayHaveStarted = false;
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(numeric.ToByteArray(), HelperNow));

        V2Frame contradictoryAssignment = new()
        {
            Sequence = 11,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Assignment = new V2Assignment
            {
                Credential = CredentialSubject(),
                AccessProfileId = new ProviderAccessProfileId { Value = "profile-1" },
                GenerationId = new CredentialGenerationId { Value = "generation-1" },
                GenerationOrdinal = 1,
                AssignmentId = "assignment-invalid",
                CommandId = "command-invalid",
                AssignmentKind = V2AssignmentKind.Enroll,
                OperationKind = V2OperationKind.SourceClaimExtraction,
                Limits = new V2Limits(),
                ProviderRequest = new V2ProviderRequest(),
            },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(contradictoryAssignment.ToByteArray(), HelperNow));

        V2Frame credentialAssignment = new()
        {
            Sequence = 12,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Assignment = new V2Assignment
            {
                Credential = CredentialSubject(),
                AccessProfileId = new ProviderAccessProfileId { Value = "profile-1" },
                GenerationId = new CredentialGenerationId { Value = "generation-1" },
                GenerationOrdinal = 1,
                AssignmentKind = V2AssignmentKind.Enroll,
                AssignmentId = "assignment-credential",
                CommandId = "command-credential",
            },
        };
        Assert.AreEqual(V2AssignmentKind.Enroll, HelperProtocolV2Codec.Decode(
            credentialAssignment.ToByteArray(), HelperNow, "assignment-credential", "command-credential",
            expectedProfileId: "profile-1", expectedGenerationId: "generation-1", expectedGenerationOrdinal: 1,
            expectedRevocationEpoch: 0,
            expectedPayloadCase: V2Frame.PayloadOneofCase.Assignment, expectedSequence: 12,
            expectedAssignmentKind: V2AssignmentKind.Enroll).Assignment.AssignmentKind);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
            credentialAssignment.ToByteArray(), HelperNow, "assignment-credential", "command-credential",
            expectedProfileId: "profile-1", expectedGenerationId: "generation-1", expectedGenerationOrdinal: 2,
            expectedRevocationEpoch: 0, expectedPayloadCase: V2Frame.PayloadOneofCase.Assignment,
            expectedSequence: 12, expectedAssignmentKind: V2AssignmentKind.Enroll));
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
            credentialAssignment.ToByteArray(), HelperNow, "assignment-credential", "command-credential",
            expectedProfileId: "profile-1", expectedGenerationId: "generation-1", expectedGenerationOrdinal: 1,
            expectedRevocationEpoch: 0,
            expectedPayloadCase: V2Frame.PayloadOneofCase.Receipt, expectedSequence: 12,
            expectedAssignmentKind: V2AssignmentKind.Enroll));
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
            credentialAssignment.ToByteArray(), HelperNow, "assignment-credential", "command-credential",
            expectedProfileId: "profile-1", expectedGenerationId: "generation-1", expectedGenerationOrdinal: 1,
            expectedRevocationEpoch: 0,
            expectedPayloadCase: V2Frame.PayloadOneofCase.Assignment, expectedSequence: 13,
            expectedAssignmentKind: V2AssignmentKind.Enroll));
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
            credentialAssignment.ToByteArray(), HelperNow, "assignment-credential", "command-credential",
            expectedProfileId: "profile-1", expectedGenerationId: "generation-1", expectedGenerationOrdinal: 1,
            expectedRevocationEpoch: 0,
            expectedPayloadCase: V2Frame.PayloadOneofCase.Assignment, expectedSequence: 12,
            expectedAssignmentKind: V2AssignmentKind.Verify));
        byte[] requestBytes = [1, 2, 3];
        V2Frame blockedDispatch = credentialAssignment.Clone();
        blockedDispatch.Assignment.AssignmentKind = V2AssignmentKind.ProviderDispatch;
        blockedDispatch.Assignment.ProviderDispatch = DispatchSubject();
        blockedDispatch.Assignment.OperationKind = V2OperationKind.SourceClaimExtraction;
        blockedDispatch.Assignment.Limits = HelperLimits();
        blockedDispatch.Assignment.AccountIdentityId = new ProviderAccountIdentityId { Value = "account-1" };
        blockedDispatch.Assignment.BillingScopeIdentityId = new BillingScopeIdentityId { Value = "billing-1" };
        blockedDispatch.Assignment.EffectiveConfigurationId = "config-v2-1";
        blockedDispatch.Assignment.Settings = Digest();
        blockedDispatch.Assignment.OutputSchema = Digest();
        blockedDispatch.Assignment.ProviderRequest = new V2ProviderRequest
        {
            DispatchId = new DispatchId { Value = "dispatch-1" },
            RequestId = "request-1",
            CanonicalRequestBytes = ByteString.CopyFrom(requestBytes),
            CanonicalRequest = Digest(requestBytes),
            RequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData(requestBytes)),
            CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
            PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
            ReservationGroupId = new ReservationGroupId { Value = "reservation-1" },
            DispatchDeadline = FutureInstant(),
            ConfirmedAt = ToInstant(HelperNow),
            EndpointIdentity = Infinium.Contracts.Protobuf.Helper.V2.ProviderEndpointV2.OpenaiResponses,
            InputBoundProof = new V2InputBoundProof
            {
                PolicyId = "unresolved-openai-responses-framing",
                PolicyVersion = "authority-required",
                Status = V2InputBoundProofStatus.AuthorityRequired,
            },
        };
        Assert.ThrowsExactly<NotSupportedException>(() => DecodeBlockedAssignment(blockedDispatch, requestBytes));
        V2Frame dispatchDeadlineOverflow = blockedDispatch.Clone();
        dispatchDeadlineOverflow.Assignment.ProviderRequest.DispatchDeadline = ToInstant(HelperNow.AddSeconds(121));
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeBlockedAssignment(dispatchDeadlineOverflow, requestBytes));
        V2Frame assignmentAccountRebind = blockedDispatch.Clone();
        assignmentAccountRebind.Assignment.AccountIdentityId.Value = "account-other";
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeBlockedAssignment(assignmentAccountRebind, requestBytes));
        V2Frame assignmentLimitOverflow = blockedDispatch.Clone();
        assignmentLimitOverflow.Assignment.Limits.MaximumInputTokens++;
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeBlockedAssignment(assignmentLimitOverflow, requestBytes));
        blockedDispatch.Assignment.ProviderRequest.CanonicalRequest.Value = ByteString.CopyFrom(new byte[32]);
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeBlockedAssignment(blockedDispatch, requestBytes));
        V2Frame credentialReceipt = new()
        {
            Sequence = 13,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Receipt = new V2Receipt
            {
                Credential = CredentialSubject(),
                AssignmentKind = V2AssignmentKind.Enroll,
                AssignmentId = "assignment-credential",
                CommandId = "command-credential",
                Outcome = V2Outcome.Completed,
                NonSecretReceipt = Digest(),
            },
        };
        Assert.IsNull(HelperProtocolV2Codec.Decode(
            credentialReceipt.ToByteArray(), HelperNow, "assignment-credential", "command-credential",
            expectedProfileId: "profile-1", expectedGenerationId: "generation-1", expectedNonSecretReceipt: Digest(),
            expectedPayloadCase: V2Frame.PayloadOneofCase.Receipt, expectedSequence: 13,
            expectedAssignmentKind: V2AssignmentKind.Enroll).Receipt.RawResponse);
        V2Frame credentialReceiptDigestRebind = credentialReceipt.Clone();
        credentialReceiptDigestRebind.Receipt.NonSecretReceipt = Digest([2]);
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
            credentialReceiptDigestRebind.ToByteArray(), HelperNow, "assignment-credential", "command-credential",
            expectedProfileId: "profile-1", expectedGenerationId: "generation-1", expectedNonSecretReceipt: Digest(),
            expectedPayloadCase: V2Frame.PayloadOneofCase.Receipt, expectedSequence: 13,
            expectedAssignmentKind: V2AssignmentKind.Enroll));
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
            credentialReceipt.ToByteArray(), HelperNow, "assignment-other", "command-credential",
            expectedProfileId: "profile-1", expectedGenerationId: "generation-1"));
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
            credentialReceipt.ToByteArray(), HelperNow, "assignment-credential", "command-other",
            expectedProfileId: "profile-1", expectedGenerationId: "generation-1"));
        foreach (V2Outcome credentialOutcome in new[]
                 {
                     V2Outcome.Completed,
                     V2Outcome.FailedKnown,
                     V2Outcome.Unavailable,
                     V2Outcome.Cancelled,
                 })
        {
            credentialReceipt.Receipt.Outcome = credentialOutcome;
            _ = HelperProtocolV2Codec.Decode(
                credentialReceipt.ToByteArray(), HelperNow, "assignment-credential", "command-credential",
                expectedProfileId: "profile-1", expectedGenerationId: "generation-1", expectedNonSecretReceipt: Digest(),
                expectedPayloadCase: V2Frame.PayloadOneofCase.Receipt, expectedSequence: 13,
                expectedAssignmentKind: V2AssignmentKind.Enroll);
        }
        foreach (V2Outcome transportOnlyOutcome in new[]
                 {
                     V2Outcome.TransportMayHaveStarted,
                     V2Outcome.Oversized,
                     V2Outcome.Malformed,
                 })
        {
            credentialReceipt.Receipt.Outcome = transportOnlyOutcome;
            Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
                credentialReceipt.ToByteArray(), HelperNow, "assignment-credential", "command-credential",
                expectedProfileId: "profile-1", expectedGenerationId: "generation-1"));
        }
        V2Frame blockedReceipt = new()
        {
            Sequence = 14,
            ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Receipt = new V2Receipt
            {
                ProviderDispatch = DispatchSubject(),
                AssignmentKind = V2AssignmentKind.ProviderDispatch,
                AssignmentId = "assignment-dispatch",
                CommandId = "command-dispatch",
                Outcome = V2Outcome.Unavailable,
                RequestId = "request-1",
                DispatchId = new DispatchId { Value = "dispatch-1" },
                RequestFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
                CoordinatorFencingEpoch = 1,
                CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
                PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
                Settings = Digest(),
                OutputSchema = Digest(),
                EffectiveConfigurationId = "config-v2-1",
                RevocationEpoch = 0,
                AccountIdentityId = new ProviderAccountIdentityId { Value = "account-1" },
                BillingScopeIdentityId = new BillingScopeIdentityId { Value = "billing-1" },
                ReservationGroupId = new ReservationGroupId { Value = "reservation-1" },
                OperationKind = V2OperationKind.SourceClaimExtraction,
                Limits = HelperLimits(),
                DispatchDeadline = FutureInstant(),
                InputBoundProof = new V2InputBoundProof
                {
                    PolicyId = "unresolved-openai-responses-framing",
                    PolicyVersion = "authority-required",
                    Status = V2InputBoundProofStatus.AuthorityRequired,
                },
                NonSecretReceipt = Digest(),
            },
        };
        Assert.ThrowsExactly<NotSupportedException>(() => HelperProtocolV2Codec.Decode(
            blockedReceipt.ToByteArray(), HelperNow, "assignment-dispatch", "command-dispatch",
            expectedOperationId: "operation-1", expectedAttemptId: "attempt-1",
            expectedRequestId: "request-1", expectedDispatchId: "dispatch-1",
            expectedRequestFingerprintSha256: new byte[32],
            expectedInputBoundPolicyId: "unresolved-openai-responses-framing",
            expectedInputBoundPolicyVersion: "authority-required", expectedCoordinatorFencingEpoch: 1,
            expectedCapabilitySnapshotId: "capability-1", expectedPriceSnapshotId: "price-1",
            expectedSettings: Digest(), expectedOutputSchema: Digest(),
            expectedEffectiveConfigurationId: "config-v2-1", expectedNonSecretReceipt: Digest(),
            expectedRevocationEpoch: 0, expectedAccountIdentityId: "account-1",
            expectedBillingScopeIdentityId: "billing-1", expectedReservationGroupId: "reservation-1",
            expectedOperationKind: V2OperationKind.SourceClaimExtraction, expectedLimits: HelperLimits(),
            expectedDispatchDeadline: FutureInstant(), expectedPayloadCase: V2Frame.PayloadOneofCase.Receipt,
            expectedSequence: 14, expectedAssignmentKind: V2AssignmentKind.ProviderDispatch));

        void AssertReceiptRebindRejected(Action<V2Receipt> mutate)
        {
            V2Frame rebound = blockedReceipt.Clone();
            mutate(rebound.Receipt);
            Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
                rebound.ToByteArray(), HelperNow, "assignment-dispatch", "command-dispatch",
                expectedOperationId: "operation-1", expectedAttemptId: "attempt-1",
                expectedRequestId: "request-1", expectedDispatchId: "dispatch-1",
                expectedRequestFingerprintSha256: new byte[32],
                expectedInputBoundPolicyId: "unresolved-openai-responses-framing",
                expectedInputBoundPolicyVersion: "authority-required", expectedCoordinatorFencingEpoch: 1,
                expectedCapabilitySnapshotId: "capability-1", expectedPriceSnapshotId: "price-1",
                expectedSettings: Digest(), expectedOutputSchema: Digest(),
                expectedEffectiveConfigurationId: "config-v2-1", expectedNonSecretReceipt: Digest(),
                expectedRevocationEpoch: 0, expectedAccountIdentityId: "account-1",
                expectedBillingScopeIdentityId: "billing-1", expectedReservationGroupId: "reservation-1",
                expectedOperationKind: V2OperationKind.SourceClaimExtraction, expectedLimits: HelperLimits(),
                expectedDispatchDeadline: FutureInstant(), expectedPayloadCase: V2Frame.PayloadOneofCase.Receipt,
                expectedSequence: 14, expectedAssignmentKind: V2AssignmentKind.ProviderDispatch));
        }
        AssertReceiptRebindRejected(x => x.AssignmentId = "assignment-other");
        AssertReceiptRebindRejected(x => x.CommandId = "command-other");
        AssertReceiptRebindRejected(x => x.ProviderDispatch.OperationId.Value = "operation-other");
        AssertReceiptRebindRejected(x => x.ProviderDispatch.AttemptId.Value = "attempt-other");
        AssertReceiptRebindRejected(x => x.RequestId = "request-other");
        AssertReceiptRebindRejected(x => x.DispatchId.Value = "dispatch-other");
        AssertReceiptRebindRejected(x => x.RequestFingerprintSha256 = ByteString.CopyFrom(Enumerable.Repeat((byte)1, 32).ToArray()));
        AssertReceiptRebindRejected(x => x.CoordinatorFencingEpoch = 2);
        AssertReceiptRebindRejected(x => x.InputBoundProof.PolicyId = "policy-other");
        AssertReceiptRebindRejected(x => x.InputBoundProof.PolicyVersion = "version-other");
        AssertReceiptRebindRejected(x => x.CapabilitySnapshotId.Value = "capability-other");
        AssertReceiptRebindRejected(x => x.PriceSnapshotId.Value = "price-other");
        AssertReceiptRebindRejected(x => x.Settings = Digest([2]));
        AssertReceiptRebindRejected(x => x.OutputSchema = Digest([2]));
        AssertReceiptRebindRejected(x => x.EffectiveConfigurationId = "config-v2-other");
        AssertReceiptRebindRejected(x => x.NonSecretReceipt = Digest([2]));
        AssertReceiptRebindRejected(x => x.RevocationEpoch = 1);
        AssertReceiptRebindRejected(x => x.AccountIdentityId.Value = "account-other");
        AssertReceiptRebindRejected(x => x.BillingScopeIdentityId.Value = "billing-other");
        AssertReceiptRebindRejected(x => x.ReservationGroupId.Value = "reservation-other");
        AssertReceiptRebindRejected(x => x.OperationKind = V2OperationKind.CandidateInvestigation);
        AssertReceiptRebindRejected(x => x.Limits.MaximumInputTokens++);
        AssertReceiptRebindRejected(x => x.DispatchDeadline = ToInstant(HelperNow.AddSeconds(119)));

        V2Frame ambiguous = blockedReceipt.Clone();
        ambiguous.Receipt.Outcome = V2Outcome.TransportMayHaveStarted;
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(
            ambiguous.ToByteArray(), HelperNow, "assignment-dispatch", "command-dispatch",
            expectedOperationId: "operation-1", expectedAttemptId: "attempt-1",
            expectedRequestId: "request-1", expectedDispatchId: "dispatch-1",
            expectedRequestFingerprintSha256: new byte[32],
            expectedInputBoundPolicyId: "unresolved-openai-responses-framing",
            expectedInputBoundPolicyVersion: "authority-required", expectedCoordinatorFencingEpoch: 1,
            expectedCapabilitySnapshotId: "capability-1", expectedPriceSnapshotId: "price-1",
            expectedSettings: Digest(), expectedOutputSchema: Digest(),
            expectedEffectiveConfigurationId: "config-v2-1", expectedNonSecretReceipt: Digest(),
            expectedRevocationEpoch: 0, expectedAccountIdentityId: "account-1",
            expectedBillingScopeIdentityId: "billing-1", expectedReservationGroupId: "reservation-1",
            expectedOperationKind: V2OperationKind.SourceClaimExtraction, expectedLimits: HelperLimits(),
            expectedDispatchDeadline: FutureInstant(), expectedPayloadCase: V2Frame.PayloadOneofCase.Receipt,
            expectedSequence: 14, expectedAssignmentKind: V2AssignmentKind.ProviderDispatch));
        ambiguous.Receipt.TransportMayHaveStarted = true;
        Assert.ThrowsExactly<NotSupportedException>(() => HelperProtocolV2Codec.Decode(
            ambiguous.ToByteArray(), HelperNow, "assignment-dispatch", "command-dispatch",
            expectedOperationId: "operation-1", expectedAttemptId: "attempt-1",
            expectedRequestId: "request-1", expectedDispatchId: "dispatch-1",
            expectedRequestFingerprintSha256: new byte[32],
            expectedInputBoundPolicyId: "unresolved-openai-responses-framing",
            expectedInputBoundPolicyVersion: "authority-required", expectedCoordinatorFencingEpoch: 1,
            expectedCapabilitySnapshotId: "capability-1", expectedPriceSnapshotId: "price-1",
            expectedSettings: Digest(), expectedOutputSchema: Digest(),
            expectedEffectiveConfigurationId: "config-v2-1", expectedNonSecretReceipt: Digest(),
            expectedRevocationEpoch: 0, expectedAccountIdentityId: "account-1",
            expectedBillingScopeIdentityId: "billing-1", expectedReservationGroupId: "reservation-1",
            expectedOperationKind: V2OperationKind.SourceClaimExtraction, expectedLimits: HelperLimits(),
            expectedDispatchDeadline: FutureInstant(), expectedPayloadCase: V2Frame.PayloadOneofCase.Receipt,
            expectedSequence: 14, expectedAssignmentKind: V2AssignmentKind.ProviderDispatch));
        V2Frame nonAmbiguousFlag = blockedReceipt.Clone();
        nonAmbiguousFlag.Receipt.TransportMayHaveStarted = true;
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeProviderReceipt(nonAmbiguousFlag));

        V2Frame oversized = blockedReceipt.Clone();
        oversized.Receipt.Outcome = V2Outcome.Oversized;
        oversized.Receipt.OverflowObservedExcessBytes = 1;
        Assert.ThrowsExactly<NotSupportedException>(() => DecodeProviderReceipt(oversized));
        oversized.Receipt.OverflowObservedExcessBytes++;
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeProviderReceipt(oversized));

        V2Frame stagedOverflow = blockedReceipt.Clone();
        V2Limits stagedLimits = HelperLimits();
        stagedLimits.MaximumResponseBytes = 1024;
        stagedLimits.MaximumStagedOutputBytes = 128;
        stagedOverflow.Receipt.Limits = stagedLimits.Clone();
        stagedOverflow.Receipt.Outcome = V2Outcome.Completed;
        stagedOverflow.Receipt.OutcomeHasResponse = true;
        stagedOverflow.Receipt.RawResponse = Digest(new byte[129]);
        stagedOverflow.Receipt.InputTokens = Available(10);
        stagedOverflow.Receipt.OutputTokens = Available(5);
        stagedOverflow.Receipt.TotalTokens = Available(15);
        stagedOverflow.Receipt.ReasoningTokens = Available(2);
        stagedOverflow.Receipt.CacheReadTokens = Available(0);
        stagedOverflow.Receipt.CacheWriteTokens = Available(0);
        stagedOverflow.Receipt.PricedToolCalls = Available(0);
        stagedOverflow.Receipt.CalculatedNanoUsd = Available(100);
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeProviderReceipt(stagedOverflow, stagedLimits));
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
                DispatchDeadline = FutureInstant(),
                Limits = HelperLimits(),
                EvaluatedAt = ToInstant(HelperNow),
                RequestId = "request-1",
                OperationId = new OperationId { Value = "operation-1" },
                RequestFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
            },
        };
        Assert.AreEqual("account-1", DecodeRevalidation(frame).DispatchRevalidation.AccountIdentityId.Value);
        V2Frame revalidationDeadlineOverflow = frame.Clone();
        revalidationDeadlineOverflow.DispatchRevalidation.DispatchDeadline = ToInstant(HelperNow.AddSeconds(121));
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeRevalidation(revalidationDeadlineOverflow));
        V2Frame revalidationOperationRebind = frame.Clone();
        revalidationOperationRebind.DispatchRevalidation.OperationId.Value = "operation-other";
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeRevalidation(revalidationOperationRebind));
        V2Frame revalidationLimitOverflow = frame.Clone();
        revalidationLimitOverflow.DispatchRevalidation.Limits.MaximumResponseBytes++;
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeRevalidation(revalidationLimitOverflow));
        V2Frame revalidationDigestRebind = frame.Clone();
        revalidationDigestRebind.DispatchRevalidation.CanonicalRequest.Value = ByteString.CopyFrom(Enumerable.Repeat((byte)1, 32).ToArray());
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeRevalidation(revalidationDigestRebind));
        frame.DispatchRevalidation.PriceSnapshotId = null;
        Assert.ThrowsExactly<InvalidDataException>(() => DecodeRevalidation(frame));

        ListProviderBudgetRequest query = new() { ScopeKind = "global", ScopeId = "global", RequestedPageSize = 100 };
        Assert.IsTrue(query.CalculateSize() < ProtocolConstants.MaximumMessageBytes);
        byte[] replayBytes = [4, 5, 6];
        ProviderReplayPayload replay = new()
        {
            OperationId = new OperationId { Value = "operation-1" },
            ReplayState = ProviderReplayState.NotAvailable,
            NetworkPermitted = false,
            InputBoundProofStatus = InputBoundProofStatus.AuthorityRequired,
            InputBoundPolicyId = "unresolved-openai-responses-framing",
            InputBoundPolicyVersion = "authority-required",
        };
        Assert.IsFalse(ProviderReplayPayload.Parser.ParseFrom(replay.ToByteArray()).NetworkPermitted);
        GetProviderReplayRequest replayQuery = new() { OperationId = new OperationId { Value = "operation-1" } };
        ApplicationProviderContractValidator.Validate(replayQuery);
        replayQuery.RetainedResponseId = " ";
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(replayQuery));

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
            OwnerKind = "evidence-acquisition-run",
            OwnerId = "acquisition-1",
            JobNodeId = "job-1",
            EffectiveConfigurationV2Id = "config-v2-1",
            InputBoundProofStatus = InputBoundProofStatus.AuthorityRequired,
            InputBoundPolicyId = "unresolved-openai-responses-framing",
            InputBoundPolicyVersion = "authority-required",
            InstallationSnapshotId = "install-1",
            AnalysisContextId = "context-1",
            ResolvedInputManifestId = "manifest-1",
            PromptId = "prompt-1",
            PromptFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
            CanonicalRequestBody = ByteString.CopyFrom(replayBytes),
            DispatchDeadline = FutureInstant(),
            CoordinatorFencingEpoch = 1,
            CommandId = "command-1",
            RequestedAt = ToInstant(HelperNow),
            ConfirmedAt = ToInstant(HelperNow.AddSeconds(1)),
            RequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData(replayBytes)),
        };
        SubmitProviderOperationRequest submitRoundTrip = SubmitProviderOperationRequest.Parser.ParseFrom(submit.ToByteArray());
        Assert.ThrowsExactly<NotSupportedException>(() => ApplicationProviderContractValidator.Validate(submitRoundTrip));
        Assert.ThrowsExactly<NotSupportedException>(() => ApplicationProviderContractValidator.RequireDispatchAdmission(submitRoundTrip));
        SubmitProviderOperationRequest fingerprintMismatch = submit.Clone();
        fingerprintMismatch.RequestFingerprintSha256 = ByteString.CopyFrom(new byte[32]);
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(fingerprintMismatch));
        SubmitProviderOperationRequest elapsedDeadline = submit.Clone();
        elapsedDeadline.DispatchDeadline = ToInstant(HelperNow.AddSeconds(121));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(elapsedDeadline));
        submitRoundTrip.OperationKind = (Infinium.Contracts.Protobuf.Application.V1.ProviderOperationKind)999;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(submitRoundTrip));

        ApplicationProviderContractValidator.Validate(query);
        query.RequestedPageSize = 101;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(query));
        query.RequestedPageSize = 1;
        query.After = new PageCursor { OpaqueValue = ByteString.CopyFrom(new byte[513]) };
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(query));
        ListProviderBudgetResponse budget = new()
        {
            Page = new ProviderBudgetPage
            {
                Items =
                {
                    new ProviderBudgetPayload
                    {
                        ScopeKind = "provider-profile",
                        ScopeId = "profile-1",
                        ReservedNanoUsd = 10,
                        SettledNanoUsd = 5,
                        UnresolvedNanoUsd = 5,
                    },
                },
            },
        };
        ApplicationProviderContractValidator.Validate(budget);
        budget.Page.Items[0].SettledNanoUsd = 6;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(budget));

        SubmitProviderEnrollmentRequest enrollment = new()
        {
            ProfileId = new ProviderAccessProfileId { Value = "profile-1" },
            GenerationId = new CredentialGenerationId { Value = "generation-1" },
            Provider = "openai",
            Purpose = "responses",
            DisplayLabel = "Synthetic",
            CommandId = "enrollment-command-1",
            RequestedAt = ToInstant(HelperNow),
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
            GenerationOrdinal = 1,
            AccountIdentityId = new ProviderAccountIdentityId { Value = "account-1" },
            BillingScopeIdentityId = new BillingScopeIdentityId { Value = "billing-1" },
            CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
            IntentId = "intent-1",
            RecoveryDisposition = "not-required",
            CleanupDisposition = "not-requested",
            Provider = "openai",
            Purpose = "responses",
            DisplayLabel = "Synthetic",
            RecordedAt = ToInstant(HelperNow),
        };
        ApplicationProviderContractValidator.Validate(profile);
        profile.LifecycleState = AppProviderProfileLifecycleState.Replacing;
        profile.VerificationState = AppProviderAvailabilityState.Unavailable;
        ApplicationProviderContractValidator.Validate(profile);
        profile.VerificationState = AppProviderAvailabilityState.Available;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(profile));
        profile.LifecycleState = AppProviderProfileLifecycleState.DeletePending;
        profile.VerificationState = AppProviderAvailabilityState.Unavailable;
        profile.CleanupDisposition = "failed";
        ApplicationProviderContractValidator.Validate(profile);
        profile.LifecycleState = AppProviderProfileLifecycleState.ActiveVerified;
        profile.VerificationState = AppProviderAvailabilityState.Available;
        profile.CleanupDisposition = "not-requested";
        profile.VerificationState = (AppProviderAvailabilityState)999;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(profile));
        profile.LifecycleState = AppProviderProfileLifecycleState.PendingEnrollment;
        profile.VerificationState = AppProviderAvailabilityState.NotApplicable;
        profile.AccountIdentityId = null;
        profile.BillingScopeIdentityId = null;
        profile.CapabilitySnapshotId = null;
        ApplicationProviderContractValidator.Validate(profile);
        profile.IntentId = string.Empty;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(profile));
        profile.LifecycleState = AppProviderProfileLifecycleState.SecureStoreUnavailable;
        profile.VerificationState = AppProviderAvailabilityState.Unavailable;
        profile.RecoveryDisposition = "unavailable";
        profile.CleanupDisposition = "failed";
        profile.IntentId = "intent-1";
        profile.AccountIdentityId = new ProviderAccountIdentityId { Value = "account-1" };
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(profile));

        ProviderOperationPayload reserved = new()
        {
            OperationId = new OperationId { Value = "operation-1" },
            ProfileId = new ProviderAccessProfileId { Value = "profile-1" },
            GenerationId = new CredentialGenerationId { Value = "generation-1" },
            CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
            PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
            OperationKind = Infinium.Contracts.Protobuf.Application.V1.ProviderOperationKind.SourceClaimExtraction,
            OwnerKind = "evidence-acquisition-run",
            OwnerId = "acquisition-1",
            JobNodeId = "job-1",
            EffectiveConfigurationV2Id = "config-v2-1",
            CommandId = "command-1",
            RequestedAt = ToInstant(HelperNow),
            State = ProviderOperationLifecycleState.InputBoundBlocked,
            SettlementState = ProviderSettlementState.NotStarted,
            ReplayState = ProviderReplayState.NotAvailable,
            InputBoundProofStatus = InputBoundProofStatus.AuthorityRequired,
            InputBoundPolicyId = "unresolved-openai-responses-framing",
            InputBoundPolicyVersion = "authority-required",
            InputTokens = UnavailableApplicationQuantity(),
            OutputTokens = UnavailableApplicationQuantity(),
            TotalTokens = UnavailableApplicationQuantity(),
            CalculatedNanoUsd = UnavailableApplicationQuantity(),
            DispatchCount = new OptionalProviderQuantity { Availability = AppProviderAvailabilityState.Available, Value = 0 },
            ReasoningTokens = UnavailableApplicationQuantity(),
            CacheReadTokens = UnavailableApplicationQuantity(),
            CacheWriteTokens = UnavailableApplicationQuantity(),
            ReservedNanoUsd = UnavailableApplicationQuantity(),
        };
        ApplicationProviderContractValidator.Validate(reserved);
        reserved.SettlementState = ProviderSettlementState.Settled;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(reserved));
        reserved.SettlementState = ProviderSettlementState.NotStarted;
        reserved.OperationKind = (Infinium.Contracts.Protobuf.Application.V1.ProviderOperationKind)999;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(reserved));

        ProviderCommandReceipt enrollmentReceipt = new()
        {
            CommandId = "enrollment-command-1",
            ReceiptId = "receipt-1",
            State = ProviderCommandState.EnrollmentIntentRecorded,
            RequestedAt = ToInstant(HelperNow),
            ConfirmedAt = ToInstant(HelperNow.AddSeconds(1)),
            Enrollment = new ProviderEnrollmentReceiptSubject
            {
                ProfileId = new ProviderAccessProfileId { Value = "profile-1" },
                GenerationId = new CredentialGenerationId { Value = "generation-1" },
            },
        };
        ApplicationProviderContractValidator.Validate(
            enrollmentReceipt, "enrollment-command-1", expectedProfileId: "profile-1", expectedGenerationId: "generation-1");
        enrollmentReceipt.Enrollment.GenerationId.Value = "generation-other";
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(
            enrollmentReceipt, "enrollment-command-1", expectedProfileId: "profile-1", expectedGenerationId: "generation-1"));
        byte[] exactRequestFingerprint = SHA256.HashData(replayBytes);
        ProviderCommandReceipt blockedOperationReceipt = new()
        {
            CommandId = "command-1",
            ReceiptId = "receipt-2",
            State = ProviderCommandState.BlockedAuthorityRequired,
            RequestedAt = ToInstant(HelperNow),
            ConfirmedAt = ToInstant(HelperNow.AddSeconds(1)),
            Operation = new ProviderOperationReceiptSubject
            {
                OperationId = new OperationId { Value = "operation-1" },
                RequestFingerprintSha256 = ByteString.CopyFrom(exactRequestFingerprint),
            },
        };
        ApplicationProviderContractValidator.Validate(
            blockedOperationReceipt, "command-1", "operation-1", exactRequestFingerprint);
        blockedOperationReceipt.Operation.RequestFingerprintSha256 = ByteString.CopyFrom(new byte[32]);
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(
            blockedOperationReceipt, "command-1", "operation-1", exactRequestFingerprint));

        SourceClaimExtractionPayload sourceClaim = new()
        {
            AcquisitionRunId = "acquisition-1",
            OperationId = new OperationId { Value = "operation-1" },
            OwnerKind = "evidence-acquisition-run",
            OwnerId = "acquisition-1",
            ParentAnalysisRunId = "run-1",
            ApplicationScopeId = "application-1",
            CostAttributionScopeId = "cost-1",
            SourceRevisionId = "source-revision-1",
            ValidationIds = { "validation-1" },
            ApplicationLinkIds = { "application-link-1" },
            AdmissionLinks =
            {
                new ProviderSemanticAdmissionLink
                {
                    AdmissionId = "admission-1",
                    AuthorizationId = "authorization-1",
                    ProposalId = "proposal-1",
                    OperationId = new OperationId { Value = "operation-1" },
                    ResponseRecordId = "response-1",
                    OwnerKind = "evidence-acquisition-run",
                    OwnerId = "acquisition-1",
                    RootSubjectId = "source-revision-1",
                    ValidationId = "validation-1",
                    ApplicationLinkId = "application-link-1",
                    State = "admitted",
                },
            },
        };
        ApplicationProviderContractValidator.Validate(sourceClaim);
        sourceClaim.OwnerId = "acquisition-other";
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(sourceClaim));
        sourceClaim.OwnerId = "acquisition-1";
        sourceClaim.ApplicationLinkIds.Clear();
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(sourceClaim));

        CandidateInvestigationPayload candidate = new()
        {
            OperationId = new OperationId { Value = "operation-2" },
            OwnerKind = "analysis-run",
            OwnerId = "run-1",
            AnalysisRunId = "run-1",
            CandidateId = "candidate-1",
            ValidationIds = { "validation-1" },
            AdmissionLinkIds = { "admission-1" },
            AdmissionLinks =
            {
                new ProviderSemanticAdmissionLink
                {
                    AdmissionId = "admission-1",
                    AuthorizationId = "authorization-2",
                    ProposalId = "proposal-2",
                    OperationId = new OperationId { Value = "operation-2" },
                    ResponseRecordId = "response-2",
                    OwnerKind = "analysis-run",
                    OwnerId = "run-1",
                    RootSubjectId = "candidate-1",
                    ValidationId = "validation-1",
                    ApplicationLinkId = "application-link-2",
                    State = "admitted",
                },
            },
        };
        ApplicationProviderContractValidator.Validate(candidate);
        candidate.AnalysisRunId = " ";
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(candidate));
        candidate.AnalysisRunId = "run-1";
        candidate.AdmissionLinkIds.Clear();
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(candidate));

        ProviderResponsePayload unavailableResponse = new()
        {
            ResponseRecordId = "response-1",
            OwnerKind = "evidence-acquisition-run",
            OwnerId = "acquisition-1",
            OperationKind = Infinium.Contracts.Protobuf.Application.V1.ProviderOperationKind.SourceClaimExtraction,
            Limits = ApplicationLimits(),
            MaximumRawResponseBytes = 1_048_576,
            RawResponseAvailability = AppProviderAvailabilityState.Unavailable,
            ResponseHeadersAvailability = AppProviderAvailabilityState.Unavailable,
            HttpStatusAvailability = AppProviderAvailabilityState.Unavailable,
            ProviderResponseIdAvailability = AppProviderAvailabilityState.Unavailable,
            ClientRequestIdAvailability = AppProviderAvailabilityState.Unavailable,
            ProviderRequestIdAvailability = AppProviderAvailabilityState.Unavailable,
            RefusalAvailability = AppProviderAvailabilityState.Unavailable,
            IncompleteAvailability = AppProviderAvailabilityState.Unavailable,
            ErrorAvailability = AppProviderAvailabilityState.Unavailable,
            ReturnedModelAvailability = AppProviderAvailabilityState.Unavailable,
            ReturnedServiceTierAvailability = AppProviderAvailabilityState.Unavailable,
            BillingEvidenceAvailability = AppProviderAvailabilityState.Unavailable,
            ResponseState = "unknown",
            RequestedModel = "gpt-5.6-sol",
            RequestedServiceTier = "default",
            ReasoningContext = "current_turn",
            ReasoningMode = "standard",
            PromptCacheMode = "explicit",
            DispatchCount = new OptionalProviderQuantity { Availability = AppProviderAvailabilityState.Available, Value = 0 },
            InputTokens = UnavailableApplicationQuantity(),
            OutputTokens = UnavailableApplicationQuantity(),
            TotalTokens = UnavailableApplicationQuantity(),
            ReasoningTokens = UnavailableApplicationQuantity(),
            CacheReadTokens = UnavailableApplicationQuantity(),
            CacheWriteTokens = UnavailableApplicationQuantity(),
            PricedToolCalls = UnavailableApplicationQuantity(),
            CalculatedNanoUsd = UnavailableApplicationQuantity(),
            BillingAvailability = AppProviderAvailabilityState.Unavailable,
            RateAvailability = AppProviderAvailabilityState.Unavailable,
            CreditAvailability = AppProviderAvailabilityState.Unavailable,
            ValidationState = "unavailable",
            AdmissionState = "unavailable",
            RecordedAt = ToInstant(HelperNow),
            Availability = AppProviderAvailabilityState.Unavailable,
            UsageAvailability = AppProviderAvailabilityState.Unavailable,
            InputBoundProofStatus = InputBoundProofStatus.AuthorityRequired,
            InputBoundPolicyId = "unresolved-openai-responses-framing",
            InputBoundPolicyVersion = "authority-required",
        };
        ApplicationProviderContractValidator.Validate(unavailableResponse);
        unavailableResponse.DispatchCount = UnavailableApplicationQuantity();
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(unavailableResponse));
        unavailableResponse.DispatchCount = new OptionalProviderQuantity { Availability = AppProviderAvailabilityState.Available, Value = 0 };
        unavailableResponse.BillingAvailability = AppProviderAvailabilityState.Available;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(unavailableResponse));
        unavailableResponse.BillingAvailability = AppProviderAvailabilityState.Unavailable;
        ProviderResponsePayload cancelledResponse = unavailableResponse.Clone();
        cancelledResponse.ResponseState = "cancelled";
        cancelledResponse.InputBoundProofStatus = InputBoundProofStatus.Proved;
        cancelledResponse.InputBoundPolicyId = "accepted-policy";
        cancelledResponse.InputBoundPolicyVersion = "1";
        Assert.ThrowsExactly<NotSupportedException>(() => ApplicationProviderContractValidator.Validate(cancelledResponse));
        cancelledResponse.ResponseHeadersAvailability = AppProviderAvailabilityState.Unsupported;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(cancelledResponse));
        cancelledResponse.ResponseHeadersAvailability = AppProviderAvailabilityState.Unavailable;
        cancelledResponse.ProviderResponseIdAvailability = AppProviderAvailabilityState.Available;
        cancelledResponse.ProviderResponseId = "fabricated-provider-response";
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(cancelledResponse));

        ProviderResponsePayload retainedOverrun = unavailableResponse.Clone();
        retainedOverrun.AuthorizationId = "authorization-overrun";
        retainedOverrun.RequestId = "request-overrun";
        retainedOverrun.DispatchFenceId = new DispatchId { Value = "fence-overrun" };
        retainedOverrun.InputBoundProofStatus = InputBoundProofStatus.Proved;
        retainedOverrun.InputBoundPolicyId = "accepted-policy";
        retainedOverrun.InputBoundPolicyVersion = "1";
        retainedOverrun.Availability = AppProviderAvailabilityState.Available;
        retainedOverrun.UsageAvailability = AppProviderAvailabilityState.Available;
        retainedOverrun.RawResponseAvailability = AppProviderAvailabilityState.Available;
        retainedOverrun.RawResponse = new Infinium.Contracts.Protobuf.Common.V1.ContentDigest
        {
            Algorithm = Infinium.Contracts.Protobuf.Common.V1.DigestAlgorithm.Sha256,
            Value = ByteString.CopyFrom(new byte[32]),
            SizeBytes = 512,
        };
        retainedOverrun.HttpStatusAvailability = AppProviderAvailabilityState.Available;
        retainedOverrun.HttpStatus = 200;
        retainedOverrun.ReturnedModelAvailability = AppProviderAvailabilityState.Available;
        retainedOverrun.ReturnedModel = "gpt-5.6-sol";
        retainedOverrun.ReturnedServiceTierAvailability = AppProviderAvailabilityState.Available;
        retainedOverrun.ReturnedServiceTier = "default";
        retainedOverrun.ResponseState = "completed";
        retainedOverrun.ValidationState = "admitted";
        retainedOverrun.AdmissionState = "admitted";
        retainedOverrun.DispatchCount = AvailableApplicationQuantity(1);
        retainedOverrun.InputTokens = AvailableApplicationQuantity(32);
        retainedOverrun.OutputTokens = AvailableApplicationQuantity(16);
        retainedOverrun.TotalTokens = AvailableApplicationQuantity(48);
        retainedOverrun.ReasoningTokens = AvailableApplicationQuantity(4);
        retainedOverrun.CacheReadTokens = AvailableApplicationQuantity(0);
        retainedOverrun.CacheWriteTokens = AvailableApplicationQuantity(0);
        retainedOverrun.PricedToolCalls = AvailableApplicationQuantity(0);
        retainedOverrun.CalculatedNanoUsd = AvailableApplicationQuantity(42);
        retainedOverrun.Limits.MaximumInputTokens = 16;
        retainedOverrun.Limits.MaximumOutputTokens = 8;
        retainedOverrun.Limits.MaximumCalculatedNanoUsd = 40;
        Assert.ThrowsExactly<NotSupportedException>(() => ApplicationProviderContractValidator.Validate(retainedOverrun));

        ApplicationProviderContractValidator.Validate(replay);
        replay.NetworkPermitted = true;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(replay));
        replay.NetworkPermitted = false;
        replay.ReplayState = ProviderReplayState.RetainedResponse;
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationProviderContractValidator.Validate(replay));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderTraceabilityInventoryResolvesEveryFieldAcrossAllSixSeams()
    {
        using JsonDocument inventory = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
            "docs", "plans", "milestones", "m1", "slices", "s6", "wp1-contract-traceability.v1.json")));
        Assert.AreEqual("Proposed", inventory.RootElement.GetProperty("maturity").GetString());
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
                AssertPathSpecificOmission(mapping.GetProperty("persistence"), path, "not_persisted_reason");
                AssertPathSpecificOmission(mapping.GetProperty("output"), path, "omission_reason");
                AssertPathSpecificOmission(mapping.GetProperty("replay"), path, "omission_reason");
            }
        }

        AssertTraceMapping(contracts, "provider-access-profile.v1.schema.json", "generation_id", "provider_generations.generation_id", "ADR-0020");
        AssertTraceMapping(contracts, "provider-access-profile.v1.schema.json", "account_identity_id", "provider_profile_projection.account_identity_id", "ADR-0020");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "operation_id", "provider_operation_blocks.operation_id", "ADR-0023");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "job_node_id", "provider_operation_blocks.job_node_id", "ADR-0016");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "owner_id", "provider_operation_blocks.owner_id", "ADR-0016");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "request_fingerprint", "provider_operation_blocks.request_fingerprint", "ADR-0025");
        AssertTraceOmission(contracts, "provider-operation.v1.schema.json", "transport_state", "persistence", "provider_operation_blocks.state");
        AssertTraceOmission(contracts, "provider-operation.v1.schema.json", "receipt_state", "persistence", "provider_operation_blocks.state");
        AssertTraceOmission(contracts, "provider-operation.v1.schema.json", "usage", "persistence", "provider_operation_blocks.state");
        AssertTraceOmission(contracts, "provider-operation.v1.schema.json", "settlement_state", "persistence", "provider_operation_blocks.state");
        AssertTraceOmission(contracts, "provider-operation.v1.schema.json", "replay_state", "persistence", "provider_operation_blocks.state");
        AssertTraceDerivation(contracts, "transport_state", "input-bound-blocked -> not-started");
        AssertTraceDerivation(contracts, "receipt_state", "input-bound-blocked -> not-available");
        AssertTraceDerivation(contracts, "usage", "input-bound-blocked -> exact blockedUsage unavailable vector with available zero dispatch_count");
        AssertTraceDerivation(contracts, "settlement_state", "input-bound-blocked -> not-started");
        AssertTraceDerivation(contracts, "replay_state", "input-bound-blocked -> not-available");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "$defs.inputBoundProof.status", "provider_operation_blocks.input_bound_proof_status", "ADR-0025");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "raw_response_payload",
            "provider_responses.raw_response_payload_id", "provider_responses.raw_response_fingerprint");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "response_headers_payload",
            "provider_responses.response_headers_payload_id", "provider_responses.response_headers_fingerprint");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "provider_request_id", "provider_responses.provider_request_id", "ADR-0025");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "maximum_raw_response_bytes", "provider_responses.maximum_raw_response_bytes", "ADR-0025");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "validation_state", "provider_response_finalizations.validation_state", "ADR-0025");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "admission_state", "provider_response_finalizations.admission_state", "ADR-0025");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "usage", "provider_usage_entries.total_tokens", "ADR-0025");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "usage", "provider_usage_entries.availability", "ADR-0025");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "rate_limit_facts", "provider_responses.expected_rate_limit_fact_count", "ADR-0025");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "limits", "provider_operation_authorizations.maximum_request_bytes",
            "provider_operation_authorizations.deadline_milliseconds");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "$defs.rateLimitFact.scope", "provider_rate_limit_facts.scope", "ADR-0025");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "$defs.capabilitySnapshot.model", "provider_capability_snapshots.model", "ADR-0020");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "$defs.priceSnapshot.model", "provider_price_snapshots.model", "ADR-0020");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "$defs.priceRule.rule_id", "provider_price_rules.rule_id", "ADR-0023");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "command_id", "provider_operation_blocks.command_id", "ADR-0016");
        AssertTraceProjection(contracts, "provider-operation.v1.schema.json", "command_id", "output", "ProviderOperationPayload", "command_id");
        AssertTraceProjection(contracts, "provider-operation.v1.schema.json", "requested_at", "output", "ProviderOperationPayload", "requested_at");
        AssertTraceMapping(contracts, "provider-operation.v1.schema.json", "effective_configuration_id",
            "provider_operation_blocks.effective_configuration_id", "ADR-0025");
        AssertTraceProjection(contracts, "provider-operation.v1.schema.json", "effective_configuration_id",
            "output", "ProviderOperationPayload", "effective_configuration_v2_id");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "acquisition_run_id",
            "evidence_acquisition_runs.acquisition_run_id", "ADR-0002");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "owner_id",
            "evidence_acquisition_runs.acquisition_run_id", "ADR-0016");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "parent_analysis_run_id",
            "evidence_acquisition_runs.parent_analysis_run_id", "ADR-0002");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "application_scope_id",
            "evidence_acquisition_runs.application_scope_id", "ADR-0002");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "cost_attribution_scope_id",
            "evidence_acquisition_runs.cost_attribution_scope_id", "ADR-0002");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "owner_kind",
            "provider_operation_blocks.owner_kind", "ADR-0016");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "application_link_ids",
            "evidence_acquisition_application_links.application_link_id", "ADR-0016");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "source_revision_id",
            "provider_semantic_proposals.root_subject_id", "ADR-0013");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "admission_links",
            "provider_semantic_admissions.operation_id", "provider_semantic_admissions.application_link_id");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "$defs.admissionLink.authorization_id",
            "provider_semantic_proposals.authorization_id", "ADR-0013");
        AssertTraceMapping(contracts, "source-claim-extraction.v1.schema.json", "$defs.admissionLink.validation_id",
            "provider_semantic_admissions.validation_id", "ADR-0013");
        AssertTraceProjection(contracts, "source-claim-extraction.v1.schema.json", "$defs.admissionLink.authorization_id",
            "output", "ProviderSemanticAdmissionLink", "authorization_id");
        AssertTraceProjection(contracts, "source-claim-extraction.v1.schema.json", "source_revision_id",
            "output", "SourceClaimExtractionPayload", "source_revision_id");
        AssertTraceProjection(contracts, "source-claim-extraction.v1.schema.json", "admission_links",
            "output", "SourceClaimExtractionPayload", "admission_links");
        AssertTraceMapping(contracts, "candidate-investigation.v1.schema.json", "owner_kind",
            "provider_operation_blocks.owner_kind", "ADR-0016");
        AssertTraceMapping(contracts, "candidate-investigation.v1.schema.json", "admission_link_ids",
            "provider_semantic_admissions.admission_id", "ADR-0013");
        AssertTraceMapping(contracts, "candidate-investigation.v1.schema.json", "admission_links",
            "provider_semantic_admissions.validation_id", "provider_semantic_admissions.application_link_id");
        AssertTraceProjection(contracts, "candidate-investigation.v1.schema.json", "admission_links",
            "output", "CandidateInvestigationPayload", "admission_links");
        AssertTraceProjection(contracts, "source-claim-extraction.v1.schema.json", "owner_kind",
            "output", "SourceClaimExtractionPayload", "owner_kind");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "input_bound_proof",
            "provider_requests.input_bound_policy_id", "provider_requests.input_bound_proof_status");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "client_request_id",
            "provider_responses.client_request_id", "ADR-0025");
        AssertTraceMapping(contracts, "provider-response.v1.schema.json", "billing_evidence_payload",
            "provider_responses.billing_evidence_payload_id", "provider_responses.billing_evidence_fingerprint");
        AssertTraceProjectionFields(contracts, "provider-response.v1.schema.json", "input_bound_proof",
            "output", "ProviderResponsePayload", "input_bound_proof_status", "input_bound_policy_id", "input_bound_policy_version");
        AssertTraceProjection(contracts, "provider-response.v1.schema.json", "client_request_id",
            "output", "ProviderResponsePayload", "client_request_id");
        AssertTraceMapping(contracts, "effective-scan-configuration.v2.schema.json", "local_configuration_v1_fingerprint",
            "provider_effective_scan_configurations_v2.local_configuration_v1_fingerprint", "ADR-0025");
        AssertTraceMapping(contracts, "effective-scan-configuration.v2.schema.json", "local_configuration_v1_provenance",
            "provider_effective_scan_configurations_v2.local_configuration_v1_provenance", "ADR-0025");
        AssertTraceMapping(contracts, "effective-scan-configuration.v2.schema.json", "access_profile_id",
            "provider_effective_scan_configurations_v2.profile_id", "ADR-0025");
        AssertTraceMapping(contracts, "run-output.v2.schema.json", "effective_configuration_v2_id",
            "provider_run_output_v2_bindings.effective_configuration_v2_id", "ADR-0019");
        AssertTraceProjection(contracts, "run-output.v2.schema.json", "effective_configuration_v2_id",
            "replay", "ProviderReplayPayload", "effective_configuration_id");
        AssertTraceOmission(contracts, "run-output.v2.schema.json", "$defs.publication.live",
            "persistence", "$defs.publication.live", "unresolved_hold");

        foreach (JsonElement mapping in contracts.SelectMany(x => x.GetProperty("field_mappings").EnumerateArray()))
        {
            string path = mapping.GetProperty("path").GetString()!;
            if (!path.EndsWith("_id", StringComparison.Ordinal)
                || path == "operation_id" || path.EndsWith(".operation_id", StringComparison.Ordinal))
            {
                continue;
            }
            JsonElement persistence = mapping.GetProperty("persistence");
            string[] columns = persistence.TryGetProperty("table_columns", out JsonElement many)
                ? many.EnumerateArray().Select(x => x.GetString()!).ToArray()
                : persistence.TryGetProperty("table_column", out JsonElement one) ? [one.GetString()!] : [];
            CollectionAssert.DoesNotContain(columns, "provider_operation_blocks.operation_id", path);
            CollectionAssert.DoesNotContain(columns, "provider_operation_projection.operation_id", path);
        }
    }

    private static void AssertPathSpecificOmission(JsonElement seam, string path, string property)
    {
        if (seam.TryGetProperty(property, out JsonElement reason))
        {
            StringAssert.Contains(reason.GetString(), path, $"{path}:{property}");
        }
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
        Match block = Regex.Match(proto, $@"message\s+{Regex.Escape(message)}\s*\{{(?<body>.*?)^\}}", RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.IsTrue(block.Success, $"{schema}:{path}:{kind}:{message}");
        string[] fields = seam.TryGetProperty("fields", out JsonElement many)
            ? many.EnumerateArray().Select(x => x.GetString()!).ToArray()
            : [seam.GetProperty("field").GetString()!];
        foreach (string field in fields)
        {
            Assert.IsTrue(Regex.IsMatch(block.Groups["body"].Value, $@"(?m)^\s*(?:optional\s+|repeated\s+)?[\w.]+\s+{Regex.Escape(field)}\s*="),
                $"{schema}:{path}:{kind}:{message}.{field}");
        }
    }

    private static void AssertTraceProjection(
        JsonElement[] contracts,
        string schema,
        string path,
        string seam,
        string message,
        string field)
    {
        JsonElement mapping = contracts.Single(x => x.GetProperty("schema").GetString() == schema)
            .GetProperty("field_mappings").EnumerateArray()
            .Single(x => x.GetProperty("path").GetString() == path);
        JsonElement projection = mapping.GetProperty(seam);
        Assert.AreEqual(message, projection.GetProperty("message").GetString(), $"{schema}:{path}:{seam}:message");
        Assert.AreEqual(field, projection.GetProperty("field").GetString(), $"{schema}:{path}:{seam}:field");
    }

    private static void AssertTraceProjectionFields(
        JsonElement[] contracts,
        string schema,
        string path,
        string seam,
        string message,
        params string[] fields)
    {
        JsonElement mapping = contracts.Single(x => x.GetProperty("schema").GetString() == schema)
            .GetProperty("field_mappings").EnumerateArray()
            .Single(x => x.GetProperty("path").GetString() == path);
        JsonElement projection = mapping.GetProperty(seam);
        Assert.AreEqual(message, projection.GetProperty("message").GetString(), $"{schema}:{path}:{seam}:message");
        CollectionAssert.AreEqual(fields, projection.GetProperty("fields").EnumerateArray().Select(x => x.GetString()!).ToArray(),
            $"{schema}:{path}:{seam}:fields");
    }

    private static void AssertTraceOmission(
        JsonElement[] contracts,
        string schema,
        string path,
        string seam,
        string expectedPath,
        string? forbiddenText = null)
    {
        JsonElement mapping = contracts.Single(x => x.GetProperty("schema").GetString() == schema)
            .GetProperty("field_mappings").EnumerateArray()
            .Single(x => x.GetProperty("path").GetString() == path);
        string property = seam == "persistence" ? "not_persisted_reason" : "omission_reason";
        string reason = mapping.GetProperty(seam).GetProperty(property).GetString()!;
        StringAssert.Contains(reason, expectedPath, $"{schema}:{path}:{seam}");
        if (forbiddenText is not null)
        {
            Assert.IsFalse(reason.Contains(forbiddenText, StringComparison.Ordinal), $"{schema}:{path}:{seam}:{forbiddenText}");
        }
    }

    private static void AssertTraceDerivation(JsonElement[] contracts, string path, string translation)
    {
        JsonElement persistence = contracts.Single(x => x.GetProperty("schema").GetString() == "provider-operation.v1.schema.json")
            .GetProperty("field_mappings").EnumerateArray()
            .Single(x => x.GetProperty("path").GetString() == path)
            .GetProperty("persistence");
        Assert.AreEqual("provider_operation_blocks.state", persistence.GetProperty("derived_from").GetString(), path);
        Assert.AreEqual(translation, persistence.GetProperty("translation").GetString(), path);
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
        DomainProviderAvailabilityState.Unavailable, Q(0), U(), U(), U(), U(), U(), U(), U(), U(),
        DomainProviderAvailabilityState.Unavailable,
        DomainProviderAvailabilityState.Unavailable,
        DomainProviderAvailabilityState.Unavailable);
    private static ProviderQuantityContract U() => new(DomainProviderAvailabilityState.Unavailable, null);
    private static OptionalProviderQuantity UnavailableApplicationQuantity() =>
        new() { Availability = AppProviderAvailabilityState.Unavailable };
    private static OptionalProviderQuantity AvailableApplicationQuantity(ulong value) =>
        new() { Availability = AppProviderAvailabilityState.Available, Value = value };
    private static ProviderInputBoundProofContract BlockedProof() => new(
        ProviderOperationContractInvariants.LocalInputBoundPolicyId,
        ProviderOperationContractInvariants.LocalInputBoundPolicyVersion,
        ProviderInputBoundProofState.AuthorityRequired);
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
            ProviderDispatch = DispatchSubject(),
            CommandId = "command-1",
            CoordinatorFencingEpoch = 1,
            ExpiresAt = FutureInstant(),
            OneUseNonceFingerprintSha256 = ByteString.CopyFrom(new byte[32]),
        },
    };

    private static V2Frame DecodeBootstrap(V2Frame frame) => HelperProtocolV2Codec.Decode(
        frame.ToByteArray(), HelperNow, expectedCommandId: "command-1",
        expectedOperationId: "operation-1", expectedAttemptId: "attempt-1",
        expectedCoordinatorFencingEpoch: 1, expectedOneUseNonceFingerprintSha256: new byte[32],
        expectedBootstrapExpiresAt: FutureInstant(), expectedPayloadCase: V2Frame.PayloadOneofCase.Bootstrap,
        expectedSequence: 8);

    private static V2Frame DecodeBlockedAssignment(V2Frame frame, byte[] requestBytes) => HelperProtocolV2Codec.Decode(
        frame.ToByteArray(), HelperNow, "assignment-credential", "command-credential",
        expectedOperationId: "operation-1", expectedAttemptId: "attempt-1",
        expectedProfileId: "profile-1", expectedGenerationId: "generation-1",
        expectedGenerationOrdinal: 1,
        expectedRequestId: "request-1", expectedDispatchId: "dispatch-1",
        expectedRequestFingerprintSha256: SHA256.HashData(requestBytes),
        expectedInputBoundPolicyId: "unresolved-openai-responses-framing",
        expectedInputBoundPolicyVersion: "authority-required", expectedRevocationEpoch: 0,
        expectedAccountIdentityId: "account-1", expectedBillingScopeIdentityId: "billing-1",
        expectedReservationGroupId: "reservation-1", expectedOperationKind: V2OperationKind.SourceClaimExtraction,
        expectedLimits: HelperLimits(), expectedDispatchDeadline: FutureInstant(),
        expectedCapabilitySnapshotId: "capability-1", expectedPriceSnapshotId: "price-1",
        expectedSettings: Digest(), expectedOutputSchema: Digest(), expectedEffectiveConfigurationId: "config-v2-1",
        expectedPayloadCase: V2Frame.PayloadOneofCase.Assignment, expectedSequence: frame.Sequence,
        expectedAssignmentKind: V2AssignmentKind.ProviderDispatch);

    private static V2Frame DecodeRevalidation(V2Frame frame) => HelperProtocolV2Codec.Decode(
        frame.ToByteArray(), HelperNow, expectedOperationId: "operation-1", expectedAttemptId: "attempt-1",
        expectedProfileId: "profile-1", expectedGenerationId: "generation-1",
        expectedRequestId: "request-1", expectedDispatchId: "dispatch-1",
        expectedRequestFingerprintSha256: new byte[32],
        expectedInputBoundPolicyId: "unresolved-openai-responses-framing",
        expectedInputBoundPolicyVersion: "authority-required", expectedCoordinatorFencingEpoch: 1,
        expectedCapabilitySnapshotId: "capability-1", expectedPriceSnapshotId: "price-1",
        expectedSettings: Digest(), expectedOutputSchema: Digest(), expectedEffectiveConfigurationId: "configuration-1",
        expectedRevocationEpoch: 0, expectedAccountIdentityId: "account-1",
        expectedBillingScopeIdentityId: "billing-1", expectedReservationGroupId: "reservation-1",
        expectedOperationKind: V2OperationKind.SourceClaimExtraction, expectedLimits: HelperLimits(),
        expectedDispatchDeadline: FutureInstant(), expectedPayloadCase: V2Frame.PayloadOneofCase.DispatchRevalidation,
        expectedSequence: frame.Sequence);

    private static V2Frame DecodeProviderReceipt(V2Frame frame, V2Limits? limits = null) => HelperProtocolV2Codec.Decode(
        frame.ToByteArray(), HelperNow, "assignment-dispatch", "command-dispatch",
        expectedOperationId: "operation-1", expectedAttemptId: "attempt-1",
        expectedRequestId: "request-1", expectedDispatchId: "dispatch-1",
        expectedRequestFingerprintSha256: new byte[32],
        expectedInputBoundPolicyId: "unresolved-openai-responses-framing",
        expectedInputBoundPolicyVersion: "authority-required", expectedCoordinatorFencingEpoch: 1,
        expectedCapabilitySnapshotId: "capability-1", expectedPriceSnapshotId: "price-1",
        expectedSettings: Digest(), expectedOutputSchema: Digest(),
        expectedEffectiveConfigurationId: "config-v2-1", expectedNonSecretReceipt: Digest(),
        expectedRevocationEpoch: 0, expectedAccountIdentityId: "account-1",
        expectedBillingScopeIdentityId: "billing-1", expectedReservationGroupId: "reservation-1",
        expectedOperationKind: V2OperationKind.SourceClaimExtraction, expectedLimits: limits ?? HelperLimits(),
        expectedDispatchDeadline: FutureInstant(), expectedPayloadCase: V2Frame.PayloadOneofCase.Receipt,
        expectedSequence: frame.Sequence, expectedAssignmentKind: V2AssignmentKind.ProviderDispatch);

    private static V2CredentialSubject CredentialSubject() => new()
    {
        AccessProfileId = new ProviderAccessProfileId { Value = "profile-1" },
        GenerationId = new CredentialGenerationId { Value = "generation-1" },
    };

    private static V2DispatchSubject DispatchSubject() => new()
    {
        OperationId = new OperationId { Value = "operation-1" },
        AttemptId = new AttemptId { Value = "attempt-1" },
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

    private static OptionalUInt64 Available(ulong value) => new()
    {
        Availability = AvailabilityState.Available,
        Value = value,
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

    private static Instant FutureInstant() => ToInstant(HelperNow.AddSeconds(120));

    private static Instant ToInstant(DateTimeOffset value) => new()
    {
        UnixSeconds = value.ToUnixTimeSeconds(),
        Nanoseconds = 0,
    };

    private static JsonObject IdentityReference(string identity) => new()
    {
        ["identity"] = identity,
        ["fingerprint"] = new string('a', 64),
    };
}
