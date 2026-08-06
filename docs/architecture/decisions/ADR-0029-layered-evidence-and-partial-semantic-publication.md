# ADR-0029: Retain independently proven evidence through partial semantic failure

Status: Accepted
Date: 2026-08-05
Deciders: Project owner

## Context

The M1 Slice 4.5 protocol `/4` authorability exercise exposed a recurring
contract problem. A record can be safely admitted and structurally identified
even when one member shape cannot be decoded. Existing public authority did
not say whether that later failure erased the earlier facts, invalidated the
whole record, or allowed a smaller result with an explicit gap. A fixture-by-
fixture correction loop found these omissions only after an independent
reviewer happened to exercise them.

Infinium already requires explicit provenance and coverage gaps. It now needs
a durable rule for partial success and a deterministic way to prove that every
admitted evidence state has a defined publication outcome. This decision is
product-wide. Its immediate first application is the public Slice 4.5 oracle
contract; it does not authorize private fixture access or scoring.

## Decision

### Evidence is retained in derivation layers

Within one claim derivation, use these prerequisite layers:

1. **structural** — identity, envelope, signature, order, declared shape, and
   contribution membership that can be established without decoding the
   affected value;
2. **observed** — exact occurrence, bytes, presence, or count actually
   established from the admitted source;
3. **decoded** — a typed value produced under an accepted field/shape
   contract;
4. **resolved** — a decoded reference, provider, localization, or other
   dependency connected to its authoritative target; and
5. **semantic** — applicability, meaning, classification, consequence, or
   other interpretation authorized by the preceding evidence.

These layers describe prerequisites inside a derivation path. They do not
replace the claim-type-specific authority classes in the data and trust model,
and they are not a universal ranking across unrelated claims.

A failure at a later layer does not erase an independently proven earlier
layer. The system retains and publishes the earlier fact when its own contract
is satisfied, omits any unsupported later fact, and reports the exact missing
capability and affected coverage. It must not infer the unavailable value from
the candidate, a parser under test, a neighboring field, a name, or a likely
default.

### Every published fact declares its evidence threshold

Each fact family must declare:

- the minimum evidence layer and exact prerequisites needed to publish it;
- whether absence, typed null, unknown, or omission is meaningful;
- the coverage population and completion effect;
- the gap produced when a prerequisite is unavailable; and
- whether failure makes only that fact unavailable or invalidates the whole
  result.

For every admissible state, each fact obligation has exactly one disposition:

- emit a typed value;
- emit an explicit typed null or accepted unknown state;
- omit the fact and emit/aggregate an exact coverage gap; or
- reject publication when the lower-level evidence cannot be safely separated
  or the result contract is necessarily atomic.

Graceful degradation is preferred when the retained fact remains independently
true and consumers can distinguish the missing evidence. It is not permission
to weaken a required atomic boundary, hide a failure, or claim completion.

### Contract totality precedes example-based qualification

An executable decision table must cover the Cartesian state space admitted by
the bounded contract. A totality gate must reject uncovered states, overlapping
rules, invalid dependency use, and contradictory publication/coverage/gap
outcomes. Synthetic fixtures are then generated from or mapped to that table.
They validate the contract; they are not the primary mechanism for discovering
which semantic rules exist.

Mechanical incompleteness may be corrected and rechecked until the public
totality gate passes. A newly exposed choice about intended meaning, authority,
or product behavior remains a project-owner decision and must not be guessed by
an implementer or reviewer.

### Immediate M1 RACE disposition

For an admitted `RACE` contribution whose `DATA` member is structurally
present but whose shape is not decodable under the bounded contract:

- retain the override chain, contribution identity, common structural facts,
  and required generic technical taxonomy assignments;
- create the contribution taxonomy subject, so it participates in the
  taxonomy-subject denominator and is completed when its required generic
  technical assignments are emitted;
- do not emit a `DATA` allowlisted-field occurrence/count unless the exact
  occurrence count is independently observed;
- do not emit a decoded `face_gen_head` value or a resolved race fact that
  depends on it;
- include the contribution in the `race-records` denominator but not its
  completed count; and
- emit or aggregate the exact unsupported-shape gap for `RACE/DATA` and the
  missing bounded shape semantics.

This is a category-neutral partial-decode rule. It is not a fixture identity
exception.

## Alternatives considered

- **Reject the whole record on any member failure.** Rejected because it loses
  independently true structural evidence and obscures the actual coverage
  boundary.
- **Treat every unavailable value as null.** Rejected because null, unknown,
  absent, and undecodable are different claims.
- **Continue correcting prose after individual fixtures fail.** Rejected as
  incomplete and non-deterministic; examples cannot prove a total contract.
- **Use candidate behavior to fill unspecified branches.** Rejected because
  the implementation under evaluation cannot author its own expected truth.

## Consequences

- Product, evaluator, persistence, and presentation contracts can retain useful
  partial results without overstating semantic completeness.
- Coverage arithmetic and gap projection become part of each fact's normative
  publication rule rather than an afterthought.
- Public contracts require a machine-checkable state model and totality gate
  before a private authorability claim.
- More states may be reported as partial instead of terminal, but only where
  earlier evidence is independently valid and the contract explicitly permits
  that disposition.
- Protocol `/4`, projection `3.0.0`, and the frozen evaluator are unchanged.
  This decision does not authorize protocol `/5`, private B2, C2, or product
  scoring.

## Requirements affected

`EVID-006`, `COVER-001`, `COVER-002`, `ANALYSIS-003`, `ANALYSIS-005`,
`ANALYSIS-006`, `ANALYSIS-016`, `ANALYSIS-019`, `FIND-001`, and `UX-002`.
