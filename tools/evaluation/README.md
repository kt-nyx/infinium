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

## Commands

The only current authorized entry point is:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-m1-slice4-protocol4-bounded-regression.ps1
```

or the same wrapper under PowerShell 7. `BOUNDED_REGRESSION_PASS` means only
historical freeze integrity, current reusable-core integrity, and allowlisted
current public regression health. It is not complete semantic, private held-
out, Slice 4.5, M1, reliability/readiness, or product acceptance.

The executable retains the following historical direct commands because its
bytes are immutable:

```powershell
dotnet run --project tools/evaluation/Infinium.EvaluatorV2 -c Release -- protocol
dotnet run --project tools/evaluation/Infinium.EvaluatorV2 -c Release -- calibrate --result-dir <new-directory>
dotnet run --project tools/evaluation/Infinium.EvaluatorV2 -c Release -- adapt --manifest <manifest.json> --result-dir <new-directory>
dotnet run --project tools/evaluation/Infinium.EvaluatorV2 -c Release -- score --manifest <manifest.json> --oracle <expected-output.json> --result-dir <new-directory>
dotnet run --project tools/evaluation/Infinium.EvaluatorV2 -c Release -- compare-prepared --manifest <manifest.json> --candidate-output <candidate-output.json> --oracle <expected-output.json> --result-dir <new-directory>
dotnet run --project tools/evaluation/Infinium.EvaluatorV2 -c Release -- score-corpus --manifest <corpus-manifest.json> --result-dir <new-directory>
```

These direct commands describe historical capability and are prohibited for
new current execution except for `calibrate` when invoked inside the bounded
wrapper. Do not run `adapt`, `score`, `compare-prepared`, or `score-corpus`,
use candidate/product output, access private material, or resume B2/C2/Stage D.
Historical exit code `0` meant `PASS`, `1` meant product `FAIL` only after a
valid comparison, and `2` meant `EVALUATOR_ERROR` or invalid invocation; no
valid current private terminal verdict exists.

The authoritative protocol identifier is `infinium.evaluator-v2/4`. Immutable
`/3` schemas remain predecessor evidence; active `/4` schemas under
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

## Evaluator-v1 compatibility

`Infinium.EvaluatorV2/LegacyV1/` contains readers retained only so public
regression fixtures and historical contract tests remain usable. No command in
the evaluator-v2 executable invokes them. They are not an active held-out
protocol and must not be extended into another v1 package version.
