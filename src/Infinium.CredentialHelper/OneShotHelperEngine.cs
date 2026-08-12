using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Helper.V2;

namespace Infinium.CredentialHelper;

public sealed class OneShotHelperEngine
{
    private readonly ISyntheticSecureStore store;
    private readonly TimeProvider timeProvider;

    public OneShotHelperEngine(ISyntheticSecureStore store, TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task RunAsync(Stream request, Stream response, CancellationToken cancellationToken)
    {
        HelperPrivateSessionV2 session = new();
        HelperPrivateFrameV2 bootstrap = await HelperPrivateProtocolV2.ReadAsync(request, 1, cancellationToken);
        session.Admit(bootstrap);
        HelperPrivateFrameV2 assignmentFrame = await HelperPrivateProtocolV2.ReadAsync(request, 2, cancellationToken);
        session.Admit(assignmentFrame);
        HelperAssignmentV2 assignment = assignmentFrame.Assignment;
        HelperExecutionSemanticsV2.ValidateBootstrapAndAssignment(bootstrap.Bootstrap, assignment);
        DateTimeOffset authoritativeNow = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = DateTimeOffset.FromUnixTimeSeconds(bootstrap.Bootstrap.ExpiresAt.UnixSeconds)
            .AddTicks(bootstrap.Bootstrap.ExpiresAt.Nanoseconds / 100);
        if (expiresAt <= authoritativeNow
            || !store.ConsumeOneUseNonce(bootstrap.Bootstrap.OneUseNonceFingerprintSha256.Span))
        {
            throw new InvalidDataException("The helper bootstrap is expired or its launch nonce was already consumed.");
        }

        DispatchRevalidationV2? revalidation = null;
        if (assignment.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch)
        {
            HelperPrivateFrameV2 revalidationFrame = await HelperPrivateProtocolV2.ReadAsync(request, 3, cancellationToken);
            session.Admit(revalidationFrame);
            revalidation = revalidationFrame.DispatchRevalidation;
        }

        HelperReceiptV2 receipt;
        byte[] stagedResponse = [];
        try
        {
            if (revalidation is not null)
            {
                HelperExecutionSemanticsV2.ValidateFinalRevalidation(bootstrap.Bootstrap, assignment, revalidation);
            }
            (receipt, stagedResponse) = Execute(bootstrap.Bootstrap, assignment, revalidation);
        }
        catch (InvalidDataException)
        {
            receipt = CreateRejectedReceipt(assignment, revalidation);
        }
        ulong terminalSequence = assignment.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch ? 4UL : 3UL;
        HelperPrivateFrameV2 terminal = HelperFrameFactory.Create(terminalSequence, receipt);
        session.Admit(terminal);
        await HelperPrivateProtocolV2.WriteAsync(response, terminal, cancellationToken);
        await WriteStagedResponseAsync(response, stagedResponse, cancellationToken);
    }

    private static HelperReceiptV2 CreateRejectedReceipt(
        HelperAssignmentV2 assignment,
        DispatchRevalidationV2? revalidation) => CreateReceipt(
            assignment, revalidation, HelperOutcomeV2.FailedKnown, hasResponse: false);

    private (HelperReceiptV2 Receipt, byte[] StagedResponse) Execute(
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        DispatchRevalidationV2? revalidation)
    {
        SyntheticCredentialSlot slot = new(assignment.AccessProfileId.Value, assignment.GenerationId.Value);
        SyntheticCredentialSlot bootstrapSlot = new(
            bootstrap.Credential?.AccessProfileId?.Value ?? assignment.AccessProfileId.Value,
            bootstrap.Credential?.GenerationId?.Value ?? assignment.GenerationId.Value);
        HelperOutcomeV2 outcome;
        bool hasResponse = false;
        byte[]? secret = null;
        try
        {
            switch (assignment.AssignmentKind)
            {
                case HelperAssignmentKindV2.Enroll:
                    secret = Encoding.UTF8.GetBytes("WP3-REAL-CHILD-SECRET-CANARY/" + assignment.AssignmentId);
                    store.WriteExact(slot, secret);
                    outcome = store.VerifyExact(slot) ? HelperOutcomeV2.Completed : HelperOutcomeV2.FailedKnown;
                    break;
                case HelperAssignmentKindV2.Replace:
                    if (bootstrapSlot == slot)
                    {
                        throw new InvalidDataException("Replacement requires an exact predecessor and fresh successor generation.");
                    }
                    secret = Encoding.UTF8.GetBytes("WP3-REAL-CHILD-SECRET-CANARY/" + assignment.AssignmentId);
                    store.WriteExact(slot, secret);
                    outcome = store.VerifyExact(slot) && store.DeleteExact(bootstrapSlot)
                        && !store.VerifyExact(bootstrapSlot)
                        ? HelperOutcomeV2.Completed
                        : HelperOutcomeV2.FailedKnown;
                    break;
                case HelperAssignmentKindV2.Verify:
                    outcome = store.VerifyExact(slot) ? HelperOutcomeV2.Completed : HelperOutcomeV2.FailedKnown;
                    break;
                case HelperAssignmentKindV2.Recover:
                    if (bootstrapSlot == slot)
                    {
                        outcome = store.VerifyExact(slot) ? HelperOutcomeV2.Completed : HelperOutcomeV2.FailedKnown;
                    }
                    else
                    {
                        secret = Encoding.UTF8.GetBytes("WP3-REAL-CHILD-SECRET-CANARY/" + assignment.AssignmentId);
                        store.WriteExact(slot, secret);
                        outcome = store.VerifyExact(slot) ? HelperOutcomeV2.Completed : HelperOutcomeV2.FailedKnown;
                    }
                    break;
                case HelperAssignmentKindV2.Disable:
                    outcome = HelperOutcomeV2.Completed;
                    break;
                case HelperAssignmentKindV2.Delete:
                    _ = store.DeleteExact(slot);
                    outcome = !store.VerifyExact(slot) ? HelperOutcomeV2.Completed : HelperOutcomeV2.FailedKnown;
                    break;
                case HelperAssignmentKindV2.ProviderDispatch:
                    secret = store.ReadExact(slot);
                    // WP3 qualifies the boundary only. The deterministic response
                    // is represented by a non-secret digest and never reaches a network.
                    hasResponse = true;
                    outcome = HelperOutcomeV2.Completed;
                    break;
                default:
                    throw new InvalidDataException("The helper assignment kind is unsupported.");
            }
        }
        catch (IOException)
        {
            outcome = HelperOutcomeV2.Unavailable;
        }
        catch (InvalidDataException)
        {
            outcome = HelperOutcomeV2.Oversized;
        }
        catch (KeyNotFoundException)
        {
            outcome = HelperOutcomeV2.FailedKnown;
        }
        finally
        {
            if (secret is not null)
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }

        HelperReceiptV2 receipt = CreateReceipt(assignment, revalidation, outcome, hasResponse);
        byte[] stagedResponse = [];
        if (hasResponse)
        {
            stagedResponse = Encoding.UTF8.GetBytes("synthetic-provider-response/" + assignment.AssignmentId);
            if ((ulong)stagedResponse.Length > assignment.Limits.MaximumResponseBytes
                || (ulong)stagedResponse.Length > assignment.Limits.MaximumStagedOutputBytes)
            {
                throw new InvalidDataException("The deterministic response exceeds its retained response/staging bound.");
            }
            receipt.RawResponse = Digest(stagedResponse);
            receipt.InputTokens = Available(0);
            receipt.OutputTokens = Available(0);
            receipt.TotalTokens = Available(0);
            receipt.ReasoningTokens = Available(0);
            receipt.CacheReadTokens = Available(0);
            receipt.CacheWriteTokens = Available(0);
            receipt.PricedToolCalls = Available(0);
            receipt.CalculatedNanoUsd = Available(0);
            receipt.DispatchCount = Available(1);
        }
        return (receipt, stagedResponse);
    }

    private static async Task WriteStagedResponseAsync(
        Stream response,
        byte[] stagedResponse,
        CancellationToken cancellationToken)
    {
        byte[] prefix = BitConverter.GetBytes(checked((uint)stagedResponse.Length));
        await response.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        if (stagedResponse.Length > 0)
        {
            await response.WriteAsync(stagedResponse, cancellationToken).ConfigureAwait(false);
        }
        await response.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static HelperReceiptV2 CreateReceipt(
        HelperAssignmentV2 assignment,
        DispatchRevalidationV2? revalidation,
        HelperOutcomeV2 outcome,
        bool hasResponse)
    {
        HelperReceiptV2 receipt = new()
        {
            Outcome = outcome,
            TransportMayHaveStarted = assignment.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch
                && (hasResponse || outcome == HelperOutcomeV2.TransportMayHaveStarted),
            AssignmentKind = assignment.AssignmentKind,
            AssignmentId = assignment.AssignmentId,
            RequestId = assignment.ProviderRequest?.RequestId ?? string.Empty,
            DispatchId = assignment.ProviderRequest?.DispatchId?.Clone(),
            InputBoundProof = assignment.ProviderRequest?.InputBoundProof?.Clone(),
            OutcomeHasResponse = hasResponse,
            CommandId = assignment.CommandId,
            RequestFingerprintSha256 = assignment.ProviderRequest?.RequestFingerprintSha256 ?? ByteString.Empty,
            CoordinatorFencingEpoch = revalidation?.CoordinatorFencingEpoch ?? 0,
            CapabilitySnapshotId = assignment.ProviderRequest?.CapabilitySnapshotId?.Clone(),
            PriceSnapshotId = assignment.ProviderRequest?.PriceSnapshotId?.Clone(),
            Settings = assignment.Settings?.Clone(),
            OutputSchema = assignment.OutputSchema?.Clone(),
            EffectiveConfigurationId = assignment.EffectiveConfigurationId,
            RevocationEpoch = assignment.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch
                ? assignment.RevocationEpoch
                : 0,
            AccountIdentityId = assignment.AccountIdentityId?.Clone(),
            BillingScopeIdentityId = assignment.BillingScopeIdentityId?.Clone(),
            ReservationGroupId = assignment.ProviderRequest?.ReservationGroupId?.Clone(),
            OperationKind = assignment.OperationKind,
            Limits = assignment.Limits?.Clone(),
            DispatchDeadline = assignment.ProviderRequest?.DispatchDeadline?.Clone(),
            UsageReceiptState = hasResponse ? UsageReceiptStateV2.Complete : UsageReceiptStateV2.NotDispatched,
            NonSecretReceipt = Digest(Encoding.UTF8.GetBytes(
                $"{assignment.AssignmentId}/{assignment.CommandId}/{outcome}")),
        };
        if (assignment.SubjectCase == HelperAssignmentV2.SubjectOneofCase.Credential)
        {
            receipt.Credential = assignment.Credential.Clone();
        }
        else if (assignment.SubjectCase == HelperAssignmentV2.SubjectOneofCase.ProviderDispatch)
        {
            receipt.ProviderDispatch = assignment.ProviderDispatch.Clone();
        }
        return receipt;
    }

    private static ContentDigest Digest(ReadOnlySpan<byte> value)
    {
        return new ContentDigest
        {
            Algorithm = DigestAlgorithm.Sha256,
            Value = ByteString.CopyFrom(SHA256.HashData(value)),
            SizeBytes = checked((ulong)value.Length),
        };
    }

    private static OptionalUInt64 Available(ulong value) => new()
    {
        Availability = AvailabilityState.Available,
        Value = value,
    };
}

public static class HelperFrameFactory
{
    public static HelperPrivateFrameV2 Create(ulong sequence, HelperReceiptV2 receipt) => new()
    {
        Sequence = sequence,
        ProtocolFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
        Receipt = receipt,
    };
}
