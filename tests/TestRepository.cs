using System.Text.Json;
using System.Xml.Linq;

namespace Infinium.Tests;

internal static class TestRepository
{
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
