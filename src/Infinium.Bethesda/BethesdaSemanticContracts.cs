using Infinium.Domain.Contracts;

namespace Infinium.Bethesda;

public enum BethesdaExtractionState
{
    Unspecified,
    Completed,
    CompletedWithGaps,
    InvalidInput,
    ChangedDuringRead,
    Failed,
}

public enum BethesdaMasterStyle
{
    Unspecified,
    Full,
    Light,
}

public enum BethesdaLinkState
{
    Unspecified,
    Null,
    Resolved,
    Unresolved,
}

public enum BethesdaFaceGenApplicability
{
    Unspecified,
    Applicable,
    CoverageGapDeletedWinner,
    CoverageGapTemplateSource,
    CoverageGapTemplateTraits,
    CoverageGapUnresolvedRace,
    InapplicableRaceWithoutFaceGenHead,
}

public enum BethesdaUnsupportedCapability
{
    ArchiveMemberRead,
    LocalizedStringResolution,
    AutomaticEnvironmentDiscovery,
}

public sealed record BethesdaSemanticRequest(
    Infinium.Mo2.Mo2SnapshotCaptureResult AcceptedSnapshot,
    IReadOnlyList<BethesdaUnsupportedCapability>? RequestedUnsupportedCapabilities = null);

public sealed record BethesdaPluginReceipt(
    string PluginName,
    int LoadOrder,
    OpaqueId LocalInstalledEntityId,
    string SnapshotAuthorizedPath,
    long ByteLength,
    Sha256Fingerprint Sha256,
    BethesdaMasterStyle MasterStyle,
    IReadOnlyList<string> Masters);

public sealed record BethesdaRecordIdentity(
    string ParticipantId,
    string Signature,
    string FormKey,
    string OriginPlugin,
    uint OriginLocalId);

public sealed record BethesdaResolvedParticipant(
    string ParticipantId,
    string FormKey);

public sealed record BethesdaRecordContribution(
    string ContributionId,
    BethesdaRecordIdentity Identity,
    string SourcePlugin,
    int LoadOrder,
    bool Deleted,
    bool Compressed,
    uint RawFlags);

public sealed record BethesdaOverrideChain(
    BethesdaRecordIdentity Identity,
    IReadOnlyList<BethesdaRecordContribution> Contributions,
    BethesdaRecordContribution Winner);

public sealed record BethesdaLinkFact(
    string SourceParticipantId,
    string SourceContributionId,
    string Field,
    string? Component,
    int Ordinal,
    string? TargetFormKey,
    BethesdaLinkState State,
    string? TargetParticipantId);

public sealed record BethesdaFieldPresence(
    string ContributionId,
    string Field,
    int Count);

public sealed record BethesdaAiDataFact(
    int Aggression,
    int Confidence,
    int EnergyLevel,
    int Responsibility,
    int Mood,
    int Assistance,
    uint Warn,
    uint WarnOrAttack,
    uint Attack,
    bool AggroRadiusBehavior);

public sealed record BethesdaNpcFact(
    BethesdaRecordContribution Contribution,
    uint ConfigurationFlags,
    uint TemplateFlags,
    bool UsesTemplate,
    bool TemplatesTraits,
    BethesdaLinkFact? Template,
    BethesdaLinkFact? Race,
    BethesdaAiDataFact? AiData,
    IReadOnlyList<BethesdaLinkFact> Packages,
    IReadOnlyList<BethesdaLinkFact> HeadParts,
    BethesdaLinkFact? HairColor);

public sealed record BethesdaRaceFact(
    BethesdaRecordContribution Contribution,
    bool FaceGenHead);

public sealed record BethesdaVector3(float X, float Y, float Z);

public sealed record BethesdaPlacementFact(
    BethesdaVector3 Position,
    BethesdaVector3 Rotation);

public sealed record BethesdaPlacedReferenceFact(
    BethesdaRecordContribution Contribution,
    BethesdaLinkFact? Base,
    IReadOnlyList<BethesdaLinkFact> LinkedReferences,
    BethesdaLinkFact? LocationReference,
    BethesdaLinkFact? Owner,
    BethesdaPlacementFact? Placement);

public sealed record BethesdaLooseAssetFact(
    string NormalizedRelativePath,
    IReadOnlyList<string> ProviderParticipantIds,
    string? WinnerParticipantId,
    bool Present,
    bool ExactAbsenceKnown);

public sealed record BethesdaFaceGenFact(
    string NpcParticipantId,
    BethesdaFaceGenApplicability Applicability,
    string OriginPlugin,
    uint OriginLocalId,
    BethesdaLooseAssetFact Mesh,
    BethesdaLooseAssetFact Tint,
    string Reason);

public sealed record BethesdaCoverageGap(
    string GapId,
    string Population,
    long Denominator,
    string Reason,
    string MissingCapability);

public sealed record BethesdaCoveragePopulation(
    string Population,
    string DenominatorLabel,
    long Denominator,
    long Completed,
    CoverageState State,
    IReadOnlyList<string> GapIds);

public sealed record BethesdaTaxonomyProjection(
    string AssignmentId,
    string TaxonomyId,
    ContractVersion TaxonomyVersion,
    string SubjectParticipantId,
    string SubjectType,
    string Axis,
    string Facet,
    string? Code,
    TaxonomyApplicability Applicability,
    ClassificationRole Role,
    IReadOnlyList<string> EvidenceFields,
    string AnalyzerOrAdjudicatorId,
    string Reason);

public sealed record BethesdaSemanticSnapshot(
    OpaqueId SourceSnapshotId,
    ContractVersion SchemaVersion,
    string ProducerId,
    string ProducerVersion,
    Sha256Fingerprint DependencyFingerprint,
    IReadOnlyList<BethesdaPluginReceipt> Plugins,
    IReadOnlyDictionary<string, BethesdaOverrideChain> OverrideChains,
    IReadOnlyDictionary<string, BethesdaRecordContribution> Winners,
    IReadOnlyList<BethesdaNpcFact> NpcContributions,
    IReadOnlyList<BethesdaRaceFact> RaceContributions,
    IReadOnlyList<BethesdaPlacedReferenceFact> PlacedReferenceContributions,
    IReadOnlyList<BethesdaFieldPresence> AllowlistedFields,
    IReadOnlyDictionary<string, BethesdaResolvedParticipant> ResolvedParticipants,
    IReadOnlyDictionary<string, BethesdaNpcFact> Npcs,
    IReadOnlyDictionary<string, BethesdaRaceFact> Races,
    IReadOnlyDictionary<string, BethesdaPlacedReferenceFact> PlacedReferences,
    IReadOnlyList<BethesdaLinkFact> Links,
    IReadOnlyDictionary<string, IReadOnlyList<BethesdaLinkFact>> ReverseLinks,
    IReadOnlyList<BethesdaFaceGenFact> FaceGen,
    IReadOnlyList<BethesdaTaxonomyProjection> Taxonomy,
    IReadOnlyList<BethesdaCoveragePopulation> Coverage,
    IReadOnlyList<BethesdaCoverageGap> Gaps);

public sealed record BethesdaExtractionFailure(
    string Code,
    string Input,
    string Message);

public sealed record BethesdaSemanticExtractionResult(
    BethesdaExtractionState State,
    BethesdaSemanticSnapshot? Snapshot,
    IReadOnlyList<BethesdaExtractionFailure> Failures,
    IReadOnlyList<BethesdaCoverageGap> Gaps);
