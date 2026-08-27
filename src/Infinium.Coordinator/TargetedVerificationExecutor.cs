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
using Microsoft.Extensions.Logging;

namespace Infinium.Coordinator;

#pragma warning disable CA1848 // Failures are exceptional and retain preparation identity.

public sealed class TargetedVerificationExecutor
{
    private const long MaximumSemanticBytes = 64L * 1024 * 1024;
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly Lock gate = new();
    private readonly CoordinatorRuntime runtime;
    private readonly SnapshotCaptureExecutor snapshotExecutor;
    private readonly ManagedRunExecutor workerLauncher;
    private readonly ILogger<TargetedVerificationExecutor> logger;
    private readonly Func<ManagedBethesdaSemanticAssignment, BethesdaSemanticExtractionResult>?
        inProcessSemanticExecution;
    private bool pumpRunning;

    public TargetedVerificationExecutor(
        CoordinatorRuntime runtime,
        SnapshotCaptureExecutor snapshotExecutor,
        ManagedRunExecutor workerLauncher,
        ILogger<TargetedVerificationExecutor> logger)
        : this(runtime, snapshotExecutor, workerLauncher, logger, null)
    {
    }

    internal TargetedVerificationExecutor(
        CoordinatorRuntime runtime,
        SnapshotCaptureExecutor snapshotExecutor,
        ManagedRunExecutor workerLauncher,
        ILogger<TargetedVerificationExecutor> logger,
        Func<ManagedBethesdaSemanticAssignment, BethesdaSemanticExtractionResult>?
            inProcessSemanticExecution)
    {
        this.runtime = runtime;
        this.snapshotExecutor = snapshotExecutor;
        this.workerLauncher = workerLauncher;
        this.logger = logger;
        this.inProcessSemanticExecution = inProcessSemanticExecution;
    }

    public void Schedule(string preparationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preparationId);
        lock (gate)
        {
            if (pumpRunning)
            {
                return;
            }
            pumpRunning = true;
            _ = Task.Run(DrainAsync);
        }
    }

    public void RecoverAtStartup()
    {
        _ = runtime.Store.RecoverInterruptedSemanticAcquisitions(
            runtime.Authority.FencingEpoch, DateTimeOffset.UtcNow);
        IReadOnlyList<TargetedPreparationPersistenceRecord> active =
            runtime.Store.GetDispatchableTargetedPreparations();
        if (active.Count != 0)
        {
            Schedule(active[0].PreparationId);
        }
    }

    internal async Task ExecuteForTestsAsync(string preparationId) =>
        await ExecuteCoreAsync(preparationId).ConfigureAwait(false);

    private async Task DrainAsync()
    {
        try
        {
            while (true)
            {
                TargetedPreparationPersistenceRecord? next = runtime.Store
                    .GetDispatchableTargetedPreparations() is { Count: > 0 } pending
                        ? pending[0]
                        : null;
                if (next is null)
                {
                    return;
                }
                await ExecuteCoreAsync(next.PreparationId).ConfigureAwait(false);
            }
        }
        finally
        {
            lock (gate)
            {
                pumpRunning = false;
            }
        }
    }

    private async Task ExecuteCoreAsync(string preparationId)
    {
        TargetedPreparationPersistenceRecord preparation = runtime.Store.GetTargetedPreparation(preparationId);
        try
        {
            ValidateCurrentSelections(preparation);
            if (preparation.State == TargetedVerificationPreparationState.CapturingSnapshot)
            {
                snapshotExecutor.Schedule(preparation.CaptureOperationId);
                SnapshotCaptureOperationRecord capture = await AwaitCaptureAsync(preparation).ConfigureAwait(false);
                if (capture.State != "Completed" || capture.InstallationSnapshotId is null)
                {
                    throw new InvalidOperationException("The fresh targeted snapshot capture did not publish a complete snapshot.");
                }
                byte[] targetSnapshotBytes = runtime.Store.ReadPublishedSnapshotPayload(
                    capture.InstallationSnapshotId, checked((int)MaximumSemanticBytes));
                Mo2SnapshotCaptureResult targetCapture = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(
                    targetSnapshotBytes, StrictJson)
                    ?? throw new InvalidDataException("The fresh targeted snapshot publication is malformed.");
                RunRecord sourceRun = runtime.Store.GetRun(preparation.SourceRunId);
                byte[] sourceSnapshotBytes = runtime.Store.ReadPublishedSnapshotPayload(
                    sourceRun.Binding.InstallationSnapshotId, checked((int)MaximumSemanticBytes));
                Mo2SnapshotCaptureResult sourceCapture = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(
                    sourceSnapshotBytes, StrictJson)
                    ?? throw new InvalidDataException("The retained source snapshot publication is malformed.");
                runtime.Store.RecordTargetedSnapshotLink(preparation.PreparationId, preparation.CaptureOperationId,
                    capture.InstallationSnapshotId, Hash(targetSnapshotBytes),
                    sourceCapture.Snapshot!.Contract.StructuralManifestFingerprint.Value,
                    targetCapture.Snapshot!.Contract.StructuralManifestFingerprint.Value,
                    preparation.ConfirmedProfileRevision, DateTimeOffset.UtcNow);
                string acquisitionId = "semantic-acquisition-" + preparation.PreparationId;
                ManagedBethesdaSemanticIntent intent = new([]);
                string requestJson = JsonSerializer.Serialize(intent);
                string requestSha = Hash(requestJson);
                ManagedBethesdaSemanticAssignment sealedAssignment = ManagedRunExecutor.SealBethesdaAssignment(
                    new(targetCapture, intent.RequestedUnsupportedCapabilities));
                string sealedFingerprint = Hash(JsonSerializer.Serialize(sealedAssignment));
                _ = runtime.Store.CreateSemanticAcquisition(acquisitionId, preparation.PreparationId,
                    capture.InstallationSnapshotId, requestJson, requestSha, sealedFingerprint,
                    preparation.DispatchDeadline, runtime.Authority.FencingEpoch, DateTimeOffset.UtcNow);
                preparation = runtime.Store.TransitionTargetedPreparation(preparation.PreparationId,
                    preparation.Revision, preparation.State, TargetedVerificationPreparationState.AcquiringEvidence,
                    "fresh-snapshot-published", JsonSerializer.Serialize(new
                    {
                        captureOperationId = capture.OperationId,
                        targetSnapshotId = capture.InstallationSnapshotId,
                        acquisitionId,
                    }), string.Empty, capture.InstallationSnapshotId, acquisitionId, null, null, false, false,
                    DateTimeOffset.UtcNow);
            }

            if (preparation.State == TargetedVerificationPreparationState.AcquiringEvidence)
            {
                SemanticAcquisitionPersistenceRecord acquisition = runtime.Store.GetSemanticAcquisition(
                    preparation.EvidenceAcquisitionId!);
                SemanticAcquisitionPublicationRecord publication;
                if (acquisition.State == "Completed")
                {
                    publication = runtime.Store.GetSemanticAcquisitionPublication(acquisition.AcquisitionId);
                }
                else
                {
                    publication = await ExecuteAcquisitionAsync(preparation, acquisition).ConfigureAwait(false);
                }
                preparation = runtime.Store.TransitionTargetedPreparation(preparation.PreparationId,
                    preparation.Revision, preparation.State, TargetedVerificationPreparationState.PreparingPlan,
                    "semantic-evidence-published", JsonSerializer.Serialize(new
                    {
                        publication.AcquisitionId,
                        publication.SemanticOutputId,
                        publication.PayloadSha256,
                    }), string.Empty, null, publication.AcquisitionId, null, null, false, false,
                    DateTimeOffset.UtcNow);
            }

            if (preparation.State == TargetedVerificationPreparationState.PreparingPlan)
            {
                ValidateCurrentSelections(preparation);
                SemanticAcquisitionPublicationRecord publication = runtime.Store
                    .GetSemanticAcquisitionPublication(preparation.EvidenceAcquisitionId!);
                TargetedVerificationPlanContract plan = BuildPlan(preparation, publication);
                _ = runtime.Store.StoreTargetedPlan(plan, DateTimeOffset.UtcNow);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Targeted verification preparation failed for {PreparationId}.", preparationId);
            try
            {
                TargetedPreparationPersistenceRecord current = runtime.Store.GetTargetedPreparation(preparationId);
                if (current.State is TargetedVerificationPreparationState.CapturingSnapshot
                    or TargetedVerificationPreparationState.AcquiringEvidence
                    or TargetedVerificationPreparationState.PreparingPlan)
                {
                    TargetedVerificationPreparationState terminal = exception is AnalysisIdentityDriftException
                        ? TargetedVerificationPreparationState.Invalidated
                        : TargetedVerificationPreparationState.Failed;
                    _ = runtime.Store.TransitionTargetedPreparation(current.PreparationId, current.Revision,
                        current.State, terminal, terminal == TargetedVerificationPreparationState.Invalidated
                            ? "preparation-invalidated" : "preparation-failed",
                        JsonSerializer.Serialize(new { reason = Bounded(exception.Message) }), Bounded(exception.Message),
                        null, null, null, null, false, current.Limited, DateTimeOffset.UtcNow);
                }
            }
            catch (Exception persistenceFailure)
            {
                logger.LogError(persistenceFailure,
                    "Failed to persist targeted preparation failure for {PreparationId}.", preparationId);
            }
        }
    }

    private void ValidateCurrentSelections(TargetedPreparationPersistenceRecord preparation)
    {
        SetupObjectRecord profile = runtime.Store.FindSetupObject("profile-selection", "current-profile")
            ?? throw new AnalysisIdentityDriftException("The selected profile is no longer retained.");
        SetupObjectRecord configuration = runtime.Store.FindSetupObject(
            "saved-scan-configuration", preparation.SavedConfigurationId)
            ?? throw new AnalysisIdentityDriftException("The selected configuration is no longer retained.");
        if (profile.LifecycleState != "active" || configuration.LifecycleState != "active"
            || profile.Revision != preparation.ConfirmedProfileRevision
            || configuration.Revision != preparation.SavedConfigurationRevision)
        {
            throw new AnalysisIdentityDriftException("The profile or configuration changed during targeted preparation.");
        }
        using JsonDocument profileDocument = JsonDocument.Parse(profile.PayloadJson);
        JsonElement root = profileDocument.RootElement;
        if (!root.TryGetProperty("ExplicitlyConfirmed", out JsonElement confirmed)
            || confirmed.ValueKind != JsonValueKind.True
            || !root.TryGetProperty("ConfirmedCandidateId", out JsonElement candidate)
            || candidate.GetString() != preparation.ConfirmedProfileId)
        {
            throw new AnalysisIdentityDriftException("The confirmed profile changed during targeted preparation.");
        }
        RunOperationRecord sourceOperation = runtime.Store.GetRunOperation(preparation.SourceRunId)
            ?? throw new AnalysisIdentityDriftException("The source managed operation is no longer retained.");
        ManagedAnalysisOrchestrationRequest source = JsonSerializer.Deserialize<ManagedAnalysisOrchestrationRequest>(
            sourceOperation.RequestJson, ContractJsonSerializer.Options)
            ?? throw new InvalidDataException("The source managed operation is malformed.");
        if (source.AnalysisContext.ContextId.Value != preparation.AnalysisContextId
            || source.AnalysisContext.CanonicalFingerprint.Value != preparation.AnalysisContextFingerprint)
        {
            throw new AnalysisIdentityDriftException("The selected analysis context changed during targeted preparation.");
        }
    }

    private async Task<SnapshotCaptureOperationRecord> AwaitCaptureAsync(
        TargetedPreparationPersistenceRecord preparation)
    {
        while (true)
        {
            TargetedPreparationPersistenceRecord current = runtime.Store.GetTargetedPreparation(preparation.PreparationId);
            if (current.State == TargetedVerificationPreparationState.Cancelled)
            {
                throw new OperationCanceledException("The targeted preparation was cancelled.");
            }
            SnapshotCaptureOperationRecord capture = runtime.Store.GetSnapshotCaptureOperation(
                preparation.CaptureOperationId);
            if (capture.State is "Completed" or "Failed")
            {
                return capture;
            }
            if (DateTimeOffset.UtcNow >= preparation.DispatchDeadline)
            {
                throw new TimeoutException("The targeted snapshot preparation deadline expired.");
            }
            await Task.Delay(25).ConfigureAwait(false);
        }
    }

    private async Task<SemanticAcquisitionPublicationRecord> ExecuteAcquisitionAsync(
        TargetedPreparationPersistenceRecord preparation,
        SemanticAcquisitionPersistenceRecord acquisition)
    {
        byte[] snapshotBytes = runtime.Store.ReadPublishedSnapshotPayload(
            acquisition.TargetSnapshotId, checked((int)MaximumSemanticBytes));
        Mo2SnapshotCaptureResult accepted = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(snapshotBytes, StrictJson)
            ?? throw new InvalidDataException("The targeted acquisition snapshot is malformed.");
        ManagedBethesdaSemanticIntent intent = JsonSerializer.Deserialize<ManagedBethesdaSemanticIntent>(
            acquisition.RequestJson, StrictJson)
            ?? throw new InvalidDataException("The targeted semantic acquisition request is malformed.");
        ManagedBethesdaSemanticAssignment assignment = ManagedRunExecutor.SealBethesdaAssignment(
            new(accepted, intent.RequestedUnsupportedCapabilities));
        if (Hash(JsonSerializer.Serialize(assignment)) != acquisition.SealedInputFingerprint)
        {
            throw new AnalysisIdentityDriftException("The semantic acquisition input seals became stale.");
        }
        SemanticAcquisitionAttemptRecord attempt = runtime.Store.DispatchSemanticAcquisition(
            acquisition.AcquisitionId, acquisition.Generation, runtime.Authority.FencingEpoch,
            TimeSpan.FromMinutes(2), DateTimeOffset.UtcNow);
        try
        {
            using AttemptStagingAuthority staging = runtime.Store.Paths.CreateAttemptStagingDirectory(attempt.AttemptId);
            ManagedWorkerBootstrap bootstrap = new(
                1, Guid.NewGuid().ToString("N"), runtime.Authority.InstanceId, runtime.Authority.FencingEpoch,
                acquisition.AcquisitionId, attempt.AttemptId, attempt.AttemptFencingToken,
                runtime.Descriptor.WorkerPipe, 0, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), 0,
                "bethesda-semantic.v2.json", MaximumSemanticBytes,
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                DateTimeOffset.UtcNow.AddMinutes(2), ManagedWorkerOperationKind.BethesdaSemanticExtraction,
                "2.0.0", null, assignment);
            ManagedWorkerResult result;
            if (inProcessSemanticExecution is null)
            {
                result = await workerLauncher.LaunchWorkerAsync(bootstrap, staging.Handle)
                    .ConfigureAwait(false);
            }
            else
            {
                BethesdaSemanticExtractionResult local = inProcessSemanticExecution(assignment);
                byte[] localBytes = JsonSerializer.SerializeToUtf8Bytes(local);
                string localSha = Hash(localBytes);
                File.WriteAllBytes(Path.Combine(runtime.Store.Paths.Staging, attempt.AttemptId,
                    bootstrap.OutputRelativeName), localBytes);
                result = new ManagedWorkerResult(1, bootstrap.BootstrapId, attempt.AttemptId,
                    attempt.CoordinatorFencingEpoch, attempt.AttemptFencingToken,
                    bootstrap.OutputRelativeName, localSha, localBytes.LongLength,
                    Convert.ToHexStringLower(ManagedWorkerManifest.ComputeDigest(
                        bootstrap.StagedArtifactId, bootstrap.OutputRelativeName, localSha,
                        localBytes.LongLength, bootstrap.OutputSchemaVersion)));
            }
            byte[] staged = runtime.Store.ReadSemanticAcquisitionStagedPayload(attempt,
                result.OutputRelativeName, result.Sha256, result.ByteLength, bootstrap.MaximumOutputBytes);
            BethesdaSemanticExtractionResult semantic = BethesdaSemanticPublicationValidator.DeserializeAndValidate(
                staged, assignment, bootstrap.MaximumOutputBytes);
            TargetedPreparationPersistenceRecord current = runtime.Store.GetTargetedPreparation(preparation.PreparationId);
            if (current.State != TargetedVerificationPreparationState.AcquiringEvidence)
            {
                throw new OperationCanceledException("A cancelled or stale targeted preparation cannot publish evidence.");
            }
            string outputId = "bethesda-semantic-" + result.Sha256[..32];
            string provenance = JsonSerializer.Serialize(new
            {
                acquisition.AcquisitionId,
                acquisition.TargetSnapshotId,
                acquisition.SealedInputFingerprint,
                producerId = semantic.Snapshot!.ProducerId,
                producerVersion = semantic.Snapshot.ProducerVersion.ToString(),
                result.Sha256,
                result.ByteLength,
            });
            return runtime.Store.PublishSemanticAcquisition(attempt, result.OutputRelativeName, result.Sha256,
                result.ByteLength, result.ManifestSha256, bootstrap.MaximumOutputBytes, outputId, provenance,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            try
            {
                runtime.Store.FailSemanticAcquisition(attempt, Bounded(exception.Message), DateTimeOffset.UtcNow);
            }
            catch (InvalidOperationException) when (runtime.Store.GetTargetedPreparation(
                preparation.PreparationId).State == TargetedVerificationPreparationState.Cancelled)
            {
                // The atomic preparation cancellation already fenced this attempt and
                // recorded the semantic acquisition as cancelled.
            }
            throw;
        }
    }

    private TargetedVerificationPlanContract BuildPlan(
        TargetedPreparationPersistenceRecord preparation,
        SemanticAcquisitionPublicationRecord publication)
    {
        RunRecord sourceRun = runtime.Store.GetRun(preparation.SourceRunId);
        RunOperationRecord sourceOperation = runtime.Store.GetRunOperation(preparation.SourceRunId)
            ?? throw new InvalidOperationException("The retained source run has no managed operation.");
        if (sourceOperation.OperationKind != ManagedRunOperationKinds.ManagedAnalysis
            || Hash(sourceOperation.RequestJson) != sourceOperation.RequestSha256)
        {
            throw new AnalysisIdentityDriftException("The retained source managed operation drifted.");
        }
        ManagedAnalysisOrchestrationRequest sourceRequest =
            JsonSerializer.Deserialize<ManagedAnalysisOrchestrationRequest>(
                sourceOperation.RequestJson, ContractJsonSerializer.Options)
            ?? throw new InvalidDataException("The retained source managed operation is malformed.");
        ManagedAnalysisOrchestrator.Validate(sourceRequest, sourceRun.RunId, sourceRun.Binding);
        ResultItemPersistenceRecord rootItem = runtime.Store.GetResultItem(
            preparation.SourceRunId, preparation.SourceOccurrenceId);
        byte[] sourcePayloadBytes = TargetedVerificationSourceIdentity.ReadCanonicalPayload(
            runtime.Store, preparation.SourceRunId, rootItem);
        FindingCaseContract canonical = FindingCaseJsonCodec.Deserialize(sourcePayloadBytes);
        TargetedCanonicalSourceIdentity canonicalIdentity = TargetedVerificationSourceIdentity.Resolve(
            canonical, preparation.SourceOccurrenceKind, preparation.SourceOccurrenceId);
        if (canonicalIdentity.LogicalId.Value != rootItem.LogicalId
            || canonicalIdentity.IdentityEnvelope.AnalyzerFamily != rootItem.AnalyzerId
            || canonicalIdentity.IdentityEnvelope.AnalyzerVersion.ToString() != rootItem.AnalyzerVersion)
        {
            throw new AnalysisIdentityDriftException(
                "The canonical source occurrence identity differs from its retained index.");
        }
        AnalysisPhaseCheckpointRecord candidateCheckpoint = runtime.Store.ReadLatestAnalysisPhaseCheckpoint(
            preparation.SourceRunId, CandidateAnalysisPhase.PhaseId)
            ?? throw new InvalidOperationException("The retained source run has no canonical candidate checkpoint.");
        byte[] candidateBytes = runtime.Store.ReadCandidateAnalysisPayload(candidateCheckpoint.PayloadId);
        if (Hash(candidateBytes) != candidateCheckpoint.PayloadSha256)
        {
            throw new AnalysisIdentityDriftException("The canonical source candidate payload drifted.");
        }
        CandidateAnalysisContract sourceCandidates = CandidateAnalysisJsonCodec.Deserialize(candidateBytes);
        CandidateDeliveredInputContract sourceDelivered = sourceRequest.Candidate.DeliveredInput
            ?? throw new AnalysisIdentityDriftException(
                "The retained source managed operation has no canonical delivered input.");
        byte[] sourceDeliveredBytes = CandidateDeliveredInputJsonCodec.Serialize(sourceDelivered);
        if (sourceDelivered.PayloadId != sourceCandidates.DeliveredInputId
            || sourceRequest.Candidate.DeliveredInputByteFingerprint?.Value != Hash(sourceDeliveredBytes))
        {
            throw new AnalysisIdentityDriftException(
                "The canonical source delivered input differs from the retained candidate checkpoint.");
        }
        if (sourceDelivered.OriginatingRunId.Value != preparation.SourceRunId
            || sourceDelivered.SourceSnapshotId.Value != sourceRun.Binding.InstallationSnapshotId)
        {
            throw new AnalysisIdentityDriftException("The canonical source delivered input no longer matches its source run.");
        }
        FindingContract[] findings;
        AnalysisCaseContract? sourceCase = null;
        if (preparation.SourceOccurrenceKind == "finding")
        {
            findings = [canonical.Findings.Single(item => item.FindingOccurrenceId.Value == preparation.SourceOccurrenceId)];
        }
        else
        {
            sourceCase = canonical.Cases.Single(item => item.CaseOccurrenceId.Value == preparation.SourceOccurrenceId);
            findings = sourceCase.FindingOccurrenceIds.Select(id => canonical.Findings.Single(item =>
                item.FindingOccurrenceId == id)).ToArray();
        }

        Dictionary<OpaqueId, TargetedScopeMemberContract> members = [];
        List<TargetedScopeDependencyContract> edges = [];
        List<TargetedScopeMemberContract> roots = [];
        FindingContract? sourceFinding = preparation.SourceOccurrenceKind == "finding"
            ? findings.Single()
            : null;
        TargetedScopeMemberContract root = Member(preparation.SourceOccurrenceId,
            preparation.SourceOccurrenceKind == "finding" ? TargetedScopeMemberKind.Finding : TargetedScopeMemberKind.Case,
            sourceFinding?.IdentityEnvelopeId ?? new OpaqueId(rootItem.LogicalId),
            "canonical source occurrence", true,
            sourceFinding is null
                ? [new(rootItem.SourcePayloadId)]
                : sourceFinding.EvidenceIds.Append(new OpaqueId(rootItem.SourcePayloadId)).Distinct().ToArray());
        Add(root); roots.Add(root);
        foreach (FindingContract finding in findings.OrderBy(item => item.FindingOccurrenceId.Value, StringComparer.Ordinal))
        {
            TargetedScopeMemberContract findingMember;
            if (sourceFinding is not null)
            {
                findingMember = root;
            }
            else
            {
                findingMember = Member(finding.FindingOccurrenceId.Value,
                    TargetedScopeMemberKind.Finding, finding.IdentityEnvelopeId, "source finding root", true,
                    finding.EvidenceIds);
                Add(findingMember);
                Link(root, findingMember, "case-member", finding.EvidenceIds);
            }
            TargetedScopeMemberContract candidate = Member(finding.CandidateId.Value, TargetedScopeMemberKind.Candidate,
                finding.CandidateId, "source candidate", true, finding.EvidenceIds);
            TargetedScopeMemberContract hypothesis = Member(finding.HypothesisId.Value, TargetedScopeMemberKind.Hypothesis,
                finding.HypothesisId, "source hypothesis", true, finding.EvidenceIds);
            Add(candidate); Add(hypothesis); Link(findingMember, candidate, "root-member", finding.EvidenceIds);
            Link(candidate, hypothesis, "candidate-hypothesis", finding.EvidenceIds);
            CandidateAnalysisEntryContract candidateResult = sourceCandidates.Candidates.Single(item =>
                item.CandidateId == finding.CandidateId && item.HypothesisId == finding.HypothesisId);
            CandidateDecisionContract decision = sourceCandidates.Decisions.Single(item =>
                item.DecisionId == candidateResult.DecisionId);
            foreach (CandidateParticipantContract sourceParticipant in decision.Participants
                         .OrderBy(item => item.ParticipantId.Value, StringComparer.Ordinal))
            {
                TargetedScopeMemberContract participant = Member(sourceParticipant.ParticipantId.Value,
                    ParticipantKind(sourceParticipant.Role), sourceParticipant.ParticipantId,
                    "canonical source candidate participant: " + sourceParticipant.Role, true,
                    decision.EvidenceIds);
                Add(participant); Link(candidate, participant, "candidate-participant", decision.EvidenceIds);
            }
            foreach (OpaqueId dependencyId in decision.DependencyIds.OrderBy(item => item.Value, StringComparer.Ordinal))
            {
                TargetedScopeMemberContract dependency = Member(dependencyId.Value,
                    TargetedScopeMemberKind.Evidence, dependencyId,
                    "retained candidate dependency proof", true, decision.EvidenceIds);
                Add(dependency); Link(candidate, dependency, "candidate-dependency", decision.EvidenceIds);
            }
            foreach (OpaqueId evidenceId in finding.EvidenceIds)
            {
                TargetedScopeMemberContract evidence = Member(evidenceId.Value, TargetedScopeMemberKind.Evidence,
                    evidenceId, "retained source evidence dependency", true, [evidenceId]);
                Add(evidence); Link(hypothesis, evidence, "evidence-support", [evidenceId]);
            }
        }
        if (sourceCase is not null)
        {
            foreach (OpaqueId proof in sourceCase.CauseProofEvidenceIds)
            {
                TargetedScopeMemberContract evidence = Member(proof.Value, TargetedScopeMemberKind.Evidence,
                    proof, "shared-cause proof dependency", true, [proof]);
                Add(evidence); Link(root, evidence, "shared-cause-member", [proof]);
            }
        }
        OpaqueId analyzerId = new(rootItem.AnalyzerId);
        TargetedScopeMemberContract analyzer = Member(rootItem.AnalyzerId, TargetedScopeMemberKind.Analyzer,
            analyzerId, "accepted source analyzer family", true, [new(rootItem.SourcePayloadId)]);
        Add(analyzer); Link(root, analyzer, "analyzer-population", [new(rootItem.SourcePayloadId)]);

        TargetedAnalysisScopeContract scope = TargetedVerificationPlanner.CloseScope(
            new(preparation.PreparationId), new(preparation.SourceOccurrenceId), roots,
            members.Values.ToArray(), edges);
        byte[] targetSemanticBytes = runtime.Store.ReadCandidateAnalysisPayload(publication.PayloadId);
        byte[] qualifiedSnapshotBytes = runtime.Store.ReadPublishedSnapshotPayload(
            publication.TargetSnapshotId, checked((int)MaximumSemanticBytes));
        Mo2SnapshotCaptureResult qualifiedCapture = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(
            qualifiedSnapshotBytes, StrictJson)
            ?? throw new InvalidDataException("The targeted snapshot publication is malformed.");
        ManagedBethesdaSemanticAssignment qualifiedAssignment = ManagedRunExecutor.SealBethesdaAssignment(
            new(qualifiedCapture, []));
        BethesdaSemanticExtractionResult targetSemantic = BethesdaSemanticPublicationValidator.DeserializeAndValidate(
            targetSemanticBytes, qualifiedAssignment, MaximumSemanticBytes);
        BethesdaSemanticSnapshot targetSnapshot = targetSemantic.Snapshot
            ?? throw new InvalidDataException("The targeted semantic publication has no qualified snapshot.");
        CandidateDeliveredInputContract targetDelivered = CandidateDeliveredInputAdapter.Create(
            new("targeted-planning-" + preparation.PreparationId), new(publication.TargetSnapshotId),
            new(preparation.AnalysisContextId), new(preparation.SavedConfigurationId), targetSnapshot,
            documentationEvidence: null);
        OpaqueId acquisitionProof = new(publication.PublicationId);
        List<TargetedCurrentObservationContract> observations = [];
        foreach (TargetedScopeMemberContract member in scope.Members)
        {
            bool analytical = member.Kind is TargetedScopeMemberKind.Finding or TargetedScopeMemberKind.Case
                or TargetedScopeMemberKind.Candidate or TargetedScopeMemberKind.Hypothesis
                or TargetedScopeMemberKind.Analyzer;
            if (analytical)
            {
                observations.Add(new(member.StableIdentity, new("qualified-target-semantic-population"),
                    member.StableIdentity, null, TargetedCorrelationStatus.ChangedCorrelated, true, true,
                    "The analytical identity is retained for fresh evaluation over the correlated target population.",
                    [acquisitionProof], acquisitionProof));
                continue;
            }
            if (member.Kind == TargetedScopeMemberKind.Evidence)
            {
                observations.Add(new(member.StableIdentity, new("retained-source-proof-population"),
                    null, null, TargetedCorrelationStatus.ProvenNotApplicable, true, true,
                    "This retained source proof is not a target-snapshot entity and remains only provenance for scope closure.",
                    [acquisitionProof, .. member.SourceProofIds], acquisitionProof));
                continue;
            }
            observations.Add(CorrelateCurrentMember(member, sourceDelivered, targetDelivered,
                targetSnapshot, targetSemantic, qualifiedCapture, acquisitionProof));
        }
        TargetedCorrelationCoverageContract coverage = TargetedVerificationPlanner.Correlate(
            new(preparation.PreparationId), scope, new(publication.TargetSnapshotId), new(publication.AcquisitionId),
            new(publication.SemanticOutputId), observations);
        TargetedVerificationSourceContract source = new(new(preparation.SourceRunId),
            preparation.SourceOccurrenceKind == "finding" ? TargetedVerificationRootKind.Finding : TargetedVerificationRootKind.Case,
            new(preparation.SourceOccurrenceId), canonicalIdentity.LogicalId, new(rootItem.SourcePayloadId),
            new(rootItem.SourcePayloadSha256), canonicalIdentity.IdentityEnvelope.CanonicalSignature,
            canonicalIdentity.IdentityEnvelope.AnalyzerFamily, canonicalIdentity.IdentityEnvelope.AnalyzerVersion,
            canonicalIdentity.IdentityEnvelope.SemanticContractVersion,
            canonicalIdentity.IdentityEnvelope.IdentityContractVersion,
            new(sourceRun.Binding.InstallationSnapshotId), new(sourceRun.Binding.AnalysisContextId),
            new(sourceRun.Binding.EffectiveScanConfigurationId), new(sourceRun.Binding.ResolvedInputManifestId));
        List<TargetedReuseDecisionContract> reuse =
        [
            new("source-managed-operation", new(sourceRun.RunId), "reuse-with-proof",
                new("targeted-source-operation-proof-" + sourceOperation.RequestSha256[..24]),
                new(sourceOperation.RequestSha256),
                "The exact retained managed-analysis-v1 source operation is revalidated before targeted start admission."),
            Recompute("installation-snapshot", publication.TargetSnapshotId, publication.PublicationId,
                "The source snapshot is never target proof."),
            Recompute("bethesda-semantic-input", publication.SemanticOutputId, publication.PublicationId,
                "Snapshot-dependent semantic evidence was freshly acquired."),
            Recompute("candidate-delivered-input", sourceDelivered.PayloadId.Value, publication.PublicationId,
                "Candidate delivery is rebound to executable current scope members."),
            new("analysis-context", sourceRequest.AnalysisContext.ContextId, "reuse-with-proof",
                new("targeted-context-proof-" + sourceRequest.AnalysisContext.CanonicalFingerprint.Value[..24]),
                sourceRequest.AnalysisContext.CanonicalFingerprint,
                "The exact retained analysis context is unchanged and was revision-validated for preparation."),
        ];
        HashSet<OpaqueId> documentationIds =
        [
            sourceRequest.DocumentationImport.Manifest.SourceId,
        ];
        if (sourceRequest.DocumentationImport.RetainedEvidence is { } retainedDocumentation)
        {
            documentationIds.Add(retainedDocumentation.PayloadId);
        }
        foreach (ArtifactReferenceContract sourceInput in sourceRequest.ExecutionInput.SourceInputs)
        {
            if (sourceInput.ArtifactId == sourceDelivered.PayloadId)
            {
                continue;
            }
            if (!documentationIds.Contains(sourceInput.ArtifactId))
            {
                throw new InvalidDataException(
                    "A source input has no accepted targeted-verification reuse classification.");
            }
            reuse.Add(new("documentation-evidence", sourceInput.ArtifactId, "reuse-with-proof",
                new("targeted-documentation-proof-" + sourceInput.Fingerprint.Value[..24]),
                sourceInput.Fingerprint,
                "Retained source documentation is snapshot-independent and is reused only by exact bytes."));
        }
        foreach (ArtifactReferenceContract analyzerInput in sourceRequest.ExecutionInput.AnalyzerDeclarations)
        {
            reuse.Add(new("analyzer-declaration", analyzerInput.ArtifactId, "reuse-with-proof",
                new("targeted-analyzer-proof-" + analyzerInput.Fingerprint.Value[..24]),
                analyzerInput.Fingerprint,
                "The accepted analyzer declaration is reused only by exact identity, version, and bytes."));
        }
        SetupObjectRecord effectiveConfiguration = runtime.Store.FindSetupObject(
            "saved-scan-configuration", preparation.SavedConfigurationId)
            ?? throw new AnalysisIdentityDriftException("The targeted saved configuration is no longer retained.");
        Sha256Fingerprint effectiveConfigurationFingerprint = new(Hash(effectiveConfiguration.PayloadJson));
        reuse.Add(new("effective-configuration", new(preparation.SavedConfigurationId), "reuse-with-proof",
            new("targeted-configuration-proof-" + effectiveConfigurationFingerprint.Value[..24]),
            effectiveConfigurationFingerprint,
            "The exact active saved-configuration revision is rebound and revalidated at start."));
        TargetedVerificationPlanContract draft = new("infinium/targeted-verification-plan", new(1, 1, 0),
            new("targeted-plan-pending"), new(preparation.PreparationId), preparation.Revision,
            source, new(preparation.CaptureOperationId), new(publication.TargetSnapshotId),
            new(Hash(qualifiedSnapshotBytes)),
            new(publication.AcquisitionId), new(publication.SemanticOutputId), new(publication.PayloadSha256),
            scope, coverage, reuse, "scope-limited-no-readiness", coverage.Startable, coverage.Limited,
            coverage.NonStartableReasons, coverage.Gaps, new(new string('0', 64)));
        Sha256Fingerprint planFingerprint = TargetedVerificationContractInvariants.ComputePlanFingerprint(draft);
        return draft with
        {
            PlanId = new("targeted-plan-" + planFingerprint.Value[..32]),
            PlanFingerprint = planFingerprint,
        };

        void Add(TargetedScopeMemberContract value)
        {
            if (!members.TryGetValue(value.MemberId, out TargetedScopeMemberContract? existing))
            {
                members.Add(value.MemberId, value);
                return;
            }
            if (existing.Kind != value.Kind
                || existing.StableIdentity != value.StableIdentity
                || existing.Reason != value.Reason
                || existing.Mandatory != value.Mandatory)
            {
                throw new InvalidDataException(
                    "A canonical targeted scope member identity has conflicting source meaning.");
            }
            members[value.MemberId] = existing with
            {
                SourceProofIds = existing.SourceProofIds.Concat(value.SourceProofIds).Distinct()
                    .OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
            };
        }
        void Link(TargetedScopeMemberContract from, TargetedScopeMemberContract to, string relation,
            IReadOnlyList<OpaqueId> proofIds)
        {
            string material = string.Join('\n', from.MemberId.Value, to.MemberId.Value, relation,
                string.Join('|', proofIds.Select(id => id.Value).Order(StringComparer.Ordinal)));
            edges.Add(new(new OpaqueId("target-edge-" + Hash(material)[..32]), from.MemberId, to.MemberId,
                relation, proofIds));
        }
    }

    private static TargetedScopeMemberContract Member(string identity, TargetedScopeMemberKind kind,
        OpaqueId stableIdentity, string reason, bool mandatory, IReadOnlyList<OpaqueId> proofIds) =>
        new(new OpaqueId("target-member-" + Hash(kind + "\n" + identity)[..32]), kind,
            stableIdentity, reason, mandatory, proofIds);

    private static TargetedScopeMemberKind ParticipantKind(string role) => role switch
    {
        "record" or "prior-target" or "winning-target" => TargetedScopeMemberKind.Record,
        "prior-contribution" or "winning-contribution" => TargetedScopeMemberKind.Contribution,
        "mesh-asset" or "tint-asset" => TargetedScopeMemberKind.Asset,
        "mesh-provider" or "tint-provider" => TargetedScopeMemberKind.Provider,
        _ => TargetedScopeMemberKind.Participant,
    };

    internal static TargetedCurrentObservationContract CorrelateCurrentMember(
        TargetedScopeMemberContract member,
        CandidateDeliveredInputContract source,
        CandidateDeliveredInputContract target,
        BethesdaSemanticSnapshot targetSnapshot,
        BethesdaSemanticExtractionResult targetSemantic,
        Mo2SnapshotCaptureResult targetCapture,
        OpaqueId proof)
    {
        HashSet<OpaqueId> current = CurrentIdentities(target, member.Kind);
        if (current.Contains(member.StableIdentity)
            || TargetContains(targetSnapshot, member.Kind, member.StableIdentity.Value))
        {
            if (ProcessingGap(member, target, targetSnapshot, targetSemantic, targetCapture) is
                (TargetedCorrelationStatus Status, string Reason, OpaqueId Evidence) gap)
            {
                return new(member.StableIdentity, new("qualified-target-semantic-population"),
                    member.StableIdentity, member.MemberId, gap.Status, true, false, gap.Reason,
                    [proof, gap.Evidence], proof);
            }
            return new(member.StableIdentity, new("qualified-target-semantic-population"), member.StableIdentity,
                member.MemberId, TargetedCorrelationStatus.MatchedExecutable, true, true,
                "The exact typed stable identity is present in the fresh semantic extraction.", [proof], proof);
        }

        OpaqueId[] related = RelatedCurrentIdentities(member, source, target);
        if (related.Length != 0)
        {
            return new(member.StableIdentity, new("qualified-target-semantic-population"), null, null,
                TargetedCorrelationStatus.Ambiguous, false, false,
                "The source slot has a different current identity but no retained typed continuity, equivalence, or provider-lineage proof.",
                [proof], proof);
        }
        if (RequiresMissingProof(member, source, target))
        {
            return new(member.StableIdentity, new("qualified-target-semantic-population"), null, null,
                TargetedCorrelationStatus.MissingRequiredProof, false, false,
                "The current semantic population cannot prove identity or absence for this mandatory source member.", [proof], proof);
        }
        return new(member.StableIdentity, new("qualified-target-semantic-population"), null, null,
            TargetedCorrelationStatus.ProvenAbsent, true, true,
            "Complete qualified target enumeration contains no match for the exact typed stable identity; this proves absence only.",
            [proof], proof);
    }

    private static (TargetedCorrelationStatus Status, string Reason, OpaqueId Evidence)? ProcessingGap(
        TargetedScopeMemberContract member,
        CandidateDeliveredInputContract target,
        BethesdaSemanticSnapshot targetSnapshot,
        BethesdaSemanticExtractionResult targetSemantic,
        Mo2SnapshotCaptureResult targetCapture)
    {
        if (member.Kind is TargetedScopeMemberKind.Record or TargetedScopeMemberKind.Participant)
        {
            string[] signatures = targetSnapshot.OverrideChains.Values
                .Where(chain => CandidateAnalysisIdentity.StableId(
                    "candidate-delivered-source", "record", chain.Identity.ParticipantId) == member.StableIdentity)
                .Select(chain => chain.Identity.Signature.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            BethesdaCoverageGap? malformed = targetSemantic.Gaps.FirstOrDefault(gap =>
                gap.Category == BethesdaCoverageGapCategory.UnsupportedShape
                && signatures.Any(signature => gap.Detail.StartsWith(signature + ":", StringComparison.Ordinal)));
            if (malformed is not null)
            {
                return (TargetedCorrelationStatus.Malformed,
                    "The exact correlated record is retained, but qualified semantic evidence reports an unsupported content shape; the member remains in the denominator as malformed processing.",
                    GapEvidence("semantic", malformed.GapId));
            }
            BethesdaCoverageGap? unsupported = targetSemantic.Gaps.FirstOrDefault(gap =>
                gap.Category is BethesdaCoverageGapCategory.UnsupportedRecord
                    or BethesdaCoverageGapCategory.UnsupportedField
                && signatures.Any(signature => gap.Detail.StartsWith(signature, StringComparison.Ordinal)));
            if (unsupported is not null)
            {
                return (TargetedCorrelationStatus.Unsupported,
                    "The exact correlated record is retained, but its qualified semantic population reports unsupported processing; the member remains in the denominator.",
                    GapEvidence("semantic", unsupported.GapId));
            }
        }

        if (member.Kind is TargetedScopeMemberKind.Asset or TargetedScopeMemberKind.Provider)
        {
            CandidateDeliveredFaceGenFactContract[] facts = target.FaceGenFacts.Where(fact =>
                    fact.MeshAssetId == member.StableIdentity || fact.TintAssetId == member.StableIdentity
                    || fact.MeshProviderParticipantId == member.StableIdentity
                    || fact.TintProviderParticipantId == member.StableIdentity)
                .ToArray();
            bool unknownContent = facts.Any(fact =>
                (fact.MeshAssetId == member.StableIdentity || fact.MeshProviderParticipantId == member.StableIdentity)
                    && fact.MeshAvailability == CandidateDeliveredAssetAvailability.Unknown
                || (fact.TintAssetId == member.StableIdentity || fact.TintProviderParticipantId == member.StableIdentity)
                    && fact.TintAvailability == CandidateDeliveredAssetAvailability.Unknown);
            if (unknownContent)
            {
                SnapshotGap? inaccessible = targetCapture.Gaps.FirstOrDefault(gap =>
                    gap.Code == "reparse-point-unsupported" && gap.Population == "filesystem");
                if (inaccessible is not null)
                {
                    return (TargetedCorrelationStatus.Inaccessible,
                        "The exact correlated asset or provider identity is retained, but the qualified capture could not traverse part of its content population; the member remains in the denominator.",
                        GapEvidence("snapshot", inaccessible.Code + "\n" + inaccessible.Population));
                }
                BethesdaCoverageGap? unsupported = targetSemantic.Gaps.FirstOrDefault(gap =>
                    gap.Category == BethesdaCoverageGapCategory.Capability
                    && gap.Population is "face-gen-archive-assets" or "face-gen-loose-assets");
                if (unsupported is not null)
                {
                    return (TargetedCorrelationStatus.Unsupported,
                        "The exact correlated asset or provider identity is retained, but its accepted content adapter is unsupported; the member remains in the denominator.",
                        GapEvidence("semantic", unsupported.GapId));
                }
            }
        }

        return null;
    }

    private static OpaqueId GapEvidence(string kind, string identity) =>
        new("targeted-processing-gap-" + Hash(kind + "\n" + identity)[..32]);

    private static HashSet<OpaqueId> CurrentIdentities(
        CandidateDeliveredInputContract input, TargetedScopeMemberKind kind)
    {
        IEnumerable<OpaqueId> values = kind switch
        {
            TargetedScopeMemberKind.Record => input.LinkFacts.SelectMany(item =>
                    new[] { item.RecordParticipantId, item.PriorTargetParticipantId, item.WinningTargetParticipantId })
                .Concat(input.FaceGenFacts.Select(item => (OpaqueId?)item.NpcParticipantId))
                .Where(item => item is not null).Cast<OpaqueId>(),
            TargetedScopeMemberKind.Contribution => input.LinkFacts.SelectMany(item =>
                new[] { item.PriorContributionId, item.WinningContributionId }),
            TargetedScopeMemberKind.Asset => input.FaceGenFacts.SelectMany(item =>
                new[] { item.MeshAssetId, item.TintAssetId }),
            TargetedScopeMemberKind.Provider => input.FaceGenFacts.SelectMany(item =>
                    new[] { item.MeshProviderParticipantId, item.TintProviderParticipantId })
                .Where(item => item is not null).Cast<OpaqueId>(),
            TargetedScopeMemberKind.ApplicabilityPopulation => input.CoverageGapFacts.Select(item => item.PopulationId),
            TargetedScopeMemberKind.Participant => input.LinkFacts.SelectMany(item =>
                    new[] { item.RecordParticipantId, item.PriorContributionId, item.WinningContributionId,
                        item.PriorTargetParticipantId, item.WinningTargetParticipantId })
                .Concat(input.FaceGenFacts.SelectMany(item => new[] { (OpaqueId?)item.NpcParticipantId,
                    item.MeshAssetId, item.TintAssetId, item.MeshProviderParticipantId, item.TintProviderParticipantId }))
                .Where(item => item is not null).Cast<OpaqueId>(),
            _ => [],
        };
        return values.ToHashSet();
    }

    private static OpaqueId[] RelatedCurrentIdentities(
        TargetedScopeMemberContract member,
        CandidateDeliveredInputContract source,
        CandidateDeliveredInputContract target)
    {
        HashSet<OpaqueId> related = [];
        if (member.Kind == TargetedScopeMemberKind.Contribution)
        {
            foreach (CandidateDeliveredLinkFactContract sourceFact in source.LinkFacts.Where(item =>
                         item.PriorContributionId == member.StableIdentity || item.WinningContributionId == member.StableIdentity))
            {
                foreach (CandidateDeliveredLinkFactContract targetFact in target.LinkFacts.Where(item =>
                             item.RecordParticipantId == sourceFact.RecordParticipantId && item.Field == sourceFact.Field
                             && item.Component == sourceFact.Component && item.Ordinal == sourceFact.Ordinal))
                {
                    related.Add(sourceFact.PriorContributionId == member.StableIdentity
                        ? targetFact.PriorContributionId : targetFact.WinningContributionId);
                }
            }
        }
        else if (member.Kind is TargetedScopeMemberKind.Asset or TargetedScopeMemberKind.Provider)
        {
            foreach (CandidateDeliveredFaceGenFactContract sourceFact in source.FaceGenFacts)
            {
                CandidateDeliveredFaceGenFactContract[] targetFacts = target.FaceGenFacts
                    .Where(item => item.NpcParticipantId == sourceFact.NpcParticipantId).ToArray();
                if (member.Kind == TargetedScopeMemberKind.Asset)
                {
                    if (sourceFact.MeshAssetId == member.StableIdentity)
                    {
                        foreach (CandidateDeliveredFaceGenFactContract item in targetFacts)
                        {
                            related.Add(item.MeshAssetId);
                        }
                    }
                    if (sourceFact.TintAssetId == member.StableIdentity)
                    {
                        foreach (CandidateDeliveredFaceGenFactContract item in targetFacts)
                        {
                            related.Add(item.TintAssetId);
                        }
                    }
                }
                else
                {
                    if (sourceFact.MeshProviderParticipantId == member.StableIdentity)
                    {
                        foreach (OpaqueId item in targetFacts.Select(value => value.MeshProviderParticipantId)
                                     .Where(value => value is not null).Cast<OpaqueId>())
                        {
                            related.Add(item);
                        }
                    }
                    if (sourceFact.TintProviderParticipantId == member.StableIdentity)
                    {
                        foreach (OpaqueId item in targetFacts.Select(value => value.TintProviderParticipantId)
                                     .Where(value => value is not null).Cast<OpaqueId>())
                        {
                            related.Add(item);
                        }
                    }
                }
            }
        }
        related.Remove(member.StableIdentity);
        return related.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray();
    }

    private static bool RequiresMissingProof(TargetedScopeMemberContract member,
        CandidateDeliveredInputContract source, CandidateDeliveredInputContract target)
    {
        if (member.Kind is not (TargetedScopeMemberKind.Asset or TargetedScopeMemberKind.Provider))
        {
            return false;
        }
        foreach (CandidateDeliveredFaceGenFactContract sourceFact in source.FaceGenFacts)
        {
            foreach (CandidateDeliveredFaceGenFactContract targetFact in target.FaceGenFacts.Where(item =>
                         item.NpcParticipantId == sourceFact.NpcParticipantId))
            {
                if ((sourceFact.MeshAssetId == member.StableIdentity
                        || sourceFact.MeshProviderParticipantId == member.StableIdentity)
                    && (targetFact.MeshAvailability == CandidateDeliveredAssetAvailability.Unknown
                        || targetFact.Applicability == CandidateDeliveredFaceGenApplicability.Unknown))
                {
                    return true;
                }
                if ((sourceFact.TintAssetId == member.StableIdentity
                        || sourceFact.TintProviderParticipantId == member.StableIdentity)
                    && (targetFact.TintAvailability == CandidateDeliveredAssetAvailability.Unknown
                        || targetFact.Applicability == CandidateDeliveredFaceGenApplicability.Unknown))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool TargetContains(BethesdaSemanticSnapshot snapshot, TargetedScopeMemberKind kind, string identity) =>
        kind switch
        {
            TargetedScopeMemberKind.Participant or TargetedScopeMemberKind.Record =>
                snapshot.ResolvedParticipants.Values.Any(item =>
                    DeliveredIdentity("record", item.ParticipantId).Value == identity)
                || snapshot.OverrideChains.Values.Any(item =>
                    DeliveredIdentity("record", item.Identity.ParticipantId).Value == identity),
            TargetedScopeMemberKind.Contribution => snapshot.OverrideChains.Values
                .SelectMany(item => item.Contributions).Any(item =>
                    DeliveredIdentity("contribution", item.ContributionId).Value == identity),
            TargetedScopeMemberKind.Provider => snapshot.Plugins.Any(item =>
                DeliveredIdentity("provider", item.LocalInstalledEntityId.Value).Value == identity)
                || snapshot.FaceGen.SelectMany(item => item.Mesh.ProviderParticipantIds
                    .Concat(item.Tint.ProviderParticipantIds)).Any(item =>
                    DeliveredIdentity("provider", item).Value == identity),
            TargetedScopeMemberKind.Asset => snapshot.FaceGen.Any(item =>
                DeliveredIdentity("asset", item.Mesh.NormalizedRelativePath).Value == identity
                || DeliveredIdentity("asset", item.Tint.NormalizedRelativePath).Value == identity),
            TargetedScopeMemberKind.ApplicabilityPopulation => snapshot.Coverage.Any(item =>
                DeliveredIdentity("population", item.Population).Value == identity),
            _ => false,
        };

    private static OpaqueId DeliveredIdentity(string kind, string value) =>
        CandidateAnalysisIdentity.StableId("candidate-delivered-source", kind, value);

    private static TargetedReuseDecisionContract Recompute(string kind, string artifactId, string proofId,
        string reason) => new(kind, new(artifactId), "recompute", new(proofId),
        new(Hash(string.Join('\n', kind, artifactId, proofId, reason))), reason);

    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    private static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private static string Bounded(string value) => value.Length <= 512 ? value : value[..512];
}
