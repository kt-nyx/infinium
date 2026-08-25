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
    private PluginStructuralObservation ObservePluginStructure(
        SealedPlugin plugin,
        IReadOnlyDictionary<ModKey, BethesdaMasterStyle> masterStyles,
        ref long decompressedBytes)
    {
        ReadOnlyMemorySlice<byte> memory = new(plugin.Bytes);
        ModHeaderFrame header = new(GameConstants.SkyrimSE, memory);
        ReadOnlyMemorySlice<byte> remaining = memory.Slice(checked((int)header.TotalLength));
        List<BethesdaUnsupportedField> unsupported = [];
        List<BethesdaUnsupportedShape> unsupportedShapes = [];
        Dictionary<string, Dictionary<string, int>> fields = new(StringComparer.OrdinalIgnoreCase);
        int recordCount = 0;
        while (remaining.Length > 0)
        {
            GroupFrame group = new(GameConstants.SkyrimSE, remaining);
            ObserveGroupFields(group, plugin, masterStyles, unsupported, unsupportedShapes, fields, ref recordCount, ref decompressedBytes, 0);
            remaining = remaining.Slice(checked((int)group.TotalLength));
        }

        if (recordCount > 4096)
        {
            throw Input("record-count-over-limit", plugin.Receipt.PluginName, "The plugin exceeds the bounded Mutagen record population.");
        }

        return new PluginStructuralObservation(fields, unsupported, unsupportedShapes);
    }

    private void ObserveGroupFields(
        GroupFrame group,
        SealedPlugin plugin,
        IReadOnlyDictionary<ModKey, BethesdaMasterStyle> masterStyles,
        List<BethesdaUnsupportedField> unsupported,
        List<BethesdaUnsupportedShape> unsupportedShapes,
        Dictionary<string, Dictionary<string, int>> fields,
        ref int recordCount,
        ref long decompressedBytes,
        int depth)
    {
        if (depth > 64)
        {
            throw Input("group-depth-over-limit", plugin.Receipt.PluginName, "The Mutagen group depth exceeds the bounded input authority.");
        }

        foreach (VariablePinHeader child in group)
        {
            if (child.IsGroup)
            {
                ObserveGroupFields(
                    new GroupFrame(GameConstants.SkyrimSE, child.HeaderAndContentData),
                    plugin,
                    masterStyles,
                    unsupported,
                    unsupportedShapes,
                    fields,
                    ref recordCount,
                    ref decompressedBytes,
                    depth + 1);
                continue;
            }

            MajorRecordFrame record = new(GameConstants.SkyrimSE, child.HeaderAndContentData);
            string signature = record.RecordType.Type;
            FormKey recordKey;
            try
            {
                recordKey = ResolveFileFormId(
                    record.FormID,
                    plugin,
                    masterStyles,
                    "record-master-index-invalid");
            }
            catch (BethesdaInputException) when (selectedFormKeys is not null)
            {
                continue;
            }
            string canonicalRecordKey = CanonicalFormKey(recordKey);
            if (selectedFormKeys is not null && !selectedFormKeys.Contains(canonicalRecordKey))
            {
                continue;
            }
            recordCount++;
            if (recordCount > 4096)
            {
                throw Input("record-count-over-limit", plugin.Receipt.PluginName, "The plugin exceeds the bounded Mutagen record population.");
            }
            Dictionary<string, int> recordFields = fields.GetValueOrDefault(canonicalRecordKey)
                ?? [];
            fields[canonicalRecordKey] = recordFields;
            _ = AllowedSemanticFields.TryGetValue(signature, out HashSet<string>? allowed);
            IEnumerable<SubrecordPinFrame> subrecords;
            if (record.IsCompressed)
            {
                if (record.Content.Length < sizeof(uint))
                {
                    throw Input("compressed-record-shape-invalid", plugin.Receipt.PluginName, "A compressed Mutagen record omits its declared expansion length.");
                }

                uint declaredLength = BinaryPrimitives.ReadUInt32LittleEndian(record.Content.Span);
                decompressedBytes = checked(decompressedBytes + declaredLength);
                if (declaredLength == 0 || decompressedBytes > maximumDecompressedBytes)
                {
                    throw Input("decompressed-bytes-over-limit", plugin.Receipt.PluginName, "The aggregate declared Mutagen decompression population exceeds the configured authority bound.");
                }

                byte[] decompressed = Decompression.Decompress(
                    record.Content.Slice(sizeof(uint)),
                    declaredLength);
                subrecords = RecordSpanExtensions.EnumerateSubrecords(
                    new ReadOnlyMemorySlice<byte>(decompressed),
                    GameConstants.SkyrimSE,
                    0);
            }
            else
            {
                subrecords = record;
            }

            int subrecordCount = 0;
            foreach (SubrecordPinFrame subrecord in subrecords)
            {
                subrecordCount++;
                if (subrecordCount > 4096)
                {
                    throw Input("subrecord-count-over-limit", plugin.Receipt.PluginName, "A Mutagen record exceeds the bounded subrecord population.");
                }
                string field = subrecord.RecordType.Type;
                recordFields[field] = recordFields.GetValueOrDefault(field) + 1;
                ValidateStructuralLink(subrecord, signature, field, plugin, masterStyles);
                if (FixedSemanticFieldLengths.TryGetValue((signature, field), out int expectedLength)
                    && subrecord.Content.Length != expectedLength)
                {
                    if (subrecord.Content.Length < expectedLength)
                    {
                        throw Input(
                            "semantic-field-shape-invalid",
                            plugin.Receipt.PluginName,
                            $"The {signature}:{field} field is truncated below its accepted semantic shape.");
                    }

                    unsupportedShapes.Add(new BethesdaUnsupportedShape(
                        plugin.Receipt.PluginName,
                        canonicalRecordKey,
                        signature,
                        field));
                }
                if (allowed is not null && !allowed.Contains(field))
                {
                    unsupported.Add(new BethesdaUnsupportedField(
                        plugin.Receipt.PluginName,
                        canonicalRecordKey,
                        signature,
                        field));
                }
            }
        }
    }

    private static void ValidateStructuralLink(
        SubrecordPinFrame subrecord,
        string recordSignature,
        string field,
        SealedPlugin plugin,
        IReadOnlyDictionary<ModKey, BethesdaMasterStyle> masterStyles)
    {
        int[] offsets = (recordSignature, field) switch
        {
            ("NPC_", "TPLT" or "RNAM" or "PKID" or "PNAM" or "HCLF") => [0],
            ("REFR", "NAME" or "XLRL" or "XOWN") => [0],
            ("REFR", "XLKR") => [0, 4],
            _ => [],
        };
        foreach (int offset in offsets)
        {
            if (subrecord.Content.Length < offset + sizeof(uint))
            {
                throw Input("link-field-shape-invalid", plugin.Receipt.PluginName, "A Mutagen-framed FormID field is truncated.");
            }

            uint raw = BitConverter.ToUInt32(subrecord.Content.Span[offset..]);
            if (raw != 0)
            {
                _ = ResolveFileFormId(new FormID(raw), plugin, masterStyles, "link-master-index-invalid");
            }
        }
    }

    private static FormKey ResolveFileFormId(
        FormID raw,
        SealedPlugin plugin,
        IReadOnlyDictionary<ModKey, BethesdaMasterStyle> masterStyles,
        string invalidIndexCode)
    {
        uint index = raw.MasterIndex(MasterStyle.Full);
        string[] table = [.. plugin.Receipt.Masters, plugin.ModKey.FileName.String];
        if (index >= table.Length)
        {
            throw Input(invalidIndexCode, plugin.Receipt.PluginName, "A Mutagen-framed FormID refers beyond the declared master table.");
        }

        ModKey origin = ModKey.FromFileName(table[index]);
        uint localId = raw.Id(MasterStyle.Full);
        if (masterStyles.TryGetValue(origin, out BethesdaMasterStyle originStyle)
            && originStyle == BethesdaMasterStyle.Light
            && localId is < 0x800 or > 0xFFF)
        {
            throw Input("light-link-local-id-invalid", raw.ToString(), "A light-origin local FormID is outside 0x800..0xFFF.");
        }

        return new FormKey(origin, localId);
    }

    public static Sha256Fingerprint ComputeDependencyFingerprint(
        OpaqueId snapshotId,
        IReadOnlyList<BethesdaPluginReceipt> plugins,
        IReadOnlyList<BethesdaUnsupportedCapability> requestedCapabilities)
    {
        StringBuilder canonical = new("infinium-bethesda-dependency-v1\n");
        canonical.AppendLine(snapshotId.Value)
            .AppendLine(ProducerId)
            .AppendLine(ProducerVersion)
            .AppendLine(MutagenVersion);
        foreach (BethesdaPluginReceipt plugin in plugins.OrderBy(plugin => plugin.LoadOrder))
        {
            canonical.Append(plugin.LoadOrder)
                .Append('|')
                .Append(plugin.PluginName.ToLowerInvariant())
                .Append('|')
                .Append(plugin.LocalInstalledEntityId.Value)
                .Append('|')
                .AppendLine(plugin.Sha256.Value);
        }

        foreach (BethesdaUnsupportedCapability capability in requestedCapabilities
                     .Distinct()
                     .OrderBy(value => value))
        {
            canonical.Append("requested-capability|").AppendLine(capability.ToString());
        }

        return new Sha256Fingerprint(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))));
    }

}
