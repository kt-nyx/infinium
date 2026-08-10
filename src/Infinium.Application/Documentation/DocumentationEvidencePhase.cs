using Infinium.Analysis.Documentation;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Application.Documentation;

public static class DocumentationEvidencePhase
{
    public const string PhaseId = "documentation-evidence";
    public const string ExtractorVersion = "1.0.0";

    public static DocumentationEvidencePhaseResult Execute(
        AuthoritativeStore store,
        DocumentationImportRequestContract request)
    {
        ArgumentNullException.ThrowIfNull(store);
        DocumentationEvidenceContract evidence = DocumentationEvidenceImporter.Import(request);
        store.AdmitDocumentationApplicationTargets(
            request.AcceptedApplicationTargets,
            request.ImportedAt.Value);
        evidence = store.PrepareDocumentationDeletionEvidence(evidence);
        byte[] payload = DocumentationEvidenceJsonCodec.Serialize(evidence);
        DocumentationEvidencePersistenceReceipt receipt = store.PublishDocumentationEvidence(
            evidence,
            request.Mode == DocumentationImportMode.CleanImport ? request.SourceBytes : null,
            payload,
            request.ImportedAt.Value);
        return new DocumentationEvidencePhaseResult(evidence, receipt, payload);
    }
}

public sealed record DocumentationEvidencePhaseResult(
    DocumentationEvidenceContract Evidence,
    DocumentationEvidencePersistenceReceipt Receipt,
    byte[] SerializedPayload);
