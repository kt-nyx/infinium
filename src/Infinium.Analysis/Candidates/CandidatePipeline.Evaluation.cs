using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.Candidates;

public static partial class CandidatePipeline
{
    private static CandidateMemberOutcome Evaluate(
        CandidatePipelineRequest request,
        CausalJoinPopulationMember member,
        CandidateDecisionDisposition? forced)
    {
        ValidateMember(member);
        OpaqueId closureId = CandidateAnalysisIdentity.StableId(
            "candidate-closure",
            member.DependencyIds.Select(item => item.Value).Prepend(member.PopulationMemberId.Value).ToArray());
        CandidateDecisionDisposition disposition = forced ?? member.InputState switch
        {
            CausalJoinInputState.Complete => CandidateDecisionDisposition.CandidateAdmitted,
            CausalJoinInputState.Ambiguous => CandidateDecisionDisposition.Ambiguous,
            CausalJoinInputState.ResolvedNegative => CandidateDecisionDisposition.ResolvedNegative,
            CausalJoinInputState.Unsupported => CandidateDecisionDisposition.Unsupported,
            CausalJoinInputState.InvalidInput => CandidateDecisionDisposition.InvalidInput,
            CausalJoinInputState.Deferred => CandidateDecisionDisposition.Deferred,
            CausalJoinInputState.Failed => CandidateDecisionDisposition.Failed,
            _ => throw new InvalidOperationException("Causal join input state is not closed."),
        };
        OpaqueId decisionId = CandidateAnalysisIdentity.StableId(
            "candidate-decision", request.OriginatingRunId.Value, request.PopulationId.Value,
            member.PopulationMemberId.Value,
            request.PolicyId.Value, request.PolicyFingerprint.Value,
            request.ThresholdId.Value, request.ThresholdFingerprint.Value,
            request.Limits.SemanticsFingerprint.Value,
            member.InputFingerprint.Value, disposition.ToString());
        CandidateLane decisionLane = member.Lane;
        CandidateDecisionContract decision = new(
            decisionId,
            member.PopulationMemberId,
            member.SourceFactId,
            decisionLane,
            disposition,
            member.Participants,
            member.JoinKind,
            member.Path,
            closureId,
            member.Rationale,
            member.SupportingEvidenceIds,
            decisionLane is CandidateLane.DeterministicRequired or CandidateLane.MandatoryEvidence,
            decisionLane == CandidateLane.OptionalRanked ? member.OptionalRank ?? 1 : null)
        {
            AnalyzerId = member.AnalyzerId,
            PolicyId = request.PolicyId,
            ThresholdId = request.ThresholdId,
            LimitId = request.Limits.LimitId,
            DependencyIds = member.DependencyIds,
        };

        CandidateAnalysisEntryContract? candidate = null;
        CandidateHypothesisContract? hypothesis = null;
        CandidateAbstentionContract? abstention = null;
        CandidateGapContract? gap = null;
        CandidateFailureContract? failure = null;
        if (disposition is CandidateDecisionDisposition.CandidateAdmitted or CandidateDecisionDisposition.Ambiguous)
        {
            OpaqueId candidateId = CandidateAnalysisIdentity.StableId("candidate", decisionId.Value, closureId.Value);
            bool mustAbstain = member.MissingInformation.Count != 0;
            AnalysisResultState candidateState = mustAbstain
                ? AnalysisResultState.Abstained
                : member.ContradictingEvidenceIds.Count != 0 || member.InputState == CausalJoinInputState.Ambiguous
                    ? AnalysisResultState.Ambiguous
                    : AnalysisResultState.Present;
            OpaqueId hypothesisId = CandidateAnalysisIdentity.StableId("hypothesis", candidateId.Value, request.ThresholdId.Value);
            OpaqueId? abstentionId = mustAbstain ? CandidateAnalysisIdentity.StableId("candidate-abstention", candidateId.Value, request.ThresholdId.Value) : null;
            candidate = new CandidateAnalysisEntryContract(
                candidateId, decisionId, candidateState, member.Rationale,
                member.SupportingEvidenceIds, member.ContradictingEvidenceIds, member.MissingInformation,
                candidateState == AnalysisResultState.Present ? AnalysisConfidence.Plausible : AnalysisConfidence.SpeculativeLead,
                request.ThresholdId)
            {
                HypothesisId = hypothesisId,
                AbstentionId = abstentionId,
            };
            hypothesis = new CandidateHypothesisContract(
                hypothesisId, candidateId, mustAbstain ? AnalysisResultState.Partial : candidateState,
                member.Rationale,
                member.PredictedImpact,
                member.SupportingEvidenceIds, member.ContradictingEvidenceIds, member.MissingInformation,
                candidate.Confidence, request.ThresholdId);
            if (abstentionId is not null)
            {
                abstention = new CandidateAbstentionContract(
                    abstentionId, decisionId, candidateId, member.AnalyzerId,
                    "Required information is missing; candidate retained without a hypothesis conclusion.",
                    member.MissingInformation);
            }
            if (member.EmitGap)
            {
                gap = new CandidateGapContract(
                    CandidateAnalysisIdentity.StableId("candidate-gap", decisionId.Value, "missing-information"),
                    decisionId, request.PopulationId, AnalysisResultState.Missing,
                    "The candidate remains admitted, but a required causal witness is missing.",
                    member.MissingInformation[0]);
            }
        }
        else if (disposition is CandidateDecisionDisposition.Unsupported
            or CandidateDecisionDisposition.Limited
            or CandidateDecisionDisposition.Unprocessed
            or CandidateDecisionDisposition.Deferred)
        {
            AnalysisResultState state = disposition switch
            {
                CandidateDecisionDisposition.Unsupported => AnalysisResultState.Unsupported,
                CandidateDecisionDisposition.Limited or CandidateDecisionDisposition.Unprocessed => AnalysisResultState.LimitReached,
                _ => AnalysisResultState.Partial,
            };
            gap = new CandidateGapContract(
                CandidateAnalysisIdentity.StableId("candidate-gap", decisionId.Value, disposition.ToString()),
                decisionId, request.PopulationId, state,
                disposition switch
                {
                    CandidateDecisionDisposition.Unsupported => "The delivered substrate does not support this semantic shape.",
                    CandidateDecisionDisposition.Limited => "The optional candidate limit was reached.",
                    CandidateDecisionDisposition.Unprocessed => "The bounded population-work limit was reached.",
                    _ => "Work was explicitly deferred by the closed input state.",
                },
                member.MissingInformation.Count == 0 ? disposition.ToString() : member.MissingInformation[0]);
            if (disposition == CandidateDecisionDisposition.Unsupported)
            {
                abstention = new CandidateAbstentionContract(
                    CandidateAnalysisIdentity.StableId("candidate-abstention", decisionId.Value, "unsupported"),
                    decisionId, null, member.AnalyzerId,
                    "The delivered substrate does not support a required causal input; no candidate conclusion is asserted.",
                    member.MissingInformation.Count == 0
                        ? ["supported delivered substrate for this causal population"]
                        : member.MissingInformation);
            }
        }
        else if (disposition == CandidateDecisionDisposition.Failed)
        {
            failure = new CandidateFailureContract(
                CandidateAnalysisIdentity.StableId("candidate-failure", decisionId.Value, member.FailureCode ?? "failed"),
                member.AnalyzerId, [member.PopulationMemberId], member.FailureCode ?? "candidate-analysis-failed",
                Bound(member.FailureMessage ?? "Candidate analyzer failed.", 512), true);
            if (member.EmitGap)
            {
                gap = new CandidateGapContract(
                    CandidateAnalysisIdentity.StableId("candidate-gap", decisionId.Value, "analyzer-failure"),
                    decisionId, request.PopulationId, AnalysisResultState.Failed,
                    "The analyzer failed for this population member; unrelated analyzers remain independent.",
                    member.FailureCode ?? "candidate-analysis-failed");
            }
        }
        return new CandidateMemberOutcome(member.InputFingerprint, decision, candidate, hypothesis, abstention, gap, failure);
    }

    private static CandidateAnalysisContract Assemble(
        CandidatePipelineRequest request,
        IEnumerable<CandidateMemberOutcome> unordered)
    {
        CandidateMemberOutcome[] outcomes = unordered.OrderBy(item => item.Decision.PopulationMemberId.Value, StringComparer.Ordinal).ToArray();
        CandidateDecisionContract[] decisions = outcomes.Select(item => item.Decision).ToArray();
        CandidateAnalysisEntryContract[] candidates = outcomes.Where(item => item.Candidate is not null).Select(item => item.Candidate!).ToArray();
        CandidateHypothesisContract[] hypotheses = outcomes.Where(item => item.Hypothesis is not null).Select(item => item.Hypothesis!).ToArray();
        CandidateAbstentionContract[] abstentions = outcomes.Where(item => item.Abstention is not null).Select(item => item.Abstention!).ToArray();
        CandidateGapContract[] gaps = outcomes.Where(item => item.Gap is not null).Select(item => item.Gap!).ToArray();
        CandidateFailureContract[] failures = outcomes.Where(item => item.Failure is not null).Select(item => item.Failure!).ToArray();
        CandidateAnalyzerBindingContract[] analyzerBindings = request.Sources
            .OrderBy(item => item.AnalyzerId.Value, StringComparer.Ordinal)
            .Select(source =>
            {
                string declarationJson = JsonSerializer.Serialize(source.Declaration);
                return new CandidateAnalyzerBindingContract(
                    source.AnalyzerId,
                    source.Declaration.AnalyzerVersion,
                    source.Declaration.SemanticContractVersion,
                    source.Declaration.IdentityContractVersion,
                    source.Declaration.RulesetVersion,
                    CandidateAnalysisIdentity.StructuralHash([declarationJson]),
                    declarationJson)
                {
                    AnalyzerFamily = source.Declaration.AnalyzerFamily,
                };
            })
            .ToArray();
        Sha256Fingerprint analyzerSetFingerprint = CandidateAnalysisIdentity.StructuralHash(
            analyzerBindings.Select(item => $"{item.AnalyzerId.Value}:{item.DeclarationFingerprint.Value}"));
        OpaqueId analysisRootId = CandidateAnalysisIdentity.StableId(
            "candidate-analysis-root", request.OriginatingRunId.Value, request.PopulationId.Value,
            request.ExecutionInputFingerprint.Value, request.PolicyFingerprint.Value,
            request.ThresholdFingerprint.Value, request.Limits.SemanticsFingerprint.Value, analyzerSetFingerprint.Value);
        List<CandidateDependencyEdgeContract> edges = [];
        edges.Add(Edge("candidate-analysis-root", analysisRootId, "execution-input-binding",
            CandidateAnalysisIdentity.StableId("candidate-execution-input-binding", request.ExecutionInputId.Value, request.ExecutionInputFingerprint.Value), "uses"));
        edges.Add(Edge("candidate-analysis-root", analysisRootId, "policy-binding",
            CandidateAnalysisIdentity.StableId("candidate-policy-binding", request.PolicyId.Value, request.PolicyFingerprint.Value), "uses"));
        edges.Add(Edge("candidate-analysis-root", analysisRootId, "threshold-binding",
            CandidateAnalysisIdentity.StableId("candidate-threshold-binding", request.ThresholdId.Value, request.ThresholdFingerprint.Value), "uses"));
        edges.Add(Edge("candidate-analysis-root", analysisRootId, "limit-binding",
            CandidateAnalysisIdentity.StableId("candidate-limit-binding", request.Limits.LimitId.Value, request.Limits.SemanticsFingerprint.Value), "uses"));
        foreach (CandidateAnalyzerBindingContract analyzerBinding in analyzerBindings)
        {
            edges.Add(Edge("candidate-analysis-root", analysisRootId, "analyzer-declaration-binding",
                CandidateAnalysisIdentity.StableId("candidate-analyzer-binding", analyzerBinding.AnalyzerId.Value,
                    analyzerBinding.AnalyzerVersion.ToString(), analyzerBinding.SemanticContractVersion.ToString(),
                    analyzerBinding.IdentityContractVersion.ToString(), analyzerBinding.RulesetVersion.ToString(),
                    analyzerBinding.DeclarationFingerprint.Value), "uses"));
        }
        foreach (CandidateMemberOutcome outcome in outcomes)
        {
            CandidateDecisionContract decision = outcome.Decision;
            edges.Add(Edge("candidate-decision", decision.DecisionId, "source-fact", decision.SourceFactId, "derived-from"));
            edges.Add(Edge("candidate-decision", decision.DecisionId, "dependency-closure", decision.DependencyClosureId, "depends-on"));
            foreach (OpaqueId dependencyId in decision.DependencyIds)
            {
                edges.Add(Edge("dependency-closure", decision.DependencyClosureId, "dependency", dependencyId, "depends-on"));
            }
            foreach (OpaqueId evidenceId in decision.EvidenceIds)
            {
                edges.Add(Edge("candidate-decision", decision.DecisionId, "evidence", evidenceId, "derived-from"));
            }
            if (outcome.Candidate is { } candidate)
            {
                edges.Add(Edge("candidate", candidate.CandidateId, "candidate-decision", decision.DecisionId, "derived-from"));
                foreach (OpaqueId evidenceId in candidate.SupportingEvidenceIds)
                {
                    edges.Add(Edge("candidate", candidate.CandidateId, "evidence", evidenceId, "supports"));
                }
                foreach (OpaqueId evidenceId in candidate.ContradictingEvidenceIds)
                {
                    edges.Add(Edge("candidate", candidate.CandidateId, "evidence", evidenceId, "contradicts"));
                }
            }
            if (outcome.Hypothesis is { } hypothesis)
            {
                edges.Add(Edge("hypothesis", hypothesis.HypothesisId, "candidate", hypothesis.CandidateId, "derived-from"));
                foreach (OpaqueId evidenceId in hypothesis.SupportingEvidenceIds)
                {
                    edges.Add(Edge("hypothesis", hypothesis.HypothesisId, "evidence", evidenceId, "supports"));
                }
                foreach (OpaqueId evidenceId in hypothesis.ContradictingEvidenceIds)
                {
                    edges.Add(Edge("hypothesis", hypothesis.HypothesisId, "evidence", evidenceId, "contradicts"));
                }
            }
            if (outcome.Abstention is { } abstention)
            {
                edges.Add(Edge("abstention", abstention.AbstentionId, "candidate-decision", decision.DecisionId, "derived-from"));
            }
            if (outcome.Gap is { } gap)
            {
                edges.Add(Edge("gap", gap.GapId, "candidate-decision", decision.DecisionId, "derived-from"));
            }
            if (outcome.Failure is { } failure)
            {
                edges.Add(Edge("failure", failure.FailureId, "candidate-decision", decision.DecisionId, "derived-from"));
            }
        }
        CandidateAnalysisContract result = new(
            ContractConstants.CandidateAnalysisSchemaId, new ContractVersion(1, 0, 0), new OpaqueId("candidate-analysis-pending"),
            request.OriginatingRunId,
            request.Sources.Count == 1 ? request.Sources[0].AnalyzerId : new OpaqueId("candidate-analyzers-composite"),
            request.PopulationId, decisions.Length, decisions, candidates, abstentions, gaps, failures)
        {
            PolicyId = request.PolicyId,
            ThresholdId = request.ThresholdId,
            LimitId = request.Limits.LimitId,
            ExecutionInputId = request.ExecutionInputId,
            AnalysisRootId = analysisRootId,
            DeliveredInputId = request.Context.AdmittedDeliveredInputId
                ?? new OpaqueId("candidate-delivered-input-unspecified"),
            ExecutionInputFingerprint = request.ExecutionInputFingerprint,
            PolicyFingerprint = request.PolicyFingerprint,
            ThresholdFingerprint = request.ThresholdFingerprint,
            LimitFingerprint = request.Limits.SemanticsFingerprint,
            AnalyzerSetFingerprint = analyzerSetFingerprint,
            AnalyzerBindings = analyzerBindings,
            ExecutionInputDescriptors = request.ExecutionInputDescriptors,
            PolicyDescriptors = request.PolicyDescriptors,
            ThresholdDescriptors = request.ThresholdDescriptors,
            LimitDescriptors = request.Limits.SemanticsDescriptors,
            Hypotheses = hypotheses,
            DependencyEdges = edges.OrderBy(item => item.EdgeId.Value, StringComparer.Ordinal).ToArray(),
        };
        result = result with { Counts = CandidateAnalysisCounts.Compute(result) };
        result = result with { PayloadId = CandidateAnalysisIdentity.ComputePayloadId(result) };
        CandidateAnalysisContractInvariants.Validate(result);
        return result;
    }

}
