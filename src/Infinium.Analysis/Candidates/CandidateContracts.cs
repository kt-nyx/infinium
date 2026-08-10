using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.Candidates;

public enum CausalJoinInputState
{
    Complete,
    ResolvedNegative,
    Unsupported,
    Ambiguous,
    InvalidInput,
    Deferred,
    Failed,
}

public sealed record CausalJoinPopulationMember(
    OpaqueId PopulationMemberId,
    OpaqueId AnalyzerId,
    CandidateLane Lane,
    IReadOnlyList<CandidateParticipantContract> Participants,
    string JoinKind,
    IReadOnlyList<OpaqueId> Path,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> MissingInformation,
    CausalJoinInputState InputState,
    string Rationale,
    string PredictedImpact,
    long? OptionalRank = null,
    string? FailureCode = null,
    string? FailureMessage = null,
    bool EmitGap = false)
{
    public OpaqueId SourceFactId { get; init; } = new("source-fact-unspecified");

    public Sha256Fingerprint InputFingerprint => CandidateAnalysisIdentity.StructuralHash(
    [
        $"population-member={PopulationMemberId.Value}",
        $"source-fact={SourceFactId.Value}",
        $"analyzer={AnalyzerId.Value}",
        $"lane={Lane}",
        CandidateAnalysisIdentity.FramedSequence(
            "participants",
            Participants.OrderBy(item => item.Role, StringComparer.Ordinal)
                .ThenBy(item => item.ParticipantId.Value, StringComparer.Ordinal)
                .Select(item => CandidateAnalysisIdentity.FramedSequence(
                "participant", [item.Role, item.ParticipantId.Value]))),
        $"join-kind={JoinKind}",
        CandidateAnalysisIdentity.FramedSequence("path", Path.Select(item => item.Value)),
        CandidateAnalysisIdentity.FramedSequence("dependencies", DependencyIds.Select(item => item.Value).Order(StringComparer.Ordinal)),
        CandidateAnalysisIdentity.FramedSequence("supporting-evidence", SupportingEvidenceIds.Select(item => item.Value).Order(StringComparer.Ordinal)),
        CandidateAnalysisIdentity.FramedSequence("contradicting-evidence", ContradictingEvidenceIds.Select(item => item.Value).Order(StringComparer.Ordinal)),
        CandidateAnalysisIdentity.FramedSequence("missing-information", MissingInformation.Order(StringComparer.Ordinal)),
        $"input-state={InputState}",
        $"rationale={Rationale}",
        $"predicted-impact={PredictedImpact}",
        $"optional-rank={OptionalRank?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}",
        $"failure-code={FailureCode ?? "none"}",
        $"failure-message={FailureMessage ?? "none"}",
        $"emit-gap={(EmitGap ? "true" : "false")}",
    ]);
}

public sealed record CandidatePopulationContext(
    DocumentationEvidenceContract? DocumentationEvidence,
    OpaqueId? OriginatingRunId = null,
    OpaqueId? SourceSnapshotId = null,
    OpaqueId? AnalysisContextId = null,
    OpaqueId? ConfigurationId = null,
    CandidateDeliveredInputContract? DeliveredInput = null,
    Sha256Fingerprint? DeliveredInputByteFingerprint = null,
    CandidateDeliveredExpansionContract? DeliveredExpansion = null,
    Sha256Fingerprint? DeliveredExpansionByteFingerprint = null,
    OpaqueId? AdmittedDeliveredInputId = null);

public interface ICandidatePopulationSource
{
    public OpaqueId AnalyzerId { get; }

    public AnalyzerDeclarationContract Declaration { get; }

    public IReadOnlyList<CausalJoinPopulationMember> DeclarePopulation(
        CandidatePopulationContext context,
        CancellationToken cancellationToken = default);

    public IReadOnlyList<CausalJoinPopulationMember> ConstructPopulation(
        CandidatePopulationContext context,
        CancellationToken cancellationToken = default);
}

public interface ICandidateDeliveredRootResolver
{
    public OpaqueId ResolveDeliveredInputId(CandidatePopulationContext context);
}

public sealed record CandidateExecutionLimits(
    OpaqueId LimitId,
    long MaximumPopulationWork,
    long MaximumOptionalCandidates)
{
    public static readonly CandidateExecutionLimits Default = new(
        new OpaqueId("candidate-limits-v1"), 1_000_000, 100_000);

    public IReadOnlyList<string> SemanticsDescriptors =>
    [
        "contract=candidate-execution-limits-v1",
        $"limit-id={LimitId.Value}",
        $"maximum-population-work={MaximumPopulationWork.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
        $"maximum-optional-candidates={MaximumOptionalCandidates.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
        "optional-limit-disposition=Limited",
    ];

    public Sha256Fingerprint SemanticsFingerprint => CandidateAnalysisIdentity.StructuralHash(SemanticsDescriptors);
}

public sealed record CandidatePipelineRequest(
    OpaqueId OriginatingRunId,
    OpaqueId PopulationId,
    OpaqueId PolicyId,
    OpaqueId ThresholdId,
    CandidateExecutionLimits Limits,
    CandidatePopulationContext Context,
    IReadOnlyList<ICandidatePopulationSource> Sources,
    AnalysisExecutionInputContract? ExecutionInput = null)
{
    public IReadOnlyList<string> PolicyDescriptors =>
    [
        "contract=candidate-admission-policy-v1",
        $"binding-id={PolicyId.Value}",
        "precedence=invalid,failed,unsupported,resolved-negative,deferred,complete-or-ambiguous,work-limit,optional-limit",
        "deterministic=admit-with-evidence-bound-hypothesis",
        "mandatory=admit-with-hypothesis",
        "optional=rank-ascending-then-member-id",
        "missing-information=abstain",
        "contradicting-or-ambiguous=ambiguous",
    ];

    public Sha256Fingerprint PolicyFingerprint => CandidateAnalysisIdentity.StructuralHash(PolicyDescriptors);

    public IReadOnlyList<string> ThresholdDescriptors =>
    [
        "contract=candidate-hypothesis-threshold-v1",
        $"binding-id={ThresholdId.Value}",
        "deterministic=not-applicable",
        "complete=plausible",
        "ambiguous=speculative-lead",
        "missing=abstained",
    ];

    public Sha256Fingerprint ThresholdFingerprint => CandidateAnalysisIdentity.StructuralHash(ThresholdDescriptors);

    public OpaqueId ExecutionInputId => ExecutionInput?.ExecutionInputId
        ?? CandidateAnalysisIdentity.StableId(
            "candidate-execution-input",
            OriginatingRunId.Value,
            PopulationId.Value,
            Context.SourceSnapshotId?.Value ?? "none",
            Context.AnalysisContextId?.Value ?? "none",
            Context.ConfigurationId?.Value ?? "none");

    public IReadOnlyList<string> ExecutionInputDescriptors => ExecutionInput is null
        ? [
                "contract=candidate-execution-input-projection-v1",
                $"execution-input-id={ExecutionInputId.Value}",
                $"run={OriginatingRunId.Value}",
                $"population={PopulationId.Value}",
                $"snapshot={Context.SourceSnapshotId?.Value ?? "none"}",
                $"analysis-context={Context.AnalysisContextId?.Value ?? "none"}",
                $"configuration={Context.ConfigurationId?.Value ?? "none"}",
                $"documentation={Context.DocumentationEvidence?.PayloadId.Value ?? "none"}",
        ]
        : DescribeExecutionInput(ExecutionInput);

    public Sha256Fingerprint ExecutionInputFingerprint =>
        CandidateAnalysisIdentity.StructuralHash(ExecutionInputDescriptors);

    public static IReadOnlyList<string> DescribeExecutionInput(AnalysisExecutionInputContract input)
    {
        List<string> descriptors =
        [
            "contract=analysis-execution-input/v1",
            $"execution-input-id={input.ExecutionInputId.Value}",
            $"run-id={input.RunId.Value}",
            Reference("analysis-context", input.AnalysisContext),
            Reference("installation-snapshot", input.InstallationSnapshot),
            Reference("bethesda-semantic-input", input.BethesdaSemanticInput),
            Reference("effective-configuration", input.EffectiveConfiguration),
            Reference("resolved-input-manifest", input.ResolvedInputManifest),
            $"mode={input.Mode}",
            $"prior-run-id={input.PriorRunId?.Value ?? "none"}",
            $"seed={input.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"maximum-entities={input.Limits.MaximumEntities.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"maximum-edges={input.Limits.MaximumEdges.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"maximum-truth-rows={input.Limits.MaximumTruthRows.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"maximum-output-items={input.Limits.MaximumOutputItems.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            $"maximum-wall-time-ms={input.Limits.MaximumWallTimeMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
        ];
        descriptors.AddRange(input.SourceInputs.OrderBy(item => item.ArtifactId.Value, StringComparer.Ordinal)
            .Select(item => Reference("source-input", item)));
        descriptors.AddRange(input.AnalyzerDeclarations.OrderBy(item => item.ArtifactId.Value, StringComparer.Ordinal)
            .Select(item => Reference("analyzer-declaration", item)));
        descriptors.AddRange(input.Boundaries.OrderBy(item => item.BoundaryId, StringComparer.Ordinal).Select(item =>
            $"boundary={item.BoundaryId}|state={item.State}|reason={item.Reason}"));
        return descriptors;
    }

    private static string Reference(string kind, ArtifactReferenceContract value) =>
        $"{kind}={value.ArtifactId.Value}|version={value.ArtifactVersion}|fingerprint={value.Fingerprint.Value}|availability={value.Availability}";
}

public sealed record CandidateMemberOutcome(
    Sha256Fingerprint InputFingerprint,
    CandidateDecisionContract Decision,
    CandidateAnalysisEntryContract? Candidate,
    CandidateHypothesisContract? Hypothesis,
    CandidateAbstentionContract? Abstention,
    CandidateGapContract? Gap,
    CandidateFailureContract? Failure);

public sealed record CandidateCheckpointState(
    OpaqueId OriginatingRunId,
    OpaqueId PopulationId,
    OpaqueId PolicyId,
    OpaqueId ThresholdId,
    OpaqueId LimitId,
    Sha256Fingerprint LimitsFingerprint,
    Sha256Fingerprint OptionalFrontierFingerprint,
    Sha256Fingerprint WorkFrontierFingerprint,
    Sha256Fingerprint AnalyzerSetFingerprint,
    Sha256Fingerprint PolicyFingerprint,
    Sha256Fingerprint ThresholdFingerprint,
    Sha256Fingerprint ExecutionInputFingerprint,
    IReadOnlyDictionary<OpaqueId, CandidateMemberOutcome> Outcomes);

public sealed record CandidatePipelineMetrics(
    long PopulationMembers,
    long EvaluatedMembers,
    long ReusedMembers,
    long ElapsedMilliseconds,
    Sha256Fingerprint StructuralHash);

public sealed record CandidatePipelineResult(
    CandidateAnalysisContract Analysis,
    CandidateCheckpointState Checkpoint,
    CandidatePipelineMetrics Metrics,
    IReadOnlyList<OpaqueId> RecomputedMemberIds,
    IReadOnlyList<OpaqueId> ReusedMemberIds);
