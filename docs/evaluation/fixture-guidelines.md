# Fixture guidelines

Status: Draft  
Last reviewed: 2026-07-25

Wave B's target boundaries and fixture/conformance obligations are accepted by
ADR-0008 through ADR-0011, but full Wave F case specifications and execution
remain pending. Fixture status must never convert acceptance of a test
obligation into a passed integration claim. ADR-0007 excludes xEdit from
fixture construction, required adjudication, and conformance dependencies.

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

## Required fixture metadata

- fixture ID and version;
- purpose;
- positive/negative/boundary classification;
- exact expected observations;
- expected candidate/hypothesis/finding and supported-case or lead-only
  investigation-case state;
- expected abstentions and gaps;
- applicable requirements/analyzers;
- accepted taxonomy version and applicable declared-purpose,
  technical-surface, affected-game-area, consequence, and effect-extent
  classifications once the RQ-036 result is accepted;
- creation or acquisition provenance;
- mod/game/tool versions;
- licensing and redistribution constraints;
- ground-truth method;
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

For Bethesda semantic fixtures, the Mutagen code path under test may assist
inspection but may not be the sole source of expected results. Fixture
provenance must identify direct byte assertions, manual adjudication, retained
format invariants, and any separate evidence used. xEdit is not an Infinium
oracle or fixture dependency.
