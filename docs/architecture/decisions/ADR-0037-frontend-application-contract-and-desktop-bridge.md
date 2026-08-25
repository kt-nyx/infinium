# ADR-0037: Frontend application contract and desktop bridge

Status: Accepted
Date: 2026-08-25
Accepted: 2026-08-25
Accepted by: Project owner
Last reviewed: 2026-08-25
Supersedes: None
Superseded by: None

## Plain-language decision

Infinium will add one safe application layer between its durable backend and
the future React interface. The backend will prepare bounded, user-meaningful
views and accept only specific user actions. React will not receive database
access, internal orchestration JSON, credentials, arbitrary paths, commands,
provider operations, or a generic gRPC proxy.

A minimal WPF/WebView2 diagnostic client must consume this layer before M2
relies on it. That client proves the contract and security boundary; it is not
the polished M2 product interface.

## Context

M1 implemented a durable coordinator, role-separated named-pipe gRPC,
application queries, run lifecycle, progress/events, retained analysis output,
provider operations, and a backend finding-report projection. The accepted M2
goal is a finding-centric frontend workflow against stable backend contracts.

ADR-0017 deliberately did not choose presentation DTO details. ADR-0019
requires bounded application queries and says React communicates through a
minimal WPF host rather than directly with coordinator gRPC. ADR-0020 keeps
credential bytes in a one-shot helper, and ADR-0021 requires one controlled
origin and a closed, deny-by-default renderer bridge.

[RESEARCH-0057](../../research/investigations/RESEARCH-0057-frontend-application-foundation-gap.md)
found that the current API is a strong engine/CLI substrate but does not yet
cover the complete setup, scan-preparation, results, review-state, assumption,
targeted-verification, export, or renderer-consumption workflow.

## Decision drivers

- M2 needs a stable and understandable frontend boundary before polished
  screens are implemented.
- Backend domain and persistence objects must not become accidental UI
  contracts.
- React cannot receive generic local authority or connect directly to the
  coordinator.
- Large histories and evidence graphs require server-side query shaping and
  bounded detail expansion.
- Renderer reload or shell restart must not own durable state.
- Mutable user decisions must not rewrite immutable analytical history.
- Credential and path workflows need typed native mediation.
- The contract must be tested by a real TypeScript consumer before it is
  treated as M2-ready.

## Considered alternatives

### Extend the current gRPC service ad hoc for each screen

Rejected. Screen-by-screen additions would encourage inconsistent errors,
pagination, revision semantics, authority checks, and presentation meanings.

### Expose current protobuf or domain objects directly to React

Rejected. The current application protocol includes developer/operational
surfaces and is not a renderer authority contract. Direct reuse would couple
the frontend to internal orchestration and storage concepts.

### Give React direct coordinator gRPC access

Rejected by ADR-0019. Browser renderer compromise would gain a much broader
local operation surface, and named-pipe gRPC is not a browser contract.

### Use a local TCP/HTTP server for the renderer

Rejected for this boundary. It adds listener, port, firewall, origin, and
local-network concerns without a product requirement. ADR-0019's named-pipe
application client and ADR-0017's native bridge remain sufficient.

### Use a closed renderer contract mapped by the WPF host

Selected. It preserves the accepted process and transport design while making
renderer authority finite, generated, testable, and replaceable.

## Decision

### 1. Separate application and renderer contracts

Infinium shall maintain two related but distinct contracts:

1. the **application contract**, implemented as the coordinator-owned,
   versioned gRPC service used by generated native clients; and
2. the **renderer contract**, a smaller generated TypeScript message surface
   mapped by the WPF host to specific application-client operations.

The renderer contract is not a serialization of the complete application
service and the WPF host is not a generic gRPC proxy. Every renderer operation
must identify its owning application operation, bounds, user-gesture rule,
request/response shape, error states, cancellation behavior, and security
classification.

### 2. Use presentation projections rather than domain objects

Application queries shall return bounded read models prepared for user
workflows. These presentation projections may combine durable records but add
no analytical evidence and do not replace canonical domain/persistence truth.

The initial projection families are:

- application bootstrap, version, health, and capability state;
- tool installation/status and capability gaps;
- profile-selection candidates and confirmed selection;
- saved and effective scan configurations;
- pre-run estimate and limitation disclosure;
- run summary, lifecycle, progress, time, cost, failures, and gaps;
- supported-case and lead-only investigation queues;
- finding, case, evidence, provenance, and focused-mod details;
- dispositions, suppression, review annotations, and assumptions;
- targeted-verification lineage; and
- export requests, manifests, status, and resulting artifact metadata.

The application layer shall not make a presentation projection canonical merely
because a screen consumes it. Immutable run output, evidence, findings, cases,
lineage, and review events retain their accepted owners.

### 3. Use one bootstrap plus snapshot/resync model

A renderer session begins with one bounded bootstrap query containing only the
state needed to route the initial workflow: compatibility, coordinator health,
available capability groups, configuration status, current selection, and
recent/resumable operation summaries.

Detailed collections are queried separately. Live events are an optimization
over an authoritative snapshot. Every event/session uses explicit instance,
projection, sequence, and resynchronization semantics compatible with
ADR-0019. Renderer reload, process failure, shell restart, or event loss must
recover through authoritative queries rather than UI-owned replay.

### 4. Keep list and detail operations bounded

Lists use coordinator-owned allowlisted filtering, searching, sorting,
aggregation, stable keyset cursors, and finite pages. Details expand only a
named subject and may page or chunk large evidence groups. The current
application ceilings—1 MiB messages, 100 page items, 256 KiB chunks, 64 queued
stream items, 16 filter terms, and 4 sort terms—remain upper bounds unless a
clean-break revision is justified by measured implementation evidence.

No startup or ordinary detail operation sends an entire high-end profile,
finding population, evidence graph, retained history, or raw payload store to
the renderer.

### 5. Replace raw orchestration with typed user operations

Renderer-reachable run initiation shall use a closed typed request derived from
confirmed tool/profile state, a saved configuration revision, resolved semantic
context inputs, and an explicit user gesture. React cannot supply internal
analysis-orchestration JSON, payload identities, provider request bytes,
arbitrary paths, commands, URLs, or executable arguments.

The application layer resolves and validates the internal orchestration input,
then submits it through the existing coordinator-owned durable command path.
Idempotency receipts and indeterminate-command reconciliation remain mandatory.

### 6. Preserve immutable analysis and version mutable user state

User dispositions, suppressions, review annotations, and assumptions use
append-only, revision-bound events. Editing creates a new revision; it does not
rewrite the finding, case, prior analysis context, prior readiness evaluation,
or previous user event. Review-state carryover follows ADR-0022 and never
follows names or visual similarity alone.

Mutable operations require expected revision/generation values and return a
typed conflict with current non-secret state when stale. Targeted verification
creates a new manually initiated run or operation linked to exact prior
subjects and declared scope; it never mutates or reopens a terminal run.

### 7. Keep native path and credential workflows typed

React may request only a named native workflow such as “select MO2
installation,” “select LOOT installation,” or “add/replace provider key.” The
WPF host may parent the corresponding native interaction but cannot expose a
generic file picker result or credential value as renderer authority.

Tool candidates are validated for the exact tool/operation before becoming
saved configuration. Credential entry remains helper-owned under ADR-0020.
React and WPF receive only non-secret configured, replaced, cancelled, or typed
failure outcomes.

### 8. Use a closed renderer message envelope

Renderer requests and host responses/events shall use generated closed schemas
with at least:

- renderer-contract major/minor version;
- host session identity;
- request or subscription identity;
- exact finite operation kind;
- sequence/revision where applicable;
- bounded typed payload;
- user-gesture proof where required; and
- typed success, rejection, conflict, unsupported, unavailable, cancelled, and
  resync-required outcomes.

The host accepts messages only from the controlled packaged origin. Unknown
fields or operations, wrong origin/session/version, malformed or oversized
payloads, replayed one-shot gestures, invalid ordering, arbitrary paths,
commands, URLs, provider requests, and credential targets fail closed.

### 9. Generate clients and test data from owned contracts

Native and TypeScript client bindings shall be generated or mechanically
derived from reviewed contract sources. Handwritten convenience code may wrap
generated clients but cannot create hidden operations or reinterpret unknown
states as success.

Frontend development uses deterministic developer-owned story states covering
positive, empty, failure, gap, unavailable, stale, conflict, resync, and large
paginated conditions. These are product-conformance fixtures, not an
independent semantic oracle.

### 10. Qualify with a diagnostic consumer before M2

The M1-to-M2 Foundation — Frontend Application Foundation shall build a minimal
WPF/WebView2/React diagnostic consumer that exercises the actual generated
contract and application client. It must prove controlled-origin loading,
query pagination, progress/events, cancellation, reload/reconnect, malformed
and hostile rejection, credential/path isolation, accessibility mechanics,
and resource measurements required by ADR-0017.

The diagnostic consumer is development tooling. Product navigation, visual
design, complete accessibility acceptance, polished error recovery, packaging,
and the M2 finding-centric experience remain M2 work.

### 11. Use functional implementation names

Planning IDs may use `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`, but active
implementation names shall describe responsibility: for example application
bootstrap, tool configuration, finding detail, review state, renderer bridge,
or desktop host. New implementation types, paths, commands, schemas, or
projects shall not contain milestone, transition, phase, or work-package
chronology.

## Contract maturity

- New application and renderer shapes begin `Proposed`.
- Owning work packages move complete producer/persistence/consumer paths to
  `Implementation-active`.
- The diagnostic consumer may establish
  `Producer-consumer-validated` for the exact exercised surface.
- Transition closeout labels the coherent contract an **M2-ready candidate**.
- Only the real M2 interface and accepted M2 plan may make the complete
  user-facing boundary `Milestone-stable`.

An incompatible correction before M2 stability uses one explicit clean-break
revision and updates producers, consumers, persistence, generated clients,
fixtures, tests, and documentation together.

## Consequences

### Positive

- M2 can plan screens against explicit, tested queries and actions.
- React remains replaceable and unprivileged.
- The backend owns query shaping, durable state, security, and reconnect.
- Mutable user review state cannot rewrite analytical history.
- Generated clients and diagnostic story states reduce frontend/backend drift.

### Negative

- The repository gains TypeScript tooling, a WPF/WebView2 host, code generation,
  and another versioned contract surface.
- Some apparent UI work must be implemented in the backend before polished
  screens exist.
- A real M2 consumer may still require a coordinated clean-break correction.

### Risks and mitigations

- **The presentation contract mirrors storage:** require user-workflow read
  models and review each field's meaning and authority.
- **The WPF host becomes a second product UI or generic proxy:** keep product
  presentation in React and permit only mapped operations.
- **Frontend planning freezes guessed data:** require the diagnostic TypeScript
  consumer before transition acceptance.
- **Large data freezes the renderer:** preserve server pagination and exercise
  100,000 synthetic finding summaries without transferring the full set.
- **Review state leaks across findings:** use append-only revision-bound events
  and ADR-0022 reconciliation gates.
- **Credential or path bytes cross the bridge:** use helper-owned credentials,
  typed native workflows, canaries, and hostile-operation tests.

## Requirements affected

- PROD-003 and PROD-004
- SCOPE-003, SCOPE-004, and SCOPE-006
- AUTH-001 through AUTH-003
- SEC-001 through SEC-004
- SCAN-001 through SCAN-006 and SCAN-009
- INTENT-001 through INTENT-005
- FIND-001 through FIND-014
- TOOL-001 through TOOL-003
- UX-001 through UX-006
- OPS-001 through OPS-005

## Validation

No evaluation is passed by accepting this ADR.

- EVAL-0090 covers bootstrap, compatibility, capabilities, bounded queries,
  typed errors, and snapshot/resync.
- EVAL-0091 covers tool/profile/settings/configuration/pre-run workflow and
  typed path/credential mediation.
- EVAL-0092 covers summary, queues, finding/case/report/evidence/focused-mod
  projections and large-data bounds.
- EVAL-0093 covers append-only review state, assumptions, targeted
  verification, and user export.
- EVAL-0094 covers controlled origin, bridge denial, generated clients,
  reload/reconnect, accessibility mechanics, and measured desktop behavior.

The existing applicable EVAL-0020, EVAL-0026, EVAL-0027, EVAL-0033 through
EVAL-0049, EVAL-0064, EVAL-0066, EVAL-0069, EVAL-0076 through EVAL-0083, and
EVAL-0085 through EVAL-0089 obligations remain in force for exercised paths.

Revisit ADR-0017 and compare the accepted Avalonia fallback if the executable
qualification finds a material WebView2 security, accessibility, stability, or
resource failure. Revisit this ADR if the renderer requires direct coordinator
or filesystem access, the bridge cannot remain closed, or the application
contract cannot represent a required M2 workflow without exposing privileged
primitives.

## References

- [Product requirements](../../product/requirements.md)
- [Product scope and milestones](../../product/scope-and-milestones.md)
- [ADR-0017](ADR-0017-windows-desktop-application-stack.md)
- [ADR-0019](ADR-0019-local-ipc-and-application-query-contract.md)
- [ADR-0020](ADR-0020-credential-storage-and-provider-dispatch.md)
- [ADR-0021](ADR-0021-desktop-and-local-operation-security-boundary.md)
- [ADR-0022](ADR-0022-finding-and-case-continuity-and-reconciliation.md)
- [ADR-0023](ADR-0023-atomic-cost-ledger-and-hard-budget-enforcement.md)
- [ADR-0035](ADR-0035-defer-independent-semantic-oracle-qualification.md)
- [RESEARCH-0057](../../research/investigations/RESEARCH-0057-frontend-application-foundation-gap.md)
- [M1-to-M2 Foundation — Frontend Application Foundation plan](../../plans/transitions/m1-to-m2/frontend-application-foundation/plan.md)
