using Infinium.Domain.Contracts;

namespace Infinium.Persistence;

/// <summary>
/// Owns the one local random product-user seed. The raw seed is never returned as a
/// transmitted value; callers receive only the domain-separated SHA-256 projection.
/// </summary>
public sealed class ProductUserSafetyIdentifierStateStore
{
    public const string StateFileName = "product-user-safety-identifier.v1.seed";
    public const string UseLatchFileName = "product-user-safety-identifier.v1.use";
    public const string UseLatchSchema = "infinium.product-user-safety-identifier-use/v1";
    private readonly string statePath;
    private readonly string useLatchPath;

    public ProductUserSafetyIdentifierStateStore(string productStateRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productStateRoot);
        statePath = Path.Combine(Path.GetFullPath(productStateRoot), StateFileName);
        useLatchPath = Path.Combine(Path.GetFullPath(productStateRoot), UseLatchFileName);
    }

    public string GetOrCreateProjection()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
        if (File.Exists(useLatchPath))
        {
            return GetRequiredProjection(ReadExactLatch());
        }

        byte[] seed = ProductUserSafetyIdentifier.GenerateSeed();
        string temporaryPath = statePath + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(seed);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, statePath, overwrite: false);
        }
        catch (IOException) when (File.Exists(statePath))
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(seed);
            seed = ReadExactSeed();
        }
        finally
        {
            if (File.Exists(temporaryPath)) { File.Delete(temporaryPath); }
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

    /// <summary>
    /// Durably closes seed creation before the first provider call can possibly start.
    /// A crash after this write is conservative: later use must recover the same seed
    /// and projection and may never silently generate a replacement.
    /// </summary>
    public string LatchPossibleStart()
    {
        string projection = GetOrCreateProjection();
        byte[] latch = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new UseLatchRecord(
            UseLatchSchema, projection, "possible-start-latched"));
        string temporaryPath = useLatchPath + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(latch);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, useLatchPath, overwrite: false);
        }
        catch (IOException) when (File.Exists(useLatchPath))
        {
            string retained = ReadExactLatch();
            if (!string.Equals(retained, projection, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The product-user safety identifier use latch changed identity.");
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) { File.Delete(temporaryPath); }
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(latch);
        }
        return projection;
    }

    public string GetRequiredProjection(string expectedProjection)
    {
        if (!ProductUserSafetyIdentifier.IsValidProjection(expectedProjection)
            || !File.Exists(useLatchPath))
        {
            throw new InvalidDataException("The used product-user safety identifier binding is absent.");
        }
        string latched = ReadExactLatch();
        if (!string.Equals(latched, expectedProjection, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The product-user safety identifier use latch is stale.");
        }
        byte[] seed = ReadExactSeed();
        try
        {
            string actual = ProductUserSafetyIdentifier.Project(seed);
            if (!string.Equals(actual, expectedProjection, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The product-user safety identifier seed no longer matches its durable use latch.");
            }
            return actual;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(seed);
        }
    }

    private string ReadExactLatch()
    {
        byte[] bytes = File.ReadAllBytes(useLatchPath);
        try
        {
            UseLatchRecord? record;
            try
            {
                record = System.Text.Json.JsonSerializer.Deserialize<UseLatchRecord>(bytes);
            }
            catch (System.Text.Json.JsonException exception)
            {
                throw new InvalidDataException("The product-user safety identifier use latch is malformed.", exception);
            }
            byte[] canonical = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(record);
            if (record is null || !bytes.AsSpan().SequenceEqual(canonical)
                || record.Schema != UseLatchSchema || record.State != "possible-start-latched"
                || !ProductUserSafetyIdentifier.IsValidProjection(record.Projection))
            {
                throw new InvalidDataException("The product-user safety identifier use latch is malformed.");
            }
            return record.Projection;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private byte[] ReadExactSeed()
    {
        if (!File.Exists(statePath))
        {
            throw new InvalidDataException("The product-user safety identifier state is absent.");
        }
        using FileStream stream = new(statePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != ProductUserSafetyIdentifier.SeedBytes)
        {
            throw new InvalidDataException("The product-user safety identifier state is malformed.");
        }
        byte[] seed = new byte[ProductUserSafetyIdentifier.SeedBytes];
        stream.ReadExactly(seed);
        return seed;
    }

    private sealed record UseLatchRecord(string Schema, string Projection, string State);
}
