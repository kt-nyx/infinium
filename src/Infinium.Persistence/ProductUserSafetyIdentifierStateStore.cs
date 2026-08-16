using Infinium.Domain.Contracts;

namespace Infinium.Persistence;

/// <summary>
/// Owns the one local random product-user seed. The raw seed is never returned as a
/// transmitted value; callers receive only the domain-separated SHA-256 projection.
/// </summary>
public sealed class ProductUserSafetyIdentifierStateStore
{
    public const string StateFileName = "product-user-safety-identifier.v1.seed";
    private readonly string statePath;

    public ProductUserSafetyIdentifierStateStore(string productStateRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productStateRoot);
        statePath = Path.Combine(Path.GetFullPath(productStateRoot), StateFileName);
    }

    public string GetOrCreateProjection()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        byte[] seed;
        try
        {
            seed = ProductUserSafetyIdentifier.GenerateSeed();
            using FileStream stream = new(statePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.WriteThrough);
            stream.Write(seed);
            stream.Flush(flushToDisk: true);
        }
        catch (IOException) when (File.Exists(statePath))
        {
            seed = ReadExactSeed();
        }

        try
        {
            return ProductUserSafetyIdentifier.Project(seed);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(seed);
        }
    }

    private byte[] ReadExactSeed()
    {
        using FileStream stream = new(statePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != ProductUserSafetyIdentifier.SeedBytes)
        {
            throw new InvalidDataException("The product-user safety identifier state is malformed.");
        }
        byte[] seed = new byte[ProductUserSafetyIdentifier.SeedBytes];
        stream.ReadExactly(seed);
        return seed;
    }
}
