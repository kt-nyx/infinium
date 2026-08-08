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

The historical WP3 generation pass produced a tracked artifact covering every
admitted state, every admitted/invalid/excluded constraint,
nearest matched negatives, all family-local pairwise dimension/value
combinations, publication rules, constructors, normalization rules, atomic
boundaries, coverage populations, gaps, transitions, and the partial
`RACE/DATA` higher-order invariant. It also recomputes and hashes the complete
23,660-state classification rather than treating the compact cases as a proof
of totality by themselves. The generator was retired and has no current entry
point.

The package intentionally contains no expected semantic output, fact count,
fact hash, product ID, product output, real-mod name, private identity, or
answer-bearing oracle reference.

The fresh product-blind WP4 review passed totality, schema, adversarial,
cross-runtime, and independent authorability checks. The package remains
answer-free public evidence; the independently authored expected outputs stay
ignored. See the
[WP4 totality review attestation](../../m1-slice4-protocol-4-totality-review-attestation.md).

## Historical execution state

The independent exercise and its generated scratch outputs are complete
historical evidence. Their dedicated generator and validator were retired in
the 2026-08-08 evaluator-deferral closeout and there is no current command that
authors or validates this package. Do not reconstruct those tools or use this
directory to create a new oracle. Protocol `/4` may be exercised only through
the accepted bounded public regression wrapper named by the repository
authority inventory; that wrapper does not make this package current fixture
input or evaluator authority.

## Claim boundary

Passing this rehearsal establishes public authorability only. It does not
establish Bethesda parser correctness, product conformance, corpus
qualification, private-input eligibility, or a held-out result.
