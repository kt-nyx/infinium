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
                race?.FaceGenHeadDecision,
                npc.UsesTemplate,
                npc.TemplateTraitsDecision);
            (string reason, string? gapKey, string? missing) = applicability switch
            {
                BethesdaFaceGenApplicability.NotApplicableDeletedWinner => ("The winning NPC contribution is deleted.", null, null),
                BethesdaFaceGenApplicability.UnknownRace => ("The winning NPC race or its FaceGenHead decision is unknown.", "race", "resolved-winning-race"),
                BethesdaFaceGenApplicability.NotApplicableRaceWithoutFaceGenHead => ("The resolved race does not carry FaceGenHead.", null, null),
                BethesdaFaceGenApplicability.UnknownTemplateTraitsDecision => ("The template-traits inheritance decision is unknown.", "template", "complete-template-traits-decision"),
                BethesdaFaceGenApplicability.NotApplicableTemplateTraits => ("The NPC inherits template traits and does not own FaceGen assets.", null, null),
                BethesdaFaceGenApplicability.Applicable => ("The qualified loose-only FaceGen applicability predicates are satisfied.", null, null),
                _ => throw new InvalidOperationException("The FaceGen applicability state is outside the closed semantic vocabulary."),
            };
            if (gapKey is not null)
            {
                gaps.Add(Gap(
                    BethesdaCoverageGapCategory.FaceGenApplicability,
                    gapKey,
                    $"face-gen-applicability:{gapKey}",
                    1,
                    reason,
                    missing!));
            }

            string origin = npc.Contribution.Identity.OriginPlugin;
            string id = npc.Contribution.Identity.OriginLocalId.ToString("X8", CultureInfo.InvariantCulture);
            BethesdaLooseAssetFact mesh = LooseAsset(snapshot, $"meshes/actors/character/facegendata/facegeom/{origin}/{id}.nif");
            BethesdaLooseAssetFact tint = LooseAsset(snapshot, $"textures/actors/character/facegendata/facetint/{origin}/{id}.dds");
            if (applicability == BethesdaFaceGenApplicability.Applicable
                && !snapshot.ArchiveMemberPopulationSupported
                && (mesh.Availability != BethesdaAssetAvailability.Present
                    || tint.Availability != BethesdaAssetAvailability.Present))
            {
                long missingPaths = new[] { mesh, tint }
                    .LongCount(asset => asset.Availability != BethesdaAssetAvailability.Present);
                gaps.Add(Gap(
                    BethesdaCoverageGapCategory.Capability,
                    "face-gen-archive-assets",
                    "face-gen-archive-assets",
                    missingPaths,
                    "Archive participation and member precedence are unsupported for applicable paths without a loose winner.",
                    "archive-activation-and-member-precedence"));
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

    public static BethesdaFaceGenApplicability DetermineFaceGenApplicability(
        bool deleted,
        BethesdaLinkState? raceState,
        BethesdaRaceFaceGenHeadDecision? raceFaceGenHead,
        bool usesTemplate,
        BethesdaTemplateTraitsDecision templateTraitsDecision)
    {
        if (deleted)
        {
            return BethesdaFaceGenApplicability.NotApplicableDeletedWinner;
        }

        if (templateTraitsDecision == BethesdaTemplateTraitsDecision.Unknown)
        {
            return BethesdaFaceGenApplicability.UnknownTemplateTraitsDecision;
        }

        if (templateTraitsDecision == BethesdaTemplateTraitsDecision.KnownInherited)
        {
            return BethesdaFaceGenApplicability.NotApplicableTemplateTraits;
        }

        if (raceState != BethesdaLinkState.Resolved
            || raceFaceGenHead is null
            || raceFaceGenHead == BethesdaRaceFaceGenHeadDecision.Unknown)
        {
            return BethesdaFaceGenApplicability.UnknownRace;
        }

        return raceFaceGenHead == BethesdaRaceFaceGenHeadDecision.KnownAbsent
            ? BethesdaFaceGenApplicability.NotApplicableRaceWithoutFaceGenHead
            : BethesdaFaceGenApplicability.Applicable;
    }

    internal static string UnsupportedMemberIdentity(
        string sourcePlugin,
        string formKey,
        string recordSignature,
        string fieldSignature) =>
        $"{sourcePlugin}|{formKey}|{recordSignature}|{fieldSignature}";

    private static BethesdaLooseAssetFact LooseAsset(Mo2InstallationSnapshot snapshot, string path)
    {
        string normalized = path.Replace('\\', '/').ToLowerInvariant();
        LooseProviderChain? chain = snapshot.LooseProviderChains.SingleOrDefault(candidate =>
            string.Equals(candidate.NormalizedRelativePath.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        if (chain is null)
        {
            (bool present, bool exactAbsenceKnown) = BethesdaSemanticContract.AssetTransport(
                BethesdaAssetAvailability.Unknown);
            return new BethesdaLooseAssetFact(
                normalized,
                [],
                null,
                BethesdaAssetAvailability.Unknown,
                present,
                exactAbsenceKnown);
        }

        (bool chainPresent, bool chainExactAbsenceKnown) = BethesdaSemanticContract.AssetTransport(
            BethesdaAssetAvailability.Present);
        return new BethesdaLooseAssetFact(
            normalized,
            chain.Providers.Select(provider => provider.LocalInstalledEntityId.Value).ToArray(),
            chain.Winner.LocalInstalledEntityId.Value,
            BethesdaAssetAvailability.Present,
            chainPresent,
            chainExactAbsenceKnown);
    }

    private static List<BethesdaTaxonomyProjection> BuildTaxonomy(
        IReadOnlyList<BethesdaRecordContribution> contributions,
        IReadOnlyList<BethesdaNpcFact> npcs,
        IReadOnlyList<BethesdaPlacedReferenceFact> references,
        IReadOnlyList<BethesdaUnsupportedRecord> unsupportedRecords,
        IReadOnlyList<BethesdaFaceGenFact> faceGen)
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
            results.Add(Projection(contribution.ContributionId, "record-contribution", "technical-modification-surface", "realization-and-delivery", "delivery.plugin-container", TaxonomyApplicability.Assigned, ClassificationRole.Observed, [Evidence(contribution, "plugin-container", 0)], "The frozen plugin receipt establishes delivery inside a plugin container."));
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
            results.Add(Projection(subject, "unsupported-record", "affected-game-system-or-content-area", "affected-area", null, TaxonomyApplicability.Unsupported, ClassificationRole.Established, [gapEvidence], "The frozen allowlist gap establishes that the semantic extractor cannot determine affected-area semantics for this record family."));
            results.Add(Projection(subject, "unsupported-record", "consequence-type", "consequence-type", null, TaxonomyApplicability.Unknown, ClassificationRole.Predicted, [gapEvidence], "The surface is present, but unsupported semantics leave any consequence unknown."));
        }

        IReadOnlyDictionary<string, BethesdaRecordContribution> byParticipant = contributions
            .GroupBy(contribution => contribution.Identity.ParticipantId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.LoadOrder).Last(), StringComparer.Ordinal);
        foreach ((BethesdaFaceGenFact fact, BethesdaLooseAssetFact asset) in faceGen
                     .SelectMany(fact => new[] { (fact, fact.Mesh), (fact, fact.Tint) })
                     .Where(item => item.Item2.ProviderParticipantIds.Count > 0))
        {
            if (!byParticipant.TryGetValue(fact.NpcParticipantId, out BethesdaRecordContribution? winner))
            {
                throw new InvalidOperationException("A FaceGen fact is not bound to its winning NPC contribution.");
            }

            string subject = $"{winner.ContributionId}:semantic:face-gen-loose-provider-chain:{asset.NormalizedRelativePath}";
            string[] evidence = asset.ProviderParticipantIds.Count == 0
                ? [$"evidence:{subject}:provider-chain:unknown"]
                : asset.ProviderParticipantIds
                    .Select((provider, ordinal) => $"evidence:{subject}:provider-chain:{ordinal}:{provider}")
                    .ToArray();
            results.Add(Projection(subject, "record-semantic-subject", "technical-modification-surface", "semantic-mechanism", "surface.asset", TaxonomyApplicability.Assigned, ClassificationRole.Observed, evidence, "The declared FaceGen loose-provider chain establishes an asset surface."));
            results.Add(Projection(subject, "record-semantic-subject", "technical-modification-surface", "realization-and-delivery", "delivery.loose-data-file", TaxonomyApplicability.Assigned, ClassificationRole.Observed, evidence, "The declared FaceGen provider chain establishes loose-data-file delivery."));
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
        string material = string.Join('|', subject, subjectType, axis, facet, code ?? applicability.ToString(), role, string.Join(',', evidence));
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
            "analyzer:infinium-bethesda-semantic-index",
            reason);
    }

    private static BethesdaSemanticExtractionResult Failure(BethesdaExtractionState state, string code, string input, string message) =>
        new(state, null, [new BethesdaExtractionFailure(code, input, message)], []);

    private static BethesdaInputException Input(string code, string input, string message) =>
        new(BethesdaExtractionState.InvalidInput, code, input, message);

    private static BethesdaInputException Changed(string input, string message) =>
        new(BethesdaExtractionState.ChangedDuringRead, "plugin-changed-during-read", input, message);

    private static string Bounded(string value) => value.Length <= 512 ? value : value[..512];

}
