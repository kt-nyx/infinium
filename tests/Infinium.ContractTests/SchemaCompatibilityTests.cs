using System.Text.Json;
using System.Text.RegularExpressions;
using Infinium.Application.Evaluation;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed partial class SchemaCompatibilityTests
{
    private static readonly string[] JsonSchemaFiles =
    [
        "common.v1.schema.json",
        "fixture-public-manifest.v1.schema.json",
        "fixture-execution-input.v1.schema.json",
        "fixture-oracle.v1.schema.json",
        "fixture-provenance.v1.schema.json",
        "fixture-redistribution.v1.schema.json",
        "fixture-partition-history.v1.schema.json",
        "bethesda-byte-oracle.v1.schema.json",
        "bethesda-accepted-order-construction-input.v1.schema.json",
        "bethesda-case-matrix.v1.schema.json",
        "taxonomy-projections.v1.schema.json",
        "taxonomy-subject-bindings.v1.schema.json",
        "replay-dependencies.v1.schema.json",
        "evaluation-assertion-result.v1.schema.json",
        "analyzer-declaration.v1.schema.json",
        "effective-scan-configuration.v1.schema.json",
        "run-output.v1.schema.json",
        "cli-summary.v1.schema.json",
        "diagnostic-trace.v1.schema.json",
    ];

    private static readonly string[] ProtoFiles =
    [
        "infinium/common/v1/common.proto",
        "infinium/domain/v1/identities.proto",
        "infinium/protocol/v1/protocol.proto",
        "infinium/application/v1/application.proto",
        "infinium/worker/v1/worker.proto",
        "infinium/helper/v1/helper.proto",
    ];

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void JsonSchemaSetIsVersionedClosedAndLocallyResolvable()
    {
        string schemaDirectory = TestRepository.PathFromRoot("contracts", "json-schema");
        string[] actualFiles = Directory.GetFiles(schemaDirectory, "*.json")
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        CollectionAssert.AreEquivalent(JsonSchemaFiles, actualFiles);

        foreach (string fileName in JsonSchemaFiles)
        {
            using JsonDocument schema = JsonDocument.Parse(
                File.ReadAllBytes(Path.Combine(schemaDirectory, fileName)));
            JsonElement root = schema.RootElement;
            Assert.AreEqual(
                "https://json-schema.org/draft/2020-12/schema",
                root.GetProperty("$schema").GetString(),
                fileName);
            Assert.AreEqual(
                $"https://schemas.infinium.dev/json-schema/{fileName}",
                root.GetProperty("$id").GetString(),
                fileName);

            if (!StringComparer.Ordinal.Equals(fileName, "common.v1.schema.json"))
            {
                Assert.AreEqual(JsonValueKind.False, root.GetProperty("additionalProperties").ValueKind, fileName);
            }

            foreach (string reference in EnumerateReferences(root))
            {
                Assert.IsFalse(Uri.TryCreate(reference, UriKind.Absolute, out _), $"{fileName}: remote $ref '{reference}'");
                string[] parts = reference.Split('#', 2);
                string targetFile = parts[0].Length == 0 ? fileName : parts[0];
                Assert.IsTrue(File.Exists(Path.Combine(schemaDirectory, targetFile)), $"{fileName}: {reference}");
                if (parts.Length == 2 && parts[1].StartsWith("/$defs/", StringComparison.Ordinal))
                {
                    using JsonDocument target = JsonDocument.Parse(
                        File.ReadAllBytes(Path.Combine(schemaDirectory, targetFile)));
                    string definition = parts[1]["/$defs/".Length..];
                    Assert.IsTrue(target.RootElement.GetProperty("$defs").TryGetProperty(definition, out _), $"{fileName}: {reference}");
                }
            }
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    public void StableJsonContractsRetainTypedCollectionsAndAcceptedConstants()
    {
        using JsonDocument publicManifest = ReadSchema("fixture-public-manifest.v1.schema.json");
        using JsonDocument oracle = ReadSchema("fixture-oracle.v1.schema.json");
        using JsonDocument assertion = ReadSchema("evaluation-assertion-result.v1.schema.json");
        using JsonDocument runOutput = ReadSchema("run-output.v1.schema.json");
        using JsonDocument cli = ReadSchema("cli-summary.v1.schema.json");
        using JsonDocument analyzer = ReadSchema("analyzer-declaration.v1.schema.json");
        using JsonDocument diagnostics = ReadSchema("diagnostic-trace.v1.schema.json");
        using JsonDocument common = ReadSchema("common.v1.schema.json");

        Assert.AreEqual(
            ContractConstants.TaxonomyId,
            publicManifest.RootElement.GetProperty("properties").GetProperty("taxonomy_id").GetProperty("const").GetString());
        AssertRequired(
            oracle.RootElement,
            "ground_truth_methods",
            "expected_observations",
            "expected_deterministic_results",
            "expected_external_claims",
            "expected_application_links",
            "expected_discovery_leads",
            "expected_model_proposals",
            "expected_proposal_admissions",
            "expected_candidates",
            "expected_hypotheses",
            "expected_findings",
            "expected_recommendations",
            "expected_supported_cases",
            "expected_lead_only_cases",
            "expected_abstentions",
            "expected_invalid_inputs",
            "expected_coverage_and_gaps",
            "expected_taxonomy_assignments",
            "expected_replayability");
        AssertRequired(
            runOutput.RootElement,
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
            "analyzer_coverage",
            "readiness",
            "replayability",
            "auditability");
        AssertRequired(
            cli.RootElement,
            "typed_counts",
            "coverage_state_counts",
            "duration_ms",
            "cost",
            "readiness",
            "no_safety_guarantee");
        Assert.AreEqual(
            ContractConstants.RunOutputSchemaId,
            runOutput.RootElement.GetProperty("properties").GetProperty("schema_id").GetProperty("const").GetString());
        Assert.AreEqual(
            ContractConstants.EvaluationAssertionSchemaId,
            assertion.RootElement.GetProperty("properties").GetProperty("schema_id").GetProperty("const").GetString());
        Assert.IsFalse(assertion.RootElement.GetProperty("properties").TryGetProperty("assertions", out _));
        Assert.AreEqual(
            ContractConstants.CliSummarySchemaId,
            cli.RootElement.GetProperty("properties").GetProperty("schema_id").GetProperty("const").GetString());
        AssertRequired(
            analyzer.RootElement,
            "ruleset_version",
            "scope",
            "input_populations",
            "dependencies",
            "thresholds",
            "coverage",
            "operation_requirements",
            "expected_scale_and_cost",
            "resource_bounds",
            "linked_evaluation_cases");
        Assert.AreEqual(
            "PrivateDiagnostic",
            diagnostics.RootElement.GetProperty("properties").GetProperty("sharing_class").GetProperty("const").GetString());
        string[] coverageStates = common.RootElement.GetProperty("$defs")
            .GetProperty("coverageRecord")
            .GetProperty("properties")
            .GetProperty("status")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
        CollectionAssert.DoesNotContain(coverageStates, "empty");
        Dictionary<string, int> exitCodes = ReadCliExitCodes(cli.RootElement);
        Assert.AreEqual((int)CliExitCode.Success, exitCodes["completed"]);
        Assert.AreEqual((int)CliExitCode.Success, exitCodes["completed-with-gaps"]);
        Assert.AreEqual((int)CliExitCode.InvalidInput, exitCodes["invalid-input"]);
        Assert.AreEqual((int)CliExitCode.Unsupported, exitCodes["unsupported"]);
        Assert.AreEqual((int)CliExitCode.Failed, exitCodes["failed"]);
        Assert.AreEqual((int)CliExitCode.Cancelled, exitCodes["cancelled"]);
        Assert.AreEqual((int)CliExitCode.LimitReached, exitCodes["limit-reached"]);
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Security")]
    public void ExecutionInputSchemaCannotCarryAnswerBearingProperties()
    {
        using JsonDocument execution = ReadSchema("fixture-execution-input.v1.schema.json");
        Assert.AreEqual(JsonValueKind.False, execution.RootElement.GetProperty("additionalProperties").ValueKind);

        string[] answerBearingNames =
        [
            "answer",
            "answers",
            "expected_findings",
            "fixture_class",
            "ground_truth",
            "oracle",
            "oracle_fingerprint",
            "oracle_path",
            "positive_negative_boundary_class",
        ];
        HashSet<string> propertyNames = EnumeratePropertyNames(execution.RootElement).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string name in answerBearingNames)
        {
            Assert.IsFalse(propertyNames.Contains(name), name);
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void ProtobufContractsUseVersionedPackagesResolvableImportsAndFailClosedEnums()
    {
        string protoDirectory = TestRepository.PathFromRoot("contracts", "protobuf");
        string[] actualFiles = Directory.GetFiles(protoDirectory, "*.proto", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(protoDirectory, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEquivalent(ProtoFiles, actualFiles);

        foreach (string relativePath in ProtoFiles)
        {
            string text = File.ReadAllText(Path.Combine(protoDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            StringAssert.Contains(text, "syntax = \"proto3\";");
            StringAssert.Matches(text, VersionedPackageRegex());
            StringAssert.Matches(text, VersionedCsharpNamespaceRegex());
            Assert.IsFalse(text.Contains("google.protobuf.Any", StringComparison.Ordinal), relativePath);
            Assert.IsFalse(text.Contains("google.protobuf.Struct", StringComparison.Ordinal), relativePath);

            foreach (Match import in ImportRegex().Matches(text))
            {
                Assert.IsTrue(File.Exists(Path.Combine(protoDirectory, import.Groups[1].Value)), $"{relativePath}: {import.Value}");
            }

            foreach (Match enumMatch in EnumRegex().Matches(text))
            {
                Match zero = ZeroEnumValueRegex().Match(enumMatch.Groups["body"].Value);
                Assert.IsTrue(zero.Success, $"{relativePath}: enum {enumMatch.Groups["name"].Value} has no zero value");
                StringAssert.EndsWith(zero.Groups["value"].Value, "_UNSPECIFIED");
            }
        }
    }

    [TestMethod]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Security")]
    public void CredentialHelperSchemaHasNoRpcServiceOrSecretBearingFields()
    {
        string helper = TestRepository.Read("contracts", "protobuf", "infinium", "helper", "v1", "helper.proto");
        Assert.IsFalse(ServiceRegex().IsMatch(helper));

        string activeText = string.Join(
            Environment.NewLine,
            helper.Split('\n').Where(line => !line.TrimStart().StartsWith("reserved", StringComparison.Ordinal)));
        string[] forbiddenFieldNames =
        [
            "credential_target",
            "provider_secret",
            "secret_bytes",
            "database_path",
            "arbitrary_url",
            "fallback_profile_id",
        ];
        foreach (string fieldName in forbiddenFieldNames)
        {
            Assert.IsFalse(
                Regex.IsMatch(activeText, $@"\b{Regex.Escape(fieldName)}\s*=", RegexOptions.CultureInvariant),
                fieldName);
        }
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void DomainContractsExposeTheStableSchemaBoundaries()
    {
        AssertRecordProperties<AnalyzerDeclarationContract>(
            nameof(AnalyzerDeclarationContract.RulesetVersion),
            nameof(AnalyzerDeclarationContract.Scope),
            nameof(AnalyzerDeclarationContract.InputPopulations),
            nameof(AnalyzerDeclarationContract.Dependencies),
            nameof(AnalyzerDeclarationContract.Thresholds),
            nameof(AnalyzerDeclarationContract.Coverage),
            nameof(AnalyzerDeclarationContract.OperationRequirements),
            nameof(AnalyzerDeclarationContract.ExpectedScaleAndCost),
            nameof(AnalyzerDeclarationContract.ResourceBounds),
            nameof(AnalyzerDeclarationContract.LinkedEvaluationCases));
        AssertRecordProperties<EffectiveScanConfigurationContract>(
            nameof(EffectiveScanConfigurationContract.Analyzers),
            nameof(EffectiveScanConfigurationContract.Sources),
            nameof(EffectiveScanConfigurationContract.Budgets),
            nameof(EffectiveScanConfigurationContract.CachePolicy),
            nameof(EffectiveScanConfigurationContract.Tracing),
            nameof(EffectiveScanConfigurationContract.CandidateBreadth),
            nameof(EffectiveScanConfigurationContract.Thresholds),
            nameof(EffectiveScanConfigurationContract.Provider),
            nameof(EffectiveScanConfigurationContract.Resources),
            nameof(EffectiveScanConfigurationContract.SemanticContextOverrides));
        AssertRecordProperties<RunOutputContract>(
            nameof(RunOutputContract.SchemaId),
            nameof(RunOutputContract.SchemaVersion),
            nameof(RunOutputContract.RunId),
            nameof(RunOutputContract.RunKind),
            nameof(RunOutputContract.RunState),
            nameof(RunOutputContract.InstallationSnapshot),
            nameof(RunOutputContract.AnalysisContext),
            nameof(RunOutputContract.EffectiveScanConfiguration),
            nameof(RunOutputContract.ResolvedInputManifest),
            nameof(RunOutputContract.AnalyzerDeclarations),
            nameof(RunOutputContract.Observations),
            nameof(RunOutputContract.DeterministicResults),
            nameof(RunOutputContract.ExternalClaims),
            nameof(RunOutputContract.ApplicationLinks),
            nameof(RunOutputContract.DiscoveryLeads),
            nameof(RunOutputContract.ModelProposals),
            nameof(RunOutputContract.ProposalAdmissions),
            nameof(RunOutputContract.Candidates),
            nameof(RunOutputContract.Hypotheses),
            nameof(RunOutputContract.Findings),
            nameof(RunOutputContract.Recommendations),
            nameof(RunOutputContract.SupportedCases),
            nameof(RunOutputContract.LeadOnlyCases),
            nameof(RunOutputContract.Abstentions),
            nameof(RunOutputContract.InvalidInputs),
            nameof(RunOutputContract.CoverageGaps),
            nameof(RunOutputContract.Failures),
            nameof(RunOutputContract.CollectionStates),
            nameof(RunOutputContract.TaxonomyAssignments),
            nameof(RunOutputContract.AnalyzerCoverage),
            nameof(RunOutputContract.ExcludedCapabilities),
            nameof(RunOutputContract.Readiness),
            nameof(RunOutputContract.Replayability),
            nameof(RunOutputContract.Auditability),
            nameof(RunOutputContract.CliSummaryFingerprint),
            nameof(RunOutputContract.DiagnosticTraceReferences));
        AssertRecordProperties<CliSummaryDocumentContract>(
            nameof(CliSummaryDocumentContract.SchemaId),
            nameof(CliSummaryDocumentContract.SchemaVersion),
            nameof(CliSummaryDocumentContract.RunId),
            nameof(CliSummaryDocumentContract.Outcome),
            nameof(CliSummaryDocumentContract.ExitCode),
            nameof(CliSummaryDocumentContract.TypedCounts),
            nameof(CliSummaryDocumentContract.CoverageStateCounts),
            nameof(CliSummaryDocumentContract.DurationMs),
            nameof(CliSummaryDocumentContract.Cost),
            nameof(CliSummaryDocumentContract.Readiness),
            nameof(CliSummaryDocumentContract.NoSafetyGuarantee));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestCategory("M1Evaluation")]
    [TestProperty("Category", "M1Contract")]
    [TestProperty("Category", "M1Evaluation")]
    public void AnalyzerDeclarationSurvivesSchemaValidatedJsonRoundTrip()
    {
        AnalyzerDeclarationContract original = CreateAnalyzerDeclaration();

        string json = AnalyzerDeclarationJsonCodec.Serialize(original);
        AnalyzerDeclarationContract roundTripped = AnalyzerDeclarationJsonCodec.Deserialize(json);

        Assert.AreEqual(original.AnalyzerId, roundTripped.AnalyzerId);
        Assert.AreEqual(original.RulesetVersion, roundTripped.RulesetVersion);
        Assert.AreEqual(original.Scope.SupportedExtentFacets[0], roundTripped.Scope.SupportedExtentFacets[0]);
        Assert.AreEqual(original.LinkedEvaluationCases.Gap[0], roundTripped.LinkedEvaluationCases.Gap[0]);
        Assert.AreEqual(json, AnalyzerDeclarationJsonCodec.Serialize(roundTripped));
    }

    private static JsonDocument ReadSchema(string fileName)
    {
        return TestRepository.ReadJson("contracts", "json-schema", fileName);
    }

    private static AnalyzerDeclarationContract CreateAnalyzerDeclaration()
    {
        return new AnalyzerDeclarationContract(
            ContractConstants.AnalyzerDeclarationSchemaId,
            new ContractVersion(1, 0, 0),
            "scope-incongruent-reversion",
            new ContractVersion(1, 0, 0),
            new ContractVersion(1, 0, 0),
            new ContractVersion(1, 0, 0),
            new ContractVersion(1, 0, 0),
            ContractConstants.TaxonomyId,
            ContractVersion.Parse(ContractConstants.TaxonomyVersion),
            new AnalyzerScopeContract(
                ["typed-index"],
                [new ReasonedAnalyzerScopeContract("unsupported-input", "outside bounded scope")],
                ["generic-relation"],
                [new ReasonedAnalyzerScopeContract("unqualified-record-family", "requires typed shape")],
                ["surface.plugin-data"],
                [new ReasonedAnalyzerScopeContract("all-other-taxonomy-regions", "outside bounded scope")],
                ["extent.scope"],
                [new ReasonedAnalyzerScopeContract("extent.runtime", "not established by this analyzer")]),
            [new AnalyzerInputPopulationContract("override-chain", "eligible relations", true)],
            [new AnalyzerDependencyContract(
                "typed-index",
                new ContractVersion(1, 0, 0),
                true,
                CoverageState.Unsupported)],
            SnapshotAssuranceState.SelectivelyContentSealed,
            new AnalyzerThresholdsContract(
                new AnalyzerThresholdContract("candidate", "1", "typed causal join"),
                new AnalyzerThresholdContract("evidence", "1", "specific local evidence"),
                new AnalyzerThresholdContract("abstention", "1", "missing intent"),
                new AnalyzerThresholdContract("finding", "1", "plausible plus declared evidence")),
            ["candidate", "hypothesis", "finding", "coverage-gap"],
            new AnalyzerCoverageDeclarationContract(
                ["eligible-relations"],
                [CoverageState.Completed, CoverageState.CompletedWithGaps, CoverageState.Unsupported],
                "unsupported inputs emit explicit coverage"),
            new AnalyzerOperationRequirementsContract(ExecutionRequirement.LocalOnly, false, false, false),
            new AnalyzerScaleAndCostContract("bounded M1", AnalyzerCostClass.LocalModerate, false),
            new AnalyzerResourceBoundsContract(100, 100, 1_000),
            AnalyzerMaturity.Experimental,
            true,
            false,
            new LinkedEvaluationCasesContract(
                ["EVAL-0001"],
                ["EVAL-0002"],
                ["EVAL-0016"],
                ["EVAL-0017"],
                ["EVAL-0032"],
                ["EVAL-0065"]));
    }

    private static void AssertRecordProperties<T>(params string[] expected)
    {
        string[] actual = typeof(T).GetProperties().Select(property => property.Name).ToArray();
        foreach (string property in expected)
        {
            CollectionAssert.Contains(actual, property);
        }
    }

    private static void AssertRequired(JsonElement schema, params string[] expected)
    {
        string[] required = schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()!).ToArray();
        foreach (string property in expected)
        {
            CollectionAssert.Contains(required, property);
        }
    }

    private static Dictionary<string, int> ReadCliExitCodes(JsonElement schema)
    {
        Dictionary<string, int> mappings = new(StringComparer.Ordinal);
        foreach (JsonElement condition in schema.GetProperty("allOf").EnumerateArray())
        {
            JsonElement outcome = condition.GetProperty("if").GetProperty("properties").GetProperty("outcome");
            int exitCode = condition.GetProperty("then").GetProperty("properties").GetProperty("exit_code").GetProperty("const").GetInt32();
            if (outcome.TryGetProperty("const", out JsonElement singleOutcome))
            {
                mappings.Add(singleOutcome.GetString()!, exitCode);
            }
            else
            {
                foreach (JsonElement value in outcome.GetProperty("enum").EnumerateArray())
                {
                    mappings.Add(value.GetString()!, exitCode);
                }
            }
        }

        return mappings;
    }

    private static IEnumerable<string> EnumerateReferences(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (StringComparer.Ordinal.Equals(property.Name, "$ref"))
                {
                    yield return property.Value.GetString()!;
                }

                foreach (string nested in EnumerateReferences(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach (string nested in EnumerateReferences(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<string> EnumeratePropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (StringComparer.Ordinal.Equals(property.Name, "properties"))
                {
                    foreach (JsonProperty contractProperty in property.Value.EnumerateObject())
                    {
                        yield return contractProperty.Name;
                    }
                }

                foreach (string nested in EnumeratePropertyNames(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach (string nested in EnumeratePropertyNames(item))
                {
                    yield return nested;
                }
            }
        }
    }

    [GeneratedRegex(@"^\s*package\s+infinium\.[a-z.]+\.v1\s*;", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex VersionedPackageRegex();

    [GeneratedRegex(@"^\s*option\s+csharp_namespace\s*=\s*""Infinium\.Contracts\.[A-Za-z.]+\.V1""\s*;", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex VersionedCsharpNamespaceRegex();

    [GeneratedRegex(@"^\s*import\s+""([^""]+)""\s*;", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ImportRegex();

    [GeneratedRegex(@"\benum\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{(?<body>.*?)\}", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex EnumRegex();

    [GeneratedRegex(@"^\s*(?<value>[A-Z][A-Z0-9_]*)\s*=\s*0\s*;", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ZeroEnumValueRegex();

    [GeneratedRegex(@"^\s*service\s+", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ServiceRegex();
}
