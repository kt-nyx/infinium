# Implementation records

Status: Active
Last reviewed: 2026-08-04

These records retain implementation and verification evidence for completed
or formally reviewed slices under accepted milestone plans, including explicit
blockers. They do not modify the authority of the plans or mark unexercised
evaluation cases as passed.

Slices 0 through 3.5 are complete. The initial Slice 4 attempt stopped at a
Mutagen/EVAL-0052 fixture-conformance defect, and no partial production
implementation was retained. The project owner subsequently selected Option A:
the affected public and evaluator-private fixtures were corrected and
independently resealed while preserving pinned Mutagen `0.54.2` and ADR-0009.
That prerequisite blocker was cleared, and the fresh Slice 4 implementation
passes the public EVAL-0052 and applicable EVAL-0086 assertions. Evaluator-v1
produced no valid held-out product verdict and is retired. Slice 4.5 now owns
evaluator-v2 qualification and held-out acceptance; Stage A is active and
held-out scoring has not run.

- [M1 Slice 0 — Toolchain, licensing posture, and dependency lock](M1-slice-0.md)
- [M1 Slice 1 — Versioned domain, wire, output, and evaluation contracts](M1-slice-1.md)
- [M1 Slice 2 — Local execution substrate, persistence, and platform boundaries](M1-slice-2.md)
- [M1 Slice 3 — Supported-target admission and MO2 snapshot reconstruction](M1-slice-3.md)
- [M1 Slice 3.5 — Bethesda binary fixture qualification](M1-slice-3.5.md)
- [M1 Slice 4 — Bethesda semantic extraction and typed indexes](M1-slice-4.md)
- [M1 Slice 4.5 — Held-out evaluation v2](M1-slice-4.5.md)
