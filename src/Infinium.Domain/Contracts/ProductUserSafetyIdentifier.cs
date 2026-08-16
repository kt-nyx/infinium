using System.Security.Cryptography;
using System.Text;

namespace Infinium.Domain.Contracts;

public static class ProductUserSafetyIdentifier
{
    public const int SeedBytes = 32;
    public const string Domain = "infinium.openai.safety-identifier/v1";
    private static readonly byte[] DomainPrefix = Encoding.UTF8.GetBytes(Domain + "\0");

    public static byte[] GenerateSeed()
    {
        byte[] seed = new byte[SeedBytes];
        RandomNumberGenerator.Fill(seed);
        return seed;
    }

    public static string Project(ReadOnlySpan<byte> seed)
    {
        if (seed.Length != SeedBytes)
        {
            throw new ArgumentException("The product-user safety identifier seed must be exactly 32 bytes.", nameof(seed));
        }

        byte[] input = new byte[DomainPrefix.Length + SeedBytes];
        DomainPrefix.CopyTo(input, 0);
        seed.CopyTo(input.AsSpan(DomainPrefix.Length));
        string projection = Convert.ToHexStringLower(SHA256.HashData(input));
        CryptographicOperations.ZeroMemory(input.AsSpan(DomainPrefix.Length));
        return projection;
    }

    public static bool IsValidProjection(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
