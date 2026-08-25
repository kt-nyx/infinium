using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Analysis.ScopeReversion;
using Infinium.Application.ScopeReversion;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ScopeReversionPersistenceIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    [TestCategory("Output")]
    [TestProperty("Category", "ScopeReversion")]
    public void CleanIncrementalRetainedReplayReopenAndArtifactReadbackPreserveExactBytes()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionPipelineResult prepared = ScopeReversionComposition.Execute(fixture.Request);
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> artifacts =
            ScopeReversionTestSupport.RetainedArtifacts(fixture.Request, prepared.Analysis);
        using TemporaryStore temporary = new();
        string payloadId;
        byte[] canonical;
        using (AuthoritativeStore store = temporary.Open())
        {
            Assert.AreEqual(AuthoritativeStore.CurrentSchemaVersion, store.GetSchemaVersion());
            ScopeReversionPersistencePhaseResult clean = ScopeReversionPersistencePhase.ExecuteAndPublish(
                store, fixture.Request, artifacts, DateTimeOffset.UtcNow, ScopeReversionReplayMode.Clean);
            payloadId = clean.Analysis.PayloadId.Value;
            canonical = clean.CanonicalJson;
            Assert.AreEqual("executed-and-published", clean.Disposition);
            Assert.AreEqual(artifacts.Count + 1, clean.Receipt.ArtifactIds.Count);
            Assert.IsTrue(clean.HumanSummary.Contains("candidates:", StringComparison.Ordinal));
            Assert.IsTrue(clean.HumanSummary.Contains("findings:", StringComparison.Ordinal));
            Assert.IsTrue(clean.HumanSummary.Contains("coverage:", StringComparison.Ordinal));
            Assert.IsTrue(clean.HumanSummary.Contains("external-boundaries:", StringComparison.Ordinal));
            foreach ((string id, ReadOnlyMemory<byte> bytes) in artifacts)
            {
                StringAssert.Contains(System.Text.Encoding.UTF8.GetString(bytes.Span), "\"member_id\"");
                CollectionAssert.AreEqual(bytes.ToArray(), store.GetScopeReversionArtifact(id), id);
            }

            ScopeReversionRetainedArtifact[] exactArtifacts = artifacts.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new ScopeReversionRetainedArtifact(
                    item.Key,
                    clean.Analysis.DependencyEdges.Any(edge => edge.ToId.Value == item.Key && edge.ToKind == "evidence")
                        ? "evidence" : "dependency",
                    item.Value))
                .ToArray();
            byte[] differentPayloadBytes = clean.CanonicalJson.ToArray();
            differentPayloadBytes[^1] ^= 1;
            Assert.ThrowsExactly<InvalidDataException>(() => store.PublishScopeReversionAnalysis(
                new ScopeReversionPublicationRequest(
                    clean.Analysis, differentPayloadBytes, exactArtifacts, DateTimeOffset.UtcNow)));
            ScopeReversionRetainedArtifact[] driftedArtifacts = exactArtifacts.ToArray();
            driftedArtifacts[0] = driftedArtifacts[0] with { Bytes = new byte[] { 1, 2, 3 } };
            Assert.ThrowsExactly<InvalidDataException>(() => store.PublishScopeReversionAnalysis(
                new ScopeReversionPublicationRequest(
                    clean.Analysis, clean.CanonicalJson, driftedArtifacts, DateTimeOffset.UtcNow)));

            OpaqueId secondRunId = new("scope-reversion-synthetic-run-second");
            ScopeReversionCompositionRequest secondRequest = fixture.Request with
            {
                OriginatingRunId = secondRunId,
                ExecutionInput = fixture.Request.ExecutionInput! with { RunId = secondRunId },
            };
            ScopeReversionPipelineResult secondPrepared = ScopeReversionComposition.Execute(secondRequest);
            ScopeReversionRetainedArtifact[] kindDriftedArtifacts = artifacts
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => new ScopeReversionRetainedArtifact(
                    item.Key,
                    secondPrepared.Analysis.DependencyEdges.Any(edge =>
                        edge.ToId.Value == item.Key && edge.ToKind == "evidence")
                            ? "evidence" : "dependency",
                    item.Value))
                .ToArray();
            kindDriftedArtifacts[0] = kindDriftedArtifacts[0] with
            {
                Kind = kindDriftedArtifacts[0].Kind == "evidence" ? "dependency" : "evidence",
            };
            Assert.ThrowsExactly<InvalidDataException>(() => store.PublishScopeReversionAnalysis(
                new ScopeReversionPublicationRequest(
                    secondPrepared.Analysis, secondPrepared.CanonicalJson,
                    kindDriftedArtifacts, DateTimeOffset.UtcNow)));
            Assert.AreEqual(0, store.ListScopeReversionArtifacts(secondPrepared.Analysis.PayloadId.Value).Count);

            ScopeReversionPersistencePhaseResult incremental = ScopeReversionPersistencePhase.ExecuteAndPublish(
                store, fixture.Request, artifacts, DateTimeOffset.UtcNow,
                ScopeReversionReplayMode.Incremental, payloadId);
            Assert.AreEqual("reused-incremental", incremental.Disposition);
            CollectionAssert.AreEqual(canonical, incremental.CanonicalJson);
            Assert.AreEqual(clean.Receipt.SemanticFingerprint, incremental.Receipt.SemanticFingerprint);
            ScopeReversionPersistencePhaseResult retained = ScopeReversionPersistencePhase.ExecuteAndPublish(
                store, fixture.Request, artifacts, DateTimeOffset.UtcNow,
                ScopeReversionReplayMode.RetainedDownstream, payloadId);
            Assert.AreEqual("reused-retained-downstream", retained.Disposition);
            CollectionAssert.AreEqual(canonical, retained.CanonicalJson);
            Assert.AreEqual(clean.Receipt.SemanticFingerprint, retained.Receipt.SemanticFingerprint);
            Dictionary<string, ReadOnlyMemory<byte>> driftedReplayArtifacts =
                new(artifacts, StringComparer.Ordinal);
            string driftedReplayId = driftedReplayArtifacts.Keys.First();
            driftedReplayArtifacts[driftedReplayId] = new byte[] { 1, 2, 3 };
            Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionPersistencePhase.ExecuteAndPublish(
                store, fixture.Request, driftedReplayArtifacts, DateTimeOffset.UtcNow,
                ScopeReversionReplayMode.Incremental, payloadId));
            Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionPersistencePhase.ExecuteAndPublish(
                store, fixture.Request, artifacts, DateTimeOffset.UtcNow,
                ScopeReversionReplayMode.RetainedDownstream));
        }

        using AuthoritativeStore reopened = temporary.Open();
        CollectionAssert.AreEqual(canonical, reopened.ReadScopeReversionAnalysisBytes(payloadId));
        ScopeReversionAnalysisContract readback = ScopeReversionPersistencePhase.ReadValidated(reopened, payloadId);
        Assert.AreEqual(payloadId, readback.PayloadId.Value);
        Assert.AreEqual(2, readback.Findings.Count);
        Assert.AreEqual(2, readback.Cases.Count);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    [TestProperty("Category", "ScopeReversion")]
    public void DependencyInvalidationIsLocalAndRelevantWinnerChangeProducesNewSemanticProjection()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionPipelineResult prepared = ScopeReversionComposition.Execute(fixture.Request);
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> artifacts =
            ScopeReversionTestSupport.RetainedArtifacts(fixture.Request, prepared.Analysis);
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        ScopeReversionPersistencePhaseResult first = ScopeReversionPersistencePhase.ExecuteAndPublish(
            store, fixture.Request, artifacts, DateTimeOffset.UtcNow);

        Assert.AreEqual(0, store.InvalidateScopeReversionDependency(
            "unrelated-dependency", "unrelated change", DateTimeOffset.UtcNow).Count);
        string relevant = fixture.Request.ActorInputs.Single(item => item.MemberId.Value == "actor-positive")
            .DependencyIds[0].Value;
        IReadOnlyList<ScopeReversionInvalidationRecord> invalidated = store.InvalidateScopeReversionDependency(
            relevant, "relevant winner changed", DateTimeOffset.UtcNow);
        Assert.AreEqual(1, invalidated.Count);
        Assert.AreEqual(first.Analysis.PayloadId.Value, invalidated[0].PayloadId);

        ActorScopeReversionInput actor = fixture.Request.ActorInputs.Single(item => item.MemberId.Value == "actor-positive");
        ScopeReversionCompositionRequest changedRequest = fixture.Request with
        {
            ActorInputs = fixture.Request.ActorInputs.Select(item => item.MemberId == actor.MemberId
                ? item with
                {
                    WinningPackageId = item.PriorPackageId,
                    WinningContributionId = new OpaqueId(item.WinningContributionId.Value + "-v2"),
                    DependencyIds = item.DependencyIds.Select(id => new OpaqueId(id.Value + "-v2")).ToArray(),
                    EvidenceIds = item.EvidenceIds.Select(id => new OpaqueId(id.Value + "-v2")).ToArray(),
                }
                : item).ToArray(),
        };
        ScopeReversionPipelineResult changedPrepared = ScopeReversionComposition.Execute(changedRequest);
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> changedArtifacts =
            ScopeReversionTestSupport.RetainedArtifacts(changedRequest, changedPrepared.Analysis);
        ScopeReversionPersistencePhaseResult changed = ScopeReversionPersistencePhase.ExecuteAndPublish(
            store, changedRequest, changedArtifacts, DateTimeOffset.UtcNow,
            ScopeReversionReplayMode.Incremental, first.Analysis.PayloadId.Value);
        Assert.AreEqual("executed-and-published", changed.Disposition);
        Assert.AreNotEqual(first.Analysis.PayloadId, changed.Analysis.PayloadId);
        Assert.AreEqual(ScopeReversionDisposition.ResolvedNegative,
            changed.Analysis.Decisions.Single(item => item.MemberId.Value == "actor-positive").Disposition);
        Assert.AreEqual(1, changed.Analysis.Counts.SupportedFindings);
        Assert.AreEqual(3, changed.Analysis.Counts.ResolvedNegative);
        Assert.AreEqual(2, first.Analysis.Counts.SupportedFindings);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    [TestProperty("Category", "ScopeReversion")]
    public void BackupRestorePreservesScopeReversionPayloadsDependenciesAndInvalidations()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionPipelineResult prepared = ScopeReversionComposition.Execute(fixture.Request);
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> artifacts =
            ScopeReversionTestSupport.RetainedArtifacts(fixture.Request, prepared.Analysis);
        using TemporaryStore source = new();
        BackupArtifact backup;
        string payloadId;
        using (AuthoritativeStore store = source.Open())
        {
            ScopeReversionPersistencePhaseResult published = ScopeReversionPersistencePhase.ExecuteAndPublish(
                store, fixture.Request, artifacts, DateTimeOffset.UtcNow);
            payloadId = published.Analysis.PayloadId.Value;
            string dependency = fixture.Request.ReferenceInputs[0].DependencyIds[0].Value;
            _ = store.InvalidateScopeReversionDependency(dependency, "backup invalidation", DateTimeOffset.UtcNow);
            backup = store.CreateBackup("ScopeReversion", DateTimeOffset.UtcNow);
        }

        using TemporaryStore restored = new();
        using (StoragePaths paths = new(restored.Root))
        {
            AuthoritativeStore.RestoreBackup(backup, paths);
        }
        using AuthoritativeStore reopened = restored.Open();
        ScopeReversionAnalysisContract analysis = ScopeReversionPersistencePhase.ReadValidated(reopened, payloadId);
        Assert.AreEqual(2, analysis.Findings.Count);
        Assert.AreEqual(1, reopened.ReadScopeReversionInvalidations(payloadId).Count);
        foreach ((string id, ReadOnlyMemory<byte> bytes) in artifacts)
        {
            CollectionAssert.AreEqual(bytes.ToArray(), reopened.GetScopeReversionArtifact(id), id);
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Replay")]
    [TestProperty("Category", "ScopeReversion")]
    public void DanglingRetainedDependencyIsRejectedBeforePublication()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionPipelineResult prepared = ScopeReversionComposition.Execute(fixture.Request);
        Dictionary<string, ReadOnlyMemory<byte>> artifacts = new(
            ScopeReversionTestSupport.RetainedArtifacts(fixture.Request, prepared.Analysis), StringComparer.Ordinal);
        artifacts.Remove(artifacts.Keys.First());
        using TemporaryStore temporary = new();
        using AuthoritativeStore store = temporary.Open();
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionPersistencePhase.ExecuteAndPublish(
            store, fixture.Request, artifacts, DateTimeOffset.UtcNow));
        Assert.AreEqual(0, store.ListScopeReversionArtifacts(prepared.Analysis.PayloadId.Value).Count);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Output")]
    [TestCategory("Cli")]
    [TestProperty("Category", "ScopeReversion")]
    public void ScopeResultsCliPublishesCanonicalJsonAndCompleteHumanState()
    {
        ScopeReversionPipelineResult prepared = ScopeReversionComposition.Execute(
            ScopeReversionTestSupport.Fixture().Request);
        string directory = Path.Combine(Path.GetTempPath(), $"infinium-scope-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "scope-reversion-analysis.v1.json");
        try
        {
            File.WriteAllBytes(path, prepared.CanonicalJson);
            ProcessResult json = TestProcessRunner.RunDotnetProject(
                "src/Infinium.Cli", ["scope-results", path, "--json"], 30_000,
                "The scope-reversion JSON CLI command exceeded its process bound.");
            Assert.AreEqual(0, json.ExitCode, json.Error);
            CollectionAssert.AreEqual(
                prepared.CanonicalJson,
                System.Text.Encoding.UTF8.GetBytes(json.Output.TrimEnd('\r', '\n')));

            ProcessResult human = TestProcessRunner.RunDotnetProject(
                "src/Infinium.Cli", ["scope-results", path], 30_000,
                "The scope-reversion human CLI command exceeded its process bound.");
            Assert.AreEqual(0, human.ExitCode, human.Error);
            foreach (string required in new[]
            {
                "candidates:", "negative-decisions:", "abstentions:", "failures:",
                "gaps:", "findings:", "taxonomy:", "cases:", "recommendations:",
                "coverage:", "retained-artifact-references:", "claim-boundary:", "external-boundaries:",
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

    private sealed class TemporaryStore : IDisposable
    {
        public TemporaryStore()
        {
            Root = Path.Combine(Path.GetTempPath(), $"infinium-scope-reversion-{Guid.NewGuid():N}");
        }

        public string Root { get; }

        public AuthoritativeStore Open() => new(new StoragePaths(Root));

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }

}
