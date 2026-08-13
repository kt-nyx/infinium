using System.Text;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateInvestigationProviderProvenanceContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateInvestigationAnswerFreeInputsValidateAndMinimizeExactly()
    {
        foreach (string package in new[] { "S6-CANDIDATE-DEV-v2", "S6-CANDIDATE-VAL-v2" })
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
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load("S6-CANDIDATE-DEV-v2");
        CandidateInvestigationScenarioResult scenario = CandidateInvestigationEngine.Execute(input, transcripts).Scenarios[0];
        Assert.AreEqual("hypothesis-d01", scenario.HypothesisId);
        Assert.AreEqual("acquisition-d01", scenario.SourceAcquisitionLinks.Single().SourceAcquisitionId);
        Assert.AreEqual("admission-d01", scenario.SourceAcquisitionLinks.Single().SourceAdmissionId);
        Assert.AreEqual("source-application-d01", scenario.SourceAcquisitionLinks.Single().SourceApplicationLinkId);
        Assert.AreEqual("evidence-application-d01", scenario.SourceAcquisitionLinks.Single().EvidenceApplicationLinkId);
        CollectionAssert.Contains(scenario.RawIntermediateIds.ToList(), "response-candidate-d01");
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
        foreach (string package in new[] { "S6-CANDIDATE-DEV-v2", "S6-CANDIDATE-VAL-v2" })
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
        foreach (string package in new[] { "S6-CANDIDATE-DEV-v2", "S6-CANDIDATE-VAL-v2" })
        {
            using CandidateInvestigationFixturePackage fixture = CandidateInvestigationFixtureReader.Read(PackageDirectory(package));
            CandidateInvestigationResult actual = CandidateInvestigationEngine.Execute(fixture.ExecutionInput, fixture.Transcripts);
            CandidateInvestigationOracleVerifier.Verify(fixture, actual);
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateAnswerIsolationRejectsRecursiveAuthorityAndDispositionCuesInIdentifierVariants()
    {
        foreach (string json in new[]
                 {
                     "{\"outer\":[{\"ground-truth\":\"x\"}]}",
                     "{\"context_id\":\"matched-negative\"}",
                     "{\"candidate_id\":\"positive_control\"}",
                     "{\"evidence_ids\":[\"no model\"]}",
                     "{\"package_identity\":\"unavailable-provider\"}",
                 })
        {
            using JsonDocument mutation = JsonDocument.Parse(json);
            Assert.ThrowsExactly<InvalidDataException>(
                () => CandidateInvestigationFixtureReader.AssertAnswerFreeProductInput(mutation.RootElement));
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateFrozenOracleRejectsEveryMaterialScenarioMutation()
    {
        using CandidateInvestigationFixturePackage fixture =
            CandidateInvestigationFixtureReader.Read(PackageDirectory("S6-CANDIDATE-DEV-v2"));
        CandidateInvestigationResult baseline = CandidateInvestigationEngine.Execute(fixture.ExecutionInput, fixture.Transcripts);
        CandidateInvestigationScenarioResult scenario = baseline.Scenarios[0];
        HypothesisProposalContract proposal = scenario.Investigation.HypothesisProposals.Single();
        ProviderSemanticAdmissionLinkContract link = scenario.Investigation.AdmissionLinks.Single();
        foreach (CandidateInvestigationScenarioResult mutation in new[]
                 {
                     scenario with { ResponseRecordId = "mutated-response" },
                     scenario with { ResponseFingerprint = new string('0', 64) },
                     scenario with { ModelUsed = false },
                     scenario with { ProviderUsed = false },
                     scenario with { AuditOnly = true },
                     scenario with { ForbiddenAuthorityDetected = true },
                     scenario with { ReplayState = "audit-only" },
                     scenario with { Investigation = scenario.Investigation with
                         { HypothesisProposals = [proposal with { SupportingEvidenceIds = [] }] } },
                     scenario with { Investigation = scenario.Investigation with
                         { HypothesisProposals = [proposal with { ContradictingEvidenceIds = [new("evidence-d01")] }] } },
                     scenario with { Investigation = scenario.Investigation with { AdmissionLinks = [] } },
                     scenario with { Investigation = scenario.Investigation with
                         { AdmissionLinks = [link with { State = ProposalAdmissionState.Rejected }] } },
                 })
        {
            CandidateInvestigationResult actual = baseline with
            {
                Scenarios = baseline.Scenarios.Select(item => item.TranscriptId == scenario.TranscriptId ? mutation : item).ToArray(),
            };
            Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(fixture, actual));
        }

        CandidateInvestigationScenarioResult conditional = baseline.Scenarios.Single(item =>
            item.Disposition == "accepted-conditional");
        HypothesisProposalContract conditionalProposal = conditional.Investigation.HypothesisProposals.Single();
        CandidateInvestigationResult missingInformationMutation = baseline with
        {
            Scenarios = baseline.Scenarios.Select(item => item.TranscriptId == conditional.TranscriptId
                ? conditional with
                {
                    Investigation = conditional.Investigation with
                    { HypothesisProposals = [conditionalProposal with { MissingInformation = ["mutated"] }] }
                }
                : item).ToArray(),
        };
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CandidateInvestigationOracleVerifier.Verify(fixture, missingInformationMutation));
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
