using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.Cases;
using Infinium.Analysis.Conclusions;
using Infinium.Application.FindingCases;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

internal static class FindingCaseFixtureProductAdapter
{
    private static readonly ContractVersion SchemaVersion = new(1, 0, 0);
    private static readonly UtcTimestamp AssessmentTime = new(
        new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));
    private static readonly string[] EvidencePropertyNames =
        ["causal_proof_refs", "applicability_proof_refs", "dependency_proof_refs"];

    public static FindingCaseContract ExecuteCoverage(JsonElement factual)
    {
        const string runId = "fixture-coverage-run";
        CandidateAnalysisContract candidates = EmptyCandidates(runId, "fixture-coverage-analyzer");
        List<CoveragePopulationFactContract> populations = [];
        List<CoverageMemberFactContract> members = [];
        Dictionary<string, CoverageFailureFactContract> failures = new(StringComparer.Ordinal);
        foreach (JsonElement population in factual.GetProperty("coverage_matrix_population_facts").EnumerateArray())
        {
            string populationId = Text(population, "population_id");
            OpaqueId analyzerId = Id("coverage-analyzer-" + populationId);
            populations.Add(new CoveragePopulationFactContract(
                Id("coverage-population-" + populationId), analyzerId, populationId, "eligible population members"));
            foreach (JsonElement member in population.GetProperty("members").EnumerateArray())
            {
                string memberId = Text(member, "member_id");
                string work = member.TryGetProperty("work_fact", out JsonElement workFact)
                    ? workFact.GetString()! : "classification " + Text(member, "classification_fact");
                CoverageMemberState state = CoverageStateFrom(work);
                OpaqueId? failureId = member.TryGetProperty("failure_id", out JsonElement failureValue)
                    ? Id(failureValue.GetString()!) : null;
                if (failureId is not null)
                {
                    OpaqueId retainedFailureId = failureId;
                    failures.TryAdd(retainedFailureId.Value, new CoverageFailureFactContract(
                        retainedFailureId, analyzerId, "fixture-work-failed", work, Retryable: false));
                }
                OpaqueId? gapId = member.TryGetProperty("gap_id", out JsonElement gapValue)
                    ? Id(gapValue.GetString()!) : null;
                string reason = member.TryGetProperty("exclusion_reason", out JsonElement exclusion)
                    ? exclusion.GetString()! : work;
                members.Add(new CoverageMemberFactContract(
                    Id("coverage-member-fact-" + populationId + "-" + memberId), analyzerId,
                    populationId, "eligible population members", Id(memberId), state, reason,
                    state == CoverageMemberState.Completed ? "none" : work, failureId, [], gapId));
            }
        }
        FindingCaseInputContract input = Input(
            candidates, [], [], [], [], populations, members, failures.Values.ToArray(), [], [], []);
        return FindingCasePipeline.Execute(Reidentify(input));
    }

    public static FindingCaseContract ExecuteCoverageVariant(JsonElement variant)
    {
        string variantId = Text(variant, "variant_id");
        JsonElement[] hypothesisFacts = variant.GetProperty("hypothesis_facts").EnumerateArray().ToArray();
        CausalJoinPopulationMember[] candidateMembers = hypothesisFacts.Select(fact =>
        {
            string hypothesisId = Text(fact, "hypothesis_id");
            OpaqueId[] evidence = fact.GetProperty("supporting_evidence_refs").EnumerateArray()
                .Select(item => Id(item.GetString()!)).ToArray();
            string[] missing = fact.GetProperty("missing_information").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            return new CausalJoinPopulationMember(
                Id(hypothesisId), Id("boundary-analyzer-" + variantId), CandidateLane.MandatoryEvidence,
                [new CandidateParticipantContract(Id("boundary-source-" + hypothesisId), "source"),
                    new CandidateParticipantContract(Id("boundary-target-" + hypothesisId), "target")],
                "boundary-condition-" + hypothesisId,
                [Id("boundary-source-" + hypothesisId), Id("boundary-target-" + hypothesisId)],
                [Id("boundary-dependency-" + hypothesisId)], evidence, [], missing,
                missing.Length == 0 ? CausalJoinInputState.Complete : CausalJoinInputState.Ambiguous,
                "Typed boundary hypothesis.", "Bounded boundary consequence.")
            {
                SourceFactId = Id("boundary-source-fact-" + hypothesisId),
            };
        }).ToArray();
        CandidateAnalysisContract candidates = candidateMembers.Length == 0
            ? EmptyCandidates("boundary-" + variantId, "boundary-analyzer-" + variantId)
            : Candidates("boundary-" + variantId, "boundary-analyzer-" + variantId, candidateMembers);
        Dictionary<string, CandidateHypothesisContract> hypotheses = candidates.Decisions.ToDictionary(
            item => item.PopulationMemberId.Value,
            item => candidates.Hypotheses.Single(hypothesis => hypothesis.CandidateId == candidates.Candidates
                .Single(candidate => candidate.DecisionId == item.DecisionId).CandidateId), StringComparer.Ordinal);
        FindingEvidenceFactContract[] findingFacts = hypothesisFacts.Select(fact =>
        {
            string hypothesisId = Text(fact, "hypothesis_id");
            return new FindingEvidenceFactContract(
                Id("boundary-finding-fact-" + hypothesisId), hypotheses[hypothesisId].HypothesisId,
                WorstCredibleConsequence.MeaningfulBoundedLoss, "boundary-locus-" + hypothesisId,
                "boundary-condition-" + hypothesisId,
                fact.TryGetProperty("applicability_predicates", out JsonElement applicability)
                    ? applicability.EnumerateArray().Select(item => item.GetString()!).ToArray() : ["boundary-scope"],
                [], [], fact.GetProperty("supporting_evidence_refs").EnumerateArray()
                    .Select(item => Id(item.GetString()!)).ToArray());
        }).ToArray();
        SharedCauseProofContract[] proofs = findingFacts.Select(fact => SingleProof(
            hypotheses.Single(item => item.Value.HypothesisId == fact.HypothesisId).Value, candidates,
            fact.CausalCondition, fact.AffectedLocus, fact.ApplicabilityPredicates, fact.EvidenceIds)).ToArray();
        List<CoveragePopulationFactContract> populations = [];
        List<CoverageMemberFactContract> members = [];
        List<CoverageFailureFactContract> failures = [];
        foreach (JsonElement population in variant.GetProperty("population_facts").EnumerateArray())
        {
            string populationId = Text(population, "population_id");
            OpaqueId analyzer = Id("boundary-coverage-analyzer-" + populationId);
            populations.Add(new CoveragePopulationFactContract(Id("boundary-population-" + populationId), analyzer,
                populationId, "boundary eligible members"));
            if (population.TryGetProperty("eligible_members", out JsonElement eligible))
            {
                HashSet<string> completed = population.TryGetProperty("completed_members", out JsonElement completedElement)
                    ? completedElement.EnumerateArray().Select(item => item.GetString()!).ToHashSet(StringComparer.Ordinal) : [];
                foreach (JsonElement member in eligible.EnumerateArray())
                {
                    string memberId = member.GetString()!;
                    bool done = completed.Contains(memberId);
                    OpaqueId? gapId = done || !population.TryGetProperty("gap_id", out JsonElement gap)
                        ? null : Id(gap.GetString()!);
                    members.Add(new CoverageMemberFactContract(Id("boundary-member-" + memberId), analyzer,
                        populationId, "boundary eligible members", Id(memberId),
                        done ? CoverageMemberState.Completed : CoverageMemberState.Unsupported,
                        done ? "completed" : "unsupported capability", done ? "none" : "unsupported capability",
                        null, [], gapId));
                }
            }
            else
            {
                foreach (JsonElement member in population.GetProperty("members").EnumerateArray())
                {
                    string memberId = Text(member, "member_id");
                    string work = Text(member, "work_fact");
                    CoverageMemberState state = CoverageStateFrom(work);
                    OpaqueId? failureId = member.TryGetProperty("failure_id", out JsonElement failure)
                        ? Id(failure.GetString()!) : null;
                    if (failureId is not null)
                    {
                        failures.Add(new CoverageFailureFactContract(failureId, analyzer, "boundary-failure", work, false));
                    }
                    members.Add(new CoverageMemberFactContract(Id("boundary-member-" + memberId), analyzer,
                        populationId, "boundary eligible members", Id(memberId), state,
                        member.TryGetProperty("exclusion_reason", out JsonElement exclusion) ? exclusion.GetString()! : work,
                        state == CoverageMemberState.Completed ? "none" : work, failureId, [],
                        state is CoverageMemberState.Completed or CoverageMemberState.Failed ? null : Id("boundary-gap-" + memberId)));
                }
            }
        }
        FindingCaseInputContract input = Input(candidates, findingFacts, proofs, [], [], populations, members,
            failures, [], [], []);
        return FindingCasePipeline.Execute(Reidentify(input));
    }

    public static TaxonomyExecution ExecuteTaxonomy(JsonElement factual)
    {
        const string runId = "fixture-taxonomy-run";
        JsonElement[] subjects = factual.GetProperty("subject_facts").EnumerateArray().ToArray();
        List<CausalJoinPopulationMember> members = [];
        foreach (JsonElement subject in subjects)
        {
            string subjectId = Text(subject, "subject_id");
            OpaqueId[] evidence = SubjectEvidence(subject);
            members.Add(new CausalJoinPopulationMember(
                Id(subjectId), Id("fixture-taxonomy-analyzer"), CandidateLane.MandatoryEvidence,
                [new CandidateParticipantContract(Id(subjectId), "subject"),
                    new CandidateParticipantContract(Id("taxonomy-classifier"), "classifier")],
                "taxonomy-subject", [Id(subjectId), Id("taxonomy-classifier")], [Id("taxonomy-classifier")],
                evidence, [], ["finding conclusion not requested"], CausalJoinInputState.Ambiguous,
                "Classify the supplied subject facts.", "No finding consequence is inferred by taxonomy classification.")
            {
                SourceFactId = Id("source-" + subjectId),
            });
        }
        CandidateAnalysisContract candidates = Candidates(runId, "fixture-taxonomy-analyzer", members);
        Dictionary<string, OpaqueId> hypothesisBySubject = candidates.Decisions.ToDictionary(
            decision => decision.PopulationMemberId.Value,
            decision => candidates.Candidates.Single(item => item.DecisionId == decision.DecisionId).HypothesisId!,
            StringComparer.Ordinal);
        Dictionary<OpaqueId, string> subjectByHypothesis = hypothesisBySubject.ToDictionary(
            item => item.Value, item => item.Key);
        JsonElement execution = factual.GetProperty("classification_execution_facts");
        OpaqueId actor = Id(Text(execution, "analyzer_or_adjudicator"));
        UtcTimestamp created = new(DateTimeOffset.Parse(
            Text(execution, "created_at"), System.Globalization.CultureInfo.InvariantCulture));
        List<TaxonomyClassificationFactContract> classificationFacts = [];
        List<TaxonomySubjectFact> productSubjects = [];
        foreach (JsonElement subject in subjects)
        {
            string subjectId = Text(subject, "subject_id");
            if (subject.TryGetProperty("historical_assignment", out JsonElement historical))
            {
                classificationFacts.Add(ClassificationFromAssignment(
                    historical, hypothesisBySubject[subjectId], actor, sourceIdentity: true));
                continue;
            }
            if (subject.TryGetProperty("test_taxonomy_v1_assignments", out JsonElement testAssignments))
            {
                classificationFacts.AddRange(testAssignments.EnumerateArray().Select(item =>
                    ClassificationFromAssignment(item, hypothesisBySubject[subjectId], actor, sourceIdentity: true)));
                continue;
            }
            int evidenceIndex = 0;
            OpaqueId[] evidence = SubjectEvidence(subject);
            productSubjects.Add(new TaxonomySubjectFact(
                Id(subjectId), hypothesisBySubject[subjectId], Text(subject, "subject_type"),
                TypedTaxonomyStatements(subject, evidence, ref evidenceIndex),
                subject.TryGetProperty("condition_refs", out JsonElement conditions)
                    ? conditions.EnumerateArray().Select(item => Id(item.GetString()!)).ToArray() : [],
                actor, created));
        }

        JsonElement mapping = factual.GetProperty("non_product_test_taxonomy_facts");
        List<TaxonomyProjectionInputContract> projections = [];
        Dictionary<string, TaxonomyClassificationFactContract> sourceByCode = classificationFacts
            .Where(item => item.TaxonomyId == "infinium.test.taxonomy" && item.Code is not null)
            .ToDictionary(item => item.Code!, StringComparer.Ordinal);
        foreach (JsonElement mappingFact in mapping.GetProperty("mapping_facts").EnumerateArray())
        {
            string kind = Text(mappingFact, "mapping_kind");
            if (kind == "split")
            {
                string sourceCode = Text(mappingFact, "source_code");
                foreach (JsonElement target in mappingFact.GetProperty("target_codes").EnumerateArray())
                {
                    projections.Add(Projection(
                        sourceByCode[sourceCode], target.GetString()!, Id("test-map-split-motion"), actor, created));
                }
            }
            else
            {
                foreach (JsonElement source in mappingFact.GetProperty("source_codes").EnumerateArray())
                {
                    projections.Add(Projection(
                        sourceByCode[source.GetString()!], Text(mappingFact, "target_code"),
                        Id("test-map-merge-delivery"), actor, created));
                }
            }
        }
        FindingCaseInputContract input = Input(
            candidates, [], [], classificationFacts, projections, [], [], [], [], [], [],
            taxonomySubjects: productSubjects);
        return new TaxonomyExecution(FindingCasePipeline.Execute(Reidentify(input)), subjectByHypothesis);
    }

    public static ReconciliationExecution ExecuteReconciliation(JsonElement factual, bool reverseCurrentOrder = false)
    {
        JsonElement policy = factual.GetProperty("policy_facts");
        Dictionary<string, JsonElement> priors = factual.GetProperty("prior_finding_occurrences").EnumerateArray()
            .ToDictionary(item => Text(item, "occurrence_id"), StringComparer.Ordinal);
        Dictionary<string, JsonElement> producerFacts = factual.GetProperty("producer_contract_facts").EnumerateArray()
            .ToDictionary(item => Text(item, "version"), StringComparer.Ordinal);
        JsonElement[] currentFacts = factual.GetProperty("current_analytical_facts").EnumerateArray().ToArray();
        if (reverseCurrentOrder)
        {
            Array.Reverse(currentFacts);
        }
        Dictionary<string, CausalJoinPopulationMember> memberByFact = [];
        foreach (JsonElement current in currentFacts)
        {
            string currentFactId = Text(current, "fact_id");
            string currentVersion = Text(current, "producer_version");
            string analyzer = Text(current, "producer_family") + "-" + currentVersion.Replace('.', '-');
            OpaqueId[] support = Evidence(current).ToArray();
            CandidateParticipantContract[] participants = current.GetProperty("dependencies").EnumerateArray()
                .Select((item, index) => new CandidateParticipantContract(
                    Id(item.GetString()!), "dependency-" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))
                .Append(new CandidateParticipantContract(Id(Text(current, "affected_locus_id")), "affected-locus"))
                .ToArray();
            memberByFact.Add(currentFactId, new CausalJoinPopulationMember(
                Id(currentFactId), Id(analyzer), CandidateLane.MandatoryEvidence,
                participants,
                Text(current, "cause"), participants.Select(item => item.ParticipantId).ToArray(),
                current.GetProperty("dependencies").EnumerateArray().Select(item => Id(item.GetString()!)).ToArray(),
                support, [], [], CausalJoinInputState.Complete, Text(current, "conclusion"),
                "bounded functional consequence")
            {
                SourceFactId = Id("source-" + currentFactId),
            });
        }
        ICandidatePopulationSource[] sources = memberByFact.Values
            .GroupBy(item => item.AnalyzerId)
            .Select(group =>
            {
                JsonElement sourceFact = currentFacts.First(item =>
                {
                    string version = Text(item, "producer_version");
                    return group.Key.Value == Text(item, "producer_family") + "-" + version.Replace('.', '-');
                });
                return (ICandidatePopulationSource)new FixtureSource(
                    group.Key, group.ToArray(), ContractVersion.Parse(Text(sourceFact, "producer_version")),
                    Text(sourceFact, "producer_family"));
            })
            .ToArray();
        CandidateAnalysisContract candidates = CandidatePipeline.Execute(new CandidatePipelineRequest(
            Id("fixture-reconciliation-global"), Id("population-fixture-reconciliation-global"),
            Id("fixture-candidate-policy"), Id("fixture-threshold"), CandidateExecutionLimits.Default,
            new CandidatePopulationContext(null), sources)).Analysis;
        Dictionary<string, CandidateHypothesisContract> hypotheses = candidates.Decisions.ToDictionary(
            item => item.PopulationMemberId.Value,
            item => candidates.Hypotheses.Single(hypothesis => hypothesis.CandidateId ==
                candidates.Candidates.Single(candidate => candidate.DecisionId == item.DecisionId).CandidateId),
            StringComparer.Ordinal);
        FindingEvidenceFactContract[] findingFacts = currentFacts.Select(current =>
        {
            string factId = Text(current, "fact_id");
            return new FindingEvidenceFactContract(
                Id("finding-evidence-" + factId), hypotheses[factId].HypothesisId,
                WorstCredibleConsequence.MeaningfulBoundedLoss, Text(current, "affected_locus_id"), Text(current, "cause"),
                current.GetProperty("applicability").EnumerateArray().Select(item => item.GetString()!).ToArray(),
                [], [], Evidence(current).ToArray());
        }).ToArray();
        SharedCauseProofContract[] causeProofs = currentFacts.Select(current =>
        {
            string factId = Text(current, "fact_id");
            return SingleProof(hypotheses[factId], candidates, Text(current, "cause"),
                Text(current, "affected_locus_id"),
                current.GetProperty("applicability").EnumerateArray().Select(item => item.GetString()!).ToArray(),
                Evidence(current).ToArray());
        }).ToArray();
        FindingCaseInputContract initialInput = Input(
            candidates, findingFacts, causeProofs, [], [], [], [], [], [], [], []);
        FindingCaseContract initial = FindingCasePipeline.Execute(Reidentify(initialInput));
        Dictionary<string, FindingContract> generatedByFact = currentFacts.ToDictionary(
            current => Text(current, "fact_id"),
            current => initial.Findings.Single(item => item.HypothesisId == hypotheses[Text(current, "fact_id")].HypothesisId),
            StringComparer.Ordinal);

        List<PriorFindingContract> priorContracts = [];
        List<ProducerCompatibilityContract> compatibilities = [];
        List<CoveragePopulationFactContract> reconciliationPopulations = [];
        List<CoverageMemberFactContract> reconciliationMembers = [];
        foreach ((string priorId, JsonElement prior) in priors)
        {
            JsonElement[] matchingCurrent = currentFacts.Where(item => item
                .GetProperty("candidate_prior_occurrences").EnumerateArray().Any(value => value.GetString() == priorId)).ToArray();
            if (matchingCurrent.Length == 0)
            {
                continue;
            }
            JsonElement currentValue = matchingCurrent[0];
            string currentFactId = Text(currentValue, "fact_id");
            FindingContract generated = generatedByFact[currentFactId];
            IdentityEnvelopeContract priorIdentity = PriorIdentity(generated.IdentityEnvelope, prior, currentValue);
            bool proofAvailable = currentValue.GetProperty("causal_proof_refs").GetArrayLength() > 0;
            Sha256Fingerprint semantic = StringComparer.Ordinal.Equals(Text(prior, "conclusion"), Text(currentValue, "conclusion"))
                ? generated.SemanticFingerprint : new Sha256Fingerprint(new string('b', 64));
            priorContracts.Add(new PriorFindingContract(
                Id(priorId), Id(Text(prior, "logical_finding_id")), Id("fixture-prior-run"),
                Id("fixture-prior-candidate-" + priorId), Id("fixture-prior-hypothesis-" + priorId),
                priorIdentity, semantic, proofAvailable, []));
            string currentVersion = Text(currentValue, "producer_version");
            JsonElement currentProducer = producerFacts[currentVersion];
            bool compatible = currentProducer.TryGetProperty("declared_compatible_predecessors", out JsonElement declared)
                && declared.EnumerateArray().Any(item => item.GetString() == Text(prior, "producer_version"));
            ProducerCompatibilityContract compatibility = new(
                Id("producer-compatibility-" + priorId + "-" + currentFactId), priorIdentity.AnalyzerFamily,
                priorIdentity.SemanticContractVersion, priorIdentity.IdentityContractVersion,
                generated.IdentityEnvelope.AnalyzerFamily, generated.IdentityEnvelope.SemanticContractVersion,
                generated.IdentityEnvelope.IdentityContractVersion, compatible,
                [Evidence(currentValue).First()])
            {
                PriorAnalyzerVersion = priorIdentity.AnalyzerVersion,
                CurrentAnalyzerVersion = generated.IdentityEnvelope.AnalyzerVersion,
            };
            if (!compatibilities.Any(item =>
                    item.PriorAnalyzerFamily == compatibility.PriorAnalyzerFamily
                    && item.PriorAnalyzerVersion == compatibility.PriorAnalyzerVersion
                    && item.PriorSemanticContractVersion == compatibility.PriorSemanticContractVersion
                    && item.PriorIdentityContractVersion == compatibility.PriorIdentityContractVersion
                    && item.CurrentAnalyzerFamily == compatibility.CurrentAnalyzerFamily
                    && item.CurrentAnalyzerVersion == compatibility.CurrentAnalyzerVersion
                    && item.CurrentSemanticContractVersion == compatibility.CurrentSemanticContractVersion
                    && item.CurrentIdentityContractVersion == compatibility.CurrentIdentityContractVersion))
            {
                compatibilities.Add(compatibility);
            }
        }
        foreach (JsonElement absence in factual.GetProperty("absence_facts").EnumerateArray())
        {
            string priorId = Text(absence, "prior_occurrence_id");
            JsonElement prior = priors[priorId];
            string populationId = "absence-population-" + priorId;
            IdentityEnvelopeContract identity = Identity(
                Text(prior, "producer_family"), ContractVersion.Parse(Text(prior, "producer_version")),
                Text(prior, "cause"), Text(prior, "affected_locus_id"),
                prior.GetProperty("applicability").EnumerateArray().Select(item => item.GetString()!).ToArray(),
                FindingCaseIdentity.SharedCauseDependencyClosureId(
                    prior.GetProperty("dependencies").EnumerateArray().Select(item => Id(item.GetString()!))).Value);
            priorContracts.Add(new PriorFindingContract(
                Id(priorId), Id(Text(prior, "logical_finding_id")), Id("fixture-prior-run"),
                Id("fixture-prior-candidate-" + priorId), Id("fixture-prior-hypothesis-" + priorId),
                identity, new Sha256Fingerprint(new string('a', 64)), true, [populationId]));
            OpaqueId analyzer = Id("absence-analyzer-" + priorId);
            reconciliationPopulations.Add(new CoveragePopulationFactContract(
                Id("absence-population-fact-" + priorId), analyzer, populationId, "applicable analysis members")
            {
                EvidenceIds = absence.GetProperty("proof_refs").EnumerateArray()
                    .Select(item => Id(item.GetString()!)).ToArray(),
            });
            bool completed = Text(absence, "applicable_analysis_status") == "completed";
            reconciliationMembers.Add(new CoverageMemberFactContract(
                Id("absence-member-fact-" + priorId), analyzer, populationId, "applicable analysis members",
                Id("absence-member-" + priorId), completed ? CoverageMemberState.Completed : CoverageMemberState.SkippedByLimit,
                completed ? "completed" : "skipped by limit", completed ? "none" : "configured limit",
                null, [], completed ? null : Id("absence-gap-" + priorId)));
        }
        RelatedFindingFactContract[] relatedFindingFacts = currentFacts
            .Where(current => current.TryGetProperty("related_condition_fact", out _))
            .Select(current => new RelatedFindingFactContract(
                Id("related-finding-fact-" + Text(current, "fact_id")), hypotheses[Text(current, "fact_id")].HypothesisId,
                Id(current.GetProperty("candidate_prior_occurrences")[0].GetString()!), Evidence(current).ToArray(),
                Text(current, "related_condition_fact")))
            .ToArray();
        ReconciliationCandidateFactContract[] reconciliationCandidates = currentFacts.Select(current =>
            new ReconciliationCandidateFactContract(
                Id("reconciliation-candidates-" + Text(current, "fact_id")),
                hypotheses[Text(current, "fact_id")].HypothesisId,
                current.GetProperty("candidate_prior_occurrences").EnumerateArray()
                    .Select(item => Id(item.GetString()!)).ToArray())).ToArray();
        FindingCaseInputContract reconciledInput = Reidentify(initialInput with
        {
            ReconciliationPolicyId = Id(Text(policy, "policy_id")),
            ReconciliationPolicyVersion = ContractVersion.Parse(Text(policy, "policy_version")),
            ReconciliationActorId = Id(Text(policy, "mechanism_id")),
            PriorFindings = priorContracts,
            ProducerCompatibilities = compatibilities,
            RelatedFindingFacts = relatedFindingFacts,
            ReconciliationCandidateFacts = reconciliationCandidates,
            CoveragePopulationFacts = reconciliationPopulations,
            CoverageMemberFacts = reconciliationMembers,
        });
        FindingCaseContract reconciled = FindingCasePipeline.Execute(reconciledInput);
        Dictionary<string, OpaqueId> occurrenceByFact = currentFacts.ToDictionary(
            current => Text(current, "fact_id"),
            current => reconciled.Findings.Single(item =>
                item.HypothesisId == hypotheses[Text(current, "fact_id")].HypothesisId).FindingOccurrenceId,
            StringComparer.Ordinal);
        List<ReconciliationObservation> observations = currentFacts.Select(current =>
        {
            string currentFactId = Text(current, "fact_id");
            OpaqueId occurrence = occurrenceByFact[currentFactId];
            Slice5ReconciliationContract assessment = reconciled.ReconciliationAssessments.Single(item =>
                item.SubjectKind == "finding" && item.CurrentOccurrenceId == occurrence);
            return new ReconciliationObservation(
                currentFactId, current.GetProperty("candidate_prior_occurrences").EnumerateArray()
                    .Select(item => item.GetString()!).Order(StringComparer.Ordinal).ToArray(),
                assessment.Outcome, assessment.Gates, assessment.PolicyVersion, assessment.ActorId,
                assessment.VisibleByDefault, assessment.ProofEvidenceIds.Select(item => item.Value).ToArray(),
                assessment.ConsideredOccurrenceIds.Select(item => item == occurrence ? currentFactId : item.Value).ToArray(),
                assessment.Gaps);
        }).ToList();
        observations.AddRange(reconciled.ReconciliationAssessments
            .Where(item => item.SubjectKind == "finding" && item.CurrentOccurrenceId is null)
            .Select(item => new ReconciliationObservation(
                string.Empty, [item.PriorOccurrenceId!.Value], item.Outcome, item.Gates, item.PolicyVersion,
                item.ActorId, item.VisibleByDefault, item.ProofEvidenceIds.Select(id => id.Value).ToArray(),
                item.ConsideredOccurrenceIds.Select(id => id.Value).ToArray(), item.Gaps)));
        return new ReconciliationExecution(reconciled, observations, occurrenceByFact);
    }

    public static LeadPromotionExecution ExecuteLeadPromotion(JsonElement factual)
    {
        JsonElement priorFact = factual.GetProperty("prior_run_fact");
        JsonElement currentFact = factual.GetProperty("current_hypothesis");
        const string analyzer = "generic-reconciliation-analyzer";
        ContractVersion version = new(1, 0, 2);

        (CandidateAnalysisContract Candidates, FindingEvidenceFactContract Fact, SharedCauseProofContract Proof) Build(
            JsonElement fact, string runId, bool lead)
        {
            string hypothesisId = Text(fact, "hypothesis_id");
            OpaqueId[] evidence = fact.GetProperty("supporting_evidence_refs").EnumerateArray()
                .Select(item => Id(item.GetString()!)).ToArray();
            string[] missing = fact.GetProperty("missing_information").EnumerateArray()
                .Select(item => item.GetString()!).ToArray();
            string dependency = fact.GetProperty("dependency_members")[0].GetString()!;
            string locus = Text(fact, "affected_locus_id");
            CausalJoinPopulationMember member = new(
                Id(hypothesisId), Id(analyzer), CandidateLane.MandatoryEvidence,
                [new CandidateParticipantContract(Id(dependency), "required-dependency"),
                    new CandidateParticipantContract(Id(locus), "affected-locus")],
                Text(fact, "causal_condition_id"), [Id(dependency), Id(locus)], [Id(dependency)], evidence, [], missing,
                lead ? CausalJoinInputState.Ambiguous : CausalJoinInputState.Complete,
                "Typed lead-promotion hypothesis.", "bounded functional consequence")
            {
                SourceFactId = Id("source-" + runId),
            };
            CandidateAnalysisContract candidates = Candidates(runId, analyzer, [member], version);
            CandidateHypothesisContract hypothesis = candidates.Hypotheses.Single();
            FindingEvidenceFactContract findingFact = new(
                Id("finding-evidence-" + runId), hypothesis.HypothesisId,
                WorstCredibleConsequence.MeaningfulBoundedLoss, locus, Text(fact, "causal_condition_id"),
                fact.GetProperty("applicability_predicates").EnumerateArray()
                    .Select(item => item.GetString()!).ToArray(), [], [], evidence);
            return (candidates, findingFact, SingleProof(hypothesis, candidates, findingFact.CausalCondition,
                findingFact.AffectedLocus, findingFact.ApplicabilityPredicates, evidence));
        }

        (CandidateAnalysisContract Candidates, FindingEvidenceFactContract Fact, SharedCauseProofContract Proof) priorRun =
            Build(priorFact, Text(priorFact, "run_fact_id"), lead: true);
        FindingCaseContract prior = FindingCasePipeline.Execute(Reidentify(Input(
            priorRun.Candidates, [priorRun.Fact], [priorRun.Proof], [], [], [], [], [], [], [], [])));
        Slice5CaseContract priorLead = prior.Cases.Single(item => item.Kind == CaseOccurrenceKind.LeadOnly);
        PriorCaseContract priorCase = new(
            priorLead.CaseOccurrenceId, priorLead.LogicalCaseId, priorLead.OriginatingRunId, priorLead.Kind,
            priorLead.FindingOccurrenceIds, priorLead.HypothesisIds, priorLead.IdentityEnvelope,
            priorLead.SemanticFingerprint, true, []);

        (CandidateAnalysisContract Candidates, FindingEvidenceFactContract Fact, SharedCauseProofContract Proof) currentRun =
            Build(currentFact, "current-run-promo", lead: false);
        FindingCaseContract current = FindingCasePipeline.Execute(Reidentify(Input(
            currentRun.Candidates, [currentRun.Fact], [currentRun.Proof], [], [], [], [], [], [], [priorCase], [])));
        return new LeadPromotionExecution(prior, current);
    }

    public static CaseReconciliationExecution ExecuteCaseReconciliation(JsonElement factual)
    {
        JsonElement caseFacts = factual.GetProperty("case_reconciliation_facts");
        JsonElement priorRun = caseFacts.GetProperty("prior_run");
        JsonElement currentRun = caseFacts.GetProperty("continuing_run");
        JsonElement lookalikeRun = caseFacts.GetProperty("lookalike_run");
        FindingCaseContract prior = FindingCasePipeline.Execute(Reidentify(CaseInput(priorRun, [], [], [], [], [], null)));
        Dictionary<string, OpaqueId> priorOccurrenceByCondition = priorRun.GetProperty("condition_facts").EnumerateArray()
            .ToDictionary(condition => Text(condition, "condition_fact_id"), condition =>
            {
                string locus = Text(condition, "affected_locus_id");
                return prior.Findings.Single(finding => finding.IdentityEnvelope.AffectedLocus == locus)
                    .FindingOccurrenceId;
            }, StringComparer.Ordinal);
        PriorFindingContract[] priorFindings = prior.Findings.Select(item => new PriorFindingContract(
            item.FindingOccurrenceId, item.LogicalFindingId, item.OriginatingRunId, item.CandidateId, item.HypothesisId,
            item.IdentityEnvelope, item.SemanticFingerprint, true, ["case-applicable-analysis"])).ToArray();
        PriorCaseContract[] priorCases = prior.Cases.Select(item => new PriorCaseContract(
            item.CaseOccurrenceId, item.LogicalCaseId, item.OriginatingRunId, item.Kind,
            item.FindingOccurrenceIds, item.HypothesisIds, item.IdentityEnvelope, item.SemanticFingerprint,
            true, ["case-applicable-analysis"])).ToArray();
        OpaqueId coverageAnalyzer = Id("case-coverage-analyzer");
        CoveragePopulationFactContract[] populations =
        [
            new(Id("case-coverage-population"), coverageAnalyzer,
                "case-applicable-analysis", "applicable case members")
            {
                EvidenceIds = [Id("case-coverage-proof")],
            },
        ];
        CoverageMemberFactContract[] coverageMembers =
        [
            new(Id("case-coverage-member"), coverageAnalyzer, "case-applicable-analysis",
                "applicable case members", Id("case-analysis-member"), CoverageMemberState.Completed,
                "completed", "none", null, []),
        ];
        ProducerCompatibilityContract[] compatibility =
        [
            Compatibility(prior.Findings[0].IdentityEnvelope, currentRun, "case-finding-producer-compatibility"),
        ];
        FindingCaseContract current = FindingCasePipeline.Execute(Reidentify(CaseInput(
            currentRun, priorFindings, priorCases, compatibility, populations, coverageMembers,
            priorOccurrenceByCondition)));
        FindingCaseContract lookalike = FindingCasePipeline.Execute(Reidentify(CaseInput(
            lookalikeRun, priorFindings, priorCases, compatibility, populations, coverageMembers,
            priorOccurrenceByCondition)));
        return new CaseReconciliationExecution(prior, current, lookalike);
    }

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
