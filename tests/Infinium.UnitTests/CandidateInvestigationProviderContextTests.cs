using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateInvestigationProviderContextTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void CandidateInvestigationRetainsPositiveMatchedNegativeAndBoundedFailureStates()
    {
        CandidateInvestigationResult development = Execute("S6-CANDIDATE-DEV-v2");
        CandidateInvestigationResult validation = Execute("S6-CANDIDATE-VAL-v3");

        Assert.AreEqual(8, development.Scenarios.Count);
        Assert.AreEqual(15, validation.Scenarios.Count);
        foreach (string disposition in new[] { "accepted", "abstained-unsupported", "accepted-conditional",
                     "abstained-contradicted",
                     "not-used", "unavailable-provider" })
        {
            Assert.IsTrue(development.Scenarios.Any(item => item.Disposition == disposition), disposition);
        }
        foreach (string disposition in new[] { "rejected-hostile-authority", "rejected-malformed", "rejected-refusal",
                     "rejected-incomplete", "rejected-deleted-audit-only", "rejected-identity-drift" })
        {
            Assert.AreEqual(disposition, Scenario(validation, disposition).Disposition);
        }
        Assert.IsFalse(development.NetworkUsed || development.CredentialUsed || development.SourceRefreshUsed);
        Assert.IsFalse(validation.NetworkUsed || validation.CredentialUsed || validation.SourceRefreshUsed);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderContextRejectsCrossCandidateAndInventedEvidence()
    {
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load("S6-CANDIDATE-DEV-v2");
        CandidateInvestigationRetainedTranscript positive = transcripts[0];
        CandidateInvestigationTranscriptProposal proposal = positive.Proposals.Single();
        CandidateInvestigationResult crossCandidate = CandidateInvestigationEngine.Execute(input,
            [positive with { Proposals = [proposal with { CandidateId = input.Contexts[1].CandidateId }] }]);
        Assert.AreEqual("rejected", crossCandidate.Scenarios.Single().Disposition);
        CandidateInvestigationResult inventedEvidence = CandidateInvestigationEngine.Execute(input,
            [positive with { Proposals = [proposal with { SupportingEvidenceIds = ["evidence-invented"] }] }]);
        Assert.AreEqual("rejected", inventedEvidence.Scenarios.Single().Disposition);
        Assert.AreEqual(0, inventedEvidence.Scenarios.Single().Investigation.HypothesisProposals.Single().SupportingEvidenceIds.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void MatchedNegativeDispositionDoesNotDependOnFixtureIdentity()
    {
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load("S6-CANDIDATE-DEV-v2");
        CandidateInvestigationResult baseline = CandidateInvestigationEngine.Execute(input, transcripts);
        string contextId = baseline.Scenarios.First(x => x.Disposition == "abstained-unsupported").ContextId;
        CandidateInvestigationRetainedTranscript transcript = transcripts.Single(x => x.ContextId == contextId)
            with
        { ContextId = "renamed-context" };
        CandidateInvestigationContextInput context = input.Contexts.Single(x => x.ContextId == contextId)
            with
        { ContextId = "renamed-context" };
        CandidateInvestigationExecutionInput renamed = input with
        {
            Contexts = input.Contexts.Select(x => x.ContextId == contextId ? context : x).ToArray(),
        };

        Assert.AreEqual("abstained-unsupported",
            CandidateInvestigationEngine.Execute(renamed, [transcript]).Scenarios.Single().Disposition);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void HostValidatesEvidenceRelationshipsAndRequiresKnownContradictionClosure()
    {
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) =
            Load("S6-CANDIDATE-DEV-v2");
        CandidateInvestigationContextInput context = input.Contexts.Single(item =>
            item.Evidence.Any(evidence => evidence.Relationship == "contradicting"));
        CandidateInvestigationRetainedTranscript transcript = transcripts.Single(item => item.ContextId == context.ContextId);
        CandidateInvestigationTranscriptProposal proposal = transcript.Proposals.Single();
        string contradictionId = context.Evidence.Single(item => item.Relationship == "contradicting").EvidenceId;
        string supportingId = context.Evidence.Single(item => item.Relationship == "supporting").EvidenceId;

        CandidateInvestigationScenarioResult wrongRelationship = CandidateInvestigationEngine.Execute(input,
            [transcript with { Proposals = [proposal with
            {
                SupportingEvidenceIds = [supportingId, contradictionId],
                ContradictingEvidenceIds = [],
                State = "proposed",
            }] }]).Scenarios.Single();
        Assert.AreEqual(SemanticDecisionState.Rejected,
            wrongRelationship.Investigation.AdmissionLinks.Single().DecisionState);

        CandidateInvestigationScenarioResult omitted = CandidateInvestigationEngine.Execute(input,
            [transcript with { Proposals = [proposal with
            {
                SupportingEvidenceIds = [supportingId],
                ContradictingEvidenceIds = [],
                State = "proposed",
            }] }]).Scenarios.Single();
        Assert.AreEqual(SemanticDecisionState.Abstained, omitted.Investigation.AdmissionLinks.Single().DecisionState);
        Assert.AreEqual(SemanticSupportState.Contradicted, omitted.Investigation.AdmissionLinks.Single().SupportState);
        Assert.AreEqual("known-contradiction-omitted", omitted.Investigation.HypothesisProposals.Single().Reason);
    }

    private static CandidateInvestigationScenarioResult Scenario(CandidateInvestigationResult result, string disposition) =>
        result.Scenarios.Single(x => x.Disposition == disposition);

    private static CandidateInvestigationResult Execute(string package)
    {
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load(package);
        return CandidateInvestigationEngine.Execute(input, transcripts);
    }

    private static (CandidateInvestigationExecutionInput, CandidateInvestigationRetainedTranscript[]) Load(string package)
    {
        string directory = PackageDirectory(package);
        CandidateInvestigationExecutionInput input = JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
            File.ReadAllBytes(Path.Combine(directory, "execution-input.v1.json")), SourceClaimContextMinimizer.JsonOptions)!;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "retained-transcripts.v1.json")));
        CandidateInvestigationRetainedTranscript[] transcripts = JsonSerializer.Deserialize<CandidateInvestigationRetainedTranscript[]>(
            document.RootElement.GetProperty("transcripts"), SourceClaimContextMinimizer.JsonOptions)!;
        return (input, transcripts);
    }

    private static string PackageDirectory(string package)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return Path.Combine(current!.FullName, "fixtures", "public", "provider", "candidate-investigations", package);
    }
}
