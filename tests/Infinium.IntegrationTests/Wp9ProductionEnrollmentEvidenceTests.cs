using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Wp9ProductionEnrollmentEvidenceTests
{
    [TestMethod]
    public async Task ActualHelperProgramAndLauncherRetainTypedEofCrashAndTimeoutTerminals()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("Windows process containment is required."); }
        string root = RepositoryRoot();
        string helper = Path.Combine(root, "src", "Infinium.Coordinator", "bin", "Release", "net10.0",
            "CredentialHelper", "Infinium.CredentialHelper.exe");
        string helperHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(helper)));
        string scratch = Path.Combine(Path.GetTempPath(), "infinium-wp9-program-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            OneShotCredentialHelperLauncher launcher = new(helper, helperHash, scratch);
            Wp9NonLiveProbeResult typed = await launcher.ExecuteWp9NonLiveProbeAsync("typed", TimeSpan.FromSeconds(3));
            Assert.AreEqual("typed-envelope", typed.Terminal);
            Assert.AreEqual("controlled-failure", typed.Envelope!.Reason);
            using (JsonDocument canaries = JsonDocument.Parse(typed.Envelope.CanaryEvidenceJson!))
            {
                Assert.AreEqual(0, canaries.RootElement.GetProperty("SecretMatches").GetInt32());
                Assert.AreEqual(0, canaries.RootElement.GetProperty("RawTargetMatches").GetInt32());
                Assert.AreEqual(5, canaries.RootElement.GetProperty("ScannedSurfaces").GetArrayLength());
            }
            Assert.AreEqual(0, typed.Survivors);
            Assert.AreEqual("eof", (await launcher.ExecuteWp9NonLiveProbeAsync("eof", TimeSpan.FromSeconds(3))).Terminal);
            Assert.AreEqual("invalid-frame", (await launcher.ExecuteWp9NonLiveProbeAsync("crash", TimeSpan.FromSeconds(3))).Terminal);
            Wp9NonLiveProbeResult timeout = await launcher.ExecuteWp9NonLiveProbeAsync("timeout", TimeSpan.FromMilliseconds(150));
            Assert.AreEqual("timeout", timeout.Terminal);
            Assert.AreEqual(0, timeout.Survivors);
            CredentialNativeHelperEvidenceAmbiguityException productionTimeout =
                OneShotCredentialHelperLauncher.NormalizeWp9ProductionTimeoutForTest(
                    "wp9-production-profile/enroll-and-verify", new OperationCanceledException());
            productionTimeout.AttachContainment(new(1, -1, 2, 1, timeout.Survivors, true));
            Assert.AreEqual("runtime-metrics", productionTimeout.ValidationStage);
            Assert.IsTrue(productionTimeout.Containment!.ProcessTreeTerminated);
        }
        finally { if (Directory.Exists(scratch)) { Directory.Delete(scratch, recursive: true); } }
    }

    [TestMethod]
    public void Wp9TypedFailureEnvelopeValidatorAcceptsExactZeroEffectCanariesAndRejectsMutation()
    {
        string manifest = Path.Combine(RepositoryRoot(), "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v1.json");
        object canaries = new
        {
            SecretMatches = 0,
            RawTargetMatches = 0,
            RawTargetEncodings,
            ScannedSurfaces = new[]
            {
                new { Name = "private protocol request", Kind = "private-pipe-bytes", ByteCount = 1, SecretMatches = 0, RawTargetMatches = 0 },
                new { Name = "private protocol partial response", Kind = "private-pipe-bytes", ByteCount = 0, SecretMatches = 0, RawTargetMatches = 0 },
                new { Name = "native call trace", Kind = "canonical-trace-bytes", ByteCount = 2, SecretMatches = 0, RawTargetMatches = 0 },
                new { Name = "process command line", Kind = "captured-text", ByteCount = 1, SecretMatches = 0, RawTargetMatches = 0 },
                new { Name = "process environment names", Kind = "captured-text", ByteCount = 1, SecretMatches = 0, RawTargetMatches = 0 },
            },
        };
        NativeHelperFailureEnvelope exact = new(
            "evidence-collection", "controlled-failure", true, 0, 0, 0, 0, 0,
            true, 0, 0, true, 0, 0, 0, "[]", null, JsonSerializer.Serialize(canaries),
            false, false, 0, false, null);
        HelperBootstrapV2 bootstrap = new();
        HelperAssignmentV2 assignment = new()
        {
            AssignmentId = "wp9-production-profile/enroll-and-verify",
            AssignmentKind = HelperAssignmentKindV2.Enroll,
        };
        CredentialNativeQualificationSupervisor.ValidateNativeHelperFailureEnvelope(
            exact, bootstrap, assignment, manifest, helperProcessId: 1);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CredentialNativeQualificationSupervisor.ValidateNativeHelperFailureEnvelope(
                exact with
                {
                    CanaryEvidenceJson = JsonSerializer.Serialize(new
                    {
                        SecretMatches = 1,
                        RawTargetMatches = 0,
                        RawTargetEncodings,
                        ScannedSurfaces = Array.Empty<object>(),
                    })
                },
                bootstrap, assignment, manifest, helperProcessId: 1));

        CredentialNativeCallTraceEntry[] fullTrace =
        [
            new(1, "CredReadW", Fingerprint, assignment.AssignmentId, "ERROR_NOT_FOUND", null, null),
            new(2, "CredWriteW", Fingerprint, assignment.AssignmentId, "success", null, null),
            new(3, "CredReadW", Fingerprint, assignment.AssignmentId, "success", 1, null),
            new(4, "CredFree", Fingerprint, assignment.AssignmentId, "released", null, 1),
        ];
        object exactEntry = new
        {
            Surface = "wp9-distinct-helper-owned-native-masked-paste-surface",
            Masked = true,
            PastePermitted = true,
            HelperOwned = true,
            RendererReceivedSecret = false,
            InitiallyBlank = true,
            Ready = true,
            HelperProcessOwned = true,
            SameSession = true,
            InputDesktopAvailable = true,
            NotCloaked = true,
            OnMonitor = true,
            Enabled = true,
            Focused = true,
            Foreground = true,
            Active = true,
            ReadinessChecks = 1,
            PreReadinessIgnoredActions = 0,
            MessagePumpIterations = 1,
            ActionSnapshot = new
            {
                Action = "submit",
                Source = "submit-button",
                WindowVisible = true,
                EditVisible = true,
                InitiallyBlank = true,
                HelperProcessOwned = true,
                SameSession = true,
                InputDesktopAvailable = true,
                NotCloaked = true,
                OnMonitor = true,
                Enabled = true,
                Focused = true,
                Foreground = true,
                Active = true,
                CurrentBlank = false,
                CurrentCharacterLength = 32,
                Admitted = true,
            },
            TerminalState = "submitted",
            WindowDestroyed = true,
            BufferCleared = true,
            NativeEditEmptyVerified = true,
            ThreadJoined = true,
        };
        NativeHelperFailureEnvelope postEngine = new(
            "evidence-collection", "controlled-failure", true, 1, 2, 0, 1, 4,
            true, 0, 0, true, 0, 0, 0, JsonSerializer.Serialize(fullTrace),
            JsonSerializer.Serialize(exactEntry), JsonSerializer.Serialize(canaries),
            true, true, 42, false, null);
        Assert.IsInstanceOfType<CredentialNativeHelperFailureException>(
            OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(postEngine, bootstrap, assignment, manifest));
        JsonObject metricsCanaries = JsonNode.Parse(JsonSerializer.Serialize(canaries))!.AsObject();
        metricsCanaries["ScannedSurfaces"]![1]!["Name"] = "private protocol response";
        Exception metricsResult = OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(
            postEngine with { Stage = "metrics-write", CanaryEvidenceJson = metricsCanaries.ToJsonString() },
            bootstrap, assignment, manifest);
        Assert.IsInstanceOfType<CredentialNativeHelperFailureException>(metricsResult,
            metricsResult.InnerException?.Message);
        for (int index = 0; index < fullTrace.Length; index++)
        {
            CredentialNativeCallTraceEntry[] mutated = fullTrace.ToArray();
            mutated[index] = mutated[index] with { Result = "mutated-result" };
            Assert.IsInstanceOfType<CredentialNativeHelperEvidenceAmbiguityException>(
                OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(
                    postEngine with { NativeCallTraceJson = JsonSerializer.Serialize(mutated) },
                    bootstrap, assignment, manifest), $"result-index={index}");
        }
        CredentialNativeCallTraceEntry[] collisionTrace =
        [
            new(1, "CredReadW", Fingerprint, assignment.AssignmentId, "success", 7, null),
            new(2, "CredFree", Fingerprint, assignment.AssignmentId, "released", null, 7),
        ];
        NativeHelperFailureEnvelope collisionEnvelope = postEngine with
        {
            CredWriteW = 0,
            CredReadW = 1,
            CredFree = 1,
            Total = 2,
            NativeCallTraceJson = JsonSerializer.Serialize(collisionTrace),
            NamespaceReuseBlocked = true,
            NamespaceReuseBlockReason = "preflight-collision",
        };
        Assert.IsInstanceOfType<CredentialNativeHelperFailureException>(
            OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(
                collisionEnvelope, bootstrap, assignment, manifest));
        for (int index = 0; index < collisionTrace.Length; index++)
        {
            CredentialNativeCallTraceEntry[] mutated = collisionTrace.ToArray();
            mutated[index] = mutated[index] with { Result = "mutated-result" };
            Assert.IsInstanceOfType<CredentialNativeHelperEvidenceAmbiguityException>(
                OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(
                    collisionEnvelope with { NativeCallTraceJson = JsonSerializer.Serialize(mutated) },
                    bootstrap, assignment, manifest));
        }
        foreach (string uiProperty in new[] { "Ready", "Foreground", "Active", "WindowDestroyed", "BufferCleared", "NativeEditEmptyVerified", "ThreadJoined" })
        {
            JsonObject entryMutation = JsonNode.Parse(JsonSerializer.Serialize(exactEntry))!.AsObject();
            entryMutation[uiProperty] = false;
            Assert.IsInstanceOfType<CredentialNativeHelperEvidenceAmbiguityException>(
                OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(
                    postEngine with { EntryCleanupJson = entryMutation.ToJsonString() },
                    bootstrap, assignment, manifest), uiProperty);
        }
        foreach (string actionProperty in new[]
        {
            "WindowVisible", "EditVisible", "InitiallyBlank", "HelperProcessOwned", "SameSession",
            "InputDesktopAvailable", "NotCloaked", "OnMonitor", "Enabled", "Focused",
            "Foreground", "Active", "Admitted",
        })
        {
            JsonObject entryMutation = JsonNode.Parse(JsonSerializer.Serialize(exactEntry))!.AsObject();
            entryMutation["ActionSnapshot"]![actionProperty] = false;
            Assert.IsInstanceOfType<CredentialNativeHelperEvidenceAmbiguityException>(
                OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(
                    postEngine with { EntryCleanupJson = entryMutation.ToJsonString() },
                    bootstrap, assignment, manifest), actionProperty);
        }
        foreach ((string property, JsonNode value) in new (string, JsonNode)[]
        {
            ("Source", JsonValue.Create("injected-command")!),
            ("CurrentBlank", JsonValue.Create(true)!),
            ("CurrentCharacterLength", JsonValue.Create(0)!),
        })
        {
            JsonObject entryMutation = JsonNode.Parse(JsonSerializer.Serialize(exactEntry))!.AsObject();
            entryMutation["ActionSnapshot"]![property] = value;
            Assert.IsInstanceOfType<CredentialNativeHelperEvidenceAmbiguityException>(
                OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(
                    postEngine with { EntryCleanupJson = entryMutation.ToJsonString() },
                    bootstrap, assignment, manifest), property);
        }
        JsonObject canaryMutation = JsonNode.Parse(JsonSerializer.Serialize(canaries))!.AsObject();
        canaryMutation["ScannedSurfaces"]!.AsArray().Add(
            canaryMutation["ScannedSurfaces"]![0]!.DeepClone());
        CredentialNativeHelperEvidenceAmbiguityException sanitized =
            (CredentialNativeHelperEvidenceAmbiguityException)
            OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(
                postEngine with { CanaryEvidenceJson = canaryMutation.ToJsonString() },
                bootstrap, assignment, manifest);
        Assert.IsNotNull(sanitized.EnvelopeSummary);
        Assert.IsGreaterThan(0, sanitized.EnvelopeSummary.CanaryEvidenceUtf8Bytes);
        JsonObject encodingMutation = JsonNode.Parse(JsonSerializer.Serialize(canaries))!.AsObject();
        encodingMutation["RawTargetEncodings"] = new JsonArray("utf-8", "utf-8");
        Assert.IsInstanceOfType<CredentialNativeHelperEvidenceAmbiguityException>(
            OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(
                postEngine with { CanaryEvidenceJson = encodingMutation.ToJsonString() },
                bootstrap, assignment, manifest));
        JsonObject malformedCanary = JsonNode.Parse(JsonSerializer.Serialize(canaries))!.AsObject();
        malformedCanary["RawTargetEncodings"] = "not-an-array";
        Assert.IsInstanceOfType<CredentialNativeHelperEvidenceAmbiguityException>(
            OneShotCredentialHelperLauncher.ValidateWp9FailureForTest(
                postEngine with { CanaryEvidenceJson = malformedCanary.ToJsonString() },
                bootstrap, assignment, manifest));
    }

    private const string Fingerprint = "55ade50556f396dd0ba579632a21581887eeb1e4e44411a0ee8e37f460f09fca";
    private static readonly string[] RawTargetEncodings = ["utf-8", "utf-16le"];

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public void SuccessCancelCollisionAndEverySafeFailurePrefixAreExactlyClassified()
    {
        CredentialNativeCallTraceEntry[] success =
        [
            new(1, "CredReadW", Fingerprint, "wp9-production-profile-enrollment", "ERROR_NOT_FOUND", null, null),
            new(2, "CredWriteW", Fingerprint, "wp9-production-profile-enrollment", "success", null, null),
            new(3, "CredReadW", Fingerprint, "wp9-production-profile-enrollment", "success", 1, null),
            new(4, "CredFree", Fingerprint, "wp9-production-profile-enrollment", "released", null, 1),
        ];
        Assert.AreEqual("passed-active-verified", Wp9ProductionProfileEnrollmentRunner.ValidateEffectReceipt(
            Receipt(HelperOutcomeV2.Completed, success, "submitted"), Projection("active-verified", "available"), Fingerprint));
        Assert.AreEqual("stopped-owner-cancelled", Wp9ProductionProfileEnrollmentRunner.ValidateEffectReceipt(
            Receipt(HelperOutcomeV2.Cancelled, [], "cancelled"), Projection("pending-enrollment", "unavailable"), Fingerprint));

        CredentialNativeCallTraceEntry[] collision =
        [
            new(1, "CredReadW", Fingerprint, "wp9-production-profile-enrollment", "success", 7, null),
            new(2, "CredFree", Fingerprint, "wp9-production-profile-enrollment", "released", null, 7),
        ];
        Assert.AreEqual("stopped-existing-target-collision", Wp9ProductionProfileEnrollmentRunner.ValidateEffectReceipt(
            Receipt(HelperOutcomeV2.FailedKnown, collision, "submitted", namespaceBlocked: true),
            Projection("pending-enrollment", "unavailable"), Fingerprint));

        CredentialNativeCallTraceEntry[][] stoppedPrefixes =
        [
            [],
            [success[0] with { Result = "win32-error:5" }],
            [success[0], success[1] with { Result = "win32-error:5" }],
            [success[0], success[1], success[2]],
        ];
        for (int length = 0; length <= 3; length++)
        {
            string expected = length == 3 ? "stopped-ambiguous-effect" : "stopped-native-failure";
            Assert.AreEqual(expected, Wp9ProductionProfileEnrollmentRunner.ValidateEffectReceipt(
                Receipt(HelperOutcomeV2.Unavailable, stoppedPrefixes[length], "submitted"),
                Projection("secure-store-unavailable", "unavailable"), Fingerprint), $"prefix={length}");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public void MutatedTraceCanaryEntryAndFreePairingAreRejected()
    {
        CredentialNativeCallTraceEntry[] success =
        [
            new(1, "CredReadW", Fingerprint, "wp9-production-profile-enrollment", "ERROR_NOT_FOUND", null, null),
            new(2, "CredWriteW", Fingerprint, "wp9-production-profile-enrollment", "success", null, null),
            new(3, "CredReadW", Fingerprint, "wp9-production-profile-enrollment", "success", 1, null),
            new(4, "CredFree", Fingerprint, "wp9-production-profile-enrollment", "released", null, 1),
        ];
        Assert.ThrowsExactly<InvalidDataException>(() => Wp9ProductionProfileEnrollmentRunner.ValidateEffectReceipt(
            Receipt(HelperOutcomeV2.Completed,
                [success[0], success[1], success[2] with { TargetFingerprintSha256 = new string('0', 64) }, success[3]],
                "submitted"),
            Projection("active-verified", "available"), Fingerprint));
        Assert.ThrowsExactly<InvalidDataException>(() => Wp9ProductionProfileEnrollmentRunner.ValidateEffectReceipt(
            Receipt(HelperOutcomeV2.Completed,
                [success[0], success[1], success[2], success[3] with { PairedAllocationId = 2 }],
                "submitted"),
            Projection("active-verified", "available"), Fingerprint));
        Assert.ThrowsExactly<InvalidDataException>(() => Wp9ProductionProfileEnrollmentRunner.ValidateEffectReceipt(
            Receipt(HelperOutcomeV2.Completed, success, "submitted", secretMatches: 1),
            Projection("active-verified", "available"), Fingerprint));
        Assert.ThrowsExactly<InvalidDataException>(() => Wp9ProductionProfileEnrollmentRunner.ValidateEffectReceipt(
            Receipt(HelperOutcomeV2.Completed, success, "cancelled"),
            Projection("active-verified", "available"), Fingerprint));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    public void AmbiguousFailureAlwaysRetainsAllThreeNonSecretArtifactsIndependently()
    {
        string root = RepositoryRoot();
        string manifest = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v1.json");
        string output = Path.Combine(Path.GetTempPath(), "infinium-wp9-ambiguity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            Wp9ProductionProfileEnrollmentRunner.RetainAmbiguousFailure(
                output, manifest, new string('a', 64), new EndOfStreamException(), "recovery-required");
            string failure = Path.Combine(output, "profile-enrollment-failure.json");
            string evidence = Path.Combine(output, "profile-enrollment-evidence.json");
            string summary = Path.Combine(output, "profile-enrollment-summary.txt");
            Assert.IsTrue(File.Exists(failure));
            Assert.IsTrue(File.Exists(evidence));
            Assert.IsTrue(File.Exists(summary));
            File.Delete(evidence);
            File.Delete(summary);
            Wp9ProductionProfileEnrollmentRunner.RetainAmbiguousFailure(
                output, manifest, new string('a', 64), new EndOfStreamException(), "recovery-required");
            Assert.IsTrue(File.Exists(evidence), "Main evidence must be repaired independently of an existing failure receipt.");
            Assert.IsTrue(File.Exists(summary), "Summary must be repaired independently of an existing failure receipt.");
            string retained = File.ReadAllText(failure) + File.ReadAllText(evidence) + File.ReadAllText(summary);
            Assert.IsFalse(retained.Contains("Infinium:openai-platform-", StringComparison.Ordinal));
            StringAssert.Contains(retained, "recovery-required");
            StringAssert.Contains(retained, "network_operations=unknown");
        }
        finally { Directory.Delete(output, recursive: true); }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    public void TypedHelperFailureRetainsKnownTraceCountsNetworkCanariesAndContainment()
    {
        string root = RepositoryRoot();
        string manifest = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v1.json");
        string output = Path.Combine(Path.GetTempPath(), "infinium-wp9-typed-failure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            CredentialNativeCallTraceEntry[] trace =
            [
                new(1, "CredReadW", Fingerprint, "wp9-production-profile/enroll-and-verify", "ERROR_NOT_FOUND", null, null),
                new(2, "CredWriteW", Fingerprint, "wp9-production-profile/enroll-and-verify", "win32-error:5", null, null),
            ];
            NativeHelperFailureEnvelope envelope = new(
                "engine-execution", "win32-failure", true, 1, 1, 0, 0, 2,
                true, 0, 0, true, 0, 0, 0, JsonSerializer.Serialize(trace),
                null, "{\"SecretMatches\":0,\"RawTargetMatches\":0}", false, true, 42, false, null);
            CredentialNativeHelperFailureException failure = new(
                envelope, "wp9-production-profile/enroll-and-verify");
            failure.AttachContainment(new(41, 72, 2, 1, 0, true));
            Wp9ProductionProfileEnrollmentRunner.RetainAmbiguousFailure(
                output, manifest, new string('a', 64), failure, "recovery-required");
            using JsonDocument retained = JsonDocument.Parse(File.ReadAllBytes(
                Path.Combine(output, "profile-enrollment-failure.json")));
            JsonElement value = retained.RootElement;
            Assert.AreEqual("known", value.GetProperty("native_call_count_status").GetString());
            Assert.AreEqual(2, value.GetProperty("native_credential_operation_count").GetInt32());
            Assert.AreEqual(2, value.GetProperty("native_call_trace").GetArrayLength());
            Assert.AreEqual(0, value.GetProperty("network_operation_count").GetInt32());
            Assert.IsTrue(value.GetProperty("process_tree_terminated").GetBoolean());
            Assert.AreEqual(0, value.GetProperty("process_tree_survivor_count").GetInt32());
        }
        finally { Directory.Delete(output, recursive: true); }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public void PreUiManifestFailureRetainsKnownZeroEffectClassificationAndCounts()
    {
        string root = RepositoryRoot();
        string manifest = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v3.json");
        string output = Path.Combine(Path.GetTempPath(), "infinium-wp9-pre-ui-zero-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        try
        {
            NativeHelperFailureEnvelope envelope = new(
                "manifest-validation", "manifest-rejected", true, 0, 0, 0, 0, 0,
                true, 0, 0, true, 0, 0, 0, "[]", null, null,
                false, false, 0, false, null);
            CredentialNativeHelperFailureException failure = new(
                envelope, "wp9-production-profile/enroll-and-verify");
            failure.AttachContainment(new(41, 72, 1, 1, 0, true));

            Wp9ProductionProfileEnrollmentRunner.RetainAmbiguousFailure(
                output, manifest, new string('a', 64), failure, "recovery-required");

            foreach (string file in new[]
            {
                "profile-enrollment-failure.json",
                "profile-enrollment-evidence.json",
            })
            {
                using JsonDocument retained = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, file)));
                Assert.AreEqual("stopped-known-zero-effect-pre-ui",
                    retained.RootElement.GetProperty("status").GetString(), file);
                Assert.AreEqual(0,
                    retained.RootElement.GetProperty("native_credential_operation_count").GetInt32(), file);
                Assert.AreEqual(0,
                    retained.RootElement.GetProperty("network_operation_count").GetInt32(), file);
                Assert.AreEqual(0,
                    retained.RootElement.GetProperty("provider_operation_count").GetInt32(), file);
                Assert.AreEqual(0,
                    retained.RootElement.GetProperty("billable_operation_count").GetInt32(), file);
            }
            string summary = File.ReadAllText(Path.Combine(output, "profile-enrollment-summary.txt"));
            StringAssert.Contains(summary, "status=stopped-known-zero-effect-pre-ui");
            StringAssert.Contains(summary, "native_calls=0");
            StringAssert.Contains(summary, "network_operations=0");
        }
        finally { Directory.Delete(output, recursive: true); }
    }

    private static HelperProcessReceipt Receipt(
        HelperOutcomeV2 outcome,
        CredentialNativeCallTraceEntry[] trace,
        string terminal,
        bool namespaceBlocked = false,
        int secretMatches = 0)
    {
        byte[] entry = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Surface = "wp9-distinct-helper-owned-native-masked-paste-surface",
            Masked = true,
            PastePermitted = true,
            HelperOwned = true,
            RendererReceivedSecret = false,
            InitiallyBlank = true,
            Ready = true,
            HelperProcessOwned = true,
            SameSession = true,
            InputDesktopAvailable = true,
            NotCloaked = true,
            OnMonitor = true,
            Enabled = true,
            Focused = true,
            Foreground = true,
            Active = true,
            ReadinessChecks = 1,
            PreReadinessIgnoredActions = 0,
            MessagePumpIterations = 1,
            ActionSnapshot = new
            {
                Action = terminal == "cancelled" ? "cancel" : "submit",
                Source = terminal == "cancelled" ? "cancel-button" : "submit-button",
                WindowVisible = true,
                EditVisible = true,
                InitiallyBlank = true,
                HelperProcessOwned = true,
                SameSession = true,
                InputDesktopAvailable = true,
                NotCloaked = true,
                OnMonitor = true,
                Enabled = true,
                Focused = true,
                Foreground = true,
                Active = true,
                CurrentBlank = terminal == "cancelled",
                CurrentCharacterLength = terminal == "cancelled" ? 0 : 32,
                Admitted = true,
            },
            TerminalState = terminal,
            WindowDestroyed = true,
            BufferCleared = true,
            NativeEditEmptyVerified = true,
            ThreadJoined = true,
        });
        byte[] canary = JsonSerializer.SerializeToUtf8Bytes(new
        {
            SecretMatches = secretMatches,
            RawTargetMatches = 0,
            RawTargetEncodings,
            ScannedSurfaces = new[]
            {
                new { Name = "private protocol request", Kind = "private-pipe-bytes", ByteCount = 1, SecretMatches = 0, RawTargetMatches = 0 },
                new { Name = "private protocol response", Kind = "private-pipe-bytes", ByteCount = 1, SecretMatches = 0, RawTargetMatches = 0 },
                new { Name = "native call trace", Kind = "canonical-trace-bytes", ByteCount = 1, SecretMatches = 0, RawTargetMatches = 0 },
                new { Name = "process command line", Kind = "captured-text", ByteCount = 1, SecretMatches = 0, RawTargetMatches = 0 },
                new { Name = "process environment names", Kind = "captured-text", ByteCount = 1, SecretMatches = 0, RawTargetMatches = 0 },
            },
        });
        return new(
            ProcessId: 1,
            ExitCode: 0,
            BinarySha256: new string('a', 64),
            Receipt: new HelperReceiptV2 { Outcome = outcome },
            StagedResponseBytes: [],
            InheritedPrivateHandleCount: 2,
            StandardProtocolHandleCount: 0,
            ListenerCount: 0,
            NetworkOperationCount: 0,
            NativeCredentialOperationCount: trace.Length,
            ProcessTreeSurvivorCount: 0,
            ProcessTreeTerminated: true,
            RetryAttempted: false,
            NativeCallTraceBytes: JsonSerializer.SerializeToUtf8Bytes(trace),
            NativeEntryCleanupBytes: entry,
            NativeCanaryEvidenceBytes: canary,
            ContainmentProbeExecuted: true,
            ExcludedHandleAccessible: false,
            ActiveProcessCountBeforeJobClose: 1,
            TotalContainedProcessCount: 2,
            NativeNamespaceReuseBlocked: namespaceBlocked,
            NativeNamespaceReuseBlockReason: namespaceBlocked ? "preflight-collision" : null);
    }

    private static CredentialProfileProjection Projection(string lifecycle, string verification) => new(
        "openai-platform-492800995cf046c7815f974e865f9e1d",
        "g-9c663cb01fb649cba7eff4e26e14274c",
        1, 0, lifecycle, verification, "cap", "account", "billing", "intent",
        "none", "none", 1, DateTimeOffset.UtcNow);

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
