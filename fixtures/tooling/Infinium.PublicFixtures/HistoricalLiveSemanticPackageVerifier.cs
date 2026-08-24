using System.Security.Cryptography;
using System.Text.Json;

namespace Infinium.PublicFixtures;

public sealed record HistoricalLiveSemanticPackageReceipt(
    int SemanticAdmissionPackageCount,
    int HistoricalRegistryEntryCount,
    int VerifiedFileBindingCount,
    int DeclaredHistoricalExternalBindingCount,
    string RegistrySha256);

/// <summary>
/// Verifies immutable historical package bindings and the current deferral boundary.
/// It never executes product code or interprets expected semantic labels.
/// </summary>
public static class HistoricalLiveSemanticPackageVerifier
{
    private const string SemanticPrefix = "fixtures/public/provider/semantic-admission/";
    private static readonly HashSet<string> ExpectedSemanticPackages = Enumerable.Range(1, 13)
        .Select(version => $"S6-SEMANTIC-ADMISSION-VAL-v{version}")
        .ToHashSet(StringComparer.Ordinal);

    public static HistoricalLiveSemanticPackageReceipt Verify(string repositoryRoot)
    {
        string root = Path.GetFullPath(repositoryRoot);
        string authorityPath = Resolve(root, "docs/evaluation/repository-evaluation-authority.v1.json");
        using JsonDocument authority = JsonDocument.Parse(File.ReadAllBytes(authorityPath));
        JsonElement policy = authority.RootElement.GetProperty("semantic_oracle_policy");
        Require(policy.GetProperty("status").GetString() == "deferred", "Independent semantic evaluation must be deferred.");
        Require(policy.GetProperty("current_authority_package").ValueKind == JsonValueKind.Null, "No semantic package may grant current authority.");
        Require(!policy.GetProperty("gates_m1_acceptance").GetBoolean(), "Semantic-oracle PASS must not gate M1.");
        Require(!policy.GetProperty("gates_m2_acceptance").GetBoolean(), "Semantic-oracle PASS must not gate M2.");
        Require(policy.GetProperty("reconsideration_boundary").GetString() == "m2-accepted-m3-planning", "Unexpected semantic evaluation reconsideration boundary.");

        string registryRelative = "fixtures/public/public-fixture-registry.v3.json";
        string registryPath = Resolve(root, registryRelative);
        byte[] registryBytes = File.ReadAllBytes(registryPath);
        using JsonDocument registry = JsonDocument.Parse(registryBytes);
        JsonElement family = registry.RootElement.GetProperty("family_classifications")
            .EnumerateArray().Single(value => value.GetProperty("family_id").GetString() == "semantic-admission");
        Require(family.GetProperty("disposition").GetString() == "historical-non-authorizing", "Semantic-admission family must be historical and non-authorizing.");
        Require(family.GetProperty("required_partition").GetString() == "development", "Semantic-admission family must remain development evidence.");
        Require(family.GetProperty("current_validation_authority_package").ValueKind == JsonValueKind.Null, "Semantic-admission validation authority must be empty.");

        JsonElement[] packages = registry.RootElement.GetProperty("packages").EnumerateArray().ToArray();
        Require(packages.Length == 56
            && registry.RootElement.GetProperty("package_count").GetInt32() == packages.Length,
            "Registry must contain the exact closed 56-package set.");
        Require(packages.Select(PackageIdentity).Distinct(StringComparer.Ordinal).Count() == packages.Length, "Registry package identities must be unique.");

        JsonElement[] semanticPackages = packages.Where(value => PackagePath(value).StartsWith(SemanticPrefix, StringComparison.Ordinal)).ToArray();
        Require(semanticPackages.Select(PackageIdentity).ToHashSet(StringComparer.Ordinal)
            .SetEquals(ExpectedSemanticPackages),
            "The historical semantic-admission family must be exactly v1 through v13.");
        int verified = 0;
        int historicalExternalBindings = 0;
        foreach (JsonElement package in semanticPackages)
        {
            Require(package.GetProperty("partition").GetString() == "development", $"{PackageIdentity(package)} must remain development evidence.");
            Require(package.TryGetProperty("authority_status", out JsonElement status) &&
                status.GetString()!.StartsWith("historical-", StringComparison.Ordinal), $"{PackageIdentity(package)} must be historical and non-authorizing.");
            verified += VerifyRegistryAuthority(root, package, requireNoCurrentAuthority: true,
                ref historicalExternalBindings);
        }

        JsonElement[] historical = packages.Where(value => value.TryGetProperty("authority_status", out JsonElement status) &&
            status.GetString()!.StartsWith("historical-", StringComparison.Ordinal)).ToArray();
        foreach (JsonElement package in historical.Where(value => !PackagePath(value).StartsWith(SemanticPrefix, StringComparison.Ordinal)))
        {
            verified += VerifyRegistryAuthority(root, package, requireNoCurrentAuthority: true,
                ref historicalExternalBindings);
        }

        return new HistoricalLiveSemanticPackageReceipt(
            semanticPackages.Length,
            historical.Length,
            verified,
            historicalExternalBindings,
            Convert.ToHexString(SHA256.HashData(registryBytes)).ToLowerInvariant());
    }

    private static int VerifyRegistryAuthority(
        string root,
        JsonElement package,
        bool requireNoCurrentAuthority,
        ref int historicalExternalBindings)
    {
        string authorityRelative = package.GetProperty("authority_file").GetString()!;
        HashSet<string> boundPaths = new(StringComparer.Ordinal) { authorityRelative };
        byte[] bytes = File.ReadAllBytes(Resolve(root, authorityRelative));
        Require(bytes.Length == package.GetProperty("authority_bytes").GetInt32(), $"Authority byte count drifted: {authorityRelative}");
        Require(Sha(bytes) == package.GetProperty("authority_sha256").GetString(), $"Authority hash drifted: {authorityRelative}");
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement value = document.RootElement;
        string authorityIdentity = AuthorityIdentity(value);
        Require(authorityIdentity == PackageIdentity(package), $"Authority identity drifted: {authorityRelative}");
        if (requireNoCurrentAuthority)
        {
            Require(!value.TryGetProperty("current_validation_authority", out JsonElement current) || !current.GetBoolean(), $"Historical semantic package grants current validation authority: {authorityRelative}");
            Require(!value.TryGetProperty("current_product_authority", out JsonElement product) || !product.GetBoolean(), $"Historical package grants current product authority: {authorityRelative}");
            Require(!value.TryGetProperty("current_semantic_authority", out JsonElement semantic) || !semantic.GetBoolean(), $"Historical package grants current semantic authority: {authorityRelative}");
        }

        int verified = 1;
        string packageRoot = PackagePath(package);
        verified += VerifyFileIdentities(root, packageRoot, value, boundPaths,
            ref historicalExternalBindings);
        verified += VerifyReclassification(root, packageRoot, value, PackageIdentity(package), boundPaths,
            ref historicalExternalBindings);
        if (value.TryGetProperty("retained_manifest", out JsonElement retained))
        {
            string relative = packageRoot + "/" + retained.GetProperty("path").GetString();
            boundPaths.Add(relative);
            byte[] bound = File.ReadAllBytes(Resolve(root, relative));
            Require(bound.Length == retained.GetProperty("bytes").GetInt32(), $"Retained manifest byte count drifted: {relative}");
            Require(Sha(bound) == retained.GetProperty("sha256").GetString(), $"Retained manifest hash drifted: {relative}");
            Require(!value.TryGetProperty("current_semantic_authority", out JsonElement current) || !current.GetBoolean(), $"Historical reclassification grants semantic authority: {authorityRelative}");
            using JsonDocument retainedManifest = JsonDocument.Parse(bound);
            JsonElement retainedValue = retainedManifest.RootElement;
            string retainedIdentity = AuthorityIdentity(retainedValue);
            Require(retainedIdentity == PackageIdentity(package), $"Retained manifest identity drifted: {relative}");
            verified += 1 + VerifyFileIdentities(root, packageRoot, retainedValue, boundPaths,
                ref historicalExternalBindings);
        }
        string[] packageFiles = Directory.EnumerateFiles(ResolveDirectory(root, packageRoot), "*",
                SearchOption.TopDirectoryOnly)
            .Select(path => packageRoot + "/" + Path.GetFileName(path))
            .Order(StringComparer.Ordinal).ToArray();
        string[] boundPackageFiles = boundPaths.Where(path => path.StartsWith(packageRoot + "/",
                StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        Require(packageFiles.SequenceEqual(boundPackageFiles, StringComparer.Ordinal),
            $"Historical package file closure drifted: {packageRoot}");
        return verified;
    }

    private static int VerifyFileIdentities(
        string root,
        string packageRoot,
        JsonElement manifest,
        HashSet<string> boundPaths,
        ref int historicalExternalBindings)
    {
        int verified = 0;
        foreach (string property in new[] { "product_input", "predecessor_manifest", "oracle" })
        {
            if (manifest.TryGetProperty(property, out JsonElement binding)
                && binding.ValueKind == JsonValueKind.Object
                && binding.TryGetProperty("path", out _)
                && binding.TryGetProperty("bytes", out _)
                && binding.TryGetProperty("sha256", out _))
            {
                verified += VerifyBinding(root, packageRoot, binding, boundPaths);
            }
        }
        if (!manifest.TryGetProperty("file_identities", out JsonElement identities))
        {
            return verified;
        }
        foreach (JsonElement identity in identities.EnumerateArray())
        {
            string identityPath = identity.GetProperty("path").GetString()!;
            string relative = identityPath.Contains('/')
                ? identityPath
                : packageRoot + "/" + identityPath;
            boundPaths.Add(relative);
            byte[] bound = File.ReadAllBytes(Resolve(root, relative));
            Require(bound.Length == identity.GetProperty("bytes").GetInt32(), $"Historical byte count drifted: {relative}");
            Require(Sha(bound) == identity.GetProperty("sha256").GetString(), $"Historical hash drifted: {relative}");
            string role = identity.TryGetProperty("role", out JsonElement roleValue)
                ? roleValue.GetString() ?? string.Empty : string.Empty;
            if (role.Contains("reclassification", StringComparison.Ordinal))
            {
                using JsonDocument reclassification = JsonDocument.Parse(bound);
                verified += VerifyReclassification(root, packageRoot, reclassification.RootElement,
                    AuthorityIdentity(manifest),
                    boundPaths, ref historicalExternalBindings);
            }
            verified++;
        }
        return verified;
    }

    private static int VerifyBinding(
        string root,
        string packageRoot,
        JsonElement identity,
        HashSet<string> boundPaths)
    {
        string identityPath = identity.GetProperty("path").GetString()!;
        string relative = identityPath.Contains('/') ? identityPath : packageRoot + "/" + identityPath;
        boundPaths.Add(relative);
        byte[] bound = File.ReadAllBytes(Resolve(root, relative));
        Require(bound.Length == identity.GetProperty("bytes").GetInt32(),
            $"Historical byte count drifted: {relative}");
        Require(Sha(bound) == identity.GetProperty("sha256").GetString(),
            $"Historical hash drifted: {relative}");
        return 1;
    }

    private static int VerifyReclassification(
        string root,
        string packageRoot,
        JsonElement value,
        string packageIdentity,
        HashSet<string> boundPaths,
        ref int historicalExternalBindings)
    {
        if (!value.TryGetProperty("to_partition", out JsonElement toPartition)
            && !value.TryGetProperty("current_partition", out toPartition))
        {
            return 0;
        }
        string identity = value.TryGetProperty("package_identity", out JsonElement reclassifiedIdentity)
            ? reclassifiedIdentity.GetString()!
            : value.GetProperty("package_id").GetString()!;
        string status = value.TryGetProperty("status", out JsonElement statusValue)
            ? statusValue.GetString()!
            : value.TryGetProperty("current_status", out statusValue)
                ? statusValue.GetString()! : string.Empty;
        Require(identity == packageIdentity && toPartition.GetString() == "development"
            && status.StartsWith("historical-", StringComparison.Ordinal)
            && (!value.TryGetProperty("current_semantic_authority", out JsonElement semantic)
                || !semantic.GetBoolean()),
            $"Historical reclassification is not development-only and non-authorizing: {packageIdentity}");
        int verified = 0;
        if (value.TryGetProperty("precomparison_manifest", out JsonElement precomparison))
        {
            string relative = packageRoot + "/" + precomparison.GetProperty("path").GetString();
            boundPaths.Add(relative);
            byte[] bytes = File.ReadAllBytes(Resolve(root, relative));
            Require(bytes.Length == precomparison.GetProperty("bytes").GetInt32()
                && Sha(bytes) == precomparison.GetProperty("sha256").GetString(),
                $"Historical precomparison manifest drifted: {relative}");
            verified++;
        }
        if (value.TryGetProperty("missing_rule_correction", out JsonElement historicalExternal))
        {
            string externalPath = historicalExternal.GetProperty("path").GetString()!;
            Require(externalPath.Contains('/')
                && historicalExternal.GetProperty("bytes").GetInt32() > 0
                && historicalExternal.GetProperty("sha256").GetString() is string externalSha
                && externalSha.Length == 64 && externalSha.All(Uri.IsHexDigit),
                $"Historical external binding declaration is malformed: {packageIdentity}");
            historicalExternalBindings++;
        }
        return verified;
    }

    private static string PackageIdentity(JsonElement package) => package.GetProperty("package_identity").GetString()!;
    private static string PackagePath(JsonElement package) => package.GetProperty("package_path").GetString()!;
    private static string AuthorityIdentity(JsonElement value) =>
        value.TryGetProperty("package_identity", out JsonElement packageIdentity)
            ? packageIdentity.GetString()!
            : value.TryGetProperty("package_id", out JsonElement packageId)
                ? packageId.GetString()!
                : value.GetProperty("fixture_id").GetString()!;
    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Resolve(string root, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require(path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase), $"Historical package path escapes the repository: {relative}");
        return path;
    }

    private static string ResolveDirectory(string root, string relative)
    {
        string path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require(path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
            $"Historical package directory escapes the repository: {relative}");
        return path;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
