# M1 Slice 7: Synthetic generic reversion proof

Status: Accepted

Disposition: Owner-accepted planning authority. This document never activates
implementation by itself; a later explicit `docs/current-state.md` activation
must name the WP1 base.

Last reviewed: 2026-08-24

Planning base: exact `main` commit
`0056621464ab2e182d9f320d9d3b1a73bdc2b2b1`

Depends on: accepted M1 plan; accepted M1 process-continuation and
semantic-oracle-deferral amendments; accepted ADR-0001, ADR-0002, ADR-0009,
ADR-0010, ADR-0015, ADR-0022, ADR-0028, ADR-0029, ADR-0034, and ADR-0035;
accepted M1/M2 product-conformance verification profile; and owner-accepted
Slice 6 closeout.

Owner plan acceptance: Accepted by the project owner on 2026-08-24.

## 0. Plain-language outcome and authority

Slice 7 must prove one deliberately narrow idea: the same analyzer can notice
when a later plugin silently restores an older value or relationship that is
outside the later plugin's supported purpose. It must do this in two genuinely
different synthetic domains without embedding actor-specific, placed-object-
specific, fixture, or real-mod rules in the shared mechanism.

The two required domains are:

1. an actor package/AI relationship interpreted together with FaceGen
   appearance evidence; and
2. a placed reference (`REFR`) link or placement relationship.

Each domain needs a supported positive, a structurally matched intentional or
harmless negative, and an ambiguous case that remains visible but does not
become a finding. The proof is product-conformance evidence built and reviewed
by developers. It is not an independent answer key, a held-out verdict, or a
claim that Infinium understands Skyrim broadly.

Plan acceptance approves this written scope but does not by itself activate
implementation. WP1 becomes eligible only after `docs/current-state.md`
records the owner's explicit Slice 7 activation and exact implementation base.
Once activated, the plan authorizes only local, effect-free Slice 7 product
work through the dependency-gated work packages below. It does not authorize:

- private-fixture access;
- archive access or protocol archaeology;
- authoring, sealing, registering, comparing, repairing, or reporting a
  semantic oracle;
- network, provider, credential, billable, external publication, push, or
  other external effects;
- reinterpretation of retained Slice 6 provider results;
- changes to the accepted meaning or bytes of a Slice-frozen Slice 6 contract;
  or
- Slice 8 controlled-real work.

## 1. Accepted meanings and exact exit state

The slice is complete only when one coherent accepted candidate demonstrates
all of the following:

- one category-neutral reversion abstraction and analyzer declaration;
- one actor/AI/FaceGen adapter and one materially different REFR/link/placement
  adapter that feed that same abstraction;
- effect-free retained replay of Slice 6 model-derived claim/application facts
  through the frozen four-axis boundary, kept separate from Slice 7's
  developer-owned expected outcomes and preserved rather than re-adjudicated;
- matched positive, intentional/harmless negative, and ambiguity-abstention
  examples in both domains;
- a supported positive path from retained factual evidence through candidate,
  hypothesis, finding, taxonomy, case, coverage, persistence, replay, JSON,
  and CLI publication;
- no finding, consequence, severity, case, or remediation claim for the
  resolved negative path;
- a retained candidate plus explicit `Abstained` result for ambiguity, with
  exact missing information, zero finding, zero case, and no silent negative;
- rename, unrelated-addition, unrelated-reorder, relevant-winner, malformed,
  and unsupported metamorphic evidence;
- enabled, disabled/skipped, limit/failure-or-cancellation, completion, reopen,
  and replay lifecycle evidence;
- dependency-local invalidation and replay evidence;
- separate coverage denominators and truthful gaps for both domains and every
  applicable analyzer layer;
- a fresh consolidated semantic, security, provenance, contract, and diff
  review followed by correction and re-review on the same candidate;
- the complete accepted verification floor passing once on the final
  review-ready candidate; and
- an implementation record that states the exact bounded claim and the
  absence of any current private or independent semantic verdict.

Final implementation acceptance, which is a later owner decision distinct from
plan acceptance and WP1 activation, freezes the final producer-consumer-
validated Slice 7 contracts and implementation. Passing tests or completing a
work package does not accept the slice by itself.

## 2. Required reading for implementation and review

An implementation or review agent must begin with:

1. repository `AGENTS.md`;
2. `docs/README.md`, `docs/current-state.md`, and
   `docs/execution-policy.md`;
3. the accepted M1 plan and both accepted amendments named above;
4. the Slice 6 entry and its closeout section in the implementation record;
5. this compact entry and this full plan;
6. the relevant accepted product requirements, domain model, analysis
   catalog, candidate-input/expansion, taxonomy, severity/confidence/coverage,
   and scope documents;
7. the ADRs named in this plan; and
8. `evaluation-strategy.md`, `case-catalog.md`, `fixture-guidelines.md`,
   `anti-overfitting-rules.md`, `product-evaluator-boundary.md`, and the
   continuation verification profile.

Historical implementation records are chronology, not current authority.
Private repositories, retired evaluator paths, external archives, and
historical semantic packages are not implementation inputs.

## 3. In scope and excluded

### In scope

- deterministic local analysis of bounded, typed, snapshot/context/config-
  bound facts already admitted to the product pipeline;
- category-neutral comparison of a qualified prior feature with the winning
  feature and the winning contribution's supported applicable purpose;
- actor-domain interpretation of an NPC `PKID` package relationship with
  FaceGen applicability/provider evidence and appearance purpose;
- REFR-domain interpretation limited to accepted M1 support for `NAME`,
  `XLKR`, `XLRL`, `XOWN`, and `DATA`, using only the subset needed by the
  synthetic proof;
- deterministic developer-owned fixtures and tests;
- existing finding, case, taxonomy, coverage, persistence, replay, output, and
  CLI seams, with additive Slice 7 contracts only where the current seams
  cannot carry the accepted meaning truthfully;
- migration and retained-read compatibility if a new stored contract or
  successor aggregate is actually required; and
- effect-free documentation, local verification, and owner handoff.

### Excluded

- quest, alias, `QUST`, `ALFR`, script, runtime-engine, save-state, rendering,
  or progression semantics;
- broad REFR correctness or spatial reasoning beyond the accepted M1 field
  subset;
- archive-backed, real-mod, controlled-real, private, held-out, adversarial,
  or independently authored semantic evaluation;
- model calls, search, Nexus, LOOT, credentials, network, billing, or live
  provider work; Slice 7 uses only effect-free retained/replayed admitted
  product facts;
- semantic tuning or selection against retained Slice 6 model output;
- a safety, compatibility, reliability, precision, recall, production-
  readiness, or broad Skyrim-understanding claim;
- user-facing M2 UX, M3 trusted-preflight maturity, or M4 distribution; and
- any compatibility shim that weakens a closed current contract or silently
  treats an unsupported region as harmless.

## 4. Frozen predecessor boundary and contract maturity

Slice 6 is complete. The following Slice-6-owned contract families are
`Slice-frozen` and may not be edited in place, weakened, or assigned new
meaning by Slice 7:

1. `provider-access-profile.v1`;
2. `provider-operation.v1`;
3. `provider-response.v1`;
4. `source-claim-extraction.v1`;
5. `candidate-investigation.v1`;
6. `provider-execution-input.v1`;
7. `effective-scan-configuration.v2`;
8. `run-output.v2`; and
9. `cli-summary.v2`.

Slice 7 consumes only admitted product facts exposed by current product
contracts. Retained provider provenance may remain visible, but the analyzer
must not reopen the provider operation, reinterpret unsupported/contradicted/
abstained states, or infer applicability from a proposal or extraction result.
ADR-0034's four decisions remain independent: proposal/extraction, support,
applicability, and the host product decision.

New Slice 7 contracts begin `Proposed`. WP1 may move them to
`Implementation-active` only while their real producers and consumers are
being implemented. They reach `Producer-consumer-validated` only after schema
and typed invariants, producer, consumer, persistence, round trip, invalid-
state, fixture, replay, and output evidence agree. Final implementation
acceptance may then move the final identities to `Slice-frozen`. Schema presence or unit tests
alone cannot advance maturity.

The preferred path is a new local analyzer and domain adapters that emit the
existing typed candidate and finding inputs without changing frozen aggregate
meaning. If accepted meaning cannot be represented, WP1 must introduce an
additive Slice 7 contract or a clean-break successor version and update every
producer, consumer, persistence, migration, replay, projection, JSON, CLI,
and test seam together. It must preserve exact retained predecessor bytes and
fail closed on mixed or drifted versions. It may not mutate a frozen schema in
place or add an unversioned side channel.

WP1 must record a contract-impact matrix covering at least:

| Seam | Required decision |
| --- | --- |
| delivered factual input | reused unchanged or exact additive successor |
| execution composition | execution input, effective configuration, source registration, exact declaration admission/fingerprint, assignment admission, and enabled/disabled behavior |
| analyzer declaration/binding | stable ID, analyzer, semantic, identity, and ruleset versions |
| candidate/hypothesis | representation of positive, resolved negative, ambiguous, unsupported, invalid, and failed states |
| conclusion input | evidence, shared cause, extent, symptom, recommendation, and validation ownership |
| taxonomy | independent axes, role, applicability, evidence, and explicit gaps |
| finding/case | promotion, causal grouping, ambiguity abstention, and continuity identity |
| coverage | separate populations, denominators, member states, failures, and gaps |
| persistence/replay | retained bytes, dependency edges, invalidation, reopen, backup/restore, and drift failure |
| publication | JSON, CLI, claim boundary, raw candidates, abstentions, failures, and gaps |

Any necessary clean-break successor is a coordinated Slice 7 revision, not a
waiver of predecessor maturity.

## 5. Category-neutral semantic model

The shared mechanism must work with neutral concepts rather than record-type
or fixture-specific rules. In plain language, it asks whether a winning
contribution changed a previously established feature, whether that change
looks like a return to an older or absent state, and whether the winner's
supported purpose actually covers that change.

The implementation may choose names that fit the codebase, but its typed
model must distinguish at least:

- **subject**: the affected entity or bounded relationship locus;
- **feature**: the comparable value or relationship under analysis;
- **prior effective state** and **winning state**: typed states with retained
  contribution and evidence identities;
- **transition**: unchanged, changed, absent, unresolved, unsupported, or
  invalid; a mere difference is not automatically a reversion;
- **purpose scope**: the supported and applicable declared purpose relevant to
  the winning contribution;
- **coverage relation**: covers the transition, does not cover it, conflicts,
  or cannot be decided;
- **causal/dependency closure**: exact facts and edges on which the decision
  depends; and
- **domain interpretation**: an adapter-owned explanation and taxonomy input,
  not a branch in the generic admission rule.

Adapters may map stable domain meaning into neutral subjects, features,
states, purpose dimensions, evidence, and dependency identities. They may not
emit an answer-bearing `scope-incongruent`, positive, negative, harmless,
ambiguous, or final-disposition label. The shared mechanism itself must derive
transition kind, purpose coverage, contradiction effect, causal closure, and
the one closed disposition from neutral inputs. A generic wrapper around a
domain-precomputed answer does not satisfy this slice.

A supported positive requires all of the following:

1. comparable prior and winning states from the accepted typed substrate;
2. evidence that the prior feature was established in the qualified earlier
   contribution;
3. evidence that the winner removes, replaces, omits, or restores that feature
   in the exact accepted reversion shape;
4. a supported and applicable purpose for the winner;
5. evidence that the lost feature is outside that purpose;
6. no defeating evidence that the removal or replacement was intentional;
7. a closed affected locus, bounded consequence, recommendation, validation,
   and dependency identity; and
8. explicit coverage for every population that contributed to the claim.

The generic rule must not infer intent from plugin names, record signatures,
load order, field presence, taxonomy codes, or fixture membership. Ranking may
order optional work but may not admit, suppress, or promote a mandatory case.

The closed rule set must have a machine-checkable totality table. Exhaustively
enumerate every admitted combination of transition state, claim support,
applicability, purpose coverage, contradiction state, causal closure,
publication eligibility, coverage state, and gap/failure state. Exactly one
disposition must match each combination. The validator must reject uncovered,
overlapping, contradictory, or unpublishable combinations before fixture tests
run. Example cases demonstrate product meaning; they do not substitute for
totality.

Disposition is closed:

| Evidence state | Product behavior |
| --- | --- |
| positive requirements closed | candidate and hypothesis may promote to one supported finding and causal case |
| same structural change, supported applicable purpose covers intentional removal/replacement | retain the candidate, contradiction, rejected/resolved hypothesis, purpose and observed taxonomy; problem consequence and effect extent are `NotApplicable`; no finding, case, severity, remediation, or readiness effect |
| purpose, applicability, comparison, or causal evidence ambiguous | retain candidate and explicit `Abstained` result with exact missing information; zero finding and zero case |
| semantic field/domain outside accepted support | retain lower-layer observation and an unsupported gap; make no higher semantic claim |
| malformed or internally inconsistent input | reject atomically at the owning boundary; no partial semantic output |
| analyzer failure or limit | explicit failure/limit and coverage effect; never a safety claim |

## 6. Required domain packages

### 6.1 Actor, AI package, and FaceGen

The positive package follows EVAL-0001's accepted shape. A qualified prior NPC
contribution supplies an AI package relationship. A later appearance-scoped
winner supplies the relevant appearance values and applicable loose FaceGen
assets but loses or restores the earlier `PKID` relationship. Supported and
applicable documentation establishes the winner's appearance purpose; it does
not establish an AI-package removal purpose.

The supported result is exactly one `Strongly supported`, `Moderate`
`scope-incongruent-reversion` finding and one causal case for the synthetic
cause. The worst credible consequence is a bounded functional change,
producing that severity/confidence pair only when its typed evidence is
closed. Static reversion may be established; the runtime symptom remains
predicted. The recommendation must explain a reversible patch or winner
correction and an exact static validation step. Rendering, archive, runtime,
unqualified-NPC, and unevaluated-population gaps remain explicit.

The matched negative must preserve the same override and relationship-change
shape while supported applicable evidence says the winner intentionally
replaces or removes the AI package relation. It retains the candidate, direct
contradiction, rejected/resolved hypothesis, purpose taxonomy, and observed
surface/delivery taxonomy. Problem consequence and effect-extent assignments
are explicitly `NotApplicable`; there is no finding, case, severity,
remediation, or readiness effect. The ambiguity member removes or makes
inapplicable the purpose evidence and must retain a candidate plus needs-input
`Abstained` result, with zero finding and zero case.

### 6.2 REFR link or placement

The second package must use the same neutral transition and purpose-coverage
rule but a materially different domain adapter. Its required positive is a
placed reference whose qualified prior contribution retains an `XLKR` linked-
reference relationship and whose winner makes a documented position-only
`DATA` placement adjustment while dropping or restoring that unrelated link.
The accepted M1 `REFR` substrate also includes `NAME`, `XLRL`, and `XOWN`, but
the positive must use only the smallest `DATA` plus `XLKR` subset needed for
the closed proof. Enable-parent semantics are excluded because no supporting
enable-parent field is admitted by this exact proof. Quest, alias, script,
runtime, and progression semantics are also excluded.

The positive winner has a supported applicable position-only purpose that is
incongruent with the lost linked relationship. The result is one bounded
finding and one cause-based case, with the placed-object/activation area,
plugin-data realization, bounded subject and dependent extents, a predicted
functional symptom, and a reversible correction plus static validation. Any
cell-wide, runtime, quest, alias, script, or progression consequence remains
unknown, unsupported, or not applicable as appropriate.

When the typed evidence closes exactly as planned, the REFR positive is one
`Strongly supported`, `Moderate` finding. It establishes
`area.world.placed-objects-activation`, observes `surface.plugin-data` and
`delivery.plugin-container`, predicts
`consequence.incorrect-functional-behavior`, predicts
`extent.subject.single-instance`, `extent.spatial.single-reference-or-point`,
and `extent.persistence.installation-persistent`, and leaves propagation
unknown or unsupported unless separate typed evidence closes an accepted code.
No purpose, consequence, or extent is inferred from `EDID`, field signature,
load order, or the mere presence of `XLKR`/`DATA`.

Its matched negative preserves the same `DATA`/`XLKR` structural change but
supplies supported applicable intent covering the linked-reference removal or
replacement. Its ambiguity member withholds a required purpose, applicability,
link-resolution, or placement fact and retains an `Abstained` result with zero
finding and zero case. Outcomes match the closed disposition table above.

### 6.3 Shared-mechanism proof

The implementation record and tests must prove that:

- both adapters emit the same neutral contract and call the same generic
  admission/evaluation mechanism;
- no generic type, stable ID, rule, threshold, production string, or branch
  contains fixture names, real-mod names/IDs, `NPC_`, `PKID`, `FaceGen`,
  `REFR`, `XLKR`, `XLRL`, `XOWN`, `NAME`, or `DATA` as a domain selector;
- domain tokens occur only in extractors/adapters, accepted typed facts,
  presentation, or domain-specific conformance data;
- actor and REFR occurrences remain separate causal cases unless evidence
  proves one real shared cause; sharing an analyzer family is insufficient;
- removing either adapter does not require changing the generic rule; and
- the proof supports only these two qualified synthetic domains and field
  subsets.

## 7. Findings, taxonomy, grouping, coverage, and claim boundaries

The finding path must preserve evidence rather than translate an analyzer
guess directly into user truth.

- Candidate and hypothesis identities derive from semantic participants,
  transition, analyzer contracts, purpose/application evidence, and dependency
  closure, never display names or encounter order.
- Promotion requires supporting evidence, no defeating contradiction, no
  missing required information, closed severity, and closed finding/case
  identities.
- Cases group by proven shared cause. Same plugin, record family, taxonomy
  assignment, or analyzer family is not a grouping reason.
- Slice 7 ambiguity hypotheses are explicitly `Abstained` and do not enter any
  finding or lead-only case. This slice does not use lead-only ambiguity as an
  alternative outcome.
- Recommendations state action, uncertainty, reversibility, risks, and a
  verification step. They must not claim runtime repair or guaranteed safety.

Taxonomy assignments keep the accepted axes independent and retain their
declared, observed, predicted, or established role. The actor proof is expected
to exercise appearance purpose, plugin-data and asset surfaces, loose-data and
plugin-container delivery, actor AI-package and appearance areas, bounded
functional consequence, and bounded extents where evidence supports them. The
REFR proof is expected to exercise world/placed-object purpose and area,
plugin-data/plugin-container realization, predicted bounded functional
consequence, and subject/propagation extents. Exact codes must come from the
accepted taxonomy and typed evidence; the adapter must emit unknown,
unsupported, unmapped, or not-applicable instead of guessing.

Coverage is not one percentage. At minimum, retain distinct populations for:

- actor transition candidates;
- actor purpose/applicability evidence;
- actor conclusion/taxonomy promotion;
- REFR transition candidates;
- REFR purpose/applicability evidence;
- REFR conclusion/taxonomy promotion; and
- publication/replay of the complete run.

These Slice 7 analyzer populations are additive to ADR-0028's fixed bounded-M1
ten-population registry. Every bounded M1 snapshot must still retain, persist,
export, replay, and test all ten registry rows, including completed rows with a
zero denominator. Slice 7 populations have their own stable versioned
identities and separate denominators; they neither replace nor merge the fixed
rows.

Each population has a denominator, member states, exclusions, failures, gaps,
and taxonomy coverage. A zero-finding result never means safe. Partial or
unsupported work must remain visible in JSON and CLI output and must make the
publication claim boundary narrower, not disappear.

The maximum accepted Slice 7 claim is:

> On the retained developer-owned synthetic examples, one category-neutral,
> deterministic local analyzer distinguished a supported scope-incongruent
> reversion from a matched intentional/harmless change and abstained on
> ambiguity in both the qualified actor/AI/FaceGen and REFR/link/placement
> domains, while preserving typed provenance, coverage, persistence, replay,
> and publication behavior.

It does not establish independent semantic correctness, held-out performance,
broad generalization, real-mod behavior, controlled-real behavior, runtime
effects, compatibility, safety, production readiness, or M3 trust.

## 8. Complete evidence and dependency flow

The accepted candidate must exercise this entire path:

1. snapshot-, context-, configuration-, and run-bound Bethesda facts plus
   admitted documentation/application facts;
2. actor and REFR adapters that validate their accepted domain support and
   create neutral feature-transition inputs;
3. the one generic analyzer declaration, bounded population, decisions,
   candidates, hypotheses, resolved negatives, abstentions, gaps, failures,
   bindings, counts, and dependency edges;
4. analyzer-owned conclusion evidence, shared-cause proof, recommendation,
   taxonomy evidence, and separate coverage facts;
5. finding promotion, ambiguity-abstention handling, taxonomy assignment, causal
   case grouping, continuity/reconciliation, and claim boundary;
6. authoritative persistence of exact payload bytes and dependency identities;
7. clean reopen, incremental invalidation, retained downstream replay,
   backup/restore, and version/fingerprint drift rejection; and
8. run output, CLI summary, raw-artifact listing, and semantic projection.

Every derived node must have a traceable source or dependency edge. A separate
effect-free regression must replay the retained Slice 6 model-derived claim and
application facts through their frozen four-axis boundary and record the
result alongside, but not as expected truth for, the two Slice 7 domain
packages. Those retained facts keep their exact proposal/extraction, support,
applicability, host-decision, source, passage, revision, transcript/response,
run, snapshot, context, and evidence provenance. They influence a Slice 7
domain conclusion only if the already admitted fact is actually bound and
applicable to that exact synthetic subject; shared vocabulary or a convenient
label is insufficient. Slice 7 neither performs a provider operation nor
re-adjudicates the frozen decision. Developer-owned expected outcomes remain
separate from model-derived product facts. No conclusion may be reconstructed
from display text during replay.

The required model-derived predecessor path is the accepted Slice 6 chain:
retained WP10 provider response and narrow source-claim proposal -> host-owned
support decision -> analysis-run-owned source-application applicability
decision -> host admission or abstention -> retained WP11 consumption/replay.
WP1 must resolve the exact current product artifact, claim, application,
response/transcript, run, snapshot, context, decision, and digest identities
from authoritative retained state, not from Git history or display text. The
downstream deterministic result and all four axes must be recorded. If no
already admitted fact is applicable to an exact Slice 7 synthetic subject,
record that truthful non-applicability and continue with the separate
developer-owned domain proof. Do not invent applicability, reuse a merely
similar claim, call the provider, or silently treat retained model output as
expected truth. Stop only if the authoritative retained chain itself cannot be
resolved and replayed without changing a frozen boundary or crossing a
prohibited-access/effect boundary.

Every Slice 7 evidence and dependency reference must resolve to retained,
queryable bytes through the owning artifact readback plus list/get APIs after
initial publication, reopen, backup/restore, clean replay, incremental replay,
and retained-downstream replay. Opaque dangling IDs are invalid. If the current
publication model has no truthful collection for an observation, the
implementation must populate an existing collection only according to its
accepted meaning or introduce a coordinated clean-break successor projection;
it may not publish an unresolvable placeholder.

Relevant-winner mutation must invalidate only the dependent transition,
candidate, conclusion, case, taxonomy, coverage, and publication nodes. A
rename, unrelated fact insertion, or unrelated input reorder must preserve the
semantic projection. Dependency, analyzer, policy, threshold, contract, or
taxonomy-version drift must fail closed or force the exact accepted replay,
never silently reuse stale output.

## 9. Required developer-owned cases and metamorphic matrix

Fixtures use opaque synthetic identities. Expected behavior comes from the
accepted requirements and explicit developer reasoning and is reviewed with
the implementation. Product output must not author expected truth.

Before any fixture is first executed, register it explicitly as
`developer-owned product-conformance evidence`, never held-out, private,
independent, or oracle evidence. In a reviewable artifact written before that
first execution, record its exact input identity and bytes/fingerprint,
expected deterministic observations, candidate disposition, hypothesis state,
finding/case outcome, taxonomy roles/applicability, coverage/gaps, and the
accepted-authority reasoning for each expectation. Subsequent output cannot be
copied back into or used to repair those expectations; a genuine authority
change requires explicit review and retained chronology.

The minimum case matrix is:

| Domain | Positive | Matched negative | Ambiguity |
| --- | --- | --- | --- |
| actor/AI/FaceGen | appearance-scoped winner loses qualified `PKID`; one supported finding/case | same structural loss with supported applicable AI-removal/replacement purpose; resolved negative | purpose/applicability or comparable evidence missing; candidate plus `Abstained`; zero finding/case |
| REFR/link/placement | incongruent winner loses a qualified supported relationship; one bounded finding/case | same structural loss with supported applicable relationship-removal/replacement purpose; resolved negative | purpose/applicability/link/placement evidence missing; candidate plus `Abstained`; zero finding/case |

Register before execution and run a bounded lifecycle package across the
production composition path. It must cover applicable enabled work,
configuration-disabled/skipped work, a limit plus failure or cancellation,
successful completion, retained reopen, clean replay, incremental replay, and
retained-downstream replay. Raw decisions, candidates/abstentions/failures,
terminal states, exact work counts, separate coverage member states, gaps, and
replay effects must remain visible at every transition. Cancellation, failure,
or limit cannot become a resolved negative, safety claim, or complete coverage.

Each domain must run this metamorphic matrix:

| Transformation | Required invariant or change |
| --- | --- |
| opaque display rename | same semantic decisions, identities, grouping, coverage, and projection; only allowed display provenance changes |
| unrelated fact/mod addition | original result unchanged; unrelated population is counted and cannot join the case |
| unrelated input reorder | canonical bytes or semantic projection remain stable according to the owning contract |
| relevant winner restores the qualified feature | only dependent positive path resolves; no finding/case remains for that cause |
| relevant winner creates the qualified reversion | only dependent path gains candidate, finding, taxonomy, and case output |
| malformed required field/state/version | atomic contract/codec rejection; no partial conclusion or publication |
| supported lower-layer fact with unsupported semantic region | raw fact retained with exact unsupported gap; no semantic guess |
| purpose or applicability ambiguity | candidate retained; explicit `Abstained` and missing information; zero finding/case and no silent negative |

Additional cross-domain checks must exchange encounter order and synthetic
opaque identities, prove no cross-domain case grouping, scan production code
and generic type/ID strings for fixture/real-mod leakage, and show that ranking
does not remove a mandatory positive, negative, or ambiguous member.

Measured fixture sizes, population counts, and elapsed times are recorded.
They are proof bounds, not universal scale thresholds.

## 10. Common implementation and review rules

Use one mutable working candidate. For each work package:

1. confirm the predecessor gate and clean intended diff;
2. implement the complete affected vertical path, not an isolated schema or
   producer;
3. run focused checks while developing;
4. inspect generated contracts, retained bytes, SQL state, JSON/CLI output,
   replay, and changed paths directly;
5. conduct one consolidated semantic/security/provenance/contract/diff review;
6. classify findings as must-fix, follow-up, non-blocking, owner/authority
   decision, or safety/isolation breach;
7. batch corrections on the same candidate and rerun focused checks plus
   changed-surface review; and
8. update the append-only Slice 7 implementation record with exact evidence.

Do not create freeze/bind/record churn for intermediate corrections. If the
same conceptual defect recurs after two completed correction attempts, pause
that path for explicit design diagnosis. Escalate only when accepted authority
is conflicting or materially incomplete, scope or authority must expand, an
owner-controlled dependency remains unavailable after safe alternatives, or
continuation would cross a security, private-answer, protected-root,
destructive, or external-effect boundary.

Fresh reviewers are read-only. They receive the exact candidate and plan,
must not access private fixtures or archives, and must state that no current
semantic oracle or private held-out verdict was used.

## 11. Work-package sequence

The packages are sequential because later contracts and evidence depend on
earlier ones:

`WP1 -> WP2 -> WP3 -> WP4 -> WP5 -> WP6 -> WP7`.

Owner acceptance of this plan approves the full WP1-through-WP7 scope. The
separate activation recorded in `docs/current-state.md` opens the full Slice 7
implementation authority on an exact base, with WP1 as the starting package
rather than the maximum authorized scope or a recurring human approval gate.
Completion and recorded review of each package automatically open its next
package. The orchestrator continues without owner intervention through WP7
unless an escalation condition in Section 10 genuinely occurs. A package may
combine implementation commits on one working candidate, but its acceptance
evidence remains distinct.

### `M1/S7/WP1` — Contract impact, neutral model, and analyzer declaration

Deliver:

- the contract-impact matrix in Section 4;
- exact effect-free resolution and replay of the retained Slice 6 WP10 support
  -> source-application applicability -> host-decision -> WP11 consumption
  path, recording truthful non-applicability to Slice 7 subjects where it
  exists and escalating only if the authoritative chain cannot be safely
  resolved/replayed;
- the smallest typed neutral transition model and closed dispositions;
- stable analyzer family/ID/version, semantic version, identity version,
  ruleset, policy, thresholds, limits, supported shapes, exclusions,
  dependencies, output kinds, maturity, evaluation links, and local-only
  boundary declaration;
- a machine-checked exhaustive totality table proving exactly one disposition
  for every admitted state combination and rejecting gaps/overlaps before
  fixture execution;
- schema/codec/migration work only if the existing seam cannot truthfully carry
  the accepted meaning; and
- positive, negative, malformed, unknown, unsupported, canonicalization, and
  round-trip contract tests.

Exit gate: design-only contracts remain `Proposed`. A contract advances to
`Implementation-active` in WP1 only if that package contains its real
production producer and consumer; otherwise WP2 or the first package that
implements both advances it. All frozen families are byte/meaning-preserved,
invalid mixed versions fail closed, and fresh contract/provenance review has
no remaining must-fix. No contract becomes `Producer-consumer-validated`
before WP5/WP6 close persistence, replay, publication, invalid-state, and
fixture evidence.

### `M1/S7/WP2` — Actor adapter and generic analyzer vertical slice

Deliver:

- the actor/AI/FaceGen adapter;
- the shared generic analyzer over the neutral model;
- the actor positive, matched negative, and ambiguity examples;
- exact EVAL-0001/0002 typed outcomes: one `Strongly supported`, `Moderate`
  positive finding/case; a negative retaining candidate, contradiction,
  rejected/resolved hypothesis, purpose/observed taxonomy, and explicit
  not-applicable consequence/extent without finding/case; and an `Abstained`
  ambiguity with zero finding/case;
- production registration/composition through the admitted execution-input
  analyzer set, effective configuration, source list, exact declaration
  identity/fingerprint, and work assignment rather than direct test-only
  invocation;
- independently runnable enabled and disabled pairs, with disabled/skipped
  analyzer and population coverage reported truthfully;
- explicit decisions, raw candidates, hypotheses, abstentions, gaps, counts,
  and dependency edges; and
- EVAL-0001, EVAL-0002, EVAL-0032, EVAL-0065, ADR-0034 four-axis, and actor
  metamorphic focused evidence.

Exit gate: the actor package reaches correct candidate dispositions through
the production composition path without fixture/name/ID rules; exact execution
input and composed sources agree; enabled and disabled runs behave and report
coverage truthfully; no finding is yet claimed from incomplete conclusion
evidence; and fresh semantic/anti-overfitting review has no remaining must-fix.

### `M1/S7/WP3` — REFR adapter and cross-domain generalization

Deliver:

- the REFR/link/placement adapter over the accepted M1 field subset;
- the REFR positive, matched negative, and ambiguity examples;
- both adapters invoking the same generic analyzer;
- production composition with either adapter enabled or disabled independently
  and with both enabled, preserving exact declaration-set admission and
  truthful skipped/unsupported coverage;
- cross-domain identity, grouping, ordering, ranking, and contamination tests;
  and
- a mechanical production-code scan plus reviewer inspection for fixture,
  real-mod, and domain-selector leakage in the generic mechanism.

Exit gate: matched outcomes exist across both materially different domains,
the generic rule remains category-neutral, and unsupported REFR regions are
visible gaps rather than inferred results. If successor candidate or finding
contracts were necessary, hard-coded analysis-publication admission and every
other producer/consumer check accept only the coordinated exact successor.

### `M1/S7/WP4` — Findings, taxonomy, cases, coverage, and claim boundary

Deliver:

- analyzer-owned conclusion, symptom, extent, recommendation, and validation
  facts for supported positives;
- explicit absence of promotion facts for resolved negatives and `Abstained`
  behavior with zero finding/case for ambiguity;
- evidence-grounded taxonomy facts with independent axes and explicit
  unknown/unsupported/unmapped/not-applicable states;
- one cause-based case per supported synthetic cause and no grouping by mod,
  record family, taxonomy, or analyzer alone;
- separate coverage populations and truthful partial/gap behavior; and
- EVAL-0084, EVAL-0085, EVAL-0086 and FIND/EVID/COVER focused tests.

Exit gate: both positives produce exactly their bounded finding and case,
matched negatives produce none, ambiguities remain visible without promotion,
and the publication claim is no broader than Section 7.

### `M1/S7/WP5` — Persistence, invalidation, replay, and publication

Deliver:

- authoritative write/read of every new or affected payload;
- SQL constraints and migration/rollback-read strategy if storage changes;
- clean, incremental, retained-downstream replay, reopen, backup/restore, and
  dependency/analyzer/policy/threshold/contract/taxonomy drift tests;
- relevant-winner dependency-local invalidation;
- raw candidate, negative, abstention, failure, coverage, gap, finding,
  taxonomy, case, recommendation, and claim-boundary projection in JSON/CLI;
- retained-byte resolvability for every evidence/dependency ID through
  readback and artifact list/get APIs after publication, reopen,
  backup/restore, and every replay mode, plus dangling-reference rejection;
  and
- evidence that all external boundaries remain `NotUsed`.

Exit gate: the full evidence graph survives persistence and replay, unrelated
changes preserve the semantic projection, relevant changes affect only their
dependency closure, and retained Slice 6 bytes remain readable and unchanged.

### `M1/S7/WP6` — Accumulated synthetic conformance package

Deliver:

- the complete two-domain case and metamorphic matrix;
- the pre-registered enabled, disabled/skipped, limit/failure-or-cancellation,
  completion, reopen, and replay lifecycle package with raw state/coverage
  evidence;
- exact fixture identities, measured populations, counts, pass/fail/skip
  totals, elapsed times, hashes, output artifacts, unsupported surfaces, and
  gaps;
- focused `Contracts`, `Candidates`, `Cases`, `Replay`, `Output`, and `Safety`
  gate receipts; WP6 must not run `Gate All` or the complete floor;
- traceability to ANALYSIS-003 through ANALYSIS-005, ANALYSIS-016,
  ANALYSIS-017, EVID-001 through EVID-007, FIND-001 through FIND-004,
  FIND-011, FIND-014, COVER-001 through COVER-003, EVAL-0001, EVAL-0002,
  EVAL-0032, EVAL-0065, EVAL-0067, and EVAL-0083 through EVAL-0086; and
- an explicit statement that the package is developer-owned conformance
  evidence, not an oracle or verdict.

Exit gate: all six continuation-profile layers are represented, with Layer 5
containing matched positives and negatives across both required domains.

### `M1/S7/WP7` — Consolidated review, correction, final floor, and handoff

Deliver:

- fresh read-only reviews of plan fidelity/product meaning, contract and
  vertical-path completeness, semantic/anti-overfitting/claim boundaries,
  security/provenance/isolation, tests, and exact diff;
- one consolidated finding ledger with classifications and dispositions;
- same-candidate corrections and focused rechecks until no must-fix remains;
- one final complete accepted floor on the exact review-ready candidate;
- a contract-maturity ledger proving producer-consumer-validated status before
  any proposed freeze;
- the complete implementation record required by Section 14; and
- an owner decision handoff that requests acceptance without self-accepting,
  pushing, or opening Slice 8.

Exit gate: no remaining must-fix or unexplained verification gap, exact clean
candidate identity and receipts retained, and owner decision pending.

## 12. Focused and complete verification

Focused development commands must select nonzero tests and include exact new
Slice 7 filters or traits created by the implementation. At minimum, the
implementation record must show focused contract/codec, generic-analyzer,
actor, REFR, conclusion/taxonomy/case, persistence/replay, output/CLI,
metamorphic, security, and fault tests. The following existing gate entry
points remain mandatory where applicable:

```powershell
$proofRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("infinium-m1-s7-" + [guid]::NewGuid().ToString("N"))
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate Contracts -OutputRoot $proofRoot
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate Candidates -OutputRoot $proofRoot
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate Cases -OutputRoot $proofRoot
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate Replay -OutputRoot $proofRoot
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate Output -OutputRoot $proofRoot
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate Safety -OutputRoot $proofRoot
```

After every focused or complete local verification run completes, is
cancelled, or times out, follow the exact repository-scoped Windows procedure
in `docs/execution-policy.md` under **Test-process cleanup and verification**.
Use absolute project paths for test commands, allow ordinary shutdown first,
confirm no other active process in this worktree owns a match, snapshot and
immediately revalidate only `dotnet.exe`, `testhost.exe`, or
`testhost.x86.exe` command lines containing the resolved exact repository root,
and retain the resolved root, matched PID/name set, and zero-survivor result.
Never kill by process name or a broader workspace/user predicate.

Once WP7's candidate is review-ready, commit the exact product candidate and
prove the worktree clean. Run the complete floor once from the repository root
on that exact clean committed product candidate:

```powershell
$repositoryRoot = (Resolve-Path -LiteralPath (git rev-parse --show-toplevel)).Path
$solution = Join-Path $repositoryRoot 'Infinium.sln'
$dependencyCheck = Join-Path $repositoryRoot 'eng/update-dependency-manifest.ps1'
$pipelineCheck = Join-Path $repositoryRoot 'eng/verify-analysis-pipeline.ps1'
dotnet restore $solution --locked-mode --nologo
dotnet build $solution -c Release --no-restore --nologo
dotnet test $solution -c Release --no-build --nologo --filter "TestCategory=Unit"
dotnet test $solution -c Release --no-build --nologo --filter "TestCategory=Contract"
dotnet test $solution -c Release --no-build --nologo --filter "TestCategory=Integration"
dotnet test $solution -c Release --no-build --nologo --filter "TestCategory=Evaluation"
dotnet test $solution -c Release --no-build --nologo --filter "TestCategory=Security"
dotnet test $solution -c Release --no-build --nologo --filter "TestCategory=Fault"
dotnet test $solution -c Release --no-build --nologo
dotnet format $solution --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File $dependencyCheck -Check
$finalRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("infinium-m1-s7-final-" + [guid]::NewGuid().ToString("N"))
powershell -NoProfile -ExecutionPolicy Bypass -File $pipelineCheck -Gate All -OutputRoot $finalRoot
git diff --check
git status --short
```

The final implementation record may be appended only after the passing floor.
Bind that record in a documentation-only handoff commit and retain both the
tested product-candidate commit and handoff commit. Prove that no product,
contract, schema, fixture, test, tool, dependency, or generated product byte
changed between them, using an exact changed-path inventory and retained
product-path digest/manifest comparison. Any such byte drift creates a new
product candidate and invalidates the earlier final-floor binding.

If the complete floor fails, the candidate was not final. Correct the same
candidate, rerun focused checks and changed-surface review, then attempt one
new final floor. Do not weaken tests, skip mandatory categories, or bind a
failed candidate.

Planning-package verification before owner decision is documentation-only:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-documentation.ps1
git diff --check
git status --short
```

## 13. Requirement and evaluation traceability

| Slice obligation | Primary authority | Required evidence |
| --- | --- | --- |
| generic scope-incongruent reversion | ANALYSIS-003 through ANALYSIS-005, ANALYSIS-016, ANALYSIS-017; M1 Slice 7 | one neutral analyzer and both domain adapters |
| actor positive/negative/ambiguity | EVAL-0001, EVAL-0002 | matched actor package plus abstention |
| bounded candidate construction | EVAL-0032, EVAL-0065 | exact population/disposition counts, no all-pairs or score-based suppression |
| typed provenance and abstention | EVID-001 through EVID-007, EVAL-0067, EVAL-0083, ADR-0001/0002/0029/0034 | complete evidence graph, four-axis admission, raw gaps and abstentions |
| causal cases and continuity | FIND-001 through FIND-004, FIND-011, FIND-014, EVAL-0084, ADR-0022 | cause proof, separate cases, rename/order stability, replay reconciliation |
| honest taxonomy | EVAL-0086 and accepted taxonomy | evidence-grounded independent axes and explicit gap states |
| honest coverage | PROD-002, PROD-004, COVER-001 through COVER-003, EVAL-0085 | separate denominators/member states; no safety percentage |
| two-domain generalization | M1 Slice 7 and continuation Layer 5 | matched positive/negative/ambiguity packages in actor and REFR domains |
| no independent verdict | ADR-0035 and continuation profile | record statement; no private/oracle/archive access or comparison |

EVAL-0016 and EVAL-0017 remain Slice 8 controlled-real obligations. Slice 7
may preserve their linked declaration references but may not claim they have
passed.

## 14. Implementation record requirements

Create `docs/plans/milestones/m1/slices/s7/record.md` when implementation
begins. Keep it append-only after each accepted package boundary and retain:

- exact base, working candidate, final candidate, and handoff identities;
- changed-path inventory and contract-impact/maturity ledgers;
- analyzer declaration identity, canonical bytes/fingerprint, versions,
  thresholds, limits, dependencies, exclusions, maturity, and boundary states;
- exact fixture/package identities for both domains without real-mod names;
- positive, negative, ambiguity, malformed, unsupported, lifecycle, and
  metamorphic expected reasoning;
- source facts, admitted claim/application provenance, candidates, hypotheses,
  contradictions, abstentions, findings, cases, taxonomy, coverage, gaps,
  recommendations, persistence, replay, output, and CLI evidence;
- exact model-derived retained/replay results and their four-axis disposition,
  kept separate from developer-owned expected outcomes and metamorphic results;
- measured population/count/elapsed evidence and exact command receipts;
- every review finding, classification, correction, recheck, and reviewer
  conclusion;
- frozen predecessor preservation and final contract maturity evidence;
- the exact bounded claim from Section 7 and every explicit gap; and
- explicit zero counts for private-fixture access, archive access, semantic-
  oracle authoring/comparison, provider/network/credential/billable effects,
  pushes, and Slice 8 work.

Do not copy large historical chronology into the current entry. Link exact
retained evidence from the record.

## 15. Owner decision

Decision: Accepted by the project owner on 2026-08-24.

This acceptance means:

- the scope, semantic model, two domains, case matrix, work-package sequence,
  review cycle, verification floor, contract-maturity gates, and claim boundary
  are approved;
- `docs/current-state.md` may separately activate full local effect-free
  WP1-through-WP7 implementation on an exact owner-named base, beginning at
  WP1 and continuing automatically through internal predecessor gates; and
- all exclusions and predecessor boundaries remain in force.

Acceptance does not accept an implementation, freeze a new contract, grant an
independent semantic verdict, authorize external effects or private access,
push changes, or open Slice 8.

## 16. Self-contained implementation-agent prompt

```text
Implement the owner-accepted Infinium M1 Slice 7 plan in
C:\Users\vex\.codex\worktrees\223b\infinium. The planning base is exact main
commit 0056621464ab2e182d9f320d9d3b1a73bdc2b2b1; the owner-named Slice 7
activation/handoff must descend from it. First read repository AGENTS.md;
docs/README.md,
docs/current-state.md, and docs/execution-policy.md; the accepted M1 plan and
its accepted process-continuation and semantic-oracle-deferral amendments; the
Slice 6 closeout; the accepted product, ADR, and evaluation authorities listed
by docs/plans/milestones/m1/slices/s7/plan.md; then read that plan in full.

Before editing, verify HEAD/base and worktree status. Stop if the Slice 7 plan
is not explicitly owner-accepted, docs/current-state.md does not explicitly
activate WP1 and name the implementation base, that base does not descend from
the planning base above, or the base/authority otherwise differs materially.
Do not access private fixtures, evaluator/private repositories, retired paths,
or external archives. Do not create, seal, register, compare, or report a
semantic oracle. Do not run network, provider, credential, billable, external
publication, push, or other external effects. Do not reinterpret retained
Slice 6 model output, weaken any Slice-frozen Slice 6 contract, or implement
Slice 8.

Use one mutable candidate and execute WP1 through WP7 in predecessor order.
WP1 is the starting package, not a human checkpoint: continue automatically
through later packages as each internal exit gate is satisfied. Do not stop or
ask the owner to resolve ordinary implementation defects, failed tests, review
findings, schema/codec mismatches, stale documentation, or recoverable fixture
problems; correct and re-review them on the same candidate under the execution
policy. Escalate only when Section 10's genuine owner/authority or safety
conditions apply.
Build one category-neutral deterministic reversion analyzer plus two adapters:
actor/AI/FaceGen and REFR/link/placement. In each domain provide a supported
positive, a structurally matched intentional/harmless negative, and an
ambiguity case that retains a candidate and is explicitly `Abstained`, with
zero finding and zero case.
Keep fixture names, real-mod names/IDs, and domain selectors out of generic
types, rules, stable IDs, thresholds, and production mechanism. Preserve
ADR-0034's separate proposal/extraction, support, applicability, and host-
decision axes.

Exercise an effect-free retained replay of Slice 6 model-derived claim and
application facts as a separate transparency regression. Preserve their four-
axis disposition and exact provenance without a new model call or re-
adjudication, and keep them separate from developer-owned Slice 7 expected
outcomes unless an already admitted fact is truly bound and applicable to the
exact synthetic subject.

Carry the complete vertical evidence path through candidate, hypothesis,
conclusion, taxonomy, causal case, separate coverage, persistence, invalidation,
replay, JSON, and CLI output. Add only the smallest new contracts required. If
an accepted meaning cannot fit a current seam, use an additive contract or
clean-break successor and update producer, consumer, schema/codec, persistence,
migration, replay, output, and tests together while preserving frozen bytes.
Do not advance contract maturity until the evidence required by the plan exists.

Implement rename, unrelated-addition/reorder, relevant-winner, malformed,
unsupported, ambiguity, dependency-local invalidation, and cross-domain
metamorphic tests. Also run the pre-registered enabled, disabled/skipped,
limit/failure-or-cancellation, completion, reopen, and replay lifecycle
package. Retain raw negatives, abstentions, failures, coverage, and gaps. The
only allowed final claim is the bounded synthetic two-domain product-
conformance claim in Section 7; do not claim independent semantic correctness,
held-out performance, real-mod behavior, broad Skyrim generalization, safety,
or production readiness.

For each package run focused checks, then perform a consolidated semantic,
security, provenance, contract, anti-overfitting, and diff review. Classify and
correct findings on the same candidate and re-review. Use fresh read-only
reviewers where useful; give them the same private/archive/oracle/effect
prohibitions. Maintain the append-only Slice 7 implementation record with exact
commands, identities, counts, gaps, changed paths, contract maturity, reviews,
and zero prohibited-effect counts.

When the candidate is review-ready, run the complete verification floor in
Section 12 once and retain its exact receipts. A failed floor returns to same-
candidate correction, focused rechecks, and changed-surface review before a
new final attempt. After every completed, cancelled, or timed-out test run,
perform the exact repository-scoped process cleanup and zero-survivor proof in
docs/execution-policy.md and retain its root/PID/name receipt. Finish with an
owner acceptance handoff. Do not self-accept, push, or open Slice 8.
```
