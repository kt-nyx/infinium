using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class SolutionIntegrationTests
{
    [TestMethod]
    [TestCategory("M1Integration")]
    [TestProperty("Category", "M1Integration")]
    public void SolutionContainsEveryDeclaredProject()
    {
        string solution = TestRepository.Read("Infinium.sln");
        string[] projectFiles = TestRepository
            .EnumerateProjectFiles()
            .Select(path => Path.GetRelativePath(TestRepository.Root, path).Replace('/', '\\'))
            .Where(path => !StringComparer.Ordinal.Equals(
                path,
                "tests\\Infinium.Protocol4RegressionTests\\Infinium.Protocol4RegressionTests.csproj"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(15, projectFiles);
        foreach (string projectFile in projectFiles)
        {
            StringAssert.Contains(solution, $"\"{projectFile}\"");
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestProperty("Category", "M1Integration")]
    public void EveryProjectHasARestoreLock()
    {
        string[] projectDirectories = TestRepository
            .EnumerateProjectFiles()
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .ToArray();

        foreach (string projectDirectory in projectDirectories)
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(projectDirectory, "packages.lock.json")),
                $"Project '{projectDirectory}' does not have a restore lock.");
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Evaluation")]
    public void RunOutputClassifiesEveryLifecycleUnitWithoutUnsupportedAuditClaims()
    {
        foreach (Infinium.Domain.Contracts.LifecycleState state
                 in Enum.GetValues<Infinium.Domain.Contracts.LifecycleState>())
        {
            RunRecord run = new(
                "run-output",
                new RunBinding("snapshot", "context", "configuration", "manifest"),
                state,
                1,
                1,
                1,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);
            RunDetail detail = ProtoMapping.ToDetail(run);
            Assert.AreEqual(ReplayabilityState.Unavailable, detail.ReplayabilityState);
            Assert.AreEqual(AuditabilityState.CompleteWithGaps, detail.AuditabilityState);
            ProgressSummary progress = detail.Summary.Progress;
            ulong classified = progress.CompletedUnits
                + progress.ReusedUnits
                + progress.QueuedUnits
                + progress.RunningUnits
                + progress.FailedUnits
                + progress.SkippedUnits
                + progress.UnsupportedUnits
                + progress.LimitedUnits
                + progress.InvalidatedUnits
                + progress.GapUnits;
            Assert.AreEqual(1UL, classified, state.ToString());
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public void RuntimeEntryPointsFailClosedOutsideTheirTypedAuthority()
    {
        ProcessResult cli = Run("Infinium.Cli", []);
        Assert.AreEqual(2, cli.ExitCode);
        StringAssert.Contains(cli.Error, "Usage:");
        string parserRoot = Path.Combine(Path.GetTempPath(), $"infinium-cli-parser-{Guid.NewGuid():N}");
        ProcessResult unknownOption = Run(
            "Infinium.Cli",
            ["--root", parserRoot, "status", "run-a", "--unknown"]);
        Assert.AreEqual(1, unknownOption.ExitCode);
        StringAssert.Contains(unknownOption.Error, "Unknown option");
        ProcessResult duplicateOption = Run(
            "Infinium.Cli",
            ["--root", parserRoot, "status", "run-a", "--root", parserRoot]);
        Assert.AreEqual(1, duplicateOption.ExitCode);
        StringAssert.Contains(duplicateOption.Error, "only once");

        ProcessResult coordinator = Run("Infinium.Coordinator", []);
        Assert.AreEqual(2, coordinator.ExitCode);
        StringAssert.Contains(coordinator.Error, "--root");

        ProcessResult worker = Run("Infinium.Worker", []);
        Assert.AreEqual(2, worker.ExitCode);
        StringAssert.Contains(worker.Error, "coordinator-launched only");

        ProcessResult helper = Run("Infinium.CredentialHelper", []);
        Assert.AreEqual(1, helper.ExitCode);
        StringAssert.Contains(helper.Error, "Slice 0 scaffold");
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Evaluation")]
    public async Task CliCoordinatorWorkerNamedPipeFlowCompletesAndInspectsImmutableBindings()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-ipc-fixture-{Guid.NewGuid():N}");
        int coordinatorProcessId = 0;
        try
        {
            ProcessResult start = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "start",
                    "--snapshot", "snapshot-a",
                    "--context", "context-a",
                    "--configuration", "configuration-a",
                    "--manifest", "manifest-a",
                    "--json",
                ],
                timeoutMilliseconds: 30_000);
            Assert.AreEqual(0, start.ExitCode, start.Error);
            using JsonDocument startJson = JsonDocument.Parse(start.Output);
            string runId = startJson.RootElement.GetProperty("runId").GetString()!;

            ProcessResult wait = Run(
                "Infinium.Cli",
                ["--root", root, "wait", runId, "--timeout-seconds", "30", "--json"],
                timeoutMilliseconds: 40_000);
            Assert.AreEqual(0, wait.ExitCode, wait.Error);
            using JsonDocument waitJson = JsonDocument.Parse(wait.Output);
            Assert.AreEqual("Completed", waitJson.RootElement.GetProperty("state").GetString());

            ProcessResult inspect = Run(
                "Infinium.Cli",
                ["--root", root, "inspect", runId, "--json"]);
            Assert.AreEqual(0, inspect.ExitCode, inspect.Error);
            using JsonDocument inspectJson = JsonDocument.Parse(inspect.Output);
            JsonElement bindings = inspectJson.RootElement.GetProperty("immutableBindings");
            Assert.AreEqual("snapshot-a", bindings.GetProperty("installationSnapshotId").GetString());
            Assert.AreEqual("context-a", bindings.GetProperty("analysisContextId").GetString());
            Assert.AreEqual(
                "configuration-a",
                bindings.GetProperty("effectiveScanConfigurationId").GetString());
            Assert.AreEqual(
                "manifest-a",
                bindings.GetProperty("resolvedInputManifestId").GetString());

            string descriptorPath = Path.Combine(root, "runtime", "coordinator.v1.json");
            using JsonDocument descriptor = JsonDocument.Parse(File.ReadAllText(descriptorPath));
            coordinatorProcessId = descriptor.RootElement.GetProperty("ProcessId").GetInt32();
            Assert.AreEqual(
                "standard-user",
                descriptor.RootElement.GetProperty("Elevation").GetString());
            StringAssert.EndsWith(
                descriptor.RootElement.GetProperty("ApplicationPipe").GetString()!,
                "-application");
            StringAssert.EndsWith(
                descriptor.RootElement.GetProperty("WorkerPipe").GetString()!,
                "-worker");
            Assert.AreNotEqual(
                descriptor.RootElement.GetProperty("ApplicationPipe").GetString(),
                descriptor.RootElement.GetProperty("WorkerPipe").GetString());

            ProcessResult competingCoordinator = Run(
                "Infinium.Coordinator",
                ["--root", root]);
            Assert.AreEqual(4, competingCoordinator.ExitCode);
            StringAssert.Contains(competingCoordinator.Error, "already owns");

            ProcessResult cancellable = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "start",
                    "--snapshot", "snapshot-cancel",
                    "--context", "context-cancel",
                    "--configuration", "configuration-cancel",
                    "--manifest", "manifest-cancel",
                    "--command-id", "start-cancellable-command",
                    "--json",
                ]);
            Assert.AreEqual(0, cancellable.ExitCode, cancellable.Error);
            using JsonDocument cancellableJson = JsonDocument.Parse(cancellable.Output);
            string cancellableRunId =
                cancellableJson.RootElement.GetProperty("runId").GetString()!;
            ProcessResult crossKindReplay = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "cancel", cancellableRunId,
                    "--command-id", "start-cancellable-command",
                    "--json",
                ]);
            Assert.AreNotEqual(0, crossKindReplay.ExitCode);
            StringAssert.Contains(
                crossKindReplay.Error,
                "already bound to different command inputs");

            await WaitForRunStateAsync(
                root,
                cancellableRunId,
                LifecycleState.Running,
                TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            ProcessResult cancel = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "cancel", cancellableRunId,
                    "--command-id", "cancel-cancellable-command",
                    "--json",
                ]);
            Assert.AreEqual(0, cancel.ExitCode, cancel.Error);
            ProcessResult cancellingInspect = Run(
                "Infinium.Cli",
                ["--root", root, "inspect", cancellableRunId, "--json"]);
            using (JsonDocument cancellingJson = JsonDocument.Parse(cancellingInspect.Output))
            {
                string? state = cancellingJson.RootElement
                    .GetProperty("lifecycle")
                    .GetProperty("state")
                    .GetString();
                Assert.IsTrue(
                    state is "Cancelling" or "Cancelled",
                    $"Cancellation returned unexpected state '{state}'.");
            }

            await Task.Delay(2_500).ConfigureAwait(false);
            ProcessResult cancelledInspect = Run(
                "Infinium.Cli",
                ["--root", root, "inspect", cancellableRunId, "--json"]);
            using JsonDocument cancelledJson = JsonDocument.Parse(cancelledInspect.Output);
            Assert.AreEqual(
                "Cancelled",
                cancelledJson.RootElement
                    .GetProperty("lifecycle")
                    .GetProperty("state")
                    .GetString());
            ProcessResult replayedCancel = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "cancel", cancellableRunId,
                    "--command-id", "cancel-cancellable-command",
                    "--json",
                ]);
            Assert.AreEqual(0, replayedCancel.ExitCode, replayedCancel.Error);
            using JsonDocument replayedCancelJson = JsonDocument.Parse(replayedCancel.Output);
            Assert.AreEqual(
                "AlreadyAccepted",
                replayedCancelJson.RootElement.GetProperty("disposition").GetString());

            await AssertIpcRoleVersionNonceAndBoundariesAsync(root).ConfigureAwait(false);
        }
        finally
        {
            StopCoordinator(root, coordinatorProcessId);
            if (Directory.Exists(root))
            {
                DeleteDirectoryAfterWorkerRelease(root);
            }
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Fault")]
    public async Task CoordinatorRestartFencesInterruptedWorkerAndRecoversDurableRun()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-recovery-fixture-{Guid.NewGuid():N}");
        int firstCoordinatorProcessId = 0;
        int recoveredCoordinatorProcessId = 0;
        try
        {
            ProcessResult start = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "start",
                    "--snapshot", "snapshot-recovery",
                    "--context", "context-recovery",
                    "--configuration", "configuration-recovery",
                    "--manifest", "manifest-recovery",
                    "--json",
                ],
                timeoutMilliseconds: 30_000);
            Assert.AreEqual(0, start.ExitCode, start.Error);
            using JsonDocument startJson = JsonDocument.Parse(start.Output);
            string runId = startJson.RootElement.GetProperty("runId").GetString()!;

            await WaitForRunStateAsync(
                root,
                runId,
                LifecycleState.Running,
                TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            RuntimeDescriptor first = RuntimeDescriptor.Read(root);
            firstCoordinatorProcessId = first.ProcessId;
            StopProcess(firstCoordinatorProcessId);
            firstCoordinatorProcessId = 0;

            ProcessResult wait = Run(
                "Infinium.Cli",
                ["--root", root, "wait", runId, "--timeout-seconds", "30", "--json"],
                timeoutMilliseconds: 40_000);
            Assert.AreEqual(0, wait.ExitCode, wait.Error);
            using JsonDocument waitJson = JsonDocument.Parse(wait.Output);
            Assert.AreEqual("Completed", waitJson.RootElement.GetProperty("state").GetString());
            Assert.IsTrue(
                waitJson.RootElement.GetProperty("generation").GetUInt64() >= 4);

            RuntimeDescriptor recovered = RuntimeDescriptor.Read(root);
            recoveredCoordinatorProcessId = recovered.ProcessId;
            Assert.AreNotEqual(first.ProcessId, recovered.ProcessId);
            Assert.IsTrue(recovered.FencingEpoch > first.FencingEpoch);
        }
        finally
        {
            StopProcess(firstCoordinatorProcessId);
            StopCoordinator(root, recoveredCoordinatorProcessId);
            if (Directory.Exists(root))
            {
                DeleteDirectoryAfterWorkerRelease(root);
            }
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Fault")]
    public async Task CoordinatorRestartObservesPendingCancellationAndSettlesAttempt()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-cancel-recovery-{Guid.NewGuid():N}");
        int coordinatorProcessId = 0;
        try
        {
            ProcessResult start = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "start",
                    "--snapshot", "snapshot-cancel-recovery",
                    "--context", "context-cancel-recovery",
                    "--configuration", "configuration-cancel-recovery",
                    "--manifest", "manifest-cancel-recovery",
                    "--json",
                ],
                timeoutMilliseconds: 30_000);
            Assert.AreEqual(0, start.ExitCode, start.Error);
            using JsonDocument startJson = JsonDocument.Parse(start.Output);
            string runId = startJson.RootElement.GetProperty("runId").GetString()!;
            await WaitForRunStateAsync(
                root,
                runId,
                LifecycleState.Running,
                TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            ProcessResult cancel = Run(
                "Infinium.Cli",
                [
                    "--root", root,
                    "cancel", runId,
                    "--command-id", "cancel-before-restart",
                    "--json",
                ]);
            Assert.AreEqual(0, cancel.ExitCode, cancel.Error);
            RuntimeDescriptor beforeRestart = RuntimeDescriptor.Read(root);
            coordinatorProcessId = beforeRestart.ProcessId;
            StopProcess(coordinatorProcessId);
            coordinatorProcessId = 0;

            ProcessResult inspect = Run(
                "Infinium.Cli",
                ["--root", root, "inspect", runId, "--json"],
                timeoutMilliseconds: 30_000);
            Assert.AreEqual(0, inspect.ExitCode, inspect.Error);
            using JsonDocument inspectJson = JsonDocument.Parse(inspect.Output);
            Assert.AreEqual(
                "Cancelled",
                inspectJson.RootElement
                    .GetProperty("lifecycle")
                    .GetProperty("state")
                    .GetString());
            RuntimeDescriptor afterRestart = RuntimeDescriptor.Read(root);
            coordinatorProcessId = afterRestart.ProcessId;
            Assert.IsGreaterThan(beforeRestart.FencingEpoch, afterRestart.FencingEpoch);

            StopCoordinator(root, coordinatorProcessId);
            coordinatorProcessId = 0;
            using AuthoritativeStore store = new(new StoragePaths(root));
            Assert.IsFalse(store.HasLiveAttempts(runId));
        }
        finally
        {
            StopCoordinator(root, coordinatorProcessId);
            if (Directory.Exists(root))
            {
                DeleteDirectoryAfterWorkerRelease(root);
            }
        }
    }

#pragma warning disable CA1416 // This integration helper is explicitly Windows-gated above.
    private static async Task AssertIpcRoleVersionNonceAndBoundariesAsync(string root)
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The Slice 2 named-pipe integration contract requires Windows.");
        }

        RuntimeDescriptor descriptor = RuntimeDescriptor.Read(root);
        using (NamedPipeClientStream securityProbe = new(
            ".",
            descriptor.ApplicationPipe,
            PipeDirection.InOut,
            PipeOptions.Asynchronous))
        {
            await securityProbe.ConnectAsync(5_000).ConfigureAwait(false);
            PipeSecurity security = securityProbe.GetAccessControl();
            AuthorizationRuleCollection rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: false,
                typeof(SecurityIdentifier));
            SecurityIdentifier currentUser = WindowsIdentity.GetCurrent().User!;
            Assert.IsTrue(rules.OfType<PipeAccessRule>().Any(rule =>
                rule.AccessControlType == AccessControlType.Allow
                && currentUser.Equals(rule.IdentityReference)
                && (rule.PipeAccessRights & PipeAccessRights.ReadWrite) != 0));
            SecurityIdentifier network =
                new(WellKnownSidType.NetworkSid, domainSid: null);
            Assert.IsTrue(rules.OfType<PipeAccessRule>().Any(rule =>
                rule.AccessControlType == AccessControlType.Deny
                && network.Equals(rule.IdentityReference)));
        }

        using (GrpcChannel unauthenticatedChannel =
            NamedPipeGrpcChannel.Create(descriptor.ApplicationPipe))
        {
            ApplicationService.ApplicationServiceClient unauthenticated = new(unauthenticatedChannel);
            RpcException exception = await Assert.ThrowsExactlyAsync<RpcException>(
                async () =>
                {
                    _ = await unauthenticated.HealthAsync(new HealthRequest()).ResponseAsync;
                });
            Assert.AreEqual(StatusCode.Unauthenticated, exception.StatusCode);
        }

        using (GrpcChannel applicationChannel =
            NamedPipeGrpcChannel.Create(descriptor.ApplicationPipe))
        {
            ApplicationService.ApplicationServiceClient application = new(applicationChannel);
            ApplicationHandshakeRequest badNonce = ApplicationHandshake(descriptor);
            badNonce.CoordinatorInstanceNonce = ByteString.CopyFrom(new byte[32]);
            HandshakeResponse nonceResponse =
                await application.NegotiateAsync(badNonce).ResponseAsync;
            Assert.AreEqual(HandshakeDisposition.InvalidNonce, nonceResponse.Disposition);

            ApplicationHandshakeRequest badMajor = ApplicationHandshake(descriptor);
            badMajor.SupportedProtocol.Major++;
            HandshakeResponse majorResponse =
                await application.NegotiateAsync(badMajor).ResponseAsync;
            Assert.AreEqual(HandshakeDisposition.IncompatibleMajor, majorResponse.Disposition);

            ApplicationHandshakeRequest unknownClient = ApplicationHandshake(descriptor);
            unknownClient.ClientKind = ApplicationClientKind.Unknown;
            HandshakeResponse unknownClientResponse =
                await application.NegotiateAsync(unknownClient).ResponseAsync;
            Assert.AreEqual(
                HandshakeDisposition.UnsupportedCapability,
                unknownClientResponse.Disposition);

            HandshakeResponse accepted =
                await application.NegotiateAsync(ApplicationHandshake(descriptor)).ResponseAsync;
            Assert.AreEqual(HandshakeDisposition.Accepted, accepted.Disposition);
            ListRunsResponse bounded = await application.ListRunsAsync(new ListRunsRequest
            {
                RequestedPageSize = ProtocolConstants.MaximumPageItems + 1,
            }).ResponseAsync;
            Assert.AreEqual(FailureCode.LimitExceeded, bounded.Failure.Code);
            ListRunsResponse firstPage = await application.ListRunsAsync(new ListRunsRequest
            {
                RequestedPageSize = 1,
            }).ResponseAsync;
            Assert.IsTrue(firstPage.Page.HasMore);
            Assert.HasCount(1, firstPage.Page.Items);
            Assert.IsTrue(firstPage.Page.Next.OpaqueValue.Length > 32);
            ListRunsResponse secondPage = await application.ListRunsAsync(new ListRunsRequest
            {
                RequestedPageSize = 1,
                After = firstPage.Page.Next,
                ExpectedProjectionVersion = firstPage.Page.ProjectionVersion,
            }).ResponseAsync;
            Assert.HasCount(1, secondPage.Page.Items);
            Assert.AreNotEqual(
                firstPage.Page.Items[0].RunId.Value,
                secondPage.Page.Items[0].RunId.Value);
            PageCursor tampered = firstPage.Page.Next.Clone();
            tampered.OpaqueValue = ByteString.CopyFrom(
                tampered.OpaqueValue.ToByteArray().Select((value, index) =>
                    index == 0 ? (byte)(value ^ 0xff) : value).ToArray());
            ListRunsResponse rejectedCursor = await application.ListRunsAsync(new ListRunsRequest
            {
                RequestedPageSize = 1,
                After = tampered,
            }).ResponseAsync;
            Assert.AreEqual(
                CursorDisposition.Malformed,
                rejectedCursor.CursorRejection.Disposition);

            string streamRunId = firstPage.Page.Items[0].RunId.Value;
            GetRunResponse staleProjection = await application.GetRunAsync(new GetRunRequest
            {
                RunId = new RunId { Value = streamRunId },
                ExpectedProjectionVersion = new ProjectionVersion { Value = "obsolete" },
            }).ResponseAsync;
            Assert.AreEqual(
                GetRunResponse.ResultOneofCase.ProjectionInvalidated,
                staleProjection.ResultCase);

            EventCursor resume;
            LifecycleState stateBeforeTransportCancel;
            using (CancellationTokenSource streamCancellation = new(TimeSpan.FromSeconds(5)))
            using (AsyncServerStreamingCall<ApplicationEvent> stream =
                application.SubscribeEvents(
                    new SubscribeEventsRequest
                    {
                        SubscriptionId = new SubscriptionId { Value = Guid.NewGuid().ToString("N") },
                        RequestedQueueItems = 2,
                        RunScope = { new RunId { Value = streamRunId } },
                        ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
                    },
                    cancellationToken: streamCancellation.Token))
            {
                Assert.IsTrue(await stream.ResponseStream.MoveNext(streamCancellation.Token));
                ApplicationEvent firstEvent = stream.ResponseStream.Current;
                Assert.AreEqual(EventKind.Progress, firstEvent.Kind);
                Assert.IsTrue(firstEvent.ResumeCursor.OpaqueValue.Length > 32);
                resume = firstEvent.ResumeCursor.Clone();
                stateBeforeTransportCancel = firstEvent.Progress.LifecycleState;
                streamCancellation.Cancel();
            }

            EventCursor invalidResume = resume.Clone();
            byte[] invalidResumeBytes = invalidResume.OpaqueValue.ToByteArray();
            invalidResumeBytes[0] ^= 0xff;
            invalidResume.OpaqueValue = ByteString.CopyFrom(invalidResumeBytes);
            using (CancellationTokenSource resyncCancellation = new(TimeSpan.FromSeconds(5)))
            using (AsyncServerStreamingCall<ApplicationEvent> resync =
                application.SubscribeEvents(
                    new SubscribeEventsRequest
                    {
                        SubscriptionId = new SubscriptionId { Value = Guid.NewGuid().ToString("N") },
                        RequestedQueueItems = 2,
                        RunScope = { new RunId { Value = streamRunId } },
                        After = invalidResume,
                        ExpectedProjectionVersion = new ProjectionVersion { Value = "1" },
                    },
                    cancellationToken: resyncCancellation.Token))
            {
                Assert.IsTrue(await resync.ResponseStream.MoveNext(resyncCancellation.Token));
                Assert.AreEqual(EventKind.ResyncRequired, resync.ResponseStream.Current.Kind);
                Assert.AreEqual(
                    ResyncReason.CursorInvalid,
                    resync.ResponseStream.Current.ResyncRequired.Reason);
            }

            GetRunResponse afterTransportCancel = await application.GetRunAsync(new GetRunRequest
            {
                RunId = new RunId { Value = streamRunId },
            }).ResponseAsync;
            Assert.AreEqual(
                stateBeforeTransportCancel,
                afterTransportCancel.Run.Summary.LifecycleState);

            WorkerService.WorkerServiceClient wrongRoleWorker = new(applicationChannel);
            HandshakeResponse wrongWorkerEndpoint = await wrongRoleWorker.NegotiateAsync(
                new WorkerHandshakeRequest
                {
                    SupportedProtocol = new ProtocolVersionRange
                    {
                        Major = ProtocolConstants.Major,
                        MinimumMinor = ProtocolConstants.Minor,
                        MaximumMinor = ProtocolConstants.Minor,
                    },
                }).ResponseAsync;
            Assert.AreEqual(
                HandshakeDisposition.WrongEndpoint,
                wrongWorkerEndpoint.Disposition);
        }

        using GrpcChannel workerChannel =
            NamedPipeGrpcChannel.Create(descriptor.WorkerPipe);
        ApplicationService.ApplicationServiceClient wrongRoleApplication = new(workerChannel);
        HandshakeResponse wrongApplicationEndpoint = await wrongRoleApplication.NegotiateAsync(
            ApplicationHandshake(descriptor)).ResponseAsync;
        Assert.AreEqual(
            HandshakeDisposition.WrongEndpoint,
            wrongApplicationEndpoint.Disposition);
    }
#pragma warning restore CA1416

    private static async Task WaitForRunStateAsync(
        string root,
        string runId,
        LifecycleState expected,
        TimeSpan timeout)
    {
        RuntimeDescriptor descriptor = RuntimeDescriptor.Read(root);
        using GrpcChannel channel = NamedPipeGrpcChannel.Create(descriptor.ApplicationPipe);
        ApplicationService.ApplicationServiceClient application = new(channel);
        HandshakeResponse accepted =
            await application.NegotiateAsync(ApplicationHandshake(descriptor)).ResponseAsync;
        Assert.AreEqual(HandshakeDisposition.Accepted, accepted.Disposition);
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            GetRunResponse response = await application.GetRunAsync(new GetRunRequest
            {
                RunId = new RunId { Value = runId },
            }).ResponseAsync;
            if (response.Run?.Summary?.LifecycleState == expected)
            {
                return;
            }

            await Task.Delay(25).ConfigureAwait(false);
        }

        Assert.Fail($"Run '{runId}' did not reach {expected} within {timeout}.");
    }

    private static ApplicationHandshakeRequest ApplicationHandshake(RuntimeDescriptor descriptor)
    {
        ApplicationHandshakeRequest request = new()
        {
            SupportedProtocol = new ProtocolVersionRange
            {
                Major = ProtocolConstants.Major,
                MinimumMinor = ProtocolConstants.Minor,
                MaximumMinor = ProtocolConstants.Minor,
            },
            Compatibility = ProtocolConstants.Compatibility,
            ClientKind = ApplicationClientKind.TestHarness,
            CoordinatorInstanceNonce = ByteString.CopyFrom(descriptor.GetNonce()),
        };
        request.RequestedCapabilities.Add(Capability.ApplicationQuery);
        return request;
    }

    private static ProcessResult Run(
        string project,
        IReadOnlyList<string> arguments,
        int timeoutMilliseconds = 15_000)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = TestRepository.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add($"src/{project}");
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--");
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        bool exited = process.WaitForExit(timeoutMilliseconds);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        Assert.IsTrue(exited, $"{project} did not terminate within its bound.");
        Task.WaitAll(output, error);
        return new ProcessResult(process.ExitCode, output.Result, error.Result);
    }

    private static void StopProcess(int processId)
    {
        if (processId <= 0)
        {
            return;
        }

        try
        {
            using Process process = Process.GetProcessById(processId);
            process.Kill();
            Assert.IsTrue(
                process.WaitForExit(5_000),
                $"Coordinator process {processId} did not terminate.");
        }
        catch (ArgumentException)
        {
            // The coordinator already exited.
        }
    }

    private static void StopCoordinator(string root, int knownProcessId)
    {
        int processId = knownProcessId;
        if (processId <= 0)
        {
            try
            {
                processId = RuntimeDescriptor.Read(root).ProcessId;
            }
            catch (IOException)
            {
                // Startup failed before a readable descriptor was committed.
            }
        }

        StopProcess(processId);
    }

    private static void DeleteDirectoryAfterWorkerRelease(string root)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        Exception? lastFailure = null;
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            try
            {
                Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException exception)
            {
                lastFailure = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                lastFailure = exception;
            }

            Thread.Sleep(100);
        }

        throw new IOException(
            $"The temporary integration root remained in use after {timeout.Elapsed}.",
            lastFailure);
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
