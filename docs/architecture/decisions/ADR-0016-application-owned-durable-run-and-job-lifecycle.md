# ADR-0016: Application-owned durable run and job lifecycle

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Final disposition

The owner selected the application-owned SQLite lifecycle on 2026-07-28 and
closed RESEARCH-0046 without a Dapr prototype. Dapr remains technically
capable of durable workflow execution, but its sidecar/placement processes,
separate history and replay model, activity semantics, packaging,
supervision, security, and cross-store reconciliation are not justified for
Infinium's bounded single-machine M1 lifecycle.

## Context

Infinium must execute hours-long local and provider-backed work while preserving
immutable run bindings, honest progress, pause/cancel semantics, checkpoints,
cost ownership, failure isolation, validated reuse, and recovery across UI or
worker failure. Queue IDs, provider IDs, process lifetime, and renderer memory
cannot be authoritative run state.

RESEARCH-0037 found that Infinium's lifecycle and provenance requirements are
domain semantics that remain necessary regardless of scheduler. It proposed a
bounded application-owned SQLite lifecycle ledger as the simpler local design.
RESEARCH-0046 records the owner's decision not to add Dapr's separate runtime
and workflow-history boundary.

## Decision drivers

- Analysis, evidence acquisition, and accepted maintenance are different
  durable owners.
- Run inputs and configuration are immutable, including across pause/resume.
- Cancellation, failure, invalidation, and limit exhaustion are terminal.
- Every attempt, checkpoint, output, reservation, and usage entry needs one
  unambiguous owner.
- UI restart and stale workers must not mutate or republish authoritative state.
- Resume and cross-run reuse require different identity and validity rules.
- Progress must describe real work populations, gaps, and reused work.
- M1 is a single-machine product and does not need distributed workflow
  operations.

## Considered options

### Application-owned relational lifecycle ledger and bounded scheduler

This option directly represents Infinium runs, attempts, checkpoints,
transitions, progress, usage ownership, reuse, and deletion. It avoids a
second workflow history and server dependency, but requires careful
implementation and fault testing of transitions, fencing, recovery, and
publication.

### Dapr Workflow

Dapr supplies a .NET Workflow SDK, durable event-sourced execution,
pause/resume/terminate/query/purge operations, child workflows, retry/recovery,
and an actor-compatible SQLite state store. It also introduces `daprd`, actor
placement, workflow history and replay/versioning rules, at-least-once
activities, and a second persistence contract beside Infinium's evidence and
provenance model. It is rejected for M1 because these integration and
operational costs do not remove enough Infinium-specific lifecycle logic.

### Temporal or Restate

These systems provide durable execution, retries, cancellation, and child
workflow primitives. They also add a service/sidecar and separate durable
history while leaving Infinium's acquisition ownership, terminal new-run
reuse, detachment attribution, cost, provenance, and deletion semantics as
application logic. Temporal's operated service footprint and Restate's absence
of a stable official .NET workflow SDK make them weaker fits for the selected
local .NET desktop baseline. They are not finalists for M1.

### Hangfire or another persistent job queue

A generic queue can persist jobs and retry work, but its IDs, at-least-once
behavior, store choices, and orchestration model do not supply Infinium's run,
checkpoint, reuse, deletion, or accounting truth. It is not selected as the
authoritative lifecycle.

### Quartz.NET, Coravel, or first-party .NET background primitives

Quartz.NET is useful when a product needs calendar/cron triggers, while
Coravel offers lightweight in-process scheduling and queues. Neither supplies
Infinium's durable run/evidence/checkpoint/reuse/cost/deletion semantics, and
Coravel's queue is in-memory. First-party .NET hosted services, bounded
channels, timers, and cancellation primitives do fit the implementation
substrate: they can wake the coordinator and carry already-authorized
assignments without becoming durable authority.

### In-memory queues, checkpoint files, provider job IDs, or process lifetime

These approaches split or lose authority across restart, cannot atomically
join lifecycle with evidence/cost/deletion, and cannot distinguish terminal,
paused, failed, or reusable work reliably. They are rejected.

## Decision

1. Infinium owns a durable transactional SQLite lifecycle ledger and bounded
   single-machine scheduler. No external durable-workflow framework is an M1
   authority. The implementation should reuse qualified .NET hosting,
   bounded-channel, timer, cancellation, retry-delay, database, and
   process-supervision primitives. “Application-owned” refers to domain state
   and admission, not reimplementing general concurrency infrastructure.
2. Infinium run IDs, not queue, worker, process, or provider IDs, are domain
   identity.
3. Analysis runs and evidence-acquisition runs are separate immutable owners.
   Accepted managed-data maintenance is a separately typed maintenance
   operation and cannot own analysis evidence, findings, cases, or readiness.
4. Every job node belongs to exactly one owner run or maintenance operation.
   Every attempt, checkpoint, direct output, transition, failure, coverage
   result, reservation, and actual usage entry belongs to exactly one job node
   and owner. Views roll those records up without copying ownership.
5. Analysis may initiate, control, and consume an acquisition through explicit
   links. Attachment or detachment never transfers acquisition ownership or
   rewrites initiation provenance, prior work, or prior cost.
6. The durable state model separates append-only requested/observed transition
   history from a transactionally maintained, reconstructible current
   projection.
7. The minimum non-terminal states are `queued`, `running`, `waiting`,
   `retrying`, `pausing`, `paused`, and `cancelling`. The minimum terminal
   states are `cancelled`, `completed`, `completed-with-gaps`, `failed`,
   `limit-reached`, and `invalidated-by-changed-input`.
8. Legal transitions are governed by a versioned policy and use a
   compare-and-swap generation plus the current coordinator fencing epoch in
   one transaction. Terminal states never transition.
9. One authoritative lifecycle coordinator holds a durable lease with a
   monotonically increasing fencing epoch. Only that authority may advance
   lifecycle projections, claim or dispatch work, authorize budget, publish
   worker output, aggregate progress/cost, recover expired attempts, or
   finalize runs. The exact process lifetime and transport are selected
   separately.
10. Work executes as bounded declared units over immutable inputs. Each retry
    is a new attempt with its own lease/fence, dispatch identity, outcome, and
    idempotency/retry-safety declaration. Exactly-once external execution is
    never assumed.
11. Workers stage outputs under an attempt identity. Only the current fenced
    attempt may transactionally publish authoritative artifacts and terminal
    node state. A stale worker may finish computation but cannot publish.
12. Pause is cooperative. A pause request stops new dispatch, propagates to
    attached children by default, and enters `paused` only after all direct
    work has reached a declared safe boundary and no concealed work or spend
    can continue. A separately owned acquisition may continue only through an
    explicit authorization whose progress and attributable spend remain
    visible while the parent is paused. Indivisible external calls remain
    explicitly uninterruptible while the run stays `pausing`.
13. Same-run resume retains the same immutable run, inputs, configuration,
    node definitions, and valid checkpoints. It never adopts newer local,
    source, context, or configuration state.
14. Cancellation atomically stops new dispatch, requests cancellation of
    running and attached work, records any explicit child detachment, settles
    interruptible work and known receipts, and commits terminal `cancelled`.
    In-flight pre-cancellation work and late provider adjustments remain owned
    by the terminal run but cannot reopen or authorize new work.
15. `cancelled`, `failed`, `limit-reached`, and
    `invalidated-by-changed-input` work continues only through a new manually
    initiated run. Cross-run checkpoint or artifact reuse requires an explicit
    reuse edge and complete dependency proof under ADR-0010; the producer is
    never rebound or represented as newly incurred work.
16. Checkpoints are immutable and identify their producing
    run/node/attempt, completed partition or range, snapshot/context/config
    dependencies, source/tool/model/analyzer/schema versions, upstream
    closure, pending/gap state, progress-population revision, accounting
    references, and content fingerprint.
17. Parent/child control and future detachment use immutable initiation links
    plus versioned control and attribution segments. A detachment cutoff uses
    the authoritative dispatch/reservation event sequence: pre-cutoff work
    remains parent-attributable even when receipts arrive later; post-cutoff
    work requires the independently authorized continuation.
18. Progress is derived from versioned real work populations, including known
    or still-enumerating totals; completed, reused, queued, running, failed,
    skipped, unsupported, limited, invalidated, and gap units; and explicit
    denominator revisions. Retries do not inflate the denominator, and ETA is
    a labeled estimate that may be unavailable.
19. One provider/tool usage event creates one immutable actual-usage entry
    owned by the originating operation attempt. Analysis, acquisition,
    stage, analyzer, and account views aggregate it through references.
    Reservations, observed usage, calculated price, provider billing,
    historical reuse, attached attribution, and later adjustments remain
    distinct.
20. Deletion is a durable planned graph operation. A dependency of any
    non-terminal or paused/cancelling run cannot be deleted in M1; the user
    must first cancel and reach terminal state. Preview and execution preserve
    independent retained copies, identify resumability/replay/audit/coverage
    effects, and leave explicit gaps and receipts.
21. The UI and CLI are clients of durable lifecycle state. Their close, crash,
    reload, or restart changes no run state. Coordinator recovery acquires a
    new fencing epoch, reconciles staged artifacts and provider receipts,
    retries only contract-safe work, and leaves unresolved ambiguity visible.

## Explicit non-decisions and M1 exclusions

This ADR does not select the exact database schema, coordinator executable
lifetime, process topology, worker protocol, IPC/query transport, worker-pool
size, checkpoint cadence, lease duration, credential mechanism, or hard-budget
algorithm. It also does not pin a low-level scheduling/concurrency package.
The M1 plan may select first-party .NET primitives or a qualified library so
long as the durable ledger remains authoritative. Quartz-style calendar
scheduling is deferred until the product actually requires time-based runs.

M1 may deliberately omit or serialize:

- user-facing child continuation and detachment controls, while retaining the
  future-compatible identity and attribution model;
- concurrent billable attempts and several simultaneous analysis runs;
- OpenAI background Responses, Batch, and explicit prompt caching;
- pause inside an indivisible parser, tool, or provider operation;
- multi-coordinator operation, dynamic work stealing, and distributed
  orchestration;
- automatic checkpoint compaction, garbage collection, or retention expiry;
- calibrated ETA and user presets; and
- recovery across sign-out, reboot, or application upgrade until separately
  tested with migration and recovery.

## Consequences

### Positive

- Lifecycle, provenance, cost ownership, checkpoints, and deletion share one
  coherent product model.
- UI or worker failure cannot silently mutate active run truth.
- Pause/resume and new-run reuse retain precise, distinct semantics.
- The local product avoids operating a second workflow server and reconciling
  duplicate histories.

### Negative

- Infinium must implement and rigorously test a scheduler, state machine,
  leasing/fencing, idempotency, recovery, progress, and publication protocol.
- External work can remain ambiguous or uninterruptible; the product must
  expose that honestly rather than claim exactly-once or instant cancellation.
- Durable transition and attempt history add storage and migration overhead.

### Risks and mitigations

- **A custom scheduler grows into an unbounded workflow engine:** constrain it
  to a finite local DAG of declared work units and prohibit arbitrary durable
  code replay, silent infinite retry, and distributed features.
- **Stale workers publish after recovery:** require monotonically fenced
  dispatch and transactional publication checks.
- **Pause or cancel conceals spend:** separate requested and observed state,
  stop new dispatch atomically, and retain in-flight/provider receipts and
  conservative holds until reconciled.
- **Retry duplicates an external effect or charge:** record attempt identity
  and operation-specific idempotency/status capabilities; abstain from
  automatic retry when dispatch is ambiguous.
- **Deletion corrupts active or reusable work:** block active dependency
  deletion and require a version-bound graph preview and explicit receipt.

## Requirements affected

- SCOPE-004
- SCAN-003 through SCAN-009
- SNAP-001 through SNAP-006
- EVID-002, EVID-004, and EVID-007
- FIND-005, FIND-006, FIND-012, and FIND-014
- COVER-001 through COVER-003
- DOC-002, DOC-009, and DOC-011
- AI-004 through AI-007
- OPS-001 through OPS-005

## Validation

Before an implementation relies on this lifecycle:

- EVAL-0018, EVAL-0021, EVAL-0024 through EVAL-0026, EVAL-0037 through
  EVAL-0041, EVAL-0044 through EVAL-0046, EVAL-0080, EVAL-0081, and
  EVAL-0083 must be specified and passed for the exercised surfaces;
- every legal and illegal transition, generation/fence race, pause boundary,
  terminal state, node-scoped gap, retry policy, and dynamic progress
  denominator must be exercised;
- fault injection must terminate the UI, coordinator, worker, and subprocess
  before staging, after staging, during publication, and around provider
  dispatch/receipt persistence;
- stale workers/coordinators must be unable to publish or dispatch after a new
  fencing epoch;
- same-run resume must preserve immutable bindings, while every terminal
  continuation must create a new run and dependency-validated reuse edge;
- one provider receipt must produce one owned ledger entry and nonduplicating
  rollups before and after delayed child-detachment attribution;
- cancellation and limit exhaustion must prove that no new work is authorized
  after their authoritative sequence while late receipts remain reconcilable;
  and
- deletion preview must find every active, checkpoint, reuse, replay, audit,
  output, trace, and coverage dependency and must block non-terminal deletion.

## References

- [ADR-0002 — Snapshot and context binding](ADR-0002-snapshot-context-binding.md)
- [ADR-0010 — Snapshot fingerprint and dependency invalidation](ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
- [ADR-0013 — OpenAI-first LLM capability boundary](ADR-0013-openai-first-llm-capability-boundary.md)
- [ADR-0014 — LOOT managed-data refresh](ADR-0014-loot-managed-data-refresh.md)
- [RESEARCH-0037 — Job, checkpoint, and run lifecycle](../../research/investigations/RESEARCH-0037-job-checkpoint-and-run-lifecycle.md)
- [RESEARCH-0046 — Dapr Workflow desktop lifecycle qualification](../../research/investigations/RESEARCH-0046-dapr-workflow-desktop-lifecycle-qualification.md)
- [RESEARCH-0044 — Wave E architecture and security integration](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
- [Dapr Workflow overview](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-overview/),
  retrieved 2026-07-28
- [Temporal documentation](https://docs.temporal.io/), retrieved 2026-07-28
- [Restate key concepts](https://docs.restate.dev/foundations/key-concepts),
  retrieved 2026-07-28
- [Hangfire processing background jobs](https://docs.hangfire.io/en/latest/background-processing/processing-background-jobs.html),
  retrieved 2026-07-28
