# RESEARCH-0019: Semantic record-family roadmap

Status: Completed — recommendation accepted by project owner  
Date: 2026-07-25  
Last reviewed: 2026-07-26  
Researcher: Codex agent  
Primary question: RQ-024  
Decision enabled: evidence-backed semantic record/relationship ordering after
the first generic scope-incongruent-reversion proof

Acceptance note: The project owner accepted this roadmap on 2026-07-25 through
[RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md).
The ordering is a product/analyzer roadmap, not a claim that any record family
or link shape is already qualified.

## 1. Question and bounded answer

RQ-024 asks:

> Which semantic record families and field relationships should follow the
> first proof?

The recommended sequence is:

1. qualify the generic override-chain, winner, changed-field, link-resolution,
   and stale-value substrate without treating any record family as product
   taxonomy;
2. make the first proof an exact, narrow `NPC_` slice covering appearance
   evidence, actor behavior fields, and the template state that controls
   whether those fields are inherited;
3. make the first materially different non-NPC generalization a
   **placed-reference topology slice**:
   - `REFR.Base`;
   - `REFR.Placement`;
   - `REFR.EnableParent.Reference` and `.Flags`;
   - `REFR.LinkedReferences[].KeywordOrReference` and `.Reference`;
   - containing cell/world identity as structural provenance, not broad
     `CELL`/`WRLD` semantic support; and
   - only `QUST.Aliases[].ID` and `.ForcedReference` when needed to prove that
     the placed reference is quest-relevant;
4. deepen quest/alias/objective/condition logic after that narrow cross-record
   edge passes;
5. qualify item/crafting graphs after the structural quest/reference proof;
6. broaden to `CELL`, `WRLD`, locations, navigation, dialogue/scenes, scripts,
   and other complex families only through separate exact allowlist increments.

The first non-NPC slice is intentionally about **activation and reference
topology**, not another cosmetic family. A spatial or visual `REFR` override
that drops an upstream enable-parent or linked-reference relation exercises a
different game mechanism from an NPC appearance override that restores stale
actor behavior. Adding the narrow quest-alias edge directly instantiates the
planned EVAL-0006/EVAL-0007 cell/quest pair without pretending that all quest,
cell, or worldspace fields are understood.

This is a research recommendation. It does not select implementation
architecture, define the RQ-036 taxonomy, qualify Mutagen fields, accept an M1
plan, or change any registry.

## 2. Authority, scope, and method

### 2.1 Accepted authority

This report applies:

- the accepted product definition, requirements, workflows, domain model,
  severity/confidence/coverage contract, analysis catalog, and milestone
  scope;
- the accepted M0 Wave C sequence and Gate C anti-overfitting requirements;
- [ADR-0007](../../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md):
  xEdit has no product, development, dependency, integration, fixture, or
  oracle role;
- [ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md):
  explicit quiescent MO2 state supplies authoritative profile and plugin
  order;
- [ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md):
  `Mutagen.Bethesda.Skyrim` `0.54.2` is a bounded dependency and only
  positively allowlisted record/field/shape/link/override semantics may be
  supported;
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md):
  every semantic result needs an exact snapshot and smallest complete
  dependency closure;
- [RESEARCH-0008](RESEARCH-0008-mutagen-bethesda-semantic-capability.md):
  library surface availability is not semantic qualification;
- [RESEARCH-0013](RESEARCH-0013-wave-b-authoritative-local-state-integration.md):
  Mutagen receives exact authoritative plugin order and bytes rather than
  discovering local state;
- the [taxonomy dependency map](../taxonomy-dependency-map.md), which prevents
  record families from silently becoming mod-purpose, affected-area,
  consequence, severity, symptom, or extent categories; and
- the evaluation strategy, case catalog, fixture guidelines, and
  [anti-overfitting rules](../../evaluation/anti-overfitting-rules.md).

The completed Wave C surface reports also constrain the ordering:

- [RESEARCH-0014](RESEARCH-0014-root-native-component-surfaces.md) supports
  generic structural inventory before named semantic rules;
- [RESEARCH-0015](RESEARCH-0015-generated-output-tool-surfaces.md) shows why
  worldspace/cell relationships matter to grass and LOD completeness but do
  not establish freshness or broad record semantics; and
- [RESEARCH-0016](RESEARCH-0016-configuration-ecosystem-survey.md) keeps
  configuration languages on separate schema/DSL contracts rather than
  treating their tokens as plugin-record semantics.

### 2.2 In scope

- exact record/field/link slices to qualify first;
- the exact NPC identity/applicability inputs required to derive first-proof
  FaceGen mesh/tint paths and preserve record/asset provenance;
- the first materially different non-NPC generalization;
- relative roadmap ordering after the first proof;
- why each slice is useful, bounded, and meaningfully distinct;
- positive, matched-negative, malformed, unsupported, and metamorphic fixture
  obligations;
- admission, stop, and expansion criteria; and
- the distinction between a library exposing a record family and Infinium
  qualifying exact semantics from independent truth.

### 2.3 Out of scope

- production implementation or code;
- an accepted M1 plan or analyzer contract;
- architecture, process, storage, or IPC selection;
- final taxonomy categories;
- severity or confidence calibration;
- real-mod selection for EVAL-0016/EVAL-0017;
- archive-member or localized-string support;
- Papyrus bytecode semantics;
- navmesh repair or geometric correctness;
- broad quest-condition evaluation;
- any xEdit-derived schema, fixture, output, or comparison; and
- product rules keyed to a specific mod, author, title, FormID, EditorID,
  quest, cell, worldspace, race, NPC, or test fixture.

### 2.4 Research method

The investigation:

1. traced the planned first proof and generalization cases through EVAL-0001,
   EVAL-0002, EVAL-0006, EVAL-0007, EVAL-0008, EVAL-0016, EVAL-0017, and
   EVAL-0052;
2. inspected the exact Mutagen `0.54.2` source revision for typed Skyrim
   record and field availability;
3. separated that availability observation from parser correctness, field
   meaning, effective inheritance, link resolution, override behavior, and
   finding semantics;
4. compared candidate slices by consequence, structural distinctness,
   qualification complexity, cross-layer value, and anti-overfitting value;
5. used primary Creation Kit documentation only as evidence of game/editor
   concepts, not as an independent binary-parser oracle; and
6. designed a parser-independent fixture obligation for every recommended
   field shape.

No game, MO2, Creation Kit, modding helper, or user setup was launched or
changed. Network access was limited to public upstream source/release reads.
The only repository write is this report.

## 3. Current primary sources and exact identities

Sources were checked on 2026-07-25.

| Source | Exact identity | Authority used here | Important limit |
|---|---|---|---|
| [Mutagen release `0.54.2`](https://github.com/Mutagen-Modding/Mutagen/releases/tag/0.54.2) | Tag and commit `282bb99a77b2df7f1b092b06270e8e3c8fb55463`; published 2026-07-08; still the latest GitHub release on 2026-07-25 | Exact accepted library version and source identity | Release status does not qualify any field |
| [Mutagen Skyrim `Npc.xml`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Npc.xml) | Same pinned commit | `NPC_` field/link surface exposed by the accepted library | Generated schema is implementation evidence, not independent game truth |
| [Mutagen Skyrim `Npc.cs`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Npc.cs#L48-L60), [`FormKey.cs`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Plugins/FormKey.cs#L11-L58), and [`SeparatedMasterPackage.cs`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Plugins/Masters/SeparatedMasterPackage.cs#L295-L365) | Same pinned commit | Exact candidate NPC-to-FaceGen path construction, origin-plugin/local-ID identity, and full/light FormID translation | Mutagen implementation is not independent Skyrim runtime truth |
| [Creation Kit Wiki `Dark Face Bug` revision `9182`](https://ck.uesp.net/w/index.php?title=Dark_Face_Bug/ja&oldid=9182) | Archived 2012 page, retrieved 2026-07-25 | Independent corroboration of conventional mesh/tint path shapes and eight-hex filenames | Does not qualify current Skyrim SE light-plugin, template, race, or effective-provider behavior |
| [Mutagen Skyrim `PlacedObject.xml`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Major%20Records/PlacedObject.xml) | Same pinned commit | `REFR` base, placement, enable-parent, linked-reference, and other exposed shapes | API presence does not prove grouping, meaning, or correct parsing |
| [Mutagen `EnableParent.xml`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Common%20Subrecords/EnableParent.xml), [`LinkedReferences.xml`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Common%20Subrecords/LinkedReferences.xml), and [`Placement.xml`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Common%20Subrecords/Placement.xml) | Same pinned commit | Exact typed members proposed for the first non-NPC slice | Still requires independent byte/structure and semantic expectations |
| [Mutagen Skyrim `Quest.xml`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Quest.xml) | Same pinned commit | Availability of quest alias IDs, forced-reference links, stages, objectives, conditions, and script fragments | Only alias ID/forced-reference is proposed initially; the rest remains unqualified |
| [Mutagen Skyrim `Cell.xml`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Cell.xml) | Same pinned commit | Availability of cell fields and persistent/temporary placed-record collections | Does not establish broad `CELL` semantics |
| [Mutagen issue 597](https://github.com/Mutagen-Modding/Mutagen/issues/597) | Open on 2026-07-25, “XCIM Data not properly loaded in Cell/ICellGetter” | Direct contrary evidence against admitting all exposed `CELL` fields | One issue does not invalidate unrelated fields, but it requires field-level rather than family-level admission |
| [Mutagen Skyrim `ConstructibleObject.xml`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Major%20Records/ConstructibleObject.xml) | Same pinned commit | Availability of recipe items, conditions, created object, workbench keyword, and count | Conditions and linked item/keyword families require separate qualification |
| [Creation Kit Actor documentation](https://ck.uesp.net/w/index.php?title=Category:Actor&oldid=4689) | Archived page revision `4689`, retrieved 2026-07-25 | Actor template inheritance and distinct AI, package, faction, spell, inventory, and character-generation concepts | Editor documentation is not a binary conformance oracle |
| [Creation Kit Hold Position documentation](https://ck.uesp.net/wiki/HoldPosition_%28Package_Template%29) | Page retrieved 2026-07-25 | Direct example that actor packages and named linked references participate in behavior | Example-specific setup is not a universal rule |
| [Creation Kit Light Switch documentation](https://ck.uesp.net/w/index.php?title=Light_Switch&oldid=12775) | Archived page revision `12775`, retrieved 2026-07-25 | Direct example that enable-parent topology controls coordinated enabled state | Script example does not qualify every enable-parent variant |

The Mutagen tag was independently resolved to the accepted commit, and the
GitHub latest-release endpoint still reported `0.54.2`. Exact source files
were inspected from that tag. Moving documentation pages are cited with a
retrieval date, and archived revisions are used where available.

## 4. Selection criteria

Each candidate slice was assessed against the following:

| Criterion | Preferred property | Rejection or deferral signal |
|---|---|---|
| User impact | Structural, logic, activation, quest, cell/world, or other high-impact relationship | Cosmetic breadth with no new mechanism |
| Generalization | Different record family, field shape, and game mechanism from the NPC proof | Same actor/appearance mechanism under another label |
| Exactness | Small named field/link allowlist | “Support the record family” without exact consumed fields |
| Ground truth | Directly specifiable binary/structure and semantic expectations independent of Mutagen | Mutagen round trip or output is the only oracle |
| Negative control | A close harmless or intentional counterpart exists | Only obvious positives |
| Failure honesty | Malformed, unresolved, unsupported, and ambiguous outcomes can remain gaps | Best-effort inference is needed to produce an answer |
| Dependency closure | Exact plugin order, bytes, masters, links, and context can be declared | Hidden runtime, save-state, archive, or script dependency is required but unqualified |
| Anti-overfitting | Stable Skyrim semantics reusable across arbitrary mods | Name, FormID, EditorID, title, fixture, or known-mod rule |
| Candidate value | Deterministic indexing can select a small interaction graph | Naive whole-list/all-pairs comparison |
| Cross-layer value | Later joins to assets, docs, config, or generated output are possible without collapsing authority | Surface label alone is treated as purpose or consequence |

These criteria favor reference topology immediately after the NPC proof.
They do not favor broad `CELL`/`WRLD` parsing merely because those families are
important: high impact increases priority, but it does not waive exact
qualification.

## 5. Findings

### F1 — “Record family available” and “semantics qualified” are different states

At the pinned revision, Mutagen exposes typed classes and fields for `NPC_`,
`REFR`, `QUST`, `CELL`, `WRLD`, `COBJ`, and many more families. That proves
that a candidate integration surface exists.

It does not prove:

- the exact binary shape is parsed correctly;
- uncommon full/light-master, deleted, injected, compressed, or malformed
  forms behave correctly;
- a link resolves to the correct winner under the accepted plugin order;
- template or alias semantics are applied correctly;
- a field means what the analyzer claims;
- a changed value is harmful, unintentional, or scope-incongruent; or
- the family is supported as a whole.

The open `CELL.XCIM` issue is direct evidence that family-wide admission would
be unsound. Infinium therefore needs a four-level distinction:

1. **library-exposed family**;
2. **library-exposed exact field/link shape**;
3. **independently qualified exact field/link/override semantics**; and
4. **accepted analyzer use with declared evidence, gaps, and evaluation**.

Only level 3 may enter the ADR-0009 positive allowlist. Only level 4 may support
a product finding.

### F2 — The first proof should qualify several field shapes, not broad NPC coverage

The first generic scope-incongruent-reversion proof needs enough variation to
show that the mechanism is not one hard-coded field comparison. A bounded
`NPC_` slice can cover:

- appearance-side links and structures;
- a scalar behavior structure;
- an ordered or repeated FormLink list;
- a link-plus-value compound list; and
- template state that can change whether local values are effective.

That is enough to prove reusable override/changed-field/reversion
infrastructure while leaving most `NPC_` fields unqualified.

The actor documentation also shows why template state cannot be ignored:
selected actor tabs may inherit from an ActorBase. Comparing raw local fields
without qualifying the relevant template link and flags can misrepresent
effective behavior.

### F3 — Placed-reference topology is the strongest first non-NPC generalization

`REFR` exposes a compact, high-impact graph:

```text
containing cell/world
  -> placed reference
       -> base object
       -> enable parent
       -> zero or more keyworded linked references
       -> position/rotation
```

The same placed reference may also be the forced target of a quest alias:

```text
quest
  -> alias ID
       -> forced placed reference
```

This is materially different from the NPC proof:

- the affected record is a world object, not an actor base;
- the protected semantics are activation and graph topology, not actor AI;
- ordered or keyed reference relations matter;
- containing cell/world context matters;
- a narrow quest edge can establish gameplay relevance; and
- the intended edit can be spatial or base-object presentation while the
  reverted value is a structural relationship.

Creation Kit examples demonstrate that enable parents and named linked
references can control coordinated state and package behavior. Those examples
support prioritizing the relationship class; they do not justify any
fixture-specific rule.

### F4 — The first non-NPC slice can be cell/world/quest-relevant without broad family claims

EVAL-0006 describes a visual cell edit that reverts a quest-relevant
reference. The minimal semantic proof does not need every `CELL`, `WRLD`, or
`QUST` field.

It needs:

- containing cell/world identity from qualified structural group traversal;
- one `REFR` override chain and winner;
- exact structural links on that `REFR`; and
- when quest relevance is part of the case, one independently qualified
  `QUST` alias-ID-to-forced-reference edge.

This keeps the consequence demonstrable while allowing:

- `CELL.ImageSpace`;
- lighting, water, region, ownership, and encounter-zone fields;
- exterior block/sub-block edge cases;
- worldspace inheritance;
- quest stages, objectives, dialogue conditions, script fragments, and
  localized text; and
- navigation meshes

to remain explicit unsupported semantics.

### F5 — Full quest logic should follow the narrow forced-reference edge

Quest records are high impact, but the exposed surface is heterogeneous:

- flags and priority;
- aliases with several fill modes;
- alias conditions, items, factions, spells, packages, and voice;
- stages and log entries;
- objectives and alias-index targets;
- dialog/event conditions;
- virtual-machine data and script fragments; and
- translated strings.

Admitting all of that at once would combine conditions, indices, links,
scripts, localization, and runtime behavior in one correctness claim. The
first quest increment should therefore be only:

- `Quest.Aliases[].ID`; and
- `Quest.Aliases[].ForcedReference`.

After that passes, alias fill modes, objective-target alias IDs, stages, and
conditions should be qualified as separate increments. Papyrus fragment
semantics and translated strings remain later dependencies rather than
implicit fallbacks.

### F6 — Item/crafting is valuable, but should not displace the structural proof

`COBJ` exposes a clean recipe graph:

- ingredient items;
- conditions;
- created object;
- workbench keyword; and
- created count.

Combined with item keywords and effects, it can instantiate EVAL-0008: a
visual item change loses keyword or recipe behavior. It is attractive because
its graph is relatively compact.

It ranks after the placed-reference/quest slice because:

- its trigger scenario is closer to the initial “presentation edit loses
  behavior” shape;
- condition interpretation adds its own qualification burden;
- “recipe behavior” may involve records beyond the winning item override; and
- the first generalization should demonstrate structural cell/world/quest
  reasoning rather than merely add another cosmetic entry point.

`COBJ` should still follow early, before broad cosmetic family expansion.

### F7 — Broad cell/world and navigation support needs a later dedicated gate

`CELL` and `WRLD` are important for:

- reference containment;
- exterior coordinates;
- location and encounter context;
- landscape and navigation;
- grass-cache completeness; and
- terrain/object/grass LOD relationships.

They also contain grouped child records and field-specific parser risks. The
open `CELL.XCIM` issue is known contrary evidence. Navigation adds geometry and
pathing correctness that cannot be inferred from record presence or FormLinks
alone.

Therefore:

- structural containing-cell/world identity may be qualified early;
- individual `CELL`/`WRLD` fields require their own allowlist entries;
- `CELL.ImageSpace` is explicitly excluded until the issue is resolved or an
  independently proven bounded workaround/version is accepted; and
- navigation topology and geometric validity remain a separate later slice.

### F8 — Generic infrastructure and Skyrim semantics have a clean boundary

Generic infrastructure may own:

- plugin order and snapshot identity;
- record override chains and winners;
- changed-field sets;
- stale-value/reversion shapes;
- link graph nodes/edges;
- candidate selection and dependency closure;
- typed observations, gaps, hypotheses, findings, and cases; and
- provenance.

Skyrim domain analyzers may own only independently qualified:

- field meanings;
- template/inheritance behavior;
- exact record relationships;
- stable feature-graph construction;
- impact rules; and
- validation expectations.

Neither layer may own a rule such as “this particular quest/cell/NPC/mod is
important.” Documentation or user intent remains separate evidence; a field
group can support a scope comparison but cannot prove author intent by itself.

### F9 — Candidate selection can remain deterministic and bounded

The proposed slices do not require whole-list or all-pairs model comparison.
Candidate indexing can be driven by:

- a record with two or more overrides;
- an upstream changed field that a later winner restores to an earlier value;
- the later winner changing at least one different qualified field;
- resolved links from that record to a small qualified neighborhood;
- applicable declared-purpose/documentation evidence; and
- exact snapshot and analyzer scope.

The model, if used later, receives the bounded evidence graph and competing
interpretations. It does not discover override chains, invent link semantics,
or decide unsupported field meaning.

## 6. Availability and qualification matrix

All rows below are **unqualified at this report's completion** unless an
accepted evaluation later says otherwise.

| Family/surface | Exposed by Mutagen 0.54.2 | Exact proposed use | Current semantic state | Roadmap disposition |
|---|---|---|---|---|
| Cross-family record identity/override context | Yes | FormKey, plugin origin, ordered override chain, winner, deletion/state, changed-field input | Research route accepted; exact shapes still require EVAL-0052 | Qualify as the substrate before slice findings |
| `NPC_` | Yes | Narrow appearance, AI data, packages, factions, and template-state slice | No field-level EVAL-0052 pass yet | First proof |
| `REFR` | Yes | Base, placement, enable parent, linked references | No field/link/group-shape EVAL-0052 pass yet | First non-NPC generalization |
| Containing cell/world identity | API/group traversal exists | Structural parent context and provenance only | Exact interior/exterior/group shapes unqualified | Qualify with `REFR`; do not imply broad `CELL`/`WRLD` support |
| `QUST` alias forced-reference edge | Yes | Alias ID plus forced placed-reference link only | Unqualified | Add to the first non-NPC proof when quest relevance is asserted |
| Full `QUST` | Yes | Alias fill modes, objectives, stages, conditions | Unqualified and heterogeneous | Next logic-focused research/qualification slice |
| `COBJ` | Yes | Ingredients, created object/count, workbench keyword; conditions separately | Unqualified | Early follow-up after structural generalization |
| Item base records and keywords/effects | Yes for many families | Only exact fields needed by selected recipe/item case | Unqualified | Expand with `COBJ`, not as “all items” |
| Broad `CELL`/`WRLD` fields | Yes | Location/environment/world relationships | Unqualified; open `CELL.XCIM` contrary evidence | Later field-by-field slice |
| Navigation records/geometry | Exposed in part | Path/topology correctness | Unqualified; semantic and geometric burden high | Defer to dedicated research |
| Dialogue, scene, and information records | Yes | Quest/dialogue flow | Unqualified; conditions, indices, localization, audio, and runtime state interact | After narrow quest graph |
| VMAD/Papyrus fragments | Exposed in part | Script attachment/property/fragment relationships | Binary exposure is not script behavior | Defer to RQ-022 and exact fixture work |
| Localized strings | Exposed, with known integration gaps | User-visible quest/item/NPC text | Not admitted by ADR-0009 route yet | Exclude until provider-aware qualification |

The table must never be rendered as “record family supported” merely because
the second column says yes.

## 7. Exact roadmap

### Stage 0 — Generic semantic substrate

Qualify the smallest cross-family operations used by every later slice:

- full and light plugin identity where selected;
- exact ordered override chains;
- winning record and deletion/state;
- FormKey identity across masters;
- resolved, unresolved, null, and invalid links;
- changed-field representation for explicitly allowlisted fields;
- containing group/record context where selected;
- exact dependency closure and provenance; and
- explicit unsupported/malformed outcomes.

This stage produces typed observations only. It does not decide harmfulness.

**Admission gate**

- exact `Mutagen.Bethesda.Skyrim` `0.54.2` package/lock identity;
- authoritative plugin order and bytes from ADR-0008/ADR-0010;
- parser-independent EVAL-0052 fixtures for every exercised structural shape;
- full/light-master, missing-master, deletion, unresolved-link, malformed,
  and changed-during-capture cases as applicable; and
- no xEdit input or comparison.

### Stage 1 — Exact first `NPC_` proof

#### 1A. Appearance-side evidence allowlist

Start with:

- `Npc.HeadParts`;
- `Npc.HairColor`;
- `Npc.HeadTexture`;
- `Npc.FaceMorph`; and
- `Npc.TintLayers`.

`TextureLighting`, `FaceParts`, and other appearance fields may be admitted in
the same milestone only if independently qualified. Height, weight, race,
outfits, voice, and name should not be classified as purely cosmetic by
default because they may affect behavior, assets, identity, or presentation
in multiple ways.

#### 1B. Behavior-side allowlist

Start with three deliberately different shapes:

- `Npc.AIData` as a scalar compound structure;
- `Npc.Packages` as a repeated FormLink list; and
- `Npc.Factions[].Faction` plus `.Rank` as a link-plus-value compound list.

This does not claim that all NPC behavior is covered. Actor effects, perks,
inventory, class, combat style, outfits, crime faction, keywords, scripts, and
other fields remain gaps until separately admitted.

#### 1C. Effective-template prerequisite

Qualify:

- `Npc.Template`; and
- the exact `Npc.Configuration.Flags` and `.TemplateFlags` bits needed to
  determine whether the selected appearance/AI/package/faction data is local
  or inherited.

If the effective value cannot be resolved through the qualified template
shape, the analyzer must abstain for that field rather than compare raw local
values as effective behavior.

#### 1D. FaceGen path and provenance prerequisite

The first M1 proof also requires the bounded RQ-023 relationship in
[RESEARCH-0018 section 6.1](RESEARCH-0018-asset-reference-completeness.md#61-first-proof-npc-to-facegen-identity-and-provenance-dependency).
Qualify these exact semantic inputs:

- `Npc.FormKey.ModKey.FileName` as the **originating** plugin filename, not
  the winning override plugin;
- `Npc.FormKey.ID` as the origin-local ID with load/master indices removed,
  formatted as eight uppercase hexadecimal digits;
- `Npc.Race` and resolved `Race.Flags.FaceGenHead`;
- `Npc.Configuration.Flags.UseTemplate`;
- `Npc.Configuration.TemplateFlags.Traits`; and
- the selected snapshot's independent record winner, FaceGen mesh/tint
  provider chains, and effective providers.

The candidate keys are:

```text
meshes\actors\character\facegendata\facegeom\
  <origin-plugin-filename>\<origin-local-id-X8>.nif
textures\actors\character\facegendata\facetint\
  <origin-plugin-filename>\<origin-local-id-X8>.dds
```

An NPC override does not replace `<origin-plugin-filename>` with the winner's
plugin. A full-origin ID such as `0x1234` becomes `00001234`; Mutagen's
small-master translation strips the `FE` marker/light index to the 12-bit
local ID, so `0xABC` becomes `00000ABC`. The latter remains a candidate
algorithm, not supported Skyrim SE light-plugin behavior, until an independent
fixture proves it. Light master style is independent of filename extension:
an ESL-flagged `.esp` still uses its exact `.esp` origin filename.

Do not derive direct FaceGen requirements when race resolution fails,
`FaceGenHead` is absent, `UseTemplate` is set, or `Traits` is templated.
Those cases require abstention or a typed gap; they do not prove missing
FaceGen. Template-source traversal is outside this first direct-link
qualification unless separately admitted.

Keep record identity, record winner, logical asset key, and effective asset
provider distinct. For the initial M1 envelope, a mod folder or other
qualified loose source may supply or shadow the origin-named key.
Normalization, case/separator comparison, and shadowing use the provider
contract from ADR-0008 and RESEARCH-0018; an asset under the winning-plugin
directory or with a runtime load-order prefix does not satisfy the derived
origin key. Archive-backed FaceGen remains unsupported/conditional and the
atomic first-proof profile must prove that archives cannot participate.

Without rendering, this slice may conclude only expected logical paths,
qualified presence/absence, provider/shadow chains, and exact bytes. It may
not conclude visual correctness, morph/tint agreement, absence of a dark-face
symptom, engine use, or runtime behavior.

RQ-023 remains M0-exit-blocking until the independent loose-only full/light,
Race/template, normalization, shadowing, matched-negative, malformed,
unsupported, and archive-independence matrix in RESEARCH-0018 passes. Active
archive-provider qualification remains later conditional work and does not
block the initial loose-only M1 envelope. The Stage 1 semantic allowlist must
not bypass the loose-only dependency.

#### 1E. Required fixture matrix

At minimum:

1. appearance-side change plus stale reversion of `AIData`;
2. appearance-side change plus stale reversion of `Packages`;
3. appearance-side change plus stale reversion of faction membership or rank;
4. the same appearance edit preserving the upstream behavior change;
5. a deliberate/documented behavior reversion;
6. ambiguous intent requiring a hypothesis or needs-input outcome;
7. template-inherited versus local data;
8. unresolved package/faction/template link;
9. missing master, malformed subrecord, and unsupported field;
10. plugin/mod-folder renaming;
11. unrelated plugin insertion/reordering;
12. one relevant winner change that predictably changes only the dependent
    result;
13. full-origin, `.esl` light-origin, and ESL-flagged `.esp`-origin NPCs with
    independently specified expected mesh/tint keys;
14. a later NPC override that retains the origin plugin in both keys while a
    separately ordered provider shadows the exact origin-named paths;
15. wrong winner-plugin directory, runtime/load-order-prefixed ID, wrong
    local ID, same basename elsewhere, and only-one-of-mesh/tint negatives;
16. race-unresolved, no-`FaceGenHead`, `UseTemplate`, and `Traits` cases that
    abstain instead of producing false missing-asset claims;
17. loose-only presence, absence, shadowing, normalization/collision, and an
    atomic profile proving that archives cannot participate; and
18. malformed NPC/RACE/FormKey input, missing master, deleted winner,
    changed-during-capture, inaccessible/corrupt payload, and unsupported
    template/provider or archive-dependent cases.

Qualified active/inactive archive, loose-over-archive, archive-over-archive,
and member-invalidation cases remain a separate later conditional matrix.

The first proof passes only when EVAL-0001 is detected, EVAL-0002 is not
misclassified, every consumed shape passes EVAL-0052 or an accepted
successor, and the RQ-023 FaceGen identity/provider prerequisite passes.

### Stage 2 — First non-NPC generalization: placed-reference topology

#### 2A. Core `REFR` allowlist

Qualify exactly:

- `PlacedObject.Base`;
- `PlacedObject.Placement.Position`;
- `PlacedObject.Placement.Rotation`;
- `PlacedObject.EnableParent.Reference`;
- `PlacedObject.EnableParent.Flags`;
- `PlacedObject.LinkedReferences[].KeywordOrReference`; and
- `PlacedObject.LinkedReferences[].Reference`.

The first fixture should use `REFR`, not every `IPlaced` subtype. `ACHR`,
projectile, hazard, trap, and other placed-record families remain outside the
slice until separately qualified.

Containing cell/world identity is included as structural provenance. No other
`CELL` or `WRLD` field becomes supported through that inclusion.

#### 2B. Narrow quest-relevance edge

When the positive case asserts that the reference is quest-relevant, also
qualify exactly:

- `Quest.Aliases[].ID`; and
- `Quest.Aliases[].ForcedReference`.

No alias fill mode, condition, objective, stage, script fragment, dialogue
condition, or localized text is implied.

#### 2C. Positive mechanism

A minimal three-plugin fixture should establish:

1. an earlier value for one `REFR`;
2. an upstream override that adds or changes a qualified structural relation,
   such as an enable parent or keyworded linked reference; and
3. a later spatial/presentation override that changes placement or base-object
   presentation while restoring the structural relation to the earlier value.

For the quest variant, a separately specified quest alias points to that
reference. The finding evidence is:

- exact override chain and winner;
- exact changed and reverted fields;
- exact old/new FormKeys and flags;
- resolved link endpoints;
- containing cell/world identity;
- narrow quest-alias edge when applicable;
- declared-purpose or other intent evidence kept separate; and
- explicit unsupported/gap state for every unmodeled field.

The deterministic candidate is “later override restores a qualified
structural relation while changing a different qualified field.” It is not
“this cell/quest is known to be fragile.”

#### 2D. Matched negatives

At minimum:

- the same placement/base change preserving enable-parent and linked-reference
  topology;
- an intentional documented structural removal;
- a structural change where the later mod's declared scope includes that
  behavior;
- a similar `REFR` with no quest alias or other evidence of the asserted
  consequence;
- a quest alias targeting a different reference;
- a harmless unrelated reference in the same cell;
- equivalent topology with a renamed keyword/reference/mod; and
- an ambiguous case that remains a hypothesis or gap.

#### 2E. Malformed and unsupported cases

At minimum:

- missing master;
- unresolved `Base`, enable-parent, linked-reference, or forced-reference
  FormKey;
- malformed/truncated `NAME`, `DATA`, `XESP`, `XLKR`, or `ALFR` shape;
- duplicate or otherwise unsupported subrecord arrangement;
- unsupported placed-record subtype;
- orphaned or unqualified cell/world group context;
- deleted winner and deleted link target;
- change during capture; and
- an exposed but unqualified `CELL`/`WRLD` field.

Independent fixture truth must record direct byte/structure assertions for the
exact subrecords and independently specified expected chains, winners, links,
flags, and values. Mutagen may parse the fixture under test; it may not be the
sole fixture author or source of expectations.

#### 2F. Generalization gate

The first mechanism is considered generalized only when:

- EVAL-0006 is detected;
- EVAL-0007 remains negative;
- the positive and matched negative survive renaming and unrelated reordering;
- the case uses no NPC-specific rule;
- every consumed `REFR`, group-context, and optional `QUST` shape passes
  EVAL-0052;
- unsupported semantics remain visible;
- a controlled-real EVAL-0017 case is later pinned and reviewed independently;
  and
- no rule mentions a real or fixture-specific mod, quest, cell, worldspace,
  EditorID, or FormID.

### Stage 3 — Quest and alias logic graph

After Stage 2:

1. qualify additional alias fill modes one at a time;
2. qualify objective targets and their alias-ID relationships;
3. qualify stages and stage/log-entry structure;
4. qualify condition records by exact function/parameter shapes rather than
   as one universal “condition” type;
5. add scene/dialogue relations only when exact links and localization inputs
   are qualified; and
6. keep VMAD/Papyrus behavior outside the claim unless RQ-022 supplies an
   accepted route.

Each increment requires its own positive, matched negative, malformed,
unsupported, and metamorphic fixtures. A broken alias index and an
intentionally unused alias, for example, are not the same case.

### Stage 4 — Item and crafting graph

Qualify a narrow EVAL-0008 graph:

- `ConstructibleObject.Items` with exact item/count shape;
- `ConstructibleObject.CreatedObject`;
- `ConstructibleObject.CreatedObjectCount`;
- `ConstructibleObject.WorkbenchKeyword`;
- only the exact condition functions/parameters selected for the fixture; and
- only the item-base keyword/effect fields needed by the selected case.

Use at least:

- a presentation change that restores stale keywords;
- a recipe whose created-object or ingredient link is reverted;
- a preserved-behavior matched negative;
- an intentional recipe replacement;
- an unrelated recipe/item;
- unresolved item/keyword links;
- malformed conditions; and
- renamed/reordered equivalents.

Do not treat all `ARMO`, `WEAP`, `MISC`, `ALCH`, or other item records as one
qualified “item family.”

### Stage 5 — Cell, worldspace, location, and navigation depth

Expand field by field:

- `CELL.Location`, encounter/ownership/context relations where justified;
- exterior grid and worldspace parent relations;
- persistent/temporary child-group semantics;
- location/reference-type graphs;
- landscape/grass dependencies;
- generated grass/LOD dependency closures; and
- navigation records and geometry only through a dedicated semantic and
  geometric qualification program.

Explicitly exclude `CELL.ImageSpace` at Mutagen `0.54.2` until issue 597 is
closed in an accepted exact version or the exact shape is independently proven
through a reviewed bounded route. Issue closure alone would trigger
requalification, not automatic admission.

### Stage 6 — Later high-complexity families

Candidates include:

- dialogue topics/information and scenes;
- packages and conditions beyond the exact early slices;
- scripts/VMAD and Papyrus properties/fragments;
- leveled lists and distribution semantics;
- spells, magic effects, perks, and effect conditions;
- race, class, relationship, and template graphs;
- weather, image-space, lighting, water, and region interactions; and
- broader assets, localization, archives, and generated-output joins.

Order these from corpus evidence and accepted taxonomy coverage, not from
record-count convenience or hosting-site categories.

## 8. Why the alternatives were not selected first

| Candidate first follow-up | Strength | Why it is not first | Disposition |
|---|---|---|---|
| More NPC fields | Low integration cost | Does not prove a materially different non-NPC mechanism | Add only when required; not the generalization |
| Another cosmetic base record | Easy stale-value analogy | Broadens appearance coverage without testing structural/logic topology | Defer |
| `COBJ` plus item keywords | Compact useful graph; EVAL-0008 already planned | Closer to the original presentation-versus-behavior pattern; conditions add scope | Stage 4 |
| Full `QUST` family | Very high impact | Aliases, indices, stages, conditions, scripts, localization, and runtime state are too heterogeneous for one slice | Narrow forced-reference edge first; deepen at Stage 3 |
| Broad `CELL`/`WRLD` | High impact and generated-output relevance | Group complexity plus field-specific contrary evidence; family-wide claim would violate ADR-0009 | Structural containing context first; field-by-field Stage 5 |
| Navigation first | High gameplay impact | Requires graph and geometric correctness beyond simple links | Dedicated later research |
| Dialogue/scenes first | High logic impact | Conditions, aliasing, localization, voice assets, scripts, and runtime state interact | Follow quest substrate |
| Broad “all records with overrides” | Maximum nominal breadth | Confuses availability with meaning and creates unbounded false confidence | Reject |
| Real-mod-first rule discovery | Fast access to examples | Encourages fixture/mod-specific rules and weak independent truth | Reject; synthetic first, controlled real later |
| xEdit comparison | Familiar external display | Prohibited by ADR-0007 in every role | Reject |

## 9. Qualification contract for every roadmap increment

A field or relationship may move from “exposed” to “qualified” only when the
reviewed capability entry records:

1. exact game/runtime scope;
2. exact Mutagen package, source revision, lock identity, and parser settings;
3. exact record family, subrecord/field members, link target types, optionality,
   list ordering/key semantics, and selected override shapes;
4. exact authoritative plugin order, masters, bytes, snapshot, and dependency
   closure;
5. parser-independent expected record identity, chain, winner, field values,
   flags, and links;
6. positive cases;
7. structurally matched negatives;
8. malformed, missing-master, unresolved-link, deletion, unsupported, and
   capture-race cases as applicable;
9. metamorphic rename, unrelated reorder, and equivalent-shape cases;
10. known upstream issues and contrary evidence;
11. explicit abstention and coverage-gap behavior;
12. performance/failure-isolation limits;
13. analyzer(s) allowed to consume the shape; and
14. the evaluation case revision and result that admitted it.

Acceptable independent truth may combine:

- hand-audited plugin bytes;
- direct record/subrecord offsets, lengths, tags, and encoded FormID
  assertions;
- independently written expected override/link tables;
- authoritative format invariants;
- exact official-master invariants where suitable;
- documented manual adjudication; and
- targeted in-game behavior only where static truth is insufficient and the
  runtime evidence is exactly bound.

Mutagen-generated fixtures can assist construction or inspection but cannot be
the sole source of expected output. xEdit cannot assist any part of this
process.

## 10. Stop, exclusion, and reopening rules

Stop or narrow a slice when:

- independent expectations disagree with Mutagen on a consumed shape;
- the discrepancy cannot be isolated to an explicit unsupported variant;
- effective field meaning requires an unqualified template, condition, script,
  localized string, archive, runtime, or save-state dependency;
- malformed input cannot be isolated within accepted resource limits;
- the matched negative cannot be separated from the positive with stable
  Skyrim semantics and evidence;
- the rule needs a real mod name, title, FormID, EditorID, NPC, quest, cell, or
  worldspace;
- candidate volume requires naive all-pairs model calls;
- missing evidence is being converted into a clean result; or
- the proposed slice would silently broaden a family-level support claim.

Reopen an excluded field or family only when:

- a later exact dependency version is pinned;
- the relevant upstream issue/fix and regression surface are reviewed;
- the full independent positive/negative/malformed/metamorphic gate is rerun;
- downstream analyzer and dependency closures are revalidated; and
- historical outputs retain their original capability/version provenance.

## 11. Recommended answer and confidence

### Recommended answer to RQ-024

After the generic NPC scope-incongruent-reversion proof, prioritize a
**placed-reference activation/link topology** rather than more cosmetic
breadth. Qualify `REFR` base, placement, enable-parent, and keyworded
linked-reference relationships, plus structural containing cell/world
identity. Add only the `QUST` alias ID/forced-reference edge needed to prove
quest relevance.

Then deepen quest/alias/objective/condition logic, followed by item/crafting
graphs, before broad `CELL`/`WRLD`, navigation, dialogue/scene, script, and
other high-complexity families. Every step remains an exact field/link/
override-shape allowlist backed by independent fixture truth.

The core sequence is:

```text
generic chain/winner/link substrate
  -> narrow NPC appearance-versus-behavior proof
       + qualified origin-FormKey -> FaceGen mesh/tint provenance
  -> REFR activation/link topology
       + narrow QUST forced-alias edge
  -> deeper quest/alias/objective/condition graph
  -> item/crafting graph
  -> broader cell/world/location/navigation depth
  -> later complex semantic families
```

### Confidence

- **High** that availability must be separated from exact semantic
  qualification under ADR-0009 and the observed open field-specific issue.
- **High** that `REFR` enable-parent/linked-reference topology is materially
  different from the NPC behavior proof.
- **High** that the proposed narrow `REFR` + forced-alias slice directly
  supports the planned EVAL-0006/EVAL-0007 pair without requiring broad quest,
  cell, or world semantics.
- **Medium** that the exact field set is the optimal first delivery slice;
  controlled corpus work and EVAL-0052 may require narrowing it further.
- **Low/unsupported** for any claim that the named families or fields are
  already correctly parsed or semantically supported.

## 12. Downstream work enabled

The owner accepted this recommendation. Registry and roadmap documentation now
reflect it; exact shape qualification and implementation remain pending:

1. an RQ-024 registry update stating that the M0 roadmap is researched while
   exact fixture qualification and milestone selection remain pending;
2. an EVAL-0052 capability-matrix expansion for the exact Stage 0 through
   Stage 2 shapes;
3. detailed EVAL-0001/EVAL-0002 NPC fixtures matching Stage 1;
4. the independent RQ-023 loose-only full/light NPC-to-FaceGen identity,
   applicability, normalization, shadowing, and archive-independence fixture
   matrix required by Stage 1;
5. detailed EVAL-0006/EVAL-0007 `REFR`/forced-alias fixtures matching Stage 2;
6. controlled-real EVAL-0016/EVAL-0017 selection only after synthetic behavior
   passes;
7. future analyzer declarations under ANALYSIS-016 for the exact selected
   slices;
8. corpus/taxonomy coverage reporting that treats these as technical surfaces
   and affected-area evidence rather than final categories; and
9. a future accepted milestone plan linking exact requirements, ADRs,
   capability entries, and evaluation revisions before implementation.

Accepted RQ-024 disposition:

> **Resolved for the M0 roadmap; exact qualification and delivery selection
> pending.** Follow the first narrow NPC proof with a placed-reference
> activation/link topology slice and only the quest forced-alias edge needed
> for quest relevance. Deepen quest logic, item/crafting, and broad
> cell/world/navigation semantics through later field-level allowlists.

## 13. Requirements and evidence traceability

| Requirement/decision | Evidence in this report | Disposition |
|---|---|---|
| ANALYSIS-003 | Exact chain/winner/link substrate and per-field admission | No conflict dump or family-wide semantic claim |
| ANALYSIS-004 | Stage 1 and Stage 2 stale-value/reversion mechanisms | Generic mechanism with intent evidence kept separate |
| ANALYSIS-005 | NPC template/behavior links and `REFR`/quest/cell-world graph | Cross-record relationships are bounded and typed |
| ANALYSIS-016 | Qualification contract and analyzer-use admission | Future analyzers must declare exact scope/evidence/gaps |
| ANALYSIS-017 | Deterministic candidate indexing from override/reversion/link evidence | No naive all-pairs LLM comparison |
| EVID-001, EVID-005, EVID-006 | Observation/interpretation separation, candidate rationale, gaps | Unsupported and ambiguous states remain visible |
| COVER-001 through COVER-003 | Availability/qualification matrix and explicit exclusions | No field presence becomes fabricated support or safety |
| SNAP-001 through SNAP-005 | Exact plugin order/bytes and dependency closures | Every conclusion binds to immutable authoritative inputs |
| ADR-0007 | No xEdit source, fixture, comparison, dependency, or oracle | Enforced throughout |
| ADR-0008 | Authoritative explicit quiescent MO2 state | Mutagen does not rediscover order/profile |
| ADR-0009 | Pinned Mutagen 0.54.2 positive allowlist | Field/link/shape support is gated independently |
| ADR-0010 | Snapshot and smallest complete dependency closure | Structural and semantic dependencies remain explicit |
| EVAL-0001/EVAL-0002 | Exact NPC positive and matched-negative matrix | First proof entry gate |
| EVAL-0006/EVAL-0007 | Exact `REFR` plus optional forced-alias matrix | First non-NPC generalization gate |
| EVAL-0008 | Bounded `COBJ`/item graph | Early follow-up, not first generalization |
| EVAL-0016/EVAL-0017 | Controlled-real cases after synthetic qualification | Real mods do not author production rules |
| EVAL-0052 | Independent byte/structure/semantic fixture contract | Required for every consumed field/link/override shape |
| RQ-023, EVAL-0051, M1 record/FaceGen provenance | Stage 1D defines the origin FormKey/path relationship, Race/template applicability, full/light boundary, provider separation, and no-rendering conclusion limit | RQ-023 remains exit-blocking until the loose-only identity/provider and archive-independence matrix passes; archive-positive support remains conditional |
| EVAL-0086/RQ-036 | Record families remain technical surfaces, not the taxonomy | Accepted roadmap creates no automatic classification |
| Gate C | Materially different non-NPC mechanism, matched negatives, gaps, no all-pairs LLM | This roadmap defines the required mechanism and replacement shape; Gate C remains unmet until RQ-025 selects exact EVAL-0016/EVAL-0017 candidates and RQ-023 completes loose-only FaceGen qualification |

## 14. Conclusion

The roadmap should broaden by **mechanism**, not by record count.

The first NPC proof qualifies a narrow combination of appearance, AI,
package, faction, and template shapes. The first non-NPC proof should then
move to placed-reference activation and linked-reference topology, with only a
narrow quest forced-alias relationship and structural cell/world context. That
is a genuine structural/logic generalization and provides a disciplined bridge
to deeper quest, crafting, worldspace, location, navigation, generated-output,
and cross-layer analysis.

At every stage, “Mutagen exposes this record” remains only an availability
observation. Supported semantics begin only after exact parser-independent
positive, matched-negative, malformed, unsupported, and metamorphic
qualification. Unknown breadth remains a coverage gap, not an inferred clean
result.
