using System.Security.Cryptography;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Persistence;

namespace Infinium.Coordinator;

internal sealed record CredentialNativeProcessEvidence(
    int ProcessId,
    int ExitCode,
    string BinarySha256,
    int InheritedPrivateHandleCount,
    int StandardProtocolHandleCount,
    int ListenerCount,
    int NetworkOperationCount,
    int NativeCredentialOperationCount,
    int ProcessTreeSurvivorCount,
    bool ProcessTreeTerminated,
    bool RetryAttempted,
    bool ContainmentProbeExecuted,
    bool ExcludedHandleAccessible,
    int ActiveProcessCountBeforeJobClose,
    string? NativeCallTraceSha256,
    IReadOnlyList<CredentialNativeCallTraceEntry> NativeCallTrace);

internal sealed record CredentialNativeEntryCleanupEvidence(
    bool InitialBlank,
    bool Terminal,
    bool WindowDestroyed,
    bool BuffersCleared,
    bool ThreadJoined,
    bool ClipboardMessagesBlocked);

internal sealed record CredentialNativeCanarySurfaceEvidence(
    string Name,
    string Kind,
    long ByteCount,
    int SecretMatches,
    int RawTargetMatches);

internal sealed record CredentialNativeCanaryEvidence(
    int SecretMatches,
    int RawTargetMatches,
    IReadOnlyList<string> RawTargetEncodings,
    IReadOnlyList<CredentialNativeCanarySurfaceEvidence> ScannedSurfaces);

internal sealed record CredentialNativeCallTraceEntry(
    long Sequence,
    string Operation,
    string TargetFingerprintSha256,
    string Scenario,
    string Result,
    long? AllocationId,
    long? PairedAllocationId);

internal sealed record CredentialNativeCallCounts(
    int CredWriteW,
    int CredReadW,
    int CredDeleteW,
    int CredFree,
    int Total);

internal sealed record CredentialNativeStagingEvidence(
    string AttemptId,
    long ReceiptByteLength,
    string ReceiptSha256,
    long ResponseByteLength,
    string? ResponseSha256,
    bool StagedBeforeAdmission,
    bool CoordinatorOnlyAdmission);

internal sealed record CredentialNativeLifecycleEvidence(
    string ProfileId,
    string GenerationId,
    long GenerationOrdinal,
    long RevocationEpoch,
    string LifecycleState,
    string VerificationState,
    string RecoveryDisposition,
    string CleanupDisposition,
    long ProjectionVersion,
    string? IntentId);

internal sealed record CredentialNativeDispatchEvidence(
    string DispatchFenceId,
    string ReservationId,
    long CoordinatorFencingEpoch,
    DateTimeOffset EffectiveGateTime,
    DateTimeOffset Deadline,
    bool Authorized,
    string DecisionReason,
    string ReservationState,
    string TransportState,
    string SettlementState,
    string ResponseId,
    string UsageEntryId);

internal sealed record CredentialNativeQualificationPhaseEvidence(
    string PhaseId,
    string AssignmentId,
    HelperAssignmentKindV2 AssignmentKind,
    string ProfileId,
    string GenerationId,
    HelperOutcomeV2 Outcome,
    CredentialNativeProcessEvidence Process,
    CredentialNativeEntryCleanupEvidence? EntryCleanup,
    CredentialNativeCanaryEvidence? Canaries,
    CredentialNativeStagingEvidence Staging,
    CredentialNativeLifecycleEvidence? Lifecycle,
    CredentialNativeDispatchEvidence? Dispatch);

internal sealed record CredentialNativeQualificationScenarioEvidence(
    string ScenarioId,
    IReadOnlyList<CredentialNativeQualificationPhaseEvidence> Phases);

internal sealed record CredentialNativeQualificationEvidence(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int PrimaryPhaseSeconds,
    int CleanupReserveSeconds,
    int EvidenceReserveSeconds,
    int OuterWallClockSeconds,
    bool NamespaceBlocked,
    bool CleanupAmbiguous,
    CredentialNativeCallCounts NativeCallCounts,
    CredentialNativeStaleGateEvidence? StaleGate,
    IReadOnlyList<CredentialNativeQualificationScenarioEvidence> Scenarios);

internal sealed record CredentialNativeStaleGateEvidence(
    string ProfileId,
    long AuthorizedRevocationEpoch,
    long CurrentRevocationEpoch,
    bool Rejected,
    bool NoFenceCreated);

internal sealed class CredentialNativePreflightCollisionException(
    string targetFingerprintSha256,
    string assignmentId)
    : InvalidOperationException("A disposable target collision was detected before native mutation.")
{
    internal string TargetFingerprintSha256 { get; } = targetFingerprintSha256;
    internal string AssignmentId { get; } = assignmentId;
}

internal sealed class CredentialNativeCleanupAmbiguityException(
    string assignmentId,
    string reason)
    : InvalidOperationException("A native cleanup outcome is ambiguous; the namespace is terminally blocked.")
{
    internal string AssignmentId { get; } = assignmentId;
    internal string Reason { get; } = reason;
}

/// <summary>
/// Runs WP4 helper phases through the real coordinator and retains evidence
/// from the resulting process, staging, and authoritative lifecycle receipts.
/// This class does not derive targets or call native APIs itself.
/// </summary>
internal sealed class CredentialNativeQualificationSupervisor : IDisposable
{
    private sealed record PhaseRequirement(
        HelperOutcomeV2 Outcome,
        HelperAssignmentKindV2 Kind,
        string? LifecycleState = null,
        bool DispatchRequired = false);
    private static readonly System.Text.Json.JsonSerializerOptions TraceJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    internal static readonly TimeSpan PrimaryPhaseTimeout = TimeSpan.FromSeconds(1650);
    internal static readonly TimeSpan CleanupReserve = TimeSpan.FromSeconds(120);
    internal static readonly TimeSpan EvidenceReserve = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan OuterWallClockTimeout = TimeSpan.FromSeconds(1800);

    internal static readonly IReadOnlySet<string> RequiredScenarioIds = new HashSet<string>(
        [
            "interactive-entry-submit",
            "interactive-entry-cancel",
            "credential-size-boundaries",
            "secure-store-unavailable",
            "replacement",
            "revoke-delete",
            "helper-and-coordinator-crash-restart",
            "backup-restore-reauthentication",
            "fake-provider-dispatch",
        ],
        StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, PhaseRequirement>>
        RequiredPhaseMatrix = new Dictionary<string, IReadOnlyDictionary<string, PhaseRequirement>>(StringComparer.Ordinal)
        {
            ["interactive-entry-submit"] = Required(("preflight", Preflight()), ("submit", CompletedEnroll("active-unverified")), ("cleanup", CompletedDelete())),
            ["interactive-entry-cancel"] = Required(("preflight", Preflight()), ("cancel", new(HelperOutcomeV2.Cancelled, HelperAssignmentKindV2.Enroll, "pending-enrollment")), ("cleanup", new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Delete, "pending-enrollment"))),
            ["credential-size-boundaries"] = Required(("preflight-maximum", Preflight()), ("preflight-oversize", Preflight()), ("maximum", CompletedEnroll("active-unverified")), ("oversize", new(HelperOutcomeV2.FailedKnown, HelperAssignmentKindV2.Enroll, "pending-enrollment")), ("cleanup-maximum", CompletedDelete()), ("cleanup-oversize", new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Delete, "pending-enrollment"))),
            ["secure-store-unavailable"] = Required(("preflight", Preflight()), ("unavailable", new(HelperOutcomeV2.Unavailable, HelperAssignmentKindV2.Enroll, "secure-store-unavailable")), ("cleanup", new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Delete, "secure-store-unavailable"))),
            ["replacement"] = Required(("preflight-predecessor", Preflight()), ("preflight-successor", Preflight()), ("predecessor-active", CompletedEnroll("active-unverified")), ("replacement-interrupted", new(HelperOutcomeV2.Unavailable, HelperAssignmentKindV2.Replace, "delete-pending")), ("replacement-recovered", new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Recover, "active-unverified")), ("cleanup-predecessor", CompletedAbsence()), ("cleanup-successor", CompletedDelete())),
            ["revoke-delete"] = Required(("preflight", Preflight()), ("active", CompletedEnroll("active-unverified")), ("verify", new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Verify, "active-verified")), ("deleted-after-revocation", CompletedDelete())),
            ["helper-and-coordinator-crash-restart"] = Required(("preflight", Preflight()), ("half-commit", new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Enroll, "pending-enrollment")), ("restart-recovery", new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Recover, "active-unverified")), ("cleanup", CompletedDelete())),
            ["backup-restore-reauthentication"] = Required(("preflight-old", Preflight()), ("preflight-new", Preflight()), ("backup-active", CompletedEnroll("active-unverified")), ("restored-new-generation", new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Recover, "active-unverified")), ("cleanup-restored-predecessor", CompletedAbsence()), ("cleanup-successor", CompletedDelete())),
            ["fake-provider-dispatch"] = Required(("preflight", Preflight()), ("enroll", CompletedEnroll("active-unverified")), ("verify", new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Verify, "active-verified")), ("final-gate-dispatch-stage-admit-settle", new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.ProviderDispatch, DispatchRequired: true)), ("cleanup", CompletedDelete())),
        };

    private CredentialHelperCoordinator coordinator;
    private readonly TimeProvider timeProvider;
    private readonly int expectedInheritedPrivateHandleCount;
    private readonly IReadOnlyDictionary<string, string> targetFingerprints;
    private readonly CancellationTokenSource outerDeadline;
    private readonly CancellationTokenSource primaryDeadline;
    private CancellationTokenSource? cleanupDeadline;
    private readonly Dictionary<string, List<CredentialNativeQualificationPhaseEvidence>> scenarios =
        new(StringComparer.Ordinal);
    private readonly DateTimeOffset startedAt;
    private bool namespaceBlocked;
    private bool cleanupAmbiguous;
    private bool disposed;
    private CredentialNativeStaleGateEvidence? staleGate;

    internal CredentialNativeQualificationSupervisor(
        CredentialHelperCoordinator coordinator,
        TimeProvider? timeProvider = null,
        int expectedInheritedPrivateHandleCount = 2,
        IReadOnlyDictionary<string, string>? targetFingerprints = null)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        if (expectedInheritedPrivateHandleCount is not (2 or 3))
        {
            throw new ArgumentOutOfRangeException(nameof(expectedInheritedPrivateHandleCount));
        }
        this.expectedInheritedPrivateHandleCount = expectedInheritedPrivateHandleCount;
        this.targetFingerprints = targetFingerprints ?? new Dictionary<string, string>(StringComparer.Ordinal);
        startedAt = this.timeProvider.GetUtcNow();
        outerDeadline = new(OuterWallClockTimeout, this.timeProvider);
        primaryDeadline = new(PrimaryPhaseTimeout, this.timeProvider);
    }

    internal async Task<CredentialNativeQualificationPhaseEvidence> ExecutePreflightPhaseAsync(
        string scenarioId,
        string phaseId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset authoritativeNow,
        CancellationToken cancellationToken = default)
    {
        EnsurePrimaryPhaseAllowed(scenarioId, phaseId);
        using CancellationTokenSource bounded = LinkedPrimaryToken(cancellationToken);
        HelperProcessReceipt process = await coordinator.ExecuteNativeQualificationPreflightAsync(
            bootstrap, assignment, authoritativeNow, bounded.Token).ConfigureAwait(false);
        if (process.Receipt.Outcome != HelperOutcomeV2.FailedKnown)
        {
            CredentialNativeCallTraceEntry collision = ParseAndValidateTrace(
                    process.NativeCallTraceBytes,
                    process.NativeCredentialOperationCount,
                    requireTrace: expectedInheritedPrivateHandleCount == 2)
                .LastOrDefault(item => item.Operation == "CredReadW" && item.Result == "success")
                ?? throw new InvalidDataException("A disposable target collision lacks exact read evidence.");
            namespaceBlocked = true;
            throw new CredentialNativePreflightCollisionException(
                collision.TargetFingerprintSha256,
                assignment.Assignment.AssignmentId);
        }
        CoordinatedHelperReceipt helper = new(process, new(
            assignment.Assignment.AssignmentId + "-preflight",
            "none",
            0,
            new string('0', 64),
            null,
            0,
            null,
            StagedBeforeAdmission: false,
            CoordinatorOnlyAdmission: true));
        CredentialNativeQualificationPhaseEvidence evidence = Capture(
            phaseId, assignment.Assignment, bootstrap.Bootstrap, helper, projection: null, dispatch: null);
        if (expectedInheritedPrivateHandleCount == 2
            && (evidence.Process.NativeCallTrace.Any(item => item.Operation != "CredReadW" && item.Operation != "CredFree")
                || !evidence.Process.NativeCallTrace.Any(item =>
                    item.Operation == "CredReadW" && item.Result == "ERROR_NOT_FOUND")))
        {
            throw new InvalidOperationException("Native preflight did not prove exact ERROR_NOT_FOUND without mutation.");
        }
        Add(scenarioId, evidence);
        return evidence;
    }

    internal async Task<CredentialNativeQualificationPhaseEvidence> ExecuteCredentialTransitionPhaseAsync(
        string scenarioId,
        string phaseId,
        string attemptId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset authoritativeNow,
        CancellationToken cancellationToken = default)
    {
        EnsurePrimaryPhaseAllowed(scenarioId, phaseId);
        using CancellationTokenSource bounded = LinkedPrimaryToken(cancellationToken);
        (CoordinatedHelperReceipt helper, CredentialProfileProjection projection) =
            await coordinator.ExecuteCredentialTransitionAsync(
                attemptId,
                bootstrap,
                assignment,
                authoritativeNow,
                bounded.Token).ConfigureAwait(false);
        CredentialNativeQualificationPhaseEvidence evidence = Capture(
            phaseId,
            assignment.Assignment,
            bootstrap.Bootstrap,
            helper,
            projection,
            dispatch: null);
        Add(scenarioId, evidence);
        return evidence;
    }

    internal async Task<CredentialNativeQualificationPhaseEvidence> ExecuteInterruptedCredentialTransitionPhaseAsync(
        string scenarioId,
        string phaseId,
        string attemptId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset authoritativeNow,
        CancellationToken cancellationToken = default)
    {
        EnsurePrimaryPhaseAllowed(scenarioId, phaseId);
        using CancellationTokenSource bounded = LinkedPrimaryToken(cancellationToken);
        try
        {
            _ = await coordinator.ExecuteCredentialTransitionWithFaultAsync(
                attemptId,
                bootstrap,
                assignment,
                authoritativeNow,
                CredentialLifecycleFaultPoint.AfterHelperBeforeProjection,
                bounded.Token).ConfigureAwait(false);
            throw new InvalidOperationException("The qualification crash phase did not reach its exact interruption point.");
        }
        catch (CredentialLifecycleInterruptionException interruption)
        {
            CredentialNativeQualificationPhaseEvidence evidence = Capture(
                phaseId,
                assignment.Assignment,
                bootstrap.Bootstrap,
                interruption.Helper,
                interruption.DurableProjection,
                dispatch: null);
            Add(scenarioId, evidence);
            return evidence;
        }
    }

    internal void BeginCleanup()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (cleanupDeadline is not null)
        {
            throw new InvalidOperationException("The qualification cleanup reserve may begin only once.");
        }
        primaryDeadline.Cancel();
        cleanupDeadline = new(CleanupReserve, timeProvider);
    }

    internal async Task<CredentialNativeQualificationPhaseEvidence> ExecuteCleanupPhaseAsync(
        string scenarioId,
        string phaseId,
        string attemptId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset authoritativeNow,
        CancellationToken cancellationToken = default)
    {
        EnsureCleanupAllowed(scenarioId, phaseId);
        using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            cleanupDeadline!.Token,
            outerDeadline.Token);
        (CoordinatedHelperReceipt helper, CredentialProfileProjection projection) =
            await coordinator.ExecuteCredentialTransitionAsync(
                attemptId, bootstrap, assignment, authoritativeNow, bounded.Token).ConfigureAwait(false);
        CredentialNativeQualificationPhaseEvidence evidence = Capture(
            phaseId,
            assignment.Assignment,
            bootstrap.Bootstrap,
            helper,
            projection,
            dispatch: null);
        Add(scenarioId, evidence);
        if (helper.Process.Receipt.Outcome != HelperOutcomeV2.Completed
            || expectedInheritedPrivateHandleCount == 2
                && !evidence.Process.NativeCallTrace.Any(item =>
                    item.Operation == "CredReadW" && item.Result == "ERROR_NOT_FOUND"))
        {
            RecordCleanupAmbiguity(scenarioId, phaseId);
            throw new InvalidOperationException(
                "Exact-target cleanup did not produce a confirmed ERROR_NOT_FOUND absence proof.");
        }
        return evidence;
    }

    internal async Task<CredentialNativeQualificationPhaseEvidence> ExecuteAbsenceOnlyCleanupPhaseAsync(
        string scenarioId,
        string phaseId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset authoritativeNow,
        CancellationToken cancellationToken = default)
    {
        EnsureCleanupAllowed(scenarioId, phaseId);
        using CancellationTokenSource bounded = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, cleanupDeadline!.Token, outerDeadline.Token);
        HelperProcessReceipt process = await coordinator.ExecuteNativeQualificationPreflightAsync(
            bootstrap, assignment, authoritativeNow, bounded.Token).ConfigureAwait(false);
        CoordinatedHelperReceipt helper = new(process, new(
            assignment.Assignment.AssignmentId + "-absence",
            "none", 0, new string('0', 64), null, 0, null, false, true));
        CredentialNativeQualificationPhaseEvidence evidence = Capture(
            phaseId, assignment.Assignment, bootstrap.Bootstrap, helper, projection: null, dispatch: null);
        Add(scenarioId, evidence);
        if (process.Receipt.Outcome != HelperOutcomeV2.Completed
            || expectedInheritedPrivateHandleCount == 2
                && !evidence.Process.NativeCallTrace.Any(item =>
                    item.Operation == "CredReadW" && item.Result == "ERROR_NOT_FOUND"))
        {
            RecordCleanupAmbiguity(scenarioId, phaseId);
            throw new InvalidOperationException("Exact predecessor absence cleanup was not confirmed.");
        }
        return evidence;
    }

    internal async Task<CredentialNativeQualificationPhaseEvidence> ExecuteAuthoritativeDispatchPhaseAsync(
        string scenarioId,
        string phaseId,
        string attemptId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset authoritativeNow,
        CancellationToken cancellationToken = default)
    {
        EnsurePrimaryPhaseAllowed(scenarioId, phaseId);
        if (assignment.Assignment.AssignmentKind != HelperAssignmentKindV2.ProviderDispatch)
        {
            throw new InvalidDataException("The native qualification dispatch phase requires a provider-dispatch assignment.");
        }
        using CancellationTokenSource bounded = LinkedPrimaryToken(cancellationToken);
        AuthoritativeDispatchQualificationReceipt result =
            await coordinator.ExecuteAuthoritativeDispatchForQualificationAsync(
            attemptId,
            bootstrap,
            assignment,
            authoritativeNow,
            bounded.Token).ConfigureAwait(false);
        ProviderDispatchGateReceipt finalGate = result.FinalGate;
        if (!finalGate.Authorized)
        {
            throw new InvalidDataException("The qualification dispatch did not pass its real authoritative final gate.");
        }
        CredentialNativeQualificationPhaseEvidence evidence = Capture(
            phaseId,
            assignment.Assignment,
            bootstrap.Bootstrap,
            result.Helper,
            projection: null,
            new(
                finalGate.DispatchFenceId,
                finalGate.ReservationId,
                finalGate.CoordinatorFencingEpoch,
                finalGate.EffectiveGateTime,
                finalGate.Deadline,
                finalGate.Authorized,
                finalGate.DecisionReason,
                "reserved-authoritative",
                "may-have-started-durable",
                result.Settlement.Kind.ToString(),
                result.Persisted.ResponseId,
                result.Persisted.UsageEntryId));
        Add(scenarioId, evidence);
        return evidence;
    }

    internal void RecordCleanupAmbiguity(string scenarioId, string phaseId)
    {
        EnsureKnownScenario(scenarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseId);
        cleanupAmbiguous = true;
        namespaceBlocked = true;
        primaryDeadline.Cancel();
    }

    internal void RejectNativeNamespaceBlockForTest(HelperProcessReceipt process, string assignmentId)
    {
        ArgumentNullException.ThrowIfNull(process);
        if (!process.NativeNamespaceReuseBlocked)
        {
            return;
        }
        namespaceBlocked = true;
        cleanupAmbiguous = true;
        throw new CredentialNativeCleanupAmbiguityException(
            assignmentId,
            process.NativeNamespaceReuseBlockReason ?? "native-cleanup-ambiguity");
    }

    internal void RecordStaleGateRejection(
        string profileId,
        long authorizedRevocationEpoch,
        long currentRevocationEpoch,
        bool rejected,
        bool noFenceCreated)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        if (!rejected || !noFenceCreated || currentRevocationEpoch <= authorizedRevocationEpoch)
        {
            throw new InvalidOperationException("Stale undispatched final-gate rejection was not authoritative.");
        }
        staleGate = new(profileId, authorizedRevocationEpoch, currentRevocationEpoch, rejected, noFenceCreated);
    }

    internal void RebindCoordinator(CredentialHelperCoordinator value) =>
        coordinator = value ?? throw new ArgumentNullException(nameof(value));

    internal CredentialNativeQualificationEvidence CompleteSuccessfulRun()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        outerDeadline.Token.ThrowIfCancellationRequested();
        if (cleanupAmbiguous || namespaceBlocked)
        {
            throw new InvalidOperationException("A cleanup ambiguity is terminal and cannot produce passing WP4 evidence.");
        }
        if (staleGate is not { Rejected: true, NoFenceCreated: true })
        {
            throw new InvalidOperationException("The revoke/delete scenario lacks authoritative stale-gate rejection evidence.");
        }
        List<string> missing = [];
        foreach ((string scenario, IReadOnlyDictionary<string, PhaseRequirement> phases) in RequiredPhaseMatrix)
        {
            if (!scenarios.TryGetValue(scenario, out List<CredentialNativeQualificationPhaseEvidence>? actual))
            {
                missing.Add(scenario + ":*");
                continue;
            }
            foreach ((string phase, PhaseRequirement requirement) in phases)
            {
                if (!actual.Any(item => item.PhaseId == phase
                    && item.Outcome == requirement.Outcome
                    && item.AssignmentKind == requirement.Kind
                    && (requirement.LifecycleState is null
                        || item.Lifecycle?.LifecycleState == requirement.LifecycleState)
                    && (!requirement.DispatchRequired
                        || item.Dispatch is { Authorized: true }
                            && item.Staging.StagedBeforeAdmission
                            && item.Staging.CoordinatorOnlyAdmission)))
                {
                    CredentialNativeQualificationPhaseEvidence? observed = actual.FirstOrDefault(item => item.PhaseId == phase);
                    missing.Add(scenario + ":" + phase + "=" + requirement.Outcome
                        + " observed=" + observed?.Outcome + "/" + observed?.AssignmentKind
                        + "/" + observed?.Lifecycle?.LifecycleState);
                }
            }
        }
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "The native qualification is missing exercised phase results: " + string.Join(", ", missing));
        }
        return Snapshot();
    }

    internal CredentialNativeQualificationEvidence CaptureTerminalFailure()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!cleanupAmbiguous || !namespaceBlocked)
        {
            throw new InvalidOperationException("Terminal failure evidence requires an actual cleanup ambiguity.");
        }
        return Snapshot();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        primaryDeadline.Dispose();
        cleanupDeadline?.Dispose();
        outerDeadline.Dispose();
    }

    private CredentialNativeQualificationPhaseEvidence Capture(
        string phaseId,
        HelperAssignmentV2 assignment,
        HelperBootstrapV2 bootstrap,
        CoordinatedHelperReceipt helper,
        CredentialProfileProjection? projection,
        CredentialNativeDispatchEvidence? dispatch)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseId);
        HelperProcessReceipt process = helper.Process;
        if (process.NativeNamespaceReuseBlocked)
        {
            namespaceBlocked = true;
            cleanupAmbiguous = true;
            throw new CredentialNativeCleanupAmbiguityException(
                assignment.AssignmentId,
                process.NativeNamespaceReuseBlockReason ?? "native-cleanup-ambiguity");
        }
        if (process.InheritedPrivateHandleCount != expectedInheritedPrivateHandleCount
            || process.StandardProtocolHandleCount != 0
            || process.ListenerCount != 0
            || process.NetworkOperationCount != 0
            || process.ProcessTreeSurvivorCount != 0
            || !process.ProcessTreeTerminated
            || process.RetryAttempted)
        {
            throw new InvalidDataException("The qualification helper violated its process, handle, network, or retry boundary.");
        }
        IReadOnlyList<CredentialNativeCallTraceEntry> trace = ParseAndValidateTrace(
            process.NativeCallTraceBytes,
            process.NativeCredentialOperationCount,
            requireTrace: expectedInheritedPrivateHandleCount == 2);
        string profileId = assignment.AccessProfileId?.Value
            ?? throw new InvalidDataException("Qualification evidence requires an exact profile identity.");
        string generationId = assignment.GenerationId?.Value
            ?? throw new InvalidDataException("Qualification evidence requires an exact generation identity.");
        if (trace.Any(item => item.Scenario != assignment.AssignmentId))
        {
            throw new InvalidDataException("A native call trace is not bound to its exact assignment phase.");
        }
        HashSet<string> allowedFingerprints = [];
        if (targetFingerprints.TryGetValue(profileId + "/" + generationId, out string? expectedFingerprint))
        {
            allowedFingerprints.Add(expectedFingerprint);
        }
        string? bootstrapProfile = bootstrap.Credential?.AccessProfileId?.Value;
        string? bootstrapGeneration = bootstrap.Credential?.GenerationId?.Value;
        if (bootstrapProfile is not null && bootstrapGeneration is not null
            && targetFingerprints.TryGetValue(bootstrapProfile + "/" + bootstrapGeneration, out string? predecessorFingerprint))
        {
            allowedFingerprints.Add(predecessorFingerprint);
        }
        if (allowedFingerprints.Count > 0
            && trace.Any(item => !allowedFingerprints.Contains(item.TargetFingerprintSha256)))
        {
            throw new InvalidDataException("A native call trace escaped its exact manifest target.");
        }
        CredentialNativeCanaryEvidence? canaries = ParseCanaries(
            process.NativeCanaryEvidenceBytes,
            requireEvidence: expectedInheritedPrivateHandleCount == 2);
        CredentialNativeEntryCleanupEvidence? entryCleanup = ParseEntryCleanup(process.NativeEntryCleanupBytes);
        bool manualEntry = expectedInheritedPrivateHandleCount == 2
            && RequiresManualEntryEvidence(assignment.AssignmentId, assignment.AssignmentKind);
        if (manualEntry
            && (entryCleanup is null || !entryCleanup.InitialBlank || !entryCleanup.Terminal
                || !entryCleanup.WindowDestroyed || !entryCleanup.BuffersCleared
                || !entryCleanup.ThreadJoined || !entryCleanup.ClipboardMessagesBlocked))
        {
            throw new InvalidDataException("A manual native entry phase lacks exact UI cleanup evidence.");
        }
        CredentialNativeProcessEvidence processEvidence = new(
            process.ProcessId,
            process.ExitCode,
            process.BinarySha256,
            process.InheritedPrivateHandleCount,
            process.StandardProtocolHandleCount,
            process.ListenerCount,
            process.NetworkOperationCount,
            process.NativeCredentialOperationCount,
            process.ProcessTreeSurvivorCount,
            process.ProcessTreeTerminated,
            process.RetryAttempted,
            process.ContainmentProbeExecuted,
            process.ExcludedHandleAccessible,
            process.ActiveProcessCountBeforeJobClose,
            process.NativeCallTraceBytes is null
                ? null
                : Convert.ToHexStringLower(SHA256.HashData(process.NativeCallTraceBytes)),
            trace);
        HelperStagingReceipt staging = helper.Staging;
        CredentialNativeStagingEvidence stagingEvidence = new(
            staging.AttemptId,
            staging.ByteLength,
            staging.Sha256,
            staging.ResponseByteLength,
            staging.ResponseSha256,
            staging.StagedBeforeAdmission,
            staging.CoordinatorOnlyAdmission);
        CredentialNativeLifecycleEvidence? lifecycle = projection is null
            ? null
            : new(
                projection.ProfileId,
                projection.GenerationId,
                projection.GenerationOrdinal,
                projection.RevocationEpoch,
                projection.LifecycleState,
                projection.VerificationState,
                projection.RecoveryDisposition,
                projection.CleanupDisposition,
                projection.ProjectionVersion,
                projection.IntentId);
        return new(
            phaseId,
            assignment.AssignmentId,
            assignment.AssignmentKind,
            profileId,
            generationId,
            process.Receipt.Outcome,
            processEvidence,
            entryCleanup,
            canaries,
            stagingEvidence,
            lifecycle,
            dispatch);
    }

    private void Add(string scenarioId, CredentialNativeQualificationPhaseEvidence evidence)
    {
        EnsureKnownScenario(scenarioId);
        if (!scenarios.TryGetValue(scenarioId, out List<CredentialNativeQualificationPhaseEvidence>? phases))
        {
            phases = [];
            scenarios.Add(scenarioId, phases);
        }
        if (phases.Any(item => string.Equals(item.PhaseId, evidence.PhaseId, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("A native qualification phase identity cannot be reused.");
        }
        phases.Add(evidence);
    }

    private void EnsurePrimaryPhaseAllowed(string scenarioId, string phaseId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        EnsureKnownScenario(scenarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseId);
        if (cleanupAmbiguous || namespaceBlocked)
        {
            throw new InvalidOperationException("Cleanup ambiguity blocks every later helper or credential operation in the namespace.");
        }
        outerDeadline.Token.ThrowIfCancellationRequested();
        primaryDeadline.Token.ThrowIfCancellationRequested();
    }

    private void EnsureCleanupAllowed(string scenarioId, string phaseId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        EnsureKnownScenario(scenarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(phaseId);
        if (cleanupDeadline is null)
        {
            throw new InvalidOperationException("The bounded cleanup reserve has not begun.");
        }
        if (cleanupAmbiguous || namespaceBlocked)
        {
            throw new InvalidOperationException("Cleanup ambiguity blocks every later helper or credential operation in the namespace.");
        }
        outerDeadline.Token.ThrowIfCancellationRequested();
        cleanupDeadline.Token.ThrowIfCancellationRequested();
    }

    private CancellationTokenSource LinkedPrimaryToken(CancellationToken cancellationToken) =>
        CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            primaryDeadline.Token,
            outerDeadline.Token);

    private static void EnsureKnownScenario(string scenarioId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scenarioId);
        if (!RequiredScenarioIds.Contains(scenarioId))
        {
            throw new InvalidDataException("The native qualification scenario is not declared by the accepted manifest.");
        }
    }

    private CredentialNativeQualificationEvidence Snapshot() => new(
        startedAt,
        timeProvider.GetUtcNow(),
        checked((int)PrimaryPhaseTimeout.TotalSeconds),
        checked((int)CleanupReserve.TotalSeconds),
        checked((int)EvidenceReserve.TotalSeconds),
        checked((int)OuterWallClockTimeout.TotalSeconds),
        namespaceBlocked,
        cleanupAmbiguous,
        AggregateCallCounts(),
        staleGate,
        scenarios
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new CredentialNativeQualificationScenarioEvidence(pair.Key, pair.Value.ToArray()))
            .ToArray());

    internal static IReadOnlyList<CredentialNativeCallTraceEntry> ParseAndValidateTrace(
        byte[]? canonicalTraceBytes,
        int reportedOperationCount,
        bool requireTrace)
    {
        if (canonicalTraceBytes is null)
        {
            if (requireTrace || reportedOperationCount != 0)
            {
                throw new InvalidDataException("Native process evidence is missing its canonical call trace.");
            }
            return [];
        }
        CredentialNativeCallTraceEntry[] trace = System.Text.Json.JsonSerializer.Deserialize<CredentialNativeCallTraceEntry[]>(
            canonicalTraceBytes,
            TraceJsonOptions)
            ?? throw new InvalidDataException("The canonical native call trace is absent.");
        Dictionary<long, CredentialNativeCallTraceEntry> allocations = [];
        HashSet<long> freed = [];
        HashSet<string> allowed = ["CredWriteW", "CredReadW", "CredDeleteW", "CredFree"];
        for (int index = 0; index < trace.Length; index++)
        {
            CredentialNativeCallTraceEntry item = trace[index];
            if (item.Sequence != index + 1
                || !allowed.Contains(item.Operation)
                || item.TargetFingerprintSha256.Length != 64
                || !item.TargetFingerprintSha256.All(char.IsAsciiHexDigit)
                || string.IsNullOrWhiteSpace(item.Scenario)
                || string.IsNullOrWhiteSpace(item.Result))
            {
                throw new InvalidDataException("The canonical native call trace is malformed or out of order.");
            }
            if (item.Operation == "CredReadW" && item.Result == "success")
            {
                if (item.AllocationId is null || item.PairedAllocationId is not null
                    || !allocations.TryAdd(item.AllocationId.Value, item))
                {
                    throw new InvalidDataException("A successful CredReadW lacks one unique allocation identity.");
                }
            }
            else if (item.Operation == "CredFree")
            {
                if (item.AllocationId is not null || item.PairedAllocationId is null
                    || !allocations.TryGetValue(item.PairedAllocationId.Value, out CredentialNativeCallTraceEntry? read)
                    || read.TargetFingerprintSha256 != item.TargetFingerprintSha256
                    || read.Scenario != item.Scenario
                    || !freed.Add(item.PairedAllocationId.Value))
                {
                    throw new InvalidDataException("CredFree is not paired exactly once after its successful read allocation.");
                }
            }
            else if (item.AllocationId is not null || item.PairedAllocationId is not null)
            {
                throw new InvalidDataException("Only successful reads and their CredFree release may name allocations.");
            }
        }
        if (allocations.Keys.Any(id => !freed.Contains(id)) || reportedOperationCount != trace.Length)
        {
            throw new InvalidDataException("Native call counts or successful-read/free pairing disagree with the canonical trace.");
        }
        return trace;
    }

    private static CredentialNativeCanaryEvidence? ParseCanaries(byte[]? bytes, bool requireEvidence)
    {
        if (bytes is null)
        {
            if (requireEvidence)
            {
                throw new InvalidDataException("Native process evidence is missing its canary scan receipt.");
            }
            return null;
        }
        CredentialNativeCanaryEvidence evidence = System.Text.Json.JsonSerializer.Deserialize<CredentialNativeCanaryEvidence>(
            bytes, TraceJsonOptions) ?? throw new InvalidDataException("The native canary scan receipt is malformed.");
        if (evidence.SecretMatches != 0 || evidence.RawTargetMatches != 0
            || !evidence.RawTargetEncodings.SequenceEqual(["utf-8", "utf-16le"], StringComparer.Ordinal)
            || evidence.ScannedSurfaces.Count == 0
            || evidence.ScannedSurfaces.Any(surface => surface.ByteCount < 0
                || surface.SecretMatches != 0 || surface.RawTargetMatches != 0
                || string.IsNullOrWhiteSpace(surface.Name) || string.IsNullOrWhiteSpace(surface.Kind)))
        {
            throw new InvalidDataException("The native canary scan found a forbidden value or lacks concrete surfaces.");
        }
        return evidence;
    }

    internal static bool RequiresManualEntryEvidence(
        string assignmentId,
        HelperAssignmentKindV2 assignmentKind) =>
        CredentialNativeQualificationPhasesV2.Parse(assignmentId, assignmentKind).SecretMode
            == CredentialNativeQualificationSecretModeV2.Manual;

    private static CredentialNativeEntryCleanupEvidence? ParseEntryCleanup(byte[]? bytes) => bytes is null
        ? null
        : System.Text.Json.JsonSerializer.Deserialize<CredentialNativeEntryCleanupEvidence>(bytes, TraceJsonOptions)
            ?? throw new InvalidDataException("The native entry cleanup receipt is malformed.");

    private CredentialNativeCallCounts AggregateCallCounts()
    {
        CredentialNativeCallTraceEntry[] trace = scenarios.Values.SelectMany(value => value)
            .SelectMany(phase => phase.Process.NativeCallTrace)
            .ToArray();
        return new(
            trace.Count(item => item.Operation == "CredWriteW"),
            trace.Count(item => item.Operation == "CredReadW"),
            trace.Count(item => item.Operation == "CredDeleteW"),
            trace.Count(item => item.Operation == "CredFree"),
            trace.Length);
    }

    private static PhaseRequirement CompletedEnroll(string lifecycle) =>
        new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Enroll, lifecycle);

    private static PhaseRequirement CompletedDelete() =>
        new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Delete, "deleted");

    private static PhaseRequirement Preflight() =>
        new(HelperOutcomeV2.FailedKnown, HelperAssignmentKindV2.Verify);

    private static PhaseRequirement CompletedAbsence() =>
        new(HelperOutcomeV2.Completed, HelperAssignmentKindV2.Delete);

    private static Dictionary<string, PhaseRequirement> Required(
        params (string Phase, PhaseRequirement Requirement)[] phases) =>
        phases.ToDictionary(item => item.Phase, item => item.Requirement, StringComparer.Ordinal);
}
