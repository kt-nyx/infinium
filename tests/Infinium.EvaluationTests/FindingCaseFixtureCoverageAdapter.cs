using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.Conclusions;
using Infinium.Analysis.FindingCases;
using Infinium.Application.FindingCases;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

internal static partial class FindingCaseFixtureProductAdapter
{
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

}
