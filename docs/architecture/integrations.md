# Integration boundaries

Status: Draft  
Last reviewed: 2026-07-25

ADR-0006 accepts the high-level external-application and bundled-dependency
posture. ADR-0007 excludes xEdit from every Infinium boundary. ADR-0008 through
ADR-0011 accept the Wave B MO2, runtime/Mutagen, snapshot, and conditional
LOOT/libloot semantic boundaries. Exact process topology, binding/IPC,
implementation operations, and supported surfaces remain subject to their
owning plans and qualification gates.

The integration sections below organize external/system boundaries. They are
not an exhaustive taxonomy of technical modification surfaces, mod types, or
affected game areas. Integration capability and coverage must map to the
accepted taxonomy resulting from RQ-036, including cross-adapter and
unknown/unsupported areas.

## Adapter contract requirements

Every integration declares:

- supported versions and target scope;
- capabilities;
- required permissions;
- inputs and outputs;
- product- and tool-owned cache/temp behavior and proof that the operation does
  not mutate user setup state;
- cancellation behavior;
- provenance/version information;
- errors and partial results;
- coverage semantics;
- cache dependencies;
- licensing/distribution constraints.

Mock or fabricated success data is prohibited in production paths.
An operation that may change MO2, mod, profile, game, configuration, generated
output, or other user setup state is ineligible through M4.

## Mod Organizer 2

MO2 is a required user-installed application and shall not be bundled,
downloaded, installed, replaced, or updated by Infinium. Setup and settings
must attempt supported detection, permit explicit path confirmation/override,
validate identity and version, and report unsupported or misconfigured state.

Required capabilities:

- discover instances and profiles;
- select one profile explicitly;
- read the exact instance's MO2 saved selection as a suggestion only;
- read enabled mods, priorities, plugins, load order, archives, and relevant
  profile configuration;
- reconstruct or obtain authoritative effective file-provider state;
- map mod directories and metadata to source identities;
- represent hidden/deleted/unmanaged state.

ADR-0008 selects version-pinned quiescent deterministic reconstruction for
MO2 `2.5.2`, requires explicit instance/profile binding, and rejects production
execution through the user's real MO2 or direct USVFS. EVAL-0051 and EVAL-0046
remain qualification gates for exercised surfaces.

## Skyrim and filesystem

Required capabilities:

- detect the exact initial Steam Windows x64 `1.6.1170.0` runtime through the
  ADR-0009 support manifest;
- inspect Data and game root;
- identify SKSE/native/root components;
- index loose and archive providers;
- read relevant configuration and generated output;
- detect mid-scan changes.

## LOOT

When a milestone claims LOOT-backed coverage, Infinium must consume LOOT's
mature metadata and semantics instead of recreating them. LOOT remains an
optional user-installed application. ADR-0011 rejects LOOT
`0.28.0`/`0.29.1` application automation as the analysis boundary and selects
a narrow bundled libloot `0.29.6` semantic core for that coverage. Exact
binding/worker operations must pass EVAL-0053 and EVAL-0046 before use.

Masterlist and prelude are managed, versioned data rather than a fixed bundled
payload by default. Provenance must distinguish curated LOOT metadata and
diagnostics, exact data revisions, and user-supplied userlist rules.

## Bethesda semantic layer

ADR-0009 selects `Mutagen.Bethesda.Skyrim` `0.54.2` as the initial bounded
semantic dependency for positively allowlisted plugin/record/link/override
shapes and low-level BSA reads over authoritative inputs. Its standard
environment, automatic load-order/archive applicability, and localized-string
lookup are not Infinium authority.

ADR-0007 excludes xEdit. Record-semantic qualification must use independently
specified first-party fixture expectations and may not accept an expected
result solely because the Mutagen code path under test produced it.

## Documentation providers

Provider adapters may cover:

- Nexus API;
- official repositories;
- author sites;
- bundled/local documentation;
- approved broader web search.

They must follow API and scraping policies, identify source revision/freshness,
and expose unavailable content as a gap.

The Nexus adapter is additionally constrained by ADR-0005: it may use only
documented supported API operations, may privately retain permitted source
material through the useful dependent work defined by the accepted RQ-031
disposition, and may not substitute page access for an unsupported content
surface. A negative Nexus response or material policy change stops the affected
path pending review.

## LLM providers

Internal contracts are provider-neutral. GPT is the first reference provider.
Through M4, authenticated or billable calls use authorization supplied by the
user for the explicitly selected provider/account. Credential-free local
providers may operate under their declared contracts; adapters expose no
project-funded or shared-project credential fallback.
Each provider advertises:

- supported models/features;
- structured output/tool capability;
- batch support;
- token/cost accounting;
- finite reservation inputs for supported consumptive hard-limit dimensions;
- billing finality and reconciliation latency;
- rate limits;
- historical/remaining usage visibility;
- retention/privacy properties;
- cancellation behavior.

Unsupported quota, enforceable-bound, or billing data must be represented
explicitly.

## Generators and logs

Named adapters should be developed only with known formats and evaluation
fixtures. Unknown systems receive generic evidence and coverage gaps rather
than guessed semantics.
