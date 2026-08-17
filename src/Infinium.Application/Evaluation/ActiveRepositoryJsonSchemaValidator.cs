using System.Text.Json;

namespace Infinium.Application.Evaluation;

/// <summary>
/// Applies the active product JSON Schema vocabulary to an explicitly selected repository
/// authority document. Repository schemas remain governance inputs rather than product data;
/// this adapter exists so effect boundaries can fail closed on their complete accepted shape.
/// </summary>
public static class ActiveRepositoryJsonSchemaValidator
{
    public static void Validate(ReadOnlySpan<byte> instanceBytes, ReadOnlySpan<byte> schemaBytes,
        string schemaIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaIdentity);
        using JsonDocument instance = JsonDocument.Parse(instanceBytes.ToArray());
        using JsonDocument schema = JsonDocument.Parse(schemaBytes.ToArray());
        ActiveJsonSchemaValidator.Validate(instance.RootElement, schema.RootElement, schemaIdentity);
    }
}
