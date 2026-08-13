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
    int TotalContainedProcessCount,
    string? NativeCallTraceSha256,
    IReadOnlyList<CredentialNativeCallTraceEntry> NativeCallTrace);

internal sealed record CredentialNativeEntryCleanupEvidence(
    bool InitialBlank,
    bool Terminal,
    bool WindowDestroyed,
    bool BuffersCleared,
    bool ThreadJoined,
    bool ClipboardMessagesBlocked,
    CredentialNativeEntryReadinessEvidence? Readiness,
    CredentialNativeEntryReadinessEvidence? ActionReadiness,
    string? ActionSource,
    string? Action,
    int PreReadinessTerminalMessages = 0,
    int PreReadinessIgnoredMessages = 0);

internal sealed record CredentialNativeEntryReadinessEvidence(
    int OwnerProcessId,
    uint OwnerThreadId,
    int OwnerSessionId,
    string DesktopNameSha256,
    bool InteractiveInputDesktop,
    bool DesktopObjectMatches,
    bool OwnerProcessMatches,
    bool OwnerThreadMatches,
    bool TopLevelWindow,
    bool WindowVisible,
    bool WindowEnabled,
    bool WindowNotCloaked,
    bool WindowIntersectsActiveMonitor,
    bool InstructionOwned,
    bool InstructionVisible,
    string InstructionMode,
    string InstructionFingerprintSha256,
    bool EditOwned,
    bool EditVisible,
    bool EditEnabled,
    bool EditMasked,
    bool SubmitOwned,
    bool SubmitVisible,
    bool SubmitEnabled,
    bool SubmitFocused,
    bool CancelOwned,
    bool CancelVisible,
    bool CancelEnabled,
    bool CancelFocused,
    bool Foreground,
    bool EditFocused,
    long ReadinessDeadlineMilliseconds,
    long ReadinessElapsedMilliseconds);

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
    CredentialNativeFailedManualPhaseEvidence? FailedManualPhase,
    IReadOnlyList<CredentialNativeQualificationScenarioEvidence> Scenarios);

internal sealed record CredentialNativeFailedManualPhaseEvidence(
    string AssignmentId,
    HelperOutcomeV2 Outcome,
    int ProcessId,
    int CredWriteW,
    int NativeCallTotal,
    CredentialNativeEntryCleanupEvidence? EntryCleanup,
    CredentialNativeStagingEvidence Staging,
    string Disposition,
    int ReportedNativeOperationCount,
    int EntryCleanupByteLength,
    string? EntryCleanupSha256,
    string EntryCleanupParseResult,
    int NativeTraceByteLength,
    string? NativeTraceSha256,
    string NativeTraceParseResult,
    int CanaryByteLength,
    string? CanarySha256,
    string CanaryParseResult,
    int ExitCode,
    int InheritedPrivateHandleCount,
    int StandardProtocolHandleCount,
    int ListenerCount,
    int NetworkOperationCount,
    int ProcessTreeSurvivorCount,
    bool ProcessTreeTerminated,
    bool RetryAttempted,
    bool ContainmentProbeExecuted,
    bool ExcludedHandleAccessible,
    int ActiveProcessCountBeforeJobClose,
    int TotalContainedProcessCount,
    bool NamespaceReuseBlocked,
    string? NamespaceReuseBlockReason,
    bool TraceCanonical,
    string ValidationStage,
    string ValidationReason);

internal enum CredentialNativeManualValidationStage
{
    ProcessJobBoundary,
    NativeTrace,
    ExactTarget,
    Canary,
    ManualUi,
    Lifecycle,
    PhaseCapture,
}

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

internal sealed class CredentialNativePrimaryFailureException(
    string failureType,
    string cleanupDisposition,
    CredentialNativeQualificationEvidence evidence,
    Exception innerException)
    : InvalidOperationException(
        "The native qualification primary phase failed after bounded exact-target cleanup.",
        innerException)
{
    internal string FailureType { get; } = failureType;
    internal string CleanupDisposition { get; } = cleanupDisposition;
    internal CredentialNativeQualificationEvidence Evidence { get; } = evidence;
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
    private CredentialNativeFailedManualPhaseEvidence? failedManualPhase;
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
        CredentialNativeFailedManualPhaseEvidence? rawManual = ObserveManualPhase(
            assignment.Assignment, helper);
        CredentialNativeQualificationPhaseEvidence evidence;
        try
        {
            evidence = Capture(
                phaseId,
                assignment.Assignment,
                bootstrap.Bootstrap,
                helper,
                projection,
                dispatch: null);
        }
        catch (Exception exception)
        {
            PromoteManualFailure(rawManual, ClassifyValidationFailure(exception));
            throw;
        }
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

    internal void RecordTerminalCleanupAmbiguity(string evidenceContext, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
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

    internal CredentialNativeQualificationEvidence CapturePrimaryFailureAfterCertainCleanup()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (cleanupDeadline is null || cleanupAmbiguous || namespaceBlocked)
        {
            throw new InvalidOperationException(
                "Primary-failure evidence requires completed, certain bounded cleanup.");
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
            throw ValidationFailure(
                CredentialNativeManualValidationStage.ProcessJobBoundary,
                "process-job-boundary-rejected");
        }
        IReadOnlyList<CredentialNativeCallTraceEntry> trace;
        try
        {
            trace = ParseAndValidateTrace(
                process.NativeCallTraceBytes,
                process.NativeCredentialOperationCount,
                requireTrace: expectedInheritedPrivateHandleCount == 2);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or System.Text.Json.JsonException or NotSupportedException)
        {
            throw ValidationFailure(
                CredentialNativeManualValidationStage.NativeTrace,
                "native-trace-rejected",
                exception);
        }
        string profileId = assignment.AccessProfileId?.Value
            ?? throw new InvalidDataException("Qualification evidence requires an exact profile identity.");
        string generationId = assignment.GenerationId?.Value
            ?? throw new InvalidDataException("Qualification evidence requires an exact generation identity.");
        if (trace.Any(item => item.Scenario != assignment.AssignmentId))
        {
            throw ValidationFailure(
                CredentialNativeManualValidationStage.ExactTarget,
                "trace-assignment-binding-rejected");
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
            throw ValidationFailure(
                CredentialNativeManualValidationStage.ExactTarget,
                "trace-target-binding-rejected");
        }
        CredentialNativeCanaryEvidence? canaries;
        try
        {
            canaries = ParseCanaries(
                process.NativeCanaryEvidenceBytes,
                requireEvidence: expectedInheritedPrivateHandleCount == 2);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or System.Text.Json.JsonException or NotSupportedException)
        {
            throw ValidationFailure(
                CredentialNativeManualValidationStage.Canary,
                "canary-evidence-rejected",
                exception);
        }
        CredentialNativeEntryCleanupEvidence? entryCleanup;
        try
        {
            entryCleanup = ParseEntryCleanup(process.NativeEntryCleanupBytes);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or System.Text.Json.JsonException or NotSupportedException)
        {
            throw ValidationFailure(
                CredentialNativeManualValidationStage.ManualUi,
                "entry-cleanup-json-rejected",
                exception);
        }
        bool manualEntry = expectedInheritedPrivateHandleCount == 2
            && RequiresManualEntryEvidence(assignment.AssignmentId, assignment.AssignmentKind);
        bool invalidManualEntry = manualEntry
            && (entryCleanup is null || !entryCleanup.InitialBlank || !entryCleanup.Terminal
                || !entryCleanup.WindowDestroyed || !entryCleanup.BuffersCleared
                || !entryCleanup.ThreadJoined || !entryCleanup.ClipboardMessagesBlocked
                || entryCleanup.PreReadinessTerminalMessages < 0
                || entryCleanup.PreReadinessIgnoredMessages < 0
                || entryCleanup.Readiness is not
                {
                    InteractiveInputDesktop: true,
                    DesktopObjectMatches: true,
                    OwnerProcessMatches: true,
                    OwnerThreadMatches: true,
                    TopLevelWindow: true,
                    WindowVisible: true,
                    WindowEnabled: true,
                    WindowNotCloaked: true,
                    WindowIntersectsActiveMonitor: true,
                    InstructionOwned: true,
                    InstructionVisible: true,
                    EditOwned: true,
                    EditVisible: true,
                    EditEnabled: true,
                    EditMasked: true,
                    SubmitOwned: true,
                    SubmitVisible: true,
                    SubmitEnabled: true,
                    CancelOwned: true,
                    CancelVisible: true,
                    CancelEnabled: true,
                    Foreground: true,
                    EditFocused: true,
                } readiness
                || readiness.OwnerProcessId != process.ProcessId
                || readiness.OwnerThreadId == 0
                || readiness.OwnerSessionId != System.Diagnostics.Process.GetCurrentProcess().SessionId
                || readiness.DesktopNameSha256.Length != 64
                || !readiness.DesktopNameSha256.All(char.IsAsciiHexDigit)
                || readiness.InstructionFingerprintSha256.Length != 64
                || !readiness.InstructionFingerprintSha256.All(char.IsAsciiHexDigit)
                || readiness.InstructionMode != (assignment.AssignmentId.Contains(
                    "interactive-entry-cancel", StringComparison.Ordinal) ? "cancel" : "submit")
                || !string.Equals(readiness.InstructionFingerprintSha256,
                    Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                        readiness.InstructionMode == "submit"
                            ? "Enter a disposable test value, then click Submit. Never use a real credential."
                            : "Leave the field blank, then click Cancel."))),
                    StringComparison.Ordinal)
                || readiness.ReadinessDeadlineMilliseconds is < 1 or > 10_000
                || readiness.ReadinessElapsedMilliseconds < 0
                || readiness.ReadinessElapsedMilliseconds > readiness.ReadinessDeadlineMilliseconds
                || entryCleanup.ActionReadiness is not
                {
                    InteractiveInputDesktop: true,
                    DesktopObjectMatches: true,
                    OwnerProcessMatches: true,
                    OwnerThreadMatches: true,
                    TopLevelWindow: true,
                    WindowVisible: true,
                    WindowEnabled: true,
                    WindowNotCloaked: true,
                    WindowIntersectsActiveMonitor: true,
                    InstructionOwned: true,
                    InstructionVisible: true,
                    EditOwned: true,
                    EditVisible: true,
                    EditEnabled: true,
                    EditMasked: true,
                    SubmitOwned: true,
                    SubmitVisible: true,
                    SubmitEnabled: true,
                    CancelOwned: true,
                    CancelVisible: true,
                    CancelEnabled: true,
                    Foreground: true,
                } actionReadiness
                || actionReadiness.OwnerProcessId != process.ProcessId
                || actionReadiness.OwnerThreadId != readiness.OwnerThreadId
                || actionReadiness.OwnerSessionId != readiness.OwnerSessionId
                || actionReadiness.DesktopNameSha256 != readiness.DesktopNameSha256
                || actionReadiness.InstructionMode != readiness.InstructionMode
                || actionReadiness.InstructionFingerprintSha256 != readiness.InstructionFingerprintSha256
                || actionReadiness.ReadinessDeadlineMilliseconds is < 1 or > 10_000
                || actionReadiness.ReadinessElapsedMilliseconds < 0
                || actionReadiness.ReadinessElapsedMilliseconds > actionReadiness.ReadinessDeadlineMilliseconds
                || entryCleanup.ActionSource == "editkey" && !actionReadiness.EditFocused
                || entryCleanup.ActionSource == "submitbutton" && !actionReadiness.SubmitFocused
                || entryCleanup.ActionSource == "cancelbutton" && !actionReadiness.CancelFocused
                || !IsActionSourceBindingValid(entryCleanup.Action, entryCleanup.ActionSource)
                || entryCleanup.ActionSource is not ("editkey" or "submitbutton" or "cancelbutton")
                || process.Receipt.Outcome == HelperOutcomeV2.Cancelled && entryCleanup.Action != "cancel"
                || process.Receipt.Outcome == HelperOutcomeV2.Completed && entryCleanup.Action != "submit"
                || entryCleanup.Action is not ("submit" or "cancel"));
        if (invalidManualEntry)
        {
            throw ValidationFailure(
                CredentialNativeManualValidationStage.ManualUi,
                "entry-cleanup-semantic-rejected");
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
            process.TotalContainedProcessCount,
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

    internal static string ClassifyManualPhaseFailure(
        HelperOutcomeV2 outcome,
        bool? initialBlank,
        bool readinessPresent,
        int credWriteCount,
        int nativeCallTotal) =>
        outcome == HelperOutcomeV2.FailedKnown
            && initialBlank == false
            && readinessPresent
            && credWriteCount == 0
            && nativeCallTotal == 0
            ? "failed-prewrite-ui-readiness"
            : "failed-manual-phase-evidence-validation";

    private static readonly HashSet<string> ManualAssignmentIds = new(
        [
            "wp4-v2/interactive-entry-submit/submit",
            "wp4-v2/interactive-entry-cancel/cancel",
            "wp4-v2/backup-restore-reauthentication/restored-new-generation",
        ],
        StringComparer.Ordinal);

    private static CredentialNativeFailedManualPhaseEvidence? ObserveManualPhase(
        HelperAssignmentV2 assignment,
        CoordinatedHelperReceipt helper)
    {
        HelperProcessReceipt process = helper.Process;
        if (!ManualAssignmentIds.Contains(assignment.AssignmentId))
        {
            return null;
        }

        // This is deliberately nonthrowing and is the first operation after a
        // manual helper result returns. It preserves raw, sanitized facts even
        // when every later semantic validator rejects the phase.
        CredentialNativeEntryCleanupEvidence? entryCleanup = null;
        string entryParse = "absent";
        if (process.NativeEntryCleanupBytes is not null)
        {
            try
            {
                entryCleanup = System.Text.Json.JsonSerializer.Deserialize<CredentialNativeEntryCleanupEvidence>(
                    process.NativeEntryCleanupBytes, TraceJsonOptions);
                entryParse = entryCleanup is null ? "json-null" : "parsed";
            }
            catch (Exception exception)
            {
                entryParse = "malformed-" + exception.GetType().Name;
            }
        }
        CredentialNativeCallTraceEntry[] rawTrace = [];
        string traceParse = "absent";
        bool traceCanonical = false;
        if (process.NativeCallTraceBytes is not null)
        {
            try
            {
                rawTrace = ParseAndValidateTrace(
                    process.NativeCallTraceBytes,
                    process.NativeCredentialOperationCount,
                    requireTrace: true).ToArray();
                traceCanonical = true;
                traceParse = "canonical-validated";
            }
            catch (Exception exception)
            {
                traceParse = "malformed-" + exception.GetType().Name;
            }
        }
        string canaryParse = "absent";
        if (process.NativeCanaryEvidenceBytes is not null)
        {
            try
            {
                CredentialNativeCanaryEvidence? parsed =
                    System.Text.Json.JsonSerializer.Deserialize<CredentialNativeCanaryEvidence>(
                    process.NativeCanaryEvidenceBytes, TraceJsonOptions);
                canaryParse = parsed is null ? "json-null" : "parsed";
            }
            catch (Exception exception)
            {
                canaryParse = "malformed-" + exception.GetType().Name;
            }
        }
        int writes = traceCanonical
            ? rawTrace.Count(item => item.Operation == "CredWriteW")
            : -1;
        int retainedTraceCount = traceCanonical ? rawTrace.Length : -1;
        return new(
            assignment.AssignmentId,
            process.Receipt.Outcome,
            process.ProcessId,
            writes,
            retainedTraceCount,
            entryCleanup,
            StagingEvidence(helper.Staging),
            traceCanonical ? ClassifyManualPhaseFailure(
                process.Receipt.Outcome,
                entryCleanup?.InitialBlank,
                entryCleanup?.Readiness is not null,
                writes,
                rawTrace.Length) : "failed-manual-phase-trace-unvalidated",
            process.NativeCredentialOperationCount,
            ByteLength(process.NativeEntryCleanupBytes),
            ByteHash(process.NativeEntryCleanupBytes),
            entryParse,
            ByteLength(process.NativeCallTraceBytes),
            ByteHash(process.NativeCallTraceBytes),
            traceParse,
            ByteLength(process.NativeCanaryEvidenceBytes),
            ByteHash(process.NativeCanaryEvidenceBytes),
            canaryParse,
            process.ExitCode,
            process.InheritedPrivateHandleCount,
            process.StandardProtocolHandleCount,
            process.ListenerCount,
            process.NetworkOperationCount,
            process.ProcessTreeSurvivorCount,
            process.ProcessTreeTerminated,
            process.RetryAttempted,
            process.ContainmentProbeExecuted,
            process.ExcludedHandleAccessible,
            process.ActiveProcessCountBeforeJobClose,
            process.TotalContainedProcessCount,
            process.NativeNamespaceReuseBlocked,
            process.NativeNamespaceReuseBlockReason,
            traceCanonical,
            "raw-observation",
            "pending-validation");
    }

    private void PromoteManualFailure(
        CredentialNativeFailedManualPhaseEvidence? raw,
        (CredentialNativeManualValidationStage Stage, string ReasonCode) failure)
    {
        if (raw is null)
        {
            return;
        }
        failedManualPhase = raw with
        {
            ValidationStage = failure.Stage.ToString(),
            ValidationReason = failure.ReasonCode,
        };
    }

    private static (CredentialNativeManualValidationStage Stage, string ReasonCode)
        ClassifyValidationFailure(Exception exception) =>
            exception.Data[nameof(CredentialNativeManualValidationStage)] is CredentialNativeManualValidationStage stage
                && exception.Data["CredentialNativeManualValidationReasonCode"] is string reasonCode
            ? (stage, reasonCode)
            : (CredentialNativeManualValidationStage.PhaseCapture,
                "typed-" + exception.GetType().Name);

    private static int ByteLength(byte[]? value) => value?.Length ?? 0;

    private static InvalidDataException ValidationFailure(
        CredentialNativeManualValidationStage stage,
        string reasonCode,
        Exception? innerException = null)
    {
        InvalidDataException failure = new(
            "A qualification phase failed a typed evidence-validation stage.",
            innerException);
        failure.Data[nameof(CredentialNativeManualValidationStage)] = stage;
        failure.Data["CredentialNativeManualValidationReasonCode"] = reasonCode;
        return failure;
    }

    private static string? ByteHash(byte[]? value) => value is null
        ? null
        : Convert.ToHexStringLower(SHA256.HashData(value));

    internal void ObserveThenRejectManualPhaseForTest(
        HelperAssignmentV2 assignment,
        CoordinatedHelperReceipt helper,
        CredentialNativeManualValidationStage stage,
        string reasonCode)
    {
        CredentialNativeFailedManualPhaseEvidence? raw = ObserveManualPhase(assignment, helper);
        PromoteManualFailure(raw, (stage, reasonCode));
    }

    internal void CaptureManualPhaseForTest(
        string phaseId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        CoordinatedHelperReceipt helper,
        CredentialProfileProjection? projection = null)
    {
        CredentialNativeFailedManualPhaseEvidence? raw = ObserveManualPhase(
            assignment.Assignment, helper);
        try
        {
            _ = Capture(
                phaseId,
                assignment.Assignment,
                bootstrap.Bootstrap,
                helper,
                projection,
                dispatch: null);
        }
        catch (Exception exception)
        {
            PromoteManualFailure(raw, ClassifyValidationFailure(exception));
            throw;
        }
    }

    internal CredentialNativeFailedManualPhaseEvidence? FailedManualPhaseForTest => failedManualPhase;

    private static CredentialNativeStagingEvidence StagingEvidence(HelperStagingReceipt staging) => new(
        staging.AttemptId,
        staging.ByteLength,
        staging.Sha256,
        staging.ResponseByteLength,
        staging.ResponseSha256,
        staging.StagedBeforeAdmission,
        staging.CoordinatorOnlyAdmission);

    internal static bool IsActionSourceBindingValid(string? action, string? source) =>
        source switch
        {
            "submitbutton" => action == "submit",
            "cancelbutton" => action == "cancel",
            "editkey" => action is "submit" or "cancel",
            _ => false,
        };

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
        failedManualPhase,
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
        CredentialNativeCallTraceEntry?[] parsed = System.Text.Json.JsonSerializer.Deserialize<CredentialNativeCallTraceEntry?[]>(
            canonicalTraceBytes,
            TraceJsonOptions)
            ?? throw new InvalidDataException("The canonical native call trace is absent.");
        if (parsed.Any(item => item is null))
        {
            throw new InvalidDataException("The canonical native call trace contains a null entry.");
        }
        CredentialNativeCallTraceEntry[] trace = parsed.Select(item => item!).ToArray();
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

    internal static void ValidateNativeHelperFailureEnvelope(
        NativeHelperFailureEnvelope evidence,
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        string? nativeManifestPath,
        int helperProcessId)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(assignment);
        if (nativeManifestPath is null || !File.Exists(nativeManifestPath)
            || helperProcessId <= 0
            || string.IsNullOrWhiteSpace(assignment.AssignmentId))
        {
            throw new InvalidDataException("A native helper failure requires exact manifest and assignment context.");
        }
        if (evidence.Stage is "handle-inheritance" or "launch-boundary" or "manifest-validation")
        {
            if (evidence.ContainmentDescendantStarted || evidence.EntryCleanupJson is not null
                || evidence.CanaryEvidenceJson is not null
                || evidence.ManualUiAttempted || !evidence.CallCountsKnown || evidence.Total != 0
                || evidence.CredWriteW != 0 || evidence.CredReadW != 0 || evidence.CredDeleteW != 0
                || evidence.CredFree != 0 || evidence.NativeCallTraceJson != "[]"
                || !evidence.NetworkFactsKnown || evidence.ListenerCount != 0
                || evidence.NetworkOperationCount != 0 || !evidence.ExternalEffectFactsKnown
                || evidence.DnsOperationCount != 0 || evidence.ProviderOperationCount != 0
                || evidence.BillableOperationCount != 0)
            {
                throw new InvalidDataException("A pre-engine helper failure contains impossible runtime evidence.");
            }
            return;
        }
        if (!evidence.CallCountsKnown || evidence.NativeCallTraceJson is null
            || evidence.CanaryEvidenceJson is null || !evidence.NetworkFactsKnown
            || evidence.ListenerCount != 0 || evidence.NetworkOperationCount != 0
            || !evidence.ExternalEffectFactsKnown || evidence.DnsOperationCount != 0
            || evidence.ProviderOperationCount != 0 || evidence.BillableOperationCount != 0)
        {
            throw new InvalidDataException("A post-store helper failure lacks independently checkable evidence.");
        }
        byte[] traceBytes = System.Text.Encoding.UTF8.GetBytes(evidence.NativeCallTraceJson);
        IReadOnlyList<CredentialNativeCallTraceEntry> trace = ParseAndValidateTrace(
            traceBytes, evidence.Total, requireTrace: true);
        if (trace.Count(item => item.Operation == "CredWriteW") != evidence.CredWriteW
            || trace.Count(item => item.Operation == "CredReadW") != evidence.CredReadW
            || trace.Count(item => item.Operation == "CredDeleteW") != evidence.CredDeleteW
            || trace.Count(item => item.Operation == "CredFree") != evidence.CredFree
            || trace.Any(item => item.Scenario != assignment.AssignmentId))
        {
            throw new InvalidDataException("The native helper failure trace disagrees with its assignment or counts.");
        }
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllBytes(nativeManifestPath));
        System.Text.Json.JsonElement maxima = document.RootElement.GetProperty("operation_limits")
            .GetProperty("native_call_maxima");
        if (evidence.CredWriteW > maxima.GetProperty("CredWriteW").GetInt32()
            || evidence.CredReadW > maxima.GetProperty("CredReadW").GetInt32()
            || evidence.CredDeleteW > maxima.GetProperty("CredDeleteW").GetInt32()
            || evidence.CredFree > maxima.GetProperty("CredFree").GetInt32()
            || evidence.Total > maxima.GetProperty("total").GetInt32())
        {
            throw new InvalidDataException("The native helper failure exceeds the accepted manifest operation limits.");
        }
        HashSet<string> allowed = [];
        foreach (System.Text.Json.JsonElement target in document.RootElement.GetProperty("disposable_namespace")
            .GetProperty("targets").EnumerateArray())
        {
            string profile = target.GetProperty("access_profile_id").GetString()!;
            string generation = target.GetProperty("generation_id").GetString()!;
            if (profile == assignment.AccessProfileId?.Value && generation == assignment.GenerationId?.Value
                || profile == bootstrap.Credential?.AccessProfileId?.Value
                    && generation == bootstrap.Credential?.GenerationId?.Value)
            {
                allowed.Add(target.GetProperty("target_fingerprint_sha256").GetString()!);
            }
        }
        if (allowed.Count == 0 || trace.Any(item => !allowed.Contains(item.TargetFingerprintSha256)))
        {
            throw new InvalidDataException("The native helper failure trace is outside the exact manifest target context.");
        }
        CredentialNativeQualificationPhaseV2 phase = CredentialNativeQualificationPhasesV2.Parse(
            assignment.AssignmentId, assignment.AssignmentKind);
        if (phase.PhaseId.StartsWith("preflight", StringComparison.Ordinal)
            && (trace.Any(item => item.Operation is "CredWriteW" or "CredDeleteW")
                || !evidence.NamespaceReuseBlocked && (trace.Count != 1
                    || trace[0].Operation != "CredReadW"
                    || trace[0].Result != "ERROR_NOT_FOUND"
                    || trace[0].AllocationId is not null
                    || trace[0].PairedAllocationId is not null)))
        {
            throw new InvalidDataException("A failed preflight contains a mutation or lacks terminal exact absence.");
        }
        ValidateFailureTraceForPhase(phase, trace, evidence.NamespaceReuseBlocked);
        CredentialNativeCanaryEvidence canaries = ParseCanaries(
            System.Text.Encoding.UTF8.GetBytes(evidence.CanaryEvidenceJson), requireEvidence: true)!;
        Dictionary<string, string> expectedSurfaces = new(StringComparer.Ordinal)
        {
            ["private protocol request"] = "private-pipe-bytes",
            ["private protocol partial response"] = "private-pipe-bytes",
            ["native call trace"] = "canonical-trace-bytes",
            ["process command line"] = "captured-text",
            ["process environment names"] = "captured-text",
        };
        if (canaries.ScannedSurfaces.Count != expectedSurfaces.Count
            || canaries.ScannedSurfaces.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count()
                != expectedSurfaces.Count
            || canaries.ScannedSurfaces.Any(item => !expectedSurfaces.TryGetValue(item.Name, out string? kind)
                || kind != item.Kind))
        {
            throw new InvalidDataException("The native helper failure canary inventory is not exact.");
        }
        if (phase.SecretMode == CredentialNativeQualificationSecretModeV2.Manual)
        {
            if (evidence.ManualUiAttempted)
            {
                CredentialNativeEntryCleanupEvidence entry = evidence.EntryCleanupJson is null
                    ? throw new InvalidDataException("A manual UI failure lacks cleanup evidence.")
                    : ParseEntryCleanup(System.Text.Encoding.UTF8.GetBytes(evidence.EntryCleanupJson))!;
                ValidateFailureManualEntryCleanup(
                    entry, assignment, helperProcessId, evidence.CredWriteW);
            }
            else if (evidence.EntryCleanupJson is not null || evidence.CredWriteW != 0)
            {
                throw new InvalidDataException("A pre-UI helper failure contains impossible entry or write evidence.");
            }
        }
        else if (evidence.ManualUiAttempted || evidence.EntryCleanupJson is not null)
        {
            throw new InvalidDataException("A non-manual helper failure contains UI evidence.");
        }
    }

    private static void ValidateFailureTraceForPhase(
        CredentialNativeQualificationPhaseV2 phase,
        IReadOnlyList<CredentialNativeCallTraceEntry> trace,
        bool namespaceReuseBlocked)
    {
        foreach (CredentialNativeCallTraceEntry item in trace)
        {
            bool canonicalResult = item.Operation switch
            {
                "CredWriteW" => item.Result == "success" || item.Result.StartsWith("win32-error:", StringComparison.Ordinal),
                "CredReadW" => item.Result is "success" or "ERROR_NOT_FOUND"
                    || item.Result.StartsWith("win32-error:", StringComparison.Ordinal),
                "CredDeleteW" => item.Result is "success" or "ERROR_NOT_FOUND"
                    || item.Result.StartsWith("win32-error:", StringComparison.Ordinal),
                "CredFree" => item.Result == "released",
                _ => false,
            };
            if (!canonicalResult)
            {
                throw new InvalidDataException("The native helper failure trace contains a noncanonical call result.");
            }
        }
        for (int index = 0; index < trace.Count; index++)
        {
            CredentialNativeCallTraceEntry item = trace[index];
            if (item.Operation == "CredReadW" && item.Result == "success"
                && (index + 1 >= trace.Count || trace[index + 1].Operation != "CredFree"
                    || trace[index + 1].PairedAllocationId != item.AllocationId))
            {
                throw new InvalidDataException("A successful failure-trace read is not released immediately.");
            }
            if (item.Result.StartsWith("win32-error:", StringComparison.Ordinal) && index != trace.Count - 1)
            {
                throw new InvalidDataException("A native Win32 call failure is not terminal in its phase trace.");
            }
        }

        string[] operations = trace.Select(item => item.Operation).ToArray();
        static bool Prefix(string[] actual, params string[][] admitted) => admitted.Any(expected =>
            actual.Length <= expected.Length && actual.SequenceEqual(expected.Take(actual.Length), StringComparer.Ordinal));
        bool admitted = phase.PhaseId.StartsWith("preflight", StringComparison.Ordinal)
            ? namespaceReuseBlocked
                ? Prefix(operations, ["CredReadW", "CredFree"])
                : Prefix(operations, ["CredReadW"])
            : phase.UnavailableBeforeNativeCall || phase.ManualEntryMustCancel
                || phase.SecretMode == CredentialNativeQualificationSecretModeV2.GeneratedOversize
                ? operations.Length == 0
            : phase.AssignmentKind == HelperAssignmentKindV2.Enroll
                ? Prefix(operations, ["CredWriteW", "CredReadW", "CredFree"])
            : phase.AssignmentKind == HelperAssignmentKindV2.Verify
                || phase.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch
                ? Prefix(operations, ["CredReadW", "CredFree"])
            : phase.AssignmentKind == HelperAssignmentKindV2.Replace
                ? Prefix(operations,
                    ["CredWriteW", "CredReadW", "CredFree", "CredReadW", "CredFree", "CredDeleteW", "CredReadW"])
            : phase.AssignmentKind == HelperAssignmentKindV2.Delete
                ? Prefix(operations,
                    ["CredDeleteW", "CredReadW", "CredReadW"],
                    ["CredDeleteW", "CredReadW", "CredFree", "CredReadW"])
            : phase.AssignmentKind == HelperAssignmentKindV2.Recover
                ? Prefix(operations,
                    ["CredReadW", "CredFree", "CredReadW", "CredFree", "CredReadW", "CredFree", "CredDeleteW", "CredReadW"],
                    ["CredReadW", "CredWriteW", "CredReadW", "CredFree", "CredReadW", "CredFree", "CredReadW", "CredFree", "CredDeleteW", "CredReadW"])
            : false;
        if (!admitted)
        {
            throw new InvalidDataException("The native helper failure trace is not an admitted prefix for its exact phase.");
        }
    }

    private static void ValidateFailureManualEntryCleanup(
        CredentialNativeEntryCleanupEvidence entry,
        HelperAssignmentV2 assignment,
        int helperProcessId,
        int credWriteCount)
    {
        CredentialNativeEntryReadinessEvidence readiness = entry.Readiness
            ?? throw new InvalidDataException("The native helper manual failure lacks a retained UI readiness attempt.");
        string expectedMode = assignment.AssignmentId.Contains(
            "interactive-entry-cancel", StringComparison.Ordinal) ? "cancel" : "submit";
        string expectedInstruction = expectedMode == "submit"
            ? "Enter a disposable test value, then click Submit. Never use a real credential."
            : "Leave the field blank, then click Cancel.";
        string expectedInstructionFingerprint = Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(expectedInstruction)));
        if (!entry.Terminal || !entry.WindowDestroyed || !entry.BuffersCleared
            || !entry.ThreadJoined || !entry.ClipboardMessagesBlocked
            || entry.PreReadinessTerminalMessages < 0 || entry.PreReadinessIgnoredMessages < 0
            || readiness.OwnerProcessId != helperProcessId || readiness.OwnerThreadId == 0
            || readiness.OwnerSessionId != System.Diagnostics.Process.GetCurrentProcess().SessionId
            || readiness.DesktopNameSha256.Length != 64
            || !readiness.DesktopNameSha256.All(char.IsAsciiHexDigit)
            || readiness.InstructionMode != expectedMode
            || readiness.InstructionFingerprintSha256 != expectedInstructionFingerprint
            || readiness.ReadinessDeadlineMilliseconds is < 1 or > 10_000
            || readiness.ReadinessElapsedMilliseconds < 0
            || readiness.ReadinessElapsedMilliseconds > readiness.ReadinessDeadlineMilliseconds)
        {
            throw new InvalidDataException("The native helper manual failure cleanup identity or deadline is invalid.");
        }

        bool readinessAdmitted = IsAdmissibleReadiness(readiness, requireEditFocus: true);
        if (!readinessAdmitted)
        {
            if (entry.InitialBlank || credWriteCount != 0 || entry.Action is not null
                || entry.ActionSource is not null || entry.ActionReadiness is not null)
            {
                throw new InvalidDataException("A failed UI readiness attempt contains an admitted action or native write.");
            }
            return;
        }
        if (!entry.InitialBlank)
        {
            throw new InvalidDataException("An admitted native helper UI was not initially blank.");
        }
        if (entry.Action is null)
        {
            if (entry.ActionSource is not null || entry.ActionReadiness is not null || credWriteCount != 0)
            {
                throw new InvalidDataException("A manual timeout contains partial action or write evidence.");
            }
            return;
        }
        CredentialNativeEntryReadinessEvidence action = entry.ActionReadiness
            ?? throw new InvalidDataException("A retained manual action lacks action-time readiness.");
        if (!IsAdmissibleReadiness(action, requireEditFocus: entry.ActionSource == "editkey")
            || action.OwnerProcessId != readiness.OwnerProcessId
            || action.OwnerThreadId != readiness.OwnerThreadId
            || action.OwnerSessionId != readiness.OwnerSessionId
            || action.DesktopNameSha256 != readiness.DesktopNameSha256
            || action.InstructionMode != readiness.InstructionMode
            || action.InstructionFingerprintSha256 != readiness.InstructionFingerprintSha256
            || action.ReadinessDeadlineMilliseconds is < 1 or > 10_000
            || action.ReadinessElapsedMilliseconds < 0
            || action.ReadinessElapsedMilliseconds > action.ReadinessDeadlineMilliseconds
            || !IsActionSourceBindingValid(entry.Action, entry.ActionSource)
            || entry.ActionSource == "submitbutton" && !action.SubmitFocused
            || entry.ActionSource == "cancelbutton" && !action.CancelFocused
            || entry.ActionSource is not ("editkey" or "submitbutton" or "cancelbutton"))
        {
            throw new InvalidDataException("The retained manual action is not bound to the owned actionable UI.");
        }
    }

    private static bool IsAdmissibleReadiness(
        CredentialNativeEntryReadinessEvidence value,
        bool requireEditFocus) =>
        value.InteractiveInputDesktop && value.DesktopObjectMatches
        && value.OwnerProcessMatches && value.OwnerThreadMatches
        && value.TopLevelWindow && value.WindowVisible && value.WindowEnabled
        && value.WindowNotCloaked && value.WindowIntersectsActiveMonitor
        && value.InstructionOwned && value.InstructionVisible
        && value.EditOwned && value.EditVisible && value.EditEnabled && value.EditMasked
        && value.SubmitOwned && value.SubmitVisible && value.SubmitEnabled
        && value.CancelOwned && value.CancelVisible && value.CancelEnabled
        && value.Foreground && (!requireEditFocus || value.EditFocused);

    internal static CredentialNativeCanaryEvidence? ParseCanaries(byte[]? bytes, bool requireEvidence)
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

    internal static CredentialNativeEntryCleanupEvidence? ParseEntryCleanup(byte[]? bytes) => bytes is null
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
