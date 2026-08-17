using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Contract")]
public sealed class LiveSemanticV2AuthorityContractTests
{
    [TestMethod]
    public void FrozenV2AuthorityClosesAllFivePackagesSchemasAndRegistry()
    {
        LiveSemanticV2AuthorityReceipt receipt = LiveSemanticV2AuthorityVerifier.Verify(TestRepository.Root);
        Assert.AreEqual(43, receipt.PackageCount);
        Assert.AreEqual(38, receipt.PreservedRegistryEntryCount);
        Assert.AreEqual(5, receipt.NewPackageCount);
        Assert.AreEqual(23, receipt.SchemaCount);
        StringAssert.Matches(receipt.RegistrySha256, new System.Text.RegularExpressions.Regex("^[0-9a-f]{64}$"));
    }

    [TestMethod]
    public void EveryMaterializedV2DocumentValidatesAgainstItsExactRepositorySchema()
    {
        (string Document, string Schema)[] pairs =
        [
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/execution-input.v2.json", "source-claim-execution-input.v2.schema.json"),
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/context-manifest.v2.json", "source-claim-context.v2.schema.json"),
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/oracle.v2.json", "public-fixture-source-claim-oracle.v2.schema.json"),
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/oracle-provenance.v2.json", "source-claim-oracle-provenance.v2.schema.json"),
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/partition-history.v2.json", "public-fixture-partition-history.v2.schema.json"),
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/public-manifest.json", "public-fixture-source-claim.v2.schema.json"),
            ("fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/execution-input.v2.json", "candidate-investigation-execution-input.v2.schema.json"),
            ("fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/context-manifest.v2.json", "candidate-investigation-context.v2.schema.json"),
            ("fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/oracle.v2.json", "candidate-investigation-oracle.v2.schema.json"),
            ("fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/oracle-provenance.v2.json", "candidate-investigation-oracle-provenance.v2.schema.json"),
            ("fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/partition-history.v2.json", "public-fixture-partition-history.v2.schema.json"),
            ("fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/public-manifest.json", "candidate-investigation-public-manifest.v2.schema.json"),
            ("fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL-v2/oracle.v2.json", "live-source-claim-oracle.v2.schema.json"),
            ("fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL-v2/public-manifest.json", "live-source-claim-public-manifest.v2.schema.json"),
            ("fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL-v2/oracle.v2.json", "live-candidate-investigation-oracle.v2.schema.json"),
            ("fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL-v2/public-manifest.json", "live-candidate-investigation-public-manifest.v2.schema.json"),
            ("fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL-v2/oracle.v2.json", "live-composed-provenance-oracle.v2.schema.json"),
            ("fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL-v2/public-manifest.json", "live-composed-provenance-public-manifest.v2.schema.json"),
            ("fixtures/public/public-fixture-registry.v2.json", "public-fixture-registry.v2.schema.json"),
        ];
        foreach ((string document, string schema) in pairs)
        {
            using JsonDocument value = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(document.Split('/'))));
            ActiveJsonSchemaValidator.Validate(value.RootElement, schema);
        }
    }

    [TestMethod]
    public void EveryR1SchemaLoadsThroughTheActiveValidatorVocabulary()
    {
        string[] schemas =
        [
            "public-fixture-source-claim.v2.schema.json", "public-fixture-source-claim-oracle.v2.schema.json",
            "candidate-investigation-public-manifest.v2.schema.json", "candidate-investigation-oracle.v2.schema.json",
            "live-source-claim-public-manifest.v2.schema.json", "live-source-claim-oracle.v2.schema.json",
            "live-candidate-investigation-public-manifest.v2.schema.json", "live-candidate-investigation-oracle.v2.schema.json",
            "live-composed-provenance-public-manifest.v2.schema.json", "live-composed-provenance-oracle.v2.schema.json",
            "source-claim-execution-input.v2.schema.json", "source-claim-context.v2.schema.json",
            "source-claim-oracle-provenance.v2.schema.json", "candidate-investigation-execution-input.v2.schema.json",
            "candidate-investigation-context.v2.schema.json", "candidate-investigation-oracle-provenance.v2.schema.json",
            "public-fixture-partition-history.v2.schema.json", "m1-slice6-campaign-stage-request.v2.schema.json",
            "m1-slice6-campaign-stage-evidence.v2.schema.json", "m1-slice6-campaign-composed-evidence.v2.schema.json",
            "m1-slice6-finite-campaign-authorization.v2.schema.json", "wp9-production-profile-authorization.v2.schema.json",
            "public-fixture-registry.v2.schema.json",
        ];
        Assert.AreEqual(23, schemas.Distinct(StringComparer.Ordinal).Count());
        using JsonDocument empty = JsonDocument.Parse("{}");
        foreach (string schema in schemas)
        {
            InvalidDataException error = Assert.ThrowsExactly<InvalidDataException>(
                () => ActiveJsonSchemaValidator.Validate(empty.RootElement, schema), schema);
            Assert.IsFalse(error.Message.Contains("unsupported keyword", StringComparison.Ordinal), schema);
        }
    }

    [TestMethod]
    public void V2AuthorityRejectsStructuralSemanticAndIsolationMutations()
    {
        (string File, Action<JsonObject> Mutate)[] mutations =
        [
            ("fixtures/public/public-fixture-registry.v2.json", root =>
                root["packages"]!.AsArray()[42]!["package_identity"] = "LLM-CLAIM-LIVE-VAL-v2"),
            ("fixtures/public/public-fixture-registry.v2.json", root => root["package_count"] = 42),
            ("fixtures/public/public-fixture-registry.v2.json", root =>
                root["packages"]!.AsArray()[0]!["partition"] = "validation"),
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/execution-input.v2.json", root =>
                root["passages"]!.AsArray()[1]!["start_byte"] = 79),
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/execution-input.v2.json", root =>
                root["expected_answer"] = "contamination"),
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/oracle.v2.json", root =>
                root["expected_semantics"]!["state_expectations"]!.AsArray().RemoveAt(7)),
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/public-manifest.json", root =>
                root["file_identities"]!.AsArray().Add(root["file_identities"]!.AsArray()[0]!.DeepClone())),
            ("fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/public-manifest.json", root =>
                root["file_identities"]!.AsArray()[2]!["role"] = "product-input"),
            ("fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL-v2/public-manifest.json", root =>
                root["predecessor_manifest"]!["path"] = "fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/public-manifest.json"),
            ("fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/execution-input.v2.json", root =>
                root["contexts"]!.AsArray()[1]!["evidence"]!.AsArray()[0]!["relationship"] = "supporting"),
            ("fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/execution-input.v2.json", root =>
                root["contexts"]!.AsArray()[1]!["local_observations"]!.AsArray()[0]!["text_sha256"] = new string('0', 64)),
            ("fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL-v2/public-manifest.json", root =>
                root["provider_call_count"] = 1),
            ("fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL-v2/public-manifest.json", root =>
                root["stage_wrappers"]!.AsArray().Add(root["stage_wrappers"]!.AsArray()[1]!.DeepClone())),
        ];

        foreach ((string file, Action<JsonObject> mutate) in mutations)
        {
            using TemporaryAuthority copy = TemporaryAuthority.Create();
            JsonObject value = JsonNode.Parse(File.ReadAllBytes(copy.Path(file)))!.AsObject();
            mutate(value);
            File.WriteAllText(copy.Path(file), value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Assert.ThrowsExactly<InvalidDataException>(() => LiveSemanticV2AuthorityVerifier.Verify(copy.Root), file);
        }
    }

    [TestMethod]
    public void FrozenV1TreesRejectUntrackedAdditionsAndByteDriftWithoutGit()
    {
        using (TemporaryAuthority copy = TemporaryAuthority.Create())
        {
            string extra = copy.Path("fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL/untracked.json");
            File.WriteAllText(extra, "{}\n");
            Assert.ThrowsExactly<InvalidDataException>(() => LiveSemanticV2AuthorityVerifier.Verify(copy.Root));
        }
        using (TemporaryAuthority copy = TemporaryAuthority.Create())
        {
            string frozen = copy.Path("fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1/execution-input.v1.json");
            File.AppendAllText(frozen, " ");
            Assert.ThrowsExactly<InvalidDataException>(() => LiveSemanticV2AuthorityVerifier.Verify(copy.Root));
        }
    }

    [TestMethod]
    public void DedicatedV2ResealerRoundTripsExactBytesAndNeverTouchesV1()
    {
        using TemporaryAuthority copy = TemporaryAuthority.Create();
        string[] writes =
        [
            "fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/public-manifest.json",
            "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2/public-manifest.json",
            "fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL-v2/public-manifest.json",
            "fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL-v2/public-manifest.json",
            "fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL-v2/public-manifest.json",
            "fixtures/public/public-fixture-registry.v2.json",
        ];
        Dictionary<string, byte[]> exact = writes.ToDictionary(x => x, x => File.ReadAllBytes(copy.Path(x)), StringComparer.Ordinal);
        byte[] v1 = File.ReadAllBytes(copy.Path("fixtures/public/public-fixture-registry.v1.json"));

        JsonObject manifest = JsonNode.Parse(File.ReadAllBytes(copy.Path(writes[0])))!.AsObject();
        manifest["file_identities"]!.AsArray()[0]!["sha256"] = new string('0', 64);
        File.WriteAllText(copy.Path(writes[0]), manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Assert.AreNotEqual(0, RunResealer(copy.Root, "--check"));
        Assert.AreEqual(0, RunResealer(copy.Root, "--write"));
        Assert.AreEqual(0, RunResealer(copy.Root, "--check"));
        foreach (string relative in writes)
        {
            CollectionAssert.AreEqual(exact[relative], File.ReadAllBytes(copy.Path(relative)), relative);
        }
        CollectionAssert.AreEqual(v1, File.ReadAllBytes(copy.Path("fixtures/public/public-fixture-registry.v1.json")));
    }

    [TestMethod]
    public void ResealingCannotLegitimizeAChangedSemanticOracle()
    {
        using TemporaryAuthority copy = TemporaryAuthority.Create();
        string relative = "fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2/oracle.v2.json";
        JsonObject oracle = JsonNode.Parse(File.ReadAllBytes(copy.Path(relative)))!.AsObject();
        oracle["expected_semantics"]!["state_expectations"]!.AsArray()[0]!["host_admission"] = "forbidden";
        File.WriteAllText(copy.Path(relative), oracle.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        Assert.AreEqual(0, RunResealer(copy.Root, "--write"));
        Assert.ThrowsExactly<InvalidDataException>(() => LiveSemanticV2AuthorityVerifier.Verify(copy.Root));
    }

    [TestMethod]
    public void SchemaAuthorityRejectsWrongIdIdentityAndNestedUnknowns()
    {
        foreach ((string field, string replacement) in new[]
        {
            ("$id", "https://schemas.infinium.dev/repository/wrong.schema.json"),
            ("identity", "infinium.evaluation.wrong/2.0.0"),
        })
        {
            using TemporaryAuthority copy = TemporaryAuthority.Create();
            string relative = "contracts/repository/live-source-claim-oracle.v2.schema.json";
            JsonObject schema = JsonNode.Parse(File.ReadAllBytes(copy.Path(relative)))!.AsObject();
            if (field == "$id")
            {
                schema["$id"] = replacement;
            }
            else
            {
                schema["properties"]!["schema_id"]!["const"] = replacement;
            }
            File.WriteAllText(copy.Path(relative), schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
            Assert.ThrowsExactly<InvalidDataException>(() => LiveSemanticV2AuthorityVerifier.Verify(copy.Root));
        }

        using JsonDocument schemaDocument = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
            "contracts", "repository", "live-composed-provenance-oracle.v2.schema.json")));
        JsonObject composed = JsonNode.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
            "fixtures", "public", "provider", "live-campaign", "PROV-LIVE-COMPOSED-VAL-v2", "oracle.v2.json")))!.AsObject();
        composed["required_semantic_chain"]!["wp10"]!["unexpected_nested"] = true;
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateFragment(composed, schemaDocument.RootElement, "nested-unknown"));
    }

    [TestMethod]
    public void SuccessorSchemasRejectNestedUnknownsWrongIdentitiesAndEvidenceRootSwaps()
    {
        using JsonDocument stageSchema = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
            "contracts", "repository", "m1-slice6-campaign-stage-request.v2.schema.json")));
        JsonElement definitions = stageSchema.RootElement.GetProperty("$defs");
        JsonObject persisted = JsonNode.Parse("""
            {"root_kind":"persisted-source-claim-application","context_id":"relay-gate-context-a","candidate_id":"relay-gate-candidate-a","acquisition_run_id":"wp10-acquisition-live-val-v2","proposal_id":"wp10-proposal-relay-activation","source_admission_id":"wp10-source-admission-relay-activation","admitted_artifact_id":"wp10-artifact-relay-activation","application_link_id":"wp10-application-link-relay-activation","source_revision_id":"relay-guidance-revision-4","passage_id":"relay-activation-rule","persisted_payload_sha256":"09b6e7649ed3e8ce0abe09911bf635144876b804901a61a7eefc0bf081ece236","parallel_claim_permitted":false}
            """)!.AsObject();
        JsonObject frozen = JsonNode.Parse("""
            {"root_kind":"frozen-host-evidence","context_id":"relay-gate-context-b","candidate_id":"relay-gate-candidate-b","evidence_root_id":"wp11-host-evidence-root-relay-observation","applicability_record_id":"wp11-applicability-relay-observation","source_revision_id":"relay-guidance-revision-4","passage_id":"relay-observation-note","content_sha256":"07026c83402e715675d6d9884c15f4dd57f04dd1fdca13b216795ebd7124090b","parallel_claim_permitted":false}
            """)!.AsObject();
        ValidateFragment(persisted, definitions.GetProperty("persistedSourceClaimApplication"), "persisted-root");
        ValidateFragment(frozen, definitions.GetProperty("frozenHostEvidence"), "frozen-root");
        JsonObject unknown = persisted.DeepClone().AsObject(); unknown["unexpected"] = true;
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateFragment(unknown, definitions.GetProperty("persistedSourceClaimApplication"), "unknown"));
        JsonObject wrong = persisted.DeepClone().AsObject(); wrong["proposal_id"] = "orphan-proposal";
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateFragment(wrong, definitions.GetProperty("persistedSourceClaimApplication"), "wrong-id"));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateFragment(frozen, definitions.GetProperty("persistedSourceClaimApplication"), "swapped-positive"));
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateFragment(persisted, definitions.GetProperty("frozenHostEvidence"), "swapped-negative"));

        using JsonDocument campaign = JsonDocument.Parse(File.ReadAllBytes(TestRepository.PathFromRoot(
            "contracts", "repository", "m1-slice6-finite-campaign-authorization.v2.schema.json")));
        JsonObject profile = JsonNode.Parse("""
            {"mode":"new-only","access_profile_id":"openai-platform-c2f213dbc4d9461c9fa8485050ab324d","generation_id":"g-cb0c3748ef2b4745b97a9311c89f2b65","generation_ordinal":1,"revocation_epoch":0,"display_label":"OpenAI Platform API (M1 Slice 6)","target_derivation":"Infinium:<access_profile_id>:<generation_id>","target_fingerprint_sha256":"7c4683448a864da4b7cb96a07cf13db93cff9b1a1eb22ed013250a2975a9c071","target_encoding":"utf-8","preflight_requirement":"exact-CredReadW-ERROR_NOT_FOUND-or-stop-no-write"}
            """)!.AsObject();
        JsonElement profileSchema = campaign.RootElement.GetProperty("$defs").GetProperty("profile");
        ValidateFragment(profile, profileSchema, "profile");
        JsonObject wrongProfile = profile.DeepClone().AsObject(); wrongProfile["generation_id"] = "g-wrong";
        Assert.ThrowsExactly<InvalidDataException>(() => ValidateFragment(wrongProfile, profileSchema, "wrong-profile"));
    }

    private static void ValidateFragment(JsonNode value, JsonElement schema, string identity)
    {
        using JsonDocument document = JsonDocument.Parse(value.ToJsonString());
        ActiveJsonSchemaValidator.Validate(document.RootElement, schema, identity);
    }

    private static int RunResealer(string root, string mode)
    {
        using Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            ArgumentList = { "fixtures/tooling/reseal-live-semantic-v2.mjs", mode },
        })!;
        process.WaitForExit(20_000);
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Dedicated v2 resealer exceeded its offline bound.");
        }
        return process.ExitCode;
    }

    private sealed class TemporaryAuthority : IDisposable
    {
        private static readonly string[] Roots =
        [
            "contracts/repository",
            "fixtures/public/provider/source-claims/S6-CLAIM-VAL-v1",
            "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-VAL-v3",
            "fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL",
            "fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL",
            "fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL",
            "fixtures/public/provider/source-claims/S6-CLAIM-LIVE-VAL-v2",
            "fixtures/public/provider/candidate-investigations/S6-CANDIDATE-LIVE-VAL-v2",
            "fixtures/public/provider/live-campaign/LLM-CLAIM-LIVE-VAL-v2",
            "fixtures/public/provider/live-campaign/LLM-INVESTIGATE-LIVE-VAL-v2",
            "fixtures/public/provider/live-campaign/PROV-LIVE-COMPOSED-VAL-v2",
            "fixtures/public/platform/provider-budget/capability-val",
        ];

        private TemporaryAuthority(string root) => Root = root;
        public string Root { get; }

        public static TemporaryAuthority Create()
        {
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Infinium-R1-v2-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            foreach (string relative in Roots)
            {
                string source = TestRepository.PathFromRoot(relative.Split('/'));
                string destination = System.IO.Path.Combine(root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));
                CopyDirectory(source, destination);
            }
            foreach (string file in new[] { "fixtures/public/public-fixture-registry.v1.json", "fixtures/public/public-fixture-registry.v2.json" })
            {
                string destination = System.IO.Path.Combine(root, file.Replace('/', System.IO.Path.DirectorySeparatorChar));
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
                File.Copy(TestRepository.PathFromRoot(file.Split('/')), destination);
            }
            string tooling = System.IO.Path.Combine(root, "fixtures", "tooling");
            Directory.CreateDirectory(tooling);
            File.Copy(TestRepository.PathFromRoot("fixtures", "tooling", "reseal-live-semantic-v2.mjs"),
                System.IO.Path.Combine(tooling, "reseal-live-semantic-v2.mjs"));
            File.WriteAllText(System.IO.Path.Combine(root, "Infinium.sln"), string.Empty);
            return new(root);
        }

        public string Path(string relative) => System.IO.Path.Combine(Root, relative.Replace('/', System.IO.Path.DirectorySeparatorChar));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, System.IO.Path.Combine(destination, System.IO.Path.GetFileName(file)));
            }
            foreach (string directory in Directory.EnumerateDirectories(source))
            {
                CopyDirectory(directory, System.IO.Path.Combine(destination, System.IO.Path.GetFileName(directory)));
            }
        }
    }
}
