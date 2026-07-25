# Architecture overview

Status: Draft  
Last reviewed: 2026-07-25

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
- claims and provenance;
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

- Mod Organizer 2;
- game/root filesystem;
- LOOT;
- Mutagen or another Bethesda semantic layer;
- xEdit and other external tools;
- Nexus and approved documentation sources;
- LLM providers;
- generated-output systems;
- log formats.

Unsupported or failed adapters produce coverage records rather than invented
fallback data.

These responsibilities and adapter boundaries are architectural groupings, not
categories of mods or affected Skyrim areas. Once the taxonomy resulting from
RQ-036 is accepted, components and adapters must declare the taxonomy coverage
they enable without assuming that one adapter owns one game area.

## Conceptual flow

```text
Approved sources
  -> Evidence acquisition runs
  -> Reusable source-bound claims ------------------+
                                                    |
Selected MO2 profile                               |
  -> Installation snapshot + context + config      |
  -> Deterministic tools and local indexes --------+
                                                    |
                                                    v
                           Candidate interactions and evidence graph
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
- Mutagen.Bethesda for Bethesda records and archives;
- SQLite evidence store;
- React/TypeScript UI;
- thin, hardened Electron shell;
- narrow versioned IPC;
- LOOT and other tools through adapters.

Rationale so far:

- C#/.NET is worth investigating because Mutagen.Bethesda appears to cover
  relevant Bethesda data domains, subject to RQ-004 verification;
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
