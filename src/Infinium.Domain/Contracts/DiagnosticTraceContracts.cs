namespace Infinium.Domain.Contracts;

public sealed record DiagnosticTraceFieldContract(
    string Name,
    DiagnosticDataClass DataClass,
    DiagnosticFieldRedaction Redaction,
    System.Text.Json.JsonElement Value);

public sealed record DiagnosticTraceEventContract(
    long Sequence,
    UtcTimestamp Timestamp,
    DiagnosticSeverity Severity,
    OpaqueId ComponentId,
    string EventCode,
    string Message,
    IReadOnlyList<DiagnosticTraceFieldContract> Fields,
    IReadOnlyList<ArtifactReferenceContract> PayloadReferences);

public sealed record DiagnosticTraceContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId TraceId,
    OpaqueId RunId,
    DiagnosticSensitivityLabel SensitivityLabel,
    DiagnosticSharingClass SharingClass,
    bool CredentialMaterialPresent,
    DiagnosticRedactionState RedactionState,
    UtcTimestamp CreatedAt,
    IReadOnlyList<DiagnosticTraceEventContract> Events);
