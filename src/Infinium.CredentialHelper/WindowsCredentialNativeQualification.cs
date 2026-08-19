using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Application.Runtime;
using Microsoft.Win32.SafeHandles;

namespace Infinium.CredentialHelper;

internal static class WindowsCredentialNativeQualification
{
    private static readonly bool ConsumedV1Authority = true;
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    public const string AcceptedManifestSha256 =
        "0c911c6c10340d4a8b6a3f98aa2c2bffa3f1f4290793d3583a460cecf89bcbd3";
    public const string AcceptedManifestId =
        "infinium.m1-s6.wp4.credential-native-authorization/56789943-8096-45fa-8ac9-03da40a1c000";

    public static int Run(string manifestPath, string evidencePath)
    {
        if (ConsumedV1Authority)
        {
            throw new InvalidOperationException("The consumed WP4 v1 native qualification is terminally disabled.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidencePath);
        manifestPath = Path.GetFullPath(manifestPath);
        evidencePath = Path.GetFullPath(evidencePath);
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        string manifestSha256 = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        if (manifestSha256 != AcceptedManifestSha256)
        {
            throw new InvalidDataException("The native qualification manifest is not the exact owner-accepted artifact.");
        }

        using JsonDocument document = JsonDocument.Parse(manifestBytes);
        JsonElement root = document.RootElement;
        if (root.GetProperty("manifest_id").GetString() != AcceptedManifestId
            || root.GetProperty("status").GetString() != "ready-for-owner-acceptance"
            || root.GetProperty("effect_authority").GetString()
                != "none-until-owner-accepts-exact-manifest-bytes")
        {
            throw new InvalidDataException("The native qualification manifest identity or prepared status is invalid.");
        }
        DateTimeOffset expires = DateTimeOffset.ParseExact(
            root.GetProperty("expires_at_utc").GetString()!,
            "yyyy-MM-ddTHH:mm:ss.fffffffZ",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal);
        if (DateTimeOffset.UtcNow >= expires)
        {
            throw new InvalidDataException("The owner-accepted native qualification manifest has expired.");
        }

        NativeQualificationManifest manifest = ReadManifest(root);
        Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)!);
        string backupPath = Path.Combine(Path.GetDirectoryName(evidencePath)!, "native-backup-metadata.json");
        FiniteNativeDeadline deadline = FiniteNativeDeadline.Start(TimeSpan.FromSeconds(1_800));
        byte[] secretBytes = [];
        List<NativeScenarioEvidence> scenarios = [];
        List<NativeCanarySurface> canarySurfaces = [];
        NativeNamespaceReuseGuard reuseGuard = new();
        WindowsCredentialManagerStore store = new(reuseGuard, deadline);
        Stopwatch duration = Stopwatch.StartNew();
        bool completed = false;
        try
        {
            deadline.DemandRemaining("preflight");
            PreflightAllTargets(store, manifest.Targets);
            secretBytes = RunInteractiveSubmit(store, Target(manifest, "interactive-primary"), scenarios);
            RunInteractiveCancel(store, Target(manifest, "interactive-cancel"), scenarios);
            RunSizeBoundaries(store, Target(manifest, "size-valid"),
                Target(manifest, "size-oversize"), scenarios);
            RunUnavailable(store, Target(manifest, "unavailable-store"), scenarios);
            RunReplacement(store, Target(manifest, "replacement-old"),
                Target(manifest, "replacement-new"), secretBytes, scenarios);
            RunRevokeDelete(store, Target(manifest, "revoke-delete"), secretBytes, scenarios);
            RunCrashRestart(store, Target(manifest, "crash-restart"), manifestPath,
                Path.GetDirectoryName(evidencePath)!, scenarios, canarySurfaces);
            RunBackupRestore(store, Target(manifest, "backup-old"), Target(manifest, "backup-new"),
                secretBytes, backupPath, scenarios);
            RunFakeDispatch(store, Target(manifest, "fake-dispatch"), secretBytes, scenarios);
            RunCleanupAmbiguityControlFlowProof(Target(manifest, "unavailable-store"),
                Target(manifest, "crash-restart"), scenarios);

            List<TargetAbsenceEvidence> absence = [];
            foreach (NativeTarget target in manifest.Targets)
            {
                bool absent = store.DeleteAndProveAbsent(target);
                absence.Add(new(target.Alias, target.TargetFingerprintSha256, absent ? "ERROR_NOT_FOUND" : "unexpected-present"));
            }
            if (absence.Any(item => item.Result != "ERROR_NOT_FOUND"))
            {
                throw new InvalidOperationException("One or more disposable targets remained present after cleanup.");
            }

            duration.Stop();
            (int listeners, int networkOperations) = NativeNetworkMeasurement.MeasureCurrentProcessTcp();
            canarySurfaces.Add(NativeCanarySurface.FromFile("backup metadata", backupPath));
            NativeCanaryEvidence canaryEvidence = NativeCanaryScanner.Scan(
                secretBytes, RawTargetCanaries(manifest.Targets), canarySurfaces);
            NativeCallTraceValidator.Validate(store.CallTrace);
            NativeQualificationEvidence evidence = new(
                "infinium.m1-s6.wp4.credential-native-evidence/v1",
                "passed",
                AcceptedManifestId,
                manifestSha256,
                Environment.ProcessId,
                store.CallCounts,
                store.CallTrace,
                scenarios,
                absence,
                new(
                    "helper-owned-native-masked-control",
                    true,
                    true,
                    "runtime-only",
                    "cancelled-no-write"),
                new(
                    "recovery-required",
                    "new-generation-only",
                    true,
                    true,
                    Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(backupPath)))),
                canaryEvidence,
                deadline.Snapshot(),
                listeners,
                networkOperations,
                0,
                0,
                0,
                false,
                true,
                duration.ElapsedMilliseconds,
                DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ",
                    System.Globalization.CultureInfo.InvariantCulture));
            WriteEvidence(evidencePath, evidence);
            NativeCanaryEvidence retainedEvidenceScan = NativeCanaryScanner.Scan(
                secretBytes,
                RawTargetCanaries(manifest.Targets),
                [NativeCanarySurface.FromFile("native evidence", evidencePath), .. canarySurfaces]);
            if (retainedEvidenceScan.SecretMatches != 0 || retainedEvidenceScan.RawTargetMatches != 0)
            {
                throw new InvalidOperationException("A native canary leaked into a scanned retained surface.");
            }
            completed = true;
            return 0;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            if (!completed && !reuseGuard.IsBlocked)
            {
                foreach (NativeTarget target in manifest.Targets.Where(store.WasWrittenByThisRun))
                {
                    try { _ = store.DeleteAndProveAbsent(target); }
                    catch { /* Gate failure preserves cleanup uncertainty for owner disposition. */ }
                }
            }
        }
    }

    public static int RunCrashProbe(string manifestPath, string alias, string countEvidencePath)
    {
        if (ConsumedV1Authority)
        {
            throw new InvalidOperationException("The consumed WP4 v1 native crash probe is terminally disabled.");
        }
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(manifestPath));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != AcceptedManifestSha256)
        {
            return 66;
        }
        using JsonDocument document = JsonDocument.Parse(bytes);
        NativeQualificationManifest manifest = ReadManifest(document.RootElement);
        NativeTarget target = Target(manifest, alias);
        byte[] secret = RandomNumberGenerator.GetBytes(48);
        try
        {
            WindowsCredentialManagerStore store = new();
            store.BeginScenario("helper-and-coordinator-crash-restart");
            store.WriteExact(target, secret);
            byte[] read = store.ReadExact(target);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(secret, read)) { return 67; }
            }
            finally { CryptographicOperations.ZeroMemory(read); }
            File.WriteAllText(Path.GetFullPath(countEvidencePath),
                JsonSerializer.Serialize(new { trace = store.CallTrace }, IndentedJson) + "\n",
                new UTF8Encoding(false));
            Environment.Exit(69);
            return 69;
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    private static NativeQualificationManifest ReadManifest(JsonElement root)
    {
        JsonElement targets = root.GetProperty("disposable_namespace").GetProperty("targets");
        List<NativeTarget> values = [];
        foreach (JsonElement item in targets.EnumerateArray())
        {
            values.Add(new(
                item.GetProperty("alias").GetString()!,
                item.GetProperty("access_profile_id").GetString()!,
                item.GetProperty("generation_id").GetString()!,
                item.GetProperty("target_fingerprint_sha256").GetString()!));
        }
        return new(values);
    }

    private static void PreflightAllTargets(WindowsCredentialManagerStore store, IEnumerable<NativeTarget> targets)
    {
        foreach (NativeTarget target in targets)
        {
            if (store.Exists(target))
            {
                throw new InvalidOperationException(
                    $"Disposable target collision for fingerprint {target.TargetFingerprintSha256}; no write or delete was attempted.");
            }
        }
    }

    private static byte[] RunInteractiveSubmit(
        WindowsCredentialManagerStore store,
        NativeTarget target,
        List<NativeScenarioEvidence> evidence)
    {
        store.BeginScenario("interactive-entry-submit");
        NativeEntryCapture entered = NativeMaskedEntryDialog.Capture(TimeSpan.FromMinutes(5));
        if (entered.TerminalState != NativeEntryTerminalState.Submitted || entered.Secret.Length == 0)
        {
            entered.Dispose();
            throw new InvalidOperationException("Native entry submit did not produce a non-empty credential.");
        }
        try
        {
            store.WriteExact(target, entered.Secret);
            byte[] read = store.ReadExact(target);
            try { RequireEqual(entered.Secret, read, "Native interactive entry did not round-trip exactly."); }
            finally { CryptographicOperations.ZeroMemory(read); }
        }
        catch
        {
            entered.Dispose();
            throw;
        }
        evidence.Add(new("interactive-entry-submit", [target.TargetFingerprintSha256], "completed", true, true));
        return entered.DetachSecret();
    }

    private static void RunInteractiveCancel(
        WindowsCredentialManagerStore store,
        NativeTarget target,
        List<NativeScenarioEvidence> evidence)
    {
        store.BeginScenario("interactive-entry-cancel");
        using NativeEntryCapture entered = NativeMaskedEntryDialog.Capture(TimeSpan.FromMinutes(5));
        if (entered.TerminalState != NativeEntryTerminalState.Cancelled || entered.Secret.Length != 0 || store.Exists(target))
        {
            throw new InvalidOperationException("Native entry cancellation retained or wrote a credential.");
        }
        evidence.Add(new("interactive-entry-cancel", [target.TargetFingerprintSha256], "cancelled", true, true));
    }

    private static void RunSizeBoundaries(
        WindowsCredentialManagerStore store,
        NativeTarget valid,
        NativeTarget oversized,
        List<NativeScenarioEvidence> evidence)
    {
        store.BeginScenario("credential-size-boundaries");
        byte[] maximum = RandomNumberGenerator.GetBytes(WindowsCredentialManagerStore.MaximumBlobBytes);
        byte[] over = RandomNumberGenerator.GetBytes(WindowsCredentialManagerStore.MaximumBlobBytes + 1);
        try
        {
            store.WriteExact(valid, maximum);
            byte[] read = store.ReadExact(valid);
            try { RequireEqual(maximum, read, "Maximum-size credential did not round-trip exactly."); }
            finally { CryptographicOperations.ZeroMemory(read); }
            try
            {
                store.WriteExact(oversized, over);
                throw new InvalidOperationException("Oversized credential reached CredWriteW.");
            }
            catch (InvalidDataException) { }
            if (store.Exists(oversized)) { throw new InvalidOperationException("Oversized target became present."); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(maximum);
            CryptographicOperations.ZeroMemory(over);
        }
        evidence.Add(new("credential-size-boundaries",
            [valid.TargetFingerprintSha256, oversized.TargetFingerprintSha256], "completed", true, true));
    }

    private static void RunUnavailable(
        WindowsCredentialManagerStore store,
        NativeTarget target,
        List<NativeScenarioEvidence> evidence)
    {
        store.BeginScenario("secure-store-unavailable");
        store.SetFault(WindowsCredentialFault.UnavailableBeforeNativeCall);
        try
        {
            store.WriteExact(target, [1]);
            throw new InvalidOperationException("Injected unavailable store did not fail closed.");
        }
        catch (IOException) { }
        finally { store.SetFault(WindowsCredentialFault.None); }
        if (store.Exists(target)) { throw new InvalidOperationException("Unavailable-store target became present."); }
        evidence.Add(new("secure-store-unavailable", [target.TargetFingerprintSha256],
            "secure-store-unavailable", true, true));
    }

    private static void RunReplacement(
        WindowsCredentialManagerStore store,
        NativeTarget oldTarget,
        NativeTarget newTarget,
        byte[] secret,
        List<NativeScenarioEvidence> evidence)
    {
        store.BeginScenario("replacement");
        store.WriteExact(oldTarget, secret);
        byte[] oldRead = store.ReadExact(oldTarget);
        CryptographicOperations.ZeroMemory(oldRead);
        byte[] next = RandomNumberGenerator.GetBytes(48);
        try
        {
            store.WriteExact(newTarget, next);
            byte[] newRead = store.ReadExact(newTarget);
            try { RequireEqual(next, newRead, "Replacement successor did not verify."); }
            finally { CryptographicOperations.ZeroMemory(newRead); }
            if (!store.DeleteAndProveAbsent(oldTarget))
            {
                throw new InvalidOperationException("Replacement predecessor was not confirmed absent.");
            }
        }
        finally { CryptographicOperations.ZeroMemory(next); }
        evidence.Add(new("replacement", [oldTarget.TargetFingerprintSha256, newTarget.TargetFingerprintSha256],
            "completed", true, true));
    }

    private static void RunRevokeDelete(
        WindowsCredentialManagerStore store,
        NativeTarget target,
        byte[] secret,
        List<NativeScenarioEvidence> evidence)
    {
        store.BeginScenario("revoke-delete");
        store.WriteExact(target, secret);
        byte[] read = store.ReadExact(target);
        CryptographicOperations.ZeroMemory(read);
        if (!store.DeleteAndProveAbsent(target))
        {
            throw new InvalidOperationException("Revoked credential was not confirmed absent.");
        }
        evidence.Add(new("revoke-delete", [target.TargetFingerprintSha256], "deleted", true, true));
    }

    private static void RunCrashRestart(
        WindowsCredentialManagerStore store,
        NativeTarget target,
        string manifestPath,
        string outputDirectory,
        List<NativeScenarioEvidence> evidence,
        List<NativeCanarySurface> canarySurfaces)
    {
        store.BeginScenario("helper-and-coordinator-crash-restart");
        string childCountsPath = Path.Combine(outputDirectory, "native-crash-call-counts.json");
        string arguments = $"--credential-native-crash-probe --manifest \"{manifestPath}\" --target-alias {target.Alias} --count-evidence \"{childCountsPath}\"";
        ProcessStartInfo start = new(Environment.ProcessPath!, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment.Clear();
        start.Environment["DOTNET_EnableDiagnostics"] = "0";
        canarySurfaces.Add(NativeCanarySurface.FromText("crash probe process launch", arguments));
        canarySurfaces.Add(NativeCanarySurface.FromText("crash probe environment", "DOTNET_EnableDiagnostics=0"));
        using Process child = Process.Start(start)
            ?? throw new InvalidOperationException("Native crash probe could not start.");
        child.WaitForExit(30_000);
        if (!child.HasExited || child.ExitCode == 0)
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); }
            throw new InvalidOperationException("Native crash probe did not terminate at the intended half-commit.");
        }
        using (JsonDocument counts = JsonDocument.Parse(File.ReadAllBytes(childCountsPath)))
        {
            JsonElement countRoot = counts.RootElement;
            store.AddExternalTrace(countRoot.GetProperty("trace").Deserialize<List<NativeCallTraceEntry>>()
                ?? throw new InvalidDataException("Crash probe call trace is absent."));
            store.MarkExternalWrite(target);
        }
        string stdout = child.StandardOutput.ReadToEnd();
        string stderr = child.StandardError.ReadToEnd();
        canarySurfaces.Add(NativeCanarySurface.FromText("crash probe stdout", stdout));
        canarySurfaces.Add(NativeCanarySurface.FromText("crash probe stderr", stderr));
        canarySurfaces.Add(NativeCanarySurface.FromFile("crash probe call evidence", childCountsPath));
        if (stdout.Contains("Infinium:", StringComparison.Ordinal)
            || stderr.Contains("Infinium:", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Native crash probe disclosed a target.");
        }
        byte[] read = store.ReadExact(target);
        CryptographicOperations.ZeroMemory(read);
        if (!store.DeleteAndProveAbsent(target))
        {
            throw new InvalidOperationException("Crash-restart target was not confirmed absent.");
        }
        evidence.Add(new("helper-and-coordinator-crash-restart", [target.TargetFingerprintSha256],
            "recovered-exact-target", true, true));
    }

    private static void RunBackupRestore(
        WindowsCredentialManagerStore store,
        NativeTarget oldTarget,
        NativeTarget newTarget,
        byte[] secret,
        string backupPath,
        List<NativeScenarioEvidence> evidence)
    {
        store.BeginScenario("backup-restore-reauthentication");
        store.WriteExact(oldTarget, secret);
        byte[] oldRead = store.ReadExact(oldTarget);
        CryptographicOperations.ZeroMemory(oldRead);
        byte[] backup = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = "infinium.m1-s6.wp4.native-backup-metadata/v1",
            profile_id = oldTarget.AccessProfileId,
            generation_id = oldTarget.GenerationId,
            restored_state = "recovery-required",
            secret = "absent",
            target = "absent",
        });
        File.WriteAllBytes(backupPath, backup);
        if (backup.AsSpan().IndexOf(secret) >= 0)
        {
            throw new InvalidOperationException("Backup retained the native credential canary.");
        }
        if (!store.DeleteAndProveAbsent(oldTarget))
        {
            throw new InvalidOperationException("Restored predecessor target was not confirmed absent.");
        }
        byte[] reentered = RandomNumberGenerator.GetBytes(48);
        try
        {
            store.WriteExact(newTarget, reentered);
            byte[] next = store.ReadExact(newTarget);
            try { RequireEqual(reentered, next, "Re-entered successor did not verify."); }
            finally { CryptographicOperations.ZeroMemory(next); }
            if (!store.DeleteAndProveAbsent(newTarget))
            {
                throw new InvalidOperationException("Re-entered successor target was not confirmed absent.");
            }
        }
        finally { CryptographicOperations.ZeroMemory(reentered); }
        evidence.Add(new("backup-restore-reauthentication",
            [oldTarget.TargetFingerprintSha256, newTarget.TargetFingerprintSha256],
            "new-generation-required", true, true));
    }

    private static void RunFakeDispatch(
        WindowsCredentialManagerStore store,
        NativeTarget target,
        byte[] secret,
        List<NativeScenarioEvidence> evidence)
    {
        store.BeginScenario("fake-provider-dispatch");
        store.WriteExact(target, secret);
        byte[] dispatchSecret = store.ReadExact(target);
        try
        {
            if (dispatchSecret.Length == 0) { throw new InvalidOperationException("Fake dispatch lacked a credential."); }
            byte[] staged = "{\"provider\":\"deterministic-fake\",\"status\":\"completed\"}"u8.ToArray();
            _ = SHA256.HashData(staged);
        }
        finally { CryptographicOperations.ZeroMemory(dispatchSecret); }
        if (!store.DeleteAndProveAbsent(target))
        {
            throw new InvalidOperationException("Fake-dispatch target was not confirmed absent.");
        }
        evidence.Add(new("fake-provider-dispatch", [target.TargetFingerprintSha256],
            "completed-stage-before-admit", true, true));
    }

    private static void RunCleanupAmbiguityControlFlowProof(
        NativeTarget unavailable,
        NativeTarget crash,
        List<NativeScenarioEvidence> evidence)
    {
        NativeNamespaceReuseGuard proof = new();
        proof.Block("injected-control-flow-proof");
        bool blocked = false;
        try { proof.DemandNativeCallAllowed(); }
        catch (NativeNamespaceBlockedException) { blocked = true; }
        if (!blocked || !proof.IsBlocked)
        {
            throw new InvalidOperationException("Ambiguous cleanup control-flow proof did not terminally block calls.");
        }
        evidence.Add(new("cleanup-failure-and-ambiguity",
            [unavailable.TargetFingerprintSha256, crash.TargetFingerprintSha256],
            "non-native-control-flow-proof:namespace-reuse-blocked", true, true));
    }

    private static NativeTarget Target(NativeQualificationManifest manifest, string alias) =>
        manifest.Targets.Single(item => item.Alias == alias);

    private static NativeRawTargetCanary[] RawTargetCanaries(
        IEnumerable<NativeTarget> targets) => targets.SelectMany(target => new[]
        {
            new NativeRawTargetCanary("utf-8", Encoding.UTF8.GetBytes(target.RawTarget)),
            new NativeRawTargetCanary("utf-16le", Encoding.Unicode.GetBytes(target.RawTarget)),
        }).ToArray();

    private static void RequireEqual(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, string message)
    {
        if (!CryptographicOperations.FixedTimeEquals(expected, actual)) { throw new InvalidOperationException(message); }
    }

    private static void WriteEvidence(string path, NativeQualificationEvidence evidence)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(evidence, IndentedJson);
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write(json);
        stream.WriteByte((byte)'\n');
        stream.Flush(flushToDisk: true);
    }

}

internal static class WindowsCredentialNativeRecovery
{
    private static readonly JsonSerializerOptions EvidenceJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    internal static bool IsAcceptedSchemaIdentityForTest(string? schemaIdentity) =>
        schemaIdentity is "infinium.repository.wp4-credential-native-recovery/1.0.0"
            or "infinium.repository.wp4-credential-native-recovery/1.1.0"
            or "infinium.repository.wp4-credential-native-recovery/1.2.0"
            or "infinium.repository.wp4-credential-native-recovery/1.3.0"
            or "infinium.repository.wp4-credential-native-recovery/1.4.0"
            or "infinium.repository.wp4-credential-native-recovery/1.5.0";

    internal static int Run(string manifestPath, string expectedSha256, string expectedManifestId, string evidencePath)
    {
        byte[] manifestBytes = File.ReadAllBytes(Path.GetFullPath(manifestPath));
        string actualSha = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        if (!string.Equals(actualSha, expectedSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Recovery manifest bytes differ from exact authority.");
        }
        using JsonDocument document = JsonDocument.Parse(manifestBytes);
        JsonElement root = document.RootElement;
        string? schemaIdentity = root.GetProperty("schema_identity").GetString();
        if (!IsAcceptedSchemaIdentityForTest(schemaIdentity)
            || root.GetProperty("manifest_id").GetString() != expectedManifestId
            || root.GetProperty("status").GetString() != "ready-for-owner-acceptance")
        {
            throw new InvalidDataException("Recovery manifest identity is invalid.");
        }
        string[] allowed = root.GetProperty("native_boundary").GetProperty("allowed_calls")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (!allowed.SequenceEqual(["CredReadW", "CredDeleteW", "CredFree"], StringComparer.Ordinal))
        {
            throw new InvalidDataException("Recovery native boundary is not exact.");
        }
        using WindowsCredentialManagerStore store = WindowsCredentialManagerStore.FromRecoveryManifest(root);
        JsonElement recoveryBinding = root.GetProperty("binding");
        bool usesTerminalArtifactLineage = schemaIdentity
            == "infinium.repository.wp4-credential-native-recovery/1.5.0";
        string? priorTerminalEvidenceSha256 = recoveryBinding.TryGetProperty("terminal_evidence_sha256", out JsonElement priorEvidence)
            ? priorEvidence.GetString() : null;
        string? priorTerminalArtifactKind = recoveryBinding.TryGetProperty("terminal_artifact_kind", out JsonElement priorArtifactKind)
            ? priorArtifactKind.GetString() : null;
        string? priorTerminalArtifactSha256 = recoveryBinding.TryGetProperty("terminal_artifact_sha256", out JsonElement priorArtifact)
            ? priorArtifact.GetString() : null;
        string? priorSuccessSummarySha256 = recoveryBinding.TryGetProperty("success_summary_sha256", out JsonElement priorSummary)
            ? priorSummary.GetString() : null;
        string? priorBackupMetadataSha256 = recoveryBinding.TryGetProperty("backup_metadata_sha256", out JsonElement priorBackup)
            ? priorBackup.GetString() : null;
        string? priorHelperReceiptInventorySha256 = recoveryBinding.TryGetProperty("helper_receipt_inventory_sha256", out JsonElement priorReceipts)
            ? priorReceipts.GetString() : null;
        string? priorOutputInventorySha256 = recoveryBinding.TryGetProperty("output_inventory_sha256", out JsonElement priorOutput)
            ? priorOutput.GetString() : null;
        string? priorAuthorityLockSha256 = recoveryBinding.TryGetProperty("consumed_lock_sha256", out JsonElement priorLock)
            ? priorLock.GetString() : null;
        int priorExactAbsenceCount = recoveryBinding.TryGetProperty("prior_exact_absence_count", out JsonElement priorCount)
            ? priorCount.GetInt32() : 0;
        List<object> absence = [];
        try
        {
            foreach (NativeTarget target in store.ManifestTargets)
            {
                store.BeginScenario("cleanup-only-recovery");
                if (store.Exists(target) && !store.DeleteAndProveAbsent(target))
                {
                    throw new InvalidOperationException("Exact target remained present after bounded recovery cleanup.");
                }
                absence.Add(new { alias = target.Alias, target_fingerprint_sha256 = target.TargetFingerprintSha256, result = "ERROR_NOT_FOUND" });
            }
            WriteEvidence(evidencePath, new
            {
                schema = usesTerminalArtifactLineage
                    ? "infinium.m1-s6.wp4.credential-native-recovery-evidence/v2"
                    : "infinium.m1-s6.wp4.credential-native-recovery-evidence/v1",
                status = "passed",
                manifest_id = expectedManifestId,
                manifest_sha256 = actualSha,
                target_absence = absence,
                native_call_counts = store.CallCounts,
                native_call_trace = store.CallTrace,
                cleanup_ambiguity = false,
                namespace_reuse_blocked = true,
                namespace_disposition = "cleanup-confirmed-absent-never-reuse",
                prior_terminal_evidence_sha256 = priorTerminalEvidenceSha256,
                prior_terminal_artifact_kind = priorTerminalArtifactKind,
                prior_terminal_artifact_sha256 = priorTerminalArtifactSha256,
                prior_success_summary_sha256 = priorSuccessSummarySha256,
                prior_backup_metadata_sha256 = priorBackupMetadataSha256,
                prior_helper_receipt_inventory_sha256 = priorHelperReceiptInventorySha256,
                prior_output_inventory_sha256 = priorOutputInventorySha256,
                prior_authority_lock_sha256 = priorAuthorityLockSha256,
                prior_exact_absence_count = priorExactAbsenceCount,
                combined_namespace_target_absence_count = checked(priorExactAbsenceCount + absence.Count),
                network_operations = 0,
                dns_operations = 0,
                provider_operations = 0,
                billable_operations = 0,
            });
            return 0;
        }
        catch
        {
            WriteEvidence(evidencePath, new
            {
                schema = usesTerminalArtifactLineage
                    ? "infinium.m1-s6.wp4.credential-native-recovery-evidence/v2"
                    : "infinium.m1-s6.wp4.credential-native-recovery-evidence/v1",
                status = "failed-cleanup-ambiguous",
                manifest_id = expectedManifestId,
                manifest_sha256 = actualSha,
                target_absence = absence,
                native_call_counts = store.CallCounts,
                native_call_trace = store.CallTrace,
                cleanup_ambiguity = true,
                namespace_reuse_blocked = true,
                namespace_disposition = "cleanup-ambiguous-never-reuse",
                prior_terminal_evidence_sha256 = priorTerminalEvidenceSha256,
                prior_terminal_artifact_kind = priorTerminalArtifactKind,
                prior_terminal_artifact_sha256 = priorTerminalArtifactSha256,
                prior_success_summary_sha256 = priorSuccessSummarySha256,
                prior_backup_metadata_sha256 = priorBackupMetadataSha256,
                prior_helper_receipt_inventory_sha256 = priorHelperReceiptInventorySha256,
                prior_output_inventory_sha256 = priorOutputInventorySha256,
                prior_authority_lock_sha256 = priorAuthorityLockSha256,
                prior_exact_absence_count = priorExactAbsenceCount,
                later_native_calls = 0,
            });
            throw;
        }
    }

    private static void WriteEvidence(string path, object value)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, JsonSerializer.Serialize(value, EvidenceJson) + "\n", new UTF8Encoding(false));
    }
}

internal sealed record NativeTarget(
    string Alias,
    string AccessProfileId,
    string GenerationId,
    string TargetFingerprintSha256)
{
    internal string RawTarget => $"Infinium:{AccessProfileId}:{GenerationId}";
}

internal sealed record NativeQualificationManifest(IReadOnlyList<NativeTarget> Targets);

internal enum WindowsCredentialFault { None, UnavailableBeforeNativeCall }

internal sealed class WindowsCredentialManagerStore : ISyntheticSecureStore, IDisposable
{
    public const int MaximumBlobBytes = 2_560;
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const string ExpectedSupersededManifestId =
        "infinium.m1-s6.wp4.credential-native-authorization/076b981a-9d32-4e6a-af35-1e7017e0f833";
    private const string ExpectedSupersededManifestSha256 =
        "36890ec28cf706484730fc9dfbd6dec5bcf3be76ed5c509a373fa61b8c910ee2";
    private const string ExpectedSupersededArtifactSha256 =
        "1c624078f51c8d4eab9563384dd5f67cecde81b16995f0819d29bf2457165f6e";
    private const string ExpectedSupersededAuthorityLockSha256 =
        "80a014c72636221a2cf52008bb9ee0d27cd0c6badbfa5659d324a6ad9be350a7";
    private const string ExpectedCleanupRecoveryManifestSha256 =
        "94cb5c77b906100c6c436ddbb889f7511b2f4c1cea0c60556651c97b7020414d";
    private const string ExpectedCleanupRecoveryEvidenceSha256 =
        "d65cefe9c2a71231c8fd9a6c4105f26acd742f49af248f38be989b059a93a515";
    private const string ProductionEnrollmentV4 =
        "infinium.repository.wp9-production-profile-authorization/4.0.0";
    private static readonly HashSet<string> RetiredProductionManifestIds = new(StringComparer.Ordinal)
    {
        "infinium.m1-s6.wp9.production-profile-authorization/09b8e309-ead8-441e-8307-5a4a1a2c43d5",
        "infinium.m1-s6.wp9.production-profile-authorization/52b2cfdb-ccd4-49c0-8f6a-ace8c426012e",
    };
    private static readonly HashSet<string> RetiredProductionProfileIds = new(StringComparer.Ordinal)
    {
        "openai-platform-c2f213dbc4d9461c9fa8485050ab324d",
        "openai-platform-ecd3de4b9fac443593347905970d942d",
    };
    private static readonly HashSet<string> RetiredProductionGenerationIds = new(StringComparer.Ordinal)
    {
        "g-cb0c3748ef2b4745b97a9311c89f2b65",
        "g-6eefeaf6e4a74273bf4ee69f02449f47",
    };
    private static readonly HashSet<string> RetiredProductionTargetFingerprints = new(StringComparer.Ordinal)
    {
        "7c4683448a864da4b7cb96a07cf13db93cff9b1a1eb22ed013250a2975a9c071",
        "990e46a57687417a1a1865bab3b11823f3b37d35961fb8101e32a8977e2a4b67",
    };
    private WindowsCredentialFault fault;
    private int writeCount;
    private int readCount;
    private int deleteCount;
    private int freeCount;
    private long sequence;
    private long allocationSequence;
    private string scenario = "preflight";
    private readonly NativeNamespaceReuseGuard reuseGuard;
    private readonly FiniteNativeDeadline deadline;
    private readonly List<NativeCallTraceEntry> callTrace = [];
    private readonly Dictionary<SyntheticCredentialSlot, NativeTarget> manifestTargets = [];
    private readonly List<NativeTarget> manifestTargetOrder = [];
    private readonly HashSet<string> consumedNonces = new(StringComparer.Ordinal);
    private readonly HashSet<string> writtenTargetFingerprints = new(StringComparer.Ordinal);
    private string? deleteFailureGenerationId;
    internal bool IsProductionEnrollment { get; private set; }

    public WindowsCredentialManagerStore()
        : this(new NativeNamespaceReuseGuard(), FiniteNativeDeadline.Start(TimeSpan.FromMinutes(30))) { }

    internal WindowsCredentialManagerStore(NativeNamespaceReuseGuard reuseGuard, FiniteNativeDeadline deadline)
    {
        this.reuseGuard = reuseGuard;
        this.deadline = deadline;
    }

    public static WindowsCredentialManagerStore FromAcceptedManifest(
        string manifestPath,
        string expectedSha256,
        string expectedManifestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedManifestId);
        try
        {
            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(manifestPath));
            string actualSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (expectedSha256.Length != 64
                || !expectedSha256.All(char.IsAsciiHexDigit)
                || !string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The native store manifest is not the exact accepted artifact.");
            }
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement manifestRoot = document.RootElement;
            JsonElement supersedes = manifestRoot.GetProperty("supersedes");
            if (!string.Equals(
                manifestRoot.GetProperty("manifest_id").GetString(),
                expectedManifestId,
                StringComparison.Ordinal)
                || manifestRoot.GetProperty("schema_identity").GetString()
                    != "infinium.repository.wp4-credential-native-authorization/1.6.0"
                || manifestRoot.GetProperty("status").GetString() != "ready-for-owner-acceptance"
                || manifestRoot.GetProperty("effect_authority").GetString()
                    != "none-until-owner-accepts-exact-manifest-bytes"
                || manifestRoot.GetProperty("candidate_binding")
                    .GetProperty("evidence_finalization_correction_candidate_commit").GetString()
                    != "03ae6929bad069c7c9e351b2ed5bd361e31b89e7")
            {
                throw new InvalidDataException("The native store manifest identity or prepared state does not match its accepted v2 binding.");
            }
            JsonElement cleanupRecovery = supersedes.GetProperty("cleanup_recovery");
            if (supersedes.TryGetProperty("gate_receipt_sha256", out _)
                || supersedes.GetProperty("manifest_id").GetString() != ExpectedSupersededManifestId
                || supersedes.GetProperty("manifest_sha256").GetString() != ExpectedSupersededManifestSha256
                || supersedes.GetProperty("terminal_artifact_kind").GetString()
                    != "typed-coordinator-stderr-post-success-evidence-finalization"
                || supersedes.GetProperty("terminal_artifact_sha256").GetString() != ExpectedSupersededArtifactSha256
                || supersedes.GetProperty("success_summary_sha256").GetString()
                    != "e05a4db0c0f7f2422ce88565b81ea8bf342e96bcf1a06feaa09a8c7a94e03299"
                || supersedes.GetProperty("backup_metadata_sha256").GetString()
                    != "04f44827955b7a6d72ba9808b317edb85de70be0759a654a3b15433ac0fefa6c"
                || supersedes.GetProperty("output_inventory_sha256").GetString()
                    != "9e3f55968721c55ce1637dfc00673acd757c6ea04b3f640bb2acb19354b4427f"
                || supersedes.GetProperty("authority_lock_sha256").GetString()
                    != ExpectedSupersededAuthorityLockSha256
                || supersedes.GetProperty("namespace_disposition").GetString()
                    != "terminal-cleanup-confirmed-absent-never-reuse"
                || cleanupRecovery.GetProperty("manifest_id").GetString()
                    != "infinium.m1-s6.wp4.credential-native-recovery/040817c8-0a87-480a-915c-71dc2fe54da3"
                || cleanupRecovery.GetProperty("manifest_sha256").GetString()
                    != ExpectedCleanupRecoveryManifestSha256
                || cleanupRecovery.GetProperty("evidence_sha256").GetString()
                    != ExpectedCleanupRecoveryEvidenceSha256
                || cleanupRecovery.GetProperty("authority_lock_sha256").GetString()
                    != "178711a914651b180d667285c6d4e22c8a820aa6f8450e398626a121afc2c5d0"
                || cleanupRecovery.GetProperty("receipt_sha256").GetString()
                    != "413789b410eb3718f7185d01d614d90444b2edb6196338dd21b246802cdb00cf"
                || cleanupRecovery.GetProperty("reconstructed_receipt_sha256").GetString()
                    != "d105f42e7dfcec30590f40fa9b9ce0c65fe0c4a6aca9d1bd09b47ac048e3d853"
                || cleanupRecovery.GetProperty("combined_namespace_target_absence_count").GetInt32() != 12)
            {
                throw new InvalidDataException("The native store manifest predecessor authority is not exact.");
            }
            if (manifestRoot.GetProperty("disposable_namespace").GetProperty("namespace_id").GetString()
                != "m1-s6-wp4-native-c6e9226e-3d95-496c-bda6-c9142bb6b980")
            {
                throw new InvalidDataException("The native store manifest namespace is not the exact fresh namespace.");
            }
            NativeTarget[] expectedTargets =
            [
                new("interactive-primary", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-interactive-primary", "g001", "735a2bb140500c961b6dd1a043328e10ea403fd718a37e8fc1d20278429e2902"),
                new("interactive-cancel", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-interactive-cancel", "g001", "70ac9332bcde2d808cce41410f75ffc65db1cc19ea00a94d088949f6d359d05b"),
                new("size-valid", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-size-valid", "g001", "ee50987dfacfe66e26648307d5163919c7a44289eef69764ac610442d9e1141a"),
                new("size-oversize", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-size-oversize", "g001", "55ff9c3afb4f6e3766fd58adf26e8ae2e70589dc915bb857ba74547d36d6b54f"),
                new("unavailable-store", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-unavailable", "g001", "dd488672949a8bd26896171648b6dcf0a500e133c5c894555cfa04967712f5cd"),
                new("replacement-old", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-replacement", "g001", "adc83ec9f53a0c15e04f4fb61adb0d265a3ba9bee4cf40755e1d0bf19e86122f"),
                new("replacement-new", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-replacement", "g002", "335870b602b5b897dcf199f6ce7b619db5057df98863bcf1fae6022866b45393"),
                new("revoke-delete", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-revoke-delete", "g001", "e5de9a8f2d96dbb73111607c42ee2c3d38f9089d9df72c7ab5997e7cba5e7112"),
                new("crash-restart", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-crash-restart", "g001", "4def6a88eb6e61b7fbbac4965a90f963aeef96b1144c000cda53f58951275670"),
                new("backup-old", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-backup-restore", "g001", "94c87d9b953118112df5e0fc319fa6c8079e8c62be2ca50abaa176fe972dacd5"),
                new("backup-new", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-backup-restore", "g002", "82975530d1612c1984c1a9befb8f89f20d1e413858e1a48e6ef405ab225deda7"),
                new("fake-dispatch", "m1s6-wp4-c6e9226e3d95496cbda6c9142bb6b980-fake-dispatch", "g001", "57097d7dcfc1702fc8d7c39195605cfc137f888229c78372ec7cccdc4ddc9750"),
            ];
            JsonElement[] actualTargets = manifestRoot.GetProperty("disposable_namespace")
                .GetProperty("targets").EnumerateArray().ToArray();
            if (actualTargets.Length != expectedTargets.Length)
            {
                throw new InvalidDataException("The native store manifest target count is not exact.");
            }
            WindowsCredentialManagerStore store = new();
            for (int index = 0; index < actualTargets.Length; index++)
            {
                JsonElement item = actualTargets[index];
                NativeTarget target = new(
                    item.GetProperty("alias").GetString()!,
                    item.GetProperty("access_profile_id").GetString()!,
                    item.GetProperty("generation_id").GetString()!,
                    item.GetProperty("target_fingerprint_sha256").GetString()!);
                if (target != expectedTargets[index])
                {
                    throw new InvalidDataException("The native store manifest target tuple is not exact or ordered.");
                }
                Validate(target);
                if (!store.manifestTargets.TryAdd(new(target.AccessProfileId, target.GenerationId), target))
                {
                    throw new InvalidDataException("The accepted manifest repeats a native credential slot.");
                }
                store.manifestTargetOrder.Add(target);
            }
            return store;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException
            or FormatException or OverflowException)
        {
            throw new InvalidDataException("The native store manifest structure is invalid.", exception);
        }
    }

    internal static WindowsCredentialManagerStore FromProductionEnrollmentManifest(
        string manifestPath,
        string expectedSha256,
        string expectedManifestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedManifestId);
        byte[] bytes = File.ReadAllBytes(Path.GetFullPath(manifestPath));
        if (!string.Equals(
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            expectedSha256,
            StringComparison.Ordinal))
        {
            throw new InvalidDataException("The production enrollment manifest is not the exact owner-accepted artifact.");
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement root = document.RootElement;
            JsonElement profile = root.GetProperty("profile");
            JsonElement boundary = root.GetProperty("native_boundary");
            string manifestId = root.GetProperty("manifest_id").GetString()!;
            NativeTarget target = new(
                "production-enrollment",
                profile.GetProperty("access_profile_id").GetString()!,
                profile.GetProperty("generation_id").GetString()!,
                profile.GetProperty("target_fingerprint_sha256").GetString()!);
            string actualFingerprint = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(target.RawTarget)));
            string[] callOrder = boundary.GetProperty("exact_call_order").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            string[] results = boundary.GetProperty("exact_results").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            string[] collisionOrder = boundary.GetProperty("exact_collision_order").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            string[] collisionResults = boundary.GetProperty("exact_collision_results").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            JsonElement maxima = boundary.GetProperty("maximum_calls");
            if (root.GetProperty("schema_identity").GetString() != ProductionEnrollmentV4
                || manifestId != expectedManifestId
                || IsRetiredProductionIdentity(manifestId, target.AccessProfileId,
                    target.GenerationId, target.TargetFingerprintSha256)
                || root.GetProperty("packet_kind").GetString() != "EnrollOrVerifyProfile"
                || root.GetProperty("status").GetString() != "ready-for-owner-acceptance"
                || root.GetProperty("effect_authority").GetString()
                    != "none-until-owner-accepts-exact-manifest-bytes"
                || profile.GetProperty("mode").GetString() != "new-only"
                || profile.GetProperty("generation_ordinal").GetInt32() != 1
                || profile.GetProperty("revocation_epoch").GetInt32() != 0
                || profile.GetProperty("target_derivation").GetString()
                    != "Infinium:<access_profile_id>:<generation_id>"
                || profile.GetProperty("target_encoding").GetString() != "utf-8"
                || profile.GetProperty("preflight_requirement").GetString()
                    != "exact-CredReadW-ERROR_NOT_FOUND-or-stop-no-write"
                || target.TargetFingerprintSha256 != actualFingerprint
                || !callOrder.SequenceEqual(["CredReadW", "CredWriteW", "CredReadW", "CredFree"])
                || !results.SequenceEqual(["ERROR_NOT_FOUND", "success", "success", "released"])
                || !collisionOrder.SequenceEqual(["CredReadW", "CredFree"])
                || !collisionResults.SequenceEqual(["success", "released"])
                || maxima.GetProperty("CredWriteW").GetInt32() != 1
                || maxima.GetProperty("CredReadW").GetInt32() != 2
                || maxima.GetProperty("CredDeleteW").GetInt32() != 0
                || maxima.GetProperty("CredFree").GetInt32() != 1
                || maxima.GetProperty("total").GetInt32() != 4
                || boundary.GetProperty("enumeration").GetString() != "prohibited"
                || boundary.GetProperty("fallback").GetString() != "none"
                || boundary.GetProperty("overwrite").GetString() != "prohibited"
                || boundary.GetProperty("delete").GetString() != "not-authorized"
                || !ValidProductionEnrollmentWindow(root)
                || !ValidProductionEntrySurface(root.GetProperty("m1_entry_surface"))
                || root.GetProperty("provider_intent").GetProperty("provider").GetString() != "openai"
                || root.GetProperty("provider_intent").GetProperty("provider_request_permitted").GetBoolean())
            {
                throw new InvalidDataException("The production enrollment manifest does not preserve its exact finite authority.");
            }
            Validate(target);
            WindowsCredentialManagerStore store = new() { IsProductionEnrollment = true };
            store.manifestTargets.Add(new(target.AccessProfileId, target.GenerationId), target);
            store.manifestTargetOrder.Add(target);
            store.BeginScenario("wp9-production-profile-enrollment");
            return store;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException
            or FormatException or OverflowException)
        {
            throw new InvalidDataException("The production enrollment manifest structure is invalid.", exception);
        }
    }

    internal static bool IsRetiredProductionIdentity(
        string manifestId,
        string profileId,
        string generationId,
        string targetFingerprintSha256) =>
        RetiredProductionManifestIds.Contains(manifestId)
        || RetiredProductionProfileIds.Contains(profileId)
        || RetiredProductionGenerationIds.Contains(generationId)
        || RetiredProductionTargetFingerprints.Contains(targetFingerprintSha256);

    private static bool ValidProductionEnrollmentWindow(JsonElement root)
    {
        if (!DateTimeOffset.TryParseExact(root.GetProperty("prepared_at_utc").GetString(), "O",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset prepared)
            || !DateTimeOffset.TryParseExact(root.GetProperty("expires_at_utc").GetString(), "O",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTimeOffset expires))
        {
            return false;
        }
        return prepared.Offset == TimeSpan.Zero && expires.Offset == TimeSpan.Zero && prepared < expires;
    }

    private static bool ValidProductionEntrySurface(JsonElement surface) =>
        surface.GetProperty("owner").GetString() == "one-shot-credential-helper"
        && surface.GetProperty("presentation").GetString() == "direct-helper-owned-native-modal"
        && surface.GetProperty("masked").GetBoolean()
        && surface.GetProperty("paste_permitted").GetBoolean()
        && !surface.GetProperty("renderer_receives_or_retains_secret").GetBoolean()
        && surface.GetProperty("readiness_deadline_seconds").GetInt32() == 10
        && surface.GetProperty("response_deadline_seconds").GetInt32() == 600
        && surface.GetProperty("input_bound").GetString()
            == "live-character-length-1-through-2560-and-utf8-byte-length-1-through-2560-no-truncation";

    internal static WindowsCredentialManagerStore FromRecoveryManifest(JsonElement manifestRoot)
    {
        string schemaIdentity = manifestRoot.GetProperty("schema_identity").GetString()
            ?? throw new InvalidDataException("Recovery schema identity is absent.");
        int expectedTargetCount;
        NativeTarget[]? exactCurrentTargets = null;
        if (schemaIdentity == "infinium.repository.wp4-credential-native-recovery/1.0.0")
        {
            expectedTargetCount = 12;
        }
        else if (schemaIdentity == "infinium.repository.wp4-credential-native-recovery/1.1.0"
            && manifestRoot.GetProperty("manifest_id").GetString()
                == "infinium.m1-s6.wp4.credential-native-recovery/df29a608-cc46-4151-bb0b-1a03acb1cdff")
        {
            JsonElement limits = manifestRoot.GetProperty("limits");
            if (limits.GetProperty("targets").GetInt32() != 2
                || limits.GetProperty("CredReadW").GetInt32() != 6
                || limits.GetProperty("CredDeleteW").GetInt32() != 2
                || limits.GetProperty("CredFree").GetInt32() != 2
                || limits.GetProperty("total_native_calls").GetInt32() != 10)
            {
                throw new InvalidDataException("Current recovery finite limits differ from exact authority.");
            }
            expectedTargetCount = 2;
            exactCurrentTargets =
            [
                new("backup-new",
                    "m1s6-wp4-ad876b9a9f454eb48d125970d76dd4ea-backup-restore", "g002",
                    "d9221f7aac7ababf9e3efbf6ef69b03d2e9c8b0f51c1c552862958d5f3eff061"),
                new("fake-dispatch",
                    "m1s6-wp4-ad876b9a9f454eb48d125970d76dd4ea-fake-dispatch", "g001",
                    "c27212cc4f0720e9fd20f7a2aff397402257bd53ad6d568048b217ac3e3df963"),
            ];
        }
        else if (schemaIdentity == "infinium.repository.wp4-credential-native-recovery/1.2.0"
            && manifestRoot.GetProperty("manifest_id").GetString()
                == "infinium.m1-s6.wp4.credential-native-recovery/8b7fc811-7cd2-4c2a-abe1-506bd7b06bf5")
        {
            JsonElement limits = manifestRoot.GetProperty("limits");
            if (limits.GetProperty("targets").GetInt32() != 2
                || limits.GetProperty("CredReadW").GetInt32() != 6
                || limits.GetProperty("CredDeleteW").GetInt32() != 2
                || limits.GetProperty("CredFree").GetInt32() != 2
                || limits.GetProperty("total_native_calls").GetInt32() != 10)
            {
                throw new InvalidDataException("Current recovery finite limits differ from exact authority.");
            }
            expectedTargetCount = 2;
            exactCurrentTargets =
            [
                new("backup-new",
                    "m1s6-wp4-e3f76cd645c14e3aa84bfa3251b3cb60-backup-restore", "g002",
                    "b78f660da620c5feee10adff48401ac1b4bc3ec0daec2e35bc39b399d55b41b3"),
                new("fake-dispatch",
                    "m1s6-wp4-e3f76cd645c14e3aa84bfa3251b3cb60-fake-dispatch", "g001",
                    "08e0f7330185d89fa471d83434e768a3d9d54961d325e5b44b5d84f664cc6b02"),
            ];
        }
        else if (schemaIdentity == "infinium.repository.wp4-credential-native-recovery/1.3.0"
            && manifestRoot.GetProperty("manifest_id").GetString()
                == "infinium.m1-s6.wp4.credential-native-recovery/6232bae5-f735-4db7-a74f-7ede9f67b752")
        {
            JsonElement limits = manifestRoot.GetProperty("limits");
            if (limits.GetProperty("targets").GetInt32() != 12
                || limits.GetProperty("CredReadW").GetInt32() != 36
                || limits.GetProperty("CredDeleteW").GetInt32() != 12
                || limits.GetProperty("CredFree").GetInt32() != 12
                || limits.GetProperty("total_native_calls").GetInt32() != 60)
            {
                throw new InvalidDataException("Current recovery finite limits differ from exact authority.");
            }
            expectedTargetCount = 12;
            exactCurrentTargets =
            [
                new("interactive-primary", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-interactive-primary", "g001", "821904462accf62dc1d6317199cd76091f7c271a599ac27ca970d5575002f3a4"),
                new("interactive-cancel", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-interactive-cancel", "g001", "e432bd2911ef3b00088e03bd05c40fc094489e89f4905ae8305e2494d708e9c7"),
                new("size-valid", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-size-valid", "g001", "53cbe11d187d98e681719a42b2e39373ac14ea8a86a1c8bc5c2f7df819bef7cc"),
                new("size-oversize", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-size-oversize", "g001", "250b7287feac38456b9e9f4a8dd9ecf846e03e210c588cd52fd654cf6b25c6f8"),
                new("unavailable-store", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-unavailable", "g001", "8b1da75ab27fb15d10d5703989430dc28386a3238b4f4cb62de7c701350f4bf7"),
                new("replacement-old", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-replacement", "g001", "545f3a638456276cf35967e02449f4cbb9b8c05196b39005de8d16b5e44d9ad3"),
                new("replacement-new", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-replacement", "g002", "f166ca075fb6c66e5d2c4782b42f0c5c35f32bd00f9a867b3a63b9dcfac55b0c"),
                new("revoke-delete", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-revoke-delete", "g001", "2e7ff725c4f2c404b59cebe08e646d71ae9f3112e9369142002a35de0c875619"),
                new("crash-restart", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-crash-restart", "g001", "c3b90f33f1dfb98b44f1db7b3bdc550175a55333a21832c58a330696d840740a"),
                new("backup-old", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-backup-restore", "g001", "b1a4d68aaaefd0f62ae7979c994ca6193a34f273e463f5395e87d474cbb9f40a"),
                new("backup-new", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-backup-restore", "g002", "c3a2805323e54bdc4aba66b5d3e33686ce12a525dfed82c4b2e463deea2c28b1"),
                new("fake-dispatch", "m1s6-wp4-e6e046514cd54f5d8b465ec84a81cbbe-fake-dispatch", "g001", "d189000ff046ad5062614e915882796017b97778178875a58a77ebde902ab1c8"),
            ];
        }
        else if (schemaIdentity == "infinium.repository.wp4-credential-native-recovery/1.4.0"
            && manifestRoot.GetProperty("manifest_id").GetString()
                == "infinium.m1-s6.wp4.credential-native-recovery/dd412ecc-3b2c-4628-8865-bc8574a357c7")
        {
            JsonElement limits = manifestRoot.GetProperty("limits");
            if (limits.GetProperty("targets").GetInt32() != 1
                || limits.GetProperty("CredWriteW").GetInt32() != 0
                || limits.GetProperty("CredReadW").GetInt32() != 3
                || limits.GetProperty("CredDeleteW").GetInt32() != 1
                || limits.GetProperty("CredFree").GetInt32() != 1
                || limits.GetProperty("total_native_calls").GetInt32() != 5)
            {
                throw new InvalidDataException("Current recovery finite limits differ from exact authority.");
            }
            expectedTargetCount = 1;
            exactCurrentTargets =
            [
                new("backup-new",
                    "m1s6-wp4-4936dcefa0f4430298990afd99b19799-backup-restore", "g002",
                    "01fcbe4a9138bcc10819e04cdadc9f83a592c022b4b436bbd2d29f50b52816c7"),
            ];
        }
        else if (schemaIdentity == "infinium.repository.wp4-credential-native-recovery/1.5.0"
            && manifestRoot.GetProperty("manifest_id").GetString()
                == "infinium.m1-s6.wp4.credential-native-recovery/040817c8-0a87-480a-915c-71dc2fe54da3")
        {
            JsonElement limits = manifestRoot.GetProperty("limits");
            if (limits.GetProperty("targets").GetInt32() != 12
                || limits.GetProperty("CredWriteW").GetInt32() != 0
                || limits.GetProperty("CredReadW").GetInt32() != 24
                || limits.GetProperty("CredDeleteW").GetInt32() != 12
                || limits.GetProperty("CredFree").GetInt32() != 12
                || limits.GetProperty("total_native_calls").GetInt32() != 48)
            {
                throw new InvalidDataException("Current recovery finite limits differ from exact authority.");
            }
            expectedTargetCount = 12;
            exactCurrentTargets =
            [
                new("interactive-primary", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-interactive-primary", "g001", "04b35e2718e202cb0a6bfef233dbe033c791aa02b2261e1779813d310bd3baad"),
                new("interactive-cancel", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-interactive-cancel", "g001", "24f709437c97a67819b06270d0d211aaae426bbfbd56f83774106bd2f7da5277"),
                new("size-valid", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-size-valid", "g001", "6aa891fe3db76c45c994b7b7a461f5242621226a788c489d0bcecde87b78e2dd"),
                new("size-oversize", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-size-oversize", "g001", "01338cb4af7abf7d50b49313cce237db80a389ba1ff2c01d23a8a96ff02d66f2"),
                new("unavailable-store", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-unavailable", "g001", "a7af4cc90f3f3021cf2a7220f92d247165fcaaf1f6b41410ca2d34fd55582895"),
                new("replacement-old", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-replacement", "g001", "e82ee891429ff57587ea0f7f35f6f5ef98ae96a9d5d75da5ad7ee716a645ae77"),
                new("replacement-new", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-replacement", "g002", "fea103ab44d0057a2a9cc10de5792ffec891cc6fe17086fe960a979d87eb852a"),
                new("revoke-delete", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-revoke-delete", "g001", "92cf677dd3dfc6509d75c9d502c12ae3d4b9295b2c25b4327b965f252b10649d"),
                new("crash-restart", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-crash-restart", "g001", "c36ea4643f97ff6a68d1880445669f213e1ef1e2b71487b3179d6102a1ce0f95"),
                new("backup-old", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-backup-restore", "g001", "1ce6dceb1deea0485f5c56b9dce06eb3d44cda389ff0805291a9719eb1de865f"),
                new("backup-new", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-backup-restore", "g002", "11d51fa6e870709f346f61e931a91ab8cf5336b689f8ddcfc427283d71fb1d0a"),
                new("fake-dispatch", "m1s6-wp4-076b981a9d324e6aaf351e7017e0f833-fake-dispatch", "g001", "bcb55be3c8d4f1b89103d28cd5fa40d97fcdbc528ffd2f4513f4f3b12770c0b1"),
            ];
        }
        else
        {
            throw new InvalidDataException("Recovery schema/identity is not an accepted exact authority.");
        }
        WindowsCredentialManagerStore store = new();
        int targetIndex = 0;
        foreach (JsonElement item in manifestRoot.GetProperty("disposable_namespace").GetProperty("targets").EnumerateArray())
        {
            NativeTarget target = new(
                item.GetProperty("alias").GetString()!,
                item.GetProperty("access_profile_id").GetString()!,
                item.GetProperty("generation_id").GetString()!,
                item.GetProperty("target_fingerprint_sha256").GetString()!);
            Validate(target);
            if (exactCurrentTargets is not null
                && (targetIndex >= exactCurrentTargets.Length || target != exactCurrentTargets[targetIndex]))
            {
                throw new InvalidDataException("Current recovery target order or identity differs from exact authority.");
            }
            targetIndex++;
            if (!store.manifestTargets.TryAdd(new(target.AccessProfileId, target.GenerationId), target))
            {
                throw new InvalidDataException("The recovery manifest repeats a native credential slot.");
            }
            store.manifestTargetOrder.Add(target);
        }
        if (store.manifestTargets.Count != expectedTargetCount)
        {
            throw new InvalidDataException($"Recovery requires exactly {expectedTargetCount} known targets.");
        }
        return store;
    }

    internal IReadOnlyList<NativeTarget> ManifestTargets => manifestTargetOrder.ToArray();

    public NativeCallCounts CallCounts => new(writeCount, readCount, deleteCount, freeCount,
        checked(writeCount + readCount + deleteCount + freeCount));
    internal bool NamespaceReuseBlocked => reuseGuard.IsBlocked;
    internal string? NamespaceReuseBlockReason => reuseGuard.Reason;
    public IReadOnlyList<NativeCallTraceEntry> CallTrace => callTrace;
    internal IReadOnlyList<NativeRawTargetCanary> RawTargetCanaries => manifestTargets.Values
        .SelectMany(target => new[]
        {
            new NativeRawTargetCanary("utf-8", Encoding.UTF8.GetBytes(target.RawTarget)),
            new NativeRawTargetCanary("utf-16le", Encoding.Unicode.GetBytes(target.RawTarget)),
        })
        .ToArray();

    public void BeginScenario(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        scenario = value;
    }

    void ISyntheticSecureStore.WriteExact(SyntheticCredentialSlot slot, ReadOnlySpan<byte> secret) =>
        WriteExact(Resolve(slot), secret);

    bool ISyntheticSecureStore.VerifyExact(SyntheticCredentialSlot slot) => Exists(Resolve(slot));

    byte[] ISyntheticSecureStore.ReadExact(SyntheticCredentialSlot slot) => ReadExact(Resolve(slot));

    bool ISyntheticSecureStore.DeleteExact(SyntheticCredentialSlot slot)
    {
        if (string.Equals(slot.GenerationId, deleteFailureGenerationId, StringComparison.Ordinal))
        {
            deleteFailureGenerationId = null;
            throw new IOException("Injected exact predecessor deletion failure before the native call.");
        }
        NativeTarget target = Resolve(slot);
        if (!Exists(target)) { return false; }
        return DeleteAndProveAbsent(target);
    }

    bool ISyntheticSecureStore.ConsumeOneUseNonce(ReadOnlySpan<byte> nonceFingerprint)
    {
        if (nonceFingerprint.Length != 32)
        {
            throw new InvalidDataException("The one-use nonce fingerprint is not SHA-256 sized.");
        }
        return consumedNonces.Add(Convert.ToHexStringLower(nonceFingerprint));
    }

    private NativeTarget Resolve(SyntheticCredentialSlot slot) =>
        manifestTargets.TryGetValue(slot, out NativeTarget? target)
            ? target
            : throw new InvalidDataException("The helper requested a credential slot absent from the accepted manifest.");

    public void Dispose() => consumedNonces.Clear();

    public void SetFault(WindowsCredentialFault value) => fault = value;

    internal void ConfigureQualificationPhase(
        Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentV2 assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        CredentialNativeQualificationPhaseV2 phase = CredentialNativeQualificationPhasesV2.Parse(
            assignment.AssignmentId,
            assignment.AssignmentKind);
        fault = phase.UnavailableBeforeNativeCall
            ? WindowsCredentialFault.UnavailableBeforeNativeCall
            : WindowsCredentialFault.None;
        deleteFailureGenerationId = null;
        if (phase.FailExactPredecessorDeleteBeforeNativeCall)
        {
            string successor = assignment.Credential?.GenerationId?.Value
                ?? throw new InvalidDataException("Replacement interruption requires a successor generation.");
            deleteFailureGenerationId = manifestTargets.Keys
                .Where(slot => slot.ProfileId == assignment.AccessProfileId?.Value
                    && slot.GenerationId != successor)
                .Select(slot => slot.GenerationId)
                .SingleOrDefault()
                ?? throw new InvalidDataException("Replacement interruption requires one exact predecessor target.");
        }
        BeginScenario(assignment.AssignmentId);
    }

    public bool WasWrittenByThisRun(NativeTarget target) =>
        writtenTargetFingerprints.Contains(target.TargetFingerprintSha256);

    public void MarkExternalWrite(NativeTarget target)
    {
        Validate(target);
        writtenTargetFingerprints.Add(target.TargetFingerprintSha256);
    }

    public void AddExternalTrace(IReadOnlyList<NativeCallTraceEntry> external)
    {
        NativeCallTraceValidator.Validate(external);
        Dictionary<long, long> allocations = [];
        foreach (NativeCallTraceEntry item in external)
        {
            long? allocationId = item.AllocationId;
            long? pairedAllocationId = item.PairedAllocationId;
            if (allocationId is not null)
            {
                long next = ++allocationSequence;
                allocations.Add(allocationId.Value, next);
                allocationId = next;
            }
            if (pairedAllocationId is not null) { pairedAllocationId = allocations[pairedAllocationId.Value]; }
            callTrace.Add(item with
            {
                Sequence = ++sequence,
                AllocationId = allocationId,
                PairedAllocationId = pairedAllocationId,
            });
            switch (item.Operation)
            {
                case "CredWriteW": writeCount++; break;
                case "CredReadW": readCount++; break;
                case "CredDeleteW": deleteCount++; break;
                case "CredFree": freeCount++; break;
            }
        }
    }

    public void WriteExact(NativeTarget target, ReadOnlySpan<byte> secret)
    {
        Validate(target);
        DemandNativeCallAllowed();
        RequireAvailable(cleanup: false);
        if (secret.IsEmpty || secret.Length > MaximumBlobBytes)
        {
            throw new InvalidDataException("The native generic credential is empty or exceeds 2,560 bytes.");
        }
        if (Exists(target))
        {
            reuseGuard.Block("preflight-collision");
            throw new InvalidOperationException(
                "The exact disposable native target already exists; no overwrite or later native call is permitted.");
        }
        byte[] copy = secret.ToArray();
        try
        {
            unsafe
            {
                fixed (byte* secretPointer = copy)
                {
                    NativeCredential credential = new()
                    {
                        Type = CredentialTypeGeneric,
                        TargetName = target.RawTarget,
                        CredentialBlobSize = checked((uint)copy.Length),
                        CredentialBlob = (nint)secretPointer,
                        Persist = CredentialPersistLocalMachine,
                        UserName = $"{target.AccessProfileId}/{target.GenerationId}",
                    };
                    writeCount++;
                    if (!CredWriteW(ref credential, 0))
                    {
                        Record("CredWriteW", target, "win32-error:" + Marshal.GetLastWin32Error());
                        throw NativeFailure("CredWriteW");
                    }
                    Record("CredWriteW", target, "success");
                    writtenTargetFingerprints.Add(target.TargetFingerprintSha256);
                }
            }
        }
        finally { CryptographicOperations.ZeroMemory(copy); }
    }

    public byte[] ReadExact(NativeTarget target)
    {
        Validate(target);
        DemandNativeCallAllowed();
        RequireAvailable(cleanup: false);
        readCount++;
        if (!CredReadW(target.RawTarget, CredentialTypeGeneric, 0, out SafeCredentialHandle? handle))
        {
            Record("CredReadW", target, "win32-error:" + Marshal.GetLastWin32Error());
            throw NativeFailure("CredReadW");
        }
        long allocationId = ++allocationSequence;
        Record("CredReadW", target, "success", allocationId: allocationId);
        handle.Attach(allocationId, target.TargetFingerprintSha256, scenario, RecordFree);
        using (handle)
        {
            NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(handle.DangerousGetHandle());
            if (credential.Type != CredentialTypeGeneric || credential.CredentialBlobSize > MaximumBlobBytes)
            {
                throw new InvalidDataException("The exact native credential record is malformed or oversized.");
            }
            byte[] result = new byte[credential.CredentialBlobSize];
            if (result.Length > 0)
            {
                try { Marshal.Copy(credential.CredentialBlob, result, 0, result.Length); }
                finally { ZeroNativeBlob(credential); }
            }
            return result;
        }
    }

    public bool Exists(NativeTarget target)
    {
        Validate(target);
        DemandNativeCallAllowed();
        RequireAvailable(cleanup: false);
        readCount++;
        if (CredReadW(target.RawTarget, CredentialTypeGeneric, 0, out SafeCredentialHandle? handle))
        {
            long allocationId = ++allocationSequence;
            Record("CredReadW", target, "success", allocationId: allocationId);
            handle.Attach(allocationId, target.TargetFingerprintSha256, scenario, RecordFree);
            ZeroNativeBlob(Marshal.PtrToStructure<NativeCredential>(handle.DangerousGetHandle()));
            handle.Dispose();
            return true;
        }
        int error = Marshal.GetLastWin32Error();
        Record("CredReadW", target, error == ErrorNotFound ? "ERROR_NOT_FOUND" : "win32-error:" + error);
        if (error == ErrorNotFound) { return false; }
        throw new Win32Exception(error, "Exact-target CredReadW failed.");
    }

    public bool DeleteAndProveAbsent(NativeTarget target)
    {
        Validate(target);
        DemandNativeCallAllowed();
        try
        {
            RequireAvailable(cleanup: true);
            deleteCount++;
            if (!CredDeleteW(target.RawTarget, CredentialTypeGeneric, 0))
            {
                int error = Marshal.GetLastWin32Error();
                Record("CredDeleteW", target, error == ErrorNotFound ? "ERROR_NOT_FOUND" : "win32-error:" + error);
                if (error != ErrorNotFound) { throw new Win32Exception(error, "Exact-target CredDeleteW failed."); }
            }
            else { Record("CredDeleteW", target, "success"); }
            return !Exists(target);
        }
        catch (Exception exception) when (exception is IOException or Win32Exception)
        {
            reuseGuard.Block("cleanup-outcome-ambiguous-or-failed");
            throw;
        }
    }

    private void RequireAvailable(bool cleanup)
    {
        if (fault == WindowsCredentialFault.UnavailableBeforeNativeCall)
        {
            throw new IOException("Injected unavailable native credential store before a native call.");
        }
    }

    private void DemandNativeCallAllowed()
    {
        reuseGuard.DemandNativeCallAllowed();
        deadline.DemandRemaining(scenario);
    }

    private void Record(
        string operation,
        NativeTarget target,
        string result,
        long? allocationId = null,
        long? pairedAllocationId = null) =>
        callTrace.Add(new(++sequence, operation, target.TargetFingerprintSha256, scenario,
            result, allocationId, pairedAllocationId));

    private void RecordFree(long allocationId, string targetFingerprint, string allocationScenario)
    {
        freeCount++;
        callTrace.Add(new(++sequence, "CredFree", targetFingerprint, allocationScenario,
            "released", null, allocationId));
    }

    private static void Validate(NativeTarget target)
    {
        static bool Valid(string value) => value.Length is > 0 and <= 160
            && value.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');
        if (!Valid(target.AccessProfileId) || !Valid(target.GenerationId)
            || target.TargetFingerprintSha256.Length != 64)
        {
            throw new InvalidDataException("The exact native credential target identity is invalid.");
        }
        string actual = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(target.RawTarget)));
        if (actual != target.TargetFingerprintSha256)
        {
            throw new InvalidDataException("The exact native credential target fingerprint does not match its derivation.");
        }
    }

    private static Win32Exception NativeFailure(string operation) =>
        new(Marshal.GetLastWin32Error(), $"Exact-target {operation} failed.");

    private static unsafe void ZeroNativeBlob(NativeCredential credential)
    {
        if (credential.CredentialBlob != 0 && credential.CredentialBlobSize > 0)
        {
            new Span<byte>((void*)credential.CredentialBlob, checked((int)credential.CredentialBlobSize)).Clear();
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        internal uint Flags;
        internal uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] internal string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? Comment;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        internal uint CredentialBlobSize;
        internal nint CredentialBlob;
        internal uint Persist;
        internal uint AttributeCount;
        internal nint Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] internal string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] internal string UserName;
    }

    private sealed class SafeCredentialHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private long allocationId;
        private string targetFingerprint = "unattached";
        private string scenario = "unattached";
        private Action<long, string, string>? released;
        private SafeCredentialHandle() : base(true) { }
        internal void Attach(long id, string target, string scenarioValue, Action<long, string, string> callback)
        {
            allocationId = id;
            targetFingerprint = target;
            scenario = scenarioValue;
            released = callback;
        }
        protected override bool ReleaseHandle()
        {
            CredFree(handle);
            released?.Invoke(allocationId, targetFingerprint, scenario);
            return true;
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(
        string target, uint type, uint flags, out SafeCredentialHandle credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern void CredFree(nint buffer);
}

internal sealed class NativeNamespaceBlockedException(string reason)
    : InvalidOperationException($"The disposable native namespace is terminally blocked: {reason}");

internal sealed class NativeNamespaceReuseGuard
{
    private string? reason;
    public bool IsBlocked => reason is not null;
    public string? Reason => reason;

    public void Block(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        reason ??= value;
    }

    public void DemandNativeCallAllowed()
    {
        if (reason is not null) { throw new NativeNamespaceBlockedException(reason); }
    }
}

internal sealed class FiniteNativeDeadline
{
    private readonly long limitMilliseconds;
    private readonly Func<long> elapsedMilliseconds;
    private int checks;

    private FiniteNativeDeadline(long limitMilliseconds, Func<long> elapsedMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limitMilliseconds);
        this.limitMilliseconds = limitMilliseconds;
        this.elapsedMilliseconds = elapsedMilliseconds;
    }

    public static FiniteNativeDeadline Start(TimeSpan limit)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        return new(checked((long)limit.TotalMilliseconds), () => stopwatch.ElapsedMilliseconds);
    }

    internal static FiniteNativeDeadline ForTest(long limitMilliseconds, Func<long> elapsedMilliseconds) =>
        new(limitMilliseconds, elapsedMilliseconds);

    public void DemandRemaining(string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        checks++;
        if (elapsedMilliseconds() >= limitMilliseconds)
        {
            throw new TimeoutException($"The native qualification deadline elapsed before {operation}.");
        }
    }

    public NativeDeadlineEvidence Snapshot()
    {
        long elapsed = elapsedMilliseconds();
        return new(limitMilliseconds, elapsed, checks, elapsed < limitMilliseconds);
    }
}

internal static class NativeCallTraceValidator
{
    private static readonly HashSet<string> Allowed =
        ["CredWriteW", "CredReadW", "CredDeleteW", "CredFree"];

    public static void Validate(IReadOnlyList<NativeCallTraceEntry> trace)
    {
        Dictionary<long, NativeCallTraceEntry> allocations = [];
        HashSet<long> freed = [];
        for (int index = 0; index < trace.Count; index++)
        {
            NativeCallTraceEntry item = trace[index];
            if (item.Sequence != index + 1 || !Allowed.Contains(item.Operation)
                || item.TargetFingerprintSha256.Length != 64
                || string.IsNullOrWhiteSpace(item.Scenario)
                || string.IsNullOrWhiteSpace(item.Result))
            {
                throw new InvalidDataException("The canonical native-call trace is malformed or unordered.");
            }
            if (item.Operation == "CredReadW" && item.Result == "success")
            {
                if (item.AllocationId is null || item.PairedAllocationId is not null
                    || !allocations.TryAdd(item.AllocationId.Value, item))
                {
                    throw new InvalidDataException("A successful CredReadW lacks a unique allocation identity.");
                }
            }
            else if (item.Operation == "CredFree")
            {
                if (item.AllocationId is not null || item.PairedAllocationId is null
                    || !allocations.TryGetValue(item.PairedAllocationId.Value, out NativeCallTraceEntry? read)
                    || read.TargetFingerprintSha256 != item.TargetFingerprintSha256
                    || read.Scenario != item.Scenario
                    || !freed.Add(item.PairedAllocationId.Value))
                {
                    throw new InvalidDataException("CredFree is not paired exactly once with its successful read allocation.");
                }
            }
            else if (item.AllocationId is not null || item.PairedAllocationId is not null)
            {
                throw new InvalidDataException("Only successful reads and their CredFree release may name allocations.");
            }
        }
        if (allocations.Keys.Any(id => !freed.Contains(id)))
        {
            throw new InvalidDataException("A successful CredReadW allocation was not released exactly once.");
        }
    }
}

internal sealed record NativeCanarySurface(string Name, string Kind, byte[] Bytes)
{
    internal static NativeCanarySurface FromText(string name, string value) =>
        new(name, "captured-text", Encoding.UTF8.GetBytes(value));

    internal static NativeCanarySurface FromFile(string name, string path) =>
        new(name, "retained-file", File.ReadAllBytes(path));
}

internal sealed record NativeRawTargetCanary(string Encoding, byte[] Bytes);

internal static class NativeCanaryScanner
{
    public static NativeCanaryEvidence Scan(
        ReadOnlySpan<byte> secretCanary,
        IEnumerable<NativeRawTargetCanary> rawTargetCanaries,
        IReadOnlyList<NativeCanarySurface> surfaces)
    {
        NativeRawTargetCanary[] targets = rawTargetCanaries
            .Select(value => new NativeRawTargetCanary(value.Encoding, value.Bytes.ToArray()))
            .ToArray();
        string[] encodings = targets.Select(value => value.Encoding)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (!encodings.SequenceEqual(["utf-16le", "utf-8"], StringComparer.Ordinal))
        {
            throw new InvalidDataException("Raw-target canaries must cover exact UTF-8 and UTF-16LE encodings.");
        }
        List<NativeCanarySurfaceEvidence> inventory = [];
        int secretMatches = 0;
        int targetMatches = 0;
        foreach (NativeCanarySurface surface in surfaces)
        {
            int surfaceSecretMatches = secretCanary.IsEmpty ? 0 : Count(surface.Bytes, secretCanary);
            int surfaceTargetMatches = targets.Sum(target => Count(surface.Bytes, target.Bytes));
            secretMatches += surfaceSecretMatches;
            targetMatches += surfaceTargetMatches;
            inventory.Add(new(surface.Name, surface.Kind, surface.Bytes.LongLength,
                surfaceSecretMatches, surfaceTargetMatches));
        }
        return new(secretMatches, targetMatches, ["utf-8", "utf-16le"], inventory);
    }

    private static int Count(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        int count = 0;
        int offset = 0;
        while (offset <= haystack.Length - needle.Length)
        {
            int match = haystack[offset..].IndexOf(needle);
            if (match < 0) { break; }
            count++;
            offset += match + Math.Max(needle.Length, 1);
        }
        return count;
    }
}

internal static class NativeMaskedEntryDialog
{
    internal static readonly TimeSpan ReadinessDeadline = TimeSpan.FromSeconds(10);
    private const uint WsOverlapped = 0x00000000;
    private const uint WsCaption = 0x00C00000;
    private const uint WsSysMenu = 0x00080000;
    private const uint WsVisible = 0x10000000;
    private const uint WsChild = 0x40000000;
    private const uint WsBorder = 0x00800000;
    private const uint EsPassword = 0x0020;
    private const int GwlStyle = -16;
    private const int GwlWndProc = -4;
    private const uint WmClose = 0x0010;
    private const uint WmCommand = 0x0111;
    private const uint WmKeyDown = 0x0100;
    private const uint WmCut = 0x0300;
    private const uint WmCopy = 0x0301;
    private const uint WmPaste = 0x0302;
    private const uint PmRemove = 0x0001;
    private const int VkReturn = 0x0D;
    private const int VkEscape = 0x1B;
    private const int SubmitButtonId = 1001;
    private const int CancelButtonId = 1002;
    internal const string SubmitInstruction =
        "Enter a disposable test value, then click Submit. Never use a real credential.";
    internal const string CancelInstruction = "Leave the field blank, then click Cancel.";
    private const int SwShow = 5;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpShowWindow = 0x0040;
    private const uint DesktopReadObjects = 0x0001;
    private const uint DesktopSwitchDesktop = 0x0100;
    private const int UoiName = 2;
    private const uint DwmwaCloaked = 14;
    private static readonly nint HwndTopmost = new(-1);
    private static readonly nint HwndNotTopmost = new(-2);

    internal static NativeEntryCapture Capture(
        TimeSpan deadline,
        NativeEntryInteraction interaction = NativeEntryInteraction.Submit)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Native masked credential entry requires Windows.");
        }
        byte[] result = [];
        Exception? failure = null;
        NativeEntryTerminalState terminal = NativeEntryTerminalState.Failed;
        NativeEntryStateMachine state = new();
        NativeEntryWindowCommandRouter commandRouter = new();
        uint nativeThreadId = 0;
        nint activeWindow = 0;
        Thread thread = new(() =>
        {
            char[] captured = new char[WindowsCredentialManagerStore.MaximumBlobBytes + 1];
            nint window = 0;
            nint edit = 0;
            nint instruction = 0;
            nint submit = 0;
            nint cancel = 0;
            nint inputDesktop = 0;
            string? windowClass = null;
            ushort windowClassAtom = 0;
            NativeWindowProcedure? editProcedure = null;
            NativeWindowProcedure? windowProcedure = null;
            nint originalEditProcedure = 0;
            try
            {
                nativeThreadId = GetCurrentThreadId();
                nint module = GetModuleHandleW(null);
                RequireMatchingWindowClassInstance(module, module);
                windowClass = $"InfiniumWp4Entry-{Environment.ProcessId}-{nativeThreadId}";
                windowProcedure = (handle, message, wParam, lParam) =>
                {
                    if (commandRouter.Route(
                        message, handle, wParam, lParam, window, submit, cancel))
                    {
                        return 0;
                    }
                    return DefWindowProcW(handle, message, wParam, lParam);
                };
                WindowClassEx windowClassDefinition = new()
                {
                    Size = checked((uint)Marshal.SizeOf<WindowClassEx>()),
                    Instance = module,
                    WindowProcedure = Marshal.GetFunctionPointerForDelegate(windowProcedure),
                    ClassName = windowClass,
                };
                windowClassAtom = RegisterClassExW(ref windowClassDefinition);
                if (windowClassAtom == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Native entry window class registration failed.");
                }
                string title = interaction == NativeEntryInteraction.Submit
                    ? "Infinium disposable credential - submit"
                    : "Infinium disposable credential - cancel";
                window = CreateWindowExW(0, windowClass, title,
                    WsOverlapped | WsCaption | WsSysMenu | WsVisible, 100, 100, 540, 200,
                    0, 0, module, 0);
                if (window == 0) { throw new Win32Exception(Marshal.GetLastWin32Error(), "Native entry window creation failed."); }
                Interlocked.Exchange(ref activeWindow, window);
                instruction = CreateWindowExW(0, "STATIC",
                    interaction == NativeEntryInteraction.Submit
                        ? SubmitInstruction
                        : CancelInstruction,
                    WsChild | WsVisible, 20, 15, 490, 20, window, 0, 0, 0);
                edit = CreateWindowExW(0, "EDIT", null,
                    WsChild | WsVisible | WsBorder | EsPassword, 20, 45, 490, 28,
                    window, 0, 0, 0);
                if (edit == 0) { throw new Win32Exception(Marshal.GetLastWin32Error(), "Native masked entry control creation failed."); }
                submit = CreateWindowExW(0, "BUTTON", "Submit", WsChild | WsVisible,
                    305, 90, 95, 30, window, SubmitButtonId, 0, 0);
                cancel = CreateWindowExW(0, "BUTTON", "Cancel", WsChild | WsVisible,
                    415, 90, 95, 30, window, CancelButtonId, 0, 0);
                if (instruction == 0 || submit == 0 || cancel == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Native entry instruction/button creation failed.");
                }
                if ((GetWindowLongPtrW(edit, GwlStyle).ToInt64() & EsPassword) == 0)
                {
                    throw new InvalidOperationException("Native credential entry control is not masked.");
                }
                editProcedure = (handle, message, wParam, lParam) =>
                {
                    if (message is WmCut or WmCopy or WmPaste)
                    {
                        state.RecordClipboardMessageBlocked();
                        return 0;
                    }
                    return CallWindowProcW(originalEditProcedure, handle, message, wParam, lParam);
                };
                originalEditProcedure = SetWindowLongPtrW(
                    edit,
                    GwlWndProc,
                    Marshal.GetFunctionPointerForDelegate(editProcedure));
                if (originalEditProcedure == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Native entry clipboard boundary could not be installed.");
                }
                _ = ShowWindow(window, SwShow);
                _ = UpdateWindow(window);
                inputDesktop = OpenInputDesktop(0, false, DesktopReadObjects | DesktopSwitchDesktop);
                if (inputDesktop == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Native entry cannot open the interactive input desktop.");
                }
                nint threadDesktop = GetThreadDesktop(nativeThreadId);
                string inputDesktopName = DesktopName(inputDesktop);
                string threadDesktopName = DesktopName(threadDesktop);
                Stopwatch readinessTimer = Stopwatch.StartNew();
                NativeEntryReadinessEvidence? readiness = null;
                NativeEntryReadinessEvidence? lastReadiness = null;
                while (readinessTimer.Elapsed < ReadinessDeadline)
                {
                    while (PeekMessageW(out NativeMessage message, 0, 0, 0, PmRemove))
                    {
                        PreReadinessMessageDisposition disposition = ClassifyPreReadinessMessage(
                            message.Message, message.Window, message.WParam, message.LParam,
                            window, instruction, edit, submit, cancel);
                        if (disposition == PreReadinessMessageDisposition.RejectTerminal)
                        {
                            state.RecordPreReadinessTerminalMessage();
                            break;
                        }
                        if (disposition is PreReadinessMessageDisposition.IgnoreUnowned
                            or PreReadinessMessageDisposition.RejectEditContent)
                        {
                            state.RecordPreReadinessIgnoredMessage();
                            break;
                        }
                        _ = TranslateMessage(in message);
                        _ = DispatchMessageW(in message);
                        break;
                    }
                    _ = SetWindowPos(window, HwndTopmost, 0, 0, 0, 0,
                        SwpNoMove | SwpNoSize | SwpShowWindow);
                    _ = SetWindowPos(window, HwndNotTopmost, 0, 0, 0, 0,
                        SwpNoMove | SwpNoSize | SwpShowWindow);
                    _ = BringWindowToTop(window);
                    _ = SetForegroundWindow(window);
                    _ = SetFocus(edit);
                    readiness = MeasureReadiness(
                        window,
                        instruction,
                        edit,
                        submit,
                        cancel,
                        nativeThreadId,
                        interaction,
                        inputDesktop,
                        threadDesktop,
                        inputDesktopName,
                        threadDesktopName,
                        readinessTimer.Elapsed);
                    lastReadiness = readiness;
                    try
                    {
                        NativeEntryReadinessOracle.Validate(readiness);
                        break;
                    }
                    catch (InvalidDataException)
                    {
                        readiness = null;
                        Thread.Sleep(25);
                    }
                }
                if (readiness is null)
                {
                    state.RecordPreReadinessTerminalMessages(
                        commandRouter.DrainPreReadinessRejectedCount(), requireActive: false);
                    state.RecordFailedReadiness(lastReadiness
                        ?? throw new InvalidDataException("Native entry readiness could not be measured."));
                    throw new InvalidDataException(
                        "Native credential entry could not establish visible interactive readiness before its finite deadline.");
                }
                state.RecordReadiness(readiness);
                state.Activate(GetWindowTextLengthW(edit));
                state.RecordClipboardMessageBlocked();
                state.RecordPreReadinessTerminalMessages(
                    commandRouter.DrainPreReadinessRejectedCount(), requireActive: true);
                commandRouter.Enable();
                Stopwatch timer = Stopwatch.StartNew();
                while (timer.Elapsed < deadline)
                {
                    NativeEntryInteraction? requestedAction = null;
                    NativeEntryActionSource? actionSource = null;
                    if (commandRouter.TryTake(out NativeEntryInteraction routedAction))
                    {
                        RecordFirstTerminalAction(
                            ref requestedAction,
                            ref actionSource,
                            routedAction,
                            routedAction == NativeEntryInteraction.Submit
                                ? NativeEntryActionSource.SubmitButton
                                : NativeEntryActionSource.CancelButton);
                    }
                    while (PeekMessageW(out NativeMessage message, 0, 0, 0, PmRemove))
                    {
                        if (commandRouter.TryTake(out routedAction))
                        {
                            RecordFirstTerminalAction(
                                ref requestedAction,
                                ref actionSource,
                                routedAction,
                                routedAction == NativeEntryInteraction.Submit
                                    ? NativeEntryActionSource.SubmitButton
                                    : NativeEntryActionSource.CancelButton);
                        }
                        if (ShouldStopMessageDrain(requestedAction))
                        {
                            break;
                        }
                        if (IsOwnedButtonCommand(
                            message.Message, message.Window, message.WParam, message.LParam,
                            window, submit, cancel, out _))
                        {
                            _ = commandRouter.Route(
                                message.Message, message.Window, message.WParam, message.LParam,
                                window, submit, cancel);
                            continue;
                        }
                        else if (message.Message == WmKeyDown && message.Window == edit
                            && GetForegroundWindow() == window && GetFocus() == edit)
                        {
                            if (requestedAction is null && message.WParam is VkReturn or VkEscape)
                            {
                                RecordFirstTerminalAction(
                                    ref requestedAction, ref actionSource,
                                    message.WParam == VkReturn
                                        ? NativeEntryInteraction.Submit
                                        : NativeEntryInteraction.Cancel,
                                    NativeEntryActionSource.EditKey);
                            }
                        }
                        if (ShouldStopMessageDrain(requestedAction))
                        {
                            break;
                        }
                        _ = TranslateMessage(in message);
                        _ = DispatchMessageW(in message);
                        if (commandRouter.TryTake(out routedAction))
                        {
                            RecordFirstTerminalAction(
                                ref requestedAction,
                                ref actionSource,
                                routedAction,
                                routedAction == NativeEntryInteraction.Submit
                                    ? NativeEntryActionSource.SubmitButton
                                    : NativeEntryActionSource.CancelButton);
                        }
                    }
                    if (!IsWindow(window))
                    {
                        terminal = NativeEntryTerminalState.Cancelled;
                        state.Cancel();
                        break;
                    }
                    if (requestedAction == NativeEntryInteraction.Cancel)
                    {
                        NativeEntryReadinessEvidence actionReadiness = MeasureReadiness(
                            window, instruction, edit, submit, cancel, nativeThreadId, interaction,
                            inputDesktop, threadDesktop, inputDesktopName, threadDesktopName,
                            TimeSpan.Zero);
                        state.RecordActionReadiness(actionReadiness, NativeEntryInteraction.Cancel, actionSource
                            ?? throw new InvalidDataException("Native entry action source is absent."));
                        terminal = NativeEntryTerminalState.Cancelled;
                        state.Cancel();
                        break;
                    }
                    if (requestedAction == NativeEntryInteraction.Submit)
                    {
                        NativeEntryReadinessEvidence actionReadiness = MeasureReadiness(
                            window, instruction, edit, submit, cancel, nativeThreadId, interaction,
                            inputDesktop, threadDesktop, inputDesktopName, threadDesktopName,
                            TimeSpan.Zero);
                        state.RecordActionReadiness(actionReadiness, NativeEntryInteraction.Submit, actionSource
                            ?? throw new InvalidDataException("Native entry action source is absent."));
                        int length = GetWindowTextW(edit, captured, captured.Length);
                        if (captured.AsSpan(0, length).ContainsAnyExceptInRange((char)0x20, (char)0x7e))
                        {
                            throw new InvalidDataException("Native credential entry must use printable ASCII bytes.");
                        }
                        result = new byte[length];
                        int encoded = Encoding.ASCII.GetBytes(captured.AsSpan(0, length), result);
                        if (encoded != length || result.Length == 0)
                        {
                            throw new InvalidDataException("Native credential entry must be non-empty ASCII bytes.");
                        }
                        terminal = NativeEntryTerminalState.Submitted;
                        state.Submit(result);
                        break;
                    }
                    Thread.Sleep(10);
                }
                if (terminal == NativeEntryTerminalState.Failed)
                {
                    terminal = NativeEntryTerminalState.TimedOut;
                    state.Timeout();
                }
            }
            catch (Exception exception)
            {
                failure = exception;
                terminal = NativeEntryTerminalState.Failed;
                state.RecordSetupFailureIfNeeded(NativeEntryReadinessEvidence.SetupFailure(
                    nativeThreadId,
                    interaction,
                    window != 0,
                    instruction != 0,
                    edit != 0,
                    submit != 0,
                    cancel != 0));
                state.FailFromAnyState();
            }
            finally
            {
                Array.Clear(captured);
                bool textCleared = edit == 0 || SetWindowTextW(edit, string.Empty)
                    && GetWindowTextLengthW(edit) == 0;
                if (edit != 0 && originalEditProcedure != 0)
                {
                    _ = SetWindowLongPtrW(edit, GwlWndProc, originalEditProcedure);
                }
                bool instructionDestroyed = instruction == 0 || DestroyWindow(instruction) || !IsWindow(instruction);
                bool submitDestroyed = submit == 0 || DestroyWindow(submit) || !IsWindow(submit);
                bool cancelDestroyed = cancel == 0 || DestroyWindow(cancel) || !IsWindow(cancel);
                bool editDestroyed = edit == 0 || DestroyWindow(edit) || !IsWindow(edit);
                bool windowDestroyed = window == 0 || DestroyWindow(window) || !IsWindow(window);
                if (inputDesktop != 0) { _ = CloseDesktop(inputDesktop); }
                if (windowClassAtom != 0 && windowClass is not null)
                {
                    _ = UnregisterClassW(windowClass, GetModuleHandleW(null));
                }
                Interlocked.Exchange(ref activeWindow, 0);
                state.CompleteUiThreadCleanup(
                    instructionDestroyed && submitDestroyed && cancelDestroyed && editDestroyed && windowDestroyed,
                    buffersWereCleared: textCleared);
                GC.KeepAlive(editProcedure);
                GC.KeepAlive(windowProcedure);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(deadline + TimeSpan.FromSeconds(5)))
        {
            nint window = Interlocked.CompareExchange(ref activeWindow, 0, 0);
            _ = window != 0
                ? PostMessageW(window, WmClose, 0, 0)
                : PostThreadMessageW(nativeThreadId, WmClose, 0, 0);
            if (!thread.Join(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Native masked entry UI thread did not terminate after bounded close.");
            }
        }
        state.RecordThreadJoined();
        return new(result, terminal, state.Evidence);
    }

    internal static void RequireMatchingWindowClassInstance(
        nint registeredInstance,
        nint createInstance)
    {
        if (registeredInstance == 0 || createInstance != registeredInstance)
        {
            throw new InvalidDataException(
                "Native entry window creation must use the registered private-class module instance.");
        }
    }

    internal static bool IsOwnedButtonCommand(
        uint message,
        nint messageWindow,
        nuint wParam,
        nint lParam,
        nint ownerWindow,
        nint submitButton,
        nint cancelButton,
        out NativeEntryInteraction interaction)
    {
        interaction = default;
        if (message != WmCommand || messageWindow != ownerWindow
            || unchecked((int)((wParam >> 16) & 0xffff)) != 0)
        {
            return false;
        }
        int controlId = unchecked((int)(wParam & 0xffff));
        if (controlId == SubmitButtonId && lParam == submitButton)
        {
            interaction = NativeEntryInteraction.Submit;
            return true;
        }
        if (controlId == CancelButtonId && lParam == cancelButton)
        {
            interaction = NativeEntryInteraction.Cancel;
            return true;
        }
        return false;
    }

    internal static void RecordFirstTerminalAction(
        ref NativeEntryInteraction? selectedAction,
        ref NativeEntryActionSource? selectedSource,
        NativeEntryInteraction action,
        NativeEntryActionSource source)
    {
        if (selectedAction is null)
        {
            selectedAction = action;
            selectedSource = source;
        }
    }

    internal static bool ShouldStopMessageDrain(NativeEntryInteraction? selectedAction) =>
        selectedAction is not null;

    internal static bool IsPreReadinessTerminalMessage(
        uint message,
        nint messageWindow,
        nuint wParam,
        nint lParam,
        nint ownerWindow,
        nint edit,
        nint submitButton,
        nint cancelButton) =>
        IsOwnedButtonCommand(
            message, messageWindow, wParam, lParam,
            ownerWindow, submitButton, cancelButton, out _)
        || message == WmKeyDown && messageWindow == edit && wParam is VkReturn or VkEscape;

    internal static bool IsPreReadinessEditContentMessage(uint message, nint messageWindow, nint edit) =>
        messageWindow == edit && message is WmKeyDown or 0x0101 or 0x0102 or 0x0103;

    internal static PreReadinessMessageDisposition ClassifyPreReadinessMessage(
        uint message,
        nint messageWindow,
        nuint wParam,
        nint lParam,
        nint ownerWindow,
        nint instruction,
        nint edit,
        nint submitButton,
        nint cancelButton)
    {
        if (IsPreReadinessTerminalMessage(
            message, messageWindow, wParam, lParam,
            ownerWindow, edit, submitButton, cancelButton))
        {
            return PreReadinessMessageDisposition.RejectTerminal;
        }
        if (messageWindow != ownerWindow
            && messageWindow != instruction
            && messageWindow != edit
            && messageWindow != submitButton
            && messageWindow != cancelButton)
        {
            return PreReadinessMessageDisposition.IgnoreUnowned;
        }
        return IsPreReadinessEditContentMessage(message, messageWindow, edit)
            ? PreReadinessMessageDisposition.RejectEditContent
            : PreReadinessMessageDisposition.DispatchOne;
    }

    private static NativeEntryReadinessEvidence MeasureReadiness(
        nint window,
        nint instruction,
        nint edit,
        nint submit,
        nint cancel,
        uint ownerThreadId,
        NativeEntryInteraction interaction,
        nint inputDesktop,
        nint threadDesktop,
        string inputDesktopName,
        string threadDesktopName,
        TimeSpan elapsed)
    {
        uint measuredThreadId = GetWindowThreadProcessId(window, out uint ownerProcessId);
        int cloaked = 1;
        int cloakedSize = sizeof(int);
        int dwmResult = DwmGetWindowAttribute(window, DwmwaCloaked, out cloaked, cloakedSize);
        string desktopFingerprint = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(Process.GetCurrentProcess().SessionId + ":" + threadDesktopName)));
        return new(
            checked((int)ownerProcessId),
            measuredThreadId,
            Process.GetCurrentProcess().SessionId,
            desktopFingerprint,
            string.Equals(inputDesktopName, threadDesktopName, StringComparison.Ordinal),
            CompareObjectHandles(inputDesktop, threadDesktop),
            ownerProcessId == Environment.ProcessId,
            measuredThreadId == ownerThreadId,
            GetParent(window) == 0,
            IsWindowVisible(window),
            IsWindowEnabled(window),
            dwmResult == 0 && cloaked == 0,
            MonitorFromWindow(window, 0) != 0 && GetWindowRect(window, out NativeRect rect)
                && rect.Right > rect.Left && rect.Bottom > rect.Top && !IsIconic(window),
            GetParent(instruction) == window,
            IsWindowVisible(instruction),
            interaction.ToString().ToLowerInvariant(),
            Convert.ToHexStringLower(SHA256.HashData(
                Encoding.UTF8.GetBytes(GetWindowText(instruction)))),
            GetParent(edit) == window,
            IsWindowVisible(edit),
            IsWindowEnabled(edit),
            (GetWindowLongPtrW(edit, GwlStyle).ToInt64() & EsPassword) != 0,
            GetParent(submit) == window,
            IsWindowVisible(submit),
            IsWindowEnabled(submit),
            GetFocus() == submit,
            GetParent(cancel) == window,
            IsWindowVisible(cancel),
            IsWindowEnabled(cancel),
            GetFocus() == cancel,
            GetForegroundWindow() == window,
            GetFocus() == edit,
            checked((long)ReadinessDeadline.TotalMilliseconds),
            checked((long)elapsed.TotalMilliseconds));
    }

    private static string GetWindowText(nint window)
    {
        int length = GetWindowTextLengthW(window);
        char[] value = new char[checked(length + 1)];
        int copied = GetWindowTextW(window, value, value.Length);
        return new string(value, 0, copied);
    }

    private static string DesktopName(nint desktop)
    {
        _ = GetUserObjectInformationW(desktop, UoiName, 0, 0, out uint required);
        if (required < 2 || required > 1024)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Interactive desktop identity is unavailable.");
        }
        nint value = Marshal.AllocHGlobal(checked((int)required));
        try
        {
            if (!GetUserObjectInformationW(desktop, UoiName, value, required, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Interactive desktop identity could not be measured.");
            }
            return Marshal.PtrToStringUni(value)
                ?? throw new InvalidDataException("Interactive desktop identity is empty.");
        }
        finally
        {
            Marshal.FreeHGlobal(value);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(
        uint exStyle, string className, string? windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WindowClassEx windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClassW(string className, nint instance);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextW(nint window, [Out] char[] text, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLengthW(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowTextW(nint window, string text);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtrW(nint window, int index, nint value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CallWindowProcW(
        nint previous, nint window, uint message, nuint wParam, nint lParam);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetWindowLongPtrW(nint window, int index);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint window);
    [DllImport("user32.dll")]
    private static extern nint GetFocus();
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateWindow(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(nint window);
    [DllImport("user32.dll")]
    private static extern nint GetParent(nint window);
    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("user32.dll")]
    private static extern nint GetThreadDesktop(uint threadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint OpenInputDesktop(uint flags, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint desiredAccess);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformationW(
        nint objectHandle, int index, nint information, uint length, out uint needed);
    [DllImport("kernelbase.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CompareObjectHandles(nint first, nint second);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessageW(out NativeMessage message, nint window, uint minimum, uint maximum, uint remove);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(in NativeMessage message);
    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(in NativeMessage message);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessageW(uint threadId, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint window, uint message, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        internal nint Window;
        internal uint Message;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal int X;
        internal int Y;
        internal uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClassEx
    {
        internal uint Size;
        internal uint Style;
        internal nint WindowProcedure;
        internal int ClassExtra;
        internal int WindowExtra;
        internal nint Instance;
        internal nint Icon;
        internal nint Cursor;
        internal nint Background;
        internal string? MenuName;
        internal string ClassName;
        internal nint SmallIcon;
    }

    private delegate nint NativeWindowProcedure(
        nint window, uint message, nuint wParam, nint lParam);
}

internal enum NativeEntryTerminalState { Submitted, Cancelled, TimedOut, Failed }
internal enum NativeEntryInteraction { Submit, Cancel }
internal enum NativeEntryActionSource { EditKey, SubmitButton, CancelButton }
internal enum PreReadinessMessageDisposition { DispatchOne, RejectTerminal, RejectEditContent, IgnoreUnowned }

internal sealed class NativeEntryWindowCommandRouter
{
    private readonly object gate = new();
    private bool enabled;
    private NativeEntryInteraction? selected;
    internal int PreReadinessRejectedCount { get; private set; }

    internal int DrainPreReadinessRejectedCount()
    {
        lock (gate)
        {
            int result = PreReadinessRejectedCount;
            PreReadinessRejectedCount = 0;
            return result;
        }
    }

    internal void Enable()
    {
        lock (gate)
        {
            enabled = true;
            selected = null;
        }
    }

    internal bool Route(
        uint message,
        nint messageWindow,
        nuint wParam,
        nint lParam,
        nint ownerWindow,
        nint submitButton,
        nint cancelButton)
    {
        if (!NativeMaskedEntryDialog.IsOwnedButtonCommand(
            message, messageWindow, wParam, lParam,
            ownerWindow, submitButton, cancelButton, out NativeEntryInteraction interaction))
        {
            return false;
        }
        lock (gate)
        {
            if (!enabled)
            {
                PreReadinessRejectedCount = checked(PreReadinessRejectedCount + 1);
                return true;
            }
            selected ??= interaction;
            return true;
        }
    }

    internal bool TryTake(out NativeEntryInteraction interaction)
    {
        lock (gate)
        {
            if (selected is not NativeEntryInteraction value)
            {
                interaction = default;
                return false;
            }
            interaction = value;
            selected = null;
            return true;
        }
    }
}

internal sealed class NativeEntryCapture(
    byte[] secret,
    NativeEntryTerminalState terminalState,
    NativeEntryCleanupEvidence evidence) : IDisposable
{
    private byte[] secret = secret;
    public byte[] Secret => secret;
    public NativeEntryTerminalState TerminalState { get; } = terminalState;
    public NativeEntryCleanupEvidence Evidence { get; } = evidence;

    public byte[] DetachSecret()
    {
        byte[] value = secret;
        secret = [];
        return value;
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(secret);
        secret = [];
    }
}

internal sealed class NativeEntryStateMachine
{
    private bool active;
    private bool terminal;
    private bool windowDestroyed;
    private bool buffersCleared;
    private bool threadJoined;
    private bool clipboardMessagesBlocked;
    private NativeEntryReadinessEvidence? readiness;
    private NativeEntryReadinessEvidence? actionReadiness;
    private NativeEntryActionSource? actionSource;
    private NativeEntryInteraction? action;
    private int preReadinessTerminalMessages;
    private int preReadinessIgnoredMessages;
    public bool InitialBlank { get; private set; }

    public NativeEntryCleanupEvidence Evidence => new(
        InitialBlank, terminal, windowDestroyed, buffersCleared, threadJoined,
        clipboardMessagesBlocked, readiness, actionReadiness,
        actionSource?.ToString().ToLowerInvariant(), action?.ToString().ToLowerInvariant(),
        preReadinessTerminalMessages, preReadinessIgnoredMessages);

    public void RecordPreReadinessTerminalMessage()
    {
        if (active || terminal)
        {
            throw new InvalidOperationException("Pre-readiness terminal input can only be retained before activation.");
        }
        preReadinessTerminalMessages = checked(preReadinessTerminalMessages + 1);
    }

    public void RecordPreReadinessTerminalMessages(int count, bool requireActive)
    {
        if (terminal || count < 0 || requireActive != active)
        {
            throw new InvalidOperationException("Routed pre-readiness terminal input must be retained at activation.");
        }
        preReadinessTerminalMessages = checked(preReadinessTerminalMessages + count);
    }

    public void RecordPreReadinessIgnoredMessage()
    {
        if (active || terminal)
        {
            throw new InvalidOperationException("Pre-readiness ignored input can only be retained before activation.");
        }
        preReadinessIgnoredMessages = checked(preReadinessIgnoredMessages + 1);
    }

    public void RecordReadiness(NativeEntryReadinessEvidence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (active || terminal || readiness is not null)
        {
            throw new InvalidOperationException("Native entry readiness must be recorded exactly once before activation.");
        }
        NativeEntryReadinessOracle.Validate(value);
        readiness = value;
    }

    public void RecordFailedReadiness(NativeEntryReadinessEvidence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (active || terminal || readiness is not null)
        {
            throw new InvalidOperationException("Failed readiness must be retained once before entry activation.");
        }
        readiness = value;
    }

    public void RecordActionReadiness(
        NativeEntryReadinessEvidence value,
        NativeEntryInteraction interaction,
        NativeEntryActionSource source)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!active || terminal || readiness is null || actionReadiness is not null)
        {
            throw new InvalidOperationException("Native entry action readiness must be recorded exactly once while active.");
        }
        NativeEntryReadinessOracle.Validate(
            value,
            requireEditFocus: source == NativeEntryActionSource.EditKey);
        if (source == NativeEntryActionSource.SubmitButton && value.SubmitFocused != true
            || source == NativeEntryActionSource.CancelButton && value.CancelFocused != true)
        {
            throw new InvalidDataException("Native entry button action lacks exact owned-control focus.");
        }
        actionReadiness = value;
        action = interaction;
        actionSource = source;
    }

    public void RecordSetupFailureIfNeeded(NativeEntryReadinessEvidence value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!active && !terminal && readiness is null)
        {
            readiness = value;
        }
    }

    public void Activate(int initialCharacterCount)
    {
        if (active || terminal || readiness is null || initialCharacterCount != 0)
        {
            throw new InvalidOperationException("Native entry must activate exactly once with a blank control.");
        }
        InitialBlank = true;
        active = true;
    }

    public void Submit(ReadOnlySpan<byte> secret)
    {
        if (!active || terminal || secret.IsEmpty) { throw new InvalidOperationException("Invalid native entry submission state."); }
        terminal = true;
    }

    public void Cancel() => SetTerminal();
    public void Timeout() => SetTerminal();
    public void Fail() => SetTerminal();

    public void FailFromAnyState()
    {
        if (terminal) { return; }
        active = true;
        terminal = true;
    }

    public void RecordClipboardMessageBlocked()
    {
        if (!active || terminal) { throw new InvalidOperationException("Clipboard blocking must occur during active entry."); }
        clipboardMessagesBlocked = true;
    }

    private void SetTerminal()
    {
        if (!active || terminal) { throw new InvalidOperationException("Invalid native entry terminal transition."); }
        terminal = true;
    }

    public void CompleteUiThreadCleanup(bool windowWasDestroyed, bool buffersWereCleared)
    {
        if (!terminal) { throw new InvalidOperationException("UI cleanup cannot precede a terminal transition."); }
        windowDestroyed = windowWasDestroyed;
        buffersCleared = buffersWereCleared;
    }

    public void RecordThreadJoined()
    {
        if (!windowDestroyed || !buffersCleared) { throw new InvalidOperationException("UI thread joined before terminal cleanup completed."); }
        threadJoined = true;
    }
}

internal sealed class NativeManualSecretSource(TimeSpan? entryDeadline = null) : IHelperSecretSource
{
    private readonly TimeSpan deadline = entryDeadline ?? TimeSpan.FromMinutes(5);

    public byte[] Capture(Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentV2 assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        using NativeEntryCapture capture = NativeMaskedEntryDialog.Capture(deadline);
        return capture.TerminalState switch
        {
            NativeEntryTerminalState.Submitted when capture.Secret.Length > 0 => capture.DetachSecret(),
            NativeEntryTerminalState.Cancelled => throw new OperationCanceledException("Native credential entry was cancelled."),
            NativeEntryTerminalState.TimedOut => throw new TimeoutException("Native credential entry reached its finite deadline."),
            _ => throw new InvalidOperationException("Native credential entry did not reach a valid terminal state."),
        };
    }
}

internal sealed class NativeQualificationSecretSource : IHelperSecretSource, IDisposable
{
    private readonly TimeSpan? entryDeadline;
    private readonly Func<TimeSpan, NativeEntryInteraction, NativeEntryCapture> capture;
    private byte[] privateOracle = [];
    internal NativeEntryCleanupEvidence? EntryEvidence { get; private set; }
    internal CredentialNativeQualificationPhaseV2? LastPhase { get; private set; }

    internal NativeQualificationSecretSource(
        TimeSpan? entryDeadline = null,
        Func<TimeSpan, NativeEntryInteraction, NativeEntryCapture>? capture = null)
    {
        this.entryDeadline = entryDeadline;
        this.capture = capture ?? NativeMaskedEntryDialog.Capture;
    }

    public byte[] Capture(Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentV2 assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        CredentialNativeQualificationPhaseV2 phase = CredentialNativeQualificationPhasesV2.Parse(
            assignment.AssignmentId,
            assignment.AssignmentKind);
        LastPhase = phase;
        if (phase.SecretMode == CredentialNativeQualificationSecretModeV2.Manual)
        {
            NativeEntryCapture entered;
            try
            {
                entered = capture(
                    entryDeadline ?? TimeSpan.FromMinutes(5),
                    phase.ManualEntryMustCancel ? NativeEntryInteraction.Cancel : NativeEntryInteraction.Submit);
            }
            catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
            {
                throw new InvalidDataException("Native qualification entry failed closed before secret use.", exception);
            }
            using (entered)
            {
                EntryEvidence = entered.Evidence;
                if (entered.TerminalState == NativeEntryTerminalState.Cancelled)
                {
                    throw new OperationCanceledException("Native credential entry was cancelled.");
                }
                if (phase.ManualEntryMustCancel || entered.TerminalState != NativeEntryTerminalState.Submitted
                    || entered.Secret.Length == 0)
                {
                    throw new InvalidDataException("Native qualification entry did not match its required terminal action.");
                }
                byte[] value = entered.DetachSecret();
                ReplaceOracle(value);
                return value;
            }
        }
        int length = phase.SecretMode switch
        {
            CredentialNativeQualificationSecretModeV2.GeneratedMaximum =>
                WindowsCredentialManagerStore.MaximumBlobBytes,
            CredentialNativeQualificationSecretModeV2.GeneratedOversize =>
                WindowsCredentialManagerStore.MaximumBlobBytes + 1,
            CredentialNativeQualificationSecretModeV2.Generated48 => 48,
            _ => throw new InvalidDataException("This native qualification phase cannot capture a secret."),
        };
        byte[] generated = RandomNumberGenerator.GetBytes(length);
        ReplaceOracle(generated);
        return generated;
    }

    internal NativeCanaryEvidence ScanAndClear(
        IReadOnlyList<NativeCanarySurface> surfaces,
        IReadOnlyList<NativeRawTargetCanary> rawTargets)
    {
        try { return NativeCanaryScanner.Scan(privateOracle, rawTargets, surfaces); }
        finally
        {
            CryptographicOperations.ZeroMemory(privateOracle);
            privateOracle = [];
        }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(privateOracle);
        privateOracle = [];
    }

    private void ReplaceOracle(ReadOnlySpan<byte> value)
    {
        CryptographicOperations.ZeroMemory(privateOracle);
        privateOracle = value.ToArray();
    }
}

internal static class NativeNetworkMeasurement
{
    internal static (int Listeners, int Operations) MeasureCurrentProcessTcp()
    {
        int processId = Environment.ProcessId;
        (int v4Listeners, int v4Operations) = Measure(2, 24, 20, 0, processId);
        (int v6Listeners, int v6Operations) = Measure(23, 56, 52, 48, processId);
        return (v4Listeners + v6Listeners, v4Operations + v6Operations);
    }

    private static (int Listeners, int Operations) Measure(
        int addressFamily, int rowSize, int processOffset, int stateOffset, int processId)
    {
        int size = 0;
        _ = GetExtendedTcpTable(0, ref size, true, addressFamily, 5, 0);
        if (size <= 4) { return (0, 0); }
        nint buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, true, addressFamily, 5, 0) != 0)
            {
                throw new InvalidOperationException("The native qualification TCP ownership table could not be measured.");
            }
            int rows = Marshal.ReadInt32(buffer);
            int listeners = 0;
            int operations = 0;
            for (int index = 0; index < rows; index++)
            {
                int row = checked(4 + index * rowSize);
                if (Marshal.ReadInt32(buffer, row + processOffset) != processId) { continue; }
                int state = Marshal.ReadInt32(buffer, row + stateOffset);
                if (state == 2) { listeners++; } else { operations++; }
            }
            return (listeners, operations);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(
        nint tcpTable, ref int size, [MarshalAs(UnmanagedType.Bool)] bool order,
        int addressFamily, int tableClass, uint reserved);
}

internal sealed record NativeCallCounts(
    int CredWriteW,
    int CredReadW,
    int CredDeleteW,
    int CredFree,
    int Total);

internal sealed record NativeCallTraceEntry(
    long Sequence,
    string Operation,
    string TargetFingerprintSha256,
    string Scenario,
    string Result,
    long? AllocationId,
    long? PairedAllocationId);

internal sealed record NativeScenarioEvidence(
    string Id,
    IReadOnlyList<string> TargetFingerprints,
    string Result,
    bool NoFallback,
    bool CleanupRequired);

internal sealed record TargetAbsenceEvidence(string Alias, string TargetFingerprintSha256, string Result);
internal sealed record NativeEntryEvidence(string Owner, bool Masked, bool NonEchoing, string SecretRetention, string CancelResult);
internal sealed record NativeBackupEvidence(
    string RestoredState, string Reactivation, bool SecretAbsent, bool TargetAbsent, string BackupSha256);
internal sealed record NativeCanarySurfaceEvidence(
    string Name,
    string Kind,
    long ByteCount,
    int SecretMatches,
    int RawTargetMatches);
internal sealed record NativeCanaryEvidence(
    int SecretMatches,
    int RawTargetMatches,
    IReadOnlyList<string> RawTargetEncodings,
    IReadOnlyList<NativeCanarySurfaceEvidence> ScannedSurfaces);
internal sealed record NativeDeadlineEvidence(
    long LimitMilliseconds,
    long ElapsedMilliseconds,
    int Checks,
    bool CompletedWithinLimit);
internal sealed record NativeEntryCleanupEvidence(
    bool InitialBlank,
    bool Terminal,
    bool WindowDestroyed,
    bool BuffersCleared,
    bool ThreadJoined,
    bool ClipboardMessagesBlocked,
    NativeEntryReadinessEvidence? Readiness = null,
    NativeEntryReadinessEvidence? ActionReadiness = null,
    string? ActionSource = null,
    string? Action = null,
    int PreReadinessTerminalMessages = 0,
    int PreReadinessIgnoredMessages = 0);

internal sealed record NativeEntryReadinessEvidence(
    int OwnerProcessId,
    uint OwnerThreadId,
    int OwnerSessionId,
    string DesktopNameSha256,
    bool InteractiveInputDesktop,
    bool DesktopObjectMatches,
    bool OwnerProcessMatches,
    bool OwnerThreadMatches,
    bool TopLevelWindow,
    bool WindowVisible,
    bool WindowEnabled,
    bool WindowNotCloaked,
    bool WindowIntersectsActiveMonitor,
    bool InstructionOwned,
    bool InstructionVisible,
    string InstructionMode,
    string InstructionFingerprintSha256,
    bool EditOwned,
    bool EditVisible,
    bool EditEnabled,
    bool EditMasked,
    bool SubmitOwned,
    bool SubmitVisible,
    bool SubmitEnabled,
    bool SubmitFocused,
    bool CancelOwned,
    bool CancelVisible,
    bool CancelEnabled,
    bool CancelFocused,
    bool Foreground,
    bool EditFocused,
    long ReadinessDeadlineMilliseconds,
    long ReadinessElapsedMilliseconds)
{
    internal static NativeEntryReadinessEvidence SetupFailure(
        uint threadId,
        NativeEntryInteraction interaction,
        bool windowCreated,
        bool instructionCreated,
        bool editCreated,
        bool submitCreated,
        bool cancelCreated) => new(
            Environment.ProcessId,
            threadId,
            Process.GetCurrentProcess().SessionId,
            new string('0', 64),
            false,
            false,
            true,
            threadId != 0,
            windowCreated,
            false,
            false,
            false,
            false,
            instructionCreated,
            false,
            interaction.ToString().ToLowerInvariant(),
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                interaction == NativeEntryInteraction.Submit
                    ? NativeMaskedEntryDialog.SubmitInstruction
                    : NativeMaskedEntryDialog.CancelInstruction))),
            editCreated,
            false,
            false,
            false,
            submitCreated,
            false,
            false,
            false,
            cancelCreated,
            false,
            false,
            false,
            false,
            false,
            checked((long)NativeMaskedEntryDialog.ReadinessDeadline.TotalMilliseconds),
            0);
}

internal static class NativeEntryReadinessOracle
{
    internal static void Validate(
        NativeEntryReadinessEvidence evidence,
        bool requireEditFocus = true)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.OwnerProcessId != Environment.ProcessId
            || evidence.OwnerThreadId == 0
            || evidence.OwnerSessionId != Process.GetCurrentProcess().SessionId
            || evidence.DesktopNameSha256.Length != 64
            || !evidence.DesktopNameSha256.All(char.IsAsciiHexDigit)
            || evidence.ReadinessDeadlineMilliseconds is < 1 or > 10_000
            || evidence.ReadinessElapsedMilliseconds < 0
            || evidence.ReadinessElapsedMilliseconds > evidence.ReadinessDeadlineMilliseconds
            || !evidence.InteractiveInputDesktop
            || !evidence.DesktopObjectMatches
            || !evidence.OwnerProcessMatches
            || !evidence.OwnerThreadMatches
            || !evidence.TopLevelWindow
            || !evidence.WindowVisible
            || !evidence.WindowEnabled
            || !evidence.WindowNotCloaked
            || !evidence.WindowIntersectsActiveMonitor
            || !evidence.InstructionOwned
            || !evidence.InstructionVisible
            || evidence.InstructionMode is not ("submit" or "cancel")
            || !string.Equals(
                evidence.InstructionFingerprintSha256,
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                    evidence.InstructionMode == "submit"
                        ? NativeMaskedEntryDialog.SubmitInstruction
                        : NativeMaskedEntryDialog.CancelInstruction))),
                StringComparison.Ordinal)
            || !evidence.EditOwned
            || !evidence.EditVisible
            || !evidence.EditEnabled
            || !evidence.EditMasked
            || !evidence.SubmitOwned
            || !evidence.SubmitVisible
            || !evidence.SubmitEnabled
            || !evidence.CancelOwned
            || !evidence.CancelVisible
            || !evidence.CancelEnabled
            || !evidence.Foreground
            || requireEditFocus && !evidence.EditFocused)
        {
            throw new InvalidDataException(
                "Native credential entry is not proven visible and actionable on the interactive input desktop.");
        }
    }
}

internal sealed record NativeQualificationEvidence(
    string Schema,
    string Status,
    string ManifestId,
    string ManifestSha256,
    int ProcessId,
    NativeCallCounts NativeCalls,
    IReadOnlyList<NativeCallTraceEntry> NativeCallTrace,
    IReadOnlyList<NativeScenarioEvidence> Scenarios,
    IReadOnlyList<TargetAbsenceEvidence> TargetAbsence,
    NativeEntryEvidence Entry,
    NativeBackupEvidence BackupRestore,
    NativeCanaryEvidence Canaries,
    NativeDeadlineEvidence Deadline,
    int ListenerCount,
    int NetworkOperations,
    int DnsOperations,
    int ProviderOperations,
    int BillableOperations,
    bool RetryAttempted,
    bool FakeProviderOnly,
    long DurationMilliseconds,
    string CompletedAtUtc);
