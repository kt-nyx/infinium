using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Application.Candidates;
using Infinium.Application.Documentation;
using Infinium.Application.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;


public static partial class AnalysisPublicationBuilder
{
    private static CliSummaryDocumentContract BuildCliSummary(
        AnalysisV1WorkAssignment assignment,
        RunOutputContract output,
        DateTimeOffset endedAt)
    {
        long Duration() => Math.Max(0, (long)(endedAt - assignment.StartedAt).TotalMilliseconds);
        string outcome = assignment.TerminalOutcome switch
        {
            AnalysisTerminalOutcome.Completed => output.CoverageGaps.Count == 0 && output.Failures.Count == 0
                ? "completed" : "completed-with-gaps",
            AnalysisTerminalOutcome.CompletedWithGaps => "completed-with-gaps",
            AnalysisTerminalOutcome.Cancelled => "cancelled",
            AnalysisTerminalOutcome.LimitReached => "limit-reached",
            _ => "failed",
        };
        CliSummaryDocumentContract summary = new(
            ContractConstants.CliSummarySchemaId, "1", output.RunId, outcome,
            outcome switch
            {
                "completed" or "completed-with-gaps" => 0,
                "cancelled" => (int)CliExitCode.Cancelled,
                "limit-reached" => (int)CliExitCode.LimitReached,
                _ => (int)CliExitCode.Failed,
            },
            new TypedOutputCountsContract(
                output.Observations.Count, output.DeterministicResults.Count, output.ExternalClaims.Count,
                output.ApplicationLinks.Count, output.DiscoveryLeads.Count, output.ModelProposals.Count,
                output.ProposalAdmissions.Count, output.Candidates.Count, output.Hypotheses.Count,
                output.Findings.Count, output.Recommendations.Count, output.SupportedCases.Count,
                output.LeadOnlyCases.Count, output.Abstentions.Count, output.InvalidInputs.Count,
                output.CoverageGaps.Count, output.Failures.Count, output.DocumentationRevisions.Count,
                output.Passages.Count, output.CandidateDecisions.Count, output.ReconciliationAssessments.Count,
                output.LineageEvents.Count),
            new CoverageStateCountsContract(
                output.AnalyzerCoverage.Count(item => item.Status == "completed"),
                output.AnalyzerCoverage.Count(item => item.Status == "completed-with-gaps"),
                output.AnalyzerCoverage.Count(item => item.Status == "failed"),
                output.AnalyzerCoverage.Count(item => item.Status == "skipped-by-configuration"),
                output.AnalyzerCoverage.Count(item => item.Status == "skipped-by-limit"),
                output.AnalyzerCoverage.Count(item => item.Status == "unsupported")),
            Duration(), new CliCostContract(0, 0, 0, 0, 0, 0, 0, false),
            "no-readiness-evaluation", true);
        CliSummaryDocumentContractInvariants.Validate(summary);
        return summary;
    }

    private static List<ReplayDependencyNodeContract> BuildDependencies(
        AnalysisV1WorkAssignment assignment,
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases,
        string documentationSha,
        string candidateSha,
        string findingSha)
    {
        List<ReplayDependencyNodeContract> result = [];
        void Add(string id, string kind, string version, string fingerprint, AnalysisResultState state = AnalysisResultState.Present) =>
            result.Add(new ReplayDependencyNodeContract(new OpaqueId(id), kind, ContractVersion.Parse(version), new Sha256Fingerprint(fingerprint), state));
        Add(assignment.ExecutionInput.ExecutionInputId.Value, "execution-input", assignment.ExecutionInput.SchemaVersion.ToString(),
            Hash(AnalysisExecutionInputJsonCodec.Serialize(assignment.ExecutionInput)));
        foreach ((string kind, ArtifactReferenceContract value) in References(assignment.ExecutionInput))
        {
            // Retained documentation is also emitted below as the typed phase output.
            // Keep one replay node for that identity instead of assigning both the
            // generic source-input kind and the authoritative documentation kind.
            if (value.ArtifactId == documentation.PayloadId)
            {
                if (value.ArtifactVersion != documentation.SchemaVersion
                    || value.Fingerprint.Value != documentationSha
                    || value.Availability != "retained")
                {
                    throw new AnalysisIdentityDriftException(
                        $"Replay dependency identity '{value.ArtifactId.Value}' resolves to drifted retained metadata.");
                }
                continue;
            }
            Add(value.ArtifactId.Value, kind, value.ArtifactVersion.ToString(), value.Fingerprint.Value,
                value.Availability == "retained" ? AnalysisResultState.Present : AnalysisResultState.Unavailable);
        }
        Add(documentation.PayloadId.Value, "documentation-evidence", documentation.SchemaVersion.ToString(), documentationSha);
        Add(candidates.PayloadId.Value, "candidate-analysis", candidates.SchemaVersion.ToString(), candidateSha);
        Add(findingCases.PayloadId.Value, "finding-case", findingCases.SchemaVersion.ToString(), findingSha);
        Add(candidates.PolicyId.Value, "candidate-policy", "1.0.0", candidates.PolicyFingerprint.Value);
        Add(candidates.ThresholdId.Value, "candidate-threshold", "1.0.0", candidates.ThresholdFingerprint.Value);
        Add(candidates.LimitId.Value, "candidate-limit", "1.0.0", candidates.LimitFingerprint.Value);
        Add(findingCases.PromotionPolicyId.Value, "promotion-policy", findingCases.PromotionPolicyVersion.ToString(),
            Hash(Encoding.UTF8.GetBytes(findingCases.PromotionPolicyId.Value + "|" + findingCases.PromotionPolicyVersion)));
        Add(findingCases.ReconciliationPolicyId.Value, "reconciliation-policy", findingCases.ReconciliationPolicyVersion.ToString(),
            Hash(Encoding.UTF8.GetBytes(findingCases.ReconciliationPolicyId.Value + "|" + findingCases.ReconciliationPolicyVersion)));
        Add("fixture-seed-" + assignment.ExecutionInput.Seed, "fixture-seed", "1.0.0",
            Hash(Encoding.UTF8.GetBytes(assignment.ExecutionInput.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        foreach (AnalysisPhaseExecution phase in assignment.PhaseExecutions)
        {
            Add("phase-" + Hash(Encoding.UTF8.GetBytes(phase.PhaseId + "|" + phase.InputFingerprint))[..32],
                "analysis-phase", "1.0.0", phase.InputFingerprint);
        }
        if (assignment.AnalysisComposition is not null)
        {
            result.AddRange(AnalysisComposition.Dependencies(assignment.AnalysisComposition));
        }
        return result.GroupBy(item => item.DependencyId).Select(group =>
        {
            ReplayDependencyNodeContract first = group.First();
            if (group.Any(item => item != first))
            {
                throw new AnalysisIdentityDriftException(
                    $"Replay dependency identity '{group.Key.Value}' resolves to drifted retained metadata.");
            }
            return first;
        }).ToList();
    }

    internal static IReadOnlyList<ReplayDependencyNodeContract> BuildDependenciesForVerification(
        AnalysisV1WorkAssignment assignment,
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases) =>
        BuildDependencies(assignment, documentation, candidates, findingCases,
            assignment.DocumentationEvidence.Sha256,
            assignment.CandidateAnalysis.Sha256,
            assignment.FindingCase.Sha256);

    private static IEnumerable<(string Kind, ArtifactReferenceContract Value)> References(AnalysisExecutionInputContract value)
    {
        yield return ("analysis-context", value.AnalysisContext);
        yield return ("installation-snapshot", value.InstallationSnapshot);
        yield return ("bethesda-semantic-input", value.BethesdaSemanticInput);
        foreach (ArtifactReferenceContract item in value.SourceInputs)
        {
            yield return ("source-input", item);
        }
        foreach (ArtifactReferenceContract item in value.AnalyzerDeclarations)
        {
            yield return ("analyzer-declaration", item);
        }
        yield return ("effective-configuration", value.EffectiveConfiguration);
        yield return ("resolved-input-manifest", value.ResolvedInputManifest);
    }

    internal static string SemanticFingerprintForVerification(
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases) =>
        SemanticFingerprint(documentation, candidates, findingCases, CancellationToken.None);

    private static string SemanticFingerprint(
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases,
        CancellationToken cancellationToken) =>
        Hash(SemanticProjection(documentation, candidates, findingCases, cancellationToken));

    internal static byte[] SemanticProjectionForVerification(
        DocumentationEvidenceContract documentation,
        CandidateAnalysisContract candidates,
        FindingCaseContract findingCases) =>
        SemanticProjection(documentation, candidates, findingCases, CancellationToken.None);

}
