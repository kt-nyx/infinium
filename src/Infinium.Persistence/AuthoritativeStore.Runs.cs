using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

#pragma warning disable IDE0008 // SQL transaction code is clearer with local type inference.
#pragma warning disable CA1512 // Guard clauses use parameter-specific messages.
#pragma warning disable CA1869 // The backup serializer is not a hot path.

namespace Infinium.Persistence;


public sealed partial class AuthoritativeStore
{
    // Durable analysis requests may carry the bounded semantic-analysis composition
    // envelope. Checkpoint documents retain their smaller independent bound.
    private const int MaximumRunOperationJsonBytes = 896 * 1024;

    public RunRecord CreateRun(
        string durableCommandId,
        string runId,
        RunBinding binding,
        long coordinatorFencingEpoch,
        DateTimeOffset now,
        string? startInitiationKind = null,
        DateTimeOffset? startDispatchDeadline = null,
        string? operationKind = null,
        string? operationRequestJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableCommandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ValidateBinding(binding);
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        if ((startInitiationKind is null) != (startDispatchDeadline is null))
        {
            throw new ArgumentException(
                "Start initiation kind and dispatch deadline must be supplied together.");
        }
        if (startInitiationKind is not null)
        {
            ValidateAuditToken(startInitiationKind, nameof(startInitiationKind));
        }
        if ((operationKind is null) != (operationRequestJson is null))
        {
            throw new ArgumentException("A durable run operation kind and request must be supplied together.");
        }
        string? operationSha256 = null;
        if (operationKind is not null)
        {
            ValidateAuditToken(operationKind, nameof(operationKind));
            if (string.IsNullOrWhiteSpace(operationRequestJson)
                || Encoding.UTF8.GetByteCount(operationRequestJson) > MaximumRunOperationJsonBytes)
            {
                throw new InvalidOperationException("The durable run operation request exceeds its bound.");
            }
            operationSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(operationRequestJson)));
        }

        lock (gate)
        {
            using var transaction = BeginTransaction();
            var existingRunId = ScalarStringOrNull(
                "SELECT run_id FROM durable_commands WHERE command_id = $id;",
                transaction,
                ("$id", durableCommandId));
            if (existingRunId is not null)
            {
                RunRecord existingRun = GetRunCore(existingRunId);
                if (existingRun.Binding != binding
                    || !string.Equals(
                        ScalarString(
                            "SELECT command_kind FROM durable_commands WHERE command_id = $id;",
                            transaction,
                            ("$id", durableCommandId)),
                        "start",
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ScalarStringOrNull(
                            "SELECT start_initiation_kind FROM durable_commands WHERE command_id = $id;",
                            transaction,
                            ("$id", durableCommandId)),
                        startInitiationKind,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A durable command key cannot be rebound to different start inputs.");
                }
                if (operationKind is not null
                    && ScalarLong("SELECT COUNT(*) FROM run_operations WHERE run_id=$run AND operation_kind=$kind AND request_sha256=$sha;",
                        transaction, ("$run", existingRun.RunId), ("$kind", operationKind), ("$sha", operationSha256)) != 1)
                {
                    throw new InvalidOperationException("A durable command key cannot be rebound to different run-operation inputs.");
                }

                transaction.Commit();
                return existingRun;
            }

            if (startDispatchDeadline is not null && startDispatchDeadline <= now)
            {
                throw new InvalidOperationException(
                    "A new start command requires a future dispatch deadline.");
            }

            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            Execute(
                """
                INSERT INTO runs(
                    run_id, installation_snapshot_id, analysis_context_id,
                    effective_scan_configuration_id, resolved_input_manifest_id,
                    lifecycle_state, lifecycle_generation, coordinator_fencing_epoch,
                    durable_sequence, created_at, updated_at)
                VALUES (
                    $run, $snapshot, $context, $config, $manifest,
                    'Queued', 0, $epoch, 1, $now, $now);
                """,
                transaction,
                ("$run", runId),
                ("$snapshot", binding.InstallationSnapshotId),
                ("$context", binding.AnalysisContextId),
                ("$config", binding.EffectiveScanConfigurationId),
                ("$manifest", binding.ResolvedInputManifestId),
                ("$epoch", coordinatorFencingEpoch),
                ("$now", ToText(now)));
            Execute(
                """
                INSERT INTO job_nodes(
                    job_node_id, run_id, parent_job_node_id, node_kind, lifecycle_state,
                    lifecycle_generation, created_at, updated_at)
                VALUES ($job, $run, NULL, 'analysis-run-root', 'Queued', 0, $now, $now);
                """,
                transaction,
                ("$job", runId + "-root"),
                ("$run", runId),
                ("$now", ToText(now)));
            Execute(
                """
                INSERT INTO durable_commands(
                    command_id, command_kind, run_id, expected_generation, disposition,
                    resulting_state, created_at, start_initiation_kind,
                    start_dispatch_deadline)
                VALUES (
                    $id, 'start', $run, 0, 'accepted', 'Queued', $now,
                    $initiation, $deadline);
                """,
                transaction,
                ("$id", durableCommandId),
                ("$run", runId),
                ("$now", ToText(now)),
                ("$initiation", startInitiationKind),
                ("$deadline", startDispatchDeadline is null
                    ? null
                    : ToText(startDispatchDeadline.Value)));
            Execute(
                """
                INSERT INTO run_projection(
                    run_id, lifecycle_state, lifecycle_generation, durable_sequence,
                    projection_version, updated_at)
                VALUES ($run, 'Queued', 0, 1, 1, $now);
                """,
                transaction,
                ("$run", runId),
                ("$now", ToText(now)));
            if (operationKind is not null)
            {
                Execute(
                    """
                    INSERT INTO run_operations(run_id,operation_kind,request_json,request_sha256,created_at)
                    VALUES ($run,$kind,$request,$sha,$now);
                    """, transaction, ("$run", runId), ("$kind", operationKind),
                    ("$request", operationRequestJson), ("$sha", operationSha256), ("$now", ToText(now)));
            }
            transaction.Commit();
            return GetRunCore(runId);
        }
    }

    public RunOperationRecord RegisterRunOperation(
        string runId,
        string operationKind,
        string requestJson,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ValidateAuditToken(operationKind, nameof(operationKind));
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        if (Encoding.UTF8.GetByteCount(requestJson) > MaximumRunOperationJsonBytes)
        {
            throw new InvalidOperationException("The durable run operation request exceeds its bound.");
        }

        string requestSha256 = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(requestJson)));
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            RunRecord run = GetRunCore(runId);
            if (run.State is not (LifecycleState.Queued or LifecycleState.Retrying))
            {
                throw new InvalidOperationException(
                    "A durable run operation can be registered only before dispatch.");
            }

            using SqliteCommand existing = connection.CreateCommand();
            existing.Transaction = transaction;
            existing.CommandText =
                "SELECT operation_kind, request_json, request_sha256, created_at FROM run_operations WHERE run_id = $run;";
            existing.Parameters.AddWithValue("$run", runId);
            using SqliteDataReader reader = existing.ExecuteReader();
            if (reader.Read())
            {
                RunOperationRecord record = new(
                    runId,
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture));
                if (!string.Equals(record.OperationKind, operationKind, StringComparison.Ordinal)
                    || !string.Equals(record.RequestJson, requestJson, StringComparison.Ordinal)
                    || !string.Equals(record.RequestSha256, requestSha256, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A run operation cannot be rebound to different inputs.");
                }

                transaction.Commit();
                return record;
            }

            reader.Close();
            Execute(
                """
                INSERT INTO run_operations(run_id, operation_kind, request_json, request_sha256, created_at)
                VALUES ($run, $kind, $request, $sha, $now);
                """,
                transaction,
                ("$run", runId),
                ("$kind", operationKind),
                ("$request", requestJson),
                ("$sha", requestSha256),
                ("$now", ToText(now)));
            transaction.Commit();
            return new RunOperationRecord(runId, operationKind, requestJson, requestSha256, now);
        }
    }

    public RunOperationRecord? GetRunOperation(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT operation_kind, request_json, request_sha256, created_at FROM run_operations WHERE run_id = $run;";
            command.Parameters.AddWithValue("$run", runId);
            using SqliteDataReader reader = command.ExecuteReader();
            return !reader.Read()
                ? null
                : new RunOperationRecord(
                    runId,
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public RunRecord GetRun(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            return GetRunCore(runId);
        }
    }

    public IReadOnlyList<RunRecord> ListRuns(
        int maximumCount = 100,
        DateTimeOffset? afterCreatedAt = null,
        string? afterRunId = null)
    {
        if (maximumCount is <= 0 or > 101)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = afterCreatedAt is null
                ? """
                SELECT run_id, installation_snapshot_id, analysis_context_id,
                       effective_scan_configuration_id, resolved_input_manifest_id,
                       lifecycle_state, lifecycle_generation, coordinator_fencing_epoch,
                       durable_sequence, created_at, updated_at
                FROM runs
                ORDER BY created_at, run_id
                LIMIT $limit;
                """
                : """
                SELECT run_id, installation_snapshot_id, analysis_context_id,
                       effective_scan_configuration_id, resolved_input_manifest_id,
                       lifecycle_state, lifecycle_generation, coordinator_fencing_epoch,
                       durable_sequence, created_at, updated_at
                FROM runs
                WHERE created_at > $after_created
                   OR (created_at = $after_created AND run_id > $after_run)
                ORDER BY created_at, run_id
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", maximumCount);
            if (afterCreatedAt is not null)
            {
                command.Parameters.AddWithValue("$after_created", ToText(afterCreatedAt.Value));
                command.Parameters.AddWithValue("$after_run", afterRunId ?? string.Empty);
            }
            using var reader = command.ExecuteReader();
            var result = new List<RunRecord>();
            while (reader.Read())
            {
                result.Add(ReadRun(reader));
            }

            return result;
        }
    }

    public IReadOnlyList<RunRecord> ListRecentRuns(int maximumCount)
    {
        if (maximumCount is <= 0 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT run_id, installation_snapshot_id, analysis_context_id,
                       effective_scan_configuration_id, resolved_input_manifest_id,
                       lifecycle_state, lifecycle_generation, coordinator_fencing_epoch,
                       durable_sequence, created_at, updated_at
                FROM runs
                ORDER BY created_at DESC, run_id DESC
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", maximumCount);
            using var reader = command.ExecuteReader();
            var result = new List<RunRecord>();
            while (reader.Read())
            {
                result.Add(ReadRun(reader));
            }

            result.Reverse();
            return result;
        }
    }

    public IReadOnlyList<RunRecord> ListNonTerminalRuns(
        int maximumCount = 100,
        DateTimeOffset? afterCreatedAt = null,
        string? afterRunId = null)
    {
        if (maximumCount is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = afterCreatedAt is null
                ? """
                SELECT run_id, installation_snapshot_id, analysis_context_id,
                       effective_scan_configuration_id, resolved_input_manifest_id,
                       lifecycle_state, lifecycle_generation, coordinator_fencing_epoch,
                       durable_sequence, created_at, updated_at
                FROM runs
                WHERE lifecycle_state IN (
                    'Queued','Running','Waiting','Retrying','Pausing','Paused','Cancelling')
                ORDER BY created_at, run_id
                LIMIT $limit;
                """
                : """
                SELECT run_id, installation_snapshot_id, analysis_context_id,
                       effective_scan_configuration_id, resolved_input_manifest_id,
                       lifecycle_state, lifecycle_generation, coordinator_fencing_epoch,
                       durable_sequence, created_at, updated_at
                FROM runs
                WHERE lifecycle_state IN (
                    'Queued','Running','Waiting','Retrying','Pausing','Paused','Cancelling')
                  AND (
                      created_at > $after_created
                      OR (created_at = $after_created AND run_id > $after_run)
                  )
                ORDER BY created_at, run_id
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$limit", maximumCount);
            if (afterCreatedAt is not null)
            {
                command.Parameters.AddWithValue("$after_created", ToText(afterCreatedAt.Value));
                command.Parameters.AddWithValue("$after_run", afterRunId ?? string.Empty);
            }
            using var reader = command.ExecuteReader();
            var result = new List<RunRecord>();
            while (reader.Read())
            {
                result.Add(ReadRun(reader));
            }

            return result;
        }
    }

    public RunRecord? GetNextDispatchableRun()
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT run_id, installation_snapshot_id, analysis_context_id,
                       effective_scan_configuration_id, resolved_input_manifest_id,
                       lifecycle_state, lifecycle_generation, coordinator_fencing_epoch,
                       durable_sequence, created_at, updated_at
                FROM runs
                WHERE lifecycle_state IN ('Queued','Retrying')
                ORDER BY created_at, run_id
                LIMIT 1;
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read() ? ReadRun(reader) : null;
        }
    }

    public DurableCommandRecord GetDurableCommand(string commandId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandId);
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT command.command_id, command.disposition, command.run_id,
                       command.resulting_state, command.transition_id, command.created_at,
                       command.command_kind, command.expected_generation,
                       run.installation_snapshot_id, run.analysis_context_id,
                       run.effective_scan_configuration_id, run.resolved_input_manifest_id,
                       command.start_initiation_kind, command.start_dispatch_deadline
                FROM durable_commands command
                JOIN runs run ON run.run_id = command.run_id
                WHERE command.command_id = $id;
                """;
            command.Parameters.AddWithValue("$id", commandId);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"Durable command '{commandId}' does not exist.");
            }

            return new DurableCommandRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                DateTimeOffset.Parse(
                    reader.GetString(5),
                    System.Globalization.CultureInfo.InvariantCulture),
                reader.GetString(6),
                reader.GetInt64(7),
                new RunBinding(
                    reader.GetString(8),
                    reader.GetString(9),
                    reader.GetString(10),
                    reader.GetString(11)),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                reader.IsDBNull(13)
                    ? null
                    : DateTimeOffset.Parse(
                        reader.GetString(13),
                        System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public RunRecord Transition(
        string durableCommandId,
        string runId,
        long expectedGeneration,
        LifecycleState target,
        long coordinatorFencingEpoch,
        string reason,
        DateTimeOffset now,
        LifecycleTransitionRecordKind recordKind = LifecycleTransitionRecordKind.Observed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableCommandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        if (recordKind is not LifecycleTransitionRecordKind.Requested
            and not LifecycleTransitionRecordKind.Observed)
        {
            throw new ArgumentOutOfRangeException(nameof(recordKind));
        }

        lock (gate)
        {
            using var transaction = BeginTransaction();
            var existing = ScalarStringOrNull(
                "SELECT run_id FROM durable_commands WHERE command_id = $id;",
                transaction,
                ("$id", durableCommandId));
            if (existing is not null)
            {
                if (!string.Equals(existing, runId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("A durable command key cannot be rebound.");
                }

                if (!string.Equals(
                        ScalarString(
                            "SELECT command_kind FROM durable_commands WHERE command_id = $id;",
                            transaction,
                            ("$id", durableCommandId)),
                        target.ToString().ToLowerInvariant(),
                        StringComparison.Ordinal)
                    || ScalarLong(
                        "SELECT expected_generation FROM durable_commands WHERE command_id = $id;",
                        transaction,
                        ("$id", durableCommandId)) != expectedGeneration)
                {
                    throw new InvalidOperationException(
                        "A durable command key cannot be rebound to different transition inputs.");
                }

                transaction.Commit();
                return GetRunCore(runId);
            }

            var current = GetRunCore(runId);
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            if (current.CoordinatorFencingEpoch > coordinatorFencingEpoch)
            {
                throw new InvalidOperationException("The coordinator fencing epoch is stale.");
            }

            if (current.Generation != expectedGeneration)
            {
                throw new InvalidOperationException(
                    $"Lifecycle generation mismatch: expected {expectedGeneration}, actual {current.Generation}.");
            }

            LifecyclePolicy.EnsureAllowed(current.State, target);
            var nextGeneration = checked(expectedGeneration + 1);
            var nextSequence = checked(current.DurableSequence + 1);
            var transitionId = Guid.NewGuid().ToString("N");
            var changed = Execute(
                """
                UPDATE runs
                SET lifecycle_state = $state,
                    lifecycle_generation = $generation,
                    coordinator_fencing_epoch = $epoch,
                    durable_sequence = $sequence,
                    updated_at = $now
                WHERE run_id = $run AND lifecycle_generation = $expected;
                """,
                transaction,
                ("$state", target.ToString()),
                ("$generation", nextGeneration),
                ("$epoch", coordinatorFencingEpoch),
                ("$sequence", nextSequence),
                ("$now", ToText(now)),
                ("$run", runId),
                ("$expected", expectedGeneration));
            if (changed != 1)
            {
                throw new InvalidOperationException("The lifecycle compare-and-swap update lost its race.");
            }

            Execute(
                """
                UPDATE job_nodes
                SET lifecycle_state = $state, lifecycle_generation = $generation, updated_at = $now
                WHERE run_id = $run AND parent_job_node_id IS NULL;
                """,
                transaction,
                ("$state", target.ToString()),
                ("$generation", nextGeneration),
                ("$now", ToText(now)),
                ("$run", runId));
            Execute(
                """
                INSERT INTO lifecycle_events(
                    transition_id, run_id, job_node_id, record_kind, policy_version,
                    from_state, to_state, expected_generation, new_generation,
                    coordinator_fencing_epoch, reason, occurred_at, durable_sequence)
                VALUES (
                    $transition, $run, $job, $record_kind, $policy, $from, $to,
                    $expected, $new, $epoch, $reason, $now, $sequence);
                """,
                transaction,
                ("$transition", transitionId),
                ("$run", runId),
                ("$job", runId + "-root"),
                ("$record_kind", recordKind.ToString().ToLowerInvariant()),
                ("$policy", LifecyclePolicy.Version),
                ("$from", current.State.ToString()),
                ("$to", target.ToString()),
                ("$expected", expectedGeneration),
                ("$new", nextGeneration),
                ("$epoch", coordinatorFencingEpoch),
                ("$reason", reason),
                ("$now", ToText(now)),
                ("$sequence", nextSequence));
            Execute(
                """
                INSERT INTO durable_commands(
                    command_id, command_kind, run_id, expected_generation,
                    disposition, resulting_state, transition_id, created_at)
                VALUES (
                    $id, $kind, $run, $expected, 'accepted', $state, $transition, $now);
                """,
                transaction,
                ("$id", durableCommandId),
                ("$kind", target.ToString().ToLowerInvariant()),
                ("$run", runId),
                ("$expected", expectedGeneration),
                ("$state", target.ToString()),
                ("$transition", transitionId),
                ("$now", ToText(now)));
            Execute(
                """
                UPDATE run_projection
                SET lifecycle_state = $state, lifecycle_generation = $generation,
                    durable_sequence = $sequence, updated_at = $now
                WHERE run_id = $run;
                """,
                transaction,
                ("$state", target.ToString()),
                ("$generation", nextGeneration),
                ("$sequence", nextSequence),
                ("$now", ToText(now)),
                ("$run", runId));
            transaction.Commit();
            return GetRunCore(runId);
        }
    }

    public AttemptRecord CreateAttempt(
        string runId,
        long coordinatorFencingEpoch,
        TimeSpan leaseDuration,
        DateTimeOffset now)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        lock (gate)
        {
            using var transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            var run = GetRunCore(runId);
            if (run.State is not (LifecycleState.Running or LifecycleState.Waiting))
            {
                throw new InvalidOperationException(
                    "An attempt may be created only for dispatched work.");
            }

            if (run.CoordinatorFencingEpoch > coordinatorFencingEpoch)
            {
                throw new InvalidOperationException("The coordinator fencing epoch is stale.");
            }

            if (ScalarLong(
                    """
                    SELECT COUNT(*)
                    FROM attempts
                    WHERE run_id = $run AND outcome = 'running';
                    """,
                    transaction,
                    ("$run", runId)) != 0)
            {
                throw new InvalidOperationException(
                    "A run may have only one live attempt.");
            }

            var generation = ScalarLong(
                "SELECT COALESCE(MAX(attempt_generation), 0) + 1 FROM attempts WHERE run_id = $run;",
                transaction,
                ("$run", runId));
            var fencingToken = ScalarLong(
                "SELECT COALESCE(MAX(attempt_fencing_token), 0) + 1 FROM attempts WHERE run_id = $run;",
                transaction,
                ("$run", runId));
            var attemptId = Guid.NewGuid().ToString("N");
            var expires = now.Add(leaseDuration);
            Execute(
                """
                INSERT INTO attempts(
                    attempt_id, run_id, job_node_id, attempt_generation,
                    coordinator_fencing_epoch, attempt_fencing_token,
                    lease_acquired_at, lease_expires_at, dispatch_identity,
                    idempotency_identity, retry_safety, outcome, created_at)
                VALUES (
                    $attempt, $run, $job, $generation, $epoch, $token, $now, $expires,
                    $dispatch, $idempotency, 'safe-with-new-attempt', 'running', $now);
                """,
                transaction,
                ("$attempt", attemptId),
                ("$run", runId),
                ("$job", runId + "-root"),
                ("$generation", generation),
                ("$epoch", coordinatorFencingEpoch),
                ("$token", fencingToken),
                ("$now", ToText(now)),
                ("$expires", ToText(expires)),
                ("$dispatch", Guid.NewGuid().ToString("N")),
                ("$idempotency", Guid.NewGuid().ToString("N")));
            transaction.Commit();
            return new AttemptRecord(
                attemptId,
                runId,
                generation,
                coordinatorFencingEpoch,
                fencingToken,
                expires,
                "running");
        }
    }

    public DispatchAdmission DispatchAttempt(
        string durableCommandId,
        string runId,
        long expectedGeneration,
        long coordinatorFencingEpoch,
        TimeSpan leaseDuration,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableCommandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        lock (gate)
        {
            using var transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            RunRecord current = GetRunCore(runId);
            if (current.CoordinatorFencingEpoch > coordinatorFencingEpoch)
            {
                throw new InvalidOperationException("The coordinator fencing epoch is stale.");
            }

            if (current.Generation != expectedGeneration)
            {
                throw new InvalidOperationException(
                    $"Lifecycle generation mismatch: expected {expectedGeneration}, actual {current.Generation}.");
            }

            LifecyclePolicy.EnsureAllowed(current.State, LifecycleState.Running);
            if (ScalarLong(
                    "SELECT COUNT(*) FROM attempts WHERE run_id = $run AND outcome = 'running';",
                    transaction,
                    ("$run", runId)) != 0)
            {
                throw new InvalidOperationException("A run may have only one live attempt.");
            }

            long nextGeneration = checked(expectedGeneration + 1);
            long nextSequence = checked(current.DurableSequence + 1);
            string transitionId = Guid.NewGuid().ToString("N");
            int changed = Execute(
                """
                UPDATE runs
                SET lifecycle_state = 'Running',
                    lifecycle_generation = $generation,
                    coordinator_fencing_epoch = $epoch,
                    durable_sequence = $sequence,
                    updated_at = $now
                WHERE run_id = $run AND lifecycle_generation = $expected;
                """,
                transaction,
                ("$generation", nextGeneration),
                ("$epoch", coordinatorFencingEpoch),
                ("$sequence", nextSequence),
                ("$now", ToText(now)),
                ("$run", runId),
                ("$expected", expectedGeneration));
            if (changed != 1)
            {
                throw new InvalidOperationException("The lifecycle compare-and-swap update lost its race.");
            }

            Execute(
                """
                UPDATE job_nodes
                SET lifecycle_state = 'Running', lifecycle_generation = $generation, updated_at = $now
                WHERE run_id = $run AND parent_job_node_id IS NULL;
                """,
                transaction,
                ("$generation", nextGeneration),
                ("$now", ToText(now)),
                ("$run", runId));
            Execute(
                """
                INSERT INTO lifecycle_events(
                    transition_id, run_id, job_node_id, record_kind, policy_version,
                    from_state, to_state, expected_generation, new_generation,
                    coordinator_fencing_epoch, reason, occurred_at, durable_sequence)
                VALUES (
                    $transition, $run, $job, 'observed', $policy, $from, 'Running',
                    $expected, $new, $epoch, 'managed worker dispatch', $now, $sequence);
                """,
                transaction,
                ("$transition", transitionId),
                ("$run", runId),
                ("$job", runId + "-root"),
                ("$policy", LifecyclePolicy.Version),
                ("$from", current.State.ToString()),
                ("$expected", expectedGeneration),
                ("$new", nextGeneration),
                ("$epoch", coordinatorFencingEpoch),
                ("$now", ToText(now)),
                ("$sequence", nextSequence));
            Execute(
                """
                INSERT INTO durable_commands(
                    command_id, command_kind, run_id, expected_generation,
                    disposition, resulting_state, transition_id, created_at)
                VALUES ($id, 'running', $run, $expected, 'accepted', 'Running', $transition, $now);
                """,
                transaction,
                ("$id", durableCommandId),
                ("$run", runId),
                ("$expected", expectedGeneration),
                ("$transition", transitionId),
                ("$now", ToText(now)));
            Execute(
                """
                UPDATE run_projection
                SET lifecycle_state = 'Running', lifecycle_generation = $generation,
                    durable_sequence = $sequence, updated_at = $now
                WHERE run_id = $run;
                """,
                transaction,
                ("$generation", nextGeneration),
                ("$sequence", nextSequence),
                ("$now", ToText(now)),
                ("$run", runId));

            long attemptGeneration = ScalarLong(
                "SELECT COALESCE(MAX(attempt_generation), 0) + 1 FROM attempts WHERE run_id = $run;",
                transaction,
                ("$run", runId));
            long fencingToken = ScalarLong(
                "SELECT COALESCE(MAX(attempt_fencing_token), 0) + 1 FROM attempts WHERE run_id = $run;",
                transaction,
                ("$run", runId));
            string attemptId = Guid.NewGuid().ToString("N");
            DateTimeOffset expires = now.Add(leaseDuration);
            Execute(
                """
                INSERT INTO attempts(
                    attempt_id, run_id, job_node_id, attempt_generation,
                    coordinator_fencing_epoch, attempt_fencing_token,
                    lease_acquired_at, lease_expires_at, dispatch_identity,
                    idempotency_identity, retry_safety, outcome, created_at)
                VALUES (
                    $attempt, $run, $job, $generation, $epoch, $token, $now, $expires,
                    $dispatch, $idempotency, 'safe-with-new-attempt', 'running', $now);
                """,
                transaction,
                ("$attempt", attemptId),
                ("$run", runId),
                ("$job", runId + "-root"),
                ("$generation", attemptGeneration),
                ("$epoch", coordinatorFencingEpoch),
                ("$token", fencingToken),
                ("$now", ToText(now)),
                ("$expires", ToText(expires)),
                ("$dispatch", Guid.NewGuid().ToString("N")),
                ("$idempotency", Guid.NewGuid().ToString("N")));
            transaction.Commit();
            RunRecord running = GetRunCore(runId);
            return new DispatchAdmission(
                running,
                new AttemptRecord(
                    attemptId,
                    runId,
                    attemptGeneration,
                    coordinatorFencingEpoch,
                    fencingToken,
                    expires,
                    "running"));
        }
    }

    public void SettleLiveAttempts(
        string runId,
        string outcome,
        long coordinatorFencingEpoch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        if (outcome.Length > 64)
        {
            throw new ArgumentException("The attempt outcome exceeds its bound.", nameof(outcome));
        }

        lock (gate)
        {
            using var transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            Execute(
                """
                UPDATE attempts
                SET outcome = $outcome
                WHERE run_id = $run AND outcome = 'running';
                """,
                transaction,
                ("$outcome", outcome),
                ("$run", runId));
            transaction.Commit();
        }
    }

    public bool HasLiveAttempts(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            return ScalarLong(
                """
                SELECT COUNT(*)
                FROM attempts
                WHERE run_id = $run AND outcome = 'running';
                """,
                transaction: null,
                ("$run", runId)) > 0;
        }
    }

    public void EnsureCandidateAttemptIsCurrent(AttemptRecord attempt, RunBinding binding)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ValidateBinding(binding);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentAttempt(attempt, transaction);
            long bindingMatches = ScalarLong(
                """
                SELECT COUNT(*) FROM runs
                WHERE run_id = $run AND installation_snapshot_id = $snapshot
                  AND analysis_context_id = $context
                  AND effective_scan_configuration_id = $config
                  AND resolved_input_manifest_id = $manifest;
                """,
                transaction,
                ("$run", attempt.RunId),
                ("$snapshot", binding.InstallationSnapshotId),
                ("$context", binding.AnalysisContextId),
                ("$config", binding.EffectiveScanConfigurationId),
                ("$manifest", binding.ResolvedInputManifestId));
            if (bindingMatches != 1)
            {
                throw new InvalidOperationException("Candidate attempt dependencies differ from the immutable run binding.");
            }
            transaction.Rollback();
        }
    }

    public void AddCheckpoint(
        string checkpointId,
        AttemptRecord attempt,
        RunBinding binding,
        string dependencyClosureId,
        string contentSha256,
        string completedPartitionsJson,
        string pendingAndGapsJson,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyClosureId);
        ValidateBinding(binding);
        ValidateSha256(contentSha256);
        ValidateBoundedJson(completedPartitionsJson, nameof(completedPartitionsJson));
        ValidateBoundedJson(pendingAndGapsJson, nameof(pendingAndGapsJson));
        lock (gate)
        {
            using var transaction = BeginTransaction();
            EnsureCurrentAttempt(attempt, transaction);
            long bindingMatches = ScalarLong(
                """
                SELECT COUNT(*)
                FROM runs
                WHERE run_id = $run
                  AND installation_snapshot_id = $snapshot
                  AND analysis_context_id = $context
                  AND effective_scan_configuration_id = $config
                  AND resolved_input_manifest_id = $manifest;
                """,
                transaction,
                ("$run", attempt.RunId),
                ("$snapshot", binding.InstallationSnapshotId),
                ("$context", binding.AnalysisContextId),
                ("$config", binding.EffectiveScanConfigurationId),
                ("$manifest", binding.ResolvedInputManifestId));
            if (bindingMatches != 1)
            {
                throw new InvalidOperationException(
                    "Checkpoint dependencies must equal the immutable run binding.");
            }

            Execute(
                """
                INSERT OR IGNORE INTO checkpoints(
                    checkpoint_id, run_id, attempt_id, installation_snapshot_id,
                    analysis_context_id, effective_scan_configuration_id,
                    resolved_input_manifest_id, dependency_closure_id,
                    content_sha256, completed_partitions_json,
                    pending_and_gaps_json, created_at)
                VALUES (
                    $checkpoint, $run, $attempt, $snapshot, $context, $config, $manifest,
                    $dependency, $sha, $completed, $pending, $now);
                """,
                transaction,
                ("$checkpoint", checkpointId),
                ("$run", attempt.RunId),
                ("$attempt", attempt.AttemptId),
                ("$snapshot", binding.InstallationSnapshotId),
                ("$context", binding.AnalysisContextId),
                ("$config", binding.EffectiveScanConfigurationId),
                ("$manifest", binding.ResolvedInputManifestId),
                ("$dependency", dependencyClosureId),
                ("$sha", contentSha256),
                ("$completed", completedPartitionsJson),
                ("$pending", pendingAndGapsJson),
                ("$now", ToText(now)));
            long checkpointMatches = ScalarLong(
                """
                SELECT COUNT(*) FROM checkpoints
                WHERE checkpoint_id = $checkpoint AND run_id = $run AND attempt_id = $attempt
                  AND installation_snapshot_id = $snapshot AND analysis_context_id = $context
                  AND effective_scan_configuration_id = $config
                  AND resolved_input_manifest_id = $manifest AND dependency_closure_id = $dependency
                  AND content_sha256 = $sha AND completed_partitions_json = $completed
                  AND pending_and_gaps_json = $pending;
                """,
                transaction,
                ("$checkpoint", checkpointId),
                ("$run", attempt.RunId),
                ("$attempt", attempt.AttemptId),
                ("$snapshot", binding.InstallationSnapshotId),
                ("$context", binding.AnalysisContextId),
                ("$config", binding.EffectiveScanConfigurationId),
                ("$manifest", binding.ResolvedInputManifestId),
                ("$dependency", dependencyClosureId),
                ("$sha", contentSha256),
                ("$completed", completedPartitionsJson),
                ("$pending", pendingAndGapsJson));
            if (checkpointMatches != 1)
            {
                throw new InvalidOperationException("A checkpoint identity resolves to different retained state.");
            }
            Execute(
                """
                INSERT OR IGNORE INTO audit_events(
                    audit_event_id, event_kind, object_kind, object_id,
                    detail_payload_id, occurred_at)
                VALUES ($id, 'checkpoint-recorded', 'checkpoint', $object, NULL, $now);
                """,
                transaction,
                ("$id", "checkpoint-recorded-" + checkpointId),
                ("$object", checkpointId),
                ("$now", ToText(now)));
            transaction.Commit();
        }
    }

    public PayloadAdmission AdmitStagedPayload(
        AttemptRecord attempt,
        string stagedRelativePath,
        string expectedSha256,
        long expectedByteLength,
        string expectedManifestSha256,
        long maximumBytes,
        DateTimeOffset now,
        string? completionCommandId = null,
        string? stagedArtifactId = null)
    {
        if (completionCommandId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(completionCommandId);
        }

        if (stagedArtifactId is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stagedArtifactId);
        }

        ValidateSha256(expectedSha256);
        ValidateSha256(expectedManifestSha256);
        if (expectedByteLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedByteLength));
        }

        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        if (expectedByteLength > maximumBytes)
        {
            throw new InvalidOperationException("The declared staged output exceeds its bound.");
        }

        string stagingRelativePath = Path.Combine(
            attempt.AttemptId,
            stagedRelativePath);
        using WindowsHandleRelativeStorage.AdmissionSource staged =
            Paths.OpenAdmissionSource(
                ProductWriteClass.AttemptStaging,
                stagingRelativePath);
        var payloadId = Guid.NewGuid().ToString("N");
        var payloadClassRelativePath = Path.Combine(
            expectedSha256[..2],
            expectedSha256[2..4],
            expectedSha256);
        var relativeObjectPath = Path.Combine("payloads", payloadClassRelativePath);
        lock (gate)
        {
            using var transaction = BeginTransaction();
            EnsureCurrentAttempt(attempt, transaction);
            WindowsHandleRelativeStorage.AdmissionCopyResult copied =
                Paths.PublishAdmissionSource(
                    staged,
                    payloadClassRelativePath,
                    expectedSha256,
                    expectedByteLength,
                    maximumBytes);
            string actualSha = copied.Sha256;
            long stagedLength = copied.ByteLength;

            var receiptId = Guid.NewGuid().ToString("N");
            Execute(
                """
                INSERT INTO payloads(
                    payload_id, content_sha256, byte_length, codec, retention_state,
                    object_relative_path, admitted_at)
                VALUES ($payload, $sha, $length, 'identity', 'retained', $path, $now)
                ON CONFLICT(content_sha256) DO NOTHING;
                """,
                transaction,
                ("$payload", payloadId),
                ("$sha", actualSha),
                ("$length", stagedLength),
                ("$path", relativeObjectPath.Replace('\\', '/')),
                ("$now", ToText(now)));
            var admittedPayloadId = ScalarString(
                "SELECT payload_id FROM payloads WHERE content_sha256 = $sha;",
                transaction,
                ("$sha", actualSha));
            Execute(
                """
                INSERT OR IGNORE INTO payload_owners(payload_id, owner_kind, owner_id)
                VALUES ($payload, 'attempt', $attempt);
                """,
                transaction,
                ("$payload", admittedPayloadId),
                ("$attempt", attempt.AttemptId));
            Execute(
                """
                INSERT INTO publication_receipts(
                    receipt_id, run_id, attempt_id, coordinator_fencing_epoch,
                    attempt_fencing_token, staged_manifest_sha256, published_at)
                VALUES ($receipt, $run, $attempt, $epoch, $token, $sha, $now);
                """,
                transaction,
                ("$receipt", receiptId),
                ("$run", attempt.RunId),
                ("$attempt", attempt.AttemptId),
                ("$epoch", attempt.CoordinatorFencingEpoch),
                ("$token", attempt.AttemptFencingToken),
                ("$sha", expectedManifestSha256),
                ("$now", ToText(now)));
            Execute(
                """
                INSERT INTO publication_receipt_payloads(receipt_id, payload_id)
                VALUES ($receipt, $payload);
                """,
                transaction,
                ("$receipt", receiptId),
                ("$payload", admittedPayloadId));
            Execute(
                "UPDATE attempts SET outcome = 'completed-staged' WHERE attempt_id = $attempt;",
                transaction,
                ("$attempt", attempt.AttemptId));
            Execute(
                """
                INSERT INTO audit_events(
                    audit_event_id, event_kind, object_kind, object_id,
                    detail_payload_id, occurred_at)
                VALUES ($id, 'attempt-staging-accepted', 'attempt', $object, NULL, $now);
                """,
                transaction,
                ("$id", Guid.NewGuid().ToString("N")),
                ("$object", attempt.AttemptId),
                ("$now", ToText(now)));
            if (stagedArtifactId is not null)
            {
                Execute(
                    """
                    INSERT INTO audit_events(
                        audit_event_id, event_kind, object_kind, object_id,
                        detail_payload_id, occurred_at)
                    VALUES ($id, 'staged-artifact-accepted', 'artifact', $object, NULL, $now);
                    """,
                    transaction,
                    ("$id", Guid.NewGuid().ToString("N")),
                    ("$object", stagedArtifactId),
                    ("$now", ToText(now)));
            }

            Execute(
                """
                INSERT INTO audit_events(
                    audit_event_id, event_kind, object_kind, object_id,
                    detail_payload_id, occurred_at)
                VALUES ($id, 'payload-published', 'payload', $object, $payload, $now);
                """,
                transaction,
                ("$id", Guid.NewGuid().ToString("N")),
                ("$object", admittedPayloadId),
                ("$payload", admittedPayloadId),
                ("$now", ToText(now)));

            if (completionCommandId is not null)
            {
                RunRecord current = GetRunCore(attempt.RunId);
                LifecyclePolicy.EnsureAllowed(current.State, LifecycleState.Completed);
                long nextGeneration = checked(current.Generation + 1);
                long nextSequence = checked(current.DurableSequence + 1);
                string transitionId = Guid.NewGuid().ToString("N");
                int changed = Execute(
                    """
                    UPDATE runs
                    SET lifecycle_state = 'Completed',
                        lifecycle_generation = $generation,
                        coordinator_fencing_epoch = $epoch,
                        durable_sequence = $sequence,
                        updated_at = $now
                    WHERE run_id = $run AND lifecycle_generation = $expected;
                    """,
                    transaction,
                    ("$generation", nextGeneration),
                    ("$epoch", attempt.CoordinatorFencingEpoch),
                    ("$sequence", nextSequence),
                    ("$now", ToText(now)),
                    ("$run", attempt.RunId),
                    ("$expected", current.Generation));
                if (changed != 1)
                {
                    throw new InvalidOperationException(
                        "The publication lifecycle compare-and-swap update lost its race.");
                }

                Execute(
                    """
                    UPDATE job_nodes
                    SET lifecycle_state = 'Completed', lifecycle_generation = $generation, updated_at = $now
                    WHERE run_id = $run AND parent_job_node_id IS NULL;
                    """,
                    transaction,
                    ("$generation", nextGeneration),
                    ("$now", ToText(now)),
                    ("$run", attempt.RunId));
                Execute(
                    """
                    INSERT INTO lifecycle_events(
                        transition_id, run_id, job_node_id, record_kind, policy_version,
                        from_state, to_state, expected_generation, new_generation,
                        coordinator_fencing_epoch, reason, occurred_at, durable_sequence)
                    VALUES (
                        $transition, $run, $job, 'observed', $policy, $from, 'Completed',
                        $expected, $new, $epoch,
                        'managed worker output admitted and published', $now, $sequence);
                    """,
                    transaction,
                    ("$transition", transitionId),
                    ("$run", attempt.RunId),
                    ("$job", attempt.RunId + "-root"),
                    ("$policy", LifecyclePolicy.Version),
                    ("$from", current.State.ToString()),
                    ("$expected", current.Generation),
                    ("$new", nextGeneration),
                    ("$epoch", attempt.CoordinatorFencingEpoch),
                    ("$now", ToText(now)),
                    ("$sequence", nextSequence));
                Execute(
                    """
                    INSERT INTO durable_commands(
                        command_id, command_kind, run_id, expected_generation,
                        disposition, resulting_state, transition_id, created_at)
                    VALUES (
                        $id, 'completed', $run, $expected, 'accepted',
                        'Completed', $transition, $now);
                    """,
                    transaction,
                    ("$id", completionCommandId),
                    ("$run", attempt.RunId),
                    ("$expected", current.Generation),
                    ("$transition", transitionId),
                    ("$now", ToText(now)));
                Execute(
                    """
                    UPDATE run_projection
                    SET lifecycle_state = 'Completed', lifecycle_generation = $generation,
                        durable_sequence = $sequence, updated_at = $now
                    WHERE run_id = $run;
                    """,
                    transaction,
                    ("$generation", nextGeneration),
                    ("$sequence", nextSequence),
                    ("$now", ToText(now)),
                    ("$run", attempt.RunId));
            }

            transaction.Commit();
            staged.Delete();
            return new PayloadAdmission(
                admittedPayloadId,
                actualSha,
                stagedLength,
                relativeObjectPath.Replace('\\', '/'),
                receiptId,
                expectedManifestSha256);
        }
    }

    private RunRecord GetRunCore(string runId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT run_id, installation_snapshot_id, analysis_context_id,
                   effective_scan_configuration_id, resolved_input_manifest_id,
                   lifecycle_state, lifecycle_generation, coordinator_fencing_epoch,
                   durable_sequence, created_at, updated_at
            FROM runs WHERE run_id = $run;
            """;
        command.Parameters.AddWithValue("$run", runId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException($"Run '{runId}' does not exist.");
        }

        return ReadRun(reader);
    }

    private static RunRecord ReadRun(SqliteDataReader reader) =>
        new(
            reader.GetString(0),
            new RunBinding(reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4)),
            Enum.Parse<LifecycleState>(reader.GetString(5), ignoreCase: false),
            reader.GetInt64(6),
            reader.GetInt64(7),
            reader.GetInt64(8),
            DateTimeOffset.Parse(reader.GetString(9), System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(reader.GetString(10), System.Globalization.CultureInfo.InvariantCulture));

    private void EnsureCurrentAttempt(AttemptRecord attempt, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(*)
            FROM attempts a
            JOIN runs r ON r.run_id = a.run_id
            WHERE a.attempt_id = $attempt
              AND a.run_id = $run
              AND a.coordinator_fencing_epoch = $epoch
              AND a.attempt_fencing_token = $token
              AND a.attempt_fencing_token = (
                  SELECT MAX(newest.attempt_fencing_token)
                  FROM attempts newest
                  WHERE newest.run_id = a.run_id)
              AND a.lease_expires_at >= $now
              AND r.coordinator_fencing_epoch <= $epoch
              AND a.coordinator_fencing_epoch = (
                  SELECT CAST(value AS INTEGER)
                  FROM store_metadata
                  WHERE key = 'active_coordinator_epoch')
              AND r.lifecycle_state IN ('Running','Waiting')
              AND a.outcome = 'running';
            """;
        command.Parameters.AddWithValue("$attempt", attempt.AttemptId);
        command.Parameters.AddWithValue("$run", attempt.RunId);
        command.Parameters.AddWithValue("$epoch", attempt.CoordinatorFencingEpoch);
        command.Parameters.AddWithValue("$token", attempt.AttemptFencingToken);
        command.Parameters.AddWithValue("$now", ToText(DateTimeOffset.UtcNow));
        if (Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException("The worker attempt is stale, expired, or already settled.");
        }
    }

    private void EnsureRunExists(string runId, SqliteTransaction transaction)
    {
        if (ScalarLong(
                "SELECT COUNT(*) FROM runs WHERE run_id = $run;",
                transaction,
                ("$run", runId)) != 1)
        {
            throw new InvalidOperationException($"Documentation evidence references unknown run '{runId}'.");
        }
    }

}

#pragma warning restore CA1869
#pragma warning restore CA1512
#pragma warning restore IDE0008
