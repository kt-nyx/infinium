using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

[TestClass]
public sealed class Slice5StateModelTests
{
    private static readonly Sha256Fingerprint Fingerprint = new(new string('a', 64));

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelRequiresOneDecisionPerPopulationMember()
    {
        CandidateAnalysisContract value = EmptyCandidates() with { PopulationDenominator = 1 };
        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(value));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelRejectsScoreDependentMandatoryAdmission()
    {
        CandidateDecisionContract decision = new(
            Id("decision-1"), Id("member-1"), Id("fact-1"), CandidateLane.MandatoryEvidence,
            CandidateDecisionDisposition.CandidateAdmitted,
            [new(Id("left"), "source"), new(Id("right"), "target")],
            "typed-causal-join", [Id("edge-1")], Id("closure-1"), "mandatory evidence",
            [Id("evidence-1")], false, null);
        CandidateAnalysisContract value = EmptyCandidates() with
        {
            PopulationDenominator = 1,
            Decisions = [decision],
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(value));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelRequiresUniquePopulationDecisionsAndOneCandidatePerAdmission()
    {
        CandidateDecisionContract first = AdmittedDecision("decision-1", "member-1");
        CandidateDecisionContract duplicateMember = AdmittedDecision("decision-2", "member-1");
        CandidateAnalysisContract duplicatedPopulation = EmptyCandidates() with
        {
            PopulationDenominator = 2,
            Decisions = [first, duplicateMember],
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(duplicatedPopulation));

        CandidateAnalysisContract missingCandidate = EmptyCandidates() with
        {
            PopulationDenominator = 1,
            Decisions = [first],
        };
        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(missingCandidate));

        CandidateHypothesisContract hypothesis = new(
            Id("hypothesis-1"), Id("candidate-1"), Slice5ResultState.Present,
            "causal explanation", "bounded analysis only", [Id("evidence-1")], [], [],
            AnalysisConfidence.Plausible, Id("threshold-1"));
        CandidateAnalysisEntryContract candidate = new(
            Id("candidate-1"), first.DecisionId, Slice5ResultState.Present, "causal explanation",
            [Id("evidence-1")], [], [], AnalysisConfidence.Plausible, Id("threshold-1"))
        { HypothesisId = hypothesis.HypothesisId };
        CandidateAnalysisContract valid = missingCandidate with
        {
            Candidates = [candidate],
            Hypotheses = [hypothesis],
            DependencyEdges =
            [
                .. missingCandidate.DependencyEdges,
                Edge("candidate-decision", first.DecisionId, "source-fact", first.SourceFactId, "derived-from"),
                Edge("candidate-decision", first.DecisionId, "dependency-closure", first.DependencyClosureId, "depends-on"),
                Edge("dependency-closure", first.DependencyClosureId, "dependency", Id("dependency-1"), "depends-on"),
                Edge("candidate-decision", first.DecisionId, "evidence", Id("evidence-1"), "derived-from"),
                Edge("candidate", candidate.CandidateId, "candidate-decision", first.DecisionId, "derived-from"),
                Edge("candidate", candidate.CandidateId, "evidence", Id("evidence-1"), "supports"),
                Edge("hypothesis", hypothesis.HypothesisId, "candidate", candidate.CandidateId, "derived-from"),
                Edge("hypothesis", hypothesis.HypothesisId, "evidence", Id("evidence-1"), "supports"),
            ],
        };
        valid = valid with { Counts = CandidateAnalysisCounts.Compute(valid) };
        valid = valid with { PayloadId = CandidateAnalysisIdentity.ComputePayloadId(valid) };
        Slice5ContractInvariants.Validate(valid);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelRejectsSpeculativeFindingAndReadinessAffectingLead()
    {
        FindingContract finding = new(
            Id("finding-1"), Id("logical-finding-1"), Id("run-1"), Id("candidate-1"),
            Id("hypothesis-1"),
            "speculative only", FindingSeverity.Major, AnalysisConfidence.SpeculativeLead,
            [Id("evidence-1")], FindingCaseIdentity.EnvelopeId(Identity()), Identity(),
            FindingCaseIdentity.EnvelopeId(Identity()), Identity(), [],
            FindingCaseIdentity.FindingSemanticFingerprint(
                "speculative only", FindingSeverity.Major, AnalysisConfidence.SpeculativeLead, Identity(), []), null);
        FindingCaseContract speculative = EmptyFindingCases() with { Findings = [finding] };
        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(speculative));

        Slice5CaseContract lead = new(
            Id("case-1"), Id("logical-case-1"), Id("run-1"), CaseOccurrenceKind.LeadOnly,
            [], [Id("candidate-1")], [Id("hypothesis-1")], "cause", [Id("evidence-1")],
            FindingCaseIdentity.EnvelopeId(Identity()), Identity(),
            FindingCaseIdentity.CaseSemanticFingerprint(
                CaseOccurrenceKind.LeadOnly, Identity(), []), null, true);
        FindingCaseContract readinessLead = EmptyFindingCases() with { Cases = [lead] };
        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(readinessLead));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelCompleteReplayCannotHideMissingDependenciesButPreservesSeparateCoverageGaps()
    {
        AnalysisReplayContract invalid = new(
            ContractConstants.AnalysisReplaySchemaId, Version(), Id("replay-1"), Id("run-1"),
            ReplayMode.RetainedDownstreamReplay, ReplayState.CompleteClean, AuditabilityState.Complete,
            [], [], [], [Id("missing-1")], [Id("gap-1")], true, Id("prior-run"));

        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(invalid));

        Slice5ContractInvariants.Validate(invalid with { MissingDependencyIds = [] });
        Assert.AreEqual(
            GapReplayEffect.None,
            FindingCaseContractInvariants.ExpectedCoverageGapShape(
                [CoverageMemberState.CompletedWithGaps]).Replay);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelRequiresPriorIdentityForEveryReplayModeThatUsesPriorState()
    {
        AnalysisReplayContract retainedWithoutPrior = new(
            ContractConstants.AnalysisReplaySchemaId, Version(), Id("replay-1"), Id("run-1"),
            ReplayMode.RetainedDownstreamReplay, ReplayState.Partial, AuditabilityState.Partial,
            [], [], [], [], [], false, null);
        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(retainedWithoutPrior));

        AnalysisExecutionInputContract retainedExecutionWithoutPrior = ExecutionInput(ReplayMode.RetainedDownstreamReplay, null);
        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(retainedExecutionWithoutPrior));

        Slice5ContractInvariants.Validate(ExecutionInput(ReplayMode.RetainedDownstreamReplay, Id("prior-run")));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelRequiresUniqueApplicationsBoundToExistingClaims()
    {
        ClaimApplicationContract application = new(
            Id("application-1"), Id("claim-1"), Id("run-1"), Id("context-1"),
            Id("subject-1"), "installed-entity", Id("closure-1"),
            ClaimApplicabilityState.Applicable, [Id("claim-1")]);
        DocumentationEvidenceContract valid = DocumentationWithApplications([application]);
        Slice5ContractInvariants.Validate(valid);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Slice5ContractInvariants.Validate(DocumentationWithApplications([
                application,
                application with { ConsumingRunId = Id("run-2") },
            ])));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            Slice5ContractInvariants.Validate(DocumentationWithApplications([
                application with { ClaimId = Id("missing-claim") },
            ])));
    }

    private static CandidateAnalysisContract EmptyCandidates()
    {
        string declarationJson = "{}";
        Sha256Fingerprint declarationFingerprint = CandidateAnalysisIdentity.StructuralHash([declarationJson]);
        string[] executionDescriptors = ["execution-binding"];
        string[] policyDescriptors = ["policy-binding"];
        string[] thresholdDescriptors = ["threshold-binding"];
        string[] limitDescriptors = ["limit-binding"];
        CandidateAnalysisContract value = new(
            ContractConstants.CandidateAnalysisSchemaId, Version(), Id("payload-1"), Id("run-1"),
            Id("analyzer-1"), Id("population-1"), 0, [], [], [], [], [])
        {
            PolicyId = Id("policy-1"),
            ThresholdId = Id("threshold-1"),
            LimitId = Id("limit-1"),
            ExecutionInputId = Id("execution-input-1"),
            ExecutionInputDescriptors = executionDescriptors,
            PolicyDescriptors = policyDescriptors,
            ThresholdDescriptors = thresholdDescriptors,
            LimitDescriptors = limitDescriptors,
            ExecutionInputFingerprint = CandidateAnalysisIdentity.StructuralHash(executionDescriptors),
            PolicyFingerprint = CandidateAnalysisIdentity.StructuralHash(policyDescriptors),
            ThresholdFingerprint = CandidateAnalysisIdentity.StructuralHash(thresholdDescriptors),
            LimitFingerprint = CandidateAnalysisIdentity.StructuralHash(limitDescriptors),
            AnalyzerBindings = [new(Id("analyzer-1"), Version(), Version(), Version(), Version(), declarationFingerprint, declarationJson)],
            AnalyzerSetFingerprint = CandidateAnalysisIdentity.StructuralHash(
                [$"analyzer-1:{declarationFingerprint.Value}"]),
        };
        value = value with
        {
            AnalysisRootId = CandidateAnalysisIdentity.StableId(
                "candidate-analysis-root", value.OriginatingRunId.Value, value.PopulationId.Value,
                value.ExecutionInputFingerprint.Value, value.PolicyFingerprint.Value,
                value.ThresholdFingerprint.Value, value.LimitFingerprint.Value, value.AnalyzerSetFingerprint.Value),
        };
        value = value with
        {
            DependencyEdges =
            [
                Edge("candidate-analysis-root", value.AnalysisRootId, "execution-input-binding",
                    CandidateAnalysisIdentity.StableId("candidate-execution-input-binding", value.ExecutionInputId.Value, value.ExecutionInputFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", value.AnalysisRootId, "policy-binding",
                    CandidateAnalysisIdentity.StableId("candidate-policy-binding", value.PolicyId.Value, value.PolicyFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", value.AnalysisRootId, "threshold-binding",
                    CandidateAnalysisIdentity.StableId("candidate-threshold-binding", value.ThresholdId.Value, value.ThresholdFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", value.AnalysisRootId, "limit-binding",
                    CandidateAnalysisIdentity.StableId("candidate-limit-binding", value.LimitId.Value, value.LimitFingerprint.Value), "uses"),
                Edge("candidate-analysis-root", value.AnalysisRootId, "analyzer-declaration-binding",
                    CandidateAnalysisIdentity.StableId("candidate-analyzer-binding", "analyzer-1", Version().ToString(),
                        Version().ToString(), Version().ToString(), Version().ToString(), declarationFingerprint.Value), "uses"),
            ],
        };
        value = value with { Counts = CandidateAnalysisCounts.Compute(value) };
        return value with { PayloadId = CandidateAnalysisIdentity.ComputePayloadId(value) };
    }

    private static FindingCaseContract EmptyFindingCases() => new(
        ContractConstants.FindingCaseSchemaId, Version(), Id("payload-1"), Id("run-1"),
        Id("input-1"), Id("promotion-policy-1"), Version(),
        Id("reconciliation-policy-1"), Version(),
        PromotionAssessments: [], Abstentions: [], Findings: [], Recommendations: [], Cases: [],
        ReconciliationAssessments: [], LineageEvents: [], TaxonomyAssignments: [], TaxonomyProjections: [],
        Coverage: [], CoverageFailures: [], Gaps: [], Boundaries: FindingCasePipelineTests.Boundaries(),
        PublicationClaimBoundary: "no-safety-claim");

    private static IdentityEnvelopeContract Identity()
    {
        IdentityEnvelopeContract value = new(
            "analyzer-family", Version(), Version(), new Dictionary<string, string> { ["subject-1"] = "subject" },
            "cause", "locus", ["applicable"], Id("dependency-1"), Fingerprint);
        return value with { CanonicalSignature = FindingCaseIdentity.ComputeIdentitySignature(value) };
    }

    private static DocumentationEvidenceContract DocumentationWithApplications(
        IReadOnlyList<ClaimApplicationContract> applications)
    {
        DocumentationEvidenceContract value = new(
            ContractConstants.DocumentationEvidenceSchemaId,
            Version(),
            Id("documentation-payload"),
            Id("run-1"),
            [
                new(
                    Id("revision-1"), Id("source-1"), DocumentationSourceKind.Fixture, "1",
                    Fingerprint, 1, Id("snapshot-1"),
                    Slice5ResultState.Present, ReplayState.CompleteClean),
            ],
            [
                new(
                    Id("import-1"), Id("run-1"), Id("revision-1"),
                    DocumentationImportMode.CleanImport, null, Id("closure-1"), Id("extractor-1"),
                    LlmInvolvementState.None, LlmOperation.None,
                    [
                        new("provider", BoundaryUseState.NotUsed, "local fixture"),
                        new("hosted-search", BoundaryUseState.NotUsed, "local fixture"),
                        new("nexus", BoundaryUseState.NotUsed, "local fixture"),
                        new("loot", BoundaryUseState.NotUsed, "local fixture"),
                    ],
                    new UtcTimestamp(DateTimeOffset.UnixEpoch)),
            ],
            [
                new(Id("passage-1"), Id("revision-1"), 0, 1, Fingerprint, Slice5ResultState.Present),
            ],
            [
                new(
                    Id("claim-1"), Id("import-1"), Id("passage-1"), ClaimKind.Requirement, "exact claim", [],
                    EvidenceAuthority.AuthoritativeExternal, ClaimApplicabilityState.Applicable,
                    ClassificationRole.Observed, []),
            ],
            applications,
            [], [], [], []);
        return value with { PayloadId = DocumentationEvidenceIdentity.ComputePayloadId(value) };
    }

    private static CandidateDecisionContract AdmittedDecision(string decisionId, string memberId)
    {
        OpaqueId member = Id(memberId);
        OpaqueId dependency = Id("dependency-1");
        return new(
            Id(decisionId), member, Id("fact-1"), CandidateLane.OptionalRanked,
            CandidateDecisionDisposition.CandidateAdmitted,
            [new(Id("left"), "source"), new(Id("right"), "target")],
            "typed-causal-join", [Id("left"), Id("evidence-1"), Id("right")],
            CandidateAnalysisIdentity.StableId("candidate-closure", member.Value, dependency.Value),
            "eligible relationship", [Id("evidence-1")], false, 1)
        {
            AnalyzerId = Id("analyzer-1"),
            PolicyId = Id("policy-1"),
            ThresholdId = Id("threshold-1"),
            LimitId = Id("limit-1"),
            DependencyIds = [dependency],
        };
    }

    private static CandidateDependencyEdgeContract Edge(
        string fromKind,
        OpaqueId fromId,
        string toKind,
        OpaqueId toId,
        string edgeKind) => new(
            CandidateAnalysisIdentity.StableId(
                "candidate-edge", fromKind, fromId.Value, toKind, toId.Value, edgeKind),
            fromKind, fromId, toKind, toId, edgeKind);

    private static AnalysisExecutionInputContract ExecutionInput(ReplayMode mode, OpaqueId? priorRunId) => new(
        ContractConstants.AnalysisExecutionInputSchemaId, Version(), Id("execution-1"), Id("run-1"),
        Reference("snapshot"), Reference("bethesda"), [], [], Reference("configuration"), Reference("manifest"),
        mode, priorRunId, 1, new(1, 1, 1, 1, 1),
        [
            new("provider", BoundaryUseState.NotUsed, "local fixture"),
            new("hosted-search", BoundaryUseState.NotUsed, "local fixture"),
            new("nexus", BoundaryUseState.NotUsed, "local fixture"),
            new("loot", BoundaryUseState.NotUsed, "local fixture"),
        ]);

    private static ArtifactReferenceContract Reference(string value) => new(
        Id(value), Version(), new Sha256Fingerprint(new string('0', 64)), "retained");

    private static ContractVersion Version() => new(1, 0, 0);

    private static OpaqueId Id(string value) => new(value);
}
