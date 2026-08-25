# Infinium documentation

Status: Accepted
Disposition: Active navigation

Last reviewed: 2026-08-25

This directory is the authoritative entry point for the rebuilt Infinium
project. It records product intent, accepted decisions, current execution,
research, evaluation standards, and implementation evidence without treating
the abandoned code or historical evaluator work as current instructions.

## Core entry

1. [Current project state](current-state.md)
2. [Development execution policy](execution-policy.md)

Then load only the task-specific product, architecture, planning, evaluation,
or research authority below. The compact active-slice entry links the full
plan and record without making either large historical document mandatory for
unrelated work.

## Current execution

The single current handoff is [current-state.md](current-state.md). Do not copy
its package status into navigation files or infer current work from historical
plans and records.

Ordinary development follows the
[development execution policy](execution-policy.md): implement a
coherent vertical package, use focused checks while developing, perform one
consolidated review, correct the same working candidate, then run the complete
floor and bind once when review-ready. Routine defects do not create a
correction-budget stop. Strict freeze, no-retry, and terminal-stop rules remain
scoped to the fixture/oracle, external-effect, evaluator, or other exceptional
operation that explicitly requires them.

## Supporting document map

- Architecture: [decision index](architecture/decisions/README.md),
  [overview](architecture/overview.md),
  [data and trust model](architecture/data-and-trust-model.md),
  [jobs/caching/snapshots](architecture/jobs-caching-and-snapshots.md),
  [integration boundaries](architecture/integrations.md), and
  [security/privacy](architecture/security-and-privacy.md)
- Evaluation: [evaluation index](evaluation/README.md),
  [evaluation strategy](evaluation/evaluation-strategy.md),
  [case catalog](evaluation/case-catalog.md),
  [product/evaluator authority boundary](evaluation/product-evaluator-boundary.md),
  [evaluator history](evaluation/evaluator-history.md),
  [product-conformance verification profile](evaluation/product-conformance-verification-profile.md),
  [fixture guidelines](evaluation/fixture-guidelines.md), and
  [anti-overfitting rules](evaluation/anti-overfitting-rules.md)
- Research: [open questions](research/open-questions.md),
  [source registry](research/source-registry.md),
  [deferred questions and residual risks](research/deferred-question-and-residual-risk-register.md),
  and [investigation procedure](research/investigations/README.md)
- Planning: [plan policy](plans/README.md),
  [work-breakdown notation](plans/work-breakdown-notation.md),
  [milestone planning boundary](plans/milestones/README.md), and the
  [completed post-M1 cleanup transition](plans/transitions/post-m1-cleanup/README.md)
- Governance: [functional implementation naming](governance/functional-implementation-naming.md)
- Product: [product definition](product/product-definition.md),
  [requirements](product/requirements.md),
  [mod-impact taxonomy](product/mod-impact-taxonomy.md),
  [workflows](product/workflows.md), [domain model](product/domain-model.md),
  [severity/confidence/coverage](product/severity-confidence-and-coverage.md),
  [analysis catalog](product/analysis-catalog.md),
  [scope/milestones](product/scope-and-milestones.md), and
  [candidate input and deterministic expansion](product/candidate-input-and-expansion.md)
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
implementation remains recoverable through Git commit `7dd3da6`. Superseded
evaluator-development staging is consolidated with completed M0/M1 development
history in `../infinium-development-history-archive/` commit
`6f8976db6c560456201a9166caf4f36506be5477`. Retired public protocol `/4` code
and its last regression closure are in the separate sibling Git repository
`../infinium-evaluator-archive/`. Do not inspect any archive unless the project
owner explicitly requests the corresponding archaeological review.

M0 research, Waves A through F, and the accepted completed M1 hierarchy through
its final backend closeout were moved there during the post-M1 cleanup. Their
exact transfer evidence is in the cleanup implementation record.
Superseded evaluator-attempt prose
and proof fixtures are summarized in
[Evaluator history](evaluation/evaluator-history.md) and retained by exact Git
blob rather than copied through current navigation.

Independent semantic-oracle qualification is deferred throughout M1 and M2 by
[ADR-0035](architecture/decisions/ADR-0035-defer-independent-semantic-oracle-qualification.md).
No current semantic-oracle package grants a product verdict. Ordinary product
conformance remains mandatory, and evaluation may be reconsidered only after
M2 acceptance at the M3 Evaluation Readiness Gate. Private held-out evaluation
remains deferred with no valid current product verdict.
Protocols `/4` and `/5` are retired and have no active execution or review
role. The current evaluator inventory and exact
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
