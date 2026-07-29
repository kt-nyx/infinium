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

public sealed class AuthoritativeStore : IDisposable
{
    public const int CurrentSchemaVersion = 1;
    private const string CurrentStorageContractVersion = "1.0.0";
    private const int MaximumBackupManifestBytes = 16 * 1024 * 1024;
    private const int MaximumCheckpointJsonBytes = 64 * 1024;

    private readonly Lock gate = new();
    private readonly SqliteConnection connection;
    private readonly WindowsGuardedSqliteVfs sqliteVfs;
    private bool disposed;

    public AuthoritativeStore(StoragePaths paths)
    {
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        try
        {
            Paths.Create();
            _ = Paths.ResolveProductPath(ProductWriteClass.Data, "infinium.sqlite3");
            SqliteRuntimeIdentity.InitializeNativeProvider();
            sqliteVfs = new WindowsGuardedSqliteVfs(
                Paths,
                ProductWriteClass.Data,
                "infinium.sqlite3");
        }
        catch
        {
            Paths.Dispose();
            throw;
        }

        try
        {
            connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = Paths.Database,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                Vfs = sqliteVfs.Name,
            }.ToString());
            connection.Open();
            BindingIdentity = SqliteRuntimeIdentity.VerifyExactPatchedBinding(connection);
            ConfigureConnection(connection);
            WindowsGuardedSqliteVfs.EnablePersistentWal(connection);
            ApplyMigrations();
            ValidateDatabaseIdentityAndIntegrity(connection, BindingIdentity);
            sqliteVfs.VerifyAllGuards();
            RecordWriteClassAuthorityBindings(DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            connection?.Dispose();
            Exception? callbackError = sqliteVfs.LastCallbackError;
            string? callbackDetail = sqliteVfs.LastCallbackDetail;
            sqliteVfs.Dispose();
            Paths.Dispose();
            if (callbackError is not null)
            {
                throw new InvalidOperationException(
                    "The guarded SQLite VFS rejected a database operation.",
                    callbackError);
            }

            if (callbackDetail is not null)
            {
                throw new InvalidOperationException(
                    $"The guarded SQLite VFS failed after '{callbackDetail}'.",
                    exception);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    public StoragePaths Paths { get; }
    public SqliteBindingIdentity BindingIdentity { get; }

    public void RecordAuditEvent(
        string eventKind,
        string objectKind,
        string objectId,
        DateTimeOffset now)
    {
        ValidateAuditToken(eventKind, nameof(eventKind));
        ValidateAuditToken(objectKind, nameof(objectKind));
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        if (Encoding.UTF8.GetByteCount(objectId) > 512)
        {
            throw new ArgumentException("The audit object identity exceeds its bound.", nameof(objectId));
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            InsertAuditEvent(eventKind, objectKind, objectId, now, transaction);
            transaction.Commit();
        }
    }

    public CoordinatorAuthority AcquireCoordinatorAuthority(
        string instanceId,
        DateTimeOffset now,
        TimeSpan leaseDuration)
    {
        return AcquireCoordinatorAuthorityCore(
            instanceId,
            now,
            leaseDuration,
            allowUnexpiredTakeoverAfterProcessExclusion: false);
    }

    public CoordinatorAuthority AcquireCoordinatorAuthorityAfterProcessExclusion(
        string instanceId,
        DateTimeOffset now,
        TimeSpan leaseDuration)
    {
        return AcquireCoordinatorAuthorityCore(
            instanceId,
            now,
            leaseDuration,
            allowUnexpiredTakeoverAfterProcessExclusion: true);
    }

    private CoordinatorAuthority AcquireCoordinatorAuthorityCore(
        string instanceId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        bool allowUnexpiredTakeoverAfterProcessExclusion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        lock (gate)
        {
            using var transaction = BeginTransaction();
            long unexpiredActiveLease = ScalarLong(
                """
                SELECT COUNT(*)
                FROM coordinator_leases lease
                JOIN store_metadata metadata
                  ON metadata.key = 'active_coordinator_epoch'
                 AND CAST(metadata.value AS INTEGER) = lease.fencing_epoch
                WHERE lease.expires_at > $now;
                """,
                transaction,
                ("$now", ToText(now)));
            if (unexpiredActiveLease != 0
                && !allowUnexpiredTakeoverAfterProcessExclusion)
            {
                throw new InvalidOperationException(
                    "An unexpired coordinator authority already owns the store.");
            }

            var epoch = ScalarLong(
                "SELECT COALESCE(MAX(fencing_epoch), 0) + 1 FROM coordinator_leases;",
                transaction);
            var expires = now.Add(leaseDuration);
            Execute(
                """
                INSERT INTO coordinator_leases(
                    coordinator_instance_id, fencing_epoch, acquired_at, expires_at)
                VALUES ($instance, $epoch, $acquired, $expires);
                """,
                transaction,
                ("$instance", instanceId),
                ("$epoch", epoch),
                ("$acquired", ToText(now)),
                ("$expires", ToText(expires)));
            Execute(
                """
                INSERT INTO store_metadata(key, value) VALUES ('active_coordinator_epoch', $epoch)
                ON CONFLICT(key) DO UPDATE SET value = excluded.value;
                """,
                transaction,
                ("$epoch", epoch.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            transaction.Commit();
            return new CoordinatorAuthority(instanceId, epoch, expires);
        }
    }

    public CoordinatorAuthority RenewCoordinatorAuthority(
        long currentFencingEpoch,
        DateTimeOffset now,
        TimeSpan leaseDuration)
    {
        RequirePositive(currentFencingEpoch, nameof(currentFencingEpoch));
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        DateTimeOffset requestedExpiry = now.Add(leaseDuration);
        lock (gate)
        {
            using var transaction = BeginTransaction();
            int updated = Execute(
                """
                UPDATE coordinator_leases
                SET expires_at = CASE
                    WHEN expires_at > $expires THEN expires_at
                    ELSE $expires
                END
                WHERE fencing_epoch = $epoch
                  AND expires_at > $now
                  AND EXISTS (
                      SELECT 1
                      FROM store_metadata
                      WHERE key = 'active_coordinator_epoch'
                        AND CAST(value AS INTEGER) = $epoch
                  );
                """,
                transaction,
                ("$epoch", currentFencingEpoch),
                ("$now", ToText(now)),
                ("$expires", ToText(requestedExpiry)));
            if (updated != 1)
            {
                throw new InvalidOperationException(
                    "Only the current unexpired coordinator authority may renew its lease.");
            }

            string instanceId = ScalarString(
                """
                SELECT coordinator_instance_id
                FROM coordinator_leases
                WHERE fencing_epoch = $epoch;
                """,
                transaction,
                ("$epoch", currentFencingEpoch));
            DateTimeOffset expiresAt = DateTimeOffset.Parse(
                ScalarString(
                    """
                    SELECT expires_at
                    FROM coordinator_leases
                    WHERE fencing_epoch = $epoch;
                    """,
                    transaction,
                    ("$epoch", currentFencingEpoch)),
                System.Globalization.CultureInfo.InvariantCulture);
            transaction.Commit();
            return new CoordinatorAuthority(instanceId, currentFencingEpoch, expiresAt);
        }
    }

    public RunRecord CreateRun(
        string durableCommandId,
        string runId,
        RunBinding binding,
        long coordinatorFencingEpoch,
        DateTimeOffset now,
        string? startInitiationKind = null,
        DateTimeOffset? startDispatchDeadline = null)
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
            transaction.Commit();
            return GetRunCore(runId);
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
                INSERT INTO checkpoints(
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
            Execute(
                """
                INSERT INTO audit_events(
                    audit_event_id, event_kind, object_kind, object_id,
                    detail_payload_id, occurred_at)
                VALUES ($id, 'checkpoint-recorded', 'checkpoint', $object, NULL, $now);
                """,
                transaction,
                ("$id", Guid.NewGuid().ToString("N")),
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

        var stagingPath = Paths.ResolveProductPath(
            ProductWriteClass.AttemptStaging,
            Path.Combine(attempt.AttemptId, stagedRelativePath));
        if (!File.Exists(stagingPath))
        {
            throw new FileNotFoundException("The declared staged output does not exist.", stagingPath);
        }

        var fileInfo = new FileInfo(stagingPath);
        if (fileInfo.Length > maximumBytes)
        {
            throw new InvalidOperationException("The staged output exceeds its declared bound.");
        }

        if (fileInfo.Length != expectedByteLength)
        {
            throw new InvalidOperationException(
                "The staged output byte length does not match its manifest.");
        }

        var actualSha = HashFile(stagingPath);
        if (!string.Equals(actualSha, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The staged output fingerprint does not match its manifest.");
        }

        var payloadId = Guid.NewGuid().ToString("N");
        var payloadClassRelativePath = Path.Combine(
            actualSha[..2],
            actualSha[2..4],
            actualSha);
        var relativeObjectPath = Path.Combine("payloads", payloadClassRelativePath);
        var objectPath = Paths.ResolveProductPath(
            ProductWriteClass.Payload,
            payloadClassRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!);

        lock (gate)
        {
            using var transaction = BeginTransaction();
            EnsureCurrentAttempt(attempt, transaction);
            if (!File.Exists(objectPath))
            {
                File.Move(stagingPath, objectPath);
            }
            else
            {
                var existing = new FileInfo(objectPath);
                if (existing.Length != fileInfo.Length
                    || !string.Equals(HashFile(objectPath), actualSha, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A conflicting object already occupies the content-addressed path.");
                }

                File.Delete(stagingPath);
            }

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
                ("$length", fileInfo.Length),
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
            return new PayloadAdmission(
                admittedPayloadId,
                actualSha,
                fileInfo.Length,
                relativeObjectPath.Replace('\\', '/'),
                receiptId,
                expectedManifestSha256);
        }
    }

    public bool HasRecoverablePublication(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT p.content_sha256, p.byte_length, p.object_relative_path
                FROM publication_receipts receipt
                JOIN attempts attempt ON attempt.attempt_id = receipt.attempt_id
                JOIN publication_receipt_payloads published
                  ON published.receipt_id = receipt.receipt_id
                JOIN payloads p ON p.payload_id = published.payload_id
                WHERE receipt.run_id = $run
                  AND attempt.outcome = 'completed-staged'
                ORDER BY receipt.published_at, p.payload_id;
                """;
            command.Parameters.AddWithValue("$run", runId);
            using var reader = command.ExecuteReader();
            bool found = false;
            while (reader.Read())
            {
                found = true;
                string expectedSha256 = reader.GetString(0);
                long expectedByteLength = reader.GetInt64(1);
                string relativePath = reader.GetString(2);
                string objectPath = Paths.ResolveProductPath(
                    ProductWriteClass.Payload,
                    relativePath["payloads/".Length..]
                        .Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(objectPath))
                {
                    return false;
                }

                var fileInfo = new FileInfo(objectPath);
                if (fileInfo.Length != expectedByteLength
                    || !string.Equals(
                        HashFile(objectPath),
                        expectedSha256,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return found;
        }
    }

    public IReadOnlyList<ReconciliationIssue> ReconcilePayloadStore()
    {
        lock (gate)
        {
            var issues = new List<ReconciliationIssue>();
            var known = new Dictionary<string, (string Sha, long Length)>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT object_relative_path, content_sha256, byte_length FROM payloads;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    known[reader.GetString(0).Replace('/', Path.DirectorySeparatorChar)] =
                        (reader.GetString(1), reader.GetInt64(2));
                }
            }

            foreach (var entry in known)
            {
                var fullPath = Paths.ResolveProductPath(
                    ProductWriteClass.Payload,
                    entry.Key["payloads".Length..]
                        .TrimStart(Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    issues.Add(new ReconciliationIssue("missing-payload", entry.Key, "Registered payload is absent."));
                    continue;
                }

                var info = new FileInfo(fullPath);
                if (info.Length != entry.Value.Length
                    || !string.Equals(HashFile(fullPath), entry.Value.Sha, StringComparison.Ordinal))
                {
                    issues.Add(new ReconciliationIssue("corrupt-payload", entry.Key, "Size or digest mismatch."));
                }
            }

            if (Directory.Exists(Paths.Payloads))
            {
                foreach (var file in Directory.EnumerateFiles(Paths.Payloads, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(Paths.ProductRoot, file);
                    if (!known.ContainsKey(relative))
                    {
                        issues.Add(new ReconciliationIssue("orphan-payload", relative, "No authoritative owner."));
                    }
                }
            }

            foreach (var file in Directory.EnumerateFiles(Paths.Staging, "*", SearchOption.AllDirectories))
            {
                issues.Add(new ReconciliationIssue(
                    "orphan-staging",
                    Path.GetRelativePath(Paths.ProductRoot, file),
                    "Staging data is never authoritative."));
            }

            return issues;
        }
    }

    public void RebuildProjections(DateTimeOffset now)
    {
        lock (gate)
        {
            using var transaction = BeginTransaction();
            Execute("DELETE FROM run_projection;", transaction);
            Execute(
                """
                INSERT INTO run_projection(
                    run_id, lifecycle_state, lifecycle_generation, durable_sequence,
                    projection_version, updated_at)
                SELECT run_id, lifecycle_state, lifecycle_generation, durable_sequence,
                       1, $now
                FROM runs;
                """,
                transaction,
                ("$now", ToText(now)));
            transaction.Commit();
        }
    }

    public BackupArtifact CreateBackup(string label, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var safeLabel = string.Concat(label.Where(char.IsLetterOrDigit));
        if (safeLabel.Length is 0 or > 48)
        {
            throw new ArgumentException("The backup label must contain 1-48 letters or digits.", nameof(label));
        }

        lock (gate)
        {
            var stamp = now.UtcDateTime.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture);
            string backupDatabaseName = $"{stamp}-{safeLabel}.sqlite3";
            var databasePath = Paths.ResolveProductPath(
                ProductWriteClass.Backup,
                backupDatabaseName);
            using (FileStream reservation = new(
                databasePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                reservation.Flush(flushToDisk: true);
            }
            using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWrite,
                Pooling = false,
            }.ToString()))
            {
                destination.Open();
                sqliteVfs.VerifyAllGuards();
                connection.BackupDatabase(destination);
            }

            var databaseSha = HashFile(databasePath);
            var payloads = new List<BackupPayloadManifest>();
            var payloadBackupRoot = databasePath + ".payloads";
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT content_sha256, byte_length, object_relative_path FROM payloads ORDER BY content_sha256;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var relativePath = reader.GetString(2);
                    var sourcePath = Paths.ResolveProductPath(
                        ProductWriteClass.Payload,
                        relativePath["payloads/".Length..]
                            .Replace('/', Path.DirectorySeparatorChar));
                    var backupPayloadPath = Paths.ResolveProductPath(
                        ProductWriteClass.Backup,
                        Path.Combine(
                            backupDatabaseName + ".payloads",
                            relativePath["payloads/".Length..]
                                .Replace('/', Path.DirectorySeparatorChar)));
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPayloadPath)!);
                    File.Copy(sourcePath, backupPayloadPath, overwrite: false);
                    payloads.Add(new BackupPayloadManifest(
                        reader.GetString(0),
                        reader.GetInt64(1),
                        relativePath));
                }
            }

            var manifestPath = Paths.ResolveProductPath(
                ProductWriteClass.Backup,
                backupDatabaseName + ".manifest.json");
            var manifest = JsonSerializer.SerializeToUtf8Bytes(
                new BackupManifest(
                    CurrentSchemaVersion,
                    BindingIdentity,
                    databaseSha,
                    payloads,
                    now),
                new JsonSerializerOptions { WriteIndented = true });
            using (FileStream manifestStream = new(
                manifestPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                manifestStream.Write(manifest);
                manifestStream.Flush(flushToDisk: true);
            }
            using (SqliteTransaction transaction = BeginTransaction())
            {
                InsertAuditEvent(
                    "backup-created",
                    "backup",
                    Path.GetFileName(databasePath),
                    now,
                    transaction);
                transaction.Commit();
            }
            return new BackupArtifact(databasePath, manifestPath, databaseSha);
        }
    }

    public static void RestoreBackup(BackupArtifact backup, StoragePaths target)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(target);
        if (Directory.Exists(target.ProductRoot) || File.Exists(target.ProductRoot))
        {
            throw new InvalidOperationException("Restore requires an absent target product root.");
        }

        ValidatedBackup validated = ValidateBackup(backup);
        string? targetParent = Directory.GetParent(target.ProductRoot)?.FullName;
        if (string.IsNullOrWhiteSpace(targetParent))
        {
            throw new InvalidOperationException("The restore target must have a parent directory.");
        }
        if (!Directory.Exists(targetParent))
        {
            throw new InvalidOperationException(
                "The restore target parent must already exist and be selected explicitly.");
        }
        string stagingRoot = Path.Combine(
            targetParent,
            $".{Path.GetFileName(target.ProductRoot)}.restore-{Guid.NewGuid():N}.tmp");
        StoragePaths staging = new(stagingRoot);
        bool published = false;
        try
        {
            staging.Create();
            File.Copy(backup.DatabasePath, staging.Database, overwrite: false);
            string backupPayloadRoot = backup.DatabasePath + ".payloads";
            foreach (BackupPayloadManifest payload in validated.Manifest.Payloads)
            {
                string source = PayloadPath(backupPayloadRoot, payload.Sha256);
                string destination = staging.ResolveProductPath(
                    ProductWriteClass.Payload,
                    payload.RelativePath["payloads/".Length..]
                        .Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite: false);
            }

            if (!string.Equals(
                    HashFile(staging.Database),
                    validated.Manifest.DatabaseSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The staged restore database fingerprint is invalid.");
            }

            AppendRestoreAudit(
                staging.Database,
                target.AuthorityIdentity,
                DateTimeOffset.UtcNow);
            IReadOnlyList<BackupPayloadManifest> stagedPayloads =
                ValidateDatabaseFile(staging.Database, validated.Manifest.Sqlite);
            ValidateManifestPayloadSet(validated.Manifest.Payloads, stagedPayloads);
            ValidatePayloadFiles(staging.Payloads, validated.Manifest.Payloads);
            staging.Dispose();
            Directory.Move(stagingRoot, target.ProductRoot);
            published = true;
        }
        finally
        {
            staging.Dispose();
            if (!published && Directory.Exists(stagingRoot))
            {
                Directory.Delete(stagingRoot, recursive: true);
            }
        }
    }

    public int GetSchemaVersion()
    {
        lock (gate)
        {
            return checked((int)ScalarLong(
                "SELECT CAST(value AS INTEGER) FROM store_metadata WHERE key = 'schema_version';",
                transaction: null));
        }
    }

    public IReadOnlyList<string> GetTableNames()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            using var reader = command.ExecuteReader();
            var result = new List<string>();
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }

            return result;
        }
    }

    public void Dispose()
    {
        if (!disposed)
        {
            connection.Dispose();
            sqliteVfs.Dispose();
            Paths.Dispose();
            disposed = true;
        }
    }

    private static ValidatedBackup ValidateBackup(BackupArtifact backup)
    {
        if (!File.Exists(backup.DatabasePath) || !File.Exists(backup.ManifestPath))
        {
            throw new InvalidOperationException("The backup database or manifest is missing.");
        }

        long manifestLength = new FileInfo(backup.ManifestPath).Length;
        if (manifestLength is <= 0 or > MaximumBackupManifestBytes)
        {
            throw new InvalidOperationException("The backup manifest exceeds its finite bound.");
        }

        BackupManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(
                File.ReadAllBytes(backup.ManifestPath),
                new JsonSerializerOptions { MaxDepth = 32 })
                ?? throw new InvalidOperationException("The backup manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The backup manifest is malformed.", exception);
        }

        if (manifest.SchemaVersion != CurrentSchemaVersion
            || manifest.Sqlite is null
            || manifest.Sqlite.CompileOptions is null
            || manifest.Payloads is null)
        {
            throw new InvalidOperationException(
                "The backup manifest schema or SQLite identity is incompatible.");
        }

        ValidateManifestBinding(manifest.Sqlite);
        if (!IsCanonicalSha256(manifest.DatabaseSha256)
            || !IsCanonicalSha256(backup.Sha256))
        {
            throw new InvalidOperationException(
                "The backup database fingerprint is not canonically encoded.");
        }

        string actualDatabaseSha = HashFile(backup.DatabasePath);
        if (!string.Equals(actualDatabaseSha, backup.Sha256, StringComparison.Ordinal)
            || !string.Equals(
                actualDatabaseSha,
                manifest.DatabaseSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The backup database fingerprint is invalid.");
        }

        IReadOnlyList<BackupPayloadManifest> databasePayloads =
            ValidateDatabaseFile(backup.DatabasePath, manifest.Sqlite);
        ValidateManifestPayloadSet(manifest.Payloads, databasePayloads);
        ValidatePayloadFiles(backup.DatabasePath + ".payloads", manifest.Payloads);
        return new ValidatedBackup(manifest);
    }

    private static List<BackupPayloadManifest> ValidateDatabaseFile(
        string databasePath,
        SqliteBindingIdentity expectedBinding)
    {
        SqliteRuntimeIdentity.InitializeNativeProvider();
        using var database = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString());
        try
        {
            database.Open();
            SqliteBindingIdentity actualBinding =
                SqliteRuntimeIdentity.VerifyExactPatchedBinding(database);
            if (!BindingEquals(actualBinding, expectedBinding))
            {
                throw new InvalidOperationException(
                    "The backup SQLite binding identity does not match the current runtime.");
            }

            ValidateDatabaseIdentityAndIntegrity(database, actualBinding);
            return ReadDatabasePayloads(database);
        }
        catch (SqliteException exception)
        {
            throw new InvalidOperationException(
                "The backup database failed SQLite validation.",
                exception);
        }
    }

    private static void ValidateDatabaseIdentityAndIntegrity(
        SqliteConnection database,
        SqliteBindingIdentity binding)
    {
        using (var integrity = database.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            using SqliteDataReader reader = integrity.ExecuteReader();
            if (!reader.Read()
                || !string.Equals(reader.GetString(0), "ok", StringComparison.Ordinal)
                || reader.Read())
            {
                throw new InvalidOperationException("The database failed SQLite integrity validation.");
            }
        }

        using (var foreignKeys = database.CreateCommand())
        {
            foreignKeys.CommandText = "PRAGMA foreign_key_check;";
            using SqliteDataReader reader = foreignKeys.ExecuteReader();
            if (reader.Read())
            {
                throw new InvalidOperationException("The database failed foreign-key validation.");
            }
        }

        using (var version = database.CreateCommand())
        {
            version.CommandText = "PRAGMA user_version;";
            if (Convert.ToInt32(
                    version.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture)
                != CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    "The database user-version does not match the supported schema.");
            }
        }

        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        using (var command = database.CreateCommand())
        {
            command.CommandText =
                """
                SELECT key, value
                FROM store_metadata
                WHERE key IN (
                    'schema_version',
                    'schema_fingerprint',
                    'storage_contract_version',
                    'sqlite_version',
                    'sqlite_source_id'
                );
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata.Add(reader.GetString(0), reader.GetString(1));
            }
        }

        if (metadata.Count != 5
            || metadata["schema_version"]
                != CurrentSchemaVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
            || metadata["storage_contract_version"] != CurrentStorageContractVersion
            || metadata["sqlite_version"] != binding.Version
            || metadata["sqlite_source_id"] != binding.SourceId
            || metadata["schema_fingerprint"] != ComputeSchemaFingerprint(database))
        {
            throw new InvalidOperationException(
                "The database storage contract or SQLite identity metadata is invalid.");
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                """
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id = 'M1-S2-0001'
                  AND from_version = 0
                  AND to_version = 1
                  AND sqlite_source_id = $source;
                """;
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(
                    migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The database migration identity is invalid.");
            }
        }

        HashSet<string> actualObjects = new(StringComparer.Ordinal);
        using (var schema = database.CreateCommand())
        {
            schema.CommandText =
                """
                SELECT type || ':' || name
                FROM sqlite_schema
                WHERE name NOT LIKE 'sqlite_%'
                ORDER BY type, name;
                """;
            using SqliteDataReader reader = schema.ExecuteReader();
            while (reader.Read())
            {
                actualObjects.Add(reader.GetString(0));
            }
        }

        if (!actualObjects.SetEquals(RequiredSchemaObjects))
        {
            throw new InvalidOperationException(
                "The database schema objects do not match the supported storage contract.");
        }
    }

    private static string ComputeSchemaFingerprint(
        SqliteConnection database,
        SqliteTransaction? transaction = null)
    {
        using SqliteCommand schema = database.CreateCommand();
        schema.Transaction = transaction;
        schema.CommandText =
            """
            SELECT type, name, tbl_name, COALESCE(sql, '')
            FROM sqlite_schema
            WHERE name NOT LIKE 'sqlite_%'
            ORDER BY type, name;
            """;
        var canonical = new StringBuilder();
        using SqliteDataReader reader = schema.ExecuteReader();
        while (reader.Read())
        {
            canonical
                .Append(reader.GetString(0)).Append('\u001f')
                .Append(reader.GetString(1)).Append('\u001f')
                .Append(reader.GetString(2)).Append('\u001f')
                .Append(reader.GetString(3)).Append('\n');
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
    }

    private static List<BackupPayloadManifest> ReadDatabasePayloads(
        SqliteConnection database)
    {
        List<BackupPayloadManifest> payloads = [];
        using var command = database.CreateCommand();
        command.CommandText =
            """
            SELECT content_sha256, byte_length, object_relative_path
            FROM payloads
            ORDER BY content_sha256;
            """;
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            payloads.Add(new BackupPayloadManifest(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetString(2)));
        }

        return payloads;
    }

    private static void ValidateManifestPayloadSet(
        IReadOnlyList<BackupPayloadManifest> manifestPayloads,
        IReadOnlyList<BackupPayloadManifest> databasePayloads)
    {
        Dictionary<string, BackupPayloadManifest> manifestBySha =
            new(StringComparer.Ordinal);
        foreach (BackupPayloadManifest payload in manifestPayloads)
        {
            if (!IsCanonicalSha256(payload.Sha256)
                || payload.ByteLength < 0
                || !string.Equals(
                    payload.RelativePath,
                    CanonicalPayloadRelativePath(payload.Sha256),
                    StringComparison.Ordinal)
                || !manifestBySha.TryAdd(payload.Sha256, payload))
            {
                throw new InvalidOperationException(
                    "The backup payload manifest contains an invalid or duplicate entry.");
            }
        }

        if (manifestBySha.Count != databasePayloads.Count)
        {
            throw new InvalidOperationException(
                "The backup payload manifest is incomplete or contains extra entries.");
        }

        foreach (BackupPayloadManifest databasePayload in databasePayloads)
        {
            if (!IsCanonicalSha256(databasePayload.Sha256)
                || databasePayload.ByteLength < 0
                || !string.Equals(
                    databasePayload.RelativePath,
                    CanonicalPayloadRelativePath(databasePayload.Sha256),
                    StringComparison.Ordinal)
                || !manifestBySha.TryGetValue(
                    databasePayload.Sha256,
                    out BackupPayloadManifest? manifestPayload)
                || manifestPayload.ByteLength != databasePayload.ByteLength
                || !string.Equals(
                    manifestPayload.RelativePath,
                    databasePayload.RelativePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The backup payload manifest does not match the database payload registry.");
            }
        }
    }

    private static void ValidatePayloadFiles(
        string payloadRoot,
        IReadOnlyList<BackupPayloadManifest> payloads)
    {
        foreach (BackupPayloadManifest payload in payloads)
        {
            string path = PayloadPath(payloadRoot, payload.Sha256);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("A referenced backup payload is missing.");
            }

            FileInfo file = new(path);
            if (file.Length != payload.ByteLength
                || !string.Equals(HashFile(path), payload.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A referenced backup payload has an invalid length or fingerprint.");
            }
        }
    }

    private static string PayloadPath(string payloadRoot, string sha256) =>
        Path.Combine(payloadRoot, sha256[..2], sha256[2..4], sha256);

    private static string CanonicalPayloadRelativePath(string sha256) =>
        $"payloads/{sha256[..2]}/{sha256[2..4]}/{sha256}";

    private static void ValidateManifestBinding(SqliteBindingIdentity binding)
    {
        SqliteBindingIdentity required = new(
            SqliteRuntimeIdentity.RequiredVersion,
            SqliteRuntimeIdentity.RequiredSourceId,
            SqliteRuntimeIdentity.RequiredWinX64NativeSha256,
            binding.CompileOptions);
        if (!BindingEquals(binding, required)
            || !binding.CompileOptions.Contains("THREADSAFE=1", StringComparer.Ordinal)
            || !binding.CompileOptions.Contains(
                "DEFAULT_WAL_SYNCHRONOUS=2",
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The backup manifest SQLite identity is incompatible.");
        }
    }

    private static bool BindingEquals(
        SqliteBindingIdentity first,
        SqliteBindingIdentity second) =>
        string.Equals(first.Version, second.Version, StringComparison.Ordinal)
        && string.Equals(first.SourceId, second.SourceId, StringComparison.Ordinal)
        && string.Equals(first.NativeSha256, second.NativeSha256, StringComparison.Ordinal)
        && first.CompileOptions.SequenceEqual(second.CompileOptions, StringComparer.Ordinal);

    private static bool IsCanonicalSha256(string value) =>
        value is not null
        && value.Length == 64
        && value.All(ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');

    private void ApplyMigrations()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            var current = Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            if (current > CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Database schema {current} is newer than supported schema {CurrentSchemaVersion}.");
            }

            if (current == 0)
            {
                using var transaction = BeginTransaction();
                Execute(SchemaV1, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    INSERT INTO store_metadata(key, value) VALUES ('schema_version', '1');
                    INSERT INTO store_metadata(key, value) VALUES ('schema_fingerprint', $schema_fingerprint);
                    INSERT INTO store_metadata(key, value) VALUES ('storage_contract_version', '1.0.0');
                    INSERT INTO store_metadata(key, value) VALUES ('sqlite_version', $sqlite_version);
                    INSERT INTO store_metadata(key, value) VALUES ('sqlite_source_id', $sqlite_source);
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S2-0001', 0, 1, $now, $sqlite_source);
                    PRAGMA user_version = 1;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_version", BindingIdentity.Version),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }
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

    private void EnsureCurrentCoordinatorEpoch(
        long coordinatorFencingEpoch,
        SqliteTransaction transaction)
    {
        long current = ScalarLong(
            """
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM coordinator_leases lease
                JOIN store_metadata metadata
                  ON metadata.key = 'active_coordinator_epoch'
                 AND CAST(metadata.value AS INTEGER) = lease.fencing_epoch
                WHERE lease.fencing_epoch = $epoch
                  AND lease.expires_at >= $now
            ) THEN 1 ELSE 0 END;
            """,
            transaction,
            ("$epoch", coordinatorFencingEpoch),
            ("$now", ToText(DateTimeOffset.UtcNow)));
        if (current != 1)
        {
            throw new InvalidOperationException(
                "The coordinator fencing epoch is stale or its lease has expired.");
        }
    }

    private static void ConfigureConnection(SqliteConnection target)
    {
        using var command = target.CreateCommand();
        command.CommandText =
            """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = FULL;
            PRAGMA temp_store = MEMORY;
            PRAGMA trusted_schema = OFF;
            PRAGMA busy_timeout = 5000;
            """;
        command.ExecuteNonQuery();
        using var verify = target.CreateCommand();
        verify.CommandText = "SELECT foreign_keys FROM pragma_foreign_keys;";
        if (Convert.ToInt32(verify.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException("SQLite foreign-key enforcement could not be enabled.");
        }
    }

    private int Execute(
        string sql,
        SqliteTransaction? transaction,
        params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        return command.ExecuteNonQuery();
    }

    private SqliteTransaction BeginTransaction()
    {
        sqliteVfs.VerifyAllGuards();
        return connection.BeginTransaction();
    }

    private long ScalarLong(
        string sql,
        SqliteTransaction? transaction,
        params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private string ScalarString(
        string sql,
        SqliteTransaction? transaction,
        params (string Name, object? Value)[] parameters) =>
        ScalarStringOrNull(sql, transaction, parameters)
        ?? throw new InvalidOperationException("A required database value was missing.");

    private string? ScalarStringOrNull(
        string sql,
        SqliteTransaction? transaction,
        params (string Name, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }

        var value = command.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private void RecordWriteClassAuthorityBindings(DateTimeOffset now)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            foreach (ProductWriteClass writeClass in Enum.GetValues<ProductWriteClass>())
            {
                InsertAuditEvent(
                    "write-class-authority-bound",
                    "write-class",
                    writeClass.ToString(),
                    now,
                    transaction);
            }

            transaction.Commit();
        }
    }

    private void InsertAuditEvent(
        string eventKind,
        string objectKind,
        string objectId,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        Execute(
            """
            INSERT INTO audit_events(
                audit_event_id, event_kind, object_kind, object_id,
                detail_payload_id, occurred_at)
            VALUES ($id, $event, $kind, $object, NULL, $now);
            """,
            transaction,
            ("$id", Guid.NewGuid().ToString("N")),
            ("$event", eventKind),
            ("$kind", objectKind),
            ("$object", objectId),
            ("$now", ToText(now)));
    }

    private static void AppendRestoreAudit(
        string databasePath,
        string authorityIdentity,
        DateTimeOffset now)
    {
        using SqliteConnection restored = new(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString());
        restored.Open();
        ConfigureConnection(restored);
        using SqliteTransaction transaction = restored.BeginTransaction();
        using SqliteCommand command = restored.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO audit_events(
                audit_event_id, event_kind, object_kind, object_id,
                detail_payload_id, occurred_at)
            VALUES ($id, 'restore-completed', 'product-root', $object, NULL, $now);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("$object", authorityIdentity);
        command.Parameters.AddWithValue("$now", ToText(now));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private static void ValidateAuditToken(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 64
            || value.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "Audit tokens must contain 1-64 ASCII letters, digits, hyphens, or underscores.",
                parameterName);
        }
    }

    private static void ValidateBinding(RunBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.InstallationSnapshotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.AnalysisContextId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.EffectiveScanConfigurationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.ResolvedInputManifestId);
    }

    private static void ValidateBoundedJson(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (Encoding.UTF8.GetByteCount(value) > MaximumCheckpointJsonBytes)
        {
            throw new ArgumentException(
                "Checkpoint JSON exceeds its finite byte bound.",
                parameterName);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                value,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32,
                });
            _ = document.RootElement.ValueKind;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "Checkpoint JSON must be a finite valid JSON value.",
                parameterName,
                exception);
        }
    }

    private static void ValidateSha256(string value)
    {
        if (value.Length != 64 || value.Any(ch => !Uri.IsHexDigit(ch)))
        {
            throw new ArgumentException("A lowercase 64-character SHA-256 value is required.", nameof(value));
        }

        if (!string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new ArgumentException("SHA-256 values must use lowercase canonical encoding.", nameof(value));
        }
    }

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }

    private static string ToText(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record BackupManifest(
        [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
        [property: JsonPropertyName("sqlite")] SqliteBindingIdentity Sqlite,
        [property: JsonPropertyName("databaseSha256")] string DatabaseSha256,
        [property: JsonPropertyName("payloads")] IReadOnlyList<BackupPayloadManifest> Payloads,
        [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt);

    private sealed record BackupPayloadManifest(
        [property: JsonPropertyName("sha256")] string Sha256,
        [property: JsonPropertyName("byteLength")] long ByteLength,
        [property: JsonPropertyName("relativePath")] string RelativePath);

    private sealed record ValidatedBackup(BackupManifest Manifest);

    private static readonly HashSet<string> RequiredSchemaObjects =
    [
        "index:idx_attempts_run",
        "index:idx_attempts_one_live_per_run",
        "index:idx_events_run_sequence",
        "index:idx_findings_signature",
        "index:idx_lineage_successor",
        "index:idx_reconciliation_successor",
        "index:idx_runs_created",
        "index:idx_runs_dispatch",
        "table:attempts",
        "table:audit_events",
        "table:case_occurrences",
        "table:checkpoints",
        "table:coordinator_leases",
        "table:durable_commands",
        "table:finding_occurrences",
        "table:job_nodes",
        "table:lifecycle_events",
        "table:lineage_events",
        "table:logical_cases",
        "table:logical_findings",
        "table:migration_history",
        "table:payload_owners",
        "table:payloads",
        "table:publication_receipt_payloads",
        "table:publication_receipts",
        "table:reconciliation_assessments",
        "table:run_projection",
        "table:runs",
        "table:store_metadata",
        "trigger:lifecycle_events_append_only_delete",
        "trigger:lifecycle_events_append_only_update",
        "trigger:audit_events_append_only_delete",
        "trigger:audit_events_append_only_update",
        "trigger:case_occurrences_append_only_delete",
        "trigger:case_occurrences_append_only_update",
        "trigger:checkpoints_append_only_delete",
        "trigger:checkpoints_append_only_update",
        "trigger:durable_commands_append_only_delete",
        "trigger:durable_commands_append_only_update",
        "trigger:finding_occurrences_append_only_delete",
        "trigger:finding_occurrences_append_only_update",
        "trigger:lineage_append_only_delete",
        "trigger:lineage_append_only_update",
        "trigger:publication_receipts_append_only_delete",
        "trigger:publication_receipts_append_only_update",
        "trigger:reconciliation_append_only_delete",
        "trigger:reconciliation_append_only_update",
        "trigger:runs_immutable_binding",
    ];

    private const string SchemaV1 =
        """
        CREATE TABLE store_metadata(
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        ) STRICT;
        CREATE TABLE migration_history(
            migration_id TEXT PRIMARY KEY,
            from_version INTEGER NOT NULL,
            to_version INTEGER NOT NULL UNIQUE,
            applied_at TEXT NOT NULL,
            sqlite_source_id TEXT NOT NULL
        ) STRICT;
        CREATE TABLE coordinator_leases(
            coordinator_instance_id TEXT NOT NULL,
            fencing_epoch INTEGER PRIMARY KEY CHECK(fencing_epoch > 0),
            acquired_at TEXT NOT NULL,
            expires_at TEXT NOT NULL,
            CHECK(expires_at > acquired_at)
        ) STRICT;
        CREATE TABLE runs(
            run_id TEXT PRIMARY KEY,
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            effective_scan_configuration_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            lifecycle_state TEXT NOT NULL,
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            durable_sequence INTEGER NOT NULL CHECK(durable_sequence > 0),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER runs_immutable_binding
        BEFORE UPDATE OF installation_snapshot_id, analysis_context_id,
                         effective_scan_configuration_id, resolved_input_manifest_id
        ON runs
        BEGIN SELECT RAISE(ABORT, 'run bindings are immutable'); END;
        CREATE TABLE job_nodes(
            job_node_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            parent_job_node_id TEXT REFERENCES job_nodes(job_node_id) ON DELETE RESTRICT,
            node_kind TEXT NOT NULL,
            lifecycle_state TEXT NOT NULL,
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE lifecycle_events(
            transition_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            job_node_id TEXT NOT NULL REFERENCES job_nodes(job_node_id) ON DELETE RESTRICT,
            record_kind TEXT NOT NULL CHECK(record_kind IN ('requested','observed')),
            policy_version TEXT NOT NULL,
            from_state TEXT NOT NULL,
            to_state TEXT NOT NULL,
            expected_generation INTEGER NOT NULL CHECK(expected_generation >= 0),
            new_generation INTEGER NOT NULL CHECK(new_generation = expected_generation + 1),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            reason TEXT NOT NULL,
            occurred_at TEXT NOT NULL,
            durable_sequence INTEGER NOT NULL CHECK(durable_sequence > 0),
            UNIQUE(run_id, durable_sequence)
        ) STRICT;
        CREATE TABLE durable_commands(
            command_id TEXT PRIMARY KEY,
            command_kind TEXT NOT NULL,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            expected_generation INTEGER NOT NULL CHECK(expected_generation >= 0),
            disposition TEXT NOT NULL,
            resulting_state TEXT NOT NULL,
            transition_id TEXT REFERENCES lifecycle_events(transition_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            start_initiation_kind TEXT,
            start_dispatch_deadline TEXT,
            CHECK (
                (command_kind = 'start'
                 AND ((start_initiation_kind IS NULL AND start_dispatch_deadline IS NULL)
                      OR (start_initiation_kind IS NOT NULL
                          AND start_dispatch_deadline IS NOT NULL)))
                OR
                (command_kind <> 'start'
                 AND start_initiation_kind IS NULL
                 AND start_dispatch_deadline IS NULL)
            )
        ) STRICT;
        CREATE TABLE attempts(
            attempt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            job_node_id TEXT NOT NULL REFERENCES job_nodes(job_node_id) ON DELETE RESTRICT,
            attempt_generation INTEGER NOT NULL CHECK(attempt_generation > 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            lease_acquired_at TEXT NOT NULL,
            lease_expires_at TEXT NOT NULL,
            dispatch_identity TEXT NOT NULL UNIQUE,
            idempotency_identity TEXT NOT NULL UNIQUE,
            retry_safety TEXT NOT NULL,
            outcome TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, attempt_generation),
            UNIQUE(run_id, attempt_fencing_token),
            CHECK(lease_expires_at > lease_acquired_at)
        ) STRICT;
        CREATE TABLE checkpoints(
            checkpoint_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL REFERENCES attempts(attempt_id) ON DELETE RESTRICT,
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            effective_scan_configuration_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            content_sha256 TEXT NOT NULL CHECK(length(content_sha256) = 64),
            completed_partitions_json TEXT NOT NULL,
            pending_and_gaps_json TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE payloads(
            payload_id TEXT PRIMARY KEY,
            content_sha256 TEXT NOT NULL UNIQUE CHECK(length(content_sha256) = 64),
            byte_length INTEGER NOT NULL CHECK(byte_length >= 0),
            codec TEXT NOT NULL,
            retention_state TEXT NOT NULL,
            object_relative_path TEXT NOT NULL UNIQUE,
            admitted_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE payload_owners(
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            owner_kind TEXT NOT NULL,
            owner_id TEXT NOT NULL,
            PRIMARY KEY(payload_id, owner_kind, owner_id)
        ) STRICT;
        CREATE TABLE publication_receipts(
            receipt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL UNIQUE REFERENCES attempts(attempt_id) ON DELETE RESTRICT,
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            staged_manifest_sha256 TEXT NOT NULL CHECK(length(staged_manifest_sha256) = 64),
            published_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE publication_receipt_payloads(
            receipt_id TEXT NOT NULL REFERENCES publication_receipts(receipt_id) ON DELETE RESTRICT,
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            PRIMARY KEY(receipt_id, payload_id)
        ) STRICT;
        CREATE TABLE logical_findings(
            logical_finding_id TEXT PRIMARY KEY,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_occurrences(
            finding_occurrence_id TEXT PRIMARY KEY,
            logical_finding_id TEXT NOT NULL REFERENCES logical_findings(logical_finding_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            analyzer_family TEXT NOT NULL,
            semantic_contract_version TEXT NOT NULL,
            identity_contract_version TEXT NOT NULL,
            identity_envelope_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            canonical_signature TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, finding_occurrence_id)
        ) STRICT;
        CREATE TABLE logical_cases(
            logical_case_id TEXT PRIMARY KEY,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE case_occurrences(
            case_occurrence_id TEXT PRIMARY KEY,
            logical_case_id TEXT NOT NULL REFERENCES logical_cases(logical_case_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            identity_envelope_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            shared_cause_signature TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE reconciliation_assessments(
            reconciliation_assessment_id TEXT PRIMARY KEY,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('finding','case')),
            predecessor_occurrence_id TEXT,
            successor_occurrence_id TEXT,
            causal_gate TEXT NOT NULL,
            applicability_gate TEXT NOT NULL,
            dependency_gate TEXT NOT NULL,
            producer_compatibility_gate TEXT NOT NULL,
            outcome TEXT NOT NULL CHECK(outcome IN (
                'exact-continuation','analytical-revision','related-follow-up','new-distinct',
                'ambiguous','unknown','not-observed','not-evaluated')),
            proof_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            policy_version TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE lineage_events(
            lineage_event_id TEXT PRIMARY KEY,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('finding','case')),
            event_kind TEXT NOT NULL CHECK(event_kind IN (
                'continuation','revision','follow-up','merge','split','supersession',
                'promotion','correction')),
            predecessor_logical_id TEXT,
            successor_logical_id TEXT NOT NULL,
            reconciliation_assessment_id TEXT REFERENCES reconciliation_assessments(reconciliation_assessment_id)
                ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE audit_events(
            audit_event_id TEXT PRIMARY KEY,
            event_kind TEXT NOT NULL,
            object_kind TEXT NOT NULL,
            object_id TEXT NOT NULL,
            detail_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            occurred_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE run_projection(
            run_id TEXT PRIMARY KEY REFERENCES runs(run_id) ON DELETE CASCADE,
            lifecycle_state TEXT NOT NULL,
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            durable_sequence INTEGER NOT NULL CHECK(durable_sequence > 0),
            projection_version INTEGER NOT NULL CHECK(projection_version > 0),
            updated_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER lifecycle_events_append_only_update
        BEFORE UPDATE ON lifecycle_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER lifecycle_events_append_only_delete
        BEFORE DELETE ON lifecycle_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER reconciliation_append_only_update
        BEFORE UPDATE ON reconciliation_assessments BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER reconciliation_append_only_delete
        BEFORE DELETE ON reconciliation_assessments BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER lineage_append_only_update
        BEFORE UPDATE ON lineage_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER lineage_append_only_delete
        BEFORE DELETE ON lineage_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER audit_events_append_only_update
        BEFORE UPDATE ON audit_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER audit_events_append_only_delete
        BEFORE DELETE ON audit_events BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER durable_commands_append_only_update
        BEFORE UPDATE ON durable_commands BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER durable_commands_append_only_delete
        BEFORE DELETE ON durable_commands BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER checkpoints_append_only_update
        BEFORE UPDATE ON checkpoints BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER checkpoints_append_only_delete
        BEFORE DELETE ON checkpoints BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER publication_receipts_append_only_update
        BEFORE UPDATE ON publication_receipts BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER publication_receipts_append_only_delete
        BEFORE DELETE ON publication_receipts BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER finding_occurrences_append_only_update
        BEFORE UPDATE ON finding_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER finding_occurrences_append_only_delete
        BEFORE DELETE ON finding_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER case_occurrences_append_only_update
        BEFORE UPDATE ON case_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER case_occurrences_append_only_delete
        BEFORE DELETE ON case_occurrences BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE INDEX idx_runs_created ON runs(created_at, run_id);
        CREATE INDEX idx_runs_dispatch ON runs(lifecycle_state, created_at, run_id);
        CREATE INDEX idx_events_run_sequence ON lifecycle_events(run_id, durable_sequence);
        CREATE INDEX idx_attempts_run ON attempts(run_id, attempt_generation);
        CREATE UNIQUE INDEX idx_attempts_one_live_per_run
        ON attempts(run_id) WHERE outcome = 'running';
        CREATE INDEX idx_findings_signature ON finding_occurrences(
            analyzer_family, identity_contract_version, canonical_signature);
        CREATE INDEX idx_reconciliation_successor ON reconciliation_assessments(
            subject_kind, successor_occurrence_id);
        CREATE INDEX idx_lineage_successor ON lineage_events(subject_kind, successor_logical_id);
        """;
}

#pragma warning restore CA1869
#pragma warning restore CA1512
#pragma warning restore IDE0008
