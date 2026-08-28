using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using App = Infinium.Contracts.Protobuf.Application.V1;
using Common = Infinium.Contracts.Protobuf.Common.V1;
using ProtoDomain = Infinium.Contracts.Protobuf.Domain.V1;

namespace Infinium.Application.Runtime;

/// <summary>Projects the closed application protobuf surface into renderer-schema JSON.</summary>
public sealed class RendererApplicationProjectionCodec : IGeneratedRendererProjectionCodec
{
    public JsonElement Project(App.GetApplicationBootstrapRequest request, App.GetApplicationBootstrapResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        return response.ResultCase switch
        {
            App.GetApplicationBootstrapResponse.ResultOneofCase.Bootstrap => Bootstrap(response.Bootstrap),
            App.GetApplicationBootstrapResponse.ResultOneofCase.Error => ApplicationError(response.Error, request.ExpectedProjectionVersion?.Value),
            _ => throw new InvalidDataException("The application bootstrap result is missing or unknown."),
        };
    }

    public JsonElement Project(App.ListResultItemsRequest request, App.ListResultItemsResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        if (response.ResultCase == App.ListResultItemsResponse.ResultOneofCase.Page
            && (response.Page.Items.Count > ProtocolConstants.MaximumPageItems
                || response.Page.HasMore != (response.Page.Next?.OpaqueValue.Length > 0)
                || response.Page.Items.Any(item => !StringComparer.Ordinal.Equals(item.RunId?.Value, request.RunId?.Value)
                    || !request.Kinds.Contains(item.Kind))))
        {
            throw new InvalidDataException("The native result page cursor, has-more state, run identity, or requested kind is inconsistent.");
        }
        JsonObject payload = response.ResultCase switch
        {
            App.ListResultItemsResponse.ResultOneofCase.Page => new JsonObject
            {
                ["outcome"] = "accepted",
                ["page"] = Page(response.Page),
            },
            App.ListResultItemsResponse.ResultOneofCase.CursorRejection => Resync(
                response.CursorRejection.CurrentProjectionVersion?.Value,
                ResyncReason(response.CursorRejection.Disposition)),
            App.ListResultItemsResponse.ResultOneofCase.Failure => Failure(response.Failure),
            _ => throw new InvalidDataException("The result-item page result is missing or unknown."),
        };
        return Element(payload);
    }

    public JsonElement Project(App.GetResultDetailRequest request, App.GetResultDetailResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        if (response.ResultCase == App.GetResultDetailResponse.ResultOneofCase.Detail
            && (!StringComparer.Ordinal.Equals(response.Detail.Summary?.RunId?.Value, request.RunId?.Value)
                || !StringComparer.Ordinal.Equals(response.Detail.Summary?.ItemId, request.ItemId)
                || response.Detail.Summary?.Kind != request.Kind))
        {
            throw new InvalidDataException("The native result detail does not match the originating request identity and kind.");
        }
        JsonObject payload = response.ResultCase switch
        {
            App.GetResultDetailResponse.ResultOneofCase.Detail => new JsonObject
            {
                ["outcome"] = "accepted",
                ["detail"] = Detail(response.Detail),
            },
            App.GetResultDetailResponse.ResultOneofCase.ProjectionInvalidated => Resync(
                response.ProjectionInvalidated.CurrentProjectionVersion?.Value,
                ResyncReason(response.ProjectionInvalidated.Reason)),
            App.GetResultDetailResponse.ResultOneofCase.Failure => Failure(response.Failure),
            _ => throw new InvalidDataException("The result-detail result is missing or unknown."),
        };
        return Element(payload);
    }

    public JsonElement Project(App.GetProgressRequest request, App.GetProgressResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        if (response.ResultCase == App.GetProgressResponse.ResultOneofCase.Progress
            && !StringComparer.Ordinal.Equals(response.Progress.RunId?.Value, request.RunId?.Value))
        {
            throw new InvalidDataException("The native progress projection does not match the originating run request.");
        }
        JsonObject payload = response.ResultCase switch
        {
            App.GetProgressResponse.ResultOneofCase.Progress => new JsonObject
            {
                ["outcome"] = "accepted",
                ["progress"] = Progress(response.Progress),
            },
            App.GetProgressResponse.ResultOneofCase.ProjectionInvalidated => Resync(
                response.ProjectionInvalidated.CurrentProjectionVersion?.Value,
                ResyncReason(response.ProjectionInvalidated.Reason)),
            App.GetProgressResponse.ResultOneofCase.Failure => Failure(response.Failure),
            _ => throw new InvalidDataException("The progress result is missing or unknown."),
        };
        return Element(payload);
    }

    public JsonElement Project(App.SubscribeEventsRequest request, App.ApplicationEvent applicationEvent)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(applicationEvent);
        if (request.RunScope.Count != 1
            || !StringComparer.Ordinal.Equals(applicationEvent.SubscriptionId?.Value, request.SubscriptionId?.Value)
            || !StringComparer.Ordinal.Equals(applicationEvent.RunScope?.Value, request.RunScope[0].Value)
            || (applicationEvent.PayloadCase == App.ApplicationEvent.PayloadOneofCase.Progress
                && !StringComparer.Ordinal.Equals(applicationEvent.Progress.RunId?.Value, request.RunScope[0].Value)))
        {
            throw new InvalidDataException("The application event does not match the originating subscription and run scope.");
        }
        JsonObject metadata = new()
        {
            ["coordinator_instance_id"] = Required(applicationEvent.CoordinatorInstanceId?.Value, "coordinator identity"),
            ["coordinator_fencing_epoch"] = UInt64(applicationEvent.CoordinatorFencingEpoch),
            ["subscription_id"] = Required(applicationEvent.SubscriptionId?.Value, "subscription identity"),
            ["durable_event_sequence"] = UInt64(applicationEvent.DurableEventSequence),
            ["projection_version"] = Required(applicationEvent.ProjectionVersion?.Value, "event projection version"),
            ["run_scope"] = Required(applicationEvent.RunScope?.Value, "event run scope"),
            ["resume_cursor"] = Required(Cursor(applicationEvent.ResumeCursor?.OpaqueValue), "event resume cursor"),
        };
        JsonObject payload = (applicationEvent.Kind, applicationEvent.PayloadCase) switch
        {
            (App.EventKind.Progress, App.ApplicationEvent.PayloadOneofCase.Progress) => new JsonObject
            {
                ["outcome"] = "accepted",
                ["event_kind"] = "progress",
                ["metadata"] = metadata,
                ["progress"] = Progress(applicationEvent.Progress),
            },
            (App.EventKind.LifecycleChanged, App.ApplicationEvent.PayloadOneofCase.LifecycleChanged) => new JsonObject
            {
                ["outcome"] = "accepted",
                ["event_kind"] = "lifecycle-changed",
                ["metadata"] = metadata,
                ["lifecycle_changed"] = new JsonObject
                {
                    ["previous_state"] = Lifecycle(applicationEvent.LifecycleChanged.PreviousState),
                    ["current_state"] = Lifecycle(applicationEvent.LifecycleChanged.CurrentState),
                    ["lifecycle_generation"] = UInt64(applicationEvent.LifecycleChanged.LifecycleGeneration),
                    ["transition_id"] = Required(applicationEvent.LifecycleChanged.TransitionId?.Value, "transition identity"),
                    ["transition_record_kind"] = TransitionKind(applicationEvent.LifecycleChanged.TransitionRecordKind),
                    ["lifecycle_policy_version"] = Required(applicationEvent.LifecycleChanged.LifecyclePolicyVersion?.Value, "lifecycle policy version"),
                },
            },
            (App.EventKind.ProjectionInvalidated, App.ApplicationEvent.PayloadOneofCase.ProjectionInvalidated) => EventResync(
                "projection-invalidated", metadata, applicationEvent.ProjectionInvalidated.CurrentProjectionVersion?.Value,
                ResyncReason(applicationEvent.ProjectionInvalidated.Reason)),
            (App.EventKind.ResyncRequired, App.ApplicationEvent.PayloadOneofCase.ResyncRequired) => EventResync(
                "resync-required", metadata, applicationEvent.ResyncRequired.CurrentProjectionVersion?.Value,
                ResyncReason(applicationEvent.ResyncRequired.Reason)),
            _ => throw new InvalidDataException("The application event kind and payload are missing, unknown, or inconsistent."),
        };
        return Element(payload);
    }

    private static JsonElement Bootstrap(App.ApplicationBootstrap bootstrap)
    {
        byte[] envelope = RendererBootstrapAdapter.BuildResponse(
            bootstrap, "renderer_session_00000001", 1, "renderer_request_00000001");
        using JsonDocument document = JsonDocument.Parse(envelope);
        return document.RootElement.GetProperty("payload").Clone();
    }

    private static JsonElement ApplicationError(App.ApplicationContractError error, string? expectedRevision)
    {
        string code = error.Code switch
        {
            App.ApplicationErrorCode.InvalidArgument => "invalid-argument",
            App.ApplicationErrorCode.Unauthenticated => "unauthenticated",
            App.ApplicationErrorCode.Unauthorized => "unauthorized",
            App.ApplicationErrorCode.NotFound => "not-found",
            App.ApplicationErrorCode.Conflict => "conflict",
            App.ApplicationErrorCode.IncompatibleVersion => "incompatible-version",
            App.ApplicationErrorCode.Unsupported => "unsupported",
            App.ApplicationErrorCode.LimitExceeded => "limit-exceeded",
            App.ApplicationErrorCode.DeadlineExpired => "deadline-expired",
            App.ApplicationErrorCode.StaleFence => "stale-fence",
            App.ApplicationErrorCode.ResyncRequired => "resync-required",
            App.ApplicationErrorCode.Indeterminate => "indeterminate",
            App.ApplicationErrorCode.Internal => "internal",
            App.ApplicationErrorCode.Replayed => "replayed",
            App.ApplicationErrorCode.OutOfOrder => "out-of-order",
            App.ApplicationErrorCode.Unavailable => "unavailable",
            App.ApplicationErrorCode.Cancelled => "cancelled",
            _ => throw new InvalidDataException("The application error code is unknown or unsupported."),
        };
        string outcome = code is "conflict" or "unsupported" or "unavailable" or "cancelled" or "indeterminate" or "resync-required" ? code : "rejected";
        JsonObject value = ErrorPayload(outcome, code, error.InertDetail, error.RetryMayBeSafe);
        if (outcome == "resync-required")
        {
            value["current_projection_version"] = Required(error.CurrentProjectionVersion?.Value, "current projection version");
        }
        if (outcome == "conflict")
        {
            value["conflict"] = new JsonObject
            {
                ["expected_revision"] = Required(expectedRevision, "expected revision"),
                ["current_revision"] = Required(error.CurrentRevision?.OpaqueValue, "current revision"),
                ["disposition"] = "resync-required",
            };
        }
        return Element(value);
    }

    private static JsonObject Failure(Common.Failure failure)
    {
        string code = failure.Code switch
        {
            Common.FailureCode.InvalidArgument => "invalid-argument",
            Common.FailureCode.Unauthenticated => "unauthenticated",
            Common.FailureCode.Unauthorized => "unauthorized",
            Common.FailureCode.NotFound => "not-found",
            Common.FailureCode.Conflict => "conflict",
            Common.FailureCode.IncompatibleVersion => "incompatible-version",
            Common.FailureCode.Unsupported => "unsupported",
            Common.FailureCode.LimitExceeded => "limit-exceeded",
            Common.FailureCode.DeadlineExpired => "deadline-expired",
            Common.FailureCode.StaleFence => "stale-fence",
            Common.FailureCode.Indeterminate => "indeterminate",
            Common.FailureCode.Internal => "internal",
            _ => throw new InvalidDataException("The native failure code is unknown or cannot be represented losslessly."),
        };
        if (code == "conflict")
        {
            throw new InvalidDataException("A native conflict without revision metadata cannot be projected as a renderer conflict.");
        }
        return ErrorPayload(code is "unsupported" or "indeterminate" ? code : "rejected", code, failure.Detail, failure.RetryMayBeSafe);
    }

    private static JsonObject ErrorPayload(string outcome, string code, string detail, bool retry) => new()
    {
        ["outcome"] = outcome,
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["inert_detail"] = Required(detail, "inert error detail"),
            ["retry_may_be_safe"] = retry,
        },
    };

    private static JsonObject Resync(string? projection, string reason) => new()
    {
        ["outcome"] = "resync-required",
        ["error"] = new JsonObject { ["code"] = "resync-required", ["inert_detail"] = $"Authoritative resynchronization is required: {reason}.", ["retry_may_be_safe"] = false },
        ["current_projection_version"] = Required(projection, "current projection version"),
    };

    private static JsonObject EventResync(string kind, JsonObject metadata, string? projection, string reason)
    {
        JsonObject value = Resync(projection, reason);
        value["event_kind"] = kind;
        value["metadata"] = metadata;
        value["reason"] = reason;
        return value;
    }

    private static JsonObject Summary(App.ResultItemSummary value)
    {
        JsonObject summary = new()
        {
            ["item_id"] = Required(value.ItemId, "result item identity"),
            ["run_id"] = Required(value.RunId?.Value, "result run identity"),
            ["kind"] = ResultKind(value.Kind),
            ["logical_id"] = Required(value.LogicalId, "logical result identity"),
            ["case_occurrence_id"] = string.IsNullOrEmpty(value.CaseOccurrenceId?.Value) ? null : value.CaseOccurrenceId.Value,
            ["inert_summary"] = value.InertSummary,
            ["severity"] = Required(value.Severity, "result severity"),
            ["confidence"] = Required(value.Confidence, "result confidence"),
            ["analyzer_id"] = Required(value.AnalyzerId, "analyzer identity"),
            ["analyzer_version"] = Required(value.AnalyzerVersion?.Value, "analyzer version"),
        };
        RemoveNullProperties(summary);
        return summary;
    }

    private static JsonObject Page(App.ResultItemPage value)
    {
        JsonObject page = new()
        {
            ["items"] = new JsonArray(value.Items.Select(Summary).ToArray()),
            ["next_cursor"] = Cursor(value.Next?.OpaqueValue),
            ["has_more"] = value.HasMore,
            ["projection_version"] = Required(value.ProjectionVersion?.Value, "result projection version"),
        };
        RemoveNullProperties(page);
        return page;
    }

    private static JsonObject Detail(App.ResultDetail value) => new()
    {
        ["summary"] = Summary(value.Summary),
        ["inert_conclusion"] = value.InertConclusion,
        ["inert_cause"] = value.InertCause,
        ["evidence_ids"] = Strings(value.EvidenceIds),
        ["contradicting_evidence_ids"] = Strings(value.ContradictingEvidenceIds),
        ["recommendation_ids"] = Strings(value.RecommendationIds),
        ["taxonomy_assignment_ids"] = Strings(value.TaxonomyAssignmentIds),
        ["finding_occurrence_ids"] = Strings(value.FindingOccurrenceIds),
        ["hypothesis_ids"] = Strings(value.HypothesisIds),
        ["inert_uncertainty"] = Strings(value.InertUncertainty),
        ["inert_gaps"] = Strings(value.InertGaps),
        ["source_payload_id"] = Required(value.SourcePayloadId, "source payload identity"),
        ["source_payload_sha256"] = Sha256(value.SourcePayloadSha256),
        ["subject_ids"] = Strings(value.SubjectIds),
        ["projection_version"] = Required(value.ProjectionVersion?.Value, "result-detail projection version"),
    };

    private static JsonObject Progress(App.ProgressSnapshot value) => new()
    {
        ["run_id"] = Required(value.RunId?.Value, "progress run identity"),
        ["lifecycle_state"] = Lifecycle(value.LifecycleState),
        ["progress"] = new JsonObject
        {
            ["denominator_state"] = Denominator(value.Progress.DenominatorState),
            ["population_revision"] = UInt64(value.Progress.PopulationRevision),
            ["total_units"] = Optional(value.Progress.TotalUnits),
            ["completed_units"] = UInt64(value.Progress.CompletedUnits),
            ["reused_units"] = UInt64(value.Progress.ReusedUnits),
            ["queued_units"] = UInt64(value.Progress.QueuedUnits),
            ["running_units"] = UInt64(value.Progress.RunningUnits),
            ["failed_units"] = UInt64(value.Progress.FailedUnits),
            ["skipped_units"] = UInt64(value.Progress.SkippedUnits),
            ["unsupported_units"] = UInt64(value.Progress.UnsupportedUnits),
            ["limited_units"] = UInt64(value.Progress.LimitedUnits),
            ["invalidated_units"] = UInt64(value.Progress.InvalidatedUnits),
            ["gap_units"] = UInt64(value.Progress.GapUnits),
        },
        ["cost"] = new JsonObject
        {
            ["reserved_nano_usd"] = Optional(value.Cost.ReservedNanoUsd),
            ["calculated_actual_nano_usd"] = Optional(value.Cost.CalculatedActualNanoUsd),
            ["provider_input_tokens"] = Optional(value.Cost.ProviderInputTokens),
            ["provider_output_tokens"] = Optional(value.Cost.ProviderOutputTokens),
            ["provider_reasoning_tokens"] = Optional(value.Cost.ProviderReasoningTokens),
            ["provider_dispatch_count"] = Optional(value.Cost.ProviderDispatchCount),
            ["provider_tool_call_count"] = Optional(value.Cost.ProviderToolCallCount),
            ["has_unresolved_hold"] = value.Cost.HasUnresolvedHold,
        },
        ["projection_version"] = Required(value.ProjectionVersion?.Value, "progress projection version"),
        ["durable_event_sequence"] = UInt64(value.DurableEventSequence),
        ["observed_at"] = Instant(value.ObservedAt),
    };

    private static JsonObject Optional(Common.OptionalUInt64 value) => value.Availability switch
    {
        Common.AvailabilityState.Available => new JsonObject { ["availability"] = "available", ["value"] = UInt64(value.Value) },
        Common.AvailabilityState.Unavailable => new JsonObject { ["availability"] = "unavailable" },
        Common.AvailabilityState.Unsupported => new JsonObject { ["availability"] = "unsupported" },
        Common.AvailabilityState.Unknown => new JsonObject { ["availability"] = "unknown" },
        _ => throw new InvalidDataException("The optional unsigned scalar availability is unspecified."),
    };

    private static JsonObject Optional(Common.OptionalInt64 value) => value.Availability switch
    {
        Common.AvailabilityState.Available => new JsonObject { ["availability"] = "available", ["value"] = value.Value.ToString(CultureInfo.InvariantCulture) },
        Common.AvailabilityState.Unavailable => new JsonObject { ["availability"] = "unavailable" },
        Common.AvailabilityState.Unsupported => new JsonObject { ["availability"] = "unsupported" },
        Common.AvailabilityState.Unknown => new JsonObject { ["availability"] = "unknown" },
        _ => throw new InvalidDataException("The optional signed scalar availability is unspecified."),
    };

    private static string ResultKind(App.ResultItemKind value) => value switch
    {
        App.ResultItemKind.SupportedCase => "supported-case",
        App.ResultItemKind.LeadOnlyCase => "lead-only-case",
        App.ResultItemKind.Finding => "finding",
        App.ResultItemKind.Abstention => "abstention",
        App.ResultItemKind.Failure => "failure",
        App.ResultItemKind.CoverageGap => "coverage-gap",
        _ => throw new InvalidDataException("The result item kind is unknown or unsupported."),
    };

    private static string Lifecycle(ProtoDomain.LifecycleState value) => value switch
    {
        ProtoDomain.LifecycleState.Queued => "queued",
        ProtoDomain.LifecycleState.Running => "running",
        ProtoDomain.LifecycleState.Waiting => "waiting",
        ProtoDomain.LifecycleState.Retrying => "retrying",
        ProtoDomain.LifecycleState.Pausing => "pausing",
        ProtoDomain.LifecycleState.Paused => "paused",
        ProtoDomain.LifecycleState.Cancelling => "cancelling",
        ProtoDomain.LifecycleState.Cancelled => "cancelled",
        ProtoDomain.LifecycleState.Completed => "completed",
        ProtoDomain.LifecycleState.CompletedWithGaps => "completed-with-gaps",
        ProtoDomain.LifecycleState.Failed => "failed",
        ProtoDomain.LifecycleState.LimitReached => "limit-reached",
        ProtoDomain.LifecycleState.InvalidatedByChangedInput => "invalidated-by-changed-input",
        _ => throw new InvalidDataException("The lifecycle state is unknown or unsupported."),
    };

    private static string Denominator(App.ProgressDenominatorState value) => value switch
    {
        App.ProgressDenominatorState.Known => "known",
        App.ProgressDenominatorState.Enumerating => "enumerating",
        App.ProgressDenominatorState.Unavailable => "unavailable",
        App.ProgressDenominatorState.Unknown => "unknown",
        App.ProgressDenominatorState.Unsupported => "unsupported",
        _ => throw new InvalidDataException("The progress denominator state is unspecified."),
    };

    private static string TransitionKind(ProtoDomain.LifecycleTransitionRecordKind value) => value switch
    {
        ProtoDomain.LifecycleTransitionRecordKind.Requested => "requested",
        ProtoDomain.LifecycleTransitionRecordKind.Observed => "observed",
        _ => throw new InvalidDataException("The lifecycle transition record kind is unknown or unsupported."),
    };

    private static string ResyncReason(App.ResyncReason value) => value switch
    {
        App.ResyncReason.SlowClient => "slow-client",
        App.ResyncReason.QueueOverflow => "queue-overflow",
        App.ResyncReason.SequenceGap => "sequence-gap",
        App.ResyncReason.CoordinatorRestart => "coordinator-restart",
        App.ResyncReason.ReplayWindowExpired => "replay-window-expired",
        App.ResyncReason.ProjectionRebuilt => "projection-rebuilt",
        App.ResyncReason.CursorInvalid => "cursor-invalid",
        _ => throw new InvalidDataException("The resync reason is unknown or unsupported."),
    };

    private static string ResyncReason(App.CursorDisposition value) => value switch
    {
        App.CursorDisposition.Expired => "replay-window-expired",
        App.CursorDisposition.ProjectionInvalidated => "projection-rebuilt",
        App.CursorDisposition.Malformed or App.CursorDisposition.QueryMismatch or App.CursorDisposition.SortMismatch or App.CursorDisposition.ScopeMismatch => "cursor-invalid",
        _ => throw new InvalidDataException("The cursor rejection disposition is unknown or unsupported."),
    };

    private static string UInt64(ulong value) => value.ToString(CultureInfo.InvariantCulture);
    private static string? Cursor(Google.Protobuf.ByteString? value) => value is null || value.Length == 0 ? null : Convert.ToBase64String(value.ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Instant(Common.Instant? value) => value is null ? throw new InvalidDataException("The observed instant is missing.") : DateTimeOffset.FromUnixTimeSeconds(value.UnixSeconds).AddTicks(value.Nanoseconds / 100).ToString("O", CultureInfo.InvariantCulture);
    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(value => JsonValue.Create(value)).ToArray());
    private static string Required(string? value, string name) => string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"The {name} is missing.") : value;
    private static string Sha256(string? value) => value is null || value.Length != 64 || value.Any(character => !char.IsAsciiDigit(character) && character is not (>= 'a' and <= 'f'))
        ? throw new InvalidDataException("The source payload fingerprint is not canonical lower-case SHA-256.") : value;
    private static JsonElement Element(JsonNode value) => JsonSerializer.SerializeToElement(value);
    private static void RemoveNullProperties(JsonObject value)
    {
        foreach (string name in value.Where(item => item.Value is null).Select(item => item.Key).ToArray())
        {
            value.Remove(name);
        }
    }
}
