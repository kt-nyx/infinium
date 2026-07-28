# ADR-0013: OpenAI-first LLM capability boundary

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Subsequent clarification

On 2026-07-28, the owner reaffirmed direct, schema-constrained Responses API
calls as the initial core LLM path and rejected the Codex/ChatGPT-plan
alternative recorded in ADR-0024. “Responses API” in decision 2 means an
ordinary direct application integration, not Responses routed through Codex.
Initial authenticated calls use a user-supplied, usage-priced OpenAI Platform
API key under the credential and cost controls subsequently accepted by
ADR-0020 and ADR-0023.

## Context

The owner has selected OpenAI as the only required initial LLM provider and
has rejected provider portability as a reason to omit otherwise useful
capabilities. RESEARCH-0032 found that the earlier two-operation
provider-neutral contract remains a valuable semantic safety subset but should
not cap the initial product at the lowest common denominator across providers.

## Decision drivers

- OpenAI hosted web search directly supports discovery of obscure documented
  mod interactions.
- Provider transport must not become authoritative domain truth.
- Local-only and deterministic analysis must remain provider-free.
- LLM work must remain evidence-bound, inspectable, and cost-controlled.
- Later providers should be possible without gating M1 or pretending feature
  parity.

## Decision

1. OpenAI is the only required initial LLM provider. A second provider and
   portability conformance do not gate M1.
2. The initial OpenAI generation surface is the Responses API.
3. Provider-independent domain objects retain source, evidence, observation,
   claim, candidate, hypothesis, finding, recommendation, case, coverage, and
   readiness truth. OpenAI Response items remain invocation provenance.
4. The initial semantic operations preserve RESEARCH-0027's schema-bound:
   - source-claim extraction; and
   - evidence-bound candidate investigation.
5. OpenAI-specific operations may add:
   - governed source discovery through hosted `web_search`;
   - bounded research of an admitted web lead; and
   - explanation of already admitted evidence/findings.
6. Web-search output is discovery provenance and an investigative lead. It
   gains ordinary external-source authority only after an approved
   host-controlled adapter acquires and fingerprints the landing source and
   extracts exact passages.
7. Hosted `web_search` is the only accepted model-selected tool for M1. Shell,
   filesystem, computer use, remote MCP, generic HTTP fetch, Infinium internal
   functions, MO2/LOOT/game/process access, credentials, and write-capable
   actions remain unavailable to the model.
8. Stateless synchronous Responses with `store=false` are the initial
   default. Background Responses, Batch, and prompt caching each require
   separate capability, retention, cancellation, cost, and evaluation
   qualification before enablement.
9. Every invocation retains requested and returned model identity, capability
   snapshot, exact request/response and typed items, validation/admission
   results, tool actions/sources, usage, cost, storage/retention declaration,
   and failure/cancellation state.
10. A future provider may implement a different declared capability profile.
    It need not emulate OpenAI-specific features, and Infinium shall not
    silently substitute a weaker provider or rewrite one provider's objects as
    another's.

## Explicit non-decisions

This ADR does not select:

- a production model;
- credential entry or storage;
- hard-budget reservation/reconciliation;
- SDK or language binding;
- desktop stack, process topology, IPC, or database;
- a landing-page source adapter;
- arbitrary model tools; or
- a provider-funded/shared credential.

Those remain later architecture, security, and conformance decisions.

## Consequences

### Positive

- The product can use OpenAI capabilities that materially improve discovery
  and long-running analysis.
- Evidence and readiness remain independent of provider transport.
- Later provider support can be capability-aware rather than
  lowest-common-denominator.

### Negative

- The UI and run model must expose provider-specific capability differences.
- Hosted search, background, Batch, and caching each add distinct retention,
  cancellation, cost, and replay semantics.
- Exact model and account behavior remain volatile and require revalidation.

## Validation

Before any capability is supported:

- Structured Outputs must pass schema, refusal, incomplete, citation,
  identifier, and semantic-admission tests;
- hosted search must preserve complete discovery provenance and must not grant
  source or operation authority;
- hostile content must not obtain tools, secrets, or local-state authority;
- exact selected-model, storage, usage, cost, cancellation, and rate behavior
  must be qualified; and
- the applicable EVAL-0010 through EVAL-0012, EVAL-0033, EVAL-0034,
  EVAL-0064, EVAL-0067, EVAL-0068, EVAL-0076, EVAL-0077, EVAL-0081, and
  EVAL-0083 cases must be specified and passed at the required milestone.

## Requirements affected

- AI-001 through AI-007
- DOC-002, DOC-008 through DOC-011
- EVID-002 through EVID-007
- ANALYSIS-017
- SEC-001 through SEC-004
- OPS-001 through OPS-003

## References

- [ADR-0001](ADR-0001-evidence-authority-boundary.md)
- [ADR-0002](ADR-0002-snapshot-context-binding.md)
- [RESEARCH-0027](../../research/investigations/RESEARCH-0027-provider-neutral-llm-contract.md)
- [RESEARCH-0028](../../research/investigations/RESEARCH-0028-provider-capability-and-authentication.md)
- [RESEARCH-0032](../../research/investigations/RESEARCH-0032-openai-first-llm-and-web-search.md)
- [RESEARCH-0033](../../research/investigations/RESEARCH-0033-wave-d-revision-integration.md)
