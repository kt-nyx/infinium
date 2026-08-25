using Infinium.Application.Analysis;
using Infinium.Application.ScopeReversion;
using Infinium.Application.Serialization;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ScopeReversionV2AnalyzerTests
{
    private static readonly string[] ActorPositiveCoverageIds =
    [
        "actor-positive", "analyzer", "persistence", "projection", "purpose", "replay", "taxonomy",
    ];

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void FindingReportsKeepGapSubjectsAndReportIdentitiesDistinct()
    {
        ScopeReversionV2AnalysisContract analysis = ControlledRealScopeReversionProjector.Execute(
            ScopeReversionV2TestSupport.Request(ScopeReversionV2SubjectKind.PlacedReference)).Analysis;

        IReadOnlyList<FindingReportDocument> reports = FindingReportProjection.Project(analysis);
        FindingReportDocument[] gapReports = reports
            .Where(item => item.State == FindingReportState.CoverageGap)
            .ToArray();

        Assert.HasCount(analysis.Gaps.Count, gapReports);
        CollectionAssert.AreEquivalent(
            analysis.Gaps.Select(item => item.MemberId.Value).ToArray(),
            gapReports.Select(item => item.SubjectId.Value).ToArray());
        Assert.AreEqual(reports.Count, reports.Select(item => item.ReportId).Distinct().Count());
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void V2ContractRoundTripsCanonicallyAndRejectsDanglingMember()
    {
        ScopeReversionV2PipelineResult result = ControlledRealScopeReversionProjector.Execute(ScopeReversionV2TestSupport.Request());
        byte[] second = ScopeReversionV2JsonCodec.Serialize(ScopeReversionV2JsonCodec.Deserialize(result.CanonicalJson));
        CollectionAssert.AreEqual(result.CanonicalJson, second);
        Assert.AreEqual(new ContractVersion(2, 0, 0), result.Analysis.Analyzer.AnalyzerVersion);
        Assert.AreEqual(new ContractVersion(1, 0, 0), result.Analysis.Analyzer.RulesetVersion);
        ScopeReversionV2AnalysisContract dangling = result.Analysis with
        {
            Cases = [result.Analysis.Cases[0] with { SubjectId = new OpaqueId("missing-subject") }],
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionV2JsonCodec.Serialize(dangling));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void ActorCohortAggregatesTwoMembersIntoOneFindingAndCase()
    {
        ScopeReversionV2PipelineResult result = ControlledRealScopeReversionProjector.Execute(ScopeReversionV2TestSupport.Request());
        Assert.HasCount(2, result.Analysis.Members);
        Assert.HasCount(1, result.Analysis.Candidates);
        Assert.HasCount(1, result.Analysis.Hypotheses);
        Assert.AreEqual(ScopeHypothesisState.Present, result.Analysis.Hypotheses[0].State);
        Assert.HasCount(1, result.Analysis.Findings);
        Assert.HasCount(1, result.Analysis.Cases);
        Assert.HasCount(1, result.Analysis.Recommendations);
        Assert.IsTrue(result.Analysis.Members.All(item => item.ResidualFacts.Contains("AIDT retained outside bounded claim")));
        CollectionAssert.AreEqual(
            ActorPositiveCoverageIds,
            result.Analysis.Coverage.Select(item => item.PopulationId).ToArray());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void MatchedControlsAndIncompletePurposeDoNotPublishFindings()
    {
        ScopeReversionV2AnalysisContract control = ControlledRealScopeReversionProjector.Execute(
            ScopeReversionV2TestSupport.Request(restored: true)).Analysis;
        Assert.AreEqual(ScopeReversionDisposition.ResolvedNegative, control.Decisions.Single().Disposition);
        CollectionAssert.Contains(control.Coverage.Select(item => item.PopulationId).ToArray(), "actor-control");
        Assert.IsEmpty(control.Findings);
        Assert.IsEmpty(control.Cases);

        ScopeReversionV2AnalysisContract incomplete = ControlledRealScopeReversionProjector.Execute(
            ScopeReversionV2TestSupport.Request(admitted: false)).Analysis;
        Assert.AreEqual(ScopeReversionDisposition.Abstained, incomplete.Decisions.Single().Disposition);
        Assert.IsEmpty(incomplete.Findings);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void ReferenceProjectionIsGenericAndUnavailableTypedInputAbstains()
    {
        ScopeReversionV2AnalysisContract positive = ControlledRealScopeReversionProjector.Execute(
            ScopeReversionV2TestSupport.Request(ScopeReversionV2SubjectKind.PlacedReference)).Analysis;
        Assert.HasCount(1, positive.Findings);
        Assert.IsTrue(positive.Gaps.Any(item => item.Reason.Contains("runtime", StringComparison.Ordinal)));

        ScopeReversionV2AnalysisContract unavailable = ControlledRealScopeReversionProjector.Execute(
            ScopeReversionV2TestSupport.Request(ScopeReversionV2SubjectKind.PlacedReference, includeWinning: false)).Analysis;
        Assert.AreEqual(ScopeReversionDisposition.Unsupported, unavailable.Decisions.Single().Disposition);
        Assert.IsTrue(unavailable.Gaps.Any(item => item.Reason.Contains("unavailable", StringComparison.Ordinal)));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void DisplayNamesAndUnrelatedSnapshotOrderDoNotAffectIdentity()
    {
        ScopeReversionV2ProjectionRequest baselineRequest = ScopeReversionV2TestSupport.Request();
        ScopeReversionV2AnalysisContract baseline = ControlledRealScopeReversionProjector.Execute(baselineRequest).Analysis;
        BethesdaSemanticSnapshot reordered = baselineRequest.Snapshot with
        {
            NpcContributions = baselineRequest.Snapshot.NpcContributions.Reverse().ToArray(),
            ProducerId = "renamed display-only producer",
        };
        ScopeReversionV2AnalysisContract transformed = ControlledRealScopeReversionProjector.Execute(
            baselineRequest with { Snapshot = reordered }).Analysis;
        Assert.AreEqual(baseline.AssignmentId, transformed.AssignmentId);
        Assert.AreEqual(baseline.PayloadId, transformed.PayloadId);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void FalseCauseSharingAndCrossDomainShapeAreRejected()
    {
        ScopeReversionV2ProjectionRequest request = ScopeReversionV2TestSupport.Request();
        ScopeReversionV2WorkAssignmentContract assignment = ControlledRealScopeReversionProjector.Project(request);
        ScopeReversionV2WorkAssignmentContract falseCause = assignment with
        {
            Members = [assignment.Members[0] with { DependencyCauseId = new OpaqueId("other-cause") }, .. assignment.Members.Skip(1)],
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionV2Contract.Validate(falseCause));
        ScopeReversionV2WorkAssignmentContract crossDomain = assignment with
        {
            Members = [assignment.Members[0] with { AdapterId = ScopeReversionV2Contract.ReferenceAdapterId }, .. assignment.Members.Skip(1)],
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionV2Contract.Validate(crossDomain));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void SourcePassageDriftChangesIdentityAndMissingOrInapplicablePurposeAbstains()
    {
        ScopeReversionV2ProjectionRequest baselineRequest = ScopeReversionV2TestSupport.Request();
        ScopeReversionV2AnalysisContract baseline = ControlledRealScopeReversionProjector.Execute(baselineRequest).Analysis;
        ScopeReversionV2SourceDecisionContract source = baselineRequest.SourceDecisions.Single();
        ScopeReversionV2AnalysisContract fingerprintDrift = ControlledRealScopeReversionProjector.Execute(
            baselineRequest with
            {
                SourceDecisions = [source with { PassageFingerprint = ContractJsonSerializer.Fingerprint("changed passage") }],
            }).Analysis;
        Assert.AreNotEqual(baseline.AssignmentId, fingerprintDrift.AssignmentId);

        ScopeReversionV2AnalysisContract inapplicable = ControlledRealScopeReversionProjector.Execute(
            baselineRequest with
            {
                SourceDecisions = [source with
                {
                    SourceRevision = "non-applicable-version",
                    ApplicabilityState = SemanticApplicabilityState.NotApplicable,
                    DecisionState = SemanticDecisionState.Abstained,
                }],
            }).Analysis;
        Assert.AreEqual(ScopeReversionDisposition.Abstained, inapplicable.Decisions.Single().Disposition);
        Assert.IsEmpty(inapplicable.Findings);

        ScopeReversionV2ProjectionRequest missing = baselineRequest with { SourceDecisions = [] };
        Assert.ThrowsExactly<InvalidDataException>(() => ControlledRealScopeReversionProjector.Project(missing));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Metamorphic")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void IrrelevantInputIdentityChangesDoNotChangeDecisionButRelevantWinnerDoes()
    {
        ScopeReversionV2ProjectionRequest baselineRequest = ScopeReversionV2TestSupport.Request();
        ScopeReversionV2AnalysisContract baseline = ControlledRealScopeReversionProjector.Execute(baselineRequest).Analysis;
        BethesdaSemanticSnapshot unrelated = baselineRequest.Snapshot with
        {
            DependencyFingerprint = ContractJsonSerializer.Fingerprint("unrelated plugin addition"),
            NpcContributions = baselineRequest.Snapshot.NpcContributions.Reverse().ToArray(),
        };
        ScopeReversionV2AnalysisContract transformed = ControlledRealScopeReversionProjector.Execute(
            baselineRequest with { Snapshot = unrelated }).Analysis;
        Assert.AreEqual(baseline.Decisions.Single().Disposition, transformed.Decisions.Single().Disposition);
        Assert.AreNotEqual(baseline.ExecutionInputId, transformed.ExecutionInputId);

        ScopeReversionV2AnalysisContract restored = ControlledRealScopeReversionProjector.Execute(
            ScopeReversionV2TestSupport.Request(restored: true)).Analysis;
        Assert.AreEqual(ScopeReversionDisposition.ResolvedNegative, restored.Decisions.Single().Disposition);
        Assert.AreNotEqual(baseline.PayloadId, restored.PayloadId);
    }

    [TestMethod]
    [TestCategory("Contract")]
    [TestCategory("Mutation")]
    [TestProperty("Category", "ScopeReversionV2")]
    public void CohortDuplicationRemovalAndOrderDriftFailClosed()
    {
        ScopeReversionV2WorkAssignmentContract assignment = ControlledRealScopeReversionProjector.Project(
            ScopeReversionV2TestSupport.Request());
        ScopeReversionV2SubjectContract subject = assignment.Subjects.Single();
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionV2Contract.Validate(
            assignment with { Subjects = [subject with { OrderedMemberIds = [subject.OrderedMemberIds[0], subject.OrderedMemberIds[0]] }] }));
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionV2Contract.Validate(
            assignment with { Subjects = [subject with { OrderedMemberIds = [subject.OrderedMemberIds[0]] }] }));
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionV2Contract.Validate(
            assignment with { Subjects = [subject with { OrderedMemberIds = subject.OrderedMemberIds.Reverse().ToArray() }] }));
    }
}
