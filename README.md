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
- Architecture: ADR-0001 through ADR-0023, ADR-0025, and ADR-0026 accepted;
  ADR-0024 rejected
- M1 plan: accepted and active
- M1 implementation: Slices 0 through 3.5 complete; Slice 3's exact-target,
  headless non-mutation, explicit initiation, and MO2 effective-state gates
  EVAL-0045, EVAL-0046, EVAL-0051, and EVAL-0054 pass for the admitted
  boundary; Slice 3.5 independently qualifies the Bethesda and applicable
  taxonomy fixtures required before Slice 4, including the ADR-0026 private
  validation boundary; the 2026-08-01 pre-Slice-4 readiness audit found no
  remaining start blocker; Slice 4 has not started
- xEdit: historical integration/oracle recommendation rejected; excluded from
  every Infinium boundary
- Abandoned implementation: removed from the active tree and retained only in
  the external local archive and Git history

Implementation must proceed slice-by-slice under the accepted
[M1 backend semantic proof plan](docs/plans/milestones/M1-backend-semantic-proof.md)
and its accepted
[revision 2 amendment](docs/plans/milestones/M1-backend-semantic-proof-adr0026-amendment.md).
