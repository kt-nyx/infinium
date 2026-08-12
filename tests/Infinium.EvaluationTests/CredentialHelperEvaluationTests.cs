using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Provider;
using Infinium.Application.Runtime;
using Infinium.Contracts.Protobuf.Helper.V2;
using Infinium.CredentialHelper;
using Infinium.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CredentialHelperEvaluationTests
{
    private static readonly string?[] ExpectedLifecycleOperations =
        ["pending-enrollment", "activation", "verification", "replacement", "disable", "delete"];

    [TestMethod]
    public async Task CredentialSyntheticDevelopmentPackageDrivesOneShotLifecycleOracle()
    {
        Fixture package = Load("lifecycle-dev", "M1-PLAT-CREDENTIAL-HELPER-DEV-v1");
        JsonElement input = package.Input.RootElement;
        JsonElement oracle = package.Oracle.RootElement;
        CollectionAssert.AreEqual(
            ExpectedLifecycleOperations,
            input.GetProperty("operations").EnumerateArray().Select(item => item.GetString()).ToArray());
        Assert.AreEqual("inherited-anonymous-pipe", input.GetProperty("transport").GetString());
        Assert.AreEqual("deterministic-fake", input.GetProperty("secure_store").GetString());

        using DeterministicFakeSecureStore store = new();
        OneShotHelperEngine engine = new(store);
        using MemoryStream request = await RequestAsync(HelperAssignmentKindV2.Enroll);
        using MemoryStream response = new();
        await engine.RunAsync(request, response, CancellationToken.None);
        HelperPrivateFrameV2 receipt = HelperPrivateProtocolV2.Decode(response.ToArray(), 3);
        Assert.AreEqual(HelperOutcomeV2.Completed, receipt.Receipt.Outcome);
        string root = Path.Combine(Path.GetTempPath(), "Infinium-Wp3-Eval-" + Guid.NewGuid().ToString("N"));
        using AuthoritativeStore state = new(new StoragePaths(root));
        DateTimeOffset now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        state.PublishProviderCatalog(M1ProviderCatalog.Capability, M1ProviderCatalog.Price, now);
        CredentialProfileProjection pending = state.BeginCredentialEnrollment(
            "profile-eval", "generation-1", "Synthetic", now.AddSeconds(1), "account-1", "billing-1");
        Assert.AreEqual("pending-enrollment", pending.LifecycleState);
        _ = Transition(state, "activate", "generation-1", "enroll", "pending-enrollment", "active-unverified", now.AddSeconds(2));
        _ = Transition(state, "verify-1", "generation-1", "verify", "active-unverified", "active-verified", now.AddSeconds(4));
        state.AddCredentialGeneration("profile-eval", "generation-2", 2, 0, now.AddSeconds(6));
        _ = Transition(state, "replace-1", "generation-2", "replace", "active-verified", "replacing", now.AddSeconds(7));
        _ = Transition(state, "replace-2", "generation-2", "replace", "replacing", "active-unverified", now.AddSeconds(9));
        _ = Transition(state, "verify-2", "generation-2", "verify", "active-unverified", "active-verified", now.AddSeconds(11));
        _ = Transition(state, "disable", "generation-2", "disable", "active-verified", "disabled", now.AddSeconds(13));
        _ = Transition(state, "delete-pending", "generation-2", "delete", "disabled", "delete-pending", now.AddSeconds(15), true);
        CredentialProfileProjection terminal = Transition(
            state, "delete", "generation-2", "delete", "delete-pending", "deleted", now.AddSeconds(17));
        Assert.AreEqual(oracle.GetProperty("expected_terminal_state").GetString(), terminal.LifecycleState);
        Assert.AreEqual(oracle.GetProperty("expected_generation_ordinal").GetInt32(), terminal.GenerationOrdinal);
        Assert.AreEqual(oracle.GetProperty("expected_revocation_epoch").GetInt32(), terminal.RevocationEpoch);
        Assert.AreEqual(2, oracle.GetProperty("expected_private_handle_count").GetInt32());
        Assert.AreEqual(0, oracle.GetProperty("expected_standard_protocol_handle_count").GetInt32());
        Assert.IsTrue(oracle.GetProperty("expected_stage_before_admit").GetBoolean());
        Assert.IsTrue(oracle.GetProperty("expected_coordinator_only_admission").GetBoolean());
        Assert.IsFalse(oracle.GetProperty("expected_retry").GetBoolean());
        Assert.AreEqual(DeterministicFakeSecureStore.NativeOperationCount,
            oracle.GetProperty("expected_native_operations").GetInt32());
        Assert.AreEqual(0, oracle.GetProperty("expected_network_operations").GetInt32());
    }

    [TestMethod]
    public async Task CredentialSyntheticValidationPackageDrivesStrictFaultOracleAndRejectsMutation()
    {
        Fixture package = Load("faults-val", "M1-PLAT-CREDENTIAL-HELPER-VAL-v1");
        JsonElement input = package.Input.RootElement;
        JsonElement oracle = package.Oracle.RootElement;
        Assert.AreEqual(oracle.GetProperty("expected_rejections").GetInt32(),
            input.GetProperty("faults").GetArrayLength());
        byte[] canonical = HelperPrivateProtocolV2.Encode(HelperTestFrames.Bootstrap());
        Assert.ThrowsExactly<InvalidDataException>(() => HelperPrivateProtocolV2.Decode(canonical, 2));

        using DeterministicFakeSecureStore store = new() { Available = false };
        OneShotHelperEngine engine = new(store);
        using MemoryStream request = await RequestAsync(HelperAssignmentKindV2.Verify);
        using MemoryStream response = new();
        await engine.RunAsync(request, response, CancellationToken.None);
        Assert.AreEqual(HelperOutcomeV2.Unavailable,
            HelperPrivateProtocolV2.Decode(response.ToArray(), 3).Receipt.Outcome);
        Assert.AreEqual(0, oracle.GetProperty("expected_transport_starts").GetInt32());
        Assert.AreEqual(0, oracle.GetProperty("expected_retries").GetInt32());
        Assert.AreEqual(0, oracle.GetProperty("expected_secret_canary_matches").GetInt32());
        Assert.AreEqual(0, oracle.GetProperty("expected_target_canary_matches").GetInt32());
        Assert.AreEqual(0, oracle.GetProperty("expected_process_tree_survivors").GetInt32());
        Assert.AreEqual("recovery-required", oracle.GetProperty("expected_restore_state").GetString());
        Assert.AreEqual(0, oracle.GetProperty("expected_native_operations").GetInt32());
        Assert.AreEqual(0, oracle.GetProperty("expected_network_operations").GetInt32());

        string mutated = package.Oracle.RootElement.GetRawText()
            .Replace("\"expected_native_operations\":0", "\"expected_native_operations\":1", StringComparison.Ordinal);
        using JsonDocument wrong = JsonDocument.Parse(mutated);
        Assert.AreNotEqual(DeterministicFakeSecureStore.NativeOperationCount,
            wrong.RootElement.GetProperty("expected_native_operations").GetInt32());
    }

    private static Fixture Load(string directory, string identity)
    {
        string relative = Path.Combine("fixtures", "public", "platform", "credential-helper", directory);
        string manifestPath = TestRepository.PathFromRoot(relative, "public-manifest.json");
        using JsonDocument registry = TestRepository.ReadJson("fixtures", "public", "public-fixture-registry.v1.json");
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
            deleted ? null : M1ProviderCatalog.Capability.Identity.Value,
            deleted ? null : "account-1", deleted ? null : "billing-1",
            pendingAt, pendingAt.AddSeconds(1), IncrementRevocationEpoch: incrementRevocation));
    }

    private sealed record Fixture(JsonDocument Manifest, JsonDocument Input, JsonDocument Oracle);
}
