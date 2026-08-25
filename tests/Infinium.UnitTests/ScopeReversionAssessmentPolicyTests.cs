using Infinium.Analysis.ScopeReversion;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ScopeReversionAssessmentPolicyTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void PolicyMakesProvisionalBasisAndExperimentalMaturityVisible()
    {
        ScopeReversionAssessment assessment = ScopeReversionAssessmentPolicy.Assess(
            ScopeReversionAnalyzerDeclaration.AnalyzerId);

        Assert.AreEqual(FindingSeverity.Moderate, assessment.Severity);
        Assert.AreEqual(AnalysisConfidence.StronglySupported, assessment.Confidence);
        Assert.AreEqual(AnalyzerMaturity.Experimental, assessment.AnalyzerMaturity);
        StringAssert.Contains(assessment.SeverityBasis, "bounded");
        StringAssert.Contains(assessment.ConfidenceBasis, "exact evidence");
        StringAssert.Contains(assessment.CalibrationBoundary, "not calibrated");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AnotherAnalyzerCannotSilentlyInheritScopeReversionLabels()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            ScopeReversionAssessmentPolicy.Assess("infinium.another-analyzer"));
    }
}
