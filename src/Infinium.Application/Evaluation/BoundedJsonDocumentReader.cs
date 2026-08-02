using System.Security.Cryptography;
using System.Text.Json;

namespace Infinium.Application.Evaluation;

internal sealed class BoundedJsonDocumentSnapshot : IDisposable
{
    internal BoundedJsonDocumentSnapshot(JsonDocument document, string sha256)
    {
        Document = document;
        Sha256 = sha256;
    }

    internal JsonDocument Document { get; }

    internal string Sha256 { get; }

    public void Dispose() => Document.Dispose();
}

internal static class BoundedJsonDocumentReader
{
    private const int CopyBufferBytes = 64 * 1024;

    internal static BoundedJsonDocumentSnapshot Read(string path, long maximumBytes, int maximumDepth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (maximumBytes < 1 || maximumBytes > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }

        byte[] bytes;
        try
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                CopyBufferBytes,
                FileOptions.SequentialScan);
            if (stream.Length > maximumBytes)
            {
                throw TooLarge(path, maximumBytes);
            }

            using MemoryStream buffer = new((int)stream.Length);
            byte[] chunk = new byte[CopyBufferBytes];
            while (true)
            {
                int read = stream.Read(chunk, 0, chunk.Length);
                if (read == 0)
                {
                    break;
                }

                if (buffer.Length > maximumBytes - read)
                {
                    throw TooLarge(path, maximumBytes);
                }

                buffer.Write(chunk, 0, read);
            }

            bytes = buffer.ToArray();
        }
        catch (IOException exception) when (exception is not FileNotFoundException)
        {
            throw new InvalidDataException($"Unable to read stable JSON snapshot '{path}'.", exception);
        }

        return Parse(bytes, path, maximumDepth);
    }

    internal static BoundedJsonDocumentSnapshot Parse(
        ReadOnlyMemory<byte> bytes,
        string description,
        int maximumDepth)
    {
        try
        {
            JsonDocument document = JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = maximumDepth,
                });
            try
            {
                RejectDuplicateProperties(document.RootElement, "$");
                string sha256 = Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant();
                return new BoundedJsonDocumentSnapshot(document, sha256);
            }
            catch
            {
                document.Dispose();
                throw;
            }
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"'{description}' is not valid strict JSON.", exception);
        }
    }

    internal static void RejectDuplicateProperties(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    HashSet<string> names = new(StringComparer.Ordinal);
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        if (!names.Add(property.Name))
                        {
                            throw new InvalidDataException(
                                $"JSON object at '{path}' contains duplicate property '{property.Name}'.");
                        }

                        RejectDuplicateProperties(property.Value, $"{path}.{property.Name}");
                    }

                    break;
                }
            case JsonValueKind.Array:
                {
                    int index = 0;
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        RejectDuplicateProperties(item, $"{path}[{index}]");
                        index++;
                    }

                    break;
                }
        }
    }

    private static InvalidDataException TooLarge(string path, long maximumBytes)
    {
        return new InvalidDataException($"'{path}' exceeds the {maximumBytes}-byte JSON document limit.");
    }
}
