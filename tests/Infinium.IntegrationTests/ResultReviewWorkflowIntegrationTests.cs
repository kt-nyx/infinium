using System.Diagnostics;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.FindingCases;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DomainLifecycleState = Infinium.Domain.Contracts.LifecycleState;

namespace Infinium.Tests;

[TestClass]
public sealed class ResultReviewWorkflowIntegrationTests
{
    private static readonly string[] ExportDeletionEventKinds = ["deleted", "deletion-requested", "created"];

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    public void ResultExplorationIsDeterministicBoundedInertAndExactlyFocused()
    {
        using CandidateStoreContext context = new();
        FindingCaseAnalysisPhaseResult publication = Publish(context, hostileCause: true);
        ResultOverviewPersistenceRecord firstOverview = context.Store.GetResultOverview("run-candidate");
        ResultOverviewPersistenceRecord secondOverview = context.Store.GetResultOverview("run-candidate");
        Assert.AreEqual(firstOverview.Summary, secondOverview.Summary);
        CollectionAssert.AreEqual(firstOverview.Gaps.ToArray(), secondOverview.Gaps.ToArray());
        Assert.IsTrue(firstOverview.NoSafetyGuarantee);
        Assert.AreEqual("provisional-incomplete", firstOverview.Readiness);
        Assert.AreEqual(2, firstOverview.FindingCount);
        Assert.AreEqual(1, firstOverview.SupportedCaseCount);
        Assert.AreEqual(1, firstOverview.LeadOnlyCaseCount);

        ResultItemPagePersistenceRecord supported = context.Store.ListResultItems(
            "run-candidate", ["supported-case"], string.Empty, "identity", 100, null);
        ResultItemPagePersistenceRecord leads = context.Store.ListResultItems(
            "run-candidate", ["lead-only-case"], string.Empty, "identity", 100, null);
        Assert.HasCount(1, supported.Items);
        Assert.HasCount(1, leads.Items);
        Assert.AreNotEqual(supported.Items[0].ItemId, leads.Items[0].ItemId);
        ResultItemPagePersistenceRecord failures = context.Store.ListResultItems(
            "run-candidate", ["failure"], string.Empty, "identity", 100, null);
        Assert.HasCount(1, failures.Items);
        StringAssert.Contains(failures.Items[0].Summary, "<script>alert('inert')</script>");

        ResultItemPagePersistenceRecord searched = context.Store.ListResultItems(
            "run-candidate", ["supported-case", "lead-only-case", "finding", "failure"], "ALERT('INERT')", "identity", 10, null);
        Assert.HasCount(1, searched.Items);
        CollectionAssert.AreEqual(
            searched.Items.Select(item => item.ItemId).ToArray(),
            context.Store.ListResultItems(
                "run-candidate", ["failure", "finding", "lead-only-case", "supported-case"], "ALERT('INERT')", "identity", 10, null)
                .Items.Select(item => item.ItemId).ToArray());

        string exactSubject = publication.Analysis.Cases.Single(item => item.Kind == CaseOccurrenceKind.Supported)
            .IdentityEnvelope.AffectedLocus;
        IReadOnlyList<ResultItemPersistenceRecord> focused = context.Store.GetFocusedResultItems(
            "run-candidate", exactSubject, 100);
        Assert.IsNotEmpty(focused);
        Assert.IsTrue(focused.All(item => item.SubjectIds.Contains(exactSubject, StringComparer.Ordinal)));
        Assert.IsEmpty(context.Store.GetFocusedResultItems("run-candidate", exactSubject + "-lookalike", 100));

        ResultItemPersistenceRecord retained = supported.Items[0];
        byte[] before = context.Store.ReadFindingCasePayload(retained.SourcePayloadId);
        ResultItemPersistenceRecord roundTrip = context.Store.GetResultItem(retained.RunId, retained.ItemId);
        byte[] after = context.Store.ReadFindingCasePayload(roundTrip.SourcePayloadId);
        CollectionAssert.AreEqual(before, after);
        Assert.AreEqual(retained.SourcePayloadSha256, roundTrip.SourcePayloadSha256);
        Assert.AreEqual(publication.Receipt.StoredPayloadId, retained.SourcePayloadId);
        FindingReportPagePersistenceRecord reports = context.Store.ListFindingReports(
            "run-candidate",
            ["supported-finding", "resolved-negative", "abstention", "failure", "limited", "coverage-gap"],
            string.Empty,
            "identity",
            100,
            null,
            null);
        Assert.IsNotEmpty(reports.Items);
        Assert.IsTrue(reports.Items.Any(item => item.State == "supported-finding"));
        Assert.IsTrue(reports.Items.Any(item => item.State == "limited"));
        Assert.IsTrue(reports.Items.Any(item => item.State == "failure"));
        FindingReportSummaryPersistenceRecord hostileReport = reports.Items.Single(item => item.State == "failure");
        StringAssert.Contains(hostileReport.Conclusion, "<script>alert('inert')</script>");
        FindingReportDetailPersistenceRecord reportDetail = context.Store.GetFindingReport(
            "run-candidate", hostileReport.ReportId);
        FindingReportDocument reportRoundTrip = Infinium.Application.Serialization.FindingReportJsonCodec.Deserialize(
            reportDetail.ReportPayload);
        Assert.AreEqual(hostileReport.ReportId, reportRoundTrip.ReportId.Value);
        Assert.AreEqual(publication.Analysis.PayloadId.Value, reportRoundTrip.Provenance.SourcePayloadId.Value);
        Assert.AreEqual(publication.Analysis.InputId.Value, reportRoundTrip.Provenance.SourceAssignmentId.Value);
        StringAssert.Contains(reportRoundTrip.Failures.Single(), "<script>alert('inert')</script>");
        Assert.ThrowsExactly<KeyNotFoundException>(() => context.Store.ListResultItems(
            "run-does-not-exist", ["finding"], string.Empty, "identity", 10, null));

        DateTimeOffset now = DateTimeOffset.UtcNow;
        ResultPageCursor cursor = new("run-candidate", "projection-a", "query-a", "identity", 20,
            retained.ItemId, retained.Severity, now.AddMinutes(5));
        Assert.AreEqual(CursorDisposition.Unspecified, ApplicationGrpcService.ValidateResultCursorBinding(
            cursor, "run-candidate", "projection-a", "query-a", "identity", 20, now));
        Assert.AreEqual(CursorDisposition.ProjectionInvalidated, ApplicationGrpcService.ValidateResultCursorBinding(
            cursor, "run-candidate", "projection-b", "query-a", "identity", 20, now));
        Assert.AreEqual(CursorDisposition.QueryMismatch, ApplicationGrpcService.ValidateResultCursorBinding(
            cursor, "run-candidate", "projection-a", "query-b", "identity", 20, now));
        Assert.AreEqual(CursorDisposition.SortMismatch, ApplicationGrpcService.ValidateResultCursorBinding(
            cursor, "run-candidate", "projection-a", "query-a", "severity", 20, now));
        Assert.AreEqual(CursorDisposition.Expired, ApplicationGrpcService.ValidateResultCursorBinding(
            cursor with { ExpiresAt = now.AddSeconds(-1) }, "run-candidate", "projection-a", "query-a", "identity", 20, now));
        FindingReportSummaryPersistenceRecord reportPageAnchor = reports.Items[0];
        FindingReportPageCursor reportCursor = new(
            "run-candidate", "report-projection-a", "report-query-a", "identity", 20,
            reportPageAnchor.ReportId, reportPageAnchor.State, now.AddMinutes(5));
        Assert.AreEqual(CursorDisposition.Unspecified, ApplicationGrpcService.ValidateFindingReportCursorBinding(
            reportCursor, "run-candidate", "report-projection-a", "report-query-a", "identity", 20, now));
        Assert.AreEqual(CursorDisposition.ProjectionInvalidated,
            ApplicationGrpcService.ValidateFindingReportCursorBinding(
                reportCursor, "run-candidate", "report-projection-b", "report-query-a", "identity", 20, now));
        Assert.AreEqual(CursorDisposition.QueryMismatch, ApplicationGrpcService.ValidateFindingReportCursorBinding(
            reportCursor, "run-candidate", "report-projection-a", "report-query-b", "identity", 20, now));
        Assert.AreEqual(CursorDisposition.SortMismatch, ApplicationGrpcService.ValidateFindingReportCursorBinding(
            reportCursor, "run-candidate", "report-projection-a", "report-query-a", "state", 20, now));
        Assert.AreEqual(CursorDisposition.ScopeMismatch, ApplicationGrpcService.ValidateFindingReportCursorBinding(
            reportCursor, "run-other", "report-projection-a", "report-query-a", "identity", 20, now));
        Assert.AreEqual(CursorDisposition.Expired, ApplicationGrpcService.ValidateFindingReportCursorBinding(
            reportCursor with { ExpiresAt = now.AddSeconds(-1) },
            "run-candidate", "report-projection-a", "report-query-a", "identity", 20, now));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Evaluation")]
    public void HundredThousandSummaryProjectionKeepsQueryAndMessageBounded()
    {
        using CandidateStoreContext context = new();
        _ = Publish(context);
        ResultItemPersistenceRecord source = context.Store.ListResultItems(
            "run-candidate", ["finding", "supported-case", "lead-only-case", "abstention"],
            string.Empty, "identity", 100, null).Items[0];
        long existing = CandidatePipelineIntegrationTests.Count(context.Paths.Database, "result_projection_items");
        ResultItemPersistenceRecord[] scale = Enumerable.Range(0, checked((int)(100_000 - existing)))
            .Select(index => source with
            {
                ItemId = $"scale-{index:D6}",
                LogicalId = $"logical-scale-{index:D6}",
                Summary = "Synthetic bounded-query scale summary " + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Severity = (index % 4) switch { 0 => "blocker", 1 => "major", 2 => "moderate", _ => "minor" },
                Kind = "finding",
            })
            .ToArray();
        context.Store.IndexResultProjectionBatch(scale, DateTimeOffset.UtcNow);
        Assert.AreEqual(100_000L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "result_projection_items"));

        Stopwatch timer = Stopwatch.StartNew();
        ResultItemPagePersistenceRecord result = context.Store.ListResultItems(
            "run-candidate", ["finding"], "bounded-query", "severity", 100, null);
        timer.Stop();
        Assert.HasCount(100, result.Items);
        Assert.IsTrue(result.HasMore);
        ResultItemPage message = new()
        {
            HasMore = result.HasMore,
            ProjectionVersion = new ProjectionVersion { Value = "1" },
        };
        message.Items.Add(result.Items.Select(item => new ResultItemSummary
        {
            ItemId = item.ItemId,
            RunId = new RunId { Value = item.RunId },
            Kind = ResultItemKind.Finding,
            LogicalId = item.LogicalId,
            InertSummary = item.Summary,
            Severity = item.Severity,
            Confidence = item.Confidence,
            AnalyzerId = item.AnalyzerId,
            AnalyzerVersion = new Infinium.Contracts.Protobuf.Common.V1.SemanticVersion { Value = item.AnalyzerVersion },
        }));
        int messageBytes = message.CalculateSize();
        string schemaFingerprint;
        using (SqliteConnection database = new($"Data Source={context.Paths.Database};Mode=ReadOnly;Pooling=False"))
        {
            database.Open();
            using SqliteCommand command = database.CreateCommand();
            command.CommandText = "SELECT value FROM store_metadata WHERE key='schema_fingerprint';";
            schemaFingerprint = (string)command.ExecuteScalar()!;
        }
        Assert.IsLessThan(1_048_576, messageBytes);
        Assert.IsLessThan(5_000, timer.ElapsedMilliseconds, "The indexed bounded query exceeded the generous local regression ceiling.");
        TestContext.WriteLine($"result-query-scale summaries=100000 page=100 latency_ms={timer.ElapsedMilliseconds} message_bytes={messageBytes} schema15={schemaFingerprint}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Migration")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Migration")]
    public void PopulatedResultsMigrationReturnsExplicitReportUnavailability()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-populated-results-migration-" + Guid.NewGuid().ToString("N"));
        string databasePath;
        string sourcePayloadId;
        byte[] canonicalBefore;
        long retainedResultCount;
        long findingCasePublicationCount;
        CandidateStoreContext source = new(root, preserveRoot: true);
        try
        {
            FindingCaseAnalysisPhaseResult publication = Publish(source);
            databasePath = source.Paths.Database;
            sourcePayloadId = publication.Receipt.StoredPayloadId;
            canonicalBefore = source.Store.ReadFindingCasePayload(sourcePayloadId);
            retainedResultCount = CandidatePipelineIntegrationTests.Count(databasePath, "result_projection_items");
            findingCasePublicationCount = CandidatePipelineIntegrationTests.Count(databasePath, "finding_case_publications");
            Assert.IsGreaterThan(0L, retainedResultCount);
            Assert.IsGreaterThan(0L, findingCasePublicationCount);
            Assert.IsGreaterThan(0L, CandidatePipelineIntegrationTests.Count(databasePath, "finding_report_publications"));
        }
        finally
        {
            source.Dispose();
        }

        try
        {
            using (SqliteConnection database = new($"Data Source={databasePath};Pooling=False"))
            {
                database.Open();
                using SqliteCommand downgrade = database.CreateCommand();
                downgrade.CommandText = TargetedVerificationMigrationTestSupport.DropSchema16Sql +
                    """
                    DROP TABLE structured_export_projection;
                    DROP TABLE structured_export_events;
                    DROP TABLE finding_report_publications;
                    DELETE FROM migration_history WHERE migration_id=$migration;
                    UPDATE store_metadata SET value='14' WHERE key='schema_version';
                    UPDATE store_metadata SET value='1.13.0' WHERE key='storage_contract_version';
                    UPDATE store_metadata SET value=$fingerprint WHERE key='schema_fingerprint';
                    PRAGMA user_version=14;
                    """;
                downgrade.Parameters.AddWithValue("$migration", ResultsPublicationPersistenceDeclarations.MigrationId);
                downgrade.Parameters.AddWithValue("$fingerprint", ResultsReviewPersistenceDeclarations.SchemaFingerprint);
                downgrade.ExecuteNonQuery();
            }

            Assert.AreEqual(retainedResultCount,
                CandidatePipelineIntegrationTests.Count(databasePath, "result_projection_items"));
            Assert.AreEqual(findingCasePublicationCount,
                CandidatePipelineIntegrationTests.Count(databasePath, "finding_case_publications"));
            using StoragePaths migratedPaths = new(root);
            using AuthoritativeStore migrated = new(migratedPaths);
            CollectionAssert.AreEqual(canonicalBefore, migrated.ReadFindingCasePayload(sourcePayloadId));
            Assert.AreEqual(AuthoritativeStore.CurrentSchemaVersion, migrated.GetSchemaVersion());
            Assert.AreEqual(0L, CandidatePipelineIntegrationTests.Count(databasePath, "finding_report_publications"));
            FindingReportProjectionUnavailableException unavailable = Assert.ThrowsExactly<
                FindingReportProjectionUnavailableException>(() => migrated.ListFindingReports(
                    "run-candidate",
                    ["supported-finding", "resolved-negative", "abstention", "failure", "limited", "coverage-gap"],
                    string.Empty,
                    "identity",
                    100,
                    null,
                    null));
            Assert.AreEqual("run-candidate", unavailable.RunId);
            TestContext.WriteLine(
                $"populated-migration schema={migrated.GetSchemaVersion()} retained_results="
                + $"{retainedResultCount} finding_case_publications={findingCasePublicationCount} reports=0 state=unavailable");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    [TestProperty("Category", "Fault")]
    public void DurableReviewAndExportDeletionPreserveSourcesAcrossFaultsRestartAndRestore()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-result-review-" + Guid.NewGuid().ToString("N"));
        string restoreRoot = Path.Combine(Path.GetTempPath(), "infinium-result-review-restore-" + Guid.NewGuid().ToString("N"));
        BackupArtifact backup;
        byte[] canonicalBefore;
        string findingId;
        string payloadId;
        string reviewEventId;
        string exportId;
        string assumptionProjectionAfterRebuild;
        CandidateStoreContext context = new(root, preserveRoot: true);
        try
        {
            FindingCaseAnalysisPhaseResult publication = Publish(context);
            FindingContract finding = publication.Analysis.Findings[0];
            findingId = finding.FindingOccurrenceId.Value;
            payloadId = publication.Receipt.StoredPayloadId;
            canonicalBefore = context.Store.ReadFindingCasePayload(payloadId);

            ReviewMutationPersistenceRequest firstRequest = new(
                "review-command-a", "run-candidate", "finding", findingId, 0,
                "disposition", "investigating", false, string.Empty, null, null);
            ReviewMutationPersistenceRequest competingRequest = firstRequest with
            {
                IdempotencyKey = "review-command-b",
                Disposition = "action-required",
            };
            Task<ReviewMutationPersistenceResult>[] concurrent =
            [
                Task.Run(() => context.Store.ApplyReviewEvent(firstRequest, DateTimeOffset.UtcNow)),
                Task.Run(() => context.Store.ApplyReviewEvent(competingRequest, DateTimeOffset.UtcNow)),
            ];
            Task.WaitAll(concurrent);
            Assert.AreEqual(1, concurrent.Count(task => task.Result.Conflict));
            Assert.AreEqual(1, concurrent.Count(task => !task.Result.Conflict));
            ReviewStatePersistenceRecord review = context.Store.GetReviewState("run-candidate", "finding", findingId);
            Assert.AreEqual(1, review.Revision);
            reviewEventId = review.History.Single().EventId;

            ReviewMutationPersistenceResult annotation = context.Store.ApplyReviewEvent(
                new("review-command-annotation", "run-candidate", "finding", findingId, 1,
                    "annotation", review.Disposition, review.Suppressed, "<b>inert review text</b>", null, null),
                DateTimeOffset.UtcNow.AddSeconds(1));
            Assert.IsFalse(annotation.Conflict);
            Assert.AreEqual(2, annotation.State.Revision);
            Assert.AreEqual("<b>inert review text</b>", annotation.State.Annotation);
            ReviewMutationPersistenceResult removed = context.Store.ApplyReviewEvent(
                new("review-command-remove", "run-candidate", "finding", findingId, 2,
                    "remove-annotation", annotation.State.Disposition, annotation.State.Suppressed, string.Empty, null, null),
                DateTimeOffset.UtcNow.AddSeconds(2));
            Assert.AreEqual(string.Empty, removed.State.Annotation);
            Assert.HasCount(3, removed.State.History);

            string assumptionsBefore = context.Store.GetAssumptionProjectionIdentity("profile-a");
            AssumptionMutationPersistenceResult inferred = context.Store.ApplyAssumptionEvent(
                new("assumption-create", "assumption-a", "profile-a", 0, "create", "inferred",
                    "unconfirmed", "load-order", "inferred value", "run-candidate", [payloadId]), DateTimeOffset.UtcNow);
            string assumptionsAfterCreate = context.Store.GetAssumptionProjectionIdentity("profile-a");
            Assert.AreNotEqual(assumptionsBefore, assumptionsAfterCreate);
            AssumptionMutationPersistenceResult edited = context.Store.ApplyAssumptionEvent(
                new("assumption-edit", "assumption-a", "profile-a", 1, "edit", "inferred",
                    "unconfirmed", "load-order", "successor value", "run-candidate", [payloadId]), DateTimeOffset.UtcNow.AddSeconds(1));
            Assert.AreNotEqual(inferred.State.AnalysisContextId, edited.State.AnalysisContextId);
            Assert.AreNotEqual(assumptionsAfterCreate, context.Store.GetAssumptionProjectionIdentity("profile-a"));
            Assert.AreEqual("inferred", edited.State.Origin);
            AssumptionMutationPersistenceResult removedAssumption = context.Store.ApplyAssumptionEvent(
                new("assumption-remove", "assumption-a", "profile-a", 2, "remove", "inferred",
                    "unconfirmed", "load-order", "successor value", "run-candidate", [payloadId]), DateTimeOffset.UtcNow.AddSeconds(2));
            Assert.IsFalse(removedAssumption.State.Effective);

            context.Store.SettleLiveAttempts("run-candidate", "phase-c-terminal-source", context.Authority.FencingEpoch);
            RunRecord running = context.Store.GetRun("run-candidate");
            RunRecord terminal = context.Store.Transition(
                "transition-phase-c-terminal", running.RunId, running.Generation, DomainLifecycleState.Completed,
                context.Authority.FencingEpoch, "retained source completed", DateTimeOffset.UtcNow);
            Assert.AreEqual(DomainLifecycleState.Completed, context.Store.GetRun("run-candidate").State);

            StructuredExportPersistenceRecord export = context.Store.CreateStructuredExport(
                new("export-command-a", "run-candidate", [findingId], [reviewEventId], ["assumption-a"],
                    ["kind=finding", "exact-run=run-candidate"], "LocalPrivateExport", ["evidence-content"],
                    ["local-private-only"], ["no-live-source-access", "retained-provenance-only"]), DateTimeOffset.UtcNow);
            exportId = export.ExportId;
            Assert.AreEqual("infinium.export.structured-results/1.0.0", export.SchemaIdentity);
            Assert.AreEqual("LocalPrivateExport", export.SharingClass);
            Assert.Contains(payloadId, export.ProvenanceIds);
            Assert.Contains(context.Binding.InstallationSnapshotId, export.ProvenanceIds);
            Assert.Contains(context.Binding.AnalysisContextId, export.ProvenanceIds);
            Assert.IsGreaterThan(0, export.ArtifactBytes);
            Assert.AreEqual("active", export.State);
            Assert.AreEqual(1, export.EventRevision);
            using (JsonDocument artifact = JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(context.Paths.Exports, exportId + ".json"))))
            {
                JsonElement exportRoot = artifact.RootElement;
                Assert.AreEqual(terminal.Generation,
                    exportRoot.GetProperty("source_run").GetProperty("lifecycle_generation").GetInt64());
                Assert.AreEqual("scope-limited",
                    exportRoot.GetProperty("source_run").GetProperty("readiness_boundary").GetString());
                Assert.AreEqual(findingId,
                    exportRoot.GetProperty("selected_results")[0].GetProperty("item_id").GetString());
                Assert.AreEqual(1L,
                    exportRoot.GetProperty("selected_review_events")[0].GetProperty("revision").GetInt64());
                Assert.AreEqual(3L,
                    exportRoot.GetProperty("selected_assumptions")[0].GetProperty("revision").GetInt64());
                Assert.AreEqual(removedAssumption.State.AnalysisContextId,
                    exportRoot.GetProperty("selected_assumptions")[0].GetProperty("analysis_context_id").GetString());
                Assert.IsFalse(exportRoot.TryGetProperty("artifact_relative_path", out _));
            }
            DeletionPreviewPersistenceRecord preview = context.Store.PreviewResultDeletion(findingId);
            Assert.Contains(reviewEventId, preview.ReviewEventIds);
            Assert.Contains(exportId, preview.ExportIds);
            Assert.IsTrue(preview.RequiresExplicitCascade);
            StructuredExportDeletionPreviewPersistenceRecord exportPreview =
                context.Store.PreviewStructuredExportDeletion(exportId);
            Assert.AreEqual("active", exportPreview.CurrentState);
            Assert.IsTrue(exportPreview.ArtifactPresent);
            Assert.IsTrue(exportPreview.AuditHistoryRetained);

            StructuredExportPersistenceRecord deleted = context.Store.DeleteStructuredExport(
                "export-delete-a", exportId, DateTimeOffset.UtcNow.AddSeconds(3));
            Assert.AreEqual("deleted", deleted.State);
            Assert.AreEqual(3, deleted.EventRevision);
            CollectionAssert.AreEqual(
                ExportDeletionEventKinds,
                deleted.History.Select(item => item.EventKind).ToArray());
            Assert.IsFalse(File.Exists(Path.Combine(context.Paths.Exports, exportId + ".json")));
            Assert.AreEqual(3, context.Store.DeleteStructuredExport(
                "export-delete-a", exportId, DateTimeOffset.UtcNow.AddSeconds(4)).EventRevision);

            StructuredExportPersistenceRecord missing = context.Store.CreateStructuredExport(
                new("export-command-missing", "run-candidate", [findingId], [], [], [],
                    "LocalPrivateExport", ["review-state"], ["local-private-only"], ["retained-only"]),
                DateTimeOffset.UtcNow.AddSeconds(5));
            File.Delete(Path.Combine(context.Paths.Exports, missing.ExportId + ".json"));
            Assert.AreEqual("deleted", context.Store.DeleteStructuredExport(
                "export-delete-missing", missing.ExportId, DateTimeOffset.UtcNow.AddSeconds(6)).State);

            StructuredExportPersistenceRecord tampered = context.Store.CreateStructuredExport(
                new("export-command-tampered", "run-candidate", [findingId], [], [], [],
                    "LocalPrivateExport", ["review-state"], ["local-private-only"], ["retained-only"]),
                DateTimeOffset.UtcNow.AddSeconds(7));
            File.WriteAllText(Path.Combine(context.Paths.Exports, tampered.ExportId + ".json"), "tampered");
            Assert.ThrowsExactly<InvalidDataException>(() => context.Store.GetStructuredExport(tampered.ExportId));
            Assert.AreEqual("deleted", context.Store.DeleteStructuredExport(
                "export-delete-tampered", tampered.ExportId, DateTimeOffset.UtcNow.AddSeconds(8)).State);

            StructuredExportPersistenceRecord interrupted = context.Store.CreateStructuredExport(
                new("export-command-interrupted", "run-candidate", [findingId], [], [], [],
                    "LocalPrivateExport", ["review-state"], ["local-private-only"], ["retained-only"]),
                DateTimeOffset.UtcNow.AddSeconds(9));
            using (SqliteConnection rawDeletion = new($"Data Source={context.Paths.Database};Pooling=False"))
            {
                rawDeletion.Open();
                using SqliteCommand pending = rawDeletion.CreateCommand();
                pending.CommandText =
                    """
                    INSERT INTO structured_export_events(
                        event_id,idempotency_key,request_sha256,export_id,revision,event_kind,created_at)
                    VALUES ($event,$key,$sha,$export,2,'deletion-requested',$now);
                    UPDATE structured_export_projection SET revision=2,state='deletion-pending',
                        last_event_id=$event,updated_at=$now WHERE export_id=$export;
                    """;
                pending.Parameters.AddWithValue("$event", interrupted.ExportId + "-interrupted-delete");
                pending.Parameters.AddWithValue("$key", "delete-requested-interrupted");
                pending.Parameters.AddWithValue("$sha", new string('a', 64));
                pending.Parameters.AddWithValue("$export", interrupted.ExportId);
                pending.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.AddSeconds(10).ToString("O"));
                Assert.AreEqual(2, pending.ExecuteNonQuery());
            }

            using (SqliteConnection raw = new($"Data Source={context.Paths.Database};Pooling=False"))
            {
                raw.Open();
                using SqliteCommand mutate = raw.CreateCommand();
                mutate.CommandText = "UPDATE review_events SET annotation='rewritten' WHERE event_id=$id;";
                mutate.Parameters.AddWithValue("$id", reviewEventId);
                Assert.ThrowsExactly<SqliteException>(() => mutate.ExecuteNonQuery());
            }

            string assumptionProjectionBeforeRebuild = context.Store.GetAssumptionProjectionIdentity("profile-a");
            context.Store.RebuildProjections(DateTimeOffset.UtcNow.AddSeconds(11));
            assumptionProjectionAfterRebuild = context.Store.GetAssumptionProjectionIdentity("profile-a");
            Assert.AreNotEqual(assumptionProjectionBeforeRebuild, assumptionProjectionAfterRebuild);
            Assert.AreEqual(3, context.Store.GetReviewState("run-candidate", "finding", findingId).Revision);
            Assert.AreEqual(3, context.Store.ListAssumptions("profile-a", 10, null).Single().Revision);
            CollectionAssert.AreEqual(canonicalBefore, context.Store.ReadFindingCasePayload(payloadId));
            backup = context.Store.CreateBackup("PhaseCReview", DateTimeOffset.UtcNow.AddSeconds(12));
        }
        finally
        {
            context.Dispose();
        }

        try
        {
            using (StoragePaths restartPaths = new(root))
            using (AuthoritativeStore restarted = new(restartPaths))
            {
                Assert.AreEqual(3, restarted.GetReviewState("run-candidate", "finding", findingId).Revision);
                Assert.AreEqual("deleted", restarted.GetStructuredExport(exportId).State);
                Assert.IsTrue(restarted.GetStructuredExport(exportId).History.Any(item => item.EventKind == "deleted"));
                Assert.AreEqual(assumptionProjectionAfterRebuild,
                    restarted.GetAssumptionProjectionIdentity("profile-a"));
                Assert.IsEmpty(Directory.EnumerateFiles(restartPaths.Exports));
                CollectionAssert.AreEqual(canonicalBefore, restarted.ReadFindingCasePayload(payloadId));
            }
            using StoragePaths restoredPaths = new(restoreRoot);
            AuthoritativeStore.RestoreBackup(backup, restoredPaths);
            using AuthoritativeStore restored = new(restoredPaths);
            Assert.AreEqual(3, restored.GetReviewState("run-candidate", "finding", findingId).Revision);
            Assert.AreEqual(assumptionProjectionAfterRebuild,
                restored.GetAssumptionProjectionIdentity("profile-a"));
            Assert.AreEqual("deleted", restored.GetStructuredExport(exportId).State);
            Assert.IsEmpty(Directory.EnumerateFiles(restoredPaths.Exports));
            CollectionAssert.AreEqual(canonicalBefore, restored.ReadFindingCasePayload(payloadId));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            if (Directory.Exists(restoreRoot))
            {
                Directory.Delete(restoreRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    public void ReviewCarryoverRequiresRetainedFourGateExactContinuityEvidence()
    {
        using CandidateStoreContext context = new();
        FindingCaseAnalysisPhaseResult first = Publish(context);
        FindingContract priorFinding = first.Analysis.Findings[0];
        ReviewMutationPersistenceResult sourceDisposition = context.Store.ApplyReviewEvent(
            new("review-carry-source", "run-candidate", "finding", priorFinding.FindingOccurrenceId.Value,
                0, "disposition", "resolved", false, string.Empty, null, null), DateTimeOffset.UtcNow);
        ReviewMutationPersistenceResult sourceSuppression = context.Store.ApplyReviewEvent(
            new("review-carry-source-suppression", "run-candidate", "finding", priorFinding.FindingOccurrenceId.Value,
                sourceDisposition.State.Revision, "suppression", "resolved", true, string.Empty, null, null),
            DateTimeOffset.UtcNow.AddTicks(1));
        ReviewMutationPersistenceResult source = context.Store.ApplyReviewEvent(
            new("review-carry-source-annotation", "run-candidate", "finding", priorFinding.FindingOccurrenceId.Value,
                sourceSuppression.State.Revision, "annotation", "resolved", true, "retained review", null, null),
            DateTimeOffset.UtcNow.AddMilliseconds(1));

        const string successorRun = "run-review-successor";
        AttemptRecord attempt = context.CreateRunAttempt(successorRun, DateTimeOffset.UtcNow.AddSeconds(2));
        CandidateAnalysisPhaseResult candidates = CandidateAnalysisPhase.Execute(
            context.Store, CandidateRequest(successorRun), attempt, context.Binding, DateTimeOffset.UtcNow.AddSeconds(3));
        FindingCaseInputContract successorInput = FindingCaseIntegrationTests.Input(
            candidates.Pipeline.Analysis,
            first.Analysis.Findings.Select(FindingCaseIntegrationTests.PriorFinding).ToArray(),
            first.Analysis.Cases.Select(FindingCaseIntegrationTests.PriorCase).ToArray());
        FindingCaseAnalysisPhaseResult successor = FindingCaseAnalysisPhase.Execute(
            context.Store, successorInput, attempt, context.Binding, DateTimeOffset.UtcNow.AddSeconds(4));
        OccurrenceReconciliationContract continuity = successor.Analysis.ReconciliationAssessments.Single(value =>
            value.PriorOccurrenceId == priorFinding.FindingOccurrenceId
            && value.Outcome == Infinium.Domain.Contracts.ReconciliationOutcome.ExactContinuation);
        Assert.AreEqual(Infinium.Domain.Contracts.ReconciliationGateState.ProvenEquivalent, continuity.Gates.Causal);
        Assert.AreEqual(Infinium.Domain.Contracts.ReconciliationGateState.ProvenEquivalent, continuity.Gates.Applicability);
        Assert.AreEqual(Infinium.Domain.Contracts.ReconciliationGateState.ProvenEquivalent, continuity.Gates.Dependency);
        Assert.AreEqual(Infinium.Domain.Contracts.ReconciliationGateState.ProvenEquivalent, continuity.Gates.Producer);
        string successorFindingId = continuity.CurrentOccurrenceId!.Value;

        Assert.ThrowsExactly<InvalidOperationException>(() => context.Store.ApplyReviewEvent(
            new("review-carry-rejected", successorRun, "finding", successorFindingId, 0,
                "carryover", "resolved", true, "retained review", source.State.History[0].EventId,
                "continuity-not-retained"), DateTimeOffset.UtcNow.AddSeconds(5)));
        Assert.ThrowsExactly<InvalidOperationException>(() => context.Store.ApplyReviewEvent(
            new("review-carry-substitution-rejected", successorRun, "finding", successorFindingId, 0,
                "carryover", "false-positive", false, "substituted review", source.State.History[0].EventId,
                continuity.AssessmentId.Value), DateTimeOffset.UtcNow.AddSeconds(5)));
        ReviewMutationPersistenceResult carried = context.Store.ApplyReviewEvent(
            new("review-carry-accepted", successorRun, "finding", successorFindingId, 0,
                "carryover", "resolved", true, "retained review", source.State.History[0].EventId,
                continuity.AssessmentId.Value), DateTimeOffset.UtcNow.AddSeconds(6));
        Assert.IsFalse(carried.Conflict);
        Assert.AreEqual("resolved", carried.State.Disposition);
        Assert.IsTrue(carried.State.Suppressed);
        Assert.AreEqual(continuity.AssessmentId.Value, carried.State.History[0].ContinuityAssessmentId);
    }

    private static FindingCaseAnalysisPhaseResult Publish(CandidateStoreContext context, bool hostileCause = false)
    {
        const string hostile = "<script>alert('inert')</script>";
        CandidateAnalysisPhaseResult candidates = CandidateAnalysisPhase.Execute(
            context.Store, CandidateRequest("run-candidate"),
            context.Attempt, context.Binding, DateTimeOffset.UtcNow);
        FindingCaseInputContract input = FindingCaseIntegrationTests.Input(candidates.Pipeline.Analysis);
        if (hostileCause)
        {
            CoverageMemberFactContract member = input.CoverageMemberFacts[0];
            OpaqueId failureId = CandidatePipelineIntegrationTests.Id("hostile-failure");
            input = input with
            {
                CoverageFailureFacts =
                [
                    new CoverageFailureFactContract(failureId, member.AnalyzerId, "hostile-text", hostile, false),
                ],
                CoverageMemberFacts = input.CoverageMemberFacts.Select(value => value == member
                    ? value with
                    {
                        State = CoverageMemberState.Failed,
                        Reason = "bounded failure",
                        MissingCapabilityOrInformation = "retained hostile-text test",
                        FailureId = failureId,
                    }
                    : value).ToArray(),
            };
            input = input with { InputId = FindingCaseIdentity.ComputeInputId(input) };
        }
        return FindingCaseAnalysisPhase.Execute(
            context.Store, input, context.Attempt, context.Binding, DateTimeOffset.UtcNow.AddSeconds(1));
    }

    private static CandidatePipelineRequest CandidateRequest(string runId)
    {
        CausalJoinPopulationMember lead = CandidatePipelineIntegrationTests.Member("lead") with
        {
            ContradictingEvidenceIds = [CandidatePipelineIntegrationTests.Id("contradiction-lead")],
        };
        return CandidatePipelineIntegrationTests.Request(
            [CandidatePipelineIntegrationTests.Member("alpha"), CandidatePipelineIntegrationTests.Member("beta"), lead],
            runId, "population-cases");
    }
}
