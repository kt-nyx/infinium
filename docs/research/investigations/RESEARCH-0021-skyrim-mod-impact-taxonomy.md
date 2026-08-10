# RESEARCH-0021: Skyrim mod impact taxonomy

Status: Completed
Disposition: recommendation accepted as
`infinium.skyrim-se.mod-impact-taxonomy/0.1.0`  
Date: 2026-07-25  
Last reviewed: 2026-08-10
Researcher: Codex agent  
Primary question: RQ-036  
M0 wave: C  
Decision enabled: Accepted versioned M1 product taxonomy, product-document
integration, and EVAL-0086/coverage-map specification

Acceptance note: On 2026-07-25 the project owner adopted this proposal as
[`infinium.skyrim-se.mod-impact-taxonomy/0.1.0`](../../product/mod-impact-taxonomy.md).
Proposal-era wording below is retained as research provenance; the linked
product specification is normative. The dependency-map and affected-document
integration and the EVAL-0086 specification review are complete.
RESEARCH-0034/0035 subsequently completed the RQ-023/RQ-025 prerequisites and
closed Gate C at the M0 research/qualification layer. Any later statement in
this dated proposal that those prerequisites remain open is historical, not
current status; no evaluation execution is thereby claimed to pass.

## 1. Question and accepted constraints

**RQ-036:** What purposes or intended feature areas do Skyrim SE mods declare;
through which technical surfaces can they alter effective state; which game
systems/content areas, consequence types, and effect extents can they affect;
and which distinct empirically grounded taxonomies should Infinium use without
conflating them?

The answer must support the product without inventing one universal “mod type.”
It is governed by:

- [FIND-001](../../product/requirements.md#find-001--independent-dimensions),
  which requires independent, versioned classifications for what was modified,
  what may be affected, what may happen, and how broadly;
- [FIND-003](../../product/requirements.md#find-003--effect-extent-and-symptoms),
  which requires blast-radius and symptom estimates with explicit confidence;
- [COVER-001 through COVER-003](../../product/requirements.md#coverage-and-readiness),
  which require labeled denominators and visible unsupported/gap states rather
  than one safety percentage;
- [ANALYSIS-016](../../product/requirements.md#analysis-016--declared-analyzer-contract)
  and
  [ANALYSIS-017](../../product/requirements.md#analysis-017--candidate-first-llm-escalation),
  which require declared analyzer scope and bounded candidate-first escalation;
- [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md),
  which keeps deterministic observation, external claims, inference, and
  recommendation authority distinct;
- [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md)
  and
  [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md),
  which preserve immutable raw evidence and historical meaning;
- [ADR-0004](../../architecture/decisions/ADR-0004-initial-target-scope.md) and
  [ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md),
  which constrain this taxonomy to the selected Skyrim SE target rather than a
  premature cross-game abstraction;
- the
  [taxonomy dependency map](../taxonomy-dependency-map.md), which inventories
  every product consumer but is explicitly not the answer; and
- the accepted
  [M0 Wave C plan](../../plans/milestones/m0/plan.md#wave-c--analysis-surfaces-taxonomy-corpus-and-candidate-scale),
  which requires a distinct, versioned, corpus-tested product taxonomy.

This report proposes
`infinium.skyrim-se.mod-impact-taxonomy/0.1.0-proposed`. It does not accept that
taxonomy, alter any accepted product document, or silently make the proposed
codes normative. For this report, Gate C requires product-owner acceptance and
a separate reviewed product-specification integration. Those taxonomy-specific
requirements are not the whole Gate C: remaining Wave C work also includes the
RQ-023 asset/provider boundary, the RQ-024 semantic scope, the RQ-025 real-case
corpus, RQ-027 measurement/budget evidence, RQ-035 candidate design and
benchmarks, exact EVAL-0016/EVAL-0017 candidates, reviewed EVAL-0032/EVAL-0086
specifications, matched negatives, materially different category coverage,
planted-interaction retention without naive all-pairs LLM work, and explicit
unsupported/unevaluated regions.

## 2. Scope and explicit non-scope

### In scope

- source-supported declared purpose and intended feature area;
- observed technical modification surfaces in the accepted effective
  installation;
- affected Skyrim game systems/content areas supported or predicted by
  qualified semantics and evidence;
- consequence type;
- direct manifestation scope and causal blast radius;
- multi-label, hierarchical, cross-cutting, unknown, unsupported, unmapped, and
  not-applicable behavior;
- classification targets and relationships across claims, observations,
  candidates, hypotheses, findings, and cases;
- versioning and reclassification without rewriting raw evidence;
- analyzer, candidate-routing, findings/cases, UI, coverage/readiness,
  change-impact, remediation/validation, evaluation, and roadmap use; and
- empirical limits and unevaluated regions.

### Out of scope

- a taxonomy of all games, Bethesda runtimes, or mod managers;
- treating a hosting category, filename, extension, record family, analyzer,
  mod title, or tool as authoritative mod intent;
- accepting the proposed taxonomy or updating accepted product requirements;
- assigning definitive classifications to every installed mod;
- analyzer implementation or broad semantic-support claims;
- severity, confidence, predicted symptoms, evidence/source authority,
  analyzer maturity, readiness, lifecycle/disposition/suppression state,
  source class, identity/topology state, runtime-log freshness, scan preset,
  cost, or fixture type;
- deciding whether a specific candidate is a supported finding; and
- claiming the survey corpus is representative of every Skyrim mod.

## 3. Sources, evidence classes, and method

Sources were retrieved or revalidated on 2026-07-25.

### 3.1 Product and technical sources

| Source | Identity and authority | Claim-level use |
|---|---|---|
| [Bethesda Support manual directory](https://help.bethesda.net/app/answers/detail/a_id/24583/) and [Bethesda-published Skyrim Legendary Edition manual](https://assets.ctfassets.net/rporu91m20dc/1IuJHrbXY8GqSQQM0GuuUM/2a3c85e2d7772c2eb45f996443f320f4/manual_skyrim-le_ps3_en-us.pdf) | Current publisher support page plus publisher-hosted manual for Skyrim's core gameplay; retrieved 2026-07-25 | Player-facing distinctions among character progression, skills/perks, items, magic, maps/travel, quests, dialogue/social interaction, combat, stealth/crime, crafting, housing, UI, audio, and saving |
| [Skyrim Special Edition Steam publisher page](https://store.steampowered.com/app/489830/The_Elder_Scrolls_V_Skyrim_Special_Edition/) | Bethesda Game Studios/Softworks product description; retrieved 2026-07-25 | SE-specific publisher vocabulary including quests, environments, characters, dialogue, armor, weapons, art, and effects |
| [Creation Kit Game Systems](https://ck.uesp.net/w/index.php?title=Category:Game_Systems&oldid=4951), [Object Classes](https://ck.uesp.net/w/index.php?title=Category:Object_Classes&oldid=5222), and [Bethesda tutorial index](https://ck.uesp.net/wiki/Category%3ATutorials) | Preserved Creation Kit technical reference and Bethesda-authored tutorial material; historical/community-hosted and not a complete current publisher taxonomy | Technical countercheck for interacting AI, combat, dialogue, magic, package, quest, radiant-story, world/cell, navigation, encounter, item, audio, effect, and world-data concepts |
| [RESEARCH-0014](RESEARCH-0014-root-native-component-surfaces.md) | Proposed Wave C primary-source/local survey | Root/native/runtime surfaces, layered identity, relationship edges, and explicit unknown states |
| [RESEARCH-0015](RESEARCH-0015-generated-output-tool-surfaces.md) | Proposed Wave C primary-source survey | Generated plugin, behavior/animation, mesh/morph, LOD, grass-cache, configuration, sidecar, and report distinctions |
| [RESEARCH-0016](RESEARCH-0016-configuration-ecosystem-survey.md) | Proposed Wave C primary-source/local survey | Base/profile settings, schema-backed configuration, runtime DSLs, condition graphs, component configuration, and tool-owned configuration |
| [RESEARCH-0017](RESEARCH-0017-compiled-papyrus-analysis-boundary.md) | Proposed Wave C primary-source/local survey | Compiled-Papyrus structure, provider/API/VMAD relationships, and the limit of surface-based inference |
| [RESEARCH-0018](RESEARCH-0018-asset-reference-completeness.md) | Proposed Wave C primary-source/local survey | Typed model-to-texture/behavior edges, effective-provider resolution, asset-format limits, and exact-versus-heuristic distinctions |
| [RESEARCH-0019](RESEARCH-0019-semantic-record-family-roadmap.md) | Proposed Wave C primary-source/fixture-design survey | Plugin record/field/link surfaces, narrow NPC and placed-reference proofs, and the difference between record availability and qualified game semantics |
| [RESEARCH-0020](RESEARCH-0020-evaluation-corpus-and-real-mod-candidates.md) | Proposed Wave C author-source/real-mod corpus | Retained declared-purpose evidence, an incomplete NPC candidate, a non-NPC placement-reconciliation lead, exact known artifact identities, replacement requirements, corpus obligations, and acquisition limitations |

The Creation Kit references are useful technical vocabulary, not an accepted
ontology. The Object Classes page itself warns that it is incomplete. Neither
it nor the player manual covers native extensions, configuration DSLs,
generated outputs, or every mod-created system. Conversely, record/object
classes are implementation concepts and cannot be copied into a player-visible
area taxonomy one-to-one.

### 3.2 Empirical coding method

The synthesis used these steps:

1. Extract provisional distinctions and every intended product consumer from
   the accepted product documents and taxonomy dependency map.
2. Code each RQ-019 through RQ-024 surface observation by what was directly
   observed, not by the analyzer that found it.
3. Code the RQ-025 corpus from retained author-purpose/compatibility evidence,
   incomplete report-local candidate manifests, and exact known artifact
   identities separately from local technical structures and predicted
   effects, preserving its incomplete-candidate, live-acquisition,
   applicability, and independent-ground-truth gates.
4. Compare player-facing game concepts in the publisher manual with technical
   Creation Kit concepts, retaining only distinctions useful to diagnosis.
5. Test whether one label could describe cross-layer examples without losing
   information. Where it could not, split the concept into independent facets.
6. Apply the proposed facets to the synthetic/real cases below, including
   matched negatives, cross-cutting examples, and unknown/unsupported states.
7. Record corpus regions with no adequate evidence rather than filling them
   from intuition.

No game, MO2 instance, modding helper, native component, archive, or model
provider was executed for this synthesis. Public unauthenticated documentation
reads and the previously recorded read-only survey artifacts were the only
external inputs. This report is the only repository write.

## 4. Findings

### F1 — A single “mod type” is semantically false

One installed entity can:

- declare several purposes;
- use several technical surfaces;
- affect several game areas;
- cause different consequence types under different interactions; and
- have localized direct scope but broad downstream propagation.

A compatibility patch is a purpose, not a technical surface. A plugin is a
technical carrier, not an affected game area. “NPC” may describe an intended
feature area, an affected-area label, a record family, or a UI navigation
shortcut, but those statements are not equivalent. “Generated” can describe
provenance while the generated runtime artifact is simultaneously a plugin,
mesh, texture set, behavior graph, or cache.

Therefore Infinium needs five related classification axes and structured
subfacets, not one mutually exclusive mod-category field.

### F2 — The five accepted provisional concepts should remain distinct

| Axis | Question answered | Classification basis | Must not imply |
|---|---|---|---|
| Declared purpose/intended feature area | What does applicable author-maintained evidence say this mod/artifact is for? | Cited author claim at an exact source revision and applicable version/options | Complete actual impact, harmlessness, or implementation surface |
| Technical modification surface | Through what runtime-consumed mechanism and delivery form can effective state change? | Snapshot-bound local structure plus qualified format/consumer semantics | Author intent, player-visible area, consequence, or severity |
| Affected game system/content area | Which Skyrim system/content domain is actually or plausibly affected? | Qualified semantic relationships, applicable claims, and supported inference | Technical carrier, consequence, severity, or breadth |
| Consequence type | What kind of undesirable result may occur if the interaction manifests? | Finding/hypothesis mechanism and supported causal reasoning | Severity, probability/confidence, symptom, or scope |
| Effect extent | How broad is direct manifestation and downstream causal propagation? | Supported population/spatial/persistence/dependency estimates | Severity, likelihood, or user importance |

These axes should be independently assignable and independently unknown. Every
assignment identifies the exact shared taxonomy release used.

### F3 — Declared purpose needs an action facet and an intended-target facet

Author prose commonly combines an action with a target: add quests, overhaul
NPC appearance, fix AI, patch two location overhauls, generate LOD, provide a
runtime framework, or expose configuration. A single flat list would either
duplicate every action/target combination or erase one half of the claim.

The declared-purpose classification therefore needs:

1. a **purpose kind** describing the claimed operation; and
2. one or more **intended target** labels describing the claimed feature area.

Both remain external-claim classifications. An intended target may map to a
similarly named affected-area concept for routing, but that mapping creates
only a candidate expectation. It never proves the local artifact's complete
affected area.

### F4 — Technical surface needs semantic-mechanism and realization facets

The surface reports show two independent questions:

- what consumer/runtime mechanism gives the bytes meaning; and
- how the effective bytes enter or are selected in the installation.

For example, a compiled Papyrus script may be loose or archived; a generated
plugin remains plugin data; root native code may depend on a Data-side native
plugin and runtime support data; and a configuration DSL may distribute forms
without editing the corresponding plugin records. Infinium should therefore
classify both **semantic mechanism** and **realization/delivery**, with
additional generated-output labels where the runtime consumes a derived
artifact.

Documentation, generator logs, source code, debug data, LOOT metadata, and
analyzer output are evidence or analysis inputs. They are not technical
modification surfaces unless a qualified game/runtime consumer actually uses
them to change effective behavior. Installer/FOMOD metadata, archive packaging,
and retained choice history are likewise acquisition/provenance inputs; the
selected runtime-consumed outputs receive the surface assignments.

### F5 — Affected area is player/system centered, not record centered

The publisher manual and Creation Kit references support stable broad
distinctions among runtime/session behavior, player progression, actors/AI,
quests/dialogue, world/environment, gameplay systems, items/crafting,
interface/controls, and presentation. The surface and corpus evidence also
demonstrates that these are many-to-many:

- one `NPC_` record can participate in appearance, AI, factions, inventory,
  combat, and quest behavior;
- one placed reference can affect world layout, activation, navigation, and
  quest progression;
- one script or native component can affect nearly any game area;
- one visual overhaul can touch plugin data, meshes, textures, and generated
  face data; and
- one location patch may resolve terrain and navigation while retaining both
  mods' intended presentation.

No record family, asset directory, filename, or analyzer should auto-assign a
complete affected area. Qualified fields and relationships may establish a
narrow area assignment; broader effects remain predictions with separate
confidence.

### F6 — The provisional “impact class” examples decompose cleanly

The accepted product examples mixed several useful ideas:

- startup/loading failure is an execution-availability consequence;
- save integrity is persistent-state integrity;
- progression failure is progression/access blockage;
- gameplay or behavioral failure is incorrect functional behavior;
- lost content is content/feature unavailability;
- “localized” is extent, not consequence;
- visual/audio/cosmetic conflict is presentation integrity; and
- maintenance/reproducibility is a non-runtime operational risk.

The proposed consequence taxonomy keeps those types but removes locality,
severity, and symptom wording from the codes. A consequence may have multiple
labels; for example, an unresolved placed-reference interaction may both block
progression and produce incorrect world behavior.

### F7 — “Gameplay scope” should become structured manifestation scope

One scalar cannot accurately compare:

- one NPC affected everywhere that NPC appears;
- every actor in one cell;
- one quest branch across the whole world;
- a visual defect across a worldspace;
- a session-only initialization failure; and
- a persistent save-state defect with bounded direct content but broad
  downstream dependencies.

The proposed effect-extent axis therefore has:

- **subject breadth**;
- **spatial breadth**;
- **persistence/lifecycle breadth**; and
- **causal propagation (blast radius)**.

Applicability conditions—installed options, player state, quest branch,
runtime version, or load order—remain evidence/claim conditions. Likelihood
remains confidence. Severity remains severity. They do not belong inside
extent.

### F8 — Classification belongs to claims and effects, not just mods

The correct classification targets are:

- a source claim for declared purpose/intended target;
- an effective contribution or typed observation for technical surface;
- a candidate/hypothesis/finding for predicted or supported affected area,
  consequence, and extent;
- an analyzer capability for supported/excluded taxonomy scope;
- a coverage population for what was attempted; and
- an evaluation case for exercised taxonomy regions.

A mod-detail page may aggregate those assignments, but should show their role:
“author declares,” “effective artifacts use,” “findings predict/establish,” and
“not evaluated.” It must not manufacture one canonical mod-type label.

Case aggregation also needs care. A case may summarize member classifications,
but it must not blindly union every area or consequence from every related mod.
The case-level labels describe the supported common cause and its effects;
member-level evidence remains inspectable.

### F9 — Cross-cutting, unknown, unsupported, unmapped, and not applicable differ

- **Cross-cutting** means several valid assignments or a supported relationship
  across facets. It is not a catch-all leaf code.
- **Unknown** means the axis applies, but available evidence cannot select a
  supported value.
- **Unsupported** means the selected analyzer/product capability cannot
  interpret the relevant semantics within its declared scope.
- **Unmapped** means evidence establishes a meaningful concept that the
  current taxonomy version cannot express. It is a taxonomy-defect signal.
- **Not applicable** means the axis does not apply to that target at that
  stage, such as consequence on a raw file observation.

“Other” should not silently absorb these states. Unknown/unsupported/unmapped
must retain reason, evidence, and affected population.

### F10 — Taxonomy revision must create derived reclassification, not rewrite history

Raw source passages, local observations, analyzer output, and finding
revisions remain immutable under their original snapshot/run provenance.
Every assignment stores its taxonomy version and target. A later taxonomy may
project historical evidence into new codes, but that creates a linked
reclassification with:

- original assignment and version;
- new assignment and version;
- mapping type (`equivalent`, `renamed`, `split`, `merged`, `retired`, or
  `no-equivalent`);
- classifier/adjudicator provenance; and
- any new uncertainty or evidence requirement.

Historical reports render their original meaning by default. A current-view
projection is allowed only when visibly labeled. Taxonomy revision alone does
not mutate an installation snapshot, source claim, analysis run, finding, case,
disposition, or readiness evaluation.

## 5. Proposed taxonomy specification

### 5.1 Identity and general rules

Proposed identity:

```text
taxonomy_id: infinium.skyrim-se.mod-impact-taxonomy
taxonomy_version: 0.1.0-proposed
target: Skyrim SE / accepted runtime target
status: proposed
```

Rules:

1. Codes are stable identifiers independent of display labels; a retired code
   is never reused for a different meaning.
2. Hierarchical parent codes support navigation and coverage rollups but are
   not a claim that children are mutually exclusive.
3. Assign the narrowest supported code and retain all independently supported
   cross-cutting codes.
4. Every assignment records classification role and applicability state
   separately from the code.
5. No assignment is derived solely from a hosting category, mod/file name,
   extension, directory, record signature, or analyzer.
6. A parent may be assigned when evidence supports the parent but not a child.
7. Unknown, unsupported, unmapped, and not-applicable are assignment states,
   not ordinary taxonomy leaf values.
8. Severity, confidence, symptoms, evidence authority, analyzer maturity,
   readiness, and review state are separate referenced objects.

Proposed release compatibility:

- a **patch** version changes only non-semantic wording, documentation, or
  metadata;
- a **minor** version adds codes or relationships without changing existing
  code meaning or historical parent rollups; and
- a **major** version changes a boundary, parent aggregation, split/merge
  meaning, or retirement in a way that can change interpretation.

Every persisted assignment records the exact version, including patch.
Acceptance should remove the `-proposed` suffix only through product-owner
review; it must not imply that every code is evaluated or every region is
supported.

### 5.2 Axis A — declared purpose and intended feature area

#### Purpose-kind facet

| Code | Meaning |
|---|---|
| `purpose.add-expand` | Add new content/capability or expand existing content |
| `purpose.replace-overhaul` | Replace or comprehensively rework an existing presentation/system/content set |
| `purpose.modify-tune` | Change, rebalance, or narrowly customize existing behavior/content |
| `purpose.fix-restore` | Correct a defect or restore intended/previous behavior |
| `purpose.integrate-patch` | Make separately supplied components work together or preserve selected intent across them |
| `purpose.configure-expose-choice` | Expose or supply user-selectable behavior/settings |
| `purpose.generate-precompute` | Produce derived runtime-consumed artifacts from inputs |
| `purpose.provide-runtime-framework` | Supply a runtime, library, framework, or shared service used by other mods |
| `purpose.provide-tool-workflow` | Supply authoring, installation, diagnostic, or maintenance workflow rather than direct player-facing content |
| `purpose.remove-disable` | Remove or disable content/behavior |

These values are multi-label. “Fix and overhaul,” “framework plus
configuration,” and “add content plus patch” are legitimate combinations.
`purpose.integrate-patch` says what the author claims, not that the patch is
effective or current.

#### Intended-target facet

The intended-target hierarchy uses a distinct `purpose-target.*` namespace:

- `purpose-target.runtime-session`
- `purpose-target.player-progression`
- `purpose-target.actors`
  - `purpose-target.actors.appearance-identity`
  - `purpose-target.actors.ai-packages`
  - `purpose-target.actors.factions-relationships`
- `purpose-target.quests`
- `purpose-target.world`
- `purpose-target.gameplay`
  - `purpose-target.gameplay.combat-action`
  - `purpose-target.gameplay.magic-effects`
  - `purpose-target.gameplay.stealth-crime`
  - `purpose-target.gameplay.items-inventory-economy`
  - `purpose-target.gameplay.crafting`
- `purpose-target.interface-controls`
- `purpose-target.presentation`
  - `purpose-target.presentation.visual`
  - `purpose-target.presentation.animation`
  - `purpose-target.presentation.audio-voice`
  - `purpose-target.presentation.text-localization`
- `purpose-target.mod-runtime-framework`
- `purpose-target.mod-creation-maintenance-workflow`

The similarly named affected-area codes below are not interchangeable. An
explicit versioned mapping may route a declared target to candidate affected
areas; it may never copy the claim into an established local impact.

### 5.3 Axis B — technical modification surface

#### Semantic-mechanism facet

| Parent/code | Meaning and examples |
|---|---|
| `surface.plugin-data` | Bethesda plugin records, fields, override chains, links, and record-attached metadata |
| `surface.asset` | Runtime-consumed content/presentation assets; assign a qualified child where possible |
| `surface.asset.model-geometry` | NIF geometry/model structures and other qualified model formats |
| `surface.asset.texture-material` | DDS/material/shader resource data and qualified typed references |
| `surface.asset.animation-behavior-morph` | Animation clips, behavior graphs, morphs, and related runtime-consumed data |
| `surface.asset.audio-voice` | Music, sound, dialogue audio, lip/voice companions where qualified |
| `surface.asset.interface` | SWF/GFX/menu/interface assets and their qualified dependencies |
| `surface.asset.localization-text` | String tables, translated text, fonts, and other runtime-consumed localization/text resources |
| `surface.logic` | Executable/interpreted game-extension logic; assign a qualified child where possible |
| `surface.logic.compiled-papyrus` | PEX declarations/instructions and qualified VMAD attachment/property/fragment relationships |
| `surface.logic.native-runtime` | Native plugins, runtime patchers, injected/proxy code, and exact static component relationships |
| `surface.configuration` | Runtime/tool-consumed settings, schema data, or rules; assign a qualified child where possible |
| `surface.configuration.game-profile` | Base game/profile settings with a qualified consumer/parser |
| `surface.configuration.component` | Versioned per-component settings and explicit schemas |
| `surface.configuration.runtime-rule-dsl` | Distribution, swap, or condition rules interpreted by a runtime framework |
| `surface.runtime-support-data` | Runtime-address/support tables and other qualified data consumed by native/framework components |

#### Realization/delivery facet

| Code | Meaning |
|---|---|
| `delivery.plugin-container` | Data carried in an effective plugin |
| `delivery.loose-data-file` | Effective loose file under Data or another qualified mapped namespace |
| `delivery.archive-member` | Effective member of a qualified active archive/provider order |
| `delivery.game-root-component` | Effective base-game-root file or relationship |
| `delivery.profile-or-external-config` | Profile, Documents, tool, or other qualified external configuration input |
| `delivery.mapped-or-secondary-root` | Effective contribution through a qualified MO2 mapper/secondary-root route |

Provider, winner, shadowed, hidden, deleted, unmanaged, merged, split, renamed,
and source-identity states remain snapshot/identity topology, not taxonomy
values. They attach to the surface observation.

#### Generated-runtime-artifact facet

Generated output is multi-labeled with its semantic mechanism:

- `surface.generated`
- `surface.generated.plugin`
- `surface.generated.behavior-animation`
- `surface.generated.mesh-morph`
- `surface.generated.lod-terrain-object-visual`
- `surface.generated.grass-cell-cache`
- `surface.generated.runtime-consumed-sidecar`

Generator inputs and selections normally classify under configuration;
generator logs, reports, progress state, and non-runtime intermediates remain
evidence/provenance rather than modification surfaces. A generated sidecar
receives `surface.generated.runtime-consumed-sidecar` only when a qualified
game/runtime consumer uses it to change effective behavior. “Generated”
provenance by itself remains an identity/provenance state outside the taxonomy.

### 5.4 Axis C — affected game system/content area

| Parent/code | Diagnostic meaning |
|---|---|
| `area.runtime-session` | Starting, loading, initialization, runtime service, session, or persistence boundaries |
| `area.runtime-session.bootstrap-loading` | Game/loader/framework startup and component admission |
| `area.runtime-session.save-persistence-lifecycle` | Save-persisted state and state continuity across new-game, install, update, removal, and regeneration events |
| `area.runtime-session.mod-framework-services` | SKSE/Papyrus/native/config/distribution services used by other mods |
| `area.player-progression` | Player identity, attributes, skills, perks, leveling, and player-state progression |
| `area.actors` | NPCs/creatures and their systemic behavior |
| `area.actors.appearance-identity` | Actor appearance, race/body/head/identity presentation |
| `area.actors.ai-packages` | AI data, schedules, packages, and action selection |
| `area.actors.factions-relationships` | Factions, relationships, crime/social affiliation, and ranks |
| `area.quests` | Authored/radiant narrative and progression systems |
| `area.quests.progression-objectives-aliases` | Quest stages/objectives/aliases and access to progression |
| `area.quests.dialogue-scenes-voice` | Dialogue, scenes, narrative text, and voice relationships |
| `area.quests.radiant-story-events` | Story Manager/radiant/event-driven content |
| `area.world` | Spatial game content and world simulation |
| `area.world.cells-worldspaces-locations` | Cells, worldspaces, interiors, locations, and their context |
| `area.world.placed-objects-activation` | Placed references, enable/linked-reference topology, doors, containers, activators |
| `area.world.navigation-encounters` | Navmesh/pathing, encounter zones, spawns, traps, and spatial AI access |
| `area.world.landscape-water-weather-lighting-lod` | Terrain, grass, water, climate/weather, lighting, and distant representation |
| `area.gameplay` | Player/actor rules not captured by a more specific content area |
| `area.gameplay.combat-action` | Combat, damage, weapons-in-use, action timing, and combat behavior |
| `area.gameplay.magic-effects` | Spells, effects, enchantments, shouts, powers, and delivery |
| `area.gameplay.stealth-crime` | Detection, sneaking, pickpocketing, lock interaction, crime, and bounty rules |
| `area.gameplay.items-inventory-economy` | Items/equipment/loot, inventory, containers, merchants, and economy |
| `area.gameplay.crafting` | Smithing, alchemy, enchanting, cooking, recipes, ingredients, and workbenches |
| `area.interface-controls` | Menus, HUD, maps/compass, input, camera controls, accessibility, and configuration UI |
| `area.presentation` | Player-perceived audiovisual/textual representation independent of functional consequence |
| `area.presentation.visual` | Models, textures, materials, lighting/effects, and visual composition |
| `area.presentation.animation` | Character/object animation and visual motion |
| `area.presentation.audio-voice` | Sound, music, ambience, and voice presentation |
| `area.presentation.text-localization` | Display text, subtitles, fonts, and localization |

This initial hierarchy is deliberately open to evidence-backed additions.
Physics, survival systems, vehicles/mounts, housing, followers, creature
ecosystems, and other concepts should be expressed through existing parents
only when that is semantically adequate; otherwise the classification is
`unmapped` and triggers a taxonomy proposal. It is better to expose one missing
concept than to hide it under a misleading leaf.

Multi-label is expected. A combat-animation issue can be both
`area.gameplay.combat-action` and `area.presentation.animation`; a voiced quest
can involve quest progression, dialogue, audio, and localization; an actor
appearance plugin that reverts packages can involve appearance and AI.

### 5.5 Axis D — consequence type

| Code | Meaning |
|---|---|
| `consequence.execution-unavailable` | Game/component/session cannot start, load, initialize, or continue through the affected execution boundary |
| `consequence.content-feature-unavailable` | Intended content or capability is missing, inaccessible, or unusable |
| `consequence.incorrect-functional-behavior` | A rule, relationship, or feature behaves differently from the supported intended result |
| `consequence.progression-access-blocked` | Quest, objective, location, actor interaction, or other required progression/access path cannot proceed |
| `consequence.state-persistence-integrity` | Persistent game/save/world state may become invalid, stale, corrupted, or unsafe across lifecycle change |
| `consequence.stability-failure` | Crash, hang, deadlock, runaway failure, or comparable runtime instability |
| `consequence.performance-resource-degradation` | A concrete documented or measured mechanism degrades performance/resources |
| `consequence.presentation-incoherence` | Visual, animation, audio, voice, text, or localization output is missing, inconsistent, or not as supported intent describes |
| `consequence.usability-control-degradation` | UI, input, camera, discoverability, readability, or control becomes materially harder or unusable |
| `consequence.reproducibility-maintenance-risk` | A specific setup, update, regeneration, provenance, or maintenance condition creates a grounded future-risk/advisory without established runtime breakage |

Cause is not a consequence code. “Missing master,” “stale patch,” “record
reversion,” “file conflict,” and “wrong version” describe mechanisms/evidence.
“Cosmetic” is not a consequence code or severity: the affected area is
presentation, the consequence is presentation incoherence, and severity is
assessed separately from user intent and extent.

### 5.6 Axis E — effect extent

#### Direct subject breadth

- `extent.subject.single-instance`
- `extent.subject.bounded-set`
- `extent.subject.type-or-category`
- `extent.subject.system-wide`
- `extent.subject.runtime-or-installation-wide`

#### Spatial breadth

- `extent.spatial.single-reference-or-point`
- `extent.spatial.cell-or-location`
- `extent.spatial.region-or-worldspace`
- `extent.spatial.world-global`
- `extent.spatial.nonspatial`

#### Persistence/lifecycle breadth

- `extent.persistence.event-only`
- `extent.persistence.while-condition-holds`
- `extent.persistence.current-session`
- `extent.persistence.save-persistent`
- `extent.persistence.installation-persistent`

#### Causal propagation / blast radius

- `extent.propagation.isolated-output`
- `extent.propagation.bounded-dependents`
- `extent.propagation.feature-wide`
- `extent.propagation.cross-feature`
- `extent.propagation.cross-system`
- `extent.propagation.runtime-or-installation-wide`

These facets are ordinal only within their own dimension and only when the
meaningful endpoints are comparable. A `cell-or-location` defect is not
automatically less important than a `type-or-category` defect. Unknown applies
independently to each facet. Exact applicability predicates remain outside the
taxonomy and accompany the estimate.

## 6. Relationships and proposed product schema

### 6.1 Core relationships

```text
external source revision
  -> supports declared-purpose claim
       -> purpose-kind assignment
       -> intended-target assignment

installation snapshot
  -> contains effective contribution
       -> technical-surface assignment(s)

claim + observation + qualified semantic relationship
  -> candidate/hypothesis/finding
       -> affected-area assignment(s)
       -> consequence assignment(s)
       -> extent subfacet assignment(s)
       -> separate severity, confidence, symptoms, evidence, and applicability

finding revisions with one supported cause
  -> case revision
       -> explicit case-level classifications
       -> inspectable member classifications
```

Purpose-to-area and surface-to-area maps are candidate-routing priors, not
facts. Area-to-consequence and consequence-to-validation maps are likewise
decision-support relationships. Every promoted conclusion still requires the
finding analyzer's evidence threshold.

### 6.2 Assignment record

A future product specification should define the equivalent of:

```text
TaxonomyAssignment
  assignment_id
  taxonomy_id
  taxonomy_version
  axis
  facet
  code?                       # absent for unknown/unsupported/etc.
  applicability_state        # assigned | unknown | unsupported |
                             # unmapped | not-applicable
  subject_type
  subject_id
  classification_role        # declared | observed | predicted | established
  evidence_refs[]
  applicability_condition_refs[]
  confidence_assessment_ref? # separate confidence object
  analyzer_or_adjudicator
  created_at
  reason
  supersedes_assignment_id?
  mapping_provenance?
```

The classification role states how the assignment relates to evidence; it is
not an authority or confidence scale and is independent of the subject
object's candidate, hypothesis, or finding state. `declared` on the purpose
axis requires applicable author-maintained evidence; `observed` requires
qualified local structure; `predicted` marks an evidence-bounded assignment
whose exact area, consequence, or extent is not established; and `established`
means that the individual assignment meets its applicable support threshold.
A supported finding may therefore contain an established consequence alongside
a still-predicted affected area, persistence, or blast radius, each with its
own confidence and evidence. Promotion of a hypothesis to a finding does not
silently promote every member assignment. Curated compatibility/requirement
claims retain their own source authority and must not be relabeled as
author-declared purpose.

### 6.3 Analyzer declaration

Each analyzer declaration should include:

- accepted taxonomy version;
- eligible input populations by technical surface;
- supported and excluded affected areas, consequences, and extent facets;
- exact classification roles it may emit;
- fields/relationships required before it may assign a narrow child;
- unknown/unsupported/unmapped behavior;
- coverage denominators for every declared population; and
- linked positive, matched-negative, boundary, malformed, cross-cutting, and
  unknown/unsupported evaluation cases.

An analyzer may discover evidence outside its semantic scope but must emit a
gap rather than borrow another analyzer's classification.

## 7. Corpus classification examples and counterexamples

### 7.1 Incomplete controlled-real EVAL-0016 candidate

The RQ-025 corpus retains AI Overhaul `1.8.6`, Children of the Pariah archive
`1.2.3.6`, and the latter archive's exact author-provided AI Overhaul patch as
an incomplete candidate for the first real NPC scope-incongruent-reversion
case. It is not yet selected or qualified as EVAL-0016. The retained archives
and exact known hashes preserve useful candidate inputs, but they do not make
the exact case locally reproducible: USSEP transformation provenance, Fishing
bytes, exact FOMOD choices, the mandatory loose FaceGen closure and archive
independence, independent byte/semantic truth, and an atomic rerun remain
qualification gates.

Provisional classification:

- AI Overhaul's declared purpose includes `purpose.replace-overhaul` with
  `purpose-target.actors.ai-packages`;
- Children of the Pariah declares `purpose.replace-overhaul` with
  `purpose-target.actors.appearance-identity` and
  `purpose-target.presentation.visual`;
- their effective artifacts use `surface.plugin-data` plus actor presentation
  assets; the exact exercised field/asset allowlist remains an EVAL-0052 and
  facegen-provenance gate;
- the risky interaction may affect
  `area.actors.appearance-identity` and `area.actors.ai-packages`;
- a stale/reverted AI field would have
  `consequence.incorrect-functional-behavior`, while a mismatched face result
  may have `consequence.presentation-incoherence`; and
- subject breadth is a bounded set of overlapping actors, while downstream
  propagation depends on the exact packages/factions/quest relations changed.

Counterexample: the same structural overlap with the applicable patch
preserving both intended changes is not a finding. The label
`purpose.integrate-patch` does not prove patch effectiveness; the exact patch
records, versions, winner, and matched-negative expectation must do so.

### 7.2 Non-NPC placement-reconciliation lead; not EVAL-0017

The RQ-025 corpus retains Ryn's Standing Stones `1.5`, Ryn's Farms `2.0`, and
official patch `1.1` as a non-NPC placement-reconciliation lead, not as the
selected EVAL-0017 gate. The two source plugins share no `REFR` FormKeys in the
research probe; the patch overrides four records originating in Ryn's Farms
and changes their placement when Standing Stones is present. That is useful
taxonomy and candidate-routing evidence, but it does not instantiate the
accepted stale-value/topology-reversion mechanism. Exact author applicability,
a bounded symptom, an independently qualified placement analyzer, and
independent byte truth would be required even for a distinct placement case.

Provisional classification:

- both principal mods declare world/location expansion or overhaul purposes;
- the official-patch relationship is evidence for a candidate
  `purpose.integrate-patch` claim, pending the exact author-source
  applicability check required by RESEARCH-0020;
- the effective technical surfaces include `surface.plugin-data` and may
  include world/presentation assets depending on the pinned file manifest;
- affected areas include `area.world.cells-worldspaces-locations`,
  `area.world.placed-objects-activation`, and potentially
  `area.presentation.visual`; navigation, terrain, collision, and wider world
  semantics remain unsupported for this exact placement-only candidate;
- consequence remains unknown until the bounded symptom/materiality gate
  passes; placement evidence alone must not be promoted to
  `consequence.incorrect-functional-behavior` or
  `consequence.presentation-incoherence`;
- direct spatial breadth is bounded to the overlapping farm/location region,
  while causal propagation remains unknown.

Counterexample: two mods editing the same cell or landscape are not
automatically incompatible. A load order or patch that preserves the supported
combined topology is a negative. An archived/outdated patch description also
cannot establish current applicability; exact version and current author
guidance remain part of ground truth. This lead is also evidence that
candidate routing cannot require a direct same-FormKey conflict: the relevant
pair relationship is represented by patch-owned placement reconciliation.

### 7.3 Cross-surface examples

| Example | Correct multi-facet classification | Invalid shortcut |
|---|---|---|
| Generated LOD output | Generated visual/LOD surface plus the exact plugin/asset mechanisms; world/environment and presentation areas when supported | “Generated-output mod” proves world area or stale output |
| BodySlide output | Generated mesh/morph plus model/geometry realization; actor/item presentation depending exact target | Tool name proves every affected actor or consequence |
| SPID/KID/BOS rule | Runtime-rule DSL; affected areas depend on resolved distributed forms/targets | INI extension or framework name proves actor/gameplay impact |
| Compiled PEX | Compiled-Papyrus surface; area/consequence unknown until VMAD/API/semantic evidence | Script name, instruction count, or native call proves behavior/performance |
| Root proxy DLL | Native-runtime plus game-root delivery; possible runtime/session effects after exact relationship rules | `dxgi.dll` filename proves ReShade, ENB, compatibility, or severity |
| Texture overwrite | Texture/material surface and a narrow presentation area when target semantics are qualified | Any overwrite is a finding or cosmetic conflict |
| Quest mod | Declared add/expand target; plugin, script, voice, localization, and asset surfaces may coexist; quest/world/actor areas may all apply | Hosting “Quests” category completely describes implementation/impact |
| Compatibility patch | Integrate/patch purpose; actual surface/area follows its contents; consequence only if the patch is missing, stale, ineffective, or overwritten | Patch filename proves resolution |

## 8. Corpus coverage and unevaluated regions

### 8.1 What the combined evidence supports

The combined surface evidence and corpus design observes or explicitly
requires:

- declared overhaul, modification, integration/patch, framework/configuration,
  and generated-output purposes;
- plugin data, multiple asset classes, compiled Papyrus, native/root,
  configuration DSL, and generated-output surfaces;
- actor appearance/AI and non-NPC world/placed-reference areas, with
  navigation/landscape retained as planned or unsupported boundaries;
- single/bounded and location-bounded extent examples;
- intentional/preserved matched negatives in the synthetic design and
  incomplete NPC candidate;
- cross-layer, multi-surface, unknown, and unsupported outcomes; and
- an author-supplied NPC compatibility patch and an official exterior patch,
  whose exact applicability and effectiveness remain unqualified under
  RESEARCH-0020.

This evidence is sufficient to reject one-dimensional mod categories and to
specify the five-axis/faceted data model. It is not evidence that the planned
synthetic matrix has been implemented, that the NPC candidate has qualified,
or that the exterior lead satisfies EVAL-0017.

### 8.2 Materially unevaluated or under-evaluated regions

The current corpus does **not** justify a claim of complete taxonomy coverage.
At minimum, these regions remain unevaluated or thin:

- save-persistent state and install/update/removal lifecycle interactions;
- deep quest stages, objectives, dialogue, scenes, and radiant-story behavior;
- combat, magic, stealth/crime, economy, and crafting semantics beyond planned
  synthetic cases;
- interface/input/camera and localization interactions;
- audio/voice companion completeness;
- native multi-plugin behavioral compatibility beyond static relationship
  rules;
- Papyrus runtime dispatch, persistence, scheduling, and performance;
- archive-backed asset absence until effective archive precedence is
  qualified;
- navigation geometry and broad cell/world semantics;
- physics and complex animation-behavior interactions;
- grounded performance/resource consequences;
- persistent-save integrity consequences;
- runtime- or installation-wide blast radius; and
- taxonomy behavior on total conversions, other runtimes, managers, and games,
  which are outside the accepted target.

These are coverage gaps, not evidence that the proposed hierarchy is wrong or
complete. A real example that cannot be represented accurately must produce
`unmapped` and trigger revision.

### 8.3 Proposed evaluation coverage map

Every evaluation case should declare:

- taxonomy version;
- expected assignments and classification roles;
- expected unknown/unsupported/unmapped/not-applicable states;
- positive, matched-negative, and boundary relationship;
- whether it tests classification correctness, analyzer correctness, or both;
- exact evidence supporting each assignment; and
- which taxonomy combinations remain untested.

EVAL-0086 should include at least:

1. one declared-purpose/actual-area mismatch;
2. one single-purpose multi-surface mod;
3. one shared surface affecting materially different areas;
4. one cross-cutting multi-area finding;
5. one unknown-purpose case;
6. one supported surface with unsupported semantics;
7. one concept intentionally reported as unmapped;
8. one not-applicable raw observation;
9. one consequence with low severity and one severe consequence of the same
   type;
10. one localized direct effect with cross-system propagation;
11. one hosting-category counterexample;
12. one record-family counterexample;
13. historical rendering under the original taxonomy version; and
14. a split/merge reclassification that leaves raw evidence unchanged.

The incomplete EVAL-0016 candidate and non-EVAL-0017 placement lead provide
controlled-real actor/AI and world/placed-reference observations. They do not
satisfy the two real-case gates. Synthetic fixtures must provide the broader
axes and adversarial states without using those real names in production
rules, and RQ-025 must still select a valid EVAL-0017 replacement.

## 9. Product-use map

| Product consumer | Required taxonomy use |
|---|---|
| Effective installation | Classify observed technical surfaces while retaining provider/container/topology facts and unsupported semantics separately |
| Analyzer contracts | Declare supported/excluded surfaces, areas, consequences, and extent facets under one exact taxonomy version |
| Candidate routing | Use purpose-to-area and surface-to-area relationships as evidence-bearing routing priors, never as findings |
| Documentation intelligence | Attach purpose/intended-target assignments to exact cited claims and applicability conditions |
| Findings and cases | Show surface, affected area, consequence, and extent beside—not instead of—severity, confidence, symptoms, evidence, and disposition |
| Summary/navigation/mod detail | Offer faceted filtering and drill-down; aggregate by role without assigning a monolithic mod type |
| Coverage/readiness | Report versioned labeled denominators and unevaluated areas; unknown/unsupported populations affect readiness under policy |
| Review priority/depth | Combine taxonomy with separate severity, confidence, user intent, maturity, validation cost, and reversibility |
| Change impact | Route changed effective contributions through dependency edges and taxonomy maps; taxonomy does not replace dependency proof |
| Remediation/validation | Select bounded checks from consequence, area, extent, and evidence; purpose alone cannot authorize a resolution |
| Evaluation/anti-overfitting | Stratify cases and expose empty cells; retain matched negatives across materially different areas/surfaces |
| Roadmap | Prioritize measured risk and empty high-value coverage regions, not record count, hosting popularity, or first-fixture convenience |
| Architecture/integrations | Report which taxonomy regions a component enables without treating adapter ownership as game-area ownership |
| History/exports | Persist the original taxonomy version and visibly label any current projection |

## 10. Alternatives evaluated

### Alternative A — One hierarchical mod-type taxonomy

Rejected. Cross-layer mods, frameworks, patches, generated output, and
multi-area interactions would require arbitrary primary labels or combinatorial
leaves. It also encourages intent to be inferred from files.

### Alternative B — Use Nexus or another hosting taxonomy

Rejected as product authority. Hosting labels are useful search/acquisition
metadata, can be inconsistent or incomplete, and do not establish exact
purpose, installed options, surfaces, or affected areas.

### Alternative C — Use record families and file extensions

Rejected beyond candidate discovery and technical-surface evidence. A record
or format can participate in multiple systems; scripts/config/native code can
affect almost any area; and one game feature spans many formats.

### Alternative D — Use analyzer/catalog sections as the taxonomy

Rejected. Requirements, LOOT, record, asset, configuration, lifecycle, and
documentation analyzers are product responsibility boundaries. One analyzer
may cover several surfaces/areas, and one area may require several analyzers.

### Alternative E — Combine consequence with severity or extent

Rejected. The same consequence type can be a minor bounded defect or a major
global one. Locality is extent; credibility is confidence; impact magnitude is
severity.

### Alternative F — Free-text labels only

Rejected for coverage, evaluation, filtering, and historical comparison. Raw
source terms and human explanation should be retained, but normalized
versioned codes are necessary. `Unmapped` prevents controlled vocabularies from
pretending to be complete.

### Alternative G — Closed exhaustive first version

Rejected. The corpus is intentionally varied but small, and several important
areas have no controlled-real evidence. The first version should be stable
enough to implement while allowing evidence-backed additions and explicit
unmapped states.

## 11. Contrary evidence, uncertainty, and limits

- The publisher manual is a player-facing guide, not a mod-impact ontology.
  It omits many engine/runtime/mod-framework systems.
- The preserved Creation Kit reference is technically useful but historical,
  community-hosted, and incomplete. Its categories cannot be copied directly.
- The Wave C surface-survey recommendations are accepted, but they are not
  accepted analyzer support or exhaustive inventories.
- The controlled-real corpus has one incomplete NPC candidate and one non-NPC
  discovery lead, not two qualified selections. It deliberately avoids
  claiming representativeness.
- Exact code boundaries below the broad affected-area parents will need
  revision as quest, interface, audio, magic, combat, and lifecycle fixtures
  are built.
- Some concepts can reasonably sit under more than one parent. Multi-label is
  intentional; parent choice should optimize stable product meaning rather
  than enforce a brittle tree.
- Extent is often predictive. Static evidence may establish direct subject or
  spatial breadth while causal propagation and persistence remain unknown.
- A source can declare purpose incompletely or inaccurately. The claim remains
  evidence of declared intent, not proof of implementation.
- “Actual affected area” may remain unknowable without runtime evidence.
  Infinium must distinguish observed qualified relationships, predictions, and
  established effects.
- The exact migration policy, storage schema, UI labels, and threshold for
  adding a code remain product/architecture work after taxonomy acceptance.
- This taxonomy does not determine readiness thresholds, severity, analyzer
  maturity, or scan presets.

## 12. Recommendation and confidence

### Recommended answer

Adopt a versioned, hierarchical-but-faceted, multi-label taxonomy with five
independent axes:

1. declared purpose, split into purpose kind and intended target;
2. technical modification surface, split into semantic mechanism,
   realization/delivery, and generated-runtime-artifact facets;
3. affected game system/content area;
4. consequence type; and
5. effect extent, split into subject, spatial, persistence, and causal
   propagation facets.

Use explicit assignment states for unknown, unsupported, unmapped, and not
applicable. Represent cross-cutting behavior through multiple supported
assignments and typed relationships. Persist the taxonomy version and preserve
raw claims/observations so later versions create linked reclassifications
instead of rewriting history.

### Confidence

- **High** that a single mod-type/category model is invalid.
- **High** that purpose, surface, affected area, consequence, severity,
  confidence, symptoms, and extent must remain distinct.
- **High** that technical surface and effect extent require the proposed
  subfacets to avoid observed conflation.
- **High** that multi-label, unknown, unsupported, unmapped, and versioned
  historical behavior are required.
- **Medium-high** in the proposed top-level purpose, surface, area, and
  consequence code sets.
- **Medium** in the exact child granularity and display names because several
  game systems remain thinly represented in controlled-real evidence.
- **Low/unsupported** for any claim that this is a complete taxonomy of all
  Skyrim mods or future target games.

### Preconditions for acceptance

1. independent semantic review of this report and RESEARCH-0020;
2. product-owner review of the five axes, code meanings, open-world behavior,
   and the replacement of scalar gameplay scope;
3. creation and acceptance of a normative product-taxonomy specification;
4. reviewed updates to every affected accepted product document identified by
   the dependency map;
5. reviewed EVAL-0086 and corpus coverage specifications;
6. stable machine-readable schema/migration rules before implementation; and
7. an accepted M1 plan linking exact supported taxonomy regions to analyzers
   and evaluation.

## 13. Exact downstream work enabled and disposition

The owner accepted the taxonomy. Items 1 through 5 and the affected product
contract updates are applied; machine-readable implementation schema,
qualification, and M1 planning remain pending:

1. create `docs/product/mod-impact-taxonomy.md` with the accepted taxonomy
   version and change policy (applied);
2. update `docs/README.md` to place that specification in the authoritative
   reading order;
3. update the accepted product documents listed in the
   [dependency map](../taxonomy-dependency-map.md) to use the accepted code
   names, replace provisional “domain/impact/gameplay scope” wording, and
   preserve their existing meaning;
4. mark the dependency map as satisfied by the accepted taxonomy version while
   retaining it as the product-consumer inventory;
5. refine EVAL-0086 using section 8.3 and require taxonomy metadata in
   EVAL-0016, EVAL-0017, and future fixture specifications;
6. add a versioned machine-readable taxonomy/assignment schema and migration
   fixture to the M1 plan;
7. require analyzer declarations and coverage output to reference exact
   supported codes and assignment roles;
8. add UI designs for role-labeled facet chips, filters, unknown/unsupported
   gaps, and historical/current projections;
9. schedule corpus expansion first into the unevaluated high-value regions in
   section 8.2; and
10. create an ADR only if a later implementation mechanism—storage,
    classification service, or migration architecture—meets ADR criteria.
    The taxonomy itself is a product specification, not an ADR.

## 14. Accepted RQ-036 disposition

> **Resolved for M0.** On 2026-07-25 the project owner accepted
> `infinium.skyrim-se.mod-impact-taxonomy/0.1.0`. The normative product
> specification, dependency-map integration, affected product-document
> updates, and EVAL-0086 specification review are complete.

This resolves RQ-036 without claiming exhaustive taxonomy coverage, analyzer
support, or evaluation execution. RESEARCH-0034/0035 subsequently completed
the separate RQ-023/RQ-025 Gate C prerequisites; their candidate qualification
still does not claim evaluation execution.

## 15. Requirements-and-evidence traceability

| Requirement/decision | Evidence and proposal | Residual work |
|---|---|---|
| FIND-001 | Five independent axes, assignment schema, separate severity/confidence/evidence, versioning | Accepted in normative product spec version `0.1.0` |
| FIND-003 | Structured extent with causal propagation; symptoms remain separate | Calibrated prediction/evaluation |
| FIND-007 through FIND-014 | Taxonomy remains separate from readiness, review, suppression, advisory, and lineage | Product integration and UI policy |
| COVER-001 | Unknown/unsupported/unmapped states and explicit analyzer populations | Exact per-analyzer denominators |
| COVER-002 | Versioned multidimensional coverage and evaluation map | Machine-readable coverage schema |
| COVER-003 | Unevaluated areas remain visible and may affect readiness by separate policy | Readiness-policy integration |
| ANALYSIS-016 | Exact taxonomy-aware analyzer declaration requirements | M1 declarations and EVAL-0065 |
| ANALYSIS-017 | Purpose/surface maps are candidate priors, not findings; no all-pairs need | RQ-035 benchmark/design |
| ANALYSIS-018 | Taxonomy routes explanations while dependency graph proves invalidation | Later change-impact implementation |
| ADR-0001 | Claims, observations, predictions, findings, and recommendations retain separate authority | Provider/analyzer implementation |
| ADR-0002/ADR-0010 | Immutable evidence, taxonomy-version provenance, linked reclassification | Storage/migration architecture |
| ADR-0004/ADR-0009 | Skyrim-specific hierarchy; no unsupported cross-target abstraction | Revisit only for a second target |
| RESEARCH-0014 through 0019 | Empirical native, generated, configuration, script, asset, and record surfaces | Named analyzer qualification remains separate |
| RESEARCH-0020 | Exact known identities for an initially incomplete actor/AI candidate and a world/placed-reference discovery lead | RESEARCH-0035 repaired EVAL-0016 and selected a distinct placed-reference EVAL-0017 replacement |
| EVAL-0086 | Section 8.3 classification/versioning matrix | Specification accepted; execution pending |
| Gate C | Distinct proposed taxonomy, real/synthetic examples, cross-cutting states, explicit gaps | Taxonomy version `0.1.0`, affected-document integration, and EVAL-0032/EVAL-0086 specification review are accepted; RESEARCH-0034/0035 completed the remaining RQ-023/RQ-025 qualification prerequisites. |

## 16. Conclusion

Infinium should not answer “what kind of mod is this?” with one label. It
should answer a sequence of inspectable questions:

```text
What does the author say it is for?
  -> What effective technical mechanisms does it use?
     -> What Skyrim systems/content may those mechanisms affect here?
        -> What kind of consequence may follow?
           -> How broad is direct manifestation and downstream propagation?
```

Each answer has its own evidence, uncertainty, and taxonomy role. The proposed
five-axis model preserves those distinctions, handles cross-layer real cases,
keeps unknown and unsupported regions honest, and gives analyzers, findings,
coverage, UI, evaluation, and history one coherent vocabulary without turning
the first NPC or world fixture into the product's scope.
