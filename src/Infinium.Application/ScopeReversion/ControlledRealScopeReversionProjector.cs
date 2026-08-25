using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.ScopeReversion;
using Infinium.Application.Serialization;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;

namespace Infinium.Application.ScopeReversion;

public sealed record ScopeReversionV2ProjectionMemberSpec(
    OpaqueId MemberId,
    OpaqueId SubjectId,
    ScopeReversionV2SubjectKind Kind,
    string FeatureDimension,
    string PriorContributionId,
    string WinningContributionId,
    bool WinningPurposeChangeObserved,
    IReadOnlyList<OpaqueId> SourceDecisionIds,
    OpaqueId DependencyCauseId,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<OpaqueId> EvidenceIds,
    IReadOnlyList<string> ResidualFacts,
    IReadOnlyList<string> Gaps);

public sealed record ScopeReversionV2ProjectionRequest(
    OpaqueId OriginatingRunId,
    string InputHandoffId,
    Sha256Fingerprint InputManifestFingerprint,
    IReadOnlyList<ScopeReversionV2PublicManifestReferenceContract> PublicManifests,
    IReadOnlyList<ScopeReversionV2ControlledInputReferenceContract> ControlledInputs,
    ScopeReversionV2PartitionRole PartitionRole,
    BethesdaSemanticSnapshot Snapshot,
    IReadOnlyList<ScopeReversionV2SubjectContract> Subjects,
    IReadOnlyList<ScopeReversionV2ProjectionMemberSpec> Members,
    IReadOnlyList<ScopeReversionV2SourceDecisionContract> SourceDecisions,
    IReadOnlyList<ScopeReversionV2TaxonomyReferenceContract> Taxonomy,
    IReadOnlyList<ScopeReversionV2PartitionTransitionContract>? PartitionTransitions = null);

public sealed record ScopeReversionV2PipelineResult(
    ScopeReversionV2WorkAssignmentContract Assignment,
    ScopeReversionV2AnalysisContract Analysis,
    byte[] CanonicalJson,
    string HumanSummary);

public static class ControlledRealScopeReversionProjector
{
    public static ScopeReversionV2WorkAssignmentContract Project(ScopeReversionV2ProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Snapshot.SchemaVersion != BethesdaSemanticContract.SchemaVersion)
        {
            throw new InvalidDataException("The controlled projector requires the accepted Bethesda semantic schema.");
        }
        ScopeReversionV2MemberContract[] members = request.Members.Select(item => ProjectMember(request.Snapshot, item))
            .OrderBy(item => item.MemberId.Value, StringComparer.Ordinal).ToArray();
        ScopeReversionV2SubjectContract[] subjects = request.Subjects.OrderBy(item => item.SubjectId.Value, StringComparer.Ordinal).ToArray();
        ScopeReversionV2SourceDecisionContract[] sourceDecisions = request.SourceDecisions.OrderBy(item => item.DecisionId.Value, StringComparer.Ordinal).ToArray();
        ScopeReversionV2TaxonomyReferenceContract[] taxonomy = request.Taxonomy.OrderBy(item => item.AssignmentId.Value, StringComparer.Ordinal).ToArray();
        ScopeReversionV2PublicManifestReferenceContract[] publicManifests = request.PublicManifests
            .OrderBy(item => item.RepositoryPath, StringComparer.Ordinal).ToArray();
        ScopeReversionV2ControlledInputReferenceContract[] controlledInputs = request.ControlledInputs
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
        ScopeReversionAnalyzerBindingContract binding = CreateBinding();
        OpaqueId snapshotId = request.Snapshot.SourceSnapshotId;
        OpaqueId contextId = ScopeReversionV2Contract.StableId("scope-v2-context",
            request.OriginatingRunId.Value, request.InputManifestFingerprint.Value);
        OpaqueId configurationId = ScopeReversionV2Contract.StableId("scope-v2-configuration",
            binding.DeclarationFingerprint.Value, request.PartitionRole.ToString());
        OpaqueId executionInputId = ScopeReversionV2Contract.StableId("scope-v2-execution-input",
            request.InputManifestFingerprint.Value, request.Snapshot.DependencyFingerprint.Value);
        OpaqueId assignmentId = ScopeReversionV2Contract.ComputeAssignmentId(
            request.OriginatingRunId, snapshotId, contextId, configurationId, executionInputId,
            request.InputHandoffId, request.InputManifestFingerprint, publicManifests, controlledInputs,
            subjects, members, sourceDecisions, taxonomy);
        ScopeReversionV2WorkAssignmentContract assignment = new(
            ScopeReversionV2Contract.SchemaId, ScopeReversionV2Contract.SchemaVersion, assignmentId,
            request.OriginatingRunId, snapshotId, contextId, configurationId, executionInputId,
            binding, request.InputHandoffId, request.InputManifestFingerprint, publicManifests, controlledInputs,
            request.PartitionRole,
            subjects, members, sourceDecisions, taxonomy, ScopeReversionV2Analyzer.NotUsedBoundaries);
        ScopeReversionV2Contract.Validate(assignment);
        return assignment;
    }

    public static ScopeReversionV2PipelineResult Execute(ScopeReversionV2ProjectionRequest request)
    {
        ScopeReversionV2WorkAssignmentContract assignment = Project(request);
        ScopeReversionV2AnalysisContract analysis = ScopeReversionV2Analyzer.Execute(
            assignment,
            request.PartitionTransitions);
        byte[] canonical = ScopeReversionV2JsonCodec.Serialize(analysis);
        ScopeReversionV2AnalysisContract decoded = ScopeReversionV2JsonCodec.Deserialize(canonical);
        if (!canonical.AsSpan().SequenceEqual(ScopeReversionV2JsonCodec.Serialize(decoded)))
        {
            throw new InvalidDataException("Scope-reversion v2 publication did not round-trip canonically.");
        }
        return new(assignment, decoded, canonical, ScopeReversionV2OutputRenderer.RenderHuman(decoded));
    }

    private static ScopeReversionV2MemberContract ProjectMember(
        BethesdaSemanticSnapshot snapshot,
        ScopeReversionV2ProjectionMemberSpec spec)
    {
        ScopeContributionStateContract prior = ProjectState(snapshot, spec.Kind, spec.PriorContributionId, spec.EvidenceIds);
        ScopeContributionStateContract winning = ProjectState(snapshot, spec.Kind, spec.WinningContributionId, spec.EvidenceIds);
        string adapterId = spec.Kind switch
        {
            ScopeReversionV2SubjectKind.ActorCohort => ScopeReversionV2Contract.ActorAdapterId,
            ScopeReversionV2SubjectKind.PlacedReference => ScopeReversionV2Contract.ReferenceAdapterId,
            _ => throw new InvalidDataException("The projector received an unsupported subject kind."),
        };
        IReadOnlyList<string> gaps = prior.State == ScopeValueState.Unresolved || winning.State == ScopeValueState.Unresolved
            ? spec.Gaps.Concat(["required typed contribution is unavailable"]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            : spec.Gaps.Order(StringComparer.Ordinal).ToArray();
        return new(spec.MemberId, spec.SubjectId, adapterId, spec.FeatureDimension, prior, winning,
            spec.WinningPurposeChangeObserved, spec.SourceDecisionIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
            spec.DependencyCauseId, spec.DependencyIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
            spec.EvidenceIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
            spec.ResidualFacts.Order(StringComparer.Ordinal).ToArray(), gaps);
    }

    private static ScopeContributionStateContract ProjectState(
        BethesdaSemanticSnapshot snapshot,
        ScopeReversionV2SubjectKind kind,
        string contributionId,
        IReadOnlyList<OpaqueId> evidence)
    {
        string? comparable = kind switch
        {
            ScopeReversionV2SubjectKind.ActorCohort => snapshot.NpcContributions
                .SingleOrDefault(item => item.Contribution.ContributionId == contributionId) is { } npc
                    ? string.Join("|", npc.Packages.Select(CanonicalLink).Order(StringComparer.Ordinal)) : null,
            ScopeReversionV2SubjectKind.PlacedReference => snapshot.PlacedReferenceContributions
                .SingleOrDefault(item => item.Contribution.ContributionId == contributionId) is { } reference
                    ? string.Join("|", reference.LinkedReferences.Select(CanonicalLink).Order(StringComparer.Ordinal)) : null,
            _ => throw new InvalidDataException("The projector received an unsupported subject kind."),
        };
        return new(comparable is null ? ScopeValueState.Unresolved : ScopeValueState.Present, comparable,
            ScopeReversionV2Contract.StableId("bethesda-contribution", contributionId),
            evidence.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray());
    }

    private static string CanonicalLink(BethesdaLinkFact link) => string.Join(":",
        link.Field, link.Component ?? "", link.Ordinal.ToString(CultureInfo.InvariantCulture),
        link.State.ToString(), link.TargetFormKey ?? "", link.TargetParticipantId ?? "");

    private static ScopeReversionAnalyzerBindingContract CreateBinding()
    {
        AnalyzerDeclarationContract declaration = ScopeReversionV2AnalyzerDeclaration.Create();
        DomainContractInvariants.Validate(declaration);
        string canonical = JsonSerializer.Serialize(declaration, ContractJsonSerializer.Options);
        Sha256Fingerprint fingerprint = new(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))));
        return new(declaration.AnalyzerFamily, declaration.AnalyzerId, declaration.AnalyzerVersion,
            declaration.SemanticContractVersion, declaration.IdentityContractVersion, declaration.RulesetVersion,
            fingerprint, canonical, declaration.Maturity);
    }
}
