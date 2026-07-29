using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class BuildPolicyTests
{
    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void GlobalJsonPinsTheExactSupportedSdk()
    {
        using JsonDocument document = TestRepository.ReadJson("global.json");
        JsonElement sdk = document.RootElement.GetProperty("sdk");

        Assert.AreEqual("10.0.302", sdk.GetProperty("version").GetString());
        Assert.AreEqual("disable", sdk.GetProperty("rollForward").GetString());
        Assert.IsFalse(sdk.GetProperty("allowPrerelease").GetBoolean());
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void BuildPolicyPinsTheDeterministicSupportedTarget()
    {
        XDocument buildProperties = TestRepository.ReadXml("Directory.Build.props");
        Dictionary<string, string> properties = buildProperties
            .Descendants()
            .Where(element => !element.HasElements)
            .ToDictionary(element => element.Name.LocalName, element => element.Value, StringComparer.Ordinal);

        Assert.AreEqual("net10.0", properties["TargetFramework"]);
        Assert.AreEqual("x64", properties["PlatformTarget"]);
        Assert.AreEqual("14.0", properties["LangVersion"]);
        Assert.AreEqual("true", properties["Deterministic"]);
        Assert.AreEqual("true", properties["ContinuousIntegrationBuild"]);
        Assert.AreEqual("true", properties["EnableNETAnalyzers"]);
        Assert.AreEqual("latest-recommended", properties["AnalysisLevel"]);
        Assert.AreEqual("true", properties["EnforceCodeStyleInBuild"]);
        Assert.AreEqual("true", properties["TreatWarningsAsErrors"]);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CentralPackagePolicyPinsOnlyAcceptedSliceZeroPackages()
    {
        Dictionary<string, string> expectedPackages = new(StringComparer.Ordinal)
        {
            ["Microsoft.NET.Test.Sdk"] = "18.8.1",
            ["MSTest.TestAdapter"] = "4.3.2",
            ["MSTest.TestFramework"] = "4.3.2",
            ["Mutagen.Bethesda.Skyrim"] = "0.54.2",
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
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Security")]
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
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Fault")]
    public void RestorePolicyRequiresLockFiles()
    {
        XDocument buildProperties = TestRepository.ReadXml("Directory.Build.props");
        XElement lockFileProperty = buildProperties.Descendants("RestorePackagesWithLockFile").Single();
        XElement lockedModeProperty = buildProperties.Descendants("RestoreLockedMode").Single();

        Assert.AreEqual("true", lockFileProperty.Value);
        Assert.AreEqual("true", lockedModeProperty.Value);
    }
}
