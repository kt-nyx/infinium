using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.Documentation;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class DocumentationEvidenceTypesProvenanceLocalUntrustedDocumentationTests
{
    private static readonly UtcTimestamp ImportedAt = new(
        new DateTimeOffset(2026, 8, 8, 18, 30, 0, TimeSpan.Zero));

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    public void EvidenceTypesCoreFixtureMatchesIndependentOracle()
    {
        DocumentationFixturePackage package = Read("DOC-WP2-CORE-DEV");
        JsonElement binding = package.CaseMatrix.GetProperty("execution_binding");
        DocumentationEvidenceContract actual = DocumentationEvidenceImporter.Import(
            CleanRequest(package, binding));
        JsonElement expected = package.Oracle;

        AssertCoreSemantics(package, expected, actual);

        Assert.AreEqual(expected.GetProperty("payload_id").GetString(), actual.PayloadId.Value);
        AssertCounts(expected.GetProperty("expected_counts"), actual);
        Assert.AreEqual(
            expected.GetProperty("revision").GetProperty("revision_id").GetString(),
            actual.Revisions.Single().RevisionId.Value);
        Assert.AreEqual(
            expected.GetProperty("import").GetProperty("import_id").GetString(),
            actual.Imports.Single().ImportId.Value);
        Assert.IsNull(actual.Imports.Single().ReusedImportId);
        AssertExpectedIds(expected.GetProperty("passages"), "passage_id", actual.Passages.Select(item => item.PassageId.Value));
        AssertExpectedIds(expected.GetProperty("claims"), "claim_id", actual.Claims.Select(item => item.ClaimId.Value));
        AssertExpectedIds(expected.GetProperty("applications"), "application_id", actual.Applications.Select(item => item.ApplicationId.Value));
        AssertExpectedIds(expected.GetProperty("gaps"), "gap_id", actual.Gaps.Select(item => item.GapId.Value));
        Assert.AreEqual(9, actual.Passages.Count);
        Assert.AreEqual(
            actual.Claims.Single(item => item.Kind == ClaimKind.InstallationInstruction).PassageId,
            actual.Claims.Single(item => item.Kind == ClaimKind.PriorityInstruction).PassageId);

        foreach (JsonElement expectedApplication in expected.GetProperty("applications").EnumerateArray())
        {
            ClaimApplicationContract application = actual.Applications.Single(item =>
                item.ApplicationId.Value == expectedApplication.GetProperty("application_id").GetString());
            AssertExpectedStrings(
                expectedApplication.GetProperty("evidence_ids"),
                application.EvidenceIds.Select(item => item.Value));
        }

        DocumentationPurposeAssignmentContract assignment = actual.PurposeAssignments.Single();
        JsonElement expectedAssignment = expected.GetProperty("purpose_assignment");
        Assert.AreEqual(expectedAssignment.GetProperty("assignment_id").GetString(), assignment.AssignmentId.Value);
        Assert.AreEqual("declared-purpose-and-intended-feature-area", assignment.Axis);
        Assert.AreEqual("purpose-kind", assignment.Facet);
        Assert.AreEqual(new ContractVersion(0, 1, 0), assignment.TaxonomyVersion);
        Assert.IsTrue(actual.Imports.Single().Boundaries.All(item => item.State == BoundaryUseState.NotUsed));
        Assert.AreEqual(LlmInvolvementState.None, actual.Imports.Single().LlmInvolvement);
        Assert.AreEqual(LlmOperation.None, actual.Imports.Single().LlmOperation);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    public void UntrustedDocumentationRemainsInertAndRetainedReuseRecordsLoss()
    {
        DocumentationFixturePackage package = Read("DOC-WP2-ADVERSARIAL-VAL");
        JsonElement cleanBinding = package.CaseMatrix.GetProperty("clean_execution_binding");
        DocumentationImportRequestContract cleanRequest = CleanRequest(package, cleanBinding);
        DocumentationEvidenceContract clean = DocumentationEvidenceImporter.Import(cleanRequest);
        JsonElement expectedClean = package.Oracle.GetProperty("clean");

        AssertAdversarialCleanSemantics(package, expectedClean, clean);

        Assert.AreEqual(expectedClean.GetProperty("payload_id").GetString(), clean.PayloadId.Value);
        Assert.AreEqual(expectedClean.GetProperty("revision_id").GetString(), clean.Revisions.Single().RevisionId.Value);
        Assert.AreEqual(expectedClean.GetProperty("import_id").GetString(), clean.Imports.Single().ImportId.Value);
        AssertExpectedIds(expectedClean.GetProperty("passages"), "passage_id", clean.Passages.Select(item => item.PassageId.Value));
        AssertExpectedIds(expectedClean.GetProperty("claims"), "claim_id", clean.Claims.Select(item => item.ClaimId.Value));
        Assert.AreEqual(expectedClean.GetProperty("application_id").GetString(), clean.Applications.Single().ApplicationId.Value);
        Assert.IsTrue(clean.Claims.All(item =>
            item.Applicability is ClaimApplicabilityState.Unsupported or ClaimApplicabilityState.Unknown));
        string sourceText = Encoding.UTF8.GetString(package.SourceBytes.Span);
        Assert.IsTrue(clean.Claims.All(item => sourceText.Contains(item.ExactText, StringComparison.Ordinal)));
        Assert.IsTrue(clean.Imports.Single().Boundaries.All(item => item.State == BoundaryUseState.NotUsed));

        AssertReuse(package, clean, "retained-reuse-deleted", "retained_reuse_deleted");
        AssertReuse(package, clean, "retained-reuse-unavailable", "retained_reuse_unavailable");

        byte[] invalidUtf8 = package.SourceBytes.ToArray();
        invalidUtf8[0] = 0xc3;
        invalidUtf8[1] = 0x28;
        DocumentationClaimImportManifestContract invalidManifest = package.ClaimImport with
        {
            Claims = [],
            Applications = [],
            ByteFingerprint = new Sha256Fingerprint(Convert.ToHexStringLower(SHA256.HashData(invalidUtf8))),
            ByteLength = invalidUtf8.Length,
        };
        DocumentationImportFailureException invalidFailure = Assert.ThrowsExactly<DocumentationImportFailureException>(() =>
            DocumentationEvidenceImporter.Import(cleanRequest with
            {
                Manifest = invalidManifest,
                SourceBytes = invalidUtf8,
                AcceptedApplicationTargets = [],
            }));
        Assert.AreEqual("invalid-utf8", invalidFailure.Failure.FailureCode);
        Assert.IsFalse(invalidFailure.Failure.Retryable);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            DocumentationEvidenceImporter.Import(cleanRequest with
            {
                Manifest = package.ClaimImport with
                {
                    SourceKind = DocumentationSourceKind.ProjectAuthoredLocal,
                    SupplyingSnapshotId = null,
                },
            }));
        DocumentationEvidenceContract fixtureWithoutSnapshot = DocumentationEvidenceImporter.Import(cleanRequest with
        {
            Manifest = package.ClaimImport with { SupplyingSnapshotId = null },
        });
        Assert.IsNull(fixtureWithoutSnapshot.Revisions.Single().SupplyingSnapshotId);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void ProvenanceLocalPackagesAreFrozenAnswerIsolatedAndCurrent()
    {
        foreach (string fixtureId in new[] { "DOC-WP2-CORE-DEV", "DOC-WP2-ADVERSARIAL-VAL" })
        {
            DocumentationFixturePackage package = Read(fixtureId);
            JsonElement isolation = package.Provenance.GetProperty("answer_isolation");
            Assert.IsTrue(isolation.GetProperty("authored_before_product_comparison").GetBoolean());
            Assert.IsFalse(isolation.GetProperty("product_output_used").GetBoolean());
            Assert.IsFalse(isolation.GetProperty("product_importer_inspected").GetBoolean());
            Assert.AreEqual("accepted", package.PublicManifest.GetProperty("review_state").GetString());
            JsonElement downstream = package.Oracle.GetProperty("downstream_objects");
            Assert.IsTrue(downstream.EnumerateObject().All(item => item.Value.GetString() == "not-created-by-wp2"));
        }
    }

    private static void AssertReuse(
        DocumentationFixturePackage package,
        DocumentationEvidenceContract clean,
        string caseId,
        string expectedProperty)
    {
        JsonElement matrixCase = package.CaseMatrix.GetProperty("cases").EnumerateArray()
            .Single(item => item.GetProperty("case_id").GetString() == caseId);
        DocumentationSourceAvailability availability = matrixCase.GetProperty("availability").GetString() switch
        {
            "deleted" => DocumentationSourceAvailability.Deleted,
            "unavailable" => DocumentationSourceAvailability.Unavailable,
            _ => throw new InvalidDataException("Fixture reuse availability is not closed."),
        };
        DocumentationClaimImportManifestContract manifest = package.ClaimImport with
        {
            Availability = availability,
            Claims = [],
            Applications = [],
        };
        DocumentationImportRequestContract request = new(
            new OpaqueId(matrixCase.GetProperty("originating_run_id").GetString()!),
            new OpaqueId(matrixCase.GetProperty("import_run_id").GetString()!),
            DocumentationImportMode.RetainedReuse,
            new OpaqueId(package.CaseMatrix.GetProperty("clean_execution_binding").GetProperty("dependency_closure_id").GetString()!),
            new OpaqueId(package.CaseMatrix.GetProperty("clean_execution_binding").GetProperty("extractor_id").GetString()!),
            ImportedAt,
            manifest,
            null,
            clean,
            []);
        DocumentationEvidenceContract actual = DocumentationEvidenceImporter.Import(request);
        JsonElement expected = package.Oracle.GetProperty(expectedProperty);
        AssertReuseSemantics(expected, actual, clean);
        Assert.AreEqual(expected.GetProperty("payload_id").GetString(), actual.PayloadId.Value);
        Assert.AreEqual(expected.GetProperty("import_id").GetString(), actual.Imports.Single().ImportId.Value);
        Assert.AreEqual(expected.GetProperty("reused_import_id").GetString(), actual.Imports.Single().ReusedImportId?.Value);
        AssertExpectedStrings(expected.GetProperty("preserved_passage_ids"), actual.Passages.Select(item => item.PassageId.Value));
        AssertExpectedStrings(expected.GetProperty("preserved_claim_ids"), actual.Claims.Select(item => item.ClaimId.Value));
        AssertExpectedStrings(expected.GetProperty("preserved_application_ids"), actual.Applications.Select(item => item.ApplicationId.Value));
        AssertExpectedStrings(expected.GetProperty("gap_ids"), actual.Gaps.Select(item => item.GapId.Value));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            DocumentationEvidenceImporter.Import(request with { SourceBytes = package.SourceBytes }));
    }

    private static DocumentationImportRequestContract CleanRequest(
        DocumentationFixturePackage package,
        JsonElement binding) =>
        new(
            new OpaqueId(binding.GetProperty("originating_run_id").GetString()!),
            new OpaqueId(binding.GetProperty("import_run_id").GetString()!),
            DocumentationImportMode.CleanImport,
            new OpaqueId(binding.GetProperty("dependency_closure_id").GetString()!),
            new OpaqueId(binding.GetProperty("extractor_id").GetString()!),
            ImportedAt,
            package.ClaimImport,
            package.SourceBytes,
            null,
            package.CaseMatrix.GetProperty("application_targets").EnumerateArray().Select(target =>
                new DocumentationApplicationTargetContract(
                    new OpaqueId(target.GetProperty("consuming_run_id").GetString()!),
                    new OpaqueId(target.GetProperty("installation_snapshot_id").GetString()!),
                    new OpaqueId(target.GetProperty("analysis_context_id").GetString()!),
                    new OpaqueId(target.GetProperty("resolved_input_manifest_id").GetString()!),
                    new OpaqueId(target.GetProperty("subject_id").GetString()!),
                    target.GetProperty("subject_type").GetString()!,
                    new OpaqueId(target.GetProperty("dependency_closure_id").GetString()!)))
                .Distinct()
                .ToArray());

    private static DocumentationFixturePackage Read(string fixtureId) =>
        DocumentationFixturePackageReader.Read(TestRepository.PathFromRoot(
            "test-data", "evaluation", "m1-semantic", fixtureId));

    private static void AssertCounts(JsonElement expected, DocumentationEvidenceContract actual)
    {
        Assert.AreEqual(expected.GetProperty("revisions").GetInt32(), actual.Revisions.Count);
        Assert.AreEqual(expected.GetProperty("imports").GetInt32(), actual.Imports.Count);
        Assert.AreEqual(expected.GetProperty("passages").GetInt32(), actual.Passages.Count);
        Assert.AreEqual(expected.GetProperty("claims").GetInt32(), actual.Claims.Count);
        Assert.AreEqual(expected.GetProperty("applications").GetInt32(), actual.Applications.Count);
        Assert.AreEqual(expected.GetProperty("purpose_assignments").GetInt32(), actual.PurposeAssignments.Count);
        Assert.AreEqual(expected.GetProperty("gaps").GetInt32(), actual.Gaps.Count);
        Assert.AreEqual(expected.GetProperty("deletion_receipts").GetInt32(), actual.DeletionReceipts.Count);
        Assert.AreEqual(expected.GetProperty("failures").GetInt32(), actual.Failures.Count);
    }

    private static void AssertCoreSemantics(
        DocumentationFixturePackage package,
        JsonElement expected,
        DocumentationEvidenceContract actual)
    {
        AssertRevision(expected.GetProperty("revision"), actual.Revisions.Single());
        AssertImport(expected.GetProperty("import"), actual.Imports.Single());
        AssertPassages(expected.GetProperty("passages"), actual.Passages, actual.Revisions.Single().RevisionId);
        AssertClaims(expected.GetProperty("claims"), package.ClaimImport.Claims, actual.Claims);
        AssertApplications(
            expected.GetProperty("applications"),
            package.ClaimImport.Applications,
            expected.GetProperty("claims"),
            actual.Applications);

        JsonElement expectedPurpose = expected.GetProperty("purpose_assignment");
        DocumentationPurposeAssignmentContract purpose = actual.PurposeAssignments.Single();
        Assert.AreEqual(expectedPurpose.GetProperty("assignment_id").GetString(), purpose.AssignmentId.Value);
        Assert.AreEqual(expectedPurpose.GetProperty("taxonomy_id").GetString(), purpose.TaxonomyId);
        Assert.AreEqual(expectedPurpose.GetProperty("taxonomy_version").GetString(), purpose.TaxonomyVersion.ToString());
        Assert.AreEqual(expectedPurpose.GetProperty("axis").GetString(), purpose.Axis);
        Assert.AreEqual(expectedPurpose.GetProperty("facet").GetString(), purpose.Facet);
        Assert.AreEqual(expectedPurpose.GetProperty("code").GetString(), purpose.Code);
        Assert.AreEqual(TaxonomyApplicability.Assigned, purpose.Applicability);
        Assert.AreEqual(expectedPurpose.GetProperty("role").GetString(), RoleToken(purpose.Role));
        Assert.AreEqual(expectedPurpose.GetProperty("claim_id").GetString(), purpose.ClaimId.Value);
        Assert.AreEqual(expectedPurpose.GetProperty("application_id").GetString(), purpose.ApplicationId.Value);
        AssertExpectedStrings(expectedPurpose.GetProperty("applicability_condition_ids"),
            purpose.ApplicabilityConditionIds.Select(item => item.Value));
        Assert.AreEqual(expectedPurpose.GetProperty("analyzer_or_adjudicator_id").GetString(),
            purpose.AnalyzerOrAdjudicatorId.Value);
        Assert.AreEqual(expectedPurpose.GetProperty("created_at").GetString(), purpose.CreatedAt.ToString());
        Assert.AreEqual(expectedPurpose.GetProperty("reason").GetString(), purpose.Reason);
        DocumentationApplicationInputContract purposeInput = package.ClaimImport.Applications.Single(item =>
            item.DeclaredPurpose is not null);
        Assert.AreEqual(purposeInput.SubjectId, purpose.SubjectId);
        Assert.AreEqual(purposeInput.SubjectType, purpose.SubjectType);

        AssertGaps(expected.GetProperty("gaps"), actual.Gaps, actual.OriginatingRunId,
            actual.Revisions.Single().RevisionId);
        Assert.HasCount(0, actual.DeletionReceipts);
        Assert.HasCount(0, actual.Failures);
    }

    private static void AssertAdversarialCleanSemantics(
        DocumentationFixturePackage package,
        JsonElement expected,
        DocumentationEvidenceContract actual)
    {
        Assert.AreEqual(expected.GetProperty("source_sha256").GetString(),
            actual.Revisions.Single().ByteFingerprint.Value);
        Assert.AreEqual(expected.GetProperty("source_byte_length").GetInt64(),
            actual.Revisions.Single().ByteLength);
        Assert.AreEqual(expected.GetProperty("revision_id").GetString(),
            actual.Revisions.Single().RevisionId.Value);
        Assert.AreEqual(expected.GetProperty("import_id").GetString(), actual.Imports.Single().ImportId.Value);
        Assert.AreEqual(expected.GetProperty("created_at").GetString(), actual.Imports.Single().CreatedAt.ToString());
        Assert.IsNull(actual.Imports.Single().ReusedImportId);
        AssertPassages(expected.GetProperty("passages"), actual.Passages, actual.Revisions.Single().RevisionId);
        AssertClaims(expected.GetProperty("claims"), package.ClaimImport.Claims, actual.Claims);

        DocumentationApplicationInputContract applicationInput = package.ClaimImport.Applications.Single();
        ClaimApplicationContract application = actual.Applications.Single();
        Assert.AreEqual(expected.GetProperty("application_id").GetString(), application.ApplicationId.Value);
        Assert.AreEqual(
            expected.GetProperty("claims").EnumerateArray().Single(item =>
                item.GetProperty("claim_key").GetString() == applicationInput.ClaimKey.Value)
                .GetProperty("claim_id").GetString(),
            application.ClaimId.Value);
        Assert.AreEqual(applicationInput.ConsumingRunId, application.ConsumingRunId);
        Assert.AreEqual(applicationInput.AnalysisContextId, application.AnalysisContextId);
        Assert.AreEqual(applicationInput.SubjectId, application.SubjectId);
        Assert.AreEqual(applicationInput.SubjectType, application.SubjectType);
        Assert.AreEqual(applicationInput.DependencyClosureId, application.DependencyClosureId);
        Assert.AreEqual(applicationInput.Applicability, application.Applicability);
        AssertExpectedStrings(expected.GetProperty("application_evidence_ids"),
            application.EvidenceIds.Select(item => item.Value));
        Assert.HasCount(0, actual.PurposeAssignments);
        Assert.HasCount(0, actual.Gaps);
        Assert.HasCount(0, actual.DeletionReceipts);
        Assert.HasCount(0, actual.Failures);
    }

    private static void AssertReuseSemantics(
        JsonElement expected,
        DocumentationEvidenceContract actual,
        DocumentationEvidenceContract retained)
    {
        Assert.AreEqual(expected.GetProperty("revision_id").GetString(), actual.Revisions.Single().RevisionId.Value);
        Assert.AreEqual(expected.GetProperty("import_id").GetString(), actual.Imports.Single().ImportId.Value);
        Assert.AreEqual(expected.GetProperty("reused_import_id").GetString(), actual.Imports.Single().ReusedImportId?.Value);
        Assert.AreEqual(DocumentationImportMode.RetainedReuse, actual.Imports.Single().Mode);
        Assert.AreEqual(ImportedAt.ToString(), actual.Imports.Single().CreatedAt.ToString());
        CollectionAssert.AreEqual(retained.Passages.ToArray(), actual.Passages.ToArray());
        CollectionAssert.AreEqual(retained.Claims.ToArray(), actual.Claims.ToArray());
        CollectionAssert.AreEqual(retained.Applications.ToArray(), actual.Applications.ToArray());
        CollectionAssert.AreEqual(retained.PurposeAssignments.ToArray(), actual.PurposeAssignments.ToArray());
        AssertGaps(expected.GetProperty("gaps"), actual.Gaps, actual.OriginatingRunId,
            actual.Revisions.Single().RevisionId);

        JsonElement expectedReceipts = expected.GetProperty("deletion_receipts");
        Assert.AreEqual(expectedReceipts.GetArrayLength(), actual.DeletionReceipts.Count);
        foreach (JsonElement expectedReceipt in expectedReceipts.EnumerateArray())
        {
            DocumentationDeletionReceiptContract receipt = actual.DeletionReceipts.Single(item =>
                item.ReceiptId.Value == expectedReceipt.GetProperty("receipt_id").GetString());
            Assert.AreEqual(expectedReceipt.GetProperty("originating_run_id").GetString(), receipt.OriginatingRunId.Value);
            Assert.AreEqual(expectedReceipt.GetProperty("revision_id").GetString(), receipt.RevisionId.Value);
            Assert.AreEqual(expectedReceipt.GetProperty("deleted_body_fingerprint").GetString(),
                receipt.DeletedBodyFingerprint.Value);
            AssertExpectedStrings(expectedReceipt.GetProperty("deleted_passage_ids"),
                receipt.DeletedPassageIds.Select(item => item.Value));
            AssertExpectedStrings(expectedReceipt.GetProperty("independently_retained_payload_ids"),
                receipt.IndependentlyRetainedPayloadIds.Select(item => item.Value));
            Assert.AreEqual(expectedReceipt.GetProperty("replay_effect").GetString(), ReplayToken(receipt.ReplayEffect));
            Assert.AreEqual(expectedReceipt.GetProperty("deleted_at").GetString(), receipt.DeletedAt.ToString());
            Assert.AreEqual(expectedReceipt.GetProperty("reason").GetString(), receipt.Reason);
        }
        Assert.HasCount(0, actual.Failures);
    }

    private static void AssertRevision(JsonElement expected, DocumentationRevisionContract actual)
    {
        Assert.AreEqual(expected.GetProperty("revision_id").GetString(), actual.RevisionId.Value);
        Assert.AreEqual(expected.GetProperty("source_id").GetString(), actual.SourceId.Value);
        Assert.AreEqual(expected.GetProperty("source_kind").GetString(), SourceKindToken(actual.SourceKind));
        Assert.AreEqual(expected.GetProperty("source_revision").GetString(), actual.SourceRevision);
        Assert.AreEqual(expected.GetProperty("byte_fingerprint").GetString(), actual.ByteFingerprint.Value);
        Assert.AreEqual(expected.GetProperty("byte_length").GetInt64(), actual.ByteLength);
        Assert.AreEqual(expected.GetProperty("supplying_snapshot_id").GetString(), actual.SupplyingSnapshotId?.Value);
        Assert.AreEqual(expected.GetProperty("retention_state").GetString(), ResultStateToken(actual.RetentionState));
        Assert.AreEqual(expected.GetProperty("replay_state").GetString(), ReplayToken(actual.ReplayState));
    }

    private static void AssertImport(JsonElement expected, DocumentationImportContract actual)
    {
        Assert.AreEqual(expected.GetProperty("import_id").GetString(), actual.ImportId.Value);
        Assert.AreEqual(expected.GetProperty("import_run_id").GetString(), actual.ImportRunId.Value);
        Assert.AreEqual(expected.GetProperty("mode").GetString(), ImportModeToken(actual.Mode));
        Assert.IsNull(actual.ReusedImportId);
        Assert.AreEqual(expected.GetProperty("dependency_closure_id").GetString(), actual.DependencyClosureId.Value);
        Assert.AreEqual(expected.GetProperty("extractor_id").GetString(), actual.ExtractorId.Value);
        Assert.AreEqual(expected.GetProperty("created_at").GetString(), actual.CreatedAt.ToString());
        Assert.AreEqual("none", expected.GetProperty("llm_involvement").GetString());
        Assert.AreEqual(LlmInvolvementState.None, actual.LlmInvolvement);
        Assert.AreEqual("none", expected.GetProperty("llm_operation").GetString());
        Assert.AreEqual(LlmOperation.None, actual.LlmOperation);
        foreach (JsonElement boundary in expected.GetProperty("boundaries").EnumerateArray())
        {
            ExecutionBoundaryContract actualBoundary = actual.Boundaries.Single(item =>
                item.BoundaryId == boundary.GetProperty("boundary_id").GetString());
            Assert.AreEqual(boundary.GetProperty("state").GetString(), BoundaryToken(actualBoundary.State));
        }
    }

    private static void AssertPassages(
        JsonElement expected,
        IReadOnlyList<DocumentationPassageContract> actual,
        OpaqueId revisionId)
    {
        Assert.AreEqual(expected.GetArrayLength(), actual.Count);
        foreach (JsonElement item in expected.EnumerateArray())
        {
            DocumentationPassageContract passage = actual.Single(candidate =>
                candidate.PassageId.Value == item.GetProperty("passage_id").GetString());
            Assert.AreEqual(revisionId, passage.RevisionId);
            Assert.AreEqual(item.GetProperty("utf8_start_offset").GetInt64(), passage.Utf8StartOffset);
            Assert.AreEqual(item.GetProperty("utf8_end_offset").GetInt64(), passage.Utf8EndOffset);
            Assert.AreEqual(item.GetProperty("passage_fingerprint").GetString(), passage.PassageFingerprint.Value);
            Assert.AreEqual(Slice5ResultState.Present, passage.State);
        }
    }

    private static void AssertClaims(
        JsonElement expected,
        IReadOnlyList<DocumentationClaimInputContract> inputs,
        IReadOnlyList<DocumentationClaimContract> actual)
    {
        Assert.AreEqual(expected.GetArrayLength(), actual.Count);
        foreach (JsonElement item in expected.EnumerateArray())
        {
            DocumentationClaimInputContract input = inputs.Single(candidate =>
                candidate.ClaimKey.Value == item.GetProperty("claim_key").GetString());
            DocumentationClaimContract claim = actual.Single(candidate =>
                candidate.PassageId.Value == item.GetProperty("passage_id").GetString()
                && candidate.Kind == input.Kind
                && StringComparer.Ordinal.Equals(candidate.ExactText, input.ExactText));
            Assert.AreEqual(item.GetProperty("claim_id").GetString(), claim.ClaimId.Value);
            Assert.AreEqual(item.GetProperty("producing_import_id").GetString(), claim.ProducingImportId.Value);
            Assert.AreEqual(item.GetProperty("passage_id").GetString(), claim.PassageId.Value);
            Assert.AreEqual(input.ExactText, claim.ExactText);
            Assert.AreEqual(input.Kind, claim.Kind);
            if (item.TryGetProperty("kind", out JsonElement kind))
            {
                Assert.AreEqual(kind.GetString(), ClaimKindToken(claim.Kind));
            }
            AssertExpectedStrings(input.Conditions, claim.Conditions);
            Assert.AreEqual(input.Authority, claim.Authority);
            Assert.AreEqual(input.Applicability, claim.Applicability);
            if (item.TryGetProperty("applicability", out JsonElement applicability))
            {
                Assert.AreEqual(applicability.GetString(), ApplicabilityToken(claim.Applicability));
            }
            Assert.AreEqual(input.ClassificationRole, claim.ClassificationRole);
            if (item.TryGetProperty("contradicting_evidence_ids", out JsonElement contradictions))
            {
                AssertExpectedStrings(contradictions, claim.ContradictingEvidenceIds.Select(id => id.Value));
            }
            else
            {
                Assert.HasCount(0, claim.ContradictingEvidenceIds);
            }
        }
    }

    private static void AssertApplications(
        JsonElement expected,
        IReadOnlyList<DocumentationApplicationInputContract> inputs,
        JsonElement expectedClaims,
        IReadOnlyList<ClaimApplicationContract> actual)
    {
        Assert.AreEqual(expected.GetArrayLength(), actual.Count);
        foreach (JsonElement item in expected.EnumerateArray())
        {
            DocumentationApplicationInputContract input = inputs.Single(candidate =>
                candidate.ClaimKey.Value == item.GetProperty("claim_key").GetString());
            ClaimApplicationContract application = actual.Single(candidate =>
                candidate.ApplicationId.Value == item.GetProperty("application_id").GetString());
            Assert.AreEqual(item.GetProperty("application_id").GetString(), application.ApplicationId.Value);
            string expectedClaimId = expectedClaims.EnumerateArray().Single(candidate =>
                candidate.GetProperty("claim_key").GetString() == input.ClaimKey.Value)
                .GetProperty("claim_id").GetString()!;
            Assert.AreEqual(expectedClaimId, application.ClaimId.Value);
            Assert.AreEqual(input.ConsumingRunId, application.ConsumingRunId);
            Assert.AreEqual(input.AnalysisContextId, application.AnalysisContextId);
            Assert.AreEqual(input.SubjectId, application.SubjectId);
            Assert.AreEqual(input.SubjectType, application.SubjectType);
            Assert.AreEqual(input.DependencyClosureId, application.DependencyClosureId);
            Assert.AreEqual(input.Applicability, application.Applicability);
            if (item.TryGetProperty("applicability", out JsonElement applicability))
            {
                Assert.AreEqual(applicability.GetString(), ApplicabilityToken(application.Applicability));
            }
            AssertExpectedStrings(item.GetProperty("evidence_ids"), application.EvidenceIds.Select(id => id.Value));
        }
    }

    private static void AssertGaps(
        JsonElement expected,
        IReadOnlyList<DocumentationGapContract> actual,
        OpaqueId originatingRunId,
        OpaqueId revisionId)
    {
        Assert.AreEqual(expected.GetArrayLength(), actual.Count);
        foreach (JsonElement item in expected.EnumerateArray())
        {
            DocumentationGapContract gap = actual.Single(candidate =>
                candidate.GapId.Value == item.GetProperty("gap_id").GetString());
            Assert.AreEqual(originatingRunId, gap.OriginatingRunId);
            Assert.AreEqual(revisionId, gap.RevisionId);
            Assert.AreEqual(item.GetProperty("kind").GetString(), GapKindToken(gap.Kind));
            Assert.AreEqual(item.TryGetProperty("claim_id", out JsonElement claim) ? claim.GetString() : null,
                gap.ClaimId?.Value);
            Assert.AreEqual(item.TryGetProperty("application_id", out JsonElement application)
                    ? application.GetString() : null,
                gap.ApplicationId?.Value);
            Assert.AreEqual(item.GetProperty("replay_effect").GetString(), ReplayToken(gap.ReplayEffect));
            Assert.AreEqual(item.GetProperty("created_at").GetString(), gap.CreatedAt.ToString());
            Assert.AreEqual(item.GetProperty("reason").GetString(), gap.Reason);
        }
    }

    private static string SourceKindToken(DocumentationSourceKind value) => value switch
    {
        DocumentationSourceKind.ProjectAuthoredLocal => "project-authored-local",
        DocumentationSourceKind.Fixture => "fixture",
        _ => throw new InvalidOperationException(),
    };

    private static string ResultStateToken(Slice5ResultState value) => value switch
    {
        Slice5ResultState.Present => "present",
        Slice5ResultState.Partial => "partial",
        Slice5ResultState.Unavailable => "unavailable",
        _ => throw new InvalidOperationException(),
    };

    private static string ReplayToken(ReplayState value) => value switch
    {
        ReplayState.CompleteClean => "complete-clean",
        ReplayState.Partial => "partial",
        ReplayState.AuditOnly => "audit-only",
        ReplayState.Unavailable => "unavailable",
        ReplayState.FailedIdentityDrift => "failed-identity-drift",
        _ => throw new InvalidOperationException(),
    };

    private static string GapKindToken(DocumentationGapKind value) => value switch
    {
        DocumentationGapKind.Contradiction => "contradiction",
        DocumentationGapKind.Deletion => "deletion",
        DocumentationGapKind.UnavailableSource => "unavailable-source",
        DocumentationGapKind.Replay => "replay",
        _ => throw new InvalidOperationException(),
    };

    private static string ImportModeToken(DocumentationImportMode value) => value switch
    {
        DocumentationImportMode.CleanImport => "clean-import",
        DocumentationImportMode.RetainedReuse => "retained-reuse",
        _ => throw new InvalidOperationException(),
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
        _ => throw new InvalidOperationException(),
    };

    private static string ApplicabilityToken(ClaimApplicabilityState value) => value switch
    {
        ClaimApplicabilityState.Applicable => "applicable",
        ClaimApplicabilityState.NotApplicable => "not-applicable",
        ClaimApplicabilityState.Unknown => "unknown",
        ClaimApplicabilityState.Unsupported => "unsupported",
        ClaimApplicabilityState.Contradicted => "contradicted",
        _ => throw new InvalidOperationException(),
    };

    private static string RoleToken(ClassificationRole value) => value switch
    {
        ClassificationRole.Declared => "declared",
        ClassificationRole.Observed => "observed",
        ClassificationRole.Predicted => "predicted",
        ClassificationRole.Established => "established",
        _ => throw new InvalidOperationException(),
    };

    private static string BoundaryToken(BoundaryUseState value) => value switch
    {
        BoundaryUseState.NotUsed => "not-used",
        BoundaryUseState.Used => "used",
        BoundaryUseState.Unsupported => "unsupported",
        _ => throw new InvalidOperationException(),
    };

    private static void AssertExpectedIds(JsonElement expected, string property, IEnumerable<string> actual) =>
        AssertExpectedStrings(expected.EnumerateArray().Select(item => item.GetProperty(property).GetString()!), actual);

    private static void AssertExpectedStrings(JsonElement expected, IEnumerable<string> actual) =>
        AssertExpectedStrings(expected.EnumerateArray().Select(item => item.GetString()!), actual);

    private static void AssertExpectedStrings(IEnumerable<string> expected, IEnumerable<string> actual) =>
        CollectionAssert.AreEquivalent(expected.ToArray(), actual.ToArray());
}
