# Infinium documentation

Status: Draft  
Last reviewed: 2026-07-26

This directory is the authoritative entry point for the rebuilt Infinium
project. It records product intent, accepted decisions, research, evaluation
standards, and implementation plans without treating the abandoned code as the
specification.

## Authoritative reading order

1. [Product definition](product/product-definition.md)
2. [Requirements](product/requirements.md)
3. [Skyrim SE mod-impact taxonomy](product/mod-impact-taxonomy.md)
4. [Workflows](product/workflows.md)
5. [Domain model](product/domain-model.md)
6. [Severity, confidence, maturity, coverage, and readiness](product/severity-confidence-and-coverage.md)
7. [Analysis catalog](product/analysis-catalog.md)
8. [Scope and milestones](product/scope-and-milestones.md)
9. [Architecture overview](architecture/overview.md)
10. [Architecture decisions](architecture/decisions/README.md)
11. [Evaluation strategy](evaluation/evaluation-strategy.md)
12. [Open research questions](research/open-questions.md)

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
- **Completed:** The documented work and its independent review are finished.
  This status does not itself make a recommendation authoritative; authority
  comes from the recorded accepted disposition.
- **Accepted:** Authoritative until superseded.
- **Deferred:** Intentionally postponed.
- **Rejected:** Considered and not selected.
- **Superseded:** Replaced by a newer document or decision.
- **Template:** Reusable document scaffold; not a decision or product
  requirement.

Every material document should include a status and last-reviewed date.

## Current project state

- The eight accepted documents under `product/`, including the
  [Skyrim SE mod-impact taxonomy](product/mod-impact-taxonomy.md), form the
  authoritative product baseline.
- The detailed [M0 research-foundation plan](plans/milestones/M0-research-foundation.md)
  was accepted on 2026-07-25 and is the active research plan.
- Wave A research has completed its initial investigations and integration
  review. Gate A is met with documented non-blocking gaps under ADR-0005;
  RQ-026 is resolved under ADR-0006 and RQ-031 is answered for M0 with
  source-specific conditions and measured-storage follow-up.
- Wave B's eight bounded investigations and independent integration review are
  complete and accepted. Gate B is met with documented non-blocking gaps for
  M0 research progression. Reviewed conformance specifications are a
  prerequisite for the M1 implementation plan; executing them gates M1
  completion and support claims over the affected surfaces.
- Wave C's ten bounded investigations and owner disposition are complete and
  accepted. Taxonomy version `0.1.0` and the EVAL-0032/EVAL-0086
  specifications are accepted. Gate C still requires the exact loose-only
  FaceGen qualification and pinned EVAL-0016/EVAL-0017 real-mod cases.
- ADR-0001 through ADR-0011 were accepted on 2026-07-25.
- Infinium uses GPLv3-family strong copyleft. MO2 and LOOT remain
  user-installed; ADR-0007 excludes xEdit completely. ADR-0008 through
  ADR-0011 accept deterministic MO2 2.5.2 reconstruction, the exact initial
  Steam `1.6.1170.0` runtime, pinned Mutagen `0.54.2`, dependency-aware
  snapshots, and the conditional libloot `0.29.6` boundary. Their
  implementation operations and supported surfaces remain qualification-gated.
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
