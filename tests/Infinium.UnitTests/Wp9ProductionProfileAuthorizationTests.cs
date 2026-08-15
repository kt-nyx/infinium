using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Wp9ProductionProfileAuthorizationTests
{
    [TestMethod]
    public void ExactDraftManifestValidatesAndRecursiveSchemaIsClosed()
    {
        string root = RepositoryRoot();
        string manifest = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v1.json");
        ProcessResult result = RunValidator(root, manifest, mutation: false);
        Assert.AreEqual(0, result.ExitCode, result.Error);
        StringAssert.Contains(result.Output, "validated-draft-binding-pending");

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
            node => node["profile"]!["credential_target"] = "Infinium:wrong:target",
            node => node["profile"]!["target_fingerprint_sha256"] = new string('0', 64),
            node => node["native_boundary"]!["exact_call_order"] = new JsonArray("CredWriteW", "CredReadW", "CredFree"),
            node => node["native_boundary"]!["maximum_calls"]!["CredDeleteW"] = 1,
            node => node["m1_entry_surface"]!["paste_permitted"] = false,
            node => node["m1_entry_surface"]!["renderer_receives_or_retains_secret"] = true,
            node => node["future_product_ux"]!["implemented_by_wp9"] = true,
            node => node["durable_state"]!["success_state"] = "active-unverified",
            node => node["durable_state"]!["active_unverified_request_gate"] = "admit",
            node => node["provider_intent"]!["provider_request_permitted"] = true,
            node => node["official_document_refresh"]!["documents"]![0]!["sha256"] = new string('0', 64),
            node => node["official_document_refresh"]!["drift_follow_up"]!["provider_request_packet_blocked"] = false,
            node => node["owner_authorization"]!["inheritance"] = "allowed",
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
    public void RunnerRejectsMissingAuthorityBeforeOutputHelperOrNativeBoundary()
    {
        string root = RepositoryRoot();
        string manifest = Path.Combine(root, "docs", "plans", "milestones", "m1", "slices", "s6",
            "wp9-production-profile-authorization.v1.json");
        string output = Path.Combine(root, "artifacts", "m1-slice6", "wp9-profile");
        Assert.IsFalse(Directory.Exists(output));
        ProcessStartInfo start = new("pwsh.exe")
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
            || errorText.Contains("exactly one canonical owner-acceptance line", StringComparison.Ordinal),
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

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
