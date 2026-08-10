using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

#pragma warning disable CA1859 // Contract-shaped collection abstractions keep capture seams narrow.

namespace Infinium.Mo2;


public sealed partial class Mo2SnapshotCapture
{
    private static CaptureModel BuildModel(
        ValidatedPaths paths,
        Mo2SnapshotCaptureRequest request,
        StructuralCapture structure,
        IReadOnlyDictionary<string, ControlFile> controls,
        IReadOnlyList<AdmittedMapping> admittedMappings,
        SkipPolicy skipPolicy,
        ICollection<SnapshotGap> gaps)
    {
        Dictionary<string, string> discoveredMods =
            DiscoverModDirectories(paths, structure);
        IReadOnlyList<ModListLine> listed = ParseModList(controls["modlist"].Bytes, gaps);
        List<string> missing = listed
            .Where(line => !discoveredMods.ContainsKey(line.Name))
            .Select(line => line.Name)
            .ToList();
        foreach (string name in missing)
        {
            gaps.Add(new SnapshotGap(
                "listed-mod-missing",
                "mods",
                $"The listed mod directory is absent: {name}"));
        }

        HashSet<string> listedNames = new(listed.Select(line => line.Name), PathComparer);
        List<ModListLine> resolved = listed
            .Where(line => discoveredMods.ContainsKey(line.Name))
            .Reverse()
            .ToList();
        foreach (string discovered in discoveredMods.Keys
                     .Where(name => !listedNames.Contains(name))
                     .OrderBy(name => name, PathComparer)
                     .ThenBy(name => name, StringComparer.Ordinal))
        {
            resolved.Add(new ModListLine(
                discovered,
                ModEnablementState.Unresolved,
                Listed: false));
            gaps.Add(new SnapshotGap(
                "unlisted-mod-enablement-unresolved",
                "mods",
                $"Discovered mod was not listed; its MO2 object type and enablement are unresolved: {discovered}"));
        }

        List<LocalInstalledEntity> entities = [];
        List<PhysicalInventoryEntry> physicalEntries = [];
        List<ProviderInventory> providers = [];
        AddProvider(
            paths.GameDataRoot,
            LooseProviderKind.PhysicalData,
            "physical-data",
            priority: 0,
            virtualPrefix: string.Empty,
            contributesToEffectiveData: true);

        List<ModState> mods = [];
        for (int index = 0; index < resolved.Count; index++)
        {
            ModListLine item = resolved[index];
            string root = discoveredMods[item.Name];
            OpaqueId entityId = AddProvider(
                root,
                LooseProviderKind.RegularMod,
                 item.Name,
                 index + 1,
                 string.Empty,
                 item.Enablement == ModEnablementState.Enabled);
            mods.Add(new ModState(
                item.Name,
                item.Enablement,
                item.Listed ? index : null,
                item.Listed,
                entityId));
        }

        OpaqueId overwriteId = AddProvider(
            paths.OverwriteRoot,
            LooseProviderKind.Overwrite,
            "overwrite",
            resolved.Count + 1,
            string.Empty,
            contributesToEffectiveData: true);
        _ = overwriteId;

        foreach (AdmittedMapping admitted in admittedMappings)
        {
            QualifiedMapping mapping = admitted.Mapping;
            AddProvider(
                admitted.SourceRoot,
                LooseProviderKind.QualifiedMapper,
                mapping.MappingId,
                resolved.Count + providers.Count + 1,
                NormalizeVirtualPrefix(mapping.VirtualPrefix),
                contributesToEffectiveData: true);
        }

        IReadOnlyList<LooseProviderChain> chains = BuildProviderChains(providers, gaps);
        GamePluginInventory gamePluginInventory = ReadGamePluginInventory(
            controls["instance-ini"].Bytes,
            controls["skyrim-ccc"].Bytes,
            gaps);
        IReadOnlyList<PluginState> plugins = BuildPlugins(
            controls["plugins"].Bytes,
            controls["loadorder"].Bytes,
            chains,
            gamePluginInventory,
            gaps);
        return new CaptureModel(
            mods,
            plugins,
            entities,
            chains,
            physicalEntries,
            missing);

        OpaqueId AddProvider(
            string root,
            LooseProviderKind kind,
            string label,
            int priority,
            string virtualPrefix,
            bool contributesToEffectiveData)
        {
            string fingerprint = FingerprintProviderStructure(structure, root, paths, request);
            string physicalIdentity = ProviderObjectIdentity(
                structure,
                root,
                paths,
                admittedMappings);
            OpaqueId id = StableId(
                "local-entity",
                $"{structure.RootIdentities["instance"]}|{kind}|{physicalIdentity}");
            IReadOnlyList<LocalSourceHint> hints = kind == LooseProviderKind.RegularMod
                && controls.TryGetValue($"mod-meta:{label}", out ControlFile? meta)
                    ? ReadSourceHints(meta.Bytes)
                : [];
            entities.Add(new LocalInstalledEntity(
                id,
                root,
                kind,
                new Sha256Fingerprint(fingerprint),
                Freeze(hints)));
            ProviderInventory inventory = EnumerateProvider(
                id,
                kind,
                root,
                priority,
                virtualPrefix,
                structure,
                paths,
                admittedMappings,
                skipPolicy,
                physicalEntries,
                gaps);
            if (contributesToEffectiveData)
            {
                providers.Add(inventory);
            }

            return id;
        }
    }

    private static IReadOnlyList<LooseProviderChain> BuildProviderChains(
        IReadOnlyList<ProviderInventory> providers,
        ICollection<SnapshotGap> gaps)
    {
        Dictionary<string, List<LooseProvider>> paths = new(PathComparer);
        foreach (ProviderInventory provider in providers.OrderBy(value => value.Priority))
        {
            Dictionary<string, string> providerSpellings = new(PathComparer);
            foreach (string relativePath in provider.EffectiveRelativePaths)
            {
                if (providerSpellings.TryGetValue(relativePath, out string? priorSpelling)
                    && !string.Equals(priorSpelling, relativePath, StringComparison.Ordinal))
                {
                    gaps.Add(new SnapshotGap(
                        "case-collision",
                        "loose-providers",
                        $"Provider contains Windows-equivalent spellings '{priorSpelling}' and '{relativePath}'."));
                    continue;
                }

                providerSpellings[relativePath] = relativePath;
                if (!paths.TryGetValue(relativePath, out List<LooseProvider>? chain))
                {
                    chain = [];
                    paths[relativePath] = chain;
                }

                string physicalRelativePath = relativePath;
                if (!string.IsNullOrEmpty(provider.VirtualPrefix))
                {
                    string prefix = $"{provider.VirtualPrefix}/";
                    if (!relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        gaps.Add(new SnapshotGap(
                            "invalid-qualified-mapping-output",
                            "loose-providers",
                            $"Mapped path is outside its declared virtual prefix: {relativePath}"));
                        continue;
                    }

                    physicalRelativePath = relativePath[prefix.Length..];
                }

                chain.Add(new LooseProvider(
                    provider.EntityId,
                    provider.Kind,
                    Path.Combine(provider.Root, physicalRelativePath),
                    provider.Priority));
            }
        }

        List<LooseProviderChain> result = [];
        foreach ((string relativePath, List<LooseProvider> chain) in paths
                     .OrderBy(pair => pair.Key, PathComparer)
                     .ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            result.Add(new LooseProviderChain(relativePath, Freeze(chain), chain[^1]));
        }

        return result;
    }

    private static ProviderInventory EnumerateProvider(
        OpaqueId entityId,
        LooseProviderKind kind,
        string root,
        int priority,
        string virtualPrefix,
        StructuralCapture structure,
        ValidatedPaths paths,
        IReadOnlyList<AdmittedMapping> admittedMappings,
        SkipPolicy skipPolicy,
        ICollection<PhysicalInventoryEntry> physicalEntries,
        ICollection<SnapshotGap> gaps)
    {
        List<string> effective = [];
        (string rootName, string relativePrefix) = ProviderStructuralLocation(
            root,
            paths,
            admittedMappings);
        List<string> skippedDirectoryPrefixes = [];
        IEnumerable<StructuralEntry> providerEntries = structure.Entries
            .Where(entry => entry.Root == rootName
                            && entry.RelativePath.StartsWith(
                                relativePrefix,
                                StringComparison.OrdinalIgnoreCase))
            .Select(entry => relativePrefix.Length == 0
                ? entry
                : entry with
                {
                    RelativePath = entry.RelativePath[relativePrefix.Length..],
                })
            .OrderBy(entry => PathDepth(entry.RelativePath))
            .ThenBy(entry => entry.RelativePath, PathComparer)
            .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal);
        foreach (StructuralEntry entry in providerEntries)
        {
            string physicalRelative = entry.RelativePath;
            if (skippedDirectoryPrefixes.Any(prefix =>
                    physicalRelative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (entry.IsDirectory)
            {
                PhysicalEntryDisposition? skipped = DirectorySkipDisposition(
                    physicalRelative,
                    skipPolicy);
                if (skipped is not null)
                {
                    skippedDirectoryPrefixes.Add($"{physicalRelative}/");
                    physicalEntries.Add(new PhysicalInventoryEntry(
                        entityId,
                        physicalRelative,
                        0,
                        skipped.Value));
                }

                continue;
            }

            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            PhysicalEntryDisposition disposition;
            string effectiveRelative = physicalRelative;
            if (skipPolicy.FileSuffixes.Any(suffix =>
                    physicalRelative.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            {
                disposition = PhysicalEntryDisposition.HiddenBySuffix;
            }
            else if (IsManagementContent(physicalRelative))
            {
                disposition = PhysicalEntryDisposition.Mo2ManagementContent;
            }
            else
            {
                disposition = PhysicalEntryDisposition.EffectiveDataCandidate;
                effectiveRelative = string.IsNullOrEmpty(virtualPrefix)
                    ? physicalRelative
                    : NormalizeRelativePath(Path.Combine(virtualPrefix, physicalRelative));
                effective.Add(effectiveRelative);
            }

            physicalEntries.Add(new PhysicalInventoryEntry(
                entityId,
                physicalRelative,
                entry.Length,
                disposition));
        }

        return new ProviderInventory(
            entityId,
            kind,
            root,
            priority,
            virtualPrefix,
            effective);
    }

    private static int PathDepth(string value) =>
        value.Count(character => character == '/');

    private static PhysicalEntryDisposition? DirectorySkipDisposition(
        string relativePath,
        SkipPolicy skipPolicy)
    {
        string name = Path.GetFileName(relativePath);
        if (skipPolicy.Directories.Contains(name))
        {
            return PhysicalEntryDisposition.SkippedDirectory;
        }

        return skipPolicy.FileSuffixes.Any(suffix =>
            name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            ? PhysicalEntryDisposition.HiddenBySuffix
            : null;
    }

}
