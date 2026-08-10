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
    Sha256Fingerprint? DeliveredExpansionByteFingerprint = null);

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

public static class CandidatePipeline
{
    public static CandidatePipelineResult Execute(
        CandidatePipelineRequest request,
        CandidateCheckpointState? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        Stopwatch stopwatch = Stopwatch.StartNew();
        using CancellationTokenSource executionDeadline = new();
        if (request.ExecutionInput is { } deadlineInput)
        {
            executionDeadline.CancelAfter(TimeSpan.FromMilliseconds(deadlineInput.Limits.MaximumWallTimeMilliseconds));
        }
        Sha256Fingerprint analyzerSetFingerprint = CandidateAnalysisIdentity.StructuralHash(
            request.Sources.OrderBy(source => source.AnalyzerId.Value, StringComparer.Ordinal).Select(source =>
                $"{source.AnalyzerId.Value}:{CandidateAnalysisIdentity.StructuralHash([JsonSerializer.Serialize(source.Declaration)]).Value}"));
        List<CausalJoinPopulationMember> population = [];
        foreach (ICandidatePopulationSource source in request.Sources.OrderBy(item => item.AnalyzerId.Value, StringComparer.Ordinal))
        {
            EnsureWithinWallTime(request, stopwatch);
            IReadOnlyList<CausalJoinPopulationMember> declared;
            try
            {
                declared = source.DeclarePopulation(request.Context, executionDeadline.Token);
                EnsureWithinWallTime(request, stopwatch);
                if (declared.Count > source.Declaration.ResourceBounds.MaximumInputItems)
                {
                    throw new InvalidDataException($"Analyzer '{source.AnalyzerId.Value}' exceeded its declared input bound.");
                }
                if (declared.Select(item => item.PopulationMemberId).Distinct().Count() != declared.Count)
                {
                    throw new InvalidDataException("An analyzer declared duplicate population member identities.");
                }
            }
            catch (OperationCanceledException) when (executionDeadline.IsCancellationRequested)
            {
                throw new InvalidDataException("Candidate execution exceeded its admitted wall-time limit.");
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                population.Add(DeclarationFailureMember(source, exception.Message));
                continue;
            }
            try
            {
                IReadOnlyList<CausalJoinPopulationMember> constructed = source.ConstructPopulation(request.Context, executionDeadline.Token);
                EnsureWithinWallTime(request, stopwatch);
                if (constructed.Select(item => item.PopulationMemberId).Distinct().Count() != constructed.Count
                    || declared.Count != constructed.Count
                    || !declared.Select(item => item.PopulationMemberId).ToHashSet()
                    .SetEquals(constructed.Select(item => item.PopulationMemberId)))
                {
                    throw new InvalidDataException("An analyzer's constructed population differs from its declared bounded population.");
                }
                foreach (CausalJoinPopulationMember member in constructed)
                {
                    EnsureWithinWallTime(request, stopwatch);
                    try
                    {
                        if (member.AnalyzerId != source.AnalyzerId)
                        {
                            throw new InvalidDataException("A population member analyzer identity differs from its declaring source.");
                        }
                        CausalJoinPopulationMember scoped = ApplyDeclaredScope(source, member);
                        ValidateMember(scoped);
                        population.Add(scoped);
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
                    {
                        population.Add(InvalidMember(source.AnalyzerId, member, exception.Message));
                    }
                }
            }
            catch (OperationCanceledException) when (executionDeadline.IsCancellationRequested)
            {
                throw new InvalidDataException("Candidate execution exceeded its admitted wall-time limit.");
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            {
                population.AddRange(declared.Select(item => FailureMember(
                    source.AnalyzerId, item, "analyzer-execution-failed", exception.Message)));
            }
        }

        OpaqueId[] duplicateOwnerIds = population
            .GroupBy(item => item.PopulationMemberId)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(item => item.AnalyzerId))
            .Distinct()
            .ToArray();
        if (duplicateOwnerIds.Length != 0)
        {
            HashSet<OpaqueId> affected = duplicateOwnerIds.ToHashSet();
            population.RemoveAll(item => affected.Contains(item.AnalyzerId));
            population.AddRange(request.Sources
                .Where(source => affected.Contains(source.AnalyzerId))
                .Select(source => DeclarationFailureMember(
                    source,
                    "The analyzer population collides with another analyzer's population member identity.")));
        }

        foreach (ICandidatePopulationSource source in request.Sources)
        {
            CausalJoinPopulationMember[] owned = population
                .Where(item => item.AnalyzerId == source.AnalyzerId)
                .ToArray();
            long retainedItems = owned.Sum(EstimateRetainedItems);
            if (retainedItems > source.Declaration.ResourceBounds.MaximumOutputItems)
            {
                population.RemoveAll(item => item.AnalyzerId == source.AnalyzerId);
                population.Add(DeclarationFailureMember(
                    source,
                    "The analyzer's declared output bound is insufficient for its exact population."));
            }
        }

        if (population.Count > 1_000_000)
        {
            throw new InvalidDataException("Candidate population exceeds the M1 bounded population contract.");
        }
        if (population.Select(item => item.PopulationMemberId).Distinct().Count() != population.Count)
        {
            throw new InvalidDataException("Candidate population members must be globally unique.");
        }
        long estimatedEdges = checked(population.Sum(EstimateDependencyEdges) + 4L + request.Sources.Count);
        long estimatedOutputItems = population.Sum(EstimateOutputItems);
        long estimatedSemanticBytes = population.Sum(EstimateMemberSemanticBytes);
        long estimatedBindingBytes = request.ExecutionInputDescriptors.Concat(request.PolicyDescriptors)
            .Concat(request.ThresholdDescriptors).Concat(request.Limits.SemanticsDescriptors)
            .Sum(value => (long)EscapedJsonBytes(value));
        estimatedBindingBytes = checked(estimatedBindingBytes + request.Sources.Sum(source =>
            (long)EscapedJsonBytes(JsonSerializer.Serialize(source.Declaration))));
        long estimatedBytes = checked(estimatedOutputItems * 1_024L + estimatedEdges * 512L
            + estimatedSemanticBytes * 6L + estimatedBindingBytes * 2L);
        if (estimatedEdges > 4_000_000 || estimatedBytes > 64L * 1024 * 1024)
        {
            throw new InvalidDataException("Candidate population exceeds the aggregate edge or payload preflight bound.");
        }
        if (request.ExecutionInput is { } executionInput
            && (population.Count > executionInput.Limits.MaximumEntities
                || estimatedEdges > executionInput.Limits.MaximumEdges
                || estimatedOutputItems > executionInput.Limits.MaximumOutputItems))
        {
            throw new InvalidDataException("Candidate population exceeds the admitted analysis execution limits.");
        }

        Sha256Fingerprint optionalFrontierFingerprint = CandidateAnalysisIdentity.StructuralHash(
            population
                .Where(item => item.Lane == CandidateLane.OptionalRanked
                    && item.InputState is CausalJoinInputState.Complete or CausalJoinInputState.Ambiguous)
                .OrderBy(item => item.OptionalRank ?? long.MaxValue)
                .ThenBy(item => item.PopulationMemberId.Value, StringComparer.Ordinal)
                .Select(item => $"member={item.PopulationMemberId.Value}|rank={item.OptionalRank}|state={item.InputState}")
                .Prepend("candidate-optional-frontier-v1"));
        CausalJoinPopulationMember[] orderedWork = population
            .Where(ConsumesCandidateWork)
            .OrderBy(item => LaneOrder(item.Lane))
            .ThenBy(item => item.Lane == CandidateLane.OptionalRanked ? item.OptionalRank ?? long.MaxValue : 0)
            .ThenBy(item => item.PopulationMemberId.Value, StringComparer.Ordinal)
            .ToArray();
        Sha256Fingerprint workFrontierFingerprint = CandidateAnalysisIdentity.StructuralHash(
            request.Limits.MaximumPopulationWork >= orderedWork.Length
                ? ["candidate-work-frontier-v1=unconstrained"]
                : orderedWork.Select((item, index) =>
                    $"position={index}|member={item.PopulationMemberId.Value}|lane={item.Lane}|rank={item.OptionalRank?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}|state={item.InputState}"));
        bool checkpointCompatible = checkpoint is not null
            && checkpoint.OriginatingRunId == request.OriginatingRunId
            && checkpoint.PopulationId == request.PopulationId
            && checkpoint.PolicyId == request.PolicyId
            && checkpoint.ThresholdId == request.ThresholdId
            && checkpoint.LimitId == request.Limits.LimitId
            && checkpoint.LimitsFingerprint == request.Limits.SemanticsFingerprint
            && checkpoint.WorkFrontierFingerprint == workFrontierFingerprint
            && checkpoint.AnalyzerSetFingerprint == analyzerSetFingerprint
            && checkpoint.PolicyFingerprint == request.PolicyFingerprint
            && checkpoint.ThresholdFingerprint == request.ThresholdFingerprint
            && checkpoint.ExecutionInputFingerprint == request.ExecutionInputFingerprint;
        bool optionalFrontierCompatible = checkpointCompatible
            && checkpoint!.OptionalFrontierFingerprint == optionalFrontierFingerprint;
        Dictionary<OpaqueId, CandidateMemberOutcome> outcomes = [];
        List<OpaqueId> recomputed = [];
        List<OpaqueId> reused = [];
        long work = 0;
        long optionalAdmitted = 0;
        foreach (CausalJoinPopulationMember member in population
            .OrderBy(item => LaneOrder(item.Lane))
            .ThenBy(item => item.Lane == CandidateLane.OptionalRanked ? item.OptionalRank ?? long.MaxValue : 0)
            .ThenBy(item => item.PopulationMemberId.Value, StringComparer.Ordinal))
        {
            EnsureWithinWallTime(request, stopwatch);
            if (checkpointCompatible
                && (member.Lane != CandidateLane.OptionalRanked || optionalFrontierCompatible)
                && checkpoint!.Outcomes.TryGetValue(member.PopulationMemberId, out CandidateMemberOutcome? retained)
                && retained.InputFingerprint == member.InputFingerprint)
            {
                outcomes.Add(member.PopulationMemberId, retained);
                reused.Add(member.PopulationMemberId);
                if (retained.Decision.Disposition is CandidateDecisionDisposition.CandidateAdmitted
                        or CandidateDecisionDisposition.Ambiguous
                    && member.Lane == CandidateLane.OptionalRanked)
                {
                    optionalAdmitted++;
                }
                if (ConsumesCandidateWork(member))
                {
                    work++;
                }
                continue;
            }

            CandidateDecisionDisposition? forced = null;
            bool consumesWork = ConsumesCandidateWork(member);
            if (consumesWork && work >= request.Limits.MaximumPopulationWork)
            {
                forced = CandidateDecisionDisposition.Unprocessed;
            }
            else if (member.Lane == CandidateLane.OptionalRanked
                && member.InputState is CausalJoinInputState.Complete or CausalJoinInputState.Ambiguous
                && optionalAdmitted >= request.Limits.MaximumOptionalCandidates)
            {
                forced = CandidateDecisionDisposition.Limited;
            }
            CandidateMemberOutcome outcome = Evaluate(request, member, forced);
            outcomes.Add(member.PopulationMemberId, outcome);
            recomputed.Add(member.PopulationMemberId);
            if (outcome.Decision.Disposition is CandidateDecisionDisposition.CandidateAdmitted
                    or CandidateDecisionDisposition.Ambiguous
                && member.Lane == CandidateLane.OptionalRanked)
            {
                optionalAdmitted++;
            }
            if (consumesWork)
            {
                work++;
            }
        }

        EnsureWithinWallTime(request, stopwatch);
        CandidateAnalysisContract analysis = Assemble(request, outcomes.Values);
        CandidateCheckpointState nextCheckpoint = new(
            request.OriginatingRunId, request.PopulationId, request.PolicyId, request.ThresholdId, request.Limits.LimitId,
            request.Limits.SemanticsFingerprint, optionalFrontierFingerprint, workFrontierFingerprint, analyzerSetFingerprint,
            request.PolicyFingerprint, request.ThresholdFingerprint, request.ExecutionInputFingerprint, outcomes);
        Sha256Fingerprint structuralHash = CandidateAnalysisIdentity.StructuralHash(
            outcomes.Values.Select(item => string.Join('|',
                $"member={item.Decision.PopulationMemberId.Value}",
                $"input={item.InputFingerprint.Value}",
                $"lane={item.Decision.Lane}",
                $"disposition={item.Decision.Disposition}",
                $"candidate={item.Candidate?.CandidateId.Value ?? "none"}",
                $"hypothesis={item.Hypothesis?.HypothesisId.Value ?? "none"}",
                $"abstention={item.Abstention?.AbstentionId.Value ?? "none"}",
                $"gap={item.Gap?.GapId.Value ?? "none"}",
                $"failure={item.Failure?.FailureId.Value ?? "none"}"))
            .Prepend($"threshold={request.ThresholdFingerprint.Value}")
            .Prepend($"policy={request.PolicyFingerprint.Value}")
            .Prepend($"limits={request.Limits.SemanticsFingerprint.Value}")
            .Prepend($"analyzers={analyzerSetFingerprint.Value}"));
        stopwatch.Stop();
        if (request.ExecutionInput is { } boundedExecution
            && stopwatch.ElapsedMilliseconds > boundedExecution.Limits.MaximumWallTimeMilliseconds)
        {
            throw new InvalidDataException("Candidate execution exceeded its admitted wall-time limit.");
        }
        return new CandidatePipelineResult(
            analysis,
            nextCheckpoint,
            new CandidatePipelineMetrics(population.Count, recomputed.Count, reused.Count, stopwatch.ElapsedMilliseconds, structuralHash),
            recomputed,
            reused);
    }

    private static bool ConsumesCandidateWork(CausalJoinPopulationMember member) =>
        member.InputState is CausalJoinInputState.Complete or CausalJoinInputState.Ambiguous;

    private static void EnsureWithinWallTime(CandidatePipelineRequest request, Stopwatch stopwatch)
    {
        if (request.ExecutionInput is { } executionInput
            && stopwatch.ElapsedMilliseconds > executionInput.Limits.MaximumWallTimeMilliseconds)
        {
            throw new InvalidDataException("Candidate execution exceeded its admitted wall-time limit.");
        }
    }

    private static long EstimateRetainedItems(CausalJoinPopulationMember member)
    {
        return EstimateOutputItems(member) + EstimateDependencyEdges(member);
    }

    private static long EstimateMemberSemanticBytes(CausalJoinPopulationMember member)
    {
        IEnumerable<string> values =
        [
            member.PopulationMemberId.Value,
            member.SourceFactId.Value,
            member.AnalyzerId.Value,
            member.JoinKind,
            member.Rationale,
            member.PredictedImpact,
            .. member.Participants.SelectMany(item => new[] { item.Role, item.ParticipantId.Value }),
            .. member.Path.Select(item => item.Value),
            .. member.DependencyIds.Select(item => item.Value),
            .. member.SupportingEvidenceIds.Select(item => item.Value),
            .. member.ContradictingEvidenceIds.Select(item => item.Value),
            .. member.MissingInformation,
            .. member.FailureCode is null ? [] : new[] { member.FailureCode },
            .. member.FailureMessage is null ? [] : new[] { member.FailureMessage },
        ];
        return values.Sum(value => (long)EscapedJsonBytes(value));
    }

    private static int EscapedJsonBytes(string value) => JsonEncodedText.Encode(value).EncodedUtf8Bytes.Length;

    private static long EstimateOutputItems(CausalJoinPopulationMember member)
    {
        long items = 1;
        if (member.InputState is CausalJoinInputState.Complete or CausalJoinInputState.Ambiguous)
        {
            items += 2;
            if (member.MissingInformation.Count != 0)
            {
                items += 1;
            }
            if (member.EmitGap)
            {
                items += 1;
            }
        }
        else if (member.InputState == CausalJoinInputState.Unsupported)
        {
            items += 2;
        }
        else if (member.InputState == CausalJoinInputState.Deferred)
        {
            items += 1;
        }
        else if (member.InputState == CausalJoinInputState.Failed)
        {
            items += 1 + (member.EmitGap ? 1 : 0);
        }
        return items;
    }

    private static long EstimateDependencyEdges(CausalJoinPopulationMember member)
    {
        long edges = 2L + member.DependencyIds.Count + member.SupportingEvidenceIds.Count;
        if (member.InputState is CausalJoinInputState.Complete or CausalJoinInputState.Ambiguous)
        {
            edges += 1L + member.SupportingEvidenceIds.Count + member.ContradictingEvidenceIds.Count;
            edges += 1L + member.SupportingEvidenceIds.Count + member.ContradictingEvidenceIds.Count;
            if (member.MissingInformation.Count != 0)
            {
                edges++;
            }
            if (member.EmitGap)
            {
                edges++;
            }
        }
        else if (member.InputState == CausalJoinInputState.Unsupported)
        {
            edges += 2;
        }
        else if (member.InputState == CausalJoinInputState.Deferred)
        {
            edges++;
        }
        else if (member.InputState == CausalJoinInputState.Failed)
        {
            edges += 1 + (member.EmitGap ? 1 : 0);
        }
        return edges;
    }

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
            Slice5ResultState candidateState = mustAbstain
                ? Slice5ResultState.Abstained
                : member.ContradictingEvidenceIds.Count != 0 || member.InputState == CausalJoinInputState.Ambiguous
                    ? Slice5ResultState.Ambiguous
                    : Slice5ResultState.Present;
            OpaqueId hypothesisId = CandidateAnalysisIdentity.StableId("hypothesis", candidateId.Value, request.ThresholdId.Value);
            OpaqueId? abstentionId = mustAbstain ? CandidateAnalysisIdentity.StableId("candidate-abstention", candidateId.Value, request.ThresholdId.Value) : null;
            candidate = new CandidateAnalysisEntryContract(
                candidateId, decisionId, candidateState, member.Rationale,
                member.SupportingEvidenceIds, member.ContradictingEvidenceIds, member.MissingInformation,
                candidateState == Slice5ResultState.Present ? AnalysisConfidence.Plausible : AnalysisConfidence.SpeculativeLead,
                request.ThresholdId)
            {
                HypothesisId = hypothesisId,
                AbstentionId = abstentionId,
            };
            hypothesis = new CandidateHypothesisContract(
                hypothesisId, candidateId, mustAbstain ? Slice5ResultState.Partial : candidateState,
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
                    decisionId, request.PopulationId, Slice5ResultState.Missing,
                    "The candidate remains admitted, but a required causal witness is missing.",
                    member.MissingInformation[0]);
            }
        }
        else if (disposition is CandidateDecisionDisposition.Unsupported
            or CandidateDecisionDisposition.Limited
            or CandidateDecisionDisposition.Unprocessed
            or CandidateDecisionDisposition.Deferred)
        {
            Slice5ResultState state = disposition switch
            {
                CandidateDecisionDisposition.Unsupported => Slice5ResultState.Unsupported,
                CandidateDecisionDisposition.Limited or CandidateDecisionDisposition.Unprocessed => Slice5ResultState.LimitReached,
                _ => Slice5ResultState.Partial,
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
                    decisionId, request.PopulationId, Slice5ResultState.Failed,
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
            request.Sources.Count == 1 ? request.Sources[0].AnalyzerId : new OpaqueId("candidate-analyzers-m1-s5-wp3"),
            request.PopulationId, decisions.Length, decisions, candidates, abstentions, gaps, failures)
        {
            PolicyId = request.PolicyId,
            ThresholdId = request.ThresholdId,
            LimitId = request.Limits.LimitId,
            ExecutionInputId = request.ExecutionInputId,
            AnalysisRootId = analysisRootId,
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
        Slice5ContractInvariants.Validate(result);
        return result;
    }

    private static CandidateDependencyEdgeContract Edge(
        string fromKind, OpaqueId fromId, string toKind, OpaqueId toId, string edgeKind) => new(
            CandidateAnalysisIdentity.StableId("candidate-edge", fromKind, fromId.Value, toKind, toId.Value, edgeKind),
            fromKind, fromId, toKind, toId, edgeKind);

    private static void ValidateRequest(CandidatePipelineRequest request)
    {
        if (request.Context.OriginatingRunId is { } contextRun
            && contextRun != request.OriginatingRunId)
        {
            throw new InvalidOperationException("Candidate population context run identity differs from the request run.");
        }
        if (request.ExecutionInput is { } executionInput)
        {
            Slice5ContractInvariants.Validate(executionInput);
            if (executionInput.RunId != request.OriginatingRunId
                || request.Context.SourceSnapshotId != executionInput.InstallationSnapshot.ArtifactId
                || request.Context.ConfigurationId != executionInput.EffectiveConfiguration.ArtifactId)
            {
                throw new InvalidOperationException("Candidate execution input differs from the admitted run, snapshot, or configuration.");
            }
            Dictionary<OpaqueId, ArtifactReferenceContract> analyzerReferences = executionInput.AnalyzerDeclarations
                .ToDictionary(item => item.ArtifactId);
            if (analyzerReferences.Count != request.Sources.Count
                || request.Sources.Any(source =>
                {
                    Sha256Fingerprint fingerprint = CandidateAnalysisIdentity.StructuralHash(
                        [JsonSerializer.Serialize(source.Declaration)]);
                    return !analyzerReferences.TryGetValue(source.AnalyzerId, out ArtifactReferenceContract? reference)
                        || reference.ArtifactVersion != source.Declaration.AnalyzerVersion
                        || reference.Fingerprint != fingerprint;
                }))
            {
                throw new InvalidOperationException("Candidate sources differ from the admitted analyzer declaration set.");
            }
            if (request.Context.DeliveredInput is not null && request.Context.DeliveredExpansion is not null)
            {
                throw new InvalidOperationException("Candidate execution must admit exactly one delivered input or expansion artifact.");
            }
            if (request.Context.DeliveredInput is { } delivered)
            {
                CandidateDeliveredContractInvariants.Validate(delivered);
                Sha256Fingerprint actualFingerprint = ContractJsonSerializer.Fingerprint(delivered);
                ArtifactReferenceContract? deliveredReference = executionInput.SourceInputs.SingleOrDefault(item =>
                    item.ArtifactId == delivered.PayloadId);
                if (request.Context.DeliveredInputByteFingerprint is null
                    || deliveredReference is null
                    || deliveredReference.ArtifactVersion != CandidateDeliveredInputIdentity.Version
                    || request.Context.DeliveredInputByteFingerprint != actualFingerprint
                    || deliveredReference.Fingerprint != request.Context.DeliveredInputByteFingerprint
                    || !StringComparer.Ordinal.Equals(deliveredReference.Availability, "retained"))
                {
                    throw new InvalidOperationException("Candidate delivered input bytes differ from the admitted source artifact reference.");
                }
            }
            if (request.Context.DeliveredExpansion is { } expansion)
            {
                CandidateDeliveredContractInvariants.Validate(expansion);
                Sha256Fingerprint actualFingerprint = ContractJsonSerializer.Fingerprint(expansion);
                ArtifactReferenceContract? expansionReference = executionInput.SourceInputs.SingleOrDefault(item =>
                    item.ArtifactId == expansion.ExpansionId);
                if (request.Context.DeliveredExpansionByteFingerprint is null
                    || expansionReference is null
                    || expansionReference.ArtifactVersion != CandidateDeliveredInputIdentity.Version
                    || request.Context.DeliveredExpansionByteFingerprint != actualFingerprint
                    || expansionReference.Fingerprint != request.Context.DeliveredExpansionByteFingerprint
                    || !StringComparer.Ordinal.Equals(expansionReference.Availability, "retained"))
                {
                    throw new InvalidOperationException("Candidate delivered expansion bytes differ from the admitted source artifact reference.");
                }
            }
        }
        else if (request.Context.DeliveredInput is not null || request.Context.DeliveredExpansion is not null)
        {
            throw new InvalidOperationException("A delivered candidate input or expansion requires an admitted analysis execution input.");
        }
        foreach (ICandidatePopulationSource source in request.Sources)
        {
            DomainContractInvariants.Validate(source.Declaration);
            if (!StringComparer.Ordinal.Equals(source.Declaration.AnalyzerId, source.AnalyzerId.Value)
                || source.Declaration.OperationRequirements.Mode != ExecutionRequirement.LocalOnly
                || source.Declaration.ExpectedScaleAndCost.Billable)
            {
                throw new InvalidOperationException("WP3 candidate sources require matching local, non-billable analyzer declarations.");
            }
        }
        if (request.Sources.Count == 0
            || request.Limits.MaximumPopulationWork < 0
            || request.Limits.MaximumPopulationWork > 1_000_000
            || request.Limits.MaximumOptionalCandidates < 0
            || request.Limits.MaximumOptionalCandidates > 1_000_000
            || request.Sources.Select(item => item.AnalyzerId).Distinct().Count() != request.Sources.Count)
        {
            throw new InvalidOperationException("Candidate execution requires unique analyzers and non-negative closed limits.");
        }
    }

    private static void ValidateMember(CausalJoinPopulationMember member)
    {
        bool invalid = member.InputState == CausalJoinInputState.InvalidInput;
        bool failed = member.InputState == CausalJoinInputState.Failed;
        if (member.Lane == CandidateLane.Unspecified
            || StringComparer.Ordinal.Equals(member.SourceFactId.Value, "source-fact-unspecified")
            || member.Participants.Count > 16
            || (!invalid && !failed && member.Participants.Count < 2)
            || member.Participants.Any(item => string.IsNullOrWhiteSpace(item.Role)
                || item.Role.Length > 128
                || !IsAsciiToken(item.Role))
            || member.Participants.Select(item => item.Role).Distinct(StringComparer.Ordinal).Count() != member.Participants.Count
            || member.Path.Count > 64
            || (!invalid && !failed && member.Path.Count == 0)
            || (!invalid && !failed && member.Participants.Any(item => !member.Path.Contains(item.ParticipantId)))
            || member.DependencyIds.Count > 128
            || (!invalid && !failed && member.DependencyIds.Count == 0)
            || member.DependencyIds.Distinct().Count() != member.DependencyIds.Count
            || member.SupportingEvidenceIds.Count > 128
            || member.ContradictingEvidenceIds.Count > 128
            || member.ContradictingEvidenceIds.Distinct().Count() != member.ContradictingEvidenceIds.Count
            || member.MissingInformation.Count > 32
            || member.MissingInformation.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 1024)
            || (!invalid && !failed && member.SupportingEvidenceIds.Count == 0)
            || member.SupportingEvidenceIds.Distinct().Count() != member.SupportingEvidenceIds.Count
            || string.IsNullOrWhiteSpace(member.JoinKind)
            || member.JoinKind.Length > 128
            || !IsAsciiToken(member.JoinKind)
            || string.IsNullOrWhiteSpace(member.Rationale)
            || member.Rationale.Length > 4096
            || !IsStrictUtf8(member.Rationale)
            || string.IsNullOrWhiteSpace(member.PredictedImpact)
            || member.PredictedImpact.Length > 4096
            || !IsStrictUtf8(member.PredictedImpact)
            || member.MissingInformation.Any(item => !IsStrictUtf8(item))
            || (failed && (string.IsNullOrWhiteSpace(member.FailureCode)
                || member.FailureCode.Length > 128
                || !IsAsciiToken(member.FailureCode)
                || string.IsNullOrWhiteSpace(member.FailureMessage)
                || member.FailureMessage.Length > 512
                || !IsStrictUtf8(member.FailureMessage)))
            || (!failed && (member.FailureCode is not null || member.FailureMessage is not null))
            || (member.InputState == CausalJoinInputState.Ambiguous
                && member.ContradictingEvidenceIds.Count == 0
                && member.MissingInformation.Count == 0)
            || (member.EmitGap
                && member.InputState is CausalJoinInputState.Complete or CausalJoinInputState.Ambiguous
                && member.MissingInformation.Count == 0)
            || (member.Lane == CandidateLane.OptionalRanked && member.OptionalRank is null or <= 0)
            || (member.Lane != CandidateLane.OptionalRanked && member.OptionalRank is not null))
        {
            throw new InvalidDataException("A causal population member is not a closed bounded join.");
        }
    }

    private static bool IsAsciiToken(string value) => value.Length != 0
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or ':' or '/' or '-');

    private static bool IsStrictUtf8(string value)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static CausalJoinPopulationMember DeclarationFailureMember(
        ICandidatePopulationSource source,
        string message)
    {
        OpaqueId memberId = CandidateAnalysisIdentity.StableId(
            "candidate-population-declaration-failure",
            source.AnalyzerId.Value,
            source.Declaration.AnalyzerVersion.ToString(),
            source.Declaration.RulesetVersion.ToString());
        return new CausalJoinPopulationMember(
            memberId,
            source.AnalyzerId,
            CandidateLane.DeterministicRequired,
            [],
            "analyzer-population-declaration",
            [],
            [],
            [],
            [],
            ["declared eligible population"],
            CausalJoinInputState.Failed,
            "The analyzer failed before it could declare a bounded eligible population.",
            "No causal impact can be assessed until the analyzer population can be declared.",
            FailureCode: "analyzer-declaration-failed",
            FailureMessage: "The analyzer could not declare its bounded population.",
            EmitGap: true)
        { SourceFactId = memberId };
    }

    private static CausalJoinPopulationMember FailureMember(
        OpaqueId analyzerId,
        CausalJoinPopulationMember member,
        string failureCode,
        string message)
    {
        try
        {
            ValidateMember(member);
            return member with
            {
                AnalyzerId = analyzerId,
                MissingInformation = ["completed analyzer execution"],
                InputState = CausalJoinInputState.Failed,
                Rationale = "The analyzer failed while constructing this declared population member.",
                PredictedImpact = "No causal impact can be assessed until analyzer execution completes.",
                FailureCode = failureCode,
                FailureMessage = "The analyzer did not complete bounded candidate construction.",
                EmitGap = true,
            };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
        {
            return new CausalJoinPopulationMember(
                member.PopulationMemberId,
                analyzerId,
                CandidateLane.DeterministicRequired,
                [],
                "failed-causal-join",
                [], [], [], [],
                ["valid analyzer output"],
                CausalJoinInputState.Failed,
                "The analyzer returned malformed output and did not complete candidate construction.",
                "No causal impact can be assessed from malformed analyzer output.",
                FailureCode: "analyzer-output-invalid",
                FailureMessage: "The analyzer output failed bounded validation.",
                EmitGap: true)
            { SourceFactId = member.PopulationMemberId };
        }
    }

    private static CausalJoinPopulationMember ApplyDeclaredScope(
        ICandidatePopulationSource source,
        CausalJoinPopulationMember member)
    {
        if (source.Declaration.Scope.SupportedRecordFieldAssetShapes.Contains(member.JoinKind, StringComparer.Ordinal))
        {
            return member;
        }
        ReasonedAnalyzerScopeContract? excluded = source.Declaration.Scope.ExcludedRecordFieldAssetShapes
            .FirstOrDefault(item => StringComparer.Ordinal.Equals(item.ScopeId, member.JoinKind));
        return excluded is null
            ? InvalidMember(source.AnalyzerId, member, $"relationship kind '{member.JoinKind}' is not declared")
            : member with
            {
                InputState = CausalJoinInputState.Unsupported,
                MissingInformation = member.MissingInformation
                    .Append(excluded.Reason)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
            };
    }

    private static CausalJoinPopulationMember InvalidMember(
        OpaqueId analyzerId,
        CausalJoinPopulationMember member,
        string reason) => member with
        {
            AnalyzerId = analyzerId,
            SourceFactId = member.SourceFactId.Value == "source-fact-unspecified"
            ? member.PopulationMemberId
            : member.SourceFactId,
            Lane = CandidateLane.DeterministicRequired,
            Participants = [],
            JoinKind = "invalid-causal-join",
            Path = [],
            DependencyIds = [],
            SupportingEvidenceIds = [],
            ContradictingEvidenceIds = [],
            MissingInformation = ["valid bounded causal-join input"],
            InputState = CausalJoinInputState.InvalidInput,
            Rationale = "The declared population member failed bounded causal-join validation.",
            PredictedImpact = "Invalid input prevents a bounded downstream causal assessment.",
            OptionalRank = null,
            FailureCode = null,
            FailureMessage = null,
            EmitGap = false,
        };

    private static int LaneOrder(CandidateLane lane) => lane switch
    {
        CandidateLane.DeterministicRequired => 0,
        CandidateLane.MandatoryEvidence => 1,
        CandidateLane.OptionalRanked => 2,
        _ => throw new InvalidOperationException("Candidate lane is not closed."),
    };

    private static string Bound(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
}
