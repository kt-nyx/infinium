using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class EvaluationBoundaryContractTests
{
    private static readonly IReadOnlyDictionary<string, string> R1V2PackageAuthorities =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["S6-CLAIM-LIVE-VAL-v2"] = "fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/public-manifest.json",
            ["LLM-CLAIM-LIVE-VAL-v2"] = "fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL-v2/public-manifest.json",
            ["S6-CANDIDATE-LIVE-VAL-v2"] = "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/public-manifest.json",
            ["LLM-INVESTIGATE-LIVE-VAL-v2"] = "fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL-v2/public-manifest.json",
            ["PROV-LIVE-COMPOSED-VAL-v2"] = "fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL-v2/public-manifest.json",
        };
    private static readonly string[] CurrentPublicFixtureFamilies =
    [
        "fixtures/public/platform/analysis-runtime-substrate",
        "fixtures/public/bethesda/BETH-NPC-DEV",
        "fixtures/public/bethesda/BETH-REFR-DEV",
        "fixtures/public/bethesda/BETH-LIGHT-VAL",
        "fixtures/public/bethesda/BETH-MALFORMED-VAL",
        "fixtures/public/bethesda/BETH-UNSUPPORTED-VAL",
        "fixtures/public/documentation/DOC-CLAIM-CORE-DEV",
        "fixtures/public/documentation/DOC-CLAIM-ADVERSARIAL-VAL",
        "fixtures/public/candidates/CAND-SEMANTIC-DEV-v1",
        "fixtures/public/candidates/CAND-SCALE-VAL-v1",
        "fixtures/public/candidates/CAND-STRESS-DEV-v1",
        "fixtures/public/findings-cases",
        "fixtures/public/operations/analysis-lifecycle",
        "fixtures/public/cross-stage/analysis-pipeline",
        "fixtures/public/contracts/provider-wp1",
    ];

    private static readonly string[] CurrentAuthoritySurfaceIds =
    [
        "current-product-contracts",
        "current-public-fixtures",
        "retained-historical-authorability-evidence",
        "fixture-governance-vocabulary",
        "retired-material",
    ];

    private static readonly string[] CurrentPublicFixtureAuthorityPaths =
    [
        "fixtures/tooling/",
        "fixtures/public/public-fixture-registry.v1.json",
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

        JsonElement publicFixtureSurface = authority.RootElement.GetProperty("surfaces")
            .EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "current-public-fixtures");
        CollectionAssert.AreEquivalent(
            CurrentPublicFixtureAuthorityPaths,
            publicFixtureSurface.GetProperty("paths").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray());

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
    [TestProperty("Category", "Contract")]
    public void ProviderPublicFixtureRegistryExactlyIndexesEveryCurrentFunctionalPackage()
    {
        using JsonDocument registry = ReadAndValidate(
            "fixtures/public/public-fixture-registry.v1.json",
            "public-fixture-registry.v1.schema.json");
        JsonElement[] entries = registry.RootElement.GetProperty("packages")
            .EnumerateArray()
            .ToArray();
        Assert.AreEqual(registry.RootElement.GetProperty("package_count").GetInt32(), entries.Length);

        Dictionary<string, FixtureRegistryEntry> actual = entries.ToDictionary(
            item => item.GetProperty("package_identity").GetString()!,
            item => new FixtureRegistryEntry(
                item.GetProperty("package_version").GetString()!,
                item.GetProperty("partition").GetString()!,
                item.GetProperty("package_path").GetString()!,
                item.GetProperty("authority_file").GetString()!,
                item.GetProperty("authority_bytes").GetInt64(),
                item.GetProperty("authority_sha256").GetString()!,
                item.TryGetProperty("authority_status", out JsonElement status)
                    ? status.GetString()
                    : null),
            StringComparer.Ordinal);
        Assert.AreEqual(entries.Length, actual.Count, "Package identities must be unique.");

        Dictionary<string, FixtureSourceIdentity> expected = DiscoverCurrentFixturePackages();
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), actual.Keys.ToArray());
        foreach ((string identity, FixtureSourceIdentity source) in expected)
        {
            FixtureRegistryEntry registered = actual[identity];
            Assert.AreEqual(source.Version, registered.Version, identity);
            Assert.AreEqual(source.Partition, registered.Partition, identity);
            Assert.AreEqual(source.PackagePath, registered.PackagePath, identity);
            Assert.AreEqual(source.AuthorityFile, registered.AuthorityFile, identity);
            if (source.AuthorityStatus is not null)
            {
                Assert.AreEqual(source.AuthorityStatus, registered.AuthorityStatus, identity);
            }
            else if (registered.AuthorityStatus is not null)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(registered.AuthorityStatus), identity);
            }

            string authorityPath = TestRepository.PathFromRoot([.. registered.AuthorityFile.Split('/')]);
            byte[] bytes = File.ReadAllBytes(authorityPath);
            Assert.AreEqual(bytes.LongLength, registered.AuthorityBytes, identity);
            Assert.AreEqual(
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                registered.AuthoritySha256,
                identity);
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderPublicFixtureRegistryV2PreservesV1AndIndexesOnlyExactR1Successors()
    {
        using JsonDocument v1 = ReadAndValidate(
            "fixtures/public/public-fixture-registry.v1.json", "public-fixture-registry.v1.schema.json");
        using JsonDocument v2 = ReadAndValidate(
            "fixtures/public/public-fixture-registry.v2.json", "public-fixture-registry.v2.schema.json");
        JsonElement[] retained = v1.RootElement.GetProperty("packages").EnumerateArray().ToArray();
        JsonElement[] rows = v2.RootElement.GetProperty("packages").EnumerateArray().ToArray();
        Assert.AreEqual(38, retained.Length);
        Assert.AreEqual(43, rows.Length);
        for (int index = 0; index < retained.Length; index++)
        {
            Assert.IsTrue(JsonElement.DeepEquals(retained[index], rows[index]), $"Registry v1 row {index} drifted in v2.");
        }
        CollectionAssert.AreEqual(R1V2PackageAuthorities.Keys.ToArray(),
            rows.Skip(38).Select(x => x.GetProperty("package_identity").GetString()).ToArray());
        foreach (JsonElement row in rows.Skip(38))
        {
            string authority = row.GetProperty("authority_file").GetString()!;
            byte[] bytes = File.ReadAllBytes(TestRepository.PathFromRoot([.. authority.Split('/')]));
            using JsonDocument manifest = JsonDocument.Parse(bytes);
            string packagePath = authority[..authority.LastIndexOf('/')];
            Assert.AreEqual(manifest.RootElement.GetProperty("package_identity").GetString(),
                row.GetProperty("package_identity").GetString(), authority);
            Assert.AreEqual(manifest.RootElement.GetProperty("package_version").GetString(),
                row.GetProperty("package_version").GetString(), authority);
            Assert.AreEqual(manifest.RootElement.GetProperty("partition").GetString(),
                row.GetProperty("partition").GetString(), authority);
            Assert.AreEqual(packagePath, row.GetProperty("package_path").GetString(), authority);
            Assert.AreEqual(bytes.LongLength, row.GetProperty("authority_bytes").GetInt64(), authority);
            Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(bytes)),
                row.GetProperty("authority_sha256").GetString(), authority);
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void R1V2ManifestExclusionRequiresExactIdentityAndAuthorityPathPair()
    {
        foreach ((string identity, string authorityPath) in R1V2PackageAuthorities)
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(
                TestRepository.PathFromRoot([.. authorityPath.Split('/')])));
            Assert.IsTrue(IsExpectedR1V2Manifest(manifest.RootElement, authorityPath));
            bool rejected = false;
            try
            {
                IsExpectedR1V2Manifest(manifest.RootElement,
                    "fixtures/public/provider/misplaced/" + identity + "/public-manifest.json");
            }
            catch (AssertFailedException)
            {
                rejected = true;
            }
            Assert.IsTrue(rejected, $"Misplaced duplicate R1 v2 identity '{identity}' was not rejected.");
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

        string publicFixtureRoot = TestRepository.PathFromRoot("fixtures", "tooling", "Infinium.PublicFixtures");
        string publicFixtureSource = string.Join('\n', Directory.EnumerateFiles(publicFixtureRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText));
        Assert.IsFalse(publicFixtureSource.Contains("EmbeddedJsonSchemaValidator", StringComparison.Ordinal));
        Assert.IsFalse(publicFixtureSource.Contains("Infinium.EvaluatorV2", StringComparison.Ordinal));

        Assert.IsFalse(Directory.Exists(TestRepository.PathFromRoot("tools")));
        Assert.IsFalse(Directory.Exists(TestRepository.PathFromRoot(
            "tests", "Infinium.Protocol4RegressionTests")));
        Assert.IsFalse(File.Exists(TestRepository.PathFromRoot(
            "tests", "Infinium.EvaluationTests", "EvaluatorV2PublicProtocolTests.cs")));
        Assert.IsFalse(File.Exists(TestRepository.PathFromRoot(
            "src", "Infinium.Application", "Evaluation", "EmbeddedJsonSchemaValidator.cs")));
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

    private static Dictionary<string, FixtureSourceIdentity> DiscoverCurrentFixturePackages()
    {
        Dictionary<string, FixtureSourceIdentity> packages = new(StringComparer.Ordinal);
        string publicRoot = TestRepository.PathFromRoot("fixtures", "public");
        HashSet<string> replacedRegistryAuthorities = [];
        foreach (string provenancePath in Directory.EnumerateFiles(
                     publicRoot,
                     "oracle-provenance.v1.json",
                     SearchOption.AllDirectories))
        {
            using JsonDocument provenance = JsonDocument.Parse(File.ReadAllBytes(provenancePath));
            if (provenance.RootElement.TryGetProperty("replaces_registry_authority_for", out JsonElement replaced))
            {
                Assert.IsTrue(provenance.RootElement.GetProperty("replaced_package_bytes_modified").ValueKind
                    == JsonValueKind.False, provenancePath);
                replacedRegistryAuthorities.Add(replaced.GetString()!);
            }
        }
        foreach (string manifestPath in Directory.EnumerateFiles(
                     publicRoot,
                     "public-manifest.json",
                     SearchOption.AllDirectories))
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
            string manifestAuthority = RepositoryRelativePath(manifestPath);
            if (IsExpectedR1V2Manifest(manifest.RootElement, manifestAuthority))
            {
                continue;
            }
            if (replacedRegistryAuthorities.Contains(manifest.RootElement.GetProperty("fixture_id").GetString()!))
            {
                continue;
            }
            string packagePath = RepositoryRelativePath(Path.GetDirectoryName(manifestPath)!);
            packages.Add(
                manifest.RootElement.GetProperty("fixture_id").GetString()!,
                new FixtureSourceIdentity(
                    manifest.RootElement.GetProperty("fixture_version").GetString()!,
                    manifest.RootElement.GetProperty("partition").GetString()!,
                    packagePath,
                    packagePath + "/public-manifest.json"));
        }

        const string findingAuthority = "fixtures/public/findings-cases/finding-case-independent-truth.v1.0.3.json";
        using (JsonDocument findings = JsonDocument.Parse(File.ReadAllBytes(
                   TestRepository.PathFromRoot([.. findingAuthority.Split('/')]))))
        {
            foreach (JsonElement package in findings.RootElement.GetProperty("package_registry").EnumerateArray())
            {
                packages.Add(
                    package.GetProperty("package_id").GetString()!,
                    new FixtureSourceIdentity(
                        package.GetProperty("package_version").GetString()!,
                        package.GetProperty("partition").GetString()!,
                        "fixtures/public/findings-cases",
                        findingAuthority));
            }
        }

        const string operationsAuthority = "fixtures/public/operations/analysis-lifecycle/fixture-manifest.v1.json";
        using (JsonDocument operations = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
                   "fixtures", "public", "operations", "analysis-lifecycle", "harness-envelope.v1.json"))))
        {
            foreach (JsonElement package in operations.RootElement.GetProperty("packages").EnumerateArray())
            {
                packages.Add(
                    package.GetProperty("package_identity").GetString()!,
                    new FixtureSourceIdentity(
                        package.GetProperty("version").GetString()!,
                        package.GetProperty("partition").GetString()!,
                        "fixtures/public/operations/analysis-lifecycle",
                        operationsAuthority));
            }
        }

        const string crossStageAuthority = "fixtures/public/cross-stage/analysis-pipeline/fixture-manifest.v1.json";
        using (JsonDocument crossStage = JsonDocument.Parse(File.ReadAllBytes(
                   TestRepository.PathFromRoot([.. crossStageAuthority.Split('/')]))))
        {
            JsonElement root = crossStage.RootElement;
            packages.Add(
                root.GetProperty("package_identity").GetString()!,
                new FixtureSourceIdentity(
                    root.GetProperty("package_version").GetString()!,
                    root.GetProperty("partition").GetString()!,
                    "fixtures/public/cross-stage/analysis-pipeline",
                    crossStageAuthority,
                    root.GetProperty("status").GetString()));
        }

        const string providerAuthority = "fixtures/public/contracts/provider-wp1/contract-examples.v1.json";
        using (JsonDocument provider = JsonDocument.Parse(File.ReadAllBytes(
                   TestRepository.PathFromRoot([.. providerAuthority.Split('/')]))))
        {
            JsonElement root = provider.RootElement;
            packages.Add(
                root.GetProperty("package_identity").GetString()!,
                new FixtureSourceIdentity(
                    root.GetProperty("package_version").GetString()!,
                    root.GetProperty("partition").GetString()!,
                    "fixtures/public/contracts/provider-wp1",
                    providerAuthority,
                    root.GetProperty("status").GetString()));
        }

        return packages;
    }

    private static bool IsExpectedR1V2Manifest(JsonElement manifest, string authorityPath)
    {
        if (!manifest.TryGetProperty("package_identity", out JsonElement identityElement)
            || identityElement.ValueKind != JsonValueKind.String
            || !R1V2PackageAuthorities.TryGetValue(identityElement.GetString()!, out string? expectedAuthority))
        {
            return false;
        }

        Assert.AreEqual(expectedAuthority, authorityPath,
            $"R1 v2 identity '{identityElement.GetString()}' is duplicated or misplaced.");
        return true;
    }

    private static string RepositoryRelativePath(string fullPath) =>
        Path.GetRelativePath(TestRepository.Root, fullPath).Replace('\\', '/');

    private sealed record FixtureSourceIdentity(
        string Version,
        string Partition,
        string PackagePath,
        string AuthorityFile,
        string? AuthorityStatus = null);

    private sealed record FixtureRegistryEntry(
        string Version,
        string Partition,
        string PackagePath,
        string AuthorityFile,
        long AuthorityBytes,
        string AuthoritySha256,
        string? AuthorityStatus);
}
