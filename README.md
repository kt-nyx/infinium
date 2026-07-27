# Infinium

Infinium is a planned evidence-driven pre-playthrough quality-assurance and
diagnostic tool for large Skyrim Special Edition modlists managed with Mod
Organizer 2.

The project is currently in the M0 research-foundation milestone. The
abandoned implementation has been preserved intact under
[`legacy/`](legacy/) and is not the specification for the rebuilt product.

Start with [`docs/README.md`](docs/README.md).

## Current status

- Product discovery: consolidated into the accepted product baseline
- Product documentation: accepted baseline plus mod-impact taxonomy `0.1.0`
- Architecture decisions: ADR-0001 through ADR-0011 accepted on 2026-07-25
- M0 research plan: accepted and active as of 2026-07-25
- Research: Waves A through C have accepted integrated dispositions; Gates A
  and B are met, while Gate C retains the exact FaceGen qualification and
  EVAL-0016/EVAL-0017 real-mod case prerequisites
- Wave A decisions: bounded supported Nexus API analysis, useful-analysis
  private source retention, GPLv3-family licensing, and explicit external-tool
  dependency boundaries
- Wave C decisions: accepted Skyrim SE mod-impact taxonomy, bounded
  root/generated/configuration/PEX/asset/record roadmaps, synthetic-first
  corpus policy, and typed-index/causal-candidate design without naïve
  all-pairs model comparison
- xEdit: historical integration/oracle recommendation rejected; excluded from
  every Infinium boundary
- Implementation architecture: not accepted
- New implementation: not started
- Legacy implementation: preserved for reference only

No implementation work should begin until the blocking technical questions
have been researched and the relevant architecture decisions and milestone
plan have been accepted.
