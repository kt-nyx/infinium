using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

[TestClass]
public sealed class SemanticAdmissionAuthorityEvaluationTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    public void SemanticAdmissionMatchesIndependentlyReviewedPublicAuthority()
    {
        string path = Path.Combine(RepositoryRoot(), "fixtures", "public", "provider", "semantic-admission",
            "S6-SEMANTIC-ADMISSION-VAL-v1", "oracle.v1.json");
        using JsonDocument oracle = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = oracle.RootElement;
        Assert.AreEqual("validation", Text(root, "partition"));
        Assert.IsFalse(root.GetProperty("frozen_boundaries").GetProperty("product_output_used").GetBoolean());
        Assert.IsFalse(root.GetProperty("frozen_boundaries").GetProperty("provider_call_required").GetBoolean());

        foreach (JsonElement item in root.GetProperty("source_cases").EnumerateArray())
        {
            VerifySource(item);
        }
        foreach (JsonElement item in root.GetProperty("candidate_cases").EnumerateArray())
        {
            VerifyCandidate(item);
        }
    }

    private static void VerifySource(JsonElement item)
    {
        string id = Text(item, "case_id");
        string[] conditions = Strings(item, "condition_ids");
        string[] applicable = Strings(item, "applicable_condition_ids");
        bool deleted = item.GetProperty("deleted").GetBoolean();
        string text = deleted ? string.Empty : "Synthetic public source statement for " + id + ".";
        string passageId = "passage-" + id;
        SourceClaimExecutionInput input = new("infinium.llm.source-claim-execution-input/v1", "1",
            "S6-SEMANTIC-ADMISSION-VAL-v1", "acquisition-" + id, "operation-" + id, "authorization-" + id,
            "evidence-acquisition-run", "acquisition-" + id, "run-" + id, "scope-" + id, "cost-" + id,
            "revision-" + id, "public semantic admission validation", SourceClaimPromptV1.Id,
            SourceClaimPromptV1.Fingerprint,
            [new(passageId, "revision-" + id, text, deleted ? new string('0', 64) : Hash(text), deleted)], applicable);
        SourceClaimRetainedTranscript transcript = new("transcript-" + id, input.OperationId, "response-" + id,
            "completed", new string('a', 64), input.SourceRevisionId, SourceClaimPromptV1.Id,
            SourceClaimPromptV1.Fingerprint,
            [new("proposal-" + id, passageId, text.Length == 0 ? "Deleted public source statement." : text,
                conditions, "documentation-claim", conditions.Length == 0 ? "unconditional" : "conditional",
                "informational", Text(item, "application_semantics"), Text(item, "raw_state"), "public oracle input")],
            item.GetProperty("contradiction").GetBoolean() ? [passageId] : [], [],
            deleted ? ["deleted-source"] : [], true);
        SourceClaimExtractionDocument actual = SourceClaimAcquisitionEngine.Execute(input, [transcript])
            .Scenarios.Single().Extraction;
        CitationProposalContract proposal = actual.ClaimProposals.Single();
        SourceClaimAdmissionCorrelationContract link = actual.AdmissionCorrelations.Single();
        Assert.AreEqual(Text(item, "expected_proposal_state"), Wire(proposal.ExtractionState), id);
        Assert.AreEqual(Text(item, "expected_support_state"), Wire(link.SupportState), id);
        Assert.AreEqual(Text(item, "expected_applicability_state"), Wire(link.ApplicabilityState), id);
        Assert.AreEqual(Text(item, "expected_decision_state"), Wire(link.DecisionState), id);
    }

    private static void VerifyCandidate(JsonElement item)
    {
        string id = Text(item, "case_id");
        bool supporting = item.GetProperty("supporting").GetBoolean();
        bool contradicting = item.GetProperty("contradicting").GetBoolean();
        CandidateEvidenceInput[] evidence =
        [
            new("evidence-main-" + id, "application-main-" + id, "acquisition-" + id,
                "admission-" + id, "source-link-" + id, "revision-" + id, "passage-" + id,
                supporting ? "supporting" : "neutral", "available", new string('b', 64)),
            .. contradicting
                ? [new CandidateEvidenceInput("evidence-contradiction-" + id, "application-contradiction-" + id,
                    "acquisition-contradiction-" + id, "admission-contradiction-" + id,
                    "source-link-contradiction-" + id, "revision-" + id, "passage-contradiction-" + id,
                    "contradicting", "available", new string('c', 64))]
                : Array.Empty<CandidateEvidenceInput>(),
        ];
        CandidateInvestigationContextInput context = new("context-" + id, "candidate-" + id,
            "hypothesis-" + id, "Synthetic bounded hypothesis " + id, ["participant-" + id], ["subject"],
            ["path-" + id], "closure-" + id, evidence);
        CandidateInvestigationContextInput unused = new("context-unused-" + id, "candidate-unused-" + id,
            "hypothesis-unused-" + id, "Unused bounded hypothesis", ["participant-unused-" + id], ["subject"],
            ["path-unused-" + id], "closure-unused-" + id,
            [new("evidence-unused-" + id, "application-unused-" + id, "acquisition-unused-" + id,
                "admission-unused-" + id, "source-link-unused-" + id, "revision-unused-" + id,
                "passage-unused-" + id, "neutral", "available", new string('d', 64))]);
        CandidateInvestigationExecutionInput input = new("infinium.llm.candidate-investigation-execution-input/v1", "1",
            "S6-SEMANTIC-ADMISSION-VAL-v1", "operation-candidate-" + id, "authorization-candidate-" + id,
            "analysis-run", "run-candidate-" + id, "run-candidate-" + id, "scope-candidate-" + id,
            "cost-candidate-" + id, CandidateInvestigationPromptV1.Id, CandidateInvestigationPromptV1.Fingerprint,
            [context, unused]);
        CandidateInvestigationTranscriptProposal proposal = new("proposal-candidate-" + id, context.CandidateId,
            context.HypothesisId, context.Hypothesis,
            supporting ? [evidence[0].EvidenceId] : [],
            contradicting ? [evidence[^1].EvidenceId] : [], [], "informational", Text(item, "raw_state"),
            "public oracle input");
        CandidateInvestigationRetainedTranscript transcript = new("transcript-candidate-" + id, input.OperationId,
            context.ContextId, "response-candidate-" + id, "completed", new string('e', 64),
            CandidateInvestigationPromptV1.Id, CandidateInvestigationPromptV1.Fingerprint, [proposal], [], [], true);
        CandidateInvestigationDocument actual = CandidateInvestigationEngine.Execute(input, [transcript])
            .Scenarios.Single().Investigation;
        HypothesisProposalContract retained = actual.HypothesisProposals.Single();
        ProviderSemanticAdmissionLinkContract link = actual.AdmissionLinks.Single();
        Assert.AreEqual(Text(item, "expected_proposal_state"), Wire(retained.ProposalState), id);
        Assert.AreEqual(Text(item, "expected_support_state"), Wire(link.SupportState), id);
        Assert.AreEqual(Text(item, "expected_applicability_state"), Wire(link.ApplicabilityState), id);
        Assert.AreEqual(Text(item, "expected_decision_state"), Wire(link.DecisionState), id);
    }

    private static string Text(JsonElement value, string property) => value.GetProperty(property).GetString()!;
    private static string[] Strings(JsonElement value, string property) => value.GetProperty(property)
        .EnumerateArray().Select(item => item.GetString()!).ToArray();
    private static string Wire<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
    private static string Hash(string value) => Convert.ToHexStringLower(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        { current = current.Parent; }
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
