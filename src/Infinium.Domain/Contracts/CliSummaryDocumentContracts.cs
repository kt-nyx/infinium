namespace Infinium.Domain.Contracts;

/// <summary>
/// The stable JSON document model for <c>infinium.cli-summary/v1</c>.
/// </summary>
public sealed record CliSummaryDocumentContract(
    string SchemaId,
    string SchemaVersion,
    string RunId,
    string Outcome,
    int ExitCode,
    TypedOutputCountsContract TypedCounts,
    CoverageStateCountsContract CoverageStateCounts,
    long DurationMs,
    CliCostContract Cost,
    string Readiness,
    bool NoSafetyGuarantee);

public static class CliSummaryDocumentContractInvariants
{
    public static void Validate(CliSummaryDocumentContract summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ArgumentNullException.ThrowIfNull(summary.TypedCounts);
        ArgumentNullException.ThrowIfNull(summary.CoverageStateCounts);
        ArgumentNullException.ThrowIfNull(summary.Cost);
        if (!StringComparer.Ordinal.Equals(summary.SchemaId, ContractConstants.CliSummarySchemaId)
            || !StringComparer.Ordinal.Equals(summary.SchemaVersion, "1"))
        {
            throw new InvalidOperationException("CLI summary metadata must bind the accepted v1 contract.");
        }
        _ = new OpaqueId(summary.RunId);
        int expectedExitCode = summary.Outcome switch
        {
            "completed" or "completed-with-gaps" => (int)CliExitCode.Success,
            "invalid-input" => (int)CliExitCode.InvalidInput,
            "unsupported" => (int)CliExitCode.Unsupported,
            "failed" => (int)CliExitCode.Failed,
            "cancelled" => (int)CliExitCode.Cancelled,
            "limit-reached" => (int)CliExitCode.LimitReached,
            _ => throw new InvalidOperationException("CLI summary outcome is unknown."),
        };
        if (summary.ExitCode != expectedExitCode
            || !summary.NoSafetyGuarantee
            || summary.Readiness is not ("no-readiness-evaluation" or "scope-limited" or "full")
            || summary.DurationMs < 0
            || HasNegativeCounts(summary)
            || (summary.Cost.CalculatedActualNanoUsd is null) != summary.Cost.UnresolvedHold)
        {
            throw new InvalidOperationException(
                "CLI outcome, exit code, counts, cost availability, readiness, and safety qualification must agree.");
        }
    }

    private static bool HasNegativeCounts(CliSummaryDocumentContract summary)
    {
        return summary.TypedCounts.Observations < 0
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
            || summary.CoverageStateCounts.SkippedByLimit < 0
            || summary.CoverageStateCounts.Unsupported < 0
            || summary.Cost.ProviderInputTokens < 0
            || summary.Cost.ProviderOutputTokens < 0
            || summary.Cost.ProviderReasoningTokens < 0
            || summary.Cost.DispatchCount < 0
            || summary.Cost.ToolCallCount < 0
            || summary.Cost.CalculatedActualNanoUsd < 0
            || summary.Cost.ReservedNanoUsd < 0;
    }
}
