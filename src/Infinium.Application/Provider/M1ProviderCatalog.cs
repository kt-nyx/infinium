using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Provider;

public sealed record ProviderAuthorityFact<T>(
    ProviderAvailabilityState Availability,
    T? Value,
    string Authority,
    string Scope,
    UtcTimestamp ObservedAt,
    string Reason) where T : struct;

public sealed record M1ProviderCatalogProjection(
    ProviderCapabilitySnapshotContract Capability,
    ProviderPriceSnapshotContract Price,
    ProviderAuthorityFact<long> ProviderSpendLimit,
    ProviderAuthorityFact<long> ProviderHistoricalCost,
    ProviderAuthorityFact<long> ProviderCredit,
    ProviderAuthorityFact<long> ProviderRateHeadroom,
    string AccessMode,
    bool NetworkPermitted,
    bool CredentialAccessPermitted);

public static class M1ProviderCatalog
{
    public const string CapabilityRevision = "openai-gpt-5.6-sol-2026-08-10";
    public const string PriceRevision = "openai-gpt-5.6-sol-standard-2026-08-10";
    public const long MaximumContextTokens = 272_000;
    public const long OrdinaryInputNanoUsdPerToken = 5_000;
    public const long CacheReadNanoUsdPerToken = 500;
    public const long CacheWriteNanoUsdPerToken = 6_250;
    public const long OutputNanoUsdPerToken = 30_000;

    public static ProviderCapabilitySnapshotContract Capability { get; } = CreateCapability();

    public static ProviderPriceSnapshotContract Price { get; } = CreatePrice();

    public static long CalculateWorstCaseNanoUsd(
        ProviderOperationKind operationKind,
        ProviderFiniteLimitsContract limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ProviderOperationContractInvariants.Validate(operationKind, limits);
        ProviderPriceRuleContract input = Price.Rules.Single(rule => rule.TokenClass == "input" && rule.CacheClass == "ordinary-input");
        ProviderPriceRuleContract output = Price.Rules.Single(rule => rule.TokenClass == "output");
        long inputCost = ProviderOperationContractInvariants.CalculateComponentNanoUsd(
            limits.MaximumInputTokens,
            input);
        long outputCost = ProviderOperationContractInvariants.CalculateComponentNanoUsd(
            limits.MaximumOutputTokens,
            output);
        long result = checked(inputCost + outputCost);
        if (result > limits.MaximumCalculatedNanoUsd)
        {
            throw new InvalidOperationException(
                "The exact M1 price catalog exceeds the operation's configured nano-USD hard limit.");
        }
        return result;
    }

    public static M1ProviderCatalogProjection CreateNonLiveProjection(UtcTimestamp observedAt)
    {
        ProviderAuthorityFact<long> spend = Unavailable("provider-admin", "selected-account", observedAt,
            "The Responses capability does not expose a qualified provider spend limit.");
        ProviderAuthorityFact<long> history = Unavailable("provider-admin", "selected-account/time-window", observedAt,
            "Administrative usage history is outside the selected credential purpose.");
        ProviderAuthorityFact<long> credit = Unavailable("provider-billing", "selected-account", observedAt,
            "Prepaid credit is not inferable from rate or local budget state.");
        ProviderAuthorityFact<long> rate = Unavailable("response-rate-metadata", "request/model-window", observedAt,
            "No provider response has been observed by the non-network simulator.");
        return new(Capability, Price, spend, history, credit, rate,
            "direct-usage-priced-platform-api", false, false);
    }

    private static ProviderAuthorityFact<long> Unavailable(
        string authority,
        string scope,
        UtcTimestamp observedAt,
        string reason) =>
        new(ProviderAvailabilityState.Unavailable, null, authority, scope, observedAt, reason);

    private static ProviderCapabilitySnapshotContract CreateCapability()
    {
        const string identity = "m1-openai-gpt-5.6-sol-capability-2026-08-10";
        string canonical = string.Join('|', identity, "openai", "gpt-5.6-sol", "default", "medium",
            "current_turn", "standard", "store=false", "background=false", "stream=false", "tools=none",
            "truncation=disabled", "prompt-cache=explicit-no-key-no-breakpoint", MaximumContextTokens,
            CapabilityRevision);
        return new(
            new OpaqueId(identity), Fingerprint(canonical), "openai", "gpt-5.6-sol", "default", "medium",
            "current_turn", "standard", false, false, false, "none", 0, "disabled", "explicit", false, false,
            MaximumContextTokens, CapabilityRevision);
    }

    private static ProviderPriceSnapshotContract CreatePrice()
    {
        const string identity = "m1-openai-gpt-5.6-sol-price-2026-08-10";
        ProviderPriceRuleContract[] rules =
        [
            new(new OpaqueId("m1-sol-standard-input"), "openai", "gpt-5.6-sol", "default",
                "standard-under-272k", "ordinary-input", "input", "none", "global", "USD",
                OrdinaryInputNanoUsdPerToken, 1, PriceRevision),
            new(new OpaqueId("m1-sol-standard-output"), "openai", "gpt-5.6-sol", "default",
                "standard-under-272k", "none", "output", "none", "global", "USD",
                OutputNanoUsdPerToken, 1, PriceRevision),
            new(new OpaqueId("m1-sol-reasoning-output"), "openai", "gpt-5.6-sol", "default",
                "standard-under-272k", "none", "reasoning", "none", "global", "USD",
                OutputNanoUsdPerToken, 1, PriceRevision),
            new(new OpaqueId("m1-sol-cache-read"), "openai", "gpt-5.6-sol", "default",
                "standard-under-272k", "cache-read", "input", "none", "global", "USD",
                CacheReadNanoUsdPerToken, 1, PriceRevision),
            new(new OpaqueId("m1-sol-cache-write"), "openai", "gpt-5.6-sol", "default",
                "standard-under-272k", "cache-write", "input", "none", "global", "USD",
                CacheWriteNanoUsdPerToken, 1, PriceRevision),
        ];
        string canonical = string.Join('|', identity, "openai", "gpt-5.6-sol", "default", "USD", PriceRevision,
            string.Join(';', rules.Select(rule => string.Join(':', rule.RuleId.Value, rule.ContextBand,
                rule.CacheClass, rule.TokenClass, rule.NumeratorNanoUsd, rule.DenominatorTokens))));
        return new(new OpaqueId(identity), Fingerprint(canonical), "openai", "gpt-5.6-sol", "default", "USD",
            PriceRevision, rules);
    }

    private static Sha256Fingerprint Fingerprint(string canonical) =>
        new(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
}
