using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

#pragma warning disable CA1859 // Contract-shaped collection abstractions keep capture seams narrow.

namespace Infinium.Mo2;


public sealed partial class Mo2SnapshotCapture
{
    private IReadOnlyList<AdmittedMapping> ResolveAdmittedMappings(
        Mo2SnapshotCaptureRequest request,
        ICollection<SnapshotGap> gaps)
    {
        HashSet<string> enabledMapperHashes = new(
            request.EnabledMapperSha256s.Select(NormalizeSha256),
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> seenIds = new(StringComparer.Ordinal);
        HashSet<string> seenSources = new(PathComparer);
        List<AdmittedMapping> admitted = [];
        foreach (QualifiedMapping mapping in request.QualifiedMappings)
        {
            ArgumentNullException.ThrowIfNull(mapping);
            if (string.IsNullOrWhiteSpace(mapping.MappingId)
                || !seenIds.Add(mapping.MappingId))
            {
                throw new InvalidDataException(
                    "Qualified mapping identifiers must be non-empty and unique.");
            }

            string mapperHash = NormalizeSha256(mapping.MapperSha256);
            if (!enabledMapperHashes.Contains(mapperHash)
                || !qualifiedMapperHashes.Contains(mapperHash))
            {
                gaps.Add(new SnapshotGap(
                    "unknown-or-unqualified-mapper",
                    "loose-providers",
                    $"Mapping '{mapping.MappingId}' is not admitted by the exact mapper inventory."));
                continue;
            }

            string source = ExistingDirectory(mapping.SourceRoot);
            RejectReparsePoint(source);
            if (!seenSources.Add(source))
            {
                throw new InvalidDataException(
                    "Qualified mappings must not alias the same physical source root.");
            }

            admitted.Add(new AdmittedMapping(mapping, source));
        }

        foreach (string mapperHash in enabledMapperHashes)
        {
            if (!request.QualifiedMappings.Any(mapping =>
                    string.Equals(
                        NormalizeSha256(mapping.MapperSha256),
                        mapperHash,
                        StringComparison.OrdinalIgnoreCase)))
            {
                gaps.Add(new SnapshotGap(
                    "enabled-mapper-without-qualified-mapping",
                    "loose-providers",
                    $"Enabled mapper {mapperHash} has no qualified static mapping description."));
            }
        }

        return admitted.ToArray();
    }

    private static Dictionary<string, string> DiscoverModDirectories(
        ValidatedPaths paths,
        StructuralCapture structure)
    {
        Dictionary<string, string> result = new(PathComparer);
        foreach (StructuralEntry entry in structure.Entries.Where(entry =>
                     entry.Root == "mods"
                     && entry.IsDirectory
                     && (entry.Attributes & FileAttributes.ReparsePoint) == 0
                     && !entry.RelativePath.Contains('/')))
        {
            if (!result.TryAdd(
                    entry.RelativePath,
                    Path.Combine(paths.ModsRoot, entry.RelativePath)))
            {
                throw new InvalidDataException(
                    $"Multiple Windows-equivalent mod directories were observed: {entry.RelativePath}");
            }
        }

        return result;
    }

    private static (string RootName, string RelativePrefix) ProviderStructuralLocation(
        string root,
        ValidatedPaths paths,
        IReadOnlyList<AdmittedMapping> admittedMappings)
    {
        if (PathComparer.Equals(root, paths.GameDataRoot))
        {
            return ("game-data", string.Empty);
        }

        if (PathComparer.Equals(root, paths.OverwriteRoot))
        {
            return ("overwrite", string.Empty);
        }

        if (Path.GetDirectoryName(root) is string parent
            && PathComparer.Equals(parent, paths.ModsRoot))
        {
            return ("mods", $"{NormalizeRelativePath(Path.GetFileName(root))}/");
        }

        AdmittedMapping? mapping = admittedMappings.FirstOrDefault(value =>
            PathComparer.Equals(value.SourceRoot, root));
        return mapping is null
            ? throw new InvalidDataException("A provider root has no admitted structural source.")
            : ($"mapping:{mapping.Mapping.MappingId}", string.Empty);
    }

    private static string ProviderObjectIdentity(
        StructuralCapture structure,
        string root,
        ValidatedPaths paths,
        IReadOnlyList<AdmittedMapping> admittedMappings)
    {
        (string rootName, string prefix) = ProviderStructuralLocation(
            root,
            paths,
            admittedMappings);
        if (prefix.Length == 0)
        {
            return structure.RootIdentities[rootName];
        }

        string directoryName = prefix.TrimEnd('/');
        return structure.Entries.Single(entry =>
                entry.Root == rootName
                && entry.IsDirectory
                && string.Equals(
                    entry.RelativePath,
                    directoryName,
                    StringComparison.OrdinalIgnoreCase))
            .ObjectIdentity
            ?? throw new InvalidDataException(
                "A local mod directory is missing its captured physical identity.");
    }

    private static bool SameAdmission(
        ExecutableAdmission initial,
        ExecutableAdmission current)
    {
        return initial.State == AdmissionState.Accepted
            && current.State == AdmissionState.Accepted
            && initial.ObservedIdentity is not null
            && current.ObservedIdentity is not null
            && initial.ObservedIdentity == current.ObservedIdentity;
    }

    private static string FingerprintProviderStructure(
        StructuralCapture structure,
        string root,
        ValidatedPaths paths,
        Mo2SnapshotCaptureRequest request)
    {
        string rootName;
        if (PathComparer.Equals(root, paths.GameDataRoot))
        {
            rootName = "game-data";
        }
        else if (PathComparer.Equals(root, paths.OverwriteRoot))
        {
            rootName = "overwrite";
        }
        else if (Path.GetDirectoryName(root) is string parent
                 && PathComparer.Equals(parent, paths.ModsRoot))
        {
            rootName = "mods";
            string prefix = $"{NormalizeRelativePath(Path.GetFileName(root))}/";
            return HashStructuralEntries(
                structure.Entries.Where(entry =>
                    entry.Root == rootName
                    && entry.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)),
                prefix);
        }
        else
        {
            QualifiedMapping? mapping = request.QualifiedMappings.FirstOrDefault(
                value => PathComparer.Equals(Path.GetFullPath(value.SourceRoot), root));
            rootName = mapping is null ? "unknown" : $"mapping:{mapping.MappingId}";
        }

        return HashStructuralEntries(
            structure.Entries.Where(entry => entry.Root == rootName),
            string.Empty);
    }

    private static string HashStructuralEntries(
        IEnumerable<StructuralEntry> entries,
        string relativePrefix)
    {
        return HashUtf8(string.Join(
            '\n',
            entries
                .Select(entry => new
                {
                    RelativePath = entry.RelativePath[relativePrefix.Length..],
                    entry.IsDirectory,
                    entry.Length,
                    entry.LastWriteUtcTicks,
                    entry.Attributes,
                })
                .OrderBy(entry => entry.RelativePath, PathComparer)
                .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal)
                .Select(entry => FormattableString.Invariant(
                    $"{entry.RelativePath}|{entry.IsDirectory}|{entry.Length}|{entry.LastWriteUtcTicks}|{entry.Attributes}"))));
    }

    private static Mo2SnapshotDependencyManifest BuildDependencyManifest(
        string canonicalFingerprint,
        ValidatedPaths paths,
        Mo2SnapshotCaptureRequest request,
        StructuralCapture structure,
        IReadOnlyDictionary<string, ControlFile> controls,
        IReadOnlyList<AdmittedMapping> admittedMappings,
        IReadOnlySet<string> qualifiedMapperHashes,
        ExecutableAdmission mo2Admission,
        ExecutableAdmission gamePluginAdmission,
        ExecutableAdmission runtimeAdmission)
    {
        HashSet<string> admittedMappingIds = new(
            admittedMappings.Select(mapping => mapping.Mapping.MappingId),
            StringComparer.Ordinal);
        IReadOnlyList<SnapshotControlObservation> controlObservations =
            Freeze(controls
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new SnapshotControlObservation(
                    pair.Key,
                    pair.Value.Path,
                    pair.Value.Exists,
                    pair.Value.Bytes.LongLength,
                    new Sha256Fingerprint(pair.Value.Sha256),
                    Convert.ToBase64String(pair.Value.Bytes),
                    pair.Value.PhysicalObjectIdentity)));
        IReadOnlyList<SnapshotRootObservation> rootObservations =
            Freeze(structure.RootIdentities
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new SnapshotRootObservation(
                    pair.Key,
                    ResolveStructuralRootPath(pair.Key, paths, admittedMappings),
                    pair.Value)));
        IReadOnlyList<SnapshotStructuralObservation> structuralObservations =
            Freeze(structure.Entries
                .OrderBy(entry => entry.Root, StringComparer.Ordinal)
                .ThenBy(entry => entry.RelativePath, PathComparer)
                .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal)
                .Select(entry => new SnapshotStructuralObservation(
                    entry.Root,
                    entry.RelativePath,
                    entry.IsDirectory,
                    entry.Length,
                    entry.LastWriteUtcTicks,
                    entry.Attributes,
                    entry.ObjectIdentity)));
        IReadOnlyList<SnapshotMappingDependency> mappingDependencies =
            Freeze(request.QualifiedMappings
                .OrderBy(mapping => mapping.MappingId, StringComparer.Ordinal)
                .Select(mapping => new SnapshotMappingDependency(
                    mapping.MappingId,
                    Path.GetFullPath(mapping.SourceRoot),
                    NormalizeVirtualPrefix(mapping.VirtualPrefix),
                    new Sha256Fingerprint(mapping.MapperSha256),
                    admittedMappingIds.Contains(mapping.MappingId))));
        return new Mo2SnapshotDependencyManifest(
            new ContractVersion(1, 0, 0),
            new Sha256Fingerprint(canonicalFingerprint),
            SupportedExecutableManifests.AdapterId,
            request.ManagerId,
            request.SelectedProfileName,
            request.RuntimeTarget,
            mo2Admission.ObservedIdentity!,
            gamePluginAdmission.ObservedIdentity!,
            runtimeAdmission.ObservedIdentity!,
            Freeze(request.EnabledMapperSha256s
                .Select(NormalizeSha256)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)),
            Freeze(qualifiedMapperHashes
                .Select(NormalizeSha256)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)),
            controlObservations,
            rootObservations,
            structuralObservations,
            mappingDependencies);
    }

    private static string ResolveStructuralRootPath(
        string role,
        ValidatedPaths paths,
        IReadOnlyList<AdmittedMapping> admittedMappings)
    {
        return role switch
        {
            "instance" => paths.InstanceRoot,
            "profile" => paths.ProfileRoot,
            "mods" => paths.ModsRoot,
            "overwrite" => paths.OverwriteRoot,
            "game-data" => paths.GameDataRoot,
            _ when role.StartsWith("mapping:", StringComparison.Ordinal) =>
                admittedMappings.Single(mapping =>
                    string.Equals(
                        mapping.Mapping.MappingId,
                        role["mapping:".Length..],
                        StringComparison.Ordinal)).SourceRoot,
            _ => throw new InvalidDataException(
                $"Unknown structural root role '{role}'."),
        };
    }

    private static string HashUtf8(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private static OpaqueId StableId(string prefix, string value)
    {
        string hash = HashUtf8(value);
        return new OpaqueId($"{prefix}-{hash[..24].ToLowerInvariant()}");
    }

    private static void AddAdmissionGaps(
        ICollection<SnapshotGap> gaps,
        string population,
        ExecutableAdmission admission)
    {
        if (admission.State == AdmissionState.Accepted)
        {
            return;
        }

        gaps.Add(new SnapshotGap(
            $"{population}-{admission.State.ToString().ToLowerInvariant()}",
            population,
            string.Join("; ", admission.Reasons)));
    }

    private sealed record ValidatedPaths(
        string Mo2Executable,
        string SkyrimGamePlugin,
        string InstanceRoot,
        string InstanceIni,
        string ProfilesRoot,
        string ProfileRoot,
        string ModsRoot,
        string OverwriteRoot,
        string GameDataRoot,
        string SkyrimExecutable);

    private sealed record StructuralEntry(
        string Root,
        string RelativePath,
        bool IsDirectory,
        long Length,
        long LastWriteUtcTicks,
        FileAttributes Attributes,
        string? ObjectIdentity);

    private sealed record StructuralCapture(
        string Fingerprint,
        IReadOnlyList<StructuralEntry> Entries,
        IReadOnlyDictionary<string, string> RootIdentities);

    private sealed record ControlFile(
        string Path,
        string RootPath,
        string RelativePath,
        byte[] Bytes,
        string Sha256,
        bool Exists,
        string? PhysicalObjectIdentity);

    private sealed record ModListLine(
        string Name,
        ModEnablementState Enablement,
        bool Listed);

    private sealed record PluginLine(string Name, bool Enabled);

    private sealed record GamePluginInventory(
        bool ForceEnableCoreFiles,
        IReadOnlyList<string> PrimaryPlugins,
        IReadOnlySet<string> PrimaryPluginSet,
        IReadOnlySet<string> CreationClubPluginSet);

    private sealed record ProviderInventory(
        OpaqueId EntityId,
        LooseProviderKind Kind,
        string Root,
        int Priority,
        string VirtualPrefix,
        IReadOnlyList<string> EffectiveRelativePaths);

    private sealed record AdmittedMapping(QualifiedMapping Mapping, string SourceRoot);

    private sealed record SkipPolicy(
        IReadOnlyList<string> FileSuffixes,
        IReadOnlySet<string> Directories);

    private sealed record CaptureModel(
        IReadOnlyList<ModState> Mods,
        IReadOnlyList<PluginState> Plugins,
        IReadOnlyList<LocalInstalledEntity> Entities,
        IReadOnlyList<LooseProviderChain> ProviderChains,
        IReadOnlyList<PhysicalInventoryEntry> PhysicalEntries,
        IReadOnlyList<string> MissingListedMods);
}
