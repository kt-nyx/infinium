using Infinium.Application.Analysis;
using Infinium.Domain.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Infinium.Tests;

#pragma warning disable CA1861 // Small literal test graphs are intentionally local to each assertion.

[TestClass]
public sealed class TargetedVerificationPlannerTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void DependencyClosureIsPermutationInvariantCycleSafeAndMonotonic()
    {
        TargetedScopeMemberContract a = Member("a", TargetedScopeMemberKind.Finding);
        TargetedScopeMemberContract b = Member("b", TargetedScopeMemberKind.Candidate);
        TargetedScopeMemberContract c = Member("c", TargetedScopeMemberKind.Hypothesis);
        TargetedScopeDependencyContract ab = Edge("ab", a, b, "root-member");
        TargetedScopeDependencyContract bc = Edge("bc", b, c, "candidate-hypothesis");
        TargetedScopeDependencyContract ca = Edge("ca", c, a, "root-member");

        TargetedAnalysisScopeContract first = Close([a], [a, b, c], [ab, bc, ca]);
        TargetedAnalysisScopeContract permuted = Close([a], [c, a, b], [ca, bc, ab]);
        Assert.AreEqual(first.CanonicalFingerprint, permuted.CanonicalFingerprint);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, first.Members.Select(item => item.MemberId.Value).ToArray());

        TargetedScopeMemberContract d = Member("d", TargetedScopeMemberKind.Evidence);
        TargetedAnalysisScopeContract expanded = Close([a], [a, b, c, d], [ab, bc, ca, Edge("cd", c, d, "evidence-support")]);
        Assert.IsTrue(first.Members.Select(item => item.MemberId).ToHashSet()
            .IsSubsetOf(expanded.Members.Select(item => item.MemberId).ToHashSet()));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DependencyClosureRejectsUnknownEdgesAndBounds()
    {
        TargetedScopeMemberContract a = Member("a", TargetedScopeMemberKind.Finding);
        TargetedScopeMemberContract missing = Member("missing", TargetedScopeMemberKind.Candidate);
        Assert.ThrowsExactly<InvalidDataException>(() => Close([a], [a], [Edge("unknown-from", missing, a, "root-member")]));
        Assert.ThrowsExactly<InvalidDataException>(() => Close([a], [a], [Edge("unknown-to", a, missing, "root-member")]));
        Assert.ThrowsExactly<InvalidDataException>(() => TargetedVerificationPlanner.CloseScope(
            new("preparation"), new("occurrence"), [a], [a], [], maximumMembers: 0, maximumEdges: 0));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CorrelationRetainsAbsenceDenominatorAndNeverFabricatesExecutionMembers()
    {
        TargetedScopeMemberContract finding = Member("finding", TargetedScopeMemberKind.Finding);
        TargetedScopeMemberContract candidate = Member("candidate", TargetedScopeMemberKind.Candidate);
        TargetedAnalysisScopeContract scope = Close([finding], [finding, candidate],
            [Edge("finding-candidate", finding, candidate, "root-member")]);
        OpaqueId proof = new("complete-enumeration-proof");
        TargetedCorrelationCoverageContract coverage = Correlate(scope,
        [
            Observation(finding, TargetedCorrelationStatus.ChangedCorrelated, true, true, null, proof),
            Observation(candidate, TargetedCorrelationStatus.ProvenAbsent, true, true, null, proof),
        ]);

        Assert.AreEqual(2L, coverage.PopulationDenominator);
        Assert.IsTrue(coverage.Startable);
        TargetedCorrelationCoverageRowContract absent = coverage.Rows.Single(row => row.ScopeMemberId == candidate.MemberId);
        Assert.AreEqual("completed-observation", absent.DenominatorEffect);
        Assert.IsNull(absent.CurrentExecutionMemberId);
        Assert.AreEqual(TargetedCorrelationStatus.ProvenAbsent, absent.Status);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CorrelationSeparatesIdentityFailureFromProcessingGap()
    {
        TargetedScopeMemberContract member = Member("member", TargetedScopeMemberKind.Record);
        TargetedAnalysisScopeContract scope = Close([member], [member], []);
        OpaqueId proof = new("proof");

        TargetedCorrelationCoverageContract ambiguous = Correlate(scope,
        [
            Observation(member, TargetedCorrelationStatus.MatchedExecutable, true, true, new("one"), proof),
            Observation(member, TargetedCorrelationStatus.MatchedExecutable, true, true, new("two"), proof),
        ]);
        Assert.IsFalse(ambiguous.Startable);
        Assert.AreEqual(TargetedCorrelationStatus.Ambiguous, ambiguous.Rows.Single().Status);

        TargetedCorrelationCoverageContract unsupportedIdentity = Correlate(scope,
            [Observation(member, TargetedCorrelationStatus.Unsupported, false, false, null, proof)]);
        Assert.IsFalse(unsupportedIdentity.Startable);

        TargetedCorrelationCoverageContract unavailableAnalyzer = Correlate(scope,
            [Observation(member, TargetedCorrelationStatus.Inaccessible, true, false, null, proof)]);
        Assert.IsTrue(unavailableAnalyzer.Startable);
        Assert.IsTrue(unavailableAnalyzer.Limited);

        TargetedCorrelationCoverageContract missing = Correlate(scope, []);
        Assert.IsFalse(missing.Startable);
        Assert.AreEqual(TargetedCorrelationStatus.MissingRequiredProof, missing.Rows.Single().Status);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CorrelationRejectsScopeExpansionAndDoesNotIgnoreOptionalIdentityFailure()
    {
        TargetedScopeMemberContract member = Member("member", TargetedScopeMemberKind.Record) with
        {
            Mandatory = false,
        };
        TargetedScopeMemberContract outside = Member("outside", TargetedScopeMemberKind.Record);
        TargetedAnalysisScopeContract scope = Close([member], [member], []);
        OpaqueId proof = new("proof");

        Assert.ThrowsExactly<InvalidDataException>(() => Correlate(scope,
            [Observation(outside, TargetedCorrelationStatus.MatchedExecutable, true, true, outside.MemberId, proof)]));

        TargetedCorrelationCoverageContract incomplete = Correlate(scope,
            [Observation(member, TargetedCorrelationStatus.Ambiguous, false, false, null, proof)]);
        Assert.IsFalse(incomplete.Startable);
        Assert.IsNotEmpty(incomplete.NonStartableReasons);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AllAbsentCaseRetainsEveryDenominatorWithoutInventingCandidates()
    {
        TargetedScopeMemberContract sourceCase = Member("case", TargetedScopeMemberKind.Case);
        TargetedScopeMemberContract first = Member("first", TargetedScopeMemberKind.Candidate);
        TargetedScopeMemberContract second = Member("second", TargetedScopeMemberKind.Candidate);
        TargetedAnalysisScopeContract scope = Close([sourceCase], [sourceCase, first, second],
        [
            Edge("case-first", sourceCase, first, "case-member"),
            Edge("case-second", sourceCase, second, "case-member"),
        ]);
        OpaqueId proof = new("complete-population-proof");

        TargetedCorrelationCoverageContract coverage = Correlate(scope,
        [
            Observation(sourceCase, TargetedCorrelationStatus.ProvenNotApplicable, true, true, null, proof),
            Observation(first, TargetedCorrelationStatus.ProvenAbsent, true, true, null, proof),
            Observation(second, TargetedCorrelationStatus.ProvenAbsent, true, true, null, proof),
        ]);

        Assert.IsTrue(coverage.Startable);
        Assert.AreEqual(3L, coverage.PopulationDenominator);
        Assert.IsTrue(coverage.Rows.All(row => row.CurrentExecutionMemberId is null));
        Assert.IsTrue(coverage.Rows.All(row => row.DenominatorEffect == "completed-observation"));
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void PlanIdentityBindsCanonicalContentAndRejectsMutation()
    {
        TargetedScopeMemberContract member = Member("finding", TargetedScopeMemberKind.Finding);
        TargetedAnalysisScopeContract scope = Close([member], [member], []);
        OpaqueId proof = new("proof");
        TargetedCorrelationCoverageContract coverage = Correlate(scope,
            [Observation(member, TargetedCorrelationStatus.ChangedCorrelated, true, true, member.MemberId, proof)]);
        TargetedVerificationSourceContract source = new(new("source-run"), TargetedVerificationRootKind.Finding,
            new("source-occurrence"), new("source-logical"), new("source-payload"), new(new string('1', 64)),
            new(new string('2', 64)), new("source-snapshot"), new("context"), new("configuration"), new("manifest"));
        TargetedVerificationPlanContract draft = new("infinium/targeted-verification-plan", new(1, 0, 0),
            new("targeted-plan-pending"), new("preparation"), 4, source, new("capture"), new("target-snapshot"),
            new(new string('3', 64)), new("acquisition"), new("semantic-output"), new(new string('4', 64)),
            scope, coverage, [], "scope-limited-no-readiness", true, false, [], [], new(new string('0', 64)));
        Sha256Fingerprint fingerprint = TargetedVerificationContractInvariants.ComputePlanFingerprint(draft);
        TargetedVerificationPlanContract plan = draft with
        {
            PlanId = new("targeted-plan-" + fingerprint.Value[..32]),
            PlanFingerprint = fingerprint,
        };
        TargetedVerificationContractInvariants.ValidatePlanIdentity(plan);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            TargetedVerificationContractInvariants.ValidatePlanIdentity(plan with { Limited = true }));
    }

    private static TargetedAnalysisScopeContract Close(
        IReadOnlyList<TargetedScopeMemberContract> roots,
        IReadOnlyList<TargetedScopeMemberContract> members,
        IReadOnlyList<TargetedScopeDependencyContract> edges) => TargetedVerificationPlanner.CloseScope(
            new("preparation"), new("occurrence"), roots, members, edges);

    private static TargetedCorrelationCoverageContract Correlate(
        TargetedAnalysisScopeContract scope,
        IReadOnlyList<TargetedCurrentObservationContract> observations) => TargetedVerificationPlanner.Correlate(
            new("preparation"), scope, new("target-snapshot"), new("acquisition"), new("semantic-output"), observations);

    private static TargetedScopeMemberContract Member(string id, TargetedScopeMemberKind kind) =>
        new(new(id), kind, new("stable-" + id), "test member", true, [new("proof-" + id)]);

    private static TargetedScopeDependencyContract Edge(string id, TargetedScopeMemberContract from,
        TargetedScopeMemberContract to, string relation) =>
        new(new(id), from.MemberId, to.MemberId, relation, [new("proof-" + id)]);

    private static TargetedCurrentObservationContract Observation(TargetedScopeMemberContract member,
        TargetedCorrelationStatus status, bool correlationQualified, bool processingQualified,
        OpaqueId? currentExecutionMemberId, OpaqueId proof) =>
        new(member.StableIdentity, new("target-population"),
            status is TargetedCorrelationStatus.ProvenAbsent or TargetedCorrelationStatus.ProvenNotApplicable
                ? null : member.StableIdentity,
            currentExecutionMemberId,
            status, correlationQualified, processingQualified, "test observation", [proof], proof);
}
