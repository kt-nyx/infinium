# Protocol `/4` answer-free oracle-authorability rehearsal

Status: Blocked public rehearsal input; not accepted oracle authority

Purpose: prove that a fresh product-blind reviewer can construct every active
protocol `/4` fact from public authority and generic answer-free inputs.

## Contents

- `execution-manifest.json` supplies accepted plugin/provider order and
  capability declarations using only generic installed-entity IDs.
- `synthetic-byte-input.json` is the retained UTF-8 synthetic byte payload. It
  is a project-owned byte ledger, not candidate output and not a replacement
  for Bethesda binary conformance fixtures.
- `zero-denominator-execution-manifest.json` and
  `zero-denominator-byte-input.json` form a second answer-free run in which
  all ten fixed coverage populations are retained with zero denominators.
- `coverage-ledger.json` maps every specification rule to an input or mutation
  exercise without embedding expected facts.

The package intentionally contains no expected semantic output, fact count,
fact hash, product ID, product output, real-mod name, private identity, or
answer-bearing oracle reference.

The independent re-review stopped on a second material public-authority gap.
The package is retained as generic evidence for owner disposition, not as a
passing rehearsal. See the
[public review attestation](../../m1-slice4-protocol-4-oracle-authorability-review.md).

## Independent exercise

A fresh reviewer reads the normative specification, this directory, and the
frozen evaluator `/4` public mechanics. The reviewer constructs a complete
`expected-semantic-output.v4.json` under an ignored `work/` directory and runs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-m1-slice4-protocol4-authorability.ps1 `
  -PackageRoot docs/evaluation/fixtures/protocol-4-oracle-authorability `
  -ExpectedOutput work/m1-slice4-protocol4-authorability-review/expected-semantic-output.v4.json `
  -ZeroDenominatorExpectedOutput work/m1-slice4-protocol4-authorability-review/zero-denominator-expected-semantic-output.v4.json
```

The validator checks answer isolation, family/rule coverage, fact typing,
ordering, uniqueness, fixed coverage rows, closed vocabularies, forbidden
product-ID tokens, and generic invalid mutations. It does not supply semantic
answers or compare with product behavior.

The output remains scratch evidence. The tracked review attestation records
the exact input/specification hashes, method, commands, coverage, findings,
corrections, and answer-isolation state without publishing the constructed
answer set as future oracle authority.

## Claim boundary

Passing this rehearsal establishes public authorability only. It does not
establish Bethesda parser correctness, product conformance, corpus
qualification, private-input eligibility, or a held-out result.
