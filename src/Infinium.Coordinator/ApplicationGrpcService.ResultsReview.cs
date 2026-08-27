using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Infinium.Application.Serialization;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using ContractFailure = Infinium.Contracts.Protobuf.Common.V1.Failure;
using ProtoFindingReportSort = Infinium.Contracts.Protobuf.Application.V1.FindingReportSort;
using ProtoFindingReportState = Infinium.Contracts.Protobuf.Application.V1.FindingReportState;
using ProtoStructuredExportState = Infinium.Contracts.Protobuf.Application.V1.StructuredExportState;

namespace Infinium.Coordinator;

internal sealed record ResultPageCursor(
    string RunId,
    string ProjectionIdentity,
    string QueryHash,
    string Sort,
    uint PageSize,
    string LastItemId,
    string LastSeverity,
    DateTimeOffset ExpiresAt);

internal sealed record AssumptionPageCursor(
    string ProfileId,
    string ProjectionIdentity,
    uint PageSize,
    string LastAssumptionId,
    DateTimeOffset ExpiresAt);

internal sealed record FindingReportPageCursor(
    string RunId,
    string ProjectionIdentity,
    string QueryHash,
    string Sort,
    uint PageSize,
    string LastReportId,
    string LastState,
    DateTimeOffset ExpiresAt);

public sealed partial class ApplicationGrpcService
{
    public override Task<GetResultOverviewResponse> GetResultOverview(
        GetResultOverviewRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new GetResultOverviewResponse { Failure = contractFailure });
        }
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetResultOverviewResponse { ProjectionInvalidated = ProjectionInvalidated() });
        }
        try
        {
            return Task.FromResult(new GetResultOverviewResponse
            {
                Overview = ToResultOverview(runtime.Store.GetResultOverview(Required(request.RunId?.Value, "run ID"))),
            });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetResultOverviewResponse
            {
                Failure = Failure(FailureCode.NotFound, "The requested result overview does not exist."),
            });
        }
    }

    public override Task<ListResultItemsResponse> ListResultItems(
        ListResultItemsRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new ListResultItemsResponse { Failure = contractFailure });
        }
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(ResultCursorRejected(
                CursorDisposition.ProjectionInvalidated,
                "The result projection was rebuilt."));
        }
        if (request.RequestedPageSize is < 1 or > 100
            || request.Kinds.Count is < 1 or > 6
            || request.SearchText.Length > 160
            || request.Kinds.Any(item => item is ResultItemKind.Unspecified or ResultItemKind.Unknown or ResultItemKind.Unsupported)
            || request.Sort is ResultItemSort.Unspecified or ResultItemSort.Unknown or ResultItemSort.Unsupported)
        {
            return Task.FromResult(new ListResultItemsResponse
            {
                Failure = Failure(FailureCode.InvalidArgument, "The result query exceeds its closed bounds."),
            });
        }

        try
        {
            string runId = Required(request.RunId?.Value, "run ID");
            string[] kinds = request.Kinds.Select(ResultKindToken).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string sort = request.Sort == ResultItemSort.SeverityDescendingIdentityAscending ? "severity" : "identity";
            string queryHash = ResultQueryHash(kinds, request.SearchText, sort);
            string projectionIdentity = runtime.Store.GetResultProjectionIdentity(runId);
            ResultPageCursor? cursor = null;
            if (request.After?.OpaqueValue.Length > 0)
            {
                try
                {
                    cursor = JsonSerializer.Deserialize<ResultPageCursor>(DecodeAuthenticated(request.After.OpaqueValue))
                        ?? throw new InvalidOperationException("The result cursor is malformed.");
                }
                catch (Exception exception) when (exception is InvalidOperationException or JsonException)
                {
                    return Task.FromResult(ResultCursorRejected(CursorDisposition.Malformed, Bounded(exception.Message)));
                }
                CursorDisposition binding = ValidateResultCursorBinding(
                    cursor, runId, projectionIdentity, queryHash, sort, request.RequestedPageSize, DateTimeOffset.UtcNow);
                if (binding != CursorDisposition.Unspecified)
                {
                    return Task.FromResult(ResultCursorRejected(binding, "The result cursor no longer matches the exact closed query."));
                }
            }

            ResultItemPagePersistenceRecord result = runtime.Store.ListResultItems(
                runId, kinds, request.SearchText, sort, checked((int)request.RequestedPageSize),
                cursor?.LastItemId, cursor?.LastSeverity);
            ResultItemPage page = new()
            {
                HasMore = result.HasMore,
                ProjectionVersion = new ProjectionVersion { Value = result.ProjectionVersion },
            };
            page.Items.Add(result.Items.Select(ToResultItemSummary));
            if (result.HasMore && result.Items.Count > 0)
            {
                ResultItemPersistenceRecord last = result.Items[^1];
                page.Next = new PageCursor
                {
                    OpaqueValue = EncodeAuthenticated(JsonSerializer.SerializeToUtf8Bytes(
                        new ResultPageCursor(runId, projectionIdentity, queryHash, sort,
                            request.RequestedPageSize, last.ItemId, last.Severity,
                            DateTimeOffset.UtcNow.AddMinutes(5)))),
                };
            }
            return Task.FromResult(new ListResultItemsResponse { Page = page });
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        {
            return Task.FromResult(new ListResultItemsResponse
            {
                Failure = Failure(
                    exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetResultDetailResponse> GetResultDetail(
        GetResultDetailRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new GetResultDetailResponse { Failure = contractFailure });
        }
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetResultDetailResponse { ProjectionInvalidated = ProjectionInvalidated() });
        }
        try
        {
            string runId = Required(request.RunId?.Value, "run ID");
            ResultItemPersistenceRecord item = runtime.Store.GetResultItem(runId, Required(request.ItemId, "result item ID"));
            if (request.Kind is ResultItemKind.Unspecified or ResultItemKind.Unknown or ResultItemKind.Unsupported
                || !StringComparer.Ordinal.Equals(ResultKindToken(request.Kind), item.Kind))
            {
                throw new ArgumentException("The requested result kind does not match the retained result item.");
            }
            FindingCaseContract source = FindingCaseJsonCodec.Deserialize(runtime.Store.ReadFindingCasePayload(item.SourcePayloadId));
            return Task.FromResult(new GetResultDetailResponse { Detail = BuildResultDetail(item, source) });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or KeyNotFoundException)
        {
            return Task.FromResult(new GetResultDetailResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetEvidenceExpansionResponse> GetEvidenceExpansion(
        GetEvidenceExpansionRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new GetEvidenceExpansionResponse { Failure = contractFailure });
        }
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetEvidenceExpansionResponse { ProjectionInvalidated = ProjectionInvalidated() });
        }
        if (request.RequestedMaximumItems is < 1 or > 100 || request.EvidenceIds.Count > 100)
        {
            return Task.FromResult(new GetEvidenceExpansionResponse
            {
                Failure = Failure(FailureCode.InvalidArgument, "The evidence expansion exceeds its closed bound."),
            });
        }
        try
        {
            string runId = Required(request.RunId?.Value, "run ID");
            ResultItemPersistenceRecord source = runtime.Store.GetResultItem(runId, Required(request.ResultItemId, "result item ID"));
            string[] requested = request.EvidenceIds.Count == 0
                ? source.EvidenceIds.ToArray()
                : request.EvidenceIds.Distinct(StringComparer.Ordinal).ToArray();
            if (requested.Any(id => !source.EvidenceIds.Contains(id, StringComparer.Ordinal)))
            {
                throw new ArgumentException("Evidence expansion is limited to evidence retained by the exact result item.");
            }
            int maximum = checked((int)request.RequestedMaximumItems);
            EvidenceExpansion expansion = new()
            {
                ResultItemId = source.ItemId,
                Truncated = requested.Length > maximum,
                ProjectionVersion = new ProjectionVersion { Value = "1" },
            };
            foreach (string evidenceId in requested.Order(StringComparer.Ordinal).Take(maximum))
            {
                try
                {
                    AnalysisArtifactPersistenceRecord artifact = runtime.Store.GetAnalysisArtifact(runId, evidenceId);
                    expansion.Items.Add(new EvidenceItem
                    {
                        EvidenceId = evidenceId,
                        EvidenceKind = artifact.Kind,
                        InertSummary = $"Retained {artifact.Kind} evidence ({artifact.State}).",
                        ProvenanceId = artifact.ProvenanceId,
                        ArtifactSchemaIdentity = artifact.SchemaId,
                        ArtifactSchemaVersion = artifact.SchemaVersion,
                        OriginatingRunId = runId,
                        LlmInvolvementState = "unknown-not-inferred",
                        Availability = "retained-metadata",
                        ContentSha256 = artifact.ContentSha256,
                    });
                }
                catch (KeyNotFoundException)
                {
                    expansion.Items.Add(new EvidenceItem
                    {
                        EvidenceId = evidenceId,
                        EvidenceKind = "referenced-evidence",
                        InertSummary = "The canonical result retains this evidence identity, but no expandable application artifact is available.",
                        OriginatingRunId = runId,
                        Availability = "reference-only",
                    });
                    expansion.InertGaps.Add($"Evidence {evidenceId} is reference-only in this application projection.");
                }
            }
            return Task.FromResult(new GetEvidenceExpansionResponse { Expansion = expansion });
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        {
            return Task.FromResult(new GetEvidenceExpansionResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetFocusedModViewResponse> GetFocusedModView(
        GetFocusedModViewRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new GetFocusedModViewResponse { Failure = contractFailure });
        }
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetFocusedModViewResponse { ProjectionInvalidated = ProjectionInvalidated() });
        }
        try
        {
            string runId = Required(request.RunId?.Value, "run ID");
            if (request.RequestedMaximumItems is < 1 or > 100)
            {
                throw new ArgumentException("The focused result view exceeds its closed bound.");
            }
            int maximum = checked((int)request.RequestedMaximumItems);
            IReadOnlyList<ResultItemPersistenceRecord> values = runtime.Store.GetFocusedResultItems(
                runId, Required(request.ExactSubjectId, "exact subject ID"), maximum);
            ResultOverviewPersistenceRecord overview = runtime.Store.GetResultOverview(runId);
            FocusedModView view = new()
            {
                RunId = new RunId { Value = runId },
                ExactSubjectId = request.ExactSubjectId,
                Truncated = values.Count > maximum,
                ProjectionVersion = new ProjectionVersion { Value = "1" },
            };
            view.Items.Add(values.Take(maximum).Select(ToResultItemSummary));
            view.Coverage.Add(overview.Coverage
                .Where(item => StringComparer.Ordinal.Equals(item.PopulationId, request.ExactSubjectId))
                .Select(ToResultCoverage));
            view.EvidenceIds.Add(values.Take(maximum).SelectMany(item => item.EvidenceIds)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            view.InertGaps.Add(values.Take(maximum)
                .Where(item => item.Kind is "coverage-gap" or "failure" or "abstention")
                .Select(item => item.Summary)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
            if (overview.Gaps.Count > values.Take(maximum).Count(item => item.Kind == "coverage-gap"))
            {
                view.InertGaps.Add(
                    "Additional run-level coverage gaps exist outside this exact subject; inspect the bounded run overview.");
            }
            return Task.FromResult(new GetFocusedModViewResponse { View = view });
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        {
            return Task.FromResult(new GetFocusedModViewResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<ListFindingReportsResponse> ListFindingReports(
        ListFindingReportsRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new ListFindingReportsResponse { Failure = contractFailure });
        }
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(FindingReportCursorRejected(
                CursorDisposition.ProjectionInvalidated, "The finding-report projection was rebuilt."));
        }
        try
        {
            string runId = Required(request.RunId?.Value, "run ID");
            string[] states = request.States.Select(FindingReportStateToken)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            string sort = request.Sort == ProtoFindingReportSort.StateThenIdentityAscending ? "state" : "identity";
            string queryHash = ResultQueryHash(states, request.SearchText, sort);
            string projectionIdentity = runtime.Store.GetFindingReportProjectionIdentity(runId);
            FindingReportPageCursor? cursor = null;
            if (request.After?.OpaqueValue.Length > 0)
            {
                try
                {
                    cursor = JsonSerializer.Deserialize<FindingReportPageCursor>(
                            DecodeAuthenticated(request.After.OpaqueValue))
                        ?? throw new InvalidOperationException("The finding-report cursor is malformed.");
                }
                catch (Exception exception) when (exception is InvalidOperationException or JsonException)
                {
                    return Task.FromResult(FindingReportCursorRejected(CursorDisposition.Malformed, Bounded(exception.Message)));
                }
                CursorDisposition binding = ValidateFindingReportCursorBinding(
                    cursor, runId, projectionIdentity, queryHash, sort,
                    request.RequestedPageSize, DateTimeOffset.UtcNow);
                if (binding != CursorDisposition.Unspecified)
                {
                    return Task.FromResult(FindingReportCursorRejected(
                        binding, "The finding-report cursor no longer matches the exact closed query."));
                }
            }
            FindingReportPagePersistenceRecord retained = runtime.Store.ListFindingReports(
                runId, states, request.SearchText, sort, checked((int)request.RequestedPageSize),
                cursor?.LastReportId, cursor?.LastState);
            FindingReportPage page = new()
            {
                HasMore = retained.HasMore,
                ProjectionVersion = new ProjectionVersion { Value = retained.ProjectionVersion },
            };
            page.Items.Add(retained.Items.Select(ToFindingReportSummary));
            if (retained.HasMore && retained.Items.Count > 0)
            {
                FindingReportSummaryPersistenceRecord last = retained.Items[^1];
                page.Next = new PageCursor
                {
                    OpaqueValue = EncodeAuthenticated(JsonSerializer.SerializeToUtf8Bytes(
                        new FindingReportPageCursor(runId, projectionIdentity, queryHash, sort,
                            request.RequestedPageSize, last.ReportId, last.State,
                            DateTimeOffset.UtcNow.AddMinutes(5)))),
                };
            }
            return Task.FromResult(new ListFindingReportsResponse { Page = page });
        }
        catch (FindingReportProjectionUnavailableException exception)
        {
            return Task.FromResult(new ListFindingReportsResponse
            {
                Availability = new FindingReportAvailability
                {
                    RunId = new RunId { Value = exception.RunId },
                    Availability = AvailabilityState.Unavailable,
                    InertReason = Bounded(exception.Message),
                    RetainedResultsPresent = true,
                    ProjectionVersion = new ProjectionVersion { Value = "1" },
                },
            });
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        {
            return Task.FromResult(new ListFindingReportsResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetFindingReportResponse> GetFindingReport(
        GetFindingReportRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new GetFindingReportResponse { Failure = contractFailure });
        }
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetFindingReportResponse { ProjectionInvalidated = ProjectionInvalidated() });
        }
        try
        {
            string runId = Required(request.RunId?.Value, "run ID");
            FindingReportDetailPersistenceRecord retained = runtime.Store.GetFindingReport(runId, request.ReportId);
            FindingReportDocument report = FindingReportJsonCodec.Deserialize(retained.ReportPayload);
            FindingCaseContract canonical = FindingCaseJsonCodec.Deserialize(
                runtime.Store.ReadFindingCasePayload(retained.Summary.SourcePayloadId));
            return Task.FromResult(new GetFindingReportResponse
            {
                Report = ToFindingReportDetail(retained.Summary, report, canonical),
            });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or KeyNotFoundException)
        {
            return Task.FromResult(new GetFindingReportResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetReviewStateResponse> GetReviewState(
        GetReviewStateRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new GetReviewStateResponse { Failure = contractFailure });
        }
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(new GetReviewStateResponse { ProjectionInvalidated = ProjectionInvalidated() });
        }
        try
        {
            ReviewStatePersistenceRecord state = runtime.Store.GetReviewState(
                Required(request.RunId?.Value, "run ID"), Required(request.SubjectKind, "subject kind"),
                Required(request.SubjectOccurrenceId, "subject occurrence ID"));
            return Task.FromResult(new GetReviewStateResponse { State = ToReviewState(state) });
        }
        catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException)
        {
            return Task.FromResult(new GetReviewStateResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<SubmitReviewEventResponse> SubmitReviewEvent(
        SubmitReviewEventRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new SubmitReviewEventResponse { Failure = contractFailure });
        }
        try
        {
            ReviewMutationPersistenceResult result = runtime.Store.ApplyReviewEvent(
                new ReviewMutationPersistenceRequest(
                    request.IdempotencyKey, Required(request.RunId?.Value, "run ID"), request.SubjectKind,
                    request.SubjectOccurrenceId, checked((long)request.ExpectedRevision), request.EventKind,
                    request.Disposition, request.Suppressed, request.InertAnnotation,
                    EmptyToNull(request.SourceEventId), EmptyToNull(request.ContinuityAssessmentId)),
                DateTimeOffset.UtcNow);
            return Task.FromResult(result.Conflict
                ? new SubmitReviewEventResponse
                {
                    Conflict = new ReviewConflict
                    {
                        ExpectedRevision = checked((ulong)result.ExpectedRevision),
                        CurrentSafeState = ToReviewState(result.State),
                    },
                }
                : new SubmitReviewEventResponse { State = ToReviewState(result.State) });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            return Task.FromResult(new SubmitReviewEventResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<ListAssumptionsResponse> ListAssumptions(
        ListAssumptionsRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new ListAssumptionsResponse { Failure = contractFailure });
        }
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(AssumptionCursorRejected(CursorDisposition.ProjectionInvalidated, "The assumption projection was rebuilt."));
        }
        if (request.RequestedPageSize is < 1 or > 100)
        {
            return Task.FromResult(new ListAssumptionsResponse
            {
                Failure = Failure(FailureCode.InvalidArgument, "The assumption page exceeds its closed bound."),
            });
        }
        try
        {
            string projectionIdentity = runtime.Store.GetAssumptionProjectionIdentity(request.ProfileId);
            AssumptionPageCursor? cursor = null;
            if (request.After?.OpaqueValue.Length > 0)
            {
                try
                {
                    cursor = JsonSerializer.Deserialize<AssumptionPageCursor>(DecodeAuthenticated(request.After.OpaqueValue))
                        ?? throw new InvalidOperationException("The assumption cursor is malformed.");
                }
                catch (Exception exception) when (exception is InvalidOperationException or JsonException)
                {
                    return Task.FromResult(AssumptionCursorRejected(CursorDisposition.Malformed, Bounded(exception.Message)));
                }
                if (cursor.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    return Task.FromResult(AssumptionCursorRejected(CursorDisposition.Expired, "The assumption cursor expired."));
                }
                if (!StringComparer.Ordinal.Equals(cursor.ProfileId, request.ProfileId)
                    || cursor.PageSize != request.RequestedPageSize)
                {
                    return Task.FromResult(AssumptionCursorRejected(CursorDisposition.QueryMismatch, "The assumption cursor belongs to another profile or page size."));
                }
                if (!StringComparer.Ordinal.Equals(cursor.ProjectionIdentity, projectionIdentity))
                {
                    return Task.FromResult(AssumptionCursorRejected(
                        CursorDisposition.ProjectionInvalidated, "The assumption projection changed after this cursor was issued."));
                }
            }
            IReadOnlyList<AssumptionStatePersistenceRecord> values = runtime.Store.ListAssumptions(
                request.ProfileId, checked((int)request.RequestedPageSize), cursor?.LastAssumptionId);
            bool more = values.Count > request.RequestedPageSize;
            AssumptionStatePersistenceRecord[] items = values.Take(checked((int)request.RequestedPageSize)).ToArray();
            AssumptionPage page = new()
            {
                HasMore = more,
                ProjectionVersion = new ProjectionVersion { Value = "1" },
            };
            page.Items.Add(items.Select(ToAssumptionState));
            if (more)
            {
                page.Next = new PageCursor
                {
                    OpaqueValue = EncodeAuthenticated(JsonSerializer.SerializeToUtf8Bytes(
                        new AssumptionPageCursor(request.ProfileId, projectionIdentity, request.RequestedPageSize,
                            items[^1].AssumptionId, DateTimeOffset.UtcNow.AddMinutes(5)))),
                };
            }
            return Task.FromResult(new ListAssumptionsResponse { Page = page });
        }
        catch (ArgumentException exception)
        {
            return Task.FromResult(new ListAssumptionsResponse
            {
                Failure = Failure(FailureCode.InvalidArgument, Bounded(exception.Message)),
            });
        }
    }

    public override Task<SubmitAssumptionEventResponse> SubmitAssumptionEvent(
        SubmitAssumptionEventRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new SubmitAssumptionEventResponse { Failure = contractFailure });
        }
        try
        {
            AssumptionMutationPersistenceResult result = runtime.Store.ApplyAssumptionEvent(
                new AssumptionMutationPersistenceRequest(
                    request.IdempotencyKey, request.AssumptionId, request.ProfileId,
                    checked((long)request.ExpectedRevision), request.EventKind, request.Origin,
                    request.Confirmation, request.Subject, request.InertValue, request.Scope,
                    request.DependencyIds.ToArray()), DateTimeOffset.UtcNow);
            return Task.FromResult(result.Conflict
                ? new SubmitAssumptionEventResponse
                {
                    Conflict = new AssumptionConflict
                    {
                        ExpectedRevision = checked((ulong)result.ExpectedRevision),
                        CurrentSafeState = ToAssumptionState(result.State),
                    },
                }
                : new SubmitAssumptionEventResponse { State = ToAssumptionState(result.State) });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException)
        {
            return Task.FromResult(new SubmitAssumptionEventResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<CreateStructuredExportResponse> CreateStructuredExport(
        CreateStructuredExportRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new CreateStructuredExportResponse { Failure = contractFailure });
        }
        try
        {
            StructuredExportPersistenceRecord value = runtime.Store.CreateStructuredExport(
                new StructuredExportPersistenceRequest(
                    request.IdempotencyKey, Required(request.RunId?.Value, "run ID"),
                    request.SelectedResultItemIds.ToArray(), request.SelectedReviewEventIds.ToArray(),
                    request.SelectedAssumptionIds.ToArray(), request.Filters.ToArray(), request.SharingClass,
                    request.DeclaredOmissions.ToArray(), request.PrivacyDecisions.ToArray(),
                    request.SourcePolicyDecisions.ToArray()), DateTimeOffset.UtcNow);
            return Task.FromResult(new CreateStructuredExportResponse { Export = ToStructuredExport(value) });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return Task.FromResult(new CreateStructuredExportResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetStructuredExportResponse> GetStructuredExport(
        GetStructuredExportRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new GetStructuredExportResponse { Failure = contractFailure });
        }
        try
        {
            return Task.FromResult(new GetStructuredExportResponse
            {
                Export = ToStructuredExport(runtime.Store.GetStructuredExport(Required(request.ExportId, "export ID"))),
            });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetStructuredExportResponse
            {
                Failure = Failure(FailureCode.NotFound, "The requested structured export does not exist."),
            });
        }
        catch (InvalidDataException)
        {
            return Task.FromResult(new GetStructuredExportResponse
            {
                Failure = Failure(FailureCode.Indeterminate, "The retained structured export failed integrity validation."),
            });
        }
    }

    public override Task<PreviewStructuredExportDeletionResponse> PreviewStructuredExportDeletion(
        PreviewStructuredExportDeletionRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new PreviewStructuredExportDeletionResponse { Failure = contractFailure });
        }
        try
        {
            StructuredExportDeletionPreviewPersistenceRecord preview =
                runtime.Store.PreviewStructuredExportDeletion(request.ExportId);
            return Task.FromResult(new PreviewStructuredExportDeletionResponse
            {
                Preview = new StructuredExportDeletionPreview
                {
                    ExportId = preview.ExportId,
                    CurrentState = StructuredExportStateValue(preview.CurrentState),
                    ArtifactPresent = preview.ArtifactPresent,
                    SourceRunMutated = false,
                    SourceResultsMutated = false,
                    ReviewHistoryMutated = false,
                    AssumptionsMutated = false,
                    ProvenanceMutated = false,
                    AuditHistoryRetained = preview.AuditHistoryRetained,
                },
            });
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidDataException)
        {
            return Task.FromResult(new PreviewStructuredExportDeletionResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.Indeterminate,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<DeleteStructuredExportResponse> DeleteStructuredExport(
        DeleteStructuredExportRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new DeleteStructuredExportResponse { Failure = contractFailure });
        }
        try
        {
            return Task.FromResult(new DeleteStructuredExportResponse
            {
                Export = ToStructuredExport(runtime.Store.DeleteStructuredExport(
                    request.IdempotencyKey, request.ExportId, DateTimeOffset.UtcNow)),
            });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or InvalidDataException or KeyNotFoundException)
        {
            return Task.FromResult(new DeleteStructuredExportResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    private static ResultOverview ToResultOverview(ResultOverviewPersistenceRecord value)
    {
        ResultOverview result = new()
        {
            RunId = new RunId { Value = value.RunId },
            Readiness = value.Readiness switch
            {
                "scope-limited" => ResultReadinessState.ScopeLimited,
                "no-readiness" => ResultReadinessState.NoReadiness,
                "provisional-incomplete" => ResultReadinessState.ProvisionalIncomplete,
                "results-stale" => ResultReadinessState.ResultsStale,
                _ => ResultReadinessState.Unknown,
            },
            InertSummary = value.Summary,
            FindingCount = checked((ulong)value.FindingCount),
            SupportedCaseCount = checked((ulong)value.SupportedCaseCount),
            LeadOnlyCaseCount = checked((ulong)value.LeadOnlyCaseCount),
            DurationState = value.DurationState,
            DurationMillis = checked((ulong)value.DurationMilliseconds),
            CostState = value.CostState,
            CalculatedCostNanoUsd = value.CalculatedCostNanoUsd,
            NoSafetyGuarantee = value.NoSafetyGuarantee,
            ProjectionVersion = new ProjectionVersion { Value = value.ProjectionVersion },
        };
        result.Coverage.Add(value.Coverage.Select(ToResultCoverage));
        result.InertFailures.Add(value.Failures);
        result.InertGaps.Add(value.Gaps);
        return result;
    }

    private static ContractFailure? PhaseCContractFailure(IMessage request)
    {
        try
        {
            Infinium.Application.Runtime.ApplicationContractValidator.ValidatePhaseC(request);
            return null;
        }
        catch (InvalidDataException exception)
        {
            return Failure(FailureCode.InvalidArgument, Bounded(exception.Message));
        }
    }

    private static FindingReportSummary ToFindingReportSummary(FindingReportSummaryPersistenceRecord value) => new()
    {
        ReportId = value.ReportId,
        RunId = new RunId { Value = value.RunId },
        State = FindingReportStateValue(value.State),
        FindingId = value.FindingId ?? string.Empty,
        CaseId = value.CaseId ?? string.Empty,
        SubjectId = value.SubjectId,
        InertTitle = value.Title,
        InertConclusion = value.Conclusion,
        AnalyzerId = value.AnalyzerId,
        RetainedSourcePayloadId = value.SourcePayloadId,
        RetainedSourcePayloadSha256 = value.SourcePayloadSha256,
    };

    private static FindingReportDetail ToFindingReportDetail(
        FindingReportSummaryPersistenceRecord summary,
        FindingReportDocument report,
        FindingCaseContract canonical)
    {
        FindingRecommendationContract? recommendation = FindRecommendation(report, canonical);
        FindingReportDetail detail = new()
        {
            Summary = ToFindingReportSummary(summary),
            SchemaIdentity = report.SchemaId,
            SchemaVersion = report.SchemaVersion.ToString(),
            ContractMaturity = report.ContractMaturity,
            InertWhatHappened = report.WhatHappened,
            InertWhyItMatters = report.WhyItMatters,
            Assessment = new Infinium.Contracts.Protobuf.Application.V1.FindingReportAssessment
            {
                Severity = report.Assessment.Severity.ToString(),
                InertSeverityBasis = report.Assessment.SeverityBasis,
                Confidence = report.Assessment.Confidence.ToString(),
                InertConfidenceBasis = report.Assessment.ConfidenceBasis,
                AnalyzerMaturity = report.Assessment.AnalyzerMaturity.ToString(),
                InertMaturityBasis = report.Assessment.MaturityBasis,
                InertCalibrationBoundary = report.Assessment.CalibrationBoundary,
            },
            RecommendationKind = recommendation?.Kind.ToString() ?? "not-applicable",
            InertRecommendedAction = report.RecommendedAction,
            InertReversibility = recommendation?.Reversibility
                ?? "No retained recommendation reversibility applies to this non-recommendation result.",
            InertValidationSteps = recommendation?.Verification ?? report.ValidationSteps,
            Provenance = new Infinium.Contracts.Protobuf.Application.V1.FindingReportProvenance
            {
                SourceSchemaIdentity = report.Provenance.SourceSchemaId,
                SourceSchemaVersion = report.Provenance.SourceSchemaVersion.ToString(),
                SourcePayloadId = report.Provenance.SourcePayloadId.Value,
                SourceInputFingerprintSha256 = report.Provenance.SourceInputFingerprint.Value,
                SourceAssignmentId = report.Provenance.SourceAssignmentId.Value,
                ReplayEquivalent = report.Provenance.ReplayEquivalent,
                CanonicalArtifactRole = report.Provenance.CanonicalArtifactRole,
            },
            ProjectionVersion = new ProjectionVersion { Value = "1" },
        };
        detail.AffectedSubjects.Add(report.AffectedSubjects.Select(item =>
            new Infinium.Contracts.Protobuf.Application.V1.FindingReportSubject
            {
                Kind = item.Kind,
                SubjectId = item.SubjectId.Value,
                InertDetail = item.Detail,
            }));
        detail.TaxonomyAssignments.Add(report.TaxonomyAssignments.Select(item =>
            new Infinium.Contracts.Protobuf.Application.V1.FindingReportTaxonomyAssignment
            {
                Axis = item.Axis,
                Code = item.Code ?? string.Empty,
                Applicability = item.Applicability,
                InertBasis = item.Basis,
            }));
        detail.Coverage.Add(report.Coverage.Select(item =>
            new Infinium.Contracts.Protobuf.Application.V1.FindingReportCoverage
            {
                PopulationId = item.PopulationId,
                Denominator = checked((ulong)item.Denominator),
                Completed = checked((ulong)item.Completed),
                CompletedWithGaps = checked((ulong)item.CompletedWithGaps),
                Failed = checked((ulong)item.Failed),
                SkippedOrUnsupported = checked((ulong)item.SkippedOrUnsupported),
            }));
        detail.SupportingEvidenceIds.Add(report.SupportingEvidenceIds.Select(item => item.Value));
        detail.ContradictingEvidenceIds.Add(report.ContradictingEvidenceIds.Select(item => item.Value));
        detail.InertAssumptions.Add(report.Assumptions);
        detail.InertApplicabilityConditions.Add(report.ApplicabilityConditions);
        detail.InertUncertainty.Add(report.Uncertainty);
        detail.InertUnresolvedQuestions.Add(report.UnresolvedQuestions);
        detail.InertFailures.Add(report.Failures);
        detail.InertExclusions.Add(report.Exclusions);
        detail.InertGaps.Add(report.Gaps);
        detail.InertRisks.Add(recommendation?.Risks
            ?? ["No retained recommendation risk statement applies to this non-recommendation result."]);
        detail.InertUnsupportedOrNotEstablished.Add(report.UnsupportedOrNotEstablished);
        return detail;
    }

    private static FindingRecommendationContract? FindRecommendation(
        FindingReportDocument report,
        FindingCaseContract canonical)
    {
        if (report.FindingId is not null)
        {
            return canonical.Recommendations.SingleOrDefault(item => item.FindingOccurrenceId == report.FindingId);
        }
        FindingRecommendationContract? abstention = canonical.Recommendations
            .SingleOrDefault(item => item.AbstentionId == report.SubjectId);
        if (abstention is not null || report.CaseId is null)
        {
            return abstention;
        }
        AnalysisCaseContract? sourceCase = canonical.Cases.SingleOrDefault(item => item.CaseOccurrenceId == report.CaseId);
        return sourceCase is null
            ? null
            : canonical.Recommendations.SingleOrDefault(item =>
                item.LeadHypothesisId is not null
                && sourceCase.HypothesisIds.Contains(item.LeadHypothesisId));
    }

    private static ProtoFindingReportState FindingReportStateValue(string value) => value switch
    {
        "supported-finding" => ProtoFindingReportState.SupportedFinding,
        "resolved-negative" => ProtoFindingReportState.ResolvedNegative,
        "abstention" => ProtoFindingReportState.Abstention,
        "failure" => ProtoFindingReportState.Failure,
        "limited" => ProtoFindingReportState.Limited,
        "coverage-gap" => ProtoFindingReportState.CoverageGap,
        _ => ProtoFindingReportState.Unknown,
    };

    private static string FindingReportStateToken(ProtoFindingReportState value) => value switch
    {
        ProtoFindingReportState.SupportedFinding => "supported-finding",
        ProtoFindingReportState.ResolvedNegative => "resolved-negative",
        ProtoFindingReportState.Abstention => "abstention",
        ProtoFindingReportState.Failure => "failure",
        ProtoFindingReportState.Limited => "limited",
        ProtoFindingReportState.CoverageGap => "coverage-gap",
        _ => throw new ArgumentException("The finding-report state is unsupported."),
    };

    private static ResultCoverage ToResultCoverage(ResultCoveragePersistenceRecord value)
    {
        ResultCoverage result = new()
        {
            PopulationId = value.PopulationId,
            DenominatorLabel = value.DenominatorLabel,
            Denominator = checked((ulong)value.Denominator),
            Completed = checked((ulong)value.Completed),
            State = value.State,
        };
        result.InertGaps.Add(value.Gaps);
        return result;
    }

    private static ResultItemSummary ToResultItemSummary(ResultItemPersistenceRecord value) => new()
    {
        ItemId = value.ItemId,
        RunId = new RunId { Value = value.RunId },
        Kind = ResultKind(value.Kind),
        LogicalId = value.LogicalId,
        CaseOccurrenceId = value.CaseOccurrenceId is null ? null : new CaseOccurrenceId { Value = value.CaseOccurrenceId },
        InertSummary = value.Summary,
        Severity = value.Severity,
        Confidence = value.Confidence,
        AnalyzerId = value.AnalyzerId,
        AnalyzerVersion = new SemanticVersion { Value = value.AnalyzerVersion },
    };

    private static ResultDetail BuildResultDetail(ResultItemPersistenceRecord item, FindingCaseContract source)
    {
        ResultDetail result = new()
        {
            Summary = ToResultItemSummary(item),
            SourcePayloadId = item.SourcePayloadId,
            SourcePayloadSha256 = item.SourcePayloadSha256,
            ProjectionVersion = new ProjectionVersion { Value = "1" },
        };
        result.EvidenceIds.Add(item.EvidenceIds);
        result.SubjectIds.Add(item.SubjectIds);
        switch (item.Kind)
        {
            case "finding":
                FindingContract finding = source.Findings.Single(value => value.FindingOccurrenceId.Value == item.ItemId);
                result.InertConclusion = finding.Conclusion;
                result.TaxonomyAssignmentIds.Add(finding.TaxonomyAssignmentIds.Select(value => value.Value));
                FindingRecommendationContract[] findingRecommendations = source.Recommendations
                    .Where(value => value.FindingOccurrenceId?.Value == item.ItemId).ToArray();
                result.RecommendationIds.Add(findingRecommendations.Select(value => value.RecommendationId.Value));
                result.InertUncertainty.Add(findingRecommendations.Select(value => value.Uncertainty));
                break;
            case "supported-case":
            case "lead-only-case":
                AnalysisCaseContract @case = source.Cases.Single(value => value.CaseOccurrenceId.Value == item.ItemId);
                result.InertCause = @case.SharedCause;
                result.FindingOccurrenceIds.Add(@case.FindingOccurrenceIds.Select(value => value.Value));
                result.HypothesisIds.Add(@case.HypothesisIds.Select(value => value.Value));
                break;
            case "abstention":
                FindingCaseAbstentionContract abstention = source.Abstentions.Single(value => value.AbstentionId.Value == item.ItemId);
                result.InertConclusion = abstention.Reason;
                result.HypothesisIds.Add(abstention.HypothesisId.Value);
                result.InertGaps.Add(abstention.RequiredInformation);
                break;
            case "coverage-gap":
                FindingCaseGapContract gap = source.Gaps.Single(value => value.GapId.Value == item.ItemId);
                result.InertConclusion = gap.Reason;
                result.InertGaps.Add(gap.MissingCapabilityOrInformation);
                break;
            case "failure":
                CoverageFailureFactContract failure = source.CoverageFailures.Single(value => value.FailureId.Value == item.ItemId);
                result.InertConclusion = failure.Message;
                result.InertGaps.Add(failure.FailureCode);
                break;
        }
        return result;
    }

    private static ReviewState ToReviewState(ReviewStatePersistenceRecord value)
    {
        ReviewState state = new()
        {
            SubjectKind = value.SubjectKind,
            SubjectOccurrenceId = value.SubjectOccurrenceId,
            Revision = checked((ulong)value.Revision),
            Disposition = value.Disposition,
            Suppressed = value.Suppressed,
            InertAnnotation = value.Annotation,
            HistoryTruncated = value.HistoryTruncated,
            ProjectionVersion = new ProjectionVersion { Value = "1" },
        };
        state.History.Add(value.History.Select(item => new ReviewEvent
        {
            EventId = item.EventId,
            Revision = checked((ulong)item.Revision),
            EventKind = item.EventKind,
            Disposition = item.Disposition,
            Suppressed = item.Suppressed,
            InertAnnotation = item.Annotation,
            SourceEventId = item.SourceEventId ?? string.Empty,
            ContinuityAssessmentId = item.ContinuityAssessmentId ?? string.Empty,
            CreatedAt = ProtoMapping.ToProto(item.CreatedAt),
        }));
        return state;
    }

    private static AssumptionState ToAssumptionState(AssumptionStatePersistenceRecord value)
    {
        AssumptionState result = new()
        {
            AssumptionId = value.AssumptionId,
            ProfileId = value.ProfileId,
            Revision = checked((ulong)value.Revision),
            Origin = value.Origin,
            Confirmation = value.Confirmation,
            Subject = value.Subject,
            InertValue = value.Value,
            Scope = value.Scope,
            Effective = value.Effective,
            AnalysisContextId = value.AnalysisContextId,
            CreatedAt = ProtoMapping.ToProto(value.CreatedAt),
        };
        result.DependencyIds.Add(value.DependencyIds);
        return result;
    }

    private static StructuredExport ToStructuredExport(StructuredExportPersistenceRecord value)
    {
        StructuredExport result = new()
        {
            ExportId = value.ExportId,
            RunId = new RunId { Value = value.RunId },
            SharingClass = value.SharingClass,
            SchemaIdentity = value.SchemaIdentity,
            GeneratorIdentity = value.GeneratorIdentity,
            SelectionManifestSha256 = value.SelectionManifestSha256,
            ArtifactSha256 = value.ArtifactSha256,
            ArtifactBytes = checked((ulong)value.ArtifactBytes),
            CreatedAt = ProtoMapping.ToProto(value.CreatedAt),
            State = StructuredExportStateValue(value.State),
            DeletedAt = value.DeletedAt is null ? null : ProtoMapping.ToProto(value.DeletedAt.Value),
            EventRevision = checked((ulong)value.EventRevision),
            HistoryTruncated = value.HistoryTruncated,
        };
        result.SelectedResultItemIds.Add(value.SelectedResultItemIds);
        result.SelectedReviewEventIds.Add(value.SelectedReviewEventIds);
        result.SelectedAssumptionIds.Add(value.SelectedAssumptionIds);
        result.Filters.Add(value.Filters);
        result.DeclaredOmissions.Add(value.DeclaredOmissions);
        result.PrivacyDecisions.Add(value.PrivacyDecisions);
        result.SourcePolicyDecisions.Add(value.SourcePolicyDecisions);
        result.ProvenanceIds.Add(value.ProvenanceIds);
        result.History.Add(value.History.Select(item => new StructuredExportEvent
        {
            EventId = item.EventId,
            Revision = checked((ulong)item.Revision),
            EventKind = item.EventKind,
            RequestFingerprintSha256 = item.RequestFingerprintSha256,
            CreatedAt = ProtoMapping.ToProto(item.CreatedAt),
        }));
        return result;
    }

    private static ProtoStructuredExportState StructuredExportStateValue(string value) => value switch
    {
        "active" => ProtoStructuredExportState.Active,
        "deletion-pending" => ProtoStructuredExportState.DeletionPending,
        "deleted" => ProtoStructuredExportState.Deleted,
        _ => ProtoStructuredExportState.Unknown,
    };

    private static ResultItemKind ResultKind(string value) => value switch
    {
        "supported-case" => ResultItemKind.SupportedCase,
        "lead-only-case" => ResultItemKind.LeadOnlyCase,
        "finding" => ResultItemKind.Finding,
        "abstention" => ResultItemKind.Abstention,
        "failure" => ResultItemKind.Failure,
        "coverage-gap" => ResultItemKind.CoverageGap,
        _ => ResultItemKind.Unknown,
    };

    private static string ResultKindToken(ResultItemKind value) => value switch
    {
        ResultItemKind.SupportedCase => "supported-case",
        ResultItemKind.LeadOnlyCase => "lead-only-case",
        ResultItemKind.Finding => "finding",
        ResultItemKind.Abstention => "abstention",
        ResultItemKind.Failure => "failure",
        ResultItemKind.CoverageGap => "coverage-gap",
        _ => throw new ArgumentException("The result-item kind is not supported."),
    };

    private static string ResultQueryHash(IEnumerable<string> kinds, string search, string sort)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(string.Join('\n', kinds.Append(search).Append(sort))));
        return Convert.ToHexStringLower(bytes);
    }

    internal static CursorDisposition ValidateResultCursorBinding(
        ResultPageCursor cursor,
        string runId,
        string projectionIdentity,
        string queryHash,
        string sort,
        uint pageSize,
        DateTimeOffset now)
    {
        if (cursor.ExpiresAt <= now)
        {
            return CursorDisposition.Expired;
        }
        if (!StringComparer.Ordinal.Equals(cursor.RunId, runId))
        {
            return CursorDisposition.ScopeMismatch;
        }
        if (!StringComparer.Ordinal.Equals(cursor.ProjectionIdentity, projectionIdentity))
        {
            return CursorDisposition.ProjectionInvalidated;
        }
        if (!StringComparer.Ordinal.Equals(cursor.QueryHash, queryHash) || cursor.PageSize != pageSize)
        {
            return CursorDisposition.QueryMismatch;
        }
        return StringComparer.Ordinal.Equals(cursor.Sort, sort)
            ? CursorDisposition.Unspecified
            : CursorDisposition.SortMismatch;
    }

    internal static CursorDisposition ValidateFindingReportCursorBinding(
        FindingReportPageCursor cursor,
        string runId,
        string projectionIdentity,
        string queryHash,
        string sort,
        uint pageSize,
        DateTimeOffset now)
    {
        if (cursor.ExpiresAt <= now)
        {
            return CursorDisposition.Expired;
        }
        if (!StringComparer.Ordinal.Equals(cursor.RunId, runId))
        {
            return CursorDisposition.ScopeMismatch;
        }
        if (!StringComparer.Ordinal.Equals(cursor.ProjectionIdentity, projectionIdentity))
        {
            return CursorDisposition.ProjectionInvalidated;
        }
        if (!StringComparer.Ordinal.Equals(cursor.QueryHash, queryHash) || cursor.PageSize != pageSize)
        {
            return CursorDisposition.QueryMismatch;
        }
        return StringComparer.Ordinal.Equals(cursor.Sort, sort)
            ? CursorDisposition.Unspecified
            : CursorDisposition.SortMismatch;
    }

    private static ListResultItemsResponse ResultCursorRejected(CursorDisposition disposition, string detail) => new()
    {
        CursorRejection = new CursorRejection
        {
            Disposition = disposition,
            CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
            Failure = Failure(FailureCode.ResyncRequired, detail),
        },
    };

    private static ListAssumptionsResponse AssumptionCursorRejected(CursorDisposition disposition, string detail) => new()
    {
        CursorRejection = new CursorRejection
        {
            Disposition = disposition,
            CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
            Failure = Failure(FailureCode.ResyncRequired, detail),
        },
    };

    private static ListFindingReportsResponse FindingReportCursorRejected(CursorDisposition disposition, string detail) => new()
    {
        CursorRejection = new CursorRejection
        {
            Disposition = disposition,
            CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
            Failure = Failure(FailureCode.ResyncRequired, detail),
        },
    };

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
