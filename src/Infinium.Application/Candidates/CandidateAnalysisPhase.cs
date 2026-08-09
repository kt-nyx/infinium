using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Application.Candidates;

public static class CandidateAnalysisPhase
{
    public const string PhaseId = "m1-s5-wp3-candidate-analysis";
    public const string PhaseVersion = "1.0.0";

    public static CandidateAnalysisPhaseResult Execute(
        AuthoritativeStore store,
        CandidatePipelineRequest request,
        AttemptRecord attempt,
        RunBinding binding,
        DateTimeOffset now,
        CandidateCheckpointState? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (!StringComparer.Ordinal.Equals(request.OriginatingRunId.Value, attempt.RunId)
            || request.ExecutionInput is null
            || request.ExecutionInput.RunId != request.OriginatingRunId
            || request.ExecutionInput.InstallationSnapshot.ArtifactId.Value != binding.InstallationSnapshotId
            || request.ExecutionInput.EffectiveConfiguration.ArtifactId.Value != binding.EffectiveScanConfigurationId
            || request.ExecutionInput.ResolvedInputManifest.ArtifactId.Value != binding.ResolvedInputManifestId
            || request.Context.OriginatingRunId != request.OriginatingRunId
            || request.Context.SourceSnapshotId?.Value != binding.InstallationSnapshotId
            || request.Context.AnalysisContextId?.Value != binding.AnalysisContextId
            || request.Context.ConfigurationId?.Value != binding.EffectiveScanConfigurationId)
        {
            throw new InvalidOperationException("Candidate phase request, attempt, and immutable run binding differ.");
        }
        store.EnsureCandidateAttemptIsCurrent(attempt, binding);
        CandidatePipelineResult pipeline = CandidatePipeline.Execute(request, checkpoint);
        byte[] payload = SerializeAnalysis(pipeline.Analysis);
        byte[] checkpointBytes = SerializeCheckpoint(pipeline.Checkpoint);
        string checkpointSha = Convert.ToHexStringLower(SHA256.HashData(checkpointBytes));
        string checkpointId = CandidateAnalysisIdentity.StableId(
            "candidate-checkpoint",
            request.OriginatingRunId.Value,
            attempt.AttemptId,
            pipeline.Analysis.PayloadId.Value,
            checkpointSha).Value;
        string dependencyClosureId = "candidate-checkpoint-" + pipeline.Metrics.StructuralHash.Value[..32];
        CandidatePhasePersistenceReceipt phaseReceipt = store.PublishCandidatePhase(
            pipeline.Analysis,
            payload,
            new CandidatePhaseCheckpointPublication(
                checkpointId, dependencyClosureId, checkpointSha, checkpointBytes,
                JsonSerializer.Serialize(
                new CandidateCheckpointPendingEnvelope(
                    pipeline.Analysis.Counts.Unprocessed,
                    pipeline.Analysis.Counts.Limited,
                    pipeline.Analysis.Gaps.Select(item => item.GapId.Value).Order(StringComparer.Ordinal).ToArray()),
                    Slice5ContractJsonCodec.JsonOptions)),
            attempt,
            binding,
            now);
        return new CandidateAnalysisPhaseResult(pipeline, phaseReceipt.Analysis, payload, checkpointId, checkpointSha);
    }

    private static byte[] SerializeAnalysis(CandidateAnalysisContract analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        byte[] payload = CandidateAnalysisJsonCodec.Serialize(analysis);
        CandidateAnalysisContract roundTrip = CandidateAnalysisJsonCodec.Deserialize(payload);
        byte[] canonicalRoundTrip = CandidateAnalysisJsonCodec.Serialize(roundTrip);
        if (!payload.AsSpan().SequenceEqual(canonicalRoundTrip)
            || roundTrip.PayloadId != analysis.PayloadId)
        {
            throw new InvalidDataException("Candidate publication bytes do not round-trip to the exact aggregate contract.");
        }
        return payload;
    }

    public static CandidateCheckpointState? ReadLatestCheckpoint(
        AuthoritativeStore store,
        string runId)
    {
        ArgumentNullException.ThrowIfNull(store);
        CandidateCheckpointPersistenceRecord? record = store.ReadLatestCandidateCheckpoint(runId);
        if (record is null)
        {
            return null;
        }
        CandidateCheckpointPayloadReference reference = JsonSerializer.Deserialize<CandidateCheckpointPayloadReference>(
            record.CompletedPartitionsJson,
            Slice5ContractJsonCodec.JsonOptions)
            ?? throw new InvalidDataException("Candidate checkpoint payload reference is empty.");
        if (!StringComparer.Ordinal.Equals(reference.Sha256, record.ContentSha256)
            || reference.ByteLength < 1
            || reference.ByteLength > 64L * 1024 * 1024)
        {
            throw new InvalidDataException("Candidate checkpoint payload reference is inconsistent.");
        }
        byte[] bytes = store.ReadCandidateCheckpointPayload(reference.PayloadId);
        string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (bytes.LongLength != reference.ByteLength || !StringComparer.Ordinal.Equals(sha, reference.Sha256))
        {
            throw new InvalidDataException("Candidate checkpoint failed content identity validation.");
        }
        CandidateCheckpointEnvelope envelope = JsonSerializer.Deserialize<CandidateCheckpointEnvelope>(
            bytes,
            Slice5ContractJsonCodec.JsonOptions)
            ?? throw new InvalidDataException("Candidate checkpoint is empty.");
        CandidateCheckpointState state = new(
            envelope.OriginatingRunId,
            envelope.PopulationId,
            envelope.PolicyId,
            envelope.ThresholdId,
            envelope.LimitId,
            envelope.LimitsFingerprint,
            envelope.OptionalFrontierFingerprint,
            envelope.WorkFrontierFingerprint,
            envelope.AnalyzerSetFingerprint,
            envelope.PolicyFingerprint,
            envelope.ThresholdFingerprint,
            envelope.ExecutionInputFingerprint,
            envelope.Outcomes.ToDictionary(item => item.PopulationMemberId, item => item.Outcome));
        if (!StringComparer.Ordinal.Equals(state.OriginatingRunId.Value, runId)
            || SerializeCheckpoint(state).AsSpan().SequenceEqual(bytes) is false)
        {
            throw new InvalidDataException("Candidate checkpoint is not canonically serialized.");
        }
        return state;
    }

    private static byte[] SerializeCheckpoint(CandidateCheckpointState checkpoint)
    {
        CandidateCheckpointEnvelope envelope = new(
            checkpoint.OriginatingRunId,
            checkpoint.PopulationId,
            checkpoint.PolicyId,
            checkpoint.ThresholdId,
            checkpoint.LimitId,
            checkpoint.LimitsFingerprint,
            checkpoint.OptionalFrontierFingerprint,
            checkpoint.WorkFrontierFingerprint,
            checkpoint.AnalyzerSetFingerprint,
            checkpoint.PolicyFingerprint,
            checkpoint.ThresholdFingerprint,
            checkpoint.ExecutionInputFingerprint,
            checkpoint.Outcomes
                .OrderBy(item => item.Key.Value, StringComparer.Ordinal)
                .Select(item => new CandidateCheckpointEntryEnvelope(item.Key, item.Value))
                .ToArray());
        return JsonSerializer.SerializeToUtf8Bytes(envelope, Slice5ContractJsonCodec.JsonOptions);
    }

    private sealed record CandidateCheckpointEnvelope(
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
        IReadOnlyList<CandidateCheckpointEntryEnvelope> Outcomes);

    private sealed record CandidateCheckpointEntryEnvelope(
        OpaqueId PopulationMemberId,
        CandidateMemberOutcome Outcome);

    private sealed record CandidateCheckpointPendingEnvelope(
        long Unprocessed,
        long Limited,
        IReadOnlyList<string> GapIds);

    private sealed record CandidateCheckpointPayloadReference(
        string PayloadId,
        string Sha256,
        long ByteLength);
}

public sealed record CandidateAnalysisPhaseResult(
    CandidatePipelineResult Pipeline,
    CandidateAnalysisPersistenceReceipt Receipt,
    byte[] SerializedPayload,
    string CheckpointId,
    string CheckpointSha256);
