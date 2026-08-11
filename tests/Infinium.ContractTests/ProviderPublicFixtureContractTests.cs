using System.Text.Json.Nodes;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ProviderPublicFixtureContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "Contract")]
    public void ProviderAnswerFreePackageReaderCoversExactlyNineSchemas()
    {
        Assert.AreEqual(9, ProviderContractExampleReader.Validate(TestRepository.Root));
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Security")]
    [TestProperty("Category", "Contract")]
    [TestProperty("Category", "Security")]
    public void ProviderAnswerFreePackageReaderRejectsAnswerBearingFields()
    {
        string temporaryRoot = Path.Combine(Path.GetTempPath(), $"infinium-provider-example-{Guid.NewGuid():N}");
        string authorityPath = Path.Combine(
            temporaryRoot,
            ProviderContractExampleReader.AuthorityRelativePath.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(authorityPath)!);
            JsonObject authority = JsonNode.Parse(File.ReadAllText(TestRepository.PathFromRoot(
                "fixtures", "public", "contracts", "provider-wp1", "contract-examples.v1.json")))!.AsObject();
            authority["expected_answer"] = "forbidden";
            File.WriteAllText(authorityPath, authority.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => ProviderContractExampleReader.Validate(temporaryRoot));
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }
}
