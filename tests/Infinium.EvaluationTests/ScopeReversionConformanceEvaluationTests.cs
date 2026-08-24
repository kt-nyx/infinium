using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Infinium.Application.ScopeReversion;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class ScopeReversionConformanceEvaluationTests
{
    private static readonly string[] ExpectedCoveragePopulations =
    [
        "actor-conclusion-taxonomy", "actor-purpose-applicability", "actor-transition",
        "publication-replay",
        "reference-conclusion-taxonomy", "reference-purpose-applicability", "reference-transition",
    ];

    private static readonly JsonSerializerOptions ReceiptJsonOptions = new(ContractJsonSerializer.Options)
    {
        WriteIndented = true,
    };

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Candidates")]
    [TestCategory("Cases")]
    [TestProperty("Category", "ScopeReversion")]
    public void TwoDomainDeveloperConformanceMatrixHasBoundedFindingsTaxonomyAndCases()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionAnalysisContract analysis = ScopeReversionComposition.Execute(fixture.Request).Analysis;
        foreach (string domainPrefix in new[] { "actor", "reference" })
        {
            ScopeReversionDecisionContract positive = analysis.Decisions.Single(item =>
                item.MemberId.Value == domainPrefix + "-positive");
            ScopeReversionDecisionContract negative = analysis.Decisions.Single(item =>
                item.MemberId.Value == domainPrefix + "-negative");
            ScopeReversionDecisionContract ambiguity = analysis.Decisions.Single(item =>
                item.MemberId.Value == domainPrefix + "-ambiguity");
            Assert.AreEqual(ScopeReversionDisposition.SupportedFinding, positive.Disposition);
            Assert.AreEqual(ScopeReversionDisposition.ResolvedNegative, negative.Disposition);
            Assert.AreEqual(ScopeReversionDisposition.Abstained, ambiguity.Disposition);

            ScopeReversionFindingContract finding = analysis.Findings.Single(item => item.MemberId == positive.MemberId);
            Assert.AreEqual(FindingSeverity.Moderate, finding.Severity);
            Assert.AreEqual(AnalysisConfidence.StronglySupported, finding.Confidence);
            ScopeReversionCaseContract cause = analysis.Cases.Single(item => item.FindingId == finding.FindingId);
            Assert.AreEqual(positive.DependencyClosureId, cause.DependencyClosureId);
            Assert.IsTrue(cause.AffectsReadiness);

            Assert.AreEqual(0, analysis.Findings.Count(item => item.MemberId == negative.MemberId));
            ScopeReversionCandidateContract negativeCandidate = analysis.Candidates.Single(item => item.MemberId == negative.MemberId);
            Assert.AreEqual(ScopeCandidateState.ResolvedNegative, negativeCandidate.State);
            Assert.AreEqual(1, analysis.Contradictions.Count(item => item.CandidateId == negativeCandidate.CandidateId));
            Assert.AreEqual(2, analysis.Taxonomy.Count(item => item.MemberId == negative.MemberId
                && item.Applicability == ScopeTaxonomyApplicability.NotApplicable));

            Assert.AreEqual(0, analysis.Findings.Count(item => item.MemberId == ambiguity.MemberId));
            Assert.AreEqual(0, analysis.Cases.Count(item =>
                analysis.Candidates.Any(candidate => candidate.MemberId == ambiguity.MemberId
                    && candidate.CandidateId == item.CandidateId)));
            Assert.IsTrue(analysis.Abstentions.Single(item =>
                analysis.Candidates.Single(candidate => candidate.MemberId == ambiguity.MemberId).CandidateId
                    == item.CandidateId).RequiredInformation.Count > 0);
        }
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Cases")]
    [TestProperty("Category", "ScopeReversion")]
    public void CrossDomainIdentityOrderingAndGroupingRemainCauseBased()
    {
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionAnalysisContract baseline = ScopeReversionComposition.Execute(fixture.Request).Analysis;
        ScopeReversionCompositionRequest reordered = fixture.Request with
        {
            ActorInputs = fixture.Request.ActorInputs.OrderByDescending(item => item.MemberId.Value, StringComparer.Ordinal).ToArray(),
            ReferenceInputs = fixture.Request.ReferenceInputs.OrderByDescending(item => item.MemberId.Value, StringComparer.Ordinal).ToArray(),
        };
        ScopeReversionAnalysisContract transformed = ScopeReversionComposition.Execute(reordered).Analysis;

        CollectionAssert.AreEqual(
            baseline.Decisions.Select(item => item.DecisionId.Value).ToArray(),
            transformed.Decisions.Select(item => item.DecisionId.Value).ToArray());
        CollectionAssert.AreEqual(
            baseline.Cases.Select(item => item.LogicalCaseId.Value).ToArray(),
            transformed.Cases.Select(item => item.LogicalCaseId.Value).ToArray());
        Assert.AreEqual(2, baseline.Cases.Select(item => item.DependencyClosureId).Distinct().Count());
        Assert.AreEqual(2, baseline.Cases.Select(item => item.LogicalCaseId).Distinct().Count());
        Assert.IsFalse(baseline.Cases[0].FindingId == baseline.Cases[1].FindingId);
        Assert.IsTrue(baseline.Decisions.All(item => item.Rationale.Length > 0));
    }

    [TestMethod]
    [TestCategory("Evaluation")]
    [TestCategory("Output")]
    [TestProperty("Category", "ScopeReversion")]
    public void MeasuredDeveloperConformanceReceiptRetainsExactOutputAndBoundaries()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ScopeReversionFixturePackage fixture = ScopeReversionTestSupport.Fixture();
        ScopeReversionPipelineResult result = ScopeReversionComposition.Execute(fixture.Request);
        stopwatch.Stop();

        Assert.AreEqual(fixture.Expectations.Aggregate.Population, result.Analysis.Counts.Population);
        Assert.AreEqual(fixture.Expectations.Aggregate.SupportedFindings, result.Analysis.Counts.SupportedFindings);
        Assert.AreEqual(fixture.Expectations.Aggregate.ResolvedNegative, result.Analysis.Counts.ResolvedNegative);
        Assert.AreEqual(fixture.Expectations.Aggregate.Abstentions, result.Analysis.Counts.Abstentions);
        Assert.AreEqual(fixture.Expectations.Aggregate.Findings, result.Analysis.Counts.Findings);
        Assert.AreEqual(fixture.Expectations.Aggregate.Cases, result.Analysis.Counts.Cases);
        Assert.AreEqual(fixture.Expectations.Aggregate.Recommendations, result.Analysis.Counts.Recommendations);
        Assert.IsTrue(result.Analysis.Boundaries.All(item => item.State == BoundaryUseState.NotUsed));
        CollectionAssert.AreEqual(
            ExpectedCoveragePopulations,
            result.Analysis.Coverage.Select(item => item.PopulationId).ToArray());

        string? receiptRoot = Environment.GetEnvironmentVariable("INFINIUM_SCOPE_REVERSION_RECEIPT_ROOT");
        if (string.IsNullOrWhiteSpace(receiptRoot))
        {
            return;
        }
        if (!Path.IsPathFullyQualified(receiptRoot) || !Directory.Exists(receiptRoot))
        {
            Assert.Fail("The scope-reversion receipt root must be an existing fully qualified directory.");
        }

        string manifestPath = Path.Combine(fixture.PackageDirectory, "conformance-manifest.v1.json");
        string inputPath = Path.Combine(fixture.PackageDirectory, "input.v1.json");
        string expectationsPath = Path.Combine(fixture.PackageDirectory, "expectations.v1.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement manifestRoot = manifest.RootElement;
        string outputPath = Path.Combine(receiptRoot, "scope-reversion-analysis.v1.json");
        File.WriteAllBytes(outputPath, result.CanonicalJson);

        object Evidence(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            return new
            {
                path = Path.GetFileName(path),
                byte_length = bytes.LongLength,
                sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            };
        }

        object receipt = new
        {
            schema_id = "infinium.verification.scope-reversion-conformance/v1",
            schema_version = "1",
            result = "passed",
            evidence_class = manifestRoot.GetProperty("evidence_class").GetString(),
            semantic_oracle = false,
            verdict_authority = false,
            package_identity = manifestRoot.GetProperty("package_identity").GetString(),
            package_version = manifestRoot.GetProperty("package_version").GetString(),
            partition = manifestRoot.GetProperty("partition").GetString(),
            domains = manifestRoot.GetProperty("domains").EnumerateArray().Select(item => item.GetString()).ToArray(),
            fixture_artifacts = new[] { Evidence(manifestPath), Evidence(inputPath), Evidence(expectationsPath) },
            measured_execution_elapsed_milliseconds = stopwatch.ElapsedMilliseconds,
            analyzer = new
            {
                result.Analysis.Analyzer.AnalyzerFamily,
                result.Analysis.Analyzer.AnalyzerId,
                result.Analysis.Analyzer.AnalyzerVersion,
                result.Analysis.Analyzer.SemanticContractVersion,
                result.Analysis.Analyzer.IdentityContractVersion,
                result.Analysis.Analyzer.RulesetVersion,
                result.Analysis.Analyzer.DeclarationFingerprint,
                result.Analysis.Analyzer.Maturity,
                canonical_declaration_byte_length = Encoding.UTF8.GetByteCount(result.Analysis.Analyzer.CanonicalDeclarationJson),
            },
            assignment = new
            {
                result.Assignment.AssignmentId,
                result.Assignment.OriginatingRunId,
                result.Assignment.InputFingerprint,
                configuration_id = result.Assignment.Configuration.ConfigurationId,
                configuration_fingerprint = result.Assignment.Configuration.Fingerprint,
                result.Assignment.Configuration.RegisteredAdapterIds,
                result.Assignment.Configuration.EnabledAdapterIds,
                result.Assignment.Configuration.MaximumMembers,
                result.Assignment.Configuration.MaximumOutputItems,
                result.Assignment.Configuration.MaximumWallTimeMilliseconds,
            },
            output = new
            {
                result.Analysis.PayloadId,
                artifact = Evidence(outputPath),
                result.Analysis.Counts,
                coverage = result.Analysis.Coverage,
                member_outcomes = result.Analysis.Decisions.Select(decision => new
                {
                    decision.MemberId,
                    decision.DecisionId,
                    decision.Disposition,
                    findings = result.Analysis.Findings.Count(item => item.MemberId == decision.MemberId),
                    cases = result.Analysis.Cases.Count(item => result.Analysis.Findings.Any(finding =>
                        finding.MemberId == decision.MemberId && finding.FindingId == item.FindingId)),
                }).ToArray(),
                unsupported_surfaces_and_gaps = result.Analysis.Gaps,
                external_boundaries = result.Analysis.Boundaries,
                result.Analysis.PublicationClaimBoundary,
            },
            developer_expected_outcome_comparison = "passed-non-authoritative-product-conformance",
        };
        byte[] receiptBytes = JsonSerializer.SerializeToUtf8Bytes(receipt, ReceiptJsonOptions);
        File.WriteAllBytes(
            Path.Combine(receiptRoot, "scope-reversion-conformance.json"),
            [.. receiptBytes, (byte)'\n']);
    }
}
