# Evaluator v2 Stage C.5 adjudication incident

Status: Accepted owner-supplied disposition; successor work active
Last reviewed: 2026-08-04

## Scope and source

This is the sanitized public record of the project-owner-supplied Stage C and
Stage C.5 history for M1 Slice 4.5. It was written without access to the
evaluator-private repository, private inputs, oracles, assertions, or raw
candidate output.

Historical identities:

- frozen candidate: `98fe8a5a173116427bf78077673fd10e8d018103`;
- historical public evaluator: `72616fb6fbb3db7021e8100adc12a251c427f8d1`,
  protocol `infinium.evaluator-v2/2`;
- historical Stage C result commit:
  `30185b478904d08f073576d652000f06b76986db`;
- historical terminal: `FAIL`, invocation count `1`; and
- sanitized Stage C.5 adjudication commit:
  `7a4842b91eca79d7f7623dc414d6e42f3fcf54e2`.

## Adjudicated disposition

The historical Stage C `FAIL` remains immutable as the terminal emitted by its
frozen tuple. Stage C.5 invalidated its product verdict; it did not rewrite the
historical result.

The aggregate disposition is:

- historical product verdict: invalidated;
- product correction required: false;
- evaluator successor required: true; and
- materially independent successor corpus required: true.

No Slice 4 product correction is indicated. Evaluator `/2` is retired as
non-authoritative for the diagnosed numeric typed-fact surface.

## Diagnosed public evaluator defect

Slice 4 deliberately projects placement components as semantic `number` facts.
An integral-valued floating-point component may serialize as the valid JSON
numeric token `10`. Evaluator `/2` inferred that token to be semantic
`integer` because `JsonElement.TryGetInt64` succeeded, then rejected the fact
whose declared `value_type` was `number`.

The successor evaluator must validate the JSON value against its declared
semantic `value_type`. Semantic `number` values compare numerically, so `10`
and `10.0`, both declared as `number`, are equal. Semantic `integer` remains a
distinct exact-integer type. This correction changes no product placement
contract.

## Sanitized private-corpus defect

The adjudication also established an independent corpus/oracle defect. A
malformed-boundary member expected a declared 32 MiB expansion to exceed the
product's normal decompression authority, while the frozen candidate's normal
bound is 64 MiB. The member and oracle are invalid. The affected private
members have lost held-out eligibility and require materially independent
replacement; this public task does not construct or inspect those replacements.

The product's 64 MiB limit remains unchanged.

## Gate state

- Slice 4 product implementation remains frozen at `98fe8a5`.
- Slice 4.5 remains active and blocked.
- Historical evaluator `/2` and its Stage-A freeze remain immutable evidence.
- The historical private corpus requires complete replacement.
- Successor public-evaluator maintenance is authorized as a fresh public-only
  task.
- Successor private-corpus construction and held-out scoring have not run.
- Any later scoring is a new tuple and invocation, not a retry.
- Stage D has not started.

