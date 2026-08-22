# ADR-0034: Bind transmitted prompt bytes and separate semantic admission axes

Status: Accepted
Date: 2026-08-22
Accepted: 2026-08-22
Last reviewed: 2026-08-22
Supersedes: None
Superseded by: None

## Context

Slice 6 retained a prompt identity and fingerprint for its source-claim and
candidate-investigation operations, but the live request serialized a shorter
developer instruction. The retained response bytes are valid historical
evidence, but the recorded prompt provenance did not describe the bytes the
provider actually received.

The same review found that one semantic state was doing too many jobs. It
could mean what the provider proposed, whether a passage faithfully stated a
claim, whether evidence supported the proposition, whether the proposition
applied in the local context, or what the host decided to admit. This made a
faithfully extracted source statement look like downstream proof and made
`unsupported`, `contradicted`, and `abstained` overlap.

## Decision drivers

- Prompt provenance must describe the exact request bytes sent to a provider.
- Source-authored meaning must remain distinct from local evidence and local
  applicability under ADR-0001 and the accepted domain model.
- A well-formed model proposal cannot grant its own admission.
- Useful narrow source claims should survive when downstream support or
  applicability is unresolved.
- Historical live responses, ledger events, accounting, and frozen public
  evidence must remain byte-for-byte unchanged.

## Considered options

### Keep one semantic state and refine its labels

This minimizes contract change, but a single value still cannot say both
"faithfully extracted" and "not yet evaluated for local support." Every new
label would encode several independent facts and recreate ambiguous state
combinations.

### Treat every faithful extraction as admitted downstream support

This preserves more provider output, but it changes source testimony into
local truth. It would allow accurate source extraction to overstate what the
local evidence establishes.

### Separate proposal, support, applicability, and host decision

This requires a clean-break contract and persistence revision, but each value
has one meaning and invalid combinations can be rejected mechanically.

## Decision

Infinium selects separate axes and exact prompt-byte provenance.

1. The recorded prompt instruction is the exact UTF-8 developer-instruction
   text serialized into the canonical provider request. Its SHA-256
   fingerprint is computed from those exact bytes. Materialization fails
   before dispatch if the prompt ID, recorded text, fingerprint, or parsed
   transmitted text disagree.
2. Provider semantic output records four independent facts:
   - **proposal/extraction state** records what the provider proposed and, for
     source claims, whether the host could faithfully extract the cited
     source statement;
   - **support state** records whether the bounded evidence was evaluated as
     supporting, insufficient, directly opposing, unavailable, or not yet
     evaluated;
   - **applicability state** records whether the proposition applies in the
     bounded local context, is conditional without established conditions,
     does not apply, is unknown, or was not evaluated; and
   - **decision state** records the host action: admitted, rejected, abstained,
     or retained for audit only.
3. `unsupported` means that available evidence is insufficient to support the
   attempted proposition. It does not mean the proposition is false.
4. `contradicted` means that available evidence directly opposes the attempted
   proposition. The opposing evidence remains visible.
5. `abstained` is a host decision to assert no conclusion because support,
   applicability, availability, or conflict is unresolved. A provider's
   explicit "cannot determine" output is separately retained as a proposal
   abstention with support `not-evaluated`; it is not relabeled unsupported.
6. Only `supported` plus `applicable` may be `admitted`. Structural or policy
   failures are `rejected`. Deleted material may be `audit-only` but cannot
   support a current conclusion. Every retained proposal has exactly one host
   decision link.
7. Faithful source extraction may be retained while the host decision
   abstains. Consuming analysis runs must perform their own support and
   applicability decision rather than inheriting local truth from extraction.
8. The active product contracts use the separated shape without accepting the
   former single-state JSON shape. Schema 9 adds the three decision columns
   while preserving the legacy database column and rows unchanged for audit.
   Frozen public packages are compared through explicit read-only historical
   projections; they are not rewritten and those projections are not product
   input compatibility.
9. The completed Slice 6 live requests, responses, evidence, ledger, cost, and
   accounting remain historical facts. This decision authorizes no credential
   access, provider retry, or reinterpretation of those bytes.

## Consequences

### Positive

- Prompt provenance now proves what the provider actually received.
- A source can be quoted faithfully without silently proving a later local
  hypothesis.
- Unsupported, contradicted, and abstained have stable, non-overlapping
  meanings.
- Persistence and replay retain the full reasoning path rather than a lossy
  combined label.

### Negative

- JSON, protobuf, persistence, replay, validation, and fixture tooling require
  a coordinated clean-break update.
- Historical public oracles need a narrow projection layer to compare their
  frozen combined-state vocabulary with the active contract.

### Risks and mitigations

- **Invalid state combinations:** contract invariants require one decision per
  proposal and admit only supported, applicable propositions.
- **Historical evidence drift:** old package bytes remain immutable and their
  readers validate both current canonical integrity and the frozen projection.
- **Answer-derived expected truth:** a new answer-isolated public semantic
  matrix defines the axes and negative-state meanings independently of live
  response content.

## Requirements affected

- EVID-001 through EVID-004 and EVID-007
- ANALYSIS-016 and ANALYSIS-019
- OPS-002 and OPS-003
- RQ-031

## Validation

- Parse canonical provider requests and compare the transmitted developer text
  byte-for-byte with the recorded prompt text and SHA-256 fingerprint.
- Round-trip the clean-break JSON/protobuf contracts and schema-9 persistence.
- Exercise proposal, support, applicability, and decision combinations,
  including unsupported, contradicted, explicit abstention, unavailable, and
  deleted cases.
- Compare the product with the answer-isolated public semantic-admission
  authority and re-run the unchanged historical public packages through their
  read-only projections.
- Run the complete M1 continuation verification floor without provider,
  credential, private-fixture, or archive access.

## References

- [ADR-0001](ADR-0001-evidence-authority-boundary.md)
- [ADR-0013](ADR-0013-openai-first-llm-capability-boundary.md)
- [ADR-0025](ADR-0025-m1-openai-model-and-synchronous-responses-profile.md)
- [Domain model](../../product/domain-model.md)
- [Data and trust model](../data-and-trust-model.md)
- [Slice 6 plan](../../plans/milestones/m1/slices/s6/plan.md)
