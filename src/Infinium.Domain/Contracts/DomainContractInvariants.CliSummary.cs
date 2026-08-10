namespace Infinium.Domain.Contracts;

public static partial class DomainContractInvariants
{
    public static void Validate(CliSummaryAggregateContract summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(summary.TypedCounts);
        ArgumentNullException.ThrowIfNull(summary.CoverageStateCounts);
        ArgumentNullException.ThrowIfNull(summary.Cost);
        if (!StringComparer.Ordinal.Equals(summary.SchemaId, ContractConstants.CliSummarySchemaId)
            || summary.SchemaVersion.Major != 1
            || summary.ReadinessScope == ReadinessScope.Unspecified)
        {
            throw new InvalidOperationException("CLI summary uses an unsupported schema contract.");
        }

        CliExitCode expected = summary.Outcome switch
        {
            CliOutcome.Completed => CliExitCode.Success,
            CliOutcome.CompletedWithGaps => CliExitCode.Success,
            CliOutcome.InvalidInput => CliExitCode.InvalidInput,
            CliOutcome.Unsupported => CliExitCode.Unsupported,
            CliOutcome.Failed => CliExitCode.Failed,
            CliOutcome.Cancelled => CliExitCode.Cancelled,
            CliOutcome.LimitReached => CliExitCode.LimitReached,
            _ => throw new InvalidOperationException("CLI outcome is unknown."),
        };
        if (summary.ExitCode != expected || !summary.NoSafetyGuarantee)
        {
            throw new InvalidOperationException("CLI outcome, exit code, and safety qualification must agree.");
        }

        if (summary.TypedCounts.Observations < 0
            || summary.TypedCounts.DeterministicResults < 0
            || summary.TypedCounts.ExternalClaims < 0
            || summary.TypedCounts.ApplicationLinks < 0
            || summary.TypedCounts.DiscoveryLeads < 0
            || summary.TypedCounts.ModelProposals < 0
            || summary.TypedCounts.ProposalAdmissions < 0
            || summary.TypedCounts.Candidates < 0
            || summary.TypedCounts.Hypotheses < 0
            || summary.TypedCounts.Findings < 0
            || summary.TypedCounts.Recommendations < 0
            || summary.TypedCounts.SupportedCases < 0
            || summary.TypedCounts.LeadOnlyCases < 0
            || summary.TypedCounts.Abstentions < 0
            || summary.TypedCounts.InvalidInputs < 0
            || summary.TypedCounts.CoverageGaps < 0
            || summary.TypedCounts.Failures < 0
            || summary.TypedCounts.DocumentationRevisions < 0
            || summary.TypedCounts.Passages < 0
            || summary.TypedCounts.CandidateDecisions < 0
            || summary.TypedCounts.ReconciliationAssessments < 0
            || summary.TypedCounts.LineageEvents < 0
            || summary.CoverageStateCounts.Completed < 0
            || summary.CoverageStateCounts.CompletedWithGaps < 0
            || summary.CoverageStateCounts.Failed < 0
            || summary.CoverageStateCounts.SkippedByConfiguration < 0
            || summary.DurationMs < 0
            || summary.Cost.ProviderInputTokens < 0
            || summary.Cost.ProviderOutputTokens < 0
            || summary.Cost.ProviderReasoningTokens < 0
            || summary.Cost.DispatchCount < 0
            || summary.Cost.ToolCallCount < 0
            || summary.Cost.CalculatedActualNanoUsd < 0
            || summary.Cost.ReservedNanoUsd < 0
            || (summary.Cost.CalculatedActualNanoUsd is null
                && !summary.Cost.UnresolvedHold)
            || (summary.Cost.CalculatedActualNanoUsd is not null
                && summary.Cost.UnresolvedHold)
            || summary.CoverageStateCounts.SkippedByLimit < 0
            || summary.CoverageStateCounts.Unsupported < 0)
        {
            throw new InvalidOperationException("CLI summary counts cannot be negative.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required.");
        }
    }

    private static void RequireNonEmpty<T>(IReadOnlyCollection<T> values, string name)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            throw new InvalidOperationException($"{name} must contain at least one value.");
        }
    }

    private static void RequireUnique(IEnumerable<string> values, string name)
    {
        string[] materialized = values.ToArray();
        if (materialized.Distinct(StringComparer.Ordinal).Count() != materialized.Length)
        {
            throw new InvalidOperationException($"{name} must not contain duplicate values.");
        }
    }
}
