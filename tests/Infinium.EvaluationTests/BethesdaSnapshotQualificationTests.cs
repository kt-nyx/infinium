using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Mo2;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaSnapshotQualificationTests
{
    private static readonly string[] FixtureIds =
    [
        "BETH-NPC-DEV",
        "BETH-REFR-DEV",
        "BETH-LIGHT-VAL",
        "BETH-MALFORMED-VAL",
        "BETH-UNSUPPORTED-VAL",
    ];

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Integration")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public void FrozenBethesdaInputsCrossAcceptedSlice3SnapshotBoundary()
    {
        foreach (string fixtureId in FixtureIds)
        {
            using SnapshotFixture fixture = new(fixtureId);
            Dictionary<string, string> sourceBefore = fixture.FingerprintSources();
            Assert.IsTrue(fixture.RetainedPluginBytesMatchReceipt(), fixtureId);

            Mo2SnapshotCaptureResult result = fixture.CreateCapture().Capture(fixture.Request);

            Assert.AreEqual(SnapshotCaptureState.Completed, result.State, fixtureId);
            Assert.IsNotNull(result.Snapshot, fixtureId);
            Assert.AreEqual(
                "3.0.0",
                result.Snapshot.Contract.SchemaVersion.ToString(),
                fixtureId);
            Assert.AreEqual(
                "infinium.mo2-static-reconstruction/v3",
                result.Snapshot.AdapterId,
                fixtureId);
            Assert.IsFalse(result.Snapshot.Mo2OrUsvfsLaunched, fixtureId);
            Assert.AreEqual(
                fixture.ExpectedCaptureBindingFingerprint,
                fixture.ComputeCaptureBindingFingerprint(result.Snapshot),
                fixtureId);
            CollectionAssert.AreEqual(
                fixture.ExpectedPluginNames,
                result.Snapshot.Plugins
                    .Where(plugin => plugin.Enabled)
                    .OrderBy(plugin => plugin.LoadOrder)
                    .Select(plugin => plugin.Name)
                    .ToArray(),
                fixtureId);
            CollectionAssert.AreEqual(
                fixture.ExpectedProviderNames,
                result.Snapshot.Mods
                    .Where(mod => mod.Listed)
                    .OrderBy(mod => mod.Priority)
                    .Select(mod => mod.Name)
                    .ToArray(),
                fixtureId);
            foreach (string pluginName in fixture.ExpectedPluginNames)
            {
                LooseProviderChain chain = result.Snapshot.LooseProviderChains.Single(
                    candidate => string.Equals(
                        candidate.NormalizedRelativePath,
                        pluginName,
                        StringComparison.OrdinalIgnoreCase));
                Assert.IsTrue(
                    chain.Winner.PhysicalPath.EndsWith(
                        pluginName,
                        StringComparison.OrdinalIgnoreCase),
                    pluginName);
            }

            CollectionAssert.AreEquivalent(
                sourceBefore,
                fixture.FingerprintSources(),
                fixtureId);
            Assert.IsTrue(fixture.RetainedPluginBytesMatchReceipt(), fixtureId);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    [TestProperty("Category", "M1Fault")]
    public void SameSizeTimestampMutationInvalidatesRetainedBethesdaInputReceipt()
    {
        using SnapshotFixture fixture = new("BETH-NPC-DEV");
        string target = fixture.CapturedPluginPaths[0];
        DateTime originalWriteTime = File.GetLastWriteTimeUtc(target);
        byte[] original = File.ReadAllBytes(target);
        Action mutate = () =>
        {
            byte[] changed = [.. original];
            changed[^1] ^= 0x01;
            File.WriteAllBytes(target, changed);
            File.SetLastWriteTimeUtc(target, originalWriteTime);
        };

        Mo2SnapshotCaptureResult result = fixture.CreateCapture(mutate).Capture(fixture.Request);

        Assert.AreEqual(SnapshotCaptureState.Completed, result.State);
        Assert.IsNotNull(result.Snapshot);
        Assert.AreNotEqual(
            fixture.ExpectedCaptureBindingFingerprint,
            fixture.ComputeCaptureBindingFingerprint(result.Snapshot));
        Assert.IsFalse(fixture.RetainedPluginBytesMatchReceipt());
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    [TestProperty("Category", "M1Security")]
    [TestProperty("Category", "M1Fault")]
    public void MidReadMutationCannotRetainFrozenBethesdaSnapshot()
    {
        using SnapshotFixture fixture = new("BETH-REFR-DEV");
        string target = fixture.CapturedPluginPaths[1];
        string targetRelative = Path.GetRelativePath(fixture.ModsRoot, target)
            .Replace('\\', '/');
        byte[] original = File.ReadAllBytes(target);
        int observations = 0;
        int mutated = 0;
        Action<string, string> mutate = (rootName, relativePath) =>
        {
            if (rootName != "mods"
                || !string.Equals(relativePath, targetRelative, StringComparison.Ordinal)
                || Interlocked.Increment(ref observations) != 2)
            {
                return;
            }

            byte[] changed = [.. original];
            Array.Resize(ref changed, changed.Length + 1);
            changed[^1] = 0x01;
            File.WriteAllBytes(target, changed);
            Interlocked.Exchange(ref mutated, 1);
        };

        Mo2SnapshotCaptureResult result =
            fixture.CreateCapture(beforeEntryOpen: mutate).Capture(fixture.Request);

        Assert.AreEqual(1, mutated);
        Assert.AreEqual(SnapshotCaptureState.ChangedDuringCapture, result.State);
        Assert.IsNull(result.Snapshot);
    }

    private sealed class SnapshotFixture : IDisposable
    {
        private readonly string root;
        private readonly string[] sourcePluginPaths;
        private readonly string[] sourceArtifactIds;
        private readonly string[] expectedPluginSha256;

        internal SnapshotFixture(string fixtureId)
        {
            string tempRoot = Path.GetFullPath(Path.GetTempPath());
            root = Path.Combine(
                tempRoot,
                $"infinium-slice35-snapshot-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            string receiptPath = TestRepository.PathFromRoot(
                "test-data",
                "evaluation",
                "m1-semantic",
                fixtureId,
                "inputs",
                "snapshot",
                "accepted-order.json");
            using JsonDocument receiptDocument =
                JsonDocument.Parse(File.ReadAllBytes(receiptPath));
            JsonElement receipt = receiptDocument.RootElement;
            string constructionManifestPath = TestRepository.PathFromRoot(
                "test-data",
                "evaluation",
                "m1-semantic",
                fixtureId,
                "inputs",
                "construction-manifest.json");
            Assert.AreEqual(
                receipt
                    .GetProperty("construction_manifest_fingerprint")
                    .GetString(),
                Convert.ToHexStringLower(
                    SHA256.HashData(File.ReadAllBytes(constructionManifestPath))),
                fixtureId);
            JsonElement[] providers =
                receipt.GetProperty("provider_order").EnumerateArray().ToArray();
            JsonElement[] plugins =
                receipt.GetProperty("plugin_order").EnumerateArray().ToArray();
            Assert.AreEqual(
                "infinium.fixture-snapshot-capture-binding/v1",
                receipt.GetProperty("capture_binding_algorithm").GetString(),
                fixtureId);
            ExpectedCaptureBindingFingerprint = receipt
                .GetProperty("expected_capture_binding_fingerprint")
                .GetString()!;

            string instanceRoot = Directory.CreateDirectory(
                Path.Combine(root, "instance")).FullName;
            string profilesRoot = Directory.CreateDirectory(
                Path.Combine(instanceRoot, "profiles")).FullName;
            ModsRoot = Directory.CreateDirectory(
                Path.Combine(instanceRoot, "mods")).FullName;
            string overwriteRoot = Directory.CreateDirectory(
                Path.Combine(instanceRoot, "overwrite")).FullName;
            string selectedProfile = receipt.GetProperty("selected_profile_name").GetString()!;
            string profileRoot = Directory.CreateDirectory(
                Path.Combine(profilesRoot, selectedProfile)).FullName;
            string gameRoot = Directory.CreateDirectory(Path.Combine(root, "game")).FullName;
            string gameDataRoot = Directory.CreateDirectory(
                Path.Combine(gameRoot, "Data")).FullName;

            ExpectedProviderNames = providers
                .OrderBy(provider => provider.GetProperty("priority").GetInt32())
                .Select(provider => provider.GetProperty("provider_id").GetString()!)
                .ToArray();
            ExpectedPluginNames = plugins
                .OrderBy(plugin => plugin.GetProperty("load_order").GetInt32())
                .Select(plugin => plugin.GetProperty("file_name").GetString()!)
                .ToArray();
            sourcePluginPaths = plugins
                .Select(plugin =>
                {
                    string[] artifactParts = plugin
                        .GetProperty("artifact_id")
                        .GetString()!
                        .Split('/');
                    return TestRepository.PathFromRoot(
                        [
                            "test-data",
                            "evaluation",
                            "m1-semantic",
                            fixtureId,
                            .. artifactParts,
                        ]);
                })
                .ToArray();
            sourceArtifactIds = plugins
                .Select(plugin => plugin.GetProperty("artifact_id").GetString()!)
                .ToArray();
            expectedPluginSha256 = plugins
                .Select(plugin => plugin.GetProperty("sha256").GetString()!)
                .ToArray();
            CapturedPluginPaths = new string[plugins.Length];

            for (int index = 0; index < plugins.Length; index++)
            {
                string providerName = plugins[index].GetProperty("provider_id").GetString()!;
                string providerRoot = Directory.CreateDirectory(
                    Path.Combine(ModsRoot, providerName)).FullName;
                string target = Path.Combine(
                    providerRoot,
                    plugins[index].GetProperty("file_name").GetString()!);
                File.Copy(sourcePluginPaths[index], target);
                CapturedPluginPaths[index] = target;
            }

            File.WriteAllText(
                Path.Combine(profileRoot, "modlist.txt"),
                string.Join(
                    '\n',
                    ExpectedProviderNames.Reverse().Select(name => $"+{name}")) + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                Path.Combine(profileRoot, "plugins.txt"),
                string.Join('\n', ExpectedPluginNames.Select(name => $"*{name}")) + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                Path.Combine(profileRoot, "loadorder.txt"),
                string.Join('\n', ExpectedPluginNames) + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            string mo2Executable = Path.Combine(root, "ModOrganizer.exe");
            File.WriteAllBytes(mo2Executable, [0x4D, 0x5A]);
            string gamePluginRoot = Directory.CreateDirectory(
                Path.Combine(root, "plugins")).FullName;
            File.WriteAllBytes(
                Path.Combine(gamePluginRoot, "game_skyrimse.dll"),
                [0x4D, 0x5A]);
            string skyrimExecutable = Path.Combine(gameRoot, "SkyrimSE.exe");
            File.WriteAllBytes(skyrimExecutable, [0x4D, 0x5A]);
            string instanceIni = Path.Combine(instanceRoot, "ModOrganizer.ini");
            File.WriteAllText(
                instanceIni,
                "[General]\n"
                + $"selected_profile=@ByteArray({selectedProfile})\n"
                + "gameName=Skyrim Special Edition\n"
                + $"gamePath={gameRoot.Replace('\\', '/')}\n"
                + "[Settings]\n"
                + $"base_directory={instanceRoot.Replace('\\', '/')}\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Request = new Mo2SnapshotCaptureRequest(
                mo2Executable,
                instanceRoot,
                instanceIni,
                profilesRoot,
                ModsRoot,
                overwriteRoot,
                gameDataRoot,
                skyrimExecutable,
                selectedProfile,
                new RuntimeTargetContext("windows-x64", "steam", "489830"),
                [],
                []);
        }

        internal string ModsRoot { get; }

        internal string[] ExpectedPluginNames { get; }

        internal string[] ExpectedProviderNames { get; }

        internal string[] CapturedPluginPaths { get; }

        internal string ExpectedCaptureBindingFingerprint { get; }

        internal Mo2SnapshotCaptureRequest Request { get; }

        internal Mo2SnapshotCapture CreateCapture(
            Action? betweenStructuralCaptures = null,
            Action<string, string>? beforeEntryOpen = null)
        {
            _ = root;
            return new Mo2SnapshotCapture(
                new AcceptingManifests(),
                new FixedProcessProbe(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                betweenStructuralCaptures,
                beforeEntryOpen);
        }

        internal Dictionary<string, string> FingerprintSources()
        {
            return sourcePluginPaths.ToDictionary(
                path => path,
                path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                StringComparer.OrdinalIgnoreCase);
        }

        internal bool RetainedPluginBytesMatchReceipt()
        {
            return CapturedPluginPaths
                .Select((path, index) => string.Equals(
                    Convert.ToHexStringLower(
                        SHA256.HashData(File.ReadAllBytes(path))),
                    expectedPluginSha256[index],
                    StringComparison.Ordinal))
                .All(matches => matches);
        }

        internal string ComputeCaptureBindingFingerprint(Mo2InstallationSnapshot snapshot)
        {
            JsonArray pluginOrder = [];
            foreach (PluginState plugin in snapshot.Plugins
                         .Where(plugin => plugin.Enabled)
                         .OrderBy(plugin => plugin.LoadOrder))
            {
                int index = Array.FindIndex(
                    ExpectedPluginNames,
                    expected => string.Equals(
                        expected,
                        plugin.Name,
                        StringComparison.Ordinal));
                Assert.IsTrue(index >= 0, plugin.Name);
                LooseProviderChain chain = snapshot.LooseProviderChains.Single(
                    candidate => string.Equals(
                        candidate.NormalizedRelativePath,
                        plugin.Name,
                        StringComparison.OrdinalIgnoreCase));
                string providerDirectory = Path.GetDirectoryName(chain.Winner.PhysicalPath)
                    ?? throw new InvalidDataException(
                        "Captured plugin winner does not have a provider directory.");
                string providerId = Path.GetFileName(providerDirectory);
                pluginOrder.Add(new JsonObject
                {
                    ["load_order"] = plugin.LoadOrder,
                    ["file_name"] = plugin.Name,
                    ["artifact_id"] = sourceArtifactIds[index],
                    ["sha256"] = Convert.ToHexStringLower(
                        SHA256.HashData(File.ReadAllBytes(chain.Winner.PhysicalPath))),
                    ["provider_id"] = providerId,
                });
            }

            JsonArray providers = [];
            foreach (ModState provider in snapshot.Mods
                         .Where(mod => mod.Listed)
                         .OrderBy(mod => mod.Priority))
            {
                int index = Array.FindIndex(
                    ExpectedProviderNames,
                    expected => string.Equals(
                        expected,
                        provider.Name,
                        StringComparison.Ordinal));
                Assert.IsTrue(index >= 0, provider.Name);
                providers.Add(new JsonObject
                {
                    ["provider_id"] = provider.Name,
                    ["priority"] = provider.Priority,
                    ["source_artifact_id"] = sourceArtifactIds[index],
                    ["source_sha256"] = Convert.ToHexStringLower(
                        SHA256.HashData(File.ReadAllBytes(CapturedPluginPaths[index]))),
                });
            }

            JsonObject binding = new()
            {
                ["providers"] = providers,
                ["plugin_order"] = pluginOrder,
            };
            using JsonDocument document = JsonDocument.Parse(binding.ToJsonString());
            return BethesdaByteOracleValidator.ComputeCanonicalFingerprint(
                document.RootElement);
        }

        public void Dispose()
        {
            string tempRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar);
            string resolvedRoot = Path.GetFullPath(root);
            if (!resolvedRoot.StartsWith(
                    tempRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(resolvedRoot).StartsWith(
                    "infinium-slice35-snapshot-",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unsafe snapshot test cleanup target.");
            }

            Directory.Delete(resolvedRoot, recursive: true);
        }
    }

    private sealed class AcceptingManifests : IExecutableAdmissionService
    {
        public ExecutableAdmission AdmitMo2(string path) =>
            Accepted(SupportedExecutableManifests.Mo2ManifestId, path);

        public ExecutableAdmission AdmitSkyrimGamePlugin(string path) =>
            Accepted(SupportedExecutableManifests.SkyrimGamePluginManifestId, path);

        public ExecutableAdmission AdmitSkyrim(
            string path,
            RuntimeTargetContext context)
        {
            Assert.AreEqual(
                new RuntimeTargetContext("windows-x64", "steam", "489830"),
                context);
            return Accepted(SupportedExecutableManifests.SkyrimManifestId, path);
        }

        private static ExecutableAdmission Accepted(string manifestId, string path)
        {
            FileInfo file = new(path);
            return new ExecutableAdmission(
                AdmissionState.Accepted,
                manifestId,
                new ExecutableIdentity(
                    file.Name,
                    file.Length,
                    Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                    "slice35-test",
                    null,
                    null,
                    null),
                []);
        }
    }

    private sealed class FixedProcessProbe : IMo2ProcessProbe
    {
        public bool IsRunning(string exactExecutablePath)
        {
            Assert.IsTrue(Path.IsPathFullyQualified(exactExecutablePath));
            return false;
        }
    }
}
