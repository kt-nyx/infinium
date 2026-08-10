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
    public override Task<SubmitRunCommandResponse> SubmitRunCommand(
        SubmitRunCommandRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            string commandId = Required(request.IdempotencyKey?.Value, "durable command ID");
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
            DurableCommandRecord durable = runtime.Store.GetDurableCommand(commandId);
            SubmitRunCommandResponse response = new()
            {
                Disposition = replay
                    ? CommandDisposition.AlreadyAccepted
                    : CommandDisposition.Accepted,
                DurableCommandId = new DurableCommandId { Value = commandId },
                RunId = new RunId { Value = result.RunId },
            };
            if (durable.TransitionId is not null)
            {
                response.DurableTransitionId =
                    new DurableTransitionId { Value = durable.TransitionId };
            }

            return Task.FromResult(response);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or InvalidDataException
                or JsonException
                or KeyNotFoundException)
        {
            return Task.FromResult(new SubmitRunCommandResponse
            {
                Disposition = CommandDisposition.Rejected,
                Failure = Failure(exception is InvalidDataException or JsonException
                    ? FailureCode.InvalidArgument : FailureCode.Conflict, Bounded(exception.Message)),
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
                AcceptedInput = new DurableCommandInputIdentity
                {
                    CommandKind = command.CommandKind switch
                    {
                        "start" => DurableCommandKind.Start,
                        "pausing" => DurableCommandKind.Pause,
                        "queued" => DurableCommandKind.Resume,
                        "cancelling" => DurableCommandKind.Cancel,
                        _ => DurableCommandKind.Unknown,
                    },
                    ExpectedLifecycleGeneration =
                        checked((ulong)command.ExpectedGeneration),
                    InstallationSnapshotId = new InstallationSnapshotId
                    {
                        Value = command.RunBinding.InstallationSnapshotId,
                    },
                    AnalysisContextId = new AnalysisContextId
                    {
                        Value = command.RunBinding.AnalysisContextId,
                    },
                    EffectiveScanConfigurationId = new ScanConfigurationId
                    {
                        Value = command.RunBinding.EffectiveScanConfigurationId,
                    },
                    ResolvedInputManifestId = new ResolvedInputManifestId
                    {
                        Value = command.RunBinding.ResolvedInputManifestId,
                    },
                },
            };
            if (command.TransitionId is not null)
            {
                status.DurableTransitionId =
                    new DurableTransitionId { Value = command.TransitionId };
            }
            if (command.StartInitiationKind is not null
                && command.StartDispatchDeadline is not null)
            {
                status.AcceptedInput.ManualInitiationKind =
                    Enum.Parse<ManualInitiationKind>(command.StartInitiationKind);
                status.AcceptedInput.DispatchDeadline =
                    ProtoMapping.ToProto(command.StartDispatchDeadline.Value);
                RunOperationRecord? operation = runtime.Store.GetRunOperation(command.RunId);
                if (operation?.OperationKind == "managed-analysis-v1")
                {
                    status.AcceptedInput.RequestedRunId = new RunId { Value = command.RunId };
                    status.AcceptedInput.AnalysisOrchestrationRequest = new ContentDigest
                    {
                        Algorithm = DigestAlgorithm.Sha256,
                        Value = ByteString.CopyFrom(Convert.FromHexString(operation.RequestSha256)),
                        SizeBytes = checked((ulong)Encoding.UTF8.GetByteCount(operation.RequestJson)),
                    };
                }
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

    public override Task<SubmitSnapshotCaptureResponse> SubmitSnapshotCapture(
        SubmitSnapshotCaptureRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            string commandId = Required(
                request.IdempotencyKey?.Value,
                "durable command ID");
            SnapshotCaptureOperationRecord? replay =
                runtime.Store.FindSnapshotCaptureByCommand(commandId);
            if (replay is null
                && !runtime.TryAdmitNewDurableCommand(DateTimeOffset.UtcNow))
            {
                return Task.FromResult(new SubmitSnapshotCaptureResponse
                {
                    Disposition = CommandDisposition.Rejected,
                    Failure = Failure(
                        FailureCode.LimitExceeded,
                        "The new durable-command rate bound is full."),
                });
            }

            ManagedMo2SnapshotCaptureAssignment selection =
                ValidateSnapshotSelection(request.Selection);
            string requestJson = JsonSerializer.Serialize(selection);
            string requestSha256 = Convert.ToHexString(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(requestJson)))
                .ToLowerInvariant();
            if (request.InitiationKind is not (
                    ManualInitiationKind.CliUserAction
                    or ManualInitiationKind.DesktopUserGesture
                    or ManualInitiationKind.EvaluationHarness))
            {
                throw new InvalidOperationException(
                    "A supported explicit initiation kind is required.");
            }

            string operationId = Guid.NewGuid().ToString("N");
            SnapshotCaptureOperationRecord operation =
                runtime.Store.CreateSnapshotCaptureOperation(
                    commandId,
                    operationId,
                    requestJson,
                    requestSha256,
                    request.InitiationKind.ToString(),
                    FromProto(request.DispatchDeadline),
                    runtime.Authority.FencingEpoch,
                    DateTimeOffset.UtcNow);
            snapshotExecutor.Schedule(operation.OperationId);
            return Task.FromResult(new SubmitSnapshotCaptureResponse
            {
                Disposition = replay is null
                    ? CommandDisposition.Accepted
                    : CommandDisposition.AlreadyAccepted,
                DurableCommandId = new DurableCommandId { Value = commandId },
                OperationId = new SnapshotCaptureOperationId
                {
                    Value = operation.OperationId,
                },
            });
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or KeyNotFoundException)
        {
            return Task.FromResult(new SubmitSnapshotCaptureResponse
            {
                Disposition = CommandDisposition.Rejected,
                Failure = Failure(FailureCode.Conflict, Bounded(exception.Message)),
            });
        }
    }

    public override Task<GetSnapshotCaptureResponse> GetSnapshotCapture(
        GetSnapshotCaptureRequest request,
        ServerCallContext context)
    {
        RequireNegotiated(context);
        try
        {
            SnapshotCaptureOperationRecord operation =
                request.IdentityCase switch
                {
                    GetSnapshotCaptureRequest.IdentityOneofCase.OperationId =>
                        runtime.Store.GetSnapshotCaptureOperation(
                            Required(
                                request.OperationId?.Value,
                                "snapshot capture operation ID")),
                    GetSnapshotCaptureRequest.IdentityOneofCase.DurableCommandId =>
                        runtime.Store.FindSnapshotCaptureByCommand(
                            Required(
                                request.DurableCommandId?.Value,
                                "durable command ID"))
                        ?? throw new KeyNotFoundException(),
                    _ => throw new ArgumentException(
                        "A snapshot capture operation or durable command identity is required."),
                };
            SnapshotCaptureStatus status = new()
            {
                OperationId = new SnapshotCaptureOperationId
                {
                    Value = operation.OperationId,
                },
                DurableCommandId = new DurableCommandId
                {
                    Value = operation.DurableCommandId,
                },
                LifecycleState = operation.State switch
                {
                    "Queued" => SnapshotCaptureLifecycleState.Queued,
                    "Running" => SnapshotCaptureLifecycleState.Running,
                    "Completed" => SnapshotCaptureLifecycleState.Completed,
                    "Failed" => SnapshotCaptureLifecycleState.Failed,
                    _ => SnapshotCaptureLifecycleState.Unknown,
                },
                LifecycleGeneration = checked((ulong)operation.Generation),
                RequestSha256 = operation.RequestSha256,
                CreatedAt = ProtoMapping.ToProto(operation.CreatedAt),
                UpdatedAt = ProtoMapping.ToProto(operation.UpdatedAt),
            };
            if (operation.InstallationSnapshotId is not null)
            {
                status.InstallationSnapshotId = new InstallationSnapshotId
                {
                    Value = operation.InstallationSnapshotId,
                };
            }
            if (operation.PayloadId is not null)
            {
                status.PayloadId = new PayloadId { Value = operation.PayloadId };
            }

            return Task.FromResult(new GetSnapshotCaptureResponse { Status = status });
        }
        catch (KeyNotFoundException)
        {
            return Task.FromResult(new GetSnapshotCaptureResponse
            {
                Failure = Failure(
                    FailureCode.NotFound,
                    "The snapshot capture operation does not exist."),
            });
        }
        catch (ArgumentException exception)
        {
            return Task.FromResult(new GetSnapshotCaptureResponse
            {
                Failure = Failure(
                    FailureCode.InvalidArgument,
                    Bounded(exception.Message)),
            });
        }
    }

}
