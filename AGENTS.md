# Infinium repository guidance

This repository is being rebuilt from a product specification. The abandoned
implementation is preserved under `legacy/` and is not authoritative.

## Required reading order

Before research, planning, architecture, or implementation work:

1. `docs/README.md`
2. `docs/product/product-definition.md`
3. `docs/product/requirements.md`
4. `docs/product/workflows.md`
5. `docs/product/domain-model.md`
6. `docs/product/severity-confidence-and-coverage.md`
7. `docs/product/analysis-catalog.md`
8. `docs/product/scope-and-milestones.md`
9. `docs/architecture/overview.md`
10. `docs/architecture/data-and-trust-model.md`
11. Relevant accepted ADRs under `docs/architecture/decisions/`

Then read the task-specific material:

- research: the relevant entry in `docs/research/open-questions.md`,
  `docs/research/source-registry.md` when sources are involved, and
  `docs/research/investigations/README.md`;
- architecture: `docs/architecture/decisions/README.md`, relevant proposed
  ADRs/research, and the applicable integration, jobs/caching, and
  security/privacy documents;
- evaluation or analyzer work: `docs/evaluation/evaluation-strategy.md`,
  `docs/evaluation/case-catalog.md`, `docs/evaluation/fixture-guidelines.md`,
  and `docs/evaluation/anti-overfitting-rules.md`;
- implementation: the active accepted milestone plan, if one exists.

## Working rules

- Treat accepted product documents and ADRs as authoritative.
- Treat `legacy/` as archaeological context only.
- Do not copy legacy behavior without independent validation against current
  requirements.
- Put unresolved technical questions in `docs/research/open-questions.md`.
- Record researched evidence in `docs/research/investigations/`.
- Do not turn a research conclusion into architecture implicitly; create or
  update an ADR.
- Do not start implementation without an accepted milestone plan linking its
  requirements, decisions, and evaluation cases.
- Keep deterministic observations, external claims, hypotheses, findings, and
  recommendations distinct.
- Do not introduce real-mod-name or fixture-specific rules into production
  analysis.
- Preserve full provenance and expose coverage gaps rather than inventing
  certainty.
