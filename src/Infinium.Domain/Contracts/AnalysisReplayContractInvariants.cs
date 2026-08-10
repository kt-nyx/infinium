using static Infinium.Domain.Contracts.AnalysisContractInvariantHelpers;

namespace Infinium.Domain.Contracts;

public static class AnalysisReplayContractInvariants
{
    public static void Validate(AnalysisReplayContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.AnalysisReplaySchemaId);
        RequireUnique(value.Dependencies.Select(item => item.DependencyId), "replay dependencies");
        HashSet<OpaqueId> dependencies = value.Dependencies.Select(item => item.DependencyId).ToHashSet();
        if (value.Edges.Any(edge => edge.From == edge.To || !dependencies.Contains(edge.From) || !dependencies.Contains(edge.To)))
        {
            throw new InvalidOperationException("Replay dependency edges must connect distinct admitted nodes.");
        }
        bool requiresComparedRun = value.Mode is ReplayMode.Incremental or ReplayMode.RetainedDownstreamReplay;
        if (value.Mode == ReplayMode.Unspecified
            || (requiresComparedRun && value.ComparedRunId is null)
            || (!requiresComparedRun && value.ComparedRunId is not null))
        {
            throw new InvalidOperationException("Replay manifests require a mode-consistent compared-run binding.");
        }
        if (value.ReplayState == ReplayState.CompleteClean
            && (value.MissingDependencyIds.Count != 0
                || !value.SemanticallyEquivalent
                || value.AuditabilityState != AuditabilityState.Complete))
        {
            throw new InvalidOperationException("Complete-clean replay requires complete retained dependencies, audit, and semantic equivalence.");
        }
    }

}
