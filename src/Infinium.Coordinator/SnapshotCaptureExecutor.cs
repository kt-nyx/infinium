using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Infinium.Application.Runtime;
using Infinium.Domain.Contracts;
using Infinium.Mo2;
using Infinium.Persistence;
using Microsoft.Extensions.Logging;

namespace Infinium.Coordinator;

#pragma warning disable CA1848 // Failures are exceptional and retain operation identity.

public sealed class SnapshotCaptureExecutor(
    CoordinatorRuntime runtime,
    ManagedRunExecutor workerLauncher,
    ILogger<SnapshotCaptureExecutor> logger)
{
    private const long MaximumSnapshotBytes = 64L * 1024 * 1024;
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private readonly Lock gate = new();
    private bool pumpRunning;

    public void Schedule(string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        lock (gate)
        {
            if (pumpRunning)
            {
                return;
            }

            pumpRunning = true;
            _ = Task.Run(DrainAsync);
        }
    }

    public void RecoverAtStartup()
    {
        // Running capture attempts cannot retain worker authority across a
        // coordinator epoch. This bounded snapshot path records them failed rather
        // than inventing a successful publication or automatically rerunning
        // a now-stale filesystem observation.
        _ = runtime.Store.FenceInterruptedSnapshotCaptures(
            runtime.Authority.FencingEpoch,
            DateTimeOffset.UtcNow);
        SnapshotCaptureOperationRecord? queued =
            runtime.Store.GetNextDispatchableSnapshotCapture();
        if (queued is not null)
        {
            Schedule(queued.OperationId);
        }
    }

    internal async Task ExecuteForTestsAsync(string operationId) =>
        await ExecuteCoreAsync(operationId).ConfigureAwait(false);

    private async Task DrainAsync()
    {
        while (true)
        {
            SnapshotCaptureOperationRecord? next =
                runtime.Store.GetNextDispatchableSnapshotCapture();
            if (next is null)
            {
                lock (gate)
                {
                    next = runtime.Store.GetNextDispatchableSnapshotCapture();
                    if (next is null)
                    {
                        pumpRunning = false;
                        return;
                    }
                }
            }

            await ExecuteCoreAsync(next.OperationId).ConfigureAwait(false);
        }
    }

    private async Task ExecuteCoreAsync(string operationId)
    {
        SnapshotCaptureAttemptRecord? attempt = null;
        try
        {
            SnapshotCaptureOperationRecord operation =
                runtime.Store.GetSnapshotCaptureOperation(operationId);
            if (operation.State != "Queued")
            {
                return;
            }

            ManagedMo2SnapshotCaptureAssignment assignment =
                JsonSerializer.Deserialize<ManagedMo2SnapshotCaptureAssignment>(
                    operation.RequestJson,
                    StrictJson)
                ?? throw new InvalidOperationException(
                    "The durable snapshot capture request is malformed.");
            attempt = runtime.Store.DispatchSnapshotCaptureAttempt(
                operation.OperationId,
                operation.Generation,
                runtime.Authority.FencingEpoch,
                TimeSpan.FromMinutes(2),
                DateTimeOffset.UtcNow);
            using AttemptStagingAuthority staging =
                runtime.Store.Paths.CreateAttemptStagingDirectory(attempt.AttemptId);
            ManagedWorkerBootstrap bootstrap = new(
                1,
                Guid.NewGuid().ToString("N"),
                runtime.Authority.InstanceId,
                runtime.Authority.FencingEpoch,
                operation.OperationId,
                attempt.AttemptId,
                attempt.AttemptFencingToken,
                runtime.Descriptor.WorkerPipe,
                0,
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N"),
                0,
                "mo2-snapshot.v3.json",
                MaximumSnapshotBytes,
                Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                DateTimeOffset.UtcNow.AddMinutes(2),
                ManagedWorkerOperationKind.Mo2SnapshotCapture,
                "3.0.0",
                assignment);
            ManagedWorkerResult result = await workerLauncher.LaunchWorkerAsync(
                bootstrap,
                staging.Handle).ConfigureAwait(false);

            byte[] bytes = runtime.Store.ReadSnapshotCaptureStagedPayload(
                attempt,
                result.OutputRelativeName,
                result.Sha256,
                result.ByteLength,
                bootstrap.MaximumOutputBytes);
            Mo2SnapshotCaptureResult captured =
                JsonSerializer.Deserialize<Mo2SnapshotCaptureResult>(
                    bytes,
                    StrictJson)
                ?? throw new InvalidOperationException(
                    "The staged snapshot result is not valid JSON.");
            ValidateCapturedSnapshot(captured, assignment);
            string snapshotId = captured.Snapshot!.Contract.SnapshotId.Value;
            _ = runtime.Store.AdmitSnapshotCapturePayload(
                attempt,
                result.OutputRelativeName,
                result.Sha256,
                result.ByteLength,
                result.ManifestSha256,
                bootstrap.MaximumOutputBytes,
                snapshotId,
                bootstrap.StagedArtifactId,
                DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "MO2 snapshot capture failed for operation {OperationId}.",
                operationId);
            if (attempt is not null)
            {
                try
                {
                    runtime.Store.FailSnapshotCapture(
                        attempt,
                        runtime.Authority.FencingEpoch,
                        DateTimeOffset.UtcNow);
                }
                catch (Exception failure)
                {
                    logger.LogError(
                        failure,
                        "Failed to persist snapshot capture failure for {OperationId}.",
                        operationId);
                }
            }
        }
    }

    internal static void ValidateCapturedSnapshot(
        Mo2SnapshotCaptureResult result,
        ManagedMo2SnapshotCaptureAssignment assignment,
        IExecutableAdmissionService? executableAdmissions = null)
    {
        if (result.State is not (
                SnapshotCaptureState.Completed
                or SnapshotCaptureState.CompletedWithGaps)
            || result.Snapshot is null
            || result.Snapshot.Contract.SchemaVersion
                != new Infinium.Domain.Contracts.ContractVersion(3, 0, 0)
            || string.IsNullOrWhiteSpace(result.Snapshot.Contract.SnapshotId.Value)
            || result.Snapshot.Mo2OrUsvfsLaunched
            || result.Snapshot.Mo2Admission.State != AdmissionState.Accepted
            || result.Snapshot.SkyrimGamePluginAdmission.State != AdmissionState.Accepted
            || result.Snapshot.RuntimeAdmission.State != AdmissionState.Accepted
            || !result.Gaps.SequenceEqual(result.Snapshot.Gaps)
            || (result.State == SnapshotCaptureState.Completed
                && result.Gaps.Count != 0)
            || (result.State == SnapshotCaptureState.CompletedWithGaps
                && result.Gaps.Count == 0)
            || !string.Equals(
                Path.GetFullPath(result.Snapshot.InstanceRoot),
                Path.GetFullPath(assignment.InstanceRoot),
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                Path.GetFullPath(result.Snapshot.ProfileRoot),
                Path.Combine(
                    Path.GetFullPath(assignment.ProfilesRoot),
                    assignment.SelectedProfileName),
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                result.Snapshot.AdapterId,
                "infinium.mo2-static-reconstruction/v3",
                StringComparison.Ordinal)
            || !string.Equals(
                result.Snapshot.Mo2Admission.ManifestId,
                "infinium.mo2-2.5.2-local-research/v1",
                StringComparison.Ordinal)
            || !string.Equals(
                result.Snapshot.SkyrimGamePluginAdmission.ManifestId,
                "infinium.mo2-game-skyrimse-2.5.2-local-research/v1",
                StringComparison.Ordinal)
            || !string.Equals(
                result.Snapshot.RuntimeAdmission.ManifestId,
                "infinium.skyrimse-1.6.1170-steam/v1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The staged snapshot fails coordinator publication validation.");
        }

        string structural =
            result.Snapshot.Contract.StructuralManifestFingerprint.Value;
        if (structural.Length != 64)
        {
            throw new InvalidOperationException(
                "The staged snapshot structural fingerprint is malformed.");
        }

        string expectedSnapshotId =
            Mo2SnapshotCanonicalization.ComputeSnapshotId(
                result.Snapshot.Contract.StructuralManifestFingerprint,
                result.Snapshot.Contract.CapturedAt).Value;
        if (!string.Equals(
                result.Snapshot.Contract.SnapshotId.Value,
                expectedSnapshotId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The staged snapshot identities are malformed.");
        }

        executableAdmissions ??= new SupportedExecutableManifests();
        string gamePluginPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(assignment.Mo2ExecutablePath))
                ?? throw new InvalidOperationException(
                    "The assigned MO2 executable has no application directory."),
            "plugins",
            "game_skyrimse.dll");
        if (!AdmissionEquals(
                executableAdmissions.AdmitMo2(assignment.Mo2ExecutablePath),
                result.Snapshot.Mo2Admission)
            || !AdmissionEquals(
                executableAdmissions.AdmitSkyrimGamePlugin(gamePluginPath),
                result.Snapshot.SkyrimGamePluginAdmission)
            || !AdmissionEquals(
                executableAdmissions.AdmitSkyrim(
                    assignment.SkyrimExecutablePath,
                    new RuntimeTargetContext(
                        assignment.Platform,
                        assignment.DistributionChannel,
                        assignment.ApplicationId)),
                result.Snapshot.RuntimeAdmission))
        {
            throw new InvalidOperationException(
                "The staged executable admissions do not match coordinator observations.");
        }

        ValidateDependencies(result.Snapshot, assignment, structural);
        ValidateSemanticGraph(result.Snapshot);
    }

    private static void ValidateDependencies(
        Mo2InstallationSnapshot snapshot,
        ManagedMo2SnapshotCaptureAssignment assignment,
        string structuralFingerprint)
    {
        Mo2SnapshotDependencyManifest dependencies = snapshot.Dependencies
            ?? throw new InvalidOperationException(
                "The staged snapshot dependency manifest is absent.");
        if (dependencies.SchemaVersion
                != new Infinium.Domain.Contracts.ContractVersion(1, 0, 0)
            || !string.Equals(
                dependencies.CanonicalFingerprint.Value,
                structuralFingerprint,
                StringComparison.OrdinalIgnoreCase)
            || dependencies.AdapterId != snapshot.AdapterId
            || dependencies.ManagerId != "mod-organizer-2"
            || dependencies.ExplicitSelectedProfileName
                != assignment.SelectedProfileName
            || dependencies.DeclaredRuntimeTarget
                != new RuntimeTargetContext(
                    assignment.Platform,
                    assignment.DistributionChannel,
                    assignment.ApplicationId)
            || dependencies.Mo2ExecutableIdentity
                != snapshot.Mo2Admission.ObservedIdentity
            || dependencies.SkyrimGamePluginIdentity
                != snapshot.SkyrimGamePluginAdmission.ObservedIdentity
            || dependencies.RuntimeExecutableIdentity
                != snapshot.RuntimeAdmission.ObservedIdentity
            || !CanonicalShaSetEquals(
                dependencies.EnabledMapperSha256s,
                assignment.EnabledMapperSha256s)
            || !CanonicalShaSetEquals(
                dependencies.QualifiedMapperSha256s,
                SupportedExecutableManifests.QualifiedMapperSha256s))
        {
            throw new InvalidOperationException(
                "The staged snapshot dependency bindings are inconsistent.");
        }

        Sha256Fingerprint recomputed = Mo2SnapshotCanonicalization.Compute(
            dependencies,
            snapshot.Contract.Mo2InstanceId,
            snapshot.Contract.ProfileId);
        if (!string.Equals(
                recomputed.Value,
                structuralFingerprint,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The staged snapshot canonical fingerprint is inconsistent.");
        }

        foreach (SnapshotControlObservation control in dependencies.ControlObservations)
        {
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(control.Base64Bytes);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    "A staged raw control observation is malformed.",
                    exception);
            }

            string observed = Convert.ToHexString(SHA256.HashData(bytes))
                .ToLowerInvariant();
            if (bytes.LongLength != control.ByteLength
                || control.Exists != (control.PhysicalObjectIdentity is not null)
                || !string.Equals(
                    observed,
                    control.Fingerprint.Value,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A staged raw control observation does not match its fingerprint.");
            }
        }

        Dictionary<string, SnapshotMappingDependency> observedMappings;
        try
        {
            observedMappings = dependencies.MappingDependencies.ToDictionary(
                mapping => mapping.MappingId,
                StringComparer.Ordinal);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "The staged mapping dependencies contain duplicate identities.",
                exception);
        }

        if (observedMappings.Count != assignment.QualifiedMappings.Count
            || assignment.QualifiedMappings.Any(expected =>
                !observedMappings.TryGetValue(
                    expected.MappingId,
                    out SnapshotMappingDependency? observed)
                || !string.Equals(
                    Path.GetFullPath(observed.SourceRoot),
                    Path.GetFullPath(expected.SourceRoot),
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    observed.VirtualPrefix,
                    expected.VirtualPrefix.Replace('\\', '/').Trim('/'),
                    StringComparison.Ordinal)
                || !string.Equals(
                    observed.MapperFingerprint.Value,
                    expected.MapperSha256,
                    StringComparison.OrdinalIgnoreCase)
                || observed.Admitted != (
                    assignment.EnabledMapperSha256s.Contains(
                        expected.MapperSha256,
                        StringComparer.OrdinalIgnoreCase)
                    && SupportedExecutableManifests.QualifiedMapperSha256s.Contains(
                        expected.MapperSha256))))
        {
            throw new InvalidOperationException(
                "The staged mapping dependencies do not match the assignment.");
        }

        Dictionary<string, string> expectedRoots =
            new(StringComparer.Ordinal)
            {
                ["instance"] = Path.GetFullPath(assignment.InstanceRoot),
                ["profile"] = Path.Combine(
                    Path.GetFullPath(assignment.ProfilesRoot),
                    assignment.SelectedProfileName),
                ["mods"] = Path.GetFullPath(assignment.ModsRoot),
                ["overwrite"] = Path.GetFullPath(assignment.OverwriteRoot),
                ["game-data"] = Path.GetFullPath(assignment.GameDataRoot),
            };
        foreach (ManagedQualifiedMappingAssignment mapping in assignment.QualifiedMappings)
        {
            if (observedMappings[mapping.MappingId].Admitted)
            {
                expectedRoots.Add(
                    $"mapping:{mapping.MappingId}",
                    Path.GetFullPath(mapping.SourceRoot));
            }
        }

        Dictionary<string, string> observedRoots;
        try
        {
            observedRoots = dependencies.RootObservations.ToDictionary(
                root => root.Role,
                root => Path.GetFullPath(root.SourcePath),
                StringComparer.Ordinal);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "The staged dependency roots contain duplicate roles.",
                exception);
        }

        if (observedRoots.Count != expectedRoots.Count
            || expectedRoots.Any(expected =>
                !observedRoots.TryGetValue(expected.Key, out string? observed)
                || !string.Equals(
                    observed,
                    expected.Value,
                    StringComparison.OrdinalIgnoreCase))
            || dependencies.StructuralObservations.Any(observation =>
                !expectedRoots.ContainsKey(observation.RootRole)))
        {
            throw new InvalidOperationException(
                "The staged dependency roots do not match the assignment.");
        }

    }

    private static void ValidateSemanticGraph(Mo2InstallationSnapshot snapshot)
    {
        HashSet<OpaqueId> entityIds =
            snapshot.LocalInstalledEntities
                .Select(entity => entity.EntityId)
                .ToHashSet();
        if (entityIds.Count != snapshot.LocalInstalledEntities.Count
            || !entityIds.SetEquals(snapshot.Contract.LocalInstalledEntityIds)
            || snapshot.Contract.LocalInstalledEntityIds.Count
                != entityIds.Count
            || snapshot.Mods.Any(mod =>
                !entityIds.Contains(mod.LocalInstalledEntityId)
                || (mod.Listed && mod.Priority is null)
                || (!mod.Listed && mod.Priority is not null))
            || snapshot.PhysicalInventory.Any(entry =>
                !entityIds.Contains(entry.LocalInstalledEntityId)
                || entry.ByteLength < 0
                || Path.IsPathRooted(entry.RelativePath)
                || entry.RelativePath.Split('/', '\\').Contains(".."))
            || snapshot.Plugins.Any(plugin =>
                plugin.WinningLocalInstalledEntityId is not null
                && !entityIds.Contains(plugin.WinningLocalInstalledEntityId))
            || snapshot.Plugins
                .Where(plugin => plugin.LoadOrder is not null)
                .GroupBy(plugin => plugin.LoadOrder)
                .Any(group => group.Count() != 1)
            || snapshot.Contract.Assurance
                .GroupBy(value => value.Population, StringComparer.Ordinal)
                .Any(group => group.Count() != 1)
            || snapshot.Contract.Assurance.Any(value =>
                value.DeclaredCount < 0
                || value.CapturedCount < 0
                || value.CapturedCount > value.DeclaredCount))
        {
            throw new InvalidOperationException(
                "The staged snapshot semantic graph is inconsistent.");
        }

        HashSet<string> providerPaths = new(StringComparer.OrdinalIgnoreCase);
        foreach (LooseProviderChain chain in snapshot.LooseProviderChains)
        {
            if (!providerPaths.Add(chain.NormalizedRelativePath)
                || Path.IsPathRooted(chain.NormalizedRelativePath)
                || chain.NormalizedRelativePath.Split('/', '\\').Contains("..")
                || chain.Providers.Count == 0
                || chain.Providers.Any(provider =>
                    !entityIds.Contains(provider.LocalInstalledEntityId))
                || !chain.Providers.Contains(chain.Winner)
                || chain.Winner.Priority
                    != chain.Providers.Max(provider => provider.Priority))
            {
                throw new InvalidOperationException(
                    "The staged provider graph is inconsistent.");
            }
        }

        if (snapshot.Dependencies.RootObservations.Any(root =>
                string.IsNullOrWhiteSpace(root.PhysicalObjectIdentity))
            || snapshot.Dependencies.StructuralObservations.Any(observation =>
                Path.IsPathRooted(observation.RelativePath)
                || observation.RelativePath.Split('/', '\\').Contains("..")))
        {
            throw new InvalidOperationException(
                "The staged structural observations are inconsistent.");
        }
    }

    private static bool CanonicalShaSetEquals(
        IEnumerable<string> observed,
        IEnumerable<string> expected)
    {
        try
        {
            string[] observedValues = observed
                .Select(value => new Sha256Fingerprint(value).Value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] expectedValues = expected
                .Select(value => new Sha256Fingerprint(value).Value)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return observedValues.Length
                    == observedValues.Distinct(StringComparer.Ordinal).Count()
                && expectedValues.Length
                    == expectedValues.Distinct(StringComparer.Ordinal).Count()
                && observedValues.SequenceEqual(
                    expectedValues,
                    StringComparer.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool AdmissionEquals(
        ExecutableAdmission coordinator,
        ExecutableAdmission staged) =>
        coordinator.State == staged.State
        && string.Equals(
            coordinator.ManifestId,
            staged.ManifestId,
            StringComparison.Ordinal)
        && coordinator.ObservedIdentity == staged.ObservedIdentity
        && coordinator.Reasons.SequenceEqual(staged.Reasons, StringComparer.Ordinal);
}

#pragma warning restore CA1848
