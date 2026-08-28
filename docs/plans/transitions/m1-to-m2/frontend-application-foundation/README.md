# M1-to-M2 Foundation — Frontend Application Foundation

Status: Accepted
Disposition: Checkpoint E accepted; foundation complete; M2 inactive

Last reviewed: 2026-08-28
Owner: Project owner
Accepted: 2026-08-25
Plan ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION`
Accepted implementation: `2538f64dda36179c27aad51fbfe6d18ba57ccfd3`
Next gate: separately accepted M2 plan and activation

## Plain-language result

M1 built Infinium's analysis engine, durable storage, command-line client, and
one narrow analytical proof. This transition supplied the safe application
layer a future interface needs: typed queries, user actions, live updates,
prepared result views, and a closed desktop bridge.

All five phases and nine work packages are complete. Generated native and
TypeScript clients plus a minimal WPF/WebView2/React diagnostic desktop prove
the application contract from producer to consumer. The diagnostic desktop is
not the M2 product interface; it exists to prove the contract, resource limits,
recovery behavior, accessibility mechanics, and security boundary before M2
builds polished workflows.

Checkpoint E is accepted at
`2538f64dda36179c27aad51fbfe6d18ba57ccfd3` (tree
`93070325b228ebba47fae420ad5bd480484855c7`). M2 is ready to be planned, but
remains inactive until a separate plan is accepted and explicitly activated.

## Accepted authority set

- [Full implementation plan](plan.md)
- [Frontend capability matrix](frontend-capability-matrix.v1.json)
- [Application contract inventory](application-contract-inventory.v1.json)
- [Integrated acceptance workflow](frontend-foundation-acceptance.v1.json)
- [Implementation record](implementation-record.md)
- [Frontend toolchain and generated-output ownership](frontend-toolchain-and-generation.md)
- [Sanitized desktop qualification receipt](desktop-qualification-receipt.v1.json)
- [Accepted WP6 targeted-verification addendum](wp6-targeted-verification-addendum.md)
- [Frontend gap investigation](../../../../research/investigations/RESEARCH-0057-frontend-application-foundation-gap.md)
- [Targeted-verification architecture investigation](../../../../research/investigations/RESEARCH-0058-targeted-verification-executable-architecture.md)
- [Frontend application contract and desktop bridge ADR](../../../../architecture/decisions/ADR-0037-frontend-application-contract-and-desktop-bridge.md)
- [Targeted-verification preparation and execution ADR](../../../../architecture/decisions/ADR-0038-targeted-verification-preparation-and-execution.md)
- [Product-conformance verification profile](../../../../evaluation/product-conformance-verification-profile.md)

The linked schemas remain authoritative for the matrix, inventory, and
acceptance workflow. Detailed implementation and correction chronology belongs
in the implementation record rather than this navigation page.

## Phase results

| Phase | Packages | Accepted result |
|---|---|---|
| A — Authority and contract foundation | WP1-WP2 | Closed application authority, capability ownership, generated-client inputs, and fail-closed contract baseline. |
| B — Setup and execution workflow | WP3-WP4 | Typed setup, configuration, estimation, run admission, progress, cancellation, reconnect, and restart through the native client. |
| C — Results and review workflow | WP5-WP6 | Bounded result exploration, immutable analysis readback, append-only review state, local-private export, and native-only targeted verification. |
| D — Desktop consumption proof | WP7-WP8 | Generated TypeScript client and a protected diagnostic desktop with exact origin, session, message, resource, lifecycle, accessibility, and recovery evidence. |
| E — Integrated acceptance and handoff | WP9 | All 16 workflow steps verified against the exact committed candidate; Checkpoint E accepted. |

Checkpoint C was accepted at
`2ec3be78da4d05d8c6ada68a3e18544a446f2f03`. Checkpoint D was accepted at
`6b9b92a5f3dae0e90219f521919555956a8b5623`. Checkpoint E and the complete
foundation were accepted at
`2538f64dda36179c27aad51fbfe6d18ba57ccfd3`.

## Acceptance evidence

Post-commit acceptance run `961a1191be884e44b939380f81c31eca` verified a
clean worktree and the exact accepted commit/tree before and after evidence
production. It derived every workflow-step result from closed typed proofs and
reported:

- all 16 integrated workflow steps passed;
- 4 contract/authority and 8 native integration tests passed;
- 31 ordinary desktop, 1 populated-state preparation, 1 live desktop, and
  1 lifecycle test passed;
- every recorded repository-owned test and desktop process survivor count was
  zero; and
- the complete repository floor passed 856 tests, skipped 12 expected
  environment-dependent tests, and failed none.

The acceptance summary SHA-256 is
`48b3ad58343121dd2ed3051e36321f0c9d6bbde3dce770cc2e7ad98331523f1a`; its
desktop summary SHA-256 is
`0dffce9f23a418f975fae341a1fa31c6eb344e0a39b4996d87465ab001e1994a`.
These ignored run artifacts are disposable evidence outputs; the durable
acceptance decision and exact evidence identities are retained in the
implementation record.

## Contract maturity

| Maturity | Surfaces | Meaning for M2 |
|---|---|---|
| Producer-consumer-validated | Version/fingerprint negotiation; bounded bootstrap; typed setup, tools, profiles, saved configuration, estimates, and prepared runs; durable admission; progress, events, cancellation, reconnect, and restart; canonical persistence/readback; generated native and renderer clients; controlled desktop bridge; native-only targeted verification; structured-export lifecycle. | M2 may plan against these exact boundaries without inventing replacements or weakening their limits. |
| Implementation-active | Summary/readiness presentation; supported-case and lead-only queues; finding/case/report/evidence/provenance presentation; review and assumption controls; export interaction; any future user-facing targeted-verification interaction. | The real M2 interface must validate meaning, empty/error/loading states, accessibility, bounds, and authority before these presentation seams become stable. |
| Deliberately unavailable | Six provider RPCs; native credential-entry choreography; generic path, command, URL, provider, credential, filesystem, or native-proxy authority; polished installation/distribution; independent semantic qualification. | M2 must not infer or silently add these capabilities. Expansion requires separately accepted authority. |

The renderer remains limited to nine operations and sixteen exact message
shapes. The five targeted-verification RPCs remain native-only and cannot be
renderer-mapped without separately accepted architecture.

## Measurement boundary

The diagnostic runs retain launch, bridge, paging, message-size, memory,
package-size, runtime, and cleanup observations for M2 planning. They are
development-machine measurements, not production guarantees or service-level
objectives. A future M2 plan must establish representative workloads and
hardware before setting acceptance thresholds.

Existing hard bounds remain planning inputs: 100 summaries per page, 500 cached
summaries, 13 mounted rows in the diagnostic virtualization proof, a 1 MiB
message maximum, 256 KiB chunks, and a 64-item stream queue.

## Boundaries that remain closed

This transition does not:

- activate M2 or authorize M2 implementation;
- widen analyzer scope or claim whole-modlist safety;
- authorize live or billable provider activity;
- expose product credentials to the renderer;
- grant generic filesystem, database, command, URL, provider, or coordinator
  proxy authority;
- access private evaluator material or revive retired evaluator protocols;
- qualify an independent semantic oracle; or
- claim production readiness, polished installation, or broad real-world
  accuracy.

Any proposal that changes those boundaries requires the appropriate research,
ADR, or accepted plan revision before implementation.
