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
            Id("decision-1"), Id("member-1"), CandidateLane.MandatoryEvidence,
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

        CandidateAnalysisEntryContract candidate = new(
            Id("candidate-1"), first.DecisionId, Slice5ResultState.Present, "causal explanation",
            [Id("evidence-1")], [], [], AnalysisConfidence.Plausible, Id("threshold-1"));
        CandidateAnalysisContract valid = missingCandidate with { Candidates = [candidate] };
        Slice5ContractInvariants.Validate(valid);
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelRejectsSpeculativeFindingAndReadinessAffectingLead()
    {
        FindingContract finding = new(
            Id("finding-1"), Id("logical-finding-1"), Id("run-1"), Id("candidate-1"),
            "speculative only", FindingSeverity.Major, AnalysisConfidence.SpeculativeLead,
            [Id("evidence-1")], Id("identity-1"), null);
        FindingCaseContract speculative = EmptyFindingCases() with { Findings = [finding] };
        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(speculative));

        Slice5CaseContract lead = new(
            Id("case-1"), Id("logical-case-1"), Id("run-1"), CaseOccurrenceKind.LeadOnly,
            [], [Id("candidate-1")], "shared unresolved cause", [Id("evidence-1")], true);
        FindingCaseContract readinessLead = EmptyFindingCases() with { Cases = [lead] };
        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(readinessLead));
    }

    [TestMethod]
    [TestCategory("M1Unit")]
    [TestProperty("Category", "M1Unit")]
    public void Slice5StateModelCompleteReplayCannotHideMissingDependenciesOrGaps()
    {
        AnalysisReplayContract invalid = new(
            ContractConstants.AnalysisReplaySchemaId, Version(), Id("replay-1"), Id("run-1"),
            ReplayMode.RetainedDownstreamReplay, ReplayState.CompleteClean, AuditabilityState.Complete,
            [], [], [], [Id("missing-1")], [Id("gap-1")], true, Id("prior-run"));

        Assert.ThrowsExactly<InvalidOperationException>(() => Slice5ContractInvariants.Validate(invalid));
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
            Id("closure-1"), ClaimApplicabilityState.Applicable, []);
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

    private static CandidateAnalysisContract EmptyCandidates() => new(
        ContractConstants.CandidateAnalysisSchemaId, Version(), Id("payload-1"), Id("run-1"),
        Id("analyzer-1"), Id("population-1"), 0, [], [], [], [], []);

    private static FindingCaseContract EmptyFindingCases() => new(
        ContractConstants.FindingCaseSchemaId, Version(), Id("payload-1"), Id("run-1"),
        [], [], [], [], [], [], [], []);

    private static DocumentationEvidenceContract DocumentationWithApplications(
        IReadOnlyList<ClaimApplicationContract> applications) => new(
            ContractConstants.DocumentationEvidenceSchemaId,
            Version(),
            Id("documentation-payload"),
            Id("run-1"),
            [
                new(
                    Id("revision-1"), Id("source-1"), Fingerprint, 1, Id("import-1"), null,
                    Slice5ResultState.Present, ReplayState.CompleteClean),
            ],
            [
                new(Id("passage-1"), Id("revision-1"), 0, 1, Fingerprint, Slice5ResultState.Present),
            ],
            [
                new(
                    Id("claim-1"), Id("passage-1"), ClaimKind.Requirement, "exact claim", [],
                    EvidenceAuthority.SnapshotBoundLocal, ClaimApplicabilityState.Applicable,
                    ClassificationRole.Observed, []),
            ],
            applications,
            [], [], []);

    private static CandidateDecisionContract AdmittedDecision(string decisionId, string memberId) => new(
        Id(decisionId), Id(memberId), CandidateLane.OptionalRanked,
        CandidateDecisionDisposition.CandidateAdmitted,
        [new(Id("left"), "source"), new(Id("right"), "target")],
        "typed-causal-join", [Id("edge-1")], Id("closure-1"), "eligible relationship",
        [Id("evidence-1")], true, 1);

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
        Id(value), Version(), new Sha256Fingerprint(new string('0', 64)), "present");

    private static ContractVersion Version() => new(1, 0, 0);

    private static OpaqueId Id(string value) => new(value);
}
