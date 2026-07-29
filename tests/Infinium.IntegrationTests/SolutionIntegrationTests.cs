using System.Diagnostics;
using System.Text.Json;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Application.V1;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;
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
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public void RuntimeEntryPointsFailClosedOutsideTheirTypedAuthority()
    {
        ProcessResult cli = Run("Infinium.Cli", []);
        Assert.AreEqual(2, cli.ExitCode);
        StringAssert.Contains(cli.Error, "Usage:");

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
                    "--json",
                ]);
            Assert.AreEqual(0, cancellable.ExitCode, cancellable.Error);
            using JsonDocument cancellableJson = JsonDocument.Parse(cancellable.Output);
            string cancellableRunId =
                cancellableJson.RootElement.GetProperty("runId").GetString()!;
            ProcessResult cancel = Run(
                "Infinium.Cli",
                ["--root", root, "cancel", cancellableRunId, "--json"]);
            Assert.AreEqual(0, cancel.ExitCode, cancel.Error);
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

            await AssertIpcRoleVersionNonceAndBoundariesAsync(root).ConfigureAwait(false);
        }
        finally
        {
            StopCoordinator(root, coordinatorProcessId);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Fault")]
    public void CoordinatorRestartFencesInterruptedWorkerAndRecoversDurableRun()
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
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task AssertIpcRoleVersionNonceAndBoundariesAsync(string root)
    {
        RuntimeDescriptor descriptor = RuntimeDescriptor.Read(root);
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

            WorkerService.WorkerServiceClient wrongRoleWorker = new(applicationChannel);
            HandshakeResponse wrongWorkerEndpoint = await wrongRoleWorker.NegotiateAsync(
                new WorkerHandshakeRequest
                {
                    SupportedProtocol = new ProtocolVersionRange
                    {
                        Major = ProtocolConstants.Major,
                        MinimumMinor = 0,
                        MaximumMinor = 0,
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

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
