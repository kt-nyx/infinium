using System.Text.Json;
using System.Text.RegularExpressions;

namespace Infinium.EvaluatorV2;

internal static partial class SemanticCanonicalizer
{
    private static readonly string[] StableIdentityProperties =
    [
        "contribution_id", "participant_id", "plugin_name", "gap_id", "assignment_id",
        "normalized_relative_path", "form_key", "population", "source_contribution_id",
        "field", "code",
    ];

    internal static IReadOnlyList<SemanticFact> Flatten(JsonElement extractionResult)
    {
        List<SemanticFact> facts = [];
        foreach (JsonProperty property in extractionResult.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            if (property.NameEquals("state"))
            {
                continue;
            }

            Visit(property.Value, $"/{Escape(property.Name)}", facts);
        }

        return facts;
    }

    private static void Visit(JsonElement element, string path, List<SemanticFact> facts)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    Visit(property.Value, $"{path}/{Escape(property.Name)}", facts);
                }

                return;
            case JsonValueKind.Array:
                VisitArray(element, path, facts);
                return;
            case JsonValueKind.String:
                string value = element.GetString()!;
                if (FormKeyPattern().IsMatch(value))
                {
                    value = value.ToLowerInvariant();
                }

                Add(path, TypeFor(path), "string", EvaluatorProtocol.Primitive(value), facts);
                return;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long integer))
                {
                    Add(path, TypeFor(path), "integer", EvaluatorProtocol.Primitive(integer), facts);
                }
                else
                {
                    using JsonDocument number = JsonDocument.Parse(element.GetRawText());
                    Add(path, TypeFor(path), "number", number.RootElement.Clone(), facts);
                }

                return;
            case JsonValueKind.True:
            case JsonValueKind.False:
                Add(path, TypeFor(path), "boolean", EvaluatorProtocol.Primitive(element.GetBoolean()), facts);
                return;
            case JsonValueKind.Null:
                Add(path, TypeFor(path), "null", EvaluatorProtocol.Null(), facts);
                return;
            default:
                throw new CandidateOutputException($"Unsupported JSON value at '{path}'.");
        }
    }

    private static void VisitArray(JsonElement array, string path, List<SemanticFact> facts)
    {
        JsonElement[] items = array.EnumerateArray().ToArray();
        if (!IsForcedSequence(path) && TryStableIdentity(items, out string? identityProperty))
        {
            foreach (JsonElement item in items.OrderBy(
                         item => IdentityText(item.GetProperty(identityProperty!)),
                         StringComparer.Ordinal))
            {
                string identity = Escape(IdentityText(item.GetProperty(identityProperty!)));
                Visit(item, $"{path}/{{{identityProperty}={identity}}}", facts);
            }

            return;
        }

        for (int index = 0; index < items.Length; index++)
        {
            Visit(items[index], $"{path}/{index}", facts);
        }
    }

    private static bool TryStableIdentity(JsonElement[] items, out string? propertyName)
    {
        propertyName = null;
        if (items.Length == 0 || items.Any(item => item.ValueKind != JsonValueKind.Object))
        {
            return false;
        }

        foreach (string candidate in StableIdentityProperties)
        {
            if (items.All(item => item.TryGetProperty(candidate, out JsonElement value)
                                  && value.ValueKind is JsonValueKind.String or JsonValueKind.Number))
            {
                string[] values = items.Select(item => IdentityText(item.GetProperty(candidate))).ToArray();
                if (values.Distinct(StringComparer.Ordinal).Count() == values.Length)
                {
                    propertyName = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsForcedSequence(string path) =>
        path.EndsWith("/plugins", StringComparison.Ordinal)
        || path.EndsWith("/contributions", StringComparison.Ordinal)
        || path.EndsWith("/masters", StringComparison.Ordinal);

    private static string IdentityText(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? value.GetString()!
        : value.GetRawText();

    private static void Add(
        string path,
        string factType,
        string valueType,
        JsonElement value,
        List<SemanticFact> facts) =>
        facts.Add(new SemanticFact(path, factType, valueType, value));

    private static string TypeFor(string path)
    {
        string lower = path.ToLowerInvariant();
        if (lower.Contains("/gaps", StringComparison.Ordinal) || lower.Contains("gap_", StringComparison.Ordinal)) { return "gap"; }
        if (lower.Contains("/failures", StringComparison.Ordinal)) { return "failure"; }
        if (lower.Contains("face_gen", StringComparison.Ordinal)) { return "face_gen"; }
        if (lower.Contains("taxonomy", StringComparison.Ordinal)) { return "taxonomy"; }
        if (lower.Contains("coverage", StringComparison.Ordinal)) { return "coverage"; }
        if (lower.Contains("winner", StringComparison.Ordinal)) { return "winner"; }
        if (lower.Contains("owner", StringComparison.Ordinal)) { return "ownership"; }
        if (lower.Contains("placement", StringComparison.Ordinal) || lower.Contains("position", StringComparison.Ordinal) || lower.Contains("rotation", StringComparison.Ordinal)) { return "placement"; }
        if (lower.Contains("override_chain", StringComparison.Ordinal) || lower.Contains("contributions", StringComparison.Ordinal)) { return "override_chain"; }
        if (lower.Contains("link", StringComparison.Ordinal) || lower.Contains("target_", StringComparison.Ordinal)) { return "link"; }
        if (lower.Contains("form_key", StringComparison.Ordinal) || lower.Contains("origin_local_id", StringComparison.Ordinal)) { return "form_key"; }
        if (lower.Contains("allowlisted_fields", StringComparison.Ordinal) || lower.EndsWith("/field", StringComparison.Ordinal)) { return "field"; }
        if (lower.Contains("plugins", StringComparison.Ordinal) || lower.Contains("plugin_", StringComparison.Ordinal)) { return "plugin"; }
        return "semantic";
    }

    private static string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    [GeneratedRegex(@"^[^:/\\]+\.(?:esm|esp|esl):[0-9a-fA-F]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex FormKeyPattern();
}
