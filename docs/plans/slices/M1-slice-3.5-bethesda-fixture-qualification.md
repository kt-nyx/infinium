# M1 Slice 3.5 — Independent Bethesda fixture and oracle qualification

Status: Accepted
Owner: Project owner
Prepared: 2026-07-30
Accepted: 2026-07-30
Last reviewed: 2026-08-02
Parent plan: [M1 backend semantic proof](../milestones/M1-backend-semantic-proof.md)
and accepted [revision 2 amendment](../milestones/M1-backend-semantic-proof-adr0026-amendment.md)
Target: Slice 3.5, between completed Slice 3 and production Slice 4

Accepted amendment: On 2026-08-01 the project owner selected ADR-0026's
separate private Git repository and fresh-context delegated-access model. The
public answer-bearing `BETH-LIGHT-VAL` and `BETH-UNSUPPORTED-VAL` versions are
development/regression fixtures and receive materially independent sealed
`BETH-LIGHT-VAL-002` and `BETH-UNSUPPORTED-VAL-002` replacements. Complete
private packages no longer remain under ignored Infinium `artifacts/` storage.
Migration inventory also found that the registered `BETH-HO-001` complete
package was unavailable. Its registry v1 is retained byte-for-byte as
historical evidence, invalidated for current use, and replaced by independently
authored and reviewed `BETH-HO-002`.

Accepted maintenance amendment: On 2026-08-02 the project owner authorized a
bounded Slice 3.5 correction for the sealed scorer contract. Every taxonomy
projection must have one answer-free literal subject-binding input with exact
subject closure, and every retained oracle file must be owned by
`expected-oracle.json` with exact path, fingerprint, and optional byte length.
Affected public and evaluator-private packages must be independently resealed
and requalified before the exact Slice 4 implementation commit is scored.

Accepted case-matrix maintenance amendment: On 2026-08-02 the project owner
authorized a second bounded Slice 3.5 correction after scorer audit found that
the answer-free Bethesda execution-scenario inventory had been assigned to the
effective scan-configuration role. The valid scan configuration must remain a
separate role-bound retained input and must not acquire a `cases` property.
Every affected package instead carries an explicit answer-free case-matrix
input whose closed schema identifies the fixture, scenarios, operation shape,
and exact retained-input membership without oracle values. Public and
evaluator-private packages must be superseded, independently resealed, and
requalified before scoring resumes.

Accepted accepted-order-role maintenance amendment: On 2026-08-02 the project
owner authorized a third bounded Slice 3.5 correction after scorer audit found
that the retained accepted-order construction receipt had no dedicated
execution-input role. Every package must declare
`accepted_order_construction_input`; each canonical Bethesda package must
provide exactly one sealed reference to the retained accepted-order receipt,
independent of `installation_snapshot_input` and runtime
`plugin_order_input`. The receipt must have a closed schema and exact
fixture/version/source-basis identity. Taxonomy provenance must bind the exact
normalized role reference. A declaration downgrade, neighboring-role
substitution, unresolved reference, stale seal, or receipt identity drift is
invalid even when the package is otherwise resealed. Affected public and
evaluator-private packages must be superseded, independently resealed, and
requalified before scoring resumes.

## Authority and precedence

This plan is the executable handoff for M1 Slice 3.5. It refines, but does not
replace, the accepted milestone plan. Authority descends in this order:

1. accepted product requirements and taxonomy;
2. accepted ADRs;
3. accepted M1 evaluation baseline, case specifications, and fixture manifest;
4. the accepted M1 milestone plan; and
5. this slice execution plan.

A fresh implementation agent must stop and report a contradiction rather than
silently choosing the lower-authority text. Completion of this slice accepts
evaluation inputs and independent truth only. It does not pass EVAL-0052 or
EVAL-0086 and does not authorize Slice 4 production semantics.

## Why this slice exists

Slice 4 must compare production Bethesda parsing and typed indexes with truth
that existed before that implementation. The completed Slice 3 disposable MO2
instance cannot provide that truth: its plugin population consists of copied
official files used only to exercise order/provider behavior, including one
arbitrary final-byte mutation. It has no bounded project-authored Bethesda
generator, hand-audited semantic oracle, light/malformed/unsupported matrix, or
sealed holdout.

Using those Slice 3 files as semantic fixtures would make broad third-party
bytes and production-derived interpretation stand in for a deliberately
specified test contract. Slice 3.5 therefore constructs a small, legal,
replayable, independently adjudicated Bethesda corpus and connects it to the
accepted snapshot boundary before any production parser is written.

The clean dependency is:

```text
completed Slice 3 snapshot authority
  -> Slice 3.5 accepted bytes + independent oracle + sealed holdout
  -> Slice 3.5 accepted fixture snapshot inputs
  -> Slice 4 production parser/index implementation
  -> later independent comparison against the pre-existing oracle
```

## Required reading and preflight

Before editing, read in full and in the repository order required by
`AGENTS.md`:

- `AGENTS.md`;
- `docs/README.md` and the product documents required by `AGENTS.md`;
- `docs/architecture/overview.md`, `docs/architecture/data-and-trust-model.md`,
  `docs/architecture/decisions/README.md`, and applicable ADRs, especially ADR-0001,
  ADR-0004, ADR-0007 through ADR-0010, ADR-0015, ADR-0018, ADR-0021, and
  ADR-0022, plus ADR-0026;
- `docs/evaluation/evaluation-strategy.md` and
  `docs/evaluation/case-catalog.md`;
- `docs/evaluation/m1-evaluation-baseline.md`;
- `docs/evaluation/fixture-guidelines.md`;
- `docs/evaluation/anti-overfitting-rules.md`;
- `docs/evaluation/evaluator-private-fixture-governance.md`;
- `docs/evaluation/specifications/m1-semantic-and-ground-truth.md`, especially
  EVAL-0052 and EVAL-0086;
- `docs/evaluation/specifications/m1-semantic-and-ground-truth-v2-amendment.md`;
- `docs/evaluation/fixtures/m1-semantic-fixture-manifests.md`, especially
  Sections 2, 8, 14, 15, 16, and 17;
- `docs/research/investigations/RESEARCH-0034-gate-c-synthetic-qualification.md`;
- `docs/research/investigations/RESEARCH-0035-gate-c-real-mod-qualification.md`
  and its retained manifests/raw maps;
- `docs/research/investigations/RESEARCH-0052-evaluator-private-fixture-repository-and-agent-access.md`;
- the parent M1 plan, its accepted revision 2 amendment, and completed Slice 3
  implementation record; and
- this plan.

Then:

1. run `git status --short --branch` and preserve unrelated work;
2. confirm Slices 0 through 3 are complete in
   `docs/plans/implementation-records/`;
3. confirm the seven fixture schemas and `FixturePackageReader` contract still
   agree with the package layout below;
4. inventory the exact available local .NET, PowerShell, Node, MO2, and private
   RESEARCH-0035 dependencies without modifying protected setup;
5. record the named people or isolated agent sessions assigned to every role;
6. prove that held-out answers and evaluator-private payloads are inaccessible
   to the implementation path; and
7. create the Slice 3.5 implementation record before material construction so
   commands, versions, decisions, and fingerprints are recorded as work occurs.

If an independent oracle reviewer or holdout custodian is unavailable, stop.
The implementer may not collapse those roles into a self-review and call the
packages accepted.

## Objective

Create, independently verify, and accept the exact fixture packages required
to implement Slice 4 without circular truth, real-installation mutation,
third-party payload redistribution, or M1 scope expansion.

At closeout, a new agent starting Slice 4 must have:

- tracked deterministic development Bethesda bytes plus sanitized bindings to
  complete, deterministic private validation packages in the separate store;
- independently authored structural and semantic expectations;
- a sealed, materially independent held-out Bethesda package;
- accepted Slice 4-applicable taxonomy expectations;
- one or more accepted Slice 3 snapshot inputs containing the synthetic plugin
  order/provider facts that Slice 4 is allowed to consume;
- verified private controlled-real dependency manifests for later validation;
- complete package contracts, provenance, redistribution, replay data, and
  review/access records in the store appropriate to each partition; and
- an exact acceptance record proving role separation and answer isolation.

## Scope

### Included

- Project-authored deterministic TES4-format plugin construction for:
  - `BETH-NPC-DEV`;
  - `BETH-REFR-DEV`;
  - `BETH-LIGHT-VAL` development predecessor and evaluator-private
    `BETH-LIGHT-VAL-002` replacement;
  - `BETH-MALFORMED-VAL`; and
  - `BETH-UNSUPPORTED-VAL` development predecessor and evaluator-private
    `BETH-UNSUPPORTED-VAL-002` replacement.
- An independently authored and sealed `BETH-HO-002` successor.
- Independent raw-byte, offset, length, flag, master, raw FormID, canonical
  FormKey, field, link, override-chain, winner, malformed, unsupported, and gap
  expectations.
- Slice 4-applicable EVAL-0086 projections and reviewer acceptance.
- Disposable MO2/profile construction, when needed, to feed the project-
  authored plugin bytes through the already accepted Slice 3 capture boundary.
- Verification of evaluator-private RESEARCH-0035 controlled-real dependencies
  against retained manifests, without committing their payloads.
- Tooling, tests, manifests, records, and documentation necessary to construct,
  audit, replay, and accept those inputs.

### Explicitly excluded

- Production Bethesda parsing or Mutagen integration.
- Production record, FormKey, link, override, winner, or typed-index code.
- Analyzer, candidate, hypothesis, finding, case, recommendation, or report
  behavior.
- `QUST`, quest alias, forced-reference, `ALFR`, objective, stage, condition,
  or other quest-logic semantics.
- Automatic load-order/environment discovery, localized-string resolution,
  archive activation/member semantics, or runtime behavior.
- A new product taxonomy, historical taxonomy projection, split/merge
  reclassification, or the complete EVAL-0086 matrix.
- Changes to a real MO2 instance, Skyrim installation, user profile, mod,
  overwrite directory, game file, or other protected setup.
- xEdit use anywhere in construction, oracle authorship, review, or
  acceptance.
- A claim that EVAL-0052, EVAL-0086, or M1 has passed.

## Roles and answer isolation

Record stable role IDs, people/agent-session identities, input visibility, and
outputs in the implementation record. One person may coordinate logistics, but
the following answer-bearing duties may not be collapsed.

| Role | May inspect | Must produce | Must not inspect/use |
|---|---|---|---|
| Slice integrator | Accepted plans, public manifests/registries, development inputs, tooling, tests, sanitized private results | Package integration, schema validation, snapshot wiring, closeout record | Raw private validation/held-out inputs or oracle; production Slice 4 output |
| Binary-fixture author | Pinned public-only contract bundle, format research, accepted allowlist, its own generator source | Deterministic development/private-validation generator and exact emitted bytes | Production parser source/output; Mutagen/xEdit-derived expected values; predecessor private/public validation inputs, generators, or answers; held-out input/oracle |
| Independent oracle reviewer | Frozen emitted bytes, format evidence, accepted allowlist, independent audit tooling | Expected offsets/lengths/fields/FormKeys/links/chains/winners/gaps and attributed review record | Generator's expected-value tables; production parser/tests/output; Mutagen/xEdit answers |
| Holdout custodian | Public scope and package contract | Materially independent `BETH-HO-002` input, oracle, sealed hashes, private replay instructions | Production code/output; development answers as a template for byte layout |
| Taxonomy reviewer | Frozen raw facts, taxonomy `0.1.0`, Slice 4 taxonomy scope | Exact assignments/non-assignments, roles, evidence, reasons, counterexample acceptance | Hosting/name/record-family shortcuts; production classifier output |

The independent oracle may use a small purpose-built raw reader or manual byte
audit. Its implementation must be separate from the binary generator and from
future production parsing. The existing RESEARCH-0035 raw scripts may be used
as reviewed format evidence or as an independently controlled reader, but
their output must not be copied blindly and they must not import generator
answer tables.

Development answers may be visible after they are frozen. Validation answers
remain evaluator-side until a run is scored. If a validation case influences a
code or generator correction, reclassify it to development in
`partition-history.json` and replace the lost validation coverage with a new
independent case. Held-out answers never enter ordinary source, test input,
logs, prompts, or implementation context.

Private access follows
[evaluator-private fixture governance](../../evaluation/evaluator-private-fixture-governance.md).
The integrator may autonomously launch a fresh-context delegated private role
for an allowed operation, but receives only sanitized metadata/outcome unless a
recorded reveal reclassifies and replaces the fixture.

## Required package set

### Bethesda development and validation packages

Each executable package is one directory containing the seven required fixture
documents plus its project-authored payloads:

```text
<fixture-id>/
  public-manifest.json
  execution-input.json
  expected-oracle.json
  provenance.json
  replay-dependencies.json
  redistribution.json
  partition-history.json
  inputs/
    <exact project-authored plugin bytes and controls>
```

Tracked development packages belong under
`test-data/evaluation/m1-semantic/<fixture-id>/`. Complete validation and
held-out packages remain in the separate private Git store selected by
ADR-0026; Infinium retains a non-answer-bearing public registry entry, not a
partial directory presented as executable. A package is executable only from
the complete seven-document directory. Public-answer publication is no longer
the default validation topology; a deliberately public answer-bearing package
is development coverage.

Development generators and audit tools belong under a purpose-specific
`tools/evaluation/bethesda-fixtures/` tree, not production `src/`. Private
generators/audit tools remain in the private store. Retain a
machine-readable construction manifest mapping generator revision, command,
seed, file length, and SHA-256 to every emitted file. Generation must be
deterministic from tracked source and fixed inputs; timestamps, temporary
paths, current locale, installed game data, and enumeration order may not
change bytes.

All seven package documents must validate through the existing schemas and
`FixturePackageReader`. `execution-input.json` identifies only runnable inputs
and public dependencies; it must contain no expected answer, oracle path,
answer-bearing filename, or held-out private locator. `expected-oracle.json`
is separately fingerprinted and records independent ground-truth methods.

#### `BETH-NPC-DEV`

Cover at least:

- full and light origin identities;
- TES4 master lists and ESL flag inputs needed for translation;
- compressed and uncompressed `NPC_`;
- winner, deleted, and templated states;
- configuration flags, template flags, and template link;
- `RNAM`, `AIDT`, repeated `PKID`, repeated `PNAM`, and `HCLF`;
- resolved `RACE` and `FaceGenHead` facts used by the M1 applicability
  decision;
- override chains and canonical FormKey/master translation; and
- unknown and unresolved links.

#### `BETH-REFR-DEV`

Cover at least:

- compressed and uncompressed `REFR`;
- `NAME`, `XLKR`, `XLRL`, `XOWN`, and `DATA`;
- differing master indices that resolve to stable canonical identities;
- override order, winner, and deleted state; and
- absent, repeated, and format-valid boundary variants, including malformed
  subrecord boundaries in the designated negative members.

#### `BETH-LIGHT-VAL`

Cover `.esl` and ESL-flagged `.esp` origin/local-ID translation, including
valid boundaries, valid maximum values, and invalid/out-of-range cases. Keep
the valid and invalid denominators explicit.

The committed `1.0.0` package is development/regression coverage after public
answer exposure. `BETH-LIGHT-VAL-002` provides the same abstract validation
obligation through independently authored private bytes and oracle evidence.

#### `BETH-MALFORMED-VAL`

Cover truncated records, inconsistent or overflowing sizes, bounded
decompression failure, pathological nesting/counts, invalid master
references, and changed-during-read input. Construction/audit tests prove the
intended malformed bytes and mutation behavior; production failure behavior
remains pending Slice 4.

#### `BETH-UNSUPPORTED-VAL`

Cover at least one unallowlisted record family, unallowlisted field/shape,
localized-string dependency, archive-member request, and automatic
environment-discovery request. The oracle expects explicit unsupported/gap
states, not guessed values or silent omission.

The committed `1.0.0` package is development/regression coverage after public
answer exposure. `BETH-UNSUPPORTED-VAL-002` provides materially independent
private validation coverage with independently constructed representatives and
boundary controls.

### Held-out package

`BETH-HO-002` must be materially independent, not a renamed or trivially
mutated development fixture. Before Slice 4 begins, the custodian freezes:

- public fixture ID/version, partition, purpose, applicable EVAL IDs, and
  redistribution class;
- exact input-package, oracle, provenance, and replay-dependency fingerprints;
- a private dependency/payload inventory and reconstruction procedure; and
- a custodian acceptance record.

Only non-answer-bearing public metadata, private-store revision identities, and
sealed hashes may enter the ordinary repository. Private input locators and all
oracle contents stay in the separate evaluator-private Git store. The Slice 4
implementer receives neither the input bytes nor answers; scoring and
maintenance use the delegated boundary in ADR-0026.

### Controlled-real validation projections

Verify the RESEARCH-0035 private dependencies by recomputing the fingerprints
listed in its retained case manifests and confirming that the raw maps still
refer to those exact bytes. Record only sanitized IDs, hashes, lengths,
availability, license/privacy classification, and verification result.

Do not commit, copy, transform, or redistribute official game files or
third-party mod payloads. Missing, mismatched, or no-longer-permitted private
bytes are a recorded blocker; they are not silently replaced with a convenient
local mod.

## Independent oracle contract

For every project-authored plugin member, the frozen oracle must identify:

- file identity, length, SHA-256, plugin order, and provider facts;
- record/group header offsets, lengths, signatures, flags, compression state,
  and raw FormIDs;
- TES4 master order, origin plugin, local ID, full/light state, and canonical
  FormKey;
- exact supported subrecords, multiplicity, byte ranges, decoding, and typed
  values;
- canonical links and resolved/unresolved status;
- override-chain membership/order, winning record, and deleted state;
- malformed or unsupported condition and exact expected gap population;
- the ground-truth method and independent evidence for each expectation; and
- plausible but forbidden interpretations where ambiguity could cause an
  overbroad implementation.

The reviewer must audit the frozen emitted bytes, not an in-memory generator
model. At least two independent techniques are required for answer-bearing
facts: for example a manual annotated hex/offset review plus a separately
implemented raw reader. Agreement between generator output and Mutagen is not
independent truth. xEdit is prohibited even as a manual tie-breaker.

Oracle changes after freeze require a new oracle version, independent evidence,
a precise prior-error explanation, reviewer identity, a new fingerprint, and
preserved change history. Changing an expected answer because production
disagrees is prohibited unless the independent evidence proves the original
oracle wrong.

## Snapshot integration contract

Slice 4 accepts ordered plugin input only from an accepted snapshot. Slice 3.5
must therefore prove that the project-authored plugin bytes can be presented
through the completed Slice 3 boundary without adding a second load-order
source.

Use a disposable copied MO2 `2.5.2` instance/profile or a fully synthetic
Slice 3 test-profile boundary, as appropriate to the existing accepted test
architecture. The construction must:

- place only project-authored redistributable fixture bytes in disposable
  roots;
- define explicit plugin/mod order and any intentional provider override;
- capture through the existing Slice 3 operation without launching MO2 or
  USVFS;
- retain the exact snapshot/input fingerprints and plugin/provider order used
  by later Slice 4 evaluation;
- demonstrate same-size one-byte invalidation and changed-during-read
  rejection where applicable; and
- clean up only the verified disposable root.

The existing Slice 3 evaluator instance may guide directory/profile shape, but
its official plugin bytes and oracle must not be copied or relabeled. No real
MO2, Skyrim, mod, profile, or overwrite state may be changed.

## Slice 4-applicable taxonomy qualification

This slice prepares only the EVAL-0086 truth that Slice 4 can exercise from
Bethesda/plugin observations. It does not construct or accept the whole
taxonomy suite.

Required Slice 4 projections are:

- observed `surface.plugin-data` and `delivery.plugin-container` only when the
  frozen fixture evidence establishes them;
- purpose, affected area, consequence, extent, severity, and confidence
  remain unassigned or have the exact independently adjudicated applicability
  state rather than being inferred from `NPC_`, `REFR`, filenames, or plugin
  delivery;
- the `TAX-03A`/`TAX-03B` shared-surface/different-area separation, with areas
  supplied by independent evidence rather than inferred by Slice 4;
- the `TAX-06` supported-surface/unsupported-semantics behavior;
- the `TAX-08` provider/winner-topology `not-applicable` behavior; and
- the `TAX-12A`/`TAX-12B` record-family counterexample, represented as
  independent pre-registered evidence rather than a Slice 4 inference.

Package these subjects in the applicable portions of `TAX-AXES-DEV`,
`TAX-COUNTEREXAMPLE-VAL`, and `TAX-STATE-VAL`, or in schema-valid Slice
4-specific projections that retain those canonical subject IDs and source
package links. The taxonomy reviewer records exact subject, axis/facet/code or
applicability state, role, evidence, conditions, reason, confidence reference,
and forbidden copied assignments.

`TAX-HISTORY-VAL`, split/merge mechanics, the broader `TAX-HO-001`, and
taxonomy assertions dependent on later claims/findings remain in their planned
later slices. Slice 3.5 must not manufacture semantic affected-area truth from
raw record family merely to complete those packages early.

## Implementation work sequence

1. **Record preflight and roles.** Create the in-progress implementation
   record, capture repository/tool identities, assign isolated roles, and
   document access boundaries.
2. **Freeze package design.** Map every required matrix member to package,
   input file, partition, expected observation class, and EVAL assertion.
   Review the map before generating bytes.
3. **Build deterministic generators.** Emit only project-authored bytes and
   construction manifests. Add deterministic replay and byte-fingerprint
   tests.
4. **Freeze development/validation inputs.** Record exact bytes, lengths,
   hashes, seeds, and generator revision before oracle authorship.
5. **Author independent oracles.** The reviewer audits frozen bytes using the
   independent method and records exact expectations plus forbidden
   interpretations.
6. **Prepare snapshot inputs.** Feed tracked synthetic plugins through the
   Slice 3 boundary and retain the accepted snapshot dependencies/order.
7. **Seal heldout.** The custodian authors, reviews, and seals
   `BETH-HO-002`; disclose only approved public metadata/fingerprints.
8. **Review taxonomy projections.** Freeze the Slice 4-applicable EVAL-0086
   assignments and negative counterexamples.
9. **Verify controlled-real dependencies.** Recompute private
   RESEARCH-0035 fingerprints and record availability without committing
   payloads.
10. **Validate and review.** Run every gate below, inspect raw artifacts,
    correct material issues, rerun, and perform a second semantic review.
11. **Accept and close out.** Mark packages accepted only after Section 17 is
    satisfied, complete the implementation record, update documentation
    indexes/statuses, and make a focused commit. Do not push unless requested.

## Required tests and evidence

### Contract

- Every complete development/validation package has exactly the seven required
  documents, validates against committed schemas, and loads through
  `FixturePackageReader`; evaluator-private packages are validated in their
  controlled environment.
- IDs, versions, EVAL links, partitions, partition histories, taxonomy
  version, fingerprints, relative links, and redistribution classes agree.
- Execution inputs remain answer-free; mutation tests prove that an inserted
  answer/oracle reference is rejected.
- Held-out public metadata matches the sealed evaluator-private package
  fingerprints without exposing private inputs, locators, or answers; it is
  not misrepresented as a complete executable package.

### Construction and oracle

- Two clean generator runs from the same revision/seed produce byte-identical
  files and manifests.
- Every emitted byte is covered by a declared construction region or an
  intentional padding/opaque region; no installed game/mod byte is consumed.
- Independent raw-reader/manual results agree with the frozen oracle.
- One-byte, master-order, record-order, compression, repeated-field, and
  local-ID mutations change only the pre-registered dependent expectations.
- Oracle provenance demonstrates no production parser, Mutagen expectation,
  xEdit, or held-out leakage.

### Snapshot integration

- The accepted Slice 3 capture receives the exact synthetic byte set and
  produces the pre-registered plugin/provider order.
- Snapshot/input fingerprints bind every byte-dependent dependency.
- Same-size/time mutation and mid-read mutation do not retain accepted stale
  identity.
- The operation launches no MO2/USVFS process and changes no protected root.

### Security, privacy, and fault

- Protected-root before/after evidence remains equal.
- Disposable path resolution is fail-closed and cleanup targets only the
  verified disposable root.
- Oversized, truncated, invalid-size, decompression, nesting/count, master, and
  changed-during-read fixtures are bounded at construction/audit time.
- Private paths, user names, game/mod bytes, credentials, and sealed answers
  do not appear in tracked files, test logs, or the final diff.
- No network, billable provider, or external model execution is used.

### Semantic review

The re-review must explicitly search for:

- accidental `QUST`/alias or other post-M1 scope;
- a record-family, filename, or fixture-ID shortcut;
- an expectation derived from generator data structures, Mutagen, xEdit,
  production output, or prior evaluation output;
- validation or holdout cases that influenced implementation without
  repartitioning/replacement;
- missing positive-allowlist coverage disguised as unsupported behavior;
- unsupported scope silently omitted instead of represented as a gap;
- taxonomy axes copied from record family or from one another;
- third-party redistribution or protected-setup mutation; and
- any claim that an unexecuted EVAL case passed.

## Verification commands

Run from the repository root. Add focused tooling commands created by the slice
to the implementation record; do not replace the repository-wide checks with
them.

```powershell
git status --short --branch
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Contract"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Integration"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Evaluation"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Security"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Fault"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=M1Security"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=M1Fault"
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
git diff --check
git status --short
```

Also retain exact output from:

- deterministic clean regeneration and manifest comparison;
- independent raw-reader/oracle comparison;
- fixture schema/reader validation;
- answer-isolation mutation tests;
- disposable snapshot construction/capture;
- protected-root before/after comparison;
- private dependency fingerprint verification; and
- repository searches for prohibited dependencies and answer-bearing leakage.

The milestone-wide `evaluate` and `verify-evaluation` CLI commands belong to a
later slice and are not prerequisites here unless they already exist through
an independently accepted plan amendment. Do not add placeholders.

## Acceptance gates

Slice 3.5 is complete only when all are true:

1. Every required role and access boundary is recorded and independently
   evidenced.
2. All required Bethesda development packages and their private validation
   replacements exist in their approved public or separate private Git stores,
   are replayable,
   schema-valid, fingerprint-valid, and accepted, with non-answer-bearing
   public metadata retained for each.
3. Required matrix coverage is explicit; unsupported and malformed populations
   have denominators and expected gap/failure states.
4. Independent oracle review covers exact frozen bytes and every answer-bearing
   field required by EVAL-0052.
5. `BETH-HO-002` has materially independent sealed input/oracle fingerprints
   and remains inaccessible to implementation.
6. Slice 4-applicable EVAL-0086 projections are accepted without pulling
   later taxonomy work forward.
7. Synthetic plugin bytes are connected to an accepted Slice 3 snapshot
   boundary with exact order/provider/fingerprint evidence.
8. RESEARCH-0035 private dependencies are verified or an explicit blocker is
   recorded; no third-party payload is committed.
9. Section 17 of the fixture manifest is satisfied for each accepted package.
10. Full verification, raw-artifact inspection, semantic review,
    fix/rerun, and re-review pass.
11. The implementation record contains exact identities, commands, results,
    fingerprints, role separation, retained locations, intentional behavior
    changes, and unresolved gaps.
12. The final diff contains no production Slice 4 or later-slice behavior.

Any supported-value ambiguity, unavailable independent reviewer/custodian,
held-out leakage, unverified controlled-real dependency, schema mismatch,
license uncertainty, or protected-root mutation is a blocker. Do not invent a
substitute architecture or weaken the gate to finish the slice.

## Completion record

Create `docs/plans/implementation-records/M1-slice-3.5.md`. At minimum record:

- status, review date, parent/execution-plan revisions, and implementation
  commit SHA;
- role IDs and explicit separation/visibility evidence;
- all fixture/package/oracle versions and fingerprints;
- generator, independent reader, PowerShell, Node, .NET, MO2, OS/runtime, and
  relevant dependency identities;
- exact construction, regeneration, review, sealing, private verification,
  snapshot, test, formatting, and diff commands with results;
- retained public and evaluator-private artifact locations, with private paths
  sanitized;
- package-by-package Section 17 acceptance;
- exact EVAL-0052 and EVAL-0086 assertions prepared, with a statement that
  execution/pass remains pending Slice 4;
- validation repartition/replacement history, if any;
- protected-root and no-launch evidence;
- review findings, corrections, rerun results, and final re-review;
- intentional behavior/document changes; and
- genuinely unresolved blockers or later-slice work.

Update `docs/plans/implementation-records/README.md` only when the record is
complete or formally reviewed with explicit blockers.

## Fresh-agent handoff

The following prompt may be used verbatim:

```text
Implement M1 Slice 3.5 in:
Z:\Development\Large Projects\Skyrim\infinium

This is the independent Bethesda fixture and oracle qualification slice. Do
not implement Slice 4 production parsing or typed indexes.

Before editing, read AGENTS.md and its complete required reading order, then:
docs/plans/milestones/M1-backend-semantic-proof.md
docs/plans/milestones/M1-backend-semantic-proof-adr0026-amendment.md
docs/plans/slices/M1-slice-3.5-bethesda-fixture-qualification.md
docs/plans/implementation-records/M1-slice-3.md
docs/evaluation/m1-evaluation-baseline.md
docs/evaluation/fixture-guidelines.md
docs/evaluation/anti-overfitting-rules.md
docs/evaluation/evaluator-private-fixture-governance.md
docs/evaluation/specifications/m1-semantic-and-ground-truth.md
docs/evaluation/specifications/m1-semantic-and-ground-truth-v2-amendment.md
docs/evaluation/fixtures/m1-semantic-fixture-manifests.md
docs/research/investigations/RESEARCH-0034-gate-c-synthetic-qualification.md
docs/research/investigations/RESEARCH-0035-gate-c-real-mod-qualification.md
docs/research/investigations/RESEARCH-0052-evaluator-private-fixture-repository-and-agent-access.md

Inspect git status and preserve unrelated work. Execute the entire accepted
Slice 3.5 plan, including role/answer isolation, deterministic development and
separately versioned private BETH-* construction, fresh-context delegated
independent oracle review, sealed BETH-HO-002,
Slice-4-applicable taxonomy projections, accepted Slice 3 snapshot
integration, private RESEARCH-0035 fingerprint verification, schema and
anti-leakage tests, protected-root evidence, review/fix/rerun/re-review, and
the exact implementation record.

Use no production parser output, Mutagen-derived expectations, xEdit, real-mod
specific production rules, third-party payload commits, protected setup
writes, or later-slice behavior. Stop and report a blocker rather than
collapsing independent roles, exposing held-out answers, weakening coverage,
or inventing substitute architecture.

Make one focused commit after verification. Do not push.
```

## Rollback

This slice is additive and pre-production. Rollback removes only the
Slice 3.5 generators, project-authored fixture packages, tests, and
documentation added by the slice. Never delete evaluator-private or protected
roots as part of repository rollback. If an accepted fixture is found wrong,
preserve its version and change history, withdraw its acceptance, and publish a
new reviewed version rather than rewriting evidence in place.
