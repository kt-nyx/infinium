using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using Infinium.Application.Analysis;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Bethesda;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Infinium.Persistence;

namespace Infinium.Coordinator;

internal sealed record TargetedTerminalGapCursor(
    long PreparationRevision,
    long AcquisitionSequence,
    int Offset,
    string BindingFingerprint);

public sealed partial class ApplicationGrpcService
{
    public override Task<BeginTargetedVerificationPreparationResponse> BeginTargetedVerificationPreparation(
        BeginTargetedVerificationPreparationRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new BeginTargetedVerificationPreparationResponse { Failure = contractFailure });
        }

        try
        {
            string sourceRunId = Required(request.SourceRunId?.Value, "source run ID");
            string occurrenceKind = request.SourceOccurrenceCase ==
                BeginTargetedVerificationPreparationRequest.SourceOccurrenceOneofCase.SourceFindingOccurrenceId
                    ? "finding" : "case";
            string occurrenceId = occurrenceKind == "finding"
                ? request.SourceFindingOccurrenceId : request.SourceCaseOccurrenceId;
            string preparationId = string.IsNullOrWhiteSpace(request.RequestedPreparationId)
                ? "targeted-preparation-" + HashTargeted(string.Join('\n', request.IdempotencyKey, sourceRunId,
                    occurrenceKind, occurrenceId, request.UserGestureId))[..32]
                : request.RequestedPreparationId;
            DateTimeOffset dispatchDeadline = FromProto(request.DispatchDeadline).ToUniversalTime();
            string requestJson = JsonSerializer.Serialize(new
            {
                schema = "infinium/targeted-verification-preparation-request/v1",
                request.IdempotencyKey,
                request.UserGestureId,
                sourceRunId,
                occurrenceKind,
                occurrenceId,
                request.ConfirmedProfileId,
                request.ExpectedConfirmedProfileRevision,
                request.SavedConfigurationId,
                request.ExpectedSavedConfigurationRevision,
                request.AnalysisContextId,
                request.ExpectedAnalysisContextRevision,
                request.AnalysisContextFingerprintSha256,
                preparationId,
                initiationKind = request.InitiationKind.ToString(),
                dispatchDeadline,
            });
            string requestSha = HashTargeted(requestJson);
            TargetedPreparationPersistenceRecord? replay = runtime.Store.FindTargetedPreparationByCommand(
                request.IdempotencyKey);
            if (replay is not null)
            {
                if (replay.PreparationId != preparationId
                    || replay.UserGestureId != request.UserGestureId
                    || replay.RequestSha256 != requestSha)
                {
                    throw new InvalidOperationException("A targeted preparation idempotency key cannot be rebound.");
                }
                targetedVerificationExecutor.Schedule(replay.PreparationId);
                return Task.FromResult(new BeginTargetedVerificationPreparationResponse
                {
                    Disposition = CommandDisposition.AlreadyAccepted,
                    Preparation = ToTargetedPreparation(runtime.Store.GetTargetedPreparationReadbackSnapshot(
                        replay.PreparationId, 100, null), 100, null, 100, null, null,
                        100, null, 100, null, 100, null),
                });
            }
            if (!runtime.TryAdmitNewDurableCommand(DateTimeOffset.UtcNow))
            {
                return Task.FromResult(new BeginTargetedVerificationPreparationResponse
                {
                    Disposition = CommandDisposition.Rejected,
                    Failure = Failure(FailureCode.LimitExceeded, "The new durable-command rate bound is full."),
                });
            }

            RunRecord sourceRun = runtime.Store.GetRun(sourceRunId);
            if (sourceRun.State is not (Infinium.Domain.Contracts.LifecycleState.Completed
                    or Infinium.Domain.Contracts.LifecycleState.CompletedWithGaps
                    or Infinium.Domain.Contracts.LifecycleState.LimitReached))
            {
                throw new InvalidOperationException("Targeted preparation requires a retained terminal analytical source result.");
            }

            ResultItemPersistenceRecord sourceItem = runtime.Store.GetResultItem(sourceRunId, occurrenceId);
            if (!TargetedVerificationSourceIdentity.ProjectionKindMatches(occurrenceKind, sourceItem.Kind))
            {
                throw new InvalidOperationException("The targeted source occurrence kind differs from canonical result state.");
            }

            byte[] canonicalBytes = TargetedVerificationSourceIdentity.ReadCanonicalPayload(
                runtime.Store, sourceRunId, sourceItem);

            FindingCaseContract canonical = FindingCaseJsonCodec.Deserialize(canonicalBytes);
            TargetedCanonicalSourceIdentity canonicalIdentity = TargetedVerificationSourceIdentity.Resolve(
                canonical, occurrenceKind, occurrenceId);
            if (canonicalIdentity.LogicalId.Value != sourceItem.LogicalId
                || canonicalIdentity.IdentityEnvelope.AnalyzerFamily != sourceItem.AnalyzerId
                || canonicalIdentity.IdentityEnvelope.AnalyzerVersion.ToString() != sourceItem.AnalyzerVersion)
            {
                throw new AnalysisIdentityDriftException(
                    "The targeted source occurrence index differs from its canonical identity envelope.");
            }

            SetupObjectRecord profile = ActiveSetupObject(ProfileObjectKind, CurrentProfileObjectId);
            SetupObjectRecord configuration = ActiveSetupObject(ConfigurationObjectKind, request.SavedConfigurationId);
            if (profile.Revision != checked((long)request.ExpectedConfirmedProfileRevision)
                || configuration.Revision != checked((long)request.ExpectedSavedConfigurationRevision))
            {
                throw new InvalidOperationException("The confirmed profile or saved configuration revision changed.");
            }

            ProfileStateDocument profileDocument = Deserialize<ProfileStateDocument>(profile.PayloadJson);
            if (!profileDocument.ExplicitlyConfirmed
                || profileDocument.ConfirmedCandidateId != request.ConfirmedProfileId)
            {
                throw new InvalidOperationException("The requested profile is not the exact currently confirmed profile.");
            }

            RunOperationRecord sourceOperation = runtime.Store.GetRunOperation(sourceRunId)
                ?? throw new InvalidOperationException("The targeted source run has no managed operation.");
            ManagedAnalysisOrchestrationRequest sourceManaged = JsonSerializer.Deserialize<ManagedAnalysisOrchestrationRequest>(
                sourceOperation.RequestJson, ContractJsonSerializer.Options)
                ?? throw new InvalidDataException("The targeted source managed request is malformed.");
            if (request.ExpectedAnalysisContextRevision != 1
                || request.AnalysisContextId != sourceRun.Binding.AnalysisContextId
                || request.AnalysisContextFingerprintSha256 != sourceManaged.AnalysisContext.CanonicalFingerprint.Value)
            {
                throw new InvalidOperationException("The selected analysis context revision or fingerprint changed.");
            }

            byte[] sourceSnapshotBytes = runtime.Store.ReadPublishedSnapshotPayload(
                sourceRun.Binding.InstallationSnapshotId, 64 * 1024 * 1024);
            Mo2SnapshotCaptureResult sourceCapture = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(sourceSnapshotBytes)
                ?? throw new InvalidDataException("The targeted source snapshot is malformed.");
            ManagedMo2SnapshotCaptureAssignment captureAssignment = CaptureAssignment(sourceCapture);
            string captureJson = JsonSerializer.Serialize(captureAssignment);
            string captureSha = HashTargeted(captureJson);
            TargetedPreparationPersistenceRecord admitted = runtime.Store.CreateTargetedPreparation(new(
                request.IdempotencyKey, preparationId, request.UserGestureId, requestJson, requestSha,
                sourceRunId, occurrenceKind, occurrenceId, request.ConfirmedProfileId,
                checked((long)request.ExpectedConfirmedProfileRevision), request.SavedConfigurationId,
                checked((long)request.ExpectedSavedConfigurationRevision), request.AnalysisContextId,
                checked((long)request.ExpectedAnalysisContextRevision), request.AnalysisContextFingerprintSha256,
                request.InitiationKind.ToString(), dispatchDeadline,
                "targeted-capture-" + HashTargeted(preparationId)[..32], captureJson, captureSha),
                runtime.Authority.FencingEpoch, DateTimeOffset.UtcNow);
            targetedVerificationExecutor.Schedule(admitted.PreparationId);
            return Task.FromResult(new BeginTargetedVerificationPreparationResponse
            {
                Disposition = replay is null ? CommandDisposition.Accepted : CommandDisposition.AlreadyAccepted,
                Preparation = ToTargetedPreparation(runtime.Store.GetTargetedPreparationReadbackSnapshot(
                    admitted.PreparationId, 100, null), 100, null, 100, null, null,
                    100, null, 100, null, 100, null),
            });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or InvalidDataException or KeyNotFoundException or OverflowException
            or AnalysisIdentityDriftException)
        {
            return Task.FromResult(new BeginTargetedVerificationPreparationResponse
            {
                Disposition = CommandDisposition.Rejected,
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.Conflict,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetTargetedVerificationPreparationResponse> GetTargetedVerificationPreparation(
        GetTargetedVerificationPreparationRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new GetTargetedVerificationPreparationResponse { Failure = contractFailure });
        }

        try
        {
            TargetedLifecycleCursorKey? lifecycleCursor = DecodeLifecycleCursor(
                request.PreparationId, request.AfterLifecycleCursor);
            TargetedPreparationReadbackSnapshotRecord value =
                runtime.Store.GetTargetedPreparationReadbackSnapshot(
                    request.PreparationId, checked((int)request.MaximumLifecycleEvents),
                    lifecycleCursor);
            return Task.FromResult(new GetTargetedVerificationPreparationResponse
            {
                Preparation = ToTargetedPreparation(value, checked((int)request.MaximumMembers), request.AfterMemberId,
                    checked((int)request.MaximumArtifactDecisions), request.AfterArtifactKind, request.AfterArtifactId,
                    checked((int)request.MaximumDependencies), request.AfterDependencyEdgeId,
                    checked((int)request.MaximumTargetAnalyzers), request.AfterTargetAnalyzerId,
                    checked((int)request.MaximumTerminalGaps), request.AfterTerminalGap),
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException
            or KeyNotFoundException or OverflowException or AnalysisIdentityDriftException)
        {
            return Task.FromResult(new GetTargetedVerificationPreparationResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.Conflict,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<CancelTargetedVerificationPreparationResponse> CancelTargetedVerificationPreparation(
        CancelTargetedVerificationPreparationRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new CancelTargetedVerificationPreparationResponse { Failure = contractFailure });
        }

        try
        {
            TargetedCancellationPersistenceReceipt receipt = runtime.Store.CancelTargetedPreparation(
                request.IdempotencyKey, request.PreparationId, checked((long)request.ExpectedRevision),
                request.UserGestureId, runtime.Authority.FencingEpoch, DateTimeOffset.UtcNow);
            return Task.FromResult(new CancelTargetedVerificationPreparationResponse
            {
                Disposition = receipt.Replayed ? CommandDisposition.AlreadyAccepted : CommandDisposition.Accepted,
                Preparation = ToTargetedPreparation(runtime.Store.GetTargetedPreparationReadbackSnapshot(
                    receipt.Preparation.PreparationId, 100, null), 100, null, 100, null, null,
                    100, null, 100, null, 100, null),
            });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or KeyNotFoundException or OverflowException)
        {
            return Task.FromResult(new CancelTargetedVerificationPreparationResponse
            {
                Disposition = CommandDisposition.Rejected,
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.Conflict,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<StartTargetedVerificationResponse> StartTargetedVerification(
        StartTargetedVerificationRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new StartTargetedVerificationResponse { Failure = contractFailure });
        }

        try
        {
            DateTimeOffset dispatchDeadline = FromProto(request.DispatchDeadline).ToUniversalTime();
            string startRequestSha = HashTargeted(JsonSerializer.Serialize(new
            {
                schema = "infinium/targeted-verification-start-request/v1",
                request.IdempotencyKey,
                request.PreparationId,
                request.ExpectedPreparationRevision,
                request.ExpectedPreparationFingerprintSha256,
                request.RequestedRunId,
                request.UserGestureId,
                initiationKind = request.InitiationKind.ToString(),
                dispatchDeadline,
            }));
            TargetedVerificationReadbackRecord? replay = runtime.Store.FindPreparedTargetedVerificationByCommand(
                request.IdempotencyKey);
            if (replay is not null)
            {
                runtime.Store.ValidateTargetedStartReplay(request.IdempotencyKey, startRequestSha);
                return Task.FromResult(ToStartResponse(replay, CommandDisposition.AlreadyAccepted));
            }
            if (!runtime.TryAdmitNewDurableCommand(DateTimeOffset.UtcNow))
            {
                return Task.FromResult(new StartTargetedVerificationResponse
                {
                    Disposition = CommandDisposition.Rejected,
                    Failure = Failure(FailureCode.LimitExceeded, "The new durable-command rate bound is full."),
                });
            }

            TargetedPreparationPersistenceRecord preparation = runtime.Store.GetTargetedPreparation(request.PreparationId);
            if (preparation.Revision != checked((long)request.ExpectedPreparationRevision)
                || preparation.PreparationFingerprint != request.ExpectedPreparationFingerprintSha256
                || preparation.State is not (Infinium.Domain.Contracts.TargetedVerificationPreparationState.Ready
                    or Infinium.Domain.Contracts.TargetedVerificationPreparationState.ReadyWithGaps)
                || !preparation.Startable)
            {
                throw new InvalidOperationException("The exact targeted preparation is stale or not startable.");
            }

            ValidateCurrentTargetedSelections(preparation);
            TargetedVerificationPlanContract plan = runtime.Store.ReadTargetedPlan(preparation.PreparationId);
            string runId = string.IsNullOrWhiteSpace(request.RequestedRunId)
                ? plan.PreparedSuccessorRunId.Value : request.RequestedRunId;
            ResolvedTargetedOperation resolved = TargetedVerificationOperationResolver.Bind(
                runtime.Store, preparation, plan, request.IdempotencyKey, runId, request.UserGestureId,
                dispatchDeadline, DateTimeOffset.UtcNow);
            TargetedStartAdmissionPersistence targetedAdmission = new(resolved.AdmissionId,
                resolved.TargetedVerificationId, preparation.PreparationId, request.UserGestureId,
                startRequestSha, resolved.SubmissionFingerprint, preparation.PreparationFingerprint,
                preparation.SourceRunId, preparation.SourceOccurrenceId, plan.TargetSnapshotId.Value,
                plan.EvidenceAcquisitionId.Value, resolved.RequestSha256)
            {
                OperationInputs = resolved.OperationInputs,
            };
            RunRecord run = runtime.Store.CreateRun(request.IdempotencyKey, runId, resolved.Binding,
                runtime.Authority.FencingEpoch, DateTimeOffset.UtcNow, request.InitiationKind.ToString(),
                dispatchDeadline, ManagedRunExecutor.ManagedAnalysisOperation,
                resolved.RequestJson, request.UserGestureId, preparation.PreparationId,
                resolved.SubmissionFingerprint, targetedAdmission);
            executor.Schedule(run.RunId);
            TargetedVerificationReadbackRecord admitted = runtime.Store.GetPreparedTargetedVerification(
                resolved.TargetedVerificationId);
            return Task.FromResult(ToStartResponse(admitted, CommandDisposition.Accepted));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or InvalidDataException or KeyNotFoundException or OverflowException
            or AnalysisIdentityDriftException)
        {
            return Task.FromResult(new StartTargetedVerificationResponse
            {
                Disposition = CommandDisposition.Rejected,
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.Conflict,
                    Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetTargetedVerificationResponse> GetTargetedVerification(
        GetTargetedVerificationRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (PhaseCContractFailure(request) is { } contractFailure)
        {
            return Task.FromResult(new GetTargetedVerificationResponse { Failure = contractFailure });
        }

        try
        {
            string identity = request.IdentityCase == GetTargetedVerificationRequest.IdentityOneofCase.TargetedVerificationId
                ? request.TargetedVerificationId : request.SuccessorRunId.Value;
            TargetedVerificationReadbackRecord value = runtime.Store.GetPreparedTargetedVerification(identity);
            return Task.FromResult(new GetTargetedVerificationResponse { Verification = ToTargetedVerification(value) });
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException
            or KeyNotFoundException)
        {
            return Task.FromResult(new GetTargetedVerificationResponse
            {
                Failure = Failure(exception is KeyNotFoundException ? FailureCode.NotFound : FailureCode.Conflict,
                    Bounded(exception.Message)),
            });
        }
    }

    private static TargetedVerificationPreparation ToTargetedPreparation(
        TargetedPreparationReadbackSnapshotRecord readback, int maximumMembers, string? afterMemberId,
        int maximumArtifactDecisions,
        string? afterArtifactKind, string? afterArtifactId, int maximumDependencies,
        string? afterDependencyEdgeId, int maximumTargetAnalyzers, string? afterTargetAnalyzerId,
        int maximumTerminalGaps, string? afterTerminalGap)
    {
        TargetedPreparationPersistenceRecord value = readback.Preparation;
        TargetedVerificationPreparation result = new()
        {
            PreparationId = value.PreparationId,
            Revision = checked((ulong)value.Revision),
            PreparationFingerprintSha256 = value.PreparationFingerprint,
            State = ToProto(value.State),
            InertTerminalReason = value.TerminalReason,
            SourceRunId = new RunId { Value = value.SourceRunId },
            AnalysisContextId = value.AnalysisContextId,
            AnalysisContextRevision = checked((ulong)value.AnalysisContextRevision),
            AnalysisContextFingerprintSha256 = value.AnalysisContextFingerprint,
            SavedConfigurationId = value.SavedConfigurationId,
            SavedConfigurationRevision = checked((ulong)value.SavedConfigurationRevision),
            CaptureOperationId = value.CaptureOperationId,
            TargetSnapshotId = value.TargetSnapshotId ?? string.Empty,
            EvidenceAcquisitionId = value.EvidenceAcquisitionId ?? string.Empty,
            PlanId = value.PlanId ?? string.Empty,
            PlanFingerprintSha256 = value.PlanFingerprint ?? string.Empty,
            Startable = value.Startable,
            Limited = value.Limited,
            ReadinessBoundary = "scope-limited-no-readiness",
            CreatedAt = ProtoMapping.ToProto(value.CreatedAt),
            UpdatedAt = ProtoMapping.ToProto(value.UpdatedAt),
            ConfirmedProfileRevision = checked((ulong)value.ConfirmedProfileRevision),
        };
        if (value.SourceOccurrenceKind == "finding")
        {
            result.SourceFindingOccurrenceId = value.SourceOccurrenceId;
        }
        else
        {
            result.SourceCaseOccurrenceId = value.SourceOccurrenceId;
        }

        ResultItemPersistenceRecord sourceItem = readback.Source.Occurrence;
        byte[] sourcePayloadBytes = readback.Source.CanonicalPayloadBytes;
        TargetedCanonicalSourceIdentity canonicalIdentity = TargetedVerificationSourceIdentity.Resolve(
            FindingCaseJsonCodec.Deserialize(sourcePayloadBytes), value.SourceOccurrenceKind, value.SourceOccurrenceId);
        if (canonicalIdentity.LogicalId.Value != sourceItem.LogicalId
            || canonicalIdentity.IdentityEnvelope.AnalyzerFamily != sourceItem.AnalyzerId
            || canonicalIdentity.IdentityEnvelope.AnalyzerVersion.ToString() != sourceItem.AnalyzerVersion)
        {
            throw new AnalysisIdentityDriftException(
                "The targeted source occurrence index differs from its canonical identity envelope.");
        }
        result.SourceLogicalId = canonicalIdentity.LogicalId.Value;
        result.SourcePayloadId = sourceItem.SourcePayloadId;
        result.SourcePayloadFingerprintSha256 = sourceItem.SourcePayloadSha256;
        result.SourceCanonicalSignatureSha256 = canonicalIdentity.IdentityEnvelope.CanonicalSignature.Value;
        result.SourceAnalyzerFamily = canonicalIdentity.IdentityEnvelope.AnalyzerFamily;
        result.SourceAnalyzerVersion = ToProto(canonicalIdentity.IdentityEnvelope.AnalyzerVersion);
        result.SourceSemanticContractVersion = ToProto(canonicalIdentity.IdentityEnvelope.SemanticContractVersion);
        result.SourceIdentityContractVersion = ToProto(canonicalIdentity.IdentityEnvelope.IdentityContractVersion);
        result.SourceSnapshotId = readback.Source.Run.Binding.InstallationSnapshotId;
        if (readback.Acquisition is { } acquisition)
        {
            result.EvidenceAcquisitionState = acquisition.State;
            result.EvidenceAcquisitionGeneration = checked((ulong)acquisition.Generation);
            if (readback.AcquisitionPublication is { } publication)
            {
                result.SemanticOutputId = publication.SemanticOutputId;
                result.SemanticOutputFingerprintSha256 = publication.PayloadSha256;
            }
        }
        if (readback.Plan is { } plan)
        {
            result.TargetSnapshotFingerprintSha256 = plan.TargetSnapshotFingerprint.Value;
            result.ScopeId = plan.Scope.ScopeId.Value;
            result.ScopeFingerprintSha256 = plan.Scope.CanonicalFingerprint.Value;
            result.CorrelationCoverageId = plan.CorrelationCoverage.CoverageId.Value;
            result.CorrelationCoverageFingerprintSha256 = plan.CorrelationCoverage.CanonicalFingerprint.Value;
            result.PopulationDenominator = checked((ulong)plan.CorrelationCoverage.PopulationDenominator);
            TargetedScopeMemberContract[] page = plan.Scope.Members
                .Where(item => string.IsNullOrWhiteSpace(afterMemberId)
                    || StringComparer.Ordinal.Compare(item.MemberId.Value, afterMemberId) > 0)
                .Take(maximumMembers + 1).ToArray();
            bool hasMore = page.Length > maximumMembers;
            foreach (TargetedScopeMemberContract member in page.Take(maximumMembers))
            {
                result.ScopeMembers.Add(ToProto(member,
                    plan.Scope.DirectRoots.Any(root => root.MemberId == member.MemberId)));
            }

            HashSet<OpaqueId> visible = result.ScopeMembers.Select(item => new OpaqueId(item.MemberId)).ToHashSet();
            foreach (TargetedCorrelationCoverageRowContract row in plan.CorrelationCoverage.Rows
                         .Where(item => visible.Contains(item.ScopeMemberId)))
            {
                result.CorrelationRows.Add(ToProto(row));
            }
            result.InertGaps.Add(plan.Gaps.Select(Bounded));
            result.InertNonStartableReasons.Add(plan.NonStartableReasons.Select(Bounded));
            TargetedScopeDependencyContract[] dependencyPage = plan.Scope.Dependencies
                .OrderBy(item => item.EdgeId.Value, StringComparer.Ordinal)
                .Where(item => string.IsNullOrWhiteSpace(afterDependencyEdgeId)
                    || StringComparer.Ordinal.Compare(item.EdgeId.Value, afterDependencyEdgeId) > 0)
                .Take(maximumDependencies + 1).ToArray();
            bool moreDependencies = dependencyPage.Length > maximumDependencies;
            result.ScopeDependencies.Add(dependencyPage.Take(maximumDependencies).Select(ToProto));
            if (moreDependencies)
            {
                result.NextDependencyEdgeId = dependencyPage[maximumDependencies - 1].EdgeId.Value;
            }

            TargetedCorrelationCoverageRowContract policy = plan.CorrelationCoverage.Rows[0];
            result.CorrelationPolicyId = policy.CorrelationPolicyId.Value;
            result.CorrelationPolicyVersion = ToProto(policy.CorrelationPolicyVersion);
            result.CorrelationPolicyFingerprintSha256 = policy.CorrelationPolicyFingerprint.Value;
            TargetedReuseDecisionContract[] artifactPage = plan.ReuseDecisions
                .OrderBy(item => item.ArtifactKind, StringComparer.Ordinal)
                .ThenBy(item => item.ArtifactId.Value, StringComparer.Ordinal)
                .Where(item => string.IsNullOrWhiteSpace(afterArtifactKind)
                    || StringComparer.Ordinal.Compare(item.ArtifactKind, afterArtifactKind) > 0
                    || item.ArtifactKind == afterArtifactKind
                    && StringComparer.Ordinal.Compare(item.ArtifactId.Value, afterArtifactId) > 0)
                .Take(maximumArtifactDecisions + 1).ToArray();
            bool moreArtifacts = artifactPage.Length > maximumArtifactDecisions;
            result.ArtifactDecisions.Add(artifactPage.Take(maximumArtifactDecisions).Select(ToProto));
            if (moreArtifacts)
            {
                TargetedReuseDecisionContract last = artifactPage[maximumArtifactDecisions - 1];
                result.NextArtifactKind = last.ArtifactKind;
                result.NextArtifactId = last.ArtifactId.Value;
            }
            TargetedReuseDecisionContract? effectiveConfiguration = plan.ReuseDecisions.SingleOrDefault(item =>
                item.ArtifactKind == "effective-configuration");
            result.EffectiveConfigurationId = plan.Source.EffectiveConfigurationId.Value;
            result.EffectiveConfigurationFingerprintSha256 =
                effectiveConfiguration?.ProofFingerprint.Value ?? string.Empty;
            result.ResolvedInputManifestId = plan.PreparedResolvedInputManifest.ArtifactId.Value;
            result.ResolvedInputManifestFingerprintSha256 =
                plan.PreparedResolvedInputManifest.Fingerprint.Value;

            RunOperationRecord sourceOperation = readback.Source.Operation;
            ManagedAnalysisOrchestrationRequest sourceRequest = JsonSerializer.Deserialize<ManagedAnalysisOrchestrationRequest>(
                sourceOperation.RequestJson, ContractJsonSerializer.Options)
                ?? throw new InvalidDataException("The targeted source managed operation is malformed.");
            ArtifactReferenceContract[] analyzerPage = sourceRequest.ExecutionInput.AnalyzerDeclarations
                .OrderBy(item => item.ArtifactId.Value, StringComparer.Ordinal)
                .Where(item => string.IsNullOrWhiteSpace(afterTargetAnalyzerId)
                    || StringComparer.Ordinal.Compare(item.ArtifactId.Value, afterTargetAnalyzerId) > 0)
                .Take(maximumTargetAnalyzers + 1).ToArray();
            bool moreAnalyzers = analyzerPage.Length > maximumTargetAnalyzers;
            foreach (ArtifactReferenceContract analyzer in analyzerPage.Take(maximumTargetAnalyzers))
            {
                TargetedReuseDecisionContract? analyzerProof = plan.ReuseDecisions.SingleOrDefault(item =>
                    item.ArtifactKind == "analyzer-declaration" && item.ArtifactId == analyzer.ArtifactId);
                bool compatible = analyzerProof?.ProofFingerprint == analyzer.Fingerprint
                    && analyzer.ArtifactId.Value == plan.Source.AnalyzerFamily
                    && analyzer.ArtifactVersion == plan.Source.AnalyzerVersion;
                result.TargetAnalyzers.Add(new TargetedAnalyzerCompatibility
                {
                    AnalyzerDeclarationId = analyzer.ArtifactId.Value,
                    AnalyzerFamily = analyzer.ArtifactId.Value,
                    AnalyzerVersion = ToProto(analyzer.ArtifactVersion),
                    SemanticContractVersion = ToProto(plan.Source.SemanticContractVersion),
                    IdentityContractVersion = ToProto(plan.Source.IdentityContractVersion),
                    CompatibilityProofId = analyzerProof?.ProofId.Value ?? string.Empty,
                    CompatibilityProofFingerprintSha256 = analyzerProof?.ProofFingerprint.Value ?? string.Empty,
                    Compatible = compatible,
                    InertReason = compatible ? analyzerProof!.Reason :
                        "The retained target analyzer declaration lacks an exact identity, version, and byte-equivalence proof.",
                });
            }
            if (moreAnalyzers)
            {
                result.NextTargetAnalyzerId = analyzerPage[maximumTargetAnalyzers - 1].ArtifactId.Value;
            }
            result.ExpectedWork = ToWork(plan);

            if (hasMore)
            {
                result.NextMemberCursor = page[maximumMembers - 1].MemberId.Value;
            }
        }
        TargetedPreparationDiagnosticsPersistenceRecord diagnostics = readback.Diagnostics;
        result.CaptureAttemptId = diagnostics.CaptureAttemptId ?? string.Empty;
        result.StructuralComparison = diagnostics.StructuralComparison ?? string.Empty;
        result.EvidenceAcquisitionAttemptCount = checked((ulong)diagnostics.EvidenceAttemptCount);
        result.EvidenceAcquisitionAttemptId = diagnostics.EvidenceAttemptId ?? string.Empty;
        result.EvidenceProgressCompleted = checked((ulong)diagnostics.EvidenceProgressCompleted);
        result.EvidenceProgressDenominator = checked((ulong)diagnostics.EvidenceProgressDenominator);
        result.EvidenceCheckpointId = diagnostics.EvidenceCheckpointId ?? string.Empty;
        if (value.PlanId is null)
        {
            result.EffectiveConfigurationId = value.SavedConfigurationId;
        }

        TargetedPreparationReadbackEvidenceRecord evidence = readback.Evidence;
        if (evidence.Snapshot is { } snapshot)
        {
            Mo2SnapshotCaptureResult targetCapture = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(
                snapshot.SnapshotPayloadBytes)
                ?? throw new InvalidDataException("The retained targeted snapshot payload is malformed.");
            Mo2InstallationSnapshot targetInstallation = targetCapture.Snapshot
                ?? throw new InvalidDataException("The retained targeted snapshot has no completed snapshot.");
            Mo2SnapshotCaptureResult retainedSourceCapture = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(
                readback.Source.SnapshotPayloadBytes)
                ?? throw new InvalidDataException("The retained source snapshot payload is malformed.");
            if (targetCapture.State is not (SnapshotCaptureState.Completed or SnapshotCaptureState.CompletedWithGaps)
                || targetInstallation.Contract.SnapshotId.Value != value.TargetSnapshotId
                || targetInstallation.Contract.StructuralManifestFingerprint.Value != snapshot.TargetStructuralFingerprint
                || retainedSourceCapture.Snapshot?.Contract.StructuralManifestFingerprint.Value
                    != snapshot.SourceStructuralFingerprint
                || snapshot.ConfirmedProfileRevision != value.ConfirmedProfileRevision)
            {
                throw new AnalysisIdentityDriftException(
                    "The targeted snapshot identity, structure, or confirmed-profile binding drifted.");
            }
            result.TargetSnapshotCapturedAt = ProtoMapping.ToProto(targetInstallation.Contract.CapturedAt.Value);
            result.ConfirmedProfileRevision = checked((ulong)snapshot.ConfirmedProfileRevision);
            result.TargetSnapshotFingerprintSha256 = snapshot.SnapshotFingerprint;
            result.StructuralComparison = snapshot.StructuralComparison;
        }
        if (evidence.Acquisition is { } readbackAcquisition)
        {
            List<string> terminalGaps = [];
            result.AcquisitionEvidence = new TargetedAcquisitionEvidence
            {
                AcquisitionRequestFingerprintSha256 = readbackAcquisition.RequestFingerprint,
                SealedInputFingerprintSha256 = readbackAcquisition.SealedInputFingerprint,
                ProducerFamily = readbackAcquisition.ProducerFamily,
                ProducerVersion = new SemanticVersion { Value = readbackAcquisition.ProducerVersion },
                SupportManifestId = readbackAcquisition.SupportManifestId,
                EnumerationPolicyId = readbackAcquisition.EnumerationPolicyId,
                EnumerationPolicyVersion = new SemanticVersion { Value = readbackAcquisition.EnumerationPolicyVersion },
                CoordinatorFencingEpoch = checked((ulong)readbackAcquisition.CoordinatorFencingEpoch),
                AttemptFencingToken = checked((ulong)readbackAcquisition.AttemptFencingToken),
                PublicationId = readbackAcquisition.PublicationId ?? string.Empty,
                PublicationPayloadId = readbackAcquisition.PublicationPayloadId ?? string.Empty,
                StagedManifestFingerprintSha256 = readbackAcquisition.StagedManifestFingerprint ?? string.Empty,
                ProvenanceFingerprintSha256 = readbackAcquisition.ProvenanceFingerprint ?? string.Empty,
                InertTerminalReason = readbackAcquisition.TerminalReason,
            };
            if (readbackAcquisition.PublishedAt is { } publishedAt)
            {
                result.AcquisitionEvidence.PublishedAt = ProtoMapping.ToProto(publishedAt);
            }
            if (!string.IsNullOrWhiteSpace(readbackAcquisition.TerminalReason))
            {
                terminalGaps.Add(Bounded("lifecycle:" + readbackAcquisition.TerminalReason));
            }
            if (evidence.Snapshot is { } retainedSnapshot)
            {
                Mo2SnapshotCaptureResult targetCapture = JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(
                    retainedSnapshot.SnapshotPayloadBytes)
                    ?? throw new InvalidDataException("The retained targeted snapshot payload is malformed.");
                IEnumerable<SnapshotGap> captureGaps = targetCapture.Gaps
                    .Concat(targetCapture.Snapshot?.Gaps ?? []);
                terminalGaps.AddRange(captureGaps
                    .Select(gap => Bounded($"capture:{gap.Code}:{gap.Population}:{gap.Reason}")));

                if (readbackAcquisition.SemanticPayloadBytes is { } semanticBytes
                    && readback.Acquisition is { } retainedAcquisition)
                {
                    ManagedBethesdaSemanticIntent intent = JsonSerializer.Deserialize<ManagedBethesdaSemanticIntent>(
                        retainedAcquisition.RequestJson)
                        ?? throw new InvalidDataException("The retained semantic acquisition request is malformed.");
                    ManagedBethesdaSemanticAssignment assignment = ManagedRunExecutor.SealBethesdaAssignment(
                        new(targetCapture, intent.RequestedUnsupportedCapabilities));
                    if (HashTargeted(JsonSerializer.Serialize(assignment)) != retainedAcquisition.SealedInputFingerprint)
                    {
                        throw new AnalysisIdentityDriftException(
                            "The retained semantic acquisition seal no longer matches the target snapshot.");
                    }
                    BethesdaSemanticExtractionResult semantic =
                        BethesdaSemanticPublicationValidator.DeserializeAndValidate(
                            semanticBytes, assignment, 64 * 1024 * 1024);
                    if (semantic.Snapshot!.ProducerId != readbackAcquisition.ProducerFamily
                        || semantic.Snapshot.ProducerVersion.ToString() != readbackAcquisition.ProducerVersion)
                    {
                        throw new AnalysisIdentityDriftException(
                            "The semantic producer identity differs from the retained acquisition authority.");
                    }
                    terminalGaps.AddRange(semantic.Gaps.Select(gap =>
                        Bounded($"semantic:{gap.Category}:{gap.Population}:{gap.GapId}:{gap.Reason}")));
                    terminalGaps.AddRange(semantic.Failures.Select(failure =>
                        Bounded($"semantic-failure:{failure.Code}:{failure.Input}:{failure.Message}")));
                }
            }
            string[] orderedGaps = terminalGaps.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray();
            long acquisitionSequence = readback.Acquisition?.DurableSequence ?? 0;
            int terminalGapOffset = DecodeTerminalGapCursor(
                value, acquisitionSequence, orderedGaps, afterTerminalGap);
            string[] gapPage = orderedGaps
                .Skip(terminalGapOffset)
                .Take(maximumTerminalGaps + 1).ToArray();
            result.AcquisitionEvidence.TerminalGapCount = checked((ulong)orderedGaps.LongLength);
            result.AcquisitionEvidence.InertTerminalGaps.Add(gapPage.Take(maximumTerminalGaps));
            if (gapPage.Length > maximumTerminalGaps)
            {
                result.AcquisitionEvidence.NextTerminalGap = EncodeTerminalGapCursor(
                    value, acquisitionSequence, orderedGaps, checked(terminalGapOffset + maximumTerminalGaps));
            }
        }
        else if (!string.IsNullOrWhiteSpace(afterTerminalGap))
        {
            throw new InvalidOperationException(
                "The targeted terminal-gap cursor is stale because this readback has no acquisition evidence.");
        }
        result.LifecycleEvents.Add(evidence.LifecycleEvents.Select(ToProto));
        if (evidence.NextLifecycleCursorAnchor is { } lifecycleAnchor)
        {
            result.NextLifecycleCursor = EncodeLifecycleCursor(value.PreparationId, lifecycleAnchor);
        }
        return result;
    }

    private void ValidateCurrentTargetedSelections(TargetedPreparationPersistenceRecord preparation)
    {
        SetupObjectRecord profile = ActiveSetupObject(ProfileObjectKind, CurrentProfileObjectId);
        SetupObjectRecord configuration = ActiveSetupObject(ConfigurationObjectKind, preparation.SavedConfigurationId);
        if (profile.Revision != preparation.ConfirmedProfileRevision
            || configuration.Revision != preparation.SavedConfigurationRevision)
        {
            throw new InvalidOperationException("The profile or configuration changed after targeted preparation.");
        }

        ProfileStateDocument profileDocument = Deserialize<ProfileStateDocument>(profile.PayloadJson);
        if (!profileDocument.ExplicitlyConfirmed || profileDocument.ConfirmedCandidateId != preparation.ConfirmedProfileId)
        {
            throw new InvalidOperationException("The confirmed profile changed after targeted preparation.");
        }

        RunOperationRecord sourceOperation = runtime.Store.GetRunOperation(preparation.SourceRunId)
            ?? throw new InvalidOperationException("The source managed operation is no longer retained.");
        ManagedAnalysisOrchestrationRequest source = JsonSerializer.Deserialize<ManagedAnalysisOrchestrationRequest>(
            sourceOperation.RequestJson, ContractJsonSerializer.Options)
            ?? throw new InvalidDataException("The source managed operation is malformed.");
        if (source.AnalysisContext.ContextId.Value != preparation.AnalysisContextId
            || source.AnalysisContext.CanonicalFingerprint.Value != preparation.AnalysisContextFingerprint)
        {
            throw new AnalysisIdentityDriftException("The selected analysis context changed after targeted preparation.");
        }
    }

    private static ManagedMo2SnapshotCaptureAssignment CaptureAssignment(Mo2SnapshotCaptureResult sourceCapture)
    {
        Mo2InstallationSnapshot snapshot = sourceCapture.Snapshot
            ?? throw new InvalidDataException("The retained source snapshot has no completed payload.");
        Dictionary<string, string> roots = snapshot.Dependencies.RootObservations
            .ToDictionary(item => item.Role, item => item.SourcePath, StringComparer.Ordinal);
        string profilesRoot = Path.GetDirectoryName(snapshot.ProfileRoot)
            ?? throw new InvalidDataException("The retained source profile has no profiles root.");
        return new(
            Path.Combine(snapshot.InstanceRoot, snapshot.Dependencies.Mo2ExecutableIdentity.FileName),
            snapshot.InstanceRoot, Path.Combine(snapshot.InstanceRoot, "ModOrganizer.ini"), profilesRoot,
            roots["mods"], roots["overwrite"], roots["game-data"],
            Path.Combine(Path.GetDirectoryName(roots["game-data"])!, snapshot.Dependencies.RuntimeExecutableIdentity.FileName),
            snapshot.Dependencies.ExplicitSelectedProfileName,
            snapshot.Dependencies.DeclaredRuntimeTarget.Platform,
            snapshot.Dependencies.DeclaredRuntimeTarget.DistributionChannel,
            snapshot.Dependencies.DeclaredRuntimeTarget.ApplicationId,
            snapshot.Dependencies.MappingDependencies.Select(item => new ManagedQualifiedMappingAssignment(
                item.MappingId, item.SourceRoot, item.VirtualPrefix, item.MapperFingerprint.Value)).ToArray(),
            snapshot.Dependencies.EnabledMapperSha256s);
    }

    private static StartTargetedVerificationResponse ToStartResponse(
        TargetedVerificationReadbackRecord value, CommandDisposition disposition) => new()
        {
            Disposition = disposition,
            DurableCommandId = new DurableCommandId { Value = value.CommandId },
            SuccessorRunId = new RunId { Value = value.SuccessorRunId },
            TargetedVerificationId = value.TargetedVerificationId,
        };

    private TargetedVerification ToTargetedVerification(TargetedVerificationReadbackRecord value)
    {
        TargetedVerificationPlanContract plan = runtime.Store.ReadTargetedPlan(value.PreparationId);
        RunRecord run = runtime.Store.GetRun(value.SuccessorRunId);
        TargetedVerification result = new()
        {
            TargetedVerificationId = value.TargetedVerificationId,
            PreparationId = value.PreparationId,
            AdmissionId = value.AdmissionId,
            SourceRunId = new RunId { Value = value.SourceRunId },
            SourceOccurrenceId = value.SourceOccurrenceId,
            SuccessorRunId = new RunId { Value = value.SuccessorRunId },
            TargetSnapshotId = value.TargetSnapshotId,
            EvidenceAcquisitionId = value.EvidenceAcquisitionId,
            SemanticOutputId = plan.SemanticOutputId.Value,
            ScopeId = plan.Scope.ScopeId.Value,
            ScopeFingerprintSha256 = plan.Scope.CanonicalFingerprint.Value,
            CorrelationCoverageId = plan.CorrelationCoverage.CoverageId.Value,
            CorrelationCoverageFingerprintSha256 = plan.CorrelationCoverage.CanonicalFingerprint.Value,
            ManagedOperationKind = value.ManagedOperationKind,
            ManagedOperationFingerprintSha256 = value.ManagedOperationFingerprint,
            SuccessorLifecycleState = run.State.ToString(),
            PopulationDenominator = checked((ulong)plan.CorrelationCoverage.PopulationDenominator),
            ReadinessBoundary = plan.ReadinessBoundary,
            Limited = plan.Limited,
            CreatedAt = ProtoMapping.ToProto(value.CreatedAt),
        };
        result.InertGaps.Add(plan.Gaps);
        result.ReconciliationRelationships.Add(
            runtime.Store.ReadTargetedReconciliationRelationships(value.TargetedVerificationId));
        return result;
    }

    private static TargetedScopeMember ToProto(TargetedScopeMemberContract value, bool directRoot)
    {
        TargetedScopeMember result = new()
        {
            MemberId = value.MemberId.Value,
            Kind = Enum.Parse<Infinium.Contracts.Protobuf.Application.V1.TargetedScopeMemberKind>(value.Kind.ToString()),
            StableIdentity = value.StableIdentity.Value,
            InertReason = value.Reason,
            Mandatory = value.Mandatory,
            DirectRoot = directRoot,
        };
        result.SourceProofIds.Add(value.SourceProofIds.Select(item => item.Value));
        return result;
    }

    private static TargetedScopeDependency ToProto(TargetedScopeDependencyContract value)
    {
        TargetedScopeDependency result = new()
        {
            EdgeId = value.EdgeId.Value,
            FromMemberId = value.FromMemberId.Value,
            ToMemberId = value.ToMemberId.Value,
            Relation = value.Relation,
        };
        result.ProofIds.Add(value.ProofIds.Select(item => item.Value));
        return result;
    }

    private static TargetedArtifactDecision ToProto(TargetedReuseDecisionContract value) => new()
    {
        ArtifactKind = value.ArtifactKind,
        ArtifactId = value.ArtifactId.Value,
        Disposition = value.Disposition,
        ValidityProofId = value.ProofId.Value,
        ValidityProofFingerprintSha256 = value.ProofFingerprint.Value,
        InertReason = value.Reason,
    };

    private static TargetedPreparationLifecycleEvent ToProto(TargetedLifecycleReadbackEvent value) => new()
    {
        Sequence = checked((ulong)value.Sequence),
        OwnerSequence = checked((ulong)value.OwnerSequence),
        Owner = value.Owner,
        EventKind = value.EventKind,
        Generation = checked((ulong)value.Generation),
        CoordinatorFencingEpoch = checked((ulong)value.CoordinatorFencingEpoch),
        OccurredAt = ProtoMapping.ToProto(value.OccurredAt),
        EvidenceFingerprintSha256 = value.EvidenceFingerprint,
        InertSummary = Bounded(value.Summary),
    };

    private static string EncodeLifecycleCursor(
        string preparationId,
        TargetedLifecycleReadbackEvent anchor)
    {
        string sequence = anchor.Sequence.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        string binding = HashTargeted(string.Join('\n',
            "infinium/targeted-lifecycle-cursor/v1", preparationId, sequence, anchor.EvidenceFingerprint));
        string cursor = string.Join('.', "tl1", sequence, anchor.EvidenceFingerprint, binding);
        if (Encoding.UTF8.GetByteCount(cursor) > 160)
        {
            throw new InvalidOperationException("The targeted lifecycle cursor exceeds its contract bound.");
        }
        return cursor;
    }

    private static TargetedLifecycleCursorKey? DecodeLifecycleCursor(
        string preparationId,
        string cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }
        string[] parts = cursor.Split('.', StringSplitOptions.None);
        if (parts.Length != 4 || parts[0] != "tl1"
            || !IsCanonicalLowerHex(parts[1], 16)
            || !ulong.TryParse(parts[1], System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out ulong parsedSequence)
            || parsedSequence is 0 or > long.MaxValue
            || !IsCanonicalLowerHex(parts[2], 64) || !IsCanonicalLowerHex(parts[3], 64))
        {
            throw new InvalidOperationException("The targeted lifecycle cursor is malformed.");
        }
        string expected = HashTargeted(string.Join('\n',
            "infinium/targeted-lifecycle-cursor/v1", preparationId, parts[1], parts[2]));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected), Convert.FromHexString(parts[3])))
        {
            throw new InvalidOperationException("The targeted lifecycle cursor is substituted.");
        }
        return new(checked((long)parsedSequence), parts[2]);
    }

    private static string EncodeTerminalGapCursor(
        TargetedPreparationPersistenceRecord preparation,
        long acquisitionSequence,
        string[] orderedGaps,
        int offset)
    {
        string revision = preparation.Revision.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        string acquisition = acquisitionSequence.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
        string encodedOffset = offset.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        string binding = TerminalGapCursorBinding(
            preparation, acquisitionSequence, orderedGaps, offset);
        string cursor = string.Join('.', "tg1", revision, acquisition, encodedOffset, binding);
        if (Encoding.UTF8.GetByteCount(cursor) > 160)
        {
            throw new InvalidOperationException("The targeted terminal-gap cursor exceeds its contract bound.");
        }
        return cursor;
    }

    private static int DecodeTerminalGapCursor(
        TargetedPreparationPersistenceRecord preparation,
        long acquisitionSequence,
        string[] orderedGaps,
        string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }
        string[] parts = cursor.Split('.', StringSplitOptions.None);
        if (parts.Length != 5 || parts[0] != "tg1"
            || !IsCanonicalLowerHex(parts[1], 16)
            || !ulong.TryParse(parts[1], System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out ulong revision)
            || revision > long.MaxValue
            || !IsCanonicalLowerHex(parts[2], 16)
            || !ulong.TryParse(parts[2], System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out ulong acquisition)
            || acquisition > long.MaxValue
            || !IsCanonicalLowerHex(parts[3], 8)
            || !uint.TryParse(parts[3], System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out uint offset)
            || offset > int.MaxValue || !IsCanonicalLowerHex(parts[4], 64))
        {
            throw new InvalidOperationException("The targeted terminal-gap cursor is malformed.");
        }
        if (checked((long)revision) != preparation.Revision
            || checked((long)acquisition) != acquisitionSequence
            || offset > orderedGaps.Length)
        {
            throw new InvalidOperationException("The targeted terminal-gap cursor is stale.");
        }
        string expected = TerminalGapCursorBinding(
            preparation, acquisitionSequence, orderedGaps, checked((int)offset));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected), Convert.FromHexString(parts[4])))
        {
            throw new InvalidOperationException("The targeted terminal-gap cursor is substituted or stale.");
        }
        return checked((int)offset);
    }

    private static string TerminalGapCursorBinding(
        TargetedPreparationPersistenceRecord preparation,
        long acquisitionSequence,
        string[] orderedGaps,
        int offset) => HashTargeted(string.Join('\n',
            "infinium/targeted-terminal-gap-cursor/v1",
            preparation.PreparationId,
            preparation.Revision,
            preparation.PreparationFingerprint,
            acquisitionSequence,
            offset,
            HashTargeted(JsonSerializer.Serialize(orderedGaps))));

    private static bool IsCanonicalLowerHex(string value, int expectedLength) =>
        value.Length == expectedLength
        && value.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');

    private static TargetedPreparationWork ToWork(TargetedVerificationPlanContract plan)
    {
        ulong Count(Infinium.Domain.Contracts.TargetedCorrelationStatus status) =>
            checked((ulong)plan.CorrelationCoverage.Rows.Count(row => row.Status == status));
        return new()
        {
            DirectRootCount = checked((ulong)plan.Scope.DirectRoots.Count),
            ExpandedMemberCount = checked((ulong)plan.Scope.Members.Count),
            DependencyEdgeCount = checked((ulong)plan.Scope.Dependencies.Count),
            MaximumMembers = checked((ulong)plan.Scope.MaximumMembers),
            MaximumEdges = checked((ulong)plan.Scope.MaximumEdges),
            MatchedExecutable = Count(Infinium.Domain.Contracts.TargetedCorrelationStatus.MatchedExecutable),
            ChangedCorrelated = Count(Infinium.Domain.Contracts.TargetedCorrelationStatus.ChangedCorrelated),
            ProvenAbsent = Count(Infinium.Domain.Contracts.TargetedCorrelationStatus.ProvenAbsent),
            ProvenNotApplicable = Count(Infinium.Domain.Contracts.TargetedCorrelationStatus.ProvenNotApplicable),
            Ambiguous = Count(Infinium.Domain.Contracts.TargetedCorrelationStatus.Ambiguous),
            Unsupported = Count(Infinium.Domain.Contracts.TargetedCorrelationStatus.Unsupported),
            Inaccessible = Count(Infinium.Domain.Contracts.TargetedCorrelationStatus.Inaccessible),
            Malformed = Count(Infinium.Domain.Contracts.TargetedCorrelationStatus.Malformed),
            MissingRequiredProof = Count(Infinium.Domain.Contracts.TargetedCorrelationStatus.MissingRequiredProof),
        };
    }

    private static SemanticVersion ToProto(ContractVersion value) => new() { Value = value.ToString() };

    private static TargetedCorrelationCoverageRow ToProto(TargetedCorrelationCoverageRowContract value)
    {
        TargetedCorrelationCoverageRow result = new()
        {
            RowId = value.RowId.Value,
            ScopeMemberId = value.ScopeMemberId.Value,
            MemberKind = Enum.Parse<Infinium.Contracts.Protobuf.Application.V1.TargetedScopeMemberKind>(value.MemberKind.ToString()),
            SourceStableIdentity = value.SourceStableIdentity.Value,
            TargetPopulationId = value.TargetPopulationId.Value,
            TargetStableIdentity = value.TargetStableIdentity?.Value ?? string.Empty,
            CurrentExecutionMemberId = value.CurrentExecutionMemberId?.Value ?? string.Empty,
            Status = Enum.Parse<Infinium.Contracts.Protobuf.Application.V1.TargetedCorrelationStatus>(value.Status.ToString()),
            CorrelationQualified = value.CorrelationQualified,
            ProcessingQualified = value.ProcessingQualified,
            DenominatorEffect = value.DenominatorEffect,
            ReadinessEffect = value.ReadinessEffect,
            InertReason = value.Reason,
            EnumerationOrApplicabilityProofId = value.EnumerationOrApplicabilityProofId?.Value ?? string.Empty,
        };
        result.EvidenceIds.Add(value.EvidenceIds.Select(item => item.Value));
        return result;
    }

    private static Infinium.Contracts.Protobuf.Application.V1.TargetedVerificationPreparationState ToProto(
        Infinium.Domain.Contracts.TargetedVerificationPreparationState value) =>
        Enum.Parse<Infinium.Contracts.Protobuf.Application.V1.TargetedVerificationPreparationState>(value.ToString());

    private static string HashTargeted(string value) => HashTargeted(Encoding.UTF8.GetBytes(value));
    private static string HashTargeted(ReadOnlySpan<byte> value) =>
        Convert.ToHexStringLower(SHA256.HashData(value));
}
