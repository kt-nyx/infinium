using System.Text.Json;
using Infinium.Application.Evaluation;

namespace Infinium.PublicFixtures;

public static class ProviderContractExampleReader
{
    public const string AuthorityRelativePath =
        "fixtures/public/contracts/provider-wp1/contract-examples.v1.json";
    public const string PackageIdentity =
        "infinium.public-fixtures.provider-contracts.wp1.answer-free";

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
            ["package_identity", "package_version", "partition", "status", "answer_free", "examples"],
            StringComparer.Ordinal);
        if (!root.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expectedRoot))
        {
            throw new InvalidDataException("Provider example authority has an unrecognized root field.");
        }

        JsonElement examples = root.GetProperty("examples");
        HashSet<string> actualSchemas = examples.EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        if (!actualSchemas.SetEquals(SchemaNames))
        {
            throw new InvalidDataException("Provider examples must cover exactly the nine WP1 schemas.");
        }

        foreach (string schema in SchemaNames)
        {
            ActiveJsonSchemaValidator.Validate(examples.GetProperty(schema), schema);
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

    private static void RequireString(JsonElement root, string property, string expected)
    {
        if (root.GetProperty(property).GetString() != expected)
        {
            throw new InvalidDataException($"Provider examples have invalid {property}.");
        }
    }
}
