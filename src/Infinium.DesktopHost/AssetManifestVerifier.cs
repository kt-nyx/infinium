using System.Security.Cryptography;
using System.Text.Json;

namespace Infinium.DesktopHost;

internal static class AssetManifestVerifier
{
    private static readonly string[] ExpectedAssets =
    [
        "/index.html", "/app.css", "/desktop-renderer.js", "/bounded-result-pager.js", "/client.js", "/decoders.js", "/schema-validator.js",
        "/generated/renderer-contract.generated.js", "/vendor/react.production.min.js", "/vendor/react-dom.production.min.js",
    ];

    public static IReadOnlySet<string> Verify(string assetRoot)
    {
        string manifestPath = Path.Combine(assetRoot, "asset-manifest.json");
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        string manifestSha256 = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        if (!StringComparer.Ordinal.Equals(manifestSha256, DesktopAssetCatalog.ManifestSha256))
        {
            throw new InvalidDataException("The packaged asset manifest does not match the compiled provenance anchor.");
        }
        using JsonDocument document = JsonDocument.Parse(manifestBytes);
        if (!StringComparer.Ordinal.Equals(
            document.RootElement.GetProperty("renderer_registry_version").GetString(),
            DesktopAssetCatalog.RendererRegistryVersion))
        {
            throw new InvalidDataException("The packaged assets do not bind the compiled renderer registry version.");
        }
        JsonElement entries = document.RootElement.GetProperty("assets");
        HashSet<string> paths = new(StringComparer.Ordinal);
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            string path = entry.GetProperty("path").GetString()!;
            if (Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal) || !paths.Add('/' + path.Replace('\\', '/')))
            {
                throw new InvalidDataException("The packaged asset manifest contains an invalid or duplicate path.");
            }

            string fullPath = Path.GetFullPath(Path.Combine(assetRoot, path));
            if (!fullPath.StartsWith(Path.GetFullPath(assetRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A packaged asset escapes the controlled asset root.");
            }

            byte[] bytes = File.ReadAllBytes(fullPath);
            string actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (bytes.Length != entry.GetProperty("bytes").GetInt64()
                || !StringComparer.Ordinal.Equals(actual, entry.GetProperty("sha256").GetString()))
            {
                throw new InvalidDataException("A packaged renderer asset failed its length or SHA-256 check.");
            }
        }
        if (!paths.SetEquals(ExpectedAssets))
        {
            throw new InvalidDataException("The packaged asset manifest is not the exact closed renderer asset set.");
        }

        return paths;
    }
}
