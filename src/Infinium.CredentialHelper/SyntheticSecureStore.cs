using System.Security.Cryptography;

namespace Infinium.CredentialHelper;

public readonly record struct SyntheticCredentialSlot(string ProfileId, string GenerationId)
{
    public override string ToString() => $"{ProfileId}/{GenerationId}";
}

/// <summary>
/// The WP3 production seam. It intentionally exposes no target string,
/// enumeration, reveal, arbitrary lookup, or native credential operation.
/// </summary>
public interface ISyntheticSecureStore
{
    public void WriteExact(SyntheticCredentialSlot slot, ReadOnlySpan<byte> secret);
    public bool VerifyExact(SyntheticCredentialSlot slot);
    public byte[] ReadExact(SyntheticCredentialSlot slot);
    public bool DeleteExact(SyntheticCredentialSlot slot);
}

public sealed class DeterministicFakeSecureStore : ISyntheticSecureStore, IDisposable
{
    public const int MaximumSecretBytes = 2_560;
    private readonly Dictionary<SyntheticCredentialSlot, byte[]> values = [];
    private bool available = true;

    public static int NativeOperationCount => 0;
    public static int EnumerationCount => 0;
    public bool Available { get => available; set => available = value; }

    public void WriteExact(SyntheticCredentialSlot slot, ReadOnlySpan<byte> secret)
    {
        RequireAvailable();
        if (secret.IsEmpty || secret.Length > MaximumSecretBytes)
        {
            throw new InvalidDataException("The synthetic credential is empty or oversized.");
        }
        byte[] copy = secret.ToArray();
        if (values.Remove(slot, out byte[]? prior))
        {
            CryptographicOperations.ZeroMemory(prior);
        }
        values.Add(slot, copy);
    }

    public bool VerifyExact(SyntheticCredentialSlot slot)
    {
        RequireAvailable();
        return values.ContainsKey(slot);
    }

    public byte[] ReadExact(SyntheticCredentialSlot slot)
    {
        RequireAvailable();
        return values.TryGetValue(slot, out byte[]? value)
            ? value.ToArray()
            : throw new KeyNotFoundException("The exact synthetic credential generation is absent.");
    }

    public bool DeleteExact(SyntheticCredentialSlot slot)
    {
        RequireAvailable();
        if (!values.Remove(slot, out byte[]? value))
        {
            return false;
        }
        CryptographicOperations.ZeroMemory(value);
        return true;
    }

    public void Dispose()
    {
        foreach (byte[] value in values.Values)
        {
            CryptographicOperations.ZeroMemory(value);
        }
        values.Clear();
    }

    private void RequireAvailable()
    {
        if (!available)
        {
            throw new IOException("The synthetic secure store is unavailable.");
        }
    }
}
