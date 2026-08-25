using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Headers;
using Mutagen.Bethesda.Plugins.Binary.Streams;
using Mutagen.Bethesda.Plugins.Binary.Translations;
using Mutagen.Bethesda.Plugins.Meta;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;

namespace Infinium.Bethesda;

#pragma warning disable CA1859 // Contract-facing collection abstractions are intentional.


public sealed partial class BethesdaSemanticExtractor
{
    private static Dictionary<string, T> WinnerFacts<T>(
        IEnumerable<T> facts,
        IReadOnlyDictionary<string, BethesdaOverrideChain> chains,
        Func<T, BethesdaRecordContribution> contribution)
    {
        return facts
            .Where(fact =>
            {
                BethesdaRecordContribution item = contribution(fact);
                return chains[item.Identity.FormKey].Winner == item;
            })
            .ToDictionary(
                fact => contribution(fact).Identity.FormKey,
                StringComparer.OrdinalIgnoreCase);
    }

    private List<SealedPlugin> SealPlugins(ValidatedInput input, CancellationToken cancellationToken)
    {
        List<SealedPlugin> sealedPlugins = [];
        long totalBytes = 0;
        for (int index = 0; index < input.OrderedPlugins.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AuthorizedPlugin source = input.OrderedPlugins[index];
            FileInfo before = new(source.Path);
            if (!before.Exists || before.Length <= 0)
            {
                throw Input("plugin-missing", source.Path, "An accepted snapshot plugin is missing or empty.");
            }

            totalBytes = checked(totalBytes + before.Length);
            if (before.Length > maximumInputBytes || totalBytes > maximumInputBytes)
            {
                throw Input("input-byte-limit", source.Path, "The Bethesda input byte limit was exceeded.");
            }

            byte[] bytes;
            using (FileStream stream = new(
                       source.Path,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read,
                       64 * 1024,
                       FileOptions.SequentialScan))
            {
                bytes = new byte[checked((int)stream.Length)];
                stream.ReadExactly(bytes);
                try
                {
                    afterPluginRead?.Invoke(source.Path, index);
                }
                catch (IOException)
                {
                    throw Changed(source.Path, "A concurrent plugin replacement was denied during the stable read.");
                }
                if (stream.Length != bytes.LongLength || stream.Position != bytes.LongLength)
                {
                    throw Changed(source.Path, "The plugin changed during the stable read.");
                }
            }

            FileInfo after = new(source.Path);
            if (!after.Exists
                || after.Length != before.Length
                || after.LastWriteTimeUtc.Ticks != before.LastWriteTimeUtc.Ticks)
            {
                throw Changed(source.Path, "The plugin identity changed during the stable read.");
            }

            string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
            ModKey modKey;
            try
            {
                modKey = ModKey.FromFileName(source.Plugin.Name);
            }
            catch (Exception exception)
            {
                throw Input("plugin-name-invalid", source.Plugin.Name, Bounded(exception.Message));
            }

            (BethesdaMasterStyle style, string[] masters) = HeaderInfo(bytes, source.Plugin.Name);
            BethesdaPluginReceipt receipt = new(
                source.Plugin.Name,
                source.Plugin.LoadOrder!.Value,
                source.Winner.LocalInstalledEntityId,
                source.Path,
                bytes.LongLength,
                new Sha256Fingerprint(sha),
                style,
                masters);
            sealedPlugins.Add(new SealedPlugin(receipt, modKey, bytes, style));
        }

        return sealedPlugins;
    }

    private static ValidatedInput ValidateInput(Mo2SnapshotCaptureResult result)
    {
        if (result.State is not (SnapshotCaptureState.Completed or SnapshotCaptureState.CompletedWithGaps)
            || result.Snapshot is null
            || result.Snapshot.Contract.SchemaVersion != new ContractVersion(3, 0, 0)
            || result.Snapshot.Mo2OrUsvfsLaunched
            || result.Snapshot.Mo2Admission.State != AdmissionState.Accepted
            || result.Snapshot.SkyrimGamePluginAdmission.State != AdmissionState.Accepted
            || result.Snapshot.RuntimeAdmission.State != AdmissionState.Accepted)
        {
            throw Input("snapshot-not-accepted", "accepted-snapshot", "Bethesda extraction requires an accepted installation snapshot.");
        }

        Mo2InstallationSnapshot snapshot = result.Snapshot;
        PluginState[] plugins = snapshot.Plugins
            .Where(plugin => plugin.Enabled)
            .OrderBy(plugin => plugin.LoadOrder)
            .ToArray();
        if (plugins.Length == 0
            || plugins.Any(plugin => plugin.LoadOrder is null
                || plugin.WinningLocalInstalledEntityId is null
                || !string.Equals(plugin.CorrelationState, "correlated", StringComparison.OrdinalIgnoreCase))
            || plugins.Select(plugin => plugin.LoadOrder).Distinct().Count() != plugins.Length)
        {
            throw Input("plugin-order-invalid", "accepted-snapshot", "The accepted snapshot has an incomplete or ambiguous plugin order.");
        }

        List<AuthorizedPlugin> authorized = [];
        foreach (PluginState plugin in plugins)
        {
            LooseProviderChain[] chains = snapshot.LooseProviderChains
                .Where(chain => string.Equals(chain.NormalizedRelativePath, plugin.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (chains.Length != 1
                || chains[0].Winner.LocalInstalledEntityId != plugin.WinningLocalInstalledEntityId)
            {
                throw Input("plugin-provider-ambiguous", plugin.Name, "The accepted snapshot does not identify exactly one matching winning plugin provider.");
            }

            string path = Path.GetFullPath(chains[0].Winner.PhysicalPath);
            if (!string.Equals(Path.GetFileName(path), plugin.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw Input("plugin-path-mismatch", plugin.Name, "The accepted snapshot winner path does not match the plugin identity.");
            }

            authorized.Add(new AuthorizedPlugin(plugin, chains[0].Winner, path));
        }

        return new ValidatedInput(snapshot, authorized);
    }

    private static BethesdaRecordContribution Contribution(
        IMajorRecordGetter record,
        string signature,
        SealedPlugin source)
    {
        BethesdaRecordIdentity identity = Identity(signature, record.FormKey);
        return new BethesdaRecordContribution(
            $"contribution:{source.Receipt.LoadOrder:D4}:{source.Receipt.PluginName.ToLowerInvariant()}:{identity.FormKey.ToLowerInvariant()}",
            identity,
            source.Receipt.PluginName,
            source.Receipt.LoadOrder,
            record.IsDeleted,
            record.IsCompressed,
            unchecked((uint)record.MajorRecordFlagsRaw));
    }

    private static BethesdaRecordIdentity Identity(string signature, FormKey key)
    {
        string plugin = key.ModKey.FileName.String;
        string canonical = CanonicalFormKey(key);
        return new BethesdaRecordIdentity(
            ParticipantId(key),
            signature,
            canonical,
            plugin,
            key.ID);
    }

    private static string RecordSignature(string mutagenTypeName) => mutagenTypeName switch
    {
        "Npc" or "INpcGetter" => "NPC_",
        "Race" or "IRaceGetter" => "RACE",
        "Cell" or "ICellGetter" => "CELL",
        "PlacedObject" or "IPlacedObjectGetter" => "REFR",
        "Class" or "IClassGetter" => "CLAS",
        "AIPackage" or "IAIPackageGetter" or "Package" or "IPackageGetter" => "PACK",
        "ColorRecord" or "IColorRecordGetter" => "CLFM",
        "HeadPart" or "IHeadPartGetter" => "HDPT",
        "Faction" or "IFactionGetter" => "FACT",
        "Keyword" or "IKeywordGetter" => "KYWD",
        "Location" or "ILocationGetter" => "LCTN",
        "Static" or "IStaticGetter" => "STAT",
        "Weapon" or "IWeaponGetter" => "WEAP",
        _ => mutagenTypeName.ToUpperInvariant(),
    };

    private static string SemanticSignature(IMajorRecordGetter record) => record switch
    {
        INpcGetter => "NPC_",
        IRaceGetter => "RACE",
        IPlacedObjectGetter => "REFR",
        _ => RecordSignature(record.Type.Name),
    };

    private static BethesdaLinkFact Link(
        BethesdaRecordContribution source,
        string field,
        int ordinal,
        FormKey target,
        bool isNull,
        IReadOnlySet<FormKey> allRecords,
        IReadOnlyDictionary<ModKey, BethesdaMasterStyle> masterStyles,
        IReadOnlySet<ModKey> allowedOrigins,
        string? component = null)
    {
        if (isNull)
        {
            return new BethesdaLinkFact(source.Identity.ParticipantId, source.ContributionId, field, component, ordinal, null, BethesdaLinkState.Null, null);
        }

        if (!allowedOrigins.Contains(target.ModKey))
        {
            throw Input("link-master-index-invalid", target.ToString(), "Mutagen resolved a link target outside the plugin's declared master table.");
        }

        if (masterStyles.TryGetValue(target.ModKey, out BethesdaMasterStyle style)
            && style == BethesdaMasterStyle.Light
            && target.ID is < 0x800 or > 0xFFF)
        {
            throw Input("light-link-local-id-invalid", target.ToString(), "A light-origin linked local FormID is outside 0x800..0xFFF.");
        }

        string canonical = CanonicalFormKey(target);
        bool resolved = allRecords.Contains(target);
        return new BethesdaLinkFact(
            source.Identity.ParticipantId,
            source.ContributionId,
            field,
            component,
            ordinal,
            canonical,
            resolved ? BethesdaLinkState.Resolved : BethesdaLinkState.Unresolved,
            resolved ? ParticipantId(target) : null);
    }

    private static string CanonicalFormKey(FormKey key) =>
        FormattableString.Invariant($"{key.ID:X8}:{key.ModKey.FileName.String}");

    private static string ParticipantId(FormKey key) =>
        $"record:{CanonicalFormKey(key).ToLowerInvariant()}";

    private void ValidateMasterStylesAndReferences(
        List<SealedPlugin> plugins,
        List<ISkyrimModDisposableGetter> mods)
    {
        if (plugins.Select(plugin => plugin.ModKey.FileName.String)
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != plugins.Count)
        {
            throw Input("plugin-name-duplicate", "accepted-snapshot", "Plugin identities must be unique case-insensitively.");
        }

        Dictionary<ModKey, BethesdaMasterStyle> styles = plugins.ToDictionary(plugin => plugin.ModKey, plugin => plugin.MasterStyle);
        for (int index = 0; index < plugins.Count; index++)
        {
            SealedPlugin plugin = plugins[index];
            string[] masters = mods[index].ModHeader.MasterReferences
                .Select(reference => reference.Master.FileName.String)
                .ToArray();
            if (mods[index].ModHeader.MasterReferences.Any(reference => reference.FileSize is null))
            {
                throw Input("master-data-pair-missing", plugin.Receipt.PluginName, "Mutagen exposed a master entry without its required DATA value.");
            }
            if (masters.Distinct(StringComparer.OrdinalIgnoreCase).Count() != masters.Length)
            {
                throw Input("master-reference-duplicate", plugin.Receipt.PluginName, "The declared master table contains a duplicate identity.");
            }

            HashSet<string> earlier = plugins.Take(index)
                .Select(candidate => candidate.ModKey.FileName.String)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (masters.Any(master => !earlier.Contains(master)))
            {
                throw Input("master-closure-invalid", plugin.Receipt.PluginName, "Every declared master must be present exactly once earlier in the accepted order.");
            }
            plugins[index] = plugin with { Receipt = plugin.Receipt with { Masters = masters } };
            foreach (IMajorRecordGetter record in mods[index].EnumerateMajorRecords().Where(record => IsSelected(record.FormKey)))
            {
                if (styles.TryGetValue(record.FormKey.ModKey, out BethesdaMasterStyle style)
                    && style == BethesdaMasterStyle.Light
                    && record.FormKey.ID is < 0x800 or > 0xFFF)
                {
                    throw Input("light-local-id-invalid", record.FormKey.ToString(), "A light-origin local FormID is outside 0x800..0xFFF.");
                }
            }
        }
    }

    private static (BethesdaMasterStyle Style, string[] Masters) HeaderInfo(ReadOnlySpan<byte> bytes, string pluginName)
    {
        if (bytes.Length < GameConstants.SkyrimSE.ModHeaderLength)
        {
            throw Input("tes4-header-missing", pluginName, "The plugin does not begin with a TES4 header.");
        }

        ModHeaderFrame header;
        try
        {
            header = new ModHeaderFrame(GameConstants.SkyrimSE, new ReadOnlyMemorySlice<byte>(bytes.ToArray()));
        }
        catch (Exception exception)
        {
            throw Input("tes4-header-missing", pluginName, Bounded(exception.Message));
        }

        if (header.RecordType.Type != "TES4")
        {
            throw Input("tes4-header-missing", pluginName, "The plugin does not begin with a TES4 header.");
        }

        bool light = header.MasterStyle == MasterStyle.Small;
        if (pluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase) && !light)
        {
            throw Input("esl-header-flag-missing", pluginName, "A native .esl must carry the TES4 light flag.");
        }

        SubrecordPinFrame[] subrecords = header.ToArray();
        if (subrecords.Count(record => record.RecordType.Type == "MAST")
            != subrecords.Count(record => record.RecordType.Type == "DATA"))
        {
            throw Input("master-data-pair-missing", pluginName, "Mutagen exposed a master entry without its required DATA value.");
        }

        string[] masters = subrecords
            .Where(record => record.RecordType.Type == "MAST")
            .Select(record => Encoding.UTF8.GetString(record.Content.Span).TrimEnd('\0'))
            .ToArray();
        return (light ? BethesdaMasterStyle.Light : BethesdaMasterStyle.Full, masters);
    }

}
