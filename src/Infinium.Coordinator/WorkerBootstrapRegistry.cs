using System.Collections.Concurrent;
using System.Text;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Bethesda;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;
using Infinium.Persistence;

namespace Infinium.Coordinator;

public sealed record StagedOutputAcceptance(
    string ReceiptId,
    WorkerReceiptDisposition Disposition);

public sealed record TerminalReceiptAcceptance(WorkerReceiptDisposition Disposition);

public sealed class WorkerBootstrapRegistry
{
    private readonly ConcurrentDictionary<string, Registration> registrations =
        new(StringComparer.Ordinal);

    public void Register(ManagedWorkerBootstrap bootstrap)
    {
        if (!registrations.TryAdd(bootstrap.BootstrapId, new Registration(bootstrap)))
        {
            throw new InvalidOperationException("A worker bootstrap ID cannot be reused.");
        }
    }

    public HandshakeResponse Negotiate(
        WorkerHandshakeRequest request,
        string connectionId,
        CoordinatorRuntime runtime)
    {
        HandshakeResponse response = BaseHandshake(runtime);
        if (!registrations.TryGetValue(request.BootstrapId, out Registration? registration))
        {
            return Reject(
                response,
                HandshakeDisposition.LaunchBindingFailed,
                FailureCode.Unauthenticated,
                "The private worker bootstrap is not live.");
        }

        lock (registration.Gate)
        {
            ManagedWorkerBootstrap expected = registration.Bootstrap;
            bool valid = expected.ExpiresAt > DateTimeOffset.UtcNow
                && request.ProcessId == checked((uint)expected.ExpectedProcessId)
                && request.ExpectedAttemptId?.Value == expected.AttemptId
                && request.ObservedCoordinatorFencingEpoch
                    == checked((ulong)expected.CoordinatorFencingEpoch)
                && request.SupportedProtocol?.Major == ProtocolConstants.Major
                && request.SupportedProtocol.MinimumMinor <= ProtocolConstants.Minor
                && request.SupportedProtocol.MaximumMinor >= ProtocolConstants.Minor
                && request.Compatibility?.ApplicationContract?.Value
                    == ProtocolConstants.ContractVersion
                && request.Compatibility.DomainContract?.Value
                    == ProtocolConstants.ContractVersion
                && request.Compatibility.StorageContract?.Value
                    == ProtocolConstants.ContractVersion
                && request.OneUseNonce.Span.SequenceEqual(
                    Convert.FromBase64String(expected.OneUseNonceBase64));
            if (!valid || registration.ConnectionId is not null)
            {
                return Reject(
                    response,
                    HandshakeDisposition.LaunchBindingFailed,
                    FailureCode.Unauthenticated,
                    "The worker bootstrap binding is invalid, expired, or consumed.");
            }

            registration.ConnectionId = connectionId;
            response.Disposition = HandshakeDisposition.Accepted;
            response.GrantedCapabilities.Add(Capability.WorkerAssignment);
            response.GrantedCapabilities.Add(Capability.WorkerStaging);
            return response;
        }
    }

    public WorkerAssignment GetAssignment(
        ReceiveAssignmentRequest request,
        string connectionId,
        CoordinatorRuntime runtime)
    {
        Registration registration = Require(request.BootstrapId, connectionId);
        ManagedWorkerBootstrap bootstrap = registration.Bootstrap;
        if (request.ExpectedAttemptId?.Value != bootstrap.AttemptId
            || request.ObservedCoordinatorFencingEpoch
                != checked((ulong)bootstrap.CoordinatorFencingEpoch))
        {
            throw new InvalidOperationException("The assignment request is stale or mismatched.");
        }

        WorkerAssignment assignment = new()
        {
            Owner = bootstrap.OperationKind == ManagedWorkerOperationKind.Mo2SnapshotCapture
                ? new OperationOwner
                {
                    SnapshotCaptureOperationId =
                        new SnapshotCaptureOperationId { Value = bootstrap.RunId },
                }
                : new OperationOwner
                {
                    AnalysisRunId = new RunId { Value = bootstrap.RunId },
                },
            OperationId = new OperationId
            {
                Value = bootstrap.OperationKind == ManagedWorkerOperationKind.Mo2SnapshotCapture
                    ? bootstrap.RunId
                    : bootstrap.OperationKind == ManagedWorkerOperationKind.BethesdaSemanticExtraction
                        ? bootstrap.RunId + "-bethesda-semantic"
                        : bootstrap.RunId + "-slice2-substrate",
            },
            JobNodeId = new JobNodeId
            {
                Value = bootstrap.OperationKind == ManagedWorkerOperationKind.Mo2SnapshotCapture
                    ? bootstrap.RunId + "-capture"
                    : bootstrap.OperationKind == ManagedWorkerOperationKind.BethesdaSemanticExtraction
                        ? bootstrap.RunId + "-bethesda-index"
                        : bootstrap.RunId + "-root",
            },
            AttemptId = new AttemptId { Value = bootstrap.AttemptId },
            DispatchId = new DispatchId { Value = bootstrap.BootstrapId },
            CoordinatorFencingEpoch = checked((ulong)bootstrap.CoordinatorFencingEpoch),
            AttemptFencingToken = checked((ulong)bootstrap.AttemptFencingToken),
            Operation = BuildOperation(bootstrap),
            StagingAuthority = new StagingAuthority
            {
                StagingAreaId = new StagingAreaId { Value = bootstrap.StagingAreaId },
                InheritedStagingHandleSlot = 1,
            },
            Limits = new WorkerLimits
            {
                MaximumTotalInputBytes = bootstrap.OperationKind
                    == ManagedWorkerOperationKind.BethesdaSemanticExtraction
                        ? 64UL * 1024 * 1024
                        : 1,
                MaximumTotalOutputBytes = checked((ulong)bootstrap.MaximumOutputBytes),
                MaximumSingleOutputBytes = checked((ulong)bootstrap.MaximumOutputBytes),
                MaximumWorkUnits = bootstrap.OperationKind
                    == ManagedWorkerOperationKind.BethesdaSemanticExtraction
                        ? checked((ulong)(bootstrap.BethesdaSemanticExtraction?
                            .AcceptedSnapshot.Snapshot?.Plugins.Count(plugin => plugin.Enabled) ?? 1))
                        : 1,
                MaximumProgressUpdates = 8,
                MaximumStagedOutputs = 1,
                MaximumDiagnosticBytes = 4096,
                MaximumDuration = new DurationMillis
                {
                    Value = bootstrap.OperationKind
                        == ManagedWorkerOperationKind.BethesdaSemanticExtraction
                            ? 120_000UL
                            : 30_000UL,
                },
            },
            Deadline = ProtoMapping.ToProto(bootstrap.ExpiresAt),
            RetrySafety = RetrySafety.SafeWithNewAttempt,
        };
        assignment.StagingAuthority.AllowedOutputs.Add(new StagedOutputSlot
        {
            StagedArtifactId = new StagedArtifactId { Value = bootstrap.StagedArtifactId },
            TypedRelativeName = bootstrap.OutputRelativeName,
            Kind = StagedArtifactKind.TypedResult,
            MaximumBytes = checked((ulong)bootstrap.MaximumOutputBytes),
            Required = true,
        });
        return assignment;
    }

    private static WorkerOperation BuildOperation(ManagedWorkerBootstrap bootstrap)
    {
        if (bootstrap.OperationKind == ManagedWorkerOperationKind.BethesdaSemanticExtraction)
        {
            _ = bootstrap.BethesdaSemanticExtraction
                ?? throw new InvalidOperationException(
                    "The Bethesda semantic assignment is absent.");
            return new WorkerOperation
            {
                Kind = WorkerOperationKind.BuildTypedIndex,
                AdapterOrAnalyzerId = BethesdaSemanticExtractor.ProducerId,
                AdapterOrAnalyzerVersion = new SemanticVersion
                {
                    Value = BethesdaSemanticExtractor.ProducerVersion,
                },
                AssignmentSchemaVersion = new SemanticVersion { Value = "1.0.0" },
            };
        }

        if (bootstrap.OperationKind != ManagedWorkerOperationKind.Mo2SnapshotCapture)
        {
            return new WorkerOperation
            {
                Kind = WorkerOperationKind.ValidateStagedArtifact,
                AdapterOrAnalyzerId = "infinium.m1.slice2.substrate",
                AdapterOrAnalyzerVersion = new SemanticVersion { Value = "1.0.0" },
                AssignmentSchemaVersion = new SemanticVersion { Value = "1.0.0" },
            };
        }

        ManagedMo2SnapshotCaptureAssignment source = bootstrap.Mo2SnapshotCapture
            ?? throw new InvalidOperationException(
                "The MO2 snapshot assignment is absent.");
        Mo2SnapshotCaptureAssignment capture = new()
        {
            Mo2ExecutablePath = source.Mo2ExecutablePath,
            InstanceRoot = source.InstanceRoot,
            InstanceIniPath = source.InstanceIniPath,
            ProfilesRoot = source.ProfilesRoot,
            ModsRoot = source.ModsRoot,
            OverwriteRoot = source.OverwriteRoot,
            GameDataRoot = source.GameDataRoot,
            SkyrimExecutablePath = source.SkyrimExecutablePath,
            SelectedProfileName = source.SelectedProfileName,
            Platform = source.Platform,
            DistributionChannel = source.DistributionChannel,
            ApplicationId = source.ApplicationId,
        };
        capture.QualifiedMappings.Add(source.QualifiedMappings.Select(mapping =>
            new QualifiedMappingAssignment
            {
                MappingId = mapping.MappingId,
                SourceRoot = mapping.SourceRoot,
                VirtualPrefix = mapping.VirtualPrefix,
                MapperSha256 = mapping.MapperSha256,
            }));
        capture.EnabledMapperSha256.Add(source.EnabledMapperSha256s);
        return new WorkerOperation
        {
            Kind = WorkerOperationKind.CaptureMo2Snapshot,
            AdapterOrAnalyzerId = "infinium.mo2-static-reconstruction",
            AdapterOrAnalyzerVersion = new SemanticVersion { Value = "3.0.0" },
            AssignmentSchemaVersion = new SemanticVersion { Value = "1.0.0" },
            Mo2SnapshotCapture = capture,
        };
    }

    public WorkerProgressReceipt AcceptProgress(
        WorkerProgress request,
        string connectionId,
        CoordinatorRuntime runtime)
    {
        Registration? registration = registrations.Values.SingleOrDefault(candidate =>
            candidate.Bootstrap.AttemptId == request.AttemptId?.Value);
        if (registration is null)
        {
            return RejectProgress(
                WorkerReceiptDisposition.RejectedStaleFence,
                0,
                FailureCode.StaleFence,
                "The progress attempt is unknown.");
        }

        lock (registration.Gate)
        {
            if (!IsCurrentAttempt(
                    registration,
                    request.CoordinatorFencingEpoch,
                    request.AttemptFencingToken,
                    connectionId,
                    runtime))
            {
                return RejectProgress(
                    WorkerReceiptDisposition.RejectedStaleFence,
                    registration.LastProgressSequence,
                    FailureCode.StaleFence,
                    "The progress attempt is stale.");
            }

            if (request.ProgressSequence != registration.LastProgressSequence + 1)
            {
                return RejectProgress(
                    WorkerReceiptDisposition.RejectedAssignmentMismatch,
                    registration.LastProgressSequence,
                    FailureCode.InvalidArgument,
                    "Progress sequences must be contiguous and strictly increasing.");
            }

            int statusByteCount = Encoding.UTF8.GetByteCount(request.InertStatusText);
            if (registration.ProgressUpdateCount >= 8
                || request.CompletedWorkUnits > 1
                || statusByteCount > 4096 - registration.DiagnosticByteCount)
            {
                return RejectProgress(
                    WorkerReceiptDisposition.RejectedLimit,
                    registration.LastProgressSequence,
                    FailureCode.LimitExceeded,
                    "The progress update exceeds an assignment limit.");
            }

            if (request.TotalWorkUnits is null
                || request.TotalWorkUnits.Availability != AvailabilityState.Available
                || request.TotalWorkUnits.Value != 1
                || request.CompletedWorkUnits > request.TotalWorkUnits.Value
                || request.CompletedWorkUnits < registration.LastCompletedWorkUnits)
            {
                return RejectProgress(
                    WorkerReceiptDisposition.RejectedAssignmentMismatch,
                    registration.LastProgressSequence,
                    FailureCode.InvalidArgument,
                    "The progress work-unit report does not match the assignment.");
            }

            registration.LastProgressSequence = request.ProgressSequence;
            registration.LastCompletedWorkUnits = request.CompletedWorkUnits;
            registration.ProgressUpdateCount++;
            registration.DiagnosticByteCount += statusByteCount;
            return new WorkerProgressReceipt
            {
                Disposition = WorkerReceiptDisposition.AcceptedForStagingOnly,
                AcceptedProgressSequence = request.ProgressSequence,
            };
        }
    }

    public StagedOutputAcceptance AcceptStagedOutput(
        SubmitStagedOutputRequest request,
        string connectionId)
    {
        string attemptId = request.Manifest?.AttemptId?.Value
            ?? throw new InvalidOperationException("The staged manifest has no attempt.");
        Registration registration = registrations.Values.SingleOrDefault(candidate =>
            candidate.Bootstrap.AttemptId == attemptId)
            ?? throw new InvalidOperationException("The staged attempt is unknown.");
        lock (registration.Gate)
        {
            RequireConnection(registration, connectionId);
            ManagedWorkerBootstrap expected = registration.Bootstrap;
            StagedOutputManifest manifest = request.Manifest;

            if (manifest.StagingAreaId?.Value != expected.StagingAreaId
                || manifest.CoordinatorFencingEpoch
                    != checked((ulong)expected.CoordinatorFencingEpoch)
                || manifest.AttemptFencingToken != checked((ulong)expected.AttemptFencingToken)
                || manifest.Outputs.Count != 1)
            {
                throw new InvalidOperationException("The staged manifest authority does not match.");
            }

            StagedOutput output = manifest.Outputs[0];
            if (output.StagedArtifactId?.Value != expected.StagedArtifactId
                || output.TypedRelativeName != expected.OutputRelativeName
                || output.Kind != StagedArtifactKind.TypedResult
                || output.Content?.Algorithm != DigestAlgorithm.Sha256
                || output.Content.Value.Length != 32
                || output.Content.SizeBytes > checked((ulong)expected.MaximumOutputBytes)
                || output.SchemaVersion?.Value != expected.OutputSchemaVersion)
            {
                throw new InvalidOperationException("The staged output is malformed or outside its slot.");
            }

            string outputSha256 =
                Convert.ToHexString(output.Content.Value.Span).ToLowerInvariant();
            byte[] canonicalManifest = ManagedWorkerManifest.GetCanonicalBytes(
                expected.StagedArtifactId,
                expected.OutputRelativeName,
                outputSha256,
                checked((long)output.Content.SizeBytes),
                expected.OutputSchemaVersion);
            byte[] expectedManifestDigest = ManagedWorkerManifest.ComputeDigest(
                expected.StagedArtifactId,
                expected.OutputRelativeName,
                outputSha256,
                checked((long)output.Content.SizeBytes),
                expected.OutputSchemaVersion);
            if (manifest.ManifestDigest?.Algorithm != DigestAlgorithm.Sha256
                || manifest.ManifestDigest.Value.Length != expectedManifestDigest.Length
                || manifest.ManifestDigest.SizeBytes
                    != checked((ulong)canonicalManifest.LongLength)
                || !manifest.ManifestDigest.Value.Span.SequenceEqual(expectedManifestDigest))
            {
                throw new InvalidOperationException("The staged manifest digest is invalid.");
            }

            if (registration.StagingReceiptId is not null)
            {
                if (registration.AcceptedManifest is null
                    || !registration.AcceptedManifest.Equals(manifest))
                {
                    throw new InvalidOperationException(
                        "A staged receipt may be replayed only with the exact accepted manifest.");
                }

                return new StagedOutputAcceptance(
                    registration.StagingReceiptId,
                    WorkerReceiptDisposition.Duplicate);
            }

            if (registration.TerminalAccepted)
            {
                throw new InvalidOperationException(
                    "A staged manifest cannot be accepted after a terminal receipt.");
            }

            registration.Result = new ManagedWorkerResult(
                1,
                expected.BootstrapId,
                expected.AttemptId,
                expected.CoordinatorFencingEpoch,
                expected.AttemptFencingToken,
                expected.OutputRelativeName,
                outputSha256,
                checked((long)output.Content.SizeBytes),
                Convert.ToHexString(expectedManifestDigest).ToLowerInvariant());
            registration.AcceptedManifest = manifest.Clone();
            registration.StagingReceiptId = Guid.NewGuid().ToString("N");
            return new StagedOutputAcceptance(
                registration.StagingReceiptId,
                WorkerReceiptDisposition.AcceptedForStagingOnly);
        }
    }

    public TerminalReceiptAcceptance AcceptTerminal(
        WorkerTerminalReceipt request,
        string connectionId)
    {
        string attemptId = request.AttemptId?.Value
            ?? throw new InvalidOperationException("The terminal receipt has no attempt.");
        Registration registration = registrations.Values.SingleOrDefault(candidate =>
            candidate.Bootstrap.AttemptId == attemptId)
            ?? throw new InvalidOperationException("The terminal attempt is unknown.");
        lock (registration.Gate)
        {
            RequireConnection(registration, connectionId);
            ManagedWorkerBootstrap expected = registration.Bootstrap;
            if (registration.TerminalAccepted)
            {
                if (registration.AcceptedTerminal is null
                    || !registration.AcceptedTerminal.Equals(request))
                {
                    throw new InvalidOperationException(
                        "A terminal receipt may be replayed only with the exact accepted receipt.");
                }

                return new TerminalReceiptAcceptance(WorkerReceiptDisposition.Duplicate);
            }

            bool completed = request.Outcome is (
                    WorkerTerminalOutcome.CompletedStaged
                    or WorkerTerminalOutcome.CompletedWithGapsStaged)
                && request.StagingReceiptId == registration.StagingReceiptId
                && registration.Result is not null;
            bool cancelled = request.Outcome == WorkerTerminalOutcome.Cancelled
                && string.IsNullOrEmpty(request.StagingReceiptId)
                && registration.Result is null;
            if (request.CoordinatorFencingEpoch
                    != checked((ulong)expected.CoordinatorFencingEpoch)
                || request.AttemptFencingToken != checked((ulong)expected.AttemptFencingToken)
                || request.Failure is not null
                || (!completed && !cancelled))
            {
                throw new InvalidOperationException("The terminal receipt is stale or incomplete.");
            }

            registration.TerminalAccepted = true;
            registration.TerminalOutcome = request.Outcome;
            registration.AcceptedTerminal = request.Clone();
            return new TerminalReceiptAcceptance(
                WorkerReceiptDisposition.AcceptedForStagingOnly);
        }
    }

    public ManagedWorkerResult GetAcceptedResult(string bootstrapId)
    {
        if (!registrations.TryRemove(bootstrapId, out Registration? registration))
        {
            throw new InvalidOperationException("The worker bootstrap is unknown.");
        }

        lock (registration.Gate)
        {
            return registration.TerminalAccepted
                && registration.TerminalOutcome is (
                    WorkerTerminalOutcome.CompletedStaged
                    or WorkerTerminalOutcome.CompletedWithGapsStaged)
                && registration.Result is not null
                ? registration.Result
                : throw new InvalidOperationException("The worker did not complete staged publication.");
        }
    }

    public void GetAcceptedCancellation(string bootstrapId)
    {
        if (!registrations.TryRemove(bootstrapId, out Registration? registration))
        {
            throw new InvalidOperationException("The worker bootstrap is unknown.");
        }

        lock (registration.Gate)
        {
            if (!registration.TerminalAccepted
                || registration.TerminalOutcome != WorkerTerminalOutcome.Cancelled)
            {
                throw new InvalidOperationException(
                    "The worker did not acknowledge the safe cancellation boundary.");
            }
        }
    }

    public void Abandon(string bootstrapId) =>
        registrations.TryRemove(bootstrapId, out _);

    public bool IsCurrentAttempt(
        string attemptId,
        ulong coordinatorFencingEpoch,
        ulong attemptFencingToken,
        string connectionId,
        CoordinatorRuntime runtime)
    {
        Registration? registration = registrations.Values.SingleOrDefault(candidate =>
            candidate.Bootstrap.AttemptId == attemptId);
        if (registration is null)
        {
            return false;
        }

        lock (registration.Gate)
        {
            return IsCurrentAttempt(
                registration,
                coordinatorFencingEpoch,
                attemptFencingToken,
                connectionId,
                runtime);
        }
    }

    private static bool IsCurrentAttempt(
        Registration registration,
        ulong coordinatorFencingEpoch,
        ulong attemptFencingToken,
        string connectionId,
        CoordinatorRuntime runtime)
    {
        if (!string.Equals(registration.ConnectionId, connectionId, StringComparison.Ordinal)
            || registration.Bootstrap.ExpiresAt <= DateTimeOffset.UtcNow
            || coordinatorFencingEpoch
                != checked((ulong)registration.Bootstrap.CoordinatorFencingEpoch)
            || attemptFencingToken
                != checked((ulong)registration.Bootstrap.AttemptFencingToken))
        {
            return false;
        }

        if (registration.Bootstrap.OperationKind
            == ManagedWorkerOperationKind.Mo2SnapshotCapture)
        {
            SnapshotCaptureOperationRecord operation =
                runtime.Store.GetSnapshotCaptureOperation(registration.Bootstrap.RunId);
            return operation.State == "Running"
                && operation.CoordinatorFencingEpoch
                    == registration.Bootstrap.CoordinatorFencingEpoch;
        }

        RunRecord run = runtime.Store.GetRun(registration.Bootstrap.RunId);
        return run.State == Infinium.Domain.Contracts.LifecycleState.Running
            && run.CoordinatorFencingEpoch == registration.Bootstrap.CoordinatorFencingEpoch;
    }

    public WorkerControl GetControl(
        PollControlRequest request,
        string connectionId,
        CoordinatorRuntime runtime)
    {
        Registration? registration = registrations.Values.SingleOrDefault(candidate =>
            candidate.Bootstrap.AttemptId == request.AttemptId?.Value);
        if (registration is null
            || !string.Equals(registration.ConnectionId, connectionId, StringComparison.Ordinal)
            || registration.Bootstrap.ExpiresAt <= DateTimeOffset.UtcNow
            || request.CoordinatorFencingEpoch
                != checked((ulong)registration.Bootstrap.CoordinatorFencingEpoch)
            || request.AttemptFencingToken
                != checked((ulong)registration.Bootstrap.AttemptFencingToken))
        {
            return WorkerControl.StopStaleAttempt;
        }

        if (registration.Bootstrap.OperationKind
            == ManagedWorkerOperationKind.Mo2SnapshotCapture)
        {
            SnapshotCaptureOperationRecord operation =
                runtime.Store.GetSnapshotCaptureOperation(registration.Bootstrap.RunId);
            return operation.State == "Running"
                ? WorkerControl.Continue
                : WorkerControl.StopStaleAttempt;
        }

        RunRecord run = runtime.Store.GetRun(registration.Bootstrap.RunId);
        return run.State switch
        {
            Infinium.Domain.Contracts.LifecycleState.Running => WorkerControl.Continue,
            Infinium.Domain.Contracts.LifecycleState.Pausing
                or Infinium.Domain.Contracts.LifecycleState.Cancelling =>
                WorkerControl.CancelAtSafeBoundary,
            _ => WorkerControl.StopStaleAttempt,
        };
    }

    private static WorkerProgressReceipt RejectProgress(
        WorkerReceiptDisposition disposition,
        ulong acceptedProgressSequence,
        FailureCode code,
        string detail) =>
        new()
        {
            Disposition = disposition,
            AcceptedProgressSequence = acceptedProgressSequence,
            Failure = new Failure { Code = code, Detail = detail },
        };

    private Registration Require(string bootstrapId, string connectionId)
    {
        if (!registrations.TryGetValue(bootstrapId, out Registration? registration))
        {
            throw new InvalidOperationException("The worker bootstrap is unknown.");
        }

        lock (registration.Gate)
        {
            RequireConnection(registration, connectionId);
            return registration;
        }
    }

    private static void RequireConnection(Registration registration, string connectionId)
    {
        if (!string.Equals(registration.ConnectionId, connectionId, StringComparison.Ordinal)
            || registration.Bootstrap.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("The worker connection is stale or unauthenticated.");
        }
    }

    private static HandshakeResponse BaseHandshake(CoordinatorRuntime runtime) =>
        new()
        {
            NegotiatedProtocol = ProtocolConstants.Version,
            Compatibility = ProtocolConstants.Compatibility,
            CoordinatorInstanceId = new CoordinatorInstanceId
            {
                Value = runtime.Authority.InstanceId,
            },
            CoordinatorFencingEpoch = checked((ulong)runtime.Authority.FencingEpoch),
            BoundEndpointRole = EndpointRole.GeneralWorker,
            Limits = ProtocolConstants.Limits,
        };

    private static HandshakeResponse Reject(
        HandshakeResponse response,
        HandshakeDisposition disposition,
        FailureCode code,
        string detail)
    {
        response.Disposition = disposition;
        response.Failure = new Failure { Code = code, Detail = detail };
        return response;
    }

    private sealed class Registration(ManagedWorkerBootstrap bootstrap)
    {
        public object Gate { get; } = new();
        public ManagedWorkerBootstrap Bootstrap { get; } = bootstrap;
        public string? ConnectionId { get; set; }
        public string? StagingReceiptId { get; set; }
        public StagedOutputManifest? AcceptedManifest { get; set; }
        public ManagedWorkerResult? Result { get; set; }
        public bool TerminalAccepted { get; set; }
        public WorkerTerminalOutcome TerminalOutcome { get; set; }
        public WorkerTerminalReceipt? AcceptedTerminal { get; set; }
        public ulong LastProgressSequence { get; set; }
        public ulong LastCompletedWorkUnits { get; set; }
        public uint ProgressUpdateCount { get; set; }
        public int DiagnosticByteCount { get; set; }
    }
}
