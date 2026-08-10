using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Google.Protobuf;
using Grpc.Core;
using Infinium.Application.Analysis;
using Infinium.Application.Runtime;
using Infinium.Application.Serialization;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.AspNetCore.Connections.Features;
using DomainLifecycleState = Infinium.Domain.Contracts.LifecycleState;

namespace Infinium.Coordinator;


public sealed partial class ApplicationGrpcService
{
    public override async Task SubscribeEvents(
        SubscribeEventsRequest request,
        IServerStreamWriter<ApplicationEvent> responseStream,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (request.RequestedQueueItems == 0
            || request.RequestedQueueItems > ProtocolConstants.MaximumStreamQueueItems
            || request.RunScope.Count != 1)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid bounded subscription."));
        }

        if (!runtime.TryAdmitEventSubscription())
        {
            throw new RpcException(
                new Status(StatusCode.ResourceExhausted, "The event subscription admission bound is full."));
        }

        try
        {
            string runId = Required(request.RunScope[0].Value, "run ID");
            RunRecord run = runtime.Store.GetRun(runId);
            if (!IsCurrentProjection(request.ExpectedProjectionVersion))
            {
                await WriteResyncAsync(
                    responseStream,
                    request,
                    run,
                    ResyncReason.ProjectionRebuilt).ConfigureAwait(false);
                return;
            }

            EventStreamCursor? cursor = null;
            if (request.After is not null && request.After.OpaqueValue.Length > 0)
            {
                try
                {
                    cursor = DecodeEventCursor(request.After.OpaqueValue);
                }
                catch (InvalidOperationException)
                {
                    await WriteResyncAsync(
                        responseStream,
                        request,
                        run,
                        IsCursorFromAnotherCoordinator(request.After.OpaqueValue)
                            ? ResyncReason.CoordinatorRestart
                            : ResyncReason.CursorInvalid).ConfigureAwait(false);
                    return;
                }

                ResyncReason? reason =
                    cursor.RunId != runId ? ResyncReason.CursorInvalid
                    : cursor.CoordinatorInstanceId != runtime.Authority.InstanceId
                        || cursor.CoordinatorFencingEpoch != runtime.Authority.FencingEpoch
                        ? ResyncReason.CoordinatorRestart
                    : cursor.ExpiresAt < DateTimeOffset.UtcNow
                        ? ResyncReason.ReplayWindowExpired
                    : cursor.ProjectionVersion != "1"
                        ? ResyncReason.ProjectionRebuilt
                    : cursor.DurableSequence > run.DurableSequence
                        ? ResyncReason.SequenceGap
                    : run.DurableSequence - cursor.DurableSequence > 1
                        ? ResyncReason.SequenceGap
                    : null;
                if (reason is not null)
                {
                    await WriteResyncAsync(responseStream, request, run, reason.Value)
                        .ConfigureAwait(false);
                    return;
                }
            }

            Channel<ApplicationEvent> queue = Channel.CreateBounded<ApplicationEvent>(
                new BoundedChannelOptions(checked((int)request.RequestedQueueItems))
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                    SingleWriter = true,
                });
            bool overflow = false;
            Task producer = Task.Run(async () =>
            {
                long lastSequence = cursor?.DurableSequence ?? 0;
                try
                {
                    while (!context.CancellationToken.IsCancellationRequested)
                    {
                        RunRecord current = runtime.Store.GetRun(runId);
                        if (current.DurableSequence > lastSequence)
                        {
                            ApplicationEvent item = ProgressEvent(request, current);
                            if (!queue.Writer.TryWrite(item))
                            {
                                overflow = true;
                                break;
                            }

                            lastSequence = current.DurableSequence;
                        }

                        await Task.Delay(50, context.CancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
                {
                }
                finally
                {
                    queue.Writer.TryComplete();
                }
            }, CancellationToken.None);
            try
            {
                await foreach (ApplicationEvent item in queue.Reader.ReadAllAsync(
                    context.CancellationToken).ConfigureAwait(false))
                {
                    await responseStream.WriteAsync(item).ConfigureAwait(false);
                }

                if (overflow && !context.CancellationToken.IsCancellationRequested)
                {
                    await WriteResyncAsync(
                        responseStream,
                        request,
                        runtime.Store.GetRun(runId),
                        ResyncReason.QueueOverflow).ConfigureAwait(false);
                }
            }
            finally
            {
                await producer.ConfigureAwait(false);
            }
        }
        finally
        {
            runtime.ReleaseEventSubscription();
        }
    }

    private RunRecord Start(string commandId, ManualStartCommand command)
    {
        if (command is null
            || command.InitiationKind is not (
                ManualInitiationKind.CliUserAction
                or ManualInitiationKind.DesktopUserGesture
                or ManualInitiationKind.EvaluationHarness))
        {
            throw new InvalidOperationException(
                "A supported initiation kind and future dispatch deadline are required.");
        }

        RunBinding binding = new(
            Required(command.InstallationSnapshotId?.Value, "installation snapshot ID"),
            Required(command.AnalysisContextId?.Value, "analysis context ID"),
            Required(command.EffectiveScanConfigurationId?.Value, "scan configuration ID"),
            Required(command.ResolvedInputManifestId?.Value, "resolved input manifest ID"));
        ManagedAnalysisOrchestrationRequest? managedRequest = null;
        string runId;
        if (command.AnalysisOrchestrationRequestJson.Length == 0)
        {
            runId = Guid.NewGuid().ToString("N");
        }
        else
        {
            if (command.AnalysisOrchestrationRequestJson.Length > ManagedAnalysisOrchestrationRequest.MaximumRequestBytes)
            {
                throw new InvalidOperationException("The managed analysis request exceeds its IPC admission bound.");
            }
            runId = Required(command.RequestedRunId?.Value, "requested managed analysis run ID");
            managedRequest = JsonSerializer.Deserialize<ManagedAnalysisOrchestrationRequest>(
                command.AnalysisOrchestrationRequestJson.Span, ContractJsonSerializer.Options)
                ?? throw new InvalidOperationException("The managed analysis request is malformed.");
            ManagedAnalysisOrchestrator.Validate(managedRequest, runId, binding);
        }
        RunRecord run = managedRequest is null
            ? runtime.Store.CreateRun(
                commandId, runId, binding, runtime.Authority.FencingEpoch, DateTimeOffset.UtcNow,
                command.InitiationKind.ToString(), FromProto(command.DispatchDeadline))
            : executor.CreateManagedAnalysisRun(
                commandId, runId, binding, managedRequest, command.InitiationKind.ToString(),
                FromProto(command.DispatchDeadline));
        executor.Schedule(run.RunId);
        return run;
    }

    private static ManagedMo2SnapshotCaptureAssignment ValidateSnapshotSelection(
        Mo2SnapshotCaptureSelection? selection)
    {
        if (selection is null
            || selection.QualifiedMappings.Count > 32
            || selection.EnabledMapperSha256.Count > 32)
        {
            throw new InvalidOperationException(
                "A bounded MO2 snapshot selection is required.");
        }

        string profileName = Required(selection.SelectedProfileName, "selected profile name");
        if (profileName is "." or ".."
            || profileName.IndexOfAny(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ':']) >= 0)
        {
            throw new InvalidOperationException(
                "The selected profile name is not a single safe component.");
        }

        ManagedQualifiedMappingAssignment[] mappings =
            selection.QualifiedMappings.Select(mapping =>
                new ManagedQualifiedMappingAssignment(
                    Required(mapping.MappingId, "mapping ID"),
                    RequiredAbsolutePath(mapping.SourceRoot, "mapping source root"),
                    Required(mapping.VirtualPrefix, "mapping virtual prefix"),
                    RequiredSha256(mapping.MapperSha256, "mapper SHA-256")))
            .ToArray();
        string[] enabledMappers = selection.EnabledMapperSha256
            .Select(value => RequiredSha256(value, "enabled mapper SHA-256"))
            .ToArray();
        if (mappings.Select(mapping => mapping.MappingId)
                .Distinct(StringComparer.Ordinal).Count() != mappings.Length
            || enabledMappers.Distinct(StringComparer.Ordinal).Count()
                != enabledMappers.Length)
        {
            throw new InvalidOperationException(
                "Snapshot mapping identities must be unique.");
        }

        return new ManagedMo2SnapshotCaptureAssignment(
            RequiredAbsolutePath(selection.Mo2ExecutablePath, "MO2 executable path"),
            RequiredAbsolutePath(selection.InstanceRoot, "MO2 instance root"),
            RequiredAbsolutePath(selection.InstanceIniPath, "MO2 instance INI path"),
            RequiredAbsolutePath(selection.ProfilesRoot, "MO2 profiles root"),
            RequiredAbsolutePath(selection.ModsRoot, "MO2 mods root"),
            RequiredAbsolutePath(selection.OverwriteRoot, "MO2 overwrite root"),
            RequiredAbsolutePath(selection.GameDataRoot, "game Data root"),
            RequiredAbsolutePath(selection.SkyrimExecutablePath, "Skyrim executable path"),
            profileName,
            Required(selection.Platform, "runtime platform"),
            Required(selection.DistributionChannel, "runtime distribution channel"),
            Required(selection.ApplicationId, "runtime application ID"),
            mappings,
            enabledMappers);
    }

    private static string RequiredAbsolutePath(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 32_767
            || !Path.IsPathFullyQualified(value)
            || value.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException($"A bounded absolute {field} is required.");
        }

        return Path.GetFullPath(value);
    }

    private static string RequiredSha256(string? value, string field)
    {
        if (value is null
            || value.Length != 64
            || value.Any(ch => ch is not (
                >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F')))
        {
            throw new ArgumentException($"A canonical {field} is required.");
        }

        return value.ToLowerInvariant();
    }

    private bool DurableCommandExists(string commandId)
    {
        try
        {
            _ = runtime.Store.GetDurableCommand(commandId);
            return true;
        }
        catch (KeyNotFoundException)
        {
            return false;
        }
    }

    private RunRecord Pause(string commandId, PauseCommand command)
    {
        string runId = Required(command.RunId?.Value, "run ID");
        RunRecord pausing = runtime.Store.Transition(
            commandId,
            runId,
            checked((long)command.ExpectedLifecycleGeneration),
            DomainLifecycleState.Pausing,
            runtime.Authority.FencingEpoch,
            "pause requested",
            DateTimeOffset.UtcNow,
            Infinium.Domain.Contracts.LifecycleTransitionRecordKind.Requested);
        return ObserveSafeBoundaryWhenIdle(
            commandId,
            pausing,
            DomainLifecycleState.Paused,
            "paused-at-safe-boundary",
            "pause observed with no dispatched work");
    }

    private RunRecord Resume(string commandId, ResumeCommand command)
    {
        string runId = Required(command.RunId?.Value, "run ID");
        RunRecord run = runtime.Store.Transition(
            commandId,
            runId,
            checked((long)command.ExpectedLifecycleGeneration),
            DomainLifecycleState.Queued,
            runtime.Authority.FencingEpoch,
            "resume requested",
            DateTimeOffset.UtcNow);
        executor.Schedule(runId);
        return run;
    }

    private RunRecord Cancel(string commandId, CancelCommand command)
    {
        string runId = Required(command.RunId?.Value, "run ID");
        RunRecord cancelling = runtime.Store.Transition(
            commandId,
            runId,
            checked((long)command.ExpectedLifecycleGeneration),
            DomainLifecycleState.Cancelling,
            runtime.Authority.FencingEpoch,
            "cancellation requested",
            DateTimeOffset.UtcNow,
            Infinium.Domain.Contracts.LifecycleTransitionRecordKind.Requested);
        return ObserveSafeBoundaryWhenIdle(
            commandId,
            cancelling,
            DomainLifecycleState.Cancelled,
            "cancelled-at-safe-boundary",
            "cancellation observed with no dispatched work");
    }

    private RunRecord ObserveSafeBoundaryWhenIdle(
        string commandId,
        RunRecord requested,
        DomainLifecycleState observedState,
        string attemptOutcome,
        string reason)
    {
        DomainLifecycleState requestedState = observedState == DomainLifecycleState.Paused
            ? DomainLifecycleState.Pausing
            : DomainLifecycleState.Cancelling;
        if (requested.State != requestedState
            || runtime.Store.HasLiveAttempts(requested.RunId))
        {
            return requested;
        }

        runtime.Store.SettleLiveAttempts(
            requested.RunId,
            attemptOutcome,
            runtime.Authority.FencingEpoch);
        return runtime.Store.Transition(
            commandId + "-observed",
            requested.RunId,
            requested.Generation,
            observedState,
            runtime.Authority.FencingEpoch,
            reason,
            DateTimeOffset.UtcNow);
    }

    private HandshakeResponse BuildHandshake(
        ApplicationHandshakeRequest request,
        ServerCallContext context)
    {
        HandshakeResponse response = new()
        {
            NegotiatedProtocol = ProtocolConstants.Version,
            Compatibility = ProtocolConstants.Compatibility,
            CoordinatorInstanceId = new CoordinatorInstanceId { Value = runtime.Authority.InstanceId },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            BoundEndpointRole = EndpointRole.ApplicationClient,
            Limits = ProtocolConstants.Limits,
        };
        if (!IsExpectedEndpoint(context))
        {
            response.Disposition = HandshakeDisposition.WrongEndpoint;
            response.Failure = Failure(
                FailureCode.Unauthorized,
                "The service is bound to another pipe role.");
            return response;
        }

        if (request.SupportedProtocol?.Major != ProtocolConstants.Major)
        {
            response.Disposition = HandshakeDisposition.IncompatibleMajor;
            response.Failure = Failure(FailureCode.IncompatibleVersion, "Protocol major is incompatible.");
            return response;
        }

        if (request.ClientKind is not (
            ApplicationClientKind.Cli
            or ApplicationClientKind.DesktopHost
            or ApplicationClientKind.TestHarness))
        {
            response.Disposition = HandshakeDisposition.UnsupportedCapability;
            response.Failure = Failure(
                FailureCode.Unsupported,
                "The application client kind is unsupported.");
            return response;
        }

        if (request.SupportedProtocol.MinimumMinor > ProtocolConstants.Minor
            || request.SupportedProtocol.MaximumMinor < ProtocolConstants.Minor)
        {
            response.Disposition = HandshakeDisposition.IncompatibleMinor;
            response.Failure = Failure(FailureCode.IncompatibleVersion, "Protocol minor is incompatible.");
            return response;
        }

        if (request.Compatibility?.ApplicationContract?.Value != ProtocolConstants.ContractVersion
            || request.Compatibility.DomainContract?.Value != ProtocolConstants.ContractVersion
            || request.Compatibility.StorageContract?.Value != ProtocolConstants.ContractVersion)
        {
            response.Disposition = HandshakeDisposition.IncompatibleApplicationContract;
            response.Failure = Failure(FailureCode.IncompatibleVersion, "Contract compatibility is invalid.");
            return response;
        }

        if (!request.CoordinatorInstanceNonce.Span.SequenceEqual(runtime.Descriptor.GetNonce()))
        {
            response.Disposition = HandshakeDisposition.InvalidNonce;
            response.Failure = Failure(FailureCode.Unauthenticated, "The coordinator nonce is invalid.");
            return response;
        }

        if (request.RequestedCapabilities.Count > ProtocolConstants.MaximumCapabilityFlags)
        {
            response.Disposition = HandshakeDisposition.LimitsRejected;
            response.Failure = Failure(FailureCode.LimitExceeded, "Too many capability flags were requested.");
            return response;
        }

        Capability[] granted =
        [
            Capability.ApplicationQuery,
            Capability.DurableCommand,
            Capability.EventStream,
            Capability.KeysetCursor,
        ];
        if (request.RequestedCapabilities.Any(capability => !granted.Contains(capability)))
        {
            response.Disposition = HandshakeDisposition.UnsupportedCapability;
            response.Failure = Failure(FailureCode.Unsupported, "A requested capability is unsupported.");
            return response;
        }

        response.Disposition = HandshakeDisposition.Accepted;
        response.GrantedCapabilities.Add(granted);
        return response;
    }

    private void RequireNegotiated(ServerCallContext context)
    {
        if (!IsExpectedEndpoint(context)
            || !runtime.IsApplicationConnectionAdmitted(context.GetHttpContext().Connection.Id))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Negotiate first."));
        }
    }

    private bool IsExpectedEndpoint(ServerCallContext? request)
    {
        if (request is null)
        {
            return true;
        }

        InfiniumPipeRoleFeature? feature =
            request.GetHttpContext().Features.Get<InfiniumPipeRoleFeature>();
        return feature is not null
            && string.Equals(feature.Role, "application", StringComparison.Ordinal)
            && string.Equals(
                feature.PipeName,
                runtime.Descriptor.ApplicationPipe,
                StringComparison.Ordinal);
    }

    private static Failure Failure(FailureCode code, string detail) =>
        new() { Code = code, Detail = Bounded(detail) };

    private static ListRunsResponse CursorRejected(
        CursorDisposition disposition,
        string detail) =>
        new()
        {
            CursorRejection = new CursorRejection
            {
                Disposition = disposition,
                CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
                Failure = Failure(FailureCode.ResyncRequired, detail),
            },
        };

    private ByteString EncodeRunCursor(RunPageCursor cursor)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(cursor);
        byte[] mac = HMACSHA256.HashData(runtime.Descriptor.GetNonce(), payload);
        byte[] value = new byte[mac.Length + payload.Length];
        mac.CopyTo(value, 0);
        payload.CopyTo(value, mac.Length);
        return ByteString.CopyFrom(value);
    }

    private ApplicationEvent ProgressEvent(
        SubscribeEventsRequest request,
        RunRecord run)
    {
        EventStreamCursor cursor = new(
            runtime.Authority.InstanceId,
            runtime.Authority.FencingEpoch,
            run.RunId,
            run.DurableSequence,
            "1",
            DateTimeOffset.UtcNow.AddMinutes(5));
        return new ApplicationEvent
        {
            CoordinatorInstanceId = new CoordinatorInstanceId { Value = runtime.Authority.InstanceId },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            SubscriptionId = request.SubscriptionId,
            DurableEventSequence = checked((ulong)run.DurableSequence),
            ProjectionVersion = new ProjectionVersion { Value = "1" },
            Kind = EventKind.Progress,
            RunScope = new RunId { Value = run.RunId },
            ResumeCursor = new EventCursor { OpaqueValue = EncodeEventCursor(cursor) },
            Progress = new ProgressSnapshot
            {
                RunId = new RunId { Value = run.RunId },
                LifecycleState = ProtoMapping.ToProto(run.State),
                Progress = ProtoMapping.EmptyProgress(run.State),
                ProjectionVersion = new ProjectionVersion { Value = "1" },
                DurableEventSequence = checked((ulong)run.DurableSequence),
                ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
            },
        };
    }

    private async Task WriteResyncAsync(
        IServerStreamWriter<ApplicationEvent> responseStream,
        SubscribeEventsRequest request,
        RunRecord run,
        ResyncReason reason)
    {
        await responseStream.WriteAsync(new ApplicationEvent
        {
            CoordinatorInstanceId = new CoordinatorInstanceId { Value = runtime.Authority.InstanceId },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            SubscriptionId = request.SubscriptionId,
            DurableEventSequence = checked((ulong)run.DurableSequence),
            ProjectionVersion = new ProjectionVersion { Value = "1" },
            Kind = EventKind.ResyncRequired,
            RunScope = new RunId { Value = run.RunId },
            ResyncRequired = new ResyncRequired
            {
                Reason = reason,
                CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
            },
        }).ConfigureAwait(false);
    }

    private ByteString EncodeEventCursor(EventStreamCursor cursor) =>
        EncodeAuthenticated(JsonSerializer.SerializeToUtf8Bytes(cursor));

    private EventStreamCursor DecodeEventCursor(ByteString opaque)
    {
        byte[] payload = DecodeAuthenticated(opaque);
        EventStreamCursor cursor = JsonSerializer.Deserialize<EventStreamCursor>(payload)
            ?? throw new InvalidOperationException("The event cursor is malformed.");
        return cursor;
    }

    private ByteString EncodeAuthenticated(byte[] payload)
    {
        byte[] mac = HMACSHA256.HashData(runtime.Descriptor.GetNonce(), payload);
        byte[] value = new byte[mac.Length + payload.Length];
        mac.CopyTo(value, 0);
        payload.CopyTo(value, mac.Length);
        return ByteString.CopyFrom(value);
    }

    private byte[] DecodeAuthenticated(ByteString opaque)
    {
        if (opaque.Length is <= 32 or > 1024)
        {
            throw new InvalidOperationException("The authenticated cursor has an invalid size.");
        }

        ReadOnlySpan<byte> bytes = opaque.Span;
        ReadOnlySpan<byte> suppliedMac = bytes[..32];
        byte[] payload = bytes[32..].ToArray();
        byte[] expectedMac = HMACSHA256.HashData(runtime.Descriptor.GetNonce(), payload);
        if (!CryptographicOperations.FixedTimeEquals(suppliedMac, expectedMac))
        {
            throw new InvalidOperationException("The cursor authentication failed.");
        }

        return payload;
    }

    private bool IsCursorFromAnotherCoordinator(ByteString opaque)
    {
        if (opaque.Length is <= 32 or > 1024)
        {
            return false;
        }

        try
        {
            EventStreamCursor? untrusted =
                JsonSerializer.Deserialize<EventStreamCursor>(opaque.Span[32..]);
            return untrusted is not null
                && !string.IsNullOrWhiteSpace(untrusted.CoordinatorInstanceId)
                && !string.Equals(
                    untrusted.CoordinatorInstanceId,
                    runtime.Authority.InstanceId,
                    StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsCurrentProjection(ProjectionVersion? expected) =>
        expected is null || expected.Value is "" or "1";

    private static ProjectionInvalidated ProjectionInvalidated() => new()
    {
        CurrentProjectionVersion = new ProjectionVersion { Value = "1" },
        Reason = ResyncReason.ProjectionRebuilt,
    };

    private RunPageCursor DecodeRunCursor(ByteString opaque)
    {
        if (opaque.Length is <= 32 or > 512)
        {
            throw new InvalidOperationException("The run cursor has an invalid size.");
        }

        ReadOnlySpan<byte> bytes = opaque.Span;
        ReadOnlySpan<byte> suppliedMac = bytes[..32];
        ReadOnlySpan<byte> payload = bytes[32..];
        byte[] expectedMac = HMACSHA256.HashData(runtime.Descriptor.GetNonce(), payload);
        if (!CryptographicOperations.FixedTimeEquals(suppliedMac, expectedMac))
        {
            throw new InvalidOperationException("The run cursor authentication failed.");
        }

        RunPageCursor cursor = JsonSerializer.Deserialize<RunPageCursor>(payload)
            ?? throw new InvalidOperationException("The run cursor payload is malformed.");
        if (string.IsNullOrWhiteSpace(cursor.RunId)
            || cursor.RunId.Length > 128
            || cursor.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("The run cursor is expired or malformed.");
        }

        return cursor;
    }

    private static string Required(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 128
            ? throw new ArgumentException($"A bounded {field} is required.")
            : value;

    private static string Bounded(string value) =>
        value.Length <= 512 ? value : value[..512];

    private static DateTimeOffset FromProto(Instant? value)
    {
        if (value is null
            || value.Nanoseconds is < 0 or > 999_999_999)
        {
            throw new InvalidOperationException("The dispatch deadline is malformed.");
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value.UnixSeconds)
                .AddTicks(value.Nanoseconds / 100);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidOperationException(
                "The dispatch deadline is outside the supported range.",
                exception);
        }
    }

    private static ReplaySummary ToProto(AnalysisSummaryPersistenceRecord value) => new()
    {
        ReplayManifestId = new ReplayManifestId { Value = value.ReplayManifestId },
        ReplayState = ReplayState(value.ReplayState),
        AuditabilityState = AuditabilityState(value.AuditabilityState),
        SemanticallyEquivalent = value.SemanticallyEquivalent,
        DependencyCount = checked((ulong)value.DependencyCount),
        MissingDependencyCount = checked((ulong)value.MissingDependencyCount),
        CoverageGapCount = checked((ulong)value.CoverageGapCount),
    };

    private static ReplaySummary ToProto(AnalysisReplayPersistenceRecord value) => new()
    {
        ReplayManifestId = new ReplayManifestId { Value = value.ReplayManifestId },
        ReplayState = ReplayState(value.ReplayState),
        AuditabilityState = AuditabilityState(value.AuditabilityState),
        SemanticallyEquivalent = value.SemanticallyEquivalent,
        DependencyCount = checked((ulong)value.DependencyCount),
        MissingDependencyCount = checked((ulong)value.MissingDependencyCount),
        CoverageGapCount = checked((ulong)value.CoverageGapCount),
    };

    private static AnalysisArtifactReference ToProto(AnalysisArtifactPersistenceRecord value) => new()
    {
        ArtifactId = new AnalysisArtifactId { Value = value.ArtifactId },
        Kind = value.Kind switch
        {
            "documentation-evidence" => AnalysisArtifactKind.DocumentationEvidence,
            "candidate-analysis" => AnalysisArtifactKind.CandidateAnalysis,
            "finding-case" => AnalysisArtifactKind.FindingCase,
            "analysis-replay" => AnalysisArtifactKind.AnalysisReplay,
            "analysis-execution-input" => AnalysisArtifactKind.AnalysisExecutionInput,
            _ => AnalysisArtifactKind.Unknown,
        },
        SchemaId = value.SchemaId,
        SchemaVersion = new SemanticVersion { Value = value.SchemaVersion },
        Revision = checked((ulong)value.Revision),
        State = ProtoState(value.State),
        ContentDigest = new ContentDigest
        {
            Algorithm = DigestAlgorithm.Sha256,
            Value = ByteString.CopyFrom(Convert.FromHexString(value.ContentSha256)),
            SizeBytes = checked((ulong)value.ByteLength),
        },
        ByteLength = checked((ulong)value.ByteLength),
        ProvenanceId = new AnalysisArtifactId { Value = value.ProvenanceId },
        DependencyClosureId = new AnalysisArtifactId { Value = value.DependencyClosureId },
    };

    private static string KindToken(AnalysisArtifactKind value) => value switch
    {
        AnalysisArtifactKind.DocumentationEvidence => "documentation-evidence",
        AnalysisArtifactKind.CandidateAnalysis => "candidate-analysis",
        AnalysisArtifactKind.FindingCase => "finding-case",
        AnalysisArtifactKind.AnalysisReplay => "analysis-replay",
        AnalysisArtifactKind.AnalysisExecutionInput => "analysis-execution-input",
        _ => throw new ArgumentException("Analysis artifact kind is not queryable."),
    };

    private static string StateToken(AnalysisArtifactState value) =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());

    private static AnalysisArtifactState ProtoState(string value) => value switch
    {
        "present" => AnalysisArtifactState.Present,
        "resolved-negative" => AnalysisArtifactState.ResolvedNegative,
        "missing" => AnalysisArtifactState.Missing,
        "invalid-input" => AnalysisArtifactState.InvalidInput,
        "unsupported" => AnalysisArtifactState.Unsupported,
        "ambiguous" => AnalysisArtifactState.Ambiguous,
        "partial" => AnalysisArtifactState.Partial,
        "abstained" => AnalysisArtifactState.Abstained,
        "not-applicable" => AnalysisArtifactState.NotApplicable,
        "not-used" => AnalysisArtifactState.NotUsed,
        "failed" => AnalysisArtifactState.Failed,
        "cancelled" => AnalysisArtifactState.Cancelled,
        "limit-reached" => AnalysisArtifactState.LimitReached,
        "unavailable" => AnalysisArtifactState.Unavailable,
        _ => AnalysisArtifactState.Unknown,
    };

    private static Infinium.Contracts.Protobuf.Domain.V1.ReplayState ReplayState(string value) => value switch
    {
        "complete-clean" => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.CompleteClean,
        "partial" => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.Partial,
        "audit-only" => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.AuditOnly,
        "unavailable" => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.Unavailable,
        "failed-identity-drift" => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.FailedIdentityDrift,
        _ => Infinium.Contracts.Protobuf.Domain.V1.ReplayState.Unknown,
    };

    private static Infinium.Contracts.Protobuf.Domain.V1.AuditabilityState AuditabilityState(string value) => value switch
    {
        "complete" => Infinium.Contracts.Protobuf.Domain.V1.AuditabilityState.Complete,
        "partial" => Infinium.Contracts.Protobuf.Domain.V1.AuditabilityState.CompleteWithGaps,
        "unavailable" => Infinium.Contracts.Protobuf.Domain.V1.AuditabilityState.Unavailable,
        _ => Infinium.Contracts.Protobuf.Domain.V1.AuditabilityState.Unknown,
    };

    private static AnalysisArtifactSortOrder ArtifactSort(AnalysisArtifactSort value) => value switch
    {
        AnalysisArtifactSort.Unspecified or AnalysisArtifactSort.IdentityAscending =>
            AnalysisArtifactSortOrder.IdentityAscending,
        AnalysisArtifactSort.RankDescendingIdentityAscending =>
            AnalysisArtifactSortOrder.RankDescendingIdentityAscending,
        AnalysisArtifactSort.UpdatedTickDescendingIdentityDescending =>
            AnalysisArtifactSortOrder.UpdatedTickDescendingIdentityDescending,
        _ => throw new ArgumentException("The analysis artifact sort is not supported."),
    };

    private static string QueryHash(
        IReadOnlyList<string> kinds,
        IReadOnlyList<string> states) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            string.Join('\n', string.Join(',', kinds), string.Join(',', states)))));

    private sealed record RunPageCursor(
        DateTimeOffset CreatedAt,
        string RunId,
        DateTimeOffset ExpiresAt);

    private sealed record EventStreamCursor(
        string CoordinatorInstanceId,
        long CoordinatorFencingEpoch,
        string RunId,
        long DurableSequence,
        string ProjectionVersion,
        DateTimeOffset ExpiresAt);

    private sealed class AnalysisCursorException(CursorDisposition disposition, string message)
        : InvalidOperationException(message)
    {
        public CursorDisposition Disposition { get; } = disposition;
    }
}
