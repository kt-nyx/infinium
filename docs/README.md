# Infinium documentation

Status: Active navigation

Last reviewed: 2026-08-08

This directory is the authoritative entry point for the rebuilt Infinium
project. It records product intent, accepted decisions, current execution,
research, evaluation standards, and implementation evidence without treating
the abandoned code or historical evaluator work as current instructions.

## Start here

1. [Current project state](current-state.md)
2. [Development execution policy](development/execution-policy.md)
3. [Product definition](product/product-definition.md)
4. [Requirements](product/requirements.md)
5. [Skyrim SE mod-impact taxonomy](product/mod-impact-taxonomy.md)
6. [Workflows](product/workflows.md)
7. [Domain model](product/domain-model.md)
8. [Severity, confidence, maturity, coverage, and readiness](product/severity-confidence-and-coverage.md)
9. [Analysis catalog](product/analysis-catalog.md)
10. [Scope and milestones](product/scope-and-milestones.md)
11. [Architecture overview](architecture/overview.md)
12. [Data and trust model](architecture/data-and-trust-model.md)
13. [Architecture decisions](architecture/decisions/README.md)

After that baseline, read the active milestone plan, active slice plan, and
current implementation record linked from `current-state.md`. Load evaluation,
research, integration, security, or historical documents only when the task
touches those surfaces.

## Current execution

The single current handoff is [current-state.md](current-state.md). Do not copy
its package status into navigation files or infer current work from historical
plans and records.

Ordinary development follows the
[development execution policy](development/execution-policy.md): implement a
vertical increment, run focused checks, review, classify findings, correct,
and re-review. Routine defects do not create a correction-budget stop. Strict
freeze, no-retry, and terminal-stop rules remain scoped to the evaluator or
other exceptional operation that explicitly requires them.

## Supporting document map

- Architecture: [decision index](architecture/decisions/README.md),
  [jobs/caching/snapshots](architecture/jobs-caching-and-snapshots.md),
  [integration boundaries](architecture/integrations.md), and
  [security/privacy](architecture/security-and-privacy.md)
- Evaluation: [evaluation strategy](evaluation/evaluation-strategy.md),
  [case catalog](evaluation/case-catalog.md),
  [product/evaluator authority boundary](evaluation/product-evaluator-boundary.md),
  [M1 continuation verification profile](evaluation/m1-continuation-verification-profile.md),
  [fixture guidelines](evaluation/fixture-guidelines.md), and
  [anti-overfitting rules](evaluation/anti-overfitting-rules.md)
- Research: [open questions](research/open-questions.md),
  [source registry](research/source-registry.md),
  [deferred questions and residual risks](research/deferred-question-and-residual-risk-register.md),
  and [investigation procedure](research/investigations/README.md)
- Planning: [plan policy](plans/README.md),
  [work-breakdown notation](plans/work-breakdown-notation.md),
  [active M1 plan](plans/milestones/M1-backend-semantic-proof.md),
  [active Slice 5 plan](plans/slices/M1-slice-5-evidence-documentation-candidates-cases-replay.md),
  and [current Slice 5 record](plans/implementation-records/M1-slice-5.md)
- Reference: [glossary](glossary.md)

## Document authority

- **Product documents** define what the product is, who it serves, required
  behavior, and non-goals.
- **ADRs** record accepted technical decisions and their consequences.
- **Development policy** defines the default execution, review, correction,
  contract-maturity, and escalation model for ordinary work.
- **Current state** names the live milestone, slice, next package, and active
  navigation. It does not create product semantics.
- **Plans** define bounded work against accepted requirements and ADRs. They
  consume product and architecture decisions; they do not redefine them.
- **Implementation records** preserve what happened and the evidence obtained.
  They are historical after their handoff changes.
- **Evaluation documents** define how claims of correctness, reliability, and
  generalization are demonstrated. Evaluator-specific execution constraints
  apply only where the current task and accepted plan invoke them.
- **Research documents** contain evidence and recommendations. Research is not
  an accepted decision by itself.

If documents conflict, accepted product requirements take precedence over
plans and implementation. Accepted ADRs govern implementation where they do
not contradict product requirements. The development policy governs ordinary
execution unless a narrower accepted safety, private-evaluator, destructive,
or externally effectful protocol explicitly overrides it.

## Status vocabulary

- **Draft:** Incomplete or awaiting review.
- **Proposed:** Complete enough for a decision but not accepted.
- **Completed:** The documented work and its review are finished. Authority
  still comes from the recorded accepted disposition.
- **Accepted:** Authoritative until superseded.
- **Deferred:** Intentionally postponed.
- **Rejected:** Considered and not selected.
- **Superseded:** Replaced by a newer document or decision.
- **Template:** Reusable scaffold; not a decision or product requirement.

Every material document should include a status and last-reviewed date.

## History and excluded material

The old implementation is outside the active repository. A maintainer-local
archive exists at sibling path `../infinium-legacy-archive/`, and the tracked
implementation remains recoverable through Git commit `7dd3da6`. Do not
inspect it unless the project owner explicitly requests archaeological review.

M0 research, Waves A through F, M1 Slices 0 through 4, Slice 4.5 evaluator
work, and Slice 5 WP1 have detailed accepted plans, ADRs, and implementation
records under their respective indexes. Those documents preserve evidence and
chronology; they are not the default path for current implementation.

Private held-out evaluation is deferred with no valid current product verdict.
Protocol `/5` is retired unqualified. Frozen protocol `/4` is historical
bounded-regression tooling only. The current evaluator inventory and exact
version-axis boundary live in
[product-evaluator-boundary.md](evaluation/product-evaluator-boundary.md) and
[repository-evaluation-authority.v1.json](evaluation/repository-evaluation-authority.v1.json).

## Change discipline

- Give requirements stable identifiers.
- Link ADRs and evaluation cases back to requirements.
- Cite external technical claims with source, version, and retrieval date.
- Preserve superseded decisions and implementation history in their owning
  records rather than duplicating chronology in navigation files.
- Record uncertainty, unsupported surfaces, and coverage gaps explicitly.
- Update `current-state.md` when the execution handoff changes.
- Update this index when an authoritative navigation category is added.
