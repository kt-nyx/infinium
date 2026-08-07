# M1 backend semantic proof plan revision 3 amendment

Status: Accepted; held-out sequencing clauses superseded by ADR-0032
Accepted: 2026-08-04
Accepted by: Project owner
Last reviewed: 2026-08-07
Plan revision: `infinium.plan.m1.backend-semantic-proof/3`
Predecessor: [M1 backend semantic proof revision 2](M1-backend-semantic-proof-adr0026-amendment.md)

## Revision model

Revision `/3` incorporates revision `/2` and its predecessor except for the
sequencing, evaluator ownership, and gate clauses replaced below. Historical
plans and execution evidence remain unchanged. ADR-0027, evaluator-private
fixture governance v2, the evaluator-v2 baseline amendment, and the Slice 4.5
execution plan are additional authority.

ADR-0032 later supersedes only this revision's requirement that a private
held-out `PASS` precede Slice 5. The historical evaluator chronology below
remains evidence; current M1 sequencing is governed by the evaluator-deferral
plan and the M1 continuation verification profile.

## Established implementation state

Preflight on 2026-08-04 confirmed that Slice 4 implementation commit
`98fe8a5a173116427bf78077673fd10e8d018103` is an ancestor of the working
baseline, its detached locked restore/Release build/category/full-suite rerun
passes, and later evaluator-v1 maintenance does not change the core Slice 4
runtime directories. Slice 4 product implementation is complete at that
commit.

Evaluator-v1 attempts failed at evaluator, package, oracle, or execution-
contract admission before authoritative hidden semantic assertions judged the
product. They produced no valid held-out product verdict and remain historical
`blocked/EVALUATOR_ERROR` evidence.

## Slice ownership correction

Slice 4 owns:

- bounded Bethesda semantic and typed-index implementation;
- public EVAL-0052 scenarios;
- public Slice-4-applicable EVAL-0086 scenarios;
- public unit, contract, integration, evaluation, security, and fault
  verification; and
- semantic and boundary review.

Slice 4.5 owns:

- qualified and frozen evaluator-v2 public protocol, schemas, adapter, scorer,
  and calibration suite;
- qualified and frozen private held-out corpus;
- one valid held-out execution against the ADR-0028-conforming frozen candidate
  `a98d648bd0adb2751ee0c09828e0227b1583950f`; and
- held-out EVAL-0052 and applicable EVAL-0086 acceptance.

This is a plan-decomposition correction, not a waiver, reduction, or
reinterpretation of held-out acceptance.

## Sequencing and gates

- Slices 0 through 4 are implementation-complete.
- Slice 4.5 is active under the accepted
  [execution plan](../slices/M1-slice-4.5-held-out-evaluation-v2.md).
- At this historical revision `/3` checkpoint, Slice 5 was blocked until Slice
  4.5 obtained one valid held-out `PASS` for the exact candidate/evaluator/
  corpus tuple.
- `EVALUATOR_ERROR` leaves Slice 4.5 blocked and carries no product verdict.
- M1 remains active. No later M1 slice or milestone completion is implied.

## Completion evidence transfer

### Slice 4 completion evidence

- bounded Bethesda semantic and typed-index implementation;
- public EVAL-0052 scenarios;
- public Slice-4-applicable EVAL-0086 scenarios;
- public unit, contract, integration, evaluation, security, and fault
  verification; and
- semantic and boundary review.

### Slice 4.5 completion evidence

- qualified and frozen evaluator-v2 public protocol/scorer;
- qualified and frozen private held-out corpus;
- one valid held-out execution against the frozen Slice 4 candidate; and
- held-out EVAL-0052 and applicable EVAL-0086 pass.

## Revision-specific verification

Revision `/3` requires the public evaluator-v2 calibration and full repository
verification defined by the Slice 4.5 plan. Private qualification and scoring
occur only in their later fresh tasks and return sanitized evidence under
governance v2.

## Protocol `/4` B2 terminal disposition

The single authorized private B2 resume ran once and stopped without an oracle,
candidate execution, scoring, or product verdict because exact
evaluator-visible identity and link/state vocabulary remained incompletely
specified by public authority. The project owner accepted the public-only
[oracle-contract completion and held-out disposition plan](../slices/M1-slice-4.5-protocol-4-oracle-contract-completion.md).

Evaluator `/4` and candidate `a98d648` remain frozen. Public completion,
answer-free product-blind authorability review, and frozen conformance audit are
the next work. Another B2 task, corpus qualification, C2, Stage D, protocol
`/5`, and Slice 5 are not authorized by this disposition. The existing M1 gate
was unchanged at that checkpoint: Slice 5 remained blocked until Slice 4.5
obtained one valid held-out `PASS` for an exact qualified tuple.

## Public oracle-contract authorability stop

The accepted public completion attempt later exercised its single correction
pass and then hard-stopped when independent re-review found a second material
public-authority gap. Candidate conformance was not inspected or classified.
The evaluator and candidate identities remain unchanged. Project-owner
milestone-plan disposition, not private execution or protocol `/5`, is the
next role. The Slice 4.5 and Slice 5 gates remain blocked.

## Accepted layered-evidence totality disposition

ADR-0029 and accepted work `M1/S4.5/PRE-B2` now supply that owner disposition.
The public sequence is: total evidence-state/fact model, deterministic totality
gate, model-derived synthetic exercises, fresh product-blind review, frozen
candidate classification, and public governance closeout. The partial
`RACE/DATA` semantic choice is closed; comprehensive contract proof remains
pending at WP1. The existing Slice 4.5 held-out and Slice 5 gates are unchanged,
and no B2, C2, Stage D, or `/5` work is authorized.

## Owner-authorized protocol `/5` successor

Pre-B2 WP1-WP5 completed and WP5 classified a frozen evaluator `/4`
representation gap. ADR-0030 now supersedes only ADR-0027 decision 15 and
authorizes accepted public work `M1/S4.5/PRE-B2/V5/WP0` through WP4 to qualify
and freeze one `/5` successor against immutable semantic model `1.2.0`.
Candidate implementation, private access, corpus eligibility, B2, C2, Stage D,
and Slice 5 remain separate and unauthorized by this amendment note.

## Current evaluator-deferral and continuation disposition

ADR-0032 and accepted work `M1/S4.5/EVAL-CLOSEOUT` supersede the active status
and sequencing statements above without rewriting their chronology.

- Slice 4 public conformance passed for exact candidate
  `a98d648bd0adb2751ee0c09828e0227b1583950f` and its declared scope.
- Protocol `/4` is frozen historical public evidence and may run only through
  its bounded public regression profile. Its known partial `RACE/DATA`
  representation gap is excluded, so it cannot issue a complete current
  semantic or held-out verdict.
- Protocol `/5` is retired unqualified. It has no implementation, freeze,
  private use, or verdict; WP1/WP1R/WP1V and WP2-WP4 are historical or
  unstarted chronology, not resumable work.
- Private held-out evaluation is deferred. No current private `PASS`, `FAIL`,
  or valid product-scoring `EVALUATOR_ERROR` exists, and B2, C2, and Stage D
  are not authorized.
- Evaluator-deferral closeout is accepted. Slice 4.5 is closed by owner
  disposition, and Slice 5 is the next eligible product package gated by the
  accepted
  [M1 continuation verification profile](../../evaluation/m1-continuation-verification-profile.md).
- M1 remains active. Public conformance, bounded `/4` regression health,
  evaluator qualification, private held-out evaluation, and product
  reliability/readiness are distinct claims.
