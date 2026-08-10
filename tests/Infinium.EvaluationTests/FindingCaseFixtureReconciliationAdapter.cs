using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.Conclusions;
using Infinium.Analysis.FindingCases;
using Infinium.Application.FindingCases;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

internal static partial class FindingCaseFixtureProductAdapter
{
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
            OccurrenceReconciliationContract assessment = reconciled.ReconciliationAssessments.Single(item =>
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

}
