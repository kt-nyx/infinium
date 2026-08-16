using System.Security.Cryptography;
using Google.Protobuf;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Helper.V2;

namespace Infinium.Application.Runtime;

public static class HelperExecutionSemanticsV2
{
    public static void ValidateBootstrapAndAssignment(
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment)
    {
        ArgumentNullException.ThrowIfNull(bootstrap);
        ArgumentNullException.ThrowIfNull(assignment);
        Require(bootstrap.CommandId, "bootstrap.command_id");
        Require(assignment.CommandId, "assignment.command_id");
        Require(assignment.AssignmentId, "assignment.assignment_id");
        Require(assignment.AccessProfileId?.Value, "assignment.access_profile_id");
        Require(assignment.GenerationId?.Value, "assignment.generation_id");
        if (bootstrap.CoordinatorFencingEpoch == 0 || bootstrap.OneUseNonceFingerprintSha256.Length != 32
            || !ValidInstant(bootstrap.ExpiresAt) || bootstrap.CommandId != assignment.CommandId
            || assignment.GenerationOrdinal == 0 || assignment.AssignmentKind == HelperAssignmentKindV2.Unspecified)
        {
            throw new InvalidDataException("The helper bootstrap and immutable assignment are incomplete or cross-bound.");
        }

        bool dispatch = assignment.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch;
        if (dispatch)
        {
            ValidateDispatchBootstrap(bootstrap, assignment);
            ValidateProviderRequest(assignment);
        }
        else
        {
            ValidateCredentialBootstrap(bootstrap, assignment);
        }
    }

    public static void ValidateFinalRevalidation(
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        DispatchRevalidationV2 value)
    {
        ValidateBootstrapAndAssignment(bootstrap, assignment);
        ProviderRequestV2 request = assignment.ProviderRequest
            ?? throw new InvalidDataException("A final revalidation requires one provider request.");
        if (!value.AuthorizedOnce || value.Disposition != DispatchDispositionV2.Authorized
            || value.CoordinatorFencingEpoch != bootstrap.CoordinatorFencingEpoch
            || value.OperationId?.Value != assignment.ProviderDispatch?.OperationId?.Value
            || value.AttemptId?.Value != assignment.ProviderDispatch?.AttemptId?.Value
            || value.AccessProfileId?.Value != assignment.AccessProfileId?.Value
            || value.GenerationId?.Value != assignment.GenerationId?.Value
            || value.RevocationEpoch != assignment.RevocationEpoch
            || value.DispatchId?.Value != request.DispatchId?.Value
            || value.ReservationGroupId?.Value != request.ReservationGroupId?.Value
            || value.RequestId != request.RequestId
            || value.RequestFingerprintSha256.Length != 32
            || !value.RequestFingerprintSha256.Span.SequenceEqual(request.RequestFingerprintSha256.Span)
            || !SameDigest(value.CanonicalRequest, request.CanonicalRequest)
            || value.AccountIdentityId?.Value != assignment.AccountIdentityId?.Value
            || value.BillingScopeIdentityId?.Value != assignment.BillingScopeIdentityId?.Value
            || value.EffectiveConfigurationId != assignment.EffectiveConfigurationId
            || value.CapabilitySnapshotId?.Value != request.CapabilitySnapshotId?.Value
            || value.PriceSnapshotId?.Value != request.PriceSnapshotId?.Value
            || !SameDigest(value.Settings, assignment.Settings)
            || !SameDigest(value.OutputSchema, assignment.OutputSchema)
            || value.OperationKind != assignment.OperationKind
            || !SameProof(value.InputBoundProof, request.InputBoundProof)
            || !SameInstant(value.DispatchDeadline, request.DispatchDeadline)
            || !SameLimits(value.Limits, assignment.Limits)
            || !ValidInstant(value.EvaluatedAt)
            || ToTimestamp(value.EvaluatedAt) > ToTimestamp(value.DispatchDeadline)
            || ToTimestamp(value.EvaluatedAt) > ToTimestamp(bootstrap.ExpiresAt))
        {
            throw new InvalidDataException("The helper final dispatch revalidation is stale, expired, over budget, or cross-bound.");
        }
    }

    private static void ValidateCredentialBootstrap(HelperBootstrapV2 bootstrap, HelperAssignmentV2 assignment)
    {
        bool freshGenerationOperation = assignment.AssignmentKind is HelperAssignmentKindV2.Replace
            or HelperAssignmentKindV2.Recover;
        if (bootstrap.SubjectCase != HelperBootstrapV2.SubjectOneofCase.Credential
            || assignment.SubjectCase != HelperAssignmentV2.SubjectOneofCase.Credential
            || bootstrap.Credential.AccessProfileId?.Value != assignment.AccessProfileId?.Value
            || (!freshGenerationOperation
                && bootstrap.Credential.GenerationId?.Value != assignment.GenerationId?.Value)
            || assignment.Credential.AccessProfileId?.Value != assignment.AccessProfileId?.Value
            || assignment.Credential.GenerationId?.Value != assignment.GenerationId?.Value
            || assignment.ProviderRequest is not null || assignment.Limits is not null
            || assignment.OperationKind != ProviderOperationKindV2.Unspecified
            || assignment.AccountIdentityId is not null || assignment.BillingScopeIdentityId is not null
            || !string.IsNullOrEmpty(assignment.EffectiveConfigurationId)
            || assignment.Settings is not null || assignment.OutputSchema is not null
            || assignment.AssignmentKind is not (HelperAssignmentKindV2.Enroll or HelperAssignmentKindV2.Replace
                or HelperAssignmentKindV2.Verify or HelperAssignmentKindV2.Recover
                or HelperAssignmentKindV2.Disable or HelperAssignmentKindV2.Delete))
        {
            throw new InvalidDataException("A credential assignment reinterprets its bootstrap or fabricates provider authority.");
        }
    }

    private static void ValidateDispatchBootstrap(HelperBootstrapV2 bootstrap, HelperAssignmentV2 assignment)
    {
        if (bootstrap.SubjectCase != HelperBootstrapV2.SubjectOneofCase.ProviderDispatch
            || assignment.SubjectCase != HelperAssignmentV2.SubjectOneofCase.ProviderDispatch
            || bootstrap.ProviderDispatch.OperationId?.Value != assignment.ProviderDispatch.OperationId?.Value
            || bootstrap.ProviderDispatch.AttemptId?.Value != assignment.ProviderDispatch.AttemptId?.Value
            || string.IsNullOrWhiteSpace(assignment.AccountIdentityId?.Value)
            || string.IsNullOrWhiteSpace(assignment.BillingScopeIdentityId?.Value)
            || string.IsNullOrWhiteSpace(assignment.EffectiveConfigurationId)
            || !ValidDigest(assignment.Settings) || !ValidDigest(assignment.OutputSchema))
        {
            throw new InvalidDataException("A provider assignment reinterprets its bootstrap or omits authority bindings.");
        }
    }

    private static void ValidateProviderRequest(HelperAssignmentV2 assignment)
    {
        ProviderRequestV2 request = assignment.ProviderRequest
            ?? throw new InvalidDataException("A provider assignment requires one closed request.");
        Require(request.DispatchId?.Value, "provider_request.dispatch_id");
        Require(request.CapabilitySnapshotId?.Value, "provider_request.capability_snapshot_id");
        Require(request.PriceSnapshotId?.Value, "provider_request.price_snapshot_id");
        Require(request.ReservationGroupId?.Value, "provider_request.reservation_group_id");
        Require(request.RequestId, "provider_request.request_id");
        if (request.EndpointIdentity != ProviderEndpointV2.OpenaiResponses
            || request.CanonicalRequestBytes.IsEmpty
            || !ValidExactDigest(request.CanonicalRequest, request.CanonicalRequestBytes.Span)
            || request.RequestFingerprintSha256.Length != 32
            || !request.RequestFingerprintSha256.Span.SequenceEqual(request.CanonicalRequest.Value.Span)
            || !ValidInstant(request.ConfirmedAt) || !ValidInstant(request.DispatchDeadline)
            || ToTimestamp(request.ConfirmedAt) > ToTimestamp(request.DispatchDeadline)
            || !SameProof(request.InputBoundProof, AcceptedProof)
            || assignment.Limits is null
            || (ulong)request.CanonicalRequestBytes.Length > assignment.Limits.MaximumRequestBytes)
        {
            throw new InvalidDataException("The helper provider request is not canonical, bounded, or proved.");
        }
        ValidateLimits(assignment.OperationKind, assignment.Limits);
    }

    private static readonly InputBoundProofV2 AcceptedProof = new()
    {
        PolicyId = "openai-responses-o200k-byte-envelope",
        PolicyVersion = "v2",
        Status = InputBoundProofStatusV2.Proved,
    };

    private static void ValidateLimits(ProviderOperationKindV2 kind, HelperLimitsV2 value)
    {
        (ulong request, ulong input, ulong output, ulong response, long cost, ulong duration) = kind switch
        {
            ProviderOperationKindV2.TransportQualification => (16_384UL, 20_480UL, 256UL, 262_144UL, 140_000_000L, 60_000UL),
            ProviderOperationKindV2.SourceClaimExtraction or ProviderOperationKindV2.CandidateInvestigation =>
                (65_536UL, 73_728UL, 4_096UL, 1_048_576UL, 600_000_000L, 120_000UL),
            _ => throw new InvalidDataException("The helper provider operation kind is unknown."),
        };
        if (value.MaximumFrameBytes is 0 or > HelperProtocolV2Constants.MaximumFrameBytes
            || value.MaximumRequestBytes is 0 || value.MaximumRequestBytes > request
            || value.MaximumResponseBytes is 0 || value.MaximumResponseBytes > response
            || value.MaximumStagedOutputBytes is 0 || value.MaximumStagedOutputBytes > response
            || value.MaximumInputTokens is 0 || value.MaximumInputTokens > input
            || value.MaximumOutputTokens is 0 || value.MaximumOutputTokens > output
            || value.MaximumCalculatedNanoUsd is <= 0 || value.MaximumCalculatedNanoUsd > cost
            || value.MaximumDuration is null || value.MaximumDuration.Value is 0 || value.MaximumDuration.Value > duration
            || value.MaximumDispatchCount != 1)
        {
            throw new InvalidDataException("The helper provider limits exceed their operation-specific ceiling.");
        }
    }

    private static bool ValidExactDigest(ContentDigest? digest, ReadOnlySpan<byte> value) =>
        ValidDigest(digest) && digest!.SizeBytes == (ulong)value.Length
        && digest.Value.Span.SequenceEqual(SHA256.HashData(value));

    private static bool ValidDigest(ContentDigest? value) => value is not null
        && value.Algorithm == DigestAlgorithm.Sha256 && value.Value.Length == 32;

    private static bool SameDigest(ContentDigest? left, ContentDigest? right) =>
        ValidDigest(left) && ValidDigest(right) && left!.SizeBytes == right!.SizeBytes
        && left.Value.Span.SequenceEqual(right.Value.Span);

    private static bool SameProof(InputBoundProofV2? left, InputBoundProofV2? right) => left is not null && right is not null
        && left.PolicyId == right.PolicyId && left.PolicyVersion == right.PolicyVersion && left.Status == right.Status;

    private static bool SameLimits(HelperLimitsV2? left, HelperLimitsV2? right) => left is not null && right is not null
        && left.MaximumFrameBytes == right.MaximumFrameBytes && left.MaximumRequestBytes == right.MaximumRequestBytes
        && left.MaximumResponseBytes == right.MaximumResponseBytes
        && left.MaximumStagedOutputBytes == right.MaximumStagedOutputBytes
        && left.MaximumInputTokens == right.MaximumInputTokens && left.MaximumOutputTokens == right.MaximumOutputTokens
        && left.MaximumCalculatedNanoUsd == right.MaximumCalculatedNanoUsd
        && left.MaximumDuration?.Value == right.MaximumDuration?.Value
        && left.MaximumDispatchCount == right.MaximumDispatchCount;

    private static bool SameInstant(Instant? left, Instant? right) => left is not null && right is not null
        && left.UnixSeconds == right.UnixSeconds && left.Nanoseconds == right.Nanoseconds;

    private static bool ValidInstant(Instant? value) => value is not null
        && value.Nanoseconds is >= 0 and <= 999_999_999;

    private static DateTimeOffset ToTimestamp(Instant? value)
    {
        if (!ValidInstant(value))
        {
            throw new InvalidDataException("The helper timestamp is invalid.");
        }
        return DateTimeOffset.FromUnixTimeSeconds(value!.UnixSeconds).AddTicks(value.Nanoseconds / 100);
    }

    private static void Require(string? value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"The helper field {path} is required.");
        }
    }
}
