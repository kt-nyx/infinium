using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderCapabilityTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void OpenAiProviderProfileCatalogIsExactImmutableAndAdministrativeFactsStayUnavailable()
    {
        UtcTimestamp observed = new(DateTimeOffset.Parse("2026-08-11T00:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture));
        ProviderCatalogProjection projection = OpenAiProviderProfileCatalog.CreateNonLiveProjection(observed);
        Assert.AreEqual("gpt-5.6-sol", projection.Capability.Model);
        Assert.AreEqual("default", projection.Capability.ServiceTier);
        Assert.AreEqual("medium", projection.Capability.ReasoningEffort);
        Assert.AreEqual("explicit", projection.Capability.PromptCacheMode);
        Assert.IsFalse(projection.Capability.Store);
        Assert.IsFalse(projection.Capability.Background);
        Assert.IsFalse(projection.Capability.Stream);
        Assert.AreEqual(ProviderAvailabilityState.Unavailable, projection.ProviderSpendLimit.Availability);
        Assert.AreEqual(ProviderAvailabilityState.Unavailable, projection.ProviderHistoricalCost.Availability);
        Assert.AreEqual(ProviderAvailabilityState.Unavailable, projection.ProviderCredit.Availability);
        Assert.AreEqual(ProviderAvailabilityState.Unavailable, projection.ProviderRateHeadroom.Availability);
        Assert.IsFalse(projection.NetworkPermitted);
        Assert.IsFalse(projection.CredentialAccessPermitted);
    }
}

[TestClass]
public sealed class PriceCatalogTests
{
    private static readonly string[] ExpectedClasses =
        ["ordinary-input/input", "cache-read/input", "cache-write/input", "none/output", "none/reasoning"];

    [TestMethod]
    [TestCategory("Unit")]
    public void PriceCatalogContainsEveryDocumentedClassAndRoundsEachRationalComponentUpward()
    {
        CollectionAssert.AreEquivalent(
            ExpectedClasses,
            OpenAiProviderProfileCatalog.Price.Rules.Select(rule => rule.CacheClass + "/" + rule.TokenClass).ToArray());
        ProviderPriceRuleContract fractional = OpenAiProviderProfileCatalog.Price.Rules[0] with
        {
            NumeratorNanoUsd = 5,
            DenominatorTokens = 2,
        };
        Assert.AreEqual(3, ProviderOperationContractInvariants.CalculateComponentNanoUsd(1, fractional));
        Assert.AreEqual(5, ProviderOperationContractInvariants.CalculateComponentNanoUsd(2, fractional));

        ProviderFiniteLimitsContract qualification = new(16_384, 20_480, 256, 262_144, 1, 140_000_000, 60_000);
        Assert.AreEqual(110_080_000,
            OpenAiProviderProfileCatalog.CalculateWorstCaseNanoUsd(ProviderOperationKind.TransportQualification, qualification));
        Assert.ThrowsExactly<OverflowException>(() =>
            ProviderOperationContractInvariants.CalculateComponentNanoUsd(
                long.MaxValue,
                OpenAiProviderProfileCatalog.Price.Rules[0]));
    }
}

[TestClass]
public sealed class ProviderBudgetTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void BudgetVectorUsesCheckedExactDimensionsAndRejectsOverflowOrRelationalDrift()
    {
        ProviderBudgetVectorContract used = new(1, 10, 5, 15, 2, 0, 0, 0, 100);
        ProviderBudgetVectorContract request = new(1, 20, 10, 30, 4, 0, 0, 0, 200);
        ProviderBudgetVectorContract limit = new(2, 30, 15, 45, 6, 0, 0, 0, 300);
        Assert.IsTrue(ProviderBudgetVectorContract.FitsWithin(used, request, limit));
        Assert.IsFalse(ProviderBudgetVectorContract.FitsWithin(used, request,
            limit with { DispatchCount = 1 }));
        Assert.IsFalse(ProviderBudgetVectorContract.FitsWithin(
            new(long.MaxValue, 0, 0, 0, 0, 0, 0, 0, 0),
            new(1, 0, 0, 0, 0, 0, 0, 0, 0),
            new(long.MaxValue, 0, 0, 0, 0, 0, 0, 0, 0)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderBudgetVectorContract.Validate(new(1, 10, 5, 14, 2, 0, 0, 0, 100)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BudgetSimulatorCoversClosedMatrixWithoutNetworkCredentialRetryOrCacheUse()
    {
        ProviderFiniteLimitsContract limits = new(16_384, 20_480, 256, 262_144, 1, 140_000_000, 60_000);
        UtcTimestamp observed = new(DateTimeOffset.Parse("2026-08-11T00:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture));
        ProviderSimulatorOutcome[] outcomes = Enum.GetValues<ProviderSimulatorOutcome>()
            .Where(value => value != ProviderSimulatorOutcome.Unspecified)
            .ToArray();
        Assert.HasCount(14, outcomes);
        foreach (ProviderSimulatorOutcome outcome in outcomes)
        {
            DeterministicProviderTranscript transcript = DeterministicProviderSimulator.Execute(outcome, limits, observed);
            Assert.AreEqual(outcome, transcript.Outcome);
            Assert.IsFalse(transcript.NetworkUsed, outcome.ToString());
            Assert.IsFalse(transcript.CredentialAccessed, outcome.ToString());
            Assert.IsFalse(transcript.RetryPermitted, outcome.ToString());
            if (outcome == ProviderSimulatorOutcome.Completed)
            {
                Assert.AreEqual(ProviderAvailabilityState.Available, transcript.Usage.CalculatedNanoUsd.Availability);
                Assert.AreEqual(0L, transcript.Usage.CacheReadTokens.Value);
                Assert.AreEqual(0L, transcript.Usage.CacheWriteTokens.Value);
            }
            if (outcome == ProviderSimulatorOutcome.Oversized)
            {
                Assert.IsFalse(transcript.RawResponseAvailable);
                Assert.AreEqual(limits.MaximumRawResponseBytes + 1, transcript.RawResponseBytes);
            }
        }
    }
}
