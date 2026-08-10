using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Bethesda;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;
using LifecycleState = Infinium.Domain.Contracts.LifecycleState;

namespace Infinium.Tests;

[TestClass]
public sealed class WorkerProtocolIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    public void BethesdaBootstrapProducesOnlyTheClosedTypedIndexAssignment()
    {
        using WorkerContext context = new();
        BethesdaSemanticRequest request = BethesdaSemanticTestSnapshot.Create("BETH-LIGHT-VAL");
        ManagedBethesdaSemanticAssignment semantic = new(request.AcceptedSnapshot, []);
        ManagedWorkerBootstrap bootstrap = context.Bootstrap with
        {
            BootstrapId = "bethesda-bootstrap",
            StagingAreaId = "bethesda-staging",
            StagedArtifactId = "bethesda-artifact",
            OutputRelativeName = "bethesda-semantic.v2.json",
            MaximumOutputBytes = 16 * 1024 * 1024,
            OneUseNonceBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            OperationKind = ManagedWorkerOperationKind.BethesdaSemanticExtraction,
            OutputSchemaVersion = "1.0.0",
            BethesdaSemanticExtraction = semantic,
        };
        WorkerBootstrapRegistry registry = new();
        registry.Register(bootstrap);
        const string connection = "bethesda-worker-connection";
        HandshakeResponse handshake = registry.Negotiate(
            new WorkerHandshakeRequest
            {
                BootstrapId = bootstrap.BootstrapId,
                ProcessId = checked((uint)bootstrap.ExpectedProcessId),
                ExpectedAttemptId = new AttemptId { Value = bootstrap.AttemptId },
                ObservedCoordinatorFencingEpoch = checked((ulong)bootstrap.CoordinatorFencingEpoch),
                SupportedProtocol = new ProtocolVersionRange
                {
                    Major = ProtocolConstants.Major,
                    MinimumMinor = ProtocolConstants.Minor,
                    MaximumMinor = ProtocolConstants.Minor,
                },
                Compatibility = ProtocolConstants.Compatibility,
                OneUseNonce = ByteString.CopyFrom(Convert.FromBase64String(bootstrap.OneUseNonceBase64)),
            },
            connection,
            context.Runtime);
        Assert.AreEqual(HandshakeDisposition.Accepted, handshake.Disposition);

        WorkerAssignment assignment = registry.GetAssignment(
            new ReceiveAssignmentRequest
            {
                BootstrapId = bootstrap.BootstrapId,
                ExpectedAttemptId = new AttemptId { Value = bootstrap.AttemptId },
                ObservedCoordinatorFencingEpoch = checked((ulong)bootstrap.CoordinatorFencingEpoch),
            },
            connection,
            context.Runtime);

        Assert.AreEqual(WorkerOperationKind.BuildTypedIndex, assignment.Operation.Kind);
        Assert.AreEqual(BethesdaSemanticExtractor.ProducerId, assignment.Operation.AdapterOrAnalyzerId);
        Assert.AreEqual(BethesdaSemanticExtractor.ProducerVersion, assignment.Operation.AdapterOrAnalyzerVersion.Value);
        Assert.AreEqual(0, assignment.Inputs.Count);
        Assert.AreEqual("bethesda-semantic.v2.json", assignment.StagingAuthority.AllowedOutputs.Single().TypedRelativeName);
        Assert.AreEqual(StagedArtifactKind.TypedResult, assignment.StagingAuthority.AllowedOutputs.Single().Kind);
        Assert.IsNull(assignment.Operation.Mo2SnapshotCapture);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public async Task WorkerOutputReaderEnforcesUtf8ByteBoundsWhileStreaming()
    {
        byte[] acceptedBytes = Encoding.UTF8.GetBytes("bounded-é");
        using MemoryStream acceptedStream = new(acceptedBytes);
        using StreamReader acceptedReader = new(acceptedStream, Encoding.UTF8);
        Assert.AreEqual(
            "bounded-é",
            await ManagedRunExecutor.ReadBoundedAsync(
                acceptedReader,
                acceptedBytes.Length,
                "test output").ConfigureAwait(false));

        byte[] oversizedBytes = Encoding.UTF8.GetBytes("éé");
        using MemoryStream oversizedStream = new(oversizedBytes);
        using StreamReader oversizedReader = new(oversizedStream, Encoding.UTF8);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => ManagedRunExecutor.ReadBoundedAsync(
                oversizedReader,
                oversizedBytes.Length - 1,
                "test output")).ConfigureAwait(false);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    public async Task PipePeerValidationUsesTheConnectedClientProcessToken()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Named-pipe process-token validation is Windows-specific.");
        }

        string pipeName = $"infinium-peer-validation-{Guid.NewGuid():N}";
        using NamedPipeServerStream server = new(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
        using NamedPipeClientStream client = new(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        Task wait = server.WaitForConnectionAsync();
        await client.ConnectAsync(5_000).ConfigureAwait(false);
        await wait.ConfigureAwait(false);

        Assert.IsTrue(
            WindowsPipePeerValidator.IsCurrentUserPeer(server, out string acceptedReason),
            acceptedReason);
        Assert.IsFalse(
            WindowsPipePeerValidator.IsCurrentUserProcess(uint.MaxValue, out string rejectedReason));
        Assert.IsFalse(string.IsNullOrWhiteSpace(rejectedReason));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    public void RuntimeAdmissionBoundsAreFiniteAndCapacityIsReleased()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-admission-{Guid.NewGuid():N}");
        try
        {
            using AuthoritativeStore store = new(new StoragePaths(root));
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                "coordinator-admission",
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5));
            RuntimeDescriptor descriptor = RuntimeDescriptor.Create(
                authority.InstanceId,
                authority.FencingEpoch,
                Environment.ProcessId,
                elevated: false,
                DateTimeOffset.UtcNow);
            CoordinatorRuntime runtime = new(store, authority, descriptor);

            for (int index = 0; index < CoordinatorRuntime.MaximumApplicationConnections; index++)
            {
                Assert.IsTrue(runtime.TryAdmitApplicationConnection($"application-{index}"));
            }

            Assert.IsFalse(runtime.TryAdmitApplicationConnection("application-overflow"));
            runtime.ReleaseApplicationConnection("application-0");
            Assert.IsTrue(runtime.TryAdmitApplicationConnection("application-replacement"));

            for (int index = 0; index < CoordinatorRuntime.MaximumWorkerConnections; index++)
            {
                Assert.IsTrue(runtime.TryAdmitWorkerConnection($"worker-{index}"));
            }

            Assert.IsFalse(runtime.TryAdmitWorkerConnection("worker-overflow"));
            runtime.ReleaseWorkerConnection("worker-0");
            Assert.IsTrue(runtime.TryAdmitWorkerConnection("worker-replacement"));

            for (int index = 0; index < CoordinatorRuntime.MaximumEventSubscriptions; index++)
            {
                Assert.IsTrue(runtime.TryAdmitEventSubscription());
            }

            Assert.IsFalse(runtime.TryAdmitEventSubscription());
            runtime.ReleaseEventSubscription();
            Assert.IsTrue(runtime.TryAdmitEventSubscription());

            DateTimeOffset admissionTime = DateTimeOffset.UtcNow;
            for (int index = 0;
                 index < CoordinatorRuntime.MaximumNewDurableCommandsPerMinute;
                 index++)
            {
                Assert.IsTrue(runtime.TryAdmitNewDurableCommand(admissionTime));
            }

            Assert.IsFalse(runtime.TryAdmitNewDurableCommand(admissionTime));
            Assert.IsTrue(runtime.TryAdmitNewDurableCommand(admissionTime.AddMinutes(1)));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    public async Task SuspendedWorkerUsesOnlyDeclaredHandlesAndOriginalDirectoryObject()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The contained worker contract is Windows-specific.");
        }

        string root = Path.Combine(
            Path.GetTempPath(),
            $"infinium-contained-worker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string stagingPath = Path.Combine(root, "staging");
        string movedStagingPath = Path.Combine(root, "staging-original");
        string includedPath = Path.Combine(root, "included.canary");
        string excludedPath = Path.Combine(root, "excluded.canary");
        Directory.CreateDirectory(stagingPath);
        File.WriteAllText(includedPath, "included");
        File.WriteAllText(excludedPath, "excluded");
        try
        {
            using SafeFileHandle stagingHandle = CreateFileW(
                stagingPath,
                FILE_LIST_DIRECTORY | FILE_ADD_FILE | SYNCHRONIZE,
                FileShare.ReadWrite,
                0,
                FileMode.Open,
                FILE_FLAG_BACKUP_SEMANTICS,
                0);
            Assert.IsFalse(stagingHandle.IsInvalid);
            using SafeFileHandle included = File.OpenHandle(
                includedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using SafeFileHandle excluded = File.OpenHandle(
                excludedPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            Assert.IsTrue(SetHandleInformation(
                excluded.DangerousGetHandle(),
                HANDLE_FLAG_INHERIT,
                HANDLE_FLAG_INHERIT));

            string workerAssembly = Path.Combine(
                AppContext.BaseDirectory,
                "Infinium.Worker.dll");
            string dotnet = Path.GetFullPath(Path.Combine(
                RuntimeEnvironment.GetRuntimeDirectory(),
                "..",
                "..",
                "..",
                "dotnet.exe"));
            Dictionary<string, string> environment = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SystemRoot"] =
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                ["DOTNET_NOLOGO"] = "1",
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            };
            using WindowsContainedWorkerProcess contained =
                WindowsContainedWorkerProcess.Create(
                    dotnet,
                    [workerAssembly, "containment-probe"],
                    AppContext.BaseDirectory,
                    environment,
                    stagingHandle,
                    [included.DangerousGetHandle()]);
            byte[] bootstrap = JsonSerializer.SerializeToUtf8Bytes(new
            {
                stagingHandle = contained.InheritedStagingDirectoryHandle.ToInt64(),
                outputName = "probe.json",
                includedHandle = included.DangerousGetHandle().ToInt64(),
                excludedHandle = excluded.DangerousGetHandle().ToInt64(),
            });
            await contained.BootstrapInput.WriteAsync(bootstrap).ConfigureAwait(false);
            contained.BootstrapInput.Close();

            Assert.Throws<IOException>(
                () => Directory.Move(stagingPath, movedStagingPath));
            contained.Resume();
            Task<string> error = contained.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
            await contained.Process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            Assert.AreEqual(0, contained.Process.ExitCode, await error.ConfigureAwait(false));

            string movedOutput = Path.Combine(stagingPath, "probe.json");
            Assert.IsTrue(File.Exists(movedOutput));
            Assert.IsFalse(Directory.Exists(movedStagingPath));
            using JsonDocument result = JsonDocument.Parse(
                await File.ReadAllBytesAsync(movedOutput).ConfigureAwait(false));
            Assert.IsTrue(
                result.RootElement.GetProperty("jobContainedAtEntry").GetBoolean());
            Assert.AreEqual(
                NormalizeFinalPath(includedPath),
                NormalizeFinalPath(
                    result.RootElement.GetProperty("includedHandlePath").GetString()));
            Assert.AreNotEqual(
                NormalizeFinalPath(excludedPath),
                NormalizeFinalPath(
                    result.RootElement.GetProperty("excludedHandlePath").GetString()));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void WorkerReceiptsEnforceBoundsAndPermitOnlyExactReplay()
    {
        using WorkerContext context = new();
        WorkerProgress valid = context.Progress(sequence: 1, completedWorkUnits: 0);
        WorkerProgressReceipt accepted = context.Registry.AcceptProgress(
            valid,
            context.ConnectionId,
            context.Runtime);
        Assert.AreEqual(
            WorkerReceiptDisposition.AcceptedForStagingOnly,
            accepted.Disposition);

        WorkerProgressReceipt repeated = context.Registry.AcceptProgress(
            valid,
            context.ConnectionId,
            context.Runtime);
        Assert.AreEqual(
            WorkerReceiptDisposition.RejectedAssignmentMismatch,
            repeated.Disposition);
        Assert.AreEqual(1UL, repeated.AcceptedProgressSequence);

        WorkerProgress oversized = context.Progress(sequence: 2, completedWorkUnits: 0);
        oversized.InertStatusText = new string('x', 4097);
        WorkerProgressReceipt rejectedLimit = context.Registry.AcceptProgress(
            oversized,
            context.ConnectionId,
            context.Runtime);
        Assert.AreEqual(WorkerReceiptDisposition.RejectedLimit, rejectedLimit.Disposition);
        Assert.AreEqual(1UL, rejectedLimit.AcceptedProgressSequence);

        WorkerProgress invalidWork = context.Progress(sequence: 2, completedWorkUnits: 2);
        Assert.AreEqual(
            WorkerReceiptDisposition.RejectedLimit,
            context.Registry.AcceptProgress(
                invalidWork,
                context.ConnectionId,
                context.Runtime).Disposition);

        for (ulong sequence = 2; sequence <= 8; sequence++)
        {
            Assert.AreEqual(
                WorkerReceiptDisposition.AcceptedForStagingOnly,
                context.Registry.AcceptProgress(
                    context.Progress(sequence, sequence == 8 ? 1UL : 0UL),
                    context.ConnectionId,
                    context.Runtime).Disposition);
        }

        WorkerProgressReceipt tooMany = context.Registry.AcceptProgress(
            context.Progress(sequence: 9, completedWorkUnits: 1),
            context.ConnectionId,
            context.Runtime);
        Assert.AreEqual(WorkerReceiptDisposition.RejectedLimit, tooMany.Disposition);
        Assert.AreEqual(8UL, tooMany.AcceptedProgressSequence);

        using (WorkerContext diagnosticContext = new())
        {
            WorkerProgress firstDiagnostic =
                diagnosticContext.Progress(sequence: 1, completedWorkUnits: 0);
            firstDiagnostic.InertStatusText = new string('x', 3000);
            Assert.AreEqual(
                WorkerReceiptDisposition.AcceptedForStagingOnly,
                diagnosticContext.Registry.AcceptProgress(
                    firstDiagnostic,
                    diagnosticContext.ConnectionId,
                    diagnosticContext.Runtime).Disposition);
            WorkerProgress cumulativeDiagnostic =
                diagnosticContext.Progress(sequence: 2, completedWorkUnits: 0);
            cumulativeDiagnostic.InertStatusText = new string('y', 1097);
            Assert.AreEqual(
                WorkerReceiptDisposition.RejectedLimit,
                diagnosticContext.Registry.AcceptProgress(
                    cumulativeDiagnostic,
                    diagnosticContext.ConnectionId,
                    diagnosticContext.Runtime).Disposition);
        }

        StagedOutputManifest manifest = context.Manifest();
        StagedOutputAcceptance first = context.Registry.AcceptStagedOutput(
            new SubmitStagedOutputRequest { Manifest = manifest },
            context.ConnectionId);
        Assert.AreEqual(
            WorkerReceiptDisposition.AcceptedForStagingOnly,
            first.Disposition);

        StagedOutputAcceptance duplicate = context.Registry.AcceptStagedOutput(
            new SubmitStagedOutputRequest { Manifest = manifest.Clone() },
            context.ConnectionId);
        Assert.AreEqual(WorkerReceiptDisposition.Duplicate, duplicate.Disposition);
        Assert.AreEqual(first.ReceiptId, duplicate.ReceiptId);

        StagedOutputManifest changed = manifest.Clone();
        changed.Outputs[0].SchemaVersion.Value = "1.0.1";
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            context.Registry.AcceptStagedOutput(
                new SubmitStagedOutputRequest { Manifest = changed },
                context.ConnectionId));

        WorkerTerminalReceipt terminal = context.Terminal(first.ReceiptId);
        TerminalReceiptAcceptance terminalFirst =
            context.Registry.AcceptTerminal(terminal, context.ConnectionId);
        Assert.AreEqual(
            WorkerReceiptDisposition.AcceptedForStagingOnly,
            terminalFirst.Disposition);
        Assert.AreEqual(
            WorkerReceiptDisposition.Duplicate,
            context.Registry.AcceptTerminal(
                terminal.Clone(),
                context.ConnectionId).Disposition);

        WorkerTerminalReceipt changedTerminal = terminal.Clone();
        changedTerminal.StagingReceiptId = "different";
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            context.Registry.AcceptTerminal(changedTerminal, context.ConnectionId));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void RecoveryFinalizesVerifiedCommittedPublicationWithoutRedispatch()
    {
        using WorkerContext context = new();
        byte[] bytes = "recoverable publication"u8.ToArray();
        string stagingDirectory = Path.Combine(
            context.Store.Paths.Staging,
            context.Attempt.AttemptId);
        Directory.CreateDirectory(stagingDirectory);
        File.WriteAllBytes(Path.Combine(stagingDirectory, "result.bin"), bytes);
        string sha256 =
            Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        _ = context.Store.AdmitStagedPayload(
            context.Attempt,
            "result.bin",
            sha256,
            bytes.LongLength,
            new string('a', 64),
            4096,
            DateTimeOffset.UtcNow);

        ManagedRunExecutor executor = new(
            context.Runtime,
            context.Registry,
            NullLogger<ManagedRunExecutor>.Instance);
        executor.RecoverAtStartup();

        RunRecord recovered = context.Store.GetRun(context.Run.RunId);
        Assert.AreEqual(LifecycleState.Completed, recovered.State);
        Assert.AreEqual(context.Run.Generation + 1, recovered.Generation);
        Assert.IsFalse(context.Store.HasLiveAttempts(context.Run.RunId));
    }

    private sealed class WorkerContext : IDisposable
    {
        private readonly string root =
            Path.Combine(Path.GetTempPath(), $"infinium-worker-protocol-{Guid.NewGuid():N}");

        public WorkerContext()
        {
            Store = new AuthoritativeStore(new StoragePaths(root));
            Authority = Store.AcquireCoordinatorAuthority(
                "coordinator-a",
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5));
            RunRecord queued = Store.CreateRun(
                "command-a",
                "run-a",
                new RunBinding("snapshot-a", "context-a", "config-a", "manifest-a"),
                Authority.FencingEpoch,
                DateTimeOffset.UtcNow);
            Run = Store.Transition(
                "transition-a",
                queued.RunId,
                queued.Generation,
                LifecycleState.Running,
                Authority.FencingEpoch,
                "test dispatch",
                DateTimeOffset.UtcNow);
            Attempt = Store.CreateAttempt(
                Run.RunId,
                Authority.FencingEpoch,
                TimeSpan.FromMinutes(2),
                DateTimeOffset.UtcNow);
            Descriptor = RuntimeDescriptor.Create(
                Authority.InstanceId,
                Authority.FencingEpoch,
                Environment.ProcessId,
                elevated: false,
                DateTimeOffset.UtcNow);
            Runtime = new CoordinatorRuntime(Store, Authority, Descriptor);
            Registry = new WorkerBootstrapRegistry();
            Bootstrap = new ManagedWorkerBootstrap(
                1,
                "bootstrap-a",
                Authority.InstanceId,
                Authority.FencingEpoch,
                Run.RunId,
                Attempt.AttemptId,
                Attempt.AttemptFencingToken,
                Descriptor.WorkerPipe,
                Environment.ProcessId,
                "staging-a",
                "artifact-a",
                1,
                "result.bin",
                4096,
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                DateTimeOffset.UtcNow.AddMinutes(1));
            Registry.Register(Bootstrap);

            HandshakeResponse handshake = Registry.Negotiate(
                new WorkerHandshakeRequest
                {
                    BootstrapId = Bootstrap.BootstrapId,
                    ProcessId = checked((uint)Bootstrap.ExpectedProcessId),
                    ExpectedAttemptId = new AttemptId { Value = Bootstrap.AttemptId },
                    ObservedCoordinatorFencingEpoch =
                        checked((ulong)Bootstrap.CoordinatorFencingEpoch),
                    SupportedProtocol = new ProtocolVersionRange
                    {
                        Major = ProtocolConstants.Major,
                        MinimumMinor = ProtocolConstants.Minor,
                        MaximumMinor = ProtocolConstants.Minor,
                    },
                    Compatibility = ProtocolConstants.Compatibility,
                    OneUseNonce = ByteString.CopyFrom(
                        Convert.FromBase64String(Bootstrap.OneUseNonceBase64)),
                },
                ConnectionId,
                Runtime);
            Assert.AreEqual(HandshakeDisposition.Accepted, handshake.Disposition);
        }

        public AuthoritativeStore Store { get; }
        public CoordinatorAuthority Authority { get; }
        public RunRecord Run { get; }
        public AttemptRecord Attempt { get; }
        public RuntimeDescriptor Descriptor { get; }
        public CoordinatorRuntime Runtime { get; }
        public WorkerBootstrapRegistry Registry { get; }
        public ManagedWorkerBootstrap Bootstrap { get; }
        public string ConnectionId { get; } = "worker-connection-a";

        public WorkerProgress Progress(ulong sequence, ulong completedWorkUnits) =>
            new()
            {
                AttemptId = new AttemptId { Value = Attempt.AttemptId },
                CoordinatorFencingEpoch = checked((ulong)Authority.FencingEpoch),
                AttemptFencingToken = checked((ulong)Attempt.AttemptFencingToken),
                ProgressSequence = sequence,
                CompletedWorkUnits = completedWorkUnits,
                TotalWorkUnits = new OptionalUInt64
                {
                    Availability = AvailabilityState.Available,
                    Value = 1,
                },
                InertStatusText = "bounded progress",
            };

        public StagedOutputManifest Manifest()
        {
            byte[] payload = "manifest payload"u8.ToArray();
            string sha256 =
                Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
            byte[] digest = ManagedWorkerManifest.ComputeDigest(
                Bootstrap.StagedArtifactId,
                Bootstrap.OutputRelativeName,
                sha256,
                payload.LongLength);
            StagedOutputManifest manifest = new()
            {
                StagingAreaId = new StagingAreaId { Value = Bootstrap.StagingAreaId },
                AttemptId = new AttemptId { Value = Bootstrap.AttemptId },
                CoordinatorFencingEpoch = checked((ulong)Bootstrap.CoordinatorFencingEpoch),
                AttemptFencingToken = checked((ulong)Bootstrap.AttemptFencingToken),
                ManifestDigest = new ContentDigest
                {
                    Algorithm = DigestAlgorithm.Sha256,
                    Value = ByteString.CopyFrom(digest),
                    SizeBytes = checked((ulong)ManagedWorkerManifest.GetCanonicalBytes(
                        Bootstrap.StagedArtifactId,
                        Bootstrap.OutputRelativeName,
                        sha256,
                        payload.LongLength).LongLength),
                },
            };
            manifest.Outputs.Add(new StagedOutput
            {
                StagedArtifactId =
                    new StagedArtifactId { Value = Bootstrap.StagedArtifactId },
                Kind = StagedArtifactKind.TypedResult,
                TypedRelativeName = Bootstrap.OutputRelativeName,
                Content = new ContentDigest
                {
                    Algorithm = DigestAlgorithm.Sha256,
                    Value = ByteString.CopyFrom(SHA256.HashData(payload)),
                    SizeBytes = checked((ulong)payload.LongLength),
                },
                SchemaVersion = new SemanticVersion
                {
                    Value = ManagedWorkerManifest.OutputSchemaVersion,
                },
            });
            return manifest;
        }

        public WorkerTerminalReceipt Terminal(string receiptId) =>
            new()
            {
                AttemptId = new AttemptId { Value = Bootstrap.AttemptId },
                CoordinatorFencingEpoch = checked((ulong)Bootstrap.CoordinatorFencingEpoch),
                AttemptFencingToken = checked((ulong)Bootstrap.AttemptFencingToken),
                Outcome = WorkerTerminalOutcome.CompletedStaged,
                StagingReceiptId = receiptId,
            };

        public void Dispose()
        {
            Store.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static string? NormalizeFinalPath(string? value) =>
        value?.StartsWith(@"\\?\", StringComparison.Ordinal) == true
            ? Path.GetFullPath(value[4..]).TrimEnd(Path.DirectorySeparatorChar)
            : value is null
                ? null
                : Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar);

    private const uint HANDLE_FLAG_INHERIT = 0x00000001;
    private const uint FILE_LIST_DIRECTORY = 0x00000001;
    private const uint FILE_ADD_FILE = 0x00000002;
    private const uint SYNCHRONIZE = 0x00100000;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        FileShare shareMode,
        nint securityAttributes,
        FileMode creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(nint handle, uint mask, uint flags);
}
