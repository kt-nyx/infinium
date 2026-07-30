using Infinium.Domain.Contracts;

namespace Infinium.Mo2;

public enum AdmissionState
{
    Unspecified,
    Accepted,
    Unrecognized,
    Unsupported,
    Inconsistent,
    Indeterminate,
}

public enum SnapshotCaptureState
{
    Unspecified,
    Completed,
    CompletedWithGaps,
    Failed,
    ChangedDuringCapture,
}

public enum LooseProviderKind
{
    Unspecified,
    PhysicalData,
    SecondaryData,
    RegularMod,
    Overwrite,
    QualifiedMapper,
}

public enum PhysicalEntryDisposition
{
    Unspecified,
    EffectiveDataCandidate,
    HiddenBySuffix,
    SkippedDirectory,
    Mo2ManagementContent,
}

public enum ModEnablementState
{
    Unspecified,
    Enabled,
    Disabled,
    Unresolved,
}

public enum PluginEnablementState
{
    Unspecified,
    EnabledByProfile,
    DisabledByProfile,
    ForcedEnabledByGamePlugin,
    Unresolved,
}

public enum PluginClassification
{
    Unspecified,
    Regular,
    PrimaryGame,
    CreationClubGame,
    ForeignGameData,
}

public sealed record ExecutableIdentity(
    string FileName,
    long ByteLength,
    string Sha256,
    string? ProductVersion,
    ushort? PeMachine,
    ushort? PeOptionalHeaderMagic,
    ushort? PeSubsystem,
    string? PhysicalObjectIdentity = null);

public sealed record ExecutableAdmission(
    AdmissionState State,
    string ManifestId,
    ExecutableIdentity? ObservedIdentity,
    IReadOnlyList<string> Reasons);

public sealed record QualifiedMapping(
    string MappingId,
    string SourceRoot,
    string VirtualPrefix,
    string MapperSha256);

public sealed record RuntimeTargetContext(
    string Platform,
    string DistributionChannel,
    string ApplicationId);

public sealed record Mo2SnapshotCaptureRequest(
    string Mo2ExecutablePath,
    string InstanceRoot,
    string InstanceIniPath,
    string ProfilesRoot,
    string ModsRoot,
    string OverwriteRoot,
    string GameDataRoot,
    string SkyrimExecutablePath,
    string SelectedProfileName,
    RuntimeTargetContext RuntimeTarget,
    IReadOnlyList<QualifiedMapping> QualifiedMappings,
    IReadOnlyList<string> EnabledMapperSha256s,
    string ManagerId = "mod-organizer-2");

public sealed record ModState(
    string Name,
    ModEnablementState Enablement,
    int? Priority,
    bool Listed,
    OpaqueId LocalInstalledEntityId);

public sealed record PluginState(
    string Name,
    PluginEnablementState Enablement,
    PluginClassification Classification,
    int? LoadOrder,
    OpaqueId? WinningLocalInstalledEntityId,
    string CorrelationState)
{
    public bool Enabled =>
        Enablement is PluginEnablementState.EnabledByProfile
            or PluginEnablementState.ForcedEnabledByGamePlugin;
}

public sealed record LocalSourceHint(
    string Key,
    string RawValue,
    string Authority);

public sealed record LocalInstalledEntity(
    OpaqueId EntityId,
    string PhysicalPath,
    LooseProviderKind Kind,
    Sha256Fingerprint PhysicalInventoryFingerprint,
    IReadOnlyList<LocalSourceHint> SourceHints);

public sealed record LooseProvider(
    OpaqueId LocalInstalledEntityId,
    LooseProviderKind Kind,
    string PhysicalPath,
    int Priority);

public sealed record LooseProviderChain(
    string NormalizedRelativePath,
    IReadOnlyList<LooseProvider> Providers,
    LooseProvider Winner);

public sealed record PhysicalInventoryEntry(
    OpaqueId LocalInstalledEntityId,
    string RelativePath,
    long ByteLength,
    PhysicalEntryDisposition Disposition);

public sealed record SnapshotGap(
    string Code,
    string Population,
    string Reason);

public sealed record Mo2InstallationSnapshot(
    InstallationSnapshotContract Contract,
    string AdapterId,
    string InstanceRoot,
    string ProfileRoot,
    string SavedProfileHint,
    ExecutableAdmission Mo2Admission,
    ExecutableAdmission SkyrimGamePluginAdmission,
    ExecutableAdmission RuntimeAdmission,
    Mo2SnapshotDependencyManifest Dependencies,
    IReadOnlyList<ModState> Mods,
    IReadOnlyList<PluginState> Plugins,
    IReadOnlyList<LocalInstalledEntity> LocalInstalledEntities,
    IReadOnlyList<LooseProviderChain> LooseProviderChains,
    IReadOnlyList<PhysicalInventoryEntry> PhysicalInventory,
    IReadOnlyList<string> MissingListedMods,
    IReadOnlyList<SnapshotGap> Gaps,
    bool ArchiveMemberPopulationSupported,
    bool Mo2OrUsvfsLaunched);

public sealed record Mo2SnapshotCaptureResult(
    SnapshotCaptureState State,
    Mo2InstallationSnapshot? Snapshot,
    IReadOnlyList<SnapshotGap> Gaps);

internal interface IMo2ProcessProbe
{
    public bool IsRunning(string exactExecutablePath);
}
