# M1-to-M2 Foundation — Frontend Application Foundation implementation plan

Status: Accepted
Disposition: Approved planning baseline; implementation has not started

Last reviewed: 2026-08-25
Owner: Project owner
Accepted: 2026-08-25
Plan ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`
Parent: completed M1 backend and post-M1 cleanup
Planning base: `32dbb2c48754666336d2da571e554ad8897ed71c`
Depends on: ADR-0017, ADR-0019 through ADR-0023, ADR-0035, ADR-0037,
RESEARCH-0057, and the accepted product-conformance profile
Next work package: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP1`

## 0. Plain-language outcome

M1 can run and retain a bounded analysis. It does not yet give a graphical
interface the complete, safe language needed to set up that run, display it,
record user decisions, or recover after a renderer restart.

This transition supplies that missing language and the backend behavior behind
it. At completion:

- a frontend can obtain one bounded startup picture of application health,
  configuration, capabilities, and resumable work;
- setup can detect and validate tools, require explicit profile selection, and
  save versioned scan configurations;
- a user gesture can start a run through a typed request rather than internal
  orchestration JSON;
- progress, time, cost, failures, and gaps remain live and reconnectable;
- summaries, cases, lead-only investigations, findings, evidence, provenance,
  focused-mod views, and reports are available through bounded queries;
- dispositions, suppressions, annotations, assumptions, targeted verification,
  and exports have durable product semantics;
- generated C# and TypeScript clients agree with the owned contracts;
- a minimal WPF/WebView2/React diagnostic client proves the renderer bridge,
  hostile-input controls, accessibility mechanics, reconnect behavior, and
  resource measurements; and
- M2 receives an explicit capability matrix, stable contract candidate,
  measured desktop evidence, known gaps, and planning handoff.

This is a bounded transition. It does not add analyzer families, claim broad
modlist safety, create polished screens, or complete M2.

## 1. Owner decision and activation

The project owner accepted this planning package on 2026-08-25, including:

- the official designation **M1-to-M2 Foundation — Frontend Application Foundation**;
- canonical ID `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION` rather than
  “M1.5”;
- ADR-0037's application/renderer contract boundary;
- the five-phase, nine-package decomposition below; and
- phase-level orchestration so related packages can run under one orchestrator
  without routine owner stops between packages.

Creating this plan does not itself begin implementation. When the owner directs
the transition to start, WP1 is authorized. After that start instruction, each
accepted phase checkpoint automatically unblocks the next phase. Ordinary
defects, review corrections, and failed tests remain same-candidate work and do
not return to the owner.

## 2. Authority and governing inputs

This plan consumes without redefining:

- [current project state](../../../../current-state.md);
- [development execution policy](../../../../execution-policy.md);
- [product requirements](../../../../product/requirements.md);
- [product scope and milestones](../../../../product/scope-and-milestones.md);
- [product workflows](../../../../product/workflows.md);
- [domain model](../../../../product/domain-model.md);
- [ADR-0017](../../../../architecture/decisions/ADR-0017-windows-desktop-application-stack.md);
- [ADR-0019](../../../../architecture/decisions/ADR-0019-local-ipc-and-application-query-contract.md);
- [ADR-0020](../../../../architecture/decisions/ADR-0020-credential-storage-and-provider-dispatch.md);
- [ADR-0021](../../../../architecture/decisions/ADR-0021-desktop-and-local-operation-security-boundary.md);
- [ADR-0022](../../../../architecture/decisions/ADR-0022-finding-and-case-continuity-and-reconciliation.md);
- [ADR-0023](../../../../architecture/decisions/ADR-0023-atomic-cost-ledger-and-hard-budget-enforcement.md);
- [ADR-0035](../../../../architecture/decisions/ADR-0035-defer-independent-semantic-oracle-qualification.md);
- [ADR-0037](../../../../architecture/decisions/ADR-0037-frontend-application-contract-and-desktop-bridge.md);
- [RESEARCH-0057](../../../../research/investigations/RESEARCH-0057-frontend-application-foundation-gap.md);
- [evaluation case catalog](../../../../evaluation/case-catalog.md);
- [fixture guidelines](../../../../evaluation/fixture-guidelines.md);
- [anti-overfitting rules](../../../../evaluation/anti-overfitting-rules.md); and
- [product-conformance verification profile](../../../../evaluation/product-conformance-verification-profile.md).

The [frontend capability matrix](frontend-capability-matrix.v1.json) is the
accepted starting traceability inventory. WP1 verifies it against the exact
implementation before production edits and keeps it current through WP9.

## 3. Scope

### 3.1 In scope

- A coherent application-facing contract for setup, scan preparation, run
  lifecycle, result exploration, user review state, assumptions, targeted
  verification, and export.
- User-meaningful presentation projections derived from canonical backend
  truth.
- Coordinator-owned typed queries, commands, validation, persistence,
  readback, replay, invalid-state behavior, and versioning.
- Tool detection/status/confirmation/override for the exact accepted MO2 and
  applicable LOOT boundaries.
- Explicit MO2 profile selection and confirmation.
- Saved scan-configuration create, inspect, name, clone, edit, and delete
  behavior with immutable effective run configuration.
- Pre-run capability, gap, work/time/cost estimate, and estimate-authority
  disclosure for the delivered subset.
- Typed manual start and durable lifecycle/progress/reconnect behavior.
- Summary/readiness, supported-case and lead-only queues, report/detail,
  evidence/provenance expansion, and focused-mod projections.
- Append-only revision-bound dispositions, suppression, annotations,
  assumptions, and targeted-verification lineage.
- User-initiated structured JSON export with exact selection and sharing-class
  metadata for the delivered local-private subset.
- Non-secret provider profile/status and the ADR-0020 WPF-parented,
  helper-owned enrollment choreography.
- Generated native and TypeScript contract/client artifacts.
- Deterministic developer-owned frontend story states.
- A minimal WPF/WebView2/React diagnostic consumer and security/accessibility/
  reconnect/resource qualification.
- Documentation, evaluation cases, verification tooling, current-state
  closeout, and M2 planning handoff.

### 3.2 Out of scope

- Polished M2 navigation, visual design, complete product screens, or usability
  acceptance.
- A production design system or component-library decision beyond what the
  diagnostic consumer requires.
- New semantic analyzer families or wider record/asset/script/configuration
  coverage.
- Any claim that no findings means a modlist is safe.
- M3 high-end scale, reliability, calibration, or trusted-preflight claims.
- Independent semantic-oracle authoring, review, sealing, comparison, or
  private-fixture access.
- Live OpenAI calls merely to build or test the application layer. Automated
  verification remains offline; any separately necessary live operation must
  use ADR-0036 and explicit effect authority.
- Generic renderer access to files, SQL, commands, URLs, providers, credentials,
  processes, MO2, LOOT, Mutagen, libloot, or coordinator gRPC.
- Installer, updater, signing, public packaging, public onboarding, or M4
  diagnostic bundles.
- Setup mutation, patch generation, automatic remediation, game launch, or
  continuous monitoring.
- Merge, push, release, or publication.

## 4. Current implementation boundary

The accepted starting state is summarized in the capability matrix. In
particular:

- the coordinator, durable lifecycle, named-pipe gRPC, CLI client, progress/
  events, retained output/provenance, and provider substrate exist;
- current list/query operations are bounded but do not cover the full M2
  workflow;
- `FindingReport` exists as an implementation-active projection and CLI/file
  consumer, not a complete application query surface;
- the manual start surface remains partly developer-oriented;
- product review-state and assumption workflows are not complete application
  verticals; and
- no desktop host, renderer contract, generated TypeScript client, or
  executable WebView2 qualification exists.

WP1 must verify these facts against the planning-base code. If an apparently
missing capability already exists, classify and test it rather than duplicating
it. If a current primitive is unsafe or unsuitable for React, retain it only
for its legitimate CLI/development consumer and add a separate typed user
operation.

## 5. Phase and orchestration model

“Stage” remains reserved for the evaluator lifecycle. This plan uses **phases**
as automation and handoff boundaries.

```text
Phase A — Authority and contract foundation
  WP1 Capability/authority inventory and executable contract map
  WP2 Application/renderer contract baseline and generated-client inputs
  -> Checkpoint A

Phase B — Setup and execution workflow
  WP3 Tool, profile, settings, configuration, estimate, and enrollment surface
  WP4 Typed run initiation, lifecycle, live state, and reconnect
  -> Checkpoint B

Phase C — Results and review workflow
  WP5 Summary, queues, report/detail, evidence, and focused-mod queries
  WP6 Dispositions, assumptions, targeted verification, and structured export
  -> Checkpoint C

Phase D — Desktop consumption proof
  WP7 Generated native/TypeScript client and deterministic story states
  WP8 WPF/WebView2/React host, bridge, and qualification
  -> Checkpoint D

Phase E — Integrated acceptance and M2 handoff
  WP9 End-to-end review, complete floor, measurement record, and closeout
  -> Checkpoint E / owner acceptance
```

### 5.1 Orchestrator behavior

One orchestrator may own every package in one phase. It shall:

1. verify the phase entry gate;
2. implement the first package as one coherent vertical candidate;
3. obtain focused package verification and a classified review;
4. correct must-fix findings on the same candidate;
5. record the package receipt without freezing or handing off the whole phase;
6. continue automatically into the next package in that phase;
7. perform one consolidated phase review after all phase packages pass; and
8. stop at the named phase checkpoint with a user-facing outcome and next
   automatically authorized phase.

A package-level failure is not a routine orchestrator stop. Stop within a phase
only for a genuine escalation, an explicit security/external-effect boundary,
or the recurrent-conceptual-defect diagnosis required by the execution policy.

### 5.2 Phase checkpoints

| Checkpoint | Required result | Default continuation |
|---|---|---|
| A | Exact capability map, accepted contract families, bounded operations, contract versions, and generated-source ownership are coherent | Phase B may start automatically |
| B | Setup through typed run start/progress/reconnect works through a generated native diagnostic client | Phase C may start automatically |
| C | Result exploration through durable review/verification/export works end to end without rewriting analysis truth | Phase D may start automatically |
| D | Generated TypeScript consumer and desktop bridge pass the exercised ADR-0017/0019/0020/0021 boundary; measurements are recorded | Phase E may start automatically if no stack-reopen trigger fires |
| E | Whole transition passes consolidated review and the complete accepted floor; M2 handoff is current and explicit | Stop for final owner acceptance |

Checkpoints A-D are automation handoffs, not routine owner approval gates. A
fresh orchestrator may resume from the checkpoint receipt without rereading
package chronology that is irrelevant to its phase.

## 6. Contract and fixture maturity

- Plan-only shapes begin `Proposed`.
- A work package owns its contract as `Implementation-active` until producer,
  persistence, generated consumer, round trip, malformed/unknown states, and
  focused fixtures agree.
- A phase checkpoint may mark only its exercised surface
  `Producer-consumer-validated`.
- WP9 may label the coherent boundary an `M2-ready contract candidate`.
- The transition does not grant `Milestone-stable`; that belongs to accepted
  M2 after the real interface consumes the boundary.

Every semantic/user-state fixture is developer-owned product-conformance
evidence. Product output does not author expected truth. No independent
semantic package is created or consulted.

## 7. Phase A — Authority and contract foundation

### 7.1 WP1 — Capability/authority inventory and executable contract map

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP1`
Phase: A
Objective: prove the exact starting surface and assign every M2 workflow need
to one owned query, command, event, persistence path, test family, and later
consumer.

Inputs:

- accepted plan/ADR/research baseline;
- current application protobuf and service implementations;
- current domain/persistence/report/CLI contracts; and
- `frontend-capability-matrix.v1.json`.

Allowed paths and actions:

- read and classify the active repository;
- update the plan-local capability matrix and implementation record;
- add answer-free contract examples and repository validation metadata;
- add focused architecture/conformance tests that expose the current gap; and
- make no product-behavior or desktop implementation change except the minimum
  executable contract inventory needed to prove ownership.

Vertical deliverables:

- exact RPC/query/command/event inventory with current consumers;
- current persistence/readback and migration ownership map;
- requirement/EVAL/capability traceability;
- explicit list of internal/developer operations not renderer-reachable;
- exact contract-family and code-generation ownership proposal;
- current bounds/version compatibility inventory; and
- zero `unknown` or unowned M2-foundation capability in the matrix.

Contract maturity: inventory only; new shapes remain `Proposed`.

Focused verification:

- strict JSON and schema validation of the matrix;
- documentation metadata/link validation;
- protobuf contract compilation and compatibility checks;
- repository searches proving there is no hidden desktop/renderer authority;
  and
- a fresh authority/security/provenance review of the mapping.

Recoverable failures: stale inventory, missing requirement links, duplicated
ownership, undocumented current RPCs, incorrect gap classification, and stale
documentation are corrected within WP1.

Genuine escalation: accepted requirements or ADRs assign incompatible owners,
or a required M2 behavior has no product meaning and choosing it would change
the product rather than expose existing meaning.

Next: WP2 automatically begins when the inventory review returns `ACCEPT`.

### 7.2 WP2 — Application/renderer contract baseline and generated-client inputs

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP2`
Phase: A
Objective: define and implement the shared contract substrate used by every
later package without exposing internal orchestration or generic local
authority.

Inputs:

- accepted WP1 map;
- ADR-0037;
- current application protocol limits and compatibility rules; and
- EVAL-0090 plus applicable EVAL-0033, EVAL-0035, and EVAL-0088 cases.

Allowed paths and actions:

- additive or clean-break application protobuf/contracts and validators;
- renderer-contract schema/source and generation configuration;
- bootstrap, typed error, compatibility, page/cursor, revision/conflict,
  request/receipt, event, cancellation, and resync primitives;
- generated-code ownership/build validation; and
- contract fixtures/tests only, without implementing complete later workflows.

Vertical deliverables:

- one bounded bootstrap/capability contract;
- common user-operation receipt and typed failure model;
- optimistic revision/conflict vocabulary;
- renderer request/response/event envelope;
- application-to-renderer operation registry that is closed by construction;
- code-generation inputs with deterministic output checks;
- explicit maximum message/page/chunk/filter/sort/queue values no weaker than
  the accepted application ceilings; and
- unknown/unsupported/version-mismatch behavior that never becomes success.

Contract maturity: common contract substrate reaches
`Producer-consumer-validated` through reference validators and generated test
consumers; feature-specific fields remain `Proposed` or
`Implementation-active` under later owners.

Focused verification:

- canonical round trip and generated-code drift checks;
- omitted/null/default/unknown-enum/unknown-field/version tests;
- malformed/oversized/replay/out-of-order request rejection;
- cursor/revision/session mismatch and resync tests;
- no generic path/SQL/command/URL/credential/provider fields; and
- contract diff/security review.

Recoverable failures: inconvenient field shape, code-generation drift,
incorrect optionality, missing typed errors, or incompatible additive changes
return to coordinated contract correction.

Genuine escalation: satisfying the contract requires direct renderer gRPC,
generic native authority, a new network listener, or an architecture change to
ADR-0017/0019/0021.

Next: Checkpoint A, then Phase B.

## 8. Phase B — Setup and execution workflow

### 8.1 WP3 — Tool, profile, settings, configuration, estimate, and enrollment surface

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP3`
Phase: B
Objective: let a future interface prepare a valid run through ordinary user
concepts while preserving explicit selection, tool validation, immutable run
inputs, budget truth, and credential secrecy.

Inputs:

- Phase A contracts;
- TOOL-001 through TOOL-003, SCOPE-003/004, SCAN-001/002/003/009;
- ADR-0008, ADR-0020, ADR-0023, and ADR-0037; and
- EVAL-0091 plus applicable EVAL-0023, EVAL-0026, EVAL-0064/0066,
  EVAL-0076/0077, EVAL-0080/0082/0089.

Allowed paths and actions:

- application/coordinator/domain/persistence contracts for setup and settings;
- exact MO2/LOOT detection, validation, status, and typed override workflows;
- explicit profile candidates, suggestion, selection, and confirmation;
- saved scan-configuration persistence and versioned CRUD/clone operations;
- effective configuration/context resolution and pre-run review/estimate;
- non-secret provider profile/status and enrollment-intent choreography; and
- focused developer-owned fixtures and native diagnostic client calls.

Vertical deliverables:

- tool state values `available`, `missing`, `unsupported`, `misconfigured`, and
  `not-yet-validated` with capability consequences;
- typed tool-specific selection/override operations, never a generic path API;
- suggestion-only MO2 saved selection and explicit confirmed profile binding;
- saved configuration list/detail/create/name/clone/edit/delete with revisions;
- immutable effective run configuration and separate semantic context;
- honest time/work/cost/coverage estimate with explicit unavailable authority;
- offline/no-credential path and user-owned credential status;
- WPF-parentable enrollment request returning only non-secret outcomes; and
- persistence/restart/readback for every mutable setup object.

Contract maturity: setup/configuration surface reaches
`Producer-consumer-validated` through the generated native diagnostic client.

Focused verification:

- valid/missing/unsupported/wrong-version/inaccessible tool cases;
- protected-root and typed-write/path-adversary cases;
- saved-selection suggestion cannot silently confirm or start a run;
- configuration concurrent-edit and immutable-effective-run cases;
- estimate authority, unavailable dimensions, and no invented cost;
- credential canaries absent from every ordinary channel; and
- backup/restart/readback and migration tests.

Recoverable failures: detector gaps for the accepted exact target, validator
bugs, settings migration defects, stale-revision behavior, estimate-display
ambiguity, or enrollment status mismatch.

Genuine escalation: a new tool invocation or version must be supported, a
generic path capability appears necessary, provider credentials would enter
ordinary IPC, or accepted product meaning does not choose between materially
different setup behaviors.

Next: WP4 automatically begins after focused review acceptance.

### 8.2 WP4 — Typed run initiation, lifecycle, live state, and reconnect

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP4`
Phase: B
Objective: replace renderer-reachable raw orchestration with a safe typed run
workflow and prove authoritative live state survives client loss.

Inputs:

- accepted WP3 setup/configuration objects;
- current durable run, command, progress, and event infrastructure;
- ADR-0016, ADR-0019, ADR-0023, and ADR-0037; and
- EVAL-0090/0091 plus EVAL-0026, EVAL-0038, EVAL-0044/0045,
  EVAL-0081/0087/0088.

Allowed paths and actions:

- typed pre-run confirmation and manual-start command;
- internal resolution into the existing orchestration path;
- durable command receipts and indeterminate-response reconciliation;
- run/stage/analyzer progress, elapsed/remaining time, usage/cost, failures,
  and gaps;
- bounded events, cancellation, snapshot/resync, and restart recovery; and
- native diagnostic client end-to-end execution using offline fixtures/fakes.

Vertical deliverables:

- no renderer-reachable raw analysis JSON or generic provider request;
- exact binding to confirmed profile, snapshot, context, effective
  configuration, input manifest, and initiation gesture;
- authoritative initial snapshot before events;
- progress rollups with no duplicated child/provider cost;
- explicit denominator/remaining-time/cost availability states;
- typed pause/resume/cancel behavior only where the accepted backend supports
  it, without widening milestone claims;
- renderer/client disconnect has no durable lifecycle effect; and
- reload, shell restart, coordinator restart, stale cursor, queue overflow,
  and event-gap recovery.

Contract maturity: setup-to-live-run surface reaches
`Producer-consumer-validated` through the generated native diagnostic client.

Focused verification:

- idempotent start and command reconciliation;
- changed/stale configuration/profile revisions fail before run admission;
- no implicit scan/network/provider work;
- progress snapshot/event equivalence and resync;
- slow client, oversized queue, cancellation, crash, and restart;
- immutable run bindings and terminal-run rules; and
- coordinator-only durable authority/security review.

Recoverable failures: incorrect mapping into current orchestration, event loss,
stale progress, race/fencing errors, lifecycle receipt ambiguity, or a client
incorrectly treating transport cancellation as durable cancellation.

Genuine escalation: the current lifecycle architecture cannot support a
required user action without reopening ADR-0016/0019/0023, or a UI-owned state
would be required for correctness.

Next: Checkpoint B, then Phase C.

## 9. Phase C — Results and review workflow

### 9.1 WP5 — Summary, queues, report/detail, evidence, and focused-mod queries

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP5`
Phase: C
Objective: expose the accepted M1 result meaning through bounded, progressive,
user-oriented application queries without inventing evidence or downloading a
whole run.

Inputs:

- accepted Phase A query substrate and Phase B run identities;
- current run output, finding/case, coverage, provenance, and `FindingReport`;
- PROD-003/004, UX-001 through UX-004, and OPS-005; and
- EVAL-0092 plus EVAL-0036, EVAL-0083 through EVAL-0086.

Allowed paths and actions:

- application list/summary/detail projections and coordinator query handlers;
- persistence indexes/projections required for bounded queries;
- finding-report publication/readback integration;
- supported-case and lead-only queue filters/sorts/search;
- evidence/provenance/detail chunk expansion; and
- focused-mod aggregation for exact retained subjects.

Vertical deliverables:

- summary/readiness or explicit scope-limited/no-readiness state;
- prose summary, finding/case/lead counts, coverage, cost, duration, and
  failures;
- separately queryable supported and lead-only items;
- paginated finding/case/report queues with stable opaque cursors;
- complete bounded finding and case detail;
- evidence/provenance expansion without raw payload paths or active markup;
- focused-mod view showing applicable findings/cases/evidence/coverage; and
- explicit empty, abstained, limited, failed, unsupported, stale, and gap
  states with no safety guarantee.

Contract maturity: result-query surface reaches
`Producer-consumer-validated` through generated native and reference
TypeScript consumers; `FindingReport` remains implementation-active until the
real M2 interface validates it.

Focused verification:

- positive, negative, lead-only, abstention, failure, limited, and gap views;
- deterministic pagination/filter/sort/search and cursor invalidation;
- 100,000 synthetic summaries queried without full-population transfer;
- hostile text remains inert data;
- report projection adds no evidence and round-trips to canonical sources;
- focused-mod aggregation neither merges unrelated causes nor hides gaps; and
- query latency/message-size measurements recorded for later M2 thresholds.

Recoverable failures: N+1/unbounded queries, insufficient projection fields,
misleading prose, missing gaps, cursor instability, or consumer-discovered
contract awkwardness.

Genuine escalation: a required view can only be produced by changing accepted
finding/case/evidence meaning, or accurate display would require authority not
owned by the application service.

Next: WP6 automatically begins after focused review acceptance.

### 9.2 WP6 — Dispositions, assumptions, targeted verification, and structured export

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP6`
Phase: C
Objective: implement durable user interaction with results without changing
the analysis that produced them.

Inputs:

- WP5 result identities and views;
- ADR-0002, ADR-0015, ADR-0021, ADR-0022, and ADR-0037;
- INTENT-001 through INTENT-005, FIND-005 through FIND-014, UX-005, OPS-002/003;
  and
- EVAL-0093 plus EVAL-0019/0020/0027/0040/0041/0043/0047/0048/0069/0078/0079.

Allowed paths and actions:

- append-only review-event and assumption contracts/persistence/projections;
- create/inspect/update/remove-from-effective-set/revalidate operations;
- disposition/suppression/annotation revision and carryover validation;
- typed targeted-verification planning and manual start linkage;
- local-private structured JSON export operation and exact selection manifest;
  and
- focused fixtures for concurrency, history, deletion gaps, and restart.

Vertical deliverables:

- per-revision disposition, suppression, and annotation events;
- inferred versus user-provided assumption origin separate from confirmation;
- assumption edit/removal creates successor context/effective state rather than
  rewriting history;
- stale revisions return typed conflict and current safe state;
- no implicit review-state carryover from names, prose, or visual similarity;
- targeted verification names exact source finding/case/scope and creates new
  manually initiated work;
- original and successor results remain linked and immutable; and
- JSON export retains exact selection, revisions, filters, sharing class,
  schema/generator, omissions, privacy/source-policy decisions, and provenance.

Contract maturity: review/assumption/verification/export surface reaches
`Producer-consumer-validated` through generated diagnostic consumers.

Focused verification:

- create/edit/delete/revalidate and concurrent stale-revision cases;
- finding disposition cannot alter analyzer output or prior readiness;
- exact/ambiguous/changed continuity and no suppression leakage;
- needs-input remains distinct from job pause and disposition;
- targeted verification never borrows unrelated coverage or reopens a terminal
  run;
- export creation/deletion does not mutate sources and restricted material is
  omitted/cited correctly; and
- persistence, restart, backup/restore, deletion-preview, and replay checks.

Recoverable failures: review projection mistakes, missing append-only records,
incorrect carryover, context mutation, export manifest incompleteness, or
targeted-scope overreach.

Genuine escalation: accepted product documents do not define a required user
decision meaning, or a requested export sharing class requires unresolved
privacy/redistribution authority beyond the local-private M2 subset.

Next: Checkpoint C, then Phase D.

## 10. Phase D — Desktop consumption proof

### 10.1 WP7 — Generated native/TypeScript client and deterministic story states

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP7`
Phase: D
Objective: give desktop/frontend development reproducible generated clients
and bounded product-conformance states without building the polished product
interface.

Inputs:

- producer-consumer-validated Phase A-C contracts;
- ADR-0017, ADR-0037, and functional naming governance; and
- EVAL-0090 through EVAL-0094.

Allowed paths and actions:

- TypeScript toolchain/project for contract generation and tests;
- generated C# application-client wrappers where needed;
- renderer operation registry and bridge adapter interfaces;
- deterministic story-state fixtures and fake application client;
- compile/type/round-trip/drift tests; and
- minimal non-visual diagnostic controls needed by WP8.

Vertical deliverables:

- one owned generation source and deterministic generated output;
- exhaustive operation/type mapping with no handwritten hidden operation;
- typed cancellation, conflict, unsupported, unavailable, and resync handling;
- story states for setup, empty, active, completed, failed, gap, lead-only,
  stale, conflict, reconnect, and 100,000-item pagination;
- fake client and real-client switch that cannot change product semantics; and
- no milestone/phase/WP names in implementation paths or symbols.

Contract maturity: generated clients become real consumers of every exercised
Phase A-C contract; mismatches return to the owning contract before WP8.

Focused verification:

- deterministic generation and clean-tree drift check;
- TypeScript strict compilation/lint/unit tests;
- protobuf/schema/TypeScript/C# round-trip and unknown-state tests;
- story-state contract validation and mutation/metamorphic cases; and
- dependency/license inventory for added frontend tooling.

Recoverable failures: codegen instability, TypeScript optionality mismatch,
unrepresentable 64-bit values, enum drift, fake/real behavior divergence, or
dependency tooling defects.

Genuine escalation: required code generation needs an incompatible license or
remote build dependency, or the renderer contract cannot be represented
without exposing an ADR-prohibited operation.

Next: WP8 automatically begins after focused review acceptance.

### 10.2 WP8 — WPF/WebView2/React host, bridge, and qualification

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP8`
Phase: D
Objective: prove the selected desktop stack can consume the application
foundation safely and responsively before M2 relies on it.

Inputs:

- WP7 generated clients/story states;
- actual Phase A-C application service;
- ADR-0017, ADR-0019, ADR-0020, ADR-0021, and ADR-0037; and
- EVAL-0094 plus applicable EVAL-0033 through EVAL-0035, EVAL-0064,
  EVAL-0080, EVAL-0088, and EVAL-0089.

Allowed paths and actions:

- minimal non-elevated WPF host and WebView2 lifecycle;
- packaged first-party React diagnostic assets under the controlled origin;
- exact-origin closed message bridge and generated operation adapters;
- typed native tool-selection and credential-enrollment choreography;
- renderer crash/reload, browser-process failure, shell restart, and
  coordinator reconnect behavior;
- diagnostic accessibility and virtualization mechanics; and
- measurement harness/receipts for startup, memory, query, bridge, package,
  dependency, and renderer behavior.

Vertical deliverables:

- controlled non-resolving application HTTPS origin and packaged assets;
- restrictive CSP/Trusted Types where supported;
- denied navigation, frames/resources, downloads, permissions, new windows,
  DevTools, remote debugging, and inherited privileged flags in release;
- no generic host objects or native proxy;
- strict origin/session/version/sequence/gesture/size/operation validation;
- actual paginated finding query, progress/event operation, cancellation, and
  reload/reconnect through the real application client;
- 100,000 synthetic summary virtualization without full transfer;
- missing/outdated Evergreen and browser-process failure behavior;
- local-only/no-credential operation;
- keyboard/focus/naming/landmark/contrast/zoom/reduced-motion/screen-reader/
  automated accessibility evidence for the diagnostic workflow; and
- cold/warm startup, idle/active private working set, query latency, bridge
  latency, message size, package size, runtime dependency, and license
  measurements suitable for M2 threshold selection.

Contract maturity: exact exercised renderer/host/application path reaches
`Producer-consumer-validated`; it remains a diagnostic rather than product UX.

Focused verification:

- hostile navigation/new-window/download/permission/origin/operation/payload/
  replay/order/path/command/URL/provider/credential cases;
- no secret canary in renderer, WPF, bridge, logs, crash artifacts, or ordinary
  IPC;
- renderer reload/shell restart during durable work;
- slow consumer/backpressure/resync and browser-process recovery;
- WebDriver/host/accessibility automation for the representative diagnostic
  path;
- resource measurements on a recorded reference machine; and
- fresh desktop-security/accessibility/dependency review.

Recoverable failures: bridge schema defects, focus/keyboard bugs, renderer
state loss, missing typed recovery, excessive nonessential payload, packaging
mistakes, or a security control omitted by implementation.

Genuine escalation: a material WebView2 security, accessibility, stability, or
resource failure meets ADR-0017's stack-reopen trigger; the accepted response
is a bounded equivalent Avalonia comparison, not an ad-hoc shell substitution.

Next: Checkpoint D, then Phase E if no reopen trigger fires.

## 11. Phase E — Integrated acceptance and M2 handoff

### 11.1 WP9 — End-to-end review, complete floor, measurement record, and closeout

Work ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP9`
Phase: E
Objective: prove the whole foundation is coherent, truth-preserving, secure,
recoverable, and sufficient input for a separately accepted M2 plan.

Inputs:

- accepted Checkpoints A-D;
- exact Phase A-D candidate and implementation record;
- complete capability matrix and EVAL-0090 through EVAL-0094; and
- accepted product-conformance profile.

Allowed paths and actions:

- correction of any in-scope consolidated-review finding;
- cross-package integration fixtures and end-to-end diagnostic workflow;
- complete verification, documentation, capability-matrix, and current-state
  closeout;
- recorded M2 planning thresholds/recommendations based on measurements; and
- no new feature or architecture expansion.

Vertical acceptance workflow:

1. bootstrap and negotiate versions;
2. detect/report tools and capability gaps;
3. explicitly select a profile;
4. create/select a saved scan configuration;
5. review estimate, limits, offline/provider state, and unavailable authority;
6. start a typed manual run;
7. observe live progress/time/cost/failures/gaps;
8. reload the renderer and reconnect/resync;
9. open summary/readiness or scope-limited status;
10. page supported and lead-only queues;
11. open finding/case/report/evidence/provenance/focused-mod details;
12. record a disposition/annotation and an assumption revision;
13. request targeted verification and retain exact lineage;
14. restart shell/coordinator and prove durable readback;
15. create and inspect a local-private structured JSON export; and
16. prove hostile/unknown/oversized/stale operations fail closed.

Consolidated review:

- product and workflow meaning;
- domain/presentation authority separation;
- immutable analysis and append-only user state;
- setup/tool/profile/configuration correctness;
- budget/provider/credential separation and no fallback;
- progress/event/reconnect durability;
- finding/case/report/evidence/provenance truth;
- pagination/query/resource bounds;
- renderer/bridge/local-operation security;
- accessibility mechanics and measured performance;
- persistence/migration/backup/replay/deletion/export;
- functional naming and dependency/license posture;
- private/evaluator isolation and claim wording; and
- complete diff and documentation/current-state accuracy.

Contract maturity: the accepted whole becomes an **M2-ready contract
candidate**. The record must explicitly state which surfaces are
producer-consumer-validated and which remain implementation-active until the
real M2 interface consumes them.

Focused and final verification:

- all phase-specific verification remains passing;
- EVAL-0090 through EVAL-0094 pass for the exact delivered surface;
- the full product-conformance floor passes once on the review-ready candidate;
- frontend build/type/lint/test, host integration, WebDriver/accessibility,
  security/adversarial, generation-drift, and measurement checks pass;
- documentation metadata, links, strict JSON, naming governance, dependency
  manifest, formatting, and diff checks pass;
- no ordinary verification accesses private fixtures, archives, network, or a
  live provider; and
- repository-owned `dotnet`/`testhost` survivors equal zero after every run,
  with graceful `dotnet build-server shutdown` used only when a verified SDK
  server holds repository output open.

Recoverable failures: any implementation, fixture, schema, generated-client,
host, test, measurement, documentation, or integration defect within accepted
scope returns to same-candidate correction and changed-surface review.

Genuine escalation: a Phase D stack-reopen trigger survives correction; a
required M2 workflow needs new product meaning; accepted ADRs conflict; or
completion would require private-answer, credential, protected-root,
destructive, network, or billable authority not granted by this plan.

Next: Checkpoint E and final owner acceptance. M2 remains inactive until its
own accepted plan exists.

## 12. Package-control matrix

| Package | Direct result | Contract result | Unblocks |
|---|---|---|---|
| WP1 | Exact capability and authority map | Proposed shapes assigned to owners | WP2 |
| WP2 | Common application/renderer substrate | Common primitives producer-consumer-validated | Checkpoint A / WP3 |
| WP3 | Setup, settings, profile, configuration, estimate, enrollment | Setup surface producer-consumer-validated | WP4 |
| WP4 | Typed run/live/reconnect workflow | Setup-to-live-run path producer-consumer-validated | Checkpoint B / WP5 |
| WP5 | Bounded summary/queue/detail/evidence/focused-mod queries | Result read surface producer-consumer-validated | WP6 |
| WP6 | Durable review/assumption/verification/export behavior | User-action surface producer-consumer-validated | Checkpoint C / WP7 |
| WP7 | Generated native/TypeScript clients and story states | Generated consumers agree with Phase A-C | WP8 |
| WP8 | Executable secure desktop diagnostic consumer | Exercised desktop path producer-consumer-validated | Checkpoint D / WP9 |
| WP9 | Consolidated accepted candidate and M2 handoff | M2-ready candidate; not milestone-stable | Final owner acceptance |

## 13. Verification profile additions

Every phase uses the common floor only when its candidate is review-ready.
During development use focused project/category tests and the smallest
diagnostic workflow that exercises the changed vertical path.

The eventual final floor shall include, in addition to the current accepted
commands:

```powershell
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-documentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-functional-naming.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
git diff --check
```

WP7/WP8 shall add exact locked frontend restore/build/type/lint/test and desktop
qualification commands after selecting the implementation tooling. Those
commands, versions, offline/cache behavior, and generated-output paths become
part of the accepted floor before Checkpoint D; they may not depend on an
implicit global Node installation or live package download during final
verification.

## 14. Required orchestrator reports

At each phase checkpoint, the orchestrator reports in plain language:

1. what the phase made possible for an eventual user;
2. which packages and vertical paths are complete;
3. which contracts changed and their maturity;
4. focused verification and consolidated review evidence;
5. material defects corrected and any remaining limitations;
6. resource/security/accessibility evidence where applicable;
7. owner decisions required, or an explicit statement that none exist;
8. exact repository-owned process-cleanup result; and
9. the next automatically authorized phase.

Dense commands, IDs, hashes, and counts follow the conceptual result. The
orchestrator does not require the owner to reconstruct the outcome from agent
logs or package receipts.

## 15. Recoverable failures and genuine escalation

Compile errors, missing handlers, contract/schema mismatches, query bugs,
persistence/migration defects, generated-client drift, frontend type errors,
host/bridge defects, accessibility failures, measurement regressions, stale
documentation, or failed conformance cases are recoverable. Correct and
re-review under the development execution policy.

Escalate only the affected path when:

- accepted product requirements or ADRs conflict or omit product meaning that
  materially changes the result;
- direct renderer/coordinator/filesystem/credential/provider authority appears
  necessary;
- the WebView2 qualification reaches ADR-0017's material stack-reopen trigger;
- a new tool/version/provider/network/billable operation needs expanded
  authority;
- private evaluator material, a secret, or a protected root would be exposed;
  or
- an external/destructive effect is required beyond this plan.

No test failure, review correction, fixture defect, or ordinary incomplete
implementation is an owner-level stop by itself. Independent in-scope work may
continue while one path is escalated.

## 16. Final acceptance and M2 handoff

The transition is ready for final owner acceptance only when:

1. every capability matrix entry has exact implemented/deferred evidence;
2. no renderer-reachable operation exposes a generic privileged primitive;
3. setup through run, results, review, targeted verification, and export works
   through generated clients;
4. the diagnostic WPF/WebView2/React consumer passes the exercised security,
   reconnect, accessibility, large-list, and measurement obligations;
5. canonical analysis truth and historical state remain immutable;
6. all mutable user actions are revision-bound and durable;
7. no credential or development-provider fallback reaches the product path;
8. consolidated review returns `ACCEPT`;
9. the complete final floor passes on one exact candidate;
10. repository-owned .NET/test-host survivors equal zero;
11. current documentation and implementation record state the honest claim
    boundary; and
12. a separately plannable M2 input set identifies screens/workflows, available
    operations, measured constraints, remaining contract-active seams, and
    known backend gaps.

Completion means the backend is ready to support M2 planning and implementation.
It does not mean the product is broadly reliable, the graphical workflow is
finished, or M2 is accepted.
