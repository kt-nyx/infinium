using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;

namespace Infinium.CredentialHelper;

public static class DevelopmentProviderInvocationRunner
{
    public static async Task<byte[]> RunAsync(
        DevelopmentProviderInvocationManifest manifest,
        bool live,
        IOpenAiResponsesTransport? offlineTransport = null,
        CancellationToken cancellationToken = default)
    {
        ProviderRequestAuthorization authorization =
            ProviderRequestAuthority.Authorize(manifest, live);
        OpenAiResponsesRequest request = new(
            manifest.Operation,
            manifest.Request.Instructions,
            manifest.Request.UntrustedInput,
            manifest.Request.OutputSchema,
            manifest.Limits.MaximumOutputTokens,
            manifest.Request.SafetyIdentifier);
        byte[] canonicalRequest = OpenAiResponsesCanonicalSerializer.Serialize(request);
        if (canonicalRequest.LongLength > authorization.FiniteLimits.MaximumRequestBytes)
        {
            throw new InvalidDataException(
                "The canonical provider request exceeds the manifest's exact request bound.");
        }
        ProviderOperationContractInvariants.RequireLocalInputBoundProof(canonicalRequest.LongLength);
        OpenAiResponsesResult result;
        if (live)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException(
                    "Live provider invocation requires Windows Credential Manager.");
            }
            ProviderCredentialReference reference = new(
                manifest.Credential.ProfileId,
                manifest.Credential.GenerationId,
                manifest.Credential.AccountIdentityId,
                manifest.Credential.ProjectIdentityId);
            using ProviderCredentialLease lease = ProviderCredentialStore.ReadExact(reference);
            using OpenAiResponsesAdapter adapter = OpenAiResponsesAdapter.CreateProduction();
            result = await adapter.SendOnceAsync(
                canonicalRequest,
                lease.Secret,
                authorization.FiniteLimits,
                manifest.InvocationId,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            IOpenAiResponsesTransport transport =
                offlineTransport ?? OfflineDevelopmentProviderTransport.Instance;
            byte[] fakeSecret = "sk-offline-not-a-real-credential"u8.ToArray();
            try
            {
                result = await transport.SendOnceAsync(
                    canonicalRequest,
                    fakeSecret,
                    authorization.FiniteLimits,
                    manifest.InvocationId,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(fakeSecret);
            }
        }
        ProviderUsageSettlement settlement = ProviderUsageBudget.Settle(
            authorization.Reservation,
            result.Usage,
            result.TransportMayHaveStarted);
        ProviderCredentialReference evidenceReference = new(
            manifest.Credential.ProfileId,
            manifest.Credential.GenerationId,
            manifest.Credential.AccountIdentityId,
            manifest.Credential.ProjectIdentityId);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_identity = "infinium.development-provider-evidence/v1",
            invocation_id = manifest.InvocationId,
            mode = live ? "live" : "offline-fake-provider",
            operation = manifest.Operation.ToString(),
            credential = new
            {
                profile_id = manifest.Credential.ProfileId,
                generation_id = manifest.Credential.GenerationId,
                account_identity_id = manifest.Credential.AccountIdentityId,
                project_identity_id = manifest.Credential.ProjectIdentityId,
                target_fingerprint_sha256 = evidenceReference.TargetFingerprintSha256(),
            },
            provider_profile = new
            {
                capability_snapshot_id = manifest.CapabilitySnapshotId,
                price_snapshot_id = manifest.PriceSnapshotId,
                model = manifest.Model,
                service_tier = manifest.ServiceTier,
            },
            request = new
            {
                canonical_request_sha256 =
                    OpenAiResponsesCanonicalSerializer.Fingerprint(canonicalRequest),
                canonical_request_bytes = canonicalRequest.LongLength,
                maximum_input_tokens = manifest.Limits.MaximumInputTokens,
                maximum_output_tokens = manifest.Limits.MaximumOutputTokens,
                deadline_milliseconds = manifest.Limits.DeadlineMilliseconds,
            },
            budget = new
            {
                authorization.Reservation.ReservedNanoUsd,
                authorization.Reservation.ProjectBoundaryNanoUsd,
                settlement.ActualNanoUsd,
                settlement.RetainedUnresolvedNanoUsd,
                settlement.State,
            },
            outcome = new
            {
                state = result.State.ToString(),
                result.Admitted,
                result.AdmissionReason,
                result.TransportMayHaveStarted,
                result.NetworkUsed,
                result.SendCount,
                result.HttpStatus,
                provider_request_id = SanitizedId(result.ProviderRequestId),
                provider_response_id = SanitizedId(result.ProviderResponseId),
                result.Usage,
            },
        });
    }

    private static string? SanitizedId(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Length > 128
            || value.Any(character => !(char.IsAsciiLetterOrDigit(character)
                || character is '-' or '_' or '.'))
            ? null
            : value;
}

internal sealed class OfflineDevelopmentProviderTransport : IOpenAiResponsesTransport
{
    internal static OfflineDevelopmentProviderTransport Instance { get; } = new();

    public Task<OpenAiResponsesResult> SendOnceAsync(
        ReadOnlyMemory<byte> canonicalRequest,
        ReadOnlyMemory<byte> secret,
        ProviderFiniteLimitsContract limits,
        string clientRequestId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OpenAiResponsesCanonicalSerializer.ValidateExactProfile(
            canonicalRequest.Span,
            limits.MaximumOutputTokens);
        if (secret.IsEmpty)
        {
            throw new InvalidOperationException(
                "The offline provider boundary still requires a non-empty one-use test credential.");
        }
        ProviderQuantityContract zero =
            new(ProviderAvailabilityState.Available, 0);
        ProviderUsageContract usage = new(
            ProviderAvailabilityState.Available,
            new(ProviderAvailabilityState.Available, 1),
            zero,
            zero,
            zero,
            zero,
            zero,
            zero,
            zero,
            zero,
            ProviderAvailabilityState.Available,
            ProviderAvailabilityState.NotApplicable,
            ProviderAvailabilityState.NotApplicable,
            UsageReceiptState.Complete);
        OpenAiResponsesResult result = new(
            ProviderResponseState.Completed,
            TransportMayHaveStarted: false,
            RetryPermitted: false,
            HttpStatus: 200,
            RawResponseBytes: "{\"offline\":true}"u8.ToArray(),
            ProviderResponseId: "offline-response",
            ClientRequestId: clientRequestId,
            ProviderRequestId: "offline-request",
            ReturnedModel: OpenAiResponsesCanonicalSerializer.Model,
            ReturnedServiceTier: OpenAiResponsesCanonicalSerializer.ServiceTier,
            RefusalCode: null,
            IncompleteReason: null,
            ErrorCode: null,
            Usage: usage,
            RateHeaders: [],
            Admitted: true,
            AdmissionReason: "offline-fake-provider",
            NetworkUsed: false,
            SendCount: 0);
        return Task.FromResult(result);
    }
}
