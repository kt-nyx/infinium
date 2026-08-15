using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.CredentialHelper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Wp9ProductionProfileAuthorizationTests
{
    [TestMethod]
    public void ProductionLaunchTailReadinessActionsAndInputBoundsAreDeterministic()
    {
        Assert.IsTrue(Wp9ProductionLaunchContract.TryParse(
            ["--excluded-handle-probe", "123", "--spawn-containment-probe", "1"],
            out Wp9ProductionLaunchOptions? launch));
        Assert.AreEqual((nint)123, launch!.ExcludedHandle);
        Assert.IsFalse(Wp9ProductionLaunchContract.TryParse([], out _));
        Assert.IsFalse(Wp9ProductionLaunchContract.TryParse(
            ["--excluded-handle-probe", "123", "--spawn-containment-probe", "0"], out _));
        Assert.IsFalse(Wp9ProductionLaunchContract.TryParse(
            ["--spawn-containment-probe", "1", "--excluded-handle-probe", "123"], out _));
        Assert.AreEqual(new Wp9ProductionFailureClassification("launch-boundary", "containment-launch-failure"),
            Wp9ProductionFailureClassifier.ContainmentLaunch());

        Wp9ProductionReadinessSnapshot ready = new(true, true, true, true, true, true, true, true, true, true, true, true);
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.IsReady(ready));
        foreach (Wp9ProductionReadinessSnapshot mutation in new[]
        {
            ready with { WindowVisible = false }, ready with { EditVisible = false },
            ready with { InitiallyBlank = false }, ready with { HelperProcessOwned = false },
            ready with { SameSession = false }, ready with { InputDesktopAvailable = false },
            ready with { NotCloaked = false }, ready with { OnMonitor = false },
            ready with { Enabled = false }, ready with { Focused = false },
            ready with { Foreground = false }, ready with { Active = false },
        }) { Assert.IsFalse(Wp9ProductionEntryReadinessOracle.IsReady(mutation)); }
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.AdmitAction(false, "submit"));
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.AdmitAction(false, "cancel"));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.AdmitAction(true, "submit"));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.AdmitAction(true, "cancel"));
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.AdmitAction(true, "paste"));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.AdmitAction(
            ready, "submit", "submit-button", 32, 2560));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.AdmitAction(
            ready, "cancel", "edit-escape", 0, 2560));
        foreach (Wp9ProductionReadinessSnapshot mutation in new[]
        {
            ready with { HelperProcessOwned = false }, ready with { SameSession = false },
            ready with { InputDesktopAvailable = false }, ready with { NotCloaked = false },
            ready with { OnMonitor = false }, ready with { Enabled = false },
            ready with { Focused = false }, ready with { Foreground = false },
            ready with { Active = false },
        })
        {
            Assert.IsFalse(Wp9ProductionEntryReadinessOracle.AdmitAction(
                mutation, "submit", "submit-button", 32, 2560));
        }
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.AdmitAction(
            ready, "submit", "injected-command", 32, 2560));
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.AdmitAction(
            ready with { Focused = false }, "submit", "edit-enter", 32, 2560));
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.AdmitAction(
            ready, "submit", "submit-button", 0, 2560));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.IsAdmissibleCharacterLength(2560, 2560));
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.IsAdmissibleCharacterLength(2561, 2560));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.ShouldClearPreReadinessContent(false, 1));
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.ShouldClearPreReadinessContent(true, 1));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.BufferCleanupComplete(true, true));
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.BufferCleanupComplete(true, false));
    }

    [TestMethod]
    public void ProductionCollisionClassifierIsExactAndCannotReinterpretOtherFailures()
    {
        Assert.IsTrue(Wp9ProductionCollisionClassifier.IsKnownCollision(
            new InvalidOperationException(), productionEnrollment: true, namespaceReuseBlocked: true));
        Assert.IsFalse(Wp9ProductionCollisionClassifier.IsKnownCollision(
            new IOException(), productionEnrollment: true, namespaceReuseBlocked: true));
        Assert.IsFalse(Wp9ProductionCollisionClassifier.IsKnownCollision(
            new InvalidOperationException(), productionEnrollment: false, namespaceReuseBlocked: true));
        Assert.IsFalse(Wp9ProductionCollisionClassifier.IsKnownCollision(
            new InvalidOperationException(), productionEnrollment: true, namespaceReuseBlocked: false));
    }

    [TestMethod]
    public void HiddenNativeMessagePumpExercisesActionDrainDesktopOwnershipBufferAndDestroyCleanup()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("Windows message-pump evidence is required."); }
        Wp9ProductionHiddenPumpProbe probe = Wp9ProductionMaskedEntryDialog.RunNonLiveHiddenPumpProbe();
        Assert.IsTrue(probe.PreReadySubmitRejected);
        Assert.IsTrue(probe.ReadySubmitAdmitted);
        Assert.IsTrue(probe.ReadyCancelAdmitted);
        Assert.IsTrue(probe.PreReadyContentCleared);
        Assert.IsTrue(probe.NativeBufferEmpty);
        Assert.IsTrue(probe.HelperProcessOwned);
        Assert.IsTrue(probe.InputDesktopMatched);
        Assert.IsTrue(probe.WindowDestroyed);
        Assert.IsTrue(probe.ThreadJoined);
    }

    [TestMethod]
    public async Task AuthorityLockCreateNewAllowsExactlyOneConcurrentWinner()
    {
        string root = RepositoryRoot();
        string runner = File.ReadAllText(Path.Combine(root, "eng", "run-m1-slice6-credential.ps1"));
        StringAssert.Contains(runner, "[IO.FileMode]::CreateNew");
        StringAssert.Contains(runner, "[IO.FileShare]::None");
        string directory = Path.Combine(Path.GetTempPath(), "infinium-wp9-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "authority-lock.json");
        try
        {
            Task<bool>[] attempts = Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
            {
                try
                {
                    using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    stream.WriteByte(1);
                    stream.Flush(flushToDisk: true);
                    return true;
                }
                catch (IOException) { return false; }
            })).ToArray();
            bool[] winners = await Task.WhenAll(attempts);
            Assert.AreEqual(1, winners.Count(value => value));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    public void ReleaseBinaryInventoryIsExactAndDetectsDependencyDrift()
    {
        string root = RepositoryRoot();
        string runner = File.ReadAllText(Path.Combine(root, "eng", "run-m1-slice6-credential.ps1"));
        StringAssert.Contains(runner, "Get-Wp9BinaryInventory");
        StringAssert.Contains(runner, "binary_inventory_file_count");
        StringAssert.Contains(runner, "binary_inventory_sha256");
        StringAssert.Contains(runner, "SourceRevisionId=$closeReady");
        string manifest = File.ReadAllText(Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v1.json"));
        StringAssert.Contains(manifest, "bin/Release/net10.0/Infinium.Coordinator.exe");
        string directory = Path.Combine(Path.GetTempPath(), "infinium-wp9-binaries-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "CredentialHelper"));
        try
        {
            File.WriteAllBytes(Path.Combine(directory, "Infinium.Coordinator.exe"), [1, 2, 3]);
            string dependency = Path.Combine(directory, "CredentialHelper", "dependency.dll");
            File.WriteAllBytes(dependency, [4, 5, 6]);
            string runtimeConfig = Path.Combine(directory, "Infinium.Coordinator.runtimeconfig.json");
            File.WriteAllText(runtimeConfig, "{\"runtimeOptions\":{}}");
            (int count, string hash) = BinaryInventory(directory);
            Assert.AreEqual(3, count);
            File.WriteAllBytes(dependency, [4, 5, 7]);
            (int changedCount, string changedHash) = BinaryInventory(directory);
            Assert.AreEqual(count, changedCount);
            Assert.AreNotEqual(hash, changedHash);
            File.WriteAllText(Path.Combine(directory, "ignored.pdb"), "not a binary authority input");
            Assert.AreEqual(changedHash, BinaryInventory(directory).Hash);
            File.WriteAllText(runtimeConfig, "{\"runtimeOptions\":{\"tfm\":\"changed\"}}");
            Assert.AreNotEqual(changedHash, BinaryInventory(directory).Hash);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
    [TestMethod]
    public void ExactPreparedManifestValidatesAndRecursiveSchemaIsClosed()
    {
        string root = RepositoryRoot();
        string manifest = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v1.json");
        ProcessResult result = RunValidator(root, manifest, mutation: false);
        Assert.AreEqual(0, result.ExitCode, result.Error);
        Assert.IsTrue(
            result.Output.Contains("validated-draft-binding-pending", StringComparison.Ordinal)
            || result.Output.Contains("validated-ready-for-owner-acceptance", StringComparison.Ordinal),
            result.Output);

        using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            root, "contracts", "repository", "wp9-production-profile-authorization.v1.schema.json")));
        AssertClosedObjects(schema.RootElement, schema.RootElement);
    }

    [TestMethod]
    public void ValidatorRejectsIdentityNativeUxLifecycleOfficialDocAndAuthorityMutations()
    {
        string root = RepositoryRoot();
        string source = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v1.json");
        JsonObject baseline = JsonNode.Parse(File.ReadAllText(source))!.AsObject();
        List<Action<JsonObject>> mutations =
        [
            node => node["extra"] = true,
            node => node["manifest_id"] = "mutated",
            node => node["profile"]!["mode"] = "existing",
            node => node["profile"]!["target_derivation"] = "literal-retained-target",
            node => node["profile"]!["target_fingerprint_sha256"] = new string('0', 64),
            node => node["native_boundary"]!["exact_call_order"] = new JsonArray("CredWriteW", "CredReadW", "CredFree"),
            node => node["native_boundary"]!["exact_collision_order"] = new JsonArray("CredReadW"),
            node => node["native_boundary"]!["maximum_calls"]!["CredDeleteW"] = 1,
            node => node["m1_entry_surface"]!["paste_permitted"] = false,
            node => node["m1_entry_surface"]!["renderer_receives_or_retains_secret"] = true,
            node => node["m1_entry_surface"]!["readiness_requirements"]![5] = "nearest-monitor-is-enough",
            node => node["m1_entry_surface"]!["cleanup_requirements"]![1] = "claim-native-edit-empty",
            node => node["future_product_ux"]!["implemented_by_wp9"] = true,
            node => node["durable_state"]!["success_state"] = "active-unverified",
            node => node["durable_state"]!["active_unverified_request_gate"] = "admit",
            node => node["provider_intent"]!["provider_request_permitted"] = true,
            node => node["official_document_refresh"]!["documents"]![0]!["sha256"] = new string('0', 64),
            node => node["official_document_refresh"]!["drift_follow_up"]!["provider_request_packet_blocked"] = false,
            node => node["owner_authorization"]!["inheritance"] = "allowed",
            node => node["owner_authorization"]!["independent_review_record"] = "missing",
            node => node["release_build"]!["source_commit"] = new string('f', 40),
            node => node["release_build"]!["build_command"] = "dotnet build Infinium.sln -c Release --no-restore --nologo",
            node => node["release_build"]!["build_command"] = "dotnet build Infinium.sln -c Release --no-restore --nologo --no-incremental -p:SourceRevisionId=" + new string('f', 40),
            node => node["release_build"]!["binary_inventory_file_count"] = 501,
            node => MakePartiallyReady(node, "source_commit"),
            node => MakePartiallyReady(node, "coordinator_sha256"),
            node => MakePartiallyReady(node, "helper_sha256"),
            node => MakePartiallyReady(node, "binary_inventory_sha256"),
            node => MakePartiallyReady(node, "binary_inventory_file_count"),
            node => node["official_document_refresh"]!["documents"]![1]!["etag"] = null,
            node => node["execution"]!["qualification_request_manifest"] = "ready",
        ];
        string temporary = Path.Combine(Path.GetTempPath(), "infinium-wp9-manifest-mutations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            for (int index = 0; index < mutations.Count; index++)
            {
                JsonObject candidate = baseline.DeepClone().AsObject();
                mutations[index](candidate);
                string path = Path.Combine(temporary, $"mutation-{index:D2}.json");
                File.WriteAllText(path, candidate.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                ProcessResult result = RunValidator(root, path, mutation: true);
                Assert.AreNotEqual(0, result.ExitCode, $"Mutation {index} was admitted: {result.Output}");
            }
        }
        finally { Directory.Delete(temporary, recursive: true); }
    }

    [TestMethod]
    public void RunnerIsAbsentFromOrdinaryVerifierAndRequiresExactOwnerMarkerBeforeLaunch()
    {
        string root = RepositoryRoot();
        string runner = File.ReadAllText(Path.Combine(root, "eng", "run-m1-slice6-credential.ps1"));
        string verifier = File.ReadAllText(Path.Combine(root, "eng", "verify-m1-slice6.ps1"));
        StringAssert.Contains(runner, "WP9_PROFILE_OWNER_ACCEPTANCE");
        StringAssert.Contains(runner, "status --porcelain=v1");
        StringAssert.Contains(runner, "merge-base --is-ancestor");
        StringAssert.Contains(runner, "output must be the exact fresh absent manifest-bound root");
        StringAssert.Contains(runner, "new-only WP9 production profile state root already exists");
        StringAssert.Contains(runner, "authority-lock.json");
        StringAssert.Contains(runner, "--wp9-production-profile-enrollment");
        Assert.IsFalse(verifier.Contains("run-m1-slice6-credential.ps1", StringComparison.Ordinal));
        Assert.IsFalse(verifier.Contains("--wp9-production-profile-enrollment", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ReviewedAndOwnerAcceptedDocumentationTransitionsRejectEveryMissingFact()
    {
        string root = RepositoryRoot();
        string contract = Path.Combine(root, "eng", "wp9-owner-documentation-contract.ps1");
        string harness = Path.Combine(Path.GetTempPath(), "infinium-wp9-doc-contract-" + Guid.NewGuid().ToString("N") + ".ps1");
        string escapedContract = contract.Replace("'", "''", StringComparison.Ordinal);
        File.WriteAllText(harness, $$"""
            $ErrorActionPreference='Stop'
            . '{{escapedContract}}'
            $sets=@(
              (Get-Wp9ReviewedOwnerPendingDocumentationRequirements -ManifestId 'manifest' -ManifestSha256 ('a'*64) -CloseReadyCommit ('b'*40) -ReviewedCandidate ('c'*40)),
              (Get-Wp9OwnerAcceptedDocumentationRequirements -ManifestId 'manifest' -ManifestSha256 ('a'*64) -CloseReadyCommit ('b'*40) -ReviewedCandidate ('c'*40))
            )
            foreach($set in $sets){
              $state=[string]::Join("`n",@($set.current_state))
              $readme=[string]::Join("`n",@($set.readme))
              if(-not (Test-Wp9DocumentationRequirements -CurrentStateText $state -ReadmeText $readme -Requirements $set)){throw 'exact state rejected'}
              foreach($line in @($set.current_state)){
                if(Test-Wp9DocumentationRequirements -CurrentStateText ($state.Replace($line,'')) -ReadmeText $readme -Requirements $set){throw 'missing current-state fact admitted'}
              }
              foreach($line in @($set.readme)){
                if(Test-Wp9DocumentationRequirements -CurrentStateText $state -ReadmeText ($readme.Replace($line,'')) -Requirements $set){throw 'missing README fact admitted'}
              }
            }
            "validated"
            """);
        try
        {
            ProcessStartInfo start = new("powershell.exe")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            foreach (string argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", harness })
            { start.ArgumentList.Add(argument); }
            using Process process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, error);
            StringAssert.Contains(output, "validated");
        }
        finally { File.Delete(harness); }
    }

    [TestMethod]
    public void RunnerRejectsMissingAuthorityBeforeOutputHelperOrNativeBoundary()
    {
        string root = RepositoryRoot();
        string manifest = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v1.json");
        string output = Path.Combine(root, "artifacts", "m1-slice6", "wp9-profile");
        Assert.IsFalse(Directory.Exists(output));
        ProcessStartInfo start = new("powershell.exe")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in new[]
        {
            "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            Path.Combine(root, "eng", "run-m1-slice6-credential.ps1"),
            "-Operation", "EnrollOrVerifyProfile", "-AuthorizationManifest", manifest,
            "-OutputRoot", "artifacts/m1-slice6/wp9-profile",
        }) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start)!;
        string outputText = process.StandardOutput.ReadToEnd();
        string errorText = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreNotEqual(0, process.ExitCode, outputText);
        Assert.IsTrue(
            errorText.Contains("still draft binding-pending", StringComparison.Ordinal)
            || errorText.Contains("exact clean codex/m1-s6 candidate", StringComparison.Ordinal)
            || errorText.Contains("exactly one canonical owner-acceptance line", StringComparison.Ordinal)
            || errorText.Contains("one exact independent-review acceptance for the current manifest bytes", StringComparison.Ordinal),
            errorText);
        Assert.IsFalse(Directory.Exists(output));
        Assert.IsFalse(Directory.Exists(Path.Combine(root, "artifacts", "m1-slice6", "wp9-production-profile-state")));
    }

    private static void AssertClosedObjects(JsonElement node, JsonElement root)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("type", out JsonElement type)
                && type.ValueKind == JsonValueKind.String && type.GetString() == "object")
            {
                Assert.IsTrue(node.TryGetProperty("additionalProperties", out JsonElement additional)
                    && additional.ValueKind == JsonValueKind.False,
                    "Every object schema must recursively reject unknown properties.");
            }
            foreach (JsonProperty property in node.EnumerateObject())
            {
                if (property.NameEquals("$ref"))
                {
                    string reference = property.Value.GetString()!;
                    if (reference.StartsWith("#/$defs/", StringComparison.Ordinal))
                    {
                        AssertClosedObjects(root.GetProperty("$defs").GetProperty(reference[8..]), root);
                    }
                }
                else { AssertClosedObjects(property.Value, root); }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in node.EnumerateArray()) { AssertClosedObjects(item, root); }
        }
    }

    private static ProcessResult RunValidator(string root, string manifest, bool mutation)
    {
        ProcessStartInfo start = new("pwsh.exe")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(Path.Combine(root, "eng", "validate-m1-slice6-wp9-profile-authorization.ps1"));
        start.ArgumentList.Add("-AuthorizationManifest");
        start.ArgumentList.Add(manifest);
        if (mutation)
        {
            start.ArgumentList.Add("-MutationTest");
            start.Environment["INFINIUM_WP9_VALIDATOR_MUTATION_TEST"] = "1";
        }
        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new(process.ExitCode, output, error);
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

    private static (int Count, string Hash) BinaryInventory(string root)
    {
        string[] files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".exe" or ".dll"
                || path.EndsWith(".deps.json", StringComparison.Ordinal)
                || path.EndsWith(".runtimeconfig.json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        string canonical = string.Join('\n', files.Select(path =>
            $"{Path.GetRelativePath(root, path).Replace('\\', '/')}|"
            + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))))) + "\n";
        return (files.Length, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(canonical))));
    }

    private static void MakePartiallyReady(JsonObject node, string pendingField)
    {
        node["status"] = "ready-for-owner-acceptance";
        node["candidate_binding"]!["close_ready_implementation_commit"] = new string('a', 40);
        JsonNode release = node["release_build"]!;
        release["source_commit"] = new string('a', 40);
        release["coordinator_sha256"] = new string('b', 64);
        release["helper_sha256"] = new string('c', 64);
        release["binary_inventory_sha256"] = new string('d', 64);
        release["binary_inventory_file_count"] = 5;
        if (pendingField == "source_commit") { release[pendingField] = new string('0', 40); }
        else if (pendingField == "binary_inventory_file_count") { release[pendingField] = 0; }
        else { release[pendingField] = new string('0', 64); }
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
