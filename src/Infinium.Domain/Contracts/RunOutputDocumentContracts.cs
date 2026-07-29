namespace Infinium.Domain.Contracts;

/// <summary>
/// The stable JSON document model for <c>infinium.run-output/v1</c>.
/// This is deliberately distinct from <see cref="RunOutputAggregateContract"/>,
/// which is the richer in-memory semantic aggregate.
/// </summary>
public sealed record RunOutputContract(
    string SchemaId,
    string SchemaVersion,
    string RunId,
    string RunKind,
    string RunState,
    string ImplementationCommit,
    string StartedAt,
    string EndedAt,
    ArtifactReferenceDocumentContract InstallationSnapshot,
    ArtifactReferenceDocumentContract AnalysisContext,
    ArtifactReferenceDocumentContract EffectiveScanConfiguration,
    ArtifactReferenceDocumentContract ResolvedInputManifest,
    string TaxonomyId,
    string TaxonomyVersion,
    IReadOnlyList<ArtifactReferenceDocumentContract> AnalyzerDeclarations,
    IReadOnlyList<TypedArtifactDocumentContract> Observations,
    IReadOnlyList<TypedArtifactDocumentContract> DeterministicResults,
    IReadOnlyList<TypedArtifactDocumentContract> ExternalClaims,
    IReadOnlyList<TypedArtifactDocumentContract> ApplicationLinks,
    IReadOnlyList<TypedArtifactDocumentContract> DiscoveryLeads,
    IReadOnlyList<TypedArtifactDocumentContract> ModelProposals,
    IReadOnlyList<TypedArtifactDocumentContract> ProposalAdmissions,
    IReadOnlyList<TypedArtifactDocumentContract> Candidates,
    IReadOnlyList<TypedArtifactDocumentContract> Hypotheses,
    IReadOnlyList<TypedArtifactDocumentContract> Findings,
    IReadOnlyList<TypedArtifactDocumentContract> Recommendations,
    IReadOnlyList<TypedArtifactDocumentContract> SupportedCases,
    IReadOnlyList<TypedArtifactDocumentContract> LeadOnlyCases,
    IReadOnlyList<TypedArtifactDocumentContract> Abstentions,
    IReadOnlyList<TypedArtifactDocumentContract> InvalidInputs,
    IReadOnlyList<TypedArtifactDocumentContract> CoverageGaps,
    IReadOnlyList<TypedArtifactDocumentContract> Failures,
    IReadOnlyDictionary<string, RunOutputCollectionStateContract> CollectionStates,
    IReadOnlyList<TaxonomyAssignmentDocumentContract> TaxonomyAssignments,
    IReadOnlyList<CoverageDocumentContract> AnalyzerCoverage,
    IReadOnlyList<ExcludedCapabilityDocumentContract> ExcludedCapabilities,
    ReadinessDocumentContract Readiness,
    ReplayabilityDocumentContract Replayability,
    AuditabilityDocumentContract Auditability,
    string CliSummaryFingerprint,
    IReadOnlyList<ArtifactReferenceDocumentContract> DiagnosticTraceReferences);

public sealed record ArtifactReferenceDocumentContract(
    string ArtifactId,
    string ArtifactVersion,
    string Fingerprint,
    string Availability);

public sealed record LlmInvolvementDocumentContract(
    string State,
    string Operation,
    string? InvocationId);

public sealed record ArtifactProvenanceDocumentContract(
    string ProducerId,
    string ProducerVersion,
    string OriginatingRunId,
    IReadOnlyList<ArtifactReferenceDocumentContract> SourceReferences,
    IReadOnlyList<string> SupportingEvidenceReferences,
    IReadOnlyList<string> ContradictingEvidenceReferences,
    LlmInvolvementDocumentContract LlmInvolvement);

public sealed record TypedArtifactDocumentContract(
    string ArtifactId,
    long ArtifactRevision,
    string ArtifactType,
    string State,
    ArtifactReferenceDocumentContract Payload,
    ArtifactProvenanceDocumentContract Provenance);

public sealed record TaxonomyAssignmentDocumentContract(
    string AssignmentId,
    string TaxonomyId,
    string TaxonomyVersion,
    string SubjectType,
    string SubjectId,
    string Axis,
    string Facet,
    string? Code,
    string ApplicabilityState,
    string ClassificationRole,
    IReadOnlyList<string> EvidenceReferences,
    IReadOnlyList<string> ApplicabilityConditionReferences,
    string? ConfidenceAssessmentReference,
    string Reason,
    ArtifactProvenanceDocumentContract DerivationProvenance);

public sealed record CoverageDocumentContract(
    string CoverageId,
    string AnalyzerId,
    string PopulationId,
    string DenominatorLabel,
    long Denominator,
    long CompletedCount,
    string Status,
    string TaxonomyId,
    string TaxonomyVersion,
    IReadOnlyList<TaxonomyAssignmentDocumentContract> TaxonomyAssignments,
    IReadOnlyList<TypedArtifactDocumentContract> Gaps,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<TypedArtifactDocumentContract> Failures);

public sealed record RunOutputCollectionStateContract(string State, string Reason);

public sealed record ExcludedCapabilityDocumentContract(
    string CapabilityId,
    string State,
    string Reason);

public sealed record ReadinessDocumentContract(
    string State,
    string Scope,
    bool NoSafetyGuarantee);

public sealed record ReplayabilityDocumentContract(
    string ProductState,
    string ExactClass,
    ArtifactReferenceDocumentContract DependencyManifest,
    IReadOnlyList<TypedArtifactDocumentContract> Gaps);

public sealed record AuditabilityDocumentContract(
    string State,
    IReadOnlyList<TypedArtifactDocumentContract> Gaps);

public static class RunOutputContractInvariants
{
    private static readonly HashSet<string> RequiredCollectionNames = new(StringComparer.Ordinal)
    {
        "observations",
        "deterministic_results",
        "external_claims",
        "application_links",
        "discovery_leads",
        "model_proposals",
        "proposal_admissions",
        "candidates",
        "hypotheses",
        "findings",
        "recommendations",
        "supported_cases",
        "lead_only_cases",
        "abstentions",
        "invalid_inputs",
        "coverage_gaps",
        "failures",
    };

    public static void Validate(RunOutputContract output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!StringComparer.Ordinal.Equals(output.SchemaId, ContractConstants.RunOutputSchemaId)
            || !StringComparer.Ordinal.Equals(output.SchemaVersion, "1")
            || !StringComparer.Ordinal.Equals(output.TaxonomyId, ContractConstants.TaxonomyId)
            || !StringComparer.Ordinal.Equals(output.TaxonomyVersion, ContractConstants.TaxonomyVersion))
        {
            throw new InvalidOperationException("Run output metadata must bind the accepted v1 contracts.");
        }
        _ = new OpaqueId(output.RunId);
        _ = new Sha256Fingerprint(output.CliSummaryFingerprint);
        UtcTimestamp startedAt = UtcTimestamp.Parse(output.StartedAt);
        UtcTimestamp endedAt = UtcTimestamp.Parse(output.EndedAt);
        if (endedAt.Value < startedAt.Value)
        {
            throw new InvalidOperationException("Run output cannot end before it starts.");
        }
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                output.ImplementationCommit,
                "^[a-f0-9]{40}$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1)))
        {
            throw new InvalidOperationException("Run output implementation commit must be a full lowercase Git SHA.");
        }
        if (output.AnalyzerDeclarations.Count == 0
            || output.AnalyzerCoverage.Count == 0
            || output.ExcludedCapabilities.Count == 0
            || !output.Readiness.NoSafetyGuarantee
            || !RequiredCollectionNames.SetEquals(output.CollectionStates.Keys))
        {
            throw new InvalidOperationException(
                "Run output must make analyzers, coverage, exclusions, collection states, and safety limits explicit.");
        }

        IReadOnlyDictionary<string, IReadOnlyList<TypedArtifactDocumentContract>> collections =
            new Dictionary<string, IReadOnlyList<TypedArtifactDocumentContract>>(StringComparer.Ordinal)
            {
                ["observations"] = output.Observations,
                ["deterministic_results"] = output.DeterministicResults,
                ["external_claims"] = output.ExternalClaims,
                ["application_links"] = output.ApplicationLinks,
                ["discovery_leads"] = output.DiscoveryLeads,
                ["model_proposals"] = output.ModelProposals,
                ["proposal_admissions"] = output.ProposalAdmissions,
                ["candidates"] = output.Candidates,
                ["hypotheses"] = output.Hypotheses,
                ["findings"] = output.Findings,
                ["recommendations"] = output.Recommendations,
                ["supported_cases"] = output.SupportedCases,
                ["lead_only_cases"] = output.LeadOnlyCases,
                ["abstentions"] = output.Abstentions,
                ["invalid_inputs"] = output.InvalidInputs,
                ["coverage_gaps"] = output.CoverageGaps,
                ["failures"] = output.Failures,
            };
        Dictionary<string, string> expectedArtifactTypes = new(StringComparer.Ordinal)
        {
            ["observations"] = "observation",
            ["deterministic_results"] = "deterministic-result",
            ["external_claims"] = "external-claim",
            ["application_links"] = "application-link",
            ["discovery_leads"] = "discovery-lead",
            ["model_proposals"] = "model-proposal",
            ["proposal_admissions"] = "proposal-admission",
            ["candidates"] = "candidate",
            ["hypotheses"] = "hypothesis",
            ["findings"] = "finding",
            ["recommendations"] = "recommendation",
            ["supported_cases"] = "supported-case",
            ["lead_only_cases"] = "lead-only-case",
            ["abstentions"] = "abstention",
            ["invalid_inputs"] = "invalid-input",
            ["coverage_gaps"] = "coverage-gap",
            ["failures"] = "failure",
        };
        foreach ((string name, IReadOnlyList<TypedArtifactDocumentContract> artifacts) in collections)
        {
            RunOutputCollectionStateContract state = output.CollectionStates[name];
            if (string.IsNullOrWhiteSpace(state.Reason)
                || (artifacts.Count > 0 && !StringComparer.Ordinal.Equals(state.State, "populated"))
                || (artifacts.Count == 0 && StringComparer.Ordinal.Equals(state.State, "populated"))
                || artifacts.Any(value =>
                    !StringComparer.Ordinal.Equals(value.ArtifactType, expectedArtifactTypes[name])))
            {
                throw new InvalidOperationException(
                    $"Run output collection '{name}' has inconsistent type, contents, or production state.");
            }
        }
        string[] artifactIds = collections.Values
            .SelectMany(value => value)
            .Select(value => value.ArtifactId)
            .ToArray();
        if (artifactIds.Distinct(StringComparer.Ordinal).Count() != artifactIds.Length)
        {
            throw new InvalidOperationException("Stable run-output artifact IDs must be globally unique.");
        }
        if (output.AnalyzerCoverage.Any(value =>
                value.Denominator < 0
                || value.CompletedCount < 0
                || value.CompletedCount > value.Denominator
                || !StringComparer.Ordinal.Equals(value.TaxonomyId, output.TaxonomyId)
                || !StringComparer.Ordinal.Equals(value.TaxonomyVersion, output.TaxonomyVersion)))
        {
            throw new InvalidOperationException(
                "Stable run-output coverage must use bounded counts and the run taxonomy.");
        }
    }
}
