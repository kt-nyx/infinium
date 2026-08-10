using System.Security.Cryptography;
using System.Text.Json;
using Infinium.Analysis.Candidates;
using Infinium.Analysis.FindingCases;
using Infinium.Application.FindingCases;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

public sealed partial class FindingCaseEvaluationTests
{
    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Cases")]
    [TestProperty("Category", "Evaluation")]
    [TestProperty("Category", "Cases")]
    public void TaxonomyHistoryPreservesProductAssignmentAndUsesExplicitNonProductMappingProvenance()
    {
        using JsonDocument truth = LoadTruth(out _);
        JsonElement package = truth.RootElement.GetProperty("packages").GetProperty("taxonomy_history");
        JsonElement factual = package.GetProperty("answer_free_factual_inputs");
        JsonElement expected = package.GetProperty("expected_typed_output");
        AssertNoAnswerKeys(factual);
        TaxonomyExecution execution = FindingCaseFixtureProductAdapter.ExecuteTaxonomy(factual);
        FindingCaseContract value = execution.Output;
        Assert.AreEqual(69, expected.GetProperty("exact_counts").GetProperty("product_assignment_records").GetInt32());
        Assert.AreEqual(0, expected.GetProperty("exact_counts").GetProperty("product_assignment_mutations").GetInt32());
        Assert.AreEqual("0.1.0", expected.GetProperty("non_product_test_taxonomy_projection")
            .GetProperty("product_taxonomy_version_after_projection").GetString());
        TaxonomyAssignmentContract[] product = value.TaxonomyAssignments
            .Where(item => item.TaxonomyId == ContractConstants.TaxonomyId).ToArray();
        string[] expectedProduct = expected.GetProperty("product_assignments_by_subject").EnumerateObject()
            .SelectMany(subject => subject.Value.EnumerateArray().Select(item => TaxonomyKey(
                subject.Name, item.GetProperty("axis").GetString()!, item.GetProperty("facet").GetString()!,
                item.GetProperty("code").ValueKind == JsonValueKind.String ? item.GetProperty("code").GetString() : null,
                item.GetProperty("applicability_state").GetString()!,
                item.GetProperty("classification_role").ValueKind == JsonValueKind.String
                    ? item.GetProperty("classification_role").GetString() : null)))
            .Order(StringComparer.Ordinal).ToArray();
        string[] actualProduct = product.Select(item => TaxonomyKey(
                execution.SubjectByHypothesis[item.SubjectId], item.Axis, item.Facet, item.Code,
                Kebab(item.Applicability), item.Role is null ? null : Kebab(item.Role.Value)))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.AreEqual(69, product.Length, string.Join(Environment.NewLine,
            expectedProduct.Except(actualProduct, StringComparer.Ordinal).Select(item => "MISSING " + item)
                .Concat(actualProduct.Except(expectedProduct, StringComparer.Ordinal).Select(item => "EXTRA " + item))));
        CollectionAssert.AreEqual(expectedProduct, actualProduct);
        Assert.AreEqual(13, product.Count(item => item.Role is null));
        Assert.AreEqual(56, product.Count(item => item.Role is not null));
        Assert.IsTrue(product.All(item => item.TaxonomyVersion.ToString() == ContractConstants.TaxonomyVersion));

        TaxonomyAssignmentContract[] testV1 = value.TaxonomyAssignments.Where(item =>
            item.TaxonomyId == "infinium.test.taxonomy" && item.TaxonomyVersion == new ContractVersion(1, 0, 0)).ToArray();
        TaxonomyAssignmentContract[] testV2 = value.TaxonomyAssignments.Where(item =>
            item.TaxonomyId == "infinium.test.taxonomy" && item.TaxonomyVersion == new ContractVersion(2, 0, 0)).ToArray();
        Assert.AreEqual(3, testV1.Length);
        Assert.AreEqual(3, testV2.Length);
        Assert.AreEqual(4, value.TaxonomyProjections.Count);
        CollectionAssert.AreEquivalent(
            ExpectedTestTaxonomySources,
            testV1.Select(item => item.AssignmentId.Value).ToArray());
        Assert.IsTrue(value.TaxonomyProjections.All(item => item.EvidenceIds.Count > 0));
        JsonElement mappingOracle = expected.GetProperty("non_product_test_taxonomy_projection");
        Dictionary<string, JsonElement> expectedDerived = mappingOracle.GetProperty("derived_assignments")
            .EnumerateArray().ToDictionary(item => item.GetProperty("code").GetString()!, StringComparer.Ordinal);
        Dictionary<string, TaxonomyAssignmentContract> actualDerived = testV2
            .ToDictionary(item => item.Code!, StringComparer.Ordinal);
        CollectionAssert.AreEquivalent(expectedDerived.Keys.ToArray(), actualDerived.Keys.ToArray());
        Dictionary<string, TaxonomyAssignmentContract> sourceById = testV1.ToDictionary(item => item.AssignmentId.Value,
            StringComparer.Ordinal);
        foreach ((string code, JsonElement oracle) in expectedDerived)
        {
            TaxonomyAssignmentContract actual = actualDerived[code];
            Assert.AreEqual(oracle.GetProperty("taxonomy_id").GetString(), actual.TaxonomyId, code);
            Assert.AreEqual(oracle.GetProperty("taxonomy_version").GetString(), actual.TaxonomyVersion.ToString(), code);
            Assert.AreEqual(oracle.GetProperty("axis").GetString(), actual.Axis, code);
            Assert.AreEqual(oracle.GetProperty("facet").GetString(), actual.Facet, code);
            Assert.AreEqual(oracle.GetProperty("applicability_state").GetString(), Kebab(actual.Applicability), code);
            Assert.AreEqual(oracle.GetProperty("classification_role").GetString(), Kebab(actual.Role!.Value), code);
            CollectionAssert.AreEquivalent(oracle.GetProperty("supersedes_assignment_ids").EnumerateArray()
                    .Select(item => item.GetString()).ToArray(),
                actual.SupersedesAssignmentIds.Select(item => item.Value).ToArray(), code);
            CollectionAssert.AreEquivalent(oracle.GetProperty("evidence_refs").EnumerateArray()
                    .Select(item => item.GetString()).ToArray(),
                actual.EvidenceIds.Select(item => item.Value).ToArray(), code);
            CollectionAssert.AreEquivalent(oracle.GetProperty("applicability_condition_refs").EnumerateArray()
                    .Select(item => item.GetString()).ToArray(),
                actual.ApplicabilityConditionIds.Select(item => item.Value).ToArray(), code);
            Assert.IsTrue(actual.SupersedesAssignmentIds.All(item => sourceById.ContainsKey(item.Value)), code);
        }
        string[] expectedEdges = mappingOracle.GetProperty("mapping_edges").EnumerateArray()
            .Select(edge => $"{edge[0].GetString()}|{expectedDerived.Values.Single(item =>
                item.GetProperty("assignment_id").GetString() == edge[1].GetString()).GetProperty("code").GetString()}")
            .Order(StringComparer.Ordinal).ToArray();
        string[] actualEdges = value.TaxonomyProjections.Select(edge =>
                $"{edge.SourceAssignmentId.Value}|{testV2.Single(item => item.AssignmentId == edge.ProjectedAssignmentId).Code}")
            .Order(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(expectedEdges, actualEdges);
        Assert.IsTrue(value.TaxonomyProjections.All(edge =>
            edge.MappingAuthorityId.Value is "test-map-split-motion" or "test-map-merge-delivery"));
        Assert.AreEqual(0, mappingOracle.GetProperty("raw_evidence_mutations").GetArrayLength());
        Assert.AreEqual(0, mappingOracle.GetProperty("product_assignment_mutations").GetArrayLength());
        Assert.IsTrue(expected.GetProperty("forbidden_inferences_absent").EnumerateArray().Any());
        foreach (JsonProperty subjectNegatives in expected.GetProperty("per_subject_negative_absences").EnumerateObject())
        {
            TaxonomyAssignmentContract[] subjectAssignments = value.TaxonomyAssignments.Where(item =>
                execution.SubjectByHypothesis.GetValueOrDefault(item.SubjectId) == subjectNegatives.Name).ToArray();
            foreach (JsonElement negative in subjectNegatives.Value.EnumerateArray())
            {
                string kind = negative.GetProperty("kind").GetString()!;
                if (negative.TryGetProperty("code", out JsonElement codeElement))
                {
                    string code = codeElement.GetString()!;
                    Assert.IsFalse(subjectAssignments.Any(item => item.Code == code
                        && (!negative.TryGetProperty("classification_role", out JsonElement role)
                            || Kebab(item.Role!.Value) == role.GetString())), $"Forbidden taxonomy inference: {subjectNegatives.Name}/{code}");
                }
                if (kind == "product-taxonomy-mutation")
                {
                    Assert.AreEqual(0, subjectAssignments.Count(item => item.TaxonomyId == ContractConstants.TaxonomyId));
                }
                if (kind == "historical-rewrite")
                {
                    Assert.IsTrue(subjectAssignments.All(item => item.TaxonomyVersion.ToString() == ContractConstants.TaxonomyVersion));
                }
            }
        }
        Assert.IsTrue(value.TaxonomyAssignments.All(item => !string.IsNullOrWhiteSpace(item.Reason)
            && !string.IsNullOrWhiteSpace(item.SubjectType)
            && !string.IsNullOrWhiteSpace(item.AnalyzerOrAdjudicatorId.Value)));
        CollectionAssert.AreEqual(
            FindingCaseJsonCodec.Serialize(value),
            FindingCaseJsonCodec.Serialize(FindingCaseJsonCodec.Deserialize(FindingCaseJsonCodec.Serialize(value))));
        Assert.IsNull(typeof(TaxonomyAssignmentContract).GetProperty("Severity"));
    }

}
