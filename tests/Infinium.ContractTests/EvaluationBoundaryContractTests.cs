using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class EvaluationBoundaryContractTests
{
    private static readonly string[] CurrentPublicFixtureFamilies =
    [
        "test-data/public-fixtures/platform/analysis-runtime-substrate",
        "test-data/public-fixtures/bethesda/BETH-NPC-DEV",
        "test-data/public-fixtures/bethesda/BETH-REFR-DEV",
        "test-data/public-fixtures/bethesda/BETH-LIGHT-VAL",
        "test-data/public-fixtures/bethesda/BETH-MALFORMED-VAL",
        "test-data/public-fixtures/bethesda/BETH-UNSUPPORTED-VAL",
        "test-data/public-fixtures/documentation/DOC-CLAIM-CORE-DEV",
        "test-data/public-fixtures/documentation/DOC-CLAIM-ADVERSARIAL-VAL",
        "test-data/public-fixtures/candidates/CAND-SEMANTIC-DEV-v1",
        "test-data/public-fixtures/candidates/CAND-SCALE-VAL-v1",
        "test-data/public-fixtures/candidates/CAND-STRESS-DEV-v1",
        "test-data/public-fixtures/findings-cases",
        "test-data/public-fixtures/operations/analysis-lifecycle",
        "test-data/public-fixtures/cross-stage/analysis-pipeline",
    ];

    private static readonly string[] CurrentAuthoritySurfaceIds =
    [
        "current-product-contracts",
        "current-public-fixtures",
        "historical-protocol-4-bounded-regression",
        "retained-historical-authorability-evidence",
        "fixture-governance-vocabulary",
        "retired-material",
    ];

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void RepositoryAuthorityAndRetirementManifestsAreClosedAndActionable()
    {
        using JsonDocument authority = ReadAndValidate(
            "docs/evaluation/repository-evaluation-authority.v1.json",
            "repository-evaluation-authority.v1.schema.json");
        using JsonDocument retirement = ReadAndValidate(
            "docs/evaluation/retired-evaluation-assets.v1.json",
            "retired-evaluation-assets.v1.schema.json");

        string[] surfaceIds = authority.RootElement.GetProperty("surfaces")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetString()!)
            .ToArray();
        CollectionAssert.AreEquivalent(
            CurrentAuthoritySurfaceIds,
            surfaceIds);

        foreach (JsonElement entry in retirement.RootElement.GetProperty("entries").EnumerateArray())
        {
            string retiredPath = entry.GetProperty("path").GetString()!;
            Assert.IsFalse(File.Exists(TestRepository.PathFromRoot([.. retiredPath.Split('/')])), retiredPath);
            JsonElement replacement = entry.GetProperty("replacement");
            if (replacement.ValueKind == JsonValueKind.String)
            {
                string replacementPath = replacement.GetString()!;
                Assert.IsTrue(File.Exists(TestRepository.PathFromRoot([.. replacementPath.Split('/')])), replacementPath);
            }
        }

        foreach (string path in CurrentPublicFixtureFamilies)
        {
            Assert.IsTrue(Directory.Exists(TestRepository.PathFromRoot([.. path.Split('/')])), path);
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void DefaultSolutionGraphCannotReachHistoricalEvaluatorOrRetiredCompatibility()
    {
        string solution = File.ReadAllText(TestRepository.PathFromRoot("Infinium.sln"));
        Assert.IsFalse(solution.Contains("Infinium.EvaluatorV2", StringComparison.Ordinal));
        StringAssert.Contains(solution, "Infinium.PublicFixtures", StringComparison.Ordinal);
        Assert.IsFalse(solution.Contains("Infinium.Protocol4RegressionTests", StringComparison.Ordinal));

        string[] projectPaths = solution.Split('\n')
            .Where(line => line.StartsWith("Project(", StringComparison.Ordinal))
            .Select(line => line.Split(',')[1].Trim().Trim('"').Replace('\\', '/'))
            .Where(path => path.EndsWith(".csproj", StringComparison.Ordinal))
            .ToArray();
        foreach (string projectPath in projectPaths)
        {
            string project = File.ReadAllText(TestRepository.PathFromRoot([.. projectPath.Split('/')]));
            Assert.IsFalse(project.Contains("Infinium.EvaluatorV2", StringComparison.Ordinal), projectPath);
            Assert.IsFalse(project.Contains("LegacyV1", StringComparison.Ordinal), projectPath);
        }

        string applicationProject = File.ReadAllText(TestRepository.PathFromRoot(
            "src", "Infinium.Application", "Infinium.Application.csproj"));
        Assert.IsFalse(
            applicationProject.Contains("contracts\\repository", StringComparison.OrdinalIgnoreCase),
            "Repository-governance schemas must not ship in the product Application assembly.");

        string publicFixtureRoot = TestRepository.PathFromRoot("tools", "fixtures", "Infinium.PublicFixtures");
        string publicFixtureSource = string.Join('\n', Directory.EnumerateFiles(publicFixtureRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
        Assert.IsFalse(publicFixtureSource.Contains("EmbeddedJsonSchemaValidator", StringComparison.Ordinal));
        Assert.IsFalse(publicFixtureSource.Contains("Infinium.EvaluatorV2", StringComparison.Ordinal));

        string protocolProject = File.ReadAllText(TestRepository.PathFromRoot(
            "tests", "Infinium.Protocol4RegressionTests", "Infinium.Protocol4RegressionTests.csproj"));
        StringAssert.Contains(protocolProject, "Infinium.EvaluatorV2", StringComparison.Ordinal);
        StringAssert.Contains(protocolProject, "EvaluatorV2PublicProtocolTests.cs", StringComparison.Ordinal);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProductCapabilityBoundaryRejectsEvaluatorGovernanceIdentities()
    {
        ExecutionBoundaryContract[] valid =
        [
            new("provider", BoundaryUseState.NotUsed, "disabled"),
            new("hosted-search", BoundaryUseState.NotUsed, "disabled"),
            new("nexus", BoundaryUseState.NotUsed, "disabled"),
            new("loot", BoundaryUseState.NotUsed, "disabled"),
        ];
        ExecutionBoundaryContractInvariants.ValidateProductCapabilities(valid, requireNotUsed: true);

        ExecutionBoundaryContract[] evaluatorBearing =
        [
            .. valid[..^1],
            new("private-evaluator", BoundaryUseState.NotUsed, "prohibited"),
        ];
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ExecutionBoundaryContractInvariants.ValidateProductCapabilities(evaluatorBearing, requireNotUsed: true));
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
