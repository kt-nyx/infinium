using Infinium.Analysis.ScopeReversion;
using Infinium.Application.ScopeReversion;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ScopeReversionFaultTests
{
    [TestMethod]
    [TestCategory("Fault")]
    [TestCategory("Candidates")]
    [TestProperty("Category", "ScopeReversion")]
    public void FailureLimitAndUnsupportedStatesRemainExplicitAndNeverPromote()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionWorkAssignmentContract assignment = ScopeReversionComposition.Compose(fixture.Request);
        ScopeReversionMemberContract failure = assignment.Members[0] with
        {
            GapFailureState = ScopeGapFailureState.Failed,
            Issue = "bounded synthetic failure",
        };
        ScopeReversionMemberContract limited = assignment.Members[1] with
        {
            GapFailureState = ScopeGapFailureState.Limited,
            Issue = "member limit reached",
        };
        ScopeReversionMemberContract unsupported = assignment.Members[2] with
        {
            WinningState = assignment.Members[2].WinningState with
            {
                State = ScopeValueState.Unsupported,
                ComparableValue = null,
            },
            Issue = "field outside accepted subset",
            GapFailureState = ScopeGapFailureState.Gap,
        };
        ScopeReversionWorkAssignmentContract faultAssignment = assignment with
        {
            Members = [failure, limited, unsupported],
        };
        Sha256Fingerprint faultFingerprint = ScopeReversionContractInvariants.ComputeInputFingerprint(
            faultAssignment.OriginatingRunId,
            faultAssignment.Configuration,
            faultAssignment.Sources,
            faultAssignment.Members,
            faultAssignment.Analyzer.DeclarationFingerprint);
        faultAssignment = faultAssignment with
        {
            InputFingerprint = faultFingerprint,
            AssignmentId = ScopeReversionContractInvariants.ComputeAssignmentId(
                faultAssignment.OriginatingRunId, faultFingerprint),
        };
        ScopeReversionAnalysisContract analysis = ScopeReversionAnalyzer.Execute(faultAssignment);

        Assert.AreEqual(ScopeReversionDisposition.Failed, analysis.Decisions[0].Disposition);
        Assert.AreEqual(ScopeReversionDisposition.Limited, analysis.Decisions[1].Disposition);
        Assert.AreEqual(ScopeReversionDisposition.Unsupported, analysis.Decisions[2].Disposition);
        Assert.AreEqual(1, analysis.Failures.Count);
        Assert.AreEqual(2, analysis.Gaps.Count);
        Assert.AreEqual(0, analysis.Findings.Count);
        Assert.AreEqual(0, analysis.Cases.Count);
        Assert.AreEqual(0, analysis.Recommendations.Count);
    }

    [TestMethod]
    [TestCategory("Fault")]
    [TestCategory("Contracts")]
    [TestProperty("Category", "ScopeReversion")]
    public void DriftedAnalyzerFingerprintAndUnregisteredAdapterFailClosed()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionWorkAssignmentContract assignment = ScopeReversionComposition.Compose(fixture.Request);
        ScopeReversionWorkAssignmentContract drifted = assignment with
        {
            Analyzer = assignment.Analyzer with { AnalyzerVersion = new ContractVersion(2, 0, 0) },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionAnalyzer.Execute(drifted));
        ScopeReversionCompositionRequest unknown = fixture.Request with
        {
            EnabledAdapterIds = ["infinium.scope-reversion.adapter.unknown"],
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionComposition.Compose(unknown));

        ScopeReversionCompositionRequest sourceDrift = fixture.Request with
        {
            ExecutionInput = fixture.Request.ExecutionInput! with { SourceInputs = [] },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionComposition.Compose(sourceDrift));

        ArtifactReferenceContract admittedDeclaration = fixture.Request.ExecutionInput!.AnalyzerDeclarations.Single();
        ScopeReversionCompositionRequest declarationDrift = fixture.Request with
        {
            ExecutionInput = fixture.Request.ExecutionInput with
            {
                AnalyzerDeclarations =
                [
                    admittedDeclaration with { Fingerprint = new Sha256Fingerprint(new string('c', 64)) },
                ],
            },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionComposition.Compose(declarationDrift));

        ScopeReversionCompositionRequest configurationDrift = fixture.Request with
        {
            ExecutionInput = fixture.Request.ExecutionInput with
            {
                EffectiveConfiguration = fixture.Request.ExecutionInput.EffectiveConfiguration with
                {
                    Fingerprint = new Sha256Fingerprint(new string('d', 64)),
                },
            },
        };
        Assert.ThrowsExactly<InvalidDataException>(() => ScopeReversionComposition.Compose(configurationDrift));
    }
}
