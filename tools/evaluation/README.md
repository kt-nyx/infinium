# Public evaluation tooling

`Infinium.EvaluatorV2` is the sole active held-out evaluation protocol. Its
rules, schemas, canonicalization, reflection adapter, scorer, and answer-known
calibration suite are public. Hidden inputs and expected semantic values are
not stored here.

The tool never discovers a candidate, evaluator, corpus, oracle, or output
location. A caller supplies an answer-free manifest, an oracle path available
only to the scoring role, and a result-directory path whose parent exists but
which does not yet exist. Candidate non-framework dependencies are fully
inventoried. Existing result files are never overwritten.

## Commands

```powershell
dotnet run --project tools/evaluation/Infinium.EvaluatorV2 -c Release -- protocol
dotnet run --project tools/evaluation/Infinium.EvaluatorV2 -c Release -- calibrate --result-dir <new-directory>
dotnet run --project tools/evaluation/Infinium.EvaluatorV2 -c Release -- adapt --manifest <manifest.json> --result-dir <new-directory>
dotnet run --project tools/evaluation/Infinium.EvaluatorV2 -c Release -- score --manifest <manifest.json> --oracle <expected-output.json> --result-dir <new-directory>
```

`adapt` is a public diagnostic for the exact black-box boundary. `score` is the
one-shot scoring command. Exit code `0` is `PASS`, `1` is product `FAIL` after
a valid comparison, and `2` is `EVALUATOR_ERROR` or invalid invocation.

The authoritative protocol identifier is `infinium.evaluator-v2/1`. Schemas
under `Infinium.EvaluatorV2/protocol/` define the answer-free manifest,
candidate output, expected output, raw assertions, sanitized result, and
calibration result. `protocol.json` defines ordering, set/sequence,
canonicalization, aggregation, and failure-stage rules.

## Evaluator-v1 compatibility

`Infinium.EvaluatorV2/LegacyV1/` contains readers retained only so public
regression fixtures and historical contract tests remain usable. No command in
the evaluator-v2 executable invokes them. They are not an active held-out
protocol and must not be extended into another v1 package version.
