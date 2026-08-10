using System.Diagnostics;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Application.Analysis;

public static class AnalysisExecutionPhase
{
    public const string PhaseId = "m1-s5-wp5-analysis-publication";
    public const string PhaseVersion = "1.0.0";

    public static AnalysisExecutionPhaseResult Execute(
        AuthoritativeStore store,
        AnalysisV1WorkAssignment assignment,
        AttemptRecord attempt,
        RunBinding binding,
        string validationReceiptPayloadId,
        DateTimeOffset now,
        Action<string>? failureInjection = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        Stopwatch elapsed = Stopwatch.StartNew();
        TimeSpan phaseLimit = TimeSpan.FromMilliseconds(assignment.ExecutionInput.Limits.MaximumWallTimeMilliseconds);
        void RequireTime()
        {
            if (elapsed.Elapsed >= phaseLimit)
            {
                throw new AnalysisOutputLimitException("The coordinator analysis phase exceeded its wall-time authority.");
            }
        }
        AnalysisPublicationBuilder.ValidateAssignment(assignment);
        if (assignment.ExecutionInput.RunId.Value != attempt.RunId
            || assignment.ExecutionInput.InstallationSnapshot.ArtifactId.Value != binding.InstallationSnapshotId
            || assignment.AnalysisContextId != binding.AnalysisContextId
            || assignment.ExecutionInput.EffectiveConfiguration.ArtifactId.Value != binding.EffectiveScanConfigurationId
            || assignment.ExecutionInput.ResolvedInputManifest.ArtifactId.Value != binding.ResolvedInputManifestId)
        {
            throw new AnalysisIdentityDriftException(
                "The analysis-v1 assignment differs from the immutable run binding.");
        }

        byte[] documentation = ReadExact(store, assignment.DocumentationEvidence);
        byte[] candidates = ReadExact(store, assignment.CandidateAnalysis);
        byte[] findingCases = ReadExact(store, assignment.FindingCase);
        RequireTime();
        string? comparedFingerprint = assignment.ExecutionInput.PriorRunId is null
            ? null
            : store.GetAnalysisSemanticFingerprint(assignment.ExecutionInput.PriorRunId.Value);
        if (assignment.ExecutionInput.PriorRunId is not null && comparedFingerprint is null)
        {
            throw new AnalysisIdentityDriftException(
                "The incremental or replay comparison run has no retained semantic output identity.");
        }

        AnalysisPublicationBundle bundle;
        TimeSpan remaining = phaseLimit - elapsed.Elapsed;
        if (remaining <= TimeSpan.Zero)
        {
            throw new AnalysisOutputLimitException("The coordinator analysis phase exceeded its wall-time authority.");
        }
        using CancellationTokenSource phaseCancellation = new(remaining);
        try
        {
            bundle = Task.Run(() => AnalysisPublicationBuilder.Build(
                    assignment, documentation, candidates, findingCases, now, comparedFingerprint,
                    phaseCancellation.Token), phaseCancellation.Token)
                .GetAwaiter().GetResult();
        }
        catch (AnalysisInputAdmissionException exception)
        {
            throw new AnalysisIdentityDriftException(
                "Retained analysis inputs failed exact version, dependency, or identity admission.", exception);
        }
        catch (OperationCanceledException exception) when (phaseCancellation.IsCancellationRequested)
        {
            throw new AnalysisOutputLimitException(
                "The coordinator analysis phase exceeded its wall-time authority.", exception);
        }
        RequireTime();

        return PublishBundle(store, assignment, attempt, binding, validationReceiptPayloadId, now, bundle, failureInjection);
    }

    public static AnalysisExecutionPhaseResult PublishTerminalFallback(
        AuthoritativeStore store,
        AnalysisV1WorkAssignment assignment,
        AttemptRecord attempt,
        RunBinding binding,
        string validationReceiptPayloadId,
        AnalysisTerminalOutcome outcome,
        string reason,
        DateTimeOffset now)
    {
        AnalysisPublicationBundle bundle = AnalysisTerminalFallbackBuilder.Build(assignment, outcome, reason, now);
        return PublishBundle(store, assignment with { TerminalOutcome = outcome, TerminalReason = reason },
            attempt, binding, validationReceiptPayloadId, now, bundle, null);
    }

    internal static AnalysisExecutionPhaseResult PublishPreparedBundleForVerification(
        AuthoritativeStore store,
        AnalysisV1WorkAssignment assignment,
        AttemptRecord attempt,
        RunBinding binding,
        string validationReceiptPayloadId,
        DateTimeOffset now,
        AnalysisPublicationBundle bundle,
        Action<string>? failureInjection = null) =>
        PublishBundle(store, assignment, attempt, binding, validationReceiptPayloadId, now, bundle, failureInjection);

    private static AnalysisExecutionPhaseResult PublishBundle(
        AuthoritativeStore store,
        AnalysisV1WorkAssignment assignment,
        AttemptRecord attempt,
        RunBinding binding,
        string validationReceiptPayloadId,
        DateTimeOffset now,
        AnalysisPublicationBundle bundle,
        Action<string>? failureInjection)
    {

        byte[] replayBytes = AnalysisReplayJsonCodec.Serialize(bundle.Replay);
        byte[] outputBytes = RunOutputJsonCodec.Serialize(bundle.RunOutput);
        byte[] cliBytes = CliSummaryJsonCodec.Serialize(bundle.CliSummary);
        byte[] boundaryBytes = JsonSerializer.SerializeToUtf8Bytes(bundle.ExternalBoundaries);
        AnalysisArtifactPersistenceRecord[] artifacts = bundle.Artifacts
            .OrderBy(item => item.ArtifactId, StringComparer.Ordinal)
            .Select(item => new AnalysisArtifactPersistenceRecord(
                item.ArtifactId, item.Kind, item.SchemaId, item.SchemaVersion, item.Revision,
                item.State, item.ContentSha256, item.ByteLength, item.ProvenanceId, item.DependencyClosureId))
            .ToArray();
        byte[] artifactIndexBytes = JsonSerializer.SerializeToUtf8Bytes(artifacts);
        if (!outputBytes.AsSpan().SequenceEqual(RunOutputJsonCodec.Serialize(RunOutputJsonCodec.Deserialize(outputBytes)))
            || !replayBytes.AsSpan().SequenceEqual(AnalysisReplayJsonCodec.Serialize(AnalysisReplayJsonCodec.Deserialize(replayBytes)))
            || !cliBytes.AsSpan().SequenceEqual(CliSummaryJsonCodec.Serialize(CliSummaryJsonCodec.Deserialize(cliBytes))))
        {
            throw new InvalidDataException("WP5 publication documents failed canonical round-trip verification.");
        }

        LifecycleState terminal = assignment.TerminalOutcome switch
        {
            AnalysisTerminalOutcome.Completed => bundle.CliSummary.Outcome == "completed"
                ? LifecycleState.Completed : LifecycleState.CompletedWithGaps,
            AnalysisTerminalOutcome.CompletedWithGaps => LifecycleState.CompletedWithGaps,
            AnalysisTerminalOutcome.Cancelled => LifecycleState.Cancelled,
            AnalysisTerminalOutcome.LimitReached => LifecycleState.LimitReached,
            _ => LifecycleState.Failed,
        };
        AnalysisPublicationPersistenceReceipt receipt = store.PublishAnalysisResult(
            new AnalysisPublicationPersistenceRequest(
                attempt, binding, bundle.Replay, bundle.RunOutput, replayBytes, outputBytes, cliBytes,
                boundaryBytes, artifactIndexBytes, artifacts, bundle.DependencyClosureId,
                bundle.SemanticOutputFingerprint, terminal, assignment.TerminalReason,
                validationReceiptPayloadId, now),
            failureInjection);
        return new AnalysisExecutionPhaseResult(bundle, receipt);
    }

    private static byte[] ReadExact(AuthoritativeStore store, RetainedAnalysisPayloadSeal seal)
    {
        RetainedPayloadRecord retained;
        byte[] bytes;
        try
        {
            retained = store.GetRetainedPayload(seal.PayloadId);
            bytes = store.ReadCandidateAnalysisPayload(seal.PayloadId);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidDataException)
        {
            throw new AnalysisIdentityDriftException(
                $"Retained payload '{seal.PayloadId}' is unavailable.", exception);
        }
        if (retained.Sha256 != seal.Sha256
            || retained.ByteLength != seal.ByteLength
            || bytes.LongLength != seal.ByteLength)
        {
            throw new AnalysisIdentityDriftException(
                $"Retained payload '{seal.PayloadId}' drifted from its admitted seal.");
        }
        return bytes;
    }
}

public sealed record AnalysisExecutionPhaseResult(
    AnalysisPublicationBundle Bundle,
    AnalysisPublicationPersistenceReceipt Receipt);

public sealed class AnalysisIdentityDriftException : Exception
{
    public AnalysisIdentityDriftException(string message) : base(message) { }
    public AnalysisIdentityDriftException(string message, Exception innerException) : base(message, innerException) { }
}
