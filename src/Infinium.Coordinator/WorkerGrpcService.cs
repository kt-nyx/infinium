using Grpc.Core;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;
using Microsoft.AspNetCore.Connections.Features;

namespace Infinium.Coordinator;

public sealed class WorkerGrpcService(
    CoordinatorRuntime runtime,
    WorkerBootstrapRegistry bootstraps)
    : WorkerService.WorkerServiceBase
{
    public override Task<HandshakeResponse> Negotiate(
        WorkerHandshakeRequest request,
        ServerCallContext context)
    {
        if (!IsWorkerEndpoint(context))
        {
            return Task.FromResult(new HandshakeResponse
            {
                Disposition = HandshakeDisposition.WrongEndpoint,
                BoundEndpointRole = EndpointRole.GeneralWorker,
                Failure = Failure(
                    FailureCode.Unauthorized,
                    "The service is bound to another pipe role."),
            });
        }

        string connectionId = context.GetHttpContext().Connection.Id;
        HandshakeResponse response = bootstraps.Negotiate(
            request,
            connectionId,
            runtime);
        if (response.Disposition == HandshakeDisposition.Accepted)
        {
            if (!runtime.TryAdmitWorkerConnection(connectionId))
            {
                response.Disposition = HandshakeDisposition.LimitsRejected;
                response.Failure = Failure(
                    FailureCode.LimitExceeded,
                    "The worker connection admission bound is full.");
                return Task.FromResult(response);
            }

            IConnectionLifetimeFeature? lifetime =
                context.GetHttpContext().Features.Get<IConnectionLifetimeFeature>();
            lifetime?.ConnectionClosed.Register(
                () => runtime.ReleaseWorkerConnection(connectionId));
        }

        return Task.FromResult(response);
    }

    public override Task<ReceiveAssignmentResponse> ReceiveAssignment(
        ReceiveAssignmentRequest request,
        ServerCallContext context)
    {
        try
        {
            return Task.FromResult(new ReceiveAssignmentResponse
            {
                Assignment = bootstraps.GetAssignment(
                    request,
                    context.GetHttpContext().Connection.Id,
                    runtime),
            });
        }
        catch (InvalidOperationException exception)
        {
            return Task.FromResult(new ReceiveAssignmentResponse
            {
                Failure = Failure(FailureCode.StaleFence, Bounded(exception.Message)),
            });
        }
    }

    public override Task<WorkerProgressReceipt> ReportProgress(
        WorkerProgress request,
        ServerCallContext context)
    {
        return Task.FromResult(bootstraps.AcceptProgress(
            request,
            context.GetHttpContext().Connection.Id,
            runtime));
    }

    public override Task<PollControlResponse> PollControl(
        PollControlRequest request,
        ServerCallContext context)
    {
        WorkerControl control = bootstraps.GetControl(
            request,
            context.GetHttpContext().Connection.Id,
            runtime);
        PollControlResponse response = new()
        {
            Control = control,
            ControlSequence = 1,
        };
        if (control == WorkerControl.StopStaleAttempt)
        {
            response.Failure =
                Failure(FailureCode.StaleFence, "The worker attempt is stale.");
        }

        return Task.FromResult(response);
    }

    public override Task<SubmitStagedOutputResponse> SubmitStagedOutput(
        SubmitStagedOutputRequest request,
        ServerCallContext context)
    {
        try
        {
            StagedOutputAcceptance acceptance = bootstraps.AcceptStagedOutput(
                request,
                context.GetHttpContext().Connection.Id);
            return Task.FromResult(new SubmitStagedOutputResponse
            {
                Disposition = acceptance.Disposition,
                StagingReceiptId = acceptance.ReceiptId,
            });
        }
        catch (InvalidOperationException exception)
        {
            return Task.FromResult(new SubmitStagedOutputResponse
            {
                Disposition = WorkerReceiptDisposition.RejectedMalformed,
                Failure = Failure(FailureCode.InvalidArgument, Bounded(exception.Message)),
            });
        }
    }

    public override Task<WorkerTerminalReceiptResponse> SubmitTerminalReceipt(
        WorkerTerminalReceipt request,
        ServerCallContext context)
    {
        try
        {
            TerminalReceiptAcceptance acceptance = bootstraps.AcceptTerminal(
                request,
                context.GetHttpContext().Connection.Id);
            return Task.FromResult(new WorkerTerminalReceiptResponse
            {
                Disposition = acceptance.Disposition,
                QueuedForCoordinatorValidation = true,
            });
        }
        catch (InvalidOperationException exception)
        {
            return Task.FromResult(new WorkerTerminalReceiptResponse
            {
                Disposition = WorkerReceiptDisposition.RejectedStaleFence,
                Failure = Failure(FailureCode.StaleFence, Bounded(exception.Message)),
            });
        }
    }

    private static Failure Failure(FailureCode code, string detail) =>
        new() { Code = code, Detail = detail };

    private static string Bounded(string value) =>
        value.Length <= 512 ? value : value[..512];

    private static bool IsWorkerEndpoint(ServerCallContext context)
    {
        InfiniumPipeRoleFeature? feature =
            context.GetHttpContext().Features.Get<InfiniumPipeRoleFeature>();
        return feature is not null
            && string.Equals(feature.Role, "worker", StringComparison.Ordinal);
    }
}
