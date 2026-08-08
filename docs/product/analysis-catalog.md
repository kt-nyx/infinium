# Analysis catalog

Status: Accepted  
Last reviewed: 2026-08-08

Accepted bounded-M1 clarification: ADR-0028 governs the `EDID`, FaceGen,
asset-availability, coverage, gap, and taxonomy semantics used by the first
Bethesda proof.

This is the living inventory of desired analysis capabilities. Catalog entries
do not imply complete analyzer delivery. M1 Slices 3 and 4 have implemented the
bounded exact-target/MO2 snapshot and Bethesda semantic/index substrate
described by their accepted plans. Slice 5 is active: WP1 delivered its
contracts, migration, and evaluator boundary, and WP2 is the next authorized
package on the path to the first complete evidence, candidate, finding, case,
and replay flow. Every
broader capability remains unimplemented unless a current implementation record
states its exact delivered scope. The section headings group product
capabilities for planning; they do not replace the accepted
[Skyrim SE mod-impact taxonomy](mod-impact-taxonomy.md). One analyzer or
interaction may cross several sections and taxonomy codes.

Each future analyzer specification must state:

- user problem and likely impact;
- supported scope and exclusions;
- inputs and provenance;
- deterministic and LLM responsibilities;
- candidate selection;
- evidence and abstention thresholds;
- false-positive risks;
- remediation and validation;
- offline/online behavior;
- expected scale, cost, and maturity;
- linked evaluation cases.

## A. Profile and installation integrity

Desired detections:

- missing or unreadable profile inputs;
- enabled entries with missing files;
- duplicate or ambiguous plugin identity;
- direct/unmanaged files affecting effective state;
- root-level leftovers and duplicate native loaders;
- corrupt/unreadable archives;
- missing localized strings;
- non-reproducible dependencies on unmanaged state;
- enabled mods contributing no effective behavior.

Ordinary redundancy is generally a low-severity advisory unless it indicates
lost expected functionality.

## B. Requirements, masters, identity, and versions

Desired detections:

- missing or disabled masters;
- missing hard/conditional requirements;
- incorrect runtime, SKSE, Address Library, or native binary branch;
- plugin/assets/patch components from different releases;
- incomplete updates;
- obsolete optional modules;
- documentation applied to the wrong version;
- ambiguous installed-mod identity;
- missing or incompatible installer options.

FOMOD reconstruction is best-effort: infer likely selections from retained
archives and installed files and expose ambiguity. Prospective choice recording
is after M4.

## C. LOOT and deterministic-tool findings

Integrate rather than duplicate:

- curated incompatibilities and requirements;
- ordering constraints and cycles;
- plugin metadata and known cleaning information;
- established error checks exposed by approved tools.

Infinium adds profile correlation, evidence normalization, case grouping, and
explanation. It does not rebuild LOOT's masterlist without demonstrated need.
Bethesda record semantics use the accepted bounded Mutagen `0.54.2`
semantic-library boundary behind an Infinium-owned analyzer contract, subject
to independent fixture qualification.

## D. Semantic record interactions

Primary goals:

- intentional changes silently lost;
- scope-incongruent stale-value reversions;
- partial feature erosion;
- internally inconsistent record groups;
- unresolved or missing references;
- dangerous deletion patterns;
- later plugins undoing a valid patch;
- incompatible combinations unsupported by one winning-record view.

Development should prioritize structural and logic impact over cosmetic impact.
The following semantic families are analyzer-roadmap examples, not taxonomy
codes or a definitive inventory of Skyrim game areas or effects:

- startup/save/global engine-related records;
- quests, dialogue, scenes, aliases, and conditions;
- cells, worldspaces, placed references, landscape, and navmesh;
- NPC behavior, appearance, inventory, factions, and packages;
- global gameplay systems, perks, spells, effects, and combat;
- leveled lists, FormLists, outfits, containers, and distribution;
- items, crafting, enchantments, and keywords;
- races, skeleton/body relationships, and transformations;
- weather, lighting, and image spaces;
- gamesettings and globals.

The accepted [taxonomy](mod-impact-taxonomy.md) supplies the broader
declared-purpose, technical-surface, affected-area, consequence, and
effect-extent vocabulary. The accepted RQ-024 roadmap separately determines
which semantic record families and field relationships should receive named
analyzer support after the first proof. Neither hosting-site categories nor
record families map one-to-one to author intent or player-visible game areas.

The currently accepted bounded proof sequence is:

1. generic override, changed-field, reference-resolution, feature-graph, and
   stale-value/reversion substrate;
2. a deliberately narrow first-category proof, currently actor/AI/FaceGen;
3. a materially different category proof, currently placed-reference (`REFR`)
   placement, activation, enable-parent, and linked relationships; and
4. quest semantics beginning with a separately qualified narrow alias edge,
   followed by item/crafting, world, and other semantic families only after the
   earlier shapes are independently qualified.

This sequence is a present implementation/evaluation roadmap, not a permanent
NPC-versus-non-NPC rule. Generic infrastructure must not encode either proof
category as the whole domain.

For M1 specifically, the second proof is the accepted EVAL-0017 REFR
linked-reference/placement case. `QUST`, quest-alias, and forced-reference
semantics remain outside the M1 allowlist; the planned EVAL-0006/EVAL-0007
quest-relevance pair is later roadmap work.

Within this bounded proof, `EDID` is admitted identifying metadata for `NPC_`,
`RACE`, and `REFR`, but the identifier value is not semantic evidence of
purpose, affected area, consequence, or intent.

## E. Cross-record and cross-layer coherence

Analyze coherent feature graphs rather than isolated rows. These are
illustrative cross-layer graphs, not an exhaustive category list:

- NPC record, facegen, outfit, packages, scripts, and placed references;
- quest, aliases, dialogue, scenes, conditions, and scripts;
- location cells, navmesh, landscape, lighting, and placed objects;
- perk trees, spells, conditions, and globals;
- item records, meshes, textures, keywords, recipes, and distribution;
- plugin records, assets, configuration, runtime components, and generated
  output.

Estimate the applicable effect-extent facets—direct-subject breadth, spatial
breadth, persistence/lifecycle breadth, and causal propagation—and predicted
symptoms. Do not collapse them into one “blast radius” value.

### Compiled Papyrus boundary

Compiled-script analysis is bounded static analysis. It may parse allowlisted
PEX structure and VMAD attachments, index defined/imported symbols, script and
property references, and participate in causal joins when those relationships
are supported by retained evidence. It must not claim complete source
reconstruction, dynamic call behavior, runtime ordering, latent-state
behavior, performance cost, or gameplay outcome from bytecode structure alone.
PEX, VMAD, SWF, and related artifacts are never executed to analyze them.

## F. Asset and archive problems

Raw overwrite enumeration is out of scope because MO2 already supplies it.

Report only meaningful conditions such as:

- plugin/facegen mismatch;
- missing referenced mesh/texture/interface/script asset;
- partial incompatible asset variants;
- script or interface file unexpectedly replaced;
- plugin and archive activation/naming inconsistency;
- archives and loose files producing unintended cross-version state;
- documentation requiring a patch or priority relationship;
- unsupported assets contributing to a broader case.

Use path indexing before expensive content hashing or extraction.
The first typed asset slice is NIF reference completeness. Loose-file FaceGen
coverage additionally requires exact full/light plugin-origin, race/template,
provider, and shadowing semantics. RESEARCH-0034 qualified that loose-only
decision boundary for pre-resolved record and provider inputs; authoritative
MO2/provider reconstruction and production-parser conformance remain separate
evaluation work. Archive FaceGen parity is independently gated. Wave C
selected no production NIF parser dependency, so parser choice and
qualification remain later work.

For the bounded M1 FaceGen proof, applicability is decided in the accepted
deleted, template-decision, definite trait-template, race-decision,
race-without-FaceGen, applicable order. Non-trait template use does not suppress
the NPC's own FaceGen check. Asset availability is reported as `present`,
`absent`, or `unknown`; archive support is a separate capability gap.
Each applicable mesh and tint loose path is one obligation. When exhaustive
byte-verified loose-provider indexing is unavailable, the observation remains
unknown and publishes `face-gen-loose-assets` /
`exhaustive-byte-verified-loose-provider-index` at snapshot and result scope;
archive evidence cannot discharge that gap.

## G. Patch effectiveness

Core capability:

- required patch missing;
- present but disabled;
- wrong load or mod priority;
- targeted upstream version mismatch;
- overwritten or superseded;
- only partially resolves the current interaction;
- now unnecessary or obsolete;
- carries unintended values;
- plugin/assets internally inconsistent.

Presence or a patch-like name is never sufficient proof.

## H. Native runtime and root components

Desired coverage:

- game runtime;
- SKSE and loaders;
- Address Library;
- native SKSE DLLs and dependencies;
- ENB;
- ReShade;
- Engine Fixes and similar root components;
- duplicate/incompatible loaders;
- unmanaged root files;
- documented configuration relationships.

Support only the explicitly pinned Skyrim SE runtime version initially.
Unsupported variants fail clearly rather than receiving best-effort
conclusions.

Inspection is static and provider-aware. Portable Executable metadata,
version resources, manifests, imports/exports, and documented component
relationships may support identity or compatibility evidence, but Infinium
never loads an installed DLL for inspection. Filename or embedded version alone
does not prove component identity, compatibility, or that the game will use it.

## I. Generated output

Named modules may support:

- Pandora, Nemesis, and FNIS;
- BodySlide;
- Synthesis;
- DynDOLOD and TexGen;
- grass cache;
- other widely used patchers/generators.

Named analyzers should understand tool version, inputs, outputs, manifests, and
regeneration conditions for an explicitly version-pinned supported subset.
Wave C found no surveyed generator with one complete, stable, authoritative
manifest contract covering all relevant inputs and outputs. Unsupported
generators receive only generic presence, provider, and bounded structural
observations plus explicit coverage gaps; freshness remains unknown unless a
separately qualified complete dependency closure proves it.

xLODGen is an xEdit mode and therefore has no named integration, adapter,
detection, configuration, output-parsing, or capability role under ADR-0007.
Generic analysis of effective terrain or object-LOD files remains permitted
when it neither attributes them to xLODGen nor depends on xEdit-specific
artifacts or behavior.

## J. Configuration

Initially lower priority but potentially high impact:

- effective configuration winner;
- parse/syntax errors;
- duplicate keys;
- known schemas for important frameworks;
- documentation-required values;
- recognized cross-mod rules;
- targeted LLM investigation of unfamiliar relevant configuration.

Do not claim semantic understanding of arbitrary configuration.

The accepted roadmap begins with exact-byte/provider/syntax indexing, then
adds separate named schemas/analyzers for MCM Helper; SPID, KID, and BOS; and
OAR. These ecosystems do not share one generic semantic contract. File-format
validity, winner/provider state, schema validity, condition-language semantics,
and Skyrim-domain meaning remain separate qualification layers.

## K. Documentation intelligence

Extract cited, versioned claims:

- hard and optional requirements;
- incompatibilities;
- required patches;
- installation choices and priority;
- runtime restrictions;
- new-game and upgrade/removal safety;
- required generation steps;
- configuration requirements;
- replaced/superseded mods;
- known bugs and troubleshooting.

Primary sources:

- mod descriptions and requirements;
- author-maintained articles and changelogs;
- sticky/author posts where allowed;
- LOOT;
- official repositories and bundled documentation.

Community posts and bug reports are investigative leads unless corroborated.
Access must follow source API and policy constraints.

## L. Playthrough lifecycle safety

Desired detections:

- new game required;
- unsafe mid-playthrough installation/removal/update;
- required upgrade procedure;
- unsupported downgrade;
- stale configuration or generated output after update;
- version-specific save risk;
- monitoring or validation advice for grounded long-term risks.

Save-to-installation-snapshot association is after M4.

## M. Runtime evidence and symptom diagnosis

Named log adapters plus bounded unfamiliar-log investigation:

- crash logs;
- SKSE/native plugin logs;
- generator logs;
- LOOT output;
- Papyrus logs;
- ENB/ReShade logs;
- other recognized reports.

Runtime evidence must be associated with an exact or sufficiently matched
installation snapshot/test-session provenance and record an application link
when used by an analysis run/context. Scan-generated tool output retains its
originating run directly. Case-scoped follow-up may refine evidence. Symptom
reporting may originate lead-only investigation cases or supported cases when
the finding threshold is already met.

## N. Performance and stability boundary

In scope only with a concrete mechanism:

- known engine/plugin limit;
- native incompatibility;
- invalid frame-rate or physics configuration;
- documented interaction;
- memory leak or severe resource pathology with evidence;
- generated output inconsistent with runtime configuration;
- performance so poor that the setup is functionally unusable.

Out of scope:

- FPS optimization;
- hardware grading;
- generic texture or script heaviness;
- speculative performance sections;
- automated benchmarks.

## O. Change impact

Post-foundation capability:

- compare installation snapshots and, where relevant, analysis contexts,
  including comparison with a user-designated prior/reference snapshot;
- identify changed winners, records, assets, configuration, runtime components,
  documentation, patches, and generated outputs;
- revalidate only dependent findings;
- preserve unaffected results with explained carryover;
- reopen affected findings whose prior dispositions no longer apply and revise
  their cases when relevant evidence changes.

A user-designated reference snapshot is a comparison baseline, not proof that
the prior setup was correct or safe.

## P. Advisories

Separate from problems:

- mod contributes nothing effective;
- available update;
- cleaning or maintenance guidance from authoritative sources;
- unmanaged output;
- unusual but not proven-broken configuration;
- consolidation or reproducibility concern.

Advisories must explain why they matter and must not drown out functional
findings. They remain visible/countable but do not affect readiness by default;
an explicit user action-required decision can make a particular advisory
readiness-relevant.
