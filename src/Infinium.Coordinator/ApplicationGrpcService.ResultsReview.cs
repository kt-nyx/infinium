using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Infinium.Application.Serialization;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

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
    uint PageSize,
    string LastAssumptionId,
    DateTimeOffset ExpiresAt);

public sealed partial class ApplicationGrpcService
{
    public override Task<GetResultOverviewResponse> GetResultOverview(
        GetResultOverviewRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
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
                        ProducerId = artifact.ProvenanceId,
                        ProducerVersion = artifact.SchemaVersion,
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

    public override Task<GetReviewStateResponse> GetReviewState(
        GetReviewStateRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
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
                        new AssumptionPageCursor(request.ProfileId, request.RequestedPageSize,
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

    public override Task<StartTargetedVerificationResponse> StartTargetedVerification(
        StartTargetedVerificationRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            TargetedVerificationPersistenceRecord value = runtime.Store.StartTargetedVerification(
                request.IdempotencyKey, request.RequestedRunId, Required(request.SourceRunId?.Value, "source run ID"),
                EmptyToNull(request.SourceFindingOccurrenceId), EmptyToNull(request.SourceCaseOccurrenceId),
                request.ExactScopeIds.ToArray(), request.UserGestureId, FromProto(request.DispatchDeadline),
                runtime.Authority.FencingEpoch, DateTimeOffset.UtcNow);
            return Task.FromResult(new StartTargetedVerificationResponse { Verification = ToTargetedVerification(value) });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return Task.FromResult(new StartTargetedVerificationResponse
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

    private static TargetedVerification ToTargetedVerification(TargetedVerificationPersistenceRecord value)
    {
        TargetedVerification result = new()
        {
            VerificationId = value.VerificationId,
            SourceRunId = new RunId { Value = value.SourceRunId },
            SuccessorRunId = new RunId { Value = value.SuccessorRunId },
            SourceFindingOccurrenceId = value.SourceFindingOccurrenceId ?? string.Empty,
            SourceCaseOccurrenceId = value.SourceCaseOccurrenceId ?? string.Empty,
            ReadinessBoundary = value.ReadinessBoundary,
            State = value.State,
            CreatedAt = ProtoMapping.ToProto(value.CreatedAt),
        };
        result.ExactScopeIds.Add(value.ExactScopeIds);
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
        };
        result.SelectedResultItemIds.Add(value.SelectedResultItemIds);
        result.SelectedReviewEventIds.Add(value.SelectedReviewEventIds);
        result.SelectedAssumptionIds.Add(value.SelectedAssumptionIds);
        result.Filters.Add(value.Filters);
        result.DeclaredOmissions.Add(value.DeclaredOmissions);
        result.PrivacyDecisions.Add(value.PrivacyDecisions);
        result.SourcePolicyDecisions.Add(value.SourcePolicyDecisions);
        result.ProvenanceIds.Add(value.ProvenanceIds);
        return result;
    }

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

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
