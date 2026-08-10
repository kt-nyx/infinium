# Public evaluation tooling

`Infinium.EvaluatorV2` protocol `/4` is a frozen historical evaluator retained
only for the bounded public regression profile documented in
`docs/evaluation/m1-slice4-protocol-4-bounded-regression-usage.md`. Its rules,
schemas, canonicalization, reflection adapter, scorer, and answer-known
calibration remain public. It is not an active held-out workflow.

The tool never discovers a candidate, evaluator, corpus, oracle, or output
location. A caller supplies an answer-free manifest, an oracle path available
only to the scoring role, and a result-directory path whose parent exists but
which does not yet exist. Candidate non-framework dependencies are fully
inventoried. Existing result files are never overwritten.

## Authorized command

The only current authorized entry point is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-m1-slice4-protocol4-bounded-regression.ps1
```

or the same wrapper under PowerShell 7. `BOUNDED_REGRESSION_PASS` means only
historical freeze integrity, current reusable-core integrity, and allowlisted
current public regression health. It is not complete semantic, private held-
out, Slice 4.5, M1, reliability/readiness, or product acceptance.

The immutable executable still contains historical protocol, calibration,
adaptation, comparison, and scoring entry points. They are retained only as
historical bytes and are not copy-pasteable or authorized current workflows;
calibration is authorized only when the bounded wrapper invokes it internally.
Do not invoke any direct evaluator command, use candidate/product output,
access private material, or resume B2/C2/Stage D.
Historical exit code `0` meant `PASS`, `1` meant product `FAIL` only after a
valid comparison, and `2` meant `EVALUATOR_ERROR` or invalid invocation; no
valid current private terminal verdict exists.

The authoritative historical protocol identifier is `infinium.evaluator-v2/4`.
Active `/4` schemas under
`Infinium.EvaluatorV2/protocol/` define the answer-free manifest,
candidate output, expected output, raw assertions, sanitized result, and
calibration result. `protocol.json` defines ordering, set/sequence,
canonicalization, aggregation, and failure-stage rules.

Protocol `/4` cannot represent the accepted partial `RACE/DATA` outcome that
retains common contribution facts while omitting the unavailable later-layer
fact. The bounded profile excludes that state. Protocol `/5` is retired
unqualified with no implementation, freeze, private use, or verdict; its
consumed identities cannot be reused. Future evaluator work requires a new ADR
and plan after Slice 9 during M3 planning, and no future protocol identity is
selected here.

## Current public fixtures

`fixtures/tooling/Infinium.PublicFixtures/` owns the current
product/public-fixture readers and validates only active product schemas. It
has no evaluator protocol, scoring, private-corpus, or product-output
authority. The default solution references this library and has no dependency
on `Infinium.EvaluatorV2`.

The retired pre-v2 compatibility readers, predecessor `/3` schemas, and
obsolete pre-B2 proof commands are recoverable only through the Git objects in
`docs/evaluation/retired-evaluation-assets.v1.json`. Do not restore them to an
active build or infer current authority from their historical namespaces.
