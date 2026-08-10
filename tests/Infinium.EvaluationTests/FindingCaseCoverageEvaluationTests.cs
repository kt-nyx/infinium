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
    public void CoveragePresentationClosesEveryPopulationStateWithoutCombinedPercentageOrSafetyClaim()
    {
        using JsonDocument truth = LoadTruth(out _);
        JsonElement package = truth.RootElement.GetProperty("packages").GetProperty("coverage_boundaries");
        JsonElement factual = package.GetProperty("answer_free_factual_inputs");
        JsonElement expected = package.GetProperty("expected_typed_output");
        AssertNoAnswerKeys(factual);
        FindingCaseContract output = FindingCaseFixtureProductAdapter.ExecuteCoverage(factual);
        Assert.IsFalse(expected.GetProperty("presentation_prohibitions").GetProperty("combined_analyzed_percentage").GetBoolean());
        Assert.IsFalse(expected.GetProperty("presentation_prohibitions").GetProperty("combined_safety_percentage").GetBoolean());
        Assert.AreEqual(0, expected.GetProperty("exact_counts").GetProperty("safety_guarantees").GetInt32());
        Assert.AreEqual(expected.GetProperty("exact_counts").GetProperty("coverage_matrix_populations").GetInt32(), output.Coverage.Count);
        foreach (JsonElement oracle in expected.GetProperty("coverage_matrix").EnumerateArray())
        {
            CoverageContract actual = output.Coverage.Single(item => item.PopulationId == oracle.GetProperty("population_id").GetString());
            Assert.AreEqual(oracle.GetProperty("denominator").GetInt64(), actual.Denominator);
            Assert.AreEqual(oracle.GetProperty("completed_count").GetInt64(), actual.CompletedCount);
            Assert.AreEqual(oracle.GetProperty("population_status").GetString(), Kebab(actual.State));
            Dictionary<string, string> expectedMembers = oracle.GetProperty("member_states").EnumerateObject()
                .ToDictionary(item => item.Name, item => item.Value.GetString()!, StringComparer.Ordinal);
            Dictionary<string, string> actualMembers = actual.MemberResults.ToDictionary(
                item => item.MemberId.Value, item => Kebab(item.State), StringComparer.Ordinal);
            CollectionAssert.AreEqual(
                expectedMembers.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}|{item.Value}").ToArray(),
                actualMembers.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}|{item.Value}").ToArray());
            Assert.AreEqual($"{actual.CompletedCount}/{actual.Denominator}",
                expected.GetProperty("coverage_completion_ratios").GetProperty(actual.PopulationId).GetString());
            Assert.AreEqual(ContractConstants.TaxonomyId, actual.TaxonomyId);
            Assert.AreEqual(ContractConstants.TaxonomyVersion, actual.TaxonomyVersion.ToString());
            Assert.IsFalse(string.IsNullOrWhiteSpace(actual.AnalyzerId.Value));
            Assert.IsFalse(string.IsNullOrWhiteSpace(actual.DenominatorLabel));
            CollectionAssert.AreEquivalent(
                oracle.GetProperty("gap_ids").EnumerateArray().Select(item => item.GetString()).ToArray(),
                actual.GapIds.Select(item => item.Value).ToArray());
            CollectionAssert.AreEquivalent(
                oracle.GetProperty("failure_ids").EnumerateArray().Select(item => item.GetString()).ToArray(),
                actual.FailureIds.Select(item => item.Value).ToArray());
            if (oracle.TryGetProperty("exclusions", out JsonElement exclusions))
            {
                string[] expectedExclusions = exclusions.EnumerateObject()
                    .Select(item => $"{item.Name}|{item.Value.GetString()}").Order(StringComparer.Ordinal).ToArray();
                string[] actualExclusions = actual.Exclusions
                    .Select(item => $"{item.MemberId.Value}|{item.Reason}").Order(StringComparer.Ordinal).ToArray();
                CollectionAssert.AreEqual(expectedExclusions, actualExclusions);
            }
        }
        JsonElement[] expectedVariants = expected.GetProperty("boundary_variants").EnumerateArray().ToArray();
        JsonElement[] factualVariants = factual.GetProperty("boundary_variant_facts").EnumerateArray().ToArray();
        Assert.AreEqual(expected.GetProperty("exact_counts").GetProperty("boundary_variants").GetInt32(), expectedVariants.Length);
        foreach (JsonElement expectedVariant in expectedVariants)
        {
            string variantId = expectedVariant.GetProperty("variant_id").GetString()!;
            JsonElement factualVariant = factualVariants.Single(item => item.GetProperty("variant_id").GetString() == variantId);
            FindingCaseContract variant = FindingCaseFixtureProductAdapter.ExecuteCoverageVariant(factualVariant);
            Assert.AreEqual(expectedVariant.GetProperty("finding_count").GetInt32(), variant.Findings.Count, variantId);
            Assert.AreEqual(expectedVariant.GetProperty("supported_case_count").GetInt32(),
                variant.Cases.Count(item => item.Kind == CaseOccurrenceKind.Supported), variantId);
            Assert.AreEqual(expectedVariant.GetProperty("lead_only_case_count").GetInt32(),
                variant.Cases.Count(item => item.Kind == CaseOccurrenceKind.LeadOnly), variantId);
            Assert.AreEqual("no-safety-claim", variant.PublicationClaimBoundary, variantId);
            Assert.IsTrue(variant.Boundaries.All(item => item.State == BoundaryUseState.NotUsed), variantId);
            foreach (JsonElement expectedPopulation in expectedVariant.GetProperty("population_results").EnumerateArray())
            {
                CoverageContract actual = variant.Coverage.Single(item =>
                    item.PopulationId == expectedPopulation.GetProperty("population_id").GetString());
                Assert.AreEqual(expectedPopulation.GetProperty("denominator").GetInt64(), actual.Denominator, variantId);
                Assert.AreEqual(expectedPopulation.GetProperty("completed_count").GetInt64(), actual.CompletedCount, variantId);
                Assert.AreEqual(expectedPopulation.GetProperty("status").GetString(), Kebab(actual.State), variantId);
                if (expectedPopulation.TryGetProperty("member_states", out JsonElement memberStates))
                {
                    CollectionAssert.AreEqual(memberStates.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal)
                            .Select(item => $"{item.Name}|{item.Value.GetString()}").ToArray(),
                        actual.MemberResults.OrderBy(item => item.MemberId.Value, StringComparer.Ordinal)
                            .Select(item => $"{item.MemberId.Value}|{Kebab(item.State)}").ToArray(), variantId);
                }
                if (expectedPopulation.TryGetProperty("gap_ids", out JsonElement gapIds))
                {
                    CollectionAssert.AreEquivalent(gapIds.EnumerateArray().Select(item => item.GetString()).ToArray(),
                        actual.GapIds.Select(item => item.Value).ToArray(), variantId);
                }
                if (expectedPopulation.TryGetProperty("failure_ids", out JsonElement failureIds))
                {
                    CollectionAssert.AreEquivalent(failureIds.EnumerateArray().Select(item => item.GetString()).ToArray(),
                        actual.FailureIds.Select(item => item.Value).ToArray(), variantId);
                }
                if (expectedPopulation.TryGetProperty("exclusions", out JsonElement expectedExclusions))
                {
                    CollectionAssert.AreEqual(expectedExclusions.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal)
                            .Select(item => $"{item.Name}|{item.Value.GetString()}").ToArray(),
                        actual.Exclusions.OrderBy(item => item.MemberId.Value, StringComparer.Ordinal)
                            .Select(item => $"{item.MemberId.Value}|{item.Reason}").ToArray(), variantId);
                }
            }
        }
        Assert.AreEqual("no-safety-claim", output.PublicationClaimBoundary);
    }

}
