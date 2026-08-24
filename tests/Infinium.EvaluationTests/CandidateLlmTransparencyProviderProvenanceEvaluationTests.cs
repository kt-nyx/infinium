using System.Text;
using Infinium.Application.Provider;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateLlmTransparencyProviderProvenanceEvaluationTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    public void CandidateLlmTransparencyExposesRawIntermediatesGapsAndClaimBoundaries()
    {
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = BuildCurrentContractExample();
        CandidateInvestigationResult result = CandidateInvestigationEngine.Execute(input, transcripts);
        string json = Encoding.UTF8.GetString(CandidateInvestigationTransparencyRenderer.RenderJson(result));
        string human = CandidateInvestigationTransparencyRenderer.RenderHuman(result);
        StringAssert.Contains(json, "raw_intermediate_ids");
        StringAssert.Contains(json, "source_acquisition_links");
        StringAssert.Contains(json, "evidence_provenance_links");
        StringAssert.Contains(json, "audit_reasons");
        StringAssert.Contains(json, "rejected-hostile-authority");
        StringAssert.Contains(json, "rejected-deleted-audit-only");
        StringAssert.Contains(json, "private_verdict\":\"not-performed");
        StringAssert.Contains(human, "no finding, case, taxonomy, readiness, reliability, or private-evaluation authority");
        Assert.IsFalse(json.Contains("held-out", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains("independently validated", StringComparison.OrdinalIgnoreCase));
    }

    private static (CandidateInvestigationExecutionInput, CandidateInvestigationRetainedTranscript[]) BuildCurrentContractExample()
    {
        const string hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        CandidateInvestigationContextInput[] contexts =
        [
            Context("context-hostile", "candidate-hostile", "hypothesis-hostile", "evidence-hostile", "available", hash),
            Context("context-deleted", "candidate-deleted", "hypothesis-deleted", "evidence-deleted", "deleted", hash),
        ];
        CandidateInvestigationExecutionInput input = new(
            "infinium.llm.candidate-investigation-execution-input/v1", "1", "developer-conformance",
            "operation-transparency", "host-authorization", "analysis-run", "analysis-run-transparency",
            "analysis-run-transparency", "application-scope", "cost-scope",
            CandidateInvestigationPromptV1.Id, CandidateInvestigationPromptV1.Fingerprint, contexts);
        CandidateInvestigationRetainedTranscript[] transcripts =
        [
            Transcript(contexts[0], "transcript-hostile", "response-hostile", "protected-effect-request", hash),
            Transcript(contexts[1], "transcript-deleted", "response-deleted", "informational", hash),
        ];
        return (input, transcripts);

        static CandidateInvestigationContextInput Context(
            string contextId, string candidateId, string hypothesisId, string evidenceId, string availability, string hash) =>
            new(contextId, candidateId, hypothesisId, "bounded hypothesis", ["participant"], ["subject"],
                ["causal-path"], "dependency-closure",
                [new CandidateEvidenceInput(evidenceId, "application-" + evidenceId, "acquisition-" + evidenceId,
                    "admission-" + evidenceId, "source-application-" + evidenceId,
                    "source-revision", "passage", "supporting", availability, hash)
                { SourceApplicationDecisionId = "application-decision-" + evidenceId }]);

        static CandidateInvestigationRetainedTranscript Transcript(
            CandidateInvestigationContextInput context, string transcriptId, string responseId,
            string authorityCategory, string hash) => new(
                transcriptId, "operation-transparency", context.ContextId, responseId, "completed", hash,
                CandidateInvestigationPromptV1.Id, CandidateInvestigationPromptV1.Fingerprint,
                [new("proposal-" + context.ContextId, context.CandidateId, context.HypothesisId,
                    context.Hypothesis, [context.Evidence[0].EvidenceId], [], [], authorityCategory,
                    "proposed", "developer-owned transparency example")], [], [], true);
    }
}
