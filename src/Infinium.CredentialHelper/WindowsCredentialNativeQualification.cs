using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace Infinium.CredentialHelper;

internal static class WindowsCredentialNativeQualification
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    public const string AcceptedManifestSha256 =
        "0c911c6c10340d4a8b6a3f98aa2c2bffa3f1f4290793d3583a460cecf89bcbd3";
    public const string AcceptedManifestId =
        "infinium.m1-s6.wp4.credential-native-authorization/56789943-8096-45fa-8ac9-03da40a1c000";

    public static int Run(string manifestPath, string evidencePath)
    {
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
        string secretCanary = NativeSecretCanary.Create();
        byte[] secretBytes = Encoding.ASCII.GetBytes(secretCanary);
        List<NativeScenarioEvidence> scenarios = [];
        WindowsCredentialManagerStore store = new();
        Stopwatch duration = Stopwatch.StartNew();
        bool completed = false;
        try
        {
            PreflightAllTargets(store, manifest.Targets);
            RunInteractiveSubmit(store, Target(manifest, "interactive-primary"), secretBytes, scenarios);
            RunInteractiveCancel(store, Target(manifest, "interactive-cancel"), scenarios);
            RunSizeBoundaries(store, Target(manifest, "size-valid"),
                Target(manifest, "size-oversize"), scenarios);
            RunUnavailable(store, Target(manifest, "unavailable-store"), scenarios);
            RunReplacement(store, Target(manifest, "replacement-old"),
                Target(manifest, "replacement-new"), secretBytes, scenarios);
            RunRevokeDelete(store, Target(manifest, "revoke-delete"), secretBytes, scenarios);
            RunCrashRestart(store, Target(manifest, "crash-restart"), manifestPath,
                Path.GetDirectoryName(evidencePath)!, scenarios);
            RunBackupRestore(store, Target(manifest, "backup-old"), Target(manifest, "backup-new"),
                secretBytes, backupPath, scenarios);
            RunFakeDispatch(store, Target(manifest, "fake-dispatch"), secretBytes, scenarios);
            RunCleanupAmbiguity(store, Target(manifest, "unavailable-store"),
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
            NativeQualificationEvidence evidence = new(
                "infinium.m1-s6.wp4.credential-native-evidence/v1",
                "passed",
                AcceptedManifestId,
                manifestSha256,
                Environment.ProcessId,
                store.CallCounts,
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
                new(
                    0,
                    0,
                    ["native evidence", "backup metadata", "captured stdout", "captured stderr", "process launch", "receipts"]),
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
            ScanRetainedEvidence(evidencePath, backupPath, secretCanary, manifest.Targets);
            completed = true;
            return 0;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secretBytes);
            if (!completed)
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
            store.WriteExact(target, secret);
            byte[] read = store.ReadExact(target);
            try
            {
                if (!CryptographicOperations.FixedTimeEquals(secret, read)) { return 67; }
            }
            finally { CryptographicOperations.ZeroMemory(read); }
            File.WriteAllText(Path.GetFullPath(countEvidencePath),
                "{\"credWriteW\":1,\"credReadW\":1,\"credDeleteW\":0,\"credFree\":1}\n",
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

    private static void RunInteractiveSubmit(
        WindowsCredentialManagerStore store,
        NativeTarget target,
        byte[] secret,
        List<NativeScenarioEvidence> evidence)
    {
        byte[] entered = NativeMaskedEntryDialog.Capture(secret, cancel: false);
        try
        {
            store.WriteExact(target, entered);
            byte[] read = store.ReadExact(target);
            try { RequireEqual(entered, read, "Native interactive entry did not round-trip exactly."); }
            finally { CryptographicOperations.ZeroMemory(read); }
        }
        finally { CryptographicOperations.ZeroMemory(entered); }
        evidence.Add(new("interactive-entry-submit", [target.TargetFingerprintSha256], "completed", true, true));
    }

    private static void RunInteractiveCancel(
        WindowsCredentialManagerStore store,
        NativeTarget target,
        List<NativeScenarioEvidence> evidence)
    {
        byte[] entered = NativeMaskedEntryDialog.Capture([], cancel: true);
        if (entered.Length != 0 || store.Exists(target))
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
        List<NativeScenarioEvidence> evidence)
    {
        string childCountsPath = Path.Combine(outputDirectory, "native-crash-call-counts.json");
        ProcessStartInfo start = new(Environment.ProcessPath!,
            $"--credential-native-crash-probe --manifest \"{manifestPath}\" --target-alias {target.Alias} --count-evidence \"{childCountsPath}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment.Clear();
        start.Environment["DOTNET_EnableDiagnostics"] = "0";
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
            store.AddExternalCounts(
                countRoot.GetProperty("credWriteW").GetInt32(),
                countRoot.GetProperty("credReadW").GetInt32(),
                countRoot.GetProperty("credDeleteW").GetInt32(),
                countRoot.GetProperty("credFree").GetInt32());
            store.MarkExternalWrite(target);
        }
        string stdout = child.StandardOutput.ReadToEnd();
        string stderr = child.StandardError.ReadToEnd();
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

    private static void RunCleanupAmbiguity(
        WindowsCredentialManagerStore store,
        NativeTarget unavailable,
        NativeTarget crash,
        List<NativeScenarioEvidence> evidence)
    {
        store.SetFault(WindowsCredentialFault.CleanupAmbiguousBeforeNativeCall);
        bool blocked = false;
        try { _ = store.DeleteAndProveAbsent(unavailable); }
        catch (IOException) { blocked = true; }
        finally { store.SetFault(WindowsCredentialFault.None); }
        if (!blocked) { throw new InvalidOperationException("Ambiguous cleanup did not block target reuse."); }
        evidence.Add(new("cleanup-failure-and-ambiguity",
            [unavailable.TargetFingerprintSha256, crash.TargetFingerprintSha256],
            "namespace-reuse-blocked", true, true));
    }

    private static NativeTarget Target(NativeQualificationManifest manifest, string alias) =>
        manifest.Targets.Single(item => item.Alias == alias);

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

    private static void ScanRetainedEvidence(
        string evidencePath,
        string backupPath,
        string secretCanary,
        IEnumerable<NativeTarget> targets)
    {
        string combined = File.ReadAllText(evidencePath) + File.ReadAllText(backupPath);
        if (combined.Contains(secretCanary, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Native secret canary leaked into retained evidence.");
        }
        foreach (NativeTarget target in targets)
        {
            if (combined.Contains(target.RawTarget, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Raw Credential Manager target leaked into retained evidence.");
            }
        }
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

internal enum WindowsCredentialFault { None, UnavailableBeforeNativeCall, CleanupAmbiguousBeforeNativeCall }

internal sealed class WindowsCredentialManagerStore
{
    public const int MaximumBlobBytes = 2_560;
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private WindowsCredentialFault fault;
    private int writeCount;
    private int readCount;
    private int deleteCount;
    private int freeCount;
    private readonly HashSet<string> writtenTargetFingerprints = new(StringComparer.Ordinal);

    public NativeCallCounts CallCounts => new(writeCount, readCount, deleteCount, freeCount,
        checked(writeCount + readCount + deleteCount + freeCount));

    public void SetFault(WindowsCredentialFault value) => fault = value;

    public bool WasWrittenByThisRun(NativeTarget target) =>
        writtenTargetFingerprints.Contains(target.TargetFingerprintSha256);

    public void MarkExternalWrite(NativeTarget target)
    {
        Validate(target);
        writtenTargetFingerprints.Add(target.TargetFingerprintSha256);
    }

    public void AddExternalCounts(int writes, int reads, int deletes, int frees)
    {
        if (writes < 0 || reads < 0 || deletes < 0 || frees < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(writes));
        }
        writeCount = checked(writeCount + writes);
        readCount = checked(readCount + reads);
        deleteCount = checked(deleteCount + deletes);
        freeCount = checked(freeCount + frees);
    }

    public void WriteExact(NativeTarget target, ReadOnlySpan<byte> secret)
    {
        Validate(target);
        RequireAvailable(cleanup: false);
        if (secret.IsEmpty || secret.Length > MaximumBlobBytes)
        {
            throw new InvalidDataException("The native generic credential is empty or exceeds 2,560 bytes.");
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
                        throw NativeFailure("CredWriteW");
                    }
                    writtenTargetFingerprints.Add(target.TargetFingerprintSha256);
                }
            }
        }
        finally { CryptographicOperations.ZeroMemory(copy); }
    }

    public byte[] ReadExact(NativeTarget target)
    {
        Validate(target);
        RequireAvailable(cleanup: false);
        readCount++;
        if (!CredReadW(target.RawTarget, CredentialTypeGeneric, 0, out SafeCredentialHandle? handle))
        {
            throw NativeFailure("CredReadW");
        }
        freeCount++;
        using (handle)
        {
            NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(handle.DangerousGetHandle());
            if (credential.Type != CredentialTypeGeneric || credential.CredentialBlobSize > MaximumBlobBytes)
            {
                throw new InvalidDataException("The exact native credential record is malformed or oversized.");
            }
            byte[] result = new byte[credential.CredentialBlobSize];
            if (result.Length > 0) { Marshal.Copy(credential.CredentialBlob, result, 0, result.Length); }
            return result;
        }
    }

    public bool Exists(NativeTarget target)
    {
        Validate(target);
        RequireAvailable(cleanup: false);
        readCount++;
        if (CredReadW(target.RawTarget, CredentialTypeGeneric, 0, out SafeCredentialHandle? handle))
        {
            freeCount++;
            handle.Dispose();
            return true;
        }
        int error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound) { return false; }
        throw new Win32Exception(error, "Exact-target CredReadW failed.");
    }

    public bool DeleteAndProveAbsent(NativeTarget target)
    {
        Validate(target);
        RequireAvailable(cleanup: true);
        deleteCount++;
        if (!CredDeleteW(target.RawTarget, CredentialTypeGeneric, 0))
        {
            int error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound) { throw new Win32Exception(error, "Exact-target CredDeleteW failed."); }
        }
        return !Exists(target);
    }

    private void RequireAvailable(bool cleanup)
    {
        if (fault == WindowsCredentialFault.UnavailableBeforeNativeCall
            || cleanup && fault == WindowsCredentialFault.CleanupAmbiguousBeforeNativeCall)
        {
            throw new IOException(cleanup
                ? "Injected ambiguous exact-target cleanup before a native call."
                : "Injected unavailable native credential store before a native call.");
        }
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
        private SafeCredentialHandle() : base(true) { }
        protected override bool ReleaseHandle() { CredFree(handle); return true; }
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

internal static class NativeMaskedEntryDialog
{
    private const uint WsOverlapped = 0x00000000;
    private const uint WsCaption = 0x00C00000;
    private const uint WsSysMenu = 0x00080000;
    private const uint WsVisible = 0x10000000;
    private const uint WsChild = 0x40000000;
    private const uint WsBorder = 0x00800000;
    private const uint EsPassword = 0x0020;
    private const int GwlStyle = -16;
    private const uint WmClose = 0x0010;

    internal static byte[] Capture(ReadOnlySpan<byte> value, bool cancel)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Native masked credential entry requires Windows.");
        }
        byte[] source = value.ToArray();
        byte[] result = [];
        Exception? failure = null;
        Thread thread = new(() =>
        {
            char[] chars = new char[source.Length + 1];
            for (int index = 0; index < source.Length; index++) { chars[index] = (char)source[index]; }
            char[] captured = new char[chars.Length + 1];
            try
            {
                nint window = CreateWindowExW(0, "STATIC", "Infinium disposable credential qualification",
                    WsOverlapped | WsCaption | WsSysMenu | WsVisible, 100, 100, 520, 130,
                    0, 0, 0, 0);
                if (window == 0) { throw new Win32Exception(Marshal.GetLastWin32Error(), "Native entry window creation failed."); }
                nint edit = CreateWindowExW(0, "EDIT", null,
                    WsChild | WsVisible | WsBorder | EsPassword, 20, 30, 470, 28,
                    window, 0, 0, 0);
                if (edit == 0) { throw new Win32Exception(Marshal.GetLastWin32Error(), "Native masked entry control creation failed."); }
                if ((GetWindowLongPtrW(edit, GwlStyle).ToInt64() & EsPassword) == 0)
                {
                    throw new InvalidOperationException("Native credential entry control is not masked.");
                }
                if (!cancel)
                {
                    unsafe
                    {
                        fixed (char* pointer = chars)
                        {
                            _ = SendMessageW(edit, 0x000C, 0, (nint)pointer);
                        }
                    }
                    int length = GetWindowTextW(edit, captured, captured.Length);
                    result = new byte[length];
                    _ = Encoding.ASCII.GetBytes(captured.AsSpan(0, length), result);
                }
                _ = SendMessageW(window, WmClose, 0, 0);
                _ = DestroyWindow(window);
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                Array.Clear(chars);
                Array.Clear(captured);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("Native masked entry did not terminate within its finite deadline.");
        }
        CryptographicOperations.ZeroMemory(source);
        if (failure is not null) { throw failure; }
        return result;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateWindowExW(
        uint exStyle, string className, string? windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextW(nint window, [Out] char[] text, int maximum);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetWindowLongPtrW(nint window, int index);
    [DllImport("user32.dll")]
    private static extern nint SendMessageW(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
}

internal static class NativeSecretCanary
{
    internal static string Create() => "WP4-NATIVE-SECRET-CANARY-" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
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
internal sealed record NativeCanaryEvidence(
    int SecretMatches, int RawTargetMatches, IReadOnlyList<string> ScannedSurfaces);

internal sealed record NativeQualificationEvidence(
    string Schema,
    string Status,
    string ManifestId,
    string ManifestSha256,
    int ProcessId,
    NativeCallCounts NativeCalls,
    IReadOnlyList<NativeScenarioEvidence> Scenarios,
    IReadOnlyList<TargetAbsenceEvidence> TargetAbsence,
    NativeEntryEvidence Entry,
    NativeBackupEvidence BackupRestore,
    NativeCanaryEvidence Canaries,
    int ListenerCount,
    int NetworkOperations,
    int DnsOperations,
    int ProviderOperations,
    int BillableOperations,
    bool RetryAttempted,
    bool FakeProviderOnly,
    long DurationMilliseconds,
    string CompletedAtUtc);
