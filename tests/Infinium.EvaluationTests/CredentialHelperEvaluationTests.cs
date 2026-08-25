using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Google.Protobuf;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.Coordinator;
using Infinium.CredentialHelper;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialHelperEvaluationTests
{
    private static readonly string?[] ExpectedLifecycleOperations =
        ["pending-enrollment", "activation", "verification", "replacement", "disable", "delete"];
    private static readonly string[] ExpectedReplacementSlots =
        ["CAPABILITY-BOUND-STORE-TARGET-CANARY/profile-eval/generation-2"];

    [TestMethod]
    public async Task CredentialSyntheticDevelopmentPackageDrivesOneShotLifecycleOracle()
    {
        Fixture package = Load("lifecycle-dev", "CREDENTIAL-LIFECYCLE-DEV-v1");
        JsonElement input = package.Input.RootElement;
        JsonElement oracle = package.Oracle.RootElement;
        AssertInputHeaderAndMutations(
            input, "synthetic-lifecycle-and-private-process",
            ["schema", "case", "operations", "transport", "secure_store", "provider"]);
        CollectionAssert.AreEqual(
            ExpectedLifecycleOperations,
            input.GetProperty("operations").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual("inherited-anonymous-pipe", input.GetProperty("transport").GetString());
        Assert.AreEqual("deterministic-fake", input.GetProperty("secure_store").GetString());

        Assert.AreEqual("deterministic-nonnetwork-simulator", input.GetProperty("provider").GetString());
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Credential-Evaluation-" + Guid.NewGuid().ToString("N"));
        using AuthoritativeStore state = new(new StoragePaths(root));
        DateTimeOffset now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        state.PublishProviderCatalog(OpenAiProviderProfileCatalog.Capability, OpenAiProviderProfileCatalog.Price, now);
        string helperPath = Directory.GetFiles(AppContext.BaseDirectory, "Infinium.CredentialHelper.exe", SearchOption.AllDirectories).Single();
        OneShotCredentialHelperLauncher launcher = new(
            helperPath, Hash(helperPath), Path.Combine(root, "fake-secure-store"));
        CredentialHelperCoordinator coordinator = new(state, launcher);
        CredentialProfileProjection pending = state.BeginCredentialEnrollment(
            "profile-eval", "generation-1", "Synthetic", now.AddSeconds(1), "account-1", "billing-1");
        Assert.AreEqual("pending-enrollment", pending.LifecycleState);
        CoordinatedHelperReceipt latest = (await ExecuteTransition(
            coordinator, "activate", HelperAssignmentKindV2.Enroll, "generation-1", 1,
            "pending-enrollment", "active-unverified", now.AddSeconds(2))).Helper;
        latest = (await ExecuteTransition(
            coordinator, "verify-1", HelperAssignmentKindV2.Verify, "generation-1", 2,
            "active-unverified", "active-verified", now.AddSeconds(4))).Helper;
        state.AddCredentialGeneration("profile-eval", "generation-2", 2, 0, now.AddSeconds(6));
        latest = (await ExecuteTransition(
            coordinator, "replace", HelperAssignmentKindV2.Replace, "generation-2", 3,
            "active-verified", "active-unverified", now.AddSeconds(7))).Helper;
        using (JsonDocument secureStore = JsonDocument.Parse(File.ReadAllText(
                   Path.Combine(root, "fake-secure-store", "synthetic-secure-store.v1.json"))))
        {
            string[] exactSlots = secureStore.RootElement.GetProperty("Values").EnumerateObject()
                .Select(property => property.Name).ToArray();
            CollectionAssert.AreEqual(
                ExpectedReplacementSlots,
                exactSlots,
                "Replacement must delete the exact predecessor fake-store slot only after making it ineligible.");
        }
        latest = (await ExecuteTransition(
            coordinator, "verify-2", HelperAssignmentKindV2.Verify, "generation-2", 4,
            "active-unverified", "active-verified", now.AddSeconds(9))).Helper;
        latest = (await ExecuteTransition(
            coordinator, "disable", HelperAssignmentKindV2.Disable, "generation-2", 5,
            "active-verified", "disabled", now.AddSeconds(11))).Helper;
        (latest, CredentialProfileProjection terminal) = await ExecuteTransition(
            coordinator, "delete", HelperAssignmentKindV2.Delete, "generation-2", 6,
            "disabled", "deleted", now.AddSeconds(13), incrementRevocation: true);

        Dictionary<string, object> actual = new(StringComparer.Ordinal)
        {
            ["schema"] = "infinium.public.credential-helper-oracle/v1",
            ["expected_terminal_state"] = terminal.LifecycleState,
            ["expected_generation_ordinal"] = terminal.GenerationOrdinal,
            ["expected_revocation_epoch"] = terminal.RevocationEpoch,
            ["expected_private_handle_count"] = latest.Process.InheritedPrivateHandleCount,
            ["expected_standard_protocol_handle_count"] = latest.Process.StandardProtocolHandleCount,
            ["expected_stage_before_admit"] = latest.Staging.StagedBeforeAdmission,
            ["expected_coordinator_only_admission"] = latest.Staging.CoordinatorOnlyAdmission,
            ["expected_retry"] = latest.Process.RetryAttempted,
            ["expected_native_operations"] = latest.Process.NativeCredentialOperationCount,
            ["expected_network_operations"] = latest.Process.NetworkOperationCount,
        };
        AssertOracleAndEveryMutationFails(oracle, actual);
    }

    [TestMethod]
    public async Task CredentialSyntheticValidationPackageDrivesStrictFaultOracleAndRejectsMutation()
    {
        Fixture package = Load("faults-val", "CREDENTIAL-FAULTS-VAL-v1");
        JsonElement input = package.Input.RootElement;
        JsonElement oracle = package.Oracle.RootElement;
        AssertInputHeaderAndMutations(
            input, "strict-protocol-and-recovery-faults",
            ["schema", "case", "faults"]);
        DateTimeOffset now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        string[] faults = input.GetProperty("faults").EnumerateArray()
            .Select(item => item.GetString() ?? throw new InvalidDataException("Fault identity is required."))
            .ToArray();
        byte[] canonical = HelperPrivateProtocolV2.Encode(HelperTestFrames.Bootstrap());
        Dictionary<string, bool> rejected = new(StringComparer.Ordinal)
        {
            ["recursive-unknown"] = Rejects(() => HelperPrivateProtocolV2.Decode(RecursiveUnknownFrame(), 1)),
            ["duplicate-singular"] = Rejects(() => HelperPrivateProtocolV2.Decode(
                InsertPayload(canonical, 2, [0x08, 0x01]), 1)),
            ["conflicting-oneof"] = Rejects(() => HelperPrivateProtocolV2.Decode(
                AppendPayload(canonical, [0x5a, 0x00]), 1)),
            ["out-of-order"] = Rejects(() => HelperPrivateProtocolV2.Decode(
                AppendPayload(canonical, [0x08, 0x01]), 1)),
            ["stale-sequence"] = Rejects(() => HelperPrivateProtocolV2.Decode(canonical, 2)),
            ["oversized-frame"] = Rejects(() => HelperPrivateProtocolV2.Decode(OversizedFrame(), 1)),
        };

        using DeterministicFakeSecureStore store = new() { Available = false };
        OneShotHelperEngine engine = new(store, new FrozenTimeProvider(
            new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero)));
        using MemoryStream request = await RequestAsync(HelperAssignmentKindV2.Verify);
        using MemoryStream response = new();
        await engine.RunAsync(request, response, CancellationToken.None);
        byte[] responseBytes = response.ToArray();
        int frameLength = checked(4 + (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(responseBytes));
        Assert.AreEqual(HelperOutcomeV2.Unavailable,
            HelperPrivateProtocolV2.Decode(responseBytes.AsSpan(0, frameLength), 3).Receipt.Outcome);
        rejected["store-unavailable"] = true;
        using DeterministicFakeSecureStore boundedStore = new();
        rejected["oversized-secret"] = Rejects(() => boundedStore.WriteExact(
            new("profile-oversize", "generation-oversize"),
            new byte[DeterministicFakeSecureStore.MaximumSecretBytes + 1]));

        string root = Path.Combine(Path.GetTempPath(), "Infinium-Credential-EvaluationFault-" + Guid.NewGuid().ToString("N"));
        string helperPath = Directory.GetFiles(AppContext.BaseDirectory, "Infinium.CredentialHelper.exe", SearchOption.AllDirectories).Single();
        OneShotCredentialHelperLauncher launcher = new(helperPath, Hash(helperPath), Path.Combine(root, "fake-secure-store"));
        List<HelperProcessReceipt> receipts = [];
        receipts.Add(await launcher.ExecuteAsync(
            HelperTestFrames.Bootstrap(nonceSeed: 51), HelperTestFrames.Assignment(), null,
            TimeSpan.FromSeconds(20), now));

        HelperPrivateFrameV2 replayBootstrap = HelperTestFrames.Bootstrap(nonceSeed: 52);
        receipts.Add(await launcher.ExecuteAsync(
            replayBootstrap, HelperTestFrames.Assignment(HelperAssignmentKindV2.Verify), null,
            TimeSpan.FromSeconds(20), now));
        bool nonceReplayRejected = await RejectsAsync(() => launcher.ExecuteAsync(
            replayBootstrap, HelperTestFrames.Assignment(HelperAssignmentKindV2.Verify), null,
            TimeSpan.FromSeconds(20), now));
        rejected["stale-sequence"] &= nonceReplayRejected;

        HelperPrivateFrameV2 staleGeneration = HelperTestFrames.Revalidation();
        staleGeneration.DispatchRevalidation.GenerationId.Value = "generation-stale";
        HelperProcessReceipt staleGenerationReceipt = await launcher.ExecuteAsync(
            HelperTestFrames.DispatchBootstrap(53), HelperTestFrames.DispatchAssignment(), staleGeneration,
            TimeSpan.FromSeconds(20), now);
        receipts.Add(staleGenerationReceipt);
        rejected["stale-generation"] = staleGenerationReceipt.Receipt.Outcome == HelperOutcomeV2.FailedKnown;

        HelperPrivateFrameV2 staleRevocation = HelperTestFrames.Revalidation();
        staleRevocation.DispatchRevalidation.RevocationEpoch++;
        HelperProcessReceipt staleRevocationReceipt = await launcher.ExecuteAsync(
            HelperTestFrames.DispatchBootstrap(54), HelperTestFrames.DispatchAssignment(), staleRevocation,
            TimeSpan.FromSeconds(20), now);
        receipts.Add(staleRevocationReceipt);
        rejected["stale-revocation"] = staleRevocationReceipt.Receipt.Outcome == HelperOutcomeV2.FailedKnown;

        HelperPrivateFrameV2 expiredDeadline = HelperTestFrames.Revalidation();
        expiredDeadline.DispatchRevalidation.EvaluatedAt = HelperTestFrames.InstantAt(31);
        HelperProcessReceipt expiredDeadlineReceipt = await launcher.ExecuteAsync(
            HelperTestFrames.DispatchBootstrap(55), HelperTestFrames.DispatchAssignment(), expiredDeadline,
            TimeSpan.FromSeconds(20), now.AddSeconds(31));
        receipts.Add(expiredDeadlineReceipt);
        rejected["deadline-expired"] = expiredDeadlineReceipt.Receipt.Outcome == HelperOutcomeV2.FailedKnown;

        HelperPrivateFrameV2 malformedAssignment = HelperTestFrames.Assignment();
        malformedAssignment.Assignment.AssignmentKind = HelperAssignmentKindV2.Unspecified;
        HashSet<int> helperProcessesBefore = ProcessIds("Infinium.CredentialHelper");
        rejected["helper-crash"] = await RejectsAsync(() => launcher.ExecuteAsync(
            HelperTestFrames.Bootstrap(nonceSeed: 56), malformedAssignment, null,
            TimeSpan.FromSeconds(20), now));
        HashSet<int> helperProcessesAfter = ProcessIds("Infinium.CredentialHelper");
        int crashSurvivors = helperProcessesAfter.Except(helperProcessesBefore).Count();

        string restoreState = ExerciseBackupRestore(root, now);
        rejected["backup-restore"] = restoreState == "recovery-required";
        Assert.AreEqual(faults.Length, rejected.Count,
            "Every public fault identity must map to exactly one concrete regression.");
        CollectionAssert.AreEquivalent(faults, rejected.Keys.ToArray());
        Assert.IsTrue(rejected.Values.All(value => value), string.Join(", ",
            rejected.Where(item => !item.Value).Select(item => item.Key)));

        int transportStarts = receipts.Count(item => item.Receipt.TransportMayHaveStarted);
        int retries = receipts.Count(item => item.RetryAttempted);
        int processTreeSurvivors = receipts.Sum(item => item.ProcessTreeSurvivorCount) + crashSurvivors;
        int nativeOperations = receipts.Sum(item => item.NativeCredentialOperationCount);
        int networkOperations = receipts.Sum(item => item.NetworkOperationCount);
        (int secretCanaryMatches, int targetCanaryMatches) = MeasureCanaries(root);

        Dictionary<string, object> actual = new(StringComparer.Ordinal)
        {
            ["schema"] = "infinium.public.credential-helper-oracle/v1",
            ["expected_rejections"] = rejected.Count,
            ["expected_transport_starts"] = transportStarts,
            ["expected_retries"] = retries,
            ["expected_secret_canary_matches"] = secretCanaryMatches,
            ["expected_target_canary_matches"] = targetCanaryMatches,
            ["expected_process_tree_survivors"] = processTreeSurvivors,
            ["expected_restore_state"] = restoreState,
            ["expected_native_operations"] = nativeOperations,
            ["expected_network_operations"] = networkOperations,
        };
        AssertOracleAndEveryMutationFails(oracle, actual);
    }

    private static Fixture Load(string directory, string identity)
    {
        string relative = Path.Combine("fixtures", "public", "platform", "credential-helper", directory);
        string manifestPath = TestRepository.PathFromRoot(relative, "public-manifest.json");
        using JsonDocument registry = TestRepository.ReadJson("fixtures", "public", "current-fixture-registry.v1.json");
        JsonElement registryEntry = registry.RootElement.GetProperty("packages").EnumerateArray()
            .Single(item => item.GetProperty("package_identity").GetString() == identity);
        Assert.AreEqual(new FileInfo(manifestPath).Length, registryEntry.GetProperty("authority_bytes").GetInt64());
        Assert.AreEqual(Hash(manifestPath), registryEntry.GetProperty("authority_sha256").GetString());
        JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.IsTrue(manifest.RootElement.GetProperty("answer_free_input").GetBoolean());
        string inputPath = TestRepository.PathFromRoot(relative, manifest.RootElement.GetProperty("input_file").GetString()!);
        string oraclePath = TestRepository.PathFromRoot(relative, manifest.RootElement.GetProperty("oracle_file").GetString()!);
        Assert.AreEqual(Hash(inputPath), manifest.RootElement.GetProperty("input_sha256").GetString());
        Assert.AreEqual(Hash(oraclePath), manifest.RootElement.GetProperty("oracle_sha256").GetString());
        return new(manifest, JsonDocument.Parse(File.ReadAllText(inputPath)), JsonDocument.Parse(File.ReadAllText(oraclePath)));
    }

    private static async Task<MemoryStream> RequestAsync(HelperAssignmentKindV2 kind)
    {
        MemoryStream request = new();
        await HelperPrivateProtocolV2.WriteAsync(request, HelperTestFrames.Bootstrap(), CancellationToken.None);
        await HelperPrivateProtocolV2.WriteAsync(request, HelperTestFrames.Assignment(kind), CancellationToken.None);
        request.Position = 0;
        return request;
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static byte[] RecursiveUnknownFrame()
    {
        HelperPrivateFrameV2 frame = HelperTestFrames.Bootstrap();
        byte[] bootstrap = [.. frame.Bootstrap.ToByteArray(), 0x80, 0x05, 0x00];
        using MemoryStream payload = new();
        using (CodedOutputStream output = new(payload, leaveOpen: true))
        {
            output.WriteTag(1, WireFormat.WireType.Varint);
            output.WriteUInt64(frame.Sequence);
            output.WriteTag(2, WireFormat.WireType.LengthDelimited);
            output.WriteBytes(frame.ProtocolFingerprintSha256);
            output.WriteTag(10, WireFormat.WireType.LengthDelimited);
            output.WriteBytes(ByteString.CopyFrom(bootstrap));
        }
        return Frame(payload.ToArray());
    }

    private static byte[] OversizedFrame()
    {
        byte[] frame = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            frame, HelperPrivateProtocolV2.MaximumMessageBytes + 1U);
        return frame;
    }

    private static byte[] Frame(byte[] payload)
    {
        byte[] frame = new byte[payload.Length + 4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(frame, checked((uint)payload.Length));
        payload.CopyTo(frame, 4);
        return frame;
    }

    private static byte[] AppendPayload(byte[] frame, byte[] suffix)
    {
        byte[] result = [.. frame, .. suffix];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result, checked((uint)(result.Length - 4)));
        return result;
    }

    private static byte[] InsertPayload(byte[] frame, int payloadOffset, byte[] bytes)
    {
        byte[] result = new byte[frame.Length + bytes.Length];
        frame.AsSpan(0, 4 + payloadOffset).CopyTo(result);
        bytes.CopyTo(result, 4 + payloadOffset);
        frame.AsSpan(4 + payloadOffset).CopyTo(result.AsSpan(4 + payloadOffset + bytes.Length));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(result, checked((uint)(result.Length - 4)));
        return result;
    }

    private static bool Rejects(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return true;
        }
    }

    private static async Task<bool> RejectsAsync<T>(Func<Task<T>> action)
    {
        try
        {
            await action();
            return false;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return true;
        }
    }

    private static HashSet<int> ProcessIds(string processName) =>
        Process.GetProcessesByName(processName).Select(process =>
        {
            using (process)
            {
                return process.Id;
            }
        }).ToHashSet();

    private static string ExerciseBackupRestore(string root, DateTimeOffset now)
    {
        string sourceRoot = Path.Combine(root, "restore-source");
        using AuthoritativeStore source = new(new StoragePaths(sourceRoot));
        source.PublishProviderCatalog(OpenAiProviderProfileCatalog.Capability, OpenAiProviderProfileCatalog.Price, now);
        source.BeginCredentialEnrollment(
            "profile-restore", "generation-restore", "Restore", now.AddSeconds(1), "account-1", "billing-1");
        source.ApplyCredentialTransition(new(
            "restore-activate", "profile-restore", "generation-restore", "enroll", "pending-enrollment",
            "active-unverified", "active-unverified", OpenAiProviderProfileCatalog.Capability.Identity.Value,
            "account-1", "billing-1", now.AddSeconds(2), now.AddSeconds(3)));
        BackupArtifact backup = source.CreateBackup("CredentialSyntheticValidation", now.AddSeconds(4));
        StoragePaths restoredPaths = new(Path.Combine(root, "restore-target"));
        AuthoritativeStore.RestoreBackup(backup, restoredPaths);
        using AuthoritativeStore restored = new(restoredPaths);
        return restored.GetCredentialProfile("profile-restore").LifecycleState;
    }

    private static (int Secret, int Target) MeasureCanaries(string root)
    {
        int secret = 0;
        int target = 0;
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(path => !path.Contains("fake-secure-store", StringComparison.OrdinalIgnoreCase)))
        {
            string value = Convert.ToHexString(File.ReadAllBytes(file));
            secret += Count(value, Convert.ToHexString("INFINIUM-HELPER-TEST-SECRET"u8));
            target += Count(value, Convert.ToHexString("CAPABILITY-BOUND-STORE-TARGET-CANARY"u8));
        }
        return (secret, target);

        static int Count(string value, string needle)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += needle.Length;
            }
            return count;
        }
    }

    private static async Task<(CoordinatedHelperReceipt Helper, CredentialProfileProjection Projection)> ExecuteTransition(
        CredentialHelperCoordinator coordinator,
        string root,
        HelperAssignmentKindV2 kind,
        string generation,
        byte nonce,
        string from,
        string to,
        DateTimeOffset now,
        bool incrementRevocation = false)
    {
        HelperPrivateFrameV2 bootstrap = HelperTestFrames.Bootstrap(nonceSeed: nonce);
        bootstrap.Bootstrap.Credential.AccessProfileId.Value = "profile-eval";
        bootstrap.Bootstrap.Credential.GenerationId.Value = kind == HelperAssignmentKindV2.Replace
            ? "generation-1"
            : generation;
        HelperPrivateFrameV2 assignment = HelperTestFrames.Assignment(kind);
        assignment.Assignment.AccessProfileId.Value = "profile-eval";
        assignment.Assignment.GenerationId.Value = generation;
        assignment.Assignment.GenerationOrdinal = generation == "generation-1" ? 1UL : 2UL;
        assignment.Assignment.Credential.AccessProfileId.Value = "profile-eval";
        assignment.Assignment.Credential.GenerationId.Value = generation;
        assignment.Assignment.AssignmentId = root + "-assignment";
        assignment.Assignment.CommandId = root + "-command";
        bootstrap.Bootstrap.CommandId = assignment.Assignment.CommandId;
        return await coordinator.ExecuteCredentialTransitionAsync(
            root + "-attempt",
            bootstrap,
            assignment,
            now);
    }

    private static void AssertOracleAndEveryMutationFails(JsonElement oracle, Dictionary<string, object> actual)
    {
        foreach (JsonProperty property in oracle.EnumerateObject())
        {
            Assert.IsTrue(actual.TryGetValue(property.Name, out object? value), property.Name);
            Assert.AreEqual(property.Value.GetRawText(), JsonSerializer.Serialize(value), property.Name);
            object mutation = value switch
            {
                bool item => !item,
                int item => item + 1,
                long item => item + 1,
                string item => item + "-wrong",
                _ => throw new InvalidOperationException("The oracle field type is not mutation-covered."),
            };
            Assert.AreNotEqual(property.Value.GetRawText(), JsonSerializer.Serialize(mutation), property.Name);
        }
        Assert.AreEqual(oracle.EnumerateObject().Count(), actual.Count);
    }

    private static void AssertInputHeaderAndMutations(
        JsonElement input,
        string expectedCase,
        string[] expectedProperties)
    {
        CollectionAssert.AreEquivalent(
            expectedProperties,
            input.EnumerateObject().Select(property => property.Name).ToArray());
        Assert.AreEqual("infinium.public.credential-helper-input/v1", input.GetProperty("schema").GetString());
        Assert.AreEqual(expectedCase, input.GetProperty("case").GetString());
        Assert.AreNotEqual(
            "infinium.public.credential-helper-input/v1-mutated",
            input.GetProperty("schema").GetString());
        Assert.AreNotEqual(expectedCase + "-mutated", input.GetProperty("case").GetString());
    }

    private static CredentialProfileProjection Transition(
        AuthoritativeStore store,
        string root,
        string generation,
        string kind,
        string from,
        string to,
        DateTimeOffset pendingAt,
        bool incrementRevocation = false)
    {
        bool deleted = to == "deleted";
        return store.ApplyCredentialTransition(new(
            root, "profile-eval", generation, kind, from, to, to,
            deleted ? null : OpenAiProviderProfileCatalog.Capability.Identity.Value,
            deleted ? null : "account-1", deleted ? null : "billing-1",
            pendingAt, pendingAt.AddSeconds(1), IncrementRevocationEpoch: incrementRevocation));
    }

    private sealed record Fixture(JsonDocument Manifest, JsonDocument Input, JsonDocument Oracle);

    private sealed class FrozenTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
