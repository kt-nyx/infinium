# Infinium repository guidance

This repository is being rebuilt from a product specification. The abandoned
implementation is not part of the active working tree and is not
authoritative. A complete maintainer-local archive exists outside the
repository at sibling path `../infinium-legacy-archive/`; the tracked portion
also remains recoverable from Git history through commit `7dd3da6`. Do not
inspect, restore, or use that archive unless the user explicitly requests it.

## Required reading order

Before research, planning, architecture, or implementation work:

1. `docs/README.md`
2. `docs/product/product-definition.md`
3. `docs/product/requirements.md`
4. `docs/product/mod-impact-taxonomy.md`
5. `docs/product/workflows.md`
6. `docs/product/domain-model.md`
7. `docs/product/severity-confidence-and-coverage.md`
8. `docs/product/analysis-catalog.md`
9. `docs/product/scope-and-milestones.md`
10. `docs/architecture/overview.md`
11. `docs/architecture/data-and-trust-model.md`
12. Relevant accepted ADRs under `docs/architecture/decisions/`

Then read the task-specific material:

- research: the relevant entry in `docs/research/open-questions.md`,
  `docs/research/source-registry.md` when sources are involved, and
  `docs/research/investigations/README.md`;
- architecture: `docs/architecture/decisions/README.md`, relevant proposed
  ADRs/research, and the applicable integration, jobs/caching, and
  security/privacy documents;
- evaluation or analyzer work: `docs/evaluation/evaluation-strategy.md`,
  `docs/evaluation/case-catalog.md`, `docs/evaluation/fixture-guidelines.md`,
  `docs/evaluation/anti-overfitting-rules.md`, and
  `docs/evaluation/evaluator-private-fixture-governance.md`;
- implementation: the active accepted milestone plan, if one exists.

## Working rules

- Treat accepted product documents and ADRs as authoritative.
- Treat the external abandoned-implementation archive as out of scope unless
  the user explicitly requests archaeological review.
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

## Evaluator-private fixtures

The separately versioned evaluator-private fixture repository is default-deny
for ordinary Infinium work. Do not read its files directly while implementing,
debugging, tuning, or reviewing production behavior.

When scoring, fixture integrity, oracle audit, independent replacement work, or
corruption recovery genuinely requires private access, autonomously delegate a
bounded fresh-context evaluator role under
`docs/evaluation/evaluator-private-fixture-governance-v2.md`. The delegate may use
the separate repository and its pinned public contract bundle; the primary
implementation agent receives only the allowed sanitized result. If raw private
information is deliberately returned to guide production, record
contamination, reclassify that fixture version to development, and require a
materially independent replacement.

Evaluator v2 keeps protocol, schemas, canonicalization, scorer, adapter, and
calibration public under ADR-0027. Ordinary product implementation must not
repair or retry private evaluation. Private access is reserved for later
fresh-context corpus qualification or scoring tasks and returns only the
sanitized result permitted by governance v2.
