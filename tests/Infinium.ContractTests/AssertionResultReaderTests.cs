using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class PublicAssertionResultReaderTests
{
    private static readonly string[] ContractOnlyMessages =
    [
        "Contract-only fixture emits no semantic result.",
    ];

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void AssertionReaderPreservesTypedStatusesAndExactExecutionIdentity()
    {
        string path = Path.Combine(Path.GetTempPath(), $"infinium-assertion-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(
                    new
                    {
                        schema_id = ContractConstants.EvaluationAssertionSchemaId,
                        schema_version = "1",
                        assertion_result_id = "assertion-result-1",
                        assertion_id = "analyzer-declaration-required-fields",
                        evaluation_id = "EVAL-0065",
                        specification_revision = "infinium.eval.m1.semantic-and-ground-truth/1",
                        fixture_id = "ANALYZER-CONTRACT-DEV",
                        fixture_version = "1.0.0",
                        partition = "development",
                        implementation_commit = new string('a', 40),
                        dirty_worktree = false,
                        run_id = "run-1",
                        run_output_fingerprint = new string('b', 64),
                        oracle_fingerprint = new string('c', 64),
                        assertion_type = "presence",
                        actual_schema_id = ContractConstants.RunOutputSchemaId,
                        oracle_schema_id = "infinium.evaluation.fixture-oracle/v1",
                        state_model_version = "1.0.0",
                        canonical_comparison_fingerprint = new string('d', 64),
                        status = "not-applicable",
                        actual_references = Array.Empty<string>(),
                        oracle_entry_references = Array.Empty<string>(),
                        messages = ContractOnlyMessages,
                        evaluated_at = DateTimeOffset.UnixEpoch.ToString("O"),
                    }));

            EvaluationAssertionResult result = PublicAssertionResultReader.Read(path);

            Assert.AreEqual("analyzer-declaration-required-fields", result.AssertionId);
            Assert.AreEqual(AssertionStatus.NotApplicable, result.Status);
            Assert.IsFalse(result.DirtyWorktree);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Fault")]
    public void AssertionReaderRefusesUnknownStatus()
    {
        string path = Path.Combine(Path.GetTempPath(), $"infinium-assertion-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                path,
                $$"""
                  {
                    "schema_id": "{{ContractConstants.EvaluationAssertionSchemaId}}",
                    "schema_version": "1",
                    "assertion_result_id": "assertion-result-1",
                    "assertion_id": "unknown-status",
                    "evaluation_id": "EVAL-0065",
                    "specification_revision": "1",
                    "fixture_id": "fixture",
                    "fixture_version": "1.0.0",
                    "partition": "development",
                    "implementation_commit": "{{new string('a', 40)}}",
                    "dirty_worktree": false,
                    "run_id": "run-1",
                    "run_output_fingerprint": "{{new string('b', 64)}}",
                    "oracle_fingerprint": "{{new string('c', 64)}}",
                    "assertion_type": "presence",
                    "actual_schema_id": "{{ContractConstants.RunOutputSchemaId}}",
                    "oracle_schema_id": "infinium.evaluation.fixture-oracle/v1",
                    "state_model_version": "1.0.0",
                    "canonical_comparison_fingerprint": "{{new string('d', 64)}}",
                    "status": "success-ish",
                    "actual_references": [],
                    "oracle_entry_references": [],
                    "messages": ["Unknown must not become success."],
                    "evaluated_at": "1970-01-01T00:00:00.0000000+00:00"
                  }
                  """);

            Assert.ThrowsExactly<InvalidDataException>(() => PublicAssertionResultReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Fault")]
    [TestProperty("Category", "M1Evaluation")]
    public void AssertionReaderRefusesPassingEvidenceFromDirtyWorktree()
    {
        string path = Path.Combine(Path.GetTempPath(), $"infinium-assertion-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, CreateAssertionJson(dirtyWorktree: true, status: "passed"));

            Assert.ThrowsExactly<InvalidDataException>(() => PublicAssertionResultReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void PassingAssertionsRequireEvidenceReferencesByAssertionType()
    {
        string path = Path.Combine(Path.GetTempPath(), $"infinium-assertion-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(
                path,
                CreateAssertionJson(
                    dirtyWorktree: false,
                    status: "passed",
                    assertionType: "presence",
                    actualReferences: [],
                    oracleReferences: ["oracle-1"]));
            Assert.ThrowsExactly<InvalidDataException>(() => PublicAssertionResultReader.Read(path));

            File.WriteAllText(
                path,
                CreateAssertionJson(
                    dirtyWorktree: false,
                    status: "passed",
                    assertionType: "absence",
                    actualReferences: [],
                    oracleReferences: ["oracle-1"]));
            EvaluationAssertionResult accepted = PublicAssertionResultReader.Read(path);
            Assert.AreEqual(AssertionStatus.Passed, accepted.Status);

            File.WriteAllText(
                path,
                CreateAssertionJson(
                    dirtyWorktree: false,
                    status: "passed",
                    assertionType: "absence",
                    actualReferences: [],
                    oracleReferences: []));
            Assert.ThrowsExactly<InvalidDataException>(() => PublicAssertionResultReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void AssertionReaderRejectsDuplicateKeysAndNonCanonicalTime()
    {
        string path = Path.Combine(Path.GetTempPath(), $"infinium-assertion-{Guid.NewGuid():N}.json");
        try
        {
            string duplicate = CreateAssertionJson(false, "failed").Replace(
                "\"run_id\":\"run-1\"",
                "\"run_id\":\"run-1\",\"run_id\":\"run-2\"",
                StringComparison.Ordinal);
            File.WriteAllText(path, duplicate);
            Assert.ThrowsExactly<InvalidDataException>(() => PublicAssertionResultReader.Read(path));

            File.WriteAllText(
                path,
                CreateAssertionJson(
                    false,
                    "failed",
                    evaluatedAt: "1970-01-01T01:00:00.0000000+01:00"));
            Assert.ThrowsExactly<InvalidDataException>(() => PublicAssertionResultReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void AssertionReaderRejectsDocumentBeyondBound()
    {
        string path = Path.Combine(Path.GetTempPath(), $"infinium-assertion-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, new string('x', (4 * 1024 * 1024) + 1));
            Assert.ThrowsExactly<InvalidDataException>(() => PublicAssertionResultReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateAssertionJson(
        bool dirtyWorktree,
        string status,
        string assertionType = "presence",
        string[]? actualReferences = null,
        string[]? oracleReferences = null,
        string? evaluatedAt = null)
    {
        return JsonSerializer.Serialize(
            new
            {
                schema_id = ContractConstants.EvaluationAssertionSchemaId,
                schema_version = "1",
                assertion_result_id = "assertion-result-1",
                assertion_id = "dirty-worktree-cannot-pass",
                evaluation_id = "EVAL-0065",
                specification_revision = "infinium.eval.m1.semantic-and-ground-truth/1",
                fixture_id = "ANALYZER-CONTRACT-DEV",
                fixture_version = "1.0.0",
                partition = "development",
                implementation_commit = new string('a', 40),
                dirty_worktree = dirtyWorktree,
                run_id = "run-1",
                run_output_fingerprint = new string('b', 64),
                oracle_fingerprint = new string('c', 64),
                assertion_type = assertionType,
                actual_schema_id = ContractConstants.RunOutputSchemaId,
                oracle_schema_id = "infinium.evaluation.fixture-oracle/v1",
                state_model_version = "1.0.0",
                canonical_comparison_fingerprint = new string('d', 64),
                status,
                actual_references = actualReferences ?? [],
                oracle_entry_references = oracleReferences ?? [],
                messages = ContractOnlyMessages,
                evaluated_at = evaluatedAt ?? DateTimeOffset.UnixEpoch.ToString("O"),
            });
    }
}
