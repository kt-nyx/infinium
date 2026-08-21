using Infinium.Domain.Contracts;

namespace Infinium.Coordinator;

internal static class M1Slice6SuccessorPricing
{
    internal const long MaximumContextTokens = 1_050_000;
    internal const long MaximumInputTokens = 922_000;
    internal const long MaximumOutputTokens = 128_000;
    private const long LongContextThreshold = 272_000;
    private const long OrdinaryInputNanoUsdPerToken = 5_000;
    private const long OrdinaryOutputNanoUsdPerToken = 30_000;
    private const long LongInputNanoUsdPerToken = 10_000;
    private const long LongOutputNanoUsdPerToken = 45_000;

    internal static long Calculate(ProviderFiniteLimitsContract limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.MaximumDispatchCount != 1
            || limits.MaximumInputTokens is < 1 or > MaximumInputTokens
            || limits.MaximumOutputTokens is < 1 or > MaximumOutputTokens
            || checked(limits.MaximumInputTokens + limits.MaximumOutputTokens) > MaximumContextTokens)
        {
            throw new InvalidOperationException("Successor v6 token bounds exceed the accepted provider snapshot.");
        }
        bool longContext = limits.MaximumInputTokens > LongContextThreshold;
        long inputRate = longContext ? LongInputNanoUsdPerToken : OrdinaryInputNanoUsdPerToken;
        long outputRate = longContext ? LongOutputNanoUsdPerToken : OrdinaryOutputNanoUsdPerToken;
        long result = checked(limits.MaximumInputTokens * inputRate
            + limits.MaximumOutputTokens * outputRate);
        if (result != limits.MaximumCalculatedNanoUsd)
        {
            throw new InvalidOperationException(
                "Successor v6 reservation must equal the exact price-derived worst case.");
        }
        return result;
    }
}
