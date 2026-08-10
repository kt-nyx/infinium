namespace Infinium.Domain.Contracts;

public sealed record CandidateParticipantContract(OpaqueId ParticipantId, string Role);

public sealed record CandidateDecisionContract(
    OpaqueId DecisionId,
    OpaqueId PopulationMemberId,
    OpaqueId SourceFactId,
    CandidateLane Lane,
    CandidateDecisionDisposition Disposition,
    IReadOnlyList<CandidateParticipantContract> Participants,
    string JoinKind,
    IReadOnlyList<OpaqueId> Path,
    OpaqueId DependencyClosureId,
    string Rationale,
    IReadOnlyList<OpaqueId> EvidenceIds,
    bool AdmissionIndependentOfScore,
    long? OptionalRank)
{
    public OpaqueId AnalyzerId { get; init; } = new("analyzer-unspecified");

    public OpaqueId PolicyId { get; init; } = new("policy-unspecified");

    public OpaqueId ThresholdId { get; init; } = new("threshold-unspecified");

    public OpaqueId LimitId { get; init; } = new("limit-unspecified");

    public IReadOnlyList<OpaqueId> DependencyIds { get; init; } = [];
}

public sealed record CandidateAnalysisEntryContract(
    OpaqueId CandidateId,
    OpaqueId DecisionId,
    AnalysisResultState State,
    string CausalExplanation,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation,
    AnalysisConfidence Confidence,
    OpaqueId ThresholdId)
{
    public OpaqueId? HypothesisId { get; init; }

    public OpaqueId? AbstentionId { get; init; }
}

public sealed record CandidateHypothesisContract(
    OpaqueId HypothesisId,
    OpaqueId CandidateId,
    AnalysisResultState State,
    string ProposedExplanation,
    string PredictedImpact,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation,
    AnalysisConfidence Confidence,
    OpaqueId ThresholdId);

public sealed record CandidateAbstentionContract(
    OpaqueId AbstentionId,
    OpaqueId DecisionId,
    OpaqueId? CandidateId,
    OpaqueId AnalyzerId,
    string Reason,
    IReadOnlyList<string> RequiredInformation);

public sealed record CandidateGapContract(
    OpaqueId GapId,
    OpaqueId DecisionId,
    OpaqueId PopulationId,
    AnalysisResultState State,
    string Reason,
    string MissingCapabilityOrInformation);

public sealed record CandidateFailureContract(
    OpaqueId FailureId,
    OpaqueId AnalyzerId,
    IReadOnlyList<OpaqueId> PopulationMemberIds,
    string FailureCode,
    string Message,
    bool Retryable);

public sealed record CandidateDependencyEdgeContract(
    OpaqueId EdgeId,
    string FromKind,
    OpaqueId FromId,
    string ToKind,
    OpaqueId ToId,
    string EdgeKind);

public sealed record CandidateAnalyzerBindingContract(
    OpaqueId AnalyzerId,
    ContractVersion AnalyzerVersion,
    ContractVersion SemanticContractVersion,
    ContractVersion IdentityContractVersion,
    ContractVersion RulesetVersion,
    Sha256Fingerprint DeclarationFingerprint,
    string CanonicalDeclarationJson)
{
    public string AnalyzerFamily { get; init; } = AnalyzerId.Value;
}

public sealed record CandidatePopulationCountsContract(
    long Population,
    long DeterministicRequired,
    long MandatoryEvidence,
    long OptionalRanked,
    long CandidateAdmitted,
    long Hypotheses,
    long Abstentions,
    long Gaps,
    long Failures,
    long ResolvedNegative,
    long Unsupported,
    long Ambiguous,
    long InvalidInput,
    long Limited,
    long Deferred,
    long Unprocessed);

public sealed record CandidateAnalysisContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PayloadId,
    OpaqueId OriginatingRunId,
    OpaqueId AnalyzerId,
    OpaqueId PopulationId,
    long PopulationDenominator,
    IReadOnlyList<CandidateDecisionContract> Decisions,
    IReadOnlyList<CandidateAnalysisEntryContract> Candidates,
    IReadOnlyList<CandidateAbstentionContract> Abstentions,
    IReadOnlyList<CandidateGapContract> Gaps,
    IReadOnlyList<CandidateFailureContract> Failures)
{
    public OpaqueId PolicyId { get; init; } = new("policy-unspecified");

    public OpaqueId ThresholdId { get; init; } = new("threshold-unspecified");

    public OpaqueId LimitId { get; init; } = new("limit-unspecified");

    public OpaqueId ExecutionInputId { get; init; } = new("execution-input-unspecified");

    public OpaqueId AnalysisRootId { get; init; } = new("candidate-analysis-root-unspecified");

    public OpaqueId DeliveredInputId { get; init; } = new("candidate-delivered-input-unspecified");

    public Sha256Fingerprint ExecutionInputFingerprint { get; init; } = new(new string('0', 64));

    public Sha256Fingerprint PolicyFingerprint { get; init; } = new(new string('0', 64));

    public Sha256Fingerprint ThresholdFingerprint { get; init; } = new(new string('0', 64));

    public Sha256Fingerprint LimitFingerprint { get; init; } = new(new string('0', 64));

    public Sha256Fingerprint AnalyzerSetFingerprint { get; init; } = new(new string('0', 64));

    public IReadOnlyList<CandidateAnalyzerBindingContract> AnalyzerBindings { get; init; } = [];

    public IReadOnlyList<string> ExecutionInputDescriptors { get; init; } = [];

    public IReadOnlyList<string> PolicyDescriptors { get; init; } = [];

    public IReadOnlyList<string> ThresholdDescriptors { get; init; } = [];

    public IReadOnlyList<string> LimitDescriptors { get; init; } = [];

    public IReadOnlyList<CandidateHypothesisContract> Hypotheses { get; init; } = [];

    public IReadOnlyList<CandidateDependencyEdgeContract> DependencyEdges { get; init; } = [];

    public CandidatePopulationCountsContract Counts { get; init; } = new(
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
