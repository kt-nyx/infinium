using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

namespace Infinium.Persistence;

public sealed partial class AuthoritativeStore
{
    public AnalysisCancellationPublicationAdmission PrepareCancelledAnalysisPublication(
        string runId,
        long coordinatorFencingEpoch,
        ReadOnlySpan<byte> receiptBytes,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        if (receiptBytes.Length is < 1 or > 1024 * 1024)
        {
            throw new InvalidDataException("The cancellation publication receipt exceeds its finite bound.");
        }
        using (JsonDocument document = JsonDocument.Parse(receiptBytes.ToArray()))
        {
            _ = document.RootElement.ValueKind;
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            RunRecord current = GetRunCore(runId);
            if (current.State != LifecycleState.Cancelling
                || current.CoordinatorFencingEpoch > coordinatorFencingEpoch
                || ScalarLong(
                    "SELECT COUNT(*) FROM attempts WHERE run_id=$run AND outcome='running';",
                    transaction, ("$run", runId)) != 0)
            {
                throw new InvalidOperationException("Cancellation publication requires a fenced cancelling run with no live attempt.");
            }
            long attemptGeneration = ScalarLong(
                "SELECT COALESCE(MAX(attempt_generation),0)+1 FROM attempts WHERE run_id=$run;",
                transaction, ("$run", runId));
            long fencingToken = ScalarLong(
                "SELECT COALESCE(MAX(attempt_fencing_token),0)+1 FROM attempts WHERE run_id=$run;",
                transaction, ("$run", runId));
            string attemptId = Guid.NewGuid().ToString("N");
            DateTimeOffset expires = now.AddMinutes(2);
            Execute(
                """
                INSERT INTO attempts(
                    attempt_id,run_id,job_node_id,attempt_generation,coordinator_fencing_epoch,
                    attempt_fencing_token,lease_acquired_at,lease_expires_at,dispatch_identity,
                    idempotency_identity,retry_safety,outcome,created_at)
                VALUES ($attempt,$run,$job,$generation,$epoch,$token,$now,$expires,$dispatch,
                    $idempotency,'safe-with-new-attempt','completed-staged',$now);
                """, transaction,
                ("$attempt", attemptId), ("$run", runId), ("$job", runId + "-root"),
                ("$generation", attemptGeneration), ("$epoch", coordinatorFencingEpoch),
                ("$token", fencingToken), ("$now", ToText(now)), ("$expires", ToText(expires)),
                ("$dispatch", Guid.NewGuid().ToString("N")), ("$idempotency", Guid.NewGuid().ToString("N")));
            string payloadId = AdmitCoordinatorPayload(receiptBytes, "attempt", attemptId, now, transaction);
            string receiptId = Guid.NewGuid().ToString("N");
            Execute(
                """
                INSERT INTO publication_receipts(
                    receipt_id,run_id,attempt_id,coordinator_fencing_epoch,
                    attempt_fencing_token,staged_manifest_sha256,published_at)
                VALUES ($receipt,$run,$attempt,$epoch,$token,$sha,$now);
                """, transaction,
                ("$receipt", receiptId), ("$run", runId), ("$attempt", attemptId),
                ("$epoch", coordinatorFencingEpoch), ("$token", fencingToken),
                ("$sha", Hash(receiptBytes)), ("$now", ToText(now)));
            Execute(
                "INSERT INTO publication_receipt_payloads(receipt_id,payload_id) VALUES ($receipt,$payload);",
                transaction, ("$receipt", receiptId), ("$payload", payloadId));
            InsertAuditEvent("analysis-cancellation-receipt-admitted", "attempt", attemptId, now, transaction);
            transaction.Commit();
            return new AnalysisCancellationPublicationAdmission(
                new AttemptRecord(attemptId, runId, attemptGeneration, coordinatorFencingEpoch,
                    fencingToken, expires, "completed-staged"),
                payloadId);
        }
    }

    public string AdmitAnalysisCoordinatorFailureReceipt(
        AttemptRecord attempt,
        ReadOnlySpan<byte> receiptBytes,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        if (receiptBytes.Length is < 1 or > 1024 * 1024)
        {
            throw new InvalidDataException("The coordinator failure receipt exceeds its finite bound.");
        }
        using (JsonDocument document = JsonDocument.Parse(receiptBytes.ToArray()))
        {
            _ = document.RootElement.ValueKind;
        }
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentAttempt(attempt, transaction);
            string payloadId = AdmitCoordinatorPayload(
                receiptBytes, "attempt", attempt.AttemptId, now, transaction);
            string receiptId = Guid.NewGuid().ToString("N");
            Execute(
                """
                INSERT INTO publication_receipts(
                    receipt_id,run_id,attempt_id,coordinator_fencing_epoch,
                    attempt_fencing_token,staged_manifest_sha256,published_at)
                VALUES ($receipt,$run,$attempt,$epoch,$token,$sha,$now);
                """, transaction,
                ("$receipt", receiptId), ("$run", attempt.RunId), ("$attempt", attempt.AttemptId),
                ("$epoch", attempt.CoordinatorFencingEpoch), ("$token", attempt.AttemptFencingToken),
                ("$sha", Hash(receiptBytes)), ("$now", ToText(now)));
            Execute(
                "INSERT INTO publication_receipt_payloads(receipt_id,payload_id) VALUES ($receipt,$payload);",
                transaction, ("$receipt", receiptId), ("$payload", payloadId));
            Execute(
                "UPDATE attempts SET outcome='completed-staged' WHERE attempt_id=$attempt;",
                transaction, ("$attempt", attempt.AttemptId));
            InsertAuditEvent("analysis-failure-receipt-admitted", "attempt", attempt.AttemptId, now, transaction);
            transaction.Commit();
            return payloadId;
        }
    }

    public RetainedPayloadRecord GetRetainedPayload(string payloadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT content_sha256,byte_length,object_relative_path FROM payloads WHERE payload_id=$id AND retention_state='retained';";
            command.Parameters.AddWithValue("$id", payloadId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw new KeyNotFoundException($"Retained payload '{payloadId}' does not exist.");
            }
            return new RetainedPayloadRecord(payloadId, reader.GetString(0), reader.GetInt64(1), reader.GetString(2));
        }
    }

    public AnalysisPublicationPersistenceReceipt PublishAnalysisResult(
        AnalysisPublicationPersistenceRequest request,
        Action<string>? failureInjection = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Attempt);
        ValidateBinding(request.Binding);
        Slice5ContractInvariants.Validate(request.Replay);
        RunOutputContractInvariants.Validate(request.RunOutput);
        ValidateSha256(request.SemanticOutputFingerprint);
        if (request.RunOutput.RunId != request.Attempt.RunId
            || request.Replay.OriginatingRunId.Value != request.Attempt.RunId
            || request.RunOutput.InstallationSnapshot.ArtifactId != request.Binding.InstallationSnapshotId
            || request.RunOutput.AnalysisContext.ArtifactId != request.Binding.AnalysisContextId
            || request.RunOutput.EffectiveScanConfiguration.ArtifactId != request.Binding.EffectiveScanConfigurationId
            || request.RunOutput.ResolvedInputManifest.ArtifactId != request.Binding.ResolvedInputManifestId
            || request.Artifacts.Count is < 5 or > 100
            || request.Artifacts.Select(item => item.ArtifactId).Distinct(StringComparer.Ordinal).Count() != request.Artifacts.Count
            || request.TerminalReason.Length is < 1 or > 512
            || request.TerminalState is not (
                LifecycleState.Completed or LifecycleState.CompletedWithGaps or LifecycleState.Cancelled
                or LifecycleState.LimitReached or LifecycleState.Failed))
        {
            throw new InvalidDataException("Analysis publication does not match its immutable run, bounds, or terminal contract.");
        }

        string replaySha = Hash(request.ReplayBytes.Span);
        string outputSha = Hash(request.RunOutputBytes.Span);
        string outputId = "run-output-" + request.Attempt.RunId;
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            string? existingOutcome = ScalarStringOrNull(
                "SELECT outcome FROM attempts WHERE attempt_id=$attempt;",
                transaction, ("$attempt", request.Attempt.AttemptId));
            if (StringComparer.Ordinal.Equals(existingOutcome, "analysis-result-published"))
            {
                string existingFingerprint = ScalarString(
                    "SELECT provenance_id FROM analysis_run_outputs WHERE run_id=$run ORDER BY revision DESC LIMIT 1;",
                    transaction, ("$run", request.Attempt.RunId));
                string existingState = ScalarString(
                    "SELECT lifecycle_state FROM runs WHERE run_id=$run;",
                    transaction, ("$run", request.Attempt.RunId));
                if (!StringComparer.Ordinal.Equals(existingFingerprint, request.SemanticOutputFingerprint)
                    || !StringComparer.Ordinal.Equals(existingState, request.TerminalState.ToString()))
                {
                    throw new InvalidOperationException(
                        "A duplicate publication admission differs from the already committed result.");
                }
                string replayId = ScalarString(
                    "SELECT replay_manifest_id FROM analysis_replay_manifests WHERE run_id=$run ORDER BY created_at DESC LIMIT 1;",
                    transaction, ("$run", request.Attempt.RunId));
                string existingOutputId = "run-output-" + request.Attempt.RunId;
                string Owned(string kind, string owner) => ScalarString(
                    "SELECT payload_id FROM payload_owners WHERE owner_kind=$kind AND owner_id=$owner ORDER BY payload_id LIMIT 1;",
                    transaction, ("$kind", kind), ("$owner", owner));
                long generation = ScalarLong(
                    "SELECT lifecycle_generation FROM runs WHERE run_id=$run;",
                    transaction, ("$run", request.Attempt.RunId));
                string replayPayload = Owned("analysis-replay", replayId);
                string outputPayload = Owned("run-output", existingOutputId);
                string cliPayload = Owned("cli-summary", request.Attempt.RunId);
                string boundaryPayload = Owned("external-boundary-receipt", request.Attempt.RunId);
                string artifactIndexPayload = Owned("analysis-artifact-index", request.Attempt.RunId);
                transaction.Rollback();
                return new AnalysisPublicationPersistenceReceipt(
                    request.Attempt.RunId, replayId, existingOutputId,
                    replayPayload, outputPayload, cliPayload, boundaryPayload, artifactIndexPayload,
                    existingFingerprint, request.TerminalState, generation);
            }
            EnsureAnalysisStagedAttemptCurrent(request.Attempt, transaction);
            RunRecord current = GetRunCore(request.Attempt.RunId);
            LifecyclePolicy.EnsureAllowed(current.State, request.TerminalState);
            RequireAnalysisRow(
                """
                SELECT COUNT(*) FROM runs
                WHERE run_id=$run AND installation_snapshot_id=$snapshot AND analysis_context_id=$context
                  AND effective_scan_configuration_id=$configuration AND resolved_input_manifest_id=$manifest;
                """, "Analysis publication differs from the immutable run binding.", transaction,
                ("$run", request.Attempt.RunId), ("$snapshot", request.Binding.InstallationSnapshotId),
                ("$context", request.Binding.AnalysisContextId), ("$configuration", request.Binding.EffectiveScanConfigurationId),
                ("$manifest", request.Binding.ResolvedInputManifestId));
            RequireAnalysisRow(
                """
                SELECT COUNT(*) FROM payloads p
                JOIN payload_owners o ON o.payload_id=p.payload_id
                WHERE p.payload_id=$payload AND p.retention_state='retained'
                  AND o.owner_kind='attempt' AND o.owner_id=$attempt;
                """, "The worker validation receipt is absent or belongs to another attempt.", transaction,
                ("$payload", request.ValidationReceiptPayloadId), ("$attempt", request.Attempt.AttemptId));

            failureInjection?.Invoke("before-payload-admission");
            string replayPayloadId = AdmitCoordinatorPayload(
                request.ReplayBytes.Span, "analysis-replay", request.Replay.ReplayManifestId.Value,
                request.PublishedAt, transaction);
            string outputPayloadId = AdmitCoordinatorPayload(
                request.RunOutputBytes.Span, "run-output", outputId, request.PublishedAt, transaction);
            string cliPayloadId = AdmitCoordinatorPayload(
                request.CliSummaryBytes.Span, "cli-summary", request.Attempt.RunId,
                request.PublishedAt, transaction);
            string boundaryPayloadId = AdmitCoordinatorPayload(
                request.BoundaryReceiptBytes.Span, "external-boundary-receipt", request.Attempt.RunId,
                request.PublishedAt, transaction);
            string artifactIndexPayloadId = AdmitCoordinatorPayload(
                request.ArtifactIndexBytes.Span, "analysis-artifact-index", request.Attempt.RunId,
                request.PublishedAt, transaction);
            failureInjection?.Invoke("after-payload-admission");

            Execute(
                """
                INSERT INTO analysis_replay_manifests(
                    replay_manifest_id,run_id,replay_mode,replay_state,auditability_state,
                    semantic_equivalence,compared_run_id,manifest_payload_id,manifest_sha256,created_at)
                VALUES ($id,$run,$mode,$state,$audit,$equivalent,$compared,$payload,$sha,$now);
                """, transaction,
                ("$id", request.Replay.ReplayManifestId.Value), ("$run", request.Attempt.RunId),
                ("$mode", ReplayModeToken(request.Replay.Mode)), ("$state", Kebab(request.Replay.ReplayState)),
                ("$audit", Kebab(request.Replay.AuditabilityState)), ("$equivalent", request.Replay.SemanticallyEquivalent ? 1 : 0),
                ("$compared", request.Replay.ComparedRunId?.Value), ("$payload", replayPayloadId),
                ("$sha", replaySha), ("$now", ToText(request.PublishedAt)));
            Execute(
                """
                INSERT INTO analysis_run_outputs(
                    run_output_id,run_id,payload_schema_id,payload_schema_version,revision,
                    output_state,output_payload_id,output_sha256,byte_length,provenance_id,
                    dependency_closure_id,replay_manifest_id,created_at)
                VALUES ($id,$run,$schema,$version,1,'present',$payload,$sha,$length,$provenance,$closure,$replay,$now);
                """, transaction,
                ("$id", outputId), ("$run", request.Attempt.RunId), ("$schema", request.RunOutput.SchemaId),
                ("$version", request.RunOutput.SchemaVersion), ("$payload", outputPayloadId),
                ("$sha", outputSha), ("$length", request.RunOutputBytes.Length),
                ("$provenance", request.SemanticOutputFingerprint), ("$closure", request.DependencyClosureId),
                ("$replay", request.Replay.ReplayManifestId.Value), ("$now", ToText(request.PublishedAt)));

            foreach (ReplayDependencyNodeContract dependency in request.Replay.Dependencies)
            {
                string edgeId = StableAnalysisId(
                    "replay-dependency", request.Replay.ReplayManifestId.Value, dependency.DependencyId.Value);
                Execute(
                    """
                    INSERT OR IGNORE INTO analysis_dependency_edges(
                        dependency_edge_id,run_id,from_kind,from_id,to_kind,to_id,edge_kind,edge_payload_id,created_at)
                    VALUES ($id,$run,'replay-manifest',$from,'dependency',$to,'uses',$payload,$now);
                    """, transaction,
                    ("$id", edgeId), ("$run", request.Attempt.RunId),
                    ("$from", request.Replay.ReplayManifestId.Value), ("$to", dependency.DependencyId.Value),
                    ("$payload", replayPayloadId), ("$now", ToText(request.PublishedAt)));
            }
            foreach (ReplayDependencyEdgeContract dependency in request.Replay.Edges)
            {
                string edgeId = StableAnalysisId(
                    "replay-closure-edge", request.Attempt.RunId,
                    dependency.From.Value, dependency.To.Value);
                Execute(
                    """
                    INSERT OR IGNORE INTO analysis_dependency_edges(
                        dependency_edge_id,run_id,from_kind,from_id,to_kind,to_id,edge_kind,edge_payload_id,created_at)
                    VALUES ($id,$run,'dependency-closure',$from,'dependency',$to,'depends-on',$payload,$now);
                    """, transaction,
                    ("$id", edgeId), ("$run", request.Attempt.RunId),
                    ("$from", dependency.From.Value), ("$to", dependency.To.Value),
                    ("$payload", replayPayloadId), ("$now", ToText(request.PublishedAt)));
            }
            foreach (string effectClass in new[] { "database", "payload-store", "staging", "trace", "run-output" })
            {
                Execute(
                    """
                    INSERT INTO effect_receipts(
                        effect_receipt_id,run_id,effect_class,effect_state,object_id,receipt_payload_id,created_at)
                    VALUES ($id,$run,$class,$state,$object,$payload,$now);
                    """, transaction,
                    ("$id", StableAnalysisId("effect", request.Attempt.RunId, effectClass)),
                    ("$run", request.Attempt.RunId), ("$class", effectClass),
                    ("$state", effectClass == "trace" ? "not-used" : "admitted"),
                    ("$object", effectClass == "run-output" ? outputId : request.Attempt.RunId),
                    ("$payload", boundaryPayloadId), ("$now", ToText(request.PublishedAt)));
            }
            failureInjection?.Invoke("before-terminal-cas");

            long nextGeneration = checked(current.Generation + 1);
            long nextSequence = checked(current.DurableSequence + 1);
            int changed = Execute(
                """
                UPDATE runs SET lifecycle_state=$state,lifecycle_generation=$generation,
                    coordinator_fencing_epoch=$epoch,durable_sequence=$sequence,updated_at=$now
                WHERE run_id=$run AND lifecycle_generation=$expected;
                """, transaction,
                ("$state", request.TerminalState.ToString()), ("$generation", nextGeneration),
                ("$epoch", request.Attempt.CoordinatorFencingEpoch), ("$sequence", nextSequence),
                ("$now", ToText(request.PublishedAt)), ("$run", request.Attempt.RunId), ("$expected", current.Generation));
            if (changed != 1)
            {
                throw new InvalidOperationException("Analysis publication lost its lifecycle compare-and-swap race.");
            }
            Execute(
                "UPDATE attempts SET outcome='analysis-result-published', lease_expires_at=$now WHERE attempt_id=$attempt;",
                transaction, ("$now", ToText(request.PublishedAt.AddTicks(1))), ("$attempt", request.Attempt.AttemptId));
            Execute(
                "UPDATE job_nodes SET lifecycle_state=$state,lifecycle_generation=$generation,updated_at=$now WHERE run_id=$run;",
                transaction, ("$state", request.TerminalState.ToString()), ("$generation", nextGeneration),
                ("$now", ToText(request.PublishedAt)), ("$run", request.Attempt.RunId));
            Execute(
                "UPDATE run_projection SET lifecycle_state=$state,lifecycle_generation=$generation,durable_sequence=$sequence,updated_at=$now WHERE run_id=$run;",
                transaction, ("$state", request.TerminalState.ToString()), ("$generation", nextGeneration),
                ("$sequence", nextSequence), ("$now", ToText(request.PublishedAt)), ("$run", request.Attempt.RunId));
            Execute(
                """
                INSERT INTO lifecycle_events(
                    transition_id,run_id,job_node_id,record_kind,policy_version,from_state,to_state,
                    expected_generation,new_generation,coordinator_fencing_epoch,reason,occurred_at,durable_sequence)
                VALUES ($id,$run,$job,'observed',$policy,$from,$to,$expected,$generation,$epoch,$reason,$now,$sequence);
                """, transaction,
                ("$id", Guid.NewGuid().ToString("N")), ("$run", request.Attempt.RunId),
                ("$job", request.Attempt.RunId + "-root"), ("$policy", LifecyclePolicy.Version),
                ("$from", current.State.ToString()), ("$to", request.TerminalState.ToString()),
                ("$expected", current.Generation), ("$generation", nextGeneration),
                ("$epoch", request.Attempt.CoordinatorFencingEpoch), ("$reason", request.TerminalReason),
                ("$now", ToText(request.PublishedAt)), ("$sequence", nextSequence));

            string publicationReceiptId = ScalarString(
                "SELECT receipt_id FROM publication_receipts WHERE attempt_id=$attempt;",
                transaction, ("$attempt", request.Attempt.AttemptId));
            foreach (string payload in new[]
            {
                replayPayloadId, outputPayloadId, cliPayloadId, boundaryPayloadId,
                artifactIndexPayloadId,
            })
            {
                Execute(
                    "INSERT INTO publication_receipt_payloads(receipt_id,payload_id) VALUES ($receipt,$payload);",
                    transaction, ("$receipt", publicationReceiptId), ("$payload", payload));
            }
            InsertAuditEvent("analysis-result-published", "run", request.Attempt.RunId, request.PublishedAt, transaction);
            failureInjection?.Invoke("before-commit");
            transaction.Commit();
            return new AnalysisPublicationPersistenceReceipt(
                request.Attempt.RunId, request.Replay.ReplayManifestId.Value, outputId,
                replayPayloadId, outputPayloadId, cliPayloadId, boundaryPayloadId, artifactIndexPayloadId,
                request.SemanticOutputFingerprint, request.TerminalState, nextGeneration);
        }
    }

    public byte[] ReadAnalysisRunOutput(string runId) => ReadOwnedAnalysisPayload("run-output", "run-output-" + runId);
    public byte[] ReadAnalysisCliSummary(string runId) => ReadOwnedAnalysisPayload("cli-summary", runId);
    public byte[] ReadAnalysisBoundaryReceipt(string runId) => ReadOwnedAnalysisPayload("external-boundary-receipt", runId);
    public byte[] ReadAnalysisReplay(string runId) =>
        ReadOwnedAnalysisPayload("analysis-replay", GetAnalysisReplay(runId).ReplayManifestId);

    public string? GetAnalysisSemanticFingerprint(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT provenance_id FROM analysis_run_outputs WHERE run_id=$run ORDER BY revision DESC LIMIT 1;";
            command.Parameters.AddWithValue("$run", runId);
            return command.ExecuteScalar() as string;
        }
    }

    public AnalysisSummaryPersistenceRecord GetAnalysisSummary(string runId)
    {
        AnalysisReplayPersistenceRecord replay = GetAnalysisReplay(runId);
        lock (gate)
        {
            long Count(string sql)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("$run", runId);
                return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
            }
            return new AnalysisSummaryPersistenceRecord(
                runId,
                Count("SELECT COUNT(*) FROM finding_occurrences WHERE run_id=$run;"),
                Count("SELECT COUNT(*) FROM case_occurrences c JOIN case_occurrence_details d ON d.case_occurrence_id=c.case_occurrence_id WHERE c.run_id=$run AND d.case_kind='supported';"),
                Count("SELECT COUNT(*) FROM case_occurrences c JOIN case_occurrence_details d ON d.case_occurrence_id=c.case_occurrence_id WHERE c.run_id=$run AND d.case_kind='lead-only';"),
                Count("SELECT COUNT(*) FROM candidate_decisions WHERE run_id=$run;"),
                Count("SELECT COUNT(*) FROM analysis_coverage WHERE run_id=$run;"),
                Count("SELECT COUNT(*) FROM analysis_gaps WHERE run_id=$run;"),
                Count("SELECT COUNT(*) FROM candidate_decisions WHERE run_id=$run AND disposition='unsupported';"),
                replay.ReplayManifestId, replay.ReplayState, replay.AuditabilityState, replay.SemanticallyEquivalent,
                replay.DependencyCount, replay.MissingDependencyCount, replay.CoverageGapCount, "1");
        }
    }

    public AnalysisReplayPersistenceRecord GetAnalysisReplay(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        lock (gate)
        {
            string replayId;
            string state;
            string audit;
            bool equivalent;
            long dependencies;
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT m.replay_manifest_id,m.replay_state,m.auditability_state,m.semantic_equivalence,
                       (SELECT COUNT(*) FROM analysis_dependency_edges e WHERE e.run_id=m.run_id AND e.from_kind='replay-manifest' AND e.from_id=m.replay_manifest_id)
                FROM analysis_replay_manifests m WHERE m.run_id=$run ORDER BY m.created_at DESC LIMIT 1;
                """;
            command.Parameters.AddWithValue("$run", runId);
            {
                using SqliteDataReader reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    throw new KeyNotFoundException($"Run '{runId}' has no published replay manifest.");
                }
                replayId = reader.GetString(0);
                state = reader.GetString(1);
                audit = reader.GetString(2);
                equivalent = reader.GetInt64(3) == 1;
                dependencies = reader.GetInt64(4);
            }
            byte[] replayBytes = ReadOwnedAnalysisPayloadCore("analysis-replay", replayId);
            using JsonDocument document = JsonDocument.Parse(replayBytes);
            long missing = document.RootElement.GetProperty("missing_dependency_ids").GetArrayLength();
            long gaps = document.RootElement.GetProperty("coverage_gap_ids").GetArrayLength();
            return new AnalysisReplayPersistenceRecord(replayId, state, audit, equivalent, dependencies, missing, gaps);
        }
    }

    public AnalysisArtifactPagePersistenceRecord ListAnalysisArtifacts(
        string runId,
        IReadOnlySet<string> kinds,
        IReadOnlySet<string> states,
        int maximumCount,
        AnalysisArtifactSortOrder sortOrder,
        AnalysisArtifactCursorKey? after)
    {
        if (maximumCount is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }
        byte[] bytes = ReadOwnedAnalysisPayload("analysis-artifact-index", runId);
        AnalysisArtifactPersistenceRecord[] all = JsonSerializer.Deserialize<AnalysisArtifactPersistenceRecord[]>(bytes)
            ?? throw new InvalidDataException("The retained analysis artifact index is empty.");
        return AnalysisArtifactKeysetPaginator.Page(all, kinds, states, maximumCount, sortOrder, after);
    }

    public AnalysisArtifactPersistenceRecord GetAnalysisArtifact(string runId, string artifactId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        byte[] bytes = ReadOwnedAnalysisPayload("analysis-artifact-index", runId);
        AnalysisArtifactPersistenceRecord[] all = JsonSerializer.Deserialize<AnalysisArtifactPersistenceRecord[]>(bytes)
            ?? throw new InvalidDataException("The retained analysis artifact index is empty.");
        return all.SingleOrDefault(item => item.ArtifactId == artifactId)
            ?? throw new KeyNotFoundException($"Published analysis artifact '{artifactId}' does not exist.");
    }

    public IReadOnlyList<string> ListAnalysisDependencyIds(string runId, string artifactId, int maximumCount)
    {
        if (maximumCount is < 1 or > 257)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }
        AnalysisArtifactPersistenceRecord artifact = GetAnalysisArtifact(runId, artifactId);
        if (string.IsNullOrWhiteSpace(artifact.DependencyClosureId))
        {
            throw new InvalidDataException("The published artifact has no dependency closure identity.");
        }
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                """
                WITH RECURSIVE closure(dependency_id) AS (
                    SELECT to_id FROM analysis_dependency_edges
                    WHERE run_id=$run AND from_kind='dependency-closure' AND from_id=$artifact
                        AND edge_kind='depends-on'
                    UNION
                    SELECT edge.to_id FROM analysis_dependency_edges edge
                    JOIN closure prior ON edge.from_id=prior.dependency_id
                    WHERE edge.run_id=$run AND edge.from_kind='dependency-closure'
                        AND edge.edge_kind='depends-on'
                )
                SELECT dependency_id FROM closure ORDER BY dependency_id LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$run", runId);
            command.Parameters.AddWithValue("$artifact", artifactId);
            command.Parameters.AddWithValue("$limit", maximumCount);
            using SqliteDataReader reader = command.ExecuteReader();
            List<string> ids = [];
            while (reader.Read())
            {
                ids.Add(reader.GetString(0));
            }
            return ids;
        }
    }

    private byte[] ReadOwnedAnalysisPayload(string ownerKind, string ownerId)
    {
        lock (gate)
        {
            return ReadOwnedAnalysisPayloadCore(ownerKind, ownerId);
        }
    }

    private byte[] ReadOwnedAnalysisPayloadCore(string ownerKind, string ownerId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT p.payload_id FROM payloads p JOIN payload_owners o ON o.payload_id=p.payload_id
            WHERE o.owner_kind=$kind AND o.owner_id=$owner AND p.retention_state='retained';
            """;
        command.Parameters.AddWithValue("$kind", ownerKind);
        command.Parameters.AddWithValue("$owner", ownerId);
        string? payloadId = command.ExecuteScalar() as string;
        if (payloadId is null)
        {
            throw new KeyNotFoundException($"Published {ownerKind} '{ownerId}' does not exist.");
        }
        return ReadCandidateAnalysisPayload(payloadId);
    }

    private void RequireAnalysisRow(
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

    private void EnsureAnalysisStagedAttemptCurrent(
        AttemptRecord attempt,
        SqliteTransaction transaction)
    {
        RequireAnalysisRow(
            """
            SELECT COUNT(*) FROM attempts a JOIN runs r ON r.run_id=a.run_id
            WHERE a.attempt_id=$attempt AND a.run_id=$run
              AND a.coordinator_fencing_epoch=$epoch AND a.attempt_fencing_token=$token
              AND a.attempt_fencing_token=(SELECT MAX(n.attempt_fencing_token) FROM attempts n WHERE n.run_id=a.run_id)
              AND a.lease_expires_at >= $now
              AND a.coordinator_fencing_epoch=(SELECT CAST(value AS INTEGER) FROM store_metadata WHERE key='active_coordinator_epoch')
              AND r.lifecycle_state IN ('Running','Waiting','Cancelling')
              AND a.outcome='completed-staged';
            """,
            "The staged analysis attempt is stale, expired, or no longer publication-authoritative.",
            transaction,
            ("$attempt", attempt.AttemptId), ("$run", attempt.RunId),
            ("$epoch", attempt.CoordinatorFencingEpoch), ("$token", attempt.AttemptFencingToken),
            ("$now", ToText(DateTimeOffset.UtcNow)));
    }

    private static string ReplayModeToken(ReplayMode mode) => mode switch
    {
        ReplayMode.Clean => "clean",
        ReplayMode.Incremental => "incremental",
        ReplayMode.RetainedDownstreamReplay => "retained-downstream-replay",
        _ => throw new InvalidDataException("Replay mode is not closed."),
    };

    private static string StableAnalysisId(string kind, params string[] parts) =>
        kind + "-" + Hash(Encoding.UTF8.GetBytes(string.Join('\n', parts)))[..32];
}
