# M1 backend semantic proof plan revision 3 amendment

Status: Accepted
Accepted: 2026-08-04
Accepted by: Project owner
Last reviewed: 2026-08-04
Plan revision: `infinium.plan.m1.backend-semantic-proof/3`
Predecessor: [M1 backend semantic proof revision 2](M1-backend-semantic-proof-adr0026-amendment.md)

## Revision model

Revision `/3` incorporates revision `/2` and its predecessor except for the
sequencing, evaluator ownership, and gate clauses replaced below. Historical
plans and execution evidence remain unchanged. ADR-0027, evaluator-private
fixture governance v2, the evaluator-v2 baseline amendment, and the Slice 4.5
execution plan are additional authority.

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
- one valid held-out execution against frozen candidate `98fe8a5`; and
- held-out EVAL-0052 and applicable EVAL-0086 acceptance.

This is a plan-decomposition correction, not a waiver, reduction, or
reinterpretation of held-out acceptance.

## Sequencing and gates

- Slices 0 through 4 are implementation-complete.
- Slice 4.5 is active under the accepted
  [execution plan](../slices/M1-slice-4.5-held-out-evaluation-v2.md).
- Slice 5 is blocked until Slice 4.5 obtains one valid held-out `PASS` for the
  exact candidate/evaluator/corpus tuple.
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
