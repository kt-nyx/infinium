using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Application.ScopeReversion;

public enum ScopeReversionV2ReplayMode
{
    Unspecified,
    Clean,
    Incremental,
    RetainedDownstream,
    AuditOnlyUnavailable,
}

public enum ScopeReversionV2CleanReplayAvailability
{
    Available,
    Unavailable,
}

public sealed record ScopeReversionV2PersistencePhaseResult(
    ScopeReversionV2ReplayMode Mode,
    string ExecutionState,
    ScopeReversionV2CleanReplayAvailability CleanReplayAvailability,
    ScopeReversionV2AnalysisContract Analysis,
    ScopeReversionV2PersistenceReceipt Receipt,
    byte[] CanonicalJson,
    string HumanSummary);

public static class ScopeReversionV2PersistencePhase
{
    public static ScopeReversionV2PersistencePhaseResult ExecuteAndPublish(
        AuthoritativeStore store,
        ScopeReversionV2ProjectionRequest request,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> retainedArtifacts,
        DateTimeOffset now,
        ScopeReversionV2ReplayMode mode = ScopeReversionV2ReplayMode.Clean,
        string? retainedPayloadId = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(retainedArtifacts);
        if (mode is ScopeReversionV2ReplayMode.Unspecified or ScopeReversionV2ReplayMode.AuditOnlyUnavailable)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        if (retainedArtifacts.Count is < 2 or > 128
            || retainedArtifacts.Any(item => string.IsNullOrWhiteSpace(item.Key) || item.Value.IsEmpty))
        {
            throw new InvalidDataException("Scope-reversion v2 replay requires bounded retained structural and source artifacts.");
        }

        ScopeReversionV2WorkAssignmentContract assignment = ControlledRealScopeReversionProjector.Project(request);
        if (mode is ScopeReversionV2ReplayMode.Incremental or ScopeReversionV2ReplayMode.RetainedDownstream)
        {
            string retainedId = retainedPayloadId
                ?? throw new InvalidDataException("A retained payload identity is required for non-clean replay.");
            ScopeReversionV2AnalysisContract retained = ReadValidated(store, retainedId);
            bool exact = retained.AssignmentId == assignment.AssignmentId
                && retained.SnapshotId == assignment.SnapshotId
                && retained.ContextId == assignment.ContextId
                && retained.ConfigurationId == assignment.ConfigurationId
                && retained.ExecutionInputId == assignment.ExecutionInputId
                && retained.InputManifestFingerprint == assignment.InputManifestFingerprint
                && store.ReadScopeReversionV2Invalidations(retainedId).Count == 0
                && retainedArtifacts.All(item => store.GetScopeReversionV2Artifact(item.Key).AsSpan()
                    .SequenceEqual(item.Value.Span));
            if (exact)
            {
                byte[] retainedBytes = store.ReadScopeReversionV2AnalysisBytes(retainedId);
                ScopeReversionV2PersistenceReceipt retainedReceipt = store.PublishScopeReversionV2Analysis(
                    new ScopeReversionV2PublicationRequest(retained, retainedBytes,
                        retainedArtifacts.OrderBy(item => item.Key, StringComparer.Ordinal)
                            .Select(item => new ScopeReversionV2RetainedArtifact(
                                item.Key, "retained-project-artifact", item.Value)).ToArray(), now));
                return new(mode,
                    mode == ScopeReversionV2ReplayMode.Incremental
                        ? "reused-incremental" : "reproduced-retained-downstream",
                    ScopeReversionV2CleanReplayAvailability.Available,
                    retained, retainedReceipt, retainedBytes, ScopeReversionV2OutputRenderer.RenderHuman(retained));
            }
            if (mode == ScopeReversionV2ReplayMode.RetainedDownstream)
            {
                throw new InvalidDataException("Retained-downstream replay provenance or retained bytes drifted.");
            }
        }

        ScopeReversionV2PipelineResult pipeline = ControlledRealScopeReversionProjector.Execute(request);
        ScopeReversionV2RetainedArtifact[] artifacts = retainedArtifacts.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new ScopeReversionV2RetainedArtifact(item.Key, "retained-project-artifact", item.Value))
            .ToArray();
        ScopeReversionV2PersistenceReceipt receipt = store.PublishScopeReversionV2Analysis(
            new ScopeReversionV2PublicationRequest(pipeline.Analysis, pipeline.CanonicalJson, artifacts, now));
        ScopeReversionV2AnalysisContract readback = ReadValidated(store, receipt.PayloadId);
        byte[] readbackBytes = store.ReadScopeReversionV2AnalysisBytes(receipt.PayloadId);
        if (!readbackBytes.AsSpan().SequenceEqual(pipeline.CanonicalJson)
            || retainedArtifacts.Any(item => !store.GetScopeReversionV2Artifact(item.Key).AsSpan()
                .SequenceEqual(item.Value.Span)))
        {
            throw new InvalidDataException("Scope-reversion v2 publication did not reopen exactly.");
        }
        return new(mode, mode == ScopeReversionV2ReplayMode.Clean ? "executed-clean" : "executed-incremental",
            ScopeReversionV2CleanReplayAvailability.Available, readback, receipt, readbackBytes,
            ScopeReversionV2OutputRenderer.RenderHuman(readback));
    }

    public static ScopeReversionV2PersistencePhaseResult ReadAuditOnlyUnavailable(
        AuthoritativeStore store,
        string retainedPayloadId)
    {
        ArgumentNullException.ThrowIfNull(store);
        ScopeReversionV2AnalysisContract retained = ReadValidated(store, retainedPayloadId);
        byte[] bytes = store.ReadScopeReversionV2AnalysisBytes(retainedPayloadId);
        ScopeReversionV2PersistenceReceipt receipt = store.ReadScopeReversionV2Receipt(retainedPayloadId);
        return new(ScopeReversionV2ReplayMode.AuditOnlyUnavailable, "audit-only-no-publication",
            ScopeReversionV2CleanReplayAvailability.Unavailable, retained, receipt, bytes,
            ScopeReversionV2OutputRenderer.RenderHuman(retained)
            + Environment.NewLine + "Clean replay: Unavailable (the exact owner-supplied root was not reopened)." + Environment.NewLine);
    }

    public static ScopeReversionV2AnalysisContract ReadValidated(AuthoritativeStore store, string payloadId)
    {
        byte[] bytes = store.ReadScopeReversionV2AnalysisBytes(payloadId);
        ScopeReversionV2AnalysisContract analysis = ScopeReversionV2JsonCodec.Deserialize(bytes);
        if (analysis.PayloadId.Value != payloadId
            || !ScopeReversionV2JsonCodec.Serialize(analysis).AsSpan().SequenceEqual(bytes))
        {
            throw new InvalidDataException("Scope-reversion v2 retained payload is non-canonical or identity-drifted.");
        }
        return analysis;
    }
}
