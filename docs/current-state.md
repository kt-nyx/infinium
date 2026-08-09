# Current project state

Status: Current execution authority

Last reviewed: 2026-08-09

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
| Completed package | `M1/S5/WP1`, `M1/S5/WP2`, and `M1/S5/WP3` |
| Next eligible package | `M1/S5/WP4` — findings, cases, and coverage publication |
| Later packages | `WP5` and `WP6`, dependency-gated by the active Slice 5 plan |
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
gates pass. WP3 is complete and WP4 is now eligible. The rejected preauthored
28-package comprehensive corpus is not a current input and must not be
reconstructed. WP4 and WP5 each own the small semantic cases for behavior they
implement; WP6 owns the accumulated cross-package corpus and closeout.

WP1's broad Slice 5 contracts are `Implementation-active`, not
`Slice-frozen`. WP2-WP5 may revise them when vertical producer, consumer,
persistence, invalid-state, and focused-fixture evidence exposes a problem,
provided every affected seam changes together. The contracts become
`Slice-frozen` only with accepted Slice 5 closeout.

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
