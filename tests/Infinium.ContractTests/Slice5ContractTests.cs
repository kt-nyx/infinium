using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.Cases;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Slice5ContractTests
{
    private static readonly Sha256Fingerprint Fingerprint = new(new string('a', 64));

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Cases")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Cases")]
    public void FindingPromotionInputAndPublicationRoundTripThroughClosedSchemas()
    {
        FindingCaseInputContract input = CreateFindingCaseInput();
        byte[] inputBytes = FindingCaseInputJsonCodec.Serialize(input);
        CollectionAssert.AreEqual(inputBytes, FindingCaseInputJsonCodec.Serialize(FindingCaseInputJsonCodec.Deserialize(inputBytes)));

        FindingCaseContract output = FindingCasePipeline.Execute(input);
        byte[] outputBytes = FindingCaseJsonCodec.Serialize(output);
        CollectionAssert.AreEqual(outputBytes, FindingCaseJsonCodec.Serialize(FindingCaseJsonCodec.Deserialize(outputBytes)));
        Assert.AreEqual(output.PayloadId, FindingCaseJsonCodec.Deserialize(outputBytes).PayloadId);
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void CaseGroupingPublicationRejectsMissingCausalProofAndAnswerBearingInput()
    {
        FindingCaseInputContract input = CreateFindingCaseInput();
        FindingCaseContract output = FindingCasePipeline.Execute(input);
        Slice5CaseContract invalidCase = output.Cases.Single() with { CauseProofEvidenceIds = [] };
        FindingCaseContract invalid = output with { Cases = [invalidCase] };
        invalid = invalid with { PayloadId = FindingCaseIdentity.ComputePayloadId(invalid) };
        Assert.ThrowsExactly<InvalidOperationException>(() => FindingCaseJsonCodec.Serialize(invalid));

        string json = Encoding.UTF8.GetString(FindingCaseInputJsonCodec.Serialize(input));
        string answerBearing = json.Replace(
            "\"promotion_policy_id\": \"promotion-policy-contract\"",
            "\"promotion_policy_id\": \"promotion-policy-contract\",\n  \"expected_outcome\": \"supported-finding\"",
            StringComparison.Ordinal);
        Assert.AreNotEqual(json, answerBearing);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            FindingCaseInputJsonCodec.Deserialize(Encoding.UTF8.GetBytes(answerBearing)));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void Slice5ContractPayloadsRoundTripThroughStrictEmbeddedSchemas()
    {
        DocumentationEvidenceContract documentation = new(
            ContractConstants.DocumentationEvidenceSchemaId,
            Version(),
            Id("documentation-payload"),
            Id("run-1"),
            [], [], [], [], [], [], [], [], []);
        documentation = documentation with
        {
            PayloadId = DocumentationEvidenceIdentity.ComputePayloadId(documentation),
        };
        CandidateAnalysisContract candidates = new(
            ContractConstants.CandidateAnalysisSchemaId,
            Version(),
            Id("candidate-payload"),
            Id("run-1"),
            Id("analyzer-1"),
            Id("population-1"),
            0,
            [], [], [], [], [])
        {
            PolicyId = Id("policy-1"),
            ThresholdId = Id("threshold-1"),
            LimitId = Id("limit-1"),
            ExecutionInputId = Id("execution-input-1"),
            ExecutionInputDescriptors = ["execution-binding"],
            PolicyDescriptors = ["policy-binding"],
            ThresholdDescriptors = ["threshold-binding"],
            LimitDescriptors = ["limit-binding"],
            ExecutionInputFingerprint = CandidateAnalysisIdentity.StructuralHash(["execution-binding"]),
            PolicyFingerprint = CandidateAnalysisIdentity.StructuralHash(["policy-binding"]),
            ThresholdFingerprint = CandidateAnalysisIdentity.StructuralHash(["threshold-binding"]),
            LimitFingerprint = CandidateAnalysisIdentity.StructuralHash(["limit-binding"]),
            AnalyzerBindings = [new(Id("analyzer-1"), Version(), Version(), Version(), Version(),
                CandidateAnalysisIdentity.StructuralHash(["{}"]), "{}")],
            AnalyzerSetFingerprint = CandidateAnalysisIdentity.StructuralHash(
                [$"analyzer-1:{CandidateAnalysisIdentity.StructuralHash(["{}"]).Value}"]),
        };
        candidates = candidates with
        {
            AnalysisRootId = CandidateAnalysisIdentity.StableId(
                "candidate-analysis-root", "run-1", "population-1",
                candidates.ExecutionInputFingerprint.Value, candidates.PolicyFingerprint.Value,
                candidates.ThresholdFingerprint.Value, candidates.LimitFingerprint.Value,
                candidates.AnalyzerSetFingerprint.Value),
            DependencyEdges =
            [
                Edge("candidate-analysis-root", CandidateAnalysisIdentity.StableId(
                        "candidate-analysis-root", "run-1", "population-1",
                        candidates.ExecutionInputFingerprint.Value, candidates.PolicyFingerprint.Value,
                        candidates.ThresholdFingerprint.Value, candidates.LimitFingerprint.Value,
                        candidates.AnalyzerSetFingerprint.Value),
                    "execution-input-binding", CandidateAnalysisIdentity.StableId(
                        "candidate-execution-input-binding", "execution-input-1", candidates.ExecutionInputFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", CandidateAnalysisIdentity.StableId(
                        "candidate-analysis-root", "run-1", "population-1",
                        candidates.ExecutionInputFingerprint.Value, candidates.PolicyFingerprint.Value,
                        candidates.ThresholdFingerprint.Value, candidates.LimitFingerprint.Value,
                        candidates.AnalyzerSetFingerprint.Value),
                    "policy-binding", CandidateAnalysisIdentity.StableId("candidate-policy-binding", "policy-1", candidates.PolicyFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", CandidateAnalysisIdentity.StableId(
                        "candidate-analysis-root", "run-1", "population-1",
                        candidates.ExecutionInputFingerprint.Value, candidates.PolicyFingerprint.Value,
                        candidates.ThresholdFingerprint.Value, candidates.LimitFingerprint.Value,
                        candidates.AnalyzerSetFingerprint.Value),
                    "threshold-binding", CandidateAnalysisIdentity.StableId("candidate-threshold-binding", "threshold-1", candidates.ThresholdFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", CandidateAnalysisIdentity.StableId(
                        "candidate-analysis-root", "run-1", "population-1",
                        candidates.ExecutionInputFingerprint.Value, candidates.PolicyFingerprint.Value,
                        candidates.ThresholdFingerprint.Value, candidates.LimitFingerprint.Value,
                        candidates.AnalyzerSetFingerprint.Value),
                    "limit-binding", CandidateAnalysisIdentity.StableId("candidate-limit-binding", "limit-1", candidates.LimitFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", CandidateAnalysisIdentity.StableId(
                        "candidate-analysis-root", "run-1", "population-1",
                        candidates.ExecutionInputFingerprint.Value, candidates.PolicyFingerprint.Value,
                        candidates.ThresholdFingerprint.Value, candidates.LimitFingerprint.Value,
                        candidates.AnalyzerSetFingerprint.Value),
                    "analyzer-declaration-binding", CandidateAnalysisIdentity.StableId(
                        "candidate-analyzer-binding", "analyzer-1", Version().ToString(), Version().ToString(),
                        Version().ToString(), Version().ToString(), CandidateAnalysisIdentity.StructuralHash(["{}"]).Value), "uses"),
            ],
        };
        candidates = candidates with { Counts = CandidateAnalysisCounts.Compute(candidates) };
        candidates = candidates with { PayloadId = CandidateAnalysisIdentity.ComputePayloadId(candidates) };
        FindingCaseContract findings = new(
            ContractConstants.FindingCaseSchemaId,
            Version(),
            Id("finding-payload"),
            Id("run-1"),
            Id("finding-input"), Id("promotion-policy"), Version(),
            Id("reconciliation-policy"), Version(),
            [], [], [], [], [], [], [], [], [], [], [], [],
            [
                new("provider", BoundaryUseState.NotUsed, "local-only"),
                new("hosted-search", BoundaryUseState.NotUsed, "local-only"),
                new("nexus", BoundaryUseState.NotUsed, "local-only"),
                new("loot", BoundaryUseState.NotUsed, "not configured"),
            ], "no-safety-claim");
        findings = findings with { PayloadId = FindingCaseIdentity.ComputePayloadId(findings) };
        AnalysisReplayContract replay = new(
            ContractConstants.AnalysisReplaySchemaId,
            Version(),
            Id("replay-1"),
            Id("run-1"),
            ReplayMode.Clean,
            ReplayState.Partial,
            AuditabilityState.Partial,
            [], [], [], [], [], false, null);
        AnalysisExecutionInputContract execution = CreateExecutionInput();

        AssertSemanticRoundTrip(documentation, DocumentationEvidenceJsonCodec.Serialize, bytes => DocumentationEvidenceJsonCodec.Deserialize(bytes));
        AssertSemanticRoundTrip(candidates, CandidateAnalysisJsonCodec.Serialize, bytes => CandidateAnalysisJsonCodec.Deserialize(bytes));
        AssertSemanticRoundTrip(findings, FindingCaseJsonCodec.Serialize, bytes => FindingCaseJsonCodec.Deserialize(bytes));
        AssertSemanticRoundTrip(replay, AnalysisReplayJsonCodec.Serialize, bytes => AnalysisReplayJsonCodec.Deserialize(bytes));
        AssertSemanticRoundTrip(execution, AnalysisExecutionInputJsonCodec.Serialize, bytes => AnalysisExecutionInputJsonCodec.Deserialize(bytes));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void CandidateContractRejectsDanglingMissingMisdirectedAndSemanticallyUnboundReferences()
    {
        CandidateAnalysisContract valid = CreateNonEmptyCandidateAnalysis();
        AssertSemanticRoundTrip(valid, CandidateAnalysisJsonCodec.Serialize, bytes => CandidateAnalysisJsonCodec.Deserialize(bytes));

        JsonNode schemaMutation = JsonNode.Parse(CandidateAnalysisJsonCodec.Serialize(valid))!;
        Assert.IsTrue(schemaMutation["candidates"]![0]!.AsObject().Remove("hypothesis_id"));
        using (JsonDocument missingHypothesisId = JsonDocument.Parse(schemaMutation.ToJsonString()))
        {
            Assert.ThrowsExactly<InvalidDataException>(() => ActiveJsonSchemaValidator.Validate(
                missingHypothesisId.RootElement, "candidate-analysis.v1.schema.json"));
        }

        CandidateDependencyEdgeContract firstEdge = valid.DependencyEdges[0];
        CandidateDependencyEdgeContract misdirected = firstEdge with
        {
            EdgeId = CandidateAnalysisIdentity.StableId(
                "candidate-edge", firstEdge.FromKind, firstEdge.FromId.Value,
                "candidate", firstEdge.ToId.Value, firstEdge.EdgeKind),
            ToKind = "candidate",
        };
        CandidateDependencyEdgeContract dangling = Edge(
            "candidate", Id("missing-candidate"), "candidate-decision", valid.Decisions[0].DecisionId, "derived-from");

        AssertRejected(Reidentify(valid with { DependencyEdges = valid.DependencyEdges.Skip(1).ToArray() }));
        AssertRejected(Reidentify(valid with { DependencyEdges = valid.DependencyEdges.Append(dangling).ToArray() }));
        AssertRejected(Reidentify(valid with { DependencyEdges = valid.DependencyEdges.Skip(1).Prepend(misdirected).ToArray() }));
        AssertRejected(Reidentify(valid with { PolicyId = Id("different-policy") }));
        AssertRejected(Reidentify(valid with
        {
            Decisions = [valid.Decisions[0] with { DependencyIds = [Id("different-dependency")] }],
        }));
        AssertRejected(Reidentify(valid with
        {
            Candidates = [valid.Candidates[0] with { SupportingEvidenceIds = [Id("invented-evidence")] }],
        }));
        AssertRejected(Reidentify(valid with
        {
            Candidates = [valid.Candidates[0] with { HypothesisId = Id("different-hypothesis") }],
        }));
        AssertRejected(valid with
        {
            Decisions = [valid.Decisions[0] with { Lane = CandidateLane.Unspecified }],
        });
        AssertRejected(RecountAndIdentify(valid with
        {
            Candidates = [valid.Candidates[0] with
            {
                State = Slice5ResultState.Abstained,
                MissingInformation = ["required witness"],
            }],
            Hypotheses = [valid.Hypotheses[0] with
            {
                State = Slice5ResultState.Partial,
                MissingInformation = ["required witness"],
            }],
        }));

        CandidateDecisionContract failedDecision = valid.Decisions[0] with
        {
            Lane = CandidateLane.MandatoryEvidence,
            Disposition = CandidateDecisionDisposition.Failed,
            AdmissionIndependentOfScore = false,
        };
        AssertRejected(RecountAndIdentify(valid with
        {
            Decisions = [failedDecision],
            Candidates = [],
            Hypotheses = [],
            DependencyEdges = valid.DependencyEdges.Take(3).ToArray(),
        }));
        AssertRejected(RecountAndIdentify(valid with
        {
            Decisions = [failedDecision with { Disposition = CandidateDecisionDisposition.Unsupported }],
            Candidates = [],
            Hypotheses = [],
            DependencyEdges = valid.DependencyEdges.Take(3).ToArray(),
        }));
        CandidateGapContract falseGap = new(
            Id("false-gap"), valid.Decisions[0].DecisionId, valid.PopulationId,
            Slice5ResultState.Missing, "claimed missing witness", "required witness");
        AssertRejected(RecountAndIdentify(valid with
        {
            Gaps = [falseGap],
            DependencyEdges = valid.DependencyEdges.Append(Edge(
                "gap", falseGap.GapId, "candidate-decision", falseGap.DecisionId, "limits")).ToArray(),
        }));

        static void AssertRejected(CandidateAnalysisContract value) =>
            Assert.ThrowsExactly<InvalidOperationException>(() => CandidateAnalysisJsonCodec.Serialize(value));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Mutation")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Mutation")]
    public void CandidateContractRequiresExactAbstentionAndOptionalLimitCorrespondence()
    {
        CandidateAnalysisContract unsupported = ExecuteCandidateContract(
            [CandidateMember("unsupported", CandidateLane.MandatoryEvidence, CausalJoinInputState.Unsupported)]);
        CandidateAbstentionContract retainedUnsupported = unsupported.Abstentions.Single();
        CandidateDependencyEdgeContract retainedUnsupportedEdge = unsupported.DependencyEdges.Single(item =>
            item.FromKind == "abstention" && item.FromId == retainedUnsupported.AbstentionId);
        AssertRejected(RecountAndIdentify(unsupported with
        {
            Abstentions = [],
            DependencyEdges = unsupported.DependencyEdges.Where(item => item.EdgeId != retainedUnsupportedEdge.EdgeId).ToArray(),
        }));

        CandidateAbstentionContract duplicateUnsupported = retainedUnsupported with
        {
            AbstentionId = Id("duplicate-unsupported-abstention"),
        };
        AssertRejected(RecountAndIdentify(unsupported with
        {
            Abstentions = unsupported.Abstentions.Append(duplicateUnsupported).ToArray(),
            DependencyEdges = unsupported.DependencyEdges.Append(Edge(
                "abstention", duplicateUnsupported.AbstentionId, "candidate-decision",
                duplicateUnsupported.DecisionId, "derived-from")).ToArray(),
        }));
        CandidateGapContract duplicateGap = unsupported.Gaps.Single() with
        {
            GapId = Id("duplicate-unsupported-gap"),
        };
        AssertRejected(RecountAndIdentify(unsupported with
        {
            Gaps = unsupported.Gaps.Append(duplicateGap).ToArray(),
            DependencyEdges = unsupported.DependencyEdges.Append(Edge(
                "gap", duplicateGap.GapId, "candidate-decision", duplicateGap.DecisionId, "derived-from")).ToArray(),
        }));

        CandidateAnalysisContract linked = ExecuteCandidateContract(
        [
            CandidateMember("missing-one", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete,
                ["first required witness"], emitGap: true),
            CandidateMember("missing-two", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete,
                ["second required witness"], emitGap: true),
        ]);
        CandidateAbstentionContract firstAbstention = linked.Abstentions.OrderBy(item => item.AbstentionId.Value, StringComparer.Ordinal).First();
        OpaqueId otherDecisionId = linked.Decisions.Single(item => item.DecisionId != firstAbstention.DecisionId).DecisionId;
        CandidateDependencyEdgeContract firstAbstentionEdge = linked.DependencyEdges.Single(item =>
            item.FromKind == "abstention" && item.FromId == firstAbstention.AbstentionId);
        AssertRejected(RecountAndIdentify(linked with
        {
            Abstentions = linked.Abstentions.Select(item => item.AbstentionId == firstAbstention.AbstentionId
                ? item with { DecisionId = otherDecisionId }
                : item).ToArray(),
            DependencyEdges = linked.DependencyEdges.Where(item => item.EdgeId != firstAbstentionEdge.EdgeId)
                .Append(Edge("abstention", firstAbstention.AbstentionId, "candidate-decision", otherDecisionId, "derived-from"))
                .ToArray(),
        }));

        CandidateAnalysisContract limited = ExecuteCandidateContract(
            [CandidateMember("limited", CandidateLane.OptionalRanked, CausalJoinInputState.Complete, rank: 1)],
            new CandidateExecutionLimits(Id("candidate-limit-contract"), 1, 0));
        AssertRejected(RecountAndIdentify(limited with
        {
            Decisions = [limited.Decisions.Single() with
            {
                Lane = CandidateLane.MandatoryEvidence,
                AdmissionIndependentOfScore = true,
                OptionalRank = null,
            }],
        }));

        static void AssertRejected(CandidateAnalysisContract value) =>
            Assert.ThrowsExactly<InvalidOperationException>(() => CandidateAnalysisJsonCodec.Serialize(value));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void Slice5ExecutionInputRejectsAnswersUnknownPropertiesAndDuplicateProperties()
    {
        string json = Encoding.UTF8.GetString(AnalysisExecutionInputJsonCodec.Serialize(CreateExecutionInput()));
        string answerBearing = json.Replace(
            "\"run_id\": \"run-1\"",
            "\"run_id\": \"run-1\",\n  \"oracle\": \"forbidden\"",
            StringComparison.Ordinal);
        Assert.AreNotEqual(json, answerBearing, "The unknown-property mutation must alter the serialized payload.");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            AnalysisExecutionInputJsonCodec.Deserialize(Encoding.UTF8.GetBytes(answerBearing)));

        string duplicate = json.Replace(
            "\"schema_version\": \"1.0.0\"",
            "\"schema_version\": \"1.0.0\",\n  \"schema_version\": \"1.0.0\"",
            StringComparison.Ordinal);
        Assert.AreNotEqual(json, duplicate, "The duplicate-property mutation must alter the serialized payload.");
        Assert.ThrowsExactly<InvalidDataException>(() =>
            AnalysisExecutionInputJsonCodec.Deserialize(Encoding.UTF8.GetBytes(duplicate)));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void Slice5ExecutionInputRepresentsFixedStressPopulationsExactly()
    {
        AnalysisExecutionInputContract value = CreateExecutionInput() with
        {
            Limits = new AnalysisExecutionLimitsContract(1_000_000, 2_000_000, 100_000, 100_000, 120_000),
        };

        byte[] encoded = AnalysisExecutionInputJsonCodec.Serialize(value);
        AnalysisExecutionInputContract decoded = AnalysisExecutionInputJsonCodec.Deserialize(encoded);
        Assert.AreEqual(1_000_000, decoded.Limits.MaximumEntities);
        Assert.AreEqual(2_000_000, decoded.Limits.MaximumEdges);
        Assert.AreEqual(100_000, decoded.Limits.MaximumTruthRows);
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void Slice5ReplayModesBindPriorRunIdentityExactly()
    {
        AnalysisExecutionInputContract retained = CreateExecutionInput() with
        {
            Mode = ReplayMode.RetainedDownstreamReplay,
            PriorRunId = Id("prior-run"),
        };
        AssertSemanticRoundTrip(
            retained,
            AnalysisExecutionInputJsonCodec.Serialize,
            bytes => AnalysisExecutionInputJsonCodec.Deserialize(bytes));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            AnalysisExecutionInputJsonCodec.Serialize(retained with { PriorRunId = null }));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            AnalysisExecutionInputJsonCodec.Serialize(CreateExecutionInput() with { PriorRunId = Id("prior-run") }));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void Slice5CleanReplayCanCloseWithoutComparedRunIdentity()
    {
        AnalysisReplayContract replay = new(
            ContractConstants.AnalysisReplaySchemaId,
            Version(),
            Id("replay-1"),
            Id("run-1"),
            ReplayMode.Clean,
            ReplayState.CompleteClean,
            AuditabilityState.Complete,
            [], [], [], [], [], true, null);

        AssertSemanticRoundTrip(
            replay,
            AnalysisReplayJsonCodec.Serialize,
            bytes => AnalysisReplayJsonCodec.Deserialize(bytes));
    }

    private static AnalysisExecutionInputContract CreateExecutionInput()
    {
        return new AnalysisExecutionInputContract(
            ContractConstants.AnalysisExecutionInputSchemaId,
            Version(),
            Id("execution-1"),
            Id("run-1"),
            Reference("snapshot-1"),
            Reference("bethesda-1"),
            [],
            [],
            Reference("configuration-1"),
            Reference("manifest-1"),
            ReplayMode.Clean,
            null,
            7,
            new AnalysisExecutionLimitsContract(1_000, 2_000, 100, 100, 1_000),
            [
                new("provider", BoundaryUseState.NotUsed, "local-only"),
                new("hosted-search", BoundaryUseState.NotUsed, "local-only"),
                new("nexus", BoundaryUseState.NotUsed, "local-only"),
                new("loot", BoundaryUseState.NotUsed, "not configured"),
            ]);
    }

    private static FindingCaseInputContract CreateFindingCaseInput()
    {
        CandidateAnalysisContract candidates = CreateNonEmptyCandidateAnalysis();
        CandidateHypothesisContract hypothesis = candidates.Hypotheses.Single();
        CandidateAnalysisEntryContract candidate = candidates.Candidates.Single(item => item.CandidateId == hypothesis.CandidateId);
        CandidateDecisionContract decision = candidates.Decisions.Single(item => item.DecisionId == candidate.DecisionId);
        CandidateAnalyzerBindingContract binding = candidates.AnalyzerBindings.Single(item => item.AnalyzerId == decision.AnalyzerId);
        FindingEvidenceFactContract findingFact = new(
            Id("finding-fact-contract"), hypothesis.HypothesisId,
            WorstCredibleConsequence.MeaningfulBoundedLoss, "finding-locus", candidate.CausalExplanation,
            ["applicable"], [], [],
            hypothesis.SupportingEvidenceIds);
        SharedCauseProofContract causeProof = new(
            Id("cause-proof-contract"), [hypothesis.HypothesisId], decision.AnalyzerId.Value,
            binding.SemanticContractVersion, binding.IdentityContractVersion,
            decision.Participants.ToDictionary(item => item.ParticipantId.Value, item => item.Role, StringComparer.Ordinal),
            candidate.CausalExplanation, findingFact.AffectedLocus, findingFact.ApplicabilityPredicates,
            FindingCaseIdentity.SharedCauseDependencyClosureId(decision.DependencyIds),
            hypothesis.SupportingEvidenceIds);
        FindingCaseInputContract input = new(
            ContractConstants.FindingCaseInputSchemaId, Version(), Id("pending"), candidates.OriginatingRunId,
            Id("promotion-policy-contract"), Version(), Id("reconciliation-policy-contract"), Version(),
            Id("reconciliation-actor-contract"),
            new UtcTimestamp(new DateTimeOffset(2026, 8, 9, 12, 0, 0, TimeSpan.Zero)),
            candidates, [findingFact],
            [new FindingRecommendationFactContract(
                Id("recommendation-fact-contract"), hypothesis.HypothesisId, RecommendationKind.Validation,
                "Validate the typed causal condition.", "Bounded to supplied typed evidence.",
                "No installed state is changed by analysis.", ["Applicability must remain valid."],
                "Reobserve the affected locus.", hypothesis.SupportingEvidenceIds)],
            [causeProof], [], [],
            [new CoveragePopulationFactContract(Id("coverage-population-contract"), candidates.AnalyzerId,
                "candidate-hypotheses", "admitted hypotheses")],
            [new CoverageMemberFactContract(Id("coverage-member-contract"), candidates.AnalyzerId,
                "candidate-hypotheses", "admitted hypotheses", hypothesis.HypothesisId,
                CoverageMemberState.Completed, "completed", "none", null, [])],
            [], [], [], [], [],
            [
                new("provider", BoundaryUseState.NotUsed, "local deterministic analysis"),
                new("hosted-search", BoundaryUseState.NotUsed, "local deterministic analysis"),
                new("nexus", BoundaryUseState.NotUsed, "local deterministic analysis"),
                new("loot", BoundaryUseState.NotUsed, "not configured"),
            ]);
        return input with { InputId = FindingCaseIdentity.ComputeInputId(input) };
    }

    private static IdentityEnvelopeContract FindingIdentity(string cause, string locus, string dependency)
    {
        IdentityEnvelopeContract identity = new(
            "analyzer-contract", Version(), Version(),
            new Dictionary<string, string> { ["source-contract"] = "source", ["target-contract"] = "target" },
            cause, locus, ["applicable"], Id(dependency), Fingerprint);
        return identity with { CanonicalSignature = FindingCaseIdentity.ComputeIdentitySignature(identity) };
    }

    private static CandidateAnalysisContract CreateNonEmptyCandidateAnalysis()
    {
        string declarationJson = "{}";
        Sha256Fingerprint declarationFingerprint = CandidateAnalysisIdentity.StructuralHash([declarationJson]);
        string[] executionDescriptors = ["execution-binding"];
        string[] policyDescriptors = ["policy-binding"];
        string[] thresholdDescriptors = ["threshold-binding"];
        string[] limitDescriptors = ["limit-binding"];
        Sha256Fingerprint executionFingerprint = CandidateAnalysisIdentity.StructuralHash(executionDescriptors);
        Sha256Fingerprint policyFingerprint = CandidateAnalysisIdentity.StructuralHash(policyDescriptors);
        Sha256Fingerprint thresholdFingerprint = CandidateAnalysisIdentity.StructuralHash(thresholdDescriptors);
        Sha256Fingerprint limitFingerprint = CandidateAnalysisIdentity.StructuralHash(limitDescriptors);
        Sha256Fingerprint analyzerSetFingerprint = CandidateAnalysisIdentity.StructuralHash(
            [$"analyzer-contract:{declarationFingerprint.Value}"]);
        OpaqueId analysisRoot = CandidateAnalysisIdentity.StableId(
            "candidate-analysis-root", "run-contract", "population-contract", executionFingerprint.Value,
            policyFingerprint.Value, thresholdFingerprint.Value, limitFingerprint.Value, analyzerSetFingerprint.Value);
        OpaqueId member = Id("member-contract");
        OpaqueId decisionId = Id("decision-contract");
        OpaqueId candidateId = Id("candidate-contract");
        OpaqueId hypothesisId = Id("hypothesis-contract");
        OpaqueId dependency = Id("dependency-contract");
        OpaqueId evidence = Id("evidence-contract");
        OpaqueId closure = CandidateAnalysisIdentity.StableId(
            "candidate-closure", member.Value, dependency.Value);
        CandidateDecisionContract decision = new(
            decisionId, member, Id("source-fact-contract"), CandidateLane.MandatoryEvidence,
            CandidateDecisionDisposition.CandidateAdmitted,
            [new(Id("source-contract"), "source"), new(Id("target-contract"), "target")],
            "typed-causal-join", [Id("source-contract"), evidence, Id("target-contract")], closure,
            "A retained causal input deserves downstream analysis.", [evidence], true, null)
        {
            AnalyzerId = Id("analyzer-contract"),
            PolicyId = Id("policy-contract"),
            ThresholdId = Id("threshold-contract"),
            LimitId = Id("limit-contract"),
            DependencyIds = [dependency],
        };
        CandidateAnalysisEntryContract candidate = new(
            candidateId, decisionId, Slice5ResultState.Present,
            "A retained causal input deserves downstream analysis.", [evidence], [], [],
            AnalysisConfidence.Plausible, Id("threshold-contract"))
        {
            HypothesisId = hypothesisId,
        };
        CandidateHypothesisContract hypothesis = new(
            hypothesisId, candidateId, Slice5ResultState.Present,
            "A retained causal input deserves downstream analysis.",
            "The retained relationship may alter downstream analysis of the exact participants.",
            [evidence], [], [], AnalysisConfidence.Plausible, Id("threshold-contract"));
        CandidateAnalysisContract value = new(
            ContractConstants.CandidateAnalysisSchemaId, Version(), Id("pending"), Id("run-contract"),
            Id("analyzer-contract"), Id("population-contract"), 1,
            [decision], [candidate], [], [], [])
        {
            PolicyId = Id("policy-contract"),
            ThresholdId = Id("threshold-contract"),
            LimitId = Id("limit-contract"),
            ExecutionInputId = Id("execution-input-contract"),
            AnalysisRootId = analysisRoot,
            ExecutionInputDescriptors = executionDescriptors,
            PolicyDescriptors = policyDescriptors,
            ThresholdDescriptors = thresholdDescriptors,
            LimitDescriptors = limitDescriptors,
            ExecutionInputFingerprint = executionFingerprint,
            PolicyFingerprint = policyFingerprint,
            ThresholdFingerprint = thresholdFingerprint,
            LimitFingerprint = limitFingerprint,
            AnalyzerBindings = [new(Id("analyzer-contract"), Version(), Version(), Version(), Version(), declarationFingerprint, declarationJson)],
            AnalyzerSetFingerprint = analyzerSetFingerprint,
            Hypotheses = [hypothesis],
            DependencyEdges =
            [
                Edge("candidate-analysis-root", analysisRoot, "execution-input-binding",
                    CandidateAnalysisIdentity.StableId("candidate-execution-input-binding", "execution-input-contract", executionFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", analysisRoot, "policy-binding",
                    CandidateAnalysisIdentity.StableId("candidate-policy-binding", "policy-contract", policyFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", analysisRoot, "threshold-binding",
                    CandidateAnalysisIdentity.StableId("candidate-threshold-binding", "threshold-contract", thresholdFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", analysisRoot, "limit-binding",
                    CandidateAnalysisIdentity.StableId("candidate-limit-binding", "limit-contract", limitFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", analysisRoot, "analyzer-declaration-binding",
                    CandidateAnalysisIdentity.StableId("candidate-analyzer-binding", "analyzer-contract",
                        Version().ToString(), Version().ToString(), Version().ToString(), Version().ToString(),
                        declarationFingerprint.Value), "uses"),
                Edge("candidate-decision", decisionId, "source-fact", Id("source-fact-contract"), "derived-from"),
                Edge("candidate-decision", decisionId, "dependency-closure", closure, "depends-on"),
                Edge("dependency-closure", closure, "dependency", dependency, "depends-on"),
                Edge("candidate-decision", decisionId, "evidence", evidence, "derived-from"),
                Edge("candidate", candidateId, "candidate-decision", decisionId, "derived-from"),
                Edge("candidate", candidateId, "evidence", evidence, "supports"),
                Edge("hypothesis", hypothesisId, "candidate", candidateId, "derived-from"),
                Edge("hypothesis", hypothesisId, "evidence", evidence, "supports"),
            ],
        };
        value = value with { Counts = CandidateAnalysisCounts.Compute(value) };
        return Reidentify(value);
    }

    private static CandidateAnalysisContract Reidentify(CandidateAnalysisContract value) =>
        value with { PayloadId = CandidateAnalysisIdentity.ComputePayloadId(value) };

    private static CandidateAnalysisContract RecountAndIdentify(CandidateAnalysisContract value)
    {
        value = value with { Counts = CandidateAnalysisCounts.Compute(value) };
        return Reidentify(value);
    }

    private static CandidateDependencyEdgeContract Edge(
        string fromKind,
        OpaqueId fromId,
        string toKind,
        OpaqueId toId,
        string edgeKind) => new(
            CandidateAnalysisIdentity.StableId(
                "candidate-edge", fromKind, fromId.Value, toKind, toId.Value, edgeKind),
            fromKind, fromId, toKind, toId, edgeKind);

    private static CandidateAnalysisContract ExecuteCandidateContract(
        IReadOnlyList<CausalJoinPopulationMember> members,
        CandidateExecutionLimits? limits = null)
    {
        OpaqueId analyzerId = Id("analyzer-contract-pipeline");
        ContractCandidatePopulationSource source = new(analyzerId, members);
        return CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("run-contract-pipeline"), Id("population-contract-pipeline"),
            Id("policy-contract-pipeline"), Id("threshold-contract-pipeline"),
            limits ?? CandidateExecutionLimits.Default, new CandidatePopulationContext(null), [source])).Analysis;
    }

    private static CausalJoinPopulationMember CandidateMember(
        string id,
        CandidateLane lane,
        CausalJoinInputState state,
        IReadOnlyList<string>? missing = null,
        long? rank = null,
        bool emitGap = false) => new(
        Id("member-" + id), Id("analyzer-contract-pipeline"), lane,
        [new(Id("source-" + id), "source"), new(Id("target-" + id), "target")],
        "typed-causal-join", [Id("source-" + id), Id("evidence-" + id), Id("target-" + id)],
        [Id("dependency-" + id)], [Id("evidence-" + id)], [], missing ?? [], state,
        "A bounded causal relationship is retained.",
        "The exact relationship may change downstream analysis of its retained participants.",
        rank, EmitGap: emitGap)
        {
            SourceFactId = Id("fact-" + id),
        };

    private sealed class ContractCandidatePopulationSource(
        OpaqueId analyzerId,
        IReadOnlyList<CausalJoinPopulationMember> members) : ICandidatePopulationSource
    {
        public OpaqueId AnalyzerId => analyzerId;

        public AnalyzerDeclarationContract Declaration { get; } = CandidateAnalyzerDeclarations.Create(
            analyzerId, Math.Max(1, members.Count), 1_000_000,
            supportedShapes: members.Select(item => item.JoinKind).Distinct(StringComparer.Ordinal).ToArray());

        public IReadOnlyList<CausalJoinPopulationMember> DeclarePopulation(
            CandidatePopulationContext context,
            CancellationToken cancellationToken = default) => members;

        public IReadOnlyList<CausalJoinPopulationMember> ConstructPopulation(
            CandidatePopulationContext context,
            CancellationToken cancellationToken = default) => members;
    }

    private static ArtifactReferenceContract Reference(string id) =>
        new(Id(id), Version(), Fingerprint, "retained");

    private static void AssertSemanticRoundTrip<T>(
        T value,
        Func<T, byte[]> serialize,
        Func<byte[], T> deserialize)
    {
        byte[] encoded = serialize(value);
        T decoded = deserialize(encoded);
        CollectionAssert.AreEqual(encoded, serialize(decoded));
    }

    private static ContractVersion Version() => new(1, 0, 0);

    private static OpaqueId Id(string value) => new(value);
}
