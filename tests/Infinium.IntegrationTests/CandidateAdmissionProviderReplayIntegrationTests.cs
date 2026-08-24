using Infinium.Application.Provider;
using Infinium.Coordinator;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateAdmissionProviderReplayIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    public void CandidateAdmissionExecutesBothPartitionsWithoutTransportOrSourceRefresh()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        CandidateInvestigationRetainedTranscript[] transcripts =
            [CandidateInvestigationDeveloperExample.Positive(), CandidateInvestigationDeveloperExample.Unsupported()];
        CandidateInvestigationResult result = CandidateInvestigationCoordinator.ExecuteRetained(input, transcripts);
        Assert.IsFalse(result.NetworkUsed || result.CredentialUsed || result.SourceRefreshUsed);
        Assert.IsTrue(result.Scenarios.All(x => x.Investigation.OwnerId.Value == input.AnalysisRunId));
        Assert.IsTrue(result.Scenarios.Any(x => x.SourceAcquisitionLinks.Count > 0));
        Assert.IsTrue(result.Scenarios.SelectMany(x => x.RawIntermediateIds).Any());
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void ProviderReplayIsByteStableAndIdentityDriftFailsAuditOnlyWithoutSend()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        CandidateInvestigationRetainedTranscript transcript = CandidateInvestigationDeveloperExample.Positive();
        CandidateInvestigationScenarioResult first = CandidateInvestigationCoordinator.ReplayRetained(input, transcript);
        CandidateInvestigationScenarioResult second = CandidateInvestigationCoordinator.ReplayRetained(input, transcript);
        Assert.AreEqual(first.CanonicalInvestigationSha256, second.CanonicalInvestigationSha256);
        Assert.AreEqual("retained-response", second.ReplayState);
        CandidateInvestigationRetainedTranscript driftTranscript = CandidateInvestigationDeveloperExample.Drift();
        CandidateInvestigationScenarioResult drift = CandidateInvestigationCoordinator.ReplayRetained(input, driftTranscript);
        Assert.AreEqual("failed-identity-drift", drift.ReplayState);
        Assert.AreEqual("rejected-identity-drift", drift.Disposition);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public void CandidateAdmissionNoModelCannotFabricateProviderUse()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        CandidateInvestigationRetainedTranscript noModel = CandidateInvestigationDeveloperExample.NoModel();
        CandidateInvestigationResult result = CandidateInvestigationCoordinator.NoModel(input, noModel);
        Assert.AreEqual("not-used", result.Scenarios.Single().Disposition);
        Assert.AreEqual(0, result.Scenarios.Single().Investigation.HypothesisProposals.Count);
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationCoordinator.NoModel(
            input, CandidateInvestigationDeveloperExample.Positive()));
    }

    internal static (CandidateInvestigationExecutionInput, CandidateInvestigationRetainedTranscript[]) CurrentExample() =>
        (CandidateInvestigationDeveloperExample.Input(),
            [
                CandidateInvestigationDeveloperExample.Positive(),
                CandidateInvestigationDeveloperExample.Unsupported(),
                CandidateInvestigationDeveloperExample.NoModel(),
                CandidateInvestigationDeveloperExample.Unavailable(),
                CandidateInvestigationDeveloperExample.Drift(),
            ]);
}
