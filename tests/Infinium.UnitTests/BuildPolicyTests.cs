using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class BuildPolicyTests
{
    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void GlobalJsonPinsTheExactSupportedSdk()
    {
        using JsonDocument document = TestRepository.ReadJson("global.json");
        JsonElement sdk = document.RootElement.GetProperty("sdk");

        Assert.AreEqual("10.0.303", sdk.GetProperty("version").GetString());
        Assert.AreEqual("disable", sdk.GetProperty("rollForward").GetString());
        Assert.IsFalse(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void BuildPolicyPinsTheDeterministicSupportedTarget()
    {
        XDocument buildProperties = TestRepository.ReadXml("Directory.Build.props");
        Dictionary<string, string> properties = buildProperties
            .Descendants()
            .Where(element => !element.HasElements)
            .ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.Ordinal);

        Assert.AreEqual("net10.0", properties["TargetFramework"]);
        Assert.AreEqual("x64", properties["PlatformTarget"]);
        Assert.AreEqual("false", properties["AppendPlatformToOutputPath"]);
        Assert.AreEqual("14.0", properties["LangVersion"]);
        Assert.AreEqual("true", properties["Deterministic"]);
        Assert.AreEqual("true", properties["ContinuousIntegrationBuild"]);
        Assert.AreEqual("true", properties["EnableNETAnalyzers"]);
        Assert.AreEqual("latest-recommended", properties["AnalysisLevel"]);
        Assert.AreEqual("true", properties["EnforceCodeStyleInBuild"]);
        Assert.AreEqual("true", properties["TreatWarningsAsErrors"]);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "Unit")]
    public void CentralPackagePolicyPinsOnlyAcceptedPackages()
    {
        Dictionary<string, string> expectedPackages = new(StringComparer.Ordinal)
        {
            ["Google.Protobuf"] = "3.31.1",
            ["Grpc.AspNetCore"] = "2.80.0",
            ["Grpc.Core.Api"] = "2.80.0",
            ["Grpc.Net.Client"] = "2.80.0",
            ["Grpc.Tools"] = "2.80.0",
            ["Microsoft.Bcl.Memory"] = "9.0.14",
            ["Microsoft.Data.Sqlite.Core"] = "10.0.10",
            ["Microsoft.ML.Tokenizers"] = "2.0.0",
            ["Microsoft.ML.Tokenizers.Data.O200kBase"] = "2.0.0",
            ["Microsoft.NET.Test.Sdk"] = "18.8.1",
            ["Microsoft.TypeScript.MSBuild"] = "5.9.3",
            ["Microsoft.Web.WebView2"] = "1.0.4129.50",
            ["MSTest.TestAdapter"] = "4.3.2",
            ["MSTest.TestFramework"] = "4.3.2",
            ["Mutagen.Bethesda.Skyrim"] = "0.54.2",
            ["Node.js.redist.win"] = "24.14.1",
            ["SQLitePCLRaw.bundle_e_sqlite3"] = "3.0.5",
        };
        Dictionary<string, string> actualPackages = TestRepository.ReadXml("Directory.Packages.props")
            .Descendants("PackageVersion")
            .ToDictionary(
                element => element.Attribute("Include")!.Value,
                element => element.Attribute("Version")!.Value,
                StringComparer.Ordinal);

        CollectionAssert.AreEquivalent(expectedPackages.Keys.ToArray(), actualPackages.Keys.ToArray());
        foreach ((string packageId, string version) in expectedPackages)
        {
            Assert.AreEqual(version, actualPackages[packageId], packageId);
        }
    }

    [TestMethod]
    [TestCategory("Security")]
    [TestProperty("Category", "Security")]
    public void ProductionSourcesDoNotReferenceExcludedArchaeology()
    {
        string sourceRoot = TestRepository.PathFromRoot("src");
        string[] excludedTokens =
        [
            "infinium" + "-legacy-archive",
            "7dd3" + "da6",
            ".." + "\\legacy",
            ".." + "/legacy",
        ];

        string[] sourceFiles = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => !TestRepository.IsGeneratedPath(path))
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".props" or ".targets")
            .ToArray();

        foreach (string sourceFile in sourceFiles)
        {
            string content = File.ReadAllText(sourceFile);
            foreach (string excludedToken in excludedTokens)
            {
                Assert.IsFalse(
                    content.Contains(excludedToken, StringComparison.OrdinalIgnoreCase),
                    $"Production file '{sourceFile}' references excluded archaeology.");
            }
        }
    }

    [TestMethod]
    [TestCategory("Security")]
    [TestProperty("Category", "Security")]
    public void ProductWritersDoNotRegressToPathnameMutation()
    {
        Dictionary<string, string[]> forbiddenBySource =
            new(StringComparer.Ordinal)
            {
                ["src/Infinium.Application/Runtime/RuntimeDescriptor.cs"] =
                [
                    "FileStream(",
                    "File.Move(",
                    "File.Delete(",
                    "File.WriteAll",
                ],
                ["src/Infinium.Coordinator/ManagedRunExecutor.cs"] =
                [
                    "Directory.CreateDirectory(",
                    "File.Create(",
                    "File.Move(",
                ],
                ["src/Infinium.Coordinator/WindowsContainedWorkerProcess.cs"] =
                [
                    "CreateFileW(",
                ],
            };

        string[] persistenceWriterForbiddenTokens =
        [
            "Directory.CreateDirectory(",
            "Directory.Move(",
            "Directory.Delete(",
            "File.Copy(",
            "File.Move(",
            "File.Delete(",
        ];
        foreach (string sourcePath in Directory.EnumerateFiles(
                     TestRepository.PathFromRoot("src", "Infinium.Persistence"),
                     "AuthoritativeStore*.cs",
                     SearchOption.TopDirectoryOnly))
        {
            string content = File.ReadAllText(sourcePath);
            foreach (string forbiddenToken in persistenceWriterForbiddenTokens)
            {
                Assert.IsFalse(
                    content.Contains(forbiddenToken, StringComparison.Ordinal),
                    $"{Path.GetRelativePath(TestRepository.Root, sourcePath)} regressed to pathname mutation through '{forbiddenToken}'.");
            }
        }

        foreach ((string relativePath, string[] forbiddenTokens) in forbiddenBySource)
        {
            string content = TestRepository.Read(
                relativePath.Split('/'));
            foreach (string forbiddenToken in forbiddenTokens)
            {
                Assert.IsFalse(
                    content.Contains(forbiddenToken, StringComparison.Ordinal),
                    $"{relativePath} regressed to pathname mutation through '{forbiddenToken}'.");
            }
        }
    }

    [TestMethod]
    [TestCategory("Security")]
    [TestProperty("Category", "Security")]
    public void IgnorePolicyProtectsSecretsWithoutHidingNestedEvidence()
    {
        string[] ignoreRules = TestRepository
            .Read(".gitignore")
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(rule => !rule.StartsWith('#'))
            .ToArray();

        CollectionAssert.Contains(ignoreRules, ".env");
        CollectionAssert.Contains(ignoreRules, ".env.*");
        CollectionAssert.Contains(ignoreRules, "!.env.example");
        CollectionAssert.Contains(ignoreRules, "/artifacts/");
        CollectionAssert.Contains(ignoreRules, "/.vs/");
        CollectionAssert.Contains(ignoreRules, "/.packages/");
        CollectionAssert.DoesNotContain(ignoreRules, "artifacts/");
        CollectionAssert.DoesNotContain(ignoreRules, ".packages/");
        Assert.IsTrue(TestRepository.IsGeneratedPath(TestRepository.PathFromRoot("artifacts", "build.log")));
        Assert.IsFalse(TestRepository.IsGeneratedPath(
            TestRepository.PathFromRoot("docs", "research", "investigations", "artifacts", "evidence.txt")));
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestProperty("Category", "Fault")]
    public void RestorePolicyRequiresLockFiles()
    {
        XDocument buildProperties = TestRepository.ReadXml("Directory.Build.props");
        XElement lockFileProperty = buildProperties.Descendants("RestorePackagesWithLockFile").Single();
        XElement lockedModeProperty = buildProperties.Descendants("RestoreLockedMode").Single();

        Assert.AreEqual("true", lockFileProperty.Value);
        Assert.AreEqual("true", lockedModeProperty.Value);
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestProperty("Category", "Fault")]
    public void LockedRestoreRejectsDependencyDrift()
    {
        string temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            $"infinium-package-lock-drift-{Guid.NewGuid():N}");
        string projectDirectory = Path.Combine(temporaryRoot, "src", "Infinium.Bethesda");
        Directory.CreateDirectory(projectDirectory);

        try
        {
            foreach (string rootFile in new[]
            {
                "Directory.Build.props",
                "Directory.Packages.props",
                "NuGet.Config",
                "global.json",
            })
            {
                File.Copy(TestRepository.PathFromRoot(rootFile), Path.Combine(temporaryRoot, rootFile));
            }

            File.Copy(
                TestRepository.PathFromRoot("src", "Infinium.Bethesda", "Infinium.Bethesda.csproj"),
                Path.Combine(projectDirectory, "Infinium.Bethesda.csproj"));
            File.Copy(
                TestRepository.PathFromRoot("src", "Infinium.Bethesda", "packages.lock.json"),
                Path.Combine(projectDirectory, "packages.lock.json"));

            string centralPackagesPath = Path.Combine(temporaryRoot, "Directory.Packages.props");
            string centralPackages = File.ReadAllText(centralPackagesPath);
            string driftedPackages = centralPackages.Replace(
                "Mutagen.Bethesda.Skyrim\" Version=\"0.54.2",
                "Mutagen.Bethesda.Skyrim\" Version=\"0.54.1",
                StringComparison.Ordinal);
            Assert.AreNotEqual(centralPackages, driftedPackages);
            File.WriteAllText(centralPackagesPath, driftedPackages);

            ProcessStartInfo startInfo = new()
            {
                FileName = "dotnet",
                WorkingDirectory = temporaryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("restore");
            startInfo.ArgumentList.Add("src/Infinium.Bethesda/Infinium.Bethesda.csproj");
            startInfo.ArgumentList.Add("--locked-mode");
            startInfo.ArgumentList.Add("--no-cache");
            startInfo.ArgumentList.Add("--nologo");
            startInfo.ArgumentList.Add("-p:NuGetAudit=false");

            using Process process = Process.Start(startInfo)!;
            bool exited = process.WaitForExit(milliseconds: 30_000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }

            Assert.IsTrue(exited, "Drifted locked restore did not terminate within 30 seconds.");
            string output = $"{process.StandardOutput.ReadToEnd()}{process.StandardError.ReadToEnd()}";
            Assert.AreNotEqual(0, process.ExitCode, output);
            StringAssert.Contains(output, "NU1004");
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }
}
