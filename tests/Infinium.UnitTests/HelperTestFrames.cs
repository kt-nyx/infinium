using System.Security.Cryptography;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Domain.V1;
using Infinium.Contracts.Protobuf.Helper.V2;

namespace Infinium.Tests;

public static class HelperTestFrames
{
    private static readonly long AuthorityEpoch = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    public static HelperPrivateFrameV2 Bootstrap(ulong sequence = 1, byte nonceSeed = 0) => new()
    {
        Sequence = sequence,
        ProtocolFingerprintSha256 = Fingerprint(),
        Bootstrap = new HelperBootstrapV2
        {
            CoordinatorFencingEpoch = 7,
            ExpiresAt = InstantAt(60),
            OneUseNonceFingerprintSha256 = ByteString.CopyFrom(Enumerable.Repeat(nonceSeed, 32).ToArray()),
            CommandId = "command-1",
            Credential = new CredentialSubjectV2
            {
                AccessProfileId = new ProviderAccessProfileId { Value = "profile-1" },
                GenerationId = new CredentialGenerationId { Value = "generation-1" },
            },
        },
    };

    public static HelperPrivateFrameV2 Assignment(
        HelperAssignmentKindV2 kind = HelperAssignmentKindV2.Enroll,
        ulong sequence = 2) => new()
        {
            Sequence = sequence,
            ProtocolFingerprintSha256 = Fingerprint(),
            Assignment = new HelperAssignmentV2
            {
                AccessProfileId = new ProviderAccessProfileId { Value = "profile-1" },
                GenerationId = new CredentialGenerationId { Value = "generation-1" },
                GenerationOrdinal = 1,
                RevocationEpoch = 0,
                AssignmentKind = kind,
                OperationKind = kind == HelperAssignmentKindV2.ProviderDispatch
                ? ProviderOperationKindV2.TransportQualification
                : ProviderOperationKindV2.Unspecified,
                AssignmentId = "assignment-1",
                CommandId = "command-1",
                Credential = new CredentialSubjectV2
                {
                    AccessProfileId = new ProviderAccessProfileId { Value = "profile-1" },
                    GenerationId = new CredentialGenerationId { Value = "generation-1" },
                },
            },
        };

    public static HelperPrivateFrameV2 DispatchBootstrap(byte nonceSeed = 0) => new()
    {
        Sequence = 1,
        ProtocolFingerprintSha256 = Fingerprint(),
        Bootstrap = new HelperBootstrapV2
        {
            CoordinatorFencingEpoch = 7,
            ExpiresAt = InstantAt(60),
            OneUseNonceFingerprintSha256 = ByteString.CopyFrom(Enumerable.Repeat(nonceSeed, 32).ToArray()),
            CommandId = "command-1",
            ProviderDispatch = new ProviderDispatchSubjectV2
            {
                OperationId = new OperationId { Value = "operation-1" },
                AttemptId = new AttemptId { Value = "attempt-1" },
            },
        },
    };

    public static HelperPrivateFrameV2 DispatchAssignment()
    {
        HelperPrivateFrameV2 frame = Assignment(HelperAssignmentKindV2.ProviderDispatch);
        frame.Assignment.ProviderDispatch = new ProviderDispatchSubjectV2
        {
            OperationId = new OperationId { Value = "operation-1" },
            AttemptId = new AttemptId { Value = "attempt-1" },
        };
        frame.Assignment.ProviderRequest = new ProviderRequestV2
        {
            DispatchId = new DispatchId { Value = "dispatch-1" },
            CanonicalRequestBytes = ByteString.CopyFromUtf8("{}"),
            CanonicalRequest = Digest("{}"u8),
            CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
            PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
            ReservationGroupId = new ReservationGroupId { Value = "reservation-1" },
            DispatchDeadline = InstantAt(30),
            EndpointIdentity = ProviderEndpointV2.OpenaiResponses,
            InputBoundProof = new InputBoundProofV2
            {
                PolicyId = "openai-responses-o200k-byte-envelope",
                PolicyVersion = "v1",
                Status = InputBoundProofStatusV2.Proved,
            },
            RequestId = "request-1",
            ConfirmedAt = InstantAt(1),
            RequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData("{}"u8)),
        };
        frame.Assignment.AccountIdentityId = new ProviderAccountIdentityId { Value = "account-1" };
        frame.Assignment.BillingScopeIdentityId = new BillingScopeIdentityId { Value = "billing-1" };
        frame.Assignment.EffectiveConfigurationId = "configuration-1";
        frame.Assignment.Settings = Digest("settings"u8);
        frame.Assignment.OutputSchema = Digest("schema"u8);
        frame.Assignment.Limits = new HelperLimitsV2
        {
            MaximumFrameBytes = HelperProtocolV2Constants.MaximumFrameBytes,
            MaximumRequestBytes = 16_384,
            MaximumResponseBytes = 262_144,
            MaximumStagedOutputBytes = 262_144,
            MaximumInputTokens = 20_480,
            MaximumOutputTokens = 256,
            MaximumCalculatedNanoUsd = 140_000_000,
            MaximumDuration = new DurationMillis { Value = 60_000 },
            MaximumDispatchCount = 1,
        };
        return frame;
    }

    public static HelperPrivateFrameV2 Revalidation(ulong sequence = 3) => new()
    {
        Sequence = sequence,
        ProtocolFingerprintSha256 = Fingerprint(),
        DispatchRevalidation = new DispatchRevalidationV2
        {
            DispatchId = new DispatchId { Value = "dispatch-1" },
            AttemptId = new AttemptId { Value = "attempt-1" },
            CoordinatorFencingEpoch = 7,
            AccessProfileId = new ProviderAccessProfileId { Value = "profile-1" },
            GenerationId = new CredentialGenerationId { Value = "generation-1" },
            RevocationEpoch = 0,
            ReservationGroupId = new ReservationGroupId { Value = "reservation-1" },
            CanonicalRequest = Digest("{}"u8),
            AuthorizedOnce = true,
            Disposition = DispatchDispositionV2.Authorized,
            AccountIdentityId = new ProviderAccountIdentityId { Value = "account-1" },
            BillingScopeIdentityId = new BillingScopeIdentityId { Value = "billing-1" },
            EffectiveConfigurationId = "configuration-1",
            CapabilitySnapshotId = new CapabilitySnapshotId { Value = "capability-1" },
            PriceSnapshotId = new PriceSnapshotId { Value = "price-1" },
            Settings = Digest("settings"u8),
            OutputSchema = Digest("schema"u8),
            OperationKind = ProviderOperationKindV2.TransportQualification,
            InputBoundProof = new InputBoundProofV2
            {
                PolicyId = "openai-responses-o200k-byte-envelope",
                PolicyVersion = "v1",
                Status = InputBoundProofStatusV2.Proved,
            },
            EvaluatedAt = InstantAt(2),
            DispatchDeadline = InstantAt(30),
            RequestId = "request-1",
            OperationId = new OperationId { Value = "operation-1" },
            RequestFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData("{}"u8)),
            Limits = DispatchAssignment().Assignment.Limits.Clone(),
        },
    };

    public static Instant InstantAt(long seconds) => new() { UnixSeconds = AuthorityEpoch + seconds, Nanoseconds = 0 };
    public static ByteString Fingerprint() =>
        ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256));

    private static ContentDigest Digest(ReadOnlySpan<byte> value) => new()
    {
        Algorithm = DigestAlgorithm.Sha256,
        Value = ByteString.CopyFrom(SHA256.HashData(value)),
        SizeBytes = checked((ulong)value.Length),
    };
}
