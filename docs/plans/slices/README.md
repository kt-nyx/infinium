# Slice execution plans

Status: Active
Last reviewed: 2026-08-05

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
  verdict exists. The unchanged Slice 4 candidate retains its historical
  public verification but does not yet conform to ADR-0028.
  Final protocol `/4` is qualified and frozen at
  `3693d19563c636cd2879804633ca4ce52448d2c1`. The B2 input bytes already exist
  and passed independent byte review, but B2 oracle qualification under `/4`
  has not run. The six semantic questions are resolved; public implementation,
  requalification, and a new candidate freeze are required before B2. C2 has
  not run, Stage D has not started, and Slice 5 remains blocked.

Completed slices retain their exact implementation and verification evidence
under [`../implementation-records/`](../implementation-records/README.md).
