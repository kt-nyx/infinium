using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Persistence;
using DomainLifecycleState = Infinium.Domain.Contracts.LifecycleState;

namespace Infinium.Coordinator;

public sealed class ApplicationGrpcService(
    CoordinatorRuntime runtime,
    ManagedRunExecutor executor)
    : ApplicationService.ApplicationServiceBase
{
    public override Task<HandshakeResponse> Negotiate(
        ApplicationHandshakeRequest request,
        ServerCallContext context)
    {
        HandshakeResponse response = BuildHandshake(request, context);
        if (response.Disposition == HandshakeDisposition.Accepted)
        {
            runtime.AdmitApplicationConnection(context.GetHttpContext().Connection.Id);
        }

        return Task.FromResult(response);
    }

    public override Task<HealthResponse> Health(HealthRequest request, ServerCallContext context)
    {
        RequireNegotiated(context);
        return Task.FromResult(new HealthResponse
        {
            State = HealthState.Healthy,
            CoordinatorInstanceId = new CoordinatorInstanceId
            {
                Value = runtime.Authority.InstanceId,
            },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
        });
    }

    public override Task<ListRunsResponse> ListRuns(
        ListRunsRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (request.RequestedPageSize == 0
            || request.RequestedPageSize > ProtocolConstants.MaximumPageItems
            || request.Sort.Count > ProtocolConstants.MaximumSortTerms
            || (request.Filter?.LifecycleStates.Count ?? 0) > ProtocolConstants.MaximumFilterTerms)
        {
            return Task.FromResult(new ListRunsResponse
            {
                Failure = Failure(FailureCode.LimitExceeded, "The query exceeds a finite protocol bound."),
            });
        }

        if ((request.Filter?.LifecycleStates.Count ?? 0) > 0
            || (request.Filter?.CoverageStates.Count ?? 0) > 0
            || request.Filter?.CreatedAtOrAfter is not null
            || request.Filter?.CreatedBefore is not null
            || request.Sort.Count > 0)
        {
            return Task.FromResult(new ListRunsResponse
            {
                Failure = Failure(
                    FailureCode.Unsupported,
                    "Slice 2 supports the deterministic created-at ascending run query only."),
            });
        }

        if (request.ExpectedProjectionVersion is not null
            && request.ExpectedProjectionVersion.Value is not ("" or "1"))
        {
            return Task.FromResult(CursorRejected(
                CursorDisposition.ProjectionInvalidated,
                "The requested projection is no longer current."));
        }

        RunPageCursor? cursor = null;
        if (request.After is not null && request.After.OpaqueValue.Length > 0)
        {
            try
            {
                cursor = DecodeRunCursor(request.After.OpaqueValue);
            }
            catch (InvalidOperationException exception)
            {
                return Task.FromResult(CursorRejected(
                    CursorDisposition.Malformed,
                    Bounded(exception.Message)));
            }
        }

        int requested = checked((int)request.RequestedPageSize);
        IReadOnlyList<RunRecord> runs = runtime.Store.ListRuns(
            requested + 1,
            cursor?.CreatedAt,
            cursor?.RunId);
        bool hasMore = runs.Count > requested;
        IReadOnlyList<RunRecord> items = hasMore ? runs.Take(requested).ToArray() : runs;
        RunPage page = new()
        {
            HasMore = hasMore,
            ProjectionVersion = new ProjectionVersion { Value = "1" },
        };
        page.Items.Add(items.Select(ProtoMapping.ToSummary));
        if (hasMore)
        {
            RunRecord last = items[^1];
            page.Next = new PageCursor
            {
                OpaqueValue = EncodeRunCursor(new RunPageCursor(
                    last.CreatedAt,
                    last.RunId,
                    DateTimeOffset.UtcNow.AddMinutes(5))),
            };
        }

        return Task.FromResult(new ListRunsResponse { Page = page });
    }

    public override Task<GetRunResponse> GetRun(GetRunRequest request, ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            RunRecord run = runtime.Store.GetRun(Required(request.RunId?.Value, "run ID"));
            return Task.FromResult(new GetRunResponse { Run = ProtoMapping.ToDetail(run) });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetRunResponse
            {
                Failure = Failure(FailureCode.NotFound, "The requested run does not exist."),
            });
        }
    }

    public override Task<GetProgressResponse> GetProgress(
        GetProgressRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            RunRecord run = runtime.Store.GetRun(Required(request.RunId?.Value, "run ID"));
            return Task.FromResult(new GetProgressResponse
            {
                Progress = new ProgressSnapshot
                {
                    RunId = new RunId { Value = run.RunId },
                    LifecycleState = ProtoMapping.ToProto(run.State),
                    Progress = ProtoMapping.EmptyProgress(run.State),
                    ProjectionVersion = new ProjectionVersion { Value = "1" },
                    DurableEventSequence = checked((ulong)run.DurableSequence),
                    ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
                },
            });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetProgressResponse
            {
                Failure = Failure(FailureCode.NotFound, "The requested run does not exist."),
            });
        }
    }

    public override Task<ListFindingsResponse> ListFindings(
        ListFindingsRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        if (request.RequestedPageSize == 0
            || request.RequestedPageSize > ProtocolConstants.MaximumPageItems)
        {
            return Task.FromResult(new ListFindingsResponse
            {
                Failure = Failure(FailureCode.LimitExceeded, "The page size exceeds its finite bound."),
            });
        }

        return Task.FromResult(new ListFindingsResponse
        {
            Page = new FindingPage
            {
                HasMore = false,
                ProjectionVersion = new ProjectionVersion { Value = "1" },
            },
        });
    }

    public override Task<SubmitRunCommandResponse> SubmitRunCommand(
        SubmitRunCommandRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            string commandId = Required(request.IdempotencyKey?.Value, "durable command ID");
            RunRecord result = request.CommandCase switch
            {
                SubmitRunCommandRequest.CommandOneofCase.Start => Start(commandId, request.Start),
                SubmitRunCommandRequest.CommandOneofCase.Pause =>
                    Pause(commandId, request.Pause),
                SubmitRunCommandRequest.CommandOneofCase.Resume =>
                    Resume(commandId, request.Resume),
                SubmitRunCommandRequest.CommandOneofCase.Cancel =>
                    Cancel(commandId, request.Cancel),
                _ => throw new InvalidOperationException("A supported command is required."),
            };
            return Task.FromResult(new SubmitRunCommandResponse
            {
                Disposition = CommandDisposition.Accepted,
                DurableCommandId = new DurableCommandId { Value = commandId },
                RunId = new RunId { Value = result.RunId },
            });
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException)
        {
            return Task.FromResult(new SubmitRunCommandResponse
            {
                Disposition = CommandDisposition.Rejected,
                Failure = Failure(FailureCode.Conflict, Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetDurableCommandResponse> GetDurableCommand(
        GetDurableCommandRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            DurableCommandRecord command = runtime.Store.GetDurableCommand(
                Required(request.DurableCommandId?.Value, "durable command ID"));
            DurableCommandStatus status = new()
            {
                DurableCommandId = new DurableCommandId { Value = command.CommandId },
                Disposition = command.Disposition == "accepted"
                    ? CommandDisposition.Accepted
                    : CommandDisposition.Rejected,
                RunId = new RunId { Value = command.RunId },
                ResultingLifecycleState = ProtoMapping.ToProto(
                    Enum.Parse<DomainLifecycleState>(command.ResultingState)),
                ObservedAt = ProtoMapping.ToProto(command.CreatedAt),
            };
            if (command.TransitionId is not null)
            {
                status.DurableTransitionId =
                    new DurableTransitionId { Value = command.TransitionId };
            }

            return Task.FromResult(new GetDurableCommandResponse { Status = status });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetDurableCommandResponse
            {
                Failure = Failure(FailureCode.NotFound, "The durable command does not exist."),
            });
        }
    }

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

        RunRecord run = runtime.Store.GetRun(Required(request.RunScope[0].Value, "run ID"));
        await responseStream.WriteAsync(new ApplicationEvent
        {
            CoordinatorInstanceId = new CoordinatorInstanceId { Value = runtime.Authority.InstanceId },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            SubscriptionId = request.SubscriptionId,
            DurableEventSequence = checked((ulong)run.DurableSequence),
            ProjectionVersion = new ProjectionVersion { Value = "1" },
            Kind = EventKind.Progress,
            RunScope = new RunId { Value = run.RunId },
            Progress = new ProgressSnapshot
            {
                RunId = new RunId { Value = run.RunId },
                LifecycleState = ProtoMapping.ToProto(run.State),
                Progress = ProtoMapping.EmptyProgress(run.State),
                ProjectionVersion = new ProjectionVersion { Value = "1" },
                DurableEventSequence = checked((ulong)run.DurableSequence),
                ObservedAt = ProtoMapping.ToProto(DateTimeOffset.UtcNow),
            },
        }).ConfigureAwait(false);
    }

    private RunRecord Start(string commandId, ManualStartCommand command)
    {
        if (command is null
            || command.InitiationKind is not (
                ManualInitiationKind.CliUserAction
                or ManualInitiationKind.DesktopUserGesture
                or ManualInitiationKind.EvaluationHarness)
            || FromProto(command.DispatchDeadline) <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException(
                "A supported initiation kind and future dispatch deadline are required.");
        }

        RunBinding binding = new(
            Required(command.InstallationSnapshotId?.Value, "installation snapshot ID"),
            Required(command.AnalysisContextId?.Value, "analysis context ID"),
            Required(command.EffectiveScanConfigurationId?.Value, "scan configuration ID"),
            Required(command.ResolvedInputManifestId?.Value, "resolved input manifest ID"));
        string runId = Guid.NewGuid().ToString("N");
        RunRecord run = runtime.Store.CreateRun(
            commandId,
            runId,
            binding,
            runtime.Authority.FencingEpoch,
            DateTimeOffset.UtcNow);
        executor.Schedule(run.RunId);
        return run;
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
        runtime.Store.SettleLiveAttempts(runId, "paused-at-safe-boundary");
        return runtime.Store.Transition(
            commandId + "-observed",
            runId,
            pausing.Generation,
            DomainLifecycleState.Paused,
            runtime.Authority.FencingEpoch,
            "pause observed at a safe boundary",
            DateTimeOffset.UtcNow);
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
        runtime.Store.SettleLiveAttempts(runId, "cancelled-at-safe-boundary");
        return runtime.Store.Transition(
            commandId + "-observed",
            runId,
            cancelling.Generation,
            DomainLifecycleState.Cancelled,
            runtime.Authority.FencingEpoch,
            "cancellation observed at a safe boundary",
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

    private sealed record RunPageCursor(
        DateTimeOffset CreatedAt,
        string RunId,
        DateTimeOffset ExpiresAt);
}
