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
            long exactCatalog = ScalarLong(
                """
                SELECT
                  (SELECT COUNT(*) FROM provider_capability_snapshots
                   WHERE capability_snapshot_id=$capability AND fingerprint=$capability_fingerprint
                     AND provider='openai' AND model='gpt-5.6-sol' AND service_tier='default'
                     AND maximum_context_tokens=$context)
                  + (SELECT COUNT(*) FROM provider_price_snapshots
                     WHERE price_snapshot_id=$price AND fingerprint=$price_fingerprint
                       AND provider='openai' AND model='gpt-5.6-sol' AND currency='USD')
                  + (SELECT COUNT(*) FROM provider_price_rules WHERE price_snapshot_id=$price);
                """,
                transaction,
                ("$capability", capability.Identity.Value), ("$capability_fingerprint", capability.Fingerprint.Value),
                ("$context", capability.MaximumContextTokens), ("$price", price.Identity.Value),
                ("$price_fingerprint", price.Fingerprint.Value));
            if (exactCatalog != checked(2 + price.Rules.Count))
            {
                throw new InvalidOperationException("An immutable provider catalog identity cannot be redefined or partially published.");
            }
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
                case ProviderBudgetEventKind.ReleasedUndispatched when !hasStart && request.UsageEntryId is null && request.Actual is null:
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
                        EnsureActualUsage(request.UsageEntryId, actual, transaction);
                    }
                    released = ProviderBudgetVectorContract.Zero;
                    settled = ProviderBudgetVectorContract.Zero;
                    unresolved = reserved;
                    retry = false;
                    break;
                case ProviderBudgetEventKind.SettledComplete or ProviderBudgetEventKind.SettledFailedKnown
                    when hasStart && request.UsageEntryId is not null && request.Actual is not null:
                    EnsureActualUsage(request.UsageEntryId, actual, transaction);
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
                    EnsureActualUsage(request.UsageEntryId, actual, transaction);
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

    private ProviderBudgetVectorContract ReadReservationVector(string reservationId, SqliteTransaction transaction)
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

    private void EnsureActualUsage(string usageEntryId, ProviderBudgetVectorContract actual, SqliteTransaction transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT dispatch_count,input_tokens,output_tokens,total_tokens,reasoning_tokens,
                   cache_read_tokens,cache_write_tokens,priced_tool_calls,calculated_nano_usd
            FROM provider_usage_entries
            WHERE usage_entry_id=$usage
              AND dispatch_count_availability='available' AND input_tokens_availability='available'
              AND output_tokens_availability='available' AND total_tokens_availability='available'
              AND reasoning_tokens_availability='available' AND cache_read_tokens_availability='available'
              AND cache_write_tokens_availability='available' AND priced_tool_calls_availability='available'
              AND calculated_nano_usd_availability='available';
            """;
        command.Parameters.AddWithValue("$usage", usageEntryId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read() || ReadVector(reader, 0) != actual)
        {
            throw new InvalidOperationException("Settlement must use the exact one-owned available provider usage entry.");
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
