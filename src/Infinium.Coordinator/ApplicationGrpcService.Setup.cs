using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Mo2;
using Infinium.Persistence;

namespace Infinium.Coordinator;

public sealed partial class ApplicationGrpcService
{
    private const string ToolObjectKind = "tool-configuration";
    private const string ProfileObjectKind = "profile-selection";
    private const string ConfigurationObjectKind = "saved-scan-configuration";
    private const string ProviderObjectKind = "provider-enrollment-status";
    private const string CurrentProfileObjectId = "current-profile";
    private const string CurrentProviderObjectId = "current-provider";

    public override Task<GetSetupStateResponse> GetSetupState(
        GetSetupStateRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (request.MaximumSavedConfigurations is 0 or > ProtocolConstants.MaximumPageItems)
        {
            return Task.FromResult(SetupError(
                ApplicationErrorCode.LimitExceeded,
                "The saved-configuration query exceeds its finite bound."));
        }
        try
        {
            ApplicationContractValidator.Validate(request);
        }
        catch (InvalidDataException exception)
        {
            return Task.FromResult(SetupError(
                ApplicationErrorCode.InvalidArgument,
                Bounded(exception.Message)));
        }
        if (!IsCurrentProjection(request.ExpectedProjectionVersion))
        {
            return Task.FromResult(SetupError(
                ApplicationErrorCode.ResyncRequired,
                "The setup projection is no longer current."));
        }

        return Task.FromResult(new GetSetupStateResponse
        {
            Setup = BuildSetupState(checked((int)request.MaximumSavedConfigurations)),
        });
    }

    public override Task<SubmitSetupCommandResponse> SubmitSetupCommand(
        SubmitSetupCommandRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        string requestId = string.IsNullOrWhiteSpace(request.RequestId)
            ? "invalid-setup-request"
            : request.RequestId;
        try
        {
            ApplicationContractValidator.Validate(request);
            requestId = Required(request.RequestId, "setup request ID");
            long expected = ParseRevision(request.ExpectedRevision, allowMissing: true);
            SetupMutationReceipt receipt = request.CommandCase switch
            {
                SubmitSetupCommandRequest.CommandOneofCase.ValidateTool =>
                    ValidateTool(requestId, expected, request.ValidateTool),
                SubmitSetupCommandRequest.CommandOneofCase.ConfirmProfile =>
                    ConfirmProfile(requestId, expected, request.ConfirmProfile),
                SubmitSetupCommandRequest.CommandOneofCase.CreateConfiguration =>
                    CreateConfiguration(requestId, expected, request.CreateConfiguration),
                SubmitSetupCommandRequest.CommandOneofCase.CloneConfiguration =>
                    CloneConfiguration(requestId, expected, request.CloneConfiguration),
                SubmitSetupCommandRequest.CommandOneofCase.UpdateConfiguration =>
                    UpdateConfiguration(requestId, expected, request.UpdateConfiguration),
                SubmitSetupCommandRequest.CommandOneofCase.DeleteConfiguration =>
                    DeleteConfiguration(requestId, expected, request.DeleteConfiguration),
                SubmitSetupCommandRequest.CommandOneofCase.RequestProviderEnrollment =>
                    RequestProviderEnrollmentIntent(
                        requestId,
                        expected,
                        request.RequestProviderEnrollment),
                _ => throw new InvalidDataException("A closed setup command is required."),
            };
            return Task.FromResult(new SubmitSetupCommandResponse
            {
                Receipt = AcceptedReceipt(receipt),
                Setup = BuildSetupState(checked((int)ProtocolConstants.MaximumPageItems)),
            });
        }
        catch (SetupRevisionConflictException conflict)
        {
            return Task.FromResult(new SubmitSetupCommandResponse
            {
                Receipt = ConflictReceipt(requestId, conflict),
                Setup = BuildSetupState(checked((int)ProtocolConstants.MaximumPageItems)),
            });
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException)
        {
            return Task.FromResult(new SubmitSetupCommandResponse
            {
                Receipt = RejectedReceipt(requestId, exception.Message),
                Setup = BuildSetupState(checked((int)ProtocolConstants.MaximumPageItems)),
            });
        }
    }

    public override Task<PrepareManualRunResponse> PrepareManualRun(
        PrepareManualRunRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            ApplicationContractValidator.Validate(request);
            string requestId = Required(request.RequestId, "run-preparation request ID");
            string configurationId = Required(
                request.SavedConfigurationId?.Value,
                "saved scan-configuration ID");
            long expectedConfiguration = ParseRevision(
                request.ExpectedConfigurationRevision,
                allowMissing: false);
            long expectedProfile = ParseRevision(
                request.ExpectedProfileRevision,
                allowMissing: false);
            SetupObjectRecord profile = ActiveSetupObject(ProfileObjectKind, CurrentProfileObjectId);
            SetupObjectRecord configuration = ActiveSetupObject(
                ConfigurationObjectKind,
                configurationId);
            if (profile.Revision != expectedProfile)
            {
                return Task.FromResult(PreparationConflict(expectedProfile, profile.Revision));
            }
            if (configuration.Revision != expectedConfiguration)
            {
                return Task.FromResult(PreparationConflict(
                    expectedConfiguration,
                    configuration.Revision));
            }

            ProfileStateDocument profileDocument = Deserialize<ProfileStateDocument>(profile.PayloadJson);
            if (!profileDocument.ExplicitlyConfirmed
                || string.IsNullOrWhiteSpace(profileDocument.ConfirmedCandidateId))
            {
                throw new InvalidOperationException(
                    "A suggested profile must be explicitly confirmed before preparing a run.");
            }
            ToolStateDocument mo2 = ReadToolDocument(ExternalToolKind.ModOrganizer2);
            if (mo2.State != ToolValidationState.Available)
            {
                throw new InvalidOperationException(
                    "The selected MO2 installation is not available for run preparation.");
            }
            ProfileStateDocument currentCandidates = ToolInstallationInspector.DiscoverProfiles(mo2);
            if (!currentCandidates.Candidates.Any(item =>
                    item.CandidateId == profileDocument.ConfirmedCandidateId))
            {
                throw new InvalidOperationException(
                    "The explicitly confirmed profile is no longer present in the validated MO2 installation.");
            }

            SavedConfigurationDocument saved =
                Deserialize<SavedConfigurationDocument>(configuration.PayloadJson);
            ValidateConfigurationValues(saved.Values);
            string snapshotId = Required(
                request.InstallationSnapshotId?.Value,
                "installation snapshot ID");
            string contextId = Required(request.AnalysisContextId?.Value, "analysis context ID");
            string manifestId = Required(
                request.ResolvedInputManifestId?.Value,
                "resolved input manifest ID");
            string effectiveJson = JsonSerializer.Serialize(new EffectiveConfigurationDocument(
                configurationId,
                configuration.Revision,
                profileDocument.ConfirmedCandidateId,
                profile.Revision,
                saved.Values));
            string effectiveId = "effective-" + Hash(effectiveJson)[..32];
            WorkEstimate estimate = BuildEstimate(saved.Values);
            string estimateJson = JsonSerializer.Serialize(EstimateDocument.FromProto(estimate));
            string preparationId = "preparation-" + Hash(string.Join(
                '\n',
                requestId,
                effectiveId,
                snapshotId,
                contextId,
                manifestId))[..32];
            PreparedRunRecord prepared = runtime.Store.CreatePreparedRun(new(
                preparationId,
                requestId,
                Revision: 1,
                profileDocument.ConfirmedCandidateId,
                profile.Revision,
                configurationId,
                configuration.Revision,
                effectiveId,
                effectiveJson,
                new RunBinding(snapshotId, contextId, effectiveId, manifestId),
                estimateJson,
                DateTimeOffset.UtcNow));
            return Task.FromResult(new PrepareManualRunResponse
            {
                Preparation = ToProto(prepared),
            });
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or InvalidDataException
            or KeyNotFoundException)
        {
            return Task.FromResult(new PrepareManualRunResponse
            {
                Error = new ApplicationContractError
                {
                    Code = exception is KeyNotFoundException
                        ? ApplicationErrorCode.NotFound
                        : ApplicationErrorCode.InvalidArgument,
                    InertDetail = Bounded(exception.Message),
                    RetryMayBeSafe = false,
                },
            });
        }
    }

    public override Task<SubmitRunCommandResponse> SubmitPreparedRun(
        SubmitPreparedRunRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            ApplicationContractValidator.Validate(request);
            string commandId = Required(request.IdempotencyKey?.Value, "durable command ID");
            string preparationId = Required(request.PreparationId, "prepared run ID");
            string gestureId = Required(request.UserGestureId, "user gesture ID");
            if (gestureId.Length < 16)
            {
                throw new InvalidDataException("A bounded one-shot user gesture is required.");
            }
            if (request.InitiationKind is not (
                    ManualInitiationKind.DesktopUserGesture
                    or ManualInitiationKind.EvaluationHarness))
            {
                throw new InvalidDataException(
                    "Prepared runs require a desktop or diagnostic user gesture.");
            }

            PreparedRunRecord prepared = runtime.Store.GetPreparedRun(preparationId);
            long expectedPreparation = ParseRevision(
                request.ExpectedPreparationRevision,
                allowMissing: false);
            if (prepared.Revision != expectedPreparation)
            {
                throw new SetupRevisionConflictException(
                    expectedPreparation,
                    prepared.Revision);
            }
            SetupObjectRecord currentProfile = ActiveSetupObject(
                ProfileObjectKind,
                CurrentProfileObjectId);
            SetupObjectRecord currentConfiguration = ActiveSetupObject(
                ConfigurationObjectKind,
                prepared.SavedConfigurationId);
            if (currentProfile.Revision != prepared.ProfileRevision
                || currentConfiguration.Revision != prepared.SavedConfigurationRevision)
            {
                throw new SetupRevisionConflictException(
                    prepared.SavedConfigurationRevision,
                    currentConfiguration.Revision);
            }

            bool replay = DurableCommandExists(commandId);
            if (!replay && !runtime.TryAdmitNewDurableCommand(DateTimeOffset.UtcNow))
            {
                return Task.FromResult(new SubmitRunCommandResponse
                {
                    Disposition = CommandDisposition.Rejected,
                    Failure = Failure(
                        FailureCode.LimitExceeded,
                        "The new durable-command rate bound is full."),
                });
            }
            string runId = request.RequestedRunId is null
                ? Guid.NewGuid().ToString("N")
                : Required(request.RequestedRunId.Value, "requested run ID");
            RunRecord run = runtime.Store.CreateRun(
                commandId,
                runId,
                prepared.Binding,
                runtime.Authority.FencingEpoch,
                DateTimeOffset.UtcNow,
                request.InitiationKind.ToString(),
                FromProto(request.DispatchDeadline),
                startUserGestureId: gestureId,
                startPreparationId: prepared.PreparationId);
            executor.Schedule(run.RunId);
            return Task.FromResult(new SubmitRunCommandResponse
            {
                Disposition = replay
                    ? CommandDisposition.AlreadyAccepted
                    : CommandDisposition.Accepted,
                DurableCommandId = new DurableCommandId { Value = commandId },
                RunId = new RunId { Value = run.RunId },
            });
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or InvalidDataException
            or KeyNotFoundException)
        {
            return Task.FromResult(new SubmitRunCommandResponse
            {
                Disposition = CommandDisposition.Rejected,
                Failure = Failure(
                    exception is KeyNotFoundException
                        ? FailureCode.NotFound
                        : exception is SetupRevisionConflictException
                            ? FailureCode.Conflict
                            : FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

    private SetupMutationReceipt ValidateTool(
        string requestId,
        long expected,
        ValidateToolConfiguration command)
    {
        if (command is null || command.Tool is not (
                ExternalToolKind.ModOrganizer2 or ExternalToolKind.Loot))
        {
            throw new InvalidDataException("A supported external tool kind is required.");
        }
        string root = command.Tool switch
        {
            ExternalToolKind.ModOrganizer2
                when command.LocationCase == ValidateToolConfiguration.LocationOneofCase.ModOrganizerInstallationRoot =>
                    command.ModOrganizerInstallationRoot,
            ExternalToolKind.Loot
                when command.LocationCase == ValidateToolConfiguration.LocationOneofCase.LootInstallationRoot =>
                    command.LootInstallationRoot,
            _ => throw new InvalidDataException(
                "The tool kind and tool-specific location do not match."),
        };
        ToolStateDocument document = ToolInstallationInspector.Inspect(command.Tool, root);
        return runtime.Store.ApplySetupMutation(new(
            requestId,
            "validate-tool",
            ToolObjectKind,
            ToolObjectId(command.Tool),
            expected,
            "active",
            JsonSerializer.Serialize(document),
            DateTimeOffset.UtcNow));
    }

    private SetupMutationReceipt ConfirmProfile(
        string requestId,
        long expected,
        ConfirmProfileSelection command)
    {
        string candidateId = Required(command?.CandidateId, "profile candidate ID");
        ToolStateDocument tool = ReadToolDocument(ExternalToolKind.ModOrganizer2);
        ProfileStateDocument observed = ToolInstallationInspector.DiscoverProfiles(tool);
        if (!observed.Candidates.Any(item => item.CandidateId == candidateId))
        {
            throw new InvalidOperationException(
                "The profile candidate is not present in the validated MO2 installation.");
        }
        ProfileStateDocument confirmed = observed with
        {
            ConfirmedCandidateId = candidateId,
            ExplicitlyConfirmed = true,
        };
        return runtime.Store.ApplySetupMutation(new(
            requestId,
            "confirm-profile",
            ProfileObjectKind,
            CurrentProfileObjectId,
            expected,
            "active",
            JsonSerializer.Serialize(confirmed),
            DateTimeOffset.UtcNow));
    }

    private SetupMutationReceipt CreateConfiguration(
        string requestId,
        long expected,
        CreateSavedScanConfiguration command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (expected != 0)
        {
            throw new InvalidDataException(
                "Creating a saved configuration requires the absent revision r0.");
        }
        string id = Required(command.ConfigurationId?.Value, "scan-configuration ID");
        string name = BoundedName(command.Name);
        ValidateConfigurationValues(command.Values);
        return SaveConfiguration(
            requestId,
            "create-configuration",
            id,
            expected,
            new(name, ConfigurationValuesDocument.FromProto(command.Values)));
    }

    private SetupMutationReceipt CloneConfiguration(
        string requestId,
        long expected,
        CloneSavedScanConfiguration command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (expected != 0)
        {
            throw new InvalidDataException(
                "Cloning a saved configuration requires a new target at revision r0.");
        }
        string sourceId = Required(
            command.SourceConfigurationId?.Value,
            "source scan-configuration ID");
        string targetId = Required(
            command.ConfigurationId?.Value,
            "scan-configuration ID");
        SetupObjectRecord source = ActiveSetupObject(ConfigurationObjectKind, sourceId);
        SavedConfigurationDocument sourceDocument =
            Deserialize<SavedConfigurationDocument>(source.PayloadJson);
        return SaveConfiguration(
            requestId,
            "clone-configuration",
            targetId,
            expected,
            sourceDocument with { Name = BoundedName(command.Name) });
    }

    private SetupMutationReceipt UpdateConfiguration(
        string requestId,
        long expected,
        UpdateSavedScanConfiguration command)
    {
        ArgumentNullException.ThrowIfNull(command);
        string id = Required(command.ConfigurationId?.Value, "scan-configuration ID");
        _ = ActiveSetupObject(ConfigurationObjectKind, id);
        ValidateConfigurationValues(command.Values);
        return SaveConfiguration(
            requestId,
            "update-configuration",
            id,
            expected,
            new(BoundedName(command.Name), ConfigurationValuesDocument.FromProto(command.Values)));
    }

    private SetupMutationReceipt DeleteConfiguration(
        string requestId,
        long expected,
        DeleteSavedScanConfiguration command)
    {
        string id = Required(command?.ConfigurationId?.Value, "scan-configuration ID");
        SetupObjectRecord current = ActiveSetupObject(ConfigurationObjectKind, id);
        return runtime.Store.ApplySetupMutation(new(
            requestId,
            "delete-configuration",
            ConfigurationObjectKind,
            id,
            expected,
            "deleted",
            current.PayloadJson,
            DateTimeOffset.UtcNow));
    }

    private SetupMutationReceipt RequestProviderEnrollmentIntent(
        string requestId,
        long expected,
        RequestProviderEnrollment command)
    {
        ArgumentNullException.ThrowIfNull(command);
        string profileId = Required(command.ProfileId?.Value, "provider profile ID");
        string label = BoundedName(command.DisplayLabel);
        string generationId = "generation-" + Hash(requestId)[..24];
        ProviderStateDocument document = new(
            profileId,
            generationId,
            "pending-enrollment",
            "not-verified",
            SecureStoreAvailable: false,
            InertStatus: $"Native credential entry for '{label}' remains unavailable in this phase; no secret entered the application contract.");
        return runtime.Store.ApplySetupMutation(new(
            requestId,
            "request-provider-enrollment",
            ProviderObjectKind,
            CurrentProviderObjectId,
            expected,
            "active",
            JsonSerializer.Serialize(document),
            DateTimeOffset.UtcNow));
    }

    private SetupMutationReceipt SaveConfiguration(
        string requestId,
        string operation,
        string id,
        long expected,
        SavedConfigurationDocument document) =>
        runtime.Store.ApplySetupMutation(new(
            requestId,
            operation,
            ConfigurationObjectKind,
            id,
            expected,
            "active",
            JsonSerializer.Serialize(document),
            DateTimeOffset.UtcNow));

    private SetupState BuildSetupState(int maximumConfigurations)
    {
        SetupState state = new()
        {
            ProjectionVersion = new ProjectionVersion { Value = "1" },
            ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
        };
        foreach (ExternalToolKind kind in new[]
                 {
                     ExternalToolKind.ModOrganizer2,
                     ExternalToolKind.Loot,
                 })
        {
            SetupObjectRecord? record = runtime.Store.FindSetupObject(
                ToolObjectKind,
                ToolObjectId(kind));
            state.Tools.Add(record is null
                ? NotValidatedTool(kind)
                : ToProto(record, Deserialize<ToolStateDocument>(record.PayloadJson)));
        }

        ToolStateDocument? mo2 = TryReadToolDocument(ExternalToolKind.ModOrganizer2);
        SetupObjectRecord? selection = runtime.Store.FindSetupObject(
            ProfileObjectKind,
            CurrentProfileObjectId);
        ProfileStateDocument profiles = mo2 is null
            ? ProfileStateDocument.Empty
            : ToolInstallationInspector.DiscoverProfiles(mo2);
        if (selection is not null && selection.LifecycleState == "active")
        {
            ProfileStateDocument saved = Deserialize<ProfileStateDocument>(selection.PayloadJson);
            bool confirmedCandidatePresent = saved.ExplicitlyConfirmed
                && profiles.Candidates.Any(item => item.CandidateId == saved.ConfirmedCandidateId);
            profiles = profiles with
            {
                ConfirmedCandidateId = confirmedCandidatePresent
                    ? saved.ConfirmedCandidateId
                    : null,
                ExplicitlyConfirmed = confirmedCandidatePresent,
            };
        }
        state.ProfileSelection = ToProto(profiles, selection?.Revision ?? 0, selection?.UpdatedAt);

        foreach (SetupObjectRecord configuration in runtime.Store.ListSetupObjects(
                     ConfigurationObjectKind,
                     maximumConfigurations))
        {
            state.SavedConfigurations.Add(ToProto(
                configuration,
                Deserialize<SavedConfigurationDocument>(configuration.PayloadJson)));
        }

        SetupObjectRecord? provider = runtime.Store.FindSetupObject(
            ProviderObjectKind,
            CurrentProviderObjectId);
        state.Provider = provider is null
            ? new ProviderEnrollmentStatus
            {
                Configured = false,
                Verified = false,
                EnrollmentPending = false,
                SecureStoreAvailable = false,
                InertStatus = "No provider credential profile is configured; local-only work remains available and native enrollment is unavailable in this phase.",
                Revision = new RevisionToken { OpaqueValue = "r0" },
            }
            : ToProto(provider, Deserialize<ProviderStateDocument>(provider.PayloadJson));
        return state;
    }

    private SetupObjectRecord ActiveSetupObject(string kind, string id)
    {
        SetupObjectRecord? value = runtime.Store.FindSetupObject(kind, id);
        return value is null || value.LifecycleState != "active"
            ? throw new KeyNotFoundException($"The active {kind} '{id}' does not exist.")
            : value;
    }

    private ToolStateDocument ReadToolDocument(ExternalToolKind kind) =>
        TryReadToolDocument(kind)
        ?? throw new KeyNotFoundException("The tool configuration does not exist.");

    private ToolStateDocument? TryReadToolDocument(ExternalToolKind kind)
    {
        SetupObjectRecord? record = runtime.Store.FindSetupObject(
            ToolObjectKind,
            ToolObjectId(kind));
        return record is null || record.LifecycleState != "active"
            ? null
            : Deserialize<ToolStateDocument>(record.PayloadJson);
    }

    private static GetSetupStateResponse SetupError(ApplicationErrorCode code, string detail) => new()
    {
        Error = new ApplicationContractError
        {
            Code = code,
            InertDetail = detail,
            RetryMayBeSafe = false,
            CurrentProjectionVersion = code == ApplicationErrorCode.ResyncRequired
                ? new ProjectionVersion { Value = "1" }
                : null,
        },
    };

    private static PrepareManualRunResponse PreparationConflict(long expected, long current) => new()
    {
        Conflict = new RevisionConflict
        {
            Expected = Revision(expected),
            Current = Revision(current),
            Disposition = ConflictDisposition.StaleRevision,
        },
    };

    private static UserOperationReceipt AcceptedReceipt(SetupMutationReceipt value) => new()
    {
        RequestId = value.RequestId,
        ReceiptId = "receipt-" + value.RequestFingerprint[..24],
        Disposition = value.Replayed
            ? OperationDisposition.AlreadyAccepted
            : OperationDisposition.Accepted,
        AcceptedRevision = Revision(value.AcceptedRevision),
        ObservedAt = ProtoMapping.ToProto(value.RecordedAt),
    };

    private static UserOperationReceipt ConflictReceipt(
        string requestId,
        SetupRevisionConflictException conflict) => new()
        {
            RequestId = requestId,
            ReceiptId = "receipt-" + Hash(requestId + ":conflict")[..24],
            Disposition = OperationDisposition.Conflict,
            Conflict = new RevisionConflict
            {
                Expected = Revision(conflict.ExpectedRevision),
                Current = Revision(conflict.CurrentRevision),
                Disposition = ConflictDisposition.StaleRevision,
            },
            Error = new ApplicationContractError
            {
                Code = ApplicationErrorCode.Conflict,
                InertDetail = "The setup object changed; read the current non-secret state before retrying.",
                RetryMayBeSafe = true,
            },
            ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
        };

    private static UserOperationReceipt RejectedReceipt(string requestId, string detail) => new()
    {
        RequestId = requestId,
        ReceiptId = "receipt-" + Hash(requestId + ":rejected")[..24],
        Disposition = OperationDisposition.Rejected,
        Error = new ApplicationContractError
        {
            Code = ApplicationErrorCode.InvalidArgument,
            InertDetail = Bounded(detail),
            RetryMayBeSafe = false,
        },
        ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
    };

    private static ToolConfiguration NotValidatedTool(ExternalToolKind kind) => new()
    {
        Tool = kind,
        State = ToolValidationState.NotYetValidated,
        InertReason = "No typed validation has been completed.",
        Revision = Revision(0),
    };

    private static ToolConfiguration ToProto(
        SetupObjectRecord record,
        ToolStateDocument document)
    {
        ToolConfiguration result = new()
        {
            Tool = document.Tool,
            State = document.State,
            InstallationRoot = document.InstallationRoot,
            ExecutablePath = document.ExecutablePath,
            ObservedVersion = document.ObservedVersion,
            InertReason = document.InertReason,
            Revision = Revision(record.Revision),
            ValidatedAt = ProtoMapping.ToProto(record.UpdatedAt),
        };
        result.CapabilityGaps.Add(document.CapabilityGaps);
        return result;
    }

    private static ProfileSelection ToProto(
        ProfileStateDocument document,
        long revision,
        DateTimeOffset? updatedAt)
    {
        ProfileSelection result = new()
        {
            SuggestedCandidateId = document.SuggestedCandidateId ?? string.Empty,
            ConfirmedCandidateId = document.ConfirmedCandidateId ?? string.Empty,
            ExplicitlyConfirmed = document.ExplicitlyConfirmed,
            Revision = Revision(revision),
        };
        if (updatedAt is not null)
        {
            result.UpdatedAt = ProtoMapping.ToProto(updatedAt.Value);
        }
        result.Candidates.Add(document.Candidates.Select(item => new ProfileCandidate
        {
            CandidateId = item.CandidateId,
            DisplayName = item.DisplayName,
            SavedSelectionSuggestion = item.SavedSelectionSuggestion,
        }));
        return result;
    }

    private static SavedScanConfiguration ToProto(
        SetupObjectRecord record,
        SavedConfigurationDocument document) => new()
        {
            ConfigurationId = new ScanConfigurationId { Value = record.ObjectId },
            Name = document.Name,
            Values = document.Values.ToProto(),
            Revision = Revision(record.Revision),
            Deleted = record.LifecycleState == "deleted",
            UpdatedAt = ProtoMapping.ToProto(record.UpdatedAt),
        };

    private static ProviderEnrollmentStatus ToProto(
        SetupObjectRecord record,
        ProviderStateDocument document) => new()
        {
            Configured = document.LifecycleState is not "deleted",
            Verified = document.LifecycleState == "active-verified",
            EnrollmentPending = document.LifecycleState == "pending-enrollment",
            SecureStoreAvailable = document.SecureStoreAvailable,
            InertStatus = document.InertStatus,
            ProfileId = new ProviderAccessProfileId { Value = document.ProfileId },
            Revision = Revision(record.Revision),
        };

    private static PreparedManualRun ToProto(PreparedRunRecord value)
    {
        WorkEstimate estimate = EstimateDocument.ToProto(
            Deserialize<EstimateDocument>(value.EstimateJson));
        PreparedManualRun result = new()
        {
            PreparationId = value.PreparationId,
            Revision = Revision(value.Revision),
            ConfirmedProfileId = value.ConfirmedProfileId,
            ProfileRevision = Revision(value.ProfileRevision),
            SavedConfigurationId = new ScanConfigurationId
            {
                Value = value.SavedConfigurationId,
            },
            SavedConfigurationRevision = Revision(value.SavedConfigurationRevision),
            EffectiveConfigurationId = new ScanConfigurationId
            {
                Value = value.EffectiveConfigurationId,
            },
            InstallationSnapshotId = new InstallationSnapshotId
            {
                Value = value.Binding.InstallationSnapshotId,
            },
            AnalysisContextId = new AnalysisContextId
            {
                Value = value.Binding.AnalysisContextId,
            },
            ResolvedInputManifestId = new ResolvedInputManifestId
            {
                Value = value.Binding.ResolvedInputManifestId,
            },
            Estimate = estimate,
            PreparedAt = ProtoMapping.ToProto(value.PreparedAt),
        };
        result.Limitations.Add(
            "The estimate describes only configured local work; no network or billable authority is implied.");
        return result;
    }

    private static WorkEstimate BuildEstimate(ConfigurationValuesDocument values)
    {
        WorkEstimate estimate = new()
        {
            PlannedWorkUnits = new OptionalUInt64
            {
                Availability = AvailabilityState.Available,
                Value = checked((ulong)values.AnalyzerIds.Count),
            },
            EstimatedElapsedMilliseconds = new OptionalUInt64
            {
                Availability = AvailabilityState.Unavailable,
            },
            EstimatedCoverageUnits = new OptionalUInt64
            {
                Availability = AvailabilityState.Unavailable,
            },
            AuthorityStatement = values.LocalOnly
                ? "Local-only work is configured; provider authority is not required."
                : "Provider work is not authorized by this estimate and remains unavailable.",
        };
        if (values.LocalOnly)
        {
            estimate.EstimatedCalculatedNanoUsd = new OptionalInt64
            {
                Availability = AvailabilityState.Available,
                Value = 0,
            };
            estimate.ProviderDispatches = new OptionalUInt64
            {
                Availability = AvailabilityState.Available,
                Value = 0,
            };
        }
        else
        {
            estimate.EstimatedCalculatedNanoUsd = new OptionalInt64
            {
                Availability = AvailabilityState.Unavailable,
            };
            estimate.ProviderDispatches = new OptionalUInt64
            {
                Availability = AvailabilityState.Unavailable,
            };
        }
        return estimate;
    }

    private static void ValidateConfigurationValues(ScanConfigurationValues values)
    {
        if (values is null)
        {
            throw new InvalidDataException("Typed scan-configuration values are required.");
        }
        ValidateConfigurationValues(ConfigurationValuesDocument.FromProto(values));
    }

    private static void ValidateConfigurationValues(ConfigurationValuesDocument values)
    {
        if (values.AnalyzerIds.Count is 0 or > 64
            || values.AnalyzerIds.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 128)
            || values.AnalyzerIds.Distinct(StringComparer.Ordinal).Count() != values.AnalyzerIds.Count
            || values.MaximumConcurrency is 0 or > 64
            || values.MaximumElapsedMilliseconds is 0 or > 604_800_000)
        {
            throw new InvalidDataException("The scan configuration exceeds its typed finite bounds.");
        }
        if (values.LocalOnly
            && (values.MaximumProviderDispatches != 0 || values.MaximumCalculatedNanoUsd != 0))
        {
            throw new InvalidDataException(
                "A local-only configuration cannot reserve provider dispatch or calculated cost authority.");
        }
    }

    private static long ParseRevision(RevisionToken? value, bool allowMissing)
    {
        if (value is null || string.IsNullOrWhiteSpace(value.OpaqueValue))
        {
            return allowMissing ? 0 : throw new InvalidDataException("An expected revision is required.");
        }
        string token = value.OpaqueValue;
        return token.Length >= 2 && token[0] == 'r'
            && long.TryParse(token.AsSpan(1), out long revision) && revision >= 0
                ? revision
                : throw new InvalidDataException("The expected revision is malformed.");
    }

    private static RevisionToken Revision(long value) => new() { OpaqueValue = $"r{value}" };

    private static string BoundedName(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 120
            ? throw new InvalidDataException("A bounded display name is required.")
            : value;

    private static string ToolObjectId(ExternalToolKind kind) => kind switch
    {
        ExternalToolKind.ModOrganizer2 => "mod-organizer-2",
        ExternalToolKind.Loot => "loot",
        _ => throw new InvalidDataException("The tool kind is unsupported."),
    };

    internal static ToolValidationState ClassifySupportedToolVersion(
        ExternalToolKind tool,
        string version) => ToolInstallationInspector.Classify(
            tool,
            "typed-root",
            tool == ExternalToolKind.ModOrganizer2 ? "ModOrganizer.exe" : "LOOT.exe",
            version).State;

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json)
        ?? throw new InvalidDataException("The retained setup object is malformed.");

    private sealed record ToolStateDocument(
        ExternalToolKind Tool,
        ToolValidationState State,
        string InstallationRoot,
        string ExecutablePath,
        string ObservedVersion,
        string InertReason,
        IReadOnlyList<string> CapabilityGaps);

    private sealed record ProfileCandidateDocument(
        string CandidateId,
        string DisplayName,
        bool SavedSelectionSuggestion);

    private sealed record ProfileStateDocument(
        IReadOnlyList<ProfileCandidateDocument> Candidates,
        string? SuggestedCandidateId,
        string? ConfirmedCandidateId,
        bool ExplicitlyConfirmed)
    {
        public static ProfileStateDocument Empty { get; } = new([], null, null, false);
    }

    private sealed record ConfigurationValuesDocument(
        IReadOnlyList<string> AnalyzerIds,
        bool LocalOnly,
        uint MaximumConcurrency,
        ulong MaximumProviderDispatches,
        ulong MaximumCalculatedNanoUsd,
        ulong MaximumElapsedMilliseconds)
    {
        public static ConfigurationValuesDocument FromProto(ScanConfigurationValues value) => new(
            value.AnalyzerIds.ToArray(),
            value.LocalOnly,
            value.MaximumConcurrency,
            value.MaximumProviderDispatches,
            value.MaximumCalculatedNanoUsd,
            value.MaximumElapsedMilliseconds);

        public ScanConfigurationValues ToProto()
        {
            ScanConfigurationValues value = new()
            {
                LocalOnly = LocalOnly,
                MaximumConcurrency = MaximumConcurrency,
                MaximumProviderDispatches = MaximumProviderDispatches,
                MaximumCalculatedNanoUsd = MaximumCalculatedNanoUsd,
                MaximumElapsedMilliseconds = MaximumElapsedMilliseconds,
            };
            value.AnalyzerIds.Add(AnalyzerIds);
            return value;
        }
    }

    private sealed record SavedConfigurationDocument(
        string Name,
        ConfigurationValuesDocument Values);

    private sealed record EffectiveConfigurationDocument(
        string SavedConfigurationId,
        long SavedConfigurationRevision,
        string ConfirmedProfileId,
        long ProfileRevision,
        ConfigurationValuesDocument Values);

    private sealed record ProviderStateDocument(
        string ProfileId,
        string GenerationId,
        string LifecycleState,
        string VerificationState,
        bool SecureStoreAvailable,
        string InertStatus);

    private sealed record EstimateDocument(
        ulong? PlannedWorkUnits,
        ulong? EstimatedElapsedMilliseconds,
        long? EstimatedCalculatedNanoUsd,
        ulong? ProviderDispatches,
        ulong? EstimatedCoverageUnits,
        string AuthorityStatement)
    {
        public static EstimateDocument FromProto(WorkEstimate value) => new(
            Available(value.PlannedWorkUnits),
            Available(value.EstimatedElapsedMilliseconds),
            Available(value.EstimatedCalculatedNanoUsd),
            Available(value.ProviderDispatches),
            Available(value.EstimatedCoverageUnits),
            value.AuthorityStatement);

        public static WorkEstimate ToProto(EstimateDocument value) => new()
        {
            PlannedWorkUnits = Optional(value.PlannedWorkUnits),
            EstimatedElapsedMilliseconds = Optional(value.EstimatedElapsedMilliseconds),
            EstimatedCalculatedNanoUsd = Optional(value.EstimatedCalculatedNanoUsd),
            ProviderDispatches = Optional(value.ProviderDispatches),
            EstimatedCoverageUnits = Optional(value.EstimatedCoverageUnits),
            AuthorityStatement = value.AuthorityStatement,
        };

        private static ulong? Available(OptionalUInt64 value) =>
            value.Availability == AvailabilityState.Available ? value.Value : null;

        private static long? Available(OptionalInt64 value) =>
            value.Availability == AvailabilityState.Available ? value.Value : null;

        private static OptionalUInt64 Optional(ulong? value) => value is null
            ? new() { Availability = AvailabilityState.Unavailable }
            : new() { Availability = AvailabilityState.Available, Value = value.Value };

        private static OptionalInt64 Optional(long? value) => value is null
            ? new() { Availability = AvailabilityState.Unavailable }
            : new() { Availability = AvailabilityState.Available, Value = value.Value };
    }

    private static class ToolInstallationInspector
    {
        public static ToolStateDocument Inspect(ExternalToolKind tool, string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
            {
                return State(tool, ToolValidationState.Misconfigured, root, string.Empty, string.Empty,
                    "The tool-specific installation root is not an absolute local path.");
            }
            string fullRoot;
            try
            {
                fullRoot = Path.GetFullPath(root);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
            {
                return State(tool, ToolValidationState.Misconfigured, root, string.Empty, string.Empty,
                    "The tool-specific installation root is malformed.");
            }
            if (fullRoot.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return State(tool, ToolValidationState.Misconfigured, fullRoot, string.Empty, string.Empty,
                    "Only an explicit local tool installation root is accepted.");
            }
            if (!Directory.Exists(fullRoot))
            {
                return State(tool, ToolValidationState.Missing, fullRoot, string.Empty, string.Empty,
                    "The selected installation root does not exist.");
            }
            DirectoryInfo rootInfo = new(fullRoot);
            if ((rootInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return State(tool, ToolValidationState.Misconfigured, fullRoot, string.Empty, string.Empty,
                    "Reparse-point installation roots are not accepted.");
            }
            try
            {
                _ = Directory.EnumerateFileSystemEntries(fullRoot).Take(1).Count();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return State(tool, ToolValidationState.Misconfigured, fullRoot, string.Empty, string.Empty,
                    "The selected tool installation root is inaccessible.");
            }
            string executableName = tool == ExternalToolKind.ModOrganizer2
                ? "ModOrganizer.exe"
                : "LOOT.exe";
            string executable = Path.Combine(fullRoot, executableName);
            if (!File.Exists(executable))
            {
                return State(tool, ToolValidationState.Missing, fullRoot, executable, string.Empty,
                    $"The exact {executableName} executable is missing.");
            }
            FileInfo executableInfo = new(executable);
            if ((executableInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return State(tool, ToolValidationState.Misconfigured, fullRoot, executable, string.Empty,
                    "Reparse-point tool executables are not accepted.");
            }
            string version;
            try
            {
                version = FileVersionInfo.GetVersionInfo(executable).FileVersion ?? string.Empty;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return State(tool, ToolValidationState.Misconfigured, fullRoot, executable, string.Empty,
                    "The tool executable identity is inaccessible.");
            }
            return Classify(tool, fullRoot, executable, version);
        }

        internal static ToolStateDocument Classify(
            ExternalToolKind tool,
            string root,
            string executable,
            string version)
        {
            if (tool == ExternalToolKind.ModOrganizer2)
            {
                return version.StartsWith("2.5.2", StringComparison.Ordinal)
                    ? State(tool, ToolValidationState.Available, root, executable, version,
                        "The exact supported MO2 version is available.")
                    : State(tool, ToolValidationState.Unsupported, root, executable, version,
                        "Only MO2 2.5.2 is currently supported.");
            }
            return State(tool, ToolValidationState.NotYetValidated, root, executable, version,
                "LOOT installation presence is recorded, but no application invocation is authorized.");
        }

        public static ProfileStateDocument DiscoverProfiles(ToolStateDocument tool)
        {
            if (tool.Tool != ExternalToolKind.ModOrganizer2
                || tool.State != ToolValidationState.Available)
            {
                return ProfileStateDocument.Empty;
            }
            string profilesRoot = Path.Combine(tool.InstallationRoot, "profiles");
            if (!Directory.Exists(profilesRoot))
            {
                return ProfileStateDocument.Empty;
            }
            DirectoryInfo profilesInfo = new(profilesRoot);
            if ((profilesInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return ProfileStateDocument.Empty;
            }
            string? savedSelection = ReadSavedSelection(tool.InstallationRoot);
            List<ProfileCandidateDocument> candidates = [];
            try
            {
                foreach (string path in Directory.EnumerateDirectories(profilesRoot)
                             .Order(StringComparer.OrdinalIgnoreCase)
                             .Take(100))
                {
                    DirectoryInfo info = new(path);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }
                    string name = info.Name;
                    candidates.Add(new(
                        "profile-" + Hash(name.ToUpperInvariant())[..24],
                        name,
                        SavedSelectionSuggestion: string.Equals(
                            name,
                            savedSelection,
                            StringComparison.Ordinal)));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return ProfileStateDocument.Empty;
            }
            string? suggested = candidates.SingleOrDefault(item => item.SavedSelectionSuggestion)?.CandidateId;
            return new(candidates, suggested, null, false);
        }

        private static string? ReadSavedSelection(string installationRoot)
        {
            try
            {
                string value = Mo2SnapshotCapture.ReadPortableSavedSelection(installationRoot);
                return value.Length is > 0 and <= 128 ? value : null;
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or Win32Exception)
            {
                return null;
            }
        }

        private static ToolStateDocument State(
            ExternalToolKind tool,
            ToolValidationState state,
            string root,
            string executable,
            string version,
            string reason) => new(
                tool,
                state,
                root ?? string.Empty,
                executable,
                version,
                reason,
                state == ToolValidationState.Available
                    ? []
                    : ["Dependent analysis capability is unavailable until exact validation succeeds."]);
    }
}
