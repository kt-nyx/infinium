# Milestone plans

Status: Draft  
Last reviewed: 2026-08-04

The M0 research plan completed on 2026-07-28. The accepted
[M1 backend semantic proof plan](M1-backend-semantic-proof.md) is the active
implementation milestone plan together with its accepted
[revision 3 amendment](M1-backend-semantic-proof-evaluator-v2-amendment.md).

Slices 0 through 3.5 are complete. The accepted
[execution plan](../slices/M1-slice-3.5-bethesda-fixture-qualification.md)
qualified the Bethesda and applicable taxonomy fixtures before Slice 4.
The positively qualified Bethesda semantic and typed-index path is implemented
at `98fe8a5` with passing retained public gates. Evaluator-v1 issued no valid
held-out product verdict and is retired. The historical evaluator-v2 `/2`
Stage C invocation ran once, but Stage C.5 invalidated its product verdict; no
valid successor held-out verdict exists. The unchanged Slice 4 candidate
remains publicly verified. Slice 4.5 is active under plan revision `/3`, with
final protocol `/4` qualified and frozen at
`3693d19563c636cd2879804633ca4ce52448d2c1`. The B2 input bytes already exist
and passed independent byte review, but B2 oracle qualification under `/4` has
not run. C2 has not run, Stage D has not started, and Slice 5 remains blocked.

Waves A through D have completed investigations and accepted integrated
dispositions. Gates A through D are met at their applicable M0 research,
design, or qualification layers. Gate C was closed on 2026-07-28 by the
accepted category-neutral anti-overfitting rules and RESEARCH-0034/0035;
EVAL-0016 and EVAL-0017 remain qualified candidates with accepted Wave F
specifications, pending executable fixture construction and later execution.
ADR-0008 through ADR-0011 accept the Wave B integration boundaries,
while taxonomy version `0.1.0` records Wave C's normative classification
result. ADR-0012 through ADR-0014 accept Wave D's Nexus, OpenAI, and LOOT
managed-data boundaries. Wave E research is complete through RESEARCH-0046.
ADR-0015 through ADR-0023 accept the complete Wave E architecture; Dapr and
ADR-0024's Codex proposal are rejected. Gate E is met at the M0
architecture/design layer, while the named
conformance/evaluation cases remain unpassed.

Current plans:

- [M0 research foundation](M0-research-foundation.md) — Accepted 2026-07-25;
  completed 2026-07-28.
- [M1 backend semantic proof](M1-backend-semantic-proof.md) — Accepted
  2026-07-28; active.
- [M1 backend semantic proof revision 3](M1-backend-semantic-proof-evaluator-v2-amendment.md)
  — Accepted 2026-08-04; active evaluator-v2 sequencing and gate ownership.

Expected future plans:

- M2 frontend workflow proof;
- M3 trusted personal preflight;
- M4 conditional public-facing MVP.

The backend semantic proof plan must not be accepted or implemented until the
product documentation is reviewed and accepted and the architecture/evaluation
prerequisites are accepted. A proposed plan may be drafted earlier as a
reviewable Wave F output.

M1 status clarification: protocol `/4` is the final authorized M1 evaluator
revision. One fresh private oracle reviewer may resume B2 once. Another
oracle-authority gap does not authorize `/5`, evaluator expansion, or product
output as oracle truth; it must be recorded for owner milestone disposition.
