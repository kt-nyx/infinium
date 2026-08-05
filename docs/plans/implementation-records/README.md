# Implementation records

Status: Active
Last reviewed: 2026-08-05

These records retain implementation and verification evidence for completed
or formally reviewed slices under accepted milestone plans, including explicit
blockers. They do not modify the authority of the plans or mark unexercised
evaluation cases as passed.

Slices 0 through 3.5 are complete. The initial Slice 4 attempt stopped at a
Mutagen/EVAL-0052 fixture-conformance defect, and no partial production
implementation was retained. The project owner subsequently selected Option A:
the affected public and evaluator-private fixtures were corrected and
independently resealed while preserving pinned Mutagen `0.54.2` and ADR-0009.
That prerequisite blocker was cleared, and the unchanged Slice 4 product
candidate at `98fe8a5` retains that historical verification. It does not yet
conform to the later owner-accepted ADR-0028 contract. Evaluator-v1 produced no
valid held-out product verdict and is retired. The historical evaluator-v2
`/2` Stage C
invocation ran once, but Stage C.5 invalidated its product verdict; no valid
successor held-out verdict exists. Slice 4.5 now owns evaluator-v2 qualification
and held-out acceptance. Final protocol `/4` is qualified and frozen at
`3693d19563c636cd2879804633ca4ce52448d2c1`; the B2 input bytes already exist
and passed independent byte review, but B2 oracle qualification under `/4` has
not run. ADR-0028 resolves the six semantic questions; public implementation,
requalification, and a newly frozen candidate are required before B2. C2 has
not run, Stage D has not started, and Slice 5 remains blocked.

- [M1 Slice 0 — Toolchain, licensing posture, and dependency lock](M1-slice-0.md)
- [M1 Slice 1 — Versioned domain, wire, output, and evaluation contracts](M1-slice-1.md)
- [M1 Slice 2 — Local execution substrate, persistence, and platform boundaries](M1-slice-2.md)
- [M1 Slice 3 — Supported-target admission and MO2 snapshot reconstruction](M1-slice-3.md)
- [M1 Slice 3.5 — Bethesda binary fixture qualification](M1-slice-3.5.md)
- [M1 Slice 4 — Bethesda semantic extraction and typed indexes](M1-slice-4.md)
- [M1 Slice 4.5 — Held-out evaluation v2](M1-slice-4.5.md)
  — final bounded public Stage A freeze at
  `3693d19563c636cd2879804633ca4ce52448d2c1`; owner semantic disposition
  accepted; public realignment and later stages pending.
