using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.Conclusions;
using Infinium.Analysis.FindingCases;
using Infinium.Application.FindingCases;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

internal static partial class FindingCaseFixtureProductAdapter
{
    private static readonly ContractVersion SchemaVersion = new(1, 0, 0);
    private static readonly UtcTimestamp AssessmentTime = new(
        new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
    private static readonly string[] EvidencePropertyNames =
        ["causal_proof_refs", "applicability_proof_refs", "dependency_proof_refs"];

    private static FindingCaseInputContract CaseInput(
        JsonElement run,
        IReadOnlyList<PriorFindingContract> priorFindings,
        IReadOnlyList<PriorCaseContract> priorCases,
        IReadOnlyList<ProducerCompatibilityContract> compatibilities,
        IReadOnlyList<CoveragePopulationFactContract> populations,
        IReadOnlyList<CoverageMemberFactContract> coverageMembers,
        Dictionary<string, OpaqueId>? priorOccurrenceByCondition)
    {
        string runId = Text(run, "run_fact_id");
        JsonElement[] conditions = run.GetProperty("condition_facts").EnumerateArray().ToArray();
        string producerFamily = Text(conditions[0], "producer_family");
        string producerVersion = Text(conditions[0], "producer_version");
        string analyzer = producerFamily;
        JsonElement proof = run.GetProperty("independent_shared_cause_proof");
        Dictionary<string, string> proofRoles = proof.GetProperty("typed_participant_role_facts").EnumerateArray()
            .ToDictionary(item => Text(item, "participant_id"), item => Text(item, "causal_role"), StringComparer.Ordinal);
        CausalJoinPopulationMember[] members = conditions.Select(condition =>
        {
            string factId = Text(condition, "condition_fact_id");
            string locus = Text(condition, "affected_locus_id");
            string[] dependencies = condition.GetProperty("dependencies").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            CandidateParticipantContract[] participants = dependencies.Select((dependency, index) =>
                    new CandidateParticipantContract(Id(dependency), proofRoles.GetValueOrDefault(dependency,
                        "dependency-" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))))
                .Append(new CandidateParticipantContract(Id(locus), proofRoles.GetValueOrDefault(locus, "affected-locus"))).ToArray();
            OpaqueId[] evidence = StringOrArray(condition.GetProperty("supporting_evidence_refs")).Select(Id).ToArray();
            return new CausalJoinPopulationMember(
                Id(factId), Id(analyzer), CandidateLane.MandatoryEvidence, participants, Text(condition, "cause"),
                participants.Select(item => item.ParticipantId).ToArray(), dependencies.Select(Id).ToArray(),
                evidence, [], [], CausalJoinInputState.Complete, factId, Text(condition, "predicted_impact"))
            {
                SourceFactId = Id("source-" + factId),
            };
        }).ToArray();
        ContractVersion declaredProducerVersion = ContractVersion.Parse(producerVersion);
        CandidateAnalysisContract candidates = Candidates(runId, analyzer, members, declaredProducerVersion);
        Dictionary<string, CandidateHypothesisContract> hypotheses = candidates.Decisions.ToDictionary(
            item => item.PopulationMemberId.Value,
            item => candidates.Hypotheses.Single(hypothesis => hypothesis.CandidateId ==
                candidates.Candidates.Single(candidate => candidate.DecisionId == item.DecisionId).CandidateId),
            StringComparer.Ordinal);
        FindingEvidenceFactContract[] findingFacts = conditions.Select(condition =>
        {
            string factId = Text(condition, "condition_fact_id");
            return new FindingEvidenceFactContract(
                Id("case-finding-fact-" + factId), hypotheses[factId].HypothesisId,
                WorstCredibleConsequence.MeaningfulBoundedLoss, Text(condition, "affected_locus_id"), Text(condition, "cause"),
                condition.GetProperty("applicability").EnumerateArray().Select(item => item.GetString()!).ToArray(),
                [], [], StringOrArray(condition.GetProperty("supporting_evidence_refs")).Select(Id).ToArray());
        }).ToArray();
        CandidateAnalyzerBindingContract binding = candidates.AnalyzerBindings.Single();
        SharedCauseProofContract shared = new(
            Id(Text(proof, "proof_id")), proof.GetProperty("condition_fact_refs").EnumerateArray()
                .Select(item => hypotheses[item.GetString()!].HypothesisId).ToArray(), analyzer,
            binding.SemanticContractVersion, binding.IdentityContractVersion, proofRoles,
            Text(proof, "cause"), string.Join("|", conditions.Select(item => Text(item, "affected_locus_id"))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)),
            proof.GetProperty("applicability").EnumerateArray().Select(item => item.GetString()!).ToArray(),
            FindingCaseIdentity.SharedCauseDependencyClosureId(
                proof.GetProperty("condition_fact_refs").EnumerateArray().SelectMany(item =>
                {
                    CandidateHypothesisContract hypothesis = hypotheses[item.GetString()!];
                    CandidateAnalysisEntryContract candidate = candidates.Candidates.Single(value => value.CandidateId == hypothesis.CandidateId);
                    return candidates.Decisions.Single(value => value.DecisionId == candidate.DecisionId).DependencyIds;
                })),
            findingFacts.SelectMany(item => item.EvidenceIds).Distinct().ToArray())
        {
            AnalyzerVersion = binding.AnalyzerVersion,
        };
        ReconciliationCandidateFactContract[] candidateFacts = priorOccurrenceByCondition is null ? [] : conditions.Select(condition =>
            new ReconciliationCandidateFactContract(
                Id("case-reconciliation-candidates-" + Text(condition, "condition_fact_id")),
                hypotheses[Text(condition, "condition_fact_id")].HypothesisId,
                condition.TryGetProperty("candidate_prior_condition_facts", out JsonElement priorFacts)
                    ? priorFacts.EnumerateArray().Select(item => priorOccurrenceByCondition[item.GetString()!]).ToArray()
                    : [])).ToArray();
        FindingCaseInputContract input = Input(
            candidates, findingFacts, [shared], [], [], populations, coverageMembers, [],
            priorFindings, priorCases, compatibilities, reconciliationCandidateFacts: candidateFacts);
        return input with
        {
            ReconciliationPolicyId = Id("reconciliation-policy-generic-1"),
            ReconciliationPolicyVersion = new ContractVersion(1, 0, 2),
            ReconciliationActorId = Id("deterministic-reconciliation-policy-generic-1"),
        };
    }

    private static ProducerCompatibilityContract Compatibility(
        IdentityEnvelopeContract prior,
        JsonElement currentRun,
        string id)
    {
        JsonElement currentCondition = currentRun.GetProperty("condition_facts")[0];
        string version = Text(currentCondition, "producer_version");
        string family = Text(currentCondition, "producer_family");
        bool caseIdentity = StringComparer.Ordinal.Equals(prior.AnalyzerFamily, family);
        return new ProducerCompatibilityContract(
            Id(id), prior.AnalyzerFamily, prior.SemanticContractVersion, prior.IdentityContractVersion,
            caseIdentity ? family : family + "-" + version.Replace('.', '-'),
            caseIdentity ? ContractVersion.Parse(version) : prior.SemanticContractVersion,
            caseIdentity ? ContractVersion.Parse(version) : prior.IdentityContractVersion,
            true, [Id("producer-contract-evidence-" + id)])
        {
            PriorAnalyzerVersion = prior.AnalyzerVersion,
            CurrentAnalyzerVersion = ContractVersion.Parse(version),
        };
    }

    private static string[] StringOrArray(JsonElement value) => value.ValueKind == JsonValueKind.Array
        ? value.EnumerateArray().Select(item => item.GetString()!).ToArray()
        : [value.GetString()!];

    private static IdentityEnvelopeContract PriorIdentity(
        IdentityEnvelopeContract generated,
        JsonElement prior,
        JsonElement current)
    {
        bool related = current.TryGetProperty("related_condition_fact", out _);
        bool sameCause = StringComparer.Ordinal.Equals(Text(prior, "cause"), Text(current, "cause"));
        string causalCondition = sameCause ? generated.CausalCondition : Text(prior, "cause");
        string affectedLocus = related ? Text(prior, "affected_locus_id") : generated.AffectedLocus;
        string priorVersion = Text(prior, "producer_version");
        string dependencyClosure = FindingCaseIdentity.SharedCauseDependencyClosureId(
            prior.GetProperty("dependencies").EnumerateArray().Select(item => Id(item.GetString()!))).Value;
        return Identity(
            Text(prior, "producer_family"),
            ContractVersion.Parse(priorVersion), causalCondition, affectedLocus,
            prior.GetProperty("applicability").EnumerateArray().Select(item => item.GetString()!).ToArray(), dependencyClosure,
            generated.ParticipantsAndRoles);
    }

    private static TaxonomyClassificationFactContract ClassificationFromAssignment(
        JsonElement assignment,
        OpaqueId hypothesisId,
        OpaqueId defaultActor,
        bool sourceIdentity)
    {
        string assignmentId = Text(assignment, "assignment_id");
        return new TaxonomyClassificationFactContract(
            Id("classification-fact-" + assignmentId), hypothesisId, Text(assignment, "taxonomy_id"),
            ContractVersion.Parse(Text(assignment, "taxonomy_version")), Text(assignment, "axis"),
            Text(assignment, "facet"), Text(assignment, "code"),
            ParseTaxonomyApplicability(Text(assignment, "applicability_state")),
            ParseClassificationRole(Text(assignment, "classification_role")),
            assignment.GetProperty("evidence_refs").EnumerateArray().Select(item => Id(item.GetString()!)).ToArray(),
            (assignment.TryGetProperty("applicability_condition_refs", out JsonElement conditions)
                ? conditions : assignment.GetProperty("condition_refs")).EnumerateArray()
                .Select(item => Id(item.GetString()!)).ToArray(), null,
            assignment.TryGetProperty("analyzer_or_adjudicator", out JsonElement actor)
                ? Id(actor.GetString()!) : defaultActor,
            new UtcTimestamp(DateTimeOffset.Parse(
                Text(assignment, "created_at"), System.Globalization.CultureInfo.InvariantCulture)),
            Text(assignment, "reason"), sourceIdentity ? Id(assignmentId) : null,
            assignment.TryGetProperty("supersedes_assignment_id", out JsonElement supersedes)
                && supersedes.ValueKind == JsonValueKind.String ? Id(supersedes.GetString()!) : null);
    }

    private static TaxonomyProjectionInputContract Projection(
        TaxonomyClassificationFactContract source,
        string targetCode,
        OpaqueId authority,
        OpaqueId actor,
        UtcTimestamp created) => new(
            source.FactId, "infinium.test.taxonomy", new ContractVersion(2, 0, 0),
            source.Axis, source.Facet, targetCode, TaxonomyApplicability.Assigned, authority,
            source.EvidenceIds, "Non-product mapping derived from explicit split or merge facts.",
            ClassificationRole.Established, actor, created);

    private static List<TaxonomyEvidenceStatement> TypedTaxonomyStatements(
        JsonElement subject, OpaqueId[] evidence, ref int evidenceIndex)
    {
        List<TaxonomyEvidenceStatement> statements = [];
        int index = evidenceIndex;
        void Add(TaxonomyEvidenceKind kind, OpaqueId evidenceId, string reason) =>
            statements.Add(new(kind, [evidenceId], reason));
        OpaqueId NextEvidence()
        {
            int retained = Math.Min(index, evidence.Length - 1);
            if (index < evidence.Length - 1)
            {
                index++;
            }

            return evidence[retained];
        }

        string[] claims = subject.TryGetProperty("author_claims", out JsonElement authorClaims)
            ? authorClaims.EnumerateArray().Select(item => item.GetString()!).ToArray() : [];
        foreach (string text in claims)
        {
            OpaqueId source = NextEvidence();
            if (text.Contains("replaces", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.DeclaredReplacement, source, "Typed author declaration establishes replacement purpose.");
            }

            if (text.Contains("runtime framework", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.DeclaredRuntimeFramework, source, "Typed author declaration establishes runtime-framework purpose.");
            }
        }
        string[] observations = subject.TryGetProperty("qualified_local_observations", out JsonElement local)
            ? local.EnumerateArray().Select(item => item.GetString()!).ToArray() : [];
        foreach (string text in observations)
        {
            OpaqueId source = NextEvidence();
            if (text.Contains("plugin data", StringComparison.OrdinalIgnoreCase)
                || text.Contains("plugin container data", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.ObservedPluginData, source, "Qualified local observation establishes plugin data.");
            }

            if (text.Contains("loose texture material", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.ObservedAsset, source, "Qualified local observation establishes an asset surface.");
                Add(TaxonomyEvidenceKind.ObservedTextureMaterial, source, "Qualified local observation establishes texture material.");
                Add(TaxonomyEvidenceKind.ObservedLooseData, source, "Qualified local observation establishes loose delivery.");
                if (claims.Any(item => item.Contains("visual presentation", StringComparison.OrdinalIgnoreCase)))
                {
                    Add(TaxonomyEvidenceKind.EstablishedVisualPresentation, source, "Qualified evidence establishes visual presentation area.");
                }
            }
            if (text.Contains("plugin container", StringComparison.OrdinalIgnoreCase)
                || (text.Contains("plugin data", StringComparison.OrdinalIgnoreCase) && text.Contains("supplied", StringComparison.OrdinalIgnoreCase)))
            {
                Add(TaxonomyEvidenceKind.ObservedPluginContainer, source, "Qualified local observation establishes plugin-container delivery.");
            }

            if (text.Contains("compiled script logic", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.ObservedCompiledPapyrus, source, "Qualified local observation establishes compiled script logic.");
            }

            if (text.Contains("native runtime logic", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.ObservedNativeRuntime, source, "Qualified local observation establishes native runtime logic.");
            }

            if (text.Contains("runtime support data", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.ObservedRuntimeSupportData, source, "Qualified local observation establishes runtime support data.");
            }

            if (text.Contains("game root", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.ObservedGameRootDelivery, source, "Qualified local observation establishes game-root delivery.");
            }

            if (text.Contains("action scheduling", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.EstablishedActorScheduling, source, "Qualified local observation establishes actor scheduling.");
            }

            if (text.Contains("placed-object activation", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.EstablishedPlacedObjectActivation, source, "Qualified local observation establishes placed-object activation.");
            }

            if (text.Contains("appearance identity", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.EstablishedAppearanceIdentity, source, "Qualified local observation establishes appearance identity.");
            }

            if (text.Contains("visual presentation", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.EstablishedVisualPresentation, source, "Qualified local observation establishes visual presentation.");
            }

            if (text.Contains("framework services", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.EstablishedFrameworkServices, source, "Qualified local observation establishes framework services.");
            }
        }
        string[] effects = subject.TryGetProperty("bounded_effect_facts", out JsonElement bounded)
            ? bounded.EnumerateArray().Select(item => item.GetString()!).ToArray() : [];
        foreach (string text in effects)
        {
            OpaqueId source = NextEvidence();
            if (text.Contains("progression-objective", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.PredictedQuestProgression, source, "Bounded effect establishes quest progression area.");
            }

            if (text.Contains("interface-control", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.PredictedInterfaceControl, source, "Bounded effect establishes interface control area.");
            }

            if (text.Contains("become unavailable", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.PredictedContentUnavailable, source, "Bounded effect establishes unavailable content consequence.");
            }

            if (text.Contains("incorrect", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.PredictedIncorrectBehavior, source, "Bounded effect establishes incorrect functional behavior.");
            }

            if (text.Contains("across features", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.PredictedCrossFeaturePropagation, source, "Bounded effect establishes cross-feature propagation.");
            }

            if (text.Contains("one local instance", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.PredictedSingleInstance, source, "Bounded effect establishes single-instance breadth.");
            }

            if (text.Contains("one point", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.PredictedSinglePoint, source, "Bounded effect establishes single-point breadth.");
            }

            if (text.Contains("persists until installation changes", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.PredictedInstallationPersistence, source, "Bounded effect establishes installation persistence.");
            }

            if (text.Contains("across systems", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.PredictedCrossSystemPropagation, source, "Bounded effect establishes cross-system propagation.");
            }

            if (text.Contains("non-problematic", StringComparison.OrdinalIgnoreCase))
            {
                Add(TaxonomyEvidenceKind.ConsequenceNotApplicable, source, "No problematic consequence applies.");
                Add(TaxonomyEvidenceKind.SubjectExtentNotApplicable, source, "Problem extent is not applicable.");
                Add(TaxonomyEvidenceKind.SpatialExtentNotApplicableEstablished, source, "Problem extent is not applicable.");
                Add(TaxonomyEvidenceKind.PersistenceExtentNotApplicable, source, "Problem extent is not applicable.");
                Add(TaxonomyEvidenceKind.PropagationExtentNotApplicable, source, "Problem extent is not applicable.");
            }
        }
        string observation = string.Join(" ", observations);
        OpaqueId first = evidence[0];
        if (StringComparer.Ordinal.Equals(observation, "effective plugin data is present"))
        {
            Add(TaxonomyEvidenceKind.PurposeUnknown, first, "No applicable purpose evidence is available.");
            Add(TaxonomyEvidenceKind.AreaUnsupported, first, "Affected area is outside current analyzer support.");
            Add(TaxonomyEvidenceKind.ConsequenceUnsupported, first, "Consequence is outside current analyzer support.");
            Add(TaxonomyEvidenceKind.SubjectExtentUnsupported, first, "Subject extent is outside current analyzer support.");
            Add(TaxonomyEvidenceKind.SpatialExtentUnsupported, first, "Spatial extent is outside current analyzer support.");
            Add(TaxonomyEvidenceKind.PersistenceExtentUnsupported, first, "Persistence extent is outside current analyzer support.");
            Add(TaxonomyEvidenceKind.PropagationExtentUnsupported, first, "Propagation extent is outside current analyzer support.");
        }
        else if (StringComparer.Ordinal.Equals(observation, "effective plugin container data is present"))
        {
            Add(TaxonomyEvidenceKind.AreaUnsupported, first, "Affected area is outside current analyzer support.");
            Add(TaxonomyEvidenceKind.ConsequenceUnknown, first, "Consequence remains unresolved.");
            Add(TaxonomyEvidenceKind.SubjectExtentUnknown, first, "Subject extent remains unresolved.");
            Add(TaxonomyEvidenceKind.SpatialExtentNotApplicable, first, "Spatial extent does not apply.");
            Add(TaxonomyEvidenceKind.PersistenceExtentUnknown, first, "Persistence extent remains unresolved.");
            Add(TaxonomyEvidenceKind.PropagationExtentUnknown, first, "Propagation extent remains unresolved.");
        }
        if (observation.Contains("native runtime logic is delivered at the game root", StringComparison.Ordinal))
        {
            Add(TaxonomyEvidenceKind.AreaUnmapped, evidence[Math.Min(1, evidence.Length - 1)], "Established affected concept is absent from the current taxonomy.");
        }

        if (Text(subject, "subject_type") == "provider-topology-observation")
        {
            Add(TaxonomyEvidenceKind.ProviderTopologyNonApplicable, first, "Provider topology alone does not establish semantic taxonomy classification.");
        }

        evidenceIndex = index;
        return statements;
    }

    private static OpaqueId[] SubjectEvidence(JsonElement subject)
    {
        if (subject.TryGetProperty("evidence_refs", out JsonElement evidence))
        {
            return evidence.EnumerateArray().Select(item => Id(item.GetString()!)).ToArray();
        }
        if (subject.TryGetProperty("raw_evidence_refs", out JsonElement raw))
        {
            return raw.EnumerateArray().Select(item => Id(item.GetString()!)).ToArray();
        }
        if (subject.TryGetProperty("historical_assignment", out JsonElement historical))
        {
            return historical.GetProperty("evidence_refs").EnumerateArray().Select(item => Id(item.GetString()!)).ToArray();
        }
        throw new InvalidDataException("A taxonomy subject requires retained evidence facts.");
    }

    private static TaxonomyApplicability ParseTaxonomyApplicability(string value) => value switch
    {
        "assigned" => TaxonomyApplicability.Assigned,
        "unknown" => TaxonomyApplicability.Unknown,
        "unsupported" => TaxonomyApplicability.Unsupported,
        "unmapped" => TaxonomyApplicability.Unmapped,
        "not-applicable" => TaxonomyApplicability.NotApplicable,
        _ => throw new InvalidDataException("Taxonomy applicability is outside the closed set."),
    };

    private static ClassificationRole ParseClassificationRole(string value) => value switch
    {
        "declared" => ClassificationRole.Declared,
        "observed" => ClassificationRole.Observed,
        "predicted" => ClassificationRole.Predicted,
        "established" => ClassificationRole.Established,
        _ => throw new InvalidDataException("Classification role is outside the closed set."),
    };

    private static IdentityEnvelopeContract Identity(
        string analyzer,
        ContractVersion version,
        string cause,
        string locus,
        IReadOnlyList<string> applicability,
        string dependency,
        IReadOnlyDictionary<string, string>? participants = null)
    {
        IdentityEnvelopeContract identity = new(
            analyzer, version, version, participants ?? new Dictionary<string, string> { [dependency] = "dependency" },
            cause, locus, applicability, Id(dependency), new Sha256Fingerprint(new string('0', 64)))
        {
            AnalyzerVersion = version,
        };
        return identity with { CanonicalSignature = FindingCaseIdentity.ComputeIdentitySignature(identity) };
    }

    private static SharedCauseProofContract SingleProof(
        CandidateHypothesisContract hypothesis,
        CandidateAnalysisContract candidates,
        string cause,
        string locus,
        IReadOnlyList<string> applicability,
        IReadOnlyList<OpaqueId> evidence)
    {
        CandidateAnalysisEntryContract candidate = candidates.Candidates.Single(item => item.CandidateId == hypothesis.CandidateId);
        CandidateDecisionContract decision = candidates.Decisions.Single(item => item.DecisionId == candidate.DecisionId);
        CandidateAnalyzerBindingContract binding = candidates.AnalyzerBindings.Single(item => item.AnalyzerId == decision.AnalyzerId);
        return new SharedCauseProofContract(
            Id("cause-proof-" + hypothesis.HypothesisId.Value), [hypothesis.HypothesisId], binding.AnalyzerFamily,
            binding.SemanticContractVersion, binding.IdentityContractVersion,
            decision.Participants.ToDictionary(item => item.ParticipantId.Value, item => item.Role, StringComparer.Ordinal),
            cause, locus, applicability, FindingCaseIdentity.SharedCauseDependencyClosureId(decision.DependencyIds), evidence)
        {
            AnalyzerVersion = binding.AnalyzerVersion,
        };
    }

    private static FindingCaseInputContract Input(
        CandidateAnalysisContract candidates,
        IReadOnlyList<FindingEvidenceFactContract> findingFacts,
        IReadOnlyList<SharedCauseProofContract> proofs,
        IReadOnlyList<TaxonomyClassificationFactContract> taxonomy,
        IReadOnlyList<TaxonomyProjectionInputContract> projections,
        IReadOnlyList<CoveragePopulationFactContract> populations,
        IReadOnlyList<CoverageMemberFactContract> members,
        IReadOnlyList<CoverageFailureFactContract> failures,
        IReadOnlyList<PriorFindingContract> priorFindings,
        IReadOnlyList<PriorCaseContract> priorCases,
        IReadOnlyList<ProducerCompatibilityContract> compatibilities,
        IReadOnlyList<RelatedFindingFactContract>? relatedFindingFacts = null,
        IReadOnlyList<TaxonomySubjectFact>? taxonomySubjects = null,
        IReadOnlyList<ReconciliationCandidateFactContract>? reconciliationCandidateFacts = null) => FindingCaseInputProducer.Create(new FindingCaseInputBuildRequest(
            Id("fixture-promotion-policy"), SchemaVersion, Id("fixture-reconciliation-policy"), SchemaVersion,
            Id("fixture-reconciliation-actor"), AssessmentTime, candidates, findingFacts,
            RecommendationFacts(findingFacts), proofs,
            taxonomySubjects ?? [], taxonomy, projections, populations, members, failures,
            priorFindings, priorCases, compatibilities, relatedFindingFacts ?? [], Boundaries())
        {
            ReconciliationCandidateFacts = reconciliationCandidateFacts ?? [],
        });

    private static FindingRecommendationFactContract[] RecommendationFacts(
        IReadOnlyList<FindingEvidenceFactContract> facts) => facts.Select(item => new FindingRecommendationFactContract(
            Id("recommendation-fact-" + item.FactId.Value), item.HypothesisId, RecommendationKind.Validation,
            "Validate the typed causal condition.", "Bounded to supplied typed evidence.",
            "No installed state is changed by analysis.", ["Applicability must remain valid."],
            "Reobserve the affected locus.", item.EvidenceIds)).ToArray();

    private static FindingCaseInputContract Reidentify(FindingCaseInputContract input) =>
        input with { InputId = FindingCaseIdentity.ComputeInputId(input) };

    private static CandidateAnalysisContract EmptyCandidates(string runId, string analyzer) =>
        Candidates(runId, analyzer, []);

    private static CandidateAnalysisContract Candidates(
        string runId,
        string analyzer,
        IReadOnlyList<CausalJoinPopulationMember> members,
        ContractVersion? producerVersion = null) => CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id(runId), Id("population-" + runId), Id("fixture-candidate-policy"), Id("fixture-threshold"),
            CandidateExecutionLimits.Default, new CandidatePopulationContext(null),
            [new FixtureSource(Id(analyzer), members, producerVersion)])).Analysis;

    private static IEnumerable<OpaqueId> Evidence(JsonElement current) =>
        EvidencePropertyNames
            .Where(name => current.TryGetProperty(name, out _))
            .SelectMany(name => current.GetProperty(name).EnumerateArray().Select(item => Id(item.GetString()!)))
            .Distinct();

    private static CoverageMemberState CoverageStateFrom(string fact) => fact switch
    {
        _ when fact.Contains("failed", StringComparison.Ordinal) => CoverageMemberState.Failed,
        _ when fact.Contains("configuration", StringComparison.Ordinal) => CoverageMemberState.SkippedByConfiguration,
        _ when fact.Contains("configured", StringComparison.Ordinal) || fact.Contains("limit", StringComparison.Ordinal) => CoverageMemberState.SkippedByLimit,
        _ when fact.Contains("unavailable", StringComparison.Ordinal) || fact.Contains("not implemented", StringComparison.Ordinal)
            || fact.Contains("classification unsupported", StringComparison.Ordinal) => CoverageMemberState.Unsupported,
        _ when fact.Contains("with", StringComparison.Ordinal) || fact.Contains("incomplete", StringComparison.Ordinal)
            || fact.Contains("classification unmapped", StringComparison.Ordinal) => CoverageMemberState.CompletedWithGaps,
        _ => CoverageMemberState.Completed,
    };

    private static IReadOnlyList<ExecutionBoundaryContract> Boundaries() =>
    [
        new("provider", BoundaryUseState.NotUsed, "answer-free local fixture"),
        new("hosted-search", BoundaryUseState.NotUsed, "answer-free local fixture"),
        new("nexus", BoundaryUseState.NotUsed, "answer-free local fixture"),
        new("loot", BoundaryUseState.NotUsed, "not configured"),
    ];

    private static string Text(JsonElement value, string property) =>
        value.GetProperty(property).GetString() ?? throw new InvalidDataException($"{property} is required.");

    private static string Text(JsonElement value) =>
        value.GetString() ?? throw new InvalidDataException("A string fact is required.");

    private static OpaqueId Id(string value) => new(value);

    private sealed class FixtureSource(
        OpaqueId analyzerId,
        IReadOnlyList<CausalJoinPopulationMember> members,
        ContractVersion? producerVersion = null,
        string? analyzerFamily = null)
        : ICandidatePopulationSource
    {
        public OpaqueId AnalyzerId => analyzerId;

        public AnalyzerDeclarationContract Declaration { get; } = CreateDeclaration(
            analyzerId, members, producerVersion, analyzerFamily);

        private static AnalyzerDeclarationContract CreateDeclaration(
            OpaqueId id,
            IReadOnlyList<CausalJoinPopulationMember> sourceMembers,
            ContractVersion? version,
            string? family)
        {
            AnalyzerDeclarationContract declaration = CandidateAnalyzerDeclarations.Create(
                id, Math.Max(1, sourceMembers.Count), 1_000_000,
                supportedShapes: sourceMembers.Select(item => item.JoinKind).Distinct(StringComparer.Ordinal)
                    .DefaultIfEmpty("empty").ToArray());
            return declaration with
            {
                AnalyzerFamily = family ?? id.Value,
                AnalyzerVersion = version ?? declaration.AnalyzerVersion,
                SemanticContractVersion = version ?? declaration.SemanticContractVersion,
                IdentityContractVersion = version ?? declaration.IdentityContractVersion,
            };
        }

        public IReadOnlyList<CausalJoinPopulationMember> DeclarePopulation(
            CandidatePopulationContext context, CancellationToken cancellationToken = default) => members;

        public IReadOnlyList<CausalJoinPopulationMember> ConstructPopulation(
            CandidatePopulationContext context, CancellationToken cancellationToken = default) => members;
    }
}

internal sealed record ReconciliationObservation(
    string CurrentFactId,
    IReadOnlyList<string> PriorOccurrenceIds,
    ReconciliationOutcome Outcome,
    ReconciliationGatesContract Gates,
    ContractVersion PolicyVersion,
    OpaqueId ActorId,
    bool VisibleByDefault,
    IReadOnlyList<string> ProofEvidenceIds,
    IReadOnlyList<string> ConsideredOccurrenceIds,
    IReadOnlyList<string> Gaps);

internal sealed record ReconciliationExecution(
    FindingCaseContract Output,
    IReadOnlyList<ReconciliationObservation> Observations,
    IReadOnlyDictionary<string, OpaqueId> OccurrenceByFact);

internal sealed record LeadPromotionExecution(
    FindingCaseContract Prior,
    FindingCaseContract Current);

internal sealed record TaxonomyExecution(
    FindingCaseContract Output,
    IReadOnlyDictionary<OpaqueId, string> SubjectByHypothesis);

internal sealed record CaseReconciliationExecution(
    FindingCaseContract Prior,
    FindingCaseContract Current,
    FindingCaseContract Lookalike);
