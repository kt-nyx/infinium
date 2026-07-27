# Architecture overview

Status: Draft  
Last reviewed: 2026-07-26

No implementation architecture, process topology, or application stack is
accepted. This document maps required responsibilities and records one leading
candidate decomposition for later research.

## Required responsibilities and proposed separation

The responsibilities below follow from product requirements. Their separation
into processes/components is proposed, not accepted.

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
outside the UI rendering/event-loop execution boundary. Whether that boundary
uses threads, processes, services, or another topology remains a research
decision.

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

## Current leading stack candidate

This candidate is not accepted:

- C#/.NET analysis worker;
- the already accepted pinned Mutagen.Bethesda dependency for bounded Bethesda
  records and low-level archives;
- SQLite evidence store;
- React/TypeScript UI;
- thin, hardened Electron shell;
- narrow versioned IPC;
- user-installed MO2/LOOT discovery plus the already accepted deterministic
  MO2 reconstruction and conditional libloot semantic boundaries.

Rationale so far:

- C#/.NET is worth investigating because the accepted Mutagen.Bethesda
  dependency is native to that ecosystem, while its exact supported shapes
  remain qualification-gated;
- a web frontend offers the desired interaction/design ecosystem;
- analysis can remain independent of the desktop shell;
- Electron may avoid adding another implementation language solely for window
  hosting;
- Avalonia is the all-C# comparison candidate.

Research must compare this candidate with realistic alternatives before an ADR
accepts the stack.

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
- provider-neutral LLM integration;
- inspectable provenance;
- offline local analysis;
- secure handling of credentials and untrusted documentation;
- replacement of the UI shell without rewriting the domain engine;
- modular game-specific analyzers without speculative cross-game abstraction.
