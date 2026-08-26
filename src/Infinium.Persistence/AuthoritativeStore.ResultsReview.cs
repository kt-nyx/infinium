using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed record ResultCoveragePersistenceRecord(
    string PopulationId,
    string DenominatorLabel,
    long Denominator,
    long Completed,
    string State,
    IReadOnlyList<string> Gaps);

public sealed record ResultOverviewPersistenceRecord(
    string RunId,
    string Readiness,
    string Summary,
    long FindingCount,
    long SupportedCaseCount,
    long LeadOnlyCaseCount,
    IReadOnlyList<ResultCoveragePersistenceRecord> Coverage,
    IReadOnlyList<string> Failures,
    IReadOnlyList<string> Gaps,
    string DurationState,
    long DurationMilliseconds,
    string CostState,
    long CalculatedCostNanoUsd,
    bool NoSafetyGuarantee,
    string ProjectionVersion);

public sealed record ResultItemPersistenceRecord(
    string RunId,
    string ItemId,
    string LogicalId,
    string? CaseOccurrenceId,
    string Kind,
    string Summary,
    string Severity,
    string Confidence,
    string AnalyzerId,
    string AnalyzerVersion,
    IReadOnlyList<string> SubjectIds,
    IReadOnlyList<string> EvidenceIds,
    string SourcePayloadId,
    string SourcePayloadSha256);

public sealed record ResultItemPagePersistenceRecord(
    IReadOnlyList<ResultItemPersistenceRecord> Items,
    bool HasMore,
    string? NextItemId,
    string ProjectionVersion);

public sealed record FindingReportPublicationPayload(
    FindingReportDocument Report,
    byte[] SerializedPayload);

public sealed record FindingReportSummaryPersistenceRecord(
    string ReportId,
    string RunId,
    string State,
    string? FindingId,
    string? CaseId,
    string SubjectId,
    string Title,
    string Conclusion,
    string AnalyzerId,
    string ReportPayloadId,
    string ReportPayloadSha256,
    string SourcePayloadId,
    string SourcePayloadSha256);

public sealed record FindingReportPagePersistenceRecord(
    IReadOnlyList<FindingReportSummaryPersistenceRecord> Items,
    bool HasMore,
    string ProjectionVersion);

public sealed record FindingReportDetailPersistenceRecord(
    FindingReportSummaryPersistenceRecord Summary,
    byte[] ReportPayload);

public sealed record ReviewEventPersistenceRecord(
    string EventId,
    long Revision,
    string EventKind,
    string Disposition,
    bool Suppressed,
    string Annotation,
    string? SourceEventId,
    string? ContinuityAssessmentId,
    DateTimeOffset CreatedAt);

public sealed record ReviewStatePersistenceRecord(
    string RunId,
    string SubjectKind,
    string SubjectOccurrenceId,
    long Revision,
    string Disposition,
    bool Suppressed,
    string Annotation,
    IReadOnlyList<ReviewEventPersistenceRecord> History,
    bool HistoryTruncated);

public sealed record ReviewMutationPersistenceRequest(
    string IdempotencyKey,
    string RunId,
    string SubjectKind,
    string SubjectOccurrenceId,
    long ExpectedRevision,
    string EventKind,
    string Disposition,
    bool Suppressed,
    string Annotation,
    string? SourceEventId,
    string? ContinuityAssessmentId);

public sealed record ReviewMutationPersistenceResult(
    ReviewStatePersistenceRecord State,
    bool Conflict,
    long ExpectedRevision);

public sealed record AssumptionStatePersistenceRecord(
    string AssumptionId,
    string ProfileId,
    long Revision,
    string Origin,
    string Confirmation,
    string Subject,
    string Value,
    string Scope,
    IReadOnlyList<string> DependencyIds,
    bool Effective,
    string AnalysisContextId,
    DateTimeOffset CreatedAt);

public sealed record AssumptionMutationPersistenceRequest(
    string IdempotencyKey,
    string AssumptionId,
    string ProfileId,
    long ExpectedRevision,
    string EventKind,
    string Origin,
    string Confirmation,
    string Subject,
    string Value,
    string Scope,
    IReadOnlyList<string> DependencyIds);

public sealed record AssumptionMutationPersistenceResult(
    AssumptionStatePersistenceRecord State,
    bool Conflict,
    long ExpectedRevision);

public sealed record TargetedVerificationPersistenceRecord(
    string VerificationId,
    string SourceRunId,
    string SuccessorRunId,
    string? SourceFindingOccurrenceId,
    string? SourceCaseOccurrenceId,
    IReadOnlyList<string> ExactScopeIds,
    string ReadinessBoundary,
    string State,
    DateTimeOffset CreatedAt);

public sealed record StructuredExportPersistenceRequest(
    string IdempotencyKey,
    string RunId,
    IReadOnlyList<string> SelectedResultItemIds,
    IReadOnlyList<string> SelectedReviewEventIds,
    IReadOnlyList<string> SelectedAssumptionIds,
    IReadOnlyList<string> Filters,
    string SharingClass,
    IReadOnlyList<string> DeclaredOmissions,
    IReadOnlyList<string> PrivacyDecisions,
    IReadOnlyList<string> SourcePolicyDecisions);

public sealed record StructuredExportPersistenceRecord(
    string ExportId,
    string RunId,
    string SharingClass,
    string SchemaIdentity,
    string GeneratorIdentity,
    string SelectionManifestSha256,
    string ArtifactSha256,
    long ArtifactBytes,
    IReadOnlyList<string> SelectedResultItemIds,
    IReadOnlyList<string> SelectedReviewEventIds,
    IReadOnlyList<string> SelectedAssumptionIds,
    IReadOnlyList<string> Filters,
    IReadOnlyList<string> DeclaredOmissions,
    IReadOnlyList<string> PrivacyDecisions,
    IReadOnlyList<string> SourcePolicyDecisions,
    IReadOnlyList<string> ProvenanceIds,
    DateTimeOffset CreatedAt,
    string State,
    DateTimeOffset? DeletedAt,
    long EventRevision,
    IReadOnlyList<StructuredExportEventPersistenceRecord> History,
    bool HistoryTruncated);

public sealed record StructuredExportEventPersistenceRecord(
    string EventId,
    long Revision,
    string EventKind,
    string RequestFingerprintSha256,
    DateTimeOffset CreatedAt);

public sealed record StructuredExportDeletionPreviewPersistenceRecord(
    string ExportId,
    string CurrentState,
    bool ArtifactPresent,
    bool AuditHistoryRetained);

public sealed record DeletionPreviewPersistenceRecord(
    string SourceId,
    IReadOnlyList<string> ReviewEventIds,
    IReadOnlyList<string> ExportIds,
    IReadOnlyList<string> AuditEffects,
    bool RequiresExplicitCascade);

public sealed partial class AuthoritativeStore
{
    private const int MaximumReviewTextBytes = 16 * 1024;
    private const int MaximumExportBytes = 1_048_576;
    private static readonly JsonSerializerOptions StructuredExportJsonOptions = new() { WriteIndented = true };

    private void IndexResultProjection(
        FindingCaseContract value,
        string payloadId,
        string payloadSha256,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        foreach (FindingContract finding in value.Findings)
        {
            AnalysisCaseContract? @case = value.Cases.SingleOrDefault(item =>
                item.FindingOccurrenceIds.Contains(finding.FindingOccurrenceId));
            InsertResultProjection(
                value.OriginatingRunId.Value,
                finding.FindingOccurrenceId.Value,
                finding.LogicalFindingId.Value,
                @case?.CaseOccurrenceId.Value,
                "finding",
                finding.Conclusion,
                Kebab(finding.Severity),
                Kebab(finding.Confidence),
                finding.IdentityEnvelope.AnalyzerFamily,
                finding.IdentityEnvelope.AnalyzerVersion.ToString(),
                Subjects(finding.IdentityEnvelope),
                finding.EvidenceIds.Select(item => item.Value),
                payloadId,
                payloadSha256,
                now,
                transaction);
        }

        foreach (AnalysisCaseContract @case in value.Cases)
        {
            InsertResultProjection(
                value.OriginatingRunId.Value,
                @case.CaseOccurrenceId.Value,
                @case.LogicalCaseId.Value,
                @case.CaseOccurrenceId.Value,
                @case.Kind == CaseOccurrenceKind.Supported ? "supported-case" : "lead-only-case",
                @case.SharedCause,
                "not-applicable",
                "not-applicable",
                @case.IdentityEnvelope.AnalyzerFamily,
                @case.IdentityEnvelope.AnalyzerVersion.ToString(),
                Subjects(@case.IdentityEnvelope),
                @case.CauseProofEvidenceIds.Select(item => item.Value),
                payloadId,
                payloadSha256,
                now,
                transaction);
        }

        foreach (FindingCaseAbstentionContract abstention in value.Abstentions)
        {
            InsertResultProjection(
                value.OriginatingRunId.Value,
                abstention.AbstentionId.Value,
                abstention.HypothesisId.Value,
                value.Cases.SingleOrDefault(item => item.HypothesisIds.Contains(abstention.HypothesisId))?.CaseOccurrenceId.Value,
                "abstention",
                abstention.Reason,
                "not-applicable",
                "not-applicable",
                "finding-case-analysis",
                value.SchemaVersion.ToString(),
                [],
                abstention.EvidenceIds.Select(item => item.Value),
                payloadId,
                payloadSha256,
                now,
                transaction);
        }

        foreach (CoverageFailureFactContract failure in value.CoverageFailures)
        {
            InsertResultProjection(
                value.OriginatingRunId.Value,
                failure.FailureId.Value,
                failure.FailureId.Value,
                null,
                "failure",
                $"{failure.FailureCode}: {failure.Message}",
                "not-applicable",
                "not-applicable",
                failure.AnalyzerId.Value,
                value.SchemaVersion.ToString(),
                [],
                [],
                payloadId,
                payloadSha256,
                now,
                transaction);
        }

        foreach (FindingCaseGapContract gap in value.Gaps)
        {
            InsertResultProjection(
                value.OriginatingRunId.Value,
                gap.GapId.Value,
                gap.GapId.Value,
                null,
                "coverage-gap",
                $"{gap.Reason} Missing: {gap.MissingCapabilityOrInformation}",
                "not-applicable",
                "not-applicable",
                gap.StageId,
                value.SchemaVersion.ToString(),
                [gap.PopulationId],
                gap.EvidenceIds.Select(item => item.Value),
                payloadId,
                payloadSha256,
                now,
                transaction);
        }
    }

    private void IndexFindingReportPublications(
        FindingCaseContract analysis,
        string sourcePayloadId,
        string sourcePayloadSha256,
        IReadOnlyList<FindingReportPublicationPayload> reports,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        foreach (FindingReportPublicationPayload publication in reports)
        {
            FindingReportContract.Validate(publication.Report);
            if (publication.Report.RunId != analysis.OriginatingRunId
                || publication.Report.Provenance.SourcePayloadId != analysis.PayloadId
                || publication.SerializedPayload.Length == 0)
            {
                throw new InvalidDataException(
                    "Finding-report publication must bind the exact canonical finding/case output.");
            }
            string reportPayloadId = AdmitCoordinatorPayload(
                publication.SerializedPayload,
                "finding-report",
                publication.Report.ReportId.Value,
                now,
                transaction);
            string reportSha256 = Convert.ToHexStringLower(SHA256.HashData(publication.SerializedPayload));
            Execute(
                """
                INSERT OR IGNORE INTO finding_report_publications(
                    report_id,run_id,report_state,finding_occurrence_id,case_occurrence_id,
                    subject_id,analyzer_id,inert_title,inert_conclusion,report_payload_id,
                    report_payload_sha256,source_payload_id,source_payload_sha256,created_at)
                VALUES ($report,$run,$state,$finding,$case,$subject,$analyzer,$title,$conclusion,
                    $report_payload,$report_sha,$source_payload,$source_sha,$now);
                """,
                transaction,
                ("$report", publication.Report.ReportId.Value),
                ("$run", publication.Report.RunId.Value),
                ("$state", Kebab(publication.Report.State)),
                ("$finding", publication.Report.FindingId?.Value),
                ("$case", publication.Report.CaseId?.Value),
                ("$subject", publication.Report.SubjectId.Value),
                ("$analyzer", publication.Report.AnalyzerId.Value),
                ("$title", BoundedText(publication.Report.Title, 256)),
                ("$conclusion", BoundedText(publication.Report.Conclusion, 4096)),
                ("$report_payload", reportPayloadId),
                ("$report_sha", reportSha256),
                ("$source_payload", sourcePayloadId),
                ("$source_sha", sourcePayloadSha256),
                ("$now", ToText(now)));
            RequireFindingCaseRow(
                """
                SELECT COUNT(*) FROM finding_report_publications
                WHERE report_id=$report AND run_id=$run AND report_state=$state
                  AND report_payload_id=$report_payload AND report_payload_sha256=$report_sha
                  AND source_payload_id=$source_payload AND source_payload_sha256=$source_sha;
                """,
                "A finding report resolves to different retained semantics.",
                transaction,
                ("$report", publication.Report.ReportId.Value),
                ("$run", publication.Report.RunId.Value),
                ("$state", Kebab(publication.Report.State)),
                ("$report_payload", reportPayloadId),
                ("$report_sha", reportSha256),
                ("$source_payload", sourcePayloadId),
                ("$source_sha", sourcePayloadSha256));
        }
    }

    internal void IndexResultProjectionBatch(
        IReadOnlyList<ResultItemPersistenceRecord> items,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(items));
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            foreach (ResultItemPersistenceRecord item in items)
            {
                InsertResultProjection(
                    item.RunId, item.ItemId, item.LogicalId, item.CaseOccurrenceId, item.Kind,
                    item.Summary, item.Severity, item.Confidence, item.AnalyzerId, item.AnalyzerVersion,
                    item.SubjectIds, item.EvidenceIds, item.SourcePayloadId, item.SourcePayloadSha256,
                    now, transaction);
            }
            transaction.Commit();
        }
    }

    private void InsertResultProjection(
        string runId,
        string itemId,
        string logicalId,
        string? caseOccurrenceId,
        string kind,
        string summary,
        string severity,
        string confidence,
        string analyzerId,
        string analyzerVersion,
        IEnumerable<string> subjectIds,
        IEnumerable<string> evidenceIds,
        string payloadId,
        string payloadSha256,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        string subjectsJson = JsonSerializer.Serialize(subjectIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        string evidenceJson = JsonSerializer.Serialize(evidenceIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Execute(
            """
            INSERT OR IGNORE INTO result_projection_items(
                run_id,item_id,logical_id,case_occurrence_id,item_kind,inert_summary,severity,
                confidence,analyzer_id,analyzer_version,subject_ids_json,evidence_ids_json,
                source_payload_id,source_payload_sha256,created_at)
            VALUES ($run,$item,$logical,$case,$kind,$summary,$severity,$confidence,$analyzer,$version,
                $subjects,$evidence,$payload,$sha,$now);
            """,
            transaction,
            ("$run", runId), ("$item", itemId), ("$logical", logicalId), ("$case", caseOccurrenceId),
            ("$kind", kind), ("$summary", BoundedText(summary, 4096)), ("$severity", severity),
            ("$confidence", confidence), ("$analyzer", analyzerId), ("$version", analyzerVersion),
            ("$subjects", subjectsJson), ("$evidence", evidenceJson), ("$payload", payloadId),
            ("$sha", payloadSha256), ("$now", ToText(now)));
    }

    public ResultOverviewPersistenceRecord GetResultOverview(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            RunRecord run = GetRunCore(runId);
            long Count(string kind)
            {
                using SqliteCommand count = connection.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM result_projection_items WHERE run_id=$run AND item_kind=$kind;";
                count.Parameters.AddWithValue("$run", runId);
                count.Parameters.AddWithValue("$kind", kind);
                return Convert.ToInt64(count.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }

            List<ResultCoveragePersistenceRecord> coverage = [];
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT population_id,denominator_label,denominator,completed,coverage_state,
                           COALESCE((SELECT json_group_array(g.gap_id) FROM analysis_coverage_gap_links l
                               JOIN analysis_gaps g ON g.gap_id=l.gap_id
                               WHERE l.coverage_result_id=c.coverage_result_id),'[]')
                    FROM analysis_coverage c WHERE run_id=$run ORDER BY population_id;
                    """;
                command.Parameters.AddWithValue("$run", runId);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    coverage.Add(new(
                        reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3),
                        reader.GetString(4), DeserializeStrings(reader.GetString(5))));
                }
            }

            string[] failures = ReadInertStrings(
                "SELECT failure_code || ': ' || message FROM analysis_coverage_failure_links f "
                + "JOIN analysis_coverage c ON c.coverage_result_id=f.coverage_result_id "
                + "WHERE c.run_id=$run ORDER BY failure_id;", runId);
            string[] gaps = ReadInertStrings(
                "SELECT population_id || ': ' || gap_state FROM analysis_gaps WHERE run_id=$run ORDER BY gap_id;", runId);
            string readiness = run.State is LifecycleState.Completed or LifecycleState.CompletedWithGaps
                ? "scope-limited"
                : run.State is LifecycleState.Cancelled or LifecycleState.Failed or LifecycleState.LimitReached
                    or LifecycleState.InvalidatedByChangedInput
                    ? "no-readiness"
                    : "provisional-incomplete";
            long findings = Count("finding");
            long supported = Count("supported-case");
            long leads = Count("lead-only-case");
            string summary =
                $"This bounded run retained {findings} supported findings, {supported} supported cases, "
                + $"{leads} lead-only investigations, {gaps.Length} coverage gaps, and {failures.Length} failures. "
                + "Absence of findings is not evidence that the modlist or playthrough is safe.";
            return new(
                runId, readiness, summary, findings, supported, leads, coverage, failures, gaps,
                "available", Math.Max(0, (long)(run.UpdatedAt - run.CreatedAt).TotalMilliseconds),
                "unavailable", 0, true, "1");
        }
    }

    public ResultItemPagePersistenceRecord ListResultItems(
        string runId,
        IReadOnlyCollection<string> kinds,
        string searchText,
        string sort,
        int maximumCount,
        string? afterItemId,
        string? afterSeverity = null)
    {
        if (maximumCount is < 1 or > 100 || kinds.Count is < 1 or > 6 || searchText.Length > 160)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }
        string[] closedKinds = kinds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (closedKinds.Any(item => item is not (
                "supported-case" or "lead-only-case" or "finding" or "abstention" or "failure" or "coverage-gap"))
            || sort is not ("identity" or "severity"))
        {
            throw new ArgumentException("The result query contains an unsupported filter or sort.");
        }

        lock (gate)
        {
            _ = GetRunCore(runId);
            using SqliteCommand command = connection.CreateCommand();
            string kindParameters = string.Join(',', closedKinds.Select((_, index) => "$kind" + index));
            string order = sort == "severity"
                ? "CASE severity WHEN 'blocker' THEN 0 WHEN 'major' THEN 1 WHEN 'moderate' THEN 2 WHEN 'minor' THEN 3 ELSE 4 END,item_id"
                : "item_id";
            const string severityRank =
                "CASE severity WHEN 'blocker' THEN 0 WHEN 'major' THEN 1 WHEN 'moderate' THEN 2 WHEN 'minor' THEN 3 ELSE 4 END";
            command.CommandText =
                $"""
                SELECT run_id,item_id,logical_id,case_occurrence_id,item_kind,inert_summary,severity,
                       confidence,analyzer_id,analyzer_version,subject_ids_json,evidence_ids_json,
                       source_payload_id,source_payload_sha256
                FROM result_projection_items
                WHERE run_id=$run AND item_kind IN ({kindParameters})
                  AND ($search='' OR instr(lower(inert_summary),lower($search)) > 0)
                  AND (($sort='identity' AND ($after='' OR item_id > $after))
                    OR ($sort='severity' AND ($after=''
                      OR {severityRank} > $after_rank
                      OR ({severityRank} = $after_rank AND item_id > $after))))
                ORDER BY {order}
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$search", searchText);
            command.Parameters.AddWithValue("$after", afterItemId ?? string.Empty);
            command.Parameters.AddWithValue("$sort", sort);
            command.Parameters.AddWithValue("$after_rank", SeverityRank(afterSeverity));
            command.Parameters.AddWithValue("$limit", maximumCount + 1);
            for (int index = 0; index < closedKinds.Length; index++)
            {
                command.Parameters.AddWithValue("$kind" + index, closedKinds[index]);
            }
            using SqliteDataReader reader = command.ExecuteReader();
            List<ResultItemPersistenceRecord> items = [];
            while (reader.Read())
            {
                items.Add(ReadResultItem(reader));
            }
            bool more = items.Count > maximumCount;
            if (more)
            {
                items.RemoveAt(items.Count - 1);
            }
            return new(items, more, more ? items[^1].ItemId : null, "1");
        }
    }

    public ResultItemPersistenceRecord GetResultItem(string runId, string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        lock (gate)
        {
            _ = GetRunCore(runId);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT run_id,item_id,logical_id,case_occurrence_id,item_kind,inert_summary,severity,
                       confidence,analyzer_id,analyzer_version,subject_ids_json,evidence_ids_json,
                       source_payload_id,source_payload_sha256
                FROM result_projection_items WHERE run_id=$run AND item_id=$item;
                """;
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$item", itemId);
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read()
                ? ReadResultItem(reader)
                : throw new KeyNotFoundException("The requested result item does not exist in the run.");
        }
    }

    public string GetResultProjectionIdentity(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            _ = GetRunCore(runId);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT (SELECT COUNT(*) FROM result_projection_items WHERE run_id=$run), "
                + "COALESCE((SELECT group_concat(source_payload_sha256,',') FROM "
                + "(SELECT source_payload_sha256 FROM result_projection_items WHERE run_id=$run "
                + "GROUP BY source_payload_sha256 ORDER BY source_payload_sha256)),'');";
            command.Parameters.AddWithValue("$run", runId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException("The result projection does not exist.");
            }
            return Sha256($"{runId}\n{reader.GetInt64(0).ToString(CultureInfo.InvariantCulture)}\n{reader.GetString(1)}");
        }
    }

    public IReadOnlyList<ResultItemPersistenceRecord> GetFocusedResultItems(
        string runId,
        string exactSubjectId,
        int maximumCount)
    {
        if (string.IsNullOrWhiteSpace(exactSubjectId) || exactSubjectId.Length > 160
            || maximumCount is < 1 or > 100)
        {
            throw new ArgumentException("The focused subject query is malformed or exceeds its bound.");
        }
        lock (gate)
        {
            _ = GetRunCore(runId);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT r.run_id,r.item_id,r.logical_id,r.case_occurrence_id,r.item_kind,r.inert_summary,
                       r.severity,r.confidence,r.analyzer_id,r.analyzer_version,r.subject_ids_json,
                       r.evidence_ids_json,r.source_payload_id,r.source_payload_sha256
                FROM result_projection_items r
                WHERE r.run_id=$run AND EXISTS (
                    SELECT 1 FROM json_each(r.subject_ids_json) subject WHERE subject.value=$subject)
                ORDER BY r.item_id LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$subject", exactSubjectId);
            command.Parameters.AddWithValue("$limit", maximumCount + 1);
            using SqliteDataReader reader = command.ExecuteReader();
            List<ResultItemPersistenceRecord> items = [];
            while (reader.Read())
            {
                items.Add(ReadResultItem(reader));
            }
            return items;
        }
    }

    public FindingReportPagePersistenceRecord ListFindingReports(
        string runId,
        IReadOnlyCollection<string> states,
        string searchText,
        string sort,
        int maximumCount,
        string? afterReportId,
        string? afterState)
    {
        if (maximumCount is < 1 or > 100 || states.Count is < 1 or > 6
            || Encoding.UTF8.GetByteCount(searchText) > 160)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }
        string[] closedStates = states.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (closedStates.Any(item => item is not (
                "supported-finding" or "resolved-negative" or "abstention" or "failure" or "limited" or "coverage-gap"))
            || sort is not ("identity" or "state"))
        {
            throw new ArgumentException("The finding-report query contains an unsupported filter or sort.");
        }
        lock (gate)
        {
            _ = GetRunCore(runId);
            using SqliteCommand command = connection.CreateCommand();
            string stateParameters = string.Join(',', closedStates.Select((_, index) => "$state" + index));
            string order = sort == "state" ? "report_state,report_id" : "report_id";
            command.CommandText =
                $"""
                SELECT report_id,run_id,report_state,finding_occurrence_id,case_occurrence_id,
                       subject_id,inert_title,inert_conclusion,analyzer_id,report_payload_id,
                       report_payload_sha256,source_payload_id,source_payload_sha256
                FROM finding_report_publications
                WHERE run_id=$run AND report_state IN ({stateParameters})
                  AND ($search='' OR instr(lower(inert_title),lower($search)) > 0
                                   OR instr(lower(inert_conclusion),lower($search)) > 0)
                  AND (($sort='identity' AND ($after='' OR report_id>$after))
                    OR ($sort='state' AND ($after='' OR report_state>$after_state
                      OR (report_state=$after_state AND report_id>$after))))
                ORDER BY {order}
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$search", searchText);
            command.Parameters.AddWithValue("$after", afterReportId ?? string.Empty);
            command.Parameters.AddWithValue("$after_state", afterState ?? string.Empty);
            command.Parameters.AddWithValue("$sort", sort);
            command.Parameters.AddWithValue("$limit", maximumCount + 1);
            for (int index = 0; index < closedStates.Length; index++)
            {
                command.Parameters.AddWithValue("$state" + index, closedStates[index]);
            }
            using SqliteDataReader reader = command.ExecuteReader();
            List<FindingReportSummaryPersistenceRecord> items = [];
            while (reader.Read())
            {
                items.Add(ReadFindingReportSummary(reader));
            }
            bool more = items.Count > maximumCount;
            if (more)
            {
                items.RemoveAt(items.Count - 1);
            }
            return new(items, more, "1");
        }
    }

    public FindingReportDetailPersistenceRecord GetFindingReport(string runId, string reportId)
    {
        ValidateOpaque(runId, nameof(runId));
        ValidateOpaque(reportId, nameof(reportId));
        lock (gate)
        {
            _ = GetRunCore(runId);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT report_id,run_id,report_state,finding_occurrence_id,case_occurrence_id,
                       subject_id,inert_title,inert_conclusion,analyzer_id,report_payload_id,
                       report_payload_sha256,source_payload_id,source_payload_sha256
                FROM finding_report_publications WHERE run_id=$run AND report_id=$report;
                """;
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$report", reportId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException("The requested finding report does not exist in the run.");
            }
            FindingReportSummaryPersistenceRecord summary = ReadFindingReportSummary(reader);
            reader.Close();
            byte[] payload = ReadCandidateAnalysisPayload(summary.ReportPayloadId);
            string actualSha = Convert.ToHexStringLower(SHA256.HashData(payload));
            if (!StringComparer.Ordinal.Equals(actualSha, summary.ReportPayloadSha256))
            {
                throw new InvalidDataException("The retained finding report failed integrity validation.");
            }
            return new(summary, payload);
        }
    }

    public string GetFindingReportProjectionIdentity(string runId)
    {
        ValidateOpaque(runId, nameof(runId));
        lock (gate)
        {
            _ = GetRunCore(runId);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*),COALESCE(group_concat(report_payload_sha256,','),'')
                FROM (SELECT report_payload_sha256 FROM finding_report_publications
                      WHERE run_id=$run ORDER BY report_id);
                """;
            command.Parameters.AddWithValue("$run", runId);
            using SqliteDataReader reader = command.ExecuteReader();
            reader.Read();
            return Sha256($"{runId}\n{reader.GetInt64(0).ToString(CultureInfo.InvariantCulture)}\n{reader.GetString(1)}");
        }
    }

    public ReviewStatePersistenceRecord GetReviewState(
        string runId,
        string subjectKind,
        string subjectOccurrenceId)
    {
        ValidateReviewSubject(runId, subjectKind, subjectOccurrenceId);
        lock (gate)
        {
            return GetReviewStateCore(runId, subjectKind, subjectOccurrenceId);
        }
    }

    public ReviewMutationPersistenceResult ApplyReviewEvent(
        ReviewMutationPersistenceRequest request,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateReviewMutation(request);
        string canonical = JsonSerializer.Serialize(request);
        string requestSha = Sha256(canonical);
        lock (gate)
        {
            ValidateReviewSubjectCore(request.RunId, request.SubjectKind, request.SubjectOccurrenceId);
            using SqliteTransaction transaction = BeginTransaction();
            string? replaySha = ScalarStringOrNull(
                "SELECT request_sha256 FROM review_events WHERE idempotency_key=$id;",
                transaction,
                ("$id", request.IdempotencyKey));
            if (replaySha is not null)
            {
                if (!StringComparer.Ordinal.Equals(replaySha, requestSha))
                {
                    throw new InvalidOperationException("A review idempotency key cannot be rebound.");
                }
                transaction.Commit();
                return new(GetReviewStateCore(request.RunId, request.SubjectKind, request.SubjectOccurrenceId), false, request.ExpectedRevision);
            }

            ReviewStatePersistenceRecord current = GetReviewStateCore(
                request.RunId, request.SubjectKind, request.SubjectOccurrenceId);
            if (current.Revision != request.ExpectedRevision)
            {
                transaction.Commit();
                return new(current, true, request.ExpectedRevision);
            }
            (string Disposition, bool Suppressed, string Annotation)? carryover =
                request.EventKind == "carryover"
                    ? ValidateReviewCarryover(request, transaction)
                    : null;

            long revision = current.Revision + 1;
            string eventId = StableId("review-event", request.IdempotencyKey, request.SubjectOccurrenceId, revision.ToString(CultureInfo.InvariantCulture));
            string disposition = carryover?.Disposition ?? (request.EventKind == "disposition"
                ? request.Disposition
                : current.Disposition);
            bool suppressed = carryover?.Suppressed ?? (request.EventKind == "suppression"
                ? request.Suppressed
                : current.Suppressed);
            string annotation = carryover?.Annotation ?? (request.EventKind switch
            {
                "annotation" => request.Annotation,
                "remove-annotation" => string.Empty,
                _ => current.Annotation,
            });
            Execute(
                """
                INSERT INTO review_events(
                    event_id,idempotency_key,request_sha256,run_id,subject_kind,subject_occurrence_id,
                    revision,event_kind,disposition,suppressed,annotation,source_event_id,
                    continuity_assessment_id,created_at)
                VALUES ($event,$key,$sha,$run,$kind,$subject,$revision,$event_kind,$disposition,
                    $suppressed,$annotation,$source,$continuity,$now);
                INSERT INTO review_projection(
                    subject_occurrence_id,run_id,subject_kind,revision,disposition,suppressed,
                    annotation,last_event_id,updated_at)
                VALUES ($subject,$run,$kind,$revision,$disposition,$suppressed,$annotation,$event,$now)
                ON CONFLICT(subject_occurrence_id) DO UPDATE SET
                    run_id=excluded.run_id,subject_kind=excluded.subject_kind,revision=excluded.revision,
                    disposition=excluded.disposition,suppressed=excluded.suppressed,
                    annotation=excluded.annotation,last_event_id=excluded.last_event_id,
                    updated_at=excluded.updated_at;
                """,
                transaction,
                ("$event", eventId), ("$key", request.IdempotencyKey), ("$sha", requestSha),
                ("$run", request.RunId), ("$kind", request.SubjectKind),
                ("$subject", request.SubjectOccurrenceId), ("$revision", revision),
                ("$event_kind", request.EventKind), ("$disposition", disposition),
                ("$suppressed", suppressed ? 1 : 0), ("$annotation", annotation),
                ("$source", request.SourceEventId), ("$continuity", request.ContinuityAssessmentId),
                ("$now", ToText(now)));
            transaction.Commit();
            return new(GetReviewStateCore(request.RunId, request.SubjectKind, request.SubjectOccurrenceId), false, request.ExpectedRevision);
        }
    }

    public IReadOnlyList<AssumptionStatePersistenceRecord> ListAssumptions(
        string profileId,
        int maximumCount,
        string? afterAssumptionId)
    {
        ValidateOpaque(profileId, nameof(profileId));
        if (maximumCount is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT assumption_id,profile_id,revision,origin,confirmation,subject,value,scope,
                       dependency_ids_json,effective,analysis_context_id,updated_at
                FROM assumption_projection WHERE profile_id=$profile AND ($after='' OR assumption_id>$after)
                ORDER BY assumption_id LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$profile", profileId);
            command.Parameters.AddWithValue("$after", afterAssumptionId ?? string.Empty);
            command.Parameters.AddWithValue("$limit", maximumCount + 1);
            using SqliteDataReader reader = command.ExecuteReader();
            List<AssumptionStatePersistenceRecord> values = [];
            while (reader.Read())
            {
                values.Add(ReadAssumption(reader));
            }
            return values;
        }
    }

    public string GetAssumptionProjectionIdentity(string profileId)
    {
        ValidateOpaque(profileId, nameof(profileId));
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*),COALESCE(group_concat(identity,','),'') FROM (
                    SELECT assumption_id || ':' || revision || ':' || analysis_context_id || ':' || updated_at AS identity
                    FROM assumption_projection WHERE profile_id=$profile ORDER BY assumption_id);
                """;
            command.Parameters.AddWithValue("$profile", profileId);
            using SqliteDataReader reader = command.ExecuteReader();
            reader.Read();
            return Sha256($"{profileId}\n{reader.GetInt64(0).ToString(CultureInfo.InvariantCulture)}\n{reader.GetString(1)}");
        }
    }

    public AssumptionMutationPersistenceResult ApplyAssumptionEvent(
        AssumptionMutationPersistenceRequest request,
        DateTimeOffset now)
    {
        ValidateAssumptionMutation(request);
        string requestSha = Sha256(JsonSerializer.Serialize(request));
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            string? replaySha = ScalarStringOrNull(
                "SELECT request_sha256 FROM assumption_events WHERE idempotency_key=$id;",
                transaction,
                ("$id", request.IdempotencyKey));
            if (replaySha is not null)
            {
                if (!StringComparer.Ordinal.Equals(replaySha, requestSha))
                {
                    throw new InvalidOperationException("An assumption idempotency key cannot be rebound.");
                }
                transaction.Commit();
                return new(GetAssumptionCore(request.AssumptionId), false, request.ExpectedRevision);
            }

            AssumptionStatePersistenceRecord? current = FindAssumptionCore(request.AssumptionId);
            long currentRevision = current?.Revision ?? 0;
            if (currentRevision != request.ExpectedRevision)
            {
                if (current is null)
                {
                    throw new KeyNotFoundException("The assumption does not exist at the expected revision.");
                }
                transaction.Commit();
                return new(current, true, request.ExpectedRevision);
            }
            if ((current is null) != (request.EventKind == "create"))
            {
                throw new InvalidOperationException("Assumption creation and successor events require distinct states.");
            }
            if (current is not null
                && (!StringComparer.Ordinal.Equals(current.ProfileId, request.ProfileId)
                    || !StringComparer.Ordinal.Equals(current.Origin, request.Origin)))
            {
                throw new InvalidOperationException("Assumption profile and origin are immutable across successors.");
            }

            long revision = currentRevision + 1;
            bool effective = request.EventKind != "remove";
            string eventId = StableId("assumption-event", request.IdempotencyKey, request.AssumptionId, revision.ToString(CultureInfo.InvariantCulture));
            string contextId = StableId("analysis-context", request.ProfileId, request.AssumptionId, revision.ToString(CultureInfo.InvariantCulture), requestSha);
            string dependencies = JsonSerializer.Serialize(request.DependencyIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            Execute(
                """
                INSERT INTO assumption_events(
                    event_id,idempotency_key,request_sha256,assumption_id,profile_id,revision,event_kind,
                    origin,confirmation,subject,value,scope,dependency_ids_json,effective,
                    analysis_context_id,predecessor_event_id,created_at)
                VALUES ($event,$key,$sha,$assumption,$profile,$revision,$kind,$origin,$confirmation,
                    $subject,$value,$scope,$dependencies,$effective,$context,$prior,$now);
                INSERT INTO assumption_projection(
                    assumption_id,profile_id,revision,origin,confirmation,subject,value,scope,
                    dependency_ids_json,effective,analysis_context_id,last_event_id,updated_at)
                VALUES ($assumption,$profile,$revision,$origin,$confirmation,$subject,$value,$scope,
                    $dependencies,$effective,$context,$event,$now)
                ON CONFLICT(assumption_id) DO UPDATE SET
                    profile_id=excluded.profile_id,revision=excluded.revision,origin=excluded.origin,
                    confirmation=excluded.confirmation,subject=excluded.subject,value=excluded.value,
                    scope=excluded.scope,dependency_ids_json=excluded.dependency_ids_json,
                    effective=excluded.effective,analysis_context_id=excluded.analysis_context_id,
                    last_event_id=excluded.last_event_id,updated_at=excluded.updated_at;
                """,
                transaction,
                ("$event", eventId), ("$key", request.IdempotencyKey), ("$sha", requestSha),
                ("$assumption", request.AssumptionId), ("$profile", request.ProfileId),
                ("$revision", revision), ("$kind", request.EventKind), ("$origin", request.Origin),
                ("$confirmation", request.Confirmation), ("$subject", request.Subject),
                ("$value", request.Value), ("$scope", request.Scope), ("$dependencies", dependencies),
                ("$effective", effective ? 1 : 0), ("$context", contextId),
                ("$prior", current is null ? null : ScalarStringOrNull(
                    "SELECT last_event_id FROM assumption_projection WHERE assumption_id=$id;",
                    transaction,
                    ("$id", request.AssumptionId))),
                ("$now", ToText(now)));
            transaction.Commit();
            return new(GetAssumptionCore(request.AssumptionId), false, request.ExpectedRevision);
        }
    }

    public TargetedVerificationPersistenceRecord StartTargetedVerification(
        string idempotencyKey,
        string requestedRunId,
        string sourceRunId,
        string? sourceFindingOccurrenceId,
        string? sourceCaseOccurrenceId,
        IReadOnlyList<string> exactScopeIds,
        string userGestureId,
        DateTimeOffset dispatchDeadline,
        long coordinatorFencingEpoch,
        DateTimeOffset now)
    {
        ValidateOpaque(idempotencyKey, nameof(idempotencyKey));
        ValidateOpaque(requestedRunId, nameof(requestedRunId));
        ValidateOpaque(userGestureId, nameof(userGestureId));
        if (userGestureId.Length < 16 || exactScopeIds.Count is < 1 or > 100
            || dispatchDeadline <= now
            || (string.IsNullOrWhiteSpace(sourceFindingOccurrenceId)
                && string.IsNullOrWhiteSpace(sourceCaseOccurrenceId)))
        {
            throw new ArgumentException("The targeted verification request is incomplete or unbounded.");
        }
        RunRecord source = GetRun(sourceRunId);
        if (requestedRunId == sourceRunId
            || source.State is not (LifecycleState.Completed or LifecycleState.CompletedWithGaps
                or LifecycleState.Failed or LifecycleState.Cancelled or LifecycleState.LimitReached
                or LifecycleState.InvalidatedByChangedInput))
        {
            throw new InvalidOperationException(
                "Targeted verification requires a distinct successor and an immutable terminal source run.");
        }
        ValidateTargetedSource(sourceRunId, sourceFindingOccurrenceId, sourceCaseOccurrenceId);
        if (sourceFindingOccurrenceId is not null && sourceCaseOccurrenceId is not null)
        {
            ResultItemPersistenceRecord selectedFinding = GetResultItem(sourceRunId, sourceFindingOccurrenceId);
            if (!StringComparer.Ordinal.Equals(selectedFinding.CaseOccurrenceId, sourceCaseOccurrenceId))
            {
                throw new ArgumentException(
                    "The selected source finding is not retained by the selected source case.");
            }
        }
        string[] scopes = exactScopeIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        HashSet<string> permittedScopes = new(StringComparer.Ordinal);
        if (sourceFindingOccurrenceId is not null)
        {
            permittedScopes.UnionWith(GetResultItem(sourceRunId, sourceFindingOccurrenceId).SubjectIds);
        }
        if (sourceCaseOccurrenceId is not null)
        {
            permittedScopes.UnionWith(GetResultItem(sourceRunId, sourceCaseOccurrenceId).SubjectIds);
        }
        if (scopes.Any(scope => !permittedScopes.Contains(scope)))
        {
            throw new ArgumentException(
                "Targeted verification scope must be retained by the exact source finding or case.");
        }
        string requestJson = JsonSerializer.Serialize(new
        {
            schema_identity = "infinium.application.targeted-verification/v1",
            source_run_id = sourceRunId,
            source_finding_occurrence_id = sourceFindingOccurrenceId,
            source_case_occurrence_id = sourceCaseOccurrenceId,
            exact_scope_ids = scopes,
            readiness_boundary = "scope-limited",
        });
        string requestSha = Sha256(requestJson);
        string verificationId = StableId("targeted-verification", idempotencyKey, sourceRunId, requestSha);
        RunRecord successor = CreateRun(
            idempotencyKey, requestedRunId, source.Binding, coordinatorFencingEpoch, now,
            "manual-targeted-verification", dispatchDeadline, "targeted-verification", requestJson);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            string? replaySha = ScalarStringOrNull(
                "SELECT request_sha256 FROM targeted_verifications WHERE idempotency_key=$id;",
                transaction,
                ("$id", idempotencyKey));
            if (replaySha is not null && !StringComparer.Ordinal.Equals(replaySha, requestSha))
            {
                throw new InvalidOperationException("A targeted-verification idempotency key cannot be rebound.");
            }
            Execute(
                """
                INSERT OR IGNORE INTO targeted_verifications(
                    verification_id,idempotency_key,request_sha256,source_run_id,successor_run_id,
                    source_finding_occurrence_id,source_case_occurrence_id,exact_scope_ids_json,
                    user_gesture_id,readiness_boundary,state,created_at)
                VALUES ($verification,$key,$sha,$source,$successor,$finding,$case,$scope,$gesture,
                    'scope-limited','manually-initiated',$now);
                """,
                transaction,
                ("$verification", verificationId), ("$key", idempotencyKey), ("$sha", requestSha),
                ("$source", sourceRunId), ("$successor", successor.RunId),
                ("$finding", sourceFindingOccurrenceId), ("$case", sourceCaseOccurrenceId),
                ("$scope", JsonSerializer.Serialize(scopes)), ("$gesture", userGestureId),
                ("$now", ToText(now)));
            transaction.Commit();
            return GetTargetedVerificationCore(verificationId);
        }
    }

    public StructuredExportPersistenceRecord CreateStructuredExport(
        StructuredExportPersistenceRequest request,
        DateTimeOffset now)
    {
        ValidateExportRequest(request);
        string requestJson = JsonSerializer.Serialize(request);
        string requestSha = Sha256(requestJson);
        lock (gate)
        {
            using (SqliteCommand replay = connection.CreateCommand())
            {
                replay.CommandText = "SELECT export_id,request_sha256 FROM structured_exports WHERE idempotency_key=$id;";
                replay.Parameters.AddWithValue("$id", request.IdempotencyKey);
                using SqliteDataReader reader = replay.ExecuteReader();
                if (reader.Read())
                {
                    if (!StringComparer.Ordinal.Equals(reader.GetString(1), requestSha))
                    {
                        throw new InvalidOperationException("An export idempotency key cannot be rebound.");
                    }
                    string existingId = reader.GetString(0);
                    reader.Close();
                    return GetStructuredExportCore(existingId);
                }
            }

            RunRecord sourceRun = GetRunCore(request.RunId);
            string[] itemIds = ClosedSelection(request.SelectedResultItemIds, 100, "result item");
            string[] reviewIds = ClosedSelection(request.SelectedReviewEventIds, 100, "review event");
            string[] assumptionIds = ClosedSelection(request.SelectedAssumptionIds, 100, "assumption");
            foreach (string item in itemIds)
            {
                _ = GetResultItem(request.RunId, item);
            }
            ValidateSelectedIds("review_events", "event_id", reviewIds);
            ValidateSelectedIds("assumption_projection", "assumption_id", assumptionIds);
            foreach (string reviewId in reviewIds)
            {
                if (ScalarLong(
                        "SELECT COUNT(*) FROM review_events WHERE event_id=$id AND run_id=$run;",
                        null,
                        ("$id", reviewId), ("$run", request.RunId)) != 1)
                {
                    throw new ArgumentException("An export review-event selection belongs to another run.");
                }
            }
            ResultItemPersistenceRecord[] selectedResults = itemIds
                .Select(item => GetResultItem(request.RunId, item)).ToArray();
            AssumptionStatePersistenceRecord[] selectedAssumptions = assumptionIds
                .Select(GetAssumptionCore).ToArray();
            ResultOverviewPersistenceRecord overview = GetResultOverview(request.RunId);
            string[] provenance = selectedResults.Select(item => item.SourcePayloadId)
                .Concat(reviewIds)
                .Concat(selectedAssumptions.Select(item => item.AnalysisContextId))
                .Concat([
                    sourceRun.Binding.InstallationSnapshotId,
                    sourceRun.Binding.AnalysisContextId,
                    sourceRun.Binding.EffectiveScanConfigurationId,
                    sourceRun.Binding.ResolvedInputManifestId,
                ])
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string exportId = StableId("structured-export", request.IdempotencyKey, request.RunId, requestSha);
            var manifest = new
            {
                schema_identity = "infinium.export.structured-results/1.0.0",
                generator_identity = "infinium.application.structured-export/1.0.0",
                export_id = exportId,
                source_run_id = request.RunId,
                source_run = new
                {
                    run_id = request.RunId,
                    lifecycle_state = sourceRun.State.ToString(),
                    lifecycle_generation = sourceRun.Generation,
                    installation_snapshot_id = sourceRun.Binding.InstallationSnapshotId,
                    analysis_context_id = sourceRun.Binding.AnalysisContextId,
                    effective_scan_configuration_id = sourceRun.Binding.EffectiveScanConfigurationId,
                    resolved_input_manifest_id = sourceRun.Binding.ResolvedInputManifestId,
                    readiness_boundary = overview.Readiness,
                    no_safety_guarantee = overview.NoSafetyGuarantee,
                },
                selected_result_item_ids = itemIds,
                selected_review_event_ids = reviewIds,
                selected_assumption_ids = assumptionIds,
                selected_results = selectedResults.Select(ResultExportObject).ToArray(),
                selected_review_events = reviewIds.Select(ReviewExportObject).ToArray(),
                selected_assumptions = selectedAssumptions.Select(AssumptionExportObject).ToArray(),
                filters = ClosedTextSet(request.Filters, 16),
                sharing_class = request.SharingClass,
                declared_omissions = ClosedTextSet(request.DeclaredOmissions, 32),
                privacy_decisions = ClosedTextSet(request.PrivacyDecisions, 32),
                source_policy_decisions = ClosedTextSet(request.SourcePolicyDecisions, 32),
                provenance_ids = provenance,
                created_at = ToText(now),
            };
            byte[] artifact = JsonSerializer.SerializeToUtf8Bytes(manifest, StructuredExportJsonOptions);
            if (artifact.Length is 0 or > MaximumExportBytes)
            {
                throw new InvalidOperationException("The structured export exceeds its local-private bound.");
            }
            string manifestSha = Sha256(JsonSerializer.Serialize(manifest));
            string artifactSha = Convert.ToHexStringLower(SHA256.HashData(artifact));
            string relativePath = exportId + ".json";
            Paths.WriteAllBytesAtomic(ProductWriteClass.Export, relativePath, artifact);
            try
            {
                using SqliteTransaction transaction = BeginTransaction();
                Execute(
                    """
                    INSERT INTO structured_exports(
                        export_id,idempotency_key,request_sha256,run_id,sharing_class,schema_identity,
                        generator_identity,selection_manifest_json,selection_manifest_sha256,
                        artifact_relative_path,artifact_sha256,artifact_bytes,created_at)
                    VALUES ($export,$key,$request_sha,$run,$sharing,$schema,$generator,$manifest,
                        $manifest_sha,$path,$artifact_sha,$bytes,$now);
                    """,
                    transaction,
                    ("$export", exportId), ("$key", request.IdempotencyKey), ("$request_sha", requestSha),
                    ("$run", request.RunId), ("$sharing", request.SharingClass),
                    ("$schema", "infinium.export.structured-results/1.0.0"),
                    ("$generator", "infinium.application.structured-export/1.0.0"),
                    ("$manifest", JsonSerializer.Serialize(manifest)), ("$manifest_sha", manifestSha),
                    ("$path", relativePath), ("$artifact_sha", artifactSha),
                    ("$bytes", artifact.Length), ("$now", ToText(now)));
                string createdEventId = StableId("structured-export-event", exportId, "created");
                Execute(
                    """
                    INSERT INTO structured_export_events(
                        event_id,idempotency_key,request_sha256,export_id,revision,event_kind,created_at)
                    VALUES ($event,$key,$sha,$export,1,'created',$now);
                    INSERT INTO structured_export_projection(
                        export_id,revision,state,last_event_id,deleted_at,updated_at)
                    VALUES ($export,1,'active',$event,NULL,$now);
                    """,
                    transaction,
                    ("$event", createdEventId), ("$key", "created-" + exportId),
                    ("$sha", requestSha), ("$export", exportId), ("$now", ToText(now)));
                transaction.Commit();
            }
            catch
            {
                Paths.DeleteFile(ProductWriteClass.Export, relativePath, missingIsSuccess: true);
                throw;
            }
            return GetStructuredExportCore(exportId);
        }
    }

    public StructuredExportPersistenceRecord GetStructuredExport(string exportId)
    {
        ValidateOpaque(exportId, nameof(exportId));
        lock (gate)
        {
            return GetStructuredExportCore(exportId);
        }
    }

    public StructuredExportDeletionPreviewPersistenceRecord PreviewStructuredExportDeletion(string exportId)
    {
        ValidateOpaque(exportId, nameof(exportId));
        lock (gate)
        {
            StructuredExportPersistenceRecord export = GetStructuredExportCore(exportId, validateArtifact: false);
            string relativePath = ScalarStringOrNull(
                    "SELECT artifact_relative_path FROM structured_exports WHERE export_id=$id;",
                    null,
                    ("$id", exportId))
                ?? throw new KeyNotFoundException("The structured export does not exist.");
            bool present = File.Exists(Paths.ResolveProductPath(ProductWriteClass.Export, relativePath));
            return new(exportId, export.State, present, true);
        }
    }

    public StructuredExportPersistenceRecord DeleteStructuredExport(
        string idempotencyKey,
        string exportId,
        DateTimeOffset now)
    {
        ValidateOpaque(idempotencyKey, nameof(idempotencyKey));
        ValidateOpaque(exportId, nameof(exportId));
        string requestSha = Sha256(JsonSerializer.Serialize(new { export_id = exportId }));
        lock (gate)
        {
            StructuredExportPersistenceRecord current = GetStructuredExportCore(exportId, validateArtifact: false);
            string requestEventKey = "delete-requested-" + idempotencyKey;
            using (SqliteCommand replay = connection.CreateCommand())
            {
                replay.CommandText =
                    "SELECT request_sha256,export_id FROM structured_export_events WHERE idempotency_key=$key;";
                replay.Parameters.AddWithValue("$key", requestEventKey);
                using SqliteDataReader reader = replay.ExecuteReader();
                if (reader.Read()
                    && (!StringComparer.Ordinal.Equals(reader.GetString(0), requestSha)
                        || !StringComparer.Ordinal.Equals(reader.GetString(1), exportId)))
                {
                    throw new InvalidOperationException("A structured-export deletion key cannot be rebound.");
                }
            }

            if (current.State == "active")
            {
                long revision = checked(current.EventRevision + 1);
                string eventId = StableId("structured-export-event", exportId, revision.ToString(CultureInfo.InvariantCulture), requestSha);
                using SqliteTransaction transaction = BeginTransaction();
                Execute(
                    """
                    INSERT INTO structured_export_events(
                        event_id,idempotency_key,request_sha256,export_id,revision,event_kind,created_at)
                    VALUES ($event,$key,$sha,$export,$revision,'deletion-requested',$now);
                    """,
                    transaction,
                    ("$event", eventId), ("$key", requestEventKey), ("$sha", requestSha),
                    ("$export", exportId), ("$revision", revision), ("$now", ToText(now)));
                int changed = Execute(
                    """
                    UPDATE structured_export_projection
                    SET revision=$revision,state='deletion-pending',last_event_id=$event,updated_at=$now
                    WHERE export_id=$export AND revision=$previous AND state='active';
                    """,
                    transaction,
                    ("$event", eventId),
                    ("$export", exportId), ("$revision", revision),
                    ("$previous", current.EventRevision), ("$now", ToText(now)));
                if (changed != 1)
                {
                    throw new InvalidOperationException("The structured-export deletion projection changed concurrently.");
                }
                transaction.Commit();
            }
            else if (current.State == "deleted")
            {
                return current;
            }

            CompleteStructuredExportDeletion(exportId, requestSha, idempotencyKey, now);
            return GetStructuredExportCore(exportId);
        }
    }

    public DeletionPreviewPersistenceRecord PreviewResultDeletion(string sourceId)
    {
        ValidateOpaque(sourceId, nameof(sourceId));
        lock (gate)
        {
            string[] review = ReadIds(
                "SELECT event_id FROM review_events WHERE subject_occurrence_id=$id ORDER BY revision;",
                sourceId);
            string[] exports = ReadIds(
                """
                SELECT DISTINCT export_id FROM structured_exports export
                WHERE EXISTS (SELECT 1 FROM json_each(export.selection_manifest_json,'$.selected_result_item_ids') item
                              WHERE item.value=$id)
                   OR EXISTS (SELECT 1 FROM json_each(export.selection_manifest_json,'$.selected_review_event_ids') review
                              WHERE review.value=$id)
                   OR EXISTS (SELECT 1 FROM json_each(export.selection_manifest_json,'$.selected_assumption_ids') assumption
                              WHERE assumption.value=$id)
                ORDER BY export_id;
                """,
                sourceId);
            List<string> effects = ["Historical analysis and source provenance must not be rewritten."];
            if (review.Length > 0)
            {
                effects.Add("Review history and current review projection depend on the selected occurrence.");
            }
            if (exports.Length > 0)
            {
                effects.Add("Independently retained local-private exports contain the selected identity.");
            }
            return new(sourceId, review, exports, effects, review.Length > 0 || exports.Length > 0);
        }
    }

    private ReviewStatePersistenceRecord GetReviewStateCore(
        string runId,
        string subjectKind,
        string subjectOccurrenceId)
    {
        long revision = 0;
        string disposition = "unreviewed";
        bool suppressed = false;
        string annotation = string.Empty;
        using (SqliteCommand current = connection.CreateCommand())
        {
            current.CommandText =
                """
                SELECT revision,disposition,suppressed,annotation FROM review_projection
                WHERE run_id=$run AND subject_kind=$kind AND subject_occurrence_id=$subject;
                """;
            current.Parameters.AddWithValue("$run", runId);
            current.Parameters.AddWithValue("$kind", subjectKind);
            current.Parameters.AddWithValue("$subject", subjectOccurrenceId);
            using SqliteDataReader reader = current.ExecuteReader();
            if (reader.Read())
            {
                revision = reader.GetInt64(0);
                disposition = reader.GetString(1);
                suppressed = reader.GetInt64(2) == 1;
                annotation = reader.GetString(3);
            }
        }
        List<ReviewEventPersistenceRecord> history = [];
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT event_id,revision,event_kind,disposition,suppressed,annotation,source_event_id,
                       continuity_assessment_id,created_at
                FROM review_events WHERE run_id=$run AND subject_kind=$kind AND subject_occurrence_id=$subject
                ORDER BY revision DESC LIMIT 101;
                """;
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$kind", subjectKind);
            command.Parameters.AddWithValue("$subject", subjectOccurrenceId);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                history.Add(new(
                    reader.GetString(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3),
                    reader.GetInt64(4) == 1, reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7), ParseRoundTrip(reader.GetString(8))));
            }
        }
        bool historyTruncated = history.Count > 100;
        if (historyTruncated)
        {
            history.RemoveAt(history.Count - 1);
        }
        return new(runId, subjectKind, subjectOccurrenceId, revision, disposition, suppressed, annotation, history, historyTruncated);
    }

    private void ValidateReviewSubject(string runId, string subjectKind, string subjectOccurrenceId)
    {
        lock (gate)
        {
            ValidateReviewSubjectCore(runId, subjectKind, subjectOccurrenceId);
        }
    }

    private void ValidateReviewSubjectCore(string runId, string subjectKind, string subjectOccurrenceId)
    {
        if (subjectKind is not ("finding" or "case"))
        {
            throw new ArgumentException("Review state supports finding and case occurrences only.");
        }
        string table = subjectKind == "finding" ? "finding_occurrences" : "case_occurrences";
        string column = subjectKind == "finding" ? "finding_occurrence_id" : "case_occurrence_id";
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE run_id=$run AND {column}=$subject;";
        command.Parameters.AddWithValue("$run", runId);
        command.Parameters.AddWithValue("$subject", subjectOccurrenceId);
        if (Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new KeyNotFoundException("The review subject is not an exact occurrence in the requested run.");
        }
    }

    private (string Disposition, bool Suppressed, string Annotation) ValidateReviewCarryover(
        ReviewMutationPersistenceRequest request,
        SqliteTransaction transaction)
    {
        if (string.IsNullOrWhiteSpace(request.SourceEventId)
            || string.IsNullOrWhiteSpace(request.ContinuityAssessmentId))
        {
            throw new InvalidOperationException(
                "Review-state carryover requires exact retained causal, applicability, dependency, and producer continuity proof.");
        }
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT source.disposition,source.suppressed,source.annotation
            FROM review_events source
            JOIN reconciliation_assessments continuity
              ON continuity.reconciliation_assessment_id=$continuity
            WHERE source.event_id=$source
              AND source.subject_kind=$kind
              AND continuity.predecessor_occurrence_id=source.subject_occurrence_id
              AND continuity.successor_occurrence_id=$target
              AND continuity.outcome='exact-continuation'
              AND continuity.causal_gate='proven-equivalent'
              AND continuity.applicability_gate='proven-equivalent'
              AND continuity.dependency_gate='proven-equivalent'
              AND continuity.producer_compatibility_gate='proven-equivalent';
            """;
        command.Parameters.AddWithValue("$source", request.SourceEventId);
        command.Parameters.AddWithValue("$continuity", request.ContinuityAssessmentId);
        command.Parameters.AddWithValue("$target", request.SubjectOccurrenceId);
        command.Parameters.AddWithValue("$kind", request.SubjectKind);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException(
                "Review-state carryover requires exact retained causal, applicability, dependency, and producer continuity proof.");
        }
        (string Disposition, bool Suppressed, string Annotation) source =
            (reader.GetString(0), reader.GetInt64(1) == 1, reader.GetString(2));
        if (!StringComparer.Ordinal.Equals(request.Disposition, source.Disposition)
            || request.Suppressed != source.Suppressed
            || !StringComparer.Ordinal.Equals(request.Annotation, source.Annotation))
        {
            throw new InvalidOperationException(
                "Carryover must reproduce the exact retained source review state without substitution.");
        }
        return source;
    }

    private static void ValidateReviewMutation(ReviewMutationPersistenceRequest request)
    {
        ValidateOpaque(request.IdempotencyKey, nameof(request.IdempotencyKey));
        if (request.ExpectedRevision < 0
            || request.EventKind is not ("disposition" or "suppression" or "annotation" or "remove-annotation" or "carryover")
            || request.Disposition is not ("unreviewed" or "investigating" or "action-required" or "resolved"
                or "accepted-as-is" or "not-applicable" or "false-positive")
            || Encoding.UTF8.GetByteCount(request.Annotation) > MaximumReviewTextBytes
            || (request.EventKind == "carryover"
                ? string.IsNullOrWhiteSpace(request.SourceEventId)
                    || string.IsNullOrWhiteSpace(request.ContinuityAssessmentId)
                : request.SourceEventId is not null || request.ContinuityAssessmentId is not null))
        {
            throw new ArgumentException("The review event is malformed or exceeds its closed bounds.");
        }
    }

    private static void ValidateAssumptionMutation(AssumptionMutationPersistenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOpaque(request.IdempotencyKey, nameof(request.IdempotencyKey));
        ValidateOpaque(request.AssumptionId, nameof(request.AssumptionId));
        ValidateOpaque(request.ProfileId, nameof(request.ProfileId));
        if (request.ExpectedRevision < 0
            || request.EventKind is not ("create" or "edit" or "confirm" or "remove" or "revalidate")
            || request.Origin is not ("inferred" or "user-provided")
            || request.Confirmation is not ("unconfirmed" or "user-confirmed")
            || string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Length > 256
            || string.IsNullOrWhiteSpace(request.Value) || Encoding.UTF8.GetByteCount(request.Value) > MaximumReviewTextBytes
            || string.IsNullOrWhiteSpace(request.Scope) || request.Scope.Length > 256
            || request.DependencyIds.Count > 100
            || request.DependencyIds.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 160))
        {
            throw new ArgumentException("The assumption event is malformed or exceeds its closed bounds.");
        }
    }

    private AssumptionStatePersistenceRecord GetAssumptionCore(string assumptionId) =>
        FindAssumptionCore(assumptionId)
        ?? throw new KeyNotFoundException("The assumption does not exist.");

    private AssumptionStatePersistenceRecord? FindAssumptionCore(string assumptionId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT assumption_id,profile_id,revision,origin,confirmation,subject,value,scope,
                   dependency_ids_json,effective,analysis_context_id,updated_at
            FROM assumption_projection WHERE assumption_id=$id;
            """;
        command.Parameters.AddWithValue("$id", assumptionId);
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadAssumption(reader) : null;
    }

    private static AssumptionStatePersistenceRecord ReadAssumption(SqliteDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetString(3), reader.GetString(4),
        reader.GetString(5), reader.GetString(6), reader.GetString(7), DeserializeStrings(reader.GetString(8)),
        reader.GetInt64(9) == 1, reader.GetString(10), ParseRoundTrip(reader.GetString(11)));

    private void ValidateTargetedSource(
        string runId,
        string? findingOccurrenceId,
        string? caseOccurrenceId)
    {
        lock (gate)
        {
            if (findingOccurrenceId is not null)
            {
                ValidateReviewSubjectCore(runId, "finding", findingOccurrenceId);
            }
            if (caseOccurrenceId is not null)
            {
                ValidateReviewSubjectCore(runId, "case", caseOccurrenceId);
            }
        }
    }

    private TargetedVerificationPersistenceRecord GetTargetedVerificationCore(string verificationId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT verification_id,source_run_id,successor_run_id,source_finding_occurrence_id,
                   source_case_occurrence_id,exact_scope_ids_json,readiness_boundary,state,created_at
            FROM targeted_verifications WHERE verification_id=$id;
            """;
        command.Parameters.AddWithValue("$id", verificationId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException("The targeted verification does not exist.");
        }
        return new(
            reader.GetString(0), reader.GetString(1), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
            DeserializeStrings(reader.GetString(5)), reader.GetString(6), reader.GetString(7),
            ParseRoundTrip(reader.GetString(8)));
    }

    private static void ValidateExportRequest(StructuredExportPersistenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateOpaque(request.IdempotencyKey, nameof(request.IdempotencyKey));
        if (request.SharingClass != "LocalPrivateExport"
            || request.SelectedResultItemIds.Count + request.SelectedReviewEventIds.Count
                + request.SelectedAssumptionIds.Count is < 1 or > 100
            || request.Filters.Count > 16
            || request.DeclaredOmissions.Count > 32
            || request.PrivacyDecisions.Count is < 1 or > 32
            || request.SourcePolicyDecisions.Count is < 1 or > 32)
        {
            throw new ArgumentException("The export selection, sharing class, or policy manifest is invalid.");
        }
    }

    private StructuredExportPersistenceRecord GetStructuredExportCore(string exportId, bool validateArtifact = true)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT export.export_id,export.run_id,export.sharing_class,export.schema_identity,export.generator_identity,
                   selection_manifest_json,selection_manifest_sha256,artifact_sha256,
                   artifact_bytes,artifact_relative_path,export.created_at,
                   projection.state,projection.deleted_at,projection.revision
            FROM structured_exports export
            JOIN structured_export_projection projection ON projection.export_id=export.export_id
            WHERE export.export_id=$id;
            """;
        command.Parameters.AddWithValue("$id", exportId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException("The structured export does not exist.");
        }
        using JsonDocument manifest = JsonDocument.Parse(reader.GetString(5));
        JsonElement root = manifest.RootElement;
        if (!StringComparer.Ordinal.Equals(Sha256(reader.GetString(5)), reader.GetString(6)))
        {
            throw new InvalidDataException("The retained structured export manifest failed integrity validation.");
        }
        if (validateArtifact && reader.GetString(11) == "active")
        {
            using FileStream artifact = Paths.OpenReadFile(ProductWriteClass.Export, reader.GetString(9));
            if (artifact.Length != reader.GetInt64(8)
                || !StringComparer.Ordinal.Equals(HashStream(artifact), reader.GetString(7)))
            {
                throw new InvalidDataException("The retained structured export artifact failed integrity validation.");
            }
        }
        List<StructuredExportEventPersistenceRecord> history = [];
        using (SqliteCommand events = connection.CreateCommand())
        {
            events.CommandText =
                """
                SELECT event_id,revision,event_kind,request_sha256,created_at
                FROM structured_export_events WHERE export_id=$id ORDER BY revision DESC LIMIT 101;
                """;
            events.Parameters.AddWithValue("$id", exportId);
            using SqliteDataReader eventReader = events.ExecuteReader();
            while (eventReader.Read())
            {
                history.Add(new(
                    eventReader.GetString(0), eventReader.GetInt64(1), eventReader.GetString(2),
                    eventReader.GetString(3), ParseRoundTrip(eventReader.GetString(4))));
            }
        }
        bool historyTruncated = history.Count > 100;
        if (historyTruncated)
        {
            history.RemoveAt(history.Count - 1);
        }
        return new(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
            reader.GetString(6), reader.GetString(7), reader.GetInt64(8),
            ExportStrings(root, "selected_result_item_ids"), ExportStrings(root, "selected_review_event_ids"),
            ExportStrings(root, "selected_assumption_ids"), ExportStrings(root, "filters"),
            ExportStrings(root, "declared_omissions"), ExportStrings(root, "privacy_decisions"),
            ExportStrings(root, "source_policy_decisions"), ExportStrings(root, "provenance_ids"),
            ParseRoundTrip(reader.GetString(10)), reader.GetString(11),
            reader.IsDBNull(12) ? null : ParseRoundTrip(reader.GetString(12)), reader.GetInt64(13),
            history, historyTruncated);
    }

    private void CompleteStructuredExportDeletion(
        string exportId,
        string requestSha,
        string idempotencyKey,
        DateTimeOffset now)
    {
        using SqliteCommand location = connection.CreateCommand();
        location.CommandText = "SELECT artifact_relative_path FROM structured_exports WHERE export_id=$id;";
        location.Parameters.AddWithValue("$id", exportId);
        string relativePath = (string)(location.ExecuteScalar()
            ?? throw new KeyNotFoundException("The structured export does not exist."));
        Paths.DeleteFile(ProductWriteClass.Export, relativePath, missingIsSuccess: true);

        using SqliteTransaction transaction = BeginTransaction();
        long revision = ScalarLong(
            "SELECT revision FROM structured_export_projection WHERE export_id=$id;",
            transaction,
            ("$id", exportId));
        string state = ScalarStringOrNull(
                "SELECT state FROM structured_export_projection WHERE export_id=$id;",
                transaction,
                ("$id", exportId))
            ?? throw new KeyNotFoundException("The structured export does not exist.");
        if (state == "deleted")
        {
            transaction.Commit();
            return;
        }
        if (state != "deletion-pending")
        {
            throw new InvalidOperationException("The structured export is not pending deletion.");
        }
        long nextRevision = checked(revision + 1);
        string eventId = StableId("structured-export-event", exportId, nextRevision.ToString(CultureInfo.InvariantCulture), requestSha);
        Execute(
            """
            INSERT INTO structured_export_events(
                event_id,idempotency_key,request_sha256,export_id,revision,event_kind,created_at)
            VALUES ($event,$key,$sha,$export,$revision,'deleted',$now);
            """,
            transaction,
            ("$event", eventId), ("$key", "delete-completed-" + idempotencyKey),
            ("$sha", requestSha), ("$export", exportId), ("$revision", nextRevision),
            ("$now", ToText(now)));
        int changed = Execute(
            """
            UPDATE structured_export_projection
            SET revision=$revision,state='deleted',last_event_id=$event,deleted_at=$now,updated_at=$now
            WHERE export_id=$export AND revision=$previous AND state='deletion-pending';
            """,
            transaction,
            ("$event", eventId), ("$export", exportId), ("$revision", nextRevision),
            ("$previous", revision), ("$now", ToText(now)));
        if (changed != 1)
        {
            throw new InvalidOperationException("The structured-export deletion projection changed concurrently.");
        }
        transaction.Commit();
    }

    private static JsonObject ResultExportObject(ResultItemPersistenceRecord item) => new()
    {
        ["run_id"] = item.RunId,
        ["item_id"] = item.ItemId,
        ["logical_id"] = item.LogicalId,
        ["case_occurrence_id"] = item.CaseOccurrenceId,
        ["kind"] = item.Kind,
        ["inert_summary"] = item.Summary,
        ["severity"] = item.Severity,
        ["confidence"] = item.Confidence,
        ["analyzer_id"] = item.AnalyzerId,
        ["analyzer_version"] = item.AnalyzerVersion,
        ["subject_ids"] = JsonStringArray(item.SubjectIds),
        ["evidence_ids"] = JsonStringArray(item.EvidenceIds),
        ["source_payload_id"] = item.SourcePayloadId,
        ["source_payload_sha256"] = item.SourcePayloadSha256,
    };

    private JsonObject ReviewExportObject(string eventId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT event_id,run_id,subject_kind,subject_occurrence_id,revision,event_kind,
                   disposition,suppressed,annotation,source_event_id,continuity_assessment_id,created_at
            FROM review_events WHERE event_id=$id;
            """;
        command.Parameters.AddWithValue("$id", eventId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException("An exact export review-event selection does not exist.");
        }
        return new JsonObject
        {
            ["event_id"] = reader.GetString(0),
            ["run_id"] = reader.GetString(1),
            ["subject_kind"] = reader.GetString(2),
            ["subject_occurrence_id"] = reader.GetString(3),
            ["revision"] = reader.GetInt64(4),
            ["event_kind"] = reader.GetString(5),
            ["disposition"] = reader.GetString(6),
            ["suppressed"] = reader.GetInt64(7) == 1,
            ["inert_annotation"] = reader.GetString(8),
            ["source_event_id"] = reader.IsDBNull(9) ? null : reader.GetString(9),
            ["continuity_assessment_id"] = reader.IsDBNull(10) ? null : reader.GetString(10),
            ["created_at"] = reader.GetString(11),
        };
    }

    private static JsonObject AssumptionExportObject(AssumptionStatePersistenceRecord assumption) => new()
    {
        ["assumption_id"] = assumption.AssumptionId,
        ["profile_id"] = assumption.ProfileId,
        ["revision"] = assumption.Revision,
        ["origin"] = assumption.Origin,
        ["confirmation"] = assumption.Confirmation,
        ["subject"] = assumption.Subject,
        ["inert_value"] = assumption.Value,
        ["scope"] = assumption.Scope,
        ["dependency_ids"] = JsonStringArray(assumption.DependencyIds),
        ["effective"] = assumption.Effective,
        ["analysis_context_id"] = assumption.AnalysisContextId,
        ["created_at"] = ToText(assumption.CreatedAt),
    };

    private static JsonArray JsonStringArray(IEnumerable<string> values)
    {
        JsonArray result = [];
        foreach (string value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private void ValidateSelectedIds(string table, string column, IReadOnlyList<string> ids)
    {
        foreach (string id in ids)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE {column}=$id;";
            command.Parameters.AddWithValue("$id", id);
            if (Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new KeyNotFoundException("An exact export selection identity does not exist.");
            }
        }
    }

    private string[] ReadInertStrings(string sql, string runId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$run", runId);
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> values = [];
        while (reader.Read())
        {
            values.Add(BoundedText(reader.GetString(0), 4096));
        }
        return values.ToArray();
    }

    private string[] ReadIds(string sql, string value, string parameter = "$id")
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(parameter, value);
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> values = [];
        while (reader.Read())
        {
            values.Add(reader.GetString(0));
        }
        return values.ToArray();
    }

    private static ResultItemPersistenceRecord ReadResultItem(SqliteDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8),
        reader.GetString(9), DeserializeStrings(reader.GetString(10)), DeserializeStrings(reader.GetString(11)),
        reader.GetString(12), reader.GetString(13));

    private static FindingReportSummaryPersistenceRecord ReadFindingReportSummary(SqliteDataReader reader) => new(
        reader.GetString(0), reader.GetString(1), reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8),
        reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12));

    private static string[] Subjects(IdentityEnvelopeContract envelope) =>
        envelope.ParticipantsAndRoles.SelectMany(item => new[] { item.Key, item.Value })
            .Append(envelope.AffectedLocus)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] DeserializeStrings(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static string[] ExportStrings(JsonElement root, string property) =>
        root.GetProperty(property).EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static DateTimeOffset ParseRoundTrip(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static int SeverityRank(string? severity) => severity switch
    {
        "blocker" => 0,
        "major" => 1,
        "moderate" => 2,
        "minor" => 3,
        _ => 4,
    };

    private static string[] ClosedSelection(IReadOnlyList<string> values, int maximum, string label)
    {
        if (values.Count > maximum || values.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 160))
        {
            throw new ArgumentException($"The {label} selection exceeds its closed bound.");
        }
        return values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static string[] ClosedTextSet(IReadOnlyList<string> values, int maximum)
    {
        if (values.Count > maximum || values.Any(item =>
            string.IsNullOrWhiteSpace(item) || Encoding.UTF8.GetByteCount(item) > 4096))
        {
            throw new ArgumentException("An export manifest text set exceeds its closed bound.");
        }
        return values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static string BoundedText(string value, int maximum) =>
        value.Length <= maximum ? value : value[..maximum];

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string StableId(string kind, params string[] parts) =>
        kind + "-" + Sha256(string.Join('\n', parts))[..32];

    private static void ValidateOpaque(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160
            || value.Any(character => char.IsControl(character)))
        {
            throw new ArgumentException("A bounded opaque identity is required.", parameterName);
        }
    }
}
