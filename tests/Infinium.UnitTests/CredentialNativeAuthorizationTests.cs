using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
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
    private static readonly string[] RecoveryAllowedCalls = ["CredReadW", "CredDeleteW", "CredFree"];
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

        Assert.AreEqual("infinium.repository.wp4-credential-native-authorization/1.3.0",
            manifest.GetProperty("schema_identity").GetString());
        Assert.AreEqual(
            "infinium.m1-s6.wp4.credential-native-authorization/e6e04651-4cd5-4f5d-8b46-5ec84a81cbbe",
            manifest.GetProperty("manifest_id").GetString());
        Assert.AreEqual("none-until-owner-accepts-exact-manifest-bytes",
            manifest.GetProperty("effect_authority").GetString());
        JsonElement candidate = manifest.GetProperty("candidate_binding");
        Assert.AreEqual("59367a7479a7395b173b974bf720543aab2404d4",
            candidate.GetProperty("accepted_wp7_product_candidate_commit").GetString());
        Assert.AreEqual("51251c0e0eb98d67dbc9b295b9ff084ebca33890",
            candidate.GetProperty("accepted_wp7_evidence_commit").GetString());
        Assert.AreEqual("44fbcc0542bef77f93c83f1422406a2b6012f0d5",
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
        Assert.IsTrue(entry.GetProperty("control").GetString()!.Contains(
            "M1 qualification-only", StringComparison.Ordinal));
        Assert.IsTrue(entry.GetProperty("control").GetString()!.Contains(
            "Settings -> Add/Replace -> WPF-parented helper modal", StringComparison.Ordinal));
        Assert.IsTrue(entry.GetProperty("operator_action").GetString()!.Contains(
            "manually types", StringComparison.Ordinal));
        Assert.IsTrue(entry.GetProperty("operator_action").GetString()!.Contains(
            "clipboard paste is deliberately blocked only in the qualification harness", StringComparison.Ordinal));
        Assert.IsTrue(entry.GetProperty("operator_action").GetString()!.Contains(
            "paste-capable WPF-parented helper-owned masked modal", StringComparison.Ordinal));
        Assert.IsTrue(entry.GetProperty("operator_action").GetString()!.Contains(
            "React/WebView provides only the gesture and non-secret status", StringComparison.Ordinal));
        Assert.IsTrue(entry.GetProperty("readiness_oracle").GetString()!.Contains(
            "short finite 10-second automatic pre-entry readiness window", StringComparison.Ordinal));
        Assert.IsTrue(entry.GetProperty("readiness_oracle").GetString()!.Contains(
            "separate finite five-minute human response interval", StringComparison.Ordinal));
        Assert.AreEqual(
            "exact manifest bytes and SHA-256 plus the superseded e3f76cd6 terminal manifest, failure evidence, authority lock, cleanup-recovery manifest/evidence/lock/receipt, and combined 12-target absence disposition",
            manifest.GetProperty("required_evidence")[0].GetString());

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
    public void NativeStoreParsesTheExactCurrentAuthorizationSchemaAndRejectsTheSupersededSchema()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string path = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-authorization.v2.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        string manifestId = document.RootElement.GetProperty("manifest_id").GetString()!;
        string sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

        bool bindingPending = document.RootElement.GetProperty("status").GetString()
            == "draft-close-ready-binding-pending";
        if (bindingPending)
        {
            Assert.ThrowsExactly<InvalidDataException>(() => WindowsCredentialManagerStore.FromAcceptedManifest(
                path, sha256, manifestId));
        }
        else
        {
            using WindowsCredentialManagerStore store = WindowsCredentialManagerStore.FromAcceptedManifest(
                path, sha256, manifestId);
            Assert.HasCount(12, store.ManifestTargets);
        }

        string temporary = Path.Combine(Path.GetTempPath(), "Infinium-Wp4-Schema-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            JsonNode accepted = JsonNode.Parse(bytes)
                ?? throw new InvalidDataException("The test manifest is malformed.");
            accepted["status"] = "ready-for-owner-acceptance";
            accepted["candidate_binding"]!["close_ready_implementation_commit"] = new string('1', 40);
            File.WriteAllText(temporary, accepted.ToJsonString());
            string acceptedSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(temporary)));
            using (WindowsCredentialManagerStore store = WindowsCredentialManagerStore.FromAcceptedManifest(
                temporary, acceptedSha256, manifestId))
            {
                Assert.HasCount(12, store.ManifestTargets);
            }

            void Reject(JsonNode rejected)
            {
                File.WriteAllText(temporary, rejected.ToJsonString());
                string rejectedSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(temporary)));
                Assert.ThrowsExactly<InvalidDataException>(() => WindowsCredentialManagerStore.FromAcceptedManifest(
                    temporary, rejectedSha256, manifestId));
            }

            JsonNode rejected = accepted.DeepClone();
            rejected["schema_identity"] = "infinium.repository.wp4-credential-native-authorization/1.2.0";
            Reject(rejected);

            rejected = accepted.DeepClone();
            rejected["supersedes"]!.AsObject().Remove("authority_lock_sha256");
            Reject(rejected);

            rejected = accepted.DeepClone();
            rejected["supersedes"]!["authority_lock_sha256"] = new string('0', 64);
            Reject(rejected);

            rejected = accepted.DeepClone();
            rejected["supersedes"]!["cleanup_recovery"]!["evidence_sha256"] = new string('0', 64);
            Reject(rejected);

            rejected = accepted.DeepClone();
            JsonObject supersedes = rejected["supersedes"]!.AsObject();
            JsonNode authorityLock = supersedes["authority_lock_sha256"]!.DeepClone();
            supersedes.Remove("authority_lock_sha256");
            supersedes["gate_receipt_sha256"] = authorityLock;
            Reject(rejected);
        }
        finally
        {
            File.Delete(temporary);
        }
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
            "infinium.m1-s6.wp4.credential-native-authorization/e6e04651-4cd5-4f5d-8b46-5ec84a81cbbe",
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

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void NativeRecoveryIsCleanupOnlyAndCannotReachWriteUiOrProvider()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifest = File.ReadAllText(Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-recovery.ad876b9a.v1.json"));
        using JsonDocument document = JsonDocument.Parse(manifest);
        JsonElement native = document.RootElement.GetProperty("native_boundary");
        CollectionAssert.AreEqual(RecoveryAllowedCalls,
            native.GetProperty("allowed_calls").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.IsTrue(native.GetProperty("forbidden").EnumerateArray().Any(item => item.GetString() == "CredWriteW"));
        Assert.AreEqual("none", native.GetProperty("ui").GetString());
        Assert.AreEqual("none", native.GetProperty("provider").GetString());
        Assert.AreEqual(2, document.RootElement.GetProperty("disposable_namespace").GetProperty("targets").GetArrayLength());
        string e6Manifest = File.ReadAllText(Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-recovery.e6e04651.v1.json"));
        using JsonDocument e6Document = JsonDocument.Parse(e6Manifest);
        JsonElement e6Native = e6Document.RootElement.GetProperty("native_boundary");
        CollectionAssert.AreEqual(RecoveryAllowedCalls,
            e6Native.GetProperty("allowed_calls").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual(12, e6Document.RootElement.GetProperty("disposable_namespace").GetProperty("targets").GetArrayLength());
        Assert.AreEqual(0, e6Document.RootElement.GetProperty("binding").GetProperty("prior_exact_absence_count").GetInt32());

        string program = File.ReadAllText(Path.Combine(root, "src", "Infinium.CredentialHelper", "Program.cs"));
        int recovery = program.IndexOf("--credential-native-recovery", StringComparison.Ordinal);
        int qualification = program.IndexOf("--credential-native-request-handle", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, recovery);
        Assert.IsGreaterThan(recovery, qualification);
        string recoveryBranch = program[recovery..qualification];
        Assert.IsFalse(recoveryBranch.Contains("NativeQualificationSecretSource", StringComparison.Ordinal));
        Assert.IsFalse(recoveryBranch.Contains("OpenAiResponsesAdapter", StringComparison.Ordinal));

        string gate = File.ReadAllText(Path.Combine(root, "eng", "verify-m1-slice6.ps1"));
        int gateStart = gate.IndexOf("function Invoke-CredentialNativeRecoveryGate", StringComparison.Ordinal);
        int dispatch = gate.IndexOf("'CredentialNativeRecovery' { Invoke-CredentialNativeRecoveryGate }", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, gateStart);
        Assert.IsGreaterThan(gateStart, dispatch);
        string recoveryGate = gate[gateStart..dispatch];
        Assert.IsTrue(recoveryGate.Contains("validate-m1-slice6-wp4-recovery-evidence.ps1",
            StringComparison.Ordinal));
        Assert.IsTrue(recoveryGate.Contains("wp4-credential-native-recovery.ad876b9a.v1.json",
            StringComparison.Ordinal));
        Assert.IsTrue(recoveryGate.Contains("wp4-credential-native-recovery.e6e04651.v1.json",
            StringComparison.Ordinal));
        Assert.IsTrue(recoveryGate.Contains("credential-native-cleanup-ambiguity.v2.json",
            StringComparison.Ordinal));
        Assert.IsTrue(recoveryGate.Contains("combined_namespace_target_absence_count=12",
            StringComparison.Ordinal));
        Assert.IsTrue(recoveryGate.Contains("Recovery requires branch codex/m1-s6.", StringComparison.Ordinal));
        Assert.IsTrue(recoveryGate.Contains("Recovery requires a fresh absent output root.", StringComparison.Ordinal));
        string reconstruction = File.ReadAllText(Path.Combine(root, "eng",
            "reconstruct-m1-slice6-wp4-recovery-receipt.ps1"));
        Assert.IsFalse(reconstruction.Contains("Infinium.CredentialHelper", StringComparison.Ordinal));
        Assert.IsFalse(reconstruction.Contains("Process.Start", StringComparison.Ordinal));
        Assert.IsFalse(reconstruction.Contains("Infinium.CredentialHelper.exe", StringComparison.Ordinal));
        Assert.IsFalse(reconstruction.Contains("--credential-native-recovery", StringComparison.Ordinal));
        string currentReconstruction = File.ReadAllText(Path.Combine(root, "eng",
            "reconstruct-m1-slice6-wp4-recovery-ad876b9a-receipt.ps1"));
        Assert.IsFalse(currentReconstruction.Contains("Infinium.CredentialHelper", StringComparison.Ordinal));
        Assert.IsFalse(currentReconstruction.Contains("Process.Start", StringComparison.Ordinal));
        Assert.IsFalse(currentReconstruction.Contains("--credential-native-recovery", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void RecoveryEvidenceValidatorAcceptsCanonicalEvidenceAndRejectsSemanticMutations()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifestPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-recovery.ad876b9a.v1.json");
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        string sha = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        JsonObject manifest = JsonNode.Parse(manifestBytes)!.AsObject();
        string id = manifest["manifest_id"]!.GetValue<string>();
        JsonArray targets = manifest["disposable_namespace"]!["targets"]!.AsArray();
        JsonArray absence = [];
        foreach (JsonNode? target in targets)
        {
            absence.Add(new JsonObject
            {
                ["alias"] = target!["alias"]!.GetValue<string>(),
                ["target_fingerprint_sha256"] = target["target_fingerprint_sha256"]!.GetValue<string>(),
                ["result"] = "ERROR_NOT_FOUND",
            });
        }
        string first = targets[0]!["target_fingerprint_sha256"]!.GetValue<string>();
        JsonArray trace =
        [
            Trace(1, "CredReadW", first, "success", 17, null),
            Trace(2, "CredFree", first, "released", null, 17),
            Trace(3, "CredDeleteW", first, "success", null, null),
            Trace(4, "CredReadW", first, "ERROR_NOT_FOUND", null, null),
        ];
        long sequence = 5;
        foreach (JsonNode? target in targets.Skip(1))
        {
            trace.Add(Trace(sequence++, "CredReadW",
                target!["target_fingerprint_sha256"]!.GetValue<string>(), "ERROR_NOT_FOUND", null, null));
        }
        JsonObject valid = new()
        {
            ["schema"] = "infinium.m1-s6.wp4.credential-native-recovery-evidence/v1",
            ["status"] = "passed",
            ["manifest_id"] = id,
            ["manifest_sha256"] = sha,
            ["target_absence"] = absence,
            ["native_call_counts"] = new JsonObject
            {
                ["cred_write_w"] = 0,
                ["cred_read_w"] = 3,
                ["cred_delete_w"] = 1,
                ["cred_free"] = 1,
                ["total"] = 5,
            },
            ["native_call_trace"] = trace,
            ["cleanup_ambiguity"] = false,
            ["namespace_reuse_blocked"] = true,
            ["namespace_disposition"] = "cleanup-confirmed-absent-never-reuse",
            ["prior_terminal_evidence_sha256"] = manifest["binding"]!["terminal_evidence_sha256"]!.GetValue<string>(),
            ["prior_authority_lock_sha256"] = manifest["binding"]!["consumed_lock_sha256"]!.GetValue<string>(),
            ["prior_exact_absence_count"] = 10,
            ["combined_namespace_target_absence_count"] = 12,
            ["network_operations"] = 0,
            ["dns_operations"] = 0,
            ["provider_operations"] = 0,
            ["billable_operations"] = 0,
        };

        AssertEvidenceValidation(root, manifestPath, sha, id, valid, expectedSuccess: true);
        AssertCurrentReceiptReconstruction(root, manifestPath, sha, id, valid);
        Reject(node => node["schema"] = "mutated");
        Reject(node => node["manifest_id"] = "mutated");
        Reject(node => node["manifest_sha256"] = new string('0', 64));
        Reject(node => node["status"] = "failed");
        Reject(node => node["target_absence"]![0]!["alias"] = "mutated");
        Reject(node => node["target_absence"]![0]!["target_fingerprint_sha256"] = new string('f', 64));
        Reject(node => node["native_call_trace"]![0]!["operation"] = "CredEnumerateW");
        Reject(node => node["native_call_trace"]![0]!["sequence"] = 2);
        Reject(node => node["native_call_trace"]![0]!["scenario"] = "mutated");
        Reject(node =>
        {
            JsonArray items = node["native_call_trace"]!.AsArray();
            for (int index = 0; index < 4; index++)
            {
                items.Insert(3, Trace(0, "CredReadW", first, "ERROR_NOT_FOUND", null, null));
            }
            Renumber(items);
            node["native_call_counts"]!["cred_read_w"] = 7;
            node["native_call_counts"]!["total"] = 9;
        });
        Reject(node => node["native_call_trace"]![1]!["target_fingerprint_sha256"] =
            targets[1]!["target_fingerprint_sha256"]!.GetValue<string>());
        Reject(node =>
        {
            JsonArray items = node["native_call_trace"]!.AsArray();
            items.RemoveAt(1);
            Renumber(items);
            node["native_call_counts"]!["cred_free"] = 0;
            node["native_call_counts"]!["total"] = 4;
        });
        Reject(node =>
        {
            JsonArray items = node["native_call_trace"]!.AsArray();
            items.Insert(2, Trace(0, "CredFree", first, "released", null, 17));
            Renumber(items);
            node["native_call_counts"]!["cred_free"] = 2;
            node["native_call_counts"]!["total"] = 6;
        });
        Reject(node =>
        {
            JsonArray items = node["native_call_trace"]!.AsArray();
            JsonNode free = items[1]!.DeepClone();
            items.RemoveAt(1);
            items.Insert(0, free);
            Renumber(items);
        });
        Reject(node => node["native_call_trace"]![4]!["allocation_id"] = 99);
        Reject(node => node["native_call_trace"]![4]!["paired_allocation_id"] = 17);
        Reject(node => node["native_call_trace"]![2]!["allocation_id"] = 99);
        Reject(node => node["native_call_trace"]![2]!["paired_allocation_id"] = 17);
        Reject(node => node["native_call_trace"]![1]!["allocation_id"] = 99);
        Reject(node => node["native_call_trace"]![1]!["paired_allocation_id"] = null);
        Reject(node => node["native_call_trace"]![1]!["result"] = "success");
        Reject(node =>
        {
            JsonArray items = node["native_call_trace"]!.AsArray();
            items.RemoveAt(4);
            node["native_call_counts"]!["cred_read_w"] = 2;
            node["native_call_counts"]!["total"] = 4;
        });
        Reject(node => node["cleanup_ambiguity"] = true);
        Reject(node => node["namespace_reuse_blocked"] = false);
        Reject(node => node["namespace_disposition"] = "reusable");
        Reject(node => node["prior_terminal_evidence_sha256"] = new string('0', 64));
        Reject(node => node["prior_authority_lock_sha256"] = new string('0', 64));
        Reject(node => node["prior_exact_absence_count"] = 9);
        Reject(node => node["combined_namespace_target_absence_count"] = 11);
        Reject(node => node["network_operations"] = 1);
        Reject(node => node["dns_operations"] = 1);
        Reject(node => node["provider_operations"] = 1);
        Reject(node => node["billable_operations"] = 1);

        void Reject(Action<JsonObject> mutate)
        {
            JsonObject mutation = valid.DeepClone().AsObject();
            mutate(mutation);
            AssertEvidenceValidation(root, manifestPath, sha, id, mutation, expectedSuccess: false);
        }

        static void Renumber(JsonArray items)
        {
            for (int index = 0; index < items.Count; index++) { items[index]!["sequence"] = index + 1; }
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void E3RecoveryEvidenceAndReceiptBindExactTwoTargetLineage()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifestPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-recovery.e3f76cd6.v1.json");
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        string sha = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        JsonObject manifest = JsonNode.Parse(manifestBytes)!.AsObject();
        string id = manifest["manifest_id"]!.GetValue<string>();
        JsonArray targets = manifest["disposable_namespace"]!["targets"]!.AsArray();
        string first = targets[0]!["target_fingerprint_sha256"]!.GetValue<string>();
        string second = targets[1]!["target_fingerprint_sha256"]!.GetValue<string>();
        JsonObject evidence = new()
        {
            ["schema"] = "infinium.m1-s6.wp4.credential-native-recovery-evidence/v1",
            ["status"] = "passed",
            ["manifest_id"] = id,
            ["manifest_sha256"] = sha,
            ["target_absence"] = new JsonArray(
                new JsonObject { ["alias"] = "backup-new", ["target_fingerprint_sha256"] = first, ["result"] = "ERROR_NOT_FOUND" },
                new JsonObject { ["alias"] = "fake-dispatch", ["target_fingerprint_sha256"] = second, ["result"] = "ERROR_NOT_FOUND" }),
            ["native_call_counts"] = new JsonObject
            {
                ["cred_write_w"] = 0,
                ["cred_read_w"] = 2,
                ["cred_delete_w"] = 0,
                ["cred_free"] = 0,
                ["total"] = 2,
            },
            ["native_call_trace"] = new JsonArray(
                Trace(1, "CredReadW", first, "ERROR_NOT_FOUND", null, null),
                Trace(2, "CredReadW", second, "ERROR_NOT_FOUND", null, null)),
            ["cleanup_ambiguity"] = false,
            ["namespace_reuse_blocked"] = true,
            ["namespace_disposition"] = "cleanup-confirmed-absent-never-reuse",
            ["prior_terminal_evidence_sha256"] = manifest["binding"]!["terminal_evidence_sha256"]!.GetValue<string>(),
            ["prior_authority_lock_sha256"] = manifest["binding"]!["consumed_lock_sha256"]!.GetValue<string>(),
            ["prior_exact_absence_count"] = 10,
            ["combined_namespace_target_absence_count"] = 12,
            ["network_operations"] = 0,
            ["dns_operations"] = 0,
            ["provider_operations"] = 0,
            ["billable_operations"] = 0,
        };
        AssertEvidenceValidation(root, manifestPath, sha, id, evidence, expectedSuccess: true);
        AssertCurrentReceiptReconstruction(root, manifestPath, sha, id, evidence);
        evidence["target_absence"]!.AsArray().RemoveAt(1);
        AssertEvidenceValidation(root, manifestPath, sha, id, evidence, expectedSuccess: false);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void E6RecoveryEvidenceAndReceiptBindExactTwelveTargetAmbiguityLineage()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifestPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-recovery.e6e04651.v1.json");
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);
        string sha = Convert.ToHexStringLower(SHA256.HashData(manifestBytes));
        JsonObject manifest = JsonNode.Parse(manifestBytes)!.AsObject();
        string id = manifest["manifest_id"]!.GetValue<string>();
        JsonArray targets = manifest["disposable_namespace"]!["targets"]!.AsArray();
        JsonArray absence = [];
        JsonArray trace = [];
        long sequence = 1;
        foreach (JsonNode? target in targets)
        {
            string alias = target!["alias"]!.GetValue<string>();
            string fingerprint = target["target_fingerprint_sha256"]!.GetValue<string>();
            absence.Add(new JsonObject
            {
                ["alias"] = alias,
                ["target_fingerprint_sha256"] = fingerprint,
                ["result"] = "ERROR_NOT_FOUND",
            });
            trace.Add(Trace(sequence++, "CredReadW", fingerprint, "ERROR_NOT_FOUND", null, null));
        }
        JsonObject evidence = new()
        {
            ["schema"] = "infinium.m1-s6.wp4.credential-native-recovery-evidence/v1",
            ["status"] = "passed",
            ["manifest_id"] = id,
            ["manifest_sha256"] = sha,
            ["target_absence"] = absence,
            ["native_call_counts"] = new JsonObject
            {
                ["cred_write_w"] = 0,
                ["cred_read_w"] = 12,
                ["cred_delete_w"] = 0,
                ["cred_free"] = 0,
                ["total"] = 12,
            },
            ["native_call_trace"] = trace,
            ["cleanup_ambiguity"] = false,
            ["namespace_reuse_blocked"] = true,
            ["namespace_disposition"] = "cleanup-confirmed-absent-never-reuse",
            ["prior_terminal_evidence_sha256"] = manifest["binding"]!["terminal_evidence_sha256"]!.GetValue<string>(),
            ["prior_authority_lock_sha256"] = manifest["binding"]!["consumed_lock_sha256"]!.GetValue<string>(),
            ["prior_exact_absence_count"] = 0,
            ["combined_namespace_target_absence_count"] = 12,
            ["network_operations"] = 0,
            ["dns_operations"] = 0,
            ["provider_operations"] = 0,
            ["billable_operations"] = 0,
        };
        AssertEvidenceValidation(root, manifestPath, sha, id, evidence, expectedSuccess: true);
        AssertE6ReceiptReconstruction(root, manifestPath, sha, id, evidence);
        Reject(node => node["prior_exact_absence_count"] = 10);
        Reject(node => node["target_absence"]!.AsArray().RemoveAt(11));
        Reject(node =>
        {
            JsonArray items = node["target_absence"]!.AsArray();
            JsonNode first = items[0]!.DeepClone();
            items[0] = items[1]!.DeepClone();
            items[1] = first;
        });
        Reject(node => node["native_call_trace"]![0]!["operation"] = "CredWriteW");
        Reject(node => node["native_call_trace"]![11]!["result"] = "success");
        Reject(node => node["native_call_counts"]!["cred_read_w"] = 13);

        void Reject(Action<JsonObject> mutate)
        {
            JsonObject mutation = evidence.DeepClone().AsObject();
            mutate(mutation);
            AssertEvidenceValidation(root, manifestPath, sha, id, mutation, expectedSuccess: false);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void RecoveryManifestValidatorRejectsNestedAuthorityMutations()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifestPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-recovery.ad876b9a.v1.json");
        JsonObject valid = JsonNode.Parse(File.ReadAllBytes(manifestPath))!.AsObject();
        Assert.AreEqual(0, RunPwsh(root, "eng/validate-m1-slice6-wp4-recovery-ad876b9a.ps1",
            "-ManifestPath", manifestPath), "The gate supplies an absolute manifest path.");
        AssertManifestValidation(root, valid, expectedSuccess: true);
        Reject(node => node["binding"]!["failed_manifest_sha256"] = new string('0', 64));
        Reject(node => node["binding"]!["terminal_evidence_sha256"] = new string('0', 64));
        Reject(node => node["binding"]!["prior_exact_absence_count"] = 9);
        Reject(node => node["disposable_namespace"]!["namespace_id"] = "mutated");
        Reject(node => node["disposable_namespace"]!["targets"]![0]!["alias"] = "fake-dispatch");
        Reject(node =>
        {
            JsonArray targets = node["disposable_namespace"]!["targets"]!.AsArray();
            JsonNode first = targets[0]!.DeepClone();
            targets[0] = targets[1]!.DeepClone();
            targets[1] = first;
        });
        Reject(node => node["disposable_namespace"]!["targets"]!.AsArray().Add(
            node["disposable_namespace"]!["targets"]![0]!.DeepClone()));
        Reject(node => node["native_boundary"]!["forbidden"]!.AsArray().RemoveAt(0));
        Reject(node => node["native_boundary"]!["fallback"] = "alternate-store");
        Reject(node => node["limits"]!["CredReadW"] = 7);
        Reject(node => node["cleanup_contract"]!["dns_operations"] = 1);
        Reject(node => node["execution_command"] = "mutated");
        Reject(node => node["binding"]!["unexpected"] = true);

        void Reject(Action<JsonObject> mutate)
        {
            JsonObject mutation = valid.DeepClone().AsObject();
            mutate(mutation);
            AssertManifestValidation(root, mutation, expectedSuccess: false);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void E3RecoveryManifestValidatorBindsExactFailureAndTargets()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifestPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-recovery.e3f76cd6.v1.json");
        JsonObject valid = JsonNode.Parse(File.ReadAllBytes(manifestPath))!.AsObject();
        Assert.AreEqual(0, RunPwsh(root, "eng/validate-m1-slice6-wp4-recovery-e3f76cd6.ps1",
            "-ManifestPath", manifestPath));
        Reject(node => node["binding"]!["failed_manifest_id"] = "mutated");
        Reject(node => node["binding"]!["terminal_evidence_sha256"] = new string('0', 64));
        Reject(node => node["binding"]!["consumed_lock_sha256"] = new string('0', 64));
        Reject(node => node["disposable_namespace"]!["targets"]![0]!["generation_id"] = "g003");
        Reject(node => node["disposable_namespace"]!["targets"]![1]!["target_fingerprint_sha256"] = new string('0', 64));
        Reject(node => node["limits"]!["CredDeleteW"] = 3);
        Reject(node => node["native_boundary"]!["allowed_calls"]!.AsArray().Insert(0, "CredWriteW"));
        Reject(node => node["execution_command"] = "mutated");

        void Reject(Action<JsonObject> mutate)
        {
            JsonObject mutation = valid.DeepClone().AsObject();
            mutate(mutation);
            string tempRoot = Path.Combine(root, "artifacts", "test-temp", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string path = Path.Combine(tempRoot, "manifest.json");
            try
            {
                File.WriteAllText(path, mutation.ToJsonString());
                Assert.AreEqual(1, RunPwsh(root, "eng/validate-m1-slice6-wp4-recovery-e3f76cd6.ps1",
                    "-ManifestPath", path));
            }
            finally { Directory.Delete(tempRoot, recursive: true); }
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void E6RecoveryManifestValidatorBindsExactAmbiguityAndTwelveTargets()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifestPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-recovery.e6e04651.v1.json");
        JsonObject valid = JsonNode.Parse(File.ReadAllBytes(manifestPath))!.AsObject();
        Assert.AreEqual(0, RunPwsh(root, "eng/validate-m1-slice6-wp4-recovery-e6e04651.ps1",
            "-ManifestPath", manifestPath));
        Reject(node => node["schema_identity"] = "infinium.repository.wp4-credential-native-recovery/1.4.0");
        Reject(node => node["binding"]!["failed_manifest_id"] = "mutated");
        Reject(node => node["binding"]!["terminal_evidence_sha256"] = new string('0', 64));
        Reject(node => node["binding"]!["prior_exact_absence_count"] = 10);
        Reject(node => node["disposable_namespace"]!["targets"]![0]!["generation_id"] = "g002");
        Reject(node => node["disposable_namespace"]!["targets"]![11]!["target_fingerprint_sha256"] = new string('0', 64));
        Reject(node => node["disposable_namespace"]!["targets"]!.AsArray().RemoveAt(11));
        Reject(node => node["disposable_namespace"]!["targets"]!.AsArray().Add(
            node["disposable_namespace"]!["targets"]![0]!.DeepClone()));
        Reject(node =>
        {
            JsonArray targets = node["disposable_namespace"]!["targets"]!.AsArray();
            JsonNode first = targets[0]!.DeepClone();
            targets[0] = targets[1]!.DeepClone();
            targets[1] = first;
        });
        Reject(node => node["limits"]!["CredReadW"] = 35);
        Reject(node => node["native_boundary"]!["allowed_calls"]!.AsArray().Insert(0, "CredWriteW"));
        Reject(node => node["execution_command"] = "mutated");

        void Reject(Action<JsonObject> mutate)
        {
            JsonObject mutation = valid.DeepClone().AsObject();
            mutate(mutation);
            string tempRoot = Path.Combine(root, "artifacts", "test-temp", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            string path = Path.Combine(tempRoot, "manifest.json");
            try
            {
                File.WriteAllText(path, mutation.ToJsonString());
                Assert.AreEqual(1, RunPwsh(root, "eng/validate-m1-slice6-wp4-recovery-e6e04651.ps1",
                    "-ManifestPath", path));
            }
            finally { Directory.Delete(tempRoot, recursive: true); }
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void LegacyRecoveryEvidenceRemainsValidUnderItsFrozenSchema()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string manifestPath = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp4-credential-native-recovery.v1.json");
        byte[] bytes = File.ReadAllBytes(manifestPath);
        string sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        JsonObject manifest = JsonNode.Parse(bytes)!.AsObject();
        string id = manifest["manifest_id"]!.GetValue<string>();
        JsonArray targets = manifest["disposable_namespace"]!["targets"]!.AsArray();
        JsonArray absence = [];
        JsonArray trace = [];
        string first = targets[0]!["target_fingerprint_sha256"]!.GetValue<string>();
        trace.Add(Trace(1, "CredReadW", first, "success", 17, null));
        trace.Add(Trace(2, "CredFree", first, "released", null, 17));
        trace.Add(Trace(3, "CredDeleteW", first, "success", null, null));
        trace.Add(Trace(4, "CredReadW", first, "ERROR_NOT_FOUND", null, null));
        long sequence = 5;
        foreach (JsonNode? target in targets)
        {
            string alias = target!["alias"]!.GetValue<string>();
            string fingerprint = target["target_fingerprint_sha256"]!.GetValue<string>();
            absence.Add(new JsonObject
            {
                ["alias"] = alias,
                ["target_fingerprint_sha256"] = fingerprint,
                ["result"] = "ERROR_NOT_FOUND",
            });
            if (fingerprint != first)
            {
                trace.Add(Trace(sequence++, "CredReadW", fingerprint, "ERROR_NOT_FOUND", null, null));
            }
        }
        JsonObject evidence = new()
        {
            ["schema"] = "infinium.m1-s6.wp4.credential-native-recovery-evidence/v1",
            ["status"] = "passed",
            ["manifest_id"] = id,
            ["manifest_sha256"] = sha,
            ["target_absence"] = absence,
            ["native_call_counts"] = new JsonObject
            {
                ["cred_write_w"] = 0,
                ["cred_read_w"] = 13,
                ["cred_delete_w"] = 1,
                ["cred_free"] = 1,
                ["total"] = 15,
            },
            ["native_call_trace"] = trace,
            ["namespace_blocked"] = false,
            ["network_operations"] = 0,
            ["dns_operations"] = 0,
            ["provider_operations"] = 0,
            ["billable_operations"] = 0,
        };
        AssertEvidenceValidation(root, manifestPath, sha, id, evidence, expectedSuccess: true);
        AssertReceiptReconstruction(root, manifestPath, sha, id, evidence);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void RecoveryStoreRequiresSchemaSpecificExactTargetCount()
    {
        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        JsonObject legacy = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "docs", "plans", "milestones", "m1",
            "slices", "s6", "wp4-credential-native-recovery.v1.json")))!.AsObject();
        using (JsonDocument validLegacy = JsonDocument.Parse(legacy.ToJsonString()))
        using (WindowsCredentialManagerStore store = WindowsCredentialManagerStore.FromRecoveryManifest(validLegacy.RootElement))
        {
            Assert.AreEqual(12, store.ManifestTargets.Count);
        }
        legacy["disposable_namespace"]!["targets"]!.AsArray().RemoveAt(0);
        using JsonDocument shortLegacy = JsonDocument.Parse(legacy.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() =>
            WindowsCredentialManagerStore.FromRecoveryManifest(shortLegacy.RootElement));

        JsonObject current = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "docs", "plans", "milestones", "m1",
            "slices", "s6", "wp4-credential-native-recovery.ad876b9a.v1.json")))!.AsObject();
        using (JsonDocument validCurrent = JsonDocument.Parse(current.ToJsonString()))
        using (WindowsCredentialManagerStore store = WindowsCredentialManagerStore.FromRecoveryManifest(validCurrent.RootElement))
        {
            Assert.AreEqual(2, store.ManifestTargets.Count);
        }
        JsonObject mutatedAlias = current.DeepClone().AsObject();
        mutatedAlias["disposable_namespace"]!["targets"]![0]!["alias"] = "fake-dispatch";
        using JsonDocument badAlias = JsonDocument.Parse(mutatedAlias.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() =>
            WindowsCredentialManagerStore.FromRecoveryManifest(badAlias.RootElement));
        JsonObject swapped = current.DeepClone().AsObject();
        JsonArray swappedTargets = swapped["disposable_namespace"]!["targets"]!.AsArray();
        JsonNode first = swappedTargets[0]!.DeepClone();
        swappedTargets[0] = swappedTargets[1]!.DeepClone();
        swappedTargets[1] = first;
        using JsonDocument badOrder = JsonDocument.Parse(swapped.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() =>
            WindowsCredentialManagerStore.FromRecoveryManifest(badOrder.RootElement));
        current["disposable_namespace"]!["targets"]!.AsArray().Add(
            current["disposable_namespace"]!["targets"]![0]!.DeepClone());
        using JsonDocument longCurrent = JsonDocument.Parse(current.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() =>
            WindowsCredentialManagerStore.FromRecoveryManifest(longCurrent.RootElement));

        JsonObject e3 = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "docs", "plans", "milestones", "m1",
            "slices", "s6", "wp4-credential-native-recovery.e3f76cd6.v1.json")))!.AsObject();
        using (JsonDocument validE3 = JsonDocument.Parse(e3.ToJsonString()))
        using (WindowsCredentialManagerStore store = WindowsCredentialManagerStore.FromRecoveryManifest(validE3.RootElement))
        {
            Assert.AreEqual(2, store.ManifestTargets.Count);
        }
        JsonObject mutatedE3 = e3.DeepClone().AsObject();
        mutatedE3["disposable_namespace"]!["targets"]![0]!["generation_id"] = "g003";
        using JsonDocument badE3Target = JsonDocument.Parse(mutatedE3.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() =>
            WindowsCredentialManagerStore.FromRecoveryManifest(badE3Target.RootElement));
        Assert.IsTrue(WindowsCredentialNativeRecovery.IsAcceptedSchemaIdentityForTest(
            "infinium.repository.wp4-credential-native-recovery/1.2.0"));
        JsonObject e6 = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "docs", "plans", "milestones", "m1",
            "slices", "s6", "wp4-credential-native-recovery.e6e04651.v1.json")))!.AsObject();
        using (JsonDocument validE6 = JsonDocument.Parse(e6.ToJsonString()))
        using (WindowsCredentialManagerStore store = WindowsCredentialManagerStore.FromRecoveryManifest(validE6.RootElement))
        {
            Assert.AreEqual(12, store.ManifestTargets.Count);
            CollectionAssert.AreEqual(
                e6["disposable_namespace"]!["targets"]!.AsArray()
                    .Select(item => item!["alias"]!.GetValue<string>()).ToArray(),
                store.ManifestTargets.Select(item => item.Alias).ToArray());
        }
        JsonObject mutatedE6 = e6.DeepClone().AsObject();
        mutatedE6["disposable_namespace"]!["targets"]![11]!["access_profile_id"] = "mutated";
        using JsonDocument badE6Target = JsonDocument.Parse(mutatedE6.ToJsonString());
        Assert.ThrowsExactly<InvalidDataException>(() =>
            WindowsCredentialManagerStore.FromRecoveryManifest(badE6Target.RootElement));
        Assert.IsTrue(WindowsCredentialNativeRecovery.IsAcceptedSchemaIdentityForTest(
            "infinium.repository.wp4-credential-native-recovery/1.3.0"));
        Assert.IsFalse(WindowsCredentialNativeRecovery.IsAcceptedSchemaIdentityForTest(
            "infinium.repository.wp4-credential-native-recovery/1.4.0"));
    }

    private static JsonObject Trace(long sequence, string operation, string fingerprint, string result,
        long? allocationId, long? pairedAllocationId) => new()
        {
            ["sequence"] = sequence,
            ["operation"] = operation,
            ["target_fingerprint_sha256"] = fingerprint,
            ["scenario"] = "cleanup-only-recovery",
            ["result"] = result,
            ["allocation_id"] = allocationId,
            ["paired_allocation_id"] = pairedAllocationId,
        };

    private static void AssertEvidenceValidation(string root, string manifestPath, string sha, string id,
        JsonObject evidence, bool expectedSuccess)
    {
        string tempRoot = Path.Combine(root, "artifacts", "test-temp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string evidencePath = Path.Combine(tempRoot, "evidence.json");
        try
        {
            File.WriteAllText(evidencePath, evidence.ToJsonString());
            int exitCode = RunPwsh(root, "eng/validate-m1-slice6-wp4-recovery-evidence.ps1",
                "-ManifestPath", manifestPath, "-ManifestSha256", sha, "-ManifestId", id,
                "-EvidencePath", evidencePath);
            Assert.AreEqual(expectedSuccess ? 0 : 1, exitCode);
        }
        finally { Directory.Delete(tempRoot, recursive: true); }
    }

    private static void AssertManifestValidation(string root, JsonObject manifest, bool expectedSuccess)
    {
        string tempRoot = Path.Combine(root, "artifacts", "test-temp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string manifestPath = Path.Combine(tempRoot, "manifest.json");
        try
        {
            File.WriteAllText(manifestPath, manifest.ToJsonString());
            string relative = Path.GetRelativePath(root, manifestPath);
            int exitCode = RunPwsh(root, "eng/validate-m1-slice6-wp4-recovery-ad876b9a.ps1", "-ManifestPath", relative);
            Assert.AreEqual(expectedSuccess ? 0 : 1, exitCode);
        }
        finally { Directory.Delete(tempRoot, recursive: true); }
    }

    private static void AssertCurrentReceiptReconstruction(string root, string manifestPath, string sha, string id,
        JsonObject evidence)
    {
        string tempRoot = Path.Combine(root, "artifacts", "test-temp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string evidencePath = Path.Combine(tempRoot, "evidence.json");
        string lockPath = Path.Combine(tempRoot, "lock.json");
        string priorPath = Path.Combine(tempRoot, "prior.json");
        string priorLockPath = Path.Combine(tempRoot, "prior-lock.json");
        string receiptPath = Path.Combine(tempRoot, "receipt.json");
        bool e3 = id.EndsWith("8b7fc811-7cd2-4c2a-abe1-506bd7b06bf5", StringComparison.Ordinal);
        string[] priorFingerprints = e3
            ?
            [
                "15c6a64197a9f08b8aa5fc10eed45fe9563725a2465c4002e693dce69c450810",
                "270e254bff7a1260695442baf6153b33b90b9ed859669876270457c5b8cc1156",
                "53b86f277ff1c14712f85a3186d459b3acbad7203d90ed1a8f5befde9fa2daa0",
                "587dd7307f3d2bd1b8f33fe046f025c153830345a7d04585f8cc9f5288a37c3d",
                "6da865def7c09aa0071458f032d0c130bf5df27a220fc31389d7cb8b2c38a4fa",
                "7274e6756febbae814af24bd3bdcd30c35fc400fe1c58a538372a53537428302",
                "b9b633443a704d639ac8913201513279faf220b2cfec2af1d38edb5662bcf73d",
                "c4ad8d33d6910ba692c8bdbff9b174c59069e238edb1b6c06beb2cfda91b92ae",
                "d292d66ac2c24cc17eb11cf3e5cc28b5ad7b3646024c933c73c7acd4ba6770d6",
                "e2927d0ea4f59b25b5caaa6b802b85dbecccd978fc320755aaf80d393f8310ce",
            ]
            :
            [
                "cf749639000f855451374b935af7cc66b3895856d77868a87d12a52bbcaa8fe7",
                "ade2fbfd10c41f22382c11f58e5f23e89c92e43b6eabd686e24e5f3d3aa32096",
                "76fc18abd12bf7dfcc496602f82fe96e579912cac7875c0ddbeab18828401ac6",
                "befecf6ffdf669836062df69f078f54f94b4bf81ca4dcdcd1d28a4463694a422",
                "2025f07cf9eff90bd87cb680be019eb11529a05f98a29459bcc7e72f8fc4b44f",
                "43e9a481fec663f10eef5753b41074b201bcc06c3edb07174153525201521078",
                "d1999c5fca496d9cd417c5f686278564e8028ab8c5db9e91d81790c8aee7ce07",
                "6190a95dc664166e75f57fc39d57ec1eba8643e7865ae73789589ac732f8bc5c",
                "598ae3c4a89d3ec7e72dfc11b6763120955a3fc946ea7425248f4e445558dea0",
                "0a6d4ba3eed8c60a4048ca388178d2700ede8e1835b0ebca99ecb6ef50b6c051",
            ];
        JsonObject prior = new()
        {
            ["status"] = "failed-primary-cleanup-confirmed",
            ["manifest_id"] = e3
                ? "infinium.m1-s6.wp4.credential-native-authorization/e3f76cd6-45c1-4e3a-a84b-fa3251b3cb60"
                : "infinium.m1-s6.wp4.credential-native-authorization/ad876b9a-9f45-4eb4-8d12-5970d76dd4ea",
            ["cleanup_confirmed"] = true,
            ["absence_confirmed"] = true,
            ["whole_namespace_absence_confirmed"] = false,
            ["namespace_disposition"] = "consumed-never-reuse",
            ["later_native_calls"] = 0,
            ["absence_target_fingerprints"] = new JsonArray(
                priorFingerprints.Select(item => (JsonNode?)JsonValue.Create(item)).ToArray()),
        };
        try
        {
            File.WriteAllText(evidencePath, evidence.ToJsonString());
            File.WriteAllText(lockPath, JsonSerializer.Serialize(new
            {
                manifest_id = id,
                manifest_sha256 = sha,
                disposition = "consumed-never-reuse",
            }) + "\n");
            File.WriteAllText(priorPath, prior.ToJsonString());
            File.WriteAllText(priorLockPath, "{\"disposition\":\"consumed-never-reuse\"}\n");
            string evidenceSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(evidencePath)));
            string lockSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(lockPath)));
            string priorSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(priorPath)));
            string priorLockSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(priorLockPath)));
            string[] Arguments(string targetReceipt, string targetPrior = "") =>
            [
                e3 ? "eng/reconstruct-m1-slice6-wp4-recovery-e3f76cd6-receipt.ps1"
                    : "eng/reconstruct-m1-slice6-wp4-recovery-ad876b9a-receipt.ps1",
                "-ManifestPath", manifestPath, "-ManifestSha256", sha, "-ManifestId", id,
                "-EvidencePath", evidencePath, "-EvidenceSha256", evidenceSha,
                "-AuthorityLockPath", lockPath, "-AuthorityLockSha256", lockSha,
                "-PriorEvidencePath", targetPrior.Length == 0 ? priorPath : targetPrior,
                "-PriorEvidenceSha256", priorSha, "-PriorAuthorityLockPath", priorLockPath,
                "-PriorAuthorityLockSha256", priorLockSha, "-ReceiptPath", targetReceipt, "-TestOnlyPaths",
            ];
            Assert.AreEqual(0, RunPwsh(root, Arguments(receiptPath).Append("-ReportFailureOutput").ToArray()));
            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(receiptPath));
            JsonElement receiptEvidence = receipt.RootElement.GetProperty("evidence");
            Assert.AreEqual(2, receiptEvidence.GetProperty("recovery_target_absence_count").GetInt32());
            Assert.AreEqual(10, receiptEvidence.GetProperty("prior_exact_absence_count").GetInt32());
            Assert.AreEqual(12, receiptEvidence.GetProperty("combined_namespace_target_absence_count").GetInt32());
            Assert.AreEqual("cleanup-confirmed-absent-consumed-never-reuse",
                receiptEvidence.GetProperty("namespace_disposition").GetString());
            Assert.AreEqual(1, RunPwsh(root, Arguments(receiptPath)), "CreateNew must refuse overwrite.");
            Assert.AreEqual(1, RunPwsh(root, Arguments(Path.Combine(tempRoot, "missing-receipt.json"),
                Path.Combine(tempRoot, "missing-prior.json"))));
            prior["absence_target_fingerprints"]!.AsArray().RemoveAt(0);
            File.WriteAllText(priorPath, prior.ToJsonString());
            priorSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(priorPath)));
            Assert.AreEqual(1, RunPwsh(root, Arguments(Path.Combine(tempRoot, "bad-prior-receipt.json"))));
        }
        finally { Directory.Delete(tempRoot, recursive: true); }
    }

    private static void AssertE6ReceiptReconstruction(string root, string manifestPath, string sha, string id,
        JsonObject evidence)
    {
        string tempRoot = Path.Combine(root, "artifacts", "test-temp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string evidencePath = Path.Combine(tempRoot, "evidence.json");
        string lockPath = Path.Combine(tempRoot, "lock.json");
        string priorPath = Path.Combine(tempRoot, "prior.json");
        string priorLockPath = Path.Combine(tempRoot, "prior-lock.json");
        string receiptPath = Path.Combine(tempRoot, "receipt.json");
        JsonObject prior = new()
        {
            ["status"] = "failed-cleanup-ambiguous",
            ["manifest_id"] = "infinium.m1-s6.wp4.credential-native-authorization/e6e04651-4cd5-4f5d-8b46-5ec84a81cbbe",
            ["cleanup_confirmed"] = false,
            ["whole_namespace_absence_confirmed"] = false,
            ["namespace_blocked"] = true,
            ["namespace_disposition"] = "consumed-never-reuse",
            ["later_native_calls"] = 0,
            ["assignment_id"] = "wp4-v2/backup-restore-reauthentication/cleanup-successor",
            ["reason"] = "cleanup-phase-failed",
        };
        try
        {
            File.WriteAllText(evidencePath, evidence.ToJsonString());
            File.WriteAllText(lockPath, JsonSerializer.Serialize(new
            {
                manifest_id = id,
                manifest_sha256 = sha,
                disposition = "consumed-never-reuse",
            }) + "\n");
            File.WriteAllText(priorPath, prior.ToJsonString());
            File.WriteAllText(priorLockPath, "{\"disposition\":\"consumed-before-native-launch-never-delete-or-reuse\"}\n");
            string evidenceSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(evidencePath)));
            string lockSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(lockPath)));
            string priorSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(priorPath)));
            string priorLockSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(priorLockPath)));
            string[] Arguments(string targetReceipt) =>
            [
                "eng/reconstruct-m1-slice6-wp4-recovery-e6e04651-receipt.ps1",
                "-ManifestPath", manifestPath, "-ManifestSha256", sha, "-ManifestId", id,
                "-EvidencePath", evidencePath, "-EvidenceSha256", evidenceSha,
                "-AuthorityLockPath", lockPath, "-AuthorityLockSha256", lockSha,
                "-PriorEvidencePath", priorPath, "-PriorEvidenceSha256", priorSha,
                "-PriorAuthorityLockPath", priorLockPath, "-PriorAuthorityLockSha256", priorLockSha,
                "-ReceiptPath", targetReceipt, "-TestOnlyPaths",
            ];
            Assert.AreEqual(0, RunPwsh(root, Arguments(receiptPath).Append("-ReportFailureOutput").ToArray()));
            using JsonDocument receipt = JsonDocument.Parse(File.ReadAllBytes(receiptPath));
            JsonElement receiptEvidence = receipt.RootElement.GetProperty("evidence");
            Assert.AreEqual(12, receiptEvidence.GetProperty("recovery_target_absence_count").GetInt32());
            Assert.AreEqual(0, receiptEvidence.GetProperty("prior_exact_absence_count").GetInt32());
            Assert.AreEqual(12, receiptEvidence.GetProperty("combined_namespace_target_absence_count").GetInt32());
            Assert.AreEqual("cleanup-confirmed-absent-consumed-never-reuse",
                receiptEvidence.GetProperty("namespace_disposition").GetString());
            Assert.AreEqual(1, RunPwsh(root, Arguments(receiptPath)), "CreateNew must refuse overwrite.");
            prior["reason"] = "mutated";
            File.WriteAllText(priorPath, prior.ToJsonString());
            priorSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(priorPath)));
            Assert.AreEqual(1, RunPwsh(root, Arguments(Path.Combine(tempRoot, "bad-prior-receipt.json"))));
        }
        finally { Directory.Delete(tempRoot, recursive: true); }
    }

    private static void AssertReceiptReconstruction(string root, string manifestPath, string sha, string id,
        JsonObject evidence)
    {
        string tempRoot = Path.Combine(root, "artifacts", "test-temp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string evidencePath = Path.Combine(tempRoot, "evidence.json");
        string lockPath = Path.Combine(tempRoot, "lock.json");
        string receiptPath = Path.Combine(tempRoot, "receipt.json");
        try
        {
            File.WriteAllText(evidencePath, evidence.ToJsonString());
            File.WriteAllText(lockPath, JsonSerializer.Serialize(new
            {
                manifest_id = id,
                manifest_sha256 = sha,
                disposition = "consumed-never-reuse",
            }) + "\n");
            string evidenceSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(evidencePath)));
            string lockSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(lockPath)));
            int exitCode = RunPwsh(root, "eng/reconstruct-m1-slice6-wp4-recovery-receipt.ps1",
                "-ManifestPath", manifestPath, "-ManifestSha256", sha, "-ManifestId", id,
                "-EvidencePath", evidencePath, "-EvidenceSha256", evidenceSha,
                "-AuthorityLockPath", lockPath, "-AuthorityLockSha256", lockSha,
                "-ReceiptPath", receiptPath, "-TestOnlyPaths", "-ReportFailureOutput");
            Assert.AreEqual(0, exitCode);
            string expected = "{\"credential_access_permitted\":true,\"evidence\":{"
                + $"\"authority_lock_sha256\":\"{lockSha}\",\"billable_operations\":0,"
                + $"\"dns_operations\":0,\"evidence_sha256\":\"{evidenceSha}\","
                + $"\"manifest_id\":\"{id}\",\"manifest_sha256\":\"{sha}\","
                + "\"namespace_disposition\":\"cleanup-confirmed-absent-consumed-never-reuse\","
                + "\"native_call_counts\":{\"cred_delete_w\":1,\"cred_free\":1,\"cred_read_w\":13,"
                + "\"cred_write_w\":0,\"total\":15},"
                + "\"network_operations\":0,\"provider_operations\":0,"
                + "\"receipt_origin\":\"post-effect-reconstruction-from-immutable-evidence-no-native-retry\","
                + "\"target_absence_count\":12},\"gate\":\"CredentialNativeRecovery\","
                + "\"network_permitted\":false,\"status\":\"passed\"}\n";
            Assert.AreEqual(expected, File.ReadAllText(receiptPath));
            Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(expected))),
                Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(receiptPath))));
            Assert.AreEqual(1, RunPwsh(root, "eng/reconstruct-m1-slice6-wp4-recovery-receipt.ps1",
                "-ManifestPath", manifestPath, "-ManifestSha256", sha, "-ManifestId", id,
                "-EvidencePath", evidencePath, "-EvidenceSha256", evidenceSha,
                "-AuthorityLockPath", lockPath, "-AuthorityLockSha256", lockSha,
                "-ReceiptPath", receiptPath, "-TestOnlyPaths"), "CreateNew must refuse receipt overwrite.");
            File.Delete(receiptPath);
            Assert.AreEqual(1, RunPwsh(root, "eng/reconstruct-m1-slice6-wp4-recovery-receipt.ps1",
                "-ManifestPath", manifestPath, "-ManifestSha256", sha, "-ManifestId", id,
                "-EvidencePath", evidencePath, "-EvidenceSha256", evidenceSha,
                "-AuthorityLockPath", lockPath, "-AuthorityLockSha256", lockSha,
                "-ReceiptPath", receiptPath), "Production mode must refuse arbitrary paths.");
        }
        finally { Directory.Delete(tempRoot, recursive: true); }
    }

    private static int RunPwsh(string root, params string[] arguments)
    {
        bool reportFailure = arguments.Length > 0 && arguments[^1] == "-ReportFailureOutput";
        if (reportFailure) { arguments = arguments[..^1]; }
        ProcessStartInfo start = new("pwsh.exe")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start)!;
        if (!process.WaitForExit(15_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            Assert.Fail("Recovery validator process exceeded its safe deadline and was terminated.");
        }
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        if (reportFailure && process.ExitCode != 0)
        {
            Assert.Fail($"Recovery reconstruction failed. stdout={standardOutput} stderr={standardError}");
        }
        return process.ExitCode;
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
        NativeEntryReadinessEvidence readiness = ValidReadiness();
        state.RecordReadiness(readiness);
        state.Activate(0);
        state.RecordClipboardMessageBlocked();
        state.RecordActionReadiness(readiness, NativeEntryInteraction.Submit, NativeEntryActionSource.EditKey);
        state.Submit("manually-entered"u8);
        state.CompleteUiThreadCleanup(windowWasDestroyed: true, buffersWereCleared: true);
        state.RecordThreadJoined();
        Assert.AreEqual(new NativeEntryCleanupEvidence(
            true, true, true, true, true, true, readiness, readiness, "editkey", "submit"), state.Evidence);

        foreach (Action<NativeEntryStateMachine> terminal in new Action<NativeEntryStateMachine>[]
        {
            item => item.Cancel(), item => item.Timeout(), item => item.Fail(),
        })
        {
            NativeEntryStateMachine candidate = new();
            candidate.RecordReadiness(readiness);
            candidate.Activate(0);
            candidate.RecordClipboardMessageBlocked();
            candidate.RecordActionReadiness(readiness, NativeEntryInteraction.Cancel, NativeEntryActionSource.EditKey);
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
    public void NativeEntryReadinessOracleRejectsEveryInvisibleOrUnactionableMutation()
    {
        NativeEntryReadinessEvidence valid = ValidReadiness();
        NativeEntryReadinessOracle.Validate(valid);
        NativeEntryReadinessEvidence[] mutations =
        [
            valid with { OwnerProcessId = valid.OwnerProcessId + 1 },
            valid with { OwnerThreadId = 0 },
            valid with { OwnerSessionId = valid.OwnerSessionId + 1 },
            valid with { DesktopNameSha256 = "bad" },
            valid with { InteractiveInputDesktop = false },
            valid with { DesktopObjectMatches = false },
            valid with { OwnerProcessMatches = false },
            valid with { OwnerThreadMatches = false },
            valid with { TopLevelWindow = false },
            valid with { WindowVisible = false },
            valid with { WindowEnabled = false },
            valid with { WindowNotCloaked = false },
            valid with { WindowIntersectsActiveMonitor = false },
            valid with { InstructionOwned = false },
            valid with { InstructionVisible = false },
            valid with { InstructionMode = "wrong" },
            valid with { InstructionFingerprintSha256 = "bad" },
            valid with { EditOwned = false },
            valid with { EditVisible = false },
            valid with { EditEnabled = false },
            valid with { EditMasked = false },
            valid with { SubmitOwned = false },
            valid with { SubmitVisible = false },
            valid with { SubmitEnabled = false },
            valid with { CancelOwned = false },
            valid with { CancelVisible = false },
            valid with { CancelEnabled = false },
            valid with { Foreground = false },
            valid with { EditFocused = false },
            valid with { ReadinessDeadlineMilliseconds = 0 },
            valid with { ReadinessElapsedMilliseconds = valid.ReadinessDeadlineMilliseconds + 1 },
        ];
        foreach (NativeEntryReadinessEvidence mutation in mutations)
        {
            Assert.ThrowsExactly<InvalidDataException>(() => NativeEntryReadinessOracle.Validate(mutation));
        }

        NativeEntryStateMachine state = new();
        Assert.ThrowsExactly<InvalidOperationException>(() => state.Activate(0));

        NativeEntryStateMachine setupFailure = new();
        setupFailure.RecordSetupFailureIfNeeded(NativeEntryReadinessEvidence.SetupFailure(
            threadId: 0, NativeEntryInteraction.Submit,
            windowCreated: false, instructionCreated: false, editCreated: false,
            submitCreated: false, cancelCreated: false));
        setupFailure.FailFromAnyState();
        setupFailure.CompleteUiThreadCleanup(true, true);
        setupFailure.RecordThreadJoined();
        Assert.IsNotNull(setupFailure.Evidence.Readiness);
        Assert.IsFalse(setupFailure.Evidence.Readiness.WindowVisible);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void NativeEntryCommandsRequireExactOwnedButtonAndRegisteredInstance()
    {
        nint window = 100;
        nint submit = 101;
        nint cancel = 102;
        nuint submitCommand = 1001;
        Assert.IsTrue(NativeMaskedEntryDialog.IsOwnedButtonCommand(
            0x0111, window, submitCommand, submit, window, submit, cancel, out NativeEntryInteraction action));
        Assert.AreEqual(NativeEntryInteraction.Submit, action);
        Assert.IsFalse(NativeMaskedEntryDialog.IsOwnedButtonCommand(
            0x0111, window, submitCommand, cancel, window, submit, cancel, out _));
        Assert.IsFalse(NativeMaskedEntryDialog.IsOwnedButtonCommand(
            0x0111, window + 1, submitCommand, submit, window, submit, cancel, out _));
        Assert.IsFalse(NativeMaskedEntryDialog.IsOwnedButtonCommand(
            0x0111, window, submitCommand | ((nuint)1 << 16), submit,
            window, submit, cancel, out _));
        NativeMaskedEntryDialog.RequireMatchingWindowClassInstance(123, 123);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            NativeMaskedEntryDialog.RequireMatchingWindowClassInstance(123, 0));

        NativeEntryReadinessEvidence button = ValidReadiness() with
        {
            EditFocused = false,
            SubmitFocused = true,
        };
        NativeEntryStateMachine buttonState = new();
        buttonState.RecordReadiness(ValidReadiness());
        buttonState.Activate(0);
        buttonState.RecordActionReadiness(button, NativeEntryInteraction.Submit, NativeEntryActionSource.SubmitButton);
        Assert.AreEqual("submitbutton", buttonState.Evidence.ActionSource);
        Assert.ThrowsExactly<InvalidDataException>(() =>
        {
            NativeEntryStateMachine wrongFocus = new();
            wrongFocus.RecordReadiness(ValidReadiness());
            wrongFocus.Activate(0);
            wrongFocus.RecordActionReadiness(
                button with { SubmitFocused = false }, NativeEntryInteraction.Submit,
                NativeEntryActionSource.SubmitButton);
        });
        Assert.ThrowsExactly<InvalidDataException>(() =>
            NativeEntryReadinessOracle.Validate(
                ValidReadiness() with { ReadinessDeadlineMilliseconds = 0 }));

        NativeEntryInteraction? selectedAction = null;
        NativeEntryActionSource? selectedSource = null;
        NativeMaskedEntryDialog.RecordFirstTerminalAction(
            ref selectedAction, ref selectedSource,
            NativeEntryInteraction.Cancel, NativeEntryActionSource.CancelButton);
        NativeMaskedEntryDialog.RecordFirstTerminalAction(
            ref selectedAction, ref selectedSource,
            NativeEntryInteraction.Submit, NativeEntryActionSource.SubmitButton);
        Assert.AreEqual(NativeEntryInteraction.Cancel, selectedAction);
        Assert.AreEqual(NativeEntryActionSource.CancelButton, selectedSource);

        selectedAction = null;
        selectedSource = null;
        NativeMaskedEntryDialog.RecordFirstTerminalAction(
            ref selectedAction, ref selectedSource,
            NativeEntryInteraction.Submit, NativeEntryActionSource.EditKey);
        NativeMaskedEntryDialog.RecordFirstTerminalAction(
            ref selectedAction, ref selectedSource,
            NativeEntryInteraction.Cancel, NativeEntryActionSource.EditKey);
        Assert.AreEqual(NativeEntryInteraction.Submit, selectedAction);
        Assert.AreEqual(NativeEntryActionSource.EditKey, selectedSource);
        Assert.IsTrue(NativeMaskedEntryDialog.ShouldStopMessageDrain(selectedAction));
        int readinessMeasurements = 0;
        foreach (NativeEntryInteraction queued in new[]
        {
            NativeEntryInteraction.Cancel,
            NativeEntryInteraction.Submit,
        })
        {
            NativeEntryInteraction? first = null;
            NativeEntryActionSource? firstSource = null;
            NativeMaskedEntryDialog.RecordFirstTerminalAction(
                ref first, ref firstSource, queued, NativeEntryActionSource.EditKey);
            if (NativeMaskedEntryDialog.ShouldStopMessageDrain(first))
            {
                readinessMeasurements++;
                break;
            }
        }
        Assert.AreEqual(1, readinessMeasurements);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void PreReadinessPumpAdmitsActivationButNeverTerminalOrEditContentInput()
    {
        nint window = 100;
        nint edit = 101;
        nint submit = 102;
        nint cancel = 103;

        Assert.IsFalse(NativeMaskedEntryDialog.IsPreReadinessTerminalMessage(
            0x0006, window, 0, 0, window, edit, submit, cancel));
        Assert.IsTrue(NativeMaskedEntryDialog.IsPreReadinessTerminalMessage(
            0x0111, window, 1001, submit, window, edit, submit, cancel));
        Assert.IsTrue(NativeMaskedEntryDialog.IsPreReadinessTerminalMessage(
            0x0100, edit, 0x0D, 0, window, edit, submit, cancel));
        Assert.IsTrue(NativeMaskedEntryDialog.IsPreReadinessEditContentMessage(0x0102, edit, edit));
        Assert.IsTrue(NativeMaskedEntryDialog.IsPreReadinessEditContentMessage(0x0100, edit, edit));
        Assert.IsFalse(NativeMaskedEntryDialog.IsPreReadinessEditContentMessage(0x000F, window, edit));

        nint instruction = 104;
        Assert.AreEqual(PreReadinessMessageDisposition.DispatchOne,
            NativeMaskedEntryDialog.ClassifyPreReadinessMessage(
                0x0006, window, 0, 0, window, instruction, edit, submit, cancel));
        Assert.AreEqual(PreReadinessMessageDisposition.IgnoreUnowned,
            NativeMaskedEntryDialog.ClassifyPreReadinessMessage(
                0x000F, 999, 0, 0, window, instruction, edit, submit, cancel));
        Assert.AreEqual(PreReadinessMessageDisposition.RejectEditContent,
            NativeMaskedEntryDialog.ClassifyPreReadinessMessage(
                0x0102, edit, (nuint)'x', 0, window, instruction, edit, submit, cancel));
        Assert.AreEqual(PreReadinessMessageDisposition.RejectTerminal,
            NativeMaskedEntryDialog.ClassifyPreReadinessMessage(
                0x0111, window, 1001, submit, window, instruction, edit, submit, cancel));

        PreReadinessMessageDisposition[] queued =
        [
            PreReadinessMessageDisposition.DispatchOne,
            PreReadinessMessageDisposition.RejectEditContent,
            PreReadinessMessageDisposition.RejectTerminal,
        ];
        int processedBeforeRemeasurement = 0;
        foreach (PreReadinessMessageDisposition _ in queued)
        {
            processedBeforeRemeasurement++;
            break;
        }
        Assert.AreEqual(1, processedBeforeRemeasurement,
            "Readiness must be remeasured after exactly one queued message.");

        NativeEntryStateMachine state = new();
        state.RecordPreReadinessTerminalMessage();
        state.RecordPreReadinessIgnoredMessage();
        state.RecordReadiness(ValidReadiness() with
        {
            ReadinessDeadlineMilliseconds = checked((long)NativeMaskedEntryDialog.ReadinessDeadline.TotalMilliseconds),
        });
        state.Activate(0);
        Assert.AreEqual(1, state.Evidence.PreReadinessTerminalMessages);
        Assert.AreEqual(1, state.Evidence.PreReadinessIgnoredMessages);
        Assert.IsTrue(state.Evidence.InitialBlank);
        Assert.AreEqual(TimeSpan.FromSeconds(10), NativeMaskedEntryDialog.ReadinessDeadline);
        Assert.ThrowsExactly<InvalidOperationException>(() => state.RecordPreReadinessTerminalMessage());

        NativeEntryStateMachine failed = new();
        failed.RecordPreReadinessTerminalMessages(2, requireActive: false);
        failed.RecordFailedReadiness(ValidReadiness() with { Foreground = false });
        failed.FailFromAnyState();
        failed.CompleteUiThreadCleanup(true, true);
        failed.RecordThreadJoined();
        Assert.AreEqual(2, failed.Evidence.PreReadinessTerminalMessages);
        Assert.IsNull(failed.Evidence.Action);
        Assert.IsFalse(failed.Evidence.InitialBlank);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void OwnedWindowProcedureRoutesExactButtonCommandsAfterReadinessFirstWins()
    {
        nint window = 100;
        nint submit = 101;
        nint cancel = 102;
        NativeEntryWindowCommandRouter router = new();

        Assert.IsFalse(router.Route(0x000F, window, 0, 0, window, submit, cancel));
        Assert.IsTrue(router.Route(0x0111, window, 1001, submit, window, submit, cancel));
        Assert.AreEqual(1, router.PreReadinessRejectedCount);
        Assert.AreEqual(1, router.DrainPreReadinessRejectedCount());
        Assert.AreEqual(0, router.DrainPreReadinessRejectedCount());
        Assert.IsFalse(router.TryTake(out _), "A pre-readiness click must never carry into active entry.");

        router.Enable();
        Assert.IsTrue(router.Route(0x0111, window, 1002, cancel, window, submit, cancel));
        Assert.IsTrue(router.Route(0x0111, window, 1001, submit, window, submit, cancel));
        Assert.IsTrue(router.TryTake(out NativeEntryInteraction first));
        Assert.AreEqual(NativeEntryInteraction.Cancel, first);
        Assert.IsFalse(router.TryTake(out _));

        Assert.IsFalse(router.Route(0x0111, window + 1, 1001, submit, window, submit, cancel));
        Assert.IsFalse(router.Route(0x0111, window, 1001, cancel, window, submit, cancel));
        Assert.IsFalse(router.Route(0x0111, window, 1001 | ((nuint)1 << 16), submit, window, submit, cancel));

        NativeEntryWindowCommandRouter ordered = new();
        ordered.Enable();
        Assert.IsTrue(ordered.Route(0x0111, window, 1002, cancel, window, submit, cancel));
        NativeEntryInteraction? selectedAction = null;
        NativeEntryActionSource? selectedSource = null;
        Assert.IsTrue(ordered.TryTake(out NativeEntryInteraction routedBeforeQueued));
        NativeMaskedEntryDialog.RecordFirstTerminalAction(
            ref selectedAction, ref selectedSource, routedBeforeQueued, NativeEntryActionSource.CancelButton);
        NativeMaskedEntryDialog.RecordFirstTerminalAction(
            ref selectedAction, ref selectedSource, NativeEntryInteraction.Submit, NativeEntryActionSource.EditKey);
        Assert.AreEqual(NativeEntryInteraction.Cancel, selectedAction,
            "A sent button command observed during PeekMessage must win over the returned queued edit key.");
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

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public void NativeQualificationEntryTerminalActionsFailAsReceiptableOutcomesWithoutSecretRetention()
    {
        NativeEntryInteraction? observedInteraction = null;
        NativeEntryCleanupEvidence complete = new(true, true, true, true, true, true, ValidReadiness());
        NativeQualificationSecretSource cancelledSubmit = new(
            capture: (_, interaction) =>
            {
                observedInteraction = interaction;
                return new([], NativeEntryTerminalState.Cancelled, complete);
            });
        HelperAssignmentV2 submit = QualificationAssignment(
            "wp4-v2/interactive-entry-submit/submit", HelperAssignmentKindV2.Enroll);
        Assert.ThrowsExactly<OperationCanceledException>(() => cancelledSubmit.Capture(submit));
        Assert.AreEqual(NativeEntryInteraction.Submit, observedInteraction);
        Assert.AreEqual(complete, cancelledSubmit.EntryEvidence);

        observedInteraction = null;
        NativeQualificationSecretSource expectedCancel = new(
            capture: (_, interaction) =>
            {
                observedInteraction = interaction;
                return new([], NativeEntryTerminalState.Cancelled, complete);
            });
        HelperAssignmentV2 cancel = QualificationAssignment(
            "wp4-v2/interactive-entry-cancel/cancel", HelperAssignmentKindV2.Enroll);
        Assert.ThrowsExactly<OperationCanceledException>(() => expectedCancel.Capture(cancel));
        Assert.AreEqual(NativeEntryInteraction.Cancel, observedInteraction);

        byte[] wrongActionSecret = "must-be-cleared"u8.ToArray();
        NativeQualificationSecretSource wrongCancelAction = new(
            capture: (_, _) => new(wrongActionSecret, NativeEntryTerminalState.Submitted, complete));
        Assert.ThrowsExactly<InvalidDataException>(() => wrongCancelAction.Capture(cancel));
        Assert.IsTrue(wrongActionSecret.All(value => value == 0));

        NativeQualificationSecretSource timedOut = new(
            capture: (_, _) => new([], NativeEntryTerminalState.TimedOut, complete));
        Assert.ThrowsExactly<InvalidDataException>(() => timedOut.Capture(submit));

        NativeQualificationSecretSource cleanupFailed = new(
            capture: (_, _) => throw new InvalidOperationException("synthetic cleanup failure"));
        Assert.ThrowsExactly<InvalidDataException>(() => cleanupFailed.Capture(submit));

        byte[] submitted = "disposable-value"u8.ToArray();
        NativeQualificationSecretSource accepted = new(
            capture: (_, interaction) =>
            {
                Assert.AreEqual(NativeEntryInteraction.Submit, interaction);
                return new(submitted, NativeEntryTerminalState.Submitted, complete);
            });
        byte[] result = accepted.Capture(submit);
        CollectionAssert.AreEqual("disposable-value"u8.ToArray(), result);
        CryptographicOperations.ZeroMemory(result);

        string root = Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
        string program = File.ReadAllText(Path.Combine(root, "src", "Infinium.CredentialHelper", "Program.cs"));
        Assert.IsTrue(program.Contains("or InvalidOperationException", StringComparison.Ordinal));
        Assert.IsTrue(program.Contains("or OperationCanceledException or TimeoutException", StringComparison.Ordinal));
        string nativeSource = File.ReadAllText(Path.Combine(root, "src", "Infinium.CredentialHelper",
            "WindowsCredentialNativeQualification.cs"));
        Assert.IsTrue(nativeSource.Contains("click Submit", StringComparison.Ordinal));
        Assert.IsTrue(nativeSource.Contains("click Cancel", StringComparison.Ordinal));
        Assert.IsTrue(nativeSource.Contains("SubmitButtonId", StringComparison.Ordinal));
        Assert.IsTrue(nativeSource.Contains("CancelButtonId", StringComparison.Ordinal));
        Assert.IsFalse(nativeSource.Contains("GetAsyncKeyState", StringComparison.Ordinal));
        Assert.IsTrue(nativeSource.Contains("message.Window == edit", StringComparison.Ordinal));
        Assert.IsTrue(nativeSource.Contains("GetForegroundWindow() == window", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public async Task NativeEntryTerminalActionsProduceCanonicalEngineReceiptsWithoutNativeEffects()
    {
        NativeEntryCleanupEvidence complete = new(true, true, true, true, true, true, ValidReadiness());
        foreach ((string assignmentId, NativeEntryTerminalState terminal, HelperOutcomeV2 expected) in new[]
        {
            ("wp4-v2/interactive-entry-cancel/cancel", NativeEntryTerminalState.Cancelled, HelperOutcomeV2.Cancelled),
            ("wp4-v2/interactive-entry-submit/submit", NativeEntryTerminalState.Failed, HelperOutcomeV2.FailedKnown),
            ("wp4-v2/interactive-entry-submit/submit", NativeEntryTerminalState.TimedOut, HelperOutcomeV2.FailedKnown),
        })
        {
            using DeterministicFakeSecureStore store = new();
            using NativeQualificationSecretSource source = new(
                capture: (_, _) => new([], terminal, complete));
            OneShotHelperEngine engine = new(store, new FixedTestTimeProvider(), source);
            HelperPrivateFrameV2 bootstrap = HelperTestFrames.Bootstrap(nonceSeed: (byte)(10 + (int)terminal));
            HelperPrivateFrameV2 assignment = HelperTestFrames.Assignment();
            assignment.Assignment.AssignmentId = assignmentId;
            using MemoryStream request = new();
            await HelperPrivateProtocolV2.WriteAsync(request, bootstrap, CancellationToken.None);
            await HelperPrivateProtocolV2.WriteAsync(request, assignment, CancellationToken.None);
            request.Position = 0;
            using MemoryStream response = new();
            await engine.RunAsync(request, response, CancellationToken.None);
            response.Position = 0;
            HelperPrivateFrameV2 receipt = await HelperPrivateProtocolV2.ReadAsync(
                response, 3, CancellationToken.None);
            Assert.AreEqual(expected, receipt.Receipt.Outcome);
            Assert.AreEqual(complete, source.EntryEvidence);
            Assert.AreEqual(0, DeterministicFakeSecureStore.NativeOperationCount);
            Assert.AreEqual(0, DeterministicFakeSecureStore.EnumerationCount);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Security")]
    public async Task FailedUiReadinessRetainsLastFactsAndPerformsNoCredentialWrite()
    {
        NativeEntryReadinessEvidence failedReadiness = ValidReadiness() with
        {
            Foreground = false,
            EditFocused = false,
        };
        NativeEntryCleanupEvidence cleanup = new(
            InitialBlank: false,
            Terminal: true,
            WindowDestroyed: true,
            BuffersCleared: true,
            ThreadJoined: true,
            ClipboardMessagesBlocked: false,
            Readiness: failedReadiness);
        using DeterministicFakeSecureStore store = new();
        using NativeQualificationSecretSource source = new(
            capture: (_, _) => new([], NativeEntryTerminalState.Failed, cleanup));
        OneShotHelperEngine engine = new(store, new FixedTestTimeProvider(), source);
        HelperPrivateFrameV2 bootstrap = HelperTestFrames.Bootstrap(nonceSeed: 27);
        HelperPrivateFrameV2 assignment = HelperTestFrames.Assignment();
        assignment.Assignment.AssignmentId = "wp4-v2/interactive-entry-submit/submit";
        using MemoryStream request = new();
        await HelperPrivateProtocolV2.WriteAsync(request, bootstrap, CancellationToken.None);
        await HelperPrivateProtocolV2.WriteAsync(request, assignment, CancellationToken.None);
        request.Position = 0;
        using MemoryStream response = new();
        await engine.RunAsync(request, response, CancellationToken.None);
        response.Position = 0;
        HelperPrivateFrameV2 receipt = await HelperPrivateProtocolV2.ReadAsync(response, 3, CancellationToken.None);
        Assert.AreEqual(HelperOutcomeV2.FailedKnown, receipt.Receipt.Outcome);
        Assert.AreEqual(failedReadiness, source.EntryEvidence?.Readiness);
        Assert.AreEqual(0, DeterministicFakeSecureStore.NativeOperationCount);
        Assert.AreEqual(0, DeterministicFakeSecureStore.EnumerationCount);
    }

    private static HelperAssignmentV2 QualificationAssignment(string assignmentId, HelperAssignmentKindV2 kind) =>
        new() { AssignmentId = assignmentId, AssignmentKind = kind };

    private static NativeEntryReadinessEvidence ValidReadiness() => new(
        OwnerProcessId: Environment.ProcessId, OwnerThreadId: 1,
        OwnerSessionId: Process.GetCurrentProcess().SessionId,
        DesktopNameSha256: new string('a', 64), InteractiveInputDesktop: true,
        DesktopObjectMatches: true, OwnerProcessMatches: true, OwnerThreadMatches: true,
        TopLevelWindow: true, WindowVisible: true, WindowEnabled: true,
        WindowNotCloaked: true, WindowIntersectsActiveMonitor: true,
        InstructionOwned: true, InstructionVisible: true,
        InstructionMode: "submit",
        InstructionFingerprintSha256: Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(NativeMaskedEntryDialog.SubmitInstruction))),
        EditOwned: true, EditVisible: true, EditEnabled: true, EditMasked: true,
        SubmitOwned: true, SubmitVisible: true, SubmitEnabled: true,
        SubmitFocused: false,
        CancelOwned: true, CancelVisible: true, CancelEnabled: true,
        CancelFocused: false,
        Foreground: true, EditFocused: true,
        ReadinessDeadlineMilliseconds: 3_000, ReadinessElapsedMilliseconds: 10);

    private sealed class FixedTestTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 11, 12, 0, 1, TimeSpan.Zero);
    }
}
