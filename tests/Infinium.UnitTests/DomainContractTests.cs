using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class DomainContractTests
{
    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void ContractPrimitivesRejectAmbiguousOrUnversionedValues()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _ = new OpaqueId(" "));
        Assert.ThrowsExactly<FormatException>(() => ContractVersion.Parse("1"));
        Assert.ThrowsExactly<ArgumentException>(() => _ = new Sha256Fingerprint("abc"));
        Assert.ThrowsExactly<ArgumentException>(
            () => _ = new UtcTimestamp(new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.FromHours(-4))));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Sha256FingerprintNormalizesCaseWithoutChangingIdentity()
    {
        Sha256Fingerprint upper = new(new string('A', 64));
        Sha256Fingerprint lower = new(new string('a', 64));

        Assert.AreEqual(lower, upper);
        Assert.AreEqual(new string('a', 64), upper.Value);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void TaxonomyAssignmentRequiresCodeOnlyForAssignedState()
    {
        TaxonomyAssignmentContract invalid = new(
            new OpaqueId("assignment-1"),
            ContractConstants.TaxonomyId,
            ContractVersion.Parse(ContractConstants.TaxonomyVersion),
            "technical-surface",
            "surface",
            "surface.plugin-data",
            TaxonomyApplicability.Unsupported,
            new OpaqueId("subject-1"),
            "candidate",
            ClassificationRole.Observed,
            [],
            [],
            null,
            new OpaqueId("analyzer-1"),
            new UtcTimestamp(DateTimeOffset.UnixEpoch),
            "unsupported example",
            null);

        Assert.ThrowsExactly<InvalidOperationException>(() => DomainContractInvariants.Validate(invalid));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void AnalyzerDeclarationRequiresAcceptedTaxonomyAndRawExperimentalOutput()
    {
        AnalyzerDeclarationContract invalid = CreateAnalyzerDeclaration() with
        {
            RawDevelopmentOutput = false,
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => DomainContractInvariants.Validate(invalid));

        AnalyzerDeclarationContract valid = invalid with { RawDevelopmentOutput = true };
        DomainContractInvariants.Validate(valid);
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
            [new AnalyzerDependencyContract("typed-index", new ContractVersion(1, 0, 0), true, CoverageState.Unsupported)],
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

    [TestMethod]
    [TestCategory("M1Fault")]
    [TestProperty("Category", "M1Fault")]
    public void DefaultLifecycleAndCoverageStatesAreExplicitlyUnspecified()
    {
        LifecycleState lifecycle = (LifecycleState)Enum.ToObject(typeof(LifecycleState), 0);
        CoverageState coverage = (CoverageState)Enum.ToObject(typeof(CoverageState), 0);

        Assert.AreEqual(LifecycleState.Unspecified, lifecycle);
        Assert.AreEqual(CoverageState.Unspecified, coverage);
        Assert.AreNotEqual(LifecycleState.Completed, lifecycle);
        Assert.AreNotEqual(CoverageState.Completed, coverage);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void TerminalLifecycleStateCannotTransition()
    {
        LifecycleTransitionContract transition = new(
            new OpaqueId("transition-1"),
            new AnalysisRunOwnerContract(new OpaqueId("run-1")),
            new OpaqueId("job-1"),
            LifecycleTransitionRecordKind.Observed,
            new ContractVersion(1, 0, 0),
            LifecycleState.Cancelled,
            LifecycleState.Running,
            1,
            2,
            1,
            new UtcTimestamp(DateTimeOffset.UnixEpoch),
            "illegal restart");

        Assert.ThrowsExactly<InvalidOperationException>(() => DomainContractInvariants.Validate(transition));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void LeadOnlyCaseCannotContainAFinding()
    {
        CaseOccurrenceContract invalid = new(
            new OpaqueId("case-occurrence-1"),
            new OpaqueId("logical-case-1"),
            1,
            null,
            new OpaqueId("run-1"),
            CaseOccurrenceKind.LeadOnly,
            [new OpaqueId("finding-occurrence-1")],
            [new OpaqueId("hypothesis-1")],
            "unconfirmed shared cause");

        Assert.ThrowsExactly<InvalidOperationException>(() => DomainContractInvariants.Validate(invalid));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void SupportedCaseRequiresAFinding()
    {
        CaseOccurrenceContract invalid = new(
            new OpaqueId("case-occurrence-1"),
            new OpaqueId("logical-case-1"),
            1,
            null,
            new OpaqueId("run-1"),
            CaseOccurrenceKind.Supported,
            [],
            [new OpaqueId("hypothesis-1")],
            "proposed shared cause");

        Assert.ThrowsExactly<InvalidOperationException>(() => DomainContractInvariants.Validate(invalid));
    }

    [TestMethod]
    [TestCategory("M1Contract")]
    [TestProperty("Category", "M1Contract")]
    public void TypedEvidenceContractsAreDistinctRuntimeTypes()
    {
        Type[] types =
        [
            typeof(ObservationContract),
            typeof(DeterministicResultContract),
            typeof(ExternalClaimContract),
            typeof(ExternalClaimApplicationLinkContract),
            typeof(DiscoveryLeadContract),
            typeof(ModelProposalContract),
            typeof(ProposalAdmissionContract),
            typeof(CandidateContract),
            typeof(HypothesisContract),
            typeof(FindingConclusionContract),
            typeof(RecommendationContract),
            typeof(CoverageGapContract),
            typeof(FailureContract),
        ];

        Assert.AreEqual(types.Length, types.Distinct().Count());
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void LlmAdmissionRequiresExplicitOperationAndInvocation()
    {
        LlmInvolvementContract invalid = new(
            LlmInvolvementState.ProposalAdmitted,
            LlmOperation.None,
            null);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => DomainContractInvariants.Validate(invalid));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void ProposalAdmissionRejectsTypeOrInvocationAuthorityDrift()
    {
        RunOutputAggregateContract valid = CreateProposalAdmissionOutput();
        DomainContractInvariants.Validate(valid);

        ProposalAdmissionContract wrongType = valid.ProposalAdmissions[0] with
        {
            AdmittedArtifactType = "finding",
        };
        Assert.ThrowsExactly<InvalidOperationException>(
            () => DomainContractInvariants.Validate(valid with { ProposalAdmissions = [wrongType] }));

        ExternalClaimContract wrongInvocation = valid.ExternalClaims[0] with
        {
            Provenance = valid.ExternalClaims[0].Provenance with
            {
                LlmInvolvement = valid.ExternalClaims[0].Provenance.LlmInvolvement with
                {
                    InvocationId = new OpaqueId("invocation-2"),
                },
            },
        };
        Assert.ThrowsExactly<InvalidOperationException>(
            () => DomainContractInvariants.Validate(valid with { ExternalClaims = [wrongInvocation] }));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestCategory("M1Security")]
    [TestProperty("Category", "M1Unit")]
    [TestProperty("Category", "M1Security")]
    public void RunOutputRejectsUnaccountedAdmissionAndAuthorityLaundering()
    {
        RunOutputAggregateContract valid = CreateProposalAdmissionOutput();

        Assert.ThrowsExactly<InvalidOperationException>(
            () => DomainContractInvariants.Validate(valid with { ProposalAdmissions = [] }));

        ExternalClaimContract laundered = valid.ExternalClaims[0] with
        {
            Authority = EvidenceAuthority.HeuristicOrLlmInference,
        };
        Assert.ThrowsExactly<InvalidOperationException>(
            () => DomainContractInvariants.Validate(valid with { ExternalClaims = [laundered] }));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void RunOutputRejectsNestedAndCollectionStateDrift()
    {
        RunOutputAggregateContract valid = CreateProposalAdmissionOutput();
        AnalyzerDeclarationContract invalidAnalyzer = valid.AnalyzerDeclarations[0] with
        {
            RawDevelopmentOutput = false,
        };
        Assert.ThrowsExactly<InvalidOperationException>(
            () => DomainContractInvariants.Validate(valid with { AnalyzerDeclarations = [invalidAnalyzer] }));

        TypedCollectionStateContract falseEmpty = valid.CollectionStates
            .Single(value => value.CollectionName == "external_claims") with
        {
            State = CollectionProductionState.Empty,
        };
        Assert.ThrowsExactly<InvalidOperationException>(
            () => DomainContractInvariants.Validate(valid with
            {
                CollectionStates =
                [
                    .. valid.CollectionStates.Where(value => value.CollectionName != "external_claims"),
                    falseEmpty,
                ],
            }));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CoverageRejectsImpossibleCountsAndDanglingRunReferences()
    {
        RunOutputAggregateContract valid = CreateProposalAdmissionOutput();
        CoverageContract impossible = new(
            new OpaqueId("coverage-1"),
            valid.RunId,
            new OpaqueId(valid.AnalyzerDeclarations[0].AnalyzerId),
            "eligible-relations",
            "eligible relations",
            1,
            2,
            CoverageState.Completed,
            ContractConstants.TaxonomyId,
            ContractVersion.Parse(ContractConstants.TaxonomyVersion),
            [],
            [],
            [],
            []);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => DomainContractInvariants.Validate(valid with { Coverage = [impossible] }));

        CoverageContract foreignRun = impossible with
        {
            OriginatingRunId = new OpaqueId("run-2"),
            CompletedCount = 1,
        };
        Assert.ThrowsExactly<InvalidOperationException>(
            () => DomainContractInvariants.Validate(valid with { Coverage = [foreignRun] }));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void CliSummaryKeepsDurationUsageCostAndUnresolvedHoldsExplicit()
    {
        TypedOutputCountsContract typedCounts = new(
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        CoverageStateCountsContract coverageCounts = new(0, 0, 0, 0, 0, 0);
        CliSummaryAggregateContract valid = new(
            ContractConstants.CliSummarySchemaId,
            new ContractVersion(1, 0, 0),
            new OpaqueId("run-1"),
            CliOutcome.Completed,
            CliExitCode.Success,
            typedCounts,
            coverageCounts,
            1,
            new CliCostContract(0, 0, 0, 0, 0, 0, 0, false),
            ReadinessScope.None,
            true);
        DomainContractInvariants.Validate(valid);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => DomainContractInvariants.Validate(valid with
            {
                Cost = valid.Cost with
                {
                    CalculatedActualNanoUsd = null,
                    UnresolvedHold = false,
                },
            }));
    }

    private static RunOutputAggregateContract CreateProposalAdmissionOutput()
    {
        ContractVersion version = new(1, 0, 0);
        OpaqueId runId = new("run-1");
        OpaqueId invocationId = new("invocation-1");
        UtcTimestamp createdAt = new(DateTimeOffset.UnixEpoch);
        Sha256Fingerprint fingerprint = new(new string('a', 64));

        ArtifactProvenanceContract Provenance(string revisionId, LlmInvolvementState state) =>
            new(
                new OpaqueId(revisionId),
                null,
                runId,
                new OpaqueId("producer-1"),
                version,
                [],
                [],
                [],
                [],
                fingerprint,
                createdAt,
                new LlmInvolvementContract(
                    state,
                    LlmOperation.CandidateInvestigation,
                    invocationId));

        ExternalClaimContract claim = new(
            new OpaqueId("claim-1"),
            version,
            EvidenceAuthority.AuthoritativeExternal,
            Provenance("claim-revision-1", LlmInvolvementState.ProposalAdmitted),
            runId,
            new OpaqueId("source-revision-1"),
            "passage unavailable",
            [],
            []);
        ModelProposalContract proposal = new(
            new OpaqueId("proposal-1"),
            version,
            Provenance("proposal-revision-1", LlmInvolvementState.ProposalRetained),
            LlmOperation.CandidateInvestigation,
            "external-claim",
            new OpaqueId("raw-response-1"),
            [],
            ProposalValidationState.Validated,
            []);
        ProposalAdmissionContract admission = new(
            new OpaqueId("admission-1"),
            version,
            proposal.ProposalId,
            claim.ClaimId,
            "external-claim",
            new OpaqueId("host-validator-1"),
            runId,
            createdAt);
        string[] collectionNames =
        [
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
        ];

        return new RunOutputAggregateContract(
            runId,
            new OpaqueId("snapshot-1"),
            new OpaqueId("analysis-context-1"),
            new OpaqueId("effective-config-1"),
            new OpaqueId("resolved-input-1"),
            fingerprint,
            [],
            [],
            [claim],
            [],
            [],
            [proposal],
            [admission],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            collectionNames
                .Select(name => new TypedCollectionStateContract(
                    name,
                    name is "external_claims" or "model_proposals" or "proposal_admissions"
                        ? CollectionProductionState.Populated
                        : CollectionProductionState.Empty,
                    "unit test"))
                .ToArray(),
            [],
            [],
            [CreateAnalyzerDeclaration()],
            new ReadinessPlaceholderContract(
                new OpaqueId("readiness-evaluation-1"),
                runId,
                ReadinessScope.None,
                null,
                [],
                createdAt,
                "not evaluated"),
            new ReplayabilityAssessmentContract(ReplayClass.AuditOnly, [], []),
            new AuditabilityAssessmentContract(AuditabilityState.Partial, ["unit-test audit gap"]),
            true,
            []);
    }
}
