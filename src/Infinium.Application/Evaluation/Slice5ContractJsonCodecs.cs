using System.Text.Json;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Evaluation;

public static class DocumentationEvidenceJsonCodec
{
    public static byte[] Serialize(DocumentationEvidenceContract value) =>
        Slice5ContractJsonCodec.Serialize(value, "documentation-evidence.v1.schema.json", static item => Slice5ContractInvariants.Validate(item));

    public static DocumentationEvidenceContract Deserialize(ReadOnlySpan<byte> bytes) =>
        Slice5ContractJsonCodec.Deserialize<DocumentationEvidenceContract>(bytes, "documentation-evidence.v1.schema.json", static item => Slice5ContractInvariants.Validate(item));
}

public static class DocumentationClaimImportJsonCodec
{
    public static byte[] Serialize(DocumentationClaimImportManifestContract value) =>
        Slice5ContractJsonCodec.Serialize(
            value,
            "documentation-claim-import.v1.schema.json",
            static item => DocumentationClaimImportContractInvariants.Validate(item));

    public static DocumentationClaimImportManifestContract Deserialize(ReadOnlySpan<byte> bytes) =>
        Slice5ContractJsonCodec.Deserialize<DocumentationClaimImportManifestContract>(
            bytes,
            "documentation-claim-import.v1.schema.json",
            static item => DocumentationClaimImportContractInvariants.Validate(item));
}

public static class CandidateAnalysisJsonCodec
{
    public static byte[] Serialize(CandidateAnalysisContract value) =>
        Slice5ContractJsonCodec.Serialize(value, "candidate-analysis.v1.schema.json", static item => Slice5ContractInvariants.Validate(item));

    public static CandidateAnalysisContract Deserialize(ReadOnlySpan<byte> bytes) =>
        Slice5ContractJsonCodec.Deserialize<CandidateAnalysisContract>(bytes, "candidate-analysis.v1.schema.json", static item => Slice5ContractInvariants.Validate(item));
}

public static class CandidateDeliveredInputJsonCodec
{
    public static byte[] Serialize(CandidateDeliveredInputContract value) =>
        Slice5ContractJsonCodec.Serialize(value, "candidate-delivered-input.v1.schema.json", static item => CandidateDeliveredContractInvariants.Validate(item));

    public static CandidateDeliveredInputContract Deserialize(ReadOnlySpan<byte> bytes) =>
        Slice5ContractJsonCodec.Deserialize<CandidateDeliveredInputContract>(bytes, "candidate-delivered-input.v1.schema.json", static item => CandidateDeliveredContractInvariants.Validate(item));
}

public static class CandidateDeliveredExpansionJsonCodec
{
    public static byte[] Serialize(CandidateDeliveredExpansionContract value) =>
        Slice5ContractJsonCodec.Serialize(value, "candidate-delivered-expansion.v1.schema.json", static item => CandidateDeliveredContractInvariants.Validate(item));

    public static CandidateDeliveredExpansionContract Deserialize(ReadOnlySpan<byte> bytes) =>
        Slice5ContractJsonCodec.Deserialize<CandidateDeliveredExpansionContract>(bytes, "candidate-delivered-expansion.v1.schema.json", static item => CandidateDeliveredContractInvariants.Validate(item));
}

public static class FindingCaseJsonCodec
{
    public static byte[] Serialize(FindingCaseContract value) =>
        Slice5ContractJsonCodec.Serialize(value, "finding-case.v1.schema.json", static item => Slice5ContractInvariants.Validate(item));

    public static FindingCaseContract Deserialize(ReadOnlySpan<byte> bytes) =>
        Slice5ContractJsonCodec.Deserialize<FindingCaseContract>(bytes, "finding-case.v1.schema.json", static item => Slice5ContractInvariants.Validate(item));
}

public static class AnalysisReplayJsonCodec
{
    public static byte[] Serialize(AnalysisReplayContract value) =>
        Slice5ContractJsonCodec.Serialize(value, "analysis-replay.v1.schema.json", static item => Slice5ContractInvariants.Validate(item));

    public static AnalysisReplayContract Deserialize(ReadOnlySpan<byte> bytes) =>
        Slice5ContractJsonCodec.Deserialize<AnalysisReplayContract>(bytes, "analysis-replay.v1.schema.json", static item => Slice5ContractInvariants.Validate(item));
}

public static class AnalysisExecutionInputJsonCodec
{
    public static byte[] Serialize(AnalysisExecutionInputContract value) =>
        Slice5ContractJsonCodec.Serialize(value, "analysis-execution-input.v1.schema.json", static item => Slice5ContractInvariants.Validate(item));

    public static AnalysisExecutionInputContract Deserialize(ReadOnlySpan<byte> bytes) =>
        Slice5ContractJsonCodec.Deserialize<AnalysisExecutionInputContract>(bytes, "analysis-execution-input.v1.schema.json", static item => Slice5ContractInvariants.Validate(item));
}

internal static class Slice5ContractJsonCodec
{
    private const int MaximumDocumentBytes = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = ContractJsonSerializer.Options;

    internal static JsonSerializerOptions JsonOptions => SerializerOptions;

    internal static byte[] Serialize<T>(T value, string schemaFile, Action<T> validate)
    {
        ArgumentNullException.ThrowIfNull(value);
        validate(value);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
        if (bytes.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException($"{schemaFile} exceeds the bounded document size.");
        }

        using JsonDocument document = ParseStrict(bytes, schemaFile);
        ActiveJsonSchemaValidator.Validate(document.RootElement, schemaFile);
        return bytes;
    }

    internal static T Deserialize<T>(ReadOnlySpan<byte> bytes, string schemaFile, Action<T> validate)
    {
        if (bytes.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException($"{schemaFile} exceeds the bounded document size.");
        }

        using JsonDocument document = ParseStrict(bytes, schemaFile);
        ActiveJsonSchemaValidator.Validate(document.RootElement, schemaFile);
        T value = document.Deserialize<T>(SerializerOptions)
            ?? throw new InvalidDataException($"{schemaFile} did not produce a contract document.");
        validate(value);
        return value;
    }

    private static JsonDocument ParseStrict(ReadOnlySpan<byte> bytes, string schemaFile)
    {
        try
        {
            JsonDocument document = JsonDocument.Parse(
                bytes.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 128,
                });
            try
            {
                BoundedJsonDocumentReader.RejectDuplicateProperties(document.RootElement, schemaFile);
                return document;
            }
            catch
            {
                document.Dispose();
                throw;
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{schemaFile} is not strict JSON.", exception);
        }
    }

}
