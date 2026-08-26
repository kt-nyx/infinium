using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

#pragma warning disable CA1859 // Contract-shaped collection abstractions keep capture seams narrow.

namespace Infinium.Mo2;


public sealed partial class Mo2SnapshotCapture
{
    private static IReadOnlyList<PluginState> BuildPlugins(
        byte[] pluginsBytes,
        byte[] loadOrderBytes,
        IReadOnlyList<LooseProviderChain> chains,
        GamePluginInventory gamePluginInventory,
        ICollection<SnapshotGap> gaps)
    {
        IReadOnlyList<PluginLine> pluginLines = ParsePlugins(pluginsBytes, gaps);
        IReadOnlyList<string> loadOrder = ParseSimpleList(loadOrderBytes, "loadorder", gaps);
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

        Dictionary<string, PluginLine> listedPlugins = new(PathComparer);
        foreach (PluginLine plugin in pluginLines)
        {
            if (!listedPlugins.TryAdd(plugin.Name, plugin))
            {
                gaps.Add(new SnapshotGap(
                    "duplicate-plugin-entry",
                    "plugins",
                    $"Duplicate plugin entry: {plugin.Name}"));
            }
        }

        List<string> effectiveOrder = [];
        HashSet<string> ordered = new(PathComparer);
        if (gamePluginInventory.ForceEnableCoreFiles)
        {
            foreach (string primary in gamePluginInventory.PrimaryPlugins.Where(
                         pluginProviders.ContainsKey))
            {
                if (ordered.Add(primary))
                {
                    effectiveOrder.Add(primary);
                }
            }
        }

        HashSet<string> loadOrderSeen = new(PathComparer);
        foreach (string name in loadOrder)
        {
            if (!loadOrderSeen.Add(name))
            {
                gaps.Add(new SnapshotGap(
                    "duplicate-loadorder-entry",
                    "plugins",
                    $"Duplicate loadorder entry: {name}"));
                continue;
            }

            if (ordered.Add(name))
            {
                effectiveOrder.Add(name);
            }
        }

        Dictionary<string, int> order = effectiveOrder
            .Select((name, index) => (name, index))
            .ToDictionary(pair => pair.name, pair => pair.index, PathComparer);
        HashSet<string> names = new(listedPlugins.Keys, PathComparer);
        names.UnionWith(pluginProviders.Keys);
        names.UnionWith(loadOrder);
        List<PluginState> result = [];
        foreach (string name in names
                     .OrderBy(value => order.TryGetValue(value, out int position)
                         ? position
                         : int.MaxValue)
                     .ThenBy(value => value, PathComparer)
                     .ThenBy(value => value, StringComparer.Ordinal))
        {
            listedPlugins.TryGetValue(name, out PluginLine? listed);
            pluginProviders.TryGetValue(name, out LooseProviderChain? chain);
            bool ambiguous = ambiguousProviders.Contains(name);
            if (ambiguous)
            {
                chain = null;
            }

            bool primary = gamePluginInventory.PrimaryPluginSet.Contains(name);
            bool creationClub = gamePluginInventory.CreationClubPluginSet.Contains(name);
            PluginClassification classification = primary
                ? creationClub
                    ? PluginClassification.CreationClubGame
                    : PluginClassification.PrimaryGame
                : chain?.Winner.Kind == LooseProviderKind.PhysicalData
                    ? PluginClassification.ForeignGameData
                    : PluginClassification.Regular;
            PluginEnablementState enablement =
                primary && gamePluginInventory.ForceEnableCoreFiles && chain is not null
                    ? PluginEnablementState.ForcedEnabledByGamePlugin
                    : listed is not null
                        ? listed.Enabled
                            ? PluginEnablementState.EnabledByProfile
                            : PluginEnablementState.DisabledByProfile
                        : PluginEnablementState.Unresolved;
            if (enablement == PluginEnablementState.Unresolved && chain is not null)
            {
                gaps.Add(new SnapshotGap(
                    "unlisted-plugin-enablement-unresolved",
                    "plugins",
                    $"Available plugin is absent from plugins.txt: {name}"));
            }

            bool hasOrder = order.TryGetValue(name, out int loadPosition);
            string correlation = chain is null
                ? ambiguous
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
                    $"{name}: {correlation}"));
            }

            result.Add(new PluginState(
                name,
                enablement,
                classification,
                hasOrder ? loadPosition : null,
                chain?.Winner.LocalInstalledEntityId,
                correlation));
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

    private static GamePluginInventory ReadGamePluginInventory(
        byte[] instanceIniBytes,
        byte[] creationClubBytes,
        ICollection<SnapshotGap> gaps)
    {
        Dictionary<string, string> values = ParseIni(instanceIniBytes);
        bool forceEnableCoreFiles = true;
        if (values.TryGetValue(
                "settings/force_enable_core_files",
                out string? configured))
        {
            if (configured.Equals("true", StringComparison.OrdinalIgnoreCase)
                || configured.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || configured == "1")
            {
                forceEnableCoreFiles = true;
            }
            else if (configured.Equals("false", StringComparison.OrdinalIgnoreCase)
                     || configured.Equals("no", StringComparison.OrdinalIgnoreCase)
                     || configured == "0")
            {
                forceEnableCoreFiles = false;
            }
            else
            {
                throw new InvalidDataException(
                    "Settings/force_enable_core_files is not a supported Boolean value.");
            }
        }

        string[] basePrimary =
        [
            "Skyrim.esm",
            "Update.esm",
            "Dawnguard.esm",
            "HearthFires.esm",
            "Dragonborn.esm",
        ];
        List<string> creationClub = [];
        HashSet<string> seen = new(basePrimary, PathComparer);
        foreach (string rawLine in DecodeLines(creationClubBytes))
        {
            string name = rawLine.Trim();
            if (name.Length == 0 || name[0] is '#' or ';')
            {
                continue;
            }

            if (!IsPluginFile(name)
                || name.Contains(Path.DirectorySeparatorChar)
                || name.Contains(Path.AltDirectorySeparatorChar))
            {
                gaps.Add(new SnapshotGap(
                    "malformed-skyrim-ccc-entry",
                    "mo2-game-plugin",
                    $"Unsupported Skyrim.ccc entry: {name}"));
                continue;
            }

            if (!seen.Add(name))
            {
                gaps.Add(new SnapshotGap(
                    "duplicate-skyrim-ccc-entry",
                    "mo2-game-plugin",
                    $"Duplicate Windows-equivalent Skyrim.ccc entry: {name}"));
                continue;
            }

            creationClub.Add(name);
        }

        List<string> primary = [.. basePrimary, .. creationClub];
        return new GamePluginInventory(
            forceEnableCoreFiles,
            Freeze(primary),
            new HashSet<string>(primary, PathComparer),
            new HashSet<string>(creationClub, PathComparer));
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
            ? DecodeSavedSelectionValue(value)
            : string.Empty;
    }

    public static string ReadPortableSavedSelection(string installationRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationRoot);
        using FileStream stream = WindowsReadOnlyObjectIdentity.OpenStableRelativeRead(
            installationRoot,
            "ModOrganizer.ini");
        if (stream.Length > 1_048_576)
        {
            throw new InvalidDataException(
                "The canonical MO2 saved-selection file exceeds its bounded input size.");
        }
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return ReadSavedProfileHint(bytes);
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
            string value = DecodeSavedSelectionValue(current.ToString().Trim());
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
        string decoded = DecodeSavedSelectionValue(value).Replace('/', Path.DirectorySeparatorChar);
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

    public static string DecodeSavedSelectionValue(string value)
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

    private static ControlFile ReadControl(string rootPath, string relativePath)
    {
        string normalizedRelative = NormalizeRelativePath(relativePath);
        string path = Path.Combine(
            rootPath,
            normalizedRelative.Replace('/', Path.DirectorySeparatorChar));
        using FileStream stream =
            WindowsReadOnlyObjectIdentity.OpenStableRelativeRead(
                rootPath,
                normalizedRelative);
        string physicalObjectIdentity =
            WindowsReadOnlyObjectIdentity.Read(stream.SafeFileHandle).CanonicalValue;
        if (stream.Length > MaximumControlBytes)
        {
            throw new InvalidDataException(
                $"Control file exceeds {MaximumControlBytes} bytes: {path}");
        }

        byte[] bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return new ControlFile(
            path,
            rootPath,
            normalizedRelative,
            bytes,
            Convert.ToHexString(SHA256.HashData(bytes)),
            Exists: true,
            physicalObjectIdentity);
    }

    private static ControlFile ReadOptionalControl(string rootPath, string relativePath)
    {
        try
        {
            return ReadControl(rootPath, relativePath);
        }
        catch (Win32Exception exception) when (
            exception.NativeErrorCode is 2 or 3)
        {
            string normalizedRelative = NormalizeRelativePath(relativePath);
            return new ControlFile(
                Path.Combine(
                    rootPath,
                    normalizedRelative.Replace('/', Path.DirectorySeparatorChar)),
                rootPath,
                normalizedRelative,
                [],
                Convert.ToHexString(SHA256.HashData([])),
                Exists: false,
                PhysicalObjectIdentity: null);
        }
    }

    private static bool ControlsRemainCurrent(
        IReadOnlyDictionary<string, ControlFile> controls)
    {
        foreach (ControlFile control in controls.Values)
        {
            ControlFile current;
            try
            {
                current = ReadOptionalControl(control.RootPath, control.RelativePath);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or Win32Exception)
            {
                return false;
            }

            if (current.Exists != control.Exists
                || !string.Equals(
                    current.Sha256,
                    control.Sha256,
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    current.PhysicalObjectIdentity,
                    control.PhysicalObjectIdentity,
                    StringComparison.Ordinal))
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

}
