using System.Security.Cryptography;
using System.Text;

namespace Infinium.Domain.Contracts;

public static class DocumentationEvidenceIdentity
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static OpaqueId ComputePayloadId(DocumentationEvidenceContract evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return StableId(
            "docevidence",
            evidence.SchemaId,
            evidence.SchemaVersion.ToString(),
            evidence.OriginatingRunId.Value,
            CanonicalInOrder(evidence.Revisions.Select(RevisionDescriptor)),
            CanonicalInOrder(evidence.Imports.Select(ImportDescriptor)),
            CanonicalInOrder(evidence.Passages.Select(PassageDescriptor)),
            CanonicalInOrder(evidence.Claims.Select(ClaimDescriptor)),
            CanonicalInOrder(evidence.Applications.Select(ApplicationDescriptor)),
            CanonicalInOrder(evidence.PurposeAssignments.Select(PurposeDescriptor)),
            CanonicalInOrder(evidence.DeletionReceipts.Select(DeletionDescriptor)),
            CanonicalInOrder(evidence.Gaps.Select(GapDescriptor)),
            CanonicalInOrder(evidence.Failures.Select(FailureDescriptor)));
    }

    private static string RevisionDescriptor(DocumentationRevisionContract revision) =>
        CanonicalInOrder(
            revision.RevisionId.Value,
            revision.SourceId.Value,
            SourceKindToken(revision.SourceKind),
            revision.SourceRevision,
            revision.ByteFingerprint.Value,
            revision.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            revision.SupplyingSnapshotId?.Value ?? "none",
            ResultStateToken(revision.RetentionState),
            ReplayStateToken(revision.ReplayState));

    private static string ImportDescriptor(DocumentationImportContract import) =>
        CanonicalInOrder(
            import.ImportId.Value,
            import.ImportRunId.Value,
            import.RevisionId.Value,
            ImportModeToken(import.Mode),
            import.ReusedImportId?.Value ?? "none",
            import.DependencyClosureId.Value,
            import.ExtractorId.Value,
            LlmInvolvementToken(import.LlmInvolvement),
            LlmOperationToken(import.LlmOperation),
            CanonicalInOrder(import.Boundaries.Select(BoundaryDescriptor)),
            import.CreatedAt.ToString());

    private static string PassageDescriptor(DocumentationPassageContract passage) =>
        CanonicalInOrder(
            passage.PassageId.Value,
            passage.RevisionId.Value,
            passage.Utf8StartOffset.ToString(System.Globalization.CultureInfo.InvariantCulture),
            passage.Utf8EndOffset.ToString(System.Globalization.CultureInfo.InvariantCulture),
            passage.PassageFingerprint.Value,
            ResultStateToken(passage.State));

    private static string ClaimDescriptor(DocumentationClaimContract claim) =>
        CanonicalInOrder(
            claim.ClaimId.Value,
            claim.ProducingImportId.Value,
            claim.PassageId.Value,
            ClaimKindToken(claim.Kind),
            claim.ExactText,
            CanonicalInOrder(claim.Conditions),
            AuthorityToken(claim.Authority),
            ApplicabilityToken(claim.Applicability),
            RoleToken(claim.ClassificationRole),
            CanonicalInOrder(claim.ContradictingEvidenceIds.Select(item => item.Value)));

    private static string ApplicationDescriptor(ClaimApplicationContract application) =>
        CanonicalInOrder(
            application.ApplicationId.Value,
            application.ClaimId.Value,
            application.ConsumingRunId.Value,
            application.AnalysisContextId.Value,
            application.SubjectId.Value,
            application.SubjectType,
            application.DependencyClosureId.Value,
            ApplicabilityToken(application.Applicability),
            CanonicalInOrder(application.EvidenceIds.Select(item => item.Value)));

    private static string PurposeDescriptor(DocumentationPurposeAssignmentContract purpose) =>
        CanonicalInOrder(
            purpose.AssignmentId.Value,
            purpose.TaxonomyId,
            purpose.TaxonomyVersion.ToString(),
            purpose.Axis,
            purpose.Facet,
            purpose.Code,
            TaxonomyApplicabilityToken(purpose.Applicability),
            purpose.SubjectId.Value,
            purpose.SubjectType,
            RoleToken(purpose.Role),
            purpose.ClaimId.Value,
            purpose.ApplicationId.Value,
            CanonicalInOrder(purpose.ApplicabilityConditionIds.Select(item => item.Value)),
            purpose.AnalyzerOrAdjudicatorId.Value,
            purpose.CreatedAt.ToString(),
            purpose.Reason);

    private static string DeletionDescriptor(DocumentationDeletionReceiptContract receipt) =>
        CanonicalInOrder(
            receipt.ReceiptId.Value,
            receipt.OriginatingRunId.Value,
            receipt.RevisionId.Value,
            receipt.DeletedBodyFingerprint.Value,
            CanonicalInOrder(receipt.DeletedPassageIds.Select(item => item.Value)),
            CanonicalInOrder(receipt.IndependentlyRetainedPayloadIds.Select(item => item.Value)),
            ReplayStateToken(receipt.ReplayEffect),
            receipt.DeletedAt.ToString(),
            receipt.Reason);

    private static string GapDescriptor(DocumentationGapContract gap) =>
        CanonicalInOrder(
            gap.GapId.Value,
            gap.OriginatingRunId.Value,
            DocumentationGapKindToken(gap.Kind),
            gap.RevisionId.Value,
            gap.ClaimId?.Value ?? "none",
            gap.ApplicationId?.Value ?? "none",
            ReplayStateToken(gap.ReplayEffect),
            gap.Reason,
            gap.CreatedAt.ToString());

    private static string BoundaryDescriptor(ExecutionBoundaryContract boundary) =>
        CanonicalInOrder(
            boundary.BoundaryId,
            BoundaryStateToken(boundary.State),
            boundary.Reason);

    private static string FailureDescriptor(DocumentationFailureContract failure) =>
        CanonicalInOrder(
            failure.FailureId.Value,
            failure.FailureCode,
            failure.Message,
            failure.Retryable ? "true" : "false");

    private static string CanonicalInOrder(params string[] values) =>
        string.Concat(values.Select(Frame));

    private static string CanonicalInOrder(IEnumerable<string> values) =>
        string.Concat(values.Select(Frame));

    private static string Frame(string value) =>
        FormattableString.Invariant($"{Utf8.GetByteCount(value)}:{value}");

    private static OpaqueId StableId(string prefix, params string[] values)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string value in values)
        {
            byte[] bytes = Utf8.GetBytes(value);
            hash.AppendData(BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(bytes.Length)));
            hash.AppendData(bytes);
        }
        return new OpaqueId(prefix + "-" + Convert.ToHexStringLower(hash.GetHashAndReset())[..32]);
    }

    private static string ResultStateToken(AnalysisResultState value) => value switch
    {
        AnalysisResultState.Present => "present",
        AnalysisResultState.Partial => "partial",
        AnalysisResultState.Unavailable => "unavailable",
        _ => throw new InvalidOperationException("Documentation retention state is not closed."),
    };

    private static string SourceKindToken(DocumentationSourceKind value) => value switch
    {
        DocumentationSourceKind.ProjectAuthoredLocal => "project-authored-local",
        DocumentationSourceKind.Fixture => "fixture",
        _ => throw new InvalidOperationException("Documentation source kind is not closed."),
    };

    private static string ImportModeToken(DocumentationImportMode value) => value switch
    {
        DocumentationImportMode.CleanImport => "clean-import",
        DocumentationImportMode.RetainedReuse => "retained-reuse",
        _ => throw new InvalidOperationException("Documentation import mode is not closed."),
    };

    private static string LlmInvolvementToken(LlmInvolvementState value) => value switch
    {
        LlmInvolvementState.None => "none",
        _ => throw new InvalidOperationException("LLM involvement is not closed."),
    };

    private static string LlmOperationToken(LlmOperation value) => value switch
    {
        LlmOperation.None => "none",
        _ => throw new InvalidOperationException("LLM operation is not closed."),
    };

    private static string BoundaryStateToken(BoundaryUseState value) => value switch
    {
        BoundaryUseState.Used => "used",
        BoundaryUseState.NotUsed => "not-used",
        BoundaryUseState.Unsupported => "unsupported",
        _ => throw new InvalidOperationException("Execution boundary state is not closed."),
    };

    private static string ClaimKindToken(ClaimKind value) => value switch
    {
        ClaimKind.DeclaredPurpose => "declared-purpose",
        ClaimKind.Requirement => "requirement",
        ClaimKind.Incompatibility => "incompatibility",
        ClaimKind.InstallationInstruction => "installation-instruction",
        ClaimKind.PriorityInstruction => "priority-instruction",
        ClaimKind.LifecycleInstruction => "lifecycle-instruction",
        ClaimKind.ConfigurationInstruction => "configuration-instruction",
        ClaimKind.PatchInstruction => "patch-instruction",
        ClaimKind.KnownIssue => "known-issue",
        _ => throw new InvalidOperationException("Documentation claim kind is not closed."),
    };

    private static string AuthorityToken(EvidenceAuthority value) => value switch
    {
        EvidenceAuthority.AuthoritativeExternal => "authoritative-external",
        _ => throw new InvalidOperationException("Documentation evidence authority is not closed."),
    };

    private static string ApplicabilityToken(ClaimApplicabilityState value) => value switch
    {
        ClaimApplicabilityState.Applicable => "applicable",
        ClaimApplicabilityState.NotApplicable => "not-applicable",
        ClaimApplicabilityState.Unknown => "unknown",
        ClaimApplicabilityState.Unsupported => "unsupported",
        ClaimApplicabilityState.Contradicted => "contradicted",
        _ => throw new InvalidOperationException("Documentation applicability is not closed."),
    };

    private static string RoleToken(ClassificationRole value) => value switch
    {
        ClassificationRole.Declared => "declared",
        ClassificationRole.Observed => "observed",
        ClassificationRole.Predicted => "predicted",
        ClassificationRole.Established => "established",
        _ => throw new InvalidOperationException("Documentation role is not closed."),
    };

    private static string TaxonomyApplicabilityToken(TaxonomyApplicability value) => value switch
    {
        TaxonomyApplicability.Assigned => "assigned",
        TaxonomyApplicability.Unknown => "unknown",
        TaxonomyApplicability.Unsupported => "unsupported",
        TaxonomyApplicability.Unmapped => "unmapped",
        TaxonomyApplicability.NotApplicable => "not-applicable",
        _ => throw new InvalidOperationException("Taxonomy applicability is not closed."),
    };

    private static string DocumentationGapKindToken(DocumentationGapKind value) => value switch
    {
        DocumentationGapKind.Contradiction => "contradiction",
        DocumentationGapKind.Deletion => "deletion",
        DocumentationGapKind.UnavailableSource => "unavailable-source",
        DocumentationGapKind.Replay => "replay",
        _ => throw new InvalidOperationException("Documentation gap kind is not closed."),
    };

    private static string ReplayStateToken(ReplayState value) => value switch
    {
        ReplayState.CompleteClean => "complete-clean",
        ReplayState.Partial => "partial",
        ReplayState.AuditOnly => "audit-only",
        ReplayState.Unavailable => "unavailable",
        ReplayState.FailedIdentityDrift => "failed-identity-drift",
        _ => throw new InvalidOperationException("Documentation replay state is not closed."),
    };
}
