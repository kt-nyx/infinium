using static Infinium.Domain.Contracts.AnalysisContractInvariantHelpers;

namespace Infinium.Domain.Contracts;

public static class AnalysisExecutionContractInvariants
{
    public static void Validate(AnalysisExecutionInputContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.AnalysisExecutionInputSchemaId);
        ExecutionBoundaryContractInvariants.ValidateProductCapabilities(value.Boundaries, requireNotUsed: false);
        RequireUnique(value.SourceInputs.Select(item => item.ArtifactId), "analysis execution source inputs");
        RequireUnique(value.AnalyzerDeclarations.Select(item => item.ArtifactId), "analysis execution analyzer declarations");
        ArtifactReferenceContract[] references =
        [
            value.AnalysisContext,
            value.InstallationSnapshot,
            value.BethesdaSemanticInput,
            .. value.SourceInputs,
            .. value.AnalyzerDeclarations,
            value.EffectiveConfiguration,
            value.ResolvedInputManifest,
        ];
        if (value.Mode == ReplayMode.Unspecified
            || value.Seed < 0
            || value.SourceInputs.Count > 128
            || value.AnalyzerDeclarations.Count > 128
            || references.Any(item => string.IsNullOrWhiteSpace(item.Availability)
                || item.Availability.Length > 128
                || item.Availability is not ("retained" or "externally-reacquirable" or "evaluator-private" or "unavailable"))
            || value.Limits.MaximumEntities is < 1 or > 1_000_000
            || value.Limits.MaximumEdges is < 1 or > 2_000_000
            || value.Limits.MaximumTruthRows is < 1 or > 100_000
            || value.Limits.MaximumOutputItems is < 1 or > 100_000
            || value.Limits.MaximumWallTimeMilliseconds is < 1 or > 120_000
            || (value.Mode is ReplayMode.Incremental or ReplayMode.RetainedDownstreamReplay) != (value.PriorRunId is not null))
        {
            throw new InvalidOperationException("Execution inputs require finite limits, closed boundaries, and mode-consistent prior-run binding.");
        }
    }

}
