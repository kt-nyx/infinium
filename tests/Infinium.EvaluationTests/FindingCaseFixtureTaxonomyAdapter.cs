using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.Conclusions;
using Infinium.Analysis.FindingCases;
using Infinium.Application.FindingCases;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

internal static partial class FindingCaseFixtureProductAdapter
{
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

}
