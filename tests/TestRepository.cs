using System.Text.Json;
using System.Xml.Linq;

namespace Infinium.Tests;

internal static class TestRepository
{
    private static readonly string[] RootGeneratedDirectoryNames =
    [
        ".git",
        ".packages",
        "artifacts",
        ".vs",
    ];

    private static readonly string[] GeneratedDirectoryNames =
    [
        "bin",
        "obj",
        "TestResults",
    ];

    private static readonly string[] ProjectRootNames = ["src", "tests"];

    private static readonly Lazy<string> LazyRoot = new(FindRoot);

    internal static string Root => LazyRoot.Value;

    internal static string PathFromRoot(params string[] parts)
    {
        return Path.Combine([Root, .. parts]);
    }

    internal static string Read(params string[] parts)
    {
        return File.ReadAllText(PathFromRoot(parts));
    }

    internal static JsonDocument ReadJson(params string[] parts)
    {
        return JsonDocument.Parse(Read(parts));
    }

    internal static XDocument ReadXml(params string[] parts)
    {
        return XDocument.Load(PathFromRoot(parts), LoadOptions.PreserveWhitespace);
    }

    internal static string[] EnumerateProjectFiles()
    {
        return ProjectRootNames
            .Select(segment => PathFromRoot(segment))
            .SelectMany(root => Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
            .Where(path => !IsGeneratedPath(path))
            .ToArray();
    }

    internal static bool IsGeneratedPath(string path)
    {
        string relativePath = Path.GetRelativePath(Root, path);
        string[] segments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return (segments.Length > 0
                && RootGeneratedDirectoryNames.Contains(segments[0], StringComparer.OrdinalIgnoreCase))
            || segments.Any(segment =>
                GeneratedDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Infinium.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate the Infinium repository root.");
    }
}
