# Architecture Decision Records

Status: Draft  
Last reviewed: 2026-08-01

ADRs preserve technical decisions and their rationale. They are append-only:
accepted records are superseded rather than rewritten to hide prior decisions.

## Index

| ADR | Status | Decision |
|---|---|---|
| [ADR-0001](ADR-0001-evidence-authority-boundary.md) | Accepted | Authority is claim-type-specific; LLM use is bounded |
| [ADR-0002](ADR-0002-snapshot-context-binding.md) | Accepted | Separate immutable snapshots, semantic contexts, run configurations, and replay |
| [ADR-0003](ADR-0003-read-only-authority.md) | Accepted | Exclude setup-mutation capabilities through M4 |
| [ADR-0004](ADR-0004-initial-target-scope.md) | Accepted | Avoid multi-manager/runtime abstractions initially |
| [ADR-0005](ADR-0005-nexus-supported-api-analysis.md) | Accepted; partially superseded | Proceed with bounded Nexus API analysis under an explicit risk decision; interface eligibility superseded by ADR-0012 |
| [ADR-0006](ADR-0006-gpl-product-and-tool-dependency-boundary.md) | Accepted; partially superseded | Use GPLv3-family licensing with user-installed applications and gated bundled-library candidates; xEdit provisions superseded by ADR-0007 |
| [ADR-0007](ADR-0007-exclude-xedit-from-infinium.md) | Accepted | Exclude xEdit from product, development, dependency, integration, and evaluation scope |
| [ADR-0008](ADR-0008-mo2-profile-effective-state-and-local-identity.md) | Accepted | Use a version-pinned, quiescent MO2 2.5.2 reconstruction with explicit profile binding and separate physical/source identity |
| [ADR-0009](ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md) | Accepted | Pin the initial Steam 1.6.1170 runtime and Mutagen 0.54.2 semantic boundary with independent field qualification |
| [ADR-0010](ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md) | Accepted | Use canonical structural manifests, scoped SHA-256 identities, and dependency-specific invalidation/reuse |
| [ADR-0011](ADR-0011-loot-semantic-and-managed-data-boundary.md) | Accepted | Reject current LOOT application automation and use a narrow pinned libloot/data boundary when LOOT coverage is delivered |
| [ADR-0012](ADR-0012-nexus-latest-capable-api-routing.md) | Accepted | Use latest-capable v3, v2 GraphQL, then v1 routing under the owner's API-wide development-risk direction |
| [ADR-0013](ADR-0013-openai-first-llm-capability-boundary.md) | Accepted | Use OpenAI Responses first while keeping authoritative domain truth provider-independent |
| [ADR-0014](ADR-0014-loot-managed-data-refresh.md) | Accepted | Refresh current-compatible LOOT data through immutable validated pair activation |
| [ADR-0015](ADR-0015-authoritative-evidence-persistence-and-payload-storage.md) | Accepted | Use SQLite as the authoritative relational store with coordinator-owned content-addressed payload storage |
| [ADR-0016](ADR-0016-application-owned-durable-run-and-job-lifecycle.md) | Accepted | Use an application-owned transactional SQLite lifecycle ledger and bounded local scheduler; reject external workflow authority for M1 |
| [ADR-0017](ADR-0017-windows-desktop-application-stack.md) | Accepted | Use .NET 10, React/TypeScript, and a minimal WPF/WebView2 Windows desktop host |
| [ADR-0018](ADR-0018-process-and-authority-topology.md) | Accepted | Use a standalone per-user coordinator with bounded workers and a one-shot provider helper |
| [ADR-0019](ADR-0019-local-ipc-and-application-query-contract.md) | Accepted | Use role-separated gRPC/HTTP2 over restricted Windows named pipes with bounded application queries |
| [ADR-0020](ADR-0020-credential-storage-and-provider-dispatch.md) | Accepted | Use Windows Credential Manager and exact-target one-shot provider dispatch |
| [ADR-0021](ADR-0021-desktop-and-local-operation-security-boundary.md) | Accepted | Use deny-by-default renderer, filesystem, process, staging, and diagnostics controls without claiming worker sandboxing |
| [ADR-0022](ADR-0022-finding-and-case-continuity-and-reconciliation.md) | Accepted | Separate immutable occurrences from logical identities and use evidence-bearing append-only reconciliation |
| [ADR-0023](ADR-0023-atomic-cost-ledger-and-hard-budget-enforcement.md) | Accepted | Use atomic multi-scope reservations, single-owned usage, exact price arithmetic, and dispatch fences |
| [ADR-0024](ADR-0024-openai-user-owned-access-modes.md) | Rejected | Reject Codex/ChatGPT-plan integration for the core LLM pipeline; retain direct Responses/API-key access under ADR-0013 |
| [ADR-0025](ADR-0025-m1-openai-model-and-synchronous-responses-profile.md) | Accepted | Use one explicit `gpt-5.6-sol` synchronous Structured Outputs profile for M1 with retained-result replay and drift requalification |
| [ADR-0026](ADR-0026-evaluator-private-fixture-repository-and-delegated-access.md) | Accepted | Store private fixtures in a separate Git repository and permit only purpose-bound fresh-context delegated access with sanitized returns |

ADR-0015 through ADR-0023 are accepted and jointly select the Wave E
persistence, lifecycle, application-stack, process, IPC, credential, security,
continuity, and budget architecture. ADR-0024 is rejected. Gate E is met at
the M0 architecture/design layer; implementation and evaluation conformance
remain pending. ADR-0025 is accepted as the exact M1 live-model profile.
ADR-0026 accepts the cross-repository evaluator-private storage and delegated
agent-access boundary without claiming hostile-process sandboxing or completed
private evaluation.
ADR-0008 through
ADR-0011 accept the Wave B integration and semantic
boundaries, and ADR-0012 accepts the revised Nexus interface/risk boundary,
but their exact implementations and supported surfaces still require the
named conformance gates. ADR-0013 and ADR-0014 accept Wave D's OpenAI and
LOOT-freshness mechanisms without claiming implementation or conformance.

The product baseline was accepted on 2026-07-25 and its requirements are now
authoritative. ADR-0001 through ADR-0011 were accepted on 2026-07-25.
ADR-0012 through ADR-0023 and ADR-0025 were accepted on 2026-07-28, and
ADR-0024 was rejected that day. ADR-0026 was accepted on 2026-08-01. ADR-0007
supersedes only ADR-0006's xEdit-specific
provisions, ADR-0012 supersedes only ADR-0005's API-interface eligibility and
selection provisions, and ADR-0014 supersedes only ADR-0011's managed-data
refresh mechanics.

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
  risk for Nexus API analysis while continuing to prohibit scraping,
  bulk/rehost behavior, and model training. ADR-0012 replaces its
  documented-supported-interface-only constraint with API-wide development
  eligibility and latest-capable v3/v2 GraphQL/v1 routing.
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
- ADR-0012 accepts the owner's API-wide Nexus development-risk posture,
  authenticated GraphQL eligibility, and latest-capable per-content routing.
- ADR-0013 accepts the OpenAI-first Responses/Structured Outputs capability
  boundary, governed hosted-web-search role, and provider-independent domain
  truth without a second-provider parity gate. Rejected ADR-0024 records why
  Codex/ChatGPT-plan access will not amend that direct API execution surface.
- ADR-0014 accepts current-compatible LOOT-data discovery, immutable
  masterlist/prelude pair validation and activation, rollback, freshness
  disclosure, and active/historical run isolation.

Other accepted decisions currently belong in product, taxonomy, evaluation, or
milestone documents rather than requiring duplicate ADRs.

## Wave E ADR coverage

Wave E research produced ADR-0015 through ADR-0023 for:

- evidence persistence and deletion mechanisms beyond ADR-0010's snapshot,
  dependency, and cache-validity boundary;
- durable jobs/checkpoints and process/data-query boundaries;
- application stack, UI/worker separation, and IPC where applicable;
- credential/provider dispatch, local security, and enforceable-cost
  boundaries after ADR-0013's accepted capability decision;
- storage/query mechanics that implement RQ-035's accepted logical
  typed-index, causal-join, and interaction-graph design; and
- finding/case continuity and review-state carryover.

RQ-013, RQ-015 through RQ-018, and RQ-032 through RQ-034 are resolved for M0
by accepted ADR-0015 through ADR-0023. RESEARCH-0046 records the owner's
decision to close the Dapr comparison without a prototype. The Wave E
mechanisms are selected architecture, not implemented or qualified behavior.
ADR-0024 remains in the index as rejected decision provenance.

## Expected future ADR subjects

Future research or implementation qualification may produce ADRs for:

- later OpenAI model routing, lower-cost-tier qualification, and provider
  capability extensions beyond accepted ADR-0025's M1 baseline;
- documentation-source acquisition, provider-transmission, retention, and
  redistribution mechanisms not already governed by the accepted policy
  semantics and persistence boundary;
- stronger worker isolation if an M1 or later operation requires compromise
  containment beyond Job Objects;
- M4 shareable-export, packaging, signing, update, and distribution
  mechanisms; and
- dependency-version advancement or materially expanded native bindings.

ADR-0006 accepts the licensing and high-level tool-dependency boundary.
ADR-0009 and ADR-0011 now select the initial Mutagen/libloot versions and
semantic boundaries. Packaging, signing, update mechanisms, exact
binding/process operations, and future dependency-version advancement remain
future decisions.

RQ-031 already establishes the accepted retention, replay, deletion, and
export policy semantics. ADR-0015 selects the storage,
cache-validity, dependency, and deletion mechanisms that implement that
policy; it does not reopen the accepted useful-analysis retention boundary.

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
