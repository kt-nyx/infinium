# M1 Slice 4.5 — Held-out evaluation v2

Status: Accepted; final bounded `/4` revision authorized, implementation pending
Owner: Project owner
Prepared: 2026-08-04
Accepted: 2026-08-04
Last reviewed: 2026-08-04
Parent plan: `infinium.plan.m1.backend-semantic-proof/3`
Target: Slice 4.5, between implementation-complete Slice 4 and blocked Slice 5

## Objective

Replace evaluator v1 with a qualified and frozen evaluator v2 whose rules are
public, whose test data and expected answers remain private, and which can issue
one valid held-out verdict for the frozen Slice 4 candidate.

## Authority and dependencies

- accepted product requirements and taxonomy `0.1.0`;
- ADR-0001, ADR-0003, ADR-0009, ADR-0015, ADR-0018, ADR-0019, ADR-0021,
  ADR-0026, and ADR-0027;
- accepted M1 evaluation baseline plus evaluator-v2 amendment;
- M1 plan revision `/3`;
- completed Slice 3.5 and Slice 4 implementation records; and
- evaluator-private fixture governance v2.

Frozen candidate:
`98fe8a5a173116427bf78077673fd10e8d018103`.

The candidate source tree remains detached, clean, and unmodified. The
evaluator records its exact build and artifact identity separately from the
public evaluator identity.

## Repository responsibilities

### Public product repository

- evaluator-v2 protocol and schemas;
- normalization, canonicalization, comparison, aggregation, and error rules;
- standalone scorer and exact-candidate black-box adapter;
- answer-known public calibration/mutation data and tests;
- public qualification/freeze evidence and hashes; and
- sanitized result contract and public closeout records.

### Evaluator-private repository

- hidden inputs and expected typed outputs;
- shallow public evaluator identity/hash manifest;
- independent corpus construction, oracle review, qualification, and freeze
  evidence;
- private raw execution/scoring output; and
- sanitized attestation plus access/contamination records.

## Scope

### Included

- retire evaluator-v1 as an active protocol while preserving history and
  public development/regression fixtures;
- move evaluator-only package/corpus policy out of production assemblies;
- define versioned public evaluator-v2 contracts;
- implement a standalone public scorer and candidate adapter;
- qualify the scorer through public calibration, mutations, malformed inputs,
  determinism, error classification, and output-write confinement;
- later qualify/freeze a private corpus in a fresh task;
- later run exactly one newly bound held-out invocation in a separate scoring
  task; and
- consume only a sanitized result in public closeout.

### Explicit non-scope

- no new product semantics;
- no new Bethesda record families or fields;
- no Slice 5 candidate, finding, case, documentation, or replay work;
- no live provider or billable calls;
- no evaluator repair during scoring;
- no use of candidate output as expected truth;
- no private fixture access during public evaluator implementation/review;
- no legacy archive access; and
- no push unless separately requested.

## Public protocol and scorer deliverables

The public protocol identifies at minimum:

- candidate commit and built-artifact identity;
- evaluator commit, scorer/adapter identity, protocol ID, and schema hashes;
- corpus ID/version/hash;
- answer-free execution manifest;
- candidate semantic-output and expected semantic-output/oracle schemas;
- canonical typed assertion-result schema;
- sanitized evaluation-result schema;
- `PASS`, `FAIL`, and `EVALUATOR_ERROR`; and
- terminal failure-stage vocabulary.

Public rules cover accepted provider/plugin order, identity and FormKey
representation, ordering/set/sequence behavior, missing/extra facts,
unsupported/gap behavior, malformed input/output, typed comparison,
aggregation, and evaluator/infrastructure error boundaries.

The scorer validates identities and manifests, validates and canonicalizes
candidate/expected output, compares typed values/identities, emits deterministic
assertions, never modifies input, contains no case-specific expected values,
and writes only within an explicitly supplied result directory.

## Public calibration requirements

Answer-known calibration must prove:

- canonical known-correct output passes;
- wrong winning record fails;
- reversed override chain fails;
- wrong regular and light-plugin FormKeys fail;
- missing expected and unexpected extra semantic facts fail;
- wrong link, ownership, and placement data fail;
- incorrect unsupported/gap handling fails;
- malformed candidate output is rejected at the correct boundary;
- broken manifests and malformed oracles are `EVALUATOR_ERROR`, not `FAIL`;
- each mutation reaches its intended assertion rather than an unrelated schema
  rejection; and
- repeated runs are byte-deterministic after excluding explicitly declared
  invocation time metadata from canonical evidence.

## Private corpus requirements

- materially independent hidden inputs and expected typed outputs;
- independent oracle construction without candidate output;
- exact case/denominator coverage for held-out EVAL-0052 and applicable
  EVAL-0086;
- shallow pin of the frozen public evaluator and required hashes;
- exact corpus/member identities and fingerprints;
- answer isolation, contamination, redistribution, and replay evidence;
- valid public-schema conformance; and
- immutable qualification/freeze record.

## Four-stage execution model

1. **Stage A — public architecture and implementation:** complete public
   protocol, scorer, adapter, calibration, evaluator-v1 retirement, review,
   and freeze. No private access or held-out scoring.
2. **Stage B — private corpus authoring/qualification:** fresh context; use the
   frozen public evaluator only as contract authority; return sanitized corpus
   identity/freeze evidence.
3. **Stage C — one-shot scoring:** separate fresh context; execute the exact
   frozen candidate/evaluator/corpus tuple once; repair nothing; return one
   sanitized terminal attestation.
4. **Stage D — public closeout:** consume only the sanitized attestation; do
   not change frozen evaluator code, schemas, adapter, or calibration.

## Qualification and freeze procedure

1. Complete public implementation and calibration.
2. Run focused and full repository checks from a clean branch.
3. Perform one fresh-context public-only review and at most one focused
   correction pass.
4. Rerun all affected and final checks.
5. Identify one exact public commit containing protocol, schemas, scorer,
   adapter, and calibration as the frozen evaluator-v2 commit.
6. In Stage B, pin those public identities and hashes, qualify the private
   corpus independently, and freeze it.
7. Do not modify the frozen public evaluator during Stages B through D.

## One-shot scoring procedure

1. Verify candidate commit/artifact and dependency identity.
2. Verify evaluator commit/protocol/schema/scorer/adapter identity.
3. Verify corpus ID/version/hash and shallow public hash pins.
4. Validate the answer-free execution manifest.
5. Execute the candidate through the public black-box adapter.
6. Validate candidate and expected outputs under public schemas.
7. Canonicalize and compare typed values.
8. Write raw records only to the private designated result directory.
9. Emit exactly one `PASS`, `FAIL`, or `EVALUATOR_ERROR` sanitized result.
10. Stop. Do not repair, reseal, or retry.

## Terminal handling

- `PASS` completes the Slice 4.5 held-out gate.
- `FAIL` is a product failure only after a valid comparison; any production
  response requires a new implementation task and contaminates/reclassifies
  any revealed or product-driving case.
- `EVALUATOR_ERROR` blocks Slice 4.5 and carries no product verdict. A separate
  maintenance task may qualify a successor, after which a new scoring task
  runs a newly bound invocation.

## Verification commands

```powershell
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

Also run the evaluator-v2 public `calibrate` command twice into separate
disposable result roots and compare its canonical outputs byte-for-byte.

## Acceptance criteria

Slice 4.5 is complete only when the public evaluator and private corpus are
qualified/frozen, one valid held-out invocation returns `PASS`, the attestation
binds the exact tuple, and public closeout records held-out EVAL-0052 and
applicable EVAL-0086 as passed without exposing private data. Stage A alone
does not complete Slice 4.5.

## Rollback and version retirement

Evaluator v1 is retired and never resumed. Its commits, public regression
fixtures, blockers, and incident history remain retained. A faulty evaluator
v2 version is retired append-only; a successor protocol/evaluator version is
qualified and frozen separately. Rollback never rewrites a frozen verdict or
private corpus.

## Completion record

Maintain
[`../implementation-records/M1-slice-4.5.md`](../implementation-records/M1-slice-4.5.md)
with exact Stage A branch/commit, candidate/evaluator identities, moved files,
protocol/schema IDs, calibration and full-check results, review/corrections,
private corpus freeze identity when available, held-out invocation identity,
sanitized terminal result, contamination state, gaps, and push state.

## Stage A successor freeze

The owner-authorized corrective pass retired public evaluator commit
`8023cdf776a25210bcc80e7574c1aaecde278b6b` before private qualification; it
produced no held-out product verdict. The unique successor is discoverable in
[`../../evaluation/evaluator-v2-stage-a-freeze.json`](../../evaluation/evaluator-v2-stage-a-freeze.json).

Stage B qualifies prepared expected outputs and deliberate mutations with:

```text
compare-prepared --manifest <prepared-comparison-manifest.json> --candidate-output <prepared-candidate-output.json> --oracle <expected-output.json> --result-dir <new-directory>
```

Stage C performs the single top-level held-out invocation, for a one- or
multi-member corpus, with:

```text
score-corpus --manifest <corpus-execution-manifest.json> --result-dir <new-directory>
```

Stage B and Stage C remain separate, owner-authorized fresh tasks. This Stage A
session does not access or orchestrate either private stage.

## Stage C.5 adjudication and successor disposition

The owner-supplied sanitized adjudication is recorded in the
[Stage C.5 incident](../../evaluation/evaluator-v2-stage-c5-adjudication-incident.md).
The historical `/2` Stage C `FAIL` remains immutable, but its product verdict
was invalidated. No product correction is indicated. Evaluator `/2` is retired
for the diagnosed numeric typed-fact surface, and the historical private corpus
requires complete replacement.

The authorized public task froze successor protocol `infinium.evaluator-v2/3`
at `34ed0c84165e9a49f44a88ecd87cac967132ebd7`. Its machine-readable inventory is
[`evaluator-v2-stage-a-successor-freeze.json`](../../evaluation/evaluator-v2-stage-a-successor-freeze.json).
Later successor corpus construction and scoring are new fresh tasks and a new
invocation, not a retry. Successor Stage B is unblocked but has not run. Slice
4.5 remains active and blocked; Stage D has not started.

## Final bounded held-out projection amendment

The owner-supplied sanitized Stage B2 review is recorded in
[`../../evaluation/evaluator-v2-successor-stage-b2-contract-gap.md`](../../evaluation/evaluator-v2-successor-stage-b2-contract-gap.md).
The successor inputs were constructed and independently byte-reviewed without
input correction, candidate execution, product source/output, or
contamination, but `/3` oracle construction stopped before expected outputs
because exact failure codes, typed `AIDT` subfields, and internal taxonomy
assignment IDs were not independently authorable.

The final accepted scope is defined by
[`../../evaluation/m1-slice4-heldout-scope-final-amendment.md`](../../evaluation/m1-slice4-heldout-scope-final-amendment.md):

- public conformance retains exact product diagnostics, the complete typed
  `AIDT` mapping, and internal taxonomy/provenance/serialization identifiers;
- held-out comparison retains only independently specifiable semantic facts;
- protocol `/3` remains qualified historical public evidence but is
  superseded before a valid successor corpus; and
- protocol `/4`, scorer/adapter `4.0.0`, and projection `3.0.0` are the final
  authorized M1 evaluator revision.

Protocol `/4` must be qualified and frozen through the same public-only review,
determinism, identity, write-confinement, adapter, prepared-comparison,
aggregate, and full-repository gates already required by this plan. The public
oracle-authority matrix is normative for every emitted fact family.

After the `/4` freeze, one fresh private oracle reviewer may resume B2 once.
If another public-contract or projection gap prevents authoritative oracle
construction, stop: do not create `/5`, do not expand the evaluator, and do
not use product output as truth. Record the held-out gate as an unresolved
evaluation gap and return to the project owner for milestone-plan disposition.
Stage C2 and Stage D remain separately authorized future tasks, Slice 5 stays
blocked, and this amendment does not waive the held-out gate.
