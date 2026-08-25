using Infinium.Domain.Contracts;

namespace Infinium.Analysis.ScopeReversion;

public sealed record ScopeReversionAssessment(
    string PolicyIdentity,
    string AnalyzerId,
    FindingSeverity Severity,
    string SeverityBasis,
    AnalysisConfidence Confidence,
    string ConfidenceBasis,
    AnalyzerMaturity AnalyzerMaturity,
    string CalibrationBoundary);

/// <summary>
/// Owns the provisional assessment labels for the one scope-reversion
/// analyzer family. The labels describe evidence within this analyzer's
/// bounded proof; they are not calibrated against other analyzers.
/// </summary>
public static class ScopeReversionAssessmentPolicy
{
    public const string PolicyIdentity =
        "infinium.scope-reversion-assessment-policy/1.0.0";

    public static ScopeReversionAssessment Assess(string analyzerId)
    {
        if (analyzerId != ScopeReversionAnalyzerDeclaration.AnalyzerId)
        {
            throw new InvalidOperationException(
                "The scope-reversion assessment policy cannot be inherited by another analyzer.");
        }
        return new(
            PolicyIdentity,
            analyzerId,
            FindingSeverity.Moderate,
            "The demonstrated consequence is meaningful, but remains bounded to the exact subjects and proof scope reported by this analyzer.",
            AnalysisConfidence.StronglySupported,
            "The supported finding has several exact evidence links and no material contradiction within that bounded proof scope.",
            AnalyzerMaturity.Experimental,
            "Severity and confidence are provisional analyzer-local labels; they are not calibrated across analyzers or problem types.");
    }
}
