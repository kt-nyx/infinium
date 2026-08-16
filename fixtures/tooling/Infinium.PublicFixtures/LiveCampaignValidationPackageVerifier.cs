using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Provider;

namespace Infinium.PublicFixtures;

public sealed record LiveCampaignValidationReceipt(
    string PackageId,
    string ProductInputSha256,
    string PredecessorManifestSha256,
    string OracleSha256,
    int ScenarioCount,
    string DeterministicResultSha256);

/// <summary>
/// Executes the two frozen public semantic packages without network or credentials. The live
/// campaign wrapper is authority only for exact input/predecessor/oracle byte identities; product
/// behavior is evaluated by the existing typed fixture reader, engine, and frozen oracle verifier.
/// </summary>
public static class LiveCampaignValidationPackageVerifier
{
    public static LiveCampaignValidationReceipt VerifySourceClaim(string repositoryRoot) =>
        VerifySourceClaimCore(Path.GetFullPath(repositoryRoot));

    public static LiveCampaignValidationReceipt VerifyCandidateInvestigation(string repositoryRoot) =>
        VerifyCandidateCore(Path.GetFullPath(repositoryRoot));

    private static LiveCampaignValidationReceipt VerifySourceClaimCore(string root)
    {
        const string package = "LLM-CLAIM-LIVE-VAL";
        (string productPath, string predecessorPath, string oraclePath) = ValidateWrapper(root, package,
            "SourceClaimExtraction", 1767,
            "77cffbebbc940357e1f8b39a9fd054c50e6c1a25c9e24c7b564f37867b95469c",
            1320, "0f95265340873dc4abb083c6f857db9e8786c6e1ba36da385f07c876afe1c13f",
            "d917aed55912b0d6c82f8d19c772c6c504b9edcd3b1d3dcf9082da0f7a52e9eb");
        SourceClaimFixturePackage fixture = SourceClaimFixtureReader.Read(Path.GetDirectoryName(productPath)!);
        SourceClaimAcquisitionResult actual = SourceClaimAcquisitionEngine.Execute(
            fixture.ExecutionInput, fixture.Transcripts);
        SourceClaimOracleVerifier.Verify(fixture, actual);
        return new(package, Sha(productPath), Sha(predecessorPath), Sha(oraclePath),
            actual.Scenarios.Count, ResultSha(actual));
    }

    private static LiveCampaignValidationReceipt VerifyCandidateCore(string root)
    {
        const string package = "LLM-INVESTIGATE-LIVE-VAL";
        (string productPath, string predecessorPath, string oraclePath) = ValidateWrapper(root, package,
            "CandidateInvestigation", 12403,
            "99029f0834e03e72bbba69ad4991a7ca22c441ce4888cfcfac31e7ca7e74fbe7",
            2035, "b42dff12144f192c1e7a913a3a99433398f0f2d41148a3353f7aa9cf89154323",
            "3f6db5e3618d8d0b5d35f2e79c203ef5bcd1bac8166e5cae417e7b5ac2e3348a");
        using CandidateInvestigationFixturePackage fixture =
            CandidateInvestigationFixtureReader.Read(Path.GetDirectoryName(productPath)!);
        CandidateInvestigationResult actual = CandidateInvestigationEngine.Execute(
            fixture.ExecutionInput, fixture.Transcripts);
        CandidateInvestigationOracleVerifier.Verify(fixture, actual);
        return new(package, Sha(productPath), Sha(predecessorPath), Sha(oraclePath),
            actual.Scenarios.Count, ResultSha(actual));
    }

    private static (string Product, string Predecessor, string Oracle) ValidateWrapper(
        string root, string package, string operation, long productBytes, string productSha,
        long predecessorBytes, string predecessorSha, string oracleSha)
    {
        string wrapperPath = Path.Combine(root, "fixtures", "public", "provider", "live-campaign",
            package, "public-manifest.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(wrapperPath));
        JsonElement wrapper = document.RootElement;
        string[] names = ["schema_identity", "fixture_id", "fixture_version", "partition", "status",
            "operation", "product_input", "predecessor_package", "oracle", "answer_free_product_input",
            "network_required_for_oracle", "semantic_use"];
        if (!wrapper.EnumerateObject().Select(property => property.Name).SequenceEqual(names,
                StringComparer.Ordinal)
            || wrapper.GetProperty("schema_identity").GetString()
                != "infinium.public-fixture.live-provider-semantic/1.0.0"
            || wrapper.GetProperty("fixture_id").GetString() != package
            || wrapper.GetProperty("fixture_version").GetString() != "1.0.0"
            || wrapper.GetProperty("partition").GetString() != "validation"
            || wrapper.GetProperty("status").GetString() != "oracle-frozen-pre-live-comparison"
            || wrapper.GetProperty("operation").GetString() != operation
            || !wrapper.GetProperty("answer_free_product_input").GetBoolean()
            || wrapper.GetProperty("network_required_for_oracle").GetBoolean()
            || !wrapper.GetProperty("semantic_use").GetBoolean())
        {
            throw new InvalidDataException("The live semantic wrapper is not exact, answer-free, and offline.");
        }
        JsonElement product = wrapper.GetProperty("product_input");
        JsonElement predecessor = wrapper.GetProperty("predecessor_package");
        JsonElement oracle = wrapper.GetProperty("oracle");
        Exact(product, ["path", "bytes", "sha256"]);
        Exact(predecessor, ["path", "bytes", "sha256"]);
        Exact(oracle, ["path", "authoring", "product_visible"]);
        string productPath = Canonical(root, product.GetProperty("path").GetString()!);
        string predecessorPath = Canonical(root, predecessor.GetProperty("path").GetString()!);
        string oraclePath = Path.Combine(Path.GetDirectoryName(wrapperPath)!, oracle.GetProperty("path").GetString()!);
        if (product.GetProperty("bytes").GetInt64() != productBytes
            || product.GetProperty("sha256").GetString() != productSha
            || predecessor.GetProperty("bytes").GetInt64() != predecessorBytes
            || predecessor.GetProperty("sha256").GetString() != predecessorSha
            || oracle.GetProperty("authoring").GetString() != "independent-pre-response"
            || oracle.GetProperty("product_visible").GetBoolean()
            || new FileInfo(productPath).Length != productBytes || Sha(productPath) != productSha
            || new FileInfo(predecessorPath).Length != predecessorBytes || Sha(predecessorPath) != predecessorSha
            || Sha(oraclePath) != oracleSha)
        {
            throw new InvalidDataException("The live semantic wrapper has stale product, predecessor, or oracle bytes.");
        }
        return (productPath, predecessorPath, oraclePath);
    }

    private static string Canonical(string root, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The live semantic wrapper path escaped the repository root.");
        }
        return path;
    }

    private static void Exact(JsonElement value, string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.EnumerateObject().Select(property => property.Name).SequenceEqual(names,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException("A live semantic wrapper object is not recursively closed.");
        }
    }

    private static string ResultSha<T>(T result) => Convert.ToHexStringLower(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(result, SourceClaimContextMinimizer.JsonOptions)));

    private static string Sha(string path) =>
        Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}
