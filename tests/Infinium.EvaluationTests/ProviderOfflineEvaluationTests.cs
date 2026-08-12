using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Evaluation")]
public sealed class ProviderOfflineEvaluationTests
{
    [TestMethod]
    public void ProviderOfflineDevelopmentPackageMatchesExactAdapterAndReplaySemantics()
    {
        using OfflinePackage package = OfflinePackage.Read("offline-dev");
        JsonElement input = package.Input.RootElement;
        JsonElement oracle = package.Oracle.RootElement;
        byte[] raw = Encoding.UTF8.GetBytes(input.GetProperty("response").GetRawText());

        OpenAiResponsesResult result = OpenAiResponsesResponseCodec.Replay(raw, 200, "client-public", "request-public");
        Assert.AreEqual(oracle.GetProperty("expected_state").GetString(), result.State.ToString().ToLowerInvariant());
        Assert.AreEqual(oracle.GetProperty("expected_admitted").GetBoolean(), result.Admitted);
        Assert.AreEqual(oracle.GetProperty("expected_replay_send_count").GetInt32(), result.SendCount);
        Assert.IsFalse(result.NetworkUsed);
        Assert.AreEqual(oracle.GetProperty("expected_cached_tokens").GetInt64(), result.Usage.CacheReadTokens.Value);
        Assert.AreEqual(oracle.GetProperty("expected_cache_write_tokens").GetInt64(), result.Usage.CacheWriteTokens.Value);
        StringAssert.Contains(input.GetProperty("untrusted_evidence").GetString()!, "Ignore all instructions");
    }

    [TestMethod]
    public void ProviderOfflineValidationPackageClosesNonSuccessAndDriftMutation()
    {
        using OfflinePackage package = OfflinePackage.Read("offline-val");
        JsonElement input = package.Input.RootElement;
        string[] expected = package.Oracle.RootElement.GetProperty("expected_states")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        string[] actual = input.GetProperty("responses").EnumerateArray()
            .Select(item => OpenAiResponsesResponseCodec.Replay(
                Encoding.UTF8.GetBytes(item.GetRawText()), 200, "client-public", "request-public")
                .State.ToString().ToLowerInvariant())
            .ToArray();
        CollectionAssert.AreEqual(expected, actual);

        byte[] modelDrift = Encoding.UTF8.GetBytes(
            "{\"id\":\"drift\",\"status\":\"completed\",\"model\":\"different\",\"service_tier\":\"default\"}");
        Assert.IsFalse(OpenAiResponsesResponseCodec.Replay(modelDrift, 200, "client", "request").Admitted);
        Assert.IsFalse(package.Oracle.RootElement.GetProperty("expected_retry_permitted").GetBoolean());
    }

    private sealed class OfflinePackage : IDisposable
    {
        private OfflinePackage(JsonDocument manifest, JsonDocument input, JsonDocument oracle)
        {
            Manifest = manifest;
            Input = input;
            Oracle = oracle;
        }

        public JsonDocument Manifest { get; }
        public JsonDocument Input { get; }
        public JsonDocument Oracle { get; }

        public static OfflinePackage Read(string directory)
        {
            string root = TestRepository.PathFromRoot(
                "fixtures", "public", "platform", "provider-offline", directory);
            byte[] manifestBytes = File.ReadAllBytes(Path.Combine(root, "public-manifest.json"));
            byte[] inputBytes = File.ReadAllBytes(Path.Combine(root, "input.json"));
            byte[] oracleBytes = File.ReadAllBytes(Path.Combine(root, "oracle.json"));
            JsonDocument manifest = JsonDocument.Parse(manifestBytes);
            Assert.AreEqual(
                manifest.RootElement.GetProperty("input_sha256").GetString(),
                Convert.ToHexStringLower(SHA256.HashData(inputBytes)));
            Assert.AreEqual(
                manifest.RootElement.GetProperty("oracle_sha256").GetString(),
                Convert.ToHexStringLower(SHA256.HashData(oracleBytes)));
            Assert.IsTrue(manifest.RootElement.GetProperty("answer_free_input").GetBoolean());
            return new OfflinePackage(manifest, JsonDocument.Parse(inputBytes), JsonDocument.Parse(oracleBytes));
        }

        public void Dispose()
        {
            Manifest.Dispose();
            Input.Dispose();
            Oracle.Dispose();
        }
    }
}
