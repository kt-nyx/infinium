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

    public void FailQueuedSnapshotCapture(
        string operationId,
        long expectedGeneration,
        long coordinatorFencingEpoch,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            int changed = Execute(
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
            if (changed != 1)
            {
                throw new InvalidOperationException(
                    "The queued snapshot capture failure compare-and-swap lost its race.");
            }

            InsertAuditEvent(
                "snapshot-capture-queued-request-failed",
                "snapshot-capture-operation",
                operationId,
                now,
                transaction);
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

}

#pragma warning restore CA1869
#pragma warning restore CA1512
#pragma warning restore IDE0008
