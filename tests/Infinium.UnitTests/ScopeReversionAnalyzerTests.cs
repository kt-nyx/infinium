using Infinium.Analysis.ScopeReversion;
using Infinium.Application.ScopeReversion;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ScopeReversionAnalyzerTests
{
    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Candidates")]
    [TestProperty("Category", "ScopeReversion")]
    public void TotalityTableRejectsGapsAndOverlapsBeforeFixtures()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ScopeReversionAnalyzer.ValidateTotality([]));
        ScopeReversionDispositionRule always = new(
            "always", ScopeReversionDisposition.Abstained, _ => true);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ScopeReversionAnalyzer.ValidateTotality([always, always with { RuleId = "overlap" }]));

        ScopeReversionDisposition positive = ScopeReversionAnalyzer.Classify(new(
            ScopeTransitionKind.Absent,
            ScopeSupportState.Supported,
            ScopeApplicabilityState.Applicable,
            true,
            ScopeCoverageRelation.DoesNotCoverTransition,
            ScopeContradictionState.None,
            ScopeCausalClosureState.Closed,
            ScopePublicationEligibility.Eligible,
            CoverageMemberState.Completed,
            ScopeGapFailureState.None));
        Assert.AreEqual(ScopeReversionDisposition.SupportedFinding, positive);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Candidates")]
    [TestProperty("Category", "ScopeReversion")]
    public void PreregisteredPackageProducesExactPositiveNegativeAndAbstainedStates()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionPipelineResult result = ScopeReversionComposition.Execute(fixture.Request);

        Assert.AreEqual(fixture.Expectations.Aggregate.Population, result.Analysis.Counts.Population);
        Assert.AreEqual(fixture.Expectations.Aggregate.SupportedFindings, result.Analysis.Counts.SupportedFindings);
        Assert.AreEqual(fixture.Expectations.Aggregate.ResolvedNegative, result.Analysis.Counts.ResolvedNegative);
        Assert.AreEqual(fixture.Expectations.Aggregate.Abstentions, result.Analysis.Counts.Abstentions);
        Assert.AreEqual(fixture.Expectations.Aggregate.Findings, result.Analysis.Counts.Findings);
        Assert.AreEqual(fixture.Expectations.Aggregate.Cases, result.Analysis.Counts.Cases);
        Assert.AreEqual(fixture.Expectations.Aggregate.Recommendations, result.Analysis.Counts.Recommendations);
        Assert.AreEqual(0, result.Analysis.Boundaries.Count(item => item.State != BoundaryUseState.NotUsed));
        foreach (ScopeReversionExpectedMember expected in fixture.Expectations.Expected)
        {
            ScopeReversionDecisionContract decision = result.Analysis.Decisions.Single(
                item => item.MemberId.Value == expected.MemberId);
            Assert.AreEqual(expected.Disposition, ScopeReversionTestSupport.Kebab(decision.Disposition), expected.MemberId);
            Assert.AreEqual(expected.Findings, result.Analysis.Findings.Count(item => item.MemberId.Value == expected.MemberId));
            Assert.AreEqual(expected.Cases, result.Analysis.Cases.Count(item => item.CandidateId ==
                result.Analysis.Candidates.Single(candidate => candidate.MemberId.Value == expected.MemberId).CandidateId));
        }

        Assert.IsTrue(result.Analysis.Findings.All(item =>
            item.Confidence == AnalysisConfidence.StronglySupported && item.Severity == FindingSeverity.Moderate));
        Assert.AreEqual(2, result.Analysis.Candidates.Count(item => item.State == ScopeCandidateState.ResolvedNegative));
        Assert.AreEqual(2, result.Analysis.Hypotheses.Count(item => item.State == ScopeHypothesisState.ResolvedRejected));
        Assert.AreEqual(2, result.Analysis.Contradictions.Count);
        Assert.AreEqual(2, result.Analysis.Candidates.Count(item => item.State == ScopeCandidateState.Ambiguous));
        Assert.AreEqual(2, result.Analysis.Abstentions.Count);
        Assert.AreEqual(4, result.Analysis.Taxonomy.Count(item =>
            item.Applicability == ScopeTaxonomyApplicability.NotApplicable));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Candidates")]
    [TestProperty("Category", "ScopeReversion")]
    public void AdaptersCanBeEnabledIndependentlyAndDisabledCoverageIsTruthful()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionCompositionRequest actorOnly = fixture.Request with
        {
            EnabledAdapterIds = [ActorScopeReversionAdapter.StableAdapterId],
        };
        ScopeReversionConfigurationContract actorOnlyConfiguration = new(
            actorOnly.ConfigurationId,
            new Sha256Fingerprint(new string('0', 64)),
            [ActorScopeReversionAdapter.StableAdapterId, PlacedReferenceScopeReversionAdapter.StableAdapterId],
            actorOnly.EnabledAdapterIds,
            actorOnly.ExecutionInput!.Limits.MaximumEntities,
            actorOnly.ExecutionInput.Limits.MaximumOutputItems,
            actorOnly.ExecutionInput.Limits.MaximumWallTimeMilliseconds);
        actorOnly = actorOnly with
        {
            ExecutionInput = actorOnly.ExecutionInput with
            {
                EffectiveConfiguration = actorOnly.ExecutionInput.EffectiveConfiguration with
                {
                    Fingerprint = ScopeReversionContractInvariants.ComputeConfigurationFingerprint(
                        actorOnlyConfiguration),
                },
            },
        };
        ScopeReversionAnalysisContract result = ScopeReversionComposition.Execute(actorOnly).Analysis;

        Assert.AreEqual(3, result.Counts.Population);
        Assert.IsTrue(result.Decisions.All(item => item.MemberId.Value.StartsWith("actor-", StringComparison.Ordinal)));
        ScopeReversionCoverageContract reference = result.Coverage.Single(item => item.PopulationId == "reference-transition");
        Assert.AreEqual(3, reference.Denominator);
        Assert.AreEqual(3, reference.SkippedByConfiguration);
        Assert.AreEqual(0, reference.Completed);
        StringAssert.Contains(result.PublicationClaimBoundary, "exact members and coverage populations");
        Assert.IsFalse(result.PublicationClaimBoundary.Contains("both", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Cases")]
    [TestProperty("Category", "ScopeReversion")]
    public void SharedCausalClosureRetainsDistinctPhysicalCasesUnderOneLogicalGroup()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ActorScopeReversionInput actor = fixture.Request.ActorInputs.Single(item =>
            item.MemberId.Value == "actor-positive");
        ScopeReversionCompositionRequest request = fixture.Request with
        {
            ReferenceInputs = fixture.Request.ReferenceInputs.Select(item =>
                item.MemberId.Value == "reference-positive"
                    ? item with { DependencyClosureId = actor.DependencyClosureId }
                    : item).ToArray(),
        };

        ScopeReversionAnalysisContract analysis = ScopeReversionComposition.Execute(request).Analysis;
        Assert.AreEqual(2, analysis.Cases.Count);
        Assert.AreEqual(2, analysis.Cases.Select(item => item.CaseId).Distinct().Count());
        Assert.AreEqual(1, analysis.Cases.Select(item => item.LogicalCaseId).Distinct().Count());
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Candidates")]
    [TestProperty("Category", "ScopeReversion")]
    public void MetamorphicReorderAndUnrelatedSourceChangesPreserveSemanticProjection()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionAnalysisContract baseline = ScopeReversionComposition.Execute(fixture.Request).Analysis;
        ScopeReversionSourceBindingContract unrelatedSource = new(
            new("unrelated-source"), "infinium.synthetic.unrelated/v1", new ContractVersion(1, 0, 0),
            new Sha256Fingerprint(new string('b', 64)), "retained");
        ScopeReversionCompositionRequest transformed = fixture.Request with
        {
            ActorInputs = fixture.Request.ActorInputs.Reverse().ToArray(),
            ReferenceInputs = fixture.Request.ReferenceInputs.Reverse().ToArray(),
            Sources = fixture.Request.Sources.Append(unrelatedSource).ToArray(),
            ExecutionInput = fixture.Request.ExecutionInput! with
            {
                SourceInputs = fixture.Request.ExecutionInput!.SourceInputs.Append(new ArtifactReferenceContract(
                    unrelatedSource.ArtifactId, unrelatedSource.SchemaVersion,
                    unrelatedSource.Fingerprint, unrelatedSource.Availability)).ToArray(),
            },
        };
        ScopeReversionAnalysisContract changed = ScopeReversionComposition.Execute(transformed).Analysis;

        CollectionAssert.AreEqual(
            baseline.Decisions.Select(item => (item.MemberId.Value, item.Transition, item.PurposeCoverage, item.Disposition)).ToArray(),
            changed.Decisions.Select(item => (item.MemberId.Value, item.Transition, item.PurposeCoverage, item.Disposition)).ToArray());
        CollectionAssert.AreEqual(
            baseline.Cases.Select(item => item.LogicalCaseId.Value).ToArray(),
            changed.Cases.Select(item => item.LogicalCaseId.Value).ToArray());
        Assert.AreNotEqual(baseline.InputFingerprint, changed.InputFingerprint);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Candidates")]
    [TestProperty("Category", "ScopeReversion")]
    public void RelevantWinnerRestoreAndCreationResolveNegativeWithoutPromotion()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ActorScopeReversionInput positive = fixture.Request.ActorInputs.Single(item => item.MemberId.Value == "actor-positive");
        ScopeReversionCompositionRequest restoredRequest = fixture.Request with
        {
            ActorInputs = [positive with { WinningPackageId = positive.PriorPackageId }],
            ReferenceInputs = [],
        };
        ScopeReversionAnalysisContract restored = ScopeReversionComposition.Execute(restoredRequest).Analysis;
        Assert.AreEqual(ScopeTransitionKind.Unchanged, restored.Decisions.Single().Transition);
        Assert.AreEqual(ScopeReversionDisposition.ResolvedNegative, restored.Decisions.Single().Disposition);
        Assert.AreEqual(0, restored.Findings.Count);

        ScopeReversionCompositionRequest createdRequest = restoredRequest with
        {
            ActorInputs = [positive with { PriorPackageId = null, WinningPackageId = "created-package" }],
        };
        ScopeReversionAnalysisContract created = ScopeReversionComposition.Execute(createdRequest).Analysis;
        Assert.AreEqual(ScopeTransitionKind.Created, created.Decisions.Single().Transition);
        Assert.AreEqual(ScopeReversionDisposition.ResolvedNegative, created.Decisions.Single().Disposition);
        Assert.AreEqual(0, created.Findings.Count);

        ScopeReversionCompositionRequest unchangedPurposeRequest = restoredRequest with
        {
            ActorInputs =
            [
                positive with
                {
                    WinningPackageId = null,
                    WinningAppearanceFingerprint = positive.PriorAppearanceFingerprint,
                },
            ],
        };
        ScopeReversionAnalysisContract unchangedPurpose = ScopeReversionComposition.Execute(unchangedPurposeRequest).Analysis;
        Assert.AreEqual(ScopeTransitionKind.Absent, unchangedPurpose.Decisions.Single().Transition);
        Assert.AreEqual(ScopeReversionDisposition.ResolvedNegative, unchangedPurpose.Decisions.Single().Disposition);
        Assert.AreEqual(0, unchangedPurpose.Findings.Count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [TestCategory("Fault")]
    [TestProperty("Category", "ScopeReversion")]
    public void MalformedNeutralStateIsRejectedAtomically()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionWorkAssignmentContract assignment = ScopeReversionComposition.Compose(fixture.Request);
        ScopeReversionMemberContract malformed = assignment.Members[0] with
        {
            PriorEffectiveState = assignment.Members[0].PriorEffectiveState with
            {
                State = ScopeValueState.Invalid,
                ComparableValue = null,
            },
        };
        assignment = assignment with { Members = [malformed, .. assignment.Members.Skip(1)] };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionAnalyzer.Execute(assignment));
    }
}
