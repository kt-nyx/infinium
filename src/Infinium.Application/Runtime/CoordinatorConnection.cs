using Google.Protobuf;
using Grpc.Net.Client;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;

namespace Infinium.Application.Runtime;

public sealed class CoordinatorConnection : IAsyncDisposable
{
    private CoordinatorConnection(
        RuntimeDescriptor descriptor,
        GrpcChannel channel,
        ApplicationService.ApplicationServiceClient client)
    {
        Descriptor = descriptor;
        Channel = channel;
        Client = client;
    }

    public RuntimeDescriptor Descriptor { get; }
    public GrpcChannel Channel { get; }
    public ApplicationService.ApplicationServiceClient Client { get; }

    public static async Task<CoordinatorConnection> ConnectAsync(
        string productRoot,
        CancellationToken cancellationToken)
    {
        RuntimeDescriptor descriptor = RuntimeDescriptor.Read(productRoot);
        GrpcChannel channel = NamedPipeGrpcChannel.Create(descriptor.ApplicationPipe);
        ApplicationService.ApplicationServiceClient client = new(channel);
        ApplicationHandshakeRequest request = new()
        {
            SupportedProtocol = new ProtocolVersionRange
            {
                Major = ProtocolConstants.Major,
                MinimumMinor = ProtocolConstants.Minor,
                MaximumMinor = ProtocolConstants.Minor,
            },
            Compatibility = ProtocolConstants.Compatibility,
            ClientKind = ApplicationClientKind.Cli,
            CoordinatorInstanceNonce = ByteString.CopyFrom(descriptor.GetNonce()),
        };
        request.RequestedCapabilities.Add(Capability.ApplicationQuery);
        request.RequestedCapabilities.Add(Capability.DurableCommand);
        request.RequestedCapabilities.Add(Capability.EventStream);
        request.RequestedCapabilities.Add(Capability.KeysetCursor);
        try
        {
            HandshakeResponse response = await client.NegotiateAsync(
                request,
                deadline: DateTime.UtcNow.AddSeconds(10),
                cancellationToken: cancellationToken).ResponseAsync.ConfigureAwait(false);
            if (response.Disposition != HandshakeDisposition.Accepted
                || response.CoordinatorInstanceId?.Value != descriptor.CoordinatorInstanceId
                || response.CoordinatorFencingEpoch != checked((ulong)descriptor.FencingEpoch)
                || response.Limits is null
                || response.Limits.MaximumMessageBytes == 0)
            {
                throw new InvalidOperationException(
                    $"Coordinator negotiation failed: {response.Disposition} {response.Failure?.Detail}");
            }

            return new CoordinatorConnection(descriptor, channel, client);
        }
        catch
        {
            channel.Dispose();
            throw;
        }
    }

    public ValueTask DisposeAsync()
    {
        Channel.Dispose();
        return ValueTask.CompletedTask;
    }
}
