namespace Infinium.Persistence;

public sealed record DocumentationEvidencePersistenceReceipt(
    string EvidenceId,
    string PayloadId,
    string RevisionId,
    string ImportId,
    int ClaimCount,
    int ApplicationCount,
    int PurposeAssignmentCount,
    int DeletionReceiptCount,
    int GapCount);
