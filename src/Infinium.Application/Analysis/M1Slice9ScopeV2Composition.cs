using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;

/// <summary>
/// Projects exact, already-retained Slice 8 v2 results into the frozen v1
/// publication collections. This performs no source acquisition and does not
/// interpret controlled payload bytes.
/// </summary>
public static class M1Slice9ScopeV2Composition
{
    private static readonly HashSet<string> ExactControlledOutputHashes = new(StringComparer.Ordinal)
    {
        "e9e28d12582e848337fb932fab4706046330c3ba5a5b73b3fa94abb7c91006b4",
        "978e4b2ad240643eb98461d9240b95de71ae77b8431c9f43323c69cb6338569a",
        "32899c5c930cd340e2aa6ab98eea9939ab7b1039b35d0d474ab0c0df44fce5fd",
        "e1b104ceb727e91207163f24e2ee5b7dec8d6affcee4042fe73f5f67dd64d6ab",
    };

    public static M1Slice9CompositionEnvelope CreateControlled(
        IReadOnlyList<byte[]> exactRetainedPayloads,
        IReadOnlyList<M1Slice9RetainedDependency> identityReceipts)
    {
        ArgumentNullException.ThrowIfNull(exactRetainedPayloads);
        ArgumentNullException.ThrowIfNull(identityReceipts);
        if (exactRetainedPayloads.Count != 4)
        {
            throw new InvalidDataException("Controlled Slice 9 composition requires exactly four retained Slice 8 results.");
        }
        ScopeReversionV2AnalysisContract[] results = exactRetainedPayloads
            .Select(bytes => ScopeReversionV2JsonCodec.Deserialize(bytes)).ToArray();
        foreach (ScopeReversionV2AnalysisContract result in results)
        {
            ScopeReversionV2Contract.Validate(result);
        }
        string[] hashes = exactRetainedPayloads.Select(Hash).ToArray();
        if (!ExactControlledOutputHashes.SetEquals(hashes)
            || results.Any(result => result.InputHandoffId != M1Slice9Composition.ControlledHandoffId
                || result.InputManifestFingerprint.Value != M1Slice9Composition.ControlledManifestSha256
                || result.PartitionRole != ScopeReversionV2PartitionRole.ControlledRealDevelopment)
            || results.SelectMany(result => result.PublicManifests)
                .Select(item => (item.RepositoryPath, item.ByteLength, item.Sha256.Value)).Distinct().Count() != 3
            || results.SelectMany(result => result.ControlledInputs)
                .Select(item => (item.RelativePath, item.ByteLength, item.Sha256.Value)).Distinct().Count() != 26)
        {
            throw new InvalidDataException("The retained controlled Slice 8 result family drifted from the activated identity, partition, or counts.");
        }

        List<M1Slice9ComposedArtifact> artifacts = [];
        List<M1Slice9TaxonomyProjection> taxonomy = [];
        List<M1Slice9CoverageProjection> coverage = [];
        List<M1Slice9RetainedDependency> dependencies = [];
        List<string> partitionTransitions = [];
        LlmInvolvementDocumentContract noLlm = new("none", "none", null);
        foreach ((ScopeReversionV2AnalysisContract result, byte[] bytes, string sha) in
                 results.Zip(exactRetainedPayloads, (result, bytes) => (result, bytes))
                     .Zip(hashes, (pair, sha) => (pair.result, pair.bytes, sha)))
        {
            string Q(string id) => result.PayloadId.Value + "." + id;
            Dictionary<string, string> subjectTypes = result.Subjects.ToDictionary(
                item => item.SubjectId.Value,
                item => Kebab(item.Kind),
                StringComparer.Ordinal);
            ArtifactReferenceDocumentContract payload = new(
                result.PayloadId.Value, result.SchemaVersion.ToString(), sha, "retained");
            ArtifactReferenceDocumentContract[] sources = [payload];
            M1Slice9ComposedArtifact Artifact(
                string collection, string id, string state, IEnumerable<string>? support = null,
                IEnumerable<string>? contradict = null) => new(
                    collection, Q(id), 1, state, payload, result.Analyzer.AnalyzerId,
                    result.Analyzer.AnalyzerVersion.ToString(), result.OriginatingRunId.Value,
                    sources, support?.Select(Q).ToArray() ?? [],
                    contradict?.Select(Q).ToArray() ?? [], noLlm);

            dependencies.Add(new(
                result.PayloadId.Value, "scope-reversion-v2", result.SchemaVersion.ToString(), sha, bytes.LongLength));
            artifacts.AddRange(result.Subjects.Select(item => Artifact(
                "observations", item.SubjectId.Value, "present", item.OrderedMemberIds.Select(id => id.Value))));
            artifacts.AddRange(result.Members.Select(item => Artifact(
                "deterministic_results", item.MemberId.Value, "present", item.EvidenceIds.Select(id => id.Value))));
            foreach (ScopeReversionV2SourceDecisionContract item in result.SourceDecisions)
            {
                string state = item.DecisionState == SemanticDecisionState.Admitted ? "present" : Kebab(item.DecisionState);
                artifacts.Add(Artifact("external_claims", item.DecisionId.Value + ".claim", state,
                    item.EvidenceIds.Select(id => id.Value)));
                artifacts.Add(Artifact("application_links", item.DecisionId.Value + ".application", state,
                    item.EvidenceIds.Select(id => id.Value)));
            }
            artifacts.AddRange(result.Decisions.Select(item => Artifact(
                "candidate_decisions", item.DecisionId.Value, Disposition(item.Disposition),
                item.EvidenceIds.Select(id => id.Value))));
            artifacts.AddRange(result.Candidates.Select(item => Artifact(
                "candidates", item.CandidateId.Value, CandidateState(item.State),
                item.SupportingEvidenceIds.Select(id => id.Value),
                item.ContradictingEvidenceIds.Select(id => id.Value))));
            artifacts.AddRange(result.Hypotheses.Select(item => Artifact(
                "hypotheses", item.HypothesisId.Value, HypothesisState(item.State),
                item.SupportingEvidenceIds.Select(id => id.Value),
                item.ContradictingEvidenceIds.Select(id => id.Value))));
            artifacts.AddRange(result.Findings.Select(item => Artifact(
                "findings", item.FindingId.Value, "present", item.EvidenceIds.Select(id => id.Value))));
            artifacts.AddRange(result.Cases.Select(item => Artifact(
                item.FindingId is null ? "lead_only_cases" : "supported_cases",
                item.CaseId.Value, "present", item.EvidenceIds.Select(id => id.Value))));
            artifacts.AddRange(result.Recommendations.Select(item => Artifact(
                "recommendations", item.RecommendationId.Value, "present", item.EvidenceIds.Select(id => id.Value))));
            artifacts.AddRange(result.Gaps.Select(item => Artifact(
                "coverage_gaps", item.GapId.Value, GapState(item.State))));
            artifacts.AddRange(result.PartitionTransitions.Select(item => Artifact(
                "lineage_events", item.TransitionId.Value, "present")));

            taxonomy.AddRange(result.Taxonomy.Select(item => new M1Slice9TaxonomyProjection(
                Q(item.AssignmentId.Value), subjectTypes[item.SubjectId.Value], Q(item.SubjectId.Value),
                item.Axis, item.Facet, item.Code, Kebab(item.Applicability), Kebab(item.Role),
                item.EvidenceIds.Select(id => Q(id.Value)).ToArray(), item.Reason,
                result.Analyzer.AnalyzerId, result.Analyzer.AnalyzerVersion.ToString(),
                result.OriginatingRunId.Value)));
            coverage.AddRange(result.Coverage.Select(item => new M1Slice9CoverageProjection(
                Q("coverage." + item.PopulationId), result.Analyzer.AnalyzerId,
                Q(item.PopulationId), item.PopulationId, item.Denominator,
                checked(item.Completed + item.CompletedWithGaps),
                item.Failed > 0 ? "failed" : item.CompletedWithGaps > 0 ? "completed-with-gaps"
                    : item.Unsupported > 0 ? "unsupported" : "completed",
                result.Gaps.Where(gap => gap.PopulationId == item.PopulationId)
                    .Select(gap => Q(gap.GapId.Value)).ToArray(), [], [])));
            partitionTransitions.AddRange(result.PartitionTransitions.Select(item =>
                $"{item.TransitionId.Value}:{Kebab(item.FromRole)}-to-{Kebab(item.ToRole)}"));
        }
        dependencies.AddRange(identityReceipts);

        ArtifactReferenceDocumentContract composed = new(
            "m1-s6-composed-evidence", "2.0.0",
            "901f278825d3fdbab2971b9f6bb4462f84c12dea96f1c14c8f222d1f29a1df9d", "retained");
        ArtifactReferenceDocumentContract providerAttempt = new(
            "m1-s6-wp10-attempt-evidence", "3.0.0",
            "5aa6391aba25fbdfcd5470e3dc0db9c8a108b392f14e3c024bf883d214d4b0af", "retained");
        artifacts.Add(new(
            "model_proposals", "m1-s9-controlled-retained-model-proposal", 1, "present",
            providerAttempt, "infinium.provider.source-claim", "1.0.0", "m1-s6-wp10-live-run",
            [providerAttempt], [], [],
            new("proposal-retained", "source-claim-extraction", "wp10-attempt-2-development-c4f6aa8")));
        artifacts.Add(new(
            "proposal_admissions", "m1-s9-controlled-retained-proposal-admission", 1, "present",
            composed, "infinium.provider.host-admission", "1.0.0", "m1-s6-wp10-live-run",
            [providerAttempt], ["m1-s9-controlled-retained-model-proposal"], [],
            new("proposal-admitted", "source-claim-extraction", "wp10-attempt-2-development-c4f6aa8")));
        dependencies.AddRange([
            new("m1-s6-composed-evidence", "retained-provider-composition", "2.0.0",
                "901f278825d3fdbab2971b9f6bb4462f84c12dea96f1c14c8f222d1f29a1df9d", 8653),
            new("m1-s6-wp9-attempt-evidence", "retained-provider-attempt", "3.0.0",
                "6f51bc6d28799711e7d62d5e67ef7965d2be6d72d9c3453ec16f7e9cfbbc1270", 6292),
            new("m1-s6-wp10-attempt-evidence", "retained-provider-attempt", "3.0.0",
                "5aa6391aba25fbdfcd5470e3dc0db9c8a108b392f14e3c024bf883d214d4b0af", 6312),
            new("m1-s6-wp11-attempt-evidence", "retained-provider-attempt", "3.0.0",
                "eaddaac9644359c1fe45bd0b726574b037f38e2449db50f51757ce03190d16ca", 6313),
        ]);

        Dictionary<string, string> effects = new(StringComparer.Ordinal)
        {
            ["provider"] = "not-used",
            ["model"] = "not-used",
            ["credential"] = "not-used",
            ["dns"] = "not-used",
            ["network"] = "not-used",
            ["billable"] = "not-used",
            ["live"] = "not-used",
            ["source-refresh"] = "not-used",
        };
        M1Slice9CompositionEnvelope envelope = new(
            1, "m1-s9-controlled-real-composition", "M1-S9-CONTROLLED-REAL-v1", "controlled-real",
            artifacts.OrderBy(item => item.ArtifactId, StringComparer.Ordinal).ToArray(),
            dependencies.OrderBy(item => item.DependencyId, StringComparer.Ordinal).ToArray(),
            taxonomy.OrderBy(item => item.AssignmentId, StringComparer.Ordinal).ToArray(),
            coverage.OrderBy(item => item.CoverageId, StringComparer.Ordinal).ToArray(),
            M1Slice9Composition.ExactExcludedRunInstanceFields, effects,
            new(M1Slice9Composition.ControlledHandoffId, M1Slice9Composition.ControlledManifestSha256,
                26, 3, partitionTransitions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
        M1Slice9Composition.Validate(envelope);
        return envelope;
    }

    private static string Disposition(ScopeReversionDisposition value) => value switch
    {
        ScopeReversionDisposition.SupportedFinding => "present",
        ScopeReversionDisposition.ResolvedNegative => "resolved-negative",
        ScopeReversionDisposition.Abstained => "abstained",
        ScopeReversionDisposition.Unsupported => "unsupported",
        ScopeReversionDisposition.InvalidInput => "invalid-input",
        ScopeReversionDisposition.Failed => "failed",
        ScopeReversionDisposition.Limited => "limit-reached",
        ScopeReversionDisposition.Unpublishable => "unavailable",
        _ => throw new InvalidDataException("A controlled decision has an unspecified disposition."),
    };

    private static string GapState(ScopeGapFailureState value) => value switch
    {
        ScopeGapFailureState.Gap => "partial",
        ScopeGapFailureState.Failed => "failed",
        ScopeGapFailureState.Limited => "limit-reached",
        ScopeGapFailureState.None => "present",
        _ => throw new InvalidDataException("A controlled gap has an unspecified state."),
    };

    private static string CandidateState(ScopeCandidateState value) => value switch
    {
        ScopeCandidateState.Present => "present",
        ScopeCandidateState.ResolvedNegative => "resolved-negative",
        ScopeCandidateState.Ambiguous => "ambiguous",
        _ => throw new InvalidDataException("A controlled candidate has an unspecified state."),
    };

    private static string HypothesisState(ScopeHypothesisState value) => value switch
    {
        ScopeHypothesisState.Present => "present",
        ScopeHypothesisState.ResolvedRejected => "rejected",
        ScopeHypothesisState.Abstained => "abstained",
        _ => throw new InvalidDataException("A controlled hypothesis has an unspecified state."),
    };

    private static string Kebab<T>(T value) where T : struct, Enum =>
        JsonNamingPolicy.KebabCaseLower.ConvertName(value.ToString());

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
