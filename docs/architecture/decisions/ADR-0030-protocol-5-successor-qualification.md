# ADR-0030: Authorize a separately qualified protocol `/5` successor

Status: Accepted historical decision; active `/5` authorization superseded by ADR-0032
Date: 2026-08-07
Accepted: 2026-08-07
Accepted by: Project owner
Last reviewed: 2026-08-10
Supersedes: ADR-0027 decision 15 only
Superseded by: ADR-0031 only for the `/5` semantic-model identity; ADR-0032 for all active `/5` authorization

## Context

The accepted evidence contract
`infinium.m1-slice4.protocol-4-evidence-contract/1.2.0` proved a public
representation gap in frozen protocol `/4`. The admitted partial `RACE/DATA`
state must retain independently proven common race-contribution facts while
omitting the unavailable later-layer `face_gen_head` fact. Protocol `/4`
cannot express that exact fact set: retaining the object necessarily emits the
boolean, while omitting the object loses its common facts.

ADR-0027 decision 15 made `/4` the final M1 revision and directed a remaining
gap to the owner. The owner has now supplied that disposition. The historical
`/4` evaluator, its candidate classification, and all earlier protocol and
evaluation records remain immutable evidence.

## Decision drivers

- Restore representability without weakening the accepted semantic model.
- Preserve answer isolation and the one-shot meaning of private scoring.
- Keep evaluator maintenance separate from candidate implementation, corpus
  qualification, oracle authoring, and scoring.
- Prove total representability before implementation or private use.
- Preserve exact identities and append-only historical evidence.

## Considered options

### Leave the representation gap unresolved for M1

Rejected by the project owner for this bounded maintenance cycle. It would
leave an accepted semantic outcome inexpressible by the public evaluator.

### Repair or retry protocol `/4`

Rejected. Frozen `/4` is immutable historical evidence. Changing its code,
projection, manifests, or freeze identity would blur the prior qualification
and would not be a successor qualification.

### Qualify protocol `/5` from the accepted public model

Selected. `/5` is created under a new protocol, projection, implementation,
manifest, review, and freeze identity. It is derived from accepted public
semantic authority, never from candidate output or private answers.

## Decision

1. Protocol `infinium.evaluator-v2/5` is authorized as a separately qualified
   M1 successor evaluator.
2. This decision supersedes only ADR-0027 decision 15's assertion that `/4`
   is the final M1 revision and its prohibition on creating `/5`. Every other
   ADR-0027 rule remains authoritative.
3. The accepted model
   `infinium.m1-slice4.protocol-4-evidence-contract/1.2.0`, SHA-256
   `09ae312824aa0c859b396fd18fef69b14905c2c6d6f901ce598d3c5ab5970bf5`,
   is immutable semantic authority. `/5` changes projection representation;
   it does not change accepted facts, coverage, gaps, state classes, or
   layered-evidence semantics.
4. Protocol `/4` at
   `3693d19563c636cd2879804633ca4ce52448d2c1`, its historical candidates, and
   protocols `/2` and `/3` remain immutable. No repair, retry, amend, rewrite,
   squash, rebase, or replacement of their records is authorized.
5. `/5` qualification is public and product-blind. Candidate output, product
   behavior, private inputs, private answers, predecessor oracles, and hidden
   results cannot author or validate the representation contract.
6. Before implementation, a machine-checkable representation contract must
   prove that every accepted semantic outcome has at least one schema-valid
   `/5` document that canonicalizes to exactly that outcome, with no missing
   required fact and no extra fact.
7. Public implementation and freeze, product realignment, private corpus
   eligibility, private oracle/corpus qualification, and C2 scoring are
   separate fresh roles. Completing `/5` does not authorize any later role.
8. Private-fixture default denial, candidate/evaluator isolation, answer
   isolation, no-retry/no-repair scoring, exact identity binding, immutable
   freeze records, and the prohibition on using product output as truth remain
   unchanged.
9. No protocol `/6` or recursive successor is authorized. A new semantic or
   authority choice, private/candidate contamination, or inability to prove
   total representation returns to the owner.

## Consequences

### Positive

- The accepted partial-publication semantics can receive a complete public
  representation rather than being weakened to fit `/4`.
- `/4` remains auditable historical evidence with an unambiguous identity.
- Later product and private roles receive a frozen, independently reviewed
  contract instead of implementation-derived truth.

### Negative

- A complete public schema, canonicalizer, validation, calibration, manifest,
  and freeze cycle is required before any downstream work.
- The existing candidate is not thereby conforming and cannot be executed or
  corrected within this evaluator-maintenance task.

### Risks and mitigations

- **Successor becomes an evaluator retry:** use a new identity and freeze and
  prohibit private execution or verdict reuse in this cycle.
- **Projection changes semantics:** mechanically compare every canonical fact
  set against immutable model `1.2.0` and stop on any semantic choice.
- **Implementation learns candidate/private behavior:** use positive public
  allowlists and a fresh product-blind acceptance role.
- **A fixture-specific patch is introduced:** require model-derived exhaustive
  and adversarial validation across every fact family and state class.

## Requirements affected

- EVID-001 through EVID-007
- COVER-001 and COVER-002
- ANALYSIS-003, ANALYSIS-005, ANALYSIS-006, ANALYSIS-016, and ANALYSIS-019
- EVAL-0052 and EVAL-0086
- SEC-001, SEC-003, and SEC-004

## Validation

- Execute work `M1/S4.5/PRE-B2/V5/WP0` through `WP4` under the accepted
  successor plan.
- Prove model-to-document construction, schema acceptance, exact
  canonicalization, mutation behavior, coverage/gap arithmetic, and complete
  mutually exclusive state coverage.
- Run a fresh product-blind public acceptance review at the exact WP3 commit.
- Freeze only after the reviewer accepts the exact implementation and public
  artifacts without an isolation breach.

## References

- [ADR-0027](ADR-0027-public-evaluation-protocol-private-held-out-corpus.md)
- [ADR-0029](ADR-0029-layered-evidence-and-partial-semantic-publication.md)
- [Accepted protocol `/4` evidence contract](../../evaluation/evaluator-history.md)
- [WP5 frozen-candidate classification](../../evaluation/evaluator-history.md)
- [Protocol `/5` successor plan](../../evaluation/evaluator-history.md)

## Subsequent disposition

[ADR-0032](ADR-0032-defer-m1-held-out-evaluator-and-continue-public-verification.md)
retired protocol `/5` unqualified before implementation or freeze.
This record remains the historical authority for why that identity was
authorized and consumed, but none of its active qualification or downstream
authorization clauses remain executable. Its isolation, immutability,
provenance, and no-retry boundaries survive through ADR-0032.
