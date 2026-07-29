using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

#pragma warning disable IDE0008 // SQL transaction code is clearer with local type inference.
#pragma warning disable CA1512 // Guard clauses use parameter-specific messages.
#pragma warning disable CA1869 // The backup serializer is not a hot path.

namespace Infinium.Persistence;

public sealed class AuthoritativeStore : IDisposable
{
    public const int CurrentSchemaVersion = 1;

    private readonly Lock gate = new();
    private readonly SqliteConnection connection;
    private bool disposed;

    public AuthoritativeStore(StoragePaths paths)
    {
        Paths = paths ?? throw new ArgumentNullException(nameof(paths));
        Paths.Create();
        SqliteRuntimeIdentity.InitializeNativeProvider();
        connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Paths.Database,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        }.ToString());
        try
        {
            connection.Open();
            BindingIdentity = SqliteRuntimeIdentity.VerifyExactPatchedBinding(connection);
            ConfigureConnection(connection);
            ApplyMigrations();
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public StoragePaths Paths { get; }
    public SqliteBindingIdentity BindingIdentity { get; }

    public CoordinatorAuthority AcquireCoordinatorAuthority(
        string instanceId,
        DateTimeOffset now,
        TimeSpan leaseDuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        lock (gate)
        {
            using var transaction = connection.BeginTransaction();
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

    public RunRecord CreateRun(
        string durableCommandId,
        string runId,
        RunBinding binding,
        long coordinatorFencingEpoch,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durableCommandId);
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ValidateBinding(binding);
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));

        lock (gate)
        {
            using var transaction = connection.BeginTransaction();
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
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "A durable command key cannot be rebound to different start inputs.");
                }

                transaction.Commit();
                return existingRun;
            }

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
                    resulting_state, created_at)
                VALUES ($id, 'start', $run, 0, 'accepted', 'Queued', $now);
                """,
                transaction,
                ("$id", durableCommandId),
                ("$run", runId),
                ("$now", ToText(now)));
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

    public IReadOnlyList<RunRecord> ListNonTerminalRuns()
    {
        lock (gate)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT run_id, installation_snapshot_id, analysis_context_id,
                       effective_scan_configuration_id, resolved_input_manifest_id,
                       lifecycle_state, lifecycle_generation, coordinator_fencing_epoch,
                       durable_sequence, created_at, updated_at
                FROM runs
                WHERE lifecycle_state IN (
                    'Queued','Running','Waiting','Retrying','Pausing','Paused','Cancelling')
                ORDER BY created_at, run_id;
                """;
            using var reader = command.ExecuteReader();
            var result = new List<RunRecord>();
            while (reader.Read())
            {
                result.Add(ReadRun(reader));
            }

            return result;
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
                SELECT command_id, disposition, run_id, resulting_state, transition_id, created_at
                FROM durable_commands
                WHERE command_id = $id;
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
            using var transaction = connection.BeginTransaction();
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
            using var transaction = connection.BeginTransaction();
            var run = GetRunCore(runId);
            if (run.CoordinatorFencingEpoch > coordinatorFencingEpoch)
            {
                throw new InvalidOperationException("The coordinator fencing epoch is stale.");
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

    public void SettleLiveAttempts(string runId, string outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);
        if (outcome.Length > 64)
        {
            throw new ArgumentException("The attempt outcome exceeds its bound.", nameof(outcome));
        }

        lock (gate)
        {
            Execute(
                """
                UPDATE attempts
                SET outcome = $outcome
                WHERE run_id = $run AND outcome = 'running';
                """,
                transaction: null,
                ("$outcome", outcome),
                ("$run", runId));
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
        ValidateBinding(binding);
        ValidateSha256(contentSha256);
        lock (gate)
        {
            using var transaction = connection.BeginTransaction();
            EnsureCurrentAttempt(attempt, transaction);
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
            transaction.Commit();
        }
    }

    public PayloadAdmission AdmitStagedPayload(
        AttemptRecord attempt,
        string stagedRelativePath,
        string expectedSha256,
        long maximumBytes,
        DateTimeOffset now)
    {
        ValidateSha256(expectedSha256);
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        var stagingPath = Paths.ResolveProductRelative(
            Path.Combine("staging", attempt.AttemptId, stagedRelativePath));
        if (!File.Exists(stagingPath))
        {
            throw new FileNotFoundException("The declared staged output does not exist.", stagingPath);
        }

        var fileInfo = new FileInfo(stagingPath);
        if (fileInfo.Length > maximumBytes)
        {
            throw new InvalidOperationException("The staged output exceeds its declared bound.");
        }

        var actualSha = HashFile(stagingPath);
        if (!string.Equals(actualSha, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The staged output fingerprint does not match its manifest.");
        }

        var payloadId = Guid.NewGuid().ToString("N");
        var relativeObjectPath = Path.Combine(
            "payloads",
            actualSha[..2],
            actualSha[2..4],
            actualSha);
        var objectPath = Paths.ResolveProductRelative(relativeObjectPath);
        Directory.CreateDirectory(Path.GetDirectoryName(objectPath)!);

        lock (gate)
        {
            using var transaction = connection.BeginTransaction();
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
                ("$sha", actualSha),
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
            transaction.Commit();
            return new PayloadAdmission(
                admittedPayloadId,
                actualSha,
                fileInfo.Length,
                relativeObjectPath.Replace('\\', '/'),
                receiptId);
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
                var fullPath = Paths.ResolveProductRelative(entry.Key);
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
            using var transaction = connection.BeginTransaction();
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
            var databasePath = Path.Combine(Paths.Backups, $"{stamp}-{safeLabel}.sqlite3");
            using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            }.ToString()))
            {
                destination.Open();
                connection.BackupDatabase(destination);
            }

            var databaseSha = HashFile(databasePath);
            var payloads = new List<object>();
            var payloadBackupRoot = databasePath + ".payloads";
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT content_sha256, byte_length, object_relative_path FROM payloads ORDER BY content_sha256;";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var relativePath = reader.GetString(2);
                    var sourcePath = Paths.ResolveProductRelative(
                        relativePath.Replace('/', Path.DirectorySeparatorChar));
                    var backupPayloadPath = Path.Combine(
                        payloadBackupRoot,
                        relativePath["payloads/".Length..]
                            .Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPayloadPath)!);
                    File.Copy(sourcePath, backupPayloadPath, overwrite: false);
                    payloads.Add(new
                    {
                        sha256 = reader.GetString(0),
                        byteLength = reader.GetInt64(1),
                        relativePath,
                    });
                }
            }

            var manifestPath = databasePath + ".manifest.json";
            var manifest = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = CurrentSchemaVersion,
                sqlite = BindingIdentity,
                databaseSha256 = databaseSha,
                payloads,
                createdAt = now,
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllBytes(manifestPath, manifest);
            return new BackupArtifact(databasePath, manifestPath, databaseSha);
        }
    }

    public static void RestoreBackup(BackupArtifact backup, StoragePaths target)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(target);
        if (File.Exists(target.Database))
        {
            throw new InvalidOperationException("Restore requires a fresh target store.");
        }

        if (!string.Equals(HashFile(backup.DatabasePath), backup.Sha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The backup database fingerprint is invalid.");
        }

        target.Create();
        File.Copy(backup.DatabasePath, target.Database, overwrite: false);
        using (var manifest = JsonDocument.Parse(File.ReadAllBytes(backup.ManifestPath)))
        {
            foreach (var payload in manifest.RootElement.GetProperty("payloads").EnumerateArray())
            {
                var relative = payload.GetProperty("relativePath").GetString()
                    ?? throw new InvalidOperationException("A backup payload path is missing.");
                var expectedSha = payload.GetProperty("sha256").GetString()
                    ?? throw new InvalidOperationException("A backup payload digest is missing.");
                var source = Path.Combine(
                    backup.DatabasePath + ".payloads",
                    relative["payloads/".Length..].Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(source)
                    || !string.Equals(HashFile(source), expectedSha, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("A backup payload is missing or corrupt.");
                }

                var destination = target.ResolveProductRelative(
                    relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, overwrite: false);
            }
        }

        SqliteRuntimeIdentity.InitializeNativeProvider();
        using var restored = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = target.Database,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        }.ToString());
        restored.Open();
        SqliteRuntimeIdentity.VerifyExactPatchedBinding(restored);
        ConfigureConnection(restored);
        using var command = restored.CreateCommand();
        command.CommandText =
            """
            SELECT CASE WHEN (SELECT integrity_check FROM pragma_integrity_check LIMIT 1) = 'ok'
                        AND NOT EXISTS (SELECT 1 FROM pragma_foreign_key_check)
                        AND (SELECT CAST(value AS INTEGER) FROM store_metadata WHERE key='schema_version') = $version
                   THEN 1 ELSE 0 END;
            """;
        command.Parameters.AddWithValue("$version", CurrentSchemaVersion);
        if (Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException("The restored database failed integrity or schema validation.");
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
            disposed = true;
        }
    }

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
                using var transaction = connection.BeginTransaction();
                Execute(SchemaV1, transaction);
                Execute(
                    """
                    INSERT INTO store_metadata(key, value) VALUES ('schema_version', '1');
                    INSERT INTO store_metadata(key, value) VALUES ('storage_contract_version', '1.0.0');
                    INSERT INTO store_metadata(key, value) VALUES ('sqlite_version', $sqlite_version);
                    INSERT INTO store_metadata(key, value) VALUES ('sqlite_source_id', $sqlite_source);
                    INSERT INTO migration_history(
                        migration_id, from_version, to_version, applied_at, sqlite_source_id)
                    VALUES ('M1-S2-0001', 0, 1, $now, $sqlite_source);
                    PRAGMA user_version = 1;
                    """,
                    transaction,
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
              AND a.lease_expires_at >= $now
              AND r.coordinator_fencing_epoch <= $epoch
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

    private static void ConfigureConnection(SqliteConnection target)
    {
        using var command = target.CreateCommand();
        command.CommandText =
            """
            PRAGMA foreign_keys = ON;
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = FULL;
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

    private static void ValidateBinding(RunBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.InstallationSnapshotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.AnalysisContextId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.EffectiveScanConfigurationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.ResolvedInputManifestId);
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
            created_at TEXT NOT NULL
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
        CREATE INDEX idx_runs_created ON runs(created_at, run_id);
        CREATE INDEX idx_events_run_sequence ON lifecycle_events(run_id, durable_sequence);
        CREATE INDEX idx_attempts_run ON attempts(run_id, attempt_generation);
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
