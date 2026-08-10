using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateSelectionEvaluationTests
{
    private const string FixtureRoot = "fixtures/public/candidates";

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Candidates")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Candidates")]
    public void SemanticPackageMatchesTheFrozenIndependentProjectionExactly()
    {
        CandidateFixture fixture = ReadFixture("CAND-SEMANTIC-DEV-v1");
        Assert.IsNotNull(fixture.Package.DeliveredInput);

        CandidatePipelineResult result = Execute(fixture.Package);
        AssertProjection(fixture.Projection.RootElement, fixture.Package.Package.Oracle, result);
        AssertSemanticMetamorphs(fixture.Package.DeliveredInput, result);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("CandidateScale")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "CandidateScale")]
    public void ValidationScaleUsesTheRealSourceAndStaysWithinThePublicationBoundary()
    {
        CandidateFixture fixture = ReadFixture("CAND-SCALE-VAL-v1");
        CandidateDeliveredExpansionContract expansion = fixture.Package.DeliveredExpansion
            ?? throw new AssertFailedException("The validation-scale package must retain an expansion.");
        JsonElement projection = fixture.Projection.RootElement;

        CandidateDeliveredExpansionMeasurement measurement = CandidateDeliveredInputExpander.Measure(expansion);
        JsonElement receipt = projection.GetProperty("factual_stream_receipt");
        Assert.AreEqual(projection.GetProperty("semantic_population_summary").GetProperty("factual_rows").GetInt64(), measurement.TotalFacts);
        Assert.AreEqual(receipt.GetProperty("sha256").GetString(), measurement.FactStreamFingerprint.Value);

        CandidatePipelineResult result = Execute(fixture.Package);
        AssertProjection(projection, fixture.Package.Package.Oracle, result);
        byte[] aggregate = CandidateAnalysisJsonCodec.Serialize(result.Analysis);
        long maximum = projection.GetProperty("aggregate_boundary").GetProperty("maximum_bytes").GetInt64();
        Assert.IsLessThanOrEqualTo(maximum, aggregate.LongLength);
        Assert.IsGreaterThan(0L, aggregate.LongLength);
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("CandidateScale")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "CandidateScale")]
    public void StressPackageStreamsTheSameRecipeWithoutMaterializingThePopulation()
    {
        CandidateFixture scale = ReadFixture("CAND-SCALE-VAL-v1");
        CandidateFixture stress = ReadFixture("CAND-STRESS-DEV-v1", "oracle/streaming-expansion-receipt.json");
        CandidateDeliveredExpansionContract scaleExpansion = scale.Package.DeliveredExpansion!;
        CandidateDeliveredExpansionContract stressExpansion = stress.Package.DeliveredExpansion!;

        Assert.IsTrue(SameRecipe(scaleExpansion, stressExpansion));
        CandidateDeliveredExpansionMeasurement measurement = CandidateDeliveredInputExpander.Measure(stressExpansion);
        JsonElement projection = stress.Projection.RootElement;
        Assert.AreEqual(projection.GetProperty("semantic_population_summary").GetProperty("factual_rows").GetInt64(), measurement.TotalFacts);
        Assert.AreEqual(projection.GetProperty("factual_stream_receipt").GetProperty("sha256").GetString(),
            measurement.FactStreamFingerprint.Value);
        Assert.AreEqual(1_000_000L, measurement.TotalFacts);
        Assert.ThrowsExactly<InvalidOperationException>(() => CandidateDeliveredInputExpander.Expand(stressExpansion));
    }

    private static CandidateFixture ReadFixture(string directoryName, string projectionPath = "oracle/semantic-population-projection.json")
    {
        string root = FindRepositoryRoot();
        string directory = Path.Combine(root, FixtureRoot.Replace('/', Path.DirectorySeparatorChar), directoryName);
        CandidatePublicFixturePackage package = CandidateFixturePackageReader.Read(directory);
        JsonDocument projection = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
            directory, projectionPath.Replace('/', Path.DirectorySeparatorChar))));
        return new(package, projection);
    }

    private static CandidatePipelineResult Execute(CandidatePublicFixturePackage package)
    {
        CandidateDeliveredInputContract? input = package.DeliveredInput;
        CandidateDeliveredExpansionContract? expansion = package.DeliveredExpansion;
        OpaqueId run = input?.OriginatingRunId ?? expansion!.OriginatingRunId;
        OpaqueId snapshot = input?.SourceSnapshotId ?? expansion!.SourceSnapshotId;
        OpaqueId contextId = input?.AnalysisContextId ?? expansion!.AnalysisContextId;
        OpaqueId configuration = input?.ConfigurationId ?? expansion!.ConfigurationId;
        OpaqueId artifactId = input?.PayloadId ?? expansion!.ExpansionId;
        byte[] bytes = input is null
            ? CandidateDeliveredExpansionJsonCodec.Serialize(expansion!)
            : CandidateDeliveredInputJsonCodec.Serialize(input);
        Sha256Fingerprint artifactFingerprint = Fingerprint(bytes);
        OpaqueId deliveredRootId = input?.PayloadId
            ?? CandidateDeliveredInputExpander.Expand(expansion!).PayloadId;

        DeliveredIndexCandidatePopulationSource source = new();
        Sha256Fingerprint analyzerFingerprint = CandidateAnalysisIdentity.StructuralHash(
            [JsonSerializer.Serialize(source.Declaration)]);
        AnalysisExecutionInputContract executionInput = new(
            ContractConstants.AnalysisExecutionInputSchemaId, new ContractVersion(1, 0, 0),
            new OpaqueId($"execution-{package.Package.FixtureId.Value}"), run,
            Reference(snapshot, new string('1', 64)), Reference(new OpaqueId("bethesda-candidate-fixture"), new string('2', 64)),
            [new(artifactId, CandidateDeliveredInputIdentity.Version, artifactFingerprint, "retained")],
            [new(source.AnalyzerId, source.Declaration.AnalyzerVersion, analyzerFingerprint, "retained")],
            Reference(configuration, new string('3', 64)), Reference(new OpaqueId("manifest-candidate-fixture"), new string('4', 64)),
            ReplayMode.Clean, null, 0, new(10_000, 500_000, 100_000, 100_000, 120_000), Boundaries());
        CandidatePopulationContext context = new(
            null, run, snapshot, contextId, configuration, input, input is null ? null : artifactFingerprint,
            expansion, expansion is null ? null : artifactFingerprint, deliveredRootId);
        return CandidatePipeline.Execute(new(
            run, new OpaqueId($"population-{package.Package.FixtureId.Value}"),
            new OpaqueId("candidate-policy-v1"), new OpaqueId("candidate-threshold-v1"),
            new CandidateExecutionLimits(new OpaqueId("candidate-limits-fixture"), 100_000, 100_000),
            context, [source], executionInput));
    }

    private static void AssertProjection(JsonElement projection, JsonElement oracle, CandidatePipelineResult result)
    {
        JsonElement[] expected = projection.GetProperty("member_projection").EnumerateArray().ToArray();
        Dictionary<string, CandidateDecisionContract> decisions = result.Analysis.Decisions
            .ToDictionary(item => item.SourceFactId.Value, StringComparer.Ordinal);
        Assert.AreEqual(expected.Length, decisions.Count);

        Dictionary<OpaqueId, CandidateDecisionContract> decisionsById = result.Analysis.Decisions.ToDictionary(item => item.DecisionId);
        Dictionary<string, CandidateAnalysisEntryContract> candidatesByFact = result.Analysis.Candidates
            .ToDictionary(item => decisionsById[item.DecisionId].SourceFactId.Value, StringComparer.Ordinal);
        HashSet<string> candidateFacts = candidatesByFact.Keys.ToHashSet(StringComparer.Ordinal);
        Dictionary<OpaqueId, CandidateAnalysisEntryContract> candidatesById = result.Analysis.Candidates.ToDictionary(item => item.CandidateId);
        Dictionary<string, CandidateHypothesisContract> hypothesesByFact = result.Analysis.Hypotheses
            .ToDictionary(item => decisionsById[candidatesById[item.CandidateId].DecisionId].SourceFactId.Value, StringComparer.Ordinal);
        HashSet<string> hypothesisFacts = hypothesesByFact.Keys.ToHashSet(StringComparer.Ordinal);
        Dictionary<string, CandidateAbstentionContract> abstentionsByFact = result.Analysis.Abstentions
            .ToDictionary(item => decisionsById[item.DecisionId].SourceFactId.Value, StringComparer.Ordinal);
        HashSet<string> abstentionFacts = abstentionsByFact.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (JsonElement item in expected)
        {
            string sourceFactId = item.GetProperty("source_fact_id").GetString()!;
            Assert.IsTrue(decisions.TryGetValue(sourceFactId, out CandidateDecisionContract? decision), sourceFactId);
            Assert.AreEqual(item.GetProperty("lane").GetString(), Lane(decision.Lane), sourceFactId);
            Assert.AreEqual(item.GetProperty("expected_disposition").GetString(), Disposition(decision.Disposition), sourceFactId);
            bool candidateExpected = item.GetProperty("candidate_membership").GetBoolean();
            Assert.AreEqual(candidateExpected, candidateFacts.Contains(sourceFactId), $"candidate {sourceFactId}");
            Assert.AreEqual(candidateExpected, hypothesisFacts.Contains(sourceFactId), $"hypothesis {sourceFactId}");
            Assert.AreEqual(item.GetProperty("abstention_state").GetString() != "absent",
                abstentionFacts.Contains(sourceFactId), $"abstention {sourceFactId}");
            if (candidateExpected)
            {
                string hypothesisState = item.GetProperty("hypothesis_state").GetString()!;
                Assert.AreEqual(hypothesisState == "needs-input" ? AnalysisResultState.Abstained : AnalysisResultState.Present,
                    candidatesByFact[sourceFactId].State, $"candidate state {sourceFactId}");
                Assert.AreEqual(hypothesisState == "needs-input" ? AnalysisResultState.Partial : AnalysisResultState.Present,
                    hypothesesByFact[sourceFactId].State, $"hypothesis state {sourceFactId}");
            }
            if (decision.Lane == CandidateLane.OptionalRanked)
            {
                JsonElement facts = item.GetProperty("factual_projection");
                long expectedRank = checked(1L + (100L - facts.GetProperty("locality").GetInt32()) * 101L
                    + (100L - facts.GetProperty("specificity").GetInt32()));
                Assert.AreEqual(expectedRank, decision.OptionalRank, $"optional rank {sourceFactId}");
            }
            Assert.IsTrue(result.Analysis.DependencyEdges.Any(edge => edge.FromId == decision.DecisionId
                && edge.ToKind == "source-fact" && edge.ToId == decision.SourceFactId && edge.EdgeKind == "derived-from"),
                $"derived-from {sourceFactId}");
        }

        JsonElement summary = projection.GetProperty("semantic_population_summary");
        Assert.AreEqual(summary.GetProperty("factual_rows").GetInt64(), result.Analysis.Counts.Population);
        Assert.AreEqual(summary.GetProperty("candidates").GetInt64(), result.Analysis.Candidates.Count);
        Assert.AreEqual(summary.GetProperty("hypotheses").GetInt64(), result.Analysis.Hypotheses.Count);
        Assert.AreEqual(summary.GetProperty("abstentions").GetInt64(), result.Analysis.Abstentions.Count);
        foreach (JsonProperty property in summary.GetProperty("by_disposition").EnumerateObject())
        {
            Assert.AreEqual(property.Value.GetInt64(), result.Analysis.Decisions.LongCount(item => Disposition(item.Disposition) == property.Name));
        }
        foreach (JsonProperty property in summary.GetProperty("by_lane").EnumerateObject())
        {
            Assert.AreEqual(property.Value.GetInt64(), result.Analysis.Decisions.LongCount(item => Lane(item.Lane) == property.Name));
        }

        string[] expectedOptional = projection.GetProperty("optional_rank_source_fact_ids").EnumerateArray()
            .Select(item => item.GetString()!).ToArray();
        string[] actualOptional = result.Analysis.Decisions
            .Where(item => item.Lane == CandidateLane.OptionalRanked
                && item.Disposition is CandidateDecisionDisposition.CandidateAdmitted
                    or CandidateDecisionDisposition.Ambiguous)
            .OrderBy(item => item.OptionalRank).ThenBy(item => item.SourceFactId.Value, StringComparer.Ordinal)
            .Select(item => item.SourceFactId.Value).ToArray();
        CollectionAssert.AreEquivalent(expectedOptional, actualOptional);
        CollectionAssert.AreEquivalent(decisions.Keys.ToArray(), OracleSubjects(oracle, "expected_candidate_decisions"));
        CollectionAssert.AreEquivalent(candidateFacts.ToArray(), OracleSubjects(oracle, "expected_candidates"));
        CollectionAssert.AreEquivalent(hypothesisFacts.ToArray(), OracleSubjects(oracle, "expected_hypotheses"));
        CollectionAssert.AreEquivalent(abstentionFacts.ToArray(), OracleSubjects(oracle, "expected_abstentions"));
    }

    private static void AssertSemanticMetamorphs(CandidateDeliveredInputContract baseline, CandidatePipelineResult baselineResult)
    {
        Dictionary<string, CandidateDecisionDisposition> baselineDisposition = baselineResult.Analysis.Decisions
            .ToDictionary(item => item.SourceFactId.Value, item => item.Disposition, StringComparer.Ordinal);
        HashSet<string> baselineCandidates = CandidateFacts(baselineResult.Analysis);

        CandidateDeliveredInputContract renamed = baseline with
        {
            LinkFacts = baseline.LinkFacts.Select(item => item with
            {
                PriorSourceIdentity = "renamed-prior",
                WinningSourceIdentity = "renamed-winner",
            }).ToArray(),
        };
        AssertExistingMembership(baselineDisposition, baselineCandidates, ExecuteDirect(renamed));

        CandidateDeliveredInputContract reordered = baseline with
        {
            LinkFacts = baseline.LinkFacts.Reverse().ToArray(),
            FaceGenFacts = baseline.FaceGenFacts.Reverse().ToArray(),
            CoverageGapFacts = baseline.CoverageGapFacts.Reverse().ToArray(),
            DocumentationFacts = baseline.DocumentationFacts.Reverse().ToArray(),
        };
        AssertExistingMembership(baselineDisposition, baselineCandidates, ExecuteDirect(reordered));

        CandidateDeliveredDocumentationFactContract documentation = baseline.DocumentationFacts.Single(item => item.FactId.Value == "fact.d.001");
        CandidatePipelineResult relevantEvidence = ExecuteDirect(baseline with
        {
            DocumentationFacts = baseline.DocumentationFacts.Select(item => item == documentation
                ? item with
                {
                    Applicability = ClaimApplicabilityState.Contradicted,
                    ContradictingEvidenceIds = [new OpaqueId("evidence.metamorph.contradiction")],
                }
                : item).ToArray(),
        });
        Assert.AreEqual(CandidateDecisionDisposition.Ambiguous,
            relevantEvidence.Analysis.Decisions.Single(item => item.SourceFactId.Value == "fact.d.001").Disposition);
        Assert.IsTrue(relevantEvidence.Analysis.Abstentions.Any(item =>
            relevantEvidence.Analysis.Decisions.Single(decision => decision.DecisionId == item.DecisionId).SourceFactId.Value == "fact.d.001"));

        CandidateDeliveredFaceGenFactContract ranked = baseline.FaceGenFacts.Single(item => item.FactId.Value == "fact.f.002");
        CandidatePipelineResult rankOnly = ExecuteDirect(baseline with
        {
            FaceGenFacts = baseline.FaceGenFacts.Select(item => item == ranked ? item with { Locality = 90 } : item).ToArray(),
        });
        CollectionAssert.AreEquivalent(baselineCandidates.ToArray(), CandidateFacts(rankOnly.Analysis).ToArray());

        CandidateDeliveredLinkFactContract template = baseline.LinkFacts[0];
        CandidateDeliveredInputContract inserted = baseline with
        {
            LinkFacts = [.. baseline.LinkFacts, template with
            {
                FactId = new OpaqueId("fact.l.metamorph-neutral"),
                RecordParticipantId = new OpaqueId("participant.record.neutral"),
                PriorContributionId = new OpaqueId("participant.prior.neutral"),
                WinningContributionId = new OpaqueId("participant.winner.neutral"),
                PriorTargetParticipantId = new OpaqueId("participant.target.neutral"),
                WinningTargetParticipantId = new OpaqueId("participant.target.neutral"),
                DependencyIds = [new OpaqueId("dependency.neutral")],
                EvidenceIds = [new OpaqueId("evidence.neutral")],
            }],
        };
        CandidatePipelineResult unrelated = ExecuteDirect(inserted);
        AssertExistingMembership(baselineDisposition, baselineCandidates, unrelated);
        Assert.AreEqual(CandidateDecisionDisposition.ResolvedNegative,
            unrelated.Analysis.Decisions.Single(item => item.SourceFactId.Value == "fact.l.metamorph-neutral").Disposition);

        CandidateDeliveredLinkFactContract dependency = baseline.LinkFacts.Single(item => item.FactId.Value == "fact.l.001");
        CandidatePipelineResult changedDependency = ExecuteDirect(baseline with
        {
            LinkFacts = baseline.LinkFacts.Select(item => item == dependency
                ? item with { WinningTargetParticipantId = item.PriorTargetParticipantId }
                : item).ToArray(),
        });
        Assert.AreEqual(CandidateDecisionDisposition.ResolvedNegative,
            changedDependency.Analysis.Decisions.Single(item => item.SourceFactId.Value == "fact.l.001").Disposition);
        Assert.IsFalse(CandidateFacts(changedDependency.Analysis).Contains("fact.l.001"));
    }

    private static CandidatePipelineResult ExecuteDirect(CandidateDeliveredInputContract input)
    {
        input = input with { PayloadId = CandidateDeliveredInputIdentity.ComputePayloadId(input) };
        CandidatePublicFixturePackage package = new(
            new PublicFixturePackage(new OpaqueId("metamorph"), new ContractVersion(1, 0, 0), FixturePartition.Development,
                default, default, default, default, default, default, default), input, null);
        return Execute(package);
    }

    private static void AssertExistingMembership(
        IReadOnlyDictionary<string, CandidateDecisionDisposition> expectedDisposition,
        HashSet<string> expectedCandidates,
        CandidatePipelineResult actual)
    {
        Dictionary<string, CandidateDecisionDisposition> actualDisposition = actual.Analysis.Decisions
            .ToDictionary(item => item.SourceFactId.Value, item => item.Disposition, StringComparer.Ordinal);
        foreach ((string key, CandidateDecisionDisposition value) in expectedDisposition)
        {
            Assert.AreEqual(value, actualDisposition[key], key);
        }
        CollectionAssert.AreEquivalent(expectedCandidates.ToArray(), CandidateFacts(actual.Analysis).Where(expectedCandidates.Contains).ToArray());
    }

    private static HashSet<string> CandidateFacts(CandidateAnalysisContract analysis)
    {
        Dictionary<OpaqueId, CandidateDecisionContract> decisions = analysis.Decisions.ToDictionary(item => item.DecisionId);
        return analysis.Candidates.Select(item => decisions[item.DecisionId].SourceFactId.Value).ToHashSet(StringComparer.Ordinal);
    }

    private static bool SameRecipe(CandidateDeliveredExpansionContract left, CandidateDeliveredExpansionContract right) =>
        JsonSerializer.Serialize(left.LinkSeries) == JsonSerializer.Serialize(right.LinkSeries)
        && JsonSerializer.Serialize(left.FaceGenSeries) == JsonSerializer.Serialize(right.FaceGenSeries)
        && JsonSerializer.Serialize(left.DocumentationSeries) == JsonSerializer.Serialize(right.DocumentationSeries)
        && JsonSerializer.Serialize(left.CoverageGapSeries) == JsonSerializer.Serialize(right.CoverageGapSeries);

    private static string[] OracleSubjects(JsonElement oracle, string collection) => oracle.GetProperty(collection)
        .EnumerateArray().Select(item => item.GetProperty("subject_id").GetString()!).ToArray();

    private static string Lane(CandidateLane value) => value switch
    {
        CandidateLane.DeterministicRequired => "deterministic-required",
        CandidateLane.MandatoryEvidence => "mandatory-evidence",
        CandidateLane.OptionalRanked => "optional-ranked",
        _ => "unspecified",
    };

    private static string Disposition(CandidateDecisionDisposition value) => value switch
    {
        CandidateDecisionDisposition.CandidateAdmitted => "admitted",
        CandidateDecisionDisposition.ResolvedNegative => "resolved-negative",
        CandidateDecisionDisposition.Unsupported => "unsupported",
        CandidateDecisionDisposition.Ambiguous => "ambiguous",
        CandidateDecisionDisposition.InvalidInput => "invalid-input",
        CandidateDecisionDisposition.Limited => "limited",
        CandidateDecisionDisposition.Deferred => "deferred",
        CandidateDecisionDisposition.Unprocessed => "unprocessed",
        CandidateDecisionDisposition.Failed => "failed",
        CandidateDecisionDisposition.Abstained => "abstained",
        _ => "unspecified",
    };

    private static IReadOnlyList<ExecutionBoundaryContract> Boundaries() =>
    [
        new("provider", BoundaryUseState.NotUsed, "public local fixture"),
        new("hosted-search", BoundaryUseState.NotUsed, "public local fixture"),
        new("nexus", BoundaryUseState.NotUsed, "public local fixture"),
        new("loot", BoundaryUseState.NotUsed, "public local fixture"),
    ];

    private static ArtifactReferenceContract Reference(OpaqueId id, string fingerprint) =>
        new(id, new ContractVersion(1, 0, 0), new Sha256Fingerprint(fingerprint), "retained");

    private static Sha256Fingerprint Fingerprint(byte[] bytes) =>
        new(Convert.ToHexStringLower(SHA256.HashData(bytes)));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Infinium.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record CandidateFixture(CandidatePublicFixturePackage Package, JsonDocument Projection);
}
