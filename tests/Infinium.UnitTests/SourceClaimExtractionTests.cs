using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

[TestClass]
public sealed class SourceClaimExtractionTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void SourceClaimExtractionAdmitsOnlyHostValidatedExactCitations()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] transcripts) = Load("S6-CLAIM-DEV-v1");
        SourceClaimAcquisitionResult result = SourceClaimAcquisitionEngine.Execute(input, transcripts);

        Assert.IsFalse(result.NetworkUsed);
        Assert.IsFalse(result.CredentialUsed);
        Assert.IsFalse(result.SourceRefreshUsed);
        Assert.AreEqual(SourceClaimPromptV1.Fingerprint, result.PromptFingerprint);
        Assert.AreEqual(6, result.Scenarios.Count);
        Assert.AreEqual(SemanticProposalState.Extracted,
            result.Scenarios.Single(x => x.TranscriptId == "dev-01").Extraction.ClaimProposals.Single().ExtractionState);
        Assert.AreEqual(SemanticSupportState.Unsupported,
            result.Scenarios.Single(x => x.TranscriptId == "dev-02").Extraction.AdmissionCorrelations.Single().SupportState);
        Assert.AreEqual(SemanticProposalState.Extracted,
            result.Scenarios.Single(x => x.TranscriptId == "dev-03").Extraction.ClaimProposals.Single().ExtractionState);
        Assert.AreEqual("not-applicable", result.Scenarios.Single(x => x.TranscriptId == "dev-04").ReplayState);
        Assert.AreEqual(0, result.Scenarios.Single(x => x.TranscriptId == "dev-05").Extraction.ClaimProposals.Count);
        Assert.AreEqual(1, result.Scenarios.Single(x => x.TranscriptId == "dev-05").Extraction.Abstentions.Count);
        SourceClaimScenarioResult conditional = result.Scenarios.Single(x => x.TranscriptId == "dev-06");
        Assert.AreEqual("extracted-condition-unestablished", conditional.Disposition);
        Assert.AreEqual(SemanticApplicabilityState.ConditionalUnestablished,
            conditional.Extraction.AdmissionCorrelations.Single().ApplicabilityState);
        Assert.AreEqual(SemanticDecisionState.Abstained,
            conditional.Extraction.AdmissionCorrelations.Single().DecisionState);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void SourceClaimExtractionRetainsContradictionHostileDeletedAndFailureStates()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] transcripts) = Load("S6-CLAIM-VAL-v1");
        SourceClaimAcquisitionResult result = SourceClaimAcquisitionEngine.Execute(input, transcripts);

        Assert.AreEqual(SemanticDecisionState.Abstained, Scenario("val-01").AdmissionCorrelations.Single().DecisionState);
        Assert.AreEqual(1, Scenario("val-01").ContradictionEvidenceIds.Count);
        Assert.AreEqual(SemanticProposalState.Rejected, Scenario("val-02").ClaimProposals.Single().ExtractionState);
        Assert.AreEqual("model-proposed-forbidden-authority", Scenario("val-02").ClaimProposals.Single().Reason);
        Assert.AreEqual(SemanticProposalState.Deleted, Scenario("val-03").ClaimProposals.Single().ExtractionState);
        Assert.AreEqual("retained-response", result.Scenarios.Single(x => x.TranscriptId == "val-04").ReplayState);
        Assert.AreEqual("retained-response", result.Scenarios.Single(x => x.TranscriptId == "val-05").ReplayState);
        Assert.AreEqual("retained-response", result.Scenarios.Single(x => x.TranscriptId == "val-06").ReplayState);
        Assert.AreEqual("failed-identity-drift", result.Scenarios.Single(x => x.TranscriptId == "val-07").ReplayState);
        Assert.AreEqual("abstained-explicit", result.Scenarios.Single(x => x.TranscriptId == "val-08").Disposition);

        SourceClaimExtractionDocument Scenario(string id) =>
            result.Scenarios.Single(x => x.TranscriptId == id).Extraction;
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderContextRejectsExpectedAnswersAndDrift()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] transcripts) = Load("S6-CLAIM-DEV-v1");
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Execute(
            input with { HostAuthorizationId = "" }, transcripts));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Execute(
            input, [transcripts[0] with { SourceRevisionId = "other-revision" }]));

        SourceClaimScenarioResult replay = SourceClaimAcquisitionEngine.Replay(input, transcripts[0], new string('f', 64));
        Assert.AreEqual("audit-only", replay.ReplayState);
        CollectionAssert.Contains(replay.AuditReasons.ToArray(), "retained-response-fingerprint-drift");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderContextUsesStructuralAuthorityAndReplayEnvelopes()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] transcripts) = Load("S6-CLAIM-DEV-v1");
        SourceClaimRetainedTranscript baseline = transcripts[0];
        SourceClaimTranscriptProposal proposal = baseline.Proposals.Single();
        SourceClaimScenarioResult hostile = SourceClaimAcquisitionEngine.Execute(input,
            [baseline with { Proposals = [proposal with
            {
                Claim = "Perform the protected action now.", AuthorityCategory = "protected-effect-request",
            }] }]).Scenarios.Single();
        Assert.AreEqual(SemanticProposalState.Rejected, hostile.Extraction.ClaimProposals.Single().ExtractionState);

        SourceClaimScenarioResult benign = SourceClaimAcquisitionEngine.Execute(input,
            [baseline with { Proposals = [proposal with
            {
                Claim = "Credentials are described as locally retained documentation metadata.",
                AuthorityCategory = "informational",
            }] }]).Scenarios.Single();
        Assert.AreEqual(SemanticProposalState.Extracted, benign.Extraction.ClaimProposals.Single().ExtractionState);

        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Replay(
            input, baseline with { OperationId = "other-operation" }, baseline.ResponseFingerprint));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Replay(
            input, baseline with { SourceRevisionId = "other-source" }, baseline.ResponseFingerprint));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Replay(
            input, baseline with { PromptId = "other-prompt" }, baseline.ResponseFingerprint));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Execute(input,
            [baseline with { ModelUsed = false }]));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Execute(input,
            [transcripts.Single(x => x.TranscriptId == "dev-04") with
            {
                Proposals = [proposal],
            }]));

        (SourceClaimExecutionInput valInput, SourceClaimRetainedTranscript[] valTranscripts) = Load("S6-CLAIM-VAL-v1");
        Assert.AreEqual("audit-only", SourceClaimAcquisitionEngine.Replay(valInput,
            valTranscripts.Single(x => x.TranscriptId == "val-03"), new string('c', 64)).ReplayState);
        Assert.AreEqual("failed-identity-drift", SourceClaimAcquisitionEngine.Replay(valInput,
            valTranscripts.Single(x => x.TranscriptId == "val-07"),
            valTranscripts.Single(x => x.TranscriptId == "val-07").ResponseFingerprint).ReplayState);
        Assert.AreEqual("not-applicable", SourceClaimAcquisitionEngine.Replay(input,
            transcripts.Single(x => x.TranscriptId == "dev-04"), new string('4', 64)).ReplayState);
    }

    private static (SourceClaimExecutionInput Input, SourceClaimRetainedTranscript[] Transcripts) Load(string package)
    {
        string root = RepositoryRoot();
        string directory = Path.Combine(root, "fixtures", "public", "provider", "source-claims", package);
        JsonSerializerOptions options = SourceClaimContextMinimizer.JsonOptions;
        SourceClaimExecutionInput input = JsonSerializer.Deserialize<SourceClaimExecutionInput>(
            File.ReadAllBytes(Path.Combine(directory, "execution-input.v1.json")), options)!;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "retained-transcripts.v1.json")));
        SourceClaimRetainedTranscript[] transcripts = JsonSerializer.Deserialize<SourceClaimRetainedTranscript[]>(
            document.RootElement.GetProperty("transcripts"), options)!;
        return (input, transcripts);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
