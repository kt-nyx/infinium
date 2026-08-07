# Infinium

Infinium is a planned evidence-driven pre-playthrough quality-assurance and
diagnostic tool for large Skyrim Special Edition modlists managed with Mod
Organizer 2.

The M0 research foundation is complete. The accepted M1 backend semantic proof
plan is now the active implementation authority.

The abandoned implementation is not part of this working tree. A complete
maintainer-local archive is stored outside the repository at sibling path
`../infinium-legacy-archive/`; its tracked source also remains in Git history
through commit `7dd3da6`. It is not the specification for the rebuilt product
and should not be inspected unless archaeological review is explicitly
requested.

Start with [`docs/README.md`](docs/README.md).

## Current status

- Product discovery: consolidated into the accepted product baseline
- Product documentation: accepted baseline plus mod-impact taxonomy `0.1.0`
- Research: Waves A through F accepted; Gates A through F met
- M0 research plan: completed on 2026-07-28
- Architecture: ADR-0001 through ADR-0023 and ADR-0025 through ADR-0032 accepted;
  ADR-0024 rejected
- M1 plan: revision `/3` accepted and active
- M1 implementation: Slices 0 through 4 implementation-complete; Slice 3's exact-target,
  headless non-mutation, explicit initiation, and MO2 effective-state gates
  EVAL-0045, EVAL-0046, EVAL-0051, and EVAL-0054 pass for the admitted
  boundary; Slice 3.5 independently qualifies the Bethesda and applicable
  taxonomy fixtures required before Slice 4; Slice 4 delivered the bounded
  Bethesda semantic and typed-index implementation at commit `98fe8a5`, with
  passing retained public verification and a successful current baseline
  rerun. The historical evaluator-v2 `/2` Stage C invocation ran once and its
  `FAIL` remains immutable, but Stage C.5 invalidated its product verdict, so
  no valid successor held-out verdict currently exists. No product correction
  is indicated, and the Slice
  4 product candidate remains unchanged and publicly verified. Final protocol
  `/4` is qualified and frozen historical public evidence at
  `3693d19563c636cd2879804633ca4ce52448d2c1`. The B2 input bytes already exist
  and passed independent byte review, but the single authorized B2 resume later
  stopped without an oracle or product verdict. The `/5` successor attempt
  hard-stopped before implementation or freeze and is retired unqualified.
  ADR-0032 defers private held-out evaluation with no valid current verdict and
  retains `/4` only for bounded public regression with its known gap excluded.
  No B2, C2, Stage D, corpus, scoring, or replacement-evaluator work is
  authorized. See the
  [sanitized incident record](docs/evaluation/evaluator-v2-stage-c5-adjudication-incident.md).
  Slice 4.5 remains in evaluator-deferral closeout. Slice 5 is the next eligible
  product package only after final closeout acceptance, under the M1
  continuation verification profile. M1 remains active; public conformance is
  not a private reliability/readiness verdict
- xEdit: historical integration/oracle recommendation rejected; excluded from
  every Infinium boundary
- Abandoned implementation: removed from the active tree and retained only in
  the external local archive and Git history

Implementation must proceed slice-by-slice under the accepted
[M1 backend semantic proof plan](docs/plans/milestones/M1-backend-semantic-proof.md)
and its accepted
[revision 3 amendment](docs/plans/milestones/M1-backend-semantic-proof-evaluator-v2-amendment.md).

M1 Slice 4.5 retains frozen protocol `/4` and projection `3.0.0` as historical
evidence. The only current use is the
[bounded public regression profile](docs/evaluation/m1-slice4-protocol-4-bounded-regression-usage.md).
Protocol `/5` is retired unqualified, private held-out evaluation is deferred,
and no current product verdict exists. Current work follows the accepted
[evaluator-deferral closeout plan](docs/plans/slices/M1-slice-4.5-evaluator-deferral-and-m1-continuation.md).
After final closeout acceptance, Slice 5 uses the
[M1 continuation verification profile](docs/evaluation/m1-continuation-verification-profile.md).
