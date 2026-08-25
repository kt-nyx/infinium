using Infinium.Application.ScopeReversion;
using Infinium.Bethesda;
using Infinium.Domain.Contracts;

namespace Infinium.Tests;

public static class ScopeReversionV2TestSupport
{
    public static ScopeReversionV2ProjectionRequest Request(
        ScopeReversionV2SubjectKind kind = ScopeReversionV2SubjectKind.ActorCohort,
        bool restored = false,
        bool admitted = true,
        bool purposeObserved = true,
        bool includeWinning = true)
    {
        OpaqueId runId = new("run-v2-test");
        OpaqueId subjectId = new(kind == ScopeReversionV2SubjectKind.ActorCohort ? "subject-actor" : "subject-reference");
        OpaqueId causeId = new("shared-cause");
        string[] memberNames = kind == ScopeReversionV2SubjectKind.ActorCohort ? ["member-a", "member-b"] : ["member-reference"];
        List<BethesdaNpcFact> npcs = [];
        List<BethesdaPlacedReferenceFact> references = [];
        List<ScopeReversionV2ProjectionMemberSpec> memberSpecs = [];
        foreach (string memberName in memberNames)
        {
            string priorId = memberName + "-prior";
            string winningId = memberName + "-winning";
            string priorTarget = memberName + "-relation";
            string winningTarget = restored ? priorTarget : memberName + "-different";
            if (kind == ScopeReversionV2SubjectKind.ActorCohort)
            {
                npcs.Add(Npc(priorId, priorTarget));
                if (includeWinning)
                {
                    npcs.Add(Npc(winningId, winningTarget));
                }
            }
            else
            {
                references.Add(Reference(priorId, priorTarget));
                if (includeWinning)
                {
                    references.Add(Reference(winningId, winningTarget));
                }
            }
            memberSpecs.Add(new(new OpaqueId(memberName), subjectId, kind,
                kind == ScopeReversionV2SubjectKind.ActorCohort ? "packages" : "linked-references",
                priorId, winningId, purposeObserved, [new OpaqueId("source-decision")], causeId,
                [new OpaqueId("dependency")], [new OpaqueId("evidence")],
                kind == ScopeReversionV2SubjectKind.ActorCohort ? ["AIDT retained outside bounded claim"] : [],
                kind == ScopeReversionV2SubjectKind.PlacedReference ? ["runtime and quest consequence not validated"] : []));
        }
        BethesdaSemanticSnapshot snapshot = new(
            new OpaqueId("bethesda-snapshot"), BethesdaSemanticContract.SchemaVersion, "test", "1.0.0",
            Hash("snapshot"), [], new Dictionary<string, BethesdaOverrideChain>(),
            new Dictionary<string, BethesdaRecordContribution>(), npcs, [], references, [],
            new Dictionary<string, BethesdaResolvedParticipant>(), new Dictionary<string, BethesdaNpcFact>(),
            new Dictionary<string, BethesdaRaceFact>(), new Dictionary<string, BethesdaPlacedReferenceFact>(), [],
            new Dictionary<string, IReadOnlyList<BethesdaLinkFact>>(), [], [], [], []);
        ScopeReversionV2SubjectContract subject = new(subjectId, kind,
            memberSpecs.Select(item => item.MemberId).OrderBy(item => item.Value, StringComparer.Ordinal).ToArray(),
            causeId, "bounded locus", "bounded predicted symptom", "reversible local correction",
            "rerun local deterministic analysis", kind == ScopeReversionV2SubjectKind.PlacedReference
                ? ["runtime and quest consequence not validated"] : ["AIDT remains outside bounded claim"]);
        ScopeReversionV2SourceDecisionContract source = new(
            new OpaqueId("source-decision"), runId, "SRC-TEST", "1", "passage-1", Hash("passage"),
            "docs/research/test-public-manifest.json",
            new UtcTimestamp(DateTimeOffset.UnixEpoch), SemanticProposalState.Proposed,
            admitted ? SemanticSupportState.Supported : SemanticSupportState.Unsupported,
            admitted ? SemanticApplicabilityState.Applicable : SemanticApplicabilityState.Unknown,
            admitted ? SemanticDecisionState.Admitted : SemanticDecisionState.Rejected,
            Hash("local-fact"), [subjectId], ["purpose"], [new OpaqueId("source-evidence")], "typed source decision");
        string[] axes = ["compatibility_surface", "gameplay_system", "impact_scope", "technical_mechanism"];
        ScopeReversionV2TaxonomyReferenceContract[] taxonomy = axes
            .Select(axis => new ScopeReversionV2TaxonomyReferenceContract(
                ScopeReversionV2Contract.StableId("taxonomy", subjectId.Value, axis), runId,
                ScopeReversionV2Contract.TaxonomyId, ScopeReversionV2Contract.TaxonomyVersion,
                subjectId, axis, axis + "_facet", axis + "_code", TaxonomyApplicability.Assigned,
                ClassificationRole.Observed, [new OpaqueId("taxonomy-evidence")], "upstream typed taxonomy"))
            .OrderBy(item => item.AssignmentId.Value, StringComparer.Ordinal).ToArray();
        return new(runId, "answer-free-test-handoff", Hash("manifest"),
            [new("docs/research/test-public-manifest.json", 123, Hash("public-manifest"))],
            [new("cases/test/plugins/TestInput.esp", "positive-plugin-or-asset", 456, Hash("controlled-input"))],
            ScopeReversionV2PartitionRole.ControlledRealValidation,
            snapshot, [subject], memberSpecs, [source], taxonomy);
    }

    private static BethesdaNpcFact Npc(string id, string target) => new(
        Contribution(id, "NPC_"), 0, 0, false, BethesdaTemplateTraitsDecision.KnownNotInherited,
        false, null, null, null, [Link(id, "PKID", target)], [], null);

    private static BethesdaPlacedReferenceFact Reference(string id, string target) => new(
        Contribution(id, "REFR"), null, [Link(id, "XLKR", target)], null, null, null);

    private static BethesdaRecordContribution Contribution(string id, string signature) => new(
        id, new BethesdaRecordIdentity(id + "-participant", signature, id + ":Test.esm", "Test.esm", 1),
        "Test.esm", 0, false, false, 0);

    private static BethesdaLinkFact Link(string contributionId, string field, string target) => new(
        contributionId + "-participant", contributionId, field, null, 0, target,
        BethesdaLinkState.Resolved, target + "-participant");

    private static Sha256Fingerprint Hash(string value) => ContractJsonSerializer.Fingerprint(value);
}
