using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Application.Provider;

public static class ProviderEffectRuntimeAuthorityLoader
{
    public const string SchemaIdentity = "infinium.provider.effect-runtime-authority/v1";

    private static readonly string[] RootProperties =
    [
        "schema_identity", "authority_id", "scope", "kind", "status", "subject_manifest",
        "campaign", "predecessor", "candidate_binding", "review", "owner_decision",
        "not_before_utc", "expires_at_utc", "execution", "limits",
    ];

    public static ProviderEffectRuntimeAuthority LoadAndValidate(
        string manifestPath,
        string expectedSha256,
        DateTimeOffset now)
    {
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(manifestPath));
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!IsHex(expectedSha256, 64) || sha256 != expectedSha256)
        {
            throw new InvalidDataException("The runtime effect authority bytes are stale.");
        }

        using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 16,
        });
        JsonElement root = document.RootElement;
        Exact(root, RootProperties);
        if (root.GetProperty("schema_identity").GetString() != SchemaIdentity
            || root.GetProperty("status").GetString() != "reviewed-and-owner-accepted")
        {
            throw new InvalidDataException("The runtime effect authority is not an accepted v1 document.");
        }

        ProviderEffectAuthorityScope scope = root.GetProperty("scope").GetString() switch
        {
            "effect-free-rehearsal" => ProviderEffectAuthorityScope.EffectFreeRehearsal,
            "external-effect" => ProviderEffectAuthorityScope.ExternalEffect,
            _ => throw new InvalidDataException("The runtime effect authority scope is unknown."),
        };
        ProviderEffectAuthorityKind kind = root.GetProperty("kind").GetString() switch
        {
            "credential-enrollment" => ProviderEffectAuthorityKind.CredentialEnrollment,
            "transport-qualification" => ProviderEffectAuthorityKind.TransportQualification,
            "source-claim-extraction" => ProviderEffectAuthorityKind.SourceClaimExtraction,
            "candidate-investigation" => ProviderEffectAuthorityKind.CandidateInvestigation,
            _ => throw new InvalidDataException("The runtime effect authority kind is unknown."),
        };

        JsonElement subject = root.GetProperty("subject_manifest");
        Exact(subject, "id", "sha256");
        JsonElement campaign = root.GetProperty("campaign");
        Exact(campaign, "id", "sha256");
        JsonElement predecessor = root.GetProperty("predecessor");
        Exact(predecessor, "ledger_event_hash", "evidence_id", "evidence_sha256");
        JsonElement candidate = root.GetProperty("candidate_binding");
        Exact(candidate, "implementation_commit", "coordinator_sha256", "helper_sha256");
        JsonElement review = root.GetProperty("review");
        Exact(review, "evidence_id", "evidence_sha256");
        JsonElement owner = root.GetProperty("owner_decision");
        Exact(owner, "decision_id", "decision_sha256");
        JsonElement executionNode = root.GetProperty("execution");
        Exact(executionNode, "output_root_relative", "ledger_path_relative", "product_state_root_relative",
            "coordinator_path_relative", "helper_path_relative");
        JsonElement limitsNode = root.GetProperty("limits");
        Exact(limitsNode, "helper_launches", "credential_native_calls", "provider_starts",
            "dns_resolutions", "billable_operations", "literal_loopback_starts",
            "automatic_retry", "fourth_call_permitted");

        DateTimeOffset notBefore = ParseUtc(root.GetProperty("not_before_utc").GetString(), "not_before_utc");
        DateTimeOffset expires = ParseUtc(root.GetProperty("expires_at_utc").GetString(), "expires_at_utc");
        if (now.Offset != TimeSpan.Zero || notBefore >= expires || now < notBefore || now >= expires)
        {
            throw new InvalidDataException("The runtime effect authority is outside its exact UTC interval.");
        }

        ProviderEffectAuthorityLimits limits = new(
            limitsNode.GetProperty("helper_launches").GetInt32(),
            limitsNode.GetProperty("credential_native_calls").GetInt32(),
            limitsNode.GetProperty("provider_starts").GetInt32(),
            limitsNode.GetProperty("dns_resolutions").GetInt32(),
            limitsNode.GetProperty("billable_operations").GetInt32(),
            limitsNode.GetProperty("literal_loopback_starts").GetInt32(),
            limitsNode.GetProperty("automatic_retry").GetBoolean(),
            limitsNode.GetProperty("fourth_call_permitted").GetBoolean());
        ValidateLimits(scope, kind, limits);
        ProviderEffectAuthorityExecution execution = new(
            RequireRelativePath(executionNode.GetProperty("output_root_relative").GetString()),
            RequireRelativePath(executionNode.GetProperty("ledger_path_relative").GetString()),
            RequireRelativePath(executionNode.GetProperty("product_state_root_relative").GetString()),
            RequireRelativePath(executionNode.GetProperty("coordinator_path_relative").GetString()),
            RequireRelativePath(executionNode.GetProperty("helper_path_relative").GetString()));

        string predecessorHash = predecessor.GetProperty("ledger_event_hash").GetString() ?? string.Empty;
        string predecessorEvidenceId = predecessor.GetProperty("evidence_id").GetString() ?? string.Empty;
        string predecessorEvidenceSha = predecessor.GetProperty("evidence_sha256").GetString() ?? string.Empty;
        bool credential = kind == ProviderEffectAuthorityKind.CredentialEnrollment;
        if (credential
            ? predecessorHash != "none" || predecessorEvidenceId != "none" || predecessorEvidenceSha != "none"
            : !IsHex(predecessorHash, 64) || !IsIdentity(predecessorEvidenceId)
                || !IsHex(predecessorEvidenceSha, 64))
        {
            throw new InvalidDataException("The runtime effect authority predecessor is missing or broadened.");
        }

        ProviderEffectRuntimeAuthority result = new(
            RequireIdentity(root.GetProperty("authority_id").GetString()), scope, kind,
            RequireIdentity(subject.GetProperty("id").GetString()), RequireHex(subject.GetProperty("sha256").GetString(), 64),
            RequireIdentity(campaign.GetProperty("id").GetString()), RequireHex(campaign.GetProperty("sha256").GetString(), 64),
            predecessorHash, predecessorEvidenceId, predecessorEvidenceSha,
            RequireHex(candidate.GetProperty("implementation_commit").GetString(), 40),
            RequireHex(candidate.GetProperty("coordinator_sha256").GetString(), 64),
            RequireHex(candidate.GetProperty("helper_sha256").GetString(), 64),
            RequireIdentity(review.GetProperty("evidence_id").GetString()),
            RequireHex(review.GetProperty("evidence_sha256").GetString(), 64),
            RequireIdentity(owner.GetProperty("decision_id").GetString()),
            RequireHex(owner.GetProperty("decision_sha256").GetString(), 64),
            notBefore, expires, execution, limits, sha256);
        return result;
    }

    public static void RequireEffectFreeRehearsal(ProviderEffectRuntimeAuthority authority) =>
        _ = authority.Scope == ProviderEffectAuthorityScope.EffectFreeRehearsal
            ? true
            : throw new InvalidOperationException("Only typed effect-free rehearsal authority is accepted here.");

    public static void RequireExternalEffect(
        ProviderEffectRuntimeAuthority authority,
        ProviderEffectAuthorityKind expectedKind)
    {
        if (authority.Scope != ProviderEffectAuthorityScope.ExternalEffect || authority.Kind != expectedKind)
        {
            throw new InvalidOperationException("The typed runtime authority does not admit this external effect.");
        }
    }

    public static void ValidateDurableBinding(
        ProviderEffectRuntimeAuthority authority,
        M1Slice6CampaignIdentity campaign,
        M1Slice6CampaignLedgerEntry predecessor,
        ProviderEffectAuthorityKind expectedKind,
        string subjectManifestId,
        string subjectManifestSha256,
        bool requireExternalEffect)
    {
        if (requireExternalEffect)
        {
            RequireExternalEffect(authority, expectedKind);
        }
        else
        {
            RequireEffectFreeRehearsal(authority);
            if (authority.Kind != expectedKind)
            {
                throw new InvalidOperationException("The rehearsal authority kind does not match the requested stage.");
            }
        }
        if (authority.SubjectManifestId != subjectManifestId
            || authority.SubjectManifestSha256 != subjectManifestSha256
            || authority.CampaignId != campaign.CampaignId
            || authority.CampaignManifestSha256 != campaign.CampaignManifestSha256)
        {
            throw new InvalidDataException("The runtime effect authority does not bind the exact campaign candidate and subject.");
        }

        bool credential = expectedKind == ProviderEffectAuthorityKind.CredentialEnrollment;
        if (credential
            ? predecessor.State != M1Slice6CampaignState.Ready
                || authority.PredecessorLedgerEventHash != "none"
                || authority.PredecessorEvidenceId != "none"
                || authority.PredecessorEvidenceSha256 != "none"
            : authority.PredecessorLedgerEventHash != predecessor.EventHash
                || authority.PredecessorEvidenceId != predecessor.EvidenceId
                || authority.PredecessorEvidenceSha256 != predecessor.EvidenceSha256)
        {
            throw new InvalidDataException("The runtime effect authority has a stale durable predecessor.");
        }
        if (!credential && (predecessor.EvidenceId.Length == 0 || predecessor.EvidenceSha256.Length == 0))
        {
            throw new InvalidDataException("A provider stage requires exact accepted predecessor evidence.");
        }
    }

    public static void ValidateExecutableBinding(ProviderEffectRuntimeAuthority authority,
        Assembly coordinatorAssembly, string coordinatorSha256, string helperSha256)
    {
        string informationalVersion = coordinatorAssembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? string.Empty;
        Match revision = Regex.Match(informationalVersion, @"\+(?<sha>[0-9a-f]{40})$");
        if (!revision.Success || revision.Groups["sha"].Value != authority.ImplementationCommit
            || coordinatorSha256 != authority.CoordinatorSha256
            || helperSha256 != authority.HelperSha256)
        {
            throw new InvalidDataException("The runtime authority implementation or executable closure differs from the executing candidate.");
        }
    }

    public static void ValidateExecutionBinding(
        ProviderEffectRuntimeAuthority authority,
        string repositoryRoot,
        string outputRoot,
        string ledgerPath,
        string productStateRoot,
        string coordinatorPath,
        string helperPath)
    {
        repositoryRoot = Path.GetFullPath(repositoryRoot);
        string[] expected =
        [
            Resolve(repositoryRoot, authority.Execution.OutputRootRelative),
            Resolve(repositoryRoot, authority.Execution.LedgerPathRelative),
            Resolve(repositoryRoot, authority.Execution.ProductStateRootRelative),
            Resolve(repositoryRoot, authority.Execution.CoordinatorPathRelative),
            Resolve(repositoryRoot, authority.Execution.HelperPathRelative),
        ];
        string[] actual = new[] { outputRoot, ledgerPath, productStateRoot, coordinatorPath, helperPath }
            .Select(Path.GetFullPath).ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The runtime effect authority execution-path binding is stale.");
        }
    }

    private static string Resolve(string repositoryRoot, string relative)
    {
        string result = Path.GetFullPath(Path.Combine(repositoryRoot,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(repositoryRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A runtime effect authority path escaped the repository.");
        }
        return result;
    }

    private static void ValidateLimits(ProviderEffectAuthorityScope scope, ProviderEffectAuthorityKind kind,
        ProviderEffectAuthorityLimits limits)
    {
        if (limits.AutomaticRetry || limits.FourthCallPermitted)
        {
            throw new InvalidDataException("Retry and fourth-call authority are prohibited.");
        }
        ProviderEffectAuthorityLimits expected = scope switch
        {
            ProviderEffectAuthorityScope.EffectFreeRehearsal => new(0, 0, 0, 0, 0,
                kind == ProviderEffectAuthorityKind.CredentialEnrollment ? 0 : 1, false, false),
            _ when kind == ProviderEffectAuthorityKind.CredentialEnrollment =>
                new(1, 4, 0, 0, 0, 0, false, false),
            _ => new(1, 2, 1, 1, 1, 0, false, false),
        };
        if (limits != expected)
        {
            throw new InvalidDataException("The runtime effect authority limits are not the exact closed envelope.");
        }
    }

    private static void Exact(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.EnumerateObject().Select(property => property.Name)
                .SequenceEqual(names, StringComparer.Ordinal))
        {
            throw new InvalidDataException("A runtime effect authority object has an unknown, missing, duplicate, or reordered property.");
        }
    }

    private static DateTimeOffset ParseUtc(string? value, string name)
    {
        if (!DateTimeOffset.TryParseExact(value, "O", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out DateTimeOffset parsed)
            || parsed.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException($"The runtime effect authority {name} is not exact UTC.");
        }
        return parsed;
    }

    private static string RequireIdentity(string? value) => IsIdentity(value)
        ? value!
        : throw new InvalidDataException("A runtime effect authority identity is malformed.");

    private static string RequireHex(string? value, int length) => IsHex(value, length)
        ? value!
        : throw new InvalidDataException("A runtime effect authority digest is malformed.");

    private static string RequireRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 300 || value.Contains('\\')
            || Path.IsPathRooted(value) || value.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw new InvalidDataException("A runtime effect authority path is not an exact repository-relative path.");
        }
        return value;
    }

    private static bool IsIdentity(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 200 && !value.Any(char.IsControl);

    private static bool IsHex(string? value, int length) => value?.Length == length
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
