namespace Infinium.Persistence;

public enum AnalysisArtifactCursorBindingDisposition
{
    Accepted,
    Expired,
    ScopeMismatch,
    PublicationMismatch,
    QueryMismatch,
    SortMismatch,
}

public sealed record AnalysisArtifactCursorBinding(
    string QueryIdentity,
    string PublicationIdentity,
    string FilterIdentity,
    AnalysisArtifactSortOrder SortOrder,
    int PageSize,
    DateTimeOffset ExpiresAt);

public static class AnalysisArtifactCursorBindingPolicy
{
    public const string CursorKind = "opaque-keyset";
    public static readonly IReadOnlyList<string> BoundFields =
        ["query", "publication", "filter", "sort", "last-key"];

    public static AnalysisArtifactCursorBindingDisposition Validate(
        AnalysisArtifactCursorBinding cursor,
        AnalysisArtifactCursorBinding request,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(cursor);
        ArgumentNullException.ThrowIfNull(request);
        if (cursor.ExpiresAt <= now)
        {
            return AnalysisArtifactCursorBindingDisposition.Expired;
        }
        if (!StringComparer.Ordinal.Equals(cursor.QueryIdentity, request.QueryIdentity))
        {
            return AnalysisArtifactCursorBindingDisposition.ScopeMismatch;
        }
        if (!StringComparer.Ordinal.Equals(cursor.PublicationIdentity, request.PublicationIdentity))
        {
            return AnalysisArtifactCursorBindingDisposition.PublicationMismatch;
        }
        if (!StringComparer.Ordinal.Equals(cursor.FilterIdentity, request.FilterIdentity)
            || cursor.PageSize != request.PageSize)
        {
            return AnalysisArtifactCursorBindingDisposition.QueryMismatch;
        }
        if (cursor.SortOrder != request.SortOrder)
        {
            return AnalysisArtifactCursorBindingDisposition.SortMismatch;
        }
        return AnalysisArtifactCursorBindingDisposition.Accepted;
    }
}
