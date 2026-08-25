using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.CredentialHelper;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class DevelopmentProviderInvocationTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public async Task OfflineInvocationExercisesFullRequestBudgetAndEvidencePathWithoutNetwork()
    {
        DevelopmentProviderInvocationManifest manifest = Manifest();

        byte[] evidence = await DevelopmentProviderInvocationRunner.RunAsync(
            manifest,
            live: false);

        using JsonDocument document = JsonDocument.Parse(evidence);
        JsonElement root = document.RootElement;
        Assert.AreEqual(
            "infinium.development-provider-evidence/v1",
            root.GetProperty("schema_identity").GetString());
        Assert.AreEqual("offline-fake-provider", root.GetProperty("mode").GetString());
        Assert.IsFalse(root.GetProperty("outcome").GetProperty("NetworkUsed").GetBoolean());
        Assert.AreEqual(0, root.GetProperty("outcome").GetProperty("SendCount").GetInt32());
        Assert.AreEqual(
            "settled",
            root.GetProperty("budget").GetProperty("State").GetString());
        Assert.IsTrue(
            root.GetProperty("credential").GetProperty("target_fingerprint_sha256")
                .GetString()!.Length == 64);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void RequestAuthorityRejectsWrongProviderProfileAndInsufficientReservation()
    {
        DevelopmentProviderInvocationManifest manifest = Manifest();
        Assert.ThrowsExactly<InvalidDataException>(() =>
            ProviderRequestAuthority.Authorize(
                manifest with { Model = "fallback-model" },
                live: false));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ProviderRequestAuthority.Authorize(
                manifest with
                {
                    Limits = manifest.Limits with
                    {
                        MaximumLocalCostNanoUsd = 1,
                    },
                },
                live: false));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public async Task OfflineInvocationHonorsCancellation()
    {
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            DevelopmentProviderInvocationRunner.RunAsync(
                Manifest(),
                live: false,
                cancellationToken: cancelled.Token));
    }

    private static DevelopmentProviderInvocationManifest Manifest()
    {
        using JsonDocument schema = JsonDocument.Parse(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["status"],
              "properties": {
                "status": { "type": "string" }
              }
            }
            """);
        return new(
            "development-provider-test",
            ProviderOperationKind.TransportQualification,
            new("development-profile", "generation-1", "account-1", "project-1"),
            OpenAiProviderProfileCatalog.Capability.Identity.Value,
            OpenAiProviderProfileCatalog.Price.Identity.Value,
            OpenAiProviderProfileCatalog.Capability.Model,
            OpenAiProviderProfileCatalog.Capability.ServiceTier,
            new(
                MaximumInputTokens: 20_480,
                MaximumOutputTokens: 256,
                DeadlineMilliseconds: 60_000,
                MaximumLocalCostNanoUsd: 140_000_000,
                ProjectCostBoundaryNanoUsd: ProviderUsageBudget.OwnerMaximumProjectBoundaryNanoUsd),
            new(
                "Return the requested transport status.",
                "Offline conformance input.",
                schema.RootElement.Clone(),
                ProviderAdapterTestData.SafetyIdentifier));
    }
}
