# RESEARCH-0057: Frontend application foundation gap

Status: Completed
Disposition: Recommendation accepted by the project owner

Date: 2026-08-25
Last reviewed: 2026-08-25
Researcher: Codex
Accepted: 2026-08-25
Accepted by: Project owner
Research question: RQ-041

## Plain-language result

M1 built a durable analysis engine, but the future interface cannot yet use it
as an ordinary application. The backend has useful low-level controls for runs,
progress, retained outputs, and provider operations; it does not yet have one
complete, safe, user-oriented contract for setup, scan preparation, result
browsing, review decisions, assumptions, targeted verification, or exports.

The recommended solution is a bounded post-M1 transition named **Frontend
Application Foundation**. It will build and qualify that missing application
layer before M2 attempts the polished user experience. A minimal diagnostic
React consumer is required so the backend contract is shaped by a real
consumer rather than guessed in isolation. The transition is not M1.5 and does
not deliver M2's product interface.

## Question and requirements

RQ-041 asks:

> What exact application-facing contract, presentation projections, user-state
> operations, generated client, and desktop-host bridge must exist before M2
> can plan and implement its finding-centric frontend without exposing backend
> internals or privileged local operations?

The primary governing requirements are:

- PROD-003 and PROD-004;
- SCOPE-003, SCOPE-004, and SCOPE-006;
- AUTH-001 through AUTH-003;
- SEC-001 through SEC-004;
- SCAN-001 through SCAN-006 and SCAN-009;
- INTENT-001 through INTENT-005;
- FIND-001 through FIND-014;
- TOOL-001 through TOOL-003;
- UX-001 through UX-006; and
- OPS-001 through OPS-005.

The M2 milestone summary additionally requires tool confirmation, path
overrides, profile selection, granular scan configuration, progress/time/cost,
summary/readiness, supported and lead-only queues, evidence expansion,
dispositions, assumptions, a focused mod view, and targeted verification.

## Scope

This investigation covers the current active repository only:

- accepted product and architecture documents;
- the current application protobuf contract;
- the coordinator application service;
- current domain, persistence, report-projection, and CLI surfaces; and
- the accepted M1 product-conformance boundary.

It does not inspect an archive, the private fixture repository, retired
evaluator code, or abandoned implementation. It does not select a production
design system, create polished interface screens, widen analyzer semantics, or
authorize a live provider call.

## Sources and inspected implementation

Authoritative documents:

- [Product scope and milestones](../../product/scope-and-milestones.md)
- [Product requirements](../../product/requirements.md)
- [ADR-0017](../../architecture/decisions/ADR-0017-windows-desktop-application-stack.md)
- [ADR-0019](../../architecture/decisions/ADR-0019-local-ipc-and-application-query-contract.md)
- [ADR-0020](../../architecture/decisions/ADR-0020-credential-storage-and-provider-dispatch.md)
- [ADR-0021](../../architecture/decisions/ADR-0021-desktop-and-local-operation-security-boundary.md)
- [ADR-0022](../../architecture/decisions/ADR-0022-finding-and-case-continuity-and-reconciliation.md)
- [ADR-0023](../../architecture/decisions/ADR-0023-atomic-cost-ledger-and-hard-budget-enforcement.md)
- [Product-conformance verification profile](../../evaluation/product-conformance-verification-profile.md)

Current implementation inspected:

- `contracts/protobuf/infinium/application/v1/application.proto`;
- `src/Infinium.Coordinator/ApplicationGrpcService*.cs`;
- `src/Infinium.Application/Analysis/FindingReportProjection.cs`;
- `src/Infinium.Domain/Contracts/FindingReportContracts.cs`;
- `src/Infinium.Persistence/`; and
- `src/Infinium.Cli/Program.cs`.

No external technical claim was required. Existing accepted ADRs already
select the stack, process topology, transport, credential boundary, and local
security model.

## Findings

### 1. The durable application substrate exists

The application endpoint already supports version negotiation, health, run
listing and detail, finding-summary pagination, progress snapshots, bounded
events and resynchronization, durable run commands, snapshot capture, retained
analysis outputs and provenance, and provider-operation inspection.

The accepted production path can start a run through application gRPC, retain
the work under coordinator authority, publish atomically, and read it back.
This means the transition does not need to replace the engine, coordinator,
named-pipe transport, or durable run model.

### 2. The present API is partly developer-oriented

The current manual-start command accepts an analysis orchestration JSON body.
Several analysis/provider operations expose implementation and diagnostic
concepts useful to the CLI and conformance work. React must not become a generic
proxy for those controls.

The interface needs typed, finite user operations such as selecting a profile,
saving a scan configuration, starting a validated scan, recording a review
decision, or requesting targeted verification. Internal JSON, arbitrary object
lookups, paths, commands, provider requests, and persistence identities are not
valid renderer authority.

### 3. The finding report is not yet an application query surface

The post-M1 cleanup added a truthful `FindingReport` projection and the
`scope-reports` CLI command. That projection is currently derived from a local
retained analysis artifact. The application service can list abbreviated
finding summaries, but it cannot yet provide the complete paginated queue,
case/investigation detail, report, evidence expansion, or focused-mod read
models required by M2.

### 4. Setup and scan preparation lack a complete user workflow

The backend contains exact MO2 capture, configuration, capability, provider,
and lifecycle substrates, but the application endpoint does not provide one
coherent workflow for:

- tool detection, confirmation, validation, and override;
- explicit profile selection;
- saved configuration creation, cloning, editing, and deletion;
- pre-run capability/gap and estimate review;
- safe provider enrollment/status; and
- submission of a typed effective run request.

These are backend-owned application behaviors even though M2 will decide how
they are visually presented.

### 5. Review state and assumptions need durable product operations

Analyzer decisions are not user review dispositions. M2 needs append-only,
revision-bound user decisions, suppressions, annotations, assumptions, and
targeted verification links that preserve historical analytical truth under
ADR-0022. The current application contract does not expose those operations as
a complete producer/persistence/readback path.

### 6. The renderer bridge remains deliberately unimplemented

ADR-0017 selects React/TypeScript in a minimal WPF/WebView2 host. ADR-0019 says
React never connects directly to coordinator gRPC, and ADR-0021 forbids a
generic host proxy. The repository currently contains no desktop shell,
generated renderer client, or executable bridge qualification.

Before M2 can rely on this stack, a diagnostic consumer must prove controlled
origin loading, closed messages, paginated queries, live progress,
reload/reconnect, hostile-input rejection, accessibility checks, and measured
resource behavior.

### 7. A purely backend-first contract would create avoidable rework

Freezing presentation objects before any TypeScript consumer exists would
repeat the same mistake this transition is intended to prevent. The foundation
therefore needs a small diagnostic React consumer. It is not the product UI;
it is implementation evidence that the proposed data and operation contract
can actually be consumed safely.

## Alternatives

### Use the current application API directly from M2

Rejected. It would expose developer-oriented operations, leave required
workflow state absent, and force each screen to interpret internal objects or
artifacts independently.

### Let React read SQLite or run artifacts

Rejected. It bypasses coordinator authorization, migrations, pagination,
retention, query bounds, security controls, and durable projection semantics.

### Build the polished M2 interface and add backend calls screen by screen

Rejected as the starting sequence. It would make architecture and frontend
planning depend on missing operations, encourage ad-hoc bridge growth, and
prevent a coherent security and reconnect proof.

### Freeze a complete presentation contract without a consumer

Rejected. A contract-only design can establish a starting shape but cannot
prove that the shape is usable, bounded, or sufficient.

### Build a bounded application foundation plus diagnostic consumer

Selected. It reuses the accepted engine/IPC/security architecture, implements
the missing product-facing operations vertically, and produces measured
evidence for the later M2 plan without claiming that the polished workflow is
already delivered.

## Uncertainty and limitations

- Exact screen composition and visual information hierarchy remain M2 work.
- Resource and interaction thresholds require measurements from the executable
  host spike before the M2 plan can bind production acceptance targets.
- Real interface consumption may reveal a clean-break contract correction.
  The transition therefore ends with a producer-consumer-validated M2-ready
  candidate, not a milestone-stable frontend contract.
- The transition does not broaden semantic analysis accuracy or supported
  modlist-problem coverage.
- Independent semantic-oracle qualification remains deferred through M2.

## Recommendation

Accept ADR-0037 and the
[M1-to-M2 Foundation — Frontend Application Foundation plan](../../plans/transitions/m1-to-m2/frontend-application-foundation/plan.md).
Implement the work in five orchestration phases:

1. authority and contract foundation;
2. setup and execution workflow;
3. results and review workflow;
4. desktop consumption proof; and
5. integrated acceptance and M2 handoff.

Each phase may use one orchestrator across its related work packages. Package
receipts remain distinct, but routine automation pauses only at phase
checkpoints or a genuine escalation condition.

## Decision and follow-up enabled

- [ADR-0037](../../architecture/decisions/ADR-0037-frontend-application-contract-and-desktop-bridge.md)
- [M1-to-M2 Foundation — Frontend Application Foundation](../../plans/transitions/m1-to-m2/frontend-application-foundation/README.md)
- EVAL-0090 through EVAL-0094 in the
  [evaluation case catalog](../../evaluation/case-catalog.md)
