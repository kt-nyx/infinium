using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Application.Runtime;
using Infinium.Bethesda;
using Infinium.Mo2;

namespace Infinium.Coordinator;

public static class BethesdaSemanticPublicationValidator
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static BethesdaSemanticExtractionResult DeserializeAndValidate(
        ReadOnlySpan<byte> stagedBytes,
        ManagedBethesdaSemanticAssignment assignment,
        long maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (stagedBytes.Length == 0 || stagedBytes.Length > maximumBytes)
        {
            throw new InvalidOperationException(
                "The staged Bethesda semantic result exceeds its publication authority.");
        }

        BethesdaSemanticExtractionResult result =
            JsonSerializer.Deserialize<BethesdaSemanticExtractionResult>(stagedBytes, StrictJson)
            ?? throw new InvalidOperationException(
                "The staged Bethesda semantic result is not valid JSON.");
        Validate(result, assignment);
        return result;
    }

    public static void Validate(
        BethesdaSemanticExtractionResult result,
        ManagedBethesdaSemanticAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(assignment);
        Mo2InstallationSnapshot source = assignment.AcceptedSnapshot.Snapshot
            ?? throw new InvalidOperationException(
                "The assigned accepted snapshot is absent.");
        BethesdaSemanticSnapshot semantic = result.Snapshot
            ?? throw new InvalidOperationException(
                "The worker did not stage a semantic snapshot.");
        IReadOnlyList<ManagedBethesdaPluginSeal> seals = assignment.PluginSeals
            ?? throw new InvalidOperationException(
                "The coordinator-sealed Bethesda input identities are absent.");
        int enabledPluginCount = source.Plugins.Count(plugin => plugin.Enabled);
        BethesdaRecordContribution[] contributions = semantic.OverrideChains.Values
            .SelectMany(chain => chain.Contributions)
            .ToArray();
        HashSet<string> contributionIds = contributions
            .Select(contribution => contribution.ContributionId)
            .ToHashSet(StringComparer.Ordinal);
        BethesdaLinkFact[] expectedReverseLinks = semantic.Links
            .Where(link => link.TargetParticipantId is not null)
            .ToArray();
        if (result.State is not (
                BethesdaExtractionState.Completed
                or BethesdaExtractionState.CompletedWithGaps)
            || result.Failures.Count != 0
            || result.Gaps.Count != semantic.Gaps.Count
            || !result.Gaps.SequenceEqual(semantic.Gaps)
            || semantic.SourceSnapshotId != source.Contract.SnapshotId
            || semantic.SchemaVersion != BethesdaSemanticContract.SchemaVersion
            || semantic.ProducerId != BethesdaSemanticExtractor.ProducerId
            || semantic.ProducerVersion != BethesdaSemanticExtractor.ProducerVersion
            || semantic.Plugins.Count != enabledPluginCount
            || seals.Count != enabledPluginCount
            || semantic.Plugins.Select(plugin => plugin.LoadOrder).Distinct().Count()
                != semantic.Plugins.Count
            || semantic.Plugins.Any(plugin => plugin.Sha256.Value.Length != 64
                || plugin.ByteLength <= 0)
            || semantic.OverrideChains.Any(item =>
                !string.Equals(item.Key, item.Value.Identity.FormKey, StringComparison.OrdinalIgnoreCase)
                || item.Value.Contributions.Count == 0
                || item.Value.Contributions.Any(contribution =>
                    !string.Equals(contribution.Identity.FormKey, item.Key, StringComparison.OrdinalIgnoreCase))
                || !item.Value.Contributions.SequenceEqual(
                    item.Value.Contributions.OrderBy(contribution => contribution.LoadOrder))
                || item.Value.Winner != item.Value.Contributions[^1])
            || contributionIds.Count != contributions.Length
            || semantic.Winners.Count != semantic.OverrideChains.Count
            || semantic.Winners.Any(item =>
                !semantic.OverrideChains.TryGetValue(item.Key, out BethesdaOverrideChain? chain)
                || item.Value != chain.Winner)
            || semantic.Links.Any(link => string.IsNullOrWhiteSpace(link.SourceParticipantId)
                || string.IsNullOrWhiteSpace(link.SourceContributionId)
                || !contributionIds.Contains(link.SourceContributionId)
                || (link.State == BethesdaLinkState.Null && link.TargetFormKey is not null)
                || (link.State == BethesdaLinkState.Resolved
                    && (link.TargetParticipantId is null
                        || !semantic.ResolvedParticipants.Values.Any(participant =>
                            participant.ParticipantId == link.TargetParticipantId))))
            || semantic.ReverseLinks.Count != expectedReverseLinks
                .Select(link => link.TargetParticipantId!)
                .Distinct(StringComparer.Ordinal).Count()
            || semantic.ReverseLinks.Any(item =>
                !item.Value.SequenceEqual(expectedReverseLinks
                    .Where(link => link.TargetParticipantId == item.Key)
                    .OrderBy(link => link.SourceParticipantId, StringComparer.Ordinal)
                    .ThenBy(link => link.Field, StringComparer.Ordinal)
                    .ThenBy(link => link.Ordinal)))
            || !TypedFactsAreBound(semantic.NpcContributions, contributionIds, fact => fact.Contribution)
            || !TypedFactsAreBound(semantic.RaceContributions, contributionIds, fact => fact.Contribution)
            || !TypedFactsAreBound(semantic.PlacedReferenceContributions, contributionIds, fact => fact.Contribution)
            || semantic.AllowlistedFields.Any(field =>
                !contributionIds.Contains(field.ContributionId)
                || field.Field.Length != 4
                || field.Count <= 0)
            || semantic.AllowlistedFields
                .Select(field => $"{field.ContributionId}|{field.Field}")
                .Distinct(StringComparer.Ordinal).Count() != semantic.AllowlistedFields.Count
            || !WinnerFactsAreExact(semantic.Npcs, semantic.NpcContributions, semantic.Winners, fact => fact.Contribution)
            || !WinnerFactsAreExact(semantic.Races, semantic.RaceContributions, semantic.Winners, fact => fact.Contribution)
            || !WinnerFactsAreExact(semantic.PlacedReferences, semantic.PlacedReferenceContributions, semantic.Winners, fact => fact.Contribution)
            || semantic.Gaps.Select(gap => gap.GapId).Distinct(StringComparer.Ordinal).Count() != semantic.Gaps.Count
            || semantic.Gaps.Any(gap => gap.Denominator <= 0)
            || !FaceGenContractIsExact(semantic, source)
            || !GapContractIsExact(semantic.Gaps)
            || !CoverageContractIsExact(semantic, source, assignment.RequestedUnsupportedCapabilities)
            || !TaxonomyContractIsExact(semantic, contributions)
            || (result.State == BethesdaExtractionState.Completed
                && (result.Gaps.Count != 0
                    || semantic.Coverage.Any(row => row.State != Infinium.Domain.Contracts.CoverageState.Completed)))
            || (result.State == BethesdaExtractionState.CompletedWithGaps
                && result.Gaps.Count == 0
                && semantic.Coverage.All(row => row.State == Infinium.Domain.Contracts.CoverageState.Completed)))
        {
            throw new InvalidOperationException(
                "The staged Bethesda semantic result fails coordinator publication validation.");
        }

        foreach (BethesdaPluginReceipt receipt in semantic.Plugins)
        {
            PluginState sourcePlugin = source.Plugins.Single(plugin =>
                plugin.Enabled
                && plugin.LoadOrder == receipt.LoadOrder
                && string.Equals(plugin.Name, receipt.PluginName, StringComparison.OrdinalIgnoreCase));
            LooseProviderChain chain = source.LooseProviderChains.Single(candidate =>
                string.Equals(candidate.NormalizedRelativePath, sourcePlugin.Name, StringComparison.OrdinalIgnoreCase));
            ManagedBethesdaPluginSeal seal = seals.Single(candidate =>
                candidate.LoadOrder == receipt.LoadOrder
                && string.Equals(candidate.PluginName, receipt.PluginName, StringComparison.OrdinalIgnoreCase));
            if (sourcePlugin.WinningLocalInstalledEntityId != receipt.LocalInstalledEntityId
                || chain.Winner.LocalInstalledEntityId != receipt.LocalInstalledEntityId
                || !string.Equals(
                    Path.GetFullPath(chain.Winner.PhysicalPath),
                    Path.GetFullPath(receipt.SnapshotAuthorizedPath),
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFullPath(seal.SnapshotAuthorizedPath), Path.GetFullPath(receipt.SnapshotAuthorizedPath), StringComparison.OrdinalIgnoreCase)
                || seal.ByteLength != receipt.ByteLength
                || !string.Equals(seal.Sha256, receipt.Sha256.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A staged Bethesda plugin receipt is not bound to its assigned snapshot winner.");
            }
        }

        if (semantic.DependencyFingerprint != BethesdaSemanticExtractor.ComputeDependencyFingerprint(
                semantic.SourceSnapshotId,
                semantic.Plugins,
                assignment.RequestedUnsupportedCapabilities))
        {
            throw new InvalidOperationException(
                "The staged Bethesda dependency fingerprint is inconsistent.");
        }
    }

    private static bool TypedFactsAreBound<T>(
        IReadOnlyList<T> facts,
        HashSet<string> contributionIds,
        Func<T, BethesdaRecordContribution> contribution) =>
        facts.Select(fact => contribution(fact).ContributionId)
            .Distinct(StringComparer.Ordinal).Count() == facts.Count
        && facts.All(fact => contributionIds.Contains(contribution(fact).ContributionId));

    private static bool FaceGenContractIsExact(
        BethesdaSemanticSnapshot semantic,
        Mo2InstallationSnapshot source)
    {
        string[] expectedNpcParticipants = semantic.Npcs.Values
            .Select(npc => npc.Contribution.Identity.ParticipantId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string[] actualNpcParticipants = semantic.FaceGen
            .Select(fact => fact.NpcParticipantId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (actualNpcParticipants.Distinct(StringComparer.Ordinal).Count() != semantic.FaceGen.Count
            || !actualNpcParticipants.SequenceEqual(expectedNpcParticipants, StringComparer.Ordinal))
        {
            return false;
        }

        foreach (BethesdaNpcFact npc in semantic.NpcContributions)
        {
            if (!Enum.IsDefined(npc.TemplateTraitsDecision)
                || npc.TemplatesTraits != (npc.TemplateTraitsDecision == BethesdaTemplateTraitsDecision.KnownInherited))
            {
                return false;
            }
        }

        foreach (BethesdaRaceFact race in semantic.RaceContributions)
        {
            if (!Enum.IsDefined(race.FaceGenHeadDecision)
                || race.FaceGenHead != (race.FaceGenHeadDecision == BethesdaRaceFaceGenHeadDecision.KnownPresent))
            {
                return false;
            }
        }

        foreach (BethesdaFaceGenFact fact in semantic.FaceGen)
        {
            BethesdaNpcFact? npc = semantic.Npcs.Values.SingleOrDefault(candidate =>
                candidate.Contribution.Identity.ParticipantId == fact.NpcParticipantId);
            if (npc is null)
            {
                return false;
            }

            BethesdaRaceFact? race = npc.Race?.TargetFormKey is not null
                && semantic.Races.TryGetValue(npc.Race.TargetFormKey, out BethesdaRaceFact? resolved)
                    ? resolved
                    : null;
            if (!Enum.IsDefined(fact.Applicability)
                || fact.Applicability != BethesdaSemanticExtractor.DetermineFaceGenApplicability(
                    npc.Contribution.Deleted,
                    npc.Race?.State,
                    race?.FaceGenHeadDecision,
                    npc.UsesTemplate,
                    npc.TemplateTraitsDecision)
                || !AssetContractIsExact(fact.Mesh, source)
                || !AssetContractIsExact(fact.Tint, source))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AssetContractIsExact(
        BethesdaLooseAssetFact asset,
        Mo2InstallationSnapshot source)
    {
        bool booleansExact;
        try
        {
            booleansExact = (asset.Present, asset.ExactAbsenceKnown)
                == BethesdaSemanticContract.AssetTransport(asset.Availability);
        }
        catch (ArgumentOutOfRangeException)
        {
            booleansExact = false;
        }
        LooseProviderChain? sourceChain = source.LooseProviderChains.SingleOrDefault(candidate =>
            string.Equals(
                candidate.NormalizedRelativePath.Replace('\\', '/'),
                asset.NormalizedRelativePath,
                StringComparison.OrdinalIgnoreCase));
        bool sourceExact = sourceChain is null
            ? asset.Availability == BethesdaAssetAvailability.Unknown
                && asset.ProviderParticipantIds.Count == 0
            : asset.Availability == BethesdaAssetAvailability.Present
                && asset.ProviderParticipantIds.SequenceEqual(
                    sourceChain.Providers.Select(provider => provider.LocalInstalledEntityId.Value),
                    StringComparer.Ordinal)
                && asset.WinnerParticipantId == sourceChain.Winner.LocalInstalledEntityId.Value;
        return booleansExact
            && sourceExact
            && asset.ProviderParticipantIds.Distinct(StringComparer.Ordinal).Count() == asset.ProviderParticipantIds.Count
            && (asset.Availability == BethesdaAssetAvailability.Present
                ? asset.WinnerParticipantId is not null
                    && asset.ProviderParticipantIds.Contains(asset.WinnerParticipantId, StringComparer.Ordinal)
                : asset.WinnerParticipantId is null)
            && !string.IsNullOrWhiteSpace(asset.NormalizedRelativePath)
            && string.Equals(
                asset.NormalizedRelativePath,
                asset.NormalizedRelativePath.Replace('\\', '/'),
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(asset.NormalizedRelativePath, asset.NormalizedRelativePath.ToLowerInvariant(), StringComparison.Ordinal);
    }

    private static bool GapContractIsExact(IReadOnlyList<BethesdaCoverageGap> gaps)
    {
        if (gaps.Select(gap => $"{gap.Population}|{gap.MissingCapability}")
            .Distinct(StringComparer.Ordinal).Count() != gaps.Count)
        {
            return false;
        }

        return gaps.All(gap =>
        {
            string material = $"{gap.Category}|{gap.Detail}|{gap.Population}|{gap.MissingCapability}".ToLowerInvariant();
            string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..20];
            if (gap.GapId != $"gap:infinium-bethesda:{digest}"
                || string.IsNullOrWhiteSpace(gap.Detail)
                || !string.Equals(gap.Detail, gap.Detail.ToLowerInvariant(), StringComparison.Ordinal))
            {
                return false;
            }

            return gap.Category switch
            {
                BethesdaCoverageGapCategory.UnsupportedRecord =>
                    gap.Population == $"unsupported-records:{gap.Detail}"
                    && gap.MissingCapability == "allowlisted-record-family-semantics",
                BethesdaCoverageGapCategory.UnsupportedField =>
                    gap.Population == $"unsupported-fields:{gap.Detail}"
                    && gap.MissingCapability == "allowlisted-record-field-semantics",
                BethesdaCoverageGapCategory.UnsupportedShape =>
                    gap.Population == $"unsupported-shapes:{gap.Detail}"
                    && gap.MissingCapability == "allowlisted-record-shape-semantics",
                BethesdaCoverageGapCategory.Capability => (gap.Detail, gap.Population, gap.MissingCapability) is
                    ("localized-strings", "localized-strings", "localized-string-resolution")
                    or ("face-gen-archive-assets", "face-gen-archive-assets", "archive-activation-and-member-precedence")
                    or ("automatic-environment-discovery", "automatic-environment-discovery", "automatic-environment-discovery"),
                BethesdaCoverageGapCategory.FaceGenApplicability => (gap.Detail, gap.Population, gap.MissingCapability) is
                    ("template", "face-gen-applicability:template", "complete-template-traits-decision")
                    or ("race", "face-gen-applicability:race", "resolved-winning-race"),
                _ => false,
            };
        });
    }

    private static bool CoverageContractIsExact(
        BethesdaSemanticSnapshot semantic,
        Mo2InstallationSnapshot source,
        IReadOnlyList<BethesdaUnsupportedCapability> requestedCapabilities)
    {
        if (!semantic.Coverage.Select(row => row.Population)
            .SequenceEqual(BethesdaSemanticContract.CoveragePopulations, StringComparer.Ordinal))
        {
            return false;
        }

        BethesdaLooseAssetFact[] loose = semantic.FaceGen
            .Where(fact => fact.Applicability == BethesdaFaceGenApplicability.Applicable)
            .SelectMany(fact => new[] { fact.Mesh, fact.Tint })
            .ToArray();
        long archiveDenominator = loose.LongCount(asset => asset.Availability != BethesdaAssetAvailability.Present);
        long taxonomySubjects = semantic.Taxonomy.Select(item => item.SubjectParticipantId).Distinct(StringComparer.Ordinal).LongCount();
        Dictionary<string, (long Denominator, long Completed)> expectedCounts = new(StringComparer.Ordinal)
        {
            ["plugins"] = (semantic.Plugins.Count, semantic.Plugins.Count),
            ["npc-records"] = (semantic.NpcContributions.Count, semantic.NpcContributions.Count),
            ["race-records"] = (semantic.RaceContributions.Count, semantic.RaceContributions.Count),
            ["placed-reference-records"] = (semantic.PlacedReferenceContributions.Count, semantic.PlacedReferenceContributions.Count),
            ["unsupported-records"] = (semantic.Taxonomy.Where(item => item.SubjectType == "unsupported-record").Select(item => item.SubjectParticipantId).Distinct(StringComparer.Ordinal).LongCount(), semantic.Taxonomy.Where(item => item.SubjectType == "unsupported-record").Select(item => item.SubjectParticipantId).Distinct(StringComparer.Ordinal).LongCount()),
            ["face-gen-loose-assets"] = (loose.LongLength, loose.LongCount(asset => asset.Availability != BethesdaAssetAvailability.Unknown)),
            ["face-gen-archive-assets"] = (archiveDenominator, source.ArchiveMemberPopulationSupported ? archiveDenominator : 0),
            ["localized-strings"] = (requestedCapabilities.Contains(BethesdaUnsupportedCapability.LocalizedStringResolution) ? 1 : 0, 0),
            ["automatic-environment-discovery"] = (requestedCapabilities.Contains(BethesdaUnsupportedCapability.AutomaticEnvironmentDiscovery) ? 1 : 0, 0),
            ["taxonomy-subjects"] = (taxonomySubjects, taxonomySubjects),
        };
        Dictionary<string, string> expectedLabels = new(StringComparer.Ordinal)
        {
            ["plugins"] = "enabled-snapshot-plugins",
            ["npc-records"] = "npc-record-contributions",
            ["race-records"] = "race-record-contributions",
            ["placed-reference-records"] = "placed-reference-record-contributions",
            ["unsupported-records"] = "unsupported-record-contributions",
            ["face-gen-loose-assets"] = "applicable-face-gen-loose-paths",
            ["face-gen-archive-assets"] = "applicable-face-gen-paths-without-loose-winner",
            ["localized-strings"] = "requested-localized-string-resolution",
            ["automatic-environment-discovery"] = "requested-automatic-environment-discovery",
            ["taxonomy-subjects"] = "distinct-taxonomy-subjects",
        };
        HashSet<string> gapIds = semantic.Gaps.Select(gap => gap.GapId).ToHashSet(StringComparer.Ordinal);
        HashSet<string> joinedGapIds = semantic.Coverage.SelectMany(row => row.GapIds).ToHashSet(StringComparer.Ordinal);
        return joinedGapIds.SetEquals(gapIds) && semantic.Coverage.All(row =>
        {
            if (!expectedCounts.TryGetValue(row.Population, out (long Denominator, long Completed) expectedCountsForRow)
                || row.DenominatorLabel != expectedLabels[row.Population]
                || row.Denominator != expectedCountsForRow.Denominator
                || row.Completed != expectedCountsForRow.Completed
                || row.Completed > row.Denominator
                || row.GapIds.Distinct(StringComparer.Ordinal).Count() != row.GapIds.Count
                || row.GapIds.Any(id => !gapIds.Contains(id))
                || !row.GapIds.SequenceEqual(
                    semantic.Gaps.Where(gap => GapBelongsToCoverage(gap, row.Population))
                        .Select(gap => gap.GapId)
                        .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal))
            {
                return false;
            }

            Infinium.Domain.Contracts.CoverageState expected = row.Denominator == row.Completed
                && row.GapIds.Count == 0
                    ? Infinium.Domain.Contracts.CoverageState.Completed
                    : row.Completed == 0 && row.Denominator > 0
                        ? Infinium.Domain.Contracts.CoverageState.Unsupported
                        : Infinium.Domain.Contracts.CoverageState.CompletedWithGaps;
            return row.State == expected;
        });
    }

    private static bool GapBelongsToCoverage(BethesdaCoverageGap gap, string population) => population switch
    {
        "npc-records" => gap.Category == BethesdaCoverageGapCategory.FaceGenApplicability
            || gap.Category is BethesdaCoverageGapCategory.UnsupportedField or BethesdaCoverageGapCategory.UnsupportedShape
                && gap.Detail.StartsWith("npc_:", StringComparison.Ordinal),
        "race-records" => gap.Category is BethesdaCoverageGapCategory.UnsupportedField or BethesdaCoverageGapCategory.UnsupportedShape
            && gap.Detail.StartsWith("race:", StringComparison.Ordinal),
        "placed-reference-records" => gap.Category is BethesdaCoverageGapCategory.UnsupportedField or BethesdaCoverageGapCategory.UnsupportedShape
            && gap.Detail.StartsWith("refr:", StringComparison.Ordinal),
        "unsupported-records" => gap.Category == BethesdaCoverageGapCategory.UnsupportedRecord,
        _ => string.Equals(gap.Population, population, StringComparison.Ordinal),
    };

    private static bool TaxonomyContractIsExact(
        BethesdaSemanticSnapshot semantic,
        IReadOnlyList<BethesdaRecordContribution> contributions)
    {
        int distinctTuples = semantic.Taxonomy.Select(item => string.Join('|',
                item.SubjectParticipantId,
                item.SubjectType,
                item.Axis,
                item.Facet,
                item.Code ?? string.Empty,
                item.Applicability,
                item.Role))
            .Distinct(StringComparer.Ordinal).Count();
        if (distinctTuples != semantic.Taxonomy.Count
            || semantic.Taxonomy.Select(item => item.AssignmentId).Distinct(StringComparer.Ordinal).Count()
                != semantic.Taxonomy.Count
            || semantic.Taxonomy.Any(item => item.TaxonomyId != BethesdaSemanticExtractor.TaxonomyId
                || item.TaxonomyVersion != BethesdaSemanticExtractor.TaxonomyVersion
                || !Enum.IsDefined(item.Applicability)
                || !Enum.IsDefined(item.Role)
                || item.EvidenceFields.Count == 0
                || item.AnalyzerOrAdjudicatorId != "analyzer:infinium-bethesda-m1-semantic-index"
                || item.SubjectType is not ("record-contribution" or "record-semantic-subject" or "unsupported-record")))
        {
            return false;
        }

        HashSet<string> supportedSignatures =
        [
            "NPC_", "RACE", "REFR", "CELL", "CLAS", "PACK", "CLFM", "FACT", "HDPT", "KYWD", "LCTN", "STAT",
        ];
        HashSet<string> expectedContributionSubjects = contributions
            .Where(contribution => supportedSignatures.Contains(contribution.Identity.Signature))
            .Select(contribution => contribution.ContributionId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> actualContributionSubjects = semantic.Taxonomy
            .Where(item => item.SubjectType == "record-contribution")
            .Select(item => item.SubjectParticipantId)
            .ToHashSet(StringComparer.Ordinal);
        if (!actualContributionSubjects.SetEquals(expectedContributionSubjects))
        {
            return false;
        }

        foreach (string subject in expectedContributionSubjects)
        {
            BethesdaTaxonomyProjection[] core = semantic.Taxonomy
                .Where(item => item.SubjectParticipantId == subject
                    && item.SubjectType == "record-contribution")
                .ToArray();
            if (core.Length != 2
                || core.Count(item => item.Axis == "technical-modification-surface"
                    && item.Facet == "semantic-mechanism"
                    && item.Code == "surface.plugin-data"
                    && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Assigned
                    && item.Role == Infinium.Domain.Contracts.ClassificationRole.Observed) != 1
                || core.Count(item => item.Axis == "technical-modification-surface"
                    && item.Facet == "realization-and-delivery"
                    && item.Code == "delivery.plugin-container"
                    && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Assigned
                    && item.Role == Infinium.Domain.Contracts.ClassificationRole.Observed) != 1)
            {
                return false;
            }
        }

        HashSet<string> actualUnsupportedSubjects = semantic.Taxonomy
            .Where(item => item.SubjectType == "unsupported-record")
            .Select(item => item.SubjectParticipantId)
            .ToHashSet(StringComparer.Ordinal);
        long expectedUnsupportedSubjectCount = semantic.Coverage
            .Single(row => row.Population == "unsupported-records")
            .Denominator;
        if (actualUnsupportedSubjects.Count != expectedUnsupportedSubjectCount
            || actualUnsupportedSubjects.Any(subject => !subject.StartsWith("unsupported-record:", StringComparison.Ordinal)))
        {
            return false;
        }

        foreach (string subject in actualUnsupportedSubjects)
        {
            BethesdaTaxonomyProjection[] core = semantic.Taxonomy
                .Where(item => item.SubjectParticipantId == subject
                    && item.SubjectType == "unsupported-record")
                .ToArray();
            if (core.Length != 4
                || core.Count(item => item.Axis == "technical-modification-surface"
                    && item.Facet == "semantic-mechanism"
                    && item.Code == "surface.plugin-data"
                    && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Assigned
                    && item.Role == Infinium.Domain.Contracts.ClassificationRole.Observed) != 1
                || core.Count(item => item.Axis == "technical-modification-surface"
                    && item.Facet == "realization-and-delivery"
                    && item.Code == "delivery.plugin-container"
                    && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Assigned
                    && item.Role == Infinium.Domain.Contracts.ClassificationRole.Observed) != 1
                || core.Count(item => item.Axis == "affected-game-system-or-content-area"
                    && item.Facet == "affected-area"
                    && item.Code is null
                    && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Unsupported
                    && item.Role == Infinium.Domain.Contracts.ClassificationRole.Established) != 1
                || core.Count(item => item.Axis == "consequence-type"
                    && item.Facet == "consequence-type"
                    && item.Code is null
                    && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Unknown
                    && item.Role == Infinium.Domain.Contracts.ClassificationRole.Predicted) != 1)
            {
                return false;
            }
        }

        Dictionary<string, string> expectedAreaSubjects = new(StringComparer.Ordinal);
        foreach (BethesdaNpcFact npc in semantic.NpcContributions)
        {
            if (npc.AiData is not null || npc.Packages.Any(link => link.State == BethesdaLinkState.Resolved))
            {
                expectedAreaSubjects.Add(
                    $"{npc.Contribution.ContributionId}:semantic:area.actors.ai-packages",
                    "area.actors.ai-packages");
            }
            if (npc.Race?.State == BethesdaLinkState.Resolved)
            {
                expectedAreaSubjects.Add(
                    $"{npc.Contribution.ContributionId}:semantic:area.actors.appearance-identity",
                    "area.actors.appearance-identity");
            }
        }
        foreach (BethesdaPlacedReferenceFact reference in semantic.PlacedReferenceContributions)
        {
            bool hasResolvedRelation = new[] { reference.Base, reference.LocationReference, reference.Owner }
                .Any(link => link?.State == BethesdaLinkState.Resolved)
                || reference.LinkedReferences.Any(link => link.State == BethesdaLinkState.Resolved);
            if (reference.Placement is not null || hasResolvedRelation)
            {
                expectedAreaSubjects.Add(
                    $"{reference.Contribution.ContributionId}:semantic:area.world.placed-objects-activation",
                    "area.world.placed-objects-activation");
            }
        }

        foreach ((string subject, string code) in expectedAreaSubjects)
        {
            BethesdaTaxonomyProjection[] projections = semantic.Taxonomy
                .Where(item => item.SubjectParticipantId == subject
                    && item.SubjectType == "record-semantic-subject")
                .ToArray();
            if (projections.Length != 2
                || projections.Count(item => item.Axis == "technical-modification-surface"
                    && item.Facet == "semantic-mechanism"
                    && item.Code == "surface.plugin-data"
                    && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Assigned
                    && item.Role == Infinium.Domain.Contracts.ClassificationRole.Observed) != 1
                || projections.Count(item => item.Axis == "affected-game-system-or-content-area"
                    && item.Facet == "affected-area"
                    && item.Code == code
                    && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Assigned
                    && item.Role == Infinium.Domain.Contracts.ClassificationRole.Established) != 1)
            {
                return false;
            }
        }

        HashSet<string> expectedFaceGenSubjects = [];
        foreach (BethesdaFaceGenFact fact in semantic.FaceGen)
        {
            BethesdaNpcFact? npc = semantic.Npcs.Values.SingleOrDefault(candidate =>
                candidate.Contribution.Identity.ParticipantId == fact.NpcParticipantId);
            if (npc is null)
            {
                return false;
            }

            if (fact.Mesh.ProviderParticipantIds.Count > 0)
            {
                expectedFaceGenSubjects.Add($"{npc.Contribution.ContributionId}:semantic:face-gen-loose-provider-chain:{fact.Mesh.NormalizedRelativePath}");
            }
            if (fact.Tint.ProviderParticipantIds.Count > 0)
            {
                expectedFaceGenSubjects.Add($"{npc.Contribution.ContributionId}:semantic:face-gen-loose-provider-chain:{fact.Tint.NormalizedRelativePath}");
            }
        }

        HashSet<string> expectedSemanticSubjects = expectedAreaSubjects.Keys
            .Concat(expectedFaceGenSubjects)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> actualSemanticSubjects = semantic.Taxonomy
            .Where(item => item.SubjectType == "record-semantic-subject")
            .Select(item => item.SubjectParticipantId)
            .ToHashSet(StringComparer.Ordinal);
        return actualSemanticSubjects.SetEquals(expectedSemanticSubjects)
            && expectedFaceGenSubjects.All(subject =>
            {
                BethesdaTaxonomyProjection[] projections = semantic.Taxonomy
                    .Where(item => item.SubjectParticipantId == subject
                        && item.SubjectType == "record-semantic-subject")
                    .ToArray();
                return projections.Length == 2
                    && projections.Count(item => item.Axis == "technical-modification-surface"
                        && item.Facet == "semantic-mechanism"
                        && item.Code == "surface.asset"
                        && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Assigned
                        && item.Role == Infinium.Domain.Contracts.ClassificationRole.Observed) == 1
                    && projections.Count(item => item.Axis == "technical-modification-surface"
                        && item.Facet == "realization-and-delivery"
                        && item.Code == "delivery.loose-data-file"
                        && item.Applicability == Infinium.Domain.Contracts.TaxonomyApplicability.Assigned
                        && item.Role == Infinium.Domain.Contracts.ClassificationRole.Observed) == 1;
            });
    }

    private static bool WinnerFactsAreExact<T>(
        IReadOnlyDictionary<string, T> winners,
        IReadOnlyList<T> facts,
        IReadOnlyDictionary<string, BethesdaRecordContribution> recordWinners,
        Func<T, BethesdaRecordContribution> contribution)
    {
        Dictionary<string, string> expected = facts
            .Where(fact => recordWinners.TryGetValue(
                contribution(fact).Identity.FormKey,
                out BethesdaRecordContribution? winner)
                && winner.ContributionId == contribution(fact).ContributionId)
            .ToDictionary(
                fact => contribution(fact).Identity.FormKey,
                fact => contribution(fact).ContributionId,
                StringComparer.OrdinalIgnoreCase);
        return winners.Count == expected.Count
            && winners.All(item => expected.TryGetValue(item.Key, out string? value)
                && contribution(item.Value).ContributionId == value);
    }
}
