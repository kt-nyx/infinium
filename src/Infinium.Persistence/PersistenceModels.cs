using Infinium.Domain.Contracts;

namespace Infinium.Persistence;

public sealed record CoordinatorAuthority(string InstanceId, long FencingEpoch, DateTimeOffset ExpiresAt);

public sealed record RunBinding(
    string InstallationSnapshotId,
    string AnalysisContextId,
    string EffectiveScanConfigurationId,
    string ResolvedInputManifestId);

public sealed record RunRecord(
    string RunId,
    RunBinding Binding,
    LifecycleState State,
    long Generation,
    long CoordinatorFencingEpoch,
    long DurableSequence,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record RunOperationRecord(
    string RunId,
    string OperationKind,
    string RequestJson,
    string RequestSha256,
    DateTimeOffset CreatedAt);

public sealed record AttemptRecord(
    string AttemptId,
    string RunId,
    long AttemptGeneration,
    long CoordinatorFencingEpoch,
    long AttemptFencingToken,
    DateTimeOffset LeaseExpiresAt,
    string Outcome);

public sealed record DispatchAdmission(RunRecord Run, AttemptRecord Attempt);

public sealed record PayloadAdmission(
    string PayloadId,
    string Sha256,
    long ByteLength,
    string RelativePath,
    string PublicationReceiptId,
    string StagedManifestSha256);

public sealed record SnapshotCaptureOperationRecord(
    string OperationId,
    string DurableCommandId,
    string RequestJson,
    string RequestSha256,
    string InitiationKind,
    DateTimeOffset DispatchDeadline,
    string State,
    long Generation,
    long CoordinatorFencingEpoch,
    string? InstallationSnapshotId,
    string? PayloadId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SnapshotCaptureAttemptRecord(
    string AttemptId,
    string OperationId,
    long AttemptGeneration,
    long CoordinatorFencingEpoch,
    long AttemptFencingToken,
    DateTimeOffset LeaseExpiresAt,
    string Outcome);

public sealed record ReconciliationIssue(string Kind, string Path, string Detail);

public sealed record BackupArtifact(string DatabasePath, string ManifestPath, string Sha256);

public sealed record DurableCommandRecord(
    string CommandId,
    string Disposition,
    string RunId,
    string ResultingState,
    string? TransitionId,
    DateTimeOffset CreatedAt,
    string CommandKind,
    long ExpectedGeneration,
    RunBinding RunBinding,
    string? StartInitiationKind,
    DateTimeOffset? StartDispatchDeadline,
    string? StartPreparationId,
    string? StartUserGestureId);
