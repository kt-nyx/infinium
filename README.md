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
- Architecture: ADR-0001 through ADR-0023 and ADR-0025 through ADR-0027 accepted;
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
  `/4` is qualified and frozen at
  `3693d19563c636cd2879804633ca4ce52448d2c1`. The B2 input bytes already exist
  and passed independent byte review, but B2 oracle qualification under `/4`
  has not run. C2 has not run, Stage D has not started, and Slice 4.5 remains
  incomplete. See the
  [sanitized incident record](docs/evaluation/evaluator-v2-stage-c5-adjudication-incident.md).
  Slice 5 remains blocked and M1 remains active
- xEdit: historical integration/oracle recommendation rejected; excluded from
  every Infinium boundary
- Abandoned implementation: removed from the active tree and retained only in
  the external local archive and Git history

Implementation must proceed slice-by-slice under the accepted
[M1 backend semantic proof plan](docs/plans/milestones/M1-backend-semantic-proof.md)
and its accepted
[revision 3 amendment](docs/plans/milestones/M1-backend-semantic-proof-evaluator-v2-amendment.md).

M1 Slice 4.5 has frozen the final bounded public evaluator at protocol `/4`
and projection `3.0.0`. The Slice 4 product candidate is unchanged. Private
oracle qualification under `/4`, Stage C2 scoring, and Stage D have not run;
Slice 5 remains blocked. One fresh private oracle reviewer may resume B2 once.
Another authority gap does not authorize `/5`, evaluator expansion, or product
output as oracle truth; it must be recorded for owner milestone disposition.
See the
[final public freeze handoff](docs/evaluation/evaluator-v2-stage-a-final-bounded-freeze.json).
