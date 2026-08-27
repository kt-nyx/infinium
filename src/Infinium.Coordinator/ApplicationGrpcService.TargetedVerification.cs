using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using Infinium.Application.Analysis;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Infinium.Persistence;

namespace Infinium.Coordinator;

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
                    Preparation = ToTargetedPreparation(replay, 100, null),
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
            if (!StringComparer.Ordinal.Equals(sourceItem.Kind, occurrenceKind))
            {
                throw new InvalidOperationException("The targeted source occurrence kind differs from canonical result state.");
            }

            byte[] canonicalBytes = runtime.Store.ReadFindingCasePayload(sourceItem.SourcePayloadId);
            if (HashTargeted(canonicalBytes) != sourceItem.SourcePayloadSha256)
            {
                throw new AnalysisIdentityDriftException("The targeted source occurrence payload drifted.");
            }

            FindingCaseContract canonical = FindingCaseJsonCodec.Deserialize(canonicalBytes);
            if (occurrenceKind == "finding")
            {
                _ = canonical.Findings.Single(item => item.FindingOccurrenceId.Value == occurrenceId);
            }
            else
            {
                _ = canonical.Cases.Single(item => item.CaseOccurrenceId.Value == occurrenceId);
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
                Preparation = ToTargetedPreparation(admitted, 100, null),
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
            TargetedPreparationPersistenceRecord value = runtime.Store.GetTargetedPreparation(request.PreparationId);
            return Task.FromResult(new GetTargetedVerificationPreparationResponse
            {
                Preparation = ToTargetedPreparation(value, checked((int)request.MaximumMembers), request.AfterMemberId),
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidDataException
            or KeyNotFoundException or OverflowException)
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
                Preparation = ToTargetedPreparation(receipt.Preparation, 100, null),
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
                ? Guid.NewGuid().ToString("N") : request.RequestedRunId;
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

    private TargetedVerificationPreparation ToTargetedPreparation(
        TargetedPreparationPersistenceRecord value, int maximumMembers, string? afterMemberId)
    {
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
        };
        if (value.SourceOccurrenceKind == "finding")
        {
            result.SourceFindingOccurrenceId = value.SourceOccurrenceId;
        }
        else
        {
            result.SourceCaseOccurrenceId = value.SourceOccurrenceId;
        }

        ResultItemPersistenceRecord sourceItem = runtime.Store.GetResultItem(value.SourceRunId, value.SourceOccurrenceId);
        result.SourceLogicalId = sourceItem.LogicalId;
        result.SourcePayloadId = sourceItem.SourcePayloadId;
        result.SourcePayloadFingerprintSha256 = sourceItem.SourcePayloadSha256;
        result.SourceCanonicalSignatureSha256 = HashTargeted(string.Join('\n', sourceItem.ItemId,
            sourceItem.LogicalId, sourceItem.SourcePayloadSha256, sourceItem.AnalyzerId, sourceItem.AnalyzerVersion));
        result.SourceSnapshotId = runtime.Store.GetRun(value.SourceRunId).Binding.InstallationSnapshotId;
        try
        {
            SnapshotCaptureOperationRecord capture = runtime.Store.GetSnapshotCaptureOperation(value.CaptureOperationId);
            result.TargetSnapshotId = capture.InstallationSnapshotId ?? result.TargetSnapshotId;
        }
        catch (KeyNotFoundException) { }
        if (value.EvidenceAcquisitionId is not null)
        {
            SemanticAcquisitionPersistenceRecord acquisition = runtime.Store.GetSemanticAcquisition(value.EvidenceAcquisitionId);
            result.EvidenceAcquisitionState = acquisition.State;
            result.EvidenceAcquisitionGeneration = checked((ulong)acquisition.Generation);
            try
            {
                SemanticAcquisitionPublicationRecord publication = runtime.Store.GetSemanticAcquisitionPublication(acquisition.AcquisitionId);
                result.SemanticOutputId = publication.SemanticOutputId;
                result.SemanticOutputFingerprintSha256 = publication.PayloadSha256;
            }
            catch (KeyNotFoundException) { }
        }
        if (value.PlanId is not null)
        {
            TargetedVerificationPlanContract plan = runtime.Store.ReadTargetedPlan(value.PreparationId);
            result.TargetSnapshotFingerprintSha256 = plan.TargetSnapshotFingerprint.Value;
            result.ScopeId = plan.Scope.ScopeId.Value;
            result.ScopeFingerprintSha256 = plan.Scope.CanonicalFingerprint.Value;
            result.PopulationDenominator = checked((ulong)plan.CorrelationCoverage.PopulationDenominator);
            result.InertGaps.Add(plan.Gaps);
            result.InertNonStartableReasons.Add(plan.NonStartableReasons);
            TargetedScopeMemberContract[] page = plan.Scope.Members
                .Where(item => string.IsNullOrWhiteSpace(afterMemberId)
                    || StringComparer.Ordinal.Compare(item.MemberId.Value, afterMemberId) > 0)
                .Take(maximumMembers + 1).ToArray();
            bool hasMore = page.Length > maximumMembers;
            foreach (TargetedScopeMemberContract member in page.Take(maximumMembers))
            {
                result.ScopeMembers.Add(ToProto(member));
            }

            HashSet<OpaqueId> visible = result.ScopeMembers.Select(item => new OpaqueId(item.MemberId)).ToHashSet();
            foreach (TargetedCorrelationCoverageRowContract row in plan.CorrelationCoverage.Rows
                         .Where(item => visible.Contains(item.ScopeMemberId)))
            {
                result.CorrelationRows.Add(ToProto(row));
            }

            if (hasMore)
            {
                result.NextMemberCursor = page[maximumMembers - 1].MemberId.Value;
            }
        }
        TargetedPreparationDiagnosticsPersistenceRecord diagnostics =
            runtime.Store.GetTargetedPreparationDiagnostics(value.PreparationId);
        result.CaptureAttemptId = diagnostics.CaptureAttemptId ?? string.Empty;
        result.StructuralComparison = diagnostics.StructuralComparison ?? string.Empty;
        result.EvidenceAcquisitionAttemptCount = checked((ulong)diagnostics.EvidenceAttemptCount);
        result.EvidenceAcquisitionAttemptId = diagnostics.EvidenceAttemptId ?? string.Empty;
        result.EvidenceProgressCompleted = checked((ulong)diagnostics.EvidenceProgressCompleted);
        result.EvidenceProgressDenominator = checked((ulong)diagnostics.EvidenceProgressDenominator);
        result.EvidenceCheckpointId = diagnostics.EvidenceCheckpointId ?? string.Empty;
        result.EffectiveConfigurationId = value.SavedConfigurationId;
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

    private static TargetedScopeMember ToProto(TargetedScopeMemberContract value)
    {
        TargetedScopeMember result = new()
        {
            MemberId = value.MemberId.Value,
            Kind = Enum.Parse<Infinium.Contracts.Protobuf.Application.V1.TargetedScopeMemberKind>(value.Kind.ToString()),
            StableIdentity = value.StableIdentity.Value,
            InertReason = value.Reason,
            Mandatory = value.Mandatory,
        };
        result.SourceProofIds.Add(value.SourceProofIds.Select(item => item.Value));
        return result;
    }

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
