# RESEARCH-0015 — Generated-output tool surfaces

Status: Completed
Disposition: recommendation accepted by project owner
Date: 2026-07-25
Last reviewed: 2026-07-26
Researcher: Codex agent
Primary RQ: RQ-020 — Which generated-output tools expose usable manifests or
stable formats?
M0 wave: C
Decision enabled: generated-output analyzer roadmap, RQ-036 technical-surface
input, and refinement of EVAL-0058

Acceptance note: The project owner accepted this report's bounded
recommendation on 2026-07-25 through
[RESEARCH-0024](RESEARCH-0024-wave-c-analysis-taxonomy-and-scale-integration.md).
Proposal-era registry/taxonomy wording below is retained as provenance; it
does not imply that a named generator adapter or freshness check is qualified.

## 1. Question and accepted authority

This investigation asks which common Skyrim SE output generators leave enough
structured, durable evidence for a read-only Infinium analyzer to determine:

1. which tool probably produced an output;
2. whether a generation run completed;
3. which configuration or selected modules governed it;
4. which local inputs it depended on;
5. which outputs it declared or actually wrote; and
6. whether the installed output is demonstrably current, mismatched, stale, or
   indeterminate for the selected MO2 snapshot.

The answer is constrained by:

- [SCOPE-005](../../product/requirements.md#scope-005--effective-installation):
  generated output is part of effective-installation reconstruction, but
  unsupported semantics must remain visible coverage gaps;
- [AUTH-001 through AUTH-003](../../product/requirements.md#authority-and-safety):
  Infinium is read-only through M4 and may not invoke a generator merely to
  rewrite or compare protected output;
- [SNAP-001 through SNAP-006](../../product/requirements.md#snapshot-and-reproducibility):
  every conclusion must bind to a quiescent installation snapshot and retain
  honest dependency validity;
- [EVID-001 through EVID-006](../../product/requirements.md#evidence-and-trust):
  observed artifacts, external claims, hypotheses, and findings remain typed
  and provenance-bearing;
- [COVER-001 through COVER-003](../../product/requirements.md#coverage-and-readiness):
  an unsupported or unprovable generator state is a result, not permission to
  guess;
- [ANALYSIS-005](../../product/requirements.md#analysis-005--cross-record-and-cross-layer-reasoning),
  [ANALYSIS-008](../../product/requirements.md#analysis-008--version-coherence),
  and
  [ANALYSIS-010](../../product/requirements.md#analysis-010--generated-outputs):
  generated-output analysis must connect configuration, records, assets,
  runtime state, and regeneration conditions without reducing the question to
  file age;
- [ANALYSIS-015 through ANALYSIS-017](../../product/requirements.md#analysis-015--playthrough-lifecycle-safety):
  regeneration advice must be grounded, each analyzer must declare its
  contract, and LLM escalation must follow deterministic candidate selection;
- [OPS-004](../../product/requirements.md#ops-004--high-end-scale):
  the design must remain practical for high-end modlists;
- [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md):
  generator artifacts may be observed, but generation may not be used as a
  supposedly read-only probe;
- [ADR-0007](../../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md):
  xEdit is excluded from product, development, dependency, integration, and
  evaluation boundaries;
- [ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md):
  generated files and sidecars must be attributed through the exact selected
  profile's effective provider state; and
- [ADR-0009](../../architecture/decisions/ADR-0009-skyrim-runtime-and-bethesda-semantic-support.md):
  generated Bethesda plugins may be inspected only through the qualified
  Mutagen semantic boundary; and
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md):
  freshness and reuse depend on declared dependency closure and scoped
  fingerprints, not modification time alone.

This report supplies technical-surface evidence to RQ-036. It does not define
a taxonomy of mod purposes, affected game areas, consequences, symptoms,
severity, or effect extent.

## 2. Scope, non-scope, and decision criteria

### In scope

- Pandora, Nemesis, and FNIS animation/behavior generation;
- BodySlide mesh and morph generation;
- Synthesis patch generation;
- DynDOLOD, TexGen, xLODGen, and grass-cache output;
- Wrye Bash's Bashed Patch as a materially different generated-plugin example;
- stable file formats, configuration, logs, progress markers, output
  inventories, run identity, and freshness implications; and
- generic fallback observations when a named tool or version is unsupported.

### Explicitly out of scope

- selecting a named production analyzer or delivery milestone;
- running any generator against a real MO2 profile;
- treating the private reference profile as representative or correct;
- changing, deleting, or regenerating user output;
- reverse-engineering HKX, NIF, TRI, CGID, or every generator-specific binary
  format;
- establishing a complete generated-output dependency taxonomy;
- declaring a tool compatible merely because its output files can be parsed;
- accepting an xLODGen exception to ADR-0007; and
- implementing an Infinium-owned generator wrapper or manifest writer.

### Evaluation rubric

A usable evidence surface is assessed on seven independent dimensions:

| Dimension | Strong evidence | Weak or absent evidence |
|---|---|---|
| Tool identity | Exact tool and version recorded by the run | File-name convention or user inference |
| Run completion | Durable success/failure state tied to the run | A file exists or a log ends abruptly |
| Configuration | Effective settings and selected modules retained | Current UI defaults or mutable global config |
| Input identity | Complete relevant input set with fingerprints | Load-order names, timestamps, or no input list |
| Output identity | Complete output inventory with fingerprints | Expected paths, partial cleanup list, or directory presence |
| Dependency scope | Declared mapping from inputs/settings to outputs | “Regenerate after any change” |
| Format stability | Versioned/documented schema with compatibility policy | Human log, mutable alpha format, undocumented binary |

No surveyed tool met all seven dimensions. “Strongest” below therefore means
best available partial evidence, not a complete freshness proof.

## 3. Sources and exact versions

All sources were retrieved 2026-07-25. Tagged source was preferred where a
current release existed; moving documentation is identified as such.

| Source | Version or revision | Authority and claim-level relevance |
|---|---|---|
| [Pandora repository](https://github.com/Monitor221hz/Pandora-Behaviour-Engine-Plus/tree/d6344e394c8a9ecfd2966cc0d84bbbdf73976b19) and [release](https://github.com/Monitor221hz/Pandora-Behaviour-Engine-Plus/releases/tag/v4.3.1-beta) | `v4.3.1-beta`, commit `d6344e394c8a9ecfd2966cc0d84bbbdf73976b19` | Official source. Establishes output-directory support, `ActiveMods.json`, `PreviousOutput.txt`, `Engine.log`, and generated packfile behavior. |
| [Pandora mod-setting record](https://github.com/Monitor221hz/Pandora-Behaviour-Engine-Plus/blob/d6344e394c8a9ecfd2966cc0d84bbbdf73976b19/Pandora%20Behaviour%20Engine/Mods/ModSaveEntry.cs) and [settings persistence](https://github.com/Monitor221hz/Pandora-Behaviour-Engine-Plus/blob/d6344e394c8a9ecfd2966cc0d84bbbdf73976b19/Pandora%20Behaviour%20Engine/Mods/ModSettingsService.cs) | Same revision | Defines saved patch code, active flag, and priority; proves that this is selected-module state rather than a complete run manifest. |
| [Pandora output metadata implementation](https://github.com/Monitor221hz/Pandora-Behaviour-Engine-Plus/blob/d6344e394c8a9ecfd2966cc0d84bbbdf73976b19/Pandora%20Behaviour%20Engine/Models/Patch.IO/Skyrim64/BasePackFileExporter.cs) | Same revision | Shows that `PreviousOutput.txt` contains successful output paths and is consumed as a deletion list before later generation. |
| [Synthesis repository](https://github.com/Mutagen-Modding/Synthesis/tree/b258579ee046ebb2552a8d7cad19f48869b6ee46) and [release](https://github.com/Mutagen-Modding/Synthesis/releases/tag/0.36.5) | `0.36.5`, commit `b258579ee046ebb2552a8d7cad19f48869b6ee46` | Official source. Establishes `PipelineSettings.json`, profile/group/patcher settings, run output, and version targeting. |
| [Synthesis Git patcher settings](https://github.com/Mutagen-Modding/Synthesis/blob/b258579ee046ebb2552a8d7cad19f48869b6ee46/Synthesis.Bethesda.Execution/Settings/GithubPatcherSettings.cs) | Same revision | Defines tag/branch/commit targeting and `LastSuccessfulRun` fields for repository, project path, commit, Mutagen version, and Synthesis version. |
| [BodySlide and Outfit Studio repository](https://github.com/ousnius/BodySlide-and-Outfit-Studio/tree/ecab889ae44af336160d249db9430220c0bfd5dd) and [release](https://github.com/ousnius/BodySlide-and-Outfit-Studio/releases/tag/v5.8.2) | `v5.8.2`, commit `ecab889ae44af336160d249db9430220c0bfd5dd` | Official source. Establishes slider-set/preset/config/automation formats and NIF/TRI output paths. |
| [Official BodySlide Nexus page](https://www.nexusmods.com/skyrimspecialedition/mods/201) | Author-maintained current page | Documents MO2 output in Overwrite and the expected move to a separate output mod. |
| [Nemesis repository](https://github.com/ShikyoKira/Project-New-Reign---Nemesis-Main/tree/5dfc90e5526b18e36d5887804786894fa132062c), [README](https://github.com/ShikyoKira/Project-New-Reign---Nemesis-Main/blob/5dfc90e5526b18e36d5887804786894fa132062c/README.md), and [release](https://github.com/ShikyoKira/Project-New-Reign---Nemesis-Main/releases/tag/v0.84-beta) | `v0.84-beta`, commit `5dfc90e5526b18e36d5887804786894fa132062c` | Official but old source. Establishes patch selection/workflow, behavior/animation-data output, and ambiguous editing/ownership of `animationdatasinglefile.txt`; exposes no complete run manifest. |
| [FNIS official Nexus page](https://www.nexusmods.com/skyrimspecialedition/mods/3038) | `7.6`, author-maintained legacy page | Documents redirected output, last-selected patches, temporary/log locations, and generated behavior/animation-data files. |
| [DynDOLOD documentation](https://dyndolod.info/), [TexGen help](https://dyndolod.info/Help/TexGen), [messages](https://dyndolod.info/Messages), [xLODGen help](https://dyndolod.info/Help/xLODGen), [Grass LOD help](https://dyndolod.info/Help/Grass-LOD), and [changelog](https://dyndolod.info/Changelog) | Current DynDOLOD 3 alpha documentation; changelog reported Alpha 208 when retrieved | Official author-maintained documentation. Defines output installation, presets, logs/debug logs, intermediate exports, CRC diagnostics, plugin/runtime-data coherence, and alpha-format instability. |
| [No Grass In Objects NG repository](https://github.com/DwemerEngineer/No-Grass-In-Objects-NG/tree/60691af0965a5602a994db3e0a477a3ca277540a) | Commit `60691af0965a5602a994db3e0a477a3ca277540a`; current default branch, not a release tag | Official maintained source. Defines `.cgid`/`.fail`, `PrecacheGrass.txt`, configuration, log, progress, resume, and completion behavior. |
| [No Grass In Objects official Nexus page](https://www.nexusmods.com/skyrimspecialedition/mods/42161) | Page reported `1.6.8` updated 2026-07-05 | Author-maintained distribution page. Establishes the current user-facing cache-generation workflow; the source revision above is intentionally not represented as that release. |
| [Wrye Bash repository](https://github.com/wrye-bash/wrye-bash/tree/ee093ed42ec7f3c406151499cbefcc4c707b2ed5), [general readme](https://wrye-bash.github.io/docs/Wrye%20Bash%20General%20Readme.html), and [advanced readme](https://wrye-bash.github.io/docs/Wrye%20Bash%20Advanced%20Readme.html) | `v314`, commit `ee093ed42ec7f3c406151499cbefcc4c707b2ed5` | Official source/docs for the contrasting Bashed Patch example. Establishes saved/exportable patch configuration, generated plugin, and `Data\Docs` reports, but not a complete content-addressed run manifest. |
| [RESEARCH-0012](RESEARCH-0012-snapshot-fingerprint-and-invalidation.md) and [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md) | Accepted Wave B result | Controls how Infinium may establish snapshot and dependency validity; generator timestamps cannot replace it. |

Official source and author-maintained documentation establish what artifacts
exist. Conclusions about their sufficiency for Infinium are interpretations
against the accepted requirements.

## 4. Experiments and artifact manifest

### 4.1 Environment and reproducible steps

The experiment used Windows NT `10.0.26200.0`, PowerShell `7.6.3`, and Git
`2.55.0.windows.2`. No generator executable was run. Public repositories were
cloned shallowly into an OS temporary directory outside the repository:

```powershell
git clone --depth 1 --branch v4.3.1-beta `
  https://github.com/Monitor221hz/Pandora-Behaviour-Engine-Plus.git <temp>\pandora
git clone --depth 1 --branch 0.36.5 `
  https://github.com/Mutagen-Modding/Synthesis.git <temp>\synthesis
git clone --depth 1 --branch v5.8.2 `
  https://github.com/ousnius/BodySlide-and-Outfit-Studio.git <temp>\bodyslide
git clone --depth 1 --branch v0.84-beta `
  https://github.com/ShikyoKira/Project-New-Reign---Nemesis-Main.git <temp>\nemesis
git clone --depth 1 `
  https://github.com/DwemerEngineer/No-Grass-In-Objects-NG.git <temp>\ngio
git clone --depth 1 --branch v314 `
  https://github.com/wrye-bash/wrye-bash.git <temp>\wrye-bash
```

Targeted `rg` and `Get-Content` inspection traced configuration serialization,
run-state persistence, output-path construction, success/progress handling,
and cleanup behavior. `git rev-parse HEAD` and `git describe --tags --always`
verified the revisions in section 3.

### 4.2 Artifact manifest

| Probe | Retained artifact | Side effects and sensitivity |
|---|---|---|
| Tagged source inspection | Commit IDs and summarized observations in this report | Temporary public source clones only; no user data or protected setup access |
| Moving NGIO source inspection | Exact commit ID and summarized observations | Same; commit is not claimed to equal Nexus `1.6.8` |
| Official web-document inspection | URLs, retrieval date, and summarized claims | Ordinary unauthenticated reads |
| Real-profile/tool execution | None | Not performed; no MO2, game, generator output, config, cache, or log was touched |

The experiment is safe under AUTH-001 through AUTH-003. Ordinary invocation of
these generators is mutating and was not qualified as a production operation.
A hypothetical isolated run into product-owned disposable space would still
need separate tool-by-tool side-effect research and an accepted authority
decision; this report does not assume that it is permitted merely because the
destination is temporary.

## 5. Findings

### 5.1 Cross-tool answer

**Verified fact:** every surveyed family documents or implements at least one
characteristic artifact or output convention: paths/extensions, configuration
files, progress state, reports, or logs.

**Interpretation:** those surfaces are sufficient for bounded generic
detection when combined with effective-provider reconstruction, although not
necessarily for unique tool attribution.

**Verified fact:** none of the surveyed tool/version lines was found to persist
a complete, versioned manifest containing exact tool identity,
successful-run identity, effective configuration, complete relevant input
fingerprints, complete output fingerprints, and dependency mapping.

**Interpretation:** a generated file's existence, provider, modification time,
or even a successful log cannot by itself prove freshness. Infinium can make
strong deterministic conclusions only where:

1. a version-qualified adapter understands the observed artifacts;
2. the exact artifact set is bound to the installation snapshot;
3. the adapter declares the inputs/settings on which its conclusion depends;
4. ADR-0010 fingerprints that closure; and
5. the asserted conclusion is no stronger than the retained evidence.

The useful distinction is therefore not “manifest tool” versus “no-manifest
tool.” It is a per-capability evidence matrix: attribution, completion,
configuration, input identity, output identity, dependency scope, and schema
stability.

### 5.2 Surface summary

| Family | Strongest retained surfaces | What can be concluded safely | Central gap | Preliminary feasibility |
|---|---|---|---|---|
| Synthesis | `PipelineSettings.json`; ordered profiles/groups/patchers; Git patcher `LastSuccessfulRun`; output plugin(s); patcher settings | Pipeline intent and, for a Git patcher, last successful code/version identity; output plugin structure and provider | No complete input/output fingerprint manifest; patcher-owned settings and non-Git patchers vary | High for a version-pinned partial-provenance adapter |
| Pandora | `ActiveMods.json`; `PreviousOutput.txt`; `Engine.log`; HKX and animation-data output | Selected patch codes/order, prior successful packfile paths, diagnostics, output presence/structure | Artifacts are mutable state/cleanup metadata, not a complete successful-run/input/output manifest | Medium-high for a version-pinned adapter |
| DynDOLOD/TexGen | Preset INI; main/debug/LODGen logs; exports/maps when retained; plugins, meshes, textures, JSON/TXT runtime data; CRC messages | Rich run/config/load-order diagnostics and internal output-coherence checks; some explicit mismatch signals | Logs are mutable/optional and output formats are alpha-sensitive; no complete durable manifest | Medium-high, but version pinning and retained sidecars are prerequisites |
| BodySlide | SliderSets OSP/XML; SliderPresets XML; group/config/automation XML; NIF/TRI output paths | Declared potential outputs, selected/current preset state, structural/provider conflicts, missing expected counterparts | Typical interactive build selection and exact effective values are not durably tied to actual output/run | Medium for declaration/structure; low for freshness without stronger retained evidence |
| Wrye Bash | Saved/exportable Bashed Patch config; generated plugin; HTML/TXT report; Wrye Bash metadata | Patch identity/config/report and plugin semantics when all are retained | Config/report are not a content-addressed input/output manifest; tool-owned metadata location complicates MO2 attribution | Medium as a contrasting patch generator |
| NGIO grass cache | `Data\Grass\*.cgid`; `.fail`; `PrecacheGrass.txt`; config; SKSE log | Cache presence by worldspace/cell, in-progress/resume state, skipped failures, configuration, successful-completion log | Success deletes the progress file; no retained input/version/output manifest; expected cell closure is nontrivial | Medium for structure/completeness, low for freshness |
| Nemesis | Patch definitions/workflow, logs/caches, HKX and animation-data output | Characteristic output presence and bounded diagnostics | No qualified durable run manifest; may modify existing animation-data file; ownership can be ambiguous | Low-medium pending controlled fixtures |
| FNIS | Redirect setting, last patch selection, generator log/temp state, HKX and animation-data output | Characteristic output and bounded legacy diagnostics | Tool-local state may be separated from installed output; no complete run/input/output manifest | Low-medium; generic fallback is initially safer |
| xLODGen | Terrain/object LOD meshes/textures and logs | Generic inspection of effective LOD files remains possible without xLODGen attribution | Official documentation defines it as renamed xEdit in `-lodgen` mode, so ADR-0007 excludes every named integration, adapter, detection, output-parsing, and capability role | Excluded; no named roadmap role |

“Preliminary feasibility” is a research recommendation, not an accepted
analyzer selection or release promise.

### 5.3 Synthesis

**Verified facts**

- `PipelineSettings.json` has a versioned pipeline schema and persists profiles,
  game release, groups, patcher ordering, patcher types, and version-targeting
  choices.
- A Git patcher can target a tag, branch, or exact commit.
- `LastSuccessfulRun` retains target repository, project subpath, commit,
  Mutagen version, and Synthesis version.
- Individual patchers may retain their own settings, while solution, external,
  CLI, and Git patcher paths do not have identical provenance surfaces.

**Interpretation**

Synthesis offers the strongest surveyed persisted run identity, especially
when `LastSuccessfulRun` records an exact Git commit. It still does not prove
that the current pipeline configuration equals the one used for the output,
that every patcher-specific setting is unchanged, or that the selected MO2
input snapshot/load order is unchanged. A branch or “latest tag” setting is
intent, not immutable identity.

A future adapter could:

- parse and version-gate the pipeline schema;
- distinguish current targeting from last-successful targeting;
- identify all patcher-specific settings that the selected pipeline declares;
- inspect the output plugin(s) through the accepted Mutagen boundary;
- bind relevant input plugin/load-order/config fingerprints to ADR-0010; and
- report unsupported patcher types or settings as gaps.

It must not claim freshness from `LastSuccessfulRun` alone.

### 5.4 Pandora, Nemesis, and FNIS

#### Pandora

**Verified facts**

- `ActiveMods.json` serializes patch code, active flag, and priority and is used
  by automatic-run behavior.
- `PreviousOutput.txt` contains successful packfile output paths. On a later
  run, Pandora reads those paths and deletes existing files before writing new
  metadata.
- `Engine.log` is a diagnostic surface.
- Output can be redirected, which is compatible with the common MO2 output-mod
  workflow.

**Interpretation**

`ActiveMods.json` is selected-module state. `PreviousOutput.txt` is a
cleanup-oriented partial output inventory. Neither records the physical input
assets, their providers/hashes, exact tool version, full output hashes, or a
durable run ID. Their combination can strengthen attribution and expected
output checks but cannot independently prove freshness.

#### Nemesis

**Verified facts**

- The official release remains `v0.84-beta`.
- It produces behavior HKX and animation data/set data and supports selected
  patches.
- Its documented/source behavior may modify an existing
  `animationdatasinglefile.txt` rather than always producing a wholly owned
  replacement, and its documentation places file management outside Nemesis.

**Interpretation**

Infinium must reconstruct the winning MO2 provider and cannot assume all
characteristic files belong to one generator output mod. Until disposable
fixtures establish stable state and log contracts for exact versions, Nemesis
should receive bounded structural observations rather than a freshness claim.

#### FNIS

**Verified facts**

- FNIS `7.6` documents redirected output, last-selected-patch reuse, tool-local
  temporary/log files, and generated behavior/animation-data paths.
- Tool-local state can remain under its generator directory while installed
  output is redirected elsewhere.

**Interpretation**

Separating run evidence from output weakens attribution after users move
Overwrite contents into an MO2 mod. A future adapter would need both sides and
must tolerate missing logs/state. Legacy status and lack of a complete
manifest make generic detection plus explicit gaps the safer initial contract.

Pandora compatibility with Nemesis/FNIS patch conventions does not make their
output provenance interchangeable. Tool attribution must remain evidential.

### 5.5 BodySlide

**Verified facts**

- Slider-set OSP/XML files declare source/project data and output path/file.
- Slider-preset and group XML plus `BodySlide.xml` expose presets, groups,
  current selection, output data path, and `BuildMorphs`.
- Automation XML can define explicit batch slider-set selection and output
  paths.
- A build produces mesh NIFs and, when applicable, TRI morph files.

**Interpretation**

These are valuable declarative formats. They let Infinium map installed
projects to potential outputs and detect missing companions, conflicting
providers, or an output that is not among declared paths. They do not prove
which outfits the user selected in a typical interactive batch build or which
effective preset values produced each output. `SelectedPreset` is current UI
state and may change after generation.

A safe initial analyzer can say:

- “this installed mesh path is declared by these slider sets”;
- “the effective provider is X and these alternatives are overwritten”;
- “the expected TRI companion is absent/present”; or
- “the build provenance/currentness is unproven.”

It cannot say “this mesh is stale because the preset file is newer.” A
controlled fixture is needed to determine whether output content can be
normalized or compared reproducibly across identical builds before any
content-derived regeneration proof is claimed.

### 5.6 DynDOLOD, TexGen, and xLODGen

**Verified facts**

- TexGen persists a current default preset INI.
- DynDOLOD/TexGen write main logs on close, overwrite a debug log for the last
  session, and can produce real-time logs.
- The debug material records selected settings and extensive mod/plugin/load
  order information.
- LODGen/xLODGen logs and intermediate exports/maps may remain outside the
  installed output.
- DynDOLOD emits CRC-based diagnostics for some billboards, models, and
  textures and requires generated plugins and JSON/TXT runtime data to be from
  a coherent generation.
- DynDOLOD 3 remains an alpha line whose formats/options may change.
- Official xLODGen documentation defines xLODGen as renamed xEdit that starts
  the xEdit `-lodgen` tool mode.

**Interpretation**

This ecosystem has the richest diagnostic evidence after Synthesis, and some
CRC diagnostics are stronger than timestamp heuristics. But the evidence is
distributed across mutable presets, logs, temporary/intermediate files, and
installed output. Users commonly retain only the installed output mod.

A future version-pinned adapter could check:

- plugin/runtime-data pairing and required output components;
- tool-reported CRC mismatches;
- log-indicated generation failures;
- declared worldspace outputs and missing components;
- retained preset/log identity against snapshot-bound load order and relevant
  assets; and
- mixed providers or outputs plausibly assembled from different runs.

Missing logs or presets must reduce confidence or produce a gap, not imply
failure.

**Decision reconciliation:** the accepted analysis catalog previously named
xLODGen, while ADR-0007 excludes xEdit from every product, development,
dependency, integration, and evaluation role. Official documentation defines
xLODGen as renamed xEdit running its `-lodgen` mode. The catalog has therefore
been corrected to exclude a named xLODGen integration, adapter, detector,
output parser, or capability. Generic inspection of effective terrain/object
LOD files remains permitted when it does not attribute them to xLODGen or
depend on xEdit-specific artifacts or behavior.

### 5.7 Grass cache

**Verified facts at the inspected NGIO-NG revision**

- Cache files use `Data\Grass\<worldspace>x<cell-x>y<cell-y>.cgid`.
- `.fail` files count failed attempts and can cause a cell to be skipped after
  the configured maximum.
- `PrecacheGrass.txt` is both a trigger and a line-oriented progress/resume
  journal containing completed keys.
- On successful completion, the progress file is deleted.
- The SKSE log records plugin version, runtime, configuration activity,
  progress, failures, and successful completion.
- The configuration itself warns that mod-setup changes can require cache
  regeneration to avoid floating or misplaced grass.

**Interpretation**

The progress file is primarily evidence of an incomplete or interrupted run,
not a durable success manifest. A cache directory can be inventoried by
worldspace/cell and checked for `.fail` residue, but determining the expected
complete cell set requires a version-qualified reconstruction from records,
worldspaces, skip/only filters, configuration, and generator behavior.

Even a complete cell set does not prove freshness. Relevant dependencies can
include landscape/cell/grass records, effective meshes/textures/collision,
NGIO and runtime versions, raycast/object filters, and configuration. The exact
closure requires separate fixture qualification; “any mod changed” is safe
generator advice but too broad for fine-grained Infinium invalidation.

### 5.8 Wrye Bash as a contrasting generated-plugin example

**Verified facts**

- A Bashed Patch is a generated plugin identified by its author field.
- Wrye Bash retains patch configuration in its own metadata and can
  export/import or list that configuration.
- `Data\Docs` may contain Bashed Patch HTML/TXT reports.
- Official guidance says to rebuild after load-order changes.

**Interpretation**

This differs from Synthesis because generator-specific state lives partly in
Wrye Bash metadata and reports rather than a plainly colocated pipeline file.
Those artifacts can explain what patchers/settings participated, but they do
not record a complete content-addressed input/output manifest. The same
adapter pattern still applies: parse qualified state, inspect the generated
plugin, reconstruct and fingerprint declared dependencies, and remain
indeterminate when the metadata/report is missing.

## 6. What can be inferred at each evidence level

The following are proposed evidence levels for evaluation design, not accepted
product enums:

| Available evidence | Allowed conclusion | Prohibited shortcut |
|---|---|---|
| Characteristic output only | Presence, effective provider, structure, conflicts, and possible generator attribution | Fresh, complete, or generated successfully |
| Output plus current config/declarations | Expected/potential paths and structural mismatch against current declarations | Current config was used for this output |
| Output plus completion log | A reported run completed, if log/output association is established | Inputs or settings are unchanged |
| Output plus selected-module/run identity | Stronger attribution and code/version identity | Complete input/output closure |
| Output plus complete declared dependency closure fingerprinted under ADR-0010 | Current or changed relative to that exact captured closure, within adapter contract | Validity outside the declared closure |
| Unknown tool/version, malformed/missing sidecar, or mixed-run evidence | Bounded observations, competing attribution hypotheses, and explicit gaps | Best-effort semantic freshness verdict |

Modification time may help prioritize an investigation but is never proof that
generated output predates or reflects a relevant dependency.

## 7. Alternatives evaluated

| Alternative | Strengths | Failure modes | Assessment |
|---|---|---|---|
| Generic output-only inventory | Broad, cheap, offline, provider-aware, works for unsupported tools | Cannot prove run completion, configuration, dependencies, or freshness | Required fallback, insufficient as named generated-output analysis |
| Version-pinned tool-specific artifact adapters | Uses available config/log/report/output semantics; preserves inspectability | Per-version maintenance; sidecars may be absent; no tool has a complete manifest | Recommended roadmap pattern |
| Infer freshness from timestamps | Cheap and easy to explain | Copy/extract operations, clock granularity, moved output mods, reproducible builds, and unrelated changes create false conclusions | Reject as finding evidence |
| Re-run the generator and compare output | Could supply direct behavior for a controlled fixture | Ordinary runs are mutating; output may be nondeterministic, costly, version-sensitive, and accompanied by tool/config/cache writes | Reject for the current production roadmap; permit only disposable research/evaluation fixtures unless a later isolated operation is separately qualified and accepted |
| Require users to retain every tool log/config directory | Improves provenance when followed | Existing lists lack it; locations are mutable; burdens users; still no full input/output manifest | Optional evidence enhancement, not a correctness precondition |
| Infinium-owned prospective sidecar around future runs | Could capture exact snapshot, config, tool identity, output hashes, and success | Requires an authorized write/invocation workflow outside current read-only product; cannot recover historical runs | Defer beyond M4 and require a new ADR/authority decision |
| Generator-authored standardized manifest | Best long-term interoperability | Not presently available across surveyed tools and outside Infinium's control | Encourage/contribute upstream later; not a roadmap dependency |

## 8. Contrary evidence, uncertainty, and unsupported cases

1. This was a source/document survey, not execution conformance. Stable-looking
   files may have edge cases that only disposable runs expose.
2. Exact output byte reproducibility was not tested. HKX, NIF/TRI, plugin, LOD,
   and CGID comparisons may contain nondeterministic or environment-sensitive
   data.
3. DynDOLOD documentation describes a moving alpha. Every named adapter needs a
   pinned compatibility matrix and fixtures; the current site is not a durable
   schema specification.
4. The inspected NGIO-NG commit is current source, not proven identical to the
   Nexus `1.6.8` binary. Binary/source qualification remains necessary.
5. Nemesis and FNIS have old or limited maintained source contracts. Community
   conventions were not promoted to authoritative format guarantees.
6. Missing sidecars can result from normal user cleanup/moving output, not a
   failed generation.
7. Logs may contain local paths, mod names, and environment details. They are
   private local evidence subject to the product's retention/export rules.
8. Multiple generators can touch similar animation, plugin, mesh, or LOD
   surfaces. Characteristic paths alone may yield competing attribution.
9. A generator can report success while producing semantically bad output.
   Generator provenance and output correctness are separate analyzer claims.
10. Dependency scope is tool and feature specific. This survey does not supply
    the definitive generated-output taxonomy or every invalidating input.
11. No conclusion here makes the private reference profile an oracle, fixture,
    scale target, or representative modlist.

### Rejection criteria for a named adapter

A candidate should remain generic/unsupported when any of these holds:

- the observed version is outside an explicit compatibility allowlist;
- required schema semantics are undocumented and unqualified by fixtures;
- output attribution is ambiguous among providers or generators;
- a purported completion marker is mutable, absent, or not bindable to output;
- dependency closure cannot be stated narrowly enough to make the verdict
  honest;
- parsing failure would otherwise be converted into “fresh” or “safe”; or
- the integration conflicts with an accepted ADR.

## 9. Recommendation

### Recommended answer

Adopt a **tiered, version-pinned, read-only artifact-adapter roadmap**:

1. always provide a generic generated-output inventory with effective provider,
   structure, conflict, and coverage observations;
2. qualify named adapters only for exact tool/schema versions and only for
   claims directly supported by retained configuration, run state, logs,
   output, and declared dependency closure;
3. use ADR-0010 fingerprints to establish currentness of that closure;
4. distinguish attribution, completion, configuration match, output
   completeness, and dependency freshness rather than collapsing them into one
   “valid” flag; and
5. fail to explicit `unknown`, `unsupported`, `mixed`, or `unproven` outcomes
   whenever required evidence is missing.

For roadmap investigation order, Synthesis, Pandora, and DynDOLOD/TexGen offer
the richest distinct surfaces. BodySlide and grass-cache fixtures should
follow because they test declarative-output mapping and distributed
worldspace/cell completeness. Nemesis/FNIS should begin with generic
observation until version-specific fixtures justify more. Wrye Bash is a useful
contrasting generated-plugin fixture, not necessarily an initial named
product module.

Do not select xLODGen for any named role; ADR-0007 excludes it as an xEdit
mode.

Confidence: **moderate** for the cross-tool conclusion and roadmap ordering;
**low to moderate** for exact adapter feasibility until disposable binary
fixtures validate each claimed format and behavior.

### Preconditions before any named analyzer is accepted

- exact tool/version or binary identity and schema allowlist;
- synthetic positive, negative, malformed, missing-sidecar, mixed-run, and
  upgrade fixtures;
- documented read-only file-access and privacy contract;
- explicit dependency-closure specification;
- performance measurement at relevant output scale;
- a finding/coverage contract that distinguishes unsupported from clean; and
- an accepted delivery plan or ADR if the adapter changes architecture,
  dependencies, authority, or product scope.

## 10. Follow-up artifacts enabled

### 10.1 Accepted RQ-020 disposition

Current status:

> Researched for the M0 roadmap: no surveyed generator exposes a complete
> freshness manifest; use generic observation plus version-pinned partial
> artifact adapters. Named analyzer selection and conformance remain delivery
> plan work. xLODGen is excluded under ADR-0007 because it is an xEdit mode.

M0 accepted roadmap/taxonomy evidence rather than a named generator
qualification. Named selection, dependency review, and conformance remain
later delivery work.

### 10.2 EVAL-0058 refinement

Split the current case into a matrix:

1. supported version with all required sidecars and unchanged dependency
   closure;
2. one relevant input changed with unchanged output;
3. unrelated input changed outside the declared closure;
4. current config differs from run-bound config;
5. missing or malformed config/log/manifest;
6. failed/interrupted generation and residual partial output;
7. mixed outputs from two runs/providers;
8. output moved into a separate MO2 mod;
9. unsupported tool version with recognizable files;
10. unknown generator with generic files; and
11. timestamp inversion without relevant content change.

Expected results must independently specify whether each case yields a finding,
hypothesis, bounded observation, or coverage gap. No expected result may be
derived solely from the adapter under test.

### 10.3 RQ-036 taxonomy input

Provide the following as technical-surface evidence only:

- generated plugin;
- generated behavior/animation data;
- generated mesh/morph;
- generated texture/terrain/object/grass LOD;
- generated grass cell cache;
- generator configuration/selection state;
- generator log/report/progress state; and
- generator runtime sidecar/intermediate output.

These are state/artifact surfaces, not mod-purpose or affected-game-area
categories. The accepted taxonomy maps them independently to purpose, affected
area, consequence, and effect extent while symptoms, severity, and evidence
remain separate.

### 10.4 Registry and decision disposition

- Exact tool sources remain registered by this report's versioned source
  table; moving alpha/default-branch sources are not version contracts.
- Retain the product-catalog clarification that xLODGen is excluded by
  ADR-0007; generic effective-file analysis must not become implicit xLODGen
  detection, attribution, or output parsing.
- Do not create an architecture ADR merely to accept this survey. Create one
  only when a milestone selects named adapters, third-party dependencies, a
  prospective manifest wrapper, or a changed authority boundary.
- Add future per-tool qualification investigations rather than treating this
  cross-tool survey as conformance.

## 11. Requirements-and-evidence traceability

| Requirement/decision | Evidence produced | Result or follow-up |
|---|---|---|
| SCOPE-005 | All surveyed families expose at least generic output/provider surfaces; semantic depth varies | Generic inventory is baseline; unsupported semantics remain gaps |
| AUTH-001 to AUTH-003; ADR-0003 | Source/docs were inspected without executing generators; ordinary reruns are mutating and no isolated operation was qualified | Named analyzers must be post-hoc read-only through M4 |
| ADR-0008 | Output and sidecars can be split across tool directories, Overwrite, and output mods | Attribution must use exact selected-profile effective providers |
| ADR-0009 | Synthesis/Wrye Bash/DynDOLOD may generate Bethesda plugins | Plugin inspection remains within the qualified Mutagen allowlist |
| SNAP-001 to SNAP-006; ADR-0010 | No surveyed tool supplies complete dependency fingerprints; currentness requires snapshot-bound closure | Adapter declares dependencies; ADR-0010 fingerprints them |
| EVID-001 to EVID-006 | Artifacts support different claim strengths and may conflict or be absent | Preserve typed observations, claims, hypotheses, findings, and gaps |
| COVER-001 to COVER-003 | Unknown versions and missing sidecars are normal supported outcomes | Explicit unsupported/indeterminate coverage is mandatory |
| ANALYSIS-005 | Generated output intersects plugins, records, assets, config, and runtime sidecars | Cross-layer joins remain separate from tool attribution |
| ANALYSIS-008 | Synthesis/Pandora/logs expose partial version identity; others may not | Version-gate adapters and report unproven identities |
| ANALYSIS-010 | Surface matrix and roadmap answer which manifests/formats are usable | Version-pinned partial adapters plus generic fallback recommended |
| ANALYSIS-015 | Generator instructions often require regeneration after relevant changes | Advice requires established applicability and dependency evidence |
| ANALYSIS-016 | Seven-dimensional rubric and rejection criteria define bounded contracts | Each adapter must publish supported versions, inputs, outputs, failures, and gaps |
| ANALYSIS-017 | Cheap structural evidence can select higher-cost investigation candidates | Do not send whole output trees to an LLM or let prose invent freshness |
| OPS-004 | Generic inventory and dependency-scoped hashing can be incremental | Per-tool performance qualification remains open |
| ADR-0007 | Official documentation identifies xLODGen as an xEdit mode | Named xLODGen support is excluded; the catalog is reconciled |
| EVAL-0058 | Eleven-case conformance matrix proposed | Replace the single broad stale/mismatch case during evaluation planning |
| RQ-036 | Technical artifact surfaces listed without game-area categorization | Taxonomy research consumes but does not inherit tool-name categories |

## 12. Conclusion

Common Skyrim generators leave useful evidence, but they do not provide the
complete manifests needed to declare output fresh by inspection alone.
Synthesis exposes the strongest persisted pipeline and last-successful code
identity; Pandora exposes useful selected-module and cleanup/output metadata;
DynDOLOD/TexGen exposes rich diagnostics and coherence checks; BodySlide,
grass-cache, Wrye Bash, Nemesis, and FNIS provide progressively more partial or
distributed state.

Infinium should therefore treat generated-output analysis as provenance-aware
adjudication over several partial artifacts, not as a timestamp comparison and
not as automatic generator execution. Exact named support remains a
version-qualified delivery decision. Generic observations and explicit gaps
remain valid, useful results for every other case.
