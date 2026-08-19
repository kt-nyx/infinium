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
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.IsExpectedActionFocus(
            "submit-button", false, true, false, true));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.IsExpectedActionFocus(
            "cancel-button", false, false, true, true));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.IsExpectedActionFocus(
            "edit-enter", true, false, false, true));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.IsExpectedActionFocus(
            "edit-escape", true, false, false, true));
        Assert.IsTrue(Wp9ProductionEntryReadinessOracle.IsExpectedActionFocus(
            "window-close", false, false, false, true));
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.IsExpectedActionFocus(
            "submit-button", true, false, false, true));
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.IsExpectedActionFocus(
            "cancel-button", true, false, false, true));
        Assert.IsFalse(Wp9ProductionEntryReadinessOracle.IsExpectedActionFocus(
            "window-close", false, false, false, false));
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
    public void CompiledHelperAdmitsFreshV4AndRejectsRetiredV2AndV3BeforeUiOrNativeCalls()
    {
        string root = RepositoryRoot();
        string slice = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6");
        string temporary = Path.Combine(Path.GetTempPath(),
            "infinium-c1-2-helper-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            JsonObject fresh = JsonNode.Parse(File.ReadAllText(Path.Combine(
                slice, "wp9-production-profile-authorization.v3.json")))!.AsObject();
            const string manifestId =
                "infinium.m1-s6.wp9.production-profile-authorization/aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
            const string profileId = "openai-platform-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            const string generationId = "g-cccccccccccc4ccc8ccccccccccccccc";
            string target = $"Infinium:{profileId}:{generationId}";
            string fingerprint = Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(target)));
            fresh["schema_identity"] = "infinium.repository.wp9-production-profile-authorization/4.0.0";
            fresh["manifest_id"] = manifestId;
            fresh["prepared_at_utc"] = "2026-08-19T18:00:00.0000000Z";
            fresh["expires_at_utc"] = "2026-09-15T23:00:00.0000000Z";
            fresh["profile"]!["access_profile_id"] = profileId;
            fresh["profile"]!["generation_id"] = generationId;
            fresh["profile"]!["target_fingerprint_sha256"] = fingerprint;
            byte[] freshBytes = JsonSerializer.SerializeToUtf8Bytes(fresh);
            string freshPath = Path.Combine(temporary, "fresh-v4.json");
            File.WriteAllBytes(freshPath, freshBytes);
            string freshSha = Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(freshBytes));

            using (WindowsCredentialManagerStore store =
                WindowsCredentialManagerStore.FromProductionEnrollmentManifest(
                    freshPath, freshSha, manifestId))
            {
                Assert.IsTrue(store.IsProductionEnrollment);
                Assert.AreEqual(0, store.CallTrace.Count,
                    "Manifest admission must not perform a native credential operation.");
            }

            JsonObject retiredV2 = fresh.DeepClone().AsObject();
            retiredV2["schema_identity"] = "infinium.repository.wp9-production-profile-authorization/2.0.0";
            retiredV2["manifest_id"] =
                "infinium.m1-s6.wp9.production-profile-authorization/09b8e309-ead8-441e-8307-5a4a1a2c43d5";
            string retiredV2Path = Path.Combine(temporary, "retired-v2.json");
            File.WriteAllText(retiredV2Path, retiredV2.ToJsonString());
            foreach (string retiredPath in new[]
            {
                retiredV2Path,
                Path.Combine(slice, "wp9-production-profile-authorization.v3.json"),
            })
            {
                byte[] retiredBytes = File.ReadAllBytes(retiredPath);
                using JsonDocument retired = JsonDocument.Parse(retiredBytes);
                string retiredId = retired.RootElement.GetProperty("manifest_id").GetString()!;
                string retiredSha = Convert.ToHexStringLower(
                    System.Security.Cryptography.SHA256.HashData(retiredBytes));
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    WindowsCredentialManagerStore.FromProductionEnrollmentManifest(
                        retiredPath, retiredSha, retiredId), Path.GetFileName(retiredPath));
            }

            (string Name, string Property, string Value)[] retiredIdentityCases =
            [
                ("v2-manifest", "manifest_id",
                    "infinium.m1-s6.wp9.production-profile-authorization/09b8e309-ead8-441e-8307-5a4a1a2c43d5"),
                ("v3-manifest", "manifest_id",
                    "infinium.m1-s6.wp9.production-profile-authorization/52b2cfdb-ccd4-49c0-8f6a-ace8c426012e"),
                ("v2-profile", "access_profile_id", "openai-platform-c2f213dbc4d9461c9fa8485050ab324d"),
                ("v3-profile", "access_profile_id", "openai-platform-ecd3de4b9fac443593347905970d942d"),
                ("v2-generation", "generation_id", "g-cb0c3748ef2b4745b97a9311c89f2b65"),
                ("v3-generation", "generation_id", "g-6eefeaf6e4a74273bf4ee69f02449f47"),
                ("v2-fingerprint", "target_fingerprint_sha256",
                    "7c4683448a864da4b7cb96a07cf13db93cff9b1a1eb22ed013250a2975a9c071"),
                ("v3-fingerprint", "target_fingerprint_sha256",
                    "990e46a57687417a1a1865bab3b11823f3b37d35961fb8101e32a8977e2a4b67"),
            ];
            foreach ((string _, string property, string value) in retiredIdentityCases)
            {
                Assert.IsTrue(WindowsCredentialManagerStore.IsRetiredProductionIdentity(
                    property == "manifest_id" ? value : manifestId,
                    property == "access_profile_id" ? value : profileId,
                    property == "generation_id" ? value : generationId,
                    property == "target_fingerprint_sha256" ? value : fingerprint), property);
            }
            foreach ((string name, string property, string value) in retiredIdentityCases)
            {
                JsonObject smuggled = fresh.DeepClone().AsObject();
                if (property == "manifest_id")
                {
                    smuggled[property] = value;
                }
                else
                {
                    smuggled["profile"]![property] = value;
                }
                string smuggledPath = Path.Combine(temporary, name + ".json");
                byte[] smuggledBytes = JsonSerializer.SerializeToUtf8Bytes(smuggled);
                File.WriteAllBytes(smuggledPath, smuggledBytes);
                string smuggledSha = Convert.ToHexStringLower(
                    System.Security.Cryptography.SHA256.HashData(smuggledBytes));
                string smuggledManifestId = smuggled["manifest_id"]!.GetValue<string>();
                Assert.ThrowsExactly<InvalidDataException>(() =>
                    WindowsCredentialManagerStore.FromProductionEnrollmentManifest(
                        smuggledPath, smuggledSha, smuggledManifestId), name);
            }

            string program = File.ReadAllText(Path.Combine(root, "src", "Infinium.CredentialHelper", "Program.cs"));
            int validation = program.IndexOf("FromProductionEnrollmentManifest(", StringComparison.Ordinal);
            int containment = program.IndexOf("productionDescendant = Process.Start", validation, StringComparison.Ordinal);
            int uiSource = program.IndexOf("productionSecretSource = new();", validation, StringComparison.Ordinal);
            Assert.IsTrue(validation >= 0 && containment > validation && uiSource > containment,
                "The compiled helper must reject a manifest before containment, UI, or native-store execution.");
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [TestMethod]
    public void HiddenNativeMessagePumpExercisesActionDrainDesktopOwnershipBufferAndDestroyCleanup()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("Windows message-pump evidence is required."); }
        Wp9ProductionHiddenPumpProbe probe = Wp9ProductionMaskedEntryDialog.RunNonLiveHiddenPumpProbe();
        Assert.IsTrue(probe.PreReadySubmitRejected);
        Assert.IsTrue(probe.ReadySubmitAdmitted);
        Assert.IsTrue(probe.ReadyCancelAdmitted);
        Assert.IsTrue(probe.SubmitButtonClickAdmitted);
        Assert.IsTrue(probe.CancelButtonClickAdmitted);
        Assert.IsTrue(probe.PreReadyContentCleared);
        Assert.IsTrue(probe.NativeBufferEmpty);
        Assert.IsTrue(probe.HelperProcessOwned);
        Assert.IsTrue(probe.InputDesktopMatched);
        Assert.IsTrue(probe.WindowDestroyed);
        Assert.IsTrue(probe.ThreadJoined);
    }

    [TestMethod]
    public async Task DurableLedgerExclusiveLeaseAllowsExactlyOneConcurrentWinner()
    {
        string root = RepositoryRoot();
        string ledger = File.ReadAllText(Path.Combine(root, "src", "Infinium.Persistence",
            "M1Slice6FiniteCampaignLedger.cs"));
        StringAssert.Contains(ledger, "FileMode.OpenOrCreate, FileAccess.ReadWrite");
        StringAssert.Contains(ledger, "FileShare.None");
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
    public void ReleaseRuntimeBindingIsTypedAndBinaryInventoryDetectsDependencyDrift()
    {
        string root = RepositoryRoot();
        string runner = File.ReadAllText(Path.Combine(root, "eng", "run-m1-slice6-credential.ps1"));
        string coordinator = File.ReadAllText(Path.Combine(root, "src", "Infinium.Coordinator",
            "Wp9ProductionProfileEnrollmentRunner.cs"));
        StringAssert.Contains(runner, "RuntimeAuthorityManifest");
        StringAssert.Contains(coordinator, "ValidateExecutableBinding(runtimeAuthority");
        string buildTargets = File.ReadAllText(TestRepository.PathFromRoot("Directory.Build.targets"));
        StringAssert.Contains(buildTargets, "InfiniumCanonicalSourceRevisionId");
        StringAssert.Contains(buildTargets, "^[0-9a-f]{40}$");
        StringAssert.Contains(buildTargets, "RevisionId=\"$(InfiniumCanonicalSourceRevisionId)\"");
        StringAssert.Contains(buildTargets, "AfterTargets=\"InitializeSourceControlInformationFromSourceControlManager\"");
        StringAssert.Contains(buildTargets, "Infinium builds require one exact Git SourceRevisionId");
        Assert.IsFalse(buildTargets.Contains("wp9-production-profile-authorization", StringComparison.Ordinal));
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
    public void RunnerIsAbsentFromOrdinaryVerifierAndRequiresTypedDurableAuthorityBeforeLaunch()
    {
        string root = RepositoryRoot();
        string runner = File.ReadAllText(Path.Combine(root, "eng", "run-m1-slice6-credential.ps1"));
        string verifier = File.ReadAllText(Path.Combine(root, "eng", "verify-m1-slice6.ps1"));
        StringAssert.Contains(runner, "RuntimeAuthorityManifest");
        StringAssert.Contains(runner, "RuntimeAuthoritySha256");
        StringAssert.Contains(runner, "--runtime-authority");
        StringAssert.Contains(runner, "--wp9-campaign-credential-handoff-admission");
        StringAssert.Contains(runner, "--campaign-reviewed-candidate");
        StringAssert.Contains(runner, "ValidateCampaignAdmissionOnly");
        Assert.IsFalse(runner.Contains("git ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(runner.Contains("record.md", StringComparison.Ordinal));
        Assert.IsFalse(runner.Contains("OWNER_ACCEPTANCE", StringComparison.Ordinal));
        StringAssert.Contains(runner, "--wp9-production-profile-enrollment");
        System.Text.RegularExpressions.Match nonLiveAll = System.Text.RegularExpressions.Regex.Match(
            verifier, @"(?ms)^function Invoke-NonLiveAllGate \{.*?^\}");
        Assert.IsTrue(nonLiveAll.Success);
        Assert.IsFalse(nonLiveAll.Value.Contains("run-m1-slice6-credential.ps1", StringComparison.Ordinal));
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
            $reviewedRecord="historical marker retained`nWP9_PROFILE_OWNER_ACCEPTANCE manifest_id=old-manifest sha256=$('f'*64) close_ready_commit=$('e'*40) expires_at_utc=2026-08-01T00:00:00.0000000Z`nreviewed candidate record"
            $marker=Get-Wp9ReviewAcceptanceMarker -ManifestId 'manifest' -ManifestSha256 ('a'*64) -ReviewedCandidate ('c'*40)
            $currentRecord=$reviewedRecord+"`n`n"+$marker
            if(-not (Test-Wp9ReviewedOwnerPendingRecord -ReviewedRecordText $reviewedRecord -CurrentRecordText $currentRecord -ManifestId 'manifest' -ManifestSha256 ('a'*64) -ReviewedCandidate ('c'*40))){throw 'exact review record rejected'}
            foreach($mutated in @(
              $reviewedRecord,
              ($currentRecord.Replace('security,semantics,diff','security,diff')),
              ($currentRecord.Replace(('a'*64),('d'*64))),
              ($currentRecord.Replace(('c'*40),('e'*40))),
              ($currentRecord+"`nextra"),
              ($currentRecord+"`nWP9_PROFILE_OWNER_ACCEPTANCE manifest_id=manifest sha256=$('a'*64) close_ready_commit=$('e'*40) expires_at_utc=2026-08-17T15:25:00.0000000Z"))) {
              if(Test-Wp9ReviewedOwnerPendingRecord -ReviewedRecordText $reviewedRecord -CurrentRecordText $mutated -ManifestId 'manifest' -ManifestSha256 ('a'*64) -ReviewedCandidate ('c'*40)){throw 'mutated review record admitted'}
            }
            $ownerMarker=Get-Wp9OwnerAcceptanceMarker -ManifestId 'manifest' -ManifestSha256 ('a'*64) -CloseReadyCommit ('b'*40) -ExpiresAtUtc '2026-08-17T15:25:00.0000000Z'
            $ownerRecord=$currentRecord+"`n`n"+$ownerMarker
            if(-not (Test-Wp9OwnerAcceptedRecord -ReviewedRecordText $reviewedRecord -CurrentRecordText $ownerRecord -ManifestId 'manifest' -ManifestSha256 ('a'*64) -CloseReadyCommit ('b'*40) -ExpiresAtUtc '2026-08-17T15:25:00.0000000Z' -ReviewedCandidate ('c'*40))){throw 'exact owner record rejected'}
            foreach($mutated in @(
              $currentRecord,
              ($ownerRecord.Replace(('a'*64),('d'*64))),
              ($ownerRecord.Replace(('b'*40),('e'*40))),
              ($ownerRecord.Replace('2026-08-17T15:25:00.0000000Z','2026-08-17T15:25:01.0000000Z')),
              ($ownerRecord+"`n"+$ownerMarker),
              ($ownerRecord+"`nextra"))) {
              if(Test-Wp9OwnerAcceptedRecord -ReviewedRecordText $reviewedRecord -CurrentRecordText $mutated -ManifestId 'manifest' -ManifestSha256 ('a'*64) -CloseReadyCommit ('b'*40) -ExpiresAtUtc '2026-08-17T15:25:00.0000000Z' -ReviewedCandidate ('c'*40)){throw 'mutated owner record admitted'}
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
            "-Operation", "EnrollOrVerifyProfile",
            "-OutputRoot", "artifacts/m1-slice6/wp9-profile",
        }) { start.ArgumentList.Add(argument); }
        using Process process = Process.Start(start)!;
        string outputText = process.StandardOutput.ReadToEnd();
        string errorText = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreNotEqual(0, process.ExitCode, outputText);
        StringAssert.Contains(errorText, "missing mandatory parameters");
        StringAssert.Contains(errorText, "RuntimeAuthorityManifest");
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
