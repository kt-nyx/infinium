# Fixture guidelines

Status: Accepted
Disposition: actively maintained
Last reviewed: 2026-08-23
Current timing authority: [ADR-0035](../architecture/decisions/ADR-0035-defer-independent-semantic-oracle-qualification.md)
defers independent semantic-oracle packages throughout M1 and M2. Developer-
owned conformance fixtures and deterministic reference tests remain required;
semantic-admission v1-v13 are historical non-authorizing packages. Any later
pre-seal workflow described here is dormant unless a new accepted M3 plan
reactivates it.
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
[archived M1 evaluation baseline](evaluator-history.md), accepted
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

ADR-0035 defers independent semantic-oracle qualification throughout M1 and
M2. Current fixtures serve product conformance: small developer-owned positive,
negative, malformed, lifecycle, abstention, mutation, and metamorphic cases;
deterministic byte/reference cases; and controlled integration cases. Product
output may not be copied into expected fields merely to make a test pass.

Historical semantic-admission packages remain development/history. Current
verification may check only their immutable files, hashes, manifests, registry
bindings, reclassification, and lack of current authority. It must not execute
the current producer or consumer against historical expected labels or report
semantic success.

No new independent semantic package may be authored, audited, reviewed,
sealed, registered, or compared until the M3 Evaluation Readiness Gate and a
new accepted M3 evaluation plan authorize that work. The former pre-seal order
and derivation-closure rules remain historical evidence in the Slice 6 record;
they are not an M1/M2 gate.
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
fact to name an authority and derivation allowed by its now-archived final
oracle-authority matrix; see [Evaluator history](evaluator-history.md).
Product output, product ID algorithms, exact product diagnostics, and Mutagen
interpretation alone cannot supply a hidden expected value.

No current M1 or M2 independent semantic-oracle or held-out authoring is
authorized. Re-entry is considered only after M2 acceptance at the M3
Evaluation Readiness Gate, and requires all ADR-0035 prerequisites plus a new
accepted M3 evaluation plan. Private isolation and separate public
implementation/private qualification/scoring/closeout roles remain unchanged.
No future protocol identity is selected here.
