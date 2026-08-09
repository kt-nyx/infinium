using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infinium.Domain.Contracts;

public static class ContractJsonSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static Sha256Fingerprint Fingerprint<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Sha256Fingerprint(Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                JsonSerializer.SerializeToUtf8Bytes(value, Options))));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new OpaqueIdJsonConverter());
        options.Converters.Add(new ContractVersionJsonConverter());
        options.Converters.Add(new Sha256FingerprintJsonConverter());
        options.Converters.Add(new UtcTimestampJsonConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private sealed class OpaqueIdJsonConverter : JsonConverter<OpaqueId>
    {
        public override OpaqueId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("Opaque ID must be a string."));

        public override void Write(Utf8JsonWriter writer, OpaqueId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class ContractVersionJsonConverter : JsonConverter<ContractVersion>
    {
        public override ContractVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ContractVersion.Parse(reader.GetString() ?? throw new JsonException("Contract version must be a string."));

        public override void Write(Utf8JsonWriter writer, ContractVersion value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    private sealed class Sha256FingerprintJsonConverter : JsonConverter<Sha256Fingerprint>
    {
        public override Sha256Fingerprint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("SHA-256 must be a string."));

        public override void Write(Utf8JsonWriter writer, Sha256Fingerprint value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class UtcTimestampJsonConverter : JsonConverter<UtcTimestamp>
    {
        public override UtcTimestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            UtcTimestamp.Parse(reader.GetString() ?? throw new JsonException("UTC timestamp must be a string."));

        public override void Write(Utf8JsonWriter writer, UtcTimestamp value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}
