using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Provider;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class SourceClaimFixtureIntegrityTests
{
    [TestMethod]
    [TestCategory("Contract")]
    public void SourceClaimPackageRejectsTransitiveByteContextManifestAndProvenanceMutations()
    {
        using TemporaryPackage package = TemporaryPackage.Copy("S6-CLAIM-DEV-v1");
        File.AppendAllText(package.Path("execution-input.v1.json"), " ");
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimFixtureReader.ReadForContractTest(package.Root));

        package.Reset();
        File.AppendAllText(package.Path("context-manifest.v1.json"), " ");
        package.Reseal("context-manifest.v1.json");
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimFixtureReader.ReadForContractTest(package.Root));

        package.Reset();
        JsonObject provenance = package.ReadObject("oracle-provenance.v1.json");
        provenance["product_output_used"] = true;
        package.WriteObject("oracle-provenance.v1.json", provenance);
        package.Reseal("oracle-provenance.v1.json");
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimFixtureReader.ReadForContractTest(package.Root));

        package.Reset();
        JsonObject manifest = package.ReadObject("public-manifest.json");
        manifest["oracle_file"] = "retained-transcripts.v1.json";
        package.WriteObject("public-manifest.json", manifest);
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimFixtureReader.ReadForContractTest(package.Root));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void SourceClaimAnswerIsolationIsRecursiveButTreatsPassageTextAsInert()
    {
        using JsonDocument inert = JsonDocument.Parse(
            """{"passages":[{"text":"An inert passage may literally mention oracle and expected_answer."}],"safe":"data"}""");
        SourceClaimFixtureReader.AssertAnswerFreeForContractTest(inert.RootElement);

        using JsonDocument hostileKey = JsonDocument.Parse("""{"nested":{"expected_answer":"x"}}""");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            SourceClaimFixtureReader.AssertAnswerFreeForContractTest(hostileKey.RootElement));
        using JsonDocument hostileValue = JsonDocument.Parse("""{"nested":{"value":"oracle authority"}}""");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            SourceClaimFixtureReader.AssertAnswerFreeForContractTest(hostileValue.RootElement));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void SourceClaimOracleVerifierRejectsEveryExpectationFamilyMutation()
    {
        SourceClaimFixturePackage package = SourceClaimFixtureReader.Read(PackageRoot("S6-CLAIM-DEV-v1"));
        SourceClaimAcquisitionResult actual = SourceClaimAcquisitionEngine.Execute(package.ExecutionInput, package.Transcripts);
        SourceClaimOracleVerifier.Verify(package, actual);

        SourceClaimScenarioOracle first = package.Oracle.Scenarios[0];
        SourceClaimExpectedProposal proposal = first.ExpectedProposals[0];
        SourceClaimFixturePackage[] mutations =
        [
            package with { Oracle = package.Oracle with
                { ExpectedIdentity = package.Oracle.ExpectedIdentity with { OwnerId = "wrong-owner" } } },
            package with { Oracle = package.Oracle with { Scenarios = ReplaceFirst(first with
                { ExpectedResult = "wrong-result" }, package.Oracle.Scenarios) } },
            package with { Oracle = package.Oracle with { Scenarios = ReplaceFirst(first with
                { ExpectedResponseRecordId = "wrong-response" }, package.Oracle.Scenarios) } },
            package with { Oracle = package.Oracle with { Scenarios = ReplaceFirst(first with
                { ExpectedProposals = [proposal with { Claim = "mutated claim" }] }, package.Oracle.Scenarios) } },
            package with { Oracle = package.Oracle with { Scenarios = ReplaceFirst(first with
                { ReplayState = "audit-only" }, package.Oracle.Scenarios) } },
            package with { Oracle = package.Oracle with { Scenarios = ReplaceFirst(first with
                { ProviderUsed = false }, package.Oracle.Scenarios) } },
            package with { Oracle = package.Oracle with { Scenarios = ReplaceFirst(first with
                { AuditOnly = true }, package.Oracle.Scenarios) } },
            package with { Oracle = package.Oracle with { Scenarios = ReplaceFirst(first with
                { ExpectedGapCount = 1 }, package.Oracle.Scenarios) } },
            package with { Oracle = package.Oracle with { Scenarios = ReplaceFirst(first with
                { ContradictionEvidenceIds = ["wrong-contradiction"] }, package.Oracle.Scenarios) } },
            package with { Oracle = package.Oracle with { Scenarios = ReplaceFirst(first with
                { ExpectedProposalStates = new Dictionary<string, string> { ["dev-p01"] = "rejected" } },
                package.Oracle.Scenarios) } },
            package with { Oracle = package.Oracle with
                { AggregateExpectations = package.Oracle.AggregateExpectations with { ScenarioCount = 99 } } },
            package with { Oracle = package.Oracle with
                { FrozenBoundaries = new Dictionary<string, bool> { ["mutated-boundary"] = false } } },
            package with { Oracle = package.Oracle with { ForbiddenClaims = ["network not used"] } },
        ];
        foreach (SourceClaimFixturePackage mutation in mutations)
        {
            Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimOracleVerifier.Verify(mutation, actual));
        }
    }

    private static IReadOnlyList<SourceClaimScenarioOracle> ReplaceFirst(
        SourceClaimScenarioOracle first, IReadOnlyList<SourceClaimScenarioOracle> source) => [first, .. source.Skip(1)];

    private static string PackageRoot(string package) => Path.Combine(
        RepositoryRoot(), "fixtures", "public", "provider", "source-claims", package);

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class TemporaryPackage : IDisposable
    {
        private readonly string source;

        private TemporaryPackage(string source, string root)
        {
            this.source = source;
            Root = root;
        }

        public string Root { get; }

        public static TemporaryPackage Copy(string package)
        {
            string source = PackageRoot(package);
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Infinium-SourceClaim-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            foreach (string file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, System.IO.Path.Combine(root, System.IO.Path.GetFileName(file)));
            }
            return new(source, root);
        }

        public string Path(string file) => System.IO.Path.Combine(Root, file);

        public void Reset()
        {
            foreach (string file in Directory.EnumerateFiles(Root))
            {
                File.Delete(file);
            }
            foreach (string file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, System.IO.Path.Combine(Root, System.IO.Path.GetFileName(file)));
            }
        }

        public JsonObject ReadObject(string file) => JsonNode.Parse(File.ReadAllBytes(Path(file)))!.AsObject();

        public void WriteObject(string file, JsonObject value) =>
            File.WriteAllText(Path(file), value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");

        public void Reseal(string file)
        {
            JsonObject manifest = ReadObject("public-manifest.json");
            JsonObject identity = manifest["file_identities"]!.AsArray().Select(x => x!.AsObject())
                .Single(x => x["path"]!.GetValue<string>() == file);
            byte[] bytes = File.ReadAllBytes(Path(file));
            identity["bytes"] = bytes.Length;
            identity["sha256"] = Convert.ToHexStringLower(SHA256.HashData(bytes));
            WriteObject("public-manifest.json", manifest);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
