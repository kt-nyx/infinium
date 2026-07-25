# ADR-0001: Evidence authority boundary

Status: Accepted  
Date: 2026-07-24  
Accepted: 2026-07-25  
Last reviewed: 2026-07-25  
Supersedes: None  
Superseded by: None

## Context

Infinium must combine exact local state, established deterministic tools,
documentation, and LLM reasoning without allowing uncertain model output to
become the source of truth.

## Decision drivers

- Local/effective state must remain exact and independently verifiable.
- Documentation and cross-system interactions require bounded semantic
  interpretation that deterministic tools alone cannot always provide.
- Every conclusion must retain inspectable evidence and provenance.
- Provider/model changes must not redefine the deterministic core.

## Considered options

1. **Model-centric agent over raw modlist context:** rejected because model
   inference would become an untestable source of local-state truth and scale
   poorly.
2. **Deterministic analysis only:** rejected because it cannot cover the
   prose-only intent, applicability, and novel semantic investigation that
   provide much of Infinium's distinct value.
3. **Typed deterministic evidence with bounded LLM escalation:** selected.

## Decision

Deterministic systems are authoritative for local profile state, file and
record winners, binary parsing, locally observed version/runtime data,
effective configuration values, and tool results.

Applicable author-maintained and curated LOOT sources are authoritative for
their stated intent, instructions, and documented constraints. These authority
domains are not collapsed into a global ranking; applicability and
contradictions remain explicit.

The LLM is limited to cited claim extraction, identity/terminology
normalization, declared-purpose inference, grounded candidate/hypothesis
investigation, evidence synthesis, explanation, and proposed remediation or
validation. It may interpret unfamiliar configuration semantics from supplied
evidence but cannot redefine the effective values observed deterministically.

High-volume local state is indexed and reduced to evidence-backed candidates
before semantic LLM investigation. The architecture does not default to naïve
all-pairs model comparison; any bounded exception declares its population,
cost, rationale, and evaluation. RQ-035 remains responsible for selecting the
exact indexing, interaction-graph, and ranking mechanisms.

All data remains typed as observations, claims, candidates, hypotheses,
findings, recommendations, and gaps. Model output is schema validated and
retains full provenance.

## Consequences

### Positive

- Findings remain inspectable.
- Model mistakes cannot silently redefine local state.
- Provider changes do not replace the deterministic core.
- Evaluation can measure extraction and reasoning separately.

### Negative

- More explicit data modeling is required.
- Some investigations must abstain.
- Deterministic indexing must exist before broad LLM value is available.

## Requirements affected

- EVID-001 through EVID-007
- FIND-001, FIND-009, and FIND-011
- ANALYSIS-017
- AI-001 through AI-006
- SEC-001
- PROD-004

## Validation

- Planted state contradictions must be rejected.
- Authoritative intent/instruction claims must retain their scope without being
  overwritten by unrelated local observations.
- Citations and identifiers must resolve to supplied evidence.
- Severity must remain independent of confidence; readiness blocking must use
  the declared evidence and maturity policy.
- LLM removal must not change deterministic observations.
- Candidate selection must retain planted-interaction recall and selection
  reasons without defaulting to naïve all-pairs LLM comparison.

## References

- [Product requirements](../../product/requirements.md)
- [Data and trust model](../data-and-trust-model.md)
- [Evaluation strategy](../../evaluation/evaluation-strategy.md)
