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
        byte[] outputSchema = Encoding.UTF8.GetBytes(input.GetProperty("output_schema").GetRawText());

        OpenAiResponsesResult result = OpenAiResponsesResponseCodec.Parse(
            raw, 200, "client-public", "request-public", [], outputSchema);
        OpenAiResponsesResult replay = OpenAiResponsesResponseCodec.Replay(
            raw, 200, "client-public", "request-public", requestedOutputSchema: outputSchema);
        Assert.AreEqual(oracle.GetProperty("expected_state").GetString(), result.State.ToString().ToLowerInvariant());
        Assert.AreEqual(oracle.GetProperty("expected_admitted").GetBoolean(), result.Admitted);
        Assert.AreEqual(oracle.GetProperty("expected_replay_send_count").GetInt32(), replay.SendCount);
        Assert.IsFalse(replay.NetworkUsed);
        Assert.AreEqual(oracle.GetProperty("expected_cached_tokens").GetInt64(), result.Usage.CacheReadTokens.Value);
        Assert.AreEqual(oracle.GetProperty("expected_cache_write_tokens").GetInt64(), result.Usage.CacheWriteTokens.Value);
        Assert.AreEqual(oracle.GetProperty("expected_send_count").GetInt32(), result.SendCount);
        Assert.AreEqual(oracle.GetProperty("expected_network_target_class").GetString(),
            result.NetworkUsed ? "literal-loopback-only" : "none");
        Assert.AreEqual(oracle.GetProperty("expected_redirects").GetInt32(), OpenAiResponsesAdapter.RedirectsEnabled ? 1 : 0);
        Assert.AreEqual(oracle.GetProperty("expected_retries").GetInt32(), OpenAiResponsesAdapter.RetriesEnabled ? 1 : 0);
        Assert.AreEqual(oracle.GetProperty("expected_proxy_fallbacks").GetInt32(), OpenAiResponsesAdapter.ProxyFallbackEnabled ? 1 : 0);
        Assert.AreEqual(oracle.GetProperty("expected_tools").GetInt32(), OpenAiResponsesAdapter.ProviderToolsEnabled ? 1 : 0);
        Assert.AreEqual(oracle.GetProperty("expected_secret_matches").GetInt32(), SecretCanaryMatches(raw));
        Assert.AreEqual(oracle.GetProperty("expected_hostile_content_interpretation").GetString(),
            result.Admitted && !OpenAiResponsesAdapter.ProviderToolsEnabled ? "inert-data" : "interpreted");
        StringAssert.Contains(input.GetProperty("untrusted_evidence").GetString()!, "Ignore all instructions");
    }

    [TestMethod]
    public void ProviderOfflineValidationPackageClosesNonSuccessAndDriftMutation()
    {
        using OfflinePackage package = OfflinePackage.Read("offline-val");
        JsonElement input = package.Input.RootElement;
        byte[] outputSchema = Encoding.UTF8.GetBytes(input.GetProperty("output_schema").GetRawText());
        string[] expected = package.Oracle.RootElement.GetProperty("expected_states")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        OpenAiResponsesResult[] observations = input.GetProperty("responses").EnumerateArray()
            .Select(item => OpenAiResponsesResponseCodec.Parse(
                Encoding.UTF8.GetBytes(item.GetRawText()), 200, "client-public", "request-public", [], outputSchema))
            .ToArray();
        string[] actual = observations.Select(item => item.State.ToString().ToLowerInvariant()).ToArray();
        CollectionAssert.AreEqual(expected, actual);

        byte[] modelDrift = Encoding.UTF8.GetBytes(
            "{\"id\":\"drift\",\"status\":\"completed\",\"model\":\"different\",\"service_tier\":\"default\"}");
        Assert.IsFalse(OpenAiResponsesResponseCodec.Replay(modelDrift, 200, "client", "request").Admitted);
        JsonElement oracle = package.Oracle.RootElement;
        Assert.AreEqual(oracle.GetProperty("expected_retry_permitted").GetBoolean(), observations.Any(x => x.RetryPermitted));
        Assert.AreEqual(oracle.GetProperty("expected_admitted").GetBoolean(), observations.Any(x => x.Admitted));
        Assert.AreEqual(oracle.GetProperty("expected_maximum_send_count").GetInt32(), observations.Max(x => x.SendCount));
        int replaySendCount = input.GetProperty("responses").EnumerateArray().Sum(item =>
            OpenAiResponsesResponseCodec.Replay(
                Encoding.UTF8.GetBytes(item.GetRawText()), 200, "client-public", "request-public").SendCount);
        Assert.AreEqual(oracle.GetProperty("expected_replay_send_count").GetInt32(), replaySendCount);
        Assert.AreEqual(oracle.GetProperty("expected_network_target_class").GetString(),
            observations.All(x => x.NetworkUsed) ? "literal-loopback-only" : "mixed");
        Assert.AreEqual(oracle.GetProperty("expected_redirects").GetInt32(), OpenAiResponsesAdapter.RedirectsEnabled ? 1 : 0);
        Assert.AreEqual(oracle.GetProperty("expected_retries").GetInt32(), OpenAiResponsesAdapter.RetriesEnabled ? 1 : 0);
        Assert.AreEqual(oracle.GetProperty("expected_proxy_fallbacks").GetInt32(), OpenAiResponsesAdapter.ProxyFallbackEnabled ? 1 : 0);
        Assert.AreEqual(oracle.GetProperty("expected_tools").GetInt32(), OpenAiResponsesAdapter.ProviderToolsEnabled ? 1 : 0);
        Assert.AreEqual(oracle.GetProperty("expected_secret_matches").GetInt32(),
            input.GetProperty("responses").EnumerateArray().Sum(item => SecretCanaryMatches(Encoding.UTF8.GetBytes(item.GetRawText()))));
        Assert.AreEqual(oracle.GetProperty("expected_hostile_content_interpretation").GetString(),
            !OpenAiResponsesAdapter.ProviderToolsEnabled && observations.All(x => !x.Admitted) ? "inert-data" : "interpreted");
        Assert.AreEqual(oracle.GetProperty("expected_drift_disposition").GetString(),
            OpenAiResponsesResponseCodec.Replay(modelDrift, 200, "client", "request").Admitted ? "admitted" : "rejected");

        using JsonDocument answerMutation = JsonDocument.Parse("""{"safe":{"expected_label":"leak"}}""");
        Assert.IsFalse(IsAnswerFree(answerMutation.RootElement));
        Assert.IsTrue(IsAnswerFree(input));
        Assert.IsFalse(OpenAiResponsesResponseCodec.Replay(
            modelDrift, 200, "client", "request", requestedOutputSchema: outputSchema).Admitted);
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
            foreach (string name in new[] { "purpose", "classification", "partition_history", "construction_provenance",
                "ground_truth_method", "preregistration", "reviewer_provenance", "answer_isolation",
                "replay_dependencies", "known_limitations" })
            {
                Assert.IsTrue(manifest.RootElement.TryGetProperty(name, out _), name);
            }
            JsonDocument input = JsonDocument.Parse(inputBytes);
            Assert.IsTrue(IsAnswerFree(input.RootElement));
            return new OfflinePackage(manifest, input, JsonDocument.Parse(oracleBytes));
        }

        public void Dispose()
        {
            Manifest.Dispose();
            Input.Dispose();
            Oracle.Dispose();
        }
    }

    private static bool IsAnswerFree(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                string name = property.Name.ToLowerInvariant();
                if (name is "expected" or "expected_answer" or "expected_label" or "oracle" or "answer_key"
                    || !IsAnswerFree(property.Value))
                {
                    return false;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
            {
                if (!IsAnswerFree(item))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static int SecretCanaryMatches(ReadOnlySpan<byte> value) =>
        Encoding.UTF8.GetString(value).Contains("sk-", StringComparison.Ordinal) ? 1 : 0;
}
