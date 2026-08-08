# M1 semantic and local-ground-truth evaluation specifications

Status: Accepted  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-08-08
Target milestone: M1 — Backend semantic proof

## 1. Purpose and authority

This document defines the accepted Wave F specifications for:

- EVAL-0001, EVAL-0002, EVAL-0016, and EVAL-0017 — the synthetic and
  controlled-real scope-incongruent-reversion proof;
- EVAL-0032 — candidate selection from typed indexes and causal joins;
- EVAL-0051, EVAL-0052, and EVAL-0054 — MO2, Bethesda-record, and supported
  target ground truth;
- EVAL-0065 and EVAL-0067 — modular-analyzer and typed-evidence contracts; and
- EVAL-0083 through EVAL-0086 — provenance, case grouping, coverage/readiness,
  and taxonomy behavior.

It refines the planned cases in the
[case catalog](../case-catalog.md) and the already accepted EVAL-0032 and
EVAL-0086 refinements in RESEARCH-0022 and RESEARCH-0021. It does not create an
implementation, make a fixture execution-ready, execute a fixture, or mark any
case as passed.

The 2026-08-08 Slice 5 recovery retired the prematurely generated fixture
identities that had been derived from this specification. The fixture-family
descriptions below remain accepted semantic obligations, but they now describe
staged work-package slots rather than reserved package names. Each owning Slice
5 work package must assign a fresh identity when it authors and reviews that
slot. Do not reconstruct the removed corpus from historical names or records.

The controlling product and architecture contracts are:

- the accepted common [M1 evaluation baseline](../m1-evaluation-baseline.md);
- [requirements](../../product/requirements.md);
- [domain model](../../product/domain-model.md);
- [taxonomy `0.1.0`](../../product/mod-impact-taxonomy.md);
- [severity, confidence, maturity, coverage, and readiness](../../product/severity-confidence-and-coverage.md);
- [evaluation strategy](../evaluation-strategy.md);
- [fixture guidelines](../fixture-guidelines.md);
- [anti-overfitting rules](../anti-overfitting-rules.md);
- ADR-0001, ADR-0002, ADR-0004, ADR-0008, ADR-0009, ADR-0010, ADR-0015, and
  ADR-0022; and
- [RESEARCH-0034](../../research/investigations/RESEARCH-0034-loose-facegen-qualification.md)
  and
  [RESEARCH-0035](../../research/investigations/RESEARCH-0035-gate-c-real-mod-qualification.md).

Where this specification conflicts with an accepted requirement, taxonomy, or ADR,
the accepted artifact controls.

This document is specification set
`infinium.eval.m1.semantic-and-ground-truth/1`. A material input, oracle,
assertion, or support-boundary change creates a reviewed successor revision;
it does not rewrite retained execution against this revision.

## 2. Common conformance contract

### 2.1 Fixture partitions and transition

Every executable fixture must be assigned before first use to `development`,
`validation`, or `held-out`. Controlled-real EVAL-0016/EVAL-0017 inputs are
validation fixtures, not held-out fixtures, because their identities and
expected answers are already documented by RESEARCH-0035.

A validation or held-out result that changes production code, rules, prompts,
ranking, or thresholds becomes development data immediately. The transition is
append-only and requires a materially independent replacement before the lost
validation or holdout claim can pass.

### 2.2 Resolved input manifest

Every execution binds one immutable resolved fixture manifest containing:

- fixture ID/version and partition history;
- exact snapshot, analysis-context, scan-configuration, analyzer, ruleset,
  taxonomy, prompt/schema/model, tool/library, and support-manifest versions;
- exact file/member byte lengths and SHA-256 values for every consumed byte
  dependency;
- plugin order, provider order, archive-exclusion or archive-support state,
  runtime identity, and supported/unsupported capability declaration;
- source/acquisition revision and retained claim identity where external
  evidence is used;
- expected replay class and all retained/external replay dependencies; and
- privacy, licensing, and redistribution classification.

The executable manifest presented to the system under test must not contain
expected answers, fixture class labels, oracle paths, or answer-bearing notes.

### 2.3 Independent ground truth and answer isolation

Expected results are pre-registered in a separately access-controlled oracle
manifest. Test orchestration may compare actual output with that oracle only
after the run has published its immutable output.

- Mutagen, the production MO2 adapter, production candidate logic, and any LLM
  under test may not author their own expectations.
- Bethesda expected results use hand-audited bytes, retained format invariants,
  project-authored independent readers, and manual master-index translation.
- MO2 expected results use a disposable pinned MO2 instance and an independent
  capture of authoritative MO2-visible behavior, not the production adapter.
- Author documentation may be an ordinary evidence input when intent or
  applicability is being evaluated. Hidden adjudication of what conclusion it
  “should” cause is not an input.
- A held-out oracle is unavailable to implementation, retrieval, ranking, and
  model contexts. Only the evaluation harness receives it after publication.

### 2.4 Required typed output

Each case must retain, including empty sets:

- local observations and deterministic results;
- external claims and application links;
- candidates and selection/lane provenance;
- hypotheses, supporting/contradicting evidence, and missing information;
- findings and recommendations;
- supported cases and lead-only investigation cases separately;
- abstentions, invalid-input outcomes, and coverage gaps;
- taxonomy assignments with exact version, role, applicability state,
  evidence, conditions, and derivation provenance;
- analyzer coverage population, denominator, status, exclusions, and failures;
  and
- originating run, resolved input, dependency, LLM-involvement, replayability,
  and audit-gap provenance.

An absent object must be represented by an asserted empty collection or typed
non-production reason where the case expects that absence; omission is not
equivalent to a correct empty result.

### 2.5 Taxonomy and classification rules

Taxonomy assertions use
`infinium.skyrim-se.mod-impact-taxonomy/0.1.0`. Codes on different axes never
imply one another. `declared`, `observed`, `predicted`, and `established`
remain classification roles rather than confidence or authority. Severity,
confidence, evidence authority, analyzer maturity, readiness, and case identity
remain separate.

Cases whose subject is an operational support gate or evidence container may
use `not-applicable` for an axis rather than invent a game-impact
classification. `unknown`, `unsupported`, and `unmapped` are asserted
independently per axis.

### 2.6 Replay, retention, and redistribution

Synthetic project-authored bytes and manifests may be tracked when their
licensing permits it. Third-party mod/game bytes, private profiles, private
absolute paths, credentials, and account data may not be committed or
redistributed.

Controlled-real and exact-runtime cases use evaluator-supplied private bytes
validated against tracked fingerprints and reproducible acquisition metadata.
Permitted private inputs remain available through parsing, analysis,
case/finding/prose synthesis, provenance, and audit. Later deletion or
minimization must change replayability/audit disclosures honestly and must not
alter historical conclusions.

Deterministic replay requires all exact inputs and implementation dependencies.
Boundary replay may consume retained outputs at a non-reproducible boundary.
Missing private, provider, source, model, or tool dependencies yields the
declared partial/audit-only/unavailable state rather than a silent refresh.

### 2.7 Common setup, execution, and retained artifacts

For every case:

1. resolve and validate the public, execution, oracle, replay, and
   redistribution manifests;
2. verify the declared partition and confirm the oracle is inaccessible to the
   system under test;
3. bind the exact repository commit, clean worktree, dependency locks,
   fixture/specification versions, snapshot, context, configuration, and
   deterministic seed where applicable;
4. once an M1 plan is accepted, run only the configured case/analyzers under
   its process boundary and the accepted authority ADRs;
5. require immutable publication of the human-readable CLI output, versioned
   JSON, raw typed intermediates, coverage/gaps, logs, and replay/audit state;
6. compare the published output with the independent oracle; and
7. retain commands, environment/version identities, machine-readable
   assertions, failed attempts, and reviewer disposition.

Development diagnosis may use a dirty implementation, but retained passing
evidence requires the clean committed execution envelope defined by the common
M1 baseline.

## 3. EVAL-0001 — Synthetic harmful scope-incongruent reversion

**Purpose.** Demonstrate that a later appearance-scoped NPC override which
supplies appearance records and loose FaceGen while reverting qualified package
relations becomes one evidence-supported finding and supported case.

**Requirements and decisions.** ANALYSIS-004, ANALYSIS-005, FIND-003,
EVID-001 through EVID-007, FIND-001 through FIND-004, COVER-001, ADR-0001,
ADR-0002, ADR-0009, and ADR-0010.

**Scope.** Positively allowlisted `NPC_` origin/override identity, `PKID`,
selected appearance fields, race/template/FaceGen applicability, and
loose-only FaceGen provider state. The generic mechanism is a stale
structural-relation reversion under scope-incongruent intent.

**Non-scope.** Runtime AI outcome; archive FaceGen; NIF/DDS visual correctness;
every NPC or AI field; generic patch correctness; and broad Skyrim safety.

**Fixture partition and input.** Development fixture `SEM-NPC-POS-001`, its
validation rename/reorder metamorph `SEM-NPC-VAL-001`, and a sealed
independently authored held-out replacement `SEM-NPC-HO-001`. The resolved
input uses the corresponding packages in the
[accepted fixture manifest](../fixtures/m1-semantic-fixture-manifests.md).

**Independent truth and isolation.** Project-authored plugin bytes are
hand-audited before production parsing. The oracle pins origin FormKeys,
master translation, winner, package sets, appearance-field differences,
FaceGen paths, provider chains, and applicable author-intent passages. Oracle
data is not supplied to the parser, candidate selector, analyzer, or model.

**Expected typed result.**

- Observations: exact override chain/winner; prior qualified package relation;
  omission/reversion by the appearance winner; different qualified appearance
  values; origin-keyed loose mesh/tint paths; complete provider chain/winner.
- Candidate: one mandatory deterministic `scope-incongruent-reversion`
  candidate with canonical participants, changed-field rationale, and exact
  dependencies.
- Hypothesis: the appearance-scoped winner may unintentionally remove the
  prior behavioral feature; author intent supports appearance scope and does
  not support package removal.
- Finding: exactly one **Strongly supported**, **Moderate** finding for the
  planted bounded behavior loss, with expected symptoms and a reversible
  patch/order remediation or bounded validation proposal. The established
  static reversion supports that confidence; the predicted runtime symptom
  remains separately labeled.
- Case: one supported case grouping the record and FaceGen observations around
  the shared stale-reversion cause.
- Abstentions/gaps: archive FaceGen, rendering, unqualified fields, and runtime
  symptom remain explicit; no lead-only case remains for the planted issue.

**Taxonomy `0.1.0`.**

- declared for the later appearance source: `purpose.replace-overhaul` and
  `purpose-target.actors.appearance-identity`;
- declared for the prior behavioral source:
  `purpose.modify-tune` and `purpose-target.actors.ai-packages`;
- observed: `surface.plugin-data`, `surface.asset`,
  `delivery.plugin-container`, and `delivery.loose-data-file`;
- established for the modified loci: `area.actors.appearance-identity` and the
  qualified `area.actors.ai-packages` record relation;
- predicted for the finding: `consequence.incorrect-functional-behavior`;
- predicted extent: `extent.subject.bounded-set`,
  `extent.spatial.nonspatial`, `extent.persistence.installation-persistent`,
  and `extent.propagation.bounded-dependents`.

The consequence remains predicted unless the fixture supplies independent
runtime evidence; the static reversion itself is established.

**Assertions.**

1. The planted interaction is admitted to a mandatory lane without a model
   all-pairs pass.
2. Renaming mod/plugin folders and reordering unrelated inputs changes no
   semantic conclusion.
3. The finding cites the exact record, field/relation, intent, provider, and
   dependency evidence.
4. FaceGen presence supports cross-layer scope evidence but does not prove
   visual correctness.
5. Removing intent evidence causes an abstention/needs-input or lead, not the
   same supported finding.
6. The case contains exactly the causally related output and no unrelated
   same-mod result.

**Failure interpretation.** A miss indicates candidate, parser, intent,
evidence-threshold, or grouping failure according to the earliest mismatching
typed stage. Extra findings indicate overreach. A mismatch never authorizes
changing the oracle without new independent evidence.

**Replay/privacy.** Fully replayable with tracked synthetic bytes and versions;
held-out bytes/oracle remain access-controlled until evaluation.

**Passing does not prove.** Runtime manifestation, archive parity, all actor
semantics, broad cross-category generality, or production MO2/Mutagen
conformance.

## 4. EVAL-0002 — Synthetic intentional/harmless matched negative

**Purpose.** Demonstrate that the same structural shape does not become a
problem when applicable authoritative intent explicitly supports the
replacement or removal.

**Requirements and decisions.** ANALYSIS-003, ANALYSIS-004, EVID-006,
INTENT-003, ADR-0001, and the anti-overfitting rules.

**Scope/non-scope.** The record/provider shape mirrors EVAL-0001 while its
applicable author evidence deliberately changes the semantic answer. It does
not prove that load order alone establishes intent or that a named patch is
effective.

**Fixture partition and input.** Development `SEM-NPC-NEG-001`, validation
metamorph `SEM-NPC-VAL-NEG-001`, and sealed held-out `SEM-NPC-HO-NEG-001`.

**Independent truth and isolation.** The independently authored intent passage
explicitly states the behavioral replacement/removal and applicable version.
The oracle records that claim and local applicability separately from the
structurally identical record expectations.

**Expected typed result.**

- The same local observations and initial candidate class may exist.
- An applicable external intent claim and application link contradict the
  harmful-reversion hypothesis.
- The candidate resolves as intentional/non-problematic or the hypothesis is
  rejected.
- No problem finding, supported case, consequence, severity, remediation, or
  readiness effect is emitted.
- Development output retains the candidate, contradiction, resolution, and
  empty finding/case sets.
- If the intent source or applicability is removed, the analyzer abstains or
  creates a lead-only needs-input case; it does not guess either harmlessness
  or harm.

**Taxonomy `0.1.0`.** Purpose and observed surface/area assignments remain
valid. The later synthetic source is declared
`purpose.replace-overhaul` with both
`purpose-target.actors.appearance-identity` and
`purpose-target.actors.ai-packages`, because its applicable passage explicitly
claims both effects. A problem consequence and finding effect extent are
`not-applicable`. The absence of a finding does not erase the observed change.

**Assertions.**

1. Fixture names, plugin order, and structural similarity do not force the
   EVAL-0001 answer.
2. Priority/order is at most weak intent evidence.
3. The applicable cited claim, not an answer label, resolves the candidate.
4. Missing or mismatched claim conditions produce abstention/lead behavior.

**Failure interpretation.** A finding is a false positive or applicability
failure. Suppression before raw-output retention is a development-transparency
failure.

**Replay/privacy.** Fully replayable from project-authored bytes and source
text; held-out oracle remains sealed.

**Passing does not prove.** That all similar overrides are intentional or that
author documentation overrides contradictory local facts.

## 5. EVAL-0016 — Controlled-real actor/AI/FaceGen candidate

**Purpose.** Validate the generic mechanism against qualified candidate
`REAL-NPC-0001`: AI Overhaul `1.8.6`, Children of the Pariah `1.2.3.6`, and
the author-supplied package-specific matched control.

**Requirements and decisions.** ANALYSIS-004, ANALYSIS-005, EVID-002,
FIND-001 through FIND-004, ADR-0008 through ADR-0010, RESEARCH-0034, and
RESEARCH-0035.

**Scope/non-scope.** Scope is limited to the selected package relations for
`0001339A:Skyrim.esm` and `0001AA63:Skyrim.esm`, selected appearance fields,
and archive-excluded loose FaceGen pairs. The matched patch is a control only
for the package relations it demonstrably restores. Its residual `AIDT`
difference is neither suppressed nor adjudicated by this case.

**Fixture partition and input.** Validation-only `REAL-NPC-0001-POS` and
`REAL-NPC-0001-CTRL`, reconstructed from the private manifest and exact hashes
qualified by RESEARCH-0035. They cannot serve as held-out data.

**Independent truth and isolation.** The tracked non-Mutagen raw byte map,
manual master-index translation, exact archive/member hashes, author purpose,
selected installer choices, and RESEARCH-0034 FaceGen relationship form the
oracle. The private payloads and byte map/oracle are not model inputs; ordinary
author evidence may be.

**Expected typed result.**

- Positive: observations for the exact package omissions, differing appearance
  values, and origin-named loose FaceGen; one mandatory candidate; exactly one
  Strongly supported, Moderate package-reversion finding and one supported
  case; likely actor behavioral symptom; package-preserving patch
  recommendation; explicit runtime/archive/visual gaps.
- Control: observations show the selected packages and appearance fields are
  combined; no package-reversion finding or supported case for those package
  relations. Any separately supported `AIDT` candidate remains independently
  visible and cannot be suppressed by the patch name.
- No output claims that the patch is universally complete or that runtime
  behavior was observed.

**Taxonomy `0.1.0`.** The Children of the Pariah intent evidence declares
`purpose.replace-overhaul` and
`purpose-target.actors.appearance-identity`; AI Overhaul intent declares
`purpose.modify-tune` and `purpose-target.actors.ai-packages`. Local evidence
observes `surface.plugin-data`,
`surface.asset`, `delivery.plugin-container`,
`delivery.loose-data-file`, `area.actors.appearance-identity`,
`area.actors.ai-packages`, predicted
`consequence.incorrect-functional-behavior`, bounded-set/nonspatial/
installation-persistent/bounded-dependent extent. The source-supported purpose
assignments are declared; local fields/providers are observed; consequence and
runtime symptom remain predicted.

**Assertions.**

1. Exact package identities survive different master indices.
2. Both selected positive records are retained under one shared cause without
   duplicate cases.
3. The control clears only the package-specific conclusion.
4. Real names, IDs, FormKeys, or patch titles are absent from production rules.
5. Missing any private dependency fails fixture resolution or records a gap;
   it does not select a similar installed file.

**Failure interpretation.** A miss is evidence against the exercised generic
mechanism or upstream conformance. A control finding may be a master-translation
or patch-effect false positive. An unrelated `AIDT` result is assessed on its
own evidence and is not automatically a failure.

**Replay/privacy.** Exact replay is private and requires evaluator-supplied
archives, official masters, selected members/assets, source passages, and
pinned analyzer/runtime dependencies. Tracked artifacts contain fingerprints,
public IDs/links, structural expectations, and claim boundaries only.

**Passing does not prove.** Broad actor support, universal patch correctness,
archive provider support, observed gameplay behavior, redistributability, or
cross-category generality by itself.

## 6. EVAL-0017 — Controlled-real placed-reference/link candidate

**Purpose.** Validate the same stale-structural-relation mechanism in a
materially different category using `REAL-REFR-0001`: Candlehearth `1.1.1`,
Nightgate Inn Revived `1.3`, and the author patch.

**Requirements and decisions.** ANALYSIS-004, ANALYSIS-005, FIND-003,
ADR-0008 through ADR-0010, RESEARCH-0035, and the category-neutral
anti-overfitting policy.

**Scope/non-scope.** Scope is one `REFR`, `00017061:Skyrim.esm`, its qualified
`XLKR` linked-reference relation, `DATA` placement, and the patch's demonstrated
merge. Runtime rental behavior, quest breakage, other references, navmesh, and
global list safety are out of scope.

**Fixture partition and input.** Validation-only `REAL-REFR-0001-POS` and
`REAL-REFR-0001-CTRL`, reconstructed from exact private inputs and the tracked
manifest. They are known-answer validation data, not held-out data.

**Independent truth and isolation.** The project-authored non-Mutagen byte map,
raw offsets/subrecords, master lists, exact hashes, and author-maintained
purpose/patch evidence establish the expected relation and placement. The
production parser and model do not receive the oracle.

**Expected typed result.**

- Positive observations establish Candlehearth's changed `XLKR`, Nightgate's
  redesigned `DATA`, and Nightgate's restoration of the vanilla `XLKR`.
- One mandatory candidate and hypothesis connect the presentation-scoped
  later override to loss of the earlier structural relation.
- Exactly one Strongly supported, Moderate finding and one supported case
  report the structural reversion, predict the localized rental-association
  symptom, and recommend the demonstrated author patch or bounded validation.
- The control observes Candlehearth's relation plus Nightgate's placement and
  produces no finding for this exact relation.
- The runtime symptom remains unobserved; no quest or global-safety claim is
  emitted.

**Taxonomy `0.1.0`.**

- declared: `purpose.modify-tune` for the inn/rental feature and
  `purpose.replace-overhaul` for the visual/spatial overhaul, with supported
  `purpose-target.world` and `purpose-target.presentation.visual`;
- observed: `surface.plugin-data` and `delivery.plugin-container`;
- established loci: `area.world.cells-worldspaces-locations` and
  `area.world.placed-objects-activation`;
- predicted affected feature/consequence:
  `area.gameplay.items-inventory-economy` where supported by the cited rental
  claim, and `consequence.incorrect-functional-behavior`;
- predicted extent: `extent.subject.single-instance`,
  `extent.spatial.cell-or-location`,
  `extent.persistence.installation-persistent`, and
  `extent.propagation.bounded-dependents`.

**Assertions.**

1. The analyzer compares canonical relation targets, not raw master-index bytes
   alone.
2. The positive and control differ only at the qualified merged relation.
3. The case is not merged with actor/FaceGen cases merely because both use the
   generic stale-reversion mechanism.
4. Passing EVAL-0016 and EVAL-0017 permits only the bounded statement that the
   mechanism survived one materially different category proof.

**Failure interpretation.** A miss may indicate unsupported `REFR` semantics,
candidate generalization failure, or intent/applicability failure. A control
finding indicates merge-semantics or patch-effect overreach.

**Replay/privacy.** Private exact archives/plugins and author-source revisions
are replay dependencies. Only hashes, public identities, raw structural
expectations, and claim boundaries are tracked or redistributable.

**Passing does not prove.** Broad world, quest, navigation, or runtime behavior
coverage; exhaustive cross-category generality; or observed symptoms.

## 7. EVAL-0032 — Typed-index and causal-join candidate selection

**Purpose.** Demonstrate that snapshot-bound typed indexes and qualified causal
joins retain all planted supported interactions without naïve all-pairs model
comparison, while preserving negatives, gaps, lane membership, provenance, and
scale accounting.

**Requirements and decisions.** EVID-005, ANALYSIS-017, OPS-004, ADR-0001,
ADR-0002, ADR-0010, and the accepted RESEARCH-0022 section 13 refinement.

**Scope.** Atomic rules, a small multi-surface profile, medium/high synthetic
profiles, an upper-bound structural stress profile, and the EVAL-0016/0017
validation candidates. Required strata are record reversion,
placed-reference topology, typed asset target, PEX/VMAD lead, named
configuration, generated output, native relation, patch effect,
documentation, cross-layer case, matched negatives, malformed/unsupported,
broad distractors, ambiguous intent, rename/reorder, and relevant-winner
invalidation.

Strata outside the bounded M1 semantic analyzers exercise only typed
index/routing, unsupported/gap, or lead behavior. They do not enable a named
generator/configuration/native/PEX analyzer, production NIF parser, or finding
authority that the M1 claim boundary excludes.

**Non-scope.** Semantic correctness outside a stratum's declared analyzer;
real-profile correctness; model reasoning quality; taxonomy labels as causal
proof; and an all-pairs exception.

**Fixture partition and input.** WP3 owns separate atomic, integration, scale,
and stress public fixture slots, plus the two controlled-real validation
packages. Any future held-out slot requires separate authorization. Each slot
has a separately pre-registered truth population and canonical participant
graph and receives a fresh package identity only in its owning work package.

**Independent truth and isolation.** Fixture construction supplies the planted
relationships and negative/gap populations. Expected lane/candidate
membership is stored outside the execution manifest. The selector receives
typed local indexes and evidence, never expected class/disposition.

**Expected typed result.**

- Every supported positive enters its required deterministic or mandatory
  lane with canonical participants, join path, rationale, dependencies, and
  scoped population.
- Every matched negative receives an explicit resolved or escalated record;
  unsupported/malformed entries produce typed gaps or invalid input.
- Ambiguous-intent entries retain the candidate and produce an abstention or
  lead-only needs-input state; they do not become findings or silent negatives.
- Ranking perturbation may reorder a lane but cannot remove mandatory work.
- Rename/unrelated insertion preserves causal membership; changing a relevant
  winner invalidates only dependent candidates.
- Candidate/call/LLM/token/cost counts, unprocessed work, and labeled
  denominators are exact.
- No whole-profile raw model context, dense implicit pair matrix, or naïve
  all-pairs model loop occurs.
- Findings/cases are not required from a candidate-selection-only execution;
  their asserted sets are empty unless a separately enabled analyzer consumes
  the candidates.

**Taxonomy `0.1.0`.** Each fixture records applicable purpose, surface, area,
consequence, and extent strata for coverage selection. These labels may
stratify metrics or route work but never establish candidate causality.
Unknown/unsupported/unmapped/not-applicable populations are retained.

**Assertions and metrics.** Candidate recall is 100% for the declared planted
supported population; mandatory-lane recall is 100%; unsupported-gap recall is
100%; no positive disappears under score perturbation; exact canonical pairs
must derive from real input identities; duplicate/false-merge/false-split,
distractor volume, latency, IO, memory, disk, checkpoint/restart, model usage,
budget-limited work, and clean/incremental equivalence are reported separately.
No production threshold for broad precision or performance is invented here;
the M1 plan must set measured bounds for the selected fixture sizes.

**Failure interpretation.** A missing planted interaction is a recall or index
contract failure. Extra work may indicate join overbreadth but is not itself a
semantic finding error. A taxonomy-derived pair without a causal join is an
authority failure.

**Replay/privacy.** Synthetic profiles are replayable from tracked generators,
seeds, manifests, and outputs. Controlled-real inputs retain their private
classification. The creator profile may be used only as private non-oracle
shape/scale evidence and is not needed to pass this M1 case.

**Passing does not prove.** Finding accuracy, real-mod recall beyond the two
qualified candidates, M3 high-end performance, or broad analyzer coverage.

## 8. EVAL-0051 — MO2 effective-state ground truth

**Purpose.** Prove agreement with authoritative MO2 `2.5.2` behavior for every
effective-state surface consumed by M1.

**Requirements and decisions.** SCOPE-003, SCOPE-005, SNAP-001, ADR-0008, and
ADR-0010.

**Scope.** Explicit instance/profile binding; saved-selection suggestion;
enabled mods/order; enabled plugins/order inputs; physical Data/unmanaged,
enabled-mod, overwrite, hidden/skipped, and qualified loose contributions;
the exact game-plugin/secondary-Data/additional-mapper inventory; complete loose
provider chains/winners; local installed entity versus source mapping; and
capture drift. A positive real additional mapper is conditional on that exact
supported inventory containing a deliberately qualified contributor. Archive
provider behavior is excluded until separately qualified.

**Non-scope.** Live USVFS operation, MO2 launch/plugin, historical FOMOD/merge
reconstruction, arbitrary mappers, archive precedence, or the private creator
profile as oracle.

**Fixture partition and input.** Disposable pinned MO2 profiles
`MO2-ATOMIC-DEV`, `MO2-INTEGRATION-VAL`, `MO2-NEGATIVE-VAL`, and sealed
`MO2-HO-001`; every protected root is disposable. The input manifest pins MO2
binary/configuration identity, profile files, physical trees, path comparator,
mapper allowlist, expected quiescence, and snapshot-assurance requirements.

**Independent truth and isolation.** An evaluator prepares each disposable
instance and records MO2 UI/VFS-visible state plus direct physical facts before
running the production adapter. The production adapter's output is not copied
into the oracle. Saved selection is tested separately from explicit target.

**Expected typed result.**

- Exact selected profile, enabled/order states, loose provider chains/winners,
  hidden/deleted/unmanaged/overwrite facts, the explicit additional-mapper
  inventory, physical local identities, and source-mapping evidence.
- When the exact supported additional-mapper inventory is empty, the expected
  result is an explicit empty inventory plus generic registry-mechanics and
  unknown/unqualified fail-closed evidence, not an invented positive provider.
- Saved selection is suggestion provenance only and cannot start/bind a run.
- Unknown mapper, ambiguity, inaccessible input, normalization collision, and
  drift fail closed or become explicit gaps.
- Archive-member population is `unsupported`, not silently omitted.
- No semantic finding/case is required; observations, snapshot assurance, and
  coverage/gaps are the expected output.

**Taxonomy `0.1.0`.** Qualified effective files/plugins receive observed
surface/delivery assignments only where evidence supports them. Provider,
winner, hidden, unmanaged, and source-mapping states remain topology rather
than taxonomy. Affected-area/consequence/extent axes are `not-applicable` at
capture unless a later analyzer derives them.

**Assertions.**

1. All positively allowlisted states agree exactly with the independent oracle.
2. Unknown or ambiguous state never becomes a guessed winner.
3. Renaming a local mod changes display/source-mapping evidence as applicable,
   not the physical identity contract by name alone.
4. Same-size/time byte drift cannot preserve a byte-dependent snapshot under
   ADR-0010.
5. Capture performs no protected setup write and requires MO2 to be closed.
6. A real positive additional-mapper case is mandatory only after that mapper
   is deliberately selected and qualified for the exact supported target.

**Failure interpretation.** Any wrong supported state is an authoritative
reconstruction failure. An explicit unsupported/gap outcome is correct only
outside the allowlist. Non-mutation is finally gated by EVAL-0046/EVAL-0080.

**Replay/privacy.** Disposable synthetic fixtures are reproducible. Any
controlled-real profile is private and supplementary. MO2 itself is
user-installed and is not redistributed by Infinium.

**Passing does not prove.** All MO2 versions/plugins/mappers, archive behavior,
live VFS parity, user-profile safety, or semantic analyzer correctness.

## 9. EVAL-0052 — Bethesda record-semantic ground truth

**Purpose.** Prove that the Mutagen `0.54.2` path agrees with independently
specified expectations for the exact record/field/shape envelope consumed by
M1.

**Requirements and decisions.** SCOPE-005, ANALYSIS-003, COVER-001,
ADR-0007, ADR-0009, and ADR-0010.

**Scope.** Exact supplied plugin order, record identity, override/winner, field,
link, state, and full/light semantics for the positive M1 allowlist below.

**Non-scope.** Every record/field/shape not named below, including `QUST`,
quest aliases, forced-reference/`ALFR`, objective, stage, condition, and other
quest-logic semantics; automatic environment or load-order discovery; standard
localized-string resolution; archive activation/member precedence; and runtime
behavior. The planned EVAL-0006/EVAL-0007 quest-relevance pair is not an M1
case.

**Positive M1 allowlist.**

- TES4 master lists and ESL flag needed for origin/master translation;
- supplied plugin order, record identity, FormKey, override chain, winner,
  deleted state, compression, and full/light origin;
- `NPC_`: configuration flags/template flags, template link where present,
  `RNAM`, `AIDT`, repeated `PKID`, repeated `PNAM`, and `HCLF`;
- the resolved `RACE` relationship and `FaceGenHead` flag needed only for the
  accepted FaceGen-applicability decision;
- `REFR`: `NAME`, `XLKR`, `XLRL`, `XOWN`, and `DATA`; and
- link resolution and local-ID/origin-plugin relationships consumed by
  EVAL-0001/EVAL-0002/EVAL-0016/EVAL-0017 and RESEARCH-0034.

All other record families/fields, including `QUST` and alias shapes, standard
localized-string lookup, automatic load order, typical-environment discovery,
and archive applicability are unsupported until added to a later reviewed
matrix.

**Fixture partition and input.** `BETH-NPC-DEV`, `BETH-REFR-DEV`,
`BETH-LIGHT-VAL`, `BETH-MALFORMED-VAL`, `BETH-UNSUPPORTED-VAL`, controlled-real
validation projections from RESEARCH-0035, and sealed `BETH-HO-001`.

**Independent truth and isolation.** Project-authored binary fixtures have
hand-audited offsets, byte sequences, master lists, FormKeys, decoded fields,
links, order, and winners. A small independent reader may validate structural
invariants, but Mutagen and xEdit cannot author expectations. Controlled-real
expectations use the retained raw maps from RESEARCH-0035.

**Expected typed result.**

- Exact records, fields, states, canonical links, override chains, and winners
  for every allowlisted shape.
- Full/light master-index translation produces canonical FormKeys independent
  of raw file-local indices.
- Malformed/pathological input fails within declared bounds with no partial
  authoritative publication.
- Unsupported record, string, archive, and shape populations produce explicit
  gaps with labeled denominators.
- No record conflict becomes a finding merely because it exists.

**Taxonomy `0.1.0`.** Parsed plugin contributions may receive observed
`surface.plugin-data` and `delivery.plugin-container`; record families do not
automatically assign purpose, affected area, consequence, extent, severity, or
confidence.

**Assertions.**

1. Every consumed field/shape is in the positive allowlist and matches the
   independent oracle exactly.
2. Adding an unqualified field does not cause it to appear supported.
3. Mutagen receives exact supplied bytes/order rather than discovering the
   user's environment.
4. One-byte changes invalidate byte-dependent results.
5. xEdit is absent from production, fixture, oracle, and manual acceptance
   dependencies.

**Failure interpretation.** Any supported-value mismatch blocks that shape.
An unsupported outcome for an allowlisted shape is incomplete implementation,
not a valid pass. A gap outside the allowlist is expected.

**Replay/privacy.** Project-authored synthetic bytes are tracked. Game/mod
bytes remain private and fingerprinted. Replay requires Mutagen `0.54.2` and
the locked dependency graph; an upgrade triggers requalification.

**Passing does not prove.** Mutagen's full API, arbitrary plugins, archive or
localized-string semantics, runtime behavior, or meaningful-conflict
detection.

## 10. EVAL-0054 — Exact supported-target rejection

**Purpose.** Demonstrate that semantic analysis starts only for the accepted
Steam Windows x64 Skyrim SE `1.6.1170.0` executable identity.

**Requirements and decisions.** SCOPE-001, SCOPE-002, SCOPE-006, ADR-0004,
ADR-0009, and ADR-0010.

**Scope/non-scope.** Runtime gate, manager/platform declarations, malformed,
missing, unreadable, inconsistent, and changed-during-capture inputs. The case
does not test runtime repair, Steam launch, SKSE compatibility, or other
channels.

**Fixture partition and input.** Private exact target
`TARGET-1170-PRIVATE-VAL`; derived one-byte mutation; same displayed version
with unknown hash; other-channel/known-unsupported manifest; project-authored
malformed PE; missing/unreadable input; manager/platform mismatches; and a
capture-race fixture.

**Independent truth and isolation.** The support manifest pins byte length
`37,157,144`, SHA-256
`C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9`,
AMD64 PE32+ GUI, fixed version `1.6.1170.0`, Steam channel, and App ID `489830`.
The expected state is selected from the manifest before detector execution.

**Expected typed result.**

- Exact target: `supported-exact`, with the matched manifest and byte identity.
- Known different supported-list entry: `unsupported-known`.
- Unknown hash/same version: `unrecognized-build`.
- Missing/unreadable: `indeterminate` with exact reason.
- Conflicting metadata/hash: `inconsistent-metadata`.
- Capture mutation: invalidated, no semantic-stage dispatch.
- Unsupported manager/platform: explicit unsupported target, no best-effort
  semantic output or fabricated coverage.

No candidate, hypothesis, finding, case, or game-impact taxonomy assignment is
expected.

**Taxonomy `0.1.0`.** Purpose, technical-surface, affected-area, consequence,
and extent axes are `not-applicable` to the operational runtime-admission
result. The support-manifest classification is not a Skyrim mod-impact
taxonomy.

**Assertions.** Whole-file hash and immutable manifest consistency are
mandatory; version strings and metadata cannot substitute. Any non-exact case
cannot dispatch semantic analyzers. Detection is read-only and records gaps.

**Failure interpretation.** Accepting any non-exact target is a fail-open
authority defect. Rejecting the exact private target indicates manifest,
capture, or detector failure.

**Replay/privacy.** The exact game executable is evaluator-supplied and never
redistributed. Tracked artifacts contain only the accepted fingerprint and
project-authored malformed/metadata fixtures.

**Passing does not prove.** Game stability, component compatibility, other
runtimes, other managers/platforms, or correct semantic analysis after the
gate.

## 11. EVAL-0065 — Analyzer modularity and declaration

**Purpose.** Demonstrate that one M1 semantic analyzer can be configured and
run independently without losing its scope, dependency, evidence, coverage,
cost, maturity, taxonomy, or evaluation contract.

**Requirements and decisions.** SCAN-001, ANALYSIS-016, SCAN-002,
SCAN-009, ADR-0001, and ADR-0002.

**Scope/non-scope.** The M1 `scope-incongruent-reversion` analyzer and its
declared record/FaceGen modules. This does not prescribe a universal plugin
framework or claim all analyzers are implemented.

**Fixture partition and input.** Development contract fixture
`ANALYZER-CONTRACT-DEV` and validation fixtures for disabled dependencies,
unsupported taxonomy area, local-only execution, invalid version, and isolated
single-analyzer operation.

**Independent truth and isolation.** A reviewed declaration schema and
pre-registered required fields form the oracle; the analyzer cannot synthesize
its contract after seeing fixture results.

**Expected typed result.** The declaration identifies:

- stable analyzer ID/version and semantic/identity-contract compatibility;
- supported and excluded inputs, record/field/asset shapes, taxonomy version,
  surfaces, areas, consequences, and extent facets;
- dependencies and minimum snapshot assurance;
- candidate, evidence, abstention, and finding-promotion thresholds;
- coverage populations/states and unsupported behavior;
- offline/network/LLM requirements;
- expected scale/cost, resource bounds, maturity, and raw-development behavior;
- linked positive, negative, boundary, malformed, cross-category, and gap
  cases; and
- immutable effective configuration retained by each run.

An independent run emits only this analyzer's typed results and declared
upstream dependencies. Disabled/missing dependencies yield explicit
skipped/unsupported/gap states without silently enabling another analyzer.
Contract-only fixtures emit no semantic candidate, hypothesis, finding, or
case; analyzer executions separately assert those typed collections according
to their linked semantic fixture.

**Taxonomy `0.1.0`.** The declaration explicitly covers the bounded assignments
used by EVAL-0001/0016/0017 and marks every other region unsupported or
excluded. It may expose evidence outside semantic scope only as a gap.

**Assertions.** Independent enable/disable works; raw output is retained;
configuration is immutable per run; declarations survive CLI/JSON round trip;
and no maturity/preset rule hides development output.

**Failure interpretation.** Missing fields or undeclared behavior mean the
analyzer is not eligible for M1 conformance. An explicit unsupported state is
correct where declared.

**Replay/privacy.** Project-authored schema/fixtures are fully replayable and
contain no private data.

**Passing does not prove.** Analyzer semantic accuracy, UI modularity, dynamic
third-party plugins, or M3 maturity.

## 12. EVAL-0067 — Typed evidence and LLM transparency

**Purpose.** Demonstrate that the evidence pipeline preserves type and
authority boundaries, including explicit LLM involvement and raw development
intermediates.

**Requirements and decisions.** EVID-001, EVID-004, EVID-007, OPS-002,
ADR-0001, ADR-0002, ADR-0013, and ADR-0015.

**Scope.** Synthetic local observations, deterministic results, external
claims, discovery/search records, candidates, hypotheses, findings,
recommendations, coverage gaps, schema-constrained OpenAI Response/search
transcripts, and the mandatory live M1 source-claim-extraction and
evidence-bound-candidate-investigation extensions.

**Non-scope.** OpenAI authentication/billing correctness (owned by the
platform cases), source acquisition fidelity, broad model quality, and
user-facing UI.

**Fixture partition and input.** WP2 owns separate evidence-type,
deterministic-provider, no-model, and hostile-content public fixture slots.
Later live claim-extraction and candidate-investigation slots run only after
the platform credential/security/budget qualification gate opens. Any future
held-out slot requires separate authorization. Deterministic provider fixtures
use inert retained transcripts, and every slot receives a fresh package
identity only in its owning work package.

**Independent truth and isolation.** The oracle specifies permitted transitions
and required/forbidden authority. Expected labels are absent from model and
pipeline inputs.

**Expected typed result.**

- Each object retains its distinct type, source, applicability, authority,
  evidence references, producer, run, and immutable revision.
- Discovery/search items and model citations remain leads until host-acquired
  exact evidence supports an external claim.
- Model output remains a proposal until host validation/admission; it cannot
  alter local observations, grant tools, or create source authority.
- Supporting and contradicting evidence, abstentions, failed validation, raw
  candidates, and empty result sets remain visible.
- LLM invocation and non-involvement are both explicit.

**Taxonomy `0.1.0`.** Assignments remain derived, versioned objects attached to
typed subjects; they do not become evidence classes or authority. The fixture
includes declared, observed, predicted, established, unknown, unsupported,
unmapped, and not-applicable examples.

**Assertions.** No type collapse; no model-created local fact; no search result
promoted directly to authoritative claim; all raw development intermediates
retained; identifiers/citations/schema validated before admission.

**Failure interpretation.** Type or authority promotion is a correctness and
trust-boundary failure. A rejected proposal with retained failure provenance
is expected behavior.

**Replay/privacy.** Synthetic payloads/transcripts are project-authored and
replayable. Synthetic provider names/models/request IDs are marked synthetic.
Live requests retain the exact accepted profile and response but never the
credential; replay of that retained result is distinct from new live
execution.

**Mandatory live assertions.** After the platform qualification request
passes, this case must separately execute:

1. `LLM-CLAIM-LIVE-VAL`, proving the source-claim-extraction schema over
   independently adjudicated project-authored passages; and
2. `LLM-INVESTIGATE-LIVE-VAL`, proving the evidence-bound
   candidate-investigation schema over a positive and matched negative.

Each request has independent authorization, finite reservation, strict schema,
retained result, typed oracle comparison, usage/cost settlement, and
secret-canary review. The host validates all proposals and retains
contradictions, abstentions, and failures. A canned transcript or the provider
qualification response cannot satisfy either live semantic assertion.

**Passing does not prove.** General live-provider or model reliability,
semantic accuracy outside the exact evaluated operations, source acquisition
fidelity, or credential security beyond the separately gated platform cases.

## 13. EVAL-0083 — End-to-end provenance

**Purpose.** Demonstrate that a material conclusion can be traversed back
through every applicable local, tool, source, provider/model, admission, and
application boundary, and that deletion/unavailability produces honest gaps.

**Requirements and decisions.** EVID-002, SNAP-006, AI-006, ADR-0001,
ADR-0002, ADR-0010, ADR-0013, and ADR-0015.

**Scope.** One local-only conclusion, one synthetic source-and-retained-LLM
conclusion, one contradictory-evidence path, one post-deletion projection, and
the two bounded live M1 semantic operations. The live source variant uses
retained project-authored local/fixture documentation and separate direct
synchronous OpenAI Responses for source-claim extraction and evidence-bound
candidate investigation. Nexus acquisition and hosted search are outside the
M1 claim boundary and must be explicitly `not-used`.

**Non-scope.** Nexus acquisition, hosted web search, background/Batch/cached
provider execution, credential handling, provider billing, or public
redistribution of source bodies. A later extension must add
Nexus interface/spec/schema/query/fingerprint routing and hosted-search
action/source provenance before either boundary is enabled.

**Fixture partition and input.** WP2 owns separate local, deterministic
source-model, contradiction, and deletion public fixture slots. A later live
composed slot is conditional on its provider and platform gates. Any future
held-out slot requires separate authorization. Every slot receives a fresh
package identity only in its owning work package.

The deterministic source-model slot uses an inert project-authored
direct-Response transcript
for deterministic provenance assertions. Because the accepted M1 baseline also
includes both accepted live semantic operations,
the later live composed slot must additionally apply the same provenance assertions
to their exact retained calls/outputs admitted by the applicable provider,
credential, and budget cases in the
[platform fixture manifests](../fixtures/m1-platform-fixture-manifests.md).
It also references the earlier qualification request. It may execute only
after the dispatch gate opens and does not independently authorize another
paid call.

**Independent truth and isolation.** A pre-registered provenance DAG specifies
nodes, typed edges, fingerprints, versions, required omissions, and deletion
effects. The execution path receives the inputs and metadata, not the expected
DAG.

**Expected typed result.**

- The finding/case links to snapshot, context, run, resolved manifest,
  analyzer/ruleset, exact local/tool versions, source revision and acquired
  passages, extraction/admission, application links, supporting and
  contradicting evidence, taxonomy assignments, and recommendation.
- The LLM variant additionally retains capability/model/prompt/schema/request,
  response, usage, and explicit proposal/admission provenance.
- The live-composed variant references the exact access-profile generation and
  separately represents the qualification, claim-extraction, and
  candidate-investigation operation identities, authorizations, schemas,
  provider calls/usage ownership, settlements, and retained responses without
  placing any credential in evidence or model context.
- Nexus and hosted-search nodes are explicitly `not-used`; a future extension
  must keep search discovery distinct from landing acquisition and exact
  passage.
- Deleting a required body/payload retains the historical conclusion,
  fingerprint and deletion receipt, changes replay/audit disclosures, and does
  not claim the missing source remains inspectable.

No extra candidate/finding/case is produced merely to test provenance.

**Taxonomy `0.1.0`.** Provenance must preserve each assignment's original
version, role, evidence, and subject. Provenance nodes themselves use
not-applicable game-impact axes unless they are the classified subject.

**Assertions.** Every material field is reachable in both directions; no
unresolved dangling citation; non-exercised boundaries state `not-used`; exact
and partial replay classifications match retained dependencies; deletion never
manufactures or rebinds evidence. Reusing the qualification response as
semantic evidence, omitting either semantic operation, or collapsing their
provenance fails.

**Failure interpretation.** A missing edge, false replay claim, source/search
collapse, or rewritten origin blocks the exercised conclusion from M1
conformance.

**Replay/privacy.** Project-authored synthetic source/provider material is
tracked. Private-source variants retain only permitted fingerprints/metadata
after configured dependent work and disclose loss. External sharing remains
out of scope.

**Passing does not prove.** Live acquisition correctness, general provider or
model reliability, all future provenance edge families, or permission to
redistribute retained sources.

## 14. EVAL-0084 — Causal case grouping

**Purpose.** Demonstrate that supported findings group by shared likely cause
and usually shared resolution rather than mod, participant, record family, or
taxonomy similarity.

**Requirements and decisions.** FIND-002, FIND-011, FIND-014, ADR-0001, and
ADR-0022.

**Scope.** A multi-finding shared-cause positive; same-mod distinct-cause
negative; same-record-family distinct-cause negative; lead-only investigation;
and ordering/rename metamorph.

**Non-scope.** Cross-run logical identity/reconciliation, interactive
merge/split adjudication, user dispositions, and case-grouping accuracy outside
the declared causal fixture families.

**Fixture partition and input.** WP4 owns separate shared-cause,
distinct-cause, lead-only, and metamorphic public fixture slots. Any future
held-out slot requires separate authorization. Every slot receives a fresh
package identity only in its owning work package.

**Independent truth and isolation.** The oracle pre-registers typed causal
conditions, applicability, dependency closures, and expected membership. Names
and expected case IDs are not supplied to grouping logic.

**Expected typed result.**

- Multiple findings sharing one demonstrated stale-reversion cause form one
  supported case and preserve individual finding identities.
- Similar findings involving the same mod or record family but different
  causes form separate cases.
- A hypothesis below threshold forms a separately counted lead-only
  investigation case with no readiness effect.
- Grouping retains explicit shared-cause evidence, membership reason,
  uncertainty, and empty/contradictory evidence.
- Reordering inputs or renaming display values does not alter membership.

**Taxonomy `0.1.0`.** Member assignments remain independent and may be
cross-cutting. Shared taxonomy codes may aid retrieval/presentation but cannot
prove common cause or identity.

**Assertions.** Exact expected partition of findings; zero false merge/split
for the fixture; lead/support distinction; deterministic order-independent
grouping; no name- or taxonomy-only grouping.

**Failure interpretation.** Wrong membership is a causal-grouping error,
separate from whether member findings are correct. Missing shared-cause proof
requires separate/ambiguous grouping, not a guessed merge.

**Replay/privacy.** Fully synthetic and replayable; no real-mod identity in
production grouping rules.

**Passing does not prove.** Cross-run identity/reconciliation, broad real-world
case topology, or interactive merge/split workflow.

## 15. EVAL-0085 — Coverage and no-safety-claim presentation

**Purpose.** Demonstrate that M1 CLI/JSON output reports unlike coverage
populations separately and qualifies no-findings output with gaps and
uncertainty.

**Requirements and decisions.** PROD-004, COVER-001 through COVER-003,
FIND-007, FIND-011, and ADR-0002.

**Scope.** Completed, completed-with-gaps, failed,
skipped-by-configuration, skipped-by-limit, and unsupported analyzer/source
populations; active/partial run; zero-finding run; lead-only run; and
scope-limited targeted run.

**Non-scope.** M2 graphical presentation/usability, empirically calibrated M3
readiness policy, a single aggregate safety metric, or coverage populations
not declared by the fixture.

**Fixture partition and input.** WP4 owns separate coverage-matrix,
zero-finding, partial, and targeted public fixture slots. Any future held-out
slot requires separate authorization. Every slot receives a fresh package
identity only in its owning work package.

**Independent truth and isolation.** A pre-registered population ledger defines
eligible members, denominators, states, exclusions, gaps, findings, leads, and
readiness applicability independently of rendering.

**Expected typed result.**

- Separate labeled counts/percentages for plugins parsed, loose providers,
  supported record families, taxonomy strata, analyzers, and source
  populations applicable to the fixture.
- Explicit state and reason for every configured/excluded population member.
- No combined “analyzed” or “safety” percentage.
- Findings, supported cases, and lead-only investigations counted separately.
- Zero findings rendered as “no unresolved risks within analyzed coverage” or
  a scope-limited/no-readiness state, always with gaps/uncertainty and never a
  safe-playthrough claim.
- Partial/targeted work cannot borrow prior coverage and is labeled
  provisional/incomplete or scope-limited.

**Taxonomy `0.1.0`.** Coverage denominators state taxonomy version and retain
assigned/unknown/unsupported/unmapped/not-applicable populations separately.
Empty taxonomy regions remain visible gaps where relevant.

**Assertions.** Exact denominators and states; no denominator collapse; no
lead/readiness inflation; no hidden failure; no unqualified safety language.

**Failure interpretation.** Wrong arithmetic is a coverage-ledger failure.
Correct arithmetic rendered as a safety claim is a product-semantics failure.

**Replay/privacy.** Fully synthetic; CLI/JSON artifacts are run-owned local
outputs and are not automatically externally shareable.

**Passing does not prove.** M2 UI usability, M3 readiness calibration, or broad
analyzer coverage.

## 16. EVAL-0086 — Taxonomy classification and historical versioning

**Purpose.** Demonstrate exact taxonomy-axis separation, applicability states,
roles, version retention, and resistance to hosting-category/record-family
shortcuts.

**Requirements and decisions.** FIND-001, COVER-002, ANALYSIS-016, taxonomy
`0.1.0`, and the accepted RESEARCH-0021 section 8.3 refinement.

**Scope.** The fixture matrix includes:

1. declared-purpose versus actual-area mismatch;
2. single-purpose/multi-surface;
3. one shared surface affecting materially different areas;
4. cross-cutting multi-area finding;
5. unknown purpose;
6. supported surface with unsupported semantics;
7. a meaningful concept absent from `0.1.0` (`unmapped`);
8. a raw observation for which an impact axis is `not-applicable`;
9. one consequence type represented at low and high severity;
10. localized direct effect with broader causal propagation;
11. hosting-category counterexample;
12. record-family counterexample;
13. historical rendering under the original taxonomy version; and
14. split/merge reclassification that leaves raw evidence unchanged.

**Non-scope.** Claiming taxonomy completeness, accepting a successor product
taxonomy, inferring classification from hosting/file/record labels, or testing
semantic analyzer correctness beyond the independently supplied fixture
evidence.

**Fixture partition and input.** WP4 owns separate taxonomy-axis,
counterexample, state, and history public fixture slots, plus controlled-real
EVAL-0016/0017 classification projections. Any future held-out slot requires
separate authorization. Every slot receives a fresh package identity only in
its owning work package.

**Independent truth and isolation.** An independently reviewed oracle maps each
subject to exact axis/facet/code, applicability state, role, evidence, and
reason. Hosting labels, filenames, record signatures, analyzer ownership, and
expected labels are excluded from the classification input unless explicitly
present as non-authoritative metadata.

**Expected typed result.**

- Exact multi-label assignments for all supported examples.
- Purpose is declared only from applicable author evidence; technical surfaces
  are observed only from qualified local evidence; areas/consequences/extents
  are predicted or established independently.
- Unknown, unsupported, unmapped, and not-applicable remain distinct.
- Severity/confidence/authority/maturity/readiness are not encoded as taxonomy
  assignments.
- Historical assignments retain taxonomy ID/version and raw evidence. A
  current projection creates linked derived assignments and mapping provenance
  without mutating the historical assignment.
- Classification-only variants emit no new candidate, hypothesis, finding, or
  case. Variants that classify a pre-existing finding retain that object's
  identity and change only the linked assignment projection.

Until a real accepted successor to product taxonomy `0.1.0` exists, the
split/merge persistence mechanics use a clearly non-product, project-authored
test taxonomy pair (`infinium.test.taxonomy/1.0.0` and `/2.0.0`). This can
qualify storage/mapping behavior but cannot be cited as an accepted Skyrim
taxonomy revision. An actual product reclassification claim remains gated on a
future accepted taxonomy version.

**Assertions.**

1. No axis assignment is copied from another axis.
2. Hosting/record counterexamples produce the evidence-supported answer.
3. The same consequence code can retain different severities.
4. Local direct extent and causal-propagation extent remain separate.
5. Historical output renders with its original version after a projection.
6. Reclassification does not mutate observations, claims, runs, findings,
   cases, dispositions, readiness evaluations, or exports.

**Failure interpretation.** Any axis collapse, applicability-state collapse,
role promotion, or historical rewrite blocks taxonomy conformance. An
`unmapped` result is correct where the oracle establishes an absent concept.

**Replay/privacy.** Synthetic taxonomy fixtures are tracked. Controlled-real
classification uses the same private payload policy as EVAL-0016/0017.

**Passing does not prove.** Taxonomy completeness, support across all codes,
accepted future taxonomy semantics, or analyzer correctness outside the
fixture matrix.

## 17. Cross-case acceptance and non-claims

This accepted set is the M1 specification baseline. Its acceptance records
owner review of the contracts, claim boundaries, fixture requirements, and
oracle methods; it does not imply that its fixture packages exist or have
passed.

Fixture-package readiness and retained M1 execution remain blocked until:

- every manifest/oracle pair has an assigned owner and partition;
- all synthetic byte/claim generators and expected outputs receive independent
  review;
- held-out slots have sealed, materially independent inputs and oracle
  fingerprints;
- EVAL-0051/0052 positive allowlists match the exact surfaces selected by the
  accepted M1 plan;
- controlled-real dependencies remain privately obtainable and hash-valid, or
  the cases are replaced through review;
- taxonomy assignments receive product-owner review; and
- links and identifier traceability pass repository validation.

Acceptance of the specification still would not pass an evaluation. Passing
the complete set would prove only the bounded M1 contracts exercised by the
accepted M1 plan. It would not establish M3 personal trust, M4 public support,
runtime safety, exhaustive Skyrim semantics, or a guaranteed safe playthrough.

### Protocol `/4` evidence partition amendment

For EVAL-0052 and the applicable EVAL-0086 surface, public conformance remains
the authority for exact product diagnostics, typed AIDT interpretation, and
taxonomy/provenance identifier construction. Held-out truth is limited to the
independently authorable semantic facts in the
[final matrix](../m1-slice4-heldout-oracle-authority-matrix.md). A future report
must state the two evidence partitions separately.
