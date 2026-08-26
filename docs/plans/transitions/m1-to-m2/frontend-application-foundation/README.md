# M1-to-M2 Foundation — Frontend Application Foundation

Status: Accepted
Disposition: Checkpoint C under correction; Phase D blocked and not started

Last reviewed: 2026-08-26
Owner: Project owner
Accepted: 2026-08-25
Plan ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`
Next work package: corrected `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP5-WP6`

## Plain-language purpose

M1 built Infinium's analysis engine, durable storage, command-line client, and
one narrow but real analytical proof. M2 needs a graphical, finding-centered
workflow. Between those two sits a missing application layer: the safe set of
queries, user actions, live updates, and prepared result views that a frontend
can actually use.

This transition builds and qualifies that layer. It also creates a minimal
diagnostic WPF/WebView2/React consumer to prove the contract and desktop
security boundary before M2 invests in polished interface work.

The transition does not create an M1.5 milestone, widen analyzer coverage, or
deliver the M2 product interface. Completion means M2 can be planned and
implemented against a producer-consumer-validated application boundary.

## Accepted planning set

- [Full implementation plan](plan.md)
- [Frontend capability matrix](frontend-capability-matrix.v1.json)
- [Capability-matrix schema](frontend-capability-matrix.v1.schema.json)
- [Application contract inventory](application-contract-inventory.v1.json)
- [Application contract inventory schema](application-contract-inventory.v1.schema.json)
- [Implementation record](implementation-record.md)
- [Gap investigation](../../../../research/investigations/RESEARCH-0057-frontend-application-foundation-gap.md)
- [ADR-0037](../../../../architecture/decisions/ADR-0037-frontend-application-contract-and-desktop-bridge.md)
- [Product-conformance verification profile](../../../../evaluation/product-conformance-verification-profile.md)

## Phase map

| Phase | Packages | One-orchestrator boundary | Checkpoint result |
|---|---|---|---|
| A — Authority and contract foundation | WP1-WP2 | One orchestrator audits the exact gap, binds the accepted contract families, and prepares generated-client inputs | Contract baseline accepted for implementation; no product UI |
| B — Setup and execution workflow | WP3-WP4 | One orchestrator implements setup/configuration and typed run/live-state paths together | A diagnostic native client can prepare, start, observe, and reconnect to a run |
| C — Results and review workflow | WP5-WP6 | One orchestrator implements result exploration and durable user review actions together | Complete finding-to-review backend path is producer-consumer-validated |
| D — Desktop consumption proof | WP7-WP8 | One orchestrator builds the generated TypeScript/native boundary and executable host/renderer qualification | WebView2 stack either qualifies for M2 planning or the stack decision is reopened |
| E — Integrated acceptance and handoff | WP9 | A fresh closeout orchestrator reviews the whole candidate, runs the final floor, and records the M2 planning handoff | Accepted M2-ready application-contract candidate |

Each package retains an individual implementation and verification receipt.
The default orchestrator does not stop after each package. It continues through
its phase while predecessor gates pass, then stops at the phase checkpoint for
a consolidated review and handoff. Owner input is required only for a genuine
escalation defined by the plan.

## Current authority

The owner accepted the planning package, corrected Phase A implementation, and
the corrected Phase B implementation. WP3 and WP4 provide typed
tool/profile/configuration setup, honest local estimates, non-secret provider
status, prepared manual-run initiation, durable gesture/binding receipts, and
an offline retained-input native diagnostic proof through the real durable
analysis operation, including reconnect and restart. Phase C result/review
primitives are present but under correction. The independent report-query,
recursive request-validation, export-deletion/recovery, provenance, and cursor
defects now have focused offline producer-consumer evidence. Populated
pre-publication stores report an explicit unavailable projection gap, and the
declared targeted-verification RPC validates requests then returns typed
`Unsupported` without durable mutation. Targeted verification remains blocked
on an accepted changed-snapshot and exact-scope architecture decision, so
WP5/WP6 are not accepted, the earlier Checkpoint C
receipt remains suspended, and Phase D has not started.

M2 remains inactive until a separate accepted M2 plan exists. The transition's
diagnostic React surface is development evidence, not the M2 interface.
