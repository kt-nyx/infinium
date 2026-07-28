# ADR-0019: Local IPC and application-query contract

Status: Accepted  
Date: 2026-07-28  
Accepted: 2026-07-28  
Accepted by: Project owner  
Last reviewed: 2026-07-28  
Supersedes: None  
Superseded by: None

## Context

The standalone coordinator accepted by ADR-0018 requires a local contract that keeps the
desktop host and CLI responsive without exposing the authoritative database or
generic privileged operations. Isolated workers also need a physically and
semantically narrower assignment channel. The transport must support bounded
queries, progress, cancellation, reconnect, versioning, backpressure, and
coordinator-only publication while remaining replaceable if measured M1
evidence disproves the candidate.

[RESEARCH-0039](../../research/investigations/RESEARCH-0039-process-and-data-query-boundary.md)
compared gRPC over Windows named pipes with raw framed pipes, local TCP, direct
database access, and in-process execution.
[RESEARCH-0041](../../research/investigations/RESEARCH-0041-security-boundary-controls.md)
added exact endpoint, role, nonce, bound, and worker-bootstrap controls.

This ADR depends on ADR-0018's accepted process roles. It owns the replaceable
local transport and application-query/event protocol, not process lifetimes,
credential semantics, domain truth, or WebView2 renderer security.

## Decision drivers

- UI/CLI/worker access must not bypass coordinator authorization, migrations,
  retention, or query bounds through direct database access.
- High-end lists and retained history require server-side filtering,
  pagination, aggregation, and bounded detail expansion.
- Progress must remain useful without becoming a second durable event store.
- A slow, crashed, stale, or incompatible client must fail closed and must not
  block durable work.
- Application clients and workers require distinct method authority.
- Secrets and Credential Manager target names must not enter ordinary
  application or worker messages.
- The transport should use generated, versionable contracts and be replaceable
  without changing domain or persistence truth.

## Considered options

### Direct database access by clients or workers

SQLite can support concurrent readers, but publishing the file would bypass
authorization, schema compatibility, migration coordination, query bounds,
retention semantics, and short-read discipline. Workers could also convert
staged output directly into durable truth. This option is rejected.

### Local TCP HTTP/gRPC

This is familiar and portable, but opens a network listener and adds port,
firewall, browser-origin, and local-network exposure that a Windows-only
per-user product does not require.

### Custom JSON or protobuf framing over named pipes

This can be smaller, but Infinium would own framing, multiplexing, flow
control, deadlines, cancellation, status mapping, compatibility, and client
generation. It remains a fallback only if the M1 spike demonstrates a material
gRPC/Kestrel problem.

### gRPC over HTTP/2 on Windows named pipes

ASP.NET Core provides first-party named-pipe gRPC support, generated protobuf
clients, unary and streaming methods, deadlines, cancellation, and HTTP/2 flow
control without a TCP listener. Explicit pipe security and application
authorization are still required.

This is the existing-solution choice: Infinium does not implement its own RPC
transport, framing, multiplexer, or streaming protocol. The product-specific
work is limited to its allowlisted service methods, projections,
authorization, compatibility, and durable command meanings. A job scheduler
cannot replace this contract because execution admission and inter-process
communication/querying solve different problems.

## Decision

Infinium shall use a versioned gRPC/HTTP2 protocol over Windows
named pipes for local coordinator communication.

### Endpoints and transport security

The coordinator exposes:

1. one application-client endpoint for the WPF host and CLI; and
2. one separate worker-only endpoint for coordinator-launched general workers.

Each endpoint is:

- local named-pipe transport only, with no TCP/HTTP fallback, reflection,
  browser endpoint, or remote exposure in release builds;
- restricted to the current user and elevation level;
- protected by an explicit restrictive DACL and remote-client rejection rather
  than relying on the platform default;
- identified by an unpredictable coordinator-instance-qualified name;
- published only through a restrictive product-owned per-user runtime
  descriptor containing no credential data; and
- configured with finite transport buffers, send/receive message sizes,
  stream queues, deadlines, and rate limits.

Every connection completes a handshake covering protocol major/minor,
application and storage/domain compatibility, coordinator instance and fencing
epoch, capability flags, and an ephemeral instance nonce. Endpoint ACLs and
nonces authenticate the local connection context; they do not authorize an
operation. Caller role comes from the endpoint and launch relationship, not a
role string claimed in an untrusted payload.

Incompatible major versions and unknown privileged operations fail before the
operation is exposed. Additive compatible changes may advance a minor version.
Unknown enum values are never silently interpreted as success.

### Application contract

The application endpoint exposes only allowlisted:

- handshake/health methods;
- bounded summary, list, detail, provenance, coverage, progress, cost, and
  deletion-preview queries;
- explicit durable commands such as manual start, pause, resume, cancel,
  review, and confirmed retention/deletion actions; and
- bounded progress/current-projection invalidation streams.

It exposes no arbitrary SQL, path, filesystem action, URL fetch, provider
operation, command line, tool invocation, generic object lookup, or raw
payload-store access.

Every read is a coordinator-owned application query using server-side
allowlisted filters, sorting, aggregation, stable deterministic tie-breakers,
keyset cursors, bounded pages, and typed summary/detail projections. Cursors
are opaque and bound to query shape, sort, scope, and projection version. An
invalidated or malformed cursor produces a typed resynchronization result,
never inferred state.

Large bodies and artifacts remain in coordinator-owned persistence. Clients
receive bounded inert chunks or typed summaries, never a local payload path or
file handle. Numeric message, page, and chunk ceilings are conservative,
versioned M1 contract values selected and measured in the milestone plan;
neither direction may be unlimited.

### Events, commands, and cancellation

An event stream is a bounded optimization, not authoritative history:

- the client first obtains an authoritative snapshot;
- each event carries coordinator instance/epoch, subscription, durable
  sequence or projection version, scope, and kind;
- coalescible progress can replace an older pending update for the same scope;
- a slow client, overflow, gap, restart, or expired replay window receives
  `resync-required`; and
- client disconnection cannot block scheduling, database maintenance, or
  another client.

Transport cancellation stops only the current query or subscription. It does
not cancel or undo an accepted durable command. Durable run control uses
explicit methods and client-generated idempotency keys; an indeterminate
response is reconciled by querying the durable command/transition identity
rather than resubmitting blindly.

### Worker contract

General workers connect as clients to the separate worker endpoint; they never
listen. A launch-bound one-use bootstrap nonce is passed through an inherited
private handle, not the command line, environment, settings, or application
endpoint. The worker then authenticates its expected process/role, fencing
epoch, and exact assignment before receiving work.

The worker service exposes only one bounded assignment, progress, cancellation
request, staged-output manifest, and terminal receipt. It has no application
query, durable command, database, payload-publication, credential, or provider
method. Only the coordinator can admit staged output.

The one-shot credential/provider helper does not use either ordinary gRPC
contract for secrets or Credential Manager targets. Its exact assignment and
final authorization travel over coordinator-created inherited private
handles. It returns only bounded non-secret status/usage and staged-output
manifests.

### Renderer boundary

React never connects to a coordinator pipe. It sends only schema-validated,
size-bounded presentation messages to the minimal WPF host. The host maps
allowlisted presentation operations to its generated application client; it is
not a generic IPC proxy. WebView2 origin, navigation, and message controls
belong to the separate desktop-security ADR.

## M1 boundary and exclusions

M1 shall prove:

- both role-separated endpoints and their fail-closed handshake;
- the CLI as a generated application client;
- at least one coordinator-launched general worker;
- one bounded paginated run/finding query using a keyset cursor;
- an authoritative progress snapshot plus bounded event/resync behavior;
- idempotent durable start, pause/resume, and cancel commands;
- finite buffers, message sizes, pages, chunks, and queues;
- malformed, oversized, slow-client, reconnect, and crash behavior; and
- coordinator-only staged-output admission.

M1 may defer:

- a long event-replay history beyond snapshot and resync;
- tuned ceilings beyond conservative measured defaults;
- remote or cross-platform transports;
- browser-direct access;
- persistent worker pools;
- concurrent live billable dispatch; and
- polished graphical presentation.

## Consequences

### Positive

- Clients and workers cannot bypass coordinator authority through the
  database.
- Generated contracts reduce framing and compatibility ambiguity.
- Bounded server-side queries and event resynchronization support high-end
  scale without loading whole histories into renderer memory.
- Application and worker roles are physically and semantically separated.
- Replacing the shell or transport need not rewrite domain truth.

### Negative

- The product must package and supervise an ASP.NET Core/Kestrel local host and
  generated protocol clients.
- Named-pipe ACL, startup discovery, versioning, flow-control, cancellation,
  and resource limits require Windows integration tests.
- gRPC adds more local infrastructure than an embedded CLI proof.

### Risks and mitigations

- Same-user malicious code is not excluded by pipe ACLs alone. Exact roles,
  nonces, schemas, operation authorization, and narrow method sets reduce
  authority but do not claim a same-user security boundary.
- A slow stream can retain memory. Finite queues, coalescing, deadlines, and
  forced resynchronization bound it.
- Contract drift can silently reinterpret state. Major-version rejection,
  generated contracts, unknown-value handling, and durable version provenance
  make incompatibility explicit.
- gRPC/Kestrel may impose unacceptable startup or packaging cost. A bounded M1
  spike must measure it; a reviewed replacement may retain the same semantic
  contract.

## Requirements affected

- SEC-001 through SEC-003
- SCAN-004 through SCAN-008
- AI-003, AI-004, and AI-007
- OPS-001 through OPS-005

## Validation

Acceptance selects a transport and protocol design; it does not pass a
conformance case.

M1 validation shall include:

- `EVAL-0018` for measured high-scale query/progress responsiveness;
- `EVAL-0026`, `EVAL-0038`, and `EVAL-0045` for immutable run bindings,
  lifecycle semantics, and no implicit work;
- `EVAL-0033` through `EVAL-0035` for hostile payloads, secret/context
  exclusion, and fail-closed roles/operations;
- `EVAL-0080` through `EVAL-0083` for write, dispatch, independent controls,
  and provenance boundaries; and
- `EVAL-0088` for startup races, versions, nonces, endpoint roles, malformed
  input, finite limits, cursors, streams, cancellation, reconnect/resync,
  worker staging, coordinator-only publication, slow clients, and crashes.

`EVAL-0089` additionally verifies that credential enrollment, lifecycle, and
dispatch never place a secret or Credential Manager target on these ordinary
contracts.

Revisit this ADR if the M1 spike finds material gRPC/Kestrel startup,
packaging, cancellation, reliability, or resource failure; Microsoft changes
named-pipe gRPC support materially; an accepted remote/cross-platform client
appears; or bounded application queries cannot meet OPS-004/OPS-005.

## References

- [ADR-0001](ADR-0001-evidence-authority-boundary.md)
- [ADR-0002](ADR-0002-snapshot-context-binding.md)
- [ADR-0003](ADR-0003-read-only-authority.md)
- [ADR-0010](ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
- [ADR-0018](ADR-0018-process-and-authority-topology.md)
- [RESEARCH-0039](../../research/investigations/RESEARCH-0039-process-and-data-query-boundary.md)
- [RESEARCH-0041](../../research/investigations/RESEARCH-0041-security-boundary-controls.md)
- [RESEARCH-0044](../../research/investigations/RESEARCH-0044-wave-e-architecture-and-security-integration.md)
