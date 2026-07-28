# RESEARCH-0037: Durable job, checkpoint, and run lifecycle

Status: Completed; recommendation accepted

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary RQ: RQ-015

M0 wave: E — Architecture and stack selection

Decision enabled: worker/job architecture ADR and bounded M1 lifecycle subset

## Reopening and final disposition

On 2026-07-28, the owner reopened RQ-015 before accepting ADR-0016. The
application-owned SQLite lifecycle was the leading candidate, and the
domain requirements derived here remain valid, but the original rejection of
Dapr relied too heavily on its broader distributed capabilities and did not
prove that its local Workflow runtime would be a worse implementation
substrate.

[RESEARCH-0046](RESEARCH-0046-dapr-workflow-desktop-lifecycle-qualification.md)
was created to compare a thin application-owned SQLite implementation with
Dapr Workflow. The owner subsequently closed that investigation without a
prototype and selected the SQLite approach. Dapr is rejected because its
additional processes, workflow history, replay/versioning, activity semantics,
packaging, security, supervision, and cross-store reconciliation are not worth
the bounded lifecycle code they might replace in a local single-machine M1.
ADR-0016 accepts this recommendation.

## Executive answer

Infinium should own its run semantics in an **application-owned transactional
relational job ledger**, not delegate authoritative run identity to a generic
queue or durable-workflow framework.

The recommended model is:

1. immutable analysis-run and evidence-acquisition-run identities and bindings;
2. one owner run for every job node, attempt, checkpoint, output, and actual
   usage/cost entry;
3. an append-only transition/attempt ledger plus transactionally maintained
   current-state projections;
4. one leased coordinator with a monotonically increasing fencing token;
5. leased, idempotent work units executed by bounded worker processes;
6. content-addressed immutable checkpoints with declared dependency closures;
7. cooperative same-run pause/resume;
8. terminal cancellation, failure, limit, and invalidation;
9. dependency-validated checkpoint/output reuse only into a new,
   user-initiated run after any terminal state;
10. explicit analysis-to-acquisition control/attribution links rather than
    transferring ownership between run types;
11. single-owned actual usage/cost ledger entries aggregated through
    non-owning rollup edges; and
12. dependency-aware deletion planning that blocks silent destruction of
    active work or retained history.

For the current local Windows product, the leading physical realization is an
embedded transactional relational database—SQLite `3.53.4` is the strongest
current candidate—plus a UI-independent coordinator and isolated worker
subprocesses. RQ-013 must confirm the shared evidence-store/schema needs, and
RQ-016/RQ-017 must select the language, database binding, process topology, and
query/IPC boundary before an ADR names the exact implementation stack.

This is intentionally a small durable scheduler, not a reimplementation of a
general distributed workflow platform. Temporal, Dapr Workflow, Restate, and
Hangfire all solve real restart/retry problems, but each would still require
Infinium domain records for immutable runs, acquisition ownership, validated
cross-run reuse, cost attribution, coverage, deletion effects, and readiness
provenance. The owner judged that adding Dapr would retain most of this
application-specific work while creating a second runtime/history boundary.
The application-owned SQLite lifecycle is therefore selected for M1.

“Application-owned” does **not** mean that Infinium should hand-write timers,
threading, queues, dependency injection, process supervision, retry-delay
calculation, or database access. The recommended implementation should use
.NET Generic Host/`BackgroundService`, bounded `System.Threading.Channels`,
`PeriodicTimer` or an equivalent clock abstraction, cancellation tokens, and
qualified SQLite libraries. The product-owned code is the thin admission and
state-transition layer that turns durable Infinium records into bounded work.
Quartz.NET could later supply calendar/cron trigger calculation if real
calendar scheduling becomes a product requirement. It should not own run
identity or lifecycle truth.

## 1. Question and governing constraints

### 1.1 Primary question

Which job store/process model supports:

- linked analysis and evidence-acquisition runs;
- same-run pause/resume;
- terminal cancellation;
- checkpoint reuse into a new run;
- retries and crash recovery;
- single-owner cost rollups;
- retention/deletion safety;
- worker and UI restarts;
- process failure isolation; and
- honest progress at high-end scale?

### 1.2 Requirements and accepted decisions

| Requirement or decision | Constraint on this investigation |
|---|---|
| `SCAN-004` | Progress and cost roll up at run/stage/analyzer levels without duplicating usage ownership. |
| `SCAN-005` | Pause/resume continues the same run; cancellation is terminal; terminal retry/continuation creates a new run; attached-child control and detachment are explicit. |
| `SCAN-006` | One node failure or limit must not invalidate unrelated work. |
| `SCAN-007` | Reuse requires complete declared dependency validity. |
| `SNAP-001`–`SNAP-006`; ADR-0002 | Run bindings and artifact origins are immutable; replay, auditability, and reuse are distinct. |
| ADR-0010 | Reusable work carries typed dependency closures and origin-preserving reuse proofs. |
| `DOC-002`, `DOC-011` | Evidence acquisition is independently runnable and owns its own calls, outputs, coverage, and cost. |
| `AI-004` | Reservations, deadlines, actual usage, hard limits, and node-scoped exhaustion require atomic enforcement without double counting. |
| ADR-0013 | OpenAI synchronous, background, and Batch work have distinct cancellation/retention semantics; synchronous `store=false` is the initial default. |
| `OPS-002` | Deletion previews effects on resumability, history, replay, reuse, and independently retained copies. |
| `OPS-004`, `OPS-005` | The mechanism must scale without making the UI the execution or query bottleneck. |
| ADR-0003 | Product-owned state and temporary work remain isolated from the modding setup. |

The accepted product contract is stricter than the lifecycle exposed by most
background-job libraries. A framework's “retry,” “delete,” “pause,” or
“terminate” operation is not automatically equivalent to Infinium's meaning.

## 2. Scope and non-scope

### 2.1 In scope

- durable run, node, attempt, lease, checkpoint, and transition semantics;
- analysis/acquisition ownership and attached-child control;
- pause, resume, cancellation, retry, failure, and crash recovery;
- progress denominators and single-owned usage/cost aggregation;
- retention/deletion interaction with active and historical work;
- process isolation needed by local parsers, tools, and provider calls;
- current realistic embedded/custom and framework alternatives; and
- a bounded M1 subset.

### 2.2 Out of scope

- selecting the application language, UI shell, or IPC transport (RQ-016/017);
- selecting the full evidence-store schema or migration library (RQ-013);
- selecting credential storage (RQ-018);
- defining the exact hard-budget algorithm (RQ-034);
- accepting background Responses, Batch, or prompt caching;
- setting production concurrency, timeout, or checkpoint-frequency defaults;
- M3 scale qualification or user-facing preset calibration;
- distributed/multi-machine execution; and
- production implementation.

## 3. Method, sources, and exact versions

Research used official product documentation and release metadata retrieved on
2026-07-28. No legacy Infinium implementation was treated as evidence.

### 3.1 Repository sources

- accepted [product requirements](../../product/requirements.md);
- accepted [domain model](../../product/domain-model.md);
- draft required-behavior
  [jobs/caching/snapshots](../../architecture/jobs-caching-and-snapshots.md);
- ADR-0002, ADR-0003, ADR-0010, and ADR-0013;
- [RESEARCH-0003](RESEARCH-0003-retention-replay-export-policy.md);
- [RESEARCH-0012](RESEARCH-0012-snapshot-fingerprint-and-invalidation.md);
- [RESEARCH-0023](RESEARCH-0023-scale-performance-baselines.md);
- [RESEARCH-0032](RESEARCH-0032-openai-first-llm-and-web-search.md);
- [RESEARCH-0033](RESEARCH-0033-wave-d-revision-integration.md); and
- EVAL-0018, EVAL-0021, EVAL-0024 through EVAL-0026, EVAL-0037 through
  EVAL-0041, EVAL-0044, EVAL-0080, EVAL-0081, and EVAL-0083.

### 3.2 External primary sources

| ID | Exact source/version observed | Relevant facts |
|---|---|---|
| S1 | [SQLite 3.53.4 release](https://www.sqlite.org/releaselog/3_53_4.html), released 2026-07-24; [isolation](https://sqlite.org/isolation.html), [WAL format/locking](https://sqlite.org/walformat.html), and [atomic commit](https://sqlite.org/atomiccommit.html), retrieved 2026-07-28 | Embedded ACID transactions; serializable writes through one writer; concurrent readers in WAL mode; crash-recovery machinery. These properties fit a single-coordinator local ledger but do not implement the domain state machine. |
| S2 | [Dapr runtime `v1.18.2`](https://github.com/dapr/dapr/releases/tag/v1.18.2), released 2026-07-21; [Workflow overview](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-overview/), [features](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-features-concepts/), [architecture](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-architecture/), and [.NET management operations](https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-workflow/dotnet-workflow-management-methods/), retrieved 2026-07-28 | Durable event-sourced workflow replay, pause/resume/terminate, child workflows, durable retries, SQLite-capable state-store support, and at-least-once activities. Termination does not stop an already in-flight activity, and normal parent termination propagates to children. |
| S3 | [Temporal server `v1.31.2`](https://github.com/temporalio/temporal/releases/tag/v1.31.2), released 2026-07-08; [Temporal documentation](https://docs.temporal.io/) and [workflow protocol/API](https://api-docs.temporal.io/), retrieved 2026-07-28 | Durable workflow histories, crash recovery, retries, cancellation requests, and configurable child parent-close policies. It requires a Temporal service plus workers and its own operational/history persistence. |
| S4 | [Restate server `v1.7.2`](https://github.com/restatedev/restate/releases/tag/v1.7.2), released 2026-07-06; [key concepts](https://docs.restate.dev/foundations/key-concepts), [workflow lifecycle](https://docs.restate.dev/tour/workflows), [architecture](https://docs.restate.dev/references/architecture), and [invocations](https://docs.restate.dev/foundations/invocations), retrieved 2026-07-28 | Single-binary local deployment, durable journals, retry/recovery, pause/resume and cancellation controls, idempotency keys, epoch fencing, and single-writer workflow identity. Its current official workflow SDK list does not include .NET, while the community .NET SDK is prerelease; Infinium-specific detachment, cost, provenance, and deletion semantics would still remain application-owned. |
| S5 | [Hangfire `v1.8.24`](https://github.com/HangfireIO/Hangfire/releases/tag/v1.8.24), released 2026-07-16; [overview](https://www.hangfire.io/overview), [best practices](https://docs.hangfire.io/en/latest/best-practices.html), and [batches](https://docs.hangfire.io/en/latest/background-methods/using-batches.html), retrieved 2026-07-28 | Persistent at-least-once background jobs, automatic/manual retries, restart recovery, and separate worker processes. Batches/cancellation are a Pro feature, and the framework does not supply Infinium's run/checkpoint/reuse/cost/deletion model. |
| S6 | Microsoft [Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects), retrieved 2026-07-28 | Windows can manage a process tree as a unit, apply resource/accounting limits, and terminate the tree; `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` prevents abandoned descendants when the owning supervisor dies. |
| S7 | Quartz.NET [4.x documentation](https://www.quartz-scheduler.net/documentation/) and [configuration reference](https://www.quartz-scheduler.net/documentation/quartz-4.x/configuration/reference.html), retrieved 2026-07-28 | Mature .NET trigger/calendar/cron scheduling, hosted-service integration, concurrency control, and RAM or ADO job stores. Its trigger/job-store model is useful for time scheduling but is not Infinium's run, evidence, checkpoint, reuse, cost, or deletion model. |
| S8 | Coravel [scheduler](https://docs.coravel.net/Scheduler/) and [queue](https://docs.coravel.net/Queuing/) documentation, retrieved 2026-07-28 | Lightweight .NET scheduling and queuing with cancellation/progress conveniences; the queue is explicitly in-memory, so it cannot be the durable authority required here. |
| S9 | Microsoft [.NET 10 hosted-service background tasks](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0), [`System.Threading.Channels`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels?view=net-10.0), and [`PeriodicTimer`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer?view=net-10.0), retrieved 2026-07-28 | First-party primitives cover process-hosted background loops, bounded producer/consumer queues with backpressure, cooperative cancellation, and periodic wakeups without defining durable application semantics. |

GitHub's official releases API was probed read-only on 2026-07-28 for S2
through S5. It returned the exact tags and publication times above. SQLite's
official release chronology/release page supplied S1. These observations select
comparison baselines only; they do not select a dependency.

## 4. Required Infinium lifecycle model

### 4.1 Run ownership is primary

There are two durable run kinds:

- **analysis run:** owns the work that applies snapshot-bound and reusable
  evidence to one installation snapshot/context/configuration; and
- **evidence-acquisition run:** owns source retrieval, extraction, provider
  calls, claims, coverage, and cost for one immutable acquisition request and
  configuration.

Every analysis/acquisition job node belongs to exactly one run. Every attempt,
checkpoint, direct output, failure, coverage result, reservation, and actual
usage/cost entry belongs to exactly one job node and therefore one owner run.

An analysis run may control and consume an acquisition run through links:

```text
analysis_run
  -> acquisition_control_link
       initiation_parent = analysis_run
       acquisition_run = independently owned run
       control_mode = attached | detached
       attribution_segments[]
  -> evidence_application_links[]
```

The control link is not ownership. Selecting targets from a profile does not
make source evidence profile-owned. Detaching acquisition work never changes
its owner, immutable initiation provenance, input bindings, prior calls, or
prior cost.

Accepted LOOT managed-data maintenance remains a separate
`maintenance_operation`, not an analysis or evidence-acquisition run. It may
reuse the scheduler substrate, but any maintenance node belongs to that
operation, cannot create claims/findings/readiness, and cannot change the
immutable pair already bound to a run. Future analysis refers to the activated
validated pair as an input; it does not adopt the maintenance operation's job
tree.

### 4.2 Durable record classes

The logical store needs at least:

| Record | Required meaning |
|---|---|
| `run` | Immutable ID, kind, initiation provenance, bound inputs/configuration, and lifecycle policy version. |
| `maintenance_operation` | Separately typed accepted managed-data maintenance identity, source/schedule/configuration, pair result, and provenance; never a profile-analysis/acquisition owner. |
| `run_transition` | Append-only within retained history: requested/observed transition with prior generation, actor, reason, time, and fencing token. |
| `run_state` | Transactionally updated current projection; reconstructible while the requisite transition history is retained. |
| `run_link` / `attribution_segment` | Immutable initiation relationship plus versioned attached/detached control and progress/cost cutovers. |
| `job_node` | One immutable work specification, owner run, parent/dependencies, declared population, retry/checkpoint policy, and versioned implementation identity. |
| `job_attempt` | Each dispatch/recovery/retry with worker lease/fence, input manifest, timestamps, result, and failure/cancellation receipt. |
| `checkpoint` | Immutable content identity, producing run/node/attempt, completed partition/range, dependency closure, schema/producer version, and validation state. |
| `artifact_publication` | Staged-to-durable publication receipt and content fingerprint; never a mutable path as authority. |
| `progress_population` | Versioned denominator and completed/gap/failed/skipped counts by real work population. |
| `usage_reservation` | A pending authorization hold owned by one operation; ADR-0023 defines the accepted enforcement algorithm, while exact schema and implementation remain pending. |
| `usage_ledger_entry` | One immutable actual or provider-adjustment entry owned by the originating operation/attempt. |
| `reuse_edge` | New consuming run/node, original artifact/checkpoint, dependency-validation proof, and reuse outcome. |
| `deletion_plan` / `deletion_receipt` | Exact selected objects, dependent effects, independent copies, confirmation, result, and resulting gaps. |
| `coordinator_lease` / `worker_lease` | Holder, epoch/fencing token, acquisition/renewal/expiry, and recovery disposition. |

Large payloads may live in product-owned content-addressed files or another
blob layer. Their identities, dependencies, publication state, and retention
remain transactional records. A database row containing a mutable filename is
not a durable checkpoint by itself.

### 4.3 State machine

The accepted minimum states remain:

Non-terminal:

- `queued`;
- `running`;
- `waiting`;
- `retrying`;
- `pausing`;
- `paused`; and
- `cancelling`.

Terminal:

- `cancelled`;
- `completed`;
- `completed-with-gaps`;
- `failed`;
- `limit-reached`; and
- `invalidated-by-changed-input`.

The store should keep **requested control** and **observed execution state**
separate until a transition is safely acknowledged. A pause request therefore
does not immediately lie that the run is paused, and a cancellation request
does not immediately hide in-flight work or unreconciled provider usage.

All lifecycle transitions use a compare-and-swap generation and the current
coordinator fencing token in one transaction. Illegal transitions are rejected
rather than repaired heuristically.

The transition policy should be versioned and enforce at least:

| From | Permitted next-state families |
|---|---|
| `queued` | `running`; `pausing`; `cancelling`; or an applicable terminal state without dispatch |
| `running` | `waiting`; `retrying`; `pausing`; `cancelling`; or any applicable terminal state |
| `waiting` | `queued`/`running` when the dependency/throttle clears; `pausing`; `cancelling`; or an applicable terminal state |
| `retrying` | `queued`/`running` for the next recorded attempt; `pausing`; `cancelling`; or applicable terminal failure/limit/invalidation |
| `pausing` | `paused`; `cancelling`; or an applicable natural completion/failure/limit/invalidation reached by already-dispatched work |
| `paused` | `queued`/`running` on same-run resume; `cancelling`; or an applicable terminal state, including completion after an explicitly continuing child settles |
| `cancelling` | `cancelled` after the cancellation boundary is settled; late usage adjustments do not change this state |
| any terminal state | no transition; all continuation uses a new run and, where valid, explicit reuse |

`waiting` is operational dependency/backoff state, not investigation
`needs-input`. `retrying` represents a durable scheduled retry between
attempts, not repeated execution hidden inside one attempt.

Node terminality is scoped. A child node may become `failed`,
`limit-reached`, or `invalidated-by-changed-input` while unrelated siblings
continue; the owner run can later become `completed-with-gaps`. A run-wide
binding failure may instead terminalize the run. The reason and affected
coverage population decide which behavior applies; the state label alone does
not.

### 4.4 Same-run pause and resume

Pause is cooperative:

1. atomically record the pause request;
2. stop dispatching new nodes and attempts;
3. propagate pause requests to attached acquisition runs;
4. allow already-running bounded units to checkpoint and stop at declared safe
   boundaries;
5. record uninterruptible work explicitly while the run remains `pausing`;
6. release or retain reservations according to RQ-034 and the actual provider
   state; and
7. enter `paused` only when no direct work or default-stopped attached work can
   continue producing unaccounted output or spend. An explicitly authorized
   attached child may continue while the parent is paused, but its continuing
   state, progress, and attributable spend must remain unmistakable.

Resume clears the pause request and schedules remaining work under the same run
ID, bindings, configuration, node specifications, and existing valid
checkpoints. It does not create a new run and cannot adopt newly edited
snapshot/context/configuration versions.

A provider request directly owned by the pausing run that has no real pause
primitive is an atomic external attempt. The run stays `pausing` until it
finishes, fails, or reaches a provider-confirmed cancellation state. “Paused”
must never conceal direct provider spend. The only exception is a separately
owned attached acquisition run that the user explicitly chose to continue;
the UI must say that the parent is paused while acquisition and attributable
spend continue.

### 4.5 Terminal cancellation

Cancellation:

1. atomically records a cancellation request;
2. stops all new dispatch;
3. requests cancellation of running local/provider work;
4. requests cancellation of attached children by default;
5. records any explicit child-detachment decision before terminalizing the
   parent;
6. waits for interruptible work to stop and records uninterruptible/provider
   receipts; and
7. commits the terminal `cancelled` state.

No job attempt may start after the authoritative cancellation sequence.
In-flight work dispatched before that sequence may produce a completion,
partial output, or later provider billing adjustment; those facts remain
owned by the terminal run but never authorize more work or reopen it.

The same rule applies to `limit-reached`, `failed`, and
`invalidated-by-changed-input`: a terminal run is never resumed or retried in
place.

### 4.6 Retry, idempotency, and attempt identity

A retry is a new `job_attempt`, not mutation of the failed attempt.

- A transient failure may retry within the same non-terminal run only under
  its immutable retry policy and remaining limits.
- A worker crash is recoverable within the same run when the lease expires,
  the run remains non-terminal, and the node's retry/idempotency contract
  permits another attempt.
- Retry after any terminal run requires a new manually initiated run and an
  explicit reuse edge.

Exactly-once external execution cannot be assumed. Each operation declares:

- an idempotency key or provider request identity where supported;
- whether retry is safe before dispatch, after dispatch without receipt, and
  after a partial receipt;
- which side effects or costs may duplicate;
- how late completion is reconciled; and
- when ambiguity becomes a gap/manual-review state instead of an automatic
  retry.

Pure local work should be idempotent over immutable inputs. Workers stage
outputs under a unique attempt identity; only the current fenced attempt may
publish the artifact and terminal node transition transactionally. An expired
worker may finish computation, but its stale fencing token prevents it from
publishing authoritative state.

### 4.7 Checkpoint identity and cross-run reuse

A checkpoint is valid only for its declared:

- producing run/node/attempt;
- snapshot, context, and effective configuration dependencies;
- exact source/tool/model/analyzer/schema identities;
- completed partitions/ranges and deterministic ordering;
- upstream artifact dependency closure;
- pending/in-flight/failed/limited work;
- progress-population revision;
- reservation and actual-ledger references; and
- content fingerprints.

Same-run resume may consume the checkpoint directly because the immutable run
bindings remain unchanged.

Reuse from a cancelled, failed, exhausted, invalidated, or completed run:

1. creates a new user-initiated run with new immutable bindings;
2. resolves the new input manifest;
3. evaluates every checkpoint/output dependency under ADR-0010;
4. records the proof and original producer through a `reuse_edge`;
5. either marks an entire new node as satisfied by reused output or starts a
   new attempt from a validated checkpoint; and
6. recomputes or gaps anything whose equivalence is absent or uncertain.

The original checkpoint is never rebound, copied as newly incurred work, or
represented as a retry of the terminal run.

### 4.8 Coordinator, leases, and UI restart

The coordinator is the only authority that:

- advances lifecycle projections;
- claims runnable work;
- publishes authoritative worker outputs;
- reserves work against budgets;
- aggregates progress/cost;
- initiates recovery after lease expiry; and
- finalizes terminal runs.

At most one active coordinator holds the database lease. Lease acquisition
increments an epoch/fencing token. Every dispatch, transition, publication,
and worker message carries that token. Time expiry alone does not give an old
coordinator authority after a new epoch is committed.

The UI/CLI is a client of durable state, never the holder of run truth. A UI
crash or restart therefore changes no run state. It reconnects, queries the
current projection, and resumes incremental/paginated updates. If the
coordinator dies, a replacement:

1. acquires a new fenced lease;
2. recovers the database and staged artifacts;
3. marks expired attempts interrupted;
4. reconciles known provider request identities/receipts;
5. retries only nodes whose contracts allow it; and
6. leaves unsafe ambiguity visible.

RQ-017 must choose the exact coordinator lifetime and UI/process transport.
The required behavior permits a coordinator that continues while the UI
restarts and also permits full application shutdown followed by later
recovery. It does not permit UI memory to be the only checkpoint.

### 4.9 Worker and subprocess isolation

Crash-prone parsing, native/library boundaries, approved external tools, and
long CPU work should execute outside the UI and preferably outside the
coordinator.

The initial Windows design should support:

- one coordinator with a small bounded worker pool;
- one worker process for the M1 proof if parallelism is unnecessary;
- per-attempt product-controlled temporary directories;
- no worker authority to publish lifecycle state directly;
- bounded, schema-validated messages;
- resource/accounting limits where practical; and
- a Windows Job Object around each worker/external-process tree, with no
  silent breakaway and termination-on-owner-loss where compatible with the
  selected operation.

Process termination is not a substitute for cooperative cancellation or
provider cancellation. It is a containment/recovery boundary for local
processes. External tools may be invoked only through their separately
qualified non-mutating contracts.

### 4.10 Attached child continuation and detachment

Parent pause/cancel stops new attached-child scheduling by default. The user
may explicitly choose to continue or detach eligible acquisition work after
seeing remaining time, work, and cost.

Detachment is a transaction that records:

- the acquisition run and immutable initiation parent;
- prior and new control mode;
- actor/reason/time;
- the exact scheduler event sequence used as the cutoff;
- work already dispatched before the cutoff;
- progress and cost attribution before the cutoff; and
- remaining acquisition work after the cutoff.

Attribution follows dispatch/reservation sequence, not completion time:

- an operation dispatched before detachment remains part of the parent's
  attributable spend even if its receipt arrives later;
- an operation dispatched after detachment belongs only to the separately
  authorized acquisition continuation;
- the acquisition run remains the single owner in both cases; and
- post-detachment progress/duration/remaining-time no longer extends the
  parent.

This avoids changing attribution when delayed provider receipts arrive.

### 4.11 Progress denominators

Progress derives from durable work populations, not attempts or fabricated
percentages.

Each stage/analyzer/source population records:

- population definition and version;
- known eligible total;
- still-enumerating/unknown-total state;
- completed, completed-with-gaps, failed, configuration-skipped,
  limit-skipped, unsupported, and invalidated units;
- reused units separately from newly executed units;
- queued/running/waiting/retrying units; and
- denominator changes with their discovery reason.

A retry does not increase the denominator. Dynamic discovery creates a new
denominator revision rather than rewriting prior progress. Parent rollups join
the latest applicable population revisions; detached-child remaining work is
removed through the recorded attribution/control transition and remains
visible on the acquisition run.

Estimated remaining time is a labeled estimate over known populations and
measured rates. It may be unavailable while totals are still being discovered.

### 4.12 Single-owned usage and cost

One actual provider/tool usage event creates one immutable ledger entry owned
by the originating operation attempt. Stage, analyzer, acquisition, analysis,
and account views aggregate that entry through explicit membership and
attribution relationships; none copies it as a second owned charge.

Required distinctions:

- reserved worst case versus observed usage;
- provider usage units versus locally calculated money;
- newly incurred versus historically reused cost;
- attached-child attributable spend versus acquisition ownership;
- pre-detachment versus post-detachment spend;
- original provider receipt versus later adjustment/reconciliation; and
- budget authority versus actual billing.

The database transaction that dispatches billable work must also establish its
operation attempt, idempotency/request identity, and reservation against every
applicable hard limit. RQ-034 owns the exact reservation/deadline/reconciliation
algorithm. Until it passes EVAL-0081, M1 should not dispatch concurrent
billable attempts against a shared limit.

### 4.13 Retention and deletion safety

Deletion is a planned graph operation, not a queue `Delete` call or filesystem
cache clear.

Before confirmation, a deletion plan reports:

- selected runs, nodes, attempts, checkpoints, artifacts, sources, and ledger
  records;
- active/paused resumability and in-flight work affected;
- downstream reuse, replay, audit, citation, and coverage effects;
- independently retained exports, run-owned outputs, traces, or rendered
  copies;
- whether a cascade is required; and
- the exact gaps/tombstones that will remain.

Recommended M1 safety rule:

- do not delete a dependency of a queued, running, waiting, retrying, pausing,
  paused, or cancelling run;
- require the user to cancel and reach a terminal state first;
- never silently delete an independently retained copy;
- apply the confirmed deletion set transactionally where possible;
- retain a minimal non-sensitive deletion receipt/tombstone when policy
  permits; and
- recompute replayability/audit-gap projections after deletion.

This stricter M1 rule may later be relaxed only if a transactional operation can
prove that deletion simultaneously and honestly terminalizes/invalidate all
affected active work.

## 5. Alternatives

### 5.1 Comparison

| Alternative | Strengths | Contract mismatch / cost | Disposition |
|---|---|---|---|
| **Application-owned relational ledger and scheduler** | Exact Infinium ownership/state semantics; one local source of truth; direct evidence/cost/deletion joins; no server dependency; stack-neutral contract; UI-independent persistence | Must implement and test transitions, leases, retry, recovery, and migrations; poor design could recreate a workflow engine badly | **Recommend**, bounded to single-machine coordination and explicit work-unit checkpoints |
| **Dapr Workflow 1.18.2** | Built-in durable history, pause/resume/terminate, retries, child workflows, .NET SDK, and SQLite-compatible state store | Adds `daprd`, actor placement, an event-sourced history, replay/versioning rules, and at-least-once activities; in-flight activity termination is not immediate; standard parent/child behavior does not implement Infinium detachment/cost segments; Infinium still needs domain/evidence records | Reject for M1 by owner decision; reconsider only if future workflow complexity materially exceeds the bounded local scheduler |
| **Temporal 1.31.2** | Mature durable execution, recovery, retries, cancellation, child close policies, strong operational tooling | Requires a Temporal service and persistence plus workers; cooperative product pause and Infinium terminal/new-run semantics remain application logic; duplicates retained history; disproportionate for one local desktop | Reject for M1/M3 local architecture; reconsider only for an explicitly accepted operated/distributed product |
| **Restate 1.7.2** | Relatively lightweight single server binary, durable journal, retry/recovery, pause/resume, cancellation propagation, idempotency, and fencing | Separate server/RocksDB truth; no supported official .NET workflow SDK in the surveyed release; no Infinium detachment/cost/provenance/deletion semantics; still needs the evidence store | Do not select for the .NET desktop baseline; reconsider if an official stable .NET surface and a materially better lifecycle fit emerge |
| **Hangfire 1.8.24** | Mature .NET persistent jobs, restart recovery, retries, separate workers, monitoring | Locks the stack before RQ-016; at-least-once/reentrant model; official durable stores favor SQL Server/Redis; the cited batch orchestration and batch-cancellation surface is Pro; no exact run/checkpoint/reuse/deletion/cost model | Reject as authoritative orchestrator; a later selected .NET stack may reuse isolated low-level ideas, not Hangfire identity |
| **Quartz.NET 4.x** | Mature calendar, cron, trigger, hosted-service, and persistent-store machinery | Solves *when a trigger fires*, not immutable analysis/acquisition identity, dependency-aware checkpoints, pause/cancel semantics, cost reservation, publication, or deletion; adds a second persistent job schema if used as authority | Do not use as M1 lifecycle authority; reconsider only for future calendar scheduling, with Quartz triggers mapped to ordinary user-authorized Infinium operations |
| **Coravel** | Small, idiomatic .NET scheduling/queue API with low setup cost | Its queue is in-memory and therefore loses queued authority on process failure; its scheduled invocables still need the full durable Infinium ledger | Reject as durable authority; no clear advantage over first-party primitives for the bounded coordinator loop |
| **.NET Generic Host + bounded Channels + timer/cancellation primitives** | First-party lifecycle, DI, cancellation, backpressure, and periodic-wakeup machinery; no second durable store | Does not implement retries, leases, checkpoints, recovery, or domain transitions; Infinium must still implement those against its ledger | **Recommend as implementation substrate**, subject to milestone-level package/API qualification; never treat the in-memory channel as durable truth |
| **In-memory queue plus serialized checkpoint files** | Minimal prototype code | Split-brain truth, weak atomic transitions, difficult deletion/cost joins, UI/process restart races, and unsafe concurrent publication | Reject |
| **Use provider job IDs as the job store** | Avoids local polling for provider work | Network/provider-specific, incomplete local-work coverage, retention/cancellation drift, no snapshot/acquisition ownership or offline behavior | Reject |
| **One OS process per whole run, process lifetime equals state** | Strong simple crash isolation | Process death cannot distinguish pause/cancel/failure, loses durable progress, and cannot model reuse, receipts, or UI restart | Reject |

### 5.2 Why the custom-ledger candidate is bounded rather than “build Temporal”

Infinium does not need arbitrary durable code replay, timers lasting years,
distributed consensus, cross-service RPC, schedules, or multi-machine
failover. It needs a finite DAG of declared work units over immutable inputs,
with explicit checkpoints and unusually strict provenance/cost/deletion
semantics.

The custom boundary must therefore prohibit:

- arbitrary workflow code whose local variables are reconstructed by replay;
- silent infinite retry;
- mutable reopening of terminal runs;
- queue identity as domain/run identity;
- exactly-once claims for external side effects;
- automatic work not traceable to a user initiation or accepted maintenance
  exception; and
- framework purge/retention operations bypassing Infinium deletion planning.

If later requirements add operated services, multi-machine execution, or
unbounded event-driven workflows, the rejection should be revisited rather
than expanding the local scheduler into those domains.

The coordinator loop itself should therefore be small:

1. query the authoritative ledger for eligible work;
2. atomically claim a finite unit under the current fence and limits;
3. place only that already-authorized assignment in a bounded in-memory
   channel or launch slot;
4. supervise execution and stage its receipt; and
5. transactionally admit the receipt or recover the expired attempt.

The channel, timer, or hosted service can be replaced without changing the
ledger. This is the key distinction between using a scheduling library and
delegating product truth to one.

## 6. Bounded M1 subset

M1 should prove the lifecycle contract without delivering every M3 control.

### 6.1 Include in M1

- immutable analysis and acquisition run records;
- immutable resolved bindings/configuration and owner-run relationships;
- transactionally validated state transitions and attempt history;
- one coordinator lease/fencing epoch;
- at least one isolated worker process;
- job-unit checkpointing and recovery after forced worker/coordinator exit;
- cooperative pause/resume at declared unit boundaries;
- terminal cancellation and new-run validated reuse;
- retry-safe versus retry-unsafe operation declarations;
- durable real-population progress and gap states;
- one-owned usage/cost entries and non-duplicating rollups;
- attached analysis/acquisition provenance and the detachment-capable data
  model;
- deletion preview plus blocking of active/paused dependency deletion;
- paginated/read-only UI/CLI queryability after UI restart; and
- human-readable CLI plus versioned JSON lifecycle/provenance output.

### 6.2 May remain unavailable or deliberately single-threaded in M1

- user-facing active child detachment/continuation controls, provided attached
  children simply stop with the parent and the schema already preserves the
  future boundary;
- concurrent billable attempts or shared billable limits before RQ-034 and
  EVAL-0081;
- OpenAI background Responses, Batch, and prompt caching;
- pause inside an indivisible parser/tool/provider call;
- several simultaneous analysis runs;
- multi-coordinator operation;
- dynamic work stealing;
- automatic checkpoint compaction/garbage collection;
- calibrated ETA/presets;
- automatic retention expiry; and
- survival across Windows sign-out, reboot, or application upgrade without a
  separately tested recovery/migration plan.

M1 can use synchronous OpenAI calls and conservative concurrency. A provider
call already dispatched when pause/cancel is requested remains an explicitly
uninterruptible attempt unless the qualified adapter proves another behavior.

## 7. Experiments and artifacts

### 7.1 Read-only release qualification

On 2026-07-28, the GitHub REST `releases/latest` endpoint was queried with a
non-secret user-agent for:

- `temporalio/temporal` → `v1.31.2`, published 2026-07-08;
- `dapr/dapr` → `v1.18.2`, published 2026-07-21;
- `restatedev/restate` → `v1.7.2`, published 2026-07-06; and
- `HangfireIO/Hangfire` → `v1.8.24`, published 2026-07-16.

No credentials, repository writes, services, workers, or durable runtimes were
started.

### 7.2 Why no framework prototype was run

A runtime benchmark before RQ-013/RQ-016/RQ-017 selects a store, language, and
process boundary would mostly measure installation and default configuration,
not the difficult contract. Dapr or Temporal “hello workflow” recovery would
not prove acquisition ownership, terminal cross-run reuse, cost attribution,
or deletion safety.

The owner decided that a Dapr comparison prototype was not worth delaying the
architecture decision. RESEARCH-0046 retains the unexecuted comparison plan.
This does not waive implementation proof for the selected SQLite lifecycle:
the M1 architecture spike must still exercise its state machine,
forced-failure matrix, and boundedness. RESEARCH-0023's existing checkpoint
envelope supplies preliminary size/timing targets but did not survive a
process crash and is not treated as proof here.

## 8. Uncertainty and limitations

1. RQ-013 may find that evidence queries require a physical store different
   from SQLite. The logical ledger recommendation survives; exact transaction,
   WAL, migration, backup, and blob choices may change.
2. RQ-016/RQ-017 may change how the coordinator and workers are hosted. The
   UI-independent authority, lease/fencing, and isolated-publication contract
   remains.
3. No crash/power-loss test has yet proven transactional artifact publication
   across the database and filesystem blob layer.
4. SQLite WAL supports one writer at a time. That aligns with the recommended
   coordinator, but high-volume progress/event writes and long UI reads require
   prototype measurement and checkpoint discipline.
5. Lease expiry and clock use require care. Correctness must derive from
   transactional fencing, not wall-clock expiry alone.
6. Provider request recovery may remain ambiguous when dispatch succeeded but
   no durable receipt was stored. Adapter-specific idempotency/status lookup is
   required; otherwise the node must abstain from automatic retry.
7. Windows Job Objects contain local process trees but do not sandbox file or
   network authority and cannot cancel remote provider work.
8. Child detachment is specified logically but not prototyped under races
   between dispatch, detachment, completion, and delayed billing.
9. Progress denominators may expand during discovery. The denominator-revision
   model needs UI testing so users do not interpret an honest changing total as
   regression.
10. Automatic cleanup and database backup/restore are not selected. Both must
    preserve run/checkpoint/content identities and deletion receipts.
11. Framework release behavior is current as of 2026-07-28 and may change.
    Dapr, Temporal, Restate, queues, and calendar schedulers are excluded for
    the fit and operational reasons above. Dapr was rejected by owner
    judgement without an Infinium prototype.
12. This report does not prove the proposed RESEARCH-0023 latency, memory, or
    disk targets.

## 9. Accepted recommendation

Confidence:

- **High** that run/acquisition ownership, lifecycle, checkpoint reuse, cost,
  and deletion must remain Infinium domain records regardless of scheduler.
- **High** that UI memory, provider IDs, and process lifetime cannot be the
  authoritative job store.
- **High** that a generic queue such as Hangfire is insufficient as the
  authoritative lifecycle model.
- **Medium-high** that a custom transactional relational ledger is less complex
  overall than wrapping a durable-workflow server while maintaining a second
  domain store.
- **Medium-high** that one leased coordinator plus isolated workers is the
  correct local-first process model.
- **Medium-high** that SQLite is the correct physical database, as subsequently
  selected by ADR-0015; prototype measurements and implementation qualification
  remain pending.
- **Medium-low** for exact checkpoint frequency, concurrency, lease timeout,
  and event-retention defaults.

Accept the application-owned SQLite lifecycle. The implementation must
preserve the logical run identity, ownership, checkpoint/reuse, cost,
provenance, and deletion contract and remain bounded to the finite local DAG
described here.

## 10. Accepted ADR disposition

### Accepted title

`ADR: Application-owned durable run and job lifecycle`

### Accepted decision content

ADR-0016 accepts:

1. Infinium run IDs—not queue/workflow/provider IDs—as domain identity.
2. Separate immutable analysis and acquisition run ownership.
3. One owner run/node/attempt for every checkpoint, output, reservation, and
   actual usage entry.
4. Append-only transition/attempt/ledger history plus transactional current
   projections.
5. A single leased coordinator with monotonically increasing fencing epochs.
6. Isolated idempotent work units and stale-worker publication rejection.
7. The state and transition semantics in section 4.3.
8. Same-run cooperative pause/resume.
9. Terminal cancellation/limit/failure/invalidation and new-run-only reuse.
10. Dependency-complete immutable checkpoints and explicit reuse proofs.
11. Attached-child control/attribution links and event-sequence detachment
    cutovers without ownership transfer.
12. Single-owned usage/cost entries and non-owning rollups.
13. Deletion planning that cannot silently corrupt active work or retained
    artifacts.
14. No external durable-workflow framework for M1.
15. A bounded single-machine scheduler; distributed orchestration requires a
    later ADR.

### Decisions to leave for the integrated Wave E architecture

- SQLite or another exact store/version/binding;
- migration and backup mechanism;
- coordinator lifetime and executable packaging;
- worker protocol/IPC and UI query API;
- exact worker/process pool;
- exact budget algorithm from RQ-034; and
- exact encryption/credential and protected-path controls from RQ-018/RQ-032.

## 11. Evaluation mapping

| Evaluation | Required addition from this report |
|---|---|
| `EVAL-0018` | Force UI, worker, and coordinator restarts at enumeration, parsing, checkpoint, and provider boundaries; measure durable resume, checkpoint size/read/write, progress, cost, and gaps at high scale. |
| `EVAL-0021`, `EVAL-0025` | Retained boundary outputs replay without re-dispatch; deleted/missing dependencies produce exact replay/audit gaps without lifecycle mutation. |
| `EVAL-0024` | A new run consumes an origin-preserving checkpoint/output reuse edge only after complete dependency proof. |
| `EVAL-0026` | Editing context/configuration during queued/running/paused work never changes the bound run or its checkpoint identity. |
| `EVAL-0037` | Clean recomputation bypasses derived checkpoints without refreshing source inputs; source refresh remains a separate acquisition run. |
| `EVAL-0038` | Exercise every legal/illegal state transition, pause with interruptible and uninterruptible work, terminal cancel, active-run retry, terminal new-run reuse, node-scoped limit, attached child, and detachment race. |
| `EVAL-0039` | Prove acquisition owns its jobs/calls/claims/cost before and after attachment/detachment; analysis uses explicit application links. |
| `EVAL-0040` | CLI/JSON output reports owner run, transitions, attempts, checkpoint/reuse, progress, cost, and gap provenance without becoming an export. |
| `EVAL-0041` | Deletion is blocked for non-terminal dependencies; preview finds every affected checkpoint, source, export, run output, and trace; confirmed deletion leaves honest gaps. |
| `EVAL-0044` | One provider receipt produces one owned ledger entry; attached rollup includes it once; delayed pre-detachment receipts remain parent-attributable; post-detachment dispatch does not. |
| `EVAL-0080` | Worker temp, checkpoint/blob, database, logs, and deletion paths remain product-controlled and reject protected-root aliases/reparse escapes. |
| `EVAL-0081` | Dispatch, reservation, operation attempt, and fencing identity commit atomically; crash/cancel/delayed adjustment cannot release or duplicate authority. |
| `EVAL-0083` | Every transition, attempt, checkpoint, provider receipt, reuse proof, attribution segment, and deletion gap resolves end to end. |

Required fault-injection cases include:

- worker dies before output staging, after staging, and during publication;
- coordinator dies before and after dispatch commit;
- stale worker reports after a new fencing epoch;
- pause races with completion and retry scheduling;
- cancel races with provider dispatch and receipt persistence;
- detachment races with dispatch and delayed cost reconciliation;
- checkpoint byte corruption or missing blob;
- database/artifact cleanup interrupted midway;
- UI closes/reopens repeatedly while work continues;
- changed input invalidates only dependent nodes; and
- same idempotency key returns an existing provider result versus an
  indeterminate dispatch.

No evaluation is marked passed by this research.

## 12. RQ-015 disposition

> **Resolved for M0 by the accepted application-owned durable run/job
> lifecycle ADR; exact store/process/IPC realization and implementation
> conformance pending.** Use immutable analysis/acquisition ownership, an
> append-only transactional lifecycle ledger, a leased fenced coordinator,
> isolated idempotent workers, cooperative same-run pause/resume, terminal
> cancellation, dependency-validated new-run reuse, single-owned cost entries,
> and deletion-safe dependency planning. Do not use a generic queue,
> provider job ID, process lifetime, or external durable-workflow history as
> Infinium's authoritative run model.

ADR-0016 was accepted by the owner on 2026-07-28. Exact schema, binding,
process/IPC realization, implementation, fault injection, and evaluation
conformance remain pending.

## 13. Semantic self-review

- Analysis and acquisition ownership remain separate.
- Attached/detached state changes control and attribution, never ownership.
- Pause resumes the same non-terminal run.
- Cancellation remains terminal; hard-limit exhaustion is terminal for the
  affected bounded node/run while unrelated parent work may continue.
- Retry within an active run and reuse from a terminal run are distinct.
- Checkpoint reuse preserves producer identity and requires complete dependency
  validation.
- Cost is owned once and aggregated without copying.
- UI restart does not imply worker restart or state loss.
- Process isolation does not claim sandboxing or provider cancellation.
- Deletion never silently cascades or corrupts paused work.
- SQLite is selected by accepted ADR-0015/ADR-0016; its exact binding, schema,
  and implementation conformance remain pending.
- The application-owned SQLite lifecycle is accepted; no implementation,
  evaluation, or conformance result is represented as complete.
- Dapr is rejected for M1 by owner judgement without a comparison prototype.
