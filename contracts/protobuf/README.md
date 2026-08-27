# Protobuf contracts

Status: current product contract
Protocol generation: application/worker `v1`; helper `v1` and `v2`

These proto3 schemas define Infinium's versioned local process contracts. They
do not implement gRPC hosting, named pipes, process launch, IPC authorization,
credential access, provider dispatch, persistence, or runtime behavior.

## Package map

| Package | File | Purpose |
| --- | --- | --- |
| `infinium.common.v1` | `infinium/common/v1/common.proto` | Versions, finite limits, timestamps, digests, explicit availability, and typed failures |
| `infinium.domain.v1` | `infinium/domain/v1/identities.proto` | Opaque wire identities and the small set of lifecycle/coverage states required by IPC |
| `infinium.protocol.v1` | `infinium/protocol/v1/protocol.proto` | Endpoint roles, capability negotiation, application handshake, and private worker/helper bootstrap envelopes |
| `infinium.application.v1` | `infinium/application/v1/application.proto` | Allowlisted application queries, keyset cursors, durable run and snapshot-capture commands, progress snapshots, and bounded events |
| `infinium.worker.v1` | `infinium/worker/v1/worker.proto` | One immutable general-worker assignment, progress/control, staged output, and terminal receipt |
| `infinium.helper.v1` | `infinium/helper/v1/helper.proto` | Private-handle-only one-shot credential/provider-helper assignment, final dispatch revalidation, non-secret status/usage, and staging |
| `infinium.helper.v2` | `infinium/helper/v2/helper.proto` | Private-handle-only provider assignment, final revalidation, cache-separated usage, bounded response receipt, and protocol fingerprint; v1 remains separately decodable |

Imports are rooted at `contracts/protobuf`. Generated C# namespaces are
`Infinium.Contracts.Protobuf.<Area>.V1`.

## Authority and transport boundaries

The schemas intentionally expose three non-interchangeable surfaces:

| Surface | Permitted | Structurally absent |
| --- | --- | --- |
| Application named-pipe gRPC endpoint | Handshake/health, bounded run/finding/progress queries, explicit start/pause/resume/cancel commands, an explicit typed MO2 snapshot selection, bounded event stream | SQL, generic paths, URLs, shell/tool/provider operations, payload-store access, worker methods, credentials |
| Worker named-pipe gRPC endpoint | One launch-bound assignment, including the exact typed read-only MO2 snapshot operation, bounded progress, cancellation polling, attempt-local staging manifest, terminal receipt | Application queries, durable commands, generic paths or commands, database/payload publication, credentials, provider dispatch, generic process/network operations |
| Helper inherited private handles | One credential-lifecycle or exact provider-dispatch assignment, final gate, non-secret status/usage, staged response manifest | Ordinary gRPC service, database/application query, credential target names, secret bytes, arbitrary URLs, publication authority |

Caller role is established by endpoint and coordinator launch relationship.
No caller-supplied role string can grant authority. Application instance
nonces and launch-bound one-use bootstrap nonces establish connection context;
they do not authorize an operation. The coordinator must separately validate
every method, identity, current fencing epoch, lifecycle generation, immutable
assignment, deadline, credential generation/revocation epoch, and reservation.

A worker/helper staging acknowledgement is never publication. Only the current
coordinator may validate, admit, and transactionally publish staged bytes.

## Deny-by-default interpretation

- Every enum uses zero as `UNSPECIFIED`; zero is invalid and never success.
- Authority-bearing enums also include explicit `UNKNOWN` and `UNSUPPORTED`
  values. Neither may be interpreted as success, compatibility, availability,
  permission, terminal completion, or a zero-valued fact.
- An unset `oneof`, unknown message variant, unknown privileged method, unknown
  enum value, malformed identifier, incompatible major version, invalid
  nonce, stale fence, or missing finite limit fails closed.
- Ordinary non-privileged protobuf messages may retain unknown fields for
  compatible forwarding, but unknown fields never grant authority or change a
  known failure into success.
- A helper private frame, or any nested credential/provider-helper message,
  fails closed when it contains an unknown field. Privileged helper decoders
  must reject the frame and must not retain, forward, echo, stage, or log the
  unknown field bytes.
- Opaque IDs are bounded, case-sensitive tokens. Consumers must not parse them,
  derive domain meaning from them, synthesize paths from them, or substitute a
  content hash for a logical identity.
- Provider usage, locally calculated nano-USD, provider billing, rate
  headroom, spend limits, credit, and local hard limits remain distinct.
  Unavailable facts use `AvailabilityState`; absence is not zero or unlimited.
- Transport cancellation ends only the current call/stream. It never acts as
  durable run cancellation. Run control requires an explicit idempotent
  command and reconciliation through `GetDurableCommand`.

Fields `90..99` and selected forbidden names are reserved in privileged
messages so later changes cannot accidentally add secrets, generic paths,
database access, arbitrary URLs/commands, or publication claims to those
surfaces.

## Finite contract ceilings

Proto3 does not enforce collection, byte, or numeric limits. Producers and
consumers must validate before allocation/use and reject a zero, larger,
negative-where-prohibited, inconsistent, or overflowed value. Negotiation may
select a lower value, never a higher one.

The current contract ceilings are:

| Item | Ceiling |
| --- | ---: |
| Application/worker serialized gRPC message | 1,048,576 bytes |
| Private helper frame | 2,097,152 bytes |
| Page items | 100 |
| Inert body chunk | 262,144 bytes |
| Pending stream queue | 64 events |
| Filter terms | 16 |
| Sort terms | 4 |
| Capability flags | 16 |
| Application event run scope | 32 run IDs |
| Staged outputs per assignment | 32 protocol ceiling; current worker assignments narrow this to 1 |
| Worker inputs per assignment | 128 |
| Finding support-state filters | 8 |
| Inert status/detail/summary UTF-8 text | 4,096 bytes |
| Opaque ID or idempotency token | 128 UTF-8 bytes |
| Semantic-version text | 128 UTF-8 bytes |
| Adapter/analyzer/logical artifact name | 256 UTF-8 bytes |
| Opaque page/event cursor | 4,096 bytes |
| Instance/bootstrap nonce | exactly 32 bytes |
| SHA-256 digest | exactly 32 bytes |
| Diagnostic text per IPC message | 65,536 bytes |
| Default unary deadline | 15,000 ms |
| Maximum unary deadline | 60,000 ms |

`ProtocolLimits` carries the negotiated subset relevant to ordinary gRPC.
`WorkerLimits`, `HelperLimits`, output-slot limits, response bounds, and
wall-elapsed deadlines further narrow one assignment. Every byte, token,
work-unit, count-capacity, and time maximum is mandatory and non-zero. A
priced-tool-call or calculated-cost ceiling may be zero to forbid that form of
consumption; zero never means unlimited. The sender must reject an assignment
that cannot be represented by a qualified finite limit; there is no unlimited
sentinel. Inherited-handle slots are also non-zero, assignment-local indexes;
unknown, duplicate, missing, or wrong-access handle slots reject the
assignment.

Lists and details that can grow with history use server-side filters and
keyset pagination. Cursors are opaque and bound to query shape, stable sort,
scope, and projection version. A malformed, expired, reordered, mismatched, or
invalidated cursor yields `CursorRejection`/`resync-required`; the receiver
must not infer a continuation position.

## Compatibility and field evolution

The package suffix is the protocol major. Within `v1`:

- compatible additive fields, enum values, and methods advance the negotiated
  minor version and capability set;
- existing field numbers, meanings, wire types, oneof membership, and success
  semantics never change;
- a removed field reserves both its number and name;
- field numbers are grouped with gaps for additive evolution: `1..9` identity
  and authority, `10..19` operation/payload, and `20..29` typed failure or
  terminal detail;
- an incompatible semantic change creates a new major package such as `v2`;
- the handshake rejects incompatible protocol, application, domain, or storage
  contracts before exposing privileged operations; and
- additive-minor peers may use only explicitly negotiated capabilities.

The schema fingerprint in `ProtocolVersion` identifies the exact generated
contract set. It supplements, but does not replace, major/minor and
application/domain/storage compatibility checks.
It is the SHA-256 of the UTF-8 concatenation of each path relative to
`contracts/protobuf`, one LF, and that file's exact bytes, with files ordered
by ordinal protobuf-root-relative path.

## Security invariants

- Credential targets and secret bytes never appear in these ordinary contracts
  or helper frames. The helper derives only the exact authorized Credential
  Manager target from opaque profile/generation identity.
- Provider dispatch assignment and immediate revalidation both bind the exact
  provider profile/generation/revocation epoch, provider account identity,
  billing-scope identity, effective configuration, capability snapshot, price
  snapshot, request digest, reservation, provider, purpose, and closed endpoint.
  No available profile, account, billing scope, or endpoint may be substituted.
- Worker and helper bootstrap messages are serialized only to inherited
  private handles. They are forbidden from command lines, environments,
  settings, runtime descriptors, application IPC, logs, diagnostics, and
  durable records.
- Provider endpoints are closed enums. No contract carries a caller-selected
  host or URL.
- Provider dispatch requires a second, immediate
  `DispatchRevalidationRequest`/`Response`; a prior reservation or assignment
  is not transport authority.
- Provider dispatch assignment and revalidation also carry the versioned local
  input-bound proof. The current proof state is `AUTHORITY_REQUIRED`: helper
  validation accepts only the corresponding rejected disposition and never
  fabricates canonical byte counts or token bounds. Credential-only assignments
  and receipts omit all provider-dispatch-only fields.
- Raw display/detail/status strings are inert bounded text. They cannot be
  interpreted as instructions or privileged primitives.
- No schema contains `google.protobuf.Any`, `Struct`, a generic object lookup,
  arbitrary metadata map, raw database query, or generic command envelope.

These schemas establish contract shape only. ADR-0019's restrictive named-pipe
DACLs, current-user/elevation checks, remote rejection, finite transport
buffers/rate limits, process supervision, private-handle inheritance, staging
authorization, and coordinator-side admission remain runtime obligations for
later slices.

The additive application v1 surface is at protocol 1.11.0. It separates the
application 1.11.0, domain 1.5.0, storage 1.15.0, and renderer 1.1.0 version
axes; the corrected persisted `TargetedVerificationPlan` is schema 1.1.0. The
surface exposes bounded display-safe bootstrap, typed setup/configuration,
prepared manual-run, non-secret provider status, live progress, reconnect,
canonical result exploration, FindingReport queues/detail, append-only durable
review shapes, structured-export deletion/tombstone operations, and an
explicit unavailable state when retained results predate a durable report
publication projection. Five native-only targeted-verification RPCs expose
durable preparation, canonical finding/case identity-envelope authority,
independently paged scope, dependency, target-analyzer, lifecycle, and artifact
evidence, fresh snapshot/evidence acquisition, inspectable proof/correlation/
reuse/limit state,
atomic `managed-analysis-v1` successor admission, and immutable lineage/readback.
`StartTargetedVerification` remains
`native-only-never-map`; no renderer operation was added. The
renderer registry still contains only its five previously accepted
operation/message combinations; renderer 1.1.0 updates bootstrap compatibility
and Phase C capability flags without adding desktop operations. The full
protobuf contract-set fingerprint is
`eaf72f2bd8c04ad16035ff7ae45ea4c08b514216a0b0f07ce50e7560c55342d8`.
Helper v2 has a separate fingerprint over only its helper/common/identity
transitive closure; its fail-closed decoder rejects unknown nested fields,
unknown enum numerics, and contradictory assignment, revalidation, or receipt
states. That helper-v2 transitive fingerprint is
`d0cf1a594ceeaf5ec32c3b40bf9f39ccc19bfb1b41aeb0a65c66ab3db2cf41d1`.
Helper v1 remains independently parsed under its own frame identity.
