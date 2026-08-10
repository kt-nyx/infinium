using Infinium.Domain.Contracts;

namespace Infinium.Persistence;

public sealed record RetainedPayloadRecord(
    string PayloadId,
    string Sha256,
    long ByteLength,
    string RelativePath);

public sealed record AnalysisArtifactPersistenceRecord(
    string ArtifactId,
    string Kind,
    string SchemaId,
    string SchemaVersion,
    long Revision,
    string State,
    string ContentSha256,
    long ByteLength,
    string ProvenanceId,
    string DependencyClosureId,
    long Rank = 0,
    long UpdatedTick = 0);

public enum AnalysisArtifactSortOrder
{
    IdentityAscending,
    RankDescendingIdentityAscending,
    UpdatedTickDescendingIdentityDescending,
}

public sealed record AnalysisArtifactCursorKey(long Rank, long UpdatedTick, string ArtifactId);

public sealed record AnalysisCancellationPublicationAdmission(
    AttemptRecord Attempt,
    string ValidationReceiptPayloadId);

public sealed record AnalysisPublicationPersistenceRequest(
    AttemptRecord Attempt,
    RunBinding Binding,
    AnalysisReplayContract Replay,
    RunOutputContract RunOutput,
    ReadOnlyMemory<byte> ReplayBytes,
    ReadOnlyMemory<byte> RunOutputBytes,
    ReadOnlyMemory<byte> CliSummaryBytes,
    ReadOnlyMemory<byte> BoundaryReceiptBytes,
    ReadOnlyMemory<byte> ArtifactIndexBytes,
    IReadOnlyList<AnalysisArtifactPersistenceRecord> Artifacts,
    string DependencyClosureId,
    string SemanticOutputFingerprint,
    LifecycleState TerminalState,
    string TerminalReason,
    string ValidationReceiptPayloadId,
    DateTimeOffset PublishedAt);

public sealed record AnalysisPublicationPersistenceReceipt(
    string RunId,
    string ReplayManifestId,
    string RunOutputId,
    string ReplayPayloadId,
    string RunOutputPayloadId,
    string CliSummaryPayloadId,
    string BoundaryReceiptPayloadId,
    string ArtifactIndexPayloadId,
    string SemanticOutputFingerprint,
    LifecycleState TerminalState,
    long TerminalGeneration);

public sealed record AnalysisSummaryPersistenceRecord(
    string RunId,
    long FindingCount,
    long SupportedCaseCount,
    long LeadOnlyCaseCount,
    long CandidateDecisionCount,
    long CoveragePopulationCount,
    long GapCount,
    long UnsupportedCount,
    string ReplayManifestId,
    string ReplayState,
    string AuditabilityState,
    bool SemanticallyEquivalent,
    long DependencyCount,
    long MissingDependencyCount,
    long CoverageGapCount,
    string ProjectionVersion);

public sealed record AnalysisReplayPersistenceRecord(
    string ReplayManifestId,
    string ReplayState,
    string AuditabilityState,
    bool SemanticallyEquivalent,
    long DependencyCount,
    long MissingDependencyCount,
    long CoverageGapCount);

public sealed record AnalysisArtifactPagePersistenceRecord(
    IReadOnlyList<AnalysisArtifactPersistenceRecord> Items,
    bool HasMore,
    AnalysisArtifactCursorKey? NextKey);

public static class AnalysisArtifactKeysetPaginator
{
    public static AnalysisArtifactPagePersistenceRecord Page(
        IEnumerable<AnalysisArtifactPersistenceRecord> source,
        IReadOnlySet<string> kinds,
        IReadOnlySet<string> states,
        int maximumCount,
        AnalysisArtifactSortOrder sortOrder,
        AnalysisArtifactCursorKey? after)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (maximumCount is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }
        IEnumerable<AnalysisArtifactPersistenceRecord> filtered = source
            .Where(item => kinds.Count == 0 || kinds.Contains(item.Kind))
            .Where(item => states.Count == 0 || states.Contains(item.State));
        IOrderedEnumerable<AnalysisArtifactPersistenceRecord> ordered = sortOrder switch
        {
            AnalysisArtifactSortOrder.IdentityAscending =>
                filtered.OrderBy(item => item.ArtifactId, StringComparer.Ordinal),
            AnalysisArtifactSortOrder.RankDescendingIdentityAscending =>
                filtered.OrderByDescending(item => item.Rank).ThenBy(item => item.ArtifactId, StringComparer.Ordinal),
            AnalysisArtifactSortOrder.UpdatedTickDescendingIdentityDescending =>
                filtered.OrderByDescending(item => item.UpdatedTick).ThenByDescending(item => item.ArtifactId, StringComparer.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(sortOrder)),
        };
        IEnumerable<AnalysisArtifactPersistenceRecord> query = after is null
            ? ordered
            : ordered.SkipWhile(item => !SameKey(item, after)).Skip(1);
        AnalysisArtifactPersistenceRecord[] page = query.Take(maximumCount + 1).ToArray();
        bool hasMore = page.Length > maximumCount;
        AnalysisArtifactPersistenceRecord[] items = page.Take(maximumCount).ToArray();
        return new AnalysisArtifactPagePersistenceRecord(
            items, hasMore,
            hasMore ? new AnalysisArtifactCursorKey(items[^1].Rank, items[^1].UpdatedTick, items[^1].ArtifactId) : null);
    }

    private static bool SameKey(AnalysisArtifactPersistenceRecord item, AnalysisArtifactCursorKey cursor) =>
        item.Rank == cursor.Rank && item.UpdatedTick == cursor.UpdatedTick && item.ArtifactId == cursor.ArtifactId;
}
