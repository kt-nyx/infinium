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
    public const string ProducerId = "infinium.bethesda.semantic-index";
    public const string ProducerVersion = "2.0.0";
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
    private static readonly IReadOnlyDictionary<(string Record, string Field), int> FixedSemanticFieldLengths =
        new Dictionary<(string Record, string Field), int>
        {
            [("NPC_", "ACBS")] = 24,
            [("NPC_", "TPLT")] = 4,
            [("NPC_", "RNAM")] = 4,
            [("NPC_", "AIDT")] = 20,
            [("NPC_", "PKID")] = 4,
            [("NPC_", "PNAM")] = 4,
            [("NPC_", "HCLF")] = 4,
            [("RACE", "DATA")] = 128,
            [("REFR", "NAME")] = 4,
            [("REFR", "XLKR")] = 8,
            [("REFR", "XLRL")] = 4,
            [("REFR", "XOWN")] = 4,
            [("REFR", "DATA")] = 24,
        };
    private readonly Action<string, int>? afterPluginRead;
    private readonly long maximumInputBytes;
    private readonly long maximumDecompressedBytes;
    private readonly IReadOnlySet<string>? selectedFormKeys;

    public BethesdaSemanticExtractor()
    {
        maximumInputBytes = MaximumInputBytes;
        maximumDecompressedBytes = MaximumInputBytes;
    }

    public BethesdaSemanticExtractor(long maximumInputBytes, long maximumDecompressedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumInputBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDecompressedBytes, 1);
        this.maximumInputBytes = maximumInputBytes;
        this.maximumDecompressedBytes = maximumDecompressedBytes;
    }

    public BethesdaSemanticExtractor(
        long maximumInputBytes,
        long maximumDecompressedBytes,
        IReadOnlyCollection<string> selectedFormKeys)
        : this(maximumInputBytes, maximumDecompressedBytes)
    {
        ArgumentNullException.ThrowIfNull(selectedFormKeys);
        if (selectedFormKeys.Count is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(selectedFormKeys));
        }
        this.selectedFormKeys = selectedFormKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    internal BethesdaSemanticExtractor(Action<string, int> afterPluginRead)
    {
        this.afterPluginRead = afterPluginRead;
        maximumInputBytes = MaximumInputBytes;
        maximumDecompressedBytes = MaximumInputBytes;
    }

    internal BethesdaSemanticExtractor(long maximumDecompressedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDecompressedBytes, 1);
        maximumInputBytes = MaximumInputBytes;
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
                    "Requested unsupported capabilities must be a unique subset of the closed semantic capability vocabulary.");
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
                    return (SemanticRecordFamilies.Contains(family)
                            || IdentityOnlyRecordFamilies.Contains(family))
                        && IsSelected(record.FormKey);
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
            List<BethesdaUnsupportedShape> unsupportedShapes = [];

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
                    if (!IsSelected(record.FormKey))
                    {
                        continue;
                    }
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
                unsupportedShapes.AddRange(structural.UnsupportedShapes);
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

                INpcGetter[] modNpcs = mod.EnumerateMajorRecords<INpcGetter>().Where(item => IsSelected(item.FormKey)).ToArray();
                for (int npcIndex = 0; npcIndex < modNpcs.Length; npcIndex++)
                {
                    INpcGetter npc = modNpcs[npcIndex];
                    BethesdaRecordContribution contribution = Contribution(npc, "NPC_", plugins[index]);
                    BethesdaLinkFact? template = !structural.HasSupportedField(contribution.Identity.FormKey, "TPLT")
                        ? null
                        : !npc.Template.IsNull
                            ? Link(contribution, "TPLT", 0, npc.Template.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                            : Link(contribution, "TPLT", 0, default, true, allRecordKeys, masterStyles, allowedOrigins);
                    BethesdaLinkFact? race = !structural.HasSupportedField(contribution.Identity.FormKey, "RNAM")
                        ? null
                        : !npc.Race.IsNull
                            ? Link(contribution, "RNAM", 0, npc.Race.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                            : Link(contribution, "RNAM", 0, default, true, allRecordKeys, masterStyles, allowedOrigins);
                    BethesdaLinkFact? hair = !structural.HasSupportedField(contribution.Identity.FormKey, "HCLF")
                        ? null
                        : !npc.HairColor.IsNull
                            ? Link(contribution, "HCLF", 0, npc.HairColor.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                            : Link(contribution, "HCLF", 0, default, true, allRecordKeys, masterStyles, allowedOrigins);
                    BethesdaLinkFact[] packages = !structural.HasSupportedField(contribution.Identity.FormKey, "PKID")
                        ? []
                        : npc.Packages
                        .Select((package, ordinal) => Link(contribution, "PKID", ordinal, package.FormKey, package.IsNull, allRecordKeys, masterStyles, allowedOrigins))
                        .ToArray();
                    BethesdaLinkFact[] headParts = !structural.HasSupportedField(contribution.Identity.FormKey, "PNAM")
                        ? []
                        : npc.HeadParts
                        .Select((part, ordinal) => Link(contribution, "PNAM", ordinal, part.FormKey, part.IsNull, allRecordKeys, masterStyles, allowedOrigins))
                        .ToArray();
                    if (structural.HasField(contribution.Identity.FormKey, "DOFT"))
                    {
                        unsupportedFields.Add(new BethesdaUnsupportedField(
                            contribution.SourcePlugin,
                            contribution.Identity.FormKey,
                            "NPC_",
                            "DOFT"));
                    }
                    BethesdaAiDataFact? ai = !structural.HasSupportedField(contribution.Identity.FormKey, "AIDT")
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
                    bool hasSupportedConfiguration = structural.HasSupportedField(contribution.Identity.FormKey, "ACBS");
                    BethesdaTemplateTraitsDecision traitsDecision = !hasSupportedConfiguration
                        ? BethesdaTemplateTraitsDecision.Unknown
                        : npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Traits)
                            ? BethesdaTemplateTraitsDecision.KnownInherited
                            : BethesdaTemplateTraitsDecision.KnownNotInherited;
                    npcFacts.Add(new BethesdaNpcFact(
                        contribution,
                        hasSupportedConfiguration ? Convert.ToUInt32(npc.Configuration.Flags, CultureInfo.InvariantCulture) : 0,
                        hasSupportedConfiguration ? Convert.ToUInt32(npc.Configuration.TemplateFlags, CultureInfo.InvariantCulture) : 0,
                        hasSupportedConfiguration && npc.Configuration.Flags.HasFlag(NpcConfiguration.Flag.UseTemplate),
                        traitsDecision,
                        traitsDecision == BethesdaTemplateTraitsDecision.KnownInherited,
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

                foreach (IRaceGetter race in mod.EnumerateMajorRecords<IRaceGetter>().Where(item => IsSelected(item.FormKey)))
                {
                    BethesdaRecordContribution contribution = Contribution(race, "RACE", plugins[index]);
                    BethesdaRaceFaceGenHeadDecision faceGenHead = !structural.HasSupportedField(contribution.Identity.FormKey, "DATA")
                        ? BethesdaRaceFaceGenHeadDecision.Unknown
                        : race.Flags.HasFlag(Race.Flag.FaceGenHead)
                            ? BethesdaRaceFaceGenHeadDecision.KnownPresent
                            : BethesdaRaceFaceGenHeadDecision.KnownAbsent;
                    raceFacts.Add(new BethesdaRaceFact(
                        contribution,
                        faceGenHead,
                        faceGenHead == BethesdaRaceFaceGenHeadDecision.KnownPresent));
                }

                IPlacedObjectGetter[] modReferences = mod.EnumerateMajorRecords<IPlacedObjectGetter>()
                    .Where(item => IsSelected(item.FormKey)).ToArray();
                for (int referenceIndex = 0; referenceIndex < modReferences.Length; referenceIndex++)
                {
                    IPlacedObjectGetter placed = modReferences[referenceIndex];
                    BethesdaRecordContribution contribution = Contribution(placed, "REFR", plugins[index]);
                    BethesdaLinkFact? baseLink = !structural.HasSupportedField(contribution.Identity.FormKey, "NAME")
                        ? null
                        : !placed.Base.IsNull
                            ? Link(contribution, "NAME", 0, placed.Base.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                            : Link(contribution, "NAME", 0, default, true, allRecordKeys, masterStyles, allowedOrigins);
                    BethesdaLinkFact[] linked = !structural.HasSupportedField(contribution.Identity.FormKey, "XLKR")
                        ? []
                        : placed.LinkedReferences
                        .SelectMany((item, ordinal) => new[]
                        {
                            Link(contribution, "XLKR", ordinal, item.Reference.FormKey, item.Reference.IsNull, allRecordKeys, masterStyles, allowedOrigins, "linked-reference"),
                            Link(contribution, "XLKR", ordinal, item.KeywordOrReference.FormKey, item.KeywordOrReference.IsNull, allRecordKeys, masterStyles, allowedOrigins, "keyword"),
                        })
                        .ToArray();
                    BethesdaLinkFact? location = !structural.HasSupportedField(contribution.Identity.FormKey, "XLRL")
                        ? null
                        : !placed.LocationReference.IsNull
                            ? Link(contribution, "XLRL", 0, placed.LocationReference.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                            : Link(contribution, "XLRL", 0, default, true, allRecordKeys, masterStyles, allowedOrigins);
                    BethesdaLinkFact? owner = !structural.HasSupportedField(contribution.Identity.FormKey, "XOWN")
                        ? null
                        : !placed.Owner.IsNull
                            ? Link(contribution, "XOWN", 0, placed.Owner.FormKey, false, allRecordKeys, masterStyles, allowedOrigins)
                            : Link(contribution, "XOWN", 0, default, true, allRecordKeys, masterStyles, allowedOrigins);
                    BethesdaPlacementFact? placement = !structural.HasSupportedField(contribution.Identity.FormKey, "DATA")
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
                .DistinctBy(item => UnsupportedMemberIdentity(item.SourcePlugin, item.FormKey, item.RecordSignature, item.FieldSignature), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            BethesdaUnsupportedShape[] distinctUnsupportedShapes = unsupportedShapes
                .DistinctBy(item => UnsupportedMemberIdentity(item.SourcePlugin, item.FormKey, item.RecordSignature, item.FieldSignature), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            List<BethesdaCoverageGap> gaps = BuildGaps(request, unsupportedRecords, distinctUnsupportedFields, distinctUnsupportedShapes);
            List<BethesdaFaceGenFact> faceGen = BuildFaceGen(input.Snapshot, npcs, races, gaps);
            gaps = gaps
                .GroupBy(gap => $"{gap.Population}\0{gap.MissingCapability}", StringComparer.Ordinal)
                .Select(group => group.First() with { Denominator = group.Sum(gap => gap.Denominator) })
                .OrderBy(gap => gap.Population, StringComparer.Ordinal)
                .ThenBy(gap => gap.MissingCapability, StringComparer.Ordinal)
                .ToList();
            List<BethesdaTaxonomyProjection> taxonomy = BuildTaxonomy(contributions, npcFacts, referenceFacts, unsupportedRecords, faceGen);
            List<BethesdaCoveragePopulation> coverage = BuildCoverage(plugins, npcFacts, raceFacts, referenceFacts, unsupportedRecords, faceGen, taxonomy, request, input.Snapshot, gaps);
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
                BethesdaSemanticContract.SchemaVersion,
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
                gaps.Count == 0 && coverage.All(row => row.State == CoverageState.Completed)
                    ? BethesdaExtractionState.Completed
                    : BethesdaExtractionState.CompletedWithGaps,
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

    private bool IsSelected(FormKey key) => selectedFormKeys is null
        || selectedFormKeys.Contains(CanonicalFormKey(key));

}
