namespace Infinium.Domain.Contracts;

internal static class AnalysisContractInvariantHelpers
{
    internal static void RequireHeader(string schemaId, ContractVersion schemaVersion, string expectedSchemaId)
    {
        if (!StringComparer.Ordinal.Equals(schemaId, expectedSchemaId) || schemaVersion.Major != 1)
        {
            throw new InvalidOperationException($"Payload must bind {expectedSchemaId} major v1.");
        }
    }

    internal static bool IsAsciiToken(string value) => value.Length != 0
        && char.IsAsciiLetterOrDigit(value[0])
        && value.All(character => char.IsAsciiLetterOrDigit(character)
            || character is '.' or '_' or ':' or '/' or '-');

    internal static CandidateDependencyEdgeContract CandidateEdge(
        string fromKind,
        OpaqueId fromId,
        string toKind,
        OpaqueId toId,
        string edgeKind) => new(
        CandidateAnalysisIdentity.StableId("candidate-edge", fromKind, fromId.Value, toKind, toId.Value, edgeKind),
        fromKind,
        fromId,
        toKind,
        toId,
        edgeKind);

    internal static void RequireUnique(IEnumerable<OpaqueId> ids, string description)
    {
        OpaqueId[] materialized = ids.ToArray();
        if (materialized.Distinct().Count() != materialized.Length)
        {
            throw new InvalidOperationException($"{description} must use unique opaque IDs.");
        }
    }
}
