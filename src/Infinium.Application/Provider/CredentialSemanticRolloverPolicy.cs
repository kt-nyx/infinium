using System.Text.Json;

namespace Infinium.Application.Provider;

public static class CredentialSemanticRolloverPolicy
{
    private static readonly string[] ExactTopLevelFields =
    [
        "schema_identity", "manifest_id", "packet_kind", "status", "effect_authority", "expires_at_utc",
        "predecessor_binding", "owner_authorization", "provider_intent", "official_document_refresh", "profile",
        "native_boundary", "m1_entry_surface", "future_product_ux", "durable_state", "output", "stop_conditions",
        "execution",
    ];

    private static readonly string[] ExactCandidateFields =
    [
        "accepted_wp8_verification_commit", "accepted_wp8_evidence_commit", "accepted_wp8_non_live_all_sha256",
        "accepted_wp8_pre_live_sha256", "accepted_wp8_direct_layer6_sha256",
    ];

    private static readonly string[] ReleaseMutableFields =
    [
        "source_commit", "build_command", "coordinator_sha256", "helper_sha256",
        "binary_inventory_file_count", "binary_inventory_sha256",
    ];

    public static void ValidatePreEffectNonBroadening(
        JsonElement prior,
        JsonElement replacement,
        M1Slice6CampaignEffectObservation effects)
    {
        if (!effects.IsExactZeroEffect)
        {
            throw new InvalidOperationException("Credential semantic rollover closed at the first effect boundary observation.");
        }
        RequireExactNames(prior, replacement);
        foreach (string field in ExactTopLevelFields)
        {
            RequireEqual(prior.GetProperty(field), replacement.GetProperty(field), field);
        }

        JsonElement priorCandidate = prior.GetProperty("candidate_binding");
        JsonElement replacementCandidate = replacement.GetProperty("candidate_binding");
        RequireExactNames(priorCandidate, replacementCandidate);
        foreach (string field in ExactCandidateFields)
        {
            RequireEqual(priorCandidate.GetProperty(field), replacementCandidate.GetProperty(field), "candidate_binding." + field);
        }
        RequireGitSha(replacementCandidate.GetProperty("close_ready_implementation_commit").GetString(),
            "candidate_binding.close_ready_implementation_commit");

        JsonElement priorRelease = prior.GetProperty("release_build");
        JsonElement replacementRelease = replacement.GetProperty("release_build");
        RequireExactNames(priorRelease, replacementRelease);
        foreach (JsonProperty property in priorRelease.EnumerateObject())
        {
            if (!ReleaseMutableFields.Contains(property.Name, StringComparer.Ordinal))
            {
                RequireEqual(property.Value, replacementRelease.GetProperty(property.Name), "release_build." + property.Name);
            }
        }
        RequireGitSha(replacementRelease.GetProperty("source_commit").GetString(), "release_build.source_commit");
        foreach (string field in new[] { "coordinator_sha256", "helper_sha256", "binary_inventory_sha256" })
        {
            RequireSha256(replacementRelease.GetProperty(field).GetString(), "release_build." + field);
        }
        if (replacementRelease.GetProperty("binary_inventory_file_count").GetInt32() <= 0)
        {
            throw new InvalidOperationException("The replacement Release execution closure is incomplete.");
        }
        string expectedBuild = "dotnet build Infinium.sln -c Release --no-restore --nologo --no-incremental -p:SourceRevisionId="
            + replacementRelease.GetProperty("source_commit").GetString();
        if (replacementRelease.GetProperty("build_command").GetString() != expectedBuild)
        {
            throw new InvalidOperationException("The replacement Release build is not pinned to its reviewed source.");
        }
    }

    private static void RequireExactNames(JsonElement prior, JsonElement replacement)
    {
        string[] first = prior.EnumerateObject().Select(item => item.Name).ToArray();
        string[] second = replacement.EnumerateObject().Select(item => item.Name).ToArray();
        if (!first.SequenceEqual(second, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Semantic rollover added, removed, or reordered a field.");
        }
    }

    private static void RequireEqual(JsonElement prior, JsonElement replacement, string path)
    {
        if (!JsonElement.DeepEquals(prior, replacement))
        {
            throw new InvalidOperationException($"Semantic rollover broadened or changed {path}.");
        }
    }

    private static void RequireGitSha(string? value, string path)
    {
        if (!IsLowerHex(value, 40))
        {
            throw new InvalidOperationException($"{path} is not an exact Git identity.");
        }
    }

    private static void RequireSha256(string? value, string path)
    {
        if (!IsLowerHex(value, 64))
        {
            throw new InvalidOperationException($"{path} is not an exact SHA-256 identity.");
        }
    }

    private static bool IsLowerHex(string? value, int length) => value is not null && value.Length == length
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public sealed record M1Slice6CampaignEffectObservation(
    long CredentialHelperLaunchCount,
    long CredentialHelperReadinessCount,
    long CredentialAuthorityLockCount,
    long CredentialManagerCallCount,
    long ProfileMaterializationCount,
    long DnsOrPublicNetworkCount,
    long ProviderDispatchCount,
    bool ApiKeyObserved,
    bool ProductionOutputRootsAbsent)
{
    public bool IsExactZeroEffect => CredentialHelperLaunchCount == 0 && CredentialHelperReadinessCount == 0
        && CredentialAuthorityLockCount == 0 && CredentialManagerCallCount == 0 && ProfileMaterializationCount == 0
        && DnsOrPublicNetworkCount == 0 && ProviderDispatchCount == 0 && !ApiKeyObserved && ProductionOutputRootsAbsent;
}
