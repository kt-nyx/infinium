using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.ScopeReversion;
using Infinium.Application.ScopeReversion;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ScopeReversionContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Contracts")]
    [TestProperty("Category", "ScopeReversion")]
    public void ContractSchemaCodecAndDeclarationRoundTripCanonically()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionFixtureReader.Read(RepositoryRoot());
        ScopeReversionPipelineResult result = ScopeReversionComposition.Execute(fixture.Request);
        ScopeReversionAnalysisContract decoded = ScopeReversionJsonCodec.Deserialize(result.CanonicalJson);
        byte[] second = ScopeReversionJsonCodec.Serialize(decoded);

        CollectionAssert.AreEqual(result.CanonicalJson, second);
        Assert.AreEqual(result.Analysis.PayloadId, decoded.PayloadId);
        Assert.AreEqual(ScopeReversionAnalyzerDeclaration.AnalyzerFamily, decoded.Analyzer.AnalyzerFamily);
        Assert.AreEqual(ScopeReversionAnalyzerDeclaration.AnalyzerId, decoded.Analyzer.AnalyzerId);
        Assert.AreEqual(new ContractVersion(1, 0, 0), decoded.Analyzer.AnalyzerVersion);
        Assert.AreEqual(new ContractVersion(1, 0, 0), decoded.Analyzer.SemanticContractVersion);
        Assert.AreEqual(new ContractVersion(1, 0, 0), decoded.Analyzer.IdentityContractVersion);
        Assert.AreEqual(new ContractVersion(1, 0, 0), decoded.Analyzer.RulesetVersion);
        Assert.IsFalse(decoded.Analyzer.CanonicalDeclarationJson.Contains("M1-S7-SYNTHETIC", StringComparison.Ordinal));
        Assert.IsTrue(decoded.Boundaries.All(item => item.State == BoundaryUseState.NotUsed));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Contracts")]
    [TestProperty("Category", "ScopeReversion")]
    public void ContractRejectsUnknownFieldsMixedVersionsAndDanglingReferences()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionFixtureReader.Read(RepositoryRoot());
        ScopeReversionAnalysisContract analysis = ScopeReversionComposition.Execute(fixture.Request).Analysis;
        string json = Encoding.UTF8.GetString(ScopeReversionJsonCodec.Serialize(analysis));
        string unknown = json.ReplaceFirst("{", "{\"unknown\":true,");
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionJsonCodec.Deserialize(Encoding.UTF8.GetBytes(unknown)));
        string nestedUnknown = json.ReplaceFirst(
            "\"candidate_id\":",
            "\"unknown_nested\":true,\"candidate_id\":");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ScopeReversionJsonCodec.Deserialize(Encoding.UTF8.GetBytes(nestedUnknown)));
        string mixedVersion = json.Replace(
            "\"schema_version\": \"1.0.0\"",
            "\"schema_version\": \"2.0.0\"",
            StringComparison.Ordinal);
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionJsonCodec.Deserialize(Encoding.UTF8.GetBytes(mixedVersion)));
        ScopeReversionAnalysisContract dangling = analysis with
        {
            Candidates = [analysis.Candidates[0] with { DecisionId = new OpaqueId("missing-decision") }, .. analysis.Candidates.Skip(1)],
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionJsonCodec.Serialize(dangling));

        ScopeReversionAnalysisContract changedFinding = analysis with
        {
            Findings = [analysis.Findings[0] with { Conclusion = analysis.Findings[0].Conclusion + " Rechecked." }, .. analysis.Findings.Skip(1)],
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionJsonCodec.Serialize(changedFinding));
        changedFinding = changedFinding with
        {
            PayloadId = ScopeReversionContractInvariants.ComputePayloadId(changedFinding),
        };
        Assert.AreNotEqual(analysis.PayloadId, changedFinding.PayloadId);
        _ = ScopeReversionJsonCodec.Serialize(changedFinding);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Contracts")]
    [TestProperty("Category", "ScopeReversion")]
    public void DeveloperConformancePackageIsRegisteredWithExactBytesAndNoVerdictAuthority()
    {
        string root = RepositoryRoot();
        ScopeReversionFixturePackage fixture = ScopeReversionFixtureReader.Read(root);
        using JsonDocument registry = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(root, "fixtures", "public", "public-fixture-registry.v3.json")));
        JsonElement package = registry.RootElement.GetProperty("packages").EnumerateArray().Single(item =>
            item.GetProperty("package_identity").GetString() == "M1-S7-SYNTHETIC-v1");
        Assert.AreEqual("development", package.GetProperty("partition").GetString());
        Assert.AreEqual("developer-owned-product-conformance-not-semantic-oracle",
            package.GetProperty("authority_status").GetString());
        Assert.AreEqual("d3ce566345c1fd2845a38d292136407c64c2afa81bd405323b5d57c5ba86262f",
            fixture.InputSha256);
        Assert.AreEqual("f4630b927dd746facda0434cebfad8a938e549a18797a3a19c7128a4e60fac23",
            fixture.ExpectationsSha256);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Contracts")]
    [TestProperty("Category", "ScopeReversion")]
    public void FrozenSlice6ContractFamiliesHaveNoChangedBytesFromAcceptedBase()
    {
        string[] paths =
        [
            "contracts/json-schema/provider-access-profile.v1.schema.json",
            "contracts/json-schema/provider-operation.v1.schema.json",
            "contracts/json-schema/provider-response.v1.schema.json",
            "contracts/json-schema/source-claim-extraction.v1.schema.json",
            "contracts/json-schema/candidate-investigation.v1.schema.json",
            "contracts/json-schema/provider-execution-input.v1.schema.json",
            "contracts/json-schema/effective-scan-configuration.v2.schema.json",
            "contracts/json-schema/run-output.v2.schema.json",
            "contracts/json-schema/cli-summary.v2.schema.json",
        ];
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = RepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("diff");
        start.ArgumentList.Add("--name-only");
        start.ArgumentList.Add("29c421d38336295e5638be0e78728d98e5c11919");
        start.ArgumentList.Add("--");
        foreach (string path in paths)
        {
            start.ArgumentList.Add(path);
        }
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Git.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreEqual(0, process.ExitCode, error);
        Assert.AreEqual(string.Empty, output.Trim());
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the Infinium repository root.");
    }
}

file static class ScopeReversionStringExtensions
{
    public static string ReplaceFirst(this string value, string oldValue, string newValue)
    {
        int index = value.IndexOf(oldValue, StringComparison.Ordinal);
        return index < 0 ? value : string.Concat(value.AsSpan(0, index), newValue, value.AsSpan(index + oldValue.Length));
    }
}
