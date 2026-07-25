# Architecture Decision Records

Status: Draft  
Last reviewed: 2026-07-25

ADRs preserve technical decisions and their rationale. They are append-only:
accepted records are superseded rather than rewritten to hide prior decisions.

## Index

| ADR | Status | Decision |
|---|---|---|
| [ADR-0001](ADR-0001-evidence-authority-boundary.md) | Accepted | Authority is claim-type-specific; LLM use is bounded |
| [ADR-0002](ADR-0002-snapshot-context-binding.md) | Accepted | Separate immutable snapshots, semantic contexts, run configurations, and replay |
| [ADR-0003](ADR-0003-read-only-authority.md) | Accepted | Exclude setup-mutation capabilities through M4 |
| [ADR-0004](ADR-0004-initial-target-scope.md) | Accepted | Avoid multi-manager/runtime abstractions initially |

No stack, process topology, database, IPC, or integration mechanism has been
accepted.

The product baseline was accepted on 2026-07-25 and its requirements are now
authoritative. ADR-0001 through ADR-0004 were accepted on 2026-07-25 and now
govern their declared architectural constraints. No implementation mechanism
is accepted merely because it appears as a leading candidate elsewhere.

A constraint ADR derived directly from an accepted product requirement may be
proposed without external technical research when it selects no implementation
mechanism. It must identify its source requirements and still requires
individual review and acceptance. ADRs selecting a stack, integration,
storage/process model, or other technical mechanism follow the full research
workflow below and compare realistic alternatives.

## What warrants an ADR

Use an ADR when a decision:

- selects or rejects a durable system structure, mechanism, authority boundary,
  or dependency after meaningful alternatives exist;
- affects several components, contracts, data lifecycles, security boundaries,
  or future implementation choices;
- is costly or risky to reverse, or would be difficult to reconstruct later
  from code and requirements alone;
- has material tradeoffs and consequences that future contributors must
  understand;
- resolves an open technical question or deliberately constrains how accepted
  product behavior may be implemented.

Do not create an ADR merely to duplicate:

- target users, workflows, feature scope, priorities, milestones, or other
  product requirements;
- taxonomy values and user-facing classifications that belong in accepted
  product specifications;
- evaluation fixtures, thresholds, or acceptance criteria;
- research findings that have not yet produced a selected decision;
- temporary implementation sequencing that belongs in a milestone plan.

One product requirement may need no ADR, one ADR may implement several
requirements, and a later researched mechanism may require several ADRs.

## Current coverage assessment

The four accepted ADRs cover the architectural decisions that are ripe before
technical research:

- ADR-0001 covers typed evidence authority, the deterministic/LLM boundary, and
  the constraint that LLM investigation remains grounded in selected
  candidates rather than becoming local-state authority.
- ADR-0002 covers immutable snapshots/contexts/runs, separate acquisition
  ownership, validated reuse, replayability, and readiness/review-state
  separation.
- ADR-0003 covers the setup-mutation and product-write authority boundary
  through M4.
- ADR-0004 covers the initial manager/game/runtime/platform target, manual
  initiation, and avoidance of premature cross-target abstractions.

Other accepted decisions currently belong in product, taxonomy, evaluation, or
milestone documents rather than requiring duplicate ADRs.

## Expected future ADR subjects

Research is likely to produce ADRs for:

- authoritative MO2/effective-state acquisition;
- Bethesda semantic parsing and xEdit ground-truth boundaries;
- LOOT integration;
- evidence persistence, retention, cache validity, and dependency modeling;
- durable jobs/checkpoints and process/data-query boundaries;
- application stack, UI/worker separation, and IPC where applicable;
- LLM provider/authentication, credential, security, and enforceable-cost
  boundaries;
- documentation-source acquisition and redistribution boundaries;
- candidate indexing/interaction-graph mechanics if RQ-035 selects a durable
  cross-cutting design;
- packaging, signing, updates, and distribution.

RQ-036 produces an accepted product taxonomy rather than an ADR merely because
it defines classifications. A later storage, schema-evolution, or routing
mechanism chosen to implement that taxonomy may warrant its own ADR.

## ADR workflow

For ADRs selecting a technical mechanism:

1. Record an open question in research.
2. Perform a dated investigation with primary evidence.
3. Write a proposed ADR comparing realistic alternatives.
4. Review consequences against product requirements and evaluation needs.
5. Accept, reject, or defer.
6. Link implementation plans and verification back to the ADR.

Use [ADR-template.md](ADR-template.md).
