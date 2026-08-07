# Slice execution plans

Status: Active
Last reviewed: 2026-08-07

Slice execution plans refine an accepted milestone slice into a bounded,
fresh-agent implementation contract. They do not supersede the milestone plan,
accepted product requirements, ADRs, evaluation specifications, or fixture
manifests. If an execution plan conflicts with one of those authorities, the
higher-authority artifact controls and the discrepancy blocks implementation
until it is reconciled.

Current execution plans:

- [M1 Slice 3.5 — Independent Bethesda fixture and oracle qualification](M1-slice-3.5-bethesda-fixture-qualification.md)
  — Accepted 2026-07-30, amended and completed 2026-08-01; the pre-Slice-4
  readiness audit found no remaining start blocker; Slice 4 was implemented on
  2026-08-01.
- [M1 Slice 4.5 — Held-out evaluation v2](M1-slice-4.5-held-out-evaluation-v2.md)
  — Accepted 2026-08-04. The historical `/2` Stage C invocation ran once, but
  Stage C.5 invalidated its product verdict; no valid successor held-out
  verdict exists. The original Slice 4 candidate retains its historical public
  verification.
  Final protocol `/4` is qualified and frozen at
  `3693d19563c636cd2879804633ca4ce52448d2c1`. The B2 input bytes already exist
  and passed independent byte review. The single authorized B2 resume ran once
  and terminated on another public lexical-authority gap without an oracle,
  candidate execution, scoring, or product verdict. Public product realignment
  and candidate freeze remain complete at
  `a98d648bd0adb2751ee0c09828e0227b1583950f`. The public oracle-contract
  authorability attempt stopped after its one correction pass found a second
  material authority gap. The later accepted totality plan completed through
  WP5 and classified an evaluator `/4` representation gap. ADR-0030 supplies
  the owner disposition through the separate public `/5` successor plan.
  C2 has not run, Stage D has not started, and Slice 5 remains blocked.
- [M1 Slice 4.5 — Public Bethesda semantic realignment and candidate freeze](M1-slice-4.5-public-product-realignment.md)
  — Accepted and completed 2026-08-05 at `a98d648`. This bounded public
  implementation contract records the
  frozen-compatible per-chain FaceGen subject representation, keeps missing
  loose assets unknown under current structural assurance, and defers
  exhaustive byte-verified loose-file absence authority to M3 planning.
- [M1 Slice 4.5 — Protocol `/4` oracle-contract completion and held-out disposition](M1-slice-4.5-protocol-4-oracle-contract-completion.md)
  — Accepted 2026-08-05; implementation hard-stopped at the product-blind
  authorability gate. The single authorized
  private B2 resume terminated without an oracle or product verdict after
  finding another public lexical-authority gap. The public attempt made one
  permitted correction, but re-review found a second material cross-family
  authority gap. Frozen candidate/evaluator conformance was not inspected or
  classified. The project-owner milestone-plan disposition is supplied by the
  next plan.
- [M1 Slice 4.5 — Pre-B2 evidence-contract totality closure](M1-slice-4.5-pre-B2-evidence-contract-totality.md)
  — Accepted 2026-08-05 as work ID `M1/S4.5/PRE-B2`. ADR-0029 resolves the
  partial `RACE/DATA` disposition and requires a total state-to-fact model,
  executable completeness gate, model-derived public exercises, fresh
  product-blind review, and frozen candidate classification. WP1-WP5 completed;
  WP5 classified an evaluator `/4` representation gap.
- [M1 Slice 4.5 — Protocol `/5` successor realignment](M1-slice-4.5-protocol-5-successor-realignment.md)
  — Accepted 2026-08-07 as work `M1/S4.5/PRE-B2/V5`. ADR-0030 authorizes one
  separately qualified public `/5` successor after WP5 proved a frozen `/4`
  representation gap. WP0 completed. WP1 hard-stopped on an independently
  reproduced accepted-model FaceGen/coverage composition contradiction that
  requires owner semantic disposition; WP2-WP4 did not start. Candidate work,
  private access, B2, C2, Stage D, and Slice 5 remain excluded.

Completed slices retain their exact implementation and verification evidence
under [`../implementation-records/`](../implementation-records/README.md).
