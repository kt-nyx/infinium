using static Infinium.Domain.Contracts.AnalysisContractInvariantHelpers;

namespace Infinium.Domain.Contracts;

public static class CandidateAnalysisContractInvariants
{
    public static void Validate(CandidateAnalysisContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.CandidateAnalysisSchemaId);
        ArgumentNullException.ThrowIfNull(value.Counts);
        if (value.PopulationDenominator < 0 || value.Decisions.Count != value.PopulationDenominator)
        {
            throw new InvalidOperationException("Every candidate population member requires exactly one eligible decision.");
        }
        RequireUnique(value.Decisions.Select(item => item.DecisionId), "candidate decisions");
        RequireUnique(value.Decisions.Select(item => item.PopulationMemberId), "candidate population members");
        RequireUnique(value.Candidates.Select(item => item.CandidateId), "candidates");
        RequireUnique(value.Candidates.Select(item => item.DecisionId), "candidate decision references");
        RequireUnique(value.Hypotheses.Select(item => item.HypothesisId), "candidate hypotheses");
        RequireUnique(value.Hypotheses.Select(item => item.CandidateId), "hypothesis candidate references");
        RequireUnique(value.Abstentions.Select(item => item.AbstentionId), "candidate abstentions");
        RequireUnique(value.Gaps.Select(item => item.GapId), "candidate gaps");
        RequireUnique(value.Gaps.Select(item => item.DecisionId), "candidate gap decision references");
        RequireUnique(value.Failures.Select(item => item.FailureId), "candidate failures");
        RequireUnique(value.DependencyEdges.Select(item => item.EdgeId), "candidate dependency edges");
        RequireUnique(value.AnalyzerBindings.Select(item => item.AnalyzerId), "candidate analyzer bindings");
        HashSet<OpaqueId> boundAnalyzers = value.AnalyzerBindings.Select(item => item.AnalyzerId).ToHashSet();
        Sha256Fingerprint expectedAnalyzerSetFingerprint = CandidateAnalysisIdentity.StructuralHash(
            value.AnalyzerBindings.Select(item =>
                $"{item.AnalyzerId.Value}:{item.DeclarationFingerprint.Value}"));
        bool invalidDescriptors = new[]
            {
                value.ExecutionInputDescriptors,
                value.PolicyDescriptors,
                value.ThresholdDescriptors,
                value.LimitDescriptors,
            }
            .Any(items => items.Count is 0 or > 512
                || items.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 4096));
        if (value.AnalyzerBindings.Count == 0
            || value.AnalysisRootId != CandidateAnalysisIdentity.StableId(
                "candidate-analysis-root", value.OriginatingRunId.Value, value.PopulationId.Value,
                value.ExecutionInputFingerprint.Value, value.PolicyFingerprint.Value,
                value.ThresholdFingerprint.Value, value.LimitFingerprint.Value, value.AnalyzerSetFingerprint.Value)
            || value.Decisions.Any(item => !boundAnalyzers.Contains(item.AnalyzerId))
            || value.AnalyzerSetFingerprint != expectedAnalyzerSetFingerprint
            || value.AnalyzerBindings.Any(item => string.IsNullOrWhiteSpace(item.AnalyzerFamily)
                || string.IsNullOrWhiteSpace(item.CanonicalDeclarationJson)
                || item.CanonicalDeclarationJson.Length > 65536
                || item.DeclarationFingerprint != CandidateAnalysisIdentity.StructuralHash([item.CanonicalDeclarationJson]))
            || invalidDescriptors
            || value.ExecutionInputFingerprint != CandidateAnalysisIdentity.StructuralHash(value.ExecutionInputDescriptors)
            || value.PolicyFingerprint != CandidateAnalysisIdentity.StructuralHash(value.PolicyDescriptors)
            || value.ThresholdFingerprint != CandidateAnalysisIdentity.StructuralHash(value.ThresholdDescriptors)
            || value.LimitFingerprint != CandidateAnalysisIdentity.StructuralHash(value.LimitDescriptors))
        {
            throw new InvalidOperationException("Candidate analysis requires exact execution, analyzer, policy, threshold, and limit semantic bindings.");
        }
        if (!StringComparer.Ordinal.Equals(value.DeliveredInputId.Value, "candidate-delivered-input-unspecified")
            && value.Decisions.Any(item => !item.DependencyIds.Contains(value.DeliveredInputId)))
        {
            throw new InvalidOperationException(
                "Every delivered candidate decision must bind the exact admitted delivered-input root.");
        }
        Dictionary<OpaqueId, CandidateDecisionContract> decisions = value.Decisions.ToDictionary(item => item.DecisionId);
        foreach (CandidateDecisionContract decision in value.Decisions)
        {
            if (decision.Lane == CandidateLane.Unspecified
                || decision.Disposition == CandidateDecisionDisposition.Unspecified
                || decision.Disposition == CandidateDecisionDisposition.Abstained
                || StringComparer.Ordinal.Equals(decision.SourceFactId.Value, "source-fact-unspecified")
                || decision.Participants.Count > 16
                || (decision.Disposition is not (CandidateDecisionDisposition.InvalidInput or CandidateDecisionDisposition.Failed)
                    && decision.Participants.Count < 2)
                || decision.Participants.Select(item => item.Role).Distinct(StringComparer.Ordinal).Count() != decision.Participants.Count
                || decision.Participants.Any(item => string.IsNullOrWhiteSpace(item.Role)
                    || item.Role.Length > 128
                    || !IsAsciiToken(item.Role))
                || string.IsNullOrWhiteSpace(decision.JoinKind)
                || decision.JoinKind.Length > 128
                || !IsAsciiToken(decision.JoinKind)
                || decision.Path.Count > 64
                || (decision.Disposition is not (CandidateDecisionDisposition.InvalidInput or CandidateDecisionDisposition.Failed)
                    && decision.Path.Count == 0)
                || (decision.Disposition is not (CandidateDecisionDisposition.InvalidInput or CandidateDecisionDisposition.Failed)
                    && decision.Participants.Any(item => !decision.Path.Contains(item.ParticipantId)))
                || decision.EvidenceIds.Count > 128
                || (decision.Disposition is not (CandidateDecisionDisposition.InvalidInput or CandidateDecisionDisposition.Failed)
                    && decision.EvidenceIds.Count == 0)
                || decision.EvidenceIds.Distinct().Count() != decision.EvidenceIds.Count
                || decision.DependencyIds.Count > 128
                || (decision.Disposition is not (CandidateDecisionDisposition.InvalidInput or CandidateDecisionDisposition.Failed)
                    && decision.DependencyIds.Count == 0)
                || decision.DependencyIds.Distinct().Count() != decision.DependencyIds.Count
                || decision.DependencyClosureId != CandidateAnalysisIdentity.StableId(
                    "candidate-closure",
                    decision.DependencyIds.Select(item => item.Value).Prepend(decision.PopulationMemberId.Value).ToArray())
                || decision.PolicyId != value.PolicyId
                || decision.ThresholdId != value.ThresholdId
                || decision.LimitId != value.LimitId
                || string.IsNullOrWhiteSpace(decision.Rationale)
                || decision.Rationale.Length > 4096
                || decision.AdmissionIndependentOfScore !=
                    (decision.Lane is CandidateLane.DeterministicRequired or CandidateLane.MandatoryEvidence)
                || (decision.Disposition == CandidateDecisionDisposition.Limited
                    && decision.Lane != CandidateLane.OptionalRanked)
                || (decision.Lane != CandidateLane.OptionalRanked && decision.OptionalRank is not null)
                || (decision.Lane == CandidateLane.OptionalRanked && decision.OptionalRank is null or <= 0))
            {
                throw new InvalidOperationException("Candidate decisions require closed lane/disposition, canonical roles, and score-independent mandatory admission.");
            }
        }
        HashSet<OpaqueId> admittedDecisionIds = value.Decisions
            .Where(item => item.Disposition is CandidateDecisionDisposition.CandidateAdmitted
                or CandidateDecisionDisposition.Ambiguous)
            .Select(item => item.DecisionId)
            .ToHashSet();
        HashSet<OpaqueId> candidateDecisionIds = value.Candidates.Select(item => item.DecisionId).ToHashSet();
        if (!admittedDecisionIds.SetEquals(candidateDecisionIds))
        {
            throw new InvalidOperationException("Every admitted decision requires exactly one candidate, and no other decision may own a candidate.");
        }
        foreach (CandidateAnalysisEntryContract candidate in value.Candidates)
        {
            if (!decisions.TryGetValue(candidate.DecisionId, out CandidateDecisionContract? decision)
                || decision.Disposition is not (CandidateDecisionDisposition.CandidateAdmitted
                    or CandidateDecisionDisposition.Ambiguous)
                || candidate.State is not (AnalysisResultState.Present or AnalysisResultState.Ambiguous or AnalysisResultState.Abstained)
                || candidate.Confidence == AnalysisConfidence.Unspecified
                || candidate.ThresholdId != value.ThresholdId
                || string.IsNullOrWhiteSpace(candidate.CausalExplanation)
                || candidate.CausalExplanation.Length > 4096
                || candidate.SupportingEvidenceIds.Count > 128
                || candidate.ContradictingEvidenceIds.Count > 128
                || candidate.MissingInformation.Count > 32
                || candidate.MissingInformation.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 1024)
                || candidate.SupportingEvidenceIds.Distinct().Count() != candidate.SupportingEvidenceIds.Count
                || candidate.ContradictingEvidenceIds.Distinct().Count() != candidate.ContradictingEvidenceIds.Count
                || !candidate.SupportingEvidenceIds.ToHashSet().SetEquals(decision.EvidenceIds)
                || (candidate.HypothesisId is not null) != value.Hypotheses.Any(item => item.CandidateId == candidate.CandidateId)
                || candidate.HypothesisId is null
                || (candidate.AbstentionId is not null) != value.Abstentions.Any(item => item.CandidateId == candidate.CandidateId)
                || (candidate.AbstentionId is not null) != (candidate.MissingInformation.Count != 0)
                || (candidate.State == AnalysisResultState.Abstained) != (candidate.MissingInformation.Count != 0)
                || (candidate.State == AnalysisResultState.Ambiguous) != (candidate.MissingInformation.Count == 0
                    && candidate.ContradictingEvidenceIds.Count != 0)
                || (candidate.State == AnalysisResultState.Present) != (candidate.MissingInformation.Count == 0
                    && candidate.ContradictingEvidenceIds.Count == 0))
            {
                throw new InvalidOperationException("Candidates require one admitted decision and explicit, closed hypothesis/abstention linkage.");
            }
        }
        Dictionary<OpaqueId, CandidateAnalysisEntryContract> candidates = value.Candidates.ToDictionary(item => item.CandidateId);
        foreach (CandidateHypothesisContract hypothesis in value.Hypotheses)
        {
            if (!candidates.TryGetValue(hypothesis.CandidateId, out CandidateAnalysisEntryContract? candidate)
                || candidate.HypothesisId != hypothesis.HypothesisId
                || hypothesis.State is not (AnalysisResultState.Present or AnalysisResultState.Ambiguous or AnalysisResultState.Partial)
                || hypothesis.Confidence == AnalysisConfidence.Unspecified
                || hypothesis.ThresholdId != value.ThresholdId
                || string.IsNullOrWhiteSpace(hypothesis.ProposedExplanation)
                || hypothesis.ProposedExplanation.Length > 4096
                || string.IsNullOrWhiteSpace(hypothesis.PredictedImpact)
                || hypothesis.PredictedImpact.Length > 4096
                || hypothesis.SupportingEvidenceIds.Count > 128
                || hypothesis.ContradictingEvidenceIds.Count > 128
                || hypothesis.MissingInformation.Count > 32
                || !hypothesis.SupportingEvidenceIds.ToHashSet().SetEquals(candidate.SupportingEvidenceIds)
                || !hypothesis.ContradictingEvidenceIds.ToHashSet().SetEquals(candidate.ContradictingEvidenceIds)
                || !hypothesis.MissingInformation.SequenceEqual(candidate.MissingInformation, StringComparer.Ordinal)
                || (candidate.State == AnalysisResultState.Abstained
                    ? hypothesis.State != AnalysisResultState.Partial
                    : hypothesis.State != candidate.State))
            {
                throw new InvalidOperationException("Every hypothesis requires one linked candidate and closed evidence-bound state.");
            }
        }
        foreach (CandidateAbstentionContract abstention in value.Abstentions)
        {
            if (!decisions.TryGetValue(abstention.DecisionId, out CandidateDecisionContract? abstentionDecision)
                || abstention.AnalyzerId != abstentionDecision.AnalyzerId
                || (abstention.CandidateId is not null
                    && (!candidates.TryGetValue(abstention.CandidateId, out CandidateAnalysisEntryContract? candidate)
                        || candidate.DecisionId != abstention.DecisionId
                        || candidate.AbstentionId != abstention.AbstentionId
                        || !abstention.RequiredInformation.SequenceEqual(candidate.MissingInformation, StringComparer.Ordinal)))
                || (abstention.CandidateId is null
                    && abstentionDecision.Disposition is not (CandidateDecisionDisposition.Abstained
                        or CandidateDecisionDisposition.Unsupported))
                || string.IsNullOrWhiteSpace(abstention.Reason)
                || abstention.Reason.Length > 4096
                || abstention.RequiredInformation.Count is 0 or > 32
                || abstention.RequiredInformation.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 1024))
            {
                throw new InvalidOperationException("Abstentions require a decision, optional linked candidate, reason, and required information.");
            }
        }
        HashSet<OpaqueId> unsupportedDecisionIds = value.Decisions
            .Where(item => item.Disposition == CandidateDecisionDisposition.Unsupported)
            .Select(item => item.DecisionId)
            .ToHashSet();
        OpaqueId[] unsupportedAbstentionDecisionIds = value.Abstentions
            .Where(item => item.CandidateId is null)
            .Select(item => item.DecisionId)
            .ToArray();
        if (unsupportedAbstentionDecisionIds.Distinct().Count() != unsupportedAbstentionDecisionIds.Length
            || !unsupportedDecisionIds.SetEquals(unsupportedAbstentionDecisionIds))
        {
            throw new InvalidOperationException("Unsupported decisions and candidate-less abstentions must correspond exactly.");
        }
        foreach (CandidateGapContract gap in value.Gaps)
        {
            if (!decisions.TryGetValue(gap.DecisionId, out CandidateDecisionContract? gapDecision)
                || gap.PopulationId != value.PopulationId
                || gap.State is AnalysisResultState.Unspecified or AnalysisResultState.Present
                || (gapDecision.Disposition switch
                {
                    CandidateDecisionDisposition.CandidateAdmitted or CandidateDecisionDisposition.Ambiguous => gap.State != AnalysisResultState.Missing,
                    CandidateDecisionDisposition.Unsupported => gap.State != AnalysisResultState.Unsupported,
                    CandidateDecisionDisposition.Limited or CandidateDecisionDisposition.Unprocessed => gap.State != AnalysisResultState.LimitReached,
                    CandidateDecisionDisposition.Deferred => gap.State != AnalysisResultState.Partial,
                    CandidateDecisionDisposition.Failed => gap.State != AnalysisResultState.Failed,
                    _ => true,
                })
                || (gapDecision.Disposition is CandidateDecisionDisposition.CandidateAdmitted
                        or CandidateDecisionDisposition.Ambiguous
                    && !value.Candidates.Any(candidate =>
                        candidate.DecisionId == gap.DecisionId
                        && candidate.State == AnalysisResultState.Abstained
                        && candidate.MissingInformation.Contains(
                            gap.MissingCapabilityOrInformation,
                            StringComparer.Ordinal)))
                || string.IsNullOrWhiteSpace(gap.Reason)
                || gap.Reason.Length > 4096
                || string.IsNullOrWhiteSpace(gap.MissingCapabilityOrInformation)
                || gap.MissingCapabilityOrInformation.Length > 1024)
            {
                throw new InvalidOperationException("Candidate gaps require a closed non-present state and a population decision.");
            }
        }
        foreach (CandidateFailureContract failure in value.Failures)
        {
            if (failure.PopulationMemberIds.Count is 0 or > 1024
                || failure.PopulationMemberIds.Any(id => value.Decisions.All(decision => decision.PopulationMemberId != id))
                || failure.PopulationMemberIds.Any(id => value.Decisions.Single(decision => decision.PopulationMemberId == id).AnalyzerId != failure.AnalyzerId)
                || string.IsNullOrWhiteSpace(failure.FailureCode)
                || string.IsNullOrWhiteSpace(failure.Message)
                || failure.Message.Length > 512)
            {
                throw new InvalidOperationException("Candidate failures require bounded diagnostics and affected population members.");
            }
        }
        HashSet<OpaqueId> failedMemberIds = value.Decisions
            .Where(item => item.Disposition == CandidateDecisionDisposition.Failed)
            .Select(item => item.PopulationMemberId)
            .ToHashSet();
        OpaqueId[] retainedFailedMemberIds = value.Failures
            .SelectMany(item => item.PopulationMemberIds)
            .ToArray();
        if (retainedFailedMemberIds.Distinct().Count() != retainedFailedMemberIds.Length
            || !failedMemberIds.SetEquals(retainedFailedMemberIds))
        {
            throw new InvalidOperationException("Failed decisions and retained failure diagnostics must correspond exactly.");
        }
        HashSet<OpaqueId> requiredGapDecisionIds = value.Decisions
            .Where(item => item.Disposition is CandidateDecisionDisposition.Unsupported
                or CandidateDecisionDisposition.Limited
                or CandidateDecisionDisposition.Unprocessed
                or CandidateDecisionDisposition.Deferred)
            .Select(item => item.DecisionId)
            .ToHashSet();
        HashSet<OpaqueId> retainedRequiredGapDecisionIds = value.Gaps
            .Where(item => requiredGapDecisionIds.Contains(item.DecisionId))
            .Select(item => item.DecisionId)
            .ToHashSet();
        if (!requiredGapDecisionIds.SetEquals(retainedRequiredGapDecisionIds))
        {
            throw new InvalidOperationException("Unsupported, limited, deferred, and unprocessed decisions require explicit gaps.");
        }
        HashSet<CandidateDependencyEdgeContract> expectedEdges = [];
        expectedEdges.Add(CandidateEdge("candidate-analysis-root", value.AnalysisRootId, "execution-input-binding",
            CandidateAnalysisIdentity.StableId("candidate-execution-input-binding", value.ExecutionInputId.Value, value.ExecutionInputFingerprint.Value), "uses"));
        expectedEdges.Add(CandidateEdge("candidate-analysis-root", value.AnalysisRootId, "policy-binding",
            CandidateAnalysisIdentity.StableId("candidate-policy-binding", value.PolicyId.Value, value.PolicyFingerprint.Value), "uses"));
        expectedEdges.Add(CandidateEdge("candidate-analysis-root", value.AnalysisRootId, "threshold-binding",
            CandidateAnalysisIdentity.StableId("candidate-threshold-binding", value.ThresholdId.Value, value.ThresholdFingerprint.Value), "uses"));
        expectedEdges.Add(CandidateEdge("candidate-analysis-root", value.AnalysisRootId, "limit-binding",
            CandidateAnalysisIdentity.StableId("candidate-limit-binding", value.LimitId.Value, value.LimitFingerprint.Value), "uses"));
        foreach (CandidateAnalyzerBindingContract analyzerBinding in value.AnalyzerBindings)
        {
            expectedEdges.Add(CandidateEdge("candidate-analysis-root", value.AnalysisRootId, "analyzer-declaration-binding",
                CandidateAnalysisIdentity.StableId("candidate-analyzer-binding", analyzerBinding.AnalyzerId.Value,
                    analyzerBinding.AnalyzerVersion.ToString(), analyzerBinding.SemanticContractVersion.ToString(),
                    analyzerBinding.IdentityContractVersion.ToString(), analyzerBinding.RulesetVersion.ToString(),
                    analyzerBinding.DeclarationFingerprint.Value), "uses"));
        }
        foreach (CandidateDecisionContract decision in value.Decisions)
        {
            expectedEdges.Add(CandidateEdge("candidate-decision", decision.DecisionId, "source-fact", decision.SourceFactId, "derived-from"));
            expectedEdges.Add(CandidateEdge("candidate-decision", decision.DecisionId, "dependency-closure", decision.DependencyClosureId, "depends-on"));
            foreach (OpaqueId dependencyId in decision.DependencyIds)
            {
                expectedEdges.Add(CandidateEdge("dependency-closure", decision.DependencyClosureId, "dependency", dependencyId, "depends-on"));
            }
            foreach (OpaqueId evidenceId in decision.EvidenceIds)
            {
                expectedEdges.Add(CandidateEdge("candidate-decision", decision.DecisionId, "evidence", evidenceId, "derived-from"));
            }
        }
        foreach (CandidateAnalysisEntryContract candidate in value.Candidates)
        {
            expectedEdges.Add(CandidateEdge("candidate", candidate.CandidateId, "candidate-decision", candidate.DecisionId, "derived-from"));
            foreach (OpaqueId evidenceId in candidate.SupportingEvidenceIds)
            {
                expectedEdges.Add(CandidateEdge("candidate", candidate.CandidateId, "evidence", evidenceId, "supports"));
            }
            foreach (OpaqueId evidenceId in candidate.ContradictingEvidenceIds)
            {
                expectedEdges.Add(CandidateEdge("candidate", candidate.CandidateId, "evidence", evidenceId, "contradicts"));
            }
        }
        foreach (CandidateHypothesisContract hypothesis in value.Hypotheses)
        {
            expectedEdges.Add(CandidateEdge("hypothesis", hypothesis.HypothesisId, "candidate", hypothesis.CandidateId, "derived-from"));
            foreach (OpaqueId evidenceId in hypothesis.SupportingEvidenceIds)
            {
                expectedEdges.Add(CandidateEdge("hypothesis", hypothesis.HypothesisId, "evidence", evidenceId, "supports"));
            }
            foreach (OpaqueId evidenceId in hypothesis.ContradictingEvidenceIds)
            {
                expectedEdges.Add(CandidateEdge("hypothesis", hypothesis.HypothesisId, "evidence", evidenceId, "contradicts"));
            }
        }
        foreach (CandidateAbstentionContract abstention in value.Abstentions)
        {
            expectedEdges.Add(CandidateEdge("abstention", abstention.AbstentionId, "candidate-decision", abstention.DecisionId, "derived-from"));
        }
        foreach (CandidateGapContract gap in value.Gaps)
        {
            expectedEdges.Add(CandidateEdge("gap", gap.GapId, "candidate-decision", gap.DecisionId, "derived-from"));
        }
        foreach (CandidateFailureContract failure in value.Failures)
        {
            foreach (OpaqueId memberId in failure.PopulationMemberIds)
            {
                OpaqueId decisionId = value.Decisions.Single(item => item.PopulationMemberId == memberId).DecisionId;
                expectedEdges.Add(CandidateEdge("failure", failure.FailureId, "candidate-decision", decisionId, "derived-from"));
            }
        }
        if (!expectedEdges.SetEquals(value.DependencyEdges))
        {
            throw new InvalidOperationException("Candidate dependency edges must exactly close every typed output and evidence reference.");
        }
        CandidatePopulationCountsContract actual = CandidateAnalysisCounts.Compute(value);
        if (actual != value.Counts)
        {
            throw new InvalidOperationException("Candidate population and output counts must exactly match the decision ledger.");
        }
        if (CandidateAnalysisIdentity.ComputePayloadId(value) != value.PayloadId)
        {
            throw new InvalidOperationException("Candidate analysis payload identity must cover the exact aggregate semantics.");
        }
    }

}
