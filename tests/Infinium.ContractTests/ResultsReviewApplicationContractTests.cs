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
            "SubmitAssumptionEvent", "BeginTargetedVerificationPreparation",
            "GetTargetedVerificationPreparation", "CancelTargetedVerificationPreparation",
            "StartTargetedVerification", "GetTargetedVerification", "CreateStructuredExport",
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
        Assert.AreEqual("1.13.0", ProtocolConstants.Compatibility.ApplicationContract.Value);
        Assert.AreEqual("1.6.0", ProtocolConstants.Compatibility.DomainContract.Value);
        Assert.AreEqual("1.16.0", ProtocolConstants.StorageContractVersion);
        Assert.AreEqual(
            "d234d44dabf902041461b5c2318fd5c71f10eff46e7ec75f9a586812fab014c7",
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
        Assert.HasCount(20, requests);
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
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new GetTargetedVerificationPreparationRequest
            {
                PreparationId = "targeted-preparation-contract-id",
                MaximumMembers = 10,
                MaximumLifecycleEvents = 101,
                MaximumArtifactDecisions = 10,
                MaximumDependencies = 10,
                MaximumTargetAnalyzers = 10,
                MaximumTerminalGaps = 10,
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new GetTargetedVerificationPreparationRequest
            {
                PreparationId = "targeted-preparation-contract-id",
                MaximumMembers = 10,
                MaximumLifecycleEvents = 10,
                AfterLifecycleCursor = new string('x', 161),
                MaximumArtifactDecisions = 10,
                MaximumDependencies = 10,
                MaximumTargetAnalyzers = 10,
                MaximumTerminalGaps = 10,
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new GetTargetedVerificationPreparationRequest
            {
                PreparationId = "targeted-preparation-contract-id",
                MaximumMembers = 10,
                MaximumLifecycleEvents = 10,
                MaximumArtifactDecisions = 101,
                MaximumDependencies = 10,
                MaximumTargetAnalyzers = 10,
                MaximumTerminalGaps = 10,
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new GetTargetedVerificationPreparationRequest
            {
                PreparationId = "targeted-preparation-contract-id",
                MaximumMembers = 10,
                MaximumLifecycleEvents = 10,
                MaximumArtifactDecisions = 10,
                AfterArtifactKind = "candidate-delivered-input",
                MaximumDependencies = 10,
                MaximumTargetAnalyzers = 10,
                MaximumTerminalGaps = 10,
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new GetTargetedVerificationPreparationRequest
            {
                PreparationId = "targeted-preparation-contract-id",
                MaximumMembers = 10,
                MaximumLifecycleEvents = 10,
                MaximumArtifactDecisions = 10,
                MaximumDependencies = 101,
                MaximumTargetAnalyzers = 10,
                MaximumTerminalGaps = 10,
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new GetTargetedVerificationPreparationRequest
            {
                PreparationId = "targeted-preparation-contract-id",
                MaximumMembers = 10,
                MaximumLifecycleEvents = 10,
                MaximumArtifactDecisions = 10,
                MaximumDependencies = 10,
                MaximumTargetAnalyzers = 101,
                MaximumTerminalGaps = 10,
            }));
        Assert.ThrowsExactly<InvalidDataException>(() => ApplicationContractValidator.ValidatePhaseC(
            new GetTargetedVerificationPreparationRequest
            {
                PreparationId = "targeted-preparation-contract-id",
                MaximumMembers = 10,
                MaximumLifecycleEvents = 10,
                MaximumArtifactDecisions = 10,
                MaximumDependencies = 10,
                MaximumTargetAnalyzers = 10,
                MaximumTerminalGaps = 101,
            }));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void TargetedPreparationProjectionRoundTripsCompleteTypedReadback()
    {
        TargetedVerificationPreparation value = new()
        {
            PreparationId = "targeted-preparation-roundtrip",
            Revision = 7,
            PreparationFingerprintSha256 = new string('a', 64),
            State = TargetedVerificationPreparationState.ReadyWithGaps,
            SourceRunId = new RunId { Value = "source-run-roundtrip" },
            SourceFindingOccurrenceId = "finding-roundtrip",
            SourceLogicalId = "logical-roundtrip",
            SourcePayloadId = "payload-roundtrip",
            SourcePayloadFingerprintSha256 = new string('b', 64),
            SourceCanonicalSignatureSha256 = new string('c', 64),
            SourceAnalyzerFamily = "bethesda-link-consistency",
            SourceAnalyzerVersion = new SemanticVersion { Value = "4.3.2" },
            SourceSemanticContractVersion = new SemanticVersion { Value = "3.2.1" },
            SourceIdentityContractVersion = new SemanticVersion { Value = "2.1.0" },
            SourceSnapshotId = "source-snapshot-roundtrip",
            TargetSnapshotId = "target-snapshot-roundtrip",
            TargetSnapshotCapturedAt = new Instant { UnixSeconds = 1_800_000_000 },
            ConfirmedProfileRevision = 9,
            EvidenceAcquisitionId = "acquisition-roundtrip",
            AnalysisContextFingerprintSha256 = new string('d', 64),
            EffectiveConfigurationFingerprintSha256 = new string('e', 64),
            ResolvedInputManifestId = "manifest-roundtrip",
            ResolvedInputManifestFingerprintSha256 = new string('f', 64),
            CorrelationCoverageId = "coverage-roundtrip",
            CorrelationCoverageFingerprintSha256 = new string('1', 64),
            CorrelationPolicyId = "typed-correlation",
            CorrelationPolicyVersion = new SemanticVersion { Value = "1.0.0" },
            CorrelationPolicyFingerprintSha256 = new string('2', 64),
            NextDependencyEdgeId = "edge-cursor-roundtrip",
            NextTargetAnalyzerId = "analyzer-cursor-roundtrip",
            ExpectedWork = new TargetedPreparationWork
            {
                DirectRootCount = 1,
                ExpandedMemberCount = 2,
                DependencyEdgeCount = 1,
                MaximumMembers = 4096,
                MaximumEdges = 16384,
                Unsupported = 1,
            },
            AcquisitionEvidence = new TargetedAcquisitionEvidence
            {
                AcquisitionRequestFingerprintSha256 = new string('3', 64),
                SealedInputFingerprintSha256 = new string('4', 64),
                ProducerFamily = "bethesda-semantic-extraction",
                ProducerVersion = new SemanticVersion { Value = "1.0.0" },
                SupportManifestId = "support-manifest",
                EnumerationPolicyId = "qualified-enumeration",
                EnumerationPolicyVersion = new SemanticVersion { Value = "1.0.0" },
                CoordinatorFencingEpoch = 11,
                AttemptFencingToken = 12,
                PublicationId = "publication-roundtrip",
                PublicationPayloadId = "publication-payload-roundtrip",
                StagedManifestFingerprintSha256 = new string('5', 64),
                ProvenanceFingerprintSha256 = new string('6', 64),
                PublishedAt = new Instant { UnixSeconds = 1_800_000_001 },
                TerminalGapCount = 2,
                NextTerminalGap = "semantic:UnsupportedRecord:records:gap-2:unsupported record",
            },
        };
        value.AcquisitionEvidence.InertTerminalGaps.Add(
            "capture:missing-provider:plugins:provider was unavailable");
        value.ScopeMembers.Add(new TargetedScopeMember
        {
            MemberId = "member-roundtrip",
            Kind = TargetedScopeMemberKind.Contribution,
            StableIdentity = "typed-stable-id",
            InertReason = "required contribution",
            Mandatory = true,
            DirectRoot = true,
            SourceProofIds = { "proof-roundtrip" },
        });
        value.ScopeDependencies.Add(new TargetedScopeDependency
        {
            EdgeId = "edge-roundtrip",
            FromMemberId = "member-roundtrip",
            ToMemberId = "member-expanded",
            Relation = "record-contribution",
            ProofIds = { "proof-roundtrip" },
        });
        value.TargetAnalyzers.Add(new TargetedAnalyzerCompatibility
        {
            AnalyzerDeclarationId = "bethesda-link-consistency",
            AnalyzerFamily = value.SourceAnalyzerFamily,
            AnalyzerVersion = value.SourceAnalyzerVersion,
            SemanticContractVersion = value.SourceSemanticContractVersion,
            IdentityContractVersion = value.SourceIdentityContractVersion,
            CompatibilityProofId = "analyzer-proof",
            CompatibilityProofFingerprintSha256 = new string('7', 64),
            Compatible = true,
            InertReason = "exact retained declaration",
        });
        value.ArtifactDecisions.Add(new TargetedArtifactDecision
        {
            ArtifactKind = "candidate-delivered-input",
            ArtifactId = "candidate-input",
            Disposition = "recompute",
            ValidityProofId = "recompute-proof",
            ValidityProofFingerprintSha256 = new string('8', 64),
            InertReason = "fresh snapshot dependency",
        });
        value.LifecycleEvents.Add(new TargetedPreparationLifecycleEvent
        {
            Sequence = 3,
            OwnerSequence = 2,
            Owner = "evidence-acquisition",
            EventKind = "published",
            Generation = 1,
            CoordinatorFencingEpoch = 11,
            OccurredAt = new Instant { UnixSeconds = 1_800_000_002 },
            EvidenceFingerprintSha256 = new string('9', 64),
            InertSummary = "published",
        });

        TargetedVerificationPreparation reparsed = TargetedVerificationPreparation.Parser.ParseFrom(value.ToByteArray());
        Assert.AreEqual(value, reparsed);
        Assert.AreEqual("4.3.2", reparsed.SourceAnalyzerVersion.Value);
        Assert.IsTrue(reparsed.ScopeMembers[0].DirectRoot);
        Assert.AreEqual("record-contribution", reparsed.ScopeDependencies[0].Relation);
        Assert.AreEqual("bethesda-link-consistency", reparsed.TargetAnalyzers[0].AnalyzerDeclarationId);
        Assert.AreEqual("edge-cursor-roundtrip", reparsed.NextDependencyEdgeId);
        Assert.AreEqual("analyzer-cursor-roundtrip", reparsed.NextTargetAnalyzerId);
        Assert.AreEqual(1UL, reparsed.ExpectedWork.Unsupported);
        Assert.AreEqual(12UL, reparsed.AcquisitionEvidence.AttemptFencingToken);
        Assert.AreEqual(2UL, reparsed.AcquisitionEvidence.TerminalGapCount);
        Assert.IsNotEmpty(reparsed.AcquisitionEvidence.NextTerminalGap);
        Assert.AreEqual("recompute", reparsed.ArtifactDecisions[0].Disposition);
        Assert.AreEqual("published", reparsed.LifecycleEvents[0].EventKind);
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
            new BeginTargetedVerificationPreparationRequest
            {
                IdempotencyKey = "verification-prepare-contract-key", UserGestureId = "prepare-gesture-contract-id",
                SourceRunId = Run(), SourceFindingOccurrenceId = "finding-contract-id",
                ConfirmedProfileId = "profile-contract-id", ExpectedConfirmedProfileRevision = 1,
                SavedConfigurationId = "configuration-contract-id", ExpectedSavedConfigurationRevision = 1,
                AnalysisContextId = "context-contract-id", ExpectedAnalysisContextRevision = 1,
                AnalysisContextFingerprintSha256 = new string('a', 64),
                RequestedPreparationId = "targeted-preparation-contract-id",
                InitiationKind = ManualInitiationKind.EvaluationHarness,
                DispatchDeadline = new Instant { UnixSeconds = 1_800_000_000 },
            },
            new GetTargetedVerificationPreparationRequest
            {
                PreparationId = "targeted-preparation-contract-id", MaximumMembers = 10,
                MaximumLifecycleEvents = 10,
                MaximumArtifactDecisions = 10,
                MaximumDependencies = 10,
                MaximumTargetAnalyzers = 10,
                MaximumTerminalGaps = 10,
            },
            new CancelTargetedVerificationPreparationRequest
            {
                IdempotencyKey = "verification-cancel-contract-key", PreparationId = "targeted-preparation-contract-id",
                ExpectedRevision = 1, UserGestureId = "cancel-gesture-contract-id",
            },
            new StartTargetedVerificationRequest
            {
                PreparationId = "targeted-preparation-contract-id", ExpectedPreparationRevision = 2,
                ExpectedPreparationFingerprintSha256 = new string('b', 64),
                IdempotencyKey = "verification-contract-key", RequestedRunId = "run-target-contract-id",
                InitiationKind = ManualInitiationKind.EvaluationHarness, UserGestureId = "gesture-contract-id",
                DispatchDeadline = new Instant { UnixSeconds = 1_800_000_000 },
            },
            new GetTargetedVerificationRequest
            {
                TargetedVerificationId = "targeted-verification-contract-id",
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
