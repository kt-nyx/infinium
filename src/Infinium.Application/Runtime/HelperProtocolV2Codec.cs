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
        string? expectedCommandId = null)
    {
        if (bytes.IsEmpty || bytes.Length > HelperProtocolV2Constants.MaximumFrameBytes)
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
        Validate(frame, now, expectedAssignmentId, expectedCommandId);
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
        string? expectedCommandId)
    {
        if (frame.Sequence == 0
            || frame.ProtocolFingerprintSha256.Length != 32
            || !frame.ProtocolFingerprintSha256.Span.SequenceEqual(
                Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)))
        {
            throw new InvalidDataException("Helper v2 frame identity is absent or mismatched.");
        }
        switch (frame.PayloadCase)
        {
            case HelperPrivateFrameV2.PayloadOneofCase.Bootstrap:
                Require(frame.Bootstrap.OperationId?.Value, "bootstrap.operation_id");
                Require(frame.Bootstrap.AttemptId?.Value, "bootstrap.attempt_id");
                if (frame.Bootstrap.CoordinatorFencingEpoch == 0 || frame.Bootstrap.OneUseNonceFingerprintSha256.Length != 32
                    || !ValidFutureInstant(frame.Bootstrap.ExpiresAt, now))
                {
                    throw new InvalidDataException("Helper v2 bootstrap is incomplete.");
                }
                break;
            case HelperPrivateFrameV2.PayloadOneofCase.Assignment:
                Validate(frame.Assignment, now);
                break;
            case HelperPrivateFrameV2.PayloadOneofCase.DispatchRevalidation:
                Validate(frame.DispatchRevalidation, now);
                break;
            case HelperPrivateFrameV2.PayloadOneofCase.Receipt:
                Validate(frame.Receipt, expectedAssignmentId, expectedCommandId);
                break;
            default:
                throw new InvalidDataException("Helper v2 payload kind must be explicit.");
        }
    }

    private static void Validate(HelperAssignmentV2 value, DateTimeOffset now)
    {
        Require(value.OperationId?.Value, "assignment.operation_id");
        Require(value.AttemptId?.Value, "assignment.attempt_id");
        Require(value.AccessProfileId?.Value, "assignment.access_profile_id");
        Require(value.GenerationId?.Value, "assignment.generation_id");
        Require(value.AssignmentId, "assignment.assignment_id");
        Require(value.CommandId, "assignment.command_id");
        if (!Enum.IsDefined(value.AssignmentKind) || value.AssignmentKind == HelperAssignmentKindV2.Unspecified
            || value.GenerationOrdinal == 0)
        {
            throw new InvalidDataException("Helper v2 assignment uses an unknown numeric state or incomplete binding.");
        }
        bool dispatch = value.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch;
        if (dispatch != (value.ProviderRequest is not null))
        {
            throw new InvalidDataException("Only a provider-dispatch assignment may carry a provider request.");
        }
        if (dispatch)
        {
            if (!Enum.IsDefined(value.OperationKind) || value.OperationKind == ProviderOperationKindV2.Unspecified
                || value.Limits is null)
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
                || !ValidFutureInstant(request.DispatchDeadline, now)
                || !IsAuthorityRequiredProof(request.InputBoundProof))
            {
                throw new InvalidDataException("Helper v2 provider request is not a closed bounded and explicitly blocked Responses request.");
            }
            throw new NotSupportedException("Helper provider dispatch assignment is blocked pending accepted local tokenizer/framing authority.");
        }
        else if (value.ProviderRequest is not null || value.Limits is not null
            || value.OperationKind != ProviderOperationKindV2.Unspecified)
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

    private static void Validate(DispatchRevalidationV2 value, DateTimeOffset now)
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
        if (!Enum.IsDefined(value.Disposition) || value.Disposition == DispatchDispositionV2.Unspecified
            || !Enum.IsDefined(value.OperationKind) || value.OperationKind == ProviderOperationKindV2.Unspecified
            || value.CoordinatorFencingEpoch == 0 || !ValidDigest(value.CanonicalRequest)
            || !ValidDigest(value.Settings) || !ValidDigest(value.OutputSchema)
            || !ValidFutureInstant(value.DispatchDeadline, now) || value.Limits is null
            || value.AuthorizedOnce || value.Disposition == DispatchDispositionV2.Authorized
            || !IsAuthorityRequiredProof(value.InputBoundProof))
        {
            throw new InvalidDataException("Helper v2 final revalidation is incomplete or internally contradictory.");
        }
        ValidateLimits(value.OperationKind, value.Limits);
    }

    private static void Validate(
        HelperReceiptV2 value,
        string? expectedAssignmentId,
        string? expectedCommandId)
    {
        Require(value.OperationId?.Value, "receipt.operation_id");
        Require(value.AttemptId?.Value, "receipt.attempt_id");
        if (!Enum.IsDefined(value.Outcome) || value.Outcome == HelperOutcomeV2.Unspecified
            || !Enum.IsDefined(value.AssignmentKind) || value.AssignmentKind == HelperAssignmentKindV2.Unspecified)
        {
            throw new InvalidDataException("Helper v2 receipt outcome is unknown.");
        }
        bool dispatch = value.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch;
        Require(value.AssignmentId, "receipt.assignment_id");
        Require(value.CommandId, "receipt.command_id");
        Require(expectedAssignmentId, "expected_receipt.assignment_id");
        Require(expectedCommandId, "expected_receipt.command_id");
        ValidateOptionalUInt64(value.InputTokens, "receipt.input_tokens");
        ValidateOptionalUInt64(value.OutputTokens, "receipt.output_tokens");
        ValidateOptionalUInt64(value.ReasoningTokens, "receipt.reasoning_tokens");
        ValidateOptionalUInt64(value.CacheReadTokens, "receipt.cache_read_tokens");
        ValidateOptionalUInt64(value.CacheWriteTokens, "receipt.cache_write_tokens");
        bool noTransport = value.Outcome is HelperOutcomeV2.Unavailable or HelperOutcomeV2.Cancelled;
        bool hasResponse = value.RawResponse is not null;
        if (value.AssignmentId != expectedAssignmentId || value.CommandId != expectedCommandId
            || value.OutcomeHasResponse != hasResponse
            || (hasResponse && !ValidDigest(value.RawResponse))
            || (!dispatch && (value.TransportMayHaveStarted || hasResponse
                || value.InputTokens is not null || value.OutputTokens is not null || value.ReasoningTokens is not null
                || value.CacheReadTokens is not null || value.CacheWriteTokens is not null
                || !string.IsNullOrEmpty(value.RequestId) || value.DispatchId is not null || value.InputBoundProof is not null))
            || (!dispatch && value.Outcome is HelperOutcomeV2.TransportMayHaveStarted
                or HelperOutcomeV2.Oversized or HelperOutcomeV2.Malformed)
            || (noTransport && (value.TransportMayHaveStarted || hasResponse))
            || !ValidDigest(value.NonSecretReceipt))
        {
            throw new InvalidDataException("Helper v2 receipt outcome contradicts transport or response evidence.");
        }
        if (dispatch)
        {
            Require(value.RequestId, "receipt.request_id");
            Require(value.DispatchId?.Value, "receipt.dispatch_id");
            if (!IsAuthorityRequiredProof(value.InputBoundProof))
            {
                throw new InvalidDataException("Provider receipt must retain the exact blocked assignment, request, fence, and proof binding.");
            }
            throw new NotSupportedException("Provider dispatch receipts are unreachable until accepted local input-bound authority changes the helper contract.");
        }
    }

    private static bool IsAuthorityRequiredProof(InputBoundProofV2? proof) =>
        proof is not null
        && proof.PolicyId == "unresolved-openai-responses-framing"
        && proof.PolicyVersion == "authority-required"
        && proof.Status == InputBoundProofStatusV2.AuthorityRequired;

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

    private static bool ValidInstant(Infinium.Contracts.Protobuf.Common.V1.Instant? value) =>
        value is not null && value.UnixSeconds > 0 && value.Nanoseconds is >= 0 and <= 999_999_999;

    private static bool ValidFutureInstant(
        Infinium.Contracts.Protobuf.Common.V1.Instant? value,
        DateTimeOffset now) =>
        ValidInstant(value)
        && DateTimeOffset.FromUnixTimeSeconds(value!.UnixSeconds).AddTicks(value.Nanoseconds / 100) > now;

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

    private static void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(name + " is required.");
        }
    }
}
