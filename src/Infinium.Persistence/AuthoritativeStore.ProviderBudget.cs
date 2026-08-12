using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed partial class AuthoritativeStore
{
    public void PublishProviderCatalog(
        ProviderCapabilitySnapshotContract capability,
        ProviderPriceSnapshotContract price,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(price);
        if (capability.Provider != price.Provider || capability.Model != price.Model
            || capability.ServiceTier != price.ServiceTier || price.Currency != "USD"
            || capability.Store || capability.Background || capability.Stream
            || capability.PromptCacheMode != "explicit" || capability.HasPromptCacheKey
            || capability.HasPromptCacheBreakpoint || capability.ToolCount != 0)
        {
            throw new InvalidOperationException("Only the exact immutable cache-off M1 provider catalog may be published.");
        }
        foreach (ProviderPriceRuleContract rule in price.Rules)
        {
            if (rule.Provider != price.Provider || rule.Model != price.Model
                || rule.ServiceTier != price.ServiceTier || rule.Currency != price.Currency)
            {
                throw new InvalidOperationException("Every price rule must retain the exact snapshot provider/model/tier/currency identity.");
            }
            _ = ProviderOperationContractInvariants.CalculateComponentNanoUsd(0, rule);
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            Execute(
                """
                INSERT OR IGNORE INTO provider_capability_snapshots VALUES(
                  $id,'openai','gpt-5.6-sol','default','medium','current_turn','standard',0,0,0,'none',0,
                  'disabled','explicit',0,0,$context,$revision,$fingerprint,$now);
                """,
                transaction,
                ("$id", capability.Identity.Value), ("$context", capability.MaximumContextTokens),
                ("$revision", capability.Revision), ("$fingerprint", capability.Fingerprint.Value),
                ("$now", ToText(now)));
            Execute(
                """
                INSERT OR IGNORE INTO provider_price_snapshots VALUES(
                  $id,'openai','gpt-5.6-sol','USD','default',$revision,$fingerprint,$now);
                """,
                transaction,
                ("$id", price.Identity.Value), ("$revision", price.Revision),
                ("$fingerprint", price.Fingerprint.Value), ("$now", ToText(now)));
            foreach (ProviderPriceRuleContract rule in price.Rules)
            {
                Execute(
                    """
                    INSERT OR IGNORE INTO provider_price_rules VALUES(
                      $price,$rule,$context,$cache,$token,$tool,$region,$numerator,$denominator,$revision);
                    """,
                    transaction,
                    ("$price", price.Identity.Value), ("$rule", rule.RuleId.Value),
                    ("$context", rule.ContextBand), ("$cache", rule.CacheClass),
                    ("$token", rule.TokenClass), ("$tool", rule.ToolClass), ("$region", rule.Region),
                    ("$numerator", rule.NumeratorNanoUsd), ("$denominator", rule.DenominatorTokens),
                    ("$revision", rule.Revision));
            }
            EnsureExactProviderCatalog(capability, price, transaction);
            transaction.Commit();
        }
    }

    public void ConfigureProviderBudgetScopes(
        long coordinatorFencingEpoch,
        IReadOnlyList<ProviderBudgetScopeContract> scopes,
        DateTimeOffset now)
    {
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Count == 0 || scopes.Select(scope => (scope.ScopeKind, scope.ScopeId.Value)).Distinct().Count() != scopes.Count)
        {
            throw new InvalidOperationException("Provider budget limits require a finite unique scope set.");
        }
        foreach (ProviderBudgetScopeContract scope in scopes)
        {
            ProviderBudgetContractInvariants.Validate(scope);
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            foreach (ProviderBudgetScopeContract scope in scopes)
            {
                ProviderBudgetVectorContract limit = scope.HardLimit;
                int inserted = Execute(
                    """
                    INSERT OR IGNORE INTO provider_budget_limits(
                        scope_kind,scope_id,dispatch_count,input_tokens,output_tokens,total_tokens,
                        reasoning_tokens,cache_read_tokens,cache_write_tokens,priced_tool_calls,nano_usd,
                        authority_kind,created_at)
                    VALUES($kind,$scope,$dispatch,$input,$output,$total,$reasoning,$cache_read,$cache_write,$tools,$nano,
                        'local-hard-limit',$now);
                    """,
                    transaction,
                    ("$kind", scope.ScopeKind), ("$scope", scope.ScopeId.Value),
                    ("$dispatch", limit.DispatchCount), ("$input", limit.InputTokens),
                    ("$output", limit.OutputTokens), ("$total", limit.TotalTokens),
                    ("$reasoning", limit.ReasoningTokens), ("$cache_read", limit.CacheReadTokens),
                    ("$cache_write", limit.CacheWriteTokens), ("$tools", limit.PricedToolCalls),
                    ("$nano", limit.NanoUsd), ("$now", ToText(now)));
                if (inserted == 0 && ReadBudgetVector("provider_budget_limits", string.Empty, scope.ScopeKind, scope.ScopeId.Value, transaction) != limit)
                {
                    throw new InvalidOperationException("An immutable provider budget scope cannot be redefined.");
                }

                Execute(
                    """
                    INSERT OR IGNORE INTO provider_budget_projection VALUES(
                        $kind,$scope,
                        0,0,0,0,0,0,0,0,0,
                        0,0,0,0,0,0,0,0,0,
                        0,0,0,0,0,0,0,0,0,
                        1,$now);
                    """,
                    transaction,
                    ("$kind", scope.ScopeKind), ("$scope", scope.ScopeId.Value), ("$now", ToText(now)));
            }
            transaction.Commit();
        }
    }

    public ProviderReservationAdmissionContract ReserveProviderBudget(
        long coordinatorFencingEpoch,
        ProviderBudgetReservationRequest request)
        => ReserveProviderBudgetCore(coordinatorFencingEpoch, request, ProviderBudgetFaultPoint.None);

    internal ProviderReservationAdmissionContract ReserveProviderBudgetWithFault(
        long coordinatorFencingEpoch,
        ProviderBudgetReservationRequest request,
        ProviderBudgetFaultPoint faultPoint) =>
        ReserveProviderBudgetCore(coordinatorFencingEpoch, request, faultPoint);

    private ProviderReservationAdmissionContract ReserveProviderBudgetCore(
        long coordinatorFencingEpoch,
        ProviderBudgetReservationRequest request,
        ProviderBudgetFaultPoint faultPoint)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        ProviderReservationAdmissionContract contract = new(
            new OpaqueId(request.ReservationId), new OpaqueId(request.OperationId), new OpaqueId(request.AttemptId),
            new OpaqueId(request.RequestId), request.Reserved, request.Scopes,
            new UtcTimestamp(request.ExpiresAt), new UtcTimestamp(request.CreatedAt));
        ProviderBudgetContractInvariants.Validate(contract);

        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            ProviderBudgetVectorContract authoritativeVector = ReadAuthoritativeReservationVector(
                request.OperationId,
                request.AttemptId,
                request.RequestId,
                coordinatorFencingEpoch,
                transaction);
            if (request.Reserved != authoritativeVector)
            {
                throw new InvalidOperationException(
                    "The caller-declared reservation must equal the authoritative worst-case vector derived from the retained request, operation limits, capability, and price catalog.");
            }
            Dictionary<string, string> expected = ReadExpectedBudgetScopes(
                request.OperationId,
                request.AttemptId,
                request.RequestId,
                coordinatorFencingEpoch,
                request.CreatedAt,
                transaction);
            if (expected.Count != request.Scopes.Count
                || request.Scopes.Any(scope => !expected.TryGetValue(scope.ScopeKind, out string? id)
                    || id != scope.ScopeId.Value))
            {
                throw new InvalidOperationException("The reservation does not bind every exact request/operation/run/profile/account/billing/global scope.");
            }

            foreach (ProviderBudgetScopeContract scope in request.Scopes)
            {
                ProviderBudgetVectorContract storedLimit = ReadBudgetVector(
                    "provider_budget_limits", string.Empty, scope.ScopeKind, scope.ScopeId.Value, transaction);
                if (storedLimit != scope.HardLimit)
                {
                    throw new InvalidOperationException("The reservation scope does not match its immutable local hard limit.");
                }
                (ProviderBudgetVectorContract authoritativeReserved,
                    ProviderBudgetVectorContract authoritativeSettled,
                    ProviderBudgetVectorContract authoritativeUnresolved) = ReplayBudgetEvents(
                        scope.ScopeKind,
                        scope.ScopeId.Value,
                        transaction);
                ProviderBudgetVectorContract committed = ProviderBudgetVectorContract.Add(
                    ProviderBudgetVectorContract.Add(authoritativeReserved, authoritativeSettled),
                    authoritativeUnresolved);
                if (!ProviderBudgetVectorContract.FitsWithin(committed, request.Reserved, storedLimit))
                {
                    throw new InvalidOperationException($"Provider budget scope '{scope.ScopeKind}/{scope.ScopeId.Value}' is exhausted.");
                }
            }

            ProviderBudgetVectorContract vector = request.Reserved;
            Execute(
                """
                INSERT INTO provider_reservations(
                    reservation_id,operation_id,provider_attempt_id,request_id,usage_json,
                    reserved_dispatch_count,reserved_input_tokens,reserved_output_tokens,reserved_reasoning_tokens,
                    reserved_cache_read_tokens,reserved_cache_write_tokens,reserved_priced_tool_calls,maximum_nano_usd,
                    expires_at,created_at)
                VALUES($reservation,$operation,$attempt,$request,
                    json_object('dispatch_count',$dispatch,'input_tokens',$input,'output_tokens',$output,
                        'total_tokens',$total,'reasoning_tokens',$reasoning,'cache_read_tokens',$cache_read,
                        'cache_write_tokens',$cache_write,'priced_tool_calls',$tools,'calculated_nano_usd',$nano),
                    $dispatch,$input,$output,$reasoning,$cache_read,$cache_write,$tools,$nano,$expires,$created);
                """,
                transaction,
                ("$reservation", request.ReservationId), ("$operation", request.OperationId),
                ("$attempt", request.AttemptId), ("$request", request.RequestId),
                ("$dispatch", vector.DispatchCount), ("$input", vector.InputTokens),
                ("$output", vector.OutputTokens), ("$total", vector.TotalTokens),
                ("$reasoning", vector.ReasoningTokens), ("$cache_read", vector.CacheReadTokens),
                ("$cache_write", vector.CacheWriteTokens), ("$tools", vector.PricedToolCalls),
                ("$nano", vector.NanoUsd), ("$expires", ToText(request.ExpiresAt)),
                ("$created", ToText(request.CreatedAt)));

            if (faultPoint == ProviderBudgetFaultPoint.AfterReservationRootBeforeScopeEvents)
            {
                throw new InvalidOperationException("Injected provider budget fault after reservation root and before scope events.");
            }

            foreach (ProviderBudgetScopeContract scope in request.Scopes)
            {
                Execute(
                    """
                    INSERT INTO provider_reservation_scope_items(
                        reservation_scope_item_id,reservation_id,scope_kind,scope_id,usage_json,nano_usd)
                    SELECT $item,$reservation,$kind,$scope,usage_json,maximum_nano_usd
                    FROM provider_reservations WHERE reservation_id=$reservation;
                    """,
                    transaction,
                    ("$item", request.ReservationId + ":" + scope.ScopeKind),
                    ("$reservation", request.ReservationId), ("$kind", scope.ScopeKind),
                    ("$scope", scope.ScopeId.Value));
                InsertBudgetEvent(request.ReservationId, null, scope.ScopeKind, scope.ScopeId.Value,
                    ProviderBudgetEventKind.Reserved, vector, 1, request.CreatedAt, transaction);
                AdvanceProjection(scope.ScopeKind, scope.ScopeId.Value, vector,
                    ProviderBudgetVectorContract.Zero, ProviderBudgetVectorContract.Zero, request.CreatedAt, transaction);
            }
            transaction.Commit();
        }
        return contract;
    }

    public ProviderDispatchGateReceipt AuthorizeProviderDispatch(ProviderDispatchGateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        RequirePositive(request.CoordinatorFencingEpoch, nameof(request.CoordinatorFencingEpoch));
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            EnsureCurrentCoordinatorEpoch(request.CoordinatorFencingEpoch, transaction);
            string? priorEffective = ScalarStringOrNull(
                "SELECT value FROM store_metadata WHERE key='provider_effective_gate_time';",
                transaction);
            if (priorEffective is not null
                && request.EvaluatedAt < DateTimeOffset.Parse(priorEffective, System.Globalization.CultureInfo.InvariantCulture))
            {
                throw new InvalidOperationException("Material clock rollback blocks provider dispatch.");
            }

            long eligible = ScalarLong(
                """
                SELECT COUNT(*)
                FROM provider_operation_authorizations a
                JOIN provider_reservations r ON r.operation_id=a.operation_id
                JOIN provider_operation_attempts attempt
                  ON attempt.operation_id=a.operation_id AND attempt.provider_attempt_id=r.provider_attempt_id
                JOIN provider_requests request
                  ON request.operation_id=a.operation_id AND request.provider_attempt_id=r.provider_attempt_id
                 AND request.request_id=r.request_id
                JOIN provider_profile_projection profile
                  ON profile.profile_id=a.profile_id AND profile.generation_id=a.generation_id
                 AND profile.revocation_epoch=a.revocation_epoch
                WHERE a.authorization_id=$authorization AND a.operation_id=$operation
                  AND r.reservation_id=$reservation AND r.provider_attempt_id=$attempt AND r.request_id=$request
                  AND a.profile_id=$profile AND a.generation_id=$generation AND a.revocation_epoch=$revocation
                  AND a.coordinator_fencing_epoch=$epoch AND attempt.coordinator_fencing_epoch=$epoch
                  AND a.execution_mode='simulated-nonnetwork'
                  AND profile.lifecycle_state='active-verified' AND profile.verification_state='available'
                  AND $evaluated >= a.confirmed_at AND $evaluated < a.dispatch_deadline_utc
                  AND $evaluated < r.expires_at
                  AND NOT EXISTS(SELECT 1 FROM provider_dispatch_fences f WHERE f.operation_id=a.operation_id)
                  AND NOT EXISTS(SELECT 1 FROM provider_transport_events e WHERE e.operation_id=a.operation_id)
                  AND NOT EXISTS(SELECT 1 FROM provider_settlements s WHERE s.operation_id=a.operation_id)
                  AND ((a.owner_kind='analysis-run' AND EXISTS(
                        SELECT 1 FROM runs run JOIN job_nodes node ON node.run_id=run.run_id
                        WHERE run.run_id=a.owner_id AND node.job_node_id=a.job_node_id
                          AND run.lifecycle_state IN ('Running','Waiting')
                          AND node.lifecycle_state IN ('Running','Waiting')))
                    OR (a.owner_kind='evidence-acquisition-run' AND EXISTS(
                        SELECT 1 FROM evidence_acquisition_runs run
                        JOIN evidence_acquisition_job_nodes node ON node.acquisition_run_id=run.acquisition_run_id
                        WHERE run.acquisition_run_id=a.owner_id AND node.acquisition_job_node_id=a.job_node_id
                          AND lower(run.lifecycle_state) IN ('running','waiting')
                          AND lower(node.lifecycle_state) IN ('running','waiting'))));
                """,
                transaction,
                ("$authorization", request.AuthorizationId), ("$operation", request.OperationId),
                ("$reservation", request.ReservationId), ("$attempt", request.AttemptId),
                ("$request", request.RequestId), ("$profile", request.ProfileId),
                ("$generation", request.GenerationId), ("$revocation", request.RevocationEpoch),
                ("$epoch", request.CoordinatorFencingEpoch), ("$evaluated", ToText(request.EvaluatedAt)));
            if (eligible != 1)
            {
                throw new InvalidOperationException("The immediate final dispatch gate rejected a stale, ineligible, expired, paused, cancelled, deleted, or prior-start operation.");
            }

            string deadline = ScalarString(
                "SELECT dispatch_deadline_utc FROM provider_operation_authorizations WHERE authorization_id=$id;",
                transaction,
                ("$id", request.AuthorizationId));
            Execute(
                """
                INSERT INTO provider_dispatch_fences(
                    dispatch_fence_id,authorization_id,operation_id,reservation_id,request_id,provider_attempt_id,
                    coordinator_fencing_epoch,profile_id,generation_id,revocation_epoch,authorized,decision_reason,evaluated_at)
                VALUES($fence,$authorization,$operation,$reservation,$request,$attempt,$epoch,$profile,$generation,$revocation,
                    1,'exact-final-gate-authorized',$evaluated);
                """,
                transaction,
                ("$fence", request.DispatchFenceId), ("$authorization", request.AuthorizationId),
                ("$operation", request.OperationId), ("$reservation", request.ReservationId),
                ("$request", request.RequestId), ("$attempt", request.AttemptId),
                ("$epoch", request.CoordinatorFencingEpoch), ("$profile", request.ProfileId),
                ("$generation", request.GenerationId), ("$revocation", request.RevocationEpoch),
                ("$evaluated", ToText(request.EvaluatedAt)));
            Execute(
                """
                INSERT INTO store_metadata(key,value) VALUES('provider_effective_gate_time',$evaluated)
                ON CONFLICT(key) DO UPDATE SET value=excluded.value;
                """,
                transaction,
                ("$evaluated", ToText(request.EvaluatedAt)));
            transaction.Commit();
            return new(request.DispatchFenceId, request.ReservationId, request.CoordinatorFencingEpoch,
                request.EvaluatedAt, DateTimeOffset.Parse(deadline, System.Globalization.CultureInfo.InvariantCulture),
                true, "exact-final-gate-authorized");
        }
    }

    public ProviderDispatchAuthoritySnapshot ReadCurrentProviderDispatchRequest(
        string dispatchFenceId,
        string operationId,
        string reservationId,
        string attemptId,
        string requestId,
        DateTimeOffset evaluatedAt)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT a.authorization_id,a.profile_id,a.generation_id,a.revocation_epoch,a.coordinator_fencing_epoch,
                       profile.account_identity_id,profile.billing_scope_identity_id,generation.generation_ordinal,
                       a.operation_kind,a.effective_configuration_id,a.capability_snapshot_id,a.price_snapshot_id,
                       request.request_fingerprint,request.canonical_request_fingerprint,request.payload_bytes,
                       request.settings_fingerprint,request.output_schema_fingerprint,request.input_bound_policy_id,
                       request.input_bound_policy_version,request.input_bound_proof_status,a.maximum_request_bytes,
                       a.maximum_input_tokens,a.maximum_output_tokens,a.maximum_raw_response_bytes,
                       a.maximum_dispatch_count,a.maximum_calculated_nano_usd,a.deadline_milliseconds,
                       a.dispatch_deadline_utc,a.confirmed_at
                FROM provider_operation_authorizations a
                JOIN provider_reservations r ON r.operation_id=a.operation_id
                JOIN provider_requests request
                  ON request.operation_id=a.operation_id AND request.provider_attempt_id=r.provider_attempt_id
                 AND request.request_id=r.request_id
                JOIN provider_profile_projection profile
                  ON profile.profile_id=a.profile_id AND profile.generation_id=a.generation_id
                 AND profile.revocation_epoch=a.revocation_epoch
                JOIN provider_generations generation
                  ON generation.profile_id=a.profile_id AND generation.generation_id=a.generation_id
                WHERE a.operation_id=$operation AND r.reservation_id=$reservation
                  AND r.provider_attempt_id=$attempt AND r.request_id=$request;
                """;
            command.Parameters.AddWithValue("$operation", operationId);
            command.Parameters.AddWithValue("$reservation", reservationId);
            command.Parameters.AddWithValue("$attempt", attemptId);
            command.Parameters.AddWithValue("$request", requestId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException("The authoritative provider dispatch root is absent.");
            }
            ProviderDispatchGateRequest request = new(
                dispatchFenceId,
                reader.GetString(0),
                operationId,
                reservationId,
                attemptId,
                requestId,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                evaluatedAt);
            ProviderDispatchAuthoritySnapshot result = new(
                request,
                reader.GetString(5), reader.GetString(6), reader.GetInt64(7), reader.GetString(8),
                reader.GetString(9), reader.GetString(10), reader.GetString(11), reader.GetString(12),
                reader.GetString(13), reader.GetInt64(14), reader.GetString(15), reader.GetString(16),
                reader.GetString(17), reader.GetString(18), reader.GetString(19), reader.GetInt64(20),
                reader.GetInt64(21), reader.GetInt64(22), reader.GetInt64(23), reader.GetInt64(24),
                reader.GetInt64(25), reader.GetInt64(26),
                DateTimeOffset.Parse(reader.GetString(27), System.Globalization.CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(reader.GetString(28), System.Globalization.CultureInfo.InvariantCulture));
            if (reader.Read())
            {
                throw new InvalidOperationException("The authoritative provider dispatch root is ambiguous.");
            }
            reader.Close();
            return result;
        }
    }

    public void RecordProviderTransportStart(
        string operationId,
        string attemptId,
        string requestId,
        string dispatchFenceId,
        bool ambiguous,
        DateTimeOffset occurredAt)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            Execute(
                """
                INSERT INTO provider_transport_events VALUES($not_started,$operation,$attempt,$request,$fence,'not-started',1,$now);
                INSERT INTO provider_transport_events VALUES($started,$operation,$attempt,$request,$fence,$kind,2,$now);
                """,
                transaction,
                ("$not_started", dispatchFenceId + ":not-started"), ("$started", dispatchFenceId + ":start"),
                ("$operation", operationId), ("$attempt", attemptId), ("$request", requestId),
                ("$fence", dispatchFenceId), ("$kind", ambiguous ? "may-have-started" : "started"),
                ("$now", ToText(occurredAt)));
            transaction.Commit();
        }
    }

    public ProviderSimulationPersistenceReceipt PersistProviderSimulation(
        ProviderSimulationPersistenceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Usage);
        ArgumentNullException.ThrowIfNull(request.RateFacts);
        string responseState = ToResponseState(request.ResponseState);
        bool undispatched = request.ResponseState == ProviderResponseState.Cancelled
            && request.RawResponseBytes is null;
        bool oversized = request.ResponseState == ProviderResponseState.Oversized;
        if (request.ResponseState == ProviderResponseState.Unknown && request.RawResponseBytes is null)
        {
            throw new InvalidOperationException(
                "An ambiguous transport start is retained as an unresolved hold without inventing a provider response or usage receipt.");
        }
        if (!undispatched && !oversized && (request.RawResponseBytes is null || request.RawResponseBytes.Length == 0))
        {
            throw new InvalidOperationException("A staged deterministic provider response requires retained raw response bytes.");
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            long exactRoot = ScalarLong(
                """
                SELECT COUNT(*) FROM provider_dispatch_fences fence
                JOIN provider_reservations reservation ON reservation.reservation_id=fence.reservation_id
                WHERE fence.dispatch_fence_id=$fence AND fence.authorization_id=$authorization
                  AND fence.operation_id=$operation AND fence.reservation_id=$reservation
                  AND fence.provider_attempt_id=$attempt AND fence.request_id=$request;
                """,
                transaction,
                ("$fence", request.DispatchFenceId), ("$authorization", request.AuthorizationId),
                ("$operation", request.OperationId), ("$reservation", request.ReservationId),
                ("$attempt", request.AttemptId), ("$request", request.RequestId));
            if (exactRoot != 1)
            {
                throw new InvalidOperationException(
                    "The simulated response must bind exactly to its reservation operation/attempt/request/fence root.");
            }

            string? payloadId = null;
            string? payloadFingerprint = null;
            long? payloadLength = null;
            if (!undispatched && !oversized)
            {
                payloadId = AdmitCoordinatorPayload(
                    request.RawResponseBytes!, "provider-response", request.ResponseId, request.OccurredAt, transaction);
                using SqliteCommand payload = connection.CreateCommand();
                payload.Transaction = transaction;
                payload.CommandText = "SELECT content_sha256,byte_length FROM payloads WHERE payload_id=$payload;";
                payload.Parameters.AddWithValue("$payload", payloadId);
                using SqliteDataReader payloadReader = payload.ExecuteReader();
                if (!payloadReader.Read())
                {
                    throw new InvalidOperationException("The simulated response payload was not retained.");
                }
                payloadFingerprint = payloadReader.GetString(0);
                payloadLength = payloadReader.GetInt64(1);
            }

            string? headersPayloadId = null;
            string? headersFingerprint = null;
            long? headersLength = null;
            if (request.ResponseHeadersBytes is { Length: > 0 })
            {
                if (request.ResponseHeadersBytes.Length > 65_536)
                {
                    throw new InvalidOperationException("The allowlisted provider response-header receipt is oversized.");
                }
                headersPayloadId = AdmitCoordinatorPayload(
                    request.ResponseHeadersBytes, "provider-response-headers", request.ResponseId + ":headers",
                    request.OccurredAt, transaction);
                using SqliteCommand headersPayload = connection.CreateCommand();
                headersPayload.Transaction = transaction;
                headersPayload.CommandText = "SELECT content_sha256,byte_length FROM payloads WHERE payload_id=$payload;";
                headersPayload.Parameters.AddWithValue("$payload", headersPayloadId);
                using SqliteDataReader headersReader = headersPayload.ExecuteReader();
                if (!headersReader.Read())
                {
                    throw new InvalidOperationException("The provider response-header receipt was not retained.");
                }
                headersFingerprint = headersReader.GetString(0);
                headersLength = headersReader.GetInt64(1);
            }

            if (!undispatched)
            {
                Execute(
                    """
                    INSERT INTO provider_transport_events VALUES(
                      $event,$operation,$attempt,$request,$fence,'response-staged',3,$now);
                    """,
                    transaction,
                    ("$event", request.DispatchFenceId + ":response"), ("$operation", request.OperationId),
                    ("$attempt", request.AttemptId), ("$request", request.RequestId),
                    ("$fence", request.DispatchFenceId), ("$now", ToText(request.OccurredAt)));
            }

            string availability = undispatched ? "unavailable" : "available";
            string usageAvailability = request.Usage.Availability == ProviderAvailabilityState.Available
                ? "available" : "unavailable";
            string returnedModelAvailability = request.ReturnedModel is null ? "unavailable" : "available";
            string returnedTierAvailability = request.ReturnedServiceTier is null ? "unavailable" : "available";
            bool admitted = request.Admitted ?? request.ResponseState == ProviderResponseState.Completed;
            string validation = request.ResponseState == ProviderResponseState.Completed
                ? "proposed"
                : "rejected";
            string rateAvailability = request.RateFacts.Count == 0 ? "unavailable" : "available";
            Execute(
                """
                INSERT INTO provider_responses(
                  response_record_id,availability,usage_availability,authorization_id,operation_id,owner_kind,owner_id,
                  request_id,provider_attempt_id,reservation_id,dispatch_fence_id,operation_kind,maximum_input_tokens,
                  maximum_output_tokens,maximum_calculated_nano_usd,raw_response_availability,raw_response_payload_id,
                  raw_response_fingerprint,raw_response_bytes,maximum_raw_response_bytes,overflow_observed_excess_bytes,
                  response_headers_payload_id,response_headers_fingerprint,response_headers_bytes,response_headers_availability,
                  http_status_availability,http_status,provider_response_id_availability,provider_response_id,
                  client_request_id,client_request_id_availability,provider_request_id_availability,provider_request_id,
                  billing_evidence_availability,response_state,refusal_availability,refusal_code,incomplete_availability,
                  incomplete_reason,error_availability,error_code,requested_model,returned_model,returned_model_availability,
                  requested_service_tier,returned_service_tier,returned_service_tier_availability,reasoning_context,
                  reasoning_mode,prompt_cache_mode,billing_availability,rate_availability,expected_rate_limit_fact_count,
                  credit_availability,validation_state,admission_state,created_at)
                SELECT $response,$availability,$usage_availability,$authorization,a.operation_id,a.owner_kind,a.owner_id,
                  request.request_id,attempt.provider_attempt_id,reservation.reservation_id,$response_fence,a.operation_kind,
                  a.maximum_input_tokens,a.maximum_output_tokens,a.maximum_calculated_nano_usd,$raw_availability,$payload,
                  $payload_fingerprint,$payload_bytes,a.maximum_raw_response_bytes,$overflow,$headers_payload,
                  $headers_fingerprint,$headers_bytes,$headers_availability,$http_availability,$http,
                  $provider_response_availability,$provider_response,request.client_request_id,$client_availability,
                  $provider_request_availability,$provider_request,'unavailable',$state,
                  $refusal_availability,$refusal,$incomplete_availability,$incomplete,$error_availability,$error,
                  'gpt-5.6-sol',$returned_model,$returned_model_availability,'default',$returned_tier,
                  $returned_tier_availability,'current_turn','standard','explicit','unavailable',$rate_availability,$rate_count,
                  'unavailable',$validation,$validation,$now
                FROM provider_operation_authorizations a
                JOIN provider_operation_attempts attempt ON attempt.operation_id=a.operation_id
                JOIN provider_requests request
                  ON request.operation_id=a.operation_id AND request.provider_attempt_id=attempt.provider_attempt_id
                JOIN provider_reservations reservation
                  ON reservation.operation_id=a.operation_id AND reservation.provider_attempt_id=attempt.provider_attempt_id
                 AND reservation.request_id=request.request_id
                WHERE a.authorization_id=$authorization AND a.operation_id=$operation
                  AND attempt.provider_attempt_id=$attempt AND request.request_id=$request
                  AND reservation.reservation_id=$reservation;
                """,
                transaction,
                ("$response", request.ResponseId), ("$availability", availability),
                ("$usage_availability", usageAvailability),
                ("$authorization", request.AuthorizationId), ("$response_fence", undispatched ? null : request.DispatchFenceId),
                ("$raw_availability", undispatched || oversized ? "unavailable" : "available"),
                ("$payload", payloadId), ("$payload_fingerprint", payloadFingerprint), ("$payload_bytes", payloadLength),
                ("$overflow", oversized ? 1 : null), ("$http_availability", undispatched ? "unavailable" : "available"),
                ("$http", undispatched ? null : request.HttpStatus),
                ("$headers_payload", headersPayloadId), ("$headers_fingerprint", headersFingerprint),
                ("$headers_bytes", headersLength),
                ("$headers_availability", headersPayloadId is null ? "unavailable" : "available"),
                ("$provider_response_availability", request.ProviderResponseId is null ? "unavailable" : "available"),
                ("$provider_response", request.ProviderResponseId),
                ("$client_availability", undispatched ? "unavailable" : "available"), ("$state", responseState),
                ("$provider_request_availability", request.ProviderRequestId is null ? "unavailable" : "available"),
                ("$provider_request", request.ProviderRequestId),
                ("$refusal_availability", request.RefusalCode is null ? "unavailable" : "available"),
                ("$refusal", request.RefusalCode),
                ("$incomplete_availability", request.IncompleteReason is null ? "unavailable" : "available"),
                ("$incomplete", request.IncompleteReason),
                ("$error_availability", request.ErrorCode is null ? "unavailable" : "available"),
                ("$error", request.ErrorCode), ("$returned_model", request.ReturnedModel),
                ("$returned_model_availability", undispatched ? "unavailable" : returnedModelAvailability),
                ("$returned_tier", request.ReturnedServiceTier),
                ("$returned_tier_availability", undispatched ? "unavailable" : returnedTierAvailability),
                ("$rate_availability", rateAvailability), ("$rate_count", request.RateFacts.Count),
                ("$validation", validation), ("$now", ToText(request.OccurredAt)),
                ("$operation", request.OperationId), ("$attempt", request.AttemptId),
                ("$request", request.RequestId), ("$reservation", request.ReservationId));

            ProviderBudgetVectorContract actual = undispatched
                || request.Usage.Availability != ProviderAvailabilityState.Available
                || request.Usage.ReceiptState != UsageReceiptState.Complete
                ? ProviderBudgetVectorContract.Zero
                : ToAvailableBudgetVector(request.Usage);
            InsertProviderUsage(request, undispatched, rateAvailability, transaction);
            for (int index = 0; index < request.RateFacts.Count; index++)
            {
                ProviderRateLimitFactContract fact = request.RateFacts[index];
                Execute(
                    """
                    INSERT INTO provider_rate_limit_facts VALUES(
                      $id,$usage,$scope,$dimension,$availability,$limit,$remaining,$observed,$reset);
                    """,
                    transaction,
                    ("$id", request.UsageEntryId + ":rate:" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("$usage", request.UsageEntryId), ("$scope", fact.Scope), ("$dimension", fact.Dimension),
                    ("$availability", ToAvailability(fact.Availability)), ("$limit", fact.Limit),
                    ("$remaining", fact.Remaining), ("$observed", ToText(fact.ObservedAt.Value)),
                    ("$reset", fact.ResetsAt is null ? null : ToText(fact.ResetsAt.Value)));
            }
            Execute(
                """
                INSERT INTO provider_response_finalizations VALUES(
                  $finalization,$response,$usage,$validation,$validation,$now);
                """,
                transaction,
                ("$finalization", request.FinalizationId), ("$response", request.ResponseId),
                ("$usage", request.UsageEntryId),
                ("$validation", admitted ? "admitted" : "rejected"),
                ("$now", ToText(request.OccurredAt)));
            if (!undispatched)
            {
                Execute(
                    """
                    INSERT INTO provider_replay_edges(
                      replay_edge_id,operation_id,provider_attempt_id,request_id,response_record_id,
                      dispatch_fence_id,replay_state,dependency_manifest_id,effective_configuration_id,created_at)
                    SELECT $edge,a.operation_id,$attempt,$request,$response,$fence,'retained-response',
                      a.resolved_input_manifest_id,a.effective_configuration_id,$now
                    FROM provider_operation_authorizations a
                    WHERE a.authorization_id=$authorization AND a.operation_id=$operation;
                    """,
                    transaction,
                    ("$edge", request.ResponseId + ":replay"), ("$attempt", request.AttemptId),
                    ("$request", request.RequestId), ("$response", request.ResponseId),
                    ("$fence", request.DispatchFenceId), ("$now", ToText(request.OccurredAt)),
                    ("$authorization", request.AuthorizationId), ("$operation", request.OperationId));
            }
            transaction.Commit();

            ProviderBudgetVectorContract reserved = ReadReservationVectorOutsideTransaction(request.ReservationId);
            ProviderBudgetEventKind kind = undispatched
                ? ProviderBudgetEventKind.ReleasedUndispatched
                : request.Usage.Availability != ProviderAvailabilityState.Available
                    ? ProviderBudgetEventKind.RetainedUnavailable
                : !ProviderBudgetVectorContract.FitsWithin(ProviderBudgetVectorContract.Zero, actual, reserved)
                    ? ProviderBudgetEventKind.SettledOverrun
                    : request.Usage.ReceiptState switch
                    {
                        UsageReceiptState.Complete => ProviderBudgetEventKind.SettledComplete,
                        UsageReceiptState.FailedKnown => ProviderBudgetEventKind.SettledFailedKnown,
                        UsageReceiptState.Partial => ProviderBudgetEventKind.RetainedPartial,
                        _ => ProviderBudgetEventKind.RetainedUnavailable,
                    };
            return new(request.ResponseId, request.UsageEntryId, actual, kind);
        }
    }

    public ProviderBudgetSettlementReceipt SettleProviderBudget(ProviderBudgetSettlementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Kind is ProviderBudgetEventKind.Unspecified or ProviderBudgetEventKind.Reserved or ProviderBudgetEventKind.Adjustment)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            ProviderBudgetVectorContract reserved = ReadReservationVector(request.ReservationId, transaction);
            IReadOnlyList<(string Kind, string Id)> scopes = ReadReservationScopes(request.ReservationId, transaction);
            // Transport events bind through the dispatch fence; this query keeps the reservation identity exact.
            long transportStarts = ScalarLong(
                """
                SELECT COUNT(*) FROM provider_transport_events e
                JOIN provider_dispatch_fences f ON f.dispatch_fence_id=e.dispatch_fence_id
                WHERE f.reservation_id=$reservation AND e.event_kind IN ('started','may-have-started');
                """,
                transaction,
                ("$reservation", request.ReservationId));
            bool hasStart = transportStarts > 0;
            string latestCausalTime = ScalarString(
                """
                SELECT MAX(causal_time) FROM (
                  SELECT created_at AS causal_time FROM provider_reservations WHERE reservation_id=$reservation
                  UNION ALL
                  SELECT evaluated_at FROM provider_dispatch_fences WHERE reservation_id=$reservation
                  UNION ALL
                  SELECT e.occurred_at FROM provider_transport_events e
                    JOIN provider_dispatch_fences f ON f.dispatch_fence_id=e.dispatch_fence_id
                    WHERE f.reservation_id=$reservation
                  UNION ALL
                  SELECT u.created_at FROM provider_usage_entries u
                    JOIN provider_reservations r
                      ON r.operation_id=u.operation_id AND r.provider_attempt_id=u.provider_attempt_id
                     AND r.request_id=u.request_id
                    WHERE r.reservation_id=$reservation AND u.usage_entry_id=$usage
                );
                """,
                transaction,
                ("$reservation", request.ReservationId), ("$usage", request.UsageEntryId));
            if (request.OccurredAt < DateTimeOffset.Parse(
                    latestCausalTime,
                    System.Globalization.CultureInfo.InvariantCulture))
            {
                throw new InvalidOperationException(
                    "Provider settlement cannot be causally backdated before its reservation, fence, transport, or usage evidence.");
            }
            long prior = ScalarLong(
                "SELECT COUNT(*) FROM provider_budget_events WHERE reservation_id=$reservation AND event_kind<>'reserved';",
                transaction,
                ("$reservation", request.ReservationId));
            if (prior != 0)
            {
                throw new InvalidOperationException("A provider reservation may be settled exactly once.");
            }

            ProviderBudgetVectorContract actual = request.Actual ?? ProviderBudgetVectorContract.Zero;
            ProviderBudgetVectorContract released;
            ProviderBudgetVectorContract settled;
            ProviderBudgetVectorContract unresolved;
            bool retry;
            switch (request.Kind)
            {
                case ProviderBudgetEventKind.ReleasedUndispatched when !hasStart && request.Actual is null:
                    if (request.UsageEntryId is not null)
                    {
                        EnsureUndispatchedUsage(request.ReservationId, request.UsageEntryId, transaction);
                    }
                    released = reserved;
                    settled = ProviderBudgetVectorContract.Zero;
                    unresolved = ProviderBudgetVectorContract.Zero;
                    retry = false;
                    break;
                case ProviderBudgetEventKind.RetainedAmbiguous
                    when hasStart && request.UsageEntryId is null && request.Actual is null:
                    released = ProviderBudgetVectorContract.Zero;
                    settled = ProviderBudgetVectorContract.Zero;
                    unresolved = reserved;
                    retry = false;
                    break;
                case ProviderBudgetEventKind.RetainedPartial or ProviderBudgetEventKind.RetainedUnavailable
                    when hasStart && ((request.UsageEntryId is null && request.Actual is null)
                        || (request.UsageEntryId is not null && request.Actual is not null)):
                    if (request.UsageEntryId is not null)
                    {
                        EnsureActualUsage(request.ReservationId, request.UsageEntryId, actual, transaction);
                    }
                    released = ProviderBudgetVectorContract.Zero;
                    settled = ProviderBudgetVectorContract.Zero;
                    unresolved = reserved;
                    retry = false;
                    break;
                case ProviderBudgetEventKind.SettledComplete or ProviderBudgetEventKind.SettledFailedKnown
                    when hasStart && request.UsageEntryId is not null && request.Actual is not null:
                    EnsureActualUsage(request.ReservationId, request.UsageEntryId, actual, transaction);
                    if (!ProviderBudgetVectorContract.FitsWithin(ProviderBudgetVectorContract.Zero, actual, reserved))
                    {
                        throw new InvalidOperationException("Usage above reservation must be classified as overrun.");
                    }
                    released = reserved;
                    settled = actual;
                    unresolved = ProviderBudgetVectorContract.Zero;
                    retry = false;
                    break;
                case ProviderBudgetEventKind.SettledOverrun
                    when hasStart && request.UsageEntryId is not null && request.Actual is not null:
                    EnsureActualUsage(request.ReservationId, request.UsageEntryId, actual, transaction);
                    if (ProviderBudgetVectorContract.FitsWithin(ProviderBudgetVectorContract.Zero, actual, reserved))
                    {
                        throw new InvalidOperationException("An overrun settlement must exceed at least one reserved dimension.");
                    }
                    released = reserved;
                    settled = actual;
                    unresolved = ProviderBudgetVectorContract.Zero;
                    retry = false;
                    break;
                default:
                    throw new InvalidOperationException("Provider settlement state contradicts transport certainty or usage availability.");
            }

            Execute(
                """
                INSERT INTO provider_budget_settlement_receipts(
                    settlement_id,reservation_id,event_kind,usage_entry_id,retry_permitted,created_at)
                VALUES($settlement,$reservation,$kind,$usage,0,$now);
                """,
                transaction,
                ("$settlement", request.SettlementId), ("$reservation", request.ReservationId),
                ("$kind", ToEventKind(request.Kind)), ("$usage", request.UsageEntryId),
                ("$now", ToText(request.OccurredAt)));

            if (request.Kind != ProviderBudgetEventKind.ReleasedUndispatched || request.UsageEntryId is not null)
            {
                string state = request.Kind switch
                {
                    ProviderBudgetEventKind.SettledComplete or ProviderBudgetEventKind.ReleasedUndispatched => "settled",
                    ProviderBudgetEventKind.SettledFailedKnown => "failed-known",
                    ProviderBudgetEventKind.SettledOverrun => "overrun",
                    _ => "unresolved-hold",
                };
                Execute(
                    """
                    INSERT INTO provider_settlements(
                      settlement_id,operation_id,provider_attempt_id,request_id,reservation_id,usage_entry_id,
                      dispatch_fence_id,state,released_nano_usd,retained_hold_nano_usd,created_at)
                    SELECT $settlement,r.operation_id,r.provider_attempt_id,r.request_id,r.reservation_id,$usage,
                      CASE WHEN $state='settled' AND $usage IS NOT NULL AND NOT EXISTS(
                        SELECT 1 FROM provider_transport_events e
                        JOIN provider_dispatch_fences f ON f.dispatch_fence_id=e.dispatch_fence_id
                        WHERE f.reservation_id=r.reservation_id) THEN NULL
                        ELSE (SELECT dispatch_fence_id FROM provider_dispatch_fences f
                              WHERE f.reservation_id=r.reservation_id) END,
                      $state,$released,$retained,$now
                    FROM provider_reservations r WHERE r.reservation_id=$reservation;
                    """,
                    transaction,
                    ("$settlement", request.SettlementId), ("$usage", request.UsageEntryId),
                    ("$state", state), ("$released", state == "unresolved-hold" ? 0 : reserved.NanoUsd),
                    ("$retained", state == "unresolved-hold" ? reserved.NanoUsd : 0),
                    ("$now", ToText(request.OccurredAt)), ("$reservation", request.ReservationId));
            }

            string ownerKind = ScalarString(
                """
                SELECT a.owner_kind FROM provider_reservations r
                JOIN provider_operation_authorizations a ON a.operation_id=r.operation_id
                WHERE r.reservation_id=$reservation;
                """,
                transaction,
                ("$reservation", request.ReservationId));
            long dispatchSequenceCutoff = ScalarLong(
                """
                SELECT COALESCE(MAX(e.sequence),0) FROM provider_transport_events e
                JOIN provider_dispatch_fences f ON f.dispatch_fence_id=e.dispatch_fence_id
                WHERE f.reservation_id=$reservation AND e.event_kind IN ('started','may-have-started');
                """,
                transaction,
                ("$reservation", request.ReservationId));

            foreach ((string kind, string id) in scopes)
            {
                ProviderBudgetVectorContract eventVector = request.Kind switch
                {
                    ProviderBudgetEventKind.SettledComplete or ProviderBudgetEventKind.SettledFailedKnown
                        or ProviderBudgetEventKind.SettledOverrun => settled,
                    _ => reserved,
                };
                InsertBudgetEvent(request.ReservationId, request.UsageEntryId, kind, id, request.Kind,
                    eventVector, 2, request.OccurredAt, transaction);
                AdvanceProjection(kind, id, ProviderBudgetVectorContract.Zero,
                    settled, unresolved, request.OccurredAt, transaction, reserved);
                if (request.UsageEntryId is not null)
                {
                    bool attachedAnalysisRollup = ownerKind == "evidence-acquisition-run" && kind == "analysis-run";
                    string attribution = kind == ownerKind
                        ? "owner"
                        : attachedAnalysisRollup ? "attached-pre-cutoff" : "non-owning-rollup";
                    Execute(
                        """
                        INSERT INTO provider_usage_rollup_references(
                            usage_entry_id,scope_kind,scope_id,attribution_kind,dispatch_sequence_cutoff,created_at)
                        VALUES($usage,$kind,$scope,$attribution,$cutoff,$now);
                        """,
                        transaction,
                        ("$usage", request.UsageEntryId), ("$kind", kind), ("$scope", id),
                        ("$attribution", attribution),
                        ("$cutoff", attachedAnalysisRollup ? dispatchSequenceCutoff : null),
                        ("$now", ToText(request.OccurredAt)));
                }
            }
            transaction.Commit();
            return new(request.SettlementId, request.ReservationId, request.Kind, released, settled, unresolved, retry);
        }
    }

    public ProviderRunOutputV2BindingReceipt BindProviderRunOutputV2(
        string runId,
        string effectiveConfigurationV2Id,
        byte[] localRunOutputV1Bytes,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectiveConfigurationV2Id);
        ArgumentNullException.ThrowIfNull(localRunOutputV1Bytes);
        if (localRunOutputV1Bytes.Length == 0)
        {
            throw new InvalidOperationException("A run-output v2 binding requires the exact non-empty local v1 bytes.");
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            string payloadId = AdmitCoordinatorPayload(
                localRunOutputV1Bytes, "local-run-output-v1", runId + ":run-output-v1", createdAt, transaction);
            string fingerprint = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(localRunOutputV1Bytes));
            Execute(
                """
                INSERT INTO provider_run_output_v2_bindings(
                  run_id,effective_configuration_v2_id,local_run_output_v1_payload_id,
                  local_run_output_v1_fingerprint,local_run_output_v1_bytes,created_at)
                VALUES($run,$configuration,$payload,$fingerprint,$bytes,$now);
                """,
                transaction,
                ("$run", runId), ("$configuration", effectiveConfigurationV2Id), ("$payload", payloadId),
                ("$fingerprint", fingerprint), ("$bytes", localRunOutputV1Bytes.LongLength),
                ("$now", ToText(createdAt)));
            transaction.Commit();
            return new(runId, effectiveConfigurationV2Id, payloadId, fingerprint, localRunOutputV1Bytes.LongLength);
        }
    }

    public ProviderOperationReadModel ReadProviderOperation(string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT r.response_record_id,r.http_status,r.client_request_id,r.provider_request_id,
                  r.provider_response_id,r.raw_response_payload_id,r.response_headers_payload_id,
                  reservation.maximum_nano_usd,usage.calculated_nano_usd,
                  finalization.admission_state,event.event_kind,replay.replay_state,replay.replay_edge_id,
                  r.authorization_id,r.operation_kind,replay.effective_configuration_id,usage.usage_entry_id,
                  settlement.settlement_id
                FROM provider_responses r
                JOIN provider_reservations reservation ON reservation.operation_id=r.operation_id
                  AND reservation.provider_attempt_id=r.provider_attempt_id AND reservation.request_id=r.request_id
                JOIN provider_usage_entries usage ON usage.response_record_id=r.response_record_id
                JOIN provider_response_finalizations finalization ON finalization.response_record_id=r.response_record_id
                JOIN provider_budget_events event ON event.reservation_id=reservation.reservation_id
                  AND event.event_kind<>'reserved'
                JOIN provider_replay_edges replay ON replay.response_record_id=r.response_record_id
                LEFT JOIN provider_settlements settlement ON settlement.usage_entry_id=usage.usage_entry_id
                WHERE r.operation_id=$operation;
                """;
            command.Parameters.AddWithValue("$operation", operationId);
            string responseId;
            int httpStatus;
            string clientRequestId;
            string? providerRequestId;
            string? providerResponseId;
            string? rawPayloadId;
            string? headersPayloadId;
            long reservedNanoUsd;
            long calculatedNanoUsd;
            string admissionState;
            string eventKind;
            string replayState;
            string replayEdgeId;
            string authorizationId;
            string operationKind;
            string effectiveConfigurationId;
            string usageEntryId;
            string? settlementId;
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                if (!reader.Read())
                {
                    throw new KeyNotFoundException($"Provider operation '{operationId}' does not have a retained terminal response.");
                }
                responseId = reader.GetString(0);
                httpStatus = reader.GetInt32(1);
                clientRequestId = reader.GetString(2);
                providerRequestId = reader.IsDBNull(3) ? null : reader.GetString(3);
                providerResponseId = reader.IsDBNull(4) ? null : reader.GetString(4);
                rawPayloadId = reader.IsDBNull(5) ? null : reader.GetString(5);
                headersPayloadId = reader.IsDBNull(6) ? null : reader.GetString(6);
                reservedNanoUsd = reader.GetInt64(7);
                calculatedNanoUsd = reader.IsDBNull(8) ? 0 : reader.GetInt64(8);
                admissionState = reader.GetString(9);
                eventKind = reader.GetString(10);
                replayState = reader.GetString(11);
                replayEdgeId = reader.GetString(12);
                authorizationId = reader.GetString(13);
                operationKind = reader.GetString(14);
                effectiveConfigurationId = reader.GetString(15);
                usageEntryId = reader.GetString(16);
                settlementId = reader.IsDBNull(17) ? null : reader.GetString(17);
            }
            bool unresolved = eventKind.StartsWith("retained-", StringComparison.Ordinal);
            ProviderOperationState state = unresolved ? ProviderOperationState.UnresolvedHold
                : admissionState == "admitted" ? ProviderOperationState.Settled : ProviderOperationState.Rejected;
            return new(
                operationId, state, reservedNanoUsd, calculatedNanoUsd, unresolved,
                replayState, responseId, httpStatus, clientRequestId,
                providerRequestId, providerResponseId, ReadRetainedPayloadBytes(rawPayloadId),
                headersPayloadId is null ? null : ReadRetainedPayloadBytes(headersPayloadId), replayEdgeId,
                authorizationId, operationKind, effectiveConfigurationId, usageEntryId, settlementId);
        }
    }

    private byte[]? ReadRetainedPayloadBytes(string? payloadId)
    {
        if (payloadId is null)
        {
            return null;
        }
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT object_relative_path FROM payloads WHERE payload_id=$payload AND retention_state='retained';";
        command.Parameters.AddWithValue("$payload", payloadId);
        string? path = command.ExecuteScalar() as string;
        if (path is null || !path.StartsWith("payloads/", StringComparison.Ordinal))
        {
            throw new InvalidDataException("A retained provider payload has no valid content-addressed path.");
        }
        string relative = path["payloads/".Length..].Replace('/', Path.DirectorySeparatorChar);
        using FileStream stream = Paths.OpenReadFile(ProductWriteClass.Payload, relative);
        if (stream.Length > 1_048_576)
        {
            throw new InvalidDataException("A retained provider payload exceeds its replay bound.");
        }
        using MemoryStream bytes = new(checked((int)stream.Length));
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }

    private void InsertProviderUsage(
        ProviderSimulationPersistenceRequest request,
        bool undispatched,
        string rateAvailability,
        SqliteTransaction transaction)
    {
        ProviderUsageContract usage = request.Usage;
        if (ToAvailability(usage.RateAvailability) != rateAvailability
            || usage.BillingAvailability == ProviderAvailabilityState.Available
            || usage.CreditAvailability == ProviderAvailabilityState.Available)
        {
            throw new InvalidOperationException("Simulated usage must retain separate unavailable billing/credit authority and exact rate availability.");
        }
        Execute(
            """
            INSERT INTO provider_usage_entries(
              usage_entry_id,receipt_id,availability,operation_id,provider_attempt_id,request_id,dispatch_fence_id,
              response_record_id,dispatch_count_availability,dispatch_count,input_tokens_availability,input_tokens,
              output_tokens_availability,output_tokens,total_tokens_availability,total_tokens,
              reasoning_tokens_availability,reasoning_tokens,cache_read_tokens_availability,cache_read_tokens,
              cache_write_tokens_availability,cache_write_tokens,priced_tool_calls_availability,priced_tool_calls,
              calculated_nano_usd_availability,calculated_nano_usd,billing_availability,rate_availability,
              credit_availability,receipt_state,created_at)
            VALUES($usage,$receipt,$availability,$operation,$attempt,$request,$fence,$response,
              $dispatch_availability,$dispatch,$input_availability,$input,$output_availability,$output,
              $total_availability,$total,$reasoning_availability,$reasoning,$cache_read_availability,$cache_read,
              $cache_write_availability,$cache_write,$tools_availability,$tools,$nano_availability,$nano,
              $billing,$rate,$credit,$receipt_state,$now);
            """,
            transaction,
            ("$usage", request.UsageEntryId), ("$receipt", request.ReceiptId),
            ("$availability", ToAvailability(usage.Availability)), ("$operation", request.OperationId),
            ("$attempt", request.AttemptId), ("$request", request.RequestId),
            ("$fence", undispatched ? null : request.DispatchFenceId), ("$response", request.ResponseId),
            ("$dispatch_availability", ToAvailability(usage.DispatchCount.Availability)),
            ("$dispatch", usage.DispatchCount.Value),
            ("$input_availability", ToAvailability(usage.InputTokens.Availability)), ("$input", usage.InputTokens.Value),
            ("$output_availability", ToAvailability(usage.OutputTokens.Availability)), ("$output", usage.OutputTokens.Value),
            ("$total_availability", ToAvailability(usage.TotalTokens.Availability)), ("$total", usage.TotalTokens.Value),
            ("$reasoning_availability", ToAvailability(usage.ReasoningTokens.Availability)),
            ("$reasoning", usage.ReasoningTokens.Value),
            ("$cache_read_availability", ToAvailability(usage.CacheReadTokens.Availability)),
            ("$cache_read", usage.CacheReadTokens.Value),
            ("$cache_write_availability", ToAvailability(usage.CacheWriteTokens.Availability)),
            ("$cache_write", usage.CacheWriteTokens.Value),
            ("$tools_availability", ToAvailability(usage.PricedToolCalls.Availability)),
            ("$tools", usage.PricedToolCalls.Value),
            ("$nano_availability", ToAvailability(usage.CalculatedNanoUsd.Availability)),
            ("$nano", usage.CalculatedNanoUsd.Value), ("$billing", ToAvailability(usage.BillingAvailability)),
            ("$rate", ToAvailability(usage.RateAvailability)), ("$credit", ToAvailability(usage.CreditAvailability)),
            ("$receipt_state", ToReceiptState(usage.ReceiptState)), ("$now", ToText(request.OccurredAt)));
    }

    private ProviderBudgetVectorContract ReadReservationVectorOutsideTransaction(string reservationId) =>
        ReadReservationVector(reservationId, null);

    private static ProviderBudgetVectorContract ToAvailableBudgetVector(ProviderUsageContract usage)
    {
        long Required(ProviderQuantityContract quantity, string name)
        {
            if (quantity.Availability != ProviderAvailabilityState.Available || quantity.Value is null)
            {
                throw new InvalidOperationException($"Simulated available usage requires an exact {name} value.");
            }
            return quantity.Value.Value;
        }
        ProviderBudgetVectorContract result = new(
            Required(usage.DispatchCount, "dispatch"), Required(usage.InputTokens, "input-token"),
            Required(usage.OutputTokens, "output-token"), Required(usage.TotalTokens, "total-token"),
            Required(usage.ReasoningTokens, "reasoning-token"), Required(usage.CacheReadTokens, "cache-read-token"),
            Required(usage.CacheWriteTokens, "cache-write-token"), Required(usage.PricedToolCalls, "priced-tool"),
            Required(usage.CalculatedNanoUsd, "calculated-cost"));
        ProviderBudgetVectorContract.Validate(result);
        return result;
    }

    private static string ToAvailability(ProviderAvailabilityState value) => value switch
    {
        ProviderAvailabilityState.Available => "available",
        ProviderAvailabilityState.Unavailable => "unavailable",
        ProviderAvailabilityState.Unsupported => "unsupported",
        ProviderAvailabilityState.NotApplicable => "not-applicable",
        _ => throw new InvalidOperationException("Provider availability must be explicit."),
    };

    private static string ToReceiptState(UsageReceiptState value) => value switch
    {
        UsageReceiptState.NotDispatched => "not-dispatched",
        UsageReceiptState.Complete => "complete",
        UsageReceiptState.Partial => "partial",
        UsageReceiptState.FailedKnown => "failed-known",
        UsageReceiptState.Ambiguous => "ambiguous",
        UsageReceiptState.Unavailable => "unavailable",
        _ => throw new InvalidOperationException("Provider usage receipt state must be explicit."),
    };

    private static string ToResponseState(ProviderResponseState value) => value switch
    {
        ProviderResponseState.Completed => "completed",
        ProviderResponseState.Refusal => "refusal",
        ProviderResponseState.Incomplete => "incomplete",
        ProviderResponseState.Failed => "failed",
        ProviderResponseState.Queued => "queued",
        ProviderResponseState.InProgress => "in-progress",
        ProviderResponseState.Malformed => "malformed",
        ProviderResponseState.Oversized => "oversized",
        ProviderResponseState.Mismatched => "mismatched",
        ProviderResponseState.Unknown => "unknown",
        ProviderResponseState.Cancelled => "cancelled",
        _ => throw new InvalidOperationException("Provider response state must be explicit."),
    };

    public ProviderBudgetProjectionContract GetProviderBudgetProjection(string scopeKind, string scopeId)
    {
        lock (gate)
        {
            return ReadProjection(scopeKind, scopeId, null);
        }
    }

    public IReadOnlyList<ProviderBudgetProjectionContract> RebuildProviderBudgetProjections(DateTimeOffset now)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            List<(string Kind, string Id)> scopes = ReadAllBudgetScopes(transaction);
            List<ProviderBudgetProjectionContract> results = new(scopes.Count);
            foreach ((string kind, string id) in scopes)
            {
                (ProviderBudgetVectorContract reserved, ProviderBudgetVectorContract settled, ProviderBudgetVectorContract unresolved) =
                    ReplayBudgetEvents(kind, id, transaction);
                ProviderBudgetProjectionContract current = ReadProjection(kind, id, transaction);
                WriteProjection(kind, id, reserved, settled, unresolved,
                    checked(current.ProjectionVersion + 1), now, transaction);
                results.Add(new(kind, new OpaqueId(id), reserved, settled, unresolved,
                    checked(current.ProjectionVersion + 1), new UtcTimestamp(now)));
            }
            transaction.Commit();
            return results;
        }
    }

    private void EnsureExactProviderCatalog(
        ProviderCapabilitySnapshotContract capability,
        ProviderPriceSnapshotContract price,
        SqliteTransaction transaction)
    {
        long exactCapability = ScalarLong(
            """
            SELECT COUNT(*) FROM provider_capability_snapshots
            WHERE capability_snapshot_id=$id AND provider=$provider AND model=$model
              AND service_tier=$tier AND reasoning_effort=$effort AND reasoning_context=$context
              AND reasoning_mode=$mode AND store=$store AND background=$background AND stream=$stream
              AND tool_choice=$tool_choice AND tool_count=$tool_count AND truncation=$truncation
              AND prompt_cache_mode=$cache_mode AND has_prompt_cache_key=$cache_key
              AND has_prompt_cache_breakpoint=$cache_breakpoint AND maximum_context_tokens=$maximum_context
              AND revision=$revision AND fingerprint=$fingerprint;
            """,
            transaction,
            ("$id", capability.Identity.Value), ("$provider", capability.Provider), ("$model", capability.Model),
            ("$tier", capability.ServiceTier), ("$effort", capability.ReasoningEffort),
            ("$context", capability.ReasoningContext), ("$mode", capability.ReasoningMode),
            ("$store", capability.Store ? 1 : 0), ("$background", capability.Background ? 1 : 0),
            ("$stream", capability.Stream ? 1 : 0), ("$tool_choice", capability.ToolChoice),
            ("$tool_count", capability.ToolCount), ("$truncation", capability.Truncation),
            ("$cache_mode", capability.PromptCacheMode), ("$cache_key", capability.HasPromptCacheKey ? 1 : 0),
            ("$cache_breakpoint", capability.HasPromptCacheBreakpoint ? 1 : 0),
            ("$maximum_context", capability.MaximumContextTokens), ("$revision", capability.Revision),
            ("$fingerprint", capability.Fingerprint.Value));
        long exactPrice = ScalarLong(
            """
            SELECT COUNT(*) FROM provider_price_snapshots
            WHERE price_snapshot_id=$id AND provider=$provider AND model=$model AND currency=$currency
              AND service_tier=$tier AND revision=$revision AND fingerprint=$fingerprint;
            """,
            transaction,
            ("$id", price.Identity.Value), ("$provider", price.Provider), ("$model", price.Model),
            ("$currency", price.Currency), ("$tier", price.ServiceTier), ("$revision", price.Revision),
            ("$fingerprint", price.Fingerprint.Value));
        long persistedRuleCount = ScalarLong(
            "SELECT COUNT(*) FROM provider_price_rules WHERE price_snapshot_id=$price;",
            transaction,
            ("$price", price.Identity.Value));
        bool exactRules = persistedRuleCount == price.Rules.Count;
        foreach (ProviderPriceRuleContract rule in price.Rules)
        {
            long matches = ScalarLong(
                """
                SELECT COUNT(*) FROM provider_price_rules
                WHERE price_snapshot_id=$price AND rule_id=$rule AND context_band=$context
                  AND cache_class=$cache AND token_class=$token AND tool_class=$tool
                  AND region=$region AND numerator_nano_usd=$numerator
                  AND denominator_tokens=$denominator AND revision=$revision;
                """,
                transaction,
                ("$price", price.Identity.Value), ("$rule", rule.RuleId.Value),
                ("$context", rule.ContextBand), ("$cache", rule.CacheClass), ("$token", rule.TokenClass),
                ("$tool", rule.ToolClass), ("$region", rule.Region), ("$numerator", rule.NumeratorNanoUsd),
                ("$denominator", rule.DenominatorTokens), ("$revision", rule.Revision));
            exactRules &= matches == 1
                && rule.Provider == price.Provider && rule.Model == price.Model
                && rule.ServiceTier == price.ServiceTier && rule.Currency == price.Currency;
        }
        if (exactCapability != 1 || exactPrice != 1 || !exactRules)
        {
            throw new InvalidOperationException(
                "An immutable provider catalog identity/fingerprint cannot be redefined, partially published, or retain altered semantic content.");
        }
    }

    private ProviderBudgetVectorContract ReadAuthoritativeReservationVector(
        string operationId,
        string attemptId,
        string requestId,
        long epoch,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT a.operation_kind,a.maximum_dispatch_count,a.maximum_input_tokens,a.maximum_output_tokens,
                   a.maximum_calculated_nano_usd,a.capability_snapshot_id,a.price_snapshot_id,
                   a.maximum_request_bytes,request.payload_bytes,request.input_bound_policy_id,
                   request.input_bound_policy_version,request.input_bound_proof_status,
                   capability.maximum_context_tokens,capability.reasoning_effort,
                   capability.reasoning_context,capability.reasoning_mode,capability.store,
                   capability.background,capability.stream,capability.tool_choice,
                   capability.tool_count,capability.truncation,capability.prompt_cache_mode,
                   capability.has_prompt_cache_key,capability.has_prompt_cache_breakpoint
            FROM provider_operation_authorizations a
            JOIN provider_operation_attempts attempt
              ON attempt.operation_id=a.operation_id AND attempt.provider_attempt_id=$attempt
            JOIN provider_requests request
              ON request.operation_id=a.operation_id AND request.provider_attempt_id=attempt.provider_attempt_id
             AND request.request_id=$request
            JOIN provider_capability_snapshots capability
              ON capability.capability_snapshot_id=a.capability_snapshot_id
            JOIN provider_price_snapshots price ON price.price_snapshot_id=a.price_snapshot_id
            WHERE a.operation_id=$operation AND a.coordinator_fencing_epoch=$epoch
              AND attempt.coordinator_fencing_epoch=$epoch
              AND request.request_fingerprint=a.request_fingerprint
              AND request.canonical_request_fingerprint=a.canonical_request_fingerprint
              AND request.settings_fingerprint=a.settings_fingerprint
              AND request.output_schema_fingerprint=a.output_schema_fingerprint
              AND request.payload_bytes <= a.maximum_request_bytes
              AND request.input_bound_policy_id=a.input_bound_policy_id
              AND request.input_bound_policy_version=a.input_bound_policy_version
              AND request.input_bound_proof_status=a.input_bound_proof_status
              AND capability.provider=price.provider AND capability.model=price.model
              AND capability.service_tier=price.service_tier AND price.currency='USD';
            """;
        command.Parameters.AddWithValue("$operation", operationId);
        command.Parameters.AddWithValue("$attempt", attemptId);
        command.Parameters.AddWithValue("$request", requestId);
        command.Parameters.AddWithValue("$epoch", epoch);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException(
                "The authoritative reservation vector requires one exact retained request, operation, capability, and price root.");
        }
        string operationKind = reader.GetString(0);
        long dispatch = reader.GetInt64(1);
        long input = reader.GetInt64(2);
        long output = reader.GetInt64(3);
        long authorizedNanoUsd = reader.GetInt64(4);
        string priceId = reader.GetString(6);
        long maximumRequestBytes = reader.GetInt64(7);
        long payloadBytes = reader.GetInt64(8);
        string policyId = reader.GetString(9);
        string policyVersion = reader.GetString(10);
        string proofStatus = reader.GetString(11);
        long maximumContext = reader.GetInt64(12);
        string reasoningEffort = reader.GetString(13);
        string reasoningContext = reader.GetString(14);
        string reasoningMode = reader.GetString(15);
        bool storeResponses = reader.GetInt64(16) != 0;
        bool background = reader.GetInt64(17) != 0;
        bool stream = reader.GetInt64(18) != 0;
        string toolChoice = reader.GetString(19);
        long toolCount = reader.GetInt64(20);
        string truncation = reader.GetString(21);
        string promptCacheMode = reader.GetString(22);
        bool hasPromptCacheKey = reader.GetInt64(23) != 0;
        bool hasPromptCacheBreakpoint = reader.GetInt64(24) != 0;
        reader.Close();

        if (operationKind is not ("transport-qualification" or "source-claim-extraction" or "candidate-investigation")
            || dispatch != 1 || payloadBytes <= 0 || payloadBytes > maximumRequestBytes
            || policyId != ProviderOperationContractInvariants.LocalInputBoundPolicyId
            || policyVersion != ProviderOperationContractInvariants.LocalInputBoundPolicyVersion
            || proofStatus != ProviderOperationContractInvariants.LocalInputBoundProofStatus
            || reasoningEffort != "medium" || reasoningContext != "current_turn" || reasoningMode != "standard"
            || storeResponses || background || stream || toolChoice != "none" || toolCount != 0
            || truncation != "disabled" || promptCacheMode != "explicit"
            || hasPromptCacheKey || hasPromptCacheBreakpoint
            || checked(input + output) > maximumContext)
        {
            throw new InvalidOperationException("The retained operation cannot produce a qualified finite M1 reservation vector.");
        }

        ProviderPriceRuleContract inputRule = ReadRetainedPriceRule(priceId, "ordinary-input", "input", transaction);
        ProviderPriceRuleContract outputRule = ReadRetainedPriceRule(priceId, "none", "output", transaction);
        long nanoUsd = checked(
            ProviderOperationContractInvariants.CalculateComponentNanoUsd(input, inputRule)
            + ProviderOperationContractInvariants.CalculateComponentNanoUsd(output, outputRule));
        if (nanoUsd > authorizedNanoUsd)
        {
            throw new InvalidOperationException("The authoritative catalog-calculated worst-case cost exceeds the retained operation limit.");
        }
        return new(dispatch, input, output, checked(input + output), output, 0, 0, 0, nanoUsd);
    }

    private ProviderPriceRuleContract ReadRetainedPriceRule(
        string priceId,
        string cacheClass,
        string tokenClass,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT rule.rule_id,snapshot.provider,snapshot.model,snapshot.service_tier,rule.context_band,
                   rule.cache_class,rule.token_class,rule.tool_class,rule.region,snapshot.currency,
                   rule.numerator_nano_usd,rule.denominator_tokens,rule.revision
            FROM provider_price_rules rule
            JOIN provider_price_snapshots snapshot ON snapshot.price_snapshot_id=rule.price_snapshot_id
            WHERE rule.price_snapshot_id=$price AND rule.cache_class=$cache AND rule.token_class=$token;
            """;
        command.Parameters.AddWithValue("$price", priceId);
        command.Parameters.AddWithValue("$cache", cacheClass);
        command.Parameters.AddWithValue("$token", tokenClass);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("The retained price catalog lacks one exact required M1 price rule.");
        }
        ProviderPriceRuleContract result = new(new OpaqueId(reader.GetString(0)), reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
            reader.GetString(8), reader.GetString(9), reader.GetInt64(10), reader.GetInt64(11), reader.GetString(12));
        if (reader.Read())
        {
            throw new InvalidOperationException("The retained price catalog has an ambiguous M1 price class.");
        }
        return result;
    }

    private Dictionary<string, string> ReadExpectedBudgetScopes(
        string operationId,
        string attemptId,
        string requestId,
        long epoch,
        DateTimeOffset evaluatedAt,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT a.owner_kind,a.owner_id,a.profile_id,p.account_identity_id,p.billing_scope_identity_id,
                   CASE WHEN a.owner_kind='evidence-acquisition-run'
                     THEN (SELECT parent_analysis_run_id FROM evidence_acquisition_runs WHERE acquisition_run_id=a.owner_id)
                     ELSE a.owner_id END
            FROM provider_operation_authorizations a
            JOIN provider_operation_attempts attempt ON attempt.operation_id=a.operation_id
            JOIN provider_requests request ON request.operation_id=a.operation_id AND request.provider_attempt_id=attempt.provider_attempt_id
            JOIN provider_access_profiles p ON p.profile_id=a.profile_id
            JOIN provider_profile_projection profile
              ON profile.profile_id=a.profile_id AND profile.generation_id=a.generation_id
             AND profile.revocation_epoch=a.revocation_epoch
            WHERE a.operation_id=$operation AND attempt.provider_attempt_id=$attempt
              AND request.request_id=$request AND a.coordinator_fencing_epoch=$epoch
              AND attempt.coordinator_fencing_epoch=$epoch AND attempt.initial_state='proposed'
              AND a.execution_mode='simulated-nonnetwork'
              AND profile.lifecycle_state='active-verified' AND profile.verification_state='available'
              AND $evaluated >= a.confirmed_at AND $evaluated < a.dispatch_deadline_utc
              AND p.account_identity_id IS NOT NULL AND p.billing_scope_identity_id IS NOT NULL
              AND NOT EXISTS(SELECT 1 FROM provider_dispatch_fences f WHERE f.operation_id=a.operation_id)
              AND NOT EXISTS(SELECT 1 FROM provider_transport_events e WHERE e.operation_id=a.operation_id)
              AND NOT EXISTS(SELECT 1 FROM provider_settlements s WHERE s.operation_id=a.operation_id)
              AND ((a.owner_kind='analysis-run' AND EXISTS(
                    SELECT 1 FROM runs run JOIN job_nodes node ON node.run_id=run.run_id
                    WHERE run.run_id=a.owner_id AND node.job_node_id=a.job_node_id
                      AND run.lifecycle_state IN ('Running','Waiting')
                      AND node.lifecycle_state IN ('Running','Waiting')))
                OR (a.owner_kind='evidence-acquisition-run' AND EXISTS(
                    SELECT 1 FROM evidence_acquisition_runs run
                    JOIN evidence_acquisition_job_nodes node ON node.acquisition_run_id=run.acquisition_run_id
                    WHERE run.acquisition_run_id=a.owner_id AND node.acquisition_job_node_id=a.job_node_id
                      AND lower(run.lifecycle_state) IN ('running','waiting')
                      AND lower(node.lifecycle_state) IN ('running','waiting'))));
            """;
        command.Parameters.AddWithValue("$operation", operationId);
        command.Parameters.AddWithValue("$attempt", attemptId);
        command.Parameters.AddWithValue("$request", requestId);
        command.Parameters.AddWithValue("$epoch", epoch);
        command.Parameters.AddWithValue("$evaluated", ToText(evaluatedAt));
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("Provider reservation requires an exact authorized request and attempt root.");
        }
        string ownerKind = reader.GetString(0);
        string ownerId = reader.GetString(1);
        Dictionary<string, string> result = new(StringComparer.Ordinal)
        {
            ["request"] = requestId,
            ["operation"] = operationId,
            [ownerKind] = ownerId,
            ["analysis-run"] = reader.GetString(5),
            ["provider-profile"] = reader.GetString(2),
            ["provider-account"] = reader.GetString(3),
            ["billing-scope"] = reader.GetString(4),
            ["global"] = "provider-global",
        };
        return result;
    }

    private ProviderBudgetProjectionContract ReadProjection(
        string kind,
        string id,
        SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT reserved_dispatch_count,reserved_input_tokens,reserved_output_tokens,reserved_total_tokens,
                   reserved_reasoning_tokens,reserved_cache_read_tokens,reserved_cache_write_tokens,reserved_priced_tool_calls,reserved_nano_usd,
                   settled_dispatch_count,settled_input_tokens,settled_output_tokens,settled_total_tokens,
                   settled_reasoning_tokens,settled_cache_read_tokens,settled_cache_write_tokens,settled_priced_tool_calls,settled_nano_usd,
                   unresolved_dispatch_count,unresolved_input_tokens,unresolved_output_tokens,unresolved_total_tokens,
                   unresolved_reasoning_tokens,unresolved_cache_read_tokens,unresolved_cache_write_tokens,unresolved_priced_tool_calls,unresolved_nano_usd,
                   projection_version,updated_at
            FROM provider_budget_projection WHERE scope_kind=$kind AND scope_id=$scope;
            """;
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$scope", id);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"Provider budget projection '{kind}/{id}' does not exist.");
        }
        return new(kind, new OpaqueId(id), ReadVector(reader, 0), ReadVector(reader, 9), ReadVector(reader, 18),
            reader.GetInt64(27), new UtcTimestamp(DateTimeOffset.Parse(reader.GetString(28), System.Globalization.CultureInfo.InvariantCulture)));
    }

    private ProviderBudgetVectorContract ReadBudgetVector(
        string table,
        string prefix,
        string kind,
        string id,
        SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT {prefix}dispatch_count,{prefix}input_tokens,{prefix}output_tokens,{prefix}total_tokens,{prefix}reasoning_tokens,{prefix}cache_read_tokens,{prefix}cache_write_tokens,{prefix}priced_tool_calls,{prefix}nano_usd FROM {table} WHERE scope_kind=$kind AND scope_id=$scope;";
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$scope", id);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"Provider budget scope '{kind}/{id}' does not exist.");
        }
        return ReadVector(reader, 0);
    }

    private ProviderBudgetVectorContract ReadReservationVector(string reservationId, SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT reserved_dispatch_count,reserved_input_tokens,reserved_output_tokens,
                   reserved_input_tokens+reserved_output_tokens,reserved_reasoning_tokens,
                   reserved_cache_read_tokens,reserved_cache_write_tokens,reserved_priced_tool_calls,maximum_nano_usd
            FROM provider_reservations WHERE reservation_id=$reservation;
            """;
        command.Parameters.AddWithValue("$reservation", reservationId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"Provider reservation '{reservationId}' does not exist.");
        }
        return ReadVector(reader, 0);
    }

    private List<(string Kind, string Id)> ReadReservationScopes(string reservationId, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT scope_kind,scope_id FROM provider_reservation_scope_items WHERE reservation_id=$reservation ORDER BY scope_kind,scope_id;";
        command.Parameters.AddWithValue("$reservation", reservationId);
        using SqliteDataReader reader = command.ExecuteReader();
        List<(string Kind, string Id)> result = [];
        while (reader.Read())
        {
            result.Add((reader.GetString(0), reader.GetString(1)));
        }
        return result;
    }

    private List<(string Kind, string Id)> ReadAllBudgetScopes(SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT scope_kind,scope_id FROM provider_budget_limits ORDER BY scope_kind,scope_id;";
        using SqliteDataReader reader = command.ExecuteReader();
        List<(string Kind, string Id)> result = [];
        while (reader.Read())
        {
            result.Add((reader.GetString(0), reader.GetString(1)));
        }
        return result;
    }

    private void EnsureActualUsage(
        string reservationId,
        string usageEntryId,
        ProviderBudgetVectorContract actual,
        SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT dispatch_count,input_tokens,output_tokens,total_tokens,reasoning_tokens,
                   cache_read_tokens,cache_write_tokens,priced_tool_calls,calculated_nano_usd
            FROM provider_usage_entries usage
            JOIN provider_reservations reservation
              ON reservation.operation_id=usage.operation_id
             AND reservation.provider_attempt_id=usage.provider_attempt_id
             AND reservation.request_id=usage.request_id
            WHERE reservation.reservation_id=$reservation AND usage.usage_entry_id=$usage
              AND dispatch_count_availability='available' AND input_tokens_availability='available'
              AND output_tokens_availability='available' AND total_tokens_availability='available'
              AND reasoning_tokens_availability='available' AND cache_read_tokens_availability='available'
              AND cache_write_tokens_availability='available' AND priced_tool_calls_availability='available'
              AND calculated_nano_usd_availability='available';
            """;
        command.Parameters.AddWithValue("$usage", usageEntryId);
        command.Parameters.AddWithValue("$reservation", reservationId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read() || ReadVector(reader, 0) != actual)
        {
            throw new InvalidOperationException("Settlement must use the exact one-owned available provider usage entry.");
        }
    }

    private void EnsureUndispatchedUsage(
        string reservationId,
        string usageEntryId,
        SqliteTransaction transaction)
    {
        long exact = ScalarLong(
            """
            SELECT COUNT(*) FROM provider_usage_entries usage
            JOIN provider_reservations reservation
              ON reservation.operation_id=usage.operation_id
             AND reservation.provider_attempt_id=usage.provider_attempt_id
             AND reservation.request_id=usage.request_id
            JOIN provider_responses response ON response.response_record_id=usage.response_record_id
            WHERE reservation.reservation_id=$reservation AND usage.usage_entry_id=$usage
              AND response.response_state='cancelled' AND usage.availability='unavailable'
              AND usage.receipt_state='not-dispatched' AND usage.dispatch_count=0;
            """,
            transaction,
            ("$reservation", reservationId), ("$usage", usageEntryId));
        if (exact != 1)
        {
            throw new InvalidOperationException("Known-undispatched settlement requires its exact zero-dispatch usage receipt.");
        }
    }

    private static ProviderBudgetVectorContract ReadVector(SqliteDataReader reader, int offset) =>
        new(reader.GetInt64(offset), reader.GetInt64(offset + 1), reader.GetInt64(offset + 2),
            reader.GetInt64(offset + 3), reader.GetInt64(offset + 4), reader.GetInt64(offset + 5),
            reader.GetInt64(offset + 6), reader.GetInt64(offset + 7), reader.GetInt64(offset + 8));

    private void InsertBudgetEvent(
        string reservationId,
        string? usageEntryId,
        string scopeKind,
        string scopeId,
        ProviderBudgetEventKind kind,
        ProviderBudgetVectorContract vector,
        long sequence,
        DateTimeOffset occurredAt,
        SqliteTransaction transaction)
    {
        string eventKind = ToEventKind(kind);
        Execute(
            """
            INSERT INTO provider_budget_events VALUES(
                $event,$reservation,$usage,$kind,$scope,$event_kind,
                $dispatch,$input,$output,$total,$reasoning,$cache_read,$cache_write,$tools,$nano,$sequence,$now);
            """,
            transaction,
            ("$event", reservationId + ":" + scopeKind + ":" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("$reservation", reservationId), ("$usage", usageEntryId), ("$kind", scopeKind), ("$scope", scopeId),
            ("$event_kind", eventKind), ("$dispatch", vector.DispatchCount), ("$input", vector.InputTokens),
            ("$output", vector.OutputTokens), ("$total", vector.TotalTokens), ("$reasoning", vector.ReasoningTokens),
            ("$cache_read", vector.CacheReadTokens), ("$cache_write", vector.CacheWriteTokens),
            ("$tools", vector.PricedToolCalls), ("$nano", vector.NanoUsd), ("$sequence", sequence),
            ("$now", ToText(occurredAt)));
    }

    private static string ToEventKind(ProviderBudgetEventKind kind) => kind switch
    {
        ProviderBudgetEventKind.Reserved => "reserved",
        ProviderBudgetEventKind.ReleasedUndispatched => "released-undispatched",
        ProviderBudgetEventKind.SettledComplete => "settled-complete",
        ProviderBudgetEventKind.SettledFailedKnown => "settled-failed-known",
        ProviderBudgetEventKind.RetainedAmbiguous => "retained-ambiguous",
        ProviderBudgetEventKind.RetainedPartial => "retained-partial",
        ProviderBudgetEventKind.RetainedUnavailable => "retained-unavailable",
        ProviderBudgetEventKind.SettledOverrun => "settled-overrun",
        ProviderBudgetEventKind.Adjustment => "adjustment",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private void AdvanceProjection(
        string kind,
        string id,
        ProviderBudgetVectorContract reserveAdd,
        ProviderBudgetVectorContract settledAdd,
        ProviderBudgetVectorContract unresolvedAdd,
        DateTimeOffset now,
        SqliteTransaction transaction,
        ProviderBudgetVectorContract? reserveSubtract = null)
    {
        ProviderBudgetProjectionContract current = ReadProjection(kind, id, transaction);
        ProviderBudgetVectorContract reserved = ProviderBudgetVectorContract.Add(current.Reserved, reserveAdd);
        if (reserveSubtract is not null)
        {
            reserved = ProviderBudgetVectorContract.Subtract(reserved, reserveSubtract);
        }
        ProviderBudgetVectorContract settled = ProviderBudgetVectorContract.Add(current.Settled, settledAdd);
        ProviderBudgetVectorContract unresolved = ProviderBudgetVectorContract.Add(current.Unresolved, unresolvedAdd);
        WriteProjection(kind, id, reserved, settled, unresolved, checked(current.ProjectionVersion + 1), now, transaction);
    }

    private void WriteProjection(
        string kind,
        string id,
        ProviderBudgetVectorContract reserved,
        ProviderBudgetVectorContract settled,
        ProviderBudgetVectorContract unresolved,
        long version,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        Execute(
            """
            UPDATE provider_budget_projection SET
              reserved_dispatch_count=$rd,reserved_input_tokens=$ri,reserved_output_tokens=$ro,reserved_total_tokens=$rt,
              reserved_reasoning_tokens=$rr,reserved_cache_read_tokens=$rcr,reserved_cache_write_tokens=$rcw,
              reserved_priced_tool_calls=$rp,reserved_nano_usd=$rn,
              settled_dispatch_count=$sd,settled_input_tokens=$si,settled_output_tokens=$so,settled_total_tokens=$st,
              settled_reasoning_tokens=$sr,settled_cache_read_tokens=$scr,settled_cache_write_tokens=$scw,
              settled_priced_tool_calls=$sp,settled_nano_usd=$sn,
              unresolved_dispatch_count=$ud,unresolved_input_tokens=$ui,unresolved_output_tokens=$uo,unresolved_total_tokens=$ut,
              unresolved_reasoning_tokens=$ur,unresolved_cache_read_tokens=$ucr,unresolved_cache_write_tokens=$ucw,
              unresolved_priced_tool_calls=$up,unresolved_nano_usd=$un,
              projection_version=$version,updated_at=$now
            WHERE scope_kind=$kind AND scope_id=$scope;
            """,
            transaction,
            ("$rd", reserved.DispatchCount), ("$ri", reserved.InputTokens), ("$ro", reserved.OutputTokens),
            ("$rt", reserved.TotalTokens), ("$rr", reserved.ReasoningTokens), ("$rcr", reserved.CacheReadTokens),
            ("$rcw", reserved.CacheWriteTokens), ("$rp", reserved.PricedToolCalls), ("$rn", reserved.NanoUsd),
            ("$sd", settled.DispatchCount), ("$si", settled.InputTokens), ("$so", settled.OutputTokens),
            ("$st", settled.TotalTokens), ("$sr", settled.ReasoningTokens), ("$scr", settled.CacheReadTokens),
            ("$scw", settled.CacheWriteTokens), ("$sp", settled.PricedToolCalls), ("$sn", settled.NanoUsd),
            ("$ud", unresolved.DispatchCount), ("$ui", unresolved.InputTokens), ("$uo", unresolved.OutputTokens),
            ("$ut", unresolved.TotalTokens), ("$ur", unresolved.ReasoningTokens), ("$ucr", unresolved.CacheReadTokens),
            ("$ucw", unresolved.CacheWriteTokens), ("$up", unresolved.PricedToolCalls), ("$un", unresolved.NanoUsd),
            ("$version", version), ("$now", ToText(now)), ("$kind", kind), ("$scope", id));
    }

    private (ProviderBudgetVectorContract Reserved, ProviderBudgetVectorContract Settled, ProviderBudgetVectorContract Unresolved)
        ReplayBudgetEvents(string kind, string id, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT reservation_id,event_kind,dispatch_count,input_tokens,output_tokens,total_tokens,reasoning_tokens,
                   cache_read_tokens,cache_write_tokens,priced_tool_calls,nano_usd
            FROM provider_budget_events WHERE scope_kind=$kind AND scope_id=$scope
            ORDER BY occurred_at,reservation_id,sequence;
            """;
        command.Parameters.AddWithValue("$kind", kind);
        command.Parameters.AddWithValue("$scope", id);
        using SqliteDataReader reader = command.ExecuteReader();
        ProviderBudgetVectorContract reserved = ProviderBudgetVectorContract.Zero;
        ProviderBudgetVectorContract settled = ProviderBudgetVectorContract.Zero;
        ProviderBudgetVectorContract unresolved = ProviderBudgetVectorContract.Zero;
        Dictionary<string, ProviderBudgetVectorContract> reservationVectors = new(StringComparer.Ordinal);
        while (reader.Read())
        {
            string reservation = reader.GetString(0);
            string eventKind = reader.GetString(1);
            ProviderBudgetVectorContract vector = ReadVector(reader, 2);
            if (eventKind == "reserved")
            {
                reserved = ProviderBudgetVectorContract.Add(reserved, vector);
                reservationVectors[reservation] = vector;
            }
            else
            {
                ProviderBudgetVectorContract original = reservationVectors[reservation];
                reserved = ProviderBudgetVectorContract.Subtract(reserved, original);
                if (eventKind is "retained-ambiguous" or "retained-partial" or "retained-unavailable")
                {
                    unresolved = ProviderBudgetVectorContract.Add(unresolved, original);
                }
                else if (eventKind is "settled-complete" or "settled-failed-known" or "settled-overrun")
                {
                    settled = ProviderBudgetVectorContract.Add(settled, vector);
                }
            }
        }
        return (reserved, settled, unresolved);
    }
}
