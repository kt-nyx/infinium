#pragma warning disable IDE0008 // Path normalization locals are self-describing.

namespace Infinium.Persistence;

public sealed class StoragePaths
{
    private static readonly string[] ProtectedLeafNames =
    [
        "Skyrim Special Edition",
        "ModOrganizer",
        "Mod Organizer 2",
    ];

    public StoragePaths(string productRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productRoot);
        if (!Path.IsPathFullyQualified(productRoot))
        {
            throw new ArgumentException("The product root must be an absolute path.", nameof(productRoot));
        }

        ProductRoot = Path.GetFullPath(productRoot);
        RejectProtectedRoot(ProductRoot);
        Data = Path.Combine(ProductRoot, "data");
        Payloads = Path.Combine(ProductRoot, "payloads");
        Staging = Path.Combine(ProductRoot, "staging");
        Backups = Path.Combine(ProductRoot, "backups");
        Runtime = Path.Combine(ProductRoot, "runtime");
        RunOutput = Path.Combine(ProductRoot, "run-output");
        Database = Path.Combine(Data, "infinium.sqlite3");
    }

    public string ProductRoot { get; }
    public string Data { get; }
    public string Payloads { get; }
    public string Staging { get; }
    public string Backups { get; }
    public string Runtime { get; }
    public string RunOutput { get; }
    public string Database { get; }

    public void Create()
    {
        foreach (var path in new[] { ProductRoot, Data, Payloads, Staging, Backups, Runtime, RunOutput })
        {
            Directory.CreateDirectory(path);
        }
    }

    public string ResolveProductRelative(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathFullyQualified(relativePath)
            || relativePath.Contains(':', StringComparison.Ordinal)
            || relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException("Only normalized product-relative paths are authorized.");
        }

        var result = Path.GetFullPath(Path.Combine(ProductRoot, relativePath));
        var prefix = ProductRoot.EndsWith(Path.DirectorySeparatorChar)
            ? ProductRoot
            : ProductRoot + Path.DirectorySeparatorChar;
        if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The resolved path escapes the product root.");
        }

        RejectReparseAncestors(result);
        return result;
    }

    private static void RejectProtectedRoot(string path)
    {
        var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (ProtectedLeafNames.Any(name =>
            normalized.EndsWith(Path.DirectorySeparatorChar + name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("A protected game or mod-manager root cannot be a product root.");
        }
    }

    private void RejectReparseAncestors(string path)
    {
        string relative = Path.GetRelativePath(ProductRoot, path);
        string current = ProductRoot;
        if (Directory.Exists(current)
            && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("Reparse-point product roots are not authorized.");
        }

        foreach (string segment in relative.Split(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((Directory.Exists(current) || File.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException("Reparse-point write paths are not authorized.");
            }
        }
    }
}

#pragma warning restore IDE0008
