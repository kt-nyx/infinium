using Grpc.Core;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;

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

        return Task.FromResult(bootstraps.Negotiate(
            request,
            context.GetHttpContext().Connection.Id,
            runtime));
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
        bool current = bootstraps.IsCurrentAttempt(
            request.AttemptId?.Value ?? string.Empty,
            request.CoordinatorFencingEpoch,
            request.AttemptFencingToken,
            context.GetHttpContext().Connection.Id,
            runtime);
        WorkerProgressReceipt response = new()
        {
            Disposition = current
                ? WorkerReceiptDisposition.AcceptedForStagingOnly
                : WorkerReceiptDisposition.RejectedStaleFence,
            AcceptedProgressSequence = current ? request.ProgressSequence : 0,
        };
        if (!current)
        {
            response.Failure =
                Failure(FailureCode.StaleFence, "The progress attempt is stale.");
        }

        return Task.FromResult(response);
    }

    public override Task<PollControlResponse> PollControl(
        PollControlRequest request,
        ServerCallContext context)
    {
        bool current = bootstraps.IsCurrentAttempt(
            request.AttemptId?.Value ?? string.Empty,
            request.CoordinatorFencingEpoch,
            request.AttemptFencingToken,
            context.GetHttpContext().Connection.Id,
            runtime);
        PollControlResponse response = new()
        {
            Control = current
                ? WorkerControl.Continue
                : WorkerControl.StopStaleAttempt,
            ControlSequence = 1,
        };
        if (!current)
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
            string receipt = bootstraps.AcceptStagedOutput(
                request,
                context.GetHttpContext().Connection.Id);
            return Task.FromResult(new SubmitStagedOutputResponse
            {
                Disposition = WorkerReceiptDisposition.AcceptedForStagingOnly,
                StagingReceiptId = receipt,
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
            _ = bootstraps.AcceptTerminal(
                request,
                context.GetHttpContext().Connection.Id);
            return Task.FromResult(new WorkerTerminalReceiptResponse
            {
                Disposition = WorkerReceiptDisposition.AcceptedForStagingOnly,
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
