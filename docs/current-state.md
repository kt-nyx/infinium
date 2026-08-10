# Current project state

Status: Accepted
Disposition: Current execution authority

Last reviewed: 2026-08-10
Owner: Project owner

This is the only document that states the live milestone, slice, work-package,
and evaluator handoff. Product documents and accepted ADRs define product
meaning; accepted plans define scope; records preserve implementation and
review history.

## Active handoff

| Field | Current value |
|---|---|
| Milestone | `M1` - active |
| Active slice | `M1/S5` - active and not owner-accepted |
| Current authorized work | None; the owner-approved final repository consolidation is complete and independently reviewed |
| Revised closeout candidate | Final cleanup implementation `5514919b8f742d00e59752fa7125da487a390926`, following public-fixture consolidation and protocol `/4` retirement; owner review remains pending |
| Next eligible action | Project-owner review and explicit Slice 5 acceptance or a bounded correction request |
| Later work | Slice 6 planning requires explicit Slice 5 owner acceptance; no successor implementation is authorized |
| Execution policy | [Repository execution policy](execution-policy.md) |
| Milestone plan | [M1 backend semantic proof](plans/milestones/m1/plan.md) |
| Slice entry | [Slice 5 current summary and navigation](plans/milestones/m1/slices/s5/current.md) |
| Public verification profile | [M1 continuation verification profile](evaluation/m1-continuation-verification-profile.md) |

## Current Slice 5 boundary

The accepted normalization amendment and owner-approved cleanup follow-up
completed functional renaming, fixture/tool relocation, documentation
consolidation, production and test-file decomposition, shared test support,
an exact public-fixture registry, and removal of proven temporary material.
Every affected producer, consumer, persistence seam, schema, fixture, test,
verification script, and current document was updated together. Public
semantic truth and claim boundaries did not change. Slice 5 contracts remain
`Implementation-active` until the project owner explicitly accepts and freezes
the revised closeout candidate.

The compact [Slice 5 entry](plans/milestones/m1/slices/s5/current.md) routes
scope changes to the full plan and chronology/evidence questions to the full
implementation record. Do not copy that chronology back into current
navigation documents.

## Evaluator status

- Private held-out evaluation is deferred and has no valid current product
  verdict.
- Protocol `/5` is retired unqualified and is not resumable.
- Protocol `/4` is retired under ADR-0033 and archived outside this repository.
  It has no current execution, testing, review, or authority role.
- Ordinary product work must not access the evaluator-private fixture
  repository or create, repair, retry, or replace private evaluator work.
- The compact chronology and exact Git-backed recovery map are in
  [Evaluator history](evaluation/evaluator-history.md).

## Maintenance rule

Update this file when the live handoff changes. Historical plans, ADR
chronology, incidents, attestations, occurrence ledgers, and implementation
records must never be scanned or amended to infer current status.
