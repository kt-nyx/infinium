# RESEARCH-0045: OpenAI user access modes and billing semantics

Status: Completed

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary RQ: RQ-037

Related RQs: RQ-012, RQ-018, RQ-034

M0 wave: E — Architecture and security synthesis amendment

Decision enabled: OpenAI access-mode ADR and amendments to the then-proposed
credential and cost-ledger ADRs

Acceptance: Recommendation rejected by owner on 2026-07-28

## Owner disposition

The investigation established that ChatGPT subscription access and ordinary
OpenAI Platform API access are different products and billing surfaces. The
owner subsequently rejected the recommended dual-adapter design.

Infinium will retain accepted ADR-0013's direct Responses API approach for its
initial LLM pipeline. Core semantic operations will be ordinary
schema-constrained Responses calls using a user-supplied, usage-priced
Platform API key. Codex and Codex app-server will not be used as the core
adapter because their agent-runtime, thread, tool, configuration, and event
semantics add a second orchestration/security surface without improving the
host-controlled call-and-response contract Infinium needs.

The research below is preserved as evidence for why ChatGPT-plan usage cannot
simply authenticate direct Responses calls and as provenance for rejected
ADR-0024. Its dual-mode recommendation is historical, not current direction.
Any future subscription-backed access option requires a supported general
application API surface, fresh research, and a new ADR.

## Executive answer

The owner's premise is correct for **Codex**, but not for ordinary OpenAI API
calls:

- ChatGPT sign-in gives Codex subscription/plan-backed access;
- a Platform API key gives usage-billed access at API rates; and
- ChatGPT credentials are not a general replacement for Platform API
  authentication on the Responses API.

Infinium can nevertheless plausibly offer both user-owned choices:

1. **ChatGPT plan mode** through a pinned local OpenAI Codex app-server
   adapter. Codex app-server is explicitly documented for deep integration
   into another product. It owns browser/device-code ChatGPT login, refreshes
   credentials, exposes account/plan and rate-limit/usage information, accepts
   per-turn JSON output schemas, and streams agent events.
2. **OpenAI Platform API mode** through Infinium's direct Responses adapter
   using a user-supplied API key. This remains the usage-priced mode and is the
   mode for which per-request token bounds, price-catalog estimates, and local
   dollar hard limits are most defensible.

These are separate execution adapters, not two credentials applied to one
identical transport. They can differ in available models, agent behavior,
tools, retention, rate limits, usage reporting, hard-limit enforceability, and
output details. Every run must bind the selected access mode and capability
snapshot. Infinium must never silently fall back from one to the other.

ChatGPT plan mode should be the prominent onboarding choice when its
qualification gate passes because it avoids surprising API charges for users
who already have an eligible plan. It must not be labeled “free” or assigned a
fictional dollar estimate: it consumes Codex plan limits and possibly ChatGPT
credits. The UI should show the exact plan, available rate-limit windows,
reset times, credits, and token-activity fields that app-server reports, while
clearly marking unavailable or non-predictable fields.

This is a viable target, not an implementation proof. Codex is documented as
a coding-focused agent. Before Infinium supports ChatGPT plan mode, a pinned
Windows app-server prototype must prove that the two schema-bound Infinium
semantic operations achieve acceptable quality, remain read-only and
tool-bounded, preserve complete provenance, and satisfy the same admission
tests as the direct Responses path. Failure of that gate leaves Platform API
mode available and makes ChatGPT plan mode an explicit unsupported
capability—not a hidden fallback.

## 1. Question and constraints

RQ-037 asks:

> Which OpenAI user-owned access modes can Infinium support—direct Platform
> API usage, ChatGPT/Codex subscription access, or both—and how must their
> authentication, execution, billing, usage visibility, security, provenance,
> and hard-limit capabilities differ?

The answer must preserve:

- accepted ADR-0013's provider-independent domain/evidence truth and
  schema-bound semantic operations;
- `AI-004`'s conservative pre-dispatch admission and honest capability gaps;
- `AI-005`'s separation of usage, rate limits, credits, and billing;
- `AI-007`'s user-owned account/credential requirement and no-fallback rule;
- proposed ADR-0020's secret isolation;
- proposed ADR-0023's one-owned usage and atomic authorization; and
- deterministic/local operation without either OpenAI access mode.

## 2. Current official evidence

Official OpenAI documentation was retrieved on 2026-07-28.

| Source | Material result |
|---|---|
| OpenAI [Codex authentication](https://learn.chatgpt.com/docs/auth) | Codex supports ChatGPT sign-in for subscription access and API-key sign-in for usage-based access. API-key use is billed at standard API rates and does not use included ChatGPT plan credits. The same page says Platform API keys remain the credential for general OpenAI API calls. |
| OpenAI [Codex app-server](https://learn.chatgpt.com/docs/app-server) | App-server is intended for deep integration into another product. Its stable surface includes JSON-RPC over stdio, managed ChatGPT browser/device login, API-key login, account/plan reporting, ChatGPT rate-limit and token-activity reads, model/capability discovery, streamed turns, per-turn configuration, and `outputSchema`. |
| OpenAI [Codex SDK](https://learn.chatgpt.com/docs/codex-sdk) | The SDK is intended for programmatic integration and local Codex threads, but is described as coding-focused. Infinium's .NET topology can invoke the pinned app-server executable directly rather than introduce a TypeScript or Python application runtime solely for the SDK wrapper. |
| OpenAI [Codex pricing and feature availability](https://learn.chatgpt.com/docs/pricing) | Eligible ChatGPT plans include local Codex/SDK/scriptable workflows under plan limits; API-key mode is usage-priced. Plan consumption varies with model, context, reasoning, tools, retrieval, and caching, so prompt length alone is not a reliable plan-impact estimate. |
| OpenAI [`openai/codex`](https://github.com/openai/codex) | The local Codex/app-server implementation is open source under Apache-2.0 and publishes platform binaries. Exact redistribution, notices, transitive artifacts, pinning, and update behavior still require the normal release dependency review. |

No login flow, plan quota, or billable model call was exercised. Documentation
establishes availability and intended integration, not Infinium conformance or
the consumption of a representative scan.

## 3. Access-mode comparison

| Dimension | ChatGPT plan through Codex app-server | Platform API key through Responses |
|---|---|---|
| User ceremony | App-server-managed browser or device-code ChatGPT login | User creates/provides a Platform API key |
| Billing authority | ChatGPT/Codex plan limits and, where applicable, workspace credits | OpenAI Platform usage billing |
| Dollar estimate | Not applicable as a per-call API charge; do not invent one | Catalog-calculated estimate and local dollar limits, clearly distinct from final provider billing |
| Remaining-use visibility | App-server rate-limit windows, reset times, plan type, credits, and token activity where returned | API rate headers/admin capabilities where qualified; prepaid balance or exact remaining dollars only if a reliable API exposes them |
| Execution surface | Codex thread/turn agent runtime | Direct Responses request |
| Structured result | Per-turn `outputSchema`, subject to qualification | Responses Structured Outputs, subject to qualification |
| Tool posture | Codex tools must be disabled/allowlisted by a dedicated profile; search separately qualified | Only ADR-0013's separately admitted hosted `web_search`; no other model-selected tool |
| Hard limits | Local operation/turn/concurrency/deadline limits; provider plan headroom is observed, not treated as atomically reservable or dollar-priced | Atomic call/token/tool/catalog-money/deadline reservation where finite adapter bounds exist |
| Credential custody | App-server owns OAuth/device flow and refresh; Infinium receives non-secret status only | Proposed one-shot provider helper resolves exact API-key generation from Credential Manager |
| Model/capability set | Models and limits exposed to the authenticated Codex account | Models and capabilities exposed to the Platform project/key |
| Failure effect | May exhaust or wait for plan rate windows/credits; no API charge claim | May incur usage-priced API charges after dispatch |

The product may estimate **work shape** in either mode—candidate count,
planned operations, context volume, expected calls/turns, and duration. It may
show a calibrated empirical plan-impact range only after representative
measurements exist and must label it as an estimate. App-server's current
rate-limit snapshot is provider state, not permission to promise that a scan
will fit or to reserve a fixed percentage atomically.

## 4. Recommended ChatGPT-plan adapter boundary

The coordinator should launch one pinned local `codex app-server` provider
process over inherited stdio for the authenticated operation. Infinium should
not implement ChatGPT OAuth itself, copy a browser session, accept raw
ChatGPT tokens, or send ChatGPT credentials over its ordinary gRPC or WebView2
contracts.

The production profile must:

- use a dedicated product-controlled Codex state/config root rather than
  implicitly consuming or modifying the user's normal Codex CLI workspace;
- require app-server-managed browser/device login and an OS credential-store
  mode, subject to a Windows isolation/recovery spike;
- identify Infinium through app-server `clientInfo`;
- pin the app-server version and generated JSON schema;
- use a product-owned staging directory containing only the admitted,
  minimized, inert inputs for the one operation;
- prevent the thread from loading arbitrary project instructions, skills,
  plugins, MCP servers, memories, hooks, or user configuration;
- deny shell, filesystem-write, subprocess, computer-use, application,
  connector, and arbitrary-network tools;
- use read-only or stricter sandboxing even though no tool authority is
  intended;
- bind one thread/turn to one Infinium operation and apply the exact output
  schema;
- retain streamed item, model, usage, refusal, incomplete, error, and
  cancellation provenance needed for admission; and
- destroy operation staging under the accepted retention/deletion rules.

The first prototype must determine which of these controls are stable,
host-owned app-server settings rather than mutable project configuration. A
missing control is a capability gap and may reject ChatGPT plan mode.

Hosted search in the Codex adapter is not implied merely because Codex can
search. It requires a separate capability test proving the same discovery-only
authority, source provenance, host acquisition, and finite operation limits
accepted by ADR-0013.

## 5. Budget and UI consequences

Before starting a plan-backed operation, the UI should show:

- “ChatGPT plan through Codex,” not “OpenAI API”;
- authenticated plan/workspace identity to the extent safely exposed;
- current provider-reported rate windows, used percentage, reset times,
  credits, and unavailable fields;
- expected number/range of Infinium LLM turns and work population;
- estimated duration and locally configured turn/concurrency/deadline limits;
- an explicit statement that plan consumption is variable and no dollar API
  charge estimate applies; and
- the possibility that plan limits or credits may stop work before completion.

Before starting an API-key operation, the UI should show:

- “OpenAI Platform API — usage billed” prominently;
- selected model/service tier and Platform billing scope where known;
- estimated input/output/tool usage and catalog-calculated cost;
- configured hard call/token/tool/dollar/deadline limits;
- the distinction between Infinium's calculated estimate and the provider's
  final bill; and
- explicit confirmation for the estimated bounded operation or scan.

The user can save a default access profile, but each run binds its actual mode,
account/profile identity, model/capability snapshot, and budget semantics.
Automatic fallback between plan and API modes is prohibited.

## 6. Alternatives considered

### Reuse ChatGPT login directly with the Responses API

Rejected. Official documentation confines subscription login to Codex
surfaces and directs general API callers to Platform keys. Treating a ChatGPT
session token as an API credential would be unsupported and unsafe.

### Support API keys only

Technically simpler and remains a required fallback/advanced mode, but it
would expose users to avoidable usage-priced charges and ignore an officially
documented local integration surface available under eligible ChatGPT plans.
Reject as the desired product ceiling.

### Route both auth modes through Codex app-server

Rejected as the primary API-key design. It would make a coding-agent runtime
mandatory even when direct Responses provides tighter request, tool, token,
price, storage, and receipt control. The same account provider does not require
one transport.

### Make ChatGPT plan mode mandatory

Rejected. Users may lack an eligible plan, Codex models/capabilities can differ
from the Platform API, plan limits are not a dollar budget, and the
non-coding-quality/security qualification may fail. Local deterministic work
and API-key mode must remain independently usable.

### Ask users to install and configure Codex manually

Rejected as the intended release UX. The app-server is a programmatic product
dependency, not a modding prerequisite analogous to MO2. Exact bundling versus
managed installation remains a release artifact decision, but Infinium must
pin, detect, and own compatibility rather than silently use any executable on
`PATH`.

## 7. Recommendation and decision impact

Create proposed ADR-0024 to:

1. amend only ADR-0013's single initial execution-surface selection;
2. target two explicit OpenAI access profiles—ChatGPT/Codex plan and Platform
   API—without conflating authentication, billing, or capability;
3. make the ChatGPT/Codex path a qualification-gated prominent onboarding
   option and retain the direct Responses API-key path;
4. forbid direct reuse of ChatGPT credentials for general API calls and
   forbid cross-mode fallback;
5. require access-mode-specific estimates, limits, usage displays, and
   provenance; and
6. require a pinned app-server security/semantic prototype before production
   support.

Amend proposed ADR-0020 so its one-shot Credential Manager helper governs API
keys, while app-server owns the managed ChatGPT login lifecycle. Amend proposed
ADR-0023 so its exact catalog-money reservation applies to usage-priced API
work; plan-backed work uses separate non-money admission dimensions and honest
provider-headroom gaps.

## 8. Confidence and remaining gates

- **High:** API keys are usage-priced and do not consume included ChatGPT plan
  credits.
- **High:** ChatGPT sign-in is supported by Codex app-server, and app-server is
  documented for product integration.
- **High:** the two access modes need different billing and hard-limit
  semantics.
- **Medium-high:** app-server can technically carry Infinium's schema-bound
  operations because it exposes model/capability discovery, `outputSchema`,
  streamed events, authentication, and usage state.
- **Medium-low:** a coding-focused Codex model will match or exceed the direct
  Responses path for mod-analysis extraction/investigation without hidden
  tool/config authority. This requires an executable comparison, not
  documentation alone.
- **Medium-low:** the exact Windows credential-store isolation, dedicated
  `CODEX_HOME`, packaging/update, and process-footprint design will satisfy
  release requirements without further changes.

Before ChatGPT plan mode can be called supported:

- pin a Windows app-server build and archive its generated schema;
- prove browser and device login, refresh, logout, account/rate-limit reads,
  dedicated state isolation, and no token leakage;
- prove every disallowed tool/config/instruction surface remains unavailable;
- run matched source-extraction and evidence-investigation cases against the
  direct Responses path without provider-specific expected-answer rules;
- validate structured results, refusals, incomplete/cancelled turns, token
  usage, rate-limit exhaustion, and restart recovery;
- qualify model identity and retention/provenance fields;
- complete dependency/license/notices and updater/rollback review; and
- update exact cost/usage presets only from measured runs.

No evaluation is passed by this investigation.
