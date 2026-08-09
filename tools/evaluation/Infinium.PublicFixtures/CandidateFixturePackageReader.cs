using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;

namespace Infinium.PublicFixtures;

public sealed record CandidatePublicFixturePackage(
    PublicFixturePackage Package,
    CandidateDeliveredInputContract? DeliveredInput,
    CandidateDeliveredExpansionContract? DeliveredExpansion);

public static class CandidateFixturePackageReader
{
    public const string DeliveredInputArtifactId = "inputs/candidate-delivered-input.json";
    public const string DeliveredExpansionArtifactId = "inputs/candidate-delivered-expansion.json";
    private const int MaximumProductInputBytes = 16 * 1024 * 1024;

    public static CandidatePublicFixturePackage Read(string fixtureDirectory)
    {
        PublicFixturePackage package = PublicFixturePackageReader.Read(fixtureDirectory);
        JsonElement[] references = package.ExecutionInput.GetProperty("input_payload_refs")
            .EnumerateArray()
            .Where(item => item.GetProperty("artifact_id").GetString() is DeliveredInputArtifactId or DeliveredExpansionArtifactId)
            .ToArray();
        if (references.Length != 1)
        {
            throw new InvalidDataException("A candidate fixture must retain exactly one delivered input or expansion artifact.");
        }
        JsonElement reference = references[0];
        string artifactId = reference.GetProperty("artifact_id").GetString()!;
        if (!StringComparer.Ordinal.Equals(reference.GetProperty("artifact_version").GetString(), "1.0.0")
            || !StringComparer.Ordinal.Equals(reference.GetProperty("availability").GetString(), "retained")
            || !reference.TryGetProperty("byte_length", out JsonElement byteLength))
        {
            throw new InvalidDataException("The candidate product artifact requires retained version 1.0.0 bytes and exact length.");
        }
        string root = Path.GetFullPath(fixtureDirectory);
        string path = Path.GetFullPath(Path.Combine(root, artifactId.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Candidate fixture artifact path escapes its package root.");
        }
        FileInfo info = new(path);
        if (!info.Exists || info.Length is < 1 or > MaximumProductInputBytes || info.Length != byteLength.GetInt64())
        {
            throw new InvalidDataException("Candidate fixture product artifact is missing or violates its byte bound.");
        }
        byte[] bytes = File.ReadAllBytes(path);
        string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!StringComparer.Ordinal.Equals(sha, reference.GetProperty("fingerprint").GetString()))
        {
            throw new InvalidDataException("Candidate fixture product artifact fingerprint differs from its retained bytes.");
        }
        return artifactId switch
        {
            DeliveredInputArtifactId => new(package, CandidateDeliveredInputJsonCodec.Deserialize(bytes), null),
            DeliveredExpansionArtifactId => new(package, null, CandidateDeliveredExpansionJsonCodec.Deserialize(bytes)),
            _ => throw new InvalidDataException("Candidate fixture product artifact kind is not closed."),
        };
    }
}
