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
        string[] implementedRpcs = declaredRpcs
            .Where(rpc => Regex.IsMatch(
                coordinatorSource,
                $@"public\s+override[\s\S]{{0,180}}\b{Regex.Escape(rpc)}\s*\(",
                RegexOptions.CultureInvariant))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.AreEqual(implementedRpcs.Length, root.GetProperty("protocol").GetProperty("implemented_rpc_count").GetInt32());

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
            declaredRpcs.Except(implementedRpcs, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            inventoryUnimplemented);
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
