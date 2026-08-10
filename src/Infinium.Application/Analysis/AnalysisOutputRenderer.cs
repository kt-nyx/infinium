using System.Globalization;
using System.Text;
using System.Text.Json;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;

public static class AnalysisOutputRenderer
{
    private const string OperationalProjectionMarker = "canonical-operational-projection-json=";

    public static string Render(RunOutputContract output, CliSummaryDocumentContract summary)
    {
        RunOutputContractInvariants.Validate(output);
        CliSummaryDocumentContractInvariants.Validate(summary);
        if (output.RunId != summary.RunId)
        {
            throw new InvalidDataException("Human output and JSON summary must describe the same run.");
        }
        StringBuilder text = new();
        text.AppendLine(CultureInfo.InvariantCulture, $"run {output.RunId} state={output.RunState} outcome={summary.Outcome}");
        text.AppendLine(CultureInfo.InvariantCulture, $"readiness={output.Readiness.State} scope={output.Readiness.Scope} no-safety-guarantee={output.Readiness.NoSafetyGuarantee.ToString().ToLowerInvariant()}");
        text.AppendLine(CultureInfo.InvariantCulture, $"coverage populations={output.AnalyzerCoverage.Count} gaps={output.CoverageGaps.Count} failures={output.Failures.Count}");
        Append(text, "observations", output.Observations);
        Append(text, "deterministic-results", output.DeterministicResults);
        Append(text, "external-claims", output.ExternalClaims);
        Append(text, "application-links", output.ApplicationLinks);
        Append(text, "model-proposals", output.ModelProposals);
        Append(text, "proposal-admissions", output.ProposalAdmissions);
        Append(text, "documentation-revisions", output.DocumentationRevisions);
        Append(text, "passages", output.Passages);
        Append(text, "candidates", output.Candidates);
        Append(text, "hypotheses", output.Hypotheses);
        Append(text, "candidate-decisions", output.CandidateDecisions);
        Append(text, "abstentions", output.Abstentions);
        Append(text, "invalid-inputs", output.InvalidInputs);
        Append(text, "recommendations", output.Recommendations);
        Append(text, "coverage-gaps", output.CoverageGaps);
        Append(text, "failures", output.Failures);
        Append(text, "discovery-leads", output.DiscoveryLeads);
        Append(text, "findings", output.Findings);
        Append(text, "supported-cases", output.SupportedCases);
        Append(text, "lead-only-cases", output.LeadOnlyCases);
        Append(text, "reconciliation", output.ReconciliationAssessments);
        Append(text, "lineage", output.LineageEvents);
        text.AppendLine(CultureInfo.InvariantCulture, $"taxonomy-assignments count={output.TaxonomyAssignments.Count}");
        foreach (TaxonomyAssignmentDocumentContract assignment in output.TaxonomyAssignments.OrderBy(item => item.AssignmentId, StringComparer.Ordinal))
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"  {assignment.AssignmentId} subject={assignment.SubjectType}:{assignment.SubjectId} axis={assignment.Axis} facet={assignment.Facet} code={assignment.Code ?? "null"} applicability={assignment.ApplicabilityState} role={assignment.ClassificationRole}");
        }
        text.AppendLine(CultureInfo.InvariantCulture, $"coverage-detail count={output.AnalyzerCoverage.Count}");
        foreach (CoverageDocumentContract coverage in output.AnalyzerCoverage.OrderBy(item => item.CoverageId, StringComparer.Ordinal))
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"  {coverage.CoverageId} population={coverage.PopulationId} denominator={coverage.DenominatorLabel}:{coverage.Denominator} completed={coverage.CompletedCount} status={coverage.Status} exclusions={coverage.Exclusions.Count} gaps={coverage.Gaps.Count} failures={coverage.Failures.Count}");
        }
        text.AppendLine(CultureInfo.InvariantCulture, $"replay state={output.Replayability.ProductState} class={output.Replayability.ExactClass} manifest={output.ReplayManifest.ArtifactId}");
        text.AppendLine("external-effects=" + string.Join(',', output.NotUsedBoundaries
            .OrderBy(item => item.CapabilityId, StringComparer.Ordinal)
            .Select(item => item.CapabilityId + ":" + item.State)));
        text.Append("canonical-run-output-json=");
        text.AppendLine(Encoding.UTF8.GetString(RunOutputJsonCodec.Serialize(output)));
        text.Append("canonical-cli-summary-json=");
        text.AppendLine(Encoding.UTF8.GetString(CliSummaryJsonCodec.Serialize(summary)));
        return text.ToString();
    }

    public static IReadOnlyList<AnalysisOperationalRunProjection> ProjectOperationalRuns(
        IEnumerable<AnalysisOperationalRunProjection> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        Dictionary<string, int> terminalOrder = new(StringComparer.Ordinal)
        {
            ["completed-with-gaps"] = 0,
            ["cancelled"] = 1,
            ["limit-reached"] = 2,
            ["failed"] = 3,
        };
        AnalysisOperationalRunProjection[] projected = runs.Select(item =>
        {
            if (string.IsNullOrWhiteSpace(item.Run)
                || !terminalOrder.ContainsKey(item.TerminalState)
                || item.Facts.Any(string.IsNullOrWhiteSpace)
                || item.Gaps.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException("An operational run projection is outside the closed terminal contract.");
            }
            return item with
            {
                Facts = item.Facts.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                Gaps = item.Gaps.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            };
        }).OrderBy(item => terminalOrder[item.TerminalState]).ThenBy(item => item.Run, StringComparer.Ordinal).ToArray();
        if (projected.Select(item => item.Run).Distinct(StringComparer.Ordinal).Count() != projected.Length)
        {
            throw new InvalidDataException("Operational run projection identities must be unique.");
        }
        return projected;
    }

    public static byte[] RenderOperationalProjectionJson(IEnumerable<AnalysisOperationalRunProjection> runs) =>
        JsonSerializer.SerializeToUtf8Bytes(
            ProjectOperationalRuns(runs), SchemaValidatedJsonCodec.JsonOptions);

    public static IReadOnlyList<AnalysisOperationalRunProjection> ParseOperationalProjectionJson(
        ReadOnlySpan<byte> json) =>
        JsonSerializer.Deserialize<AnalysisOperationalRunProjection[]>(json, SchemaValidatedJsonCodec.JsonOptions)
            ?? throw new InvalidDataException("JSON operational projection is empty.");

    public static string RenderOperationalProjectionHuman(IEnumerable<AnalysisOperationalRunProjection> runs)
    {
        IReadOnlyList<AnalysisOperationalRunProjection> projected = ProjectOperationalRuns(runs);
        StringBuilder text = new();
        foreach (AnalysisOperationalRunProjection item in projected)
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"run {item.Run} state={item.TerminalState} facts={item.Facts.Count} gaps={item.Gaps.Count} review={item.Review ?? "not-recorded"}");
        }
        text.Append(OperationalProjectionMarker);
        text.AppendLine(Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(
            projected, SchemaValidatedJsonCodec.JsonOptions)));
        return text.ToString();
    }

    public static IReadOnlyList<AnalysisOperationalRunProjection> ParseOperationalProjectionHuman(string human)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(human);
        int marker = human.IndexOf(OperationalProjectionMarker, StringComparison.Ordinal);
        if (marker < 0)
        {
            throw new InvalidDataException("Human output has no canonical operational projection.");
        }
        string json = human[(marker + OperationalProjectionMarker.Length)..].Trim();
        return JsonSerializer.Deserialize<AnalysisOperationalRunProjection[]>(json, SchemaValidatedJsonCodec.JsonOptions)
            ?? throw new InvalidDataException("Human operational projection is empty.");
    }

    private static void Append(
        StringBuilder text,
        string label,
        IReadOnlyList<TypedArtifactDocumentContract> artifacts)
    {
        text.AppendLine(CultureInfo.InvariantCulture, $"{label} count={artifacts.Count}");
        foreach (TypedArtifactDocumentContract artifact in artifacts.OrderBy(item => item.ArtifactId, StringComparer.Ordinal))
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"  {artifact.ArtifactId} state={artifact.State} payload={artifact.Payload.ArtifactId}");
        }
    }
}

public sealed record AnalysisOperationalRunProjection(
    string Run,
    string TerminalState,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> Gaps,
    string? Review = null);
