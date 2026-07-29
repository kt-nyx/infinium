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
        string[] projectFiles = Directory
            .GetFiles(TestRepository.Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.packages{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
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
        string[] projectDirectories = Directory
            .GetFiles(TestRepository.Root, "*.csproj", SearchOption.AllDirectories)
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
}
