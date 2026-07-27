# Architecture Decision Records

Status: Draft  
Last reviewed: 2026-07-26

ADRs preserve technical decisions and their rationale. They are append-only:
accepted records are superseded rather than rewritten to hide prior decisions.

## Index

| ADR | Status | Decision |
|---|---|---|
| [ADR-0001](ADR-0001-evidence-authority-boundary.md) | Accepted | Authority is claim-type-specific; LLM use is bounded |
| [ADR-0002](ADR-0002-snapshot-context-binding.md) | Accepted | Separate immutable snapshots, semantic contexts, run configurations, and replay |
| [ADR-0003](ADR-0003-read-only-authority.md) | Accepted | Exclude setup-mutation capabilities through M4 |
| [ADR-0004](ADR-0004-initial-target-scope.md) | Accepted | Avoid multi-manager/runtime abstractions initially |
| [ADR-0005](ADR-0005-nexus-supported-api-analysis.md) | Accepted | Proceed with bounded supported Nexus API analysis under an explicit risk decision |
| [ADR-0006](ADR-0006-gpl-product-and-tool-dependency-boundary.md) | Accepted; partially superseded | Use GPLv3-family licensing with user-installed applications and gated bundled-library candidates; xEdit provisions superseded by ADR-0007 |
| [ADR-0007](ADR-0007-exclude-xedit-from-infinium.md) | Accepted | Exclude xEdit from product, development, dependency, integration, and evaluation scope |
| [ADR-0008](ADR-0008-mo2-profile-effective-state-and-local-identity.md) | Accepted | Use a version-pinned, quiescent MO2 2.5.2 reconstruction with explicit profile binding and separate physical/source identity |
| [ADR-0009](ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md) | Accepted | Pin the initial Steam 1.6.1170 runtime and Mutagen 0.54.2 semantic boundary with independent field qualification |
| [ADR-0010](ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md) | Accepted | Use canonical structural manifests, scoped SHA-256 identities, and dependency-specific invalidation/reuse |
| [ADR-0011](ADR-0011-loot-semantic-and-managed-data-boundary.md) | Accepted | Reject current LOOT application automation and use a narrow pinned libloot/data boundary when LOOT coverage is delivered |

No application stack, process topology, database, or IPC mechanism has been
accepted. ADR-0008 through ADR-0011 accept the Wave B integration and semantic
boundaries, but their exact implementations and supported surfaces still
require the named conformance gates.

The product baseline was accepted on 2026-07-25 and its requirements are now
authoritative. ADR-0001 through ADR-0011 were accepted on 2026-07-25 and now
govern their declared architectural constraints. ADR-0007 supersedes only
ADR-0006's xEdit-specific provisions.

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

The accepted ADRs cover the architectural decisions that are ripe at this
stage:

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
- ADR-0005 records the owner's bounded-use interpretation and accepted policy
  risk for supported Nexus API analysis while continuing to prohibit scraping,
  bulk/rehost behavior, unsupported interfaces, and model training.
- ADR-0006 selects GPLv3-family licensing; keeps MO2 and LOOT user-installed;
  and established the initial gated Mutagen, libloot, USVFS, LOOT-data, and
  first-party-helper candidate posture. ADR-0008 rejects direct USVFS operation
  for the initial product, ADR-0009/ADR-0011 select the Mutagen/libloot
  boundaries, and ADR-0007 supersedes the earlier xEdit provisions.
- ADR-0007 excludes xEdit from Infinium's product, development, dependency,
  integration, and evaluation boundaries and replaces its proposed
  ground-truth role with parser-independent first-party fixture validation.
- ADR-0008 accepts the MO2 2.5.2 quiescent reconstruction, explicit
  profile-binding, local-identity, source-mapping, and installer-history gap
  boundaries.
- ADR-0009 accepts the initial exact Steam `1.6.1170.0` runtime-support
  manifest and pinned Mutagen `0.54.2` semantic dependency while keeping
  record/field breadth, archives, and strings behind explicit qualification.
- ADR-0010 accepts the structural-manifest, scoped strong-content,
  dependency-closure, invalidation, and reuse model.
- ADR-0011 accepts the rejection of current LOOT application automation and
  the conditional narrow libloot `0.29.6` plus managed-data boundary.

Other accepted decisions currently belong in product, taxonomy, evaluation, or
milestone documents rather than requiring duplicate ADRs.

## Expected future ADR subjects

Research is likely to produce ADRs for:

- evidence persistence and deletion mechanisms beyond ADR-0010's snapshot,
  dependency, and cache-validity boundary;
- durable jobs/checkpoints and process/data-query boundaries;
- application stack, UI/worker separation, and IPC where applicable;
- LLM provider/authentication, credential, security, and enforceable-cost
  boundaries;
- documentation-source acquisition, provider-transmission, retention, and
  redistribution mechanisms within ADR-0005's accepted Nexus boundary;
- storage/query mechanics that implement RQ-035's accepted logical
  typed-index, causal-join, and interaction-graph design;
- packaging, signing, updates, and distribution.

ADR-0006 accepts the licensing and high-level tool-dependency boundary.
ADR-0009 and ADR-0011 now select the initial Mutagen/libloot versions and
semantic boundaries. Packaging, signing, update mechanisms, exact
binding/process operations, and future dependency-version advancement remain
future decisions.

RQ-031 already establishes the accepted retention, replay, deletion, and
export policy semantics. A future persistence ADR should select the storage,
cache-validity, dependency, and deletion mechanisms that implement that
policy; it must not reopen the accepted useful-analysis retention boundary
without an explicit superseding decision.

RQ-036 produced an accepted product taxonomy rather than an ADR merely because
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
