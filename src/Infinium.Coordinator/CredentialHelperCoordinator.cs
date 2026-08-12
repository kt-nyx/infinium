using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
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
            bootstrap, assignment, finalRevalidation, TimeSpan.FromSeconds(30), cancellationToken)
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
        HelperStagingReceipt staging = store.StageAndAdmitHelperReceipt(attemptId, canonical, now);
        return new(process, staging);
    }

    private static Google.Protobuf.ByteString ProtocolFingerprint() =>
        Google.Protobuf.ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256));
}
