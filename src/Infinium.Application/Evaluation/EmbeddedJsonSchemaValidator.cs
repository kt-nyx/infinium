using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Infinium.Application.Evaluation;

internal static class EmbeddedJsonSchemaValidator
{
    private const string ResourcePrefix = "Infinium.Contracts.JsonSchema.";
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly ConcurrentDictionary<string, JsonDocument> Schemas =
        new(StringComparer.Ordinal);
    private static readonly HashSet<string> SupportedSchemaKeywords = new(StringComparer.Ordinal)
    {
        "$schema",
        "$id",
        "$ref",
        "$defs",
        "title",
        "description",
        "type",
        "additionalProperties",
        "required",
        "properties",
        "const",
        "enum",
        "items",
        "minItems",
        "maxItems",
        "uniqueItems",
        "minLength",
        "maxLength",
        "pattern",
        "format",
        "minimum",
        "maximum",
        "allOf",
        "anyOf",
        "oneOf",
        "not",
        "if",
        "then",
        "else",
    };

    internal static void Validate(JsonElement instance, string schemaFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaFileName);
        JsonElement schema = LoadSchema(schemaFileName).RootElement;
        ValidateNode(instance, schema, schemaFileName, schemaFileName, "$");
    }

    private static void ValidateNode(
        JsonElement instance,
        JsonElement schema,
        string currentSchemaFile,
        string rootSchemaFile,
        string instancePath)
    {
        if (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            (JsonElement target, string targetFile) = ResolveReference(
                reference.GetString()!,
                currentSchemaFile,
                rootSchemaFile);
            ValidateNode(instance, target, targetFile, targetFile, instancePath);
        }

        if (schema.TryGetProperty("type", out JsonElement type)
            && !MatchesType(instance, type))
        {
            Fail(instancePath, $"expected type '{type.GetRawText()}'");
        }

        if (schema.TryGetProperty("const", out JsonElement constant)
            && !JsonElement.DeepEquals(instance, constant))
        {
            Fail(instancePath, "does not equal the required constant");
        }

        if (schema.TryGetProperty("enum", out JsonElement choices)
            && !choices.EnumerateArray().Any(choice => JsonElement.DeepEquals(instance, choice)))
        {
            Fail(instancePath, "is outside the closed enum");
        }

        ValidateCombinators(instance, schema, currentSchemaFile, rootSchemaFile, instancePath);

        if (instance.ValueKind == JsonValueKind.Object)
        {
            ValidateObject(instance, schema, currentSchemaFile, rootSchemaFile, instancePath);
        }
        else if (instance.ValueKind == JsonValueKind.Array)
        {
            ValidateArray(instance, schema, currentSchemaFile, rootSchemaFile, instancePath);
        }
        else if (instance.ValueKind == JsonValueKind.String)
        {
            ValidateString(instance.GetString()!, schema, instancePath);
        }
        else if (instance.ValueKind == JsonValueKind.Number)
        {
            ValidateNumber(instance, schema, instancePath);
        }
    }

    private static void ValidateCombinators(
        JsonElement instance,
        JsonElement schema,
        string currentSchemaFile,
        string rootSchemaFile,
        string instancePath)
    {
        if (schema.TryGetProperty("allOf", out JsonElement allOf))
        {
            foreach (JsonElement branch in allOf.EnumerateArray())
            {
                ValidateNode(instance, branch, currentSchemaFile, rootSchemaFile, instancePath);
            }
        }

        if (schema.TryGetProperty("anyOf", out JsonElement anyOf)
            && !anyOf.EnumerateArray().Any(
                branch => IsValid(instance, branch, currentSchemaFile, rootSchemaFile, instancePath)))
        {
            Fail(instancePath, "does not satisfy any allowed schema branch");
        }

        if (schema.TryGetProperty("oneOf", out JsonElement oneOf)
            && oneOf.EnumerateArray().Count(
                branch => IsValid(instance, branch, currentSchemaFile, rootSchemaFile, instancePath)) != 1)
        {
            Fail(instancePath, "does not satisfy exactly one schema branch");
        }

        if (schema.TryGetProperty("not", out JsonElement not)
            && IsValid(instance, not, currentSchemaFile, rootSchemaFile, instancePath))
        {
            Fail(instancePath, "matches a forbidden schema branch");
        }

        if (schema.TryGetProperty("if", out JsonElement condition))
        {
            bool conditionMatches = IsValid(
                instance,
                condition,
                currentSchemaFile,
                rootSchemaFile,
                instancePath);
            if (conditionMatches && schema.TryGetProperty("then", out JsonElement then))
            {
                ValidateNode(instance, then, currentSchemaFile, rootSchemaFile, instancePath);
            }
            else if (!conditionMatches && schema.TryGetProperty("else", out JsonElement @else))
            {
                ValidateNode(instance, @else, currentSchemaFile, rootSchemaFile, instancePath);
            }
        }
    }

    private static void ValidateObject(
        JsonElement instance,
        JsonElement schema,
        string currentSchemaFile,
        string rootSchemaFile,
        string instancePath)
    {
        if (schema.TryGetProperty("required", out JsonElement required))
        {
            foreach (JsonElement propertyName in required.EnumerateArray())
            {
                string name = propertyName.GetString()!;
                if (!instance.TryGetProperty(name, out _))
                {
                    Fail(instancePath, $"is missing required property '{name}'");
                }
            }
        }

        bool hasProperties = schema.TryGetProperty("properties", out JsonElement properties);
        if (hasProperties)
        {
            foreach (JsonProperty propertySchema in properties.EnumerateObject())
            {
                if (instance.TryGetProperty(propertySchema.Name, out JsonElement value))
                {
                    ValidateNode(
                        value,
                        propertySchema.Value,
                        currentSchemaFile,
                        rootSchemaFile,
                        $"{instancePath}.{propertySchema.Name}");
                }
            }
        }

        if (schema.TryGetProperty("additionalProperties", out JsonElement additional)
            && additional.ValueKind == JsonValueKind.False)
        {
            foreach (JsonProperty property in instance.EnumerateObject())
            {
                if (!hasProperties || !properties.TryGetProperty(property.Name, out _))
                {
                    Fail(instancePath, $"contains unsupported property '{property.Name}'");
                }
            }
        }
    }

    private static void ValidateArray(
        JsonElement instance,
        JsonElement schema,
        string currentSchemaFile,
        string rootSchemaFile,
        string instancePath)
    {
        int length = instance.GetArrayLength();
        if (schema.TryGetProperty("minItems", out JsonElement minimum)
            && length < minimum.GetInt32())
        {
            Fail(instancePath, $"requires at least {minimum.GetInt32()} items");
        }

        if (schema.TryGetProperty("maxItems", out JsonElement maximum)
            && length > maximum.GetInt32())
        {
            Fail(instancePath, $"permits at most {maximum.GetInt32()} items");
        }

        if (schema.TryGetProperty("uniqueItems", out JsonElement unique)
            && unique.ValueKind == JsonValueKind.True)
        {
            JsonElement[] values = instance.EnumerateArray().ToArray();
            for (int left = 0; left < values.Length; left++)
            {
                for (int right = left + 1; right < values.Length; right++)
                {
                    if (JsonElement.DeepEquals(values[left], values[right]))
                    {
                        Fail(instancePath, "requires unique items");
                    }
                }
            }
        }

        if (schema.TryGetProperty("items", out JsonElement itemSchema))
        {
            int index = 0;
            foreach (JsonElement item in instance.EnumerateArray())
            {
                ValidateNode(
                    item,
                    itemSchema,
                    currentSchemaFile,
                    rootSchemaFile,
                    $"{instancePath}[{index}]");
                index++;
            }
        }
    }

    private static void ValidateString(string value, JsonElement schema, string instancePath)
    {
        if (schema.TryGetProperty("minLength", out JsonElement minimum)
            && value.Length < minimum.GetInt32())
        {
            Fail(instancePath, $"requires at least {minimum.GetInt32()} characters");
        }

        if (schema.TryGetProperty("maxLength", out JsonElement maximum)
            && value.Length > maximum.GetInt32())
        {
            Fail(instancePath, $"permits at most {maximum.GetInt32()} characters");
        }

        if (schema.TryGetProperty("pattern", out JsonElement pattern)
            && !Regex.IsMatch(
                value,
                pattern.GetString()!,
                RegexOptions.CultureInvariant,
                RegexTimeout))
        {
            Fail(instancePath, "does not match the required pattern");
        }

        if (schema.TryGetProperty("format", out JsonElement format)
            && StringComparer.Ordinal.Equals(format.GetString(), "date-time")
            && !IsCanonicalUtcTimestamp(value))
        {
            Fail(instancePath, "is not a canonical UTC date-time");
        }
    }

    private static void ValidateNumber(JsonElement instance, JsonElement schema, string instancePath)
    {
        if (!instance.TryGetDecimal(out decimal value))
        {
            Fail(instancePath, "is outside the supported numeric contract range");
        }

        if (schema.TryGetProperty("minimum", out JsonElement minimum)
            && value < minimum.GetDecimal())
        {
            Fail(instancePath, $"must be at least {minimum.GetDecimal()}");
        }

        if (schema.TryGetProperty("maximum", out JsonElement maximum)
            && value > maximum.GetDecimal())
        {
            Fail(instancePath, $"must be at most {maximum.GetDecimal()}");
        }
    }

    private static bool MatchesType(JsonElement instance, JsonElement type)
    {
        return type.ValueKind switch
        {
            JsonValueKind.String => MatchesType(instance, type.GetString()!),
            JsonValueKind.Array => type.EnumerateArray().Any(item => MatchesType(instance, item.GetString()!)),
            _ => false,
        };
    }

    private static bool MatchesType(JsonElement instance, string type)
    {
        return type switch
        {
            "object" => instance.ValueKind == JsonValueKind.Object,
            "array" => instance.ValueKind == JsonValueKind.Array,
            "string" => instance.ValueKind == JsonValueKind.String,
            "integer" => instance.ValueKind == JsonValueKind.Number && instance.TryGetInt64(out _),
            "number" => instance.ValueKind == JsonValueKind.Number,
            "boolean" => instance.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => instance.ValueKind == JsonValueKind.Null,
            _ => throw new InvalidDataException($"Unsupported embedded schema type '{type}'."),
        };
    }

    private static bool IsValid(
        JsonElement instance,
        JsonElement schema,
        string currentSchemaFile,
        string rootSchemaFile,
        string instancePath)
    {
        try
        {
            ValidateNode(instance, schema, currentSchemaFile, rootSchemaFile, instancePath);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static (JsonElement Schema, string FileName) ResolveReference(
        string reference,
        string currentSchemaFile,
        string rootSchemaFile)
    {
        if (Uri.TryCreate(reference, UriKind.Absolute, out _))
        {
            throw new InvalidDataException($"Remote JSON Schema reference '{reference}' is forbidden.");
        }

        string[] parts = reference.Split('#', 2);
        string fileName = parts[0].Length == 0 ? currentSchemaFile : parts[0];
        JsonElement target = LoadSchema(fileName).RootElement;
        if (parts.Length == 1 || parts[1].Length == 0)
        {
            return (target, fileName);
        }

        if (!parts[1].StartsWith('/'))
        {
            throw new InvalidDataException(
                $"Unsupported JSON Schema reference fragment '{reference}' in '{rootSchemaFile}'.");
        }

        foreach (string encodedSegment in parts[1].Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            string segment = Uri.UnescapeDataString(encodedSegment)
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);
            if (target.ValueKind != JsonValueKind.Object
                || !target.TryGetProperty(segment, out target))
            {
                throw new InvalidDataException(
                    $"Unresolved JSON Schema reference '{reference}' in '{rootSchemaFile}'.");
            }
        }

        return (target, fileName);
    }

    private static JsonDocument LoadSchema(string fileName)
    {
        if (fileName.Contains('/', StringComparison.Ordinal)
            || fileName.Contains('\\', StringComparison.Ordinal))
        {
            throw new InvalidDataException($"JSON Schema reference '{fileName}' is not repository-local.");
        }

        return Schemas.GetOrAdd(
            fileName,
            static name =>
            {
                string resourceName = $"{ResourcePrefix}{name}";
                Assembly assembly = typeof(EmbeddedJsonSchemaValidator).Assembly;
                Stream? resource = assembly.GetManifestResourceStream(resourceName);
                if (resource is null)
                {
                    resource = AppDomain.CurrentDomain.GetAssemblies()
                        .OrderBy(candidate => candidate.FullName, StringComparer.Ordinal)
                        .Select(candidate => candidate.GetManifestResourceStream(resourceName))
                        .FirstOrDefault(candidate => candidate is not null);
                }

                using Stream stream = resource
                    ?? throw new InvalidDataException($"Embedded JSON Schema '{name}' is missing.");
                JsonDocument document = JsonDocument.Parse(stream);
                BoundedJsonDocumentReader.RejectDuplicateProperties(document.RootElement, name);
                EnsureSupportedSchemaVocabulary(document.RootElement, name);
                return document;
            });
    }

    private static bool IsCanonicalUtcTimestamp(string value)
    {
        return DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTimeOffset parsed)
            && parsed.Offset == TimeSpan.Zero
            && StringComparer.Ordinal.Equals(value, parsed.ToString("O", CultureInfo.InvariantCulture));
    }

    private static void EnsureSupportedSchemaVocabulary(JsonElement schema, string path)
    {
        foreach (JsonProperty keyword in schema.EnumerateObject())
        {
            if (!SupportedSchemaKeywords.Contains(keyword.Name))
            {
                throw new InvalidDataException(
                    $"Embedded JSON Schema '{path}' uses unsupported keyword '{keyword.Name}'.");
            }

            if (keyword.Name is "properties" or "$defs")
            {
                foreach (JsonProperty child in keyword.Value.EnumerateObject())
                {
                    EnsureSupportedSchemaVocabulary(child.Value, $"{path}/{keyword.Name}/{child.Name}");
                }
            }
            else if (keyword.Name is "allOf" or "anyOf" or "oneOf")
            {
                int index = 0;
                foreach (JsonElement child in keyword.Value.EnumerateArray())
                {
                    EnsureSupportedSchemaVocabulary(child, $"{path}/{keyword.Name}/{index}");
                    index++;
                }
            }
            else if (keyword.Name is "items" or "not" or "if" or "then" or "else")
            {
                EnsureSupportedSchemaVocabulary(keyword.Value, $"{path}/{keyword.Name}");
            }
        }
    }

    private static void Fail(string path, string reason)
    {
        throw new InvalidDataException($"JSON Schema validation failed at '{path}': {reason}.");
    }
}
