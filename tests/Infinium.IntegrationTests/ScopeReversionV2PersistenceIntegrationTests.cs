using System.Text;
using Infinium.Application.ScopeReversion;
using Infinium.Persistence;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ScopeReversionV2PersistenceIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void CurrentSchemaPublishesReopensReplaysInvalidatesAndDisclosesUnavailableRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-scope-reversion-v2-store-" + Guid.NewGuid().ToString("N"));
        string restoredRoot = Path.Combine(Path.GetTempPath(), "infinium-scope-reversion-v2-restored-" + Guid.NewGuid().ToString("N"));
        ScopeReversionV2ProjectionRequest request = ScopeReversionV2TestSupport.Request();
        Dictionary<string, ReadOnlyMemory<byte>> artifacts = new(StringComparer.Ordinal)
        {
            ["artifact-bethesda-structural"] = Encoding.UTF8.GetBytes("project-authored structural facts"),
            ["artifact-source-application"] = Encoding.UTF8.GetBytes("project-authored source application facts"),
        };
        string payloadId;
        byte[] canonical;
        BackupArtifact backup;
        try
        {
            using (AuthoritativeStore store = new(new StoragePaths(root)))
            {
                Assert.AreEqual(14, store.GetSchemaVersion());
                Assert.AreEqual(ResultsReviewPersistenceDeclarations.SchemaFingerprint,
                    store.GetCurrentSchemaFingerprint());
                ScopeReversionV2PersistencePhaseResult clean = ScopeReversionV2PersistencePhase.ExecuteAndPublish(
                    store, request, artifacts, DateTimeOffset.UtcNow);
                payloadId = clean.Analysis.PayloadId.Value;
                canonical = clean.CanonicalJson;
                Assert.AreEqual(request.InputHandoffId, clean.Receipt.InputHandoffId);
                CollectionAssert.AreEqual(request.PublicManifests.ToArray(), clean.Receipt.PublicManifests.ToArray());
                CollectionAssert.AreEqual(request.ControlledInputs.ToArray(), clean.Receipt.ControlledInputs.ToArray());
                CollectionAssert.AreEqual(canonical, store.ReadScopeReversionV2AnalysisBytes(payloadId));

                ScopeReversionV2PersistencePhaseResult incremental = ScopeReversionV2PersistencePhase.ExecuteAndPublish(
                    store, request, artifacts, DateTimeOffset.UtcNow,
                    ScopeReversionV2ReplayMode.Incremental, payloadId);
                Assert.AreEqual("reused-incremental", incremental.ExecutionState);
                ScopeReversionV2PersistencePhaseResult retained = ScopeReversionV2PersistencePhase.ExecuteAndPublish(
                    store, request, artifacts, DateTimeOffset.UtcNow,
                    ScopeReversionV2ReplayMode.RetainedDownstream, payloadId);
                Assert.AreEqual("reproduced-retained-downstream", retained.ExecutionState);

                IReadOnlyList<ScopeReversionV2InvalidationRecord> invalidated =
                    store.InvalidateScopeReversionV2Dependency(
                        "artifact-source-application", "source application changed", DateTimeOffset.UtcNow);
                Assert.HasCount(1, invalidated);
                Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionV2PersistencePhase.ExecuteAndPublish(
                    store, request, artifacts, DateTimeOffset.UtcNow,
                    ScopeReversionV2ReplayMode.RetainedDownstream, payloadId));
                ScopeReversionV2PersistencePhaseResult audit =
                    ScopeReversionV2PersistencePhase.ReadAuditOnlyUnavailable(store, payloadId);
                Assert.AreEqual(ScopeReversionV2CleanReplayAvailability.Unavailable,
                    audit.CleanReplayAvailability);
                StringAssert.Contains(audit.HumanSummary, "Clean replay: Unavailable");
                backup = store.CreateBackup("ScopeReversionV2", DateTimeOffset.UtcNow);
            }

            using AuthoritativeStore reopened = new(new StoragePaths(root));
            CollectionAssert.AreEqual(canonical, reopened.ReadScopeReversionV2AnalysisBytes(payloadId));
            Assert.HasCount(1, reopened.ReadScopeReversionV2Invalidations(payloadId));
            reopened.Dispose();

            using (StoragePaths restoredPaths = new(restoredRoot))
            {
                AuthoritativeStore.RestoreBackup(backup, restoredPaths);
            }
            using AuthoritativeStore restored = new(new StoragePaths(restoredRoot));
            CollectionAssert.AreEqual(canonical, restored.ReadScopeReversionV2AnalysisBytes(payloadId));
            Assert.HasCount(1, restored.ReadScopeReversionV2Invalidations(payloadId));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
            if (Directory.Exists(restoredRoot))
            {
                Directory.Delete(restoredRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Migration")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void V1PublicationAndReadRemainExactUnderSchema12()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-scope-reversion-v1-preservation-" + Guid.NewGuid().ToString("N"));
        try
        {
            ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
            ScopeReversionPipelineResult prepared = ScopeReversionComposition.Execute(fixture.Request);
            IReadOnlyDictionary<string, ReadOnlyMemory<byte>> artifacts =
                ScopeReversionTestSupport.RetainedArtifacts(fixture.Request, prepared.Analysis);
            using AuthoritativeStore store = new(new StoragePaths(root));
            ScopeReversionPersistencePhaseResult published = ScopeReversionPersistencePhase.ExecuteAndPublish(
                store, fixture.Request, artifacts, DateTimeOffset.UtcNow);
            CollectionAssert.AreEqual(prepared.CanonicalJson,
                store.ReadScopeReversionAnalysisBytes(published.Analysis.PayloadId.Value));
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
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void CorruptBytesAndInvalidArtifactsPublishNothingWhileConcurrentDuplicatePublishIsStable()
    {
        string root = Path.Combine(Path.GetTempPath(), "infinium-scope-reversion-v2-atomic-" + Guid.NewGuid().ToString("N"));
        try
        {
            ScopeReversionV2PipelineResult prepared = ControlledRealScopeReversionProjector.Execute(
                ScopeReversionV2TestSupport.Request());
            ScopeReversionV2RetainedArtifact[] artifacts =
            [
                new("artifact-structural", "retained-project-artifact", Encoding.UTF8.GetBytes("structure")),
                new("artifact-source", "retained-project-artifact", Encoding.UTF8.GetBytes("source")),
            ];
            using AuthoritativeStore store = new(new StoragePaths(root));
            byte[] corrupt = prepared.CanonicalJson.ToArray();
            corrupt[^1] ^= 1;
            Assert.ThrowsExactly<InvalidDataException>(() => store.PublishScopeReversionV2Analysis(
                new ScopeReversionV2PublicationRequest(prepared.Analysis, corrupt, artifacts, DateTimeOffset.UtcNow)));
            Assert.ThrowsExactly<InvalidDataException>(() => store.PublishScopeReversionV2Analysis(
                new ScopeReversionV2PublicationRequest(prepared.Analysis, prepared.CanonicalJson,
                    [artifacts[0], artifacts[0]], DateTimeOffset.UtcNow)));
            Assert.ThrowsExactly<KeyNotFoundException>(() =>
                store.ReadScopeReversionV2AnalysisBytes(prepared.Analysis.PayloadId.Value));

            ScopeReversionV2PersistenceReceipt[] receipts = new ScopeReversionV2PersistenceReceipt[8];
            Parallel.For(0, receipts.Length, index =>
            {
                receipts[index] = store.PublishScopeReversionV2Analysis(
                    new ScopeReversionV2PublicationRequest(
                        prepared.Analysis, prepared.CanonicalJson, artifacts, DateTimeOffset.UtcNow));
            });
            Assert.AreEqual(1, receipts.Select(item => item.PayloadSha256).Distinct(StringComparer.Ordinal).Count());
            CollectionAssert.AreEqual(prepared.CanonicalJson,
                store.ReadScopeReversionV2AnalysisBytes(prepared.Analysis.PayloadId.Value));
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
    [TestCategory("Integration")]
    [TestCategory("Output")]
    [TestCategory("Cli")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void ScopeResultsCliAutoDetectsV2AndPublishesCanonicalAndHumanOutput()
    {
        ScopeReversionV2PipelineResult prepared = ControlledRealScopeReversionProjector.Execute(
            ScopeReversionV2TestSupport.Request());
        string directory = Path.Combine(Path.GetTempPath(), "infinium-scope-reversion-v2-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "scope-reversion-analysis.v2.json");
        try
        {
            File.WriteAllBytes(path, prepared.CanonicalJson);
            ProcessResult json = TestProcessRunner.RunDotnetProject(
                "src/Infinium.Cli", ["scope-results", path, "--json"], 30_000,
                "The scope-reversion v2 JSON CLI command exceeded its process bound.");
            Assert.AreEqual(0, json.ExitCode, json.Error);
            CollectionAssert.AreEqual(
                prepared.CanonicalJson,
                Encoding.UTF8.GetBytes(json.Output.TrimEnd('\r', '\n')));

            ProcessResult human = TestProcessRunner.RunDotnetProject(
                "src/Infinium.Cli", ["scope-results", path], 30_000,
                "The scope-reversion v2 human CLI command exceeded its process bound.");
            Assert.AreEqual(0, human.ExitCode, human.Error);
            foreach (string required in new[]
            {
                "Infinium scope-reversion analysis v2", "Snapshot/context/configuration/input:",
                "hypotheses:", "Coverage:", "External/prohibited boundaries: NotUsed", "Claim boundary:",
            })
            {
                StringAssert.Contains(human.Output, required);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
