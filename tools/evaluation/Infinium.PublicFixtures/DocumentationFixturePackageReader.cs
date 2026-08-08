using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;

namespace Infinium.PublicFixtures;

public sealed record DocumentationFixturePackage(
    OpaqueId FixtureId,
    ContractVersion FixtureVersion,
    FixturePartition Partition,
    DocumentationClaimImportManifestContract ClaimImport,
    ReadOnlyMemory<byte> SourceBytes,
    JsonElement CaseMatrix,
    JsonElement Oracle,
    JsonElement PublicManifest,
    JsonElement Provenance,
    JsonElement ReplayDependencies);

public static class DocumentationFixturePackageReader
{
    private const long MaximumJsonBytes = 4 * 1024 * 1024;
    private const int MaximumDepth = 64;
    private static readonly HashSet<string> ExpectedRootFiles = new(StringComparer.Ordinal)
    {
        "expected-oracle.json",
        "partition-history.json",
        "provenance.json",
        "public-manifest.json",
        "redistribution.json",
        "replay-dependencies.json",
    };
    private static readonly HashSet<string> ExpectedInputFiles = new(StringComparer.Ordinal)
    {
        "case-matrix.json",
        "claim-import.json",
        "source.txt",
    };

    public static DocumentationFixturePackage Read(string fixtureDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fixtureDirectory);
        string root = Path.GetFullPath(fixtureDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Documentation fixture directory '{root}' does not exist.");
        }
        EnsureNoReparsePoints(root);
        EnsureExactFiles(root, ExpectedRootFiles);
        EnsureExactDirectories(root, ["inputs", "oracle"]);
        string inputs = RequiredDirectory(root, "inputs");
        string oracleDirectory = RequiredDirectory(root, "oracle");
        EnsureExactFiles(inputs, ExpectedInputFiles);
        EnsureExactDirectories(inputs, []);
        EnsureExactFiles(oracleDirectory, ["independent-derivation.md"]);
        EnsureExactDirectories(oracleDirectory, []);

        using BoundedJsonDocumentSnapshot publicSnapshot = ReadJson(root, "public-manifest.json");
        using BoundedJsonDocumentSnapshot claimSnapshot = ReadJson(inputs, "claim-import.json");
        using BoundedJsonDocumentSnapshot caseSnapshot = ReadJson(inputs, "case-matrix.json");
        using BoundedJsonDocumentSnapshot oracleSnapshot = ReadJson(root, "expected-oracle.json");
        using BoundedJsonDocumentSnapshot provenanceSnapshot = ReadJson(root, "provenance.json");
        using BoundedJsonDocumentSnapshot replaySnapshot = ReadJson(root, "replay-dependencies.json");
        using BoundedJsonDocumentSnapshot redistributionSnapshot = ReadJson(root, "redistribution.json");
        using BoundedJsonDocumentSnapshot partitionSnapshot = ReadJson(root, "partition-history.json");

        JsonElement publicManifest = RequireObject(publicSnapshot.Document.RootElement, "public-manifest.json");
        ActiveJsonSchemaValidator.Validate(publicManifest, "fixture-public-manifest.v1.schema.json");
        OpaqueId fixtureId = new(RequireString(publicManifest, "fixture_id"));
        ContractVersion fixtureVersion = ContractVersion.Parse(RequireString(publicManifest, "fixture_version"));
        if (!StringComparer.Ordinal.Equals(RequireString(publicManifest, "review_state"), "accepted"))
        {
            throw new InvalidDataException("Current documentation fixtures must have independent accepted review state.");
        }

        DocumentationClaimImportManifestContract claimImport =
            DocumentationClaimImportJsonCodec.Deserialize(claimSnapshot.Document.RootElement.GetRawTextBytes());
        byte[] sourceBytes = ReadBoundedSource(Path.Combine(inputs, "source.txt"));
        string sourceSha = Convert.ToHexStringLower(SHA256.HashData(sourceBytes));
        if (sourceBytes.LongLength != claimImport.ByteLength
            || !StringComparer.Ordinal.Equals(sourceSha, claimImport.ByteFingerprint.Value))
        {
            throw new InvalidDataException("Documentation fixture source bytes do not match the claim-import identity.");
        }

        ValidateIdentity(caseSnapshot.Document.RootElement, fixtureId, fixtureVersion, "case-matrix.json");
        ValidateIdentity(oracleSnapshot.Document.RootElement, fixtureId, fixtureVersion, "expected-oracle.json");
        ValidateIdentity(provenanceSnapshot.Document.RootElement, fixtureId, fixtureVersion, "provenance.json");
        ValidateIdentity(replaySnapshot.Document.RootElement, fixtureId, fixtureVersion, "replay-dependencies.json");
        ValidateIdentity(redistributionSnapshot.Document.RootElement, fixtureId, fixtureVersion, "redistribution.json");
        ValidateIdentity(partitionSnapshot.Document.RootElement, fixtureId, fixtureVersion, "partition-history.json");
        ValidateFingerprint(publicManifest, "oracle_fingerprint", oracleSnapshot.Sha256);
        ValidateFingerprint(publicManifest, "provenance_fingerprint", provenanceSnapshot.Sha256);
        ValidateFingerprint(publicManifest, "replay_dependency_fingerprint", replaySnapshot.Sha256);
        ValidateRedistribution(publicManifest, redistributionSnapshot.Document.RootElement);
        string graphFingerprint = ValidateReplayClosure(root, replaySnapshot.Document.RootElement);
        ValidateFingerprint(publicManifest, "input_package_fingerprint", graphFingerprint);

        FixturePartition partition = RequireString(publicManifest, "partition") switch
        {
            "development" => FixturePartition.Development,
            "validation" => FixturePartition.Validation,
            _ => throw new InvalidDataException("WP2 documentation fixture partition must be development or validation."),
        };
        return new DocumentationFixturePackage(
            fixtureId,
            fixtureVersion,
            partition,
            claimImport,
            sourceBytes,
            caseSnapshot.Document.RootElement.Clone(),
            oracleSnapshot.Document.RootElement.Clone(),
            publicManifest.Clone(),
            provenanceSnapshot.Document.RootElement.Clone(),
            replaySnapshot.Document.RootElement.Clone());
    }

    private static string ValidateReplayClosure(string root, JsonElement replay)
    {
        JsonElement dependencies = RequireObject(replay, "replay-dependencies.json")
            .GetProperty("dependencies");
        List<string> entries = [];
        foreach (JsonElement dependency in dependencies.EnumerateArray())
        {
            string relative = RequireString(dependency, "path");
            if (!relative.StartsWith("inputs/", StringComparison.Ordinal)
                || Path.IsPathRooted(relative)
                || relative.Split('/').Any(part => part is "" or "." or ".."))
            {
                throw new InvalidDataException("Documentation fixture replay dependencies must remain inside inputs/.");
            }
            string fullPath = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            string expectedPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)
                || !File.Exists(fullPath))
            {
                throw new InvalidDataException("Documentation fixture replay dependency is missing or escapes its package.");
            }
            byte[] bytes = File.ReadAllBytes(fullPath);
            string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            long length = bytes.LongLength;
            if (!StringComparer.Ordinal.Equals(sha, RequireString(dependency, "sha256"))
                || length != dependency.GetProperty("byte_length").GetInt64())
            {
                throw new InvalidDataException($"Documentation fixture replay dependency drifted: {relative}");
            }
            entries.Add(FormattableString.Invariant($"{relative}\0{sha}\0{length}\n"));
        }
        string graph = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Concat(entries.Order(StringComparer.Ordinal)))));
        ValidateFingerprint(replay, "dependency_graph_fingerprint", graph);
        return graph;
    }

    private static void ValidateRedistribution(JsonElement manifest, JsonElement redistribution)
    {
        if (!StringComparer.Ordinal.Equals(
                RequireString(manifest, "redistribution_class"),
                RequireString(redistribution, "redistribution_class")))
        {
            throw new InvalidDataException("Documentation fixture redistribution identity drifted.");
        }
    }

    private static void ValidateIdentity(
        JsonElement document,
        OpaqueId fixtureId,
        ContractVersion fixtureVersion,
        string description)
    {
        JsonElement root = RequireObject(document, description);
        if (!StringComparer.Ordinal.Equals(RequireString(root, "fixture_id"), fixtureId.Value)
            || !StringComparer.Ordinal.Equals(RequireString(root, "fixture_version"), fixtureVersion.ToString()))
        {
            throw new InvalidDataException($"Documentation fixture identity drifted in {description}.");
        }
    }

    private static void ValidateFingerprint(JsonElement document, string property, string actual)
    {
        if (!StringComparer.Ordinal.Equals(RequireString(document, property), actual))
        {
            throw new InvalidDataException($"Documentation fixture fingerprint mismatch at {property}.");
        }
    }

    private static BoundedJsonDocumentSnapshot ReadJson(string directory, string fileName) =>
        BoundedJsonDocumentReader.Read(Path.Combine(directory, fileName), MaximumJsonBytes, MaximumDepth);

    private static byte[] ReadBoundedSource(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 8 * 1024 * 1024)
        {
            throw new InvalidDataException("Documentation fixture source exceeds 8 MiB.");
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static void EnsureExactFiles(string directory, IEnumerable<string> expectedNames)
    {
        HashSet<string> expected = expectedNames.ToHashSet(StringComparer.Ordinal);
        HashSet<string> actual = Directory.EnumerateFiles(directory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
        {
            throw new InvalidDataException($"Documentation fixture file closure drifted under '{directory}'.");
        }
    }

    private static void EnsureExactDirectories(string directory, IEnumerable<string> expectedNames)
    {
        HashSet<string> expected = expectedNames.ToHashSet(StringComparer.Ordinal);
        HashSet<string> actual = Directory.EnumerateDirectories(directory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
        {
            throw new InvalidDataException($"Documentation fixture directory closure drifted under '{directory}'.");
        }
    }

    private static void EnsureNoReparsePoints(string root)
    {
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count != 0)
        {
            string directory = pending.Pop();
            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Documentation fixture packages must not contain reparse points.");
            }
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                FileAttributes attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException("Documentation fixture packages must not contain reparse points.");
                }
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    pending.Push(entry);
                }
            }
        }
    }

    private static string RequiredDirectory(string root, string name)
    {
        string path = Path.Combine(root, name);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Required documentation fixture directory is missing: {name}");
        }
        return path;
    }

    private static JsonElement RequireObject(JsonElement value, string description)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{description} must contain a JSON object.");
        }
        return value;
    }

    private static string RequireString(JsonElement value, string property)
    {
        if (!value.TryGetProperty(property, out JsonElement item)
            || item.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(item.GetString()))
        {
            throw new InvalidDataException($"Documentation fixture property '{property}' must be a non-empty string.");
        }
        return item.GetString()!;
    }

    private static ReadOnlySpan<byte> GetRawTextBytes(this JsonElement value) =>
        Encoding.UTF8.GetBytes(value.GetRawText());
}
