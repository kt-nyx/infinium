using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;

public enum AnalysisTerminalOutcome
{
    Completed,
    CompletedWithGaps,
    Cancelled,
    LimitReached,
    Failed,
}

public sealed record RetainedAnalysisPayloadSeal(
    string PayloadId,
    string SchemaId,
    string SchemaVersion,
    string Sha256,
    long ByteLength);

public sealed record AnalysisV1WorkAssignment(
    int SchemaVersion,
    string AssignmentId,
    AnalysisExecutionInputContract ExecutionInput,
    string AnalysisContextId,
    RetainedAnalysisPayloadSeal DocumentationEvidence,
    RetainedAnalysisPayloadSeal CandidateAnalysis,
    RetainedAnalysisPayloadSeal FindingCase,
    string ImplementationCommit,
    DateTimeOffset StartedAt,
    AnalysisTerminalOutcome TerminalOutcome,
    string TerminalReason,
    long MaximumInputBytes,
    long MaximumOutputBytes,
    long MaximumQueryItems)
{
    public const int CurrentSchemaVersion = 1;
    public const long MaximumAssignmentBytes = 1024 * 1024;
    public const long AbsoluteMaximumInputBytes = 192L * 1024 * 1024;
    public const long AbsoluteMaximumOutputBytes = 384L * 1024;
    public const long MinimumTerminalOutputBytes = 64L * 1024;
    public const long AbsoluteMaximumQueryResponseBytes = 896L * 1024;
    public const long AbsoluteMaximumQueryItems = 100;
}

public sealed record ExternalBoundaryReceipt(
    int SchemaVersion,
    string RunId,
    IReadOnlyDictionary<string, string> Effects,
    string Reason);

public sealed record AnalysisPublicationBundle(
    AnalysisReplayContract Replay,
    RunOutputContract RunOutput,
    CliSummaryDocumentContract CliSummary,
    ExternalBoundaryReceipt ExternalBoundaries,
    string DependencyClosureId,
    string SemanticOutputFingerprint,
    IReadOnlyList<AnalysisPublishedArtifact> Artifacts);

public sealed record AnalysisPublishedArtifact(
    string ArtifactId,
    string Kind,
    string SchemaId,
    string SchemaVersion,
    long Revision,
    string State,
    string ContentSha256,
    long ByteLength,
    string ProvenanceId,
    string DependencyClosureId);

public sealed record AnalysisWorkerValidationReceipt(
    int SchemaVersion,
    string AssignmentId,
    string RunId,
    IReadOnlyList<RetainedAnalysisPayloadSeal> ValidatedInputs,
    long TotalInputBytes,
    IReadOnlyDictionary<string, string> ExternalEffects,
    string Disposition);

public sealed class AnalysisOutputLimitException : InvalidOperationException
{
    public AnalysisOutputLimitException(string message) : base(message) { }
    public AnalysisOutputLimitException(string message, Exception innerException) : base(message, innerException) { }
}
