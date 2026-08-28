using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Google.Protobuf;
using Infinium.Application.Runtime;
using AppContract = Infinium.Contracts.Protobuf.Application.V1;
using CommonContract = Infinium.Contracts.Protobuf.Common.V1;
using DomainContract = Infinium.Contracts.Protobuf.Domain.V1;

namespace Infinium.DesktopHost;

public sealed class RendererBridgeSession : IGeneratedRendererRequestHandler, IAsyncDisposable
{
    private readonly IGeneratedRendererApplicationClient client;
    private readonly GeneratedRendererProjectionAdapter projections;
    private readonly RendererContractValidator validator;
    private readonly Func<string, Task> postMessage;
    private readonly Func<Task> reestablishSession;
    private readonly RendererBridgeMetrics metrics;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> activeRequests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Task> ownedSubscriptions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, GestureGrant> pendingGestureGrants = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim activeRequestSlots = new(checked((int)ProtocolConstants.MaximumStreamQueueItems));
    private readonly Channel<OutboundDelivery> outbound;
    private readonly Task outboundPump;
    private readonly SemaphoreSlim subscriptionTransition = new(1, 1);
    private readonly Func<DateTimeOffset> utcNow;
    private static readonly TimeSpan GestureGrantLifetime = TimeSpan.FromSeconds(5);
    private bool established;
    private string? lastCancellableRequestId;
    private string? currentSubscriptionRequestId;
    private int reestablishmentScheduled;

    public RendererBridgeSession(
        IGeneratedRendererApplicationClient client,
        Func<string, Task> postMessage,
        Func<Task> reestablishSession,
        RendererBridgeMetrics? metrics = null,
        Func<DateTimeOffset>? utcNow = null)
    {
        this.client = client;
        this.postMessage = postMessage;
        this.reestablishSession = reestablishSession;
        this.metrics = metrics ?? new RendererBridgeMetrics();
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        projections = new(new RendererApplicationProjectionCodec());
        SessionId = $"renderer_session_{Guid.NewGuid():N}";
        validator = new(SessionId);
        outbound = Channel.CreateBounded<OutboundDelivery>(new BoundedChannelOptions(checked((int)ProtocolConstants.MaximumStreamQueueItems))
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });
        outboundPump = RunOutboundPumpAsync();
    }

    public string SessionId { get; }

    public async Task<bool> GrantCancellationGestureAsync()
    {
        RemoveExpiredGestureGrants();
        string? target = lastCancellableRequestId;
        if (!established || target is null || !activeRequests.ContainsKey(target))
        {
            return false;
        }
        if (!pendingGestureGrants.IsEmpty)
        {
            return false;
        }
        string gestureId = $"gesture_{Guid.NewGuid():N}";
        GestureGrant grant = new(target, utcNow().Add(GestureGrantLifetime));
        if (!pendingGestureGrants.TryAdd(gestureId, grant))
        {
            throw new InvalidOperationException("A host gesture identity collided unexpectedly.");
        }
        try
        {
            await SendHostEventAsync(sequence => SerializeEnvelope(CommonEnvelope(
                "event",
                sequence,
                "transport.gesture.grant",
                Element(new JsonObject
                {
                    ["outcome"] = "accepted",
                    ["gesture_id"] = gestureId,
                    ["target_request_id"] = target,
                    ["operation"] = "application.cancel",
                }))), lifetime.Token).ConfigureAwait(false);
        }
        catch
        {
            pendingGestureGrants.TryRemove(gestureId, out _);
            throw;
        }
        return true;
    }

    public Task SendSessionInitializationAsync(CancellationToken cancellationToken = default)
    {
        return SendHostEventAsync(sequence => SerializeEnvelope(new JsonObject
        {
            ["contract_version"] = ProtocolConstants.RendererContractVersion,
            ["message_kind"] = "event",
            ["session_id"] = SessionId,
            ["sequence"] = sequence.ToString(CultureInfo.InvariantCulture),
            ["operation"] = "transport.session.establish",
            ["payload"] = new JsonObject
            {
                ["outcome"] = "accepted",
                ["origin"] = DesktopRuntimePolicy.ApplicationOrigin,
                ["renderer_contract_version"] = ProtocolConstants.RendererContractVersion,
                ["renderer_registry_version"] = GeneratedRendererOperationCatalog.RegistryVersion,
                ["renderer_registry_sha256"] = GeneratedRendererOperationCatalog.RegistrySha256,
            },
        }), cancellationToken);
    }

    public async Task HandleRendererMessageAsync(string source, string serializedEnvelope)
    {
        if (!DesktopRuntimePolicy.IsExactApplicationOrigin(source))
        {
            throw new InvalidDataException("The renderer message source is not the controlled application origin.");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(serializedEnvelope);
        metrics.ObserveInboundRequest(bytes.Length);
        RendererEnvelope envelope = validator.ValidateRendererRequestAndAdvance(bytes);
        if (envelope.MessageKind != "request" || envelope.RequestId is null)
        {
            throw new InvalidDataException("The desktop host accepts only registered renderer requests.");
        }

        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement payload = document.RootElement.GetProperty("payload");
        if (!established)
        {
            if (envelope.Operation != "transport.session.establish")
            {
                throw new InvalidDataException("The renderer session acknowledgement must precede application requests.");
            }
            await GeneratedRendererRequestDispatcher.DispatchAsync(this, envelope, payload, lifetime.Token).ConfigureAwait(false);
            return;
        }
        if (envelope.Operation == "transport.session.establish")
        {
            throw new InvalidDataException("The renderer session acknowledgement was replayed.");
        }

        if (!await activeRequestSlots.WaitAsync(0, lifetime.Token).ConfigureAwait(false))
        {
            throw new InvalidDataException("The renderer concurrent request limit was exceeded.");
        }

        JsonElement projected;
        CancellationTokenSource requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        bool ownershipTransferred = false;
        if (!activeRequests.TryAdd(envelope.RequestId, requestCancellation))
        {
            activeRequestSlots.Release();
            requestCancellation.Dispose();
            throw new InvalidDataException("The renderer request identifier is already active.");
        }

        try
        {
            JsonElement? dispatchResult = await GeneratedRendererRequestDispatcher.DispatchAsync(
                this,
                envelope,
                payload,
                requestCancellation.Token).ConfigureAwait(false);
            if (dispatchResult is null)
            {
                ownershipTransferred = envelope.Operation == "progress.subscribe";
                return;
            }
            projected = dispatchResult.Value;
        }
        catch (OperationCanceledException)
        {
            projected = Failure("cancelled", "cancelled", "The local request was cancelled.", false);
        }
        catch (Exception exception) when (exception is not InvalidDataException)
        {
            projected = Failure("unavailable", "unavailable", "The local application service is unavailable.", true);
        }
        finally
        {
            if (!ownershipTransferred)
            {
                activeRequests.TryRemove(envelope.RequestId, out _);
                RetireGestureGrants(envelope.RequestId);
                activeRequestSlots.Release();
                requestCancellation.Dispose();
            }
        }

        await SendAsync(() => ResponseEnvelope(envelope, projected), lifetime.Token).ConfigureAwait(false);
    }

    public Task<JsonElement?> TransportSessionEstablishAsync(RendererEnvelope envelope, JsonElement payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (envelope.Sequence != 1
            || !StringComparer.Ordinal.Equals(envelope.RequestId, "transport_acknowledgement_0001")
            || !StringComparer.Ordinal.Equals(payload.GetProperty("renderer_registry_version").GetString(), GeneratedRendererOperationCatalog.RegistryVersion)
            || !StringComparer.Ordinal.Equals(payload.GetProperty("renderer_registry_sha256").GetString(), GeneratedRendererOperationCatalog.RegistrySha256))
        {
            throw new InvalidDataException("The renderer session acknowledgement does not bind the host registry.");
        }
        established = true;
        return Task.FromResult<JsonElement?>(null);
    }

    public async Task<JsonElement?> ApplicationBootstrapAsync(RendererEnvelope envelope, JsonElement payload, CancellationToken cancellationToken)
        => await BootstrapAsync(payload, cancellationToken).ConfigureAwait(false);

    public async Task<JsonElement?> ResultsListAsync(RendererEnvelope envelope, JsonElement payload, CancellationToken cancellationToken)
        => await ListResultsAsync(payload, cancellationToken).ConfigureAwait(false);

    public async Task<JsonElement?> ResultsDetailAsync(RendererEnvelope envelope, JsonElement payload, CancellationToken cancellationToken)
        => await ResultDetailAsync(payload, cancellationToken).ConfigureAwait(false);

    public async Task<JsonElement?> ProgressReadAsync(RendererEnvelope envelope, JsonElement payload, CancellationToken cancellationToken)
        => await ProgressAsync(payload, cancellationToken).ConfigureAwait(false);

    public async Task<JsonElement?> ProgressSubscribeAsync(RendererEnvelope envelope, JsonElement payload, CancellationToken cancellationToken)
    {
        await StartSubscriptionAsync(envelope, payload, cancellationToken).ConfigureAwait(false);
        return null;
    }

    public Task<JsonElement?> ApplicationCancelAsync(RendererEnvelope envelope, JsonElement payload, CancellationToken cancellationToken)
    {
        string target = payload.GetProperty("target_request_id").GetString()!;
        if (envelope.GestureId is null
            || !pendingGestureGrants.TryRemove(envelope.GestureId, out GestureGrant? grant)
            || grant.ExpiresAt <= utcNow()
            || !StringComparer.Ordinal.Equals(target, grant.TargetRequestId))
        {
            throw new InvalidDataException("The renderer cancellation lacks an exact host-attested gesture grant.");
        }
        return Task.FromResult<JsonElement?>(CancelTransport(envelope, payload));
    }

    private async Task<JsonElement> BootstrapAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        AppContract.GetApplicationBootstrapRequest request = new()
        {
            RendererContractVersion = new CommonContract.SemanticVersion { Value = ProtocolConstants.RendererContractVersion },
            MaximumRecentRuns = payload.GetProperty("maximum_recent_runs").GetUInt32(),
            ExpectedProjectionVersion = Projection(payload, "expected_projection_version"),
        };
        AppContract.GetApplicationBootstrapResponse response = await client.GetApplicationBootstrapAsync(request, cancellationToken).ConfigureAwait(false);
        return projections.Project("application.bootstrap", request, response);
    }

    private async Task<JsonElement> ListResultsAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        AppContract.ListResultItemsRequest request = new()
        {
            RunId = new DomainContract.RunId { Value = payload.GetProperty("run_id").GetString()! },
            SearchText = payload.GetProperty("search_text").GetString()!,
            Sort = ResultSort(payload.GetProperty("sort").GetString()!),
            RequestedPageSize = payload.GetProperty("requested_page_size").GetUInt32(),
            ExpectedProjectionVersion = Projection(payload, "expected_projection_version"),
        };
        foreach (JsonElement kind in payload.GetProperty("kinds").EnumerateArray())
        {
            request.Kinds.Add(ResultKind(kind.GetString()!));
        }
        if (payload.TryGetProperty("after_cursor", out JsonElement cursor))
        {
            request.After = new AppContract.PageCursor { OpaqueValue = ByteString.CopyFrom(DecodeCursor(cursor.GetString()!)) };
        }
        AppContract.ListResultItemsResponse response = await client.ListResultItemsAsync(request, cancellationToken).ConfigureAwait(false);
        return projections.Project("results.list", request, response);
    }

    private async Task<JsonElement> ResultDetailAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        AppContract.GetResultDetailRequest request = new()
        {
            RunId = new DomainContract.RunId { Value = payload.GetProperty("run_id").GetString()! },
            Kind = ResultKind(payload.GetProperty("kind").GetString()!),
            ItemId = payload.GetProperty("item_id").GetString()!,
            ExpectedProjectionVersion = Projection(payload, "expected_projection_version"),
        };
        AppContract.GetResultDetailResponse response = await client.GetResultDetailAsync(request, cancellationToken).ConfigureAwait(false);
        return projections.Project("results.detail", request, response);
    }

    private async Task<JsonElement> ProgressAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        AppContract.GetProgressRequest request = new()
        {
            RunId = new DomainContract.RunId { Value = payload.GetProperty("run_id").GetString()! },
            ExpectedProjectionVersion = Projection(payload, "expected_projection_version"),
        };
        AppContract.GetProgressResponse response = await client.GetProgressAsync(request, cancellationToken).ConfigureAwait(false);
        return projections.Project("progress.read", request, response);
    }

    private async Task StartSubscriptionAsync(RendererEnvelope envelope, JsonElement payload, CancellationToken cancellationToken)
    {
        await subscriptionTransition.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string requestId = envelope.RequestId!;
            string? previousRequestId = currentSubscriptionRequestId;
            if (previousRequestId is not null && !StringComparer.Ordinal.Equals(previousRequestId, requestId))
            {
                if (activeRequests.TryGetValue(previousRequestId, out CancellationTokenSource? previousCancellation))
                {
                    _ = TryCancel(previousCancellation);
                }
                if (ownedSubscriptions.TryGetValue(previousRequestId, out Task? previousSubscription))
                {
                    await IgnoreCancellationAsync(previousSubscription).ConfigureAwait(false);
                }
            }

            if (!activeRequests.TryGetValue(requestId, out CancellationTokenSource? cancellation))
            {
                throw new InvalidDataException("The renderer subscription request is not active.");
            }
            lastCancellableRequestId = requestId;
            currentSubscriptionRequestId = requestId;
            AppContract.SubscribeEventsRequest request = new()
            {
                SubscriptionId = new DomainContract.SubscriptionId { Value = payload.GetProperty("subscription_id").GetString()! },
                RequestedQueueItems = payload.GetProperty("requested_queue_items").GetUInt32(),
                ExpectedProjectionVersion = Projection(payload, "expected_projection_version"),
            };
            request.RunScope.Add(new DomainContract.RunId { Value = payload.GetProperty("run_id").GetString()! });
            if (payload.TryGetProperty("after_cursor", out JsonElement cursor))
            {
                request.After = new AppContract.EventCursor { OpaqueValue = ByteString.CopyFrom(DecodeCursor(cursor.GetString()!)) };
            }
            Task subscription = RunSubscriptionAsync(requestId, request, cancellation);
            if (!ownedSubscriptions.TryAdd(requestId, subscription))
            {
                cancellation.Cancel();
                throw new InvalidDataException("The renderer subscription task identity is already active.");
            }
            _ = subscription.ContinueWith(
                (completed, state) => ((ConcurrentDictionary<string, Task>)state!).TryRemove(requestId, out Task? ignored),
                ownedSubscriptions,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        finally
        {
            subscriptionTransition.Release();
        }
    }

    private async Task RunSubscriptionAsync(
        string requestId,
        AppContract.SubscribeEventsRequest request,
        CancellationTokenSource cancellation)
    {
        string? lastAuthoritativeProjection = null;
        bool overflow = false;
        try
        {
            Channel<(JsonElement Payload, string Projection)> queue = Channel.CreateBounded<(JsonElement, string)>(
                new BoundedChannelOptions(checked((int)ProtocolConstants.MaximumStreamQueueItems))
                {
                    SingleReader = true,
                    SingleWriter = true,
                    FullMode = BoundedChannelFullMode.Wait,
                });
            Task producer = Task.Run(async () =>
            {
                try
                {
                    await foreach (AppContract.ApplicationEvent applicationEvent in client.SubscribeEventsAsync(request, cancellation.Token).ConfigureAwait(false))
                    {
                        JsonElement projected = projections.ProjectEvent("progress.subscribe", request, applicationEvent);
                        string revision = ProjectionFromEvent(projected);
                        if (!queue.Writer.TryWrite((projected, revision)))
                        {
                            overflow = true;
                            cancellation.Cancel();
                            break;
                        }
                    }
                    queue.Writer.TryComplete();
                }
                catch (Exception exception)
                {
                    queue.Writer.TryComplete(exception);
                }
            }, CancellationToken.None);
            await foreach ((JsonElement Payload, string Projection) item in queue.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false))
            {
                await SendHostEventAsync(
                    sequence => EventEnvelope(sequence, "progress.subscribe", request.SubscriptionId.Value, item.Projection, item.Payload),
                    cancellation.Token).ConfigureAwait(false);
                lastAuthoritativeProjection = item.Projection;
            }
            await producer.ConfigureAwait(false);
            if (overflow)
            {
                await ResynchronizeOrReestablishAsync(request.SubscriptionId.Value, lastAuthoritativeProjection, "The renderer event queue reached its finite bound.").ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested && !overflow)
        {
        }
        catch
        {
            await ResynchronizeOrReestablishAsync(request.SubscriptionId.Value, lastAuthoritativeProjection, "The local event stream must be resynchronized.").ConfigureAwait(false);
        }
        finally
        {
            activeRequests.TryRemove(requestId, out _);
            RetireGestureGrants(requestId);
            activeRequestSlots.Release();
            if (StringComparer.Ordinal.Equals(lastCancellableRequestId, requestId))
            {
                lastCancellableRequestId = null;
            }
            Interlocked.CompareExchange(ref currentSubscriptionRequestId, null, requestId);

            cancellation.Dispose();
        }
    }

    private async Task ResynchronizeOrReestablishAsync(string subscriptionId, string? authoritativeProjection, string inertReason)
    {
        if (string.IsNullOrWhiteSpace(authoritativeProjection))
        {
            ScheduleSessionReestablishment();
            return;
        }
        JsonElement payload = Failure("resync-required", "resync-required", inertReason, false, authoritativeProjection);
        await SendHostEventAsync(
            sequence => EventEnvelope(sequence, "application.resync-required", subscriptionId, authoritativeProjection, payload),
            lifetime.Token).ConfigureAwait(false);
    }

    private JsonElement CancelTransport(RendererEnvelope envelope, JsonElement payload)
    {
        string target = payload.GetProperty("target_request_id").GetString()!;
        if (!StringComparer.Ordinal.Equals(target, envelope.RequestId)
            && activeRequests.TryGetValue(target, out CancellationTokenSource? cancellation)
            && TryCancel(cancellation))
        {
            return Element(new JsonObject { ["outcome"] = "accepted" });
        }
        return Failure("unavailable", "unavailable", "The target request is no longer active.", false);
    }

    private string ResponseEnvelope(RendererEnvelope request, JsonElement payload)
    {
        JsonObject value = CommonEnvelope("response", request.Sequence, request.Operation, payload);
        value["request_id"] = request.RequestId;
        string? revision = ProjectionFromResponse(payload);
        if (revision is not null)
        {
            value["revision"] = revision;
        }

        return SerializeEnvelope(value);
    }

    private string EventEnvelope(ulong sequence, string operation, string subscriptionId, string revision, JsonElement payload)
    {
        JsonObject value = CommonEnvelope("event", sequence, operation, payload);
        value["subscription_id"] = subscriptionId;
        value["revision"] = revision;
        return SerializeEnvelope(value);
    }

    private JsonObject CommonEnvelope(string kind, ulong sequence, string operation, JsonElement payload) => new()
    {
        ["contract_version"] = ProtocolConstants.RendererContractVersion,
        ["message_kind"] = kind,
        ["session_id"] = SessionId,
        ["sequence"] = sequence.ToString(CultureInfo.InvariantCulture),
        ["operation"] = operation,
        ["payload"] = JsonNode.Parse(payload.GetRawText()),
    };

    private static string? ProjectionFromResponse(JsonElement payload)
    {
        if (payload.TryGetProperty("bootstrap", out JsonElement bootstrap))
        {
            return bootstrap.GetProperty("projection_version").GetString();
        }

        if (payload.TryGetProperty("page", out JsonElement page))
        {
            return page.GetProperty("projection_version").GetString();
        }

        if (payload.TryGetProperty("detail", out JsonElement detail))
        {
            return detail.GetProperty("projection_version").GetString();
        }

        if (payload.TryGetProperty("progress", out JsonElement progress))
        {
            return progress.GetProperty("projection_version").GetString();
        }

        if (payload.TryGetProperty("current_projection_version", out JsonElement current))
        {
            return current.GetString();
        }

        if (payload.TryGetProperty("conflict", out JsonElement conflict))
        {
            return conflict.GetProperty("current_revision").GetString();
        }

        return null;
    }

    private static string ProjectionFromEvent(JsonElement payload)
        => payload.TryGetProperty("metadata", out JsonElement metadata)
            ? metadata.GetProperty("projection_version").GetString()!
            : payload.GetProperty("current_projection_version").GetString()!;

    private static DomainContract.ProjectionVersion Projection(JsonElement payload, string property)
        => payload.TryGetProperty(property, out JsonElement value)
            ? new DomainContract.ProjectionVersion { Value = value.GetString()! }
            : new DomainContract.ProjectionVersion();

    private static AppContract.ResultItemKind ResultKind(string value) => value switch
    {
        "supported-case" => AppContract.ResultItemKind.SupportedCase,
        "lead-only-case" => AppContract.ResultItemKind.LeadOnlyCase,
        "finding" => AppContract.ResultItemKind.Finding,
        "abstention" => AppContract.ResultItemKind.Abstention,
        "failure" => AppContract.ResultItemKind.Failure,
        "coverage-gap" => AppContract.ResultItemKind.CoverageGap,
        _ => throw new InvalidDataException("The renderer result kind is not registered."),
    };

    private static AppContract.ResultItemSort ResultSort(string value) => value switch
    {
        "identity-ascending" => AppContract.ResultItemSort.IdentityAscending,
        "severity-descending-identity-ascending" => AppContract.ResultItemSort.SeverityDescendingIdentityAscending,
        _ => throw new InvalidDataException("The renderer result sort is not registered."),
    };

    private static byte[] DecodeCursor(string value)
    {
        string standard = value.Replace('-', '+').Replace('_', '/');
        standard += new string('=', (4 - standard.Length % 4) % 4);
        return Convert.FromBase64String(standard);
    }

    private static JsonElement Failure(string outcome, string code, string inertDetail, bool retry, string? projection = null)
    {
        JsonObject value = new()
        {
            ["outcome"] = outcome,
            ["error"] = new JsonObject { ["code"] = code, ["inert_detail"] = inertDetail, ["retry_may_be_safe"] = retry },
        };
        if (projection is not null)
        {
            value["current_projection_version"] = projection;
        }

        return Element(value);
    }

    private static JsonElement Element(JsonNode node) => JsonSerializer.SerializeToElement(node);

    private void RemoveExpiredGestureGrants()
    {
        DateTimeOffset now = utcNow();
        foreach ((string gestureId, GestureGrant grant) in pendingGestureGrants)
        {
            if (grant.ExpiresAt <= now)
            {
                pendingGestureGrants.TryRemove(gestureId, out _);
            }
        }
    }

    private void RetireGestureGrants(string targetRequestId)
    {
        foreach ((string gestureId, GestureGrant grant) in pendingGestureGrants)
        {
            if (StringComparer.Ordinal.Equals(grant.TargetRequestId, targetRequestId))
            {
                pendingGestureGrants.TryRemove(gestureId, out _);
            }
        }
    }
    private static string SerializeEnvelope(JsonNode node)
    {
        string serialized = node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        if (Encoding.UTF8.GetByteCount(serialized) > ProtocolConstants.MaximumMessageBytes)
        {
            throw new InvalidDataException("The host refused to emit an oversized renderer envelope.");
        }
        return serialized;
    }

    private async Task SendAsync(Func<string> serialize, CancellationToken cancellationToken)
        => await EnqueueDeliveryAsync(new OutboundDelivery(_ => serialize(), false, new(TaskCreationOptions.RunContinuationsAsynchronously)), cancellationToken).ConfigureAwait(false);

    private async Task SendHostEventAsync(Func<ulong, string> serialize, CancellationToken cancellationToken)
        => await EnqueueDeliveryAsync(new OutboundDelivery(sequence => serialize(sequence!.Value), true, new(TaskCreationOptions.RunContinuationsAsynchronously)), cancellationToken).ConfigureAwait(false);

    private async Task EnqueueDeliveryAsync(OutboundDelivery delivery, CancellationToken cancellationToken)
    {
        await outbound.Writer.WriteAsync(delivery, cancellationToken).ConfigureAwait(false);
        await delivery.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunOutboundPumpAsync()
    {
        try
        {
            await foreach (OutboundDelivery delivery in outbound.Reader.ReadAllAsync(lifetime.Token).ConfigureAwait(false))
            {
                try
                {
                    ulong? hostEventSequence = delivery.IsHostEvent
                        ? validator.PeekNextHostEventSequence()
                        : null;
                    string serialized = delivery.Serialize(hostEventSequence);
                    metrics.ObserveOutbound(serialized, Encoding.UTF8.GetByteCount(serialized));
                    await postMessage(serialized).ConfigureAwait(false);
                    if (hostEventSequence.HasValue)
                    {
                        validator.CommitHostEventSequence(hostEventSequence.Value);
                    }
                    delivery.Completion.TrySetResult();
                }
                catch (Exception exception)
                {
                    delivery.Completion.TrySetException(exception);
                }
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            while (outbound.Reader.TryRead(out OutboundDelivery? delivery))
            {
                delivery.Completion.TrySetCanceled(lifetime.Token);
            }
        }
    }

    private void ScheduleSessionReestablishment()
    {
        if (Interlocked.Exchange(ref reestablishmentScheduled, 1) != 0)
        {
            return;
        }

        _ = TryCancel(lifetime);
        _ = Task.Run(async () =>
        {
            try
            {
                await reestablishSession().ConfigureAwait(false);
            }
            catch
            {
                // Recovery is fail-closed. The generation-guarded host owns any visible retry.
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync().ConfigureAwait(false);
        foreach (CancellationTokenSource cancellation in activeRequests.Values)
        {
            _ = TryCancel(cancellation);
        }

        Task[] subscriptions = ownedSubscriptions.Values.ToArray();
        if (subscriptions.Length > 0)
        {
            await Task.WhenAll(subscriptions.Select(IgnoreCancellationAsync)).ConfigureAwait(false);
        }
        outbound.Writer.TryComplete();
        await IgnoreCancellationAsync(outboundPump).ConfigureAwait(false);
        if (client is IAsyncDisposable disposableClient)
        {
            await disposableClient.DisposeAsync().ConfigureAwait(false);
        }
        activeRequestSlots.Dispose();
        subscriptionTransition.Dispose();
        lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    private static bool TryCancel(CancellationTokenSource cancellation)
    {
        try
        {
            cancellation.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
    }

    private sealed record OutboundDelivery(Func<ulong?, string> Serialize, bool IsHostEvent, TaskCompletionSource Completion);
    private sealed record GestureGrant(string TargetRequestId, DateTimeOffset ExpiresAt);
}
