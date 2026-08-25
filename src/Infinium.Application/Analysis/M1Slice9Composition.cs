using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Infinium.Domain.Contracts;

namespace Infinium.Application.Analysis;

/// <summary>
/// A closed, internal manifest over already-retained M1 stage outputs. It is
/// deliberately not a product contract and grants no authority to acquire or
/// mutate any named input.
/// </summary>
public sealed record M1Slice9CompositionEnvelope(
    int SchemaVersion,
    string EnvelopeId,
    string PackageId,
    string PackageKind,
    IReadOnlyList<M1Slice9ComposedArtifact> Artifacts,
    IReadOnlyList<M1Slice9RetainedDependency> Dependencies,
    IReadOnlyList<M1Slice9TaxonomyProjection> Taxonomy,
    IReadOnlyList<M1Slice9CoverageProjection> Coverage,
    IReadOnlyList<string> ExcludedRunInstanceFields,
    IReadOnlyDictionary<string, string> Effects,
    M1Slice9ControlledIdentity? ControlledIdentity)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record M1Slice9ComposedArtifact(
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

public sealed record M1Slice9RetainedDependency(
    string DependencyId,
    string Kind,
    string Version,
    string Sha256,
    long ByteLength);

public sealed record M1Slice9CoverageProjection(
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

public sealed record M1Slice9TaxonomyProjection(
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

public sealed record M1Slice9ControlledIdentity(
    string HandoffId,
    string ManifestSha256,
    int InputCount,
    int PublicManifestCount,
    IReadOnlyList<string> PartitionTransitions);

public static class M1Slice9Composition
{
    public const string ControlledHandoffId = "m1-slice8-research0035-local-v1";
    public const string ControlledManifestSha256 = "8972ef0e160b9de04da281d48639b66d8bffcc153504c1d699f654f1eff6ecf5";
    public const string ExactSyntheticEnvelopeSha256 =
        "cc48ef713282d7060a0dd9560972f2e16235e52c4147d6f5c9c4db31cd1fabb1";
    public const string ExactControlledEnvelopeSha256 =
        "02d33986cd28326074cc7889f8949716cd961e630ebb82f139b0d327af135b77";

    public static readonly IReadOnlyList<string> ExactExcludedRunInstanceFields =
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

    public static void Validate(M1Slice9CompositionEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.SchemaVersion != M1Slice9CompositionEnvelope.CurrentSchemaVersion
            || envelope.EnvelopeId.Length is < 1 or > 128
            || envelope.PackageId.Length is < 1 or > 128
            || envelope.PackageKind is not ("synthetic" or "controlled-real")
            || envelope.Artifacts.Count is < 1 or > 10_000
            || envelope.Dependencies.Count is < 1 or > 10_000
            || envelope.Taxonomy.Count > 10_000
            || envelope.Coverage.Count is < 1 or > 1_000
            || !ExactExcludedRunInstanceFields.SequenceEqual(envelope.ExcludedRunInstanceFields, StringComparer.Ordinal)
            || envelope.Effects.Count != EffectNames.Length
            || EffectNames.Any(name => !envelope.Effects.TryGetValue(name, out string? state)
                || !StringComparer.Ordinal.Equals(state, "not-used")))
        {
            throw new InvalidDataException("The Slice 9 composition envelope is unbounded, incomplete, or enables an external effect.");
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
            throw new InvalidDataException("Slice 9 composed artifacts have duplicate, substituted, or malformed retained identities.");
        }

        string[] dependencyIds = envelope.Dependencies.Select(item => item.DependencyId).ToArray();
        if (dependencyIds.Distinct(StringComparer.Ordinal).Count() != dependencyIds.Length
            || envelope.Dependencies.Any(item => item.DependencyId.Length is < 1 or > 160
                || item.Kind.Length is < 1 or > 80
                || item.ByteLength < 1
                || !IsSha(item.Sha256)))
        {
            throw new InvalidDataException("Slice 9 retained dependency identities are duplicated or malformed.");
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
            throw new InvalidDataException("Slice 9 taxonomy projections are duplicated or malformed.");
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
            throw new InvalidDataException("Slice 9 coverage does not close over the exact composed artifact set.");
        }

        if (envelope.PackageKind == "controlled-real")
        {
            M1Slice9ControlledIdentity controlled = envelope.ControlledIdentity
                ?? throw new InvalidDataException("Controlled-real composition requires an identity-only handoff receipt.");
            if (controlled.HandoffId != ControlledHandoffId
                || controlled.ManifestSha256 != ControlledManifestSha256
                || controlled.InputCount != 26
                || controlled.PublicManifestCount != 3
                || controlled.PartitionTransitions.Count == 0
                || controlled.PartitionTransitions.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException("Controlled-real Slice 8 handoff identity, counts, or partitions drifted.");
            }
        }
        else if (envelope.ControlledIdentity is not null)
        {
            throw new InvalidDataException("Synthetic composition cannot claim a controlled-real handoff.");
        }

        string expectedIdentity = envelope.PackageKind == "synthetic"
            ? ExactSyntheticEnvelopeSha256 : ExactControlledEnvelopeSha256;
        if (!StringComparer.Ordinal.Equals(RawFingerprint(envelope), expectedIdentity))
        {
            throw new InvalidDataException(
                "The Slice 9 composition is well formed but does not match either exact authorized package identity.");
        }
    }

    public static string Fingerprint(M1Slice9CompositionEnvelope envelope)
    {
        Validate(envelope);
        return RawFingerprint(envelope);
    }

    private static string RawFingerprint(M1Slice9CompositionEnvelope envelope)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, ContractJsonSerializer.Options);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    internal static IReadOnlyList<ReplayDependencyNodeContract> Dependencies(M1Slice9CompositionEnvelope envelope)
    {
        Validate(envelope);
        return envelope.Dependencies.Select(item => new ReplayDependencyNodeContract(
            new OpaqueId(item.DependencyId), item.Kind, ContractVersion.Parse(item.Version),
            new Sha256Fingerprint(item.Sha256), AnalysisResultState.Present)).ToArray();
    }

    internal static void Apply(
        M1Slice9CompositionEnvelope envelope,
        string consumingRunId,
        IDictionary<string, IReadOnlyList<TypedArtifactDocumentContract>> collections,
        ICollection<TaxonomyAssignmentDocumentContract> taxonomy,
        ICollection<CoverageDocumentContract> coverage)
    {
        Validate(envelope);
        foreach (IGrouping<string, M1Slice9ComposedArtifact> group in envelope.Artifacts.GroupBy(item => item.Collection))
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
        foreach (M1Slice9TaxonomyProjection item in envelope.Taxonomy)
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
        foreach (M1Slice9CoverageProjection item in envelope.Coverage)
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
}

public static class M1Slice9SyntheticComposition
{
    public static M1Slice9CompositionEnvelope Create()
    {
        ArtifactReferenceDocumentContract composed = new(
            "m1-s6-composed-evidence", "2.0.0",
            "901f278825d3fdbab2971b9f6bb4462f84c12dea96f1c14c8f222d1f29a1df9d", "retained");
        ArtifactReferenceDocumentContract wp10 = new(
            "m1-s6-wp10-attempt-evidence", "3.0.0",
            "5aa6391aba25fbdfcd5470e3dc0db9c8a108b392f14e3c024bf883d214d4b0af", "retained");
        ArtifactReferenceDocumentContract source = new(
            "m1-s9-synthetic-source", "1.0.0",
            "b14a50bf341d467c5922c7a9be200a5f61ef974c4dfb95d656c5701ee2220ac6", "retained");
        LlmInvolvementDocumentContract none = new("none", "none", null);
        M1Slice9ComposedArtifact Artifact(
            string collection, string id, string state, ArtifactReferenceDocumentContract payload,
            string producer, string origin, LlmInvolvementDocumentContract llm,
            IReadOnlyList<string>? support = null) => new(
                collection, id, 1, state, payload, producer, "1.0.0", origin,
                [source], support ?? [], [], llm);
        M1Slice9CompositionEnvelope envelope = new(
            1, "m1-s9-synthetic-composition", "M1-S9-SYNTHETIC-v1", "synthetic",
            [
                Artifact("observations", "m1-s9-observation-supported", "present", source,
                    "m1-s9-local-composer", "$current-run", none),
                Artifact("deterministic_results", "m1-s9-control-resolved", "resolved-negative", source,
                    "m1-s9-local-composer", "$current-run", none),
                Artifact("model_proposals", "m1-s9-retained-model-proposal", "present", wp10,
                    "infinium.provider.source-claim", "m1-s6-wp10-live-run",
                    new("proposal-retained", "source-claim-extraction", "wp10-attempt-2-development-c4f6aa8")),
                Artifact("proposal_admissions", "m1-s9-retained-proposal-admission", "present", composed,
                    "infinium.provider.host-admission", "m1-s6-wp10-live-run",
                    new("proposal-admitted", "source-claim-extraction", "wp10-attempt-2-development-c4f6aa8"),
                    ["m1-s9-retained-model-proposal"]),
                Artifact("abstentions", "m1-s9-bounded-abstention", "abstained", source,
                    "m1-s9-local-composer", "$current-run", none),
                Artifact("coverage_gaps", "m1-s9-visible-gap", "partial", source,
                    "m1-s9-local-composer", "$current-run", none),
                Artifact("discovery_leads", "m1-s9-unsupported-lead", "unsupported", source,
                    "m1-s9-local-composer", "$current-run", none),
            ],
            [
                new("m1-s9-synthetic-source", "synthetic-package-manifest", "1.0.0",
                    "b14a50bf341d467c5922c7a9be200a5f61ef974c4dfb95d656c5701ee2220ac6", 1775),
                new("m1-s6-composed-evidence", "retained-provider-composition", "2.0.0",
                    "901f278825d3fdbab2971b9f6bb4462f84c12dea96f1c14c8f222d1f29a1df9d", 8653),
                new("m1-s6-wp9-attempt-evidence", "retained-provider-attempt", "3.0.0",
                    "6f51bc6d28799711e7d62d5e67ef7965d2be6d72d9c3453ec16f7e9cfbbc1270", 6292),
                new("m1-s6-wp10-attempt-evidence", "retained-provider-attempt", "3.0.0",
                    "5aa6391aba25fbdfcd5470e3dc0db9c8a108b392f14e3c024bf883d214d4b0af", 6312),
                new("m1-s6-wp11-attempt-evidence", "retained-provider-attempt", "3.0.0",
                    "eaddaac9644359c1fe45bd0b726574b037f38e2449db50f51757ce03190d16ca", 6313),
            ],
            [],
            [new("m1-s9-coverage", "m1-s9-composition", "m1-s9-synthetic-population",
                "declared accumulated stage artifacts", 7, 6, "completed-with-gaps",
                ["m1-s9-visible-gap"], ["unsupported surfaces remain excluded"], [])],
            M1Slice9Composition.ExactExcludedRunInstanceFields,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["provider"] = "not-used",
                ["model"] = "not-used",
                ["credential"] = "not-used",
                ["dns"] = "not-used",
                ["network"] = "not-used",
                ["billable"] = "not-used",
                ["live"] = "not-used",
                ["source-refresh"] = "not-used",
            }, null);
        M1Slice9Composition.Validate(envelope);
        return envelope;
    }
}

public sealed record M1Slice9SemanticEquivalenceProjection(
    string SchemaId,
    int SchemaVersion,
    IReadOnlyList<string> ExcludedRunInstanceFields,
    JsonElement SemanticOutput);

public static class M1Slice9SemanticEquivalence
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
        M1Slice9SemanticEquivalenceProjection projection = new(
            SchemaId, 1, M1Slice9Composition.ExactExcludedRunInstanceFields,
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
                "Slice 9 outputs differ outside the declared run-instance fields; " + detail + ".");
        }
    }

}
