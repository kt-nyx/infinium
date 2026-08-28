using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class DependencyManifestTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
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
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
    public void DependencyManifestInventoriesEveryResolvedLockIdentity()
    {
        using JsonDocument manifest = TestRepository.ReadJson("dependencies", "dependency-manifest.json");
        string[] manifestPackages = manifest.RootElement.GetProperty("resolvedPackages")
            .EnumerateArray()
            .Select(element => $"{element.GetProperty("id").GetString()}/{element.GetProperty("version").GetString()}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] projectFiles = TestRepository
            .EnumerateProjectFiles()
            .Concat(Directory.EnumerateFiles(
                TestRepository.PathFromRoot("eng", "tooling"),
                "*.csproj",
                SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.HasCount(21, projectFiles, "The dependency inventory must cover every product/test project plus the repository frontend toolchain.");
        string[] lockFiles = projectFiles
            .Select(Path.GetDirectoryName)
            .OfType<string>()
            .Select(projectDirectory => Path.Combine(projectDirectory, "packages.lock.json"))
            .ToArray();
        Assert.IsTrue(lockFiles.All(File.Exists), "Every inventoried project must retain its lock file.");
        string[] manifestLockPaths = manifest.RootElement.GetProperty("lockIdentity")
            .GetProperty("projectLocks")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        string[] expectedLockPaths = lockFiles
            .Select(path => Path.GetRelativePath(TestRepository.Root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(expectedLockPaths, manifestLockPaths);
        string[] lockPackages = lockFiles
            .SelectMany(ReadLockPackages)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(lockPackages, manifestPackages);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
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
            string license = package.GetProperty("license").GetString()!;
            Assert.IsFalse(string.IsNullOrWhiteSpace(license));
            Assert.AreNotEqual(
                "Package license file:",
                license.Trim(),
                $"{package.GetProperty("id").GetString()} has an empty license-file classification.");
        }

        JsonElement[] provenanceGroups = root.GetProperty("provenanceGroups")
            .EnumerateArray()
            .ToArray();
        Assert.IsNotEmpty(
            provenanceGroups,
            "Accepted repository-level provenance groups must survive deterministic regeneration.");
        IEnumerable<string> groupedProvenance = provenanceGroups
            .SelectMany(group => group.GetProperty("packages").EnumerateArray())
            .Select(package => package.GetString()!);
        IEnumerable<string> individualProvenance = root.GetProperty("individuallyVerifiedProvenance")
            .EnumerateArray()
            .Select(package => package.GetProperty("package").GetString()!);
        IEnumerable<string> explicitLimitations = root.GetProperty("explicitProvenanceLimitations")
            .EnumerateArray()
            .Select(package => package.GetProperty("package").GetString()!);
        string[] provenanceCoverage = groupedProvenance
            .Concat(individualProvenance)
            .Concat(explicitLimitations)
            .ToArray();
        string[] resolvedIdentities = resolvedPackages
            .Select(package => $"{package.GetProperty("id").GetString()}/{package.GetProperty("version").GetString()}")
            .ToArray();

        CollectionAssert.AreEquivalent(resolvedIdentities, provenanceCoverage);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
    public void DependencyManifestPreservesCuratedLicensesAndActualPackageHashes()
    {
        using JsonDocument manifest = TestRepository.ReadJson("dependencies", "dependency-manifest.json");
        using JsonDocument curation = TestRepository.ReadJson("dependencies", "dependency-curation.json");
        Dictionary<string, string> manifestLicenses = manifest.RootElement
            .GetProperty("resolvedPackages")
            .EnumerateArray()
            .ToDictionary(
                package => $"{package.GetProperty("id").GetString()}/{package.GetProperty("version").GetString()}",
                package => package.GetProperty("license").GetString()!,
                StringComparer.Ordinal);

        foreach (JsonProperty curatedLicense in curation.RootElement.GetProperty("licenses").EnumerateObject())
        {
            Assert.IsTrue(
                manifestLicenses.TryGetValue(curatedLicense.Name, out string? actualLicense),
                $"Curated package '{curatedLicense.Name}' is absent from the resolved manifest.");
            Assert.AreEqual(curatedLicense.Value.GetString(), actualLicense, curatedLicense.Name);
        }

        foreach (JsonElement package in manifest.RootElement.GetProperty("directPackages").EnumerateArray())
        {
            string id = package.GetProperty("id").GetString()!;
            string version = package.GetProperty("version").GetString()!;
            string packageDirectory = Path.Combine(
                TestRepository.Root,
                ".packages",
                id.ToLowerInvariant(),
                version);
            string shaPath = Directory.EnumerateFiles(packageDirectory, "*.nupkg.sha512").Single();
            string actualSha512 = File.ReadAllText(shaPath).Trim();
            Assert.AreEqual(
                package.GetProperty("nupkgSha512").GetString(),
                actualSha512,
                $"{id}/{version}");
        }
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestProperty("Category", "Evaluation")]
    public void NoOperativeProjectLicenseWasIntroduced()
    {
        string[] operativeLicenseFiles =
        [
            "LICENSE",
            "LICENSE.md",
            "LICENSE.txt",
            "LICENCE",
            "LICENCE.md",
            "LICENCE.txt",
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
            $"The current dependency policy must not introduce an operative license file: {string.Join(", ", introducedLicenseFiles)}");

        string[] projectMetadataFiles = EnumerateRepositoryFiles()
            .Where(path => Path.GetExtension(path) is ".csproj" or ".props" or ".targets")
            .ToArray();
        foreach (string projectMetadataFile in projectMetadataFiles)
        {
            XDocument projectMetadata = XDocument.Load(projectMetadataFile);
            string[] operativeProperties = projectMetadata
                .Descendants()
                .Select(element => element.Name.LocalName)
                .Where(name =>
                    name.Equals("PackageLicenseExpression", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("PackageLicenseFile", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.HasCount(
                0,
                operativeProperties,
                $"The current dependency policy must not introduce operative license metadata in '{projectMetadataFile}'.");
        }

        string buildProperties = TestRepository.Read("Directory.Build.props");
        StringAssert.Contains(buildProperties, "operative SPDX selector deferred");

        ProcessStartInfo gitStart = new("git")
        {
            WorkingDirectory = TestRepository.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        gitStart.ArgumentList.Add("ls-files");
        gitStart.ArgumentList.Add("--");
        gitStart.ArgumentList.Add("work");
        using Process git = Process.Start(gitStart) ?? throw new InvalidOperationException("Could not start Git.");
        string trackedWork = git.StandardOutput.ReadToEnd();
        string gitError = git.StandardError.ReadToEnd();
        git.WaitForExit();
        Assert.AreEqual(0, git.ExitCode, gitError);
        Assert.AreEqual(string.Empty, trackedWork, "The generated-root exclusion is valid only while work remains untracked.");
    }

    private static IEnumerable<string> EnumerateRepositoryFiles()
    {
        return Directory.EnumerateFiles(TestRepository.Root, "*", SearchOption.AllDirectories)
            .Where(path => !TestRepository.IsGeneratedPath(path));
    }

    private static IEnumerable<string> ReadLockPackages(string lockPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(lockPath));
        foreach (JsonProperty framework in document.RootElement.GetProperty("dependencies").EnumerateObject())
        {
            foreach (JsonProperty package in framework.Value.EnumerateObject())
            {
                if (package.Value.TryGetProperty("resolved", out JsonElement resolved))
                {
                    string version = resolved.GetString()!;
                    yield return $"{package.Name}/{version}";
                }
            }
        }
    }
}
