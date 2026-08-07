# Evaluator-v1 invalid attempts: no product verdict

Status: Completed historical incident; current disposition supplied by ADR-0032
Date: 2026-08-04
Last reviewed: 2026-08-07

## Summary

Slice 4 product implementation completed at commit
`98fe8a5a173116427bf78077673fd10e8d018103` before the evaluator-v1 repair
loop. Retained public EVAL-0052 and Slice-4-applicable EVAL-0086 gates passed.

Evaluator-v1 attempts later failed at evaluator, package, oracle, or execution-
contract admission. Zero authoritative hidden semantic assertions produced a
valid product verdict. Those attempts are historical
`blocked/EVALUATOR_ERROR` evidence and must not be represented as product
`PASS` or `FAIL`.

## Runtime isolation audit

The 2026-08-04 Stage-A preflight confirmed no diff from `98fe8a5` through the
working baseline in:

- `src/Infinium.Bethesda`;
- `src/Infinium.Coordinator`;
- `src/Infinium.Persistence`;
- `src/Infinium.Worker`; and
- `src/Infinium.Application/Runtime`.

Post-candidate evaluator-v1 maintenance changed evaluator schemas, fixture
package readers, public fixture envelopes, evaluation tooling, and
documentation, not the core Slice 4 semantic extraction or runtime execution
path.

## Disposition

- Evaluator v1 is retired and must not be resumed.
- Its attempts remain append-only invalid/blocked history.
- ADR-0027 and M1 Slice 4.5 replace the workflow with public evaluator rules,
  a private held-out corpus, and separate authoring/scoring/closeout stages.
- The product baseline remains frozen at `98fe8a5`; it is not rolled back or
  modified in response to invalid evaluator attempts.
- At this historical checkpoint, a valid held-out product verdict remained
  pending evaluator-v2 private corpus qualification and one-shot scoring in
  later fresh tasks.

## Current disposition

The evaluator-v2 successor history did not produce a valid private product
verdict. ADR-0032 now defers private held-out evaluation, retires protocol `/5`
unqualified, and authorizes Slices 5-9 to continue under the public M1
continuation verification profile. This incident remains evidence that
evaluator admission failures are not product results; it authorizes no private
or successor evaluator work.
