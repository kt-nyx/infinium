# ADR-0025: M1 OpenAI model and synchronous Responses profile

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Context

ADR-0013 selects direct OpenAI Responses with Structured Outputs, ADR-0020
selects credential isolation, and ADR-0023 selects the budget authority. M1
still needs one exact, bounded live-call profile. Leaving the model as
“OpenAI” would make evaluation provenance and cost admission ambiguous.

OpenAI currently exposes `gpt-5.6-sol` as its flagship and as the only listed
Sol snapshot; no date-pinned Sol identity is documented. The decision must
therefore distinguish exact retained-result replay from non-reproducible live
re-execution.

## Decision drivers

- Maximize semantic-proof quality before optimizing price or throughput.
- Minimize simultaneous variables across the two M1 semantic operations.
- Preserve strict host-owned schemas, authority, budget, and provenance.
- Avoid aliases, fallback, provider tools, and hidden execution modes.
- Represent model drift honestly when no immutable snapshot exists.

## Considered options

### One explicit Sol profile for both M1 operations

This keeps the first proof quality-first and experimentally simple. It costs
more than lower tiers and does not establish the eventual production router.

### Immediate per-operation Sol/Terra/Luna routing

This may reduce later production cost, especially for extraction, but adds
model selection and multiple capability/price profiles before either semantic
operation has a passing baseline.

### Moving `gpt-5.6` alias

This is convenient but adds an unnecessary alias indirection to an already
non-date-pinned model.

### Deterministic-only M1

This avoids provider variability but fails M1's purpose of proving the bounded
semantic layer.

## Decision

M1 shall use:

- direct synchronous `/v1/responses`;
- explicit `gpt-5.6-sol`;
- explicit `reasoning.effort: medium`;
- strict Structured Outputs through `text.format`;
- `store: false`;
- explicit `service_tier: "default"` and non-streaming execution;
- no provider tools, background mode, Batch, conversation state, persisted
  reasoning, Pro mode, model alias, alternate provider, or fallback; and
- accepted per-operation finite input, output, call, elapsed-time, and dollar
  bounds before any paid dispatch.

The M1 live sequence consists of one minimal provider-transport qualification
request followed, only after it passes, by separate source-claim-extraction and
evidence-bound-candidate-investigation requests. Each request receives its own
authorization, reservation, operation-specific strict schema, retained result,
typed evaluation, and settlement. The qualification response is not semantic
evidence.

Every call retains the exact rendered request, raw response, requested and
returned model identity, requested and returned service tier, usage, request
ID where available, prompt/schema/settings digests, immutable
capability/price snapshot, and budget authorization/settlement provenance.

Because OpenAI provides no date-pinned Sol snapshot, exact replay means replay
of the retained result. Repeating the same request is a new live execution.
Material model/capability drift invalidates live-baseline comparability and
requires requalification. No silent fallback is permitted.

Sol at `medium` is the M1 baseline, not the permanent production default.
After it passes, lower effort and lower-cost family tiers may be compared one
variable at a time on the same accepted cases. Any production routing
selection requires a later reviewed decision or accepted implementation-plan
amendment.

## Consequences

### Positive

- The live semantic proof has one unambiguous execution profile.
- Early failures are less likely to be artifacts of premature model
  cost-optimization.
- Provider variability, retention, and replay limits remain visible.
- Cost optimization can use controlled treatment comparisons after a baseline
  exists.

### Negative

- Sol is the highest-cost ordinary GPT-5.6 tier.
- A moving, non-date-pinned model cannot provide deterministic live
  re-execution.
- M1 does not prove Terra, Luna, provider search/tools, or production routing.

### Risks and mitigations

- **Provider drift:** retain exact results and capability fingerprints; rerun
  evaluation before accepting a changed live baseline.
- **Unexpected cost:** require finite worst-case reservations, a deliberately
  small first request, and user confirmation under ADR-0023.
- **Provider retention misunderstood:** label `store: false` accurately and
  preserve OpenAI's separate abuse-monitoring disclosure.
- **Baseline becomes an accidental permanent default:** label it M1-only and
  require measured lower-cost comparisons before production routing.

## Requirements affected

- AI-001 through AI-007
- EVID-004 through EVID-007
- OPS-001
- SEC-001 through SEC-004
- SNAP-002, SNAP-003, SNAP-005, SNAP-006

## Validation

- EVAL-0033 through EVAL-0035 prove prompt-injection, credential, and
  operation-boundary controls.
- EVAL-0064 proves provider failure and honest partial behavior.
- EVAL-0067 proves structured-output provenance.
- EVAL-0081 proves synchronous reservation/settlement behavior.
- EVAL-0083 proves exact context, configuration, and model provenance.
- EVAL-0089 proves one-shot helper and no-fallback dispatch.
- EVAL-0067 and EVAL-0083 require actual live source-claim extraction and
  evidence-bound candidate investigation after the transport qualification
  gate.
- M1 semantic positive, negative, malformed, refusal/incomplete, and
  metamorphic cases must pass before the profile is described as qualified.

## References

- [RESEARCH-0048](../../research/investigations/RESEARCH-0048-openai-m1-model-qualification.md)
- OpenAI, [Using GPT-5.6](https://developers.openai.com/api/docs/guides/latest-model?model=gpt-5.6), retrieved 2026-07-28
- OpenAI, [GPT-5.6 Sol](https://developers.openai.com/api/docs/models/gpt-5.6-sol), retrieved 2026-07-28
- OpenAI, [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs), retrieved 2026-07-28
- OpenAI, [API pricing](https://developers.openai.com/api/docs/pricing), retrieved 2026-07-28
- OpenAI, [Data controls](https://developers.openai.com/api/docs/guides/your-data), retrieved 2026-07-28
