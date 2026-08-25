namespace Infinium.Domain.Contracts;

public enum FindingReportState
{
    Unspecified,
    SupportedFinding,
    ResolvedNegative,
    Abstention,
    Failure,
    Limited,
    CoverageGap,
}

public sealed record FindingReportSubject(
    string Kind,
    OpaqueId SubjectId,
    string Detail);

public sealed record FindingReportTaxonomyAssignment(
    string Axis,
    string? Code,
    string Applicability,
    string Basis);

public sealed record FindingReportAssessment(
    FindingSeverity Severity,
    string SeverityBasis,
    AnalysisConfidence Confidence,
    string ConfidenceBasis,
    AnalyzerMaturity AnalyzerMaturity,
    string MaturityBasis,
    string CalibrationBoundary);

public sealed record FindingReportCoverage(
    string PopulationId,
    long Denominator,
    long Completed,
    long CompletedWithGaps,
    long Failed,
    long SkippedOrUnsupported);

public sealed record FindingReportProvenance(
    string SourceSchemaId,
    ContractVersion SourceSchemaVersion,
    OpaqueId SourcePayloadId,
    Sha256Fingerprint SourceInputFingerprint,
    OpaqueId SourceAssignmentId,
    bool ReplayEquivalent,
    string CanonicalArtifactRole);

public sealed record FindingReportDocument(
    string SchemaId,
    ContractVersion SchemaVersion,
    string ContractMaturity,
    OpaqueId ReportId,
    FindingReportState State,
    OpaqueId RunId,
    OpaqueId AnalyzerId,
    OpaqueId? FindingId,
    OpaqueId? CaseId,
    OpaqueId SubjectId,
    string Title,
    string Conclusion,
    string WhatHappened,
    string WhyItMatters,
    IReadOnlyList<FindingReportSubject> AffectedSubjects,
    IReadOnlyList<FindingReportTaxonomyAssignment> TaxonomyAssignments,
    FindingReportAssessment Assessment,
    IReadOnlyList<OpaqueId> SupportingEvidenceIds,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> ApplicabilityConditions,
    IReadOnlyList<string> Uncertainty,
    IReadOnlyList<string> UnresolvedQuestions,
    IReadOnlyList<FindingReportCoverage> Coverage,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> Gaps,
    string RecommendedAction,
    string ValidationSteps,
    FindingReportProvenance Provenance,
    IReadOnlyList<string> UnsupportedOrNotEstablished);

public static class FindingReportContract
{
    public const string SchemaId = "infinium.presentation.finding-report/v1";
    public static readonly ContractVersion SchemaVersion = new(1, 0, 0);
    public const string ContractMaturity = "implementation-active";

    public static void Validate(FindingReportDocument value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaId != SchemaId
            || value.SchemaVersion != SchemaVersion
            || value.ContractMaturity != ContractMaturity
            || value.State == FindingReportState.Unspecified
            || value.Assessment.AnalyzerMaturity != AnalyzerMaturity.Experimental
            || value.ReportId.Value.Length is < 1 or > 160
            || value.RunId.Value.Length is < 1 or > 160
            || value.AnalyzerId.Value.Length is < 1 or > 160
            || value.SubjectId.Value.Length is < 1 or > 160
            || !Text(value.Title, 256)
            || !Text(value.Conclusion, 4096)
            || !Text(value.WhatHappened, 4096)
            || !Text(value.WhyItMatters, 4096)
            || !Text(value.Assessment.SeverityBasis, 4096)
            || !Text(value.Assessment.ConfidenceBasis, 4096)
            || !Text(value.Assessment.MaturityBasis, 4096)
            || !Text(value.Assessment.CalibrationBoundary, 4096)
            || !Text(value.RecommendedAction, 4096)
            || !Text(value.ValidationSteps, 4096)
            || value.Provenance.SourceSchemaId.Length is < 1 or > 200
            || value.Provenance.CanonicalArtifactRole != "raw-run-output-is-canonical"
            || !value.Provenance.ReplayEquivalent
            || value.UnsupportedOrNotEstablished.Count == 0
            || value.AffectedSubjects.Count == 0
            || value.Coverage.Count == 0
            || value.AffectedSubjects.Any(item =>
                !Text(item.Kind, 80) || !Text(item.Detail, 4096))
            || value.TaxonomyAssignments.Any(item =>
                !Text(item.Axis, 160)
                || item.Code is not null && !Text(item.Code, 200)
                || !Text(item.Applicability, 80)
                || !Text(item.Basis, 4096))
            || value.Coverage.Any(item =>
                !Text(item.PopulationId, 160)
                || item.Denominator < 0
                || item.Completed < 0
                || item.CompletedWithGaps < 0
                || item.Failed < 0
                || item.SkippedOrUnsupported < 0)
            || !Unique(value.SupportingEvidenceIds.Select(item => item.Value))
            || !Unique(value.ContradictingEvidenceIds.Select(item => item.Value))
            || !AllText(value.Assumptions)
            || !AllText(value.ApplicabilityConditions)
            || !AllText(value.Uncertainty)
            || !AllText(value.UnresolvedQuestions)
            || !AllText(value.Failures)
            || !AllText(value.Exclusions)
            || !AllText(value.Gaps)
            || !AllText(value.UnsupportedOrNotEstablished))
        {
            throw new InvalidDataException(
                "The finding report is incomplete, unbounded, contradictory, or not implementation-active.");
        }
        if (value.Coverage.Any(item =>
            item.Completed > item.Denominator
            || item.CompletedWithGaps > item.Denominator
            || item.Failed > item.Denominator
            || item.SkippedOrUnsupported > item.Denominator
            || item.Denominator != (decimal)item.Completed
                + item.CompletedWithGaps
                + item.Failed
                + item.SkippedOrUnsupported))
        {
            throw new InvalidDataException(
                "Finding-report coverage counts must form one complete, non-overlapping denominator.");
        }
        bool supported = value.State == FindingReportState.SupportedFinding;
        if (supported != (value.FindingId is not null)
            || supported && value.Assessment.Severity == FindingSeverity.Unspecified
            || supported && value.Assessment.Confidence == AnalysisConfidence.Unspecified
            || supported && value.SupportingEvidenceIds.Count == 0
            || !supported && value.Assessment.Severity != FindingSeverity.Unspecified
            || !supported && value.Assessment.Confidence != AnalysisConfidence.Unspecified
            || value.State == FindingReportState.Failure && value.Failures.Count == 0
            || value.State == FindingReportState.CoverageGap && value.Gaps.Count == 0)
        {
            throw new InvalidDataException(
                "Only a supported finding may carry finding identity, severity, and confidence.");
        }
    }

    private static bool Text(string value, int maximum) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;

    private static bool AllText(IEnumerable<string> values) =>
        values.All(value => Text(value, 4096));

    private static bool Unique(IEnumerable<string> values)
    {
        string[] items = values.ToArray();
        return items.Distinct(StringComparer.Ordinal).Count() == items.Length;
    }
}
