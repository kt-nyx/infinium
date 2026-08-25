using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class EvaluationBoundaryContractTests
{
    private static readonly string[] CurrentSurfaceIds =
    [
        "current-product-contracts",
        "current-public-fixtures",
        "fixture-governance-vocabulary",
        "retired-material",
    ];

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    public void CurrentEvaluationAuthorityContainsOnlyActiveRepositorySurfaces()
    {
        using JsonDocument authority = ReadAndValidate(
            "docs/evaluation/repository-evaluation-authority.v1.json",
            "repository-evaluation-authority.v1.schema.json");

        JsonElement policy = authority.RootElement.GetProperty("semantic_oracle_policy");
        Assert.AreEqual("deferred", policy.GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, policy.GetProperty("current_authority_package").ValueKind);
        Assert.IsFalse(policy.GetProperty("gates_m1_acceptance").GetBoolean());
        Assert.IsFalse(policy.GetProperty("gates_m2_acceptance").GetBoolean());

        string[] surfaceIds = authority.RootElement.GetProperty("surfaces")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .ToArray();
        CollectionAssert.AreEquivalent(CurrentSurfaceIds, surfaceIds);

        JsonElement fixtures = authority.RootElement.GetProperty("surfaces")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "current-public-fixtures");
        CollectionAssert.Contains(
            fixtures.GetProperty("paths").EnumerateArray().Select(item => item.GetString()!).ToArray(),
            "fixtures/public/current-fixture-registry.v1.json");
        Assert.IsFalse(Directory.Exists(TestRepository.PathFromRoot("fixtures", "public", "provider")));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CurrentFixtureRegistryIndexesOnlyPresentCurrentPackages()
    {
        using JsonDocument registry = ReadAndValidate(
            "fixtures/public/current-fixture-registry.v1.json",
            "current-fixture-registry.v1.schema.json");
        JsonElement[] packages = registry.RootElement.GetProperty("packages").EnumerateArray().ToArray();
        Assert.AreEqual(registry.RootElement.GetProperty("package_count").GetInt32(), packages.Length);
        Assert.AreEqual(packages.Length, packages
            .Select(item => item.GetProperty("package_identity").GetString())
            .Distinct(StringComparer.Ordinal).Count());

        foreach (JsonElement package in packages)
        {
            string packagePath = package.GetProperty("package_path").GetString()!;
            string authorityPath = package.GetProperty("authority_file").GetString()!;
            StringAssert.StartsWith(packagePath, "fixtures/public/");
            StringAssert.StartsWith(authorityPath, packagePath + "/");
            Assert.IsFalse(packagePath.StartsWith("fixtures/public/provider/", StringComparison.Ordinal));

            string absolutePackage = TestRepository.PathFromRoot([.. packagePath.Split('/')]);
            string absoluteAuthority = TestRepository.PathFromRoot([.. authorityPath.Split('/')]);
            Assert.IsTrue(Directory.Exists(absolutePackage), packagePath);
            Assert.IsTrue(File.Exists(absoluteAuthority), authorityPath);
            byte[] bytes = File.ReadAllBytes(absoluteAuthority);
            Assert.AreEqual(bytes.LongLength, package.GetProperty("authority_bytes").GetInt64(), authorityPath);
            Assert.AreEqual(
                Convert.ToHexStringLower(SHA256.HashData(bytes)),
                package.GetProperty("authority_sha256").GetString(),
                authorityPath);
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    public void DefaultSolutionCannotReachArchivedEvaluatorOrLegacyProjects()
    {
        string solution = File.ReadAllText(TestRepository.PathFromRoot("Infinium.sln"));
        Assert.IsFalse(solution.Contains("Infinium.EvaluatorV2", StringComparison.Ordinal));
        Assert.IsFalse(solution.Contains("Infinium.Protocol4RegressionTests", StringComparison.Ordinal));
        Assert.IsFalse(solution.Contains("LegacyV1", StringComparison.Ordinal));

        string[] projectPaths = solution.Split('\n')
            .Where(line => line.StartsWith("Project(", StringComparison.Ordinal))
            .Select(line => line.Split(',')[1].Trim().Trim('"').Replace('\\', '/'))
            .Where(path => path.EndsWith(".csproj", StringComparison.Ordinal))
            .ToArray();
        foreach (string projectPath in projectPaths)
        {
            string project = File.ReadAllText(TestRepository.PathFromRoot([.. projectPath.Split('/')]));
            Assert.IsFalse(project.Contains("infinium-evaluator-archive", StringComparison.OrdinalIgnoreCase), projectPath);
            Assert.IsFalse(project.Contains("infinium-legacy-archive", StringComparison.OrdinalIgnoreCase), projectPath);
            Assert.IsFalse(project.Contains("infinium-development-history-archive", StringComparison.OrdinalIgnoreCase), projectPath);
        }
    }

    private static JsonDocument ReadAndValidate(string relativePath, string schemaName)
    {
        JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            TestRepository.PathFromRoot([.. relativePath.Split('/')])),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32,
            });
        ActiveJsonSchemaValidator.Validate(document.RootElement, schemaName);
        return document;
    }
}
