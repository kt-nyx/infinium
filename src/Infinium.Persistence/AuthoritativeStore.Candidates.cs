using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed partial class AuthoritativeStore
{
    internal CandidatePhasePersistenceReceipt PublishCandidatePhase(
        CandidateAnalysisContract analysis,
        ReadOnlyMemory<byte> serializedAnalysis,
        CandidatePhaseCheckpointPublication checkpoint,
        AttemptRecord attempt,
        RunBinding binding,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        CandidateAnalysisContractInvariants.Validate(analysis);
        ValidateBinding(binding);
        ValidateSha256(checkpoint.ContentSha256);
        ValidateBoundedJson(checkpoint.PendingAndGapsJson, nameof(checkpoint.PendingAndGapsJson));
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentAttempt(attempt, transaction);
            if (ScalarLong(
                """
                SELECT COUNT(*) FROM runs
                WHERE run_id = $run AND installation_snapshot_id = $snapshot
                  AND analysis_context_id = $context AND effective_scan_configuration_id = $config
                  AND resolved_input_manifest_id = $manifest;
                """, transaction,
                ("$run", attempt.RunId), ("$snapshot", binding.InstallationSnapshotId),
                ("$context", binding.AnalysisContextId), ("$config", binding.EffectiveScanConfigurationId),
                ("$manifest", binding.ResolvedInputManifestId)) != 1)
            {
                throw new InvalidOperationException("Candidate phase dependencies differ from the immutable run binding.");
            }
            CandidateAnalysisPersistenceReceipt receipt = PublishCandidateAnalysisCore(
                analysis, serializedAnalysis, now, transaction);
            string checkpointPayloadId = AdmitCoordinatorPayload(
                checkpoint.PayloadBytes, "candidate-checkpoint", checkpoint.CheckpointId, now, transaction);
            string completedPartitionsJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["payload_id"] = checkpointPayloadId,
                ["sha256"] = checkpoint.ContentSha256,
                ["byte_length"] = checkpoint.PayloadBytes.LongLength,
            });
            ValidateBoundedJson(completedPartitionsJson, nameof(completedPartitionsJson));
            Execute(
                """
                INSERT OR IGNORE INTO checkpoints(
                    checkpoint_id, run_id, attempt_id, installation_snapshot_id,
                    analysis_context_id, effective_scan_configuration_id, resolved_input_manifest_id,
                    dependency_closure_id, content_sha256, completed_partitions_json,
                    pending_and_gaps_json, created_at)
                VALUES ($checkpoint,$run,$attempt,$snapshot,$context,$config,$manifest,$dependency,$sha,$completed,$pending,$now);
                """, transaction,
                ("$checkpoint", checkpoint.CheckpointId), ("$run", attempt.RunId), ("$attempt", attempt.AttemptId),
                ("$snapshot", binding.InstallationSnapshotId), ("$context", binding.AnalysisContextId),
                ("$config", binding.EffectiveScanConfigurationId), ("$manifest", binding.ResolvedInputManifestId),
                ("$dependency", checkpoint.DependencyClosureId), ("$sha", checkpoint.ContentSha256),
                ("$completed", completedPartitionsJson), ("$pending", checkpoint.PendingAndGapsJson),
                ("$now", ToText(now)));
            RequireCandidateRow(
                """
                SELECT COUNT(*) FROM checkpoints
                WHERE checkpoint_id=$checkpoint AND run_id=$run AND attempt_id=$attempt
                  AND dependency_closure_id=$dependency AND content_sha256=$sha
                  AND completed_partitions_json=$completed AND pending_and_gaps_json=$pending;
                """,
                "A candidate checkpoint identity resolves to different retained semantics.", transaction,
                ("$checkpoint", checkpoint.CheckpointId), ("$run", attempt.RunId), ("$attempt", attempt.AttemptId),
                ("$dependency", checkpoint.DependencyClosureId), ("$sha", checkpoint.ContentSha256),
                ("$completed", completedPartitionsJson), ("$pending", checkpoint.PendingAndGapsJson));
            transaction.Commit();
            return new CandidatePhasePersistenceReceipt(receipt, checkpointPayloadId);
        }
    }

    private CandidateAnalysisPersistenceReceipt PublishCandidateAnalysisCore(
        CandidateAnalysisContract analysis,
        ReadOnlyMemory<byte> serializedAnalysis,
        DateTimeOffset now,
        SqliteTransaction transaction)
    {
        EnsureRunExists(analysis.OriginatingRunId.Value, transaction);
        string payloadId = AdmitCoordinatorPayload(
                serializedAnalysis.Span,
                "candidate-analysis",
                analysis.PayloadId.Value,
                now,
                transaction);
        foreach (CandidateDecisionContract decision in analysis.Decisions)
        {
            Execute(
                """
                    INSERT OR IGNORE INTO candidate_decisions(
                        candidate_decision_id, run_id, population_id, relationship_id,
                        disposition, lane, rule_version, decision_payload_id, created_at)
                    VALUES ($decision, $run, $population, $relationship, $disposition,
                            $lane, $rule, $payload, $now);
                    """,
                transaction,
                ("$decision", decision.DecisionId.Value),
                ("$run", analysis.OriginatingRunId.Value),
                ("$population", analysis.PopulationId.Value),
                ("$relationship", decision.PopulationMemberId.Value),
                ("$disposition", CandidateDispositionToken(decision.Disposition)),
                ("$lane", CandidateLaneToken(decision.Lane)),
                ("$rule", decision.PolicyId.Value),
                ("$payload", payloadId),
                ("$now", ToText(now)));
            RequireCandidateRow(
                """
                    SELECT COUNT(*) FROM candidate_decisions
                    WHERE candidate_decision_id = $id AND run_id = $run
                      AND population_id = $population AND relationship_id = $relationship
                      AND disposition = $disposition AND lane = $lane AND rule_version = $rule;
                    """,
                "A candidate decision ID resolves to different retained semantics.",
                transaction,
                ("$id", decision.DecisionId.Value), ("$run", analysis.OriginatingRunId.Value),
                ("$population", analysis.PopulationId.Value), ("$relationship", decision.PopulationMemberId.Value),
                ("$disposition", CandidateDispositionToken(decision.Disposition)),
                ("$lane", CandidateLaneToken(decision.Lane)), ("$rule", decision.PolicyId.Value));
        }
        Dictionary<OpaqueId, CandidateDecisionContract> decisions = analysis.Decisions.ToDictionary(item => item.DecisionId);
        foreach (CandidateAnalysisEntryContract candidate in analysis.Candidates)
        {
            CandidateDecisionContract decision = decisions[candidate.DecisionId];
            Execute(
                """
                    INSERT OR IGNORE INTO analysis_candidates(
                        candidate_id, candidate_decision_id, run_id, lane, candidate_state,
                        dependency_closure_id, candidate_payload_id, created_at)
                    VALUES ($candidate, $decision, $run, $lane, $state, $closure, $payload, $now);
                    """,
                transaction,
                ("$candidate", candidate.CandidateId.Value),
                ("$decision", candidate.DecisionId.Value),
                ("$run", analysis.OriginatingRunId.Value),
                ("$lane", CandidateLaneToken(decision.Lane)),
                ("$state", CandidateStateToken(candidate.State)),
                ("$closure", decision.DependencyClosureId.Value),
                ("$payload", payloadId),
                ("$now", ToText(now)));
            RequireCandidateRow(
                """
                    SELECT COUNT(*) FROM analysis_candidates
                    WHERE candidate_id = $id AND candidate_decision_id = $decision AND run_id = $run
                      AND lane = $lane AND candidate_state = $state AND dependency_closure_id = $closure;
                    """,
                "A candidate ID resolves to different retained semantics.",
                transaction,
                ("$id", candidate.CandidateId.Value), ("$decision", candidate.DecisionId.Value),
                ("$run", analysis.OriginatingRunId.Value), ("$lane", CandidateLaneToken(decision.Lane)),
                ("$state", CandidateStateToken(candidate.State)), ("$closure", decision.DependencyClosureId.Value));
        }
        foreach (CandidateHypothesisContract hypothesis in analysis.Hypotheses)
        {
            Execute(
                """
                    INSERT OR IGNORE INTO analysis_hypotheses(
                        hypothesis_id, candidate_id, run_id, hypothesis_state, confidence,
                        threshold_id, hypothesis_payload_id, created_at)
                    VALUES ($hypothesis, $candidate, $run, $state, $confidence, $threshold, $payload, $now);
                    """,
                transaction,
                ("$hypothesis", hypothesis.HypothesisId.Value),
                ("$candidate", hypothesis.CandidateId.Value),
                ("$run", analysis.OriginatingRunId.Value),
                ("$state", CandidateStateToken(hypothesis.State)),
                ("$confidence", CandidateConfidenceToken(hypothesis.Confidence)),
                ("$threshold", hypothesis.ThresholdId.Value),
                ("$payload", payloadId),
                ("$now", ToText(now)));
            RequireCandidateRow(
                """
                    SELECT COUNT(*) FROM analysis_hypotheses
                    WHERE hypothesis_id = $id AND candidate_id = $candidate AND run_id = $run
                      AND hypothesis_state = $state AND confidence = $confidence AND threshold_id = $threshold;
                    """,
                "A hypothesis ID resolves to different retained semantics.",
                transaction,
                ("$id", hypothesis.HypothesisId.Value), ("$candidate", hypothesis.CandidateId.Value),
                ("$run", analysis.OriginatingRunId.Value), ("$state", CandidateStateToken(hypothesis.State)),
                ("$confidence", CandidateConfidenceToken(hypothesis.Confidence)), ("$threshold", hypothesis.ThresholdId.Value));
        }
        foreach (CandidateDependencyEdgeContract edge in analysis.DependencyEdges)
        {
            Execute(
                """
                    INSERT OR IGNORE INTO analysis_dependency_edges(
                        dependency_edge_id, run_id, from_kind, from_id, to_kind, to_id,
                        edge_kind, edge_payload_id, created_at)
                    VALUES ($edge, $run, $from_kind, $from, $to_kind, $to, $kind, $payload, $now);
                    """,
                transaction,
                ("$edge", edge.EdgeId.Value),
                ("$run", analysis.OriginatingRunId.Value),
                ("$from_kind", edge.FromKind),
                ("$from", edge.FromId.Value),
                ("$to_kind", edge.ToKind),
                ("$to", edge.ToId.Value),
                ("$kind", edge.EdgeKind),
                ("$payload", payloadId),
                ("$now", ToText(now)));
            RequireCandidateRow(
                """
                    SELECT COUNT(*) FROM analysis_dependency_edges
                    WHERE dependency_edge_id = $id AND run_id = $run AND from_kind = $from_kind
                      AND from_id = $from AND to_kind = $to_kind AND to_id = $to AND edge_kind = $kind;
                    """,
                "A candidate dependency edge ID resolves to different retained semantics.",
                transaction,
                ("$id", edge.EdgeId.Value), ("$run", analysis.OriginatingRunId.Value),
                ("$from_kind", edge.FromKind), ("$from", edge.FromId.Value),
                ("$to_kind", edge.ToKind), ("$to", edge.ToId.Value), ("$kind", edge.EdgeKind));
        }
        return new CandidateAnalysisPersistenceReceipt(
            analysis.PayloadId.Value,
            payloadId,
            analysis.Decisions.Count,
            analysis.Candidates.Count,
            analysis.Hypotheses.Count,
            analysis.Abstentions.Count,
            analysis.Gaps.Count,
            analysis.Failures.Count,
            analysis.DependencyEdges.Count);
    }

    public byte[] ReadCandidateAnalysisPayload(string payloadId)
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
            if (expectedLength > 64 * 1024 * 1024)
            {
                throw new InvalidDataException("Candidate analysis payload exceeds the readback bound.");
            }
            using FileStream stream = Paths.OpenReadFile(
                ProductWriteClass.Payload,
                relativePath["payloads/".Length..].Replace('/', Path.DirectorySeparatorChar));
            byte[] bytes = new byte[checked((int)expectedLength)];
            stream.ReadExactly(bytes);
            if (!StringComparer.Ordinal.Equals(Hash(bytes), expectedSha))
            {
                throw new InvalidDataException("Candidate analysis payload failed readback identity validation.");
            }
            return bytes;
        }
    }

    public byte[] ReadCandidateCheckpointPayload(string payloadId) =>
        ReadCandidateAnalysisPayload(payloadId);

    public CandidateCheckpointPersistenceRecord? ReadLatestCandidateCheckpoint(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT checkpoint_id, run_id, dependency_closure_id, content_sha256,
                       completed_partitions_json, pending_and_gaps_json, created_at
                FROM checkpoints
                WHERE run_id = $run AND dependency_closure_id LIKE 'candidate-checkpoint-%'
                ORDER BY created_at DESC, checkpoint_id DESC
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$run", runId);
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read()
                ? new CandidateCheckpointPersistenceRecord(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), DateTimeOffset.Parse(reader.GetString(6), System.Globalization.CultureInfo.InvariantCulture))
                : null;
        }
    }

    public CandidateCheckpointPersistenceRecord ReadCandidateCheckpoint(string checkpointId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT checkpoint_id, run_id, dependency_closure_id, content_sha256,
                       completed_partitions_json, pending_and_gaps_json, created_at
                FROM checkpoints WHERE checkpoint_id=$checkpoint;
                """;
            command.Parameters.AddWithValue("$checkpoint", checkpointId);
            using SqliteDataReader reader = command.ExecuteReader();
            return reader.Read()
                ? new CandidateCheckpointPersistenceRecord(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), DateTimeOffset.Parse(
                        reader.GetString(6), System.Globalization.CultureInfo.InvariantCulture))
                : throw new KeyNotFoundException($"Candidate checkpoint '{checkpointId}' does not exist.");
        }
    }

    private static string CandidateLaneToken(CandidateLane value) => value switch
    {
        CandidateLane.DeterministicRequired => "deterministic-required",
        CandidateLane.MandatoryEvidence => "mandatory-evidence",
        CandidateLane.OptionalRanked => "optional-ranked",
        _ => throw new InvalidOperationException("Candidate lane is not persistable."),
    };

    private void RequireCandidateRow(
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

    private static string CandidateDispositionToken(CandidateDecisionDisposition value) => value switch
    {
        CandidateDecisionDisposition.CandidateAdmitted => "candidate-admitted",
        CandidateDecisionDisposition.Ambiguous => "ambiguous",
        CandidateDecisionDisposition.ResolvedNegative => "resolved-negative",
        CandidateDecisionDisposition.Unsupported => "unsupported",
        CandidateDecisionDisposition.InvalidInput => "invalid-input",
        CandidateDecisionDisposition.Limited => "limited",
        CandidateDecisionDisposition.Deferred => "deferred",
        CandidateDecisionDisposition.Unprocessed => "unprocessed",
        CandidateDecisionDisposition.Failed => "failed",
        _ => throw new InvalidOperationException("Candidate disposition is not persistable."),
    };

    private static string CandidateStateToken(AnalysisResultState value) => value switch
    {
        AnalysisResultState.Present => "present",
        AnalysisResultState.Ambiguous => "ambiguous",
        AnalysisResultState.Partial => "partial",
        AnalysisResultState.Abstained => "abstained",
        AnalysisResultState.Unsupported => "unsupported",
        AnalysisResultState.Failed => "failed",
        AnalysisResultState.LimitReached => "limit-reached",
        _ => throw new InvalidOperationException("Candidate state is not persistable."),
    };

    private static string CandidateConfidenceToken(AnalysisConfidence value) => value switch
    {
        AnalysisConfidence.SpeculativeLead => "speculative-lead",
        AnalysisConfidence.Plausible => "plausible",
        AnalysisConfidence.StronglySupported => "strongly-supported",
        AnalysisConfidence.Confirmed => "confirmed",
        _ => throw new InvalidOperationException("Candidate confidence is not persistable."),
    };
}
