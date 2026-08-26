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

    /// <summary>
    /// Projects the canonical candidate and finding/case publication into the
    /// same bounded report contract. The candidate input is used only for
    /// resolved-negative decisions that the finding/case aggregate deliberately
    /// does not promote into finding or case occurrences.
    /// </summary>
    public static IReadOnlyList<FindingReportDocument> Project(
        FindingCaseInputContract input,
        FindingCaseContract analysis,
        OpaqueId sourceAssignmentId)
    {
        FindingCaseContractInvariants.Validate(input);
        FindingCaseContractInvariants.Validate(analysis);
        if (input.OriginatingRunId != analysis.OriginatingRunId
            || input.CandidateAnalysis.OriginatingRunId != analysis.OriginatingRunId)
        {
            throw new InvalidDataException(
                "Finding-report publication requires one exact canonical run.");
        }

        FindingReportCoverage[] coverage = GenericCoverage(analysis);
        string[] failures = analysis.CoverageFailures
            .Select(item => $"{item.FailureCode}: {item.Message}")
            .ToArray();
        string[] exclusions = analysis.Coverage.SelectMany(item => item.Exclusions)
            .Select(item => $"{item.MemberId.Value}: {item.Reason}")
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] gaps = analysis.Gaps
            .Select(item => $"{item.PopulationId}: {item.MissingCapabilityOrInformation}")
            .ToArray();
        List<FindingReportDocument> reports = [];

        foreach (FindingContract finding in analysis.Findings)
        {
            AnalysisCaseContract? @case = analysis.Cases.SingleOrDefault(item =>
                item.FindingOccurrenceIds.Contains(finding.FindingOccurrenceId));
            FindingRecommendationContract? recommendation = analysis.Recommendations
                .SingleOrDefault(item => item.FindingOccurrenceId == finding.FindingOccurrenceId);
            TaxonomyAssignmentContract[] taxonomy = analysis.TaxonomyAssignments
                .Where(item => finding.TaxonomyAssignmentIds.Contains(item.AssignmentId))
                .ToArray();
            reports.Add(CreateGeneric(
                analysis,
                input.CandidateAnalysis.ExecutionInputFingerprint,
                FindingReportState.SupportedFinding,
                finding.FindingOccurrenceId,
                finding.FindingOccurrenceId,
                @case?.CaseOccurrenceId,
                finding.FindingOccurrenceId,
                new(finding.IdentityEnvelope.AnalyzerFamily),
                "Supported finding retained",
                finding.Conclusion,
                finding.Conclusion,
                finding.IdentityEnvelope.AffectedLocus,
                GenericSubjects(finding.FindingOccurrenceId, finding.IdentityEnvelope),
                taxonomy,
                finding.EvidenceIds.Concat(recommendation?.EvidenceIds ?? []),
                [],
                finding.IdentityEnvelope.ApplicabilityPredicates,
                recommendation is null ? [] : [recommendation.Uncertainty],
                [],
                coverage,
                failures,
                exclusions,
                gaps,
                recommendation?.Action ?? "Review the exact retained finding and its evidence.",
                recommendation?.Verification ?? "Re-run the exact retained scope after a reversible correction.",
                finding.Severity,
                finding.Confidence,
                sourceAssignmentId));
        }

        foreach (AnalysisCaseContract @case in analysis.Cases.Where(item => item.Kind == CaseOccurrenceKind.LeadOnly))
        {
            FindingRecommendationContract? recommendation = analysis.Recommendations
                .FirstOrDefault(item => item.LeadHypothesisId is not null
                    && @case.HypothesisIds.Contains(item.LeadHypothesisId));
            reports.Add(CreateGeneric(
                analysis,
                input.CandidateAnalysis.ExecutionInputFingerprint,
                FindingReportState.Limited,
                @case.CaseOccurrenceId,
                null,
                @case.CaseOccurrenceId,
                @case.CaseOccurrenceId,
                new(@case.IdentityEnvelope.AnalyzerFamily),
                "Lead-only investigation retained",
                @case.SharedCause,
                @case.SharedCause,
                @case.IdentityEnvelope.AffectedLocus,
                GenericSubjects(@case.CaseOccurrenceId, @case.IdentityEnvelope),
                [],
                @case.CauseProofEvidenceIds.Concat(recommendation?.EvidenceIds ?? []),
                [],
                @case.IdentityEnvelope.ApplicabilityPredicates,
                recommendation is null ? ["No supported finding was established."] : [recommendation.Uncertainty],
                ["This lead remains separate from supported findings and readiness."],
                coverage,
                failures,
                exclusions,
                gaps,
                recommendation?.Action ?? "Collect additional evidence before treating this lead as a finding.",
                recommendation?.Verification ?? "Re-run the exact scope after the missing evidence is retained.",
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified,
                sourceAssignmentId));
        }

        foreach (FindingCaseAbstentionContract abstention in analysis.Abstentions)
        {
            FindingRecommendationContract? recommendation = analysis.Recommendations
                .SingleOrDefault(item => item.AbstentionId == abstention.AbstentionId);
            reports.Add(CreateGeneric(
                analysis,
                input.CandidateAnalysis.ExecutionInputFingerprint,
                FindingReportState.Abstention,
                abstention.AbstentionId,
                null,
                analysis.Cases.SingleOrDefault(item => item.HypothesisIds.Contains(abstention.HypothesisId))?.CaseOccurrenceId,
                abstention.AbstentionId,
                input.CandidateAnalysis.AnalyzerId,
                "Analysis abstained",
                abstention.Reason,
                abstention.Reason,
                "No supported conclusion was established for this exact subject.",
                [new("analysis-subject", abstention.AbstentionId, "Exact abstained analysis subject.")],
                [],
                abstention.EvidenceIds.Concat(recommendation?.EvidenceIds ?? []),
                [],
                [],
                recommendation is null ? ["Required information is missing."] : [recommendation.Uncertainty],
                abstention.RequiredInformation,
                coverage,
                failures,
                exclusions,
                gaps,
                recommendation?.Action ?? "Collect the required information before making a compatibility decision.",
                recommendation?.Verification ?? "Re-run the exact subject after the missing information is retained.",
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified,
                sourceAssignmentId));
        }

        foreach (CoverageFailureFactContract failure in analysis.CoverageFailures)
        {
            reports.Add(CreateGeneric(
                analysis,
                input.CandidateAnalysis.ExecutionInputFingerprint,
                FindingReportState.Failure,
                failure.FailureId,
                null,
                null,
                failure.FailureId,
                failure.AnalyzerId,
                "Analysis failed for one subject",
                failure.Message,
                failure.FailureCode,
                "No semantic conclusion was established for the failed subject.",
                [new("analysis-subject", failure.FailureId, "Exact failed analysis subject.")],
                [],
                [],
                [],
                [],
                [failure.Retryable ? "The failure is marked retryable." : "The failure is not marked retryable."],
                [],
                coverage,
                [$"{failure.FailureCode}: {failure.Message}"],
                exclusions,
                gaps,
                failure.Retryable
                    ? "Correct the input or transient failure and retry the exact subject."
                    : "Inspect and correct the named failure before retrying.",
                "Confirm a completed retained result replaces this failure.",
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified,
                sourceAssignmentId));
        }

        foreach (FindingCaseGapContract gap in analysis.Gaps)
        {
            reports.Add(CreateGeneric(
                analysis,
                input.CandidateAnalysis.ExecutionInputFingerprint,
                FindingReportState.CoverageGap,
                gap.GapId,
                null,
                null,
                gap.GapId,
                new(gap.StageId),
                "Coverage gap retained",
                gap.Reason,
                gap.MissingCapabilityOrInformation,
                $"Population: {gap.PopulationId}; state: {gap.State}.",
                [new("coverage-population", new(gap.PopulationId), "Exact population with incomplete coverage.")],
                [],
                gap.EvidenceIds,
                [],
                [],
                ["No supported conclusion is available outside retained coverage."],
                [gap.MissingCapabilityOrInformation],
                coverage,
                failures,
                exclusions,
                [$"{gap.PopulationId}: {gap.MissingCapabilityOrInformation}"],
                "Resolve the named coverage gap before broadening the conclusion.",
                "Re-run the exact population and confirm the gap is replaced by retained coverage.",
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified,
                sourceAssignmentId));
        }

        foreach (CandidateDecisionContract decision in input.CandidateAnalysis.Decisions
            .Where(item => item.Disposition == CandidateDecisionDisposition.ResolvedNegative))
        {
            reports.Add(CreateGeneric(
                analysis,
                input.CandidateAnalysis.ExecutionInputFingerprint,
                FindingReportState.ResolvedNegative,
                decision.DecisionId,
                null,
                null,
                decision.PopulationMemberId,
                decision.AnalyzerId,
                "Resolved negative retained",
                decision.Rationale,
                decision.Rationale,
                "The exact candidate was evaluated without establishing a supported finding.",
                decision.Participants.Count == 0
                    ? [new("analysis-subject", decision.PopulationMemberId, "Exact resolved-negative subject.")]
                    : decision.Participants.Select(item => new FindingReportSubject(
                        item.Role, item.ParticipantId, "Exact retained candidate participant.")).ToArray(),
                [],
                decision.EvidenceIds,
                [],
                [],
                ["Severity and confidence are not assigned to a resolved negative."],
                [],
                coverage,
                failures,
                exclusions,
                gaps,
                "No corrective action is recommended for this exact resolved subject.",
                "Re-run the exact subject only if its retained inputs or applicability change.",
                FindingSeverity.Unspecified,
                AnalysisConfidence.Unspecified,
                sourceAssignmentId));
        }

        return CanonicalRoundTrip(reports);
    }

    private static FindingReportDocument CreateGeneric(
        FindingCaseContract analysis,
        Sha256Fingerprint sourceInputFingerprint,
        FindingReportState state,
        OpaqueId reportSourceId,
        OpaqueId? findingId,
        OpaqueId? caseId,
        OpaqueId subjectId,
        OpaqueId analyzerId,
        string title,
        string conclusion,
        string whatHappened,
        string whyItMatters,
        IReadOnlyList<FindingReportSubject> subjects,
        IReadOnlyList<TaxonomyAssignmentContract> taxonomy,
        IEnumerable<OpaqueId> supporting,
        IEnumerable<OpaqueId> contradicting,
        IEnumerable<string> applicability,
        IEnumerable<string> uncertainty,
        IEnumerable<string> unresolved,
        IReadOnlyList<FindingReportCoverage> coverage,
        IReadOnlyList<string> failures,
        IReadOnlyList<string> exclusions,
        IReadOnlyList<string> gaps,
        string action,
        string validation,
        FindingSeverity severity,
        AnalysisConfidence confidence,
        OpaqueId sourceAssignmentId) => new(
            FindingReportContract.SchemaId,
            FindingReportContract.SchemaVersion,
            FindingReportContract.ContractMaturity,
            ReportId(analysis.OriginatingRunId, subjectId, state, reportSourceId),
            state,
            analysis.OriginatingRunId,
            analyzerId,
            findingId,
            caseId,
            subjectId,
            title,
            conclusion,
            whatHappened,
            whyItMatters,
            subjects,
            taxonomy.Select(item => new FindingReportTaxonomyAssignment(
                item.Axis, item.Code, item.Applicability.ToString(), item.Reason)).ToArray(),
            GenericAssessment(state, severity, confidence),
            Distinct(supporting),
            Distinct(contradicting),
            [],
            applicability.Distinct(StringComparer.Ordinal).ToArray(),
            uncertainty.Distinct(StringComparer.Ordinal).ToArray(),
            unresolved.Distinct(StringComparer.Ordinal).ToArray(),
            coverage,
            failures,
            exclusions,
            gaps,
            action,
            validation,
            new(
                analysis.SchemaId,
                analysis.SchemaVersion,
                analysis.PayloadId,
                sourceInputFingerprint,
                sourceAssignmentId,
                true,
                "raw-run-output-is-canonical"),
            [.. UnsupportedStatements, analysis.PublicationClaimBoundary]);

    private static FindingReportAssessment GenericAssessment(
        FindingReportState state,
        FindingSeverity severity,
        AnalysisConfidence confidence) => new(
            state == FindingReportState.SupportedFinding ? severity : FindingSeverity.Unspecified,
            state == FindingReportState.SupportedFinding
                ? "Severity is the exact analyzer-local retained finding assessment."
                : "Severity is not assigned because this report is not a supported finding.",
            state == FindingReportState.SupportedFinding ? confidence : AnalysisConfidence.Unspecified,
            state == FindingReportState.SupportedFinding
                ? "Confidence is the exact analyzer-local retained finding assessment."
                : "Confidence is not assigned because this report is not a supported finding.",
            AnalyzerMaturity.Experimental,
            "The analyzer remains Experimental while broader reliability evidence is deferred.",
            "Severity and confidence are not calibrated across analyzers or problem types.");

    private static FindingReportSubject[] GenericSubjects(
        OpaqueId fallbackSubjectId,
        IdentityEnvelopeContract envelope)
    {
        FindingReportSubject[] subjects = envelope.ParticipantsAndRoles
            .Select(item => new FindingReportSubject(item.Value, new(item.Key), envelope.AffectedLocus))
            .ToArray();
        return subjects.Length == 0
            ? [new("affected-locus", fallbackSubjectId, envelope.AffectedLocus)]
            : subjects;
    }

    private static FindingReportCoverage[] GenericCoverage(FindingCaseContract analysis) =>
        analysis.Coverage.Select(item =>
        {
            long completed = item.MemberResults.Count(value => value.State == CoverageMemberState.Completed);
            long completedWithGaps = item.MemberResults.Count(value => value.State == CoverageMemberState.CompletedWithGaps);
            long failed = item.MemberResults.Count(value => value.State == CoverageMemberState.Failed);
            long skipped = item.MemberResults.Count(value => value.State is CoverageMemberState.SkippedByConfiguration
                or CoverageMemberState.SkippedByLimit or CoverageMemberState.Unsupported);
            if (item.MemberResults.Count == 0)
            {
                completed = item.CompletedCount;
                skipped = item.Denominator - completed;
            }
            return new FindingReportCoverage(
                item.PopulationId, item.Denominator, completed, completedWithGaps, failed, skipped);
        }).ToArray();

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
