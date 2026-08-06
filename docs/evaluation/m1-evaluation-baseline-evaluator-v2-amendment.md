# M1 evaluation baseline evaluator-v2 amendment

Status: Accepted
Accepted: 2026-08-04
Accepted by: Project owner
Last reviewed: 2026-08-05
Predecessor: [M1 evaluation baseline](m1-evaluation-baseline.md)

## Amendment model

The accepted M1 baseline remains authoritative except where this document
replaces evaluator protocol, ownership, terminal-state mapping, and held-out
gate sequencing under ADR-0027 and M1 plan revision `/3`.
ADR-0028 later binds the bounded semantic authority inside final protocol `/4`
without changing evaluator ownership or terminal mapping.

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

Public successor protocol `infinium.evaluator-v2/3` is qualified and frozen at
`34ed0c84165e9a49f44a88ecd87cac967132ebd7`. Its exact source/protocol inventory
and calibration identity are recorded in the
[successor Stage A freeze](evaluator-v2-stage-a-successor-freeze.json). At that
`/3` checkpoint, successor Stage B was unblocked. Private successor-corpus work
and held-out scoring had not run, and Stage D had not started.

## Final bounded held-out scope clarification

The owner-supplied sanitized Stage B2 review found that protocol `/3` required
exact failure codes, typed `AIDT` subfields, and internal taxonomy assignment
IDs that an independent oracle could not author from public authority and
hidden input bytes alone. The complete public disclosure is retained in the
[Stage B2 contract-gap record](evaluator-v2-successor-stage-b2-contract-gap.md).

The accepted
[final Slice 4 held-out scope amendment](m1-slice4-heldout-scope-final-amendment.md)
therefore separates implementation-specific public conformance from
independently specifiable held-out semantics. Protocol `/3` remains qualified
historical public evidence and is superseded before a valid successor corpus.
Protocol `/4` is the final authorized M1 evaluator revision. It does not waive
the held-out gate or authorize a product correction, Stage C2, Stage D, Slice
5, or a later `/5` evaluator.

Protocol `/4` was publicly qualified and frozen at
`3693d19563c636cd2879804633ca4ce52448d2c1`. Its machine-readable authority is
the [final bounded freeze handoff](evaluator-v2-stage-a-final-bounded-freeze.json).
At that freeze checkpoint, the existing successor-corpus inputs were permitted
to resume B2 once; no oracle, corpus fingerprint/freeze/tag, comparison, Stage
C2 score, or Stage D work had occurred under `/4`.

## Owner semantic disposition

ADR-0028 and the accepted
[semantic-authority owner disposition](m1-slice4-semantic-authority-owner-disposition.md)
resolve the six later authority-completion mismatches without changing
protocol `/4`. B2 may no longer resume under the evaluator freeze alone.
Public product/specification realignment,
requalification, and a newly frozen conforming candidate are required first.

That prerequisite was satisfied on 2026-08-05 by the independently reviewed
public realignment and exact candidate freeze at
`a98d648bd0adb2751ee0c09828e0227b1583950f`. The machine-readable candidate
handoff is
[the public product candidate freeze](m1-slice4.5-public-product-candidate-freeze.json).
At that checkpoint, one fresh private oracle reviewer was permitted to resume
B2 once. That authorization was subsequently consumed by the terminal attempt
below. Oracle/corpus qualification, C2 scoring, and Stage D remain unrun.

## Superseding public-authority status

The single authorized B2 resume subsequently ran and stopped without an
oracle, candidate execution, comparison, scoring, or product verdict. The
owner-authorized public contract-completion attempt then hard-stopped after
its one permitted correction pass left a second exact cross-family authority
gap. Its [public attestation](m1-slice4-protocol-4-oracle-authorability-review.md)
did not reach candidate conformance. Another B2 operation, corpus
qualification, C2, Stage D, Slice 5, and protocol `/5` are not authorized by
this status. Project-owner milestone-plan disposition was required and is
supplied by the successor section below.

## Accepted deterministic totality successor

ADR-0029 resolves the disclosed partial-decode choice and requires lower-layer
evidence retention with exact higher-layer coverage gaps. The accepted
[Pre-B2 totality plan](../plans/slices/M1-slice-4.5-pre-B2-evidence-contract-totality.md)
now owns public completion as work ID `M1/S4.5/PRE-B2`. Its total state/fact
model and mechanical gate must pass before fixtures, a fresh product-blind
review, or candidate conformance can support any new held-out-authorability
claim. Private B2 remains separately unauthorized.
