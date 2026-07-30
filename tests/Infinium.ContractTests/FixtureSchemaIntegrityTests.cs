using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Infinium.Application.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class FixtureSchemaIntegrityTests
{
    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void ExecutionReaderRejectsRecursiveDuplicateProperties()
    {
        using FixturePackageTestBuilder fixture = new();
        string path = fixture.FilePath(FixturePackageReader.ExecutionInputFileName);
        string json = File.ReadAllText(path);
        json = json.Replace(
            "\"state\": \"empty\",",
            "\"state\": \"empty\",\n    \"state\": \"empty\",",
            StringComparison.Ordinal);
        File.WriteAllText(path, json);

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadExecutionInput(path));
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    public void ExecutionReaderRejectsDocumentBeyondBound()
    {
        using FixturePackageTestBuilder fixture = new();
        string path = fixture.FilePath(FixturePackageReader.ExecutionInputFileName);
        using (FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.SetLength((16 * 1024 * 1024) + 1);
        }

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadExecutionInput(path));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Fault")]
    public void HarnessRejectsUnknownSupportingDocumentFieldsAndRedistributionDrift()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddProvenanceProperty("unversioned_extension", new JsonObject());
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.SetRedistributionClass("non-redistributable");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Security")]
    public void HarnessBindsRetainedInputAndOracleArtifactsTransitively()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.AddRetainedInputArtifact(
            "inputs/project-authored.esp",
            [0x54, 0x45, 0x53, 0x34]);
        fixture.AddRetainedOracleArtifact(
            "oracle/independent-review-evidence.json",
            "{}"u8.ToArray());

        EvaluationHarnessFixturePackage package =
            FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath);

        Assert.AreEqual("fixture-development-1", package.FixtureId.Value);
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Security")]
    [TestProperty("Category", "M1Fault")]
    public void HarnessRejectsRetainedArtifactMutationMissingFilesAndTraversal()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputArtifact(
                "inputs/project-authored.esp",
                [0x54, 0x45, 0x53, 0x34]);
            fixture.MutateRetainedArtifact(
                "inputs/project-authored.esp",
                [0x54, 0x45, 0x53, 0x35]);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputReference(
                "inputs/missing.esp",
                new string('1', 64));

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddRetainedInputReference(
                "inputs/../escaped.esp",
                new string('1', 64));

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Security")]
    [TestProperty("Category", "M1Fault")]
    public void HarnessRejectsHardLinkedRetainedArtifactsOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using FixturePackageTestBuilder fixture = new();
        fixture.AddRetainedInputArtifact(
            "inputs/project-authored.esp",
            [0x54, 0x45, 0x53, 0x34]);
        string retained = Path.Combine(
            fixture.DirectoryPath,
            "inputs",
            "project-authored.esp");
        string source = retained + ".source";
        File.Move(retained, source);
        Assert.IsTrue(CreateHardLinkW(retained, source, 0));

        Assert.ThrowsExactly<InvalidDataException>(
            () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void OracleRejectsWrongCollectionTypeUnknownMethodsAndDuplicateIds()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddOracleExpectedItem("expected_findings", "expected-1", "observation");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddOracleExpectedItem(
                "expected_findings",
                "expected-1",
                "finding",
                "missing-ground-truth-method");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddOracleExpectedItem("expected_observations", "duplicate-expected-id", "observation");
            fixture.AddOracleExpectedItem("expected_findings", "duplicate-expected-id", "finding");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddDuplicateGroundTruthMethod();
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void OracleRequiresFailureAndCollectionStateExpectations()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.RemoveOracleProperty("expected_failures");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.RemoveOracleProperty("expected_collection_states");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void OracleCollectionStatesMustMatchExpectedItemCounts()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.SetExpectedCollectionState("observations", "populated");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddOracleExpectedItem("expected_observations", "expected-1", "observation");
            fixture.SetExpectedCollectionState("observations", "empty");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    public void CoverageExpectationDoesNotLaunderCoverageGapCollectionState()
    {
        using FixturePackageTestBuilder fixture = new();
        fixture.AddOracleExpectedItem(
            "expected_coverage_and_gaps",
            "expected-coverage-1",
            "coverage");

        EvaluationHarnessFixturePackage package =
            FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath);

        Assert.AreEqual(
            "empty",
            package.Oracle.GetProperty("expected_collection_states")
                .GetProperty("coverage_gaps")
                .GetProperty("state")
                .GetString());
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void OracleTaxonomyAssignmentsRequireUniqueIdsAndExpectedSubjects()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddTaxonomyAssignment("assignment-1", "missing-expected-item");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            fixture.AddOracleExpectedItem("expected_observations", "expected-1", "observation");
            fixture.AddTaxonomyAssignment("assignment-1", "expected-1");
            fixture.AddTaxonomyAssignment("assignment-1", "expected-1");
            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Security")]
    public void PartitionHistoryForbidsKnownAnswerLaundering()
    {
        foreach ((string from, string to) in new[]
        {
            ("development", "validation"),
            ("development", "held-out"),
            ("validation", "held-out"),
            ("validation", "validation"),
        })
        {
            using FixturePackageTestBuilder fixture = new();
            JsonArray history =
            [
                FixturePackageTestBuilder.PartitionTransition(
                    null,
                    from,
                    DateTimeOffset.UnixEpoch,
                    "initial registration",
                    false),
                FixturePackageTestBuilder.PartitionTransition(
                    from,
                    to,
                    DateTimeOffset.UnixEpoch.AddSeconds(1),
                    "forbidden relabel",
                    false),
            ];
            fixture.SetPartitionHistory(to, history);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath),
                $"{from} -> {to}");
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Fault")]
    public void KnownAnswerTransitionRequiresChronologyAndIndependentFingerprints()
    {
        using (FixturePackageTestBuilder fixture = new())
        {
            JsonArray history =
            [
                FixturePackageTestBuilder.PartitionTransition(
                    null,
                    "validation",
                    DateTimeOffset.UnixEpoch.AddSeconds(2),
                    "initial registration",
                    false),
                FixturePackageTestBuilder.PartitionTransition(
                    "validation",
                    "development",
                    DateTimeOffset.UnixEpoch.AddSeconds(1),
                    "implementation influence",
                    true,
                    includeReplacement: true),
            ];
            fixture.SetPartitionHistory("development", history);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }

        using (FixturePackageTestBuilder fixture = new())
        {
            using JsonDocument manifest = JsonDocument.Parse(
                File.ReadAllBytes(fixture.FilePath(FixturePackageReader.PublicManifestFileName)));
            string currentInput = manifest.RootElement.GetProperty("input_package_fingerprint").GetString()!;
            JsonArray history =
            [
                FixturePackageTestBuilder.PartitionTransition(
                    null,
                    "held-out",
                    DateTimeOffset.UnixEpoch,
                    "initial registration",
                    false),
                FixturePackageTestBuilder.PartitionTransition(
                    "held-out",
                    "development",
                    DateTimeOffset.UnixEpoch.AddSeconds(1),
                    "implementation influence",
                    true,
                    includeReplacement: true,
                    replacementInputFingerprint: currentInput),
            ];
            fixture.SetPartitionHistory("development", history);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath));
        }
    }

    [TestMethod]
    [TestCategory("M1Evaluation")]
    [TestCategory("M1Contract")]
    public void KnownAnswerTransitionAcceptsCompleteIndependentReplacementMetadata()
    {
        using FixturePackageTestBuilder fixture = new();
        JsonArray history =
        [
            FixturePackageTestBuilder.PartitionTransition(
                null,
                "held-out",
                DateTimeOffset.UnixEpoch,
                "initial registration",
                false),
            FixturePackageTestBuilder.PartitionTransition(
                "held-out",
                "development",
                DateTimeOffset.UnixEpoch.AddSeconds(1),
                "implementation influence",
                true,
                includeReplacement: true),
        ];
        fixture.SetPartitionHistory("development", history);

        EvaluationHarnessFixturePackage package =
            FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath);

        Assert.AreEqual(FixturePartition.Development, package.Partition);
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Fault")]
    public void SchemasRequireCanonicalUtcTimestampText()
    {
        foreach (string timestamp in new[]
        {
            "1970-01-01T01:00:00.0000000+01:00",
            "1970-01-01T00:00:00Z",
        })
        {
            using FixturePackageTestBuilder fixture = new();
            fixture.SetPublicString("created_at", timestamp);

            Assert.ThrowsExactly<InvalidDataException>(
                () => FixturePackageReader.ReadForEvaluationHarness(fixture.DirectoryPath),
                timestamp);
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    public void CliSummaryRequiresDurationAndSeparatedCostUsage()
    {
        JsonObject summary = CreateCliSummary();
        using (JsonDocument document = JsonDocument.Parse(summary.ToJsonString()))
        {
            EmbeddedJsonSchemaValidator.Validate(
                document.RootElement,
                "cli-summary.v1.schema.json");
        }

        summary.Remove("duration_ms");
        using (JsonDocument document = JsonDocument.Parse(summary.ToJsonString()))
        {
            Assert.ThrowsExactly<InvalidDataException>(
                () => EmbeddedJsonSchemaValidator.Validate(
                    document.RootElement,
                    "cli-summary.v1.schema.json"));
        }

        summary = CreateCliSummary();
        summary["cost"]!.AsObject()["unresolved_hold"] = true;
        summary["cost"]!.AsObject()["calculated_actual_nano_usd"] = null;
        using (JsonDocument document = JsonDocument.Parse(summary.ToJsonString()))
        {
            EmbeddedJsonSchemaValidator.Validate(
                document.RootElement,
                "cli-summary.v1.schema.json");
        }

        summary = CreateCliSummary();
        summary["cost"]!.AsObject()["reserved_nano_usd"] = -1;
        using (JsonDocument document = JsonDocument.Parse(summary.ToJsonString()))
        {
            Assert.ThrowsExactly<InvalidDataException>(
                () => EmbeddedJsonSchemaValidator.Validate(
                    document.RootElement,
                    "cli-summary.v1.schema.json"));
        }

        summary = CreateCliSummary();
        summary["cost"]!.AsObject()["calculated_actual_nano_usd"] = null;
        using (JsonDocument document = JsonDocument.Parse(summary.ToJsonString()))
        {
            Assert.ThrowsExactly<InvalidDataException>(
                () => EmbeddedJsonSchemaValidator.Validate(
                    document.RootElement,
                    "cli-summary.v1.schema.json"));
        }
    }

    private static JsonObject CreateCliSummary()
    {
        JsonObject typedCounts = new();
        foreach (string name in new[]
        {
            "observations",
            "deterministic_results",
            "external_claims",
            "application_links",
            "discovery_leads",
            "model_proposals",
            "proposal_admissions",
            "candidates",
            "hypotheses",
            "findings",
            "recommendations",
            "supported_cases",
            "lead_only_cases",
            "abstentions",
            "invalid_inputs",
            "coverage_gaps",
            "failures",
        })
        {
            typedCounts[name] = 0;
        }

        JsonObject coverageCounts = new();
        foreach (string name in new[]
        {
            "completed",
            "completed_with_gaps",
            "failed",
            "skipped_by_configuration",
            "skipped_by_limit",
            "unsupported",
        })
        {
            coverageCounts[name] = 0;
        }

        return new JsonObject
        {
            ["schema_id"] = "infinium.cli-summary/v1",
            ["schema_version"] = "1",
            ["run_id"] = "run-1",
            ["outcome"] = "completed",
            ["exit_code"] = 0,
            ["typed_counts"] = typedCounts,
            ["coverage_state_counts"] = coverageCounts,
            ["duration_ms"] = 0,
            ["cost"] = new JsonObject
            {
                ["provider_input_tokens"] = 0,
                ["provider_output_tokens"] = 0,
                ["provider_reasoning_tokens"] = 0,
                ["dispatch_count"] = 0,
                ["tool_call_count"] = 0,
                ["calculated_actual_nano_usd"] = 0,
                ["reserved_nano_usd"] = 0,
                ["unresolved_hold"] = false,
            },
            ["readiness"] = "no-readiness-evaluation",
            ["no_safety_guarantee"] = true,
        };
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(
        string fileName,
        string existingFileName,
        nint securityAttributes);
}
