using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Runtime;
using Infinium.Bethesda;
using Infinium.Coordinator;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

[TestClass]
public sealed class BethesdaSemanticBoundaryIntegrationTests
{
    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public void CoordinatorAdmitsOnlySnapshotBoundStrictTypedResult()
    {
        BethesdaSemanticRequest request = BethesdaSemanticTestSnapshot.Create("BETH-REFR-DEV");
        BethesdaSemanticExtractionResult extracted = new BethesdaSemanticExtractor().Extract(request);
        ManagedBethesdaSemanticAssignment assignment = new(
            request.AcceptedSnapshot,
            [],
            extracted.Snapshot!.Plugins.Select(plugin => new ManagedBethesdaPluginSeal(
                plugin.PluginName,
                plugin.LoadOrder,
                plugin.SnapshotAuthorizedPath,
                plugin.ByteLength,
                plugin.Sha256.Value)).ToArray());
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(extracted);

        BethesdaSemanticExtractionResult admitted =
            BethesdaSemanticPublicationValidator.DeserializeAndValidate(
                bytes,
                assignment,
                16 * 1024 * 1024);

        Assert.AreEqual(extracted.Snapshot!.DependencyFingerprint, admitted.Snapshot!.DependencyFingerprint);
        BethesdaSemanticExtractionResult tampered = extracted with
        {
            Snapshot = extracted.Snapshot with
            {
                SourceSnapshotId = new OpaqueId("snapshot-tampered"),
            },
        };
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            BethesdaSemanticPublicationValidator.DeserializeAndValidate(
                JsonSerializer.SerializeToUtf8Bytes(tampered),
                assignment,
                16 * 1024 * 1024));
        Assert.ThrowsExactly<JsonException>(() =>
            BethesdaSemanticPublicationValidator.DeserializeAndValidate(
                "{\"unexpected\":true}"u8,
                assignment,
                16 * 1024 * 1024));
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public void CoordinatorRejectsTamperedSealsDependencyWinnersAndReverseLinks()
    {
        BethesdaSemanticRequest request = BethesdaSemanticTestSnapshot.Create("BETH-REFR-DEV");
        BethesdaSemanticExtractionResult extracted = new BethesdaSemanticExtractor().Extract(request);
        Assert.IsNotNull(extracted.Snapshot);
        ManagedBethesdaPluginSeal[] seals = extracted.Snapshot.Plugins.Select(plugin => new ManagedBethesdaPluginSeal(
            plugin.PluginName,
            plugin.LoadOrder,
            plugin.SnapshotAuthorizedPath,
            plugin.ByteLength,
            plugin.Sha256.Value)).ToArray();
        ManagedBethesdaSemanticAssignment assignment = new(request.AcceptedSnapshot, [], seals);

        ManagedBethesdaPluginSeal[] badSeals = [.. seals];
        badSeals[0] = badSeals[0] with { Sha256 = new string('0', 64) };
        AssertRejected(extracted, assignment with { PluginSeals = badSeals });

        AssertRejected(extracted with
        {
            Snapshot = extracted.Snapshot with
            {
                DependencyFingerprint = new Sha256Fingerprint(new string('0', 64)),
            },
        }, assignment);

        Dictionary<string, BethesdaRecordContribution> missingWinner = extracted.Snapshot.Winners
            .Skip(1)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        AssertRejected(extracted with
        {
            Snapshot = extracted.Snapshot with { Winners = missingWinner },
        }, assignment);

        Dictionary<string, IReadOnlyList<BethesdaLinkFact>> missingReverse = extracted.Snapshot.ReverseLinks
            .Skip(1)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        AssertRejected(extracted with
        {
            Snapshot = extracted.Snapshot with { ReverseLinks = missingReverse },
        }, assignment);
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public void ExtractionReadsOnlySnapshotWinnersAndDoesNotMutateInputs()
    {
        BethesdaSemanticRequest request = BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV");
        string[] paths = request.AcceptedSnapshot.Snapshot!.LooseProviderChains
            .Select(chain => chain.Winner.PhysicalPath)
            .ToArray();
        Dictionary<string, string> before = paths.ToDictionary(
            path => path,
            path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
            StringComparer.OrdinalIgnoreCase);

        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(request);

        Assert.IsNotNull(result.Snapshot);
        CollectionAssert.AreEquivalent(
            before,
            paths.ToDictionary(
                path => path,
                path => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))),
                StringComparer.OrdinalIgnoreCase));
        CollectionAssert.AreEqual(
            request.AcceptedSnapshot.Snapshot.Plugins
                .Where(plugin => plugin.Enabled)
                .OrderBy(plugin => plugin.LoadOrder)
                .Select(plugin => plugin.Name)
                .ToArray(),
            result.Snapshot.Plugins.Select(plugin => plugin.PluginName).ToArray());
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Fault")]
    public void MidReadIdentityChangeFailsWithoutPartialSemanticAuthority()
    {
        string source = TestRepository.PathFromRoot(
            "test-data", "evaluation", "m1-semantic", "BETH-MALFORMED-VAL",
            "inputs", "mutations", "ChangedDuringRead-A.esp");
        string alternate = TestRepository.PathFromRoot(
            "test-data", "evaluation", "m1-semantic", "BETH-MALFORMED-VAL",
            "inputs", "mutations", "ChangedDuringRead-B.esp");
        string root = Path.Combine(Path.GetTempPath(), $"infinium-bethesda-drift-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string target = Path.Combine(root, "ChangedDuringRead.esp");
        File.Copy(source, target);
        try
        {
            BethesdaSemanticRequest request = BethesdaSemanticTestSnapshot.Create(
            [
                ("ChangedDuringRead.esp", 0, target, new OpaqueId("drift-provider")),
            ]);
            BethesdaSemanticExtractor extractor = new((path, _) =>
            {
                File.Copy(alternate, path, overwrite: true);
            });

            BethesdaSemanticExtractionResult result = extractor.Extract(request);

            Assert.AreEqual(BethesdaExtractionState.ChangedDuringRead, result.State);
            Assert.IsNull(result.Snapshot);
            Assert.AreEqual("plugin-changed-during-read", result.Failures.Single().Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [TestCategory("M1Integration")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Integration")]
    [TestProperty("Category", "M1Security")]
    public void SnapshotWinnerPathCannotBeReboundToAnotherPluginName()
    {
        BethesdaSemanticRequest request = BethesdaSemanticTestSnapshot.Create("BETH-LIGHT-VAL");
        Infinium.Mo2.Mo2InstallationSnapshot source = request.AcceptedSnapshot.Snapshot!;
        Infinium.Mo2.LooseProviderChain first = source.LooseProviderChains[0];
        Infinium.Mo2.LooseProvider rebound = first.Winner with
        {
            PhysicalPath = source.LooseProviderChains[1].Winner.PhysicalPath,
        };
        BethesdaSemanticRequest tampered = request with
        {
            AcceptedSnapshot = request.AcceptedSnapshot with
            {
                Snapshot = source with
                {
                    LooseProviderChains =
                    [
                        first with { Winner = rebound },
                        .. source.LooseProviderChains.Skip(1),
                    ],
                },
            },
        };

        BethesdaSemanticExtractionResult result = new BethesdaSemanticExtractor().Extract(tampered);

        Assert.AreEqual(BethesdaExtractionState.InvalidInput, result.State);
        Assert.IsNull(result.Snapshot);
        Assert.AreEqual("plugin-path-mismatch", result.Failures.Single().Code);
    }

    private static void AssertRejected(
        BethesdaSemanticExtractionResult result,
        ManagedBethesdaSemanticAssignment assignment) =>
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            BethesdaSemanticPublicationValidator.DeserializeAndValidate(
                JsonSerializer.SerializeToUtf8Bytes(result),
                assignment,
                16 * 1024 * 1024));
}
