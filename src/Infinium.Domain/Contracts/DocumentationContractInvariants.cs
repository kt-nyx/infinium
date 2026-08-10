using static Infinium.Domain.Contracts.AnalysisContractInvariantHelpers;

namespace Infinium.Domain.Contracts;

public static class DocumentationEvidenceContractInvariants
{
    private static readonly HashSet<string> DocumentationPurposeCodes = new(StringComparer.Ordinal)
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

    public static void Validate(DocumentationEvidenceContract value)
    {
        RequireHeader(value.SchemaId, value.SchemaVersion, ContractConstants.DocumentationEvidenceSchemaId);
        RequireUnique(value.Revisions.Select(item => item.RevisionId), "documentation revisions");
        RequireUnique(value.Imports.Select(item => item.ImportId), "documentation imports");
        RequireUnique(value.Passages.Select(item => item.PassageId), "documentation passages");
        RequireUnique(value.Claims.Select(item => item.ClaimId), "documentation claims");
        RequireUnique(value.Applications.Select(item => item.ApplicationId), "claim applications");
        HashSet<OpaqueId> revisions = value.Revisions.Select(item => item.RevisionId).ToHashSet();
        HashSet<OpaqueId> passages = value.Passages.Select(item => item.PassageId).ToHashSet();
        HashSet<OpaqueId> claims = value.Claims.Select(item => item.ClaimId).ToHashSet();
        HashSet<OpaqueId> producingImports = value.Imports
            .SelectMany(item => item.ReusedImportId is null
                ? new[] { item.ImportId }
                : new[] { item.ImportId, item.ReusedImportId })
            .OfType<OpaqueId>()
            .ToHashSet();
        foreach (DocumentationRevisionContract revision in value.Revisions)
        {
            if (revision.SourceKind == DocumentationSourceKind.Unspecified
                || string.IsNullOrWhiteSpace(revision.SourceRevision)
                || revision.ByteLength < 0
                || revision.RetentionState is not (AnalysisResultState.Present or AnalysisResultState.Partial or AnalysisResultState.Unavailable)
                || revision.ReplayState == ReplayState.Unspecified
                || (revision.SourceKind == DocumentationSourceKind.ProjectAuthoredLocal
                    && revision.SupplyingSnapshotId is null))
            {
                throw new InvalidOperationException("Documentation revisions require closed source/revision, local supplying-snapshot, retention, and replay state.");
            }
        }
        foreach (DocumentationImportContract import in value.Imports)
        {
            if (!revisions.Contains(import.RevisionId)
                || import.ImportRunId != value.OriginatingRunId
                || import.Mode == DocumentationImportMode.Unspecified
                || (import.Mode == DocumentationImportMode.CleanImport && import.ReusedImportId is not null)
                || (import.Mode == DocumentationImportMode.RetainedReuse
                    && (import.ReusedImportId is null || import.ReusedImportId == import.ImportId))
                || import.LlmInvolvement != LlmInvolvementState.None
                || import.LlmOperation != LlmOperation.None)
            {
                throw new InvalidOperationException("Documentation imports require an admitted revision, closed mode, and explicit llm = none.");
            }
            ExecutionBoundaryContractInvariants.ValidateProductCapabilities(import.Boundaries, requireNotUsed: true);
        }
        if (!revisions.SetEquals(value.Imports.Select(item => item.RevisionId)))
        {
            throw new InvalidOperationException("Every documentation revision requires at least one explicit import or retained-reuse record.");
        }
        foreach (DocumentationPassageContract passage in value.Passages)
        {
            if (!revisions.Contains(passage.RevisionId)
                || passage.Utf8StartOffset < 0
                || passage.Utf8EndOffset <= passage.Utf8StartOffset)
            {
                throw new InvalidOperationException("Passages require an existing revision and a non-empty UTF-8 byte range.");
            }
        }
        foreach (DocumentationClaimContract claim in value.Claims)
        {
            if (!passages.Contains(claim.PassageId)
                || !producingImports.Contains(claim.ProducingImportId)
                || claim.Kind == ClaimKind.Unspecified
                || claim.Authority != EvidenceAuthority.AuthoritativeExternal
                || claim.Applicability == ClaimApplicabilityState.Unspecified
                || claim.ClassificationRole == ClassificationRole.Unspecified
                || (claim.Kind == ClaimKind.DeclaredPurpose && claim.ClassificationRole != ClassificationRole.Declared)
                || !claim.ContradictingEvidenceIds.All(claims.Contains)
                || claim.ContradictingEvidenceIds.Contains(claim.ClaimId)
                || claim.ContradictingEvidenceIds.Distinct().Count() != claim.ContradictingEvidenceIds.Count)
            {
                throw new InvalidOperationException("Claims require admitted passages and closed authority, applicability, kind, and role states.");
            }
        }
        foreach (ClaimApplicationContract application in value.Applications)
        {
            if (!claims.Contains(application.ClaimId)
                || application.Applicability == ClaimApplicabilityState.Unspecified
                || !StringComparer.Ordinal.Equals(application.SubjectType, "installed-entity")
                || !application.EvidenceIds.All(claims.Contains)
                || !application.EvidenceIds.Contains(application.ClaimId)
                || application.EvidenceIds.Distinct().Count() != application.EvidenceIds.Count)
            {
                throw new InvalidOperationException(
                    "Claim applications require an existing claim and a closed applicability state.");
            }
        }
        HashSet<OpaqueId> applications = value.Applications.Select(item => item.ApplicationId).ToHashSet();
        RequireUnique(value.PurposeAssignments.Select(item => item.AssignmentId), "documentation purpose assignments");
        foreach (DocumentationPurposeAssignmentContract assignment in value.PurposeAssignments)
        {
            DocumentationClaimContract? purposeClaim = value.Claims.SingleOrDefault(item => item.ClaimId == assignment.ClaimId);
            if (assignment.TaxonomyId != ContractConstants.TaxonomyId
                || assignment.Role != ClassificationRole.Declared
                || assignment.Axis != "declared-purpose-and-intended-feature-area"
                || assignment.Facet != "purpose-kind"
                || assignment.TaxonomyVersion != new ContractVersion(0, 1, 0)
                || assignment.Applicability != TaxonomyApplicability.Assigned
                || !DocumentationPurposeCodes.Contains(assignment.Code)
                || !StringComparer.Ordinal.Equals(assignment.SubjectType, "installed-entity")
                || !claims.Contains(assignment.ClaimId)
                || !applications.Contains(assignment.ApplicationId)
                || purposeClaim is null
                || purposeClaim.Kind != ClaimKind.DeclaredPurpose
                || purposeClaim.Authority != EvidenceAuthority.AuthoritativeExternal
                || purposeClaim.ClassificationRole != ClassificationRole.Declared
                || purposeClaim.Applicability != ClaimApplicabilityState.Applicable
                || !assignment.ApplicabilityConditionIds.All(claims.Contains)
                || assignment.ApplicabilityConditionIds.Distinct().Count()
                    != assignment.ApplicabilityConditionIds.Count
                || !value.Applications.Any(item =>
                    item.ApplicationId == assignment.ApplicationId
                    && item.ClaimId == assignment.ClaimId
                    && item.SubjectId == assignment.SubjectId
                    && StringComparer.Ordinal.Equals(item.SubjectType, assignment.SubjectType)
                    && item.Applicability == ClaimApplicabilityState.Applicable))
            {
                throw new InvalidOperationException("Purpose assignments require declared-purpose taxonomy authority and admitted claim evidence.");
            }
        }
        RequireUnique(value.Gaps.Select(item => item.GapId), "documentation gaps");
        foreach (DocumentationGapContract gap in value.Gaps)
        {
            if (gap.Kind == DocumentationGapKind.Unspecified
                || gap.OriginatingRunId != value.OriginatingRunId
                || !revisions.Contains(gap.RevisionId)
                || (gap.ClaimId is not null && !claims.Contains(gap.ClaimId))
                || (gap.ApplicationId is not null && !applications.Contains(gap.ApplicationId))
                || gap.ReplayEffect == ReplayState.Unspecified
                || string.IsNullOrWhiteSpace(gap.Reason))
            {
                throw new InvalidOperationException("Documentation gaps require admitted references and closed gap/replay semantics.");
            }
        }
        RequireUnique(value.DeletionReceipts.Select(item => item.ReceiptId), "documentation deletion receipts");
        if (value.DeletionReceipts.Count != 0
            && !value.Imports.Any(item => item.Mode == DocumentationImportMode.RetainedReuse))
        {
            throw new InvalidOperationException(
                "Documentation deletion receipts require retained-reuse provenance over prior admitted evidence.");
        }
        foreach (DocumentationDeletionReceiptContract receipt in value.DeletionReceipts)
        {
            if (receipt.OriginatingRunId != value.OriginatingRunId
                || !revisions.Contains(receipt.RevisionId)
                || receipt.DeletedPassageIds.Distinct().Count() != receipt.DeletedPassageIds.Count
                || !receipt.DeletedPassageIds.All(passages.Contains)
                || receipt.IndependentlyRetainedPayloadIds.Distinct().Count()
                    != receipt.IndependentlyRetainedPayloadIds.Count
                || receipt.ReplayEffect is not (ReplayState.AuditOnly or ReplayState.Unavailable)
                || string.IsNullOrWhiteSpace(receipt.Reason))
            {
                throw new InvalidOperationException("Documentation deletion receipts require exact retained identity and replay effects.");
            }
        }
        if (value.Gaps.Any(item => item.Kind == DocumentationGapKind.Deletion)
            != (value.DeletionReceipts.Count != 0))
        {
            throw new InvalidOperationException("Documentation deletion gaps and receipts must be emitted together.");
        }
        if (value.Claims.Any(claim =>
                (claim.Applicability == ClaimApplicabilityState.Contradicted
                 || claim.ContradictingEvidenceIds.Count != 0)
                && !value.Gaps.Any(gap =>
                    gap.Kind == DocumentationGapKind.Contradiction
                    && gap.ClaimId == claim.ClaimId))
            || value.Applications.Any(application =>
                application.Applicability == ClaimApplicabilityState.Contradicted
                && !value.Gaps.Any(gap =>
                    gap.Kind == DocumentationGapKind.Contradiction
                    && gap.ApplicationId == application.ApplicationId)))
        {
            throw new InvalidOperationException("Contradicted documentation claims and applications require explicit contradiction gaps.");
        }
        RequireUnique(value.Failures.Select(item => item.FailureId), "documentation failures");
        if (value.Failures.Any(item =>
                string.IsNullOrWhiteSpace(item.FailureCode)
                || item.FailureCode.Length > 128
                || string.IsNullOrWhiteSpace(item.Message)
                || item.Message.Length > 512))
        {
            throw new InvalidOperationException("Documentation failures require a code and bounded diagnostic message.");
        }
        if (DocumentationEvidenceIdentity.ComputePayloadId(value) != value.PayloadId)
        {
            throw new InvalidOperationException(
                "Documentation evidence payload identity must cover the exact aggregate semantics.");
        }
    }

}
