# Architecture overview

Status: Accepted
Disposition: synthesis; actively maintained
Last reviewed: 2026-08-10
ADR-0015 through ADR-0023 accept the complete Wave E persistence, lifecycle,
application-stack, process, IPC, credential, security, continuity, and budget
architecture. Exact implementation status and evidence live in
[current project state](../current-state.md) and the owning slice records;
this synthesis does not duplicate that moving handoff. Provider dispatch,
generic reversion proof, controlled-real execution, and the frontend remain
later slices or milestones. Dapr and ADR-0024's Codex proposal are rejected.
This document maps the selected decomposition without treating partial M1
delivery as complete architecture conformance.

## Required responsibilities and selected separation

The responsibilities below follow from product requirements and accepted
ADR-0015 through ADR-0023. Exact implementation details remain bounded by the
accepted M1 plan and the later owning milestone plans.

### Presentation

Responsibilities:

- profile selection;
- scan configuration;
- progress and job control;
- readiness summary;
- case/finding exploration;
- assumptions and dispositions;
- exports and diagnostic review.

The presentation layer does not determine game state, parse plugins, call
privileged local operations directly, or become the source of analytical truth.

### Analysis execution

Responsibilities:

- installation snapshots, semantic analysis contexts, and scan configurations;
- analysis runs and independent evidence-acquisition runs;
- tool and source adapters;
- indexes and semantic analyzers;
- job/checkpoint orchestration;
- LLM investigation;
- evidence, findings, cases, and coverage;
- validation and export generation.

Long-running, CPU-heavy, IO-heavy, crash-prone, or privileged work belongs
outside the UI rendering/event-loop execution boundary. ADR-0018 accepts a
standalone per-user coordinator, bounded workers, and a one-shot
credential/provider-helper process role. Initial OpenAI work uses the direct
Responses adapter under ADR-0013; the rejected Codex proposal adds no provider
process. ADR-0019 through ADR-0021 accept the applicable transport, credential,
and security mechanisms.

### Evidence persistence

Responsibilities:

- logically immutable installation snapshots;
- versioned analysis contexts;
- analysis runs and resolved input manifests;
- evidence-acquisition runs and their resolved source inputs;
- versioned scan configurations;
- cached evidence and dependencies;
- permitted private source bodies/excerpts while required by configured
  extraction, analysis, synthesis, provenance, audit, replay, or refresh;
- claims, versioned taxonomy assignments, and provenance;
- logical finding/case identities and revisions/lineage;
- candidates, hypotheses, and recommendations;
- assumptions, dispositions, and suppression;
- versioned readiness policies and evaluations with their review-state inputs;
- review annotations, symptom reports, and export artifacts;
- jobs/checkpoints;
- evaluation and audit artifacts.

The store must support dependency-aware invalidation, paginated exploration, and
historical auditability with explicit replayability status.

### Integrations

Bounded adapters isolate:

- user-installed Mod Organizer 2;
- game/root filesystem;
- optional user-installed LOOT application/configuration;
- the accepted bounded Mutagen `0.54.2` Bethesda semantic layer;
- the accepted conditional libloot `0.29.6` semantic boundary;
- other explicitly accepted external tools;
- Nexus and approved documentation sources;
- LLM providers;
- generated-output systems;
- log formats.

Unsupported or failed adapters produce coverage records rather than invented
fallback data.

These responsibilities and adapter boundaries are architectural groupings, not
categories of mods or affected Skyrim areas. Components and adapters must
declare the coverage they enable under the accepted
[Skyrim SE mod-impact taxonomy](../product/mod-impact-taxonomy.md) without
assuming that one adapter owns one affected area.

## Conceptual flow

```text
Approved sources
  -> Evidence acquisition runs
  -> Reusable source-bound claims ------------------+
                                                    |
Selected MO2 profile                               |
  -> Installation snapshot + context + config      |
  -> Typed local indexes and causal joins ---------+
  -> Deterministic mandatory candidates -----------+
                                                    |
                                                    v
             Canonical candidate interactions and typed evidence graph
                                      |
                           staged, evidence-bound routing
                                      |
                                      +----> Targeted LLM investigations
                                      |
                                      v
Findings and causally grouped cases
        |
        v
Readiness, remediation, validation, and exports
```

## Current accepted Wave E architecture

The following architecture is research-complete and accepted:

- a .NET 10 UI-independent domain engine and human-readable CLI;
- the already accepted pinned Mutagen.Bethesda dependency for bounded Bethesda
  records and low-level archives;
- SQLite as the authoritative relational store, paired with a
  coordinator-owned content-addressed payload store and rebuildable
  projections;
- a standalone, non-elevated, per-user coordinator as the sole Infinium
  database, authorization, query, and publication authority;
- a thin application-owned transactional SQLite lifecycle and bounded local
  scheduler under accepted ADR-0016, with no external workflow authority;
- bounded general workers that stage outputs for coordinator validation and
  adoption, without credentials or direct authoritative-store access;
- React/TypeScript presentation hosted by a minimal WPF/WebView2 Evergreen
  desktop shell;
- gRPC/HTTP2 over current-user-restricted Windows named pipes for
  application/coordinator and coordinator/worker communication, with distinct
  endpoints and role-specific contracts;
- a coordinator-launched one-shot helper that alone performs native credential
  entry/storage access and an authorized reusable-secret provider request;
- schema-constrained, usage-priced direct Responses API operation through a
  user-supplied Platform API key; and
- user-installed MO2/LOOT discovery plus the already accepted deterministic
  MO2 reconstruction and conditional libloot semantic boundaries.

RESEARCH-0036 through RESEARCH-0046 provide the evidence, rejected alternatives,
and owner dispositions.
If the WPF/WebView2 qualification prototype fails, ADR-0017 must be reopened;
no fallback stack is selected automatically. Prior research identified Electron
and Avalonia as the principal reconsideration candidates, while Tauri adds an
unnecessary language/toolchain boundary for the selected engine. The graphical
shell is replaceable and must not be required for M1 engine or CLI operation.

Gate E is met at the M0 architecture/design layer because every required Wave
E ADR is accepted. ADR-0024 is not required because it was rejected.
Acceptance selects a design, not implementation conformance or an evaluation
pass.

## Architecture qualities

The chosen architecture must support:

- exactness before inference;
- candidate-first analysis without naïve all-pairs model comparison;
- snapshot-bound typed indexes, causal joins, canonical participant identity,
  explicit negatives/gaps, and score-independent mandatory lanes;
- taxonomy-backed routing and coverage without treating taxonomy labels as
  causal truth;
- explicit authority boundaries;
- long-running resumable jobs;
- high-end profile scale;
- validated incremental caching;
- inspectable provenance;
- offline local analysis;
- secure handling of credentials and untrusted documentation;
- replacement of the UI shell without rewriting the domain engine;
- modular game-specific analyzers without speculative cross-game abstraction;
- provider-independent domain truth with capability-profiled LLM adapters,
  initially OpenAI-first without a second-provider parity gate.
