using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Provider;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class CredentialSemanticRolloverPolicyTests
{
    private static readonly M1Slice6CampaignEffectObservation Zero = new(0, 0, 0, 0, 0, 0, 0, false, true);

    [TestMethod]
    public void ExactBuildAndCandidateRebindingIsAccepted()
    {
        (JsonDocument prior, JsonDocument replacement) = Documents(node => Rebind(node));
        using (prior)
        using (replacement)
        {
            CredentialSemanticRolloverPolicy.ValidatePreEffectNonBroadening(prior.RootElement, replacement.RootElement, Zero);
        }
    }

    [TestMethod]
    public void EveryEffectBoundaryClosesRollover()
    {
        (JsonDocument prior, JsonDocument replacement) = Documents(node => Rebind(node));
        using (prior)
        using (replacement)
        {
            M1Slice6CampaignEffectObservation[] mutations =
            [
                Zero with { CredentialHelperLaunchCount = 1 }, Zero with { CredentialHelperReadinessCount = 1 },
                Zero with { CredentialAuthorityLockCount = 1 }, Zero with { CredentialManagerCallCount = 1 },
                Zero with { ProfileMaterializationCount = 1 }, Zero with { DnsOrPublicNetworkCount = 1 },
                Zero with { ProviderDispatchCount = 1 }, Zero with { ApiKeyObserved = true },
                Zero with { ProductionOutputRootsAbsent = false },
            ];
            foreach (M1Slice6CampaignEffectObservation mutation in mutations)
            {
                Assert.ThrowsExactly<InvalidOperationException>(() => CredentialSemanticRolloverPolicy.ValidatePreEffectNonBroadening(
                    prior.RootElement, replacement.RootElement, mutation));
            }
        }
    }

    [TestMethod]
    public void CredentialEnvelopeExpiryCallGrammarUxAndExecutionMutationsAreRejected()
    {
        Action<JsonObject>[] mutations =
        [
            root => root["expires_at_utc"] = "2026-08-22T23:59:00Z",
            root => root["profile"]!["generation_id"] = "g-other",
            root => root["native_boundary"]!["maximum_calls"]!["CredWriteW"] = 2,
            root => root["m1_entry_surface"]!["masked"] = false,
            root => root["durable_state"]!["active_unverified_request_gate"] = "accept",
            root => root["execution"]!["provider_operation"] = "permitted",
            root => root["stop_conditions"]!["native_failure"] = "retry",
        ];
        foreach (Action<JsonObject> mutate in mutations)
        {
            (JsonDocument prior, JsonDocument replacement) = Documents(root => { Rebind(root); mutate(root); });
            using (prior)
            using (replacement)
            {
                Assert.ThrowsExactly<InvalidOperationException>(() => CredentialSemanticRolloverPolicy.ValidatePreEffectNonBroadening(
                    prior.RootElement, replacement.RootElement, Zero));
            }
        }
    }

    private static (JsonDocument Prior, JsonDocument Replacement) Documents(Action<JsonObject> mutate)
    {
        string path = FindRepositoryFile("docs", "plans", "milestones", "m1", "slices", "s6", "wp9-production-profile-authorization.v1.json");
        JsonObject priorNode = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        JsonObject replacementNode = priorNode.DeepClone().AsObject();
        mutate(replacementNode);
        return (JsonDocument.Parse(priorNode.ToJsonString()), JsonDocument.Parse(replacementNode.ToJsonString()));
    }

    private static void Rebind(JsonObject root)
    {
        const string commit = "1111111111111111111111111111111111111111";
        root["candidate_binding"]!["close_ready_implementation_commit"] = commit;
        root["release_build"]!["source_commit"] = commit;
        root["release_build"]!["build_command"] = "dotnet build Infinium.sln -c Release --no-restore --nologo --no-incremental -p:SourceRevisionId=" + commit;
        root["release_build"]!["coordinator_sha256"] = new string('2', 64);
        root["release_build"]!["helper_sha256"] = new string('3', 64);
        root["release_build"]!["binary_inventory_sha256"] = new string('4', 64);
        root["release_build"]!["binary_inventory_file_count"] = 126;
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            string path = Path.Combine([current.FullName, .. parts]);
            if (File.Exists(path))
            {
                return path;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
