# Fixture guidelines

Status: Accepted; actively maintained

Last reviewed: 2026-08-10
Wave B's target boundaries and fixture/conformance obligations are accepted by
ADR-0008 through ADR-0011, and the M1 Wave F case specifications are accepted.
Execution is partial: completed early M1 slices have exact retained package and
case evidence, while later-slice and controlled-real obligations remain
pending as identified in the case catalog and implementation records. Fixture
status must never convert acceptance of a test obligation into a passed
integration claim. ADR-0007 excludes xEdit from
fixture construction, required adjudication, and conformance dependencies.
RESEARCH-0034/0035 qualify the remaining Gate C decision boundary and
controlled-real candidates; they do not create executable fixtures or passing
cases.

Wave F produced the accepted
[common M1 baseline](m1-evaluation-baseline.md), accepted
[semantic fixture manifests](specifications/semantic-fixture-catalog.md), and
accepted
[platform fixture manifests](specifications/platform-fixture-catalog.md).
These pre-register detailed fixture families and obligations. Their acceptance
approves the designs only; the described fixture packages remain uncreated or
unexecuted where their manifests say so.

## Fixture categories

### Atomic synthetic

Minimal records/files/docs needed to test one behavior. Preferred for correctness
and regression tests.

### Integration synthetic

Several analyzers and evidence types operating together without real-mod
licensing or version drift.

### Controlled real-mod

A small MO2 profile with pinned mod/file versions, documented acquisition, and
inspectable ground truth.

### Scale

Large synthetic or personal profiles used for performance, failure isolation,
and coverage—not as the sole correctness oracle.

The private `Brain Blast Destruction 2024` profile is one such real-used
shape/scale reference. It is not a representative corpus, correctness oracle,
gold standard, or source of special-case expectations. The obsolete
`test profile` is not a fixture and no longer exists.

## Fixture-use partitions

Complete access-controlled packages, repository bindings, delegated agent
roles, sanitized disclosures, access records, and contamination transitions are
governed canonically by
[evaluator-private fixture governance v2](evaluator-private-fixture-governance-v2.md).

Every fixture is designated before use as:

- **development** — may guide implementation and prompt/rule changes;
- **validation** — checks behavior during development but must be reclassified
  if its result directly drives tuning; or
- **held-out** — remains unseen by the tuning path until the declared
  evaluation point.

If a validation or held-out result drives a production change, record the
transition to development use and add materially independent replacement
coverage. Real-mod identities and expected answers must never enter production
logic, retrieval, ranking, or model context merely because they exist in a
fixture.

Tracked development packages remain in Infinium. Complete private validation
and held-out packages live in the separate evaluator-private Git repository,
not under ignored `artifacts/`, a submodule, or a nested product directory.
Infinium retains only sanitized registry bindings and attestations. Evaluator-
v2 rules and scoring remain public. Private corpus authoring and scoring occur
only in their later purpose-bound fresh tasks, and public closeout receives no
raw answer-bearing return.

ADR-0032 defers the current M1 private held-out effort with no valid product
verdict. Slices 5-9 therefore use public development/validation fixtures under
the M1 continuation verification profile: expectations remain independently
pre-authored, every positive has a meaningful negative or abstention, and any
product-driving result is development/validation evidence rather than held-out
evidence. This deferral does not permit private access or weaken partition,
contamination, replacement, or answer-isolation rules.

For M1 Slice 5, public semantic fixtures are authored and reviewed by the work
package that owns the behavior. WP1 provides only closed contracts, invariants,
boundary enforcement, and minimal answer-free examples. WP2 owns documentation
and provenance truth, WP3 owns candidate and scale/stress truth, WP4 owns
finding/case/taxonomy/lineage/coverage truth, WP5 owns publication/replay/
recovery/query/output/platform truth, and WP6 assembles the comprehensive
cross-stage corpus. Each expected set freezes before product comparison.

## Required fixture metadata

- fixture ID and version;
- development, validation, or held-out designation and transition history;
- purpose;
- positive/negative/boundary classification;
- exact expected observations;
- expected candidate/hypothesis/finding and supported-case or lead-only
  investigation-case state;
- expected abstentions and gaps;
- applicable requirements/analyzers;
- accepted taxonomy version and applicable declared-purpose,
  technical-surface, affected-area, consequence, and effect-extent
  classifications;
- creation or acquisition provenance;
- mod/game/tool versions;
- licensing and redistribution constraints;
- ground-truth method;
- pre-registration/review provenance for expected results and confirmation
  that answer-bearing material is isolated from the path under test;
- retained/external replay dependencies and expected replayability;
- known limitations.

## Real-mod handling

- Pin exact archive and plugin versions.
- Do not redistribute files without permission.
- Prefer small profiles with one interpretable interaction.
- Retain author documentation and curated LOOT metadata used for ground truth
  where policy allows, through ground-truth construction, configured dependent
  analysis, and evaluation. Apply source-specific durable minimization only
  after those uses are materialized.
- Record whether a supplied compatibility patch demonstrates intended outcome.
- Do not assume an author's patch is perfect; inspect its effect.
- Keep personal-profile evidence private by default.

## Negative controls

Every positive should have a meaningful negative, such as:

- same structural override but intentional purpose;
- correct patch preserving both sides;
- harmless cosmetic overwrite;
- documentation condition that does not apply;
- same mod names but different effective state;
- ambiguous evidence requiring abstention.

## Mutation/metamorphic tests

Useful transformations:

- rename mod folders;
- reorder unrelated entries;
- change one relevant winner;
- remove a required document;
- change upstream version;
- alter one patch field;
- update generated input;
- reuse a result in an equivalent or new installation snapshot/analysis
  context while preserving origin and recording the validated reuse edge.

## Ground truth

Ground truth may use:

- fixture construction;
- authoritative format semantics;
- MO2 effective-state inspection;
- hand-audited plugin bytes, structural assertions, and independently specified
  expected record/field/override/link outcomes;
- curated LOOT metadata and exact qualified libloot adapter output, with
  userlist provenance kept distinct;
- author documentation;
- known-good patch comparison;
- targeted in-game observation where static proof is unavailable.

Conflicting ground truth is recorded rather than concealed.

Expected results are recorded before execution and remain separate from the
implementation input. A mismatch is evidence to investigate, not permission
to rewrite the expectation. Expectation changes require new independent
evidence, an explanation of the prior error, and review.

For Bethesda semantic fixtures, the Mutagen code path under test may assist
inspection but may not be the sole source of expected results. Fixture
provenance must identify direct byte assertions, manual adjudication, retained
format invariants, and any separate evidence used. xEdit is not an Infinium
oracle or fixture dependency.

The historical protocol `/4` held-out-authoring rule required every expected
fact to name an authority and derivation allowed by the
[final oracle-authority matrix](m1-slice4-heldout-oracle-authority-matrix.md).
Product output, product ID algorithms, exact product diagnostics, and Mutagen
interpretation alone cannot supply a hidden expected value.

No current M1 held-out authoring is authorized. A future evaluator requires a
new ADR and plan after Slice 9 during M3 planning; it must retain independently
authorable expected values, answer-free totality review, and separate public
implementation/private qualification/scoring/closeout roles. No future
protocol identity is selected here.
