using System.Security.Cryptography;
using System.Text.Json;

namespace Infinium.Tests;

[TestClass]
public sealed class SemanticOracleDeferralBoundaryTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    public void SemanticAdmissionFamilyIsDynamicallyHistoricalAndHasNoAuthority()
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
            root, "fixtures", "public", "public-fixture-registry.v3.json")));
        JsonElement family = registry.RootElement.GetProperty("family_classifications")
            .EnumerateArray().Single(value => value.GetProperty("family_id").GetString() == "semantic-admission");
        Assert.AreEqual("historical-non-authorizing", family.GetProperty("disposition").GetString());
        Assert.AreEqual(JsonValueKind.Null, family.GetProperty("current_validation_authority_package").ValueKind);

        JsonElement[] rows = registry.RootElement.GetProperty("packages").EnumerateArray()
            .Where(value => value.GetProperty("package_path").GetString()!
                .StartsWith("fixtures/public/provider/semantic-admission/", StringComparison.Ordinal))
            .ToArray();
        Assert.IsGreaterThan(0, rows.Length);
        foreach (JsonElement row in rows)
        {
            Assert.AreEqual("development", row.GetProperty("partition").GetString());
            StringAssert.StartsWith(row.GetProperty("authority_status").GetString()!, "historical-");
            string relative = row.GetProperty("authority_file").GetString()!;
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            Assert.AreEqual(bytes.LongLength, row.GetProperty("authority_bytes").GetInt64());
            Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(bytes)), row.GetProperty("authority_sha256").GetString());
        }
        Assert.IsFalse(Directory.Exists(Path.Combine(root, "fixtures", "public", "provider", "semantic-admission", "S6-SEMANTIC-ADMISSION-VAL-v14")));
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
