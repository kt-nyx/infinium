using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Evaluation;

internal static class BethesdaByteOracleValidator
{
    internal const string ArtifactId = "oracle/independent-byte-facts.json";
    private const long MaximumOracleBytes = 16 * 1024 * 1024;

    internal static void Validate(
        JsonElement executionInput,
        JsonElement expectedOracle,
        OpaqueId fixtureId,
        ContractVersion fixtureVersion,
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> inputSnapshots,
        IReadOnlyDictionary<string, RetainedArtifactSnapshot> oracleSnapshots)
    {
        JsonElement? oracleReference = FindArtifactReference(expectedOracle, ArtifactId);
        if (oracleReference is null)
        {
            return;
        }

        if (!oracleSnapshots.TryGetValue(ArtifactId, out RetainedArtifactSnapshot? snapshot))
        {
            throw new InvalidDataException(
                "Supplemental Bethesda byte oracle was not captured as a validated retained artifact.");
        }

        if (snapshot.ByteLength > MaximumOracleBytes)
        {
            throw new InvalidDataException(
                "Supplemental Bethesda byte oracle exceeds the byte bound.");
        }

        using JsonDocument document = ParseOracleSnapshot(snapshot);
        BoundedJsonDocumentReader.RejectDuplicateProperties(document.RootElement, "$");
        JsonElement root = document.RootElement;
        EmbeddedJsonSchemaValidator.Validate(root, "bethesda-byte-oracle.v1.schema.json");

        if (!StringComparer.Ordinal.Equals(
                RequireString(root, "fixture_id"),
                fixtureId.Value)
            || ContractVersion.Parse(RequireString(root, "fixture_version")) != fixtureVersion)
        {
            throw new InvalidDataException(
                "Supplemental Bethesda byte oracle identity does not match the fixture.");
        }

        Dictionary<string, string> inputArtifacts = CollectInputArtifacts(executionInput);
        HashSet<string> methods = ReadUniqueStrings(
            RequireArray(root, "ground_truth_method_ids"),
            "ground-truth method");
        HashSet<string> expectedMethods = ReadExpectedMethodIds(expectedOracle);
        if (!methods.IsSubsetOf(expectedMethods))
        {
            throw new InvalidDataException(
                "Supplemental Bethesda byte oracle references an undeclared oracle method.");
        }

        HashSet<string> fileIds = [];
        Dictionary<string, HashSet<int>> scenarioOrders = new(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> scenarioFiles = new(StringComparer.Ordinal);
        foreach (JsonElement file in RequireArray(root, "files").EnumerateArray())
        {
            string artifactId = RequireString(file, "artifact_id");
            if (!fileIds.Add(artifactId))
            {
                throw new InvalidDataException(
                    $"Supplemental Bethesda byte oracle contains duplicate file ID '{artifactId}'.");
            }

            if (!inputArtifacts.TryGetValue(artifactId, out string? inputFingerprint)
                || !StringComparer.Ordinal.Equals(
                    inputFingerprint,
                    RequireString(file, "sha256")))
            {
                throw new InvalidDataException(
                    $"Oracle file '{artifactId}' is not bound to the matching execution payload.");
            }

            if (!inputSnapshots.TryGetValue(artifactId, out RetainedArtifactSnapshot? inputSnapshot))
            {
                throw new InvalidDataException(
                    $"Oracle file '{artifactId}' is not bound to a validated retained payload snapshot.");
            }

            long actualByteLength = inputSnapshot.ByteLength;
            if (file.GetProperty("byte_length").GetInt64() != actualByteLength)
            {
                throw new InvalidDataException(
                    $"Oracle file '{artifactId}' byte length does not match the retained payload.");
            }

            ValidateObservationState(
                file,
                artifactId,
                "masters_state",
                "masters",
                JsonValueKind.Array);
            ValidateObservationState(
                file,
                artifactId,
                "esl_flag_state",
                "esl_flag",
                JsonValueKind.True,
                JsonValueKind.False);

            HashSet<string> fileScenarios = new(StringComparer.Ordinal);
            foreach (JsonElement membership in RequireArray(
                file,
                "scenario_memberships").EnumerateArray())
            {
                string scenarioId = RequireString(membership, "scenario_id");
                int pluginOrder = membership.GetProperty("plugin_order").GetInt32();
                if (!fileScenarios.Add(scenarioId)
                    || !scenarioOrders
                        .GetValueOrDefault(scenarioId, [])
                        .Add(pluginOrder)
                    || !scenarioFiles
                        .GetValueOrDefault(scenarioId, [])
                        .Add(artifactId))
                {
                    throw new InvalidDataException(
                        $"Oracle file '{artifactId}' has a duplicate scenario/order membership.");
                }

                if (!scenarioOrders.ContainsKey(scenarioId))
                {
                    scenarioOrders[scenarioId] = [pluginOrder];
                    scenarioFiles[scenarioId] = [artifactId];
                }
            }

            ValidatePhysicalByteCoverage(file, artifactId);
        }

        HashSet<string> executablePluginArtifacts = inputArtifacts.Keys
            .Where(IsPluginArtifact)
            .ToHashSet(StringComparer.Ordinal);
        if (!fileIds.SetEquals(executablePluginArtifacts))
        {
            throw new InvalidDataException(
                "Supplemental Bethesda oracle files must exactly cover execution plugin payloads.");
        }

        foreach ((string scenarioId, HashSet<int> orders) in scenarioOrders)
        {
            if (!orders.SetEquals(Enumerable.Range(0, orders.Count)))
            {
                throw new InvalidDataException(
                    $"Supplemental Bethesda scenario '{scenarioId}' plugin order is not contiguous.");
            }
        }

        Dictionary<string, ExpectedItem> expectedItems = ReadExpectedItems(expectedOracle);
        HashSet<string> factIds = [];
        foreach (JsonElement fact in RequireArray(root, "facts").EnumerateArray())
        {
            string factId = RequireString(fact, "fact_id");
            if (!factIds.Add(factId))
            {
                throw new InvalidDataException(
                    $"Supplemental Bethesda byte oracle contains duplicate fact ID '{factId}'.");
            }

            HashSet<string> factMethods = ReadUniqueStrings(
                RequireArray(fact, "ground_truth_method_ids"),
                $"fact '{factId}' ground-truth method");
            if (!factMethods.IsSubsetOf(methods))
            {
                throw new InvalidDataException(
                    $"Fact '{factId}' references an undeclared supplemental method.");
            }

            JsonElement canonicalValue = fact.GetProperty("canonical_value");
            if (canonicalValue.EnumerateObject().Count() < 2)
            {
                throw new InvalidDataException(
                    $"Fact '{factId}' canonical value does not contain a typed value.");
            }

            string actualFingerprint = ComputeCanonicalFingerprint(canonicalValue);
            string declaredFingerprint = RequireString(fact, "canonical_value_fingerprint");
            if (!StringComparer.Ordinal.Equals(declaredFingerprint, actualFingerprint))
            {
                throw new InvalidDataException(
                    $"Fact '{factId}' canonical value fingerprint does not match.");
            }

            if (canonicalValue.TryGetProperty("artifact_id", out JsonElement artifactIdElement)
                && artifactIdElement.ValueKind == JsonValueKind.String
                && !fileIds.Contains(artifactIdElement.GetString()!))
            {
                throw new InvalidDataException(
                    $"Fact '{factId}' references an unknown oracle file.");
            }

            if (!expectedItems.TryGetValue(factId, out ExpectedItem? expectedItem)
                || expectedItem is null
                || !StringComparer.Ordinal.Equals(
                    expectedItem.CanonicalValueFingerprint,
                    actualFingerprint)
                || !factMethods.SetEquals(expectedItem.GroundTruthMethodIds))
            {
                throw new InvalidDataException(
                    $"Fact '{factId}' does not match its expected-oracle item.");
            }
        }

        HashSet<string> byteExpectedIds = expectedItems
            .Where(item => item.Value.GroundTruthMethodIds.Overlaps(methods))
            .Select(item => item.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (!factIds.SetEquals(byteExpectedIds))
        {
            throw new InvalidDataException(
                "Supplemental Bethesda facts and byte-method expected items do not form an exact set.");
        }

        HashSet<string> mutationIds = [];
        foreach (JsonElement mutation in RequireArray(root, "mutation_expectations").EnumerateArray())
        {
            string mutationId = RequireString(mutation, "mutation_id");
            if (!mutationIds.Add(mutationId))
            {
                throw new InvalidDataException(
                    $"Supplemental Bethesda byte oracle contains duplicate mutation ID '{mutationId}'.");
            }

            string targetArtifactId = RequireString(mutation, "target_artifact_id");
            if (!fileIds.Contains(targetArtifactId))
            {
                throw new InvalidDataException(
                    $"Mutation '{mutationId}' references unknown file '{targetArtifactId}'.");
            }

            HashSet<string> changed = ReadUniqueStrings(
                RequireArray(mutation, "changed_fact_ids"),
                $"mutation '{mutationId}' changed fact");
            HashSet<string> unchanged = ReadUniqueStrings(
                RequireArray(mutation, "unchanged_fact_ids"),
                $"mutation '{mutationId}' unchanged fact");
            if (changed.Overlaps(unchanged)
                || !changed.IsSubsetOf(factIds)
                || !unchanged.IsSubsetOf(factIds)
                || changed.Count + unchanged.Count != factIds.Count)
            {
                throw new InvalidDataException(
                    $"Mutation '{mutationId}' has inconsistent fact dependencies.");
            }
        }
    }

    private static JsonDocument ParseOracleSnapshot(RetainedArtifactSnapshot snapshot)
    {
        try
        {
            return JsonDocument.Parse(
                snapshot.Bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 96,
                });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Supplemental Bethesda byte oracle is not valid strict JSON.",
                exception);
        }
    }

    internal static string ComputeCanonicalFingerprint(JsonElement value)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(
            stream,
            new JsonWriterOptions
            {
                Indented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
            }))
        {
            WriteCanonical(value, writer);
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonical(JsonElement value, Utf8JsonWriter writer)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in value.EnumerateObject().OrderBy(
                    property => property.Name,
                    StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in value.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }

                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;
            case JsonValueKind.Number:
                if (!value.TryGetInt64(out long signed))
                {
                    throw new InvalidDataException(
                        "Canonical Bethesda oracle values permit JSON integers only.");
                }

                writer.WriteNumberValue(signed);
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                throw new InvalidDataException(
                    "Unsupported canonical Bethesda oracle JSON value.");
        }
    }

    private static void ValidatePhysicalByteCoverage(JsonElement file, string artifactId)
    {
        long byteLength = file.GetProperty("byte_length").GetInt64();
        List<(long Offset, long Length)> spans = [];
        HashSet<string> spanIds = new(StringComparer.Ordinal);
        foreach (JsonElement span in RequireArray(file, "byte_coverage").EnumerateArray())
        {
            string spanId = RequireString(span, "span_id");
            if (!spanIds.Add(spanId))
            {
                throw new InvalidDataException(
                    $"Oracle file '{artifactId}' contains duplicate span ID '{spanId}'.");
            }

            if (StringComparer.Ordinal.Equals(
                    RequireString(span, "offset_space"),
                    "physical-file"))
            {
                long length = span.GetProperty("length").GetInt64();
                if (length == 0)
                {
                    throw new InvalidDataException(
                        $"Oracle file '{artifactId}' contains a zero-length physical span.");
                }

                spans.Add((span.GetProperty("offset").GetInt64(), length));
            }
        }

        long nextOffset = 0;
        foreach ((long offset, long length) in spans.OrderBy(span => span.Offset))
        {
            if (offset != nextOffset || length < 0 || length > byteLength - offset)
            {
                throw new InvalidDataException(
                    $"Oracle file '{artifactId}' physical byte coverage has a gap, overlap, or overrun.");
            }

            nextOffset = checked(offset + length);
        }

        if (nextOffset != byteLength)
        {
            throw new InvalidDataException(
                $"Oracle file '{artifactId}' physical byte coverage is incomplete.");
        }
    }

    private static void ValidateObservationState(
        JsonElement file,
        string artifactId,
        string stateProperty,
        string valueProperty,
        params JsonValueKind[] observedKinds)
    {
        string state = RequireString(file, stateProperty);
        JsonValueKind valueKind = file.GetProperty(valueProperty).ValueKind;
        if ((state == "observed" && !observedKinds.Contains(valueKind))
            || (state == "unknown" && valueKind != JsonValueKind.Null))
        {
            throw new InvalidDataException(
                $"Oracle file '{artifactId}' has inconsistent {valueProperty} observation state.");
        }
    }

    private static bool IsPluginArtifact(string artifactId)
    {
        string extension = Path.GetExtension(artifactId);
        return extension.Equals(".esm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".esp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".esl", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> CollectInputArtifacts(JsonElement executionInput)
    {
        Dictionary<string, string> artifacts = new(StringComparer.Ordinal);
        foreach (JsonElement reference in RequireArray(
            executionInput,
            "input_payload_refs").EnumerateArray())
        {
            string artifactId = RequireString(reference, "artifact_id");
            if (!artifactId.StartsWith("inputs/", StringComparison.Ordinal))
            {
                continue;
            }

            if (!artifacts.TryAdd(artifactId, RequireString(reference, "fingerprint")))
            {
                throw new InvalidDataException(
                    $"Execution input contains duplicate payload artifact '{artifactId}'.");
            }
        }

        return artifacts;
    }

    private static JsonElement? FindArtifactReference(JsonElement root, string artifactId)
    {
        foreach (JsonElement method in RequireArray(root, "ground_truth_methods").EnumerateArray())
        {
            foreach (JsonElement reference in RequireArray(
                method,
                "evidence_references").EnumerateArray())
            {
                if (StringComparer.Ordinal.Equals(
                        RequireString(reference, "artifact_id"),
                        artifactId))
                {
                    return reference;
                }
            }
        }

        return null;
    }

    private static HashSet<string> ReadExpectedMethodIds(JsonElement oracle)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach (JsonElement method in RequireArray(
            oracle,
            "ground_truth_methods").EnumerateArray())
        {
            if (!result.Add(RequireString(method, "method_id")))
            {
                throw new InvalidDataException("Expected oracle contains duplicate method IDs.");
            }
        }

        return result;
    }

    private static Dictionary<string, ExpectedItem> ReadExpectedItems(JsonElement oracle)
    {
        string[] collectionNames =
        [
            "expected_observations",
            "expected_deterministic_results",
            "expected_external_claims",
            "expected_application_links",
            "expected_discovery_leads",
            "expected_model_proposals",
            "expected_proposal_admissions",
            "expected_candidates",
            "expected_hypotheses",
            "expected_findings",
            "expected_recommendations",
            "expected_supported_cases",
            "expected_lead_only_cases",
            "expected_abstentions",
            "expected_invalid_inputs",
            "expected_failures",
            "expected_coverage_and_gaps",
        ];
        Dictionary<string, ExpectedItem> result = new(StringComparer.Ordinal);
        foreach (string collectionName in collectionNames)
        {
            foreach (JsonElement item in RequireArray(oracle, collectionName).EnumerateArray())
            {
                string expectedId = RequireString(item, "expected_id");
                HashSet<string> methods = ReadUniqueStrings(
                    RequireArray(item, "ground_truth_method_ids"),
                    $"expected item '{expectedId}' method");
                if (!result.TryAdd(
                        expectedId,
                        new ExpectedItem(
                            RequireString(item, "canonical_value_fingerprint"),
                            methods)))
                {
                    throw new InvalidDataException(
                        $"Expected oracle contains duplicate item '{expectedId}'.");
                }
            }
        }

        return result;
    }

    private static HashSet<string> ReadUniqueStrings(JsonElement array, string description)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach (JsonElement element in array.EnumerateArray())
        {
            string value = element.GetString()
                ?? throw new InvalidDataException($"{description} must be a string.");
            if (!result.Add(value))
            {
                throw new InvalidDataException($"Duplicate {description} '{value}'.");
            }
        }

        return result;
    }

    private static JsonElement RequireArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"Required array property '{propertyName}' is missing.");
        }

        return value;
    }

    private static string RequireString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"Required string property '{propertyName}' is missing.");
        }

        return value.GetString()!;
    }

    private sealed record ExpectedItem(
        string CanonicalValueFingerprint,
        HashSet<string> GroundTruthMethodIds);
}
