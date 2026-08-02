using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;
using Infinium.Mo2;

namespace Infinium.Tests;

internal static class BethesdaSemanticTestSnapshot
{
    internal static BethesdaSemanticRequest Create(
        string fixtureId,
        IReadOnlyDictionary<string, string>? replacements = null,
        IReadOnlyList<BethesdaUnsupportedCapability>? unsupportedCapabilities = null)
    {
        string fixtureRoot = TestRepository.PathFromRoot(
            "test-data",
            "evaluation",
            "m1-semantic",
            fixtureId);
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(fixtureRoot, "inputs", "snapshot", "accepted-order.json")));
        JsonElement[] inputs = receipt.RootElement
            .GetProperty("plugin_order")
            .EnumerateArray()
            .OrderBy(item => item.GetProperty("load_order").GetInt32())
            .ToArray();
        List<(string Name, int Order, string Path, OpaqueId Entity)> plugins = [];
        foreach (JsonElement input in inputs)
        {
            string name = input.GetProperty("file_name").GetString()!;
            string artifactId = replacements is not null
                                && replacements.TryGetValue(name, out string? replacement)
                ? replacement
                : input.GetProperty("artifact_id").GetString()!;
            string path = Path.IsPathFullyQualified(artifactId)
                ? artifactId
                : Path.Combine([fixtureRoot, .. artifactId.Split('/')]);
            plugins.Add((
                name,
                input.GetProperty("load_order").GetInt32(),
                Path.GetFullPath(path),
                new OpaqueId($"fixture-provider-{input.GetProperty("load_order").GetInt32():D3}")));
        }

        return Create(plugins, unsupportedCapabilities);
    }

    internal static BethesdaSemanticRequest CreateSelected(
        string fixtureId,
        IReadOnlyCollection<string> selectedPluginNames,
        IReadOnlyDictionary<string, string>? replacements = null)
    {
        string fixtureRoot = TestRepository.PathFromRoot(
            "test-data",
            "evaluation",
            "m1-semantic",
            fixtureId);
        using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(fixtureRoot, "inputs", "snapshot", "accepted-order.json")));
        List<(string Name, int Order, string Path, OpaqueId Entity)> plugins = receipt.RootElement
            .GetProperty("plugin_order")
            .EnumerateArray()
            .Where(item => selectedPluginNames.Contains(
                item.GetProperty("file_name").GetString()!,
                StringComparer.OrdinalIgnoreCase))
            .OrderBy(item => item.GetProperty("load_order").GetInt32())
            .Select(item =>
            {
                string name = item.GetProperty("file_name").GetString()!;
                int order = item.GetProperty("load_order").GetInt32();
                string artifactId = replacements is not null
                                    && replacements.TryGetValue(name, out string? replacement)
                    ? replacement
                    : item.GetProperty("artifact_id").GetString()!;
                return (
                    name,
                    order,
                    Path.GetFullPath(Path.Combine([fixtureRoot, .. artifactId.Split('/')])),
                    new OpaqueId($"fixture-provider-{order:D3}"));
            })
            .ToList();
        if (plugins.Count != selectedPluginNames.Count)
        {
            throw new InvalidOperationException("A selected fixture plugin was not present in the accepted order.");
        }

        return Create(plugins);
    }

    internal static BethesdaSemanticRequest Create(
        IReadOnlyList<(string Name, int Order, string Path, OpaqueId Entity)> plugins,
        IReadOnlyList<BethesdaUnsupportedCapability>? unsupportedCapabilities = null)
    {
        const string digest = "0000000000000000000000000000000000000000000000000000000000000000";
        DateTimeOffset capturedAt = new(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
        string structuralMaterial = string.Join('|', plugins.Select(item => string.Join(
            '|',
            item.Name,
            item.Order,
            item.Entity.Value,
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(item.Path))))));
        Sha256Fingerprint structural = new(Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(structuralMaterial))));
        InstallationSnapshotContract contract = new(
            Mo2SnapshotCanonicalization.ComputeSnapshotId(structural, new UtcTimestamp(capturedAt)),
            new ContractVersion(3, 0, 0),
            new OpaqueId("fixture-instance"),
            new OpaqueId("fixture-profile"),
            structural,
            [],
            plugins.Select(item => item.Entity).ToArray(),
            new UtcTimestamp(capturedAt));
        ExecutableIdentity executable = new("fixture.exe", 2, digest, null, null, null, null, "fixture-object");
        ExecutableAdmission admission = new(AdmissionState.Accepted, "fixture-admission", executable, []);
        RuntimeTargetContext target = new("windows-x64", "steam", "489830");
        Mo2SnapshotDependencyManifest dependencies = new(
            new ContractVersion(3, 0, 0),
            structural,
            "infinium.mo2-static-reconstruction/v3",
            "mod-organizer-2",
            "fixture-profile",
            target,
            executable,
            executable,
            executable,
            [],
            [],
            [],
            [],
            [],
            []);
        PluginState[] pluginStates = plugins.Select(item => new PluginState(
            item.Name,
            PluginEnablementState.EnabledByProfile,
            PluginClassification.Regular,
            item.Order,
            item.Entity,
            "correlated")).ToArray();
        LocalInstalledEntity[] entities = plugins.Select(item => new LocalInstalledEntity(
            item.Entity,
            Path.GetDirectoryName(item.Path)!,
            LooseProviderKind.RegularMod,
            structural,
            [])).ToArray();
        LooseProviderChain[] chains = plugins.Select(item =>
        {
            LooseProvider provider = new(item.Entity, LooseProviderKind.RegularMod, item.Path, item.Order);
            return new LooseProviderChain(item.Name, [provider], provider);
        }).ToArray();
        Mo2InstallationSnapshot snapshot = new(
            contract,
            "infinium.mo2-static-reconstruction/v3",
            Path.GetDirectoryName(plugins[0].Path)!,
            Path.GetDirectoryName(plugins[0].Path)!,
            "fixture-profile",
            admission,
            admission,
            admission,
            dependencies,
            [],
            pluginStates,
            entities,
            chains,
            [],
            [],
            [],
            false,
            false);
        return new BethesdaSemanticRequest(
            new Mo2SnapshotCaptureResult(SnapshotCaptureState.Completed, snapshot, []),
            unsupportedCapabilities);
    }
}
