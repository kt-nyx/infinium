# RESEARCH-0042: Finding and case continuity and reconciliation

- **Status:** Completed
- **Date opened:** 2026-07-28
- **Last reviewed:** 2026-07-28
- **Researcher:** Codex agent
- **Primary research question:** RQ-033
- **Research wave:** M0 Wave E
- **Decision enabled:** Finding/case identity, reconciliation, and review-state
  carryover ADR
- **RQ status:** Resolved for M0 by accepted ADR-0022
- **Acceptance:** Recommendation accepted by the project owner through
  ADR-0022 on 2026-07-28

## Executive answer

Infinium should not derive the identity of a finding or case from a title, mod
name, taxonomy code, or one opaque content hash. It should preserve three
different concepts:

1. an **immutable run occurrence** containing exactly what one run concluded;
2. an **opaque logical identity** representing a recorded continuity claim
   across occurrences; and
3. an **append-only reconciliation assessment or lineage event** explaining
   why occurrences were matched, left separate, superseded, merged, or split.

Automated reconciliation should be conservative and two-stage:

- generate possible matches from a versioned canonical signature and typed
  indexes; then
- admit continuity only after separately proving **causal identity**,
  **applicability equivalence**, and **dependency equivalence**.

The signature is an efficient and inspectable candidate key, not identity
authority. It uses typed participant identities and roles, a typed causal
pattern, an affected locus or analytical question, applicability predicates,
the artifact's dependency closure, and analyzer identity-contract versions.
Display names and taxonomy labels may help presentation or candidate retrieval
but never prove continuity.

The safe default is asymmetric:

- a unique, fully proven one-to-one match may join an existing logical identity;
- material dependency or applicability changes create a related successor,
  validation, or follow-up relationship rather than pretending that the old
  finding is unchanged;
- an ambiguous, unsupported, stale, or unavailable comparison remains
  unreconciled and requires review or stays separate;
- many-to-one or one-to-many identity changes require explicit merge/split
  lineage and never rewrite historical membership.

Review state remains attached to exact finding revisions. Carryover creates a
separate event linking a prior disposition, suppression choice, or annotation
to a new revision and its equivalence proof. A changed finding is visible and
unreviewed by default. Case-level actions never bypass per-finding review
state.

For M1, implement only opaque logical IDs, immutable occurrences, the typed
signature/dependency contract for exercised surfaces, conservative exact
one-to-one reconciliation, explicit unmatched/ambiguous outcomes, and the
lineage representation needed by later milestones. Interactive merge/split
adjudication and broad historical cleanup can wait for M2, but M1 must not
create a schema that makes those operations destructive.

## Research question

RQ-033 asks:

> Which causal/dependency continuity keys and reconciliation workflow can link
> or explicitly supersede logical findings and cases across runs without false
> merges, false splits, or disposition leakage?

The conceptual problem has four parts:

- how to recognize the same underlying analytical condition after a new run;
- how to avoid treating merely similar outputs as the same condition;
- how to preserve history when a prior grouping or identity judgment changes;
  and
- how to carry user review state only where it is still applicable.

## Scope and non-scope

### In scope

- logical finding and case identity;
- immutable run-specific finding and case revisions;
- typed participant, cause, applicability, and dependency signatures;
- analyzer, taxonomy, and identity-contract version changes;
- automated and reviewed reconciliation outcomes;
- explicit merge, split, supersession, and correction lineage;
- disposition, suppression, and annotation carryover;
- stale, contradicted, unavailable, and not-observed states;
- a bounded M1 contract and evaluation mapping.

### Out of scope

- the database engine, ORM, IPC, desktop stack, or physical schema;
- the analyzer-specific semantics that decide whether a Skyrim interaction is
  harmful;
- the general snapshot-fingerprint mechanism already governed by ADR-0010;
- readiness policy beyond consuming applicable per-finding dispositions;
- user-interface design beyond the states the UI must be able to explain;
- probabilistic entity resolution from names as a substitute for local
  identity evidence;
- community-shared/global finding identities;
- automatic repair or setup mutation.

## Authoritative constraints

This investigation is derived from accepted product contracts rather than
selecting new product behavior.

### Immutable analytical history

[ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md)
requires every run-specific finding and case revision to remain immutable and
bound to its originating run, snapshot, context, configuration, and resolved
inputs. A later result cannot overwrite an earlier conclusion or its
provenance.

[OPS-002](../../product/requirements.md#ops-002--local-history) requires
logical identities, revision/supersession lineage, dispositions, suppression,
and annotations to persist independently of current projections.

### Continuity requires evidence, not resemblance

[FIND-014](../../product/requirements.md#find-014--cross-run-finding-and-case-identity)
allows reuse, supersession, disposition carryover, or case reconciliation only
when declared causal, applicability, and dependency equivalence establishes
continuity. Ambiguous matches must stay separate or require review.

[FIND-002](../../product/requirements.md#find-002--causal-case-grouping)
requires case grouping around a shared likely cause, usually with a shared
resolution. Shared mod names, record families, participants, symptoms, or
taxonomy labels are insufficient on their own.

### Review state is not analyzer output

[FIND-006](../../product/requirements.md#find-006--persistent-decisions)
binds each disposition to one exact finding revision and its applicability.
Carryover must retain the source review event and a validation record.
Materially changed dependencies preserve history but require new review state;
suppression does not silently carry.

The accepted [domain model](../../product/domain-model.md#disposition) makes
disposition, suppression, resolution verification, and review annotations
separate from immutable analytical output. Case state is derived from member
findings; bulk case actions create explicit per-finding events.

### Dependency identity is scoped

[ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
requires each reusable artifact to declare its smallest complete typed
dependency closure. A global snapshot ID, metadata tuple, path name, or file ID
is not universal content or cache identity.

The accepted candidate work in
[RESEARCH-0022](RESEARCH-0022-candidate-index-and-ranking.md) likewise requires
canonical participants, typed relationships, explicit negative/gap states, and
run-bound provenance. Candidate identity is useful input to reconciliation but
does not automatically become finding or case identity.

### Classifications are not causal identity

The accepted
[mod-impact taxonomy](../../product/mod-impact-taxonomy.md#persistence-contract)
preserves historical assignments by version. Reclassification creates a
linked projection rather than mutating the classified finding or case.
Taxonomy codes describe purpose, surface, affected area, consequence, and
extent; they do not prove a shared cause.

## Method

This is a domain-contract investigation. It traced the accepted requirements
through representative cross-run transitions and tested whether candidate
identity models could express them without mutating history.

The transitions considered were:

- identical resolved inputs and equivalent analyzer output;
- equivalent resolved evidence with a new supporting or contradicting
  interpretation/output, and separately a changed resolved source revision;
- an unrelated snapshot change;
- a relevant winner, provider, record, configuration, or assumption change;
- a display-name or folder-name change with proven participant continuity;
- identical names with different physical or causal participants;
- analyzer and identity-contract upgrades;
- taxonomy reclassification;
- finding promotion, rejection, or disappearance;
- case membership changes around an unchanged shared cause;
- discovery that one case represented two causes;
- discovery that two cases represented one cause;
- missing dependencies or unavailable retained evidence; and
- user review state applied before and after each transition.

No external source was needed to establish the recommendation. Identity here is
a product-specific evidentiary claim governed by Infinium's accepted snapshot,
provenance, and review contracts. Generic event-sourcing or entity-resolution
literature cannot decide when two Skyrim analytical conditions are causally
equivalent.

## Required identity layers

### 1. Run occurrence

A finding occurrence is the immutable analytical conclusion emitted or
admitted in one analysis run. A case occurrence is the immutable grouping
produced in one run.

An occurrence records its complete historical facts, including:

```text
occurrence_id
originating_run_id
installation_snapshot_id
analysis_context_id
effective_scan_configuration_id
resolved_input_manifest_id
producer_analyzer_id
producer_analyzer_version
producer_semantic_contract_version
identity_contract_version
conclusion_or_case_summary
typed_participants_and_roles[]
causal_pattern
applicability_predicates[]
dependency_closure_ref
evidence_refs[]
taxonomy_assignment_refs[]
created_at
```

An occurrence never changes logical identity merely because a current view
later prefers another reconciliation.

### 2. Logical identity

A logical finding ID or logical case ID is an opaque product-generated
identifier. It means only:

> Infinium has accepted that these identified occurrences form one continuity
> under the recorded reconciliation evidence.

The logical ID contains no mod name, path, FormKey, taxonomy code, analyzer
name, severity, confidence, or title. Those values can change, collide, or be
reclassified. They remain inspectable attributes of occurrences and
assessments.

A logical identity is not mutable current state. Its durable meaning is the
set of accepted membership and lineage events retained in history. A current
projection may answer “which logical identity should this occurrence be shown
under now?” without rewriting an earlier answer.

### 3. Reconciliation assessment

Every attempted or accepted cross-run relationship should be a first-class
record:

```text
assessment_id
subject_occurrence_id
candidate_predecessor_occurrence_id?
candidate_logical_identity_id?
assessment_kind
causal_equivalence_state
applicability_equivalence_state
dependency_equivalence_state
producer_compatibility_state
supporting_identity_evidence_refs[]
contradicting_identity_evidence_refs[]
missing_information[]
signature_version
reconciler_id_and_version
actor: system | user
decision_time
decision_reason
supersedes_assessment_id?
```

The independent equivalence states are:

- `proven-equivalent`;
- `proven-different`;
- `unknown`;
- `unsupported`; and
- `not-applicable`.

One total similarity score must not hide which gate failed.

### 4. Lineage event

Many-to-many history requires a structure separate from revision sequence:

```text
lineage_event_id
lineage_kind
predecessor_logical_ids[]
successor_logical_ids[]
effective_from_occurrence_ids[]
supporting_assessment_ids[]
actor
reason
created_at
supersedes_lineage_event_id?
```

The minimum lineage kinds are:

- `continues`;
- `analytical-revision`;
- `supersedes`;
- `related-follow-up`;
- `promotes-lead`;
- `merge`;
- `split`;
- `merge-correction`;
- `split-correction`; and
- `relationship-retracted`.

`merge` and `split` create new successor logical identities rather than
collapsing old IDs or moving historical occurrences. This keeps old exports,
readiness evaluations, review events, and citations truthful.

## Typed participant identity

### Identity is type- and scope-specific

Each analyzer declares the participant types it can identify and the evidence
that establishes equality across snapshots. Example participant classes are:

| Participant class | Canonical identity input | Required caution |
|---|---|---|
| Local installed entity | Snapshot-local entity ID plus a validated cross-snapshot correspondence edge | MO2 folder name, Nexus mod ID, or displayed version alone is not identity |
| Plugin | Qualified plugin identity plus captured content/provider state where the condition is byte-dependent | Filename alone is not sufficient across rename/replacement |
| Plugin record/locus | Qualified record identity, record type, origin namespace, and supported field/link locus | Numeric form ID without origin/plugin context can collide |
| Effective path | Qualified namespace, canonical comparator version, normalized logical path, and typed winning/losing provider roles | Same path with a different provider chain may be a different cause |
| Archive member | Container identity, member identity, namespace/path, and provider role | Container and member dependencies remain separate |
| Configuration value | Qualified document/schema identity, key/section path, effective value source, and comparator semantics | Textual key similarity does not establish same effective setting |
| Runtime/native component | Qualified component/product identity plus content/version evidence required by its analyzer | Display version and filename may be weak or contradictory |
| Generated output | Qualified generator/output family, logical output identity, and declared input/dependency set | Output filename alone does not reconstruct generator history |
| External claim | Source entity/version, claim revision, and applicability conditions | A changed claim may revise evidence without changing local cause |
| Assumption/context input | Exact versioned typed input and context applicability | Notes and informal text are not assumptions automatically |
| Abstract game feature | Analyzer-defined stable feature identity backed by supported semantic edges | Taxonomy area is not a substitute for a feature identity |

The table is a participant vocabulary, not a list of mod or affected-game-area
categories.

### Roles are part of identity

The same participant can appear in different causal roles. A canonical
participant tuple therefore includes:

```text
participant_type
canonical_subject_id
role
scope
correspondence_or_identity_evidence_refs[]
comparator_id_and_version
```

Illustrative generic roles include `affected-subject`, `required-input`,
`effective-winner`, `overridden-provider`, `consumer`, `producer`,
`documented-intent-subject`, and `causal-intermediary`.

Role inversion is not an equivalent signature merely because the participant
set is unchanged.

## Finding continuity signature

### Components

A finding continuity signature should retain, in canonical structured form:

1. **condition family** — the analyzer-declared kind of analytical condition,
   independent of its prose title;
2. **causal pattern** — typed cause/relationship edges and participant roles;
3. **affected locus or analytical question** — the exact supported record
   field/link, path/provider relationship, configuration relationship,
   runtime component relationship, generated output relationship, or other
   declared subject;
4. **applicability predicate set** — the local runtime, versions, enabled
   state, options, assumptions, claim conditions, or context inputs material to
   whether the condition applies;
5. **dependency schema and resolved dependency-state proof** — the smallest
   complete closure under ADR-0010;
6. **producer semantic compatibility** — analyzer family, semantic contract,
   identity-contract, canonicalization, and comparator versions; and
7. **identity evidence** — the observations and correspondence edges proving
   every cross-snapshot participant mapping.

Severity, confidence, symptoms, prose, recommendation, disposition,
suppression, and current taxonomy presentation are deliberately excluded.
They can change while the same underlying condition is re-evaluated.

### Fingerprint

The canonical signature may be hashed to create an indexed
`continuity_fingerprint`. The stored canonical components remain authoritative
and inspectable.

The fingerprint:

- is namespaced by the finding identity-contract version;
- is never the logical finding ID;
- cannot auto-merge across contract versions without a declared compatibility
  mapping;
- has collision handling that compares canonical components before admission;
  and
- may change after a better canonicalization algorithm without rewriting the
  older occurrence.

An exact fingerprint match generates a strong candidate. It does not bypass
the three equivalence gates.

## Case continuity signature

A case cannot be identified by the unordered set of member finding IDs. Case
membership may grow or shrink while the shared cause remains the same, and two
causes can initially produce similar members.

The case signature therefore contains:

```text
case_kind: supported | lead-only
shared_cause_pattern
causal_participants_and_roles[]
applicability_predicates[]
shared_dependency_closure_ref
cause_evidence_refs[]
grouping_contract_id_and_version
```

Member occurrence identities, likely resolution, and symptoms remain
supporting comparison features rather than identity authority.

Case continuity requires:

- a supported equivalent shared cause, not only member overlap;
- equivalent material applicability;
- equivalent shared-cause dependencies;
- a compatible grouping contract; and
- no unresolved evidence that the group actually contains distinct causes.

A lead-only case can continue through additional hypotheses. Promotion to a
supported case creates a new case occurrence and `promotes-lead` lineage. It
does not relabel the prior occurrence or silently convert every hypothesis
into a finding.

## Equivalence and relationship rules

### Causal equivalence

Causal equivalence means the analyzer can support that two occurrences address
the same underlying condition or shared cause, with equivalent participant
roles and relationship shape.

It is not established by:

- equal titles or generated summaries;
- the same named mods;
- the same taxonomy classifications;
- the same symptom;
- member overlap;
- a similar remediation;
- proximity in time; or
- a model's unsupported “same issue” judgment.

An LLM may propose a reconciliation candidate from bounded evidence, but the
host must validate its typed participants, cited evidence, and declared
identity contract before admission.

### Applicability equivalence

Applicability equivalence compares only predicates material to the condition.
An unrelated change does not break continuity. A changed runtime, version,
feature selection, identity mapping, user intent assumption, or external-claim
condition does break it when the analyzer declares that input material.

The applicability proof records both:

- the predicates compared; and
- why other changed context was outside the condition's declared closure.

A broader or narrower applicability scope is not silently treated as
equivalent. It may support an explicit related or supersession relationship,
but it needs new review state.

### Dependency equivalence

Dependency equivalence uses the producing artifact's complete declared closure,
not the whole-snapshot ID and not a convenient subset discovered after the
result.

It requires:

- the same dependency types and semantic roles;
- content or structural identity at the assurance level each dependency
  requires;
- compatible parser, canonicalization, analyzer, and ruleset semantics; and
- an explicit comparison result for every declared dependency.

Missing, inaccessible, unsupported, or unretained dependencies produce
`unknown` or `unsupported`, never presumed equality.

### Analytical revision under continuity

Equivalent cause, applicability, and dependencies can still yield a revised
conclusion because:

- retained resolved evidence was newly interpreted, contradicted, or
  adjudicated without changing the identity/dependency inputs;
- an analyzer/prompt/model was rerun under a declared compatible semantic
  contract;
- a nondeterministic producer emitted a materially different admissible result
  from the same resolved inputs; or
- an earlier analytical conclusion was rejected after a review that supplied
  no changed analysis-affecting input.

The new occurrence may retain the logical identity and explicitly supersede
the prior analytical occurrence. Its conclusion, confidence, evidence, and
recommendations remain immutable within each run.

If the condition no longer meets finding admission, the new hypothesis,
abstention, or coverage result remains its own typed object and may
`supersede` or `contradict` the old finding through lineage. It is not stored
as a fabricated “resolved finding.”

A new source revision, corrected source extraction, changed semantic threshold,
or other changed analysis-affecting input must first pass the declared
applicability/dependency-equivalence contract. When it does not, the output is
an explicit related successor/follow-up with fresh review state, not an
`analytical-revision` admitted by resemblance.

### Material local change

When a material dependency or applicability predicate changes—such as a
different effective winner, provider chain, required component, local option,
or semantic assumption—the earlier finding remains historical. The new
analysis may be explicitly related as a remediation follow-up or successor,
but it does not qualify for dependency-validated identity or review-state
carryover.

This distinction prevents “the user changed the thing the finding was about”
from being mislabeled as “the finding was unchanged.”

## Reconciliation workflow

### Step 1 — finalize immutable new outputs

Reconciliation operates on committed run outputs. It cannot change the run's
findings, case grouping, evidence, or resolved inputs.

### Step 2 — construct versioned signatures

Each supported analyzer constructs finding and case signatures from its
declared identity contract and dependency closure. Unsupported participant
types or missing material dependencies produce an explicit identity gap.

### Step 3 — retrieve bounded candidates

Candidate retrieval uses:

- exact continuity fingerprints;
- compatible prior identity-contract versions;
- typed affected-locus and participant-role indexes;
- explicit reuse/supersession/validation links; and
- case shared-cause indexes.

This stage may over-select. It must not merge.

### Step 4 — evaluate independent gates

For each candidate, the reconciler records:

- causal equivalence;
- applicability equivalence;
- dependency equivalence;
- producer/identity-contract compatibility;
- supporting and contradicting evidence; and
- missing information.

### Step 5 — classify the outcome

| Outcome | Minimum condition | Durable effect |
|---|---|---|
| `exact-continuation` | Unique one-to-one match; all gates proven equivalent; materially equivalent analytical conclusion | Add new occurrence membership to existing logical identity |
| `analytical-revision` | Unique one-to-one match; all identity gates proven; conclusion/evidence/confidence differs under continuity | Add occurrence and explicit supersession/revision edge |
| `related-follow-up` | Explicit causal relationship but material dependency or applicability changed | Keep separate logical identity and add typed relationship |
| `new-distinct` | Cause or applicability proven different | Create a new logical identity |
| `ambiguous` | Several plausible predecessors or unresolved contradictory identity evidence | Keep separate/unassigned and request review |
| `unknown` | Required identity/dependency evidence unavailable or unsupported | Keep separate/unassigned and expose the gap |
| `merge-proposed` | Several prior identities may share one newly supported cause | No merge until reviewed or independently proven under declared policy |
| `split-proposed` | One prior case/finding identity may contain materially distinct causes | No split until reviewed or independently proven under declared policy |
| `not-observed` | Prior population was examined but no equivalent new finding occurred | Record coverage-relative observation; do not mark resolved |
| `not-evaluated` | Relevant population/analyzer was skipped, failed, or unsupported | Preserve history and expose coverage gap |

Automatic acceptance should initially be limited to unique one-to-one
`exact-continuation` and `analytical-revision` results with complete proofs.
Thresholded fuzzy matches remain proposals, even if their similarity score is
high.

### Step 6 — reconcile cases after findings

Case reconciliation uses the shared-cause contract after finding reconciliation
is available. Finding continuity can support—but cannot replace—the case
cause proof. A case is not automatically split because one member disappears,
or merged because memberships overlap.

### Step 7 — evaluate review-state carryover separately

Identity continuity does not itself carry a disposition or suppression.
Carryover performs the stricter review-state checks below and creates its own
event.

### Step 8 — build disposable current projections

Current views may show:

- preferred logical identity and current occurrence;
- pending ambiguity, merge, or split review;
- stale or unavailable proof;
- inherited review state and its source;
- related earlier/follow-up conditions; and
- complete historical lineage.

These are rebuildable projections. They are not allowed to become the only
record of reconciliation.

## Disposition and suppression carryover

### Carryover record

Every carryover is explicit:

```text
carryover_event_id
source_review_event_id
source_finding_occurrence_id
target_finding_occurrence_id
reconciliation_assessment_id
applicability_scope
dependency_validation_ref
carryover_kind: disposition | suppression | annotation-reference
actor_or_policy
created_at
reason
revoked_or_superseded_by?
```

The target does not receive a copied field that hides the source decision.

### Eligibility

Carryover requires:

- accepted one-to-one continuity;
- unchanged material finding conclusion for the review decision's purpose;
- proven applicability and dependency equivalence;
- source review state that is still retained and valid;
- no unresolved contradiction affecting the decision; and
- a carryover policy version retained with the event.

The same proof rule applies to all dispositions. `resolved`,
`accepted-as-is`, `not-applicable`, and `false-positive` must not receive a
weaker shortcut merely because carrying them reduces the queue.

If the conclusion, material evidence, affected locus, cause, applicability, or
dependency state changes, the target is unreviewed by default. The prior
decision remains visible as history.

### Suppression

Suppression carries only across a materially equivalent finding occurrence
with the same strict proof. Any changed, ambiguous, split, merged, or
reidentified occurrence is visible by default.

Suppression never carries through case identity alone.

### Annotations

Annotations are not duplicated silently:

- an annotation bound to a finding occurrence remains attached to that exact
  occurrence;
- an equivalent successor may show it as an inherited historical reference
  with the source occurrence and author visible;
- copying or retargeting it creates an explicit annotation revision/carryover
  event;
- an annotation intentionally authored at logical-case scope remains a
  logical-case annotation, with its creation time and scope visible; and
- if note content should affect analysis, the user must convert it into a typed
  assumption, applicability decision, symptom report, or evidence item.

### Cases

Case dispositions are not canonical. A case-level action records one review
event for each selected member finding. Merge or split never copies one
member's disposition to another finding.

## Manual reconciliation and correction

### User input can supply evidence, not waive the gate

A user may:

- confirm or reject a proposed participant correspondence;
- supply a typed identity or applicability fact;
- select among ambiguous predecessor candidates;
- approve a supported merge or split;
- mark occurrences related but distinct;
- retract an earlier reconciliation; and
- explain the decision.

A bare “treat as the same anyway” action cannot be represented as
dependency-validated continuity. If the user knowingly wants a presentation
association despite unresolved identity, store `related-by-user`, not
`proven-equivalent`.

### Merge

When two or more prior logical identities are shown to describe one cause:

1. preserve each old logical identity and all occurrences;
2. create a new successor logical identity;
3. record a `merge` lineage event with the supporting assessments;
4. attach future/newly reviewed occurrences to the successor; and
5. keep review state on exact finding occurrences unless separately carried.

### Split

When one prior logical identity is shown to contain distinct causes:

1. preserve the old identity and its historical membership;
2. create two or more successor identities;
3. record a `split` event and the allocation of current/future occurrences;
4. do not rewrite old exports or readiness evaluations; and
5. require fresh review state for successor findings unless exact
   occurrence-level carryover is independently proven.

### Correcting a false merge or false split

Correction creates a new lineage event that supersedes the prior identity
decision. It does not delete the bad decision. Current projections follow the
latest applicable reviewed event while historical views explain both.

## Version change behavior

### Taxonomy versions

Taxonomy assignments are excluded from causal identity. A taxonomy change:

- never merges or splits a finding/case by itself;
- creates linked reclassification projections where required;
- retains original assignment versions on historical occurrences; and
- triggers identity review only if it accompanies a real change to the
  analyzer's causal or applicability semantics.

### Analyzer and ruleset versions

Each analyzer publishes:

- stable analyzer-family identity;
- semantic contract version;
- identity-contract version;
- supported participant/comparator versions;
- declared compatibility with earlier versions; and
- any migration/adjudication needed when a contract changes.

A patch/minor/major version label alone does not prove compatibility.
Compatibility is an explicit, tested declaration over the relevant output and
dependency contract.

An analyzer upgrade with no declared identity compatibility produces
unreconciled candidates. It may be reviewed or recomputed; it does not auto
split every history item or auto merge by title.

### Identity-contract versions

When canonicalization or signature shape changes:

- historical signatures remain stored under their original version;
- a new comparison projection may derive a candidate signature from retained
  historical inputs;
- the derivation records its algorithm/version and source inputs;
- accepted mappings create reconciliation assessments; and
- the old signature and logical membership are not overwritten.

## Contradiction, stale state, and absence

### Contradicting evidence

Contradiction does not automatically mean a different identity. If the
underlying cause, applicability, and declared dependencies remain equivalent,
a contradictory interpretation or producer output over the same resolved
evidence can produce an `analytical-revision`, abstention, rejected conclusion,
or hypothesis under the same continuity. Contradiction introduced by a changed
source or other material input instead requires a new dependency assessment and
does not inherit continuity or review state automatically.

The contradiction remains typed and visible. Reconciliation must not resolve
the evidentiary dispute merely to simplify history.

### Stale history

A historical finding can be:

- internally valid for its originating run;
- stale relative to a current snapshot/context;
- related to a newer occurrence; and
- ineligible for current disposition carryover

at the same time. “Stale” is a current applicability projection, not mutation
or deletion of the historical finding.

### Missing or unavailable proof

If source bytes, dependency inputs, analyzer compatibility declarations, or
participant correspondence evidence are unavailable:

- continuity state is `unknown` or `unsupported`;
- review state does not carry;
- the current item remains visible;
- the missing input is exposed as an identity/replay/audit gap; and
- the system may offer recomputation or typed user input where that can
  establish the missing fact.

If the proof became unavailable through an explicit retention/deletion action,
previously accepted reconciliation and carryover events remain immutable
historical decisions, but their lost inspectability is recorded as an audit
gap. They do not authorize new reconciliation or carryover after deletion.
Deletion preview must report that consequence before the proof is removed.

### A finding is not emitted in a later run

Absence is not an implicit “resolved” finding and does not close the prior
case. The system distinguishes:

- the relevant population was not evaluated;
- evaluation failed or was unsupported;
- inputs changed materially;
- equivalent inputs were evaluated and the analyzer abstained;
- equivalent inputs were evaluated and produced contradicting/non-finding
  output; and
- a validation step independently supported resolution.

Each is a continuity/coverage/validation event linked to the old finding, not a
mutation of it.

## False-merge and false-split risks

| Shortcut | Likely failure | Required defense |
|---|---|---|
| Generated title or prose hash | Wording changes split one condition; generic wording merges distinct conditions | Exclude prose from identity |
| Mod names or Nexus IDs | Rename/reinstall splits; same mods can participate in multiple causes | Typed local identity and causal roles |
| Participant set only | Role reversal and distinct affected loci merge | Include role, locus, and causal edges |
| Taxonomy codes | Many unrelated causes share classifications; revisions alter codes | Use taxonomy only for routing/presentation |
| Case membership overlap | Membership evolves; broad cases absorb distinct causes | Identify shared cause independently |
| Whole-snapshot equality | Unrelated changes cause false splits; same snapshot can yield revised evidence | Compare complete artifact dependency closure |
| One similarity score | Missing applicability/dependency proof is hidden | Store independent gate states |
| Analyzer output key as permanent ID | Analyzer upgrade rewrites product identity | Versioned signature plus opaque logical ID |
| Automatically closing absent findings | Skipped/failed coverage looks like remediation | Record not-observed/not-evaluated explicitly |
| Logical-ID-level disposition field | Review leaks across changed revisions | Revision-bound events and explicit carryover |
| Destructive merge/split | Historical exports and readiness become false | Append-only successor identities and lineage |

## Alternatives considered

### 1. Deterministic hash as the logical ID — reject

This is simple and fast, but it turns canonicalization choices into permanent
product identity. Any versioned participant mapping, analyzer change, or
signature refinement either rewrites history or falsely splits it. Hashes
remain useful indexed fingerprints only.

### 2. Latest-row mutation — reject

Updating one “current finding” or “current case” loses run ownership,
contradictions, review applicability, export truth, and why a prior readiness
evaluation existed.

### 3. Name/mod/record-family fuzzy matching — reject as authority

These fields are useful candidate-retrieval hints. They cannot establish a
shared cause and invite both false merges and false splits. Any model-based
entity-resolution step stays lead-only until its typed evidence is validated.

### 4. Never reconcile; show every run independently — reject as the product
model

This preserves history but makes repeated scans unreviewable, prevents valid
review-state persistence, and cannot express remediation follow-up. The
product still needs conservative continuity.

### 5. Opaque logical IDs plus evidence-bearing reconciliation — recommend

This separates history from current grouping, supports correction, and makes
uncertainty inspectable. Its cost is additional data modeling, explicit
identity contracts per analyzer, and a review queue for ambiguous cases.

## Bounded M1 subset

M1 should implement the minimum durable substrate without claiming the full M2
review workflow.

### Required in M1

1. Opaque logical finding and case IDs.
2. Immutable run-specific finding and case occurrence IDs.
3. A versioned identity envelope for every M1 analyzer containing:
   - analyzer family and semantic/identity-contract versions;
   - typed participant identities and roles;
   - causal condition/shared-cause pattern;
   - affected locus or analytical question;
   - applicability predicates;
   - complete dependency-closure reference; and
   - inspectable canonical signature plus optional fingerprint.
4. Conservative unique one-to-one reconciliation for the exact participant and
   dependency types exercised by M1.
5. Explicit `exact-continuation`, `analytical-revision`,
   `related-follow-up`, `new-distinct`, `ambiguous`, `unknown`,
   `not-observed`, and `not-evaluated` outcomes.
6. Append-only reconciliation and lineage records; the schema must support
   later merge/split successors even if M1 exposes no interactive editor.
7. Human-readable CLI and JSON output showing the identity decision, reason,
   missing proof, origin run, and lineage.
8. No implicit review-state carryover. If M1 includes dispositions at all, it
   must use the revision-bound event and validated-carryover contract.

### Deferred to M2 or later

- interactive ambiguity review;
- merge/split/correction UI;
- cross-analyzer identity adjudication beyond declared compatible contracts;
- broad participant identity support outside delivered analyzer surfaces;
- user-facing lineage visualization;
- historical bulk cleanup;
- learned/fuzzy reconciliation ranking; and
- automatic annotation retargeting.

Deferral does not permit a destructive latest-row schema or content-derived
logical ID in M1.

## Evaluation mapping

### EVAL-0079 staged specification inputs

[EVAL-0079](../../evaluation/case-catalog.md#evaluation-case-catalog) should
cover the M1 identity/reconciliation substrate with generic synthetic positive,
negative, boundary, and noninteractive lineage cases. Interactive ambiguity,
reviewed merge/split, bulk review, and correction workflow are M2 extensions:

1. **Exact continuation:** equivalent typed cause/applicability/dependencies
   across two runs produces one logical lineage and a traceable carryover.
2. **Unrelated change:** a snapshot change outside the dependency closure does
   not split the finding.
3. **Display rename:** changed mod/folder/display names with proven local
   entity correspondence do not split identity.
4. **Same names, distinct cause:** identical names/participants with a
   different affected locus or causal relationship remain separate.
5. **Changed applicability:** a material option, runtime, version, assumption,
   or claim condition prevents carryover.
6. **Changed dependency:** a different effective winner/provider/value creates
   a related follow-up or distinct condition and leaves it unreviewed/visible.
7. **New contradiction:** a contradictory output over equivalent resolved
   evidence creates an analytical revision without erasing the earlier
   conclusion; a changed source revision requires new dependency validation
   and no implicit carryover.
8. **Analyzer compatibility:** a declared compatible analyzer update may
   reconcile; an undeclared/incompatible update remains ambiguous or separate.
9. **Taxonomy change:** reclassification preserves identity and historical
   assignment versions.
10. **Case membership change:** added/removed symptom findings do not split a
    case when shared cause remains proven.
11. **False merge defense:** overlapping participants with distinct causes do
    not merge.
12. **Reviewed merge (M2 extension):** several earlier logical cases gain one
    successor identity while old history and review events remain intact.
13. **Reviewed split (M2 extension):** one earlier identity gains several
    successor identities without rewriting old exports/readiness.
14. **Missing evidence:** unavailable identity/dependency proof produces
    unknown and no state carryover.
15. **Absent output:** skipped, failed, abstained, not-observed, and validated
    resolution remain distinguishable.
16. **Suppression leakage:** a materially changed successor is visible by
    default.
17. **Case bulk action (M2 extension):** member findings receive explicit
    individual review events; unrelated/new members do not inherit them.
18. **Metamorphic order:** reconciliation result is independent of unrelated
    insertion order and candidate ordering.

### Related cases

- EVAL-0013/EVAL-0014: dependency-aware invalidation and safe carryover;
- EVAL-0026: immutable run bindings and new-run behavior;
- EVAL-0037: validated reuse versus clean recomputation;
- EVAL-0069: per-finding disposition/readiness events;
- EVAL-0078: cross-snapshot change impact;
- EVAL-0083: end-to-end provenance;
- EVAL-0084: causal case grouping; and
- EVAL-0086: taxonomy/version separation.

### Metrics

Report separately:

- auto-reconciliation precision;
- auto-reconciliation coverage;
- false-merge rate;
- false-split rate;
- ambiguous/unknown rate by missing proof;
- review-state carryover precision;
- erroneous suppression carryover count;
- merge/split correction rate;
- percentage of decisions with complete causal, applicability, and dependency
  explanations; and
- latency and candidate volume at each profile scale.

Do not trade false-merge precision for recall through an opaque score. A
conservative ambiguous result is preferable to disposition leakage.

## Recommendation for an ADR

A Wave E ADR should accept:

1. immutable run occurrences separate from opaque logical finding/case IDs;
2. append-only reconciliation assessments and merge/split/supersession lineage;
3. versioned typed signatures as candidate keys, never identity authority;
4. separate causal, applicability, dependency, and producer-compatibility
   gates;
5. automatic reconciliation limited initially to unique fully proven
   one-to-one matches;
6. explicit related-follow-up, ambiguous, unknown, not-observed, and
   not-evaluated outcomes;
7. non-destructive successor identities for merges and splits;
8. exact-revision review state and explicit provenance-bearing carryover;
9. no suppression carryover after material change or uncertainty;
10. taxonomy exclusion from causal identity;
11. analyzer-published semantic and identity compatibility contracts; and
12. the bounded M1 subset above.

The ADR should leave the physical schema, ORM, and UI interaction design to the
storage/process/application-stack decisions and milestone plans.

## Confidence and remaining uncertainty

### Confidence

- **High** that run occurrence, logical identity, reconciliation decision, and
  review state must remain distinct.
- **High** that causal, applicability, and dependency equivalence must be
  independently inspectable and that display names/taxonomy cannot establish
  identity.
- **High** that merges/splits must be append-only successor lineage.
- **Medium-high** that unique fully proven one-to-one matching is the correct
  first automatic policy.
- **Medium** on the exact participant vocabulary and signature fields because
  each delivered analyzer must define its supported semantic identity.
- **Medium** on how much interactive reconciliation belongs in M2 versus M3;
  usability evidence may change sequencing without changing the durable model.

### Unresolved implementation details

- exact ID representation and canonical encoding;
- physical tables, indexes, and transaction boundaries;
- whether a logical finding may span compatible analyzer families or only one
  analyzer family initially;
- the reviewed compatibility declaration format;
- identity-gap and ambiguity UI;
- physical retention/deletion mechanics for identity proof, subject to the
  required historical-event, audit-gap, preview, and no-new-carryover behavior
  above;
- thresholds for offering fuzzy candidates for manual review; and
- measured candidate volume and reconciliation latency at high-end scale.

These do not block the logical ADR. They belong in the M1 plan, storage/process
ADRs, and evaluation specifications.

### Reopen the recommendation if

- a delivered analyzer cannot express its causal condition or dependency
  closure without prose-only identity;
- controlled evaluation shows that unique fully proven matching still creates
  false merges;
- the chosen persistence mechanism cannot retain append-only many-to-many
  lineage efficiently;
- cross-analyzer reconciliation becomes an M1 requirement; or
- user research shows that the distinction among continuity, related follow-up,
  and merge/split cannot be made understandable without a different domain
  contract.

## Decision provenance

This recommendation derives from:

- [FIND-002, FIND-006, and FIND-014](../../product/requirements.md#findings-cases-and-readiness);
- [SNAP-004](../../product/requirements.md#snap-004--safe-carryover);
- [OPS-002](../../product/requirements.md#ops-002--local-history);
- the accepted [domain model](../../product/domain-model.md);
- [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md);
- [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md);
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md);
- [RESEARCH-0012](RESEARCH-0012-snapshot-fingerprint-and-invalidation.md);
- [RESEARCH-0022](RESEARCH-0022-candidate-index-and-ranking.md);
- [RESEARCH-0036](RESEARCH-0036-evidence-persistence-and-versioning.md);
- the [evaluation strategy](../../evaluation/evaluation-strategy.md);
- the [fixture guidelines](../../evaluation/fixture-guidelines.md); and
- the accepted [anti-overfitting rules](../../evaluation/anti-overfitting-rules.md).

ADR-0022 accepts this research recommendation and resolves RQ-033 for M0.
Implementation and evaluation conformance remain pending.
