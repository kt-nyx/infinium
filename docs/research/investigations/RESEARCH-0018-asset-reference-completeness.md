# RESEARCH-0018 — Asset-reference completeness

Status: Completed
Disposition: recommendation accepted by project owner
Date: 2026-07-25
Last reviewed: 2026-08-10
Researcher: Codex agent
Primary RQ: RQ-023 — Which asset formats can be checked for referenced-file
completeness efficiently?
M0 wave: C
Decision enabled: bounded asset-reference analyzer scope, EVAL-0059
specification input, and candidate/index design input

Acceptance note: The project owner accepted the NIF-first bounded
recommendation on 2026-07-25 through
[RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md).
RESEARCH-0034 subsequently completed the exact loose-only FaceGen
identity/provider qualification at its declared pre-resolved-input boundary,
closing this RQ-023 Gate C prerequisite. Archive-positive parity, authoritative
MO2/provider reconstruction, and production parser selection/conformance
remain later work. Any “pending” wording below records the state of this
2026-07-25 investigation and is superseded by RESEARCH-0034.

## 1. Question and governing boundaries

This investigation asks which Skyrim SE assets expose explicit references to
other files, which of those references can be extracted safely and cheaply,
and what Infinium may conclude after resolving them against the selected MO2
profile's effective namespace.

It is governed by:

- [SCOPE-005](../../product/requirements.md#scope-005--effective-installation),
  which requires the selected profile's effective loose, archive, root, and
  generated state rather than a raw directory listing;
- [SNAP-001 through SNAP-006](../../product/requirements.md#snapshot-and-reproducibility),
  which bind observations and cache reuse to exact input state;
- [EVID-001 through EVID-006](../../product/requirements.md#evidence-and-trust),
  which keep observations, candidates, hypotheses, and findings distinct;
- [COVER-001 through COVER-003](../../product/requirements.md#coverage-and-readiness),
  which require unsupported formats and incomplete provider reconstruction to
  remain visible gaps;
- [AUTH-001 through AUTH-003](../../product/requirements.md#authority-and-safety),
  which prohibit setup mutation and constrain file/process operations;
- [ANALYSIS-005](../../product/requirements.md#analysis-005--cross-record-and-cross-layer-reasoning),
  [ANALYSIS-013](../../product/requirements.md#analysis-013--missing-referenced-assets),
  [ANALYSIS-016](../../product/requirements.md#analysis-016--declared-analyzer-contract),
  and
  [ANALYSIS-017](../../product/requirements.md#analysis-017--candidate-first-llm-escalation);
- [ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md),
  which accepts qualified loose-file provider reconstruction but leaves
  effective archive-provider behavior separately gated;
- [ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md),
  which accepts Mutagen `0.54.2` only for allowlisted semantics and does not
  accept its standard archive applicability or ordering as authority;
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md),
  which requires structural manifests, scoped hashes, and typed dependency
  closure; and
- [archived Gate C plan](../../plans/milestones/README.md),
  which requires materially different surfaces, matched negatives,
  candidate-first scaling, and explicit unsupported regions.

The direct evaluation target is
[EVAL-0059](../../evaluation/case-catalog.md), with EVAL-0051, EVAL-0052,
EVAL-0032, EVAL-0046, and EVAL-0086 providing provider, record, candidate,
non-mutation, and taxonomy-classification boundaries.

## 2. Scope and non-scope

### In scope

- explicit external path fields in Skyrim SE asset formats;
- typed, version-qualified extraction of those fields;
- resolution against a provider-aware effective `Data` namespace;
- loose and BSA-contained targets, subject to the accepted archive gate;
- safe path handling, malformed-file behavior, caching, and bounded indexing;
- the bounded NPC-to-FaceGen path/provenance relationship required by the
  first M1 proof, as a fixture-gated convention rather than a NIF reference;
- exact observations versus heuristic candidate-generation boundaries; and
- a small first slice capable of supporting EVAL-0059 and a later
  cross-layer plugin-to-asset proof.

### Out of scope

- declaring every unreferenced file to be erroneous;
- raw overwrite enumeration already supplied by MO2;
- arbitrary string search as proof that a file is referenced;
- proving that a present target is visually, behaviorally, or semantically
  correct;
- executing meshes, scripts, interfaces, behavior graphs, or native code;
- duplicating the configuration, compiled-Papyrus, generated-output, native
  component, or semantic-record investigations;
- inventing a final mod, technical-surface, affected-area, consequence, or
  effect-extent taxonomy;
- Skyrim LE, VR, console, or non-Skyrim format semantics; and
- choosing a production parser or application architecture in this research
  item.

## 3. Method and pinned evidence

The investigation used current primary implementation sources and one bounded
local parser trial. It did not inspect the user's real modlist and therefore
does not treat the private profile as an oracle. All sources in the following
table were retrieved or inspected on 2026-07-25.

| Evidence | Exact revision used | Purpose |
|---|---|---|
| `ousnius/nifly` | commit `70da91c1304f7d405b0a3e396df0db4d15bee9d8`, inspected 2026-07-25 | Skyrim SE NIF parsing, typed texture and behavior-graph fields, normalization behavior, tests, and parser-risk review |
| `niftools/nifskope` | commit `3a85ac55e65cc60abc3434cc4aaca2a5cc712eef`, inspected 2026-07-25 | independent corroboration of named NIF path-bearing blocks and fields |
| `Mutagen` | tag `0.54.2`, commit `282bb99a77b2df7f1b092b06270e8e3c8fb55463` | read-only BSA member enumeration and stream API; exact candidate NPC-to-FaceGen link construction in `Npc.cs`; local origin identity in `FormKey.cs` and `SeparatedMasterPackage.cs`; not archive activation, precedence, or Skyrim runtime authority |
| Creation Kit Wiki, `Dark Face Bug` revision `9182` | archived 2012 page, retrieved 2026-07-25 | independent corroboration of the conventional `Data\Meshes\...\FaceGeom\<plugin>\<eight-hex-id>.nif` and `Data\Textures\...\FaceTint\<plugin>\<eight-hex-id>.dds` shapes | Old Skyrim editor documentation; it does not independently qualify Skyrim SE light-plugin, template, race, or effective-provider behavior |
| `Pandora Behaviour Engine` | tag `v4.3.1-beta`, commit `d6344e394c8a9ecfd2966cc0d84bbbdf73976b19` | evidence that Skyrim behavior/project HKX structures contain semantically named animation, behavior, skeleton, character, and project references |
| Microsoft DDS documentation | retrieved 2026-07-25 | DDS header/content boundary |
| Adobe SWF format specification v19 and current Ruffle source | retrieved 2026-07-25 | static SWF import references, dynamic-code boundary, and available parser surface |
| `niflysharp` NuGet | package `2.0.4`, content hash `tzHVdwQSnpoTq6S4MiI72LhWxkaJQ/UhbyYbgH/G2Wwc/BbAFacJ5bKxdnzgKl9E9MF9IRDR8nJwVElIXutXxw==` | executable NIF read trial through the current .NET/native wrapper |

### 3.1 Local NIF trial

Environment:

- Windows NT `10.0.26200.0`;
- .NET SDK `10.0.302`; and
- `niflysharp` `2.0.4`.

The trial loaded two fixtures supplied by the current `nifly` repository:

| Input | Observed result |
|---|---|
| `TestNifFile_Skinned_SE.nif` | load result `0`; `IsValid=true`; `IsSSECompatible=true`; 15 blocks; two typed slot reads: `textures\white.dds` and `textures\gray.dds` |
| `TestNifFile_Corrupted.nif` | native wrapper raised `SEHException: External component has thrown an exception` |

The positive result proves that a current parser surface can extract explicit
Skyrim SE NIF texture references without rendering the asset. It does not
prove coverage of every NIF block or target-path rule. The malformed result is
equally important: this parser cannot be treated as safe in the main
application process merely because it is maintained and read-only. A
production adapter requires isolation, resource limits, cancellation, and
adversarial qualification.

The package build also emitted an `MSB3246` warning while resolving a native
image as managed metadata, although the trial ran successfully. Parser
packaging and load behavior therefore remain implementation questions rather
than accepted architecture.

### 3.2 Reproduction and side effects

The bounded trial can be reproduced as follows:

1. clone `ousnius/nifly` into a disposable directory and check out
   `70da91c1304f7d405b0a3e396df0db4d15bee9d8`;
2. create a disposable .NET `10.0` console project;
3. add `niflysharp` package `2.0.4`;
4. for each fixture, create `NifFile`, call `Load`, record `IsValid`,
   `IsSSECompatible`, and `GetHeader().GetNumBlocks()`, then iterate
   `GetShapes()` and texture slots `0` through `8` using
   `GetTexturePathByIndex`; and
5. catch and record the exception type without retrying, saving, or opening
   any referenced target.

Artifact manifest:

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `TestNifFile_Skinned_SE.nif` | 21,861 | `17D09B29BB9BCB32D1E9A4D09FF51683852BEAB4C43D44E2F24BAD3344EBD6A5` |
| `TestNifFile_Corrupted.nif` | 9,317 | `181489CC8E73CAB0B931BA0F8CE8CAFCCE1E468E25A731BCF3A4563F3C93933B` |
| `niflysharp.2.0.4.nupkg` | 2,164,083 | `94460D3A5D995664F18F44FF48DAFC1B74F2B3103E0C3A847708A0D562F40810` |

Side effects were limited to network reads, a disposable
`%LOCALAPPDATA%\Temp` source/project directory, .NET build output, and the
ordinary user NuGet package cache. The probe did not read or write Skyrim,
MO2, a real profile, the game root, or any repository file other than this
report. No fixture was saved or mutated.

## 4. Core model: extract typed edges, then resolve them

Referenced-file completeness is not a property of an extension alone. It is a
join between:

1. an **effective source asset**;
2. a versioned parser and allowlisted source field;
3. a typed non-empty reference extracted from that field;
4. format- and field-specific path interpretation;
5. the selected snapshot's qualified effective provider index; and
6. applicability or requiredness semantics strong enough for the intended
   conclusion.

A retained edge should include at least:

- source logical path, exact source provider, source kind, and source hash;
- parser, parser version, format/version discriminator, and extraction-rule
  version;
- block/record/field/slot identity and original reference text;
- normalized `Data`-relative lookup key or a typed rejection reason;
- target provider, container/member identity, and winning/shadowed state when
  known;
- applicability and requiredness state;
- resolution state: present, absent, inaccessible, ambiguous, unsupported, or
  unresolved because provider semantics are not qualified; and
- observation, coverage-gap, and candidate provenance.

This design allows multiple upstream sources to converge on one target and
lets one missing target contribute to a larger case without creating
all-pairs comparisons.

## 5. Format capability survey

The following table is a bounded analyzer survey, not a final product
taxonomy. “Exact” means a typed reference can be observed; it does not
automatically mean that absence is a user-facing finding.

| Surface | Outgoing-reference evidence | Efficient initial treatment | Current conclusion |
|---|---|---|---|
| Skyrim SE NIF (`.nif`, and only other files that pass an accepted NIF format/version discriminator) | `BSShaderTextureSet` stores texture strings; `BSEffectShaderProperty` stores source, normal, greyscale, environment-map, and environment-mask strings; `BSBehaviorGraphExtraData` stores `behaviorGraphFile`. Older `NiSourceTexture.fileName` is also modeled, but its applicability must be version-gated. | Parse each effective NIF once, extract only allowlisted typed fields, and resolve deduplicated paths through one provider index. | **Best first slice.** Exact reference observations are feasible. Slot applicability, requiredness, path rules, and parser safety still require fixtures. |
| Plugin records (`.esm`, `.esp`, `.esl`) | Many record fields can point to models, icons, sounds, scripts, interfaces, and other assets. | RQ-024 must select and independently qualify Mutagen field families. Emit those fields into the same edge/index contract. | **Cross-layer source, not owned here.** Do not scan generated object strings or expand beyond the ADR-0009 allowlist. |
| Skyrim behavior/project HKX (`.hkx`) | Pandora's typed structures expose character animation names, behavior filename, skeleton filename, project character filenames, clip `animationName`, and behavior-reference `behaviorName`. | A version-pinned Skyrim-specific adapter could extract named fields and reconstruct their field-specific base directories. | **Promising but deferred.** This surface is coupled to generated-output semantics and proprietary/complex binary structures. Raw `.hkx` string scanning is not authority. |
| Compiled Papyrus (`.pex`) | Script object/type/property relationships and string-table content require their own semantic qualification. | Consume only RQ-022's accepted typed outputs later. | **Owned by RQ-022.** A `.pex`-looking string is not sufficient evidence of an asset reference. |
| Interface SWF (`.swf`) | The published SWF format has static `ImportAssets`/`ImportAssets2` URL fields, while ActionScript can construct or load targets dynamically. | A version-pinned parser such as Ruffle's SWF parser could extract allowlisted static tags without executing bytecode. | **Partial and deferred.** Static imports can be exact; whole-interface completeness cannot be established from them. Skyrim's actual path rules and allowable dynamic behavior need fixtures. |
| Scaleform GFX (`.gfx`) | No sufficiently qualified Skyrim SE parser/format contract was established in this survey. | Inventory and report coverage; do not reinterpret it as ordinary SWF by extension. | **Unsupported for reference completeness.** |
| Configuration and text (`.ini`, `.toml`, `.json`, `.yaml`, `.xml`, `.txt`, and tool-specific files) | Some named keys contain file paths; arbitrary text contains many path-like false positives. | Use RQ-021's versioned named schemas and generic syntax boundary. | **Owned by RQ-021.** Only schema-qualified keys can become exact edges. |
| Generated-output manifests and behavior tables | Named generators expose tool-specific project, input, and output relationships. | Consume adapters and generic provenance from RQ-020. | **Owned by RQ-020.** Generated filenames or hashes can be dependencies without being embedded asset paths. |
| BSA (`.bsa`) | The container provides member names and bytes, not application or precedence by itself. Mutagen `0.54.2` can enumerate `IArchiveFile.Path` and open member streams. | Enumerate names once for activated, qualified archives; defer member decompression until a targeted parser or content check needs bytes. | **Provider container, not a reference source by default.** Applicability and precedence remain gated by ADR-0008/0009 and EVAL-0051. |
| DDS (`.dds`) | The documented DDS header describes texture dimensions, format, mip levels, arrays, and payload; this survey found no external-file path field. | Treat as a leaf for reference completeness. A separate structural validator may inspect magic/header/payload bounds. | **Not an outgoing-reference source.** Presence does not prove a usable or appropriate texture. |
| Audio and lip containers (`.wav`, `.xwm`, `.fuz`, `.lip`) | No qualified external-path field was established here. FUZ's contained components are a container-integrity question, not a free-standing path lookup. | Treat as leaf/container targets until a named validator is researched. | **No outgoing-reference claim.** Missing relationships based on naming conventions need separate adapters. |
| Morph/geometry companions (`.tri`, `.egm`, `.egt`) | No qualified outgoing-path field was established here; their relationships are commonly supplied by a plugin, another asset, or naming convention. | Treat as target/leaf formats initially. | **No outgoing-reference claim.** Convention-derived completeness is a later named analyzer, not raw path extraction. |
| Native components (`.dll`) | Imports and version relationships are not `Data` asset paths in this analyzer's sense. | Consume RQ-019's bounded static observations. | **Owned by RQ-019.** Never load a DLL to discover dependencies. |

This matrix deliberately distinguishes “no outgoing reference established”
from “the format can never refer to anything.” Unsupported and unevaluated
variants remain coverage gaps.

### 5.1 Parser/extractor alternatives

Alternatives were compared on Skyrim SE typed-field coverage, maintainability,
host-language fit, malformed-input containment, ability to serve as an
independent oracle, and implementation cost.

| Alternative | Strengths | Rejection criteria or limitations | Proposed role |
|---|---|---|---|
| Pinned `nifly` C++ | Current Skyrim SE support; direct typed APIs; used by established asset tools; GPL-compatible with Infinium's accepted license direction. | Native parser; the inspected source exposes many file-controlled counts and allocations; current inspected revision has no release tag; local malformed fixture crashed through its wrapper. | Leading capability source and possible adapter only after isolation and adversarial qualification. |
| Pinned `niflysharp` package | Convenient .NET surface over current `nifly`; exact `2.0.4` package successfully extracted the trial references. | It inherits the native parser's crash boundary, emitted a package-resolution warning, and is not an independent semantic oracle for `nifly`. | Useful integration prototype, not accepted production dependency. |
| NifSkope/NifTools implementation | Independent field naming and mature inspection behavior; useful for hand-audited expectations. | Desktop editor architecture is broader than the required read-only extractor; not independently proven safe for bulk hostile input. | Fixture oracle/corroboration, not the initial embedded parser. |
| Narrow first-party read-only NIF extractor | Can expose only the required blocks, apply explicit bounds, and avoid editing APIs. | Significant binary-format and conformance burden; risks silently missing version/block variants; still needs an independent oracle. | Viable only if existing parsers cannot meet isolation, packaging, performance, or coverage gates. |
| Extension plus raw-string scan | Very cheap and language-neutral. | Cannot distinguish fields, versions, optionality, embedded names, or false positives; cannot provide trustworthy provenance. | Rejected as finding authority; at most a development-only lead generator. |

No option is selected here. Parser/dependency selection becomes an ADR input
only after the application stack and the adversarial/fixture results exist.

## 6. NIF capability and limits

At the inspected `nifly` revision:

- `GetTexturePathRefs` walks a shape's `BSShaderTextureSet`, the five explicit
  `BSEffectShaderProperty` texture strings, and older
  `NiTexturingProperty`/`NiSourceTexture.fileName` relationships;
- `BSBehaviorGraphExtraData` has a typed `behaviorGraphFile` string reference;
- the project claims Skyrim Special Edition read/write support and preserves
  blocks unknown to the library; and
- its supplied normalization test maps
  ` \Data\\Textures//white.dds` plus whitespace to
  `textures\white.dds`.

Those are parser observations, not universal engine authority. In particular:

- `nifly`'s path-trimming helper mutates strings for editing use cases and
  must not simply be copied as Infinium's lookup semantics;
- retaining an unknown block does not mean Infinium knows whether that block
  contains an external reference;
- `GetExternalGeometryPathRefs` concerns `BSGeometry` external meshes in the
  inspected code and must not be enabled for Skyrim SE without a positive
  Skyrim fixture/version proof;
- empty texture slots are common and are not missing references;
- even a non-empty slot may be optional or inactive under its shader flags;
  and
- a behavior-graph field's base-directory and runtime applicability still
  require independent fixtures before absence becomes an exact finding.

NifSkope independently labels `NiSourceTexture`'s `File Name` and
`BSBehaviorGraphExtraData`'s `Behaviour Graph File`, which is useful
corroboration. Neither implementation should be the sole expected-result
oracle for EVAL-0059.

### 6.1 First-proof NPC-to-FaceGen identity and provenance dependency

This is a convention-derived **record-to-asset lookup**, not an embedded NIF
reference and not an expansion of the NIF block/field allowlist.

At the exact accepted Mutagen `0.54.2` revision,
`NpcCommon.GetResolvedAssetLinks` constructs these candidate `Data`-relative
keys:

```text
textures\actors\character\facegendata\facetint\
  <Npc.FormKey.ModKey.FileName>\<Npc.FormKey.ID as X8>.dds

meshes\actors\character\facegendata\facegeom\
  <Npc.FormKey.ModKey.FileName>\<Npc.FormKey.ID as X8>.nif
```

The texture/model asset types supply the `Textures`/`Meshes` base folders.
`FormKey` defines `ModKey` as the plugin from which the record **originates**
and `ID` as the record-local ID with master indices removed. Consequently:

- an override does not change the lookup directory to the winning override
  plugin; the directory remains the originating plugin filename, including
  `.esm`, `.esp`, or `.esl`;
- light master style does not imply a `.esl` filename: an ESL-flagged `.esp`
  still uses its exact `.esp` origin filename in the directory;
- the filename is the origin-local ID padded to eight uppercase hexadecimal
  digits, not a runtime/load-order FormID;
- a full-origin example with local ID `0x1234` therefore yields
  `00001234.nif` and `00001234.dds`; and
- Mutagen's small-master translation removes the `FE` marker and light-master
  index, retains the 12-bit `LightId`, and the same `X8` formatting yields,
  for example, `00000ABC`, not an `FE...` filename.

This source-level result establishes the exact Mutagen candidate algorithm for
full and light origin `FormKey`s. The archived Creation Kit page independently
corroborates the two conventional path shapes and eight-hex filename, but it
does not qualify current Skyrim SE light-plugin lookup. Full-origin and
light-origin runtime/path behavior must therefore pass independent EVAL-0052
fixtures before this relationship is supported.

The pinned method emits the two links only when all of the following hold:

- `Npc.Race` resolves;
- the resolved `Race.Flags` includes `FaceGenHead`;
- `Npc.Configuration.Flags` does not include `UseTemplate`; and
- `Npc.Configuration.TemplateFlags` does not include `Traits`.

Those are the exact candidate applicability inputs. If the race is unresolved,
the race flag is absent, or either template condition applies, Infinium must
record an inapplicable/unsupported direct-link state, not report missing
FaceGen. The source does not prove which template NPC supplies effective face
data, whether a non-`FaceGenHead` race uses another route, or whether every
emitted link is required by the runtime.

Path identity and provider identity are deliberately separate. A mod folder
or archive owned by any source may provide or shadow the exact logical key
whose directory names the NPC's originating plugin. The selected profile's
provider index must retain:

- the origin `FormKey`, origin plugin filename, and local ID used to derive
  the key;
- the effective NPC record and its winning plugin;
- the exact FaceGen logical key;
- the winning FaceGen provider and complete known shadow chain;
- loose versus archive-member/container identity and hashes; and
- applicability, provider-qualification, resolution, and coverage states.

A winning NPC override and a winning FaceGen provider are independent facts.
Finding `WinnerPatch.esp\00001234.nif` does not satisfy a
`BaseActors.esm\00001234.nif` lookup merely because `WinnerPatch.esp` wins the
record; conversely, a later mod folder can correctly win the
`BaseActors.esm\00001234.nif` key.

Normalization must preserve the exact derived path and separately produce the
qualified provider-index comparison key. It may normalize case and separators
only after EVAL-0051 qualifies those rules. It must not substitute a winning
plugin name, add an `FE` prefix, guess a different extension, accept the same
basename in another directory, or probe outside the captured `Data`
namespace. Loose presence can be exact after the loose-provider gate; archive
presence/absence and shadowing remain conditional on the archive route.

Required **loose-only M0 exit qualification** controls are:

- **positive:** full-origin, `.esl` light-origin, and ESL-flagged `.esp`-origin
  NPCs meeting the race/template gates, with independently specified expected
  mesh and tint keys;
- **override/provenance:** a later plugin wins the NPC while the path retains
  the origin plugin, and a separately ordered mod folder shadows the exact
  origin-named FaceGen key;
- **provider:** a complete loose-provider chain and winner, exact loose
  absence in an atomic profile where archives cannot participate, loose
  shadowing, and an explicit archive-excluded coverage state;
- **matched negative:** same basename under the winner-plugin or another
  plugin directory, runtime/load-order-prefixed ID, wrong local ID, wrong
  extension, only one of the mesh/tint pair, and unrelated FaceGen files;
- **applicability negative:** unresolved race, race without `FaceGenHead`,
  `UseTemplate`, and template `Traits`, each producing abstention/coverage
  rather than a missing-file claim;
- **normalization/adversarial:** case/separator variants, collisions after
  normalization, invalid plugin/FormKey input, path escape/device/absolute
  syntax, duplicate providers, and changed-during-capture state; and
- **malformed/unsupported:** malformed NPC/RACE fields, missing master,
  deleted winner, unresolved template relationship, out-of-contract local ID
  shape, corrupt/inaccessible NIF or DDS, and any archive-dependent lookup
  represented as unsupported rather than exact presence or absence.

Later **conditional archive** qualification should cover qualified active
archives, inactive archives, loose-over-archive, archive-over-archive,
container/member invalidation, and unqualified-archive abstention. Those cases
remain required before archive-backed FaceGen conclusions are supported, but
they are not part of the initial loose-only M0 exit gate. This follows
ADR-0009 and RESEARCH-0013: the first M1 proof must use a profile whose FaceGen
dependency closure is provably archive-independent.

Without rendering, a qualified result may establish the expected logical
mesh/tint keys, their presence or qualified absence, exact effective
providers, shadowing, and byte identities. It may not establish visual
correctness, morph/tint agreement, absence of the dark-face symptom, semantic
appropriateness, engine use, or runtime behavior. NIF/DDS structural validity
is a separate parser result.

Because the current research has not executed that independent loose-only
full/light, applicability, normalization, shadowing, malformed, unsupported,
and archive-independence matrix, this relationship remains an exit-blocking
RQ-023 qualification dependency for the first M1 record-plus-FaceGen
provenance proof. Archive-positive support remains a later conditional gate.

## 7. Provider-aware resolution

### 7.1 Required index

Build a single snapshot-bound map from canonical `Data`-relative lookup key to:

- effective winner;
- full known provider chain;
- provider kind: loose, archive member, generated, unmanaged, or unknown;
- mod/source identity;
- physical container and member identity where applicable;
- original path spelling and comparison key;
- accessibility, size, and hash state; and
- provider-reconstruction confidence and qualification status.

Candidate generation then becomes approximately:

- `O(N)` to build the effective namespace from `N` provider entries;
- `O(S)` to parse each selected source asset once;
- `O(R)` hash lookups for `R` extracted, deduplicated references; and
- targeted hashing or parsing only where the next conclusion requires it.

No all-pairs asset comparison or LLM call is needed.

### 7.2 Archive boundary

The BSA member list alone cannot answer whether a referenced target exists
effectively. Infinium must know that the archive is active and where that
member falls relative to loose files and other applicable archives.

Therefore:

- a member in a **qualified active archive** may satisfy a reference;
- a member in an inactive or non-applicable archive does not satisfy it;
- a same-named member in an archive whose activation or precedence is
  unqualified yields an unresolved provider result, not an exact missing-file
  result;
- Mutagen's low-level BSA reader may enumerate or stream exact members, but
  its standard applicable-archive discovery is not authority under ADR-0009;
  and
- until EVAL-0051 qualifies the archive route, a qualified loose winner can
  prove presence, but absence is exact only when the captured fixture proves
  that no unqualified archive could satisfy the key. Archive-dependent cases
  must lower coverage rather than fabricate absence.

Archive decompression must be bounded by container size, declared uncompressed
size, actual bytes produced, time, memory, and per-run quotas. An archive
container hash invalidates its member-derived results unless later evidence
proves a safe member-equivalence shortcut, as required by ADR-0010.

## 8. Path normalization and filesystem safety

Every extracted path is untrusted data. Resolution must remain inside the
captured `Data` namespace and must never turn a path string into an
unconstrained host-filesystem probe.

A format adapter should:

1. preserve the raw reference exactly;
2. reject or type as unsupported NUL-containing, absolute drive, UNC, device,
   rooted, alternate-data-stream, or traversal paths;
3. normalize separators and redundant current-directory segments only under
   qualified Skyrim/MO2 rules;
4. apply a namespace prefix such as `textures\` or `meshes\` only when that
   exact field's semantics establish it;
5. preserve original spelling while producing a comparison key consistent
   with the qualified Windows/MO2/BSA behavior;
6. avoid guessing extensions, suffixes, directories, or language variants;
   and
7. resolve only against the immutable provider index for the captured
   snapshot.

Case folding, Unicode handling, trailing dots/spaces, reserved Windows names,
and duplicate paths differing only by case require explicit fixtures. General
Windows intuition is not sufficient to declare game/VFS/archive equivalence.

## 9. Exact observations, heuristics, and findings

### Exact structural observation

An allowlisted field in a supported format/version contains a specific,
non-empty reference. The observation retains its exact source bytes, provider,
field, parser, and raw path.

### Exact resolution observation

The qualified provider index either contains or does not contain the canonical
target for the captured snapshot.

### Candidate

An exact reference is absent, malformed, ambiguous, resolves through an
unexpected provider, or cannot be resolved because archive/path semantics are
unsupported. This is enough to queue deterministic adjudication or narrowly
scoped documentation/LLM work.

### Finding

A missing-reference finding additionally requires evidence that the reference
is applicable and sufficiently required in the effective source. Optional
shader slots, inactive branches, engine fallbacks, dynamically generated
paths, alternate language assets, or merely shadowed source files can prevent
that conclusion.

### Heuristic lead

A raw string or filename convention resembles a path. Such leads may rank
already bounded candidates but cannot independently produce a missing-asset
finding. An LLM must not convert an untyped string into deterministic
provenance.

Target presence only proves that the effective namespace can supply bytes at
that path. It does not prove format validity, version compatibility, visual
correctness, semantic pairing, or absence of corruption. Those are separate
observations or named analyzers.

## 10. Caching and dependency invalidation

Cache extraction by:

- source SHA-256;
- parser identity and version;
- format/version allowlist;
- extraction-rule version; and
- safety-limit profile.

Cache resolution by:

- the extraction-result identity;
- path-normalization rule version;
- selected-profile provider-index identity;
- exact target lookup key and winner/provider state; and
- archive container/member dependencies when applicable.

A source-byte change invalidates its extracted edges. A provider-index change
only invalidates resolutions whose dependency closure intersects the changed
key/provider state; it should not force unrelated NIFs to be reparsed. A BSA
container change invalidates member results unless member equivalence was
independently established. Parser crashes, timeouts, unsupported versions, and
partial extraction are cached only as bounded run evidence, never as
successful coverage.

## 11. Malformed and adversarial inputs

Format parsers and archive readers must be assumed hostile until qualified.
Required controls include:

- isolated worker process rather than UI or durable job-store process;
- read-only handles to exact snapshotted inputs;
- cancellation and per-file time, memory, output-count, nesting, and byte
  limits;
- checked arithmetic for block counts, sizes, offsets, and decompressed
  lengths;
- bounds on strings, arrays, block graphs, references, archive members, and
  deduplicated edges;
- validation that offsets and spans remain inside the captured bytes;
- cycle handling for internal block/reference graphs;
- protection against truncated files, unknown versions, unknown block types,
  duplicate names, case collisions, invalid encodings, and decompression
  bombs;
- no external path traversal or automatic opening of referenced targets
  during parsing;
- snapshot revalidation against mid-read file changes; and
- a typed malformed, partial, timeout, inaccessible, or unsupported result
  with honest coverage.

The local corrupted-NIF `SEHException` is a concrete reason for this boundary,
not a theoretical precaution. A parser crash must lose one bounded unit of
work, not the scan coordinator or user interface.

## 12. EVAL-0059 and negative-control design

EVAL-0059 should begin with a synthetic Skyrim SE NIF fixture whose effective
winner contains one allowlisted, applicable texture-slot reference to a DDS
path.

### Positive

- source NIF is an effective loose winner;
- its applicable typed slot references
  `textures\infinium_eval\missing.dds`;
- no qualified effective provider supplies that key; and
- the result retains NIF provider, block/shape/slot, raw reference, normalized
  key, provider-index identity, and absence proof.

### Matched negative

The identical NIF and profile state include a valid effective DDS at that
path. No missing-reference candidate is produced, and the satisfying provider
is retained.

### Required boundary and adversarial controls

- empty optional texture slot: no reference and no candidate;
- path-like `.dds` text in a node name: no typed reference;
- same basename in a different directory: does not satisfy the reference;
- separator/case variants: agree only under the qualified comparison rules;
- target only in a qualified active BSA: satisfies the reference;
- target only in an inactive archive: does not satisfy it;
- target in an archive whose semantics are not qualified: unresolved gap, not
  exact absence;
- reference containing `..`, an absolute path, UNC/device syntax, NUL, or an
  alternate-data-stream separator: rejected/unsupported without probing
  outside `Data`;
- unrelated missing DDS not referenced by any supported source: no candidate;
- referenced target present but malformed: completeness passes while a
  separate structural check may fail;
- source NIF is shadowed and not effective: it does not describe the selected
  runtime state;
- corrupted/truncated/unknown-version NIF: bounded parser failure and coverage
  gap;
- provider/mod rename with identical effective bytes and identity facts:
  conclusion is unchanged;
- unrelated provider change: cached edge and resolution remain valid where
  dependency closure proves independence; and
- planted reference survives candidate indexing without any all-pairs or LLM
  comparison.

The fixture's expected reference and absence must be specified independently
of the production parser. A second parser/source-level hand audit may
corroborate it, but the implementation under test cannot generate its own
oracle.

## 13. Contrary evidence, uncertainty, and limitations

- One successful NIF fixture demonstrates feasibility, not format-wide
  coverage or high-end performance.
- The supplied corrupted fixture crashed the native wrapper rather than
  returning a typed parse failure.
- `nifly` is an implementation, not Bethesda runtime documentation; its
  editing-oriented normalization behavior is evidence to test, not engine
  authority.
- NifSkope corroborates field identity but shares the broader community
  reverse-engineering knowledge base, so it is not fully independent ground
  truth.
- The trial did not reconstruct MO2 providers, read a BSA, verify archive
  precedence, or test file changes during capture.
- No current primary source established complete Skyrim SE GFX, audio,
  morph-companion, or dynamic-interface reference semantics.
- A present target may still be corrupt, wrong-version, incomplete, or
  semantically incompatible; reference completeness cannot claim otherwise.
- A missing explicit path may still be harmless when a field is inactive,
  optional, dynamically replaced, or governed by an unqualified fallback.
- The current report does not establish the number of effective NIFs,
  references, parse latency, memory, or candidate rate on small or high-end
  profiles. RQ-027 and the Wave C candidate benchmark must measure them.

These limitations constrain support labels and evaluation design; they do not
invalidate the narrow NIF feasibility result.

## 14. Recommendation

Adopt the following **proposed** scope for the first asset-analysis increment:

1. build the snapshot-bound effective provider index once;
2. implement a version-pinned, read-only Skyrim SE NIF adapter;
3. initially allowlist `BSShaderTextureSet` and the explicit
   `BSEffectShaderProperty` texture fields, with field/slot applicability
   qualified by synthetic fixtures;
4. retain `BSBehaviorGraphExtraData.behaviorGraphFile` as the next typed edge,
   but do not claim missing behavior graphs until its path and applicability
   rules are qualified;
5. join later allowlisted plugin asset fields from RQ-024 into the same edge
   contract, and admit the convention-derived NPC-to-FaceGen relationship in
   section 6.1 only after its independent fixture gate passes;
6. treat DDS and similar payload assets as leaves for reference completeness;
7. keep HKX, SWF, GFX, PEX, configuration, generated outputs, and
   convention-derived pairings behind their named adapters or explicit gaps;
   and
8. run native/complex parsers in an isolated, bounded worker and never use raw
   string scanning as missing-reference authority.

This gives Infinium a useful deterministic capability at high mod counts:
parse each supported effective source once, perform indexed lookups, and send
only grounded anomalies to later adjudication. The LLM adds no value to
existence checks themselves; its potential value begins after the tool has a
small evidence-bearing candidate, for example to interpret documentation,
connect symptoms, or compare plausible resolutions. The extraction, indexing,
and existence stages are local, offline-capable, and require no network or LLM
usage after their parser dependencies are installed.

### Proposed support labels

- **Supported/exact:** allowlisted NIF field extraction and positive
  loose-provider resolution after parser/path fixtures pass; negative
  resolution only where archive independence is proven.
- **Conditionally supported:** archive-backed resolution after EVAL-0051
  qualifies activation and precedence.
- **Pending/exit-blocking for the first M1 proof:** the exact
  NPC-to-FaceGen convention and applicability/provider relationship in
  section 6.1 until its loose-only full/light and archive-independence fixture
  matrix passes.
- **Observed but not finding-authoritative:** non-empty typed references whose
  requiredness/applicability is not yet qualified.
- **Heuristic:** path-like strings and naming conventions.
- **Unsupported:** unqualified formats/versions/blocks, GFX semantics,
  dynamic interface loads, and provider behavior outside the accepted
  reconstruction.

No new ADR is justified yet. The investigation supplies an analyzer and
evaluation scope, while parser selection, process isolation, exact schemas,
and archive integration belong in later architecture decisions or milestone
plans.

Confidence is **high** that typed, indexed NIF texture-reference checks are
feasible and much cheaper than all-pairs analysis; **medium** that the current
`nifly` family is the best production parser; and **low/not established** for
complete archive-backed, HKX, SWF/GFX, and convention-derived coverage. The
preconditions for a supported result are a qualified parser/field allowlist,
qualified path semantics, a stable effective provider snapshot, and explicit
archive qualification where archives can satisfy the target.

## 15. Enabled follow-ups and accepted RQ-023 disposition

This report enables the coordinator to propose:

- refining EVAL-0059 with the positive, matched-negative, provider, malformed,
  and path-safety controls in section 12;
- reconciling the typed edge and provider-index contract with RQ-024's
  completed semantic-roadmap output and the Wave C candidate/index benchmark;
- adding the selected NIF parser, isolation boundary, and exact dependency
  version to a later architecture/dependency ADR after the stack decision and
  parser qualification;
- retaining effective archive resolution as conditional on EVAL-0051 rather
  than expanding ADR-0009 implicitly; and
- requiring the independent loose-only full/light record-to-FaceGen identity,
  applicability, normalization, shadowing, and archive-independence matrix in
  section 6.1 before resolving RQ-023. RESEARCH-0034 subsequently passed that
  matrix at its declared pre-resolved-input boundary; archive-positive support
  remains conditional later work.

No taxonomy or accepted product-language change is proposed by this
investigation.

## 16. Requirements-and-evidence traceability

| Authority or output | Evidence in this report | Result or required follow-up |
|---|---|---|
| SCOPE-005, ADR-0008 | Sections 4, 7, and 8 distinguish effective sources/targets from physical files and bind resolution to the selected provider index. | Loose support can qualify first; archive-backed results remain conditional. |
| SNAP-001 through SNAP-006, ADR-0010 | Sections 4, 7, and 10 specify source hashes, provider-index identity, typed dependency closure, and conservative archive invalidation. | Carry these keys into the eventual asset-analyzer contract and candidate benchmark. |
| EVID-001 through EVID-006 | Section 9 separates typed references, resolution, candidates, heuristics, and findings. | Do not promote raw strings or target presence beyond their evidence strength. |
| COVER-001 through COVER-003 | Sections 5, 6, 7, and 13 retain unsupported formats, unknown blocks, unqualified archives, and parser failures as gaps. | Coverage reporting is mandatory for every selected source and format. |
| ANALYSIS-005 | Sections 4 and 14 define a shared edge contract for later plugin → NIF → DDS/HKX reasoning. | RQ-024 selects independently qualified plugin record fields. |
| ANALYSIS-013 | The local trial and sections 5, 6, and 14 establish a feasible first format-specific reference check. | Qualify the NIF field/path allowlist before calling it supported. |
| ANALYSIS-016 | Sections 2, 5, 13, and 14 state scope, exclusions, dependencies, evidence thresholds, cost shape, and maturity. | Encode these declarations in the eventual asset-analyzer contract. |
| ANALYSIS-017, EVAL-0032 | Sections 4, 7, 9, and 12 use indexed candidates and forbid all-pairs/LLM existence checks. | Benchmark recall, volume, latency, memory, and escalation rate. |
| EVAL-0086 | Sections 2, 5, and 9 keep technical asset surfaces separate from affected game areas, consequences, severity, and other taxonomy concepts. | Execute the accepted classification-separation and taxonomy-version specification. |
| AUTH-001 through AUTH-003, EVAL-0046 | Sections 3.2, 8, and 11 describe read-only side effects, path confinement, and parser isolation. | Production qualification must prove non-mutation and crash containment. |
| EVAL-0051 | Sections 7.2 and 12 include active, inactive, and unqualified archive controls. | Do not claim full effective absence before the archive route passes. |
| EVAL-0052 | Sections 5 and 14 leave plugin field selection to RQ-024 and the accepted Mutagen allowlist. | Expected plugin edges require independent binary/semantic fixtures. |
| EVAL-0059 | Section 12 supplies positive, matched-negative, provider, malformed, path, and cache controls. | Coordinator may refine the case specification without changing its product intent. |
| M1 record/FaceGen provenance proof, RQ-023 | Section 6.1 defines origin FormKey/path identity, effective-provider separation, applicability, light-plugin boundary, and the no-rendering conclusion limit. | RESEARCH-0034 completed the loose-only matrix at the pre-resolved-input boundary; archive-positive and production-adapter support remain conditional. |

## 17. Remaining questions

- Which exact NIF versions, blocks, shader flags, and texture slots form the
  first supported allowlist?
- What independently specified fixtures qualify each field's path prefix,
  case/separator behavior, optionality, and fallback behavior?
- Which parser implementation is selected after architecture and adversarial
  testing: pinned `nifly`, `niflysharp`, a narrower first-party extractor, or
  another implementation?
- What archive route passes EVAL-0051, and how are member-level equivalence
  and invalidation represented?
- Which plugin fields selected by RQ-024 supply the first plugin → asset
  edges?
- Does a later HKX adapter provide enough value beyond the generated-output
  analyzers to justify its parser and fixture cost?
- Which interface cases justify static SWF support despite dynamic-loading and
  GFX gaps?
- Does the section 6.1 independent matrix qualify full- and light-origin
  FaceGen identity and Race/template/provider applicability exactly enough for
  the first M1 proof? Voice/lip and morph companions remain separate later
  questions.

These questions refine the proposed scope. They do not justify guessing in any
milestone.

## 18. Sources

Primary and maintained implementation sources:

- [nifly README at inspected commit](https://github.com/ousnius/nifly/blob/70da91c1304f7d405b0a3e396df0db4d15bee9d8/README.md)
- [nifly typed texture extraction](https://github.com/ousnius/nifly/blob/70da91c1304f7d405b0a3e396df0db4d15bee9d8/src/NifFile.cpp#L934-L989)
- [nifly texture-path normalization](https://github.com/ousnius/nifly/blob/70da91c1304f7d405b0a3e396df0db4d15bee9d8/src/NifFile.cpp#L1170-L1250)
- [nifly behavior-graph extra-data field](https://github.com/ousnius/nifly/blob/70da91c1304f7d405b0a3e396df0db4d15bee9d8/include/ExtraData.hpp#L317-L329)
- [nifly supplied normalization test](https://github.com/ousnius/nifly/blob/70da91c1304f7d405b0a3e396df0db4d15bee9d8/tests/TestNifFile.cpp#L65-L91)
- [Mutagen `0.54.2` NPC FaceGen link construction](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Records/Major%20Records/Npc.cs#L48-L60)
- [Mutagen `0.54.2` origin-local `FormKey`](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Plugins/FormKey.cs#L11-L58)
- [Mutagen `0.54.2` full/light FormID translation](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Plugins/Masters/SeparatedMasterPackage.cs#L295-L365)
- [Mutagen `0.54.2` Skyrim texture/model base folders](https://github.com/Mutagen-Modding/Mutagen/tree/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Skyrim/Assets)
- [Creation Kit Wiki `Dark Face Bug` revision 9182](https://ck.uesp.net/w/index.php?title=Dark_Face_Bug/ja&oldid=9182)
- [NifSkope named reference fields at inspected commit](https://github.com/niftools/nifskope/blob/3a85ac55e65cc60abc3434cc4aaca2a5cc712eef/src/spells/blocks.cpp#L164-L191)
- [`niflysharp` 2.0.4 package record](https://www.nuget.org/packages/niflysharp/2.0.4)
- [Mutagen `0.54.2` archive reader documentation](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/docs/Archives.md)
- [Mutagen `IArchiveFile` read-only member API](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Archives/IArchiveFile.cs)
- [Mutagen Skyrim BSA reader](https://github.com/Mutagen-Modding/Mutagen/blob/282bb99a77b2df7f1b092b06270e8e3c8fb55463/Mutagen.Bethesda.Core/Archives/Bsa/BsaReader.cs)
- [Pandora character HKX path-bearing fields](https://github.com/Monitor221hz/Pandora-Behaviour-Engine-Plus/blob/d6344e394c8a9ecfd2966cc0d84bbbdf73976b19/Pandora%20Behaviour%20Engine/Models/Patch.Skyrim64/Hkx.Packfile/PackFileCharacter.cs)
- [Pandora project-to-character/behavior/skeleton resolution](https://github.com/Monitor221hz/Pandora-Behaviour-Engine-Plus/blob/d6344e394c8a9ecfd2966cc0d84bbbdf73976b19/Pandora%20Behaviour%20Engine/Models/Patch.Skyrim64/Project.cs)
- [Microsoft DDS programming guide](https://learn.microsoft.com/en-us/windows/win32/direct3ddds/dx-graphics-dds-pguide)
- [Microsoft DDS header reference](https://learn.microsoft.com/en-us/windows/win32/direct3ddds/dds-header)
- [Adobe SWF file-format specification version 19 mirror](https://open-flash.github.io/mirrors/swf-spec-19.pdf)
- [Ruffle repository and maintained SWF parser](https://github.com/ruffle-rs/ruffle)

Repository authority and related investigations:

- [Archived M0 research plan](../../plans/milestones/README.md)
- [Research questions](../open-questions.md)
- [Taxonomy dependency map](../taxonomy-dependency-map.md)
- [RESEARCH-0014 — Root and native component surfaces](RESEARCH-0014-root-native-component-surfaces.md)
- [RESEARCH-0015 — Generated-output tool surfaces](RESEARCH-0015-generated-output-tool-surfaces.md)
