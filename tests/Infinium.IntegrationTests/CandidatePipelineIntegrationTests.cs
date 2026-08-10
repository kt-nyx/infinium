using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidatePipelineIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestProperty("Category", "Integration")]
    public void CandidatePipelinePublishesCanonicalPayloadRowsAndDependencyEdges()
    {
        using CandidateStoreContext context = new();
        CandidateAnalysisPhaseResult phase = CandidateAnalysisPhase.Execute(
            context.Store,
            Request([Member("first"), Member("second", CandidateLane.MandatoryEvidence)]),
            context.Attempt,
            context.Binding,
            DateTimeOffset.UtcNow);

        byte[] readback = context.Store.ReadCandidateAnalysisPayload(phase.Receipt.PayloadId);
        CollectionAssert.AreEqual(phase.SerializedPayload, readback);
        CandidateAnalysisContract decoded = CandidateAnalysisJsonCodec.Deserialize(readback);
        Assert.AreEqual(phase.Pipeline.Analysis.PayloadId, decoded.PayloadId);
        Assert.AreEqual(2L, Count(context.Paths.Database, "candidate_decisions"));
        Assert.AreEqual(2L, Count(context.Paths.Database, "analysis_candidates"));
        Assert.AreEqual(2L, Count(context.Paths.Database, "analysis_hypotheses"));
        Assert.AreEqual(decoded.DependencyEdges.Count, Count(context.Paths.Database, "analysis_dependency_edges"));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Candidates")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Candidates")]
    public void CandidatePipelinePersistsAmbiguousLaneProvenanceAndUnsupportedAbstention()
    {
        using CandidateStoreContext context = new();
        CandidateAnalysisPhaseResult phase = CandidateAnalysisPhase.Execute(
            context.Store,
            Request(
            [
                Member("ambiguous", CandidateLane.DeterministicRequired,
                    inputState: CausalJoinInputState.Ambiguous, missing: ["resolved target"]),
                Member("unsupported", CandidateLane.MandatoryEvidence,
                    inputState: CausalJoinInputState.Unsupported, missing: ["supported substrate"]),
            ]),
            context.Attempt,
            context.Binding,
            DateTimeOffset.UtcNow);

        Assert.AreEqual(2, phase.Pipeline.Analysis.Abstentions.Count);
        using SqliteConnection connection = new($"Data Source={context.Paths.Database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT disposition || ':' || lane FROM candidate_decisions ORDER BY disposition;";
        using SqliteDataReader reader = command.ExecuteReader();
        List<string> rows = [];
        while (reader.Read())
        {
            rows.Add(reader.GetString(0));
        }
        Assert.AreEqual(
            "ambiguous:deterministic-required|unsupported:mandatory-evidence",
            string.Join('|', rows));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestProperty("Category", "Integration")]
    public void CandidatePersistenceScopesDecisionsByPopulationAndSharedEdgesByRun()
    {
        using CandidateStoreContext context = new();
        CausalJoinPopulationMember member = Member("shared-identity");
        CandidateAnalysisPhaseResult first = CandidateAnalysisPhase.Execute(
            context.Store, Request([member], populationId: "population-first"),
            context.Attempt, context.Binding, DateTimeOffset.UtcNow);
        CandidateAnalysisPhaseResult secondPopulation = CandidateAnalysisPhase.Execute(
            context.Store, Request([member], populationId: "population-second"),
            context.Attempt, context.Binding, DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.AreNotEqual(
            first.Pipeline.Analysis.Decisions.Single().DecisionId,
            secondPopulation.Pipeline.Analysis.Decisions.Single().DecisionId);

        const string otherRun = "run-candidate-other";
        AttemptRecord otherAttempt = context.CreateRunAttempt(otherRun, DateTimeOffset.UtcNow.AddSeconds(2));
        _ = CandidateAnalysisPhase.Execute(
            context.Store, Request([member], otherRun, "population-first"),
            otherAttempt, context.Binding, DateTimeOffset.UtcNow.AddSeconds(3));

        Assert.AreEqual(3L, Count(context.Paths.Database, "candidate_decisions"));
        CandidateDependencyEdgeContract closureEdge = first.Pipeline.Analysis.DependencyEdges.Single(item =>
            item.FromKind == "dependency-closure" && item.ToKind == "dependency");
        using SqliteConnection connection = new($"Data Source={context.Paths.Database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM analysis_dependency_edges WHERE dependency_edge_id = $edge;";
        command.Parameters.AddWithValue("$edge", closureEdge.EdgeId.Value);
        Assert.AreEqual(2L, Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture));
    }

    internal static CandidatePipelineRequest Request(
        IReadOnlyList<CausalJoinPopulationMember> members,
        string runId = "run-candidate",
        string populationId = "population-candidate")
    {
        OpaqueId analyzer = Id("analyzer-integration");
        TestCandidatePopulationSource source = new(analyzer, members);
        return new CandidatePipelineRequest(
            Id(runId), Id(populationId), Id("policy-candidate"), Id("threshold-candidate"),
            CandidateExecutionLimits.Default,
            new CandidatePopulationContext(
                null, Id(runId), Id("snapshot-candidate"), Id("context-candidate"), Id("config-candidate")),
            [source], ExecutionInput(source, runId));
    }

    internal static AnalysisExecutionInputContract ExecutionInput(
        ICandidatePopulationSource source,
        string runId = "run-candidate")
    {
        SemanticAnalysisContextContract context = SemanticContext("context-candidate");
        return new(
            ContractConstants.AnalysisExecutionInputSchemaId, new ContractVersion(1, 0, 0), Id("execution-input-" + runId),
            Id(runId), Reference("snapshot-candidate"), Reference("bethesda-candidate"), [],
            [new(source.AnalyzerId, new ContractVersion(1, 0, 0),
                CandidateAnalysisIdentity.StructuralHash([System.Text.Json.JsonSerializer.Serialize(source.Declaration)]), "retained")],
            Reference("config-candidate"), Reference("manifest-candidate"), ReplayMode.Clean, null, 0,
            new(1_000_000, 2_000_000, 100_000, 100_000, 120_000),
            [new("provider", BoundaryUseState.NotUsed, "local-only"),
             new("hosted-search", BoundaryUseState.NotUsed, "local-only"),
             new("nexus", BoundaryUseState.NotUsed, "local-only"),
             new("loot", BoundaryUseState.NotUsed, "local-only")])
        {
            AnalysisContext = new(context.ContextId, context.SchemaVersion, context.CanonicalFingerprint, "retained"),
        };
    }

    internal static SemanticAnalysisContextContract SemanticContext(string id)
    {
        SemanticAnalysisContextContract value = new(
            Id(id), new ContractVersion(1, 0, 0), new Sha256Fingerprint(new string('0', 64)),
            [], new Dictionary<string, string>());
        return value with { CanonicalFingerprint = SemanticAnalysisContextIdentity.ComputeFingerprint(value) };
    }

    private static ArtifactReferenceContract Reference(string id) => new(
        Id(id), new ContractVersion(1, 0, 0), new Sha256Fingerprint(new string('a', 64)), "retained");

    internal static CausalJoinPopulationMember Member(
        string id,
        CandidateLane lane = CandidateLane.DeterministicRequired,
        string rationale = "bounded relationship",
        CausalJoinInputState inputState = CausalJoinInputState.Complete,
        IReadOnlyList<string>? missing = null) => new(
            Id("member-" + id), Id("analyzer-integration"), lane,
            [new(Id("source-" + id), "source"), new(Id("target-" + id), "target")],
            "typed-causal-join", [Id("source-" + id), Id("evidence-" + id), Id("target-" + id)], [Id("dependency-" + id)],
            [Id("evidence-" + id)], [], missing ?? [], inputState, rationale,
            "The exact relationship may change downstream analysis of its retained participants.",
            lane == CandidateLane.OptionalRanked ? 1 : null)
        { SourceFactId = Id("fact-" + id) };

    internal static OpaqueId Id(string value) => new(value);

    internal static long Count(string database, string table)
    {
        using SqliteConnection connection = new($"Data Source={database};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }
}

[TestClass]
public sealed class CandidateCheckpointIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void StaleAttemptCannotPublishAnyCandidatePhaseState()
    {
        using CandidateStoreContext context = new();
        CandidatePipelineRequest request = Request([CandidatePipelineIntegrationTests.Member("first")]);
        _ = context.StartRecoveryAttempt(DateTimeOffset.UtcNow.AddSeconds(1));

        Assert.ThrowsExactly<InvalidOperationException>(() => CandidateAnalysisPhase.Execute(
            context.Store, request, context.Attempt, context.Binding, DateTimeOffset.UtcNow.AddSeconds(2)));
        Assert.AreEqual(0L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "candidate_decisions"));
        Assert.AreEqual(0L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "analysis_dependency_edges"));
        Assert.AreEqual(0L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "checkpoints"));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void CandidateCheckpointRetryUnderNewAttemptRetainsAttemptScopedIdentity()
    {
        using CandidateStoreContext context = new();
        CandidatePipelineRequest request = Request(
            [CandidatePipelineIntegrationTests.Member("first")]);
        CandidateAnalysisPhaseResult baseline = CandidateAnalysisPhase.Execute(
            context.Store, request, context.Attempt, context.Binding, DateTimeOffset.UtcNow);
        AttemptRecord recoveryAttempt = context.StartRecoveryAttempt(DateTimeOffset.UtcNow.AddSeconds(1));

        CandidateAnalysisPhaseResult recovered = CandidateAnalysisPhase.Execute(
            context.Store, request, recoveryAttempt, context.Binding,
            DateTimeOffset.UtcNow.AddSeconds(2), baseline.Pipeline.Checkpoint);

        Assert.AreNotEqual(baseline.CheckpointId, recovered.CheckpointId);
        Assert.AreEqual(1L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "candidate_decisions"));
        Assert.AreEqual(2L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "checkpoints"));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("Fault")]
    [TestProperty("Category", "Integration")]
    [TestProperty("Category", "Fault")]
    public void CandidateCheckpointReadbackReusesUnchangedAndInvalidatesOnlyRelevantInput()
    {
        using CandidateStoreContext context = new();
        CausalJoinPopulationMember first = CandidatePipelineIntegrationTests.Member("first");
        CausalJoinPopulationMember second = CandidatePipelineIntegrationTests.Member("second", CandidateLane.MandatoryEvidence);
        CandidatePipelineRequest baselineRequest = Request([first, second]);
        _ = CandidateAnalysisPhase.Execute(
            context.Store, baselineRequest, context.Attempt, context.Binding, DateTimeOffset.UtcNow);
        CandidateCheckpointState checkpoint = CandidateAnalysisPhase.ReadLatestCheckpoint(
            context.Store, "run-candidate")!;

        CandidatePipelineRequest changedRequest = Request(
            [first, second with { Rationale = "relevant relationship changed" }]);
        CandidateAnalysisPhaseResult restartedPhase = CandidateAnalysisPhase.Execute(
            context.Store, changedRequest, context.Attempt, context.Binding,
            DateTimeOffset.UtcNow.AddSeconds(1), checkpoint);
        CandidatePipelineResult restarted = restartedPhase.Pipeline;

        CollectionAssert.AreEquivalent(
            new[] { CandidatePipelineIntegrationTests.Id("member-first") },
            restarted.ReusedMemberIds.ToArray());
        CollectionAssert.AreEquivalent(
            new[] { CandidatePipelineIntegrationTests.Id("member-second") },
            restarted.RecomputedMemberIds.ToArray());
        Assert.AreEqual(3L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "candidate_decisions"));
        Assert.AreEqual(2L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "checkpoints"));

        _ = CandidateAnalysisPhase.Execute(
            context.Store, changedRequest, context.Attempt, context.Binding,
            DateTimeOffset.UtcNow.AddSeconds(2), restarted.Checkpoint);
        Assert.AreEqual(3L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "candidate_decisions"));
        Assert.AreEqual(2L, CandidatePipelineIntegrationTests.Count(context.Paths.Database, "checkpoints"));
    }

    private static CandidatePipelineRequest Request(IReadOnlyList<CausalJoinPopulationMember> members)
    {
        OpaqueId analyzer = CandidatePipelineIntegrationTests.Id("analyzer-integration");
        TestCandidatePopulationSource source = new(analyzer, members);
        return new CandidatePipelineRequest(
            CandidatePipelineIntegrationTests.Id("run-candidate"),
            CandidatePipelineIntegrationTests.Id("population-candidate"),
            CandidatePipelineIntegrationTests.Id("policy-candidate"),
            CandidatePipelineIntegrationTests.Id("threshold-candidate"),
            CandidateExecutionLimits.Default,
            new CandidatePopulationContext(
                null,
                CandidatePipelineIntegrationTests.Id("run-candidate"),
                CandidatePipelineIntegrationTests.Id("snapshot-candidate"),
                CandidatePipelineIntegrationTests.Id("context-candidate"),
                CandidatePipelineIntegrationTests.Id("config-candidate")),
            [source], CandidatePipelineIntegrationTests.ExecutionInput(source));
    }
}

internal sealed class CandidateStoreContext : IDisposable
{
    private readonly string root;
    private readonly long coordinatorFencingEpoch;
    private readonly bool preserveRoot;

    public CandidateStoreContext(
        string? rootOverride = null,
        TimeSpan? coordinatorLease = null,
        bool preserveRoot = false)
    {
        root = rootOverride ?? Path.Combine(Path.GetTempPath(), $"infinium-candidate-{Guid.NewGuid():N}");
        this.preserveRoot = preserveRoot;
        Paths = new StoragePaths(root);
        Store = new AuthoritativeStore(Paths);
        CoordinatorAuthority authority = Store.AcquireCoordinatorAuthority(
            "candidate-integration", DateTimeOffset.UtcNow, coordinatorLease ?? TimeSpan.FromMinutes(10));
        Authority = authority;
        coordinatorFencingEpoch = authority.FencingEpoch;
        Binding = new RunBinding("snapshot-candidate", "context-candidate", "config-candidate", "manifest-candidate");
        RunRecord queued = Store.CreateRun(
            "command-candidate", "run-candidate", Binding, authority.FencingEpoch, DateTimeOffset.UtcNow);
        _ = Store.Transition(
            "transition-candidate", queued.RunId, queued.Generation, LifecycleState.Running,
            authority.FencingEpoch, "candidate integration dispatch", DateTimeOffset.UtcNow);
        Attempt = Store.CreateAttempt(
            queued.RunId, authority.FencingEpoch, TimeSpan.FromMinutes(5), DateTimeOffset.UtcNow);
    }

    public StoragePaths Paths { get; }
    public AuthoritativeStore Store { get; }
    public RunBinding Binding { get; }
    public AttemptRecord Attempt { get; }
    public CoordinatorAuthority Authority { get; }

    public AttemptRecord StartRecoveryAttempt(DateTimeOffset now)
    {
        Store.SettleLiveAttempts("run-candidate", "interrupted-by-test-recovery", coordinatorFencingEpoch);
        return Store.CreateAttempt("run-candidate", coordinatorFencingEpoch, TimeSpan.FromMinutes(5), now);
    }

    public AttemptRecord CreateRunAttempt(string runId, DateTimeOffset now)
    {
        RunRecord queued = Store.CreateRun(
            "command-" + runId, runId, Binding, coordinatorFencingEpoch, now);
        _ = Store.Transition(
            "transition-" + runId, queued.RunId, queued.Generation, LifecycleState.Running,
            coordinatorFencingEpoch, "candidate integration dispatch", now);
        return Store.CreateAttempt(runId, coordinatorFencingEpoch, TimeSpan.FromMinutes(5), now);
    }

    public void Dispose()
    {
        Store.Dispose();
        Paths.Dispose();
        if (!preserveRoot && Directory.Exists(root))
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    Directory.Delete(root, recursive: true);
                    break;
                }
                catch (IOException) when (attempt < 4)
                {
                    if (attempt == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                    Thread.Sleep(25 * (attempt + 1));
                }
            }
        }
    }
}
