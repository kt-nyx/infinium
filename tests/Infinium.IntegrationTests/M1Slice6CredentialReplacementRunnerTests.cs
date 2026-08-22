using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class M1Slice6CredentialReplacementRunnerTests
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static readonly string[] MalformedWin32Errors =
    [
        "win32-error:05", "win32-error:-5", "win32-error:5 secret", "win32-error:2147483648",
    ];
    private enum FixtureMode { Initial, ReplacingRecovery, DeletePendingRecovery }

    [TestMethod]
    public void ForegroundRecoveryOwnerBindsExactStoppedAttemptAndReviewedCorrection()
    {
        string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(AppContext.BaseDirectory);
        string ownerPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "m1-slice6-development-campaign-amendment.v7.json");
        byte[] ownerBytes = File.ReadAllBytes(ownerPath);
        ActiveRepositoryJsonSchemaValidator.Validate(ownerBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-development-campaign-amendment.v7.schema.json")),
            M1Slice6SuccessorCredentialReplacementRunner.ForegroundRecoveryAmendmentSchema);
        using JsonDocument owner = JsonDocument.Parse(ownerBytes);
        string productRoot = Path.Combine(
            repository, "artifacts", "m1-slice6", "successor-product-state");
        string checkpointBefore =
            M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(productRoot);
        M1Slice6SuccessorCredentialReplacementRunner.ValidateForegroundRecoveryOwner(
            repository, owner.RootElement, "g-e6b6a3f21ad74108ba65955850349f83");
        M1Slice6SuccessorCredentialReplacementRunner.ValidateForegroundRecoveryOwner(
            repository, owner.RootElement, "g-e6b6a3f21ad74108ba65955850349f83");
        Assert.AreEqual(checkpointBefore,
            M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(productRoot));

        JsonObject tampered = JsonNode.Parse(ownerBytes)!.AsObject();
        tampered["retained_boundary"]!["sha256"] = new string('0', 64);
        using JsonDocument tamperedDocument = JsonDocument.Parse(
            JsonSerializer.SerializeToUtf8Bytes(tampered));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6SuccessorCredentialReplacementRunner.ValidateForegroundRecoveryOwner(
                repository, tamperedDocument.RootElement,
                "g-e6b6a3f21ad74108ba65955850349f83"));
    }

    [TestMethod]
    public void TypedFailureRecoveryOwnerBindsExactRetainedAttemptAndConsumedLaunch()
    {
        string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(AppContext.BaseDirectory);
        string ownerPath = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "m1-slice6-development-campaign-amendment.v6.json");
        byte[] ownerBytes = File.ReadAllBytes(ownerPath);
        ActiveRepositoryJsonSchemaValidator.Validate(ownerBytes,
            File.ReadAllBytes(Path.Combine(repository, "contracts", "repository",
                "m1-slice6-development-campaign-amendment.v6.schema.json")),
            M1Slice6SuccessorCredentialReplacementRunner.TypedFailureRecoveryAmendmentSchema);
        using JsonDocument owner = JsonDocument.Parse(ownerBytes);
        string productRoot = Path.Combine(
            repository, "artifacts", "m1-slice6", "successor-product-state");
        string checkpointBefore =
            M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(productRoot);
        M1Slice6SuccessorCredentialReplacementRunner.ValidateTypedFailureRecoveryOwner(
            repository, owner.RootElement, "g-e6b6a3f21ad74108ba65955850349f83");
        M1Slice6SuccessorCredentialReplacementRunner.ValidateTypedFailureRecoveryOwner(
            repository, owner.RootElement, "g-e6b6a3f21ad74108ba65955850349f83");
        Assert.AreEqual(checkpointBefore,
            M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(productRoot));

        JsonObject tampered = JsonNode.Parse(ownerBytes)!.AsObject();
        tampered["correction"]!["consumed_helper_launches"] = 2;
        using JsonDocument tamperedDocument = JsonDocument.Parse(
            JsonSerializer.SerializeToUtf8Bytes(tampered));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            M1Slice6SuccessorCredentialReplacementRunner.ValidateTypedFailureRecoveryOwner(
                repository, tamperedDocument.RootElement, "g-e6b6a3f21ad74108ba65955850349f83"));
    }

    [TestMethod]
    public async Task InitialNoNativeRunnerAtomicallyBeginsFreshReplacement()
    {
        string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(AppContext.BaseDirectory);
        string testRoot = Path.Combine(repository, "artifacts", "m1-slice6",
            "replacement-runner-initial-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            ReplacementFixture initial = CreateFixture(repository, testRoot, "initial-success", FixtureMode.Initial);
            int result = await M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                initial.AuthorityPath, initial.AuthoritySha256, initial.ReviewPath, initial.ReviewSha256,
                initial.ProductRoot, initial.LedgerPath, initial.HelperPath, initial.HelperSha256,
                initial.EvidencePath, CancellationToken.None, initial.Hooks(CompleteWithoutNative));
            Assert.AreEqual(0, result);
            using JsonDocument evidence = JsonDocument.Parse(File.ReadAllBytes(initial.EvidencePath));
            Assert.AreEqual("passed-active-verified-predecessor-absent",
                evidence.RootElement.GetProperty("status").GetString());
            CredentialProfileProjection activated = AuthoritativeStore.ReadCredentialProfileProjectionReadOnly(
                initial.ProductRoot, initial.ProfileId);
            Assert.AreEqual(initial.SuccessorGeneration, activated.GenerationId);
            Assert.AreEqual(2, activated.GenerationOrdinal);
            Assert.AreEqual("active-verified", activated.LifecycleState);
        }
        finally
        {
            if (Directory.Exists(testRoot)) { Directory.Delete(testRoot, recursive: true); }
        }
    }

    [TestMethod]
    public async Task BoundaryOnlyCrashRecoveryReplaysWithoutHelperLaunch()
    {
        string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(AppContext.BaseDirectory);
        string testRoot = Path.Combine(repository, "artifacts", "m1-slice6",
            "replacement-boundary-only-replay-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            ReplacementFixture fixture = CreateFixture(
                repository, testRoot, "boundary-only", FixtureMode.DeletePendingRecovery);
            using JsonDocument authorityDocument = JsonDocument.Parse(File.ReadAllBytes(fixture.AuthorityPath));
            string authorityId = authorityDocument.RootElement.GetProperty("authority_id").GetString()!;
            string operationId = "m1s6-credential-replacement-" + fixture.AuthoritySha256[..32];
            (_, HelperPrivateFrameV2 assignment) = CleanupFrames(
                fixture.ProfileId, fixture.SuccessorGeneration, operationId, authorityId);
            using (AuthoritativeStore store = new(new StoragePaths(fixture.ProductRoot)))
            {
                Assert.IsTrue(store.TryAdmitCredentialReplacementHelperLaunch(operationId, fixture.Now));
                CoordinatedHelperReceipt prepared = CompletedReplacementReceipt(
                    operationId, assignment, fixture.ProfileId, fixture.SuccessorGeneration,
                    fixture.HelperSha256);
                HelperPrivateFrameV2 terminal = new()
                {
                    Sequence = 3,
                    ProtocolFingerprintSha256 = Google.Protobuf.ByteString.CopyFrom(
                        Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
                    Receipt = prepared.Process.Receipt.Clone(),
                };
                byte[] canonical = HelperPrivateProtocolV2.Encode(terminal);
                CoordinatedHelperReceipt boundarySubject = prepared with
                {
                    Staging = new(
                        operationId, Path.Combine(operationId, "helper-receipt.v2.pb"),
                        canonical.Length, Sha(canonical), null, 0, null, true, true),
                };
                string predecessorFingerprint =
                    "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0";
                string successorFingerprint = Sha(Encoding.UTF8.GetBytes(
                    $"Infinium:{fixture.ProfileId}:{fixture.SuccessorGeneration}"));
                byte[] boundary = M1Slice6SuccessorCredentialReplacementRunner.CreateValidatedReplacementBoundary(
                    repository, operationId, boundarySubject,
                    predecessorFingerprint, successorFingerprint, canonical);
                _ = store.StageCredentialReplacementBoundary(operationId, boundary);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(fixture.EvidencePath)!);

            int result = await M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                fixture.AuthorityPath, fixture.AuthoritySha256, fixture.ReviewPath, fixture.ReviewSha256,
                fixture.ProductRoot, fixture.LedgerPath, fixture.HelperPath, fixture.HelperSha256,
                fixture.EvidencePath, CancellationToken.None,
                fixture.Hooks(CompleteWithoutNative) with { UtcNow = fixture.Now.AddHours(1) });

            Assert.AreEqual(0, result);
            Assert.IsTrue(File.Exists(fixture.EvidencePath));
            Assert.IsTrue(File.Exists(Path.Combine(
                fixture.ProductRoot, "staging", operationId, "helper-receipt.v2.pb")));
            CredentialProfileProjection projection = AuthoritativeStore.ReadCredentialProfileProjectionReadOnly(
                fixture.ProductRoot, fixture.ProfileId);
            Assert.AreEqual("active-verified", projection.LifecycleState);
            Assert.AreEqual(fixture.SuccessorGeneration, projection.GenerationId);
        }
        finally
        {
            if (Directory.Exists(testRoot)) { Directory.Delete(testRoot, recursive: true); }
        }
    }

    [TestMethod]
    public async Task RecoveryRejectsTamperedPriorOwnerAndFailureSemanticsBeforeEffect()
    {
        string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(AppContext.BaseDirectory);
        string testRoot = Path.Combine(repository, "artifacts", "m1-slice6",
            "replacement-runner-tamper-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            ReplacementFixture priorOwnerFixture = CreateFixture(repository, testRoot, "prior-owner-tamper");
            string ownerSource = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
                "m1-slice6-development-campaign-amendment.v4.json");
            JsonObject priorOwner = JsonNode.Parse(File.ReadAllBytes(ownerSource))!.AsObject();
            priorOwner["prior_owner_authority"]!["id"] = "infinium.m1-s6.credential-replacement/wrong-owner";
            string priorOwnerPath = Path.Combine(Path.GetDirectoryName(priorOwnerFixture.AuthorityPath)!,
                "tampered-prior-owner.v4.json");
            WriteNode(priorOwnerPath, priorOwner);
            priorOwnerFixture = RebindOwner(repository, priorOwnerFixture, priorOwnerPath);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                    priorOwnerFixture.AuthorityPath, priorOwnerFixture.AuthoritySha256,
                    priorOwnerFixture.ReviewPath, priorOwnerFixture.ReviewSha256,
                    priorOwnerFixture.ProductRoot, priorOwnerFixture.LedgerPath,
                    priorOwnerFixture.HelperPath, priorOwnerFixture.HelperSha256,
                    priorOwnerFixture.EvidencePath, CancellationToken.None,
                    priorOwnerFixture.Hooks(CompleteWithoutNative)));
            Assert.IsFalse(File.Exists(priorOwnerFixture.EvidencePath));

            ReplacementFixture failureFixture = CreateFixture(repository, testRoot, "failure-tamper");
            string failureSource = Path.Combine(repository, "artifacts", "m1-slice6",
                "successor-credential-replacement", "c2cf7e8c-dd55-4791-9eb1-f1e557f80124",
                "replacement-evidence.v1.json");
            JsonObject failure = JsonNode.Parse(File.ReadAllBytes(failureSource))!.AsObject();
            failure["typed_failure"] = "IOException";
            string failurePath = Path.Combine(Path.GetDirectoryName(failureFixture.AuthorityPath)!,
                "tampered-failure.v1.json");
            WriteNode(failurePath, failure);
            JsonObject failureOwner = JsonNode.Parse(File.ReadAllBytes(ownerSource))!.AsObject();
            failureOwner["retained_failure"]!["path"] = Relative(repository, failurePath);
            failureOwner["retained_failure"]!["sha256"] = HashFile(failurePath);
            string failureOwnerPath = Path.Combine(Path.GetDirectoryName(failureFixture.AuthorityPath)!,
                "tampered-failure-owner.v4.json");
            WriteNode(failureOwnerPath, failureOwner);
            failureFixture = RebindOwner(repository, failureFixture, failureOwnerPath);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                    failureFixture.AuthorityPath, failureFixture.AuthoritySha256,
                    failureFixture.ReviewPath, failureFixture.ReviewSha256,
                    failureFixture.ProductRoot, failureFixture.LedgerPath,
                    failureFixture.HelperPath, failureFixture.HelperSha256,
                    failureFixture.EvidencePath, CancellationToken.None,
                    failureFixture.Hooks(CompleteWithoutNative)));
            Assert.IsFalse(File.Exists(failureFixture.EvidencePath));
        }
        finally
        {
            if (Directory.Exists(testRoot)) { Directory.Delete(testRoot, recursive: true); }
        }
    }

    [TestMethod]
    public async Task NoNativeRunnerClosesSuccessPreflightAndPostAdmissionFailureEvidence()
    {
        string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(AppContext.BaseDirectory);
        string testRoot = Path.Combine(repository, "artifacts", "m1-slice6",
            "replacement-runner-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            ReplacementFixture success = CreateFixture(repository, testRoot, "success");
            using (JsonDocument authority = JsonDocument.Parse(File.ReadAllBytes(success.AuthorityPath)))
            {
                _ = OneShotCredentialHelperLauncher.CreateSuccessorCredentialReplacement(
                    success.HelperPath, success.HelperSha256, success.AuthorityPath,
                    success.AuthoritySha256, authority.RootElement.GetProperty("authority_id").GetString()!);
            }
            int result = await M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                success.AuthorityPath, success.AuthoritySha256, success.ReviewPath, success.ReviewSha256,
                success.ProductRoot, success.LedgerPath, success.HelperPath, success.HelperSha256,
                success.EvidencePath, CancellationToken.None, success.Hooks(CompleteWithoutNative));
            Assert.AreEqual(0, result);
            using (JsonDocument evidence = JsonDocument.Parse(File.ReadAllBytes(success.EvidencePath)))
            {
                Assert.AreEqual("passed-active-verified-predecessor-absent",
                    evidence.RootElement.GetProperty("status").GetString());
                Assert.AreEqual(11,
                    evidence.RootElement.GetProperty("effect").GetProperty("native_call_trace").GetArrayLength());
            }
            CredentialProfileProjection activated = AuthoritativeStore.ReadCredentialProfileProjectionReadOnly(
                success.ProductRoot, success.ProfileId);
            Assert.AreEqual(success.SuccessorGeneration, activated.GenerationId);
            Assert.AreEqual("active-verified", activated.LifecycleState);

            ReplacementFixture absent = CreateFixture(repository, testRoot, "predecessor-already-absent");
            int absentResult = await M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                absent.AuthorityPath, absent.AuthoritySha256, absent.ReviewPath, absent.ReviewSha256,
                absent.ProductRoot, absent.LedgerPath, absent.HelperPath, absent.HelperSha256,
                absent.EvidencePath, CancellationToken.None, absent.Hooks(CompleteAlreadyAbsentWithoutNative));
            Assert.AreEqual(0, absentResult);
            using (JsonDocument absentEvidence = JsonDocument.Parse(File.ReadAllBytes(absent.EvidencePath)))
            {
                Assert.AreEqual(5,
                    absentEvidence.RootElement.GetProperty("effect").GetProperty("native_call_trace").GetArrayLength());
            }

            string unreviewed = Path.Combine(testRoot, "unreviewed", "must-not-exist.json");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                    success.AuthorityPath, new string('0', 64), success.ReviewPath, success.ReviewSha256,
                    success.ProductRoot, success.LedgerPath, success.HelperPath, success.HelperSha256,
                    unreviewed, CancellationToken.None, success.Hooks(CompleteWithoutNative)));
            Assert.IsFalse(File.Exists(unreviewed));
            Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(unreviewed)!));

            ReplacementFixture stopped = CreateFixture(repository, testRoot, "stopped");
            int stoppedResult = await M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                stopped.AuthorityPath, stopped.AuthoritySha256, stopped.ReviewPath, stopped.ReviewSha256,
                stopped.ProductRoot, stopped.LedgerPath, stopped.HelperPath, stopped.HelperSha256,
                stopped.EvidencePath, CancellationToken.None, stopped.Hooks(StopWithoutNative));
            Assert.AreEqual(2, stoppedResult);
            using (JsonDocument stoppedEvidence = JsonDocument.Parse(File.ReadAllBytes(stopped.EvidencePath)))
            {
                Assert.AreEqual("stopped-non-dispatchable-recovery-required",
                    stoppedEvidence.RootElement.GetProperty("status").GetString());
                Assert.AreEqual("Cancelled",
                    stoppedEvidence.RootElement.GetProperty("effect").GetProperty("helper_outcome").GetString());
                Assert.AreEqual(0,
                    stoppedEvidence.RootElement.GetProperty("effect").GetProperty("native_call_trace").GetArrayLength());
            }

            ReplacementFixture collision = CreateFixture(repository, testRoot, "successor-collision");
            int collisionResult = await M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                collision.AuthorityPath, collision.AuthoritySha256, collision.ReviewPath, collision.ReviewSha256,
                collision.ProductRoot, collision.LedgerPath, collision.HelperPath, collision.HelperSha256,
                collision.EvidencePath, CancellationToken.None, collision.Hooks(CollideWithoutNative));
            Assert.AreEqual(2, collisionResult);
            using (JsonDocument collisionEvidence = JsonDocument.Parse(File.ReadAllBytes(collision.EvidencePath)))
            {
                Assert.IsTrue(collisionEvidence.RootElement.GetProperty("effect")
                    .GetProperty("namespace_reuse_blocked").GetBoolean());
                Assert.AreEqual(2, collisionEvidence.RootElement.GetProperty("effect")
                    .GetProperty("native_call_trace").GetArrayLength());
            }

            ReplacementFixture contradictory = CreateFixture(repository, testRoot, "contradictory-active-stop");
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                    contradictory.AuthorityPath, contradictory.AuthoritySha256,
                    contradictory.ReviewPath, contradictory.ReviewSha256,
                    contradictory.ProductRoot, contradictory.LedgerPath,
                    contradictory.HelperPath, contradictory.HelperSha256,
                    contradictory.EvidencePath, CancellationToken.None,
                    contradictory.Hooks(ContradictoryActiveStopWithoutNative)));
            using (JsonDocument contradictoryEvidence = JsonDocument.Parse(
                File.ReadAllBytes(contradictory.EvidencePath)))
            {
                Assert.AreEqual(M1Slice6SuccessorCredentialReplacementRunner.FailureEvidenceSchema,
                    contradictoryEvidence.RootElement.GetProperty("schema_identity").GetString());
                Assert.AreNotEqual("stopped-non-dispatchable-recovery-required",
                    contradictoryEvidence.RootElement.GetProperty("status").GetString());
            }

            ReplacementFixture failed = CreateFixture(repository, testRoot, "post-admission-failure");
            await Assert.ThrowsExactlyAsync<IOException>(() =>
                M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                    failed.AuthorityPath, failed.AuthoritySha256, failed.ReviewPath, failed.ReviewSha256,
                    failed.ProductRoot, failed.LedgerPath, failed.HelperPath, failed.HelperSha256,
                    failed.EvidencePath, CancellationToken.None,
                    failed.Hooks((_, _, _, _, _, _) => throw new IOException("synthetic-after-admission"))));
            using JsonDocument retained = JsonDocument.Parse(File.ReadAllBytes(failed.EvidencePath));
            Assert.AreEqual("stopped-ambiguous-effect-recovery-required",
                retained.RootElement.GetProperty("status").GetString());
            Assert.AreEqual("fallback-fields-are-secret-free-effect-isolation-unverified-stop-condition",
                retained.RootElement.GetProperty("isolation_observation").GetString());
            Assert.AreEqual("replacing",
                retained.RootElement.GetProperty("product_state").GetProperty("lifecycle_state").GetString());
        }
        finally
        {
            if (Directory.Exists(testRoot)) { Directory.Delete(testRoot, recursive: true); }
        }
    }

    [TestMethod]
    public async Task DeletePendingRecoveryUsesExactReplaceWithoutSecondBeginAndRetainsStoppedState()
    {
        string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(AppContext.BaseDirectory);
        string testRoot = Path.Combine(repository, "artifacts", "m1-slice6",
            "replacement-runner-delete-pending-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            ReplacementFixture success = CreateFixture(
                repository, testRoot, "success", FixtureMode.DeletePendingRecovery);
            int result = await M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                success.AuthorityPath, success.AuthoritySha256, success.ReviewPath, success.ReviewSha256,
                success.ProductRoot, success.LedgerPath, success.HelperPath, success.HelperSha256,
                success.EvidencePath, CancellationToken.None, success.Hooks(CompleteCleanupWithoutNative));
            Assert.AreEqual(0, result);
            CredentialProfileProjection active = AuthoritativeStore.ReadCredentialProfileProjectionReadOnly(
                success.ProductRoot, success.ProfileId);
            Assert.AreEqual(success.SuccessorGeneration, active.GenerationId);
            Assert.AreEqual(2, active.GenerationOrdinal);
            Assert.AreEqual("active-verified", active.LifecycleState);
            using (JsonDocument evidence = JsonDocument.Parse(File.ReadAllBytes(success.EvidencePath)))
            {
                Assert.AreEqual("passed-active-verified-predecessor-absent",
                    evidence.RootElement.GetProperty("status").GetString());
            }

            ReplacementFixture stopped = CreateFixture(
                repository, testRoot, "stopped", FixtureMode.DeletePendingRecovery);
            int stoppedResult = await M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                stopped.AuthorityPath, stopped.AuthoritySha256, stopped.ReviewPath, stopped.ReviewSha256,
                stopped.ProductRoot, stopped.LedgerPath, stopped.HelperPath, stopped.HelperSha256,
                stopped.EvidencePath, CancellationToken.None, stopped.Hooks(StopCleanupFailedKnownWithoutNative));
            Assert.AreEqual(2, stoppedResult);
            CredentialProfileProjection unchanged = AuthoritativeStore.ReadCredentialProfileProjectionReadOnly(
                stopped.ProductRoot, stopped.ProfileId);
            Assert.AreEqual("delete-pending", unchanged.LifecycleState);
            Assert.AreEqual(1, unchanged.GenerationOrdinal);
            using (JsonDocument evidence = JsonDocument.Parse(File.ReadAllBytes(stopped.EvidencePath)))
            {
                Assert.AreEqual("FailedKnown",
                    evidence.RootElement.GetProperty("effect").GetProperty("helper_outcome").GetString());
                Assert.AreEqual(0,
                    evidence.RootElement.GetProperty("effect").GetProperty("native_call_trace").GetArrayLength());
            }

            ReplacementFixture ambiguous = CreateFixture(
                repository, testRoot, "ambiguous", FixtureMode.DeletePendingRecovery);
            await Assert.ThrowsExactlyAsync<IOException>(() =>
                M1Slice6SuccessorCredentialReplacementRunner.RunAsync(
                    ambiguous.AuthorityPath, ambiguous.AuthoritySha256,
                    ambiguous.ReviewPath, ambiguous.ReviewSha256,
                    ambiguous.ProductRoot, ambiguous.LedgerPath,
                    ambiguous.HelperPath, ambiguous.HelperSha256,
                    ambiguous.EvidencePath, CancellationToken.None,
                    ambiguous.Hooks((_, _, _, _, _, _) => throw new IOException("synthetic-cleanup-ambiguity"))));
            CredentialProfileProjection retained = AuthoritativeStore.ReadCredentialProfileProjectionReadOnly(
                ambiguous.ProductRoot, ambiguous.ProfileId);
            Assert.AreEqual("delete-pending", retained.LifecycleState);
            using JsonDocument fallback = JsonDocument.Parse(File.ReadAllBytes(ambiguous.EvidencePath));
            Assert.AreEqual("delete-pending",
                fallback.RootElement.GetProperty("product_state").GetProperty("lifecycle_state").GetString());
        }
        finally
        {
            if (Directory.Exists(testRoot)) { Directory.Delete(testRoot, recursive: true); }
        }
    }

    [TestMethod]
    public async Task CleanupCoordinatorStagesValidatesReplaysAndRefusesRelaunch()
    {
        string repository = M1Slice6SuccessorAuthorityLoader.FindRepositoryRoot(AppContext.BaseDirectory);
        string testRoot = Path.Combine(repository, "artifacts", "m1-slice6",
            "replacement-cleanup-coordinator-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            ReplacementFixture fixture = CreateFixture(
                repository, testRoot, "complete", FixtureMode.DeletePendingRecovery);
            (HelperPrivateFrameV2 bootstrap, HelperPrivateFrameV2 assignment) =
                CleanupFrames(fixture.ProfileId, fixture.SuccessorGeneration, "cleanup-complete");
            using (AuthoritativeStore store = new(new StoragePaths(fixture.ProductRoot)))
            {
                Assert.IsTrue(store.TryAdmitCredentialReplacementHelperLaunch("cleanup-complete", fixture.Now));
                CoordinatedHelperReceipt prepared = CompletedReplacementReceipt(
                    "cleanup-complete", assignment, fixture.ProfileId, fixture.SuccessorGeneration,
                    fixture.HelperSha256);
                CoordinatedHelperReceipt staged = StageReceipt(
                    store, "cleanup-complete", prepared, fixture.Now);
                OneShotCredentialHelperLauncher launcher = new(
                    fixture.HelperPath, fixture.HelperSha256, Path.Combine(testRoot, "synthetic-store-complete"));
                CredentialHelperCoordinator coordinator = new(store, launcher);
                (CoordinatedHelperReceipt _, CredentialProfileProjection published) =
                    coordinator.CompleteVerifiedReplacementCleanup(
                        repository, "cleanup-complete", bootstrap, assignment, staged, fixture.Now);
                Assert.AreEqual("active-verified", published.LifecycleState);
                Assert.IsTrue(File.Exists(Path.Combine(store.Paths.Staging, "cleanup-complete",
                    AuthoritativeStore.CredentialReplacementBoundaryFileName)));
                (CoordinatedHelperReceipt _, CredentialProfileProjection replayed) =
                    coordinator.RecoverVerifiedReplacementCleanup(
                        repository, "cleanup-complete", assignment,
                        "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0",
                        Sha(Encoding.UTF8.GetBytes(
                            $"Infinium:{fixture.ProfileId}:{fixture.SuccessorGeneration}")),
                        fixture.HelperSha256,
                        fixture.Now.AddMinutes(40));
                Assert.AreEqual(published, replayed);
            }

            ReplacementFixture recover = CreateFixture(
                repository, testRoot, "recover-before-publish", FixtureMode.DeletePendingRecovery);
            (HelperPrivateFrameV2 recoverBootstrap, HelperPrivateFrameV2 recoverAssignment) =
                CleanupFrames(recover.ProfileId, recover.SuccessorGeneration, "cleanup-recover");
            using (AuthoritativeStore store = new(new StoragePaths(recover.ProductRoot)))
            {
                Assert.IsTrue(store.TryAdmitCredentialReplacementHelperLaunch("cleanup-recover", recover.Now));
                CoordinatedHelperReceipt prepared = CompletedReplacementReceipt(
                    "cleanup-recover", recoverAssignment, recover.ProfileId, recover.SuccessorGeneration,
                    recover.HelperSha256);
                HelperPrivateFrameV2 terminal = new()
                {
                    Sequence = 3,
                    ProtocolFingerprintSha256 = Google.Protobuf.ByteString.CopyFrom(
                        Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
                    Receipt = prepared.Process.Receipt.Clone(),
                };
                byte[] canonical = HelperPrivateProtocolV2.Encode(terminal);
                CoordinatedHelperReceipt staged = prepared with
                {
                    Staging = new(
                        "cleanup-recover", Path.Combine("cleanup-recover", "helper-receipt.v2.pb"),
                        canonical.Length, Sha(canonical), null, 0, null, true, true),
                };
                string predecessorFingerprint =
                    "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0";
                string successorFingerprint = Sha(Encoding.UTF8.GetBytes(
                    $"Infinium:{recover.ProfileId}:{recover.SuccessorGeneration}"));
                byte[] boundary = M1Slice6SuccessorCredentialReplacementRunner.CreateValidatedReplacementBoundary(
                    repository, "cleanup-recover", staged, predecessorFingerprint, successorFingerprint, canonical);
                _ = store.StageCredentialReplacementBoundary("cleanup-recover", boundary);
                string receiptRelative = Path.Combine("cleanup-recover", "helper-receipt.v2.pb");
                using (AttemptStagingAuthority stagingAuthority =
                    store.Paths.CreateAttemptStagingDirectory("cleanup-recover"))
                using (FileStream receipt = store.Paths.CreateNewFile(
                    ProductWriteClass.AttemptStaging, receiptRelative))
                {
                    receipt.Write(canonical);
                    receipt.Flush(flushToDisk: true);
                }
                Assert.AreEqual(0, store.HelperReceiptAdmissionCount("cleanup-recover"));
                OneShotCredentialHelperLauncher launcher = new(
                    recover.HelperPath, recover.HelperSha256, Path.Combine(testRoot, "synthetic-store-recover"));
                CredentialHelperCoordinator coordinator = new(store, launcher);
                (CoordinatedHelperReceipt _, CredentialProfileProjection published) =
                    coordinator.RecoverVerifiedReplacementCleanup(
                        repository, "cleanup-recover", recoverAssignment,
                        predecessorFingerprint, successorFingerprint, recover.HelperSha256,
                        recover.Now.AddMinutes(40));
                Assert.AreEqual("active-verified", published.LifecycleState);
                Assert.IsTrue(File.Exists(Path.Combine(
                    store.Paths.Staging, "cleanup-recover", "helper-receipt.v2.pb")));
                Assert.AreEqual(1, store.HelperReceiptAdmissionCount("cleanup-recover"));
            }

            ReplacementFixture unverified = CreateFixture(
                repository, testRoot, "recover-before-verify", FixtureMode.DeletePendingRecovery);
            (HelperPrivateFrameV2 unverifiedBootstrap, HelperPrivateFrameV2 unverifiedAssignment) =
                CleanupFrames(unverified.ProfileId, unverified.SuccessorGeneration, "cleanup-unverified");
            using (AuthoritativeStore store = new(new StoragePaths(unverified.ProductRoot)))
            {
                Assert.IsTrue(store.TryAdmitCredentialReplacementHelperLaunch("cleanup-unverified", unverified.Now));
                CoordinatedHelperReceipt prepared = CompletedReplacementReceipt(
                    "cleanup-unverified", unverifiedAssignment, unverified.ProfileId,
                    unverified.SuccessorGeneration, unverified.HelperSha256);
                CoordinatedHelperReceipt staged = StageReceipt(
                    store, "cleanup-unverified", prepared, unverified.Now);
                string predecessorFingerprint =
                    "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0";
                string successorFingerprint = Sha(Encoding.UTF8.GetBytes(
                    $"Infinium:{unverified.ProfileId}:{unverified.SuccessorGeneration}"));
                byte[] boundary = M1Slice6SuccessorCredentialReplacementRunner.CreateValidatedReplacementBoundary(
                    repository, "cleanup-unverified", staged,
                    predecessorFingerprint, successorFingerprint);
                _ = store.StageCredentialReplacementBoundary("cleanup-unverified", boundary);
                CredentialProfileProjection old = store.GetCredentialProfile(unverified.ProfileId);
                CredentialProfileProjection intermediate = store.ApplyCredentialTransition(new(
                    "cleanup-unverified-replacement-cleanup-recovered",
                    old.ProfileId, unverified.SuccessorGeneration, "recover",
                    "delete-pending", "active-unverified", "active-unverified",
                    old.CapabilitySnapshotId, old.AccountIdentityId, old.BillingScopeIdentityId,
                    unverified.Now.AddTicks(3), unverified.Now.AddTicks(4)));
                Assert.AreEqual("active-unverified", intermediate.LifecycleState);
                CredentialHelperCoordinator coordinator = new(store);
                (CoordinatedHelperReceipt _, CredentialProfileProjection published) =
                    coordinator.RecoverVerifiedReplacementCleanup(
                        repository, "cleanup-unverified", unverifiedAssignment,
                        predecessorFingerprint, successorFingerprint, unverified.HelperSha256,
                        unverified.Now.AddMinutes(40));
                Assert.AreEqual("active-verified", published.LifecycleState);
                Assert.AreEqual(unverified.SuccessorGeneration, published.GenerationId);
            }

            ReplacementFixture stopped = CreateFixture(
                repository, testRoot, "stopped", FixtureMode.DeletePendingRecovery);
            (HelperPrivateFrameV2 stoppedBootstrap, HelperPrivateFrameV2 stoppedAssignment) =
                CleanupFrames(stopped.ProfileId, stopped.SuccessorGeneration, "cleanup-stopped");
            using (AuthoritativeStore store = new(new StoragePaths(stopped.ProductRoot)))
            {
                Assert.IsTrue(store.TryAdmitCredentialReplacementHelperLaunch("cleanup-stopped", stopped.Now));
                (CoordinatedHelperReceipt prepared, _) = await StopCleanupFailedKnownWithoutNative(
                    store, "cleanup-stopped", stoppedBootstrap, stoppedAssignment,
                    stopped.Now, CancellationToken.None);
                prepared = prepared with
                {
                    Process = prepared.Process with { BinarySha256 = stopped.HelperSha256 },
                };
                CoordinatedHelperReceipt staged = StageReceipt(
                    store, "cleanup-stopped", prepared, stopped.Now);
                string predecessorFingerprint =
                    "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0";
                string successorFingerprint = Sha(Encoding.UTF8.GetBytes(
                    $"Infinium:{stopped.ProfileId}:{stopped.SuccessorGeneration}"));
                OneShotCredentialHelperLauncher launcher = new(
                    stopped.HelperPath, stopped.HelperSha256, Path.Combine(testRoot, "synthetic-store-stopped"));
                CredentialHelperCoordinator coordinator = new(store, launcher);
                (CoordinatedHelperReceipt _, CredentialProfileProjection unchanged) =
                    coordinator.CompleteVerifiedReplacementCleanup(
                        repository, "cleanup-stopped", stoppedBootstrap,
                        stoppedAssignment, staged, stopped.Now);
                Assert.AreEqual("delete-pending", unchanged.LifecycleState);
                Assert.AreEqual("unavailable", unchanged.VerificationState);
                Assert.AreEqual("failed", unchanged.CleanupDisposition);

                (CoordinatedHelperReceipt replayed, CredentialProfileProjection replayProjection) =
                    coordinator.RecoverVerifiedReplacementCleanup(
                        repository, "cleanup-stopped", stoppedAssignment,
                        predecessorFingerprint, successorFingerprint, stopped.HelperSha256,
                        stopped.Now.AddMinutes(40));
                Assert.AreEqual(HelperOutcomeV2.FailedKnown, replayed.Process.Receipt.Outcome);
                Assert.AreEqual(unchanged, replayProjection);
            }

            string midTracePredecessor =
                "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0";
            string midTraceSuccessor = Sha(Encoding.UTF8.GetBytes(
                $"Infinium:{stopped.ProfileId}:{stopped.SuccessorGeneration}"));
            M1Slice6SuccessorCredentialReplacementRunner.ValidateReplacementHelperBoundary(
                MidTraceReadFailureProcess(midTraceSuccessor, forgedAllocation: false),
                midTracePredecessor, midTraceSuccessor, requireCompleted: false);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                M1Slice6SuccessorCredentialReplacementRunner.ValidateReplacementHelperBoundary(
                    MidTraceReadFailureProcess(midTraceSuccessor, forgedAllocation: true),
                    midTracePredecessor, midTraceSuccessor, requireCompleted: false));
            foreach (string malformed in MalformedWin32Errors)
            {
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    M1Slice6SuccessorCredentialReplacementRunner.ValidateReplacementHelperBoundary(
                        MidTraceReadFailureProcess(
                            midTraceSuccessor, forgedAllocation: false, failureResult: malformed),
                        midTracePredecessor, midTraceSuccessor, requireCompleted: false));
            }

            ReplacementFixture failureEnvelope = CreateFixture(
                repository, testRoot, "failure-envelope", FixtureMode.DeletePendingRecovery);
            using JsonDocument failureAuthority = JsonDocument.Parse(
                File.ReadAllBytes(failureEnvelope.AuthorityPath));
            (HelperPrivateFrameV2 failureBootstrap, HelperPrivateFrameV2 failureAssignment) = CleanupFrames(
                failureEnvelope.ProfileId, failureEnvelope.SuccessorGeneration,
                "cleanup-failure-envelope", failureAuthority.RootElement
                    .GetProperty("authority_id").GetString());
            HelperProcessReceipt failureProcess = MidTraceReadFailureProcess(
                Sha(Encoding.UTF8.GetBytes(
                    $"Infinium:{failureEnvelope.ProfileId}:{failureEnvelope.SuccessorGeneration}")),
                forgedAllocation: false);
            NativeHelperFailureEnvelope exactFailure = new(
                "engine-execution", "win32-failure", true,
                CredWriteW: 1, CredReadW: 2, CredDeleteW: 0, CredFree: 0, Total: 3,
                NetworkFactsKnown: true, ListenerCount: 0, NetworkOperationCount: 0,
                ExternalEffectFactsKnown: true, DnsOperationCount: 0,
                ProviderOperationCount: 0, BillableOperationCount: 0,
                Encoding.UTF8.GetString(failureProcess.NativeCallTraceBytes!),
                Encoding.UTF8.GetString(failureProcess.NativeEntryCleanupBytes!),
                JsonSerializer.Serialize(Canary("private protocol partial response")),
                ManualUiAttempted: true, ContainmentDescendantStarted: true,
                ContainmentDescendantProcessId: 2,
                NamespaceReuseBlocked: false, NamespaceReuseBlockReason: null,
                ContainmentProbeExecuted: true, ExcludedHandleAccessible: false);
            CredentialNativeQualificationSupervisor.ValidateNativeHelperFailureEnvelope(
                exactFailure, failureBootstrap.Bootstrap, failureAssignment.Assignment,
                failureEnvelope.AuthorityPath, helperProcessId: 1);
            HelperAssignmentV2 wrongReplacement = failureAssignment.Assignment.Clone();
            wrongReplacement.AssignmentId += "-drift";
            Assert.ThrowsExactly<InvalidDataException>(() =>
                CredentialNativeQualificationSupervisor.ValidateNativeHelperFailureEnvelope(
                    exactFailure, failureBootstrap.Bootstrap, wrongReplacement,
                    failureEnvelope.AuthorityPath, helperProcessId: 1));
            HelperAssignmentV2 wrongCommand = failureAssignment.Assignment.Clone();
            wrongCommand.CommandId += "-drift";
            Assert.ThrowsExactly<InvalidDataException>(() =>
                CredentialNativeQualificationSupervisor.ValidateNativeHelperFailureEnvelope(
                    exactFailure, failureBootstrap.Bootstrap, wrongCommand,
                    failureEnvelope.AuthorityPath, helperProcessId: 1));
            HelperBootstrapV2 wrongPredecessor = failureBootstrap.Bootstrap.Clone();
            wrongPredecessor.Credential.GenerationId.Value += "-drift";
            Assert.ThrowsExactly<InvalidDataException>(() =>
                CredentialNativeQualificationSupervisor.ValidateNativeHelperFailureEnvelope(
                    exactFailure, wrongPredecessor, failureAssignment.Assignment,
                    failureEnvelope.AuthorityPath, helperProcessId: 1));
            using (AuthoritativeStore store = new(new StoragePaths(failureEnvelope.ProductRoot)))
            {
                CredentialNativeHelperFailureException typedFailure = new(
                    exactFailure, failureAssignment.Assignment.AssignmentId);
                typedFailure.AttachContainment(new(
                    ProcessId: 71,
                    ExitCode: 72,
                    TotalContainedProcessCount: 2,
                    ActiveProcessCountBeforeJobClose: 1,
                    ProcessTreeSurvivorCount: 0,
                    ProcessTreeTerminated: true));
                OneShotCredentialHelperLauncher launcher =
                    OneShotCredentialHelperLauncher.CreateSuccessorCredentialReplacement(
                        failureEnvelope.HelperPath, failureEnvelope.HelperSha256,
                        failureEnvelope.AuthorityPath, failureEnvelope.AuthoritySha256,
                        failureAuthority.RootElement.GetProperty("authority_id").GetString()!);
                CredentialHelperCoordinator coordinator = new(store, launcher);
                (CoordinatedHelperReceipt stoppedHelper, CredentialProfileProjection stoppedProjection) =
                    await coordinator.ExecuteVerifiedReplacementCleanupAsync(
                        "cleanup-failure-envelope", failureBootstrap, failureAssignment,
                        failureEnvelope.Now,
                        _ => Task.FromException<HelperProcessReceipt>(typedFailure),
                        cancellationToken: CancellationToken.None);
                Assert.AreEqual(72, stoppedHelper.Process.ExitCode);
                Assert.AreEqual(HelperOutcomeV2.FailedKnown, stoppedHelper.Process.Receipt.Outcome);
                Assert.IsNotNull(stoppedHelper.ValidatedNativeFailureEnvelopeBytes);
                Assert.AreEqual("delete-pending", stoppedProjection.LifecycleState);
                string boundaryPath = Path.Combine(
                    store.Paths.Staging, "cleanup-failure-envelope",
                    AuthoritativeStore.CredentialReplacementBoundaryFileName);
                using JsonDocument boundary = JsonDocument.Parse(File.ReadAllBytes(boundaryPath));
                Assert.AreEqual(
                    "validated-native-failure-envelope",
                    boundary.RootElement.GetProperty("terminal_origin").GetString());
                Assert.AreEqual(
                    "engine-execution",
                    boundary.RootElement.GetProperty("validated_failure_envelope")
                        .GetProperty("stage").GetString());
                byte[] exactBoundaryBytes = File.ReadAllBytes(boundaryPath);
                void AssertEnvelopeProcessTamperRejected(NativeHelperFailureEnvelope tampered)
                {
                    JsonObject tamperedBoundary = JsonNode.Parse(exactBoundaryBytes)!.AsObject();
                    JsonObject failureNode = tamperedBoundary["validated_failure_envelope"]!.AsObject();
                    byte[] tamperedEnvelope = NativeHelperFailureProtocol.EncodeCanonical(tampered);
                    failureNode["sha256"] = Sha(tamperedEnvelope);
                    failureNode["base64"] = Convert.ToBase64String(tamperedEnvelope);
                    failureNode["stage"] = tampered.Stage;
                    failureNode["reason"] = tampered.Reason;
                    failureNode["network_facts_known"] = tampered.NetworkFactsKnown;
                    failureNode["external_effect_facts_known"] = tampered.ExternalEffectFactsKnown;
                    failureNode["dns_operation_count"] = tampered.DnsOperationCount;
                    failureNode["provider_operation_count"] = tampered.ProviderOperationCount;
                    failureNode["billable_operation_count"] = tampered.BillableOperationCount;
                    File.WriteAllBytes(boundaryPath,
                        JsonSerializer.SerializeToUtf8Bytes(tamperedBoundary, Json));
                    Assert.ThrowsExactly<InvalidDataException>(() =>
                        coordinator.RecoverVerifiedReplacementCleanup(
                            repository, "cleanup-failure-envelope", failureAssignment,
                            midTracePredecessor, midTraceSuccessor, failureEnvelope.HelperSha256,
                            failureEnvelope.Now.AddHours(2)));
                    File.WriteAllBytes(boundaryPath, exactBoundaryBytes);
                }
                AssertEnvelopeProcessTamperRejected(exactFailure with
                {
                    ContainmentProbeExecuted = false,
                    ExcludedHandleAccessible = false,
                });
                AssertEnvelopeProcessTamperRejected(exactFailure with
                {
                    CredReadW = 1,
                    Total = 2,
                });
                JsonObject alteredCanaries = JsonNode.Parse(exactFailure.CanaryEvidenceJson!)!.AsObject();
                alteredCanaries["ScannedSurfaces"]!.AsArray()[1]!["ByteCount"] = 2;
                AssertEnvelopeProcessTamperRejected(exactFailure with
                {
                    CanaryEvidenceJson = alteredCanaries.ToJsonString(),
                });
                AssertEnvelopeProcessTamperRejected(exactFailure with
                {
                    NamespaceReuseBlocked = true,
                    NamespaceReuseBlockReason = "preflight-collision",
                });
                (CoordinatedHelperReceipt replayed, CredentialProfileProjection replayedProjection) =
                    coordinator.RecoverVerifiedReplacementCleanup(
                        repository, "cleanup-failure-envelope", failureAssignment,
                        midTracePredecessor, midTraceSuccessor, failureEnvelope.HelperSha256,
                        failureEnvelope.Now.AddHours(2));
                Assert.AreEqual(72, replayed.Process.ExitCode);
                CollectionAssert.AreEqual(
                    stoppedHelper.ValidatedNativeFailureEnvelopeBytes!,
                    replayed.ValidatedNativeFailureEnvelopeBytes!);
                Assert.AreEqual(stoppedProjection, replayedProjection);
            }

            ReplacementFixture launchGuard = CreateFixture(
                repository, testRoot, "launch-guard", FixtureMode.DeletePendingRecovery);
            (HelperPrivateFrameV2 guardBootstrap, HelperPrivateFrameV2 guardAssignment) =
                CleanupFrames(launchGuard.ProfileId, launchGuard.SuccessorGeneration, "cleanup-launch-guard");
            using (AuthoritativeStore store = new(new StoragePaths(launchGuard.ProductRoot)))
            {
                CoordinatedHelperReceipt prepared = CompletedReplacementReceipt(
                    "cleanup-launch-guard", guardAssignment, launchGuard.ProfileId,
                    launchGuard.SuccessorGeneration, launchGuard.HelperSha256);
                CredentialHelperCoordinator coordinator = new(store);
                int executions = 0;
                Task<HelperProcessReceipt> Effect(CancellationToken _)
                {
                    executions++;
                    return Task.FromResult(prepared.Process);
                }
                await Assert.ThrowsExactlyAsync<IOException>(() =>
                    coordinator.ExecuteVerifiedReplacementCleanupAsync(
                        "cleanup-launch-guard", guardBootstrap, guardAssignment, launchGuard.Now,
                        Effect, testInterruptAfterEffect: true, CancellationToken.None));
                Assert.AreEqual(1, executions);
                Assert.IsTrue(store.HasExactCredentialReplacementHelperLaunchAdmission("cleanup-launch-guard"));
                Assert.IsFalse(File.Exists(Path.Combine(store.Paths.Staging, "cleanup-launch-guard",
                    AuthoritativeStore.CredentialReplacementBoundaryFileName)));
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                    coordinator.ExecuteVerifiedReplacementCleanupAsync(
                        "cleanup-launch-guard", guardBootstrap, guardAssignment, launchGuard.Now,
                        Effect, cancellationToken: CancellationToken.None));
                Assert.AreEqual(1, executions);
            }

            ReplacementFixture digestMismatch = CreateFixture(
                repository, testRoot, "digest-mismatch", FixtureMode.DeletePendingRecovery);
            (HelperPrivateFrameV2 digestBootstrap, HelperPrivateFrameV2 digestAssignment) =
                CleanupFrames(
                    digestMismatch.ProfileId, digestMismatch.SuccessorGeneration,
                    "cleanup-digest-mismatch");
            using (AuthoritativeStore store = new(new StoragePaths(digestMismatch.ProductRoot)))
            {
                CoordinatedHelperReceipt prepared = CompletedReplacementReceipt(
                    "cleanup-digest-mismatch", digestAssignment, digestMismatch.ProfileId,
                    digestMismatch.SuccessorGeneration, digestMismatch.HelperSha256);
                HelperReceiptV2 receipt = prepared.Process.Receipt.Clone();
                receipt.NonSecretReceipt.Value = Google.Protobuf.ByteString.CopyFrom(new byte[32]);
                HelperProcessReceipt mismatched = prepared.Process with { Receipt = receipt };
                CredentialHelperCoordinator coordinator = new(store);
                await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                    coordinator.ExecuteVerifiedReplacementCleanupAsync(
                        "cleanup-digest-mismatch", digestBootstrap, digestAssignment,
                        digestMismatch.Now, _ => Task.FromResult(mismatched),
                        cancellationToken: CancellationToken.None));
                Assert.AreEqual(
                    "delete-pending",
                    store.GetCredentialProfile(digestMismatch.ProfileId).LifecycleState);
                string stagingRoot = Path.Combine(store.Paths.Staging, "cleanup-digest-mismatch");
                Assert.IsFalse(File.Exists(Path.Combine(
                    stagingRoot, AuthoritativeStore.CredentialReplacementBoundaryFileName)));
                Assert.IsFalse(File.Exists(Path.Combine(stagingRoot, "helper-receipt.v2.pb")));
            }

            ReplacementFixture preadmitted = CreateFixture(
                repository, testRoot, "launch-preadmitted", FixtureMode.DeletePendingRecovery);
            (HelperPrivateFrameV2 preadmittedBootstrap, HelperPrivateFrameV2 preadmittedAssignment) =
                CleanupFrames(preadmitted.ProfileId, preadmitted.SuccessorGeneration, "cleanup-preadmitted");
            using (AuthoritativeStore store = new(new StoragePaths(preadmitted.ProductRoot)))
            {
                Assert.IsTrue(store.TryAdmitCredentialReplacementHelperLaunch(
                    "cleanup-preadmitted", preadmitted.Now));
                CredentialHelperCoordinator coordinator = new(store);
                int executions = 0;
                await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                    coordinator.ExecuteVerifiedReplacementCleanupAsync(
                        "cleanup-preadmitted", preadmittedBootstrap, preadmittedAssignment, preadmitted.Now,
                        _ =>
                        {
                            executions++;
                            return Task.FromException<HelperProcessReceipt>(
                                new AssertFailedException("A pre-admitted helper must not execute."));
                        }, cancellationToken: CancellationToken.None));
                Assert.AreEqual(0, executions);
            }

            ReplacementFixture tampered = CreateFixture(
                repository, testRoot, "tampered", FixtureMode.DeletePendingRecovery);
            (HelperPrivateFrameV2 tamperedBootstrap, HelperPrivateFrameV2 tamperedAssignment) =
                CleanupFrames(tampered.ProfileId, tampered.SuccessorGeneration, "cleanup-tampered");
            using (AuthoritativeStore store = new(new StoragePaths(tampered.ProductRoot)))
            {
                Assert.IsTrue(store.TryAdmitCredentialReplacementHelperLaunch("cleanup-tampered", tampered.Now));
                CoordinatedHelperReceipt prepared = CompletedReplacementReceipt(
                    "cleanup-tampered", tamperedAssignment, tampered.ProfileId, tampered.SuccessorGeneration,
                    tampered.HelperSha256);
                CoordinatedHelperReceipt staged = StageReceipt(store, "cleanup-tampered", prepared with
                {
                    Process = prepared.Process with { NativeCallTraceBytes = "[]"u8.ToArray() },
                }, tampered.Now);
                OneShotCredentialHelperLauncher launcher = new(
                    tampered.HelperPath, tampered.HelperSha256, Path.Combine(testRoot, "synthetic-store-tampered"));
                CredentialHelperCoordinator coordinator = new(store, launcher);
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    coordinator.CompleteVerifiedReplacementCleanup(
                        repository, "cleanup-tampered", tamperedBootstrap,
                        tamperedAssignment, staged, tampered.Now));
                Assert.AreEqual("delete-pending", store.GetCredentialProfile(tampered.ProfileId).LifecycleState);
                Assert.IsFalse(File.Exists(Path.Combine(store.Paths.Staging, "cleanup-tampered",
                    AuthoritativeStore.CredentialReplacementBoundaryFileName)));
                await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                    coordinator.ExecuteVerifiedReplacementCleanupAsync(
                        "cleanup-tampered", tamperedBootstrap,
                        tamperedAssignment, tampered.Now,
                        cancellationToken: CancellationToken.None));
            }
        }
        finally
        {
            if (Directory.Exists(testRoot)) { Directory.Delete(testRoot, recursive: true); }
        }
    }

    private static ReplacementFixture CreateFixture(
        string repository,
        string testRoot,
        string name,
        FixtureMode mode = FixtureMode.ReplacingRecovery)
    {
        string root = Path.Combine(testRoot, name);
        string product = Path.Combine(root, "product");
        string profile = "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e";
        string successor = mode == FixtureMode.Initial
            ? "g-test" + Guid.NewGuid().ToString("N")
            : "g-e6b6a3f21ad74108ba65955850349f83";
        Directory.CreateDirectory(root);
        using (StoragePaths paths = new(product)) { paths.Create(); }
        EnsureInitialVerifiedCredential(product);
        if (mode != FixtureMode.Initial)
        {
            EnsureReplacementState(product, profile, successor, mode == FixtureMode.DeletePendingRecovery);
        }
        string ledger = Path.Combine(repository, "artifacts", "m1-slice6", "successor-campaign", "ledger.v3.jsonl");
        string coordinator = Path.Combine(repository, "src", "Infinium.Coordinator", "bin", "Debug", "net10.0",
            "Infinium.Coordinator.exe");
        string helper = Path.Combine(repository, "src", "Infinium.CredentialHelper", "bin", "Debug", "net10.0",
            "Infinium.CredentialHelper.exe");
        Assert.IsTrue(File.Exists(coordinator));
        Assert.IsTrue(File.Exists(helper));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string successorFingerprint = Sha(Encoding.UTF8.GetBytes($"Infinium:{profile}:{successor}"));
        string authorityId = "infinium.m1-s6.test-credential-replacement/" + Guid.NewGuid().ToString("N");
        string evidenceId = "infinium.m1-s6.test-credential-replacement-evidence/" + Guid.NewGuid().ToString("N");
        string evidence = Path.Combine(root, "evidence", "replacement.v1.json");
        string owner = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            mode switch
            {
                FixtureMode.ReplacingRecovery => "m1-slice6-development-campaign-amendment.v4.json",
                FixtureMode.DeletePendingRecovery => "m1-slice6-development-campaign-amendment.v5.json",
                _ => "m1-slice6-development-campaign-amendment.v3.json",
            });
        string v4 = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v4.json");
        using JsonDocument entryDocument = JsonDocument.Parse(File.ReadAllBytes(v4));
        JsonElement entry = entryDocument.RootElement.GetProperty("m1_entry_surface").Clone();
        string commit = typeof(M1Slice6SuccessorCredentialReplacementRunner).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[^1];
        object authority = new
        {
            schema_identity = M1Slice6SuccessorCredentialReplacementRunner.AuthoritySchema,
            authority_id = authorityId,
            evidence_id = evidenceId,
            status = "independently-reviewed-ready-for-owner-effect",
            prepared_at_utc = Z(now.AddMinutes(-2)),
            not_before_utc = Z(now.AddMinutes(-1)),
            expires_at_utc = Z(now.AddMinutes(30)),
            owner_authority = new
            {
                id = mode switch
                {
                    FixtureMode.ReplacingRecovery =>
                        "infinium.m1-s6.credential-replacement-recovery/20260821-pre-native-launcher-factory",
                    FixtureMode.DeletePendingRecovery =>
                        "infinium.m1-s6.credential-replacement-cleanup-recovery/20260821-pre-entry-assignment-prefix",
                    _ => "infinium.m1-s6.credential-replacement/20260821-owner-fresh-key",
                },
                path = Relative(repository, owner), sha256 = HashFile(owner),
            },
            predecessor_ledger = new
            {
                path = Relative(repository, ledger), sha256 = HashFile(ledger), sequence = 39,
                event_hash = "b5292326a65791731614a6ab75a0b838de121ea721d01d5489f4c2897f88dcc0",
            },
            product_state = new
            {
                root_absolute = Path.GetFullPath(product),
                checkpoint_sha256 = M1Slice6SuccessorAuthorityLoader.ComputeProductStateCheckpointSha256(product),
            },
            profile = new
            {
                access_profile_id = profile,
                predecessor_generation_id = "g-ff6d82e7a7d244f6b8a9d0164991be37",
                predecessor_target_fingerprint_sha256 = "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0",
                successor_generation_id = successor,
                successor_generation_ordinal = 2,
                successor_target_fingerprint_sha256 = successorFingerprint,
                target_derivation = "Infinium:<access_profile_id>:<generation_id>", target_encoding = "utf-8",
            },
            release_build = new
            {
                implementation_commit = commit,
                coordinator_path = Relative(repository, coordinator), coordinator_sha256 = HashFile(coordinator),
                helper_path = Relative(repository, helper), helper_sha256 = HashFile(helper),
            },
            effect_boundary = new
            {
                evidence_path = Relative(repository, evidence), helper_launches = 1, dns_resolutions = 0,
                network_operations = 0, provider_operations = 0, billable_operations = 0,
                automatic_retry = false, enumeration = "prohibited", secret_exposure = "prohibited",
            },
            native_boundary = new
            {
                maximum_calls = new { CredWriteW = 1, CredReadW = 6, CredDeleteW = 1, CredFree = 3, total = 11 },
                enumeration = "prohibited", overwrite = "prohibited",
                predecessor_delete = "required-after-successor-write-readback-verification",
            },
            m1_entry_surface = entry,
        };
        string authorityPath = Path.Combine(root, "authority.v1.json");
        File.WriteAllText(authorityPath, JsonSerializer.Serialize(authority, Json) + "\n", new UTF8Encoding(false));
        string authoritySha = HashFile(authorityPath);
        object review = new
        {
            schema_identity = M1Slice6SuccessorCredentialReplacementRunner.ReviewSchema,
            review_id = "infinium.m1-s6.test-credential-replacement-review/" + Guid.NewGuid().ToString("N"),
            verdict = "accept", reviewer_id = "/root/test-independent", independent = true,
            provider_effect_used = false, subject = new { id = authorityId, sha256 = authoritySha },
            findings = Array.Empty<string>(), reviewed_at_utc = Z(now.AddSeconds(-30)),
        };
        string reviewPath = Path.Combine(root, "review.v1.json");
        File.WriteAllText(reviewPath, JsonSerializer.Serialize(review, Json) + "\n", new UTF8Encoding(false));
        return new(repository, product, ledger, coordinator, helper, authorityPath, authoritySha,
            reviewPath, HashFile(reviewPath), evidence, profile, successor, now);
    }

    private static void EnsureInitialVerifiedCredential(string productRoot)
    {
        const string profile = "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e";
        const string generation = "g-ff6d82e7a7d244f6b8a9d0164991be37";
        DateTimeOffset now = DateTimeOffset.UtcNow.AddHours(-1);
        using AuthoritativeStore store = new(new StoragePaths(productRoot));
        store.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, now);
        _ = store.BeginCredentialEnrollment(profile, generation, "Synthetic initial replacement predecessor",
            now.AddTicks(1), "account-initial", "billing-initial");
        _ = store.ApplyCredentialTransition(new("initial-enroll", profile, generation, "enroll",
            "pending-enrollment", "active-unverified", "active-unverified",
            M1ProviderCatalog.Capability.Identity.Value, "account-initial", "billing-initial",
            now.AddTicks(2), now.AddTicks(3)));
        _ = store.ApplyCredentialTransition(new("initial-verify", profile, generation, "verify",
            "active-unverified", "active-verified", "active-verified",
            M1ProviderCatalog.Capability.Identity.Value, "account-initial", "billing-initial",
            now.AddTicks(4), now.AddTicks(5)));
    }

    private static void EnsureReplacementState(
        string productRoot,
        string profile,
        string successor,
        bool deletePending)
    {
        const string predecessor = "g-ff6d82e7a7d244f6b8a9d0164991be37";
        using AuthoritativeStore store = new(new StoragePaths(productRoot));
        CredentialProfileProjection current = store.GetCredentialProfile(profile);
        DateTimeOffset now = current.UpdatedAt.AddTicks(10);
        CredentialProfileProjection replacing = store.BeginCredentialReplacement(
            "fixture-replacement-begin-" + Guid.NewGuid().ToString("N"),
            profile, predecessor, successor, 2, now);
        if (!deletePending) { return; }
        CredentialProfileProjection pending = store.ApplyCredentialTransition(new(
            "fixture-replacement-cleanup-pending-" + Guid.NewGuid().ToString("N"),
            profile, predecessor, "delete", "replacing", "delete-pending", "delete-pending",
            replacing.CapabilitySnapshotId, replacing.AccountIdentityId, replacing.BillingScopeIdentityId,
            replacing.UpdatedAt.AddTicks(10), replacing.UpdatedAt.AddTicks(11)));
        _ = store.ApplyCredentialTransition(new(
            "fixture-replacement-cleanup-failed-" + Guid.NewGuid().ToString("N"),
            profile, predecessor, "delete", "delete-pending", "delete-pending", "delete-pending",
            pending.CapabilitySnapshotId, pending.AccountIdentityId, pending.BillingScopeIdentityId,
            pending.UpdatedAt.AddTicks(10), pending.UpdatedAt.AddTicks(11), Failed: true));
    }

    private static ReplacementFixture RebindOwner(
        string repository,
        ReplacementFixture fixture,
        string ownerPath)
    {
        JsonObject authority = JsonNode.Parse(File.ReadAllBytes(fixture.AuthorityPath))!.AsObject();
        authority["owner_authority"]!["path"] = Relative(repository, ownerPath);
        authority["owner_authority"]!["sha256"] = HashFile(ownerPath);
        WriteNode(fixture.AuthorityPath, authority);
        string authoritySha = HashFile(fixture.AuthorityPath);
        JsonObject review = JsonNode.Parse(File.ReadAllBytes(fixture.ReviewPath))!.AsObject();
        review["subject"]!["sha256"] = authoritySha;
        WriteNode(fixture.ReviewPath, review);
        return fixture with { AuthoritySha256 = authoritySha, ReviewSha256 = HashFile(fixture.ReviewPath) };
    }

    private static void WriteNode(string path, JsonNode value) => File.WriteAllText(
        path,
        value.ToJsonString(Json) + "\n",
        new UTF8Encoding(false));

    private static Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)> CompleteWithoutNative(
        AuthoritativeStore store, string attemptId, HelperPrivateFrameV2 _, HelperPrivateFrameV2 assignment,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        CompleteWithoutNativeCore(store, attemptId, assignment, now, predecessorAlreadyAbsent: false, cancellationToken);

    private static Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)>
        CompleteCleanupWithoutNative(
            AuthoritativeStore store, string attemptId, HelperPrivateFrameV2 _, HelperPrivateFrameV2 assignment,
            DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assert.AreEqual(HelperAssignmentKindV2.Replace, assignment.Assignment.AssignmentKind);
        CredentialProfileProjection old = store.GetCredentialProfile(assignment.Assignment.AccessProfileId.Value);
        Assert.AreEqual("delete-pending", old.LifecycleState);
        string generation = assignment.Assignment.GenerationId.Value;
        CredentialProfileProjection recovered = store.ApplyCredentialTransition(new(
            attemptId + "-replacement-cleanup-recovered", old.ProfileId, generation, "recover",
            "delete-pending", "active-unverified", "active-unverified", old.CapabilitySnapshotId,
            old.AccountIdentityId, old.BillingScopeIdentityId, now.AddTicks(3), now.AddTicks(4)));
        CredentialProfileProjection verified = store.ApplyCredentialTransition(new(
            attemptId + "-verified-generation", old.ProfileId, generation, "verify", "active-unverified",
            "active-verified", "active-verified", old.CapabilitySnapshotId, old.AccountIdentityId,
            old.BillingScopeIdentityId, now.AddTicks(5), now.AddTicks(6)));
        Assert.AreEqual(2, recovered.GenerationOrdinal);
        return Task.FromResult((CompletedReplacementReceipt(attemptId, assignment, old.ProfileId, generation), verified));
    }

    private static Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)>
        StopCleanupFailedKnownWithoutNative(
            AuthoritativeStore store, string attemptId, HelperPrivateFrameV2 bootstrap, HelperPrivateFrameV2 assignment,
            DateTimeOffset now, CancellationToken cancellationToken)
    {
        _ = bootstrap;
        _ = now;
        cancellationToken.ThrowIfCancellationRequested();
        CredentialProfileProjection current = store.GetCredentialProfile(assignment.Assignment.AccessProfileId.Value);
        Assert.AreEqual("delete-pending", current.LifecycleState);
        HelperReceiptV2 receipt = new()
        {
            Outcome = HelperOutcomeV2.FailedKnown,
            AssignmentId = assignment.Assignment.AssignmentId,
            CommandId = assignment.Assignment.CommandId,
            AssignmentKind = HelperAssignmentKindV2.Replace,
            Credential = assignment.Assignment.Credential.Clone(),
            UsageReceiptState = UsageReceiptStateV2.NotDispatched,
            NonSecretReceipt = Digest(
                $"{assignment.Assignment.AssignmentId}/{assignment.Assignment.CommandId}/{HelperOutcomeV2.FailedKnown}"),
        };
        HelperProcessReceipt process = new(1, 0, new string('a', 64), receipt, [], 2, 0, 0, 0, 0, 0, true,
            false, "[]"u8.ToArray(), null,
            JsonSerializer.SerializeToUtf8Bytes(Canary()), true, false, 1, 2);
        HelperStagingReceipt staging = new(attemptId, "staging/test/helper-receipt.v2.pb", 1, new string('b', 64),
            null, 0, null, true, true);
        return Task.FromResult((new CoordinatedHelperReceipt(process, staging), current));
    }

    private static CoordinatedHelperReceipt CompletedReplacementReceipt(
        string attemptId,
        HelperPrivateFrameV2 assignment,
        string profileId,
        string generation,
        string? binarySha256 = null)
    {
        string predecessor = "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0";
        string successor = Sha(Encoding.UTF8.GetBytes($"Infinium:{profileId}:{generation}"));
        string[] operations = ["CredReadW", "CredWriteW", "CredReadW", "CredFree", "CredReadW", "CredFree",
            "CredReadW", "CredFree", "CredDeleteW", "CredReadW", "CredReadW"];
        string[] fingerprints = [successor, successor, successor, successor, predecessor, predecessor, predecessor,
            predecessor, predecessor, predecessor, predecessor];
        string[] results = ["ERROR_NOT_FOUND", "success", "success", "released", "success", "released", "success",
            "released", "success", "ERROR_NOT_FOUND", "ERROR_NOT_FOUND"];
        object[] trace = Enumerable.Range(0, operations.Length).Select(index => (object)new
        {
            Sequence = index + 1, Operation = operations[index], TargetFingerprintSha256 = fingerprints[index],
            Scenario = "m1-slice6-successor-credential-replacement", Result = results[index],
            AllocationId = index switch { 2 => (long?)1, 4 => 2, 6 => 3, _ => null },
            PairedAllocationId = index switch { 3 => (long?)1, 5 => 2, 7 => 3, _ => null },
        }).ToArray();
        HelperReceiptV2 receipt = new()
        {
            Outcome = HelperOutcomeV2.Completed,
            AssignmentId = assignment.Assignment.AssignmentId,
            CommandId = assignment.Assignment.CommandId,
            AssignmentKind = HelperAssignmentKindV2.Replace,
            Credential = assignment.Assignment.Credential.Clone(),
            UsageReceiptState = UsageReceiptStateV2.NotDispatched,
            NonSecretReceipt = Digest(
                $"{assignment.Assignment.AssignmentId}/{assignment.Assignment.CommandId}/{HelperOutcomeV2.Completed}"),
        };
        HelperProcessReceipt process = new(1, 0, binarySha256 ?? new string('a', 64), receipt, [], 2, 0, 0, 0, 11, 0, true,
            false, JsonSerializer.SerializeToUtf8Bytes(trace), JsonSerializer.SerializeToUtf8Bytes(Entry("submitted")),
            JsonSerializer.SerializeToUtf8Bytes(Canary()), true, false, 1, 2);
        HelperStagingReceipt staging = new(attemptId, "staging/test/helper-receipt.v2.pb", 1, new string('b', 64),
            null, 0, null, true, true);
        return new(process, staging);
    }

    private static HelperProcessReceipt MidTraceReadFailureProcess(
        string successorFingerprint,
        bool forgedAllocation,
        string failureResult = "win32-error:5")
    {
        object[] trace =
        [
            new
            {
                Sequence = 1, Operation = "CredReadW", TargetFingerprintSha256 = successorFingerprint,
                Scenario = "m1-slice6-successor-credential-replacement", Result = "ERROR_NOT_FOUND",
                AllocationId = (long?)null, PairedAllocationId = (long?)null,
            },
            new
            {
                Sequence = 2, Operation = "CredWriteW", TargetFingerprintSha256 = successorFingerprint,
                Scenario = "m1-slice6-successor-credential-replacement", Result = "success",
                AllocationId = (long?)null, PairedAllocationId = (long?)null,
            },
            new
            {
                Sequence = 3, Operation = "CredReadW", TargetFingerprintSha256 = successorFingerprint,
                Scenario = "m1-slice6-successor-credential-replacement", Result = failureResult,
                AllocationId = forgedAllocation ? (long?)1 : null, PairedAllocationId = (long?)null,
            },
        ];
        HelperReceiptV2 receipt = new()
        {
            Outcome = HelperOutcomeV2.FailedKnown,
            AssignmentId = "replacement-mid-trace/read-failure",
        };
        return new(
            1, 0, new string('a', 64), receipt, [], 2, 0, 0, 0, 3, 0, true, false,
            JsonSerializer.SerializeToUtf8Bytes(trace),
            JsonSerializer.SerializeToUtf8Bytes(Entry("failed")),
            JsonSerializer.SerializeToUtf8Bytes(Canary()),
            true, false, 1, 2);
    }

    private static Infinium.Contracts.Protobuf.Common.V1.ContentDigest Digest(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        return new()
        {
            Algorithm = Infinium.Contracts.Protobuf.Common.V1.DigestAlgorithm.Sha256,
            Value = Google.Protobuf.ByteString.CopyFrom(SHA256.HashData(bytes)),
            SizeBytes = checked((ulong)bytes.Length),
        };
    }

    private static (HelperPrivateFrameV2 Bootstrap, HelperPrivateFrameV2 Assignment) CleanupFrames(
        string profileId,
        string successorGeneration,
        string attemptId,
        string? exactAuthorityId = null)
    {
        string authorityId = exactAuthorityId
            ?? "infinium.m1-s6.successor-credential-replacement-cleanup-recovery/" + attemptId;
        HelperPrivateFrameV2 bootstrap = new()
        {
            Sequence = 1,
            ProtocolFingerprintSha256 = Google.Protobuf.ByteString.CopyFrom(
                Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Bootstrap = new()
            {
                CoordinatorFencingEpoch = 1,
                ExpiresAt = new() { UnixSeconds = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds() },
                OneUseNonceFingerprintSha256 = Google.Protobuf.ByteString.CopyFrom(new byte[32]),
                CommandId = authorityId + "/command",
                Credential = new()
                {
                    AccessProfileId = new() { Value = profileId },
                    GenerationId = new() { Value = "g-ff6d82e7a7d244f6b8a9d0164991be37" },
                },
            },
        };
        HelperPrivateFrameV2 assignment = new()
        {
            Sequence = 2,
            ProtocolFingerprintSha256 = bootstrap.ProtocolFingerprintSha256,
            Assignment = new()
            {
                AssignmentId = authorityId + "/replace",
                CommandId = authorityId + "/command",
                AssignmentKind = HelperAssignmentKindV2.Replace,
                AccessProfileId = new() { Value = profileId },
                GenerationId = new() { Value = successorGeneration },
                GenerationOrdinal = 2,
                Credential = new()
                {
                    AccessProfileId = new() { Value = profileId },
                    GenerationId = new() { Value = successorGeneration },
                },
            },
        };
        return (bootstrap, assignment);
    }

    private static CoordinatedHelperReceipt StageReceipt(
        AuthoritativeStore store,
        string attemptId,
        CoordinatedHelperReceipt prepared,
        DateTimeOffset now)
    {
        HelperPrivateFrameV2 terminal = new()
        {
            Sequence = 3,
            ProtocolFingerprintSha256 = Google.Protobuf.ByteString.CopyFrom(
                Convert.FromHexString(HelperProtocolV2Constants.SchemaFingerprintSha256)),
            Receipt = prepared.Process.Receipt.Clone(),
        };
        byte[] canonical = HelperPrivateProtocolV2.Encode(terminal);
        HelperStagingReceipt staging = store.StageAndAdmitHelperReceipt(attemptId, canonical, now);
        return prepared with { Staging = staging };
    }

    private static Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)>
        CompleteAlreadyAbsentWithoutNative(
            AuthoritativeStore store, string attemptId, HelperPrivateFrameV2 _, HelperPrivateFrameV2 assignment,
            DateTimeOffset now, CancellationToken cancellationToken) =>
            CompleteWithoutNativeCore(store, attemptId, assignment, now, predecessorAlreadyAbsent: true, cancellationToken);

    private static Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)>
        CompleteWithoutNativeCore(
        AuthoritativeStore store, string attemptId, HelperPrivateFrameV2 assignment,
        DateTimeOffset now, bool predecessorAlreadyAbsent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CredentialProfileProjection old = store.GetCredentialProfile(assignment.Assignment.AccessProfileId.Value);
        string generation = assignment.Assignment.GenerationId.Value;
        CredentialProfileProjection activeUnverified = store.ApplyCredentialTransition(new(
            attemptId + "-credential-transition", old.ProfileId, generation, "replace", "replacing",
            "active-unverified", "active-unverified", old.CapabilitySnapshotId, old.AccountIdentityId,
            old.BillingScopeIdentityId, now.AddTicks(3), now.AddTicks(4)));
        CredentialProfileProjection verified = store.ApplyCredentialTransition(new(
            attemptId + "-verified-generation", old.ProfileId, generation, "verify", "active-unverified",
            "active-verified", "active-verified", old.CapabilitySnapshotId, old.AccountIdentityId,
            old.BillingScopeIdentityId, now.AddTicks(5), now.AddTicks(6)));
        Assert.AreEqual("active-unverified", activeUnverified.LifecycleState);
        string predecessor = "06637d7e67004768b83297623114c8e44c7298c9a2ab37c05ab41fa8617a4dd0";
        string successor = Sha(Encoding.UTF8.GetBytes($"Infinium:{old.ProfileId}:{generation}"));
        string[] operations = ["CredReadW", "CredWriteW", "CredReadW", "CredFree", "CredReadW", "CredFree",
            "CredReadW", "CredFree", "CredDeleteW", "CredReadW", "CredReadW"];
        string[] fingerprints = [successor, successor, successor, successor, predecessor, predecessor, predecessor,
            predecessor, predecessor, predecessor, predecessor];
        string[] results = ["ERROR_NOT_FOUND", "success", "success", "released", "success", "released", "success",
            "released", "success", "ERROR_NOT_FOUND", "ERROR_NOT_FOUND"];
        int traceLength = predecessorAlreadyAbsent ? 5 : 11;
        object[] trace = Enumerable.Range(0, traceLength).Select(index => (object)new
        {
            Sequence = index + 1, Operation = operations[index], TargetFingerprintSha256 = fingerprints[index],
            Scenario = "m1-slice6-successor-credential-replacement",
            Result = predecessorAlreadyAbsent && index == 4 ? "ERROR_NOT_FOUND" : results[index],
            AllocationId = index switch { 2 => (long?)1, 4 when !predecessorAlreadyAbsent => 2, 6 => 3, _ => null },
            PairedAllocationId = index switch { 3 => (long?)1, 5 => 2, 7 => 3, _ => null },
        }).ToArray();
        object entry = Entry("submitted");
        object canary = Canary();
        HelperReceiptV2 receipt = new() { Outcome = HelperOutcomeV2.Completed, AssignmentId = assignment.Assignment.AssignmentId };
        HelperProcessReceipt process = new(1, 0, new string('a', 64), receipt, [], 2, 0, 0, 0, traceLength, 0, true,
            false, JsonSerializer.SerializeToUtf8Bytes(trace), JsonSerializer.SerializeToUtf8Bytes(entry),
            JsonSerializer.SerializeToUtf8Bytes(canary), true, false, 1, 2);
        HelperStagingReceipt staging = new(attemptId, "staging/test/helper-receipt.v2.pb", 1, new string('b', 64),
            null, 0, null, true, true);
        return Task.FromResult((new CoordinatedHelperReceipt(process, staging), verified));
    }

    private static Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)> StopWithoutNative(
        AuthoritativeStore store, string attemptId, HelperPrivateFrameV2 _, HelperPrivateFrameV2 assignment,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CredentialProfileProjection replacing = store.GetCredentialProfile(assignment.Assignment.AccessProfileId.Value);
        CredentialProfileProjection stopped = StopProjection(store, attemptId, replacing, now);
        HelperReceiptV2 receipt = new()
        {
            Outcome = HelperOutcomeV2.Cancelled,
            AssignmentId = assignment.Assignment.AssignmentId,
        };
        HelperProcessReceipt process = new(1, 0, new string('a', 64), receipt, [], 2, 0, 0, 0, 0, 0, true,
            false, "[]"u8.ToArray(), JsonSerializer.SerializeToUtf8Bytes(Entry("cancelled")),
            JsonSerializer.SerializeToUtf8Bytes(Canary()), true, false, 1, 2);
        HelperStagingReceipt staging = new(attemptId, "staging/test/helper-receipt.v2.pb", 1, new string('b', 64),
            null, 0, null, true, true);
        return Task.FromResult((new CoordinatedHelperReceipt(process, staging), stopped));
    }

    private static Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)> CollideWithoutNative(
        AuthoritativeStore store, string attemptId, HelperPrivateFrameV2 _, HelperPrivateFrameV2 assignment,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CredentialProfileProjection replacing = store.GetCredentialProfile(assignment.Assignment.AccessProfileId.Value);
        CredentialProfileProjection stopped = StopProjection(store, attemptId, replacing, now);
        string generation = assignment.Assignment.GenerationId.Value;
        string successor = Sha(Encoding.UTF8.GetBytes($"Infinium:{replacing.ProfileId}:{generation}"));
        object[] trace =
        [
            new { Sequence = 1, Operation = "CredReadW", TargetFingerprintSha256 = successor,
                Scenario = "m1-slice6-successor-credential-replacement", Result = "success",
                AllocationId = (long?)1, PairedAllocationId = (long?)null },
            new { Sequence = 2, Operation = "CredFree", TargetFingerprintSha256 = successor,
                Scenario = "m1-slice6-successor-credential-replacement", Result = "released",
                AllocationId = (long?)null, PairedAllocationId = (long?)1 },
        ];
        HelperReceiptV2 receipt = new()
        {
            Outcome = HelperOutcomeV2.FailedKnown,
            AssignmentId = assignment.Assignment.AssignmentId,
        };
        HelperProcessReceipt process = new(1, 0, new string('a', 64), receipt, [], 2, 0, 0, 0, 2, 0, true,
            false, JsonSerializer.SerializeToUtf8Bytes(trace), JsonSerializer.SerializeToUtf8Bytes(Entry("submitted")),
            JsonSerializer.SerializeToUtf8Bytes(Canary()), true, false, 1, 2, true, "preflight-collision");
        HelperStagingReceipt staging = new(attemptId, "staging/test/helper-receipt.v2.pb", 1, new string('b', 64),
            null, 0, null, true, true);
        return Task.FromResult((new CoordinatedHelperReceipt(process, staging), stopped));
    }

    private static async Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)>
        ContradictoryActiveStopWithoutNative(
            AuthoritativeStore store, string attemptId, HelperPrivateFrameV2 bootstrap, HelperPrivateFrameV2 assignment,
            DateTimeOffset now, CancellationToken cancellationToken)
    {
        (CoordinatedHelperReceipt completed, CredentialProfileProjection active) =
            await CompleteWithoutNative(store, attemptId, bootstrap, assignment, now, cancellationToken);
        HelperReceiptV2 cancelledReceipt = completed.Process.Receipt.Clone();
        cancelledReceipt.Outcome = HelperOutcomeV2.Cancelled;
        HelperProcessReceipt contradictory = completed.Process with
        {
            Receipt = cancelledReceipt,
            NativeCredentialOperationCount = 0,
            NativeCallTraceBytes = "[]"u8.ToArray(),
            NativeEntryCleanupBytes = JsonSerializer.SerializeToUtf8Bytes(Entry("cancelled")),
        };
        return (completed with { Process = contradictory }, active);
    }

    private static CredentialProfileProjection StopProjection(
        AuthoritativeStore store,
        string attemptId,
        CredentialProfileProjection replacing,
        DateTimeOffset now) => store.ApplyCredentialTransition(new(
            attemptId + "-predecessor-cleanup-pending", replacing.ProfileId, replacing.GenerationId, "delete",
            "replacing", "delete-pending", "delete-pending", replacing.CapabilitySnapshotId,
            replacing.AccountIdentityId, replacing.BillingScopeIdentityId, now.AddTicks(3), now.AddTicks(4)));

    private static object Entry(string terminal)
    {
        bool cancelled = terminal == "cancelled";
        object? action = terminal is "submitted" or "cancelled" ? new
        {
            Action = cancelled ? "cancel" : "submit",
            Source = cancelled ? "cancel-button" : "submit-button",
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
            CurrentBlank = cancelled,
            CurrentCharacterLength = cancelled ? 0 : 32,
            Admitted = true,
        } : null;
        return new
        {
            Surface = "wp9-distinct-helper-owned-native-masked-paste-surface", Masked = true, PastePermitted = true,
            HelperOwned = true, RendererReceivedSecret = false, InitiallyBlank = true, Ready = true,
            HelperProcessOwned = true, SameSession = true, InputDesktopAvailable = true, NotCloaked = true,
            OnMonitor = true, Enabled = true, Focused = true, Foreground = true, Active = true, ReadinessChecks = 1,
            PreReadinessIgnoredActions = 0, MessagePumpIterations = 1,
            ActionSnapshot = action,
            TerminalState = terminal, WindowDestroyed = true, BufferCleared = true, NativeEditEmptyVerified = true,
            ThreadJoined = true,
        };
    }

    private static object Canary(string responseSurface = "private protocol response")
    {
        string[] names = ["private protocol request", responseSurface, "native call trace",
            "process command line", "process environment names"];
        string[] kinds = ["private-pipe-bytes", "private-pipe-bytes", "canonical-trace-bytes", "captured-text", "captured-text"];
        return new { SecretMatches = 0, RawTargetMatches = 0, RawTargetEncodings = new[] { "utf-8", "utf-16le" },
            ScannedSurfaces = names.Select((value, index) => new { Name = value, Kind = kinds[index], ByteCount = 1,
                SecretMatches = 0, RawTargetMatches = 0 }).ToArray() };
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
    private static string Z(DateTimeOffset value) => value.ToUniversalTime().ToString(
        "yyyy-MM-ddTHH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
    private static string HashFile(string path) => Sha(File.ReadAllBytes(path));
    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed record ReplacementFixture(
        string Repository, string ProductRoot, string LedgerPath, string CoordinatorPath, string HelperPath,
        string AuthorityPath, string AuthoritySha256, string ReviewPath, string ReviewSha256, string EvidencePath,
        string ProfileId, string SuccessorGeneration, DateTimeOffset Now)
    {
        internal string HelperSha256 => HashFile(HelperPath);
        internal M1Slice6SuccessorCredentialReplacementRunner.ReplacementRunnerTestHooks Hooks(
            Func<AuthoritativeStore, string, HelperPrivateFrameV2, HelperPrivateFrameV2, DateTimeOffset,
                CancellationToken, Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)>> effect) =>
            new(Now, ProductRoot, CoordinatorPath, effect);
    }
}
