using System.Text;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Application.Serialization;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateInvestigationProviderProvenanceContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateInvestigationAnswerFreeInputsValidateAndMinimizeExactly()
    {
        foreach (string package in new[] { "S6-CANDIDATE-DEV-v1", "S6-CANDIDATE-VAL-v1" })
        {
            string directory = PackageDirectory(package);
            Validate(Path.Combine(directory, "execution-input.v1.json"), "candidate-investigation-execution-input.v1.schema.json");
            Validate(Path.Combine(directory, "context-manifest.v1.json"), "candidate-investigation-context.v1.schema.json");
            Validate(Path.Combine(directory, "retained-transcripts.v1.json"), "candidate-investigation-retained-transcripts.v1.schema.json");
            CandidateInvestigationExecutionInput input = JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
                File.ReadAllBytes(Path.Combine(directory, "execution-input.v1.json")), SourceClaimContextMinimizer.JsonOptions)!;
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(directory, "context-manifest.v1.json")),
                CandidateInvestigationContextMinimizer.CreateManifest(input));
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void ProviderProvenanceRetainsPromptCandidateHypothesisEvidenceAndSourceLinks()
    {
        StringAssert.Matches(CandidateInvestigationPromptV1.Fingerprint,
            new System.Text.RegularExpressions.Regex("^[0-9a-f]{64}$"));
        StringAssert.Contains(CandidateInvestigationPromptV1.Instructions, "untrusted data");
        StringAssert.Contains(CandidateInvestigationPromptV1.Instructions, "source-acquisition");
        StringAssert.Contains(CandidateInvestigationPromptV1.Instructions, "Do not create findings");
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load("S6-CANDIDATE-DEV-v1");
        CandidateInvestigationScenarioResult scenario = CandidateInvestigationEngine.Execute(input, transcripts).Scenarios[0];
        Assert.AreEqual("hypothesis-dev-positive", scenario.HypothesisId);
        Assert.AreEqual("acquisition-dev-positive", scenario.SourceAcquisitionLinks.Single().SourceAcquisitionId);
        Assert.AreEqual("admission-dev-positive", scenario.SourceAcquisitionLinks.Single().SourceAdmissionId);
        Assert.AreEqual("source-application-dev-positive", scenario.SourceAcquisitionLinks.Single().SourceApplicationLinkId);
        CollectionAssert.Contains(scenario.RawIntermediateIds.ToList(), "response-candidate-dev-positive");
        byte[] json = CandidateInvestigationTransparencyRenderer.RenderJson(new(
            CandidateInvestigationPromptV1.Id, CandidateInvestigationPromptV1.Fingerprint,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(CandidateInvestigationContextMinimizer.CreateManifest(input))),
            [scenario], false, false, false));
        string text = Encoding.UTF8.GetString(json);
        StringAssert.Contains(text, "source_acquisition_links");
        StringAssert.Contains(text, "finding_authority\":\"not-granted");
        StringAssert.Contains(text, "private_verdict\":\"not-performed");
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateInvestigationOutputStrictCodecRoundTripsEveryTerminalState()
    {
        foreach (string package in new[] { "S6-CANDIDATE-DEV-v1", "S6-CANDIDATE-VAL-v1" })
        {
            (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load(package);
            foreach (CandidateInvestigationScenarioResult scenario in CandidateInvestigationEngine.Execute(input, transcripts).Scenarios)
            {
                byte[] first = ProviderContractJsonCodecs.Serialize(scenario.Investigation);
                byte[] second = ProviderContractJsonCodecs.Serialize(ProviderContractJsonCodecs.DeserializeCandidateInvestigation(first));
                CollectionAssert.AreEqual(first, second);
            }
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateInvestigationFrozenOracleComparesBothPartitionsExactly()
    {
        foreach (string package in new[] { "S6-CANDIDATE-DEV-v1", "S6-CANDIDATE-VAL-v1" })
        {
            using CandidateInvestigationFixturePackage fixture = CandidateInvestigationFixtureReader.Read(PackageDirectory(package));
            CandidateInvestigationResult actual = CandidateInvestigationEngine.Execute(fixture.ExecutionInput, fixture.Transcripts);
            CandidateInvestigationOracleVerifier.Verify(fixture, actual);
        }
    }

    private static void Validate(string path, string schema)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        ActiveJsonSchemaValidator.Validate(document.RootElement, schema);
    }

    private static (CandidateInvestigationExecutionInput, CandidateInvestigationRetainedTranscript[]) Load(string package)
    {
        string directory = PackageDirectory(package);
        CandidateInvestigationExecutionInput input = JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
            File.ReadAllBytes(Path.Combine(directory, "execution-input.v1.json")), SourceClaimContextMinimizer.JsonOptions)!;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "retained-transcripts.v1.json")));
        return (input, JsonSerializer.Deserialize<CandidateInvestigationRetainedTranscript[]>(
            document.RootElement.GetProperty("transcripts"), SourceClaimContextMinimizer.JsonOptions)!);
    }

    private static string PackageDirectory(string package) => Path.Combine(TestRepository.Root,
        "fixtures", "public", "provider", "candidate-investigations", package);
}
