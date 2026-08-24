using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateInvestigationProviderContextTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void CurrentExamplesRetainSupportedUnsupportedAndFailureStates()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        CandidateInvestigationResult normal = CandidateInvestigationEngine.Execute(input,
            [CandidateInvestigationDeveloperExample.Positive(), CandidateInvestigationDeveloperExample.Unsupported()]);
        Assert.AreEqual("accepted", normal.Scenarios[0].Disposition);
        Assert.AreEqual(SemanticDecisionState.Admitted,
            normal.Scenarios[0].Investigation.AdmissionLinks.Single().DecisionState);
        Assert.AreEqual("abstained-unsupported", normal.Scenarios[1].Disposition);
        Assert.AreEqual(SemanticSupportState.Unsupported,
            normal.Scenarios[1].Investigation.AdmissionLinks.Single().SupportState);
        Assert.AreEqual("not-used", CandidateInvestigationEngine.Execute(input,
            [CandidateInvestigationDeveloperExample.NoModel()]).Scenarios.Single().Disposition);
        Assert.AreEqual("rejected-identity-drift", CandidateInvestigationEngine.Execute(input,
            [CandidateInvestigationDeveloperExample.Drift()]).Scenarios.Single().Disposition);
        Assert.IsFalse(normal.NetworkUsed || normal.CredentialUsed || normal.SourceRefreshUsed);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderContextRejectsCrossCandidateAndInventedEvidence()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        CandidateInvestigationRetainedTranscript positive = CandidateInvestigationDeveloperExample.Positive();
        CandidateInvestigationTranscriptProposal proposal = positive.Proposals.Single();
        CandidateInvestigationScenarioResult crossCandidate = CandidateInvestigationEngine.Execute(input,
            [positive with { Proposals = [proposal with { CandidateId = input.Contexts[1].CandidateId }] }])
            .Scenarios.Single();
        Assert.AreEqual("rejected", crossCandidate.Disposition);
        CandidateInvestigationScenarioResult inventedEvidence = CandidateInvestigationEngine.Execute(input,
            [positive with { Proposals = [proposal with { SupportingEvidenceIds = ["evidence-invented"] }] }])
            .Scenarios.Single();
        Assert.AreEqual(SemanticDecisionState.Rejected,
            inventedEvidence.Investigation.AdmissionLinks.Single().DecisionState);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderLabelsCannotChangeHostCandidateDecision()
    {
        CandidateInvestigationExecutionInput input = CandidateInvestigationDeveloperExample.Input();
        CandidateInvestigationRetainedTranscript transcript = CandidateInvestigationDeveloperExample.Positive();
        CandidateInvestigationTranscriptProposal proposal = transcript.Proposals.Single();
        CandidateInvestigationScenarioResult baseline = CandidateInvestigationEngine.Execute(input, [transcript])
            .Scenarios.Single();
        foreach (string state in new[] { "proposed", "unsupported", "abstained", "unavailable" })
        {
            CandidateInvestigationScenarioResult mutation = CandidateInvestigationEngine.Execute(input,
                [transcript with { Proposals = [proposal with { State = state, Reason = "provider-label-mutated" }] }])
                .Scenarios.Single();
            Assert.AreEqual(baseline.Investigation.AdmissionLinks.Single().SupportState,
                mutation.Investigation.AdmissionLinks.Single().SupportState, state);
            Assert.AreEqual(baseline.Investigation.AdmissionLinks.Single().DecisionState,
                mutation.Investigation.AdmissionLinks.Single().DecisionState, state);
        }
    }
}
