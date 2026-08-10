namespace Infinium.Domain.Contracts;

public sealed record ExecutionBoundaryContract(string BoundaryId, BoundaryUseState State, string Reason);

public static class ExecutionBoundaryContractInvariants
{
    private static readonly HashSet<string> ProductCapabilityIds = new(StringComparer.Ordinal)
    {
        "provider",
        "hosted-search",
        "nexus",
        "loot",
    };

    public static void ValidateProductCapabilities(
        IReadOnlyList<ExecutionBoundaryContract> boundaries,
        bool requireNotUsed)
    {
        ArgumentNullException.ThrowIfNull(boundaries);
        HashSet<string> actualIds = boundaries.Select(item => item.BoundaryId).ToHashSet(StringComparer.Ordinal);
        if (boundaries.Count != ProductCapabilityIds.Count
            || !actualIds.SetEquals(ProductCapabilityIds)
            || boundaries.Any(item => item.State == BoundaryUseState.Unspecified)
            || (requireNotUsed && boundaries.Any(item => item.State != BoundaryUseState.NotUsed)))
        {
            throw new InvalidOperationException(
                "Execution boundaries must declare exactly the four product capabilities with closed states.");
        }
    }
}

public sealed record AnalysisExecutionLimitsContract(
    long MaximumEntities,
    long MaximumEdges,
    long MaximumTruthRows,
    long MaximumOutputItems,
    long MaximumWallTimeMilliseconds);

public sealed record AnalysisExecutionInputContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId ExecutionInputId,
    OpaqueId RunId,
    ArtifactReferenceContract InstallationSnapshot,
    ArtifactReferenceContract BethesdaSemanticInput,
    IReadOnlyList<ArtifactReferenceContract> SourceInputs,
    IReadOnlyList<ArtifactReferenceContract> AnalyzerDeclarations,
    ArtifactReferenceContract EffectiveConfiguration,
    ArtifactReferenceContract ResolvedInputManifest,
    ReplayMode Mode,
    OpaqueId? PriorRunId,
    long Seed,
    AnalysisExecutionLimitsContract Limits,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries)
{
    public ArtifactReferenceContract AnalysisContext { get; init; } = new(
        new OpaqueId("analysis-context-unspecified"), new ContractVersion(1, 0, 0),
        new Sha256Fingerprint(new string('0', 64)), "unavailable");
}
