# Scope and milestones

Status: Accepted  
Last reviewed: 2026-08-10
Accepted M1 verification amendment, 2026-08-07: ADR-0032 defers the current
private held-out evaluator with no product verdict and makes the accepted
[M1 continuation verification profile](../evaluation/m1-continuation-verification-profile.md)
the public gate for Slices 5-9. Slice 4.5 closeout is accepted. This changes
sequencing and evidence labeling, not the M1 product goal or required cases.
Slice 5 uses staged work-package-owned fixtures, with comprehensive
cross-stage evidence assembled only after its producing behavior exists.
Current execution status lives only in [current project state](../current-state.md).

## Supported product scope

The initial supported target is:

- Steam Skyrim Special Edition for Windows x64 runtime `1.6.1170.0`, gated by
  the exact versioned support-manifest identity accepted in ADR-0009;
- one explicitly selected Mod Organizer 2 profile;
- user-installed MO2 as a required external application; a user-installed
  LOOT application may supply configuration/userlist discovery or a later
  explicitly accepted application capability, while the conditional
  libloot/data semantic boundary does not require the LOOT executable;
- manually initiated analysis;
- Windows desktop;
- local-first deterministic analysis;
- optional OpenAI-backed analyzers and acquisition/extraction operations using
  the user's own provider/account when authentication or billing is required;
  later providers are permitted but no second-provider or feature-parity gate
  applies through M1;
- read-only authority.

The exact instance's MO2 saved selection may be offered as a validated
suggestion only; selection must remain explicit.

Scans, Nexus/general documentation acquisition, broader web search, and LLM
work remain manually initiated. Configurable nonblocking maintenance of
accepted LOOT managed data may run on startup/interval under an accepted
mechanism, but it cannot start analysis or change immutable inputs already
bound to a run.

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
- historical legacy assessment, subsequently removed from the active tree
  after external archival;
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

The mechanism must be generic scope-incongruent reversion, not a product rule
specific to whichever category supplies the first proof. Synthetic positive
and negative cases come first, followed by carefully selected real mods. Before
the mechanism is considered generic across categories, its first category
proof must be followed by at least one materially different category proof.
The contrast may be along technical surface, affected game area, consequence,
interaction shape, or another relevant accepted taxonomy axis. The currently
qualified candidates happen to use actor/AI/FaceGen and placed-reference/link
semantics; that pair does not permanently define the requirement. Exact
classification uses the accepted
[Skyrim SE mod-impact taxonomy](mod-impact-taxonomy.md).

The backend proof emits human-readable CLI output and a versioned JSON artifact.
It must obey every foundational product, authority, evidence, provenance, and
safety constraint applicable to the behavior it exercises even where broader
completion of that requirement is targeted at M3.
If it exercises an external application, its configuration contract shall
accept an explicitly supplied validated path and report unavailable
capabilities without silently changing scope.

M1 is proved through public contract/schema conformance, independently
expected public fixtures, model-derived mutation and metamorphic checks,
determinism/replay/operational safety, two-domain synthetic generalization,
controlled-real EVAL-0016/EVAL-0017 evidence, and fresh semantic/diff review.
This public conformance package does not establish a private held-out verdict
or the reliability/readiness required by M3. A new held-out evaluator may be
proposed only after Slice 9 during M3 planning around a stable, versioned,
user-meaningful output contract, with independently authorable expectations,
answer-free totality review, separate roles, and a new accepted ADR and plan.
No future protocol identity is selected here.

## M2: Frontend workflow proof

Goal: prove the finding-centric user experience against a stable backend
contract.

M2 exercises the expected workflow end to end with representative M1
capabilities and stable contracts. It does not imply that the analyzer breadth,
scale, or reliability required by M3 is already complete.

Expected scope:

- initial tool detection/confirmation and settings-based path overrides;
- visible external-tool status and resulting capability gaps;
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
affected game areas. The accepted
[Skyrim SE mod-impact taxonomy](mod-impact-taxonomy.md) and its coverage map
define how breadth across technical surfaces, game systems/content areas,
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
