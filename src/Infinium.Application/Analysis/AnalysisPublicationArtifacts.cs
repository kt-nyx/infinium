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
    private static TypedArtifactDocumentContract Typed(
        string id, string type, string state, ArtifactReferenceDocumentContract payload, ArtifactProvenanceDocumentContract provenance) =>
        new(id, 1, type, state, payload, provenance);

    private static ArtifactReferenceDocumentContract Reference(RetainedAnalysisPayloadSeal value) =>
        new(value.PayloadId, value.SchemaVersion, value.Sha256, "retained");

    private static ArtifactReferenceDocumentContract Reference(ArtifactReferenceContract value) =>
        new(value.ArtifactId.Value, value.ArtifactVersion.ToString(), value.Fingerprint.Value, value.Availability);

    private static void ValidateSeal(RetainedAnalysisPayloadSeal seal, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != seal.ByteLength || !StringComparer.Ordinal.Equals(Hash(bytes), seal.Sha256))
        {
            throw new InvalidDataException($"Retained payload '{seal.PayloadId}' failed exact identity admission.");
        }
    }

    private static string DecisionState(CandidateDecisionDisposition state) => state switch
    {
        CandidateDecisionDisposition.CandidateAdmitted => "present",
        CandidateDecisionDisposition.ResolvedNegative => "resolved-negative",
        CandidateDecisionDisposition.InvalidInput => "invalid-input",
        CandidateDecisionDisposition.Limited => "limit-reached",
        CandidateDecisionDisposition.Deferred or CandidateDecisionDisposition.Unprocessed => "partial",
        _ => Kebab(state),
    };

    private static string ClaimState(ClaimApplicabilityState state) => state switch
    {
        ClaimApplicabilityState.Applicable => "present",
        ClaimApplicabilityState.NotApplicable => "not-applicable",
        ClaimApplicabilityState.Unknown => "unknown",
        ClaimApplicabilityState.Unsupported => "unsupported",
        ClaimApplicabilityState.Contradicted => "rejected",
        _ => throw new InvalidDataException("Claim applicability is unspecified."),
    };

    internal static long CountOutputItems(RunOutputContract output) => checked(
        output.Observations.Count
        + output.DeterministicResults.Count
        + output.ExternalClaims.Count
        + output.ApplicationLinks.Count
        + output.DiscoveryLeads.Count
        + output.ModelProposals.Count
        + output.ProposalAdmissions.Count
        + output.Candidates.Count
        + output.Hypotheses.Count
        + output.Findings.Count
        + output.Recommendations.Count
        + output.SupportedCases.Count
        + output.LeadOnlyCases.Count
        + output.Abstentions.Count
        + output.InvalidInputs.Count
        + output.CoverageGaps.Count
        + output.Failures.Count
        + output.DocumentationRevisions.Count
        + output.Passages.Count
        + output.CandidateDecisions.Count
        + output.ReconciliationAssessments.Count
        + output.LineageEvents.Count
        + output.TaxonomyAssignments.Count
        + output.AnalyzerCoverage.Count);

    private static string TerminalState(AnalysisTerminalOutcome value) => value switch
    {
        AnalysisTerminalOutcome.Completed => "completed",
        AnalysisTerminalOutcome.CompletedWithGaps => "completed-with-gaps",
        AnalysisTerminalOutcome.Cancelled => "cancelled",
        AnalysisTerminalOutcome.LimitReached => "limit-reached",
        _ => "failed",
    };

    private static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString("O");
    private static string MissingDependencyGapId(string dependencyId) =>
        StableId("missing-dependency-gap", dependencyId);
    private static string Kebab<T>(T value) where T : struct, Enum => JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string StableId(string kind, params string[] parts) =>
        kind + "-" + Hash(Encoding.UTF8.GetBytes(string.Join('\n', parts)))[..32];
}
