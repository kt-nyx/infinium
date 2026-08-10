using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

internal static class TestFileSystem
{
    internal static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            File.Copy(file, target);
        }
    }

    internal static void CreateJunctionOrInconclusive(string link, string target)
    {
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            ArgumentList = { "/d", "/c", "mklink", "/J", link, target },
        }) ?? throw new InvalidOperationException("Could not start the junction helper.");
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            Assert.Inconclusive($"Junction creation is unavailable: {process.StandardError.ReadToEnd()}");
        }
    }

    internal static void DeleteJunction(string path)
    {
        if (Directory.Exists(path)
            && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(path);
        }
    }
}
