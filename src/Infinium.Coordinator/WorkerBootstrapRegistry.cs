using System.Collections.Concurrent;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Protocol.V1;
using Infinium.Contracts.Protobuf.Worker.V1;
using Infinium.Persistence;

namespace Infinium.Coordinator;

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

        RunBinding binding = runtime.Store.GetRun(bootstrap.RunId).Binding;
        WorkerAssignment assignment = new()
        {
            Owner = new OperationOwner
            {
                AnalysisRunId = new RunId { Value = bootstrap.RunId },
            },
            OperationId = new OperationId { Value = bootstrap.RunId + "-slice2-substrate" },
            JobNodeId = new JobNodeId { Value = bootstrap.RunId + "-root" },
            AttemptId = new AttemptId { Value = bootstrap.AttemptId },
            DispatchId = new DispatchId { Value = bootstrap.BootstrapId },
            CoordinatorFencingEpoch = checked((ulong)bootstrap.CoordinatorFencingEpoch),
            AttemptFencingToken = checked((ulong)bootstrap.AttemptFencingToken),
            Operation = new WorkerOperation
            {
                Kind = WorkerOperationKind.ValidateStagedArtifact,
                AdapterOrAnalyzerId = "infinium.m1.slice2.substrate",
                AdapterOrAnalyzerVersion = new SemanticVersion { Value = "1.0.0" },
                AssignmentSchemaVersion = new SemanticVersion { Value = "1.0.0" },
            },
            ResolvedInputManifestId =
                new ResolvedInputManifestId { Value = binding.ResolvedInputManifestId },
            StagingAuthority = new StagingAuthority
            {
                StagingAreaId = new StagingAreaId { Value = bootstrap.StagingAreaId },
                InheritedStagingHandleSlot = 1,
            },
            Limits = new WorkerLimits
            {
                MaximumTotalInputBytes = 1,
                MaximumTotalOutputBytes = checked((ulong)bootstrap.MaximumOutputBytes),
                MaximumSingleOutputBytes = checked((ulong)bootstrap.MaximumOutputBytes),
                MaximumWorkUnits = 1,
                MaximumProgressUpdates = 8,
                MaximumStagedOutputs = 1,
                MaximumDiagnosticBytes = 4096,
                MaximumDuration = new DurationMillis { Value = 30_000 },
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

    public string AcceptStagedOutput(
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
            if (registration.StagingReceiptId is not null)
            {
                return registration.StagingReceiptId;
            }

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
                || output.Content.SizeBytes > checked((ulong)expected.MaximumOutputBytes))
            {
                throw new InvalidOperationException("The staged output is malformed or outside its slot.");
            }

            string outputSha256 =
                Convert.ToHexString(output.Content.Value.Span).ToLowerInvariant();
            byte[] expectedManifestDigest = ManagedWorkerManifest.ComputeDigest(
                expected.StagedArtifactId,
                expected.OutputRelativeName,
                outputSha256,
                checked((long)output.Content.SizeBytes));
            if (manifest.ManifestDigest?.Algorithm != DigestAlgorithm.Sha256
                || manifest.ManifestDigest.Value.Length != expectedManifestDigest.Length
                || manifest.ManifestDigest.SizeBytes
                    != checked((ulong)expectedManifestDigest.Length)
                || !manifest.ManifestDigest.Value.Span.SequenceEqual(expectedManifestDigest))
            {
                throw new InvalidOperationException("The staged manifest digest is invalid.");
            }

            registration.Result = new ManagedWorkerResult(
                1,
                expected.BootstrapId,
                expected.AttemptId,
                expected.CoordinatorFencingEpoch,
                expected.AttemptFencingToken,
                expected.OutputRelativeName,
                outputSha256,
                checked((long)output.Content.SizeBytes));
            registration.StagingReceiptId = Guid.NewGuid().ToString("N");
            return registration.StagingReceiptId;
        }
    }

    public ManagedWorkerResult AcceptTerminal(
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
            if (request.CoordinatorFencingEpoch
                    != checked((ulong)expected.CoordinatorFencingEpoch)
                || request.AttemptFencingToken != checked((ulong)expected.AttemptFencingToken)
                || request.Outcome != WorkerTerminalOutcome.CompletedStaged
                || request.StagingReceiptId != registration.StagingReceiptId
                || registration.Result is null)
            {
                throw new InvalidOperationException("The terminal receipt is stale or incomplete.");
            }

            registration.TerminalAccepted = true;
            return registration.Result;
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
            return registration.TerminalAccepted && registration.Result is not null
                ? registration.Result
                : throw new InvalidOperationException("The worker did not complete staged publication.");
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
        if (registration is null
            || !string.Equals(registration.ConnectionId, connectionId, StringComparison.Ordinal)
            || registration.Bootstrap.ExpiresAt <= DateTimeOffset.UtcNow
            || coordinatorFencingEpoch
                != checked((ulong)registration.Bootstrap.CoordinatorFencingEpoch)
            || attemptFencingToken
                != checked((ulong)registration.Bootstrap.AttemptFencingToken))
        {
            return false;
        }

        RunRecord run = runtime.Store.GetRun(registration.Bootstrap.RunId);
        return run.State == Infinium.Domain.Contracts.LifecycleState.Running
            && run.CoordinatorFencingEpoch == registration.Bootstrap.CoordinatorFencingEpoch;
    }

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
        public ManagedWorkerResult? Result { get; set; }
        public bool TerminalAccepted { get; set; }
    }
}
