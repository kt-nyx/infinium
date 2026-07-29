using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class SolutionIntegrationTests
{
    [TestMethod]
    [TestCategory("M1Integration")]
    [TestProperty("Category", "M1Integration")]
    public void SolutionContainsEveryDeclaredProject()
    {
        string solution = TestRepository.Read("Infinium.sln");
        string[] projectFiles = TestRepository
            .EnumerateProjectFiles()
            .Select(path => Path.GetRelativePath(TestRepository.Root, path).Replace('/', '\\'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(15, projectFiles);
        foreach (string projectFile in projectFiles)
        {
            StringAssert.Contains(solution, $"\"{projectFile}\"");
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestProperty("Category", "M1Integration")]
    public void EveryProjectHasARestoreLock()
    {
        string[] projectDirectories = TestRepository
            .EnumerateProjectFiles()
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .ToArray();

        foreach (string projectDirectory in projectDirectories)
        {
            Assert.IsTrue(
                File.Exists(Path.Combine(projectDirectory, "packages.lock.json")),
                $"Project '{projectDirectory}' does not have a restore lock.");
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestProperty("Category", "M1Integration")]
    public void AcceptedProjectRunEntryPointsReachExplicitSliceZeroStubs()
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"infinium-slice0-stub-output-{Guid.NewGuid():N}");
        string forbiddenOutputPath = Path.Combine(temporaryRoot, "output");
        (string Project, string Message, string[] Arguments)[] entryPoints =
        [
            (
                "Infinium.Cli",
                "Infinium.Cli is a Slice 0 scaffold; no analysis capability is implemented.",
                ["evaluate", "--manifest", "missing-manifest.json", "--output", forbiddenOutputPath]
            ),
            (
                "Infinium.Coordinator",
                "Infinium.Coordinator is a Slice 0 scaffold; coordinator behavior is not implemented.",
                []
            ),
            (
                "Infinium.Worker",
                "Infinium.Worker is a Slice 0 scaffold; worker behavior is not implemented.",
                []
            ),
            (
                "Infinium.CredentialHelper",
                "Infinium.CredentialHelper is a Slice 0 scaffold; credential behavior is not implemented.",
                []
            ),
        ];

        try
        {
            foreach ((string project, string message, string[] arguments) in entryPoints)
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = "dotnet",
                    WorkingDirectory = TestRepository.Root,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                startInfo.ArgumentList.Add("run");
                startInfo.ArgumentList.Add("--project");
                startInfo.ArgumentList.Add($"src/{project}");
                startInfo.ArgumentList.Add("-c");
                startInfo.ArgumentList.Add("Release");
                startInfo.ArgumentList.Add("--no-build");
                startInfo.ArgumentList.Add("--");
                foreach (string argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                using Process process = Process.Start(startInfo)!;
                bool exited = process.WaitForExit(milliseconds: 15_000);
                if (!exited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }

                Assert.IsTrue(exited, $"{project} did not terminate within 15 seconds.");
                Assert.AreEqual(1, process.ExitCode, project);
                Assert.AreEqual(string.Empty, process.StandardOutput.ReadToEnd(), project);
                Assert.AreEqual($"{message}{Environment.NewLine}", process.StandardError.ReadToEnd(), project);
            }

            Assert.IsFalse(File.Exists(forbiddenOutputPath));
            Assert.IsFalse(Directory.Exists(forbiddenOutputPath));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }
}
