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

public sealed partial class AuthoritativeStore : IDisposable
{
    public const int CurrentSchemaVersion = 5;
    public const string CurrentStorageContractVersion = "1.4.0";
    private const string SchemaV3Fingerprint =
        "02fed67fa5dac6c28ec2a9f477733edc9f12eaa03a08f9d7dec05b502e45d6cf";
    private const int MaximumBackupManifestBytes = 16 * 1024 * 1024;
    private const int MaximumCheckpointJsonBytes = 64 * 1024;
    private static readonly JsonSerializerOptions DocumentationPayloadJsonOptions =
        CreateDocumentationPayloadJsonOptions();

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

    public RunOperationRecord RegisterRunOperation(
        string runId,
        string operationKind,
        string requestJson,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ValidateAuditToken(operationKind, nameof(operationKind));
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        if (Encoding.UTF8.GetByteCount(requestJson) > MaximumCheckpointJsonBytes)
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

    public SnapshotCaptureOperationRecord CreateSnapshotCaptureOperation(
        string durableCommandId,
        string operationId,
        string requestJson,
        string requestSha256,
        string initiationKind,
        DateTimeOffset dispatchDeadline,
        long coordinatorFencingEpoch,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableCommandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        ValidateSha256(requestSha256);
        ValidateAuditToken(initiationKind, nameof(initiationKind));
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        if (Encoding.UTF8.GetByteCount(requestJson) > 64 * 1024)
        {
            throw new ArgumentException("The snapshot capture request exceeds its bound.");
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            string? existingOperationId = ScalarStringOrNull(
                """
                SELECT operation_id FROM snapshot_capture_operations
                WHERE durable_command_id = $command;
                """,
                transaction,
                ("$command", durableCommandId));
            if (existingOperationId is not null)
            {
                SnapshotCaptureOperationRecord existing =
                    GetSnapshotCaptureOperationCore(existingOperationId);
                if (!string.Equals(existing.RequestSha256, requestSha256, StringComparison.Ordinal)
                    || !string.Equals(existing.RequestJson, requestJson, StringComparison.Ordinal)
                    || !string.Equals(existing.InitiationKind, initiationKind, StringComparison.Ordinal)
                    || existing.DispatchDeadline != dispatchDeadline)
                {
                    throw new InvalidOperationException(
                        "A durable snapshot-capture key cannot be rebound.");
                }

                transaction.Commit();
                return existing;
            }

            if (dispatchDeadline <= now)
            {
                throw new InvalidOperationException(
                    "A new snapshot capture requires a future dispatch deadline.");
            }

            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            Execute(
                """
                INSERT INTO snapshot_capture_operations(
                    operation_id, durable_command_id, request_json, request_sha256,
                    initiation_kind, dispatch_deadline, lifecycle_state,
                    lifecycle_generation, coordinator_fencing_epoch,
                    installation_snapshot_id, payload_id, created_at, updated_at)
                VALUES (
                    $operation, $command, $json, $sha, $initiation, $deadline,
                    'Queued', 0, $epoch, NULL, NULL, $now, $now);
                """,
                transaction,
                ("$operation", operationId),
                ("$command", durableCommandId),
                ("$json", requestJson),
                ("$sha", requestSha256),
                ("$initiation", initiationKind),
                ("$deadline", ToText(dispatchDeadline)),
                ("$epoch", coordinatorFencingEpoch),
                ("$now", ToText(now)));
            InsertAuditEvent(
                "snapshot-capture-requested",
                "snapshot-capture-operation",
                operationId,
                now,
                transaction);
            transaction.Commit();
            return GetSnapshotCaptureOperationCore(operationId);
        }
    }

    public SnapshotCaptureOperationRecord GetSnapshotCaptureOperation(string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        lock (gate)
        {
            return GetSnapshotCaptureOperationCore(operationId);
        }
    }

    public SnapshotCaptureOperationRecord? FindSnapshotCaptureByCommand(
        string durableCommandId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableCommandId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT operation_id FROM snapshot_capture_operations
                WHERE durable_command_id = $command;
                """;
            command.Parameters.AddWithValue("$command", durableCommandId);
            string? operationId = command.ExecuteScalar() as string;
            return operationId is null
                ? null
                : GetSnapshotCaptureOperationCore(operationId);
        }
    }

    public SnapshotCaptureOperationRecord? GetNextDispatchableSnapshotCapture()
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT operation_id
                FROM snapshot_capture_operations
                WHERE lifecycle_state = 'Queued'
                ORDER BY created_at, operation_id
                LIMIT 1;
                """;
            string? operationId = command.ExecuteScalar() as string;
            return operationId is null ? null : GetSnapshotCaptureOperationCore(operationId);
        }
    }

    public int FenceInterruptedSnapshotCaptures(
        long coordinatorFencingEpoch,
        DateTimeOffset now)
    {
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            Execute(
                """
                UPDATE snapshot_capture_attempts
                SET outcome = 'interrupted-by-coordinator-recovery'
                WHERE outcome = 'running';
                """,
                transaction);
            int changed = Execute(
                """
                UPDATE snapshot_capture_operations
                SET lifecycle_state = 'Failed',
                    lifecycle_generation = lifecycle_generation + 1,
                    coordinator_fencing_epoch = $epoch,
                    updated_at = $now
                WHERE lifecycle_state = 'Running';
                """,
                transaction,
                ("$epoch", coordinatorFencingEpoch),
                ("$now", ToText(now)));
            transaction.Commit();
            return changed;
        }
    }

    public SnapshotCaptureAttemptRecord DispatchSnapshotCaptureAttempt(
        string operationId,
        long expectedGeneration,
        long coordinatorFencingEpoch,
        TimeSpan leaseDuration,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            SnapshotCaptureOperationRecord current =
                GetSnapshotCaptureOperationCore(operationId);
            if (current.State != "Queued" || current.Generation != expectedGeneration)
            {
                throw new InvalidOperationException(
                    "The snapshot capture is not dispatchable at the expected generation.");
            }

            if (current.DispatchDeadline <= now)
            {
                int expired = Execute(
                    """
                    UPDATE snapshot_capture_operations
                    SET lifecycle_state = 'Failed',
                        lifecycle_generation = lifecycle_generation + 1,
                        coordinator_fencing_epoch = $epoch,
                        updated_at = $now
                    WHERE operation_id = $operation
                      AND lifecycle_state = 'Queued'
                      AND lifecycle_generation = $generation;
                    """,
                    transaction,
                    ("$epoch", coordinatorFencingEpoch),
                    ("$now", ToText(now)),
                    ("$operation", operationId),
                    ("$generation", expectedGeneration));
                if (expired != 1)
                {
                    throw new InvalidOperationException(
                        "The expired snapshot capture compare-and-swap lost its race.");
                }

                InsertAuditEvent(
                    "snapshot-capture-dispatch-expired",
                    "snapshot-capture-operation",
                    operationId,
                    now,
                    transaction);
                transaction.Commit();
                throw new InvalidOperationException("The snapshot capture dispatch deadline expired.");
            }

            long attemptGeneration = ScalarLong(
                """
                SELECT COALESCE(MAX(attempt_generation), 0) + 1
                FROM snapshot_capture_attempts WHERE operation_id = $operation;
                """,
                transaction,
                ("$operation", operationId));
            long fencingToken = ScalarLong(
                """
                SELECT COALESCE(MAX(attempt_fencing_token), 0) + 1
                FROM snapshot_capture_attempts WHERE operation_id = $operation;
                """,
                transaction,
                ("$operation", operationId));
            string attemptId = Guid.NewGuid().ToString("N");
            DateTimeOffset expires = now.Add(leaseDuration);
            int changed = Execute(
                """
                UPDATE snapshot_capture_operations
                SET lifecycle_state = 'Running',
                    lifecycle_generation = lifecycle_generation + 1,
                    coordinator_fencing_epoch = $epoch,
                    updated_at = $now
                WHERE operation_id = $operation
                  AND lifecycle_state = 'Queued'
                  AND lifecycle_generation = $generation;
                """,
                transaction,
                ("$epoch", coordinatorFencingEpoch),
                ("$now", ToText(now)),
                ("$operation", operationId),
                ("$generation", expectedGeneration));
            if (changed != 1)
            {
                throw new InvalidOperationException(
                    "The snapshot capture dispatch compare-and-swap lost its race.");
            }

            Execute(
                """
                INSERT INTO snapshot_capture_attempts(
                    attempt_id, operation_id, attempt_generation,
                    coordinator_fencing_epoch, attempt_fencing_token,
                    lease_acquired_at, lease_expires_at, outcome, created_at)
                VALUES (
                    $attempt, $operation, $generation, $epoch, $token,
                    $now, $expires, 'running', $now);
                """,
                transaction,
                ("$attempt", attemptId),
                ("$operation", operationId),
                ("$generation", attemptGeneration),
                ("$epoch", coordinatorFencingEpoch),
                ("$token", fencingToken),
                ("$now", ToText(now)),
                ("$expires", ToText(expires)));
            InsertAuditEvent(
                "snapshot-capture-dispatched",
                "snapshot-capture-attempt",
                attemptId,
                now,
                transaction);
            transaction.Commit();
            return new SnapshotCaptureAttemptRecord(
                attemptId,
                operationId,
                attemptGeneration,
                coordinatorFencingEpoch,
                fencingToken,
                expires,
                "running");
        }
    }

    public void FailSnapshotCapture(
        SnapshotCaptureAttemptRecord attempt,
        long coordinatorFencingEpoch,
        DateTimeOffset now)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            EnsureCurrentSnapshotCaptureAttempt(
                attempt,
                transaction,
                requireUnexpiredLease: false);
            Execute(
                """
                UPDATE snapshot_capture_attempts SET outcome = 'failed'
                WHERE attempt_id = $attempt;
                """,
                transaction,
                ("$attempt", attempt.AttemptId));
            Execute(
                """
                UPDATE snapshot_capture_operations
                SET lifecycle_state = 'Failed',
                    lifecycle_generation = lifecycle_generation + 1,
                    updated_at = $now
                WHERE operation_id = $operation AND lifecycle_state = 'Running';
                """,
                transaction,
                ("$now", ToText(now)),
                ("$operation", attempt.OperationId));
            transaction.Commit();
        }
    }

    public PayloadAdmission AdmitSnapshotCapturePayload(
        SnapshotCaptureAttemptRecord attempt,
        string stagedRelativePath,
        string expectedSha256,
        long expectedByteLength,
        string expectedManifestSha256,
        long maximumBytes,
        string installationSnapshotId,
        string stagedArtifactId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationSnapshotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedArtifactId);
        ValidateSha256(expectedSha256);
        ValidateSha256(expectedManifestSha256);
        if (expectedByteLength < 0 || maximumBytes <= 0 || expectedByteLength > maximumBytes)
        {
            throw new InvalidOperationException("The staged snapshot exceeds its bound.");
        }

        string stagingRelativePath = Path.Combine(attempt.AttemptId, stagedRelativePath);
        using WindowsHandleRelativeStorage.AdmissionSource staged =
            Paths.OpenAdmissionSource(ProductWriteClass.AttemptStaging, stagingRelativePath);
        string payloadClassRelativePath = Path.Combine(
            expectedSha256[..2],
            expectedSha256[2..4],
            expectedSha256);
        string relativeObjectPath = Path.Combine("payloads", payloadClassRelativePath);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(
                attempt.CoordinatorFencingEpoch,
                transaction);
            EnsureCurrentSnapshotCaptureAttempt(
                attempt,
                transaction,
                requireUnexpiredLease: true);
            WindowsHandleRelativeStorage.AdmissionCopyResult copied =
                Paths.PublishAdmissionSource(
                    staged,
                    payloadClassRelativePath,
                    expectedSha256,
                    expectedByteLength,
                    maximumBytes);
            string payloadId = Guid.NewGuid().ToString("N");
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
                ("$sha", copied.Sha256),
                ("$length", copied.ByteLength),
                ("$path", relativeObjectPath.Replace('\\', '/')),
                ("$now", ToText(now)));
            string admittedPayloadId = ScalarString(
                "SELECT payload_id FROM payloads WHERE content_sha256 = $sha;",
                transaction,
                ("$sha", copied.Sha256));
            Execute(
                """
                INSERT OR IGNORE INTO payload_owners(payload_id, owner_kind, owner_id)
                VALUES ($payload, 'snapshot-capture-operation', $operation);
                """,
                transaction,
                ("$payload", admittedPayloadId),
                ("$operation", attempt.OperationId));
            Execute(
                """
                UPDATE snapshot_capture_attempts SET outcome = 'completed-staged'
                WHERE attempt_id = $attempt;
                """,
                transaction,
                ("$attempt", attempt.AttemptId));
            int changed = Execute(
                """
                UPDATE snapshot_capture_operations
                SET lifecycle_state = 'Completed',
                    lifecycle_generation = lifecycle_generation + 1,
                    installation_snapshot_id = $snapshot,
                    payload_id = $payload,
                    updated_at = $now
                WHERE operation_id = $operation
                  AND lifecycle_state = 'Running'
                  AND coordinator_fencing_epoch = $epoch;
                """,
                transaction,
                ("$snapshot", installationSnapshotId),
                ("$payload", admittedPayloadId),
                ("$now", ToText(now)),
                ("$operation", attempt.OperationId),
                ("$epoch", attempt.CoordinatorFencingEpoch));
            if (changed != 1)
            {
                throw new InvalidOperationException(
                    "The snapshot publication fence is stale.");
            }

            string receiptId = Guid.NewGuid().ToString("N");
            Execute(
                """
                INSERT INTO snapshot_capture_publications(
                    receipt_id, operation_id, attempt_id,
                    coordinator_fencing_epoch, attempt_fencing_token,
                    staged_manifest_sha256, payload_id,
                    installation_snapshot_id, published_at)
                VALUES (
                    $receipt, $operation, $attempt, $epoch, $token,
                    $manifest, $payload, $snapshot, $now);
                """,
                transaction,
                ("$receipt", receiptId),
                ("$operation", attempt.OperationId),
                ("$attempt", attempt.AttemptId),
                ("$epoch", attempt.CoordinatorFencingEpoch),
                ("$token", attempt.AttemptFencingToken),
                ("$manifest", expectedManifestSha256),
                ("$payload", admittedPayloadId),
                ("$snapshot", installationSnapshotId),
                ("$now", ToText(now)));
            InsertAuditEvent(
                "snapshot-capture-published",
                "installation-snapshot",
                installationSnapshotId,
                now,
                transaction,
                admittedPayloadId);
            transaction.Commit();
            staged.Delete();
            return new PayloadAdmission(
                admittedPayloadId,
                copied.Sha256,
                copied.ByteLength,
                relativeObjectPath.Replace('\\', '/'),
                receiptId,
                expectedManifestSha256);
        }
    }

    public byte[] ReadSnapshotCaptureStagedPayload(
        SnapshotCaptureAttemptRecord attempt,
        string stagedRelativePath,
        string expectedSha256,
        long expectedByteLength,
        long maximumBytes)
    {
        ValidateSha256(expectedSha256);
        if (expectedByteLength < 0
            || maximumBytes <= 0
            || expectedByteLength > maximumBytes
            || maximumBytes > 64L * 1024 * 1024)
        {
            throw new InvalidOperationException("The staged snapshot read exceeds its bound.");
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(
                attempt.CoordinatorFencingEpoch,
                transaction);
            EnsureCurrentSnapshotCaptureAttempt(
                attempt,
                transaction,
                requireUnexpiredLease: true);
            transaction.Commit();
        }

        using WindowsHandleRelativeStorage.AdmissionSource source =
            Paths.OpenAdmissionSource(
                ProductWriteClass.AttemptStaging,
                Path.Combine(attempt.AttemptId, stagedRelativePath));
        using MemoryStream buffer = new(checked((int)expectedByteLength));
        WindowsHandleRelativeStorage.AdmissionCopyResult observed =
            source.CopyToAndHash(buffer, maximumBytes);
        if (observed.ByteLength != expectedByteLength
            || !string.Equals(observed.Sha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The staged snapshot bytes do not match the worker manifest.");
        }

        return buffer.ToArray();
    }

    public byte[] ReadPublishedSnapshotPayload(
        string installationSnapshotId,
        int maximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationSnapshotId);
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT payload.content_sha256, payload.byte_length, payload.object_relative_path
                FROM snapshot_capture_publications publication
                JOIN payloads payload ON payload.payload_id = publication.payload_id
                WHERE publication.installation_snapshot_id = $snapshot;
                """;
            command.Parameters.AddWithValue("$snapshot", installationSnapshotId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new InvalidOperationException(
                    "The run-bound installation snapshot has no authoritative publication.");
            }

            string expectedSha256 = reader.GetString(0);
            long expectedLength = reader.GetInt64(1);
            string relativePath = reader.GetString(2);
            if (reader.Read() || expectedLength > maximumBytes)
            {
                throw new InvalidOperationException(
                    "The run-bound installation snapshot publication is outside its authority.");
            }

            string objectPath = Paths.ResolveProductPath(
                ProductWriteClass.Payload,
                relativePath["payloads/".Length..].Replace('/', Path.DirectorySeparatorChar));
            byte[] bytes = ReadBoundedFile(objectPath, maximumBytes, "published installation snapshot");
            if (bytes.LongLength != expectedLength
                || !string.Equals(
                    Convert.ToHexStringLower(SHA256.HashData(bytes)),
                    expectedSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The run-bound installation snapshot payload failed identity validation.");
            }

            return bytes;
        }
    }

    public byte[] ReadRunStagedPayload(
        AttemptRecord attempt,
        string stagedRelativePath,
        string expectedSha256,
        long expectedByteLength,
        long maximumBytes)
    {
        ValidateSha256(expectedSha256);
        if (expectedByteLength < 0
            || maximumBytes <= 0
            || expectedByteLength > maximumBytes
            || maximumBytes > 64L * 1024 * 1024)
        {
            throw new InvalidOperationException("The staged run payload read exceeds its bound.");
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(
                attempt.CoordinatorFencingEpoch,
                transaction);
            EnsureCurrentAttempt(attempt, transaction);
            transaction.Commit();
        }

        using WindowsHandleRelativeStorage.AdmissionSource source =
            Paths.OpenAdmissionSource(
                ProductWriteClass.AttemptStaging,
                Path.Combine(attempt.AttemptId, stagedRelativePath));
        using MemoryStream buffer = new(checked((int)expectedByteLength));
        WindowsHandleRelativeStorage.AdmissionCopyResult observed =
            source.CopyToAndHash(buffer, maximumBytes);
        if (observed.ByteLength != expectedByteLength
            || !string.Equals(observed.Sha256, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The staged run bytes do not match the worker manifest.");
        }

        return buffer.ToArray();
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

    internal void AdmitDocumentationApplicationTargets(
        IReadOnlyList<DocumentationApplicationTargetContract> targets,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(targets);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            foreach (DocumentationApplicationTargetContract target in targets)
            {
                EnsureRunExists(target.ConsumingRunId.Value, transaction);
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM runs
                    WHERE run_id = $run
                      AND installation_snapshot_id = $snapshot
                      AND analysis_context_id = $context
                      AND resolved_input_manifest_id = $manifest;
                    """,
                    "A documentation application target does not belong to the consuming run's immutable snapshot mapping.",
                    transaction,
                    ("$run", target.ConsumingRunId.Value),
                    ("$snapshot", target.InstallationSnapshotId.Value),
                    ("$context", target.AnalysisContextId.Value),
                    ("$manifest", target.ResolvedInputManifestId.Value));
                string bindingId = StableDocumentationId(
                    "docbinding",
                    target.ConsumingRunId.Value,
                    target.InstallationSnapshotId.Value,
                    target.AnalysisContextId.Value,
                    target.ResolvedInputManifestId.Value,
                    target.SubjectType,
                    target.SubjectId.Value,
                    target.DependencyClosureId.Value).Value;
                Execute(
                    """
                    INSERT OR IGNORE INTO documentation_application_bindings(
                        documentation_application_binding_id, run_id, installation_snapshot_id,
                        analysis_context_id, resolved_input_manifest_id, subject_id, subject_type,
                        dependency_closure_id, created_at)
                    VALUES ($binding, $run, $snapshot, $context, $manifest, $subject, $subject_type, $closure, $now);
                    """,
                    transaction,
                    ("$binding", bindingId),
                    ("$run", target.ConsumingRunId.Value),
                    ("$snapshot", target.InstallationSnapshotId.Value),
                    ("$context", target.AnalysisContextId.Value),
                    ("$manifest", target.ResolvedInputManifestId.Value),
                    ("$subject", target.SubjectId.Value),
                    ("$subject_type", target.SubjectType),
                    ("$closure", target.DependencyClosureId.Value),
                    ("$now", ToText(now)));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM documentation_application_bindings
                    WHERE documentation_application_binding_id = $binding
                      AND run_id = $run
                      AND installation_snapshot_id = $snapshot
                      AND analysis_context_id = $context
                      AND resolved_input_manifest_id = $manifest
                      AND subject_id = $subject
                      AND subject_type = $subject_type
                      AND dependency_closure_id = $closure;
                    """,
                    "A documentation application target identity resolves to different admitted mapping semantics.",
                    transaction,
                    ("$binding", bindingId),
                    ("$run", target.ConsumingRunId.Value),
                    ("$snapshot", target.InstallationSnapshotId.Value),
                    ("$context", target.AnalysisContextId.Value),
                    ("$manifest", target.ResolvedInputManifestId.Value),
                    ("$subject", target.SubjectId.Value),
                    ("$subject_type", target.SubjectType),
                    ("$closure", target.DependencyClosureId.Value));
            }
            transaction.Commit();
        }
    }

    internal DocumentationEvidenceContract PrepareDocumentationDeletionEvidence(
        DocumentationEvidenceContract evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Slice5ContractInvariants.Validate(evidence);
        if (evidence.DeletionReceipts.Count == 0)
        {
            return evidence;
        }

        lock (gate)
        {
            List<DocumentationDeletionReceiptContract> receipts = [];
            foreach (DocumentationDeletionReceiptContract receipt in evidence.DeletionReceipts)
            {
                Dictionary<string, HashSet<(string Kind, string Id)>> payloadOwners =
                    ReadDocumentationDeletionPayloadOwners(receipt);
                List<OpaqueId> independentlyRetained = [];
                foreach ((string targetPayloadId, HashSet<(string Kind, string Id)> owners) in payloadOwners)
                {
                    HashSet<(string Kind, string Id)> permitted = new()
                    {
                        ("documentation-revision", receipt.RevisionId.Value),
                    };
                    permitted.UnionWith(receipt.DeletedPassageIds.Select(item =>
                        ("documentation-passage", item.Value)));
                    if (owners.Any(owner => !permitted.Contains(owner)))
                    {
                        independentlyRetained.Add(DocumentationPayloadSemanticId(targetPayloadId, null));
                    }
                    else if (HasDocumentationBackupPin(targetPayloadId, null))
                    {
                        independentlyRetained.Add(DocumentationPayloadSemanticId(targetPayloadId, null));
                    }
                }
                OpaqueId[] retainedIds = independentlyRetained
                    .Distinct()
                    .OrderBy(item => item.Value, StringComparer.Ordinal)
                    .ToArray();
                OpaqueId receiptId = StableDocumentationId(
                    "docdelete",
                    receipt.OriginatingRunId.Value,
                    receipt.RevisionId.Value,
                    receipt.DeletedBodyFingerprint.Value,
                    CanonicalDocumentation(receipt.DeletedPassageIds.Select(item => item.Value)),
                    CanonicalDocumentation(retainedIds.Select(item => item.Value)),
                    receipt.DeletedAt.ToString(),
                    receipt.Reason);
                receipts.Add(receipt with
                {
                    ReceiptId = receiptId,
                    IndependentlyRetainedPayloadIds = retainedIds,
                });
            }
            receipts = receipts.OrderBy(item => item.ReceiptId.Value, StringComparer.Ordinal).ToList();
            DocumentationEvidenceContract prepared = evidence with
            {
                PayloadId = new OpaqueId("docevidence-pending"),
                DeletionReceipts = receipts,
            };
            prepared = prepared with
            {
                PayloadId = DocumentationEvidenceIdentity.ComputePayloadId(prepared),
            };
            Slice5ContractInvariants.Validate(prepared);
            return prepared;
        }
    }

    internal DocumentationEvidencePersistenceReceipt PublishDocumentationEvidence(
        DocumentationEvidenceContract evidence,
        ReadOnlyMemory<byte>? sourceBytes,
        ReadOnlyMemory<byte> serializedEvidence,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Slice5ContractInvariants.Validate(evidence);
        byte[] canonicalEvidence = JsonSerializer.SerializeToUtf8Bytes(evidence, DocumentationPayloadJsonOptions);
        if (!serializedEvidence.Span.SequenceEqual(canonicalEvidence))
        {
            throw new InvalidDataException(
                "Serialized documentation evidence must be the canonical encoding of the published contract object.");
        }
        if (evidence.Revisions.Count != 1 || evidence.Imports.Count != 1)
        {
            throw new InvalidOperationException("WP2 publication accepts one revision/import transaction at a time.");
        }

        DocumentationRevisionContract revision = evidence.Revisions[0];
        DocumentationImportContract import = evidence.Imports[0];
        bool cleanImport = import.Mode == DocumentationImportMode.CleanImport;
        if ((cleanImport && revision.RetentionState == Slice5ResultState.Present) != sourceBytes.HasValue)
        {
            throw new InvalidOperationException("Retained source bytes must match the revision retention state.");
        }
        if (sourceBytes is { } bytes
            && (bytes.Length != revision.ByteLength
                || !StringComparer.Ordinal.Equals(Hash(bytes.Span), revision.ByteFingerprint.Value)))
        {
            throw new InvalidDataException("Published source bytes do not match the admitted revision identity.");
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureRunExists(evidence.OriginatingRunId.Value, transaction);
            foreach (ClaimApplicationContract application in evidence.Applications)
            {
                EnsureRunExists(application.ConsumingRunId.Value, transaction);
                string admittedContext = ScalarString(
                    "SELECT analysis_context_id FROM runs WHERE run_id = $run;",
                    transaction,
                    ("$run", application.ConsumingRunId.Value));
                if (!StringComparer.Ordinal.Equals(admittedContext, application.AnalysisContextId.Value))
                {
                    throw new InvalidDataException(
                        "A documentation application context does not match its consuming run binding.");
                }
            }

            string evidencePayloadId = AdmitCoordinatorPayload(
                serializedEvidence.Span,
                "documentation-evidence",
                evidence.PayloadId.Value,
                now,
                transaction);
            string? bodyPayloadId = sourceBytes is null
                ? ScalarStringOrNull(
                    "SELECT body_payload_id FROM documentation_revisions WHERE documentation_revision_id = $revision;",
                    transaction,
                    ("$revision", revision.RevisionId.Value))
                : AdmitCoordinatorPayload(
                    sourceBytes.Value.Span,
                    "documentation-revision",
                    revision.RevisionId.Value,
                    now,
                    transaction);
            if (!cleanImport && bodyPayloadId is null)
            {
                throw new InvalidOperationException("Retained reuse requires the original retained source payload.");
            }

            Execute(
                """
                INSERT OR IGNORE INTO documentation_revisions(
                    documentation_revision_id, source_id, source_kind, source_revision,
                    supplying_snapshot_id, body_payload_id, content_sha256, byte_length,
                    availability_state, retention_state, replay_state, created_at)
                VALUES (
                    $revision, $source, $source_kind, $source_revision,
                    $snapshot, $body, $sha, $length, $availability, $retention, $replay, $now);
                """,
                transaction,
                ("$revision", revision.RevisionId.Value),
                ("$source", revision.SourceId.Value),
                ("$source_kind", SourceKindToken(revision.SourceKind)),
                ("$source_revision", revision.SourceRevision),
                ("$snapshot", revision.SupplyingSnapshotId?.Value),
                ("$body", bodyPayloadId),
                ("$sha", revision.ByteFingerprint.Value),
                ("$length", revision.ByteLength),
                ("$availability", ResultStateToken(revision.RetentionState)),
                ("$retention", ResultStateToken(revision.RetentionState)),
                ("$replay", ReplayStateToken(revision.ReplayState)),
                ("$now", ToText(now)));
            RequireSingleDocumentationRow(
                """
                SELECT COUNT(*) FROM documentation_revisions
                WHERE documentation_revision_id = $revision
                  AND source_id = $source
                  AND source_kind = $source_kind
                  AND source_revision = $source_revision
                  AND supplying_snapshot_id IS $snapshot
                  AND body_payload_id IS $body
                  AND content_sha256 = $sha
                  AND byte_length = $length
                  AND availability_state = $availability
                  AND retention_state = $retention
                  AND replay_state = $replay;
                """,
                "A retained documentation revision ID resolves to different source identity or state.",
                transaction,
                ("$revision", revision.RevisionId.Value),
                ("$source", revision.SourceId.Value),
                ("$source_kind", SourceKindToken(revision.SourceKind)),
                ("$source_revision", revision.SourceRevision),
                ("$snapshot", revision.SupplyingSnapshotId?.Value),
                ("$body", bodyPayloadId),
                ("$sha", revision.ByteFingerprint.Value),
                ("$length", revision.ByteLength),
                ("$availability", ResultStateToken(revision.RetentionState)),
                ("$retention", ResultStateToken(revision.RetentionState)),
                ("$replay", ReplayStateToken(revision.ReplayState)));
            Execute(
                """
                INSERT INTO documentation_imports(
                    documentation_import_id, import_run_id, documentation_revision_id,
                    import_mode, reused_import_id, dependency_closure_id, extractor_id,
                    llm_involvement, llm_operation, boundaries_payload_id,
                    import_payload_id, created_at)
                VALUES (
                    $import, $import_run, $revision, $mode, $reused_import, $closure, $extractor,
                    'none', 'none', $payload, $payload, $now);
                """,
                transaction,
                ("$import", import.ImportId.Value),
                ("$import_run", import.ImportRunId.Value),
                ("$revision", revision.RevisionId.Value),
                ("$mode", ImportModeToken(import.Mode)),
                ("$reused_import", import.ReusedImportId?.Value),
                ("$closure", import.DependencyClosureId.Value),
                ("$extractor", import.ExtractorId.Value),
                ("$payload", evidencePayloadId),
                ("$now", import.CreatedAt.ToString()));

            Dictionary<OpaqueId, string> passagePayloadIds = [];
            foreach (DocumentationPassageContract passage in evidence.Passages)
            {
                string passagePayloadId;
                if (sourceBytes is { } retainedSource)
                {
                    if (passage.Utf8EndOffset > retainedSource.Length)
                    {
                        throw new InvalidDataException("A documentation passage lies outside the retained source bytes.");
                    }
                    ReadOnlyMemory<byte> passageBytes = retainedSource[
                        checked((int)passage.Utf8StartOffset)..checked((int)passage.Utf8EndOffset)];
                    if (!StringComparer.Ordinal.Equals(Hash(passageBytes.Span), passage.PassageFingerprint.Value))
                    {
                        throw new InvalidDataException("A documentation passage fingerprint does not match its exact UTF-8 byte slice.");
                    }
                    passagePayloadId = AdmitCoordinatorPayload(
                        passageBytes.Span,
                        "documentation-passage",
                        passage.PassageId.Value,
                        now,
                        transaction);
                }
                else
                {
                    passagePayloadId = ScalarString(
                        "SELECT passage_payload_id FROM documentation_passages WHERE documentation_passage_id = $passage;",
                        transaction,
                        ("$passage", passage.PassageId.Value));
                }
                passagePayloadIds.Add(passage.PassageId, passagePayloadId);
                Execute(
                    """
                    INSERT OR IGNORE INTO documentation_passages(
                        documentation_passage_id, documentation_revision_id,
                        utf8_byte_start, utf8_byte_end, passage_sha256,
                        passage_payload_id, availability_state, created_at)
                    VALUES ($passage, $revision, $start, $end, $sha, $payload, 'present', $now);
                    """,
                    transaction,
                    ("$passage", passage.PassageId.Value),
                    ("$revision", passage.RevisionId.Value),
                    ("$start", passage.Utf8StartOffset),
                    ("$end", passage.Utf8EndOffset),
                    ("$sha", passage.PassageFingerprint.Value),
                    ("$payload", passagePayloadId),
                    ("$now", ToText(now)));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM documentation_passages
                    WHERE documentation_passage_id = $passage
                      AND documentation_revision_id = $revision
                      AND utf8_byte_start = $start
                      AND utf8_byte_end = $end
                      AND passage_sha256 = $sha
                      AND passage_payload_id = $payload
                      AND availability_state = 'present';
                    """,
                    "A retained documentation passage ID resolves to different bytes or range.",
                    transaction,
                    ("$passage", passage.PassageId.Value),
                    ("$revision", passage.RevisionId.Value),
                    ("$start", passage.Utf8StartOffset),
                    ("$end", passage.Utf8EndOffset),
                    ("$sha", passage.PassageFingerprint.Value),
                    ("$payload", passagePayloadId));
            }

            foreach (DocumentationClaimContract claim in evidence.Claims)
            {
                if (cleanImport)
                {
                    Execute(
                    """
                    INSERT OR IGNORE INTO evidence_revisions(
                        evidence_revision_id, documentation_passage_id, import_id,
                        payload_schema_id, payload_schema_version, evidence_kind,
                        claim_kind, authority_kind, applicability_state,
                        classification_role, evidence_state, evidence_payload_id,
                        contradiction_payload_id, created_at)
                    VALUES (
                        $claim, $passage, $import, $schema, '1.0.0', 'documentation-claim',
                        $kind, $authority, $applicability, $role, 'admitted', $payload,
                        $contradiction, $now);
                    """,
                    transaction,
                    ("$claim", claim.ClaimId.Value),
                    ("$passage", claim.PassageId.Value),
                    ("$import", claim.ProducingImportId.Value),
                    ("$schema", ContractConstants.DocumentationEvidenceSchemaId),
                    ("$kind", ClaimKindToken(claim.Kind)),
                    ("$authority", EvidenceAuthorityToken(claim.Authority)),
                    ("$applicability", ApplicabilityToken(claim.Applicability)),
                    ("$role", ClassificationRoleToken(claim.ClassificationRole)),
                    ("$payload", evidencePayloadId),
                    ("$contradiction", claim.ContradictingEvidenceIds.Count == 0 ? null : evidencePayloadId),
                        ("$now", ToText(now)));
                }
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM evidence_revisions
                    WHERE evidence_revision_id = $claim
                      AND documentation_passage_id = $passage
                      AND import_id = $import
                      AND payload_schema_id = $schema
                      AND payload_schema_version = '1.0.0'
                      AND evidence_kind = 'documentation-claim'
                      AND claim_kind = $kind
                      AND authority_kind = $authority
                      AND applicability_state = $applicability
                      AND classification_role = $role
                      AND evidence_state = 'admitted'
                      AND (($has_contradiction = 0 AND contradiction_payload_id IS NULL)
                           OR ($has_contradiction = 1 AND contradiction_payload_id IS NOT NULL));
                    """,
                    "A retained documentation claim ID resolves to different claim semantics.",
                    transaction,
                    ("$claim", claim.ClaimId.Value),
                    ("$passage", claim.PassageId.Value),
                    ("$import", claim.ProducingImportId.Value),
                    ("$schema", ContractConstants.DocumentationEvidenceSchemaId),
                    ("$kind", ClaimKindToken(claim.Kind)),
                    ("$authority", EvidenceAuthorityToken(claim.Authority)),
                    ("$applicability", ApplicabilityToken(claim.Applicability)),
                    ("$role", ClassificationRoleToken(claim.ClassificationRole)),
                    ("$has_contradiction", claim.ContradictingEvidenceIds.Count == 0 ? 0 : 1));
            }

            foreach (ClaimApplicationContract application in evidence.Applications)
            {
                string bindingId = ScalarString(
                    """
                    SELECT documentation_application_binding_id
                    FROM documentation_application_bindings
                    WHERE run_id = $run
                      AND analysis_context_id = $context
                      AND subject_id = $subject
                      AND subject_type = $subject_type
                      AND dependency_closure_id = $closure;
                    """,
                    transaction,
                    ("$run", application.ConsumingRunId.Value),
                    ("$context", application.AnalysisContextId.Value),
                    ("$subject", application.SubjectId.Value),
                    ("$subject_type", application.SubjectType),
                    ("$closure", application.DependencyClosureId.Value));
                Execute(
                    """
                    INSERT OR IGNORE INTO evidence_application_links(
                        evidence_application_link_id, evidence_revision_id, run_id,
                        application_binding_id, analysis_context_id, subject_id, subject_type,
                        dependency_closure_id, application_state,
                        application_payload_id, created_at)
                    VALUES ($application, $claim, $run, $binding, $context, $subject, $subject_type, $closure, $state, $payload, $now);
                    """,
                    transaction,
                    ("$application", application.ApplicationId.Value),
                    ("$claim", application.ClaimId.Value),
                    ("$run", application.ConsumingRunId.Value),
                    ("$binding", bindingId),
                    ("$context", application.AnalysisContextId.Value),
                    ("$subject", application.SubjectId.Value),
                    ("$subject_type", application.SubjectType),
                    ("$closure", application.DependencyClosureId.Value),
                    ("$state", ApplicabilityToken(application.Applicability)),
                    ("$payload", evidencePayloadId),
                    ("$now", ToText(now)));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM evidence_application_links
                    WHERE evidence_application_link_id = $application
                      AND evidence_revision_id = $claim
                      AND run_id = $run
                      AND application_binding_id = $binding
                      AND analysis_context_id = $context
                      AND subject_id = $subject
                      AND subject_type = $subject_type
                      AND dependency_closure_id = $closure
                      AND application_state = $state;
                    """,
                    "A retained claim-application ID resolves to different application semantics.",
                    transaction,
                    ("$application", application.ApplicationId.Value),
                    ("$claim", application.ClaimId.Value),
                    ("$run", application.ConsumingRunId.Value),
                    ("$binding", bindingId),
                    ("$context", application.AnalysisContextId.Value),
                    ("$subject", application.SubjectId.Value),
                    ("$subject_type", application.SubjectType),
                    ("$closure", application.DependencyClosureId.Value),
                    ("$state", ApplicabilityToken(application.Applicability)));
            }

            foreach (DocumentationPurposeAssignmentContract assignment in evidence.PurposeAssignments)
            {
                ClaimApplicationContract application = evidence.Applications.Single(item =>
                    item.SubjectId == assignment.SubjectId
                    && StringComparer.Ordinal.Equals(item.SubjectType, assignment.SubjectType)
                    && item.ClaimId == assignment.ClaimId
                    && item.ApplicationId == assignment.ApplicationId);
                Execute(
                    """
                    INSERT OR IGNORE INTO taxonomy_assignments(
                        taxonomy_assignment_id, run_id, subject_kind, subject_id,
                        taxonomy_id, taxonomy_version, axis, facet, taxonomy_code,
                        applicability_state, classification_role, assignment_payload_id, created_at)
                    VALUES (
                        $assignment, $run, $subject_kind, $subject, $taxonomy, $version,
                        $axis, $facet, $code, 'assigned', 'declared', $payload, $now);
                    """,
                    transaction,
                    ("$assignment", assignment.AssignmentId.Value),
                    ("$run", application.ConsumingRunId.Value),
                    ("$subject_kind", assignment.SubjectType),
                    ("$subject", assignment.SubjectId.Value),
                    ("$taxonomy", assignment.TaxonomyId),
                    ("$version", assignment.TaxonomyVersion.ToString()),
                    ("$axis", assignment.Axis),
                    ("$facet", assignment.Facet),
                    ("$code", assignment.Code),
                    ("$payload", evidencePayloadId),
                    ("$now", assignment.CreatedAt.ToString()));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM taxonomy_assignments
                    WHERE taxonomy_assignment_id = $assignment
                      AND run_id = $run
                      AND subject_kind = $subject_kind
                      AND subject_id = $subject
                      AND taxonomy_id = $taxonomy
                      AND taxonomy_version = $version
                      AND axis = $axis
                      AND facet = $facet
                      AND taxonomy_code = $code
                      AND applicability_state = 'assigned'
                      AND classification_role = 'declared';
                    """,
                    "A retained purpose-assignment ID resolves to different taxonomy semantics.",
                    transaction,
                    ("$assignment", assignment.AssignmentId.Value),
                    ("$run", application.ConsumingRunId.Value),
                    ("$subject_kind", assignment.SubjectType),
                    ("$subject", assignment.SubjectId.Value),
                    ("$taxonomy", assignment.TaxonomyId),
                    ("$version", assignment.TaxonomyVersion.ToString()),
                    ("$axis", assignment.Axis),
                    ("$facet", assignment.Facet),
                    ("$code", assignment.Code));
                string conditionIdsJson = JsonSerializer.Serialize(
                    assignment.ApplicabilityConditionIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO documentation_purpose_assignment_details(
                        taxonomy_assignment_id, evidence_revision_id, evidence_application_link_id,
                        analyzer_or_adjudicator_id, applicability_condition_ids_json, reason, detail_payload_id)
                    VALUES ($assignment, $claim, $application, $analyzer, $conditions, $reason, $payload);
                    """,
                    transaction,
                    ("$assignment", assignment.AssignmentId.Value),
                    ("$claim", assignment.ClaimId.Value),
                    ("$application", assignment.ApplicationId.Value),
                    ("$analyzer", assignment.AnalyzerOrAdjudicatorId.Value),
                    ("$conditions", conditionIdsJson),
                    ("$reason", assignment.Reason),
                    ("$payload", evidencePayloadId));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM documentation_purpose_assignment_details
                    WHERE taxonomy_assignment_id = $assignment
                      AND evidence_revision_id = $claim
                      AND evidence_application_link_id = $application
                      AND analyzer_or_adjudicator_id = $analyzer
                      AND applicability_condition_ids_json = $conditions
                      AND reason = $reason;
                    """,
                    "A documentation purpose assignment resolves to different evidence or derivation semantics.",
                    transaction,
                    ("$assignment", assignment.AssignmentId.Value),
                    ("$claim", assignment.ClaimId.Value),
                    ("$application", assignment.ApplicationId.Value),
                    ("$analyzer", assignment.AnalyzerOrAdjudicatorId.Value),
                    ("$conditions", conditionIdsJson),
                    ("$reason", assignment.Reason));
            }

            foreach (DocumentationGapContract gap in evidence.Gaps)
            {
                Execute(
                    """
                    INSERT OR IGNORE INTO analysis_gaps(
                        gap_id, run_id, population_id, stage_id, gap_state,
                        replay_effect, conclusion_effect, gap_payload_id, created_at)
                    VALUES (
                        $gap, $run, 'documentation-evidence', 'documentation-import',
                        $state, $replay, $conclusion, $payload, $now);
                    """,
                    transaction,
                    ("$gap", gap.GapId.Value),
                    ("$run", evidence.OriginatingRunId.Value),
                    ("$state", gap.Kind switch
                    {
                        DocumentationGapKind.Deletion => "deleted",
                        DocumentationGapKind.UnavailableSource => "unavailable",
                        DocumentationGapKind.Replay => "audit-gap",
                        _ => "missing-information",
                    }),
                    ("$replay", ReplayEffectToken(gap.ReplayEffect)),
                    ("$conclusion", gap.Kind == DocumentationGapKind.Contradiction
                        || gap.ReplayEffect == ReplayState.Partial ? "bounded" : "unavailable"),
                    ("$payload", evidencePayloadId),
                    ("$now", ToText(now)));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM analysis_gaps
                    WHERE gap_id = $gap
                      AND run_id = $run
                      AND population_id = 'documentation-evidence'
                      AND stage_id = 'documentation-import'
                      AND gap_state = $state
                      AND replay_effect = $replay
                      AND conclusion_effect = $conclusion
                      AND created_at = $created;
                    """,
                    "A documentation gap ID resolves to different provenance or replay semantics.",
                    transaction,
                    ("$gap", gap.GapId.Value),
                    ("$run", gap.OriginatingRunId.Value),
                    ("$state", gap.Kind switch
                    {
                        DocumentationGapKind.Deletion => "deleted",
                        DocumentationGapKind.UnavailableSource => "unavailable",
                        DocumentationGapKind.Replay => "audit-gap",
                        _ => "missing-information",
                    }),
                    ("$replay", ReplayEffectToken(gap.ReplayEffect)),
                    ("$conclusion", gap.Kind == DocumentationGapKind.Contradiction
                        || gap.ReplayEffect == ReplayState.Partial ? "bounded" : "unavailable"),
                    ("$created", gap.CreatedAt.ToString()));
                Execute(
                    """
                    INSERT OR IGNORE INTO documentation_gap_details(
                        gap_id, documentation_revision_id, evidence_revision_id,
                        evidence_application_link_id, gap_kind, reason, detail_payload_id)
                    VALUES ($gap, $revision, $claim, $application, $kind, $reason, $payload);
                    """,
                    transaction,
                    ("$gap", gap.GapId.Value),
                    ("$revision", gap.RevisionId.Value),
                    ("$claim", gap.ClaimId?.Value),
                    ("$application", gap.ApplicationId?.Value),
                    ("$kind", DocumentationGapKindToken(gap.Kind)),
                    ("$reason", gap.Reason),
                    ("$payload", evidencePayloadId));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM documentation_gap_details
                    WHERE gap_id = $gap
                      AND documentation_revision_id = $revision
                      AND evidence_revision_id IS $claim
                      AND evidence_application_link_id IS $application
                      AND gap_kind = $kind
                      AND reason = $reason;
                    """,
                    "A documentation gap ID resolves to different exact gap semantics.",
                    transaction,
                    ("$gap", gap.GapId.Value),
                    ("$revision", gap.RevisionId.Value),
                    ("$claim", gap.ClaimId?.Value),
                    ("$application", gap.ApplicationId?.Value),
                    ("$kind", DocumentationGapKindToken(gap.Kind)),
                    ("$reason", gap.Reason));
            }

            List<string> deletedObjectPaths = [];
            foreach (DocumentationDeletionReceiptContract receipt in evidence.DeletionReceipts)
            {
                string passageIdsJson = JsonSerializer.Serialize(
                    receipt.DeletedPassageIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                string retainedIdsJson = JsonSerializer.Serialize(
                    receipt.IndependentlyRetainedPayloadIds.Select(item => item.Value).Order(StringComparer.Ordinal).ToArray());
                Execute(
                    """
                    INSERT OR IGNORE INTO documentation_deletion_receipts(
                        documentation_deletion_receipt_id, run_id, documentation_revision_id,
                        deleted_body_sha256, deleted_passage_ids_json,
                        independently_retained_payload_ids_json, replay_effect,
                        receipt_payload_id, reason, deleted_at)
                    VALUES ($receipt, $run, $revision, $sha, $passages, $retained, $replay, $payload, $reason, $deleted);
                    """,
                    transaction,
                    ("$receipt", receipt.ReceiptId.Value),
                    ("$run", receipt.OriginatingRunId.Value),
                    ("$revision", receipt.RevisionId.Value),
                    ("$sha", receipt.DeletedBodyFingerprint.Value),
                    ("$passages", passageIdsJson),
                    ("$retained", retainedIdsJson),
                    ("$replay", ReplayEffectToken(receipt.ReplayEffect)),
                    ("$payload", evidencePayloadId),
                    ("$reason", receipt.Reason),
                    ("$deleted", receipt.DeletedAt.ToString()));
                RequireSingleDocumentationRow(
                    """
                    SELECT COUNT(*) FROM documentation_deletion_receipts
                    WHERE documentation_deletion_receipt_id = $receipt
                      AND run_id = $run
                      AND documentation_revision_id = $revision
                      AND deleted_body_sha256 = $sha
                      AND deleted_passage_ids_json = $passages
                      AND independently_retained_payload_ids_json = $retained
                      AND replay_effect = $replay
                      AND reason = $reason
                      AND deleted_at = $deleted;
                    """,
                    "A documentation deletion receipt ID resolves to different deletion semantics.",
                    transaction,
                    ("$receipt", receipt.ReceiptId.Value),
                    ("$run", receipt.OriginatingRunId.Value),
                    ("$revision", receipt.RevisionId.Value),
                    ("$sha", receipt.DeletedBodyFingerprint.Value),
                    ("$passages", passageIdsJson),
                    ("$retained", retainedIdsJson),
                    ("$replay", ReplayEffectToken(receipt.ReplayEffect)),
                    ("$reason", receipt.Reason),
                    ("$deleted", receipt.DeletedAt.ToString()));

                HashSet<string> deletionPayloadIds = passagePayloadIds
                    .Where(item => receipt.DeletedPassageIds.Contains(item.Key))
                    .Select(item => item.Value)
                    .ToHashSet(StringComparer.Ordinal);
                if (bodyPayloadId is not null)
                {
                    deletionPayloadIds.Add(bodyPayloadId);
                }
                deletionPayloadIds.Remove(evidencePayloadId);
                HashSet<string> expectedRetainedIdentities = deletionPayloadIds
                    .Where(payloadId =>
                        HasExternalDocumentationDeletionOwner(
                            payloadId,
                            receipt.RevisionId.Value,
                            receipt.DeletedPassageIds.Select(item => item.Value),
                            transaction)
                        || HasDocumentationBackupPin(payloadId, transaction))
                    .Select(payloadId => DocumentationPayloadSemanticId(payloadId, transaction).Value)
                    .ToHashSet(StringComparer.Ordinal);
                if (!expectedRetainedIdentities.SetEquals(
                        receipt.IndependentlyRetainedPayloadIds.Select(item => item.Value)))
                {
                    throw new InvalidDataException(
                        "A documentation deletion receipt does not exactly identify independently retained payloads.");
                }
                foreach (string deletionPayloadId in deletionPayloadIds)
                {
                    if (HasExternalDocumentationDeletionOwner(
                            deletionPayloadId,
                            receipt.RevisionId.Value,
                            receipt.DeletedPassageIds.Select(item => item.Value),
                            transaction))
                    {
                        continue;
                    }
                    RequireDeletionPayloadOwners(
                        deletionPayloadId,
                        receipt.RevisionId.Value,
                        receipt.DeletedPassageIds.Select(item => item.Value),
                        transaction);
                    string objectPath = ScalarString(
                        "SELECT object_relative_path FROM payloads WHERE payload_id = $payload AND retention_state = 'retained';",
                        transaction,
                        ("$payload", deletionPayloadId));
                    Execute(
                        "UPDATE payloads SET retention_state = 'deleted' WHERE payload_id = $payload AND retention_state = 'retained';",
                        transaction,
                        ("$payload", deletionPayloadId));
                    deletedObjectPaths.Add(objectPath);
                }
            }

            InsertDocumentationDependencyEdges(evidence, evidencePayloadId, now, transaction);
            RequireExactDocumentationDependencySets(evidence, transaction);
            transaction.Commit();
            foreach (string objectPath in deletedObjectPaths.Distinct(StringComparer.Ordinal))
            {
                Paths.DeleteFile(
                    ProductWriteClass.Payload,
                    objectPath["payloads/".Length..].Replace('/', Path.DirectorySeparatorChar),
                    missingIsSuccess: true);
            }
            return new DocumentationEvidencePersistenceReceipt(
                evidence.PayloadId.Value,
                evidencePayloadId,
                revision.RevisionId.Value,
                import.ImportId.Value,
                evidence.Claims.Count,
                evidence.Applications.Count,
                evidence.PurposeAssignments.Count,
                evidence.DeletionReceipts.Count,
                evidence.Gaps.Count);
        }
    }

    public byte[] ReadDocumentationEvidencePayload(string payloadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT object_relative_path, content_sha256, byte_length FROM payloads WHERE payload_id = $payload;";
            command.Parameters.AddWithValue("$payload", payloadId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"Payload '{payloadId}' does not exist.");
            }

            string relativePath = reader.GetString(0);
            string expectedSha = reader.GetString(1);
            long expectedLength = reader.GetInt64(2);
            using FileStream stream = Paths.OpenReadFile(
                ProductWriteClass.Payload,
                relativePath["payloads/".Length..].Replace('/', Path.DirectorySeparatorChar));
            if (expectedLength > int.MaxValue)
            {
                throw new InvalidDataException("Documentation evidence payload exceeds the readback bound.");
            }
            byte[] bytes = new byte[checked((int)expectedLength)];
            stream.ReadExactly(bytes);
            if (!StringComparer.Ordinal.Equals(Hash(bytes), expectedSha))
            {
                throw new InvalidDataException("Documentation evidence payload failed readback identity validation.");
            }
            return bytes;
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
            var known = new Dictionary<string, (string Sha, long Length, string Retention)>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT object_relative_path, content_sha256, byte_length, retention_state FROM payloads;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    known[reader.GetString(0).Replace('/', Path.DirectorySeparatorChar)] =
                        (reader.GetString(1), reader.GetInt64(2), reader.GetString(3));
                }
            }

            foreach (var entry in known)
            {
                var fullPath = Paths.ResolveProductPath(
                    ProductWriteClass.Payload,
                    entry.Key["payloads".Length..]
                        .TrimStart(Path.DirectorySeparatorChar));
                bool exists = File.Exists(fullPath);
                if (StringComparer.Ordinal.Equals(entry.Value.Retention, "deleted"))
                {
                    if (exists)
                    {
                        issues.Add(new ReconciliationIssue(
                            "deleted-payload-present",
                            entry.Key,
                            "Deleted payload bytes remain physically present."));
                    }
                    continue;
                }
                if (!exists)
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
        string safeLabel = string.Concat(label.Where(char.IsLetterOrDigit));
        if (safeLabel.Length is 0 or > 48)
        {
            throw new ArgumentException("The backup label must contain 1-48 letters or digits.", nameof(label));
        }

        lock (gate)
        {
            string stamp = now.UtcDateTime.ToString(
                "yyyyMMddTHHmmssfffZ",
                System.Globalization.CultureInfo.InvariantCulture);
            string backupDatabaseName = $"{stamp}-{safeLabel}.sqlite3";
            string databasePath = Paths.ResolveProductPath(
                ProductWriteClass.Backup,
                backupDatabaseName);
            bool reservationCreated = false;
            try
            {
                using (FileStream reservation = Paths.CreateNewFile(
                           ProductWriteClass.Backup,
                           backupDatabaseName))
                {
                    reservationCreated = true;
                    reservation.Flush(flushToDisk: true);
                }

                using (WindowsGuardedSqliteVfs backupVfs = new(
                           Paths,
                           ProductWriteClass.Backup,
                           backupDatabaseName))
                {
                    using SqliteConnection destination = new(
                        new SqliteConnectionStringBuilder
                        {
                            DataSource = databasePath,
                            Mode = SqliteOpenMode.ReadWrite,
                            Pooling = false,
                            Vfs = backupVfs.Name,
                        }.ToString());
                    destination.Open();
                    sqliteVfs.VerifyAllGuards();
                    connection.BackupDatabase(destination);
                    backupVfs.VerifyAllGuards();
                }

                string databaseSha;
                using (FileStream database = Paths.OpenReadFile(
                           ProductWriteClass.Backup,
                           backupDatabaseName))
                {
                    databaseSha = HashStream(database);
                }

                List<BackupPayloadManifest> payloads = [];
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText =
                        """
                        SELECT content_sha256, byte_length, object_relative_path
                        FROM payloads
                        WHERE retention_state = 'retained'
                        ORDER BY content_sha256;
                        """;
                    using SqliteDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string sha256 = reader.GetString(0);
                        long byteLength = reader.GetInt64(1);
                        string relativePath = reader.GetString(2);
                        string payloadRelative = relativePath["payloads/".Length..]
                            .Replace('/', Path.DirectorySeparatorChar);
                        Paths.CopyFile(
                            ProductWriteClass.Payload,
                            payloadRelative,
                            ProductWriteClass.Backup,
                            Path.Combine(
                                backupDatabaseName + ".payloads",
                                payloadRelative),
                            byteLength,
                            sha256);
                        payloads.Add(new BackupPayloadManifest(
                            sha256,
                            byteLength,
                            relativePath));
                    }
                }

                string manifestPath = Paths.ResolveProductPath(
                    ProductWriteClass.Backup,
                    backupDatabaseName + ".manifest.json");
                byte[] manifest = JsonSerializer.SerializeToUtf8Bytes(
                    new BackupManifest(
                        CurrentSchemaVersion,
                        BindingIdentity,
                        databaseSha,
                        payloads,
                        now),
                    new JsonSerializerOptions { WriteIndented = true });
                if (manifest.Length is 0 or > MaximumBackupManifestBytes)
                {
                    throw new InvalidOperationException(
                        "The backup manifest exceeds its finite bound.");
                }

                using (FileStream manifestStream = Paths.CreateNewFile(
                           ProductWriteClass.Backup,
                           backupDatabaseName + ".manifest.json"))
                {
                    manifestStream.Write(manifest);
                    manifestStream.Flush(flushToDisk: true);
                }

                BackupArtifact artifact =
                    new(databasePath, manifestPath, databaseSha);
                _ = ValidateBackup(artifact);
                using (SqliteTransaction transaction = BeginTransaction())
                {
                    Execute(
                        """
                        INSERT INTO payload_backup_pins(
                            payload_id, backup_identity, content_sha256, created_at)
                        SELECT payload_id, $backup, content_sha256, $now
                        FROM payloads
                        WHERE retention_state = 'retained';
                        """,
                        transaction,
                        ("$backup", backupDatabaseName),
                        ("$now", ToText(now)));
                    InsertAuditEvent(
                        "backup-created",
                        "backup",
                        Path.GetFileName(databasePath),
                        now,
                        transaction);
                    transaction.Commit();
                }

                return artifact;
            }
            catch (Exception backupException) when (reservationCreated)
            {
                try
                {
                    CleanupFailedBackup(backupDatabaseName);
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        "Backup creation failed and its partial bundle could not be removed.",
                        backupException,
                        cleanupException);
                }

                throw;
            }
        }
    }

    private void CleanupFailedBackup(string backupDatabaseName)
    {
        Paths.DeleteFile(
            ProductWriteClass.Backup,
            backupDatabaseName + ".manifest.json",
            missingIsSuccess: true);
        Paths.DeleteDirectoryTree(
            ProductWriteClass.Backup,
            backupDatabaseName + ".payloads",
            missingIsSuccess: true);
        foreach (string suffix in new[] { "-journal", "-shm", "-wal", string.Empty })
        {
            Paths.DeleteFile(
                ProductWriteClass.Backup,
                backupDatabaseName + suffix,
                missingIsSuccess: true);
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
        StoragePaths staging = target.CreateRestoreStagingPaths();
        bool published = false;
        try
        {
            staging.Create();
            staging.CopyExternalFileIntoProduct(
                ProductWriteClass.Data,
                "infinium.sqlite3",
                backup.DatabasePath,
                validated.DatabaseByteLength,
                validated.Manifest.DatabaseSha256);
            string backupPayloadRoot = backup.DatabasePath + ".payloads";
            foreach (BackupPayloadManifest payload in validated.Manifest.Payloads)
            {
                string source = PayloadPath(backupPayloadRoot, payload.Sha256);
                staging.CopyExternalFileIntoProduct(
                    ProductWriteClass.Payload,
                    payload.RelativePath["payloads/".Length..]
                        .Replace('/', Path.DirectorySeparatorChar),
                    source,
                    payload.ByteLength,
                    payload.Sha256);
            }

            using (FileStream stagedDatabase = staging.OpenReadFile(
                       ProductWriteClass.Data,
                       "infinium.sqlite3"))
            {
                if (!string.Equals(
                        HashStream(stagedDatabase),
                        validated.Manifest.DatabaseSha256,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The staged restore database fingerprint is invalid.");
                }
            }

            List<PublicationFileExpectation> expectedFiles;
            using (FileStream stagedDatabase = AppendRestoreAudit(
                       staging,
                       target.AuthorityIdentity,
                       DateTimeOffset.UtcNow))
            {
                long stagedDatabaseLength = stagedDatabase.Length;
                string stagedDatabaseSha256 = HashStream(stagedDatabase);
                IReadOnlyList<BackupPayloadManifest> stagedPayloads =
                    ValidateDatabaseFile(
                        staging.Database,
                        validated.Manifest.Sqlite);
                ValidateManifestPayloadSet(
                    validated.Manifest.Payloads,
                    stagedPayloads);
                ValidatePayloadFiles(
                    staging.Payloads,
                    validated.Manifest.Payloads);

                expectedFiles =
                [
                    new(
                        Path.Combine("data", "infinium.sqlite3"),
                        stagedDatabaseLength,
                        stagedDatabaseSha256),
                ];
                expectedFiles.AddRange(validated.Manifest.Payloads.Select(payload =>
                    new PublicationFileExpectation(
                        payload.RelativePath.Replace(
                            '/',
                            Path.DirectorySeparatorChar),
                        payload.ByteLength,
                        payload.Sha256)));
            }

            target.PublishFrom(staging, expectedFiles);
            published = true;
        }
        finally
        {
            if (!published && staging.HasBoundProductRoot)
            {
                staging.DeleteProductTree();
            }

            staging.Dispose();
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

        BackupManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BackupManifest>(
                ReadBoundedFile(
                    backup.ManifestPath,
                    MaximumBackupManifestBytes,
                    "backup manifest"),
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

        string actualDatabaseSha;
        long databaseByteLength;
        using (FileStream database = new(
                   backup.DatabasePath,
                   FileMode.Open,
                   FileAccess.Read,
                   FileShare.Read))
        {
            databaseByteLength = database.Length;
            actualDatabaseSha = HashStream(database);
        }

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
        return new ValidatedBackup(manifest, databaseByteLength);
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

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                """
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id = 'M1-S3-0002'
                  AND from_version = 1
                  AND to_version = 2
                  AND sqlite_source_id = $source;
                """;
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(
                    migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The snapshot-capture storage migration identity is invalid.");
            }
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                """
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id = 'M1-S4-0003'
                  AND from_version = 2
                  AND to_version = 3
                  AND sqlite_source_id = $source;
                """;
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(
                    migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The durable run-operation storage migration identity is invalid.");
            }
        }

        using (var migration = database.CreateCommand())
        {
            migration.CommandText =
                """
                SELECT COUNT(*)
                FROM migration_history
                WHERE migration_id = 'M1-S5-0004'
                  AND from_version = 3
                  AND to_version = 4
                  AND sqlite_source_id = $source;
                """;
            migration.Parameters.AddWithValue("$source", binding.SourceId);
            if (Convert.ToInt32(
                    migration.ExecuteScalar(),
                    System.Globalization.CultureInfo.InvariantCulture) != 1)
            {
                throw new InvalidOperationException(
                    "The Slice 5 analytical storage migration identity is invalid.");
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
            string missing = string.Join(",", RequiredSchemaObjects.Except(actualObjects).Order(StringComparer.Ordinal));
            string unexpected = string.Join(",", actualObjects.Except(RequiredSchemaObjects).Order(StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"The database schema objects do not match the supported storage contract. Missing=[{missing}] Unexpected=[{unexpected}]");
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
            WHERE retention_state = 'retained'
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

            using FileStream file = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            if (file.Length != payload.ByteLength
                || !string.Equals(HashStream(file), payload.Sha256, StringComparison.Ordinal))
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

            if (current <= 1)
            {
                using var transaction = BeginTransaction();
                Execute(SchemaV2, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '2' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.1.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S3-0002', 1, 2, $now, $sqlite_source);
                    PRAGMA user_version = 2;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 2)
            {
                using var transaction = BeginTransaction();
                Execute(SchemaV3, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '3' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.2.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S4-0003', 2, 3, $now, $sqlite_source);
                    PRAGMA user_version = 3;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 3)
            {
                ValidateSchema3MigrationSource();
                using var transaction = BeginTransaction();
                Execute(SchemaV4, transaction);
                CreateSchemaV4AppendOnlyTriggers(transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '4' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.3.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S5-0004', 3, 4, $now, $sqlite_source);
                    PRAGMA user_version = 4;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }

            if (current <= 4)
            {
                ValidateSchema4MigrationSource();
                using var transaction = BeginTransaction();
                Execute(SchemaV5, transaction);
                CreateAppendOnlyTriggers(SchemaV5AppendOnlyTables, transaction);
                string schemaFingerprint = ComputeSchemaFingerprint(connection, transaction);
                Execute(
                    """
                    UPDATE store_metadata SET value = '5' WHERE key = 'schema_version';
                    UPDATE store_metadata SET value = '1.4.0'
                    WHERE key = 'storage_contract_version';
                    UPDATE store_metadata SET value = $schema_fingerprint
                    WHERE key = 'schema_fingerprint';
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S5-WP4-0005', 4, 5, $now, $sqlite_source);
                    PRAGMA user_version = 5;
                    """,
                    transaction,
                    ("$schema_fingerprint", schemaFingerprint),
                    ("$sqlite_source", BindingIdentity.SourceId),
                    ("$now", ToText(DateTimeOffset.UtcNow)));
                transaction.Commit();
            }
        }
    }

    private void ValidateSchema4MigrationSource()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT key, value FROM store_metadata
                WHERE key IN (
                    'schema_version','schema_fingerprint','storage_contract_version',
                    'sqlite_version','sqlite_source_id');
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata.Add(reader.GetString(0), reader.GetString(1));
            }
        }
        const string schema4Fingerprint = "0e4fbeb821fdd83d86737d60979fa35d9a1300a4d971450c516f66d07ef2231e";
        if (metadata.Count != 5
            || metadata["schema_version"] != "4"
            || metadata["storage_contract_version"] != "1.3.0"
            || metadata["sqlite_version"] != BindingIdentity.Version
            || metadata["sqlite_source_id"] != BindingIdentity.SourceId
            || metadata["schema_fingerprint"] != schema4Fingerprint
            || ComputeSchemaFingerprint(connection) != schema4Fingerprint)
        {
            throw new InvalidOperationException(
                "Schema 4 does not match the exact accepted Slice 5 contract required for WP4 migration.");
        }
        using SqliteCommand migration = connection.CreateCommand();
        migration.CommandText =
            """
            SELECT COUNT(*) FROM migration_history
            WHERE migration_id = 'M1-S5-0004' AND from_version = 3 AND to_version = 4
              AND sqlite_source_id = $source;
            """;
        migration.Parameters.AddWithValue("$source", BindingIdentity.SourceId);
        if (Convert.ToInt32(migration.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException("Schema 4 migration provenance is invalid.");
        }
    }

    private void ValidateSchema3MigrationSource()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal);
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT key, value FROM store_metadata
                WHERE key IN (
                    'schema_version','schema_fingerprint','storage_contract_version',
                    'sqlite_version','sqlite_source_id');
                """;
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata.Add(reader.GetString(0), reader.GetString(1));
            }
        }

        if (metadata.Count != 5
            || metadata["schema_version"] != "3"
            || metadata["storage_contract_version"] != "1.2.0"
            || metadata["sqlite_version"] != BindingIdentity.Version
            || metadata["sqlite_source_id"] != BindingIdentity.SourceId
            || metadata["schema_fingerprint"] != SchemaV3Fingerprint
            || ComputeSchemaFingerprint(connection) != SchemaV3Fingerprint)
        {
            throw new InvalidOperationException(
                "Schema 3 does not match the exact M1 storage contract required for migration.");
        }

        using SqliteCommand migration = connection.CreateCommand();
        migration.CommandText =
            """
            SELECT COUNT(*) FROM migration_history
            WHERE migration_id = 'M1-S4-0003'
              AND from_version = 2
              AND to_version = 3
              AND sqlite_source_id = $source;
            """;
        migration.Parameters.AddWithValue("$source", BindingIdentity.SourceId);
        if (Convert.ToInt32(
                migration.ExecuteScalar(),
                System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException(
                "Schema 3 migration provenance is invalid.");
        }
    }

    private void CreateSchemaV4AppendOnlyTriggers(SqliteTransaction transaction)
        => CreateAppendOnlyTriggers(SchemaV4AppendOnlyTables, transaction);

    private void CreateAppendOnlyTriggers(IEnumerable<string> tables, SqliteTransaction transaction)
    {
        foreach (string table in tables)
        {
            Execute(
                $"""
                CREATE TRIGGER {table}_append_only_update
                BEFORE UPDATE ON {table}
                BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
                CREATE TRIGGER {table}_append_only_delete
                BEFORE DELETE ON {table}
                BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
                """,
                transaction);
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

    private SnapshotCaptureOperationRecord GetSnapshotCaptureOperationCore(
        string operationId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT operation_id, durable_command_id, request_json, request_sha256,
                   initiation_kind, dispatch_deadline, lifecycle_state,
                   lifecycle_generation, coordinator_fencing_epoch,
                   installation_snapshot_id, payload_id, created_at, updated_at
            FROM snapshot_capture_operations
            WHERE operation_id = $operation;
            """;
        command.Parameters.AddWithValue("$operation", operationId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new KeyNotFoundException(
                $"Snapshot capture operation '{operationId}' does not exist.");
        }

        return new SnapshotCaptureOperationRecord(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            DateTimeOffset.Parse(
                reader.GetString(5),
                System.Globalization.CultureInfo.InvariantCulture),
            reader.GetString(6),
            reader.GetInt64(7),
            reader.GetInt64(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            DateTimeOffset.Parse(
                reader.GetString(11),
                System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(
                reader.GetString(12),
                System.Globalization.CultureInfo.InvariantCulture));
    }

    private void EnsureCurrentSnapshotCaptureAttempt(
        SnapshotCaptureAttemptRecord attempt,
        SqliteTransaction transaction,
        bool requireUnexpiredLease)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT operation_id, coordinator_fencing_epoch,
                   attempt_fencing_token, outcome, lease_expires_at
            FROM snapshot_capture_attempts
            WHERE attempt_id = $attempt;
            """;
        command.Parameters.AddWithValue("$attempt", attempt.AttemptId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()
            || reader.GetString(0) != attempt.OperationId
            || reader.GetInt64(1) != attempt.CoordinatorFencingEpoch
            || reader.GetInt64(2) != attempt.AttemptFencingToken
            || reader.GetString(3) != "running"
            || (requireUnexpiredLease
                && DateTimeOffset.Parse(
                    reader.GetString(4),
                    System.Globalization.CultureInfo.InvariantCulture)
                    <= DateTimeOffset.UtcNow))
        {
            throw new InvalidOperationException(
                "The snapshot capture attempt is stale or no longer live.");
        }

        SnapshotCaptureOperationRecord operation =
            GetSnapshotCaptureOperationCore(attempt.OperationId);
        if (operation.State != "Running"
            || operation.CoordinatorFencingEpoch != attempt.CoordinatorFencingEpoch)
        {
            throw new InvalidOperationException(
                "The snapshot capture operation fence is stale.");
        }
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

    private void RequireSingleDocumentationRow(
        string sql,
        string message,
        SqliteTransaction transaction,
        params (string Name, object? Value)[] parameters)
    {
        if (ScalarLong(sql, transaction, parameters) != 1)
        {
            throw new InvalidDataException(message);
        }
    }

    private string AdmitCoordinatorPayload(
        ReadOnlySpan<byte> bytes,
        string ownerKind,
        string ownerId,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        if (bytes.Length > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("Documentation payload exceeds the 64 MiB coordinator admission bound.");
        }
        string sha = Hash(bytes);
        string classRelativePath = Path.Combine(sha[..2], sha[2..4], sha);
        string? existingRetentionState = ScalarStringOrNull(
            "SELECT retention_state FROM payloads WHERE content_sha256 = $sha;",
            transaction,
            ("$sha", sha));
        if (existingRetentionState is not null
            && !StringComparer.Ordinal.Equals(existingRetentionState, "retained"))
        {
            throw new InvalidOperationException(
                "A deleted content-addressed payload cannot be silently resurrected by publication.");
        }
        if (Paths.FileExists(ProductWriteClass.Payload, classRelativePath))
        {
            using FileStream existing = Paths.OpenReadFile(ProductWriteClass.Payload, classRelativePath);
            if (existing.Length != bytes.Length || !StringComparer.Ordinal.Equals(HashStream(existing), sha))
            {
                throw new InvalidDataException("An existing content-addressed payload does not match its path identity.");
            }
        }
        else
        {
            Paths.WriteAllBytesAtomic(ProductWriteClass.Payload, classRelativePath, bytes);
        }

        string proposedPayloadId = Guid.NewGuid().ToString("N");
        string objectRelativePath = "payloads/" + classRelativePath.Replace('\\', '/');
        Execute(
            """
            INSERT INTO payloads(
                payload_id, content_sha256, byte_length, codec, retention_state,
                object_relative_path, admitted_at)
            VALUES ($payload, $sha, $length, 'identity', 'retained', $path, $now)
            ON CONFLICT(content_sha256) DO NOTHING;
            """,
            transaction,
            ("$payload", proposedPayloadId),
            ("$sha", sha),
            ("$length", bytes.Length),
            ("$path", objectRelativePath),
            ("$now", ToText(now)));
        string payloadId = ScalarString(
            "SELECT payload_id FROM payloads WHERE content_sha256 = $sha AND byte_length = $length AND object_relative_path = $path;",
            transaction,
            ("$sha", sha),
            ("$length", bytes.Length),
            ("$path", objectRelativePath));
        Execute(
            "INSERT OR IGNORE INTO payload_owners(payload_id, owner_kind, owner_id) VALUES ($payload, $kind, $owner);",
            transaction,
            ("$payload", payloadId),
            ("$kind", ownerKind),
            ("$owner", ownerId));
        return payloadId;
    }

    private void InsertDocumentationDependencyEdges(
        DocumentationEvidenceContract evidence,
        string payloadId,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        DocumentationRevisionContract revision = evidence.Revisions.Single();
        InsertDocumentationEdge(
            evidence.OriginatingRunId.Value,
            "documentation-import",
            evidence.Imports.Single().ImportId.Value,
            "documentation-revision",
            revision.RevisionId.Value,
            "consumes",
            payloadId,
            now,
            transaction);
        if (evidence.Imports.Single().ReusedImportId is { } reusedImportId)
        {
            InsertDocumentationEdge(
                evidence.OriginatingRunId.Value,
                "documentation-import",
                evidence.Imports.Single().ImportId.Value,
                "documentation-import",
                reusedImportId.Value,
                "reuses",
                payloadId,
                now,
                transaction);
        }
        if (revision.SupplyingSnapshotId is { } supplyingSnapshotId)
        {
            InsertDocumentationEdge(
                evidence.OriginatingRunId.Value,
                "documentation-revision",
                revision.RevisionId.Value,
                "snapshot",
                supplyingSnapshotId.Value,
                "depends-on",
                payloadId,
                now,
                transaction);
        }
        foreach (DocumentationPassageContract passage in evidence.Passages)
        {
            InsertDocumentationEdge(
                evidence.OriginatingRunId.Value,
                "passage",
                passage.PassageId.Value,
                "documentation-revision",
                passage.RevisionId.Value,
                "derived-from",
                payloadId,
                now,
                transaction);
        }
        foreach (DocumentationClaimContract claim in evidence.Claims)
        {
            InsertDocumentationEdge(
                evidence.OriginatingRunId.Value,
                "evidence-revision",
                claim.ClaimId.Value,
                "passage",
                claim.PassageId.Value,
                "derived-from",
                payloadId,
                now,
                transaction);
            foreach (OpaqueId contradiction in claim.ContradictingEvidenceIds)
            {
                if (!evidence.Claims.Any(item => item.ClaimId == contradiction))
                {
                    throw new InvalidOperationException("Documentation contradiction edge is dangling.");
                }
                InsertDocumentationEdge(
                    evidence.OriginatingRunId.Value,
                    "evidence-revision",
                    claim.ClaimId.Value,
                    "evidence-revision",
                    contradiction.Value,
                    "contradicts",
                    payloadId,
                    now,
                    transaction);
            }
        }
        foreach (ClaimApplicationContract application in evidence.Applications)
        {
            if (!application.EvidenceIds.All(id => evidence.Claims.Any(item => item.ClaimId == id)))
            {
                throw new InvalidOperationException("Documentation application evidence edge is dangling.");
            }
            InsertDocumentationEdge(
                application.ConsumingRunId.Value,
                "claim-application",
                application.ApplicationId.Value,
                "analysis-context",
                application.AnalysisContextId.Value,
                "applies",
                payloadId,
                now,
                transaction);
            InsertDocumentationEdge(
                application.ConsumingRunId.Value,
                "claim-application",
                application.ApplicationId.Value,
                application.SubjectType,
                application.SubjectId.Value,
                "applies-to",
                payloadId,
                now,
                transaction);
            foreach (OpaqueId evidenceId in application.EvidenceIds)
            {
                InsertDocumentationEdge(
                    application.ConsumingRunId.Value,
                    "claim-application",
                    application.ApplicationId.Value,
                    "evidence-revision",
                    evidenceId.Value,
                    "supported-by",
                    payloadId,
                    now,
                    transaction);
            }
        }
        foreach (DocumentationPurposeAssignmentContract assignment in evidence.PurposeAssignments)
        {
            ClaimApplicationContract application = evidence.Applications.Single(item =>
                item.ApplicationId == assignment.ApplicationId);
            InsertDocumentationEdge(
                application.ConsumingRunId.Value,
                "taxonomy-assignment",
                assignment.AssignmentId.Value,
                "claim-application",
                assignment.ApplicationId.Value,
                "derived-from",
                payloadId,
                now,
                transaction);
            InsertDocumentationEdge(
                application.ConsumingRunId.Value,
                "taxonomy-assignment",
                assignment.AssignmentId.Value,
                "evidence-revision",
                assignment.ClaimId.Value,
                "supported-by",
                payloadId,
                now,
                transaction);
            InsertDocumentationEdge(
                application.ConsumingRunId.Value,
                "taxonomy-assignment",
                assignment.AssignmentId.Value,
                assignment.SubjectType,
                assignment.SubjectId.Value,
                "classifies",
                payloadId,
                now,
                transaction);
            foreach (OpaqueId conditionId in assignment.ApplicabilityConditionIds)
            {
                InsertDocumentationEdge(
                    application.ConsumingRunId.Value,
                    "taxonomy-assignment",
                    assignment.AssignmentId.Value,
                    "evidence-revision",
                    conditionId.Value,
                    "conditioned-by",
                    payloadId,
                    now,
                    transaction);
            }
        }
        foreach (DocumentationGapContract gap in evidence.Gaps)
        {
            InsertDocumentationEdge(
                evidence.OriginatingRunId.Value,
                "documentation-gap",
                gap.GapId.Value,
                "documentation-revision",
                gap.RevisionId.Value,
                "limits",
                payloadId,
                now,
                transaction);
            if (gap.ClaimId is { } claimId)
            {
                InsertDocumentationEdge(
                    evidence.OriginatingRunId.Value,
                    "documentation-gap",
                    gap.GapId.Value,
                    "evidence-revision",
                    claimId.Value,
                    "limits",
                    payloadId,
                    now,
                    transaction);
            }
            if (gap.ApplicationId is { } applicationId)
            {
                InsertDocumentationEdge(
                    evidence.OriginatingRunId.Value,
                    "documentation-gap",
                    gap.GapId.Value,
                    "claim-application",
                    applicationId.Value,
                    "limits",
                    payloadId,
                    now,
                    transaction);
            }
        }
    }

    private void RequireExactDocumentationDependencySets(
        DocumentationEvidenceContract evidence,
        SqliteTransaction transaction)
    {
        foreach (DocumentationClaimContract claim in evidence.Claims)
        {
            RequireExactDocumentationEdgeTargets(
                evidence.OriginatingRunId.Value,
                "evidence-revision",
                claim.ClaimId.Value,
                "evidence-revision",
                "contradicts",
                claim.ContradictingEvidenceIds.Select(item => item.Value),
                transaction);
        }
        foreach (ClaimApplicationContract application in evidence.Applications)
        {
            RequireExactDocumentationEdgeTargets(
                application.ConsumingRunId.Value,
                "claim-application",
                application.ApplicationId.Value,
                "evidence-revision",
                "supported-by",
                application.EvidenceIds.Select(item => item.Value),
                transaction);
        }
        foreach (DocumentationPurposeAssignmentContract assignment in evidence.PurposeAssignments)
        {
            ClaimApplicationContract application = evidence.Applications.Single(item =>
                item.ApplicationId == assignment.ApplicationId);
            RequireExactDocumentationEdgeTargets(
                application.ConsumingRunId.Value,
                "taxonomy-assignment",
                assignment.AssignmentId.Value,
                "evidence-revision",
                "conditioned-by",
                assignment.ApplicabilityConditionIds.Select(item => item.Value),
                transaction);
        }
    }

    private void RequireDeletionPayloadOwners(
        string payloadId,
        string revisionId,
        IEnumerable<string> passageIds,
        SqliteTransaction transaction)
    {
        HashSet<(string Kind, string Id)> permitted = new()
        {
            ("documentation-revision", revisionId),
        };
        permitted.UnionWith(passageIds.Select(item => ("documentation-passage", item)));
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT owner_kind, owner_id FROM payload_owners WHERE payload_id = $payload;";
        command.Parameters.AddWithValue("$payload", payloadId);
        using SqliteDataReader reader = command.ExecuteReader();
        bool found = false;
        while (reader.Read())
        {
            found = true;
            if (!permitted.Contains((reader.GetString(0), reader.GetString(1))))
            {
                throw new InvalidOperationException(
                    "A documentation deletion receipt must identify payloads independently retained by another owner.");
            }
        }
        if (!found)
        {
            throw new InvalidDataException("A documentation deletion target has no admitted payload owner.");
        }
    }

    private Dictionary<string, HashSet<(string Kind, string Id)>> ReadDocumentationDeletionPayloadOwners(
        DocumentationDeletionReceiptContract receipt)
    {
        HashSet<string> targetPayloadIds = [];
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT body_payload_id FROM documentation_revisions WHERE documentation_revision_id = $revision;";
            command.Parameters.AddWithValue("$revision", receipt.RevisionId.Value);
            object? value = command.ExecuteScalar();
            if (value is string payloadId)
            {
                targetPayloadIds.Add(payloadId);
            }
        }
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT documentation_passage_id, passage_payload_id
                FROM documentation_passages
                WHERE documentation_revision_id = $revision;
                """;
            command.Parameters.AddWithValue("$revision", receipt.RevisionId.Value);
            using SqliteDataReader reader = command.ExecuteReader();
            HashSet<string> passageIds = receipt.DeletedPassageIds
                .Select(item => item.Value)
                .ToHashSet(StringComparer.Ordinal);
            while (reader.Read())
            {
                if (passageIds.Contains(reader.GetString(0)) && !reader.IsDBNull(1))
                {
                    targetPayloadIds.Add(reader.GetString(1));
                }
            }
        }
        return targetPayloadIds.ToDictionary(
            payloadId => payloadId,
            payloadId => ReadPayloadOwners(payloadId, null),
            StringComparer.Ordinal);
    }

    private bool HasExternalDocumentationDeletionOwner(
        string payloadId,
        string revisionId,
        IEnumerable<string> passageIds,
        SqliteTransaction transaction)
    {
        HashSet<(string Kind, string Id)> permitted = new()
        {
            ("documentation-revision", revisionId),
        };
        permitted.UnionWith(passageIds.Select(item => ("documentation-passage", item)));
        return ReadPayloadOwners(payloadId, transaction).Any(owner => !permitted.Contains(owner));
    }

    private bool HasDocumentationBackupPin(
        string payloadId,
        SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT COUNT(*) FROM payload_backup_pins WHERE payload_id = $payload;";
        command.Parameters.AddWithValue("$payload", payloadId);
        return Convert.ToInt64(
            command.ExecuteScalar(),
            System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private HashSet<(string Kind, string Id)> ReadPayloadOwners(
        string payloadId,
        SqliteTransaction? transaction)
    {
        HashSet<(string Kind, string Id)> owners = [];
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT owner_kind, owner_id FROM payload_owners WHERE payload_id = $payload;";
        command.Parameters.AddWithValue("$payload", payloadId);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            owners.Add((reader.GetString(0), reader.GetString(1)));
        }
        return owners;
    }

    private OpaqueId DocumentationPayloadSemanticId(
        string payloadId,
        SqliteTransaction? transaction)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT content_sha256 FROM payloads WHERE payload_id = $payload;";
        command.Parameters.AddWithValue("$payload", payloadId);
        object? value = command.ExecuteScalar();
        if (value is not string sha || sha.Length != 64)
        {
            throw new InvalidDataException("A documentation deletion payload identity is missing.");
        }
        return new OpaqueId("payloadsha-" + sha[..32]);
    }

    private static OpaqueId StableDocumentationId(string prefix, params string[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string value in values)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            hash.AppendData(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(bytes.Length)));
            hash.AppendData(bytes);
        }
        return new OpaqueId(prefix + "-" + Convert.ToHexStringLower(hash.GetHashAndReset())[..32]);
    }

    private static string CanonicalDocumentation(IEnumerable<string> values) =>
        string.Concat(values.Order(StringComparer.Ordinal).Select(value =>
            FormattableString.Invariant($"{Encoding.UTF8.GetByteCount(value)}:{value}")));

    private void RequireExactDocumentationEdgeTargets(
        string runId,
        string fromKind,
        string fromId,
        string toKind,
        string edgeKind,
        IEnumerable<string> expectedTargets,
        SqliteTransaction transaction)
    {
        string[] targets = expectedTargets.Order(StringComparer.Ordinal).ToArray();
        long actualCount = ScalarLong(
            """
            SELECT COUNT(*) FROM analysis_dependency_edges
            WHERE run_id = $run AND from_kind = $from_kind AND from_id = $from
              AND to_kind = $to_kind AND edge_kind = $edge_kind;
            """,
            transaction,
            ("$run", runId),
            ("$from_kind", fromKind),
            ("$from", fromId),
            ("$to_kind", toKind),
            ("$edge_kind", edgeKind));
        if (actualCount != targets.Length)
        {
            throw new InvalidDataException("A documentation identity resolves to a different dependency-edge set.");
        }
        foreach (string target in targets)
        {
            RequireSingleDocumentationRow(
                """
                SELECT COUNT(*) FROM analysis_dependency_edges
                WHERE run_id = $run AND from_kind = $from_kind AND from_id = $from
                  AND to_kind = $to_kind AND to_id = $to AND edge_kind = $edge_kind;
                """,
                "A documentation identity resolves to a different dependency-edge target.",
                transaction,
                ("$run", runId),
                ("$from_kind", fromKind),
                ("$from", fromId),
                ("$to_kind", toKind),
                ("$to", target),
                ("$edge_kind", edgeKind));
        }
    }

    private void InsertDocumentationEdge(
        string runId,
        string fromKind,
        string fromId,
        string toKind,
        string toId,
        string edgeKind,
        string payloadId,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        string material = string.Join("\n", runId, fromKind, fromId, toKind, toId, edgeKind);
        string edgeId = "depedge-" + Hash(Encoding.UTF8.GetBytes(material))[..32];
        Execute(
            """
            INSERT OR IGNORE INTO analysis_dependency_edges(
                dependency_edge_id, run_id, from_kind, from_id, to_kind, to_id,
                edge_kind, edge_payload_id, created_at)
            VALUES ($edge, $run, $from_kind, $from, $to_kind, $to, $kind, $payload, $now);
            """,
            transaction,
            ("$edge", edgeId),
            ("$run", runId),
            ("$from_kind", fromKind),
            ("$from", fromId),
            ("$to_kind", toKind),
            ("$to", toId),
            ("$kind", edgeKind),
            ("$payload", payloadId),
            ("$now", ToText(now)));
        RequireSingleDocumentationRow(
            """
            SELECT COUNT(*) FROM analysis_dependency_edges
            WHERE dependency_edge_id = $edge
              AND run_id = $run
              AND from_kind = $from_kind
              AND from_id = $from
              AND to_kind = $to_kind
              AND to_id = $to
              AND edge_kind = $kind;
            """,
            "A documentation dependency-edge ID resolves to different traversal semantics.",
            transaction,
            ("$edge", edgeId),
            ("$run", runId),
            ("$from_kind", fromKind),
            ("$from", fromId),
            ("$to_kind", toKind),
            ("$to", toId),
            ("$kind", edgeKind));
    }

    private static string SourceKindToken(DocumentationSourceKind value) => value switch
    {
        DocumentationSourceKind.ProjectAuthoredLocal => "project-authored-local",
        DocumentationSourceKind.Fixture => "fixture",
        _ => throw new InvalidOperationException("Documentation source kind is not closed."),
    };

    private static string ImportModeToken(DocumentationImportMode value) => value switch
    {
        DocumentationImportMode.CleanImport => "clean-import",
        DocumentationImportMode.RetainedReuse => "retained-reuse",
        _ => throw new InvalidOperationException("Documentation import mode is not closed."),
    };

    private static string ResultStateToken(Slice5ResultState value) => value switch
    {
        Slice5ResultState.Present => "present",
        Slice5ResultState.Partial => "partial",
        Slice5ResultState.Unavailable => "unavailable",
        _ => throw new InvalidOperationException("Documentation retention state is not persistable."),
    };

    private static string ReplayStateToken(ReplayState value) => value switch
    {
        ReplayState.CompleteClean => "complete-clean",
        ReplayState.Partial => "partial",
        ReplayState.AuditOnly => "audit-only",
        ReplayState.Unavailable => "unavailable",
        ReplayState.FailedIdentityDrift => "failed-identity-drift",
        _ => throw new InvalidOperationException("Documentation replay state is not closed."),
    };

    private static string ReplayEffectToken(ReplayState value) => value switch
    {
        ReplayState.CompleteClean => "none",
        ReplayState.Partial => "partial",
        ReplayState.AuditOnly => "audit-only",
        ReplayState.Unavailable or ReplayState.FailedIdentityDrift => "unavailable",
        _ => throw new InvalidOperationException("Documentation replay effect is not closed."),
    };

    private static string DocumentationGapKindToken(DocumentationGapKind value) => value switch
    {
        DocumentationGapKind.Contradiction => "contradiction",
        DocumentationGapKind.Deletion => "deletion",
        DocumentationGapKind.UnavailableSource => "unavailable-source",
        DocumentationGapKind.Replay => "replay",
        _ => throw new InvalidOperationException("Documentation gap kind is not closed."),
    };

    private static string ClaimKindToken(ClaimKind value) => value switch
    {
        ClaimKind.DeclaredPurpose => "declared-purpose",
        ClaimKind.Requirement => "requirement",
        ClaimKind.Incompatibility => "incompatibility",
        ClaimKind.InstallationInstruction => "installation-instruction",
        ClaimKind.PriorityInstruction => "priority-instruction",
        ClaimKind.LifecycleInstruction => "lifecycle-instruction",
        ClaimKind.ConfigurationInstruction => "configuration-instruction",
        ClaimKind.PatchInstruction => "patch-instruction",
        ClaimKind.KnownIssue => "known-issue",
        _ => throw new InvalidOperationException("Documentation claim kind is not closed."),
    };

    private static string EvidenceAuthorityToken(EvidenceAuthority value) => value switch
    {
        EvidenceAuthority.SnapshotBoundLocal => "snapshot-bound-local",
        EvidenceAuthority.DeterministicDerived => "deterministic-derived",
        EvidenceAuthority.AuthoritativeExternal => "authoritative-external",
        EvidenceAuthority.CorroboratedCommunity => "corroborated-community",
        EvidenceAuthority.UncorroboratedReport => "uncorroborated-report",
        EvidenceAuthority.UserStatement => "user-statement",
        EvidenceAuthority.TestResult => "test-result",
        EvidenceAuthority.HeuristicOrLlmInference => "heuristic-or-llm-inference",
        _ => throw new InvalidOperationException("Evidence authority is not closed."),
    };

    private static string ApplicabilityToken(ClaimApplicabilityState value) => value switch
    {
        ClaimApplicabilityState.Applicable => "applicable",
        ClaimApplicabilityState.NotApplicable => "not-applicable",
        ClaimApplicabilityState.Unknown => "unknown",
        ClaimApplicabilityState.Unsupported => "unsupported",
        ClaimApplicabilityState.Contradicted => "contradicted",
        _ => throw new InvalidOperationException("Claim applicability is not closed."),
    };

    private static string ClassificationRoleToken(ClassificationRole value) => value switch
    {
        ClassificationRole.Declared => "declared",
        ClassificationRole.Observed => "observed",
        ClassificationRole.Predicted => "predicted",
        ClassificationRole.Established => "established",
        _ => throw new InvalidOperationException("Classification role is not closed."),
    };

    private static JsonSerializerOptions CreateDocumentationPayloadJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower));
        options.Converters.Add(new PersistenceOpaqueIdJsonConverter());
        options.Converters.Add(new PersistenceContractVersionJsonConverter());
        options.Converters.Add(new PersistenceSha256FingerprintJsonConverter());
        options.Converters.Add(new PersistenceUtcTimestampJsonConverter());
        return options;
    }

    private sealed class PersistenceOpaqueIdJsonConverter : JsonConverter<OpaqueId>
    {
        public override OpaqueId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("Opaque ID must be a string."));

        public override void Write(Utf8JsonWriter writer, OpaqueId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class PersistenceContractVersionJsonConverter : JsonConverter<ContractVersion>
    {
        public override ContractVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            ContractVersion.Parse(reader.GetString() ?? throw new JsonException("Contract version must be a string."));

        public override void Write(Utf8JsonWriter writer, ContractVersion value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    private sealed class PersistenceSha256FingerprintJsonConverter : JsonConverter<Sha256Fingerprint>
    {
        public override Sha256Fingerprint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("SHA-256 must be a string."));

        public override void Write(Utf8JsonWriter writer, Sha256Fingerprint value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }

    private sealed class PersistenceUtcTimestampJsonConverter : JsonConverter<UtcTimestamp>
    {
        public override UtcTimestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            UtcTimestamp.Parse(reader.GetString() ?? throw new JsonException("UTC timestamp must be a string."));

        public override void Write(Utf8JsonWriter writer, UtcTimestamp value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
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
        SqliteTransaction transaction,
        string? detailPayloadId = null)
    {
        Execute(
            """
            INSERT INTO audit_events(
                audit_event_id, event_kind, object_kind, object_id,
                detail_payload_id, occurred_at)
            VALUES ($id, $event, $kind, $object, $payload, $now);
            """,
            transaction,
            ("$id", Guid.NewGuid().ToString("N")),
            ("$event", eventKind),
            ("$kind", objectKind),
            ("$object", objectId),
            ("$payload", detailPayloadId),
            ("$now", ToText(now)));
    }

    private static FileStream AppendRestoreAudit(
        StoragePaths paths,
        string authorityIdentity,
        DateTimeOffset now)
    {
        WindowsGuardedSqliteVfs restoredVfs = new(
            paths,
            ProductWriteClass.Data,
            "infinium.sqlite3");
        SqliteConnection restored = new(new SqliteConnectionStringBuilder
        {
            DataSource = paths.Database,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            Vfs = restoredVfs.Name,
        }.ToString());
        FileStream? pinnedDatabase = null;
        try
        {
            restored.Open();
            ConfigureConnection(restored);
            using (SqliteTransaction transaction = restored.BeginTransaction())
            using (SqliteCommand command = restored.CreateCommand())
            {
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

            using (SqliteCommand checkpoint = restored.CreateCommand())
            {
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                using SqliteDataReader result = checkpoint.ExecuteReader();
                if (!result.Read() || result.GetInt32(0) != 0)
                {
                    throw new InvalidOperationException(
                        "The restored database WAL could not be checkpointed.");
                }
            }

            using (SqliteCommand journalMode = restored.CreateCommand())
            {
                journalMode.CommandText = "PRAGMA journal_mode = DELETE;";
                if (!string.Equals(
                        Convert.ToString(
                            journalMode.ExecuteScalar(),
                            System.Globalization.CultureInfo.InvariantCulture),
                        "delete",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "The restored database could not be normalized to one main file.");
                }
            }

            restoredVfs.VerifyAllGuards();
            restored.Dispose();
            pinnedDatabase = paths.OpenReadFile(
                ProductWriteClass.Data,
                "infinium.sqlite3");
            restoredVfs.VerifyAllGuards();
            restoredVfs.Dispose();
            foreach (string suffix in new[] { "-journal", "-shm", "-wal" })
            {
                paths.DeleteFile(
                    ProductWriteClass.Data,
                    "infinium.sqlite3" + suffix,
                    missingIsSuccess: true);
            }

            FileStream resultStream = pinnedDatabase;
            pinnedDatabase = null;
            return resultStream;
        }
        finally
        {
            pinnedDatabase?.Dispose();
            restored.Dispose();
            restoredVfs.Dispose();
        }
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

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string HashStream(Stream stream) =>
        Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

    private static byte[] ReadBoundedFile(
        string path,
        int maximumBytes,
        string description)
    {
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        if (stream.Length is <= 0 || stream.Length > maximumBytes)
        {
            throw new InvalidOperationException(
                $"The {description} exceeds its finite bound.");
        }

        byte[] buffer = new byte[maximumBytes + 1];
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        if (total is 0 || total > maximumBytes)
        {
            throw new InvalidOperationException(
                $"The {description} exceeds its finite bound.");
        }

        return buffer[..total];
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

    private sealed record ValidatedBackup(
        BackupManifest Manifest,
        long DatabaseByteLength);

    private static readonly HashSet<string> RequiredSchemaObjects =
    [
        "index:idx_attempts_run",
        "index:idx_attempts_one_live_per_run",
        "index:idx_candidate_decisions_run_population",
        "index:idx_candidates_run_lane",
        "index:idx_case_memberships_member",
        "index:idx_coverage_run_population",
        "index:idx_dependency_edges_from",
        "index:idx_dependency_edges_to",
        "index:idx_documentation_passages_revision",
        "index:idx_documentation_imports_revision",
        "index:idx_documentation_application_bindings_run",
        "index:idx_documentation_deletion_receipts_revision",
        "index:idx_effect_receipts_run",
        "index:idx_evidence_applications_run",
        "index:idx_evidence_revisions_passage",
        "index:idx_events_run_sequence",
        "index:idx_findings_signature",
        "index:idx_gaps_run_population",
        "index:idx_hypotheses_candidate",
        "index:idx_lineage_successor",
        "index:idx_recommendations_finding",
        "index:idx_reconciliation_successor",
        "index:idx_replay_manifests_run",
        "index:idx_run_outputs_run",
        "index:idx_runs_created",
        "index:idx_runs_dispatch",
        "index:idx_snapshot_capture_dispatch",
        "index:idx_snapshot_capture_one_live_attempt",
        "index:idx_taxonomy_subject",
        "table:analysis_candidates",
        "table:analysis_coverage",
        "table:analysis_coverage_failure_links",
        "table:analysis_coverage_gap_links",
        "table:analysis_coverage_taxonomy_links",
        "table:analysis_dependency_edges",
        "table:analysis_gaps",
        "table:analysis_hypotheses",
        "table:analysis_recommendations",
        "table:analysis_replay_manifests",
        "table:analysis_run_outputs",
        "table:attempts",
        "table:audit_events",
        "table:candidate_decisions",
        "table:case_memberships",
        "table:case_hypothesis_memberships",
        "table:case_occurrence_details",
        "table:case_occurrences",
        "table:checkpoints",
        "table:coordinator_leases",
        "table:documentation_passages",
        "table:documentation_imports",
        "table:documentation_revisions",
        "table:documentation_application_bindings",
        "table:documentation_deletion_receipts",
        "table:documentation_purpose_assignment_details",
        "table:documentation_gap_details",
        "table:durable_commands",
        "table:effect_receipts",
        "table:evidence_application_links",
        "table:evidence_revisions",
        "table:finding_occurrence_details",
        "table:finding_case_abstentions",
        "table:finding_case_case_details",
        "table:finding_case_finding_details",
        "table:finding_case_gap_details",
        "table:finding_case_publications",
        "table:finding_case_recommendations",
        "table:finding_case_taxonomy_assignments",
        "table:finding_promotion_assessments",
        "table:finding_occurrences",
        "table:job_nodes",
        "table:lifecycle_events",
        "table:lineage_details",
        "table:lineage_event_edges",
        "table:lineage_events",
        "table:logical_cases",
        "table:logical_findings",
        "table:migration_history",
        "table:payload_owners",
        "table:payload_backup_pins",
        "table:payloads",
        "table:publication_receipt_payloads",
        "table:publication_receipts",
        "table:reconciliation_assessments",
        "table:reconciliation_details",
        "table:reconciliation_metadata",
        "table:reconciliation_proof_links",
        "table:run_projection",
        "table:run_operations",
        "table:runs",
        "table:store_metadata",
        "table:snapshot_capture_attempts",
        "table:snapshot_capture_operations",
        "table:snapshot_capture_publications",
        "table:taxonomy_assignments",
        "table:taxonomy_projection_edges",
        "trigger:analysis_candidates_append_only_delete",
        "trigger:analysis_candidates_append_only_update",
        "trigger:analysis_coverage_append_only_delete",
        "trigger:analysis_coverage_append_only_update",
        "trigger:analysis_coverage_failure_links_append_only_delete",
        "trigger:analysis_coverage_failure_links_append_only_update",
        "trigger:analysis_coverage_gap_links_append_only_delete",
        "trigger:analysis_coverage_gap_links_append_only_update",
        "trigger:analysis_coverage_taxonomy_links_append_only_delete",
        "trigger:analysis_coverage_taxonomy_links_append_only_update",
        "trigger:analysis_dependency_edges_append_only_delete",
        "trigger:analysis_dependency_edges_append_only_update",
        "trigger:analysis_gaps_append_only_delete",
        "trigger:analysis_gaps_append_only_update",
        "trigger:analysis_hypotheses_append_only_delete",
        "trigger:analysis_hypotheses_append_only_update",
        "trigger:analysis_recommendations_append_only_delete",
        "trigger:analysis_recommendations_append_only_update",
        "trigger:analysis_replay_manifests_append_only_delete",
        "trigger:analysis_replay_manifests_append_only_update",
        "trigger:analysis_run_outputs_append_only_delete",
        "trigger:analysis_run_outputs_append_only_update",
        "trigger:candidate_decisions_append_only_delete",
        "trigger:candidate_decisions_append_only_update",
        "trigger:case_memberships_append_only_delete",
        "trigger:case_memberships_append_only_update",
        "trigger:case_hypothesis_memberships_append_only_delete",
        "trigger:case_hypothesis_memberships_append_only_update",
        "trigger:case_occurrence_details_append_only_delete",
        "trigger:case_occurrence_details_append_only_update",
        "trigger:documentation_passages_append_only_delete",
        "trigger:documentation_passages_append_only_update",
        "trigger:documentation_imports_append_only_delete",
        "trigger:documentation_imports_append_only_update",
        "trigger:documentation_revisions_append_only_delete",
        "trigger:documentation_revisions_append_only_update",
        "trigger:documentation_application_bindings_append_only_delete",
        "trigger:documentation_application_bindings_append_only_update",
        "trigger:documentation_deletion_receipts_append_only_delete",
        "trigger:documentation_deletion_receipts_append_only_update",
        "trigger:documentation_purpose_assignment_details_append_only_delete",
        "trigger:documentation_purpose_assignment_details_append_only_update",
        "trigger:documentation_gap_details_append_only_delete",
        "trigger:documentation_gap_details_append_only_update",
        "trigger:payload_backup_pins_append_only_delete",
        "trigger:payload_backup_pins_append_only_update",
        "trigger:effect_receipts_append_only_delete",
        "trigger:effect_receipts_append_only_update",
        "trigger:evidence_application_links_append_only_delete",
        "trigger:evidence_application_links_append_only_update",
        "trigger:evidence_revisions_append_only_delete",
        "trigger:evidence_revisions_append_only_update",
        "trigger:finding_occurrence_details_append_only_delete",
        "trigger:finding_occurrence_details_append_only_update",
        "trigger:finding_case_abstentions_append_only_delete",
        "trigger:finding_case_abstentions_append_only_update",
        "trigger:finding_case_case_details_append_only_delete",
        "trigger:finding_case_case_details_append_only_update",
        "trigger:finding_case_finding_details_append_only_delete",
        "trigger:finding_case_finding_details_append_only_update",
        "trigger:finding_case_gap_details_append_only_delete",
        "trigger:finding_case_gap_details_append_only_update",
        "trigger:finding_case_publications_append_only_delete",
        "trigger:finding_case_publications_append_only_update",
        "trigger:finding_case_recommendations_append_only_delete",
        "trigger:finding_case_recommendations_append_only_update",
        "trigger:finding_case_taxonomy_assignments_append_only_delete",
        "trigger:finding_case_taxonomy_assignments_append_only_update",
        "trigger:finding_promotion_assessments_append_only_delete",
        "trigger:finding_promotion_assessments_append_only_update",
        "trigger:lineage_details_append_only_delete",
        "trigger:lineage_details_append_only_update",
        "trigger:lineage_event_edges_append_only_delete",
        "trigger:lineage_event_edges_append_only_update",
        "trigger:reconciliation_details_append_only_delete",
        "trigger:reconciliation_details_append_only_update",
        "trigger:reconciliation_metadata_append_only_delete",
        "trigger:reconciliation_metadata_append_only_update",
        "trigger:reconciliation_proof_links_append_only_delete",
        "trigger:reconciliation_proof_links_append_only_update",
        "trigger:taxonomy_assignments_append_only_delete",
        "trigger:taxonomy_assignments_append_only_update",
        "trigger:taxonomy_projection_edges_append_only_delete",
        "trigger:taxonomy_projection_edges_append_only_update",
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
        "trigger:run_operations_immutable",
        "trigger:snapshot_capture_request_immutable",
        "trigger:snapshot_capture_publications_append_only_delete",
        "trigger:snapshot_capture_publications_append_only_update",
    ];

    private static readonly string[] SchemaV4AppendOnlyTables =
    [
        "analysis_candidates",
        "analysis_coverage",
        "analysis_dependency_edges",
        "analysis_gaps",
        "analysis_hypotheses",
        "analysis_recommendations",
        "analysis_replay_manifests",
        "analysis_run_outputs",
        "candidate_decisions",
        "case_memberships",
        "case_occurrence_details",
        "documentation_passages",
        "documentation_imports",
        "documentation_revisions",
        "documentation_application_bindings",
        "documentation_deletion_receipts",
        "documentation_purpose_assignment_details",
        "documentation_gap_details",
        "payload_backup_pins",
        "effect_receipts",
        "evidence_application_links",
        "evidence_revisions",
        "finding_occurrence_details",
        "lineage_details",
        "reconciliation_details",
        "taxonomy_assignments",
    ];

    private static readonly string[] SchemaV5AppendOnlyTables =
    [
        "analysis_coverage_failure_links",
        "analysis_coverage_gap_links",
        "analysis_coverage_taxonomy_links",
        "case_hypothesis_memberships",
        "finding_case_abstentions",
        "finding_case_case_details",
        "finding_case_finding_details",
        "finding_case_gap_details",
        "finding_case_publications",
        "finding_case_recommendations",
        "finding_case_taxonomy_assignments",
        "finding_promotion_assessments",
        "lineage_event_edges",
        "reconciliation_metadata",
        "reconciliation_proof_links",
        "taxonomy_projection_edges",
    ];

    private const string SchemaV5 =
        """
        ALTER TABLE lineage_events ADD COLUMN predecessor_occurrence_id TEXT;
        ALTER TABLE lineage_events ADD COLUMN successor_occurrence_id TEXT;
        ALTER TABLE finding_occurrences ADD COLUMN analyzer_version TEXT NOT NULL DEFAULT 'legacy-unspecified';
        DROP INDEX idx_findings_signature;
        CREATE INDEX idx_findings_signature ON finding_occurrences(
            analyzer_family, analyzer_version, identity_contract_version, canonical_signature);
        ALTER TABLE analysis_coverage ADD COLUMN analyzer_id TEXT NOT NULL DEFAULT 'legacy-unspecified';
        ALTER TABLE analysis_coverage ADD COLUMN denominator_label TEXT NOT NULL DEFAULT 'legacy coverage';
        ALTER TABLE analysis_coverage ADD COLUMN exclusions_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(exclusions_json));
        ALTER TABLE analysis_coverage ADD COLUMN member_results_json TEXT NOT NULL DEFAULT '[]' CHECK(json_valid(member_results_json));
        CREATE TABLE finding_case_publications(
            finding_case_payload_id TEXT PRIMARY KEY REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            input_id TEXT NOT NULL,
            promotion_policy_id TEXT NOT NULL,
            promotion_policy_version TEXT NOT NULL,
            reconciliation_policy_id TEXT NOT NULL,
            reconciliation_policy_version TEXT NOT NULL,
            boundaries_json TEXT NOT NULL CHECK(json_valid(boundaries_json)),
            publication_claim_boundary TEXT NOT NULL,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_promotion_assessments(
            promotion_assessment_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            state_present INTEGER NOT NULL CHECK(state_present IN (0,1)),
            confidence_at_least_plausible INTEGER NOT NULL CHECK(confidence_at_least_plausible IN (0,1)),
            has_supporting_evidence INTEGER NOT NULL CHECK(has_supporting_evidence IN (0,1)),
            has_no_defeating_contradictions INTEGER NOT NULL CHECK(has_no_defeating_contradictions IN (0,1)),
            has_no_missing_information INTEGER NOT NULL CHECK(has_no_missing_information IN (0,1)),
            severity_closed INTEGER NOT NULL CHECK(severity_closed IN (0,1)),
            identity_closed INTEGER NOT NULL CHECK(identity_closed IN (0,1)),
            conclusion_available INTEGER NOT NULL CHECK(conclusion_available IN (0,1)),
            lead_eligible_state INTEGER NOT NULL CHECK(lead_eligible_state IN (0,1)),
            promotion_outcome TEXT NOT NULL CHECK(promotion_outcome IN ('supported-finding','lead-only','abstained')),
            reasons_json TEXT NOT NULL CHECK(json_valid(reasons_json)),
            assessment_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_abstentions(
            abstention_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            reason TEXT NOT NULL,
            required_information_json TEXT NOT NULL CHECK(json_valid(required_information_json)),
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            abstention_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_finding_details(
            finding_occurrence_id TEXT PRIMARY KEY REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            conclusion TEXT NOT NULL,
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            case_identity_envelope_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            taxonomy_assignment_ids_json TEXT NOT NULL CHECK(json_valid(taxonomy_assignment_ids_json)),
            semantic_fingerprint TEXT NOT NULL,
            supersedes_occurrence_id TEXT,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_recommendations(
            recommendation_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            finding_occurrence_id TEXT REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            abstention_id TEXT REFERENCES finding_case_abstentions(abstention_id) ON DELETE RESTRICT,
            lead_hypothesis_id TEXT REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            recommendation_kind TEXT NOT NULL CHECK(recommendation_kind IN (
                'remediation','alternative-remediation','validation','further-investigation','abstention')),
            action TEXT NOT NULL,
            uncertainty TEXT NOT NULL,
            reversibility TEXT NOT NULL,
            verification TEXT NOT NULL,
            risks_json TEXT NOT NULL CHECK(json_valid(risks_json)),
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            recommendation_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((finding_occurrence_id IS NOT NULL) + (abstention_id IS NOT NULL) + (lead_hypothesis_id IS NOT NULL) = 1)
        ) STRICT;
        CREATE TABLE case_hypothesis_memberships(
            case_hypothesis_membership_id TEXT PRIMARY KEY,
            case_occurrence_id TEXT NOT NULL REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            hypothesis_id TEXT NOT NULL REFERENCES analysis_hypotheses(hypothesis_id) ON DELETE RESTRICT,
            membership_role TEXT NOT NULL CHECK(membership_role IN ('cause','lead')),
            cause_proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(case_occurrence_id,hypothesis_id)
        ) STRICT;
        CREATE TABLE finding_case_case_details(
            case_occurrence_id TEXT PRIMARY KEY REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            shared_cause TEXT NOT NULL,
            cause_proof_evidence_ids_json TEXT NOT NULL CHECK(json_valid(cause_proof_evidence_ids_json)),
            semantic_fingerprint TEXT NOT NULL,
            supersedes_occurrence_id TEXT,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_taxonomy_assignments(
            taxonomy_assignment_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN ('hypothesis','finding-occurrence','case-occurrence')),
            subject_id TEXT NOT NULL,
            taxonomy_id TEXT NOT NULL,
            taxonomy_version TEXT NOT NULL,
            axis TEXT NOT NULL,
            facet TEXT NOT NULL,
            taxonomy_code TEXT,
            applicability_state TEXT NOT NULL CHECK(applicability_state IN ('assigned','unknown','unsupported','unmapped','not-applicable')),
            classification_role TEXT CHECK(classification_role IN ('declared','observed','predicted','established')),
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            applicability_condition_ids_json TEXT NOT NULL CHECK(json_valid(applicability_condition_ids_json)),
            confidence_assessment_id TEXT,
            analyzer_or_adjudicator_id TEXT NOT NULL,
            reason TEXT NOT NULL,
            supersedes_assignment_ids_json TEXT NOT NULL CHECK(json_valid(supersedes_assignment_ids_json)),
            assignment_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((applicability_state = 'assigned') = (taxonomy_code IS NOT NULL)),
            CHECK(applicability_state <> 'assigned' OR classification_role IS NOT NULL)
        ) STRICT;
        CREATE TABLE taxonomy_projection_edges(
            taxonomy_projection_id TEXT PRIMARY KEY,
            source_assignment_id TEXT NOT NULL REFERENCES finding_case_taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            projected_assignment_id TEXT NOT NULL REFERENCES finding_case_taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            mapping_authority_id TEXT NOT NULL,
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            reason TEXT NOT NULL,
            projection_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_case_gap_details(
            gap_id TEXT PRIMARY KEY REFERENCES analysis_gaps(gap_id) ON DELETE RESTRICT,
            reason TEXT NOT NULL,
            missing_capability_or_information TEXT NOT NULL,
            evidence_ids_json TEXT NOT NULL CHECK(json_valid(evidence_ids_json)),
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_coverage_taxonomy_links(
            coverage_taxonomy_link_id TEXT PRIMARY KEY,
            coverage_result_id TEXT NOT NULL REFERENCES analysis_coverage(coverage_result_id) ON DELETE RESTRICT,
            taxonomy_assignment_id TEXT NOT NULL REFERENCES finding_case_taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            link_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(coverage_result_id,taxonomy_assignment_id)
        ) STRICT;
        CREATE TABLE analysis_coverage_gap_links(
            coverage_gap_link_id TEXT PRIMARY KEY,
            coverage_result_id TEXT NOT NULL REFERENCES analysis_coverage(coverage_result_id) ON DELETE RESTRICT,
            gap_id TEXT NOT NULL REFERENCES analysis_gaps(gap_id) ON DELETE RESTRICT,
            link_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(coverage_result_id,gap_id)
        ) STRICT;
        CREATE TABLE analysis_coverage_failure_links(
            coverage_failure_link_id TEXT PRIMARY KEY,
            coverage_result_id TEXT NOT NULL REFERENCES analysis_coverage(coverage_result_id) ON DELETE RESTRICT,
            failure_id TEXT NOT NULL,
            failure_code TEXT NOT NULL,
            message TEXT NOT NULL,
            retryable INTEGER NOT NULL CHECK(retryable IN (0,1)),
            link_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(coverage_result_id,failure_id)
        ) STRICT;
        CREATE TABLE reconciliation_metadata(
            reconciliation_assessment_id TEXT PRIMARY KEY REFERENCES reconciliation_assessments(reconciliation_assessment_id) ON DELETE RESTRICT,
            actor_id TEXT NOT NULL,
            policy_id TEXT NOT NULL,
            policy_version TEXT NOT NULL,
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            visible_by_default INTEGER NOT NULL CHECK(visible_by_default IN (0,1)),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE reconciliation_proof_links(
            reconciliation_proof_link_id TEXT PRIMARY KEY,
            reconciliation_assessment_id TEXT NOT NULL REFERENCES reconciliation_assessments(reconciliation_assessment_id) ON DELETE RESTRICT,
            evidence_id TEXT NOT NULL,
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(reconciliation_assessment_id,evidence_id)
        ) STRICT;
        CREATE TABLE lineage_event_edges(
            lineage_event_edge_id TEXT PRIMARY KEY,
            lineage_event_id TEXT NOT NULL REFERENCES lineage_events(lineage_event_id) ON DELETE RESTRICT,
            edge_side TEXT NOT NULL CHECK(edge_side IN ('predecessor','successor')),
            occurrence_id TEXT NOT NULL,
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(lineage_event_id,edge_side,occurrence_id)
        ) STRICT;
        """;

    private const string SchemaV4 =
        """
        CREATE TABLE payload_backup_pins(
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            backup_identity TEXT NOT NULL,
            content_sha256 TEXT NOT NULL CHECK(
                length(content_sha256) = 64
                AND content_sha256 NOT GLOB '*[^0-9a-f]*'),
            created_at TEXT NOT NULL,
            PRIMARY KEY(payload_id, backup_identity)
        ) STRICT;
        CREATE TABLE documentation_revisions(
            documentation_revision_id TEXT PRIMARY KEY,
            source_id TEXT NOT NULL,
            source_kind TEXT NOT NULL CHECK(source_kind IN (
                'project-authored-local','fixture')),
            source_revision TEXT NOT NULL,
            supplying_snapshot_id TEXT,
            body_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            content_sha256 TEXT NOT NULL CHECK(
                length(content_sha256) = 64
                AND content_sha256 NOT GLOB '*[^0-9a-f]*'),
            byte_length INTEGER NOT NULL CHECK(byte_length >= 0),
            availability_state TEXT NOT NULL CHECK(availability_state IN (
                'present','partial','unavailable')),
            retention_state TEXT NOT NULL CHECK(retention_state IN (
                'present','partial','unavailable')),
            replay_state TEXT NOT NULL CHECK(replay_state IN (
                'complete-clean','partial','audit-only','unavailable','failed-identity-drift')),
            created_at TEXT NOT NULL,
            UNIQUE(source_id, source_revision, content_sha256),
            CHECK((availability_state = 'present') = (body_payload_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_imports(
            documentation_import_id TEXT PRIMARY KEY,
            import_run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            import_mode TEXT NOT NULL CHECK(import_mode IN (
                'clean-import','retained-reuse')),
            reused_import_id TEXT REFERENCES documentation_imports(documentation_import_id) ON DELETE RESTRICT,
            dependency_closure_id TEXT NOT NULL,
            extractor_id TEXT NOT NULL,
            llm_involvement TEXT NOT NULL CHECK(llm_involvement = 'none'),
            llm_operation TEXT NOT NULL CHECK(llm_operation = 'none'),
            boundaries_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            import_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(import_run_id, documentation_revision_id, import_mode),
            CHECK((import_mode = 'retained-reuse') = (reused_import_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_passages(
            documentation_passage_id TEXT PRIMARY KEY,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            utf8_byte_start INTEGER NOT NULL CHECK(utf8_byte_start >= 0),
            utf8_byte_end INTEGER NOT NULL CHECK(utf8_byte_end > utf8_byte_start),
            passage_sha256 TEXT NOT NULL CHECK(
                length(passage_sha256) = 64
                AND passage_sha256 NOT GLOB '*[^0-9a-f]*'),
            passage_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            availability_state TEXT NOT NULL CHECK(availability_state IN (
                'present','partial','unavailable')),
            created_at TEXT NOT NULL,
            UNIQUE(documentation_revision_id, utf8_byte_start, utf8_byte_end),
            CHECK((availability_state = 'present') = (passage_payload_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE evidence_revisions(
            evidence_revision_id TEXT PRIMARY KEY,
            documentation_passage_id TEXT
                REFERENCES documentation_passages(documentation_passage_id) ON DELETE RESTRICT,
            import_id TEXT NOT NULL
                REFERENCES documentation_imports(documentation_import_id) ON DELETE RESTRICT,
            payload_schema_id TEXT NOT NULL,
            payload_schema_version TEXT NOT NULL,
            evidence_kind TEXT NOT NULL CHECK(evidence_kind IN (
                'local-observation','deterministic-derived','documentation-claim')),
            claim_kind TEXT CHECK(claim_kind IN (
                'declared-purpose','requirement','incompatibility','installation-instruction',
                'priority-instruction','lifecycle-instruction','configuration-instruction',
                'patch-instruction','known-issue')),
            authority_kind TEXT NOT NULL CHECK(authority_kind IN (
                'snapshot-bound-local','deterministic-derived','authoritative-external',
                'corroborated-community','uncorroborated-report','user-statement',
                'test-result','heuristic-or-llm-inference')),
            applicability_state TEXT NOT NULL CHECK(applicability_state IN (
                'applicable','not-applicable','unknown','unsupported','contradicted')),
            classification_role TEXT CHECK(classification_role IN (
                'declared','observed','predicted','established')),
            evidence_state TEXT NOT NULL CHECK(evidence_state IN (
                'admitted','invalid-input','unsupported','unavailable','deleted')),
            evidence_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            contradiction_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK(
                (evidence_kind = 'documentation-claim')
                = (documentation_passage_id IS NOT NULL
                   AND claim_kind IS NOT NULL
                   AND classification_role IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_application_bindings(
            documentation_application_binding_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            installation_snapshot_id TEXT NOT NULL,
            analysis_context_id TEXT NOT NULL,
            resolved_input_manifest_id TEXT NOT NULL,
            subject_id TEXT NOT NULL,
            subject_type TEXT NOT NULL CHECK(subject_type = 'installed-entity'),
            dependency_closure_id TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, analysis_context_id, subject_type, subject_id, dependency_closure_id)
        ) STRICT;
        CREATE TABLE evidence_application_links(
            evidence_application_link_id TEXT PRIMARY KEY,
            evidence_revision_id TEXT NOT NULL
                REFERENCES evidence_revisions(evidence_revision_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            application_binding_id TEXT NOT NULL
                REFERENCES documentation_application_bindings(documentation_application_binding_id) ON DELETE RESTRICT,
            analysis_context_id TEXT NOT NULL,
            subject_id TEXT NOT NULL,
            subject_type TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            application_state TEXT NOT NULL CHECK(application_state IN (
                'applicable','not-applicable','unknown','unsupported','contradicted')),
            application_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(evidence_revision_id, run_id, analysis_context_id, subject_type, subject_id)
        ) STRICT;
        CREATE TABLE documentation_deletion_receipts(
            documentation_deletion_receipt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            deleted_body_sha256 TEXT NOT NULL CHECK(
                length(deleted_body_sha256) = 64
                AND deleted_body_sha256 NOT GLOB '*[^0-9a-f]*'),
            deleted_passage_ids_json TEXT NOT NULL CHECK(json_valid(deleted_passage_ids_json)),
            independently_retained_payload_ids_json TEXT NOT NULL
                CHECK(json_valid(independently_retained_payload_ids_json)),
            replay_effect TEXT NOT NULL CHECK(replay_effect IN ('audit-only','unavailable')),
            receipt_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            reason TEXT NOT NULL,
            deleted_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE candidate_decisions(
            candidate_decision_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            population_id TEXT NOT NULL,
            relationship_id TEXT NOT NULL,
            disposition TEXT NOT NULL CHECK(disposition IN (
                'candidate-admitted','resolved-negative','unsupported','ambiguous',
                'invalid-input','limited','deferred','unprocessed','failed')),
            lane TEXT NOT NULL CHECK(lane IN (
                'deterministic-required','mandatory-evidence','optional-ranked')),
            rule_version TEXT NOT NULL,
            decision_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_candidates(
            candidate_id TEXT PRIMARY KEY,
            candidate_decision_id TEXT NOT NULL UNIQUE
                REFERENCES candidate_decisions(candidate_decision_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            lane TEXT NOT NULL CHECK(lane IN (
                'deterministic-required','mandatory-evidence','optional-ranked')),
            candidate_state TEXT NOT NULL CHECK(candidate_state IN (
                'present','ambiguous','abstained')),
            dependency_closure_id TEXT NOT NULL,
            candidate_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_hypotheses(
            hypothesis_id TEXT PRIMARY KEY,
            candidate_id TEXT NOT NULL REFERENCES analysis_candidates(candidate_id) ON DELETE RESTRICT,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            hypothesis_state TEXT NOT NULL CHECK(hypothesis_state IN (
                'present','ambiguous','partial')),
            confidence TEXT NOT NULL CHECK(confidence IN (
                'speculative-lead','plausible','strongly-supported','confirmed')),
            threshold_id TEXT NOT NULL,
            hypothesis_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE analysis_recommendations(
            recommendation_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            finding_occurrence_id TEXT
                REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            abstention_id TEXT,
            recommendation_kind TEXT NOT NULL CHECK(recommendation_kind IN (
                'remediation','alternative-remediation','validation',
                'further-investigation','abstention')),
            recommendation_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((finding_occurrence_id IS NOT NULL) <> (abstention_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE taxonomy_assignments(
            taxonomy_assignment_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            subject_kind TEXT NOT NULL CHECK(subject_kind IN (
                'documentation-revision','evidence-revision','installed-entity','candidate','hypothesis',
                'finding-occurrence','case-occurrence')),
            subject_id TEXT NOT NULL,
            taxonomy_id TEXT NOT NULL,
            taxonomy_version TEXT NOT NULL,
            axis TEXT NOT NULL,
            facet TEXT NOT NULL,
            taxonomy_code TEXT,
            applicability_state TEXT NOT NULL CHECK(applicability_state IN (
                'assigned','unknown','unsupported','unmapped','not-applicable')),
            classification_role TEXT NOT NULL CHECK(classification_role IN (
                'declared','observed','predicted','established')),
            assignment_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((applicability_state = 'assigned') = (taxonomy_code IS NOT NULL))
        ) STRICT;
        CREATE TABLE documentation_purpose_assignment_details(
            taxonomy_assignment_id TEXT PRIMARY KEY
                REFERENCES taxonomy_assignments(taxonomy_assignment_id) ON DELETE RESTRICT,
            evidence_revision_id TEXT NOT NULL
                REFERENCES evidence_revisions(evidence_revision_id) ON DELETE RESTRICT,
            evidence_application_link_id TEXT NOT NULL
                REFERENCES evidence_application_links(evidence_application_link_id) ON DELETE RESTRICT,
            analyzer_or_adjudicator_id TEXT NOT NULL,
            applicability_condition_ids_json TEXT NOT NULL
                CHECK(json_valid(applicability_condition_ids_json)),
            reason TEXT NOT NULL,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE analysis_coverage(
            coverage_result_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            population_id TEXT NOT NULL,
            coverage_state TEXT NOT NULL CHECK(coverage_state IN (
                'completed','completed-with-gaps','failed','skipped-by-configuration',
                'skipped-by-limit','unsupported')),
            denominator INTEGER NOT NULL CHECK(denominator >= 0),
            completed INTEGER NOT NULL CHECK(completed >= 0 AND completed <= denominator),
            excluded INTEGER NOT NULL CHECK(excluded >= 0),
            taxonomy_id TEXT NOT NULL,
            taxonomy_version TEXT NOT NULL,
            coverage_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, population_id)
        ) STRICT;
        CREATE TABLE analysis_gaps(
            gap_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            population_id TEXT NOT NULL,
            stage_id TEXT NOT NULL,
            gap_state TEXT NOT NULL CHECK(gap_state IN (
                'missing-information','missing-capability','missing-dependency','unsupported',
                'failed','limited','unavailable','deleted','audit-gap')),
            replay_effect TEXT NOT NULL CHECK(replay_effect IN (
                'none','partial','audit-only','unavailable')),
            conclusion_effect TEXT NOT NULL CHECK(conclusion_effect IN (
                'none','bounded','abstain','unavailable')),
            gap_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE documentation_gap_details(
            gap_id TEXT PRIMARY KEY REFERENCES analysis_gaps(gap_id) ON DELETE RESTRICT,
            documentation_revision_id TEXT NOT NULL
                REFERENCES documentation_revisions(documentation_revision_id) ON DELETE RESTRICT,
            evidence_revision_id TEXT REFERENCES evidence_revisions(evidence_revision_id) ON DELETE RESTRICT,
            evidence_application_link_id TEXT
                REFERENCES evidence_application_links(evidence_application_link_id) ON DELETE RESTRICT,
            gap_kind TEXT NOT NULL CHECK(gap_kind IN (
                'contradiction','deletion','unavailable-source','replay')),
            reason TEXT NOT NULL,
            detail_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT
        ) STRICT;
        CREATE TABLE analysis_dependency_edges(
            dependency_edge_id TEXT NOT NULL,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            from_kind TEXT NOT NULL CHECK(from_kind IN (
                'documentation-import','documentation-revision','passage','evidence-revision',
                'claim-application','taxonomy-assignment','documentation-gap','installed-entity','candidate-analysis-root','candidate-decision','dependency-closure',
                'candidate','hypothesis','abstention','failure','finding-occurrence','recommendation','case-occurrence',
                'coverage','gap','replay-manifest','run-output')),
            from_id TEXT NOT NULL,
            to_kind TEXT NOT NULL CHECK(to_kind IN (
                'snapshot','analysis-context','scan-configuration','resolved-input-manifest',
                'documentation-import','documentation-revision','passage','evidence-revision','claim-application',
                'installed-entity','documentation-evidence','evidence','dependency-closure','dependency','candidate-decision',
                'candidate','hypothesis','finding-occurrence','recommendation','case-occurrence',
                'coverage','gap','source-fact','payload','execution-input-binding','policy-binding','threshold-binding','limit-binding','analyzer-declaration-binding')),
            to_id TEXT NOT NULL,
            edge_kind TEXT NOT NULL CHECK(edge_kind IN (
                'derived-from','supports','supported-by','contradicts','applies','applies-to',
                'depends-on','consumes','conditioned-by','classifies','limits','reuses',
                'member-of','supersedes','produced-by','uses')),
            edge_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            PRIMARY KEY(dependency_edge_id, run_id),
            UNIQUE(run_id, from_kind, from_id, to_kind, to_id, edge_kind)
        ) STRICT;
        CREATE TABLE case_memberships(
            case_membership_id TEXT PRIMARY KEY,
            case_occurrence_id TEXT NOT NULL
                REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            member_kind TEXT NOT NULL CHECK(member_kind IN (
                'finding-occurrence','candidate')),
            member_id TEXT NOT NULL,
            membership_role TEXT NOT NULL CHECK(membership_role IN (
                'cause','effect','support','contradiction','lead')),
            cause_proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(case_occurrence_id, member_kind, member_id)
        ) STRICT;
        CREATE TABLE analysis_replay_manifests(
            replay_manifest_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            replay_mode TEXT NOT NULL CHECK(replay_mode IN (
                'clean','incremental','retained-downstream-replay')),
            replay_state TEXT NOT NULL CHECK(replay_state IN (
                'complete-clean','partial','audit-only','unavailable','failed-identity-drift')),
            auditability_state TEXT NOT NULL CHECK(auditability_state IN (
                'complete','partial','unavailable')),
            semantic_equivalence INTEGER NOT NULL CHECK(semantic_equivalence IN (0,1)),
            compared_run_id TEXT REFERENCES runs(run_id) ON DELETE RESTRICT,
            manifest_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            manifest_sha256 TEXT NOT NULL CHECK(
                length(manifest_sha256) = 64
                AND manifest_sha256 NOT GLOB '*[^0-9a-f]*'),
            created_at TEXT NOT NULL,
            UNIQUE(run_id, replay_manifest_id)
        ) STRICT;
        CREATE TABLE analysis_run_outputs(
            run_output_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            payload_schema_id TEXT NOT NULL,
            payload_schema_version TEXT NOT NULL,
            revision INTEGER NOT NULL CHECK(revision > 0),
            output_state TEXT NOT NULL CHECK(output_state IN (
                'present','partial','unavailable','failed')),
            output_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            output_sha256 TEXT NOT NULL CHECK(
                length(output_sha256) = 64
                AND output_sha256 NOT GLOB '*[^0-9a-f]*'),
            byte_length INTEGER NOT NULL CHECK(byte_length >= 0),
            provenance_id TEXT NOT NULL,
            dependency_closure_id TEXT NOT NULL,
            replay_manifest_id TEXT
                REFERENCES analysis_replay_manifests(replay_manifest_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            UNIQUE(run_id, revision),
            CHECK((output_state = 'present') = (output_payload_id IS NOT NULL))
        ) STRICT;
        CREATE TABLE effect_receipts(
            effect_receipt_id TEXT PRIMARY KEY,
            run_id TEXT NOT NULL REFERENCES runs(run_id) ON DELETE RESTRICT,
            effect_class TEXT NOT NULL CHECK(effect_class IN (
                'database','payload-store','staging','trace','run-output')),
            effect_state TEXT NOT NULL CHECK(effect_state IN (
                'admitted','reconciled','missing','orphaned','invalid','not-used')),
            object_id TEXT NOT NULL,
            receipt_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE finding_occurrence_details(
            finding_occurrence_id TEXT PRIMARY KEY
                REFERENCES finding_occurrences(finding_occurrence_id) ON DELETE RESTRICT,
            candidate_id TEXT NOT NULL REFERENCES analysis_candidates(candidate_id) ON DELETE RESTRICT,
            confidence TEXT NOT NULL CHECK(confidence IN (
                'confirmed','strongly-supported','plausible')),
            severity TEXT NOT NULL CHECK(severity IN (
                'advisory','minor','moderate','major','blocker')),
            finding_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE case_occurrence_details(
            case_occurrence_id TEXT PRIMARY KEY
                REFERENCES case_occurrences(case_occurrence_id) ON DELETE RESTRICT,
            case_kind TEXT NOT NULL CHECK(case_kind IN ('supported','lead-only')),
            affects_readiness INTEGER NOT NULL CHECK(affects_readiness IN (0,1)),
            case_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            CHECK((case_kind = 'supported') OR affects_readiness = 0)
        ) STRICT;
        CREATE TABLE reconciliation_details(
            reconciliation_assessment_id TEXT PRIMARY KEY
                REFERENCES reconciliation_assessments(reconciliation_assessment_id) ON DELETE RESTRICT,
            mechanism TEXT NOT NULL CHECK(mechanism IN ('automatic','reviewed','not-evaluated')),
            causal_gate TEXT NOT NULL CHECK(causal_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            applicability_gate TEXT NOT NULL CHECK(applicability_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            dependency_gate TEXT NOT NULL CHECK(dependency_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            producer_gate TEXT NOT NULL CHECK(producer_gate IN (
                'proven-equivalent','proven-different','ambiguous','unknown','not-evaluated')),
            gap_payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            considered_occurrences_payload_id TEXT NOT NULL
                REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TABLE lineage_details(
            lineage_event_id TEXT PRIMARY KEY
                REFERENCES lineage_events(lineage_event_id) ON DELETE RESTRICT,
            lineage_kind TEXT NOT NULL CHECK(lineage_kind IN (
                'supersedes','analytical-revision','related-follow-up','promotes-lead',
                'merge-successor','split-successor','correction-successor')),
            proof_payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE INDEX idx_documentation_passages_revision
            ON documentation_passages(documentation_revision_id, utf8_byte_start);
        CREATE INDEX idx_documentation_imports_revision
            ON documentation_imports(documentation_revision_id, created_at);
        CREATE INDEX idx_documentation_application_bindings_run
            ON documentation_application_bindings(run_id, subject_type, subject_id);
        CREATE INDEX idx_documentation_deletion_receipts_revision
            ON documentation_deletion_receipts(documentation_revision_id, deleted_at);
        CREATE INDEX idx_evidence_revisions_passage
            ON evidence_revisions(documentation_passage_id, evidence_revision_id);
        CREATE INDEX idx_evidence_applications_run
            ON evidence_application_links(run_id, evidence_revision_id);
        CREATE INDEX idx_candidate_decisions_run_population
            ON candidate_decisions(run_id, population_id, disposition);
        CREATE INDEX idx_candidates_run_lane ON analysis_candidates(run_id, lane, candidate_id);
        CREATE INDEX idx_hypotheses_candidate ON analysis_hypotheses(candidate_id, hypothesis_id);
        CREATE INDEX idx_recommendations_finding
            ON analysis_recommendations(finding_occurrence_id, recommendation_id);
        CREATE INDEX idx_taxonomy_subject
            ON taxonomy_assignments(subject_kind, subject_id, taxonomy_version, axis);
        CREATE INDEX idx_coverage_run_population
            ON analysis_coverage(run_id, population_id, coverage_state);
        CREATE INDEX idx_gaps_run_population ON analysis_gaps(run_id, population_id, gap_state);
        CREATE INDEX idx_dependency_edges_from
            ON analysis_dependency_edges(from_kind, from_id, edge_kind);
        CREATE INDEX idx_dependency_edges_to
            ON analysis_dependency_edges(to_kind, to_id, edge_kind);
        CREATE INDEX idx_case_memberships_member
            ON case_memberships(member_kind, member_id, case_occurrence_id);
        CREATE INDEX idx_replay_manifests_run
            ON analysis_replay_manifests(run_id, replay_state);
        CREATE INDEX idx_run_outputs_run ON analysis_run_outputs(run_id, revision);
        CREATE INDEX idx_effect_receipts_run ON effect_receipts(run_id, effect_class, effect_state);
        """;

    private const string SchemaV3 =
        """
        CREATE TABLE run_operations(
            run_id TEXT PRIMARY KEY REFERENCES runs(run_id) ON DELETE RESTRICT,
            operation_kind TEXT NOT NULL,
            request_json TEXT NOT NULL,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256) = 64),
            created_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER run_operations_immutable
        BEFORE UPDATE ON run_operations
        BEGIN SELECT RAISE(ABORT, 'run operations are immutable'); END;
        """;

    private const string SchemaV2 =
        """
        CREATE TABLE snapshot_capture_operations(
            operation_id TEXT PRIMARY KEY,
            durable_command_id TEXT NOT NULL UNIQUE,
            request_json TEXT NOT NULL,
            request_sha256 TEXT NOT NULL CHECK(length(request_sha256) = 64),
            initiation_kind TEXT NOT NULL,
            dispatch_deadline TEXT NOT NULL,
            lifecycle_state TEXT NOT NULL
                CHECK(lifecycle_state IN ('Queued','Running','Completed','Failed')),
            lifecycle_generation INTEGER NOT NULL CHECK(lifecycle_generation >= 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            installation_snapshot_id TEXT,
            payload_id TEXT REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            created_at TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            CHECK (
                (lifecycle_state = 'Completed'
                 AND installation_snapshot_id IS NOT NULL
                 AND payload_id IS NOT NULL)
                OR
                (lifecycle_state <> 'Completed'
                 AND installation_snapshot_id IS NULL
                 AND payload_id IS NULL)
            )
        ) STRICT;
        CREATE TRIGGER snapshot_capture_request_immutable
        BEFORE UPDATE OF durable_command_id, request_json, request_sha256,
                         initiation_kind, dispatch_deadline
        ON snapshot_capture_operations
        BEGIN SELECT RAISE(ABORT, 'snapshot capture requests are immutable'); END;
        CREATE INDEX idx_snapshot_capture_dispatch
            ON snapshot_capture_operations(lifecycle_state, created_at, operation_id);
        CREATE TABLE snapshot_capture_attempts(
            attempt_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL
                REFERENCES snapshot_capture_operations(operation_id) ON DELETE RESTRICT,
            attempt_generation INTEGER NOT NULL CHECK(attempt_generation > 0),
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            lease_acquired_at TEXT NOT NULL,
            lease_expires_at TEXT NOT NULL,
            outcome TEXT NOT NULL,
            created_at TEXT NOT NULL,
            UNIQUE(operation_id, attempt_generation),
            UNIQUE(operation_id, attempt_fencing_token),
            CHECK(lease_expires_at > lease_acquired_at)
        ) STRICT;
        CREATE UNIQUE INDEX idx_snapshot_capture_one_live_attempt
            ON snapshot_capture_attempts(operation_id)
            WHERE outcome = 'running';
        CREATE TABLE snapshot_capture_publications(
            receipt_id TEXT PRIMARY KEY,
            operation_id TEXT NOT NULL UNIQUE
                REFERENCES snapshot_capture_operations(operation_id) ON DELETE RESTRICT,
            attempt_id TEXT NOT NULL UNIQUE
                REFERENCES snapshot_capture_attempts(attempt_id) ON DELETE RESTRICT,
            coordinator_fencing_epoch INTEGER NOT NULL CHECK(coordinator_fencing_epoch > 0),
            attempt_fencing_token INTEGER NOT NULL CHECK(attempt_fencing_token > 0),
            staged_manifest_sha256 TEXT NOT NULL CHECK(length(staged_manifest_sha256) = 64),
            payload_id TEXT NOT NULL REFERENCES payloads(payload_id) ON DELETE RESTRICT,
            installation_snapshot_id TEXT NOT NULL UNIQUE,
            published_at TEXT NOT NULL
        ) STRICT;
        CREATE TRIGGER snapshot_capture_publications_append_only_update
        BEFORE UPDATE ON snapshot_capture_publications
        BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        CREATE TRIGGER snapshot_capture_publications_append_only_delete
        BEFORE DELETE ON snapshot_capture_publications
        BEGIN SELECT RAISE(ABORT, 'append-only history'); END;
        """;

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
