using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;

namespace Infinium.CredentialHelper;

public interface IHelperSecretSource
{
    public byte[] Capture(HelperAssignmentV2 assignment);
}

internal sealed class DeterministicHelperSecretSource : IHelperSecretSource
{
    internal static DeterministicHelperSecretSource Instance { get; } = new();

    public byte[] Capture(HelperAssignmentV2 assignment)
    {
        if (assignment.AssignmentId.StartsWith("wp4-v2/", StringComparison.Ordinal))
        {
            CredentialNativeQualificationPhaseV2 phase = CredentialNativeQualificationPhasesV2.Parse(
                assignment.AssignmentId, assignment.AssignmentKind);
            if (phase.ScenarioId == "interactive-entry-cancel")
            {
                throw new OperationCanceledException("Injected qualification cancel before secret creation.");
            }
            return phase.SecretMode switch
            {
                CredentialNativeQualificationSecretModeV2.GeneratedMaximum =>
                    new byte[DeterministicFakeSecureStore.MaximumSecretBytes],
                CredentialNativeQualificationSecretModeV2.GeneratedOversize =>
                    new byte[DeterministicFakeSecureStore.MaximumSecretBytes + 1],
                _ => Encoding.UTF8.GetBytes("WP3-REAL-CHILD-SECRET-CANARY/" + assignment.AssignmentId),
            };
        }
        return Encoding.UTF8.GetBytes("WP3-REAL-CHILD-SECRET-CANARY/" + assignment.AssignmentId);
    }
}

public sealed class OneShotHelperEngine
{
    private readonly ISyntheticSecureStore store;
    private readonly TimeProvider timeProvider;
    private readonly IHelperSecretSource secretSource;
    private readonly IOpenAiResponsesTransport? providerTransport;

    public OneShotHelperEngine(
        ISyntheticSecureStore store,
        TimeProvider? timeProvider = null,
        IHelperSecretSource? secretSource = null,
        IOpenAiResponsesTransport? providerTransport = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.secretSource = secretSource ?? DeterministicHelperSecretSource.Instance;
        this.providerTransport = providerTransport;
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
            (receipt, stagedResponse) = await ExecuteAsync(
                bootstrap.Bootstrap, assignment, revalidation, cancellationToken).ConfigureAwait(false);
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

    private async Task<(HelperReceiptV2 Receipt, byte[] StagedResponse)> ExecuteAsync(
        HelperBootstrapV2 bootstrap,
        HelperAssignmentV2 assignment,
        DispatchRevalidationV2? revalidation,
        CancellationToken cancellationToken)
    {
        SyntheticCredentialSlot slot = new(assignment.AccessProfileId.Value, assignment.GenerationId.Value);
        SyntheticCredentialSlot bootstrapSlot = new(
            bootstrap.Credential?.AccessProfileId?.Value ?? assignment.AccessProfileId.Value,
            bootstrap.Credential?.GenerationId?.Value ?? assignment.GenerationId.Value);
        HelperOutcomeV2 outcome;
        bool hasResponse = false;
        byte[]? secret = null;
        OpenAiResponsesResult? adapterResult = null;
        try
        {
            CredentialNativeQualificationPhaseV2? qualificationPhase =
                assignment.AssignmentId.StartsWith("wp4-v2/", StringComparison.Ordinal)
                    ? CredentialNativeQualificationPhasesV2.Parse(
                        assignment.AssignmentId,
                        assignment.AssignmentKind)
                    : null;
            if (store is WindowsCredentialManagerStore nativeQualificationStore)
            {
                nativeQualificationStore.ConfigureQualificationPhase(assignment);
            }
            if (qualificationPhase?.UnavailableBeforeNativeCall == true)
            {
                throw new IOException("Injected qualification store unavailability before any store operation.");
            }
            switch (assignment.AssignmentKind)
            {
                case HelperAssignmentKindV2.Enroll:
                    secret = secretSource.Capture(assignment);
                    outcome = WriteAndVerify(slot, secret)
                        ? HelperOutcomeV2.Completed
                        : HelperOutcomeV2.FailedKnown;
                    break;
                case HelperAssignmentKindV2.Replace:
                    if (bootstrapSlot == slot)
                    {
                        throw new InvalidDataException("Replacement requires an exact predecessor and fresh successor generation.");
                    }
                    secret = secretSource.Capture(assignment);
                    bool successorVerified = WriteAndVerify(slot, secret);
                    if (successorVerified
                        && assignment.AssignmentId.Contains("replacement-interrupted", StringComparison.Ordinal)
                        && store is not WindowsCredentialManagerStore)
                    {
                        throw new IOException("Injected qualification predecessor cleanup interruption after successor half-commit.");
                    }
                    outcome = successorVerified && DeleteOrConfirmAbsent(bootstrapSlot)
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
                        if (!store.VerifyExact(slot))
                        {
                            secret = secretSource.Capture(assignment);
                            if (!WriteAndVerify(slot, secret))
                            {
                                outcome = HelperOutcomeV2.FailedKnown;
                                break;
                            }
                        }
                        outcome = store.VerifyExact(slot) && DeleteOrConfirmAbsent(bootstrapSlot)
                            ? HelperOutcomeV2.Completed
                            : HelperOutcomeV2.FailedKnown;
                    }
                    break;
                case HelperAssignmentKindV2.Disable:
                    outcome = HelperOutcomeV2.Completed;
                    break;
                case HelperAssignmentKindV2.Delete:
                    _ = store.DeleteExact(slot);
                    outcome = !store.VerifyExact(slot)
                        ? HelperOutcomeV2.Completed
                        : HelperOutcomeV2.FailedKnown;
                    break;
                case HelperAssignmentKindV2.ProviderDispatch:
                    secret = store.ReadExact(slot);
                    if (providerTransport is null)
                    {
                        // WP3/WP4 qualification retains its explicit deterministic fake.
                        // WP5 injects the closed loopback/production transport branch.
                        hasResponse = true;
                        outcome = HelperOutcomeV2.Completed;
                    }
                    else
                    {
                        adapterResult = await providerTransport.SendOnceAsync(
                            assignment.ProviderRequest.CanonicalRequestBytes.Memory,
                            secret,
                            Limits(assignment.Limits),
                            assignment.ProviderRequest.RequestId,
                            cancellationToken).ConfigureAwait(false);
                        hasResponse = adapterResult.RawResponseBytes is not null;
                        outcome = adapterResult.State switch
                        {
                            ProviderResponseState.Completed when adapterResult.Admitted => HelperOutcomeV2.Completed,
                            ProviderResponseState.Malformed => HelperOutcomeV2.Malformed,
                            ProviderResponseState.Oversized => HelperOutcomeV2.Oversized,
                            ProviderResponseState.Unknown when adapterResult.TransportMayHaveStarted => HelperOutcomeV2.TransportMayHaveStarted,
                            ProviderResponseState.Cancelled => HelperOutcomeV2.Cancelled,
                            _ => HelperOutcomeV2.FailedKnown,
                        };
                    }
                    break;
                default:
                    throw new InvalidDataException("The helper assignment kind is unsupported.");
            }
        }
        catch (IOException)
        {
            outcome = HelperOutcomeV2.Unavailable;
        }
        catch (System.ComponentModel.Win32Exception) when (
            store is WindowsCredentialManagerStore { NamespaceReuseBlocked: true })
        {
            outcome = HelperOutcomeV2.Unavailable;
        }
        catch (InvalidDataException)
        {
            // Oversized is a provider-response receipt state. Credential
            // enrollment rejects an inadmissible secret as a known failure;
            // qualification evidence independently proves the exact
            // pre-CredWriteW size boundary.
            outcome = assignment.AssignmentKind == HelperAssignmentKindV2.ProviderDispatch
                ? HelperOutcomeV2.Oversized
                : HelperOutcomeV2.FailedKnown;
        }
        catch (KeyNotFoundException)
        {
            outcome = HelperOutcomeV2.FailedKnown;
        }
        catch (OperationCanceledException)
        {
            outcome = HelperOutcomeV2.Cancelled;
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
        bool stageOversizedReceipt = adapterResult?.State == ProviderResponseState.Oversized;
        if (hasResponse || stageOversizedReceipt)
        {
            stagedResponse = adapterResult is null
                ? Encoding.UTF8.GetBytes("synthetic-provider-response/" + assignment.AssignmentId)
                : OpenAiStagedResponseEnvelope.Create(adapterResult);
            ulong rawResponseLength = checked((ulong)(adapterResult?.RawResponseBytes?.Length ?? stagedResponse.Length));
            if (rawResponseLength > assignment.Limits.MaximumResponseBytes
                && !stageOversizedReceipt
                || (ulong)stagedResponse.Length > assignment.Limits.MaximumStagedOutputBytes)
            {
                throw new InvalidDataException("The deterministic response exceeds its retained response/staging bound.");
            }
            if (hasResponse)
            {
                receipt.RawResponse = Digest(adapterResult?.RawResponseBytes ?? stagedResponse);
            }
            else
            {
                receipt.OverflowObservedExcessBytes = 1;
            }
            if (adapterResult is null)
            {
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
            else
            {
                receipt.InputTokens = Quantity(adapterResult.Usage.InputTokens);
                receipt.OutputTokens = Quantity(adapterResult.Usage.OutputTokens);
                receipt.TotalTokens = Quantity(adapterResult.Usage.TotalTokens);
                receipt.ReasoningTokens = Quantity(adapterResult.Usage.ReasoningTokens);
                receipt.CacheReadTokens = Quantity(adapterResult.Usage.CacheReadTokens);
                receipt.CacheWriteTokens = Quantity(adapterResult.Usage.CacheWriteTokens);
                receipt.PricedToolCalls = Quantity(adapterResult.Usage.PricedToolCalls);
                receipt.CalculatedNanoUsd = Quantity(adapterResult.Usage.CalculatedNanoUsd);
                receipt.DispatchCount = Quantity(adapterResult.Usage.DispatchCount);
                receipt.UsageReceiptState = UsageState(adapterResult.Usage.ReceiptState);
            }
        }
        return (receipt, stagedResponse);
    }

    private bool DeleteOrConfirmAbsent(SyntheticCredentialSlot slot)
    {
        if (!store.VerifyExact(slot))
        {
            return true;
        }
        _ = store.DeleteExact(slot);
        return !store.VerifyExact(slot);
    }

    private bool WriteAndVerify(SyntheticCredentialSlot slot, ReadOnlySpan<byte> secret)
    {
        store.WriteExact(slot, secret);
        byte[] retained = store.ReadExact(slot);
        try { return CryptographicOperations.FixedTimeEquals(secret, retained); }
        finally { CryptographicOperations.ZeroMemory(retained); }
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

    private static ProviderFiniteLimitsContract Limits(HelperLimitsV2 value) => new(
        checked((long)value.MaximumRequestBytes), checked((long)value.MaximumInputTokens),
        checked((long)value.MaximumOutputTokens), checked((long)value.MaximumResponseBytes),
        checked((long)value.MaximumDispatchCount), value.MaximumCalculatedNanoUsd,
        checked((long)value.MaximumDuration.Value));

    private static OptionalUInt64 Quantity(ProviderQuantityContract value) => new()
    {
        Availability = (AvailabilityState)(int)value.Availability,
        Value = checked((ulong)(value.Value ?? 0)),
    };

    private static UsageReceiptStateV2 UsageState(UsageReceiptState value) => value switch
    {
        UsageReceiptState.NotDispatched => UsageReceiptStateV2.NotDispatched,
        UsageReceiptState.Complete => UsageReceiptStateV2.Complete,
        UsageReceiptState.Partial => UsageReceiptStateV2.Partial,
        UsageReceiptState.FailedKnown => UsageReceiptStateV2.FailedKnown,
        UsageReceiptState.Ambiguous => UsageReceiptStateV2.Ambiguous,
        _ => UsageReceiptStateV2.Unavailable,
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
