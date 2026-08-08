# Protocol `/4` answer-free oracle-authorability rehearsal

Status: Accepted public authorability rehearsal input

Current repository classification: retained historical pre-B2 proof evidence;
not current fixture input, an executable workflow, product authority, or
evaluator authority. See
`docs/evaluation/repository-evaluation-authority.v1.json`.

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
- `generated-state-coverage.schema.json` defines the tracked, answer-free WP3
  coverage artifact.
- `generated-state-coverage.json` is deterministically derived from accepted
  totality model `1.2.0`. It contains compact state exercises and obligation
  mappings, never expected facts or product output.

The WP3 generator is
`eng/generate-m1-slice4-protocol4-state-coverage.ps1`. Its tracked artifact
covers every admitted state, every admitted/invalid/excluded constraint,
nearest matched negatives, all family-local pairwise dimension/value
combinations, publication rules, constructors, normalization rules, atomic
boundaries, coverage populations, gaps, transitions, and the partial
`RACE/DATA` higher-order invariant. It also recomputes and hashes the complete
23,660-state classification rather than treating the compact cases as a proof
of totality by themselves.

The package intentionally contains no expected semantic output, fact count,
fact hash, product ID, product output, real-mod name, private identity, or
answer-bearing oracle reference.

The fresh product-blind WP4 review passed totality, schema, adversarial,
cross-runtime, and independent authorability checks. The package remains
answer-free public evidence; the independently authored expected outputs stay
ignored. See the
[WP4 totality review attestation](../../m1-slice4-protocol-4-totality-review-attestation.md).

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

Generate and validate the model-derived coverage on either supported
PowerShell host with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/generate-m1-slice4-protocol4-state-coverage.ps1
pwsh -NoProfile -File eng/generate-m1-slice4-protocol4-state-coverage.ps1 -ValidateOnly
```

Generation is byte-stable across Windows PowerShell 5.1 and PowerShell 7.
Derived summaries and any expected semantic outputs remain under ignored
`work/` paths.

## Claim boundary

Passing this rehearsal establishes public authorability only. It does not
establish Bethesda parser correctness, product conformance, corpus
qualification, private-input eligibility, or a held-out result.
