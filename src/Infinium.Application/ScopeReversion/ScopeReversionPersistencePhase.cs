using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Application.ScopeReversion;

public enum ScopeReversionReplayMode
{
    Unspecified,
    Clean,
    Incremental,
    RetainedDownstream,
}

public sealed record ScopeReversionPersistencePhaseResult(
    ScopeReversionReplayMode Mode,
    string Disposition,
    ScopeReversionAnalysisContract Analysis,
    ScopeReversionPersistenceReceipt Receipt,
    byte[] CanonicalJson,
    string HumanSummary);

public static class ScopeReversionPersistencePhase
{
    public static ScopeReversionPersistencePhaseResult ExecuteAndPublish(
        AuthoritativeStore store,
        ScopeReversionCompositionRequest request,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> retainedArtifacts,
        DateTimeOffset now,
        ScopeReversionReplayMode mode = ScopeReversionReplayMode.Clean,
        string? retainedPayloadId = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        if (mode == ScopeReversionReplayMode.Unspecified)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        ScopeReversionWorkAssignmentContract assignment = ScopeReversionComposition.Compose(request);
        if ((mode is ScopeReversionReplayMode.Incremental or ScopeReversionReplayMode.RetainedDownstream)
            && retainedPayloadId is null)
        {
            throw new InvalidDataException("Incremental and retained-downstream scope replay require an exact retained payload identity.");
        }
        if (mode is ScopeReversionReplayMode.Incremental or ScopeReversionReplayMode.RetainedDownstream)
        {
            string retainedId = retainedPayloadId!;
            ScopeReversionAnalysisContract retained = ReadValidated(store, retainedId);
            if (retained.InputFingerprint == assignment.InputFingerprint
                && retained.Analyzer.DeclarationFingerprint == assignment.Analyzer.DeclarationFingerprint
                && store.ReadScopeReversionInvalidations(retainedId).Count == 0)
            {
                HashSet<string> retainedRequired = retained.DependencyEdges
                    .Where(item => item.ToKind is "dependency" or "evidence")
                    .Select(item => item.ToId.Value)
                    .ToHashSet(StringComparer.Ordinal);
                if (retainedArtifacts.Count != retainedRequired.Count
                    || retainedRequired.Any(id => !retainedArtifacts.TryGetValue(id, out ReadOnlyMemory<byte> supplied)
                        || !store.GetScopeReversionArtifact(id).AsSpan().SequenceEqual(supplied.Span)))
                {
                    throw new InvalidDataException(
                        "Scope-reversion replay dependencies differ from the exact retained artifact set or bytes.");
                }
                byte[] retainedBytes = store.ReadScopeReversionAnalysisBytes(retainedId);
                IReadOnlyList<ScopeReversionArtifactRecord> retainedArtifactRecords =
                    store.ListScopeReversionArtifacts(retainedId);
                if (!retainedArtifactRecords.Select(item => item.ArtifactId).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(retainedRequired.Append(retainedId)))
                {
                    throw new InvalidDataException(
                        "Scope-reversion replay cannot resolve its exact retained payload and dependency artifact set.");
                }
                ScopeReversionPersistenceReceipt retainedReceipt = new(
                    retained.PayloadId.Value,
                    retained.OriginatingRunId.Value,
                    retained.AssignmentId.Value,
                    Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(retainedBytes)),
                    retainedBytes.LongLength,
                    ContractJsonSerializer.Fingerprint(new
                    {
                        retained.Decisions,
                        retained.Candidates,
                        retained.Hypotheses,
                        retained.Contradictions,
                        retained.Abstentions,
                        retained.Gaps,
                        retained.Failures,
                        retained.Findings,
                        retained.Cases,
                        retained.Recommendations,
                        retained.Taxonomy,
                        retained.Coverage,
                        retained.DependencyEdges,
                        retained.PublicationClaimBoundary,
                    }).Value,
                    retainedArtifactRecords.Select(item => item.ArtifactId).ToArray());
                return new ScopeReversionPersistencePhaseResult(
                    mode,
                    mode == ScopeReversionReplayMode.Incremental ? "reused-incremental" : "reused-retained-downstream",
                    retained,
                    retainedReceipt,
                    retainedBytes,
                    ScopeReversionOutputRenderer.RenderHuman(retained));
            }
        }

        ScopeReversionAnalysisContract analysis = Infinium.Analysis.ScopeReversion.ScopeReversionAnalyzer.Execute(assignment);
        byte[] bytes = ScopeReversionJsonCodec.Serialize(analysis);
        HashSet<string> required = analysis.DependencyEdges
            .Where(item => item.ToKind is "dependency" or "evidence")
            .Select(item => item.ToId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (required.Any(item => !retainedArtifacts.ContainsKey(item)))
        {
            throw new InvalidDataException("Scope-reversion publication cannot resolve every evidence and dependency ID to retained bytes.");
        }
        ScopeReversionRetainedArtifact[] artifacts = required.Order(StringComparer.Ordinal)
            .Select(id => new ScopeReversionRetainedArtifact(
                id,
                analysis.DependencyEdges.Any(edge => edge.ToId.Value == id && edge.ToKind == "evidence")
                    ? "evidence" : "dependency",
                retainedArtifacts[id]))
            .ToArray();
        ScopeReversionPersistenceReceipt receipt = store.PublishScopeReversionAnalysis(
            new ScopeReversionPublicationRequest(analysis, bytes, artifacts, now));
        ScopeReversionAnalysisContract readback = ReadValidated(store, receipt.PayloadId);
        byte[] readbackBytes = store.ReadScopeReversionAnalysisBytes(receipt.PayloadId);
        if (!bytes.AsSpan().SequenceEqual(readbackBytes)
            || readback.PayloadId != analysis.PayloadId
            || required.Any(id => !store.GetScopeReversionArtifact(id).AsSpan().SequenceEqual(retainedArtifacts[id].Span)))
        {
            throw new InvalidDataException("Scope-reversion persistence did not preserve exact payload or dependency bytes.");
        }
        return new ScopeReversionPersistencePhaseResult(
            mode,
            "executed-and-published",
            readback,
            receipt,
            readbackBytes,
            ScopeReversionOutputRenderer.RenderHuman(readback));
    }

    public static ScopeReversionAnalysisContract ReadValidated(AuthoritativeStore store, string payloadId)
    {
        ArgumentNullException.ThrowIfNull(store);
        byte[] bytes = store.ReadScopeReversionAnalysisBytes(payloadId);
        ScopeReversionAnalysisContract analysis = ScopeReversionJsonCodec.Deserialize(bytes);
        if (analysis.PayloadId.Value != payloadId
            || !ScopeReversionJsonCodec.Serialize(analysis).AsSpan().SequenceEqual(bytes))
        {
            throw new InvalidDataException("Retained scope-reversion payload identity or canonical bytes drifted.");
        }
        return analysis;
    }
}
