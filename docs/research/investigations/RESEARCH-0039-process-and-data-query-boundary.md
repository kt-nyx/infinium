# RESEARCH-0039: Process and data-query boundary

Status: Completed; recommendations accepted

Date: 2026-07-28

Last reviewed: 2026-07-28

Researcher: Codex agent

Primary RQ: RQ-017

M0 wave: E — Architecture and stack selection

Decision enabled: Process/authority topology ADR and local IPC/application-query ADRs

Acceptance: Process/authority and IPC/query recommendations accepted by the
project owner through ADR-0018/ADR-0019 on 2026-07-28

Subsequent owner disposition: ADR-0019 accepts the named-pipe gRPC contract as
the general Infinium UI/CLI/coordinator/worker IPC design. The later Codex
app-server adapter proposal was rejected in ADR-0024, so it adds no process or
transport to this design. Reusable OpenAI Platform API-key dispatch remains
within the separately governed credential-helper boundary. RQ-015 was
subsequently resolved by accepted ADR-0016: RESEARCH-0046 closed without a
prototype and Dapr was rejected. ADR-0018 accepts the coordinator as the sole
Infinium database, scheduler, authorization, query, and publication authority.

## Executive answer

Infinium should use one **standalone, per-user .NET coordinator process** as
the only runtime owner of the accepted Infinium SQLite store, authoritative
query model, privileged-operation authorization, and result publication. The
WPF desktop host and CLI are clients. Isolated workers perform bounded
assigned work but cannot query the database or publish durable state
themselves. ADR-0016 assigns durable lifecycle authority to the
application-owned SQLite scheduler.

The recommended local client/coordinator transport is **gRPC over HTTP/2 on a
Windows named pipe**:

- ASP.NET Core has first-party gRPC-over-named-pipe support on .NET 8 and
  later;
- protocol-buffer contracts provide generated, versionable .NET clients;
- unary and server-streaming methods fit queries, controls, and progress;
- HTTP/2 and Kestrel named-pipe buffers provide bounded flow control; and
- Windows pipe ACLs can restrict the endpoint to the current user and
  elevation level without opening a TCP port.

React must not connect to that pipe. It communicates only with the minimal WPF
host through a separate, narrow, size-bounded WebView2 JSON message contract.
The host validates message origin and schema, maps allowlisted presentation
operations to the generated coordinator client, and returns presentation DTOs.
It is not a generic proxy.

The exact recommended topology is:

```text
React renderer
  -> validated WebView2 presentation messages
WPF desktop host                 Human-readable CLI
  -> generated gRPC client          -> generated gRPC client
         \                         /
          gRPC/HTTP2 over one current-user-only application pipe
                              |
              standalone per-user .NET coordinator
                - only SQLite connection owner
                - durable job/scheduler authority
                - query and projection service
                - operation/path/budget authorization
                - worker supervisor and result admission
                    /                         \
   private assigned worker channels    one-shot inherited handle channel
                    |                         |
       bounded .NET parser/tool workers      credential/provider helper
       - no database, UI, or secrets         - exact credential target only
       - no lifecycle-publication authority  - one authorized dispatch
                - stage outputs in per-attempt product temp
```

The coordinator may outlive a renderer or desktop-shell restart while work is
active. It is not a Windows service and does not survive sign-out. If it exits,
later startup recovers from the durable ledger under the accepted ADR-0016
fencing rules. Workers are descendants of the coordinator and do not outlive
it.

Every UI and CLI read is an allowlisted application query. Direct SQLite
access by the renderer, WPF host, CLI, worker, or external tool is prohibited.
Queries use server-side filtering, sorting, aggregation, bounded detail
expansion, and stable keyset cursors. Progress events are hints tagged with a
durable sequence/projection version; reconnect or overflow causes an
authoritative snapshot query rather than event replay being treated as truth.

At research time, this report recommended rather than accepted its ADRs and
treated RESEARCH-0036 through RESEARCH-0038 as upstream proposals. ADR-0015
through ADR-0019 now accept those dependencies and this report's process/IPC
recommendations.

## 1. Question and governing constraints

RQ-017 asks:

> Which process and data-query boundary, including whether IPC is needed,
> keeps the UI responsive at high-end scale?

The answer must preserve:

- immutable run, snapshot, context, and resolved-input identity;
- a single write owner for the accepted SQLite store;
- durable jobs independent of renderer and shell memory;
- read-only setup authority through M4;
- isolated crash-prone parsers, native libraries, and external tools;
- offline CLI operation when local inputs are present;
- responsive drill-down over high-end profiles and retained history;
- schema-validated, least-authority UI and worker boundaries;
- explicit cancellation, failure, stale, partial, and gap states; and
- replaceability of the React/WPF shell without changing engine truth.

The most relevant normative requirements are SEC-001, SEC-003, SCAN-004
through SCAN-007, SNAP-001 through SNAP-006, OPS-004, OPS-005, AUTH-001
through AUTH-003, AI-004, DOC-002, and DOC-011. Accepted ADR-0001 through
ADR-0004, ADR-0010, ADR-0013, and ADR-0014 constrain authority, identity,
non-mutation, scope, invalidation, provider behavior, and maintenance.

## 2. Scope and non-scope

### In scope

- UI host, CLI, coordinator, and worker process authority;
- runtime database ownership;
- exact local client/coordinator transport;
- renderer/host presentation boundary;
- worker result-publication boundary;
- query, pagination, event, backpressure, and cancellation contracts;
- protocol compatibility and reconnect behavior;
- crash, restart, and supervision behavior;
- bounded message and payload policy; and
- the M1 subset and evaluation obligations.

### Out of scope

- selecting the stack, database, or job ledger, which were owned by separate
  investigations and are now accepted in ADR-0015 through ADR-0017;
- credential entry and secure storage, owned by RQ-018;
- detailed protected-path, sanitizer, subprocess, and export controls, owned
  by RQ-032;
- the exact cost reservation algorithm, owned by RQ-034;
- installer, signing, update, or Windows-service behavior, owned by RQ-030;
- exact database schema, ORM, SQLite binding, dependency injection framework,
  process executable names, or deployment layout;
- remote access, multi-user operation, or browser-direct API access;
- production implementation and performance qualification.

## 3. Sources and current versions

Primary sources and package metadata were reviewed on 2026-07-28.

| Subject | Current source/result | Relevance |
|---|---|---|
| .NET IPC with gRPC | Microsoft [.NET 10 inter-process gRPC guidance](https://learn.microsoft.com/en-us/aspnet/core/grpc/interprocess?view=aspnetcore-10.0) | Named pipes are the Windows-specific local IPC option; gRPC clients use a custom `ConnectCallback`. |
| Named-pipe gRPC server | Microsoft [.NET 10 gRPC over named pipes](https://learn.microsoft.com/en-us/aspnet/core/grpc/interprocess-namedpipes?view=aspnetcore-10.0) | Kestrel can listen with HTTP/2 on a named pipe and accept custom `PipeSecurity`. |
| Named-pipe transport limits | Microsoft [`NamedPipeTransportOptions`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.server.kestrel.transport.namedpipes.namedpipetransportoptions?view=aspnetcore-10.0) | Current-user restriction, ACL configuration, and finite read/write buffers are first-class; unlimited buffering is identified as a security risk. |
| Pipe user restriction | Microsoft [.NET 10 `PipeOptions`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0) | `CurrentUserOnly` verifies the same user and, on Windows, elevation level. |
| Windows pipe security | Microsoft [named-pipe security and access rights](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights) and [`CreateNamedPipe`](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createnamedpipea) | Default ACLs are too broad for assumption-free use; explicit DACLs and local-only/remote-rejection behavior are required. |
| gRPC message limits | Microsoft [.NET 10 gRPC security](https://learn.microsoft.com/en-us/aspnet/core/grpc/security?view=aspnetcore-10.0) and [configuration](https://learn.microsoft.com/en-us/aspnet/core/grpc/configuration?view=aspnetcore-10.0) | Incoming messages default to 4 MiB while outgoing messages are unlimited unless configured; both directions require explicit limits. |
| gRPC streaming | Microsoft [.NET 10 gRPC performance guidance](https://learn.microsoft.com/en-us/aspnet/core/grpc/performance?view=aspnetcore-10.0) | Async calls, IPC transports, stream cleanup, cancellation, flow control, and the complexity of long-lived streams constrain the event design. |
| gRPC failure semantics | Microsoft [.NET 10 error handling](https://learn.microsoft.com/en-us/aspnet/core/grpc/error-handling?view=aspnetcore-10.0) | Cancellation, deadline, unavailable, and structured application failures must remain distinguishable. |
| WebView2 boundary | Microsoft [WebView2 security guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security) and [`WebMessageReceived` arguments](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2webmessagereceivedeventargs) | Host code must validate sender origin and messages and avoid generic proxies; received messages expose source and JSON. |
| Worker supervision | Microsoft [Windows Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects) | A coordinator can contain a worker/external-tool process tree and terminate descendants on owner loss. |
| Compared package line | NuGet v3 registry observations: `Grpc.AspNetCore` and `Grpc.Net.Client` `2.80.0`, `Google.Protobuf` `3.35.1`, `Grpc.Tools` `2.83.0` | These are research-time comparison versions, not implementation pins. |

RESEARCH-0036 through RESEARCH-0038 were reviewed as proposed upstream
evidence. ADR-0015 through ADR-0017 subsequently made their selected
conclusions authoritative.

## 4. Process and authority model

### 4.1 Desktop host and renderer

The WPF host owns only:

- window and WebView2 lifecycle;
- origin and message validation;
- presentation-query request correlation and cancellation;
- native shell operations separately allowlisted by RQ-032;
- a generated coordinator client; and
- initiation and parenting of the dedicated native credential-entry helper
  selected by RQ-018/RQ-032; it never receives the entered secret.

It does not open SQLite, parse mod artifacts, call Mutagen or libloot, invoke
tools, acquire documentation, hold durable job truth, or decide whether an
operation is authorized.

React owns presentation state only. A row ID, optimistic indicator, event, or
client cache never becomes a finding, case, run, review event, cost entry, or
coverage fact. Browser storage is not an authoritative or secret-bearing
store.

### 4.2 Coordinator

The coordinator is the sole runtime authority for:

- all SQLite connections, migrations, backup coordination, and checkpoints;
- lifecycle transitions, leases/fences, dispatch, and recovery;
- application queries and current projections;
- path, operation, source, provider, credential, and budget authorization;
- worker launch, assignment, cancellation, and result admission;
- content-addressed payload publication;
- progress/cost aggregation; and
- authoritative audit and error records.

Owning all reads as well as writes is deliberate. SQLite can support concurrent
read connections, but exposing the file would bypass authorization, query
bounds, schema compatibility, migration coordination, retention semantics,
and short-read-transaction discipline. The coordinator may internally use
several short read connections while retaining one process owner.

The coordinator runs as the interactive user, never elevated. It is a
standalone executable, not loaded into WebView2/WPF and not a Windows service.
It may remain in the user's session after the shell exits only while active
work or an explicit keep-running choice requires it. A renderer or shell
restart reconnects; a coordinator exit uses later durable recovery.

### 4.3 CLI

The CLI consumes the same versioned application contract as the desktop host.
It can:

- start a coordinator when none owns the product store;
- connect to an existing compatible coordinator;
- initiate and control supported operations;
- execute bounded queries and stream progress;
- render human-readable output; and
- emit the separately specified versioned run-owned JSON.

The CLI has no embedded/direct-database mode in normal product operation.
Offline means no external network/provider requirement for eligible local
work, not bypassing the coordinator boundary.

### 4.4 Workers

Workers:

- receive one immutable, bounded work assignment at a time;
- receive only the files, non-secret capabilities, and limits needed for that
  assignment; general parser/tool workers receive no provider credential or
  provider-dispatch authority;
- use a per-attempt product-controlled temporary area;
- stage candidate outputs only inside that assigned temporary area and return
  typed manifests containing claimed hashes and sizes;
- emit bounded progress/diagnostic messages;
- cannot open the Infinium database;
- cannot publish lifecycle, evidence, finding, cost, or completion state; and
- cannot call the renderer or accept independent external clients.

The coordinator independently validates assignment identity, fencing epoch,
schema, producer version, dependencies, sizes, content hashes, provenance, and
admission result. Only the coordinator may move or adopt a staged object into
the content-addressed payload store and commit its durable reference. Workers
never write directly into the authoritative payload namespace.

Native/crash-prone parsing and approved external tools run in workers or their
contained process trees. A Windows Job Object should prevent silent descendant
survival when the owning worker/coordinator dies where the qualified tool
contract permits it. Job Objects provide process containment, not a
filesystem/network sandbox; RQ-032 still owns least-authority execution.

For M1, use a **separate worker-only gRPC service on a second
current-user-only named-pipe endpoint**. Workers connect as clients; they
never listen. A launch-bound nonce is delivered through an inherited
one-way bootstrap handle rather than a command line or shared settings file.
The worker then authenticates its role, fencing epoch, and exact assignment
identity before the coordinator sends work. The endpoint exposes no
application query or durable-command method. Replacing this physical channel
later requires measured justification and an ADR amendment, not an
unreviewed implementation substitution.

Credential entry and provider dispatch use a distinct trusted helper process,
not this general worker endpoint. Under the RQ-018/RQ-032 recommendation, the
coordinator records and authorizes an exact non-secret enrollment or dispatch
assignment, launches the one-shot helper through inherited private handles,
and receives only non-secret status, usage, and staged-output manifests. The
helper alone addresses the exact Credential Manager target and observes secret
bytes. Neither secret bytes nor the credential target cross the application
or general worker gRPC contracts, and parser/tool workers receive no credential
or provider-network authority.

## 5. Client/coordinator transport decision

### 5.1 Recommended: gRPC over a Windows named pipe

Use one per-user-instance **application-client** named-pipe endpoint hosted by
Kestrel in the coordinator:

- HTTP/2 only;
- current-user/elevation restriction enabled;
- explicit DACL rather than the platform default;
- local-only pipe creation with remote clients rejected;
- unpredictable instance-qualified pipe name;
- bounded read and write buffers;
- finite send and receive message limits;
- no TCP listener, HTTP fallback, reflection, browser access, or remote
  exposure in release builds; and
- application handshake before any non-health operation.

The endpoint descriptor can live only in a product-controlled per-user
runtime location with restrictive ACLs. It contains endpoint/instance and
protocol discovery data, never provider credentials. An ephemeral
coordinator-instance nonce supplements OS pipe checks and detects stale or
wrong-instance clients. It does not claim to defend against fully compromised
code already running as the same Windows user.

Named-pipe ACL and nonce validation authenticate the local client boundary.
They do not authorize an operation. Every request is independently checked
against the caller role (`desktop`, `cli`, or `worker`), method allowlist,
current lifecycle state, path/credential/budget policy, and immutable request
bindings.

### 5.2 Why gRPC rather than raw framing

gRPC supplies:

- generated protocol-buffer contracts;
- unary and streaming methods;
- structured status, cancellation, and deadlines;
- HTTP/2 multiplexing and flow control;
- ASP.NET Core hosting and diagnostics; and
- one contract usable by WPF, CLI, and .NET test clients.

A raw JSON/protobuf pipe could be smaller, but Infinium would need to implement
framing, multiplexing, cancellation, deadlines, status mapping, flow control,
compatibility, and client generation itself. That is unjustified unless the
M1 spike finds a material gRPC packaging, startup, reliability, or resource
failure.

The recommendation does not turn gRPC messages into domain truth. Durable
state remains in the coordinator-owned store. A transport success does not
prove operation completion; the returned durable transition/revision identity
does.

## 6. Application-query contract

### 6.1 Method families

Expose separate allowlisted services:

1. **Handshake/health:** protocol, application, schema, instance, coordinator
   epoch, and capability compatibility.
2. **Queries:** summaries, paginated lists, details, provenance expansion,
   progress snapshots, cost, coverage, and deletion preview.
3. **Durable commands:** manual run initiation, pause/resume/cancel request,
   review event, retention/deletion confirmation, and other accepted product
   writes.
4. **Events:** coalescible progress/current-projection invalidations and
   resynchronization signals.
5. **Worker protocol:** assignment, bounded progress, cancellation request,
   staged-output manifest, and completion/failure receipt.

No method accepts SQL, an arbitrary path, command line, URL fetch, provider
operation, model tool, generic object name, or generic filesystem/database
action.

### 6.2 Query shape

Long collections use:

- engine-side allowlisted filters and sort keys;
- stable opaque object IDs;
- deterministic tie-breakers;
- keyset cursors bound to query shape, sort, projection version, and scope;
- a default page of 50 and hard maximum of 200 rows;
- bounded field selection through defined summary/detail DTOs, not caller SQL;
- separately fetched evidence bodies and provenance neighborhoods; and
- explicit partial, stale, unsupported, unavailable, and gap states.

Offset pagination is acceptable only for tiny static settings lists. It is not
the high-scale finding/history contract because concurrent projection changes
can make pages skip or duplicate items and large offsets cause avoidable work.

A cursor is not authority. The coordinator validates or rejects it and returns
`resync-required` if the bound projection changed incompatibly. Historical
run-bound queries may remain stable because their source revision is
immutable.

### 6.3 Events and backpressure

Progress/event streams are a UI optimization, not a second event store:

- every message carries coordinator instance/epoch, subscription ID, durable
  projection or transition sequence, entity scope, and event kind;
- the client obtains an authoritative initial snapshot before consuming
  later events;
- the coordinator uses a bounded per-client channel;
- coalescible progress updates replace older pending updates for the same
  scope;
- non-coalescible durable transitions carry sequence identities;
- a slow client, gap, server restart, or expired replay window receives
  `resync-required` and repeats the snapshot query;
- a disconnected renderer cannot block scheduling, SQLite checkpointing, or
  another client; and
- events never contain complete evidence bodies or unbounded result sets.

The coordinator may retain a bounded in-memory event replay window for smooth
reconnect. The durable lifecycle ledger, not this window, is authoritative.

### 6.4 Cancellation and idempotency

Transport cancellation has narrow meaning:

- canceling a query or subscription stops only that query/stream;
- canceling the RPC that submitted a durable command does not undo an
  accepted command;
- pausing or cancelling a run requires the explicit durable run-control
  method and follows the RQ-015 state machine;
- command methods use a client-generated idempotency key and return the
  resulting durable command/transition identity; and
- a retry after an indeterminate response asks for that idempotency result
  rather than submitting a second command.

Unary queries receive short deadlines. Long-lived subscriptions use explicit
lease/heartbeat and reconnect policy rather than pretending one infinite RPC
is durable. Worker cancellation remains cooperative and is backed by process
containment only for local work.

### 6.5 Versioning

The handshake returns:

- protocol major/minor;
- application build and supported contract range;
- storage schema and domain-contract versions;
- coordinator instance and fencing epoch;
- feature/capability flags; and
- minimum compatible client version.

Rules:

- incompatible major versions fail before privileged methods are exposed;
- additive protobuf fields and methods may advance a compatible minor;
- enum `UNSPECIFIED`/unknown values are never silently mapped to success;
- unknown method/operation identifiers are rejected;
- every durable command records the contract and application versions used;
- a newer client cannot cause an older coordinator to infer unsupported
  behavior; and
- a CLI remains independently usable against the same declared compatible
  contract.

## 7. Payload and resource boundaries

Set explicit M1 safety ceilings on both gRPC send and receive:

- 1 MiB maximum unary request;
- 4 MiB maximum unary/stream message response;
- 200 rows and 1 MiB serialized target maximum per list page;
- 256 KiB maximum source/evidence text chunk;
- 64 KiB maximum progress/event or worker-progress message; and
- no unlimited Kestrel named-pipe read/write buffering.

The byte limits are initial safety ceilings, not performance targets. A method
may impose a lower limit. M1 measurement may lower or raise a ceiling only
through a reviewed contract change with memory/adversarial evidence.

Large source bodies, model/tool payloads, indexes, checkpoints, worker output,
and exports do not cross IPC as one message. They remain in the accepted
coordinator-owned content-addressed store and are:

- queried as typed summaries;
- read in bounded inert chunks where the UI genuinely needs content; or
- staged by workers and admitted by hash/size/schema manifest.

The renderer never receives a local payload path or file handle. Compression
does not relax the post-decompression message or content limit.

## 8. Startup, crash, restart, and supervision

### Startup

1. Client reads the restrictive endpoint descriptor and attempts handshake.
2. If no compatible coordinator responds, one client starts it.
3. A per-store launch guard reduces duplicate startup, but the transactional
   coordinator lease/fencing epoch remains correctness authority.
4. Losing startup candidates exit or connect to the winner.
5. The coordinator validates/migrates the store and publishes its endpoint
   only when ready.

### Failure behavior

- **Renderer crash/reload:** WPF remains; React re-handshakes and requeries.
- **WPF/CLI exit:** no durable state changes implicitly. Active coordinator
  work continues only under the accepted close/keep-running policy.
- **Coordinator crash:** worker Job Objects terminate descendants; later
  coordinator increments the fencing epoch, recovers staged output and
  attempts, and retries only safe work.
- **Worker crash:** coordinator records the attempt failure/interruption,
  rejects stale publication, and applies the declared retry/gap policy.
- **Named-pipe disconnect:** query/stream ends; no durable command is inferred
  cancelled. Client reconciles by idempotency/durable query.
- **Database unavailable/migration mismatch:** coordinator does not expose
  normal methods; UI/CLI reports a typed recovery or incompatibility state.
- **Protocol mismatch:** fail closed without direct-database fallback.

The coordinator should normally exit when no active work or explicit
keep-running requirement remains. Paused work is durable and need not keep a
process alive. Automatic restart after Windows sign-in/reboot is deferred
unless RQ-030 later accepts it.

## 9. Alternatives

This recommendation already uses existing infrastructure for the generic
parts: ASP.NET Core/Kestrel hosts HTTP/2 on the pipe, gRPC supplies RPC
framing, generated protobuf contracts, deadlines, cancellation, streaming,
and flow control, and Windows supplies named-pipe ACLs. Infinium owns only its
domain-specific methods, projections, authorization rules, and durable command
semantics.

RQ-015's scheduler cannot replace this layer. A scheduler answers “which
authorized unit may run next?” and records its lifecycle. IPC answers “how do
separate UI, CLI, coordinator, and worker processes exchange bounded commands
and results?” A queue dashboard or job API does not provide finding/case
queries, keyset cursors, provenance expansion, renderer reconnect/resync,
role-separated worker publication, or the application security contract.

| Alternative | Strengths | Why not selected |
|---|---|---|
| **gRPC/HTTP2 over current-user-only named pipe** | First-party .NET Windows IPC; generated contracts; streaming/cancellation/flow control; no TCP port | Adds ASP.NET Core/gRPC hosting and protobuf contracts; requires explicit ACL, buffers, limits, compatibility, and spike validation | **Recommend** |
| **Raw framed protobuf/JSON over named pipes** | Small runtime surface; complete framing control | Must build multiplexing, status, deadlines, cancellation, backpressure, compatibility, and clients; recreates bounded RPC machinery | Fallback only if measured gRPC cost/failure is material |
| **Loopback HTTP/REST or gRPC over TCP** | Familiar tooling; easier non-.NET/browser clients | Port discovery, endpoint authentication, firewall/proxy exposure, origin/CSRF risk, and broader attack surface provide no M1 benefit | Reject for local M1; reconsider only for an accepted remote client |
| **Renderer direct to local HTTP service** | Removes WPF relay | Gives browser content a network-capable privileged bridge, complicates origin/auth, and weakens shell containment | Reject |
| **Direct SQLite reads from WPF/CLI** | Fewer query-service methods; potentially low latency | Bypasses authorization, migrations, projection semantics, query bounds, short transactions, and shell replaceability; risks WAL pressure | Reject |
| **In-process coordinator inside WPF** | Simplest launch/debug path | Shell crash kills authority; durable work and CLI depend on UI; analysis isolation and shell replaceability weaken | Reject |
| **Windows service coordinator** | Can survive user shell and sign-out | Elevation/account/installer/service-security complexity; conflicts with personal per-user product and WebView2/user-session boundary | Reject through M4 |
| **One process per whole scan** | Simple process-level isolation | Process lifetime cannot represent durable run, pause, checkpoint reuse, child acquisition, or recovery | Reject |
| **stdio/anonymous pipes for every boundary** | Excellent parent-child privacy for workers | Cannot support independent shell/CLI reconnect to a durable coordinator; manual framing remains | Suitable only as a worker-channel implementation detail |
| **Memory-mapped shared state** | High throughput for bulk bytes | Synchronization, schema, crash, lifetime, authorization, and mutation complexity; bulk state should remain store-backed | Reject without measured need |

## 10. Bounded M1 subset

M1 should include:

- one standalone non-elevated coordinator executable;
- one runtime owner of all SQLite connections;
- one current-user-only application-client named-pipe gRPC endpoint and one
  separate worker-only endpoint;
- protocol handshake and incompatible-version rejection;
- the CLI as a real client of that endpoint;
- at least one isolated coordinator-launched worker;
- the one-shot credential/provider helper and inherited private-handle channel
  before any authenticated provider operation;
- no direct UI/CLI/worker database access;
- a paginated run/finding query with server filtering and keyset cursor;
- authoritative progress snapshot plus bounded event stream/resync;
- explicit durable start, pause/resume, and cancel commands with idempotency;
- renderer/shell/coordinator/worker restart fault tests;
- staged worker output plus coordinator validation/publication;
- finite transport/message/page/event limits; and
- human-readable CLI and versioned JSON output through the same application
  services.

M1 may defer:

- polished WPF/WebView2/React UI beyond the disposable RQ-016 spike;
- several simultaneous analysis runs;
- a persistent worker pool rather than one worker per bounded operation;
- user-facing active acquisition detachment;
- concurrent billable work before RQ-034/EVAL-0081;
- background/Batch provider execution;
- Windows-service or automatic sign-in startup;
- remote clients or cross-platform transports;
- large event replay beyond snapshot-and-resync; and
- tuned message/page ceilings beyond conservative validated defaults.

If the M1 implementation remains CLI-first, the same coordinator/query
contract still must be proven. Deferring the graphical shell must not justify
embedding durable state in CLI memory or granting direct database access.

## 11. Evaluation mapping

| Evaluation | Required proof |
|---|---|
| `EVAL-0018` | High-scale queries, progress, event coalescing/resync, pause/resume, coordinator/worker restart, SQLite WAL behavior, and UI/CLI responsiveness meet later budgets. |
| `EVAL-0026` | Mid-run edits and client reconnect cannot mutate bound run inputs; returned projection/run identities remain exact. |
| `EVAL-0033` | Hostile HTML, text, model/tool output, WebView2 messages, and gRPC payloads cannot grant methods, paths, tools, credentials, or authority. |
| `EVAL-0034` | Secrets and unnecessary path/user context are absent from renderer messages, gRPC logs/errors, traces, and outputs. |
| `EVAL-0035` | Unknown method, arbitrary path/URL/command/SQL, malformed cursor, oversized payload, wrong role, stale nonce, and wrong-origin renderer message fail closed. |
| `EVAL-0038` | Query cancellation is not run cancellation; explicit pause/resume/cancel state survives client/coordinator restart and duplicate commands. |
| `EVAL-0039` | Acquisition ownership and application links remain queryable without UI or worker re-ownership. |
| `EVAL-0040` | Human and JSON output come through application services and retain exact run/provenance identity. |
| `EVAL-0041` | Retention/deletion preview and confirmation are durable commands, never direct DB/file deletion by a client. |
| `EVAL-0044` | Events, retries, and reconnect cannot duplicate owned cost or attached rollups. |
| `EVAL-0045` | Client reconnect, profile change, and endpoint startup never initiate a scan or billable acquisition implicitly. |
| `EVAL-0046` | Worker/tool boundary exposes only qualified non-mutating operations and contains declared process/temp effects. |
| `EVAL-0064` | Local CLI/coordinator/worker flow operates without WebView2, network, or provider credential. |
| `EVAL-0077` | Renderer/CLI cannot dispatch billable work without coordinator-side current authorization. |
| `EVAL-0079` | Stable finding/case identity comes from coordinator projections, not UI rows/cursors/client caches. |
| `EVAL-0080` | Coordinator/worker staging and client-requested product writes stay within authorized destinations under alias/reparse adversaries. |
| `EVAL-0081` | Dispatch/reservation and fencing remain atomic across duplicate client commands, disconnects, coordinator death, and stale worker output. |
| `EVAL-0082` | The application contract independently controls and retains effective analyzer/source/budget/cache/tracing values; unknown/unsupported fields never silently change them. |
| `EVAL-0083` | UI/CLI can traverse end-to-end provenance through bounded queries without provider payloads or transport events becoming domain truth. |
| `EVAL-0088` | Owns the M1 coordinator-start race, role/protocol/nonce compatibility, bounded query/cursor/stream behavior, cancellation-versus-run-control distinction, reconnect/resync, crash recovery, worker staging, and coordinator-only publication contract. |

Add Wave E boundary cases for:

- two simultaneous clients racing to start the coordinator;
- cross-user, different-elevation, remote, stale-instance, and wrong-nonce
  pipe connections;
- renderer wrong-origin, malformed, oversized, replayed, and out-of-order
  messages;
- slow event consumer, event-buffer overflow, sequence gap, and resync;
- cursor tampering, projection change between pages, and high-fan-out
  provenance expansion;
- coordinator death before/after durable command acceptance and worker output
  staging/publication; and
- protocol major mismatch and additive-minor compatibility.

No evaluation is marked passed by this research.

## 12. Accepted recommendation and ADR content

ADR-0018 and ADR-0019 preserve two decisions so the durable authority topology
is not coupled to a replaceable local transport:

### Process and authority topology ADR

Accept:

1. a standalone per-user .NET coordinator as the only database, scheduler,
   query, authorization, and durable-publication authority;
2. WPF desktop host and CLI as application-contract clients;
3. React as a presentation-only client through the separately validated
   WebView2 host boundary;
4. no direct database access from UI, CLI, workers, or tools;
5. coordinator-launched bounded workers with per-attempt staging and no direct
   authoritative-payload or durable-publication authority;
6. a separate one-shot credential/provider helper, launched through inherited
   private handles, with no database/query authority and no secret-bearing
   application or general-worker IPC; and
7. no Windows service or remote client through M4.

### Local IPC and application-query ADR

Accept:

1. gRPC/HTTP2 over a current-user-only, explicitly ACL-restricted, local-only
   Windows named pipe as the client/coordinator transport;
2. role-aware authorization in addition to transport authentication;
3. server-filtered/keyset-paginated queries and bounded detail expansion;
4. sequenced/coalesced progress events with authoritative snapshot/resync;
5. explicit separation of RPC cancellation from durable run control;
6. a separate worker-only named-pipe gRPC service with launch-bound bootstrap
   nonce and no query/publication methods;
7. version handshake, idempotent durable commands, finite message/buffer
   limits, and fail-closed compatibility; and
8. no TCP listener or direct renderer-to-coordinator service through M4.

The ADRs leave exact library/package pins, executable layout, generated
contract tooling, page/message tuning, endpoint descriptor representation,
and idle-shutdown timing to accepted milestone plans and qualification.

## 13. Confidence, uncertainty, and reopen triggers

Confidence:

- **High** that UI/CLI/workers must not directly access the authoritative
  store.
- **High** that one UI-independent coordinator must own durable lifecycle,
  queries, authorization, and worker publication.
- **High** that high-scale UI access needs bounded server-side queries,
  pagination, and resynchronizable progress.
- **Medium-high** that gRPC over a Windows named pipe is the best M1
  client/coordinator transport for the accepted all-.NET backend boundary.
- **Medium** that the initial size/page/event ceilings are appropriate; they
  are conservative safety bounds requiring prototype measurement.
- **Medium-low** for worker-channel physical framing, endpoint discovery
  details, and idle lifetime before implementation spikes.

Remaining uncertainty:

- the selected SQLite binding and ASP.NET Core host must coexist without
  unacceptable startup, memory, WAL, or thread-pool behavior;
- current-user/elevation checks and explicit ACL/remote rejection need
  adversarial Windows integration tests;
- same-user malicious code is outside the protection claimed by the pipe
  nonce/ACL boundary;
- worker isolation is process containment, not yet an OS sandbox;
- exact React/WPF message schema generation is unselected;
- large evidence-text interaction may require lower chunk sizes or a dedicated
  read stream; and
- a future remote/read-only client would require a new transport,
  authentication, privacy, and authority ADR.

Reopen the transport selection if:

- the bounded M1 spike finds material gRPC/Kestrel packaging, startup,
  reliability, cancellation, or resource failure;
- Microsoft withdraws or materially changes named-pipe gRPC support;
- an accepted cross-platform or remote-client requirement appears;
- measured result/query shape cannot meet OPS-004/OPS-005 through bounded
  gRPC pages and projections; or
- the selected security model needs stronger isolation than same-user local
  IPC can provide.

## 14. Current RQ-017 status

> **Resolved for M0.** ADR-0018 accepts a standalone per-user .NET
> coordinator as sole database/scheduler/query authority, desktop and CLI
> clients, a one-shot credential/provider-helper process role, and isolated
> workers with no direct database or publication authority. ADR-0019 accepts
> the gRPC/HTTP2 contract over an ACL-restricted current-user-only Windows
> named pipe. Implementation, security, scale, and failure conformance remain
> pending.

## 15. Semantic self-review

- The accepted process topology and IPC mechanism are labeled separately.
- SQLite's single write owner is strengthened into one process owner, without
  claiming an implementation exists.
- Renderer, host, CLI, coordinator, and worker authority are distinct.
- UI virtualization does not substitute for server-side query bounds.
- Progress events do not become durable truth.
- RPC cancellation does not rewrite durable run-control semantics.
- Workers cannot publish, charge, or complete work directly.
- Offline CLI support does not bypass the coordinator or security boundary.
- Named-pipe ACLs authenticate a local boundary but do not themselves
  authorize operations or claim protection from a compromised same-user
  process.
- No TCP, remote-client, service, database, worker, UI, or named-pipe IPC
  implementation is represented as complete; ADR-0018 accepts design only.
