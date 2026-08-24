using Infinium.Domain.Contracts;

namespace Infinium.Analysis.ScopeReversion;

public sealed record ActorScopeReversionInput(
    OpaqueId MemberId,
    OpaqueId ActorId,
    OpaqueId PriorContributionId,
    OpaqueId WinningContributionId,
    string? PriorPackageId,
    string? WinningPackageId,
    string PriorAppearanceFingerprint,
    string WinningAppearanceFingerprint,
    ScopeSupportState PurposeSupport,
    ScopeApplicabilityState PurposeApplicability,
    IReadOnlyList<string> PurposeDimensions,
    IReadOnlyList<string> IntentionalDimensions,
    ScopeContradictionState Contradiction,
    ScopeCausalClosureState ClosureState,
    OpaqueId DependencyClosureId,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<OpaqueId> EvidenceIds,
    ScopePublicationEligibility PublicationEligibility,
    CoverageMemberState CoverageState,
    ScopeGapFailureState GapFailureState,
    string? Issue);

public sealed record PlacedReferenceScopeReversionInput(
    OpaqueId MemberId,
    OpaqueId ReferenceId,
    OpaqueId PriorContributionId,
    OpaqueId WinningContributionId,
    string? PriorLinkId,
    string? WinningLinkId,
    string PriorPositionFingerprint,
    string WinningPositionFingerprint,
    ScopeSupportState PurposeSupport,
    ScopeApplicabilityState PurposeApplicability,
    IReadOnlyList<string> PurposeDimensions,
    IReadOnlyList<string> IntentionalDimensions,
    ScopeContradictionState Contradiction,
    ScopeCausalClosureState ClosureState,
    OpaqueId DependencyClosureId,
    IReadOnlyList<OpaqueId> DependencyIds,
    IReadOnlyList<OpaqueId> EvidenceIds,
    ScopePublicationEligibility PublicationEligibility,
    CoverageMemberState CoverageState,
    ScopeGapFailureState GapFailureState,
    string? Issue);

public interface IScopeReversionAdapter<in T>
{
    public string AdapterId { get; }
    public ScopeReversionMemberContract Adapt(T input);
}

public sealed class ActorScopeReversionAdapter : IScopeReversionAdapter<ActorScopeReversionInput>
{
    public const string StableAdapterId = "infinium.scope-reversion.adapter.actor-ai-facegen";
    public string AdapterId => StableAdapterId;

    public ScopeReversionMemberContract Adapt(ActorScopeReversionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new ScopeReversionMemberContract(
            input.MemberId,
            AdapterId,
            input.ActorId,
            "actor.ai-package",
            State(input.PriorPackageId, input.PriorContributionId, input.EvidenceIds),
            State(input.WinningPackageId, input.WinningContributionId, input.EvidenceIds),
            new ScopePurposeContract(
                input.PurposeSupport,
                input.PurposeApplicability,
                !string.Equals(input.PriorAppearanceFingerprint, input.WinningAppearanceFingerprint, StringComparison.Ordinal),
                input.PurposeDimensions.Order(StringComparer.Ordinal).ToArray(),
                input.IntentionalDimensions.Order(StringComparer.Ordinal).ToArray(),
                input.EvidenceIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray()),
            input.Contradiction,
            new ScopeCausalClosureContract(
                input.ClosureState,
                input.DependencyClosureId,
                input.DependencyIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
                input.EvidenceIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray()),
            input.PublicationEligibility,
            input.CoverageState,
            input.GapFailureState,
            input.Issue,
            new ScopeDomainInterpretationContract(
                "actor-ai-facegen",
                "actor",
                "actor.ai-package",
                "A winning actor contribution changed appearance while changing the earlier AI-package relationship.",
                "the bounded actor and its effective AI-package relationship",
                "the actor can lose the established scheduling behavior while retaining the winning appearance",
                "one actor and its directly established package relationship",
                "preserve the appearance change while restoring the intended AI-package relationship",
                "re-resolve the actor contribution chain and confirm both appearance and package behavior",
                "purpose-target.appearance",
                "surface.plugin-data.actor-package",
                "consequence.meaningful-bounded-loss",
                "extent.subject.bounded-set"));
    }

    private static ScopeContributionStateContract State(
        string? value,
        OpaqueId contributionId,
        IReadOnlyList<OpaqueId> evidenceIds) => new(
            value is null ? ScopeValueState.Absent : ScopeValueState.Present,
            value,
            contributionId,
            evidenceIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray());
}

public sealed class PlacedReferenceScopeReversionAdapter : IScopeReversionAdapter<PlacedReferenceScopeReversionInput>
{
    public const string StableAdapterId = "infinium.scope-reversion.adapter.refr-link-placement";
    public string AdapterId => StableAdapterId;

    public ScopeReversionMemberContract Adapt(PlacedReferenceScopeReversionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new ScopeReversionMemberContract(
            input.MemberId,
            AdapterId,
            input.ReferenceId,
            "reference.link",
            State(input.PriorLinkId, input.PriorContributionId, input.EvidenceIds),
            State(input.WinningLinkId, input.WinningContributionId, input.EvidenceIds),
            new ScopePurposeContract(
                input.PurposeSupport,
                input.PurposeApplicability,
                !string.Equals(input.PriorPositionFingerprint, input.WinningPositionFingerprint, StringComparison.Ordinal),
                input.PurposeDimensions.Order(StringComparer.Ordinal).ToArray(),
                input.IntentionalDimensions.Order(StringComparer.Ordinal).ToArray(),
                input.EvidenceIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray()),
            input.Contradiction,
            new ScopeCausalClosureContract(
                input.ClosureState,
                input.DependencyClosureId,
                input.DependencyIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
                input.EvidenceIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray()),
            input.PublicationEligibility,
            input.CoverageState,
            input.GapFailureState,
            input.Issue,
            new ScopeDomainInterpretationContract(
                "refr-link-placement",
                "reference",
                "reference.link",
                "A winning placed-reference contribution changed position while changing the earlier link relationship.",
                "the bounded placed reference and its effective link relationship",
                "the placed reference can lose the established linked behavior while retaining the winning position",
                "one placed reference and its directly established link relationship",
                "preserve the position change while restoring the intended link relationship",
                "re-resolve the reference contribution chain and confirm both position and linked behavior",
                "purpose-target.position",
                "surface.plugin-data.reference-link",
                "consequence.meaningful-bounded-loss",
                "extent.subject.bounded-set"));
    }

    private static ScopeContributionStateContract State(
        string? value,
        OpaqueId contributionId,
        IReadOnlyList<OpaqueId> evidenceIds) => new(
            value is null ? ScopeValueState.Absent : ScopeValueState.Present,
            value,
            contributionId,
            evidenceIds.OrderBy(item => item.Value, StringComparer.Ordinal).ToArray());
}
