using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using Google.Protobuf;
using Infinium.Contracts.Protobuf.Helper.V1;
using Infinium.Contracts.Protobuf.Helper.V2;

namespace Infinium.Application.Runtime;

public static class HelperProtocolV2Codec
{
    public static HelperPrivateFrameV2 Decode(
        ReadOnlySpan<byte> bytes,
        DateTimeOffset now,
        string? expectedAssignmentId = null,
        string? expectedCommandId = null,
        string? expectedOperationId = null,
        string? expectedAttemptId = null,
        string? expectedProfileId = null,
        string? expectedGenerationId = null,
        ulong? expectedGenerationOrdinal = null,
        string? expectedRequestId = null,
        string? expectedDispatchId = null,
        byte[]? expectedRequestFingerprintSha256 = null,
        string? expectedInputBoundPolicyId = null,
        string? expectedInputBoundPolicyVersion = null,
        ulong? expectedCoordinatorFencingEpoch = null,
        string? expectedCapabilitySnapshotId = null,
        string? expectedPriceSnapshotId = null,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedSettings = null,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedOutputSchema = null,
        string? expectedEffectiveConfigurationId = null,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedNonSecretReceipt = null,
        ulong? expectedRevocationEpoch = null,
        string? expectedAccountIdentityId = null,
        string? expectedBillingScopeIdentityId = null,
        string? expectedReservationGroupId = null,
        ProviderOperationKindV2? expectedOperationKind = null,
        HelperLimitsV2? expectedLimits = null,
        Infinium.Contracts.Protobuf.Common.V1.Instant? expectedDispatchDeadline = null,
        ulong expectedMaximumFrameBytes = HelperProtocolV2Constants.MaximumFrameBytes,
        byte[]? expectedOneUseNonceFingerprintSha256 = null,
        Infinium.Contracts.Protobuf.Common.V1.Instant? expectedBootstrapExpiresAt = null,
        HelperPrivateFrameV2.PayloadOneofCase? expectedPayloadCase = null,
        ulong? expectedSequence = null,
        HelperAssignmentKindV2? expectedAssignmentKind = null)
    {
        if (expectedMaximumFrameBytes is 0 or > HelperProtocolV2Constants.MaximumFrameBytes
            || bytes.IsEmpty || (ulong)bytes.Length > expectedMaximumFrameBytes)
        {
            throw new InvalidDataException("Helper v2 frame size is outside the closed bound.");
        }
        HelperPrivateFrameV2 frame;
        try
        {
            frame = HelperPrivateFrameV2.Parser.ParseFrom(bytes.ToArray());
        }
        catch (InvalidProtocolBufferException exception)
        {
            throw new InvalidDataException("Helper v2 frame is malformed.", exception);
        }
        RejectUnknownFields(frame, "$frame");
        Validate(frame, now, expectedAssignmentId, expectedCommandId, expectedOperationId,
            expectedAttemptId, expectedProfileId, expectedGenerationId, expectedGenerationOrdinal,
            expectedRequestId,
            expectedDispatchId, expectedRequestFingerprintSha256, expectedInputBoundPolicyId,
            expectedInputBoundPolicyVersion, expectedCoordinatorFencingEpoch, expectedCapabilitySnapshotId,
            expectedPriceSnapshotId, expectedSettings, expectedOutputSchema, expectedEffectiveConfigurationId,
            expectedNonSecretReceipt, expectedRevocationEpoch, expectedAccountIdentityId,
            expectedBillingScopeIdentityId, expectedReservationGroupId, expectedOperationKind,
            expectedLimits, expectedDispatchDeadline, expectedMaximumFrameBytes,
            expectedOneUseNonceFingerprintSha256, expectedBootstrapExpiresAt,
            expectedPayloadCase, expectedSequence, expectedAssignmentKind);
        return frame;
    }

    public static HelperPrivateFrame DecodeV1(ReadOnlySpan<byte> bytes)
    {
        try
        {
            HelperPrivateFrame frame = HelperPrivateFrame.Parser.ParseFrom(bytes.ToArray());
            RejectUnknownFields(frame, "$v1-frame");
            return frame;
        }
        catch (InvalidProtocolBufferException exception)
        {
            throw new InvalidDataException("Helper v1 frame is malformed.", exception);
        }
    }

    private static void Validate(
        HelperPrivateFrameV2 frame,
        DateTimeOffset now,
        string? expectedAssignmentId,
        string? expectedCommandId,
        string? expectedOperationId,
        string? expectedAttemptId,
        string? expectedProfileId,
        string? expectedGenerationId,
        ulong? expectedGenerationOrdinal,
        string? expectedRequestId,
        string? expectedDispatchId,
        byte[]? expectedRequestFingerprintSha256,
        string? expectedInputBoundPolicyId,
        string? expectedInputBoundPolicyVersion,
        ulong? expectedCoordinatorFencingEpoch,
        string? expectedCapabilitySnapshotId,
        string? expectedPriceSnapshotId,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedSettings,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedOutputSchema,
        string? expectedEffectiveConfigurationId,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedNonSecretReceipt,
        ulong? expectedRevocationEpoch,
        string? expectedAccountIdentityId,
        string? expectedBillingScopeIdentityId,
        string? expectedReservationGroupId,
        ProviderOperationKindV2? expectedOperationKind,
        HelperLimitsV2? expectedLimits,
        Infinium.Contracts.Protobuf.Common.V1.Instant? expectedDispatchDeadline,
        ulong expectedMaximumFrameBytes,
        byte[]? expectedOneUseNonceFingerprintSha256,
        Infinium.Contracts.Protobuf.Common.V1.Instant? expectedBootstrapExpiresAt,
        HelperPrivateFrameV2.PayloadOneofCase? expectedPayloadCase,
        ulong? expectedSequence,
        HelperAssignmentKindV2? expectedAssignmentKind)
    {
        if (expectedPayloadCase is null or HelperPrivateFrameV2.PayloadOneofCase.None
            || expectedSequence is null or 0
            || frame.PayloadCase != expectedPayloadCase
            || frame.Sequence != expectedSequence
            || frame.Sequence == 0
            || frame.ProtocolFingerprintSha256.Length != 32
            || !frame.ProtocolFingerprintSha256.Span.SequenceEqual(
                Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)))
        {
            throw new InvalidDataException("Helper v2 frame identity is absent or mismatched.");
        }
        switch (frame.PayloadCase)
        {
            case HelperPrivateFrameV2.PayloadOneofCase.Bootstrap:
                Require(frame.Bootstrap.CommandId, "bootstrap.command_id");
                ValidateSubject(frame.Bootstrap.SubjectCase, frame.Bootstrap.Credential,
                    frame.Bootstrap.ProviderDispatch, "bootstrap");
                if (frame.Bootstrap.CoordinatorFencingEpoch == 0 || frame.Bootstrap.OneUseNonceFingerprintSha256.Length != 32
                    || !ValidFutureInstant(frame.Bootstrap.ExpiresAt, now))
                {
                    throw new InvalidDataException("Helper v2 bootstrap is incomplete.");
                }
                ValidateExpectedSubject(frame.Bootstrap.SubjectCase, frame.Bootstrap.Credential,
                    frame.Bootstrap.ProviderDispatch, expectedProfileId, expectedGenerationId,
                    expectedOperationId, expectedAttemptId, "bootstrap");
                Require(expectedCommandId, "expected_bootstrap.command_id");
                if (expectedOneUseNonceFingerprintSha256 is null
                    || expectedOneUseNonceFingerprintSha256.Length != 32
                    || expectedBootstrapExpiresAt is null)
                {
                    throw new InvalidDataException("Bootstrap expected nonce and coordinator-selected expiry are required.");
                }
                if (frame.Bootstrap.CommandId != expectedCommandId
                    || expectedCoordinatorFencingEpoch is null or 0
                    || frame.Bootstrap.CoordinatorFencingEpoch != expectedCoordinatorFencingEpoch
                    || !CryptographicOperations.FixedTimeEquals(
                        frame.Bootstrap.OneUseNonceFingerprintSha256.Span,
                        expectedOneUseNonceFingerprintSha256)
                    || !SameInstant(frame.Bootstrap.ExpiresAt, expectedBootstrapExpiresAt))
                {
                    throw new InvalidDataException("Bootstrap must bind the expected command, subject, and fencing epoch.");
                }
                break;
            case HelperPrivateFrameV2.PayloadOneofCase.Assignment:
                Validate(frame.Assignment, now, expectedAssignmentId, expectedCommandId,
                    expectedOperationId, expectedAttemptId, expectedProfileId, expectedGenerationId,
                    expectedGenerationOrdinal,
                    expectedRequestId, expectedDispatchId, expectedRequestFingerprintSha256,
                    expectedInputBoundPolicyId, expectedInputBoundPolicyVersion, expectedRevocationEpoch,
                    expectedAccountIdentityId, expectedBillingScopeIdentityId, expectedReservationGroupId,
                    expectedOperationKind, expectedLimits, expectedDispatchDeadline,
                    expectedCapabilitySnapshotId, expectedPriceSnapshotId, expectedSettings,
                    expectedOutputSchema, expectedEffectiveConfigurationId, expectedAssignmentKind);
                break;
            case HelperPrivateFrameV2.PayloadOneofCase.DispatchRevalidation:
                Validate(frame.DispatchRevalidation, now, expectedOperationId, expectedAttemptId,
                    expectedProfileId, expectedGenerationId, expectedRequestId, expectedDispatchId,
                    expectedRequestFingerprintSha256, expectedInputBoundPolicyId,
                    expectedInputBoundPolicyVersion, expectedCoordinatorFencingEpoch,
                    expectedRevocationEpoch, expectedAccountIdentityId, expectedBillingScopeIdentityId,
                    expectedReservationGroupId, expectedOperationKind, expectedLimits,
                    expectedDispatchDeadline, expectedCapabilitySnapshotId, expectedPriceSnapshotId,
                    expectedSettings, expectedOutputSchema, expectedEffectiveConfigurationId);
                break;
            case HelperPrivateFrameV2.PayloadOneofCase.Receipt:
                Validate(frame.Receipt, expectedAssignmentId, expectedCommandId, expectedOperationId,
                    expectedAttemptId, expectedProfileId, expectedGenerationId, expectedRequestId,
                    expectedDispatchId, expectedRequestFingerprintSha256, expectedInputBoundPolicyId,
                    expectedInputBoundPolicyVersion, expectedCoordinatorFencingEpoch, expectedCapabilitySnapshotId,
                    expectedPriceSnapshotId, expectedSettings, expectedOutputSchema,
                    expectedEffectiveConfigurationId, expectedNonSecretReceipt, expectedRevocationEpoch,
                    expectedAccountIdentityId, expectedBillingScopeIdentityId, expectedReservationGroupId,
                    expectedOperationKind, expectedLimits, expectedDispatchDeadline,
                    expectedAssignmentKind);
                break;
            default:
                throw new InvalidDataException("Helper v2 payload kind must be explicit.");
        }
        if (expectedLimits is not null && expectedLimits.MaximumFrameBytes != expectedMaximumFrameBytes)
        {
            throw new InvalidDataException("Received helper frame must use the exact coordinator-selected frame bound.");
        }
    }

    private static void Validate(
        HelperAssignmentV2 value,
        DateTimeOffset now,
        string? expectedAssignmentId,
        string? expectedCommandId,
        string? expectedOperationId,
        string? expectedAttemptId,
        string? expectedProfileId,
        string? expectedGenerationId,
        ulong? expectedGenerationOrdinal,
        string? expectedRequestId,
        string? expectedDispatchId,
        byte[]? expectedRequestFingerprintSha256,
        string? expectedInputBoundPolicyId,
        string? expectedInputBoundPolicyVersion,
        ulong? expectedRevocationEpoch,
        string? expectedAccountIdentityId,
        string? expectedBillingScopeIdentityId,
        string? expectedReservationGroupId,
        ProviderOperationKindV2? expectedOperationKind,
        HelperLimitsV2? expectedLimits,
        Infinium.Contracts.Protobuf.Common.V1.Instant? expectedDispatchDeadline,
        string? expectedCapabilitySnapshotId,
        string? expectedPriceSnapshotId,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedSettings,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedOutputSchema,
        string? expectedEffectiveConfigurationId,
        HelperAssignmentKindV2? expectedAssignmentKind)
    {
        Require(value.AccessProfileId?.Value, "assignment.access_profile_id");
        Require(value.GenerationId?.Value, "assignment.generation_id");
        Require(value.AssignmentId, "assignment.assignment_id");
        Require(value.CommandId, "assignment.command_id");
        Require(expectedAssignmentId, "expected_assignment.assignment_id");
        Require(expectedCommandId, "expected_assignment.command_id");
        Require(expectedProfileId, "expected_assignment.profile_id");
        Require(expectedGenerationId, "expected_assignment.generation_id");
        if (!Enum.IsDefined(value.AssignmentKind) || value.AssignmentKind == HelperAssignmentKindV2.Unspecified
            || expectedAssignmentKind is null or HelperAssignmentKindV2.Unspecified
            || value.AssignmentKind != expectedAssignmentKind
            || expectedGenerationOrdinal is null or 0
            || value.GenerationOrdinal != expectedGenerationOrdinal)
        {
            throw new InvalidDataException("Helper v2 assignment uses an unknown numeric state or incomplete binding.");
        }
        bool dispatch = value.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch;
        if (dispatch != (value.SubjectCase == HelperAssignmentV2.SubjectOneofCase.ProviderDispatch)
            || !dispatch != (value.SubjectCase == HelperAssignmentV2.SubjectOneofCase.Credential))
        {
            throw new InvalidDataException("Helper assignment subject must match its credential or provider-dispatch kind.");
        }
        ValidateSubject(value.SubjectCase, value.Credential, value.ProviderDispatch, "assignment");
        if (value.AssignmentId != expectedAssignmentId || value.CommandId != expectedCommandId
            || value.AccessProfileId?.Value != expectedProfileId || value.GenerationId?.Value != expectedGenerationId
            || expectedRevocationEpoch is null || value.RevocationEpoch != expectedRevocationEpoch)
        {
            throw new InvalidDataException("Helper assignment must bind the expected command, profile generation, and revocation epoch.");
        }
        if (!dispatch && (value.Credential!.AccessProfileId?.Value != value.AccessProfileId?.Value
            || value.Credential.GenerationId?.Value != value.GenerationId?.Value))
        {
            throw new InvalidDataException("Credential assignment subject must bind the exact profile generation.");
        }
        if (dispatch != (value.ProviderRequest is not null))
        {
            throw new InvalidDataException("Only a provider-dispatch assignment may carry a provider request.");
        }
        if (dispatch)
        {
            Require(expectedOperationId, "expected_assignment.operation_id");
            Require(expectedAttemptId, "expected_assignment.attempt_id");
            Require(expectedRequestId, "expected_assignment.request_id");
            Require(expectedDispatchId, "expected_assignment.dispatch_id");
            Require(expectedAccountIdentityId, "expected_assignment.account_identity_id");
            Require(expectedBillingScopeIdentityId, "expected_assignment.billing_scope_identity_id");
            Require(expectedReservationGroupId, "expected_assignment.reservation_group_id");
            Require(expectedCapabilitySnapshotId, "expected_assignment.capability_snapshot_id");
            Require(expectedPriceSnapshotId, "expected_assignment.price_snapshot_id");
            Require(expectedEffectiveConfigurationId, "expected_assignment.effective_configuration_id");
            if (!Enum.IsDefined(value.OperationKind) || value.OperationKind == ProviderOperationKindV2.Unspecified
                || value.Limits is null || expectedOperationKind is null or ProviderOperationKindV2.Unspecified
                || expectedLimits is null || expectedDispatchDeadline is null
                || expectedRequestFingerprintSha256 is null || expectedRequestFingerprintSha256.Length != 32
                || !ValidDigest(expectedSettings) || !ValidDigest(expectedOutputSchema))
            {
                throw new InvalidDataException("Provider dispatch assignment is missing its operation kind or limits.");
            }
            ValidateLimits(value.OperationKind, value.Limits);
            ProviderRequestV2 request = value.ProviderRequest!;
            Require(request.DispatchId?.Value, "provider_request.dispatch_id");
            Require(request.CapabilitySnapshotId?.Value, "provider_request.capability_snapshot_id");
            Require(request.PriceSnapshotId?.Value, "provider_request.price_snapshot_id");
            Require(request.ReservationGroupId?.Value, "provider_request.reservation_group_id");
            Require(request.RequestId, "provider_request.request_id");
            if (request.EndpointIdentity != ProviderEndpointV2.OpenaiResponses
                || request.CanonicalRequestBytes.IsEmpty
                || (uint)request.CanonicalRequestBytes.Length > value.Limits.MaximumRequestBytes
                || !ValidExactDigest(request.CanonicalRequest, request.CanonicalRequestBytes.Span)
                || request.RequestFingerprintSha256.Length != 32
                || !request.RequestFingerprintSha256.Span.SequenceEqual(request.CanonicalRequest.Value.Span)
                || !ValidInstant(request.ConfirmedAt)
                || !ValidFutureInstant(request.DispatchDeadline, now)
                || ElapsedHundredNanoseconds(request.ConfirmedAt, request.DispatchDeadline)
                    > checked(value.Limits.MaximumDuration.Value * 10_000UL)
                || !IsAcceptedInputProof(request.InputBoundProof))
            {
                throw new InvalidDataException("Helper v2 provider request is not a closed, bounded Responses request with an accepted input-bound proof.");
            }
            if (value.ProviderDispatch!.OperationId.Value != expectedOperationId
                || value.ProviderDispatch.AttemptId.Value != expectedAttemptId
                || request.RequestId != expectedRequestId || request.DispatchId!.Value != expectedDispatchId
                || !request.RequestFingerprintSha256.Span.SequenceEqual(expectedRequestFingerprintSha256)
                || request.InputBoundProof.PolicyId != expectedInputBoundPolicyId
                || request.InputBoundProof.PolicyVersion != expectedInputBoundPolicyVersion
                || request.ReservationGroupId!.Value != expectedReservationGroupId
                || request.CapabilitySnapshotId!.Value != expectedCapabilitySnapshotId
                || request.PriceSnapshotId!.Value != expectedPriceSnapshotId
                || value.OperationKind != expectedOperationKind || !SameLimits(value.Limits, expectedLimits)
                || !SameInstant(request.DispatchDeadline, expectedDispatchDeadline)
                || value.AccountIdentityId?.Value != expectedAccountIdentityId
                || value.BillingScopeIdentityId?.Value != expectedBillingScopeIdentityId
                || value.EffectiveConfigurationId != expectedEffectiveConfigurationId
                || !SameDigest(value.Settings, expectedSettings) || !SameDigest(value.OutputSchema, expectedOutputSchema))
            {
                throw new InvalidDataException("Provider assignment cross-rebound an expected authority, request, reservation, limit, or configuration identity.");
            }
        }
        else if (value.ProviderRequest is not null || value.Limits is not null
            || value.OperationKind != ProviderOperationKindV2.Unspecified
            || value.AccountIdentityId is not null || value.BillingScopeIdentityId is not null
            || !string.IsNullOrEmpty(value.EffectiveConfigurationId) || value.Settings is not null
            || value.OutputSchema is not null)
        {
            throw new InvalidDataException("Credential-only assignments cannot fabricate provider dispatch fields.");
        }
    }

    private static void ValidateLimits(ProviderOperationKindV2 kind, HelperLimitsV2 value)
    {
        (uint request, uint input, uint output, uint response, long cost, ulong duration) = kind switch
        {
            ProviderOperationKindV2.TransportQualification => (16_384U, 20_480U, 256U, 262_144U, 140_000_000, 60_000UL),
            ProviderOperationKindV2.SourceClaimExtraction or ProviderOperationKindV2.CandidateInvestigation =>
                (65_536U, 73_728U, 4_096U, 1_048_576U, 600_000_000, 120_000UL),
            _ => throw new InvalidDataException("Helper v2 operation kind is unknown."),
        };
        if (value.MaximumFrameBytes is 0 or > HelperProtocolV2Constants.MaximumFrameBytes
            || value.MaximumRequestBytes is 0 || value.MaximumRequestBytes > request
            || value.MaximumInputTokens is 0 || value.MaximumInputTokens > input
            || value.MaximumOutputTokens is 0 || value.MaximumOutputTokens > output
            || value.MaximumResponseBytes is 0 || value.MaximumResponseBytes > response
            || value.MaximumStagedOutputBytes is 0 || value.MaximumStagedOutputBytes > response
            || value.MaximumCalculatedNanoUsd is <= 0 || value.MaximumCalculatedNanoUsd > cost
            || value.MaximumDuration?.Value is 0 || value.MaximumDuration?.Value > duration
            || value.MaximumDispatchCount != 1)
        {
            throw new InvalidDataException("Helper v2 limits exceed the operation-specific seven-dimensional ceiling.");
        }
    }

    private static void Validate(
        DispatchRevalidationV2 value,
        DateTimeOffset now,
        string? expectedOperationId,
        string? expectedAttemptId,
        string? expectedProfileId,
        string? expectedGenerationId,
        string? expectedRequestId,
        string? expectedDispatchId,
        byte[]? expectedRequestFingerprintSha256,
        string? expectedInputBoundPolicyId,
        string? expectedInputBoundPolicyVersion,
        ulong? expectedCoordinatorFencingEpoch,
        ulong? expectedRevocationEpoch,
        string? expectedAccountIdentityId,
        string? expectedBillingScopeIdentityId,
        string? expectedReservationGroupId,
        ProviderOperationKindV2? expectedOperationKind,
        HelperLimitsV2? expectedLimits,
        Infinium.Contracts.Protobuf.Common.V1.Instant? expectedDispatchDeadline,
        string? expectedCapabilitySnapshotId,
        string? expectedPriceSnapshotId,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedSettings,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedOutputSchema,
        string? expectedEffectiveConfigurationId)
    {
        Require(value.DispatchId?.Value, "revalidation.dispatch_id");
        Require(value.AttemptId?.Value, "revalidation.attempt_id");
        Require(value.AccessProfileId?.Value, "revalidation.access_profile_id");
        Require(value.GenerationId?.Value, "revalidation.generation_id");
        Require(value.ReservationGroupId?.Value, "revalidation.reservation_group_id");
        Require(value.AccountIdentityId?.Value, "revalidation.account_identity_id");
        Require(value.BillingScopeIdentityId?.Value, "revalidation.billing_scope_identity_id");
        Require(value.EffectiveConfigurationId, "revalidation.effective_configuration_id");
        Require(value.CapabilitySnapshotId?.Value, "revalidation.capability_snapshot_id");
        Require(value.PriceSnapshotId?.Value, "revalidation.price_snapshot_id");
        Require(value.OperationId?.Value, "revalidation.operation_id");
        Require(expectedOperationId, "expected_revalidation.operation_id");
        Require(expectedAttemptId, "expected_revalidation.attempt_id");
        Require(expectedProfileId, "expected_revalidation.profile_id");
        Require(expectedGenerationId, "expected_revalidation.generation_id");
        Require(expectedRequestId, "expected_revalidation.request_id");
        Require(expectedDispatchId, "expected_revalidation.dispatch_id");
        Require(expectedAccountIdentityId, "expected_revalidation.account_identity_id");
        Require(expectedBillingScopeIdentityId, "expected_revalidation.billing_scope_identity_id");
        Require(expectedReservationGroupId, "expected_revalidation.reservation_group_id");
        Require(expectedCapabilitySnapshotId, "expected_revalidation.capability_snapshot_id");
        Require(expectedPriceSnapshotId, "expected_revalidation.price_snapshot_id");
        Require(expectedEffectiveConfigurationId, "expected_revalidation.effective_configuration_id");
        if (!Enum.IsDefined(value.Disposition) || value.Disposition == DispatchDispositionV2.Unspecified
            || !Enum.IsDefined(value.OperationKind) || value.OperationKind == ProviderOperationKindV2.Unspecified
            || value.CoordinatorFencingEpoch == 0 || !ValidDigest(value.CanonicalRequest)
            || !ValidDigest(value.Settings) || !ValidDigest(value.OutputSchema)
            || !ValidFutureInstant(value.DispatchDeadline, now) || !ValidInstant(value.EvaluatedAt)
            || string.IsNullOrWhiteSpace(value.RequestId) || value.Limits is null
            || !value.AuthorizedOnce || value.Disposition != DispatchDispositionV2.Authorized
            || !IsAcceptedInputProof(value.InputBoundProof)
            || expectedRequestFingerprintSha256 is null || expectedRequestFingerprintSha256.Length != 32
            || expectedCoordinatorFencingEpoch is null or 0 || expectedRevocationEpoch is null
            || expectedOperationKind is null or ProviderOperationKindV2.Unspecified
            || expectedLimits is null || expectedDispatchDeadline is null
            || !ValidDigest(expectedSettings) || !ValidDigest(expectedOutputSchema))
        {
            throw new InvalidDataException("Helper v2 final revalidation is incomplete or internally contradictory.");
        }
        ValidateLimits(value.OperationKind, value.Limits);
        if (ElapsedHundredNanoseconds(value.EvaluatedAt, value.DispatchDeadline)
            > checked(value.Limits.MaximumDuration.Value * 10_000UL))
        {
            throw new InvalidDataException("Helper final revalidation deadline exceeds the operation-specific duration ceiling.");
        }
        if (value.OperationId!.Value != expectedOperationId || value.AttemptId!.Value != expectedAttemptId
            || value.AccessProfileId!.Value != expectedProfileId || value.GenerationId!.Value != expectedGenerationId
            || value.RequestId != expectedRequestId || value.DispatchId!.Value != expectedDispatchId
            || !CryptographicOperations.FixedTimeEquals(
                value.RequestFingerprintSha256.Span, expectedRequestFingerprintSha256)
            || !CryptographicOperations.FixedTimeEquals(
                value.CanonicalRequest.Value.Span, expectedRequestFingerprintSha256)
            || value.CoordinatorFencingEpoch != expectedCoordinatorFencingEpoch
            || value.RevocationEpoch != expectedRevocationEpoch
            || value.AccountIdentityId!.Value != expectedAccountIdentityId
            || value.BillingScopeIdentityId!.Value != expectedBillingScopeIdentityId
            || value.ReservationGroupId!.Value != expectedReservationGroupId
            || value.OperationKind != expectedOperationKind || !SameLimits(value.Limits, expectedLimits)
            || !SameInstant(value.DispatchDeadline, expectedDispatchDeadline)
            || value.CapabilitySnapshotId!.Value != expectedCapabilitySnapshotId
            || value.PriceSnapshotId!.Value != expectedPriceSnapshotId
            || !SameDigest(value.Settings, expectedSettings) || !SameDigest(value.OutputSchema, expectedOutputSchema)
            || value.EffectiveConfigurationId != expectedEffectiveConfigurationId
            || value.InputBoundProof.PolicyId != expectedInputBoundPolicyId
            || value.InputBoundProof.PolicyVersion != expectedInputBoundPolicyVersion)
        {
            throw new InvalidDataException("Final revalidation cross-rebound an expected authorization or request identity.");
        }
    }

    private static void Validate(
        HelperReceiptV2 value,
        string? expectedAssignmentId,
        string? expectedCommandId,
        string? expectedOperationId,
        string? expectedAttemptId,
        string? expectedProfileId,
        string? expectedGenerationId,
        string? expectedRequestId,
        string? expectedDispatchId,
        byte[]? expectedRequestFingerprintSha256,
        string? expectedInputBoundPolicyId,
        string? expectedInputBoundPolicyVersion,
        ulong? expectedCoordinatorFencingEpoch,
        string? expectedCapabilitySnapshotId,
        string? expectedPriceSnapshotId,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedSettings,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedOutputSchema,
        string? expectedEffectiveConfigurationId,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expectedNonSecretReceipt,
        ulong? expectedRevocationEpoch,
        string? expectedAccountIdentityId,
        string? expectedBillingScopeIdentityId,
        string? expectedReservationGroupId,
        ProviderOperationKindV2? expectedOperationKind,
        HelperLimitsV2? expectedLimits,
        Infinium.Contracts.Protobuf.Common.V1.Instant? expectedDispatchDeadline,
        HelperAssignmentKindV2? expectedAssignmentKind)
    {
        if (!Enum.IsDefined(value.Outcome) || value.Outcome == HelperOutcomeV2.Unspecified
            || !Enum.IsDefined(value.AssignmentKind) || value.AssignmentKind == HelperAssignmentKindV2.Unspecified
            || expectedAssignmentKind is null or HelperAssignmentKindV2.Unspecified
            || value.AssignmentKind != expectedAssignmentKind)
        {
            throw new InvalidDataException("Helper v2 receipt outcome is unknown.");
        }
        bool dispatch = value.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch;
        if (dispatch != (value.SubjectCase == HelperReceiptV2.SubjectOneofCase.ProviderDispatch)
            || !dispatch != (value.SubjectCase == HelperReceiptV2.SubjectOneofCase.Credential))
        {
            throw new InvalidDataException("Helper receipt subject must match its exact assignment kind.");
        }
        ValidateSubject(value.SubjectCase, value.Credential, value.ProviderDispatch, "receipt");
        Require(value.AssignmentId, "receipt.assignment_id");
        Require(value.CommandId, "receipt.command_id");
        Require(expectedAssignmentId, "expected_receipt.assignment_id");
        Require(expectedCommandId, "expected_receipt.command_id");
        if (!ValidDigest(expectedNonSecretReceipt))
        {
            throw new InvalidDataException("Expected credential or provider receipt digest is required.");
        }
        if (dispatch)
        {
            Require(expectedOperationId, "expected_receipt.operation_id");
            Require(expectedAttemptId, "expected_receipt.attempt_id");
            Require(expectedRequestId, "expected_receipt.request_id");
            Require(expectedDispatchId, "expected_receipt.dispatch_id");
            Require(expectedInputBoundPolicyId, "expected_receipt.input_bound_policy_id");
            Require(expectedInputBoundPolicyVersion, "expected_receipt.input_bound_policy_version");
            Require(expectedCapabilitySnapshotId, "expected_receipt.capability_snapshot_id");
            Require(expectedPriceSnapshotId, "expected_receipt.price_snapshot_id");
            Require(expectedEffectiveConfigurationId, "expected_receipt.effective_configuration_id");
            Require(expectedAccountIdentityId, "expected_receipt.account_identity_id");
            Require(expectedBillingScopeIdentityId, "expected_receipt.billing_scope_identity_id");
            Require(expectedReservationGroupId, "expected_receipt.reservation_group_id");
            if (expectedRequestFingerprintSha256 is null || expectedRequestFingerprintSha256.Length != 32
                || expectedCoordinatorFencingEpoch is null or 0
                || expectedRevocationEpoch is null || expectedOperationKind is null or ProviderOperationKindV2.Unspecified
                || expectedLimits is null || expectedDispatchDeadline is null
                || !ValidDigest(expectedSettings) || !ValidDigest(expectedOutputSchema)
                || !ValidDigest(expectedNonSecretReceipt))
            {
                throw new InvalidDataException("Expected provider receipt fingerprint and fencing epoch are required.");
            }
        }
        else
        {
            Require(expectedProfileId, "expected_receipt.profile_id");
            Require(expectedGenerationId, "expected_receipt.generation_id");
        }
        ValidateOptionalUInt64(value.InputTokens, "receipt.input_tokens");
        ValidateOptionalUInt64(value.OutputTokens, "receipt.output_tokens");
        ValidateOptionalUInt64(value.ReasoningTokens, "receipt.reasoning_tokens");
        ValidateOptionalUInt64(value.CacheReadTokens, "receipt.cache_read_tokens");
        ValidateOptionalUInt64(value.CacheWriteTokens, "receipt.cache_write_tokens");
        ValidateOptionalUInt64(value.TotalTokens, "receipt.total_tokens");
        ValidateOptionalUInt64(value.PricedToolCalls, "receipt.priced_tool_calls");
        ValidateOptionalUInt64(value.CalculatedNanoUsd, "receipt.calculated_nano_usd");
        bool noTransport = value.Outcome is HelperOutcomeV2.Unavailable or HelperOutcomeV2.Cancelled;
        bool hasResponse = value.RawResponse is not null;
        if (value.AssignmentId != expectedAssignmentId || value.CommandId != expectedCommandId
            || (dispatch && (value.ProviderDispatch!.OperationId.Value != expectedOperationId
                || value.ProviderDispatch.AttemptId.Value != expectedAttemptId))
            || (!dispatch && (value.Credential!.AccessProfileId.Value != expectedProfileId
                || value.Credential.GenerationId.Value != expectedGenerationId))
            || value.OutcomeHasResponse != hasResponse
            || value.TransportMayHaveStarted != (value.Outcome == HelperOutcomeV2.TransportMayHaveStarted
                || dispatch && hasResponse)
            || (hasResponse && !ValidDigest(value.RawResponse))
            || (!dispatch && (value.TransportMayHaveStarted || hasResponse
                || value.InputTokens is not null || value.OutputTokens is not null || value.ReasoningTokens is not null
                || value.CacheReadTokens is not null || value.CacheWriteTokens is not null
                || value.TotalTokens is not null || value.PricedToolCalls is not null || value.CalculatedNanoUsd is not null
                || !string.IsNullOrEmpty(value.RequestId) || value.DispatchId is not null || value.InputBoundProof is not null
                || !value.RequestFingerprintSha256.IsEmpty || value.CoordinatorFencingEpoch != 0
                || value.CapabilitySnapshotId is not null || value.PriceSnapshotId is not null
                || value.Settings is not null || value.OutputSchema is not null
                || !string.IsNullOrEmpty(value.EffectiveConfigurationId) || value.RevocationEpoch != 0
                || value.AccountIdentityId is not null || value.BillingScopeIdentityId is not null
                || value.ReservationGroupId is not null || value.OperationKind != ProviderOperationKindV2.Unspecified
                || value.Limits is not null || value.DispatchDeadline is not null))
            || (!dispatch && value.Outcome is HelperOutcomeV2.TransportMayHaveStarted
                or HelperOutcomeV2.Oversized or HelperOutcomeV2.Malformed)
            || (noTransport && (value.TransportMayHaveStarted || hasResponse))
            || !ValidDigest(value.NonSecretReceipt)
            || !SameDigest(value.NonSecretReceipt, expectedNonSecretReceipt))
        {
            throw new InvalidDataException("Helper v2 receipt outcome contradicts transport or response evidence.");
        }
        if (dispatch)
        {
            Require(value.RequestId, "receipt.request_id");
            Require(value.DispatchId?.Value, "receipt.dispatch_id");
            Require(value.EffectiveConfigurationId, "receipt.effective_configuration_id");
            Require(value.CapabilitySnapshotId?.Value, "receipt.capability_snapshot_id");
            Require(value.PriceSnapshotId?.Value, "receipt.price_snapshot_id");
            Require(value.AccountIdentityId?.Value, "receipt.account_identity_id");
            Require(value.BillingScopeIdentityId?.Value, "receipt.billing_scope_identity_id");
            Require(value.ReservationGroupId?.Value, "receipt.reservation_group_id");
            if (value.RequestId != expectedRequestId || value.DispatchId?.Value != expectedDispatchId
                || value.RequestFingerprintSha256.Length != 32
                || !value.RequestFingerprintSha256.Span.SequenceEqual(expectedRequestFingerprintSha256)
                || value.CoordinatorFencingEpoch != expectedCoordinatorFencingEpoch
                || value.CapabilitySnapshotId?.Value != expectedCapabilitySnapshotId
                || value.PriceSnapshotId?.Value != expectedPriceSnapshotId
                || value.EffectiveConfigurationId != expectedEffectiveConfigurationId
                || value.RevocationEpoch != expectedRevocationEpoch
                || value.AccountIdentityId?.Value != expectedAccountIdentityId
                || value.BillingScopeIdentityId?.Value != expectedBillingScopeIdentityId
                || value.ReservationGroupId?.Value != expectedReservationGroupId
                || value.OperationKind != expectedOperationKind || !SameLimits(value.Limits, expectedLimits)
                || !SameInstant(value.DispatchDeadline, expectedDispatchDeadline)
                || !SameDigest(value.Settings, expectedSettings)
                || !SameDigest(value.OutputSchema, expectedOutputSchema)
                || value.InputBoundProof?.PolicyId != expectedInputBoundPolicyId
                || value.InputBoundProof?.PolicyVersion != expectedInputBoundPolicyVersion
                || !IsAcceptedInputProof(value.InputBoundProof))
            {
                throw new InvalidDataException("Provider receipt must retain the exact assignment, command, operation, attempt, request, dispatch fence, request fingerprint, proof, receipt, and fencing binding.");
            }
            ValidateReceiptUsage(value, expectedLimits!);
        }
    }

    private static void ValidateReceiptUsage(HelperReceiptV2 value, HelperLimitsV2 limits)
    {
        bool hasUsage = value.InputTokens is not null || value.OutputTokens is not null
            || value.TotalTokens is not null || value.ReasoningTokens is not null
            || value.CacheReadTokens is not null || value.CacheWriteTokens is not null
            || value.PricedToolCalls is not null || value.CalculatedNanoUsd is not null;
        bool oversized = value.Outcome == HelperOutcomeV2.Oversized;
        if ((!value.OutcomeHasResponse && hasUsage && !oversized)
            || oversized && (value.OutcomeHasResponse || value.RawResponse is not null
                || !value.HasOverflowObservedExcessBytes
                || value.OverflowObservedExcessBytes != 1)
            || !oversized && value.HasOverflowObservedExcessBytes)
        {
            throw new InvalidDataException("A receipt without a response cannot fabricate provider usage.");
        }
        if (value.Outcome == HelperOutcomeV2.Completed
            && (!value.OutcomeHasResponse || value.RawResponse is null
                || new[] { value.InputTokens, value.OutputTokens, value.TotalTokens, value.ReasoningTokens,
                    value.CacheReadTokens, value.CacheWriteTokens, value.PricedToolCalls, value.CalculatedNanoUsd }
                    .Any(quantity => !IsAvailable(quantity))))
        {
            throw new InvalidDataException("A completed provider receipt requires bounded raw response and complete typed usage.");
        }
        // Assignment limits are pre-dispatch authority. Receipts are post-fact
        // evidence and must retain bounded overruns instead of erasing them.
        if (value.RawResponse is not null && (!ValidDigest(value.RawResponse)
                || value.RawResponse.SizeBytes > limits.MaximumResponseBytes
                || value.RawResponse.SizeBytes > limits.MaximumStagedOutputBytes)
            || IsAvailable(value.InputTokens) && value.InputTokens!.Value > 147_456
            || IsAvailable(value.OutputTokens) && value.OutputTokens!.Value > 8_192
            || IsAvailable(value.ReasoningTokens)
                && (!IsAvailable(value.OutputTokens) || value.ReasoningTokens!.Value > value.OutputTokens!.Value
                    || value.ReasoningTokens.Value > 8_192)
            || IsAvailable(value.TotalTokens)
                && (!IsAvailable(value.InputTokens) || !IsAvailable(value.OutputTokens)
                    || value.TotalTokens!.Value != checked(value.InputTokens!.Value + value.OutputTokens!.Value)
                    || value.TotalTokens.Value > 155_648)
            || IsAvailable(value.CacheReadTokens) && value.CacheReadTokens!.Value > 147_456
            || IsAvailable(value.CacheWriteTokens) && value.CacheWriteTokens!.Value > 147_456
            || IsAvailable(value.PricedToolCalls) && value.PricedToolCalls!.Value > 64
            || IsAvailable(value.CalculatedNanoUsd)
                && value.CalculatedNanoUsd!.Value > 1_200_000_000)
        {
            throw new InvalidDataException("Provider receipt usage, cache, tool, cost, or raw-response facts exceed absolute retained-evidence bounds.");
        }
    }

    private static void ValidateExpectedSubject<T>(
        T subjectCase,
        CredentialSubjectV2? credential,
        ProviderDispatchSubjectV2? providerDispatch,
        string? expectedProfileId,
        string? expectedGenerationId,
        string? expectedOperationId,
        string? expectedAttemptId,
        string path) where T : struct, Enum
    {
        if (credential is not null)
        {
            Require(expectedProfileId, $"expected_{path}.profile_id");
            Require(expectedGenerationId, $"expected_{path}.generation_id");
            if (credential.AccessProfileId?.Value != expectedProfileId
                || credential.GenerationId?.Value != expectedGenerationId)
            {
                throw new InvalidDataException($"{path} credential subject cross-rebound its expected profile generation.");
            }
        }
        else if (providerDispatch is not null)
        {
            Require(expectedOperationId, $"expected_{path}.operation_id");
            Require(expectedAttemptId, $"expected_{path}.attempt_id");
            if (providerDispatch.OperationId?.Value != expectedOperationId
                || providerDispatch.AttemptId?.Value != expectedAttemptId)
            {
                throw new InvalidDataException($"{path} dispatch subject cross-rebound its expected operation attempt.");
            }
        }
        else
        {
            throw new InvalidDataException($"{path} subject is absent.");
        }
    }

    private static bool SameLimits(HelperLimitsV2? value, HelperLimitsV2? expected) =>
        value is not null && expected is not null
        && value.MaximumFrameBytes == expected.MaximumFrameBytes
        && value.MaximumRequestBytes == expected.MaximumRequestBytes
        && value.MaximumResponseBytes == expected.MaximumResponseBytes
        && value.MaximumStagedOutputBytes == expected.MaximumStagedOutputBytes
        && value.MaximumInputTokens == expected.MaximumInputTokens
        && value.MaximumOutputTokens == expected.MaximumOutputTokens
        && value.MaximumCalculatedNanoUsd == expected.MaximumCalculatedNanoUsd
        && value.MaximumDuration?.Value == expected.MaximumDuration?.Value
        && value.MaximumDispatchCount == expected.MaximumDispatchCount;

    private static bool SameInstant(
        Infinium.Contracts.Protobuf.Common.V1.Instant? value,
        Infinium.Contracts.Protobuf.Common.V1.Instant? expected) =>
        value is not null && expected is not null
        && value.UnixSeconds == expected.UnixSeconds && value.Nanoseconds == expected.Nanoseconds;

    private static bool IsAcceptedInputProof(InputBoundProofV2? proof) =>
        proof is not null
        && proof.PolicyId == "openai-responses-o200k-byte-envelope"
        && proof.PolicyVersion == "v1"
        && proof.Status == InputBoundProofStatusV2.Proved;

    private static void ValidateSubject<T>(
        T subjectCase,
        CredentialSubjectV2? credential,
        ProviderDispatchSubjectV2? providerDispatch,
        string path) where T : struct, Enum
    {
        if (credential is not null)
        {
            Require(credential.AccessProfileId?.Value, path + ".credential.access_profile_id");
            Require(credential.GenerationId?.Value, path + ".credential.generation_id");
        }
        else if (providerDispatch is not null)
        {
            Require(providerDispatch.OperationId?.Value, path + ".provider_dispatch.operation_id");
            Require(providerDispatch.AttemptId?.Value, path + ".provider_dispatch.attempt_id");
        }
        else
        {
            throw new InvalidDataException(path + " subject must be explicit.");
        }
    }

    private static void RejectUnknownFields(IMessage message, string path)
    {
        FieldInfo? unknown = message.GetType().GetField("_unknownFields", BindingFlags.Instance | BindingFlags.NonPublic);
        if (unknown?.GetValue(message) is not null)
        {
            throw new InvalidDataException($"Unknown helper field at {path}.");
        }
        foreach (PropertyInfo property in message.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length != 0 || property.Name is "Descriptor" or "Parser")
            {
                continue;
            }
            object? item = property.GetValue(message);
            if (item is IMessage nested)
            {
                RejectUnknownFields(nested, path + "." + property.Name);
            }
            else if (item is IEnumerable sequence and not string and not ByteString)
            {
                foreach (object? element in sequence)
                {
                    if (element is IMessage nestedElement)
                    {
                        RejectUnknownFields(nestedElement, path + "." + property.Name);
                    }
                }
            }
        }
    }

    private static bool ValidDigest(Infinium.Contracts.Protobuf.Common.V1.ContentDigest? value) =>
        value is not null
        && value.Algorithm == Infinium.Contracts.Protobuf.Common.V1.DigestAlgorithm.Sha256
        && value.Value.Length == 32
        && value.SizeBytes > 0;

    private static bool ValidExactDigest(
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? value,
        ReadOnlySpan<byte> bytes) =>
        ValidDigest(value)
        && value!.SizeBytes == (ulong)bytes.Length
        && value.Value.Span.SequenceEqual(SHA256.HashData(bytes));

    private static bool SameDigest(
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? value,
        Infinium.Contracts.Protobuf.Common.V1.ContentDigest? expected) =>
        ValidDigest(value) && ValidDigest(expected)
        && value!.Algorithm == expected!.Algorithm
        && value.SizeBytes == expected.SizeBytes
        && value.Value.Span.SequenceEqual(expected.Value.Span);

    private static bool ValidInstant(Infinium.Contracts.Protobuf.Common.V1.Instant? value) =>
        value is not null && value.UnixSeconds > 0 && value.Nanoseconds is >= 0 and <= 999_999_999
        && value.Nanoseconds % 100 == 0;

    private static bool ValidFutureInstant(
        Infinium.Contracts.Protobuf.Common.V1.Instant? value,
        DateTimeOffset now) =>
        ValidInstant(value)
        && DateTimeOffset.FromUnixTimeSeconds(value!.UnixSeconds).AddTicks(value.Nanoseconds / 100) > now;

    private static ulong ElapsedHundredNanoseconds(
        Infinium.Contracts.Protobuf.Common.V1.Instant start,
        Infinium.Contracts.Protobuf.Common.V1.Instant end)
    {
        long seconds = checked(end.UnixSeconds - start.UnixSeconds);
        long nanos = checked((long)end.Nanoseconds - start.Nanoseconds);
        long totalNanos = checked(seconds * 1_000_000_000L + nanos);
        if (totalNanos <= 0)
        {
            throw new InvalidDataException("Dispatch deadline must follow its confirmed or evaluated instant.");
        }
        if (totalNanos % 100 != 0)
        {
            throw new InvalidDataException("Helper authority instants must use exact 100-nanosecond precision.");
        }
        return checked((ulong)(totalNanos / 100));
    }

    private static void ValidateOptionalUInt64(Infinium.Contracts.Protobuf.Common.V1.OptionalUInt64? value, string field)
    {
        if (value is null)
        {
            return;
        }
        if (!Enum.IsDefined(value.Availability)
            || value.Availability == Infinium.Contracts.Protobuf.Common.V1.AvailabilityState.Unspecified
            || (value.Availability != Infinium.Contracts.Protobuf.Common.V1.AvailabilityState.Available && value.Value != 0))
        {
            throw new InvalidDataException(field + " availability contradicts its value.");
        }
    }

    private static bool IsAvailable(Infinium.Contracts.Protobuf.Common.V1.OptionalUInt64? value) =>
        value?.Availability == Infinium.Contracts.Protobuf.Common.V1.AvailabilityState.Available;

    private static void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(name + " is required.");
        }
    }
}
