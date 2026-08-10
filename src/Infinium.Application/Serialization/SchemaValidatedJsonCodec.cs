using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Serialization;

internal static class SchemaValidatedJsonCodec
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
