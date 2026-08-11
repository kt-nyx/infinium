using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

public static class ProviderContractJsonCodecs
{
    public static byte[] Serialize(ProviderAccessProfileDocument value) =>
        SchemaValidatedJsonCodec.Serialize(value, "provider-access-profile.v1.schema.json", ProviderOperationContractInvariants.Validate);
    public static ProviderAccessProfileDocument DeserializeAccessProfile(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<ProviderAccessProfileDocument>(bytes, "provider-access-profile.v1.schema.json", ProviderOperationContractInvariants.Validate);

    public static byte[] Serialize(ProviderOperationDocument value) =>
        SchemaValidatedJsonCodec.Serialize(value, "provider-operation.v1.schema.json", ProviderOperationContractInvariants.Validate);
    public static ProviderOperationDocument DeserializeOperation(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<ProviderOperationDocument>(bytes, "provider-operation.v1.schema.json", ProviderOperationContractInvariants.Validate);

    public static byte[] Serialize(ProviderResponseDocument value) =>
        SchemaValidatedJsonCodec.Serialize(value, "provider-response.v1.schema.json", ProviderOperationContractInvariants.Validate);
    public static ProviderResponseDocument DeserializeResponse(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<ProviderResponseDocument>(bytes, "provider-response.v1.schema.json", ProviderOperationContractInvariants.Validate);

    public static byte[] Serialize(SourceClaimExtractionDocument value) =>
        SchemaValidatedJsonCodec.Serialize(value, "source-claim-extraction.v1.schema.json", ProviderOperationContractInvariants.Validate);
    public static SourceClaimExtractionDocument DeserializeSourceClaimExtraction(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<SourceClaimExtractionDocument>(bytes, "source-claim-extraction.v1.schema.json", ProviderOperationContractInvariants.Validate);

    public static byte[] Serialize(CandidateInvestigationDocument value) =>
        SchemaValidatedJsonCodec.Serialize(value, "candidate-investigation.v1.schema.json", ProviderOperationContractInvariants.Validate);
    public static CandidateInvestigationDocument DeserializeCandidateInvestigation(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<CandidateInvestigationDocument>(bytes, "candidate-investigation.v1.schema.json", ProviderOperationContractInvariants.Validate);

    public static byte[] Serialize(ProviderExecutionInputDocument value) =>
        SchemaValidatedJsonCodec.Serialize(value, "provider-execution-input.v1.schema.json", ProviderOperationContractInvariants.Validate);
    public static ProviderExecutionInputDocument DeserializeExecutionInput(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<ProviderExecutionInputDocument>(bytes, "provider-execution-input.v1.schema.json", ProviderOperationContractInvariants.Validate);

    public static byte[] Serialize(EffectiveScanConfigurationV2Document value) =>
        SchemaValidatedJsonCodec.Serialize(value, "effective-scan-configuration.v2.schema.json", ProviderOperationContractInvariants.Validate);
    public static EffectiveScanConfigurationV2Document DeserializeEffectiveConfigurationV2(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<EffectiveScanConfigurationV2Document>(bytes, "effective-scan-configuration.v2.schema.json", ProviderOperationContractInvariants.Validate);

    public static byte[] Serialize(RunOutputV2Document value) =>
        SchemaValidatedJsonCodec.Serialize(value, "run-output.v2.schema.json", ProviderOperationContractInvariants.Validate);
    public static RunOutputV2Document DeserializeRunOutputV2(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<RunOutputV2Document>(bytes, "run-output.v2.schema.json", ProviderOperationContractInvariants.Validate);

    public static byte[] Serialize(CliSummaryV2Document value) =>
        SchemaValidatedJsonCodec.Serialize(value, "cli-summary.v2.schema.json", ProviderOperationContractInvariants.Validate);
    public static CliSummaryV2Document DeserializeCliSummaryV2(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<CliSummaryV2Document>(bytes, "cli-summary.v2.schema.json", ProviderOperationContractInvariants.Validate);
}
