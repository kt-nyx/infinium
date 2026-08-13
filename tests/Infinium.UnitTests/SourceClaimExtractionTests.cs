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
        Assert.AreEqual(5, result.Scenarios.Count);
        Assert.AreEqual(ProposalAdmissionState.Admitted,
            result.Scenarios.Single(x => x.TranscriptId == "dev-01").Extraction.ClaimProposals.Single().State);
        Assert.AreEqual(ProposalAdmissionState.Unsupported,
            result.Scenarios.Single(x => x.TranscriptId == "dev-02").Extraction.ClaimProposals.Single().State);
        Assert.AreEqual(ProposalAdmissionState.Admitted,
            result.Scenarios.Single(x => x.TranscriptId == "dev-03").Extraction.ClaimProposals.Single().State);
        Assert.AreEqual("not-applicable", result.Scenarios.Single(x => x.TranscriptId == "dev-04").ReplayState);
        Assert.AreEqual(0, result.Scenarios.Single(x => x.TranscriptId == "dev-05").Extraction.ClaimProposals.Count);
        Assert.AreEqual(1, result.Scenarios.Single(x => x.TranscriptId == "dev-05").Extraction.Abstentions.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void SourceClaimExtractionRetainsContradictionHostileDeletedAndFailureStates()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] transcripts) = Load("S6-CLAIM-VAL-v1");
        SourceClaimAcquisitionResult result = SourceClaimAcquisitionEngine.Execute(input, transcripts);

        Assert.AreEqual(ProposalAdmissionState.Abstained, Scenario("val-01").ClaimProposals.Single().State);
        Assert.AreEqual(1, Scenario("val-01").ContradictionEvidenceIds.Count);
        Assert.AreEqual(ProposalAdmissionState.Rejected, Scenario("val-02").ClaimProposals.Single().State);
        Assert.AreEqual("model-proposed-forbidden-authority", Scenario("val-02").ClaimProposals.Single().Reason);
        Assert.AreEqual(ProposalAdmissionState.Deleted, Scenario("val-03").ClaimProposals.Single().State);
        foreach (string id in new[] { "val-04", "val-05", "val-06", "val-07" })
        {
            Assert.AreEqual("audit-only", result.Scenarios.Single(x => x.TranscriptId == id).ReplayState);
        }

        SourceClaimExtractionDocument Scenario(string id) =>
            result.Scenarios.Single(x => x.TranscriptId == id).Extraction;
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderContextRejectsExpectedAnswersAndDrift()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] transcripts) = Load("S6-CLAIM-DEV-v1");
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Execute(
            input with { DeclaredPurpose = "Use the expected_oracle answer." }, transcripts));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Execute(
            input, [transcripts[0] with { SourceRevisionId = "other-revision" }]));

        SourceClaimScenarioResult replay = SourceClaimAcquisitionEngine.Replay(input, transcripts[0], new string('f', 64));
        Assert.AreEqual("audit-only", replay.ReplayState);
        CollectionAssert.Contains(replay.AuditReasons.ToArray(), "retained-response-fingerprint-drift");
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
