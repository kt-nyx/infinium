using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Application.Runtime;
using Infinium.Bethesda;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.NamedPipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infinium.Tests;

#pragma warning disable CA1416 // The test has an explicit Windows-only guard.

[TestClass]
public sealed class BethesdaSemanticWorkerEndToEndIntegrationTests
{
    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public async Task PublishedSnapshotFlowsThroughContainedWorkerValidationAndCasAdmission()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The contained worker boundary is Windows-only.");
        }

        await using EndToEndContext context = await EndToEndContext.CreateAsync("BETH-LIGHT-VAL");

        await context.Executor.ExecuteBethesdaSemanticAsync(context.Run.RunId, []);

        RunRecord completed = context.Store.GetRun(context.Run.RunId);
        Assert.AreEqual(LifecycleState.Completed, completed.State);
        Assert.IsTrue(context.Store.HasRecoverablePublication(context.Run.RunId));
        RunOperationRecord operation = context.Store.GetRunOperation(context.Run.RunId)!;
        Assert.AreEqual("bethesda-semantic-v1", operation.OperationKind);
        Assert.AreEqual(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(operation.RequestJson))),
            operation.RequestSha256);
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Fault")]
    public async Task RecoveryRedispatchesDurableSemanticOperationThroughContainedWorker()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The contained worker boundary is Windows-only.");
        }

        await using EndToEndContext context = await EndToEndContext.CreateAsync("BETH-LIGHT-VAL");
        string intent = JsonSerializer.Serialize(new ManagedBethesdaSemanticIntent([]));
        _ = context.Store.RegisterRunOperation(
            context.Run.RunId,
            "bethesda-semantic-v1",
            intent,
            DateTimeOffset.UtcNow);
        RunRecord running = context.Store.Transition(
            Guid.NewGuid().ToString("N"),
            context.Run.RunId,
            context.Run.Generation,
            LifecycleState.Running,
            context.Authority.FencingEpoch,
            "simulated interrupted semantic dispatch",
            DateTimeOffset.UtcNow);
        _ = context.Store.CreateAttempt(
            running.RunId,
            context.Authority.FencingEpoch,
            TimeSpan.FromMinutes(2),
            DateTimeOffset.UtcNow);

        context.Executor.RecoverAtStartup();
        RunRecord recovered = await WaitForTerminalAsync(context.Store, running.RunId);

        Assert.AreEqual(LifecycleState.Completed, recovered.State);
        Assert.IsTrue(context.Store.HasRecoverablePublication(running.RunId));
        Assert.AreEqual("bethesda-semantic-v1", context.Store.GetRunOperation(running.RunId)!.OperationKind);
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public async Task AggregatePluginBytesFailBeforeDispatchOrPublication()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The contained worker boundary is Windows-only.");
        }

        string inputRoot = Path.Combine(Path.GetTempPath(), $"infinium-bethesda-aggregate-{Guid.NewGuid():N}");
        Directory.CreateDirectory(inputRoot);
        try
        {
            string first = Path.Combine(inputRoot, "First.esp");
            string second = Path.Combine(inputRoot, "Second.esp");
            using (FileStream stream = File.Create(first))
            {
                stream.SetLength(33L * 1024 * 1024);
            }
            using (FileStream stream = File.Create(second))
            {
                stream.SetLength(33L * 1024 * 1024);
            }
            BethesdaSemanticRequest source = BethesdaSemanticTestSnapshot.Create(
            [
                ("First.esp", 0, first, new OpaqueId("aggregate-provider-1")),
                ("Second.esp", 1, second, new OpaqueId("aggregate-provider-2")),
            ]);
            await using EndToEndContext context = await EndToEndContext.CreateAsync(source);

            await context.Executor.ExecuteBethesdaSemanticAsync(context.Run.RunId, []);

            Assert.AreEqual(LifecycleState.Failed, context.Store.GetRun(context.Run.RunId).State);
            Assert.IsFalse(context.Store.HasLiveAttempts(context.Run.RunId));
            Assert.IsFalse(context.Store.HasRecoverablePublication(context.Run.RunId));
        }
        finally
        {
            Directory.Delete(inputRoot, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public void DurableSemanticIntentIsIdempotentAndCannotBeRebound()
    {
        string root = TemporaryRoot();
        try
        {
            using AuthoritativeStore store = new(new StoragePaths(root));
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                "semantic-intent-test",
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5));
            RunRecord run = store.CreateRun(
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"),
                new RunBinding("snapshot-authoritative", "context-a", "config-a", "manifest-a"),
                authority.FencingEpoch,
                DateTimeOffset.UtcNow);
            const string request = "{\"requestedUnsupportedCapabilities\":[]}";

            RunOperationRecord first = store.RegisterRunOperation(
                run.RunId,
                "bethesda-semantic-v1",
                request,
                DateTimeOffset.UtcNow);
            RunOperationRecord duplicate = store.RegisterRunOperation(
                run.RunId,
                "bethesda-semantic-v1",
                request,
                DateTimeOffset.UtcNow.AddSeconds(1));

            Assert.AreEqual(first, duplicate);
            Assert.ThrowsExactly<InvalidOperationException>(() => store.RegisterRunOperation(
                run.RunId,
                "bethesda-semantic-v1",
                "{\"requestedUnsupportedCapabilities\":[\"archiveMemberRead\"]}",
                DateTimeOffset.UtcNow.AddSeconds(2)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string TemporaryRoot() =>
        Path.Combine(Path.GetTempPath(), $"infinium-bethesda-e2e-{Guid.NewGuid():N}");

    private static async Task<RunRecord> WaitForTerminalAsync(AuthoritativeStore store, string runId)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        while (true)
        {
            RunRecord run = store.GetRun(runId);
            if (LifecyclePolicy.IsTerminal(run.State))
            {
                return run;
            }

            await Task.Delay(50, timeout.Token);
        }
    }

    private sealed class EndToEndContext : IAsyncDisposable
    {
        private readonly string root;
        private readonly WebApplication app;

        private EndToEndContext(
            string root,
            AuthoritativeStore store,
            RunRecord run,
            ManagedRunExecutor executor,
            WebApplication app,
            CoordinatorAuthority authority)
        {
            this.root = root;
            Store = store;
            Run = run;
            Executor = executor;
            this.app = app;
            Authority = authority;
        }

        public AuthoritativeStore Store { get; }
        public RunRecord Run { get; }
        public ManagedRunExecutor Executor { get; }
        public CoordinatorAuthority Authority { get; }

        public static async Task<EndToEndContext> CreateAsync(string fixtureId)
        {
            return await CreateAsync(BethesdaSemanticTestSnapshot.Create(fixtureId));
        }

        public static async Task<EndToEndContext> CreateAsync(BethesdaSemanticRequest source)
        {
            string root = TemporaryRoot();
            AuthoritativeStore store = new(new StoragePaths(root));
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                "bethesda-e2e",
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5));
            PublishSnapshot(store, authority, source.AcceptedSnapshot);
            string snapshotId = source.AcceptedSnapshot.Snapshot!.Contract.SnapshotId.Value;
            RunRecord run = store.CreateRun(
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"),
                new RunBinding(snapshotId, "context-a", "config-a", "manifest-a"),
                authority.FencingEpoch,
                DateTimeOffset.UtcNow);
            RuntimeDescriptor descriptor = RuntimeDescriptor.Create(
                authority.InstanceId,
                authority.FencingEpoch,
                Environment.ProcessId,
                elevated: false,
                DateTimeOffset.UtcNow);
            CoordinatorRuntime runtime = new(store, authority, descriptor);
            WorkerBootstrapRegistry registry = new();
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Services.AddSingleton(runtime);
            builder.Services.AddSingleton(registry);
            builder.Services.AddGrpc(options =>
            {
                options.MaxReceiveMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
                options.MaxSendMessageSize = checked((int)ProtocolConstants.MaximumMessageBytes);
            });
            builder.WebHost.UseNamedPipes(options => options.CurrentUserOnly = true);
            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenNamedPipe(descriptor.WorkerPipe, listen =>
                {
                    listen.Protocols = HttpProtocols.Http2;
                    listen.Use(next => connection =>
                    {
                        connection.Features.Set(new InfiniumPipeRoleFeature("worker", descriptor.WorkerPipe));
                        return next(connection);
                    });
                });
            });
            WebApplication app = builder.Build();
            app.MapGrpcService<WorkerGrpcService>();
            await app.StartAsync();
            ManagedRunExecutor executor = new(
                runtime,
                registry,
                app.Services.GetRequiredService<ILogger<ManagedRunExecutor>>());
            return new EndToEndContext(root, store, run, executor, app, authority);
        }

        private static void PublishSnapshot(
            AuthoritativeStore store,
            CoordinatorAuthority authority,
            Infinium.Mo2.Mo2SnapshotCaptureResult capture)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            const string request = "{}";
            SnapshotCaptureOperationRecord operation = store.CreateSnapshotCaptureOperation(
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"),
                request,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(request))),
                "EvaluationHarness",
                now.AddMinutes(1),
                authority.FencingEpoch,
                now);
            SnapshotCaptureAttemptRecord attempt = store.DispatchSnapshotCaptureAttempt(
                operation.OperationId,
                operation.Generation,
                authority.FencingEpoch,
                TimeSpan.FromMinutes(2),
                now);
            using AttemptStagingAuthority staging = store.Paths.CreateAttemptStagingDirectory(attempt.AttemptId);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(capture);
            const string output = "mo2-snapshot.v3.json";
            File.WriteAllBytes(Path.Combine(store.Paths.Staging, attempt.AttemptId, output), bytes);
            string sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            _ = store.AdmitSnapshotCapturePayload(
                attempt,
                output,
                sha256,
                bytes.LongLength,
                new string('a', 64),
                64L * 1024 * 1024,
                capture.Snapshot!.Contract.SnapshotId.Value,
                "snapshot-capture-result",
                now);
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync();
            await app.DisposeAsync();
            Store.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

#pragma warning restore CA1416
