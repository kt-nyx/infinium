using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

#pragma warning disable CA1859 // Contract-shaped collection abstractions keep capture seams narrow.

namespace Infinium.Mo2;

public sealed class Mo2SnapshotCapture
{
    private const int MaximumControlBytes = 4 * 1024 * 1024;
    private const int MaximumEntries = 500_000;

    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IExecutableAdmissionService manifests;
    private readonly IMo2ProcessProbe processProbe;
    private readonly IReadOnlySet<string> qualifiedMapperHashes;
    private readonly Action? betweenStructuralCaptures;

    public Mo2SnapshotCapture()
        : this(
            new SupportedExecutableManifests(),
            new WindowsMo2ProcessProbe(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            null)
    {
    }

    internal Mo2SnapshotCapture(
        IExecutableAdmissionService manifests,
        IMo2ProcessProbe processProbe,
        IReadOnlySet<string> qualifiedMapperHashes,
        Action? betweenStructuralCaptures)
    {
        this.manifests = manifests;
        this.processProbe = processProbe;
        this.qualifiedMapperHashes = qualifiedMapperHashes;
        this.betweenStructuralCaptures = betweenStructuralCaptures;
    }

    public Mo2SnapshotCaptureResult Capture(
        Mo2SnapshotCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RuntimeTarget);
        ArgumentNullException.ThrowIfNull(request.QualifiedMappings);
        ArgumentNullException.ThrowIfNull(request.EnabledMapperSha256s);
        List<SnapshotGap> gaps = [];

        ValidatedPaths? paths;
        try
        {
            paths = ValidatePaths(request);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or UnauthorizedAccessException)
        {
            gaps.Add(new SnapshotGap(
                "invalid-or-inaccessible-configuration",
                "mo2-configuration",
                exception.Message));
            return new Mo2SnapshotCaptureResult(
                SnapshotCaptureState.Failed,
                null,
                Freeze(gaps));
        }

        ExecutableAdmission mo2Admission = manifests.AdmitMo2(paths.Mo2Executable);
        ExecutableAdmission runtimeAdmission = manifests.AdmitSkyrim(
            paths.SkyrimExecutable,
            request.RuntimeTarget);
        if (mo2Admission.State != AdmissionState.Accepted
            || runtimeAdmission.State != AdmissionState.Accepted)
        {
            AddAdmissionGaps(gaps, "mo2-identity", mo2Admission);
            AddAdmissionGaps(gaps, "runtime-identity", runtimeAdmission);
            return new Mo2SnapshotCaptureResult(
                SnapshotCaptureState.Failed,
                null,
                Freeze(gaps));
        }

        if (processProbe.IsRunning(paths.Mo2Executable))
        {
            gaps.Add(new SnapshotGap(
                "mo2-not-quiescent",
                "snapshot",
                "The selected MO2 executable is running; capture requires a closed instance."));
            return new Mo2SnapshotCaptureResult(
                SnapshotCaptureState.Failed,
                null,
                Freeze(gaps));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<AdmittedMapping> admittedMappings =
                ResolveAdmittedMappings(request, gaps);
            StructuralCapture first = CaptureStructure(
                paths,
                admittedMappings,
                gaps,
                cancellationToken);
            Dictionary<string, ControlFile> controls = ReadControls(paths);
            AddMetaControls(paths, first, controls);
            foreach (string required in new[] { "modlist", "plugins", "loadorder" })
            {
                if (!controls[required].Exists)
                {
                    gaps.Add(new SnapshotGap(
                        "required-control-file-missing",
                        required,
                        $"Required selected-profile control is missing: {controls[required].Path}"));
                }
            }

            if (gaps.Any(gap => gap.Code == "required-control-file-missing"))
            {
                return new Mo2SnapshotCaptureResult(
                    SnapshotCaptureState.Failed,
                    null,
                    Freeze(gaps));
            }

            SkipPolicy skipPolicy =
                ValidateInstanceConfiguration(paths, controls["instance-ini"].Bytes);
            gaps.Add(new SnapshotGap(
                "mo2-game-plugin-inventory-unqualified",
                "mo2-game-plugin-and-secondary-data",
                "The exact Skyrim game-plugin automatic/foreign/secondary-root inventory has not completed EVAL-0051 qualification."));
            string savedProfileHint = ReadSavedProfileHint(controls["instance-ini"].Bytes);

            betweenStructuralCaptures?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            StructuralCapture second = CaptureStructure(
                paths,
                admittedMappings,
                [],
                cancellationToken);
            ExecutableAdmission finalMo2Admission =
                manifests.AdmitMo2(paths.Mo2Executable);
            ExecutableAdmission finalRuntimeAdmission = manifests.AdmitSkyrim(
                paths.SkyrimExecutable,
                request.RuntimeTarget);
            if (!string.Equals(
                    first.Fingerprint,
                    second.Fingerprint,
                    StringComparison.OrdinalIgnoreCase)
                || !ControlsRemainCurrent(controls)
                || !SameAdmission(mo2Admission, finalMo2Admission)
                || !SameAdmission(runtimeAdmission, finalRuntimeAdmission)
                || processProbe.IsRunning(paths.Mo2Executable))
            {
                gaps.Add(new SnapshotGap(
                    "changed-during-capture",
                    "snapshot",
                    "A structural, executable, quiescence, or content-sealed dependency changed during capture."));
                return new Mo2SnapshotCaptureResult(
                    SnapshotCaptureState.ChangedDuringCapture,
                    null,
                    Freeze(gaps));
            }

            CaptureModel model = BuildModel(
                paths,
                request,
                first,
                controls,
                admittedMappings,
                skipPolicy,
                gaps);
            SnapshotCaptureState state = gaps.Count == 0
                ? SnapshotCaptureState.Completed
                : SnapshotCaptureState.CompletedWithGaps;
            OpaqueId instanceId = StableId(
                "mo2-instance",
                first.RootIdentities["instance"]);
            OpaqueId profileId = StableId(
                "mo2-profile",
                $"{instanceId.Value}|{first.RootIdentities["profile"]}");
            string captureFingerprint = FingerprintCapture(
                first.Fingerprint,
                controls,
                request,
                mo2Admission,
                runtimeAdmission,
                instanceId,
                profileId);
            Sha256Fingerprint structuralFingerprint = new(captureFingerprint);
            OpaqueId snapshotId = new($"snapshot-{captureFingerprint[..24].ToLowerInvariant()}");
            IReadOnlyList<SnapshotPopulationAssurance> assurance =
            Freeze<SnapshotPopulationAssurance>(
            [
                new(
                    "mo2-control-files",
                    SnapshotAssuranceState.SelectivelyContentSealed,
                    controls.Count,
                    controls.Count,
                    []),
                new(
                    "loose-provider-structure",
                    SnapshotAssuranceState.Structural,
                    first.Entries.Count,
                    first.Entries.Count,
                    gaps
                        .Where(gap => gap.Population is "loose-providers" or "filesystem")
                        .Select(gap => gap.Code)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()),
                new(
                    "archive-members",
                    SnapshotAssuranceState.Unsupported,
                    0,
                    0,
                    ["archive-member-semantics-not-qualified"]),
            ]);
            InstallationSnapshotContract contract = new(
                snapshotId,
                new ContractVersion(2, 0, 0),
                instanceId,
                profileId,
                structuralFingerprint,
                assurance,
                Freeze(model.Entities.Select(entity => entity.EntityId)),
                new UtcTimestamp(DateTimeOffset.UtcNow));
            Mo2InstallationSnapshot snapshot = new(
                contract,
                SupportedExecutableManifests.AdapterId,
                paths.InstanceRoot,
                paths.ProfileRoot,
                savedProfileHint,
                mo2Admission,
                runtimeAdmission,
                Freeze(model.Mods),
                Freeze(model.Plugins),
                Freeze(model.Entities),
                Freeze(model.ProviderChains),
                Freeze(model.PhysicalEntries),
                Freeze(model.MissingListedMods),
                Freeze(gaps),
                ArchiveMemberPopulationSupported: false,
                Mo2OrUsvfsLaunched: false);
            return new Mo2SnapshotCaptureResult(state, snapshot, Freeze(gaps));
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or OperationCanceledException
                or Win32Exception)
        {
            gaps.Add(new SnapshotGap(
                "capture-input-failure",
                "snapshot",
                $"{exception.GetType().Name}: {exception.Message}"));
            return new Mo2SnapshotCaptureResult(
                SnapshotCaptureState.Failed,
                null,
                Freeze(gaps));
        }
    }

    private static ValidatedPaths ValidatePaths(Mo2SnapshotCaptureRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedProfileName)
            || request.SelectedProfileName.IndexOfAny(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
            || request.SelectedProfileName is "." or "..")
        {
            throw new ArgumentException(
                "An explicit profile directory name is required.",
                nameof(request));
        }

        string mo2Executable = ExistingFile(request.Mo2ExecutablePath);
        string instanceRoot = ExistingDirectory(request.InstanceRoot);
        string instanceIni = ExistingFile(request.InstanceIniPath);
        string profilesRoot = ExistingDirectory(request.ProfilesRoot);
        string modsRoot = ExistingDirectory(request.ModsRoot);
        string overwriteRoot = ExistingDirectory(request.OverwriteRoot);
        string gameDataRoot = ExistingDirectory(request.GameDataRoot);
        string skyrimExecutable = ExistingFile(request.SkyrimExecutablePath);

        RequireWithin(instanceRoot, instanceIni, "instance INI");
        string[] profileMatches = Directory
            .EnumerateDirectories(profilesRoot)
            .Where(path => string.Equals(
                Path.GetFileName(path),
                request.SelectedProfileName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (profileMatches.Length != 1)
        {
            throw new InvalidDataException(
                "The explicit profile must resolve to exactly one immediate profile directory.");
        }

        string profileRoot = ExistingDirectory(profileMatches[0]);
        RequireWithin(profilesRoot, profileRoot, "selected profile");
        foreach (string path in new string[]
                 {
                     mo2Executable,
                     instanceRoot,
                     instanceIni,
                     profilesRoot,
                     profileRoot,
                     modsRoot,
                     overwriteRoot,
                     gameDataRoot,
                     skyrimExecutable,
                 })
        {
            RejectReparsePoint(path);
        }

        return new ValidatedPaths(
            mo2Executable,
            instanceRoot,
            instanceIni,
            profilesRoot,
            profileRoot,
            modsRoot,
            overwriteRoot,
            gameDataRoot,
            skyrimExecutable);
    }

    private static StructuralCapture CaptureStructure(
        ValidatedPaths paths,
        IReadOnlyList<AdmittedMapping> admittedMappings,
        ICollection<SnapshotGap> gaps,
        CancellationToken cancellationToken)
    {
        List<StructuralEntry> entries = [];
        WindowsObjectIdentity instanceIdentity =
            WindowsReadOnlyObjectIdentity.Open(paths.InstanceRoot, directory: true);
        ValidateOpenedPath(instanceIdentity, paths.InstanceRoot);
        Dictionary<string, string> rootIdentities = new(StringComparer.Ordinal)
        {
            ["instance"] = instanceIdentity.CanonicalValue,
        };
        CaptureRoot("profile", paths.ProfileRoot, entries);
        CaptureRoot("mods", paths.ModsRoot, entries);
        CaptureRoot("overwrite", paths.OverwriteRoot, entries);
        CaptureRoot("game-data", paths.GameDataRoot, entries);
        foreach (AdmittedMapping mapping in admittedMappings)
        {
            CaptureRoot($"mapping:{mapping.Mapping.MappingId}", mapping.SourceRoot, entries);
        }

        string canonical = string.Join(
            '\n',
            entries
                .OrderBy(entry => entry.Root, StringComparer.Ordinal)
                .ThenBy(entry => entry.RelativePath, PathComparer)
                .ThenBy(entry => entry.RelativePath, StringComparer.Ordinal)
                .Select(entry => FormattableString.Invariant(
                    $"{entry.Root}|{entry.RelativePath}|{entry.IsDirectory}|{entry.Length}|{entry.LastWriteUtcTicks}|{entry.Attributes}|{entry.ObjectIdentity}")));
        string roots = string.Join(
            '\n',
            rootIdentities
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}|{pair.Value}"));
        return new StructuralCapture(HashUtf8($"{roots}\n{canonical}"), entries, rootIdentities);

        void CaptureRoot(
            string rootName,
            string root,
            List<StructuralEntry> destination)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WindowsObjectIdentity rootIdentity =
                WindowsReadOnlyObjectIdentity.Open(root, directory: true);
            ValidateOpenedPath(rootIdentity, root);
            if ((rootIdentity.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException(
                    $"Reparse points are not qualified capture roots: {root}");
            }

            rootIdentities[rootName] = rootIdentity.CanonicalValue;
            Stack<string> directories = new();
            directories.Push(root);
            while (directories.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string directory = directories.Pop();
                foreach (string child in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    FileAttributes attributes = File.GetAttributes(child);
                    bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                    string relative = NormalizeRelativePath(Path.GetRelativePath(root, child));
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        destination.Add(new StructuralEntry(
                            rootName,
                            relative,
                            isDirectory,
                            0,
                            0,
                            attributes,
                            "reparse-point"));
                        gaps.Add(new SnapshotGap(
                            "reparse-point-unsupported",
                            "filesystem",
                            $"Reparse point is outside the qualified capture surface: {child}"));
                        continue;
                    }

                    FileSystemInfo info = isDirectory
                        ? new DirectoryInfo(child)
                        : new FileInfo(child);
                    string? objectIdentity = rootName == "mods"
                                             && isDirectory
                                             && !relative.Contains('/')
                        ? ReadValidatedObjectIdentity(child, directory: true)
                        : null;
                    destination.Add(new StructuralEntry(
                        rootName,
                        relative,
                        isDirectory,
                        isDirectory ? 0 : ((FileInfo)info).Length,
                        info.LastWriteTimeUtc.Ticks,
                        attributes,
                        objectIdentity));
                    if (destination.Count > MaximumEntries)
                    {
                        throw new InvalidDataException(
                            $"Snapshot contains more than {MaximumEntries} structural entries.");
                    }

                    if (isDirectory)
                    {
                        directories.Push(child);
                    }
                }
            }
        }
    }

    private static string ReadValidatedObjectIdentity(string path, bool directory)
    {
        WindowsObjectIdentity identity =
            WindowsReadOnlyObjectIdentity.Open(path, directory);
        ValidateOpenedPath(identity, path);
        return identity.CanonicalValue;
    }

    private static void ValidateOpenedPath(WindowsObjectIdentity identity, string expectedPath)
    {
        if ((identity.Attributes & FileAttributes.ReparsePoint) != 0
            || !PathComparer.Equals(identity.FinalPath, Path.GetFullPath(expectedPath)))
        {
            throw new InvalidDataException(
                "A capture object changed identity, resolved through a reparse point, or escaped its declared path.");
        }
    }

    private static Dictionary<string, ControlFile> ReadControls(ValidatedPaths paths)
    {
        Dictionary<string, ControlFile> controls = new(StringComparer.Ordinal)
        {
            ["instance-ini"] = ReadControl(paths.InstanceIni),
            ["modlist"] = ReadOptionalControl(Path.Combine(paths.ProfileRoot, "modlist.txt")),
            ["plugins"] = ReadOptionalControl(Path.Combine(paths.ProfileRoot, "plugins.txt")),
            ["loadorder"] = ReadOptionalControl(Path.Combine(paths.ProfileRoot, "loadorder.txt")),
            ["archives"] = ReadOptionalControl(Path.Combine(paths.ProfileRoot, "archives.txt")),
        };
        return controls;
    }

    private static void AddMetaControls(
        ValidatedPaths paths,
        StructuralCapture structure,
        IDictionary<string, ControlFile> controls)
    {
        foreach ((string name, string root) in DiscoverModDirectories(paths, structure))
        {
            string path = Path.Combine(root, "meta.ini");
            string relative = $"{NormalizeRelativePath(name)}/meta.ini";
            if (structure.Entries.Any(entry =>
                    entry.Root == "mods"
                    && !entry.IsDirectory
                    && string.Equals(
                        entry.RelativePath,
                        relative,
                        StringComparison.OrdinalIgnoreCase)))
            {
                controls.Add($"mod-meta:{name}", ReadControl(path));
            }
        }
    }

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
                index,
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
        IReadOnlyList<PluginState> plugins = BuildPlugins(
            controls["plugins"].Bytes,
            controls["loadorder"].Bytes,
            chains,
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

    private static IReadOnlyList<PluginState> BuildPlugins(
        byte[] pluginsBytes,
        byte[] loadOrderBytes,
        IReadOnlyList<LooseProviderChain> chains,
        ICollection<SnapshotGap> gaps)
    {
        IReadOnlyList<PluginLine> pluginLines = ParsePlugins(pluginsBytes, gaps);
        IReadOnlyList<string> loadOrder = ParseSimpleList(loadOrderBytes, "loadorder", gaps);
        Dictionary<string, int> order = new(PathComparer);
        for (int index = 0; index < loadOrder.Count; index++)
        {
            if (!order.TryAdd(loadOrder[index], index))
            {
                gaps.Add(new SnapshotGap(
                    "duplicate-loadorder-entry",
                    "plugins",
                    $"Duplicate loadorder entry: {loadOrder[index]}"));
            }
        }

        Dictionary<string, LooseProviderChain> pluginProviders = new(PathComparer);
        HashSet<string> ambiguousProviders = new(PathComparer);
        foreach (LooseProviderChain chain in chains.Where(
                     value => IsPluginFile(value.NormalizedRelativePath)
                              && !value.NormalizedRelativePath.Contains('/')))
        {
            string name = Path.GetFileName(chain.NormalizedRelativePath);
            if (!pluginProviders.TryAdd(name, chain))
            {
                ambiguousProviders.Add(name);
                gaps.Add(new SnapshotGap(
                    "ambiguous-plugin-provider",
                    "plugins",
                    $"Multiple Windows-equivalent plugin paths were reconstructed for {name}."));
            }
        }

        List<PluginState> result = [];
        HashSet<string> seen = new(PathComparer);
        foreach (PluginLine plugin in pluginLines)
        {
            if (!seen.Add(plugin.Name))
            {
                gaps.Add(new SnapshotGap(
                    "duplicate-plugin-entry",
                    "plugins",
                    $"Duplicate plugin entry: {plugin.Name}"));
                continue;
            }

            pluginProviders.TryGetValue(plugin.Name, out LooseProviderChain? chain);
            if (ambiguousProviders.Contains(plugin.Name))
            {
                chain = null;
            }

            order.TryGetValue(plugin.Name, out int loadPosition);
            bool hasOrder = order.ContainsKey(plugin.Name);
            string correlation = chain is null
                ? ambiguousProviders.Contains(plugin.Name)
                    ? "ambiguous-winning-provider"
                    : "missing-winning-provider"
                : hasOrder
                    ? "correlated"
                    : "provider-without-loadorder-entry";
            if (correlation != "correlated")
            {
                gaps.Add(new SnapshotGap(
                    "plugin-correlation-gap",
                    "plugins",
                    $"{plugin.Name}: {correlation}"));
            }

            result.Add(new PluginState(
                plugin.Name,
                plugin.Enabled,
                hasOrder ? loadPosition : null,
                chain?.Winner.LocalInstalledEntityId,
                correlation));
        }

        foreach (string loadName in loadOrder.Where(name => !seen.Contains(name)))
        {
            gaps.Add(new SnapshotGap(
                "loadorder-plugin-not-listed",
                "plugins",
                $"Loadorder entry is absent from plugins.txt: {loadName}"));
        }

        return result;
    }

    private static IReadOnlyList<ModListLine> ParseModList(
        byte[] bytes,
        ICollection<SnapshotGap> gaps)
    {
        List<ModListLine> values = [];
        HashSet<string> seen = new(PathComparer);
        foreach (string rawLine in DecodeLines(bytes))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            ModEnablementState enablement = line[0] == '-'
                ? ModEnablementState.Disabled
                : ModEnablementState.Enabled;
            string name = line[0] is '+' or '-' or '*'
                ? line[1..].Trim()
                : line;
            if (name.Length == 0)
            {
                gaps.Add(new SnapshotGap(
                    "malformed-modlist-line",
                    "mods",
                    "A modlist entry has no mod name."));
                continue;
            }

            if (!seen.Add(name))
            {
                gaps.Add(new SnapshotGap(
                    "duplicate-modlist-entry",
                    "mods",
                    $"Duplicate Windows-equivalent modlist entry: {name}"));
                continue;
            }

            values.Add(new ModListLine(name, enablement, Listed: true));
        }

        return values;
    }

    private static IReadOnlyList<PluginLine> ParsePlugins(
        byte[] bytes,
        ICollection<SnapshotGap> gaps)
    {
        List<PluginLine> result = [];
        foreach (string rawLine in DecodeLines(bytes))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            bool enabled = line[0] == '*';
            string name = enabled ? line[1..].Trim() : line;
            if (!IsPluginFile(name) || name.Contains(Path.DirectorySeparatorChar))
            {
                gaps.Add(new SnapshotGap(
                    "malformed-plugin-entry",
                    "plugins",
                    $"Unsupported plugins.txt entry: {line}"));
                continue;
            }

            result.Add(new PluginLine(name, enabled));
        }

        return result;
    }

    private static IReadOnlyList<string> ParseSimpleList(
        byte[] bytes,
        string population,
        ICollection<SnapshotGap> gaps)
    {
        List<string> result = [];
        foreach (string rawLine in DecodeLines(bytes))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (!IsPluginFile(line) || line.Contains(Path.DirectorySeparatorChar))
            {
                gaps.Add(new SnapshotGap(
                    $"malformed-{population}-entry",
                    "plugins",
                    $"Unsupported {population} entry: {line}"));
                continue;
            }

            result.Add(line);
        }

        return result;
    }

    private static IReadOnlyList<LocalSourceHint> ReadSourceHints(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return [];
        }

        Dictionary<string, string> values = ParseIni(bytes);
        string[] admittedKeys =
        [
            "general/modid",
            "general/version",
            "general/newestversion",
            "general/installationfile",
            "general/repository",
        ];
        return admittedKeys
            .Where(values.ContainsKey)
            .Select(key => new LocalSourceHint(
                key,
                values[key],
                "mutable-mo2-meta-ini-hint"))
            .ToArray();
    }

    private static string ReadSavedProfileHint(byte[] bytes)
    {
        Dictionary<string, string> values = ParseIni(bytes);
        return values.TryGetValue("general/selected_profile", out string? value)
            ? DecodeQtByteArray(value)
            : string.Empty;
    }

    private static SkipPolicy ValidateInstanceConfiguration(ValidatedPaths paths, byte[] bytes)
    {
        Dictionary<string, string> values = ParseIni(bytes);
        string gameName = RequiredIniValue(values, "general/gamename");
        if (!string.Equals(
                gameName,
                "Skyrim Special Edition",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The MO2 instance gameName is not the supported Skyrim Special Edition target.");
        }

        string configuredGame = NormalizeConfiguredPath(
            RequiredIniValue(values, "general/gamepath"));
        string expectedGame = Path.GetDirectoryName(paths.SkyrimExecutable)
            ?? throw new InvalidDataException("The Skyrim executable has no parent directory.");
        if (!PathComparer.Equals(configuredGame, Path.GetFullPath(expectedGame))
            || !PathComparer.Equals(
                paths.GameDataRoot,
                Path.Combine(configuredGame, "Data")))
        {
            throw new InvalidDataException(
                "The declared Skyrim executable/Data roots do not match the canonical MO2 gamePath.");
        }

        string baseDirectory = NormalizeConfiguredPath(
            RequiredIniValue(values, "settings/base_directory"));
        if (!PathComparer.Equals(paths.ProfilesRoot, Path.Combine(baseDirectory, "profiles"))
            || !PathComparer.Equals(paths.ModsRoot, Path.Combine(baseDirectory, "mods"))
            || !PathComparer.Equals(paths.OverwriteRoot, Path.Combine(baseDirectory, "overwrite")))
        {
            throw new InvalidDataException(
                "Only the qualified MO2 2.5.2 default base-directory layout is supported.");
        }

        IReadOnlyList<string> suffixes = ReadQtStringList(
            values,
            "settings/skip_file_suffixes",
            [".mohidden"]);
        IReadOnlyList<string> directories = ReadQtStringList(
            values,
            "settings/skip_directories",
            [".git"]);
        ValidateSkipValues(suffixes, "file suffix");
        ValidateSkipValues(directories, "directory");
        return new SkipPolicy(
            suffixes.ToArray(),
            new HashSet<string>(directories, PathComparer));
    }

    private static IReadOnlyList<string> ReadQtStringList(
        IReadOnlyDictionary<string, string> values,
        string key,
        IReadOnlyList<string> defaults)
    {
        if (!values.TryGetValue(key, out string? raw) || string.IsNullOrWhiteSpace(raw))
        {
            return defaults;
        }

        List<string> result = [];
        StringBuilder current = new();
        bool escaped = false;
        foreach (char character in raw)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
            }
            else if (character == '\\')
            {
                escaped = true;
            }
            else if (character == ',')
            {
                AddValue();
            }
            else
            {
                current.Append(character);
            }
        }

        if (escaped)
        {
            current.Append('\\');
        }

        AddValue();
        return result;

        void AddValue()
        {
            string value = DecodeQtByteArray(current.ToString().Trim());
            current.Clear();
            if (value.Length > 0)
            {
                result.Add(value);
            }
        }
    }

    private static void ValidateSkipValues(
        IReadOnlyList<string> values,
        string label)
    {
        if (values.Count == 0
            || values.Count > 32
            || values.Any(value =>
                value.Length > 128
                || value.Contains('\0', StringComparison.Ordinal)
                || value.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || value.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"The configured MO2 skip {label} list is outside the qualified grammar.");
        }
    }

    private static string RequiredIniValue(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        if (!values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Required canonical MO2 setting is missing: {key}");
        }

        return value;
    }

    private static string NormalizeConfiguredPath(string value)
    {
        string decoded = DecodeQtByteArray(value).Replace('/', Path.DirectorySeparatorChar);
        return NormalizeAbsolutePath(decoded);
    }

    private static Dictionary<string, string> ParseIni(byte[] bytes)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        string section = string.Empty;
        foreach (string rawLine in DecodeLines(bytes))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] is ';' or '#')
            {
                continue;
            }

            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1].Trim();
                continue;
            }

            int separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();
            result[$"{section}/{key}"] = value;
        }

        return result;
    }

    private static string DecodeQtByteArray(string value)
    {
        if (!value.StartsWith("@ByteArray(", StringComparison.Ordinal)
            || !value.EndsWith(')'))
        {
            return value;
        }

        string encoded = value[11..^1];
        List<byte> bytes = [];
        for (int index = 0; index < encoded.Length; index++)
        {
            char character = encoded[index];
            if (character != '\\' || index + 1 >= encoded.Length)
            {
                bytes.Add((byte)character);
                continue;
            }

            char escaped = encoded[++index];
            if (escaped == 'x'
                && index + 2 < encoded.Length
                && byte.TryParse(
                    encoded.AsSpan(index + 1, 2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out byte hex))
            {
                bytes.Add(hex);
                index += 2;
            }
            else
            {
                bytes.Add(escaped switch
                {
                    'n' => (byte)'\n',
                    'r' => (byte)'\r',
                    't' => (byte)'\t',
                    _ => (byte)escaped,
                });
            }
        }

        return Encoding.UTF8.GetString(bytes.ToArray());
    }

    private static IEnumerable<string> DecodeLines(byte[] bytes)
    {
        string text = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(bytes);
        text = text.TrimStart('\uFEFF');
        using StringReader reader = new(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }

    private static ControlFile ReadControl(string path)
    {
        using FileStream stream = SupportedExecutableManifests.OpenStableRead(path);
        if (stream.Length > MaximumControlBytes)
        {
            throw new InvalidDataException(
                $"Control file exceeds {MaximumControlBytes} bytes: {path}");
        }

        byte[] bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return new ControlFile(
            path,
            bytes,
            Convert.ToHexString(SHA256.HashData(bytes)),
            Exists: true);
    }

    private static ControlFile ReadOptionalControl(string path)
    {
        return File.Exists(path)
            ? ReadControl(path)
            : new ControlFile(
                path,
                [],
                Convert.ToHexString(SHA256.HashData([])),
                Exists: false);
    }

    private static bool ControlsRemainCurrent(
        IReadOnlyDictionary<string, ControlFile> controls)
    {
        foreach (ControlFile control in controls.Values)
        {
            if (!File.Exists(control.Path))
            {
                if (!control.Exists)
                {
                    continue;
                }

                return false;
            }

            if (!control.Exists)
            {
                return false;
            }

            ControlFile current = ReadControl(control.Path);
            if (!string.Equals(current.Sha256, control.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsManagementContent(string relativePath)
    {
        string normalized = NormalizeRelativePath(relativePath);
        return string.Equals(normalized, "meta.ini", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "readme.txt", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("fomod/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPluginFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".esp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".esm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".esl", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExistingDirectory(string path)
    {
        string full = NormalizeAbsolutePath(path);
        return Directory.Exists(full)
            ? full
            : throw new DirectoryNotFoundException($"Required directory is missing: {full}");
    }

    private static string ExistingFile(string path)
    {
        string full = NormalizeAbsolutePath(path);
        return File.Exists(full)
            ? full
            : throw new FileNotFoundException("Required file is missing.", full);
    }

    private static string NormalizeAbsolutePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("A fully qualified path is required.", nameof(path));
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static void RequireWithin(string root, string candidate, string label)
    {
        string relative = Path.GetRelativePath(root, candidate);
        if (relative == ".."
            || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"{label} is outside its declared root.");
        }
    }

    private static void RejectReparsePoint(string path)
    {
        string? current = path;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(current) || Directory.Exists(current))
            {
                FileAttributes attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidDataException(
                        $"Reparse points are not qualified capture roots: {current}");
                }
            }

            current = Path.GetDirectoryName(current);
        }
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .TrimStart('/');
    }

    private static string NormalizeVirtualPrefix(string prefix)
    {
        if (Path.IsPathFullyQualified(prefix)
            || prefix.Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries)
                .Any(part => part is "." or ".."))
        {
            throw new InvalidDataException("A mapping virtual prefix must be a safe relative path.");
        }

        return NormalizeRelativePath(prefix).TrimEnd('/');
    }

    private static string NormalizeSha256(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length != 64
            || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException("Mapper identities must be exact SHA-256 values.");
        }

        return value.ToUpperInvariant();
    }

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

    private static string FingerprintCapture(
        string structuralFingerprint,
        IReadOnlyDictionary<string, ControlFile> controls,
        Mo2SnapshotCaptureRequest request,
        ExecutableAdmission mo2Admission,
        ExecutableAdmission runtimeAdmission,
        OpaqueId instanceId,
        OpaqueId profileId)
    {
        StringBuilder canonical = new();
        canonical.AppendLine(SupportedExecutableManifests.AdapterId)
            .AppendLine(structuralFingerprint)
            .AppendLine(instanceId.Value)
            .AppendLine(profileId.Value)
            .AppendLine(request.SelectedProfileName)
            .Append(request.RuntimeTarget.Platform)
            .Append('|')
            .Append(request.RuntimeTarget.DistributionChannel)
            .Append('|')
            .AppendLine(request.RuntimeTarget.ApplicationId)
            .AppendLine(mo2Admission.ObservedIdentity!.Sha256)
            .AppendLine(runtimeAdmission.ObservedIdentity!.Sha256);
        foreach ((string name, ControlFile control) in controls.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            canonical.Append(name)
                .Append('|')
                .Append(control.Exists)
                .Append('|')
                .AppendLine(control.Sha256);
        }

        foreach (QualifiedMapping mapping in request.QualifiedMappings.OrderBy(
                     value => value.MappingId,
                     StringComparer.Ordinal))
        {
            canonical.Append(mapping.MappingId)
                .Append('|')
                .Append(Path.GetFullPath(mapping.SourceRoot))
                .Append('|')
                .Append(mapping.VirtualPrefix)
                .Append('|')
                .AppendLine(mapping.MapperSha256);
        }

        return HashUtf8(canonical.ToString());
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

    private sealed record ControlFile(string Path, byte[] Bytes, string Sha256, bool Exists);

    private sealed record ModListLine(
        string Name,
        ModEnablementState Enablement,
        bool Listed);

    private sealed record PluginLine(string Name, bool Enabled);

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

internal sealed class WindowsMo2ProcessProbe : IMo2ProcessProbe
{
    public bool IsRunning(string exactExecutablePath)
    {
        string expected = Path.GetFullPath(exactExecutablePath);
        WindowsObjectIdentity expectedIdentity =
            WindowsReadOnlyObjectIdentity.Open(expected, directory: false);
        string processName = Path.GetFileNameWithoutExtension(expected);
        foreach (Process process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    string? path = process.MainModule?.FileName;
                    if (path is null
                        || WindowsReadOnlyObjectIdentity.Open(path, directory: false)
                            .CanonicalValue == expectedIdentity.CanonicalValue)
                    {
                        return true;
                    }
                }
                catch (Exception exception) when (
                    exception is Win32Exception
                        or InvalidOperationException
                        or NotSupportedException)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
