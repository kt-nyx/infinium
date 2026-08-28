using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.DesktopHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class RendererBridgeSessionTests
{
    private const string SecretCanary = "SECRET-CANARY-MUST-NOT-CROSS-DESKTOP-BRIDGE";

    [TestMethod]
    public async Task MoreThanOneThousandSequentialRequestsRemainAvailableAndSecretsStayInert()
    {
        ConcurrentQueue<string> outbound = new();
        await using RendererBridgeSession bridge = new(
            new ThrowingClient(),
            message => { outbound.Enqueue(message); return Task.CompletedTask; },
            () => Task.CompletedTask);
        await EstablishAsync(bridge).ConfigureAwait(false);

        for (ulong sequence = 2; sequence <= 1_005; sequence++)
        {
            await bridge.HandleRendererMessageAsync(
                DesktopRuntimePolicy.ApplicationOrigin,
                Request(bridge.SessionId, sequence, $"request_progress_{sequence:00000000}", "progress.read", new JsonObject { ["run_id"] = "opaque-run" })).ConfigureAwait(false);
        }

        Assert.AreEqual(1_004, outbound.Count);
        Assert.IsFalse(outbound.Any(message => message.Contains(SecretCanary, StringComparison.Ordinal)));
        using JsonDocument final = JsonDocument.Parse(outbound.Last());
        Assert.AreEqual("1005", final.RootElement.GetProperty("sequence").GetString());
        Assert.AreEqual("unavailable", final.RootElement.GetProperty("payload").GetProperty("outcome").GetString());
    }

    [TestMethod]
    public async Task SixtyFifthConcurrentRequestIsRejectedAtomically()
    {
        BlockingClient client = new();
        await using RendererBridgeSession bridge = new(client, _ => Task.CompletedTask, () => Task.CompletedTask);
        await EstablishAsync(bridge).ConfigureAwait(false);

        List<Task> active = [];
        for (ulong sequence = 2; sequence <= 65; sequence++)
        {
            active.Add(bridge.HandleRendererMessageAsync(
                DesktopRuntimePolicy.ApplicationOrigin,
                Request(bridge.SessionId, sequence, $"request_progress_{sequence:00000000}", "progress.read", new JsonObject { ["run_id"] = "opaque-run" })));
        }
        await client.AllStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 66, "request_progress_00000066", "progress.read", new JsonObject { ["run_id"] = "opaque-run" }))).ConfigureAwait(false);

        client.Release.TrySetResult();
        await Task.WhenAll(active).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task HostEventAndRendererRequestSequencesInterleaveWithoutSharingState()
    {
        ConcurrentQueue<string> outbound = new();
        BlockingSubscriptionClient client = new();
        await using RendererBridgeSession bridge = new(
            client,
            message => { outbound.Enqueue(message); return Task.CompletedTask; },
            () => Task.CompletedTask);
        await bridge.SendSessionInitializationAsync().ConfigureAwait(false);
        await EstablishAsync(bridge).ConfigureAwait(false);
        await bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 2, "request_subscribe_00000002", "progress.subscribe", new JsonObject
            {
                ["subscription_id"] = "subscription_00000002",
                ["run_id"] = "opaque-run",
                ["requested_queue_items"] = 64,
            })).ConfigureAwait(false);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        Assert.IsTrue(await bridge.GrantCancellationGestureAsync().ConfigureAwait(false));
        await bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 3, "request_progress_00000003", "progress.read", new JsonObject { ["run_id"] = "opaque-run" })).ConfigureAwait(false);

        JsonDocument[] messages = outbound.Select(message => JsonDocument.Parse(message)).ToArray();
        try
        {
            Assert.AreEqual("1", messages[0].RootElement.GetProperty("sequence").GetString());
            Assert.AreEqual("transport.session.establish", messages[0].RootElement.GetProperty("operation").GetString());
            Assert.AreEqual("2", messages[1].RootElement.GetProperty("sequence").GetString());
            Assert.AreEqual("transport.gesture.grant", messages[1].RootElement.GetProperty("operation").GetString());
            Assert.AreEqual("3", messages[2].RootElement.GetProperty("sequence").GetString());
            Assert.AreEqual("progress.read", messages[2].RootElement.GetProperty("operation").GetString());
        }
        finally
        {
            foreach (JsonDocument message in messages)
            {
                message.Dispose();
            }
        }
    }

    [TestMethod]
    public async Task SessionRejectsOrdinaryRequestBeforeExactAcknowledgementAndRejectsReplay()
    {
        await using RendererBridgeSession bridge = new(new ThrowingClient(), _ => Task.CompletedTask, () => Task.CompletedTask);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 1, "request_progress_00000001", "progress.read", new JsonObject { ["run_id"] = "opaque-run" }))).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task BoundSessionRejectsAStaleSessionEnvelope()
    {
        await using RendererBridgeSession bridge = new(new ThrowingClient(), _ => Task.CompletedTask, () => Task.CompletedTask);
        await EstablishAsync(bridge).ConfigureAwait(false);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request("renderer_session_stale_00000000000000000001", 2, "request_progress_00000002", "progress.read", new JsonObject
            {
                ["run_id"] = "opaque-run",
            }))).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task HostGestureGrantCancelsOnlyItsActiveSubscriptionAndCannotReplay()
    {
        ConcurrentQueue<string> outbound = new();
        BlockingSubscriptionClient client = new();
        await using RendererBridgeSession bridge = new(
            client,
            message => { outbound.Enqueue(message); return Task.CompletedTask; },
            () => Task.CompletedTask);
        await bridge.SendSessionInitializationAsync().ConfigureAwait(false);
        await EstablishAsync(bridge).ConfigureAwait(false);
        const string subscriptionRequest = "request_subscribe_00000002";
        await bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 2, subscriptionRequest, "progress.subscribe", new JsonObject
            {
                ["subscription_id"] = "subscription_00000002",
                ["run_id"] = "opaque-run",
                ["requested_queue_items"] = 64,
            })).ConfigureAwait(false);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.IsTrue(await bridge.GrantCancellationGestureAsync().ConfigureAwait(false));
        Assert.IsFalse(await bridge.GrantCancellationGestureAsync().ConfigureAwait(false));
        using JsonDocument grant = JsonDocument.Parse(outbound.Last());
        string gestureId = grant.RootElement.GetProperty("payload").GetProperty("gesture_id").GetString()!;
        string cancel = Request(
            bridge.SessionId,
            3,
            "request_cancel_00000003",
            "application.cancel",
            new JsonObject { ["target_request_id"] = subscriptionRequest },
            gestureId);
        await bridge.HandleRendererMessageAsync(DesktopRuntimePolicy.ApplicationOrigin, cancel).ConfigureAwait(false);
        await client.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        using JsonDocument response = JsonDocument.Parse(outbound.Last());
        Assert.AreEqual("accepted", response.RootElement.GetProperty("payload").GetProperty("outcome").GetString());

        string replay = Request(
            bridge.SessionId,
            4,
            "request_cancel_00000004",
            "application.cancel",
            new JsonObject { ["target_request_id"] = subscriptionRequest },
            gestureId);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            replay)).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task ReplacementSubscriptionCancelsAndAwaitsThePriorHostStream()
    {
        TrackingSubscriptionClient client = new();
        await using RendererBridgeSession bridge = new(client, _ => Task.CompletedTask, () => Task.CompletedTask);
        await EstablishAsync(bridge).ConfigureAwait(false);

        await bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 2, "request_subscribe_00000002", "progress.subscribe", new JsonObject
            {
                ["subscription_id"] = "subscription_00000002",
                ["run_id"] = "opaque-run",
                ["requested_queue_items"] = 64,
            })).ConfigureAwait(false);
        await WaitUntilAsync(() => client.Started == 1 && client.Active == 1).ConfigureAwait(false);

        await bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 3, "request_subscribe_00000003", "progress.subscribe", new JsonObject
            {
                ["subscription_id"] = "subscription_00000003",
                ["run_id"] = "opaque-run",
                ["requested_queue_items"] = 64,
            })).ConfigureAwait(false);
        await WaitUntilAsync(() => client.Started == 2 && client.Cancelled == 1 && client.Active == 1).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task StreamFailureBeforeAnyAuthoritativeEventRotatesWithoutInventingARevision()
    {
        ConcurrentQueue<string> outbound = new();
        TaskCompletionSource reestablished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using RendererBridgeSession bridge = new(
            new FailingSubscriptionClient(),
            message => { outbound.Enqueue(message); return Task.CompletedTask; },
            () => { reestablished.TrySetResult(); return Task.CompletedTask; });
        await EstablishAsync(bridge).ConfigureAwait(false);
        await bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 2, "request_subscribe_00000002", "progress.subscribe", new JsonObject
            {
                ["subscription_id"] = "subscription_00000002",
                ["run_id"] = "opaque-run",
                ["requested_queue_items"] = 64,
            })).ConfigureAwait(false);

        await reestablished.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.IsFalse(outbound.Any(message => message.Contains("application.resync-required", StringComparison.Ordinal)));
        Assert.IsFalse(outbound.Any(message => message.Contains("authoritative-resync-required", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task StreamFailureRecoveryCanDisposeItsOwningBridgeWithoutDeadlock()
    {
        ConcurrentQueue<string> outbound = new();
        TaskCompletionSource<string> rotated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RendererBridgeSession? replacement = null;
        RendererBridgeSession? bridge = null;
        bridge = new RendererBridgeSession(
            new FailingSubscriptionClient(),
            message => { outbound.Enqueue(message); return Task.CompletedTask; },
            async () =>
            {
                await bridge!.DisposeAsync().ConfigureAwait(false);
                replacement = new RendererBridgeSession(new ThrowingClient(), _ => Task.CompletedTask, () => Task.CompletedTask);
                rotated.TrySetResult(replacement.SessionId);
            });

        try
        {
            string originalSession = bridge.SessionId;
            await EstablishAsync(bridge).ConfigureAwait(false);
            await bridge.HandleRendererMessageAsync(
                DesktopRuntimePolicy.ApplicationOrigin,
                Request(originalSession, 2, "request_subscribe_00000002", "progress.subscribe", new JsonObject
                {
                    ["subscription_id"] = "subscription_00000002",
                    ["run_id"] = "opaque-run",
                    ["requested_queue_items"] = 64,
                })).ConfigureAwait(false);

            string replacementSession = await rotated.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            Assert.AreNotEqual(originalSession, replacementSession);
            Assert.IsFalse(outbound.Any(message => message.Contains("application.resync-required", StringComparison.Ordinal)));
        }
        finally
        {
            if (replacement is not null)
            {
                await replacement.DisposeAsync().ConfigureAwait(false);
            }
            else if (bridge is not null)
            {
                await bridge.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    [TestMethod]
    public async Task OversizedOutboundProjectionIsRejectedBeforeDelivery()
    {
        ConcurrentQueue<string> outbound = new();
        await using RendererBridgeSession bridge = new(
            new OversizedFailureClient(),
            message => { outbound.Enqueue(message); return Task.CompletedTask; },
            () => Task.CompletedTask);
        await EstablishAsync(bridge).ConfigureAwait(false);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 2, "request_progress_00000002", "progress.read", new JsonObject { ["run_id"] = "opaque-run" }))).ConfigureAwait(false);
        Assert.IsEmpty(outbound);
    }

    [TestMethod]
    public async Task GestureGrantRejectsSelfTargetAndLateCompletedTarget()
    {
        ConcurrentQueue<string> outbound = new();
        CompletingSubscriptionClient client = new();
        await using RendererBridgeSession bridge = new(
            client,
            message => { outbound.Enqueue(message); return Task.CompletedTask; },
            () => Task.CompletedTask);
        await bridge.SendSessionInitializationAsync().ConfigureAwait(false);
        await EstablishAsync(bridge).ConfigureAwait(false);
        const string target = "request_subscribe_00000002";
        await bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 2, target, "progress.subscribe", new JsonObject
            {
                ["subscription_id"] = "subscription_00000002",
                ["run_id"] = "opaque-run",
                ["requested_queue_items"] = 64,
            })).ConfigureAwait(false);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.IsTrue(await bridge.GrantCancellationGestureAsync().ConfigureAwait(false));
        using JsonDocument grant = JsonDocument.Parse(outbound.Last());
        string gestureId = grant.RootElement.GetProperty("payload").GetProperty("gesture_id").GetString()!;

        client.Release.TrySetResult();
        await client.Completed.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Assert.IsFalse(await bridge.GrantCancellationGestureAsync().ConfigureAwait(false));
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 3, "request_cancel_00000003", "application.cancel", new JsonObject
            {
                ["target_request_id"] = target,
            }, gestureId))).ConfigureAwait(false);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 4, "request_cancel_00000004", "application.cancel", new JsonObject
            {
                ["target_request_id"] = "request_cancel_00000004",
            }, "gesture_unattested_self_00000004"))).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task GestureGrantExpiresAndFailedDeliveryRetiresAuthority()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-08-27T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        BlockingSubscriptionClient client = new();
        int sends = 0;
        ConcurrentQueue<string> outbound = new();
        await using RendererBridgeSession bridge = new(
            client,
            message =>
            {
                if (Interlocked.Increment(ref sends) == 2)
                {
                    throw new IOException("inert delivery failure");
                }
                outbound.Enqueue(message);
                return Task.CompletedTask;
            },
            () => Task.CompletedTask,
            utcNow: () => now);
        await bridge.SendSessionInitializationAsync().ConfigureAwait(false);
        await EstablishAsync(bridge).ConfigureAwait(false);
        const string target = "request_subscribe_00000002";
        await bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 2, target, "progress.subscribe", new JsonObject
            {
                ["subscription_id"] = "subscription_00000002",
                ["run_id"] = "opaque-run",
                ["requested_queue_items"] = 64,
            })).ConfigureAwait(false);
        await client.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

        await Assert.ThrowsExactlyAsync<IOException>(() => bridge.GrantCancellationGestureAsync()).ConfigureAwait(false);
        Assert.IsTrue(await bridge.GrantCancellationGestureAsync().ConfigureAwait(false), "A failed host delivery must retire the unused grant.");
        RendererContractValidator renderer = new(bridge.SessionId);
        RendererEnvelope initialization = renderer.ValidateAndAdvance(Encoding.UTF8.GetBytes(outbound.First()));
        RendererEnvelope acceptedGrant = renderer.ValidateAndAdvance(Encoding.UTF8.GetBytes(outbound.Last()));
        Assert.AreEqual(1UL, initialization.Sequence);
        Assert.AreEqual(2UL, acceptedGrant.Sequence, "A failed post must not consume the next host-event sequence.");
        Assert.AreEqual("transport.gesture.grant", acceptedGrant.Operation);
        using JsonDocument grant = JsonDocument.Parse(outbound.Last());
        string gestureId = grant.RootElement.GetProperty("payload").GetProperty("gesture_id").GetString()!;
        now = now.AddSeconds(6);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 3, "request_cancel_00000003", "application.cancel", new JsonObject
            {
                ["target_request_id"] = target,
            }, gestureId))).ConfigureAwait(false);
    }

    [TestMethod]
    public async Task SlowConsumerOverflowCancelsLosslessStreamAndEmitsAuthoritativeResync()
    {
        ConcurrentQueue<string> outbound = new();
        TaskCompletionSource firstDelivered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseSecondPost = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int posted = 0;
        OverflowSubscriptionClient client = new();
        await using RendererBridgeSession bridge = new(
            client,
            message =>
            {
                outbound.Enqueue(message);
                int delivery = Interlocked.Increment(ref posted);
                if (delivery == 1)
                {
                    firstDelivered.TrySetResult();
                }
                return delivery == 2 ? releaseSecondPost.Task : Task.CompletedTask;
            },
            () => Task.CompletedTask);
        await EstablishAsync(bridge).ConfigureAwait(false);
        await bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 2, "request_subscribe_00000002", "progress.subscribe", new JsonObject
            {
                ["subscription_id"] = "subscription_00000002",
                ["run_id"] = "opaque-run",
                ["requested_queue_items"] = 64,
            })).ConfigureAwait(false);

        await firstDelivered.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        await Task.Delay(50).ConfigureAwait(false);
        client.ContinueFlood.TrySetResult();
        await client.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        releaseSecondPost.TrySetResult();
        await WaitUntilAsync(() => outbound.Any(message => message.Contains("application.resync-required", StringComparison.Ordinal))).ConfigureAwait(false);
        Assert.IsTrue(outbound.Any(message => message.Contains("\"current_projection_version\":\"projection-1\"", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task DisposeAwaitsBlockedDeliveryAndSuppressesLaterPosts()
    {
        TaskCompletionSource<string> posted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int postCount = 0;
        RendererBridgeSession bridge = new(
            new ThrowingClient(),
            message =>
            {
                Interlocked.Increment(ref postCount);
                posted.TrySetResult(message);
                return release.Task;
            },
            () => Task.CompletedTask);
        await EstablishAsync(bridge).ConfigureAwait(false);
        Task handling = bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 2, "request_progress_00000002", "progress.read", new JsonObject { ["run_id"] = "opaque-run" }));
        await posted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        Task disposing = bridge.DisposeAsync().AsTask();
        await Task.Delay(50).ConfigureAwait(false);
        Assert.IsFalse(disposing.IsCompleted);
        release.TrySetResult();
        await disposing.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        try { await handling.ConfigureAwait(false); } catch (OperationCanceledException) { }
        await Task.Delay(50).ConfigureAwait(false);
        Assert.AreEqual(1, postCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token).ConfigureAwait(false);
        }
    }

    private static async Task EstablishAsync(RendererBridgeSession bridge)
    {
        JsonObject payload = new()
        {
            ["renderer_registry_version"] = GeneratedRendererOperationCatalog.RegistryVersion,
            ["renderer_registry_sha256"] = GeneratedRendererOperationCatalog.RegistrySha256,
        };
        await bridge.HandleRendererMessageAsync(
            DesktopRuntimePolicy.ApplicationOrigin,
            Request(bridge.SessionId, 1, "transport_acknowledgement_0001", "transport.session.establish", payload)).ConfigureAwait(false);
    }

    private static string Request(string sessionId, ulong sequence, string requestId, string operation, JsonObject payload, string? gestureId = null)
    {
        JsonObject envelope = new()
        {
            ["contract_version"] = ProtocolConstants.RendererContractVersion,
            ["message_kind"] = "request",
            ["session_id"] = sessionId,
            ["sequence"] = sequence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["request_id"] = requestId,
            ["operation"] = operation,
            ["payload"] = payload,
        };
        if (gestureId is not null)
        {
            envelope["gesture_proof"] = new JsonObject { ["gesture_id"] = gestureId };
        }
        return envelope.ToJsonString();
    }

    private class ThrowingClient : IGeneratedRendererApplicationClient
    {
        public Task<GetApplicationBootstrapResponse> GetApplicationBootstrapAsync(GetApplicationBootstrapRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException(SecretCanary);
        public Task<ListResultItemsResponse> ListResultItemsAsync(ListResultItemsRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException(SecretCanary);
        public Task<GetResultDetailResponse> GetResultDetailAsync(GetResultDetailRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException(SecretCanary);
        public virtual Task<GetProgressResponse> GetProgressAsync(GetProgressRequest request, CancellationToken cancellationToken) => throw new InvalidOperationException(SecretCanary);
        public virtual async IAsyncEnumerable<ApplicationEvent> SubscribeEventsAsync(SubscribeEventsRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            yield break;
        }
    }

    private sealed class BlockingClient : ThrowingClient
    {
        private int started;
        internal TaskCompletionSource AllStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<GetProgressResponse> GetProgressAsync(GetProgressRequest request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref started) == 64)
            {
                AllStarted.TrySetResult();
            }
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(SecretCanary);
        }
    }

    private sealed class BlockingSubscriptionClient : ThrowingClient
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async IAsyncEnumerable<ApplicationEvent> SubscribeEventsAsync(
            SubscribeEventsRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Cancelled.TrySetResult();
            }
            yield break;
        }
    }

    private sealed class TrackingSubscriptionClient : ThrowingClient
    {
        private int active;
        private int started;
        private int cancelled;

        internal int Active => Volatile.Read(ref active);
        internal int Started => Volatile.Read(ref started);
        internal int Cancelled => Volatile.Read(ref cancelled);

        public override async IAsyncEnumerable<ApplicationEvent> SubscribeEventsAsync(
            SubscribeEventsRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref started);
            Interlocked.Increment(ref active);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref active);
                Interlocked.Increment(ref cancelled);
            }
            yield break;
        }
    }

    private sealed class FailingSubscriptionClient : ThrowingClient
    {
        public override async IAsyncEnumerable<ApplicationEvent> SubscribeEventsAsync(
            SubscribeEventsRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            throw new IOException("inert stream failure");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }
    }

    private sealed class OversizedFailureClient : ThrowingClient
    {
        public override Task<GetProgressResponse> GetProgressAsync(GetProgressRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new GetProgressResponse
            {
                Failure = new Infinium.Contracts.Protobuf.Common.V1.Failure
                {
                    Code = Infinium.Contracts.Protobuf.Common.V1.FailureCode.Internal,
                    Detail = new string('x', checked((int)ProtocolConstants.MaximumMessageBytes + 1)),
                },
            });
    }

    private sealed class CompletingSubscriptionClient : ThrowingClient
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async IAsyncEnumerable<ApplicationEvent> SubscribeEventsAsync(
            SubscribeEventsRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try
            {
                await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Completed.TrySetResult();
            }
            yield break;
        }
    }

    private sealed class OverflowSubscriptionClient : ThrowingClient
    {
        internal TaskCompletionSource ContinueFlood { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async IAsyncEnumerable<ApplicationEvent> SubscribeEventsAsync(
            SubscribeEventsRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            try
            {
                yield return Event(request, 1);
                await ContinueFlood.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                for (ulong sequence = 2; sequence <= 68; sequence++)
                {
                    yield return Event(request, sequence);
                }
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                Cancelled.TrySetResult();
            }
        }

        private static ApplicationEvent Event(SubscribeEventsRequest request, ulong sequence) => new()
        {
            CoordinatorInstanceId = new Infinium.Contracts.Protobuf.Domain.V1.CoordinatorInstanceId { Value = "coordinator-1" },
            CoordinatorFencingEpoch = 1,
            SubscriptionId = request.SubscriptionId.Clone(),
            DurableEventSequence = sequence,
            ProjectionVersion = new Infinium.Contracts.Protobuf.Domain.V1.ProjectionVersion { Value = $"projection-{sequence}" },
            Kind = EventKind.ResyncRequired,
            RunScope = request.RunScope[0].Clone(),
            ResyncRequired = new ResyncRequired
            {
                Reason = ResyncReason.QueueOverflow,
                CurrentProjectionVersion = new Infinium.Contracts.Protobuf.Domain.V1.ProjectionVersion { Value = $"projection-{sequence}" },
            },
            ResumeCursor = new EventCursor { OpaqueValue = ByteString.CopyFromUtf8($"cursor-{sequence}") },
        };
    }
}
