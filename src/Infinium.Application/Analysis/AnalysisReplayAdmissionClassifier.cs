namespace Infinium.Application.Analysis;

public sealed record AnalysisReplayAdmissionFailure(
    string Admission,
    string Reason,
    string Gap,
    string Replayability);

public static class AnalysisReplayAdmissionClassifier
{
    public static AnalysisReplayAdmissionFailure Classify(AnalysisIdentityDriftException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new AnalysisReplayAdmissionFailure(
            "rejected",
            "retained-dependency-identity-drift",
            "required-retained-dependency-unavailable",
            "not-currently-replayable");
    }
}
