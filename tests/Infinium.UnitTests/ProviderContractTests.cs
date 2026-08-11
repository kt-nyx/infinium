using System.Security.Cryptography;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderContractTests
{
    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderFiniteBoundProvesBothAcceptedOperationCeilingsLocally()
    {
        ProviderFiniteLimitsContract qualification = new(
            16_384, 20_480, 256, 262_144, 1, 140_000_000, 60_000);
        ProviderFiniteLimitsContract semantic = new(
            65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000);

        ProviderOperationContractInvariants.Validate(qualification);
        ProviderOperationContractInvariants.Validate(semantic);
        Assert.AreEqual(
            20_480L,
            ProviderOperationContractInvariants.ConservativeUtf8TokenUpperBound(16_384));
        Assert.AreEqual(
            69_632L,
            ProviderOperationContractInvariants.ConservativeUtf8TokenUpperBound(65_536));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.Validate(qualification with
            {
                MaximumInputTokens = 20_479,
            }));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ProviderOperationContractInvariants.ConservativeUtf8TokenUpperBound(65_537));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderContractPriceRuleUsesCheckedComponentWiseUpwardRounding()
    {
        ProviderPriceRuleContract rule = new(
            Id("price-rule-1"), "openai", "gpt-5.6-sol", "default",
            "standard-under-272k", "ordinary-input", "input", "none",
            "global", "USD", 3, 2, "2026-08-10");

        Assert.AreEqual(2L, ProviderOperationContractInvariants.CalculateComponentNanoUsd(1, rule));
        Assert.AreEqual(3L, ProviderOperationContractInvariants.CalculateComponentNanoUsd(2, rule));
        Assert.ThrowsExactly<OverflowException>(() =>
            ProviderOperationContractInvariants.CalculateComponentNanoUsd(long.MaxValue, rule));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderOperationContractInvariants.CalculateComponentNanoUsd(1, rule with { Region = "implicit" }));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderContractApplicationQueriesAreFiniteAndReplayIsOffline()
    {
        ProviderApplicationContractInvariants.Validate(new ProviderBudgetQuery(Id("scope-1"), "global", 100));
        ProviderApplicationContractInvariants.Validate(new ProviderReplayQuery(Id("op-1"), Id("response-1"), false));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderApplicationContractInvariants.Validate(new ProviderBudgetQuery(Id("scope-1"), "global", 101)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderApplicationContractInvariants.Validate(new ProviderReplayQuery(Id("op-1"), Id("response-1"), true)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void ProviderContractFactoriesBindFrozenV1BytesWithoutReinterpretingThem()
    {
        byte[] localConfigurationV1 = [1, 2, 3];
        byte[] localRunOutputV1 = [4, 5, 6];
        byte[] localCliSummaryV1 = [7, 8, 9];
        ProviderFiniteLimitsContract limits = new(65_536, 73_728, 4_096, 1_048_576, 1, 600_000_000, 120_000);
        EffectiveScanConfigurationV2Document configuration = ProviderContractFactories.CreateEffectiveConfigurationV2(
            Id("config-2"), Id("config-1"), localConfigurationV1, Id("profile-1"), Id("generation-1"), limits);
        RunOutputV2Document output = ProviderContractFactories.CreateRunOutputV2Supplement(
            Id("run-1"), Id("run-output-1"), localRunOutputV1, configuration.ConfigurationId, [], [], [], [], []);
        ProviderOperationSummaryProjection projection = new(
            Id("operation-1"), ProviderOperationState.Settled, "openai", "gpt-5.6-sol", 100, 42, false,
            "retained-response", []);
        CliSummaryV2Document summary = ProviderContractFactories.CreateCliSummaryV2Supplement(
            Id("run-1"), localCliSummaryV1, projection, 1, 32, 16, 4, []);

        Assert.AreEqual(Hash(localConfigurationV1), configuration.LocalConfigurationV1Fingerprint.Value);
        Assert.AreEqual(Hash(localRunOutputV1), output.LocalRunOutputV1.Fingerprint.Value);
        Assert.AreEqual(Hash(localCliSummaryV1), summary.LocalCliSummaryV1Fingerprint.Value);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, localConfigurationV1);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, localRunOutputV1);
        CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, localCliSummaryV1);
    }

    private static OpaqueId Id(string value) => new(value);
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
}
