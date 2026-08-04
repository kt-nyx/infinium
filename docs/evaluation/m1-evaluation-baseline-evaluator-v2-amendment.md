# M1 evaluation baseline evaluator-v2 amendment

Status: Accepted
Accepted: 2026-08-04
Accepted by: Project owner
Last reviewed: 2026-08-04
Predecessor: [M1 evaluation baseline](m1-evaluation-baseline.md)

## Amendment model

The accepted M1 baseline remains authoritative except where this document
replaces evaluator protocol, ownership, terminal-state mapping, and held-out
gate sequencing under ADR-0027 and M1 plan revision `/3`.

## Public protocol and private corpus

Evaluation rules, schemas, adapter, canonicalization, comparison, scorer,
calibration, and terminal vocabulary are public. Hidden input data and expected
typed outputs remain in the separate evaluator-private repository with a
shallow manifest pinning exact public evaluator identities and hashes.

The public evaluator must pass answer-known calibration and be frozen before a
candidate is used against any private corpus. The private corpus must then be
independently qualified and frozen before scoring.

## Exact invocation binding

Every held-out invocation binds one exact:

- candidate commit and built-artifact identity;
- evaluator commit, protocol/schema/scorer/adapter identity; and
- corpus ID, version, aggregate hash, and member identities.

A changed tuple member creates a new invocation. Same-task repair or retry is
prohibited.

## Terminal mapping

- evaluator-v2 `PASS` maps to case execution state `passed`;
- evaluator-v2 `FAIL` maps to `failed` only after a valid admitted comparison;
- evaluator-v2 `EVALUATOR_ERROR` maps to `blocked` and carries no product
  verdict.

Manifest, tuple identity, retained-input, evaluator binary/dependency, oracle,
corpus, publication, infrastructure, and comparison-admission failures are
`EVALUATOR_ERROR`, not product `FAIL`. After exact admission, a resolved
candidate invocation that throws is product `FAIL` with category
`candidate_execution`; candidate projection/output-contract violations are
product `FAIL` with category `candidate_output_contract`.

One Stage C `score-corpus` command may admit one or more private members and
emits one aggregate terminal. Every member must be validly admitted; any
admission or evaluator failure makes the aggregate `EVALUATOR_ERROR` with no
product verdict. Otherwise any member mismatch makes the aggregate `FAIL` and
all-member agreement makes it `PASS`.

## Disclosure and contamination

Public closeout consumes only a sanitized attestation. Revealed or
product-driving hidden cases become development coverage, are retired from
held-out claims, and require materially independent replacement before a later
held-out claim. Oracle corrections require new independent evidence and a new
version; old evidence is invalidated, not rewritten.

## Partition reporting

Public and held-out evidence may be reported separately:

```text
Public Slice 4 evidence: passed
Evaluator-v1 held-out attempts: blocked / no product verdict
Evaluator-v2 held-out state: not run | passed | failed | blocked
Overall M1 gate: pending Slice 4.5 until both applicable partitions pass
```

Passing public evidence alone does not complete held-out EVAL-0052 or
applicable EVAL-0086. M1 remains pending until the valid held-out partition
passes and later required slices/cases complete.

## Historical `/2` adjudication and successor

The owner-supplied Stage C.5 adjudication is recorded in the
[sanitized incident](evaluator-v2-stage-c5-adjudication-incident.md). The
historical `/2` `FAIL` remains immutable, but its product verdict is invalidated.
No product correction is indicated. Evaluator `/2` is retired for the diagnosed
numeric typed-fact surface, the historical private corpus requires complete
replacement, and any later scoring is a new successor tuple and invocation.
