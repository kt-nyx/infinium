using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using V1Frame = Infinium.Contracts.Protobuf.Helper.V1.HelperPrivateFrame;
using V2Assignment = Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentV2;
using V2AssignmentKind = Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentKindV2;
using V2Bootstrap = Infinium.Contracts.Protobuf.Helper.V2.HelperBootstrapV2;
using V2Disposition = Infinium.Contracts.Protobuf.Helper.V2.DispatchDispositionV2;
using V2Frame = Infinium.Contracts.Protobuf.Helper.V2.HelperPrivateFrameV2;
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
        ProviderAvailabilityState.Available,
        ProviderAvailabilityState.Available,
        ProviderAvailabilityState.Unavailable);

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderContractsRoundTripCanonicallyAcrossAllNineSchemas()
    {
        AssertCanonical(
            new ProviderAccessProfileDocument(
                ContractConstants.ProviderAccessProfileSchemaId, "1", Id("profile-1"), Id("generation-1"), 1, 0,
                "openai", "responses", "Synthetic profile", ProviderProfileState.ActiveVerified,
                ProviderAvailabilityState.Available, Id("account-1"), Id("billing-1"), Id("capability-1"),
                Id("intent-1"), "not-required", "confirmed", RecordedAt),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeAccessProfile);
        AssertCanonical(
            new ProviderOperationDocument(
                ContractConstants.ProviderOperationSchemaId, "1", Id("operation-1"), Id("run-1"), "analysis-run",
                ProviderOperationKind.SourceClaimExtraction, Id("job-1"), Id("attempt-1"), Id("request-1"), Id("profile-1"), Id("generation-1"), 0,
                Capability(), Price(), Fingerprint, Fingerprint, Fingerprint, Limits,
                Id("authorization-1"), Id("reservation-1"), Id("fence-1"), ProviderOperationState.Settled,
                "completed", "validated", Usage, "settled", "retained-response", RecordedAt),
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
                Fingerprint, ProviderOperationKind.SourceClaimExtraction, Fingerprint),
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
                [new(Id("operation-1"), ProviderOperationKind.SourceClaimExtraction, Id("acquisition-1"), Id("authorization-1"), Id("response-1"),
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

        foreach (string futureField in new[] { "attempt_id", "request_id", "settings_fingerprint", "output_schema_fingerprint",
                     "request_fingerprint", "authorization_id", "reservation_id", "dispatch_fence_id" })
        {
            operation.Remove(futureField);
        }
        operation["transport_state"] = "not-started";
        operation["receipt_state"] = "not-available";
        operation["settlement_state"] = "not-started";
        operation["replay_state"] = "not-available";
        Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractJsonCodecs.DeserializeOperation(
            System.Text.Encoding.UTF8.GetBytes(operation.ToJsonString())));
        foreach (JsonNode? quantity in operation["usage"]!.AsObject().Select(x => x.Value))
        {
            if (quantity is JsonObject available && available.ContainsKey("value"))
            {
                available["value"] = 0;
            }
        }
        Assert.AreEqual(ProviderOperationState.Proposed, ProviderContractJsonCodecs.DeserializeOperation(
            System.Text.Encoding.UTF8.GetBytes(operation.ToJsonString())).State);

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
                AuthorizedOnce = true,
                Disposition = V2Disposition.Authorized,
                AccountIdentityId = new ProviderAccountIdentityId { Value = "account-1" },
                BillingScopeIdentityId = new BillingScopeIdentityId { Value = "billing-1" },
                EffectiveConfigurationId = "configuration-1",
                CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
                PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
                Settings = Digest(),
                OutputSchema = Digest(),
                OperationKind = V2OperationKind.SourceClaimExtraction,
            },
        };
        Assert.AreEqual("account-1", HelperProtocolV2Codec.Decode(frame.ToByteArray()).DispatchRevalidation.AccountIdentityId.Value);
        frame.DispatchRevalidation.PriceSnapshotId = null;
        Assert.ThrowsExactly<InvalidDataException>(() => HelperProtocolV2Codec.Decode(frame.ToByteArray()));

        ListProviderBudgetRequest query = new() { ScopeKind = "global", ScopeId = "global", RequestedPageSize = 100 };
        Assert.IsTrue(query.CalculateSize() < ProtocolConstants.MaximumMessageBytes);
        ProviderReplayPayload replay = new() { OperationId = new OperationId { Value = "operation-1" }, RetainedResponseId = "response-1", ReplayState = "retained-response", DependencyManifestId = "manifest-1", NetworkPermitted = false };
        Assert.IsFalse(ProviderReplayPayload.Parser.ParseFrom(replay.ToByteArray()).NetworkPermitted);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderTraceabilityInventoryMapsEveryDeclaredFieldAcrossAllSixSeams()
    {
        using JsonDocument inventory = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
            "docs", "plans", "milestones", "m1", "slices", "s6", "wp1-contract-traceability.v1.json")));
        JsonElement[] contracts = inventory.RootElement.GetProperty("contracts").EnumerateArray().ToArray();
        Assert.AreEqual(9, contracts.Length);
        CollectionAssert.AreEquivalent(
            SchemaNames,
            contracts.Select(x => x.GetProperty("schema").GetString()!).ToArray());

        foreach (JsonElement contract in contracts)
        {
            string schemaName = contract.GetProperty("schema").GetString()!;
            using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(
                TestRepository.PathFromRoot("contracts", "json-schema", schemaName)));
            HashSet<string> declared = [];
            CollectDeclaredFields(schema.RootElement, string.Empty, declared);
            HashSet<string> mapped = contract.GetProperty("fields").EnumerateArray()
                .Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
            Assert.IsTrue(declared.SetEquals(mapped), schemaName);
            JsonElement seamGroups = contract.GetProperty("field_seams");
            Assert.IsTrue(seamGroups.EnumerateObject().Count() >= 2, schemaName);
            string[] assigned = seamGroups.EnumerateObject()
                .SelectMany(group => group.Value.EnumerateArray().Select(x => x.GetString()!)).ToArray();
            Assert.AreEqual(assigned.Length, assigned.Distinct(StringComparer.Ordinal).Count(), schemaName);
            Assert.IsTrue(mapped.SetEquals(assigned), schemaName);
            string[] authority = contract.GetProperty("authority").EnumerateArray().Select(x => x.GetString()!).ToArray();
            Assert.IsTrue(authority.Length > 0 && authority.All(AcceptedAuthorityIds.Contains), schemaName);
            foreach (string seam in new[] { contract.GetProperty("producer").GetString()!, contract.GetProperty("consumer").GetString()!, contract.GetProperty("output").GetString()! })
            {
                Assert.IsTrue(File.Exists(TestRepository.PathFromRoot(seam.Split('/'))), $"{schemaName}:{seam}");
            }
            string migration = File.ReadAllText(TestRepository.PathFromRoot("src", "Infinium.Persistence", "AuthoritativeStore.Migrations.cs"));
            foreach (string table in contract.GetProperty("persistence").EnumerateArray().Select(x => x.GetString()!))
            {
                StringAssert.Contains(migration, "CREATE TABLE " + table + "(", $"{schemaName}:{table}");
            }
            StringAssert.StartsWith(contract.GetProperty("replay").GetString()!, "retained-", schemaName);
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
    private static ProviderQuantityContract Q(long value) => new(ProviderAvailabilityState.Available, value);
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
}
