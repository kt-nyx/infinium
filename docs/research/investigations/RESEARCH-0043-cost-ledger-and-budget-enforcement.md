# RESEARCH-0043: Cost ledger and budget enforcement

Status: Completed

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary RQ: RQ-034

M0 wave: E — Architecture and stack selection

Decision enabled: Cost-ledger and hard-budget-enforcement ADR

Acceptance: Recommendation accepted by the project owner through ADR-0023 on
2026-07-28

## Executive answer

Infinium should enforce billable-work limits in the coordinator through an
**application-owned, append-only usage ledger plus transactionally maintained
budget projections in the accepted SQLite system of record**.

Before one provider request can start, one database transaction must:

1. identify the immutable operation, run, node, and attempt that will own the
   usage;
2. bind the exact provider profile and credential generation, capability
   snapshot, request envelope, execution mode, pricing catalog, and idempotency
   identity;
3. compute a finite worst-case vector for every configured consumptive
   dimension;
4. test the dispatch deadline and all applicable operation-node,
   evidence-acquisition-run, analysis-run, and locally configured
   provider/account scopes;
5. reserve that vector against every scope atomically; and
6. create the attempt and reservation records before credential resolution or
   transport begins.

Immediately before transport, a second short transaction must revalidate the
run state, coordinator fence, reservation, deadline, credential generation and
revocation epoch, and cancellation state, then record one dispatch
authorization. A crash or network failure after that point is potentially
billable unless the adapter can prove that no request bytes left the process.
Ambiguous usage retains the full reservation as an unresolved enforcement hold;
it is not released merely because the UI stopped waiting, a connection closed,
or cancellation was requested.

One provider receipt produces one owned usage entry. Reservation reconciliation
replaces the worst case with observed usage and a price-catalog calculation
where possible. Stage, acquisition, analysis, and account views roll that same
entry up through relationships; they do not copy it. Provider-reported usage,
Infinium's calculated cost, provider administrative historical cost, rate
headroom, configured spend limits, and prepaid credits remain different facts.
With the currently documented OpenAI surfaces, Infinium can hard-enforce a
**catalog-calculated-cost** dimension, not the provider's eventual exact billed
dollars. A separately named exact-billing hard limit must remain unavailable
unless a provider supplies both a finite price contract and sufficiently prompt
exact charge reconciliation.

For M1's **usage-priced Platform API mode**, use synchronous, stateless OpenAI
Responses only, at most one billable dispatch in flight, an explicit finite
`max_output_tokens`, no provider-selected tool except a separately bounded
hosted-web-search operation, and a qualified fixed pricing/service-tier
contract. Keep background Responses, Batch, and explicit prompt-cache behavior
disabled. This deliberately proves the hard limit and audit contract before
adding the cancellation and delayed-settlement states those modes require.

RESEARCH-0045 later investigated a distinct Codex/ChatGPT-plan candidate, but
the owner rejected that recommendation and ADR-0024. It does not amend this
report's direct Platform API budget contract.

## 1. Question and governing constraints

RQ-034 asks:

> Which deadline-check, atomic reservation, and reconciliation model can
> enforce concurrent operation/acquisition/analysis hard limits across
> providers, including elapsed-time deadlines, maximum-call bounds, batching,
> cancellation, rounding, delayed billing, and adapter capability gaps?

The answer must satisfy:

- `AI-004`: reserve a declared worst case against every applicable hard limit
  before billable work; reconcile observed usage; expose finite-bound gaps;
- `SCAN-003` and `SCAN-004`: estimates, live usage, cost, limits, and
  reconciliation limitations remain visible without duplicated ownership;
- `SCAN-005`: pause/cancel and detachment do not erase already dispatched or
  potentially billable work;
- `AI-005`: rate headroom, historical cost, spend limits, credits, and local
  budget are not conflated;
- `AI-006` and `SNAP-005`: exact request, receipt, price, budget, cache,
  execution, and concurrency configuration remains reproducible;
- `AI-007`: usage is bound to the selected user-owned provider/account;
- [RESEARCH-0037](RESEARCH-0037-job-checkpoint-and-run-lifecycle.md): one
  operation attempt owns each reservation and actual usage entry;
- [RESEARCH-0039](RESEARCH-0039-process-and-data-query-boundary.md): only the
  coordinator authorizes dispatch and owns the database;
- [RESEARCH-0040](RESEARCH-0040-credential-entry-and-storage.md): credential
  resolution is attempt-scoped and generation/revocation checked; and
- `EVAL-0044` and `EVAL-0081`: attached/detached accounting and concurrent
  reservation cannot duplicate, oversubscribe, or prematurely release budget.

This investigation proposes the logical mechanism. It does not accept SQLite,
the process topology, the credential mechanism, or this decision; each remains
subject to the integrated Wave E ADR review.

## 2. Scope and non-scope

### In scope

- consumptive calculated-money, token, call, and built-in-tool-call limits;
- dispatch deadlines and in-flight concurrency;
- operation-node, acquisition-run, analysis-run, and provider/account scopes;
- atomic multi-scope reservation;
- retries, idempotency, credential generation, and dispatch ambiguity;
- synchronous abort, background cancellation, Batch partial completion, and
  delayed reconciliation;
- versioned price calculation and exact integer/rational arithmetic;
- cache read/write uncertainty;
- child detachment and historical reuse;
- node/run/account exhaustion;
- durable audit and evaluation obligations; and
- the smallest defensible M1 subset.

### Out of scope

- choosing an exact OpenAI production model;
- treating a local cost estimate as provider billing truth;
- inventing a prepaid-credit API or provider-side transaction identifier;
- mutating provider organization/project spend limits;
- implementing a payment, subscription, or project-funded inference service;
- performance benchmarking without a representative implementation; and
- production code or a final physical migration.

## 3. Method and primary sources

Repository requirements and accepted/proposed Wave D/E research were reviewed
first. Current OpenAI documentation was retrieved on 2026-07-28 through the
official OpenAI developer-documents interface.

| Subject | Primary source | Material result |
|---|---|---|
| Responses request bounds | [Create a model response](https://developers.openai.com/api/reference/resources/responses/methods/create) | `max_output_tokens` bounds visible plus reasoning output; `max_tool_calls` bounds built-in tool calls across a Response; returned service tier may differ from requested `auto`. |
| Input tokens | [Counting tokens](https://developers.openai.com/api/docs/guides/token-counting) | The Responses input-token endpoint accepts the request shape and returns the exact input count, including protocol formatting, files, images, and tools. Local tokenizers can miss those components. |
| Synchronous/background cancellation | [Background mode](https://developers.openai.com/api/docs/guides/background) | Synchronous cancellation is connection termination; background work has pollable states and an idempotent cancel endpoint, but cancel request and terminal cancellation remain separate. |
| Batch lifecycle | [Batch API](https://developers.openai.com/api/docs/guides/batch) | Responses Batch supports item correlation, partial results, cancellation/expiry with completed billable work, separate queue limits, and a 24-hour completion window; cancellation may remain in progress for up to ten minutes. |
| Prompt-cache accounting | [Prompt caching](https://developers.openai.com/api/docs/guides/prompt-caching) | Cache reads and current-model cache writes are reported separately; later-model writes can cost more than ordinary input, hits are not guaranteed, and cached tokens still affect rate limits. |
| Price dimensions | [API pricing](https://developers.openai.com/api/docs/pricing) | Price varies by model, service tier, context band, input/cache class, output, Batch, region, and tool; price data must be versioned rather than embedded as one model rate. |
| Rate limits | [Rate limits](https://developers.openai.com/api/docs/guides/rate-limits) | Request/token windows, organization/project/model scopes, approved monthly usage, spend limits, and Batch queue limits are different controls; failed requests can consume rate capacity. |
| Administrative usage | [Completions usage API](https://developers.openai.com/api/reference/resources/admin/subresources/organization/subresources/usage/methods/completions) | Admin usage is time-bucketed and may group by project, key, model, Batch, and service tier; it is not an immediate per-attempt billing receipt. |
| Administrative cost | [Costs API](https://developers.openai.com/api/reference/resources/admin/subresources/organization/subresources/usage/methods/costs) | Admin cost is currently returned in daily buckets and can group by project, line item, and key; it cannot generally prove one Infinium attempt's exact bill. |
| SQLite atomicity | [SQLite transaction control](https://www.sqlite.org/lang_transaction.html) | SQLite serializes writes and supports explicit atomic write transactions, fitting one coordinator-owned reservation gate. |
| SQLite integer behavior | [SQLite datatypes](https://www.sqlite.org/datatype3.html) | Signed 64-bit INTEGER values provide exact counters; monetary rate calculation still needs an explicit rational and rounding policy. |

No paid or authenticated provider call was made. No performance conclusion is
drawn from a hypothetical high-scale run.

## 4. Keep distinct facts distinct

The budget system must not collapse these concepts into one `cost` field:

| Fact | Meaning | Authority |
|---|---|---|
| `declared_bound` | Finite worst case for one proposed attempt | Qualified adapter + immutable request |
| `reservation` | Local authorization hold against applicable limits | Coordinator transaction |
| `provider_usage` | Tokens/tool units in an attempt or Batch-item receipt | Provider response |
| `calculated_cost` | Provider usage multiplied by the invocation's price catalog | Infinium-derived estimate |
| `billing_cost` | Provider administrative billing record | Provider billing report |
| `rate_headroom` | Capacity in a replenishing request/token window | Provider response headers/admin capability |
| `provider_spend_limit` | Provider-configured future-use control | Provider configuration |
| `local_hard_limit` | User-authorized Infinium enforcement boundary | Immutable run/operation configuration |
| `prepaid_credit_balance` | Purchased balance still available | Provider billing system, only if exposed |

An OpenAI response can support `provider_usage` but not an exact dollar charge.
The organization Costs API is daily and administrative, so it cannot generally
be allocated to one attempt. Infinium therefore needs at least:

```text
unknown
reserved
calculated_from_provider_usage
provider_aggregate_observed
exact_provider_charge_reconciled   // only if a future provider supplies it
unresolved
```

The UI may call a calculated amount “current estimated cost.” It must not call
it “amount charged” unless an exact provider billing authority supports that
claim.

## 5. Logical ledger

### 5.1 Budget scopes and policies

Each immutable operation/run configuration resolves its applicable scopes
before scheduling:

```text
BudgetScope {
  scope_id
  scope_kind =
    operation_node | evidence_acquisition_run | analysis_run |
    provider_profile | provider_billing_scope
  owner_or_binding_id
  period_identity             // lifetime for M1; versioned periods later
  policy_version
}

BudgetLimit {
  scope_id
  dimension
  hard_limit_integer
  unit
  deadline_utc?               // separate non-consumptive gate
}
```

Provider/account scopes are **local limits attached to the selected profile or
verified billing scope**. They do not claim to mirror provider prepaid credits,
monthly approved usage, or project spend enforcement.

Initial consumptive dimensions may include:

- `provider_model_dispatches`;
- `input_tokens`;
- `output_tokens` (including reasoning/non-visible output);
- `builtin_tool_calls`;
- a separately typed `web_search_calls`;
- `calculated_cost_nano_usd`;
- `provider_billed_cost_minor_units` only for a future adapter with a qualified
  exact-billing contract; and
- future provider-specific typed units only after qualification.

`max_inflight_attempts` is a non-consumptive capacity dimension. A dispatch
deadline is a time gate. Neither should be added to token or money usage.
The capacity definition must say whether it covers only locally open transport
or provider work that may still be running after local abort. The latter cannot
release an ambiguous slot without a qualified provider terminal state or finite
maximum duration.

### 5.2 Reservation group

One attempt owns one group:

```text
UsageReservationGroup {
  reservation_id
  owner_operation_id
  owner_run_id
  job_node_id
  attempt_id
  coordinator_fence
  request_envelope_hash
  provider_profile_id
  credential_generation
  revocation_epoch
  capability_snapshot_id
  transport_mode
  pricing_catalog_id
  pricing_rule_set_id
  idempotency_identity
  attribution_segment_id
  state
  created_sequence
}

UsageReservationItem {
  reservation_id
  scope_id
  dimension
  reserved_integer
  unit
}
```

The same worst-case vector is checked against each applicable scope. This does
not duplicate ownership: the group owns the hold; scope items say where that
one hold consumes headroom.

### 5.3 Events and projections

The durable history is append-only:

```text
reservation_created
dispatch_authorized
transport_start_observed
provider_identity_observed
cancellation_requested
provider_terminal_observed
usage_receipt_admitted
reservation_reconciled
reservation_voided_before_dispatch
unresolved_hold_declared
billing_aggregate_observed
billing_adjustment_observed
```

Current counters are transactionally maintained rebuildable projections:

```text
BudgetCounter {
  scope_id
  dimension
  actual_enforcement_debit
  outstanding_reserved
  unresolved_reserved
  reported_actual
  projection_sequence
}
```

`reported_actual` may later decrease after a provider correction.
`actual_enforcement_debit` is monotonic within an immutable run/operation:
negative or late provider adjustments remain visible but do not silently
authorize more work. Releasing the unused part of a successfully reconciled
worst-case reservation is not a negative adjustment; it is normal settlement.

Every event retains actor, operation/run/node/attempt, coordinator fence,
prior/current state, UTC time, effective elapsed time, provider request IDs,
raw receipt reference/fingerprint, price version, reason, and projection
sequence. This is auditable provenance, not a claim of tamper resistance
against a malicious local database owner.

## 6. Finite bound contract

Each provider operation exposes:

```text
TryBound(request, capability_snapshot, price_catalog)
  -> finite vector + derivation
  | capability_gap(dimension, reason)
```

The bound must be calculated from the exact immutable request that will be
dispatched. A mean, percentile, expected cache hit, or “typical output” is not
a hard-limit bound.

### Calls

- A synchronous/background Response reserves one billable request.
- Every automatic retry is a new attempt and new request reservation.
- A token-count, poll, cancel, upload, or administrative request is separately
  typed operational usage. It must not be mislabeled as a model call; its
  billing status must be qualified before a money bound assumes zero.
- Batch reserves each request item, not merely one outer Batch object.

### Input tokens

The selected adapter must use the provider input-token-count capability or a
separately qualified conservative upper bound over the exact request. A local
plain-text tokenizer is insufficient for tools, schema formatting, images, or
files. If counting changes the request payload or selected model, count again.

### Output and reasoning tokens

The request must set an explicit finite `max_output_tokens`. Current OpenAI
documentation states that this bounds visible plus reasoning/non-visible
output. A request with no enforceable maximum cannot run under output-token or
catalog-calculated-cost hard limits.

### Tool calls

An enabled built-in tool requires a finite `max_tool_calls`, and every priced
tool class needs a catalog rule. M1 model-backed extraction/investigation
enables no tools. Governed web discovery may be a separate operation with a
finite search-call bound. Function-tool loops or remote MCP are not made
billable M1 capabilities by this report.

### Money

Catalog-calculated money is stored as signed 64-bit integer nano-USD for
comparison. Catalog rates are exact decimal strings parsed into integer
numerator/denominator pairs.

For each price component:

```text
reserved_component = ceil(bound_units * rate_numerator / rate_denominator)
```

Every component rounds upward before summation. Observed calculated cost uses
the same exact rational catalog and a documented display-rounding policy; it
does not use binary floating point. Overflow or an unsupported currency/context
band/service tier is a capability gap, not a saturated or guessed value.

The price catalog retains:

- source URL and retrieval time;
- currency and effective/observed interval;
- model identity/classification;
- direct/Batch/background mode;
- requested and every allowed returned service tier;
- context band, region, cache class, input/output class, and tool class;
- exact rate and rate unit; and
- catalog/normalizer schema version.

If `service_tier=auto` can resolve to differently priced processing and the
adapter cannot bound every allowed result, catalog-calculated-cost-limited work
must select a qualified explicit tier or not start. A moving model alias is
valid only if the active catalog can conservatively cover its allowed resolved
identities.

This produces a hard bound over Infinium's named
`calculated_cost_nano_usd` dimension. Current OpenAI Responses do not return an
exact dollar charge, and the administrative Costs API is daily and aggregate.
Therefore `provider_billed_cost_minor_units` is an explicit unsupported
capability for M1. The UI must not imply that the calculated-cost stop is the
provider's own billing cutoff. If a future asynchronous mode can execute after
a catalog price changes and the provider does not document which price applies,
that mode has a calculated-cost hard-bound gap as well.

### Prompt caching

A hard bound never assumes a cache hit. For a cache-capable request, reserve
the most expensive documented outcome among ordinary input, cache write, and
other applicable classes for each token population. Current later-model cache
writes can cost more than ordinary input, so “uncached is worst case” is not a
general rule. Reconcile from `cached_tokens` and `cache_write_tokens`.

If the provider may cache implicitly but does not expose sufficient usage or
pricing detail for the chosen model, catalog-calculated-cost enforcement has a
capability gap. Explicit caching remains disabled in M1.

## 7. Atomic admission and dispatch

### 7.1 Reservation transaction

The coordinator executes one short SQLite write transaction:

1. validate coordinator lease/fence and immutable attempt identity;
2. validate the operation/run/node is schedulable and not paused, cancelling,
   terminal, invalidated, or already exhausted;
3. validate the exact request/configuration/capability/price identities;
4. resolve all applicable scopes and limits;
5. compute the finite vector and reject any configured dimension with a gap;
6. compute an effective nondecreasing current time and reject a passed
   dispatch deadline;
7. for every scope/dimension, require:

   ```text
   hard_limit - actual_enforcement_debit
              - outstanding_reserved
              - unresolved_reserved
     >= requested_reservation
   ```

8. validate the in-flight-capacity counter;
9. create the attempt, reservation group/items, and append event;
10. increment all scope projections; and
11. commit.

SQLite's one-writer transaction makes the multi-scope check-and-increment
atomic. Application mutexes, UI disabled states, or per-run transactions would
not protect two coordinators/attempts from oversubscription.

### 7.2 Effective deadline time

M1 uses a wall-elapsed dispatch deadline fixed in the immutable operation
configuration. Pause, application exit, and offline time count. During one
coordinator lifetime, use a monotonic timer anchored to recorded UTC. Persist
the greatest effective gate time and never move it backward.

After restart, if the system wall clock is materially behind the last durable
gate time, block new billable dispatch and report clock uncertainty rather than
granting extra time. Finishing after a deadline remains actual elapsed time and
cost; the deadline only prevents a new dispatch. A stronger “must finish by”
guarantee requires a provider operation with a qualified finite execution
timeout and cancellation contract.

### 7.3 Final dispatch gate

After reservation, the attempt-scoped credential broker may resolve the exact
secret into transient trusted memory. Immediately before transport, a second
short transaction must verify:

- the reservation is active and undispatched;
- the coordinator/attempt fence remains current;
- the run/node remains eligible;
- the deadline has not passed;
- the exact credential generation and revocation epoch remain active;
- no pause/cancel/delete request closed the gate; and
- the idempotency identity has no prior transport-start/ambiguous result.

It appends `dispatch_authorized` and transitions the attempt to
`dispatch_committed`. Credential resolution and handling follow the RQ-018
trusted boundary; no secret enters this transaction or ledger. The adapter then
starts transport immediately and records the earliest observable transport
start.

There is an unavoidable crash window between the durable gate and proof of
provider receipt. If the provider supports a qualified idempotency key, retries
query/reuse that identity. If it does not, an indeterminate start becomes an
unresolved full hold and automatic retry is prohibited. A client-generated
request ID is correlation only unless provider documentation proves
idempotency.

An undispatched reservation may have a short coordinator lease. It can be
voided only when durable state proves transport was never authorized/started.
Once dispatch is authorized and transport may have started, expiry cannot
release consumptive budget.

## 8. Settlement, retries, and cancellation

### Completed receipt

One admitted provider receipt:

1. preserves raw and normalized usage;
2. creates one immutable owned actual-usage entry;
3. calculates cost with the invocation's bound catalog;
4. atomically moves reservation quantities to actual debit and releases only
   the proven unused remainder;
5. records any `actual > reserved` variance;
6. marks each overrun scope exhausted and blocks further dispatch; and
7. updates rollups by reference.

The provider can exceed a local expectation despite a bound defect. Infinium
must record the overrun rather than clamp the receipt or roll back the entry.
Locally observed dispatch-count usage settles at proven transport start even
when no terminal provider receipt follows. Conversely, a reservation can return
to zero dispatches only when the host proves transport never began.

### Retry

A retry is a new attempt and new reservation. The previous attempt's actual or
unresolved hold remains. Rate-limit errors can consume provider rate capacity,
and a timeout/disconnect may still be billable; neither is automatically a
zero-cost retry. Retry policy must classify:

- definitely before transport;
- provider-rejected with known zero billable usage;
- completed/known usage;
- ambiguous after dispatch; and
- idempotently recoverable by provider request identity.

Only the first two can release/avoid an earlier consumptive reservation without
later reconciliation evidence.

### Pause and synchronous cancel

Pause prevents new dispatch. It does not revoke dispatched authorization.
Closing a synchronous connection stops local waiting but does not prove
provider computation or billing stopped. If no terminal usage receipt exists,
the attempt becomes `client_aborted_usage_unknown` and retains its full
unresolved hold.

### Background Response

If enabled later:

- reserve the full request before creation;
- retain provider response ID and state history;
- cancellation request does not release the hold;
- release/reconcile only after a terminal provider object with sufficient
  usage, or retain an unresolved full hold;
- losing the credential needed to poll/cancel creates an audit gap, not free
  budget; and
- provider-side temporary storage/retention remains independently disclosed.

### Batch

If enabled later:

- construct and reserve every item in one atomic admission transaction before
  upload/submission;
- bind each item `custom_id` to its attempt/reservation;
- do not reserve only the Batch container;
- cancellation/expiry leaves completed items billable;
- reconcile each returned item independently, regardless of output order;
- release an unprocessed item's reservation only from a terminal item state
  that proves it was not executed; and
- keep missing/ambiguous items held.

One enormous Batch need not be the scheduling unit. Bounded batches reduce the
amount of budget and work trapped behind one delayed terminal state.

### Delayed billing and adjustments

Per-response usage settles Infinium's local estimate. Administrative usage/cost
reports are separate observations. Current OpenAI cost data is daily and may
include unrelated activity, so M1 must not distribute a bucket across attempts.

An aggregate provider adjustment:

- is recorded at the exact provider/account/project/key/time/line-item scope
  supplied;
- links to candidate invocation populations only when the relationship is
  explicit;
- changes displayed aggregate billing variance;
- does not rewrite an original receipt or fabricate per-attempt cost; and
- does not create new headroom in an immutable exhausted run.

A future exact charge/adjustment identity may reconcile one attempt, but its
original reservation, receipt, calculation, and correction remain as separate
events.

## 9. Attribution, reuse, and exhaustion

### Attached acquisition and detachment

At reservation/dispatch, each attempt binds the current attribution segment:

- acquisition-run and provider/account scopes always apply;
- while attached, the controlling analysis-run scope also applies;
- detachment records an immutable cutoff sequence;
- attempts dispatched before the cutoff remain in the parent's rollup and
  limit even if receipts arrive later; and
- post-cutoff attempts require separately authorized continuation and omit the
  former parent's scope.

Detachment never moves ledger ownership and never frees pre-cutoff
reservations.

### Reused historical work

Reusing an admitted prior result creates no provider request, reservation, or
current-run debit. The consuming run shows:

- reused work and its original usage/cost through the reuse edge; and
- zero new spend for that reuse.

Historical cost may appear in provenance but must not be added to current
estimated cost or current hard-limit consumption.

### Exhaustion propagation

- **Operation-node limit exhausted:** terminalize that bounded node as
  limit-reached; mark remaining population skipped-by-limit; unaffected parent
  work may continue.
- **Acquisition-run limit exhausted:** stop new billable acquisition nodes in
  that run; dependent analysis exposes documentation coverage gaps.
- **Analysis-run limit exhausted:** stop new billable descendants controlled by
  that analysis; independent local work may continue only if its configuration
  permits.
- **Provider/account local limit exhausted:** block every new attempt using that
  scope, across otherwise eligible runs.
- **Deadline reached:** no new dispatch in the affected operation/run; in-flight
  work remains visible and reconciles normally.

Continuing after any immutable operation/run hard limit requires a new
user-initiated run/configuration and validated reuse. A provider/account policy
period may advance only through an explicit versioned period rule, not by
editing an old counter.

## 10. Conservative M1 subset

M1 should implement and qualify the following direct Platform API subset:

1. one coordinator-owned transactional ledger in the accepted SQLite store;
2. immutable operation/run/node/attempt ownership and append-only events;
3. operation-node, acquisition-run, analysis-run, and local provider-profile
   scopes, with credential generation retained separately at dispatch;
4. hard limits for provider model-dispatch count, input tokens, output tokens,
   hosted web-search calls where enabled, and nano-USD catalog-calculated cost;
5. a wall-elapsed dispatch deadline;
6. one billable attempt in flight globally, while still exercising atomic
   multi-scope reservation;
7. synchronous stateless `store=false` OpenAI Responses only;
8. a finite explicit `max_output_tokens`;
9. no model-selected tool for extraction/investigation and a separately bounded
   discovery operation for hosted web search;
10. a qualified explicit model/service-tier/context/region price rule;
11. provider input-token count or a qualified conservative exact-request bound;
12. exact rational/integer arithmetic and upward reservation rounding;
13. attempt-scoped credential generation/revocation/final-gate checks;
14. no automatic retry after ambiguous dispatch;
15. completed, failed-known, abort-unknown, overrun, and unresolved settlement;
16. one-owned actual entries, attached rollups, historical-reuse separation,
   and detachment-ready attribution segments; and
17. CLI/diagnostic output showing reservations, actual usage, estimate state,
   gaps, and ledger audit without secrets.

M1 must expose exact provider-billed-money enforcement as unsupported. A user
who requires that dimension as a hard limit cannot start paid work; token, call,
tool, and catalog-calculated-cost limits remain independently available.

M1 should deliberately disable:

- background Responses;
- Batch;
- explicit prompt-cache breakpoints or cache-dependent budgeting;
- multiple concurrent billable attempts;
- provider-admin cost/usage reconciliation;
- automatic period resets;
- non-USD catalog-calculated-cost limits;
- remote/custom/MCP/model-selected tools; and
- any provider operation lacking a finite bound in every configured dimension.

Single in-flight execution is not a substitute for the atomic algorithm. It is
a risk-reduction mode while M1 proves the same schema and check-and-reserve path
that later concurrency will use.

## 11. Alternatives considered

| Alternative | Result |
|---|---|
| Check cost only before each call | Reject: concurrent attempts can each see the same headroom and oversubscribe. |
| Trust OpenAI project spend limits | Reject: provider controls are different scopes, may require admin authority, do not provide per-operation attribution, and do not replace local token/call/deadline limits. |
| Subtract organization historical cost from a limit | Reject: buckets can be delayed, daily, aggregate, and include unrelated activity; this is not prepaid balance or exact run spend. |
| Release reservation on client cancel/timeout | Reject: connection closure/cancel request does not prove terminal zero usage. |
| Reserve expected or p95 cost | Reject for hard limits: useful for estimates, not a finite authorization bound. |
| One money counter only | Reject: money estimates can drift and cannot replace explicit token/call/tool bounds or provider receipt provenance. |
| Decimal floating-point counters | Reject: binary floating point and implicit rounding undermine deterministic hard-limit comparisons. |
| Event log without transactional projections | Insufficient alone: reconstruction is useful, but dispatch needs one atomic current headroom check. |
| General workflow framework budget plugin | Reject for M1: the Infinium operation/run/attribution/credential contract still requires the same application ledger and transaction. |

## 12. Uncertainty and required conformance

The design is decision-grade, but these are not yet proven:

- exact production model, service tier, region, context-band, and price catalog;
- whether the selected OpenAI input-token-count operation has any billable or
  rate behavior requiring its own consumptive reservation;
- provider request-id/idempotency behavior for the exact SDK/API path;
- complete usage fields for aborts, tool calls, cache classes, and selected
  models;
- provider price-change effective-time semantics;
- precise administrative cost-report latency and whether future APIs add exact
  request correlation;
- clock rollback/suspend/restart behavior in the implementation;
- SQLite binding transaction mode, busy handling, checked integer arithmetic,
  and crash recovery; and
- user-facing display terminology for calculated versus billed cost.

Before the first authenticated or paid M1 call, a bounded disposable-account
conformance suite must qualify the exact model/API/SDK request shape, token
count, usage receipt, service-tier return, tool count, cancellation ambiguity,
price rules, request IDs, and redaction. This is conformance work, not a reason
to broaden M0 into speculative performance benchmarking.

## 13. Accepted recommendation and ADR

ADR-0023 accepts the recommendation titled:

> Coordinator-owned atomic cost ledger and hard-budget enforcement

It should decide:

1. the coordinator and transactional relational store own all billable
   admission;
2. every attempt has one multi-scope worst-case reservation group;
3. reservation, attempt identity, deadline, fences, and scope counters commit
   atomically before dispatch;
4. the final gate binds active credential generation/revocation, reservation,
   deadline, and idempotency identity;
5. provider usage and local calculated cost remain distinct from provider
   billing aggregates, rate headroom, spend limits, and credits;
6. ambiguous dispatched work retains an unresolved full hold;
7. one actual event has one owner and non-owning rollups;
8. price rules use versioned rational rates, exact integer counters, and
   upward worst-case rounding;
9. negative/late adjustments do not silently reopen immutable run budget;
10. detachment uses dispatch-sequence attribution and historical reuse incurs
    no new debit;
11. M1 uses the conservative synchronous, single-billable-dispatch subset; and
12. background, Batch, caching, concurrency, and new provider/tool dimensions
    require explicit capability qualification and evaluation before enablement.

ADR-0023 cross-references rather than merges the accepted lifecycle,
process/query, credential, and security decisions. Budget authorization is one
part of dispatch authority; it does not grant source, filesystem, tool, or
credential authority by itself.

## 14. Evaluation obligations

`EVAL-0044` should prove:

- an attached child has one owned charge visible once in child and applicable
  parent scope;
- detachment freezes the parent at the dispatch cutoff;
- a delayed pre-cutoff receipt still affects the parent;
- post-cutoff continuation is separately authorized; and
- reused historical cost is displayed separately and consumes no current
  budget.

`EVAL-0081` should include adversarial interleavings:

- two attempts competing for the final unit in operation and account scopes;
- child scope available but parent scope exhausted, and the reverse;
- pause/cancel/delete/credential-revoke between reservation and final gate;
- crash before reservation commit, after commit, after final gate, and after
  transport start;
- retry after definitely-undispatched versus ambiguous dispatch;
- receipt below, equal to, and above reservation;
- sync abort with known and unknown usage;
- clock rollback and deadline crossing;
- integer boundary, rational rounding, price-version, service-tier, context
  band, and overflow cases;
- provider adjustment that lowers displayed aggregate but cannot restart an
  exhausted immutable run;
- future background cancel before/after terminal usage;
- future Batch out-of-order partial/cancelled/expired items;
- future cache hit, miss, write, and missing cache-usage detail; and
- ledger projection rebuild matching the append-only event history.

The synchronous reservation/final-gate/crash/receipt/rounding/projection
scenarios are the M1 budget-substrate gate even while M1 permits only one
billable attempt in flight. The background, Batch, cache, and actual concurrent
competition scenarios are capability-specific extensions that must pass before
those modes are enabled; they are not claims that M1 enables those modes.

Related cases:

| Case | Required contribution |
|---|---|
| `EVAL-0034` | Budget/price/receipt diagnostics contain no credential or unnecessary source context. |
| `EVAL-0038` | Pause/restart preserves reservations and cannot double-dispatch. |
| `EVAL-0041` | Deletion preview exposes reservations, unresolved holds, audit, and resumability effects. |
| `EVAL-0076` | UI distinguishes usage, calculated cost, billing aggregate, rate headroom, spend limit, local budget, and unsupported credit balance. |
| `EVAL-0077` | Reservation/final gate uses only the selected profile/generation/account and never falls back. |
| `EVAL-0083` | Exact request, bound derivation, price/capability versions, receipt, reconciliation, and adjustment provenance resolve end to end. |

Passing synthetic atomicity tests is insufficient by itself. The exact provider
adapter must pass live, bounded, user-authorized conformance before concurrent
or expanded billable operation.

## 15. Suggested RQ-034 status

After owner acceptance of the relevant Wave E ADR:

> Resolved for M0 by the accepted coordinator-owned multi-scope reservation,
> deadline, dispatch-fence, one-owned ledger, and conservative reconciliation
> design. M1 is restricted to qualified synchronous single-billable-dispatch
> operation; provider/model live conformance and EVAL-0044/EVAL-0081 remain
> pending, and background, Batch, explicit caching, and concurrent billable
> execution remain disabled until separately qualified.

ADR-0023 is accepted, so the resolved status above applies. Implementation,
provider/model live conformance, and the named evaluation cases remain
pending.

## 16. Semantic self-review

- The design does not treat OpenAI administrative cost as per-attempt truth.
- Rate limits, spend limits, credits, local budgets, usage, and cost remain
  distinct.
- Every billable attempt has one owner despite multi-scope enforcement.
- Cancellation, detachment, and reuse cannot erase or duplicate spend.
- Unknown usage remains unknown and conservatively consumes headroom.
- A provider capability gap blocks only the configured hard-limit operation;
  it does not fabricate a finite bound.
- The M1 restriction does not accept background, Batch, cache, concurrency, or
  an exact model by implication.
- No source/model/UI state grants credential, dispatch, filesystem, or
  analytical authority.
