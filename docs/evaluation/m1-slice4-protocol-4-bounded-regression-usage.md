# M1 Slice 4 protocol `/4` bounded regression usage

Status: Accepted active bounded public regression profile

Last reviewed: 2026-08-10

Date: 2026-08-07

Work ID: `M1/S4.5/EVAL-CLOSEOUT/WP2`

Authority: ADR-0032, the accepted evaluator-deferral plan, and the accepted
[freeze-boundary clarification](evaluator-history.md)

## Claim boundary

The retained `/4` evaluator is historical public evidence with a narrowly
reusable regression core. It is not a current full semantic evaluator.
`BOUNDED_REGRESSION_PASS` means only:

1. all 23 raw Git blobs at evaluator commit
   `3693d19563c636cd2879804633ca4ce52448d2c1` match the immutable final freeze;
2. all 20 current non-test runtime/schema/core paths in that freeze still have
   their frozen bytes and identities; and
3. the three attributed current public test files plus the explicitly
   allowlisted calibration and focused tests pass at the recorded current
   commit.

It never means the evolved tests are the original frozen qualification suite.
It never means a complete current semantic, private held-out, Slice 4.5, M1,
reliability, readiness, or product `PASS` or verdict.

## Machine profile and command

The machine-readable contract is
[`specifications/m1-slice4-protocol-4-bounded-regression-profile.json`](specifications/m1-slice4-protocol-4-bounded-regression-profile.json).
It fixes the freeze and protocol/projection identities, exact current evolved-
test identities, calibration identity, public test allowlist, excluded state,
prohibited modes/claims, and consumed `/5` identities.

Run only:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/invoke-m1-slice4-protocol4-bounded-regression.ps1
pwsh -NoProfile -File eng/invoke-m1-slice4-protocol4-bounded-regression.ps1
```

The final output line must be exactly `BOUNDED_REGRESSION_PASS`. The wrapper
fails closed before testing if any identity, hash, classification, command,
claim, excluded state, or reserved identity drifts.

Refusal coverage is public and has no evaluator/candidate/private input:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/test-m1-slice4-protocol4-bounded-regression-refusals.ps1
pwsh -NoProfile -File eng/test-m1-slice4-protocol4-bounded-regression-refusals.ps1
```

## Three verification layers

### 1. Historical integrity

The wrapper reads each `required_public_files` path as a raw Git blob at the
exact evaluator commit, hashes the raw bytes, and requires 23/23 exact length
and SHA-256 matches. It separately pins the unchanged 6,972-byte freeze JSON
at SHA-256
`2e30980f9e8628bf88c519e12c510c86a9c3ff2f6a7374b796fd8e6b769907d6`.
This proves historical reproducibility; it does not claim the current checkout
contains the original test suite.

### 2. Current reusable core

The wrapper mechanically classifies every freeze-manifest path outside
`tests/` as core. It requires exactly 20 paths and exact current byte matches.
The frozen project, runtime sources, protocol declaration, schemas,
canonicalizer, scorer, adapter, calibration, write authority, and frozen test-
independent dependencies therefore fail closed on missing files, hash drift,
extra claimed dependencies, or identity drift.

### 3. Current public regression

These three files are current public regression evidence changed only by the
two authorized public commits below, listed newest first:

1. repository normalization commit
   `8e75995f9f0f6b3934f806af08fb46799fc6e0ea`; and
2. product-realignment commit
   `a98d648bd0adb2751ee0c09828e0227b1583950f`.

| Path | Current bytes | Current SHA-256 |
|---|---:|---|
| `tests/Infinium.EvaluationTests/BethesdaOracleAgreementEvaluationTests.cs` | 30,967 | `ba99dedcb9a98ddb4e2c63eb2b900e9a7c314c2d3af36f2368f715ac7ad74f70` |
| `tests/Infinium.EvaluationTests/BethesdaSemanticExtractionEvaluationTests.cs` | 20,219 | `58681e739cb170cbffad0cd30f41e3ec9e10c181603a5f33b829a32e0745804f` |
| `tests/Infinium.EvaluationTests/EvaluatorV2PublicProtocolTests.cs` | 34,294 | `821c7bae79c049b3d0bcc52e772e2c55e4af3a16840b7510570f6800b4865e8d` |

The evaluator and out-of-solution regression-project `packages.lock.json`
files are current restore metadata, not members of the 20-file frozen core.
They are refreshed only to represent the authorized current product project
graph; they do not change protocol `/4` source, schema, or semantic identities.

The wrapper runs the 56-case answer-known public calibration and exactly eight
public-only tests covering calibration mutations, typed values,
canonicalization, fact-family authority, result-directory write confinement,
sanitized null handling, production-source isolation, and evaluator identity.
It does not run adapter/product fixtures, candidate execution, expected-output
comparison, corpus work, or scoring.

## Required exclusion

The accepted partial `RACE/DATA` state retains common structural contribution
facts while omitting the unavailable later-layer `face_gen_head` fact and
publishing its exact coverage gap. `/4` cannot represent that smaller retained
fact set because the affected projected object is atomic. The wrapper excludes
that state; it does not normalize, reject, reinterpret, or derive truth for it.

Any proposed regression that reaches this state or any post-freeze semantic
surface not explicitly represented by the public calibration is prohibited.

## Prohibited entry points and claims

The wrapper refuses `adapt`, `score`, `compare-prepared`, `score-corpus`,
private-corpus, held-out, and full-current modes. It neither accepts nor reads
candidate manifests, candidate output, oracles, corpus manifests, private
paths, expected answers, or product output.

Direct frozen-tool commands remain present only because `/4` is immutable.
They are not authorized current workflows. Product output and the retired
`/5` proof artifacts may not author expected truth.

## Retired `/5` identity reservation

The following consumed historical identities must never be reused:

- `infinium.evaluator-v2/5`;
- `infinium.m1-slice4.protocol-5-evidence-contract/1.0.1`;
- `infinium.m1-slice4.protocol-5-projection-representation/1.2.0`;
- `infinium.evaluator-v2.slice4-semantic-projection/5.1.0`; and
- `infinium.evaluator-v2.slice4-projection-document.schema/v5.2`.

Their narrative history remains at the exact failed-evidence commit
`2b41ad8c06f3da0f692cd963b524ff5b5d279bd0` and in the retained ADR, plan,
acceptance, and hard-stop records. No active `/5` schema, model, summary,
ledger, validator, or executable surface remains.

## `/2` and `/3` predecessor inventory

No predecessor protocol is accepted for new execution.

| Protocol | Retained tracked material | Why retained | Active execution |
|---|---|---|---|
| `/2` | `evaluator-v2-stage-a-freeze.json`, `evaluator-v2-stage-a-successor-freeze.json`, and `evaluator-v2-stage-c5-adjudication-incident.md` | Historical freeze, supersession, and invalidated-verdict chronology required to explain `/4` lineage | Prohibited; no `/2` schema remains in the tool tree |
| `/3` | `evaluator-v2-stage-a-successor-freeze.json`, `evaluator-v2-stage-a-final-bounded-freeze.json`, `evaluator-v2-successor-stage-b2-contract-gap.md`, and `m1-evaluation-baseline-evaluator-v2-amendment.md` | Historical predecessor freeze and the authority gap that led to `/4` | Prohibited; records are historical only |
| `/3` schemas | Exact Git blob identities for `assertion-results`, `calibration-results`, `candidate-semantic-output`, `corpus-execution-manifest`, `evaluator-v2-common`, `execution-manifest`, `expected-semantic-output`, `prepared-comparison-manifest`, and `sanitized-result` in `retired-evaluation-assets.v1.json` | Exact historical bytes remain recoverable without placing predecessor schemas in the active tool tree | Prohibited; `/4` code binds only `/4` identities and `.v4` schemas |
| `/4` `protocol.json` predecessor entry | One historical `/3` identity/status object inside the active frozen `/4` declaration | Explains immutable predecessor lineage | Does not activate `/3` |

The inventory is closed: there are zero current `.v2.schema.json` or
`.v3.schema.json` files. Adding a predecessor command or
advertising either predecessor as accepted/runnable would violate this usage
contract.

The default solution graph does not reference or compile `Infinium.EvaluatorV2`.
The allowlisted focused tests are linked from their unchanged tracked source
path into the out-of-solution `Infinium.Protocol4RegressionTests` project and
remain reachable only through the bounded wrapper.
