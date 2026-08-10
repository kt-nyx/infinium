using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;

#pragma warning disable CA1859 // Contract-shaped collection abstractions keep capture seams narrow.

namespace Infinium.Mo2;


public sealed partial class Mo2SnapshotCapture
{
    private const int MaximumControlBytes = 4 * 1024 * 1024;
    private const int MaximumEntries = 500_000;

    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IExecutableAdmissionService manifests;
    private readonly IMo2ProcessProbe processProbe;
    private readonly IReadOnlySet<string> qualifiedMapperHashes;
    private readonly Action? betweenStructuralCaptures;
    private readonly Action<string, string>? beforeHandleRelativeEntryOpen;

    public Mo2SnapshotCapture()
        : this(
            new SupportedExecutableManifests(),
            new WindowsMo2ProcessProbe(),
            SupportedExecutableManifests.QualifiedMapperSha256s,
            null,
            null)
    {
    }

    internal Mo2SnapshotCapture(
        IExecutableAdmissionService manifests,
        IMo2ProcessProbe processProbe,
        IReadOnlySet<string> qualifiedMapperHashes,
        Action? betweenStructuralCaptures,
        Action<string, string>? beforeHandleRelativeEntryOpen = null)
    {
        this.manifests = manifests;
        this.processProbe = processProbe;
        this.qualifiedMapperHashes = qualifiedMapperHashes;
        this.betweenStructuralCaptures = betweenStructuralCaptures;
        this.beforeHandleRelativeEntryOpen = beforeHandleRelativeEntryOpen;
    }

    public Mo2SnapshotCaptureResult Capture(
        Mo2SnapshotCaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RuntimeTarget);
        ArgumentNullException.ThrowIfNull(request.QualifiedMappings);
        ArgumentNullException.ThrowIfNull(request.EnabledMapperSha256s);
        List<SnapshotGap> gaps = [];
        if (!string.Equals(
                request.ManagerId,
                "mod-organizer-2",
                StringComparison.Ordinal))
        {
            gaps.Add(new SnapshotGap(
                "unsupported-manager",
                "manager-target",
                $"The declared manager '{request.ManagerId}' is outside the supported target."));
            return new Mo2SnapshotCaptureResult(
                SnapshotCaptureState.Failed,
                null,
                Freeze(gaps));
        }

        ValidatedPaths? paths;
        try
        {
            paths = ValidatePaths(request);
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or IOException
                or UnauthorizedAccessException)
        {
            gaps.Add(new SnapshotGap(
                "invalid-or-inaccessible-configuration",
                "mo2-configuration",
                exception.Message));
            return new Mo2SnapshotCaptureResult(
                SnapshotCaptureState.Failed,
                null,
                Freeze(gaps));
        }

        ExecutableAdmission mo2Admission = manifests.AdmitMo2(paths.Mo2Executable);
        ExecutableAdmission gamePluginAdmission =
            manifests.AdmitSkyrimGamePlugin(paths.SkyrimGamePlugin);
        ExecutableAdmission runtimeAdmission = manifests.AdmitSkyrim(
            paths.SkyrimExecutable,
            request.RuntimeTarget);
        if (mo2Admission.State != AdmissionState.Accepted
            || gamePluginAdmission.State != AdmissionState.Accepted
            || runtimeAdmission.State != AdmissionState.Accepted)
        {
            AddAdmissionGaps(gaps, "mo2-identity", mo2Admission);
            AddAdmissionGaps(gaps, "mo2-game-plugin-identity", gamePluginAdmission);
            AddAdmissionGaps(gaps, "runtime-identity", runtimeAdmission);
            return new Mo2SnapshotCaptureResult(
                SnapshotCaptureState.Failed,
                null,
                Freeze(gaps));
        }

        if (processProbe.IsRunning(paths.Mo2Executable))
        {
            gaps.Add(new SnapshotGap(
                "mo2-not-quiescent",
                "snapshot",
                "The selected MO2 executable is running; capture requires a closed instance."));
            return new Mo2SnapshotCaptureResult(
                SnapshotCaptureState.Failed,
                null,
                Freeze(gaps));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<AdmittedMapping> admittedMappings =
                ResolveAdmittedMappings(request, gaps);
            StructuralCapture first = CaptureStructure(
                paths,
                admittedMappings,
                gaps,
                cancellationToken);
            Dictionary<string, ControlFile> controls = ReadControls(paths);
            AddMetaControls(paths, first, controls);
            foreach (string required in new[] { "modlist", "plugins", "loadorder" })
            {
                if (!controls[required].Exists)
                {
                    gaps.Add(new SnapshotGap(
                        "required-control-file-missing",
                        required,
                        $"Required selected-profile control is missing: {controls[required].Path}"));
                }
            }

            if (gaps.Any(gap => gap.Code == "required-control-file-missing"))
            {
                return new Mo2SnapshotCaptureResult(
                    SnapshotCaptureState.Failed,
                    null,
                    Freeze(gaps));
            }

            SkipPolicy skipPolicy =
                ValidateInstanceConfiguration(paths, controls["instance-ini"].Bytes);
            string savedProfileHint = ReadSavedProfileHint(controls["instance-ini"].Bytes);

            betweenStructuralCaptures?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            StructuralCapture second;
            try
            {
                second = CaptureStructure(
                    paths,
                    admittedMappings,
                    [],
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or ArgumentException
                    or Win32Exception)
            {
                gaps.Add(new SnapshotGap(
                    "changed-during-capture",
                    "snapshot",
                    $"Structural revalidation failed after the initial capture: {exception.GetType().Name}."));
                return new Mo2SnapshotCaptureResult(
                    SnapshotCaptureState.ChangedDuringCapture,
                    null,
                    Freeze(gaps));
            }

            ExecutableAdmission finalMo2Admission =
                manifests.AdmitMo2(paths.Mo2Executable);
            ExecutableAdmission finalGamePluginAdmission =
                manifests.AdmitSkyrimGamePlugin(paths.SkyrimGamePlugin);
            ExecutableAdmission finalRuntimeAdmission = manifests.AdmitSkyrim(
                paths.SkyrimExecutable,
                request.RuntimeTarget);
            if (!string.Equals(
                    first.Fingerprint,
                    second.Fingerprint,
                    StringComparison.OrdinalIgnoreCase)
                || !ControlsRemainCurrent(controls)
                || !SameAdmission(mo2Admission, finalMo2Admission)
                || !SameAdmission(gamePluginAdmission, finalGamePluginAdmission)
                || !SameAdmission(runtimeAdmission, finalRuntimeAdmission)
                || processProbe.IsRunning(paths.Mo2Executable))
            {
                gaps.Add(new SnapshotGap(
                    "changed-during-capture",
                    "snapshot",
                    "A structural, executable, quiescence, or content-sealed dependency changed during capture."));
                return new Mo2SnapshotCaptureResult(
                    SnapshotCaptureState.ChangedDuringCapture,
                    null,
                    Freeze(gaps));
            }

            CaptureModel model = BuildModel(
                paths,
                request,
                first,
                controls,
                admittedMappings,
                skipPolicy,
                gaps);
            SnapshotCaptureState state = gaps.Count == 0
                ? SnapshotCaptureState.Completed
                : SnapshotCaptureState.CompletedWithGaps;
            OpaqueId instanceId = StableId(
                "mo2-instance",
                first.RootIdentities["instance"]);
            OpaqueId profileId = StableId(
                "mo2-profile",
                $"{instanceId.Value}|{first.RootIdentities["profile"]}");
            Mo2SnapshotDependencyManifest dependencies = BuildDependencyManifest(
                new string('0', 64),
                paths,
                request,
                first,
                controls,
                admittedMappings,
                qualifiedMapperHashes,
                mo2Admission,
                gamePluginAdmission,
                runtimeAdmission);
            Sha256Fingerprint structuralFingerprint =
                Mo2SnapshotCanonicalization.Compute(
                    dependencies,
                    instanceId,
                    profileId);
            dependencies = dependencies with
            {
                CanonicalFingerprint = structuralFingerprint,
            };
            UtcTimestamp capturedAt = new(DateTimeOffset.UtcNow);
            OpaqueId snapshotId = Mo2SnapshotCanonicalization.ComputeSnapshotId(
                structuralFingerprint,
                capturedAt);
            IReadOnlyList<SnapshotPopulationAssurance> assurance =
            Freeze<SnapshotPopulationAssurance>(
            [
                new(
                    "mo2-control-files",
                    SnapshotAssuranceState.SelectivelyContentSealed,
                    controls.Count,
                    controls.Count,
                    []),
                new(
                    "loose-provider-structure",
                    SnapshotAssuranceState.Structural,
                    model.PhysicalEntries.Count,
                    model.PhysicalEntries.Count,
                    gaps
                        .Where(gap => gap.Population is "loose-providers" or "filesystem")
                        .Select(gap => gap.Code)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()),
                new(
                    "archive-members",
                    SnapshotAssuranceState.Unsupported,
                    0,
                    0,
                    ["archive-member-semantics-not-qualified"]),
            ]);
            InstallationSnapshotContract contract = new(
                snapshotId,
                new ContractVersion(3, 0, 0),
                instanceId,
                profileId,
                structuralFingerprint,
                assurance,
                Freeze(model.Entities.Select(entity => entity.EntityId)),
                capturedAt);
            Mo2InstallationSnapshot snapshot = new(
                contract,
                SupportedExecutableManifests.AdapterId,
                paths.InstanceRoot,
                paths.ProfileRoot,
                savedProfileHint,
                mo2Admission,
                gamePluginAdmission,
                runtimeAdmission,
                dependencies,
                Freeze(model.Mods),
                Freeze(model.Plugins),
                Freeze(model.Entities),
                Freeze(model.ProviderChains),
                Freeze(model.PhysicalEntries),
                Freeze(model.MissingListedMods),
                Freeze(gaps),
                ArchiveMemberPopulationSupported: false,
                Mo2OrUsvfsLaunched: false);
            return new Mo2SnapshotCaptureResult(state, snapshot, Freeze(gaps));
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or OperationCanceledException
                or Win32Exception)
        {
            gaps.Add(new SnapshotGap(
                "capture-input-failure",
                "snapshot",
                $"{exception.GetType().Name}: {exception.Message}"));
            return new Mo2SnapshotCaptureResult(
                SnapshotCaptureState.Failed,
                null,
                Freeze(gaps));
        }
    }

    private static ValidatedPaths ValidatePaths(Mo2SnapshotCaptureRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SelectedProfileName)
            || request.SelectedProfileName.IndexOfAny(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
            || request.SelectedProfileName is "." or "..")
        {
            throw new ArgumentException(
                "An explicit profile directory name is required.",
                nameof(request));
        }

        string mo2Executable = ExistingFile(request.Mo2ExecutablePath);
        string mo2ApplicationRoot = Path.GetDirectoryName(mo2Executable)
            ?? throw new InvalidDataException(
                "The selected MO2 executable has no application directory.");
        string skyrimGamePlugin = ExistingFile(Path.Combine(
            mo2ApplicationRoot,
            "plugins",
            "game_skyrimse.dll"));
        string instanceRoot = ExistingDirectory(request.InstanceRoot);
        string instanceIni = ExistingFile(request.InstanceIniPath);
        string profilesRoot = ExistingDirectory(request.ProfilesRoot);
        string modsRoot = ExistingDirectory(request.ModsRoot);
        string overwriteRoot = ExistingDirectory(request.OverwriteRoot);
        string gameDataRoot = ExistingDirectory(request.GameDataRoot);
        string skyrimExecutable = ExistingFile(request.SkyrimExecutablePath);

        RequireWithin(instanceRoot, instanceIni, "instance INI");
        string[] profileMatches = Directory
            .EnumerateDirectories(profilesRoot)
            .Where(path => string.Equals(
                Path.GetFileName(path),
                request.SelectedProfileName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (profileMatches.Length != 1)
        {
            throw new InvalidDataException(
                "The explicit profile must resolve to exactly one immediate profile directory.");
        }

        string profileRoot = ExistingDirectory(profileMatches[0]);
        RequireWithin(profilesRoot, profileRoot, "selected profile");
        foreach (string path in new string[]
                 {
                     mo2Executable,
                     skyrimGamePlugin,
                     instanceRoot,
                     instanceIni,
                     profilesRoot,
                     profileRoot,
                     modsRoot,
                     overwriteRoot,
                     gameDataRoot,
                     skyrimExecutable,
                 })
        {
            RejectReparsePoint(path);
        }

        return new ValidatedPaths(
            mo2Executable,
            skyrimGamePlugin,
            instanceRoot,
            instanceIni,
            profilesRoot,
            profileRoot,
            modsRoot,
            overwriteRoot,
            gameDataRoot,
            skyrimExecutable);
    }
}
