# M1-to-M2 Foundation — Frontend Application Foundation

Status: Accepted
Disposition: WP9 integrated candidate passed; awaiting Checkpoint E architecture-steward and final owner acceptance; M2 inactive

Last reviewed: 2026-08-28
Owner: Project owner
Accepted: 2026-08-25
Plan ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`
Next gate: Checkpoint E architecture-steward and final owner acceptance after WP9

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
- [Integrated acceptance workflow](frontend-foundation-acceptance.v1.json)
- [Integrated acceptance workflow schema](frontend-foundation-acceptance.v1.schema.json)
- [Implementation record](implementation-record.md)
- [Frontend toolchain and generated-output ownership](frontend-toolchain-and-generation.md)
- [Sanitized desktop qualification receipt](desktop-qualification-receipt.v1.json)
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
| E — Integrated acceptance and handoff | WP9 | A fresh closeout orchestrator reviews the whole candidate, runs the final floor, and records the M2 planning handoff | M2-ready contract candidate awaiting Checkpoint E acceptance |

Each package retains an individual implementation and verification receipt.
The default orchestrator does not stop after each package. It continues through
its phase while predecessor gates pass, then stops at the phase checkpoint for
a consolidated review and handoff. Owner input is required only for a genuine
escalation defined by the plan.

## Current authority

The owner accepted the planning package and the corrected Phase A and Phase B
implementations. The architecture steward independently accepted corrected
WP5/WP6 and Checkpoint C at
`2ec3be78da4d05d8c6ada68a3e18544a446f2f03`; verification reported 774 passed,
10 expected skips, zero failures, all non-test gates passing, a clean worktree,
and zero repository-owned .NET test-process survivors. WP7's generated-consumer
cycle and WP8's executable desktop-consumer cycle, independent correction, and
re-review pass on the same Phase D candidate. The tracked qualification receipt
retains complete sanitized launch, bridge, process-split memory, message,
package, runtime, license, coverage, and exact zero-survivor evidence. After
the recorded first-floor defects were corrected on that same candidate, its
then-current complete floor passed. A subsequent changed-surface review
expanded inherited WebView2 override denial across eight environment variables
and seven HKLM/HKCU policy families, moved refusal before launch-option/window
construction, and added Stable-only runtime selection and revalidation before
creation and recovery. The corrected focused Release and live qualification
gates pass with zero owned survivors; independent architecture and evaluation
re-review found no remaining finding. The corrected bytes pass the complete
locked repository floor with 855 tests, 12 expected skips, zero failures, and
zero repository-owned test-process survivors.
The architecture steward accepted Checkpoint D at
`6b9b92a5f3dae0e90219f521919555956a8b5623`, whose parent is
`ed870882cf6887b05fe91641cb3118b5252ea5d6`. Phase E/WP9 now has a passing
integrated candidate at Checkpoint E. This status does not activate M2. WP3 and WP4 provide typed
tool/profile/configuration setup, honest local estimates, non-secret provider
status, prepared manual-run initiation, durable gesture/binding receipts, and
an offline retained-input native diagnostic proof through the real durable
analysis operation, including reconnect and restart. Phase C result/review
primitives are accepted. The independent report-query,
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
revalidates all retained authority before atomic start. WP5/WP6 and Checkpoint
C are accepted. Phase D maps none of the five targeted-verification RPCs, so
they remain native-only under ADR-0038.

The latest contract correction completes the accepted preparation readback:
generated native clients receive the canonical finding/case signature and
producer/semantic/identity versions, capture and acquisition lifecycle/fencing/
publication/provenance evidence, direct roots and dependency proofs, typed
correlation coverage, analyzer compatibility, reuse/recompute proofs, and work
limits through independent bounded pages. Readback is now revision-coherent:
one store-locked snapshot validates historical events, capture/publication
seals, acquisition checkpoints/provenance/application links, and the retained
payloads before returning any authoritative history. Actual capture and
semantic gaps have their own bounded page, while plan-wide limitation and
non-startable summaries remain truthful on every member page. Canonical typed identities are now
the only correlation authority; raw names, form keys, contribution strings,
and paths cannot match a stable identity, and slot similarity alone cannot
create changed-correlation authority. The preparation also owns the exact
resolved target manifest, delivered input, coverage input, and successor
identity later consumed by atomic start. Terminal-gap pages now use bounded
opaque cursors tied to the exact preparation/acquisition generation and gap
ordering. Lifecycle pages use a schema-17 append-only sealed cross-owner
sequence, preserving equal-timestamp causality and stable continuation across
later appends, rebuild, restart, and restore. Application `1.13.0`, domain
`1.6.0`, storage `1.16.0`, schema `17`, and persisted targeted-plan `1.2.0`
identify the corrected contract bytes.

Valid dependency-closed plans with ambiguous identity or missing required
proof now publish durably as inspectable, non-startable `Invalidated`
preparations rather than rolling back or being mislabeled as execution
failures. Their denominator, reasons, gaps, plan bytes, and lifecycle evidence
remain readable, while atomic start admits no run, command, operation input,
admission, or lineage mutation. Schema-16 acquisition history is also checked
against an exact closed event shape and the retained authority for each event
kind before schema 17 can seal it. Added, missing, or substituted fields fail
migration with the source fingerprint and append-only triggers intact and no
partial schema-17 objects.

M2 remains inactive until a separate accepted M2 plan exists. Phase E may
classify the integrated boundary only as an M2-ready contract candidate and
must stop at Checkpoint E for architecture-steward and final owner acceptance.
The packaged
React surface is diagnostic development evidence, not the M2 interface. It
consumes the real bootstrap, paginated findings, detail, progress, event stream,
host-attested cancellation, resync, reload, and coordinator-reconnect paths.
Targeted verification remains native-only and unmapped.

## WP9 integrated acceptance result

The strict [integrated acceptance workflow](frontend-foundation-acceptance.v1.json)
maps each of the required 16 actions to its real native or diagnostic-desktop
consumer, exact evidence paths, maturity, evaluation cases, and fail-closed
boundary. `eng/verify-frontend-foundation.ps1` validated that manifest, ran 3
contract/authority tests and 8 native integration tests, then ran the complete
offline desktop qualification. All 11 focused tests passed with no skips or
failures. The desktop batches passed 31 ordinary desktop tests, one populated-
state preparation test, one live WebView2/accessibility test, and one lifecycle
test. Every .NET batch and the final desktop cleanup reported zero repository-
owned survivors. The fresh post-correction complete repository floor also
passed: 856 tests, 12 expected skips, zero failures, zero build warnings or
errors, every documentation, functional-naming, dependency, formatting, and
diff gate, and zero repository-owned process survivors.

The first receipt attempt exposed one harness-only defect: Windows PowerShell
returned an ordered dictionary without the property adapter expected by the
counter aggregation. The runner was corrected to emit a property-bearing
object, the complete integrated workflow was rerun, and the fresh rerun passed.
No product contract, architecture boundary, renderer operation, or product
meaning changed during that correction.

## Contract maturity at Checkpoint E

The complete boundary is an **M2-ready contract candidate**. In plain language,
the backend, generated clients, and diagnostic desktop now work together well
enough to be a trustworthy input to a separately planned M2. This is not a
claim that the interface is polished, production-ready, or Milestone-stable.

| Maturity | Surfaces | Meaning for the next plan |
|---|---|---|
| Producer-consumer-validated | Version/fingerprint negotiation; bounded bootstrap; typed setup, tool, profile, saved configuration, estimate, and prepared-run flow; durable run admission; progress/events/cancellation/reconnect/restart; canonical persistence/readback; generated native client; nine-operation generated renderer client; controlled desktop origin, bridge, accessibility mechanics, and lifecycle; native-only targeted-verification preparation/start/readback; structured export lifecycle | M2 planning may consume these exact boundaries without inventing replacements, while preserving their limits and authority rules. |
| Implementation-active until the real M2 interface consumes them | Full summary/readiness and scope-limited presentation; supported-case and lead-only queue presentation; complete finding/case/report/evidence/provenance/focused-mod presentation; review/disposition/annotation and assumption controls; export interaction; any future user-facing targeted-verification interaction | The contracts and native diagnostics pass, but the real M2 presentation seam must validate meaning, empty/error/loading states, accessibility, bounds, and authority before these surfaces can be treated as stable. The five targeted-verification RPCs remain native-only unless separately authorized architecture creates a narrow interface. |
| Deliberately unavailable | Six provider RPCs, native credential-entry choreography, generic path/command/URL/provider/credential/native proxy authority, polished installation/distribution, and independent semantic qualification | M2 must not infer or silently add these capabilities. Any product expansion needs its own accepted authority. |

## Measurements and M2 planning recommendations

The fresh Phase E repeat on the recorded Windows reference machine observed a
1,194 ms median and 1,372 ms maximum browser-ready launch; 1,540.3463 ms bootstrap;
approximately 62–63 ms median finding-page, detail, progress, resync, and
second-page bridge operations; 93.6069 ms median renderer reload; 355,483,648
bytes median idle and 411,131,904 bytes median active private working set; a
6,615,820-byte 31-file desktop package; and zero launched-process survivors.
The exact repeat is retained in
[the desktop qualification receipt](desktop-qualification-receipt.v1.json).

These are development-machine observations, not production guarantees. A
future accepted M2 plan should:

- begin with the observed launch, bridge, page, message, memory, package, and
  100,000-summary virtualization evidence as measurement baselines, then set
  acceptance thresholds only after representative hardware and workload runs;
- keep the existing 100-item page, 500-summary cache, 13 mounted-row,
  1 MiB-message, 256 KiB-chunk, and 64-item stream limits unless evidence and
  accepted architecture justify a change;
- design complete loading, empty, partial, unavailable, stale, conflict,
  cancellation, reconnect, and scope-limited states before adding polish;
- preserve coordinator-owned truth, immutable analysis, append-only user state,
  exact lineage, local-private export, and no provider or credential fallback;
- decide the narrow user-facing interaction model for results, review,
  assumptions, export, and targeted verification without exposing generic
  renderer authority; and
- plan representative assistive-technology and hardware qualification,
  installer/distribution/runtime servicing, and credential enrollment as
  explicit work rather than treating diagnostic evidence as product closure.

Remaining owner decisions are the M2 scope and accepted plan, the polished
interaction model, any separately authorized targeted-verification exposure,
credential enrollment, distribution/runtime servicing, and the breadth of
hardware and assistive-technology qualification. None is decided here. M2
remains inactive.
