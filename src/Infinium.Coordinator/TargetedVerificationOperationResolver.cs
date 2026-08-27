using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Application.Analysis;
using Infinium.Application.Candidates;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Infinium.Persistence;

namespace Infinium.Coordinator;

internal sealed record ResolvedTargetedOperation(
    RunBinding Binding,
    ManagedAnalysisOrchestrationRequest Request,
    string RequestJson,
    string RequestSha256,
    string SubmissionFingerprint,
    string TargetedVerificationId,
    string AdmissionId,
    IReadOnlyList<TargetedOperationInputPersistence> OperationInputs);

internal static class TargetedVerificationOperationResolver
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static ResolvedTargetedOperation Bind(
        AuthoritativeStore store,
        TargetedPreparationPersistenceRecord preparation,
        TargetedVerificationPlanContract plan,
        string commandId,
        string runId,
        string gestureId,
        DateTimeOffset dispatchDeadline,
        DateTimeOffset now)
    {
        RunRecord sourceRun = store.GetRun(preparation.SourceRunId);
        if (sourceRun.State is not (LifecycleState.Completed or LifecycleState.CompletedWithGaps
                or LifecycleState.LimitReached))
        {
            throw new AnalysisIdentityDriftException(
                "The targeted source run no longer has the required terminal analytical state.");
        }
        if (plan.PreparationId.Value != preparation.PreparationId
            || plan.PreparationRevision != preparation.Revision - 1
            || preparation.PlanId != plan.PlanId.Value
            || preparation.PlanFingerprint != plan.PlanFingerprint.Value
            || preparation.TargetSnapshotId != plan.TargetSnapshotId.Value
            || preparation.EvidenceAcquisitionId != plan.EvidenceAcquisitionId.Value
            || preparation.State is not (TargetedVerificationPreparationState.Ready
                or TargetedVerificationPreparationState.ReadyWithGaps)
            || preparation.Startable != plan.Startable
            || preparation.Limited != plan.Limited)
        {
            throw new AnalysisIdentityDriftException(
                "The retained targeted preparation revision or plan binding drifted.");
        }
        if (Hash(preparation.RequestJson) != preparation.RequestSha256)
        {
            throw new AnalysisIdentityDriftException(
                "The retained targeted preparation request bytes drifted.");
        }
        ValidateCanonicalPlanIdentities(plan);
        store.ValidateTargetedPlanProjection(plan);
        ResultItemPersistenceRecord sourceOccurrence = store.GetResultItem(
            sourceRun.RunId, preparation.SourceOccurrenceId);
        string canonicalSourceSignature = Hash(string.Join('\n', sourceOccurrence.ItemId,
            sourceOccurrence.LogicalId, sourceOccurrence.SourcePayloadSha256, sourceOccurrence.AnalyzerId,
            sourceOccurrence.AnalyzerVersion));
        if (sourceOccurrence.Kind != preparation.SourceOccurrenceKind
            || sourceOccurrence.LogicalId != plan.Source.LogicalId.Value
            || sourceOccurrence.SourcePayloadId != plan.Source.SourcePayloadId.Value
            || sourceOccurrence.SourcePayloadSha256 != plan.Source.SourcePayloadFingerprint.Value
            || canonicalSourceSignature != plan.Source.CanonicalSignature.Value)
        {
            throw new AnalysisIdentityDriftException(
                "The canonical targeted source occurrence identity drifted.");
        }
        byte[] sourcePayloadBytes = store.ReadFindingCasePayload(sourceOccurrence.SourcePayloadId);
        if (Hash(sourcePayloadBytes) != sourceOccurrence.SourcePayloadSha256)
        {
            throw new AnalysisIdentityDriftException("The canonical targeted source payload bytes drifted.");
        }
        FindingCaseContract canonicalSource = FindingCaseJsonCodec.Deserialize(sourcePayloadBytes);
        int occurrenceMatches = preparation.SourceOccurrenceKind == "finding"
            ? canonicalSource.Findings.Count(item => item.FindingOccurrenceId.Value == preparation.SourceOccurrenceId)
            : canonicalSource.Cases.Count(item => item.CaseOccurrenceId.Value == preparation.SourceOccurrenceId);
        if (occurrenceMatches != 1)
        {
            throw new AnalysisIdentityDriftException(
                "The canonical targeted source occurrence no longer resolves exactly once.");
        }
        RunOperationRecord sourceOperation = store.GetRunOperation(sourceRun.RunId)
            ?? throw new InvalidOperationException("The targeted source run has no managed operation.");
        if (sourceOperation.OperationKind != ManagedRunExecutor.ManagedAnalysisOperation
            || Hash(sourceOperation.RequestJson) != sourceOperation.RequestSha256)
        {
            throw new AnalysisIdentityDriftException("The targeted source managed operation drifted.");
        }
        ManagedAnalysisOrchestrationRequest source = JsonSerializer.Deserialize<ManagedAnalysisOrchestrationRequest>(
            sourceOperation.RequestJson, ContractJsonSerializer.Options)
            ?? throw new InvalidDataException("The targeted source managed operation is malformed.");
        ManagedAnalysisOrchestrator.Validate(source, sourceRun.RunId, sourceRun.Binding);
        TargetedReuseDecisionContract sourceOperationProof = plan.ReuseDecisions.SingleOrDefault(item =>
            item.ArtifactKind == "source-managed-operation"
            && item.ArtifactId.Value == sourceRun.RunId)
            ?? throw new AnalysisIdentityDriftException(
                "The targeted source managed operation lacks an exact retained proof.");
        if (sourceOperationProof.Disposition != "reuse-with-proof"
            || sourceOperationProof.ProofFingerprint.Value != sourceOperation.RequestSha256
            || plan.Source.SourceRunId.Value != sourceRun.RunId
            || plan.Source.RootOccurrenceId.Value != preparation.SourceOccurrenceId
            || !string.Equals(plan.Source.RootKind.ToString(), preparation.SourceOccurrenceKind,
                StringComparison.OrdinalIgnoreCase)
            || plan.Source.SourceSnapshotId.Value != sourceRun.Binding.InstallationSnapshotId
            || plan.Source.AnalysisContextId.Value != sourceRun.Binding.AnalysisContextId
            || plan.Source.EffectiveConfigurationId.Value != sourceRun.Binding.EffectiveScanConfigurationId
            || plan.Source.ResolvedInputManifestId.Value != sourceRun.Binding.ResolvedInputManifestId
            || source.ExecutionInput.InstallationSnapshot.ArtifactId.Value != sourceRun.Binding.InstallationSnapshotId)
        {
            throw new AnalysisIdentityDriftException(
                "The retained source operation, binding, or exact proof drifted.");
        }
        byte[] sourceSnapshotBytes = store.ReadPublishedSnapshotPayload(
            sourceRun.Binding.InstallationSnapshotId, 64 * 1024 * 1024);
        Mo2SnapshotCaptureResult sourceSnapshot = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(
            sourceSnapshotBytes, StrictJson)
            ?? throw new InvalidDataException("The retained targeted source snapshot is malformed.");
        if (sourceSnapshot.Snapshot?.Contract.SnapshotId.Value != sourceRun.Binding.InstallationSnapshotId)
        {
            throw new AnalysisIdentityDriftException("The retained source snapshot identity drifted.");
        }
        if (plan.TargetSnapshotId == plan.Source.SourceSnapshotId)
        {
            throw new AnalysisIdentityDriftException(
                "The source snapshot occurrence can never serve as targeted-verification target proof.");
        }
        SnapshotCaptureOperationRecord capture = store.GetSnapshotCaptureOperation(plan.CaptureOperationId.Value);
        if (capture.State != "Completed"
            || capture.InstallationSnapshotId != plan.TargetSnapshotId.Value
            || capture.OperationId != preparation.CaptureOperationId
            || Hash(capture.RequestJson) != capture.RequestSha256
            || capture.CreatedAt < preparation.CreatedAt)
        {
            throw new AnalysisIdentityDriftException(
                "The targeted snapshot is not the distinct fresh capture retained by this preparation.");
        }
        store.ValidateTargetedSnapshotLink(preparation.PreparationId, capture.OperationId,
            plan.TargetSnapshotId.Value, plan.TargetSnapshotFingerprint.Value);
        SemanticAcquisitionPersistenceRecord acquisition = store.GetSemanticAcquisition(
            plan.EvidenceAcquisitionId.Value);
        if (acquisition.State != "Completed"
            || acquisition.PreparationId != preparation.PreparationId
            || acquisition.TargetSnapshotId != plan.TargetSnapshotId.Value
            || Hash(acquisition.RequestJson) != acquisition.RequestSha256)
        {
            throw new AnalysisIdentityDriftException(
                "The retained targeted semantic acquisition lifecycle or request seal drifted.");
        }
        SemanticAcquisitionPublicationRecord publication = store.GetSemanticAcquisitionPublication(
            plan.EvidenceAcquisitionId.Value);
        store.ValidateSemanticAcquisitionPublicationSeal(acquisition, publication);
        byte[] semanticBytes = store.ReadCandidateAnalysisPayload(publication.PayloadId);
        if (Hash(semanticBytes) != publication.PayloadSha256
            || publication.SemanticOutputId != plan.SemanticOutputId.Value)
        {
            throw new AnalysisIdentityDriftException("The targeted semantic publication drifted.");
        }
        byte[] snapshotBytes = store.ReadPublishedSnapshotPayload(plan.TargetSnapshotId.Value, 64 * 1024 * 1024);
        if (Hash(snapshotBytes) != plan.TargetSnapshotFingerprint.Value
            || publication.TargetSnapshotId != plan.TargetSnapshotId.Value
            || publication.AcquisitionId != plan.EvidenceAcquisitionId.Value
            || publication.PayloadSha256 != plan.SemanticOutputFingerprint.Value)
        {
            throw new AnalysisIdentityDriftException("The targeted snapshot or acquisition seal drifted.");
        }
        Mo2SnapshotCaptureResult snapshotCapture = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(
            snapshotBytes, StrictJson)
            ?? throw new InvalidDataException("The targeted snapshot publication is malformed.");
        if (snapshotCapture.Snapshot?.Contract.SnapshotId != plan.TargetSnapshotId)
        {
            throw new AnalysisIdentityDriftException("The retained target snapshot identity drifted.");
        }
        ManagedBethesdaSemanticAssignment semanticAssignment = ManagedRunExecutor.SealBethesdaAssignment(
            new(snapshotCapture, []));
        if (Hash(JsonSerializer.Serialize(semanticAssignment)) != acquisition.SealedInputFingerprint)
        {
            throw new AnalysisIdentityDriftException(
                "The targeted semantic acquisition sealed input drifted.");
        }
        BethesdaSemanticExtractionResult extraction = BethesdaSemanticPublicationValidator.DeserializeAndValidate(
            semanticBytes, semanticAssignment, 64L * 1024 * 1024);

        CandidateDeliveredInputContract delivered = CandidateDeliveredInputAdapter.Create(
            new(runId), plan.TargetSnapshotId, source.AnalysisContext.ContextId,
            new(preparation.SavedConfigurationId),
            extraction.Snapshot, documentationEvidence: null);
        HashSet<OpaqueId> executableMembers = plan.CorrelationCoverage.Rows
            .Where(item => item.Status is TargetedCorrelationStatus.MatchedExecutable
                or TargetedCorrelationStatus.ChangedCorrelated)
            .SelectMany(item => new[]
            {
                item.SourceStableIdentity,
                item.TargetStableIdentity,
                item.CurrentExecutionMemberId,
            })
            .Where(item => item is not null).Cast<OpaqueId>().ToHashSet();
        delivered = delivered with
        {
            PayloadId = new("candidate-delivered-input-pending"),
            LinkFacts = delivered.LinkFacts.Where(item => executableMembers.Contains(item.RecordParticipantId)
                || executableMembers.Contains(item.PriorContributionId)
                || executableMembers.Contains(item.WinningContributionId)
                || (item.PriorTargetParticipantId is not null && executableMembers.Contains(item.PriorTargetParticipantId))
                || (item.WinningTargetParticipantId is not null && executableMembers.Contains(item.WinningTargetParticipantId))).ToArray(),
            FaceGenFacts = delivered.FaceGenFacts.Where(item => executableMembers.Contains(item.NpcParticipantId)
                || executableMembers.Contains(item.MeshAssetId) || executableMembers.Contains(item.TintAssetId)
                || (item.MeshProviderParticipantId is not null && executableMembers.Contains(item.MeshProviderParticipantId))
                || (item.TintProviderParticipantId is not null && executableMembers.Contains(item.TintProviderParticipantId))).ToArray(),
            DocumentationFacts = [],
        };
        delivered = delivered with { PayloadId = CandidateDeliveredInputIdentity.ComputePayloadId(delivered) };
        CandidateDeliveredContractInvariants.Validate(delivered);
        byte[] deliveredBytes = CandidateDeliveredInputJsonCodec.Serialize(delivered);
        Sha256Fingerprint deliveredFingerprint = new(Hash(deliveredBytes));
        byte[] coverageBytes = JsonSerializer.SerializeToUtf8Bytes(plan.CorrelationCoverage);
        SetupObjectRecord configuration = store.FindSetupObject(
            "saved-scan-configuration", preparation.SavedConfigurationId)
            ?? throw new AnalysisIdentityDriftException("The targeted saved configuration is no longer retained.");
        if (configuration.LifecycleState != "active" || configuration.Revision != preparation.SavedConfigurationRevision)
        {
            throw new AnalysisIdentityDriftException("The targeted saved configuration changed after preparation.");
        }
        Sha256Fingerprint configurationFingerprint = new(Hash(configuration.PayloadJson));
        TargetedReuseDecisionContract contextProof = plan.ReuseDecisions.SingleOrDefault(item =>
            item.ArtifactKind == "analysis-context" && item.ArtifactId == source.AnalysisContext.ContextId)
            ?? throw new AnalysisIdentityDriftException(
                "The targeted analysis context lacks an exact reuse proof.");
        TargetedReuseDecisionContract configurationProof = plan.ReuseDecisions.SingleOrDefault(item =>
            item.ArtifactKind == "effective-configuration"
            && item.ArtifactId.Value == preparation.SavedConfigurationId)
            ?? throw new AnalysisIdentityDriftException(
                "The targeted effective configuration lacks an exact reuse proof.");
        if (contextProof.Disposition != "reuse-with-proof"
            || contextProof.ProofFingerprint != source.AnalysisContext.CanonicalFingerprint
            || configurationProof.Disposition != "reuse-with-proof"
            || configurationProof.ProofFingerprint != configurationFingerprint)
        {
            throw new AnalysisIdentityDriftException(
                "The targeted context or configuration reuse proof drifted.");
        }

        string manifestId = "targeted-manifest-" + Hash(string.Join('\n', preparation.PreparationId,
            plan.PlanFingerprint.Value, runId, deliveredFingerprint.Value, plan.CorrelationCoverage.CanonicalFingerprint.Value))[..32];
        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "infinium/targeted-resolved-input-manifest/v1",
            preparationId = preparation.PreparationId,
            planId = plan.PlanId.Value,
            sourceRunId = plan.Source.SourceRunId.Value,
            targetSnapshotId = plan.TargetSnapshotId.Value,
            semanticOutputId = plan.SemanticOutputId.Value,
            scopeId = plan.Scope.ScopeId.Value,
            scopeFingerprint = plan.Scope.CanonicalFingerprint.Value,
            coverageId = plan.CorrelationCoverage.CoverageId.Value,
            coverageFingerprint = plan.CorrelationCoverage.CanonicalFingerprint.Value,
            deliveredInputId = delivered.PayloadId.Value,
            deliveredInputFingerprint = deliveredFingerprint.Value,
            savedConfigurationId = preparation.SavedConfigurationId,
            savedConfigurationRevision = preparation.SavedConfigurationRevision,
            savedConfigurationFingerprint = configurationFingerprint.Value,
        });
        Sha256Fingerprint manifestFingerprint = new(Hash(manifestBytes));

        ArtifactReferenceContract deliveredReference = new(delivered.PayloadId, delivered.SchemaVersion,
            deliveredFingerprint, "retained");
        ArtifactReferenceContract coverageReference = new(plan.CorrelationCoverage.CoverageId,
            plan.CorrelationCoverage.SchemaVersion, plan.CorrelationCoverage.CanonicalFingerprint, "retained");
        ArtifactReferenceContract semanticReference = new(plan.SemanticOutputId,
            BethesdaSemanticContract.SchemaVersion, plan.SemanticOutputFingerprint, "retained");
        ArtifactReferenceContract configurationReference = new(new(preparation.SavedConfigurationId),
            source.ExecutionInput.EffectiveConfiguration.ArtifactVersion, configurationFingerprint, "retained");
        ArtifactReferenceContract manifestReference = new(new(manifestId), new(1, 0, 0), manifestFingerprint, "retained");
        Dictionary<OpaqueId, TargetedReuseDecisionContract> retainedSourceProofs = plan.ReuseDecisions
            .Where(item => item.Disposition == "reuse-with-proof"
                && item.ArtifactKind == "documentation-evidence")
            .ToDictionary(item => item.ArtifactId);
        OpaqueId? sourceDeliveredInputId = source.Candidate.DeliveredInput?.PayloadId;
        ArtifactReferenceContract[] reusableSourceInputs = source.ExecutionInput.SourceInputs
            .Where(item => item.ArtifactId != sourceDeliveredInputId).ToArray();
        ArtifactReferenceContract[] retainedSourceInputs = reusableSourceInputs
            .Where(item => retainedSourceProofs.TryGetValue(item.ArtifactId, out TargetedReuseDecisionContract? proof)
                && proof.ProofFingerprint == item.Fingerprint)
            .ToArray();
        if (retainedSourceInputs.Length != reusableSourceInputs.Length)
        {
            throw new AnalysisIdentityDriftException(
                "A retained source input lacks an exact targeted-verification reuse proof.");
        }
        Dictionary<OpaqueId, TargetedReuseDecisionContract> retainedAnalyzerProofs = plan.ReuseDecisions
            .Where(item => item.Disposition == "reuse-with-proof"
                && item.ArtifactKind == "analyzer-declaration")
            .ToDictionary(item => item.ArtifactId);
        if (source.ExecutionInput.AnalyzerDeclarations.Any(item =>
                !retainedAnalyzerProofs.TryGetValue(item.ArtifactId, out TargetedReuseDecisionContract? proof)
                || proof.ProofFingerprint != item.Fingerprint))
        {
            throw new AnalysisIdentityDriftException(
                "A retained analyzer declaration lacks an exact targeted-verification reuse proof.");
        }
        AnalysisExecutionInputContract execution = source.ExecutionInput with
        {
            ExecutionInputId = new("targeted-execution-" + Hash(runId + "\n" + plan.PlanFingerprint.Value)[..32]),
            RunId = new(runId),
            InstallationSnapshot = source.ExecutionInput.InstallationSnapshot with
            {
                ArtifactId = plan.TargetSnapshotId,
                Fingerprint = plan.TargetSnapshotFingerprint,
            },
            BethesdaSemanticInput = semanticReference,
            EffectiveConfiguration = configurationReference,
            SourceInputs = retainedSourceInputs
                .Append(deliveredReference).Append(coverageReference)
                .DistinctBy(item => item.ArtifactId).OrderBy(item => item.ArtifactId.Value, StringComparer.Ordinal).ToArray(),
            ResolvedInputManifest = manifestReference,
            Mode = ReplayMode.Incremental,
            PriorRunId = new(sourceRun.RunId),
            Seed = BitConverter.ToInt64(
                SHA256.HashData(Encoding.UTF8.GetBytes(plan.PlanFingerprint.Value)),
                0) & long.MaxValue,
        };
        CoveragePopulationFactContract population = new(
            CandidateAnalysisIdentity.StableId("targeted-coverage-population", plan.CorrelationCoverage.CoverageId.Value),
            source.Candidate.PopulationId, plan.CorrelationCoverage.CoverageId.Value,
            "prepared targeted dependency closure")
        {
            EvidenceIds = [new(publication.PublicationId)],
        };
        CoverageMemberFactContract[] memberFacts = plan.CorrelationCoverage.Rows.Select(row => new CoverageMemberFactContract(
            CandidateAnalysisIdentity.StableId("targeted-coverage-member", row.RowId.Value),
            source.Candidate.PopulationId, plan.CorrelationCoverage.CoverageId.Value,
            "prepared targeted dependency closure", row.ScopeMemberId,
            CoverageState(row), row.Reason,
            row.Status is TargetedCorrelationStatus.Unsupported or TargetedCorrelationStatus.Inaccessible
                or TargetedCorrelationStatus.Malformed or TargetedCorrelationStatus.Ambiguous
                or TargetedCorrelationStatus.MissingRequiredProof ? row.Status.ToString() : "none",
            FailureId(row), [], GapId: GapId(row))).ToArray();
        CoverageFailureFactContract[] failures = plan.CorrelationCoverage.Rows
            .Where(row => FailureId(row) is not null)
            .Select(row => new CoverageFailureFactContract(FailureId(row)!, source.Candidate.PopulationId,
                "targeted-" + row.Status.ToString().ToLowerInvariant(), row.Reason,
                row.Status == TargetedCorrelationStatus.Inaccessible)).ToArray();
        HashSet<OpaqueId> selectedHypotheses = SelectedFindings(store, plan)
            .Select(item => item.HypothesisId).ToHashSet();
        FindingCasePhaseParameters finding = source.FindingCase with
        {
            AssessmentTime = new(now),
            FindingEvidenceFacts = source.FindingCase.FindingEvidenceFacts
                .Where(item => selectedHypotheses.Contains(item.HypothesisId)).ToArray(),
            FindingRecommendationFacts = source.FindingCase.FindingRecommendationFacts
                .Where(item => selectedHypotheses.Contains(item.HypothesisId)).ToArray(),
            SharedCauseProofs = source.FindingCase.SharedCauseProofs
                .Where(item => item.HypothesisIds.All(selectedHypotheses.Contains)).ToArray(),
            CoveragePopulationFacts = [population],
            CoverageMemberFacts = memberFacts,
            CoverageFailureFacts = failures,
            PriorFindings = SelectedFindings(store, plan).Select(item => new PriorFindingContract(
                item.FindingOccurrenceId, item.LogicalFindingId, item.OriginatingRunId, item.CandidateId,
                item.HypothesisId, item.IdentityEnvelope, item.SemanticFingerprint, true,
                [plan.CorrelationCoverage.CoverageId.Value])).ToArray(),
            PriorCases = SelectedCases(store, plan).Select(item => new PriorCaseContract(
                item.CaseOccurrenceId, item.LogicalCaseId, item.OriginatingRunId, item.Kind,
                item.FindingOccurrenceIds, item.HypothesisIds, item.IdentityEnvelope, item.SemanticFingerprint,
                true, [plan.CorrelationCoverage.CoverageId.Value])).ToArray(),
            TargetedCorrelationCoverage = plan.CorrelationCoverage,
        };
        ManagedAnalysisOrchestrationRequest request = source with
        {
            RequestId = "targeted-analysis-" + Hash(runId + "\n" + plan.PlanFingerprint.Value)[..40],
            ExecutionInput = execution,
            DocumentationImport = source.DocumentationImport with
            {
                OriginatingRunId = new(runId),
                ImportRunId = new(runId),
            },
            Candidate = source.Candidate with
            {
                DeliveredInput = delivered,
                DeliveredInputByteFingerprint = deliveredFingerprint,
            },
            FindingCase = finding,
            StartedAt = now,
            TerminalOutcome = plan.Limited ? AnalysisTerminalOutcome.CompletedWithGaps : AnalysisTerminalOutcome.Completed,
            TerminalReason = plan.Limited
                ? "The prepared targeted scope contains inspectable processing gaps."
                : "The prepared targeted scope is dependency-complete.",
            TargetedCorrelationCoverage = plan.CorrelationCoverage,
        };
        RunBinding binding = new(plan.TargetSnapshotId.Value, source.AnalysisContext.ContextId.Value,
            preparation.SavedConfigurationId, manifestId);
        ManagedAnalysisOrchestrator.Validate(request, runId, binding);
        string requestJson = JsonSerializer.Serialize(request, ContractJsonSerializer.Options);
        string requestSha = Hash(requestJson);
        string submission = Hash(string.Join('\n', "targeted-verification-submission/v1", commandId, runId,
            preparation.PreparationId, preparation.Revision, preparation.PreparationFingerprint, gestureId,
            dispatchDeadline.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            binding.InstallationSnapshotId, binding.AnalysisContextId, binding.EffectiveScanConfigurationId,
            binding.ResolvedInputManifestId, requestSha));
        string verificationId = "targeted-verification-" + runId;
        TargetedOperationInputPersistence[] operationInputs =
        [
            new("targeted-candidate-delivered-input", delivered.PayloadId.Value, deliveredBytes),
            new("targeted-correlation-coverage", plan.CorrelationCoverage.CoverageId.Value, coverageBytes),
            new("targeted-resolved-input-manifest", manifestId, manifestBytes),
        ];
        return new(binding, request, requestJson, requestSha, submission, verificationId,
            "targeted-admission-" + submission[..32], operationInputs);
    }

    private static void ValidateCanonicalPlanIdentities(TargetedVerificationPlanContract plan)
    {
        TargetedAnalysisScopeContract rebuiltScope = TargetedVerificationPlanner.CloseScope(
            plan.PreparationId, plan.Source.RootOccurrenceId, plan.Scope.DirectRoots,
            plan.Scope.Members, plan.Scope.Dependencies, plan.Scope.MaximumMembers, plan.Scope.MaximumEdges);
        if (rebuiltScope.ScopeId != plan.Scope.ScopeId
            || rebuiltScope.CanonicalFingerprint != plan.Scope.CanonicalFingerprint)
        {
            throw new AnalysisIdentityDriftException("The retained targeted scope fingerprint drifted.");
        }
        TargetedCurrentObservationContract[] observations = plan.CorrelationCoverage.Rows.Select(row => new
            TargetedCurrentObservationContract(row.SourceStableIdentity, row.TargetPopulationId,
                row.TargetStableIdentity, row.CurrentExecutionMemberId, row.Status, row.CorrelationQualified,
                row.ProcessingQualified, row.Reason, row.EvidenceIds,
                row.EnumerationOrApplicabilityProofId)).ToArray();
        TargetedCorrelationCoverageContract rebuiltCoverage = TargetedVerificationPlanner.Correlate(
            plan.PreparationId, rebuiltScope, plan.TargetSnapshotId, plan.EvidenceAcquisitionId,
            plan.SemanticOutputId, observations);
        if (rebuiltCoverage.CoverageId != plan.CorrelationCoverage.CoverageId
            || rebuiltCoverage.CanonicalFingerprint != plan.CorrelationCoverage.CanonicalFingerprint
            || rebuiltCoverage.PopulationDenominator != plan.Scope.Members.Count)
        {
            throw new AnalysisIdentityDriftException(
                "The retained targeted correlation fingerprint or denominator drifted.");
        }
    }

    private static FindingContract[] SelectedFindings(AuthoritativeStore store,
        TargetedVerificationPlanContract plan)
    {
        FindingCaseContract canonical = FindingCaseJsonCodec.Deserialize(
            store.ReadFindingCasePayload(plan.Source.SourcePayloadId.Value));
        if (plan.Source.RootKind == TargetedVerificationRootKind.Finding)
        {
            return [canonical.Findings.Single(item => item.FindingOccurrenceId == plan.Source.RootOccurrenceId)];
        }

        AnalysisCaseContract sourceCase = canonical.Cases.Single(item => item.CaseOccurrenceId == plan.Source.RootOccurrenceId);
        return sourceCase.FindingOccurrenceIds.Select(id => canonical.Findings.Single(item => item.FindingOccurrenceId == id)).ToArray();
    }

    private static IReadOnlyList<AnalysisCaseContract> SelectedCases(AuthoritativeStore store,
        TargetedVerificationPlanContract plan)
    {
        if (plan.Source.RootKind != TargetedVerificationRootKind.Case)
        {
            return [];
        }
        FindingCaseContract canonical = FindingCaseJsonCodec.Deserialize(
            store.ReadFindingCasePayload(plan.Source.SourcePayloadId.Value));
        return [canonical.Cases.Single(item => item.CaseOccurrenceId == plan.Source.RootOccurrenceId)];
    }

    private static CoverageMemberState CoverageState(TargetedCorrelationCoverageRowContract row) => row.Status switch
    {
        TargetedCorrelationStatus.ProvenAbsent or TargetedCorrelationStatus.ProvenNotApplicable => CoverageMemberState.Completed,
        TargetedCorrelationStatus.MatchedExecutable or TargetedCorrelationStatus.ChangedCorrelated
            when row.ProcessingQualified => CoverageMemberState.Completed,
        TargetedCorrelationStatus.Unsupported => CoverageMemberState.Unsupported,
        _ => CoverageMemberState.Failed,
    };

    private static OpaqueId? FailureId(TargetedCorrelationCoverageRowContract row) => row.Status is
        TargetedCorrelationStatus.Ambiguous or TargetedCorrelationStatus.Inaccessible
        or TargetedCorrelationStatus.Malformed or TargetedCorrelationStatus.MissingRequiredProof
            ? CandidateAnalysisIdentity.StableId("targeted-coverage-failure", row.RowId.Value) : null;

    private static OpaqueId? GapId(TargetedCorrelationCoverageRowContract row) => row.Status is
        TargetedCorrelationStatus.Unsupported or TargetedCorrelationStatus.Inaccessible
        or TargetedCorrelationStatus.Malformed or TargetedCorrelationStatus.Ambiguous
        or TargetedCorrelationStatus.MissingRequiredProof
            ? CandidateAnalysisIdentity.StableId("targeted-coverage-gap", row.RowId.Value) : null;

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    private static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(SHA256.HashData(value));
}
