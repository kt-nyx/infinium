namespace Infinium.Domain.Contracts;

public sealed record DocumentationRevisionContract(
    OpaqueId RevisionId,
    OpaqueId SourceId,
    DocumentationSourceKind SourceKind,
    string SourceRevision,
    Sha256Fingerprint ByteFingerprint,
    long ByteLength,
    OpaqueId? SupplyingSnapshotId,
    AnalysisResultState RetentionState,
    ReplayState ReplayState);

public sealed record DocumentationImportContract(
    OpaqueId ImportId,
    OpaqueId ImportRunId,
    OpaqueId RevisionId,
    DocumentationImportMode Mode,
    OpaqueId? ReusedImportId,
    OpaqueId DependencyClosureId,
    OpaqueId ExtractorId,
    LlmInvolvementState LlmInvolvement,
    LlmOperation LlmOperation,
    IReadOnlyList<ExecutionBoundaryContract> Boundaries,
    UtcTimestamp CreatedAt);

public sealed record DocumentationPassageContract(
    OpaqueId PassageId,
    OpaqueId RevisionId,
    long Utf8StartOffset,
    long Utf8EndOffset,
    Sha256Fingerprint PassageFingerprint,
    AnalysisResultState State);

public sealed record DocumentationClaimContract(
    OpaqueId ClaimId,
    OpaqueId ProducingImportId,
    OpaqueId PassageId,
    ClaimKind Kind,
    string ExactText,
    IReadOnlyList<string> Conditions,
    EvidenceAuthority Authority,
    ClaimApplicabilityState Applicability,
    ClassificationRole ClassificationRole,
    IReadOnlyList<OpaqueId> ContradictingEvidenceIds);

public sealed record ClaimApplicationContract(
    OpaqueId ApplicationId,
    OpaqueId ClaimId,
    OpaqueId ConsumingRunId,
    OpaqueId AnalysisContextId,
    OpaqueId SubjectId,
    string SubjectType,
    OpaqueId DependencyClosureId,
    ClaimApplicabilityState Applicability,
    IReadOnlyList<OpaqueId> EvidenceIds);

public sealed record DocumentationGapContract(
    OpaqueId GapId,
    OpaqueId OriginatingRunId,
    DocumentationGapKind Kind,
    OpaqueId RevisionId,
    OpaqueId? ClaimId,
    OpaqueId? ApplicationId,
    ReplayState ReplayEffect,
    string Reason,
    UtcTimestamp CreatedAt);

public sealed record DocumentationDeletionReceiptContract(
    OpaqueId ReceiptId,
    OpaqueId OriginatingRunId,
    OpaqueId RevisionId,
    Sha256Fingerprint DeletedBodyFingerprint,
    IReadOnlyList<OpaqueId> DeletedPassageIds,
    IReadOnlyList<OpaqueId> IndependentlyRetainedPayloadIds,
    ReplayState ReplayEffect,
    UtcTimestamp DeletedAt,
    string Reason);

public sealed record DocumentationPurposeAssignmentContract(
    OpaqueId AssignmentId,
    string TaxonomyId,
    ContractVersion TaxonomyVersion,
    string Axis,
    string Facet,
    string Code,
    TaxonomyApplicability Applicability,
    OpaqueId SubjectId,
    string SubjectType,
    ClassificationRole Role,
    OpaqueId ClaimId,
    OpaqueId ApplicationId,
    IReadOnlyList<OpaqueId> ApplicabilityConditionIds,
    OpaqueId AnalyzerOrAdjudicatorId,
    UtcTimestamp CreatedAt,
    string Reason);

public sealed record DocumentationFailureContract(
    OpaqueId FailureId,
    string FailureCode,
    string Message,
    bool Retryable);

public sealed record DocumentationEvidenceContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId PayloadId,
    OpaqueId OriginatingRunId,
    IReadOnlyList<DocumentationRevisionContract> Revisions,
    IReadOnlyList<DocumentationImportContract> Imports,
    IReadOnlyList<DocumentationPassageContract> Passages,
    IReadOnlyList<DocumentationClaimContract> Claims,
    IReadOnlyList<ClaimApplicationContract> Applications,
    IReadOnlyList<DocumentationPurposeAssignmentContract> PurposeAssignments,
    IReadOnlyList<DocumentationDeletionReceiptContract> DeletionReceipts,
    IReadOnlyList<DocumentationGapContract> Gaps,
    IReadOnlyList<DocumentationFailureContract> Failures);
