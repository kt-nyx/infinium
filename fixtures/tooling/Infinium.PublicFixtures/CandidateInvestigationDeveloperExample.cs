using Infinium.Application.Provider;

namespace Infinium.PublicFixtures;

/// <summary>
/// Answer-free, developer-owned examples for current candidate contract tests.
/// These objects are not semantic fixtures and carry no expected-answer authority.
/// </summary>
public static class CandidateInvestigationDeveloperExample
{
    private const string DigestA = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string DigestB = "2222222222222222222222222222222222222222222222222222222222222222";

    public static CandidateInvestigationExecutionInput Input()
    {
        CandidateEvidenceInput source = new(
            "evidence-source", "evidence-application-source", "source-acquisition",
            "source-admission", "source-application-link", "source-revision",
            "source-passage", "supporting", "available", DigestA)
        {
            SourceApplicationDecisionId = "source-application-decision",
            RootKind = "persisted-source-claim-application",
        };
        CandidateEvidenceInput host = new(
            "evidence-host", "evidence-application-host", "", "", "",
            "host-revision", "host-passage", "neutral", "available", DigestB)
        {
            RootKind = "frozen-host-evidence",
            EvidenceRootId = "host-evidence-root",
            ApplicabilityRecordId = "host-applicability-record",
        };
        return new(
            "infinium.llm.candidate-investigation-execution-input/v1", "1",
            "candidate-developer-example", "candidate-operation", "host-authorization",
            "analysis-run", "analysis-run-current", "analysis-run-current",
            "application-scope", "cost-scope", CandidateInvestigationPromptV1.Id,
            CandidateInvestigationPromptV1.Fingerprint,
            [
                new("context-source", "candidate-source", "hypothesis-source",
                    "The source-bound hypothesis is supported.", ["participant-source"], ["subject"],
                    ["causal-path-source"], "dependency-source", [source]),
                new("context-host", "candidate-host", "hypothesis-host",
                    "The host-rooted hypothesis requires support.", ["participant-host"], ["subject"],
                    ["causal-path-host"], "dependency-host", [host]),
            ]);
    }

    public static CandidateInvestigationRetainedTranscript Positive() => new(
        "transcript-positive", "candidate-operation", "context-source", "response-positive",
        "completed", DigestA, CandidateInvestigationPromptV1.Id,
        CandidateInvestigationPromptV1.Fingerprint,
        [new("proposal-positive", "candidate-source", "hypothesis-source",
            "The source-bound hypothesis is supported.", ["evidence-source"], [], [],
            "informational", "proposed", "developer-example")], [], [], true);

    public static CandidateInvestigationRetainedTranscript Unsupported() => new(
        "transcript-unsupported", "candidate-operation", "context-host", "response-unsupported",
        "completed", DigestB, CandidateInvestigationPromptV1.Id,
        CandidateInvestigationPromptV1.Fingerprint,
        [new("proposal-unsupported", "candidate-host", "hypothesis-host",
            "The host-rooted hypothesis requires support.", [], [], ["supporting-evidence"],
            "informational", "unsupported", "developer-example")], [], [], true);

    public static CandidateInvestigationRetainedTranscript NoModel() => new(
        "transcript-no-model", "candidate-operation", "context-host", "response-no-model",
        "not-used", DigestB, CandidateInvestigationPromptV1.Id,
        CandidateInvestigationPromptV1.Fingerprint, [], [], ["provider-not-used"], false);

    public static CandidateInvestigationRetainedTranscript Drift() => new(
        "transcript-drift", "candidate-operation", "context-host", "response-drift",
        "drift", DigestB, CandidateInvestigationPromptV1.Id,
        CandidateInvestigationPromptV1.Fingerprint, [], [], ["identity-drift"], true);

    public static CandidateInvestigationRetainedTranscript Unavailable() => new(
        "transcript-unavailable", "candidate-operation", "context-host", "response-unavailable",
        "unavailable", DigestB, CandidateInvestigationPromptV1.Id,
        CandidateInvestigationPromptV1.Fingerprint, [], [], ["provider-response-unavailable"], false);
}
