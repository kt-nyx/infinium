using Infinium.Domain.Contracts;

namespace Infinium.OpenAI;

public sealed record DeterministicProviderTranscript(
    ProviderSimulatorOutcome Outcome,
    bool TransportStarted,
    bool TransportStartAmbiguous,
    ProviderResponseState ResponseState,
    int? HttpStatus,
    string RequestedModel,
    string? ReturnedModel,
    string RequestedServiceTier,
    string? ReturnedServiceTier,
    ProviderUsageContract Usage,
    IReadOnlyList<ProviderRateLimitFactContract> RateFacts,
    string? ErrorCode,
    string? RefusalCode,
    string? IncompleteReason,
    bool RawResponseAvailable,
    long RawResponseBytes,
    bool CacheProfileCompliant,
    bool RetryPermitted,
    bool NetworkUsed,
    bool CredentialAccessed);

public sealed class DeterministicProviderSimulator
{
    public static DeterministicProviderTranscript Execute(
        ProviderSimulatorOutcome outcome,
        ProviderFiniteLimitsContract limits,
        UtcTimestamp observedAt)
    {
        if (outcome == ProviderSimulatorOutcome.Unspecified)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }
        ArgumentNullException.ThrowIfNull(limits);

        ProviderUsageContract complete = Usage(UsageReceiptState.Complete,
            dispatch: 1, input: Math.Min(128, limits.MaximumInputTokens),
            output: Math.Min(32, limits.MaximumOutputTokens), reasoning: Math.Min(8, limits.MaximumOutputTokens));
        ProviderUsageContract partial = Usage(UsageReceiptState.Partial,
            dispatch: 1, input: Math.Min(128, limits.MaximumInputTokens),
            output: Math.Min(8, limits.MaximumOutputTokens), reasoning: Math.Min(4, limits.MaximumOutputTokens));
        ProviderUsageContract failed = Usage(UsageReceiptState.FailedKnown,
            dispatch: 1, input: Math.Min(64, limits.MaximumInputTokens), output: 0, reasoning: 0);
        ProviderUsageContract unavailable = UnavailableUsage(
            outcome == ProviderSimulatorOutcome.KnownUndispatched
                ? UsageReceiptState.NotDispatched
                : UsageReceiptState.Ambiguous,
            outcome == ProviderSimulatorOutcome.KnownUndispatched ? 0 : null);
        ProviderRateLimitFactContract[] noRates = [];
        ProviderRateLimitFactContract[] rates =
        [
            new("model", "requests", ProviderAvailabilityState.Available, 100, 99, observedAt,
                new UtcTimestamp(observedAt.Value.AddMinutes(1))),
        ];

        return outcome switch
        {
            ProviderSimulatorOutcome.Completed => Transcript(outcome, ProviderResponseState.Completed, 200, complete,
                rates, true, 512),
            ProviderSimulatorOutcome.Refusal => Transcript(outcome, ProviderResponseState.Refusal, 200, complete,
                rates, true, 384, refusalCode: "policy_refusal"),
            ProviderSimulatorOutcome.Incomplete => Transcript(outcome, ProviderResponseState.Incomplete, 200, partial,
                rates, true, 384, incompleteReason: "max_output_tokens"),
            ProviderSimulatorOutcome.Failed => Transcript(outcome, ProviderResponseState.Failed, 500, failed,
                noRates, true, 256, errorCode: "simulated_server_error"),
            ProviderSimulatorOutcome.Queued => Transcript(outcome, ProviderResponseState.Queued, 200, partial,
                noRates, true, 256),
            ProviderSimulatorOutcome.InProgress => Transcript(outcome, ProviderResponseState.InProgress, 200, partial,
                noRates, true, 256),
            ProviderSimulatorOutcome.Malformed => Transcript(outcome, ProviderResponseState.Malformed, 200, complete,
                rates, true, 128),
            ProviderSimulatorOutcome.Oversized => Transcript(outcome, ProviderResponseState.Oversized, 200, partial,
                rates, false, checked(limits.MaximumRawResponseBytes + 1)),
            ProviderSimulatorOutcome.ReturnedModelMismatch => Transcript(outcome, ProviderResponseState.Mismatched, 200,
                complete, rates, true, 512, returnedModel: "gpt-5.6-terra"),
            ProviderSimulatorOutcome.ReturnedTierMismatch => Transcript(outcome, ProviderResponseState.Mismatched, 200,
                complete, rates, true, 512, returnedTier: "priority"),
            ProviderSimulatorOutcome.RateLimitedWithReset => Transcript(outcome, ProviderResponseState.Failed, 429,
                failed, rates, true, 192, errorCode: "rate_limit"),
            ProviderSimulatorOutcome.RateLimitedWithoutReset => Transcript(outcome, ProviderResponseState.Failed, 429,
                failed, noRates, true, 192, errorCode: "rate_limit"),
            ProviderSimulatorOutcome.AmbiguousStart => new(outcome, true, true, ProviderResponseState.Unknown, null,
                "gpt-5.6-sol", null, "default", null, unavailable, noRates, null, null, null, false, 0, false,
                false, false, false),
            ProviderSimulatorOutcome.KnownUndispatched => new(outcome, false, false, ProviderResponseState.Cancelled, null,
                "gpt-5.6-sol", null, "default", null, unavailable, noRates, null, null, null, false, 0, true,
                false, false, false),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
    }

    private static DeterministicProviderTranscript Transcript(
        ProviderSimulatorOutcome outcome,
        ProviderResponseState state,
        int status,
        ProviderUsageContract usage,
        IReadOnlyList<ProviderRateLimitFactContract> rateFacts,
        bool rawAvailable,
        long rawBytes,
        string? errorCode = null,
        string? refusalCode = null,
        string? incompleteReason = null,
        string returnedModel = "gpt-5.6-sol",
        string returnedTier = "default") =>
        new(outcome, true, false, state, status, "gpt-5.6-sol", returnedModel, "default", returnedTier,
            usage, rateFacts, errorCode, refusalCode, incompleteReason, rawAvailable, rawBytes,
            usage.CacheReadTokens.Value == 0 && usage.CacheWriteTokens.Value == 0,
            false, false, false);

    private static ProviderUsageContract Usage(
        UsageReceiptState state,
        long dispatch,
        long input,
        long output,
        long reasoning)
    {
        ProviderQuantityContract available(long value) => new(ProviderAvailabilityState.Available, value);
        long calculatedNanoUsd = checked(checked(input * 5_000) + checked(output * 30_000));
        return new(ProviderAvailabilityState.Available, available(dispatch), available(input), available(output),
            available(checked(input + output)), available(reasoning), available(0), available(0), available(0),
            available(calculatedNanoUsd),
            ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Available,
            ProviderAvailabilityState.Unavailable, state);
    }

    private static ProviderUsageContract UnavailableUsage(UsageReceiptState state, long? dispatch)
    {
        ProviderQuantityContract absent = new(ProviderAvailabilityState.Unavailable, null);
        ProviderQuantityContract dispatchFact = dispatch.HasValue
            ? new(ProviderAvailabilityState.Available, dispatch.Value)
            : absent;
        return new(ProviderAvailabilityState.Unavailable, dispatchFact, absent, absent, absent, absent, absent, absent,
            absent, absent, ProviderAvailabilityState.Unavailable, ProviderAvailabilityState.Unavailable,
            ProviderAvailabilityState.Unavailable, state);
    }
}
