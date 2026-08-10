using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

public static class CliSummaryJsonCodec
{
    private const int MaximumDocumentBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = true,
    };

    public static byte[] Serialize(CliSummaryDocumentContract summary)
    {
        CliSummaryDocumentContractInvariants.Validate(summary);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(summary, SerializerOptions);
        using JsonDocument document = ParseAndValidate(bytes);
        return bytes;
    }

    public static CliSummaryDocumentContract Deserialize(ReadOnlySpan<byte> bytes)
    {
        using JsonDocument document = ParseAndValidate(bytes);
        CliSummaryDocumentContract summary =
            document.Deserialize<CliSummaryDocumentContract>(SerializerOptions)
            ?? throw new InvalidDataException("CLI summary JSON did not produce a contract document.");
        CliSummaryDocumentContractInvariants.Validate(summary);
        return summary;
    }

    private static JsonDocument ParseAndValidate(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException(
                $"CLI summary exceeds the {MaximumDocumentBytes}-byte stable document limit.");
        }

        try
        {
            JsonDocument document = JsonDocument.Parse(
                bytes.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64,
                });
            try
            {
                BoundedJsonDocumentReader.RejectDuplicateProperties(document.RootElement, "$");
                ActiveJsonSchemaValidator.Validate(
                    document.RootElement,
                    "cli-summary.v1.schema.json");
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
            throw new InvalidDataException("CLI summary is not valid strict JSON.", exception);
        }
    }
}
