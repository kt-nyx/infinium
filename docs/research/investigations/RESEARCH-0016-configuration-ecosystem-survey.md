# RESEARCH-0016: Configuration ecosystem survey

Status: Completed
Disposition: recommendation accepted by project owner
Date: 2026-07-25
Last reviewed: 2026-07-26
Researcher: Codex agent
Primary question: RQ-021 — Which configuration ecosystems merit named schemas
first?
M0 wave: C — Analysis surfaces, taxonomy, corpus, and candidate scale
Decision enabled: Bounded configuration-analyzer roadmap input, RQ-036
technical-surface evidence, and EVAL-0071 configuration-fixture design

Acceptance note: The project owner accepted this report's bounded
recommendation on 2026-07-25 through
[RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md).
Proposal-era wording below is retained as provenance; no named configuration
analyzer has thereby passed qualification.

## 1. Question and accepted constraints

This investigation asks which materially different Skyrim SE configuration
ecosystems deserve the first versioned, named schema contracts. It does not ask
which configuration-related mod purpose, affected game area, consequence, or
effect extent should become an accepted product taxonomy.

The answer must preserve these accepted boundaries:

- [ANALYSIS-011](../../product/requirements.md) makes configuration a targeted
  M3 `Should`: generic syntax/winner checks, known schemas, documentation rules,
  and targeted unfamiliar-configuration investigation are distinct layers.
- [ANALYSIS-016](../../product/requirements.md) requires every future analyzer
  to declare scope, exclusions, dependencies, evidence and abstention
  thresholds, coverage, scale, maturity, and evaluation links.
- [SCOPE-005](../../product/requirements.md) includes relevant configuration in
  effective installation state while requiring unsupported semantics to remain
  visible gaps.
- [AUTH-001](../../product/requirements.md) and
  [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md)
  prohibit configuration changes through M4.
- [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md)
  makes deterministic systems authoritative for effective configuration
  values. An LLM may interpret supplied unfamiliar configuration evidence but
  cannot redefine an observed winner or value.
- [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md)
  and
  [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
  require immutable input identity, dependency-specific invalidation, and
  provenance-preserving reuse.
- [ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md)
  owns effective loose-file provider reconstruction. A configuration analyzer
  consumes that result; it does not invent a second winner model.
- [ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md)
  requires positively qualified record/form/link semantics rather than
  assuming every textual form reference is resolvable.
- [ADR-0011](../../architecture/decisions/ADR-0011-loot-semantic-and-managed-data-boundary.md)
  already owns LOOT configuration and userlist semantics when LOOT coverage is
  delivered. Those inputs should not be duplicated as a generic configuration
  analyzer.
- the accepted
  [Skyrim SE mod-impact taxonomy](../../product/mod-impact-taxonomy.md) remains
  authoritative for declared-purpose, technical-surface,
  affected-game-area, consequence, and effect-extent classifications.

The M0 plan classifies RQ-021 as Conditional Wave C input. Named configuration
schemas do not block the first backend proof, and full configuration breadth is
explicitly deferred to the appropriate M3 analyzer plans.

## 2. Scope and non-scope

### In scope

- A bounded comparison of materially different static configuration
  mechanisms found in the accepted Skyrim SE/MO2 target.
- Current public primary-source evidence for:
  - MCM Helper JSON and INI configuration;
  - Spell Perk Item Distributor (SPID) distribution files;
  - Keyword Item Distributor (KID) distribution files;
  - Base Object Swapper (BOS) swap files;
  - Open Animation Replacer (OAR) JSON configuration and extensible conditions;
  - base-game/profile INI files; and
  - heterogeneous per-SKSE-plugin INI, TOML, and JSON files.
- A sanitized, read-only census of the user-confirmed private reference profile
  to test whether those mechanisms occur in a large real-used profile.
- Prioritization of named schema work using source quality, deterministic
  observability, interaction value, local occurrence, versionability, and
  fixture feasibility.
- Separation of generic syntax and winner checks from schema validation,
  reference resolution, runtime semantics, documentation claims, and LLM
  interpretation.
- Proposed evaluation and registry follow-up for coordinator review.

### Out of scope

- An exhaustive catalog of every Skyrim configuration framework or SKSE
  plugin.
- A promise that the private reference profile is representative, correct, or
  a gold standard.
- Runtime execution of Skyrim, SKSE, MO2, a framework plugin, or an external
  tool.
- Writing, normalizing, repairing, or generating user configuration.
- Accepting a parser library, application stack, storage schema, or production
  architecture.
- Assigning purpose, game-area, consequence, severity, symptom, or effect
  extent from a file suffix or framework name.
- Inferring exact MCM choices, runtime state, or prior user changes from static
  files.
- Full semantic interpretation of arbitrary per-plugin INI/TOML/JSON.
- LOOT userlist/config semantics, which remain under ADR-0011.
- Root/native compatibility, generated-output semantics, script semantics, or
  installer-history reconstruction owned by adjacent research questions.
- Inspection of the then-present abandoned implementation, now retained only
  in the external maintainer archive and Git history.

## 3. Method and prioritization criteria

The survey used three evidence layers:

1. accepted Infinium authority and effective-state constraints;
2. exact public upstream repository revisions and format specifications; and
3. sanitized path-shape counts from the user-confirmed private reference
   profile.

A candidate moves earlier when:

- the runtime discovers it through a stable, identifiable path or suffix;
- an exact upstream revision defines a machine-readable schema or inspectable
  parser contract;
- deterministic static checks can make useful claims without executing the
  game;
- the configuration can create cross-mod or cross-layer behavior rather than
  only local presentation preferences;
- the reference profile supplies enough varied instances to design positive,
  matched-negative, malformed, duplicate, collision, and unsupported cases;
- the contract can be versioned and fail closed;
- effective provider/winner and referenced-form inputs can be retained with
  exact provenance; and
- a named analyzer can abstain cleanly when extensions or runtime-only
  dependencies exceed its allowlist.

A candidate moves later when:

- the only shared property is a generic file extension;
- the consumer's parser behavior is undocumented or plugin-specific;
- values are meaningful only after live runtime state, custom code, or opaque
  side effects;
- no exact schema or parser revision is available;
- documentation, not the file itself, is the only authority for a value's
  intended meaning; or
- the candidate would duplicate another accepted adapter.

Local occurrence is a prioritization signal, not correctness evidence.

## 4. Sources and exact revisions

All network sources below were retrieved on 2026-07-25 through unauthenticated
public HTTPS or Git operations. No Nexus page, authenticated API, paid provider,
browser session, or prohibited scraping method was used.

The MCM Helper, SPID, KID, and BOS repositories declare MIT licensing. OAR
declares GPL-3.0-or-later with its stated modding/linking exceptions. This
report cites small behavioral facts and immutable source locations; it does not
copy, bundle, modify, or select any upstream code as an Infinium dependency.
Any future reuse of implementation code requires its own dependency,
distribution, notice, and corresponding-source review under ADR-0006. Public
source access and citation do not authorize redistribution of the user's
installed configuration bytes, which remain private and are not retained here.

| Source | Exact identity | Authority and claim supported |
|---|---|---|
| [JSON RFC 8259](https://www.rfc-editor.org/info/rfc8259/) | Internet Standard, December 2017 | Generic JSON syntax and the interoperability risk of duplicate object names; not an application schema |
| [JSON Schema Core 2020-12](https://json-schema.org/draft/2020-12/json-schema-core) | Draft 2020-12 | Machine-readable JSON schema vocabulary used by MCM Helper |
| [TOML 1.0.0](https://toml.io/en/v1.0.0) | Version 1.0.0 | Generic TOML syntax only; not SKSE-plugin semantics |
| [MCM Helper repository](https://github.com/Exit-9B/MCM-Helper/tree/9df6b69348dea57b3247b9f812711384f1113bab) | Release `v1.6.2`, commit `9df6b69348dea57b3247b9f812711384f1113bab`, committed 2026-04-26 | Author-maintained framework source, parser implementation, sample files, and exact release identity |
| [MCM Helper config schema](https://github.com/Exit-9B/MCM-Helper/blob/9df6b69348dea57b3247b9f812711384f1113bab/docs/config.schema.json) | Draft 2020-12 schema at the `v1.6.2` commit | Explicit `MCM\Config\<mod>\config.json` structure, required `modName` and `displayName`, closed unevaluated properties, and reusable definitions |
| [MCM Helper keybind schema](https://github.com/Exit-9B/MCM-Helper/blob/9df6b69348dea57b3247b9f812711384f1113bab/docs/keybinds.schema.json) | Draft 2020-12 schema at the `v1.6.2` commit | Explicit MCM keybind structure; evidence that one framework may have several separately versioned schemas |
| [MCM Helper sample config](https://github.com/Exit-9B/MCM-Helper/blob/9df6b69348dea57b3247b9f812711384f1113bab/data/MCM/Config/SkyUI_SE/config.json) | Sample at the `v1.6.2` commit | Positive schema/fixture seed; not evidence that arbitrary installed configs are correct |
| [SPID repository](https://github.com/powerof3/Spell-Perk-Item-Distributor/tree/bb0c227aaae44a152c347d410669b9d0b6587e04) | Release `7.3.1`, commit `bb0c227aaae44a152c347d410669b9d0b6587e04`, committed 2026-06-17 | Author-maintained runtime source and exact release identity |
| [SPID config discovery](https://github.com/powerof3/Spell-Perk-Item-Distributor/blob/bb0c227aaae44a152c347d410669b9d0b6587e04/SPID/src/LookupConfigs.cpp) | `7.3.1` source | Discovers Data files through the `_DISTR` suffix, loads them as INI, parses recognized entries, and logs failed entries |
| [SPID entry parser](https://github.com/powerof3/Spell-Perk-Item-Distributor/blob/bb0c227aaae44a152c347d410669b9d0b6587e04/SPID/src/Parser.h) | `7.3.1` source | Pipe-delimited component parsing with bounded component counts; proves that generic INI validity is not sufficient semantic validation |
| [KID repository](https://github.com/powerof3/Keyword-Item-Distributor/tree/895df224d4964dc9723460038eb533bfff06d860) | Release `v4.0.6`, commit `895df224d4964dc9723460038eb533bfff06d860`, committed 2026-06-30 | Author-maintained runtime source and exact release identity |
| [KID config parser](https://github.com/powerof3/Keyword-Item-Distributor/blob/895df224d4964dc9723460038eb533bfff06d860/src/LookupConfigs.cpp) | `v4.0.6` source | Discovers `_KID` INI files, splits typed entries into pipe-delimited sections, resolves form/filter/trait/chance fields, and reports parse failures |
| [BOS repository](https://github.com/powerof3/BaseObjectSwapper/tree/a90a1c22b23fb384ddf76203872a83b363c22dd2) | Release `v3.4.1`, commit `a90a1c22b23fb384ddf76203872a83b363c22dd2`, committed 2025-07-15 | Author-maintained runtime source and exact release identity |
| [BOS config discovery](https://github.com/powerof3/BaseObjectSwapper/blob/a90a1c22b23fb384ddf76203872a83b363c22dd2/src/Manager.cpp) | `v3.4.1` source | Discovers `_SWAP` INI files and builds conditional/unconditional runtime swap maps |
| [BOS swap parser](https://github.com/powerof3/BaseObjectSwapper/blob/a90a1c22b23fb384ddf76203872a83b363c22dd2/src/SwapData.cpp) | `v3.4.1` source | Validates base/swap form sets and properties; evidence that syntactically valid INI text can still be semantically invalid |
| [OAR repository](https://github.com/ersh1/OpenAnimationReplacer/tree/eb19107c823d500907cfff8be6f80b4530ebbc99) | `main` commit `eb19107c823d500907cfff8be6f80b4530ebbc99`, committed 2026-06-27; no GitHub release was published at retrieval | Author-maintained current source for JSON/legacy parsing and custom condition APIs |
| [OAR parsing source](https://github.com/ersh1/OpenAnimationReplacer/blob/eb19107c823d500907cfff8be6f80b4530ebbc99/src/Parsing.cpp) | Current source commit | Reads current JSON configuration and legacy condition text through distinct parsing routes |
| [OAR condition API](https://github.com/ersh1/OpenAnimationReplacer/blob/eb19107c823d500907cfff8be6f80b4530ebbc99/src/API/OpenAnimationReplacer-ConditionTypes.h) | Current source commit | Uses RapidJSON values and permits custom condition components supplied by other SKSE plugins |
| [OAR repository README](https://github.com/ersh1/OpenAnimationReplacer/tree/eb19107c823d500907cfff8be6f80b4530ebbc99#readme) | Current source commit | Defines OAR as a configurable-condition animation replacement framework and explicitly describes third-party extensibility |
| [MO2 repository](https://github.com/ModOrganizer2/modorganizer/tree/9c130cbf2fc7225fb2916e46419af50671772aa0) | Accepted MO2 `v2.5.2`, commit `9c130cbf2fc7225fb2916e46419af50671772aa0` | Existing accepted authority for profile-local configuration and provider reconstruction under ADR-0008 |

The framework repositories are primary authorities for the formats consumed by
their own exact revisions. They do not establish author intent for every file
using the format or prove how a particular installed version behaves.

## 5. Read-only private-reference census

### Environment and procedure

The experiment used the user-confirmed private reference environment from
[the Wave B manifest](WAVE-B-reference-environment-manifest.md):

- MO2 `2.5.2`;
- one explicitly identified `Brain Blast Destruction 2024` profile;
- exact profile control-file fingerprints already retained in that manifest;
- no game, MO2, LOOT, SKSE, framework plugin, or external executable launched;
- no file opened for writing; and
- no raw config content, mod name, private absolute path, or source value
  retained in this report.

Procedure:

1. Re-hash `modlist.txt`, `plugins.txt`, `loadorder.txt`, `archives.txt`,
   `settings.ini`, and `lockedorder.txt`.
2. Read enabled `+` entries from `modlist.txt`.
3. Enumerate files under the corresponding physical mod directories plus
   `overwrite`.
4. Classify only stable path/suffix signatures for the surveyed ecosystems.
5. Count provider occurrences and case-insensitive unique relative paths.
6. Re-hash the six profile control files.

All pre- and post-observation hashes matched the completed Wave B manifest.
The scan observed 1,791 enabled physical mod directories, 244,626 provider-file
occurrences, and 223,819 case-insensitive unique relative paths.

These are descriptive physical-provider counts. They do not:

- prove that all files are visible in the game Data view;
- implement every MO2 mapper/skip/hidden rule;
- count archive members;
- establish which duplicate provider wins;
- prove that the installed framework version matches the current upstream
  revision; or
- make the private profile representative.

### Sanitized ecosystem observations

| Path/signature family | Provider occurrences | Unique relative paths | Observation only |
|---|---:|---:|---|
| SPID `*_DISTR.ini` | 86 | 85 | Shared runtime distribution DSL is present across many distinct files |
| KID `*_KID.ini` | 98 | 95 | Shared keyword distribution DSL is present across many distinct files |
| BOS `*_SWAP.ini` | 112 | 109 | Shared runtime object-swap DSL is present across many distinct files |
| OAR `...\OpenAnimationReplacer\...\config.json` | 1,721 | 1,695 | Current JSON graph is the largest observed named family |
| OAR legacy `*_conditions.txt` under the same path family | 6 | 6 | Legacy syntax is present but far less common than current JSON in this profile |
| MCM Helper `MCM\Config\...\config.json` | 50 | 48 | Explicit machine-readable schema has a meaningful real-profile population |
| MCM settings INI under `MCM\Config` or `MCM\Settings` | 106 | 103 | Values are common, but per-setting meaning is not established by generic INI syntax |
| Direct `SKSE\Plugins` INI | 252 | 245 | Many files share a location/extension but not one semantic schema |
| Direct `SKSE\Plugins` TOML | 23 | 22 | Generic TOML syntax is useful; semantic commonality is unproven |
| Direct `SKSE\Plugins` JSON | 288 | 276 | Generic JSON syntax is useful; semantic commonality is unproven |

The profile also contains profile-local `skyrim.ini`, `skyrimprefs.ini`, and
`skyrimcustom.ini`. Their presence supports an effective-winner and generic INI
layer, not a claim that a comprehensive current Bethesda key schema was found.

### Artifact manifest and side effects

| Artifact/effect | Retention |
|---|---|
| Exact profile-control hashes | Already retained in the Wave B manifest; compared in memory only |
| Aggregate counts above | Retained in this report |
| Raw file list, mod names, paths, and config contents | Not retained |
| Network source text | Read transiently from public upstream repositories; linked by immutable commit |
| Repository writes | This report only |
| Protected setup writes | None |
| Processes launched | Git and PowerShell only for public source lookup and read-only local enumeration; no modding/game executable |

## 6. Findings

### 6.1 “Named schema” needs three separate support levels

A generic format grammar, an application schema, and runtime semantics are
different contracts:

| Level | Deterministic claim permitted | Claim not permitted |
|---|---|---|
| Generic syntax | Bytes decode under the declared encoding/parser version; JSON/TOML/selected INI grammar is valid; duplicate-key behavior is reported according to that exact parser contract | The setting or rule has a known game effect |
| Named application schema | File/path/version matches an allowlisted ecosystem; required keys, types, cardinality, and application-specific entry grammar validate | Referenced forms/plugins/files exist or runtime conditions will match |
| Resolved semantics | Exact effective provider, applicable framework version, referenced local entities, and allowlisted cross-file/runtime rules resolve | Arbitrary custom code, undocumented setting meaning, or live runtime outcome |

Documentation claims and LLM interpretation are additional evidence layers,
not a fourth deterministic parser level.

This separation is necessary even for JSON. RFC 8259 defines syntax and warns
that duplicate object-name behavior varies among implementations. A generic
JSON parser therefore cannot substitute for the exact consumer's parser and
schema.

### 6.2 Priority 1: MCM Helper's explicit JSON schemas

**Recommendation:** implement the first named structural schema contract for
MCM Helper `v1.6.2` `config.json` and keybind JSON, while keeping settings-value
meaning separate.

Why it ranks first:

- the exact release ships machine-readable JSON Schema draft 2020-12 files;
- required properties and closed property behavior are explicit;
- upstream provides a positive sample;
- 48 case-insensitive unique `config.json` paths occur in the reference
  profile;
- syntax/schema fixtures can be completely synthetic and redistributed;
- validation can be performed read-only without game execution; and
- unknown schema versions or extension properties can become explicit gaps.

Initial deterministic scope should include:

- exact effective file/provider identity;
- JSON syntax under a declared parser profile;
- schema identity and supported MCM Helper version;
- required keys, property types, enumerations, and structural constraints;
- directory/mod-name consistency only where the schema/source contract proves
  it;
- duplicate JSON member detection before lossy deserialization;
- referenced plugin/form/file existence only through separately qualified
  local-state resolvers; and
- schema-invalid, unsupported-version, and unresolved-reference gaps.

It should not claim:

- that a user-selected value is desirable;
- that a setting's arbitrary mod-specific effect is understood;
- that an MCM choice is active in a save;
- that every `settings.ini` key has a shared schema; or
- that a valid menu definition proves its backing Papyrus/native behavior.

### 6.3 Priority 2: SPID, KID, and BOS as three related named DSLs

**Recommendation:** treat SPID `7.3.1`, KID `4.0.6`, and BOS `3.4.1` as a
coordinated early family of three separately versioned schemas, not as generic
INI and not as one merged “powerof3 format.”

Why they rank early:

- each runtime discovers a stable Data suffix;
- exact author-maintained parser source is available;
- the reference profile contains 85, 95, and 109 unique paths respectively;
- entries can change runtime distribution or object selection without plugin
  record overrides, so they matter to cross-layer analysis;
- generic INI validity is visibly insufficient because values contain
  application-specific pipe-delimited fields, filters, traits, chances,
  conditions, form sets, and properties; and
- positive, matched-negative, malformed, collision, missing-reference, and
  unsupported-version fixtures are feasible.

Shared infrastructure may cover:

- exact file/provider bytes and path suffix;
- encoding and line/key preservation;
- duplicate key/entry preservation rather than map collapse;
- framework/version applicability;
- plugin/form/editor-ID tokenization;
- qualified reference resolution;
- source location for every parsed component;
- deterministic parse diagnostics; and
- dependency closure over effective plugins/records and framework version.

Each named analyzer must retain its own parser contract and semantic allowlist.
SPID distribution targets, KID keyword application, and BOS conditional swaps
are not interchangeable merely because the source repositories share utility
patterns.

The first semantic scope should stop at:

- parser-equivalent entry structure;
- deterministic referenced-entity existence/type checks where qualified;
- directly provable duplicate/collision or impossible-reference conditions;
- explicit unresolved/unsupported custom conditions or runtime-only behavior;
  and
- documented cross-mod rules only when the exact framework source or
  applicable author documentation supports them.

Static analysis must not promise that a rule will fire in a particular live
game state.

### 6.4 Priority 3: OAR current JSON structure, with condition semantics gated

**Recommendation:** prioritize an OAR named structural/indexing contract after
the explicit MCM schema and the bounded distribution DSLs. Treat condition
evaluation as a separately allowlisted maturity stage.

Evidence pulls in both directions:

- 1,695 unique current OAR `config.json` paths make it the largest observed
  named configuration population by far;
- upstream source has an exact current JSON parser and serialization model;
- OAR configurations are structurally connected to animation providers and
  priorities; but
- no immutable GitHub release was published at retrieval, so the surveyed
  source is a main-branch commit rather than a release contract;
- OAR supports legacy condition text as a distinct route; and
- third-party SKSE plugins may add custom conditions and JSON components.

The first OAR contract should therefore support:

- exact commit/schema-adapter identity;
- current JSON syntax and parser-equivalent structural fields;
- path/provider, priority, submod, animation-file, and required-plugin
  references where source-defined and statically resolvable;
- duplicate/ambiguous definitions and missing effective animation assets;
- known built-in condition names/argument shapes under a positive allowlist;
- custom-condition dependency identification; and
- explicit `unsupported-custom-condition`, `unsupported-legacy-syntax`, and
  runtime-state gaps.

It should not evaluate a custom condition merely because its JSON is valid.
The small legacy `*_conditions.txt` population in this one private profile is
not a reason to remove the legacy boundary; it only lowers its initial roadmap
priority relative to current JSON.

### 6.5 Generic foundation: game/profile INIs and per-plugin formats

Generic support should precede or accompany every named analyzer:

- capture exact bytes, encoding observations, provider chain, and winner;
- select an explicit parser profile rather than assuming one universal INI;
- preserve duplicate keys, comments, ordering, and raw source spans where the
  consumer may care;
- validate generic JSON/TOML syntax with exact parser/version provenance;
- distinguish parse errors from unsupported application semantics;
- compare changed effective values across snapshots; and
- attach documentation requirements without converting prose into local-state
  authority.

Base-game/profile INIs merit early **generic winner and syntax** coverage because
they are core effective configuration. This survey did not find a comprehensive
current primary-source schema that safely authorizes arbitrary Skyrim INI key
semantics. Named key groups should therefore enter only through later
allowlisted, source-backed modules with explicit runtime applicability.

Likewise, 245 direct SKSE-plugin INI paths, 22 TOML paths, and 276 JSON paths do
not form one schema. Their shared directory and extension justify discovery,
syntax, provider, change, and coverage accounting. A specific plugin should
receive a named semantic schema only when all of these are true:

- exact plugin/framework version is deterministically identified;
- primary source defines the parser and setting semantics;
- the value has a concrete compatibility, lifecycle, stability, or
  cross-layer mechanism relevant to Infinium;
- matched positive/negative fixtures are available; and
- unsupported keys and versions fail to gaps rather than best effort.

ENB/ReShade and other root-level configuration should be coordinated with
RQ-019 so the configuration parser does not independently infer native/root
component identity or compatibility.

### 6.6 LOOT configuration is not a fourth generic priority

LOOT userlist and configuration are important, exact local inputs, but ADR-0011
already assigns them to the conditional libloot/data adapter with distinct
curated, user-supplied, direct-libloot, and Infinium-derived authorities.

A generic config inventory may identify their presence and provider. It should
not reparse their meaning through an independent named schema that could drift
from the accepted libloot boundary.

## 7. Recommended staged roadmap

| Stage | Scope | Earliest named ecosystems | Required abstention |
|---|---|---|---|
| G0 — Generic inventory | Exact bytes, encoding, path, provider chain/winner, changed values, generic JSON/TOML and selected INI syntax, duplicate preservation | All discovered configuration | Unknown parser profile, archive/member gap, inaccessible/drifted bytes |
| N1 — Explicit structural schemas | Machine-readable application-schema validation and source-backed path/name constraints | MCM Helper `v1.6.2` config and keybind JSON | Unknown schema/framework version, unresolved reference, arbitrary settings meaning |
| N2 — Shared runtime rule DSLs | Parser-equivalent entries, qualified local references, collision/malformed checks | SPID `7.3.1`, KID `4.0.6`, BOS `3.4.1` as separate contracts | Runtime-only condition truth, unknown entry variant, unqualified record/form shape |
| N3 — Extensible condition graphs | Structural graph/index checks plus allowlisted built-in conditions and asset references | OAR at an immutable selected revision | Custom condition/plugin behavior, legacy route until separately qualified, live animation outcome |
| N4 — Targeted plugin schemas | Source-backed high-impact per-plugin key groups | Selected later by concrete mechanism and corpus evidence | Arbitrary INI/TOML/JSON semantics or unsupported plugin version |
| L — Bounded unfamiliar investigation | Cited interpretation of relevant unknown configuration after deterministic reduction | No local-state authority | Model must abstain from winner/value redefinition and unsupported runtime conclusions |

This ordering is a research recommendation, not an accepted milestone plan or
architecture.

## 8. Alternatives considered

### Alternative A — Prioritize by raw file count

This would put OAR first and may also elevate generic JSON over smaller
frameworks.

Rejected as the sole rule because:

- one animation pack may contribute many configs;
- prevalence does not supply a stable schema;
- OAR custom conditions enlarge the unsupported semantic boundary; and
- file count says nothing about consequence, cross-mod interaction, or fixture
  quality.

Local count remains a useful tie-breaker and corpus-design input.

### Alternative B — Treat INI, TOML, JSON, XML, and YAML as the schemas

Rejected. These are serialization grammars. They do not define MCM controls,
SPID filters, KID traits, BOS swaps, OAR conditions, or per-plugin setting
meaning. INI additionally lacks one universal consumer behavior for duplicate
keys, case, ordering, escaping, and comments.

### Alternative C — Support all `SKSE\Plugins` configuration uniformly

Rejected beyond generic inventory/syntax. The observed location contains
hundreds of heterogeneous files. A shared directory is not a shared semantic
contract.

### Alternative D — Use an LLM to interpret every unfamiliar config

Rejected as a default. It would be expensive, difficult to evaluate, and
contrary to ADR-0001 if it became effective-value authority. A targeted LLM
may interpret a small evidence-backed candidate after deterministic discovery,
with citations, uncertainty, and abstention.

### Alternative E — Defer all configuration until after semantic records

Rejected as a blanket rule. Configuration remains lower priority overall, but
MCM schemas and runtime rule DSLs offer unusually stable, common, and
cross-layer structures. Researching them now also supplies RQ-036 and corpus
evidence without placing them in M1.

### Alternative F — Duplicate LOOT YAML semantics in a config analyzer

Rejected because ADR-0011 already selects a fidelity and authority boundary.
Duplicate parsing would create conflicting semantics.

## 9. Contrary evidence, uncertainty, and limitations

- The private census is one large real-used profile, not a representative
  corpus or correctness oracle.
- Counts are physical enabled-mod/overwrite provider occurrences and unique
  relative paths. They do not implement a qualified MO2 effective-winner
  adapter, archive member inventory, or every mapper.
- Installed framework versions were not inferred from file names or mod names.
  Current upstream source proves candidate contracts, not applicability to
  every local file.
- OAR had no GitHub release object at retrieval. Its exact source commit is
  reproducible, but selecting a production support revision remains future
  work.
- MCM's shipped JSON Schema provides strong structural authority, but the
  runtime parser may have compatibility behavior that requires conformance
  testing against the exact release.
- SPID, KID, and BOS source exposes detailed parser behavior but not a
  machine-readable schema. A first-party schema must be derived from and tested
  against the exact runtime revision; it must not silently simplify the parser.
- Static form/plugin/file resolution depends on qualified MO2, runtime,
  Bethesda semantic, and asset/provider surfaces. An analyzer cannot outrun
  those gates.
- No runtime experiment proved actual distribution, swapping, MCM persistence,
  or animation selection. Those claims are intentionally outside this survey.
- This report does not establish consequences or severity. A malformed config
  may be harmless, high impact, or unused depending on exact applicability.
- The six observed OAR legacy paths are weak prevalence evidence from one
  private profile only.
- A comprehensive authoritative Bethesda Skyrim INI-key schema was not
  established. Generic parsing and selective documented-key support are safer
  than a broad guessed schema.
- No performance benchmark was run. RQ-027/RQ-035 must measure indexing cost
  if configuration enters candidate generation at high-end scale.

## 10. Recommendation and confidence

**Recommended answer:** begin with a generic exact-byte/provider/syntax layer,
then add MCM Helper's explicit JSON schemas first, followed by separate
versioned SPID, KID, and BOS DSL contracts, then OAR's current structural graph
with custom-condition semantics explicitly gated. Keep base-game INIs and
heterogeneous per-SKSE-plugin INI/TOML/JSON at generic winner/syntax/change
coverage until an exact source-backed, high-impact plugin schema meets the
named-adapter criteria.

Confidence is:

- **high** that MCM Helper is the lowest-risk first explicit schema;
- **high** that SPID/KID/BOS merit early separate named contracts rather than
  generic INI treatment;
- **high** that OAR deserves early structural/index coverage due to its large
  observed population;
- **medium** on the exact ordering between the distribution DSL group and OAR,
  because no measured implementation cost or failure corpus exists yet; and
- **high** that arbitrary SKSE-plugin configuration must remain unsupported
  semantically by default.

Preconditions before any support claim:

1. select an immutable framework/schema version;
2. define parser-equivalent behavior, encoding, duplicates, limits, and
   failure states;
3. consume qualified ADR-0008/ADR-0010 effective provider inputs;
4. qualify every record/form/file reference resolver used;
5. create synthetic positives, matched negatives, malformed, duplicate,
   collision, missing-reference, unsupported-version, and custom-extension
   cases;
6. retain raw source spans and exact dependency provenance;
7. prove read-only behavior; and
8. declare unknown semantics as coverage gaps.

## 11. RQ status and downstream follow-up enabled

### Accepted RQ-021 disposition

> **Answered for bounded M0 roadmap input by RESEARCH-0016; generic
> configuration foundation and first named-schema priorities accepted; exact
> delivery versions, conformance, and broader M3 ecosystem roadmap remain
> pending.**

Do not mark any named analyzer implemented or qualified.

### Product taxonomy input

The accepted taxonomy used this survey as evidence that the configuration
technical surface contains materially different mechanisms:

- schema-backed declarative UI/config structure;
- shared runtime distribution/swap DSL;
- extensible condition graph;
- base-game/profile settings;
- per-native-plugin settings; and
- tool-owned configuration already governed by a separate adapter.

Those are observed technical mechanisms, not proposed accepted taxonomy values.
They do not determine declared purpose, affected game area, consequence,
severity, symptom, affected area, or effect extent. One config may affect
several areas, and one feature may span config, records, assets, scripts, and a
native component.

### Evaluation follow-up

Refine [EVAL-0071](../../evaluation/case-catalog.md) or create reviewed
successors with at least:

1. generic JSON/TOML/selected-INI syntax and duplicate behavior under exact
   parser profiles;
2. effective-winner/provider and change-impact cases;
3. MCM Helper valid, invalid, duplicate-member, unsupported-schema,
   missing-reference, and mod-specific-setting-abstention cases;
4. separate SPID/KID/BOS positive and matched-negative parser-equivalence
   fixtures;
5. missing/wrong-type form/plugin references, collisions, and runtime-only
   condition abstention;
6. OAR built-in condition, missing animation/reference, unknown custom
   condition, legacy syntax, and unsupported-revision cases;
7. an arbitrary syntactically valid SKSE-plugin config that must remain a
   semantic gap; and
8. metamorphic provider-winner and unrelated-file changes with
   dependency-correct invalidation.

Every positive needs a structurally similar harmless or unsupported negative.
Expected results must not be changed to match a convenient parser.

### Registry and planning follow-up

For coordinator review:

- register the five upstream framework repositories and exact revisions as
  author-maintained technical authorities within their parser/schema scopes;
- record OAR's lack of a GitHub release at retrieval as a support-version
  selection gap;
- keep LOOT configuration under ADR-0011;
- add no architecture ADR solely for this roadmap recommendation;
- carry the staged scopes into the eventual M3 configuration-analyzer plan;
  and
- coordinate root-level configuration with RQ-019, asset references with
  RQ-023, record/form references with RQ-024, scale with RQ-027/RQ-035, and all
  classifications with RQ-036.

## 12. Requirements and evidence traceability

| Requirement / decision | Evidence | Proposed result |
|---|---|---|
| SCOPE-005 | Wave B authority map plus private path-shape census | Relevant configuration is observable; unsupported semantics remain explicit |
| AUTH-001 through AUTH-003; ADR-0003 | Read-only enumeration and no framework/game execution | Survey and proposed analyzers require no setup write authority |
| SNAP-001 through SNAP-006; ADR-0002/ADR-0010 | Exact pre/post control hashes and proposed byte/provider dependencies | Configuration results bind to immutable bytes, provider state, and parser/schema versions |
| EVID-001 through EVID-007; ADR-0001 | Generic/schema/resolved/documentation/LLM separation | Effective values remain deterministic; inference cannot redefine them |
| ANALYSIS-005 | SPID/KID/BOS and OAR cross-layer structures | Named config evidence can connect to records/assets/runtime only through qualified dependencies |
| ANALYSIS-008 | Version-pinned framework contracts and unsupported-version gaps | Version coherence can be checked without assuming compatibility |
| ANALYSIS-011 | Staged G0/N1/N2/N3/N4/L roadmap | Generic syntax/winner, named schemas, documentation rules, and unfamiliar interpretation stay distinct |
| ANALYSIS-016 | Named-analyzer admission criteria and preconditions | Each future analyzer must declare scope, dependencies, gaps, maturity, and evaluation |
| ANALYSIS-017 | Named-path/suffix discovery and staged escalation | High-volume configs can be indexed before targeted semantic/LLM work |
| ANALYSIS-018 | Winner/change foundation and metamorphic evaluation proposal | Configuration changes invalidate only declared dependent work |
| COVER-001 through COVER-003 | Unsupported version/custom condition/arbitrary plugin gaps | Coverage reports named populations without claiming arbitrary semantic understanding |
| ADR-0008 | Existing effective provider/winner authority | Config analyzers consume MO2 reconstruction rather than duplicate it |
| ADR-0009 | Qualified form/link semantics | Textual references do not become resolved game entities without qualification |
| ADR-0011 | LOOT configuration ownership | No duplicate generic LOOT semantic parser is proposed |
| EVAL-0071 | Proposed split positive/negative/boundary fixture matrix | Evaluation can distinguish syntax, schema, references, semantics, and abstention |
| RQ-036 | Technical-mechanism examples and explicit non-taxonomy boundary | Survey informed accepted taxonomy `0.1.0` without deciding classifications |

## 13. Conclusion

The first configuration roadmap should not be “support INI and JSON.” It should
be:

```text
exact effective bytes and provider
  -> parser-profile syntax and duplicate preservation
  -> exact versioned application schema
  -> qualified reference and cross-file resolution
  -> allowlisted runtime semantics
  -> cited unfamiliar interpretation only when needed
```

MCM Helper supplies the strongest first explicit machine-readable schema.
SPID, KID, and BOS supply common, consequential, source-defined runtime DSLs
that deserve separate early contracts. OAR supplies the largest observed named
population and useful structural analysis, but its extensible custom-condition
boundary requires deliberate abstention. Base-game and arbitrary plugin config
remain valuable at the generic winner/syntax/change layer until exact evidence
justifies narrower semantic modules.
