using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderBudgetEvaluationTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    public void ProviderCapabilityPublicPackagesAgreeWithDynamicProductionCatalog()
    {
        ValidateRegisteredPackages("PROVIDER-CAPABILITY", 2);
        ProviderCapabilitySnapshotContract capability = M1ProviderCatalog.Capability;
        Assert.AreEqual("openai", capability.Provider);
        Assert.AreEqual("gpt-5.6-sol", capability.Model);
        Assert.AreEqual(272000L, capability.MaximumContextTokens);
        Assert.AreEqual(5, M1ProviderCatalog.Price.Rules.Count);
        Assert.IsFalse(capability.Store || capability.Stream || capability.Background);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void ProviderAuthorityPublicPackagesAgreeWithDynamicSimulatorMatrix()
    {
        ValidateRegisteredPackages("PROVIDER-AUTHORITY", 2);
        ProviderFiniteLimitsContract limits = new(16_384, 20_480, 256, 262_144, 1, 140_000_000, 60_000);
        DeterministicProviderTranscript ambiguous = DeterministicProviderSimulator.Execute(
            ProviderSimulatorOutcome.AmbiguousStart, limits, new UtcTimestamp(DateTimeOffset.UnixEpoch));
        Assert.IsTrue(ambiguous.TransportStartAmbiguous);
        Assert.IsFalse(ambiguous.RetryPermitted);
        Assert.IsFalse(ambiguous.NetworkUsed);
        Assert.IsFalse(ambiguous.CredentialAccessed);
        Assert.IsTrue(Enum.GetValues<ProviderSimulatorOutcome>().Where(value => value != ProviderSimulatorOutcome.Unspecified)
            .All(value => !DeterministicProviderSimulator.Execute(value, limits, new UtcTimestamp(DateTimeOffset.UnixEpoch)).NetworkUsed));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void AtomicBudgetPublicPackagesAgreeWithFiniteProductionVectorRules()
    {
        ValidateRegisteredPackages("PLAT-BUDGET", 2);
        ProviderBudgetVectorContract reservation = new(1, 10, 5, 15, 2, 0, 0, 0, 100);
        ProviderBudgetVectorContract limit = new(1, 10, 5, 15, 2, 0, 0, 0, 100);
        Assert.IsTrue(ProviderBudgetVectorContract.FitsWithin(ProviderBudgetVectorContract.Zero, reservation, limit));
        Assert.IsFalse(ProviderBudgetVectorContract.FitsWithin(reservation, reservation, limit));
        Assert.IsFalse(ProviderBudgetVectorContract.FitsWithin(
            new(long.MaxValue, 0, 0, 0, 0, 0, 0, 0, 0), reservation,
            new(long.MaxValue, 10, 5, 15, 2, 0, 0, 0, 100)));
    }

    private static void ValidateRegisteredPackages(string identityFragment, int expectedCount)
    {
        string repositoryRoot = FindRepositoryRoot();
        using JsonDocument registry = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(repositoryRoot, "fixtures", "public", "public-fixture-registry.v1.json")));
        JsonElement[] packages = registry.RootElement.GetProperty("packages").EnumerateArray()
            .Where(item => item.GetProperty("package_identity").GetString()!.Contains(identityFragment, StringComparison.Ordinal))
            .ToArray();
        Assert.AreEqual(expectedCount, packages.Length);
        foreach (JsonElement package in packages)
        {
            string authority = Path.Combine(repositoryRoot,
                package.GetProperty("authority_file").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            byte[] bytes = File.ReadAllBytes(authority);
            Assert.AreEqual(package.GetProperty("authority_bytes").GetInt64(), bytes.LongLength);
            Assert.AreEqual(package.GetProperty("authority_sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
            string directory = Path.GetDirectoryName(authority)!;
            using JsonDocument manifest = JsonDocument.Parse(bytes);
            Assert.IsTrue(manifest.RootElement.GetProperty("answer_free_input").GetBoolean());
            using JsonDocument input = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "input.json")));
            Assert.IsFalse(input.RootElement.EnumerateObject().Any(property =>
                property.Name.StartsWith("expected", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("oracle", StringComparison.OrdinalIgnoreCase)));
            Assert.IsTrue(File.Exists(Path.Combine(directory, "oracle.json")));
        }
        Assert.IsTrue(Directory.Exists(Path.Combine(repositoryRoot, "fixtures", "public", "platform", "provider-budget")));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Infinium repository root was not found.");
    }
}
