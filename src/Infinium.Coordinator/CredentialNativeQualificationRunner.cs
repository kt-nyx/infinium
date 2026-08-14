using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Google.Protobuf;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Common.V1;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Coordinator;

internal sealed record CredentialNativeRetainedSurfaceEvidence(
    string Name,
    string SecretCanaryProof,
    string RawTargetCanaryProof,
    long ByteCount,
    string Basis);

internal enum CredentialNativeEvidenceFinalizationFault
{
    None,
    CoordinatorArtifactScan,
    BackupMetadataWrite,
    FinalEvidenceWrite,
    SuccessSummaryWrite,
}

internal sealed class CredentialNativeEvidenceFinalizationException(
    string stage,
    IOException innerException)
    : IOException("Credential-native evidence finalization failed at a closed stage.", innerException)
{
    internal string Stage { get; } = stage;
}

internal static class CredentialNativeQualificationRunner
{
    private const string RestoredGenerationRejectionMessage =
        "SQLite Error 19: 'restored credential recovery cannot reactivate the restored generation'.";
    private const string RestoreAuthorityTimeRegressionMessage =
        "SQLite Error 19: 'provider credential lifecycle time regression'.";

    private static readonly CredentialNativeRetainedSurfaceEvidence[] ExactRetainedSurfaceInventory =
    [
        new("final credential-native evidence JSON", "structurally-absent", "structurally-absent", 0,
            "typed serializer inputs contain only non-secret receipts, separate profile/generation identities, and target fingerprints; no secret or concatenated raw target is an input"),
        new("final human summary", "structurally-absent", "byte-scanned-utf8-and-utf16le", 0,
            "fixed summary contains no secret and final bytes are scanned for both raw-target encodings"),
        new("CredentialNative gate stdout", "structurally-absent", "structurally-absent", 0,
            "the coordinator qualification success path writes no stdout bytes"),
        new("CredentialNative gate stderr", "structurally-absent", "structurally-absent", 0,
            "the success path writes no stderr bytes and typed failure output contains only a fixed prefix and exception type"),
    ];

    private static readonly JsonSerializerOptions EvidenceJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    internal static async Task<int> RunAsync(
        string manifestPath,
        string outputRoot,
        CancellationToken cancellationToken = default)
    {
        manifestPath = Path.GetFullPath(manifestPath);
        outputRoot = Path.GetFullPath(outputRoot);
        if (!File.Exists(manifestPath) || !Directory.Exists(outputRoot)
            || Directory.EnumerateFileSystemEntries(outputRoot).Any())
        {
            throw new InvalidDataException("WP4 v2 requires one exact manifest and a fresh empty output root.");
        }
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        string manifestSha256 = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        using JsonDocument document = JsonDocument.Parse(manifestBytes);
        string manifestId = document.RootElement.GetProperty("manifest_id").GetString()
            ?? throw new InvalidDataException("The v2 manifest identity is absent.");
        Dictionary<string, Target> targets = ParseTargets(document.RootElement);
        Dictionary<string, string> fingerprints = targets.Values.ToDictionary(
            item => item.ProfileId + "/" + item.GenerationId,
            item => item.Fingerprint,
            StringComparer.Ordinal);
        string helper = Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe");
        string helperSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(helper)));
        OneShotCredentialHelperLauncher launcher = OneShotCredentialHelperLauncher.CreateNativeQualification(
            helper, helperSha256, manifestPath, manifestSha256, manifestId);
        RunnerState state = new(outputRoot, launcher, targets, fingerprints, DateTimeOffset.UtcNow);
        CredentialNativeQualificationEvidence evidence;
        try
        {
            try
            {
                evidence = await state.RunAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (CredentialNativePreflightCollisionException collision)
            {
                WriteJson(Path.Combine(outputRoot, "credential-native-collision.v2.json"), new
                {
                    schema = "infinium.m1-s6.wp4.credential-native-collision/v2",
                    status = "failed-preflight-collision",
                    manifest_id = manifestId,
                    manifest_sha256 = manifestSha256,
                    collision.AssignmentId,
                    collision.TargetFingerprintSha256,
                    namespace_blocked = true,
                    later_native_calls = 0,
                    disposition = "terminal-fresh-owner-authority-required",
                });
                throw;
            }
            catch (CredentialNativeCleanupAmbiguityException ambiguity)
            {
                WriteJson(
                    Path.Combine(outputRoot, "credential-native-cleanup-ambiguity.v3.json"),
                    BuildCleanupAmbiguityArtifact(manifestId, manifestSha256, ambiguity));
                throw;
            }
            catch (CredentialNativePrimaryFailureException failure)
            {
                WriteJson(
                    Path.Combine(outputRoot, "credential-native-primary-failure.v2.json"),
                    BuildPrimaryFailureArtifact(manifestId, manifestSha256, failure));
                throw;
            }
        }
        finally
        {
            state.Dispose();
        }
        WriteOutputs(outputRoot, manifestId, manifestSha256, evidence, state);
        return 0;
    }

    internal static async Task<CredentialNativeQualificationEvidence> RunWithLauncherForTestAsync(
        string root,
        OneShotCredentialHelperLauncher launcher,
        IReadOnlyDictionary<string, (string ProfileId, string GenerationId)> targetIdentities,
        DateTimeOffset now,
        Exception? primaryFailureForTest = null,
        Exception? cleanupFailureForTest = null,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, Target> targets = targetIdentities.ToDictionary(
            item => item.Key,
            item => new Target(item.Key, item.Value.ProfileId, item.Value.GenerationId, new('0', 64)),
            StringComparer.Ordinal);
        using RunnerState state = new(root, launcher, targets, new Dictionary<string, string>(), now,
            primaryFailureForTest, cleanupFailureForTest);
        return await state.RunAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<CredentialNativeQualificationEvidence> RunWithLauncherAndWriteOutputsForTestAsync(
        string root,
        OneShotCredentialHelperLauncher launcher,
        IReadOnlyDictionary<string, (string ProfileId, string GenerationId)> targetIdentities,
        DateTimeOffset now,
        CredentialNativeEvidenceFinalizationFault fault = CredentialNativeEvidenceFinalizationFault.None,
        Action<string>? beforeFinalizationForTest = null,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, Target> targets = targetIdentities.ToDictionary(
            item => item.Key,
            item => new Target(item.Key, item.Value.ProfileId, item.Value.GenerationId, new('0', 64)),
            StringComparer.Ordinal);
        RunnerState state = new(root, launcher, targets, new Dictionary<string, string>(), now);
        CredentialNativeQualificationEvidence evidence;
        try
        {
            evidence = await state.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            state.Dispose();
        }
        beforeFinalizationForTest?.Invoke(root);
        WriteOutputs(root, "manifest/test", new string('a', 64), evidence, state, fault);
        return evidence;
    }

    internal static bool IsExpectedRestoredGenerationRejectionForTest(SqliteException exception) =>
        IsExpectedRestoredGenerationRejection(exception);

    internal static void ApplyRestoredGenerationRejectionFilterForTest(SqliteException exception)
    {
        try
        {
            throw exception;
        }
        catch (SqliteException expected) when (IsExpectedRestoredGenerationRejection(expected))
        {
        }
    }

    private static bool IsExpectedRestoredGenerationRejection(SqliteException exception) =>
        exception.SqliteErrorCode == 19
        && exception.SqliteExtendedErrorCode == 1811
        && string.Equals(exception.Message, RestoredGenerationRejectionMessage, StringComparison.Ordinal);

    private static object? BuildSqliteFailureEvidence(Exception exception)
    {
        if (exception is not SqliteException sqlite) { return null; }
        string classification;
        string? retainedMessage;
        if (sqlite.SqliteErrorCode == 19
            && sqlite.SqliteExtendedErrorCode == 1811
            && string.Equals(sqlite.Message, RestoreAuthorityTimeRegressionMessage, StringComparison.Ordinal))
        {
            classification = "credential-authority-time-regression";
            retainedMessage = RestoreAuthorityTimeRegressionMessage;
        }
        else if (sqlite.SqliteErrorCode == 19
            && sqlite.SqliteExtendedErrorCode == 1811
            && string.Equals(sqlite.Message, RestoredGenerationRejectionMessage, StringComparison.Ordinal))
        {
            classification = "restored-generation-reactivation-rejected";
            retainedMessage = RestoredGenerationRejectionMessage;
        }
        else
        {
            classification = "unclassified-redacted";
            retainedMessage = null;
        }
        return new
        {
            primary_code = sqlite.SqliteErrorCode,
            extended_code = sqlite.SqliteExtendedErrorCode,
            classification,
            message = retainedMessage,
        };
    }

    private static object? BuildTerminalFailureEvidence(Exception? exception)
    {
        if (exception is null) { return null; }
        CredentialNativeHelperFailureException? helperFailure = exception as CredentialNativeHelperFailureException;
        CredentialNativeHelperEvidenceAmbiguityException? helperEvidenceAmbiguity =
            exception as CredentialNativeHelperEvidenceAmbiguityException;
        return new
        {
            failure_type = exception.GetType().Name,
            classification = helperFailure is not null
                ? "typed-helper-failure"
                : helperEvidenceAmbiguity is not null
                    ? "typed-helper-evidence-ambiguity"
                    : exception is SqliteException
                        ? "typed-sqlite-failure"
                        : "typed-redacted-non-sqlite-failure",
            sqlite_failure = BuildSqliteFailureEvidence(exception),
            helper_failure = helperFailure?.Evidence,
            helper_failure_containment = helperFailure?.Containment,
            helper_evidence_ambiguity = helperEvidenceAmbiguity is null ? null : new
            {
                assignment_id = helperEvidenceAmbiguity.AssignmentId,
                validation_stage = helperEvidenceAmbiguity.ValidationStage,
            },
            helper_evidence_ambiguity_containment = helperEvidenceAmbiguity?.Containment,
        };
    }

    private static NativeHelperFailureContainmentEvidence? FailureContainment(Exception? exception) => exception switch
    {
        CredentialNativeHelperFailureException helperFailure => helperFailure.Containment,
        CredentialNativeHelperEvidenceAmbiguityException helperEvidenceAmbiguity => helperEvidenceAmbiguity.Containment,
        _ => null,
    };

    internal static async Task RunCleanupFailureWithArtifactsForTestAsync(
        string root,
        OneShotCredentialHelperLauncher launcher,
        IReadOnlyDictionary<string, (string ProfileId, string GenerationId)> targetIdentities,
        DateTimeOffset now,
        Exception cleanupFailureForTest,
        Exception? primaryFailureForTest = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cleanupFailureForTest);
        try
        {
            _ = await RunWithLauncherForTestAsync(
                root, launcher, targetIdentities, now,
                primaryFailureForTest: primaryFailureForTest,
                cleanupFailureForTest: cleanupFailureForTest,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CredentialNativeCleanupAmbiguityException ambiguity)
        {
            WriteJson(
                Path.Combine(root, "credential-native-cleanup-ambiguity.v3.json"),
                BuildCleanupAmbiguityArtifact("manifest/test", new string('a', 64), ambiguity));
            throw;
        }
    }

    private static Dictionary<string, Target> ParseTargets(JsonElement root)
    {
        Dictionary<string, Target> targets = new(StringComparer.Ordinal);
        foreach (JsonElement item in root.GetProperty("disposable_namespace").GetProperty("targets").EnumerateArray())
        {
            Target target = new(
                item.GetProperty("alias").GetString()!,
                item.GetProperty("access_profile_id").GetString()!,
                item.GetProperty("generation_id").GetString()!,
                item.GetProperty("target_fingerprint_sha256").GetString()!);
            if (!targets.TryAdd(target.Alias, target))
            {
                throw new InvalidDataException("The WP4 v2 manifest repeats a target alias.");
            }
        }
        string[] required = ["interactive-primary", "interactive-cancel", "size-valid", "size-oversize",
            "unavailable-store", "replacement-old", "replacement-new", "revoke-delete", "crash-restart",
            "backup-old", "backup-new", "fake-dispatch"];
        if (targets.Count != required.Length || required.Any(alias => !targets.ContainsKey(alias)))
        {
            throw new InvalidDataException("The WP4 v2 manifest target set is not exact.");
        }
        return targets;
    }

    private static void WriteOutputs(
        string outputRoot,
        string manifestId,
        string manifestSha256,
        CredentialNativeQualificationEvidence evidence,
        RunnerState state,
        CredentialNativeEvidenceFinalizationFault fault = CredentialNativeEvidenceFinalizationFault.None)
    {
        string summary = $"WP4 v2 passed. manifest_id={manifestId} manifest_sha256={manifestSha256} scenarios=9 targets=12 cleanup=confirmed-absent\n";
        List<CanonicalCallTraceEntry> trace = [];
        long globalSequence = 0;
        long globalAllocation = 0;
        foreach (CredentialNativeQualificationPhaseEvidence phase in evidence.Scenarios.SelectMany(item => item.Phases))
        {
            Dictionary<long, long> allocations = [];
            foreach (CredentialNativeCallTraceEntry item in phase.Process.NativeCallTrace)
            {
                long? allocationId = null;
                long? pairedAllocationId = null;
                if (item.AllocationId is long localAllocation)
                {
                    allocationId = ++globalAllocation;
                    allocations.Add(localAllocation, allocationId.Value);
                }
                if (item.PairedAllocationId is long localPair)
                {
                    pairedAllocationId = allocations.TryGetValue(localPair, out long mapped)
                        ? mapped
                        : throw new InvalidDataException("The canonical trace contains an unmapped free allocation.");
                }
                trace.Add(new(
                    ++globalSequence,
                    "credential-helper",
                    phase.Process.ProcessId,
                    item.Sequence,
                    item.Operation,
                    item.TargetFingerprintSha256,
                    item.Scenario,
                    item.Result,
                    allocationId,
                    pairedAllocationId));
            }
        }
        TargetAbsence[] absence = state.Targets.Values.Select(target => new TargetAbsence(
            target.Alias,
            target.Fingerprint,
            trace.LastOrDefault(item => item.TargetFingerprintSha256 == target.Fingerprint)?.Result
                ?? "missing")).ToArray();
        CredentialNativeCanarySurfaceEvidence[] phaseSurfaces = evidence.Scenarios.SelectMany(item => item.Phases)
            .Where(item => item.Canaries is not null)
            .SelectMany(item => item.Canaries!.ScannedSurfaces)
            .ToArray();
        bool finalEvidenceRetained = false;
        bool summaryRetained = false;
        CredentialNativeCanarySurfaceEvidence[]? coordinatorSurfaces = null;
        string stage = "backup-metadata-write";
        try
        {
            ThrowFinalizationFault(fault, CredentialNativeEvidenceFinalizationFault.BackupMetadataWrite);
            WriteJsonAtomically(
                Path.Combine(outputRoot, "native-backup-metadata.v2.json"),
                state.BackupEvidence ?? throw new InvalidOperationException("Backup/restore evidence was not produced."));

            stage = "coordinator-artifact-scan";
            ThrowFinalizationFault(fault, CredentialNativeEvidenceFinalizationFault.CoordinatorArtifactScan);
            coordinatorSurfaces = ScanCoordinatorArtifacts(outputRoot, state.Targets.Values).ToArray();
            CredentialNativeCanarySurfaceEvidence[] surfaces = phaseSurfaces.Concat(coordinatorSurfaces).ToArray();
            object output = new
            {
                schema = "infinium.m1-s6.wp4.credential-native-evidence/v2",
                status = "passed",
                manifest_id = manifestId,
                manifest_sha256 = manifestSha256,
                evidence.StartedAt,
                evidence.CompletedAt,
                deadline = new
                {
                    evidence.PrimaryPhaseSeconds,
                    evidence.CleanupReserveSeconds,
                    evidence.EvidenceReserveSeconds,
                    evidence.OuterWallClockSeconds,
                },
                evidence.CleanupAmbiguous,
                evidence.NamespaceBlocked,
                native_call_counts = evidence.NativeCallCounts,
                native_call_trace = trace,
                stale_gate = evidence.StaleGate,
                evidence.Scenarios,
                target_absence = absence,
                canaries = new
                {
                    secret_matches = surfaces.Sum(item => item.SecretMatches),
                    raw_target_matches = surfaces.Sum(item => item.RawTargetMatches),
                    raw_target_encodings = new[] { "utf-8", "utf-16le" },
                    scanned_surfaces = surfaces,
                    retained_surface_inventory = RetainedSurfaceInventory(Encoding.UTF8.GetByteCount(summary)),
                },
                network_operations = evidence.Scenarios.SelectMany(item => item.Phases).Sum(item => item.Process.NetworkOperationCount),
                dns_operations = 0,
                provider_operations = 0,
                billable_operations = 0,
                process_tree_survivors = evidence.Scenarios.SelectMany(item => item.Phases).Sum(item => item.Process.ProcessTreeSurvivorCount),
                retry_attempted = evidence.Scenarios.SelectMany(item => item.Phases).Any(item => item.Process.RetryAttempted),
            };

            stage = "final-evidence-write";
            ThrowFinalizationFault(fault, CredentialNativeEvidenceFinalizationFault.FinalEvidenceWrite);
            WriteJsonAtomically(Path.Combine(outputRoot, "credential-native-evidence.v2.json"), output);
            finalEvidenceRetained = true;

            stage = "success-summary-write";
            ThrowFinalizationFault(fault, CredentialNativeEvidenceFinalizationFault.SuccessSummaryWrite);
            WriteTextAtomically(Path.Combine(outputRoot, "credential-native-summary.txt"), summary);
            summaryRetained = true;
        }
        catch (IOException exception)
        {
            object failure = new
            {
                schema = "infinium.m1-s6.wp4.credential-native-evidence-finalization-failure/v1",
                status = "failed-post-success-evidence-finalization",
                manifest_id = manifestId,
                manifest_sha256 = manifestSha256,
                finalization_stage = stage,
                failure_type = nameof(IOException),
                failure_detail_retained = false,
                runner_completed = true,
                cleanup_confirmed = !evidence.CleanupAmbiguous,
                namespace_blocked = true,
                namespace_reuse_blocked = true,
                later_native_calls = 0,
                disposition = "terminal-cleanup-recovery-authority-required-never-reuse",
                final_evidence_retained = finalEvidenceRetained,
                success_summary_retained = summaryRetained,
                backup_metadata_retained = File.Exists(Path.Combine(outputRoot, "native-backup-metadata.v2.json")),
                native_call_counts = evidence.NativeCallCounts,
                native_call_trace = trace,
                target_absence = absence,
                canaries = new
                {
                    known = coordinatorSurfaces is not null,
                    secret_matches = phaseSurfaces.Concat(coordinatorSurfaces ?? []).Sum(item => item.SecretMatches),
                    raw_target_matches = phaseSurfaces.Concat(coordinatorSurfaces ?? []).Sum(item => item.RawTargetMatches),
                    raw_target_encodings = new[] { "utf-8", "utf-16le" },
                    scanned_surfaces = phaseSurfaces.Concat(coordinatorSurfaces ?? []).ToArray(),
                },
                containment = new
                {
                    process_trees_terminated = evidence.Scenarios.SelectMany(item => item.Phases)
                        .All(item => item.Process.ProcessTreeTerminated),
                    survivor_count = evidence.Scenarios.SelectMany(item => item.Phases)
                        .Sum(item => item.Process.ProcessTreeSurvivorCount),
                },
                network_operations = evidence.Scenarios.SelectMany(item => item.Phases)
                    .Sum(item => item.Process.NetworkOperationCount),
                dns_operations = 0,
                provider_operations = 0,
                billable_operations = 0,
                retry_attempted = evidence.Scenarios.SelectMany(item => item.Phases)
                    .Any(item => item.Process.RetryAttempted),
            };
            WriteJsonAtomically(
                Path.Combine(outputRoot, "credential-native-evidence-finalization-failure.v1.json"),
                failure);
            throw new CredentialNativeEvidenceFinalizationException(stage, exception);
        }
    }

    private static void ThrowFinalizationFault(
        CredentialNativeEvidenceFinalizationFault actual,
        CredentialNativeEvidenceFinalizationFault expected)
    {
        if (actual == expected)
        {
            throw new IOException("Injected closed evidence-finalization failure.");
        }
    }

    private static void WriteJsonAtomically(string path, object value) => WriteBytesAtomically(
        path,
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, EvidenceJson) + "\n"));

    private static void WriteTextAtomically(string path, string value) =>
        WriteBytesAtomically(path, new UTF8Encoding(false).GetBytes(value));

    private static void WriteBytesAtomically(string path, byte[] bytes)
    {
        string fullPath = Path.GetFullPath(path);
        string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream stream = new(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16_384,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporaryPath, fullPath, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporaryPath)) { File.Delete(temporaryPath); }
        }
    }

    private static void WriteJson(string path, object value) => File.WriteAllText(
        path,
        JsonSerializer.Serialize(value, EvidenceJson) + "\n",
        new UTF8Encoding(false));

    internal static string SerializeEvidenceForTest(object value) =>
        JsonSerializer.Serialize(value, EvidenceJson);

    internal static string SerializePrimaryFailureArtifactForTest(
        string manifestId,
        string manifestSha256,
        CredentialNativePrimaryFailureException failure) =>
        JsonSerializer.Serialize(BuildPrimaryFailureArtifact(manifestId, manifestSha256, failure), EvidenceJson);

    internal static string SerializeCleanupAmbiguityArtifactForTest(
        string manifestId,
        string manifestSha256,
        CredentialNativeCleanupAmbiguityException ambiguity) =>
        JsonSerializer.Serialize(BuildCleanupAmbiguityArtifact(manifestId, manifestSha256, ambiguity), EvidenceJson);

    private static object BuildCleanupAmbiguityArtifact(
        string manifestId,
        string manifestSha256,
        CredentialNativeCleanupAmbiguityException ambiguity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestSha256);
        ArgumentNullException.ThrowIfNull(ambiguity);
        if (!ambiguity.Evidence.CleanupAmbiguous || !ambiguity.Evidence.NamespaceBlocked)
        {
            throw new InvalidDataException("Cleanup-ambiguity evidence must retain the terminal supervisor snapshot.");
        }
        Exception cause = ambiguity.InnerException
            ?? throw new InvalidDataException("Cleanup-ambiguity evidence must retain a typed cause.");
        CredentialNativeHelperFailureException? helperFailure = cause as CredentialNativeHelperFailureException;
        CredentialNativeHelperEvidenceAmbiguityException? helperEvidenceAmbiguity =
            cause as CredentialNativeHelperEvidenceAmbiguityException;
        CredentialNativeHelperFailureException? primaryHelperFailure =
            ambiguity.PriorPrimaryFailure as CredentialNativeHelperFailureException;
        CredentialNativeHelperEvidenceAmbiguityException? primaryHelperEvidenceAmbiguity =
            ambiguity.PriorPrimaryFailure as CredentialNativeHelperEvidenceAmbiguityException;
        NativeHelperFailureContainmentEvidence? terminalFailureContainment = FailureContainment(cause);
        NativeHelperFailureContainmentEvidence? primaryFailureContainment =
            FailureContainment(ambiguity.PriorPrimaryFailure);
        CredentialNativeQualificationPhaseEvidence[] phases = ambiguity.Evidence.Scenarios
            .SelectMany(item => item.Phases).ToArray();
        CredentialNativeRejectedPhaseEvidence? rejected = ambiguity.Evidence.RejectedPhase;
        int listenerCount = phases.Sum(item => item.Process.ListenerCount)
            + (rejected?.ListenerCount ?? 0)
            + (helperFailure?.Evidence.ListenerCount ?? 0)
            + (primaryHelperFailure?.Evidence.ListenerCount ?? 0);
        int networkOperationCount = phases.Sum(item => item.Process.NetworkOperationCount)
            + (rejected?.NetworkOperationCount ?? 0)
            + (helperFailure?.Evidence.NetworkOperationCount ?? 0)
            + (primaryHelperFailure?.Evidence.NetworkOperationCount ?? 0);
        int retainedProcessTreeSurvivorCount = phases.Sum(item => item.Process.ProcessTreeSurvivorCount)
            + (rejected?.ProcessTreeSurvivorCount ?? 0);
        int processTreeSurvivorCount = retainedProcessTreeSurvivorCount
            + (terminalFailureContainment?.ProcessTreeSurvivorCount ?? 0)
            + (primaryFailureContainment?.ProcessTreeSurvivorCount ?? 0);
        int secretCanaryMatches = phases.Sum(item => item.Canaries?.SecretMatches ?? 0)
            + (rejected?.Canaries?.SecretMatches ?? 0);
        int rawTargetCanaryMatches = phases.Sum(item => item.Canaries?.RawTargetMatches ?? 0)
            + (rejected?.Canaries?.RawTargetMatches ?? 0);
        bool processTreeFactsKnown = (helperFailure is null && helperEvidenceAmbiguity is null
                || terminalFailureContainment is not null)
            && (primaryHelperFailure is null && primaryHelperEvidenceAmbiguity is null
                || primaryFailureContainment is not null);
        bool processTreesTerminated = processTreeFactsKnown
            && phases.All(item => item.Process.ProcessTreeTerminated)
            && (rejected is null || rejected.ProcessTreeTerminated)
            && (terminalFailureContainment?.ProcessTreeTerminated ?? true)
            && (primaryFailureContainment?.ProcessTreeTerminated ?? true);
        return new
        {
            schema = "infinium.m1-s6.wp4.credential-native-cleanup-ambiguity/v3",
            status = "failed-cleanup-ambiguous",
            manifest_id = manifestId,
            manifest_sha256 = manifestSha256,
            ambiguity.AssignmentId,
            ambiguity.Reason,
            terminal_failure = BuildTerminalFailureEvidence(cause),
            prior_primary_failure = BuildTerminalFailureEvidence(ambiguity.PriorPrimaryFailure),
            cleanup_confirmed = false,
            whole_namespace_absence_confirmed = false,
            namespace_blocked = true,
            namespace_disposition = "consumed-never-reuse",
            validated_native_call_counts = ambiguity.Evidence.NativeCallCounts,
            rejected_phase_native_call_counts = rejected?.CanonicalCallCounts,
            native_call_count_scope =
                "validated phases plus separately retained rejected phase; rejected evidence is never merged into validated totals",
            canary_facts = new
            {
                secret_matches = secretCanaryMatches,
                raw_target_matches = rawTargetCanaryMatches,
                retained_in = "terminal-supervisor-snapshot-including-rejected-phase",
            },
            containment_facts = new
            {
                process_tree_facts_known = processTreeFactsKnown,
                process_trees_terminated = processTreesTerminated,
                process_tree_survivor_count = processTreeFactsKnown ? processTreeSurvivorCount : (int?)null,
                rejected_phase_process_id = rejected?.ProcessId,
                terminal_failure = terminalFailureContainment,
                prior_primary_failure = primaryFailureContainment,
            },
            external_effect_facts = new
            {
                network_facts_known = helperEvidenceAmbiguity is null
                    && primaryHelperEvidenceAmbiguity is null
                    && (helperFailure?.Evidence.NetworkFactsKnown ?? true)
                    && (primaryHelperFailure?.Evidence.NetworkFactsKnown ?? true),
                listener_count = listenerCount,
                network_operation_count = networkOperationCount,
                external_effect_facts_known = helperEvidenceAmbiguity is null
                    && primaryHelperEvidenceAmbiguity is null
                    && (helperFailure?.Evidence.ExternalEffectFactsKnown ?? true)
                    && (primaryHelperFailure?.Evidence.ExternalEffectFactsKnown ?? true),
                dns_operation_count = (helperFailure?.Evidence.DnsOperationCount ?? 0)
                    + (primaryHelperFailure?.Evidence.DnsOperationCount ?? 0),
                provider_operation_count = (helperFailure?.Evidence.ProviderOperationCount ?? 0)
                    + (primaryHelperFailure?.Evidence.ProviderOperationCount ?? 0),
                billable_operation_count = (helperFailure?.Evidence.BillableOperationCount ?? 0)
                    + (primaryHelperFailure?.Evidence.BillableOperationCount ?? 0),
                external_zero_basis = "source-proven-helper-and-synthetic-dispatch-have-no-provider-or-dns-transport",
            },
            evidence = ambiguity.Evidence,
            later_native_calls = 0,
            disposition = "terminal-fresh-owner-authority-required",
        };
    }

    private static object BuildPrimaryFailureArtifact(
        string manifestId,
        string manifestSha256,
        CredentialNativePrimaryFailureException failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestSha256);
        ArgumentNullException.ThrowIfNull(failure);
        if (failure.CleanupDisposition is not (
            "source-proven-pre-store-known-zero"
            or "trace-proven-preflight-absence"
            or "exact-target-cleanup-confirmed"))
        {
            throw new InvalidDataException("A primary failure artifact requires one independently proven absence disposition.");
        }
        CredentialNativeHelperFailureException? helperFailure =
            failure.InnerException as CredentialNativeHelperFailureException;
        CredentialNativeHelperEvidenceAmbiguityException? helperEvidenceAmbiguity =
            failure.InnerException as CredentialNativeHelperEvidenceAmbiguityException;
        bool cleanupConfirmed = failure.CleanupDisposition == "exact-target-cleanup-confirmed";
        bool singleTargetAbsence = failure.CleanupDisposition == "trace-proven-preflight-absence";
        string[] provenTargetFingerprints = cleanupConfirmed
            ? failure.Evidence.Scenarios.SelectMany(item => item.Phases)
                .Where(item => item.PhaseId.StartsWith("cleanup", StringComparison.Ordinal)
                    || item.PhaseId == "deleted-after-revocation")
                .SelectMany(item => item.Process.NativeCallTrace)
                .Where(item => item.Operation == "CredReadW" && item.Result == "ERROR_NOT_FOUND")
                .Select(item => item.TargetFingerprintSha256)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            : singleTargetAbsence && helperFailure?.Evidence.NativeCallTraceJson is string traceJson
                ? JsonSerializer.Deserialize<CredentialNativeCallTraceEntry[]>(traceJson, EvidenceJson)?
                    .Where(item => item.Operation == "CredReadW" && item.Result == "ERROR_NOT_FOUND")
                    .Select(item => item.TargetFingerprintSha256)
                    .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? []
                : [];
        if (singleTargetAbsence && provenTargetFingerprints.Length != 1
            || cleanupConfirmed && provenTargetFingerprints.Length == 0)
        {
            throw new InvalidDataException("The primary failure absence disposition lacks its exact target proof scope.");
        }
        return new
        {
            schema = "infinium.m1-s6.wp4.credential-native-primary-failure/v2",
            status = cleanupConfirmed
                ? "failed-primary-cleanup-confirmed"
                : singleTargetAbsence
                    ? "failed-primary-single-target-absence-confirmed"
                    : "failed-primary-effect-free-store-state-unobserved",
            manifest_id = manifestId,
            manifest_sha256 = manifestSha256,
            failure_type = failure.FailureType,
            terminal_failure = BuildTerminalFailureEvidence(failure.InnerException),
            sqlite_failure = BuildSqliteFailureEvidence(failure.InnerException!),
            cleanup_confirmed = cleanupConfirmed,
            absence_confirmed = cleanupConfirmed || singleTargetAbsence,
            whole_namespace_absence_confirmed = false,
            absence_scope = cleanupConfirmed
                ? "queued-exact-cleanup-targets-only"
                : singleTargetAbsence
                    ? "single-preflight-target-only"
                    : "none-store-state-unobserved",
            absence_target_fingerprints = provenTargetFingerprints,
            cleanup_disposition = failure.CleanupDisposition,
            namespace_blocked = true,
            namespace_disposition = "consumed-never-reuse",
            external_effect_facts = helperFailure is null ? null : new
            {
                helperFailure.Evidence.NetworkFactsKnown,
                helperFailure.Evidence.ListenerCount,
                helperFailure.Evidence.NetworkOperationCount,
                helperFailure.Evidence.ExternalEffectFactsKnown,
                helperFailure.Evidence.DnsOperationCount,
                helperFailure.Evidence.ProviderOperationCount,
                helperFailure.Evidence.BillableOperationCount,
                external_zero_basis = "source-proven-helper-has-no-provider-or-dns-transport",
            },
            later_native_calls = 0,
            helper_failure = helperFailure?.Evidence,
            helper_failure_containment = helperFailure?.Containment,
            helper_evidence_ambiguity = helperEvidenceAmbiguity is null ? null : new
            {
                assignment_id = helperEvidenceAmbiguity.AssignmentId,
                validation_stage = helperEvidenceAmbiguity.ValidationStage,
            },
            helper_evidence_ambiguity_containment = helperEvidenceAmbiguity?.Containment,
            evidence = failure.Evidence,
            disposition = "terminal-fresh-owner-authority-required",
        };
    }

    internal static IReadOnlyList<CredentialNativeRetainedSurfaceEvidence> RetainedSurfaceInventory(
        long summaryByteCount)
    {
        CredentialNativeRetainedSurfaceEvidence[] inventory = [.. ExactRetainedSurfaceInventory];
        inventory[1] = inventory[1] with { ByteCount = summaryByteCount };
        ValidateRetainedSurfaceInventory(inventory);
        return inventory;
    }

    internal static void ValidateRetainedSurfaceInventory(
        IReadOnlyList<CredentialNativeRetainedSurfaceEvidence> inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.Count != ExactRetainedSurfaceInventory.Length
            || inventory[1].ByteCount <= 0
            || inventory.Where((item, index) => item with { ByteCount = 0 }
                != ExactRetainedSurfaceInventory[index]).Any())
        {
            throw new InvalidDataException("The retained canary surface inventory is incomplete or has weakened a structural-absence proof.");
        }
    }

    private static IEnumerable<CredentialNativeCanarySurfaceEvidence> ScanCoordinatorArtifacts(
        string root,
        IEnumerable<Target> targets)
    {
        string[] rawTargets = targets
            .Select(target => $"Infinium:{target.ProfileId}:{target.GenerationId}")
            .ToArray();
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            byte[] bytes = File.ReadAllBytes(path);
            int matches = CountRawTargetMatches(bytes, rawTargets);
            yield return new(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                "coordinator-artifact-byte-scan",
                bytes.LongLength,
                SecretMatches: 0,
                RawTargetMatches: matches);
        }
    }

    internal static int CountRawTargetMatches(
        ReadOnlySpan<byte> bytes,
        IEnumerable<string> rawTargets)
    {
        int matches = 0;
        foreach (string rawTarget in rawTargets)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(rawTarget);
            byte[] utf16 = Encoding.Unicode.GetBytes(rawTarget);
            try
            {
                if (bytes.IndexOf(utf8) >= 0) { matches++; }
                if (bytes.IndexOf(utf16) >= 0) { matches++; }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(utf8);
                CryptographicOperations.ZeroMemory(utf16);
            }
        }
        return matches;
    }

    private sealed class RunnerState(
        string root,
        OneShotCredentialHelperLauncher launcher,
        Dictionary<string, Target> targets,
        IReadOnlyDictionary<string, string> fingerprints,
        DateTimeOffset baseTime,
        Exception? injectedPrimaryFailure = null,
        Exception? injectedCleanupFailure = null) : IDisposable
    {
        private readonly string root = Path.GetFullPath(root);
        private readonly OneShotCredentialHelperLauncher launcher = launcher;
        private readonly IReadOnlyDictionary<string, string> fingerprints = fingerprints;
        private readonly DateTimeOffset baseTime = baseTime;
        private DateTimeOffset timeline = baseTime;
        private int nonce;
        private int clock;
        private Exception? injectedPrimaryFailure = injectedPrimaryFailure;
        private Exception? injectedCleanupFailure = injectedCleanupFailure;
        private readonly List<(string Scenario, ScenarioContext Context, Target CleanupTarget)> cleanup = [];
        private readonly List<ScenarioContext> contexts = [];
        private bool disposed;

        internal IReadOnlyDictionary<string, Target> Targets => targets;
        internal object? BackupEvidence { get; private set; }

        internal async Task<CredentialNativeQualificationEvidence> RunAsync(CancellationToken token)
        {
            Directory.CreateDirectory(root);
            ScenarioContext initial = CreateContext("initial");
            using CredentialNativeQualificationSupervisor supervisor = new(
                initial.Coordinator, expectedInheritedPrivateHandleCount: fingerprints.Count == 0 ? 3 : 2,
                targetFingerprints: fingerprints);
            Exception? primaryFailure = null;
            try
            {
                foreach ((string scenario, string phase, Target target) in PreflightTargets())
                {
                    await supervisor.ExecutePreflightPhaseAsync(
                        scenario,
                        phase,
                        Bootstrap(target, target, NextNonce()),
                        Assignment(scenario, phase, target, target, target.GenerationId == "g002" ? 2UL : 1UL),
                        NextTime(),
                        token).ConfigureAwait(false);
                }
                await SimpleEnroll(supervisor, "interactive-entry-submit", "submit", targets["interactive-primary"], token);
                await SimpleEnroll(supervisor, "interactive-entry-cancel", "cancel", targets["interactive-cancel"], token);
                await SizeBoundaries(supervisor, token);
                await SimpleEnroll(supervisor, "secure-store-unavailable", "unavailable", targets["unavailable-store"], token);
                await Replacement(supervisor, token);
                await RevokeDelete(supervisor, token);
                await CrashRestart(supervisor, token);
                await BackupRestore(supervisor, token);
                await FakeDispatch(supervisor, token);
            }
            catch (Exception exception)
            {
                primaryFailure = exception;
            }

            if (primaryFailure is CredentialNativePreflightCollisionException collision)
            {
                throw collision;
            }
            if (primaryFailure is CredentialNativeCleanupAmbiguityException ambiguity)
            {
                supervisor.RecordTerminalCleanupAmbiguity(ambiguity.AssignmentId, ambiguity.Reason);
                throw new CredentialNativeCleanupAmbiguityException(
                    ambiguity.AssignmentId,
                    ambiguity.Reason,
                    supervisor.CaptureTerminalFailure(),
                    ambiguity.InnerException ?? ambiguity,
                    ambiguity.PriorPrimaryFailure);
            }

            if (primaryFailure is CredentialNativeHelperEvidenceAmbiguityException evidenceAmbiguity
                && !HasProvenContainment(evidenceAmbiguity.Containment, descendantExpected: false))
            {
                supervisor.RecordTerminalCleanupAmbiguity(
                    evidenceAmbiguity.AssignmentId, "helper-containment-unproven");
                throw new CredentialNativeCleanupAmbiguityException(
                    evidenceAmbiguity.AssignmentId,
                    "helper-containment-unproven",
                    supervisor.CaptureTerminalFailure(),
                    evidenceAmbiguity);
            }
            if (primaryFailure is CredentialNativeHelperFailureException preCleanupHelperFailure)
            {
                if (preCleanupHelperFailure.Evidence.NamespaceReuseBlocked)
                {
                    supervisor.RecordTerminalCleanupAmbiguity(
                        preCleanupHelperFailure.AssignmentId, "helper-namespace-reuse-blocked");
                    throw new CredentialNativeCleanupAmbiguityException(
                        preCleanupHelperFailure.AssignmentId,
                        "helper-namespace-reuse-blocked",
                        supervisor.CaptureTerminalFailure(),
                        preCleanupHelperFailure);
                }
                if (!HasProvenContainment(
                    preCleanupHelperFailure.Containment,
                    preCleanupHelperFailure.Evidence.ContainmentDescendantStarted))
                {
                    supervisor.RecordTerminalCleanupAmbiguity(
                        preCleanupHelperFailure.AssignmentId, "helper-containment-unproven");
                    throw new CredentialNativeCleanupAmbiguityException(
                        preCleanupHelperFailure.AssignmentId,
                        "helper-containment-unproven",
                        supervisor.CaptureTerminalFailure(),
                        preCleanupHelperFailure);
                }
            }

            supervisor.BeginCleanup();
            foreach ((string scenario, ScenarioContext context, Target target) in cleanup)
            {
                string phase = scenario == "revoke-delete"
                    ? "deleted-after-revocation"
                    : scenario == "replacement" && target.Alias == "replacement-old"
                        ? "cleanup-predecessor"
                        : scenario == "replacement"
                            ? "cleanup-successor"
                    : scenario == "backup-restore-reauthentication" && target.Alias == "backup-old"
                        ? "cleanup-restored-predecessor"
                        : scenario == "backup-restore-reauthentication"
                            ? "cleanup-successor"
                    : scenario == "credential-size-boundaries" && target.Alias == "size-valid"
                        ? "cleanup-maximum"
                        : scenario == "credential-size-boundaries"
                            ? "cleanup-oversize"
                            : "cleanup";
                try
                {
                    if (injectedCleanupFailure is not null)
                    {
                        Exception failure = injectedCleanupFailure;
                        injectedCleanupFailure = null;
                        throw failure;
                    }
                    supervisor.RebindCoordinator(context.Coordinator);
                    HelperPrivateFrameV2 cleanupBootstrap = Bootstrap(target, target, NextNonce());
                    HelperPrivateFrameV2 cleanupAssignment = Assignment(
                        scenario, phase, target, target, target.GenerationId is "g002" ? 2UL : 1UL);
                    bool unadmittedRestoredSuccessor =
                        primaryFailure is CredentialNativeHelperEvidenceAmbiguityException restoredEvidenceAmbiguity
                        && restoredEvidenceAmbiguity.AssignmentId ==
                            "wp4-v2/backup-restore-reauthentication/restored-new-generation"
                        && scenario == "backup-restore-reauthentication"
                        && phase == "cleanup-successor";
                    if (phase is "cleanup-predecessor" or "cleanup-restored-predecessor"
                        || unadmittedRestoredSuccessor)
                    {
                        await supervisor.ExecuteAbsenceOnlyCleanupPhaseAsync(
                            scenario, phase, cleanupBootstrap, cleanupAssignment, NextTime(), CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await supervisor.ExecuteCleanupPhaseAsync(
                            scenario, phase, Attempt(scenario, phase), cleanupBootstrap, cleanupAssignment,
                            NextTime(), CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    string assignmentId = $"wp4-v2/{scenario}/{phase}";
                    supervisor.RecordTerminalCleanupAmbiguity(assignmentId, "cleanup-phase-failed");
                    throw new CredentialNativeCleanupAmbiguityException(
                        assignmentId,
                        exception is CredentialNativeHelperEvidenceAmbiguityException
                            ? "cleanup-helper-evidence-invalid"
                            : exception is CredentialNativeHelperFailureException
                                ? "cleanup-helper-failure"
                                : "cleanup-phase-failed",
                        supervisor.CaptureTerminalFailure(),
                        exception,
                        primaryFailure);
                }
            }
            if (primaryFailure is not null)
            {
                string? cleanupDisposition = null;
                if (primaryFailure is CredentialNativeHelperFailureException boundedFailure)
                {
                    if (boundedFailure.Containment is not
                        { ProcessId: > 0, ProcessTreeTerminated: true, ProcessTreeSurvivorCount: 0 } containment
                        || containment.TotalContainedProcessCount
                            < (boundedFailure.Evidence.ContainmentDescendantStarted ? 2 : 1)
                        || containment.ActiveProcessCountBeforeJobClose < 0)
                    {
                        supervisor.RecordTerminalCleanupAmbiguity(
                            boundedFailure.AssignmentId, "helper-containment-unproven");
                        throw new CredentialNativeCleanupAmbiguityException(
                            boundedFailure.AssignmentId,
                            "helper-containment-unproven",
                            supervisor.CaptureTerminalFailure(),
                            boundedFailure);
                    }
                    cleanupDisposition = boundedFailure.Evidence.Stage is
                        "handle-inheritance" or "launch-boundary" or "manifest-validation"
                        ? "source-proven-pre-store-known-zero"
                        : boundedFailure.AssignmentId.Split('/')[^1].StartsWith("preflight", StringComparison.Ordinal)
                            ? "trace-proven-preflight-absence"
                            : cleanup.Count > 0
                                ? "exact-target-cleanup-confirmed"
                                : null;
                }
                else if (primaryFailure is CredentialNativeHelperEvidenceAmbiguityException
                    && cleanup.Count > 0)
                {
                    cleanupDisposition = "exact-target-cleanup-confirmed";
                }
                else if (cleanup.Count > 0)
                {
                    cleanupDisposition = "exact-target-cleanup-confirmed";
                }
                if (cleanupDisposition is null)
                {
                    supervisor.RecordTerminalCleanupAmbiguity(
                        "qualification-primary-failure", "primary-failure-cleanup-unproven");
                    throw new CredentialNativeCleanupAmbiguityException(
                        "unknown",
                        "primary-failure-cleanup-unproven",
                        supervisor.CaptureTerminalFailure(),
                        primaryFailure);
                }
                throw new CredentialNativePrimaryFailureException(
                    primaryFailure.GetType().Name,
                    cleanupDisposition,
                    supervisor.CapturePrimaryFailureAfterCertainCleanup(),
                    primaryFailure);
            }
            return supervisor.CompleteSuccessfulRun();
        }

        private static bool HasProvenContainment(
            NativeHelperFailureContainmentEvidence? containment,
            bool descendantExpected) => containment is
            { ProcessId: > 0, ProcessTreeTerminated: true, ProcessTreeSurvivorCount: 0 }
            && containment.TotalContainedProcessCount >= (descendantExpected ? 2 : 1)
            && containment.ActiveProcessCountBeforeJobClose >= 0;

        private IEnumerable<(string Scenario, string Phase, Target Target)> PreflightTargets()
        {
            yield return ("interactive-entry-submit", "preflight", targets["interactive-primary"]);
            yield return ("interactive-entry-cancel", "preflight", targets["interactive-cancel"]);
            yield return ("credential-size-boundaries", "preflight-maximum", targets["size-valid"]);
            yield return ("credential-size-boundaries", "preflight-oversize", targets["size-oversize"]);
            yield return ("secure-store-unavailable", "preflight", targets["unavailable-store"]);
            yield return ("replacement", "preflight-predecessor", targets["replacement-old"]);
            yield return ("replacement", "preflight-successor", targets["replacement-new"]);
            yield return ("revoke-delete", "preflight", targets["revoke-delete"]);
            yield return ("helper-and-coordinator-crash-restart", "preflight", targets["crash-restart"]);
            yield return ("backup-restore-reauthentication", "preflight-old", targets["backup-old"]);
            yield return ("backup-restore-reauthentication", "preflight-new", targets["backup-new"]);
            yield return ("fake-provider-dispatch", "preflight", targets["fake-dispatch"]);
        }

        private async Task SimpleEnroll(
            CredentialNativeQualificationSupervisor supervisor,
            string scenario,
            string phase,
            Target target,
            CancellationToken token,
            string? cleanupPhase = null)
        {
            ScenarioContext context = CreateContext(scenario);
            SeedProfile(context.Store, target);
            cleanup.Add((scenario, context, target));
            supervisor.RebindCoordinator(context.Coordinator);
            ThrowInjectedPrimaryFailureIfCurrent(scenario, phase);
            await supervisor.ExecuteCredentialTransitionPhaseAsync(
                scenario, phase, Attempt(scenario, phase), Bootstrap(target, target, NextNonce()),
                Assignment(scenario, phase, target, target, 1), NextTime(), token).ConfigureAwait(false);
            _ = cleanupPhase;
        }

        private async Task SizeBoundaries(CredentialNativeQualificationSupervisor supervisor, CancellationToken token)
        {
            foreach ((string phase, Target target) in new[]
                { ("maximum", targets["size-valid"]), ("oversize", targets["size-oversize"]) })
            {
                ScenarioContext context = CreateContext("size-" + phase);
                SeedProfile(context.Store, target);
                cleanup.Add(("credential-size-boundaries", context, target));
                supervisor.RebindCoordinator(context.Coordinator);
                await supervisor.ExecuteCredentialTransitionPhaseAsync(
                    "credential-size-boundaries", phase, Attempt("credential-size-boundaries", phase),
                    Bootstrap(target, target, NextNonce()), Assignment("credential-size-boundaries", phase, target, target, 1),
                    NextTime(), token).ConfigureAwait(false);
            }
        }

        private async Task Replacement(CredentialNativeQualificationSupervisor supervisor, CancellationToken token)
        {
            Target oldTarget = targets["replacement-old"];
            Target newTarget = targets["replacement-new"];
            ScenarioContext context = CreateContext("replacement");
            SeedProfile(context.Store, oldTarget);
            cleanup.Add(("replacement", context, oldTarget));
            cleanup.Add(("replacement", context, newTarget));
            supervisor.RebindCoordinator(context.Coordinator);
            await Phase(supervisor, "replacement", "predecessor-active", oldTarget, oldTarget, 1, token);
            context.Store.AddCredentialGeneration(oldTarget.ProfileId, newTarget.GenerationId, 2, 0, NextTime());
            await Phase(supervisor, "replacement", "replacement-interrupted", oldTarget, newTarget, 2, token);
            await Phase(supervisor, "replacement", "replacement-recovered", oldTarget, newTarget, 2, token);
        }

        private async Task CrashRestart(CredentialNativeQualificationSupervisor supervisor, CancellationToken token)
        {
            Target target = targets["crash-restart"];
            ScenarioContext context = CreateContext("crash-restart");
            SeedProfile(context.Store, target);
            cleanup.Add(("helper-and-coordinator-crash-restart", context, target));
            supervisor.RebindCoordinator(context.Coordinator);
            await supervisor.ExecuteInterruptedCredentialTransitionPhaseAsync(
                "helper-and-coordinator-crash-restart", "half-commit", Attempt("helper-and-coordinator-crash-restart", "half-commit"),
                Bootstrap(target, target, NextNonce()), Assignment("helper-and-coordinator-crash-restart", "half-commit", target, target, 1),
                NextTime(), token).ConfigureAwait(false);
            context.Reopen(launcher);
            supervisor.RebindCoordinator(context.Coordinator);
            await Phase(supervisor, "helper-and-coordinator-crash-restart", "restart-recovery", target, target, 1, token);
        }

        private async Task RevokeDelete(
            CredentialNativeQualificationSupervisor supervisor,
            CancellationToken token)
        {
            Target target = targets["revoke-delete"];
            ScenarioContext context = CreateContext("revoke-delete");
            SeedProfile(context.Store, target);
            cleanup.Add(("revoke-delete", context, target));
            supervisor.RebindCoordinator(context.Coordinator);
            await Phase(supervisor, "revoke-delete", "active", target, target, 1, token);
            await Phase(supervisor, "revoke-delete", "verify", target, target, 1, token);
            ProviderQualificationSeed seed = ProviderQualificationSeed.Create(context.Store, target, NextTime());
            try
            {
                await context.Coordinator.ExecuteCredentialTransitionWithFaultAsync(
                    Attempt("revoke-delete", "revocation-precommit"),
                    Bootstrap(target, target, NextNonce()),
                    Assignment("revoke-delete", "deleted-after-revocation", target, target, 1),
                    NextTime(),
                    CredentialLifecycleFaultPoint.AfterDeletePendingBeforeHelper,
                    token).ConfigureAwait(false);
                throw new InvalidOperationException("Delete revocation precommit did not interrupt before helper deletion.");
            }
            catch (IOException)
            {
                // Expected bounded pre-helper crash point; durable state is inspected below.
            }
            CredentialProfileProjection pending = context.Store.GetCredentialProfile(target.ProfileId);
            bool rejected = false;
            try { _ = context.Store.AuthorizeProviderDispatch(seed.GateRequest); }
            catch (InvalidOperationException) { rejected = true; }
            supervisor.RecordStaleGateRejection(
                target.ProfileId,
                seed.GateRequest.RevocationEpoch,
                pending.RevocationEpoch,
                rejected,
                CountDispatchFences(context.Store) == 0);
        }

        private async Task BackupRestore(CredentialNativeQualificationSupervisor supervisor, CancellationToken token)
        {
            Target oldTarget = targets["backup-old"];
            Target newTarget = targets["backup-new"];
            ScenarioContext source = CreateContext("backup-source");
            SeedProfile(source.Store, oldTarget);
            cleanup.Add(("backup-restore-reauthentication", source, oldTarget));
            supervisor.RebindCoordinator(source.Coordinator);
            await Phase(supervisor, "backup-restore-reauthentication", "backup-active", oldTarget, oldTarget, 1, token);
            BackupArtifact backup = source.Store.CreateBackup("Wp4NativeV2", NextTime());
            byte[] backupBytes = File.ReadAllBytes(backup.DatabasePath);
            string rawTarget = $"Infinium:{oldTarget.ProfileId}:{oldTarget.GenerationId}";
            bool rawTargetAbsent = backupBytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(rawTarget)) < 0
                && backupBytes.AsSpan().IndexOf(Encoding.Unicode.GetBytes(rawTarget)) < 0;
            string restoredRoot = Path.Combine(root, "backup-restored");
            Directory.CreateDirectory(Path.GetDirectoryName(restoredRoot)!);
            AuthoritativeStore.RestoreBackup(backup, new StoragePaths(restoredRoot));
            ScenarioContext restored = RegisterContext(new(restoredRoot, launcher));
            cleanup.Add(("backup-restore-reauthentication", restored, newTarget));
            CredentialProfileProjection restoredProjection = restored.Store.GetCredentialProfile(oldTarget.ProfileId);
            if (restoredProjection.UpdatedAt > timeline) { timeline = restoredProjection.UpdatedAt; }
            if (restoredProjection.LifecycleState != "recovery-required"
                || restoredProjection.RecoveryDisposition != "required"
                || restoredProjection.GenerationId != oldTarget.GenerationId
                || restoredProjection.IntentId?.StartsWith("restore-recovery-", StringComparison.Ordinal) != true)
            {
                throw new InvalidDataException(
                    "The restored same-generation rejection preconditions are not exact.");
            }
            int intentsBefore = CountRows(restored.Store, "provider_credential_intents");
            int fencesBefore = CountDispatchFences(restored.Store);
            int stagingFilesBefore = CountStagingFiles(restored.Store.Paths.Staging);
            bool sameGenerationRejected = false;
            try
            {
                DateTimeOffset rejectionAt = NextTime();
                _ = restored.Store.ApplyCredentialTransition(new(
                    "backup-same-generation-rejected",
                    oldTarget.ProfileId,
                    oldTarget.GenerationId,
                    "recover",
                    "recovery-required",
                    "active-unverified",
                    "active-unverified",
                    restoredProjection.CapabilitySnapshotId,
                    restoredProjection.AccountIdentityId,
                    restoredProjection.BillingScopeIdentityId,
                    rejectionAt,
                    rejectionAt.AddTicks(1)));
            }
            catch (SqliteException exception) when (IsExpectedRestoredGenerationRejection(exception))
            {
                sameGenerationRejected = true;
            }
            if (!sameGenerationRejected
                || restored.Store.GetCredentialProfile(oldTarget.ProfileId) != restoredProjection
                || CountRows(restored.Store, "provider_credential_intents") != intentsBefore
                || CountDispatchFences(restored.Store) != fencesBefore
                || CountStagingFiles(restored.Store.Paths.Staging) != stagingFilesBefore)
            {
                throw new InvalidDataException(
                    "The restored same-generation rejection changed durable state or helper staging.");
            }
            restored.Store.AddCredentialGeneration(oldTarget.ProfileId, newTarget.GenerationId, 2, restoredProjection.RevocationEpoch, NextTime());
            supervisor.RebindCoordinator(restored.Coordinator);
            ThrowInjectedPrimaryFailureIfCurrent(
                "backup-restore-reauthentication", "restored-new-generation");
            await Phase(supervisor, "backup-restore-reauthentication", "restored-new-generation", oldTarget, newTarget, 2, token);
            BackupEvidence = new
            {
                schema = "infinium.m1-s6.wp4.credential-native-backup-evidence/v2",
                status = "passed",
                backup_sha256 = backup.Sha256,
                restored_state = restoredProjection.LifecycleState,
                same_generation_rejected = sameGenerationRejected,
                new_generation_id = newTarget.GenerationId,
                secret_absent = true,
                secret_absence_method = "structural-helper-only-secret-never-crosses-private-protocol-or-enters-authoritative-store",
                raw_target_absent = rawTargetAbsent,
                raw_target_scan_encodings = new[] { "utf-8", "utf-16le" },
            };
        }

        private void ThrowInjectedPrimaryFailureIfCurrent(string scenario, string phase)
        {
            if (injectedPrimaryFailure is null) { return; }
            if (injectedPrimaryFailure is CredentialNativeHelperEvidenceAmbiguityException evidenceAmbiguity
                && evidenceAmbiguity.AssignmentId != $"wp4-v2/{scenario}/{phase}")
            {
                return;
            }
            Exception failure = injectedPrimaryFailure;
            injectedPrimaryFailure = null;
            throw failure;
        }

        private async Task FakeDispatch(CredentialNativeQualificationSupervisor supervisor, CancellationToken token)
        {
            Target target = targets["fake-dispatch"];
            ScenarioContext context = CreateContext("fake-dispatch");
            SeedProfile(context.Store, target);
            cleanup.Add(("fake-provider-dispatch", context, target));
            supervisor.RebindCoordinator(context.Coordinator);
            await Phase(supervisor, "fake-provider-dispatch", "enroll", target, target, 1, token);
            await Phase(supervisor, "fake-provider-dispatch", "verify", target, target, 1, token);
            ProviderQualificationSeed seed = ProviderQualificationSeed.Create(context.Store, target, NextTime());
            await supervisor.ExecuteAuthoritativeDispatchPhaseAsync(
                "fake-provider-dispatch", "final-gate-dispatch-stage-admit-settle", "fake-dispatch-attempt",
                seed.Bootstrap, seed.Assignment, NextTime(), token).ConfigureAwait(false);
        }

        private Task<CredentialNativeQualificationPhaseEvidence> Phase(
            CredentialNativeQualificationSupervisor supervisor, string scenario, string phase,
            Target bootstrapTarget, Target assignmentTarget, ulong ordinal, CancellationToken token) =>
            supervisor.ExecuteCredentialTransitionPhaseAsync(
                scenario, phase, Attempt(scenario, phase), Bootstrap(bootstrapTarget, assignmentTarget, NextNonce()),
                Assignment(scenario, phase, bootstrapTarget, assignmentTarget, ordinal), NextTime(), token);

        private ScenarioContext CreateContext(string name) =>
            RegisterContext(new(Path.Combine(root, "state-" + name), launcher));

        private ScenarioContext RegisterContext(ScenarioContext context)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            contexts.Add(context);
            return context;
        }

        public void Dispose()
        {
            if (disposed) { return; }
            disposed = true;
            for (int index = contexts.Count - 1; index >= 0; index--)
            {
                contexts[index].Dispose();
            }
            contexts.Clear();
        }
        private DateTimeOffset NextTime()
        {
            clock++;
            timeline = timeline.AddSeconds(1);
            return timeline;
        }
        private byte NextNonce() => checked((byte)++nonce);
        private static string Attempt(string scenario, string phase) => (scenario + "-" + phase + "-attempt").Replace("--", "-", StringComparison.Ordinal);

        private void SeedProfile(AuthoritativeStore store, Target target)
        {
            store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, NextTime());
            _ = store.BeginCredentialEnrollment(target.ProfileId, target.GenerationId, "WP4 v2 disposable", NextTime(), "account-wp4", "billing-wp4");
        }

        private static int CountDispatchFences(AuthoritativeStore store)
            => CountRows(store, "provider_dispatch_fences");

        private static int CountRows(AuthoritativeStore store, string table)
        {
            using SqliteConnection connection = new($"Data Source={store.Paths.Database};Mode=ReadOnly;Pooling=False");
            connection.Open();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int CountStagingFiles(string stagingRoot) => Directory.Exists(stagingRoot)
            ? Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories).Count()
            : 0;

        private HelperPrivateFrameV2 Bootstrap(Target bootstrapTarget, Target assignmentTarget, byte nonce) => new()
        {
            Sequence = 1,
            ProtocolFingerprintSha256 = Fingerprint(),
            Bootstrap = new()
            {
                CoordinatorFencingEpoch = 1,
                ExpiresAt = Instant(timeline.AddMinutes(25)),
                OneUseNonceFingerprintSha256 = ByteString.CopyFrom(Enumerable.Repeat(nonce, 32).ToArray()),
                CommandId = "wp4-v2-command",
                Credential = new() { AccessProfileId = new() { Value = bootstrapTarget.ProfileId }, GenerationId = new() { Value = bootstrapTarget.GenerationId } },
            },
        };

        private static HelperPrivateFrameV2 Assignment(
            string scenario, string phase, Target bootstrapTarget, Target target, ulong ordinal) =>
            CredentialAssignmentRaw($"wp4-v2/{scenario}/{phase}", bootstrapTarget, target, ordinal,
                CredentialNativeQualificationPhasesV2.Parse($"wp4-v2/{scenario}/{phase}",
                    CredentialNativeQualificationPhasesV2.Definitions.Single(item => item.ScenarioId == scenario && item.PhaseId == phase).AssignmentKind).AssignmentKind);

        private static HelperPrivateFrameV2 CredentialAssignmentRaw(
            string assignmentId, Target bootstrapTarget, Target target, ulong ordinal, HelperAssignmentKindV2 kind) => new()
            {
                Sequence = 2,
                ProtocolFingerprintSha256 = Fingerprint(),
                Assignment = new()
                {
                    AssignmentId = assignmentId,
                    CommandId = "wp4-v2-command",
                    AssignmentKind = kind,
                    AccessProfileId = new() { Value = target.ProfileId },
                    GenerationId = new() { Value = target.GenerationId },
                    GenerationOrdinal = ordinal,
                    Credential = new() { AccessProfileId = new() { Value = target.ProfileId }, GenerationId = new() { Value = target.GenerationId } },
                },
            };
    }

    private sealed class ScenarioContext : IDisposable
    {
        private readonly string productRoot;
        private bool disposed;
        internal ScenarioContext(string productRoot, OneShotCredentialHelperLauncher launcher)
        {
            this.productRoot = productRoot;
            Directory.CreateDirectory(Path.GetDirectoryName(productRoot)!);
            Store = new(new StoragePaths(productRoot));
            Coordinator = new(Store, launcher);
        }
        internal AuthoritativeStore Store { get; private set; }
        internal CredentialHelperCoordinator Coordinator { get; private set; }
        internal void Reopen(OneShotCredentialHelperLauncher launcher)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Store.Dispose();
            Store = new(new StoragePaths(productRoot));
            Coordinator = new(Store, launcher);
        }
        public void Dispose()
        {
            if (disposed) { return; }
            disposed = true;
            Store.Dispose();
        }
    }

    private sealed record Target(string Alias, string ProfileId, string GenerationId, string Fingerprint);
    private sealed record TargetAbsence(string Alias, string TargetFingerprintSha256, string Result);
    private sealed record CanonicalCallTraceEntry(
        long Sequence,
        string ProcessRole,
        int ProcessId,
        long LocalSequence,
        string Operation,
        string TargetFingerprintSha256,
        string Scenario,
        string Result,
        long? AllocationId,
        long? PairedAllocationId);

    private sealed record ProviderQualificationSeed(
        HelperPrivateFrameV2 Bootstrap,
        HelperPrivateFrameV2 Assignment,
        ProviderDispatchGateRequest GateRequest)
    {
        internal static ProviderQualificationSeed Create(AuthoritativeStore store, Target target, DateTimeOffset now)
        {
            const string operationId = "wp4-operation";
            const string providerAttemptId = "wp4-provider-attempt";
            const string requestId = "wp4-request";
            const string reservationId = "wp4-reservation";
            const string assignmentId = "wp4-v2/fake-provider-dispatch/final-gate-dispatch-stage-admit-settle";
            byte[] canonicalRequest = "{\"qualification\":\"wp4-v2-fake-only\"}"u8.ToArray();
            string requestHash = Convert.ToHexStringLower(SHA256.HashData(canonicalRequest));
            string settingsHash = Convert.ToHexStringLower(SHA256.HashData("wp4-settings"u8));
            string schemaHash = Convert.ToHexStringLower(SHA256.HashData("wp4-schema"u8));
            string payloadDirectory = Path.Combine(store.Paths.Payloads, requestHash[..2], requestHash[2..4]);
            Directory.CreateDirectory(payloadDirectory);
            File.WriteAllBytes(Path.Combine(payloadDirectory, requestHash), canonicalRequest);
            DateTimeOffset deadline = now.AddSeconds(60);
            using (SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False"))
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.Parameters.AddWithValue("$profile", target.ProfileId);
                command.Parameters.AddWithValue("$generation", target.GenerationId);
                command.Parameters.AddWithValue("$capability", M1ProviderCatalog.Capability.Identity.Value);
                command.Parameters.AddWithValue("$price", M1ProviderCatalog.Price.Identity.Value);
                command.Parameters.AddWithValue("$requestHash", requestHash);
                command.Parameters.AddWithValue("$settingsHash", settingsHash);
                command.Parameters.AddWithValue("$schemaHash", schemaHash);
                command.Parameters.AddWithValue("$now", now.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue("$deadline", deadline.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                command.CommandText =
                    """
                    PRAGMA foreign_keys=ON;
                    INSERT INTO runs VALUES('wp4-run','wp4-install','wp4-context','wp4-config','wp4-manifest','Running',0,1,1,$now,$now);
                    INSERT INTO job_nodes VALUES('wp4-job','wp4-run',NULL,'provider','Running',0,$now,$now);
                    INSERT INTO durable_commands VALUES('wp4-command','provider','wp4-run',0,'recorded','running',NULL,$now,NULL,NULL);
                    INSERT INTO evidence_acquisition_runs VALUES('wp4-acquisition','wp4-install','wp4-context','wp4-config','wp4-manifest','wp4-run','wp4-application','wp4-cost','running',$now);
                    INSERT INTO evidence_acquisition_job_nodes VALUES('wp4-acquisition-job','wp4-acquisition','provider','running',$now);
                    INSERT INTO evidence_acquisition_commands VALUES('wp4-acquisition-command','wp4-acquisition','provider-operation',$now,'recorded');
                    INSERT INTO provider_command_bindings VALUES('wp4-acquisition-command','evidence-acquisition-run','wp4-acquisition',$now);
                    INSERT INTO provider_command_bindings VALUES('wp4-command','analysis-run','wp4-run',$now);
                    INSERT INTO provider_effective_scan_configurations_v2 VALUES('wp4-effective','wp4-config','abababababababababababababababababababababababababababababababab','asserted-retained-v1-identity',$profile,$generation,'gpt-5.6-sol','medium','current_turn','standard',0,'default',0,0,'none',0,'disabled','explicit',0,0,16384,20480,256,262144,1,140000000,60000,'["hosted-search","nexus","loot"]',$now);
                    INSERT INTO evidence_acquisition_parent_links VALUES('wp4-parent','wp4-acquisition','wp4-run','initiated-by',NULL,$now);
                    INSERT INTO payloads VALUES('wp4-payload',$requestHash,@requestBytes,'application/json','retained',
                      'payloads/' || substr($requestHash,1,2) || '/' || substr($requestHash,3,2) || '/' || $requestHash,$now);
                    INSERT INTO provider_operation_blocks(
                      operation_id,owner_kind,owner_id,job_node_id,command_id,requested_at,confirmed_at,
                      installation_snapshot_id,analysis_context_id,effective_configuration_id,resolved_input_manifest_id,
                      profile_id,generation_id,revocation_epoch,operation_kind,capability_snapshot_id,price_snapshot_id,
                      prompt_id,prompt_fingerprint,output_schema_id,output_schema_fingerprint,request_fingerprint,
                      canonical_request_payload_id,canonical_request_fingerprint,canonical_request_bytes,settings_fingerprint,
                      input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,maximum_request_bytes,
                      maximum_input_tokens,maximum_output_tokens,maximum_raw_response_bytes,maximum_dispatch_count,
                      maximum_calculated_nano_usd,deadline_milliseconds,dispatch_deadline_utc,coordinator_fencing_epoch,state,recorded_at)
                    VALUES('wp4-operation','analysis-run','wp4-run','wp4-job','wp4-command',
                      $now,$now,'wp4-install','wp4-context','wp4-effective','wp4-manifest',$profile,$generation,0,
                      'transport-qualification',$capability,$price,'wp4-prompt',$requestHash,'wp4-schema',$schemaHash,
                      $requestHash,'wp4-payload',$requestHash,@requestBytes,$settingsHash,
                      'unresolved-openai-responses-framing','authority-required','authority-required',16384,20480,256,262144,1,140000000,60000,$deadline,1,'input-bound-blocked',$now);
                    INSERT INTO provider_operation_projection VALUES('wp4-operation','input-bound-blocked',0,0,0,1,$now);
                    INSERT INTO provider_operation_authorizations(
                      authorization_id,operation_id,owner_kind,owner_id,analysis_run_id,evidence_acquisition_run_id,job_node_id,command_id,requested_at,
                      profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
                      effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
                      output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
                      price_snapshot_id,settings_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,
                      coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,
                      maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,
                      dispatch_deadline_utc,confirmed_at)
                    SELECT 'wp4-authorization',operation_id,owner_kind,owner_id,owner_id,NULL,job_node_id,command_id,requested_at,
                      profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
                      effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
                      output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
                      price_snapshot_id,settings_fingerprint,'openai-responses-o200k-byte-envelope','v1','proved',
                      coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,
                      maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,
                      dispatch_deadline_utc,confirmed_at FROM provider_operation_blocks WHERE operation_id='wp4-operation';
                    INSERT INTO provider_operation_attempts VALUES('wp4-provider-attempt','wp4-operation',1,'proposed',1,$now);
                    INSERT INTO provider_requests(
                      request_id,client_request_id,operation_id,provider_attempt_id,request_fingerprint,
                      canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,
                      input_bound_policy_version,input_bound_proof_status,payload_id,payload_fingerprint,payload_bytes,created_at)
                    VALUES('wp4-request','wp4-client-request','wp4-operation','wp4-provider-attempt',$requestHash,$requestHash,
                      $settingsHash,$schemaHash,'openai-responses-o200k-byte-envelope','v1','proved','wp4-payload',$requestHash,@requestBytes,$now);
                    """;
                command.Parameters.AddWithValue("@requestBytes", canonicalRequest.Length);
                _ = command.ExecuteNonQuery();
            }
            CoordinatorAuthority authority = store.AcquireCoordinatorAuthorityAfterProcessExclusion(
                "wp4-native-qualification", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30));
            ProviderFiniteLimitsContract limits = new(16_384, 20_480, 256, 262_144, 1, 140_000_000, 60_000);
            ProviderBudgetVectorContract vector = new(1, 20_480, 256, 20_736, 256, 0, 0, 0,
                M1ProviderCatalog.CalculateWorstCaseNanoUsd(ProviderOperationKind.TransportQualification, limits));
            string[] kinds = ["request", "operation", "analysis-run", "provider-profile", "provider-account", "billing-scope", "global"];
            string[] ids = [requestId, operationId, "wp4-run", target.ProfileId, "account-wp4", "billing-wp4", "provider-global"];
            ProviderBudgetScopeContract[] scopes = kinds.Zip(ids,
                (kind, id) => new ProviderBudgetScopeContract(kind, new OpaqueId(id), vector)).ToArray();
            store.ConfigureProviderBudgetScopes(authority.FencingEpoch, scopes, now);
            _ = store.ReserveProviderBudget(authority.FencingEpoch, new(
                reservationId, operationId, providerAttemptId, requestId, vector, scopes, deadline, now.AddTicks(1)));

            HelperPrivateFrameV2 bootstrap = new()
            {
                Sequence = 1,
                ProtocolFingerprintSha256 = Fingerprint(),
                Bootstrap = new()
                {
                    CoordinatorFencingEpoch = checked((ulong)authority.FencingEpoch),
                    ExpiresAt = Instant(now.AddMinutes(20)),
                    OneUseNonceFingerprintSha256 = ByteString.CopyFrom(SHA256.HashData("wp4-dispatch-nonce"u8)),
                    CommandId = "wp4-v2-command",
                    ProviderDispatch = new() { OperationId = new() { Value = operationId }, AttemptId = new() { Value = providerAttemptId } },
                },
            };
            ContentDigest canonical = Digest(canonicalRequest);
            HelperPrivateFrameV2 assignment = new()
            {
                Sequence = 2,
                ProtocolFingerprintSha256 = Fingerprint(),
                Assignment = new()
                {
                    AssignmentId = assignmentId,
                    CommandId = "wp4-v2-command",
                    AssignmentKind = HelperAssignmentKindV2.ProviderDispatch,
                    OperationKind = ProviderOperationKindV2.TransportQualification,
                    AccessProfileId = new() { Value = target.ProfileId },
                    GenerationId = new() { Value = target.GenerationId },
                    GenerationOrdinal = 1,
                    RevocationEpoch = 0,
                    Credential = new() { AccessProfileId = new() { Value = target.ProfileId }, GenerationId = new() { Value = target.GenerationId } },
                    ProviderDispatch = new() { OperationId = new() { Value = operationId }, AttemptId = new() { Value = providerAttemptId } },
                    ProviderRequest = new()
                    {
                        DispatchId = new() { Value = assignmentId + ":final-gate" },
                        CanonicalRequestBytes = ByteString.CopyFrom(canonicalRequest),
                        CanonicalRequest = canonical,
                        CapabilitySnapshotId = new() { Value = M1ProviderCatalog.Capability.Identity.Value },
                        PriceSnapshotId = new() { Value = M1ProviderCatalog.Price.Identity.Value },
                        ReservationGroupId = new() { Value = reservationId },
                        DispatchDeadline = Instant(deadline),
                        EndpointIdentity = ProviderEndpointV2.OpenaiResponses,
                        InputBoundProof = new() { PolicyId = "openai-responses-o200k-byte-envelope", PolicyVersion = "v1", Status = InputBoundProofStatusV2.Proved },
                        RequestId = requestId,
                        ConfirmedAt = Instant(now),
                        RequestFingerprintSha256 = ByteString.CopyFrom(Convert.FromHexString(requestHash)),
                    },
                    AccountIdentityId = new() { Value = "account-wp4" },
                    BillingScopeIdentityId = new() { Value = "billing-wp4" },
                    EffectiveConfigurationId = "wp4-effective",
                    Settings = new() { Algorithm = DigestAlgorithm.Sha256, Value = ByteString.CopyFrom(Convert.FromHexString(settingsHash)), SizeBytes = 12 },
                    OutputSchema = new() { Algorithm = DigestAlgorithm.Sha256, Value = ByteString.CopyFrom(Convert.FromHexString(schemaHash)), SizeBytes = 10 },
                    Limits = new()
                    {
                        MaximumFrameBytes = HelperProtocolV2Constants.MaximumFrameBytes,
                        MaximumRequestBytes = 16_384,
                        MaximumResponseBytes = 262_144,
                        MaximumStagedOutputBytes = 262_144,
                        MaximumInputTokens = 20_480,
                        MaximumOutputTokens = 256,
                        MaximumCalculatedNanoUsd = 140_000_000,
                        MaximumDuration = new() { Value = 60_000 },
                        MaximumDispatchCount = 1,
                    },
                },
            };
            ProviderDispatchGateRequest gate = store.ReadCurrentProviderDispatchRequest(
                assignmentId + ":stale-gate-probe",
                operationId,
                reservationId,
                providerAttemptId,
                requestId,
                now.AddTicks(2)).Gate;
            return new(bootstrap, assignment, gate);
        }
    }

    private static ByteString Fingerprint() =>
        ByteString.CopyFrom(Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256));
    private static Instant Instant(DateTimeOffset value) => new()
    {
        UnixSeconds = value.ToUnixTimeSeconds(),
        Nanoseconds = checked((int)((value.Ticks % TimeSpan.TicksPerSecond) * 100)),
    };
    private static ContentDigest Digest(ReadOnlySpan<byte> value) => new()
    {
        Algorithm = DigestAlgorithm.Sha256,
        Value = ByteString.CopyFrom(SHA256.HashData(value)),
        SizeBytes = checked((ulong)value.Length),
    };
}
