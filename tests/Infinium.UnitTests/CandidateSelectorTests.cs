using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateSelectorTests
{
    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorProducesTotalLedgerAndScoreIndependentRequiredLanes()
    {
        CandidatePipelineResult result = Execute(
        [
            Member("required", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete),
            Member("mandatory", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete),
            Member("negative", CandidateLane.MandatoryEvidence, CausalJoinInputState.ResolvedNegative),
            Member("optional-1", CandidateLane.OptionalRanked, CausalJoinInputState.Complete, rank: 1),
            Member("optional-2", CandidateLane.OptionalRanked, CausalJoinInputState.Complete, rank: 2),
        ],
        new CandidateExecutionLimits(Id("limit"), 5, 1));

        Assert.AreEqual(5L, result.Analysis.Counts.Population);
        Assert.AreEqual(1L, result.Analysis.Counts.DeterministicRequired);
        Assert.AreEqual(2L, result.Analysis.Counts.MandatoryEvidence);
        Assert.AreEqual(2L, result.Analysis.Counts.OptionalRanked);
        Assert.AreEqual(3L, result.Analysis.Counts.CandidateAdmitted);
        Assert.AreEqual(1L, result.Analysis.Counts.ResolvedNegative);
        Assert.AreEqual(1L, result.Analysis.Counts.Limited);
        Assert.IsTrue(result.Analysis.Decisions
            .Where(item => item.Lane is CandidateLane.DeterministicRequired or CandidateLane.MandatoryEvidence)
            .All(item => item.AdmissionIndependentOfScore && item.OptionalRank is null));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorRetainsAbstentionGapFailureAndUnrelatedWork()
    {
        CandidatePipelineResult result = Execute(
        [
            Member("abstain", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete, missing: ["applicability"]),
            Member("unsupported", CandidateLane.MandatoryEvidence, CausalJoinInputState.Unsupported, missing: ["supported shape"]),
            Member("failed", CandidateLane.MandatoryEvidence, CausalJoinInputState.Failed, failureCode: "fault", failureMessage: "isolated"),
            Member("unrelated", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete),
        ],
        CandidateExecutionLimits.Default);

        Assert.AreEqual(2L, result.Analysis.Counts.CandidateAdmitted);
        Assert.AreEqual(2L, result.Analysis.Counts.Hypotheses);
        Assert.AreEqual(2L, result.Analysis.Counts.Abstentions);
        Assert.AreEqual(1L, result.Analysis.Counts.Gaps);
        Assert.AreEqual(1L, result.Analysis.Counts.Failures);
        Assert.IsTrue(result.Analysis.Decisions.Any(item => item.PopulationMemberId == Id("member-unrelated")
            && item.Disposition == CandidateDecisionDisposition.CandidateAdmitted));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorIsolatesPopulationDeclarationFailureFromUnrelatedAnalyzers()
    {
        OpaqueId goodAnalyzer = Id("analyzer-good");
        CausalJoinPopulationMember good = Member(
            "unrelated-good", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete) with
        {
            AnalyzerId = goodAnalyzer,
        };
        CandidatePipelineResult result = CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("run-test"), Id("population-test"), Id("policy-test"), Id("threshold-test"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [
                new DeclarationThrowingCandidatePopulationSource(Id("analyzer-broken")),
                new TestCandidatePopulationSource(goodAnalyzer, [good]),
            ]));

        Assert.AreEqual(2L, result.Analysis.Counts.Population);
        Assert.AreEqual(1L, result.Analysis.Counts.CandidateAdmitted);
        Assert.AreEqual(1L, result.Analysis.Counts.Failures);
        Assert.IsTrue(result.Analysis.Failures.Single().FailureCode == "analyzer-declaration-failed");
        string retained = System.Text.Encoding.UTF8.GetString(
            Infinium.Application.Evaluation.CandidateAnalysisJsonCodec.Serialize(result.Analysis));
        Assert.IsFalse(retained.Contains("C:\\private\\fixture-answer.json", StringComparison.Ordinal));
        Assert.IsFalse(retained.Contains("secret fixture content", StringComparison.Ordinal));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorConvertsMalformedOrEvidenceFreeMembersToInvalidWithoutAbortingOthers()
    {
        CausalJoinPopulationMember malformed = Member(
            "malformed", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete) with
        {
            Participants = [new(Id("same"), "duplicate"), new(Id("other"), "duplicate")],
            SupportingEvidenceIds = [],
        };
        CandidatePipelineResult result = Execute(
            [malformed, Member("unrelated", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete)],
            CandidateExecutionLimits.Default);

        Assert.AreEqual(1L, result.Analysis.Counts.InvalidInput);
        Assert.AreEqual(1L, result.Analysis.Counts.CandidateAdmitted);
        CandidateDecisionContract invalid = result.Analysis.Decisions.Single(item =>
            item.Disposition == CandidateDecisionDisposition.InvalidInput);
        Assert.AreEqual(0, invalid.EvidenceIds.Count);
        Assert.AreEqual(0, invalid.DependencyIds.Count);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorRestartReusesOnlyUnchangedMemberFingerprints()
    {
        CausalJoinPopulationMember first = Member("first", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete);
        CausalJoinPopulationMember second = Member("second", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete);
        CandidatePipelineResult baseline = Execute([first, second], CandidateExecutionLimits.Default);
        CandidatePipelineResult changed = Execute(
            [first, second with { Rationale = "relevant input changed" }],
            CandidateExecutionLimits.Default,
            baseline.Checkpoint);

        CollectionAssert.AreEquivalent(new[] { Id("member-first") }, changed.ReusedMemberIds.ToArray());
        CollectionAssert.AreEquivalent(new[] { Id("member-second") }, changed.RecomputedMemberIds.ToArray());
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorOptionalContentMutationInvalidatesOnlyThatMember()
    {
        CausalJoinPopulationMember first = Member(
            "optional-first", CandidateLane.OptionalRanked, CausalJoinInputState.Complete, rank: 1);
        CausalJoinPopulationMember second = Member(
            "optional-second", CandidateLane.OptionalRanked, CausalJoinInputState.Complete, rank: 2);
        CandidatePipelineResult baseline = Execute(
            [first, second], new CandidateExecutionLimits(Id("optional-content-limit"), 2, 2));
        CandidatePipelineResult changed = Execute(
            [first with { DependencyIds = [Id("dependency-optional-first-revised")] }, second],
            new CandidateExecutionLimits(Id("optional-content-limit"), 2, 2), baseline.Checkpoint);

        CollectionAssert.AreEqual(new[] { Id("member-optional-first") }, changed.RecomputedMemberIds.ToArray());
        CollectionAssert.AreEqual(new[] { Id("member-optional-second") }, changed.ReusedMemberIds.ToArray());
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorNeverReusesCheckpointAcrossRuns()
    {
        CausalJoinPopulationMember member = Member(
            "cross-run", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete);
        CandidatePipelineResult baseline = Execute([member], CandidateExecutionLimits.Default);
        OpaqueId analyzer = Id("analyzer-test");
        CandidatePipelineResult changed = CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("run-other"), Id("population-test"), Id("policy-test"), Id("threshold-test"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [new TestCandidatePopulationSource(analyzer, [member])]), baseline.Checkpoint);

        Assert.AreEqual(0, changed.ReusedMemberIds.Count);
        CollectionAssert.AreEqual(new[] { Id("member-cross-run") }, changed.RecomputedMemberIds.ToArray());
        Assert.AreEqual(Id("run-other"), changed.Analysis.OriginatingRunId);
        Assert.AreNotEqual(baseline.Analysis.Decisions.Single().DecisionId, changed.Analysis.Decisions.Single().DecisionId);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorInvalidatesCheckpointWhenLimitSemanticsChangeUnderSameId()
    {
        CausalJoinPopulationMember member = Member(
            "optional", CandidateLane.OptionalRanked, CausalJoinInputState.Complete, rank: 1);
        CandidatePipelineResult baseline = Execute(
            [member], new CandidateExecutionLimits(Id("same-limit-id"), 1, 1));
        CandidatePipelineResult changed = Execute(
            [member], new CandidateExecutionLimits(Id("same-limit-id"), 1, 0), baseline.Checkpoint);

        Assert.AreEqual(0, changed.ReusedMemberIds.Count);
        CollectionAssert.AreEqual(new[] { Id("member-optional") }, changed.RecomputedMemberIds.ToArray());
        Assert.AreEqual(CandidateDecisionDisposition.Limited, changed.Analysis.Decisions.Single().Disposition);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateFingerprintsDistinguishSwappedFieldAndLimitValues()
    {
        CausalJoinPopulationMember baseline = Member("fingerprint", CandidateLane.OptionalRanked, CausalJoinInputState.Complete, rank: 1);
        CausalJoinPopulationMember swapped = baseline with
        {
            JoinKind = baseline.Rationale,
            Rationale = baseline.JoinKind,
        };

        Assert.AreNotEqual(baseline.InputFingerprint, swapped.InputFingerprint);
        Assert.AreNotEqual(
            new CandidateExecutionLimits(Id("same"), 2, 1).SemanticsFingerprint,
            new CandidateExecutionLimits(Id("same"), 1, 2).SemanticsFingerprint);

        CausalJoinPopulationMember framedA = baseline with
        {
            Participants = [new(Id("b:c"), "a"), new(Id("target"), "target")],
            MissingInformation = ["a", "b"],
        };
        CausalJoinPopulationMember framedB = baseline with
        {
            Participants = [new(Id("c"), "a:b"), new(Id("target"), "target")],
            MissingInformation = ["a|b"],
        };
        Assert.AreNotEqual(framedA.InputFingerprint, framedB.InputFingerprint);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateCheckpointRejectsSameIdAnalyzerSemanticDrift()
    {
        CausalJoinPopulationMember member = Member("semantic-drift", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete);
        OpaqueId analyzer = Id("analyzer-test");
        AnalyzerDeclarationContract baselineDeclaration = CandidateAnalyzerDeclarations.Create(analyzer, 1, 10);
        CandidatePipelineRequest baselineRequest = new(
            Id("run-test"), Id("population-test"), Id("policy-test"), Id("threshold-test"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [new TestCandidatePopulationSource(analyzer, [member], baselineDeclaration)]);
        CandidatePipelineResult baseline = CandidatePipeline.Execute(baselineRequest);
        AnalyzerDeclarationContract changedDeclaration = baselineDeclaration with
        {
            RulesetVersion = new ContractVersion(1, 0, 1),
        };
        CandidatePipelineRequest changedRequest = baselineRequest with
        {
            Sources = [new TestCandidatePopulationSource(analyzer, [member], changedDeclaration)],
        };

        CandidatePipelineResult changed = CandidatePipeline.Execute(changedRequest, baseline.Checkpoint);
        Assert.AreEqual(0, changed.ReusedMemberIds.Count);
        Assert.AreEqual(1, changed.RecomputedMemberIds.Count);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorEnforcesAnalyzerIdentityScopeAndResourceDeclarations()
    {
        OpaqueId analyzer = Id("analyzer-test");
        CausalJoinPopulationMember member = Member(
            "declaration", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete);
        AnalyzerDeclarationContract baseline = CandidateAnalyzerDeclarations.Create(
            analyzer, 1, 10, supportedShapes: [member.JoinKind]);

        Assert.ThrowsExactly<InvalidOperationException>(() => CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("run-test"), Id("population-test"), Id("policy-test"), Id("threshold-test"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [new TestCandidatePopulationSource(analyzer, [member], baseline with { AnalyzerId = "different-analyzer" })])));

        AnalyzerDeclarationContract excluded = baseline with
        {
            Scope = baseline.Scope with
            {
                SupportedRecordFieldAssetShapes = ["different-shape"],
                ExcludedRecordFieldAssetShapes = [new(member.JoinKind, "declared unsupported shape")],
            },
        };
        CandidatePipelineResult unsupported = CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("run-test"), Id("population-test"), Id("policy-test"), Id("threshold-test"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [new TestCandidatePopulationSource(analyzer, [member], excluded)]));
        Assert.AreEqual(CandidateDecisionDisposition.Unsupported, unsupported.Analysis.Decisions.Single().Disposition);

        AnalyzerDeclarationContract outputBound = baseline with
        {
            ResourceBounds = baseline.ResourceBounds with { MaximumOutputItems = 1 },
        };
        OpaqueId outputUnrelatedAnalyzer = Id("analyzer-output-unrelated");
        CausalJoinPopulationMember outputUnrelated = Member(
            "output-unrelated", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete)
            with
        { AnalyzerId = outputUnrelatedAnalyzer };
        CandidatePipelineResult outputIsolated = CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("run-test"), Id("population-test"), Id("policy-test"), Id("threshold-test"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [
                new TestCandidatePopulationSource(analyzer, [member], outputBound),
                new TestCandidatePopulationSource(outputUnrelatedAnalyzer, [outputUnrelated]),
            ]));
        Assert.AreEqual(CandidateDecisionDisposition.Failed,
            outputIsolated.Analysis.Decisions.Single(item => item.AnalyzerId == analyzer).Disposition);
        Assert.AreEqual(CandidateDecisionDisposition.CandidateAdmitted,
            outputIsolated.Analysis.Decisions.Single(item => item.AnalyzerId == outputUnrelatedAnalyzer).Disposition);

        AnalyzerDeclarationContract insufficientDeterministicBound = baseline with
        {
            ResourceBounds = baseline.ResourceBounds with { MaximumOutputItems = 10 },
        };
        CausalJoinPopulationMember deterministic = Member(
            "deterministic-output-bound", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete);
        CandidatePipelineResult deterministicBounded = CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("run-test"), Id("population-test"), Id("policy-test"), Id("threshold-test"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [new TestCandidatePopulationSource(analyzer, [deterministic], insufficientDeterministicBound)]));
        Assert.AreEqual(CandidateDecisionDisposition.Failed, deterministicBounded.Analysis.Decisions.Single().Disposition);
        Assert.AreEqual(1, deterministicBounded.Analysis.Failures.Count);

        AnalyzerDeclarationContract insufficientUnsupportedBound = baseline with
        {
            ResourceBounds = baseline.ResourceBounds with { MaximumOutputItems = 8 },
        };
        CausalJoinPopulationMember unsupportedBoundMember = Member(
            "unsupported-output-bound", CandidateLane.MandatoryEvidence, CausalJoinInputState.Unsupported);
        CandidatePipelineResult unsupportedBounded = CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("run-test"), Id("population-test"), Id("policy-test"), Id("threshold-test"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [new TestCandidatePopulationSource(analyzer, [unsupportedBoundMember], insufficientUnsupportedBound)]));
        Assert.AreEqual(CandidateDecisionDisposition.Failed, unsupportedBounded.Analysis.Decisions.Single().Disposition);
        Assert.AreEqual(1, unsupportedBounded.Analysis.Failures.Count);

        AnalyzerDeclarationContract inputBound = baseline with
        {
            ResourceBounds = baseline.ResourceBounds with { MaximumInputItems = 1 },
        };
        OpaqueId unrelatedAnalyzer = Id("analyzer-unrelated");
        CausalJoinPopulationMember unrelated = Member(
            "declaration-unrelated", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete)
            with
        { AnalyzerId = unrelatedAnalyzer };
        CandidatePipelineResult isolated = CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("run-test"), Id("population-test"), Id("policy-test"), Id("threshold-test"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [
                new TestCandidatePopulationSource(
                    analyzer,
                    [member, member with { PopulationMemberId = Id("member-second") }],
                    inputBound),
                new TestCandidatePopulationSource(
                    unrelatedAnalyzer,
                    [unrelated],
                    CandidateAnalyzerDeclarations.Create(
                        unrelatedAnalyzer, 1, 11, supportedShapes: [unrelated.JoinKind])),
            ]));
        Assert.AreEqual(2, isolated.Analysis.Decisions.Count);
        Assert.AreEqual(
            CandidateDecisionDisposition.Failed,
            isolated.Analysis.Decisions.Single(item => item.AnalyzerId == analyzer).Disposition);
        Assert.AreEqual(
            CandidateDecisionDisposition.CandidateAdmitted,
            isolated.Analysis.Decisions.Single(item => item.AnalyzerId == unrelatedAnalyzer).Disposition);
        Assert.AreEqual(1, isolated.Analysis.Failures.Count);
        Assert.AreEqual(1, isolated.Analysis.Gaps.Count);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorBoundsEveryMemberBeforeHashAndEdgeMaterialization()
    {
        CausalJoinPopulationMember oversized = Member(
            "oversized", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete) with
        {
            SupportingEvidenceIds = Enumerable.Range(0, 129).Select(index => Id($"evidence-{index}")).ToArray(),
        };
        CandidatePipelineResult result = Execute([oversized], CandidateExecutionLimits.Default);
        Assert.AreEqual(1L, result.Analysis.Counts.InvalidInput);
        Assert.AreEqual(0, result.Analysis.Decisions.Single().EvidenceIds.Count);
        Assert.AreEqual(7, result.Analysis.DependencyEdges.Count);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorInvalidatesOptionalFrontierWhenRankOrderChanges()
    {
        CausalJoinPopulationMember first = Member(
            "first", CandidateLane.OptionalRanked, CausalJoinInputState.Complete, rank: 1);
        CausalJoinPopulationMember second = Member(
            "second", CandidateLane.OptionalRanked, CausalJoinInputState.Complete, rank: 2);
        CandidateExecutionLimits limits = new(Id("frontier-limit"), 2, 1);
        CandidatePipelineResult baseline = Execute([first, second], limits);
        CandidatePipelineResult changed = Execute(
            [first with { OptionalRank = 2 }, second with { OptionalRank = 1 }],
            limits,
            baseline.Checkpoint);

        Assert.AreEqual(0, changed.ReusedMemberIds.Count);
        CollectionAssert.AreEquivalent(
            new[] { Id("member-first"), Id("member-second") },
            changed.RecomputedMemberIds.ToArray());
        Assert.AreEqual(1L, changed.Analysis.Counts.CandidateAdmitted);
        Assert.AreEqual(
            Id("member-second"),
            changed.Analysis.Decisions.Single(item =>
                item.Disposition == CandidateDecisionDisposition.CandidateAdmitted).PopulationMemberId);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorInvalidatesGlobalWorkFrontierWhenMembershipChanges()
    {
        CausalJoinPopulationMember first = Member("first", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete);
        CausalJoinPopulationMember second = Member("second", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete);
        CandidateExecutionLimits limits = new(Id("work-frontier-limit"), 1, 10);
        CandidatePipelineResult baseline = Execute([second], limits);
        CandidatePipelineResult changed = Execute([first, second], limits, baseline.Checkpoint);

        Assert.AreEqual(0, changed.ReusedMemberIds.Count);
        Assert.AreEqual(1L, changed.Analysis.Counts.CandidateAdmitted);
        Assert.AreEqual(1L, changed.Analysis.Counts.Unprocessed);
        Assert.AreEqual(
            Id("member-first"),
            changed.Analysis.Decisions.Single(item =>
                item.Disposition == CandidateDecisionDisposition.CandidateAdmitted).PopulationMemberId);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateWorkLimitPreservesAllPreclassifiedNegativeStates()
    {
        CandidatePipelineResult result = Execute(
        [
            Member("eligible-first", CandidateLane.DeterministicRequired, CausalJoinInputState.Complete),
            Member("eligible-excess", CandidateLane.MandatoryEvidence, CausalJoinInputState.Complete),
            Member("negative", CandidateLane.MandatoryEvidence, CausalJoinInputState.ResolvedNegative),
            Member("unsupported", CandidateLane.MandatoryEvidence, CausalJoinInputState.Unsupported),
            Member("invalid", CandidateLane.MandatoryEvidence, CausalJoinInputState.InvalidInput),
            Member("deferred", CandidateLane.MandatoryEvidence, CausalJoinInputState.Deferred),
            Member("failed", CandidateLane.MandatoryEvidence, CausalJoinInputState.Failed,
                failureCode: "bounded-failure", failureMessage: "bounded failure"),
        ], new CandidateExecutionLimits(Id("mixed-work-limit"), 1, 10));

        Assert.AreEqual(1L, result.Analysis.Counts.CandidateAdmitted);
        Assert.AreEqual(1L, result.Analysis.Counts.Unprocessed);
        Assert.AreEqual(1L, result.Analysis.Counts.ResolvedNegative);
        Assert.AreEqual(1L, result.Analysis.Counts.Unsupported);
        Assert.AreEqual(1L, result.Analysis.Counts.InvalidInput);
        Assert.AreEqual(1L, result.Analysis.Counts.Deferred);
        Assert.AreEqual(1L, result.Analysis.Counts.Failures);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CandidateSelectorConsumesDeliveredBethesdaAndDocumentationIndexes()
    {
        BethesdaSemanticSnapshot bethesda = EmptyBethesdaSnapshot() with
        {
            Gaps =
            [
                new BethesdaCoverageGap(
                    "gap-1", BethesdaCoverageGapCategory.Capability, "decoder unavailable",
                    "records", 1, "unsupported record shape", "record decoder"),
            ],
        };
        DocumentationEvidenceContract documentation = DocumentationEvidence();
        CandidateDeliveredInputContract delivered = CandidateDeliveredInputAdapter.Create(
            Id("run-documentation"), Id("snapshot-indexes"), Id("context-1"), Id("configuration-1"),
            bethesda, documentation);
        IReadOnlyList<CausalJoinPopulationMember> members = new DeliveredIndexCandidatePopulationSource()
            .ConstructPopulation(new CandidatePopulationContext(
                documentation, Id("run-documentation"), Id("snapshot-indexes"), Id("context-1"),
                Id("configuration-1"), delivered));

        Assert.AreEqual(2, members.Count);
        Assert.AreEqual(1, members.Count(item => item.InputState == CausalJoinInputState.Complete));
        Assert.AreEqual(1, members.Count(item => item.InputState == CausalJoinInputState.Unsupported));
        Assert.IsTrue(members.Any(item => item.JoinKind == "delivered-coverage-gap"));
        Assert.IsTrue(members.Any(item => item.JoinKind == "documentation-application"));
        Assert.AreEqual(1,
            new DeliveredIndexCandidatePopulationSource().Declaration.InputPopulations.Count(item => item.Required));
        Assert.AreEqual(
            0,
            new DeliveredIndexCandidatePopulationSource().Declaration.Dependencies.Count(item => item.Required));

        Assert.ThrowsExactly<InvalidDataException>(() =>
            new DeliveredIndexCandidatePopulationSource().ConstructPopulation(
                new CandidatePopulationContext(null)));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            new DeliveredIndexCandidatePopulationSource().ConstructPopulation(
                new CandidatePopulationContext(documentation, Id("run-documentation"), Id("snapshot-indexes"),
                    Id("context-1"), Id("configuration-1"))));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void DeliveredIndexesPreserveFaceGenNegativesUnknownsAndUnresolvedLinkIdentity()
    {
        BethesdaRecordIdentity identity = new("record-1", "NPC_", "000001:Fixture.esm", "Fixture.esm", 1);
        BethesdaRecordContribution prior = new("contribution-prior", identity, "Prior.esp", 0, false, false, 0);
        BethesdaRecordContribution winner = new("contribution-winner", identity, "Winner.esp", 1, false, false, 0);
        BethesdaLinkFact unresolved = new(
            identity.ParticipantId, prior.ContributionId, "Race", null, 0, null,
            BethesdaLinkState.Unresolved, null);
        BethesdaLinkFact introduced = new(
            identity.ParticipantId, winner.ContributionId, "Template", null, 0, "000002:Fixture.esm",
            BethesdaLinkState.Resolved, "introduced-target");
        BethesdaLooseAssetFact knownMesh = new("meshes/face.nif", ["provider-mesh"], "provider-mesh", BethesdaAssetAvailability.Present, true, false);
        BethesdaLooseAssetFact knownTint = new("textures/face.dds", ["provider-tint"], "provider-tint", BethesdaAssetAvailability.Present, true, false);
        BethesdaLooseAssetFact unknownMesh = new("meshes/unknown.nif", [], null, BethesdaAssetAvailability.Unknown, false, false);
        BethesdaLooseAssetFact unknownTint = new("textures/unknown.dds", [], null, BethesdaAssetAvailability.Unknown, false, false);
        BethesdaSemanticSnapshot snapshot = EmptyBethesdaSnapshot() with
        {
            OverrideChains = new Dictionary<string, BethesdaOverrideChain>(StringComparer.Ordinal)
            {
                [identity.ParticipantId] = new(identity, [prior, winner], winner),
            },
            Links = [unresolved, introduced],
            FaceGen =
            [
                new("npc-applicable", BethesdaFaceGenApplicability.Applicable, "Fixture.esm", 1, knownMesh, knownTint, "applicable"),
                new("npc-negative", BethesdaFaceGenApplicability.NotApplicableTemplateTraits, "Fixture.esm", 2, unknownMesh, unknownTint, "not applicable"),
                new("npc-unknown", BethesdaFaceGenApplicability.UnknownRace, "Fixture.esm", 3, unknownMesh, unknownTint, "unknown"),
            ],
        };

        CandidateDeliveredInputContract delivered = CandidateDeliveredInputAdapter.Create(
            Id("run-indexes"), Id("snapshot-indexes"), Id("context-indexes"), Id("configuration-indexes"), snapshot, null);
        IReadOnlyList<CausalJoinPopulationMember> members =
            new DeliveredIndexCandidatePopulationSource().ConstructPopulation(
                new CandidatePopulationContext(null, Id("run-indexes"), Id("snapshot-indexes"),
                    Id("context-indexes"), Id("configuration-indexes"), delivered));

        CausalJoinPopulationMember negative = members.Single(item =>
            item.Participants.Any(participant => participant.ParticipantId == delivered.FaceGenFacts[1].NpcParticipantId));
        CausalJoinPopulationMember unknown = members.Single(item =>
            item.Participants.Any(participant => participant.ParticipantId == delivered.FaceGenFacts[2].NpcParticipantId));
        CausalJoinPopulationMember link = members.Single(item =>
            item.JoinKind == "record-link-winner-comparison"
            && item.MissingInformation.Contains("resolved canonical link target")
            && item.Participants.All(participant => !participant.Role.EndsWith("target", StringComparison.Ordinal)));
        CausalJoinPopulationMember introducedLink = members.Single(item =>
            item.JoinKind == "record-link-winner-comparison"
            && item.Participants.Any(participant => participant.ParticipantId ==
                delivered.LinkFacts.Single(fact => fact.WinningTargetParticipantId is not null).WinningTargetParticipantId));
        Assert.AreEqual(CausalJoinInputState.ResolvedNegative, negative.InputState);
        Assert.AreEqual(CausalJoinInputState.Ambiguous, unknown.InputState);
        Assert.IsFalse(negative.Participants.Any(item => item.Role.EndsWith("provider", StringComparison.Ordinal)));
        Assert.IsFalse(link.Participants.Any(item => item.Role.StartsWith("prior-target", StringComparison.Ordinal)));
        Assert.IsTrue(link.MissingInformation.Contains("resolved canonical link target"));
        Assert.AreEqual(CausalJoinInputState.Complete, introducedLink.InputState);
        Assert.IsTrue(introducedLink.Participants.Any(item => item.Role == "winning-target"));
        Assert.IsFalse(introducedLink.Participants.Any(item => item.Role.StartsWith("prior-target", StringComparison.Ordinal)));
        foreach (CausalJoinPopulationMember member in members)
        {
            CollectionAssert.IsSubsetOf(
                member.Participants.Select(item => item.ParticipantId).ToArray(),
                member.Path.ToArray(),
                member.PopulationMemberId.Value);
        }
    }

    private static CandidatePipelineResult Execute(
        IReadOnlyList<CausalJoinPopulationMember> members,
        CandidateExecutionLimits limits,
        CandidateCheckpointState? checkpoint = null)
    {
        OpaqueId analyzer = Id("analyzer-test");
        return CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("run-test"), Id("population-test"), Id("policy-test"), Id("threshold-test"), limits,
            new CandidatePopulationContext(null),
            [new TestCandidatePopulationSource(analyzer, members)]), checkpoint);
    }

    internal static CausalJoinPopulationMember Member(
        string id,
        CandidateLane lane,
        CausalJoinInputState state,
        long? rank = null,
        IReadOnlyList<string>? missing = null,
        string? failureCode = null,
        string? failureMessage = null) => new(
            Id("member-" + id), Id("analyzer-test"), lane,
            [new(Id("source-" + id), "source"), new(Id("target-" + id), "target")],
            "typed-causal-join", [Id("source-" + id), Id("evidence-" + id), Id("target-" + id)], [Id("dependency-" + id)],
            [Id("evidence-" + id)], [], missing ?? [], state, "bounded causal relationship",
            "The exact typed relationship may change downstream analysis of its retained participants.", rank,
            failureCode, failureMessage)
        { SourceFactId = Id("fact-" + id) };

    internal static OpaqueId Id(string value) => new(value);

    private static BethesdaSemanticSnapshot EmptyBethesdaSnapshot() => new(
        SourceSnapshotId: Id("snapshot-indexes"),
        SchemaVersion: BethesdaSemanticContract.SchemaVersion,
        ProducerId: "bethesda-test",
        ProducerVersion: "1.0.0",
        DependencyFingerprint: new Sha256Fingerprint(new string('1', 64)),
        Plugins: [],
        OverrideChains: new Dictionary<string, BethesdaOverrideChain>(StringComparer.Ordinal),
        Winners: new Dictionary<string, BethesdaRecordContribution>(StringComparer.Ordinal),
        NpcContributions: [],
        RaceContributions: [],
        PlacedReferenceContributions: [],
        AllowlistedFields: [],
        ResolvedParticipants: new Dictionary<string, BethesdaResolvedParticipant>(StringComparer.Ordinal),
        Npcs: new Dictionary<string, BethesdaNpcFact>(StringComparer.Ordinal),
        Races: new Dictionary<string, BethesdaRaceFact>(StringComparer.Ordinal),
        PlacedReferences: new Dictionary<string, BethesdaPlacedReferenceFact>(StringComparer.Ordinal),
        Links: [],
        ReverseLinks: new Dictionary<string, IReadOnlyList<BethesdaLinkFact>>(StringComparer.Ordinal),
        FaceGen: [],
        Taxonomy: [],
        Coverage: [],
        Gaps: []);

    private static DocumentationEvidenceContract DocumentationEvidence()
    {
        OpaqueId run = Id("run-documentation");
        DocumentationEvidenceContract value = new(
            ContractConstants.DocumentationEvidenceSchemaId, new ContractVersion(1, 0, 0),
            Id("documentation-payload"), run,
            [new(Id("revision-1"), Id("source-1"), DocumentationSourceKind.Fixture, "1",
                new Sha256Fingerprint(new string('2', 64)), 1, Id("snapshot-indexes"),
                Slice5ResultState.Present, ReplayState.CompleteClean)],
            [new(Id("import-1"), run, Id("revision-1"), DocumentationImportMode.CleanImport, null,
                Id("closure-1"), Id("extractor-1"), LlmInvolvementState.None, LlmOperation.None,
                [
                    new("provider", BoundaryUseState.NotUsed, "local fixture"),
                    new("hosted-search", BoundaryUseState.NotUsed, "local fixture"),
                    new("nexus", BoundaryUseState.NotUsed, "local fixture"),
                    new("loot", BoundaryUseState.NotUsed, "local fixture"),
                ], new UtcTimestamp(DateTimeOffset.UnixEpoch))],
            [new(Id("passage-1"), Id("revision-1"), 0, 1,
                new Sha256Fingerprint(new string('3', 64)), Slice5ResultState.Present)],
            [new(Id("claim-1"), Id("import-1"), Id("passage-1"), ClaimKind.Requirement,
                "exact claim", [], EvidenceAuthority.AuthoritativeExternal,
                ClaimApplicabilityState.Applicable, ClassificationRole.Observed, [])],
            [new(Id("application-1"), Id("claim-1"), run, Id("context-1"),
                Id("subject-1"), "installed-entity", Id("closure-application"),
                ClaimApplicabilityState.Applicable, [Id("claim-1")])],
            [], [], [], []);
        return value with { PayloadId = DocumentationEvidenceIdentity.ComputePayloadId(value) };
    }

    private sealed class DeclarationThrowingCandidatePopulationSource(OpaqueId analyzerId)
        : ICandidatePopulationSource
    {
        public OpaqueId AnalyzerId => analyzerId;
        public AnalyzerDeclarationContract Declaration { get; } = CandidateAnalyzerDeclarations.Create(analyzerId);
        public IReadOnlyList<CausalJoinPopulationMember> DeclarePopulation(
            CandidatePopulationContext context,
            CancellationToken cancellationToken = default) =>
            throw new InvalidDataException("C:\\private\\fixture-answer.json: secret fixture content");
        public IReadOnlyList<CausalJoinPopulationMember> ConstructPopulation(
            CandidatePopulationContext context,
            CancellationToken cancellationToken = default) => [];
    }
}

[TestClass]
public sealed class FindingThresholdTests
{
    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void FindingThresholdLeavesAmbiguousCandidateAsHypothesisWithoutFindingPromotion()
    {
        CausalJoinPopulationMember ambiguous = CandidateSelectorTests.Member(
            "ambiguous", CandidateLane.MandatoryEvidence, CausalJoinInputState.Ambiguous) with
        {
            ContradictingEvidenceIds = [CandidateSelectorTests.Id("contradicting-evidence")],
        };
        OpaqueId analyzer = CandidateSelectorTests.Id("analyzer-test");
        CandidatePipelineResult result = CandidatePipeline.Execute(new CandidatePipelineRequest(
            CandidateSelectorTests.Id("run-test"), CandidateSelectorTests.Id("population-test"),
            CandidateSelectorTests.Id("policy-test"), CandidateSelectorTests.Id("threshold-test"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [new TestCandidatePopulationSource(analyzer, [ambiguous])]));

        Assert.AreEqual(Slice5ResultState.Ambiguous, result.Analysis.Candidates.Single().State);
        Assert.AreEqual(AnalysisConfidence.SpeculativeLead, result.Analysis.Hypotheses.Single().Confidence);
        Assert.IsFalse(result.Analysis.GetType().GetProperties().Any(property =>
            property.Name.Contains("Finding", StringComparison.Ordinal)
            || property.Name.Contains("Recommendation", StringComparison.Ordinal)
            || property.Name.Contains("Case", StringComparison.Ordinal)));
    }
}
