# RESEARCH-0032: OpenAI-first LLM and governed web-search boundary

Status: Completed
Disposition: recommendation accepted by ADR-0013
Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary RQs: RQ-011 and RQ-012

M0 wave: D follow-up — OpenAI-first provider and web-search direction

Decision enabled: Superseding owner/ADR disposition for AI-001, the initial
OpenAI adapter and operation set, governed web discovery, and later
provider-expansion boundaries

Subsequent disposition: OpenAI-first delivery without a second-provider parity
gate and the Responses, hosted-search, execution-mode, and
capability-boundary mechanism were accepted in
[ADR-0013](../../architecture/decisions/ADR-0013-openai-first-llm-capability-boundary.md).
Exact model, account, credential, cost, retention, and implementation
conformance remain pending.

## Executive answer

Infinium should implement **OpenAI first** through the Responses API and should
not constrain its initial LLM feature set to capabilities shared by every
possible provider.

Provider independence remains important at the **domain-truth boundary**:
installation observations, source records, exact passages, claims,
candidates, hypotheses, findings, cases, recommendations, coverage, and
readiness must not become OpenAI objects. It should not remain a requirement
that every LLM operation, tool, execution mode, or response envelope can be
implemented identically by another provider.

The two stateless, schema-constrained operations proposed by
[RESEARCH-0027](RESEARCH-0027-provider-neutral-llm-contract.md) remain the
correct safe primitives for:

1. source-bound claim extraction; and
2. evidence-bound candidate investigation.

They are a common semantic subset, not an architectural ceiling. The initial
OpenAI capability profile should additionally support:

- Responses API Structured Outputs for those two operations and later bounded
  synthesis operations;
- OpenAI-hosted `web_search` for governed discovery of obscure documented
  interactions;
- provider-native search actions, complete source URL lists, and inline
  citation annotations as discovery provenance;
- background Responses for individual long-running investigations where their
  provider-side state and retention are accepted;
- Batch as an optional high-throughput mode for large populations of
  independent extraction or investigation requests;
- prompt caching where measured reuse offsets its current write/read costs and
  source-retention implications; and
- OpenAI-specific usage, rate-limit, cancellation, storage, and administrative
  telemetry without pretending that another provider must expose the same
  features.

OpenAI web search does **not** remove the need for separate landing-source
acquisition. Its response exposes search actions, URLs, titles, citations, and
optionally every consulted source URL, but not the exact source bytes,
source-controlled revision identity, deterministic passage offsets, complete
authorship/authority adjudication, or a source-specific acquisition-policy
decision. Search output therefore creates discovery records and investigative
leads. A claim can gain normal external-source authority only after an approved
source adapter acquires and fingerprints the landing content, extracts exact
passages, and applies the source registry.

No OpenAI capability makes model output local-state authority. The hosted web
tool should be the only model-selected network tool in the initial scope.
Shell, code execution, computer use, remote MCP, arbitrary function tools, and
write-capable actions are neither required nor recommended for M1.

## 1. Question and owner direction

### 1.1 Revised questions

RQ-011, revised conceptually:

> What safe semantic and provider-specific LLM operations should Infinium
> expose when OpenAI is the initial supported provider, without allowing
> provider portability to remove useful capabilities?

RQ-012, revised conceptually:

> Which current OpenAI API capabilities, identities, execution modes,
> telemetry, and gaps should define the initial provider profile, with other
> providers added only after the MVP path is working?

### 1.2 Direct owner direction

The project owner directed on 2026-07-28 that:

- useful LLM integration must not be removed to satisfy provider neutrality;
- OpenAI may be the only implemented and supported provider for the initial
  product path;
- later providers may expose different capability sets instead of forcing one
  lowest-common-denominator interface; and
- what an LLM does must be selected from product value, evidence, safety, cost,
  and evaluation—not universal provider availability.

This report treats that direction as authoritative product input. It proposes
the technical consequence and the exact ADR/requirement changes needed; it does
not edit or accept them.

### 1.3 Accepted boundaries that remain unchanged

- [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md):
  deterministic state and applicable source authority remain outside model
  control.
- [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md):
  each call and result retains exact run, snapshot/context, source, model,
  prompt, schema, and reuse provenance.
- `AI-003`, `AI-004`, `AI-006`, and `AI-007`: context is minimized, user-owned
  usage is bounded, exact invocation provenance is retained, and no account or
  provider fallback occurs.
- `DOC-006` and `DOC-010`: source authority is registry-controlled and broader
  community web search is opt-in.
- `SEC-001` and `SEC-003`: retrieved text and model output are untrusted data
  and cannot grant privileged operations.
- `ANALYSIS-017`: high-volume local state becomes typed candidates before
  expensive semantic investigation.

## 2. Scope, non-scope, and method

### 2.1 In scope

- Current direct OpenAI API capabilities relevant to Infinium.
- Responses API, Structured Outputs, hosted web search, background Responses,
  Batch, prompt caching, usage/rate/cost surfaces, retention, cancellation,
  and model identity.
- An OpenAI-first capability-profile and invocation-receipt design.
- The boundary between web discovery and normal source acquisition.
- Changes needed to the RESEARCH-0027/0028 recommendations.
- Architecture and evaluation implications, without choosing the desktop
  stack, storage engine, SDK, or credential-store implementation.

### 2.2 Explicit non-scope

- Selecting a specific production model or price point.
- Live, authenticated, or paid OpenAI conformance.
- Provider quality, latency, or cost benchmarks.
- Implementing an adapter or prompt.
- Secure credential storage, owned by RQ-018.
- Concurrent hard-budget reservation and reconciliation, owned by RQ-034.
- General source-policy approval, owned by the source registry and RQ-010.
- OpenAI hosted file search/vector stores, code interpreter, shell, computer
  use, remote MCP, image generation, or unrestricted custom tool access.
- A requirement to implement Anthropic or any second provider in M1.

### 2.3 Research effects

| Item | Treatment |
|---|---|
| Local private data | Not accessed |
| Provider credential | Not accessed or required |
| Paid inference | Not performed |
| OpenAI sources | Current official developer documentation and OpenAPI only |
| Broad web search | Not used for OpenAI claims |
| Workspace writes | This report only |
| Architecture acceptance | Not performed |

## 3. Current official OpenAI sources

All sources were retrieved or rechecked on **2026-07-28**.

| ID | Official source | Exact relevance |
|---|---|---|
| O1 | [Migrate to the Responses API](https://developers.openai.com/api/docs/guides/migrate-to-responses) | OpenAI recommends Responses for new projects; typed items, state choices, tools, Structured Outputs, storage, and response model identity |
| O2 | [Responses API reference](https://developers.openai.com/api/reference/resources/responses) and OpenAPI `2.3.0` | `/v1/responses`, typed output, usage, `store`, `background`, `max_tool_calls`, `include`, requested/returned model, retrieval, and cancellation |
| O3 | [Structured model outputs](https://developers.openai.com/api/docs/guides/structured-outputs) | `text.format`, strict JSON Schema subset, refusal/incomplete handling, closed/required object rules, and schema limits |
| O4 | [Web search](https://developers.openai.com/api/docs/guides/tools-web-search) | Current `web_search` tool, actions, citations, complete source list, domain filters, live-access control, context size, longer search, and display requirement |
| O5 | [Background mode](https://developers.openai.com/api/docs/guides/background) | Asynchronous Responses, polling, cancellation, resumable streams, synchronous-abort limit, and temporary storage |
| O6 | [Batch API](https://developers.openai.com/api/docs/guides/batch) | Responses batches, 24-hour window, separate rate pool, discounted pricing, partial completion, cancellation, file lifecycle, and result ordering |
| O7 | [Prompt caching](https://developers.openai.com/api/docs/guides/prompt-caching) | Exact-prefix behavior, eligibility, breakpoints, measured cache reads/writes, current retention, and current cache-write pricing |
| O8 | [Rate limits](https://developers.openai.com/api/docs/guides/rate-limits) | Organization/project/model scopes, rate headers, separate batch limits, usage tiers, and retry/backoff |
| O9 | [Data controls](https://developers.openai.com/api/docs/guides/your-data) | Training default, abuse-monitoring retention, Responses application state, `store=false`, ZDR, background, prompt-cache, Batch, and third-party-tool data handling |
| O10 | [Usage and Costs API example](https://developers.openai.com/cookbook/examples/completions_usage_api) | Administrative key requirement and organization usage/cost aggregation |
| O11 | [Safety in building agents](https://developers.openai.com/api/docs/guides/agent-builder-safety#prompt-injections) | Prompt-injection risk when untrusted content can influence tool use or data flow |

Current documentation and provider capabilities are volatile. Every eventual
provider capability record must retain the documentation/OpenAPI identity and
retrieval time used to qualify it.

## 4. Verified capability findings

### 4.1 Responses is the correct initial API surface

Verified facts:

- OpenAI recommends Responses for new projects.
- A Response contains typed output items rather than only one message shape.
- It supports Structured Outputs, native web search, background execution,
  streaming, provider-managed state, and stateless use with `store: false`.
- The response contains provider model identity and per-response usage.
- `max_tool_calls` can bound the total built-in tool calls processed within
  one Response.

Recommendation:

- Use `/v1/responses` as the only initial OpenAI generation surface.
- Do not build new Infinium behavior on Chat Completions or the deprecated
  Assistants path.
- Parse typed items explicitly. Do not flatten messages, tool calls,
  citations, refusals, incomplete states, and usage into one text result.

### 4.2 Structured Outputs is necessary but not sufficient

Verified facts:

- Strict Structured Outputs adheres to a supported JSON Schema subset.
- Root schemas must be objects; all fields are required; objects require
  `additionalProperties: false`.
- Refusals and incomplete responses remain separate conditions that the
  application must handle.
- The schema subset has size, nesting, keyword, and model-compatibility limits.

Consequences:

- Retain the RESEARCH-0027 closed source-extraction and investigation result
  shapes as Infinium-owned semantic schemas.
- Compile them directly to the exact supported OpenAI schema for the qualified
  model.
- Continue all host-side identifier, citation, applicability, authority,
  taxonomy, and evidence-threshold validation. Schema adherence proves shape,
  not truth or entailment.
- Preserve raw provider output, refusal/incomplete state, validator outcome,
  admitted proposals, and rejected proposals separately.

### 4.3 Native web search provides a valuable OpenAI-only capability

Verified facts:

- New integrations use Responses `web_search`; the earlier
  `web_search_preview` lacks newer controls.
- A web-search response can include:
  - typed `web_search_call` items;
  - action types for search, and on reasoning models page opening or
    find-in-page;
  - searched queries when returned;
  - inline URL citations with title and location;
  - the complete consulted URL list through
    `include: ["web_search_call.action.sources"]`.
- `filters` can constrain allowed or blocked domains.
- `external_web_access` distinguishes live access from cache/index-only use.
- `search_context_size` controls a coarse amount of result context, not an
  exact source or token count.
- reasoning search can run several searches; longer runs can use background
  mode and a larger returned-token budget.
- web-search calls have a separate tool-call cost.
- if web-derived information is displayed, inline citations must be visible
  and clickable.

This capability directly supports Infinium's goal of discovering obscure,
documented interactions. Requiring a second provider to match it before use
would weaken the product for no compensating safety benefit.

### 4.4 Web-search output is discovery provenance, not acquired source truth

The following is an **architectural inference** from O2/O4 plus Infinium's
accepted evidence model:

- OpenAI returns URLs, titles, actions, citations, and synthesized model text.
- Those objects do not supply the exact canonical landing-page bytes,
  source-controlled revision/validator, deterministic passage offsets,
  complete author-role adjudication, or Infinium's source-policy decision.
- Therefore they cannot satisfy `DOC-008`, `DOC-011`, `EVID-002`, or the
  RESEARCH-0027 citation contract by themselves.

Required separation:

```text
OpenAI web search
  -> WebDiscoveryRecord + WebLead
  -> registry classification and URL normalization
  -> approved landing-source adapter acquisition
  -> exact revision/body/passage fingerprinting
  -> claim extraction
  -> local applicability and investigation
```

If Infinium cannot lawfully or technically acquire a cited landing source:

- retain the search item as a provider-originated investigative lead;
- preserve its URL/title/citation, query/action, model, and retrieval
  provenance;
- expose the acquisition gap; and
- do not promote the search summary into an author-maintained or curated
  external claim.

Search rank, citation, repeated model selection, or a source-looking domain
does not grant authority. The source registry still decides authority by
source identity, author role, claim type, and acquired content.

### 4.5 Background Responses fit long single investigations

Verified facts:

- Background mode starts an asynchronous Response that can be polled through
  queued/in-progress to a terminal state.
- A background Response can be cancelled through a provider endpoint.
- a stream may be resumed from a sequence cursor only when created with both
  background and streaming enabled.
- synchronous cancellation is only connection termination.
- background mode temporarily stores response data to enable polling,
  including under `store=false`; the current documentation describes roughly
  ten minutes for that temporary disk state.

Recommendation:

- Use ordinary Responses for short extraction/investigation tasks.
- Permit background mode only for one explicitly long investigation or
  research operation whose storage/cancellation declaration is shown before
  dispatch.
- Map “cancel requested” and terminal provider cancellation separately.
- Keep the local run/checkpoint authoritative; a provider Response ID is only
  invocation provenance.

### 4.6 Batch fits independent bulk work, with different semantics

Verified facts:

- Batch accepts `/v1/responses` request populations.
- The current service advertises a 24-hour completion window, a separate rate
  pool, and a 50% pricing discount relative to synchronous requests.
- One batch can currently contain up to 50,000 requests in a file up to
  200 MB.
- output order is not guaranteed; `custom_id` is the correlation key.
- cancellation may spend up to ten minutes in `cancelling`; completed work and
  partial output remain.
- expired/cancelled batches can still contain billable completed requests.
- Batch uploads and outputs introduce provider-side file/application state;
  the current guide says output files are automatically deleted after 30
  days, while data-control records must still be treated as the operative
  retention authority for the exact feature/account configuration.

Recommendation:

- Keep Batch optional and configuration-visible, not the default for
  interactive or targeted work.
- It is promising for thousands of independent claim-extraction or
  candidate-investigation requests in an exhaustive run after exact cost,
  retention, and cancellation conformance.
- Never put dependent agent steps into one unordered batch without explicit
  orchestration.
- Reserve budgets per batch item before submission and reconcile every
  terminal/partial item under RQ-034.

### 4.7 Prompt caching is an optimization, not semantic reuse

Verified facts:

- caching requires exact prompt-prefix matches and is automatic for eligible
  prompts; current eligibility begins at 1,024 tokens.
- static instructions, schemas, tools, and repeated evidence prefixes can
  participate.
- OpenAI reports cache reads and, for current later model families, cache
  writes in the usage object.
- current later-model behavior can charge a higher rate for cache writes while
  discounting reads.
- cache retention depends on model and account policy; current later-model
  controls express a minimum cache lifetime and cached state may remain longer.
- caching does not make output deterministic.

Recommendation:

- Put stable instruction/schema/taxonomy material before dynamic evidence.
- Use a versioned cache key that never crosses source-policy, provider profile,
  prompt/schema, or semantic-context boundaries.
- Measure cache writes, reads, latency, and net cost before enabling explicit
  breakpoints by default.
- Treat provider prompt-cache state as a provider retention capability, not an
  Infinium reusable analytical artifact.
- Do not place restricted source bytes into a cacheable prefix unless their
  provider-transmission and effective provider-retention decisions allow it.

### 4.8 Usage, cost, limits, and “remaining usage” stay distinct

Verified facts:

- a completed Response reports input, output, reasoning, and cache token
  details where applicable.
- web search has separately countable/priceable tool calls.
- HTTP headers can report live request/token rate-window limits, remaining
  headroom, and reset time.
- organization/project/model limits differ; Batch uses a separate queue/rate
  pool.
- OpenAI exposes organization usage/cost administrative APIs and the current
  OpenAPI includes organization/project spend-limit administration.
- administrative usage/cost access requires a separately privileged
  organization Admin key.
- the inspected official endpoint inventory exposes no prepaid-credit-balance
  read operation.

Consequences:

- Always retain per-attempt provider usage and web-search call counts.
- Estimate cost from a versioned local price catalog before dispatch, reconcile
  actual usage from the response, and derive final cost from the catalog
  version that applied to the invocation.
- Make organization historical usage/cost and configured spend-limit reads an
  optional, separately authorized administrative feature.
- Do not require an Admin key for inference.
- Show rate headroom, configured limit, historical spend, local run budget,
  and prepaid balance as different fields.
- Report prepaid balance as `not_supported` unless a later documented API
  supplies it. Do not synthesize it as limit minus spend.

### 4.9 Retention is feature-specific

Verified current documentation:

- API data is not used for model training by default unless the customer opts
  in.
- default abuse-monitoring logs may contain customer content for up to 30
  days, subject to documented exceptions.
- Responses are stored for at least 30 days by default or with `store=true`;
  `store=false` disables normal Responses application-state storage.
- approved ZDR changes applicable request behavior but is not assumed for
  ordinary personal API accounts.
- background, Batch, Files, prompt caching, and third-party/network tools have
  their own application-state or external-retention consequences.
- live web search involves external web access and must be treated as a
  different effective capability from cache/index-only search.

Recommendation:

- Default stateless claim extraction and candidate investigation to
  `store=false`.
- Do not use Conversations or `previous_response_id` for M1 analytical truth.
- Preserve the exact request, response, tool items, validation, and usage in
  Infinium's permitted local run record.
- Compute and display an effective retention declaration for every invocation
  from provider profile, endpoint, mode, tools, files, cache settings, and
  account controls.

## 5. Proposed OpenAI-first operation set

| Operation | OpenAI capability | Model authority | Initial tool access | Output role |
|---|---|---|---|---|
| `extract_source_claims` | Responses + Structured Outputs | Propose source-bound claims only | None | Untrusted claim proposals, abstentions, and gaps |
| `investigate_candidate` | Responses + Structured Outputs | Propose hypotheses, contradictions, symptoms, and recommendations from supplied evidence | None | Untrusted hypothesis/recommendation proposals |
| `discover_web_sources` | Responses + `web_search` | Select/search candidate URLs within host policy | Hosted web search only | Discovery records and investigative leads |
| `research_web_lead` | reasoning Responses + `web_search`; background optional | Expand one admitted lead under a fixed query/source/budget envelope | Hosted web search only | Lead synthesis plus full search provenance; never authoritative claim by itself |
| `synthesize_case_explanation` | Responses + Structured Outputs or bounded text | Explain already admitted evidence/findings without changing them | None | User-facing prose linked to immutable domain IDs |

The first two retain the semantic safety of RESEARCH-0027. The latter three are
OpenAI-capability operations and are not required to have portable provider
equivalents.

Every operation must declare:

- exact provider capability profile and model qualification;
- local operation/run ID and immutable input hash;
- model-visible context and removal/redaction policy;
- allowed tools, tool choice, domain filters, live-access mode, and maximum
  tool calls;
- maximum output tokens, time, calls, and reserved cost;
- storage/background/batch/cache mode and effective retention;
- requested and returned model identity;
- raw provider item identities and terminal/cancellation state;
- provider usage, search calls, derived cost, and later reconciliation; and
- validation/admission result plus resulting coverage/gaps.

## 6. Governed web-search design

### 6.1 Two search lanes

#### Approved-domain discovery

Purpose:

- find author-maintained documentation, official project documentation,
  technical documentation, or curated sources already represented by an
  active registry entry.

Controls:

- domain allowlist generated from active source-registry records;
- exact mod/source identity and supported version terms;
- minimal query context;
- bounded calls and returned-token budget;
- landing acquisition only through the registered adapter.

This lane can find potentially authoritative material, but search output itself
still has investigative authority until the landing source is acquired and
adjudicated.

#### Broader community discovery

Purpose:

- find obscure interactions, bug patterns, or symptom reports not represented
  in approved primary sources.

Controls:

- separately visible opt-in under `DOC-010`;
- explicit query/privacy/cost preview;
- blocked-domain and safety rules where appropriate;
- every result initially community/investigative;
- no readiness effect without later independent corroboration or strong local
  evidence.

### 6.2 Search input minimization

Prefer:

- public mod/project names and source identifiers;
- versions relevant to the candidate;
- a bounded interaction or symptom;
- technical terms derived from typed local evidence.

Exclude unless demonstrably necessary:

- usernames;
- absolute paths;
- the whole modlist;
- unrelated installed mods;
- credentials, tokens, or account identifiers;
- raw local logs;
- private notes or symptoms outside the targeted candidate.

### 6.3 Required discovery record

```text
WebDiscoveryRecord {
  acquisition_or_analysis_run_id
  operation_id
  provider_profile_id
  capability_snapshot_id
  requested_model_id
  returned_model_id
  request_id
  query_scope_and_input_hash
  configured_domain_filters
  external_web_access
  search_context_size
  returned_token_budget
  max_tool_calls
  web_search_call_ids
  actions_and_returned_queries
  all_returned_sources
  inline_url_citations
  provider_response_text_ref
  usage_and_cost_refs
  retrieved_at
  landing_acquisition_links[]
  unresolved_or_rejected_sources[]
}
```

OpenAI character-index citations belong to the provider response text. They do
not become offsets into the landing source.

### 6.4 Display and citation behavior

- If provider web-search prose is shown, preserve clearly visible, clickable
  inline citations as OpenAI requires.
- Show provider search synthesis as a lead, not as source-authored prose.
- A later extracted claim cites exact acquired source passages, not the
  provider response.
- Preserve the complete consulted source URL list even when only some URLs
  receive inline citations.
- Redirects, canonicalization, source-registry match, landing acquisition,
  author role, and content revision remain host-side records.

## 7. Provider-independent truth and provider-specific capability

### 7.1 Stable domain boundary

These remain provider-independent:

- source, entity, revision, passage, and policy identity;
- observation, deterministic result, claim, candidate, hypothesis, finding,
  recommendation, coverage gap, case, and readiness;
- taxonomy, applicability, authority, confidence, severity, and maturity;
- installation snapshot, analysis context, run, and dependency validity;
- validation/admission and source-acquisition semantics.

### 7.2 OpenAI-specific invocation boundary

These may remain OpenAI-specific:

- Response/item IDs and item unions;
- `web_search_call`, action, source, and URL-citation shapes;
- `background`, polling, stream cursors, and response cancellation;
- Batch objects, files, states, and `custom_id`;
- prompt-cache keys, breakpoints, read/write token classes, and retention;
- service tier;
- provider rate headers, administrative usage/cost, and spend-limit surfaces;
- storage/ZDR declarations; and
- model-specific schema/tool/reasoning capabilities.

### 7.3 Capability profile rather than lowest-common-denominator adapter

```text
ProviderCapabilityProfile {
  provider_id
  direct_api_identity
  verified_at
  documentation_or_spec_identity
  authentication_and_billing_scope
  supported_models[]
  semantic_operations[]
  provider_specific_operations[]
  structured_output_profile
  hosted_tools[]
  sync_background_batch_modes[]
  cancellation_semantics
  usage_cost_and_limit_capabilities
  storage_retention_and_deletion
  capability_gaps[]
}
```

A future provider adapter may implement:

- the two semantic operations only;
- its own search/research operation;
- no background or Batch mode;
- a different citation or telemetry envelope.

The UI and scan configuration must disclose those differences. The product
must not silently emulate a missing capability with a weaker provider or
rewrite one provider's object as another's.

## 8. Tool and security boundary

### 8.1 Initial allowed model-selected tool

Only OpenAI hosted `web_search` is recommended for initial model-selected tool
use, and only inside the two governed discovery operations.

### 8.2 Initial prohibited model tools

- Infinium internal function tools;
- filesystem access;
- shell or code interpreter;
- computer use;
- remote MCP/connectors;
- generic HTTP/page-fetch tools;
- MO2, LOOT, game, or process access;
- credential, settings, or data-store operations;
- write-capable or remediation actions.

Source acquisition is an orchestrator-controlled adapter operation, not a tool
the model can invoke or parameterize after reading hostile content.

### 8.3 Prompt-injection consequence

Official OpenAI guidance identifies untrusted-data prompt injection as a route
to private-data exfiltration or unintended downstream tool calls. Infinium's
initial design reduces that path because:

- source text enters only schema-bound operations with no tools; or
- web-search operations receive no privileged local tools or secrets;
- the host fixes the operation, source policy, domain filters, context,
  maximum calls, and budget before dispatch;
- output cannot grant a new tool or authority; and
- landing acquisition and claim admission remain deterministic host actions.

This reduces but does not prove elimination of search-result manipulation.
Adversarial provider conformance remains required.

## 9. Execution, cost, and replay recommendations

### 9.1 Default mode

- `store=false`;
- stateless request with exact retained local input package;
- no model tools for extraction/investigation/explanation;
- hosted web search only for explicit discovery operations;
- synchronous request unless predicted duration warrants background;
- local prompt/schema and price-catalog version;
- requested and returned model identity retained;
- strict local admission after provider completion.

### 9.2 Background

Use when one request may take minutes or needs provider-side polling. Retain:

- response ID;
- queued/in-progress/terminal history;
- cancel-request and terminal-cancel state;
- stream cursor if used;
- effective temporary-storage disclosure; and
- usage/cost uncertainty until a terminal receipt is obtained.

### 9.3 Batch

Use only after:

- exact selected-model and Structured Output conformance;
- item-level reservation/reconciliation;
- input/output file retention and deletion controls;
- result correlation by `custom_id`;
- partial/cancelled/expired coverage semantics; and
- explicit user choice accepting asynchronous completion and different
  retention.

### 9.4 Replay

- Exact local replay can reuse a retained provider response without recalling
  OpenAI.
- Clean model recomputation is a new invocation, not a replay guarantee.
- A web-search rerun is live evidence acquisition and may return different
  actions, sources, and prose.
- Deleted or unavailable provider/source content must downgrade replayability
  and auditability explicitly.

## 10. Alternatives

| Alternative | Benefit | Failure or cost | Disposition |
|---|---|---|---|
| Keep RESEARCH-0027 as the maximum universal feature set | Easy portability | Excludes useful OpenAI search/background/Batch behavior and makes provider choice determine product scope negatively | Reject |
| Put OpenAI Response objects directly in findings/cases | Maximum fidelity | Provider transport becomes domain truth and future adapters cannot coexist cleanly | Reject |
| OpenAI-first capability profile with provider-independent domain truth | Uses current features without contaminating evidence semantics | Requires explicit capability-aware UI, tests, and invocation records | **Recommend** |
| General external search API before OpenAI web search | Search-provider neutrality | More integration/policy/credential work before proving product value | Defer |
| Treat web-search citations as acquired evidence | Low implementation effort | No exact landing bytes/revision/passage policy; false source authority | Reject |
| Give the model a generic landing-page fetch function | Flexible | Prompt-injection and source-policy boundary become model-controlled | Reject for M1 |
| Use only synchronous requests | Simple | Poor fit for long research and high-volume exhaustive work | Keep as default, not exclusive |
| Use Batch for every scan | Lower nominal cost | 24-hour semantics, provider files, partial completion, slower feedback, and cancellation complexity | Reject as default; qualify as optional |

## 11. Uncertainty and limitations

1. No live credential verified Responses, Structured Outputs, web search,
   background, Batch, headers, usage/cost, or cancellation for the owner's
   account.
2. No specific production model is selected or qualified.
3. Official documentation does not make web-search citations equivalent to
   exact landing-source passages; the separate-acquisition requirement is an
   Infinium trust-model inference.
4. Search coverage, ranking, query rewriting, source recall, source freshness,
   and citation correctness require empirical evaluation.
5. Domain filters do not prove author identity or source authority.
6. Provider model/tool availability, prices, rate limits, caching, retention,
   and API fields can change and need dated capability revalidation.
7. Administrative usage/cost/spend-limit access may be unavailable to the
   user's account or may require an undesirably privileged key.
8. No documented prepaid-credit-balance API was found in the inspected
   official endpoint inventory; absence must be rechecked before
   implementation.
9. `max_tool_calls` bounds processed built-in calls but is not by itself a
   complete dollar/token/time hard limit.
10. Background and Batch cancellation do not imply that no completed work was
    billed.
11. Prompt-cache economics differ across current model families and may
    reverse an assumed cost saving.
12. A later non-OpenAI provider may require different schemas, refusal
    handling, search provenance, or no equivalent provider-specific operation.

## 12. Recommendation

Confidence: **High** in the OpenAI-first boundary and the separation of
provider capability from domain truth; **high** that hosted web search is
useful only as governed discovery until landing acquisition; **medium** in the
exact operational defaults pending live conformance, quality evaluation, and
cost benchmarks.

Recommend that the owner accept:

1. OpenAI Responses as the only initial LLM API surface.
2. OpenAI as the only required M1 provider; no second-provider adapter or
   portability test gates M1.
3. Provider-independent domain/evidence contracts, but capability-profiled,
   provider-specific operations and invocation envelopes.
4. The RESEARCH-0027 claim-extraction and candidate-investigation contracts as
   the safe semantic subset, not the whole LLM interface.
5. `discover_web_sources` and `research_web_lead` as explicit OpenAI
   provider-specific operations using only hosted `web_search`.
6. Separate landing-source acquisition and passage extraction before any web
   result gains ordinary source authority.
7. `store=false` stateless calls as the default, with background and Batch
   separately qualified and disclosed.
8. Optional prompt caching only after measured economics and retention review.
9. Per-attempt usage/search-call tracking and local estimates; optional
   separately privileged administrative cost/limit telemetry; no invented
   prepaid balance.
10. Exact capability and model qualification records with current-doc
    revalidation before implementation and release.

## 13. ADR and requirement changes enabled

### 13.1 Amend AI-001

The current title and wording, “Provider-neutral contract,” are too broad under
the owner's revised direction.

Proposed replacement concept:

> Infinium's authoritative domain, evidence, finding, case, coverage, and
> readiness contracts shall remain provider-independent. The initial supported
> LLM implementation shall target OpenAI and may expose OpenAI-specific
> operations, tools, execution modes, and provenance when they improve product
> value and satisfy the evidence, safety, privacy, cost, and evaluation
> requirements. Later providers may implement different declared capability
> profiles; lowest-common-denominator parity is not required.

### 13.2 Proposed ADR: OpenAI-first LLM capability boundary

The ADR should accept:

- Responses as the initial API;
- user-owned direct OpenAI API credentials and no provider/account fallback;
- provider-independent domain truth;
- the two safe semantic operations;
- the two governed web-search operations;
- OpenAI-specific capability profiles and invocation receipts;
- Structured Output plus host validation;
- initial no-tool operations versus web-search-only operations;
- stateless default, background/Batch/cache qualification rules;
- model/capability revalidation; and
- deferred later-provider expansion without M1 portability gating.

It must not implicitly select:

- credential storage;
- hard-budget reservation;
- desktop stack or process topology;
- database schema;
- production model;
- arbitrary model tools; or
- a source adapter or landing-page permission.

### 13.3 RESEARCH-0027/0028 disposition

- Preserve both reports as dated evidence.
- Supersede RESEARCH-0027's recommendation that the provider-neutral
  two-operation surface is the complete LLM boundary.
- Preserve its two operations and admission/citation invariants.
- Preserve RESEARCH-0028's authentication, billing-scope, capability-snapshot,
  invocation-receipt, rate/cost/cancellation, and retention findings.
- Supersede its requirement for an initial portability-constraining provider
  abstraction or second-provider conformance gate.

## 14. Evaluation inputs

Existing cases should be specified or amended to verify:

| Case | Revised OpenAI-first input |
|---|---|
| `EVAL-0010` | A Structured Output claim proposal resolves only to host-supplied exact spans and source revision. |
| `EVAL-0011` | Authoritative-looking web search output cannot satisfy local applicability or source authority without acquired evidence. |
| `EVAL-0012` | Missing landing acquisition, version, or exact passage produces a lead/gap/abstention rather than a claim or finding. |
| `EVAL-0033` | Hostile source/search content cannot obtain internal tools, secrets, source authority, or operation authority. |
| `EVAL-0034` | Web queries and model payloads omit credentials, usernames, paths, unrelated mods, and unnecessary local state. |
| `EVAL-0064` | Local-only runs remain provider-free. A future provider exposes its own declared capabilities without being required to emulate OpenAI web search. |
| `EVAL-0067` | Response items, web-search calls, citations, model prose, admitted claims/hypotheses, and findings remain distinct. |
| `EVAL-0068` | Search discovery, landing acquisition, extraction, and local application are separately provenanced; no page adapter means an explicit gap. |
| `EVAL-0076` | UI distinguishes response usage, search-call usage, rate headroom, configured spend limit, historical cost, local budget, and unsupported credit balance. |
| `EVAL-0077` | Only the selected user-owned OpenAI credential/project may dispatch work. |
| `EVAL-0081` | sync abort, background cancel, Batch cancel/expiry, partial results, and delayed usage do not release budget incorrectly. |
| `EVAL-0083` | End-to-end provenance resolves exact provider capability, request/model, search actions/sources, landing acquisition, passages, validation, and application. |

Additional bounded cases are needed for:

- allowed/blocked-domain enforcement;
- complete-source-list retention versus inline citation subset;
- redirect/canonical URL mapping;
- search result whose landing page is unavailable;
- misleading/high-ranking community result;
- author-looking page with unverified authorship;
- live versus cache/index-only search;
- `max_tool_calls` exhaustion;
- background resume/cancel;
- Batch out-of-order and partial output;
- prompt-cache read/write accounting; and
- requested/returned model identity drift.

## 15. Suggested RQ status text

### RQ-011

> **Researched; OpenAI-first semantic and tool boundary proposed.** Preserve
> provider-independent domain truth and the safe source-extraction/candidate-
> investigation contracts, but do not use portability as a ceiling. OpenAI-
> specific governed web discovery, background, Batch, caching, and provenance
> may be exposed through a capability profile. Resolution requires owner/ADR
> acceptance and later operation/model conformance.

### RQ-012

> **Researched; OpenAI selected as the proposed initial provider.** Responses,
> Structured Outputs, hosted web search, background execution, optional Batch,
> prompt caching, per-response usage, rate headers, and optional
> administrative telemetry form the proposed capability set. No second
> provider or lowest-common-denominator parity gates M1. Exact credential,
> model, mode, retention, cancellation, usage/cost, and adversarial conformance
> remain implementation prerequisites.

## 16. Semantic self-review

- OpenAI capability does not become source authority or local-state authority.
- Search citations are not mislabeled as exact landing-source passages.
- Landing acquisition remains host-controlled and source-registry-governed.
- The two RESEARCH-0027 operations are retained rather than discarded.
- Provider independence is narrowed at the correct boundary instead of removed
  from findings/evidence.
- OpenAI-specific features remain outside canonical domain records.
- Background, Batch, and sync cancellation remain distinct.
- Provider usage, search-call cost, historical cost, rate headroom, spend
  limit, local budget, and prepaid balance remain distinct.
- No undocumented consumer login, credit-balance API, or exact replay
  guarantee is invented.
- No paid or authenticated provider conformance is claimed.
- No specific model, SDK, stack, storage, credential mechanism, search-result
  authority, or landing-source permission is selected.
