using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed record M1Slice6SuccessorV6AdmissionRequest(
    string AuthorizationId,
    string OperationId,
    string CampaignId,
    string Stage,
    string OperationKind,
    string AttemptId,
    string RequestId,
    string ReservationId,
    string DispatchFenceId,
    string OwnerKind,
    string OwnerId,
    string SemanticAuthorizationId,
    string SemanticOperationId,
    string CanonicalRequestSha256,
    long MaximumRequestBytes,
    long MaximumInputTokens,
    long MaximumOutputTokens,
    long MaximumRawResponseBytes,
    long DeadlineMilliseconds,
    long ReservedNanoUsd,
    long CoordinatorFencingEpoch,
    DateTimeOffset DispatchDeadlineUtc,
    DateTimeOffset AdmittedAt);

public sealed record M1Slice6SuccessorV6AdmissionReceipt(
    DateTimeOffset EffectiveGateTime,
    DateTimeOffset DeadlineUtc);

public sealed record M1Slice6SuccessorV6PersistenceReceipt(
    string ResponseId,
    string UsageEntryId,
    string SettlementId,
    string ReplayEdgeId,
    long SettledNanoUsd,
    long UnresolvedNanoUsd,
    bool ResponsePersisted);

public sealed record M1Slice6SuccessorV6ReservationReadModel(
    long MaximumInputTokens,
    long MaximumOutputTokens,
    long ReservedNanoUsd,
    long CoordinatorFencingEpoch,
    DateTimeOffset DeadlineUtc);

public sealed partial class AuthoritativeStore
{
    private const long M1Slice6HistoricalCommittedNanoUsd = 250_080_000;
    private const long M1Slice6HardBudgetNanoUsd = 10_000_000_000;

    public M1Slice6SuccessorV6AdmissionReceipt AdmitM1Slice6SuccessorV6(
        M1Slice6SuccessorV6AdmissionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.OperationId.StartsWith("m1s6-successor-v6-", StringComparison.Ordinal)
            || request.ReservedNanoUsd <= 0
            || request.DispatchDeadlineUtc <= request.AdmittedAt)
        { throw new InvalidOperationException("Successor-v6 admission is not an exact finite operation."); }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            long currentCommitted = ScalarLong(
                """
                SELECT $historical + COALESCE(SUM(CASE
                  WHEN terminal.event_kind='settled' THEN terminal.settled_nano_usd
                  WHEN terminal.event_kind='unresolved' THEN terminal.unresolved_nano_usd
                  WHEN terminal.event_kind='released-undispatched' THEN 0
                  ELSE operation.reserved_nano_usd END),0)
                FROM m1_slice6_successor_v6_operations operation
                LEFT JOIN m1_slice6_successor_v6_budget_events terminal
                  ON terminal.operation_id=operation.operation_id
                 AND terminal.event_kind IN ('settled','unresolved','released-undispatched');
                """, transaction, ("$historical", M1Slice6HistoricalCommittedNanoUsd));
            if (checked(currentCommitted + request.ReservedNanoUsd) > M1Slice6HardBudgetNanoUsd)
            { throw new InvalidOperationException("Successor-v6 SQLite admission would exceed the aggregate hard budget."); }
            Execute(
                """
                INSERT INTO m1_slice6_successor_v6_operations(
                  authorization_id,operation_id,campaign_id,stage,operation_kind,provider_attempt_id,
                  request_id,reservation_id,dispatch_fence_id,owner_kind,owner_id,
                  semantic_authorization_id,semantic_operation_id,canonical_request_sha256,
                  maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,
                  maximum_raw_response_bytes,deadline_milliseconds,reserved_nano_usd,
                  coordinator_fencing_epoch,dispatch_deadline_utc,admitted_at)
                VALUES($authorization,$operation,$campaign,$stage,$kind,$attempt,$request,$reservation,
                  $fence,$owner_kind,$owner,$semantic_authorization,$semantic_operation,$request_sha,
                  $maximum_request,$maximum_input,$maximum_output,$maximum_raw,$deadline_ms,$reserved,
                  $epoch,$deadline,$now);
                INSERT INTO m1_slice6_successor_v6_budget_events(
                  event_id,operation_id,reservation_id,event_kind,reserved_nano_usd,settled_nano_usd,
                  unresolved_nano_usd,released_nano_usd,occurred_at)
                VALUES($reservation || ':reserved',$operation,$reservation,'reserved',$reserved,0,0,0,$now);
                """, transaction,
                ("$authorization", request.AuthorizationId), ("$operation", request.OperationId),
                ("$campaign", request.CampaignId), ("$stage", request.Stage),
                ("$kind", request.OperationKind), ("$attempt", request.AttemptId),
                ("$request", request.RequestId), ("$reservation", request.ReservationId),
                ("$fence", request.DispatchFenceId), ("$owner_kind", request.OwnerKind),
                ("$owner", request.OwnerId), ("$semantic_authorization", request.SemanticAuthorizationId),
                ("$semantic_operation", request.SemanticOperationId),
                ("$request_sha", request.CanonicalRequestSha256),
                ("$maximum_request", request.MaximumRequestBytes),
                ("$maximum_input", request.MaximumInputTokens),
                ("$maximum_output", request.MaximumOutputTokens),
                ("$maximum_raw", request.MaximumRawResponseBytes),
                ("$deadline_ms", request.DeadlineMilliseconds), ("$reserved", request.ReservedNanoUsd),
                ("$epoch", request.CoordinatorFencingEpoch), ("$deadline", ToText(request.DispatchDeadlineUtc)),
                ("$now", ToText(request.AdmittedAt)));
            transaction.Commit();
            return new(request.AdmittedAt, request.DispatchDeadlineUtc);
        }
    }

    public void RecordM1Slice6SuccessorV6PossibleStart(string operationId, string attemptId,
        string requestId, string reservationId, string dispatchFenceId, DateTimeOffset occurredAt)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            long exact = ScalarLong(
                """
                SELECT COUNT(*) FROM m1_slice6_successor_v6_operations
                WHERE operation_id=$operation AND provider_attempt_id=$attempt AND request_id=$request
                  AND reservation_id=$reservation AND dispatch_fence_id=$fence
                  AND admitted_at<=$now AND dispatch_deadline_utc>$now;
                """, transaction, ("$operation", operationId), ("$attempt", attemptId),
                ("$request", requestId), ("$reservation", reservationId), ("$fence", dispatchFenceId),
                ("$now", ToText(occurredAt)));
            if (exact != 1) { throw new InvalidOperationException("Successor-v6 possible start lacks exact live admission."); }
            Execute(
                """
                INSERT INTO m1_slice6_successor_v6_budget_events(
                  event_id,operation_id,reservation_id,event_kind,reserved_nano_usd,settled_nano_usd,
                  unresolved_nano_usd,released_nano_usd,occurred_at)
                VALUES($fence || ':possible-start',$operation,$reservation,'possible-start',0,0,0,0,$now);
                """, transaction, ("$fence", dispatchFenceId), ("$operation", operationId),
                ("$reservation", reservationId), ("$now", ToText(occurredAt)));
            transaction.Commit();
        }
    }

    public void ReleaseM1Slice6SuccessorV6BeforeStart(string operationId, string reservationId,
        DateTimeOffset occurredAt)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            long reserved = ScalarLong("SELECT reserved_nano_usd FROM m1_slice6_successor_v6_operations "
                + "WHERE operation_id=$operation AND reservation_id=$reservation;", transaction,
                ("$operation", operationId), ("$reservation", reservationId));
            Execute(
                """
                INSERT INTO m1_slice6_successor_v6_budget_events(
                  event_id,operation_id,reservation_id,event_kind,reserved_nano_usd,settled_nano_usd,
                  unresolved_nano_usd,released_nano_usd,occurred_at)
                VALUES($reservation || ':released',$operation,$reservation,'released-undispatched',0,0,0,$reserved,$now);
                """, transaction, ("$reservation", reservationId), ("$operation", operationId),
                ("$reserved", reserved), ("$now", ToText(occurredAt)));
            transaction.Commit();
        }
    }

    public M1Slice6SuccessorV6PersistenceReceipt RetainM1Slice6SuccessorV6Ambiguous(
        string operationId, string reservationId, string settlementId, DateTimeOffset occurredAt)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            long reserved = ScalarLong("SELECT reserved_nano_usd FROM m1_slice6_successor_v6_operations "
                + "WHERE operation_id=$operation AND reservation_id=$reservation;", transaction,
                ("$operation", operationId), ("$reservation", reservationId));
            Execute(
                """
                INSERT INTO m1_slice6_successor_v6_budget_events(
                  event_id,operation_id,reservation_id,event_kind,reserved_nano_usd,settled_nano_usd,
                  unresolved_nano_usd,released_nano_usd,occurred_at)
                VALUES($settlement,$operation,$reservation,'unresolved',0,0,$reserved,0,$now);
                """, transaction, ("$settlement", settlementId), ("$operation", operationId),
                ("$reservation", reservationId), ("$reserved", reserved), ("$now", ToText(occurredAt)));
            transaction.Commit();
            return new("", "", settlementId, "", 0, reserved, false);
        }
    }

    public M1Slice6SuccessorV6PersistenceReceipt PersistM1Slice6SuccessorV6Response(
        ProviderSimulationPersistenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RawResponseBytes is null || request.RawResponseBytes.Length == 0)
        { throw new InvalidOperationException("Successor-v6 response persistence requires retained response bytes."); }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            long exactRoot = ScalarLong(
                """
                SELECT COUNT(*) FROM m1_slice6_successor_v6_operations operation
                JOIN m1_slice6_successor_v6_budget_events started ON started.operation_id=operation.operation_id
                  AND started.event_kind='possible-start'
                WHERE operation.authorization_id=$authorization AND operation.operation_id=$operation
                  AND operation.provider_attempt_id=$attempt AND operation.request_id=$request
                  AND operation.reservation_id=$reservation AND operation.dispatch_fence_id=$fence;
                """, transaction, ("$authorization", request.AuthorizationId), ("$operation", request.OperationId),
                ("$attempt", request.AttemptId), ("$request", request.RequestId),
                ("$reservation", request.ReservationId), ("$fence", request.DispatchFenceId));
            if (exactRoot != 1) { throw new InvalidOperationException("Successor-v6 response lacks exact started admission."); }
            long reserved = ScalarLong("SELECT reserved_nano_usd FROM m1_slice6_successor_v6_operations "
                + "WHERE operation_id=$operation;", transaction, ("$operation", request.OperationId));
            string rawPayloadId = AdmitCoordinatorPayload(request.RawResponseBytes, "successor-v6-provider-response",
                request.ResponseId, request.OccurredAt, transaction);
            (string rawSha, long rawBytes) = ReadPayloadIdentity(rawPayloadId, transaction);
            string? headersPayloadId = null;
            string? headersSha = null;
            long? headersBytes = null;
            if (request.ResponseHeadersBytes is { Length: > 0 })
            {
                if (request.ResponseHeadersBytes.Length > 65_536)
                { throw new InvalidOperationException("Successor-v6 response headers exceed their retained bound."); }
                headersPayloadId = AdmitCoordinatorPayload(request.ResponseHeadersBytes,
                    "successor-v6-provider-response-headers", request.ResponseId + ":headers",
                    request.OccurredAt, transaction);
                (headersSha, long length) = ReadPayloadIdentity(headersPayloadId, transaction);
                headersBytes = length;
            }
            bool exactUsage = request.Usage.Availability == ProviderAvailabilityState.Available
                && request.Usage.ReceiptState is UsageReceiptState.Complete or UsageReceiptState.FailedKnown
                && ExactM1Slice6SuccessorV6Quantity(request.Usage.DispatchCount)
                && ExactM1Slice6SuccessorV6Quantity(request.Usage.InputTokens)
                && ExactM1Slice6SuccessorV6Quantity(request.Usage.OutputTokens)
                && ExactM1Slice6SuccessorV6Quantity(request.Usage.TotalTokens)
                && ExactM1Slice6SuccessorV6Quantity(request.Usage.ReasoningTokens)
                && ExactM1Slice6SuccessorV6Quantity(request.Usage.CacheReadTokens)
                && ExactM1Slice6SuccessorV6Quantity(request.Usage.CacheWriteTokens)
                && ExactM1Slice6SuccessorV6Quantity(request.Usage.PricedToolCalls)
                && ExactM1Slice6SuccessorV6Quantity(request.Usage.CalculatedNanoUsd);
            long dispatch = M1Slice6SuccessorV6Value(request.Usage.DispatchCount, exactUsage);
            long input = M1Slice6SuccessorV6Value(request.Usage.InputTokens, exactUsage);
            long output = M1Slice6SuccessorV6Value(request.Usage.OutputTokens, exactUsage);
            long total = M1Slice6SuccessorV6Value(request.Usage.TotalTokens, exactUsage);
            long reasoning = M1Slice6SuccessorV6Value(request.Usage.ReasoningTokens, exactUsage);
            long cacheRead = M1Slice6SuccessorV6Value(request.Usage.CacheReadTokens, exactUsage);
            long cacheWrite = M1Slice6SuccessorV6Value(request.Usage.CacheWriteTokens, exactUsage);
            long tools = M1Slice6SuccessorV6Value(request.Usage.PricedToolCalls, exactUsage);
            long cost = M1Slice6SuccessorV6Value(request.Usage.CalculatedNanoUsd, exactUsage);
            string settlementId = request.ResponseId.Replace("-response", "-settlement", StringComparison.Ordinal);
            string replayEdgeId = request.ResponseId + ":replay";
            Execute(
                """
                INSERT INTO m1_slice6_successor_v6_responses(
                  response_record_id,usage_entry_id,settlement_id,replay_edge_id,authorization_id,operation_id,
                  provider_attempt_id,request_id,reservation_id,dispatch_fence_id,response_state,http_status,
                  returned_model,returned_service_tier,error_code,refusal_code,incomplete_reason,
                  provider_response_id,provider_request_id,raw_response_payload_id,raw_response_sha256,
                  raw_response_bytes,response_headers_payload_id,response_headers_sha256,response_headers_bytes,
                  usage_available,dispatch_count,input_tokens,output_tokens,total_tokens,reasoning_tokens,
                  cache_read_tokens,cache_write_tokens,priced_tool_calls,calculated_nano_usd,admitted,created_at)
                VALUES($response,$usage,$settlement,$replay,$authorization,$operation,$attempt,$request,$reservation,$fence,
                  $state,$http,$model,$tier,$error,$refusal,$incomplete,$provider_response,$provider_request,
                  $raw_payload,$raw_sha,$raw_bytes,$headers_payload,$headers_sha,$headers_bytes,$usage_available,
                  $dispatch,$input,$output,$total,$reasoning,$cache_read,$cache_write,$tools,$cost,$admitted,$now);
                INSERT INTO m1_slice6_successor_v6_budget_events(
                  event_id,operation_id,reservation_id,event_kind,reserved_nano_usd,settled_nano_usd,
                  unresolved_nano_usd,released_nano_usd,occurred_at)
                VALUES($settlement,$operation,$reservation,$terminal,0,$settled,$unresolved,$released,$now);
                """, transaction,
                ("$response", request.ResponseId), ("$usage", request.UsageEntryId),
                ("$settlement", settlementId), ("$replay", replayEdgeId),
                ("$authorization", request.AuthorizationId), ("$operation", request.OperationId),
                ("$attempt", request.AttemptId), ("$request", request.RequestId),
                ("$reservation", request.ReservationId), ("$fence", request.DispatchFenceId),
                ("$state", ToResponseState(request.ResponseState)), ("$http", request.HttpStatus),
                ("$model", request.ReturnedModel), ("$tier", request.ReturnedServiceTier),
                ("$error", request.ErrorCode), ("$refusal", request.RefusalCode),
                ("$incomplete", request.IncompleteReason), ("$provider_response", request.ProviderResponseId),
                ("$provider_request", request.ProviderRequestId), ("$raw_payload", rawPayloadId),
                ("$raw_sha", rawSha), ("$raw_bytes", rawBytes), ("$headers_payload", headersPayloadId),
                ("$headers_sha", headersSha), ("$headers_bytes", headersBytes),
                ("$usage_available", exactUsage ? 1 : 0), ("$dispatch", dispatch), ("$input", input),
                ("$output", output), ("$total", total), ("$reasoning", reasoning),
                ("$cache_read", cacheRead), ("$cache_write", cacheWrite), ("$tools", tools),
                ("$cost", cost), ("$admitted", request.Admitted ?? request.ResponseState == ProviderResponseState.Completed ? 1 : 0),
                ("$terminal", exactUsage ? "settled" : "unresolved"),
                ("$settled", exactUsage ? cost : 0), ("$unresolved", exactUsage ? 0 : reserved),
                ("$released", exactUsage ? checked(reserved - cost) : 0),
                ("$now", ToText(request.OccurredAt)));
            transaction.Commit();
            return new(request.ResponseId, request.UsageEntryId, settlementId, replayEdgeId,
                exactUsage ? cost : 0, exactUsage ? 0 : reserved, true);
        }
    }

    public M1Slice6SuccessorV6ReservationReadModel ReadM1Slice6SuccessorV6Reservation(
        string operationId, string authorizationId, string attemptId, string requestId,
        string reservationId, string dispatchFenceId)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT maximum_input_tokens,maximum_output_tokens,reserved_nano_usd,"
                + "coordinator_fencing_epoch,dispatch_deadline_utc FROM m1_slice6_successor_v6_operations "
                + "WHERE operation_id=$operation AND authorization_id=$authorization AND provider_attempt_id=$attempt "
                + "AND request_id=$request AND reservation_id=$reservation AND dispatch_fence_id=$fence;";
            command.Parameters.AddWithValue("$operation", operationId);
            command.Parameters.AddWithValue("$authorization", authorizationId);
            command.Parameters.AddWithValue("$attempt", attemptId);
            command.Parameters.AddWithValue("$request", requestId);
            command.Parameters.AddWithValue("$reservation", reservationId);
            command.Parameters.AddWithValue("$fence", dispatchFenceId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read()) { throw new InvalidDataException("Successor-v6 recovery reservation is absent."); }
            M1Slice6SuccessorV6ReservationReadModel result = new(reader.GetInt64(0), reader.GetInt64(1),
                reader.GetInt64(2), reader.GetInt64(3), DateTimeOffset.Parse(reader.GetString(4),
                    System.Globalization.CultureInfo.InvariantCulture));
            if (reader.Read()) { throw new InvalidDataException("Successor-v6 recovery reservation is ambiguous."); }
            return result;
        }
    }

    private ProviderOperationReadModel ReadM1Slice6SuccessorV6Operation(string operationId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT operation.authorization_id,operation.operation_kind,operation.owner_kind,operation.owner_id,
              operation.canonical_request_sha256,operation.reserved_nano_usd,
              terminal.event_kind,terminal.settled_nano_usd,terminal.unresolved_nano_usd,
              response.response_record_id,response.http_status,response.request_id,response.provider_request_id,
              response.provider_response_id,response.raw_response_payload_id,response.response_headers_payload_id,
              response.replay_edge_id,response.usage_entry_id,response.settlement_id,response.admitted
            FROM m1_slice6_successor_v6_operations operation
            LEFT JOIN m1_slice6_successor_v6_budget_events terminal ON terminal.operation_id=operation.operation_id
              AND terminal.event_kind IN ('released-undispatched','settled','unresolved')
            LEFT JOIN m1_slice6_successor_v6_responses response ON response.operation_id=operation.operation_id
            WHERE operation.operation_id=$operation;
            """;
        command.Parameters.AddWithValue("$operation", operationId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(6))
        { throw new KeyNotFoundException($"Provider operation '{operationId}' does not have terminal successor-v6 evidence."); }
        string terminal = reader.GetString(6);
        bool unresolved = terminal == "unresolved";
        string? rawPayload = reader.IsDBNull(14) ? null : reader.GetString(14);
        string? headersPayload = reader.IsDBNull(15) ? null : reader.GetString(15);
        ProviderOperationReadModel result = new(operationId,
            unresolved ? ProviderOperationState.UnresolvedHold
                : !reader.IsDBNull(19) && reader.GetInt64(19) == 1 ? ProviderOperationState.Settled : ProviderOperationState.Rejected,
            reader.GetInt64(5), reader.GetInt64(7), unresolved, reader.IsDBNull(16) ? "" : "retained-response",
            reader.IsDBNull(9) ? "" : reader.GetString(9), reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
            reader.IsDBNull(11) ? "" : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13), ReadRetainedPayloadBytes(rawPayload),
            ReadRetainedPayloadBytes(headersPayload), reader.IsDBNull(16) ? "" : reader.GetString(16),
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.IsDBNull(17) ? "" : reader.GetString(17),
            reader.IsDBNull(18) ? null : reader.GetString(18));
        if (reader.Read()) { throw new InvalidDataException("Successor-v6 provider operation is ambiguous."); }
        return result;
    }

    private (string Sha256, long Bytes) ReadPayloadIdentity(string payloadId, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT content_sha256,byte_length FROM payloads WHERE payload_id=$payload;";
        command.Parameters.AddWithValue("$payload", payloadId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) { throw new InvalidOperationException("Successor-v6 retained payload identity is absent."); }
        (string, long) result = (reader.GetString(0), reader.GetInt64(1));
        if (reader.Read()) { throw new InvalidOperationException("Successor-v6 retained payload identity is ambiguous."); }
        return result;
    }

    private static bool ExactM1Slice6SuccessorV6Quantity(ProviderQuantityContract quantity) =>
        quantity.Availability == ProviderAvailabilityState.Available && quantity.Value.HasValue;

    private static long M1Slice6SuccessorV6Value(ProviderQuantityContract quantity, bool exact) =>
        exact ? quantity.Value!.Value : 0;
}
