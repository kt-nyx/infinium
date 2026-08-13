using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.CredentialHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialNativeAuthorizationTests
{
    private static readonly string[] AllowedCalls = ["CredWriteW", "CredReadW", "CredDeleteW", "CredFree"];
    private static readonly string[] ExactImports =
    [
        "advapi32.dll!CredDeleteW",
        "advapi32.dll!CredFree",
        "advapi32.dll!CredReadW",
        "advapi32.dll!CredWriteW",
    ];
    private static readonly string[] ExpectedCanarySurfaceNames = ["stdout", "receipt"];
    private static readonly string[] ExpectedRawTargetEncodings = ["utf-8", "utf-16le"];

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CredentialNativeManifestIsExactFiniteAndTargetBoundWithoutNativeEffect()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string path = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-authorization.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        Assert.AreEqual(WindowsCredentialNativeQualification.AcceptedManifestSha256,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));

        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement rootElement = document.RootElement;
        Assert.AreEqual(WindowsCredentialNativeQualification.AcceptedManifestId,
            rootElement.GetProperty("manifest_id").GetString());
        CollectionAssert.AreEqual(
            AllowedCalls,
            rootElement.GetProperty("native_boundary").GetProperty("allowed_calls")
                .EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual(0, rootElement.GetProperty("provider_boundary").GetProperty("network_operations").GetInt32());
        Assert.AreEqual(0, rootElement.GetProperty("provider_boundary").GetProperty("provider_operations").GetInt32());
        Assert.AreEqual(10, rootElement.GetProperty("required_scenarios").GetArrayLength());

        JsonElement targets = rootElement.GetProperty("disposable_namespace").GetProperty("targets");
        Assert.AreEqual(12, targets.GetArrayLength());
        HashSet<string> fingerprints = new(StringComparer.Ordinal);
        foreach (JsonElement target in targets.EnumerateArray())
        {
            string raw = $"Infinium:{target.GetProperty("access_profile_id").GetString()}:{target.GetProperty("generation_id").GetString()}";
            string expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
            Assert.AreEqual(expected, target.GetProperty("target_fingerprint_sha256").GetString());
            Assert.IsTrue(fingerprints.Add(expected));
            Assert.IsFalse(Encoding.UTF8.GetString(bytes).Contains(raw, StringComparison.Ordinal));
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CredentialNativeV2ProposalIsFiniteFreshAndHasNoEffectAuthority()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string path = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-authorization.v2.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement manifest = document.RootElement;

        Assert.AreEqual("infinium.repository.wp4-credential-native-authorization/1.1.0",
            manifest.GetProperty("schema_identity").GetString());
        Assert.AreEqual(
            "infinium.m1-s6.wp4.credential-native-authorization/ecc56ea0-6ba7-4664-9cf1-8763bc3a26af",
            manifest.GetProperty("manifest_id").GetString());
        Assert.AreEqual("none-until-owner-accepts-exact-manifest-bytes",
            manifest.GetProperty("effect_authority").GetString());
        JsonElement candidate = manifest.GetProperty("candidate_binding");
        Assert.AreEqual("59367a7479a7395b173b974bf720543aab2404d4",
            candidate.GetProperty("accepted_wp7_product_candidate_commit").GetString());
        Assert.AreEqual("51251c0e0eb98d67dbc9b295b9ff084ebca33890",
            candidate.GetProperty("accepted_wp7_evidence_commit").GetString());
        Assert.AreEqual("5df6b621a6ea0031066b2afbfbe204799854910e",
            candidate.GetProperty("authorization_handoff_commit").GetString());
        string closeReady = candidate.GetProperty("close_ready_implementation_commit").GetString()!;
        string status = manifest.GetProperty("status").GetString()!;
        Assert.AreEqual(closeReady == new string('0', 40), status == "draft-close-ready-binding-pending");

        CollectionAssert.AreEqual(AllowedCalls,
            manifest.GetProperty("native_boundary").GetProperty("allowed_calls")
                .EnumerateArray().Select(item => item.GetString()).ToArray());
        JsonElement entry = manifest.GetProperty("entry_boundary");
        Assert.IsFalse(entry.GetProperty("prepopulate").GetBoolean());
        Assert.IsFalse(entry.GetProperty("echo").GetBoolean());
        Assert.IsFalse(entry.GetProperty("clipboard_return").GetBoolean());
        Assert.IsTrue(entry.GetProperty("control").GetString()!.Contains("begins empty", StringComparison.Ordinal));

        JsonElement components = manifest.GetProperty("qualification_components");
        Assert.IsFalse(components.GetProperty("native_success_run")
            .GetProperty("inject_cleanup_ambiguity").GetBoolean());
        Assert.IsTrue(components.GetProperty("non_native_prerequisites")
            .GetProperty("cleanup_ambiguity_probe").GetString()!
            .Contains("zero later native calls", StringComparison.Ordinal));
        Assert.AreEqual(9, manifest.GetProperty("required_scenarios").GetArrayLength());
        Assert.AreEqual(12, manifest.GetProperty("disposable_namespace")
            .GetProperty("targets").GetArrayLength());
        Assert.AreEqual(1800, manifest.GetProperty("operation_limits")
            .GetProperty("gate_wall_clock_seconds").GetInt32());
        Assert.AreEqual(1650, manifest.GetProperty("operation_limits")
            .GetProperty("primary_phase_seconds").GetInt32());
        Assert.AreEqual(3, manifest.GetProperty("operation_limits")
            .GetProperty("entry_dialogs").GetInt32());
        JsonElement maxima = manifest.GetProperty("operation_limits").GetProperty("native_call_maxima");
        Assert.AreEqual(9, maxima.GetProperty("CredWriteW").GetInt32());
        Assert.AreEqual(78, maxima.GetProperty("CredReadW").GetInt32());
        Assert.AreEqual(9, maxima.GetProperty("CredDeleteW").GetInt32());
        Assert.AreEqual(28, maxima.GetProperty("CredFree").GetInt32());
        Assert.AreEqual(124, maxima.GetProperty("total").GetInt32());
        Assert.AreEqual(
            maxima.GetProperty("total").GetInt32(),
            maxima.GetProperty("CredWriteW").GetInt32()
                + maxima.GetProperty("CredReadW").GetInt32()
                + maxima.GetProperty("CredDeleteW").GetInt32()
                + maxima.GetProperty("CredFree").GetInt32());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CredentialNativeGateCannotInvokeConsumedV1OrRecoverEvidence()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string gate = File.ReadAllText(Path.Combine(root, "eng", "verify-m1-slice6.ps1"));
        int activeStart = gate.IndexOf("function Invoke-CredentialNativeGate {", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, activeStart);
        string activeGate = gate[activeStart..];

        Assert.IsTrue(activeGate.Contains("wp4-credential-native-authorization.v2.json", StringComparison.Ordinal));
        Assert.IsTrue(activeGate.Contains(
            "infinium.m1-s6.wp4.credential-native-authorization/ecc56ea0-6ba7-4664-9cf1-8763bc3a26af",
            StringComparison.Ordinal));
        Assert.IsTrue(activeGate.Contains("--credential-native-qualification-v2", StringComparison.Ordinal));
        Assert.IsTrue(activeGate.Contains("FileMode]::CreateNew", StringComparison.Ordinal));
        Assert.IsTrue(activeGate.Contains("manifestIdentitySha", StringComparison.Ordinal));
        Assert.IsTrue(activeGate.Contains(
            "UTF8.GetBytes([string]$manifest.manifest_id)", StringComparison.Ordinal));
        Assert.IsTrue(activeGate.Contains("never recovers or reuses evidence", StringComparison.Ordinal));
        Assert.IsTrue(activeGate.Contains("WP4_V2_OWNER_ACCEPTANCE manifest_id=", StringComparison.Ordinal));
        Assert.IsTrue(activeGate.Contains("WP4_V2_NATIVE_EXECUTED manifest_id=", StringComparison.Ordinal));
        Assert.IsTrue(activeGate.Contains("WaitForExit(1800000)", StringComparison.Ordinal));
        Assert.IsFalse(activeGate.Contains("--credential-native-qualification'", StringComparison.Ordinal));
        Assert.IsFalse(activeGate.Contains("wp4-credential-native-authorization.v1.json", StringComparison.Ordinal));
        Assert.IsFalse(activeGate.Contains("evidenceRecoveryOnly", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void SyntheticProviderDispatchIsEnabledOnlyForExplicitQualificationEntrypoints()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string program = File.ReadAllText(Path.Combine(root, "src", "Infinium.CredentialHelper", "Program.cs"));
        int nativeStart = program.IndexOf(
            "if (args is [\"--credential-native-request-handle\"", StringComparison.Ordinal);
        int ordinaryStart = program.IndexOf(
            "bool providerTransportSelected =", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, nativeStart);
        Assert.IsGreaterThan(nativeStart, ordinaryStart);
        string nativeBranch = program[nativeStart..ordinaryStart];
        string ordinaryBranch = program[ordinaryStart..];

        Assert.AreEqual(1, CountOccurrences(
            nativeBranch, "allowSyntheticProviderDispatch: true"));
        Assert.IsFalse(nativeBranch.Contains("CreateProduction()", StringComparison.Ordinal));
        Assert.IsTrue(ordinaryBranch.Contains(
            "allowSyntheticProviderDispatch: syntheticQualificationTransport",
            StringComparison.Ordinal));
        Assert.IsTrue(ordinaryBranch.Contains(
            "providerTransportSelected && args[^1] == \"synthetic-qualification\"",
            StringComparison.Ordinal));
        Assert.IsFalse(ordinaryBranch.Contains(
            "allowSyntheticProviderDispatch: true", StringComparison.Ordinal));
    }

    private static int CountOccurrences(string value, string token)
    {
        int count = 0;
        for (int index = 0; (index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0;
            index += token.Length)
        {
            count++;
        }
        return count;
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void ConsumedCredentialNativeV1EntrypointsAreTerminallyDisabledBeforeFileAccess()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WindowsCredentialNativeQualification.Run("missing-v1-manifest.json", "must-not-exist.json"));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            WindowsCredentialNativeQualification.RunCrashProbe(
                "missing-v1-manifest.json", "missing-alias", "must-not-exist.json"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CredentialNativeInteropImportsOnlyExactReviewedAdvapiCalls()
    {
        string[] imports = typeof(WindowsCredentialManagerStore)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .Select(method => (Method: method, Import: method.GetCustomAttribute<DllImportAttribute>()))
            .Where(item => item.Import is not null)
            .Select(item => $"{item.Import!.Value}!{item.Method.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(ExactImports, imports);
        Assert.IsFalse(imports.Any(value => value.Contains("Enumerate", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CredentialNativeOversizeAndWrongTargetFailBeforeAnyNativeCall()
    {
        WindowsCredentialManagerStore store = new();
        NativeTarget target = new("test", "profile", "g001", new string('0', 64));
        Assert.ThrowsExactly<InvalidDataException>(() => store.WriteExact(target, new byte[2_561]));
        Assert.AreEqual(0, store.CallCounts.Total);

        string raw = "Infinium:profile:g001";
        target = target with
        {
            TargetFingerprintSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(raw))),
        };
        Assert.ThrowsExactly<InvalidDataException>(() => store.WriteExact(target, new byte[2_561]));
        Assert.AreEqual(0, store.CallCounts.Total);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CleanupAmbiguityProofTerminallyBlocksEveryLaterNativeCall()
    {
        NativeNamespaceReuseGuard guard = new();
        guard.Block("cleanup-ambiguous");
        Assert.IsTrue(guard.IsBlocked);
        Assert.AreEqual("cleanup-ambiguous", guard.Reason);
        Assert.ThrowsExactly<NativeNamespaceBlockedException>(guard.DemandNativeCallAllowed);

        guard.Block("later-reason-cannot-reopen-or-reinterpret");
        Assert.AreEqual("cleanup-ambiguous", guard.Reason);
        Assert.ThrowsExactly<NativeNamespaceBlockedException>(guard.DemandNativeCallAllowed);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CanonicalNativeCallTraceRequiresOrderedExactlyOnceCredFreePairing()
    {
        string fingerprint = new('a', 64);
        NativeCallTraceEntry[] valid =
        [
            new(1, "CredWriteW", fingerprint, "scenario", "success", null, null),
            new(2, "CredReadW", fingerprint, "scenario", "success", 17, null),
            new(3, "CredFree", fingerprint, "scenario", "released", null, 17),
            new(4, "CredDeleteW", fingerprint, "scenario", "success", null, null),
            new(5, "CredReadW", fingerprint, "scenario", "ERROR_NOT_FOUND", null, null),
        ];
        NativeCallTraceValidator.Validate(valid);

        NativeCallTraceEntry[] missingFree = valid.Where(item => item.Operation != "CredFree").ToArray();
        for (int index = 0; index < missingFree.Length; index++)
        {
            missingFree[index] = missingFree[index] with { Sequence = index + 1 };
        }
        Assert.ThrowsExactly<InvalidDataException>(() => NativeCallTraceValidator.Validate(missingFree));

        NativeCallTraceEntry[] duplicateFree = [.. valid, valid[2] with { Sequence = 6 }];
        Assert.ThrowsExactly<InvalidDataException>(() => NativeCallTraceValidator.Validate(duplicateFree));
        Assert.ThrowsExactly<InvalidDataException>(() => NativeCallTraceValidator.Validate(
            [valid[0] with { Operation = "CredEnumerateW" }]));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void CanaryScannerReportsOnlyActuallyScannedSurfacesAndDetectsMutations()
    {
        byte[] secret = "manual-secret-canary"u8.ToArray();
        const string rawTarget = "Infinium:profile:g001";
        byte[] targetUtf8 = Encoding.UTF8.GetBytes(rawTarget);
        byte[] targetUtf16 = Encoding.Unicode.GetBytes(rawTarget);
        NativeRawTargetCanary[] targets =
        [
            new("utf-8", targetUtf8),
            new("utf-16le", targetUtf16),
        ];
        NativeCanarySurface[] clean =
        [
            NativeCanarySurface.FromText("stdout", "ordinary output"),
            new("receipt", "retained-bytes", "ordinary receipt"u8.ToArray()),
        ];
        NativeCanaryEvidence cleanResult = NativeCanaryScanner.Scan(secret, targets, clean);
        Assert.AreEqual(0, cleanResult.SecretMatches);
        Assert.AreEqual(0, cleanResult.RawTargetMatches);
        CollectionAssert.AreEqual(ExpectedCanarySurfaceNames,
            cleanResult.ScannedSurfaces.Select(item => item.Name).ToArray());
        CollectionAssert.AreEqual(ExpectedRawTargetEncodings, cleanResult.RawTargetEncodings.ToArray());
        Assert.IsTrue(cleanResult.ScannedSurfaces.All(item => item.ByteCount > 0));
        Assert.ThrowsExactly<InvalidDataException>(() => NativeCanaryScanner.Scan(
            secret, [new("utf-8", targetUtf8)], clean));

        NativeCanaryEvidence mutated = NativeCanaryScanner.Scan(secret, targets,
        [
            new("mutated-secret", "test-mutation", [.. "prefix"u8.ToArray(), .. secret]),
            new("mutated-target-utf8", "test-mutation", [.. targetUtf8, .. "suffix"u8.ToArray()]),
            new("mutated-target-utf16le", "test-mutation", [.. targetUtf16, .. "suffix"u8.ToArray()]),
        ]);
        Assert.AreEqual(1, mutated.SecretMatches);
        Assert.AreEqual(2, mutated.RawTargetMatches);
        CryptographicOperations.ZeroMemory(secret);
        CryptographicOperations.ZeroMemory(targetUtf8);
        CryptographicOperations.ZeroMemory(targetUtf16);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void NativeEntryStateMachineRequiresBlankStartAndTerminalCleanup()
    {
        NativeEntryStateMachine state = new();
        Assert.ThrowsExactly<InvalidOperationException>(() => state.Activate(1));

        state = new();
        state.Activate(0);
        state.RecordClipboardMessageBlocked();
        state.Submit("manually-entered"u8);
        state.CompleteUiThreadCleanup(windowWasDestroyed: true, buffersWereCleared: true);
        state.RecordThreadJoined();
        Assert.AreEqual(new NativeEntryCleanupEvidence(true, true, true, true, true, true), state.Evidence);

        foreach (Action<NativeEntryStateMachine> terminal in new Action<NativeEntryStateMachine>[]
        {
            item => item.Cancel(), item => item.Timeout(), item => item.Fail(),
        })
        {
            NativeEntryStateMachine candidate = new();
            candidate.Activate(0);
            candidate.RecordClipboardMessageBlocked();
            terminal(candidate);
            candidate.CompleteUiThreadCleanup(true, true);
            candidate.RecordThreadJoined();
            Assert.IsTrue(candidate.Evidence.InitialBlank);
            Assert.IsTrue(candidate.Evidence.Terminal);
            Assert.IsTrue(candidate.Evidence.WindowDestroyed);
            Assert.IsTrue(candidate.Evidence.BuffersCleared);
            Assert.IsTrue(candidate.Evidence.ThreadJoined);
            Assert.IsTrue(candidate.Evidence.ClipboardMessagesBlocked);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void NativeQualificationDeadlineFailsClosedAtExactFiniteBound()
    {
        long elapsed = 999;
        FiniteNativeDeadline deadline = FiniteNativeDeadline.ForTest(1_000, () => elapsed);
        deadline.DemandRemaining("before-bound");
        NativeDeadlineEvidence before = deadline.Snapshot();
        Assert.IsTrue(before.CompletedWithinLimit);
        Assert.AreEqual(1_000L, before.LimitMilliseconds);
        Assert.AreEqual(1, before.Checks);

        elapsed = 1_000;
        Assert.ThrowsExactly<TimeoutException>(() => deadline.DemandRemaining("at-bound"));
        NativeDeadlineEvidence atBound = deadline.Snapshot();
        Assert.IsFalse(atBound.CompletedWithinLimit);
        Assert.AreEqual(2, atBound.Checks);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void NativeQualificationGeneratedBoundarySecretsAreExactAndRemainHelperLocal()
    {
        NativeQualificationSecretSource source = new(TimeSpan.FromMilliseconds(1));
        Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentV2 maximum = new()
        {
            AssignmentId = "wp4-v2/credential-size-boundaries/maximum",
            AssignmentKind = Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentKindV2.Enroll,
        };
        byte[] maximumBytes = source.Capture(maximum);
        Assert.HasCount(WindowsCredentialManagerStore.MaximumBlobBytes, maximumBytes);
        CryptographicOperations.ZeroMemory(maximumBytes);

        Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentV2 oversized = new()
        {
            AssignmentId = "wp4-v2/credential-size-boundaries/oversize",
            AssignmentKind = Infinium.Contracts.Protobuf.Helper.V2.HelperAssignmentKindV2.Enroll,
        };
        byte[] oversizedBytes = source.Capture(oversized);
        Assert.HasCount(WindowsCredentialManagerStore.MaximumBlobBytes + 1, oversizedBytes);
        CryptographicOperations.ZeroMemory(oversizedBytes);
    }
}
