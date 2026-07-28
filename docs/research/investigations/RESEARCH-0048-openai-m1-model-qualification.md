# RESEARCH-0048: OpenAI M1 model and synchronous Responses qualification

Status: Completed

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary RQ: RQ-038

Related RQs: RQ-011, RQ-012, RQ-034, RQ-037

M0 wave: F — Evaluation specifications, deferred-question ledger, and M1 plan

Decision enabled: Exact M1 OpenAI model and execution-profile ADR

Acceptance: Recommendation accepted by the project owner through ADR-0025 on
2026-07-28

## Executive answer

M1 should qualify one deliberately narrow live-model profile:

- direct, synchronous `/v1/responses` calls;
- explicit model ID `gpt-5.6-sol`, not the moving `gpt-5.6` family alias;
- explicit `reasoning.effort: medium`;
- strict Structured Outputs through `text.format`;
- `store: false`, no provider tools, no background mode, no Batch, no
  conversation object, no `previous_response_id`, and no automatic fallback;
- explicit `service_tier: "default"`, with streaming disabled for the first
  proof;
- per-operation finite input, output, call, elapsed-time, and dollar
  reservations fixed in the accepted evaluation configuration before the first
  paid request; and
- exact local retention of the rendered request, raw response, returned model
  identity, requested and returned service tier, usage, provider request ID
  where available, prompt/schema/settings digests, price snapshot, and
  admission result.

This is a quality-first semantic proof, not the eventual production router.
Using one model and one explicit reasoning setting reduces experimental
variables while M1 establishes whether the two schema-bound semantic
operations work at all. Lower-cost model tiers and lower reasoning effort
should be compared only after a passing Sol baseline, using the same cases and
without changing prompts at the same time.

OpenAI currently lists `gpt-5.6-sol` as both the model ID and its only
available snapshot. No date-pinned Sol snapshot is documented. Infinium
therefore cannot promise reproducible re-execution from provider inputs alone.
It must preserve the original response for exact replay, record the returned
model identity and documentation/capability revision, and treat any material
model/capability drift as an evaluation invalidation that requires
requalification.

## 1. Question and requirements

RQ-038 asks:

> Which exact OpenAI model identity and synchronous Responses profile should
> M1 qualify for its two semantic operations, and how must Infinium handle
> capability drift when no immutable model snapshot is available?

The answer must preserve:

- ADR-0001's bounded LLM authority;
- ADR-0002's snapshot, configuration, provenance, and replay separation;
- ADR-0013's direct OpenAI Responses and Structured Outputs boundary;
- ADR-0020's one-shot credential/provider helper;
- ADR-0023's finite reservation, dispatch fence, one-owned usage, and honest
  settlement;
- ADR-0024's rejection of Codex/ChatGPT-plan execution; and
- M1's need for a small, reviewable semantic proof rather than an unbounded
  production scan.

## 2. Scope and non-scope

This investigation selects the initial M1 live-call profile. It does not:

- claim that either semantic operation passes its evaluation cases;
- choose production model routing or cost/quality presets;
- qualify Terra, Luna, Pro mode, provider tools, hosted search, prompt
  caching, background mode, Batch, conversation state, persisted reasoning,
  or another provider;
- authorize a paid request; or
- define immutable provider behavior where OpenAI supplies no immutable
  snapshot.

## 3. Current official evidence

Official OpenAI documentation was retrieved on 2026-07-28.

| Source | Material result |
|---|---|
| [Using GPT-5.6](https://developers.openai.com/api/docs/guides/latest-model?model=gpt-5.6) | `gpt-5.6-sol` is the current flagship model; the `gpt-5.6` alias routes to it. OpenAI recommends the Responses API for reasoning and tool-calling work and explicit reasoning settings. |
| [GPT-5.6 Sol model page](https://developers.openai.com/api/docs/models/gpt-5.6-sol) | The model supports Responses and Structured Outputs, has a 1,050,000-token context window, a 922,000-token maximum input, and 128,000-token maximum output. Its snapshot list contains only `gpt-5.6-sol`, so no date-pinned identity is currently available. |
| [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs) | Responses supports strict JSON-schema output through `text.format`; schema adherence is stronger than JSON mode and is the recommended path when available. |
| [Responses create reference](https://developers.openai.com/api/reference/resources/responses/methods/create) | The request can set model, instructions/input, maximum output tokens, structured text format, reasoning, service tier, storage, and other bounded options. |
| [API pricing](https://developers.openai.com/api/docs/pricing) | Standard short-context Sol pricing is currently $5.00/M input, $0.50/M cached input, and $30.00/M output; requests above 272K input use the documented long-context rates. Prices are mutable external facts and must be snapshotted, not hard-coded as timeless constants. |
| [OpenAI data controls](https://developers.openai.com/api/docs/guides/your-data) | API data is not used for training unless the customer opts in. Responses has default application-state retention when stored; `store: false` avoids that application-state path, while ordinary abuse-monitoring retention may still apply for up to 30 days unless the account has approved controls. |

No paid or authenticated model request was needed to establish the documented
capability boundary. Runtime availability, actual returned model identity,
schema behavior, usage fields, and failure semantics remain implementation
conformance checks.

## 4. Alternatives

### Use the `gpt-5.6` alias

Rejected for M1. It adds an avoidable routing alias when the explicit Sol ID is
available. The returned model identity must still be recorded even with the
explicit ID.

### Use Sol for both M1 semantic operations

Selected. It maximizes the probability that an early failure identifies a
prompt, evidence, schema, or product-boundary problem rather than a
cost-optimized model ceiling. One model also makes controlled comparison
easier.

### Route extraction to Luna or Terra immediately

Deferred. Extraction is a plausible lower-tier workload, but introducing a
router before the semantic contracts pass adds a second variable and a second
capability/price profile. Compare cheaper tiers after the accepted Sol
baseline passes.

### Use Pro mode or higher reasoning

Rejected for the baseline. It increases latency and cost and is not justified
without measured failures at the ordinary explicit `medium` profile.

### Use `none` or `low` reasoning

Deferred as an optimization treatment. M1 starts from explicit `medium`, then
may compare one lower setting against the unchanged passing baseline.

### Avoid live calls entirely in M1

Rejected. M1 is the backend semantic proof and must demonstrate the real
provider boundary as well as deterministic fixture paths. Offline behavior
remains independently required.

## 5. Required drift and replay behavior

M1 must separate:

1. **exact artifact replay**, which reuses the retained response and all
   admitted dependencies without a provider call; from
2. **live re-execution**, which is a new operation against current provider
   behavior and cannot be claimed equivalent merely because the request is
   identical.

Before every live evaluation session, the adapter records a capability/price
snapshot. A live result is not comparable to the accepted baseline without
review when any of these change materially:

- requested or returned model identity;
- supported endpoint or Structured Outputs behavior;
- reasoning or request-setting semantics;
- prompt or schema digest;
- input population or evidence revision;
- token accounting or price schedule; or
- adapter/SDK/request serialization.

If OpenAI later publishes a date-pinned compatible snapshot, adopting it
requires a reviewed amendment and evaluation rerun; it is not selected
silently.

## 6. Admission and failure contract

The M1 adapter must reject before dispatch when:

- the user has not selected the exact API access profile;
- the model or required Structured Outputs capability is unavailable;
- context or output bounds are not finite;
- the complete worst-case reservation does not fit every applicable hard
  limit;
- the rendered request, schema, or evidence manifest fails validation;
- offline mode is active;
- the immutable request assignment does not match the one-shot helper; or
- a fallback model, provider tool, background mode, or alternate service tier
  would be required.

Client cancellation or timeout is a local stop request, not proof that the
provider performed no billable work. Dispatched work remains reserved until
usage is settled or explicitly held unresolved under ADR-0023.

## 7. Recommendation

The project owner accepted ADR-0025 with the narrow profile above on
2026-07-28. The accepted M1 evaluation configuration owns operation-specific
input/output/call/time/money limits; the ADR owns the durable model/profile and
drift policy.

M1 should first pass non-live contract, credential, secret-canary, budget, and
failure-path cases. Its first paid request should then be a deliberately tiny
provider-transport qualification with a user-confirmed budget. If that gate
passes, M1 must separately execute one bounded live source-claim-extraction
request and one bounded live evidence-bound-candidate-investigation request,
each with an independent typed oracle, authorization, reservation, provenance,
and settlement. The qualification response cannot stand in for either
semantic operation. No M1 document should state that Sol, Structured Outputs,
or semantic quality conforms until the corresponding execution cases pass.

## 8. Confidence and remaining gates

- **High:** current official documentation identifies Sol and documents
  Responses plus Structured Outputs support.
- **High:** no date-pinned Sol snapshot is currently documented.
- **High:** `store: false` is the appropriate M1 application-state posture,
  but it does not imply zero provider retention.
- **Medium:** explicit `medium` is the best quality-first single-profile
  baseline. Its cost/quality advantage over `low`, Terra, or Luna requires
  evaluation.
- **Unproven:** actual schema adherence, refusal/incomplete behavior, returned
  model identity, usage accuracy, cancellation settlement, and semantic
  success for Infinium fixtures.

No evaluation case is passed by this investigation.
