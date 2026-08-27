using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;
using Microsoft.Data.Sqlite;

#pragma warning disable IDE0011 // SQL persistence guard clauses stay compact around long statements.

namespace Infinium.Persistence;

public sealed record TargetedPreparationPersistenceRequest(
    string DurableCommandId,
    string PreparationId,
    string UserGestureId,
    string RequestJson,
    string RequestSha256,
    string SourceRunId,
    string SourceOccurrenceKind,
    string SourceOccurrenceId,
    string ConfirmedProfileId,
    long ConfirmedProfileRevision,
    string SavedConfigurationId,
    long SavedConfigurationRevision,
    string AnalysisContextId,
    long AnalysisContextRevision,
    string AnalysisContextFingerprint,
    string InitiationKind,
    DateTimeOffset DispatchDeadline,
    string CaptureOperationId,
    string CaptureRequestJson,
    string CaptureRequestSha256);

public sealed record TargetedPreparationPersistenceRecord(
    string PreparationId,
    string DurableCommandId,
    string UserGestureId,
    string RequestSha256,
    string RequestJson,
    string SourceRunId,
    string SourceOccurrenceKind,
    string SourceOccurrenceId,
    string ConfirmedProfileId,
    long ConfirmedProfileRevision,
    string SavedConfigurationId,
    long SavedConfigurationRevision,
    string AnalysisContextId,
    long AnalysisContextRevision,
    string AnalysisContextFingerprint,
    string InitiationKind,
    DateTimeOffset DispatchDeadline,
    long Revision,
    TargetedVerificationPreparationState State,
    string PreparationFingerprint,
    string TerminalReason,
    string CaptureOperationId,
    string? TargetSnapshotId,
    string? EvidenceAcquisitionId,
    string? PlanId,
    string? PlanFingerprint,
    bool Startable,
    bool Limited,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SemanticAcquisitionPersistenceRecord(
    string AcquisitionId,
    string PreparationId,
    string TargetSnapshotId,
    string RequestJson,
    string RequestSha256,
    string SealedInputFingerprint,
    DateTimeOffset DispatchDeadline,
    string State,
    long Generation,
    long DurableSequence,
    long ProgressCompleted,
    long ProgressDenominator,
    string? ActiveAttemptId,
    string TerminalReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SemanticAcquisitionAttemptRecord(
    string AttemptId,
    string AcquisitionId,
    long AttemptGeneration,
    long CoordinatorFencingEpoch,
    long AttemptFencingToken,
    string SealedInputFingerprint,
    DateTimeOffset LeaseExpiresAt,
    DateTimeOffset CreatedAt);

public sealed record SemanticAcquisitionPublicationRecord(
    string PublicationId,
    string AcquisitionId,
    string AttemptId,
    long AttemptFencingToken,
    string TargetSnapshotId,
    string SemanticOutputId,
    string PayloadId,
    string PayloadSha256,
    long PayloadByteLength,
    string StagedManifestSha256,
    string ProvenanceJson,
    DateTimeOffset CreatedAt);

public sealed record TargetedStartAdmissionPersistence(
    string AdmissionId,
    string TargetedVerificationId,
    string PreparationId,
    string UserGestureId,
    string StartRequestSha256,
    string SubmissionFingerprint,
    string PreparationFingerprint,
    string SourceRunId,
    string SourceOccurrenceId,
    string TargetSnapshotId,
    string EvidenceAcquisitionId,
    string ManagedOperationFingerprint)
{
    public IReadOnlyList<TargetedOperationInputPersistence> OperationInputs { get; init; } = [];
}

public sealed record TargetedOperationInputPersistence(
    string InputKind,
    string InputId,
    byte[] Bytes);

public sealed record TargetedVerificationReadbackRecord(
    string AdmissionId,
    string TargetedVerificationId,
    string PreparationId,
    string CommandId,
    string SourceRunId,
    string SourceOccurrenceId,
    string SuccessorRunId,
    string TargetSnapshotId,
    string EvidenceAcquisitionId,
    string ManagedOperationKind,
    string ManagedOperationFingerprint,
    string SubmissionFingerprint,
    DateTimeOffset CreatedAt);

public sealed record TargetedCancellationPersistenceReceipt(
    TargetedPreparationPersistenceRecord Preparation,
    bool Replayed);

public sealed record TargetedPreparationDiagnosticsPersistenceRecord(
    string? CaptureAttemptId,
    string? StructuralComparison,
    long EvidenceAttemptCount,
    string? EvidenceAttemptId,
    long EvidenceProgressCompleted,
    long EvidenceProgressDenominator,
    string? EvidenceCheckpointId);

public sealed record TargetedSnapshotReadbackEvidenceRecord(
    string SnapshotPayloadId,
    string SnapshotFingerprint,
    string SourceStructuralFingerprint,
    string TargetStructuralFingerprint,
    string StructuralComparison,
    long ConfirmedProfileRevision,
    byte[] SnapshotPayloadBytes);

public sealed record TargetedAcquisitionReadbackEvidenceRecord(
    string RequestFingerprint,
    string SealedInputFingerprint,
    string ProducerFamily,
    string ProducerVersion,
    string SupportManifestId,
    string EnumerationPolicyId,
    string EnumerationPolicyVersion,
    long CoordinatorFencingEpoch,
    long AttemptFencingToken,
    string? PublicationId,
    string? PublicationPayloadId,
    string? StagedManifestFingerprint,
    string? ProvenanceFingerprint,
    DateTimeOffset? PublishedAt,
    string TerminalReason,
    byte[]? SemanticPayloadBytes);

public sealed record TargetedLifecycleReadbackEvent(
    long Sequence,
    string Owner,
    string EventKind,
    long Generation,
    long CoordinatorFencingEpoch,
    DateTimeOffset OccurredAt,
    string EvidenceFingerprint,
    string Summary);

public sealed record TargetedPreparationReadbackEvidenceRecord(
    TargetedSnapshotReadbackEvidenceRecord? Snapshot,
    TargetedAcquisitionReadbackEvidenceRecord? Acquisition,
    IReadOnlyList<TargetedLifecycleReadbackEvent> LifecycleEvents,
    long? NextLifecycleSequence);

public sealed record TargetedSourceReadbackEvidenceRecord(
    RunRecord Run,
    RunOperationRecord Operation,
    ResultItemPersistenceRecord Occurrence,
    byte[] CanonicalPayloadBytes,
    byte[] SnapshotPayloadBytes);

public sealed record TargetedPreparationReadbackSnapshotRecord(
    TargetedPreparationPersistenceRecord Preparation,
    TargetedSourceReadbackEvidenceRecord Source,
    TargetedVerificationPlanContract? Plan,
    TargetedPreparationDiagnosticsPersistenceRecord Diagnostics,
    SemanticAcquisitionPersistenceRecord? Acquisition,
    SemanticAcquisitionPublicationRecord? AcquisitionPublication,
    TargetedPreparationReadbackEvidenceRecord Evidence);

public sealed partial class AuthoritativeStore
{
    internal Action? TargetedReadbackLockedTestHook { get; set; }

    public TargetedPreparationPersistenceRecord CreateTargetedPreparation(
        TargetedPreparationPersistenceRequest request,
        long coordinatorFencingEpoch,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSha256(request.RequestSha256);
        ValidateSha256(request.CaptureRequestSha256);
        if (Hash(request.RequestJson) != request.RequestSha256
            || Hash(request.CaptureRequestJson) != request.CaptureRequestSha256)
        {
            throw new InvalidDataException("The targeted preparation request fingerprints do not match their bytes.");
        }
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        if (request.SourceOccurrenceKind is not ("finding" or "case")
            || request.ConfirmedProfileRevision <= 0
            || request.SavedConfigurationRevision <= 0
            || request.AnalysisContextRevision <= 0
            || request.DispatchDeadline <= now
            || Encoding.UTF8.GetByteCount(request.RequestJson) > 64 * 1024
            || Encoding.UTF8.GetByteCount(request.CaptureRequestJson) > 64 * 1024)
        {
            throw new InvalidDataException("The targeted preparation request exceeds its closed contract.");
        }
        ValidateSha256(request.AnalysisContextFingerprint);
        ValidateSetupIdentity(request.PreparationId, nameof(request.PreparationId));
        ValidateSetupIdentity(request.DurableCommandId, nameof(request.DurableCommandId));
        ValidateSetupIdentity(request.UserGestureId, nameof(request.UserGestureId));
        if (request.UserGestureId.Length < 16)
        {
            throw new InvalidDataException("The targeted preparation gesture is too short.");
        }

        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            string? existing = ScalarStringOrNull(
                "SELECT preparation_id FROM targeted_preparation_requests WHERE durable_command_id=$command;",
                transaction, ("$command", request.DurableCommandId));
            if (existing is not null)
            {
                TargetedPreparationPersistenceRecord replay = GetTargetedPreparationCore(existing, transaction);
                if (replay.PreparationId != request.PreparationId
                    || replay.RequestSha256 != request.RequestSha256
                    || replay.UserGestureId != request.UserGestureId)
                {
                    throw new InvalidOperationException("A targeted preparation idempotency key cannot be rebound.");
                }
                transaction.Commit();
                return replay;
            }
            if (ScalarLong("SELECT COUNT(*) FROM targeted_preparation_requests WHERE preparation_id=$preparation;",
                    transaction, ("$preparation", request.PreparationId)) != 0)
            {
                throw new InvalidOperationException("A targeted preparation identity cannot be reused.");
            }
            EnsureAuthorityGestureUnused(request.UserGestureId, transaction);
            RunRecord source = GetRunCore(request.SourceRunId, transaction);
            if (source.State is not (LifecycleState.Completed or LifecycleState.CompletedWithGaps
                    or LifecycleState.LimitReached))
            {
                throw new InvalidOperationException("Targeted preparation requires a terminal completed source run.");
            }
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            Execute(
                """
                INSERT INTO snapshot_capture_operations(
                    operation_id,durable_command_id,request_json,request_sha256,initiation_kind,dispatch_deadline,
                    lifecycle_state,lifecycle_generation,coordinator_fencing_epoch,installation_snapshot_id,payload_id,
                    created_at,updated_at)
                VALUES($operation,$captureCommand,$captureJson,$captureSha,$initiation,$deadline,
                    'Queued',0,$epoch,NULL,NULL,$now,$now);
                """, transaction,
                ("$operation", request.CaptureOperationId),
                ("$captureCommand", "targeted-capture-" + request.DurableCommandId),
                ("$captureJson", request.CaptureRequestJson), ("$captureSha", request.CaptureRequestSha256),
                ("$initiation", request.InitiationKind), ("$deadline", ToText(request.DispatchDeadline)),
                ("$epoch", coordinatorFencingEpoch), ("$now", ToText(now)));
            Execute(
                """
                INSERT INTO targeted_preparation_requests(
                    preparation_id,durable_command_id,user_gesture_id,request_sha256,request_json,source_run_id,
                    source_occurrence_kind,source_occurrence_id,confirmed_profile_id,confirmed_profile_revision,
                    saved_configuration_id,saved_configuration_revision,analysis_context_id,analysis_context_revision,
                    analysis_context_fingerprint,capture_operation_id,initiation_kind,dispatch_deadline,created_at)
                VALUES($preparation,$command,$gesture,$sha,$json,$source,$kind,$occurrence,$profile,$profileRevision,
                    $configuration,$configurationRevision,$context,$contextRevision,$contextFingerprint,$operation,$initiation,$deadline,$now);
                """, transaction,
                ("$preparation", request.PreparationId), ("$command", request.DurableCommandId),
                ("$gesture", request.UserGestureId), ("$sha", request.RequestSha256), ("$json", request.RequestJson),
                ("$source", request.SourceRunId), ("$kind", request.SourceOccurrenceKind),
                ("$occurrence", request.SourceOccurrenceId), ("$profile", request.ConfirmedProfileId),
                ("$profileRevision", request.ConfirmedProfileRevision), ("$configuration", request.SavedConfigurationId),
                ("$configurationRevision", request.SavedConfigurationRevision), ("$context", request.AnalysisContextId),
                ("$contextRevision", request.AnalysisContextRevision),
                ("$contextFingerprint", request.AnalysisContextFingerprint), ("$operation", request.CaptureOperationId),
                ("$initiation", request.InitiationKind),
                ("$deadline", ToText(request.DispatchDeadline)), ("$now", ToText(now)));
            string fingerprint = Hash(string.Join('\n', request.RequestSha256, request.CaptureRequestSha256,
                request.PreparationId, request.SourceRunId, request.SourceOccurrenceKind, request.SourceOccurrenceId));
            string eventId = request.PreparationId + "-event-1";
            string eventJson = JsonSerializer.Serialize(new { state = "CapturingSnapshot", captureOperationId = request.CaptureOperationId });
            string projectionJson = PreparationProjectionJson(1, TargetedVerificationPreparationState.CapturingSnapshot,
                fingerprint, string.Empty, request.CaptureOperationId, null, null, null, null, false, false, eventId, now);
            Execute(
                """
                INSERT INTO targeted_preparation_events(event_id,preparation_id,revision,event_kind,event_sha256,event_json,projection_json,created_at)
                VALUES($event,$preparation,1,'admitted',$eventSha,$eventJson,$projectionJson,$now);
                INSERT INTO targeted_preparation_projection(
                    preparation_id,revision,lifecycle_state,preparation_fingerprint,terminal_reason,capture_operation_id,
                    target_snapshot_id,evidence_acquisition_id,plan_id,plan_fingerprint,startable,limited,last_event_id,updated_at)
                VALUES($preparation,1,'CapturingSnapshot',$fingerprint,'',$operation,NULL,NULL,NULL,NULL,0,0,$event,$now);
                """, transaction,
                ("$event", eventId), ("$preparation", request.PreparationId),
                ("$eventSha", TargetedEventHash(eventJson, projectionJson)),
                ("$eventJson", eventJson), ("$projectionJson", projectionJson), ("$now", ToText(now)), ("$fingerprint", fingerprint),
                ("$operation", request.CaptureOperationId));
            InsertAuditEvent("targeted-preparation-admitted", "targeted-preparation", request.PreparationId, now, transaction);
            transaction.Commit();
            return GetTargetedPreparationCore(request.PreparationId);
        }
    }

    public TargetedPreparationPersistenceRecord GetTargetedPreparation(string preparationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preparationId);
        lock (gate)
        {
            return GetTargetedPreparationCore(preparationId);
        }
    }

    public TargetedPreparationPersistenceRecord? FindTargetedPreparationByCommand(string commandId)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT preparation_id FROM targeted_preparation_requests WHERE durable_command_id=$command;";
            command.Parameters.AddWithValue("$command", commandId);
            return command.ExecuteScalar() is string id ? GetTargetedPreparationCore(id) : null;
        }
    }

    public IReadOnlyList<TargetedPreparationPersistenceRecord> GetDispatchableTargetedPreparations()
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT preparation_id FROM targeted_preparation_projection WHERE lifecycle_state IN "
                + "('CapturingSnapshot','AcquiringEvidence','PreparingPlan','Cancelling') ORDER BY updated_at,preparation_id LIMIT 64;";
            using SqliteDataReader reader = command.ExecuteReader();
            List<string> ids = [];
            while (reader.Read()) ids.Add(reader.GetString(0));
            return ids.Select(id => GetTargetedPreparationCore(id)).ToArray();
        }
    }

    public TargetedPreparationPersistenceRecord TransitionTargetedPreparation(
        string preparationId,
        long expectedRevision,
        TargetedVerificationPreparationState expectedState,
        TargetedVerificationPreparationState nextState,
        string eventKind,
        string eventJson,
        string terminalReason,
        string? targetSnapshotId,
        string? evidenceAcquisitionId,
        string? planId,
        string? planFingerprint,
        bool startable,
        bool limited,
        DateTimeOffset now)
    {
        if (Encoding.UTF8.GetByteCount(eventJson) > 64 * 1024 || terminalReason.Length > 4096)
            throw new InvalidDataException("The targeted preparation event exceeds its bound.");
        if (planFingerprint is not null) ValidateSha256(planFingerprint);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            TargetedPreparationPersistenceRecord current = GetTargetedPreparationCore(preparationId, transaction);
            if (current.Revision != expectedRevision || current.State != expectedState)
                throw new InvalidOperationException("The targeted preparation compare-and-swap lost its race.");
            long revision = checked(expectedRevision + 1);
            string eventId = preparationId + "-event-" + revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string fingerprint = Hash(string.Join('\n', current.PreparationFingerprint, revision, nextState,
                Hash(eventJson), targetSnapshotId ?? current.TargetSnapshotId ?? "none",
                evidenceAcquisitionId ?? current.EvidenceAcquisitionId ?? "none", planFingerprint ?? "none"));
            string projectionJson = PreparationProjectionJson(revision, nextState, fingerprint, terminalReason,
                current.CaptureOperationId, targetSnapshotId ?? current.TargetSnapshotId,
                evidenceAcquisitionId ?? current.EvidenceAcquisitionId, planId ?? current.PlanId,
                planFingerprint ?? current.PlanFingerprint, startable, limited, eventId, now);
            Execute(
                "INSERT INTO targeted_preparation_events(event_id,preparation_id,revision,event_kind,event_sha256,event_json,projection_json,created_at) "
                + "VALUES($event,$preparation,$revision,$kind,$sha,$json,$projectionJson,$now);", transaction,
                ("$event", eventId), ("$preparation", preparationId), ("$revision", revision), ("$kind", eventKind),
                ("$sha", TargetedEventHash(eventJson, projectionJson)), ("$json", eventJson),
                ("$projectionJson", projectionJson), ("$now", ToText(now)));
            int changed = Execute(
                """
                UPDATE targeted_preparation_projection
                SET revision=$revision,lifecycle_state=$state,preparation_fingerprint=$fingerprint,
                    terminal_reason=$reason,target_snapshot_id=COALESCE($snapshot,target_snapshot_id),
                    evidence_acquisition_id=COALESCE($acquisition,evidence_acquisition_id),
                    plan_id=COALESCE($plan,plan_id),plan_fingerprint=COALESCE($planFingerprint,plan_fingerprint),
                    startable=$startable,limited=$limited,last_event_id=$event,updated_at=$now
                WHERE preparation_id=$preparation AND revision=$expectedRevision AND lifecycle_state=$expectedState;
                """, transaction,
                ("$revision", revision), ("$state", nextState.ToString()), ("$fingerprint", fingerprint),
                ("$reason", terminalReason), ("$snapshot", targetSnapshotId), ("$acquisition", evidenceAcquisitionId),
                ("$plan", planId), ("$planFingerprint", planFingerprint), ("$startable", startable ? 1 : 0),
                ("$limited", limited ? 1 : 0), ("$event", eventId), ("$now", ToText(now)),
                ("$preparation", preparationId), ("$expectedRevision", expectedRevision),
                ("$expectedState", expectedState.ToString()));
            if (changed != 1) throw new InvalidOperationException("The targeted preparation compare-and-swap lost its race.");
            transaction.Commit();
            return GetTargetedPreparationCore(preparationId);
        }
    }

    public void RecordTargetedSnapshotLink(
        string preparationId, string captureOperationId, string targetSnapshotId,
        string snapshotFingerprint, string sourceStructuralFingerprint,
        string targetStructuralFingerprint, long confirmedProfileRevision, DateTimeOffset now)
    {
        ValidateSha256(snapshotFingerprint);
        ValidateSha256(sourceStructuralFingerprint);
        ValidateSha256(targetStructuralFingerprint);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            Execute(
                """
                INSERT OR IGNORE INTO targeted_snapshot_links(
                    link_id,preparation_id,capture_operation_id,target_snapshot_id,snapshot_payload_id,
                    snapshot_fingerprint,source_structural_fingerprint,target_structural_fingerprint,
                    structural_comparison,confirmed_profile_revision,created_at)
                SELECT $link,$preparation,$capture,$snapshot,payload_id,$fingerprint,$sourceStructural,
                    $targetStructural,$comparison,$profileRevision,$now
                FROM snapshot_capture_publications WHERE installation_snapshot_id=$snapshot;
                """, transaction,
                ("$link", preparationId + "-target-snapshot"), ("$preparation", preparationId),
                ("$capture", captureOperationId), ("$snapshot", targetSnapshotId),
                ("$fingerprint", snapshotFingerprint), ("$sourceStructural", sourceStructuralFingerprint),
                ("$targetStructural", targetStructuralFingerprint),
                ("$comparison", sourceStructuralFingerprint == targetStructuralFingerprint ? "equivalent" : "changed"),
                ("$profileRevision", confirmedProfileRevision), ("$now", ToText(now)));
            if (ScalarLong("SELECT COUNT(*) FROM targeted_snapshot_links WHERE preparation_id=$preparation AND target_snapshot_id=$snapshot;",
                    transaction, ("$preparation", preparationId), ("$snapshot", targetSnapshotId)) != 1)
            {
                throw new InvalidOperationException("The targeted snapshot publication link could not be established.");
            }
            transaction.Commit();
        }
    }

    public void ValidateTargetedSnapshotLink(
        string preparationId,
        string captureOperationId,
        string targetSnapshotId,
        string snapshotFingerprint)
    {
        ValidateSha256(snapshotFingerprint);
        lock (gate)
        {
            if (ScalarLong(
                    "SELECT COUNT(*) FROM targeted_snapshot_links link "
                    + "JOIN snapshot_capture_operations capture "
                    + "ON capture.operation_id=link.capture_operation_id "
                    + "AND capture.installation_snapshot_id=link.target_snapshot_id "
                    + "AND capture.payload_id=link.snapshot_payload_id "
                    + "AND capture.lifecycle_state='Completed' "
                    + "JOIN snapshot_capture_publications publication "
                    + "ON publication.operation_id=link.capture_operation_id "
                    + "AND publication.installation_snapshot_id=link.target_snapshot_id "
                    + "AND publication.payload_id=link.snapshot_payload_id "
                    + "JOIN payloads payload ON payload.payload_id=link.snapshot_payload_id "
                    + "AND payload.content_sha256=link.snapshot_fingerprint "
                    + "WHERE link.preparation_id=$preparation "
                    + "AND link.capture_operation_id=$capture AND link.target_snapshot_id=$snapshot "
                    + "AND link.snapshot_fingerprint=$fingerprint;",
                    null,
                    ("$preparation", preparationId),
                    ("$capture", captureOperationId),
                    ("$snapshot", targetSnapshotId),
                    ("$fingerprint", snapshotFingerprint)) != 1)
            {
                throw new InvalidOperationException(
                    "The targeted snapshot capture occurrence or retained fingerprint drifted.");
            }
        }
    }

    public TargetedCancellationPersistenceReceipt CancelTargetedPreparation(
        string commandId, string preparationId, long expectedRevision, string userGestureId,
        long coordinatorFencingEpoch, DateTimeOffset now)
    {
        ValidateSetupIdentity(commandId, nameof(commandId));
        ValidateSetupIdentity(userGestureId, nameof(userGestureId));
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        string requestSha = Hash(string.Join('\n', "targeted-preparation-cancel/v1", commandId,
            preparationId, expectedRevision, userGestureId));
        lock (gate)
        {
            using SqliteTransaction transaction = BeginImmediateTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            string? existingPreparation = ScalarStringOrNull(
                "SELECT preparation_id FROM targeted_preparation_commands WHERE command_id=$command;",
                transaction, ("$command", commandId));
            if (existingPreparation is not null)
            {
                if (existingPreparation != preparationId
                    || ScalarLong("SELECT COUNT(*) FROM targeted_preparation_commands WHERE command_id=$command AND expected_revision=$revision AND user_gesture_id=$gesture AND request_sha256=$sha;",
                        transaction, ("$command", commandId), ("$revision", expectedRevision),
                        ("$gesture", userGestureId), ("$sha", requestSha)) != 1)
                {
                    throw new InvalidOperationException("A targeted cancellation idempotency key cannot be rebound.");
                }
                transaction.Commit();
                return new(GetTargetedPreparationCore(preparationId), true);
            }
            EnsureAuthorityGestureUnused(userGestureId, transaction);
            TargetedPreparationPersistenceRecord current = GetTargetedPreparationCore(preparationId, transaction);
            if (current.Revision != expectedRevision)
            {
                throw new InvalidOperationException("The targeted preparation revision changed before cancellation.");
            }
            if (current.State is not (TargetedVerificationPreparationState.Ready
                    or TargetedVerificationPreparationState.ReadyWithGaps
                    or TargetedVerificationPreparationState.CapturingSnapshot
                    or TargetedVerificationPreparationState.AcquiringEvidence
                    or TargetedVerificationPreparationState.PreparingPlan))
            {
                throw new InvalidOperationException("The targeted preparation is not cancellable in its current state.");
            }
            long revision = checked(current.Revision + 1);
            string eventId = preparationId + "-event-" + revision;
            string eventJson = JsonSerializer.Serialize(new { commandId, userGestureId });
            string fingerprint = Hash(string.Join('\n', current.PreparationFingerprint, revision,
                TargetedVerificationPreparationState.Cancelled, Hash(eventJson)));
            string projectionJson = PreparationProjectionJson(revision,
                TargetedVerificationPreparationState.Cancelled, fingerprint,
                "Cancelled by an explicit durable user gesture.", current.CaptureOperationId,
                current.TargetSnapshotId, current.EvidenceAcquisitionId, current.PlanId, current.PlanFingerprint,
                false, current.Limited, eventId, now);
            Execute(
                "INSERT INTO targeted_preparation_events(event_id,preparation_id,revision,event_kind,event_sha256,event_json,projection_json,created_at) "
                + "VALUES($event,$preparation,$revision,'cancelled',$eventSha,$eventJson,$projectionJson,$now); "
                + "INSERT INTO targeted_preparation_commands(command_id,preparation_id,command_kind,expected_revision,user_gesture_id,request_sha256,resulting_revision,created_at) "
                + "VALUES($command,$preparation,'cancel',$expectedRevision,$gesture,$requestSha,$revision,$now);",
                transaction, ("$event", eventId), ("$preparation", preparationId), ("$revision", revision),
                ("$eventSha", TargetedEventHash(eventJson, projectionJson)),
                ("$eventJson", eventJson), ("$projectionJson", projectionJson),
                ("$now", ToText(now)),
                ("$command", commandId), ("$expectedRevision", expectedRevision), ("$gesture", userGestureId),
                ("$requestSha", requestSha));
            int changed = Execute(
                "UPDATE targeted_preparation_projection SET revision=$revision,lifecycle_state='Cancelled',"
                + "preparation_fingerprint=$fingerprint,terminal_reason=$reason,startable=0,last_event_id=$event,updated_at=$now "
                + "WHERE preparation_id=$preparation AND revision=$expectedRevision AND lifecycle_state IN "
                + "('CapturingSnapshot','AcquiringEvidence','PreparingPlan','Ready','ReadyWithGaps');",
                transaction, ("$revision", revision), ("$fingerprint", fingerprint),
                ("$reason", "Cancelled by an explicit durable user gesture."), ("$event", eventId),
                ("$now", ToText(now)), ("$preparation", preparationId), ("$expectedRevision", expectedRevision));
            if (changed != 1)
            {
                throw new InvalidOperationException("The targeted cancellation lost its compare-and-swap race.");
            }
            if (current.EvidenceAcquisitionId is not null)
            {
                SemanticAcquisitionPersistenceRecord acquisition = GetSemanticAcquisitionCore(
                    current.EvidenceAcquisitionId, transaction);
                if (acquisition.State is "Queued" or "Retrying" or "Running" or "Cancelling")
                {
                    long acquisitionSequence = checked(acquisition.DurableSequence + 1);
                    string acquisitionReason = "Cancelled with its owning targeted preparation.";
                    string acquisitionProjectionJson = SemanticProjectionJson(
                        "Cancelled", acquisition.Generation, acquisitionSequence,
                        acquisition.ProgressCompleted, acquisition.ProgressDenominator,
                        null, acquisitionReason, now);
                    string acquisitionEventJson = JsonSerializer.Serialize(new
                    {
                        preparationCancellationCommandId = commandId,
                        preparationId,
                        projectionSha256 = Hash(acquisitionProjectionJson),
                    });
                    Execute(
                        "INSERT INTO semantic_acquisition_commands(command_id,acquisition_id,command_kind,expected_generation,user_gesture_id,request_sha256,created_at) "
                        + "VALUES($acquisitionCommand,$acquisition,'cancel',$generation,$gesture,$acquisitionRequestSha,$now); "
                        + "INSERT INTO semantic_acquisition_events(event_id,acquisition_id,sequence,event_kind,generation,coordinator_fencing_epoch,event_json,projection_json,created_at) "
                        + "VALUES($acquisitionEvent,$acquisition,$sequence,'cancelled',$generation,$epoch,$acquisitionEventJson,$acquisitionProjectionJson,$now);",
                        transaction,
                        ("$acquisitionCommand", commandId + "-semantic-acquisition"),
                        ("$acquisition", acquisition.AcquisitionId),
                        ("$generation", acquisition.Generation),
                        ("$gesture", userGestureId),
                        ("$acquisitionRequestSha", Hash(string.Join('\n',
                            "semantic-acquisition-cancel/v1", commandId, acquisition.AcquisitionId,
                            acquisition.Generation, userGestureId))),
                        ("$acquisitionEvent", acquisition.AcquisitionId + "-event-" + acquisitionSequence),
                        ("$sequence", acquisitionSequence),
                        ("$epoch", coordinatorFencingEpoch),
                        ("$acquisitionEventJson", acquisitionEventJson),
                        ("$acquisitionProjectionJson", acquisitionProjectionJson),
                        ("$now", ToText(now)));
                    int acquisitionChanged = Execute(
                        "UPDATE semantic_acquisition_projection SET lifecycle_state='Cancelled',"
                        + "durable_sequence=$sequence,active_attempt_id=NULL,terminal_reason=$reason,updated_at=$now "
                        + "WHERE acquisition_id=$acquisition AND generation=$generation "
                        + "AND lifecycle_state IN ('Queued','Retrying','Running','Cancelling');",
                        transaction,
                        ("$sequence", acquisitionSequence),
                        ("$reason", acquisitionReason),
                        ("$now", ToText(now)),
                        ("$acquisition", acquisition.AcquisitionId),
                        ("$generation", acquisition.Generation));
                    if (acquisitionChanged != 1)
                    {
                        throw new InvalidOperationException(
                            "The targeted semantic acquisition cancellation lost its fencing race.");
                    }
                }
            }
            transaction.Commit();
            return new(GetTargetedPreparationCore(preparationId), false);
        }
    }

    private void EnsureAuthorityGestureUnused(string userGestureId, SqliteTransaction transaction)
    {
        long uses = ScalarLong(
            """
            SELECT COUNT(*) FROM (
                SELECT user_gesture_id FROM targeted_preparation_requests WHERE user_gesture_id=$gesture
                UNION ALL
                SELECT user_gesture_id FROM targeted_preparation_commands WHERE user_gesture_id=$gesture
                UNION ALL
                SELECT user_gesture_id FROM prepared_run_submissions WHERE user_gesture_id=$gesture
                UNION ALL
                SELECT user_gesture_id FROM targeted_start_admissions WHERE user_gesture_id=$gesture
            );
            """,
            transaction,
            ("$gesture", userGestureId));
        if (uses != 0)
        {
            throw new InvalidOperationException(
                "A one-shot user gesture identity cannot authorize more than one authority-bearing command.");
        }
    }

    public SemanticAcquisitionPersistenceRecord CreateSemanticAcquisition(
        string acquisitionId, string preparationId, string targetSnapshotId,
        string requestJson, string requestSha256, string sealedInputFingerprint,
        DateTimeOffset dispatchDeadline, long coordinatorFencingEpoch, DateTimeOffset now)
    {
        ValidateSha256(requestSha256);
        ValidateSha256(sealedInputFingerprint);
        if (Hash(requestJson) != requestSha256)
        {
            throw new InvalidDataException("The semantic acquisition request fingerprint does not match its bytes.");
        }
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        if (dispatchDeadline <= now || Encoding.UTF8.GetByteCount(requestJson) > 64 * 1024)
            throw new InvalidDataException("The semantic acquisition request exceeds its bound.");
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            string? existing = ScalarStringOrNull(
                "SELECT acquisition_id FROM semantic_acquisition_runs WHERE preparation_id=$preparation;",
                transaction, ("$preparation", preparationId));
            if (existing is not null)
            {
                SemanticAcquisitionPersistenceRecord replay = GetSemanticAcquisitionCore(existing, transaction);
                if (replay.RequestSha256 != requestSha256 || replay.TargetSnapshotId != targetSnapshotId)
                    throw new InvalidOperationException("A semantic acquisition preparation link cannot be rebound.");
                transaction.Commit();
                return replay;
            }
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            string projectionJson = SemanticProjectionJson("Queued", 0, 1, 0, 1, null, string.Empty, now);
            string eventJson = JsonSerializer.Serialize(new
            {
                projectionSha256 = Hash(projectionJson),
            });
            Execute(
                """
                INSERT INTO semantic_acquisition_runs(
                    acquisition_id,preparation_id,target_snapshot_id,operation_kind,request_sha256,request_json,
                    producer_id,producer_version,support_manifest_id,enumeration_policy_id,enumeration_policy_version,
                    sealed_input_fingerprint,dispatch_deadline,created_at)
                VALUES($acquisition,$preparation,$snapshot,'bethesda-semantic-extraction',$sha,$json,
                    'infinium.bethesda.semantic-index','2.0.0','bethesda-semantic-support-v2',
                    'qualified-bethesda-enumeration','1.0.0',$sealed,$deadline,$now);
                INSERT INTO semantic_acquisition_jobs(job_id,acquisition_id,job_kind,created_at)
                VALUES($job,$acquisition,'semantic-extraction-root',$now);
                INSERT INTO semantic_acquisition_commands(
                    command_id,acquisition_id,command_kind,expected_generation,user_gesture_id,request_sha256,created_at)
                VALUES($command,$acquisition,'start',0,NULL,$sha,$now);
                INSERT INTO semantic_acquisition_events(
                    event_id,acquisition_id,sequence,event_kind,generation,coordinator_fencing_epoch,event_json,projection_json,created_at)
                VALUES($event,$acquisition,1,'admitted',0,$epoch,$eventJson,$projectionJson,$now);
                INSERT INTO semantic_acquisition_projection(
                    acquisition_id,lifecycle_state,generation,durable_sequence,progress_completed,progress_denominator,
                    active_attempt_id,terminal_reason,updated_at)
                VALUES($acquisition,'Queued',0,1,0,1,NULL,'',$now);
                """, transaction,
                ("$acquisition", acquisitionId), ("$preparation", preparationId), ("$snapshot", targetSnapshotId),
                ("$sha", requestSha256), ("$json", requestJson), ("$sealed", sealedInputFingerprint),
                ("$deadline", ToText(dispatchDeadline)), ("$now", ToText(now)),
                ("$job", acquisitionId + "-root"), ("$command", acquisitionId + "-start"),
                ("$event", acquisitionId + "-event-1"), ("$epoch", coordinatorFencingEpoch),
                ("$eventJson", eventJson),
                ("$projectionJson", projectionJson));
            transaction.Commit();
            return GetSemanticAcquisitionCore(acquisitionId);
        }
    }

    public SemanticAcquisitionPersistenceRecord GetSemanticAcquisition(string acquisitionId)
    {
        lock (gate) return GetSemanticAcquisitionCore(acquisitionId);
    }

    public int RecoverInterruptedSemanticAcquisitions(long coordinatorFencingEpoch, DateTimeOffset now)
    {
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "SELECT acquisition_id,generation,durable_sequence,active_attempt_id FROM semantic_acquisition_projection "
                + "WHERE lifecycle_state='Running' ORDER BY acquisition_id;";
            using SqliteDataReader reader = command.ExecuteReader();
            List<(string AcquisitionId, long Generation, long Sequence, string AttemptId)> interrupted = [];
            while (reader.Read())
            {
                interrupted.Add((reader.GetString(0), reader.GetInt64(1), reader.GetInt64(2), reader.GetString(3)));
            }
            reader.Close();
            foreach ((string acquisitionId, long generation, long sequence, string attemptId) in interrupted)
            {
                long nextGeneration = checked(generation + 1);
                long nextSequence = checked(sequence + 1);
                string requestSha = Hash(string.Join('\n', "semantic-acquisition-recover/v1", acquisitionId,
                    generation, attemptId, coordinatorFencingEpoch));
                string projectionJson = SemanticProjectionJson("Retrying", nextGeneration, nextSequence,
                    0, 1, null, string.Empty, now);
                string eventJson = JsonSerializer.Serialize(new
                {
                    interruptedAttemptId = attemptId,
                    projectionSha256 = Hash(projectionJson),
                });
                Execute(
                    "INSERT INTO semantic_acquisition_commands(command_id,acquisition_id,command_kind,expected_generation,user_gesture_id,request_sha256,created_at) "
                    + "VALUES($command,$acquisition,'recover',$generation,NULL,$sha,$now); "
                    + "INSERT INTO semantic_acquisition_events(event_id,acquisition_id,sequence,event_kind,generation,coordinator_fencing_epoch,event_json,projection_json,created_at) "
                    + "VALUES($event,$acquisition,$sequence,'recovered',$nextGeneration,$epoch,$json,$projectionJson,$now);",
                    transaction, ("$command", acquisitionId + "-recover-" + nextGeneration),
                    ("$acquisition", acquisitionId), ("$generation", generation), ("$sha", requestSha),
                    ("$now", ToText(now)), ("$event", acquisitionId + "-event-" + nextSequence),
                    ("$sequence", nextSequence), ("$nextGeneration", nextGeneration),
                    ("$epoch", coordinatorFencingEpoch),
                    ("$json", eventJson),
                    ("$projectionJson", projectionJson));
                int changed = Execute(
                    "UPDATE semantic_acquisition_projection SET lifecycle_state='Retrying',generation=$nextGeneration,"
                    + "durable_sequence=$sequence,active_attempt_id=NULL,terminal_reason='',updated_at=$now "
                    + "WHERE acquisition_id=$acquisition AND lifecycle_state='Running' AND generation=$generation AND active_attempt_id=$attempt;",
                    transaction, ("$nextGeneration", nextGeneration), ("$sequence", nextSequence),
                    ("$now", ToText(now)), ("$acquisition", acquisitionId), ("$generation", generation),
                    ("$attempt", attemptId));
                if (changed != 1)
                {
                    throw new InvalidOperationException("Interrupted semantic acquisition recovery lost its fencing race.");
                }
            }
            transaction.Commit();
            return interrupted.Count;
        }
    }

    public SemanticAcquisitionAttemptRecord DispatchSemanticAcquisition(
        string acquisitionId, long expectedGeneration, long coordinatorFencingEpoch,
        TimeSpan leaseDuration, DateTimeOffset now)
    {
        RequirePositive(coordinatorFencingEpoch, nameof(coordinatorFencingEpoch));
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(coordinatorFencingEpoch, transaction);
            SemanticAcquisitionPersistenceRecord current = GetSemanticAcquisitionCore(acquisitionId, transaction);
            if (current.State is not ("Queued" or "Retrying") || current.Generation != expectedGeneration
                || current.DispatchDeadline <= now)
                throw new InvalidOperationException("The semantic acquisition is not dispatchable at this generation.");
            long attemptGeneration = ScalarLong(
                "SELECT COALESCE(MAX(attempt_generation),0)+1 FROM semantic_acquisition_attempts WHERE acquisition_id=$acquisition;",
                transaction, ("$acquisition", acquisitionId));
            long token = ScalarLong(
                "SELECT COALESCE(MAX(attempt_fencing_token),0)+1 FROM semantic_acquisition_attempts WHERE acquisition_id=$acquisition;",
                transaction, ("$acquisition", acquisitionId));
            string attemptId = Guid.NewGuid().ToString("N");
            DateTimeOffset expires = now.Add(leaseDuration);
            Execute(
                "INSERT INTO semantic_acquisition_attempts(attempt_id,acquisition_id,attempt_generation,coordinator_fencing_epoch,attempt_fencing_token,sealed_input_fingerprint,lease_expires_at,created_at) "
                + "VALUES($attempt,$acquisition,$attemptGeneration,$epoch,$token,$sealed,$expires,$now);", transaction,
                ("$attempt", attemptId), ("$acquisition", acquisitionId), ("$attemptGeneration", attemptGeneration),
                ("$epoch", coordinatorFencingEpoch), ("$token", token), ("$sealed", current.SealedInputFingerprint),
                ("$expires", ToText(expires)), ("$now", ToText(now)));
            long sequence = checked(current.DurableSequence + 1);
            string projectionJson = SemanticProjectionJson("Running", current.Generation, sequence,
                current.ProgressCompleted, current.ProgressDenominator, attemptId, string.Empty, now);
            string eventJson = JsonSerializer.Serialize(new
            {
                attemptId,
                token,
                expires,
                projectionSha256 = Hash(projectionJson),
            });
            Execute(
                "INSERT INTO semantic_acquisition_events(event_id,acquisition_id,sequence,event_kind,generation,coordinator_fencing_epoch,event_json,projection_json,created_at) "
                + "VALUES($event,$acquisition,$sequence,'dispatched',$generation,$epoch,$json,$projectionJson,$now);", transaction,
                ("$event", acquisitionId + "-event-" + sequence), ("$acquisition", acquisitionId),
                ("$sequence", sequence), ("$generation", expectedGeneration), ("$epoch", coordinatorFencingEpoch),
                ("$json", eventJson),
                ("$projectionJson", projectionJson), ("$now", ToText(now)));
            int changed = Execute(
                "UPDATE semantic_acquisition_projection SET lifecycle_state='Running',durable_sequence=$sequence,active_attempt_id=$attempt,updated_at=$now "
                + "WHERE acquisition_id=$acquisition AND lifecycle_state IN ('Queued','Retrying') AND generation=$generation;",
                transaction, ("$sequence", sequence), ("$attempt", attemptId), ("$now", ToText(now)),
                ("$acquisition", acquisitionId), ("$generation", expectedGeneration));
            if (changed != 1) throw new InvalidOperationException("The semantic acquisition dispatch lost its race.");
            transaction.Commit();
            return new(attemptId, acquisitionId, attemptGeneration, coordinatorFencingEpoch, token,
                current.SealedInputFingerprint, expires, now);
        }
    }

    public byte[] ReadSemanticAcquisitionStagedPayload(SemanticAcquisitionAttemptRecord attempt,
        string stagedRelativePath, string expectedSha256, long expectedByteLength, long maximumBytes)
    {
        ValidateSha256(expectedSha256);
        if (expectedByteLength is < 1 || maximumBytes <= 0 || expectedByteLength > maximumBytes
            || maximumBytes > 64L * 1024 * 1024)
            throw new InvalidOperationException("The staged semantic acquisition payload exceeds its bound.");
        EnsureCurrentSemanticAcquisitionAttempt(attempt);
        using WindowsHandleRelativeStorage.AdmissionSource source = Paths.OpenAdmissionSource(
            ProductWriteClass.AttemptStaging, Path.Combine(attempt.AttemptId, stagedRelativePath));
        using MemoryStream buffer = new(checked((int)expectedByteLength));
        WindowsHandleRelativeStorage.AdmissionCopyResult observed = source.CopyToAndHash(buffer, maximumBytes);
        if (observed.ByteLength != expectedByteLength || observed.Sha256 != expectedSha256)
            throw new InvalidOperationException("The staged semantic acquisition bytes differ from the worker receipt.");
        return buffer.ToArray();
    }

    public SemanticAcquisitionPublicationRecord PublishSemanticAcquisition(
        SemanticAcquisitionAttemptRecord attempt, string stagedRelativePath, string expectedSha256,
        long expectedByteLength, string expectedManifestSha256, long maximumBytes,
        string semanticOutputId, string provenanceJson, DateTimeOffset now)
    {
        ValidateSha256(expectedSha256);
        ValidateSha256(expectedManifestSha256);
        byte[] admittedBytes = ReadSemanticAcquisitionStagedPayload(
            attempt, stagedRelativePath, expectedSha256, expectedByteLength, maximumBytes);
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentCoordinatorEpoch(attempt.CoordinatorFencingEpoch, transaction);
            EnsureCurrentSemanticAcquisitionAttempt(attempt, transaction);
            string payloadId = AdmitCoordinatorPayload(
                admittedBytes, "semantic-acquisition-output", semanticOutputId, now, transaction);
            string publicationId = attempt.AcquisitionId + "-publication";
            Execute(
                "INSERT INTO semantic_acquisition_publications(publication_id,acquisition_id,attempt_id,attempt_fencing_token,target_snapshot_id,semantic_output_id,payload_id,payload_sha256,payload_byte_length,staged_manifest_sha256,provenance_json,created_at) "
                + "SELECT $publication,$acquisition,$attempt,$token,target_snapshot_id,$output,$payload,$sha,$length,$manifest,$provenance,$now FROM semantic_acquisition_runs WHERE acquisition_id=$acquisition; "
                + "INSERT INTO semantic_acquisition_application_links(link_id,acquisition_id,preparation_id,successor_run_id,semantic_output_id,use_kind,created_at) "
                + "SELECT $applicationLink,r.acquisition_id,r.preparation_id,NULL,$output,'preparation-prerequisite',$now "
                + "FROM semantic_acquisition_runs r WHERE r.acquisition_id=$acquisition;",
                transaction, ("$publication", publicationId), ("$acquisition", attempt.AcquisitionId),
                ("$attempt", attempt.AttemptId), ("$token", attempt.AttemptFencingToken), ("$output", semanticOutputId),
                ("$payload", payloadId), ("$sha", expectedSha256), ("$length", expectedByteLength),
                ("$manifest", expectedManifestSha256),
                ("$provenance", provenanceJson), ("$now", ToText(now)),
                ("$applicationLink", attempt.AcquisitionId + "-preparation-link"));
            SemanticAcquisitionPersistenceRecord current = GetSemanticAcquisitionCore(attempt.AcquisitionId, transaction);
            long sequence = checked(current.DurableSequence + 1);
            string projectionJson = SemanticProjectionJson("Completed", current.Generation, sequence,
                1, 1, null, string.Empty, now);
            string eventJson = JsonSerializer.Serialize(new
            {
                publicationId,
                semanticOutputId,
                payloadId,
                sha256 = expectedSha256,
                projectionSha256 = Hash(projectionJson),
            });
            string checkpointId = attempt.AcquisitionId + "-checkpoint-" + sequence;
            string checkpointJson = JsonSerializer.Serialize(new
            {
                attempt.AcquisitionId,
                attempt.AttemptId,
                attempt.AttemptFencingToken,
                current.TargetSnapshotId,
                semanticOutputId,
                payloadId,
                expectedSha256,
                expectedByteLength,
                expectedManifestSha256,
            });
            Execute(
                "INSERT INTO semantic_acquisition_progress(progress_id,acquisition_id,attempt_id,completed,denominator,created_at) "
                + "VALUES($progress,$acquisition,$attempt,1,1,$now); "
                + "INSERT INTO semantic_acquisition_checkpoints(checkpoint_id,acquisition_id,attempt_id,attempt_fencing_token,target_snapshot_id,sealed_input_fingerprint,checkpoint_sha256,checkpoint_json,created_at) "
                + "VALUES($checkpoint,$acquisition,$attempt,$token,$snapshot,$sealed,$checkpointSha,$checkpointJson,$now); "
                + "INSERT INTO semantic_acquisition_events(event_id,acquisition_id,sequence,event_kind,generation,coordinator_fencing_epoch,event_json,projection_json,created_at) "
                + "VALUES($event,$acquisition,$sequence,'published',$generation,$epoch,$json,$projectionJson,$now);",
                transaction, ("$progress", attempt.AcquisitionId + "-progress-" + sequence),
                ("$acquisition", attempt.AcquisitionId), ("$attempt", attempt.AttemptId), ("$now", ToText(now)),
                ("$event", attempt.AcquisitionId + "-event-" + sequence), ("$sequence", sequence),
                ("$generation", current.Generation), ("$epoch", attempt.CoordinatorFencingEpoch),
                ("$checkpoint", checkpointId), ("$token", attempt.AttemptFencingToken),
                ("$snapshot", current.TargetSnapshotId), ("$sealed", attempt.SealedInputFingerprint),
                ("$checkpointSha", Hash(checkpointJson)), ("$checkpointJson", checkpointJson),
                ("$json", eventJson),
                ("$projectionJson", projectionJson));
            int changed = Execute(
                "UPDATE semantic_acquisition_projection SET lifecycle_state='Completed',durable_sequence=$sequence,progress_completed=1,progress_denominator=1,active_attempt_id=NULL,updated_at=$now "
                + "WHERE acquisition_id=$acquisition AND lifecycle_state='Running' AND active_attempt_id=$attempt;",
                transaction, ("$sequence", sequence), ("$now", ToText(now)),
                ("$acquisition", attempt.AcquisitionId), ("$attempt", attempt.AttemptId));
            if (changed != 1) throw new InvalidOperationException("A stale semantic acquisition attempt cannot publish.");
            transaction.Commit();
            using (WindowsHandleRelativeStorage.AdmissionSource staged = Paths.OpenAdmissionSource(
                       ProductWriteClass.AttemptStaging, Path.Combine(attempt.AttemptId, stagedRelativePath)))
            {
                staged.Delete();
            }
            return new(publicationId, attempt.AcquisitionId, attempt.AttemptId, attempt.AttemptFencingToken,
                current.TargetSnapshotId, semanticOutputId, payloadId, expectedSha256, expectedByteLength,
                expectedManifestSha256, provenanceJson, now);
        }
    }

    public void FailSemanticAcquisition(SemanticAcquisitionAttemptRecord attempt, string reason, DateTimeOffset now)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentSemanticAcquisitionAttempt(attempt, transaction, requireUnexpiredLease: false);
            SemanticAcquisitionPersistenceRecord current = GetSemanticAcquisitionCore(attempt.AcquisitionId, transaction);
            long sequence = checked(current.DurableSequence + 1);
            string projectionJson = SemanticProjectionJson("Failed", current.Generation, sequence,
                current.ProgressCompleted, current.ProgressDenominator, null, reason, now);
            string eventJson = JsonSerializer.Serialize(new
            {
                reason,
                projectionSha256 = Hash(projectionJson),
            });
            Execute("INSERT INTO semantic_acquisition_events(event_id,acquisition_id,sequence,event_kind,generation,coordinator_fencing_epoch,event_json,projection_json,created_at) "
                + "VALUES($event,$acquisition,$sequence,'failed',$generation,$epoch,$json,$projectionJson,$now);", transaction,
                ("$event", attempt.AcquisitionId + "-event-" + sequence), ("$acquisition", attempt.AcquisitionId),
                ("$sequence", sequence), ("$generation", current.Generation), ("$epoch", attempt.CoordinatorFencingEpoch),
                ("$json", eventJson), ("$projectionJson", projectionJson),
                ("$now", ToText(now)));
            int changed = Execute("UPDATE semantic_acquisition_projection SET lifecycle_state='Failed',durable_sequence=$sequence,active_attempt_id=NULL,terminal_reason=$reason,updated_at=$now "
                + "WHERE acquisition_id=$acquisition AND lifecycle_state='Running' AND active_attempt_id=$attempt;", transaction,
                ("$sequence", sequence), ("$reason", reason), ("$now", ToText(now)),
                ("$acquisition", attempt.AcquisitionId), ("$attempt", attempt.AttemptId));
            if (changed != 1)
            {
                throw new InvalidOperationException("A stale semantic acquisition attempt cannot record failure.");
            }
            transaction.Commit();
        }
    }

    public SemanticAcquisitionPublicationRecord GetSemanticAcquisitionPublication(string acquisitionId)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT publication_id,attempt_id,attempt_fencing_token,target_snapshot_id,semantic_output_id,payload_id,payload_sha256,payload_byte_length,staged_manifest_sha256,provenance_json,created_at "
                + "FROM semantic_acquisition_publications WHERE acquisition_id=$acquisition;";
            command.Parameters.AddWithValue("$acquisition", acquisitionId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read()) throw new KeyNotFoundException("The semantic acquisition has no publication.");
            SemanticAcquisitionPublicationRecord value = new(reader.GetString(0), acquisitionId, reader.GetString(1),
                reader.GetInt64(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6),
                reader.GetInt64(7), reader.GetString(8), reader.GetString(9), ParseTargetedTimestamp(reader.GetString(10)));
            if (reader.Read()) throw new InvalidOperationException("The semantic acquisition has multiple publications.");
            return value;
        }
    }

    public TargetedPreparationPersistenceRecord StoreTargetedPlan(
        TargetedVerificationPlanContract plan,
        IReadOnlyList<TargetedOperationInputPersistence> preparedInputs,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(preparedInputs);
        TargetedVerificationContractInvariants.Validate(plan);
        string[] requiredInputKinds =
        [
            "targeted-candidate-delivered-input",
            "targeted-correlation-coverage",
            "targeted-resolved-input-manifest",
        ];
        if (preparedInputs.Count != requiredInputKinds.Length
            || !preparedInputs.Select(item => item.InputKind).Order(StringComparer.Ordinal)
                .SequenceEqual(requiredInputKinds.Order(StringComparer.Ordinal))
            || preparedInputs.Select(item => (item.InputKind, item.InputId)).Distinct().Count()
                != preparedInputs.Count
            || preparedInputs.Any(item => item.Bytes.Length is < 1 or > 64 * 1024 * 1024)
            || preparedInputs.Single(item => item.InputKind == "targeted-candidate-delivered-input").InputId
                != plan.PreparedDeliveredInput.ArtifactId.Value
            || Hash(preparedInputs.Single(item => item.InputKind == "targeted-candidate-delivered-input").Bytes)
                != plan.PreparedDeliveredInput.Fingerprint.Value
            || preparedInputs.Single(item => item.InputKind == "targeted-correlation-coverage").InputId
                != plan.PreparedCoverageInput.ArtifactId.Value
            || Hash(preparedInputs.Single(item => item.InputKind == "targeted-correlation-coverage").Bytes)
                != plan.PreparedCoverageInput.Fingerprint.Value
            || preparedInputs.Single(item => item.InputKind == "targeted-resolved-input-manifest").InputId
                != plan.PreparedResolvedInputManifest.ArtifactId.Value
            || Hash(preparedInputs.Single(item => item.InputKind == "targeted-resolved-input-manifest").Bytes)
                != plan.PreparedResolvedInputManifest.Fingerprint.Value)
        {
            throw new InvalidDataException("The targeted plan does not bind its exact prepared operation inputs.");
        }
        byte[] planBytes = JsonSerializer.SerializeToUtf8Bytes(plan);
        if (planBytes.LongLength > 4L * 1024 * 1024) throw new InvalidDataException("The targeted plan exceeds its bound.");
        string preparationId = plan.PreparationId.Value;
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            TargetedPreparationPersistenceRecord current = GetTargetedPreparationCore(preparationId, transaction);
            if (current.State != TargetedVerificationPreparationState.PreparingPlan)
                throw new InvalidOperationException("The targeted preparation is not ready to publish a plan.");
            string payloadId = AdmitCoordinatorPayload(planBytes, "targeted-verification-plan", plan.PlanId.Value, now, transaction);
            foreach (TargetedOperationInputPersistence input in preparedInputs)
            {
                string inputPayloadId = AdmitCoordinatorPayload(
                    input.Bytes, input.InputKind, input.InputId, now, transaction);
                string inputSha = Hash(input.Bytes);
                string inputRowId = "targeted-input-" + Hash(string.Join('\n', preparationId,
                    input.InputKind, input.InputId, inputSha))[..32];
                Execute(
                    "INSERT INTO targeted_operation_inputs(input_row_id,preparation_id,input_kind,input_id,payload_id,input_sha256,created_at) "
                    + "VALUES($row,$preparation,$kind,$input,$payload,$sha,$now);",
                    transaction,
                    ("$row", inputRowId), ("$preparation", preparationId),
                    ("$kind", input.InputKind), ("$input", input.InputId),
                    ("$payload", inputPayloadId), ("$sha", inputSha), ("$now", ToText(now)));
            }
            foreach (TargetedScopeMemberContract root in plan.Scope.DirectRoots)
                Execute("INSERT INTO targeted_scope_roots(root_id,preparation_id,member_id,root_json,created_at) VALUES($id,$preparation,$member,$json,$now);",
                    transaction, ("$id", preparationId + "-root-" + Hash(root.MemberId.Value)[..16]),
                    ("$preparation", preparationId), ("$member", root.MemberId.Value),
                    ("$json", JsonSerializer.Serialize(root)), ("$now", ToText(now)));
            foreach (TargetedScopeMemberContract member in plan.Scope.Members)
                Execute("INSERT INTO targeted_scope_members(member_row_id,preparation_id,scope_id,member_id,member_kind,stable_identity,mandatory,member_json,created_at) "
                    + "VALUES($row,$preparation,$scope,$member,$kind,$stable,$mandatory,$json,$now);", transaction,
                    ("$row", preparationId + "-member-" + Hash(member.MemberId.Value)[..16]), ("$preparation", preparationId),
                    ("$scope", plan.Scope.ScopeId.Value), ("$member", member.MemberId.Value), ("$kind", member.Kind.ToString()),
                    ("$stable", member.StableIdentity.Value), ("$mandatory", member.Mandatory ? 1 : 0),
                    ("$json", JsonSerializer.Serialize(member)), ("$now", ToText(now)));
            foreach (TargetedScopeDependencyContract edge in plan.Scope.Dependencies)
                Execute("INSERT INTO targeted_scope_dependencies(edge_row_id,preparation_id,scope_id,edge_id,from_member_id,to_member_id,relation,edge_json,created_at) "
                    + "VALUES($row,$preparation,$scope,$edge,$from,$to,$relation,$json,$now);", transaction,
                    ("$row", preparationId + "-edge-" + Hash(edge.EdgeId.Value)[..16]), ("$preparation", preparationId),
                    ("$scope", plan.Scope.ScopeId.Value), ("$edge", edge.EdgeId.Value), ("$from", edge.FromMemberId.Value),
                    ("$to", edge.ToMemberId.Value), ("$relation", edge.Relation), ("$json", JsonSerializer.Serialize(edge)),
                    ("$now", ToText(now)));
            foreach (TargetedCorrelationCoverageRowContract row in plan.CorrelationCoverage.Rows)
                Execute("INSERT INTO targeted_correlation_rows(row_id,preparation_id,coverage_id,scope_member_id,status,correlation_qualified,processing_qualified,row_json,created_at) "
                    + "VALUES($row,$preparation,$coverage,$member,$status,$correlation,$processing,$json,$now);", transaction,
                    ("$row", row.RowId.Value), ("$preparation", preparationId),
                    ("$coverage", plan.CorrelationCoverage.CoverageId.Value), ("$member", row.ScopeMemberId.Value),
                    ("$status", row.Status.ToString()), ("$correlation", row.CorrelationQualified ? 1 : 0),
                    ("$processing", row.ProcessingQualified ? 1 : 0), ("$json", JsonSerializer.Serialize(row)),
                    ("$now", ToText(now)));
            foreach (TargetedReuseDecisionContract decision in plan.ReuseDecisions)
                Execute("INSERT INTO targeted_reuse_decisions(decision_id,preparation_id,artifact_kind,artifact_id,disposition,proof_fingerprint,decision_json,created_at) "
                    + "VALUES($id,$preparation,$kind,$artifact,$disposition,$proof,$json,$now);", transaction,
                    ("$id", preparationId + "-reuse-" + Hash(decision.ArtifactKind + decision.ArtifactId.Value)[..16]),
                    ("$preparation", preparationId), ("$kind", decision.ArtifactKind), ("$artifact", decision.ArtifactId.Value),
                    ("$disposition", decision.Disposition), ("$proof", decision.ProofFingerprint.Value),
                    ("$json", JsonSerializer.Serialize(decision)), ("$now", ToText(now)));
            Execute("INSERT INTO targeted_verification_plans(plan_id,preparation_id,plan_payload_id,plan_fingerprint,scope_id,scope_fingerprint,coverage_id,coverage_fingerprint,resolved_manifest_id,startable,limited,created_at) "
                + "VALUES($plan,$preparation,$payload,$fingerprint,$scope,$scopeFingerprint,$coverage,$coverageFingerprint,$manifest,$startable,$limited,$now);",
                transaction, ("$plan", plan.PlanId.Value), ("$preparation", preparationId), ("$payload", payloadId),
                ("$fingerprint", plan.PlanFingerprint.Value), ("$scope", plan.Scope.ScopeId.Value),
                ("$scopeFingerprint", plan.Scope.CanonicalFingerprint.Value),
                ("$coverage", plan.CorrelationCoverage.CoverageId.Value),
                ("$coverageFingerprint", plan.CorrelationCoverage.CanonicalFingerprint.Value),
                ("$manifest", plan.PreparedResolvedInputManifest.ArtifactId.Value), ("$startable", plan.Startable ? 1 : 0),
                ("$limited", plan.Limited ? 1 : 0), ("$now", ToText(now)));
            long revision = checked(current.Revision + 1);
            TargetedVerificationPreparationState state = plan.Startable
                ? plan.Limited ? TargetedVerificationPreparationState.ReadyWithGaps : TargetedVerificationPreparationState.Ready
                : TargetedVerificationPreparationState.Invalidated;
            string eventJson = JsonSerializer.Serialize(new
            {
                planId = plan.PlanId.Value,
                planFingerprint = plan.PlanFingerprint.Value,
                plan.Startable,
                plan.Limited,
                plan.NonStartableReasons,
                plan.Gaps
            });
            string eventId = preparationId + "-event-" + revision;
            string preparationFingerprint = Hash(string.Join('\n', current.PreparationFingerprint, revision, state,
                plan.PlanId.Value, plan.PlanFingerprint.Value));
            string terminalReason = plan.Startable ? "" : string.Join("; ", plan.NonStartableReasons);
            string projectionJson = PreparationProjectionJson(revision, state, preparationFingerprint,
                terminalReason, current.CaptureOperationId, current.TargetSnapshotId, current.EvidenceAcquisitionId,
                plan.PlanId.Value, plan.PlanFingerprint.Value, plan.Startable, plan.Limited, eventId, now);
            Execute("INSERT INTO targeted_preparation_events(event_id,preparation_id,revision,event_kind,event_sha256,event_json,projection_json,created_at) "
                + "VALUES($event,$preparation,$revision,'plan-published',$sha,$json,$projectionJson,$now);", transaction,
                ("$event", eventId), ("$preparation", preparationId), ("$revision", revision),
                ("$sha", TargetedEventHash(eventJson, projectionJson)), ("$json", eventJson),
                ("$projectionJson", projectionJson), ("$now", ToText(now)));
            int projectionChanges = Execute("UPDATE targeted_preparation_projection SET revision=$revision,lifecycle_state=$state,preparation_fingerprint=$preparationFingerprint,terminal_reason=$reason,plan_id=$plan,plan_fingerprint=$planFingerprint,startable=$startable,limited=$limited,last_event_id=$event,updated_at=$now "
                + "WHERE preparation_id=$preparation AND revision=$expectedRevision AND lifecycle_state='PreparingPlan';", transaction,
                ("$revision", revision), ("$state", state.ToString()), ("$preparationFingerprint", preparationFingerprint),
                ("$reason", terminalReason),
                ("$plan", plan.PlanId.Value), ("$planFingerprint", plan.PlanFingerprint.Value),
                ("$startable", plan.Startable ? 1 : 0), ("$limited", plan.Limited ? 1 : 0),
                ("$event", eventId), ("$now", ToText(now)), ("$preparation", preparationId),
                ("$expectedRevision", current.Revision));
            if (projectionChanges != 1)
            {
                throw new InvalidOperationException("The targeted plan publication lost its compare-and-swap race.");
            }
            foreach (TargetedScopeMemberContract root in plan.Scope.DirectRoots)
            {
                if (ScalarLong(
                        "SELECT COUNT(*) FROM targeted_scope_roots WHERE preparation_id=$preparation "
                        + "AND member_id=$member AND root_json=$json;", transaction,
                        ("$preparation", plan.PreparationId.Value), ("$member", root.MemberId.Value),
                        ("$json", JsonSerializer.Serialize(root))) != 1)
                {
                    throw new InvalidOperationException("A retained targeted direct-root projection drifted.");
                }
            }
            foreach (TargetedScopeDependencyContract edge in plan.Scope.Dependencies)
            {
                if (ScalarLong(
                        "SELECT COUNT(*) FROM targeted_scope_dependencies WHERE preparation_id=$preparation "
                        + "AND edge_id=$edge AND from_member_id=$from AND to_member_id=$to "
                        + "AND relation=$relation AND edge_json=$json;", transaction,
                        ("$preparation", plan.PreparationId.Value), ("$edge", edge.EdgeId.Value),
                        ("$from", edge.FromMemberId.Value), ("$to", edge.ToMemberId.Value),
                        ("$relation", edge.Relation), ("$json", JsonSerializer.Serialize(edge))) != 1)
                {
                    throw new InvalidOperationException("A retained targeted dependency projection drifted.");
                }
            }
            transaction.Commit();
            return GetTargetedPreparationCore(preparationId);
        }
    }

    public TargetedVerificationPlanContract ReadTargetedPlan(string preparationId)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT plan_payload_id,plan_fingerprint FROM targeted_verification_plans WHERE preparation_id=$preparation;";
            command.Parameters.AddWithValue("$preparation", preparationId);
            using SqliteDataReader reader = command.ExecuteReader();
            if (!reader.Read()) throw new KeyNotFoundException("The targeted preparation has no retained plan.");
            string payloadId = reader.GetString(0); string fingerprint = reader.GetString(1);
            byte[] bytes = ReadCandidateAnalysisPayload(payloadId);
            TargetedVerificationPlanContract plan = JsonSerializer.Deserialize<TargetedVerificationPlanContract>(bytes)
                ?? throw new InvalidDataException("The retained targeted plan is malformed.");
            TargetedVerificationContractInvariants.Validate(plan);
            if (plan.PlanFingerprint.Value != fingerprint)
                throw new InvalidOperationException("The targeted plan payload failed identity validation.");
            return plan;
        }
    }

    public void ValidateTargetedPlanProjection(TargetedVerificationPlanContract plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        lock (gate)
        {
            if (ScalarLong(
                    "SELECT COUNT(*) FROM targeted_verification_plans WHERE preparation_id=$preparation "
                    + "AND plan_id=$plan AND plan_fingerprint=$planFingerprint AND scope_id=$scope "
                    + "AND scope_fingerprint=$scopeFingerprint AND coverage_id=$coverage "
                    + "AND coverage_fingerprint=$coverageFingerprint AND resolved_manifest_id=$manifest "
                    + "AND startable=$startable AND limited=$limited;",
                    null,
                    ("$preparation", plan.PreparationId.Value), ("$plan", plan.PlanId.Value),
                    ("$planFingerprint", plan.PlanFingerprint.Value), ("$scope", plan.Scope.ScopeId.Value),
                    ("$scopeFingerprint", plan.Scope.CanonicalFingerprint.Value),
                    ("$coverage", plan.CorrelationCoverage.CoverageId.Value),
                    ("$coverageFingerprint", plan.CorrelationCoverage.CanonicalFingerprint.Value),
                    ("$manifest", plan.PreparedResolvedInputManifest.ArtifactId.Value),
                    ("$startable", plan.Startable ? 1 : 0), ("$limited", plan.Limited ? 1 : 0)) != 1
                || ScalarLong("SELECT COUNT(*) FROM targeted_scope_roots WHERE preparation_id=$preparation;",
                    null, ("$preparation", plan.PreparationId.Value)) != plan.Scope.DirectRoots.Count
                || ScalarLong("SELECT COUNT(*) FROM targeted_scope_members WHERE preparation_id=$preparation;",
                    null, ("$preparation", plan.PreparationId.Value)) != plan.Scope.Members.Count
                || ScalarLong("SELECT COUNT(*) FROM targeted_scope_dependencies WHERE preparation_id=$preparation;",
                    null, ("$preparation", plan.PreparationId.Value)) != plan.Scope.Dependencies.Count
                || ScalarLong("SELECT COUNT(*) FROM targeted_correlation_rows WHERE preparation_id=$preparation;",
                    null, ("$preparation", plan.PreparationId.Value)) != plan.CorrelationCoverage.Rows.Count
                || ScalarLong("SELECT COUNT(*) FROM targeted_reuse_decisions WHERE preparation_id=$preparation;",
                    null, ("$preparation", plan.PreparationId.Value)) != plan.ReuseDecisions.Count
                || ScalarLong("SELECT COUNT(*) FROM targeted_operation_inputs WHERE preparation_id=$preparation;",
                    null, ("$preparation", plan.PreparationId.Value)) != 3
                || ScalarLong(
                    "SELECT COUNT(*) FROM targeted_operation_inputs WHERE preparation_id=$preparation "
                    + "AND input_kind='targeted-candidate-delivered-input' AND input_id=$input AND input_sha256=$sha;",
                    null, ("$preparation", plan.PreparationId.Value),
                    ("$input", plan.PreparedDeliveredInput.ArtifactId.Value),
                    ("$sha", plan.PreparedDeliveredInput.Fingerprint.Value)) != 1
                || ScalarLong(
                    "SELECT COUNT(*) FROM targeted_operation_inputs WHERE preparation_id=$preparation "
                    + "AND input_kind='targeted-correlation-coverage' AND input_id=$input AND input_sha256=$sha;",
                    null, ("$preparation", plan.PreparationId.Value),
                    ("$input", plan.PreparedCoverageInput.ArtifactId.Value),
                    ("$sha", plan.PreparedCoverageInput.Fingerprint.Value)) != 1
                || ScalarLong(
                    "SELECT COUNT(*) FROM targeted_operation_inputs WHERE preparation_id=$preparation "
                    + "AND input_kind='targeted-resolved-input-manifest' AND input_id=$input AND input_sha256=$sha;",
                    null, ("$preparation", plan.PreparationId.Value),
                    ("$input", plan.PreparedResolvedInputManifest.ArtifactId.Value),
                    ("$sha", plan.PreparedResolvedInputManifest.Fingerprint.Value)) != 1)
            {
                throw new InvalidOperationException(
                    "The targeted plan projection, scope, correlation denominator, or reuse inventory drifted.");
            }
            foreach (TargetedScopeMemberContract member in plan.Scope.Members)
            {
                if (ScalarLong(
                        "SELECT COUNT(*) FROM targeted_scope_members WHERE preparation_id=$preparation "
                        + "AND member_id=$member AND member_kind=$kind AND stable_identity=$stable "
                        + "AND mandatory=$mandatory AND member_json=$json;", null,
                        ("$preparation", plan.PreparationId.Value), ("$member", member.MemberId.Value),
                        ("$kind", member.Kind.ToString()), ("$stable", member.StableIdentity.Value),
                        ("$mandatory", member.Mandatory ? 1 : 0),
                        ("$json", JsonSerializer.Serialize(member))) != 1)
                {
                    throw new InvalidOperationException("A retained targeted scope member projection drifted.");
                }
            }
            foreach (TargetedCorrelationCoverageRowContract row in plan.CorrelationCoverage.Rows)
            {
                if (ScalarLong(
                        "SELECT COUNT(*) FROM targeted_correlation_rows WHERE preparation_id=$preparation "
                        + "AND row_id=$row AND scope_member_id=$member AND status=$status "
                        + "AND correlation_qualified=$correlation AND processing_qualified=$processing "
                        + "AND row_json=$json;", null,
                        ("$preparation", plan.PreparationId.Value), ("$row", row.RowId.Value),
                        ("$member", row.ScopeMemberId.Value), ("$status", row.Status.ToString()),
                        ("$correlation", row.CorrelationQualified ? 1 : 0),
                        ("$processing", row.ProcessingQualified ? 1 : 0),
                        ("$json", JsonSerializer.Serialize(row))) != 1)
                {
                    throw new InvalidOperationException("A retained targeted correlation row projection drifted.");
                }
            }
            foreach (TargetedReuseDecisionContract decision in plan.ReuseDecisions)
            {
                if (ScalarLong(
                        "SELECT COUNT(*) FROM targeted_reuse_decisions WHERE preparation_id=$preparation "
                        + "AND artifact_kind=$kind AND artifact_id=$artifact AND disposition=$disposition "
                        + "AND proof_fingerprint=$proof AND decision_json=$json;", null,
                        ("$preparation", plan.PreparationId.Value), ("$kind", decision.ArtifactKind),
                        ("$artifact", decision.ArtifactId.Value), ("$disposition", decision.Disposition),
                        ("$proof", decision.ProofFingerprint.Value),
                        ("$json", JsonSerializer.Serialize(decision))) != 1)
                {
                    throw new InvalidOperationException("A retained targeted reuse proof projection drifted.");
                }
            }
        }
    }

    public IReadOnlyList<TargetedOperationInputPersistence> ReadTargetedPreparedOperationInputs(
        string preparationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preparationId);
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT input_kind,input_id,payload_id,input_sha256 FROM targeted_operation_inputs "
                + "WHERE preparation_id=$preparation ORDER BY input_kind,input_id;";
            command.Parameters.AddWithValue("$preparation", preparationId);
            using SqliteDataReader reader = command.ExecuteReader();
            List<(string Kind, string Id, string PayloadId, string Sha)> retained = [];
            while (reader.Read())
            {
                retained.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
            }
            if (retained.Count != 3)
            {
                throw new InvalidOperationException(
                    "The targeted preparation does not retain its complete prepared input authority.");
            }
            List<TargetedOperationInputPersistence> inputs = [];
            foreach ((string kind, string id, string payloadId, string sha) in retained)
            {
                byte[] bytes = ReadCandidateAnalysisPayload(payloadId);
                if (Hash(bytes) != sha)
                {
                    throw new InvalidOperationException("A retained targeted prepared input failed identity validation.");
                }
                inputs.Add(new(kind, id, bytes));
            }
            return inputs;
        }
    }

    public void ValidateSemanticAcquisitionPublicationSeal(
        SemanticAcquisitionPersistenceRecord acquisition,
        SemanticAcquisitionPublicationRecord publication)
    {
        lock (gate)
        {
            if (ScalarLong(
                    "SELECT COUNT(*) FROM semantic_acquisition_attempts WHERE attempt_id=$attempt "
                    + "AND acquisition_id=$acquisition AND attempt_fencing_token=$token "
                    + "AND sealed_input_fingerprint=$seal;", null,
                    ("$attempt", publication.AttemptId), ("$acquisition", acquisition.AcquisitionId),
                    ("$token", publication.AttemptFencingToken),
                    ("$seal", acquisition.SealedInputFingerprint)) != 1
                || ScalarLong(
                    "SELECT COUNT(*) FROM payloads WHERE payload_id=$payload AND content_sha256=$sha "
                    + "AND byte_length=$length;", null,
                    ("$payload", publication.PayloadId), ("$sha", publication.PayloadSha256),
                    ("$length", publication.PayloadByteLength)) != 1)
            {
                throw new InvalidOperationException(
                    "The targeted semantic acquisition attempt fence or published payload seal drifted.");
            }
        }
    }

    public TargetedVerificationReadbackRecord? FindPreparedTargetedVerificationByCommand(string commandId)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT targeted_verification_id FROM targeted_start_admissions WHERE command_id=$command;";
            command.Parameters.AddWithValue("$command", commandId);
            return command.ExecuteScalar() is string id ? GetPreparedTargetedVerificationCore(id) : null;
        }
    }

    public void ValidateTargetedStartReplay(string commandId, string startRequestSha256)
    {
        ValidateSha256(startRequestSha256);
        lock (gate)
        {
            if (ScalarLong(
                    "SELECT COUNT(*) FROM targeted_start_admissions WHERE command_id=$command AND start_request_sha256=$sha;",
                    null, ("$command", commandId), ("$sha", startRequestSha256)) != 1)
            {
                throw new InvalidOperationException("A targeted start idempotency key cannot be rebound.");
            }
        }
    }

    public TargetedVerificationReadbackRecord GetPreparedTargetedVerification(string identity)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT targeted_verification_id FROM targeted_start_admissions WHERE targeted_verification_id=$id OR successor_run_id=$id;";
            command.Parameters.AddWithValue("$id", identity);
            string id = command.ExecuteScalar() as string
                ?? throw new KeyNotFoundException("The targeted verification does not exist.");
            return GetPreparedTargetedVerificationCore(id);
        }
    }

    public IReadOnlyList<string> ReadTargetedReconciliationRelationships(string targetedVerificationId)
    {
        lock (gate)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT relationship || ':' || source_occurrence_id || ':' || COALESCE(successor_occurrence_id,'none') "
                + "FROM targeted_result_links WHERE targeted_verification_id=$verification ORDER BY link_id;";
            command.Parameters.AddWithValue("$verification", targetedVerificationId);
            using SqliteDataReader reader = command.ExecuteReader();
            List<string> values = [];
            while (reader.Read())
            {
                values.Add(reader.GetString(0));
            }
            return values;
        }
    }

    private TargetedPreparationDiagnosticsPersistenceRecord GetTargetedPreparationDiagnostics(
        string preparationId)
    {
        lock (gate)
        {
            TargetedPreparationPersistenceRecord preparation = GetTargetedPreparationCore(preparationId);
            string? captureAttemptId = ScalarStringOrNull(
                "SELECT attempt_id FROM snapshot_capture_attempts WHERE operation_id=$operation "
                + "ORDER BY attempt_generation DESC LIMIT 1;", null,
                ("$operation", preparation.CaptureOperationId));
            string? comparison = ScalarStringOrNull(
                "SELECT structural_comparison FROM targeted_snapshot_links WHERE preparation_id=$preparation;",
                null, ("$preparation", preparationId));
            if (preparation.EvidenceAcquisitionId is null)
            {
                return new(captureAttemptId, comparison, 0, null, 0, 0, null);
            }
            SemanticAcquisitionPersistenceRecord acquisition = GetSemanticAcquisitionCore(
                preparation.EvidenceAcquisitionId);
            long attemptCount = ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_attempts WHERE acquisition_id=$acquisition;",
                null, ("$acquisition", acquisition.AcquisitionId));
            string? attemptId = ScalarStringOrNull(
                "SELECT attempt_id FROM semantic_acquisition_attempts WHERE acquisition_id=$acquisition "
                + "ORDER BY attempt_generation DESC LIMIT 1;", null,
                ("$acquisition", acquisition.AcquisitionId));
            string? checkpointId = ScalarStringOrNull(
                "SELECT checkpoint_id FROM semantic_acquisition_checkpoints WHERE acquisition_id=$acquisition "
                + "ORDER BY created_at DESC,checkpoint_id DESC LIMIT 1;", null,
                ("$acquisition", acquisition.AcquisitionId));
            return new(captureAttemptId, comparison, attemptCount, attemptId,
                acquisition.ProgressCompleted, acquisition.ProgressDenominator, checkpointId);
        }
    }

    private TargetedPreparationReadbackEvidenceRecord GetTargetedPreparationReadbackEvidence(
        string preparationId,
        int maximumLifecycleEvents,
        long afterLifecycleSequence)
    {
        if (maximumLifecycleEvents is < 1 or > 100 || afterLifecycleSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLifecycleEvents));
        }
        lock (gate)
        {
            TargetedPreparationPersistenceRecord preparation = GetTargetedPreparationCore(preparationId);
            ValidateTargetedVerificationEventHistory(preparationId, preparation.EvidenceAcquisitionId);
            TargetedSnapshotReadbackEvidenceRecord? snapshot = null;
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT link.snapshot_payload_id,link.snapshot_fingerprint,link.source_structural_fingerprint," +
                    "link.target_structural_fingerprint,link.structural_comparison,link.confirmed_profile_revision," +
                    "capture.request_json,capture.request_sha256,capture.installation_snapshot_id,capture.payload_id," +
                    "publication.attempt_id,publication.coordinator_fencing_epoch,publication.attempt_fencing_token," +
                    "publication.staged_manifest_sha256,publication.payload_id,publication.installation_snapshot_id," +
                    "attempt.operation_id,attempt.coordinator_fencing_epoch,attempt.attempt_fencing_token,attempt.outcome " +
                    "FROM targeted_snapshot_links link " +
                    "JOIN snapshot_capture_operations capture ON capture.operation_id=link.capture_operation_id " +
                    "AND capture.lifecycle_state='Completed' " +
                    "JOIN snapshot_capture_publications publication ON publication.operation_id=capture.operation_id " +
                    "JOIN snapshot_capture_attempts attempt ON attempt.attempt_id=publication.attempt_id " +
                    "WHERE link.preparation_id=$preparation;";
                command.Parameters.AddWithValue("$preparation", preparationId);
                using SqliteDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    string requestJson = reader.GetString(6);
                    if (Hash(requestJson) != reader.GetString(7)
                        || reader.GetString(8) != preparation.TargetSnapshotId
                        || reader.GetString(9) != reader.GetString(0)
                        || reader.GetString(14) != reader.GetString(0)
                        || reader.GetString(15) != preparation.TargetSnapshotId
                        || reader.GetString(16) != preparation.CaptureOperationId
                        || reader.GetInt64(17) != reader.GetInt64(11)
                        || reader.GetInt64(18) != reader.GetInt64(12)
                        || reader.GetString(19) != "completed-staged")
                    {
                        throw new InvalidOperationException(
                            "The targeted snapshot link, capture request, publication, or attempt fence drifted.");
                    }
                    ValidateSha256(reader.GetString(13));
                    byte[] snapshotBytes = ReadPublishedSnapshotPayload(preparation.TargetSnapshotId!, 64 * 1024 * 1024);
                    if (Hash(snapshotBytes) != reader.GetString(1))
                    {
                        throw new InvalidOperationException(
                            "The targeted snapshot payload differs from its retained link fingerprint.");
                    }
                    snapshot = new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                        reader.GetString(4), reader.GetInt64(5), snapshotBytes);
                    if (reader.Read())
                    {
                        throw new InvalidOperationException("The targeted snapshot readback is ambiguous.");
                    }
                }
            }

            TargetedAcquisitionReadbackEvidenceRecord? acquisition = null;
            if (preparation.EvidenceAcquisitionId is not null)
            {
                SemanticAcquisitionPersistenceRecord retainedAcquisition =
                    GetSemanticAcquisitionCore(preparation.EvidenceAcquisitionId);
                ValidateSemanticAcquisitionLifecycleEvidence(retainedAcquisition);
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    "SELECT r.request_sha256,r.sealed_input_fingerprint,r.producer_id,r.producer_version," +
                    "r.support_manifest_id,r.enumeration_policy_id,r.enumeration_policy_version," +
                    "COALESCE(a.coordinator_fencing_epoch,0),COALESCE(a.attempt_fencing_token,0)," +
                    "p.publication_id,p.payload_id,p.staged_manifest_sha256,p.provenance_json,p.created_at," +
                    "projection.terminal_reason " +
                    "FROM semantic_acquisition_runs r " +
                    "JOIN semantic_acquisition_projection projection USING(acquisition_id) " +
                    "LEFT JOIN semantic_acquisition_attempts a ON a.attempt_id=(SELECT attempt_id " +
                    "FROM semantic_acquisition_attempts WHERE acquisition_id=r.acquisition_id " +
                    "ORDER BY attempt_generation DESC LIMIT 1) " +
                    "LEFT JOIN semantic_acquisition_publications p USING(acquisition_id) " +
                    "WHERE r.acquisition_id=$acquisition;";
                command.Parameters.AddWithValue("$acquisition", preparation.EvidenceAcquisitionId);
                using SqliteDataReader reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    throw new InvalidOperationException("The targeted semantic acquisition readback is missing.");
                }
                string? provenance = reader.IsDBNull(12) ? null : reader.GetString(12);
                byte[]? semanticBytes = null;
                string? provenanceFingerprint = null;
                if (!reader.IsDBNull(9))
                {
                    SemanticAcquisitionPublicationRecord publication =
                        GetSemanticAcquisitionPublication(preparation.EvidenceAcquisitionId);
                    ValidateSemanticAcquisitionPublicationEvidence(retainedAcquisition, publication);
                    semanticBytes = ReadCandidateAnalysisPayload(publication.PayloadId);
                    if (Hash(semanticBytes) != publication.PayloadSha256
                        || semanticBytes.LongLength != publication.PayloadByteLength)
                    {
                        throw new InvalidOperationException(
                            "The targeted semantic acquisition payload differs from its publication seal.");
                    }
                    ValidateSemanticAcquisitionProvenance(retainedAcquisition, publication,
                        reader.GetString(2), reader.GetString(3));
                    provenanceFingerprint = Hash(publication.ProvenanceJson);
                }
                acquisition = new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetInt64(7), reader.GetInt64(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11), provenance is null ? null : provenanceFingerprint,
                    reader.IsDBNull(13) ? null : ParseTargetedTimestamp(reader.GetString(13)), reader.GetString(14),
                    semanticBytes);
                if (reader.Read())
                {
                    throw new InvalidOperationException("The targeted semantic acquisition readback is ambiguous.");
                }
            }

            List<TargetedLifecycleReadbackEvent> allEvents = [];
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT revision,event_kind,event_sha256,created_at FROM targeted_preparation_events " +
                    "WHERE preparation_id=$preparation ORDER BY revision;";
                command.Parameters.AddWithValue("$preparation", preparationId);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    allEvents.Add(new(reader.GetInt64(0), "preparation", reader.GetString(1), reader.GetInt64(0), 0,
                        ParseTargetedTimestamp(reader.GetString(3)), reader.GetString(2), reader.GetString(1)));
                }
            }
            if (preparation.EvidenceAcquisitionId is not null)
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    "SELECT sequence,event_kind,generation,coordinator_fencing_epoch,event_json,created_at " +
                    "FROM semantic_acquisition_events WHERE acquisition_id=$acquisition ORDER BY sequence;";
                command.Parameters.AddWithValue("$acquisition", preparation.EvidenceAcquisitionId);
                using SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string eventJson = reader.GetString(4);
                    allEvents.Add(new(reader.GetInt64(0), "evidence-acquisition", reader.GetString(1), reader.GetInt64(2),
                        reader.GetInt64(3), ParseTargetedTimestamp(reader.GetString(5)), Hash(eventJson), reader.GetString(1)));
                }
            }
            TargetedLifecycleReadbackEvent[] ordered = allEvents
                .OrderBy(item => item.OccurredAt)
                .ThenBy(item => item.Owner, StringComparer.Ordinal)
                .ThenBy(item => item.Sequence)
                .Select((item, index) => item with { Sequence = checked(index + 1L) })
                .ToArray();
            TargetedLifecycleReadbackEvent[] page = ordered
                .Where(item => item.Sequence > afterLifecycleSequence)
                .Take(maximumLifecycleEvents + 1)
                .ToArray();
            bool hasMore = page.Length > maximumLifecycleEvents;
            IReadOnlyList<TargetedLifecycleReadbackEvent> visible = page.Take(maximumLifecycleEvents).ToArray();
            return new(snapshot, acquisition, visible,
                hasMore ? visible[^1].Sequence : null);
        }
    }

    public TargetedPreparationReadbackSnapshotRecord GetTargetedPreparationReadbackSnapshot(
        string preparationId,
        int maximumLifecycleEvents,
        long afterLifecycleSequence)
    {
        lock (gate)
        {
            TargetedPreparationPersistenceRecord preparation = GetTargetedPreparationCore(preparationId);
            TargetedReadbackLockedTestHook?.Invoke();
            TargetedSourceReadbackEvidenceRecord source = GetTargetedSourceReadbackEvidence(preparation);
            TargetedVerificationPlanContract? plan = preparation.PlanId is null
                ? null
                : ReadTargetedPlan(preparationId);
            if (plan is not null)
            {
                ValidateTargetedPlanProjection(plan);
                _ = ReadTargetedPreparedOperationInputs(preparationId);
            }
            TargetedPreparationDiagnosticsPersistenceRecord diagnostics =
                GetTargetedPreparationDiagnostics(preparationId);
            SemanticAcquisitionPersistenceRecord? acquisition = preparation.EvidenceAcquisitionId is null
                ? null
                : GetSemanticAcquisitionCore(preparation.EvidenceAcquisitionId);
            SemanticAcquisitionPublicationRecord? publication = null;
            if (acquisition is not null && acquisition.State is "Completed" or "CompletedWithGaps")
            {
                publication = GetSemanticAcquisitionPublication(acquisition.AcquisitionId);
            }
            TargetedPreparationReadbackEvidenceRecord evidence = GetTargetedPreparationReadbackEvidence(
                preparationId, maximumLifecycleEvents, afterLifecycleSequence);
            TargetedPreparationPersistenceRecord confirmed = GetTargetedPreparationCore(preparationId);
            if (confirmed.Revision != preparation.Revision
                || confirmed.PreparationFingerprint != preparation.PreparationFingerprint
                || confirmed.State != preparation.State
                || confirmed.PlanFingerprint != preparation.PlanFingerprint
                || confirmed.EvidenceAcquisitionId != preparation.EvidenceAcquisitionId)
            {
                throw new InvalidOperationException(
                    "The targeted preparation advanced during coherent readback.");
            }
            if (acquisition is not null)
            {
                SemanticAcquisitionPersistenceRecord confirmedAcquisition =
                    GetSemanticAcquisitionCore(acquisition.AcquisitionId);
                if (confirmedAcquisition.Generation != acquisition.Generation
                    || confirmedAcquisition.DurableSequence != acquisition.DurableSequence
                    || confirmedAcquisition.State != acquisition.State)
                {
                    throw new InvalidOperationException(
                        "The targeted semantic acquisition advanced during coherent readback.");
                }
            }
            return new(preparation, source, plan, diagnostics, acquisition, publication, evidence);
        }
    }

    private TargetedSourceReadbackEvidenceRecord GetTargetedSourceReadbackEvidence(
        TargetedPreparationPersistenceRecord preparation)
    {
        RunRecord sourceRun = GetRun(preparation.SourceRunId);
        if (sourceRun.State is not (LifecycleState.Completed or LifecycleState.CompletedWithGaps
                or LifecycleState.LimitReached))
        {
            throw new InvalidOperationException(
                "The targeted source run no longer has the required terminal analytical state.");
        }
        RunOperationRecord sourceOperation = GetRunOperation(preparation.SourceRunId)
            ?? throw new InvalidOperationException(
                "The targeted source managed operation is not retained.");
        if (sourceOperation.OperationKind != "managed-analysis-v1"
            || Hash(sourceOperation.RequestJson) != sourceOperation.RequestSha256)
        {
            throw new InvalidOperationException(
                "The targeted source managed operation failed retained identity validation.");
        }
        ResultItemPersistenceRecord occurrence = GetResultItem(
            preparation.SourceRunId, preparation.SourceOccurrenceId);
        AnalysisPhaseCheckpointRecord checkpoint = ReadLatestAnalysisPhaseCheckpoint(
            preparation.SourceRunId, "finding-case-analysis")
            ?? throw new InvalidOperationException(
                "The targeted source run has no retained canonical finding/case checkpoint.");
        byte[] canonicalBytes = ReadFindingCasePayload(checkpoint.PayloadId);
        if (checkpoint.PayloadSha256 != Hash(canonicalBytes)
            || checkpoint.PayloadByteLength != canonicalBytes.LongLength
            || occurrence.SourcePayloadId != checkpoint.PayloadId
            || occurrence.SourcePayloadSha256 != checkpoint.PayloadSha256)
        {
            throw new InvalidOperationException(
                "The targeted source occurrence differs from its retained canonical checkpoint.");
        }
        byte[] sourceSnapshotBytes = ReadPublishedSnapshotPayload(
            sourceRun.Binding.InstallationSnapshotId, 64 * 1024 * 1024);
        return new(sourceRun, sourceOperation, occurrence, canonicalBytes, sourceSnapshotBytes);
    }

    private void ValidateTargetedVerificationEventHistory(
        string preparationId,
        string? acquisitionId)
    {
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT event_json,projection_json,event_sha256 FROM targeted_preparation_events "
                + "WHERE preparation_id=$preparation ORDER BY revision;";
            command.Parameters.AddWithValue("$preparation", preparationId);
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (TargetedEventHash(reader.GetString(0), reader.GetString(1)) != reader.GetString(2))
                {
                    throw new InvalidOperationException(
                        "Targeted preparation event history failed projection identity validation.");
                }
            }
        }
        if (acquisitionId is null)
        {
            return;
        }
        using SqliteCommand acquisitionEvents = connection.CreateCommand();
        acquisitionEvents.CommandText =
            "SELECT event_json,projection_json FROM semantic_acquisition_events "
            + "WHERE acquisition_id=$acquisition ORDER BY sequence;";
        acquisitionEvents.Parameters.AddWithValue("$acquisition", acquisitionId);
        using SqliteDataReader acquisitionReader = acquisitionEvents.ExecuteReader();
        while (acquisitionReader.Read())
        {
            if (!SemanticProjectionIsBound(acquisitionReader.GetString(0), acquisitionReader.GetString(1)))
            {
                throw new InvalidOperationException(
                    "Semantic acquisition event history failed projection identity validation.");
            }
        }
    }

    private void ValidateSemanticAcquisitionPublicationEvidence(
        SemanticAcquisitionPersistenceRecord acquisition,
        SemanticAcquisitionPublicationRecord publication)
    {
        ValidateSemanticAcquisitionPublicationSeal(acquisition, publication);
        using (SqliteCommand publicationCheckpoint = connection.CreateCommand())
        {
            publicationCheckpoint.CommandText =
                "SELECT checkpoint_json FROM semantic_acquisition_checkpoints "
                + "WHERE acquisition_id=$acquisition AND attempt_id=$attempt;";
            publicationCheckpoint.Parameters.AddWithValue("$acquisition", acquisition.AcquisitionId);
            publicationCheckpoint.Parameters.AddWithValue("$attempt", publication.AttemptId);
            string checkpointJson = publicationCheckpoint.ExecuteScalar() as string
                ?? throw new InvalidOperationException(
                    "The targeted semantic acquisition publication checkpoint is missing.");
            using JsonDocument document = JsonDocument.Parse(checkpointJson);
            JsonElement checkpoint = document.RootElement;
            if (checkpoint.ValueKind != JsonValueKind.Object
                || checkpoint.EnumerateObject().Count() != 9
                || checkpoint.GetProperty("AcquisitionId").GetString() != acquisition.AcquisitionId
                || checkpoint.GetProperty("AttemptId").GetString() != publication.AttemptId
                || checkpoint.GetProperty("AttemptFencingToken").GetInt64() != publication.AttemptFencingToken
                || checkpoint.GetProperty("TargetSnapshotId").GetString() != publication.TargetSnapshotId
                || checkpoint.GetProperty("semanticOutputId").GetString() != publication.SemanticOutputId
                || checkpoint.GetProperty("payloadId").GetString() != publication.PayloadId
                || checkpoint.GetProperty("expectedSha256").GetString() != publication.PayloadSha256
                || checkpoint.GetProperty("expectedByteLength").GetInt64() != publication.PayloadByteLength
                || checkpoint.GetProperty("expectedManifestSha256").GetString()
                    != publication.StagedManifestSha256)
            {
                throw new InvalidOperationException(
                    "The targeted semantic acquisition publication differs from its sealed checkpoint.");
            }
        }
        if (publication.AcquisitionId != acquisition.AcquisitionId
            || publication.TargetSnapshotId != acquisition.TargetSnapshotId
            || ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_attempts WHERE acquisition_id=$acquisition "
                + "AND sealed_input_fingerprint<>$seal;", null,
                ("$acquisition", acquisition.AcquisitionId),
                ("$seal", acquisition.SealedInputFingerprint)) != 0
            || ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_checkpoints checkpoint "
                + "JOIN semantic_acquisition_attempts attempt ON attempt.attempt_id=checkpoint.attempt_id "
                + "WHERE checkpoint.acquisition_id=$acquisition AND (attempt.acquisition_id<>$acquisition "
                + "OR checkpoint.attempt_fencing_token<>attempt.attempt_fencing_token "
                + "OR checkpoint.target_snapshot_id<>$snapshot "
                + "OR checkpoint.sealed_input_fingerprint<>$seal);", null,
                ("$acquisition", acquisition.AcquisitionId),
                ("$snapshot", acquisition.TargetSnapshotId),
                ("$seal", acquisition.SealedInputFingerprint)) != 0)
        {
            throw new InvalidOperationException(
                "The targeted semantic acquisition attempt or checkpoint evidence drifted.");
        }

        using (SqliteCommand checkpoints = connection.CreateCommand())
        {
            checkpoints.CommandText =
                "SELECT checkpoint_json,checkpoint_sha256 FROM semantic_acquisition_checkpoints "
                + "WHERE acquisition_id=$acquisition;";
            checkpoints.Parameters.AddWithValue("$acquisition", acquisition.AcquisitionId);
            using SqliteDataReader reader = checkpoints.ExecuteReader();
            while (reader.Read())
            {
                if (Hash(reader.GetString(0)) != reader.GetString(1))
                {
                    throw new InvalidOperationException(
                        "The targeted semantic acquisition checkpoint payload drifted.");
                }
            }
        }

        if (ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_progress progress "
                + "JOIN semantic_acquisition_attempts attempt ON attempt.attempt_id=progress.attempt_id "
                + "WHERE progress.acquisition_id=$acquisition AND attempt.acquisition_id<>$acquisition;", null,
                ("$acquisition", acquisition.AcquisitionId)) != 0
            || ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_application_links link "
                + "WHERE link.acquisition_id=$acquisition AND (link.preparation_id<>$preparation "
                + "OR link.semantic_output_id<>$output "
                + "OR (link.use_kind='preparation-prerequisite' AND link.successor_run_id IS NOT NULL) "
                + "OR (link.use_kind='successor-input' AND link.successor_run_id IS NULL));", null,
                ("$acquisition", acquisition.AcquisitionId),
                ("$preparation", acquisition.PreparationId),
                ("$output", publication.SemanticOutputId)) != 0
            || ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_application_links WHERE acquisition_id=$acquisition "
                + "AND use_kind='preparation-prerequisite' AND successor_run_id IS NULL;", null,
                ("$acquisition", acquisition.AcquisitionId)) != 1)
        {
            throw new InvalidOperationException(
                "The targeted semantic acquisition progress or application link drifted.");
        }
    }

    private void ValidateSemanticAcquisitionLifecycleEvidence(
        SemanticAcquisitionPersistenceRecord acquisition)
    {
        if (ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_runs WHERE acquisition_id=$acquisition "
                + "AND producer_id='infinium.bethesda.semantic-index' AND producer_version='2.0.0' "
                + "AND support_manifest_id='bethesda-semantic-support-v2' "
                + "AND enumeration_policy_id='qualified-bethesda-enumeration' "
                + "AND enumeration_policy_version='1.0.0';", null,
                ("$acquisition", acquisition.AcquisitionId)) != 1
            || ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_attempts WHERE acquisition_id=$acquisition "
                + "AND sealed_input_fingerprint<>$seal;", null,
                ("$acquisition", acquisition.AcquisitionId),
                ("$seal", acquisition.SealedInputFingerprint)) != 0
            || acquisition.ActiveAttemptId is not null
            && ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_attempts WHERE attempt_id=$attempt "
                + "AND acquisition_id=$acquisition AND sealed_input_fingerprint=$seal;", null,
                ("$attempt", acquisition.ActiveAttemptId),
                ("$acquisition", acquisition.AcquisitionId),
                ("$seal", acquisition.SealedInputFingerprint)) != 1)
        {
            throw new InvalidOperationException(
                "The targeted semantic acquisition request, producer, or attempt evidence drifted.");
        }

        using (SqliteCommand checkpoints = connection.CreateCommand())
        {
            checkpoints.CommandText =
                "SELECT checkpoint.checkpoint_json,checkpoint.checkpoint_sha256," +
                "checkpoint.attempt_fencing_token,attempt.attempt_fencing_token," +
                "checkpoint.target_snapshot_id,checkpoint.sealed_input_fingerprint,attempt.acquisition_id " +
                "FROM semantic_acquisition_checkpoints checkpoint " +
                "JOIN semantic_acquisition_attempts attempt ON attempt.attempt_id=checkpoint.attempt_id " +
                "WHERE checkpoint.acquisition_id=$acquisition;";
            checkpoints.Parameters.AddWithValue("$acquisition", acquisition.AcquisitionId);
            using SqliteDataReader reader = checkpoints.ExecuteReader();
            while (reader.Read())
            {
                if (Hash(reader.GetString(0)) != reader.GetString(1)
                    || reader.GetInt64(2) != reader.GetInt64(3)
                    || reader.GetString(4) != acquisition.TargetSnapshotId
                    || reader.GetString(5) != acquisition.SealedInputFingerprint
                    || reader.GetString(6) != acquisition.AcquisitionId)
                {
                    throw new InvalidOperationException(
                        "The targeted semantic acquisition checkpoint evidence drifted.");
                }
            }
        }
        if (ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_progress progress "
                + "JOIN semantic_acquisition_attempts attempt ON attempt.attempt_id=progress.attempt_id "
                + "WHERE progress.acquisition_id=$acquisition AND attempt.acquisition_id<>$acquisition;", null,
                ("$acquisition", acquisition.AcquisitionId)) != 0)
        {
            throw new InvalidOperationException(
                "The targeted semantic acquisition progress evidence drifted.");
        }
    }

    private static void ValidateSemanticAcquisitionProvenance(
        SemanticAcquisitionPersistenceRecord acquisition,
        SemanticAcquisitionPublicationRecord publication,
        string producerId,
        string producerVersion)
    {
        using JsonDocument document = JsonDocument.Parse(publication.ProvenanceJson);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || root.EnumerateObject().Count() != 7
            || root.GetProperty("AcquisitionId").GetString() != acquisition.AcquisitionId
            || root.GetProperty("TargetSnapshotId").GetString() != acquisition.TargetSnapshotId
            || root.GetProperty("SealedInputFingerprint").GetString() != acquisition.SealedInputFingerprint
            || root.GetProperty("producerId").GetString() != producerId
            || root.GetProperty("producerVersion").GetString() != producerVersion
            || root.GetProperty("Sha256").GetString() != publication.PayloadSha256
            || root.GetProperty("ByteLength").GetInt64() != publication.PayloadByteLength)
        {
            throw new InvalidOperationException(
                "The targeted semantic acquisition provenance is not bound to its retained publication.");
        }
    }

    private TargetedPreparationPersistenceRecord GetTargetedPreparationCore(
        string preparationId, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT r.durable_command_id,r.user_gesture_id,r.request_sha256,r.request_json,r.source_run_id,
                   r.source_occurrence_kind,r.source_occurrence_id,r.confirmed_profile_id,r.confirmed_profile_revision,
                   r.saved_configuration_id,r.saved_configuration_revision,r.analysis_context_id,r.analysis_context_revision,
                   r.analysis_context_fingerprint,r.initiation_kind,r.dispatch_deadline,p.revision,p.lifecycle_state,
                   p.preparation_fingerprint,p.terminal_reason,r.capture_operation_id,p.target_snapshot_id,
                   p.evidence_acquisition_id,p.plan_id,p.plan_fingerprint,p.startable,p.limited,r.created_at,p.updated_at
            FROM targeted_preparation_requests r JOIN targeted_preparation_projection p USING(preparation_id)
            WHERE r.preparation_id=$preparation;
            """;
        command.Parameters.AddWithValue("$preparation", preparationId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) throw new KeyNotFoundException("The targeted preparation does not exist.");
        TargetedPreparationPersistenceRecord value = new(preparationId, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetInt64(8),
            reader.GetString(9), reader.GetInt64(10), reader.GetString(11), reader.GetInt64(12), reader.GetString(13),
            reader.GetString(14), ParseTargetedTimestamp(reader.GetString(15)), reader.GetInt64(16),
            Enum.Parse<TargetedVerificationPreparationState>(reader.GetString(17)), reader.GetString(18), reader.GetString(19),
            reader.GetString(20), reader.IsDBNull(21) ? null : reader.GetString(21), reader.IsDBNull(22) ? null : reader.GetString(22),
            reader.IsDBNull(23) ? null : reader.GetString(23), reader.IsDBNull(24) ? null : reader.GetString(24),
            reader.GetInt64(25) == 1, reader.GetInt64(26) == 1, ParseTargetedTimestamp(reader.GetString(27)), ParseTargetedTimestamp(reader.GetString(28)));
        reader.Close();
        if (Hash(value.RequestJson) != value.RequestSha256
            || ScalarLong(
                "SELECT COUNT(*) FROM targeted_preparation_projection p "
                + "JOIN targeted_preparation_events e ON e.event_id=p.last_event_id "
                + "WHERE p.preparation_id=$preparation AND p.revision=json_extract(e.projection_json,'$.revision') "
                + "AND p.lifecycle_state=json_extract(e.projection_json,'$.state') "
                + "AND p.preparation_fingerprint=json_extract(e.projection_json,'$.fingerprint') "
                + "AND p.terminal_reason=json_extract(e.projection_json,'$.terminal_reason') "
                + "AND p.capture_operation_id=json_extract(e.projection_json,'$.capture_operation_id') "
                + "AND p.target_snapshot_id IS json_extract(e.projection_json,'$.target_snapshot_id') "
                + "AND p.evidence_acquisition_id IS json_extract(e.projection_json,'$.evidence_acquisition_id') "
                + "AND p.plan_id IS json_extract(e.projection_json,'$.plan_id') "
                + "AND p.plan_fingerprint IS json_extract(e.projection_json,'$.plan_fingerprint') "
                + "AND p.startable=json_extract(e.projection_json,'$.startable') "
                + "AND p.limited=json_extract(e.projection_json,'$.limited') "
                + "AND p.updated_at=json_extract(e.projection_json,'$.updated_at');",
                transaction, ("$preparation", preparationId)) != 1
            || ScalarLong(
                "SELECT COUNT(*) FROM targeted_preparation_events e JOIN targeted_preparation_projection p "
                + "ON p.preparation_id=e.preparation_id AND p.last_event_id=e.event_id AND p.revision=e.revision "
                + "WHERE e.preparation_id=$preparation AND e.revision=$revision AND e.event_sha256=$eventSha;",
                transaction, ("$preparation", preparationId), ("$revision", value.Revision),
                ("$eventSha", ScalarString(
                    "SELECT event_sha256 FROM targeted_preparation_events WHERE preparation_id=$preparation AND revision=$revision;",
                    transaction, ("$preparation", preparationId), ("$revision", value.Revision)))) != 1)
        {
            throw new InvalidOperationException("The targeted preparation failed durable identity validation.");
        }
        using SqliteCommand eventCommand = connection.CreateCommand();
        eventCommand.Transaction = transaction;
        eventCommand.CommandText =
            "SELECT event_json,projection_json,event_sha256 FROM targeted_preparation_events "
            + "WHERE preparation_id=$preparation AND revision=$revision;";
        eventCommand.Parameters.AddWithValue("$preparation", preparationId);
        eventCommand.Parameters.AddWithValue("$revision", value.Revision);
        using SqliteDataReader eventReader = eventCommand.ExecuteReader();
        if (!eventReader.Read())
        {
            throw new InvalidOperationException("The targeted preparation current event is missing.");
        }
        string eventJson = eventReader.GetString(0);
        string projectionJson = eventReader.GetString(1);
        string eventSha = eventReader.GetString(2);
        if (eventReader.Read())
        {
            throw new InvalidOperationException("The targeted preparation current event is ambiguous.");
        }
        if (TargetedEventHash(eventJson, projectionJson) != eventSha)
        {
            throw new InvalidOperationException("The targeted preparation event payload failed identity validation.");
        }
        return value;
    }

    private SemanticAcquisitionPersistenceRecord GetSemanticAcquisitionCore(
        string acquisitionId, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT r.preparation_id,r.target_snapshot_id,r.request_json,r.request_sha256,r.sealed_input_fingerprint,r.dispatch_deadline," +
            "p.lifecycle_state,p.generation,p.durable_sequence,p.progress_completed,p.progress_denominator,p.active_attempt_id,p.terminal_reason,r.created_at,p.updated_at " +
            "FROM semantic_acquisition_runs r JOIN semantic_acquisition_projection p USING(acquisition_id) WHERE r.acquisition_id=$acquisition;";
        command.Parameters.AddWithValue("$acquisition", acquisitionId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) throw new KeyNotFoundException("The semantic acquisition does not exist.");
        SemanticAcquisitionPersistenceRecord value = new(acquisitionId, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            reader.GetString(4), ParseTargetedTimestamp(reader.GetString(5)), reader.GetString(6), reader.GetInt64(7),
            reader.GetInt64(8), reader.GetInt64(9), reader.GetInt64(10), reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.GetString(12), ParseTargetedTimestamp(reader.GetString(13)), ParseTargetedTimestamp(reader.GetString(14)));
        reader.Close();
        if (Hash(value.RequestJson) != value.RequestSha256
            || ScalarLong(
                "SELECT COUNT(*) FROM semantic_acquisition_projection p "
                + "JOIN semantic_acquisition_events e ON e.acquisition_id=p.acquisition_id "
                + "AND e.sequence=p.durable_sequence WHERE p.acquisition_id=$acquisition "
                + "AND p.lifecycle_state=json_extract(e.projection_json,'$.state') "
                + "AND p.generation=json_extract(e.projection_json,'$.generation') "
                + "AND p.durable_sequence=json_extract(e.projection_json,'$.sequence') "
                + "AND p.progress_completed=json_extract(e.projection_json,'$.progress_completed') "
                + "AND p.progress_denominator=json_extract(e.projection_json,'$.progress_denominator') "
                + "AND p.active_attempt_id IS json_extract(e.projection_json,'$.active_attempt_id') "
                + "AND p.terminal_reason=json_extract(e.projection_json,'$.terminal_reason') "
                + "AND p.updated_at=json_extract(e.projection_json,'$.updated_at');",
                transaction, ("$acquisition", acquisitionId)) != 1
            || ScalarLong("SELECT COUNT(*) FROM semantic_acquisition_events WHERE acquisition_id=$acquisition AND sequence=$sequence;",
                transaction, ("$acquisition", acquisitionId), ("$sequence", value.DurableSequence)) != 1)
        {
            throw new InvalidOperationException("The semantic acquisition failed durable identity validation.");
        }
        using SqliteCommand eventCommand = connection.CreateCommand();
        eventCommand.Transaction = transaction;
        eventCommand.CommandText =
            "SELECT event_json,projection_json FROM semantic_acquisition_events "
            + "WHERE acquisition_id=$acquisition AND sequence=$sequence;";
        eventCommand.Parameters.AddWithValue("$acquisition", acquisitionId);
        eventCommand.Parameters.AddWithValue("$sequence", value.DurableSequence);
        using SqliteDataReader eventReader = eventCommand.ExecuteReader();
        if (!eventReader.Read()
            || !SemanticProjectionIsBound(eventReader.GetString(0), eventReader.GetString(1)))
        {
            throw new InvalidOperationException(
                "The semantic acquisition event projection failed identity validation.");
        }
        return value;
    }

    private void EnsureCurrentSemanticAcquisitionAttempt(SemanticAcquisitionAttemptRecord attempt)
    {
        lock (gate)
        {
            using SqliteTransaction transaction = BeginTransaction();
            EnsureCurrentSemanticAcquisitionAttempt(attempt, transaction);
            transaction.Commit();
        }
    }

    private void EnsureCurrentSemanticAcquisitionAttempt(SemanticAcquisitionAttemptRecord attempt,
        SqliteTransaction transaction, bool requireUnexpiredLease = true)
    {
        long count = ScalarLong(
            "SELECT COUNT(*) FROM semantic_acquisition_attempts a JOIN semantic_acquisition_projection p USING(acquisition_id) "
            + "WHERE a.attempt_id=$attempt AND a.acquisition_id=$acquisition AND a.attempt_fencing_token=$token "
            + "AND a.coordinator_fencing_epoch=$epoch AND a.sealed_input_fingerprint=$sealed "
            + "AND p.lifecycle_state='Running' AND p.active_attempt_id=a.attempt_id"
            + (requireUnexpiredLease ? " AND a.lease_expires_at>$now;" : ";"), transaction,
            ("$attempt", attempt.AttemptId), ("$acquisition", attempt.AcquisitionId),
            ("$token", attempt.AttemptFencingToken), ("$epoch", attempt.CoordinatorFencingEpoch),
            ("$sealed", attempt.SealedInputFingerprint), ("$now", ToText(DateTimeOffset.UtcNow)));
        if (count != 1) throw new InvalidOperationException("The semantic acquisition attempt is stale or fenced.");
    }

    private TargetedVerificationReadbackRecord GetPreparedTargetedVerificationCore(string targetedVerificationId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT a.admission_id,a.preparation_id,a.command_id,l.source_run_id,l.source_occurrence_id,a.successor_run_id," +
            "l.target_snapshot_id,l.evidence_acquisition_id,a.managed_operation_kind,a.managed_operation_fingerprint," +
            "a.submission_fingerprint,a.created_at FROM targeted_start_admissions a JOIN targeted_initiation_lineage l " +
            "USING(targeted_verification_id) WHERE a.targeted_verification_id=$id;";
        command.Parameters.AddWithValue("$id", targetedVerificationId);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) throw new KeyNotFoundException("The targeted verification does not exist.");
        return new(reader.GetString(0), targetedVerificationId, reader.GetString(1), reader.GetString(2),
            reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
            reader.GetString(8), reader.GetString(9), reader.GetString(10), ParseTargetedTimestamp(reader.GetString(11)));
    }

    private void RebuildTargetedVerificationProjections(SqliteTransaction transaction)
    {
        ValidateTargetedVerificationEventHistory(transaction);
        Execute("DELETE FROM targeted_preparation_projection; DELETE FROM semantic_acquisition_projection;", transaction);
        Execute(
            """
            INSERT INTO targeted_preparation_projection(
                preparation_id,revision,lifecycle_state,preparation_fingerprint,terminal_reason,capture_operation_id,
                target_snapshot_id,evidence_acquisition_id,plan_id,plan_fingerprint,startable,limited,last_event_id,updated_at)
            SELECT event.preparation_id,
                json_extract(event.projection_json,'$.revision'),
                json_extract(event.projection_json,'$.state'),
                json_extract(event.projection_json,'$.fingerprint'),
                json_extract(event.projection_json,'$.terminal_reason'),
                json_extract(event.projection_json,'$.capture_operation_id'),
                json_extract(event.projection_json,'$.target_snapshot_id'),
                json_extract(event.projection_json,'$.evidence_acquisition_id'),
                json_extract(event.projection_json,'$.plan_id'),
                json_extract(event.projection_json,'$.plan_fingerprint'),
                json_extract(event.projection_json,'$.startable'),
                json_extract(event.projection_json,'$.limited'),
                event.event_id,
                json_extract(event.projection_json,'$.updated_at')
            FROM targeted_preparation_events event
            WHERE event.revision=(SELECT MAX(latest.revision) FROM targeted_preparation_events latest
                WHERE latest.preparation_id=event.preparation_id);

            INSERT INTO semantic_acquisition_projection(
                acquisition_id,lifecycle_state,generation,durable_sequence,progress_completed,progress_denominator,
                active_attempt_id,terminal_reason,updated_at)
            SELECT event.acquisition_id,
                json_extract(event.projection_json,'$.state'),
                json_extract(event.projection_json,'$.generation'),
                json_extract(event.projection_json,'$.sequence'),
                json_extract(event.projection_json,'$.progress_completed'),
                json_extract(event.projection_json,'$.progress_denominator'),
                json_extract(event.projection_json,'$.active_attempt_id'),
                json_extract(event.projection_json,'$.terminal_reason'),
                json_extract(event.projection_json,'$.updated_at')
            FROM semantic_acquisition_events event
            WHERE event.sequence=(SELECT MAX(latest.sequence) FROM semantic_acquisition_events latest
                WHERE latest.acquisition_id=event.acquisition_id);
            """, transaction);
    }

    private void ValidateTargetedVerificationEventHistory(SqliteTransaction transaction)
    {
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                "SELECT event_json,projection_json,event_sha256 FROM targeted_preparation_events ORDER BY preparation_id,revision;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (TargetedEventHash(reader.GetString(0), reader.GetString(1)) != reader.GetString(2))
                {
                    throw new InvalidOperationException(
                        "Targeted preparation event history failed projection identity validation.");
                }
            }
        }
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                "SELECT event_json,projection_json FROM semantic_acquisition_events ORDER BY acquisition_id,sequence;";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!SemanticProjectionIsBound(reader.GetString(0), reader.GetString(1)))
                {
                    throw new InvalidOperationException(
                        "Semantic acquisition event history failed projection identity validation.");
                }
            }
        }
    }

    private static string PreparationProjectionJson(
        long revision, TargetedVerificationPreparationState state, string fingerprint, string terminalReason,
        string captureOperationId, string? targetSnapshotId, string? evidenceAcquisitionId,
        string? planId, string? planFingerprint, bool startable, bool limited, string lastEventId,
        DateTimeOffset updatedAt) => JsonSerializer.Serialize(new
        {
            revision,
            state = state.ToString(),
            fingerprint,
            terminal_reason = terminalReason,
            capture_operation_id = captureOperationId,
            target_snapshot_id = targetSnapshotId,
            evidence_acquisition_id = evidenceAcquisitionId,
            plan_id = planId,
            plan_fingerprint = planFingerprint,
            startable,
            limited,
            last_event_id = lastEventId,
            updated_at = ToText(updatedAt),
        });

    private static string SemanticProjectionJson(
        string state, long generation, long sequence, long progressCompleted, long progressDenominator,
        string? activeAttemptId, string terminalReason, DateTimeOffset updatedAt) => JsonSerializer.Serialize(new
        {
            state,
            generation,
            sequence,
            progress_completed = progressCompleted,
            progress_denominator = progressDenominator,
            active_attempt_id = activeAttemptId,
            terminal_reason = terminalReason,
            updated_at = ToText(updatedAt),
        });

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));

    private static string TargetedEventHash(string eventJson, string projectionJson) =>
        Hash(eventJson + "\n" + projectionJson);

    private static bool SemanticProjectionIsBound(string eventJson, string projectionJson)
    {
        using JsonDocument document = JsonDocument.Parse(eventJson);
        return document.RootElement.TryGetProperty("projectionSha256", out JsonElement fingerprint)
            && fingerprint.ValueKind == JsonValueKind.String
            && fingerprint.GetString() == Hash(projectionJson);
    }

    private static DateTimeOffset ParseTargetedTimestamp(string value) =>
        DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
