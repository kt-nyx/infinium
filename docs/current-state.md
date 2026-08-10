# Current project state

Status: Current execution authority

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
| Current authorized work | [Pre-closeout repository normalization](plans/milestones/m1/slices/s5/repository-normalization.md) |
| Prior closeout candidate | WP1-WP6 implementation and review completed at `d47e4290a95cd86cbcf210374cd76788902cc7fb`; terminal review returned `ACCEPT`, but that candidate was not owner-accepted and is superseded only as the exact closeout candidate by the normalization amendment |
| Next eligible action | Complete normalization, rerun the full Slice 5 and protocol `/4` public verification floors, obtain fresh terminal review, and present a revised owner-acceptance packet |
| Later work | Slice 6 planning requires explicit Slice 5 owner acceptance; no successor implementation is authorized |
| Execution policy | [Repository execution policy](execution-policy.md) |
| Milestone plan | [M1 backend semantic proof](plans/milestones/m1/plan.md) |
| Slice plan | [Evidence-to-analysis pipeline](plans/milestones/m1/slices/s5/plan.md) |
| Implementation record | [Slice 5 record](plans/milestones/m1/slices/s5/record.md) |
| Public verification profile | [M1 continuation verification profile](evaluation/m1-continuation-verification-profile.md) |

## Current Slice 5 boundary

The accepted normalization amendment permits functional renaming,
fixture/tool relocation, documentation consolidation, and removal of proven
temporary material before closeout. Slice 5 contracts remain
`Implementation-active`; normalization must update every affected producer,
consumer, persistence seam, schema, fixture, test, and document together.
Existing semantic truth and claim boundaries do not change merely because a
path or development-era identity changes.

The complete package chronology, correction cycles, retained evidence,
coverage limits, and owner-acceptance proposal live in the
[Slice 5 implementation record](plans/milestones/m1/slices/s5/record.md).
Do not copy that chronology back into current navigation documents.

## Evaluator status

- Private held-out evaluation is deferred and has no valid current product
  verdict.
- Protocol `/5` is retired unqualified and is not resumable.
- Protocol `/4` is immutable historical evidence with bounded public
  regression use only. It is not a product, Slice 5, M1, held-out,
  reliability, readiness, or acceptance verdict.
- Ordinary product work must not access the evaluator-private fixture
  repository or create, repair, retry, or replace private evaluator work.
- The compact chronology and exact Git-backed recovery map are in
  [Evaluator history](evaluation/evaluator-history.md).

## Maintenance rule

Update this file when the live handoff changes. Historical plans, ADR
chronology, incidents, attestations, occurrence ledgers, and implementation
records must never be scanned or amended to infer current status.
