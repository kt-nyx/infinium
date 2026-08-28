using Grpc.Core;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;

namespace Infinium.DesktopHost;

/// <summary>Owns one reconnectable, least-authority coordinator connection for a renderer session.</summary>
public sealed class DesktopApplicationClient : IGeneratedRendererApplicationClient, IAsyncDisposable
{
    private const int MaximumConnectionAttempts = 3;
    private static readonly TimeSpan ConnectionAttemptTimeout = TimeSpan.FromSeconds(3);
    private readonly string productRoot;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim connectionTransition = new(1, 1);
    private CoordinatorConnection? connection;
    private int disposed;

    public DesktopApplicationClient(string productRoot)
    {
        this.productRoot = Path.GetFullPath(productRoot);
    }

    public Task<GetApplicationBootstrapResponse> GetApplicationBootstrapAsync(
        GetApplicationBootstrapRequest request,
        CancellationToken cancellationToken)
        => UnaryAsync((client, token) => client.GetApplicationBootstrapAsync(request, cancellationToken: token), cancellationToken);

    public Task<ListResultItemsResponse> ListResultItemsAsync(
        ListResultItemsRequest request,
        CancellationToken cancellationToken)
        => UnaryAsync((client, token) => client.ListResultItemsAsync(request, cancellationToken: token), cancellationToken);

    public Task<GetResultDetailResponse> GetResultDetailAsync(
        GetResultDetailRequest request,
        CancellationToken cancellationToken)
        => UnaryAsync((client, token) => client.GetResultDetailAsync(request, cancellationToken: token), cancellationToken);

    public Task<GetProgressResponse> GetProgressAsync(
        GetProgressRequest request,
        CancellationToken cancellationToken)
        => UnaryAsync((client, token) => client.GetProgressAsync(request, cancellationToken: token), cancellationToken);

    public async IAsyncEnumerable<ApplicationEvent> SubscribeEventsAsync(
        SubscribeEventsRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaximumConnectionAttempts; attempt++)
        {
            CoordinatorConnection activeConnection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            bool observedEvent = false;
            bool retry = false;
            using AsyncServerStreamingCall<ApplicationEvent> call = activeConnection.Client.SubscribeEvents(
                request,
                cancellationToken: cancellationToken);
            while (true)
            {
                bool hasEvent;
                try
                {
                    hasEvent = await call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (!observedEvent && IsReconnectable(exception, cancellationToken))
                {
                    await InvalidateAsync(activeConnection).ConfigureAwait(false);
                    if (attempt == MaximumConnectionAttempts - 1)
                    {
                        throw;
                    }
                    retry = true;
                    break;
                }
                catch (Exception exception) when (observedEvent && IsReconnectable(exception, cancellationToken))
                {
                    await InvalidateAsync(activeConnection).ConfigureAwait(false);
                    throw;
                }
                if (!hasEvent)
                {
                    yield break;
                }
                observedEvent = true;
                yield return call.ResponseStream.Current;
            }
            if (retry)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<TResponse> UnaryAsync<TResponse>(
        Func<ApplicationService.ApplicationServiceClient, CancellationToken, AsyncUnaryCall<TResponse>> start,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < MaximumConnectionAttempts; attempt++)
        {
            CoordinatorConnection activeConnection = await GetConnectionAsync(cancellationToken).ConfigureAwait(false);
            using CancellationTokenSource operationAttempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            operationAttempt.CancelAfter(ConnectionAttemptTimeout);
            try
            {
                return await start(activeConnection.Client, operationAttempt.Token).ResponseAsync.ConfigureAwait(false);
            }
            catch (Exception exception) when (IsReconnectable(exception, cancellationToken))
            {
                await InvalidateAsync(activeConnection).ConfigureAwait(false);
                if (attempt == MaximumConnectionAttempts - 1)
                {
                    throw;
                }
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException("The finite desktop coordinator reconnect loop ended unexpectedly.");
    }

    private async Task<CoordinatorConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        await connectionTransition.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (connection is not null)
            {
                return connection;
            }
            using CancellationTokenSource attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            attempt.CancelAfter(ConnectionAttemptTimeout);
            connection = await CoordinatorConnection.ConnectDesktopHostAsync(productRoot, attempt.Token).ConfigureAwait(false);
            return connection;
        }
        finally
        {
            connectionTransition.Release();
        }
    }

    private async Task InvalidateAsync(CoordinatorConnection failed)
    {
        await connectionTransition.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (ReferenceEquals(connection, failed))
            {
                connection = null;
                await failed.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            connectionTransition.Release();
        }
    }

    private bool IsReconnectable(Exception exception, CancellationToken requestCancellation)
    {
        if (requestCancellation.IsCancellationRequested || lifetime.IsCancellationRequested)
        {
            return false;
        }
        return exception is IOException
            or TimeoutException
            or InvalidOperationException
            or OperationCanceledException
            || exception is RpcException rpcException
                && rpcException.StatusCode is StatusCode.Unavailable
                    or StatusCode.Internal
                    or StatusCode.Cancelled
                    or StatusCode.DeadlineExceeded;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }
        await lifetime.CancelAsync().ConfigureAwait(false);
        await connectionTransition.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                connection = null;
            }
        }
        finally
        {
            connectionTransition.Release();
            connectionTransition.Dispose();
            lifetime.Dispose();
        }
    }
}
