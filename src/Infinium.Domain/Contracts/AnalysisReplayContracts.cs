namespace Infinium.Domain.Contracts;

public sealed record ReplayDependencyNodeContract(
    OpaqueId DependencyId,
    string Kind,
    ContractVersion Version,
    Sha256Fingerprint Fingerprint,
    AnalysisResultState State);

public sealed record ReplayDependencyEdgeContract(OpaqueId From, OpaqueId To);

public sealed record ReplayOutputContract(
    OpaqueId ArtifactId,
    string SchemaId,
    ContractVersion SchemaVersion,
    Sha256Fingerprint SemanticFingerprint,
    Sha256Fingerprint ByteFingerprint);

public sealed record AnalysisReplayContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId ReplayManifestId,
    OpaqueId OriginatingRunId,
    ReplayMode Mode,
    ReplayState ReplayState,
    AuditabilityState AuditabilityState,
    IReadOnlyList<ReplayDependencyNodeContract> Dependencies,
    IReadOnlyList<ReplayDependencyEdgeContract> Edges,
    IReadOnlyList<ReplayOutputContract> Outputs,
    IReadOnlyList<OpaqueId> MissingDependencyIds,
    IReadOnlyList<OpaqueId> CoverageGapIds,
    bool SemanticallyEquivalent,
    OpaqueId? ComparedRunId);
