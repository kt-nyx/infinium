# RESEARCH-0046: Dapr Workflow desktop lifecycle qualification

Status: Completed
Disposition: Closed; Dapr rejected by owner without prototype

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Not executed

Primary RQ: RQ-015 (resolved by accepted ADR-0016)

Related RQs: RQ-013, RQ-016, RQ-017, RQ-018, RQ-032, RQ-034

M0 wave: E — Architecture and stack selection amendment

Decision enabled: final run/job lifecycle ADR disposition

Acceptance: Closed by owner decision on 2026-07-28

## Final disposition

The owner selected the thin application-owned SQLite lifecycle and rejected
Dapr Workflow for Infinium's M1 architecture. No Dapr or comparison prototype
was executed.

This is a deliberate product/architecture judgement rather than an empirical
claim that Dapr cannot implement the required workflow. Dapr's useful
durability, retry, pause/resume, and recovery features do not remove
Infinium's domain-specific run identity, evidence ownership, provenance,
cost, checkpoint-reuse, publication, coverage, and deletion semantics.
Adopting it would add `daprd`, placement, Dapr workflow history,
replay/versioning, activity semantics, packaging, supervision, security, and
cross-store reconciliation to a local single-machine desktop product. The
owner judged that integration and operational surface more costly than the
bounded lifecycle code it might replace.

ADR-0016 therefore accepts an application-owned transactional SQLite
lifecycle ledger and bounded scheduler. Dapr, Temporal, Restate, generic
persistent queues, and calendar schedulers do not own run identity or durable
lifecycle truth in M1. The implementation should still use standard .NET
hosting, async, cancellation, bounded-channel, timer, process-supervision,
retry-delay, and SQLite libraries rather than hand-writing general concurrency
infrastructure.

The unexecuted qualification plan below is retained as decision provenance and
as a ready-made reconsideration checklist if future requirements add
multi-service, distributed, or substantially more complex workflow needs.

## Closed executive question

Should Infinium use Dapr Workflow as its durable orchestration substrate, or
should it use the leading thin application-owned SQLite lifecycle proposed in
RESEARCH-0037 and ADR-0016?

This is not a comparison between Dapr and raw threads. Both candidates may use
ordinary .NET hosting, async, cancellation, process-supervision, and database
libraries. The decision is which layer owns durable orchestration:

- **Dapr candidate:** Dapr owns workflow history, replay, durable scheduling,
  activity recovery, and workflow lifecycle; Infinium still owns domain
  identity, evidence, provenance, costs, coverage, findings, and deletion
  policy.
- **SQLite candidate:** Infinium's SQLite transactions own both the domain
  lifecycle ledger and admission/publication protocol; bounded .NET
  background primitives execute already-authorized work.

The earlier observation that Dapr has capabilities beyond M1 is not itself a
valid rejection reason. Dapr should be rejected only if its concrete Windows
desktop footprint, failure semantics, dual-store consistency, security,
versioning, or implementation burden is worse for this product than the
bounded SQLite design.

## Why the question was reopened before closure

RESEARCH-0037 correctly established that run identity, immutable bindings,
analysis/acquisition ownership, checkpoints and reuse, cost attribution,
coverage, provenance, and deletion effects are Infinium domain semantics. It
did not establish whether Dapr can remove enough scheduler, recovery, retry,
and workflow-state-machine code to justify a separate workflow runtime and
history.

The owner subsequently chose the application-owned SQLite approach without
running the planned comparison. The reasoning and unexecuted plan are retained
above and below.

## Current official capability baseline

As of Dapr documentation version 1.18, retrieved 2026-07-28:

- Dapr Workflow provides .NET authoring support and HTTP/gRPC management for
  start, query, pause/resume, terminate, events, and purge.
- The Workflow engine runs inside `daprd`, uses the actor runtime and a
  gRPC stream to application code, and persists event-sourced workflow
  history in an actor state store.
- Self-hosted operation without Docker installs at least `daprd` and the
  actor `placement` service; actors require an ETag-capable transactional
  state store.
- SQLite is listed as a supported Workflow state store.
- Workflow activities recover through reminders and may execute again after a
  crash. Termination does not stop an already in-flight activity.
- Parent termination terminates its Dapr child workflows.
- Completed workflow history remains until explicit purge or configured
  retention removes it.

These facts make Dapr a credible contender. They also identify the areas that
the proposed comparison would have tested rather than assumed.

## Unexecuted qualification plan

Everything from this point through **Required output** is the comparison plan
that would have governed a Dapr-versus-SQLite prototype. It was not executed
and is not pending work for M0 or M1. Imperative and future-tense wording in
those sections records the planned method; it does not override the final
disposition or accepted ADR-0016 above.

## Governing invariants

Either candidate must preserve:

1. immutable analysis and evidence-acquisition run identities and bindings;
2. explicit ownership for every job, attempt, checkpoint, output, reservation,
   actual-usage entry, and provider receipt;
3. same-run cooperative pause/resume and terminal cancellation/failure/limit/
   invalidation;
4. new-run-only continuation after terminal state, with dependency-complete
   origin-preserving reuse;
5. coordinator-only authoritative admission and publication, including stale
   worker/output fencing;
6. no automatic work or spend without the required user or accepted
   maintenance authorization;
7. exact single-owned cost rollups and conservative pre-dispatch limits;
8. explicit analysis/acquisition attachment, control, and future detachment
   attribution without ownership transfer;
9. honest progress, unsupported/gap state, replayability, and auditability;
10. dependency-aware retention and deletion that cannot silently destroy
    active or reusable work; and
11. UI/CLI restarts that do not mutate or terminate authoritative work.

## Qualification work

### 1. Desktop packaging and operation

Build and document the smallest supported Windows self-hosted configuration:

- exact Dapr runtime, .NET SDK, SQLite component, and configuration versions;
- every shipped or launched binary and process, including `daprd` and
  placement;
- all local ports, sockets, health/metrics endpoints, files, logs, and
  configuration roots;
- startup, shutdown, orphan cleanup, update, rollback, and uninstall behavior;
- whether Infinium can package and supervise the runtime without Docker,
  administrator access, or a separate user-installed Dapr environment; and
- cold-start, idle, active-scan CPU/memory, disk, and workflow-history growth.

The release dependency, notice, vulnerability/update, and GPL-compatible
redistribution implications must be recorded; no bundling decision is implied.

### 2. Like-for-like lifecycle fixture

Implement the same minimal generic finite-DAG fixture twice:

- once with Dapr Workflow plus the minimum Infinium domain records; and
- once with the thin SQLite ledger plus bounded .NET background primitives.

The fixture must cover enumeration, parallel independent work, dependency
joins, an uninterruptible external call, retry-safe and retry-unsafe work,
staged output publication, checkpoints, attached acquisition, dynamic
progress, and a billable-operation reservation/receipt.

Do not use Skyrim-, mod-, NPC-, or fixture-name-specific shortcuts.

### 3. Failure and lifecycle matrix

For both implementations, force failure:

- before and after dispatch authorization;
- during activity execution;
- before and after output staging;
- during authoritative publication;
- after provider dispatch but before receipt persistence;
- during pause, resume, cancellation, retry, and finalization;
- in the UI, coordinator, worker, `daprd`, placement service, and state store;
  and
- across application restart, Windows reboot, and a pinned-version upgrade
  where practical.

Measure duplicate execution, stale publication, unauthorized post-terminal
dispatch, recovery latency, manual-repair requirements, lost progress,
incorrect cost ownership, and unreconciled ambiguity.

### 4. Pause, cancellation, children, and independent acquisition

Prove the exact mapping between Dapr and Infinium semantics:

- whether suspend stops new activity scheduling at the required boundary;
- how already-running activities finish and how their late outputs/charges are
  admitted or rejected;
- how terminal cancellation remains terminal even if Dapr reports later
  activity completion;
- whether analysis-initiated acquisition should be a child workflow, a
  separately scheduled workflow, or only an Infinium relationship; and
- how future detachment avoids Dapr's parent-termination cascade while
  preserving immutable initiation and dispatch-cutoff attribution.

### 5. Authority and atomicity

Draw the exact transaction boundaries for:

- workflow transition/history in the Dapr state store;
- Infinium run, evidence, provenance, cost, and publication records;
- pre-dispatch budget reservation;
- external-call dispatch identity and ambiguity;
- activity completion and authoritative output publication; and
- deletion, purge, retention, replay, and backup.

The investigation must show whether one physical SQLite database can safely
host both Dapr and Infinium data without implying a cross-schema atomic
transaction Dapr does not expose. If two commits remain, specify the
outbox/inbox, idempotency, fencing, and reconciliation protocol and demonstrate
that it cannot double-authorize spend or publish stale work.

### 6. Query, provenance, retention, and evolution

Determine:

- whether the application can derive required run/stage/analyzer progress and
  provenance without reading Dapr's internal storage schema;
- which Dapr history and custom status are authoritative versus diagnostic;
- how histories map to immutable Infinium runs and attempts;
- how checkpoint reuse into a new run differs from workflow replay,
  retry, restart, and continue-as-new;
- how product deletion preview coordinates with Dapr purge and retention;
- backup/restore and corruption-repair behavior across both stores; and
- deterministic replay/versioning constraints for application upgrades,
  migrations, renamed activities, and changed workflow code.

### 7. Local security boundary

Identify and test:

- endpoint binding and authentication/authorization options;
- access to health, metrics, workflow management, state, and placement
  surfaces from another local process;
- component/configuration injection and protected-root/reparse behavior;
- log and diagnostic leakage of paths, inputs, credentials, or retained
  evidence;
- least-privilege process launch and Job Object supervision; and
- safe failure when the runtime or component configuration is missing,
  altered, incompatible, or malicious.

## Decision rule

Dapr is selected only if the like-for-like evidence shows that it materially
reduces Infinium-owned lifecycle, recovery, and failure-handling code or risk
without:

- creating two competing sources of lifecycle truth;
- requiring fragile cross-store consistency for publication or paid dispatch;
- weakening terminal, pause, child/detachment, reuse, provenance, cost, or
  deletion semantics;
- imposing an unreasonable Windows desktop process, resource, installation,
  update, repair, or security burden; or
- making application queries and schema/version evolution dependent on
  unstable Dapr internals.

Dapr is ruled out if any required invariant cannot be represented safely, or
if preserving the invariants leaves essentially the same custom lifecycle
ledger plus an additional sidecar/history/operations burden.

The SQLite candidate is not accepted merely because Dapr is rejected. The
final report must also show that the thin SQLite baseline stays bounded and
does not recreate a general workflow engine. The owner will review the
evidence and revised ADR-0016 before either option becomes authoritative.

## Required output

The completed investigation must provide:

- a concise conceptual answer;
- exact versions, configuration, and primary sources;
- process/data/authority diagrams for both candidates;
- the generic fixture and reproducible commands;
- a completed failure/semantic matrix with observed results;
- resource and history-growth measurements;
- security and upgrade findings;
- an implementation-complexity comparison based on actual prototype code,
  not feature counts;
- explicit remaining uncertainties and deferred capabilities;
- a recommendation to select Dapr or the thin SQLite lifecycle; and
- an amended proposed ADR-0016 and any required ADR-0015/ADR-0018/ADR-0019
  amendments with no mixed or implicit authority.

No evaluation case is marked passed and no architecture is accepted by
planning this investigation.

## Primary sources to revalidate

- Dapr [Workflow overview](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-overview/)
- Dapr [Workflow architecture](https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-architecture/)
- Dapr [.NET Workflow management operations](https://docs.dapr.io/developing-applications/sdks/dotnet/dotnet-workflow/dotnet-workflow-management-methods/)
- Dapr [self-hosted operation without Docker](https://docs.dapr.io/operations/hosting/self-hosted/self-hosted-no-docker/)
- Dapr [state-store capability reference](https://docs.dapr.io/reference/components-reference/supported-state-stores/)
- Microsoft [.NET hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)
- Microsoft [`System.Threading.Channels`](https://learn.microsoft.com/en-us/dotnet/api/system.threading.channels)
- SQLite [atomic commit](https://www.sqlite.org/atomiccommit.html) and
  [isolation](https://www.sqlite.org/isolation.html)

All current external facts must be rechecked when the investigation begins.
