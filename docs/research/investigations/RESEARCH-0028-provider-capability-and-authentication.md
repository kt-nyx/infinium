# RESEARCH-0028: Provider capability and authentication boundary

Status: Completed; recommendation partially superseded

Subsequent revision: Authentication, capability-snapshot, invocation,
usage/cost, cancellation, and retention findings remain historical inputs.
The initial portability/parity implication is superseded by the owner's
OpenAI-first direction and
[RESEARCH-0032](RESEARCH-0032-openai-first-llm-and-web-search.md), as accepted
by ADR-0013.

Date: 2026-07-26

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary RQ: RQ-012

M0 wave: D — Documentation acquisition and provider-neutral LLM boundary

Decision enabled: Provider-adapter and authentication ADR work after owner
review, plus exact capability-gap and user-owned-credential evaluation
specifications

## Executive answer

OpenAI and Anthropic can both support the two stateless, schema-constrained
operations proposed by
[RESEARCH-0027](RESEARCH-0027-provider-neutral-llm-contract.md), but their
account, billing, model-identity, batch, cancellation, and retention surfaces
are not interchangeable.

The smallest honest adapter boundary is therefore:

1. a **user-supplied API credential profile** bound to an explicitly selected
   API organization/project or workspace;
2. a **capability snapshot** that distinguishes inference, structured output,
   model discovery, batch, cancellation, rate-limit telemetry, administrative
   usage/cost reporting, and retention controls;
3. a **trusted invocation receipt** containing requested and returned model
   identity, provider request identity, usage, rate-limit state, cost state,
   cancellation state, and effective retention declarations; and
4. explicit `unsupported`, `not_authorized`, `not_documented`, and
   `not_verified` states rather than fabricated cross-provider parity.

For a third-party local desktop app, the documented initial authentication
path is an API/platform credential owned by the user. OpenAI project selection
can be expressed by a project-scoped key or, for applicable legacy user keys,
organization/project headers. Anthropic inference keys are scoped to one
workspace, so selecting a workspace means selecting the corresponding
credential profile; listing workspaces requires administrative authority and
is unavailable for Anthropic individual accounts. Both providers also
document workload-identity federation, but that is workload authentication,
not consumer “Sign in with ChatGPT/Claude.”

ChatGPT and Claude consumer subscriptions are separate from API billing. No
officially supported direct-API surface found in this investigation exposes a
user's current prepaid-credit balance. Both providers expose request-window
rate-limit headroom, and administrative APIs expose historical usage/cost
data; those are different facts. Infinium may display a remaining API credit
balance only if a future provider exposes and documents that exact capability.
Otherwise it must show unavailable and may link the user to the provider's
billing console.

No credential was used and no live or paid request was made. This is a
documentation-level portability result, not adapter conformance.

## 1. Question and accepted constraints

### 1.1 Primary question

Which provider authentication modes and APIs support explicit
user-account/project/workspace selection, billing attribution, model
discovery and stable identities, structured output, batching, exact or
estimated token and cost reporting, quota/rate/spend visibility,
cancellation, and retention controls?

### 1.2 Repository constraints

| Input | Consequence |
|---|---|
| `AI-001`, `AI-002` | The core remains provider neutral while users select a provider and may override model routing. |
| `AI-004` | Estimates, reservations, hard limits, attempt deadlines, and reconciliation cannot depend on a provider exposing a live credit balance. |
| `AI-005` | Historical usage, rate limits, and remaining quota/credits are shown only when a provider reliably exposes the exact fact; absence is explicit. |
| `AI-006` | Exact provider, requested/resolved model, settings, prompt/schema/tool versions, token usage, cost state, and limitations remain attributable. |
| `AI-007` | Provider use requires the user's own selected account/credential context; no project/shared fallback account may silently pay for work. |
| `SEC-002` | Credentials require secure storage, replacement, and revocation behavior; this report defines capability, not the storage mechanism. |
| `OPS-001`, `OPS-002` | Provider availability and replayability remain explicit, and local-only analysis must not require an LLM provider. |
| [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md) | Provider output remains untrusted semantic evidence, never local-state or finding authority. |
| [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md) | Every attempt stays bound to one immutable local analysis context. |
| [ADR-0004](../../architecture/decisions/ADR-0004-initial-target-scope.md) | The initial product remains a local Skyrim SE/MO2 advisor; this research does not generalize the application architecture. |
| [ADR-0005](../../architecture/decisions/ADR-0005-nexus-supported-api-analysis.md) | Provider capability does not authorize source transmission; source policy remains independently governed. |
| [RESEARCH-0003](RESEARCH-0003-retention-replay-export-policy.md) | Local retention, provider transmission, provider-side storage, and export are independent permissions. |
| [RESEARCH-0004](RESEARCH-0004-wave-a-policy-and-evidence-handling-integration.md) | Provider data controls cannot silently weaken accepted provenance or deletion-gap behavior. |
| [RESEARCH-0027](RESEARCH-0027-provider-neutral-llm-contract.md) | The M1 semantic core is two stateless structured-output operations with no tools; provider transport and operational metadata stay outside them. |

This investigation compares direct OpenAI API and direct Anthropic Claude API
surfaces. It does not select a provider, SDK, application language, credential
store, billing model, UI, queue, or process topology.

## 2. Scope, non-scope, and method

### 2.1 In scope

- API/platform credentials and explicit billing-scope selection.
- The distinction between API credentials, workload identity, and consumer
  product login/subscriptions.
- Model discovery and documented pinned-versus-alias identity.
- Structured-output availability for the RESEARCH-0027 contract.
- Synchronous and batch execution/cancellation semantics.
- Per-request usage, historical usage/cost reports, rate-limit headroom,
  spend limits, and credit-balance visibility.
- Provider retention defaults and request/organization-level controls.
- A provider-neutral capability matrix and minimum adapter contract.

### 2.2 Explicit non-scope

- Live credential validation, paid inference, latency, quality, or cost
  benchmarking.
- Browser-session automation, consumer subscription token reuse, reverse
  engineering, or undocumented billing endpoints.
- OAuth authorization for an Infinium desktop app. Neither provider's
  workload-identity OAuth/token exchange is evidence of consumer delegated
  login support.
- Secure-secret implementation; RQ-018 owns that work.
- Cost-ledger, reservation, and concurrent hard-bound implementation; RQ-034
  owns that work.
- Storage schema or job topology; RQ-013, RQ-015, and RQ-017 own those choices.
- Provider-side tools, retrieval, file storage, agents, or conversation state.
- An architecture or provider selection.

### 2.3 Method and access

The current OpenAI documentation and OpenAPI specification were read through
the official OpenAI developer-documentation interface. Anthropic was compared
from current official Claude Platform documentation and official Anthropic
help/privacy material. Endpoint inventories and public contracts were
inspected without invoking them.

| Item | Treatment |
|---|---|
| Credentials or secrets | Not accessed |
| Authenticated/provider APIs | Not invoked |
| Paid requests | None |
| Browser sessions | Not used |
| Local private/mod data | Not accessed or transmitted |
| Workspace write | This report only |
| Empirical claims | None; every runtime behavior remains unverified |

## 3. Current primary sources

All sources were retrieved or rechecked on **2026-07-26**. Provider pages are
live documentation without a displayed immutable page revision unless noted.

### 3.1 OpenAI

| ID | Primary source | Relevant contract |
|---|---|---|
| O1 | [API authentication](https://developers.openai.com/api/reference/overview#authentication) | Bearer API keys or workload-identity access tokens; organization/project headers; usage attribution. |
| O2 | [Models endpoint](https://developers.openai.com/api/reference/resources/models/methods/list) | Lists models currently available to the credential and basic IDs/ownership. |
| O3 | [Structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs) | Provider-specific strict JSON-schema output surface. |
| O4 | [Batch API](https://developers.openai.com/api/docs/guides/batch) | Asynchronous batch lifecycle and cancellation; cancellation may remain in `cancelling` for up to ten minutes. |
| O5 | [Background mode](https://developers.openai.com/api/docs/guides/background) | Background cancellation endpoint; synchronous cancellation by terminating the connection. |
| O6 | [Rate limits](https://developers.openai.com/api/docs/guides/rate-limits#rate-limits-in-headers) | Request/token/project-token limit, remaining, and reset response headers. |
| O7 | [Organization completions usage API](https://developers.openai.com/api/reference/resources/admin/subresources/organization/subresources/usage/methods/completions) | Admin-key historical token/request data with project, API-key, model, batch, and service-tier grouping. |
| O8 | [Organization costs API](https://developers.openai.com/api/reference/resources/admin/subresources/organization/subresources/usage/methods/costs) | Admin-key daily historical USD cost buckets filterable by project/API key. |
| O9 | [Data controls](https://developers.openai.com/api/docs/guides/your-data) | Training, abuse-monitoring, application-state, `store`, ZDR, background, and Batch retention boundaries. |
| O10 | [ChatGPT and API billing separation](https://help.openai.com/en/articles/8156019) | ChatGPT subscriptions and API service are billed and managed separately. |
| O11 | [Prepaid API billing](https://help.openai.com/en/articles/8264644) | Credit balance exists in the billing portal; exhaustion and delayed cutoff behavior. |
| O12 | OpenAI OpenAPI specification `2.3.0`, endpoints `/v1/models`, `/v1/organization/usage/completions`, `/v1/organization/costs`, `/v1/batches`, and `/v1/responses/{response_id}/cancel` | Public endpoint and authentication-shape cross-check. |
| O13 | [Counting tokens](https://developers.openai.com/api/docs/guides/token-counting) | Exact preflight input-token count for a matching Responses payload through `POST /v1/responses/input_tokens`. |

### 3.2 Anthropic

| ID | Primary source | Relevant contract |
|---|---|---|
| A1 | [Claude API overview](https://platform.claude.com/docs/en/api/overview) | API key/WIF authentication, Models and Message Batches APIs, response organization ID, workspace segmentation. |
| A2 | [Authentication](https://platform.claude.com/docs/en/manage-claude/authentication) | Static API keys, key expiration/revocation, and workload-identity federation. |
| A3 | [Workspaces](https://platform.claude.com/docs/en/manage-claude/workspaces) | Keys and resources are workspace scoped; Admin API lists workspaces; default workspace has no listed ID. |
| A4 | [Models API](https://platform.claude.com/docs/en/api/models/list) | Lists models available to the credential. |
| A5 | [Model IDs and versioning](https://platform.claude.com/docs/en/about-claude/models/model-ids-and-versions) | Canonical pinned model IDs versus convenience aliases; serving infrastructure may still change. |
| A6 | [Structured outputs](https://platform.claude.com/docs/en/build-with-claude/structured-outputs) | `output_config.format`, supported schema subset, and constrained JSON. |
| A7 | [Message Batches](https://platform.claude.com/docs/en/build-with-claude/batch-processing) | Workspace-scoped asynchronous jobs, 24-hour processing expiry, partial cancellation, and 29-day result retention. |
| A8 | [Rate limits](https://platform.claude.com/docs/en/api/rate-limits) | Request/input/output-token headroom headers, organization/workspace limits, and Admin Rate Limits API. |
| A9 | [Usage and Cost API](https://platform.claude.com/docs/en/manage-claude/usage-cost-api) | Admin-key historical reporting by workspace/key/model; data typically appears within five minutes; unavailable to individual accounts. |
| A10 | [Cost report](https://platform.claude.com/docs/en/api/admin/cost_report) | Historical amounts in fractional cents with workspace/model/service-tier dimensions. |
| A11 | [API and data retention](https://platform.claude.com/docs/en/manage-claude/api-and-data-retention) | Standard/ZDR/feature-specific retention and structured-output grammar caching. |
| A12 | [Commercial API retention](https://privacy.claude.com/en/articles/7996866-how-long-do-you-store-my-organization-s-data) | Standard API inputs/outputs deleted within 30 days, subject to stated exceptions. |
| A13 | [Consumer subscription/API separation](https://support.anthropic.com/en/articles/9876003-i-subscribe-to-a-paid-claude-ai-plan-why-do-i-have-to-pay-separately-for-api-usage-on-console) | Claude paid consumer plan does not include Console API usage. |
| A14 | [API prepaid credits](https://support.anthropic.com/en/articles/8977456-how-do-i-pay-for-my-api-usage) | Credit balance and reload controls are displayed in Console billing. |
| A15 | [API billing/disconnection](https://support.anthropic.com/en/articles/8114526-how-will-i-be-billed) | A client disconnect or timeout can still be charged when a successful request was in progress. |

### 3.3 Source limitations

- Documentation describes supported contracts, not the caller's actual role,
  account tier, model access, or runtime behavior.
- OpenAI's public Costs specification exposes one-day buckets and does not
  state a freshness/finality SLA. Anthropic says Usage/Cost data typically
  appears within five minutes, not that every bucket is immediately final.
- Absence of a credit-balance endpoint is based on the documented API
  inventories and billing guidance, not a proof that no private/internal
  endpoint exists. Infinium must not depend on private/internal endpoints.

## 4. Authentication and billing-scope findings

### 4.1 API identity is not consumer identity

| Question | OpenAI direct API | Anthropic direct API |
|---|---|---|
| Normal user-owned inference credential | Bearer API key | `x-api-key` API key |
| Short-lived workload credential | Workload identity federation access token | Workload identity federation access token |
| Consumer subscription pays API use | No; ChatGPT and API billing are separate | No; Claude consumer plans and Console API billing are separate |
| Documented delegated consumer login for an arbitrary desktop app | Not found | Not found |
| Safe M1 interpretation | User supplies/selects an API credential profile | User supplies/selects an API credential profile |

Workload identity federation maps a trusted workload to a provider service
account. It does not authorize Infinium to reuse a user's ChatGPT or Claude
consumer login. Consumer-product login behavior in first-party applications
must not be generalized into a third-party desktop authentication contract.

### 4.2 Project/workspace selection

**OpenAI**

- Project-scoped credentials naturally bill and authorize their project.
- For a user in multiple organizations or a legacy user API key, O1 documents
  `OpenAI-Organization` and `OpenAI-Project` headers; usage counts against the
  selected scope.
- Administrative project enumeration and cost/usage reporting require
  administrative authority. A normal inference key must not be assumed able
  to enumerate every organization/project.

**Anthropic**

- A normal API key belongs to one workspace. There is no documented
  per-request “choose workspace” header for that key.
- Workspace choice is therefore credential-profile choice. Batch resources,
  files, and prompt caches are also workspace isolated.
- Listing workspaces requires an Admin API key; the Admin API is unavailable
  for individual accounts. The default workspace has no ID and does not appear
  in workspace list endpoints.

Infinium must save a non-secret provider-profile label and verified billing
scope separately from the secret reference. It must never switch to another
credential or scope on authentication, quota, or billing failure.

## 5. Provider-neutral capability matrix

Legend:

- **Direct** — supported by an ordinary inference credential/request.
- **Admin** — supported only with separately authorized administrative access.
- **Derived** — Infinium can calculate it from documented response data and a
  versioned local catalog, but it is not provider-reported billing truth.
- **Conditional** — provider/model/account dependent and must be probed or
  declared.
- **Unavailable** — no supported direct surface found for the required fact.

| Capability | OpenAI direct API | Anthropic direct API | Portable boundary |
|---|---|---|---|
| User-owned API credential | Direct API key; WIF for configured workloads | Direct API key; WIF for configured workloads | Credential reference plus `auth_mode`; never model-visible |
| Explicit billing scope | Project-scoped key or applicable org/project headers | Workspace-scoped key | Required verified `billing_scope`; selection mechanics remain adapter-specific |
| Enumerate scopes | Admin project APIs | Admin workspaces API; unavailable to individual accounts | Optional Admin capability; manual profile labeling remains valid |
| Consumer subscription use | Unavailable | Unavailable | Explicitly reject as API auth/billing mode |
| Model discovery | Direct `GET /v1/models` | Direct `GET /v1/models` | Cache a dated availability snapshot; availability is not suitability |
| Pinned model identity | Conditional; provider/model documentation must identify snapshot semantics | Directly documented canonical pinned IDs; earlier convenience aliases may move | Store requested ID, returned ID, identity class, and documentation snapshot |
| Structured JSON output | Conditional by model; strict schema subset | Conditional by model; `output_config.format` schema subset | RESEARCH-0027 common schema plus adapter capability check |
| Per-response token usage | Direct response `usage` | Direct response `usage`; total input includes uncached plus cache-create/read tokens | Provider-normalized token categories plus untouched raw usage |
| Preflight token count | Direct input-token-counting API for a matching Responses payload | Direct token-counting API | Optional provider count with declared scope/authority; dollar cost remains estimated until provider reporting |
| Immediate dollar cost | Not returned as authoritative dollars | Not returned as authoritative dollars | Derived estimate only, using versioned prices and actual usage |
| Historical authoritative cost | Admin Costs API, daily buckets | Admin Cost API, typically visible within five minutes; unavailable to individual accounts | Optional `historical_cost_report` with freshness and authority metadata |
| Historical usage | Admin Usage API | Admin Usage API; unavailable to individual accounts | Optional; never required to complete an inference receipt |
| Rate-window headroom | Direct response headers | Direct response headers | Normalize limit/remaining/reset by limiter type |
| Configured rate limits | Admin project APIs where authorized | Admin Rate Limits API where authorized | Optional configuration view, distinct from live headroom |
| Spend limit/cap | Administrative/project surfaces where authorized | Console and workspace limits; programmatic administrative surfaces vary by organization type | Optional declared limit; never infer remaining balance from it |
| Remaining prepaid credits | Unavailable through a documented public API | Unavailable through a documented public API; Console displays balance | `not_supported`; provider-console link may be shown |
| Sync cancellation | Terminate connection; no durable terminal receipt for ordinary sync invocation | No Messages cancel endpoint; disconnect/timeout may still be billed | `client_aborted` is not `provider_cancelled`; final usage/cost may remain unknown |
| Provider-managed async response cancellation | Background Responses cancel endpoint; temporary state required | No equivalent needed for normal Messages comparison | Optional capability outside the RESEARCH-0027 M1 core |
| Batch | Direct; 24-hour completion window | Direct; processing expires after 24 hours | Optional asynchronous mode with provider lifecycle mapping |
| Batch cancellation | `cancelling`, in-flight work may finish for up to ten minutes; partial results | `canceling`, then `ended`; processed requests may remain as partial results | Accepted request is not terminal cancellation; usage may continue until terminal |
| Request-level retention control | Responses `store=false`; does not remove default abuse-monitoring logs or feature-specific storage | No ordinary per-request “store false” equivalent for stateless Messages | Declare effective provider policy; never equate request state with all retention |
| Organization retention control | MAM/ZDR by approval; endpoint eligibility varies | ZDR by arrangement; model/feature eligibility varies | Capability/policy snapshot plus exact request feature set |
| Batch retention | Application state until deleted; Batch not ZDR eligible | Up to 29 days; deletable after processing; not ZDR eligible | Batch opt-in must disclose different retention before dispatch |

## 6. Usage, cost, quota, and “remaining” semantics

These terms must remain separate:

| Fact | Meaning | Authority |
|---|---|---|
| `request_usage` | Tokens/tool units reported for one completed response or batch item | Provider response |
| `estimated_cost` | Infinium calculation from usage, service tier, cache classes, and a versioned price catalog | Derived; not billing truth |
| `historical_usage` | Aggregated provider usage buckets | Administrative provider report |
| `historical_cost` | Aggregated provider billing-cost buckets | Administrative provider report |
| `rate_headroom` | Requests/tokens remaining in a replenishing limiter window | Provider response headers |
| `configured_spend_limit` | A cap that may stop future use | Provider configuration/console |
| `prepaid_credit_balance` | Purchased credit still available for billing | Billing system |

Neither rate headroom nor `spend limit - historical cost` is a prepaid-credit
balance. Costs may arrive late, scopes may contain unrelated activity,
discounts/adjustments may apply, and provider cutoff can lag balance
exhaustion. Infinium must not synthesize a “remaining credits” number from
those inputs.

The supported M1 behavior should be:

- always record provider-reported per-attempt usage when present;
- calculate and label a price-catalog estimate when possible;
- reconcile against administrative historical cost only when the user
  separately authorizes that capability;
- show rate-window remaining/reset from response headers;
- show prepaid credits as unavailable for both surveyed providers; and
- retain reservation/hard-bound accounting locally under RQ-034 rather than
  depending on provider balance enforcement.

Cost state needs at least:

```text
unknown
estimated_from_provider_usage
provider_reported_historical
reconciled
```

`provider_reported_historical` is not automatically `reconciled`: the adapter
must preserve the report interval, retrieval time, grouping keys, currency,
and any provider freshness limitation.

## 7. Model identity and discovery

`GET /v1/models` answers “what IDs are currently available to this
credential,” not “which model is compatible with the Infinium contract” or
“will this alias never move.”

The adapter registry should classify every selectable model as:

```text
pinned_provider_identity
moving_alias
unknown_stability
```

Anthropic A5 explicitly states that canonical model IDs are pinned for the
lifetime of the ID, while earlier convenience aliases can resolve to the most
recent dated snapshot. It also warns that serving infrastructure may change
even when model weights remain fixed.

OpenAI model naming and snapshot availability vary by model family. The
Models endpoint exposes the ID and availability but no general `is_snapshot`
or alias-target field. Therefore an OpenAI ID may be marked pinned only from
current model-specific official documentation, not from its spelling or the
Models response alone.

Every invocation receipt must preserve:

- requested model ID;
- provider-returned model ID, when returned;
- model identity classification and its source/retrieval date;
- capability snapshot ID;
- provider API version/beta headers;
- service tier and material generation settings; and
- an explicit limitation when the provider cannot prove a resolved immutable
  identity.

A pinned ID improves reproducibility but does not guarantee identical output.

## 8. Cancellation and usage finality

### 8.1 Synchronous invocation

OpenAI documents terminating the connection to cancel a synchronous response.
Anthropic exposes no Messages cancellation resource and warns that an
in-progress request can still be charged after client disconnect/timeout.

For both adapters, a local cancellation signal and closed connection mean only
that Infinium stopped waiting. They do not prove that provider computation
stopped immediately, that no more tokens were billed, or that a terminal
usage object was received.

The normalized terminal states should distinguish:

```text
completed
provider_failed
client_aborted_usage_known
client_aborted_usage_unknown
provider_cancellation_requested
provider_cancelled_terminal
```

### 8.2 Batch invocation

Both providers accept a cancellation request before reaching a terminal
state, and both may retain successful partial results and bill the work that
completed. OpenAI documents an up-to-ten-minute `cancelling` period for
in-flight requests. Anthropic changes to `canceling`, later reaches `ended`,
and marks unprocessed items canceled while preserving completed items.

Reservations must therefore remain outstanding until terminal item states are
observed or a conservative local deadline/unknown-cost policy takes over.

## 9. Retention differences

### 9.1 OpenAI

- API data is not used for training by default.
- Default abuse-monitoring logs may retain customer content for up to 30 days.
- Responses application state is retained for 30 days by default or
  with `store=true`; `store=false` avoids stored Responses-object
  retrieval/application state, subject to documented feature-specific
  exceptions, but is not the same as ZDR.
- Background mode temporarily stores response data to support polling.
- Batch application state is retained until deleted and Batch is not ZDR
  eligible.
- MAM/ZDR require approval and can be configured by organization/project,
  subject to endpoint/model limitations.

### 9.2 Anthropic

- Standard commercial API inputs/outputs are deleted within 30 days, subject
  to feature, safety, legal, and contractual exceptions.
- Stateless eligible Messages can be covered by an approved organization-wide
  ZDR arrangement; ordinary requests do not expose an OpenAI-style `store`
  switch.
- Structured outputs may cache the JSON schema grammar for up to 24 hours
  since last use even under its qualified ZDR treatment; prompt/response
  content is treated separately.
- Message Batches require asynchronous storage, retain data/results for up to
  29 days, and are not ZDR eligible.
- Model-specific retention requirements can make a model unavailable under
  ZDR or require a workspace retention override.

### 9.3 Adapter consequence

Retention cannot be a single boolean. A capability/policy snapshot needs:

```text
training_use
abuse_or_safety_retention
application_state_retention
request_level_storage_control
organization_retention_mode
feature_specific_retention
batch_retention
provider_deletion_control
unknown_or_contract_specific_limitations
```

The effective declaration must be computed for the exact provider, billing
scope, model, endpoint/mode, and features before dispatch. The accepted
four-permission local policy from RESEARCH-0003 remains authoritative;
provider retention metadata does not itself authorize transmission or local
storage.

## 10. Minimum provider-adapter boundary

This is a capability contract, not a class hierarchy or technology choice.

### 10.1 Provider profile

```text
ProviderProfile {
  provider_profile_id
  provider_id
  direct_api_base_identity
  auth_mode
  credential_secret_ref
  credential_owner = user
  organization_or_account_id
  billing_scope_type
  billing_scope_id
  billing_scope_display_label
  scope_selection_method
  capability_snapshot_id
}
```

No raw secret, consumer session, or provider access token may enter the
logical LLM request, retained report, model prompt, or export by default.

### 10.2 Capability snapshot

Each optional operation declares `supported`, `unsupported`,
`not_authorized`, `not_documented`, or `not_verified`, plus source version and
last verification time:

```text
list_models
structured_output
count_input_tokens
invoke_synchronous
cancel_synchronous
submit_batch
poll_batch
cancel_batch
read_historical_usage
read_historical_cost
read_rate_limit_configuration
read_live_rate_headroom
read_spend_limit
read_prepaid_credit_balance
declare_retention
request_provider_storage_control
delete_provider_object
```

### 10.3 Trusted invocation receipt

In addition to the RESEARCH-0027 logical request/result envelope:

```text
ProviderInvocationReceipt {
  provider_profile_id
  billing_scope_id
  capability_snapshot_id
  attempt_id
  transport_mode
  requested_model_id
  returned_model_id
  model_identity_class
  provider_request_ids
  started_at
  ended_at
  terminal_state
  provider_stop_or_error
  raw_usage
  normalized_usage
  rate_headroom
  cost_state
  estimated_cost
  historical_cost_refs
  cancellation_state
  retention_declaration_id
}
```

The host, not the model, authors this receipt. Unknown usage/cost after abort
remains unknown until reconciliation.

## 11. Alternatives considered

| Alternative | Benefit | Failure against requirements | Disposition |
|---|---|---|---|
| Consumer “Sign in with ChatGPT/Claude” | Familiar UX | No documented arbitrary third-party API billing delegation; consumer/API products are separate | Reject for current plan |
| API key only, no billing-scope record | Minimal setup | Cannot prove selected project/workspace or prevent silent scope confusion | Reject |
| Require Admin keys | Rich usage/cost/scope discovery | Excess privilege; unavailable to some users/accounts; violates minimum-authority posture | Reject as baseline; optional capability only |
| Treat every model ID as pinned | Simple caching | False across aliases/providers; model list does not prove stability | Reject |
| Estimate credit balance from usage/cost | Gives a number | Not billing truth; ignores delays, adjustments, unrelated usage, and cutoff lag | Reject |
| Use batch for all exhaustive work | Cost/throughput advantage | Different retention, cancellation, latency, and result-finality semantics | Defer as optional mode |
| One retention boolean | Simple UI/schema | Collapses abuse logs, application state, batch state, schema cache, ZDR, and deletion | Reject |
| Provider-specific domain objects | Full provider fidelity | Leaks transport/account semantics into findings and breaks neutrality | Reject |

## 12. Uncertainty and unresolved verification

1. No live call verified credential validation, returned project/workspace
   identity, model list, structured schema acceptance, response headers,
   cancellation, usage, or costs.
2. No provider SDK was selected or tested. SDK behavior must not replace the
   HTTP contract.
3. OpenAI Costs documentation does not state a freshness/finality SLA.
4. Anthropic Usage/Cost reporting is unavailable to individual accounts and
   normally requires a separate Admin key; actual organization eligibility is
   unknown.
5. Administrative role and key-creation availability can differ by provider
   account type and may change.
6. OpenAI stable-snapshot semantics must be qualified per selected model; the
   Models endpoint alone is insufficient.
7. Provider pricing, model availability, structured-output subsets, API
   versions, rate headers, and retention eligibility are volatile and require
   a dated capability registry.
8. The absence of a supported prepaid-balance API must be rechecked before
   implementation, but undocumented/private endpoints remain prohibited.
9. Client abort behavior and post-abort billing must be tested with
   non-sensitive, bounded, user-funded calls only after an accepted plan
   authorizes that work.
10. This report does not decide whether optional administrative telemetry is
    worth the secret-management and privilege cost.

## 13. Recommendation

Confidence: **High** in the documented separation of consumer and API
accounts, API-key/project/workspace boundaries, structured-output
portability, administrative usage/cost distinction, rate-headroom semantics,
batch cancellation/retention differences, and lack of a supported prepaid
credit-balance API; **medium** in operational details pending bounded live
conformance.

Recommend:

1. Use user-owned direct API credential profiles as the only initial
   authentication assumption.
2. Require explicit provider and billing-scope confirmation; never fail over
   to another credential/account/project/workspace.
3. Treat workload identity as an optional deployment credential, not consumer
   delegated login.
4. Keep administrative usage/cost/scope discovery optional and separately
   authorized; never require an Admin key for ordinary inference.
5. Implement the capability snapshot and trusted invocation receipt boundary
   in §§10.2–10.3 when an ADR accepts the provider design.
6. Normalize rate-window headroom, historical usage/cost, configured spend
   limits, and prepaid credit balance as distinct capabilities.
7. Report prepaid credit balance as unavailable for both surveyed direct APIs.
8. Use a versioned local price catalog for preflight/post-response estimates,
   with provider historical cost as later reconciliation where authorized.
9. Prefer documented pinned model identities, but retain requested/returned
   identity and stability limitations for every attempt.
10. Keep synchronous and batch cancellation stateful and conservative; a
    local abort or accepted cancel request is not terminal provider
    cancellation or zero cost.
11. Keep batch opt-in because it changes latency, cancellation, usage
    finality, and provider-side retention.
12. Revalidate documentation and run bounded conformance before declaring an
    adapter supported.

## 14. Downstream work enabled

These are proposals for coordinator/owner review. This report does not apply
them.

### 14.1 ADR work

Create or extend the proposed LLM provider ADR after RQ-011/RQ-012 review to:

- accept the provider-profile, capability-snapshot, and trusted-receipt
  boundaries;
- select the initial reference adapter without weakening provider neutrality;
- state the user-owned API credential and no-fallback rule;
- distinguish ordinary and administrative credentials;
- define stable-model qualification and capability-registry refresh policy;
- preserve unknown usage/cost after cancellation;
- make batch an explicit retention-changing mode; and
- keep consumer subscription login and undocumented endpoints out of scope.

RQ-018 should separately decide secure credential storage and revocation.
RQ-034 should separately decide estimates, reservations, reconciliation,
deadlines, and hard-limit behavior.

### 14.2 Evaluation work

| Existing case | Required specification |
|---|---|
| `EVAL-0034` | No secret or unnecessary account/local context enters a provider payload, retained logical request, or default export. |
| `EVAL-0064` | OpenAI and a materially different adapter preserve the RESEARCH-0027 logical contract while unsupported capabilities remain explicit. |
| `EVAL-0067` | Trusted provider/model/usage/cost/retention metadata remains distinct from model-authored claims. |
| `EVAL-0076` | Capability UI/API distinguishes request rate headroom, historical usage/cost, spend limit, and unavailable prepaid credits without invention. |
| `EVAL-0077` | A run uses only the selected user-owned credential and verified billing scope; auth/quota failure cannot fall back to another account. |
| `EVAL-0081` | Abort, delayed cost visibility, partial batch cancellation, and reconciliation cannot exceed or falsely release local reservations. |
| `EVAL-0083` | Invocation provenance resolves provider profile, billing scope, capability snapshot, requested/returned model, usage/cost state, and retention declaration. |

Additional adapter conformance should cover moving-alias rejection,
admin-capability absence, individual Anthropic account behavior, cancellation
with unknown usage, batch partial results, and retention-mode incompatibility.

## 15. Suggested RQ-012 status

Suggested update:

> **Researched; provider capability/authentication boundary proposed.**
> OpenAI and Anthropic direct APIs can carry the RQ-011 structured semantic
> contract through user-owned API credential profiles, but scope selection,
> administrative telemetry, stable model identity, cancellation, and
> retention require explicit adapter capabilities. Consumer subscriptions are
> not API billing/authentication. Rate headroom and historical usage/cost are
> available at different privilege levels; neither surveyed public API
> exposes current prepaid credits. Resolution for M0 requires integration
> review and an accepted provider ADR; live conformance remains later work.

## 16. Validation and semantic self-review

Documentation checks completed:

- official current OpenAI API documentation and OpenAPI endpoint inventory;
- official current Claude Platform API, Admin, billing, and retention
  documentation;
- direct comparison of authentication, scope, model, structured-output,
  usage/cost, rate, cancellation, batch, and retention surfaces;
- explicit review of consumer/API product separation and remaining-credit
  availability;
- cross-check against RESEARCH-0027 and accepted retention/trust boundaries.

Semantic checks:

- No consumer OAuth/account-login support is invented.
- Workload-identity federation is not mislabeled as user delegated login.
- API key, Admin key, consumer subscription, project, and workspace remain
  distinct.
- Rate headroom, spend limit, historical cost, and credit balance remain
  distinct.
- Provider-reported tokens are not mislabeled as provider-reported dollars.
- Cost estimates and historical cost reconciliation remain distinct.
- Model availability is not treated as schema compatibility or pinned
  identity.
- Local abort, accepted cancellation, and terminal cancellation remain
  distinct.
- Request storage, abuse/safety retention, application state, batch state,
  ZDR, and local retention remain distinct.
- Batch capability does not silently enter the M1 semantic core.
- No provider, SDK, stack, storage, process topology, or credential mechanism
  is selected.
- No live capability, quality, latency, or cost claim is made.
