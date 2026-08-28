using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infinium.Application.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed partial class ApplicationFoundationAuthorityContractTests
{
    private static readonly string[] DeclaredUnimplementedRpcs =
    [
        "GetProviderOperation",
        "GetProviderProfile",
        "GetProviderReplay",
        "ListProviderBudget",
        "SubmitProviderEnrollment",
        "SubmitProviderOperation",
    ];
    private static readonly string[] DeclaredFailClosedRpcs = [];

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void ApplicationInventoryIsStrictCompleteAndMatchesImplementedService()
    {
        byte[] inventoryBytes = File.ReadAllBytes(FoundationPath("application-contract-inventory.v1.json"));
        byte[] schemaBytes = File.ReadAllBytes(FoundationPath("application-contract-inventory.v1.schema.json"));
        ActiveRepositoryJsonSchemaValidator.Validate(
            inventoryBytes,
            schemaBytes,
            "application-contract-inventory.v1.schema.json");

        using JsonDocument inventory = JsonDocument.Parse(inventoryBytes);
        JsonElement root = inventory.RootElement;
        string proto = TestRepository.Read(
            "contracts", "protobuf", "infinium", "application", "v1", "application.proto");
        string[] declaredRpcs = RpcRegex().Matches(proto)
            .Select(match => match.Groups["name"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] inventoriedRpcs = root.GetProperty("rpc_inventory")
            .EnumerateArray()
            .Select(item => item.GetProperty("rpc").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(declaredRpcs, inventoriedRpcs);
        Assert.AreEqual(declaredRpcs.Length, root.GetProperty("protocol").GetProperty("declared_rpc_count").GetInt32());

        string coordinatorSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(
                    TestRepository.PathFromRoot("src", "Infinium.Coordinator"),
                    "ApplicationGrpcService*.cs")
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        string[] handlerRpcs = declaredRpcs
            .Where(rpc => Regex.IsMatch(
                coordinatorSource,
                $@"public\s+override[\s\S]{{0,180}}\b{Regex.Escape(rpc)}\s*\(",
                RegexOptions.CultureInvariant))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] inventoryImplemented = root.GetProperty("rpc_inventory")
            .EnumerateArray()
            .Where(item => StringComparer.Ordinal.Equals(
                item.GetProperty("implementation_state").GetString(),
                "implemented"))
            .Select(item => item.GetProperty("rpc").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.AreEqual(inventoryImplemented.Length,
            root.GetProperty("protocol").GetProperty("implemented_rpc_count").GetInt32());
        Assert.IsEmpty(inventoryImplemented.Except(handlerRpcs, StringComparer.Ordinal));

        string[] inventoryUnimplemented = root.GetProperty("rpc_inventory")
            .EnumerateArray()
            .Where(item => StringComparer.Ordinal.Equals(
                item.GetProperty("implementation_state").GetString(),
                "declared-unimplemented"))
            .Select(item => item.GetProperty("rpc").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(DeclaredUnimplementedRpcs, inventoryUnimplemented);
        CollectionAssert.AreEqual(
            DeclaredFailClosedRpcs,
            handlerRpcs.Except(inventoryImplemented, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
        Assert.IsEmpty(declaredRpcs.Except(
            handlerRpcs.Concat(inventoryUnimplemented), StringComparer.Ordinal));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void CapabilityMatrixIsStrictFullyOwnedAndDeniesGenericRendererAuthority()
    {
        byte[] matrixBytes = File.ReadAllBytes(FoundationPath("frontend-capability-matrix.v1.json"));
        byte[] schemaBytes = File.ReadAllBytes(FoundationPath("frontend-capability-matrix.v1.schema.json"));
        ActiveRepositoryJsonSchemaValidator.Validate(
            matrixBytes,
            schemaBytes,
            "frontend-capability-matrix.v1.schema.json");

        using JsonDocument matrix = JsonDocument.Parse(matrixBytes);
        JsonElement root = matrix.RootElement;
        JsonElement[] capabilities = root.GetProperty("capabilities").EnumerateArray().ToArray();
        JsonElement audit = root.GetProperty("ownership_audit");
        Assert.AreEqual(capabilities.Length, audit.GetProperty("capability_count").GetInt32());
        Assert.AreEqual(capabilities.Length, audit.GetProperty("owned_capability_count").GetInt32());
        Assert.AreEqual(0, audit.GetProperty("unknown_or_unowned_count").GetInt32());
        Assert.AreEqual(
            capabilities.Length,
            capabilities.Select(item => item.GetProperty("capability_id").GetString())
                .Distinct(StringComparer.Ordinal).Count());

        foreach (JsonElement capability in capabilities)
        {
            string owner = capability.GetProperty("owning_phase").GetString()!
                + "/" + capability.GetProperty("owning_work_package").GetString()!;
            Assert.IsFalse(owner.Contains("unknown", StringComparison.OrdinalIgnoreCase), owner);
            Assert.IsFalse(owner.Contains("unowned", StringComparison.OrdinalIgnoreCase), owner);
        }

        using JsonDocument inventory = JsonDocument.Parse(
            File.ReadAllBytes(FoundationPath("application-contract-inventory.v1.json")));
        string denied = string.Join(
            "\n",
            inventory.RootElement.GetProperty("renderer_denied_operations")
                .EnumerateArray()
                .Select(item => item.GetProperty("operation").GetString()));
        foreach (string primitive in new[]
        {
            "path", "SQL", "command", "URL", "credential", "provider", "filesystem", "coordinator proxy",
        })
        {
            StringAssert.Contains(denied, primitive, StringComparison.OrdinalIgnoreCase);
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    public void IntegratedAcceptanceWorkflowIsExactOfflineAndPreservesNativeOnlyAuthority()
    {
        byte[] manifestBytes = File.ReadAllBytes(FoundationPath("frontend-foundation-acceptance.v1.json"));
        byte[] schemaBytes = File.ReadAllBytes(FoundationPath("frontend-foundation-acceptance.v1.schema.json"));
        ActiveRepositoryJsonSchemaValidator.Validate(
            manifestBytes,
            schemaBytes,
            "frontend-foundation-acceptance.v1.schema.json");

        using JsonDocument manifest = JsonDocument.Parse(manifestBytes);
        JsonElement root = manifest.RootElement;
        Assert.AreEqual("inactive", root.GetProperty("m" + "2_state").GetString());
        Assert.AreEqual(
            "6b9b92a5f3dae0e90219f521919555956a8b5623",
            root.GetProperty("checkpoint_d_commit").GetString());

        string[] expectedEvaluations =
        [
            "EVAL-0090",
            "EVAL-0091",
            "EVAL-0092",
            "EVAL-0093",
            "EVAL-0094",
        ];
        string[] actualEvaluations = root.GetProperty("evaluation_ids")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(expectedEvaluations, actualEvaluations);

        JsonElement[] steps = root.GetProperty("workflow").EnumerateArray().ToArray();
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 16).ToArray(),
            steps.Select(step => step.GetProperty("step").GetInt32()).ToArray());
        CollectionAssert.AreEquivalent(
            expectedEvaluations,
            steps.SelectMany(step => step.GetProperty("evaluation_ids").EnumerateArray())
                .Select(item => item.GetString()!)
                .Distinct(StringComparer.Ordinal)
                .ToArray());

        JsonElement[] proofs = steps
            .SelectMany(step => step.GetProperty("proofs").EnumerateArray())
            .ToArray();
        Assert.AreEqual(
            proofs.Length,
            proofs.Select(proof => proof.GetProperty("proof_id").GetString())
                .Distinct(StringComparer.Ordinal)
                .Count());
        foreach (JsonElement step in steps)
        {
            JsonElement[] stepProofs = step.GetProperty("proofs").EnumerateArray().ToArray();
            Assert.IsTrue(stepProofs.Any(proof => !StringComparer.Ordinal.Equals(
                proof.GetProperty("kind").GetString(),
                "reference")));
            foreach (JsonElement proof in stepProofs)
            {
                string kind = proof.GetProperty("kind").GetString()!;
                if (StringComparer.Ordinal.Equals(kind, "executable-test") ||
                    StringComparer.Ordinal.Equals(kind, "desktop-qualification-test"))
                {
                    string project = proof.GetProperty("project").GetString()!;
                    Assert.IsTrue(
                        File.Exists(TestRepository.PathFromRoot([.. project.Split('/')])),
                        $"Acceptance test project does not exist: {project}");
                    StringAssert.StartsWith(
                        proof.GetProperty("fully_qualified_name").GetString(),
                        "Infinium.Tests.");
                    Assert.IsTrue(proof.GetProperty("required").GetBoolean());
                }
                else if (StringComparer.Ordinal.Equals(kind, "machine-evidence"))
                {
                    Assert.AreEqual(
                        "artifacts/desktop-qualification/summary.json",
                        proof.GetProperty("path").GetString());
                    Assert.IsTrue(proof.GetProperty("required").GetBoolean());
                }
                else
                {
                    Assert.AreEqual("reference", kind);
                    Assert.IsFalse(proof.GetProperty("required").GetBoolean());
                    string reference = proof.GetProperty("path").GetString()!;
                    Assert.IsTrue(
                        File.Exists(TestRepository.PathFromRoot([.. reference.Split('/')])),
                        $"Acceptance reference does not exist: {reference}");
                }
            }
        }

        JsonElement targetedVerification = steps.Single(step => step.GetProperty("step").GetInt32() == 13);
        Assert.AreEqual("native-generated-client", targetedVerification.GetProperty("consumer").GetString());
        Assert.AreEqual("native-application", targetedVerification.GetProperty("authority").GetString());
        Assert.AreEqual(
            "producer-consumer-validated",
            targetedVerification.GetProperty("surface_maturity").GetString());
        Assert.IsTrue(targetedVerification.GetProperty("proofs").EnumerateArray().Any(proof =>
            StringComparer.Ordinal.Equals(proof.GetProperty("kind").GetString(), "executable-test")));
        Assert.IsTrue(targetedVerification.GetProperty("proofs").EnumerateArray().Any(proof =>
            StringComparer.Ordinal.Equals(proof.GetProperty("kind").GetString(), "reference") &&
            !proof.GetProperty("required").GetBoolean()));

        using JsonDocument inventory = JsonDocument.Parse(
            File.ReadAllBytes(FoundationPath("application-contract-inventory.v1.json")));
        string[] targetedVerificationRpcs =
        [
            "BeginTargetedVerificationPreparation",
            "GetTargetedVerificationPreparation",
            "CancelTargetedVerificationPreparation",
            "StartTargetedVerification",
            "GetTargetedVerification",
        ];
        JsonElement[] inventoriedTargetedVerification = inventory.RootElement.GetProperty("rpc_inventory")
            .EnumerateArray()
            .Where(item => targetedVerificationRpcs.Contains(
                item.GetProperty("rpc").GetString(),
                StringComparer.Ordinal))
            .ToArray();
        Assert.HasCount(targetedVerificationRpcs.Length, inventoriedTargetedVerification);
        Assert.IsTrue(inventoriedTargetedVerification.All(item => StringComparer.Ordinal.Equals(
            "native-only-never-map",
            item.GetProperty("renderer_policy").GetString())));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void AcceptanceEvidenceBindingRejectsUnverifiedOrSubstitutedProofs()
    {
        ProcessStartInfo start = new()
        {
            FileName = "powershell",
            WorkingDirectory = TestRepository.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(TestRepository.PathFromRoot(
            "eng", "test-frontend-foundation-evidence.ps1"));

        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("The evidence mutation checks could not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        bool exited = process.WaitForExit(milliseconds: 30_000);
        if (!exited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }

        Assert.IsTrue(exited, "Evidence mutation checks did not finish within 30 seconds.");
        Assert.AreEqual(0, process.ExitCode, $"{output}{error}");
        StringAssert.Contains(output, "Frontend foundation evidence mutation checks passed: 14");
    }

    private static string FoundationPath(string fileName) => Directory.EnumerateFiles(
            TestRepository.PathFromRoot("docs", "plans", "transitions"),
            fileName,
            SearchOption.AllDirectories)
        .Single(path => StringComparer.Ordinal.Equals(
            Path.GetFileName(Path.GetDirectoryName(path)),
            "frontend-application-foundation"));

    [GeneratedRegex(@"\brpc\s+(?<name>[A-Z][A-Za-z0-9]*)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex RpcRegex();
}
