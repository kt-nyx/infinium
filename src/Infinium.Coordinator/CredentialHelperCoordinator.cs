using Google.Protobuf;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Infinium.Persistence;

namespace Infinium.Coordinator;

public sealed record CoordinatedHelperReceipt(
    HelperProcessReceipt Process,
    HelperStagingReceipt Staging);

internal enum ProviderDispatchFaultPoint
{
    None,
    AfterDurableMayHaveStartedBeforeHelper,
}

internal enum CredentialLifecycleFaultPoint
{
    None,
    AfterDeletePendingBeforeHelper,
    AfterHelperBeforeProjection,
}

internal sealed class CredentialLifecycleInterruptionException(
    CoordinatedHelperReceipt helper,
    CredentialProfileProjection durableProjection)
    : IOException("Injected crash after helper completion and before lifecycle projection publication.")
{
    internal CoordinatedHelperReceipt Helper { get; } = helper;
    internal CredentialProfileProjection DurableProjection { get; } = durableProjection;
}

internal sealed record AuthoritativeDispatchQualificationReceipt(
    CoordinatedHelperReceipt Helper,
    ProviderSimulationPersistenceReceipt Persisted,
    ProviderBudgetSettlementReceipt Settlement,
    ProviderDispatchGateReceipt FinalGate);

public sealed class CredentialHelperCoordinator
{
    internal Task<HelperProcessReceipt> ExecuteNativeQualificationPreflightAsync(
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) => launcher.ExecuteAsync(
            bootstrap,
            assignment,
            null,
            launcher.OperationTimeout,
            now,
            cancellationToken);
    private readonly AuthoritativeStore store;
    private readonly OneShotCredentialHelperLauncher launcher;

    public CredentialHelperCoordinator(AuthoritativeStore store, OneShotCredentialHelperLauncher launcher)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
    }

    public async Task<CoordinatedHelperReceipt> ExecuteStageAndAdmitAsync(
        string attemptId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        HelperPrivateFrameV2? finalRevalidation,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        HelperProcessReceipt process = await launcher.ExecuteAsync(
            bootstrap, assignment, finalRevalidation, launcher.OperationTimeout, now, cancellationToken)
            .ConfigureAwait(false);
        ulong sequence = finalRevalidation is null ? 3UL : 4UL;
        HelperPrivateFrameV2 terminal = new()
        {
            Sequence = sequence,
            ProtocolFingerprintSha256 = ProtocolFingerprint(),
            Receipt = process.Receipt.Clone(),
        };
        HelperAssignmentV2 work = assignment.Assignment;
        ProviderRequestV2? request = work.ProviderRequest;
        DispatchRevalidationV2? revalidation = finalRevalidation?.DispatchRevalidation;
        _ = HelperProtocolV2Codec.Decode(
            terminal.ToByteArray(), now,
            expectedAssignmentId: work.AssignmentId,
            expectedCommandId: work.CommandId,
            expectedOperationId: work.ProviderDispatch?.OperationId?.Value,
            expectedAttemptId: work.ProviderDispatch?.AttemptId?.Value,
            expectedProfileId: work.Credential?.AccessProfileId?.Value,
            expectedGenerationId: work.Credential?.GenerationId?.Value,
            expectedRequestId: request?.RequestId,
            expectedDispatchId: request?.DispatchId?.Value,
            expectedRequestFingerprintSha256: request?.RequestFingerprintSha256.ToByteArray(),
            expectedInputBoundPolicyId: request?.InputBoundProof?.PolicyId,
            expectedInputBoundPolicyVersion: request?.InputBoundProof?.PolicyVersion,
            expectedCoordinatorFencingEpoch: revalidation?.CoordinatorFencingEpoch,
            expectedCapabilitySnapshotId: request?.CapabilitySnapshotId?.Value,
            expectedPriceSnapshotId: request?.PriceSnapshotId?.Value,
            expectedSettings: work.Settings,
            expectedOutputSchema: work.OutputSchema,
            expectedEffectiveConfigurationId: work.EffectiveConfigurationId,
            expectedNonSecretReceipt: process.Receipt.NonSecretReceipt,
            expectedRevocationEpoch: work.RevocationEpoch,
            expectedAccountIdentityId: work.AccountIdentityId?.Value,
            expectedBillingScopeIdentityId: work.BillingScopeIdentityId?.Value,
            expectedReservationGroupId: request?.ReservationGroupId?.Value,
            expectedOperationKind: work.OperationKind,
            expectedLimits: work.Limits,
            expectedDispatchDeadline: request?.DispatchDeadline,
            expectedPayloadCase: HelperPrivateFrameV2.PayloadOneofCase.Receipt,
            expectedSequence: sequence,
            expectedAssignmentKind: work.AssignmentKind);
        byte[] canonical = HelperPrivateProtocolV2.Encode(terminal);
        // Persistence sees only already validated, bounded, canonical non-secret bytes.
        byte[] stagedRawResponse = process.StagedResponseBytes;
        if (OpenAiStagedResponseEnvelope.TryRead(process.StagedResponseBytes, out byte[] envelopeRaw, out _))
        {
            stagedRawResponse = envelopeRaw;
        }
        bool exactOversizedReceipt = process.Receipt.Outcome == HelperOutcomeV2.Oversized
            && process.Receipt.RawResponse is null && process.Receipt.HasOverflowObservedExcessBytes
            && process.Receipt.OverflowObservedExcessBytes == 1 && stagedRawResponse.Length == 0;
        if (process.StagedResponseBytes.Length > 0
            && !exactOversizedReceipt
            && (process.Receipt.RawResponse is null
                || process.Receipt.RawResponse.SizeBytes != (ulong)stagedRawResponse.Length
                || !process.Receipt.RawResponse.Value.Span.SequenceEqual(
                    System.Security.Cryptography.SHA256.HashData(stagedRawResponse))))
        {
            throw new InvalidDataException("The helper staged response does not match its validated manifest digest.");
        }
        HelperStagingReceipt staging = store.StageAndAdmitHelperReceipt(
            attemptId, canonical, now, process.StagedResponseBytes);
        return new(process, staging);
    }

    public async Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)>
        ExecuteCredentialTransitionAsync(
            string attemptId,
            HelperPrivateFrameV2 bootstrap,
            HelperPrivateFrameV2 assignment,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) => await ExecuteCredentialTransitionCoreAsync(
                attemptId, bootstrap, assignment, now, CredentialLifecycleFaultPoint.None, cancellationToken)
                .ConfigureAwait(false);

    internal async Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)>
        ExecuteCredentialTransitionWithFaultAsync(
            string attemptId,
            HelperPrivateFrameV2 bootstrap,
            HelperPrivateFrameV2 assignment,
            DateTimeOffset now,
            CredentialLifecycleFaultPoint faultPoint,
            CancellationToken cancellationToken = default) => await ExecuteCredentialTransitionCoreAsync(
                attemptId, bootstrap, assignment, now, faultPoint, cancellationToken).ConfigureAwait(false);

    private async Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)>
        ExecuteCredentialTransitionCoreAsync(
            string attemptId,
            HelperPrivateFrameV2 bootstrap,
            HelperPrivateFrameV2 assignment,
            DateTimeOffset now,
            CredentialLifecycleFaultPoint faultPoint,
            CancellationToken cancellationToken)
    {
        if (assignment.Assignment.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch)
        {
            throw new InvalidOperationException("Credential lifecycle execution cannot reinterpret a dispatch assignment.");
        }
        HelperAssignmentV2 work = assignment.Assignment;
        string profileId = work.AccessProfileId?.Value
            ?? throw new InvalidDataException("Credential profile identity is required.");
        string generationId = work.GenerationId?.Value
            ?? throw new InvalidDataException("Credential generation identity is required.");
        CredentialProfileProjection current = store.GetCredentialProfile(profileId);
        (string? accountIdentityId, string? billingScopeIdentityId) = store.ReadCredentialIdentityBinding(profileId);
        string bootstrapGenerationId = bootstrap.Bootstrap.Credential?.GenerationId?.Value
            ?? throw new InvalidDataException("Credential bootstrap generation identity is required.");
        bool restoredGeneration = current.LifecycleState == "recovery-required"
            && current.IntentId?.StartsWith("restore-recovery-", StringComparison.Ordinal) == true;
        bool changesGeneration = generationId != current.GenerationId;
        bool replacementCleanupRecovery = work.AssignmentKind == HelperAssignmentKindV2.Recover
            && current.LifecycleState == "delete-pending"
            && changesGeneration
            && store.IsCredentialReplacementCleanupRecovery(profileId, current.GenerationId, generationId);
        bool validFreshGeneration = changesGeneration
            && bootstrap.Bootstrap.Credential?.AccessProfileId?.Value == profileId
            && bootstrapGenerationId == current.GenerationId
            && work.GenerationOrdinal == checked((ulong)(current.GenerationOrdinal + 1))
            && (work.AssignmentKind == HelperAssignmentKindV2.Replace
                    && current.LifecycleState is "active-unverified" or "active-verified"
                || work.AssignmentKind == HelperAssignmentKindV2.Recover
                    && current.LifecycleState is "secure-store-unavailable" or "recovery-required"
                || replacementCleanupRecovery);
        bool validCurrentGeneration = !changesGeneration
            && bootstrapGenerationId == generationId
            && work.GenerationOrdinal == checked((ulong)current.GenerationOrdinal);
        if ((!validFreshGeneration && !validCurrentGeneration)
            || work.Credential?.AccessProfileId?.Value != profileId
            || work.Credential?.GenerationId?.Value != generationId
            || bootstrap.Bootstrap.Credential?.AccessProfileId?.Value != profileId
            || work.AssignmentKind == HelperAssignmentKindV2.Replace && !changesGeneration
            || work.AssignmentKind == HelperAssignmentKindV2.Recover
                && restoredGeneration && !changesGeneration)
        {
            throw new InvalidDataException("The helper credential subject is not the authoritative lifecycle subject.");
        }
        if (work.AssignmentKind == HelperAssignmentKindV2.Replace)
        {
            current = store.ApplyCredentialTransition(new(
                attemptId + "-replacement-ineligible",
                profileId,
                current.GenerationId,
                "replace",
                current.LifecycleState,
                "replacing",
                "replacing",
                current.CapabilitySnapshotId ?? M1ProviderCatalog.Capability.Identity.Value,
                current.AccountIdentityId ?? accountIdentityId,
                current.BillingScopeIdentityId ?? billingScopeIdentityId,
                now,
                now.AddTicks(1)));
        }
        bool absenceOnlyCleanup = work.AssignmentKind == HelperAssignmentKindV2.Delete
            && current.LifecycleState is "pending-enrollment" or "secure-store-unavailable" or "recovery-required";
        if (work.AssignmentKind == HelperAssignmentKindV2.Delete
            && !absenceOnlyCleanup
            && current.LifecycleState != "delete-pending")
        {
            current = store.ApplyCredentialTransition(new(
                attemptId + "-delete-pending",
                profileId,
                generationId,
                "delete",
                current.LifecycleState,
                "delete-pending",
                "delete-pending",
                current.CapabilitySnapshotId ?? M1ProviderCatalog.Capability.Identity.Value,
                current.AccountIdentityId ?? accountIdentityId,
                current.BillingScopeIdentityId ?? billingScopeIdentityId,
                now,
                now.AddTicks(1),
                IncrementRevocationEpoch: true));
            if (faultPoint == CredentialLifecycleFaultPoint.AfterDeletePendingBeforeHelper)
            {
                throw new IOException("Injected crash after durable delete-pending revocation and before helper deletion.");
            }
        }
        CoordinatedHelperReceipt helper;
        try
        {
            helper = await ExecuteStageAndAdmitAsync(
                attemptId, bootstrap, assignment, null, now.AddTicks(2), cancellationToken).ConfigureAwait(false);
        }
        catch when (work.AssignmentKind == HelperAssignmentKindV2.Replace)
        {
            _ = PersistReplacementCleanupFailure(
                attemptId, current, now, accountIdentityId, billingScopeIdentityId, unavailable: false);
            throw;
        }
        catch when (work.AssignmentKind == HelperAssignmentKindV2.Delete && !absenceOnlyCleanup)
        {
            _ = PersistDeleteFailure(attemptId, current, now, unavailable: false);
            throw;
        }
        HelperOutcomeV2 outcome = helper.Process.Receipt.Outcome;
        if (faultPoint == CredentialLifecycleFaultPoint.AfterHelperBeforeProjection)
        {
            throw new CredentialLifecycleInterruptionException(helper, current);
        }
        if (absenceOnlyCleanup)
        {
            // A cancelled, rejected, or unavailable enrollment has no active
            // generation to revoke. The helper still performs exact-target
            // deletion/absence verification, while the durable lifecycle
            // remains the truthful pre-activation state.
            return (helper, current);
        }
        if (work.AssignmentKind == HelperAssignmentKindV2.Replace && outcome != HelperOutcomeV2.Completed)
        {
            return (helper, PersistReplacementCleanupFailure(
                attemptId, current, now, accountIdentityId, billingScopeIdentityId,
                unavailable: outcome == HelperOutcomeV2.Unavailable));
        }
        if (replacementCleanupRecovery && outcome != HelperOutcomeV2.Completed)
        {
            return (helper, store.ApplyCredentialTransition(new(
                attemptId + "-predecessor-cleanup-retry-failed",
                profileId,
                current.GenerationId,
                "delete",
                "delete-pending",
                "delete-pending",
                "delete-pending",
                current.CapabilitySnapshotId,
                current.AccountIdentityId,
                current.BillingScopeIdentityId,
                now.AddTicks(3),
                now.AddTicks(4),
                SecureStoreUnavailable: outcome == HelperOutcomeV2.Unavailable,
                Failed: outcome != HelperOutcomeV2.Unavailable)));
        }
        if (work.AssignmentKind == HelperAssignmentKindV2.Enroll
            && outcome is HelperOutcomeV2.Cancelled or HelperOutcomeV2.Oversized or HelperOutcomeV2.FailedKnown)
        {
            // The helper receipt is durably staged, but an enrollment that
            // never produced an admissible secure-store value leaves the
            // existing pending projection unchanged.
            return (helper, current);
        }
        (string intentKind, string completedState, bool incrementRevocation) = CredentialTransition(
            work.AssignmentKind, current.LifecycleState);
        bool deleted = completedState == "deleted";
        CredentialTransitionRequest transition = new(
            attemptId + "-credential-transition",
            profileId,
            generationId,
            intentKind,
            current.LifecycleState,
            completedState,
            completedState,
            deleted ? null : current.CapabilitySnapshotId ?? M1ProviderCatalog.Capability.Identity.Value,
            deleted ? null : current.AccountIdentityId ?? accountIdentityId,
            deleted ? null : current.BillingScopeIdentityId ?? billingScopeIdentityId,
            now.AddTicks(3),
            now.AddTicks(4),
            IncrementRevocationEpoch: incrementRevocation);
        if (outcome != HelperOutcomeV2.Completed)
        {
            transition = work.AssignmentKind == HelperAssignmentKindV2.Delete
                ? transition with
                {
                    TerminalState = "delete-pending",
                    ToState = "delete-pending",
                    CapabilitySnapshotId = current.CapabilitySnapshotId,
                    AccountIdentityId = current.AccountIdentityId,
                    BillingScopeIdentityId = current.BillingScopeIdentityId,
                    SecureStoreUnavailable = outcome == HelperOutcomeV2.Unavailable,
                    Failed = outcome != HelperOutcomeV2.Unavailable,
                }
                : transition with
                {
                    TerminalState = outcome == HelperOutcomeV2.Unavailable
                        ? "secure-store-unavailable"
                        : current.LifecycleState,
                    ToState = outcome == HelperOutcomeV2.Unavailable
                        ? "secure-store-unavailable"
                        : current.LifecycleState,
                    SecureStoreUnavailable = outcome == HelperOutcomeV2.Unavailable,
                    Failed = outcome is not (HelperOutcomeV2.Unavailable or HelperOutcomeV2.Cancelled),
                    Cancelled = outcome == HelperOutcomeV2.Cancelled,
                };
        }
        return (helper, store.ApplyCredentialTransition(transition));
    }

    private CredentialProfileProjection PersistDeleteFailure(
        string attemptId,
        CredentialProfileProjection pending,
        DateTimeOffset now,
        bool unavailable) => store.ApplyCredentialTransition(new(
            attemptId + "-delete-failed",
            pending.ProfileId,
            pending.GenerationId,
            "delete",
            "delete-pending",
            "delete-pending",
            "delete-pending",
            pending.CapabilitySnapshotId,
            pending.AccountIdentityId,
            pending.BillingScopeIdentityId,
            now.AddTicks(3),
            now.AddTicks(4),
            SecureStoreUnavailable: unavailable,
            Failed: !unavailable));

    private CredentialProfileProjection PersistReplacementCleanupFailure(
        string attemptId,
        CredentialProfileProjection predecessor,
        DateTimeOffset now,
        string? accountIdentityId,
        string? billingScopeIdentityId,
        bool unavailable)
    {
        CredentialProfileProjection pendingCleanup = store.ApplyCredentialTransition(new(
            attemptId + "-predecessor-cleanup-pending",
            predecessor.ProfileId,
            predecessor.GenerationId,
            "delete",
            predecessor.LifecycleState,
            "delete-pending",
            "delete-pending",
            predecessor.CapabilitySnapshotId ?? M1ProviderCatalog.Capability.Identity.Value,
            predecessor.AccountIdentityId ?? accountIdentityId,
            predecessor.BillingScopeIdentityId ?? billingScopeIdentityId,
            now.AddTicks(3),
            now.AddTicks(4)));
        return store.ApplyCredentialTransition(new(
            attemptId + "-predecessor-cleanup-failed",
            predecessor.ProfileId,
            pendingCleanup.GenerationId,
            "delete",
            "delete-pending",
            "delete-pending",
            "delete-pending",
            pendingCleanup.CapabilitySnapshotId,
            pendingCleanup.AccountIdentityId,
            pendingCleanup.BillingScopeIdentityId,
            now.AddTicks(5),
            now.AddTicks(6),
            SecureStoreUnavailable: unavailable,
            Failed: !unavailable));
    }

    public async Task<(CoordinatedHelperReceipt Helper, ProviderSimulationPersistenceReceipt Persisted,
        ProviderBudgetSettlementReceipt Settlement)> ExecuteAuthoritativeDispatchAsync(
        string attemptId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        AuthoritativeDispatchQualificationReceipt result = await ExecuteAuthoritativeDispatchCoreAsync(
            attemptId, bootstrap, assignment, now, ProviderDispatchFaultPoint.None, cancellationToken)
            .ConfigureAwait(false);
        return (result.Helper, result.Persisted, result.Settlement);
    }

    internal Task<AuthoritativeDispatchQualificationReceipt> ExecuteAuthoritativeDispatchForQualificationAsync(
        string attemptId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) => ExecuteAuthoritativeDispatchCoreAsync(
            attemptId, bootstrap, assignment, now, ProviderDispatchFaultPoint.None, cancellationToken);

    internal async Task<(CoordinatedHelperReceipt Helper, ProviderSimulationPersistenceReceipt Persisted,
        ProviderBudgetSettlementReceipt Settlement)> ExecuteAuthoritativeDispatchWithFaultAsync(
        string attemptId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset now,
        ProviderDispatchFaultPoint faultPoint,
        CancellationToken cancellationToken = default)
    {
        AuthoritativeDispatchQualificationReceipt result = await ExecuteAuthoritativeDispatchCoreAsync(
            attemptId, bootstrap, assignment, now, faultPoint, cancellationToken).ConfigureAwait(false);
        return (result.Helper, result.Persisted, result.Settlement);
    }

    private async Task<AuthoritativeDispatchQualificationReceipt> ExecuteAuthoritativeDispatchCoreAsync(
        string attemptId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset now,
        ProviderDispatchFaultPoint faultPoint,
        CancellationToken cancellationToken)
    {
        HelperAssignmentV2 work = assignment.Assignment;
        ProviderRequestV2 request = work.ProviderRequest
            ?? throw new InvalidDataException("A provider dispatch assignment requires one request.");
        string operationId = work.ProviderDispatch?.OperationId?.Value
            ?? throw new InvalidDataException("A provider dispatch operation identity is required.");
        string providerAttemptId = work.ProviderDispatch?.AttemptId?.Value
            ?? throw new InvalidDataException("A provider dispatch attempt identity is required.");
        string reservationId = request.ReservationGroupId?.Value
            ?? throw new InvalidDataException("A provider dispatch reservation identity is required.");
        string fenceId = work.AssignmentId + ":final-gate";
        ProviderDispatchAuthoritySnapshot authority = store.ReadCurrentProviderDispatchRequest(
            fenceId, operationId, reservationId, providerAttemptId, request.RequestId, now);
        ProviderDispatchGateRequest gate = authority.Gate;
        if (gate.ProfileId != work.AccessProfileId?.Value || gate.GenerationId != work.GenerationId?.Value
            || gate.RevocationEpoch != (long)work.RevocationEpoch
            || gate.CoordinatorFencingEpoch != (long)bootstrap.Bootstrap.CoordinatorFencingEpoch
            || authority.GenerationOrdinal != (long)work.GenerationOrdinal
            || authority.AccountIdentityId != work.AccountIdentityId?.Value
            || authority.BillingScopeIdentityId != work.BillingScopeIdentityId?.Value
            || authority.EffectiveConfigurationId != work.EffectiveConfigurationId
            || authority.CapabilitySnapshotId != request.CapabilitySnapshotId?.Value
            || authority.PriceSnapshotId != request.PriceSnapshotId?.Value
            || authority.OperationKind != OperationKind(work.OperationKind)
            || authority.RequestFingerprintSha256 != Hex(request.RequestFingerprintSha256)
            || authority.CanonicalRequestFingerprintSha256 != Hex(request.CanonicalRequest?.Value)
            || authority.CanonicalRequestBytes != (long)(request.CanonicalRequest?.SizeBytes ?? 0)
            || authority.SettingsFingerprintSha256 != Hex(work.Settings?.Value)
            || authority.OutputSchemaFingerprintSha256 != Hex(work.OutputSchema?.Value)
            || authority.InputBoundPolicyId != request.InputBoundProof?.PolicyId
            || authority.InputBoundPolicyVersion != request.InputBoundProof?.PolicyVersion
            || authority.InputBoundProofStatus != InputBoundStatus(request.InputBoundProof?.Status ?? InputBoundProofStatusV2.Unspecified)
            || authority.MaximumRequestBytes != (long)(work.Limits?.MaximumRequestBytes ?? 0)
            || authority.MaximumInputTokens != (long)(work.Limits?.MaximumInputTokens ?? 0)
            || authority.MaximumOutputTokens != (long)(work.Limits?.MaximumOutputTokens ?? 0)
            || authority.MaximumRawResponseBytes != (long)(work.Limits?.MaximumResponseBytes ?? 0)
            || authority.MaximumDispatchCount != (long)(work.Limits?.MaximumDispatchCount ?? 0)
            || authority.MaximumCalculatedNanoUsd != (long)(work.Limits?.MaximumCalculatedNanoUsd ?? 0)
            || authority.DeadlineMilliseconds != (long)(work.Limits?.MaximumDuration?.Value ?? 0)
            || authority.DispatchDeadline != ToDateTimeOffset(request.DispatchDeadline)
            || authority.ConfirmedAt != ToDateTimeOffset(request.ConfirmedAt))
        {
            throw new InvalidOperationException("The caller assignment is stale or fabricated relative to authoritative dispatch state.");
        }
        ProviderDispatchGateReceipt finalGate = new ProviderAccountingCoordinator(store).FinalGate(gate);
        HelperPrivateFrameV2 final = CreateAuthoritativeRevalidation(assignment, authority, finalGate);
        store.RecordProviderTransportStart(
            operationId, providerAttemptId, request.RequestId, fenceId, ambiguous: true, now);
        try
        {
            if (faultPoint == ProviderDispatchFaultPoint.AfterDurableMayHaveStartedBeforeHelper)
            {
                throw new IOException("Injected crash after the durable may-have-started boundary.");
            }
        }
        catch
        {
            _ = store.SettleProviderBudget(new(
                work.AssignmentId + ":ambiguous-settlement",
                reservationId,
                ProviderBudgetEventKind.RetainedAmbiguous,
                null,
                null,
                now.AddTicks(1)));
            throw;
        }
        CoordinatedHelperReceipt helper;
        try
        {
            helper = await ExecuteStageAndAdmitAsync(
                attemptId, bootstrap, assignment, final, now.AddTicks(1), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _ = store.SettleProviderBudget(new(
                work.AssignmentId + ":ambiguous-settlement",
                reservationId,
                ProviderBudgetEventKind.RetainedAmbiguous,
                null,
                null,
                now.AddTicks(2)));
            throw;
        }
        if (helper.Process.Receipt.Outcome is not (HelperOutcomeV2.Completed
                or HelperOutcomeV2.FailedKnown or HelperOutcomeV2.Malformed or HelperOutcomeV2.Oversized)
            || helper.Process.StagedResponseBytes.Length == 0
            || !helper.Process.Receipt.TransportMayHaveStarted)
        {
            _ = store.SettleProviderBudget(new(
                work.AssignmentId + ":ambiguous-settlement",
                reservationId,
                ProviderBudgetEventKind.RetainedAmbiguous,
                null,
                null,
                now.AddTicks(2)));
            throw new InvalidOperationException("The authorized helper dispatch did not produce one admissible staged response.");
        }
        OpenAiResponsesResult adapterResult;
        byte[] rawResponse;
        byte[]? headerReceipt = null;
        if (OpenAiStagedResponseEnvelope.TryRead(
            helper.Process.StagedResponseBytes, out byte[] decodedRaw, out byte[] decodedHeaders))
        {
            rawResponse = decodedRaw;
            headerReceipt = decodedHeaders;
            adapterResult = OpenAiStagedResponseEnvelope.Replay(rawResponse, headerReceipt, request.RequestId);
        }
        else
        {
            rawResponse = helper.Process.StagedResponseBytes;
            adapterResult = new(
                ProviderResponseState.Completed, false, false, 200, rawResponse, null, request.RequestId, null,
                "gpt-5.6-sol", "default", null, null, null, CreateUsage(helper.Process.Receipt), [], true,
                "wp3-wp4-deterministic-fake", false, 0);
        }
        ProviderSimulationPersistenceReceipt persisted = store.PersistProviderSimulation(new(
            work.AssignmentId + ":response",
            work.AssignmentId + ":usage",
            work.AssignmentId + ":receipt",
            work.AssignmentId + ":finalization",
            gate.AuthorizationId,
            operationId,
            reservationId,
            providerAttemptId,
            request.RequestId,
            fenceId,
            adapterResult.State,
            adapterResult.HttpStatus ?? 0,
            adapterResult.ReturnedModel,
            adapterResult.ReturnedServiceTier,
            adapterResult.ErrorCode,
            adapterResult.RefusalCode,
            adapterResult.IncompleteReason,
            NormalizeUsageRateAvailability(adapterResult.Usage, CreateRateFacts(adapterResult.RateHeaders, now.AddTicks(2))),
            CreateRateFacts(adapterResult.RateHeaders, now.AddTicks(2)),
            rawResponse.Length == 0 ? null : rawResponse,
            now.AddTicks(2),
            headerReceipt,
            adapterResult.ProviderResponseId,
            adapterResult.ProviderRequestId,
            adapterResult.Admitted));
        ProviderBudgetSettlementReceipt settlement = store.SettleProviderBudget(new(
            work.AssignmentId + ":settlement",
            reservationId,
            persisted.SettlementKind,
            persisted.SettlementKind == ProviderBudgetEventKind.RetainedUnavailable ? null : persisted.UsageEntryId,
            persisted.SettlementKind == ProviderBudgetEventKind.RetainedUnavailable ? null : persisted.Actual,
            now.AddTicks(3)));
        return new(helper, persisted, settlement, finalGate);
    }

    private static HelperPrivateFrameV2 CreateAuthoritativeRevalidation(
        HelperPrivateFrameV2 assignmentFrame,
        ProviderDispatchAuthoritySnapshot authority,
        ProviderDispatchGateReceipt receipt)
    {
        HelperAssignmentV2 work = assignmentFrame.Assignment;
        ProviderRequestV2 request = work.ProviderRequest;
        ProviderDispatchGateRequest gate = authority.Gate;
        return new()
        {
            Sequence = 3,
            ProtocolFingerprintSha256 = ProtocolFingerprint(),
            DispatchRevalidation = new()
            {
                DispatchId = request.DispatchId.Clone(),
                AttemptId = work.ProviderDispatch.AttemptId.Clone(),
                CoordinatorFencingEpoch = checked((ulong)gate.CoordinatorFencingEpoch),
                AccessProfileId = work.AccessProfileId.Clone(),
                GenerationId = work.GenerationId.Clone(),
                RevocationEpoch = checked((ulong)gate.RevocationEpoch),
                ReservationGroupId = request.ReservationGroupId.Clone(),
                CanonicalRequest = request.CanonicalRequest.Clone(),
                AuthorizedOnce = receipt.Authorized,
                Disposition = receipt.Authorized ? DispatchDispositionV2.Authorized : DispatchDispositionV2.Rejected,
                AccountIdentityId = new() { Value = authority.AccountIdentityId },
                BillingScopeIdentityId = new() { Value = authority.BillingScopeIdentityId },
                EffectiveConfigurationId = authority.EffectiveConfigurationId,
                CapabilitySnapshotId = new() { Value = authority.CapabilitySnapshotId },
                PriceSnapshotId = new() { Value = authority.PriceSnapshotId },
                Settings = new() { Algorithm = DigestAlgorithm.Sha256, Value = ByteString.CopyFrom(Convert.FromHexString(authority.SettingsFingerprintSha256)), SizeBytes = work.Settings.SizeBytes },
                OutputSchema = new() { Algorithm = DigestAlgorithm.Sha256, Value = ByteString.CopyFrom(Convert.FromHexString(authority.OutputSchemaFingerprintSha256)), SizeBytes = work.OutputSchema.SizeBytes },
                OperationKind = work.OperationKind,
                InputBoundProof = request.InputBoundProof.Clone(),
                DispatchDeadline = request.DispatchDeadline.Clone(),
                Limits = work.Limits.Clone(),
                EvaluatedAt = Instant(now: receipt.EffectiveGateTime),
                RequestId = request.RequestId,
                OperationId = work.ProviderDispatch.OperationId.Clone(),
                RequestFingerprintSha256 = request.RequestFingerprintSha256,
            },
        };
    }

    private static ProviderUsageContract CreateUsage(HelperReceiptV2 receipt)
    {
        static ProviderQuantityContract Quantity(OptionalUInt64? value) => value is null
            ? new(ProviderAvailabilityState.Unavailable, null)
            : new((ProviderAvailabilityState)(int)value.Availability,
                value.Availability == AvailabilityState.Available ? checked((long)value.Value) : null);
        ProviderAvailabilityState availability = receipt.UsageReceiptState == UsageReceiptStateV2.Complete
            ? ProviderAvailabilityState.Available : ProviderAvailabilityState.Unavailable;
        return new(
            availability,
            Quantity(receipt.DispatchCount), Quantity(receipt.InputTokens), Quantity(receipt.OutputTokens),
            Quantity(receipt.TotalTokens), Quantity(receipt.ReasoningTokens), Quantity(receipt.CacheReadTokens),
            Quantity(receipt.CacheWriteTokens), Quantity(receipt.PricedToolCalls), Quantity(receipt.CalculatedNanoUsd),
            ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable,
            ProviderAvailabilityState.Unavailable, receipt.UsageReceiptState switch
            {
                UsageReceiptStateV2.Complete => UsageReceiptState.Complete,
                UsageReceiptStateV2.Partial => UsageReceiptState.Partial,
                UsageReceiptStateV2.FailedKnown => UsageReceiptState.FailedKnown,
                UsageReceiptStateV2.Ambiguous => UsageReceiptState.Ambiguous,
                UsageReceiptStateV2.NotDispatched => UsageReceiptState.NotDispatched,
                _ => UsageReceiptState.Unavailable,
            });
    }

    private static List<ProviderRateLimitFactContract> CreateRateFacts(
        IReadOnlyList<OpenAiRateHeader> headers,
        DateTimeOffset observedAt)
    {
        Dictionary<string, string> values = headers.ToDictionary(item => item.Name, item => item.Value, StringComparer.Ordinal);
        List<ProviderRateLimitFactContract> result = [];
        foreach ((string suffix, string dimension) in new[]
        {
            ("requests", "requests"),
            ("input-tokens", "input-tokens"),
            ("output-tokens", "output-tokens"),
            ("tokens", "total-tokens"),
        })
        {
            if (values.TryGetValue("x-ratelimit-limit-" + suffix, out string? limitText)
                && values.TryGetValue("x-ratelimit-remaining-" + suffix, out string? remainingText)
                && long.TryParse(limitText, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out long limit)
                && long.TryParse(remainingText, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out long remaining)
                && limit >= 0 && remaining >= 0)
            {
                result.Add(new("model", dimension, ProviderAvailabilityState.Available,
                    limit, remaining, new UtcTimestamp(observedAt), null));
            }
        }
        return result;
    }

    private static ProviderUsageContract NormalizeUsageRateAvailability(
        ProviderUsageContract usage,
        List<ProviderRateLimitFactContract> rateFacts) => usage with
        {
            RateAvailability = rateFacts.Count == 0
                ? ProviderAvailabilityState.Unavailable
                : ProviderAvailabilityState.Available,
        };

    private static Infinium.Contracts.Protobuf.Common.V1.Instant Instant(DateTimeOffset now) => new()
    {
        UnixSeconds = now.ToUnixTimeSeconds(),
        Nanoseconds = checked((int)((now.Ticks % TimeSpan.TicksPerSecond) * 100)),
    };

    private static Google.Protobuf.ByteString ProtocolFingerprint() =>
        Google.Protobuf.ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256));

    private static string Hex(ByteString? value) => value is null ? string.Empty : Convert.ToHexStringLower(value.Span);

    private static string OperationKind(ProviderOperationKindV2 value) => value switch
    {
        ProviderOperationKindV2.TransportQualification => "transport-qualification",
        ProviderOperationKindV2.SourceClaimExtraction => "source-claim-extraction",
        ProviderOperationKindV2.CandidateInvestigation => "candidate-investigation",
        _ => string.Empty,
    };

    private static string InputBoundStatus(InputBoundProofStatusV2 value) => value switch
    {
        InputBoundProofStatusV2.Proved => "proved",
        InputBoundProofStatusV2.AuthorityRequired => "authority-required",
        _ => string.Empty,
    };

    private static DateTimeOffset ToDateTimeOffset(Infinium.Contracts.Protobuf.Common.V1.Instant value) =>
        DateTimeOffset.FromUnixTimeSeconds(value.UnixSeconds).AddTicks(value.Nanoseconds / 100);

    private static (string IntentKind, string State, bool IncrementRevocation) CredentialTransition(
        HelperAssignmentKindV2 kind,
        string currentState) => kind switch
        {
            HelperAssignmentKindV2.Enroll when currentState == "pending-enrollment" => ("enroll", "active-unverified", false),
            HelperAssignmentKindV2.Verify when currentState == "active-unverified" => ("verify", "active-verified", false),
            HelperAssignmentKindV2.Replace when currentState == "replacing" => ("replace", "active-unverified", false),
            HelperAssignmentKindV2.Disable when currentState is "active-unverified" or "active-verified" => ("disable", "disabled", false),
            HelperAssignmentKindV2.Delete when currentState != "delete-pending" => ("delete", "delete-pending", true),
            HelperAssignmentKindV2.Delete when currentState == "delete-pending" => ("delete", "deleted", false),
            HelperAssignmentKindV2.Recover when currentState == "pending-enrollment" => ("enroll", "active-unverified", false),
            HelperAssignmentKindV2.Recover when currentState is "secure-store-unavailable" or "recovery-required"
                => ("recover", "active-unverified", false),
            HelperAssignmentKindV2.Recover when currentState == "delete-pending"
                => ("recover", "active-unverified", false),
            _ => throw new InvalidOperationException("The helper receipt cannot drive a lifecycle transition from the current authoritative state."),
        };
}
