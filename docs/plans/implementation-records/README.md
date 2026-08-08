# Implementation records

Status: Active
Last reviewed: 2026-08-08

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
candidate at `98fe8a5` retains that historical verification. Evaluator-v1
produced no
valid held-out product verdict and is retired. The historical evaluator-v2
`/2` Stage C
invocation ran once, but Stage C.5 invalidated its product verdict; no valid
successor held-out verdict exists. Slice 4.5 historically owned evaluator-v2
qualification and held-out acceptance. Final protocol `/4` is qualified and frozen at
`3693d19563c636cd2879804633ca4ce52448d2c1`; the B2 input bytes already exist
and passed independent byte review. The single authorized B2 resume ran once
and stopped on another public lexical-authority gap without an oracle,
candidate execution, scoring, or product verdict. Public realignment and
candidate freeze remain complete at
`a98d648bd0adb2751ee0c09828e0227b1583950f`. The first public oracle-contract
completion attempt hard-stopped. ADR-0032 now defers private held-out
evaluation without a verdict and limits `/4` to bounded public regression.
No B2, C2, Stage D, corpus, scoring, or replacement-evaluator work is
authorized.

The first public completion attempt is historical hard-stop evidence at
`9d29d7a`. ADR-0029 resolved its partial-decode semantic question, and accepted
work `M1/S4.5/PRE-B2` completed through WP5. WP5 classified an evaluator `/4`
representation gap. ADR-0030 and work `M1/S4.5/PRE-B2/V5/WP0` through WP4
historically authorized one public `/5` successor attempt. WP0 completed, but
WP1's accepted-model FaceGen/coverage composition hard stop remains historical
evidence. ADR-0031 and WP1R accepted the distinct `/5` successor model and
global composition proof; WP1V later hard-stopped, WP1 never proof-closed, and
WP2-WP4 never started. `/5` is retired unqualified. Evaluator-deferral closeout
is accepted; Slice 4.5 is closed, Slice 5 WP1 is complete and reviewed, and
Slice 5 WP2 is next under the M1 continuation verification profile. M1 remains
active.

Slices 5-9 must add implementation records at completion. Each record must map
its applicable continuation-profile layers to exact requirements, cases,
fixtures/evidence, commands/results, coverage/gaps/unsupported surfaces, fresh
review, and claims; it must state explicitly that no private held-out verdict
exists.

The [M1 Slice 5 record](M1-slice-5.md) retains the failed comprehensive-corpus
review and the owner-authorized staged-verification recovery. WP1 is complete
and reviewed; the rejected corpus establishes no product verdict, and WP2 is
eligible without another owner acceptance gate.

- [M1 Slice 0 — Toolchain, licensing posture, and dependency lock](M1-slice-0.md)
- [M1 Slice 1 — Versioned domain, wire, output, and evaluation contracts](M1-slice-1.md)
- [M1 Slice 2 — Local execution substrate, persistence, and platform boundaries](M1-slice-2.md)
- [M1 Slice 3 — Supported-target admission and MO2 snapshot reconstruction](M1-slice-3.md)
- [M1 Slice 3.5 — Bethesda binary fixture qualification](M1-slice-3.5.md)
- [M1 Slice 4 — Bethesda semantic extraction and typed indexes](M1-slice-4.md)
- [M1 Slice 4.5 — Held-out evaluation v2](M1-slice-4.5.md)
  — final bounded public Stage A freeze at
  `3693d19563c636cd2879804633ca4ce52448d2c1`; owner semantic disposition
  accepted; public realignment complete at `a98d648`; B2 stopped on a public
  lexical-authority gap; the first public contract attempt hard-stopped;
  `M1/S4.5/PRE-B2/V5/WP1R` accepted the distinct successor model before WP1V
  hard-stopped; ADR-0032 retires `/5` unqualified, and the evaluator-deferral
  closeout is accepted. Slice 4.5 is closed; Slice 5 WP1 is complete and WP2 is
  next.
- [Post-Slice-4.5 documentation and Slice 5 readiness review](M1-post-slice-4.5-documentation-readiness-review.md)
  — repository-wide status, authority, link, JSON, and verification review
  after fast-forward integration to `main`; records the now-completed readiness
  gate that allowed Slice 5 planning to begin.
- [M1 Slice 5 — Evidence, documentation, candidates, cases, and replay](M1-slice-5.md)
  — WP1 complete and reviewed after removal/deferral of the rejected premature
  comprehensive corpus; WP2 is eligible under staged work-package authority.
