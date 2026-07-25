# Scope and milestones

Status: Accepted  
Last reviewed: 2026-07-25

## Supported product scope

The initial supported target is:

- one explicitly pinned Skyrim Special Edition runtime version, initially the
  version installed in the creator's reference setup when support is first
  pinned;
- one explicitly selected Mod Organizer 2 profile;
- manually initiated analysis;
- Windows desktop;
- local-first deterministic analysis;
- optional user-selected LLM provider for LLM-backed analyzers and
  acquisition/extraction operations, using the user's own provider/account
  when authentication or billing is required, with GPT as the initial
  reference provider;
- read-only authority.

An automatically suggested current or last-selected MO2 profile may be offered
after MO2 semantics are researched, but selection must remain explicit.

## Milestone terminology

To avoid using "MVP" for several different things, milestone identifiers are
normative:

- **M0 — Documentation and research foundation**
- **M1 — Backend semantic proof**
- **M2 — Frontend workflow proof**
- **M3 — Trusted personal preflight**
- **M4 — Public-facing MVP**

"MVP" without an identifier should be avoided. M3 is the point at which the
creator can rely on the tool for a personal playthrough. M4 is a separate,
conditional release milestone.

## Development scale

Testing progresses through:

1. atomic synthetic fixtures;
2. small controlled integration profiles;
3. small real-mod profiles;
4. medium representative profiles;
5. the creator's large real profile;
6. upper-bound stress profiles.

A list near 2,000 enabled mods, 2,500 plugins, and millions of file entries is
a supported high-end target rather than the normal unit-test baseline.

## M0: Documentation and research foundation

Goal: establish reviewed product truth and answer technical questions before
selecting an implementation architecture.

Deliverables:

- reviewed product documentation;
- legacy assessment;
- research investigations with dated sources;
- accepted foundational ADRs;
- proposed and compared system architectures;
- an accepted backend-proof plan.

## M1: Backend semantic proof

Goal: prove the evidence pipeline and one general semantic mechanism without a
polished frontend.

Initial proof scenario:

- an appearance-focused mod unintentionally reverts unrelated NPC behavior;
- record and facegen provenance are reconstructed;
- documentation establishes mod purpose;
- a patch may be missing, ineffective, obsolete, or overwritten;
- the output forms one inspectable case with impact, symptoms, remediation or
  validation, provenance, and uncertainty.

The mechanism must be generic scope-incongruent reversion, not an NPC-specific
product rule. Synthetic positive and negative cases come first, followed by
carefully selected real mods. At least one materially different non-NPC
technical surface or affected game area must validate the general mechanism
before it is considered proven. The exact classification follows RQ-036;
“non-NPC” is only the current proof-planning shorthand.
M1 may use provisional labels while fixtures are being designed, but it cannot
be accepted until the taxonomy resulting from RQ-036 is accepted and the
surfaces/areas exercised by the proof are mapped to it.

The backend proof emits human-readable CLI output and a versioned JSON artifact.
It must obey every foundational product, authority, evidence, provenance, and
safety constraint applicable to the behavior it exercises even where broader
completion of that requirement is targeted at M3.

## M2: Frontend workflow proof

Goal: prove the finding-centric user experience against a stable backend
contract.

M2 exercises the expected workflow end to end with representative M1
capabilities and stable contracts. It does not imply that the analyzer breadth,
scale, or reliability required by M3 is already complete.

Expected scope:

- profile selection;
- granular scan configuration;
- progress, time, and cost;
- summary and readiness;
- supported-case and lead-only investigation queues and detail views;
- evidence expansion;
- finding dispositions;
- assumptions;
- focused mod view;
- targeted verification.

## M3: Trusted personal preflight

Goal: expand analyzer coverage and reliability until the creator can rely on
the product before a real playthrough.

Required capabilities include:

- exact effective-installation snapshot;
- deterministic tool integration;
- several semantic record/system families;
- patch effectiveness;
- documentation intelligence;
- grounded novel hypotheses;
- native/runtime and asset coherence;
- generated-output coverage;
- update/install/removal safety;
- scan history and validated caching;
- cost controls and resumable jobs;
- creator-profile and upper-bound scale validation;
- evaluation against synthetic and real cases.

Configuration and installer-choice analysis remain targeted Should items for M3
and do not gate the milestone unless explicitly promoted by an accepted
milestone plan.

The M3 capability list is a delivery inventory, not a taxonomy of mod types or
affected game areas. The accepted RQ-036 taxonomy and coverage map will define
how breadth across technical surfaces, game systems/content areas,
consequences, and effect extents is measured.

## M4: Public-facing MVP

This milestone is conditional on personal success and potential adoption.

Additional concerns include:

- onboarding and accessibility;
- packaging and updates;
- public-release hardening and migration behavior for secure credential
  storage;
- provider-configuration onboarding and supportability;
- API registration and policy compliance;
- public-facing unsupported-environment guidance and recovery;
- privacy- and source-policy-reviewed diagnostic exports;
- supportability.

## Explicitly after M4

- write-capable remediation or autonomous setup changes;
- patch generation;
- other mod managers;
- other Skyrim runtimes, editions, and total conversions;
- other games;
- continuous monitoring;
- custom in-game instrumentation;
- product-initiated MO2/game launch for tracked test sessions;
- save-to-installation-snapshot association;
- community/shared compatibility service;
- full documentation revision browser;
- prospective installer-choice recording;
- detailed local-resource controls;
- game-performance recommendations and automated in-game benchmarking;
- global general-purpose chat.
