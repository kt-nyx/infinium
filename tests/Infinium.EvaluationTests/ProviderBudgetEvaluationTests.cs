using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Provider;
using Infinium.Domain.Contracts;
using Infinium.OpenAI;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderBudgetEvaluationTests
{
    private static readonly DateTimeOffset BaseTime = DateTimeOffset.Parse(
        "2026-08-10T00:00:00.0000000+00:00",
        System.Globalization.CultureInfo.InvariantCulture);
    private static readonly string[] RequestedAuthorityFacts = ["spend", "billing", "credit", "rate"];
    private static readonly string[] BudgetDimensionNames = ["dispatch_count", "input_tokens", "output_tokens",
        "total_tokens", "reasoning_tokens", "cache_read_tokens", "cache_write_tokens", "priced_tool_calls", "nano_usd"];

    [TestMethod]
    [TestCategory("Evaluation")]
    public void ProviderCapabilityDevelopmentPackageDrivesExactProductionCatalog()
    {
        using Package package = Package.Read("PROVIDER-CAPABILITY-DEV-v1");
        JsonElement input = package.Input.RootElement;
        AssertInputProperties(input, ["schema", "case", "provider", "model", "service_tier", "reasoning_effort", "cache_mode"]);
        Assert.AreEqual("exact-provider-profile-catalog", input.GetProperty("case").GetString());
        ProviderCapabilitySnapshotContract capability = OpenAiProviderProfileCatalog.Capability;
        Assert.AreEqual(input.GetProperty("provider").GetString(), capability.Provider);
        Assert.AreEqual(input.GetProperty("model").GetString(), capability.Model);
        Assert.AreEqual(input.GetProperty("service_tier").GetString(), capability.ServiceTier);
        Assert.AreEqual(input.GetProperty("reasoning_effort").GetString(), capability.ReasoningEffort);
        Assert.AreEqual("off", input.GetProperty("cache_mode").GetString());
        JsonObject actual = new()
        {
            ["schema"] = "infinium.public.provider-capability-oracle/v1",
            ["expected_provider"] = capability.Provider,
            ["expected_model"] = capability.Model,
            ["expected_service_tier"] = capability.ServiceTier,
            ["expected_context_tokens"] = capability.MaximumContextTokens,
            ["expected_network_used"] = false,
            ["expected_credentials_used"] = false,
        };
        AssertOracleAndMutation(package.Oracle, actual);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void ProviderCapabilityValidationPackageDrivesUnavailableAuthorityProjection()
    {
        using Package package = Package.Read("PROVIDER-CAPABILITY-VAL-v1");
        JsonElement input = package.Input.RootElement;
        AssertInputProperties(input, ["schema", "case", "provider", "model", "requested_facts"]);
        Assert.AreEqual("explicit-unavailable-facts", input.GetProperty("case").GetString());
        Assert.AreEqual("openai", input.GetProperty("provider").GetString());
        Assert.AreEqual("gpt-5.6-sol", input.GetProperty("model").GetString());
        string[] requested = input.GetProperty("requested_facts").EnumerateArray().Select(item => item.GetString()!).ToArray();
        CollectionAssert.AreEqual(RequestedAuthorityFacts, requested);
        ProviderCatalogProjection projection = OpenAiProviderProfileCatalog.CreateNonLiveProjection(new UtcTimestamp(BaseTime));
        Assert.IsTrue(new[] { projection.ProviderSpendLimit, projection.ProviderHistoricalCost,
            projection.ProviderCredit, projection.ProviderRateHeadroom }
            .All(fact => fact.Availability == ProviderAvailabilityState.Unavailable && fact.Value is null));
        JsonObject actual = new()
        {
            ["schema"] = "infinium.public.provider-capability-oracle/v1",
            ["expected_fact_state"] = "unavailable",
            ["expected_network_used"] = projection.NetworkPermitted,
            ["expected_credentials_used"] = projection.CredentialAccessPermitted,
            ["forbidden_inference"] = "zero-or-unlimited",
        };
        AssertOracleAndMutation(package.Oracle, actual);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void ProviderAuthorityDevelopmentPackageDrivesRealSqliteFinalGate()
    {
        using Package package = Package.Read("PROVIDER-AUTHORIZATION-DEV-v1");
        JsonElement input = package.Input.RootElement;
        AssertInputProperties(input, ["schema", "case", "execution_mode", "profile_state", "transport_state"]);
        Assert.AreEqual("eligible-simulated-dispatch", input.GetProperty("case").GetString());
        Assert.AreEqual("simulated-nonnetwork", input.GetProperty("execution_mode").GetString());
        Assert.AreEqual("active-verified", input.GetProperty("profile_state").GetString());
        Assert.AreEqual("not-started", input.GetProperty("transport_state").GetString());
        using EvaluationBudgetContext context = EvaluationBudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        ProviderDispatchGateReceipt gate = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        JsonObject actual = new()
        {
            ["schema"] = "infinium.public.provider-authority-oracle/v1",
            ["expected_gate"] = gate.Authorized ? "authorized" : "rejected",
            ["expected_fence_count"] = context.Count("provider_dispatch_fences"),
            ["expected_network_used"] = false,
            ["expected_credentials_used"] = false,
        };
        AssertOracleAndMutation(package.Oracle, actual);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void ProviderAuthorityValidationPackageDrivesAmbiguousFullHold()
    {
        using Package package = Package.Read("PROVIDER-AUTHORIZATION-VAL-v1");
        JsonElement input = package.Input.RootElement;
        AssertInputProperties(input, ["schema", "case", "execution_mode", "transport_state", "usage_state"]);
        Assert.AreEqual("ambiguous-transport", input.GetProperty("case").GetString());
        Assert.AreEqual("simulated-nonnetwork", input.GetProperty("execution_mode").GetString());
        Assert.AreEqual("may-have-started", input.GetProperty("transport_state").GetString());
        Assert.AreEqual("unavailable", input.GetProperty("usage_state").GetString());
        using EvaluationBudgetContext context = EvaluationBudgetContext.Create();
        _ = context.Store.ReserveProviderBudget(1, context.Request);
        ProviderDispatchGateReceipt gate = context.Store.AuthorizeProviderDispatch(context.GateRequest);
        context.Store.RecordProviderTransportStart(
            "operation-restore", "attempt-settlement", "request-settlement", gate.DispatchFenceId,
            ambiguous: true, BaseTime.AddSeconds(6));
        DeterministicProviderTranscript transcript = DeterministicProviderSimulator.Execute(
            ProviderSimulatorOutcome.AmbiguousStart, context.Limits, new UtcTimestamp(BaseTime.AddSeconds(6)));
        ProviderBudgetSettlementReceipt settlement = context.Store.SettleProviderBudget(new(
            "fixture-ambiguous:settlement", "reservation-settlement", ProviderBudgetEventKind.RetainedAmbiguous,
            null, null, BaseTime.AddSeconds(7)));
        Assert.IsTrue(transcript.TransportStartAmbiguous);
        JsonObject actual = new()
        {
            ["schema"] = "infinium.public.provider-authority-oracle/v1",
            ["expected_hold"] = settlement.Unresolved == context.Vector ? "full-reservation" : "incorrect",
            ["expected_retry"] = settlement.RetryPermitted,
            ["expected_network_used"] = false,
            ["expected_credentials_used"] = false,
        };
        AssertOracleAndMutation(package.Oracle, actual);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void AtomicBudgetDevelopmentPackageDrivesEightScopeCommitAndRebuild()
    {
        using Package package = Package.Read("PROVIDER-BUDGET-DEV-v1");
        JsonElement input = package.Input.RootElement;
        AssertInputProperties(input, ["schema", "case", "dimensions"]);
        Assert.AreEqual("eight-scope-reservation", input.GetProperty("case").GetString());
        ProviderBudgetVectorContract declared = ReadVector(input.GetProperty("dimensions"));
        using EvaluationBudgetContext context = EvaluationBudgetContext.Create();
        Assert.AreEqual(context.Vector, declared);
        _ = context.Store.ReserveProviderBudget(1, context.Request with { Reserved = declared });
        IReadOnlyList<ProviderBudgetProjectionContract> rebuilt =
            context.Store.RebuildProviderBudgetProjections(BaseTime.AddSeconds(6));
        JsonObject actual = new()
        {
            ["schema"] = "infinium.public.atomic-budget-oracle/v1",
            ["expected_scope_count"] = rebuilt.Count,
            ["expected_atomic_commit"] = rebuilt.All(item => item.Reserved == declared),
            ["expected_partial_debit"] = false,
            ["expected_projection_rebuild_equal"] = rebuilt.All(item => item.Reserved == declared),
        };
        AssertOracleAndMutation(package.Oracle, actual);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    public void AtomicBudgetValidationPackageDrivesContentionAndOverflowFailure()
    {
        using Package package = Package.Read("PROVIDER-BUDGET-VAL-v1");
        JsonElement input = package.Input.RootElement;
        AssertInputProperties(input, ["schema", "case", "dimensions"]);
        Assert.AreEqual("overflow-and-contention", input.GetProperty("case").GetString());
        string[] dimensions = input.GetProperty("dimensions").EnumerateArray().Select(item => item.GetString()!).ToArray();
        CollectionAssert.AreEqual(BudgetDimensionNames, dimensions);
        using EvaluationBudgetContext context = EvaluationBudgetContext.Create();
        using AuthoritativeStore contender = new(new StoragePaths(context.Root));
        Exception? first = null;
        Exception? second = null;
        using ManualResetEventSlim start = new(false);
        Task a = Task.Run(() => { start.Wait(); try { context.Store.ReserveProviderBudget(1, context.Request with { ReservationId = "fixture-race-a" }); } catch (Exception error) { first = error; } });
        Task b = Task.Run(() => { start.Wait(); try { contender.ReserveProviderBudget(1, context.Request with { ReservationId = "fixture-race-b" }); } catch (Exception error) { second = error; } });
        start.Set();
        Task.WaitAll(a, b);
        int winners = new[] { first, second }.Count(error => error is null);
        bool overflowClosed = !ProviderBudgetVectorContract.FitsWithin(
            new(long.MaxValue, 0, 0, 0, 0, 0, 0, 0, 0),
            context.Vector,
            new(long.MaxValue, context.Vector.InputTokens, context.Vector.OutputTokens,
                context.Vector.TotalTokens, context.Vector.ReasoningTokens, 0, 0, 0, context.Vector.NanoUsd));
        JsonObject actual = new()
        {
            ["schema"] = "infinium.public.atomic-budget-oracle/v1",
            ["expected_winners"] = winners,
            ["expected_overflow"] = overflowClosed ? "fail-closed" : "incorrect",
            ["expected_partial_debit"] = context.Count("provider_budget_events") != 8,
            ["expected_network_used"] = false,
            ["expected_credentials_used"] = false,
        };
        AssertOracleAndMutation(package.Oracle, actual);
    }

    private static ProviderBudgetVectorContract ReadVector(JsonElement value)
    {
        AssertInputProperties(value, ["dispatch_count", "input_tokens", "output_tokens", "total_tokens",
            "reasoning_tokens", "cache_read_tokens", "cache_write_tokens", "priced_tool_calls", "nano_usd"]);
        return new(value.GetProperty("dispatch_count").GetInt64(), value.GetProperty("input_tokens").GetInt64(),
            value.GetProperty("output_tokens").GetInt64(), value.GetProperty("total_tokens").GetInt64(),
            value.GetProperty("reasoning_tokens").GetInt64(), value.GetProperty("cache_read_tokens").GetInt64(),
            value.GetProperty("cache_write_tokens").GetInt64(), value.GetProperty("priced_tool_calls").GetInt64(),
            value.GetProperty("nano_usd").GetInt64());
    }

    private static void AssertInputProperties(JsonElement value, string[] expected)
    {
        CollectionAssert.AreEquivalent(expected, value.EnumerateObject().Select(property => property.Name).ToArray());
        if (expected.Contains("schema", StringComparer.Ordinal))
        {
            Assert.IsTrue(value.GetProperty("schema").GetString()!.StartsWith("infinium.public.", StringComparison.Ordinal));
        }
    }

    private static void AssertOracleAndMutation(JsonDocument oracleDocument, JsonObject actual)
    {
        JsonObject oracle = JsonNode.Parse(oracleDocument.RootElement.GetRawText())!.AsObject();
        AssertOracleEquals(oracle, actual);
        JsonObject mutated = oracle.DeepClone().AsObject();
        KeyValuePair<string, JsonNode?> target = mutated.First(property => property.Key != "schema");
        JsonValue value = target.Value!.AsValue();
        if (value.TryGetValue(out bool boolean)) { mutated[target.Key] = !boolean; }
        else if (value.TryGetValue(out long number)) { mutated[target.Key] = checked(number + 1); }
        else if (value.TryGetValue(out string? text)) { mutated[target.Key] = text + "-wrong"; }
        else { throw new InvalidDataException("Oracle mutation target is not one closed scalar."); }
        Assert.ThrowsExactly<InvalidDataException>(() => AssertOracleEquals(mutated, actual));
    }

    private static void AssertOracleEquals(JsonObject oracle, JsonObject actual)
    {
        if (oracle.Count != actual.Count || oracle.Any(expected => !actual.TryGetPropertyValue(expected.Key, out JsonNode? actualValue)
            || !JsonNode.DeepEquals(expected.Value, actualValue)))
        {
            throw new InvalidDataException("The production outcome does not equal every independently expected oracle field.");
        }
    }

    private sealed class Package : IDisposable
    {
        private Package(JsonDocument input, JsonDocument oracle) { Input = input; Oracle = oracle; }
        public JsonDocument Input { get; }
        public JsonDocument Oracle { get; }

        public static Package Read(string fixtureId)
        {
            string root = FindRepositoryRoot();
            using JsonDocument registry = JsonDocument.Parse(File.ReadAllBytes(
                Path.Combine(root, "fixtures", "public", "current-fixture-registry.v1.json")));
            JsonElement entry = registry.RootElement.GetProperty("packages").EnumerateArray()
                .Single(item => item.GetProperty("package_identity").GetString() == fixtureId
                    && item.GetProperty("package_version").GetString() == "1.0.0");
            string manifestPath = Path.Combine(root, entry.GetProperty("authority_file").GetString()!
                .Replace('/', Path.DirectorySeparatorChar));
            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            Assert.AreEqual(entry.GetProperty("authority_bytes").GetInt64(), manifestBytes.LongLength);
            Assert.AreEqual(entry.GetProperty("authority_sha256").GetString(), Hash(manifestBytes));
            using JsonDocument manifest = JsonDocument.Parse(manifestBytes);
            Assert.IsTrue(manifest.RootElement.GetProperty("answer_free_input").GetBoolean());
            Assert.AreEqual(fixtureId, manifest.RootElement.GetProperty("fixture_id").GetString());
            string directory = Path.GetDirectoryName(manifestPath)!;
            byte[] inputBytes = File.ReadAllBytes(Path.Combine(directory, manifest.RootElement.GetProperty("input_file").GetString()!));
            byte[] oracleBytes = File.ReadAllBytes(Path.Combine(directory, manifest.RootElement.GetProperty("oracle_file").GetString()!));
            Assert.AreEqual(manifest.RootElement.GetProperty("input_sha256").GetString(), Hash(inputBytes));
            Assert.AreEqual(manifest.RootElement.GetProperty("oracle_sha256").GetString(), Hash(oracleBytes));
            JsonDocument input = JsonDocument.Parse(inputBytes);
            Assert.IsFalse(input.RootElement.EnumerateObject().Any(property =>
                property.Name.StartsWith("expected", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("oracle", StringComparison.OrdinalIgnoreCase)));
            return new(input, JsonDocument.Parse(oracleBytes));
        }

        public void Dispose() { Input.Dispose(); Oracle.Dispose(); }
        private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private sealed class EvaluationBudgetContext : IDisposable
    {
        private EvaluationBudgetContext(string root, AuthoritativeStore store, ProviderBudgetVectorContract vector,
            IReadOnlyList<ProviderBudgetScopeContract> scopes)
        {
            Root = root; Store = store; Vector = vector; Scopes = scopes;
            Limits = new(65_536, 20, 10, 1_048_576, 1, 400_000, 120_000);
            Request = new("reservation-settlement", "operation-restore", "attempt-settlement", "request-settlement",
                vector, scopes, BaseTime.AddSeconds(90), BaseTime.AddSeconds(4));
            GateRequest = new("fence-settlement", "authorization-settlement", "operation-restore",
                "reservation-settlement", "attempt-settlement", "request-settlement", "profile-restore",
                "generation-restore", 0, 1, BaseTime.AddSeconds(5));
        }
        public string Root { get; }
        public AuthoritativeStore Store { get; }
        public ProviderBudgetVectorContract Vector { get; }
        public IReadOnlyList<ProviderBudgetScopeContract> Scopes { get; }
        public ProviderFiniteLimitsContract Limits { get; }
        public ProviderBudgetReservationRequest Request { get; }
        public ProviderDispatchGateRequest GateRequest { get; }

        public static EvaluationBudgetContext Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "Infinium-ProviderBudget-Evaluation-" + Guid.NewGuid().ToString("N"));
            AuthoritativeStore store = new(new StoragePaths(root));
            try
            {
                PersistenceAndLifecycleTests.SeedProviderAuthorityBlock(root, "running");
                using SqliteConnection connection = new($"Data Source={store.Paths.Database};Pooling=False");
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = AuthorizationSql;
                Assert.AreEqual(3, command.ExecuteNonQuery());
                CoordinatorAuthority authority = store.AcquireCoordinatorAuthority(
                    "provider-budget-evaluation", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
                Assert.AreEqual(1L, authority.FencingEpoch);
                ProviderBudgetVectorContract vector = new(1, 20, 10, 30, 10, 0, 0, 0, 400_000);
                string[] kinds = ["request", "operation", "evidence-acquisition-run", "analysis-run",
                    "provider-profile", "provider-account", "billing-scope", "global"];
                string[] ids = ["request-settlement", "operation-restore", "acquisition-restore", "run-restore",
                    "profile-restore", "account-restore", "billing-restore", "provider-global"];
                ProviderBudgetScopeContract[] scopes = kinds.Zip(ids,
                    (kind, id) => new ProviderBudgetScopeContract(kind, new OpaqueId(id), vector)).ToArray();
                store.ConfigureProviderBudgetScopes(1, scopes, BaseTime.AddSeconds(3));
                return new(root, store, vector, scopes);
            }
            catch { store.Dispose(); throw; }
        }

        public long Count(string table)
        {
            if (table is not ("provider_dispatch_fences" or "provider_budget_events")) { throw new ArgumentOutOfRangeException(nameof(table)); }
            using SqliteConnection database = new($"Data Source={Store.Paths.Database};Mode=ReadOnly;Pooling=False");
            database.Open();
            using SqliteCommand command = database.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table};";
            return (long)command.ExecuteScalar()!;
        }

        public void Dispose()
        {
            Store.Dispose();
            if (Directory.Exists(Root)) { Directory.Delete(Root, recursive: true); }
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Infinium.sln"))) { current = current.Parent; }
        return current?.FullName ?? throw new DirectoryNotFoundException("Infinium repository root was not found.");
    }

    private const string AuthorizationSql =
        """
        INSERT INTO provider_operation_authorizations(
          authorization_id,operation_id,owner_kind,owner_id,evidence_acquisition_run_id,job_node_id,command_id,requested_at,
          profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
          effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
          output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
          price_snapshot_id,settings_fingerprint,input_bound_policy_id,input_bound_policy_version,input_bound_proof_status,
          coordinator_fencing_epoch,maximum_request_bytes,maximum_input_tokens,maximum_output_tokens,
          maximum_raw_response_bytes,maximum_dispatch_count,maximum_calculated_nano_usd,deadline_milliseconds,
          dispatch_deadline_utc,confirmed_at)
        SELECT 'authorization-settlement',operation_id,owner_kind,owner_id,owner_id,job_node_id,command_id,requested_at,
          profile_id,generation_id,revocation_epoch,operation_kind,installation_snapshot_id,analysis_context_id,
          effective_configuration_id,resolved_input_manifest_id,prompt_id,prompt_fingerprint,output_schema_id,
          output_schema_fingerprint,request_fingerprint,canonical_request_fingerprint,capability_snapshot_id,
          price_snapshot_id,settings_fingerprint,'openai-responses-o200k-byte-envelope','v2','proved',coordinator_fencing_epoch,
          maximum_request_bytes,20,10,maximum_raw_response_bytes,maximum_dispatch_count,400000,deadline_milliseconds,
          dispatch_deadline_utc,confirmed_at
        FROM provider_operation_blocks WHERE operation_id='operation-restore';
        INSERT INTO provider_operation_attempts VALUES(
          'attempt-settlement','operation-restore',1,'proposed',1,'2026-08-10T00:00:02.0000000+00:00');
        INSERT INTO provider_requests(
          request_id,client_request_id,operation_id,provider_attempt_id,request_fingerprint,
          canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,
          input_bound_policy_version,input_bound_proof_status,payload_id,payload_fingerprint,payload_bytes,created_at)
        SELECT 'request-settlement','client-request-settlement',operation_id,'attempt-settlement',request_fingerprint,
          canonical_request_fingerprint,settings_fingerprint,output_schema_fingerprint,input_bound_policy_id,
          input_bound_policy_version,input_bound_proof_status,'request-payload-restore',request_fingerprint,1024,
          '2026-08-10T00:00:03.0000000+00:00'
        FROM provider_operation_authorizations WHERE authorization_id='authorization-settlement';
        """;
}
