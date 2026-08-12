using Google.Protobuf;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Coordinator;

public sealed record CoordinatedHelperReceipt(
    HelperProcessReceipt Process,
    HelperStagingReceipt Staging);

public sealed class CredentialHelperCoordinator
{
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
            bootstrap, assignment, finalRevalidation, TimeSpan.FromSeconds(30), now, cancellationToken)
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
        if (process.StagedResponseBytes.Length > 0
            && (process.Receipt.RawResponse is null
                || process.Receipt.RawResponse.SizeBytes != (ulong)process.StagedResponseBytes.Length
                || !process.Receipt.RawResponse.Value.Span.SequenceEqual(
                    System.Security.Cryptography.SHA256.HashData(process.StagedResponseBytes))))
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
            CancellationToken cancellationToken = default)
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
        if (work.Credential?.AccessProfileId?.Value != profileId
            || work.Credential?.GenerationId?.Value != generationId
            || bootstrap.Bootstrap.Credential?.AccessProfileId?.Value != profileId
            || bootstrap.Bootstrap.Credential?.GenerationId?.Value != generationId)
        {
            throw new InvalidDataException("The helper credential subject is not the authoritative lifecycle subject.");
        }
        CoordinatedHelperReceipt helper = await ExecuteStageAndAdmitAsync(
            attemptId, bootstrap, assignment, null, now, cancellationToken).ConfigureAwait(false);
        HelperOutcomeV2 outcome = helper.Process.Receipt.Outcome;
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
            now,
            now.AddTicks(1),
            IncrementRevocationEpoch: incrementRevocation);
        if (outcome != HelperOutcomeV2.Completed)
        {
            transition = transition with
            {
                TerminalState = outcome == HelperOutcomeV2.Unavailable
                    ? "secure-store-unavailable"
                    : "recovery-required",
                ToState = outcome == HelperOutcomeV2.Unavailable
                    ? "secure-store-unavailable"
                    : "recovery-required",
                SecureStoreUnavailable = outcome == HelperOutcomeV2.Unavailable,
                Failed = outcome != HelperOutcomeV2.Unavailable,
            };
        }
        return (helper, store.ApplyCredentialTransition(transition));
    }

    public async Task<(CoordinatedHelperReceipt Helper, ProviderSimulationPersistenceReceipt Persisted,
        ProviderBudgetSettlementReceipt Settlement)> ExecuteAuthoritativeDispatchAsync(
        string attemptId,
        HelperPrivateFrameV2 bootstrap,
        HelperPrivateFrameV2 assignment,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
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
        CoordinatedHelperReceipt helper = await ExecuteStageAndAdmitAsync(
            attemptId, bootstrap, assignment, final, now, cancellationToken).ConfigureAwait(false);
        if (helper.Process.Receipt.Outcome != HelperOutcomeV2.Completed
            || helper.Process.StagedResponseBytes.Length == 0)
        {
            throw new InvalidOperationException("The authorized helper dispatch did not produce one admissible staged response.");
        }
        store.RecordProviderTransportStart(operationId, providerAttemptId, request.RequestId, fenceId, false, now);
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
            ProviderResponseState.Completed,
            200,
            "gpt-5.6-sol",
            "default",
            null,
            null,
            null,
            CreateUsage(helper.Process.Receipt),
            [],
            helper.Process.StagedResponseBytes,
            now));
        ProviderBudgetSettlementReceipt settlement = store.SettleProviderBudget(new(
            work.AssignmentId + ":settlement",
            reservationId,
            persisted.SettlementKind,
            persisted.UsageEntryId,
            persisted.Actual,
            now));
        return (helper, persisted, settlement);
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
            : new((ProviderAvailabilityState)(int)value.Availability, checked((long)value.Value));
        return new(
            ProviderAvailabilityState.Available,
            Quantity(receipt.DispatchCount), Quantity(receipt.InputTokens), Quantity(receipt.OutputTokens),
            Quantity(receipt.TotalTokens), Quantity(receipt.ReasoningTokens), Quantity(receipt.CacheReadTokens),
            Quantity(receipt.CacheWriteTokens), Quantity(receipt.PricedToolCalls), Quantity(receipt.CalculatedNanoUsd),
            ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable,
            ProviderAvailabilityState.Unavailable, UsageReceiptState.Complete);
    }

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
            HelperAssignmentKindV2.Replace when currentState == "active-verified" => ("replace", "active-unverified", false),
            HelperAssignmentKindV2.Disable when currentState is "active-unverified" or "active-verified" => ("disable", "disabled", false),
            HelperAssignmentKindV2.Delete when currentState != "delete-pending" => ("delete", "delete-pending", true),
            HelperAssignmentKindV2.Delete when currentState == "delete-pending" => ("delete", "deleted", false),
            HelperAssignmentKindV2.Recover when currentState == "pending-enrollment" => ("enroll", "active-unverified", false),
            HelperAssignmentKindV2.Recover when currentState is "secure-store-unavailable" or "recovery-required"
                => ("recover", "active-unverified", false),
            _ => throw new InvalidOperationException("The helper receipt cannot drive a lifecycle transition from the current authoritative state."),
        };
}
