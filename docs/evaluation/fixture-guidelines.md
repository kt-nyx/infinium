# Fixture guidelines

Status: Draft  
Last reviewed: 2026-08-01

Wave B's target boundaries and fixture/conformance obligations are accepted by
ADR-0008 through ADR-0011, and the M1 Wave F case specifications are accepted;
execution remains pending. Fixture status must never convert acceptance of a test
obligation into a passed integration claim. ADR-0007 excludes xEdit from
fixture construction, required adjudication, and conformance dependencies.
RESEARCH-0034/0035 qualify the remaining Gate C decision boundary and
controlled-real candidates; they do not create executable fixtures or passing
cases.

Wave F produced the accepted
[common M1 baseline](m1-evaluation-baseline.md), accepted
[semantic fixture manifests](fixtures/m1-semantic-fixture-manifests.md), and
accepted
[platform fixture manifests](fixtures/m1-platform-fixture-manifests.md).
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
[evaluator-private fixture governance](evaluator-private-fixture-governance.md).

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
Infinium retains only sanitized registry bindings and attestations. Ordinary
implementation agents access private material only through purpose-bound
fresh-context delegates and receive no raw answer-bearing return.

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
