using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderContractJsonCodecTests
{
    private static readonly Sha256Fingerprint Fingerprint = new(new string('a', 64));
    private static readonly UtcTimestamp RecordedAt = UtcTimestamp.Parse("2026-08-10T00:00:00.0000000+00:00");
    private static readonly ProviderFiniteLimitsContract Limits = new(
        65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000);
    private static readonly ProviderUsageContract Usage = new(
        1, 32, 16, 4, 0, 0, 0, 42,
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
                Id("job-1"), Id("attempt-1"), Id("request-1"), Id("profile-1"), Id("generation-1"), 0,
                Ref("capability-1"), Ref("price-1"), Fingerprint, Fingerprint, Fingerprint, Limits,
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
                Ref("capability-1"), Ref("price-1"), Limits, Id("prompt-1"), Fingerprint, Id("schema-1"),
                Fingerprint, "source-claim-extraction", Fingerprint),
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
                [new(Id("operation-1"), Id("acquisition-1"), Id("authorization-1"), Id("response-1"),
                    Id("admission-1"), Id("usage-1"), Id("settlement-1"), Id("replay-1"), "retained", false)],
                [Id("acquisition-1")], [], [], ["Synthetic gap"], false, false),
            ProviderContractJsonCodecs.Serialize,
            ProviderContractJsonCodecs.DeserializeRunOutputV2);
        AssertCanonical(
            new CliSummaryV2Document(
                ContractConstants.CliSummaryV2SchemaId, "1", Id("run-1"), Fingerprint, "live", 1, 32, 16, 4,
                0, 0, 42, 100, false, "retained-response", ["Synthetic gap"], false, false),
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
    public void HelperV2ProtocolFingerprintIsExactAndV1RemainsDecodableAuthority()
    {
        string v2 = TestRepository.PathFromRoot("contracts", "protobuf", "infinium", "helper", "v2", "helper.proto");
        string v1 = TestRepository.PathFromRoot("contracts", "protobuf", "infinium", "helper", "v1", "helper.proto");
        Assert.IsTrue(File.Exists(v1));
        Assert.AreEqual(HelperProtocolV2Constants.SchemaFingerprintSha256, ProtobufContractSetFingerprint());
        Assert.AreEqual(
            HelperProtocolV2Constants.SchemaFingerprintSha256,
            Convert.ToHexStringLower(Infinium.Application.Runtime.ProtocolConstants.Version.SchemaFingerprintSha256.Span));
        StringAssert.Contains(File.ReadAllText(v1), "package infinium.helper.v1;");
        StringAssert.Contains(File.ReadAllText(v2), "package infinium.helper.v2;");
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
            Assert.IsTrue(contract.GetProperty("authority").GetArrayLength() > 0, schemaName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(contract.GetProperty("producer").GetString()), schemaName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(contract.GetProperty("consumer").GetString()), schemaName);
            Assert.IsTrue(contract.GetProperty("persistence").GetArrayLength() > 0, schemaName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(contract.GetProperty("output").GetString()), schemaName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(contract.GetProperty("replay").GetString()), schemaName);
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

    private static void AssertCanonical<T>(T value, Func<T, byte[]> serialize, Deserialize<T> deserialize)
    {
        byte[] first = serialize(value);
        T roundTrip = deserialize(first);
        byte[] second = serialize(roundTrip);
        CollectionAssert.AreEqual(first, second);
    }

    private static ProviderIdentityReferenceContract Ref(string id) => new(Id(id), Fingerprint);
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
}
