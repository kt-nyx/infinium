using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;

/// <summary>
/// A closed manifest over already-retained analysis outputs. It grants no
/// authority to acquire or mutate any named input.
/// </summary>
public sealed record AnalysisCompositionEnvelope(
    int SchemaVersion,
    string EnvelopeId,
    string PackageId,
    string PackageKind,
    IReadOnlyList<AnalysisComposedArtifact> Artifacts,
    IReadOnlyList<AnalysisRetainedDependency> Dependencies,
    IReadOnlyList<AnalysisTaxonomyProjection> Taxonomy,
    IReadOnlyList<AnalysisCoverageProjection> Coverage,
    IReadOnlyList<string> ExcludedRunInstanceFields,
    IReadOnlyDictionary<string, string> Effects,
    ControlledAnalysisIdentity? ControlledIdentity)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record AnalysisComposedArtifact(
    string Collection,
    string ArtifactId,
    long ArtifactRevision,
    string State,
    ArtifactReferenceDocumentContract Payload,
    string ProducerId,
    string ProducerVersion,
    string OriginatingRunId,
    IReadOnlyList<ArtifactReferenceDocumentContract> SourceReferences,
    IReadOnlyList<string> SupportingEvidenceReferences,
    IReadOnlyList<string> ContradictingEvidenceReferences,
    LlmInvolvementDocumentContract LlmInvolvement);

public sealed record AnalysisRetainedDependency(
    string DependencyId,
    string Kind,
    string Version,
    string Sha256,
    long ByteLength);

public sealed record AnalysisCoverageProjection(
    string CoverageId,
    string AnalyzerId,
    string PopulationId,
    string DenominatorLabel,
    long Denominator,
    long CompletedCount,
    string Status,
    IReadOnlyList<string> GapIds,
    IReadOnlyList<string> Exclusions,
    IReadOnlyList<string> FailureIds);

public sealed record AnalysisTaxonomyProjection(
    string AssignmentId,
    string SubjectType,
    string SubjectId,
    string Axis,
    string Facet,
    string? Code,
    string ApplicabilityState,
    string ClassificationRole,
    IReadOnlyList<string> EvidenceReferences,
    string Reason,
    string ProducerId,
    string ProducerVersion,
    string OriginatingRunId);

public sealed record ControlledAnalysisIdentity(
    string HandoffId,
    string ManifestSha256,
    int InputCount,
    int PublicManifestCount,
    IReadOnlyList<string> PartitionTransitions);

public static class AnalysisComposition
{
    public static readonly IReadOnlyList<string> SemanticEquivalenceExcludedFields =
    [
        "$.run_id",
        "$.started_at",
        "$.ended_at",
        "$.cli_summary_fingerprint",
        "$.replay_manifest",
        "$.replayability.dependency_manifest.artifact_id",
        "$.diagnostic_trace_references",
        "$.collections[*] current-run-derived identity hashes while preserving typed relationship labels",
        "$.collections[*].provenance.originating_run_id and consuming-run edge for the current run",
        "$.taxonomy_assignments current-run-derived identity hashes while preserving typed relationship labels",
        "$.analyzer_coverage current-run-derived identity hashes while preserving typed relationship labels",
    ];

    private static readonly Dictionary<string, string> ArtifactTypes =
        new Dictionary<string, string>(StringComparer.Ordinal)
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
            ["documentation_revisions"] = "documentation-revision",
            ["passages"] = "passage",
            ["candidate_decisions"] = "candidate-decision",
            ["reconciliation_assessments"] = "reconciliation-assessment",
            ["lineage_events"] = "lineage-event",
        };

    private static readonly string[] EffectNames =
        ["provider", "model", "credential", "dns", "network", "billable", "live", "source-refresh"];

    public static void Validate(AnalysisCompositionEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.SchemaVersion != AnalysisCompositionEnvelope.CurrentSchemaVersion
            || envelope.EnvelopeId.Length is < 1 or > 128
            || envelope.PackageId.Length is < 1 or > 128
            || envelope.PackageKind is not ("synthetic" or "controlled-real")
            || envelope.Artifacts.Count is < 1 or > 10_000
            || envelope.Dependencies.Count is < 1 or > 10_000
            || envelope.Taxonomy.Count > 10_000
            || envelope.Coverage.Count is < 1 or > 1_000
            || !SemanticEquivalenceExcludedFields.SequenceEqual(envelope.ExcludedRunInstanceFields, StringComparer.Ordinal)
            || envelope.Effects.Count != EffectNames.Length
            || EffectNames.Any(name => !envelope.Effects.TryGetValue(name, out string? state)
                || !StringComparer.Ordinal.Equals(state, "not-used")))
        {
            throw new InvalidDataException("The analysis composition envelope is unbounded, incomplete, or enables an external effect.");
        }

        string[] artifactIds = envelope.Artifacts.Select(item => item.ArtifactId).ToArray();
        if (artifactIds.Distinct(StringComparer.Ordinal).Count() != artifactIds.Length
            || envelope.Artifacts.Any(item => !ArtifactTypes.ContainsKey(item.Collection)
                || item.ArtifactId.Length is < 1 or > 160
                || item.ArtifactRevision < 1
                || string.IsNullOrWhiteSpace(item.State)
                || item.ProducerId.Length is < 1 or > 160
                || item.ProducerVersion.Length is < 1 or > 32
                || item.OriginatingRunId.Length is < 1 or > 160
                || item.Payload.Availability != "retained"
                || !IsSha(item.Payload.Fingerprint)
                || item.SourceReferences.Count is < 1 or > 1_000
                || item.SourceReferences.Any(reference => reference.Availability != "retained"
                    || !IsSha(reference.Fingerprint))
                || !ValidLlm(item.LlmInvolvement)))
        {
            throw new InvalidDataException("Composed artifacts have duplicate, substituted, or malformed retained identities.");
        }

        string[] dependencyIds = envelope.Dependencies.Select(item => item.DependencyId).ToArray();
        if (dependencyIds.Distinct(StringComparer.Ordinal).Count() != dependencyIds.Length
            || envelope.Dependencies.Any(item => item.DependencyId.Length is < 1 or > 160
                || item.Kind.Length is < 1 or > 80
                || item.ByteLength < 1
                || !IsSha(item.Sha256)))
        {
            throw new InvalidDataException("Retained dependency identities are duplicated or malformed.");
        }

        Dictionary<string, AnalysisRetainedDependency> dependencies = envelope.Dependencies
            .ToDictionary(item => item.DependencyId, StringComparer.Ordinal);
        if (envelope.Artifacts.Any(item => !Matches(item.Payload, dependencies)
                || item.SourceReferences.Any(reference => !Matches(reference, dependencies))))
        {
            throw new InvalidDataException(
                "Every composed payload and source reference must resolve to an exact retained dependency.");
        }

        HashSet<string> artifactIdSet = artifactIds.ToHashSet(StringComparer.Ordinal);
        if (envelope.Taxonomy.Select(item => item.AssignmentId).Distinct(StringComparer.Ordinal).Count()
                != envelope.Taxonomy.Count
            || envelope.Taxonomy.Any(item => item.AssignmentId.Length is < 1 or > 160
                || item.SubjectType.Length is < 1 or > 80
                || item.SubjectId.Length is < 1 or > 160
                || item.Axis.Length is < 1 or > 80
                || item.Facet.Length is < 1 or > 80
                || item.EvidenceReferences.Count == 0
                || item.ProducerId.Length is < 1 or > 160
                || item.OriginatingRunId.Length is < 1 or > 160))
        {
            throw new InvalidDataException("Taxonomy projections are duplicated or malformed.");
        }
        if (envelope.Coverage.Select(item => item.CoverageId).Distinct(StringComparer.Ordinal).Count()
                != envelope.Coverage.Count
            || envelope.Coverage.Any(item => item.CoverageId.Length is < 1 or > 160
                || item.AnalyzerId.Length is < 1 or > 160
                || item.PopulationId.Length is < 1 or > 160
                || string.IsNullOrWhiteSpace(item.DenominatorLabel)
                || item.Denominator < 0
                || item.CompletedCount < 0
                || item.CompletedCount > item.Denominator
                || item.GapIds.Any(id => !artifactIdSet.Contains(id))
                || item.FailureIds.Any(id => !artifactIdSet.Contains(id))))
        {
            throw new InvalidDataException("Coverage does not close over the exact composed artifact set.");
        }

        if (envelope.PackageKind == "controlled-real")
        {
            ControlledAnalysisIdentity controlled = envelope.ControlledIdentity
                ?? throw new InvalidDataException("Controlled-real composition requires an identity-only handoff receipt.");
            if (controlled.HandoffId.Length is < 1 or > 160
                || !IsSha(controlled.ManifestSha256)
                || controlled.InputCount is < 1 or > 100_000
                || controlled.PublicManifestCount is < 1 or > 10_000
                || controlled.PublicManifestCount > controlled.InputCount
                || controlled.PartitionTransitions.Count is < 1 or > 10_000
                || controlled.PartitionTransitions.Distinct(StringComparer.Ordinal).Count()
                    != controlled.PartitionTransitions.Count
                || controlled.PartitionTransitions.Any(item => string.IsNullOrWhiteSpace(item) || item.Length > 320))
            {
                throw new InvalidDataException("Controlled-real handoff identity, counts, or partitions are malformed.");
            }
        }
        else if (envelope.ControlledIdentity is not null)
        {
            throw new InvalidDataException("Synthetic composition cannot claim a controlled-real handoff.");
        }

    }

    public static string Fingerprint(AnalysisCompositionEnvelope envelope)
    {
        Validate(envelope);
        return RawFingerprint(envelope);
    }

    private static string RawFingerprint(AnalysisCompositionEnvelope envelope)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, ContractJsonSerializer.Options);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    internal static IReadOnlyList<ReplayDependencyNodeContract> Dependencies(AnalysisCompositionEnvelope envelope)
    {
        Validate(envelope);
        return envelope.Dependencies.Select(item => new ReplayDependencyNodeContract(
            new OpaqueId(item.DependencyId), item.Kind, ContractVersion.Parse(item.Version),
            new Sha256Fingerprint(item.Sha256), AnalysisResultState.Present)).ToArray();
    }

    internal static void Apply(
        AnalysisCompositionEnvelope envelope,
        string consumingRunId,
        IDictionary<string, IReadOnlyList<TypedArtifactDocumentContract>> collections,
        ICollection<TaxonomyAssignmentDocumentContract> taxonomy,
        ICollection<CoverageDocumentContract> coverage)
    {
        Validate(envelope);
        foreach (IGrouping<string, AnalysisComposedArtifact> group in envelope.Artifacts.GroupBy(item => item.Collection))
        {
            List<TypedArtifactDocumentContract> values = collections[group.Key].ToList();
            values.AddRange(group.Select(item => new TypedArtifactDocumentContract(
                item.ArtifactId, item.ArtifactRevision, ArtifactTypes[item.Collection], item.State, item.Payload,
                new ArtifactProvenanceDocumentContract(
                    item.ProducerId, item.ProducerVersion,
                    item.OriginatingRunId == "$current-run" ? consumingRunId : item.OriginatingRunId,
                    item.SourceReferences,
                    item.SupportingEvidenceReferences.Append("consuming-run:" + consumingRunId)
                        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                    item.ContradictingEvidenceReferences,
                    item.LlmInvolvement))));
            collections[group.Key] = values.OrderBy(item => item.ArtifactId, StringComparer.Ordinal).ToArray();
        }

        Dictionary<string, TypedArtifactDocumentContract> artifacts = collections.Values
            .SelectMany(item => item).ToDictionary(item => item.ArtifactId, StringComparer.Ordinal);
        foreach (AnalysisTaxonomyProjection item in envelope.Taxonomy)
        {
            taxonomy.Add(new TaxonomyAssignmentDocumentContract(
                item.AssignmentId, ContractConstants.TaxonomyId, ContractConstants.TaxonomyVersion,
                item.SubjectType, item.SubjectId, item.Axis, item.Facet, item.Code,
                item.ApplicabilityState, item.ClassificationRole, item.EvidenceReferences, [], null,
                item.Reason,
                new ArtifactProvenanceDocumentContract(
                    item.ProducerId, item.ProducerVersion,
                    item.OriginatingRunId == "$current-run" ? consumingRunId : item.OriginatingRunId,
                    [], item.EvidenceReferences, [], new("none", "none", null))));
        }
        foreach (AnalysisCoverageProjection item in envelope.Coverage)
        {
            coverage.Add(new CoverageDocumentContract(
                item.CoverageId, item.AnalyzerId, item.PopulationId, item.DenominatorLabel,
                item.Denominator, item.CompletedCount, item.Status,
                ContractConstants.TaxonomyId, ContractConstants.TaxonomyVersion, [],
                item.GapIds.Select(id => artifacts[id]).ToArray(), item.Exclusions,
                item.FailureIds.Select(id => artifacts[id]).ToArray()));
        }
    }

    private static bool ValidLlm(LlmInvolvementDocumentContract value) => value.State switch
    {
        "none" => value.Operation == "none" && value.InvocationId is null,
        "proposal-retained" or "proposal-rejected" or "proposal-admitted" =>
            value.Operation is "source-claim-extraction" or "candidate-investigation"
            && value.InvocationId is { Length: > 0 and <= 160 },
        _ => false,
    };

    private static bool IsSha(string value) => value.Length == 64
        && value.All(ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool Matches(
        ArtifactReferenceDocumentContract reference,
        Dictionary<string, AnalysisRetainedDependency> dependencies) =>
        dependencies.TryGetValue(reference.ArtifactId, out AnalysisRetainedDependency? dependency)
        && dependency.Version == reference.ArtifactVersion
        && dependency.Sha256 == reference.Fingerprint;
}

public sealed record RunOutputSemanticEquivalenceProjection(
    string SchemaId,
    int SchemaVersion,
    IReadOnlyList<string> ExcludedRunInstanceFields,
    JsonElement SemanticOutput);

public static class RunOutputSemanticEquivalence
{
    public const string SchemaId = "infinium.m1-slice9-semantic-equivalence-projection/v1";

    public static byte[] Project(RunOutputContract output)
    {
        RunOutputContractInvariants.Validate(output);
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
                ["documentation_revisions"] = output.DocumentationRevisions,
                ["passages"] = output.Passages,
                ["candidate_decisions"] = output.CandidateDecisions,
                ["reconciliation_assessments"] = output.ReconciliationAssessments,
                ["lineage_events"] = output.LineageEvents,
            };
        Dictionary<string, TypedArtifactDocumentContract> allArtifacts = collections.Values
            .SelectMany(items => items).ToDictionary(item => item.ArtifactId, StringComparer.Ordinal);
        string NormalizeIdentity(string value)
        {
            string normalized = Regex.Replace(
                value, "[0-9a-f]{32,64}", "<run-instance-hash>",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            return Regex.Replace(
                normalized, "run-input-[0-9]+", "<run-input>",
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        string SemanticReference(string value)
        {
            if (!allArtifacts.TryGetValue(value, out TypedArtifactDocumentContract? referenced))
            {
                return NormalizeIdentity(value);
            }
            bool retained = referenced.Provenance.LlmInvolvement.State != "none"
                || referenced.Provenance.OriginatingRunId != output.RunId;
            return retained
                ? referenced.ArtifactId
                : string.Join(':', "current-artifact", referenced.ArtifactType, referenced.State,
                    referenced.Provenance.ProducerId, NormalizeIdentity(referenced.ArtifactId));
        }
        object ProjectReference(ArtifactReferenceDocumentContract reference, bool retained) => retained
            ? reference
            : new
            {
                ArtifactId = NormalizeIdentity(reference.ArtifactId),
                reference.ArtifactVersion,
                Fingerprint = "$run-instance-fingerprint",
                reference.Availability,
            };
        object Artifact(TypedArtifactDocumentContract artifact)
        {
            bool retained = artifact.Provenance.LlmInvolvement.State != "none"
                || artifact.Provenance.OriginatingRunId != output.RunId;
            return new
            {
                ArtifactId = retained ? artifact.ArtifactId : SemanticReference(artifact.ArtifactId),
                artifact.ArtifactRevision,
                artifact.ArtifactType,
                artifact.State,
                Payload = ProjectReference(artifact.Payload, retained),
                Provenance = new
                {
                    artifact.Provenance.ProducerId,
                    artifact.Provenance.ProducerVersion,
                    OriginatingRunId = artifact.Provenance.OriginatingRunId == output.RunId
                        ? "$run-instance" : artifact.Provenance.OriginatingRunId,
                    SourceReferences = artifact.Provenance.SourceReferences
                        .Select(reference => ProjectReference(reference, retained))
                        .OrderBy(reference => JsonSerializer.Serialize(reference, ContractJsonSerializer.Options),
                            StringComparer.Ordinal).ToArray(),
                    SupportingEvidence = artifact.Provenance.SupportingEvidenceReferences
                        .Where(value => !value.StartsWith("consuming-run:", StringComparison.Ordinal))
                        .Select(SemanticReference).Order(StringComparer.Ordinal).ToArray(),
                    ContradictingEvidence = artifact.Provenance.ContradictingEvidenceReferences
                        .Select(SemanticReference).Order(StringComparer.Ordinal).ToArray(),
                    artifact.Provenance.LlmInvolvement,
                },
            };
        }
        var semanticShape = new
        {
            output.SchemaId,
            output.SchemaVersion,
            output.RunKind,
            output.RunState,
            output.ImplementationCommit,
            output.InstallationSnapshot,
            output.AnalysisContext,
            output.EffectiveScanConfiguration,
            output.ResolvedInputManifest,
            output.TaxonomyId,
            output.TaxonomyVersion,
            AnalyzerDeclarations = output.AnalyzerDeclarations.OrderBy(item => item.ArtifactId, StringComparer.Ordinal),
            Collections = collections.ToDictionary(
                item => item.Key,
                item => item.Value.Select(Artifact)
                    .OrderBy(value => JsonSerializer.Serialize(value, ContractJsonSerializer.Options), StringComparer.Ordinal)
                    .ToArray(), StringComparer.Ordinal),
            output.CollectionStates,
            TaxonomyAssignments = output.TaxonomyAssignments.Select(item => new
            {
                AssignmentId = NormalizeIdentity(item.AssignmentId),
                item.TaxonomyId,
                item.TaxonomyVersion,
                item.SubjectType,
                SubjectId = SemanticReference(item.SubjectId),
                item.Axis,
                item.Facet,
                item.Code,
                item.ApplicabilityState,
                item.ClassificationRole,
                EvidenceReferences = item.EvidenceReferences.Select(SemanticReference)
                    .Order(StringComparer.Ordinal).ToArray(),
                ApplicabilityConditionReferences = item.ApplicabilityConditionReferences
                    .Select(SemanticReference).Order(StringComparer.Ordinal).ToArray(),
                ConfidenceAssessmentReference = item.ConfidenceAssessmentReference is null
                    ? null : SemanticReference(item.ConfidenceAssessmentReference),
                item.Reason,
                item.DerivationProvenance.ProducerId,
                item.DerivationProvenance.ProducerVersion,
                OriginatingRunId = item.DerivationProvenance.OriginatingRunId == output.RunId
                    ? "$run-instance" : item.DerivationProvenance.OriginatingRunId,
                SourceReferences = item.DerivationProvenance.SourceReferences
                    .Select(reference => ProjectReference(reference,
                        item.DerivationProvenance.OriginatingRunId != output.RunId))
                    .OrderBy(reference => JsonSerializer.Serialize(reference, ContractJsonSerializer.Options),
                        StringComparer.Ordinal).ToArray(),
                SupportingEvidence = item.DerivationProvenance.SupportingEvidenceReferences
                    .Where(value => !value.StartsWith("consuming-run:", StringComparison.Ordinal))
                    .Select(SemanticReference).Order(StringComparer.Ordinal).ToArray(),
                ContradictingEvidence = item.DerivationProvenance.ContradictingEvidenceReferences
                    .Select(SemanticReference).Order(StringComparer.Ordinal).ToArray(),
                item.DerivationProvenance.LlmInvolvement,
            }).OrderBy(item => JsonSerializer.Serialize(item, ContractJsonSerializer.Options), StringComparer.Ordinal),
            AnalyzerCoverage = output.AnalyzerCoverage.Select(item => new
            {
                CoverageId = NormalizeIdentity(item.CoverageId),
                item.AnalyzerId,
                item.PopulationId,
                item.DenominatorLabel,
                item.Denominator,
                item.CompletedCount,
                item.Status,
                item.TaxonomyId,
                item.TaxonomyVersion,
                TaxonomyAssignments = item.TaxonomyAssignments.Select(assignment =>
                        NormalizeIdentity(assignment.AssignmentId))
                    .Order(StringComparer.Ordinal).ToArray(),
                Gaps = item.Gaps.Select(gap => SemanticReference(gap.ArtifactId))
                    .Order(StringComparer.Ordinal).ToArray(),
                item.Exclusions,
                Failures = item.Failures.Select(failure => SemanticReference(failure.ArtifactId))
                    .Order(StringComparer.Ordinal).ToArray(),
            }).OrderBy(item => JsonSerializer.Serialize(item, ContractJsonSerializer.Options), StringComparer.Ordinal),
            output.ExcludedCapabilities,
            output.Readiness,
            Replayability = new
            {
                output.Replayability.ProductState,
                output.Replayability.ExactClass,
                output.Replayability.DependencyManifest.ArtifactVersion,
                output.Replayability.DependencyManifest.Availability,
                Gaps = output.Replayability.Gaps.Select(gap => new { gap.ArtifactType, gap.State }),
            },
            Auditability = new
            {
                output.Auditability.State,
                Gaps = output.Auditability.Gaps.Select(gap => new { gap.ArtifactType, gap.State }),
            },
            output.NotUsedBoundaries,
        };
        using JsonDocument semantic = JsonDocument.Parse(
            JsonSerializer.SerializeToUtf8Bytes(semanticShape, ContractJsonSerializer.Options));
        RunOutputSemanticEquivalenceProjection projection = new(
            SchemaId, 1, AnalysisComposition.SemanticEquivalenceExcludedFields,
            semantic.RootElement.Clone());
        return JsonSerializer.SerializeToUtf8Bytes(projection, ContractJsonSerializer.Options);
    }

    public static string Fingerprint(RunOutputContract output) =>
        Convert.ToHexStringLower(SHA256.HashData(Project(output)));

    public static void AssertEquivalent(RunOutputContract left, RunOutputContract right)
    {
        byte[] leftProjection = Project(left);
        byte[] rightProjection = Project(right);
        if (!leftProjection.AsSpan().SequenceEqual(rightProjection))
        {
            string[] leftLines = Encoding.UTF8.GetString(leftProjection).Split('\n');
            string[] rightLines = Encoding.UTF8.GetString(rightProjection).Split('\n');
            int difference = Enumerable.Range(0, Math.Min(leftLines.Length, rightLines.Length))
                .FirstOrDefault(index => !StringComparer.Ordinal.Equals(leftLines[index], rightLines[index]), -1);
            string detail = difference < 0
                ? $"projection lengths differ ({leftLines.Length} versus {rightLines.Length})"
                : $"first difference at line {difference + 1}: '{leftLines[difference]}' versus '{rightLines[difference]}'";
            throw new InvalidDataException(
                "Run outputs differ outside the declared run-instance fields; " + detail + ".");
        }
    }

}
