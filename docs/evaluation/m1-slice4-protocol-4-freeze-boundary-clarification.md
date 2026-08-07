# M1 Slice 4 protocol `/4` freeze-boundary clarification

Status: Accepted

Date: 2026-08-07

Work ID: `M1/S4.5/EVAL-CLOSEOUT/WP2/T1`

Authority: Project owner

## Prior stop

The first WP2 preflight stopped before edits because three paths in
`required_public_files` no longer had their historical byte identities in the
current checkout. That was the correct literal application of the governing
plan at the time. It consumed no Implementer A correction pass and performed
no evaluator, candidate, private, corpus, adaptation, or scoring execution.

The plan had conflated immutable historical freeze provenance with the current
checkout of public tests that may evolve through separately authorized product
work. This record resolves that boundary; it does not repair `/4`, rewrite its
manifest, change product semantics, continue `/5`, or issue a verdict.

## Verified historical freeze integrity

The immutable freeze manifest is
`docs/evaluation/evaluator-v2-stage-a-final-bounded-freeze.json`, 6,972 bytes,
SHA-256
`2e30980f9e8628bf88c519e12c510c86a9c3ff2f6a7374b796fd8e6b769907d6`.

Every manifest path was read as a raw Git blob at exact evaluator commit
`3693d19563c636cd2879804633ca4ce52448d2c1`. All 23 of 23 blob lengths and
SHA-256 values match the immutable manifest. The historical freeze is intact.

## Verified current reusable core

Mechanical classification treats the three `tests/` paths as public test
evidence and the other 20 manifest paths as evaluator runtime/schema/core
files. All 20 of 20 current non-test files match their frozen byte length and
SHA-256. No current evaluator runtime source, protocol declaration, schema,
canonicalizer, scorer, adapter, calibration implementation, or other non-test
manifest file has drifted.

Any later mismatch or missing file in this 20-file set is a hard stop for a
claim that the current checkout contains the reusable frozen `/4` core.

## Evolved current public regression tests

Only these current files differ from their historical blobs:

| Path | Current bytes | Current SHA-256 |
|---|---:|---|
| `tests/Infinium.EvaluationTests/BethesdaOracleAgreementEvaluationTests.cs` | 30,972 | `f02b67afaad9a22d893a0b819fa33175c6a2d256db8d8255c45241c5b51a4a51` |
| `tests/Infinium.EvaluationTests/BethesdaSemanticExtractionEvaluationTests.cs` | 20,267 | `73e0ee3c3f4c617982e7d0e5de0d596feee4d958a951d8ef7fc9418b8084991a` |
| `tests/Infinium.EvaluationTests/EvaluatorV2PublicProtocolTests.cs` | 34,302 | `c7c99bcf234ad3a72a1e04a52fa9835fb3d1f912c95b0b0aaf80a22bdcb5b01f` |

Git attributes all three changes exclusively to authorized public
product-realignment commit
`a98d648bd0adb2751ee0c09828e0227b1583950f`: 150 insertions and 96 deletions
across the three paths. The current files are public regression evidence. The
original qualification-test blobs remain reproducible at the frozen commit.

## Permitted claims

- all 23 historical frozen blobs match the immutable manifest;
- all 20 current non-test reusable-core files match their frozen hashes;
- current allowlisted public regression checks may report their own health at
  their recorded current commit and file identities; and
- the frozen `/4` runtime remains usable for bounded public regression over
  states it represents exactly.

## Prohibited claims

- the evolved current tests are the original frozen qualification suite;
- `/4` represents the complete current accepted Slice 4 semantic contract;
- bounded regression success is a private held-out verdict, Slice 4.5 `PASS`,
  or an overall current-product verdict;
- `/4` may reject or normalize away the accepted partial `RACE/DATA` behavior;
  or
- product output, private material, or the retired `/5` artifacts author truth.

No freeze JSON, frozen `/4` byte, current product-realignment test, private
material, candidate, or historical record was changed to obtain this result.
