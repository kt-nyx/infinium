using System.Security.Cryptography;
using System.Text;
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
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] retained) =
            LoadTestCase("core");
        SourceClaimRetainedTranscript[] transcripts = NormalizeGroundedClaims(input, retained);
        SourceClaimAcquisitionResult result = SourceClaimAcquisitionEngine.Execute(input, transcripts);

        Assert.IsFalse(result.NetworkUsed);
        Assert.IsFalse(result.CredentialUsed);
        Assert.IsFalse(result.SourceRefreshUsed);
        Assert.AreEqual(SourceClaimPromptV1.Fingerprint, result.PromptFingerprint);
        Assert.AreEqual(6, result.Scenarios.Count);
        Assert.AreEqual(SemanticProposalState.Extracted,
            result.Scenarios.Single(x => x.TranscriptId == "core-01").Extraction.ClaimProposals.Single().ExtractionState);
        Assert.AreEqual(SemanticSupportState.Supported,
            result.Scenarios.Single(x => x.TranscriptId == "core-01").Extraction.AdmissionCorrelations.Single().SupportState);
        Assert.AreEqual(SemanticApplicabilityState.NotEvaluated,
            result.Scenarios.Single(x => x.TranscriptId == "core-01").Extraction.AdmissionCorrelations.Single()
                .ApplicabilityState);
        Assert.AreEqual(SemanticSupportState.Supported,
            result.Scenarios.Single(x => x.TranscriptId == "core-02").Extraction.AdmissionCorrelations.Single().SupportState);
        Assert.AreEqual(SemanticApplicabilityState.NotEvaluated,
            result.Scenarios.Single(x => x.TranscriptId == "core-02").Extraction.AdmissionCorrelations.Single()
                .ApplicabilityState);
        Assert.AreEqual(SemanticProposalState.Extracted,
            result.Scenarios.Single(x => x.TranscriptId == "core-02").Extraction.ClaimProposals.Single().ExtractionState);
        Assert.AreEqual(SemanticProposalState.Extracted,
            result.Scenarios.Single(x => x.TranscriptId == "core-03").Extraction.ClaimProposals.Single().ExtractionState);
        Assert.AreEqual("not-applicable", result.Scenarios.Single(x => x.TranscriptId == "core-04").ReplayState);
        Assert.AreEqual(0, result.Scenarios.Single(x => x.TranscriptId == "core-05").Extraction.ClaimProposals.Count);
        Assert.AreEqual(1, result.Scenarios.Single(x => x.TranscriptId == "core-05").Extraction.Abstentions.Count);
        SourceClaimScenarioResult conditional = result.Scenarios.Single(x => x.TranscriptId == "core-06");
        Assert.AreEqual("accepted-source-extraction", conditional.Disposition);
        Assert.AreEqual(SemanticApplicabilityState.NotEvaluated,
            conditional.Extraction.AdmissionCorrelations.Single().ApplicabilityState);
        Assert.AreEqual(SemanticDecisionState.Abstained,
            conditional.Extraction.AdmissionCorrelations.Single().DecisionState);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void SourceClaimExtractionRetainsContradictionHostileDeletedAndFailureStates()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] retained) =
            LoadTestCase("edge");
        SourceClaimRetainedTranscript[] transcripts = NormalizeGroundedClaims(input, retained);
        SourceClaimAcquisitionResult result = SourceClaimAcquisitionEngine.Execute(input, transcripts);

        Assert.AreEqual(SemanticDecisionState.Rejected, Scenario("edge-01").AdmissionCorrelations.Single().DecisionState);
        Assert.AreEqual(1, Scenario("edge-01").ContradictionEvidenceIds.Count);
        Assert.AreEqual(SemanticProposalState.Extracted, Scenario("edge-02").ClaimProposals.Single().ExtractionState);
        Assert.AreEqual(SemanticDecisionState.Rejected, Scenario("edge-02").AdmissionCorrelations.Single().DecisionState);
        Assert.AreEqual("model-proposed-forbidden-authority", Scenario("edge-02").ClaimProposals.Single().Reason);
        Assert.AreEqual(SemanticProposalState.Deleted, Scenario("edge-03").ClaimProposals.Single().ExtractionState);
        Assert.AreEqual("retained-response", result.Scenarios.Single(x => x.TranscriptId == "edge-04").ReplayState);
        Assert.AreEqual("retained-response", result.Scenarios.Single(x => x.TranscriptId == "edge-05").ReplayState);
        Assert.AreEqual("retained-response", result.Scenarios.Single(x => x.TranscriptId == "edge-06").ReplayState);
        Assert.AreEqual("failed-identity-drift", result.Scenarios.Single(x => x.TranscriptId == "edge-07").ReplayState);
        Assert.AreEqual("rejected", result.Scenarios.Single(x => x.TranscriptId == "edge-08").Disposition);
        Assert.AreEqual(SemanticSupportState.NotEvaluated,
            Scenario("edge-08").AdmissionCorrelations.Single().SupportState);
        Assert.AreEqual(SemanticApplicabilityState.NotEvaluated,
            Scenario("edge-08").AdmissionCorrelations.Single().ApplicabilityState);

        SourceClaimExtractionDocument Scenario(string id) =>
            result.Scenarios.Single(x => x.TranscriptId == id).Extraction;
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderContextRejectsExpectedAnswersAndDrift()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] retained) =
            LoadTestCase("core");
        SourceClaimRetainedTranscript[] transcripts = NormalizeGroundedClaims(input, retained);
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
    public void ProviderStateReasonAndContradictionLabelsCannotChangeHostSourceSupport()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] retained) =
            LoadTestCase("core");
        SourceClaimRetainedTranscript transcript = NormalizeGroundedClaims(input, retained)[0];
        SourceClaimTranscriptProposal proposal = transcript.Proposals.Single();
        SourceClaimAdmissionCorrelationContract baseline = SourceClaimAcquisitionEngine.Execute(input, [transcript])
            .Scenarios.Single().Extraction.AdmissionCorrelations.Single();
        foreach (string state in new[] { "proposed", "unsupported", "abstained", "unavailable" })
        {
            SourceClaimAdmissionCorrelationContract mutation = SourceClaimAcquisitionEngine.Execute(input,
                [transcript with
                {
                    ContradictionEvidenceIds = ["provider-only-contradiction-label"],
                    Proposals = [proposal with { State = state, Reason = "provider-label-mutated" }],
                }]).Scenarios.Single().Extraction.AdmissionCorrelations.Single();
            Assert.AreEqual(baseline.SupportState, mutation.SupportState, state);
            Assert.AreEqual(baseline.DecisionState, mutation.DecisionState, state);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ProviderContextUsesStructuralAuthorityAndReplayEnvelopes()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] retained) =
            LoadTestCase("core");
        SourceClaimRetainedTranscript[] transcripts = NormalizeGroundedClaims(input, retained);
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
                AuthorityCategory = "informational",
            }] }]).Scenarios.Single();
        Assert.AreEqual(SemanticProposalState.Extracted, benign.Extraction.ClaimProposals.Single().ExtractionState);

        string quotedClaim = "Feature Alder is enabled only when mode Copper is selected.";
        string containingPassage = "Configuration notes. " + quotedClaim + " This setting is optional.";
        SourceClaimPassageInput quotedPassage = input.Passages.Single(item => item.PassageId == proposal.PassageId) with
        {
            Text = containingPassage,
            TextSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(containingPassage))),
        };
        SourceClaimExecutionInput quotedInput = input with
        {
            Passages = input.Passages.Select(item => item.PassageId == quotedPassage.PassageId
                ? quotedPassage
                : item).ToArray(),
        };
        SourceClaimScenarioResult exactQuotedSpan = SourceClaimAcquisitionEngine.Execute(quotedInput,
            [baseline with { Proposals = [proposal with { Claim = quotedClaim }] }]).Scenarios.Single();
        Assert.AreEqual(SemanticProposalState.Extracted,
            exactQuotedSpan.Extraction.ClaimProposals.Single().ExtractionState);

        SourceClaimPassageInput unrelatedPassage = input.Passages.First(item => item.PassageId != proposal.PassageId);
        SourceClaimScenarioResult mismatchedCitation = SourceClaimAcquisitionEngine.Execute(input,
            [baseline with { Proposals = [proposal with { PassageId = unrelatedPassage.PassageId }] }]).Scenarios.Single();
        Assert.AreEqual(SemanticProposalState.Rejected,
            mismatchedCitation.Extraction.ClaimProposals.Single().ExtractionState);
        Assert.AreEqual("cited-passage-does-not-faithfully-ground-claim",
            mismatchedCitation.Extraction.ClaimProposals.Single().Reason);

        SourceClaimScenarioResult unknownCitation = SourceClaimAcquisitionEngine.Execute(input,
            [baseline with { Proposals = [proposal with { PassageId = "passage-outside-context" }] }]).Scenarios.Single();
        Assert.AreEqual(SemanticProposalState.Rejected,
            unknownCitation.Extraction.ClaimProposals.Single().ExtractionState);
        Assert.AreEqual(SemanticDecisionState.Rejected,
            unknownCitation.Extraction.AdmissionCorrelations.Single().DecisionState);
        Assert.AreEqual("citation-outside-minimized-context",
            unknownCitation.Extraction.ClaimProposals.Single().Reason);

        SourceClaimScenarioResult reversedMeaning = SourceClaimAcquisitionEngine.Execute(input,
            [baseline with { Proposals = [proposal with
            {
                Claim = "Feature Alder is not enabled when mode Copper is selected.",
            }] }]).Scenarios.Single();
        Assert.AreEqual(SemanticProposalState.Rejected,
            reversedMeaning.Extraction.ClaimProposals.Single().ExtractionState);
        Assert.AreEqual("cited-passage-does-not-faithfully-ground-claim",
            reversedMeaning.Extraction.ClaimProposals.Single().Reason);

        SourceClaimScenarioResult unknownWithContradiction = SourceClaimAcquisitionEngine.Execute(input,
            [baseline with
            {
                ContradictionEvidenceIds = ["opposing-evidence-external"],
                Proposals = [proposal with { State = "invented-state" }],
            }]).Scenarios.Single();
        Assert.AreEqual(SemanticProposalState.Rejected,
            unknownWithContradiction.Extraction.ClaimProposals.Single().ExtractionState);
        Assert.AreEqual("unknown-proposal-state",
            unknownWithContradiction.Extraction.ClaimProposals.Single().Reason);

        SourceClaimScenarioResult externallyIdentifiedContradiction = SourceClaimAcquisitionEngine.Execute(input,
            [baseline with { ContradictionEvidenceIds = ["opposing-evidence-external"] }]).Scenarios.Single();
        Assert.AreEqual(SemanticSupportState.Supported,
            externallyIdentifiedContradiction.Extraction.AdmissionCorrelations.Single().SupportState);
        Assert.AreEqual(SemanticApplicabilityState.NotEvaluated,
            externallyIdentifiedContradiction.Extraction.AdmissionCorrelations.Single().ApplicabilityState);
        Assert.AreEqual(SemanticDecisionState.Abstained,
            externallyIdentifiedContradiction.Extraction.AdmissionCorrelations.Single().DecisionState);
        CollectionAssert.AreEqual(new[] { new OpaqueId("opposing-evidence-external") },
            externallyIdentifiedContradiction.Extraction.ContradictionEvidenceIds.ToArray());
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Execute(input,
            [baseline with
            {
                ContradictionEvidenceIds = ["opposing-evidence-external"],
                Proposals =
                [
                    proposal,
                    proposal with { ProposalId = "second-unrelated-proposal" },
                ],
            }]));

        AssertGroundingRejectsOmission(
            "Feature Alder is enabled only when mode Copper is selected.",
            "Feature Alder is enabled when mode Copper is selected.");
        AssertGroundingRejectsOmission(
            "Feature Alder may be enabled when mode Copper is selected.",
            "Feature Alder is enabled when mode Copper is selected.");
        AssertGroundingRejectsOmission(
            "All Feature Alder relays are enabled when mode Copper is selected.",
            "Feature Alder relays are enabled when mode Copper is selected.");
        AssertGroundingRejectsOmission(
            "The guide explicitly rejects this claim: Feature Alder is enabled.",
            "Feature Alder is enabled.");

        void AssertGroundingRejectsOmission(string passageText, string claim)
        {
            SourceClaimPassageInput cited = input.Passages.Single(item => item.PassageId == proposal.PassageId) with
            {
                Text = passageText,
                TextSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(passageText))),
            };
            SourceClaimExecutionInput exactInput = input with
            {
                Passages = input.Passages.Select(item => item.PassageId == cited.PassageId ? cited : item).ToArray(),
            };
            SourceClaimScenarioResult result = SourceClaimAcquisitionEngine.Execute(exactInput,
                [baseline with { Proposals = [proposal with { Claim = claim }] }]).Scenarios.Single();
            Assert.AreEqual(SemanticProposalState.Rejected,
                result.Extraction.ClaimProposals.Single().ExtractionState, passageText);
            Assert.AreEqual("cited-passage-does-not-faithfully-ground-claim",
                result.Extraction.ClaimProposals.Single().Reason, passageText);
        }

        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Replay(
            input, baseline with { OperationId = "other-operation" }, baseline.ResponseFingerprint));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Replay(
            input, baseline with { SourceRevisionId = "other-source" }, baseline.ResponseFingerprint));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Replay(
            input, baseline with { PromptId = "other-prompt" }, baseline.ResponseFingerprint));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Execute(input,
            [baseline with { ModelUsed = false }]));
        Assert.ThrowsExactly<InvalidDataException>(() => SourceClaimAcquisitionEngine.Execute(input,
            [transcripts.Single(x => x.TranscriptId == "core-04") with
            {
                Proposals = [proposal],
            }]));

        (SourceClaimExecutionInput edgeInput, SourceClaimRetainedTranscript[] edgeRetained) =
            LoadTestCase("edge");
        SourceClaimRetainedTranscript[] edgeTranscripts = NormalizeGroundedClaims(edgeInput, edgeRetained);
        Assert.AreEqual("audit-only", SourceClaimAcquisitionEngine.Replay(edgeInput,
            edgeTranscripts.Single(x => x.TranscriptId == "edge-03"), new string('c', 64)).ReplayState);
        Assert.AreEqual("failed-identity-drift", SourceClaimAcquisitionEngine.Replay(edgeInput,
            edgeTranscripts.Single(x => x.TranscriptId == "edge-07"),
            edgeTranscripts.Single(x => x.TranscriptId == "edge-07").ResponseFingerprint).ReplayState);
        Assert.AreEqual("not-applicable", SourceClaimAcquisitionEngine.Replay(input,
            transcripts.Single(x => x.TranscriptId == "core-04"), new string('4', 64)).ReplayState);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ConditionlessSourceClaimsRequireAnExplicitAnalysisOwnedApplicationFact()
    {
        (SourceClaimExecutionInput input, SourceClaimRetainedTranscript[] retained) =
            LoadTestCase("core");
        SourceClaimRetainedTranscript transcript = NormalizeGroundedClaims(input, retained)
            .Single(item => item.TranscriptId == "core-01");
        SourceClaimScenarioResult source = SourceClaimAcquisitionEngine.Execute(input, [transcript])
            .Scenarios.Single();

        CitationProposalContract proposal = source.Extraction.ClaimProposals.Single() with { ConditionIds = [] };
        source = source with
        {
            Extraction = source.Extraction with { ClaimProposals = [proposal] },
        };
        SourceClaimApplicationDecisionContract absent = SourceClaimApplicationAdjudicator.Evaluate(
            input, source,
            [new(proposal.ProposalId.Value, "application-decision-empty", "application-validation-empty",
                "application-link-empty", "analysis-run", "root-subject", [])]).Decisions.Single();
        Assert.AreEqual(SemanticApplicabilityState.NotEvaluated,
            absent.DecisionLink.ApplicabilityState);
        Assert.AreEqual(SemanticSupportState.Supported, absent.DecisionLink.SupportState);
        Assert.AreEqual(SemanticDecisionState.Abstained, absent.DecisionLink.DecisionState);

        SourceClaimApplicationDecisionContract bound = SourceClaimApplicationAdjudicator.Evaluate(
            input, source,
            [new(proposal.ProposalId.Value, "application-decision-bound", "application-validation-bound",
                "application-link-bound", "analysis-run", "root-subject", ["local-fact-bound-to-proposal"])]
            ).Decisions.Single();
        Assert.AreEqual(SemanticApplicabilityState.Applicable,
            bound.DecisionLink.ApplicabilityState);
        Assert.AreEqual(SemanticDecisionState.Admitted, bound.DecisionLink.DecisionState);

        CitationProposalContract unsupportedProposal = proposal;
        SourceClaimAdmissionCorrelationContract unsupportedLink = source.Extraction
            .AdmissionCorrelations.Single() with
        {
            SupportState = SemanticSupportState.Unsupported,
            ApplicabilityState = SemanticApplicabilityState.NotEvaluated,
            DecisionState = SemanticDecisionState.Abstained,
        };
        SourceClaimScenarioResult unsupportedSource = source with
        {
            Extraction = source.Extraction with
            {
                AdmissionCorrelations = [unsupportedLink],
            },
        };
        SourceClaimApplicationDecisionContract unsupportedApplication = SourceClaimApplicationAdjudicator.Evaluate(
            input, unsupportedSource,
            [new(unsupportedProposal.ProposalId.Value, "application-decision-unsupported",
                "application-validation-unsupported", "application-link-unsupported", "analysis-run",
                "root-subject", ["local-fact-unsupported"])]).Decisions.Single();
        Assert.AreEqual(SemanticSupportState.Unsupported,
            unsupportedApplication.DecisionLink.SupportState);
        Assert.AreEqual(SemanticApplicabilityState.NotEvaluated,
            unsupportedApplication.DecisionLink.ApplicabilityState);
        Assert.AreEqual(SemanticDecisionState.Abstained,
            unsupportedApplication.DecisionLink.DecisionState);
        SourceClaimApplicationDecisionContract forgedSupport = absent with
        {
            DecisionLink = absent.DecisionLink with
            {
                SupportState = SemanticSupportState.Supported,
                DecisionState = SemanticDecisionState.Admitted,
            },
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.ValidateSourceClaimApplicationDecision(
                proposal, source.Extraction.AdmissionCorrelations.Single(), forgedSupport));
        SourceClaimApplicationDecisionContract reusedSourceIdentities = unsupportedApplication with
        {
            DecisionLink = unsupportedApplication.DecisionLink with
            {
                AdmissionId = unsupportedLink.AdmissionId,
                ValidationId = unsupportedLink.ValidationId,
                ApplicationLinkId = unsupportedLink.AdmissionCorrelationId,
            },
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.ValidateSourceClaimApplicationDecision(
                unsupportedProposal, unsupportedLink, reusedSourceIdentities));
    }

    private static (SourceClaimExecutionInput Input, SourceClaimRetainedTranscript[] Transcripts)
        LoadTestCase(string caseSet)
    {
        if (caseSet is not ("core" or "edge"))
        {
            throw new ArgumentOutOfRangeException(nameof(caseSet));
        }
        string root = RepositoryRoot();
        string directory = Path.Combine(root, "tests", "TestData", "Provider", "SourceClaims");
        JsonSerializerOptions options = SourceClaimContextMinimizer.JsonOptions;
        SourceClaimExecutionInput input = JsonSerializer.Deserialize<SourceClaimExecutionInput>(
            File.ReadAllBytes(Path.Combine(directory, caseSet + "-input.v1.json")), options)!;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(directory, caseSet + "-transcripts.v1.json")));
        SourceClaimRetainedTranscript[] transcripts = JsonSerializer.Deserialize<SourceClaimRetainedTranscript[]>(
            document.RootElement.GetProperty("transcripts"), options)!;
        return (input, transcripts);
    }

    private static SourceClaimRetainedTranscript[] NormalizeGroundedClaims(
        SourceClaimExecutionInput input,
        IReadOnlyList<SourceClaimRetainedTranscript> retainedTranscripts)
    {
        Dictionary<string, SourceClaimPassageInput> passages = input.Passages.ToDictionary(
            passage => passage.PassageId, StringComparer.Ordinal);
        return retainedTranscripts.Select(transcript => transcript with
        {
            Proposals = transcript.Proposals.Select(proposal => proposal.State is "proposed" or "unsupported"
                    && passages.TryGetValue(proposal.PassageId, out SourceClaimPassageInput? passage)
                    && !passage.Deleted
                ? proposal with { Claim = passage.Text }
                : proposal).ToArray(),
        }).ToArray();
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
