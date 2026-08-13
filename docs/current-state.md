# Current project state

Status: Accepted
Disposition: Current execution authority

Last reviewed: 2026-08-12
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
| Current authorized work | `M1/S6/WP7` only: candidate investigation and retained provenance/replay over deterministic public inputs and retained provider transcripts under the accepted Slice 6 plan |
| Accepted Slice 5 candidate | Final cleanup implementation `5514919b8f742d00e59752fa7125da487a390926`, following public-fixture consolidation and protocol `/4` retirement |
| Accepted Slice 6 WP1 candidate | `61b90314d8273749849f590b303814008fa2fdfa`; nine Slice 6 contracts are `Implementation-active` and the accepted local input-bound policy is `openai-responses-o200k-byte-envelope/v1` |
| Accepted `M1/S6/WP2` candidate | `ed27ed04897103d93a60e6200971ca12d04f2e11`; capability, price, atomic reservation/final-gate, settlement, projection, replay, simulator, and public fixture/oracle evidence are independently accepted |
| Accepted `M1/S6/WP3` candidate | `b32939e8b7491a5c47453f912d25dd98c090f103`; one-shot helper process isolation, strict protocol, synthetic credential lifecycle, recovery, staging/admission, exact SDK `10.0.303`, and the integration synchronization barrier are independently accepted |
| Accepted `M1/S6/WP5` candidate | `fd3c80d91dd247e65b5130309a9b5bb19dd1381f`, with append-only evidence `11e60445b6d5f1d3efc5b607f080dd986afb4ed4`; exact Responses serialization/codec/transport, deterministic loopback/offline replay, bounded secret-safe receipts, persistence/output/replay, and public WP5 evidence are independently accepted |
| Accepted `M1/S6/WP6` candidate | Product `ee0b6d31f1c1826c2af7634766155397e916c3e1`, append-only evidence `2b277338390f7dac37b5a5436bbe2cd81dedc871`, and answer-isolated oracle `37aa2b4e2fc084307ba5211f21bbeeb7a93efab0`; source-claim acquisition, deterministic admission, retained semantic provenance/replay, and later admitted-artifact consumption are independently accepted |
| Accepted Slice 6 plan | Explicit stateless/cache-off ADR-0025 conformance closure; no separate ADR; eleven packages with distinct native/live authorization gates |
| Next eligible action | Implement and independently accept `M1/S6/WP7` from the exact accepted plan using deterministic public inputs and retained provider transcripts only; WP4 remains closed/stale, WP8 is not authorized, and Credential Manager, API key, network, provider, source-refresh, private-fixture, and later-package effects remain prohibited |
| Later work | WP8 may be authorized only after WP7 is independently accepted and this handoff is advanced. WP4 remains closed/stale, and WP9-WP11 always require their exact fresh owner authorization; no Credential Manager or provider request is authorized now |
| Execution policy | [Repository execution policy](execution-policy.md) |
| Milestone plan | [M1 backend semantic proof](plans/milestones/m1/plan.md) |
| Slice entry | [Slice 6 current summary and navigation](plans/milestones/m1/slices/s6/README.md) |
| Public verification profile | [M1 continuation verification profile](evaluation/m1-continuation-verification-profile.md) |

## Accepted Slice 6 authority

On 2026-08-10 the project owner accepted the independently reviewed Slice 6
plan and accepted explicit `reasoning.context: "current_turn"`, standard
reasoning mode, and explicit prompt-cache mode with no cache breakpoint/key as
ADR-0025 conformance closure. No separate ADR is required.

WP1 is accepted at exact candidate
`61b90314d8273749849f590b303814008fa2fdfa`, WP2 is independently accepted
at exact candidate `ed27ed04897103d93a60e6200971ca12d04f2e11`, and WP3 is
independently accepted at exact candidate
`b32939e8b7491a5c47453f912d25dd98c090f103`. WP5 is independently accepted
at exact candidate `fd3c80d91dd247e65b5130309a9b5bb19dd1381f`, with append-only
evidence `11e60445b6d5f1d3efc5b607f080dd986afb4ed4`. WP6 is independently
accepted at exact product candidate
`ee0b6d31f1c1826c2af7634766155397e916c3e1`, with append-only evidence
`2b277338390f7dac37b5a5436bbe2cd81dedc871` and answer-isolated oracle
`37aa2b4e2fc084307ba5211f21bbeeb7a93efab0`. The nine Slice 6 contracts
remain `Implementation-active`, while Slice 5 v1 remains `Slice-frozen`.
This handoff opens only `M1/S6/WP7`; WP4 remains closed and its prior
authorization is stale/non-authoritative for any new native effect, and WP8
is not authorized. Plan acceptance permits automatic progression among the
named non-live packages only after each prerequisite package is independently
accepted and this file is advanced to the exact next package. It does not
authorize the WP4 disposable native
Credential Manager gate, production-profile enrollment/verification, or any
WP9-WP11 provider request. Those effects remain closed pending their own exact
owner-approved manifests.

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
