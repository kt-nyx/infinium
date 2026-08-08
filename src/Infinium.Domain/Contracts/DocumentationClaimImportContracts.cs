namespace Infinium.Domain.Contracts;

public sealed record DocumentationClaimImportManifestContract(
    string SchemaId,
    ContractVersion SchemaVersion,
    OpaqueId SourceId,
    DocumentationSourceKind SourceKind,
    string SourceRevision,
    DocumentationSourceAvailability Availability,
    Sha256Fingerprint ByteFingerprint,
    long ByteLength,
    OpaqueId? SupplyingSnapshotId,
    IReadOnlyList<DocumentationClaimInputContract> Claims,
    IReadOnlyList<DocumentationApplicationInputContract> Applications);

public sealed record DocumentationClaimInputContract(
    OpaqueId ClaimKey,
    long Utf8StartOffset,
    long Utf8EndOffset,
    string ExactText,
    ClaimKind Kind,
    IReadOnlyList<string> Conditions,
    EvidenceAuthority Authority,
    ClaimApplicabilityState Applicability,
    ClassificationRole ClassificationRole,
    IReadOnlyList<OpaqueId> ContradictingClaimKeys);

public sealed record DocumentationApplicationInputContract(
    OpaqueId ClaimKey,
    OpaqueId ConsumingRunId,
    OpaqueId AnalysisContextId,
    OpaqueId SubjectId,
    string SubjectType,
    OpaqueId DependencyClosureId,
    ClaimApplicabilityState Applicability,
    IReadOnlyList<OpaqueId> SupportingClaimKeys,
    DocumentationPurposeInputContract? DeclaredPurpose);

public sealed record DocumentationPurposeInputContract(
    string Code,
    IReadOnlyList<OpaqueId> ApplicabilityConditionIds,
    OpaqueId AnalyzerOrAdjudicatorId,
    string Reason);

public sealed record DocumentationApplicationTargetContract(
    OpaqueId ConsumingRunId,
    OpaqueId InstallationSnapshotId,
    OpaqueId AnalysisContextId,
    OpaqueId ResolvedInputManifestId,
    OpaqueId SubjectId,
    string SubjectType,
    OpaqueId DependencyClosureId);

public sealed record DocumentationImportRequestContract(
    OpaqueId OriginatingRunId,
    OpaqueId ImportRunId,
    DocumentationImportMode Mode,
    OpaqueId DependencyClosureId,
    OpaqueId ExtractorId,
    UtcTimestamp ImportedAt,
    DocumentationClaimImportManifestContract Manifest,
    ReadOnlyMemory<byte>? SourceBytes,
    DocumentationEvidenceContract? RetainedEvidence,
    IReadOnlyList<DocumentationApplicationTargetContract> AcceptedApplicationTargets);

public static class DocumentationClaimImportContractInvariants
{
    private static readonly HashSet<string> PurposeCodes = new(StringComparer.Ordinal)
    {
        "purpose.add-expand",
        "purpose.replace-overhaul",
        "purpose.modify-tune",
        "purpose.fix-restore",
        "purpose.integrate-patch",
        "purpose.configure-expose-choice",
        "purpose.generate-precompute",
        "purpose.provide-runtime-framework",
        "purpose.provide-tool-workflow",
        "purpose.remove-disable",
    };

    public static void Validate(DocumentationClaimImportManifestContract value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaId != ContractConstants.DocumentationClaimImportSchemaId
            || value.SchemaVersion != new ContractVersion(1, 0, 0)
            || value.SourceKind == DocumentationSourceKind.Unspecified
            || value.Availability == DocumentationSourceAvailability.Unspecified
            || string.IsNullOrWhiteSpace(value.SourceRevision)
            || value.ByteLength < 0
            || value.ByteLength > 8 * 1024 * 1024
            || value.Claims.Count > 10_000
            || value.Applications.Count > 10_000
            || (value.SourceKind == DocumentationSourceKind.ProjectAuthoredLocal
                && value.SupplyingSnapshotId is null))
        {
            throw new InvalidOperationException("Documentation claim input requires the exact closed v1 source contract.");
        }

        if (value.Claims.Select(item => item.ClaimKey).Distinct().Count() != value.Claims.Count)
        {
            throw new InvalidOperationException("Documentation claim keys must be unique.");
        }

        HashSet<OpaqueId> claimKeys = value.Claims.Select(item => item.ClaimKey).ToHashSet();
        long selectedPassageBytes = value.Claims
            .Select(item => (item.Utf8StartOffset, item.Utf8EndOffset))
            .Distinct()
            .Sum(item => item.Utf8EndOffset - item.Utf8StartOffset);
        long contradictionReferences = value.Claims.Sum(item => (long)item.ContradictingClaimKeys.Count);
        long applicationReferences = value.Applications.Sum(item =>
            (long)item.SupportingClaimKeys.Count
            + (item.DeclaredPurpose?.ApplicabilityConditionIds.Count ?? 0));
        long declaredTextBytes = value.Claims.Sum(item =>
            (long)System.Text.Encoding.UTF8.GetByteCount(item.ExactText ?? string.Empty));
        long conditionAndReasonBytes = value.Claims.Sum(item => item.Conditions.Sum(condition =>
                (long)System.Text.Encoding.UTF8.GetByteCount(condition ?? string.Empty)))
            + value.Applications.Sum(item => item.DeclaredPurpose is null
                ? 0L
                : System.Text.Encoding.UTF8.GetByteCount(item.DeclaredPurpose.Reason ?? string.Empty));
        if (selectedPassageBytes > 32L * 1024 * 1024
            || declaredTextBytes > 32L * 1024 * 1024
            || conditionAndReasonBytes > 4L * 1024 * 1024
            || contradictionReferences > 100_000
            || applicationReferences > 100_000)
        {
            throw new InvalidOperationException(
                "Documentation claim input exceeds the aggregate passage or reference work bound.");
        }
        foreach (DocumentationClaimInputContract claim in value.Claims)
        {
            if (claim.Utf8StartOffset < 0
                || claim.Utf8EndOffset <= claim.Utf8StartOffset
                || claim.Utf8EndOffset > value.ByteLength
                || string.IsNullOrWhiteSpace(claim.ExactText)
                || claim.Kind == ClaimKind.Unspecified
                || claim.Authority != EvidenceAuthority.AuthoritativeExternal
                || claim.Applicability == ClaimApplicabilityState.Unspecified
                || claim.ClassificationRole == ClassificationRole.Unspecified
                || (claim.Kind == ClaimKind.DeclaredPurpose
                    && claim.ClassificationRole != ClassificationRole.Declared)
                || claim.Conditions.Count > 100
                || claim.Conditions.Any(string.IsNullOrWhiteSpace)
                || claim.Conditions.Distinct(StringComparer.Ordinal).Count() != claim.Conditions.Count
                || claim.ContradictingClaimKeys.Count > 100
                || claim.ContradictingClaimKeys.Distinct().Count() != claim.ContradictingClaimKeys.Count)
            {
                throw new InvalidOperationException("Documentation claims require bounded passages and closed external-claim authority.");
            }
            if (!claim.ContradictingClaimKeys.All(claimKeys.Contains)
                || claim.ContradictingClaimKeys.Contains(claim.ClaimKey))
            {
                throw new InvalidOperationException("Contradiction references must resolve to another claim in the same admitted manifest.");
            }
        }

        foreach (DocumentationApplicationInputContract application in value.Applications)
        {
            if (!claimKeys.Contains(application.ClaimKey)
                || !StringComparer.Ordinal.Equals(application.SubjectType, "installed-entity")
                || application.Applicability == ClaimApplicabilityState.Unspecified
                || application.SupportingClaimKeys.Count > 100
                || application.SupportingClaimKeys.Distinct().Count() != application.SupportingClaimKeys.Count)
            {
                throw new InvalidOperationException("Documentation applications require an admitted claim and explicit subject/applicability.");
            }
            if (!application.SupportingClaimKeys.All(claimKeys.Contains))
            {
                throw new InvalidOperationException("Application evidence references must resolve to admitted manifest claims.");
            }

            if (application.DeclaredPurpose is not null
                && (application.DeclaredPurpose.ApplicabilityConditionIds.Count > 100
                    || application.DeclaredPurpose.ApplicabilityConditionIds.Distinct().Count()
                        != application.DeclaredPurpose.ApplicabilityConditionIds.Count
                    || !application.DeclaredPurpose.ApplicabilityConditionIds.All(claimKeys.Contains)))
            {
                throw new InvalidOperationException(
                    "Purpose applicability-condition references must resolve to admitted manifest claims.");
            }

            if (application.DeclaredPurpose is not null
                && (!PurposeCodes.Contains(application.DeclaredPurpose.Code)
                    || string.IsNullOrWhiteSpace(application.DeclaredPurpose.Reason)))
            {
                throw new InvalidOperationException("Declared-purpose assignments require a closed taxonomy code and evidence reason.");
            }
        }
    }
}
