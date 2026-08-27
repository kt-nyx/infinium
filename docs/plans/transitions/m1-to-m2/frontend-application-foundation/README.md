# M1-to-M2 Foundation — Frontend Application Foundation

Status: Accepted
Disposition: Checkpoint C under correction; Phase D blocked and not started

Last reviewed: 2026-08-27
Owner: Project owner
Accepted: 2026-08-25
Plan ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`
Next gate: corrected Checkpoint C architecture-steward review

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
- [Accepted WP6 targeted-verification addendum](wp6-targeted-verification-addendum.md)
- [Gap investigation](../../../../research/investigations/RESEARCH-0057-frontend-application-foundation-gap.md)
- [Targeted-verification architecture investigation](../../../../research/investigations/RESEARCH-0058-targeted-verification-executable-architecture.md)
- [ADR-0037](../../../../architecture/decisions/ADR-0037-frontend-application-contract-and-desktop-bridge.md)
- [ADR-0038](../../../../architecture/decisions/ADR-0038-targeted-verification-preparation-and-execution.md)
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
pre-publication stores report an explicit unavailable projection gap. The
project owner accepted
RESEARCH-0058, ADR-0038, and the WP6 addendum from architecture source
`bd936a02562a8df1ddcb62f275cc45b6c225e594`. Their changed-snapshot,
separately owned semantic acquisition, correlation-complete scope, and
`managed-analysis-v1` mapping now have a corrected implementation candidate:
five native-only generated-client RPCs, durable preparation and evidence
acquisition, dependency-complete scope/correlation, atomic successor admission,
and immutable lineage/readback. The corrected candidate additionally proves an
unseeded native preparation-to-completed-successor path through the actual
snapshot and semantic producers, makes qualified limited processing gaps
reachable in production planning, enforces cross-family one-shot gestures, and
revalidates all retained authority before atomic start. WP5/WP6 are not accepted, the earlier
Checkpoint C receipt remains suspended pending architecture-steward review,
and Phase D has not started.

The latest contract correction completes the accepted preparation readback:
generated native clients receive the canonical finding/case signature and
producer/semantic/identity versions, capture and acquisition lifecycle/fencing/
publication/provenance evidence, direct roots and dependency proofs, typed
correlation coverage, analyzer compatibility, reuse/recompute proofs, and work
limits through independent bounded pages. Canonical typed identities are now
the only correlation authority; raw names, form keys, contribution strings,
and paths cannot match a stable identity, and slot similarity alone cannot
create changed-correlation authority. Application `1.11.0`, domain `1.5.0`,
and persisted targeted-plan `1.1.0` identify the corrected contract bytes.

M2 remains inactive until a separate accepted M2 plan exists. The transition's
diagnostic React surface is development evidence, not the M2 interface.
