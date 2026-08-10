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
    public void CaseReconciliationExecutesMemberFirstDecisionEightContinuityAndRejectsLookalikeMerge()
    {
        using JsonDocument truth = LoadTruth(out _);
        JsonElement package = truth.RootElement.GetProperty("packages").GetProperty("reconciliation_lineage");
        JsonElement factual = package.GetProperty("answer_free_factual_inputs");
        JsonElement oracle = package.GetProperty("expected_typed_output").GetProperty("case_reconciliation");
        AssertNoAnswerKeys(factual);
        CaseReconciliationExecution execution = FindingCaseFixtureProductAdapter.ExecuteCaseReconciliation(factual);
        AnalysisCaseContract priorCase = execution.Prior.Cases.Single(item => item.Kind == CaseOccurrenceKind.Supported);
        AnalysisCaseContract currentCase = execution.Current.Cases.Single(item => item.Kind == CaseOccurrenceKind.Supported);
        OccurrenceReconciliationContract continuity = execution.Current.ReconciliationAssessments.Single(item =>
            item.SubjectKind == "case" && item.CurrentOccurrenceId == currentCase.CaseOccurrenceId);
        Assert.AreEqual("exact-continuation", Kebab(continuity.Outcome), string.Join(" | ",
            execution.Current.ReconciliationAssessments.Select(item =>
                $"{item.SubjectKind}:{item.Outcome}:{item.PriorOccurrenceId?.Value}->{item.CurrentOccurrenceId?.Value}:{string.Join(',', item.Gaps)}")));
        Assert.AreEqual(priorCase.LogicalCaseId, currentCase.LogicalCaseId);
        Assert.AreNotEqual(0, priorCase.FindingOccurrenceIds.Count);
        Assert.IsFalse(priorCase.FindingOccurrenceIds.SequenceEqual(currentCase.FindingOccurrenceIds));
        OpaqueId[] memberAssessmentIds = execution.Current.ReconciliationAssessments
            .Where(item => item.SubjectKind == "finding").Select(item => item.AssessmentId).ToArray();
        Assert.IsTrue(memberAssessmentIds.All(continuity.ProofEvidenceIds.Contains));
        Assert.AreEqual(3, oracle.GetProperty("exact_counts").GetProperty("case_occurrences").GetInt32());

        OccurrenceReconciliationContract[] actualMembers = execution.Current.ReconciliationAssessments
            .Where(item => item.SubjectKind == "finding")
            .Concat(execution.Lookalike.ReconciliationAssessments.Where(item => item.SubjectKind == "finding"))
            .ToArray();
        JsonElement[] oracleMembers = oracle.GetProperty("member_finding_assessments").EnumerateArray().ToArray();
        Assert.AreEqual(oracle.GetProperty("exact_counts").GetProperty("member_finding_assessments").GetInt32(),
            actualMembers.Length);
        CollectionAssert.AreEquivalent(oracleMembers.Select(item => item.GetProperty("outcome").GetString()).ToArray(),
            actualMembers.Select(item => Kebab(item.Outcome)).ToArray());
        string[] expectedMemberGates = oracleMembers.Select(CanonicalOracleReconciliation).Order(StringComparer.Ordinal).ToArray();
        string[] actualMemberGates = actualMembers.Select(CanonicalReconciliation).Order(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(expectedMemberGates, actualMemberGates,
            string.Join(Environment.NewLine, expectedMemberGates.Select(item => "EXPECTED " + item)
                .Concat(actualMemberGates.Select(item => "ACTUAL " + item))));

        AnalysisCaseContract lookalikeCase = execution.Lookalike.Cases.Single(item => item.Kind == CaseOccurrenceKind.Supported);
        OccurrenceReconciliationContract rejected = execution.Lookalike.ReconciliationAssessments.Single(item =>
            item.SubjectKind == "case" && item.CurrentOccurrenceId == lookalikeCase.CaseOccurrenceId);
        Assert.AreEqual(ReconciliationOutcome.NewDistinct, rejected.Outcome,
            $"Gates={rejected.Gates}; gaps={string.Join(',', rejected.Gaps)}; considered={string.Join(',', rejected.ConsideredOccurrenceIds)}");
        Assert.IsNull(rejected.PriorOccurrenceId);
        Assert.AreNotEqual(priorCase.LogicalCaseId, lookalikeCase.LogicalCaseId);
        OccurrenceReconciliationContract[] actualCases = [continuity, rejected];
        JsonElement[] oracleCases = oracle.GetProperty("case_assessments").EnumerateArray().ToArray();
        Assert.AreEqual(oracle.GetProperty("exact_counts").GetProperty("case_reconciliation_assessments").GetInt32(),
            actualCases.Length);
        CollectionAssert.AreEqual(oracleCases.Select(CanonicalOracleReconciliation).Order(StringComparer.Ordinal).ToArray(),
            actualCases.Select(CanonicalReconciliation).Order(StringComparer.Ordinal).ToArray());
        Assert.AreEqual(priorCase.IdentityEnvelope.CausalCondition, currentCase.IdentityEnvelope.CausalCondition);
        Assert.AreNotEqual(priorCase.IdentityEnvelope.CausalCondition, lookalikeCase.IdentityEnvelope.CausalCondition);
        Assert.AreEqual("no-safety-claim", execution.Current.PublicationClaimBoundary);
    }

}
