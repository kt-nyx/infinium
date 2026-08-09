namespace Infinium.Persistence;

public sealed record CandidateAnalysisPersistenceReceipt(
    string EvidenceId,
    string PayloadId,
    int DecisionCount,
    int CandidateCount,
    int HypothesisCount,
    int AbstentionCount,
    int GapCount,
    int FailureCount,
    int DependencyEdgeCount);

public sealed record CandidateCheckpointPersistenceRecord(
    string CheckpointId,
    string RunId,
    string DependencyClosureId,
    string ContentSha256,
    string CompletedPartitionsJson,
    string PendingAndGapsJson,
    DateTimeOffset CreatedAt);

public sealed record CandidatePhaseCheckpointPublication(
    string CheckpointId,
    string DependencyClosureId,
    string ContentSha256,
    byte[] PayloadBytes,
    string PendingAndGapsJson);

public sealed record CandidatePhasePersistenceReceipt(
    CandidateAnalysisPersistenceReceipt Analysis,
    string CheckpointPayloadId);
