# Infinium documentation

Status: Draft  
Last reviewed: 2026-07-28

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
  [accepted M1 evaluation baseline](evaluation/m1-evaluation-baseline.md),
  [fixture guidelines](evaluation/fixture-guidelines.md), and
  [anti-overfitting rules](evaluation/anti-overfitting-rules.md)
- Research: [source registry](research/source-registry.md),
  [taxonomy research dependency map](research/taxonomy-dependency-map.md),
  [accepted deferred-question and residual-risk register](research/deferred-question-and-residual-risk-register.md), and
  [investigation procedure](research/investigations/README.md)
- Planning: [plan policy](plans/README.md),
  [milestone-plan index](plans/milestones/README.md), and
  [accepted M1 backend semantic proof plan](plans/milestones/M1-backend-semantic-proof.md),
  [research-agent handoff template](plans/research-investigation-agent-handoff-template.md)
- Reference: [glossary](glossary.md)

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
- **Historical legacy material** is excluded from the active repository. A
  complete maintainer-local implementation archive exists at sibling path
  `../infinium-legacy-archive/`, and the tracked implementation and assessment
  documents remain recoverable from Git history through commit `7dd3da6`.
  They must not be inspected unless the user explicitly requests
  archaeological review.

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
  was accepted on 2026-07-25 and completed on 2026-07-28.
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
  specifications are accepted. The category-neutral anti-overfitting rules
  are accepted, and the project owner accepted RESEARCH-0034/0035 as completing
  the remaining RQ-023/RQ-025 qualification work. Gate C is met at the M0
  research/qualification layer;
  no evaluation case or analyzer implementation is thereby claimed to pass.
- Revised Wave D research RESEARCH-0030 through RESEARCH-0033 is complete.
  Authenticated Nexus qualification and accepted ADR-0012 remove the former
  Nexus/GraphQL blockers. The owner accepted the revised Wave D disposition
  and ADR-0013/ADR-0014 on 2026-07-28; Gate D is met at the M0
  research/design layer. Production adapter, credential, budget, and
  evaluation conformance remain later gates.
- Wave E research is complete through RESEARCH-0046.
  ADR-0015 through ADR-0023 accept SQLite plus content-addressed payload
  storage, an application-owned transactional lifecycle, a
  .NET/React/direct WPF-WebView2 stack, standalone coordinator and bounded
  workers, named-pipe gRPC, one-shot reusable-secret/provider handling,
  layered local security controls, conservative finding/case continuity, and
  atomic budget enforcement. Dapr was rejected without a comparison
  prototype.
  Direct, schema-constrained Responses API calls through a user-supplied,
  usage-priced Platform API key remain the OpenAI path; ADR-0024 rejects the
  Codex/ChatGPT-plan proposal. Gate E is met at the M0 architecture/design
  layer. Implementation and evaluation conformance remain pending.
- ADR-0001 through ADR-0011 were accepted on 2026-07-25. ADR-0012 through
  ADR-0023 and ADR-0025 were accepted on 2026-07-28; ADR-0024 was rejected.
- Wave F produced the accepted M1 evaluation baseline, detailed semantic and
  platform/operational case specifications and fixture manifests, the
  RQ-028 calibration protocol, exact OpenAI M1 profile research, a
  deferred-question/residual-risk register, ADR-0025, and the M1 backend
  semantic proof plan. The package was integrated, independently reviewed, and
  accepted on 2026-07-28. Gate F is met, M0 is complete, and the M1 plan is
  active. M1 Slice 0 subsequently established the locked toolchain,
  dependency evidence, and required repository skeleton. M1 Slice 1
  subsequently established the versioned domain, wire, output, fixture, and
  assertion contracts plus answer-isolating readers. M1 Slice 2 subsequently
  established the local coordinator/worker substrate, authoritative lifecycle
  and persistence boundaries, protected-root write controls, and platform
  evaluation package. No complete accepted evaluation case has passed; only
  the plan-declared Slice 1 contract portions and Slice 2 substrate portions
  are satisfied, with the remaining coverage gap retained explicitly in the
  Slice 2 fixture oracle and implementation record.
- Infinium uses GPLv3-family strong copyleft. MO2 and LOOT remain
  user-installed; ADR-0007 excludes xEdit completely. ADR-0008 through
  ADR-0011 accept deterministic MO2 2.5.2 reconstruction, the exact initial
  Steam `1.6.1170.0` runtime, pinned Mutagen `0.54.2`, dependency-aware
  snapshots, and the conditional libloot `0.29.6` boundary. Their
  implementation operations and supported surfaces remain qualification-gated.
- The old codebase, dependencies, local artifacts, and uncommitted work were
  first isolated on 2026-07-24, then removed from the active repository and
  moved intact to the maintainer-local sibling archive
  `../infinium-legacy-archive/` on 2026-07-28. Its tracked source remains
  recoverable from Git history through commit `7dd3da6`.

## Change discipline

- Give requirements stable identifiers.
- Link ADRs and evaluation cases back to requirements.
- Cite external technical claims with source, version, and retrieval date.
- Preserve superseded decisions rather than rewriting history.
- Record uncertainty explicitly.
- Avoid duplicating the same authoritative statement across several documents.
- Update this index when a new authoritative document is added.
