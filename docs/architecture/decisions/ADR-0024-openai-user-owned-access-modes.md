# ADR-0024: OpenAI user-owned access modes

Status: Rejected  
Date: 2026-07-28  
Accepted: No — rejected by owner on 2026-07-28  
Last reviewed: 2026-07-28  
Supersedes: None  
Amends if accepted: ADR-0013 decision 2 and its synchronous-Responses-only
initial-surface provisions  
Superseded by: None

## Owner disposition

The owner rejected this proposal after reviewing the actual execution
boundary. Infinium's initial LLM pipeline shall use direct, schema-constrained
OpenAI Responses API calls under accepted ADR-0013. It shall not use Codex,
Codex app-server, or ChatGPT-plan access as its core model adapter.

Codex app-server can return schema-bound output, but it remains a coding-agent
runtime with its own thread, turn, tool, configuration, and event semantics.
Those additional semantics do not improve Infinium's deliberately bounded
model-in/model-out operations and would create a second orchestration and
security surface. The potential reduction in user API cost does not justify
changing the product's deterministic host-controlled pipeline.

Direct Responses access uses a user-supplied OpenAI Platform API key and is
usage-priced independently of ChatGPT subscriptions. The product must state
that clearly, estimate and limit cost before dispatch, and keep all
deterministic/offline features usable without an LLM credential. This
rejection does not claim that a future supported subscription-backed general
API surface could never be considered; such a surface would require new
research and a new ADR.

The remainder of this ADR is retained as the rejected proposal and its
rationale. It is not an implementation target.

## Context

ADR-0013 accepted OpenAI as the only required initial LLM provider and selected
the Responses API as its initial generation surface. That decision preceded
the owner-requested distinction between ChatGPT subscription usage and
usage-priced API access.

Current OpenAI documentation establishes that ChatGPT sign-in provides
subscription access through Codex, while Platform API keys remain
usage-priced and are required for general OpenAI API calls. Codex app-server
is explicitly intended for deep integration in other products and exposes
managed ChatGPT login, account/plan/rate-limit reads, models, streamed turns,
and per-turn output schemas. It is therefore a plausible second OpenAI
execution surface, but not an alternate credential for the Responses API.

RESEARCH-0045 recommends preserving both user-owned modes with separate
capability, billing, security, and provenance contracts.

## Decision drivers

- Users with eligible ChatGPT plans should not be forced into unexpected API
  charges when a qualified plan-backed path exists.
- Users must retain an explicit direct Platform API-key option with estimates
  and hard cost limits.
- Authentication mode, execution surface, billing authority, quota
  visibility, and hard-limit enforceability must not be conflated.
- Codex is coding-focused and tool-capable, so plan-backed support requires a
  stricter prototype and allowlist than documentation alone can prove.
- Deterministic/local operation and provider-independent domain truth must
  remain unchanged.

## Considered options

### Direct Responses API with either ChatGPT login or an API key

Rejected. ChatGPT login is not documented as general Responses API
authentication. Platform keys remain the supported credential for general API
calls.

### Direct Responses API and API keys only

Retained as a required supported mode but rejected as the product ceiling. It
would make every LLM operation usage-priced and ignore an officially supported
Codex integration path under eligible ChatGPT plans.

### Route every mode through Codex app-server

Rejected. The direct Responses adapter provides tighter API-specific control
and accounting for usage-priced work. A single provider does not require one
execution surface.

### Distinct direct-API and Codex-plan adapters

Selected, subject to the app-server qualification gate.

## Decision

1. Infinium shall model an OpenAI **access profile** as the combination of
   execution surface, authentication mode, account/workspace or Platform
   billing scope, model/capability snapshot, retention behavior, and
   consumptive-limit contract.
2. The intended initial OpenAI access profiles are:
   - **ChatGPT plan through Codex app-server**, using app-server-managed
     browser or device-code login; and
   - **OpenAI Platform API through direct Responses**, using a user-supplied
     Platform API key.
3. ChatGPT plan mode is a qualification-gated prominent onboarding choice,
   not an accepted implementation claim. Platform API mode remains available
   independently. Deterministic/local features require neither.
4. Infinium shall not use ChatGPT credentials for direct general API calls,
   accept raw ChatGPT session/access tokens from the user, implement an
   unofficial ChatGPT login flow, or silently reuse the user's unrelated Codex
   configuration/session.
5. The coordinator shall launch a pinned Codex app-server over inherited stdio
   under a dedicated product-controlled state/config root. App-server owns
   login, credential refresh, and logout. Infinium receives only the
   authorization URL/device ceremony and non-secret account, capability,
   limit, usage, status, and turn data required by the application.
6. The Codex profile shall load only product-owned inert operation staging and
   host-owned configuration. It shall not load arbitrary user/project
   instructions, rules, skills, plugins, MCP servers, memories, hooks, or
   provider definitions.
7. Codex plan-backed semantic operations shall use one bounded thread/turn per
   Infinium operation, an exact output schema, read-only or stricter sandbox
   posture, and no shell, write, subprocess, computer-use, application,
   connector, arbitrary-network, credential, MO2, LOOT, game, or Infinium
   operation authority. Hosted search is unavailable until separately
   qualified against ADR-0013's discovery-only contract.
8. Platform API mode shall continue to use the direct Responses adapter and
   the API-key generation/helper boundary selected by the applicable
   credential ADR. Supplying an API key shall never cause Infinium to
   represent usage as ChatGPT-plan activity.
9. Every operation/run shall immutably bind its access profile, resolved model
   and capability identity, request/turn schema, retention declaration,
   account/billing-scope identity where safely available, limit semantics, and
   provider invocation provenance.
10. Infinium shall never automatically fall back, retry, or continue from
    ChatGPT plan mode to Platform API mode or the reverse. A mode change
    requires a new explicit user authorization and, where immutable work has
    started, a new operation/run with validated reuse.
11. ChatGPT plan UI shall show provider-reported plan, rate windows, reset
    times, credits, and token activity only where returned. It shall show
    planned/estimated turn and work shape but shall not display API dollar
    pricing, call the mode free, or claim that current headroom guarantees
    completion.
12. Platform API UI shall prominently label usage billing and show
    model/tier, estimated token/tool usage, catalog-calculated cost, configured
    hard limits, and the distinction between local calculation and final
    provider billing. Starting a bounded scan/operation requires explicit
    confirmation under the product workflow.
13. The cost ledger shall keep access-mode-specific dimensions. API work may
    reserve call/token/tool/catalog-money bounds under ADR-0023. Plan-backed
    work shall reserve its qualified local turn/concurrency/deadline bounds,
    retain observed token/limit/credit state, and expose provider-quota
    reservation or predictability as unavailable where app-server does not
    supply a finite enforceable contract.
14. A pinned Windows app-server build, generated protocol schema, dedicated
    credential/config isolation, tool-denial posture, structured-output
    behavior, provenance, rate-limit handling, packaging, and matched semantic
    quality shall pass the named M1 qualification before ChatGPT plan mode is
    represented as supported.

## Consequences

### Positive

- Eligible users can target included ChatGPT/Codex usage instead of being
  forced into API billing.
- API users retain fine-grained price estimates and hard local dollar limits.
- The product describes plan quota and API charges honestly.
- A failure in one access mode does not remove local analysis or authorize a
  costly fallback.

### Negative

- Two OpenAI execution adapters require separate capability, prompt,
  provenance, cancellation, retention, and evaluation work.
- Codex app-server adds a pinned local executable and update/protocol surface.
- ChatGPT plan consumption cannot currently be converted into a reliable
  per-operation dollar estimate or atomically reserved provider quota.
- Coding-focused model behavior may fail the mod-analysis qualification.

### Risks and mitigations

- **A plan-backed run unexpectedly invokes tools:** use a dedicated
  host-controlled profile, explicit denials, inert staging, event validation,
  and adversarial tests; reject the mode if the stable surface cannot close
  the boundary.
- **The product implies a scan will fit remaining plan usage:** display exact
  provider fields and calibrated estimates separately; never treat used
  percentage as a reservation.
- **Credentials leak through app config/history:** require app-server-owned
  OS credential storage, dedicated state, no raw token IPC, canary tests, and
  explicit logout/deletion/recovery behavior.
- **The two adapters drift semantically:** run shared schema/admission cases
  and record access-mode-specific capability/version provenance rather than
  forcing claimed parity.
- **A bundled app-server becomes stale:** pin it, generate matching schemas,
  test updates, retain rollback, and include it in dependency/notices review.

## Explicit non-decisions

This ADR does not:

- accept a production app-server version or prove ChatGPT plan conformance;
- select a production model or preset;
- accept Codex hosted search;
- promise a particular plan's message count, credits, or availability;
- select the final app-server packaging/update mechanism;
- enable background, Batch, cache-dependent, or concurrent billable work; or
- add a non-OpenAI provider.

## Requirements affected

- AI-001 through AI-007
- SCAN-001 through SCAN-004
- EVID-002 through EVID-007
- DOC-009 through DOC-011
- SEC-001 through SEC-004
- OPS-001 through OPS-004

## Validation

No evaluation is passed by accepting this ADR.

Before ChatGPT plan mode is supported, an accepted M1 plan shall:

- pin the app-server binary and generated schema;
- prove dedicated state plus browser/device login, refresh, logout, restart,
  and OS-backed credential isolation without token leakage;
- prove the absence of every unapproved configuration and tool authority;
- run matched generic extraction/investigation cases against the direct
  Responses path and the same semantic admission contract;
- exercise valid, malformed, refusal, incomplete, cancelled, interrupted,
  rate-limited, exhausted, restarted, and incompatible-version paths;
- prove access-mode-specific usage/cost displays and no cross-mode fallback;
  and
- extend EVAL-0010 through EVAL-0012, EVAL-0033 through EVAL-0035,
  EVAL-0044, EVAL-0064, EVAL-0076 through EVAL-0077, EVAL-0081 through
  EVAL-0083, and EVAL-0089 as applicable.

## References

- [ADR-0013](ADR-0013-openai-first-llm-capability-boundary.md)
- [ADR-0020](ADR-0020-credential-storage-and-provider-dispatch.md)
- [ADR-0023](ADR-0023-atomic-cost-ledger-and-hard-budget-enforcement.md)
- [RESEARCH-0045](../../research/investigations/RESEARCH-0045-openai-user-access-modes.md)
- OpenAI [Codex authentication](https://learn.chatgpt.com/docs/auth),
  retrieved 2026-07-28
- OpenAI [Codex app-server](https://learn.chatgpt.com/docs/app-server),
  retrieved 2026-07-28
- OpenAI [Codex pricing](https://learn.chatgpt.com/docs/pricing), retrieved
  2026-07-28
- OpenAI [`openai/codex`](https://github.com/openai/codex), retrieved
  2026-07-28
