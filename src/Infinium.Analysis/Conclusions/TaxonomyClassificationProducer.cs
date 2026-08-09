using Infinium.Analysis.Candidates;
using Infinium.Domain.Contracts;

namespace Infinium.Analysis.Conclusions;

public enum TaxonomyEvidenceKind
{
    Unspecified,
    DeclaredReplacement,
    DeclaredRuntimeFramework,
    ObservedPluginData,
    ObservedAsset,
    ObservedTextureMaterial,
    ObservedPluginContainer,
    ObservedLooseData,
    ObservedCompiledPapyrus,
    ObservedNativeRuntime,
    ObservedRuntimeSupportData,
    ObservedGameRootDelivery,
    EstablishedActorScheduling,
    EstablishedPlacedObjectActivation,
    EstablishedAppearanceIdentity,
    EstablishedVisualPresentation,
    EstablishedFrameworkServices,
    PredictedQuestProgression,
    PredictedInterfaceControl,
    PredictedContentUnavailable,
    PredictedIncorrectBehavior,
    PredictedCrossFeaturePropagation,
    PredictedSingleInstance,
    PredictedSinglePoint,
    PredictedInstallationPersistence,
    PredictedCrossSystemPropagation,
    PurposeUnknown,
    AreaUnsupported,
    ConsequenceUnsupported,
    ConsequenceUnknown,
    SubjectExtentUnsupported,
    SubjectExtentUnknown,
    SpatialExtentUnsupported,
    SpatialExtentNotApplicable,
    SpatialExtentNotApplicableEstablished,
    PersistenceExtentUnsupported,
    PersistenceExtentUnknown,
    PropagationExtentUnsupported,
    PropagationExtentUnknown,
    ConsequenceNotApplicable,
    SubjectExtentNotApplicable,
    PersistenceExtentNotApplicable,
    PropagationExtentNotApplicable,
    AreaUnmapped,
    ProviderTopologyNonApplicable,
}

public sealed record TaxonomyEvidenceStatement(
    TaxonomyEvidenceKind Kind,
    IReadOnlyList<OpaqueId> EvidenceIds,
    string Reason);

public sealed record TaxonomySubjectFact(
    OpaqueId SubjectId,
    OpaqueId HypothesisId,
    string SubjectType,
    IReadOnlyList<TaxonomyEvidenceStatement> TypedClassifications,
    IReadOnlyList<OpaqueId> ApplicabilityConditionIds,
    OpaqueId AnalyzerOrAdjudicatorId,
    UtcTimestamp CreatedAt);

public static class TaxonomyClassificationProducer
{
    private const string Purpose = "declared-purpose-and-intended-feature-area";
    private const string Surface = "technical-modification-surface";
    private const string Area = "affected-game-system-or-content-area";
    private const string Consequence = "consequence-type";
    private const string Extent = "effect-extent";

    public static IReadOnlyList<TaxonomyClassificationFactContract> Produce(TaxonomySubjectFact subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (subject.TypedClassifications.Count == 0
            || subject.TypedClassifications.Any(item => item.Kind == TaxonomyEvidenceKind.Unspecified
                || item.EvidenceIds.Count == 0 || string.IsNullOrWhiteSpace(item.Reason)))
        {
            throw new InvalidOperationException("Typed taxonomy evidence requires a semantic kind and retained provenance.");
        }
        List<TaxonomyClassificationFactContract> facts = [];
        foreach (TaxonomyEvidenceStatement statement in subject.TypedClassifications)
        {
            AddKind(facts, subject, statement);
        }
        return facts.OrderBy(item => item.FactId.Value, StringComparer.Ordinal).ToArray();
    }

    private static void AddKind(List<TaxonomyClassificationFactContract> facts, TaxonomySubjectFact subject,
        TaxonomyEvidenceStatement evidence)
    {
        switch (evidence.Kind)
        {
            case TaxonomyEvidenceKind.DeclaredReplacement: Add(facts, subject, evidence, Purpose, "purpose-kind", "purpose.replace-overhaul", TaxonomyApplicability.Assigned, ClassificationRole.Declared); break;
            case TaxonomyEvidenceKind.DeclaredRuntimeFramework: Add(facts, subject, evidence, Purpose, "purpose-kind", "purpose.provide-runtime-framework", TaxonomyApplicability.Assigned, ClassificationRole.Declared); break;
            case TaxonomyEvidenceKind.ObservedPluginData: Add(facts, subject, evidence, Surface, "semantic-mechanism", "surface.plugin-data", TaxonomyApplicability.Assigned, ClassificationRole.Observed); break;
            case TaxonomyEvidenceKind.ObservedAsset: Add(facts, subject, evidence, Surface, "semantic-mechanism", "surface.asset", TaxonomyApplicability.Assigned, ClassificationRole.Observed); break;
            case TaxonomyEvidenceKind.ObservedTextureMaterial: Add(facts, subject, evidence, Surface, "semantic-mechanism", "surface.asset.texture-material", TaxonomyApplicability.Assigned, ClassificationRole.Observed); break;
            case TaxonomyEvidenceKind.ObservedPluginContainer: Add(facts, subject, evidence, Surface, "realization-and-delivery", "delivery.plugin-container", TaxonomyApplicability.Assigned, ClassificationRole.Observed); break;
            case TaxonomyEvidenceKind.ObservedLooseData: Add(facts, subject, evidence, Surface, "realization-and-delivery", "delivery.loose-data-file", TaxonomyApplicability.Assigned, ClassificationRole.Observed); break;
            case TaxonomyEvidenceKind.ObservedCompiledPapyrus: Add(facts, subject, evidence, Surface, "semantic-mechanism", "surface.logic.compiled-papyrus", TaxonomyApplicability.Assigned, ClassificationRole.Observed); break;
            case TaxonomyEvidenceKind.ObservedNativeRuntime: Add(facts, subject, evidence, Surface, "semantic-mechanism", "surface.logic.native-runtime", TaxonomyApplicability.Assigned, ClassificationRole.Observed); break;
            case TaxonomyEvidenceKind.ObservedRuntimeSupportData: Add(facts, subject, evidence, Surface, "semantic-mechanism", "surface.runtime-support-data", TaxonomyApplicability.Assigned, ClassificationRole.Observed); break;
            case TaxonomyEvidenceKind.ObservedGameRootDelivery: Add(facts, subject, evidence, Surface, "realization-and-delivery", "delivery.game-root-component", TaxonomyApplicability.Assigned, ClassificationRole.Observed); break;
            case TaxonomyEvidenceKind.EstablishedActorScheduling: Add(facts, subject, evidence, Area, "affected-area", "area.actors.ai-packages", TaxonomyApplicability.Assigned, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.EstablishedPlacedObjectActivation: Add(facts, subject, evidence, Area, "affected-area", "area.world.placed-objects-activation", TaxonomyApplicability.Assigned, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.EstablishedAppearanceIdentity: Add(facts, subject, evidence, Area, "affected-area", "area.actors.appearance-identity", TaxonomyApplicability.Assigned, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.EstablishedVisualPresentation: Add(facts, subject, evidence, Area, "affected-area", "area.presentation.visual", TaxonomyApplicability.Assigned, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.EstablishedFrameworkServices: Add(facts, subject, evidence, Area, "affected-area", "area.runtime-session.mod-framework-services", TaxonomyApplicability.Assigned, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.PredictedQuestProgression: Add(facts, subject, evidence, Area, "affected-area", "area.quests.progression-objectives-aliases", TaxonomyApplicability.Assigned, ClassificationRole.Predicted); break;
            case TaxonomyEvidenceKind.PredictedInterfaceControl: Add(facts, subject, evidence, Area, "affected-area", "area.interface-controls", TaxonomyApplicability.Assigned, ClassificationRole.Predicted); break;
            case TaxonomyEvidenceKind.PredictedContentUnavailable: Add(facts, subject, evidence, Consequence, "consequence-type", "consequence.content-feature-unavailable", TaxonomyApplicability.Assigned, ClassificationRole.Predicted); break;
            case TaxonomyEvidenceKind.PredictedIncorrectBehavior: Add(facts, subject, evidence, Consequence, "consequence-type", "consequence.incorrect-functional-behavior", TaxonomyApplicability.Assigned, ClassificationRole.Predicted); break;
            case TaxonomyEvidenceKind.PredictedCrossFeaturePropagation: Add(facts, subject, evidence, Extent, "causal-propagation-or-blast-radius", "extent.propagation.cross-feature", TaxonomyApplicability.Assigned, ClassificationRole.Predicted); break;
            case TaxonomyEvidenceKind.PredictedSingleInstance: Add(facts, subject, evidence, Extent, "direct-subject-breadth", "extent.subject.single-instance", TaxonomyApplicability.Assigned, ClassificationRole.Predicted); break;
            case TaxonomyEvidenceKind.PredictedSinglePoint: Add(facts, subject, evidence, Extent, "spatial-breadth", "extent.spatial.single-reference-or-point", TaxonomyApplicability.Assigned, ClassificationRole.Predicted); break;
            case TaxonomyEvidenceKind.PredictedInstallationPersistence: Add(facts, subject, evidence, Extent, "persistence-and-lifecycle-breadth", "extent.persistence.installation-persistent", TaxonomyApplicability.Assigned, ClassificationRole.Predicted); break;
            case TaxonomyEvidenceKind.PredictedCrossSystemPropagation: Add(facts, subject, evidence, Extent, "causal-propagation-or-blast-radius", "extent.propagation.cross-system", TaxonomyApplicability.Assigned, ClassificationRole.Predicted); break;
            case TaxonomyEvidenceKind.PurposeUnknown: Add(facts, subject, evidence, Purpose, "purpose-kind", null, TaxonomyApplicability.Unknown, null); break;
            case TaxonomyEvidenceKind.AreaUnsupported: Add(facts, subject, evidence, Area, "affected-area", null, TaxonomyApplicability.Unsupported, null); break;
            case TaxonomyEvidenceKind.ConsequenceUnsupported: Add(facts, subject, evidence, Consequence, "consequence-type", null, TaxonomyApplicability.Unsupported, null); break;
            case TaxonomyEvidenceKind.ConsequenceUnknown: Add(facts, subject, evidence, Consequence, "consequence-type", null, TaxonomyApplicability.Unknown, null); break;
            case TaxonomyEvidenceKind.SubjectExtentUnsupported: Add(facts, subject, evidence, Extent, "direct-subject-breadth", null, TaxonomyApplicability.Unsupported, null); break;
            case TaxonomyEvidenceKind.SubjectExtentUnknown: Add(facts, subject, evidence, Extent, "direct-subject-breadth", null, TaxonomyApplicability.Unknown, null); break;
            case TaxonomyEvidenceKind.SpatialExtentUnsupported: Add(facts, subject, evidence, Extent, "spatial-breadth", null, TaxonomyApplicability.Unsupported, null); break;
            case TaxonomyEvidenceKind.SpatialExtentNotApplicable: Add(facts, subject, evidence, Extent, "spatial-breadth", null, TaxonomyApplicability.NotApplicable, null); break;
            case TaxonomyEvidenceKind.SpatialExtentNotApplicableEstablished: Add(facts, subject, evidence, Extent, "spatial-breadth", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.PersistenceExtentUnsupported: Add(facts, subject, evidence, Extent, "persistence-and-lifecycle-breadth", null, TaxonomyApplicability.Unsupported, null); break;
            case TaxonomyEvidenceKind.PersistenceExtentUnknown: Add(facts, subject, evidence, Extent, "persistence-and-lifecycle-breadth", null, TaxonomyApplicability.Unknown, null); break;
            case TaxonomyEvidenceKind.PropagationExtentUnsupported: Add(facts, subject, evidence, Extent, "causal-propagation-or-blast-radius", null, TaxonomyApplicability.Unsupported, null); break;
            case TaxonomyEvidenceKind.PropagationExtentUnknown: Add(facts, subject, evidence, Extent, "causal-propagation-or-blast-radius", null, TaxonomyApplicability.Unknown, null); break;
            case TaxonomyEvidenceKind.ConsequenceNotApplicable: Add(facts, subject, evidence, Consequence, "consequence-type", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.SubjectExtentNotApplicable: Add(facts, subject, evidence, Extent, "direct-subject-breadth", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.PersistenceExtentNotApplicable: Add(facts, subject, evidence, Extent, "persistence-and-lifecycle-breadth", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.PropagationExtentNotApplicable: Add(facts, subject, evidence, Extent, "causal-propagation-or-blast-radius", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.AreaUnmapped: Add(facts, subject, evidence, Area, "affected-area", null, TaxonomyApplicability.Unmapped, ClassificationRole.Established); break;
            case TaxonomyEvidenceKind.ProviderTopologyNonApplicable:
                Add(facts, subject, evidence, Purpose, "purpose-kind", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established);
                Add(facts, subject, evidence, Surface, "semantic-mechanism", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established);
                Add(facts, subject, evidence, Surface, "realization-and-delivery", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established);
                Add(facts, subject, evidence, Area, "affected-area", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established);
                Add(facts, subject, evidence, Consequence, "consequence-type", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established);
                Add(facts, subject, evidence, Extent, "direct-subject-breadth", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established);
                Add(facts, subject, evidence, Extent, "spatial-breadth", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established);
                Add(facts, subject, evidence, Extent, "persistence-and-lifecycle-breadth", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established);
                Add(facts, subject, evidence, Extent, "causal-propagation-or-blast-radius", null, TaxonomyApplicability.NotApplicable, ClassificationRole.Established);
                break;
            default: throw new InvalidOperationException($"Unsupported typed taxonomy evidence kind {evidence.Kind}.");
        }
    }

    private static void Add(List<TaxonomyClassificationFactContract> facts, TaxonomySubjectFact subject,
        TaxonomyEvidenceStatement evidence, string axis, string facet, string? code,
        TaxonomyApplicability applicability, ClassificationRole? role)
    {
        facts.Add(new TaxonomyClassificationFactContract(
            CandidateAnalysisIdentity.StableId("taxonomy-classification-fact", subject.SubjectId.Value,
                axis, facet, code ?? applicability.ToString()), subject.HypothesisId,
            ContractConstants.TaxonomyId, ContractVersion.Parse(ContractConstants.TaxonomyVersion),
            axis, facet, code, applicability, role, evidence.EvidenceIds,
            subject.ApplicabilityConditionIds, null, subject.AnalyzerOrAdjudicatorId,
            subject.CreatedAt, evidence.Reason));
    }
}
