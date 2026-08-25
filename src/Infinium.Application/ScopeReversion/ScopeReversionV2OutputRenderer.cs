using System.Text;
using System.Globalization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.ScopeReversion;

public static class ScopeReversionV2OutputRenderer
{
    public static string RenderHuman(ScopeReversionV2AnalysisContract analysis)
    {
        ScopeReversionV2Contract.Validate(analysis);
        StringBuilder text = new();
        text.AppendLine("Infinium scope-reversion analysis v2");
        text.AppendLine(CultureInfo.InvariantCulture, $"Run: {analysis.OriginatingRunId.Value}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Snapshot/context/configuration/input: {analysis.SnapshotId.Value} / {analysis.ContextId.Value} / {analysis.ConfigurationId.Value} / {analysis.ExecutionInputId.Value}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Answer-free handoff: {analysis.InputHandoffId}; local manifest: {analysis.InputManifestFingerprint.Value}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Public manifests: {analysis.PublicManifests.Count}; admitted controlled inputs: {analysis.ControlledInputs.Count}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Partition: {analysis.PartitionRole}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Partition transitions: {analysis.PartitionTransitions.Count}");
        text.AppendLine(CultureInfo.InvariantCulture, $"Subjects: {analysis.Subjects.Count}; hypotheses: {analysis.Hypotheses.Count}; findings: {analysis.Findings.Count}; cases: {analysis.Cases.Count}");
        foreach (ScopeReversionV2DecisionContract decision in analysis.Decisions)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"- {decision.SubjectId.Value}: {decision.Disposition} ({decision.Transition})");
        }
        text.AppendLine("Coverage:");
        foreach (ScopeReversionV2CoverageContract coverage in analysis.Coverage)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"- {coverage.PopulationId}: denominator={coverage.Denominator}; completed={coverage.Completed}; with-gaps={coverage.CompletedWithGaps}; unsupported={coverage.Unsupported}; failed={coverage.Failed}");
        }
        text.AppendLine("External/prohibited boundaries: NotUsed");
        if (analysis.Gaps.Count > 0)
        {
            text.AppendLine("Retained gaps:");
            foreach (ScopeReversionGapContract gap in analysis.Gaps)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"- {gap.MemberId.Value}: {gap.Reason}");
            }
        }
        text.AppendLine(CultureInfo.InvariantCulture, $"Claim boundary: {analysis.PublicationClaimBoundary}");
        return text.ToString();
    }
}
