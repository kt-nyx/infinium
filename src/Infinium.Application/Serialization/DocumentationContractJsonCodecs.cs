using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

public static class DocumentationEvidenceJsonCodec
{
    public static byte[] Serialize(DocumentationEvidenceContract value) =>
        SchemaValidatedJsonCodec.Serialize(value, "documentation-evidence.v1.schema.json", static item => DocumentationEvidenceContractInvariants.Validate(item));

    public static DocumentationEvidenceContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<DocumentationEvidenceContract>(bytes, "documentation-evidence.v1.schema.json", static item => DocumentationEvidenceContractInvariants.Validate(item));
}

public static class DocumentationClaimImportJsonCodec
{
    public static byte[] Serialize(DocumentationClaimImportManifestContract value) =>
        SchemaValidatedJsonCodec.Serialize(
            value,
            "documentation-claim-import.v1.schema.json",
            static item => DocumentationClaimImportContractInvariants.Validate(item));

    public static DocumentationClaimImportManifestContract Deserialize(ReadOnlySpan<byte> bytes) =>
        SchemaValidatedJsonCodec.Deserialize<DocumentationClaimImportManifestContract>(
            bytes,
            "documentation-claim-import.v1.schema.json",
            static item => DocumentationClaimImportContractInvariants.Validate(item));
}
