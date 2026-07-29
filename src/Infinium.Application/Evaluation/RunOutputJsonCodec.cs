using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Evaluation;

public static class RunOutputJsonCodec
{
    private const int MaximumDocumentBytes = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public static byte[] Serialize(RunOutputContract output)
    {
        RunOutputContractInvariants.Validate(output);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(output, SerializerOptions);
        if (bytes.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException(
                $"Run output exceeds the {MaximumDocumentBytes}-byte stable document limit.");
        }

        using JsonDocument document = ParseStrict(bytes);
        EmbeddedJsonSchemaValidator.Validate(document.RootElement, "run-output.v1.schema.json");
        return bytes;
    }

    public static RunOutputContract Deserialize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException(
                $"Run output exceeds the {MaximumDocumentBytes}-byte stable document limit.");
        }

        using JsonDocument document = ParseStrict(bytes);
        EmbeddedJsonSchemaValidator.Validate(document.RootElement, "run-output.v1.schema.json");
        RunOutputContract output = document.Deserialize<RunOutputContract>(SerializerOptions)
            ?? throw new InvalidDataException("Run output JSON did not produce a contract document.");
        RunOutputContractInvariants.Validate(output);
        return output;
    }

    private static JsonDocument ParseStrict(ReadOnlySpan<byte> bytes)
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
                BoundedJsonDocumentReader.RejectDuplicateProperties(document.RootElement, "$");
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
            throw new InvalidDataException("Run output is not valid strict JSON.", exception);
        }
    }
}
