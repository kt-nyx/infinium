using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class M1Slice6CampaignContractTests
{
    private const string R1AcceptedEnforcementAnchor = "6a1f0774fdfc3b4efa2e44f88d3df67e48393ffe";
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
        Assert.AreNotEqual(0, RunValidator(TestRepository.Root, null, requireSuccess: false),
            "The active correction worktree must not remain executable as the rejected B7 candidate.");
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
            Assert.AreEqual(0, RunValidator(temporary, null));
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
    public void CampaignStateRetainsHistoricalRemainderAuthorityAndExactActiveHandoff()
    {
        string record = File.ReadAllText(TestRepository.PathFromRoot(
            "docs", "plans", "milestones", "m1", "slices", "s6", "record.md"));
        string normalized = System.Text.RegularExpressions.Regex.Replace(record, @"\s+", " ");
        const string authority = "M1_S6_REMAINDER_OWNER_ACCEPTANCE candidate_commit=5cb20ad8697901fc5dcbaccdf70d8eaa89ae8e98";
        StringAssert.Contains(normalized, authority);
        Assert.IsFalse(normalized.Replace(authority, "M1_S6_REMAINDER_OWNER_ACCEPTANCE candidate_commit=0000000000000000000000000000000000000000", StringComparison.Ordinal)
            .Contains(authority, StringComparison.Ordinal));

        string verifier = File.ReadAllText(TestRepository.PathFromRoot("eng", "verify-m1-slice6.ps1"));
        string currentState = File.ReadAllText(TestRepository.PathFromRoot("docs", "current-state.md"));
        const string stateAuthority = "C1 effect-free readiness closure is accepted";
        StringAssert.Contains(currentState, stateAuthority);
        StringAssert.Contains(currentState,
            "The accepted v4 campaign crossed one WP9 possible-start latch");
        StringAssert.Contains(currentState,
            "total committed exposure is USD 1.62548");
        StringAssert.Contains(currentState,
            "WP9 ordinal 11, WP10 ordinal 2, and WP11 ordinal 1 are the permanent first-structurally-valid results.");
        StringAssert.Contains(currentState,
            "Private/evaluator material, archives, credentials, providers, DNS/network, billable or other external effects, semantic-oracle work, merge, and push remain unauthorized.");
        Assert.IsFalse(currentState.Replace(stateAuthority, "C2 live execution is authorized", StringComparison.Ordinal)
            .Contains(stateAuthority, StringComparison.Ordinal));
        StringAssert.Contains(verifier, "function Test-M1Slice6RemainderR1NoEffectState");
        StringAssert.Contains(verifier, "M1_S6_REMAINDER_OWNER_ACCEPTANCE candidate_commit=5cb20ad8697901fc5dcbaccdf70d8eaa89ae8e98");
        StringAssert.Contains(verifier, "'tests/Infinium.UnitTests/Wp9ProductionProfileAuthorizationTests.cs'");
        StringAssert.Contains(verifier, "'eng/validate-m1-slice6-wp8-prelive.ps1'");
        StringAssert.Contains(verifier, "'tests/Infinium.ContractTests/Wp8PreLiveReadinessContractTests.cs'");
        Assert.IsTrue(
            verifier.IndexOf("$failures = [System.Collections.Generic.List[string]]::new()", StringComparison.Ordinal)
            < verifier.IndexOf("[string[]]$actualOwnerCloseoutPaths", StringComparison.Ordinal),
            "Layer6 must initialize its finding list before any mode-specific path-set finding can be retained.");
    }

    [TestMethod]
    public void RemainderR1AuthorityReadersRejectNearMutationsAndDoNotBroadenPaths()
    {
        foreach (string relative in new[] { "eng/verify-m1-slice6.ps1", "eng/validate-m1-slice6-wp8-prelive.ps1" })
        {
            string script = File.ReadAllText(TestRepository.PathFromRoot([.. relative.Split('/')]));
            System.Text.RegularExpressions.Match stateFunction = System.Text.RegularExpressions.Regex.Match(script,
                @"(?ms)^function Test-M1Slice6RemainderR1NoEffectState\(.*?^\}");
            Assert.IsTrue(stateFunction.Success, relative);
            StringAssert.Contains(script, "function Test-M1Slice6RemainderR1Candidate");
            StringAssert.Contains(script, "$planningBase = '5cb20ad8697901fc5dcbaccdf70d8eaa89ae8e98'");
            StringAssert.Contains(script, "[string]::Join(\"`n\", $actual) -cne [string]::Join(\"`n\", $expected)");
            string currentBase64 = Convert.ToBase64String(ReadGitBlob(
                R1AcceptedEnforcementAnchor, "docs/current-state.md"));
            string readmeBase64 = Convert.ToBase64String(ReadGitBlob(
                R1AcceptedEnforcementAnchor, "docs/plans/milestones/m1/slices/s6/README.md"));
            string recordBase64 = Convert.ToBase64String(ReadGitBlob(
                R1AcceptedEnforcementAnchor, "docs/plans/milestones/m1/slices/s6/record.md"));
            string command = stateFunction.Value + "\n"
                + "$utf8 = [Text.UTF8Encoding]::new($false, $true)\n"
                + "$current = $utf8.GetString([Convert]::FromBase64String('" + currentBase64 + "'))\n"
                + "$readme = $utf8.GetString([Convert]::FromBase64String('" + readmeBase64 + "'))\n"
                + "$record = $utf8.GetString([Convert]::FromBase64String('" + recordBase64 + "'))\n"
                + """
                if (-not (Test-M1Slice6RemainderR1NoEffectState $current $readme $record)) { exit 10 }
                $currentWork = @($current -split '\r?\n' | Where-Object { $_.StartsWith('| Current authorized work |') })[0]
                $nextAction = @($current -split '\r?\n' | Where-Object { $_.StartsWith('| Next eligible action |') })[0]
                $effectBoundary = @($current -split '\r?\n' | Where-Object { $_.StartsWith('| Campaign effect boundary |') })[0]
                $readmeMarker = 'The project owner accepted the exact remainder planning candidate `5cb20ad8697901fc5dcbaccdf70d8eaa89ae8e98` and bound digests on 2026-08-16.'
                $mutations = @(
                    [pscustomobject]@{ current=$current.Replace('No credential or provider effect is currently admitted.', 'Credential and provider effects are admitted.'); readme=$readme; record=$record },
                    [pscustomobject]@{ current=$current.Replace('Begin `R1`:', 'Begin `R4`:'); readme=$readme; record=$record },
                    [pscustomobject]@{ current=$current; readme=$readme.Replace('5cb20ad8697901fc5dcbaccdf70d8eaa89ae8e98', '0000000000000000000000000000000000000000'); record=$record },
                    [pscustomobject]@{ current=$current; readme=$readme; record=$record.Replace('credential_effect_expires_at_utc=2026-08-31T23:00:00.0000000Z', 'credential_effect_expires_at_utc=2026-09-01T00:00:00.0000000Z') },
                    [pscustomobject]@{ current=$current + "`n" + $currentWork; readme=$readme; record=$record },
                    [pscustomobject]@{ current=$current + "`n" + $nextAction; readme=$readme; record=$record },
                    [pscustomobject]@{ current=$current + "`n" + $effectBoundary; readme=$readme; record=$record },
                    [pscustomobject]@{ current=$current; readme=$readme + "`n" + $readmeMarker; record=$record },
                    [pscustomobject]@{ current=$current + "`n| Campaign admission | admitted |"; readme=$readme; record=$record },
                    [pscustomobject]@{ current=$current; readme=$readme + "`nPROVIDER_REQUEST_EXECUTED request_count=1"; record=$record },
                    [pscustomobject]@{ current=$current; readme=$readme; record=$record + "`nprovider_request_count=1" },
                    [pscustomobject]@{ current=$current; readme=$readme; record=$record + "`nM1_S6_REMAINDER_R4_CAMPAIGN_ADMISSION status=accepted" },
                    [pscustomobject]@{ current=$current + "`n| External effect authority | admitted |"; readme=$readme; record=$record },
                    [pscustomobject]@{ current=$current + "`nCredential and provider effects are admitted."; readme=$readme; record=$record },
                    [pscustomobject]@{ current=$current; readme=$readme + "`nCampaign admission is accepted."; record=$record },
                    [pscustomobject]@{ current=$current; readme=$readme; record=$record + "`nR4 is admitted and executable." },
                    [pscustomobject]@{ current=$current; readme=$readme; record=$record + 'x' })
                $mutationIndex = 0
                foreach ($mutation in $mutations) {
                    if (Test-M1Slice6RemainderR1NoEffectState $mutation.current $mutation.readme $mutation.record) {
                        Write-Error "R1 no-effect mutation $mutationIndex was accepted"
                        exit (20 + $mutationIndex)
                    }
                    $mutationIndex++
                }
                exit 0
                """;
            Assert.AreEqual(0, RunPowerShellScript(command), relative);
        }
    }

    private static int RunValidator(string root, Action<JsonObject>? mutation, bool requireSuccess = true)
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
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            bool exited = process.WaitForExit(30_000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new TimeoutException("Campaign validator exceeded its non-live bound.");
            }
            string stdout = stdoutTask.GetAwaiter().GetResult();
            string stderr = stderrTask.GetAwaiter().GetResult();
            if (requireSuccess && mutation is null && process.ExitCode != 0)
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

    private static int RunPowerShellScript(string command)
    {
        string path = Path.Combine(Path.GetTempPath(), "infinium-r1-authority-" + Guid.NewGuid().ToString("N") + ".ps1");
        try
        {
            File.WriteAllText(path, command);
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "pwsh.exe",
                WorkingDirectory = TestRepository.Root,
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { "-NoProfile", "-File", path },
            })!;
            bool exited = process.WaitForExit(10_000);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                throw new TimeoutException("R1 authority mutation script exceeded its non-live bound.");
            }
            return process.ExitCode;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static byte[] ReadGitBlob(string commit, string relative)
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = TestRepository.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in new[] { "cat-file", "blob", commit + ":" + relative })
        {
            start.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(start)!;
        Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
        using MemoryStream bytes = new();
        Task standardOutputTask = process.StandardOutput.BaseStream.CopyToAsync(bytes);
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            try { Task.WhenAll(standardOutputTask, standardErrorTask).GetAwaiter().GetResult(); }
            catch { /* The timeout remains the authoritative failure. */ }
            throw new TimeoutException("Historical R1 Git blob read exceeded its non-live bound.");
        }
        standardOutputTask.GetAwaiter().GetResult();
        string standardError = standardErrorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException("Historical R1 Git blob read failed: " + standardError);
        }
        return bytes.ToArray();
    }

    private static void CloneWithCurrentCampaignValidator(string destination)
    {
        RunProcess(TestRepository.Root, "git", "-c", "safe.directory=" + TestRepository.Root,
            "-c", "safe.directory=" + Path.Combine(TestRepository.Root, ".git"),
            "clone", "--quiet", "--no-hardlinks", TestRepository.Root, destination);
        RunProcess(destination, "git", "checkout", "--quiet", "--detach",
            "0b72c001db0a06c35b330a980090952f62c5613e");
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
