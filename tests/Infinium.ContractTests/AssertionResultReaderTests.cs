using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class AssertionResultReaderTests
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
                        status = "not-applicable",
                        actual_references = Array.Empty<string>(),
                        oracle_entry_references = Array.Empty<string>(),
                        messages = ContractOnlyMessages,
                        evaluated_at = DateTimeOffset.UnixEpoch.ToString("O"),
                    }));

            EvaluationAssertionResult result = AssertionResultReader.Read(path);

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
                    "status": "success-ish",
                    "actual_references": [],
                    "oracle_entry_references": [],
                    "messages": ["Unknown must not become success."],
                    "evaluated_at": "1970-01-01T00:00:00.0000000+00:00"
                  }
                  """);

            Assert.ThrowsExactly<InvalidDataException>(() => AssertionResultReader.Read(path));
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

            Assert.ThrowsExactly<InvalidDataException>(() => AssertionResultReader.Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateAssertionJson(bool dirtyWorktree, string status)
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
                assertion_type = "presence",
                status,
                actual_references = Array.Empty<string>(),
                oracle_entry_references = Array.Empty<string>(),
                messages = ContractOnlyMessages,
                evaluated_at = DateTimeOffset.UnixEpoch.ToString("O"),
            });
    }
}
