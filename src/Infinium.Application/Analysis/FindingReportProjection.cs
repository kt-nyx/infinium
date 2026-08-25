using System.Security.Cryptography;
using System.Text;
using Infinium.Analysis.ScopeReversion;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;

/// <summary>
/// Derives presentation-ready reports from retained scope-reversion output.
/// The retained analysis remains canonical; this projection adds no evidence.
/// </summary>
public static class FindingReportProjection
{
    private static readonly string[] UnsupportedStatements =
    [
        "Broad mod compatibility is not established.",
        "Patch safety is not established.",
        "Runtime correctness is not established.",
        "Completeness, precision, and recall are not established.",
        "Severity and confidence are not calibrated across analyzers or problem types.",
        "Production readiness is not established.",
    ];

    public static IReadOnlyList<FindingReportDocument> Project(
        ScopeReversionAnalysisContract analysis)
    {
        ScopeReversionContractInvariants.Validate(analysis);
        List<FindingReportDocument> reports = [];
        foreach (ScopeReversionFindingContract finding in analysis.Findings)
        {
            ScopeReversionCaseContract? @case =
                analysis.Cases.SingleOrDefault(item => item.FindingId == finding.FindingId);
            ScopeReversionRecommendationContract? recommendation =
                analysis.Recommendations.SingleOrDefault(item => item.FindingId == finding.FindingId);
            ScopeReversionHypothesisContract? hypothesis =
                analysis.Hypotheses.SingleOrDefault(item => item.HypothesisId == finding.HypothesisId);
            reports.Add(Create(
                analysis,
                FindingReportState.SupportedFinding,
                finding.FindingId,
                finding.MemberId,
                finding.FindingId,
                @case?.CaseId,
                "Scope-incongruent reversion detected",
                finding.Conclusion,
                finding.Symptom,
                finding.BoundedExtent,
                finding.EvidenceIds,
                hypothesis?.ContradictingEvidenceIds ?? [],
                hypothesis?.MissingInformation ?? [],
                recommendation?.Action ?? "Review the exact winning record and intended feature scope.",
                recommendation?.Validation ?? "Re-run the same retained input after a reversible correction.",
                finding.Severity,
                finding.Confidence));
        }
        AddDecisionReports(analysis, reports);
        AddAbstentionReports(analysis, reports);
        AddFailureReports(analysis, reports);
        AddGapReports(analysis, reports);
        return CanonicalRoundTrip(reports);
    }

    public static IReadOnlyList<FindingReportDocument> Project(
        ScopeReversionV2AnalysisContract analysis)
    {
        ScopeReversionV2Contract.Validate(analysis);
        List<FindingReportDocument> reports = [];
        foreach (ScopeReversionV2FindingContract finding in analysis.Findings)
        {
            ScopeReversionV2SubjectContract subject =
                analysis.Subjects.Single(item => item.SubjectId == finding.SubjectId);
            ScopeReversionV2CaseContract? @case =
                analysis.Cases.SingleOrDefault(item => item.FindingId == finding.FindingId);
            ScopeReversionRecommendationContract? recommendation =
                analysis.Recommendations.SingleOrDefault(item => item.FindingId == finding.FindingId);
            reports.Add(Create(
                analysis,
                FindingReportState.SupportedFinding,
                finding.FindingId,
                subject,
                finding.FindingId,
                @case?.CaseId,
                "Scope-incongruent reversion detected",
                finding.Conclusion,
                finding.PredictedSymptom,
                subject.AffectedLocus,
                finding.EvidenceIds,
                [],
                subject.ClaimGaps,
                recommendation?.Action ?? subject.Recommendation,
                recommendation?.Validation ?? subject.Validation,
                finding.Severity,
                finding.Confidence));
        }
        foreach (ScopeReversionV2DecisionContract decision in analysis.Decisions
            .Where(item => item.Disposition != ScopeReversionDisposition.SupportedFinding))
        {
            ScopeReversionV2SubjectContract subject =
                analysis.Subjects.Single(item => item.SubjectId == decision.SubjectId);
            FindingReportState state = decision.Disposition switch
            {
                ScopeReversionDisposition.ResolvedNegative => FindingReportState.ResolvedNegative,
                ScopeReversionDisposition.Failed => FindingReportState.Failure,
                ScopeReversionDisposition.Limited => FindingReportState.Limited,
                _ => FindingReportState.Abstention,
            };
            reports.Add(Create(
                analysis,
                state,
                decision.DecisionId,
                subject,
                null,
                analysis.Cases.SingleOrDefault(item =>
                    item.SubjectId == subject.SubjectId && item.FindingId is null)?.CaseId,
                StateTitle(state),
                decision.Rationale,
                decision.Rationale,
                subject.AffectedLocus,
                decision.EvidenceIds,
                [],
                subject.ClaimGaps,
                state == FindingReportState.ResolvedNegative
                    ? "No corrective action is recommended for this exact resolved subject."
                    : "Supply the missing evidence or correct the failed/limited input before relying on this subject.",
                subject.Validation,
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified));
        }
        foreach (ScopeReversionGapContract gap in analysis.Gaps)
        {
            ScopeReversionV2SubjectContract subject =
                analysis.Subjects.Single(item => item.SubjectId == gap.MemberId);
            reports.Add(Create(
                analysis,
                FindingReportState.CoverageGap,
                gap.GapId,
                subject,
                null,
                null,
                "Coverage gap retained",
                gap.Reason,
                gap.MissingCapabilityOrInformation,
                subject.AffectedLocus,
                [],
                [],
                [gap.MissingCapabilityOrInformation],
                "Resolve the named coverage gap before broadening the conclusion.",
                subject.Validation,
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified));
        }
        return CanonicalRoundTrip(reports);
    }

    private static void AddDecisionReports(
        ScopeReversionAnalysisContract analysis,
        List<FindingReportDocument> reports)
    {
        foreach (ScopeReversionDecisionContract decision in analysis.Decisions
            .Where(item => item.Disposition is ScopeReversionDisposition.ResolvedNegative
                or ScopeReversionDisposition.Limited
                or ScopeReversionDisposition.Unsupported
                or ScopeReversionDisposition.InvalidInput
                or ScopeReversionDisposition.Unpublishable))
        {
            FindingReportState state = decision.Disposition switch
            {
                ScopeReversionDisposition.ResolvedNegative => FindingReportState.ResolvedNegative,
                ScopeReversionDisposition.Limited => FindingReportState.Limited,
                _ => FindingReportState.Abstention,
            };
            ScopeReversionCandidateContract? candidate =
                analysis.Candidates.SingleOrDefault(item => item.DecisionId == decision.DecisionId);
            reports.Add(Create(
                analysis,
                state,
                decision.DecisionId,
                decision.MemberId,
                null,
                null,
                StateTitle(state),
                decision.Rationale,
                candidate?.CausalExplanation ?? decision.Rationale,
                "The conclusion applies only to this exact analyzed member.",
                decision.EvidenceIds,
                candidate?.ContradictingEvidenceIds ?? [],
                candidate?.MissingInformation ?? [],
                state == FindingReportState.ResolvedNegative
                    ? "No corrective action is recommended for this exact resolved member."
                    : "Supply the missing evidence or supported capability before relying on this member.",
                "Re-run the same retained member after its evidence or support state changes.",
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified));
        }
    }

    private static void AddAbstentionReports(
        ScopeReversionAnalysisContract analysis,
        List<FindingReportDocument> reports)
    {
        foreach (ScopeReversionAbstentionContract abstention in analysis.Abstentions)
        {
            ScopeReversionCandidateContract candidate =
                analysis.Candidates.Single(item => item.CandidateId == abstention.CandidateId);
            reports.Add(Create(
                analysis,
                FindingReportState.Abstention,
                abstention.AbstentionId,
                candidate.MemberId,
                null,
                null,
                "Analysis abstained",
                abstention.Reason,
                candidate.CausalExplanation,
                "No supported finding or resolved-negative conclusion was established.",
                abstention.EvidenceIds,
                candidate.ContradictingEvidenceIds,
                abstention.RequiredInformation,
                "Collect the required information before making a compatibility decision.",
                "Re-run the same member after the missing information is retained.",
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified));
        }
    }

    private static void AddFailureReports(
        ScopeReversionAnalysisContract analysis,
        List<FindingReportDocument> reports)
    {
        foreach (ScopeReversionFailureContract failure in analysis.Failures)
        {
            reports.Add(Create(
                analysis,
                FindingReportState.Failure,
                failure.FailureId,
                failure.MemberId,
                null,
                null,
                "Analysis failed for one subject",
                failure.Message,
                failure.FailureCode,
                "No semantic conclusion was established for the failed member.",
                [],
                [],
                [failure.Retryable ? "The failure is marked retryable." : "The failure is not marked retryable."],
                failure.Retryable
                    ? "Correct the input or transient failure and retry the exact member."
                    : "Inspect and correct the named failure before retrying.",
                "Confirm a completed retained result replaces this failure.",
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified));
        }
    }

    private static void AddGapReports(
        ScopeReversionAnalysisContract analysis,
        List<FindingReportDocument> reports)
    {
        foreach (ScopeReversionGapContract gap in analysis.Gaps)
        {
            reports.Add(Create(
                analysis,
                FindingReportState.CoverageGap,
                gap.GapId,
                gap.MemberId,
                null,
                null,
                "Coverage gap retained",
                gap.Reason,
                gap.MissingCapabilityOrInformation,
                $"Population: {gap.PopulationId}; state: {gap.State}.",
                [],
                [],
                [gap.MissingCapabilityOrInformation],
                "Resolve the named coverage gap before broadening the conclusion.",
                "Re-run the same coverage population and confirm completion.",
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified));
        }
    }

    private static FindingReportDocument Create(
        ScopeReversionAnalysisContract analysis,
        FindingReportState state,
        OpaqueId reportSourceId,
        OpaqueId subjectId,
        OpaqueId? findingId,
        OpaqueId? caseId,
        string title,
        string conclusion,
        string whatHappened,
        string whyItMatters,
        IReadOnlyList<OpaqueId> supporting,
        IReadOnlyList<OpaqueId> contradicting,
        IReadOnlyList<string> unresolved,
        string action,
        string validation,
        FindingSeverity severity,
        AnalysisConfidence confidence)
    {
        ScopeReversionAssessment policy = ScopeReversionAssessmentPolicy.Assess(
            ScopeReversionAnalyzerDeclaration.AnalyzerId);
        IReadOnlyList<ScopeReversionTaxonomyFactContract> taxonomy =
            analysis.Taxonomy.Where(item => item.MemberId == subjectId).ToArray();
        return new(
            FindingReportContract.SchemaId,
            FindingReportContract.SchemaVersion,
            FindingReportContract.ContractMaturity,
            ReportId(analysis.OriginatingRunId, subjectId, state, reportSourceId),
            state,
            analysis.OriginatingRunId,
            new(analysis.Analyzer.AnalyzerId),
            findingId,
            caseId,
            subjectId,
            title,
            conclusion,
            whatHappened,
            whyItMatters,
            [new("analysis-member", subjectId, "Exact retained scope-reversion member.")],
            taxonomy.Select(item => new FindingReportTaxonomyAssignment(
                item.Axis,
                item.Code,
                item.Applicability.ToString(),
                item.Reason)).ToArray(),
            Assessment(state, severity, confidence, policy),
            Distinct(supporting),
            Distinct(contradicting),
            [],
            taxonomy.Where(item => item.Applicability == ScopeTaxonomyApplicability.Applicable)
                .Select(item => item.Reason).Distinct(StringComparer.Ordinal).ToArray(),
            state == FindingReportState.SupportedFinding
                ? ["The result is bounded to the exact reported evidence and coverage."]
                : ["No severity or confidence label is assigned because no supported finding was established."],
            unresolved.Distinct(StringComparer.Ordinal).ToArray(),
            analysis.Coverage.Select(item => new FindingReportCoverage(
                item.PopulationId,
                item.Denominator,
                item.Completed,
                item.CompletedWithGaps,
                item.Failed,
                checked(item.SkippedByConfiguration + item.SkippedByLimit + item.Unsupported))).ToArray(),
            analysis.Failures.Select(item => $"{item.FailureCode}: {item.Message}").ToArray(),
            analysis.Boundaries.Select(item => $"{item.BoundaryId}: {item.Reason}").ToArray(),
            analysis.Gaps.Select(item => $"{item.PopulationId}: {item.MissingCapabilityOrInformation}").ToArray(),
            action,
            validation,
            new(
                analysis.SchemaId,
                analysis.SchemaVersion,
                analysis.PayloadId,
                analysis.InputFingerprint,
                analysis.AssignmentId,
                true,
                "raw-run-output-is-canonical"),
            [.. UnsupportedStatements, analysis.PublicationClaimBoundary]);
    }

    private static FindingReportDocument Create(
        ScopeReversionV2AnalysisContract analysis,
        FindingReportState state,
        OpaqueId reportSourceId,
        ScopeReversionV2SubjectContract subject,
        OpaqueId? findingId,
        OpaqueId? caseId,
        string title,
        string conclusion,
        string whatHappened,
        string whyItMatters,
        IReadOnlyList<OpaqueId> supporting,
        IReadOnlyList<OpaqueId> contradicting,
        IReadOnlyList<string> unresolved,
        string action,
        string validation,
        FindingSeverity severity,
        AnalysisConfidence confidence)
    {
        ScopeReversionAssessment policy = ScopeReversionAssessmentPolicy.Assess(
            ScopeReversionAnalyzerDeclaration.AnalyzerId);
        IReadOnlyList<ScopeReversionV2TaxonomyReferenceContract> taxonomy =
            analysis.Taxonomy.Where(item => item.SubjectId == subject.SubjectId).ToArray();
        return new(
            FindingReportContract.SchemaId,
            FindingReportContract.SchemaVersion,
            FindingReportContract.ContractMaturity,
            ReportId(analysis.OriginatingRunId, subject.SubjectId, state, reportSourceId),
            state,
            analysis.OriginatingRunId,
            new(analysis.Analyzer.AnalyzerId),
            findingId,
            caseId,
            subject.SubjectId,
            title,
            conclusion,
            whatHappened,
            whyItMatters,
            subject.OrderedMemberIds.Select(item =>
                new FindingReportSubject(subject.Kind.ToString(), item, subject.AffectedLocus)).ToArray(),
            taxonomy.Select(item => new FindingReportTaxonomyAssignment(
                item.Axis,
                item.Code,
                item.Applicability.ToString(),
                item.Reason)).ToArray(),
            Assessment(state, severity, confidence, policy),
            Distinct(supporting),
            Distinct(contradicting),
            [],
            taxonomy.Where(item => item.Applicability == TaxonomyApplicability.Assigned)
                .Select(item => item.Reason).Distinct(StringComparer.Ordinal).ToArray(),
            state == FindingReportState.SupportedFinding
                ? ["The result is bounded to the exact reported evidence and coverage."]
                : ["No severity or confidence label is assigned because no supported finding was established."],
            unresolved.Distinct(StringComparer.Ordinal).ToArray(),
            analysis.Coverage.Select(item => new FindingReportCoverage(
                item.PopulationId,
                item.Denominator,
                item.Completed,
                item.CompletedWithGaps,
                item.Failed,
                item.Unsupported)).ToArray(),
            [],
            analysis.Boundaries.Select(item => $"{item.BoundaryId}: {item.Reason}").ToArray(),
            analysis.Gaps.Select(item => $"{item.PopulationId}: {item.MissingCapabilityOrInformation}").ToArray(),
            action,
            validation,
            new(
                analysis.SchemaId,
                analysis.SchemaVersion,
                analysis.PayloadId,
                analysis.InputManifestFingerprint,
                analysis.AssignmentId,
                true,
                "raw-run-output-is-canonical"),
            [.. UnsupportedStatements, analysis.PublicationClaimBoundary]);
    }

    private static FindingReportAssessment Assessment(
        FindingReportState state,
        FindingSeverity severity,
        AnalysisConfidence confidence,
        ScopeReversionAssessment policy) =>
        state == FindingReportState.SupportedFinding
            ? new(
                severity,
                policy.SeverityBasis,
                confidence,
                policy.ConfidenceBasis,
                policy.AnalyzerMaturity,
                "The analyzer remains Experimental while broader calibration and reliability evidence are deferred.",
                policy.CalibrationBoundary)
            : new(
                FindingSeverity.Unspecified,
                "Severity is not assigned because this report is not a supported finding.",
                AnalysisConfidence.Unspecified,
                "Confidence is not assigned because this report is not a supported finding.",
                AnalyzerMaturity.Experimental,
                "The analyzer remains Experimental.",
                policy.CalibrationBoundary);

    private static OpaqueId ReportId(
        OpaqueId runId,
        OpaqueId subjectId,
        FindingReportState state,
        OpaqueId reportSourceId)
    {
        string input = string.Join(
            '|',
            FindingReportContract.SchemaId,
            runId.Value,
            subjectId.Value,
            state,
            reportSourceId.Value);
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        return new($"finding-report-{digest[..32]}");
    }

    private static OpaqueId[] Distinct(IEnumerable<OpaqueId> values) =>
        values.Distinct().OrderBy(item => item.Value, StringComparer.Ordinal).ToArray();

    private static string StateTitle(FindingReportState state) => state switch
    {
        FindingReportState.ResolvedNegative => "No scope-reversion problem found for this subject",
        FindingReportState.Limited => "Analysis was limited",
        FindingReportState.Failure => "Analysis failed",
        FindingReportState.CoverageGap => "Coverage gap retained",
        _ => "Analysis could not reach a supported conclusion",
    };

    private static FindingReportDocument[] CanonicalRoundTrip(
        IEnumerable<FindingReportDocument> reports) =>
        reports
            .OrderBy(item => item.ReportId.Value, StringComparer.Ordinal)
            .Select(item => FindingReportJsonCodec.Deserialize(FindingReportJsonCodec.Serialize(item)))
            .ToArray();
}
