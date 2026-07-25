# Integration boundaries

Status: Draft  
Last reviewed: 2026-07-25

No integration mechanism is accepted until researched.

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

Required capabilities:

- discover instances and profiles;
- select one profile explicitly;
- determine current/last-selected profile if authoritative;
- read enabled mods, priorities, plugins, load order, archives, and relevant
  profile configuration;
- reconstruct or obtain authoritative effective file-provider state;
- map mod directories and metadata to source identities;
- represent hidden/deleted/unmanaged state.

Open question: companion plugin, supported API, profile-file reconstruction,
USVFS integration, or a combination.

## Skyrim and filesystem

Required capabilities:

- detect the single supported runtime;
- inspect Data and game root;
- identify SKSE/native/root components;
- index loose and archive providers;
- read relevant configuration and generated output;
- detect mid-scan changes.

## LOOT

Infinium must consume LOOT's mature metadata and analysis instead of
recreating it. Research must compare CLI, libloot, or another supported
interface and ensure masterlist/userlist/prelude metadata is actually loaded.
Provenance must distinguish curated LOOT metadata and diagnostics from
user-supplied userlist rules.

## Bethesda semantic layer

The leading candidate is Mutagen.Bethesda for plugin, override, linking, and
archive analysis. Coverage, performance, version behavior, and gaps require
targeted verification.

xEdit remains a reference/optional external analyzer where it provides unique
checks or ground truth.

## Documentation providers

Provider adapters may cover:

- Nexus API;
- official repositories;
- author sites;
- bundled/local documentation;
- approved broader web search.

They must follow API and scraping policies, identify source revision/freshness,
and expose unavailable content as a gap.

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
