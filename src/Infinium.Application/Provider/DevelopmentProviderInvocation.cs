using System.Text.Json;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Provider;

public sealed record DevelopmentProviderCredential(
    string ProfileId,
    string GenerationId,
    string AccountIdentityId,
    string ProjectIdentityId);

public sealed record DevelopmentProviderLimits(
    long MaximumInputTokens,
    long MaximumOutputTokens,
    long DeadlineMilliseconds,
    long MaximumLocalCostNanoUsd,
    long ProjectCostBoundaryNanoUsd);

public sealed record DevelopmentProviderRequest(
    string Instructions,
    string UntrustedInput,
    JsonElement OutputSchema,
    string SafetyIdentifier);

public sealed record DevelopmentProviderInvocationManifest(
    string InvocationId,
    ProviderOperationKind Operation,
    DevelopmentProviderCredential Credential,
    string CapabilitySnapshotId,
    string PriceSnapshotId,
    string Model,
    string ServiceTier,
    DevelopmentProviderLimits Limits,
    DevelopmentProviderRequest Request);

public sealed record ProviderUsageReservation(
    string InvocationId,
    long ReservedNanoUsd,
    long ProjectBoundaryNanoUsd);

public sealed record ProviderUsageSettlement(
    string InvocationId,
    long ReservedNanoUsd,
    long? ActualNanoUsd,
    long RetainedUnresolvedNanoUsd,
    string State);

/// <summary>
/// Local, single-invocation reservation and settlement used by the explicit
/// development provider command. Durable product accounting remains in the
/// authoritative store.
/// </summary>
public static class ProviderUsageBudget
{
    public const long OwnerMaximumProjectBoundaryNanoUsd = 10_000_000_000;

    public static ProviderUsageReservation Reserve(
        DevelopmentProviderInvocationManifest manifest,
        ProviderFiniteLimitsContract finiteLimits)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        long worstCase = OpenAiProviderProfileCatalog.CalculateWorstCaseNanoUsd(
            manifest.Operation,
            finiteLimits);
        if (manifest.Limits.ProjectCostBoundaryNanoUsd is <= 0
            or > OwnerMaximumProjectBoundaryNanoUsd
            || manifest.Limits.MaximumLocalCostNanoUsd <= 0
            || manifest.Limits.MaximumLocalCostNanoUsd > manifest.Limits.ProjectCostBoundaryNanoUsd
            || worstCase > manifest.Limits.MaximumLocalCostNanoUsd)
        {
            throw new InvalidOperationException(
                "The invocation cannot reserve within both its local limit and the owner-configured $10 project boundary.");
        }
        return new(
            manifest.InvocationId,
            worstCase,
            manifest.Limits.ProjectCostBoundaryNanoUsd);
    }

    public static ProviderUsageSettlement Settle(
        ProviderUsageReservation reservation,
        ProviderUsageContract usage,
        bool transportMayHaveStarted)
    {
        ArgumentNullException.ThrowIfNull(reservation);
        ArgumentNullException.ThrowIfNull(usage);
        long? actual = usage.CalculatedNanoUsd.Availability == ProviderAvailabilityState.Available
            ? usage.CalculatedNanoUsd.Value
            : null;
        if (actual is < 0 || actual > reservation.ProjectBoundaryNanoUsd)
        {
            throw new InvalidDataException(
                "Observed provider cost exceeds the closed project boundary.");
        }
        if (actual is not null)
        {
            return new(
                reservation.InvocationId,
                reservation.ReservedNanoUsd,
                actual,
                0,
                actual > reservation.ReservedNanoUsd ? "settled-overrun" : "settled");
        }
        return new(
            reservation.InvocationId,
            reservation.ReservedNanoUsd,
            null,
            transportMayHaveStarted ? reservation.ReservedNanoUsd : 0,
            transportMayHaveStarted ? "unresolved-hold" : "released-undispatched");
    }
}

public sealed record ProviderRequestAuthorization(
    DevelopmentProviderInvocationManifest Manifest,
    ProviderFiniteLimitsContract FiniteLimits,
    ProviderUsageReservation Reservation,
    bool Live);

/// <summary>
/// The final non-secret checks immediately before a development provider call.
/// It binds one exact credential generation, project, model, deadline, and
/// local cost envelope and refuses all fallback.
/// </summary>
public static class ProviderRequestAuthority
{
    public static ProviderRequestAuthorization Authorize(
        DevelopmentProviderInvocationManifest manifest,
        bool live)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateIdentity(manifest.InvocationId, "invocation");
        ValidateIdentity(manifest.Credential.ProfileId, "credential profile");
        ValidateIdentity(manifest.Credential.GenerationId, "credential generation");
        ValidateIdentity(manifest.Credential.AccountIdentityId, "account");
        ValidateIdentity(manifest.Credential.ProjectIdentityId, "project");
        if (manifest.CapabilitySnapshotId != OpenAiProviderProfileCatalog.Capability.Identity.Value
            || manifest.PriceSnapshotId != OpenAiProviderProfileCatalog.Price.Identity.Value
            || manifest.Model != OpenAiProviderProfileCatalog.Capability.Model
            || manifest.ServiceTier != OpenAiProviderProfileCatalog.Capability.ServiceTier)
        {
            throw new InvalidDataException(
                "The invocation selected a provider profile other than the exact current catalog.");
        }
        ProviderFiniteLimitsContract ceiling = manifest.Operation == ProviderOperationKind.TransportQualification
            ? new(16_384, manifest.Limits.MaximumInputTokens, manifest.Limits.MaximumOutputTokens,
                262_144, 1, manifest.Limits.MaximumLocalCostNanoUsd, manifest.Limits.DeadlineMilliseconds)
            : new(65_536, manifest.Limits.MaximumInputTokens, manifest.Limits.MaximumOutputTokens,
                1_048_576, 1, manifest.Limits.MaximumLocalCostNanoUsd, manifest.Limits.DeadlineMilliseconds);
        ProviderOperationContractInvariants.Validate(manifest.Operation, ceiling);
        if (string.IsNullOrWhiteSpace(manifest.Request.Instructions)
            || string.IsNullOrWhiteSpace(manifest.Request.UntrustedInput)
            || manifest.Request.OutputSchema.ValueKind != JsonValueKind.Object
            || !ProductUserSafetyIdentifier.IsValidProjection(manifest.Request.SafetyIdentifier))
        {
            throw new InvalidDataException(
                "The invocation request must contain bounded instructions, input, schema, and a valid safety identifier.");
        }
        return new(manifest, ceiling, ProviderUsageBudget.Reserve(manifest, ceiling), live);
    }

    private static void ValidateIdentity(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > 120
            || value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')))
        {
            throw new InvalidDataException($"The {name} identity is malformed.");
        }
    }
}

public static class DevelopmentProviderInvocationManifestCodec
{
    public const string SchemaIdentity = "infinium.development-provider-invocation/v1";

    public static DevelopmentProviderInvocationManifest Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(path));
        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16,
        });
        JsonElement root = document.RootElement;
        Exact(
            root,
            "schema_identity",
            "invocation_id",
            "operation",
            "credential",
            "provider_profile",
            "limits",
            "request");
        if (root.GetProperty("schema_identity").GetString() != SchemaIdentity)
        {
            throw new InvalidDataException(
                "The development provider invocation schema identity is unsupported.");
        }
        JsonElement credential = root.GetProperty("credential");
        Exact(
            credential,
            "profile_id",
            "generation_id",
            "account_identity_id",
            "project_identity_id");
        JsonElement profile = root.GetProperty("provider_profile");
        Exact(profile, "capability_snapshot_id", "price_snapshot_id", "model", "service_tier");
        JsonElement limits = root.GetProperty("limits");
        Exact(
            limits,
            "maximum_input_tokens",
            "maximum_output_tokens",
            "deadline_milliseconds",
            "maximum_local_cost_nano_usd",
            "project_cost_boundary_nano_usd");
        JsonElement request = root.GetProperty("request");
        Exact(request, "instructions", "untrusted_input", "output_schema", "safety_identifier");
        ProviderOperationKind operation = root.GetProperty("operation").GetString() switch
        {
            "transport-qualification" => ProviderOperationKind.TransportQualification,
            "source-claim-extraction" => ProviderOperationKind.SourceClaimExtraction,
            "candidate-investigation" => ProviderOperationKind.CandidateInvestigation,
            _ => throw new InvalidDataException(
                "The development provider operation is outside the closed profile."),
        };
        DevelopmentProviderInvocationManifest manifest = new(
            root.GetProperty("invocation_id").GetString() ?? "",
            operation,
            new(
                credential.GetProperty("profile_id").GetString() ?? "",
                credential.GetProperty("generation_id").GetString() ?? "",
                credential.GetProperty("account_identity_id").GetString() ?? "",
                credential.GetProperty("project_identity_id").GetString() ?? ""),
            profile.GetProperty("capability_snapshot_id").GetString() ?? "",
            profile.GetProperty("price_snapshot_id").GetString() ?? "",
            profile.GetProperty("model").GetString() ?? "",
            profile.GetProperty("service_tier").GetString() ?? "",
            new(
                limits.GetProperty("maximum_input_tokens").GetInt64(),
                limits.GetProperty("maximum_output_tokens").GetInt64(),
                limits.GetProperty("deadline_milliseconds").GetInt64(),
                limits.GetProperty("maximum_local_cost_nano_usd").GetInt64(),
                limits.GetProperty("project_cost_boundary_nano_usd").GetInt64()),
            new(
                request.GetProperty("instructions").GetString() ?? "",
                request.GetProperty("untrusted_input").GetString() ?? "",
                request.GetProperty("output_schema").Clone(),
                request.GetProperty("safety_identifier").GetString() ?? ""));
        _ = ProviderRequestAuthority.Authorize(manifest, live: false);
        return manifest;
    }

    private static void Exact(JsonElement value, params string[] propertyNames)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.EnumerateObject().Select(property => property.Name)
                .SequenceEqual(propertyNames, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "A development provider invocation object has unknown, missing, duplicate, or reordered properties.");
        }
    }
}
