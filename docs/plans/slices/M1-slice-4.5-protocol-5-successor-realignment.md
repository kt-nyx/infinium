# M1 Slice 4.5 — Protocol `/5` successor realignment

Status: Accepted; WP0 and WP1R complete; WP1 resumed
Owner: Project owner
Prepared: 2026-08-07
Accepted: 2026-08-07
Last reviewed: 2026-08-07
Work package: `M1/S4.5/PRE-B2/V5`
Parent plan: `infinium.plan.m1.backend-semantic-proof/3`

## Objective

Qualify and freeze a public protocol `/5` successor whose schema and
canonicalizer can represent every outcome in accepted successor semantic model
`infinium.m1-slice4.protocol-5-evidence-contract/1.0.0`
exactly, including partial-object retention, without inspecting or executing
the product candidate or accessing private material.

This is evaluator successor qualification, not repair or retry of protocol
`/4`, a private corpus, or a private verdict.

## Authority and immutable inputs

- accepted product requirements and taxonomy `0.1.0`;
- ADR-0027 except decision 15, which ADR-0030 narrowly supersedes;
- ADR-0028, ADR-0029, ADR-0030, and ADR-0031;
- evaluator-private fixture governance v2;
- immutable predecessor model
  `infinium.m1-slice4.protocol-4-evidence-contract/1.2.0` at SHA-256
  `09ae312824aa0c859b396fd18fef69b14905c2c6d6f901ce598d3c5ab5970bf5`;
- accepted `/5` successor model
  `infinium.m1-slice4.protocol-5-evidence-contract/1.0.0`, derived by the
  exact ADR-0031 overlay and mandatory global composition gate;
- historical frozen evaluator `/4`
  `3693d19563c636cd2879804633ca4ce52448d2c1`; and
- public WP5 classification at
  `b11be1d7da01a6eb73c10bd9e6569d65beb74abc`.

The successor model is `/5` semantic authority. Model `1.2.0` and `/4`
mechanics may be inspected as historical
public implementation evidence, but neither `/4` nor the frozen candidate may
fill a model or contract omission.

## Scope

### Included

- one append-only successor ADR and this accepted plan;
- a complete `/5` projection-representation contract;
- an exhaustive deterministic representability proof derived from the model;
- public `/5` schemas, canonicalizer, adapter/scorer bindings, calibration,
  tests, manifests, validation commands, and documentation;
- deterministic LF/CRLF dependency-manifest validation;
- an explicit aggregate artifact ordering algorithm whose implementation
  reproduces the documented hash;
- one fresh product-blind public acceptance review; and
- one exact public `/5` freeze and closeout.

### Excluded

- candidate source, tests, builds, runtime artifacts, execution, adaptation,
  correction, realignment, or freeze;
- private repository, fixtures, manifests, expected outputs, oracle material,
  answers, qualification, or scoring;
- predecessor answer-bearing reviews or product behavior as truth;
- B2, C2, Stage D, Slice 5, live calls, billable calls, and push;
- legacy archive and broad parent-directory operations; and
- protocol `/6`, changes to immutable model `1.2.0`, or semantic changes beyond
  ADR-0031's exact successor delta.

## Role and isolation model

The orchestration parent integrates work, reviews every package, commits each
package once, and preserves a clean tree. Delegates may not create delegates.
Read-only WP1 audits use fresh contexts and positive public allowlists. WP2 and
WP3 implementation use scoped public-only contexts and avoid simultaneous
edits to interdependent files. WP4 uses exactly one fresh reviewer with no
inherited conversation and a clean detached checkout at the exact WP3 commit.

No role may enumerate or inspect candidate, private, legacy, detached-candidate,
or answer-bearing roots. An isolation breach stops the cycle immediately and
does not authorize a replacement reviewer.

## Work packages

### WP0 — Successor disposition and accepted plan

Work ID: `M1/S4.5/PRE-B2/V5/WP0`

Create ADR-0030 and this accepted plan. Update public indexes, governance,
`AGENTS.md`, Slice 4.5 status, and the implementation record. Preserve `/4`
and earlier evidence. Do not modify evaluator code.

Exit criteria:

- the supersession is limited to ADR-0027 decision 15;
- representability, role, review, hard-stop, freeze, product-handoff,
  corpus-eligibility, and later B2/C2 boundaries are explicit;
- links, status statements, protected-path scope, and `git diff --check` pass;
- parent review has no unresolved material finding; and
- one focused local commit completes WP0.

Correction budget: one parent-review correction pass.

### WP1 — Projection-representation contract

Work ID: `M1/S4.5/PRE-B2/V5/WP1`

Define a complete machine-checkable mapping from every admitted model state to
legal `/5` documents and exact canonical facts. For every property and object,
specify required, optional, omitted, explicitly null, malformed, and
contradictory shapes; partial-object retention; canonicalization/rejection;
coverage; gaps; atomic boundaries; and the prohibition on invented
higher-layer facts.

Create:

- `docs/evaluation/specifications/m1-slice4-protocol-5-projection-representation-contract.md`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-projection-representation-model.schema.json`;
  and
- `docs/evaluation/specifications/m1-slice4-protocol-5-projection-representation-model.json`.

The defining invariant is:

> Every accepted semantic outcome has at least one schema-valid `/5` document
> that canonicalizes to exactly that outcome, with no missing required facts
> and no extra facts.

The contract covers all 15 fact families, 9 state classes, 10 coverage
populations, 9 gap rules, 11 atomic boundaries, constructor and normalization
groups, and higher-order invariants in successor model `1.0.0`. It is not a one-off
`RACE/DATA` patch.

Fresh read-only audits must separately cover property optionality/null/omission,
coverage/gap arithmetic, schema/canonicalizer expressiveness, and malformed or
adversarial boundaries. The parent integrates and formally reviews them.

Exit criteria:

- the contract and machine representation validate and have stable identities;
- every admitted outcome has at least one explicit witness class;
- no outcome requires candidate/private behavior or a semantic change;
- all four audit scopes are resolved;
- deterministic checks and `git diff --check` pass; and
- one focused local commit completes WP1.

Correction budget: one parent-review correction pass.

WP1 status: **hard-stopped pending owner semantic disposition**. The four
required fresh audits and parent review found that accepted rule
`P4-FACEGEN-APPLICABLE-UNKNOWN-ARCHIVE-SUPPORTED` requires loose-asset
coverage denominator/completion `1/0` while producing no owning gap and no
failed or skipped lifecycle. Accepted coverage rules admit no such row. The
[WP1 representability hard-stop record](../../evaluation/m1-slice4-protocol-5-wp1-representability-hard-stop.md)
contains the mechanical proof, audit results, draft-artifact status, and exact
boundary. No correction pass was attempted because every resolution requires
a new semantic or authority choice, which is a global hard stop. WP2 through
WP4 did not start.

### WP1R — Semantic composition recovery and successor-model acceptance

Work ID: `M1/S4.5/PRE-B2/V5/WP1R`

The owner accepted the exact graceful-degradation disposition recorded by
ADR-0031. WP1R preserves the historical hard stop and immutable model `1.2.0`,
accepts the distinct successor identity
`infinium.m1-slice4.protocol-5-evidence-contract/1.0.0`, adds only
`P5-GAP-LOOSE-AVAILABILITY` and the two explicit successor rule replacements,
and installs a deterministic global composition gate.

The gate materializes the exact overlay, verifies all predecessor hashes,
composes every admitted state/rule through coverage and gaps, exercises
cross-family aggregate witnesses, verifies fixed rows and atomic boundaries,
and rejects 24 global mutations. It is mandatory for resumed WP1 and WP2-WP4.
Two runs on Windows PowerShell and two on PowerShell must produce byte-identical
machine summaries. Three fresh read-only audits cover the semantic delta,
global composition, and history/documentation consistency. The parent may use
at most one correction pass.

WP1R status: **accepted**. The owner disposition resolved both unknown-loose
branches without coercing unknown or coupling archive evidence. Resumed WP1
consumes only the accepted successor identity. The original WP1 correction
budget remains unused.

### WP2 — Deterministic representability proof

Work ID: `M1/S4.5/PRE-B2/V5/WP2`

Extend or reuse the public totality tooling to construct a `/5` witness for
every admitted state, validate it against the schema, canonicalize it, and
compare its exact fact set with the model-derived expectation. Prove forbidden
fact absence, distinct omission/null/empty/unknown/unresolved/unsupported and
terminal outcomes, exact mutation behavior, coverage/gap arithmetic, and
complete mutually exclusive state coverage.

The primary validator is
`eng/validate-m1-slice4-protocol5-representability.ps1`. Scratch witness and
canonical output remains ignored under `work/`; no expected answer is copied
from a product or private source.

Expectations come from successor model `1.0.0`, its mandatory global
composition proof, and the WP1 representation contract, not
from evaluator implementation or candidate output. The validator reports exact
raw, admitted, excluded, invalid/terminal, witness, mutation, uncovered,
overlap, rejection, and runtime counts. Required Windows PowerShell and
PowerShell variants must agree semantically where applicable.

Exit criteria:

- zero admitted states lack a valid exact witness;
- zero uncovered or overlapping state classes remain;
- every required mutation is rejected or yields its exact expected different
  fact set;
- repeat runs and runtime variants are deterministic;
- parent review has no unresolved finding; and
- one focused local commit completes WP2.

Correction budget: two parent-review correction passes. The same material
failure after two corrections, or any required semantic change, is a hard stop.

### WP3 — Implement and qualify public evaluator `/5`

Work ID: `M1/S4.5/PRE-B2/V5/WP3`

Implement protocol `infinium.evaluator-v2/5` coherently across candidate and
expected schemas, canonicalizer, adapter/scorer bindings, calibration, public
tests, protocol and dependency/freeze manifests, validation commands, and
documentation. Use a general mechanism for conditionally authorable facts so
independently proven common facts survive unavailable later-layer facts.

Correct the public tooling defects by normalizing dependency-manifest text
deterministically across LF/CRLF hosts and defining aggregate artifact order
as an explicit byte-reproducible algorithm. No record-, fixture-, race-, title-,
mod-, or pilot-specific exception is permitted.

Required checks include focused positive, malformed, mutation, determinism,
write-boundary, adapter, scorer, and identity tests; the full WP2 proof;
calibration twice with byte comparison; locked restore; build; all required M1
test categories and full tests; format; dependency and freeze manifests;
links/identities; and `git diff --check`.

Exit criteria:

- all required checks pass without waiver;
- protocol/schema/projection/tool/manifests have exact deterministic identities;
- source and tests contain no candidate/private or fixture-specific rule;
- parent review has no unresolved finding; and
- one focused local commit completes WP3.

Correction budget: two parent-review correction passes.

### WP4 — Fresh product-blind acceptance and freeze

Work ID: `M1/S4.5/PRE-B2/V5/WP4`

One genuinely fresh reviewer receives only the accepted public semantic
authority, WP0-WP3 and WP1R contract and records, `/5` public implementation/tests,
deterministic validation tooling, and exact commands. The reviewer works from
a clean detached checkout at the exact WP3 commit and independently verifies
total representation, schema/canonicalizer exactness, absence of invented or
lost facts, arithmetic, mutations, malformed/terminal handling, identities,
real-path calibration, isolation, and documentation agreement.

The reviewer may create only an immutable public attestation after a clean
pass. It cannot silently edit implementation or authority.

Review budget:

- one initial independent review;
- if needed, one correction by the original WP3 implementer; and
- one re-review by the same reviewer.

A material finding after re-review or any isolation breach is a hard stop. No
replacement reviewer may be launched to obtain a different judgment.

After acceptance, the parent verifies the attestation, exact WP3 commit and
artifact hashes, manifests, test/state counts, diff scope, links, and clean
tree; records the product-realignment and corpus-eligibility handoffs; and
creates one focused WP4 freeze/closeout commit.

The immutable public records are:

- `docs/evaluation/m1-slice4-protocol-5-public-acceptance-attestation.md`; and
- `docs/evaluation/evaluator-v2-stage-a-protocol-5-freeze.json`.

## Parent review after every package

The parent records exact exit-criterion coverage, semantic correctness,
completeness against accepted successor model `1.0.0` and its mandatory global
composition proof, diff and protected-path scope,
deterministic tests and identities, documentation consistency, findings and
correction count, and input/output commits with clean Git state. Passing tests
do not replace semantic and diff review.

## Global hard stops and progress controls

Stop for the owner if the same command fails for the same reason three times;
the same material finding survives two correction attempts; a correction
weakens fact, coverage, gap, mutation, or atomic obligations; a new semantic or
authority choice is required; candidate/private behavior becomes truth; a
fixture-specific rule is proposed; a protected/prohibited path is accessed;
immutable model `1.2.0` would change; the successor delta would exceed
ADR-0031; protocol `/6` is proposed; or deterministic progress
cannot be demonstrated.

A correction counts as progress only when it reduces uncovered/contradictory
states, resolves a named material finding without regression, or restores a
required deterministic invariant.

## Verification commands

The package-specific checks are additive. WP3 and WP4 run the complete set:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-m1-slice4-protocol5-global-composition.ps1
pwsh -NoProfile -File eng/validate-m1-slice4-protocol5-global-composition.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-m1-slice4-protocol5-representability.ps1
pwsh -NoProfile -File eng/validate-m1-slice4-protocol5-representability.ps1
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Unit"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Contract"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Integration"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Evaluation"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Security"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Fault"
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
git diff --check
```

Run `/5` calibration twice into distinct disposable directories and compare
the declared canonical artifacts byte-for-byte. Run the representability
validator twice on each supported host and compare its machine summary and
tracked generated artifacts byte-for-byte. Run the global composition gate
twice on each supported PowerShell host and compare its summary byte-for-byte.
WP4 repeats the checks from the
exact detached WP3 commit before writing its attestation.

## Freeze and downstream handoffs

WP4 freezes exactly one public `/5` commit, protocol/schema/projection/tool
identities, required file hashes, dependency inventory, and aggregate algorithm.
The freeze is public evaluator authority only.

Product realignment remains a separate evaluator-blind task. It may consume
the accepted semantic and representation contracts but not private material or
review answers. This plan does not implement or freeze a product candidate.

Private corpus eligibility is a later separate custodian/governance decision
using only the sanitized public handoff. Private oracle/corpus qualification
is a fresh Stage B2 task only if separately authorized after eligibility. C2
scoring is another fresh one-shot role after exact candidate, evaluator, and
corpus freezes. No later role is authorized by a WP0-WP4 pass.

## Completion record

Append exact commits, checks, counts, review cycles, correction counts,
identities, hashes, boundaries, and downstream status to
[`../implementation-records/M1-slice-4.5.md`](../implementation-records/M1-slice-4.5.md).
Leave a clean unpushed local branch with one focused commit per work package.
