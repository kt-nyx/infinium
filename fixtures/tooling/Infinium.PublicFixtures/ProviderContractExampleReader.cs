using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;

namespace Infinium.PublicFixtures;

public static class ProviderContractExampleReader
{
    public const string AuthorityRelativePath =
        "fixtures/public/contracts/provider-contract-examples/contract-examples.v1.json";
    public const string PackageIdentity =
        "infinium.public-fixtures.provider-contracts.answer-free-examples";

    private const int MaximumAuthorityBytes = 1_048_576;
    private static readonly string[] SchemaNames =
    [
        "provider-access-profile.v1.schema.json",
        "provider-operation.v1.schema.json",
        "provider-response.v1.schema.json",
        "source-claim-extraction.v1.schema.json",
        "candidate-investigation.v1.schema.json",
        "provider-execution-input.v1.schema.json",
        "effective-scan-configuration.v2.schema.json",
        "run-output.v2.schema.json",
        "cli-summary.v2.schema.json",
    ];

    public static int Validate(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        string path = Path.Combine(repositoryRoot, AuthorityRelativePath.Replace('/', Path.DirectorySeparatorChar));
        FileInfo info = new(path);
        if (!info.Exists || info.Length is <= 0 or > MaximumAuthorityBytes)
        {
            throw new InvalidDataException("Provider contract-example authority is absent or outside its finite bound.");
        }

        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllBytes(path),
            new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 64 });
        BoundedJsonDocumentReader.RejectDuplicateProperties(document.RootElement, AuthorityRelativePath);
        JsonElement root = document.RootElement;
        RequireString(root, "package_identity", PackageIdentity);
        RequireString(root, "package_version", "1.0.0");
        RequireString(root, "partition", "development");
        RequireString(root, "status", "Proposed");
        if (!root.GetProperty("answer_free").GetBoolean())
        {
            throw new InvalidDataException("Provider examples must remain explicitly answer-free.");
        }

        HashSet<string> expectedRoot = new(
            ["package_identity", "package_version", "partition", "status", "answer_free", "post_fact_usage_examples", "examples"],
            StringComparer.Ordinal);
        if (!root.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expectedRoot))
        {
            throw new InvalidDataException("Provider example authority has an unrecognized root field.");
        }

        JsonElement[] usageExamples = root.GetProperty("post_fact_usage_examples").EnumerateArray().ToArray();
        if (usageExamples.Length != 3
            || usageExamples.Select(x => x.GetProperty("case").GetString()).ToArray() is not ["below", "equal", "above"]
            || usageExamples.Any(x => !x.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
                .SetEquals(["case", "authorized_input_tokens", "observed_input_tokens", "reserved_nano_usd", "observed_nano_usd", "settlement_state"]))
            || !ValidUsageExample(usageExamples[0], expectedComparison: -1, expectedSettlement: "settled")
            || !ValidUsageExample(usageExamples[1], expectedComparison: 0, expectedSettlement: "settled")
            || !ValidUsageExample(usageExamples[2], expectedComparison: 1, expectedSettlement: "overrun"))
        {
            throw new InvalidDataException("Provider examples must retain exact answer-free below/equal/above post-fact usage settlement vectors.");
        }

        JsonElement examples = root.GetProperty("examples");
        HashSet<string> actualSchemas = examples.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        if (!actualSchemas.SetEquals(SchemaNames))
        {
            throw new InvalidDataException("Provider examples must cover exactly the nine WP1 schemas.");
        }

        foreach (string schema in SchemaNames)
        {
            ActiveJsonSchemaValidator.Validate(ProjectHistoricalSemanticFieldNames(
                examples.GetProperty(schema), schema), schema);
        }

        string authorityText = root.GetRawText();
        foreach (string forbidden in new[] { "expected_answer", "expected_label", "oracle", "provider_secret", "credential_target", "authorization_header" })
        {
            if (authorityText.Contains($"\"{forbidden}\"", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Provider examples contain forbidden answer/secret field '{forbidden}'.");
            }
        }

        return SchemaNames.Length;
    }

    private static JsonElement ProjectHistoricalSemanticFieldNames(JsonElement example, string schema)
    {
        if (schema is not ("source-claim-extraction.v1.schema.json" or "candidate-investigation.v1.schema.json"))
        {
            return example;
        }

        // The WP1 answer-free package is immutable prior evidence. Validate its historical
        // single-axis `state` spelling against the clean-break contract by projecting only
        // that field name in memory; product codecs do not accept the historical shape.
        JsonObject projected = JsonNode.Parse(example.GetRawText())!.AsObject();
        string replacement = schema == "source-claim-extraction.v1.schema.json"
            ? "extraction_state"
            : "proposal_state";
        string proposalsProperty = schema == "source-claim-extraction.v1.schema.json"
            ? "claim_proposals"
            : "hypothesis_proposals";
        foreach (JsonObject proposal in projected[proposalsProperty]!.AsArray().Select(node => node!.AsObject()))
        {
            JsonNode? state = proposal["state"]?.DeepClone();
            if (!proposal.Remove("state") || state is null)
            {
                throw new InvalidDataException("Historical provider semantic example is missing its frozen state field.");
            }
            proposal[replacement] = state;
        }
        return JsonSerializer.SerializeToElement(projected);
    }

    private static bool ValidUsageExample(JsonElement value, int expectedComparison, string expectedSettlement)
    {
        long authorizedInput = value.GetProperty("authorized_input_tokens").GetInt64();
        long observedInput = value.GetProperty("observed_input_tokens").GetInt64();
        long reservedCost = value.GetProperty("reserved_nano_usd").GetInt64();
        long observedCost = value.GetProperty("observed_nano_usd").GetInt64();
        return authorizedInput > 0 && observedInput >= 0 && reservedCost > 0 && observedCost >= 0
            && Math.Sign(observedInput.CompareTo(authorizedInput)) == expectedComparison
            && Math.Sign(observedCost.CompareTo(reservedCost)) == expectedComparison
            && value.GetProperty("settlement_state").GetString() == expectedSettlement;
    }

    private static void RequireString(JsonElement root, string property, string expected)
    {
        if (root.GetProperty(property).GetString() != expected)
        {
            throw new InvalidDataException($"Provider examples have invalid {property}.");
        }
    }
}
