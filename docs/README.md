# Infinium documentation

Status: Draft  
Last reviewed: 2026-07-25

This directory is the authoritative entry point for the rebuilt Infinium
project. It records product intent, accepted decisions, research, evaluation
standards, and implementation plans without treating the abandoned code as the
specification.

## Authoritative reading order

1. [Product definition](product/product-definition.md)
2. [Requirements](product/requirements.md)
3. [Workflows](product/workflows.md)
4. [Domain model](product/domain-model.md)
5. [Severity, confidence, maturity, coverage, and readiness](product/severity-confidence-and-coverage.md)
6. [Analysis catalog](product/analysis-catalog.md)
7. [Scope and milestones](product/scope-and-milestones.md)
8. [Architecture overview](architecture/overview.md)
9. [Architecture decisions](architecture/decisions/README.md)
10. [Evaluation strategy](evaluation/evaluation-strategy.md)
11. [Open research questions](research/open-questions.md)

## Supporting document map

- Architecture: [data and trust](architecture/data-and-trust-model.md),
  [jobs/caching/snapshots](architecture/jobs-caching-and-snapshots.md),
  [integration boundaries](architecture/integrations.md), and
  [security/privacy](architecture/security-and-privacy.md)
- Evaluation: [case catalog](evaluation/case-catalog.md),
  [fixture guidelines](evaluation/fixture-guidelines.md), and
  [anti-overfitting rules](evaluation/anti-overfitting-rules.md)
- Research: [source registry](research/source-registry.md),
  [taxonomy research dependency map](research/taxonomy-dependency-map.md), and
  [investigation procedure](research/investigations/README.md)
- Planning: [plan policy](plans/README.md),
  [milestone-plan index](plans/milestones/README.md), and
  [research-agent handoff template](plans/research-investigation-agent-handoff-template.md)
- Reference: [glossary](glossary.md),
  [legacy assessment](legacy/implementation-assessment.md), and
  [legacy reuse disposition](legacy/reuse-disposition.md)

## Document authority

- **Product documents** define what the product is, who it serves, required
  behavior, and non-goals.
- **Architecture Decision Records (ADRs)** record accepted technical decisions
  and their consequences.
- **Research documents** contain evidence and recommendations. Research is not
  an accepted decision by itself.
- **Plans** describe bounded work against accepted requirements and ADRs. Plans
  must not redefine product behavior.
- **Evaluation documents** define how claims of correctness, reliability, and
  generalization are demonstrated.
- **Legacy documents** describe the abandoned implementation and possible reuse.
  The implementation itself is preserved at [`../legacy/`](../legacy/).

If documents conflict, accepted product requirements take precedence over
plans and legacy behavior. Accepted ADRs govern implementation only where they
do not contradict product requirements.

## Status vocabulary

- **Draft:** Incomplete or awaiting review.
- **Proposed:** Complete enough for a decision but not accepted.
- **Accepted:** Authoritative until superseded.
- **Deferred:** Intentionally postponed.
- **Rejected:** Considered and not selected.
- **Superseded:** Replaced by a newer document or decision.
- **Template:** Reusable document scaffold; not a decision or product
  requirement.

Every material document should include a status and last-reviewed date.

## Current project state

- The seven product documents in `product/` were accepted as the authoritative
  product baseline on 2026-07-25.
- The detailed [M0 research-foundation plan](plans/milestones/M0-research-foundation.md)
  was accepted on 2026-07-25 and is the active research plan.
- Research execution and implementation planning have not started.
- ADR-0001 through ADR-0004 were accepted on 2026-07-25.
- No application stack is accepted yet.
- The current leading stack candidate is documented as a proposal only.
- The old codebase, dependencies, local artifacts, and uncommitted work were
  moved intact to [`../legacy/`](../legacy/) on 2026-07-24.

## Change discipline

- Give requirements stable identifiers.
- Link ADRs and evaluation cases back to requirements.
- Cite external technical claims with source, version, and retrieval date.
- Preserve superseded decisions rather than rewriting history.
- Record uncertainty explicitly.
- Avoid duplicating the same authoritative statement across several documents.
- Update this index when a new authoritative document is added.
