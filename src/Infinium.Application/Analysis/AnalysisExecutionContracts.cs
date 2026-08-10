using Infinium.Analysis.Candidates;
using Infinium.Analysis.Conclusions;
using Infinium.Application.FindingCases;
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
    SemanticAnalysisContextContract AnalysisContext,
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
    public IReadOnlyList<AnalysisPhaseExecution> PhaseExecutions { get; init; } = [];
    public IReadOnlyList<OpaqueId> DocumentationDependencyIds { get; init; } = [];
    public DateTimeOffset? ExecutionDeadline { get; init; }
}

public sealed record CandidatePhaseParameters(
    OpaqueId PopulationId,
    OpaqueId PolicyId,
    OpaqueId ThresholdId,
    CandidateExecutionLimits Limits);

public sealed record FindingCasePhaseParameters(
    OpaqueId PromotionPolicyId,
    ContractVersion PromotionPolicyVersion,
    OpaqueId ReconciliationPolicyId,
    ContractVersion ReconciliationPolicyVersion,
    OpaqueId ReconciliationActorId,
    UtcTimestamp AssessmentTime,
    IReadOnlyList<FindingEvidenceFactContract> FindingEvidenceFacts,
    IReadOnlyList<FindingRecommendationFactContract> FindingRecommendationFacts,
    IReadOnlyList<SharedCauseProofContract> SharedCauseProofs,
    IReadOnlyList<TaxonomySubjectFact> TaxonomySubjects,
    IReadOnlyList<TaxonomyClassificationFactContract> RetainedTaxonomyFacts,
    IReadOnlyList<TaxonomyProjectionInputContract> TaxonomyProjectionInputs,
    IReadOnlyList<CoveragePopulationFactContract> CoveragePopulationFacts,
    IReadOnlyList<CoverageMemberFactContract> CoverageMemberFacts,
    IReadOnlyList<CoverageFailureFactContract> CoverageFailureFacts,
    IReadOnlyList<PriorFindingContract> PriorFindings,
    IReadOnlyList<PriorCaseContract> PriorCases,
    IReadOnlyList<ProducerCompatibilityContract> ProducerCompatibilities,
    IReadOnlyList<RelatedFindingFactContract> RelatedFindingFacts,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries)
{
    public IReadOnlyList<ReconciliationCandidateFactContract> ReconciliationCandidateFacts { get; init; } = [];

    public FindingCaseInputBuildRequest Bind(CandidateAnalysisContract candidateAnalysis) => new(
        PromotionPolicyId, PromotionPolicyVersion, ReconciliationPolicyId,
        ReconciliationPolicyVersion, ReconciliationActorId, AssessmentTime,
        candidateAnalysis, FindingEvidenceFacts, FindingRecommendationFacts,
        SharedCauseProofs, TaxonomySubjects, RetainedTaxonomyFacts,
        TaxonomyProjectionInputs, CoveragePopulationFacts, CoverageMemberFacts,
        CoverageFailureFacts, PriorFindings, PriorCases, ProducerCompatibilities,
        RelatedFindingFacts, Boundaries)
    {
        ReconciliationCandidateFacts = ReconciliationCandidateFacts,
    };
}

public sealed record ManagedAnalysisOrchestrationRequest(
    int SchemaVersion,
    string RequestId,
    AnalysisExecutionInputContract ExecutionInput,
    SemanticAnalysisContextContract AnalysisContext,
    DocumentationImportRequestContract DocumentationImport,
    CandidatePhaseParameters Candidate,
    FindingCasePhaseParameters FindingCase,
    string ImplementationCommit,
    DateTimeOffset StartedAt,
    AnalysisTerminalOutcome TerminalOutcome,
    string TerminalReason,
    long MaximumInputBytes,
    long MaximumOutputBytes,
    long MaximumQueryItems)
{
    public const int CurrentSchemaVersion = 1;
    public const long MaximumRequestBytes = 896L * 1024;
}

public sealed record AnalysisPhaseExecution(
    string PhaseId,
    string InputFingerprint,
    RetainedAnalysisPayloadSeal Output,
    string Disposition,
    string SourceRunId);

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
