using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class M1Slice6CredentialReplacementRunnerTests
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

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

    private static ReplacementFixture CreateFixture(string repository, string testRoot, string name)
    {
        string root = Path.Combine(testRoot, name);
        string product = Path.Combine(root, "product");
        Directory.CreateDirectory(root);
        using (StoragePaths paths = new(product)) { paths.Create(); }
        CopyDirectory(Path.Combine(repository, "artifacts", "m1-slice6", "successor-product-state"), product);
        string ledger = Path.Combine(repository, "artifacts", "m1-slice6", "successor-campaign", "ledger.v3.jsonl");
        string coordinator = Path.Combine(repository, "src", "Infinium.Coordinator", "bin", "Debug", "net10.0",
            "Infinium.Coordinator.exe");
        string helper = Path.Combine(repository, "src", "Infinium.CredentialHelper", "bin", "Debug", "net10.0",
            "Infinium.CredentialHelper.exe");
        Assert.IsTrue(File.Exists(coordinator));
        Assert.IsTrue(File.Exists(helper));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string profile = "openai-platform-dc68f2ca9775415eb6fa78de5cafe14e";
        string successor = "g-test" + Guid.NewGuid().ToString("N");
        string successorFingerprint = Sha(Encoding.UTF8.GetBytes($"Infinium:{profile}:{successor}"));
        string authorityId = "infinium.m1-s6.test-credential-replacement/" + Guid.NewGuid().ToString("N");
        string evidenceId = "infinium.m1-s6.test-credential-replacement-evidence/" + Guid.NewGuid().ToString("N");
        string evidence = Path.Combine(root, "evidence", "replacement.v1.json");
        string owner = Path.Combine(repository, "docs", "plans", "milestones", "m1", "slices", "s6",
            "m1-slice6-development-campaign-amendment.v3.json");
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
                id = "infinium.m1-s6.credential-replacement/20260821-owner-fresh-key",
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

    private static Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)> CompleteWithoutNative(
        AuthoritativeStore store, string attemptId, HelperPrivateFrameV2 _, HelperPrivateFrameV2 assignment,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        CompleteWithoutNativeCore(store, attemptId, assignment, now, predecessorAlreadyAbsent: false, cancellationToken);

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
        HelperProcessReceipt process = new(1, 0, new string('a', 64), receipt, [], 3, 0, 0, 0, traceLength, 0, true,
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
        HelperProcessReceipt process = new(1, 0, new string('a', 64), receipt, [], 3, 0, 0, 0, 0, 0, true,
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
        HelperProcessReceipt process = new(1, 0, new string('a', 64), receipt, [], 3, 0, 0, 0, 2, 0, true,
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
        return new
        {
            Surface = "wp9-distinct-helper-owned-native-masked-paste-surface", Masked = true, PastePermitted = true,
            HelperOwned = true, RendererReceivedSecret = false, InitiallyBlank = true, Ready = true,
            HelperProcessOwned = true, SameSession = true, InputDesktopAvailable = true, NotCloaked = true,
            OnMonitor = true, Enabled = true, Focused = true, Foreground = true, Active = true, ReadinessChecks = 1,
            PreReadinessIgnoredActions = 0, MessagePumpIterations = 1,
            ActionSnapshot = new { Action = cancelled ? "cancel" : "submit",
            Source = cancelled ? "cancel-button" : "submit-button", WindowVisible = true, EditVisible = true,
            InitiallyBlank = true, HelperProcessOwned = true, SameSession = true, InputDesktopAvailable = true,
            NotCloaked = true, OnMonitor = true, Enabled = true, Focused = true, Foreground = true, Active = true,
            CurrentBlank = cancelled, CurrentCharacterLength = cancelled ? 0 : 32, Admitted = true },
            TerminalState = terminal, WindowDestroyed = true, BufferCleared = true, NativeEditEmptyVerified = true,
            ThreadJoined = true,
        };
    }

    private static object Canary()
    {
        string[] names = ["private protocol request", "private protocol response", "native call trace",
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
