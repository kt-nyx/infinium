# RESEARCH-0022 benchmark artifacts

Status: Completed
Disposition: retained research artifact set
Last reviewed: 2026-07-26

These files support the bounded synthetic candidate-index and ranking
experiment in `RESEARCH-0022-candidate-index-and-ranking.md`.

## Files

- `benchmark-config.json` defines exact scale populations, seeds, rule-family
  order, lane declarations, distractor counts, and the provider-neutral
  workload model.
- `benchmark-truth-manifest.json` is the separately materialized truth
  manifest. It explicitly identifies every supported positive, matched
  negative, and unsupported case, including expected disposition and
  canonical participant mod IDs.
- `benchmark.mjs` constructs deterministic generated index inputs from the
  config and truth cases, invokes candidate detection without providing truth
  or expected dispositions, ranks the score-independent mandatory lane, and
  only then evaluates detector output against the truth manifest.
- `benchmark-results.json` contains every run, per-case observed disposition,
  every matched-negative outcome, aggregate metrics, timings, memory
  observations, candidate participant pairs, and provider-neutral workload
  envelopes.

The truth manifest and fixture constructor are still authored by the same
research probe. The `constructionSmoke` result is therefore only a
construction-coupled smoke check. Recall numbers come from a separate
post-detection comparison in which the detector has not received the truth
manifest, but they are not an independent implementation or independently
authored fixture corpus.

## Replay

From the repository root, using Node.js:

```powershell
node docs/research/investigations/artifacts/RESEARCH-0022/benchmark.mjs prepare
node --expose-gc docs/research/investigations/artifacts/RESEARCH-0022/benchmark.mjs run
```

`prepare` deterministically replaces `benchmark-truth-manifest.json`.
`run` requires that manifest's recorded configuration hash to match
`benchmark-config.json`, then replaces `benchmark-results.json`.

Timing, process-memory values, `generatedAt`, and therefore the result-file
hash may change on replay. Seeds, truth identities, generated structural
inputs, dispositions, candidate counts, canonical participant-pair counts,
and workload arithmetic should remain stable on the same script/config/truth
bytes.

## Observed artifact identities

The report records the exact identities from the reviewed run. They can be
recomputed with:

```powershell
Get-ChildItem docs/research/investigations/artifacts/RESEARCH-0022 -File |
  Get-FileHash -Algorithm SHA256
```

The benchmark contains no production code, real-mod rule, provider call, live
model call, or current provider price.
