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
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
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
        CollectionAssert.AreEqual(bytes, JsonSerializer.SerializeToUtf8Bytes(admitted));
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
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
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
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
    public void CoordinatorRejectsTamperedV2AvailabilityCoverageGapAndTaxonomyContracts()
    {
        BethesdaSemanticRequest request = BethesdaSemanticTestSnapshot.Create("BETH-NPC-DEV");
        BethesdaSemanticExtractionResult extracted = new BethesdaSemanticExtractor().Extract(request);
        BethesdaSemanticSnapshot snapshot = extracted.Snapshot!;
        ManagedBethesdaSemanticAssignment assignment = new(
            request.AcceptedSnapshot,
            [],
            snapshot.Plugins.Select(plugin => new ManagedBethesdaPluginSeal(
                plugin.PluginName,
                plugin.LoadOrder,
                plugin.SnapshotAuthorizedPath,
                plugin.ByteLength,
                plugin.Sha256.Value)).ToArray());

        AssertRejected(extracted with
        {
            Snapshot = snapshot with { SchemaVersion = new ContractVersion(1, 0, 0) },
        }, assignment);

        int faceGenIndex = snapshot.FaceGen.ToList().FindIndex(fact =>
            fact.Mesh.Availability == BethesdaAssetAvailability.Unknown);
        Assert.IsGreaterThanOrEqualTo(0, faceGenIndex);
        BethesdaFaceGenFact firstFaceGen = snapshot.FaceGen[faceGenIndex];

        int omittableFaceGenIndex = snapshot.FaceGen.ToList().FindIndex(fact =>
            fact.Applicability != BethesdaFaceGenApplicability.Applicable
            && fact.Mesh.ProviderParticipantIds.Count == 0
            && fact.Tint.ProviderParticipantIds.Count == 0);
        Assert.IsGreaterThanOrEqualTo(0, omittableFaceGenIndex);
        AssertRejected(extracted with
        {
            Snapshot = snapshot with
            {
                FaceGen =
                [
                    .. snapshot.FaceGen.Take(omittableFaceGenIndex),
                    .. snapshot.FaceGen.Skip(omittableFaceGenIndex + 1),
                ],
            },
        }, assignment);

        BethesdaFaceGenFact[] badFaceGen = [.. snapshot.FaceGen];
        badFaceGen[faceGenIndex] = firstFaceGen with
        {
            Mesh = firstFaceGen.Mesh with { Present = true, ExactAbsenceKnown = true },
        };
        AssertRejected(extracted with { Snapshot = snapshot with { FaceGen = badFaceGen } }, assignment);

        badFaceGen = [.. snapshot.FaceGen];
        badFaceGen[faceGenIndex] = firstFaceGen with
        {
            Mesh = firstFaceGen.Mesh with
            {
                Availability = BethesdaAssetAvailability.Present,
                Present = true,
            },
        };
        AssertRejected(extracted with { Snapshot = snapshot with { FaceGen = badFaceGen } }, assignment);

        badFaceGen = [.. snapshot.FaceGen];
        badFaceGen[faceGenIndex] = firstFaceGen with
        {
            Mesh = firstFaceGen.Mesh with { WinnerParticipantId = "undeclared-winner" },
        };
        AssertRejected(extracted with { Snapshot = snapshot with { FaceGen = badFaceGen } }, assignment);

        badFaceGen = [.. snapshot.FaceGen];
        badFaceGen[faceGenIndex] = firstFaceGen with
        {
            Mesh = firstFaceGen.Mesh with
            {
                Availability = BethesdaAssetAvailability.Absent,
                ExactAbsenceKnown = true,
            },
        };
        AssertRejected(extracted with { Snapshot = snapshot with { FaceGen = badFaceGen } }, assignment);

        AssertRejected(extracted with
        {
            Snapshot = snapshot with { Coverage = snapshot.Coverage.Skip(1).ToArray() },
        }, assignment);
        AssertRejected(extracted with
        {
            Snapshot = snapshot with { Coverage = [.. snapshot.Coverage, snapshot.Coverage[0]] },
        }, assignment);
        BethesdaCoveragePopulation[] duplicateCoverage = [.. snapshot.Coverage];
        duplicateCoverage[^1] = duplicateCoverage[0];
        AssertRejected(extracted with
        {
            Snapshot = snapshot with { Coverage = duplicateCoverage },
        }, assignment);
        BethesdaCoveragePopulation[] badArithmetic = [.. snapshot.Coverage];
        badArithmetic[0] = badArithmetic[0] with { Completed = badArithmetic[0].Denominator + 1 };
        AssertRejected(extracted with
        {
            Snapshot = snapshot with { Coverage = badArithmetic },
        }, assignment);
        int joinedRowIndex = snapshot.Coverage.ToList().FindIndex(row => row.GapIds.Count > 0);
        Assert.IsGreaterThanOrEqualTo(0, joinedRowIndex);
        BethesdaCoveragePopulation[] badJoin = [.. snapshot.Coverage];
        badJoin[0] = badJoin[0] with { GapIds = badJoin[joinedRowIndex].GapIds };
        badJoin[joinedRowIndex] = badJoin[joinedRowIndex] with { GapIds = [] };
        AssertRejected(extracted with
        {
            Snapshot = snapshot with { Coverage = badJoin },
        }, assignment);

        BethesdaCoverageGap firstGap = snapshot.Gaps[0];
        BethesdaCoverageGap[] badGaps = [.. snapshot.Gaps];
        badGaps[0] = firstGap with { MissingCapability = "invented-capability" };
        AssertRejected(extracted with
        {
            Gaps = badGaps,
            Snapshot = snapshot with { Gaps = badGaps },
        }, assignment);

        AssertRejected(extracted with
        {
            Snapshot = snapshot with { Taxonomy = [.. snapshot.Taxonomy, snapshot.Taxonomy[0]] },
        }, assignment);
        BethesdaTaxonomyProjection requiredCore = snapshot.Taxonomy.First(item =>
            item.SubjectType == "record-contribution"
            && item.Code == "surface.plugin-data");
        AssertRejected(extracted with
        {
            Snapshot = snapshot with
            {
                Taxonomy = snapshot.Taxonomy.Where(item => item != requiredCore).ToArray(),
            },
        }, assignment);
        BethesdaTaxonomyProjection malformedFaceGenSubject = snapshot.Taxonomy[0] with
        {
            AssignmentId = "taxonomy:malformed-facegen",
            SubjectParticipantId = "malformed-facegen-subject",
            SubjectType = "record-semantic-subject",
            Axis = "technical-modification-surface",
            Facet = "semantic-mechanism",
            Code = "surface.asset",
        };
        AssertRejected(extracted with
        {
            Snapshot = snapshot with { Taxonomy = [.. snapshot.Taxonomy, malformedFaceGenSubject] },
        }, assignment);

        BethesdaTaxonomyProjection semanticArea = snapshot.Taxonomy.First(item =>
            item.SubjectType == "record-semantic-subject"
            && item.Code?.StartsWith("area.", StringComparison.Ordinal) == true);
        BethesdaTaxonomyProjection unsupportedExtraClaim = semanticArea with
        {
            AssignmentId = "taxonomy:extra-claim-0001",
            Axis = "consequence-type",
            Facet = "consequence-type",
            Code = null,
            Applicability = TaxonomyApplicability.Unknown,
            Role = ClassificationRole.Predicted,
        };
        AssertRejected(extracted with
        {
            Snapshot = snapshot with { Taxonomy = [.. snapshot.Taxonomy, unsupportedExtraClaim] },
        }, assignment);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
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
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void MidReadIdentityChangeFailsWithoutPartialSemanticAuthority()
    {
        string source = TestRepository.PathFromRoot(
            "test-data", "public-fixtures", "bethesda", "BETH-MALFORMED-VAL",
            "inputs", "mutations", "ChangedDuringRead-A.esp");
        string alternate = TestRepository.PathFromRoot(
            "test-data", "public-fixtures", "bethesda", "BETH-MALFORMED-VAL",
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
    [TestCategory("Integration")]
    [TestCategory("Security")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Security")]
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
