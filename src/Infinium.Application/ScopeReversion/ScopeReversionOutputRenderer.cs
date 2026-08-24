using System.Globalization;
using System.Text;
using Infinium.Domain.Contracts;

namespace Infinium.Application.ScopeReversion;

public static class ScopeReversionOutputRenderer
{
    public static string RenderHuman(ScopeReversionAnalysisContract analysis)
    {
        ScopeReversionContractInvariants.Validate(analysis);
        StringBuilder output = new();
        output.AppendLine("Infinium local scope-reversion analysis");
        output.AppendLine(CultureInfo.InvariantCulture, $"run: {analysis.OriginatingRunId.Value}");
        output.AppendLine(CultureInfo.InvariantCulture, $"payload: {analysis.PayloadId.Value}");
        output.AppendLine(CultureInfo.InvariantCulture, $"analyzer: {analysis.Analyzer.AnalyzerId} {analysis.Analyzer.AnalyzerVersion}");
        output.AppendLine(CultureInfo.InvariantCulture, $"decisions={analysis.Counts.Decisions} candidates={analysis.Counts.Candidates} hypotheses={analysis.Counts.Hypotheses}");
        output.AppendLine(CultureInfo.InvariantCulture, $"findings={analysis.Counts.Findings} cases={analysis.Counts.Cases} negatives={analysis.Counts.ResolvedNegative} abstentions={analysis.Counts.Abstentions}");
        output.AppendLine(CultureInfo.InvariantCulture, $"gaps={analysis.Counts.Gaps} failures={analysis.Counts.Failures} recommendations={analysis.Counts.Recommendations}");
        output.AppendLine("decisions:");
        foreach (ScopeReversionDecisionContract decision in analysis.Decisions)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {decision.MemberId.Value}: {decision.Disposition} transition={decision.Transition} purpose={decision.PurposeCoverage}");
        }
        output.AppendLine("candidates:");
        foreach (ScopeReversionCandidateContract candidate in analysis.Candidates)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {candidate.CandidateId.Value}: {candidate.State}; missing={string.Join(",", candidate.MissingInformation)}");
        }
        output.AppendLine("hypotheses:");
        foreach (ScopeReversionHypothesisContract hypothesis in analysis.Hypotheses)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {hypothesis.HypothesisId.Value}: {hypothesis.State}; missing={string.Join(",", hypothesis.MissingInformation)}");
        }
        output.AppendLine("negative-decisions:");
        foreach (ScopeReversionDecisionContract negative in analysis.Decisions.Where(item =>
                     item.Disposition == ScopeReversionDisposition.ResolvedNegative))
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {negative.MemberId.Value}: {negative.Rationale}");
        }
        output.AppendLine("contradictions:");
        foreach (ScopeReversionContradictionContract contradiction in analysis.Contradictions)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {contradiction.ContradictionId.Value}: {contradiction.State}; {contradiction.Reason}");
        }
        output.AppendLine("abstentions:");
        foreach (ScopeReversionAbstentionContract abstention in analysis.Abstentions)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {abstention.AbstentionId.Value}: {abstention.Reason}; required={string.Join(",", abstention.RequiredInformation)}");
        }
        output.AppendLine("failures:");
        foreach (ScopeReversionFailureContract failure in analysis.Failures)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {failure.FailureId.Value}: {failure.FailureCode}; retryable={failure.Retryable}; {failure.Message}");
        }
        output.AppendLine("gaps:");
        foreach (ScopeReversionGapContract gap in analysis.Gaps)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {gap.GapId.Value}: {gap.PopulationId}/{gap.State}; {gap.Reason}; missing={gap.MissingCapabilityOrInformation}");
        }
        output.AppendLine("findings:");
        foreach (ScopeReversionFindingContract finding in analysis.Findings)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {finding.FindingId.Value}: {finding.Confidence}/{finding.Severity} {finding.Conclusion}");
        }
        output.AppendLine("taxonomy:");
        foreach (ScopeReversionTaxonomyFactContract fact in analysis.Taxonomy)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {fact.TaxonomyFactId.Value}: {fact.Axis}={fact.Code ?? "unmapped"} {fact.Applicability}; role={fact.Role}");
        }
        output.AppendLine("cases:");
        foreach (ScopeReversionCaseContract item in analysis.Cases)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {item.CaseId.Value}: logical={item.LogicalCaseId.Value}; cause={item.SharedCause}; readiness={item.AffectsReadiness}");
        }
        output.AppendLine("recommendations:");
        foreach (ScopeReversionRecommendationContract recommendation in analysis.Recommendations)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {recommendation.RecommendationId.Value}: {recommendation.Action}; reversibility={recommendation.Reversibility}; validation={recommendation.Validation}");
        }
        output.AppendLine("coverage:");
        foreach (ScopeReversionCoverageContract coverage in analysis.Coverage)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {coverage.PopulationId}: denominator={coverage.Denominator} completed={coverage.Completed} gaps={coverage.CompletedWithGaps} failed={coverage.Failed} disabled={coverage.SkippedByConfiguration} limited={coverage.SkippedByLimit} unsupported={coverage.Unsupported}");
        }
        output.AppendLine("dependency-edges:");
        foreach (ScopeReversionDependencyEdgeContract edge in analysis.DependencyEdges)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {edge.EdgeId.Value}: {edge.FromKind}/{edge.FromId.Value} -{edge.EdgeKind}-> {edge.ToKind}/{edge.ToId.Value}");
        }
        output.AppendLine("retained-artifact-references:");
        foreach ((string Kind, string Id) artifact in analysis.DependencyEdges
            .Select(edge => (Kind: edge.ToKind, Id: edge.ToId.Value))
            .Distinct()
            .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {artifact.Id}: {artifact.Kind}");
        }
        output.AppendLine("external-boundaries:");
        foreach (ExecutionBoundaryContract boundary in analysis.Boundaries)
        {
            output.AppendLine(CultureInfo.InvariantCulture, $"  {boundary.BoundaryId}: {boundary.State}");
        }
        output.AppendLine("claim-boundary:");
        output.AppendLine("  " + analysis.PublicationClaimBoundary);
        return output.ToString();
    }
}
