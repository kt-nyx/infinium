using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.Candidates;

public static partial class CandidatePipeline
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
            throw new InvalidDataException("Candidate population exceeds the bounded population contract.");
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

}
