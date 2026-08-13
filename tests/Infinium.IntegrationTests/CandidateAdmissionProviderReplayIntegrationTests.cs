using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.Coordinator;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateAdmissionProviderReplayIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public void CandidateAdmissionExecutesBothPartitionsWithoutTransportOrSourceRefresh()
    {
        foreach (string package in new[] { "S6-CANDIDATE-DEV-v1", "S6-CANDIDATE-VAL-v1" })
        {
            (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load(package);
            CandidateInvestigationResult result = CandidateInvestigationCoordinator.ExecuteRetained(input, transcripts);
            Assert.IsFalse(result.NetworkUsed || result.CredentialUsed || result.SourceRefreshUsed);
            Assert.IsTrue(result.Scenarios.All(x => x.Investigation.OwnerId.Value == input.AnalysisRunId));
            Assert.IsTrue(result.Scenarios.All(x => x.SourceAcquisitionLinks.Count > 0));
            Assert.IsTrue(result.Scenarios.SelectMany(x => x.RawIntermediateIds).Any());
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderReplayIsByteStableAndIdentityDriftFailsAuditOnlyWithoutSend()
    {
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load("S6-CANDIDATE-DEV-v1");
        CandidateInvestigationRetainedTranscript transcript = transcripts[0];
        CandidateInvestigationScenarioResult first = CandidateInvestigationCoordinator.ReplayRetained(input, transcript, transcript.ResponseFingerprint);
        CandidateInvestigationScenarioResult second = CandidateInvestigationCoordinator.ReplayRetained(input, transcript, transcript.ResponseFingerprint);
        Assert.AreEqual(first.CanonicalInvestigationSha256, second.CanonicalInvestigationSha256);
        Assert.AreEqual("retained-response", second.ReplayState);
        CandidateInvestigationScenarioResult drift = CandidateInvestigationCoordinator.ReplayRetained(input, transcript, new string('0', 64));
        Assert.AreEqual("audit-only", drift.ReplayState);
        Assert.AreEqual("rejected-identity-drift", drift.Disposition);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void CandidateAdmissionNoModelCannotFabricateProviderUse()
    {
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load("S6-CANDIDATE-DEV-v1");
        CandidateInvestigationRetainedTranscript noModel = transcripts.Single(x => x.ContextId == "context-dev-no-model");
        CandidateInvestigationResult result = CandidateInvestigationCoordinator.NoModel(input, noModel);
        Assert.AreEqual("not-used", result.Scenarios.Single().Disposition);
        Assert.AreEqual(0, result.Scenarios.Single().Investigation.HypothesisProposals.Count);
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationCoordinator.NoModel(input, transcripts[0]));
    }

    internal static (CandidateInvestigationExecutionInput, CandidateInvestigationRetainedTranscript[]) Load(string package)
    {
        string directory = Path.Combine(SourceClaimAdmissionIntegrationTests.RepositoryRoot(), "fixtures", "public", "provider", "candidate-investigations", package);
        CandidateInvestigationExecutionInput input = JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
            File.ReadAllBytes(Path.Combine(directory, "execution-input.v1.json")), SourceClaimContextMinimizer.JsonOptions)!;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "retained-transcripts.v1.json")));
        return (input, JsonSerializer.Deserialize<CandidateInvestigationRetainedTranscript[]>(
            document.RootElement.GetProperty("transcripts"), SourceClaimContextMinimizer.JsonOptions)!);
    }
}
