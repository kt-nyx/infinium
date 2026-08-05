# Skyrim SE mod-impact taxonomy

Status: Accepted  
Taxonomy ID: `infinium.skyrim-se.mod-impact-taxonomy`  
Version: `0.1.0`  
Accepted: 2026-07-25  
Last reviewed: 2026-08-05

Accepted clarification: ADR-0028 binds the canonical persisted identifiers used
by the bounded M1 projection and the hybrid-emission rule below. It does not
change taxonomy version `0.1.0` or any existing code meaning.

## Purpose

This is Infinium's normative vocabulary for describing what a mod claims to
do, how the selected installation is changed, where an effect may manifest,
what kind of consequence may follow, and how broad that effect may be.

The taxonomy is Skyrim SE-specific. It is deliberately open to evidence-backed
extension and does not claim complete coverage of every mod or game system.
Its research basis, examples, counterexamples, and known coverage gaps are in
[RESEARCH-0021](../research/investigations/RESEARCH-0021-skyrim-mod-impact-taxonomy.md).

## Core rules

1. The five axes below are independent. No code on one axis implies a code,
   finding, severity, or confidence on another.
2. Classification is multi-label. Assign the narrowest supported code and
   retain every independently supported cross-cutting code.
3. A parent may be assigned when evidence supports it but not a child.
4. Codes are stable identifiers independent of display labels. A retired code
   is never reused for a different meaning.
5. No assignment may be derived solely from a hosting category, mod or file
   name, extension, directory, record signature, or analyzer ownership.
6. Purpose-to-area, surface-to-area, area-to-consequence, and
   consequence-to-validation mappings are routing priors, not established
   facts.
7. Severity, confidence, symptoms, evidence authority, analyzer maturity,
   readiness, review state, identity/topology, and operational job state remain
   separate.
8. Persist raw source claims and observations independently from derived
   classifications so later taxonomy versions can reclassify without
   rewriting evidence or history.

## Applicability and classification role

Every classification records one applicability state:

- `assigned`: the evidence supports a taxonomy code;
- `unknown`: the axis applies, but current evidence cannot select a code;
- `unsupported`: the current analyzer or product capability cannot determine
  the code;
- `unmapped`: evidence establishes a meaningful concept that version `0.1.0`
  does not represent adequately; or
- `not-applicable`: the axis does not apply to this subject.

Every assignment also records one role:

- `declared`: applicable author-maintained evidence states the purpose or
  intended target;
- `observed`: qualified local evidence establishes a technical surface;
- `predicted`: evidence supports a bounded possible area, consequence, or
  extent but has not established it; or
- `established`: this individual classification meets its applicable support
  threshold.

The role is neither an evidence-authority scale nor a confidence scale.
Promoting a hypothesis to a finding does not promote all of its assignments.

## Axis A — declared purpose and intended feature area

Purpose is an author-supported claim. It is not proof of effective local
impact.

### Purpose kind

| Code | Meaning |
|---|---|
| `purpose.add-expand` | Add new content or capability, or expand existing content |
| `purpose.replace-overhaul` | Replace or comprehensively rework an existing presentation, system, or content set |
| `purpose.modify-tune` | Change, rebalance, or narrowly customize existing behavior or content |
| `purpose.fix-restore` | Correct a defect or restore intended or previous behavior |
| `purpose.integrate-patch` | Make separately supplied components work together or preserve selected intent across them |
| `purpose.configure-expose-choice` | Expose or supply user-selectable behavior or settings |
| `purpose.generate-precompute` | Produce derived runtime-consumed artifacts from inputs |
| `purpose.provide-runtime-framework` | Supply a runtime, library, framework, or shared service used by other mods |
| `purpose.provide-tool-workflow` | Supply an authoring, installation, diagnostic, or maintenance workflow rather than direct player-facing content |
| `purpose.remove-disable` | Remove or disable content or behavior |

`purpose.integrate-patch` does not prove that a patch is effective or current.

### Intended target

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

Similarly named `purpose-target.*` and `area.*` codes are not
interchangeable. A mapping may route a declared target to candidate affected
areas but may not copy the claim into an established local effect.

## Axis B — technical modification surface

Technical surfaces describe qualified effective local state.

### Semantic mechanism

| Code | Meaning |
|---|---|
| `surface.plugin-data` | Bethesda plugin records, fields, override chains, links, and record-attached metadata |
| `surface.asset` | Runtime-consumed content or presentation assets |
| `surface.asset.model-geometry` | NIF geometry/model structures and other qualified model formats |
| `surface.asset.texture-material` | DDS, material, shader-resource data, and qualified typed references |
| `surface.asset.animation-behavior-morph` | Animation clips, behavior graphs, morphs, and related runtime-consumed data |
| `surface.asset.audio-voice` | Music, sound, dialogue audio, and qualified voice or lip companions |
| `surface.asset.interface` | SWF, GFX, menu, interface assets, and their qualified dependencies |
| `surface.asset.localization-text` | String tables, translated text, fonts, and other runtime-consumed localization or text resources |
| `surface.logic` | Executable or interpreted game-extension logic |
| `surface.logic.compiled-papyrus` | PEX declarations or instructions and qualified VMAD attachment, property, or fragment relationships |
| `surface.logic.native-runtime` | Native plugins, runtime patchers, injected or proxy code, and exact static component relationships |
| `surface.configuration` | Runtime- or tool-consumed settings, schema data, or rules |
| `surface.configuration.game-profile` | Base game or profile settings with a qualified consumer or parser |
| `surface.configuration.component` | Versioned per-component settings and explicit schemas |
| `surface.configuration.runtime-rule-dsl` | Distribution, swap, or condition rules interpreted by a runtime framework |
| `surface.runtime-support-data` | Runtime-address or support tables and other qualified data consumed by native or framework components |

### Realization and delivery

| Code | Meaning |
|---|---|
| `delivery.plugin-container` | Data carried in an effective plugin |
| `delivery.loose-data-file` | Effective loose file under Data or another qualified mapped namespace |
| `delivery.archive-member` | Effective member of a qualified active archive and provider order |
| `delivery.game-root-component` | Effective base-game-root file or relationship |
| `delivery.profile-or-external-config` | Profile, Documents, tool, or other qualified external configuration input |
| `delivery.mapped-or-secondary-root` | Effective contribution through a qualified MO2 mapper or secondary-root route |

Provider, winner, shadowed, hidden, deleted, unmanaged, merged, split, renamed,
and source-identity states remain snapshot and identity topology.

### Generated runtime artifact

- `surface.generated`
- `surface.generated.plugin`
- `surface.generated.behavior-animation`
- `surface.generated.mesh-morph`
- `surface.generated.lod-terrain-object-visual`
- `surface.generated.grass-cell-cache`
- `surface.generated.runtime-consumed-sidecar`

Generated output is multi-labeled with its semantic mechanism. Generator
configuration, logs, reports, progress state, and non-runtime intermediates
remain configuration or provenance rather than generated modification
surfaces.

## Axis C — affected game system or content area

| Code | Diagnostic meaning |
|---|---|
| `area.runtime-session` | Starting, loading, initialization, runtime service, session, or persistence boundaries |
| `area.runtime-session.bootstrap-loading` | Game, loader, or framework startup and component admission |
| `area.runtime-session.save-persistence-lifecycle` | Save-persisted state and continuity across new-game, install, update, removal, and regeneration events |
| `area.runtime-session.mod-framework-services` | SKSE, Papyrus, native, configuration, or distribution services used by other mods |
| `area.player-progression` | Player identity, attributes, skills, perks, leveling, and player-state progression |
| `area.actors` | NPCs, creatures, and their systemic behavior |
| `area.actors.appearance-identity` | Actor appearance, race, body, head, identity, and presentation |
| `area.actors.ai-packages` | AI data, schedules, packages, and action selection |
| `area.actors.factions-relationships` | Factions, relationships, crime or social affiliation, and ranks |
| `area.quests` | Authored or radiant narrative and progression systems |
| `area.quests.progression-objectives-aliases` | Quest stages, objectives, aliases, and access to progression |
| `area.quests.dialogue-scenes-voice` | Dialogue, scenes, narrative text, and voice relationships |
| `area.quests.radiant-story-events` | Story Manager, radiant, or event-driven content |
| `area.world` | Spatial game content and world simulation |
| `area.world.cells-worldspaces-locations` | Cells, worldspaces, interiors, locations, and their context |
| `area.world.placed-objects-activation` | Placed references, enable or linked-reference topology, doors, containers, and activators |
| `area.world.navigation-encounters` | Navmesh, pathing, encounter zones, spawns, traps, and spatial AI access |
| `area.world.landscape-water-weather-lighting-lod` | Terrain, grass, water, climate, weather, lighting, and distant representation |
| `area.gameplay` | Player or actor rules not captured by a more specific content area |
| `area.gameplay.combat-action` | Combat, damage, weapons in use, action timing, and combat behavior |
| `area.gameplay.magic-effects` | Spells, effects, enchantments, shouts, powers, and delivery |
| `area.gameplay.stealth-crime` | Detection, sneaking, pickpocketing, lock interaction, crime, and bounty rules |
| `area.gameplay.items-inventory-economy` | Items, equipment, loot, inventory, containers, merchants, and economy |
| `area.gameplay.crafting` | Smithing, alchemy, enchanting, cooking, recipes, ingredients, and workbenches |
| `area.interface-controls` | Menus, HUD, maps or compass, input, camera controls, accessibility, and configuration UI |
| `area.presentation` | Player-perceived audiovisual or textual representation independent of functional consequence |
| `area.presentation.visual` | Models, textures, materials, lighting, effects, and visual composition |
| `area.presentation.animation` | Character or object animation and visual motion |
| `area.presentation.audio-voice` | Sound, music, ambience, and voice presentation |
| `area.presentation.text-localization` | Display text, subtitles, fonts, and localization |

This hierarchy is open. A well-supported concept that does not fit is
`unmapped`, not forced under a misleading leaf. Physics, survival systems,
vehicles or mounts, housing, followers, creature ecosystems, and other
concepts may require later evidence-backed additions.

## Axis D — consequence type

| Code | Meaning |
|---|---|
| `consequence.execution-unavailable` | Game, component, or session cannot start, load, initialize, or continue through the affected execution boundary |
| `consequence.content-feature-unavailable` | Intended content or capability is missing, inaccessible, or unusable |
| `consequence.incorrect-functional-behavior` | A rule, relationship, or feature behaves differently from the supported intended result |
| `consequence.progression-access-blocked` | A required quest, objective, location, actor interaction, or other progression or access path cannot proceed |
| `consequence.state-persistence-integrity` | Persistent game, save, or world state may become invalid, stale, corrupted, or unsafe across a lifecycle change |
| `consequence.stability-failure` | Crash, hang, deadlock, runaway failure, or comparable runtime instability |
| `consequence.performance-resource-degradation` | A concrete documented or measured mechanism degrades performance or resources |
| `consequence.presentation-incoherence` | Visual, animation, audio, voice, text, or localization output is missing, inconsistent, or contrary to supported intent |
| `consequence.usability-control-degradation` | UI, input, camera, discoverability, readability, or control becomes materially harder or unusable |
| `consequence.reproducibility-maintenance-risk` | A specific setup, update, regeneration, provenance, or maintenance condition creates a grounded future risk without established runtime breakage |

Causes such as missing masters, stale patches, record reversions, file
conflicts, and wrong versions are mechanisms or evidence, not consequences.
“Cosmetic” is neither a consequence nor a severity.

## Axis E — effect extent

Effect extent is faceted. Values are ordinal only within a facet and when their
endpoints are comparable.

### Direct subject breadth

- `extent.subject.single-instance`
- `extent.subject.bounded-set`
- `extent.subject.type-or-category`
- `extent.subject.system-wide`
- `extent.subject.runtime-or-installation-wide`

### Spatial breadth

- `extent.spatial.single-reference-or-point`
- `extent.spatial.cell-or-location`
- `extent.spatial.region-or-worldspace`
- `extent.spatial.world-global`
- `extent.spatial.nonspatial`

### Persistence and lifecycle breadth

- `extent.persistence.event-only`
- `extent.persistence.while-condition-holds`
- `extent.persistence.current-session`
- `extent.persistence.save-persistent`
- `extent.persistence.installation-persistent`

### Causal propagation or blast radius

- `extent.propagation.isolated-output`
- `extent.propagation.bounded-dependents`
- `extent.propagation.feature-wide`
- `extent.propagation.cross-feature`
- `extent.propagation.cross-system`
- `extent.propagation.runtime-or-installation-wide`

Unknown applies independently to each facet. Applicability predicates accompany
the estimate and are not taxonomy codes.

## Canonical bounded-M1 persisted axis and facet identifiers

The bounded M1 Bethesda projection uses these stable pairs:

| Axis | Facet |
|---|---|
| `declared-purpose-and-intended-feature-area` | `purpose-kind` |
| `technical-modification-surface` | `semantic-mechanism` |
| `technical-modification-surface` | `realization-and-delivery` |
| `affected-game-system-or-content-area` | `affected-area` |
| `consequence-type` | `consequence-type` |
| `effect-extent` | `direct-subject-breadth` |
| `effect-extent` | `spatial-breadth` |
| `effect-extent` | `persistence-and-lifecycle-breadth` |
| `effect-extent` | `causal-propagation-or-blast-radius` |

The bounded M1 Bethesda analyzer uses hybrid emission. Every plugin-record
contribution emits its required technical `surface.plugin-data` and
`delivery.plugin-container` assignments. It adds area, consequence, extent,
purpose, or delivery
assignments only where decoded semantic or provider evidence supports them.
It does not emit a mandatory null tuple for every pair. Explicit `unknown`,
`unsupported`, `unmapped`, or `not-applicable` tuples are retained when they
communicate a real required conclusion rather than filling a matrix.

Every declared FaceGen loose-provider chain is a taxonomy subject, including a
single-provider chain; an enabled-plugin list alone is not a generic provider
subject. Record signatures, names, and `EDID` values are insufficient by
themselves to establish purpose, affected area, or consequence.

## Persistence contract

Every future taxonomy assignment shall preserve the equivalent of:

```text
assignment_id
taxonomy_id
taxonomy_version
axis
facet
code?
applicability_state
subject_type
subject_id
classification_role
evidence_refs[]
applicability_condition_refs[]
confidence_assessment_ref?
analyzer_or_adjudicator
created_at
reason
supersedes_assignment_id?
mapping_provenance?
```

Historical assignments retain their exact version. A current-view
reclassification creates a linked derived projection; it does not mutate the
source claim, observation, run, finding, case, disposition, readiness
evaluation, or exported historical result.

## Analyzer and coverage contract

Each analyzer declaration shall identify:

- accepted taxonomy version;
- eligible input populations by technical surface;
- supported and excluded areas, consequences, and extent facets;
- roles it may emit and evidence required for each;
- unknown, unsupported, unmapped, and not-applicable behavior;
- labeled coverage denominators; and
- linked positive, matched-negative, boundary, malformed, cross-cutting, and
  unknown or unsupported evaluation cases.

An analyzer may expose evidence outside its semantic scope, but it must report
a gap rather than borrow another analyzer's classification authority.

## Versioning and change control

- Patch releases change non-semantic wording or metadata.
- Minor releases add codes or relationships without changing existing meaning
  or historical parent rollups.
- Major releases change boundaries, parent aggregation, split or merge
  meaning, or retirement in a way that can change interpretation.

Every persisted assignment records the exact version, including patch. Changes
require product-owner review, updated classification fixtures, affected
product-document review, and an amended dependency map. Acceptance of version
`0.1.0` does not imply complete analyzer support or evaluation coverage.

## Initial limitations

Version `0.1.0` has thin controlled-real evidence for several systems,
including deep quest and dialogue behavior, navigation, interface and input,
economy, magic, stealth and crime, save lifecycle, performance mechanisms,
native runtime behavior, compiled-script semantics, configuration effects, and
generated-output families. These remain visible evaluation or support gaps,
not implicit clean coverage.
