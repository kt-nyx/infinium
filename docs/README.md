# Infinium documentation

Status: Draft  
Last reviewed: 2026-08-07

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
  [product/evaluator authority boundary](evaluation/product-evaluator-boundary.md),
  [accepted M1 evaluation baseline](evaluation/m1-evaluation-baseline.md),
  [accepted M1 continuation verification profile](evaluation/m1-continuation-verification-profile.md),
  [fixture guidelines](evaluation/fixture-guidelines.md), and
  [anti-overfitting rules](evaluation/anti-overfitting-rules.md), with the
  historical evaluator-private boundary in
  [evaluator-private fixture governance](evaluation/evaluator-private-fixture-governance.md)
  and current public-rule/private-data boundary in
  [evaluator-private fixture governance v2](evaluation/evaluator-private-fixture-governance-v2.md)
- Research: [source registry](research/source-registry.md),
  [taxonomy research dependency map](research/taxonomy-dependency-map.md),
  [accepted deferred-question and residual-risk register](research/deferred-question-and-residual-risk-register.md), and
  [investigation procedure](research/investigations/README.md)
- Planning: [plan policy](plans/README.md),
  [milestone-plan index](plans/milestones/README.md),
  [accepted M1 backend semantic proof plan](plans/milestones/M1-backend-semantic-proof.md),
  with its accepted
  [revision 3 amendment](plans/milestones/M1-backend-semantic-proof-evaluator-v2-amendment.md),
  the historical
  [Slice 4.5 evaluator-v2 plan](plans/slices/M1-slice-4.5-held-out-evaluation-v2.md),
  its accepted
  [Pre-B2 evidence-contract totality successor](plans/slices/M1-slice-4.5-pre-B2-evidence-contract-totality.md),
  the historical retired
  [protocol `/5` successor plan](plans/slices/M1-slice-4.5-protocol-5-successor-realignment.md),
  the completed
  [evaluator-deferral and M1-continuation closeout plan](plans/slices/M1-slice-4.5-evaluator-deferral-and-m1-continuation.md),
  the completed
  [post-Slice-4.5 documentation and Slice 5 readiness review](plans/implementation-records/M1-post-slice-4.5-documentation-readiness-review.md),
  the [work-breakdown notation](plans/work-breakdown-notation.md),
  and the
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
  ADR-0026's evaluator-private repository and delegated-access boundary was
  accepted on 2026-08-01. ADR-0027's public-rule/private-data evaluator-v2
  architecture was accepted on 2026-08-04 and partially supersedes ADR-0026.
  ADR-0028's bounded M1 semantic-reporting and oracle-authority disposition and
  ADR-0029's layered-evidence decision were accepted on 2026-08-05. ADR-0030
  and ADR-0031 were accepted on 2026-08-07 as the historical authorization and
  semantic model for one `/5` successor attempt. ADR-0032 supersedes their
  active `/5` authority, retires that protocol unqualified, defers private M1
  held-out evaluation, and authorizes bounded `/4` regression plus the public
  M1 continuation profile.
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
  evaluation package. M1 Slice 3 subsequently established exact MO2/Skyrim
  admission, explicit-profile effective-state reconstruction, the durable
  headless capture path, retained snapshot authority, and the independent
  exact-target evaluator package. EVAL-0045, EVAL-0046 for the delivered
  capture operation, EVAL-0051, and EVAL-0054 pass for that admitted boundary;
  broader MO2 versions, additional mappers, archive members, and later semantic
  slices remain unsupported or pending. Slice 3.5 subsequently constructed and
  independently qualified the Bethesda and applicable taxonomy fixtures under
  ADR-0026's separate evaluator-private store boundary. Slice 4 delivered the
  original bounded Bethesda semantic and typed-index implementation at
  `98fe8a5`, with passing retained public gates. The historical evaluator-v2
  `/2` Stage C invocation ran once and its
  `FAIL` remains immutable, but the owner-supplied Stage C.5 adjudication
  invalidated its product verdict, so no valid successor held-out verdict
  currently exists. The later independent authority-completion review found six
  semantic mismatches in that historical candidate, and ADR-0028 resolved the
  intended behavior. The public realignment, independent review,
  requalification, and candidate freeze are complete at conforming candidate
  `a98d648bd0adb2751ee0c09828e0227b1583950f`. Final protocol `/4` is qualified
  and frozen at
  `3693d19563c636cd2879804633ca4ce52448d2c1`. The single authorized B2 resume
  stopped without an oracle or product verdict, and the first public contract-
  completion attempt then hard-stopped before candidate inspection. ADR-0029
  resolves the disclosed partial-decode choice. Accepted work
  `M1/S4.5/PRE-B2` replaced fixture-led corrections with deterministic
  totality, model-derived exercises, fresh product-blind review, and candidate
  classification. WP1-WP5 completed, and WP5 proved an evaluator `/4`
  representation gap. ADR-0030 then authorized the public-only historical
  `M1/S4.5/PRE-B2/V5` successor cycle. WP1 hard-stopped on a global
  FaceGen/coverage composition contradiction; that history remains recorded.
  ADR-0031 and WP1R accepted the distinct `/5` successor semantic model, exact
  loose-availability gap, and mandatory global composition proof. Resumed WP1
  recovery WP1V then hard-stopped after its final review found noncanonical
  resolved-link witnesses and a self-authorizing ledger/document comparison.
  WP1 was not proof-closed; WP2-WP4 never started. ADR-0032 now retires `/5`
  unqualified and defers the private held-out evaluator with no valid current
  product verdict. Frozen `/4` is retained only for bounded public regression
  with its known gap excluded. No B2, C2, or Stage D work is authorized. The
  detailed sanitized history is recorded
  in the
  [Stage C.5 incident](evaluation/evaluator-v2-stage-c5-adjudication-incident.md).
  `M1/S4.5/EVAL-CLOSEOUT` is accepted and complete. Slice 4.5 is closed by
  owner disposition, and Slice 5 is eligible as the next product package under
  the accepted M1 continuation verification profile. The exact result is in
  the [closeout acceptance record](evaluation/m1-slice4.5-evaluator-deferral-closeout-acceptance.md).
  M1 remains active; public conformance is not a private held-out,
  reliability, or readiness claim. M1 explicitly defers `QUST` forced-alias
  semantics and retains EVAL-0017's REFR linked-reference/placement proof as
  the materially different category.
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

## Final bounded M1 held-out authority

- [Sanitized Stage B.2 contract gap](evaluation/evaluator-v2-successor-stage-b2-contract-gap.md)
- [Final Slice 4 held-out scope amendment](evaluation/m1-slice4-heldout-scope-final-amendment.md)
- [Normative oracle-authority matrix](evaluation/m1-slice4-heldout-oracle-authority-matrix.md)
- [Accepted semantic-authority owner disposition](evaluation/m1-slice4-semantic-authority-owner-disposition.md)
- [ADR-0028 bounded M1 semantic authority](architecture/decisions/ADR-0028-m1-bethesda-semantic-reporting-and-oracle-authority.md)
- [Protocol `/4` final freeze handoff](evaluation/evaluator-v2-stage-a-final-bounded-freeze.json)
