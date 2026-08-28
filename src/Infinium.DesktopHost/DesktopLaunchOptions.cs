namespace Infinium.DesktopHost;

public sealed record DesktopLaunchOptions(string ProductRoot, bool IsQualification)
{
    public static DesktopLaunchOptions Parse(IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 2
            && StringComparer.Ordinal.Equals(arguments[0], "--qualification-session")
            && System.Text.RegularExpressions.Regex.IsMatch(arguments[1], "^[a-f0-9]{32}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            string qualificationRoot = Path.Combine(Path.GetTempPath(), $"infinium-desktop-qualification-{arguments[1]}");
            Directory.CreateDirectory(qualificationRoot);
            return new(Path.GetFullPath(qualificationRoot), IsQualification: true);
        }
        if (arguments.Count != 0)
        {
            throw new ArgumentException("Infinium.DesktopHost accepts no filesystem path or native command authority; its optional qualification identity is opaque and bounded.");
        }
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Infinium");
        Directory.CreateDirectory(root);
        return new(Path.GetFullPath(root), IsQualification: false);
    }

    internal static DesktopLaunchOptions ForTest(string productRoot) => new(Path.GetFullPath(productRoot), IsQualification: true);
}
