# M1 Slice 8: Controlled-real generalization

Status: Accepted

Disposition: Owner-accepted planning authority. Live implementation and local
input authorization are stated only in `docs/current-state.md`.

Last reviewed: 2026-08-24

Planning base: accepted Slice 7 owner-closeout commit
`b8fe038e7b19c68d1876a14e978b14b9d88a6b3e`

Depends on: the accepted M1 plan; the accepted process-continuation and
semantic-oracle-deferral amendments; accepted ADR-0001, ADR-0002, ADR-0008,
ADR-0009, ADR-0010, ADR-0015, ADR-0022, ADR-0028, ADR-0029, ADR-0034, and
ADR-0035; the M1/M2 product-conformance verification profile; and the
owner-accepted Slice 7 closeout and implementation record.

Owner plan acceptance: Accepted by the project owner on 2026-08-24 for exact
plan candidate `ab3f7ed2cf0d44067c96a7d88a44be4074486412`.

## 0. Plain-language outcome and authority

Slice 8 must show that the narrow analyzer accepted in Slice 7 still tells the
truth when it receives two small sets of real Skyrim mod data. Each set has a
positive arrangement that should expose a lost relationship and a matched
author patch that restores that relationship while preserving the intended
change. The controls matter as much as the positives: a mechanism that reports
both is recognizing filenames or change volume, not the intended causal
pattern.

The two required product-conformance cases are:

1. `EVAL-0016`: two actor records whose later appearance winner loses an
   established AI-package relationship, paired with the exact patch that
   preserves the appearance while restoring the package relationship; and
2. `EVAL-0017`: one placed reference whose later visual placement winner
   restores an earlier linked-reference relationship, paired with the exact
   patch that preserves the placement while restoring the intended link.

These are developer-owned controlled-real checks. They are not private held-
out evaluation, an independently authored answer key (a semantic oracle), or
proof that Infinium understands arbitrary mods. The inputs are used only
locally, by exact identity, after separate owner activation. No third-party
payload is committed or redistributed.

Plan acceptance approves this written scope only. Implementation may start
only after `docs/current-state.md` separately records:

- the owner's acceptance of this exact plan candidate;
- an exact implementation base descending from the planning base; and
- the work-package range and ordinary correction authority.

WP1 and WP2 may start under that implementation activation. Before WP3 reads a
controlled-real payload, an answer-free local input manifest and root for the
exact public `RESEARCH-0035` identities must validate and confirm that the root
contains only the authorized controlled-real dependencies, not evaluator-
private material, expected answers, credentials, or an archive/repository
being opened for archaeology. The owner may pre-approve that exact class of
read-only handoff in current project state; once a conforming handoff exists,
its operational validation does not require another owner decision.

That activation authorizes only local, read-only input consumption and
effect-free WP1-through-WP7 work below. It never authorizes network access,
providers, credentials, private fixtures, evaluator-private repositories,
archives, semantic-oracle work, external publication, push, or another
external effect.

## 1. Exact exit state

Slice 8 is complete only when one coherent accepted candidate demonstrates all
of the following:

- exact preflight admission of the two controlled-real packages and every
  upstream local product dependency, with byte length and SHA-256 identity;
- no enumeration or read outside the declared local root and no third-party
  payload copied into the repository or a shareable receipt;
- a clean-break additive `infinium.analysis.scope-reversion/v2` contract that
  preserves every frozen `v1` byte and can represent the two-actor cohort as
  one shared causal candidate, finding, and case;
- one controlled-real projector that consumes accepted Bethesda semantic
  snapshots and explicit source-purpose/application decisions rather than
  fixture labels or real-mod names;
- `EVAL-0016` and `EVAL-0017` positives with their structurally matched patch
  controls, including the specified residual and gap behavior;
- separate evidence for source passage support, exact local applicability,
  host admission, observed structure, predicted consequence, and established
  extent;
- taxonomy `infinium.mod-impact-taxonomy/0.1.0` applied by exact axis, facet,
  applicability, classification role, and evidence reference, with no cross-
  axis inference;
- exact provenance, coverage denominators, unsupported regions, abstention,
  and bounded claim language carried through JSON and human output;
- schema migration, canonical round trip, clean execution, retained-
  downstream replay, reopen, backup/restore, invalidation, and unavailable-
  dependency behavior;
- positive, negative, malformed, lifecycle, mutation, metamorphic, and cross-
  category evidence across all six product-conformance layers;
- append-only bookkeeping when a controlled-real result drives a product
  correction, without inventing a replacement held-out case or semantic
  oracle;
- one consolidated semantic, security, provenance, contract, persistence,
  and diff review, same-candidate corrections, and affected-surface re-review;
- the complete accepted verification floor passing once on the final review-
  ready candidate; and
- an implementation record and owner handoff stating the exact bounded claim,
  retained gaps, contract maturity, and absence of any current private or
  independent semantic verdict.

Owner acceptance of the final implementation is a later decision distinct
from plan acceptance and activation. Only final owner acceptance may move
Slice 8-owned producer-consumer-validated contracts to `Slice-frozen`.

## 2. Required reading for implementation and review

An implementation or review agent must begin with:

1. repository `AGENTS.md`;
2. `docs/README.md`, `docs/current-state.md`, and
   `docs/execution-policy.md`;
3. the accepted M1 plan and both accepted amendments named above;
4. the Slice 7 entry, accepted plan, and implementation record;
5. this Slice 8 entry and full plan;
6. the relevant accepted product requirements, domain model, analysis
   catalog, candidate-input/expansion, taxonomy, severity/confidence/coverage,
   and scope documents;
7. the accepted ADRs named above;
8. `evaluation-strategy.md`, `case-catalog.md`, `fixture-guidelines.md`,
   `anti-overfitting-rules.md`, `product-evaluator-boundary.md`, and the
   continuation verification profile; and
9. the tracked `RESEARCH-0035` investigation, public manifests, exact source
   passages in the semantic specification/catalog, and source registry.

The tracked research package is identity and source evidence, not permission
to obtain payloads or an answer key. Historical evaluator records, private
repositories, retired paths, external archives, and provider artifacts are not
Slice 8 inputs.

### 2.1 Requirement and case traceability

| Planned surface | Accepted requirement/case authority |
|---|---|
| exact local target, snapshot, context, configuration, and read-only admission | `SCOPE-001` through `SCOPE-006`, `AUTH-001` through `AUTH-003`, `SNAP-001`, `SNAP-002`, `SNAP-005`, `SNAP-006` |
| typed evidence, provenance, candidate/hypothesis, abstention, and bounded output | `EVID-001` through `EVID-007`, `EVAL-0083` |
| finding, shared case, severity/confidence, extent, recommendation, and continuity | `FIND-001` through `FIND-004`, `FIND-011`, `FIND-014`, `EVAL-0079`, `EVAL-0084`, `EVAL-0086` |
| semantic reversion and materially different domain | `ANALYSIS-003` through `ANALYSIS-005`, `ANALYSIS-016`, `ANALYSIS-017`, `EVAL-0016`, `EVAL-0017`, `EVAL-0065` |
| purpose/source retention and support/application/admission separation | `DOC-002`, local installed-entity portion of `DOC-003`, local/fixture portions of `DOC-006` through `DOC-008`, `DOC-011`, `EVAL-0067` |
| truthful coverage and no safety implication | `PROD-002`, `PROD-004`, `COVER-001` through `COVER-003`, `EVAL-0085` |
| lifecycle, persistence, exact replay, and invalidation | M1-bounded `SCAN-001`, `SCAN-002`, `SCAN-004` through `SCAN-007`, `SCAN-009`, `OPS-002`, `OPS-003`, `EVAL-0026`, `EVAL-0037` through `EVAL-0040`, `EVAL-0087`, `EVAL-0088` |
| security, sensitive traces, and prohibited operation boundaries | `SEC-001` through `SEC-004`, `EVAL-0033` through `EVAL-0035`, `EVAL-0046`, `EVAL-0080`, `EVAL-0089` |

Only the M1-bounded portions identified by the milestone plan are in scope.
This table does not pull M2/M3 UX, scale, runtime, maturity, or distribution
requirements into Slice 8.

## 3. In scope and excluded

### 3.1 In scope

- deterministic local projection from admitted Bethesda semantic snapshots,
  admitted source-purpose/application facts, and exact controlled-real byte
  identities;
- the exact selected records, fields, plugin order, loose files, and matched
  patch controls declared by the tracked `RESEARCH-0035` manifests;
- an additive v2 contract only where the frozen v1 shape cannot represent the
  accepted controlled-real meaning;
- generic actor-cohort and placed-reference adapters with no production rule
  keyed to a mod name, filename, form ID, fixture ID, or expected result;
- canonical product taxonomy projections, bounded consequence/extent claims,
  truthful gaps, coverage, persistence, replay, JSON, and CLI output;
- developer-owned conformance fixtures derived independently from accepted
  product meaning; and
- local documentation, verification, review, correction, and owner handoff.

### 3.2 Excluded

- any read from `infinium-evaluator-fixtures`, an evaluator-private repository,
  a retired evaluator path, or an archive;
- authoring, sealing, registering, comparing, scoring, repairing, replacing,
  or reporting a semantic oracle or private verdict;
- network, model/provider, hosted search, Nexus, LOOT, credentials, billing,
  download, telemetry, push, publication, or another external effect;
- redistribution or repository storage of third-party plugins, archives,
  assets, official game masters, or answer-bearing derived data;
- source acquisition or reinterpretation: Slice 8 consumes only the exact
  tracked passages and already accepted local four-axis product boundary;
- runtime execution, save-state inspection, rendering validation, gameplay
  observation, quest/global/progression safety, archive-wide visual
  completeness, or patch-wide correctness;
- general rules about the real mod names, selected form IDs, file paths, or
  patch naming conventions;
- claims of precision, recall, broad compatibility, safety, readiness, or
  general Skyrim understanding; and
- Slice 9 design or execution beyond the predecessor handoff in section 15.

## 4. Frozen Slice 7 boundary and the additive v2 decision

Slice 7 froze the following together:

- `infinium.analysis.scope-reversion/v1` JSON schema and codec;
- analyzer family `infinium.scope-reversion`, analyzer ID
  `infinium.scope-reversion.local`, and all four `1.0.0` version axes;
- declaration fingerprint
  `7b1f83420402668a89ba370bf17ed4e0df6aeb5512472e79639c92bd8b258110`;
- storage `1.9.0`, schema 10, and schema fingerprint
  `d1a334b43b1c681c454971cb27554c4e2b38d4b2148ad8e78059fb6c26374381`;
- v1 actor and placed-reference adapters, invariants, canonical identities,
  claim boundary, renderer, public synthetic reader, and replay behavior; and
- every earlier Slice 5 and Slice 6 frozen family named by their accepted
  plans and records.

None may be edited in place, weakened, or assigned new meaning.

The v1 shape is deliberately one member to one finding/case and exactly four
coarse conclusion-taxonomy facts. `EVAL-0016` instead requires two actor
records to remain separately evidenced while producing one shared-cause
candidate, finding, and case, and both controlled-real cases require links to
the fuller accepted taxonomy projections and four-axis purpose/applicability
evidence. Encoding the actor pair as one fictional actor, emitting two cases,
or overloading the four v1 strings would be false.

WP1 therefore creates a clean-break additive
`infinium.analysis.scope-reversion/v2` contract. It must:

- retain the analyzer family and category-neutral causal rule;
- version the analyzer, semantic contract, and identity contract to `2.0.0`,
  with a new canonical declaration and fingerprint;
- retain ruleset `1.0.0` because the category-neutral causal decision rule is
  unchanged; the four version axes remain independent, and implementation may
  bump the ruleset only if accepted product meaning actually changes;
- retain v1 codecs, declarations, stores, readers, and tests unchanged;
- add an explicit subject kind and ordered subject-member references so an
  actor cohort is real data, not a disguised single actor;
- permit multiple observed member transitions to support exactly one causal
  candidate/hypothesis/finding/case when they have the same closed dependency
  cause;
- reference exact upstream Bethesda taxonomy assignments rather than copying
  or compressing them into fixture-specific codes;
- retain separate support, applicability, contradiction, causal closure,
  coverage, gap, evidence, and publication states;
- use canonical typed identities and reject dangling, duplicate, unsorted,
  mismatched, or cross-run references; and
- publish a v2 claim boundary no broader than the v1 boundary plus the exact
  controlled-real cohort/control qualification.

Because v2 adds a persisted product payload and indexes, WP4 introduces
storage `1.10.0` and schema 11 as an append-only successor. Schema 11 must
migrate from the exact schema-10 source fingerprint, leave all schema-10 rows
and v1 reads intact, and use separate v2 storage identities. This plan does not
authorize compatibility aliases or mutation of frozen rows.

Contract maturity proceeds `Proposed` -> `Implementation-active` ->
`Producer-consumer-validated` -> `Slice-frozen`. Implementation evidence may
correct the v2 proposal cleanly across all affected seams; intermediate bytes
are not frozen. A correction that changes accepted product meaning or weakens
a frozen predecessor requires owner decision.

## 5. Controlled-real input authority and admission

### 5.1 Tracked, answer-free identity authority

The following tracked public files are the only case-identity authority:

| File | Bytes | SHA-256 |
|---|---:|---|
| `gate-c-case-manifests.json` | 10,699 | `2ab135d50adb533e533918de2b5c42f3642348c3234432d6750f073ba68e4d15` |
| `eval-0016-independent-byte-map.json` | 45,038 | `e5a1ff7cbe1ff1db84331769b426df333cd442c1ff5b522c7959e08a09a16130` |
| `eval-0017-independent-byte-map.json` | 10,504 | `9dee14a525fa4aac751c946a87ba2a567f03d0e362dd4b68386f79b69b7b5cb9` |

They identify files and bounded observations. They do not supply a semantic
verdict and must not be extended by path discovery or historical names.

### 5.2 Pre-WP3 local handoff

Before WP3 consumes controlled-real bytes, implementation must receive one
untracked answer-free manifest with:

- a manifest schema/version and stable handoff ID;
- one explicitly resolved local root;
- an allowlist of relative paths, byte lengths, and SHA-256 values matching the
  tracked manifests exactly;
- a role for each entry: official master, positive plugin/asset, matched patch
  control, or required extraction dependency;
- `expected_result`, `oracle`, `verdict`, and private-repository fields absent;
- a statement that the files may be read locally for this bounded product-
  conformance task and must not be copied or redistributed; and
- owner read authorization recorded in `docs/current-state.md` by exact handoff
  and manifest fingerprint, without embedding credentials or secret material.
  Git binds reviewed bytes but a branch, commit subject, or manifest's mere
  presence never grants runtime read authority.

The implementation must resolve the root once, reject reparse/symlink escape,
open only allowlisted files, recompute length/hash from the opened handle, and
reject missing, extra-declared, changed, duplicate, case-colliding, or wrong-
role entries before semantic projection. It must not recursively enumerate the
root, search adjacent directories, derive a download location, or repair input.

The root is an external replay dependency. Product storage retains its exact
identity and permitted derived structural facts, not the third-party payloads.
Clean replay requires the exact root; retained-downstream replay may use
persisted project-authored structural artifacts. If the root later disappears,
the run remains auditable but clean replay is `Unavailable`; it must never be
reported as reproduced.

If the exact handoff is unavailable, no substitute is allowed. Only the
affected WP3-and-later controlled-real path stops for the owner-controlled
dependency; WP1, WP2, and independent hermetic product work may continue. When
current project state pre-approves the conforming handoff class, successful
validation is an operational gate rather than another owner-acceptance gate.

## 6. Purpose, applicability, and host-admission evidence

Purpose means what a source says the mod/change is trying to do. Applicability
means whether that supported statement is relevant to the exact local version,
record, field, and load-order context. These are separate from the analyzer's
final decision.

For every declared purpose dimension, the v2 projector must bind:

1. the tracked source registry ID, source revision, passage ID, exact retained
   passage bytes/fingerprint, retrieval date already recorded by research, and
   public manifest identity;
2. the developer-reviewed source-support state (`Supported`, `Unsupported`,
   `Contradicted`, or `Unavailable`) and evidence references;
3. the local application decision (`Applicable`, `ConditionalUnestablished`,
   `NotApplicable`, `Unknown`, or `NotEvaluated`) bound to exact neutral fact
   bytes, version identity, subject(s), fields, and load-order context; and
4. the host admission/abstention decision used by the analyzer.

No source acquisition, provider call, model inference, filename inference, or
proposal-as-fact shortcut is permitted. Support does not imply applicability;
applicability does not imply admission; and admission does not determine the
observed plugin structure.

The accepted purpose/applicability facts are:

- `EVAL-0016`: the appearance winner is supported/applicable for the bounded
  appearance/identity change; the prior overhaul is supported/applicable for
  the selected AI-package relation. The control patch is established from its
  exact decoded relation, not its name.
- `EVAL-0017`: the visual winner is supported/applicable for the selected
  placement change; the earlier feature source is supported/applicable for the
  selected link relation. The control patch is established from exact `XLKR`
  and `DATA`, not its title.

Removing or changing a required passage, version predicate, subject binding,
or fact fingerprint must make applicability unknown/unsupported or cause
abstention. It must never fall back to a filename or fixture label.

## 7. Taxonomy and claim limits

Slice 8 uses `infinium.mod-impact-taxonomy/0.1.0`. Every assignment records the
taxonomy ID/version, subject, axis, facet, code or explicit null, applicability,
classification role, evidence fields, responsible analyzer/adjudicator, and
reason. Axis assignments are independent.

### 7.1 EVAL-0016 minimum assignments

- declared purpose/target: `purpose.replace-overhaul` and
  `purpose-target.actors.appearance-identity` for the later appearance change;
  `purpose.modify-tune` and `purpose-target.actors.ai-packages` for the prior
  overhaul relationship;
- observed surface/delivery: `surface.plugin-data`, `surface.asset`,
  `delivery.plugin-container`, and `delivery.loose-data-file` for the exact
  loose FaceGen data present in the admitted root;
- established area/locus: `area.actors.appearance-identity` and
  `area.actors.ai-packages` for the two selected actors only;
- predicted consequence: `consequence.incorrect-functional-behavior`; runtime
  actor scheduling is a prediction, not an observation;
- established extent: `extent.subject.bounded-set`,
  `extent.spatial.nonspatial`,
  `extent.persistence.installation-persistent`, and
  `extent.propagation.bounded-dependents`; and
- explicit gaps: residual `AIDT`, archive/rendered appearance, runtime behavior,
  other actors/fields/packages, and patch-wide correctness.

### 7.2 EVAL-0017 minimum assignments

- declared purpose/target: `purpose.modify-tune` and `purpose-target.world`
  for the rental feature; `purpose.replace-overhaul` and
  `purpose-target.presentation.visual` for visual placement;
- observed surface/delivery: `surface.plugin-data` and
  `delivery.plugin-container`;
- established area/locus: `area.world.cells-worldspaces-locations` and
  `area.world.placed-objects-activation` for the one selected reference;
- predicted consequence: `consequence.incorrect-functional-behavior` and only
  `area.gameplay.items-inventory-economy` where the retained rental passage
  supports it;
- predicted extent: `extent.subject.single-instance`,
  `extent.spatial.cell-or-location`,
  `extent.persistence.installation-persistent`, and
  `extent.propagation.bounded-dependents`; and
- explicit gaps: runtime rental behavior, quest/global state, navmesh,
  rendering, other references/fields, and patch-wide safety.

### 7.3 Negative-control rules

A resolved patch control retains the observed change, purpose, applicability,
taxonomy, provenance, and coverage facts. It has no scope-reversion finding,
case, severity, confidence, consequence conclusion, or remediation for the
restored relation. `EVAL-0016`'s residual `AIDT` difference remains an observed
fact and explicit gap; it is neither suppressed nor adjudicated. A negative is
not a safety claim.

Unknown, unsupported, unmapped, and not-applicable are distinct. Missing facts
produce a gap or abstention, never an inferred harmless state. A later-layer
failure must not erase a lower-layer fact already established by exact bytes.

## 8. Exact case semantics

### 8.1 EVAL-0016 actor positive and control

The exact case is `REAL-NPC-0001`: AI Overhaul `1.8.6`, Children of the Pariah
`1.2.3.6`, and the author-supplied package-specific control, limited to
`0001339A:Skyrim.esm` and `0001AA63:Skyrim.esm`. The positive input uses only
those canonical actor identities and the declared appearance, package, loose
FaceGen, load-order, and dependency facts. Master-index translation must
resolve to canonical source identity. Both actor observations bind one closed
dependency cause and therefore produce:

- two separately provenance-linked observed members;
- one mandatory actor-cohort candidate;
- one present hypothesis;
- exactly one `Strongly supported`, `Moderate` finding;
- exactly one shared logical case;
- one bounded recommendation to preserve the appearance change while restoring
  the established package relation; and
- explicit runtime, archive, visual, residual-field, and completeness gaps.

The matched control differs only by the exact author-patch relation needed for
the selected package relationship. It produces one resolved-negative cohort,
zero finding, zero case, and zero remediation for that relation. Its residual
`AIDT` difference and every other gap remain visible.

### 8.2 EVAL-0017 reference positive and control

The exact case is `REAL-REFR-0001`: Candlehearth `1.1.1`, Nightgate Inn Revived
`1.3`, and their author patch, limited to `00017061:Skyrim.esm`. The positive
uses only that canonical reference, `DATA` placement, `XLKR` target, load order,
and dependency facts. Link targets must be resolved canonically. It produces
one mandatory candidate, one present hypothesis, exactly one
`Strongly supported`, `Moderate` finding/case, a bounded predicted rental
symptom, and a recommendation to preserve placement while restoring the
established link.

The matched control differs only by the merged author-patch link relationship.
It produces one resolved negative, zero finding, zero case, and zero
remediation. No actor grouping rule may participate.

### 8.3 Generalization claim

Passing both cases establishes only that one category-neutral causal rule
survived two materially different supported domains under exact controlled-real
conditions. It does not establish broad mod compatibility, patch safety,
runtime correctness, production readiness, precision, recall, or future-case
performance.

## 9. Provenance, persistence, and replay

### 9.1 Required provenance

Every v2 assignment, output, and receipt must bind:

- run, snapshot, configuration, context, and execution-input identity;
- v2 analyzer declaration and all four version axes;
- taxonomy ID/version and exact upstream taxonomy assignment IDs;
- exact tracked public manifest paths, bytes, and SHA-256 values;
- activation-time local manifest identity and every consumed dependency's
  relative path, role, length, and SHA-256 without retaining its payload;
- Bethesda extractor/projector identity and version, Mutagen `0.54.2`, official
  master/plugin order, canonical record/link identities, selected fields, and
  loose-asset identities;
- source registry/revision/passage and support/application/admission decision
  identities;
- positive/control pair identity and exact relation delta;
- every evidence, gap, coverage population, dependency edge, finding, case,
  and recommendation identity; and
- conformance partition and append-only transition history.

Human output may show approved source/mod display names for traceability, but
generic production decisions must not branch on them. Shareable receipts must
not contain third-party payload bytes, absolute local roots, usernames,
credentials, or expected-answer material.

### 9.2 Schema 11 and storage 1.10.0

The migration and store must provide:

- exact schema-10 predecessor fingerprint validation;
- append-only v2 run, assignment, subject/member, source binding,
  purpose/application, taxonomy-reference, decision, finding/case,
  coverage/gap, dependency-edge, partition-transition, and publication rows;
- canonical payload and derived-artifact fingerprints;
- foreign keys and exact run/snapshot/context/config ownership;
- uniqueness preventing duplicate case members or positive/control aliasing;
- atomic publish or no visible partial v2 state;
- v1 and all predecessor readers retaining exact accepted behavior;
- current-only consumers selecting v2 only when explicitly requested; and
- corruption, missing row, cross-run substitution, order drift, hash drift,
  taxonomy-version drift, or source-applicability drift rejected before
  publication.

### 9.3 Execution and replay modes

- **Clean execution:** reopens and validates every declared external input,
  regenerates the Bethesda snapshot/projected members, executes v2, persists,
  reopens, and byte-compares canonical output.
- **Incremental execution:** reuses only exact upstream artifacts whose input,
  code, configuration, dependency, and source/application fingerprints match;
  changed dependencies invalidate only affected descendants.
- **Retained-downstream replay:** starts from retained project-authored
  structural/product artifacts, revalidates their provenance, and reproduces
  exact v2 output without reopening external payloads.
- **Audit-only unavailable:** preserves provenance and prior canonical output
  when the owner-supplied root is absent, but states clean replay is unavailable
  and publishes no new result.

Reopen, process restart, backup/restore, repeated replay, and concurrent
read/publish tests must retain exact identity. Missing or corrupted retained
bytes fail closed. No mode may silently substitute the live game directory,
another mod version, another local root, or a historical fixture.

## 10. Work packages and predecessor gates

Implementation uses one mutable working candidate. Work packages are logical
gates, not freeze points or separate correction branches.

### 10.1 Planned implementation surface

The implementation should extend these owned seams. A necessary clean file
split is allowed, but moving meaning into an unrelated package is not.

| Concern | Planned path/surface |
|---|---|
| v2 JSON contract | new `contracts/json-schema/scope-reversion-analysis.v2.schema.json` beside the frozen v1 schema |
| v2 domain types/invariants | new `src/Infinium.Domain/Contracts/ScopeReversionV2Contracts.cs`; no edits that change v1 types/invariants |
| v2 codec | additive v2 codec in `src/Infinium.Application/Serialization/` with strict schema validation and canonical round trip |
| adapters/analyzer/declaration/identity | additive v2 files under `src/Infinium.Analysis/ScopeReversion/`; shared pure helpers may be extracted only with byte-for-byte v1 regression proof |
| Bethesda-to-scope projection | additive projector under `src/Infinium.Application/ScopeReversion/` consuming `BethesdaSemanticSnapshot` and typed source-application decisions; Bethesda decoding remains owned by `src/Infinium.Bethesda/` |
| composition/output/replay | additive v2 composition, renderer, and persistence phase under `src/Infinium.Application/ScopeReversion/` |
| schema 11/store | additive migration in `src/Infinium.Persistence/AuthoritativeStore.Migrations.cs` and v2 store surface beside `AuthoritativeStore.ScopeReversion.cs` |
| controlled-real harness | new `eng/verify-m1-slice8.ps1` plus answer-free test support; no payload under `fixtures/public/`, `docs/`, or Git |
| verification | focused additions across Unit, Contract, Integration, Evaluation, Security, and Fault test projects; frozen v1 tests remain in every affected floor |
| closeout | new `docs/plans/milestones/m1/slices/s8/record.md` only during authorized implementation closeout, plus compact navigation/current-state updates |

No product assembly or test project may reference an evaluator archive,
evaluator-private project, retired path, or controlled-real absolute root. The
root enters only through the explicit verification harness and typed manifest.

### WP1 - Contract and admission seam

**Predecessor:** separately recorded plan acceptance and activation.

Implement the v2 schema/domain contracts, invariants, canonical JSON codec,
analyzer declaration, exact local-input manifest validator, and v1 preservation
tests. Define actor-cohort and placed-reference subject shapes, shared causal
case aggregation, upstream taxonomy references, and the bounded v2 claim.

**Exit gate:** schema/codec round trip, invalid/dangling/cross-run rejection,
manifest containment/hash checks, declaration fingerprint checks, and complete
v1 golden/regression compatibility pass. No controlled-real semantic run yet.

### WP2 - Generic controlled-real projector

**Predecessor:** WP1 passes.

Implement one deterministic projector from admitted Bethesda snapshots plus
source support/application decisions to v2 actor-cohort and reference inputs.
It must use typed fields and canonical identities only, preserve evidence
layers, expose unsupported input, and abstain on incomplete causal closure.

**Exit gate:** synthetic positive/negative/ambiguity/malformed/metamorphic
projection tests for both domains, no real names or IDs in generic decision
branches, and exact upstream producer/consumer provenance.

### WP3 - Matched controlled-real cases

**Predecessor:** WP2 passes and exact input admission succeeds.

Run `EVAL-0016` positive/control and then `EVAL-0017` positive/control through
the same projector/analyzer. Add only the smallest developer-owned conformance
expectations needed by accepted product meaning. Keep expected outcomes outside
execution input and never derive them by copying analyzer output.

**Exit gate:** exact results in section 8, exact residual/gap behavior, no
cross-domain branching, no third-party payload in Git/output, and all upstream
local/provider/record cases retained.

### WP4 - Persistence and replay

**Predecessor:** WP3 passes.

Implement schema 11/storage 1.10.0, v2 persistence, canonical publication,
reopen/replay/backup/restore, dependency-local invalidation, unavailable-root
disclosure, JSON, and human/CLI output.

**Exit gate:** producer, consumer, persistence, migration, round-trip,
invalid-state, retained replay, and exact controlled-real fixture evidence
support `Producer-consumer-validated` maturity. v1 and predecessor floors pass.

### WP5 - Mutation, metamorphic, and boundary evidence

**Predecessor:** WP4 passes.

Exercise at least:

- subject/mod display-name changes;
- unrelated plugin/member addition and unrelated reorder;
- relevant winner/load-order change;
- positive/control patch relation removal or alteration;
- purpose passage removal, fingerprint drift, and version inapplicability;
- missing dependency/master and failed canonical link resolution;
- `EVAL-0016` residual `AIDT` change;
- cohort member duplication/removal/reordering and false cause sharing;
- actor facts sent to the reference adapter and vice versa;
- unsupported record family, absent field, corrupt payload, malformed manifest,
  symlink/reparse escape, case collision, limit, timeout/cancellation, and
  concurrent publication; and
- external dependency unavailable after a valid prior run.

**Exit gate:** relevant changes alter or abstain as required; irrelevant names
and order do not; lower-layer facts survive later gaps; every population and
skip/failure state is accounted for; all prohibited boundaries remain
`NotUsed`.

### WP6 - Accumulated six-layer conformance

**Predecessor:** WP5 passes.

Assemble one product-conformance package covering:

1. schema/contract and canonical codec evidence;
2. bounded positive/negative examples;
3. invalid, mutation, and metamorphic evidence;
4. persistence, migration, lifecycle, replay, and safety evidence;
5. controlled integration/generalization for both categories; and
6. fresh semantic, security, provenance, and diff review.

Every mandatory command must discover nonzero tests and fail on zero. Receipts
record command, exit code, counts, skipped reason, duration, input manifest
fingerprint, candidate commit/tree identity, and output fingerprint. A skip for
missing controlled-real inputs is not a Slice 8 pass.

**Exit gate:** focused and affected-surface checks pass and a candidate is ready
for consolidated review. The complete final floor has not yet been run.

### WP7 - Review, correction, final floor, and owner handoff

**Predecessor:** WP6 passes.

Perform the consolidated review in section 13, batch corrections on the same
candidate, rerun focused/affected checks and re-review, then run the complete
floor in section 12 once. Bind the passing product candidate once, write the
implementation record and compact handoff, validate documentation, review the
documentation-only diff, and bind one documentation-only handoff.

**Exit gate:** no must-fix finding remains; accepted counts/receipts bind the
exact clean product candidate; documentation names the bounded claim and gaps;
the owner receives an accept/reject/amend decision package. No push occurs.

## 11. Required test matrix

At minimum, tests must cover:

| Surface | Required proof |
|---|---|
| v1 preservation | frozen schema, declaration, fingerprint, canonical bytes, storage-10 migration/read/replay, synthetic fixtures, and claim boundary unchanged |
| v2 contract | canonical round trip; stable IDs; sorted/unique references; shared-case aggregation; all invalid cross-links rejected |
| input admission | exact manifests accepted; path escape, extra/missing/drifted/case-colliding files rejected; no outside-root read |
| purpose/applicability | support/application/admission separated; removed/drifted/inapplicable passage abstains; no filename fallback |
| EVAL-0016 | one cohort candidate/finding/case positive; matched zero-finding control; residual `AIDT` and gaps retained |
| EVAL-0017 | one candidate/finding/case positive; matched zero-finding control; bounded runtime/quest gaps retained |
| taxonomy | exact 0.1.0 identity, axis/facet/code/applicability/role/evidence; no axis implication or unsupported runtime promotion |
| coverage | separate actor/reference, positive/control, projection, purpose, taxonomy, analyzer, persistence, and replay denominators |
| lifecycle | enabled/disabled, clean/incremental/retained/audit-only, reopen, restart, backup/restore, invalidation, unavailable dependency |
| safety/fault | corrupt/truncated/oversized data, bad master/link, limit, cancellation, timeout, concurrency, atomic publish, bounded output |
| boundaries | provider, hosted search, Nexus, LOOT, credential, network, private fixture, semantic oracle, archive, publication, and push unused |

Every positive has a meaningful negative. Every malformed case has a precise
failure/abstention expectation. Coverage counts unsupported and skipped work;
it does not hide it from denominators.

## 12. Verification commands and accepted floor

### 12.1 Focused development checks

WP1-WP5 may use project/filter-specific `dotnet test`, schema validation,
SQLite migration/replay tests, and a new effect-free
`eng/verify-m1-slice8.ps1` harness. The harness must require explicit
`-InputManifest` and `-OutputRoot`, reject network/provider/private/oracle
options, and emit a sanitized machine-readable receipt. It must not make
controlled-real tests silently optional.

The final focused controlled-real command is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice8.ps1 `
  -InputManifest <owner-accepted-answer-free-manifest> `
  -OutputRoot <fresh-local-receipt-directory> `
  -Configuration Release
```

The receipt must show all four positive/control executions completed, zero
mandatory skips, exact public/local manifest fingerprints, no third-party
payload bytes, and zero use of every prohibited boundary.

### 12.2 Final accepted product floor

After review/correction/re-review, run the repository's complete accepted floor
from a clean committed candidate with the exact owner-accepted manifest
available:

```powershell
dotnet restore Infinium.sln
dotnet build Infinium.sln -c Release --no-restore

dotnet test Infinium.sln -c Release --no-build --filter "TestCategory=Unit"
dotnet test Infinium.sln -c Release --no-build --filter "TestCategory=Contract"
dotnet test Infinium.sln -c Release --no-build --filter "TestCategory=Integration"
dotnet test Infinium.sln -c Release --no-build --filter "TestCategory=Evaluation"
dotnet test Infinium.sln -c Release --no-build --filter "TestCategory=Security"
dotnet test Infinium.sln -c Release --no-build --filter "TestCategory=Fault"

powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice8.ps1 `
  -InputManifest <owner-accepted-answer-free-manifest> `
  -OutputRoot <fresh-local-receipt-directory> `
  -Configuration Release

powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate All -OutputRoot <fresh-local-pipeline-output-root>
git diff --check
```

The implementation record must replace placeholders with the exact local
manifest fingerprint and sanitized receipt location, record nonzero counts for
every category, and explain every legitimate skip. The manifest/root itself is
never committed. Process cleanup is required before a retry. A failed complete
floor is diagnostic: correct the same candidate, rerun focused/affected checks
and review, then attempt a new final floor. Do not bind intermediate failures.

### 12.3 Documentation validation

The plan and final handoff both require:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-documentation.ps1
git diff --check
rg -n "Status:|Disposition:|Last reviewed:" docs/plans/milestones/m1/slices/s8 docs/current-state.md docs/README.md
rg -n "private|oracle|network|provider|credential|archive|push|Slice 9" docs/plans/milestones/m1/slices/s8
```

The repository validator checks document metadata, local Markdown links, and
strict JSON. Missing local tooling is recorded; it is not replaced by a
network-installed package.

## 13. Consolidated review and correction policy

The review must inspect implementation and representative output, not only
test names. It covers:

- **Semantics:** exact positive/control differences; one shared actor case;
  causal closure; abstention; no name/ID heuristics; no negative-as-safe claim.
- **Contracts:** v1 bytes frozen; v2 clean break coherent across producer,
  consumer, codec, declaration, renderer, persistence, replay, and tests.
- **Purpose/applicability:** support, local application, and host admission
  remain separate and exact-source-bound.
- **Taxonomy/claims:** exact taxonomy version; independent axes; observed,
  established, declared, and predicted roles; consequence/extent limits.
- **Provenance/coverage:** every conclusion traceable; denominators and gaps
  complete; no third-party payload or absolute local root leaks.
- **Persistence/replay:** migration atomicity, predecessor compatibility,
  reopen/backup/restore, exact reuse/invalidation, corruption rejection, and
  unavailable-dependency honesty.
- **Security/isolation:** root containment; no unexpected filesystem reads;
  bounded parsing/output; no network, provider, credential, private, archive,
  oracle, publication, or push seam.
- **Diff/maintenance:** no fixture-specific production branch, dead code,
  duplicate meaning, stale navigation, unowned schema, or generated debris.

Findings are classified `must-fix`, `follow-up`, `non-blocking`,
`owner/authority decision`, or `safety/isolation breach`. Ordinary defects are
corrected in batches on the same candidate, followed by focused checks and
affected-surface re-review. Do not impose a pass budget. If the same conceptual
defect recurs after two completed correction attempts, pause that path for
explicit design diagnosis.

Escalate only when the durable solution would choose missing product meaning,
change accepted architecture, expand scope/authority, weaken a frozen or
isolation boundary, require missing owner-controlled input, or perform an
unauthorized effect. Continue independent in-scope work where possible.

## 14. Controlled-real result partition bookkeeping

Each case begins with a recorded role of `controlled-real-validation` only if
no result from that exact case has influenced production behavior. The ledger
records case ID, input-manifest fingerprint, candidate identity, role, reason,
timestamp, and predecessor transition.

If a result exposes a defect and any product code, contract, configuration, or
fixture expectation changes because of it:

1. append a transition to `controlled-real-development` before using the
   corrected result as acceptance evidence;
2. state what generic defect was learned and which independent synthetic
   positive/negative/malformed regression now expresses accepted product
   meaning;
3. correct the same candidate and rerun focused/affected review;
4. retain later runs of that controlled-real case as development-conformance
   evidence only; and
5. state that no independent or held-out validation claim exists.

The M1 milestone's replacement bookkeeping is satisfied by this append-only
loss-of-validation record. ADR-0035 prohibits authoring a replacement semantic
oracle or claiming a replacement independent verdict during M1/M2. No
replacement package is created. Any future independent semantic qualification
remains deferred to an accepted M3 Evaluation Readiness plan.

Product-driving results do not automatically fail Slice 8: the slice may be
accepted on truthful developer-owned conformance evidence after correction and
review. It may not retain the stronger validation label.

## 15. Implementation record, owner acceptance, and Slice 9 handoff

The Slice 8 implementation record must retain:

- exact accepted plan, activation, implementation-base, product-candidate, and
  handoff commit identities;
- all public and local answer-free manifest fingerprints, without local roots
  or third-party payload bytes;
- v2 contract/declaration/storage identities and proof that every frozen v1
  identity/behavior remained unchanged;
- exact EVAL-0016/EVAL-0017 positive/control results and representative
  provenance/taxonomy/gap/coverage summaries;
- purpose/source/application/admission evidence identities;
- clean, incremental, retained, reopen, backup/restore, invalidation, and
  unavailable-dependency evidence;
- partition transitions and any product-driving correction history;
- focused, six-layer, final-floor, documentation-validation, and review counts;
- must-fix findings and their same-candidate corrections/re-review;
- exact bounded claim and unresolved taxonomy/runtime/archive/quest/safety/
  completeness gaps;
- zero use of private fixtures, evaluator repositories, semantic oracles,
  archives, credentials, providers, network, external effects, publication,
  and push; and
- contract maturity and the owner's final accept/reject/amend decision.

The review-ready owner package contains: the clean product candidate, one
documentation-only handoff, sanitized receipts, final review report, and a
single decision request. Passing tests does not accept the slice.

If the owner accepts Slice 8, the only predecessor handoff to Slice 9 is:

- accepted Slice 8 product and documentation commits;
- frozen v2 contracts and exact migration/replay identities;
- the bounded controlled-real conformance result and its partition history;
- the retained gaps and absence of an independent semantic verdict; and
- confirmation that no external-effect authority is open.

This document does not define Slice 9 work packages, requirements, tests, or
acceptance criteria. Slice 9 remains a separately planned M1 end-to-end
closeout.

## 16. Accepted activation

The owner accepted exact plan candidate
`ab3f7ed2cf0d44067c96a7d88a44be4074486412` and activated WP1 through WP7 on
that same exact implementation base. The active work package and detailed
authorization remain stated only in `docs/current-state.md`.

Ordinary in-scope implementation, testing, review, correction, re-review, and
final-floor work may continue without another owner decision. Read-only use of
a conforming answer-free controlled-real handoff is pre-approved, but no
payload may be read before the exact pre-WP3 manifest/root gate passes. Final
product acceptance remains a later owner decision.

This activation grants no private-material, evaluator-repository, semantic-
oracle, network, provider, credential, archive, external-effect, publication,
push, or Slice 9 authority.
