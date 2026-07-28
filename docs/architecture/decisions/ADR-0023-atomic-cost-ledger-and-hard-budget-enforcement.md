# ADR-0023: Atomic cost ledger and hard-budget enforcement

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Context

Infinium must estimate, authorize, attribute, stop, and reconcile consumptive
provider work across operation, acquisition-run, analysis-run, and
provider/account limits. A pre-call check, UI-disabled state, provider spend
limit, expected-cost estimate, or single money counter cannot prevent competing
work from oversubscribing a shared local budget or explain delayed,
uncancellable, retried, reused, or ambiguously dispatched work.

Provider usage, Infinium-calculated cost, provider billing aggregates, rate
headroom, provider spend limits, and prepaid credit are distinct facts. Current
OpenAI Responses usage does not provide an exact provider-billed dollar charge,
and administrative cost reports are aggregate and delayed. The first M1
provider subset therefore needs a conservative local enforcement contract that
does not promise authority the provider does not expose.

## Decision drivers

- No work may dispatch unless every configured consumptive dimension has a
  qualified finite worst-case bound.
- Competing attempts must not oversubscribe any shared operation, run, or local
  provider/account budget.
- Usage must have one owner while appearing once in each applicable rollup.
- Deadline, pause, cancellation, credential revocation, retry, and crash races
  must fail conservatively.
- Provider usage and local price calculations must remain distinguishable from
  exact provider billing.
- Reused historical work must not become current spend; attached child work
  must not be double-counted.
- M1 should prove one durable accounting path before enabling background,
  Batch, cache-dependent, or concurrent billable execution.

## Considered options

### Check expected cost immediately before each request

Rejected. Expected or percentile estimates are not finite authorization bounds,
and concurrent attempts can observe the same headroom and oversubscribe it.

### Rely on provider spend/rate limits or administrative cost reports

Rejected. These are different scopes and authorities, may include unrelated
usage, can be delayed or aggregate, and cannot provide Infinium operation/run
attribution or replace local token, call, tool, and deadline limits.

### Release reservations on client cancellation, timeout, or lease expiry

Rejected after a dispatch may have started. Connection closure and a cancel
request do not prove zero provider usage. Ambiguous potentially billable work
must retain a conservative hold.

### Use one floating-point cost counter

Rejected. It collapses distinct usage and billing facts, cannot enforce
non-money dimensions, and introduces nondeterministic rounding.

### Use a coordinator-owned atomic multi-scope ledger with finite
worst-case reservations and explicit settlement

Selected. A single relational write transaction can check and reserve every
applicable scope before dispatch and preserve one-owned usage with
non-owning rollups.

## Decision

1. The coordinator and its authoritative transactional relational store shall
   own all consumptive admission, reservations, usage events, settlements, and
   current budget projections. UI state, application mutexes, workers, and
   provider limits do not grant dispatch authority.
2. Each billable or otherwise consumptive attempt shall have exactly one
   reservation group. The group shall contain a finite worst-case vector for
   every applicable configured dimension and shall be checked atomically
   against all applicable operation-node, evidence-acquisition-run,
   analysis-run, and local provider-profile/account scopes.
3. The bound shall derive from the exact immutable request, declared operation,
   adapter capability snapshot, and versioned price catalog. Means, percentiles,
   expected cache hits, and typical usage are estimates, not hard-limit bounds.
   If any configured dimension lacks a qualified finite bound, the operation
   shall not start under that hard-limit configuration and shall expose the
   capability gap.
4. The initial dimensions shall include model-dispatch count, input tokens,
   output/reasoning tokens, explicitly enabled priced tool calls, and
   catalog-calculated money. Token-count, poll, cancel, upload,
   administrative, and Batch-item work shall be separately typed; none may be
   assumed free without qualification.
5. Catalog-calculated money shall use signed 64-bit integer nano-USD counters.
   Catalog rates shall be exact decimal values parsed into integer
   numerator/denominator pairs. Each worst-case price component shall round
   upward before summation. Overflow, unsupported currency, price class,
   service tier, model resolution, region, context band, or cache class is a
   capability gap, never a guessed or saturated value. A bound shall never
   assume a cache hit; if caching may occur, it shall cover the most expensive
   documented applicable outcome, and insufficient cache usage or price detail
   is a capability gap.
6. The ledger shall keep separate the declared bound, local reservation,
   provider usage receipt, catalog-calculated cost, provider aggregate billing
   observation, rate headroom, provider spend limit, local hard limit, and
   prepaid credit. M1 shall not label calculated cost as “amount charged” or
   claim exact provider-billed-dollar enforcement.
7. In one short write transaction, reservation shall validate the active
   coordinator/run/attempt fences and schedulability; exact request,
   configuration, capability, and price identities; every applicable scope and
   limit; a non-expired wall-elapsed dispatch deadline; and in-flight capacity.
   It shall then create the attempt and reservation group, append the event,
   update every scope projection, and commit atomically.
8. Dispatch authorization is a separate final transaction in the same
   authorization sequence. Immediately before transport, it shall revalidate
   the active undispatched reservation, coordinator/attempt fence, run/node
   eligibility, deadline, selected credential generation and revocation epoch,
   pause/cancel/delete state, and absence of a prior or ambiguous transport
   start for that idempotency identity. Budget authorization does not grant
   credential, source, filesystem, tool, or provider authority.
9. M1 dispatch deadlines shall be fixed wall-elapsed deadlines in immutable
   operation/run configuration. Pause, application exit, suspend, and offline
   time shall not extend them. A persisted effective gate time shall never move
   backward; material clock rollback uncertainty shall block new dispatch.
   The deadline prevents new work but does not promise that already started
   work will finish or cease billing by that time.
10. Once transport may have started, reservation expiry or local cancellation
    shall not release budget. If a qualified provider idempotency contract can
    prove safe reuse, the adapter may use it. Otherwise an indeterminate start
    shall retain the full unresolved hold and automatic retry is prohibited.
11. Settlement shall preserve completed, failed-known, abort-unknown, overrun,
    and unresolved states. Provider-reported usage shall create one actual entry
    owned by its attempt/operation. Applicable parents and provider/account
    views shall reference that entry through rollups rather than copy its debit.
    Usage above a reservation remains visible and exhausts affected scopes; it
    never authorizes further work.
12. Acquisition work always retains acquisition-run and provider/account
    ownership. While attached, its dispatch also applies to the controlling
    analysis-run scope. Detachment shall record an immutable dispatch-sequence
    cutoff: pre-cutoff attempts remain in the parent's rollup even if receipts
    arrive later, and post-cutoff continuation requires separate authorization.
13. Reusing admitted historical work shall create no provider request,
    reservation, or current-run debit. Its original usage/cost remains visible
    through the reuse edge and is not added to current estimated spend or hard
    limits.
14. Exhaustion shall terminate only the bounded operation or run scope it
    governs, stop new affected dispatches, and report remaining population as
    skipped by limit. Unaffected local or parent work may continue only where
    its own immutable configuration and limits allow. Continuing exhausted work
    requires a new user-initiated run/configuration and validated reuse.
15. Provider adjustments and delayed aggregate billing observations shall be
    retained separately. A later negative adjustment shall not silently reopen
    an exhausted immutable run or rewrite earlier admission decisions.
16. Each consumptive operation shall bind one provider-capability-specific
    limit contract. Direct Platform API work may use the
    call/token/tool/catalog-money vector above. No exhaustion or failure may
    authorize fallback to another credential, account, billing scope, or
    provider.

## M1 boundary

M1 shall use a conservative, synchronous, single-live-dispatch subset:

- one coordinator-owned transactional ledger using the same multi-scope
  reservation and final-gate path intended for later concurrency;
- synchronous stateless OpenAI Responses with `store=false`;
- one globally in-flight billable attempt;
- explicit finite `max_output_tokens`;
- no model-selected tool for extraction/investigation, with hosted web search
  only as a separately typed operation with its own finite search-call bound;
- a qualified explicit model, service tier, context band, region, and versioned
  rational price rule;
- the provider input-token-count capability or a separately qualified
  conservative bound over the exact request;
- hard limits for dispatch count, input/output tokens, bounded hosted-search
  calls where enabled, catalog-calculated nano-USD, and wall-elapsed dispatch
  deadline;
- no automatic retry after ambiguous dispatch;
- one-owned usage, rollups, detachment-ready attribution, historical-reuse
  separation, and explicit unresolved holds; and
- secret-free CLI/diagnostics showing reservations, actual usage, estimate
  authority, capability gaps, and audit events.

M1 shall disable background Responses, Batch, explicit prompt-cache behavior or
cache-dependent budgeting, concurrent live billable attempts, provider-admin
cost/usage reconciliation, automatic period resets, non-USD calculated-cost
limits, remote/custom/MCP/function tools, every model-selected tool except the
separately bounded hosted-search operation accepted by ADR-0013, and every
operation lacking a finite bound in each configured dimension. Each later mode
requires separate provider-capability qualification and its applicable
evaluation extensions. Single-dispatch execution reduces initial risk; it does
not replace the atomic algorithm.

## Consequences

### Positive

- One atomic admission path prevents multi-scope budget oversubscription.
- Usage is attributable without duplicated ledger ownership.
- Cancellation, crash, retry, detachment, and reuse have conservative durable
  semantics.
- The UI can distinguish enforceable local limits from provider estimates and
  billing facts.
- The M1 subset is bounded while preserving the data model needed for later
  concurrency.

### Negative

- Every supported provider operation needs a qualified finite-bound adapter and
  versioned price/capability catalog.
- Uncertain dispatch may hold the full reserved amount indefinitely until
  reconciled or explicitly resolved under a future qualified mechanism.
- Conservative upward rounding and no assumed cache hit may reserve materially
  more than expected usage.
- Background, Batch, cache-dependent, and concurrent execution are unavailable
  in M1.
- Exact provider-billed-dollar enforcement is unsupported.

### Risks and mitigations

- **Crash between durable authorization and provider receipt:** use qualified
  provider idempotency where available; otherwise retain an unresolved full
  hold and prohibit automatic retry.
- **Clock rollback grants time:** persist a nondecreasing effective gate time
  and block new billable dispatch on material uncertainty.
- **Price or model alias drift:** bind the immutable request to a versioned
  capability and price snapshot and reject unresolved classes.
- **Integer overflow or rounding error:** use checked signed 64-bit counters,
  rational rates, component-wise upward reservation rounding, and boundary
  fixtures.
- **Provider receipt differs from reservation:** retain overrun and actual
  usage, exhaust affected scopes, and never turn the overrun into authority.
- **Credential revoked after reservation:** require the separate final
  credential-generation/revocation check governed by ADR-0020 before transport.

## Requirements affected

- AI-004 through AI-007
- SCAN-005 through SCAN-010
- OPS-001 through OPS-004
- DOC-009 through DOC-011
- SEC-002 through SEC-004

## Validation

No evaluation is passed by accepting this ADR.

- EVAL-0044 must prove one-owned acquisition/analysis attribution,
  dispatch-cutoff detachment, delayed receipt behavior, separately authorized
  continuation, and zero current debit for historical reuse.
- EVAL-0081 must cover competing reservations, exhausted child/parent scopes,
  pause/cancel/delete/revocation races, crash points, retries, known and
  ambiguous aborts, clock rollback, deadline crossing, exact arithmetic,
  service-tier/context/price changes, overflow, adjustments, projection
  rebuild, and receipts below/equal to/above reservation.
- EVAL-0034 and EVAL-0089 must prove that ledger/diagnostic data contains no
  credential and that replacement, disable, deletion, restart, and dispatch
  races cannot authorize stale credential use.
- EVAL-0038 must prove pause/restart does not lose reservations or
  double-dispatch.
- EVAL-0041 must expose outstanding and unresolved reservations in deletion
  planning.
- EVAL-0076 must distinguish provider usage, calculated cost, billing
  aggregate, rate headroom, spend limit, local budget, and unavailable credit.
- EVAL-0083 must resolve request, bound, capability, price, receipt,
  attribution, settlement, and adjustment provenance end to end.

Synthetic concurrency must exercise the atomic algorithm during M1 even though
live M1 provider execution remains single-dispatch. Background, Batch, cache,
and concurrent live-provider cases gate those capabilities only when they are
later enabled. Before the first authenticated or paid call, the exact
model/API/SDK request shape, token count, usage receipt, service-tier result,
tool count, cancellation ambiguity, request identifiers, pricing rules, and
redaction must pass bounded live conformance.

The decision must be revisited if the selected relational store cannot provide
the required atomic transaction, OpenAI changes the bound/usage/price contract,
an exact-request token bound cannot be qualified, provider idempotency or exact
billing capabilities materially change, or a later milestone proposes
background, Batch, explicit caching, or concurrent billable execution.

## References

- [Product requirements](../../product/requirements.md)
- [ADR-0002](ADR-0002-snapshot-context-binding.md)
- [ADR-0013](ADR-0013-openai-first-llm-capability-boundary.md)
- [ADR-0015](ADR-0015-authoritative-evidence-persistence-and-payload-storage.md)
- [ADR-0016](ADR-0016-application-owned-durable-run-and-job-lifecycle.md)
- [ADR-0018](ADR-0018-process-and-authority-topology.md)
- [ADR-0019](ADR-0019-local-ipc-and-application-query-contract.md)
- [ADR-0020](ADR-0020-credential-storage-and-provider-dispatch.md)
- [RESEARCH-0037](../../research/investigations/RESEARCH-0037-job-checkpoint-and-run-lifecycle.md)
- [RESEARCH-0040](../../research/investigations/RESEARCH-0040-credential-entry-and-storage.md)
- [RESEARCH-0043](../../research/investigations/RESEARCH-0043-cost-ledger-and-budget-enforcement.md)
- [RESEARCH-0044](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
