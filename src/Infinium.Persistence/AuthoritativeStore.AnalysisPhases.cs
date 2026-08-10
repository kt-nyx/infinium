using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed record AnalysisPhaseCheckpointRecord(
    string CheckpointId,
    string RunId,
    string PhaseId,
    string InputFingerprint,
    string PayloadId,
    string SchemaId,
    string SchemaVersion,
    string PayloadSha256,
    long PayloadByteLength,
    string Disposition,
    string SourceRunId,
    DateTimeOffset CreatedAt);

public sealed partial class AuthoritativeStore
{
    public string RetainAnalysisPhaseInput(
        AttemptRecord attempt,
        string inputKind,
        string inputId,
        ReadOnlySpan<byte> bytes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        if (inputKind.Length is < 1 or > 64 || inputId.Length is < 1 or > 256
            || bytes.Length is < 1 or > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("The analysis phase input exceeds its closed bounds.");
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentAttempt(attempt, transaction);
            string payloadId = AdmitCoordinatorPayload(bytes, inputKind, inputId, now, transaction);
            transaction.Commit();
            return payloadId;
        }
    }

    public AnalysisPhaseCheckpointRecord RecordAnalysisPhaseCheckpoint(
        AttemptRecord attempt,
        RunBinding binding,
        string phaseId,
        string inputFingerprint,
        string payloadId,
        string schemaId,
        string schemaVersion,
        string payloadSha256,
        long payloadByteLength,
        string disposition,
        string sourceRunId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ValidateBinding(binding);
        ValidateSha256(inputFingerprint);
        ValidateSha256(payloadSha256);
        if (string.IsNullOrWhiteSpace(phaseId) || phaseId.Length > 128
            || payloadByteLength < 1 || disposition.Length is < 1 or > 64
            || sourceRunId.Length is < 1 or > 128)
        {
            throw new InvalidDataException("The analysis phase checkpoint is malformed.");
        }
        string checkpointId = "analysis-phase-" + Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                string.Join('\n', attempt.RunId, phaseId, inputFingerprint, payloadSha256))));
        string completed = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["phase_id"] = phaseId,
            ["input_fingerprint"] = inputFingerprint,
            ["payload_id"] = payloadId,
            ["schema_id"] = schemaId,
            ["schema_version"] = schemaVersion,
            ["payload_sha256"] = payloadSha256,
            ["payload_byte_length"] = payloadByteLength,
            ["disposition"] = disposition,
            ["source_run_id"] = sourceRunId,
        });
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentAttempt(attempt, transaction);
            Execute(
                """
                INSERT OR IGNORE INTO checkpoints(
                    checkpoint_id,run_id,attempt_id,installation_snapshot_id,analysis_context_id,
                    effective_scan_configuration_id,resolved_input_manifest_id,dependency_closure_id,
                    content_sha256,completed_partitions_json,pending_and_gaps_json,created_at)
                VALUES ($checkpoint,$run,$attempt,$snapshot,$context,$configuration,$manifest,
                    $dependency,$sha,$completed,'{}',$now);
                """, transaction,
                ("$checkpoint", checkpointId), ("$run", attempt.RunId), ("$attempt", attempt.AttemptId),
                ("$snapshot", binding.InstallationSnapshotId), ("$context", binding.AnalysisContextId),
                ("$configuration", binding.EffectiveScanConfigurationId), ("$manifest", binding.ResolvedInputManifestId),
                ("$dependency", "analysis-phase-" + phaseId + "-" + inputFingerprint),
                ("$sha", payloadSha256), ("$completed", completed), ("$now", ToText(now)));
            transaction.Commit();
        }
        return new(checkpointId, attempt.RunId, phaseId, inputFingerprint, payloadId, schemaId,
            schemaVersion, payloadSha256, payloadByteLength, disposition, sourceRunId, now);
    }

    public AnalysisPhaseCheckpointRecord? ReadAnalysisPhaseCheckpoint(
        string runId,
        string phaseId,
        string inputFingerprint)
    {
        ValidateSha256(inputFingerprint);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT checkpoint_id,completed_partitions_json,created_at
                FROM checkpoints
                WHERE run_id=$run AND dependency_closure_id=$dependency
                ORDER BY created_at DESC,checkpoint_id DESC LIMIT 1;
                """;
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$dependency", "analysis-phase-" + phaseId + "-" + inputFingerprint);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }
            string checkpointId = reader.GetString(0);
            using JsonDocument document = JsonDocument.Parse(reader.GetString(1));
            JsonElement root = document.RootElement;
            return new(checkpointId, runId, root.GetProperty("phase_id").GetString()!,
                root.GetProperty("input_fingerprint").GetString()!, root.GetProperty("payload_id").GetString()!,
                root.GetProperty("schema_id").GetString()!, root.GetProperty("schema_version").GetString()!,
                root.GetProperty("payload_sha256").GetString()!, root.GetProperty("payload_byte_length").GetInt64(),
                root.GetProperty("disposition").GetString()!, root.GetProperty("source_run_id").GetString()!,
                DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public AnalysisPhaseCheckpointRecord? ReadLatestAnalysisPhaseCheckpoint(string runId, string phaseId)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT checkpoint_id,completed_partitions_json,created_at
                FROM checkpoints
                WHERE run_id=$run AND dependency_closure_id LIKE $prefix
                ORDER BY created_at DESC,checkpoint_id DESC LIMIT 1;
                """;
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$prefix", "analysis-phase-" + phaseId + "-%");
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }
            string checkpointId = reader.GetString(0);
            using JsonDocument document = JsonDocument.Parse(reader.GetString(1));
            JsonElement root = document.RootElement;
            return new(checkpointId, runId, root.GetProperty("phase_id").GetString()!,
                root.GetProperty("input_fingerprint").GetString()!, root.GetProperty("payload_id").GetString()!,
                root.GetProperty("schema_id").GetString()!, root.GetProperty("schema_version").GetString()!,
                root.GetProperty("payload_sha256").GetString()!, root.GetProperty("payload_byte_length").GetInt64(),
                root.GetProperty("disposition").GetString()!, root.GetProperty("source_run_id").GetString()!,
                DateTimeOffset.Parse(reader.GetString(2), System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
