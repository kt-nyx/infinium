using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class M1Slice6CampaignContractTests
{
    private const string ManifestRelative = "docs/plans/milestones/m1/slices/s6/m1-slice6-finite-campaign-authorization.v1.json";
    private static readonly string[] OrderedOperations = ["Qualification", "SourceClaimExtraction", "CandidateInvestigation"];
    private static readonly string[] CampaignSchemas = ["m1-slice6-finite-campaign-authorization.v1.schema.json",
        "m1-slice6-finite-campaign-owner-authority.v1.schema.json",
        "m1-slice6-campaign-stage-request.v1.schema.json",
        "m1-slice6-campaign-stage-evidence.v1.schema.json",
        "m1-slice6-campaign-composed-evidence.v1.schema.json"];

    [TestMethod]
    public void CampaignSchemaIsRecursivelyClosedAndAuthorityBound()
    {
        foreach (string file in CampaignSchemas)
        {
            using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
                "contracts", "repository", file)));
            AssertRecursivelyClosed(schema.RootElement, "$" + file);
        }

        JsonObject manifest = ReadManifest();
        Assert.AreEqual("c9541bb5563304335e8f7af4d176eba3e507c719c4e135c542b8ac1bc4bc12be",
            manifest["authority_source"]!["attachment_sha256"]!.GetValue<string>());
        Assert.AreEqual("2026-08-22T23:59:00.0000000Z", manifest["expires_at_utc"]!.GetValue<string>());
        Assert.AreEqual("2026-08-17T15:25:00.0000000Z",
            manifest["admission"]!["credential_expiry_hard_cap_utc"]!.GetValue<string>());
        CollectionAssert.AreEqual(OrderedOperations,
            manifest["ordered_stages"]!.AsArray().Select(stage => stage!["operation"]!.GetValue<string>()).ToArray());
    }

    [TestMethod]
    public void StandaloneValidatorAcceptsExactDraftAndRejectsEveryBoundaryMutation()
    {
        Assert.AreEqual(0, RunValidator(TestRepository.Root, null));
        Action<JsonObject>[] mutations =
        [
            root => root["unexpected"] = true,
            root => root["authority_source"]!["attachment_sha256"] = new string('0', 64),
            root => root["authority_source"]!["unexpected_nested"] = true,
            root => root["expires_at_utc"] = "2026-08-23T00:00:00.0000000Z",
            root => root["semantic_rollover"]!["zero_effect_proof"]!["credential_helper_launch_count"] = 1,
            root => root["semantic_rollover"]!["zero_effect_proof"]!["credential_helper_readiness_count"] = 1,
            root => root["semantic_rollover"]!["zero_effect_proof"]!["credential_authority_lock_count"] = 1,
            root => root["semantic_rollover"]!["zero_effect_proof"]!["credential_manager_call_count"] = 1,
            root => root["semantic_rollover"]!["zero_effect_proof"]!["provider_dispatch_count"] = 1,
            root => root["credential_envelope"]!["profile_id"] = "openai-platform-other",
            root => root["safety_identifier"]!["raw_seed_transmitted"] = true,
            root => root["safety_identifier"]!["domain"] = "unframed",
            root => root["safety_identifier"]!["use_latch_schema"] = "unversioned",
            root => root["admission"]!["campaign_admission_marker"] = "substring-marker",
            root => root["stage_authority_contract"]!["stage_evidence_schema_path"] = "unknown.json",
            root => root["rehearsal"]!["required_stop_mutations"]!.AsArray().Add(
                root["rehearsal"]!["required_stop_mutations"]![0]!.DeepClone()),
            root =>
            {
                JsonArray stages = root["ordered_stages"]!.AsArray();
                JsonNode? first = stages[0]!.DeepClone();
                stages[0] = stages[1]!.DeepClone();
                stages[1] = first;
            },
            root => root["ordered_stages"]![0]!["maximum_provider_calls"] = 2,
            root => root["aggregate_limits"]!["maximum_provider_calls"] = 4,
            root => root["aggregate_limits"]!["maximum_nano_usd"] = 1_340_000_001,
            root => root["aggregate_limits"]!["maximum_dns_resolutions"] = 4,
            root => root["aggregate_limits"]!["automatic_retry"] = true,
            root => root["aggregate_limits"]!["parallel_calls"] = true,
            root => root["aggregate_limits"]!["fourth_call"] = "permitted",
            root => root["execution"]!["provider_request_permitted"] = true,
            root => root["ordered_stages"]![0]!["request_manifest"] = "materialized-early",
        ];
        string temporary = Path.Combine(Path.GetTempPath(), "infinium-campaign-contract-" + Guid.NewGuid().ToString("N"));
        try
        {
            CloneWithCurrentCampaignValidator(temporary);
            foreach (Action<JsonObject> mutation in mutations)
            {
                Assert.AreNotEqual(0, RunValidator(temporary, mutation), mutation.Method.Name);
            }
        }
        finally
        {
            if (Directory.Exists(temporary))
            {
                foreach (string path in Directory.EnumerateFileSystemEntries(temporary, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }
                File.SetAttributes(temporary, FileAttributes.Normal);
                Directory.Delete(temporary, recursive: true);
            }
        }
    }

    [TestMethod]
    public void NonLiveAllCampaignStateRecognizesOnlyExactWhitespaceNormalizedRecordAuthority()
    {
        string record = File.ReadAllText(TestRepository.PathFromRoot(
            "docs", "plans", "milestones", "m1", "slices", "s6", "record.md"));
        string normalized = System.Text.RegularExpressions.Regex.Replace(record, @"\s+", " ");
        const string authority = "Current authority is correction and non-live reverification only.";
        StringAssert.Contains(normalized, authority);
        Assert.IsFalse(normalized.Replace(authority, "Current authority is live execution.", StringComparison.Ordinal)
            .Contains(authority, StringComparison.Ordinal));

        string verifier = File.ReadAllText(TestRepository.PathFromRoot("eng", "verify-m1-slice6.ps1"));
        string currentState = File.ReadAllText(TestRepository.PathFromRoot("docs", "current-state.md"));
        const string stateAuthority = "finite-campaign amendment implementation, non-live verification";
        StringAssert.Contains(currentState, stateAuthority);
        Assert.IsFalse(currentState.Replace(stateAuthority, "finite-campaign live execution", StringComparison.Ordinal)
            .Contains(stateAuthority, StringComparison.Ordinal));
        StringAssert.Contains(verifier, "$normalizedRecordText = [regex]::Replace($recordText, '\\s+', ' ')");
        StringAssert.Contains(verifier,
            "$normalizedRecordText.Contains('Current authority is correction and non-live reverification only.'");
        StringAssert.Contains(verifier, "'tests/Infinium.UnitTests/Wp9ProductionProfileAuthorizationTests.cs'");
        StringAssert.Contains(verifier, "'eng/validate-m1-slice6-wp8-prelive.ps1'");
        StringAssert.Contains(verifier, "'tests/Infinium.ContractTests/Wp8PreLiveReadinessContractTests.cs'");
        Assert.IsTrue(
            verifier.IndexOf("$failures = [System.Collections.Generic.List[string]]::new()", StringComparison.Ordinal)
            < verifier.IndexOf("[string[]]$actualOwnerCloseoutPaths", StringComparison.Ordinal),
            "Layer6 must initialize its finding list before any mode-specific path-set finding can be retained.");
    }

    private static int RunValidator(string root, Action<JsonObject>? mutation)
    {
        string manifest = Path.Combine(root, ManifestRelative.Replace('/', Path.DirectorySeparatorChar));
        string exact = File.ReadAllText(manifest);
        try
        {
            if (mutation is not null)
            {
                JsonObject node = JsonNode.Parse(exact)!.AsObject();
                mutation(node);
                File.WriteAllText(manifest, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell",
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                ArgumentList =
                {
                    "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "eng/validate-m1-slice6-campaign.ps1",
                    "-AuthorizationManifest", ManifestRelative, "-RequireState", "Verification",
                },
            }) ?? throw new InvalidOperationException("Campaign validator did not start.");
            process.WaitForExit(30_000);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException("Campaign validator exceeded its non-live bound.");
            }
            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            if (mutation is null && process.ExitCode != 0)
            {
                throw new InvalidOperationException("Exact campaign validator failed: " + stdout + stderr);
            }
            return process.ExitCode;
        }
        finally
        {
            if (mutation is not null) { File.WriteAllText(manifest, exact); }
        }
    }

    private static void CloneWithCurrentCampaignValidator(string destination)
    {
        RunProcess(TestRepository.Root, "git", "-c", "safe.directory=" + TestRepository.Root,
            "-c", "safe.directory=" + Path.Combine(TestRepository.Root, ".git"),
            "clone", "--quiet", "--no-hardlinks", TestRepository.Root, destination);
        bool overlayChanged = false;
        foreach (string relative in new[]
        {
            "eng/validate-m1-slice6-campaign.ps1",
            "contracts/repository/m1-slice6-finite-campaign-authorization.v1.schema.json",
            "contracts/repository/m1-slice6-finite-campaign-owner-authority.v1.schema.json",
            ManifestRelative,
        })
        {
            string target = Path.Combine(destination, relative.Replace('/', Path.DirectorySeparatorChar));
            string source = TestRepository.PathFromRoot(relative.Split('/'));
            overlayChanged |= !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(target));
            File.Copy(source, target, overwrite: true);
        }
        if (overlayChanged)
        {
            RunProcess(destination, "git", "add", "--", "eng/validate-m1-slice6-campaign.ps1",
                "contracts/repository/m1-slice6-finite-campaign-authorization.v1.schema.json",
                "contracts/repository/m1-slice6-finite-campaign-owner-authority.v1.schema.json", ManifestRelative);
            RunProcess(destination, "git", "-c", "user.name=Infinium Contract", "-c", "user.email=contract@invalid",
                "commit", "--quiet", "-m", "contract validator overlay");
        }
    }

    private static void RunProcess(string root, string file, params string[] arguments)
    {
        ProcessStartInfo start = new(file)
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in arguments) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit(60_000);
        if (process.ExitCode != 0) { throw new InvalidOperationException(file + " failed: " + output + error); }
    }

    private static JsonObject ReadManifest() => JsonNode.Parse(File.ReadAllText(
        TestRepository.PathFromRoot(ManifestRelative.Split('/'))))!.AsObject();

    private static void AssertRecursivelyClosed(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out JsonElement type) && type.ValueKind == JsonValueKind.String
                && type.GetString() == "object")
            {
                Assert.IsTrue(element.TryGetProperty("additionalProperties", out JsonElement additional)
                    && additional.ValueKind == JsonValueKind.False, path);
            }
            foreach (JsonProperty property in element.EnumerateObject())
            {
                AssertRecursivelyClosed(property.Value, path + "." + property.Name);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                AssertRecursivelyClosed(item, path + "[" + index++ + "]");
            }
        }
    }
}
