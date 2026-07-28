# ADR-0018: Process and authority topology

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Context

Infinium needs a responsive desktop client and human-readable CLI while
long-running analysis, durable job recovery, database access, crash-prone
parsing, external-tool execution, and authenticated provider operations remain
outside presentation memory. The process split must also preserve the accepted
read-only product boundary and ensure that a worker's or helper's output does
not become authoritative merely because a child process produced it.

[RESEARCH-0039](../../research/investigations/RESEARCH-0039-process-and-data-query-boundary.md)
compared embedded, service, and standalone-coordinator topologies.
[RESEARCH-0040](../../research/investigations/RESEARCH-0040-credential-entry-and-storage.md)
and
[RESEARCH-0041](../../research/investigations/RESEARCH-0041-security-boundary-controls.md)
then refined the credential/provider helper and worker boundaries.
[RESEARCH-0044](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
resolved one wording conflict: the coordinator, not the desktop host, launches
and authorizes the credential/provider helper. The desktop host may request
and parent the helper's native UI but never grants it independent authority.

This ADR selects process ownership and lifetime. It does not select the
presentation stack, replaceable local transport, credential-store semantics,
general desktop security controls, or physical database implementation.

## Decision drivers

- Exactly one Infinium authority must own database access, durable scheduling,
  operation authorization, authoritative queries, and result publication.
- Presentation crashes or reloads must not corrupt or silently cancel durable
  work.
- Crash-prone and native analysis must be failure-isolated from the UI and
  durable authority.
- General workers must not gain database, credential, provider-dispatch, or
  publication authority.
- Secret bytes must not enter the shell, coordinator, general workers, or
  ordinary application/worker IPC.
- The CLI must exercise the same authoritative contracts as the desktop client
  and remain usable for local M1 work.
- The initial product is per-user, non-elevated, local, and manually initiated;
  it is not a Windows service or remote server.

## Considered options

### Embed the engine and database in each UI or CLI process

This minimizes the number of executables, but it couples durable work to client
lifetime, creates competing database and migration owners, encourages direct
database queries, and makes shell replacement or crash recovery unsafe.

### Run a persistent Windows service

A service could outlive sign-out and centralize ownership, but it adds
installation, elevation, cross-session identity, ACL, update, and recovery
complexity that the single-user local product does not need through M4.

### Use a standalone per-user coordinator with bounded child processes

This centralizes durable authority without service installation, allows the
shell and CLI to be replaceable clients, and permits isolated workers and a
separate secret-bearing helper. It adds local process supervision and IPC, but
those costs directly implement required durability, responsiveness, and
least-authority boundaries.

## Decision

Infinium shall use the following process and authority topology.

### Coordinator

One standalone, non-elevated, per-user .NET coordinator is the sole runtime
authority for:

- every connection to the authoritative application database, including
  migration, backup coordination, reads, and writes;
- durable run/job transitions, leases, fencing, dispatch, checkpoints, and
  recovery;
- authoritative application queries and projections;
- path, operation, source, provider, credential-generation, deadline, and
  budget authorization;
- worker/helper launch, assignment, cancellation, supervision, and result
  admission;
- authoritative payload adoption and durable publication; and
- audit, progress, coverage, cost, and typed failure records.

The coordinator may use multiple short-lived internal read connections where
the accepted persistence design permits them. No other process may open the
authoritative database.

Only the coordinator may admit staged worker/helper bytes into authoritative
payload storage and commit durable result references. A child-process success
code, manifest, path, hash claim, or transport acknowledgement is not
publication authority.

### Application clients

The desktop host and CLI are clients of the coordinator's versioned
application contract.

The desktop host owns native window and renderer lifecycle,
presentation-boundary validation, and narrowly accepted native presentation
operations. The renderer owns presentation state only. Neither the renderer
nor host parses mod artifacts, opens the database, invokes analysis tools,
schedules durable work independently, or publishes domain truth. ADR-0017
separately selects the concrete desktop and renderer technologies.

The CLI may start a coordinator when none currently owns the store, connect to
an existing compatible coordinator, submit explicit supported commands, query
state, stream bounded progress, and render human-readable or versioned
run-owned output. It has no normal direct-database or embedded-engine mode.

### General workers

The coordinator launches isolated workers for bounded parser, analyzer,
native-library, and approved external-tool assignments. Each worker:

- receives one immutable, bounded assignment and only its required non-secret
  capabilities;
- stages output only in its assigned per-attempt product temporary area;
- emits bounded untrusted progress, diagnostics, and a typed output manifest;
- cannot open the Infinium database or authoritative payload namespace;
- cannot publish lifecycle, evidence, finding, case, cost, or completion
  state;
- cannot accept external clients or call the renderer; and
- receives no credential, Credential Manager target, provider-dispatch
  authority, or provider secret.

Workers and their permitted descendants are coordinator-owned process trees.
They do not outlive the coordinator. Job Object containment may enforce
lifetime and resource limits, but it must not be described as a
filesystem/network security sandbox.

### Credential/provider helper

Credential enrollment/replacement and authenticated provider dispatch use a
dedicated one-shot helper, not the coordinator or a general worker, when the
operation uses a reusable provider secret such as a Nexus or OpenAI Platform
API key.

The coordinator records the exact non-secret intent or dispatch assignment and
then launches and authorizes the exact helper through inherited private
handles. The desktop host may request the operation and parent/present its native
dialog; it neither launches an independently authorized helper nor receives
the entered secret.

The helper has no database, general application-query, parser/tool, or durable
publication authority. Secret bytes and the Credential Manager target do not
cross ordinary application or general-worker IPC. The helper returns only
bounded non-secret status/usage and staged-output manifests for coordinator
validation and admission.

### Process lifetime and reachability

- The coordinator is not a Windows service, does not run elevated, accepts no
  remote clients, and does not survive user sign-out.
- It may outlive the desktop shell or CLI only while active work or an explicit
  keep-running choice requires it.
- Paused work is durable and does not require the coordinator to remain
  running.
- A later coordinator recovers through the accepted fenced durable lifecycle;
  stale workers or coordinators cannot publish.
- Workers and helpers are coordinator descendants and terminate with their
  bounded assignment or owning coordinator.
- Renderer, shell, and CLI exit or reconnect does not itself start, pause,
  cancel, resume, or otherwise mutate durable work.

## M1 boundary and exclusions

M1 shall prove one coordinator, the CLI as a real client, at least one isolated
general worker, coordinator-only staged-output admission, and the one-shot
credential/provider helper before any reusable-secret authenticated provider
operation. It must also prove the lifecycle processes and authority division
accepted in ADR-0016.

M1 does not require:

- Windows-service or sign-in startup;
- remote, cross-user, or cross-platform clients;
- a persistent worker pool or general parallel billable execution;
- polished desktop presentation beyond the separately selected stack spike;
- AppContainer/LPAC or another stronger worker sandbox; or
- M4 packaging, update, repair, or uninstall behavior.

An operation whose accepted threat model requires stronger compromise
containment remains excluded until a separate prototype and decision select
that boundary.

## Consequences

### Positive

- Durable truth survives renderer and shell restarts.
- Database, scheduling, authorization, query, and publication ownership is
  unambiguous.
- Crash-prone analysis and native dependencies cannot directly corrupt the
  authoritative store.
- The graphical shell can be replaced without rewriting domain or job truth.
- General analysis workers remain secret-free.

### Negative

- The product requires process supervision, startup fencing, local IPC, and
  staged-result admission.
- Authenticated provider work needs a separate small executable and
  one-shot launch protocol.
- The coordinator becomes a critical local component whose compatibility and
  recovery behavior must be tested explicitly.

### Risks and mitigations

- A coordinator crash can interrupt work. Durable fenced attempts,
  checkpoints, staged-output reconciliation, and safe retries mitigate this.
- Same-user malicious code is outside the protection claimed by process
  separation alone. Narrow IPC, exact operation authorization, OS-backed
  credentials, and explicit threat-model disclosure limit the exposed
  authority.
- Job Objects can be mistaken for sandboxing. Documentation and evaluation
  must state that they constrain lifetime/resources, not ambient same-user
  filesystem or network rights.
- A client may receive an indeterminate command response. Durable idempotency
  and reconciliation through the application contract prevent duplicate
  commands.

## Requirements affected

- AUTH-001 through AUTH-003
- SEC-001 through SEC-003
- SCAN-004 through SCAN-008
- AI-003, AI-004, and AI-007
- OPS-001 through OPS-005

## Validation

Acceptance selects a design; it does not prove an implementation.

M1 validation shall include:

- `EVAL-0033`, `EVAL-0034`, and `EVAL-0035` for hostile content, secret
  exclusion, and narrow authority;
- `EVAL-0038`, `EVAL-0045`, and `EVAL-0046` for durable lifecycle, manual
  initiation, and read-only external operations;
- `EVAL-0080` and `EVAL-0081` for authorized writes and dispatch/budget races;
- `EVAL-0088` for coordinator startup races, process roles, inherited handles,
  child lifetime, staged output, coordinator-only publication, and crashes;
  and
- `EVAL-0089` before authenticated provider integration for helper and
  credential-lifecycle recovery.

Revisit this ADR if the selected persistence design cannot support one process
owner, the coordinator cannot meet measured responsiveness/recovery needs, a
remote or cross-user requirement is accepted, or an exercised worker requires
stronger isolation than this topology provides.

## References

- [ADR-0001](ADR-0001-evidence-authority-boundary.md)
- [ADR-0002](ADR-0002-snapshot-context-binding.md)
- [ADR-0003](ADR-0003-read-only-authority.md)
- [ADR-0010](ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
- [ADR-0013](ADR-0013-openai-first-llm-capability-boundary.md)
- [RESEARCH-0039](../../research/investigations/RESEARCH-0039-process-and-data-query-boundary.md)
- [RESEARCH-0040](../../research/investigations/RESEARCH-0040-credential-entry-and-storage.md)
- [RESEARCH-0041](../../research/investigations/RESEARCH-0041-security-boundary-controls.md)
- [RESEARCH-0044](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
- [RESEARCH-0046](../../research/investigations/RESEARCH-0046-dapr-workflow-desktop-lifecycle-qualification.md)
