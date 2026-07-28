# ADR-0022: Finding and case continuity and reconciliation

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Context

Repeated scans must preserve immutable analytical history while helping the
user recognize when a current finding or case represents the same underlying
condition as an earlier one. Names, prose, taxonomy labels, participant sets,
or whole-snapshot equality cannot safely establish that continuity. Treating
them as identity would create false merges, false splits, stale disposition
carryover, and historical reports that change after the fact.

The product therefore needs an explicit distinction among run occurrences,
logical identity, reconciliation evidence, lineage, and review state. It must
also remain honest when evidence needed to prove continuity has been deleted
or was never available.

## Decision drivers

- Run-specific findings, cases, exports, and readiness results are immutable.
- Continuity must be based on the same causal condition or shared cause under
  equivalent applicability and dependencies.
- False merges and disposition/suppression leakage are more harmful than a
  visible ambiguous or unreviewed result.
- Analyzer and taxonomy revisions must not silently rewrite product identity.
- Lead-only and supported cases must retain their distinct historical meaning.
- Merge, split, supersession, correction, and review-state carryover must be
  inspectable and reversible without destructive history edits.
- M1 needs a conservative durable substrate; interactive adjudication belongs
  to later workflow work.

## Considered options

### Use a canonical hash as the permanent logical ID

Rejected. Canonicalization, participant mapping, and analyzer-contract changes
would either rewrite identity or falsely split one continuing condition.
Signatures and hashes remain useful candidate indexes only.

### Mutate one latest finding or case row

Rejected. This loses run ownership, prior conclusions, contradictions, review
applicability, export truth, and the reason a historical readiness result
existed.

### Reconcile by names, taxonomy, record family, participant overlap, or a
single fuzzy score

Rejected as identity authority. These values may retrieve candidates or rank a
manual-review queue, but they do not prove a shared cause, applicability, or
dependency closure. Model-assisted entity resolution remains lead-only until
typed evidence validates it.

### Never reconcile across runs

Rejected as the product model. It preserves history but makes repeated scans
unreviewable and cannot express valid remediation follow-up or review-state
continuity.

### Use opaque logical IDs plus evidence-bearing reconciliation and append-only
lineage

Selected. It preserves history, supports conservative continuity and
correction, and exposes uncertainty rather than encoding it into identity.

## Decision

1. Every analysis run shall emit immutable finding and case occurrences with
   unique occurrence IDs. A separate opaque logical finding ID or logical case
   ID may connect occurrences across runs. Neither a signature nor content hash
   is the logical ID.
2. Every delivered analyzer shall publish versioned semantic-compatibility and
   identity-contract metadata. Each occurrence shall retain an inspectable
   identity envelope containing the analyzer family and versions, typed
   participant identities and causal roles, causal condition or shared-cause
   pattern, affected locus or analytical question, applicability predicates,
   complete dependency-closure reference, and canonical signature version.
3. Canonical signatures and optional fingerprints are bounded candidate
   retrieval keys. They do not grant identity. Display prose, titles, mod
   names, symptoms, severity, confidence, and taxonomy assignments are excluded
   from causal identity. Taxonomy may support routing and presentation only.
4. Reconciliation shall evaluate causal equivalence, applicability
   equivalence, dependency equivalence, and producer/identity-contract
   compatibility as separate, retained gates. One score cannot hide a missing
   or failed gate.
5. Automatic reconciliation shall initially be limited to a unique, fully
   proven one-to-one match. Every assessment shall record the considered
   occurrences, gate results, proof and gaps, policy/contract versions, actor
   or mechanism, time, and resulting outcome.
6. Reconciliation outcomes shall distinguish at least
   `exact-continuation`, `analytical-revision`, `related-follow-up`,
   `new-distinct`, `ambiguous`, `unknown`, `not-observed`, and
   `not-evaluated`. Absence after completed applicable analysis is not by
   itself verified resolution; skipped, failed, unsupported, or abstained work
   shall not be presented as observation.
7. A changed conclusion under proven continuity creates an immutable
   analytical revision and explicit revision/supersession lineage. A material
   change to the causal condition, applicability, or dependency closure creates
   a related follow-up or distinct condition rather than dependency-validated
   continuity.
8. Cases shall reconcile only after their member findings and independent
   shared-cause proof have been evaluated. Membership overlap or the appearance
   or disappearance of one symptom does not establish a case merge or split.
9. Promotion of a lead-only case shall create a new supported-case occurrence
   under a successor logical case and a `promotes-lead` lineage event. The
   earlier lead-only case remains unchanged, and hypotheses are not silently
   relabeled as findings.
10. Merge, split, supersession, promotion, and correction shall use append-only
    lineage events and non-destructive successor identities. Historical
    occurrences, exports, readiness evaluations, and earlier identity decisions
    shall not be rewritten or deleted to make the latest projection simpler.
11. Dispositions, suppression, and annotations remain separate review events
    bound to exact finding or case revisions. Identity continuity does not
    itself carry review state. Any carryover shall create a distinct,
    provenance-bearing event that references the source review event, source
    and target occurrences, reconciliation assessment, applicable scope,
    dependency validation, policy version, actor, reason, and time.
12. Review-state carryover requires exact or declared-compatible semantic
    continuity, complete applicable identity proof, proven applicability and
    dependency equivalence, a retained valid source event, and no unresolved
    contradiction affecting the decision. Material change, ambiguity, unknown
    proof, or unsupported compatibility prevents carryover and leaves the new
    occurrence visible by default. Suppression receives no weaker rule than
    disposition.
13. If retained continuity proof is deleted, previously accepted
    reconciliation and carryover events remain immutable historical decisions,
    but the lost inspectability shall be recorded as an audit gap. Deleted
    proof cannot authorize a new reconciliation or carryover. The deletion
    preview shall disclose the affected identity decisions, future carryover,
    replay/audit status, and independently retained copies before removal.
14. Current-history and current-review views are rebuildable projections over
    immutable occurrences, reconciliation assessments, lineage, and review
    events. A projection is not the only record of any identity decision.

## M1 boundary

M1 shall implement:

- opaque logical finding and case IDs plus immutable run occurrence IDs;
- the versioned identity envelope for every exercised analyzer;
- conservative unique one-to-one reconciliation for only the exact participant
  and dependency types delivered by M1;
- all explicit outcomes in item 6;
- append-only reconciliation and lineage records, with schema support for
  later merge/split successors;
- human-readable CLI and versioned JSON output explaining the decision, proof,
  gaps, origin run, and lineage; and
- no implicit review-state carryover. If M1 exposes dispositions, it must use
  exact-revision events and the validated carryover contract.

M2 or later may add interactive ambiguity review, reviewed merge/split and
correction workflows, cross-analyzer adjudication beyond declared compatible
contracts, broad participant-identity support, lineage visualization,
historical bulk cleanup, learned/fuzzy candidate ranking, and automatic
annotation retargeting. These deferrals do not permit content-derived logical
IDs, destructive latest-row storage, or silent review-state carryover in M1.

## Consequences

### Positive

- Repeated scans can be organized without changing historical analytical truth.
- Dispositions and suppression cannot leak merely because two outputs look
  similar.
- Analyzer and taxonomy evolution have explicit compatibility and lineage.
- Ambiguity, missing proof, missing coverage, and later correction remain
  visible.
- Lead promotion, merge, and split preserve the original case meaning.

### Negative

- Every analyzer must define typed semantic identity and dependency closure.
- Reconciliation needs additional persistence, indexes, audit records, and
  explanation.
- Conservative matching may produce more ambiguous/new items than users expect.
- Interactive merge/split correction is deferred beyond M1.

### Risks and mitigations

- **False merge:** require all independent gates and unique one-to-one proof;
  prefer ambiguity over recall.
- **False split:** use scoped dependency closure and typed local
  correspondence, not whole-snapshot equality or display names.
- **Analyzer upgrade drift:** require explicit semantic and identity-contract
  compatibility declarations and retain both versions.
- **Disposition leakage:** keep review events revision-bound and apply stricter
  validated carryover separately from identity continuity.
- **Deletion erases proof:** retain the historical decision, create an audit
  gap, prohibit new use of missing proof, and preview the consequence.
- **Projection corruption:** rebuild current views from append-only source
  events and validate them against authoritative history.

## Requirements affected

- SNAP-003 through SNAP-006
- FIND-002 and FIND-005 through FIND-014
- OPS-002 through OPS-004
- EVID-004 through EVID-007

## Validation

No evaluation is passed by accepting this ADR.

EVAL-0079 shall include generic positive, negative, boundary, deletion, and
metamorphic cases for exact continuation; unrelated change; display rename;
same names with distinct causes; changed applicability or dependencies; new
contradictions; compatible and incompatible analyzer changes; taxonomy
reclassification; stable shared cause with changed membership; false-merge and
false-split defenses; missing proof; `not-observed` versus `not-evaluated`;
lead promotion; suppression leakage; append-only merge/split/correction
lineage; review-state carryover; and candidate-order independence.

The M1 gate covers noninteractive identity/reconciliation and durable lineage.
Interactive reviewed merge/split, bulk action, and correction workflow cases
are M2 extensions. Metrics shall report auto-reconciliation precision and
coverage, false-merge and false-split rates, ambiguity/unknown causes,
review-state carryover precision, erroneous suppression carryover, and
decision explainability separately.

EVAL-0087 must additionally prove that retained identity envelopes,
dependencies, reconciliation/lineage records, review events, and deletion audit
gaps survive crash/recovery, backup/restore, graph-aware deletion, and
projection rebuild without manufacturing or rebinding continuity.

The decision must be revisited if an exercised analyzer cannot express typed
causal identity without prose, controlled evaluation finds false merges under
the unique fully proven rule, cross-analyzer reconciliation becomes an M1
requirement, or the selected store cannot retain append-only many-to-many
lineage efficiently.

## References

- [Product requirements](../../product/requirements.md)
- [Domain model](../../product/domain-model.md)
- [ADR-0001](ADR-0001-evidence-authority-boundary.md)
- [ADR-0002](ADR-0002-snapshot-context-binding.md)
- [ADR-0010](ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
- [ADR-0015](ADR-0015-authoritative-evidence-persistence-and-payload-storage.md)
- [RESEARCH-0042](../../research/investigations/RESEARCH-0042-finding-case-continuity-and-reconciliation.md)
- [RESEARCH-0044](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
