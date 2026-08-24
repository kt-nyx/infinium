using System.Diagnostics;
using System.Text.Json;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class HistoricalLiveSemanticPackageIntegrityContractTests
{
    [TestMethod]
    public void HistoricalPackagesRemainVisibleAndNonAuthorizing()
    {
        HistoricalLiveSemanticPackageReceipt receipt = HistoricalLiveSemanticPackageVerifier.Verify(TestRepository.Root);
        Assert.IsGreaterThan(0, receipt.SemanticAdmissionPackageCount);
        Assert.IsGreaterThanOrEqualTo(receipt.SemanticAdmissionPackageCount, receipt.HistoricalRegistryEntryCount);
        Assert.IsGreaterThan(receipt.HistoricalRegistryEntryCount, receipt.VerifiedFileBindingCount);
        StringAssert.Matches(receipt.RegistrySha256, new System.Text.RegularExpressions.Regex("^[0-9a-f]{64}$"));
    }

    [TestMethod]
    public void ReclassificationReopensRetainedManifestAndRejectsBoundHistoricalByteDrift()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), "infinium-historical-integrity-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyHistoricalIntegritySurface(temporaryRoot);
            _ = HistoricalLiveSemanticPackageVerifier.Verify(temporaryRoot);
            string oracle = Path.Combine(temporaryRoot, "fixtures", "public", "provider", "semantic-admission",
                "S6-SEMANTIC-ADMISSION-VAL-v1", "oracle.v1.json");
            File.AppendAllText(oracle, " ");

            InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(() =>
                HistoricalLiveSemanticPackageVerifier.Verify(temporaryRoot));
            StringAssert.Contains(error.Message,
                "fixtures/public/provider/semantic-admission/S6-SEMANTIC-ADMISSION-VAL-v1/oracle.v1.json");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public void RepositoryPolicyDefersSemanticQualificationWithoutRemovingEvaluationTests()
    {
        using JsonDocument authority = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
            "docs", "evaluation", "repository-evaluation-authority.v1.json")));
        JsonElement policy = authority.RootElement.GetProperty("semantic_oracle_policy");
        Assert.AreEqual("deferred", policy.GetProperty("status").GetString());
        Assert.AreEqual(JsonValueKind.Null, policy.GetProperty("current_authority_package").ValueKind);
        Assert.IsFalse(policy.GetProperty("gates_m1_acceptance").GetBoolean());
        Assert.IsFalse(policy.GetProperty("gates_m2_acceptance").GetBoolean());
        Assert.AreEqual("no-independent-semantic-verdict", policy.GetProperty("claim_boundary").GetString());

        string profile = File.ReadAllText(TestRepository.PathFromRoot(
            "docs", "evaluation", "m1-continuation-verification-profile.md"));
        StringAssert.Contains(profile, "TestCategory=Evaluation");
        StringAssert.Contains(profile, "oracle `PASS`");
        StringAssert.Contains(profile, "do **not** require");
    }

    [TestMethod]
    public void HistoricalIntegrityCommandIsCheckOnly()
    {
        Assert.AreEqual(0, Run("--check"));
        Assert.AreNotEqual(0, Run("--write"));
    }

    private static int Run(string argument)
    {
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = TestRepository.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "fixtures/tooling/reseal-live-semantic-v2.mjs", argument },
        })!;
        process.WaitForExit(30_000);
        return process.ExitCode;
    }

    private static void CopyHistoricalIntegritySurface(string targetRoot)
    {
        Copy("docs/evaluation/repository-evaluation-authority.v1.json");
        const string registryRelative = "fixtures/public/public-fixture-registry.v3.json";
        Copy(registryRelative);
        using JsonDocument registry = JsonDocument.Parse(File.ReadAllBytes(
            TestRepository.PathFromRoot(registryRelative.Split('/'))));
        foreach (JsonElement package in registry.RootElement.GetProperty("packages").EnumerateArray())
        {
            if (!package.TryGetProperty("authority_status", out JsonElement status)
                || !status.GetString()!.StartsWith("historical-", StringComparison.Ordinal))
            {
                continue;
            }

            string packageRoot = package.GetProperty("package_path").GetString()!;
            string authorityRelative = package.GetProperty("authority_file").GetString()!;
            Copy(authorityRelative);
            using JsonDocument authority = JsonDocument.Parse(File.ReadAllBytes(
                TestRepository.PathFromRoot(authorityRelative.Split('/'))));
            CopyManifestBindings(packageRoot, authority.RootElement);
            if (authority.RootElement.TryGetProperty("retained_manifest", out JsonElement retained))
            {
                string retainedRelative = packageRoot + "/" + retained.GetProperty("path").GetString();
                Copy(retainedRelative);
                using JsonDocument retainedManifest = JsonDocument.Parse(File.ReadAllBytes(
                    TestRepository.PathFromRoot(retainedRelative.Split('/'))));
                CopyManifestBindings(packageRoot, retainedManifest.RootElement);
            }
        }

        void CopyManifestBindings(string packageRoot, JsonElement manifest)
        {
            foreach (string property in new[] { "product_input", "predecessor_manifest", "oracle", "precomparison_manifest" })
            {
                if (manifest.TryGetProperty(property, out JsonElement binding)
                    && binding.ValueKind == JsonValueKind.Object
                    && binding.TryGetProperty("path", out JsonElement boundPath))
                {
                    string identityPath = boundPath.GetString()!;
                    Copy(identityPath.Contains('/') ? identityPath : packageRoot + "/" + identityPath);
                }
            }
            if (manifest.TryGetProperty("file_identities", out JsonElement identities))
            {
                foreach (JsonElement identity in identities.EnumerateArray())
                {
                    string identityPath = identity.GetProperty("path").GetString()!;
                    string relative = identityPath.Contains('/')
                        ? identityPath
                        : packageRoot + "/" + identityPath;
                    Copy(relative);
                    if (identity.TryGetProperty("role", out JsonElement role)
                        && role.GetString()!.Contains("reclassification", StringComparison.Ordinal))
                    {
                        using JsonDocument nested = JsonDocument.Parse(File.ReadAllBytes(
                            TestRepository.PathFromRoot(relative.Split('/'))));
                        CopyManifestBindings(packageRoot, nested.RootElement);
                    }
                }
            }
        }

        void Copy(string relative)
        {
            string source = TestRepository.PathFromRoot(relative.Split('/'));
            string target = Path.Combine(targetRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }
}
