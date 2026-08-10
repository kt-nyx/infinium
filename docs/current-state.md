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
| Active slice | `M1/S6` - owner-accepted; implementation-active |
| Current authorized work | `M1/S6/WP1` only: contracts, codecs, migration, finite-bound policy, answer-free examples, public authority updates, focused verification, review, correction, and re-review under the accepted Slice 6 plan |
| Accepted Slice 5 candidate | Final cleanup implementation `5514919b8f742d00e59752fa7125da487a390926`, following public-fixture consolidation and protocol `/4` retirement |
| Accepted Slice 6 plan | Explicit stateless/cache-off ADR-0025 conformance closure; no separate ADR; eleven packages with distinct native/live authorization gates |
| Next eligible action | Implement and independently accept `M1/S6/WP1` from the exact accepted plan; create the Slice 6 implementation record before product edits |
| Later work | The orchestrator may advance WP1-WP3 and WP5-WP8 only through accepted prerequisite/package gates while updating this handoff. WP4 and WP9-WP11 always require their exact fresh owner authorization; no Credential Manager or provider request is authorized now |
| Execution policy | [Repository execution policy](execution-policy.md) |
| Milestone plan | [M1 backend semantic proof](plans/milestones/m1/plan.md) |
| Slice entry | [Slice 6 current summary and navigation](plans/milestones/m1/slices/s6/README.md) |
| Public verification profile | [M1 continuation verification profile](evaluation/m1-continuation-verification-profile.md) |

## Accepted Slice 6 authority

On 2026-08-10 the project owner accepted the independently reviewed Slice 6
plan and accepted explicit `reasoning.context: "current_turn"`, standard
reasoning mode, and explicit prompt-cache mode with no cache breakpoint/key as
ADR-0025 conformance closure. No separate ADR is required.

This handoff opens only `M1/S6/WP1`. Plan acceptance permits automatic
progression among the named non-live packages only after each prerequisite
package is independently accepted and this file is advanced to the exact next
package. It does not authorize the WP4 disposable native Credential Manager
gate, production-profile enrollment/verification, or any WP9-WP11 provider
request. Those effects remain closed pending their own exact owner-approved
manifests.

## Completed Slice 5 boundary

The accepted normalization amendment and owner-approved cleanup follow-up
completed functional renaming, fixture/tool relocation, documentation
consolidation, production and test-file decomposition, shared test support,
an exact public-fixture registry, and removal of proven temporary material.
Every affected producer, consumer, persistence seam, schema, fixture, test,
verification script, and current document was updated together. Public
semantic truth and claim boundaries did not change. On 2026-08-10 the project
owner accepted the revised closeout candidate, marked `M1/S5` complete, and
advanced its contracts from `Implementation-active` to `Slice-frozen`.

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
