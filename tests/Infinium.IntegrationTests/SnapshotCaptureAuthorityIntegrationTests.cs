using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class SnapshotCaptureAuthorityIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Evaluation")]
    public void CaptureExistsOnlyAfterExplicitDurableIdempotentSubmission()
    {
        using CaptureStoreContext context = new();
        Assert.IsNull(context.Store.GetNextDispatchableSnapshotCapture());
        Assert.IsFalse(Directory.EnumerateFileSystemEntries(
            context.Store.Paths.Staging).Any());

        ManagedMo2SnapshotCaptureAssignment selection = Selection(context.Root);
        string json = JsonSerializer.Serialize(selection);
        string sha256 = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
        DateTimeOffset deadline = context.Now.AddMinutes(1);
        SnapshotCaptureOperationRecord created =
            context.Store.CreateSnapshotCaptureOperation(
                "capture-command",
                "capture-operation",
                json,
                sha256,
                "EvaluationHarness",
                deadline,
                context.Authority.FencingEpoch,
                context.Now);
        SnapshotCaptureOperationRecord replay =
            context.Store.CreateSnapshotCaptureOperation(
                "capture-command",
                "different-generated-operation",
                json,
                sha256,
                "EvaluationHarness",
                deadline,
                context.Authority.FencingEpoch,
                context.Now);

        Assert.AreEqual(created.OperationId, replay.OperationId);
        Assert.AreEqual(
            created.OperationId,
            context.Store.FindSnapshotCaptureByCommand("capture-command")!.OperationId);
        Assert.AreEqual("Queued", replay.State);
        Assert.AreEqual(
            created.OperationId,
            context.Store.GetNextDispatchableSnapshotCapture()!.OperationId);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            context.Store.CreateSnapshotCaptureOperation(
                "capture-command",
                "rebound-operation",
                json + " ",
                sha256,
                "EvaluationHarness",
                deadline,
                context.Authority.FencingEpoch,
                context.Now));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void ExpiredQueuedCaptureTerminalizesWithoutStarvingLaterWork()
    {
        using CaptureStoreContext context = new();
        ManagedMo2SnapshotCaptureAssignment selection = Selection(context.Root);
        string json = JsonSerializer.Serialize(selection);
        string sha256 = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
        SnapshotCaptureOperationRecord expired =
            context.Store.CreateSnapshotCaptureOperation(
                "expired-command",
                "expired-operation",
                json,
                sha256,
                "EvaluationHarness",
                context.Now.AddSeconds(1),
                context.Authority.FencingEpoch,
                context.Now);
        SnapshotCaptureOperationRecord later =
            context.Store.CreateSnapshotCaptureOperation(
                "later-command",
                "later-operation",
                json,
                sha256,
                "EvaluationHarness",
                context.Now.AddMinutes(1),
                context.Authority.FencingEpoch,
                context.Now.AddTicks(1));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            context.Store.DispatchSnapshotCaptureAttempt(
                expired.OperationId,
                expired.Generation,
                context.Authority.FencingEpoch,
                TimeSpan.FromMinutes(2),
                context.Now.AddSeconds(2)));

        Assert.AreEqual(
            "Failed",
            context.Store.GetSnapshotCaptureOperation(expired.OperationId).State);
        Assert.AreEqual(
            later.OperationId,
            context.Store.GetNextDispatchableSnapshotCapture()!.OperationId);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void ExpiredAttemptCanFailButCannotPublish()
    {
        using CaptureStoreContext context = new();
        ManagedMo2SnapshotCaptureAssignment selection = Selection(context.Root);
        string json = JsonSerializer.Serialize(selection);
        string sha256 = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
        SnapshotCaptureOperationRecord operation =
            context.Store.CreateSnapshotCaptureOperation(
                "lease-command",
                "lease-operation",
                json,
                sha256,
                "EvaluationHarness",
                context.Now.AddMinutes(1),
                context.Authority.FencingEpoch,
                context.Now);
        SnapshotCaptureAttemptRecord attempt =
            context.Store.DispatchSnapshotCaptureAttempt(
                operation.OperationId,
                operation.Generation,
                context.Authority.FencingEpoch,
                TimeSpan.FromTicks(1),
                context.Now);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            context.Store.ReadSnapshotCaptureStagedPayload(
                attempt,
                "absent.json",
                new string('a', 64),
                0,
                4096));
        context.Store.FailSnapshotCapture(
            attempt,
            context.Authority.FencingEpoch,
            context.Now.AddSeconds(1));

        Assert.AreEqual(
            "Failed",
            context.Store.GetSnapshotCaptureOperation(operation.OperationId).State);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void MalformedQueuedRequestBecomesDurablyFailed()
    {
        using CaptureStoreContext context = new();
        string json = "{\"unknown\":true}";
        string sha256 = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))
            .ToLowerInvariant();
        SnapshotCaptureOperationRecord operation =
            context.Store.CreateSnapshotCaptureOperation(
                "malformed-command",
                "malformed-operation",
                json,
                sha256,
                "EvaluationHarness",
                context.Now.AddMinutes(1),
                context.Authority.FencingEpoch,
                context.Now);

        context.Store.FailQueuedSnapshotCapture(
            operation.OperationId,
            operation.Generation,
            context.Authority.FencingEpoch,
            context.Now.AddSeconds(1));

        SnapshotCaptureOperationRecord failed =
            context.Store.GetSnapshotCaptureOperation(operation.OperationId);
        Assert.AreEqual("Failed", failed.State);
        Assert.AreEqual(operation.Generation + 1, failed.Generation);
        Assert.IsNull(context.Store.GetNextDispatchableSnapshotCapture());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            context.Store.FailQueuedSnapshotCapture(
                operation.OperationId,
                operation.Generation,
                context.Authority.FencingEpoch,
                context.Now.AddSeconds(2)));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    public void TypedWorkerAssignmentRetainsCaptureAuthorityAndRejectsStaleProgress()
    {
        using CaptureStoreContext context = new();
        (SnapshotCaptureOperationRecord operation, SnapshotCaptureAttemptRecord attempt) =
            context.CreateRunningCapture();
        RuntimeDescriptor descriptor = RuntimeDescriptor.Create(
            context.Authority.InstanceId,
            context.Authority.FencingEpoch,
            Environment.ProcessId,
            elevated: false,
            context.Now);
        CoordinatorRuntime runtime =
            new(context.Store, context.Authority, descriptor);
        WorkerBootstrapRegistry registry = new();
        ManagedMo2SnapshotCaptureAssignment selection = Selection(context.Root);
        ManagedWorkerBootstrap bootstrap = new(
            1,
            "capture-bootstrap",
            context.Authority.InstanceId,
            context.Authority.FencingEpoch,
            operation.OperationId,
            attempt.AttemptId,
            attempt.AttemptFencingToken,
            descriptor.WorkerPipe,
            Environment.ProcessId,
            "capture-staging",
            "capture-artifact",
            1,
            "mo2-snapshot.v3.json",
            64 * 1024 * 1024,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            context.Now.AddMinutes(1),
            ManagedWorkerOperationKind.Mo2SnapshotCapture,
            "3.0.0",
            selection);
        registry.Register(bootstrap);
        const string connectionId = "capture-worker-connection";
        HandshakeResponse handshake = registry.Negotiate(
            new WorkerHandshakeRequest
            {
                BootstrapId = bootstrap.BootstrapId,
                ProcessId = checked((uint)bootstrap.ExpectedProcessId),
                ExpectedAttemptId = new AttemptId { Value = attempt.AttemptId },
                ObservedCoordinatorFencingEpoch =
                    checked((ulong)context.Authority.FencingEpoch),
                SupportedProtocol = new ProtocolVersionRange
                {
                    Major = ProtocolConstants.Major,
                    MinimumMinor = ProtocolConstants.Minor,
                    MaximumMinor = ProtocolConstants.Minor,
                },
                Compatibility = ProtocolConstants.Compatibility,
                OneUseNonce = ByteString.CopyFrom(
                    Convert.FromBase64String(bootstrap.OneUseNonceBase64)),
            },
            connectionId,
            runtime);
        Assert.AreEqual(HandshakeDisposition.Accepted, handshake.Disposition);

        WorkerAssignment assignment = registry.GetAssignment(
            new ReceiveAssignmentRequest
            {
                BootstrapId = bootstrap.BootstrapId,
                ExpectedAttemptId = new AttemptId { Value = attempt.AttemptId },
                ObservedCoordinatorFencingEpoch =
                    checked((ulong)context.Authority.FencingEpoch),
            },
            connectionId,
            runtime);
        Assert.AreEqual(WorkerOperationKind.CaptureMo2Snapshot, assignment.Operation.Kind);
        Assert.AreEqual(
            operation.OperationId,
            assignment.Owner.SnapshotCaptureOperationId.Value);
        Assert.AreEqual(
            selection.SelectedProfileName,
            assignment.Operation.Mo2SnapshotCapture.SelectedProfileName);
        Assert.AreEqual(
            Infinium.Contracts.Protobuf.Worker.V1.StagedArtifactKind.TypedResult,
            assignment.StagingAuthority.AllowedOutputs.Single().Kind);

        context.Store.FailSnapshotCapture(
            attempt,
            context.Authority.FencingEpoch,
            context.Now.AddSeconds(1));
        WorkerProgressReceipt stale = registry.AcceptProgress(
            new WorkerProgress
            {
                AttemptId = new AttemptId { Value = attempt.AttemptId },
                CoordinatorFencingEpoch =
                    checked((ulong)context.Authority.FencingEpoch),
                AttemptFencingToken = checked((ulong)attempt.AttemptFencingToken),
                ProgressSequence = 1,
                CompletedWorkUnits = 0,
                TotalWorkUnits = new OptionalUInt64
                {
                    Availability = AvailabilityState.Available,
                    Value = 1,
                },
                InertStatusText = "stale",
            },
            connectionId,
            runtime);
        Assert.AreEqual(
            WorkerReceiptDisposition.RejectedStaleFence,
            stale.Disposition);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void CoordinatorRejectsMalformedOrStaleSnapshotPublication()
    {
        ManagedMo2SnapshotCaptureAssignment selection = Selection(Path.GetTempPath());
        Mo2SnapshotCaptureResult valid = ValidCapturedResult(selection);
        IExecutableAdmissionService admissions =
            new FixedExecutableAdmissionService(valid.Snapshot!);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SnapshotCaptureExecutor.ValidateCapturedSnapshot(valid, selection));
        SnapshotCaptureExecutor.ValidateCapturedSnapshot(valid, selection, admissions);
        ManagedMo2SnapshotCaptureAssignment unqualifiedMappingSelection =
            selection with
            {
                QualifiedMappings =
                [
                    new ManagedQualifiedMappingAssignment(
                        "unqualified-mapper",
                        Path.Combine(Path.GetTempPath(), "unqualified-mapper-root"),
                        "mapped",
                        new string('4', 64)),
                ],
                EnabledMapperSha256s = [new string('4', 64)],
            };
        SnapshotCaptureExecutor.ValidateCapturedSnapshot(
            ValidCapturedResult(unqualifiedMappingSelection),
            unqualifiedMappingSelection,
            admissions);
        Mo2InstallationSnapshot snapshot = valid.Snapshot!;
        Mo2SnapshotCaptureResult dependencyMismatch = valid with
        {
            Snapshot = snapshot with
            {
                Dependencies = snapshot.Dependencies with
                {
                    CanonicalFingerprint = new Sha256Fingerprint(new string('b', 64)),
                },
            },
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SnapshotCaptureExecutor.ValidateCapturedSnapshot(
                dependencyMismatch,
                selection,
                admissions));
        Mo2SnapshotCaptureResult structuralTamper = valid with
        {
            Snapshot = snapshot with
            {
                Dependencies = snapshot.Dependencies with
                {
                    StructuralObservations =
                    [
                        new SnapshotStructuralObservation(
                            "mods",
                            "tampered.txt",
                            false,
                            1,
                            2,
                            FileAttributes.Normal,
                            "tampered-object"),
                    ],
                },
            },
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SnapshotCaptureExecutor.ValidateCapturedSnapshot(
                structuralTamper,
                selection,
                admissions));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SnapshotCaptureExecutor.ValidateCapturedSnapshot(
                new Mo2SnapshotCaptureResult(
                    SnapshotCaptureState.Completed,
                    null,
                    []),
                selection,
                admissions));

        using CaptureStoreContext context = new();
        (_, SnapshotCaptureAttemptRecord attempt) = context.CreateRunningCapture();
        using AttemptStagingAuthority staging =
            context.Store.Paths.CreateAttemptStagingDirectory(attempt.AttemptId);
        byte[] bytes = "{}"u8.ToArray();
        string output = "mo2-snapshot.v3.json";
        File.WriteAllBytes(
            Path.Combine(context.Store.Paths.Staging, attempt.AttemptId, output),
            bytes);
        string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        CoordinatorAuthority replacement = context.Store.AcquireCoordinatorAuthority(
            "replacement-coordinator",
            context.Now.AddMinutes(6),
            TimeSpan.FromMinutes(5));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            context.Store.AdmitSnapshotCapturePayload(
                attempt,
                output,
                sha256,
                bytes.LongLength,
                new string('a', 64),
                4096,
                "snapshot-fabricated",
                "artifact-stale",
                context.Now.AddMinutes(6)));
        Assert.AreEqual(
            "Running",
            context.Store.GetSnapshotCaptureOperation(attempt.OperationId).State);
        Assert.AreEqual(
            1,
            context.Store.FenceInterruptedSnapshotCaptures(
                replacement.FencingEpoch,
                context.Now.AddMinutes(6)));
        Assert.AreEqual(
            "Failed",
            context.Store.GetSnapshotCaptureOperation(attempt.OperationId).State);
    }

    private static ManagedMo2SnapshotCaptureAssignment Selection(string root) =>
        new(
            Path.Combine(root, "ModOrganizer.exe"),
            Path.Combine(root, "instance"),
            Path.Combine(root, "instance", "ModOrganizer.ini"),
            Path.Combine(root, "instance", "profiles"),
            Path.Combine(root, "instance", "mods"),
            Path.Combine(root, "instance", "overwrite"),
            Path.Combine(root, "game", "Data"),
            Path.Combine(root, "game", "SkyrimSE.exe"),
            "Explicit Profile",
            "windows-x64",
            "steam",
            "489830",
            [],
            []);

    private static Mo2SnapshotCaptureResult ValidCapturedResult(
        ManagedMo2SnapshotCaptureAssignment assignment)
    {
        OpaqueId instanceId = new("instance-id");
        OpaqueId profileId = new("profile-id");
        ExecutableIdentity mo2 = new(
            "ModOrganizer.exe",
            1,
            new string('1', 64),
            "2.5.2",
            null,
            null,
            null);
        ExecutableIdentity gamePlugin = new(
            "game_skyrimse.dll",
            1,
            new string('2', 64),
            "2.5.2",
            null,
            null,
            null);
        ExecutableIdentity runtime = new(
            "SkyrimSE.exe",
            1,
            new string('3', 64),
            "1.6.1170.0",
            null,
            null,
            null);
        RuntimeTargetContext target = new(
            assignment.Platform,
            assignment.DistributionChannel,
            assignment.ApplicationId);
        SnapshotRootObservation[] roots =
        [
            new("instance", assignment.InstanceRoot, "root-instance"),
            new(
                "profile",
                Path.Combine(assignment.ProfilesRoot, assignment.SelectedProfileName),
                "root-profile"),
            new("mods", assignment.ModsRoot, "root-mods"),
            new("overwrite", assignment.OverwriteRoot, "root-overwrite"),
            new("game-data", assignment.GameDataRoot, "root-game-data"),
        ];
        Mo2SnapshotDependencyManifest dependencies = new(
            new ContractVersion(1, 0, 0),
            new Sha256Fingerprint(new string('0', 64)),
            "infinium.mo2-static-reconstruction/v3",
            "mod-organizer-2",
            assignment.SelectedProfileName,
            target,
            mo2,
            gamePlugin,
            runtime,
            assignment.EnabledMapperSha256s,
            SupportedExecutableManifests.QualifiedMapperSha256s.ToArray(),
            [],
            roots,
            [],
            assignment.QualifiedMappings.Select(mapping =>
                new SnapshotMappingDependency(
                    mapping.MappingId,
                    mapping.SourceRoot,
                    mapping.VirtualPrefix.Replace('\\', '/').Trim('/'),
                    new Sha256Fingerprint(mapping.MapperSha256),
                    Admitted: false)).ToArray());
        Sha256Fingerprint fingerprint = Mo2SnapshotCanonicalization.Compute(
            dependencies,
            instanceId,
            profileId);
        dependencies = dependencies with
        {
            CanonicalFingerprint = fingerprint,
        };
        UtcTimestamp capturedAt = new(DateTimeOffset.UtcNow);
        InstallationSnapshotContract contract = new(
            Mo2SnapshotCanonicalization.ComputeSnapshotId(fingerprint, capturedAt),
            new ContractVersion(3, 0, 0),
            instanceId,
            profileId,
            fingerprint,
            [],
            [],
            capturedAt);
        Mo2InstallationSnapshot snapshot = new(
            contract,
            "infinium.mo2-static-reconstruction/v3",
            assignment.InstanceRoot,
            Path.Combine(assignment.ProfilesRoot, assignment.SelectedProfileName),
            assignment.SelectedProfileName,
            new ExecutableAdmission(
                AdmissionState.Accepted,
                "infinium.mo2-2.5.2-local-research/v1",
                mo2,
                []),
            new ExecutableAdmission(
                AdmissionState.Accepted,
                "infinium.mo2-game-skyrimse-2.5.2-local-research/v1",
                gamePlugin,
                []),
            new ExecutableAdmission(
                AdmissionState.Accepted,
                "infinium.skyrimse-1.6.1170-steam/v1",
                runtime,
                []),
            dependencies,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            false,
            false);
        return new Mo2SnapshotCaptureResult(
            SnapshotCaptureState.Completed,
            snapshot,
            []);
    }

    private sealed class CaptureStoreContext : IDisposable
    {
        public CaptureStoreContext()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                $"infinium-snapshot-authority-{Guid.NewGuid():N}");
            Store = new AuthoritativeStore(new StoragePaths(Root));
            Now = DateTimeOffset.UtcNow;
            Authority = Store.AcquireCoordinatorAuthority(
                "capture-coordinator",
                Now,
                TimeSpan.FromMinutes(5));
        }

        public string Root { get; }
        public AuthoritativeStore Store { get; }
        public DateTimeOffset Now { get; }
        public CoordinatorAuthority Authority { get; }

        public (
            SnapshotCaptureOperationRecord Operation,
            SnapshotCaptureAttemptRecord Attempt) CreateRunningCapture()
        {
            ManagedMo2SnapshotCaptureAssignment selection = Selection(Root);
            string json = JsonSerializer.Serialize(selection);
            string sha = Convert.ToHexString(
                SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))
                .ToLowerInvariant();
            SnapshotCaptureOperationRecord operation =
                Store.CreateSnapshotCaptureOperation(
                    Guid.NewGuid().ToString("N"),
                    Guid.NewGuid().ToString("N"),
                    json,
                    sha,
                    "EvaluationHarness",
                    Now.AddMinutes(1),
                    Authority.FencingEpoch,
                    Now);
            SnapshotCaptureAttemptRecord attempt =
                Store.DispatchSnapshotCaptureAttempt(
                    operation.OperationId,
                    operation.Generation,
                    Authority.FencingEpoch,
                    TimeSpan.FromMinutes(2),
                    Now);
            return (operation, attempt);
        }

        public void Dispose()
        {
            Store.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

    private sealed class FixedExecutableAdmissionService(
        Mo2InstallationSnapshot snapshot) : IExecutableAdmissionService
    {
        public ExecutableAdmission AdmitMo2(string path) =>
            snapshot.Mo2Admission;

        public ExecutableAdmission AdmitSkyrimGamePlugin(string path) =>
            snapshot.SkyrimGamePluginAdmission;

        public ExecutableAdmission AdmitSkyrim(
            string path,
            RuntimeTargetContext context) =>
            snapshot.RuntimeAdmission;
    }
}
