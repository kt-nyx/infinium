# Current project state

Status: Current execution authority

Last reviewed: 2026-08-10

Owner: Project owner

This is the single default navigation source for current milestone, slice,
work-package, and evaluator status. Update it when the accepted execution
handoff changes. Product documents and ADRs remain authoritative for product
meaning; accepted plans remain authoritative for package scope; implementation
records preserve evidence and history.

## Active handoff

| Field | Current value |
|---|---|
| Milestone | `M1` — active |
| Active slice | `M1/S5` — active |
| Completed package | `M1/S5/WP1` through `M1/S5/WP5` |
| Package awaiting owner acceptance | `M1/S5/WP6` — implementation and independent corpus review complete; two final-review correction cycles closed in the candidate tree; fresh exact-commit re-review pending; not owner-accepted |
| Next eligible action | Commit and freshly review the replay/oracle correction candidate, then owner review only if that exact verdict is `ACCEPT` |
| Later work | Successor-slice planning only after explicit Slice 5 owner acceptance; no successor implementation is authorized |
| Default execution policy | [Development execution policy](development/execution-policy.md) |
| Active milestone plan | [M1 backend semantic proof](plans/milestones/M1-backend-semantic-proof.md) |
| Active slice plan | [M1 Slice 5](plans/slices/M1-slice-5-evidence-documentation-candidates-cases-replay.md) |
| Current implementation record | [M1 Slice 5 record](plans/implementation-records/M1-slice-5.md) |
| Public verification profile | [M1 continuation verification profile](evaluation/m1-continuation-verification-profile.md) |

## Evaluator status

- Private held-out evaluation is deferred and has no valid current product
  verdict.
- Protocol `/5` is retired unqualified and is not resumable.
- Protocol `/4` is frozen historical evidence and may run only through its
  bounded public regression wrapper where relevant. It is not a product,
  Slice 5, M1, held-out, reliability, or readiness verdict.
- Ordinary product work must not access the evaluator-private fixture
  repository and must not create, repair, retry, or replace private evaluator
  work.

## Current Slice 5 boundary

WP1 established the contract, codec, state, migration, and repository-boundary
foundation. WP2 completed the deterministic documentation product path, typed
persistence, and independently frozen public evidence. WP3 now completes the
answer-free delivered Bethesda/WP2 candidate input and bounded expansion
contracts, candidate/hypothesis/abstention selection, exact provenance and
dependency closure, schema-4 persistence/readback, attempt-fenced checkpoint
restart, and the real-source semantic, validation-scale, and streaming-stress
gates. Three independently authored public packages received a clean
product-blind fixture review before product comparison; a separate final
product review accepted the settled implementation with no remaining findings.
The full Release floor and `Candidates`, `CandidateScale`, and `Contracts`
gates pass. WP3 is complete. WP4 now adds the product-reachable finding input
producer, promotion and abstention rules, typed recommendations, causally
grouped supported and lead-only cases, immutable occurrence/logical identity,
all eight four-gate reconciliation outcomes, append-only lineage and lead
promotion, taxonomy history/projection, exact coverage member/gap/failure
ledgers, schema-5 typed persistence with accepted schema-4 migration, and the
no-safety publication boundary. Four independently authored generic public
packages were frozen and accepted before product comparison; the settled
product passed fresh review, the exact `M1Cases` surface, and the `Cases` gate.
WP5 now completes bounded `analysis-v1` execution from admitted, non-precomputed
inputs through a durable coordinator-owned WP2 -> WP3 -> WP4 phase graph,
managed-worker seal validation, atomic publication, retained replay and
targeted invalidation, typed result/provenance/output queries, and CLI readback.
The production entry is `Infinium.Cli start --analysis-request`; the Application
boundary atomically admits the run and durable operation before the executor
invokes the existing stage boundaries and checkpoints each retained result.
Restart, stale-attempt fencing, cancellation, wall/item limits, missing or
drifted dependencies, and terminal output are exercised on that same product
path. The immutable semantic-analysis context retains and validates its exact
ID, schema version, and canonical fingerprint through input, assignment,
replay, provenance, ordinary output, and terminal fallback.

Its independently authored 12-case development/validation package passed
closed-schema and answer-isolation validation before dispatch and a fresh
whole-object product comparison. The retained safety evidence physically
covers Windows final-object identity, hard links, junction/mount reparses,
relative/parent/case paths, canaries, handle-relative writes, and pinned-handle
races. Native symbolic-link creation was unavailable with Windows error 1314;
native 8.3, UNC, device, alternate-data-stream, and cross-volume qualification
remain explicit gaps or stand-ins. These gaps do not broaden the package into a
native-filesystem, external-adapter, readiness, full-EVAL, or whole-Slice
verdict. A fresh final product-path review traced real production execution
separately from test construction and found no remaining must-fix or authority
breach. WP5 is complete and WP6 is now eligible.

WP6 has assembled an independently authored and reviewed four-case public
cross-stage corpus over the exact eleven accepted WP2-WP5 fixture packages.
The ordinary product inputs were validated answer-free before dispatch, the
real WP2 -> WP3 -> WP4 -> coordinator/publication/query/output path ran before
the frozen oracle was loaded, and clean, unchanged incremental, changed-source,
and retained replay requests matched the expected typed counts and lifecycle
semantics. The final independent corpus review verdict is `ACCEPT`; all nine Slice 5 gates,
the new `Comprehensive` gate, the explicit traceability audit, the full Release
test floor, formatting, dependency-manifest, strict JSON, local-link, and diff
checks pass. This is bounded public synthetic local/fixture conformance only.
It is not a private, held-out, native-platform, controlled-real, reliability,
readiness, safety, whole-M1, or product-acceptance verdict.

The first exact whole-slice candidate received a `CORRECT` verdict because it
executed only D01, bypassed the managed coordinator/query boundary, overstated
traceability, left source-authority seals unresolved, and failed the whitespace
diff check. The first correction candidate then received `CORRECT` because its
D04 test masked `partial` product replay against a frozen `complete-clean`
oracle, and because ID-only documentation-node deduplication could hide drifted
retained metadata. The third exact candidate received `CORRECT` because the
replay projection still exposed a partial semantic gap while claiming complete
replay, its alias test stopped before the intended guard and the reachable
guard used a generic failure type, and prefix-wide delivered-input
normalization could hide an ordinary dependency substitution. The current
candidate separates semantic coverage gaps from retained replay/audit loss
across domain, schema, producer, consumer, and receipt seams; requires exact
documentation alias version/hash/state with typed identity-drift failure; and
normalizes only the single delivered-root dependency common to every decision.
It also executes all four managed cases, validates immutable source-authority
Git blobs, reports 35 direct requirements, and passes the corrected
Comprehensive gate. A fresh whole-slice review of the exact committed candidate
is still required.

WP6 is not owner-accepted, the Slice 5 contracts are not frozen, and Slice 5 is
not complete. The only eligible handoff after a final `ACCEPT` review is owner
review of the acceptance packet in the current implementation record. M1/S6
may be planned only after that explicit owner decision; it is not eligible for
implementation from this status document.

The rejected preauthored 28-package comprehensive corpus is not a current input
and was not reconstructed. WP4 and WP5 each own the small semantic cases for
behavior they implement; WP6 owns the accumulated cross-package corpus and
closeout evidence now awaiting owner acceptance.

WP1's broad Slice 5 contracts remain `Implementation-active`, not
`Slice-frozen`, pending owner acceptance. WP2-WP6 may revise them when vertical
producer, consumer, persistence, invalid-state, and focused-fixture evidence
exposes a problem, provided every affected seam changes together. The contracts
become `Slice-frozen` only with accepted Slice 5 closeout.

Slice 5 uses the repository-wide development loop: implement a vertical
increment, test, review, correct, and re-review until accepted. Routine defects
do not consume a correction budget and do not block unrelated in-scope work.
Only the escalation conditions in the development execution policy require an
owner decision or safety stop.

## Status maintenance rule

Historical plans, ADR chronology, incident reports, occurrence ledgers, and
implementation records must not be scanned to infer the next package. They may
be consulted for task-specific evidence after this file, the active plan, and
the current implementation record establish the live handoff.
