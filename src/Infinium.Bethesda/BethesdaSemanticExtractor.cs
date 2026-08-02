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

public sealed class BethesdaSemanticExtractor
{
    public const string ProducerId = "infinium.bethesda.m1-semantic-index";
    public const string ProducerVersion = "1.0.0";
    public const string MutagenVersion = "0.54.2";
    public const string TaxonomyId = "infinium.skyrim-se.mod-impact-taxonomy";
    public static readonly ContractVersion TaxonomyVersion = new(0, 1, 0);

    private const long MaximumInputBytes = 64L * 1024 * 1024;
    private static readonly HashSet<string> SemanticRecordFamilies =
    [
        "NPC_", "RACE", "REFR",
    ];
    private static readonly HashSet<string> IdentityOnlyRecordFamilies =
    [
        "CELL", "CLAS", "PACK", "CLFM", "FACT", "HDPT", "KYWD", "LCTN", "STAT",
    ];
    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedSemanticFields =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["NPC_"] = ["EDID", "ACBS", "TPLT", "RNAM", "AIDT", "PKID", "PNAM", "HCLF"],
            ["RACE"] = ["EDID", "DATA"],
            ["REFR"] = ["EDID", "NAME", "XLKR", "XLRL", "XOWN", "DATA"],
        };
    private readonly Action<string, int>? afterPluginRead;
    private readonly long maximumDecompressedBytes;

    public BethesdaSemanticExtractor()
    {
        maximumDecompressedBytes = MaximumInputBytes;
    }

    internal BethesdaSemanticExtractor(Action<string, int> afterPluginRead)
    {
        this.afterPluginRead = afterPluginRead;
        maximumDecompressedBytes = MaximumInputBytes;
    }

    internal BethesdaSemanticExtractor(long maximumDecompressedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDecompressedBytes, 1);
        this.maximumDecompressedBytes = maximumDecompressedBytes;
    }

    public BethesdaSemanticExtractionResult Extract(
        BethesdaSemanticRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            IReadOnlyList<BethesdaUnsupportedCapability> requested =
                request.RequestedUnsupportedCapabilities ?? [];
            if (requested.Count > 3
                || requested.Distinct().Count() != requested.Count
                || requested.Any(capability => !Enum.IsDefined(capability)))
            {
                throw Input(
                    "unsupported-capability-request-invalid",
                    "requested-unsupported-capabilities",
                    "Requested unsupported capabilities must be a unique subset of the closed Slice 4 vocabulary.");
            }

            ValidatedInput input = ValidateInput(request.AcceptedSnapshot);
            List<SealedPlugin> plugins = SealPlugins(input, cancellationToken);
            return BuildResult(request, input, plugins, cancellationToken);
        }
        catch (BethesdaInputException exception)
        {
            return Failure(exception.State, exception.Code, exception.Input, exception.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failure(
                BethesdaExtractionState.InvalidInput,
                "bethesda-parse-rejected",
                "accepted-snapshot",
                Bounded(exception.Message));
        }
    }

    private BethesdaSemanticExtractionResult BuildResult(
        BethesdaSemanticRequest request,
        ValidatedInput input,
        List<SealedPlugin> plugins,
        CancellationToken cancellationToken)
    {
        KeyedMasterStyle[] loadOrder = plugins
            .Select(plugin => new KeyedMasterStyle(
                plugin.ModKey,
                plugin.MasterStyle == BethesdaMasterStyle.Light
                    ? MasterStyle.Small
                    : MasterStyle.Full))
            .ToArray();
        Dictionary<ModKey, BethesdaMasterStyle> masterStyles = plugins.ToDictionary(
            plugin => plugin.ModKey,
            plugin => plugin.MasterStyle);
        long decompressedBytes = 0;
        Dictionary<string, PluginStructuralObservation> structuralByPlugin = plugins.ToDictionary(
            plugin => plugin.Receipt.PluginName,
            plugin => ObservePluginStructure(plugin, masterStyles, ref decompressedBytes),
            StringComparer.OrdinalIgnoreCase);
        List<ISkyrimModDisposableGetter> mods = [];
        try
        {
            foreach (SealedPlugin plugin in plugins)
            {
                cancellationToken.ThrowIfCancellationRequested();
                mods.Add(SkyrimMod.Create(SkyrimRelease.SkyrimSE)
                    .FromStreamFactory(
                        () => new MemoryStream(plugin.Bytes, writable: false),
                        plugin.ModKey)
                    .WithLoadOrder(loadOrder)
                    .WithNoDataFolder()
                    .SingleThread()
                    .ThrowIfUnknownSubrecord(false)
                    .Construct());
            }

            ValidateMasterStylesAndReferences(plugins, mods);
            HashSet<FormKey> allRecordKeys = mods
                .SelectMany(mod => mod.EnumerateMajorRecords())
                .Where(record =>
                {
                    string family = SemanticSignature(record);
                    return SemanticRecordFamilies.Contains(family)
                        || IdentityOnlyRecordFamilies.Contains(family);
                })
                .Select(record => record.FormKey)
                .ToHashSet();
            Dictionary<string, BethesdaResolvedParticipant> resolvedParticipants =
                allRecordKeys.ToDictionary(
                    CanonicalFormKey,
                    key => new BethesdaResolvedParticipant(
                        ParticipantId(key),
                        CanonicalFormKey(key)),
                    StringComparer.OrdinalIgnoreCase);
            List<BethesdaRecordContribution> contributions = [];
            List<BethesdaNpcFact> npcFacts = [];
            List<BethesdaRaceFact> raceFacts = [];
            List<BethesdaPlacedReferenceFact> referenceFacts = [];
            List<BethesdaFieldPresence> allowlistedFields = [];
            List<BethesdaLinkFact> links = [];
            List<BethesdaUnsupportedRecord> unsupportedRecords = [];
            List<BethesdaUnsupportedField> unsupportedFields = [];

            for (int index = 0; index < mods.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ISkyrimModGetter mod = mods[index];
                IMajorRecordGetter[] allModRecords = mod.EnumerateMajorRecords().ToArray();
                HashSet<ModKey> allowedOrigins = mod.ModHeader.MasterReferences
                    .Select(reference => reference.Master)
                    .Append(plugins[index].ModKey)
                    .ToHashSet();
                foreach (IMajorRecordGetter record in allModRecords)
                {
                    string recordSignature = SemanticSignature(record);
                    if (!allowedOrigins.Contains(record.FormKey.ModKey))
                    {
                        throw Input("record-master-index-invalid", record.FormKey.ToString(), "Mutagen resolved a record origin outside the plugin's declared master table.");
                    }

                    if (SemanticRecordFamilies.Contains(recordSignature))
                    {
                        contributions.Add(Contribution(
                            record,
                            recordSignature,
                            plugins[index]));
                    }
                    else if (!IdentityOnlyRecordFamilies.Contains(recordSignature))
                    {
                        unsupportedRecords.Add(new BethesdaUnsupportedRecord(
                            recordSignature,
                            CanonicalFormKey(record.FormKey),
                            plugins[index].Receipt.PluginName));
                    }
                }

                PluginStructuralObservation structural = structuralByPlugin[plugins[index].Receipt.PluginName];
                unsupportedFields.AddRange(structural.UnsupportedFields);
                foreach (BethesdaRecordContribution contribution in contributions.Where(item =>
                             item.SourcePlugin == plugins[index].Receipt.PluginName))
                {
                    if (!structural.Fields.TryGetValue(
                            contribution.Identity.FormKey,
                            out Dictionary<string, int>? observed)
                        || !AllowedSemanticFields.TryGetValue(
                            contribution.Identity.Signature,
                            out HashSet<string>? allowed))
                    {
                        continue;
                    }

                    allowlistedFields.AddRange(observed
                        .Where(item => allowed.Contains(item.Key))
                        .OrderBy(item => item.Key, StringComparer.Ordinal)
                        .Select(item => new BethesdaFieldPresence(
                            contribution.ContributionId,
                            item.Key,
                            item.Value)));
                }

                INpcGetter[] modNpcs = mod.EnumerateMajorRecords<INpcGetter>().ToArray();
                for (int npcIndex = 0; npcIndex < modNpcs.Length; npcIndex++)
                {
                    INpcGetter npc = modNpcs[npcIndex];
                    BethesdaRecordContribution contribution = Contribution(npc, "NPC_", plugins[index]);
                    BethesdaLinkFact? template = !structural.HasField(contribution.Identity.FormKey, "TPLT")
                        ? null
                        : !npc.Template.IsNull
                            ? Link(contribution, "TPLT", 0, npc.Template.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                            : Link(contribution, "TPLT", 0, default, true, allRecordKeys, masterStyles, allowedOrigins);
                    BethesdaLinkFact? race = !structural.HasField(contribution.Identity.FormKey, "RNAM")
                        ? null
                        : !npc.Race.IsNull
                            ? Link(contribution, "RNAM", 0, npc.Race.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                            : Link(contribution, "RNAM", 0, default, true, allRecordKeys, masterStyles, allowedOrigins);
                    BethesdaLinkFact? hair = !structural.HasField(contribution.Identity.FormKey, "HCLF")
                        ? null
                        : !npc.HairColor.IsNull
                            ? Link(contribution, "HCLF", 0, npc.HairColor.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                            : Link(contribution, "HCLF", 0, default, true, allRecordKeys, masterStyles, allowedOrigins);
                    BethesdaLinkFact[] packages = npc.Packages
                        .Select((package, ordinal) => Link(contribution, "PKID", ordinal, package.FormKey, package.IsNull, allRecordKeys, masterStyles, allowedOrigins))
                        .ToArray();
                    BethesdaLinkFact[] headParts = npc.HeadParts
                        .Select((part, ordinal) => Link(contribution, "PNAM", ordinal, part.FormKey, part.IsNull, allRecordKeys, masterStyles, allowedOrigins))
                        .ToArray();
                    if (structural.HasField(contribution.Identity.FormKey, "DOFT")
                        && !npc.DefaultOutfit.IsNull)
                    {
                        unsupportedFields.Add(new BethesdaUnsupportedField(
                            contribution.Identity.FormKey,
                            "NPC_",
                            "DOFT"));
                    }
                    BethesdaAiDataFact? ai = !structural.HasField(contribution.Identity.FormKey, "AIDT")
                        || npc.AIData is null
                        ? null
                        : new BethesdaAiDataFact(
                            Convert.ToInt32(npc.AIData.Aggression, CultureInfo.InvariantCulture),
                            Convert.ToInt32(npc.AIData.Confidence, CultureInfo.InvariantCulture),
                            npc.AIData.EnergyLevel,
                            Convert.ToInt32(npc.AIData.Responsibility, CultureInfo.InvariantCulture),
                            Convert.ToInt32(npc.AIData.Mood, CultureInfo.InvariantCulture),
                            Convert.ToInt32(npc.AIData.Assistance, CultureInfo.InvariantCulture),
                            npc.AIData.Warn,
                            npc.AIData.WarnOrAttack,
                            npc.AIData.Attack,
                            npc.AIData.AggroRadiusBehavior);
                    npcFacts.Add(new BethesdaNpcFact(
                        contribution,
                        Convert.ToUInt32(npc.Configuration.Flags, CultureInfo.InvariantCulture),
                        Convert.ToUInt32(npc.Configuration.TemplateFlags, CultureInfo.InvariantCulture),
                        npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.UseTemplate),
                        npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Traits),
                        template,
                        race,
                        ai,
                        packages,
                        headParts,
                        hair));
                    if (template is not null)
                    {
                        links.Add(template);
                    }

                    if (race is not null)
                    {
                        links.Add(race);
                    }

                    links.AddRange(packages);
                    links.AddRange(headParts);
                    if (hair is not null)
                    {
                        links.Add(hair);
                    }
                }

                foreach (IRaceGetter race in mod.EnumerateMajorRecords<IRaceGetter>())
                {
                    BethesdaRecordContribution contribution = Contribution(race, "RACE", plugins[index]);
                    raceFacts.Add(new BethesdaRaceFact(
                        contribution,
                        race.Flags.HasFlag(Race.Flag.FaceGenHead)));
                }

                IPlacedObjectGetter[] modReferences = mod.EnumerateMajorRecords<IPlacedObjectGetter>().ToArray();
                for (int referenceIndex = 0; referenceIndex < modReferences.Length; referenceIndex++)
                {
                    IPlacedObjectGetter placed = modReferences[referenceIndex];
                    BethesdaRecordContribution contribution = Contribution(placed, "REFR", plugins[index]);
                    BethesdaLinkFact? baseLink = !placed.Base.IsNull
                        ? Link(contribution, "NAME", 0, placed.Base.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                        : structural.HasField(contribution.Identity.FormKey, "NAME")
                            ? Link(contribution, "NAME", 0, default, true, allRecordKeys, masterStyles, allowedOrigins)
                            : null;
                    BethesdaLinkFact[] linked = placed.LinkedReferences
                        .SelectMany((item, ordinal) => new[]
                        {
                            Link(contribution, "XLKR", ordinal, item.Reference.FormKey, item.Reference.IsNull, allRecordKeys, masterStyles, allowedOrigins, "linked-reference"),
                            Link(contribution, "XLKR", ordinal, item.KeywordOrReference.FormKey, item.KeywordOrReference.IsNull, allRecordKeys, masterStyles, allowedOrigins, "keyword"),
                        })
                        .ToArray();
                    BethesdaLinkFact? location = !placed.LocationReference.IsNull
                        ? Link(contribution, "XLRL", 0, placed.LocationReference.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                        : structural.HasField(contribution.Identity.FormKey, "XLRL")
                            ? Link(contribution, "XLRL", 0, default, true, allRecordKeys, masterStyles, allowedOrigins)
                            : null;
                    BethesdaLinkFact? owner = !placed.Owner.IsNull
                        ? Link(contribution, "XOWN", 0, placed.Owner.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                        : structural.HasField(contribution.Identity.FormKey, "XOWN")
                            ? Link(contribution, "XOWN", 0, default, true, allRecordKeys, masterStyles, allowedOrigins)
                            : null;
                    BethesdaPlacementFact? placement = !structural.HasField(contribution.Identity.FormKey, "DATA")
                        || placed.Placement is null
                        ? null
                        : new BethesdaPlacementFact(
                            new BethesdaVector3(
                                placed.Placement.Position.X,
                                placed.Placement.Position.Y,
                                placed.Placement.Position.Z),
                            new BethesdaVector3(
                                placed.Placement.Rotation.X,
                                placed.Placement.Rotation.Y,
                                placed.Placement.Rotation.Z));
                    referenceFacts.Add(new BethesdaPlacedReferenceFact(
                        contribution,
                        baseLink,
                        linked,
                        location,
                        owner,
                        placement));
                    if (baseLink is not null)
                    {
                        links.Add(baseLink);
                    }

                    links.AddRange(linked);
                    if (location is not null)
                    {
                        links.Add(location);
                    }

                    if (owner is not null)
                    {
                        links.Add(owner);
                    }
                }
            }

            IReadOnlyDictionary<string, BethesdaOverrideChain> chains = contributions
                .GroupBy(item => item.Identity.FormKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        BethesdaRecordContribution[] ordered = group.OrderBy(item => item.LoadOrder).ToArray();
                        return new BethesdaOverrideChain(ordered[0].Identity, ordered, ordered[^1]);
                    },
                    StringComparer.OrdinalIgnoreCase);
            Dictionary<string, BethesdaRecordContribution> winners = chains.ToDictionary(
                item => item.Key,
                item => item.Value.Winner,
                StringComparer.OrdinalIgnoreCase);
            Dictionary<string, BethesdaNpcFact> npcs = WinnerFacts(npcFacts, chains, fact => fact.Contribution);
            Dictionary<string, BethesdaRaceFact> races = WinnerFacts(raceFacts, chains, fact => fact.Contribution);
            Dictionary<string, BethesdaPlacedReferenceFact> references = WinnerFacts(referenceFacts, chains, fact => fact.Contribution);

            BethesdaUnsupportedField[] distinctUnsupportedFields = unsupportedFields
                .DistinctBy(item => $"{item.FormKey}|{item.RecordSignature}|{item.FieldSignature}", StringComparer.OrdinalIgnoreCase)
                .ToArray();
            List<BethesdaCoverageGap> gaps = BuildGaps(request, unsupportedRecords, distinctUnsupportedFields);
            List<BethesdaFaceGenFact> faceGen = BuildFaceGen(input.Snapshot, npcs, races, gaps);
            List<BethesdaTaxonomyProjection> taxonomy = BuildTaxonomy(input.Snapshot, contributions, npcFacts, referenceFacts, unsupportedRecords);
            List<BethesdaCoveragePopulation> coverage = BuildCoverage(plugins, contributions, gaps);
            Dictionary<string, IReadOnlyList<BethesdaLinkFact>> reverseLinks = links
                .Where(link => link.TargetParticipantId is not null)
                .GroupBy(link => link.TargetParticipantId!, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<BethesdaLinkFact>)group
                        .OrderBy(link => link.SourceParticipantId, StringComparer.Ordinal)
                        .ThenBy(link => link.Field, StringComparer.Ordinal)
                        .ThenBy(link => link.Ordinal)
                        .ToArray(),
                    StringComparer.Ordinal);
            Sha256Fingerprint dependency = ComputeDependencyFingerprint(
                input.Snapshot.Contract.SnapshotId,
                plugins.Select(plugin => plugin.Receipt).ToArray(),
                request.RequestedUnsupportedCapabilities ?? []);
            BethesdaSemanticSnapshot snapshot = new(
                input.Snapshot.Contract.SnapshotId,
                new ContractVersion(1, 0, 0),
                ProducerId,
                ProducerVersion,
                dependency,
                plugins.Select(plugin => plugin.Receipt).ToArray(),
                chains,
                winners,
                npcFacts,
                raceFacts,
                referenceFacts,
                allowlistedFields,
                resolvedParticipants,
                npcs,
                races,
                references,
                links,
                reverseLinks,
                faceGen,
                taxonomy,
                coverage,
                gaps);
            return new BethesdaSemanticExtractionResult(
                gaps.Count == 0 ? BethesdaExtractionState.Completed : BethesdaExtractionState.CompletedWithGaps,
                snapshot,
                [],
                gaps);
        }
        finally
        {
            foreach (ISkyrimModDisposableGetter mod in mods)
            {
                mod.Dispose();
            }
        }
    }

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
            if (before.Length > MaximumInputBytes || totalBytes > MaximumInputBytes)
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
            throw Input("snapshot-not-accepted", "accepted-snapshot", "Bethesda extraction requires an accepted Slice 3 snapshot.");
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

    private static void ValidateMasterStylesAndReferences(
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
            foreach (IMajorRecordGetter record in mods[index].EnumerateMajorRecords())
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

    private PluginStructuralObservation ObservePluginStructure(
        SealedPlugin plugin,
        IReadOnlyDictionary<ModKey, BethesdaMasterStyle> masterStyles,
        ref long decompressedBytes)
    {
        ReadOnlyMemorySlice<byte> memory = new(plugin.Bytes);
        ModHeaderFrame header = new(GameConstants.SkyrimSE, memory);
        ReadOnlyMemorySlice<byte> remaining = memory.Slice(checked((int)header.TotalLength));
        List<BethesdaUnsupportedField> unsupported = [];
        Dictionary<string, Dictionary<string, int>> fields = new(StringComparer.OrdinalIgnoreCase);
        int recordCount = 0;
        while (remaining.Length > 0)
        {
            GroupFrame group = new(GameConstants.SkyrimSE, remaining);
            ObserveGroupFields(group, plugin, masterStyles, unsupported, fields, ref recordCount, ref decompressedBytes, 0);
            remaining = remaining.Slice(checked((int)group.TotalLength));
        }

        if (recordCount > 4096)
        {
            throw Input("record-count-over-limit", plugin.Receipt.PluginName, "The plugin exceeds the bounded Mutagen record population.");
        }

        return new PluginStructuralObservation(fields, unsupported);
    }

    private void ObserveGroupFields(
        GroupFrame group,
        SealedPlugin plugin,
        IReadOnlyDictionary<ModKey, BethesdaMasterStyle> masterStyles,
        List<BethesdaUnsupportedField> unsupported,
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
                    fields,
                    ref recordCount,
                    ref decompressedBytes,
                    depth + 1);
                continue;
            }

            recordCount++;
            if (recordCount > 4096)
            {
                throw Input("record-count-over-limit", plugin.Receipt.PluginName, "The plugin exceeds the bounded Mutagen record population.");
            }

            MajorRecordFrame record = new(GameConstants.SkyrimSE, child.HeaderAndContentData);
            string signature = record.RecordType.Type;
            FormKey recordKey = ResolveFileFormId(
                record.FormID,
                plugin,
                masterStyles,
                "record-master-index-invalid");
            string canonicalRecordKey = CanonicalFormKey(recordKey);
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
                if (allowed is not null && !allowed.Contains(field))
                {
                    unsupported.Add(new BethesdaUnsupportedField(
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

    private static List<BethesdaCoverageGap> BuildGaps(
        BethesdaSemanticRequest request,
        IReadOnlyList<BethesdaUnsupportedRecord> unsupportedRecords,
        IReadOnlyList<BethesdaUnsupportedField> unsupportedFields)
    {
        List<BethesdaCoverageGap> gaps = [];
        int unsupportedFamilies = unsupportedRecords.Count;
        if (unsupportedFamilies > 0)
        {
            gaps.Add(Gap("record-family", "record_family", unsupportedFamilies, "One or more record families are outside the M1 positive allowlist.", "allowlisted-record-family-semantics"));
        }

        if (unsupportedFields.Count > 0)
        {
            gaps.Add(Gap("record-field", "record_field", unsupportedFields.Count, "One or more record fields are outside the M1 positive allowlist.", "allowlisted-record-field-semantics"));
        }

        foreach (BethesdaUnsupportedCapability capability in
                 (request.RequestedUnsupportedCapabilities ?? []).Distinct())
        {
            (string Key, string Population, string Missing) mapping = capability switch
            {
                BethesdaUnsupportedCapability.LocalizedStringResolution => ("localized-string-resolution", "localized_string_resolution", "localized-string-resolution"),
                BethesdaUnsupportedCapability.ArchiveMemberRead => ("archive-member-read", "archive_member_read", "archive-activation-and-member-precedence"),
                BethesdaUnsupportedCapability.AutomaticEnvironmentDiscovery => ("automatic-environment-discovery", "automatic_environment_discovery", "automatic-environment-discovery"),
                _ => throw Input("unsupported-capability-invalid", capability.ToString(), "The requested unsupported capability is outside the closed Slice 4 vocabulary."),
            };
            gaps.Add(Gap(mapping.Key, mapping.Population, 1, $"Capability '{mapping.Key}' is outside the M1 Bethesda allowlist.", mapping.Missing));
        }

        return gaps;
    }

    private static BethesdaCoverageGap Gap(string key, string population, long denominator, string reason, string missing) =>
        new($"gap:infinium-bethesda:{key}", population, denominator, reason, missing);

    private static List<BethesdaCoveragePopulation> BuildCoverage(
        IReadOnlyList<SealedPlugin> plugins,
        IReadOnlyList<BethesdaRecordContribution> contributions,
        IReadOnlyList<BethesdaCoverageGap> gaps)
    {
        List<BethesdaCoveragePopulation> coverage =
        [
            new("plugins", "enabled_snapshot_plugins", plugins.Count, plugins.Count, CoverageState.Completed, []),
            new("allowlisted_records", "allowlisted_record_contributions", contributions.Count, contributions.Count, CoverageState.Completed, []),
        ];
        foreach (IGrouping<string, BethesdaCoverageGap> group in gaps.GroupBy(gap => gap.Population, StringComparer.Ordinal))
        {
            coverage.Add(new BethesdaCoveragePopulation(
                group.Key,
                group.Key,
                group.Sum(gap => gap.Denominator),
                0,
                CoverageState.Unsupported,
                group.Select(gap => gap.GapId).ToArray()));
        }
        return coverage;
    }

    private static List<BethesdaFaceGenFact> BuildFaceGen(
        Mo2InstallationSnapshot snapshot,
        IReadOnlyDictionary<string, BethesdaNpcFact> npcs,
        IReadOnlyDictionary<string, BethesdaRaceFact> races,
        List<BethesdaCoverageGap> gaps)
    {
        List<BethesdaFaceGenFact> results = [];
        foreach (BethesdaNpcFact npc in npcs.Values)
        {
            BethesdaRaceFact? race = npc.Race?.TargetFormKey is not null
                && races.TryGetValue(npc.Race.TargetFormKey, out BethesdaRaceFact? resolvedRace)
                    ? resolvedRace
                    : null;
            BethesdaFaceGenApplicability applicability = DetermineFaceGenApplicability(
                npc.Contribution.Deleted,
                npc.Race?.State,
                race?.FaceGenHead,
                npc.UsesTemplate,
                npc.TemplatesTraits);
            (string reason, string? gapKey, string? missing) = applicability switch
            {
                BethesdaFaceGenApplicability.CoverageGapDeletedWinner => ("The winning NPC contribution is deleted.", "deleted", "non-deleted-winning-npc"),
                BethesdaFaceGenApplicability.CoverageGapUnresolvedRace => ("The winning NPC race is not resolved inside the accepted input.", "race", "resolved-winning-race"),
                BethesdaFaceGenApplicability.InapplicableRaceWithoutFaceGenHead => ("The resolved race does not carry FaceGenHead.", null, null),
                BethesdaFaceGenApplicability.CoverageGapTemplateSource => ("UseTemplate prevents an exact loose FaceGen applicability conclusion without the resolved template source.", "template-source", "template-source-required"),
                BethesdaFaceGenApplicability.CoverageGapTemplateTraits => ("Template-Traits prevents an exact loose FaceGen applicability conclusion without the resolved traits source.", "template-traits", "template-traits-source-required"),
                BethesdaFaceGenApplicability.Applicable => ("The qualified loose-only FaceGen applicability predicates are satisfied.", null, null),
                _ => throw new InvalidOperationException("The FaceGen applicability state is outside the closed Slice 4 vocabulary."),
            };
            if (gapKey is not null)
            {
                gaps.Add(Gap($"facegen-{gapKey}-{npc.Contribution.Identity.ParticipantId}", "facegen_applicability", 1, reason, missing!));
            }

            string origin = npc.Contribution.Identity.OriginPlugin;
            string id = npc.Contribution.Identity.OriginLocalId.ToString("X8", CultureInfo.InvariantCulture);
            BethesdaLooseAssetFact mesh = LooseAsset(snapshot, $"meshes/actors/character/facegendata/facegeom/{origin}/{id}.nif");
            BethesdaLooseAssetFact tint = LooseAsset(snapshot, $"textures/actors/character/facegendata/facetint/{origin}/{id}.dds");
            if (applicability == BethesdaFaceGenApplicability.Applicable
                && !snapshot.ArchiveMemberPopulationSupported
                && (!mesh.Present || !tint.Present)
                && gaps.All(gap => gap.Population != "archive_member_read"))
            {
                gaps.Add(Gap("facegen-archive-unknown", "archive_member_read", 1, "Loose FaceGen absence is not exact because archive participation is unsupported.", "archive-activation-and-member-precedence"));
            }

            results.Add(new BethesdaFaceGenFact(
                npc.Contribution.Identity.ParticipantId,
                applicability,
                origin,
                npc.Contribution.Identity.OriginLocalId,
                mesh,
                tint,
                reason));
        }

        return results;
    }

    internal static BethesdaFaceGenApplicability DetermineFaceGenApplicability(
        bool deleted,
        BethesdaLinkState? raceState,
        bool? raceFaceGenHead,
        bool usesTemplate,
        bool templatesTraits)
    {
        if (deleted)
        {
            return BethesdaFaceGenApplicability.CoverageGapDeletedWinner;
        }

        if (raceState != BethesdaLinkState.Resolved || raceFaceGenHead is null)
        {
            return BethesdaFaceGenApplicability.CoverageGapUnresolvedRace;
        }

        if (!raceFaceGenHead.Value)
        {
            return BethesdaFaceGenApplicability.InapplicableRaceWithoutFaceGenHead;
        }

        if (usesTemplate)
        {
            return BethesdaFaceGenApplicability.CoverageGapTemplateSource;
        }

        return templatesTraits
            ? BethesdaFaceGenApplicability.CoverageGapTemplateTraits
            : BethesdaFaceGenApplicability.Applicable;
    }

    private static BethesdaLooseAssetFact LooseAsset(Mo2InstallationSnapshot snapshot, string path)
    {
        string normalized = path.Replace('\\', '/').ToLowerInvariant();
        LooseProviderChain? chain = snapshot.LooseProviderChains.SingleOrDefault(candidate =>
            string.Equals(candidate.NormalizedRelativePath.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        if (chain is null)
        {
            return new BethesdaLooseAssetFact(normalized, [], null, false, snapshot.ArchiveMemberPopulationSupported);
        }

        return new BethesdaLooseAssetFact(
            normalized,
            chain.Providers.Select(provider => provider.LocalInstalledEntityId.Value).ToArray(),
            chain.Winner.LocalInstalledEntityId.Value,
            true,
            true);
    }

    private static List<BethesdaTaxonomyProjection> BuildTaxonomy(
        Mo2InstallationSnapshot source,
        IReadOnlyList<BethesdaRecordContribution> contributions,
        IReadOnlyList<BethesdaNpcFact> npcs,
        IReadOnlyList<BethesdaPlacedReferenceFact> references,
        IReadOnlyList<BethesdaUnsupportedRecord> unsupportedRecords)
    {
        List<BethesdaTaxonomyProjection> results = [];
        HashSet<string> unsupportedContributionKeys = unsupportedRecords
            .Select(unsupported => $"{unsupported.SourcePlugin}\0{unsupported.FormKey}\0{unsupported.Signature}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (BethesdaRecordContribution contribution in contributions.Where(contribution =>
                     SemanticRecordFamilies.Contains(contribution.Identity.Signature)
                     || IdentityOnlyRecordFamilies.Contains(contribution.Identity.Signature)).Where(contribution =>
                     !unsupportedContributionKeys.Contains($"{contribution.SourcePlugin}\0{contribution.Identity.FormKey}\0{contribution.Identity.Signature}")))
        {
            results.Add(Projection(contribution.ContributionId, "record-contribution", "technical-modification-surface", "semantic-mechanism", "surface.plugin-data", TaxonomyApplicability.Assigned, ClassificationRole.Observed, [Evidence(contribution, "major-record", 0)], "The frozen major-record fact establishes plugin-carried record data."));
        }

        foreach (BethesdaNpcFact npc in npcs)
        {
            BethesdaLinkFact[] resolvedPackages = npc.Packages
                .Where(link => link.State == BethesdaLinkState.Resolved)
                .ToArray();
            if (npc.AiData is not null || resolvedPackages.Length > 0)
            {
                string subject = $"{npc.Contribution.ContributionId}:semantic:area.actors.ai-packages";
                List<string> evidence = [];
                if (npc.AiData is not null)
                {
                    evidence.Add(Evidence(npc.Contribution, "AIDT", 0));
                }
                evidence.AddRange(resolvedPackages.Select(link => Evidence(npc.Contribution, "PKID", link.Ordinal)));
                results.Add(Projection(subject, "record-semantic-subject", "technical-modification-surface", "semantic-mechanism", "surface.plugin-data", TaxonomyApplicability.Assigned, ClassificationRole.Observed, [Evidence(npc.Contribution, "major-record", 0)], "The frozen major-record fact establishes plugin-carried record data."));
                results.Add(Projection(subject, "record-semantic-subject", "affected-game-system-or-content-area", "affected-area", "area.actors.ai-packages", TaxonomyApplicability.Assigned, ClassificationRole.Established, evidence, "Separate frozen AIDT and resolved PKID facts establish AI-package semantics; the NPC_ signature does not."));
            }

            if (npc.Race?.State == BethesdaLinkState.Resolved)
            {
                string subject = $"{npc.Contribution.ContributionId}:semantic:area.actors.appearance-identity";
                results.Add(Projection(subject, "record-semantic-subject", "technical-modification-surface", "semantic-mechanism", "surface.plugin-data", TaxonomyApplicability.Assigned, ClassificationRole.Observed, [Evidence(npc.Contribution, "major-record", 0)], "The frozen major-record fact establishes plugin-carried record data."));
                results.Add(Projection(subject, "record-semantic-subject", "affected-game-system-or-content-area", "affected-area", "area.actors.appearance-identity", TaxonomyApplicability.Assigned, ClassificationRole.Established, [Evidence(npc.Contribution, "RNAM", 0)], "Separate frozen RNAM and resolved RACE-link facts establish appearance and identity semantics; the NPC_ signature does not."));
            }
        }

        foreach (BethesdaPlacedReferenceFact reference in references)
        {
            BethesdaLinkFact[] resolvedRelations = new BethesdaLinkFact?[]
                {
                    reference.Base,
                    reference.LocationReference,
                    reference.Owner,
                }
                .Where(link => link?.State == BethesdaLinkState.Resolved)
                .Select(link => link!)
                .Concat(reference.LinkedReferences.Where(link => link.State == BethesdaLinkState.Resolved))
                .ToArray();
            if (reference.Placement is not null || resolvedRelations.Length > 0)
            {
                string subject = $"{reference.Contribution.ContributionId}:semantic:area.world.placed-objects-activation";
                List<string> evidence = [];
                if (reference.Placement is not null)
                {
                    evidence.Add(Evidence(reference.Contribution, "DATA", 0));
                }
                evidence.AddRange(resolvedRelations.Select(link => Evidence(reference.Contribution, link.Field, link.Ordinal, link.Component)));
                results.Add(Projection(subject, "record-semantic-subject", "technical-modification-surface", "semantic-mechanism", "surface.plugin-data", TaxonomyApplicability.Assigned, ClassificationRole.Observed, [Evidence(reference.Contribution, "major-record", 0)], "The frozen major-record fact establishes plugin-carried record data."));
                results.Add(Projection(subject, "record-semantic-subject", "affected-game-system-or-content-area", "affected-area", "area.world.placed-objects-activation", TaxonomyApplicability.Assigned, ClassificationRole.Established, evidence, "Separate frozen placement, linked-reference, location, and resolved-link facts establish the placed-object area; the REFR signature does not."));
            }
        }

        foreach (BethesdaUnsupportedRecord unsupported in unsupportedRecords)
        {
            string subject = $"unsupported-record:{unsupported.SourcePlugin.ToLowerInvariant()}:{unsupported.Signature.ToLowerInvariant()}:{unsupported.FormKey.ToLowerInvariant()}";
            string recordEvidence = $"evidence:{subject}:major-record:0";
            string fileEvidence = $"evidence:{subject}:plugin-container:0";
            string gapEvidence = $"evidence:{subject}:record-family-gap:0";
            results.Add(Projection(subject, "unsupported-record", "technical-modification-surface", "semantic-mechanism", "surface.plugin-data", TaxonomyApplicability.Assigned, ClassificationRole.Observed, [recordEvidence], "The frozen major-record fact establishes plugin-carried record data even though its family semantics are unsupported."));
            results.Add(Projection(subject, "unsupported-record", "technical-modification-surface", "realization-and-delivery", "delivery.plugin-container", TaxonomyApplicability.Assigned, ClassificationRole.Observed, [fileEvidence, recordEvidence], "The frozen file and record facts establish delivery inside a plugin container."));
            results.Add(Projection(subject, "unsupported-record", "affected-game-system-or-content-area", "affected-area", null, TaxonomyApplicability.Unsupported, ClassificationRole.Established, [gapEvidence], "The frozen allowlist gap establishes that Slice 4 cannot determine affected-area semantics for this record family."));
            results.Add(Projection(subject, "unsupported-record", "consequence-type", "consequence-type", null, TaxonomyApplicability.Unknown, ClassificationRole.Predicted, [gapEvidence], "The surface is present, but unsupported semantics leave any consequence unknown."));
            foreach ((string Facet, string Reason) in new[]
                     {
                         ("direct-subject-breadth", "No effect is established, so direct-subject breadth is not applicable."),
                         ("spatial-breadth", "No effect is established, so spatial breadth is not applicable."),
                         ("persistence-and-lifecycle-breadth", "No effect is established, so persistence breadth is not applicable."),
                         ("causal-propagation-or-blast-radius", "No effect is established, so propagation breadth is not applicable."),
                     })
            {
                results.Add(Projection(subject, "unsupported-record", "effect-extent", Facet, null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established, [gapEvidence], Reason));
            }
        }

        if (source.Plugins.Count(plugin => plugin.Enabled) > 1)
        {
            string subject = $"provider-topology:{source.Contract.SnapshotId.Value}";
            string[] evidence = source.Plugins.Where(plugin => plugin.Enabled)
                .OrderBy(plugin => plugin.LoadOrder)
                .Select(plugin => $"provider:{plugin.WinningLocalInstalledEntityId!.Value}")
                .ToArray();
            results.Add(Projection(subject, "provider-topology", "declared-purpose-and-intended-feature-area", "purpose-kind", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established, evidence, "Provider ordering is topology, not declared purpose."));
            results.Add(Projection(subject, "provider-topology", "affected-game-system-or-content-area", "affected-area", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established, evidence, "Provider ordering alone does not describe an affected game area."));
            results.Add(Projection(subject, "provider-topology", "consequence-type", "consequence-type", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established, evidence, "Provider ordering alone establishes no consequence."));
            foreach ((string Facet, string Reason) in new[]
                     {
                         ("direct-subject-breadth", "Provider ordering alone establishes no direct-subject effect."),
                         ("spatial-breadth", "Provider ordering alone establishes no spatial effect."),
                         ("persistence-and-lifecycle-breadth", "Provider ordering alone establishes no persistence effect."),
                         ("causal-propagation-or-blast-radius", "Provider ordering alone establishes no propagation effect."),
                     })
            {
                results.Add(Projection(subject, "provider-topology", "effect-extent", Facet, null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established, evidence, Reason));
            }
        }

        return results;
    }

    private static string Evidence(
        BethesdaRecordContribution contribution,
        string field,
        int ordinal,
        string? component = null) =>
        $"evidence:{contribution.ContributionId}:{field}:{ordinal}:{component ?? "value"}";

    private static BethesdaTaxonomyProjection Projection(
        string subject,
        string subjectType,
        string axis,
        string facet,
        string? code,
        TaxonomyApplicability applicability,
        ClassificationRole role,
        IReadOnlyList<string> evidence,
        string reason)
    {
        string material = string.Join('|', subject, axis, code ?? applicability.ToString(), string.Join(',', evidence));
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..20];
        return new BethesdaTaxonomyProjection(
            $"taxonomy:{digest}",
            TaxonomyId,
            TaxonomyVersion,
            subject,
            subjectType,
            axis,
            facet,
            code,
            applicability,
            role,
            evidence,
            "analyzer:infinium-bethesda-m1-semantic-index",
            reason);
    }

    private static BethesdaSemanticExtractionResult Failure(BethesdaExtractionState state, string code, string input, string message) =>
        new(state, null, [new BethesdaExtractionFailure(code, input, message)], []);

    private static BethesdaInputException Input(string code, string input, string message) =>
        new(BethesdaExtractionState.InvalidInput, code, input, message);

    private static BethesdaInputException Changed(string input, string message) =>
        new(BethesdaExtractionState.ChangedDuringRead, "plugin-changed-during-read", input, message);

    private static string Bounded(string value) => value.Length <= 512 ? value : value[..512];

    private sealed record AuthorizedPlugin(PluginState Plugin, LooseProvider Winner, string Path);

    private sealed record ValidatedInput(Mo2InstallationSnapshot Snapshot, IReadOnlyList<AuthorizedPlugin> OrderedPlugins);

    private sealed record SealedPlugin(
        BethesdaPluginReceipt Receipt,
        ModKey ModKey,
        byte[] Bytes,
        BethesdaMasterStyle MasterStyle);

    private sealed record BethesdaUnsupportedRecord(
        string Signature,
        string FormKey,
        string SourcePlugin);

    private sealed record BethesdaUnsupportedField(
        string FormKey,
        string RecordSignature,
        string FieldSignature);

    private sealed record PluginStructuralObservation(
        IReadOnlyDictionary<string, Dictionary<string, int>> Fields,
        IReadOnlyList<BethesdaUnsupportedField> UnsupportedFields)
    {
        public bool HasField(string formKey, string field) =>
            Fields.TryGetValue(formKey, out Dictionary<string, int>? fields)
            && fields.ContainsKey(field);
    }

    private sealed class BethesdaInputException(
        BethesdaExtractionState state,
        string code,
        string input,
        string message) : Exception(message)
    {
        public BethesdaExtractionState State { get; } = state;
        public string Code { get; } = code;
        public string Input { get; } = input;
    }
}
