using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Runtime;

namespace Infinium.CredentialHelper;

public readonly record struct SyntheticCredentialSlot(string ProfileId, string GenerationId)
{
    public override string ToString() => $"{ProfileId}/{GenerationId}";
}

/// <summary>
/// The synthetic secure-store seam. It intentionally exposes no target string,
/// enumeration, reveal, arbitrary lookup, or native credential operation.
/// </summary>
public interface ISyntheticSecureStore
{
    public void WriteExact(SyntheticCredentialSlot slot, ReadOnlySpan<byte> secret);
    public bool VerifyExact(SyntheticCredentialSlot slot);
    public byte[] ReadExact(SyntheticCredentialSlot slot);
    public bool DeleteExact(SyntheticCredentialSlot slot);
    public bool ConsumeOneUseNonce(ReadOnlySpan<byte> nonceFingerprint);
}

public sealed class DeterministicFakeSecureStore : ISyntheticSecureStore, IDisposable
{
    public const int MaximumSecretBytes = 2_560;
    private readonly Dictionary<SyntheticCredentialSlot, byte[]> values = [];
    private readonly HashSet<string> consumedNonces = new(StringComparer.Ordinal);
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

    public bool ConsumeOneUseNonce(ReadOnlySpan<byte> nonceFingerprint) =>
        consumedNonces.Add(Convert.ToHexString(nonceFingerprint));

    public void Dispose()
    {
        foreach (byte[] value in values.Values)
        {
            CryptographicOperations.ZeroMemory(value);
        }
        values.Clear();
        consumedNonces.Clear();
    }

    private void RequireAvailable()
    {
        if (!available)
        {
            throw new IOException("The synthetic secure store is unavailable.");
        }
    }
}


/// <summary>
/// Persistent capability-bound fake-store implementation rooted only by an inherited
/// directory capability. The helper can access one fixed file and exact
/// profile/generation slots; it receives neither a path nor an enumeration API.
/// </summary>
public sealed class CapabilityBoundFakeSecureStore : ISyntheticSecureStore, IDisposable
{
    private const string StoreLeaf = "synthetic-secure-store.v1.json";
    internal const string TargetCanaryPrefix = "CAPABILITY-BOUND-STORE-TARGET-CANARY";
    private readonly nint directoryHandle;

    public CapabilityBoundFakeSecureStore(nint directoryHandle) =>
        this.directoryHandle = directoryHandle is 0 or -1
            ? throw new ArgumentOutOfRangeException(nameof(directoryHandle))
            : directoryHandle;

    public static int NativeCredentialOperationCount => 0;
    public static int EnumerationCount => 0;

    public void WriteExact(SyntheticCredentialSlot slot, ReadOnlySpan<byte> secret)
    {
        Validate(slot);
        if (secret.IsEmpty || secret.Length > DeterministicFakeSecureStore.MaximumSecretBytes)
        {
            throw new InvalidDataException("The synthetic credential is empty or oversized.");
        }
        string encoded = Convert.ToBase64String(secret);
        Mutate(state => state.Values[Key(slot)] = encoded);
    }

    public bool VerifyExact(SyntheticCredentialSlot slot)
    {
        Validate(slot);
        return Read().Values.ContainsKey(Key(slot));
    }

    public byte[] ReadExact(SyntheticCredentialSlot slot)
    {
        Validate(slot);
        return Read().Values.TryGetValue(Key(slot), out string? value)
            ? Convert.FromBase64String(value)
            : throw new KeyNotFoundException("The exact synthetic credential generation is absent.");
    }

    public bool DeleteExact(SyntheticCredentialSlot slot)
    {
        Validate(slot);
        bool removed = false;
        bool fail = false;
        Mutate(state =>
        {
            string key = Key(slot);
            fail = state.DeleteFailures.Remove(key);
            if (!fail)
            {
                removed = state.Values.Remove(key);
            }
        });
        if (fail)
        {
            throw new IOException("Injected exact predecessor deletion failure.");
        }
        return removed;
    }

    public bool ConsumeOneUseNonce(ReadOnlySpan<byte> nonceFingerprint)
    {
        if (nonceFingerprint.Length != 32)
        {
            throw new InvalidDataException("The one-use nonce fingerprint is not SHA-256 sized.");
        }
        string value = Convert.ToHexString(nonceFingerprint).ToLowerInvariant();
        bool added = false;
        Mutate(state => added = state.ConsumedNonces.Add(value));
        return added;
    }

    public void Dispose() { }

    private StoreState Read()
    {
        using FileStream stream = WindowsHandleRelativeFile.OpenOrCreateReadWrite(directoryHandle, StoreLeaf);
        return ReadState(stream);
    }

    private void Mutate(Action<StoreState> mutation)
    {
        using FileStream stream = WindowsHandleRelativeFile.OpenOrCreateReadWrite(directoryHandle, StoreLeaf);
        StoreState state = ReadState(stream);
        mutation(state);
        stream.Position = 0;
        stream.SetLength(0);
        JsonSerializer.Serialize(stream, state);
        stream.Flush(flushToDisk: true);
    }

    private static StoreState ReadState(FileStream stream)
    {
        if (stream.Length == 0)
        {
            return new();
        }
        stream.Position = 0;
        return JsonSerializer.Deserialize<StoreState>(stream)
            ?? throw new InvalidDataException("The capability-bound fake secure store is malformed.");
    }

    private static string Key(SyntheticCredentialSlot slot) =>
        $"{TargetCanaryPrefix}/{slot.ProfileId}/{slot.GenerationId}";

    private static void Validate(SyntheticCredentialSlot slot)
    {
        static bool Valid(string value) => value.Length is > 0 and <= 120
            && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');
        if (!Valid(slot.ProfileId) || !Valid(slot.GenerationId))
        {
            throw new InvalidDataException("The exact synthetic credential slot is invalid.");
        }
    }

    private sealed class StoreState
    {
        public SortedDictionary<string, string> Values { get; init; } = new(StringComparer.Ordinal);
        public SortedSet<string> ConsumedNonces { get; init; } = new(StringComparer.Ordinal);
        public SortedSet<string> DeleteFailures { get; init; } = new(StringComparer.Ordinal);
    }
}
