using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class ProductUserSafetyIdentifierTests
{
    [TestMethod]
    public void ProjectionIsDomainSeparatedStableAndContainsNoSeedMaterial()
    {
        byte[] seed = Enumerable.Range(0, ProductUserSafetyIdentifier.SeedBytes).Select(value => (byte)value).ToArray();
        string first = ProductUserSafetyIdentifier.Project(seed);
        string second = ProductUserSafetyIdentifier.Project(seed);
        byte[] domainInput = Encoding.UTF8.GetBytes(ProductUserSafetyIdentifier.Domain + "\0").Concat(seed).ToArray();

        Assert.AreEqual(Convert.ToHexStringLower(SHA256.HashData(domainInput)), first);
        Assert.AreEqual(first, second);
        Assert.IsTrue(ProductUserSafetyIdentifier.IsValidProjection(first));
        Assert.AreNotEqual(Convert.ToHexStringLower(seed), first);
    }

    [TestMethod]
    public void LocalStateIsCreatedOnceAndNeverDerivedFromForbiddenInputs()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-safety-id-" + Guid.NewGuid().ToString("N"));
        try
        {
            ProductUserSafetyIdentifierStateStore store = new(root);
            string first = store.GetOrCreateProjection();
            string second = store.GetOrCreateProjection();
            Assert.AreEqual(first, second);
            string path = Path.Combine(root, ProductUserSafetyIdentifierStateStore.StateFileName);
            Assert.AreEqual(ProductUserSafetyIdentifier.SeedBytes, new FileInfo(path).Length);

            Assert.AreNotEqual(first, ProductUserSafetyIdentifier.Project(new byte[ProductUserSafetyIdentifier.SeedBytes]));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void MalformedOrTruncatedStateFailsClosed()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-safety-id-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, ProductUserSafetyIdentifierStateStore.StateFileName), [1, 2, 3]);
            Assert.ThrowsExactly<InvalidDataException>(() => new ProductUserSafetyIdentifierStateStore(root).GetOrCreateProjection());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void SerializerRejectsMissingUppercaseAndMalformedProjection()
    {
        using System.Text.Json.JsonDocument schema = System.Text.Json.JsonDocument.Parse(ProviderAdapterTestData.OutputSchemaBytes);
        foreach (string value in new[] { "", ProviderAdapterTestData.SafetyIdentifier.ToUpperInvariant(), new string('a', 63), new string('g', 64) })
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => Infinium.OpenAI.OpenAiResponsesCanonicalSerializer.Serialize(new(
                ProviderOperationKind.TransportQualification, "bounded", "bounded", schema.RootElement.Clone(), 256, value)), value);
        }
    }
}
