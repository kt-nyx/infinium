using System.IO.Pipes;
using Grpc.Net.Client;

namespace Infinium.Application.Runtime;

public static class NamedPipeGrpcChannel
{
    public static GrpcChannel Create(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        SocketsHttpHandler handler = new()
        {
            ConnectCallback = async (_, cancellationToken) =>
            {
                NamedPipeClientStream pipe = new(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                try
                {
                    await pipe.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    return pipe;
                }
                catch
                {
                    await pipe.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            },
            EnableMultipleHttp2Connections = true,
        };
        return GrpcChannel.ForAddress(
            "http://localhost",
            new GrpcChannelOptions
            {
                HttpHandler = handler,
                MaxReceiveMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes),
                MaxSendMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes),
                DisposeHttpClient = true,
            });
    }
}
