using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ResultsReviewApplicationContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void ResultsAndReviewSurfaceIsClosedBoundedAndPathFree()
    {
        string application = TestRepository.Read(
            "contracts", "protobuf", "infinium", "application", "v1", "application.proto");
        string[] requiredRpcs =
        [
            "GetResultOverview", "ListResultItems", "GetResultDetail", "GetEvidenceExpansion",
            "GetFocusedModView", "GetReviewState", "SubmitReviewEvent", "ListAssumptions",
            "SubmitAssumptionEvent", "StartTargetedVerification", "CreateStructuredExport",
            "GetStructuredExport", "ListFindingReports", "GetFindingReport",
            "PreviewStructuredExportDeletion", "DeleteStructuredExport",
        ];
        foreach (string rpc in requiredRpcs)
        {
            StringAssert.Contains(application, $"rpc {rpc}(");
        }
        StringAssert.Contains(application, "reserved \"sql\", \"path\", \"url\", \"object_type\", \"object_id\", \"query\";");
        StringAssert.Contains(application, "sharing_class = 7;");
        StringAssert.Contains(application, "string llm_involvement_state = 7;");
        Assert.IsFalse(application.Contains("rpc Download", StringComparison.Ordinal));
        Assert.IsFalse(application.Contains("bytes raw_payload", StringComparison.Ordinal));
        Assert.IsFalse(application.Contains("string payload_path =", StringComparison.Ordinal));
        Assert.IsFalse(application.Contains("string sql =", StringComparison.Ordinal));
        Assert.IsFalse(application.Contains("string query =", StringComparison.Ordinal));
        StringAssert.Contains(application, "string provenance_id = 10;");
        StringAssert.Contains(application, "string artifact_schema_identity = 11;");
        StringAssert.Contains(application, "reserved \"producer_id\", \"producer_version\"");
        StringAssert.Contains(application, "FindingReportAvailability availability = 4;");
        StringAssert.Contains(application, "bool retained_results_present = 4;");
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void PhaseCComputedVersionAxesAndFingerprintAreExact()
    {
        Assert.AreEqual("1.14.0", ProtocolConstants.StorageContractVersion);
        Assert.AreEqual(
            "d4db44c3c64f4c661162c938696c8d9ffc3d258f81eac18e9a6479d09c3491f9",
            Convert.ToHexStringLower(ProtocolConstants.Version.SchemaFingerprintSha256.Span));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void EveryPhaseCRequestRejectsUnknownFieldsAndInvalidEnumsRecursively()
    {
        IMessage[] requests = ValidPhaseCRequests();
        Assert.HasCount(16, requests);
        foreach (IMessage request in requests)
        {
            ApplicationContractValidator.ValidatePhaseC(request);
            byte[] unknown = request.ToByteArray().Concat(new byte[] { 0x98, 0x06, 0x01 }).ToArray();
            IMessage reparsed = request.Descriptor.Parser.ParseFrom(unknown);
            Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(reparsed),
                request.Descriptor.FullName);
        }

        RunId nested = RunId.Parser.ParseFrom(new RunId { Value = "run-phase-c-contract" }
            .ToByteArray().Concat(new byte[] { 0x98, 0x06, 0x01 }).ToArray());
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new GetResultOverviewRequest { RunId = nested }));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new ListResultItemsRequest
            {
                RunId = new RunId { Value = "run-phase-c-contract" },
                RequestedPageSize = 1,
                Sort = (ResultItemSort)999,
                Kinds = { ResultItemKind.Finding },
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new ListFindingReportsRequest
            {
                RunId = new RunId { Value = "run-phase-c-contract" },
                RequestedPageSize = 1,
                Sort = FindingReportSort.IdentityAscending,
                States = { (FindingReportState)999 },
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new SubmitReviewEventRequest
            {
                IdempotencyKey = "review-contract-key",
                RunId = new RunId { Value = "run-phase-c-contract" },
                SubjectKind = "finding",
                SubjectOccurrenceId = "finding-contract-id",
                EventKind = "annotation",
                Disposition = "investigating",
                InertAnnotation = new string('x', 16_385),
            }));
    }

    private static IMessage[] ValidPhaseCRequests()
    {
        RunId Run() => new() { Value = "run-phase-c-contract" };
        ProjectionVersion Projection() => new() { Value = "1" };
        return
        [
            new GetResultOverviewRequest { RunId = Run(), ExpectedProjectionVersion = Projection() },
            new ListResultItemsRequest
            {
                RunId = Run(), Kinds = { ResultItemKind.Finding }, Sort = ResultItemSort.IdentityAscending,
                RequestedPageSize = 1, ExpectedProjectionVersion = Projection(),
            },
            new GetResultDetailRequest
            {
                RunId = Run(), Kind = ResultItemKind.Finding, ItemId = "finding-contract-id",
                ExpectedProjectionVersion = Projection(),
            },
            new GetEvidenceExpansionRequest
            {
                RunId = Run(), ResultItemId = "finding-contract-id", RequestedMaximumItems = 1,
                ExpectedProjectionVersion = Projection(),
            },
            new GetFocusedModViewRequest
            {
                RunId = Run(), ExactSubjectId = "subject-contract-id", RequestedMaximumItems = 1,
                ExpectedProjectionVersion = Projection(),
            },
            new ListFindingReportsRequest
            {
                RunId = Run(), States = { FindingReportState.SupportedFinding },
                Sort = FindingReportSort.IdentityAscending, RequestedPageSize = 1,
                ExpectedProjectionVersion = Projection(),
            },
            new GetFindingReportRequest
            {
                RunId = Run(), ReportId = "report-contract-id", ExpectedProjectionVersion = Projection(),
            },
            new GetReviewStateRequest
            {
                RunId = Run(), SubjectKind = "finding", SubjectOccurrenceId = "finding-contract-id",
                ExpectedProjectionVersion = Projection(),
            },
            new SubmitReviewEventRequest
            {
                IdempotencyKey = "review-contract-key", RunId = Run(), SubjectKind = "finding",
                SubjectOccurrenceId = "finding-contract-id", EventKind = "disposition", Disposition = "unreviewed",
            },
            new ListAssumptionsRequest
            {
                ProfileId = "profile-contract-id", RequestedPageSize = 1, ExpectedProjectionVersion = Projection(),
            },
            new SubmitAssumptionEventRequest
            {
                IdempotencyKey = "assumption-contract-key", AssumptionId = "assumption-contract-id",
                ProfileId = "profile-contract-id", EventKind = "create", Origin = "user-provided",
                Confirmation = "user-confirmed", Subject = "subject", InertValue = "value", Scope = "scope",
            },
            new StartTargetedVerificationRequest
            {
                IdempotencyKey = "verification-contract-key", RequestedRunId = "run-target-contract-id",
                SourceRunId = Run(), SourceFindingOccurrenceId = "finding-contract-id",
                ExactScopeIds = { "scope-contract-id" }, UserGestureId = "gesture-contract-id",
                DispatchDeadline = new Instant { UnixSeconds = 1_800_000_000 },
            },
            new CreateStructuredExportRequest
            {
                IdempotencyKey = "export-contract-key", RunId = Run(),
                SelectedResultItemIds = { "finding-contract-id" }, SharingClass = "LocalPrivateExport",
                PrivacyDecisions = { "local-private-only" }, SourcePolicyDecisions = { "retained-only" },
            },
            new GetStructuredExportRequest { ExportId = "structured-export-contract-id" },
            new PreviewStructuredExportDeletionRequest { ExportId = "structured-export-contract-id" },
            new DeleteStructuredExportRequest
            {
                IdempotencyKey = "export-delete-contract-key", ExportId = "structured-export-contract-id",
            },
        ];
    }
}
