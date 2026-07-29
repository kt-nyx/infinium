using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class DependencyManifestTests
{
    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void DependencyManifestMatchesCentralDirectPackageVersions()
    {
        using JsonDocument manifest = TestRepository.ReadJson("dependencies", "dependency-manifest.json");
        Dictionary<string, string> centralVersions = TestRepository.ReadXml("Directory.Packages.props")
            .Descendants("PackageVersion")
            .ToDictionary(
                element => element.Attribute("Include")!.Value,
                element => element.Attribute("Version")!.Value,
                StringComparer.Ordinal);

        Dictionary<string, string> manifestVersions = manifest.RootElement.GetProperty("directPackages")
            .EnumerateArray()
            .ToDictionary(
                element => element.GetProperty("id").GetString()!,
                element => element.GetProperty("version").GetString()!,
                StringComparer.Ordinal);

        CollectionAssert.AreEquivalent(centralVersions.Keys.ToArray(), manifestVersions.Keys.ToArray());
        foreach ((string packageId, string version) in centralVersions)
        {
            Assert.AreEqual(version, manifestVersions[packageId], packageId);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void DependencyManifestInventoriesEveryResolvedLockIdentity()
    {
        using JsonDocument manifest = TestRepository.ReadJson("dependencies", "dependency-manifest.json");
        string[] manifestPackages = manifest.RootElement.GetProperty("resolvedPackages")
            .EnumerateArray()
            .Select(element => $"{element.GetProperty("id").GetString()}/{element.GetProperty("version").GetString()}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] lockPackages = Directory.GetFiles(TestRepository.Root, "packages.lock.json", SearchOption.AllDirectories)
            .SelectMany(ReadLockPackages)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(lockPackages, manifestPackages);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void DependencyManifestCarriesLicenseAndProvenanceForEveryResolvedPackage()
    {
        using JsonDocument manifest = TestRepository.ReadJson("dependencies", "dependency-manifest.json");
        JsonElement root = manifest.RootElement;
        JsonElement[] resolvedPackages = root.GetProperty("resolvedPackages").EnumerateArray().ToArray();
        int declaredCount = root.GetProperty("lockIdentity").GetProperty("resolvedPackageCount").GetInt32();

        Assert.HasCount(declaredCount, resolvedPackages);
        foreach (JsonElement package in resolvedPackages)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(package.GetProperty("id").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(package.GetProperty("version").GetString()));
            Assert.IsFalse(string.IsNullOrWhiteSpace(package.GetProperty("license").GetString()));
        }

        Dictionary<string, string> versionsById = resolvedPackages.ToDictionary(
            package => package.GetProperty("id").GetString()!,
            package => package.GetProperty("version").GetString()!,
            StringComparer.Ordinal);
        IEnumerable<string> groupedProvenance = root.GetProperty("provenanceGroups")
            .EnumerateArray()
            .SelectMany(group => group.GetProperty("packages").EnumerateArray())
            .Select(package => package.GetString()!)
            .Select(packageId => $"{packageId}/{versionsById[packageId]}");
        IEnumerable<string> individualProvenance = root.GetProperty("individuallyVerifiedProvenance")
            .EnumerateArray()
            .Select(package => package.GetProperty("package").GetString()!);
        IEnumerable<string> explicitLimitations = root.GetProperty("explicitProvenanceLimitations")
            .EnumerateArray()
            .Select(package => package.GetProperty("package").GetString()!);
        string[] provenanceCoverage = groupedProvenance
            .Concat(individualProvenance)
            .Concat(explicitLimitations)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] resolvedIdentities = resolvedPackages
            .Select(package => $"{package.GetProperty("id").GetString()}/{package.GetProperty("version").GetString()}")
            .ToArray();

        CollectionAssert.AreEquivalent(resolvedIdentities, provenanceCoverage);
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Evaluation")]
    public void NoOperativeProjectLicenseWasIntroduced()
    {
        string[] operativeLicenseFiles =
        [
            "LICENSE",
            "LICENSE.md",
            "LICENSE.txt",
            "COPYING",
            "COPYING.md",
            "COPYING.txt",
        ];

        string[] introducedLicenseFiles = EnumerateRepositoryFiles()
            .Where(path => operativeLicenseFiles.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            .ToArray();
        Assert.HasCount(
            0,
            introducedLicenseFiles,
            $"Slice 0 must not introduce an operative license file: {string.Join(", ", introducedLicenseFiles)}");

        string[] projectMetadataFiles = EnumerateRepositoryFiles()
            .Where(path => Path.GetExtension(path) is ".csproj" or ".props" or ".targets")
            .ToArray();
        foreach (string projectMetadataFile in projectMetadataFiles)
        {
            string content = File.ReadAllText(projectMetadataFile);
            Assert.IsFalse(
                content.Contains("<PackageLicenseExpression>", StringComparison.Ordinal),
                $"Slice 0 must not introduce an operative selector in '{projectMetadataFile}'.");
        }

        string buildProperties = TestRepository.Read("Directory.Build.props");
        StringAssert.Contains(buildProperties, "operative SPDX selector deferred");
    }

    private static IEnumerable<string> EnumerateRepositoryFiles()
    {
        string[] excludedSegments =
        [
            ".git",
            ".packages",
            "artifacts",
            "bin",
            "obj",
            "TestResults",
        ];

        return Directory.EnumerateFiles(TestRepository.Root, "*", SearchOption.AllDirectories)
            .Where(path => !path
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => excludedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)));
    }

    private static IEnumerable<string> ReadLockPackages(string lockPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(lockPath));
        foreach (JsonProperty framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
        {
            foreach (JsonProperty package in framework.Value.EnumerateObject())
            {
                string version = package.Value.GetProperty("resolved").GetString()!;
                yield return $"{package.Name}/{version}";
            }
        }
    }
}
