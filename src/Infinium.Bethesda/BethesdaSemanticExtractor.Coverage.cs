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
    private static List<BethesdaCoverageGap> BuildGaps(
        BethesdaSemanticRequest request,
        IReadOnlyList<BethesdaUnsupportedRecord> unsupportedRecords,
        IReadOnlyList<BethesdaUnsupportedField> unsupportedFields,
        IReadOnlyList<BethesdaUnsupportedShape> unsupportedShapes)
    {
        List<BethesdaCoverageGap> gaps = [];
        foreach (IGrouping<string, BethesdaUnsupportedRecord> group in unsupportedRecords
                     .GroupBy(record => record.Signature.ToLowerInvariant(), StringComparer.Ordinal))
        {
            gaps.Add(Gap(
                BethesdaCoverageGapCategory.UnsupportedRecord,
                group.Key,
                $"unsupported-records:{group.Key}",
                group.Count(),
                "The record family is outside the positive semantic allowlist.",
                "allowlisted-record-family-semantics"));
        }

        foreach (IGrouping<string, BethesdaUnsupportedField> group in unsupportedFields
                     .GroupBy(field => $"{field.RecordSignature.ToLowerInvariant()}:{field.FieldSignature.ToLowerInvariant()}", StringComparer.Ordinal))
        {
            gaps.Add(Gap(
                BethesdaCoverageGapCategory.UnsupportedField,
                group.Key,
                $"unsupported-fields:{group.Key}",
                group.Count(),
                "The record field is outside the positive semantic allowlist.",
                "allowlisted-record-field-semantics"));
        }

        foreach (IGrouping<string, BethesdaUnsupportedShape> group in unsupportedShapes
                     .GroupBy(shape => $"{shape.RecordSignature.ToLowerInvariant()}:{shape.FieldSignature.ToLowerInvariant()}", StringComparer.Ordinal))
        {
            gaps.Add(Gap(
                BethesdaCoverageGapCategory.UnsupportedShape,
                group.Key,
                $"unsupported-shapes:{group.Key}",
                group.Count(),
                "The record field shape is outside the accepted semantic shape.",
                "allowlisted-record-shape-semantics"));
        }

        foreach (BethesdaUnsupportedCapability capability in
                 (request.RequestedUnsupportedCapabilities ?? []).Distinct())
        {
            (string Detail, string Population, string Missing) mapping = capability switch
            {
                BethesdaUnsupportedCapability.LocalizedStringResolution => ("localized-strings", "localized-strings", "localized-string-resolution"),
                BethesdaUnsupportedCapability.ArchiveMemberRead => ("face-gen-archive-assets", "face-gen-archive-assets", "archive-activation-and-member-precedence"),
                BethesdaUnsupportedCapability.AutomaticEnvironmentDiscovery => ("automatic-environment-discovery", "automatic-environment-discovery", "automatic-environment-discovery"),
                _ => throw Input("unsupported-capability-invalid", capability.ToString(), "The requested unsupported capability is outside the closed semantic capability vocabulary."),
            };
            if (capability != BethesdaUnsupportedCapability.ArchiveMemberRead)
            {
                gaps.Add(Gap(
                    BethesdaCoverageGapCategory.Capability,
                    mapping.Detail,
                    mapping.Population,
                    1,
                    $"Capability '{mapping.Detail}' is outside the Bethesda semantic allowlist.",
                    mapping.Missing));
            }
        }

        return gaps;
    }

    private static BethesdaCoverageGap Gap(
        BethesdaCoverageGapCategory category,
        string detail,
        string population,
        long denominator,
        string reason,
        string missing)
    {
        string material = $"{category}|{detail}|{population}|{missing}".ToLowerInvariant();
        string digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(material)))[..20];
        return new BethesdaCoverageGap(
            $"gap:infinium-bethesda:{digest}",
            category,
            detail,
            population,
            denominator,
            reason,
            missing);
    }

    private static List<BethesdaCoveragePopulation> BuildCoverage(
        IReadOnlyList<SealedPlugin> plugins,
        IReadOnlyList<BethesdaNpcFact> npcs,
        IReadOnlyList<BethesdaRaceFact> races,
        IReadOnlyList<BethesdaPlacedReferenceFact> references,
        IReadOnlyList<BethesdaUnsupportedRecord> unsupportedRecords,
        IReadOnlyList<BethesdaFaceGenFact> faceGen,
        IReadOnlyList<BethesdaTaxonomyProjection> taxonomy,
        BethesdaSemanticRequest request,
        Mo2InstallationSnapshot source,
        IReadOnlyList<BethesdaCoverageGap> gaps)
    {
        BethesdaFaceGenFact[] applicable = faceGen
            .Where(fact => fact.Applicability == BethesdaFaceGenApplicability.Applicable)
            .ToArray();
        BethesdaLooseAssetFact[] loose = applicable
            .SelectMany(fact => new[] { fact.Mesh, fact.Tint })
            .ToArray();
        BethesdaLooseAssetFact[] archiveCandidates = loose
            .Where(asset => asset.Availability != BethesdaAssetAvailability.Present)
            .ToArray();
        bool localizedRequested = (request.RequestedUnsupportedCapabilities ?? [])
            .Contains(BethesdaUnsupportedCapability.LocalizedStringResolution);
        bool discoveryRequested = (request.RequestedUnsupportedCapabilities ?? [])
            .Contains(BethesdaUnsupportedCapability.AutomaticEnvironmentDiscovery);
        long taxonomySubjects = taxonomy.Select(item => item.SubjectParticipantId).Distinct(StringComparer.Ordinal).LongCount();

        List<BethesdaCoveragePopulation> coverage =
        [
            Population("plugins", "enabled-snapshot-plugins", plugins.Count, plugins.Count, gaps),
            Population("npc-records", "npc-record-contributions", npcs.Count, npcs.Count, gaps),
            Population("race-records", "race-record-contributions", races.Count, races.Count, gaps),
            Population("placed-reference-records", "placed-reference-record-contributions", references.Count, references.Count, gaps),
            Population("unsupported-records", "unsupported-record-contributions", unsupportedRecords.Count, unsupportedRecords.Count, gaps),
            Population("face-gen-loose-assets", "applicable-face-gen-loose-paths", loose.LongLength, loose.LongCount(asset => asset.Availability != BethesdaAssetAvailability.Unknown), gaps),
            Population("face-gen-archive-assets", "applicable-face-gen-paths-without-loose-winner", archiveCandidates.LongLength, source.ArchiveMemberPopulationSupported ? archiveCandidates.LongLength : 0, gaps),
            Population("localized-strings", "requested-localized-string-resolution", localizedRequested ? 1 : 0, 0, gaps),
            Population("automatic-environment-discovery", "requested-automatic-environment-discovery", discoveryRequested ? 1 : 0, 0, gaps),
            Population("taxonomy-subjects", "distinct-taxonomy-subjects", taxonomySubjects, taxonomySubjects, gaps),
        ];

        return coverage;
    }

    private static BethesdaCoveragePopulation Population(
        string population,
        string denominatorLabel,
        long denominator,
        long completed,
        IReadOnlyList<BethesdaCoverageGap> gaps)
    {
        string[] gapIds = gaps
            .Where(gap => GapBelongsToPopulation(gap, population))
            .Select(gap => gap.GapId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        CoverageState state;
        if (denominator == 0 && gapIds.Length == 0)
        {
            state = CoverageState.Completed;
        }
        else if (completed == denominator && gapIds.Length == 0)
        {
            state = CoverageState.Completed;
        }
        else if (completed == 0 && denominator > 0)
        {
            state = CoverageState.Unsupported;
        }
        else
        {
            state = CoverageState.CompletedWithGaps;
        }

        return new BethesdaCoveragePopulation(population, denominatorLabel, denominator, completed, state, gapIds);
    }

    private static bool GapBelongsToPopulation(BethesdaCoverageGap gap, string population) => population switch
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

}
