using System.Text;
using System.Text.Json;
using Infinium.Application.Evaluation;
using Infinium.Application.Provider;
using Infinium.Application.Serialization;
using Infinium.Domain.Contracts;
using Infinium.PublicFixtures;

namespace Infinium.Tests;

[TestClass]
public sealed class CandidateInvestigationProviderProvenanceContractTests
{
    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateInvestigationAnswerFreeInputsValidateAndMinimizeExactly()
    {
        foreach (string package in new[] { "S6-CANDIDATE-DEV-v2", "S6-CANDIDATE-VAL-v3" })
        {
            string directory = PackageDirectory(package);
            Validate(Path.Combine(directory, "execution-input.v1.json"), "candidate-investigation-execution-input.v1.schema.json");
            Validate(Path.Combine(directory, "context-manifest.v1.json"), "candidate-investigation-context.v1.schema.json");
            Validate(Path.Combine(directory, "retained-transcripts.v1.json"), "candidate-investigation-retained-transcripts.v1.schema.json");
            CandidateInvestigationExecutionInput input = JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
                File.ReadAllBytes(Path.Combine(directory, "execution-input.v1.json")), SourceClaimContextMinimizer.JsonOptions)!;
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(directory, "context-manifest.v1.json")),
                CandidateInvestigationContextMinimizer.CreateManifest(input));
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void ProviderProvenanceRetainsPromptCandidateHypothesisEvidenceAndSourceLinks()
    {
        StringAssert.Matches(CandidateInvestigationPromptV1.Fingerprint,
            new System.Text.RegularExpressions.Regex("^[0-9a-f]{64}$"));
        StringAssert.Contains(CandidateInvestigationPromptV1.Instructions, "untrusted data");
        StringAssert.Contains(CandidateInvestigationPromptV1.Instructions, "source-acquisition");
        StringAssert.Contains(CandidateInvestigationPromptV1.Instructions, "Do not create findings");
        (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load("S6-CANDIDATE-DEV-v2");
        CandidateInvestigationScenarioResult scenario = CandidateInvestigationEngine.Execute(input, transcripts).Scenarios[0];
        Assert.AreEqual("hypothesis-d01", scenario.HypothesisId);
        Assert.AreEqual("acquisition-d01", scenario.SourceAcquisitionLinks.Single().SourceAcquisitionId);
        Assert.AreEqual("admission-d01", scenario.SourceAcquisitionLinks.Single().SourceAdmissionId);
        Assert.AreEqual("source-application-d01", scenario.SourceAcquisitionLinks.Single().SourceApplicationLinkId);
        Assert.AreEqual("evidence-application-d01", scenario.SourceAcquisitionLinks.Single().EvidenceApplicationLinkId);
        CollectionAssert.Contains(scenario.RawIntermediateIds.ToList(), "response-candidate-d01");
        byte[] json = CandidateInvestigationTransparencyRenderer.RenderJson(new(
            CandidateInvestigationPromptV1.Id, CandidateInvestigationPromptV1.Fingerprint,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(CandidateInvestigationContextMinimizer.CreateManifest(input))),
            [scenario], false, false, false));
        string text = Encoding.UTF8.GetString(json);
        StringAssert.Contains(text, "source_acquisition_links");
        StringAssert.Contains(text, "finding_authority\":\"not-granted");
        StringAssert.Contains(text, "private_verdict\":\"not-performed");
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateInvestigationOutputStrictCodecRoundTripsEveryTerminalState()
    {
        foreach (string package in new[] { "S6-CANDIDATE-DEV-v2", "S6-CANDIDATE-VAL-v3" })
        {
            (CandidateInvestigationExecutionInput input, CandidateInvestigationRetainedTranscript[] transcripts) = Load(package);
            foreach (CandidateInvestigationScenarioResult scenario in CandidateInvestigationEngine.Execute(input, transcripts).Scenarios)
            {
                byte[] first = ProviderContractJsonCodecs.Serialize(scenario.Investigation);
                byte[] second = ProviderContractJsonCodecs.Serialize(ProviderContractJsonCodecs.DeserializeCandidateInvestigation(first));
                CollectionAssert.AreEqual(first, second);
            }
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateInvestigationFrozenOracleComparesBothPartitionsExactly()
    {
        foreach (string package in new[] { "S6-CANDIDATE-DEV-v2", "S6-CANDIDATE-VAL-v3" })
        {
            using CandidateInvestigationFixturePackage fixture = CandidateInvestigationFixtureReader.Read(PackageDirectory(package));
            CandidateInvestigationResult actual = CandidateInvestigationEngine.Execute(fixture.ExecutionInput, fixture.Transcripts);
            CandidateInvestigationOracleVerifier.Verify(fixture, actual);
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateAnswerIsolationRejectsRecursiveAuthorityAndDispositionCuesInIdentifierVariants()
    {
        foreach (string json in new[]
                 {
                     "{\"outer\":[{\"ground-truth\":\"x\"}]}",
                     "{\"context_id\":\"matched-negative\"}",
                     "{\"candidate_id\":\"positive_control\"}",
                     "{\"evidence_ids\":[\"no model\"]}",
                     "{\"package_identity\":\"unavailable-provider\"}",
                 })
        {
            using JsonDocument mutation = JsonDocument.Parse(json);
            Assert.ThrowsExactly<InvalidDataException>(
                () => CandidateInvestigationFixtureReader.AssertAnswerFreeProductInput(mutation.RootElement));
        }
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateFrozenOracleRejectsEveryMaterialScenarioMutation()
    {
        using CandidateInvestigationFixturePackage fixture =
            CandidateInvestigationFixtureReader.Read(PackageDirectory("S6-CANDIDATE-DEV-v2"));
        CandidateInvestigationResult baseline = CandidateInvestigationEngine.Execute(fixture.ExecutionInput, fixture.Transcripts);
        CandidateInvestigationScenarioResult scenario = baseline.Scenarios[0];
        HypothesisProposalContract proposal = scenario.Investigation.HypothesisProposals.Single();
        ProviderSemanticAdmissionLinkContract link = scenario.Investigation.AdmissionLinks.Single();
        foreach (CandidateInvestigationScenarioResult mutation in new[]
                 {
                     scenario with { ResponseRecordId = "mutated-response" },
                     scenario with { ResponseFingerprint = new string('0', 64) },
                     scenario with { ModelUsed = false },
                     scenario with { ProviderUsed = false },
                     scenario with { AuditOnly = true },
                     scenario with { ForbiddenAuthorityDetected = true },
                     scenario with { ReplayState = "audit-only" },
                     scenario with { Investigation = scenario.Investigation with
                         { HypothesisProposals = [proposal with { SupportingEvidenceIds = [] }] } },
                     scenario with { Investigation = scenario.Investigation with
                         { HypothesisProposals = [proposal with { ContradictingEvidenceIds = [new("evidence-d01")] }] } },
                     scenario with { Investigation = scenario.Investigation with { AdmissionLinks = [] } },
                     scenario with { Investigation = scenario.Investigation with
                         { AdmissionLinks = [link with { DecisionState = SemanticDecisionState.Rejected }] } },
                 })
        {
            CandidateInvestigationResult actual = baseline with
            {
                Scenarios = baseline.Scenarios.Select(item => item.TranscriptId == scenario.TranscriptId ? mutation : item).ToArray(),
            };
            Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(fixture, actual));
        }

        CandidateInvestigationScenarioResult conditional = baseline.Scenarios.Single(item =>
            item.Disposition == "accepted-conditional");
        HypothesisProposalContract conditionalProposal = conditional.Investigation.HypothesisProposals.Single();
        CandidateInvestigationResult missingInformationMutation = baseline with
        {
            Scenarios = baseline.Scenarios.Select(item => item.TranscriptId == conditional.TranscriptId
                ? conditional with
                {
                    Investigation = conditional.Investigation with
                    { HypothesisProposals = [conditionalProposal with { MissingInformation = ["mutated"] }] }
                }
                : item).ToArray(),
        };
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CandidateInvestigationOracleVerifier.Verify(fixture, missingInformationMutation));
    }

    [TestMethod]
    [TestCategory("Contract")]
    public void CandidateTypedValidationOracleRejectsEveryMaterialFieldFamily()
    {
        using CandidateInvestigationFixturePackage fixture =
            CandidateInvestigationFixtureReader.Read(PackageDirectory("S6-CANDIDATE-VAL-v3"));
        CandidateInvestigationResult baseline = CandidateInvestigationEngine.Execute(fixture.ExecutionInput, fixture.Transcripts);
        CandidateInvestigationScenarioResult scenario = baseline.Scenarios[0];
        CandidateInvestigationDocument document = scenario.Investigation;
        HypothesisProposalContract proposal = document.HypothesisProposals.Single();
        ProviderSemanticAdmissionLinkContract link = document.AdmissionLinks.Single();
        CandidateSourceAcquisitionLink source = scenario.SourceAcquisitionLinks.Single();

        CandidateInvestigationScenarioResult[] mutations =
        [
            scenario with { TranscriptState = "drift" },
            scenario with { ResponseRecordId = "rr-mutated" },
            scenario with { ResponseFingerprint = new string('0', 64) },
            scenario with { ModelUsed = false },
            scenario with { ProviderUsed = false },
            scenario with { AuditOnly = true },
            scenario with { ForbiddenAuthorityDetected = true },
            scenario with { Disposition = "rejected" },
            scenario with { ReplayState = "audit-only" },
            scenario with { ContextId = "cx-mutated" },
            scenario with { HypothesisId = "hy-mutated" },
            scenario with { RawIntermediateIds = [.. scenario.RawIntermediateIds, "raw-mutated"] },
            scenario with { CanonicalInvestigationSha256 = new string('0', 64) },
            scenario with { AbstentionKinds = ["mutated"] },
            scenario with { GapKinds = ["mutated"] },
            scenario with { AuditReasons = ["mutated"] },
            scenario with { Investigation = document with { SchemaId = "mutated" } },
            scenario with { Investigation = document with { SchemaVersion = "mutated" } },
            scenario with { Investigation = document with { OperationId = new("op-mutated") } },
            scenario with { Investigation = document with { OwnerKind = "mutated" } },
            scenario with { Investigation = document with { OwnerId = new("owner-mutated") } },
            scenario with { Investigation = document with { AnalysisRunId = new("run-mutated") } },
            scenario with { Investigation = document with { CandidateId = new("cd-mutated") } },
            scenario with { Investigation = document with { ParticipantIds = [new("pt-mutated")] } },
            scenario with { Investigation = document with { ParticipantRoles = ["mutated"] } },
            scenario with { Investigation = document with { CausalPathIds = [new("cp-mutated")] } },
            scenario with { Investigation = document with { DependencyClosureId = new("dc-mutated") } },
            scenario with { Investigation = document with { EvidenceIds = [new("ev-mutated")] } },
            scenario with { Investigation = document with { HypothesisProposals = [] } },
            scenario with { Investigation = document with { Abstentions = ["mutated"] } },
            scenario with { Investigation = document with { Gaps = ["mutated"] } },
            scenario with { Investigation = document with { ValidationIds = [new("validation-mutated")] } },
            scenario with { Investigation = document with { AdmissionLinkIds = [new("admission-mutated")] } },
            scenario with { Investigation = document with { AdmissionLinks = [] } },
            scenario with { Investigation = document with { HypothesisProposals = [proposal with { CandidateId = new("cd-mutated") }] } },
            scenario with { Investigation = document with { HypothesisProposals = [proposal with { Hypothesis = "mutated" }] } },
            scenario with { Investigation = document with { HypothesisProposals = [proposal with { SupportingEvidenceIds = [] }] } },
            scenario with { Investigation = document with { HypothesisProposals = [proposal with { ContradictingEvidenceIds = [new("ev-mutated")] }] } },
            scenario with { Investigation = document with { HypothesisProposals = [proposal with { MissingInformation = ["mutated"] }] } },
            scenario with { Investigation = document with { HypothesisProposals = [proposal with { ProposalState = SemanticProposalState.Rejected }] } },
            scenario with { Investigation = document with { HypothesisProposals = [proposal with { Reason = "mutated" }] } },
            scenario with { Investigation = document with { AdmissionLinks = [link with { AdmissionId = new("admission-mutated") }] } },
            scenario with { Investigation = document with { AdmissionLinks = [link with { AuthorizationId = new("auth-mutated") }] } },
            scenario with { Investigation = document with { AdmissionLinks = [link with { RootSubjectId = new("cd-mutated") }] } },
            scenario with { Investigation = document with { AdmissionLinks = [link with { ValidationId = new("validation-mutated") }] } },
            scenario with { Investigation = document with { AdmissionLinks = [link with { ApplicationLinkId = new("ea-mutated") }] } },
            scenario with { SourceAcquisitionLinks = [source with { EvidenceId = "ev-mutated" }] },
            scenario with { SourceAcquisitionLinks = [source with { EvidenceApplicationLinkId = "ea-mutated" }] },
            scenario with { SourceAcquisitionLinks = [source with { SourceAcquisitionId = "aq-mutated" }] },
            scenario with { SourceAcquisitionLinks = [source with { SourceAdmissionId = "sa-mutated" }] },
            scenario with { SourceAcquisitionLinks = [source with { SourceApplicationLinkId = "sl-mutated" }] },
            scenario with { SourceAcquisitionLinks = [source with { SourceRevisionId = "sr-mutated" }] },
            scenario with { SourceAcquisitionLinks = [source with { PassageId = "ps-mutated" }] },
            scenario with { SourceAcquisitionLinks = [source with { Relationship = "neutral" }] },
            scenario with { SourceAcquisitionLinks = [source with { Availability = "deleted" }] },
            scenario with { SourceAcquisitionLinks = [source with { ContentSha256 = new string('0', 64) }] },
        ];
        foreach (CandidateInvestigationScenarioResult mutation in mutations)
        {
            AssertRejected(fixture, baseline, scenario.TranscriptId, mutation);
        }

        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(
            fixture, baseline with { ContextManifestSha256 = new string('0', 64) }));
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(
            fixture, baseline with { PromptId = "prompt-mutated" }));
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(
            fixture, baseline with { PromptFingerprint = new string('0', 64) }));
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(
            fixture, baseline with { Scenarios = baseline.Scenarios.Reverse().ToArray() }));
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(
            fixture, baseline with { Scenarios = baseline.Scenarios.Take(baseline.Scenarios.Count - 1).ToArray() }));
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(
            fixture, baseline with { NetworkUsed = true }));
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(
            fixture, baseline with { CredentialUsed = true }));
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(
            fixture, baseline with { SourceRefreshUsed = true }));

        CandidateInvestigationOracle oracle = fixture.Oracle!;
        CandidateInvestigationOracleIdentity identity = oracle.ExpectedIdentity;
        foreach (CandidateInvestigationOracle mutation in new[]
                 {
                     oracle with { SchemaId = "mutated" },
                     oracle with { PackageId = "mutated" },
                     oracle with { Partition = "development" },
                     oracle with { ExpectedIdentity = identity with { OperationId = "op-mutated" } },
                     oracle with { ExpectedIdentity = identity with { HostAuthorizationId = "auth-mutated" } },
                     oracle with { ExpectedIdentity = identity with { AnalysisRunId = "run-mutated" } },
                     oracle with { ExpectedIdentity = identity with { PromptId = "prompt-mutated" } },
                     oracle with { ExpectedIdentity = identity with { PromptFingerprint = new string('0', 64) } },
                     oracle with { ExpectedContextManifestSha256 = new string('0', 64) },
                     oracle with { Scenarios = oracle.Scenarios.Reverse().ToArray() },
                 })
        {
            AssertOracleRejected(fixture, baseline, mutation);
        }

        CandidateInvestigationOracleAggregate aggregate = oracle.AggregateExpectations;
        foreach (CandidateInvestigationOracleAggregate mutation in new[]
                 {
                     aggregate with { ScenarioCount = 99 },
                     aggregate with { ProposalCount = 99 },
                     aggregate with { AdmittedProposalCount = 99 },
                     aggregate with { RejectedProposalCount = 99 },
                     aggregate with { AdmissionLinkCount = 99 },
                     aggregate with { ModelUsedScenarioCount = 99 },
                     aggregate with { NoModelScenarioCount = 99 },
                     aggregate with { UnavailableProviderScenarioCount = 99 },
                     aggregate with { DistinctOperationIdCount = 99 },
                     aggregate with { PositiveAndMatchedNegativeShareOperation = false },
                     aggregate with { RetainedResponseScenarioCount = 99 },
                     aggregate with { AuditOnlyScenarioCount = 99 },
                     aggregate with { FailedIdentityDriftScenarioCount = 99 },
                     aggregate with { ForbiddenAuthorityScenarioCount = 99 },
                     aggregate with { NetworkSendCount = 1 },
                     aggregate with { CredentialOperationCount = 1 },
                     aggregate with { SourceRefreshCount = 1 },
                     aggregate with { ScenarioTranscriptIds = aggregate.ScenarioTranscriptIds.Reverse().ToArray() },
                     aggregate with
                     {
                         ScenarioCanonicalInvestigationSha256 =
                             aggregate.ScenarioCanonicalInvestigationSha256.Reverse().ToArray(),
                     },
                 })
        {
            AssertOracleRejected(fixture, baseline, oracle with { AggregateExpectations = mutation });
        }

        CandidateInvestigationOracleBoundaries boundaries = oracle.FrozenBoundaries;
        foreach (CandidateInvestigationOracleBoundaries mutation in new[]
                 {
                     boundaries with { OracleFrozenBeforeProductComparison = false },
                     boundaries with { AnswerIsolated = false },
                     boundaries with { Partition = "development" },
                     boundaries with { ProductOutputUsed = true },
                     boundaries with { ProductImplementationUsed = true },
                     boundaries with { PriorOracleBytesInspected = true },
                     boundaries with { PriorValidationBytesPreserved = false },
                     boundaries with { ReplacementHistory = [] },
                     boundaries with { ReplacementHistory = ["mutated"] },
                 })
        {
            AssertOracleRejected(fixture, baseline, oracle with { FrozenBoundaries = mutation });
        }
        AssertOracleRejected(fixture, baseline, oracle with { ForbiddenClaims = [] });
    }

    private static void AssertRejected(
        CandidateInvestigationFixturePackage fixture,
        CandidateInvestigationResult baseline,
        string transcriptId,
        CandidateInvestigationScenarioResult mutation)
    {
        CandidateInvestigationResult actual = baseline with
        {
            Scenarios = baseline.Scenarios.Select(item => item.TranscriptId == transcriptId ? mutation : item).ToArray(),
        };
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(fixture, actual));
    }

    private static void AssertOracleRejected(
        CandidateInvestigationFixturePackage fixture,
        CandidateInvestigationResult baseline,
        CandidateInvestigationOracle oracle) =>
        Assert.ThrowsExactly<InvalidDataException>(() => CandidateInvestigationOracleVerifier.Verify(
            fixture with { Oracle = oracle }, baseline));

    private static void Validate(string path, string schema)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        ActiveJsonSchemaValidator.Validate(document.RootElement, schema);
    }

    private static (CandidateInvestigationExecutionInput, CandidateInvestigationRetainedTranscript[]) Load(string package)
    {
        string directory = PackageDirectory(package);
        CandidateInvestigationExecutionInput input = JsonSerializer.Deserialize<CandidateInvestigationExecutionInput>(
            File.ReadAllBytes(Path.Combine(directory, "execution-input.v1.json")), SourceClaimContextMinimizer.JsonOptions)!;
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "retained-transcripts.v1.json")));
        return (input, JsonSerializer.Deserialize<CandidateInvestigationRetainedTranscript[]>(
            document.RootElement.GetProperty("transcripts"), SourceClaimContextMinimizer.JsonOptions)!);
    }

    private static string PackageDirectory(string package) => Path.Combine(TestRepository.Root,
        "fixtures", "public", "provider", "candidate-investigations", package);
}
