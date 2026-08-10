using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Infinium.Domain.Contracts;

public static class CandidateAnalysisCounts
{
    public static CandidatePopulationCountsContract Compute(CandidateAnalysisContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CandidatePopulationCountsContract(
            value.Decisions.Count,
            value.Decisions.Count(item => item.Lane == CandidateLane.DeterministicRequired),
            value.Decisions.Count(item => item.Lane == CandidateLane.MandatoryEvidence),
            value.Decisions.Count(item => item.Lane == CandidateLane.OptionalRanked),
            value.Candidates.Count,
            value.Hypotheses.Count,
            value.Abstentions.Count,
            value.Gaps.Count,
            value.Failures.Count,
            value.Decisions.Count(item => item.Disposition == CandidateDecisionDisposition.ResolvedNegative),
            value.Decisions.Count(item => item.Disposition == CandidateDecisionDisposition.Unsupported),
            value.Candidates.Count(item => item.State == Slice5ResultState.Ambiguous),
            value.Decisions.Count(item => item.Disposition == CandidateDecisionDisposition.InvalidInput),
            value.Decisions.Count(item => item.Disposition == CandidateDecisionDisposition.Limited),
            value.Decisions.Count(item => item.Disposition == CandidateDecisionDisposition.Deferred),
            value.Decisions.Count(item => item.Disposition == CandidateDecisionDisposition.Unprocessed));
    }
}

public static class CandidateAnalysisIdentity
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static OpaqueId ComputePayloadId(CandidateAnalysisContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return StableId(
            "candidate-analysis",
            value.SchemaId,
            value.SchemaVersion.ToString(),
            value.OriginatingRunId.Value,
            value.AnalyzerId.Value,
            value.PopulationId.Value,
            value.PolicyId.Value,
            value.ThresholdId.Value,
            value.LimitId.Value,
            value.ExecutionInputId.Value,
            value.AnalysisRootId.Value,
            value.DeliveredInputId.Value,
            value.ExecutionInputFingerprint.Value,
            value.PolicyFingerprint.Value,
            value.ThresholdFingerprint.Value,
            value.LimitFingerprint.Value,
            value.AnalyzerSetFingerprint.Value,
            Canonical(value.AnalyzerBindings.Select(AnalyzerBinding)),
            FramedSequence("execution-input-descriptors", value.ExecutionInputDescriptors),
            FramedSequence("policy-descriptors", value.PolicyDescriptors),
            FramedSequence("threshold-descriptors", value.ThresholdDescriptors),
            FramedSequence("limit-descriptors", value.LimitDescriptors),
            value.PopulationDenominator.ToString(CultureInfo.InvariantCulture),
            Canonical(value.Decisions.Select(Decision)),
            Canonical(value.Candidates.Select(Candidate)),
            Canonical(value.Hypotheses.Select(Hypothesis)),
            Canonical(value.Abstentions.Select(Abstention)),
            Canonical(value.Gaps.Select(Gap)),
            Canonical(value.Failures.Select(Failure)),
            Canonical(value.DependencyEdges.Select(Edge)),
            Counts(value.Counts));
    }

    private static string AnalyzerBinding(CandidateAnalyzerBindingContract value) => FramedSequence(
        "analyzer-binding",
        [
            value.AnalyzerId.Value,
            value.AnalyzerVersion.ToString(),
            value.RulesetVersion.ToString(),
            value.DeclarationFingerprint.Value,
            value.CanonicalDeclarationJson,
        ]);

    public static OpaqueId StableId(string prefix, params string[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string value in values)
        {
            byte[] bytes = Utf8.GetBytes(value);
            hash.AppendData(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(bytes.Length)));
            hash.AppendData(bytes);
        }
        return new OpaqueId(prefix + "-" + Convert.ToHexStringLower(hash.GetHashAndReset())[..32]);
    }

    public static Sha256Fingerprint StructuralHash(IEnumerable<string> descriptors)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string descriptor in descriptors.Order(StringComparer.Ordinal))
        {
            byte[] bytes = Utf8.GetBytes(descriptor);
            hash.AppendData(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(bytes.Length)));
            hash.AppendData(bytes);
        }
        return new Sha256Fingerprint(Convert.ToHexStringLower(hash.GetHashAndReset()));
    }

    public static string FramedSequence(string tag, IEnumerable<string> values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        ArgumentNullException.ThrowIfNull(values);
        return tag + "=" + Canonical(values);
    }

    private static string Decision(CandidateDecisionContract item) => Canonical(
        item.DecisionId.Value, item.PopulationMemberId.Value, item.SourceFactId.Value, Lane(item.Lane), Disposition(item.Disposition),
        item.AnalyzerId.Value, item.PolicyId.Value, item.ThresholdId.Value, item.LimitId.Value,
        Canonical(item.Participants.Select(participant => Canonical(participant.Role, participant.ParticipantId.Value))),
        item.JoinKind, Canonical(item.Path.Select(id => id.Value)), item.DependencyClosureId.Value,
        Canonical(item.DependencyIds.Select(id => id.Value)),
        item.Rationale, Canonical(item.EvidenceIds.Select(id => id.Value)),
        item.AdmissionIndependentOfScore ? "true" : "false",
        item.OptionalRank?.ToString(CultureInfo.InvariantCulture) ?? "none");

    private static string Candidate(CandidateAnalysisEntryContract item) => Canonical(
        item.CandidateId.Value, item.DecisionId.Value, State(item.State), item.CausalExplanation,
        Canonical(item.SupportingEvidenceIds.Select(id => id.Value)),
        Canonical(item.ContradictingEvidenceIds.Select(id => id.Value)), Canonical(item.MissingInformation),
        Confidence(item.Confidence), item.ThresholdId.Value, item.HypothesisId?.Value ?? "none",
        item.AbstentionId?.Value ?? "none");

    private static string Hypothesis(CandidateHypothesisContract item) => Canonical(
        item.HypothesisId.Value, item.CandidateId.Value, State(item.State), item.ProposedExplanation,
        item.PredictedImpact, Canonical(item.SupportingEvidenceIds.Select(id => id.Value)),
        Canonical(item.ContradictingEvidenceIds.Select(id => id.Value)), Canonical(item.MissingInformation),
        Confidence(item.Confidence), item.ThresholdId.Value);

    private static string Abstention(CandidateAbstentionContract item) => Canonical(
        item.AbstentionId.Value, item.DecisionId.Value, item.CandidateId?.Value ?? "none", item.AnalyzerId.Value,
        item.Reason, Canonical(item.RequiredInformation));

    private static string Gap(CandidateGapContract item) => Canonical(
        item.GapId.Value, item.DecisionId.Value, item.PopulationId.Value, State(item.State), item.Reason,
        item.MissingCapabilityOrInformation);

    private static string Failure(CandidateFailureContract item) => Canonical(
        item.FailureId.Value, item.AnalyzerId.Value, Canonical(item.PopulationMemberIds.Select(id => id.Value)),
        item.FailureCode, item.Message, item.Retryable ? "true" : "false");

    private static string Edge(CandidateDependencyEdgeContract item) => Canonical(
        item.EdgeId.Value, item.FromKind, item.FromId.Value, item.ToKind, item.ToId.Value, item.EdgeKind);

    private static string Counts(CandidatePopulationCountsContract item) => Canonical(
        item.Population.ToString(CultureInfo.InvariantCulture), item.DeterministicRequired.ToString(CultureInfo.InvariantCulture),
        item.MandatoryEvidence.ToString(CultureInfo.InvariantCulture), item.OptionalRanked.ToString(CultureInfo.InvariantCulture),
        item.CandidateAdmitted.ToString(CultureInfo.InvariantCulture), item.Hypotheses.ToString(CultureInfo.InvariantCulture),
        item.Abstentions.ToString(CultureInfo.InvariantCulture), item.Gaps.ToString(CultureInfo.InvariantCulture),
        item.Failures.ToString(CultureInfo.InvariantCulture), item.ResolvedNegative.ToString(CultureInfo.InvariantCulture),
        item.Unsupported.ToString(CultureInfo.InvariantCulture), item.Ambiguous.ToString(CultureInfo.InvariantCulture),
        item.InvalidInput.ToString(CultureInfo.InvariantCulture), item.Limited.ToString(CultureInfo.InvariantCulture),
        item.Deferred.ToString(CultureInfo.InvariantCulture), item.Unprocessed.ToString(CultureInfo.InvariantCulture));

    private static string Canonical(params string[] values) => string.Concat(values.Select(Frame));
    private static string Canonical(IEnumerable<string> values) => string.Concat(values.Select(Frame));
    private static string Frame(string value) => FormattableString.Invariant($"{Utf8.GetByteCount(value)}:{value}");

    private static string Lane(CandidateLane value) => value switch
    {
        CandidateLane.DeterministicRequired => "deterministic-required",
        CandidateLane.MandatoryEvidence => "mandatory-evidence",
        CandidateLane.OptionalRanked => "optional-ranked",
        _ => throw new InvalidOperationException("Candidate lane is not closed."),
    };

    private static string Disposition(CandidateDecisionDisposition value) => value switch
    {
        CandidateDecisionDisposition.CandidateAdmitted => "candidate-admitted",
        CandidateDecisionDisposition.ResolvedNegative => "resolved-negative",
        CandidateDecisionDisposition.Unsupported => "unsupported",
        CandidateDecisionDisposition.Ambiguous => "ambiguous",
        CandidateDecisionDisposition.InvalidInput => "invalid-input",
        CandidateDecisionDisposition.Limited => "limited",
        CandidateDecisionDisposition.Deferred => "deferred",
        CandidateDecisionDisposition.Unprocessed => "unprocessed",
        CandidateDecisionDisposition.Abstained => "abstained",
        CandidateDecisionDisposition.Failed => "failed",
        _ => throw new InvalidOperationException("Candidate disposition is not closed."),
    };

    private static string State(Slice5ResultState value) => value.ToString();
    private static string Confidence(AnalysisConfidence value) => value.ToString();
}
