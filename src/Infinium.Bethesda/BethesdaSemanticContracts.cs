using System.Text.Json.Serialization;
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

[JsonConverter(typeof(JsonStringEnumConverter<BethesdaFaceGenApplicability>))]
public enum BethesdaFaceGenApplicability
{
    [JsonStringEnumMemberName("applicable")]
    Applicable,
    [JsonStringEnumMemberName("not_applicable_deleted_winner")]
    NotApplicableDeletedWinner,
    [JsonStringEnumMemberName("unknown_template_traits_decision")]
    UnknownTemplateTraitsDecision,
    [JsonStringEnumMemberName("not_applicable_template_traits")]
    NotApplicableTemplateTraits,
    [JsonStringEnumMemberName("unknown_race")]
    UnknownRace,
    [JsonStringEnumMemberName("not_applicable_race_without_face_gen_head")]
    NotApplicableRaceWithoutFaceGenHead,
}

[JsonConverter(typeof(JsonStringEnumConverter<BethesdaTemplateTraitsDecision>))]
public enum BethesdaTemplateTraitsDecision
{
    [JsonStringEnumMemberName("unknown")]
    Unknown,
    [JsonStringEnumMemberName("known_inherited")]
    KnownInherited,
    [JsonStringEnumMemberName("known_not_inherited")]
    KnownNotInherited,
}

[JsonConverter(typeof(JsonStringEnumConverter<BethesdaRaceFaceGenHeadDecision>))]
public enum BethesdaRaceFaceGenHeadDecision
{
    [JsonStringEnumMemberName("unknown")]
    Unknown,
    [JsonStringEnumMemberName("known_present")]
    KnownPresent,
    [JsonStringEnumMemberName("known_absent")]
    KnownAbsent,
}

[JsonConverter(typeof(JsonStringEnumConverter<BethesdaAssetAvailability>))]
public enum BethesdaAssetAvailability
{
    [JsonStringEnumMemberName("present")]
    Present,
    [JsonStringEnumMemberName("absent")]
    Absent,
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}

[JsonConverter(typeof(JsonStringEnumConverter<BethesdaCoverageGapCategory>))]
public enum BethesdaCoverageGapCategory
{
    [JsonStringEnumMemberName("unsupported_record")]
    UnsupportedRecord,
    [JsonStringEnumMemberName("unsupported_field")]
    UnsupportedField,
    [JsonStringEnumMemberName("unsupported_shape")]
    UnsupportedShape,
    [JsonStringEnumMemberName("capability")]
    Capability,
    [JsonStringEnumMemberName("face_gen_applicability")]
    FaceGenApplicability,
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
    BethesdaTemplateTraitsDecision TemplateTraitsDecision,
    bool TemplatesTraits,
    BethesdaLinkFact? Template,
    BethesdaLinkFact? Race,
    BethesdaAiDataFact? AiData,
    IReadOnlyList<BethesdaLinkFact> Packages,
    IReadOnlyList<BethesdaLinkFact> HeadParts,
    BethesdaLinkFact? HairColor);

public sealed record BethesdaRaceFact(
    BethesdaRecordContribution Contribution,
    BethesdaRaceFaceGenHeadDecision FaceGenHeadDecision,
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
    BethesdaAssetAvailability Availability,
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
    BethesdaCoverageGapCategory Category,
    string Detail,
    string Population,
    long Denominator,
    string Reason,
    string MissingCapability);

public static class BethesdaSemanticContract
{
    public static readonly ContractVersion SchemaVersion = new(2, 0, 0);

    public static readonly IReadOnlyList<string> CoveragePopulations =
    [
        "plugins",
        "npc-records",
        "race-records",
        "placed-reference-records",
        "unsupported-records",
        "face-gen-loose-assets",
        "face-gen-archive-assets",
        "localized-strings",
        "automatic-environment-discovery",
        "taxonomy-subjects",
    ];

    public static (bool Present, bool ExactAbsenceKnown) AssetTransport(
        BethesdaAssetAvailability availability) => availability switch
        {
            BethesdaAssetAvailability.Present => (true, false),
            BethesdaAssetAvailability.Absent => (false, true),
            BethesdaAssetAvailability.Unknown => (false, false),
            _ => throw new ArgumentOutOfRangeException(nameof(availability)),
        };
}

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
