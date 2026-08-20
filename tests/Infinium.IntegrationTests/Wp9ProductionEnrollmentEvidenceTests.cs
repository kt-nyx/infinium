using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;
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
    [TestCategory("Security")]
    public void SuccessEvidenceAcceptsTheExactHelperScenarioAndRejectsTheLegacyAssignmentScenario()
    {
        CredentialNativeCallTraceEntry[] success =
        [
            new(1, "CredReadW", Fingerprint, "wp9-production-profile-enrollment", "ERROR_NOT_FOUND", null, null),
            new(2, "CredWriteW", Fingerprint, "wp9-production-profile-enrollment", "success", null, null),
            new(3, "CredReadW", Fingerprint, "wp9-production-profile-enrollment", "success", 1, null),
            new(4, "CredFree", Fingerprint, "wp9-production-profile-enrollment", "released", null, 1),
        ];
        HelperProcessReceipt receipt = Receipt(HelperOutcomeV2.Completed, success, "submitted");
        string scratch = Path.Combine(Path.GetTempPath(), "infinium-wp9-success-evidence-"
            + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            using JsonDocument trace = JsonDocument.Parse(receipt.NativeCallTraceBytes!);
            using JsonDocument entry = JsonDocument.Parse(receipt.NativeEntryCleanupBytes!);
            using JsonDocument canaries = JsonDocument.Parse(receipt.NativeCanaryEvidenceBytes!);
            string evidence = Path.Combine(scratch, "success.json");
            string sha = Wp9ProductionProfileEnrollmentRunner.ProduceV2SuccessEvidence(
                evidence, "manifest", new string('a', 64), new string('b', 64),
                "profile", "generation", Fingerprint, trace.RootElement, entry.RootElement,
                canaries.RootElement, DateTimeOffset.UtcNow);
            Assert.AreEqual(64, sha.Length);
            using JsonDocument retained = JsonDocument.Parse(File.ReadAllBytes(evidence));
            Assert.AreEqual("wp9-production-profile-enrollment",
                retained.RootElement.GetProperty("native_call_trace")[0]
                    .GetProperty("Scenario").GetString());

            byte[] exactEvidence = File.ReadAllBytes(evidence);
            Action<JsonObject>[] semanticMutations =
            [
                root => root["target_fingerprint_sha256"] = new string('0', 64),
                root => root["lifecycle_state"] = "active-unverified",
                root => root["verification_state"] = "not-requested",
                root => root["native_credential_operation_count"] = 3,
                root => root["network_operation_count"] = 1,
                root => root["listener_count"] = 1,
                root => root["provider_operation_count"] = 1,
                root => root["billable_operation_count"] = 1,
                root => root["retry_attempted"] = true,
                root => root["containment"]!["probe_executed"] = false,
                root => root["containment"]!["excluded_handle_accessible"] = true,
                root => root["containment"]!["process_tree_terminated"] = false,
                root => root["containment"]!["process_tree_survivor_count"] = 1,
                root => root["containment"]!["total_contained_process_count"] = 1,
                root => root["completed_at_utc"] = "2026-08-20T12:00:00+00:00",
            ];
            foreach (Action<JsonObject> mutate in semanticMutations)
            {
                JsonObject changed = JsonNode.Parse(exactEvidence)!.AsObject();
                mutate(changed);
                using JsonDocument changedDocument = JsonDocument.Parse(changed.ToJsonString());
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    Wp9ProductionProfileEnrollmentRunner.ValidateAcceptedCampaignCredentialArtifacts(
                        changedDocument.RootElement, Fingerprint));
            }

            CredentialNativeCallTraceEntry[] legacy = success.Select(item => item with
            {
                Scenario = "wp9-production-profile/enroll-and-verify",
            }).ToArray();
            using JsonDocument legacyTrace = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(legacy));
            Assert.ThrowsExactly<InvalidDataException>(() =>
                Wp9ProductionProfileEnrollmentRunner.ProduceV2SuccessEvidence(
                    Path.Combine(scratch, "legacy.json"), "manifest", new string('a', 64),
                    new string('b', 64), "profile", "generation", Fingerprint,
                    legacyTrace.RootElement, entry.RootElement, canaries.RootElement,
                    DateTimeOffset.UtcNow));
        }
        finally { Directory.Delete(scratch, recursive: true); }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public void ExactPostSuccessRecoveryCrossesTheCoordinatorSeamWithoutChangingProductState()
    {
        using RecoveryFixture fixture = RecoveryFixture.Create();
        byte[] productBefore = fixture.ProductStateIdentity();

        fixture.Recover();

        M1Slice6FiniteCampaignLedger recovered = fixture.OpenLedger();
        Assert.AreEqual(M1Slice6CampaignState.CredentialEvidenceAccepted, recovered.Current.State);
        Assert.AreEqual("credential-post-success-validator-defect-evidence-accepted", recovered.Current.Event);
        Assert.AreEqual(new M1Slice6CampaignNativeEnvelope(1, 2, 0, 1, 4), recovered.Current.NativeEnvelope);
        Assert.AreEqual(0L, recovered.Current.ProviderCallCount);
        Assert.AreEqual(0L, recovered.Current.DnsResolutionCount);
        CollectionAssert.AreEqual(productBefore, fixture.ProductStateIdentity());
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    public void RecoveryDenialsLeaveTheTerminalLedgerAndProductStateByteIdentical()
    {
        AssertRecoveryDenied(fixture => fixture.MutateSuccess(root => root["provider_operation_count"] = 1));
        AssertRecoveryDenied(fixture => fixture.MutateSuccess(root =>
            root["completed_at_utc"] = "2026-09-16T00:00:00.0000000Z"));
        AssertRecoveryDenied(fixture => fixture.MutateFailure(root => root["provider_operation_count"] = 1));
        AssertRecoveryDenied(fixture => fixture.MutateRuntime(root => root["scope"] = "external-effect"));
        AssertRecoveryDenied(fixture => fixture.MutateRuntime(root =>
            root["execution"]!["output_root_relative"] = "artifacts/wrong-output"));
        AssertRecoveryDenied(fixture => fixture.HelperPath = fixture.WriteChangedBinary("changed-helper.exe"));
        AssertRecoveryDenied(fixture => fixture.CoordinatorPath = fixture.WriteChangedBinary("changed-coordinator.dll"));
        AssertRecoveryDenied(fixture => fixture.DisableDurableProjection());
        AssertRecoveryDenied(_ => { }, durableAccountOverride: "wrong-account-binding");
        AssertRecoveryDenied(_ => { }, terminalReason: "owner-cancelled");
    }

    private static void AssertRecoveryDenied(Action<RecoveryFixture> mutate,
        string terminalReason = "helper-evidence-ambiguity", string? durableAccountOverride = null)
    {
        using RecoveryFixture fixture = RecoveryFixture.Create(terminalReason, durableAccountOverride);
        mutate(fixture);
        byte[] ledgerBefore = File.ReadAllBytes(fixture.LedgerPath);
        byte[] productBefore = fixture.ProductStateIdentity();
        Exception? failure = null;
        try { fixture.Recover(); }
        catch (Exception exception) { failure = exception; }
        Assert.IsNotNull(failure);
        CollectionAssert.AreEqual(ledgerBefore, File.ReadAllBytes(fixture.LedgerPath));
        CollectionAssert.AreEqual(productBefore, fixture.ProductStateIdentity());
        Assert.AreEqual(M1Slice6CampaignState.Stopped, fixture.OpenLedger().Current.State);
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

    private sealed class RecoveryFixture : IDisposable
    {
        private static readonly DateTimeOffset Now = DateTimeOffset.Parse(
            "2026-08-20T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        private RecoveryFixture(string root, string credentialPath, string credentialSha,
            string campaignPath, string campaignSha, string reviewedCandidate, string ledgerPath,
            string successPath, string failurePath, string productRoot, string helperPath,
            string coordinatorPath, string runtimePath, string runtimeSha, M1Slice6CampaignIdentity identity,
            DateTimeOffset campaignExpiry, DateTimeOffset credentialExpiry)
        {
            Root = root;
            CredentialPath = credentialPath;
            CredentialSha = credentialSha;
            CampaignPath = campaignPath;
            CampaignSha = campaignSha;
            ReviewedCandidate = reviewedCandidate;
            LedgerPath = ledgerPath;
            SuccessPath = successPath;
            FailurePath = failurePath;
            ProductRoot = productRoot;
            HelperPath = helperPath;
            CoordinatorPath = coordinatorPath;
            RuntimePath = runtimePath;
            RuntimeSha = runtimeSha;
            Identity = identity;
            CampaignExpiry = campaignExpiry;
            CredentialExpiry = credentialExpiry;
        }

        internal string Root { get; }
        internal string CredentialPath { get; }
        internal string CredentialSha { get; }
        internal string CampaignPath { get; }
        internal string CampaignSha { get; }
        internal string ReviewedCandidate { get; }
        internal string LedgerPath { get; }
        internal string SuccessPath { get; }
        internal string FailurePath { get; }
        internal string ProductRoot { get; }
        internal string HelperPath { get; set; }
        internal string CoordinatorPath { get; set; }
        internal string RuntimePath { get; }
        internal string RuntimeSha { get; private set; }
        private M1Slice6CampaignIdentity Identity { get; }
        private DateTimeOffset CampaignExpiry { get; }
        private DateTimeOffset CredentialExpiry { get; }

        internal static RecoveryFixture Create(string terminalReason = "helper-evidence-ambiguity",
            string? durableAccountOverride = null)
        {
            string root = Path.Combine(Path.GetTempPath(), "infinium-c2a-recovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            string contracts = Path.Combine(root, "contracts", "repository");
            Directory.CreateDirectory(contracts);
            foreach (string schema in new[]
                     {
                         "wp9-production-profile-authorization.v4.schema.json",
                         "m1-slice6-finite-campaign-authorization.v4.schema.json",
                     })
            {
                File.Copy(TestRepository.PathFromRoot("contracts", "repository", schema),
                    Path.Combine(contracts, schema));
            }

            string bin = Path.Combine(root, "bin");
            Directory.CreateDirectory(bin);
            string coordinatorPath = Path.Combine(bin, "Infinium.Coordinator.dll");
            File.Copy(typeof(Wp9ProductionProfileEnrollmentRunner).Assembly.Location, coordinatorPath);
            string helperPath = Path.Combine(bin, "Infinium.CredentialHelper.exe");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "CredentialHelper", "Infinium.CredentialHelper.exe"),
                helperPath);

            string docs = Path.Combine(root, "docs");
            Directory.CreateDirectory(docs);
            JsonObject credential = JsonNode.Parse(File.ReadAllBytes(TestRepository.PathFromRoot("docs", "plans",
                "milestones", "m1", "slices", "s6", "wp9-production-profile-authorization.v4.json")))!.AsObject();
            credential["release_build"]!["helper_sha256"] = Sha(helperPath);
            credential["release_build"]!["coordinator_sha256"] = Sha(coordinatorPath);
            string credentialPath = Path.Combine(docs, "credential.v4.json");
            Write(credentialPath, credential);
            string credentialSha = Sha(credentialPath);

            JsonObject campaign = JsonNode.Parse(File.ReadAllBytes(TestRepository.PathFromRoot("docs", "plans",
                "milestones", "m1", "slices", "s6", "m1-slice6-finite-campaign-authorization.v4.json")))!.AsObject();
            campaign["credential_envelope"]!["source_manifest_sha256"] = credentialSha;
            string campaignPath = Path.Combine(docs, "campaign.v4.json");
            Write(campaignPath, campaign);
            string campaignSha = Sha(campaignPath);

            string reviewedCandidate = credential["candidate_binding"]!["close_ready_implementation_commit"]!
                .GetValue<string>();
            JsonObject profile = credential["profile"]!.AsObject();
            JsonObject providerIntent = credential["provider_intent"]!.AsObject();
            string accountIdentity = durableAccountOverride
                ?? providerIntent["account_identity_id"]!.GetValue<string>();
            string billingIdentity = providerIntent["billing_scope_identity_id"]!.GetValue<string>();
            M1Slice6CampaignIdentity identity = new(
                campaign["campaign_id"]!.GetValue<string>(), campaignSha,
                campaign["authority_source"]!["attachment_sha256"]!.GetValue<string>(), reviewedCandidate,
                credential["manifest_id"]!.GetValue<string>(), credentialSha,
                profile["access_profile_id"]!.GetValue<string>(), profile["generation_id"]!.GetValue<string>(),
                profile["target_fingerprint_sha256"]!.GetValue<string>());
            DateTimeOffset campaignExpiry = DateTimeOffset.Parse(campaign["expires_at_utc"]!.GetValue<string>(),
                System.Globalization.CultureInfo.InvariantCulture);
            DateTimeOffset credentialExpiry = DateTimeOffset.Parse(credential["expires_at_utc"]!.GetValue<string>(),
                System.Globalization.CultureInfo.InvariantCulture);

            string output = Path.Combine(root, "artifacts", "wp9-profile");
            Directory.CreateDirectory(output);
            string ledgerPath = Path.Combine(output, "ledger.jsonl");
            M1Slice6FiniteCampaignLedger ledger = new(ledgerPath, identity, campaignExpiry, credentialExpiry, Now);
            ledger.RecordIndependentReview("original-c2a-runtime", new string('a', 64), Now.AddSeconds(1));
            ledger.AdmitCampaign(Now.AddSeconds(2));
            ledger.BeginCredentialExecutionHandoff(Now.AddSeconds(3));

            CredentialNativeCallTraceEntry[] trace =
            [
                new(1, "CredReadW", identity.CredentialTargetFingerprintSha256,
                    "wp9-production-profile-enrollment", "ERROR_NOT_FOUND", null, null),
                new(2, "CredWriteW", identity.CredentialTargetFingerprintSha256,
                    "wp9-production-profile-enrollment", "success", null, null),
                new(3, "CredReadW", identity.CredentialTargetFingerprintSha256,
                    "wp9-production-profile-enrollment", "success", 1, null),
                new(4, "CredFree", identity.CredentialTargetFingerprintSha256,
                    "wp9-production-profile-enrollment", "released", null, 1),
            ];
            HelperProcessReceipt receipt = Receipt(HelperOutcomeV2.Completed, trace, "submitted");
            using JsonDocument traceJson = JsonDocument.Parse(receipt.NativeCallTraceBytes!);
            using JsonDocument entryJson = JsonDocument.Parse(receipt.NativeEntryCleanupBytes!);
            using JsonDocument canaryJson = JsonDocument.Parse(receipt.NativeCanaryEvidenceBytes!);
            string successPath = Path.Combine(output, "success.json");
            string successSha = Wp9ProductionProfileEnrollmentRunner.ProduceV2SuccessEvidence(
                successPath, identity.CredentialManifestId, credentialSha, ledger.Current.EventHash,
                identity.CredentialProfileId, identity.CredentialGenerationId,
                identity.CredentialTargetFingerprintSha256, traceJson.RootElement, entryJson.RootElement,
                canaryJson.RootElement, Now.AddSeconds(4));

            string failurePath = Path.Combine(output, "failure.json");
            Write(failurePath, JsonSerializer.SerializeToNode(new
            {
                status = "stopped-ambiguous-effect",
                manifest_id = identity.CredentialManifestId,
                manifest_sha256 = credentialSha,
                provider_operation_count = 0,
                billable_operation_count = 0,
                retry_permitted = false,
            })!);
            string failureSha = Sha(failurePath);
            ledger.StopCredentialHandoff(terminalReason, "wp9-production-profile-enrollment-failure",
                failureSha, Now.AddSeconds(5));

            string productRoot = Path.Combine(root, "product-state");
            using (AuthoritativeStore store = new(new StoragePaths(productRoot)))
            {
                store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price,
                    Now.AddSeconds(-5));
                store.BeginCredentialEnrollment(identity.CredentialProfileId, identity.CredentialGenerationId,
                    "Recovery fixture", Now.AddSeconds(-4), accountIdentity, billingIdentity);
                store.ApplyCredentialTransition(new("fixture-enroll", identity.CredentialProfileId,
                    identity.CredentialGenerationId, "enroll", "pending-enrollment", "active-unverified",
                    "active-unverified", M1ProviderCatalog.Capability.Identity.Value,
                    accountIdentity, billingIdentity,
                    Now.AddSeconds(-3), Now.AddSeconds(-2)));
                store.ApplyCredentialTransition(new("fixture-verify", identity.CredentialProfileId,
                    identity.CredentialGenerationId, "verify", "active-unverified", "active-verified",
                    "active-verified", M1ProviderCatalog.Capability.Identity.Value,
                    accountIdentity, billingIdentity,
                    Now.AddSeconds(-1), Now));
            }

            string revision = Regex.Match(typeof(Wp9ProductionProfileEnrollmentRunner).Assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
                @"\+(?<sha>[0-9a-f]{40})$").Groups["sha"].Value;
            Assert.AreEqual(40, revision.Length);
            string runtimePath = Path.Combine(root, "runtime.json");
            JsonNode runtime = JsonSerializer.SerializeToNode(new
            {
                schema_identity = ProviderEffectRuntimeAuthorityLoader.SchemaIdentity,
                authority_id = "recovery-runtime/fixture",
                scope = "effect-free-rehearsal",
                kind = "credential-evidence-recovery",
                status = "reviewed-and-owner-accepted",
                subject_manifest = new { id = identity.CredentialManifestId, sha256 = credentialSha },
                campaign = new { id = identity.CampaignId, sha256 = campaignSha },
                predecessor = new
                {
                    ledger_event_hash = ledger.Current.EventHash,
                    evidence_id = ledger.Current.EvidenceId,
                    evidence_sha256 = ledger.Current.EvidenceSha256,
                },
                candidate_binding = new
                {
                    implementation_commit = revision,
                    coordinator_sha256 = Sha(coordinatorPath),
                    helper_sha256 = Sha(helperPath),
                },
                review = new
                {
                    evidence_id = "wp9-production-profile-enrollment-evidence-v2",
                    evidence_sha256 = successSha,
                },
                owner_decision = new { decision_id = "recovery-owner/fixture", decision_sha256 = new string('d', 64) },
                not_before_utc = Now.AddMinutes(-1).ToString("O"),
                expires_at_utc = Now.AddHours(1).ToString("O"),
                execution = new
                {
                    output_root_relative = Relative(root, output),
                    ledger_path_relative = Relative(root, ledgerPath),
                    product_state_root_relative = Relative(root, productRoot),
                    coordinator_path_relative = Relative(root, coordinatorPath),
                    helper_path_relative = Relative(root, helperPath),
                },
                limits = new
                {
                    helper_launches = 0,
                    credential_native_calls = 0,
                    provider_starts = 0,
                    dns_resolutions = 0,
                    billable_operations = 0,
                    literal_loopback_starts = 0,
                    automatic_retry = false,
                    fourth_call_permitted = false,
                },
            })!;
            Write(runtimePath, runtime);
            return new(root, credentialPath, credentialSha, campaignPath, campaignSha, reviewedCandidate,
                ledgerPath, successPath, failurePath, productRoot, helperPath, coordinatorPath,
                runtimePath, Sha(runtimePath), identity, campaignExpiry, credentialExpiry);
        }

        internal void Recover() => Wp9ProductionProfileEnrollmentRunner.RecoverCampaignCredentialEvidenceForTesting(
            CredentialPath, CredentialSha, CampaignPath, CampaignSha, ReviewedCandidate, LedgerPath,
            SuccessPath, FailurePath, ProductRoot, HelperPath, RuntimePath, RuntimeSha, Now.AddSeconds(6),
            CoordinatorPath);

        internal M1Slice6FiniteCampaignLedger OpenLedger() => new(LedgerPath, Identity,
            CampaignExpiry, CredentialExpiry, Now.AddSeconds(7));

        internal void MutateSuccess(Action<JsonObject> mutate)
        {
            JsonObject root = JsonNode.Parse(File.ReadAllBytes(SuccessPath))!.AsObject();
            mutate(root);
            Write(SuccessPath, root);
        }

        internal void MutateFailure(Action<JsonObject> mutate)
        {
            JsonObject root = JsonNode.Parse(File.ReadAllBytes(FailurePath))!.AsObject();
            mutate(root);
            Write(FailurePath, root);
        }

        internal void MutateRuntime(Action<JsonObject> mutate)
        {
            JsonObject root = JsonNode.Parse(File.ReadAllBytes(RuntimePath))!.AsObject();
            mutate(root);
            Write(RuntimePath, root);
            RuntimeSha = Sha(RuntimePath);
        }

        internal void DisableDurableProjection()
        {
            using AuthoritativeStore store = new(new StoragePaths(ProductRoot));
            CredentialProfileProjection current = store.GetCredentialProfile(Identity.CredentialProfileId);
            store.ApplyCredentialTransition(new("fixture-disable", current.ProfileId, current.GenerationId,
                "disable", "active-verified", "disabled", "disabled",
                M1ProviderCatalog.Capability.Identity.Value,
                current.AccountIdentityId, current.BillingScopeIdentityId,
                Now.AddTicks(1), Now.AddTicks(2)));
        }

        internal string WriteChangedBinary(string fileName)
        {
            string path = Path.Combine(Root, "bin", fileName);
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes("changed executable"));
            return path;
        }

        internal byte[] ProductStateIdentity()
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (string file in Directory.EnumerateFiles(ProductRoot, "*", SearchOption.AllDirectories)
                         .OrderBy(path => Relative(ProductRoot, path), StringComparer.Ordinal))
            {
                string relative = Relative(ProductRoot, file);
                hash.AppendData(Encoding.UTF8.GetBytes(relative + "\0" + new FileInfo(file).Length + "\0"));
                hash.AppendData(File.ReadAllBytes(file));
            }
            return hash.GetHashAndReset();
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root)) { Directory.Delete(Root, recursive: true); }
        }

        private static string Relative(string root, string path) =>
            Path.GetRelativePath(root, path).Replace('\\', '/');

        private static string Sha(string path) => Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(path)));

        private static void Write(string path, JsonNode node) => File.WriteAllText(path,
            node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
    }

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
