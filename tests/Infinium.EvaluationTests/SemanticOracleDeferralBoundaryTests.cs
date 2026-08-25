using System.Text.Json;

namespace Infinium.Tests;

[TestClass]
public sealed class SemanticOracleDeferralBoundaryTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    public void SemanticOracleAuthorityIsDeferredAndHistoricalProviderFixturesAreAbsent()
    {
        string root = RepositoryRoot();
        using JsonDocument authority = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            root, "docs", "evaluation", "repository-evaluation-authority.v1.json")));
        JsonElement policy = authority.RootElement.GetProperty("semantic_oracle_policy");
        Assert.AreEqual("deferred", policy.GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, policy.GetProperty("current_authority_package").ValueKind);
        Assert.IsFalse(policy.GetProperty("gates_m1_acceptance").GetBoolean());
        Assert.IsFalse(policy.GetProperty("gates_m2_acceptance").GetBoolean());

        using JsonDocument registry = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            root, "fixtures", "public", "current-fixture-registry.v1.json")));
        Assert.IsFalse(registry.RootElement.GetProperty("packages").EnumerateArray().Any(
            item => item.GetProperty("package_path").GetString()!
                .StartsWith("fixtures/public/provider/", StringComparison.Ordinal)));
        Assert.IsFalse(Directory.Exists(Path.Combine(root, "fixtures", "public", "provider")));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException();
    }
}
