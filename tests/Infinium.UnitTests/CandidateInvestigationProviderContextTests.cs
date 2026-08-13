using System.Text.Json;
using Infinium.Application.Provider;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateInvestigationProviderContextTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void CandidateInvestigationRetainsPositiveMatchedNegativeAndBoundedFailureStates()
    {
        CandidateInvestigationResult development = Execute("S6-CANDIDATE-DEV-v1");
        CandidateInvestigationResult validation = Execute("S6-CANDIDATE-VAL-v1");

        Assert.AreEqual(8, development.Scenarios.Count);
        Assert.AreEqual(7, validation.Scenarios.Count);
        Assert.AreEqual("accepted", Scenario(development, "positive").Disposition);
        Assert.AreEqual("rejected-matched-negative", Scenario(development, "matched-negative").Disposition);
        Assert.AreEqual("accepted-conditional", Scenario(development, "conditional").Disposition);
        Assert.AreEqual("rejected-unsupported", Scenario(development, "unsupported").Disposition);
        Assert.AreEqual("rejected-contradiction-abstained", Scenario(development, "contradiction").Disposition);
        Assert.AreEqual("rejected-explicit-abstention", Scenario(development, "abstention").Disposition);
        Assert.AreEqual("not-used", Scenario(development, "no-model").Disposition);
        Assert.AreEqual("unavailable-provider", Scenario(development, "unavailable-provider").Disposition);
        Assert.AreEqual("rejected-hostile-authority", Scenario(validation, "hostile").Disposition);
        Assert.AreEqual("rejected-malformed", Scenario(validation, "malformed").Disposition);
        Assert.AreEqual("rejected-refusal", Scenario(validation, "refusal").Disposition);
        Assert.AreEqual("rejected-incomplete", Scenario(validation, "incomplete").Disposition);
        Assert.AreEqual("rejected-deleted-audit-only", Scenario(validation, "deleted").Disposition);
        Assert.AreEqual("rejected-identity-drift", Scenario(validation, "drift").Disposition);
        Assert.IsFalse(development.NetworkUsed || development.CredentialUsed || development.SourceRefreshUsed);
        Assert.IsFalse(validation.NetworkUsed || validation.CredentialUsed || validation.SourceRefreshUsed);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderContextRejectsCrossCandidateAndInventedEvidence()
    {
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load("S6-CANDIDATE-DEV-v1");
        CandidateInvestigationRetainedTranscript positive = transcripts.Single(x => x.ContextId == "context-dev-positive");
        CandidateInvestigationTranscriptProposal proposal = positive.Proposals.Single();
        CandidateInvestigationResult crossCandidate = CandidateInvestigationEngine.Execute(input,
            [positive with { Proposals = [proposal with { CandidateId = "candidate-dev-matched-negative" }] }]);
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
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load("S6-CANDIDATE-DEV-v1");
        CandidateInvestigationRetainedTranscript transcript = transcripts.Single(x => x.ContextId == "context-dev-matched-negative")
            with
        { ContextId = "renamed-context" };
        CandidateInvestigationContextInput context = input.Contexts.Single(x => x.ContextId == "context-dev-matched-negative")
            with
        { ContextId = "renamed-context" };
        CandidateInvestigationExecutionInput renamed = input with
        {
            Contexts = input.Contexts.Select(x => x.ContextId == "context-dev-matched-negative" ? context : x).ToArray(),
        };

        Assert.AreEqual("rejected-matched-negative",
            CandidateInvestigationEngine.Execute(renamed, [transcript]).Scenarios.Single().Disposition);
    }

    private static CandidateInvestigationScenarioResult Scenario(CandidateInvestigationResult result, string suffix) =>
        result.Scenarios.Single(x => x.ContextId.EndsWith(suffix, StringComparison.Ordinal));

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
