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
            || semantic.SchemaVersion != new Infinium.Domain.Contracts.ContractVersion(1, 0, 0)
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
            || (result.State == BethesdaExtractionState.Completed && result.Gaps.Count != 0)
            || (result.State == BethesdaExtractionState.CompletedWithGaps && result.Gaps.Count == 0))
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
