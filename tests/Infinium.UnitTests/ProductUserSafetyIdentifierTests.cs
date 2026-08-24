using System.Security.Cryptography;
using System.Text;
using Infinium.Domain.Contracts;
using Infinium.Persistence;

namespace Infinium.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class ProductUserSafetyIdentifierTests
{
    private static readonly string[] SeedRecordProperties = ["Schema", "Scope", "SeedBase64", "Projection"];
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
            using System.Text.Json.JsonDocument state = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(path));
            CollectionAssert.AreEqual(SeedRecordProperties,
                state.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
            Assert.AreEqual(ProductUserSafetyIdentifierStateStore.StateSchema,
                state.RootElement.GetProperty("Schema").GetString());
            Assert.AreEqual(ProductUserSafetyIdentifierStateStore.ProductUserScope,
                state.RootElement.GetProperty("Scope").GetString());
            Assert.AreEqual(ProductUserSafetyIdentifier.SeedBytes,
                Convert.FromBase64String(state.RootElement.GetProperty("SeedBase64").GetString()!).Length);
            Assert.AreEqual(first, state.RootElement.GetProperty("Projection").GetString());

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
    public void UsedStateSurvivesRestartAndMissingCorruptOrTornBytesNeverRegenerate()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-safety-id-" + Guid.NewGuid().ToString("N"));
        try
        {
            ProductUserSafetyIdentifierStateStore first = new(root);
            string projection = first.LatchPossibleStart();
            ProductUserSafetyIdentifierStateStore reopened = new(root);
            Assert.AreEqual(projection, reopened.GetRequiredProjection(projection));
            Assert.AreEqual(projection, reopened.GetOrCreateProjection());

            string seed = Path.Combine(root, ProductUserSafetyIdentifierStateStore.StateFileName);
            byte[] retained = File.ReadAllBytes(seed);
            File.Delete(seed);
            Assert.ThrowsExactly<InvalidDataException>(() => reopened.GetRequiredProjection(projection));
            Assert.ThrowsExactly<InvalidDataException>(() => reopened.GetOrCreateProjection());

            File.WriteAllBytes(seed, retained);
            string latch = Path.Combine(root, ProductUserSafetyIdentifierStateStore.UseLatchFileName);
            File.WriteAllText(latch, projection[..31]);
            Assert.ThrowsExactly<InvalidDataException>(() => reopened.GetRequiredProjection(projection));
            File.WriteAllText(latch, "{\"Schema\":\"infinium.product-user-safety-identifier-use/v1\",\"Projection\":\""
                + new string('f', 64) + "\",\"State\":\"possible-start-latched\"}");
            Assert.ThrowsExactly<InvalidDataException>(() => reopened.GetRequiredProjection(projection));
            CryptographicOperations.ZeroMemory(retained);
        }
        finally
        {
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }

    [TestMethod]
    public async Task ConcurrentCreateAndUseLatchConvergeOnOneProjection()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-safety-id-" + Guid.NewGuid().ToString("N"));
        try
        {
            Task<string>[] operations = Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => new ProductUserSafetyIdentifierStateStore(root).LatchPossibleStart()))
                .ToArray();
            string[] projections = await Task.WhenAll(operations);
            Assert.AreEqual(1, projections.Distinct(StringComparer.Ordinal).Count());
            Assert.AreEqual(projections[0], new ProductUserSafetyIdentifierStateStore(root)
                .GetRequiredProjection(projections[0]));
        }
        finally
        {
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
        }
    }

    [TestMethod]
    public async Task ImmutableSeedReadWaitsForTransientWindowsSharingLock()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-safety-id-" + Guid.NewGuid().ToString("N"));
        try
        {
            ProductUserSafetyIdentifierStateStore store = new(root);
            string projection = store.GetOrCreateProjection();
            string seedPath = Path.Combine(root, ProductUserSafetyIdentifierStateStore.StateFileName);
            Task<string> blocked;
            using (FileStream exclusive = new(seedPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.ThrowsExactly<IOException>(() => File.ReadAllBytes(seedPath));
                TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
                blocked = Task.Run(() =>
                {
                    entered.SetResult();
                    return new ProductUserSafetyIdentifierStateStore(root).GetOrCreateProjection();
                });
                await entered.Task;
                await Task.Delay(25);
            }
            Assert.AreEqual(projection, await blocked);
        }
        finally
        {
            if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
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
