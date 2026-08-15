using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Wp8PreLiveReadinessContractTests
{
    private static readonly string[] RelativeTemplates =
    [
        "docs/plans/milestones/m1/slices/s6/wp8-production-profile-authorization.template.v1.json",
        "docs/plans/milestones/m1/slices/s6/wp8-qualification-authorization.template.v1.json",
        "docs/plans/milestones/m1/slices/s6/wp8-source-claim-authorization.template.v1.json",
        "docs/plans/milestones/m1/slices/s6/wp8-candidate-investigation-authorization.template.v1.json",
    ];

    [TestMethod]
    [TestCategory("Contract")]
    public void MatrixAndFourTemplatesAreClosedFiniteAndNonExecutable()
    {
        string root = RepositoryRoot();
        JsonObject matrix = ReadNode(root, "docs/plans/milestones/m1/slices/s6/wp8-case-requirement-matrix.v1.json");
        Assert.AreEqual("infinium.m1-s6.wp8.case-requirement-matrix/v1", matrix["matrix_id"]!.GetValue<string>());
        Assert.AreEqual(23, matrix["cases"]!.AsArray().Count);
        Assert.AreEqual(6, matrix["evidence_groups"]!.AsArray().Count);
        Assert.AreEqual(0, matrix["external_effects"]!["credential_manager_operations"]!.GetValue<int>());
        Assert.AreEqual(0, matrix["external_effects"]!["provider_requests"]!.GetValue<int>());
        Assert.AreEqual("pending-fresh-independent-review", matrix["review"]!["judgment"]!.GetValue<string>());

        HashSet<string> packetIds = new(StringComparer.Ordinal);
        HashSet<string> packetKinds = new(StringComparer.Ordinal);
        foreach (string relative in RelativeTemplates)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            using JsonDocument document = JsonDocument.Parse(bytes);
            JsonElement template = document.RootElement;
            Assert.AreEqual("non-executable-template", template.GetProperty("status").GetString());
            Assert.AreEqual("none", template.GetProperty("effect_authority").GetString());
            Assert.IsFalse(template.GetProperty("execution").GetProperty("permitted").GetBoolean());
            Assert.AreEqual(JsonValueKind.Null, template.GetProperty("execution").GetProperty("command").ValueKind);
            Assert.IsTrue(packetIds.Add(template.GetProperty("packet_id").GetString()!));
            Assert.IsTrue(packetKinds.Add(template.GetProperty("packet_kind").GetString()!));
            string text = Encoding.UTF8.GetString(bytes);
            Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(text,
                @"(?i)bearer\s+[A-Za-z0-9._-]+|sk-(?:proj-)?[A-Za-z0-9_-]{8,}|Infinium:[^\""\s]+"));
        }
        Assert.AreEqual(4, packetIds.Count);
        Assert.AreEqual(4, packetKinds.Count);

        foreach (string relative in new[]
        {
            "contracts/repository/wp8-case-requirement-matrix.v1.schema.json",
            "contracts/repository/wp8-production-profile-authorization-template.v1.schema.json",
            "contracts/repository/wp8-provider-request-authorization-template.v1.schema.json",
        })
        {
            using JsonDocument schema = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,
                relative.Replace('/', Path.DirectorySeparatorChar))));
            Assert.IsFalse(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
            StringAssert.StartsWith(schema.RootElement.GetProperty("$id").GetString()!,
                "https://schemas.infinium.dev/repository/wp8-");
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void SemanticValidatorAcceptsExactCandidateAndRejectsPacketMatrixMutations()
    {
        Assert.AreEqual(0, RunValidator(null), "Exact WP8 candidate failed semantic validation.");

        (string Name, Action<Dictionary<string, JsonObject>> Mutation)[] mutations =
        [
            ("missing-case", docs => docs["matrix"]["cases"]!.AsArray().RemoveAt(0)),
            ("unknown-property", docs => docs["profile"]["unexpected"] = true),
            ("matrix-nested-unknown", docs => docs["matrix"]["cases"]![0]!["unexpected"] = true),
            ("profile-nested-unknown", docs => docs["profile"]["persistence_delete"]!["unexpected"] = true),
            ("request-nested-unknown", docs => docs["qualification"]["limits"]!["unexpected"] = 1),
            ("case-classification", docs => docs["matrix"]["cases"]![0]!["classification"] = "primary"),
            ("case-disposition", docs => docs["matrix"]["cases"]![4]!["disposition"] = "covered-non-live"),
            ("case-requirement", docs => docs["matrix"]["cases"]![0]!["requirements"]![0] = "SNAP-001"),
            ("case-gate", docs => docs["matrix"]["cases"]![0]!["evidence_gates"]![0] = "Budget"),
            ("case-assertion", docs => docs["matrix"]["cases"]![0]!["covered_assertions"]![0] = "weakened"),
            ("case-na-tuple", docs => docs["matrix"]["cases"]![4]!["n_a_assertions"]![0]!["later_authority"] = "WP10"),
            ("supplemental-mapping", docs => docs["matrix"]["supplemental_requirement_mappings"]![0]!["evidence_gates"]![0] = "Budget"),
            ("operation-swap", docs => docs["qualification"]["request_binding"]!["operation"] = "source-claim-extraction"),
            ("packet-id", docs => docs["source"]["packet_id"] = "infinium.m1-s6.wp8.pre-live-qualification-authorization-template/v1"),
            ("limit", docs => docs["candidate"]["limits"]!["maximum_dispatch_count"] = 2),
            ("unbounded-extra-limit", docs => docs["candidate"]["limits"]!["maximum_total_tokens"] = 999999999),
            ("retry", docs => docs["source"]["transport_boundary"]!["automatic_retry"] = true),
            ("predecessor", docs => docs["source"]["candidate_binding"]!["required_predecessor_live_acceptance"] = "accepted-WP10-source-claim-operation-receipt-pending"),
            ("official-doc", docs => docs["candidate"]["capability_price_binding"]!["official_doc_snapshot_sha256"] = new string('0', 64)),
            ("refresh-authority", docs => docs["source"]["capability_price_binding"]!["official_doc_refresh_authority"] = "inherited-from-WP10"),
            ("owner-inheritance", docs => docs["qualification"]["owner_authorization"]!["inheritance"] = "allowed"),
            ("common-binding", docs => docs["profile"]["candidate_binding"]!["accepted_wp4_execution_commit"] = new string('0', 40)),
            ("product-template-identity", docs => docs["matrix"]["candidate_binding"]!["wp8_product_template_commit"] = new string('0', 40)),
            ("verification-identity", docs => docs["matrix"]["candidate_binding"]!["wp8_verification_candidate_commit"] = new string('0', 40)),
            ("delete-enabled", docs => docs["profile"]["persistence_delete"]!["deletion_permitted"] = true),
            ("delete-call-authorized", docs => docs["profile"]["native_boundary"]!["forbidden_calls"]!.AsArray().RemoveAt(0)),
            ("secret", docs => docs["qualification"]["request_binding"]!["canonical_request"] = "Bearer sk-proj-forbidden-canary"),
            ("raw-target", docs => docs["profile"]["canaries"]!["target_canary"] = "Infinium:raw:target"),
            ("stale-identity", docs => docs["matrix"]["candidate_binding"]!["accepted_wp7_product_commit"] = new string('0', 40)),
            ("executable", docs => docs["candidate"]["status"] = "ready-for-owner-acceptance"),
        ];
        foreach ((string name, Action<Dictionary<string, JsonObject>> mutation) in mutations)
        {
            Assert.AreNotEqual(0, RunValidator(mutation), $"WP8 validator accepted mutation '{name}'.");
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void NonLiveAllIsClosedAndDoesNotInvokeNativeOrLiveGates()
    {
        string script = TestRepository.Read("eng", "verify-m1-slice6.ps1");
        StringAssert.Contains(script, "'NonLiveAll'");
        StringAssert.Contains(script, "function Invoke-NonLiveAllGate");
        StringAssert.Contains(script, "NonLiveAll refuses every authorization manifest.");
        StringAssert.Contains(script, "Invoke-Wp8PreLiveValidationGate");
        StringAssert.Contains(script, "content-bound evidence");
        StringAssert.Contains(script, "credential_manager_operations = 0");
        StringAssert.Contains(script, "provider_requests = 0");
        StringAssert.Contains(script, "live_manifest_execution = $false");

        System.Text.RegularExpressions.Match function = System.Text.RegularExpressions.Regex.Match(script,
            @"(?ms)^function Invoke-NonLiveAllGate \{.*?^\}");
        Assert.IsTrue(function.Success, "NonLiveAll implementation was not found.");
        Assert.IsFalse(function.Value.Contains("Invoke-CredentialNativeGate", StringComparison.Ordinal));
        Assert.IsFalse(function.Value.Contains("Invoke-CredentialNativeRecoveryGate", StringComparison.Ordinal));
        Assert.IsFalse(function.Value.Contains("run-m1-slice6-live", StringComparison.Ordinal));
        Assert.IsFalse(function.Value.Contains("run-m1-slice6-credential", StringComparison.Ordinal));
    }

    private static int RunValidator(Action<Dictionary<string, JsonObject>>? mutation)
    {
        string root = RepositoryRoot();
        string temp = Path.Combine(Path.GetTempPath(), "infinium-wp8-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            Dictionary<string, JsonObject> docs = new(StringComparer.Ordinal)
            {
                ["matrix"] = ReadNode(root, "docs/plans/milestones/m1/slices/s6/wp8-case-requirement-matrix.v1.json"),
                ["profile"] = ReadNode(root, RelativeTemplates[0]),
                ["qualification"] = ReadNode(root, RelativeTemplates[1]),
                ["source"] = ReadNode(root, RelativeTemplates[2]),
                ["candidate"] = ReadNode(root, RelativeTemplates[3]),
            };
            mutation?.Invoke(docs);
            foreach ((string name, JsonObject document) in docs)
            {
                File.WriteAllText(Path.Combine(temp, name + ".json"), document.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true,
                }), new UTF8Encoding(false));
            }

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
            start.ArgumentList.Add(Path.Combine(root, "eng", "validate-m1-slice6-wp8-prelive.ps1"));
            start.ArgumentList.Add("-MatrixPath");
            start.ArgumentList.Add(Path.Combine(temp, "matrix.json"));
            start.ArgumentList.Add("-ProfileTemplatePath");
            start.ArgumentList.Add(Path.Combine(temp, "profile.json"));
            start.ArgumentList.Add("-QualificationTemplatePath");
            start.ArgumentList.Add(Path.Combine(temp, "qualification.json"));
            start.ArgumentList.Add("-SourceClaimTemplatePath");
            start.ArgumentList.Add(Path.Combine(temp, "source.json"));
            start.ArgumentList.Add("-CandidateTemplatePath");
            start.ArgumentList.Add(Path.Combine(temp, "candidate.json"));
            using Process process = Process.Start(start)!;
            Assert.IsTrue(process.WaitForExit(30_000), "WP8 validator mutation process timed out.");
            return process.ExitCode;
        }
        finally
        {
            Directory.Delete(temp, true);
        }
    }

    private static JsonObject ReadNode(string root, string relative) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))))!.AsObject();

    private static string RepositoryRoot() => Path.GetFullPath("../../../../../", AppContext.BaseDirectory);
}
