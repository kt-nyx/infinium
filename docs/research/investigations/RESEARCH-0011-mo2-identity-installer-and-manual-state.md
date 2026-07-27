# RESEARCH-0011: MO2 identity, installer, and manual-change state

Status: Completed — recommendation accepted by ADR-0008

Date: 2026-07-25

Last reviewed: 2026-07-26

Researcher: Codex agent

Primary RQ: RQ-007

M0 wave: B — Local-state and deterministic-tool capability

Decision enabled: A version-pinned MO2 metadata adapter, source-identity
mapping contract, and explicit coverage semantics for installer choices,
hidden content, and manual changes

Accepted disposition:
[ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md)
accepts the physical local-entity/source-mapping distinction, many-to-many
mapping semantics, raw metadata preservation, and explicit installer-history
and manual-change gaps.

## Executive conclusion

MO2 2.5.2 retains useful but mutable source-identity hints in each regular
mod's `meta.ini`: game, repository, Nexus mod ID, declared installed version,
installation archive name, cached Nexus fields, notes, categories, and an
`installedFiles` array of mod/file ID pairs. Download sidecars retain a second
set of archive-level hints. Neither file is an immutable provenance record.
Users and installers can change the values, replacement can preserve older
metadata, manual/copied/generated mods can have absent or zero identity, and
multiple installed mod folders can legitimately or accidentally share one
Nexus identity.

The exact current mod folder, raw metadata bytes, current source-archive bytes,
and physical `.mohidden` paths can be observed directly. The meaning assigned
to those observations remains version-modelled MO2 behavior. In contrast, MO2
2.5.2 does not retain a general FOMOD choice history, a complete
merge/replace/reinstall history, install-time FOMOD condition inputs, a
per-file installed baseline, deletion tombstones, or source-archive hashes.
The normal C++ and C# FOMOD installers materialize the selected output tree but
do not persist the user's selections. BAIN is a plugin-specific exception: it
stores selected subpackage names in opaque plugin settings.

Infinium must therefore identify a current installed mod by its MO2
instance/profile snapshot and physical mod-folder identity, not by Nexus ID.
Source identity is a separate, versioned analysis-context mapping that can be
zero-to-many in both directions. Present-state FOMOD analysis may report
compatible-choice sets when the exact archive, configuration, relevant
conditions, and current output are available. It must never call such a set
the historical selection. Ambiguity and unavailable history are coverage
results, not prompts to invent an identity or option choice.

This result supplies RQ-007's contribution to Gate B if M1 requires exact
current physical and effective state plus honest source-identity coverage.
The full gate still depends on the other Wave B work. It must not require exact
installer history that MO2 never recorded.

## 1. Question and requirements

### Primary question

What metadata does MO2 retain about Nexus identity, source archives, FOMOD
choices, hidden files, and manual changes?

### Derived questions

1. Which retained fields are current observations, mutable claims, inferred
   defaults, or absent history?
2. Can a Nexus mod ID or source archive identify an installed mod uniquely?
3. What happens to metadata across replace, merge, reinstall, copy, rename,
   split, generated-output, and manual-install workflows?
4. Can FOMOD selections be reconstructed exactly or only constrained by the
   present installed output?
5. What can `.mohidden` prove?
6. Which gaps require abstention, user adjudication, or later source
   validation?

### Accepted constraints applied

| Requirement/decision | Consequence for this investigation |
|---|---|
| `SCOPE-002`, `SCOPE-003`, `SCOPE-005` | The initial target is one selected MO2 profile and its effective installation, not a generic manager abstraction. |
| `SNAP-001` through `SNAP-006` | Physical state, semantic context, retained inputs, invalidation, and replay disclosures must remain distinct. |
| `DOC-003` | Source identity is a correctable, versioned analysis-context mapping; it is not silently equated with a local folder. |
| `ANALYSIS-012` | Installer selections may be inferred only where evidence permits, and ambiguity must remain visible. |
| `EVID-001` through `EVID-006` | Observations, claims, interpretations, hypotheses, and gaps must retain their types and provenance. |
| `ADR-0001` | MO2 metadata can be evidence without becoming infallible authority for source intent or current file content. |
| `ADR-0002` | Identity corrections change analysis context; they do not rewrite the captured physical snapshot. |
| `ADR-0003` | Inspection must remain read-only with respect to MO2, the selected profile, game files, downloads, and mod folders. |
| `ADR-0004` | Results apply only to the pinned initial target and must fail closed for unsupported manager/runtime behavior. |

The user-provided `Brain Blast Destruction 2024` profile was used only as a
private example of the shape and scale of an actually used MO2 profile. It is
not representative of all modlists, a correctness oracle, or a gold-standard
fixture. No raw mod names or private profile contents are reproduced here.
Synthetic atomic fixtures and small controlled profiles with real mods remain
the required evaluation direction.

## 2. Scope and non-scope

### In scope

- MO2 2.5.2 regular-mod `meta.ini` persistence and mutation behavior.
- Download archive `.meta` sidecars.
- The relationship among physical mod folders, repository/game/mod/file IDs,
  installed archive names, and current download archives.
- Replace, merge, reinstall, empty/manual, copied, renamed, split, and
  generated-mod ambiguity visible in the inspected source.
- Normal C++ and C# FOMOD output construction and persistence behavior.
- The BAIN installer's plugin-specific retained option state.
- `.mohidden`, skip-suffix, foreign/unmanaged, overwrite, separator, and backup
  semantics relevant to identity and coverage.
- A read-only aggregate inspection of the private reference environment.
- Product contracts, evaluation cases, and research/ADR follow-ups enabled by
  the findings.

### Non-scope

- Launching MO2, Skyrim, LOOT, an installer, or any modding tool.
- Changing a profile, mod directory, download, archive, metadata file, or
  application setting.
- Deleting the obsolete `test profile`; the Wave B manifest records that the
  coordinator completed that owner-authorized preflight action while MO2 was
  closed. This investigation did not inspect or modify that profile.
- Declaring the private reference profile correct, healthy, representative, or
  reusable as a public fixture.
- Validating Nexus identity against the live Nexus API; that belongs to
  RQ-008 and the accepted Nexus policy boundary.
- Reconstructing arbitrary installer history by executing untrusted installer
  code.
- Defining game-area, impact, record-family, asset-family, behavior, or
  symptom taxonomies governed by RQ-036.
- Selecting implementation libraries, persistence technology, or UI.
- Applying proposed changes to the RQ registry, domain model, ADRs, evaluation
  catalog, milestone plan, or investigation index.

## 3. Sources and exact versions

### Local reference environment

| Item | Version/fingerprint | Use |
|---|---|---|
| Installed MO2 | 2.5.2; executable SHA-256 `442B354A8F34754DA0048654C44D27F51628FEBA54CE46C3187CF58D6C43E622` | Pinned behavior target |
| MO2 core source | Commit [`9c130cbf2fc7225fb2916e46419af50671772aa0`](https://github.com/ModOrganizer2/modorganizer/tree/9c130cbf2fc7225fb2916e46419af50671772aa0) | Direct implementation evidence for the installed release |
| MO2 build orchestrator | Tag [`v2.5.2`](https://github.com/ModOrganizer2/mob/tree/v2.5.2), commit `b3c66b3221e3d865807921b9571a5012d9a6f732` | Confirms the installer plugin set built for MO2 2.5.2 |
| Installed C++ FOMOD plugin | SHA-256 `AA47047B9BA16A8F2F8844F297AF449A0ABA20DF790631FF4588A57AAB2E967D` | Pins the inspected local binary |
| Installed C# FOMOD plugin | SHA-256 `338F5779BE28D62208CAE4B40051EEC1C28D362D92A28D5B502D9F79CCDBACD7` | Pins the inspected local binary |
| Installed BAIN plugin | SHA-256 `A798807C3BEC073D4874E9E0B4E18868B1F1CF19B8DC44F2FAD1790517472AB0` | Pins the inspected local binary |
| Private profile | `Brain Blast Destruction 2024`; profile file hashes recorded in the completed shared Wave B manifest | Shape/scale cross-check only |

The build-orchestrator tag names the FOMOD, C# FOMOD, BAIN, manual, quick, and
bundle installer projects in the 2.5.2 build, but it does not pin their Git
commits in the repository. The report therefore does **not** claim an
unverified source-to-binary identity for those plugin binaries. Plugin source
was inspected at the nearest pre-release/release-window commits below as
implementation evidence only. The local hashes pin the observed binaries; an
accepted version-specific adapter would still need a verified association:

| Project | Inspected commit | Relevant evidence |
|---|---|---|
| C++ FOMOD installer | [`add901f12d49dc5fdfa7ac811099cdfbee416bd3`](https://github.com/ModOrganizer2/modorganizer-installer_fomod/tree/add901f12d49dc5fdfa7ac811099cdfbee416bd3) | Selected tree is constructed and returned; no general choice-persistence call exists |
| C# FOMOD installer | [`613eea0eae10edb6aba654781165d0b7e27e8e3f`](https://github.com/ModOrganizer2/modorganizer-installer_fomod_csharp/tree/613eea0eae10edb6aba654781165d0b7e27e8e3f) | Managed installer produces the selected install result; no general choice history is persisted |
| BAIN installer | [`e74d313cc70b00da3479c1dbcbc9266c8aadfc59`](https://github.com/ModOrganizer2/modorganizer-installer_bain/tree/e74d313cc70b00da3479c1dbcbc9266c8aadfc59) | Reads and writes plugin settings named `optionN` for selected subpackages |
| Manual installer | [`467dc2725ef22a9c7b55004ac234b46e563e5622`](https://github.com/ModOrganizer2/modorganizer-installer_manual/tree/467dc2725ef22a9c7b55004ac234b46e563e5622) | Produces a selected output tree without a durable manual-edit ledger |
| Quick installer | [`77bd4d031f8f006ca71adee7ce673dce80f1c8cf`](https://github.com/ModOrganizer2/modorganizer-installer_quick/tree/77bd4d031f8f006ca71adee7ce673dce80f1c8cf) | Simple output materialization, not provenance history |

### Primary source locations

- [`modinforegular.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/modinforegular.cpp)
  defines regular-mod metadata reads, writes, defaults, and opaque plugin
  settings.
- [`installationmanager.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/installationmanager.cpp)
  defines archive-sidecar input, name/ID guessing, replace/merge installation,
  and metadata writes.
- [`downloadmanager.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/downloadmanager.cpp)
  defines download-sidecar persistence and transient MD5 lookup.
- [`modinfo.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/modinfo.cpp)
  defines folder classification, identity indexing, `.mohidden`, and special
  mod types.
- [`modlistviewactions.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/modlistviewactions.cpp)
  defines hide/unhide rename behavior.
- [`organizercore.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/organizercore.cpp)
  connects install completion with installed mod/file identity updates.

### Version-drift source

Current upstream MO2 commit
[`efe2a02d5dc641946baaa8db1440800f38d07837`](https://github.com/ModOrganizer2/modorganizer/tree/efe2a02d5dc641946baaa8db1440800f38d07837)
changes relevant install metadata behavior, including merge-result handling and
additional author/uploader fields. That comparison is evidence that the
adapter must be version-pinned, not permission to apply current behavior to
2.5.2.

## 4. Experiments and artifacts

### Preflight

- Read the accepted product baseline, architecture/trust documents, accepted
  ADRs, M0 plan, Wave B research registry/material, and relevant evaluation
  documents in the required order.
- Confirmed MO2 was not running before inspection.
- Confirmed the installed MO2 executable version and hash matched the Wave B
  manifest.
- Confirmed the selected private reference profile and all six recorded
  profile-file hashes matched the manifest before inspection.
- Confirmed the manifest records the obsolete `test profile` removal and the
  selected-profile change to `Brain Blast Destruction 2024`; this
  investigation made neither setup change.
- Used only direct filesystem reads, source inspection, hashing, and aggregate
  counts.
- Did not launch a tool, invoke an installer, call Nexus, or change modding
  state.
- Rechecked the profile-file hashes after inspection; all six still matched.
- Confirmed MO2 was not running after inspection.

### Source experiments

1. Traced every field read and written by `ModInfoRegular::readMeta()` and
   `saveMeta()`.
2. Traced archive `.meta` ingestion through `InstallationManager`.
3. Traced replace and merge paths, `installationFile` normalization, and
   `installedFiles` update behavior in the exact MO2 2.5.2 source.
4. Traced download-sidecar writes and searched for persisted hash fields.
5. Traced `.mohidden` suffix handling, configured skip suffixes, and
   hide/unhide filesystem operations.
6. Traced regular/separator/backup/foreign/overwrite classification and the
   one-to-many mod-ID index.
7. Searched the release-window C++ and C# FOMOD sources for durable plugin
   setting or option-history writes and followed selected-tree construction.
8. Traced BAIN's explicit `optionN` read/write behavior.
9. Compared exact 2.5.2 core behavior with current upstream to identify
   version-sensitive assumptions.

### Private aggregate experiment

The local reference inspection intentionally retained only aggregate counts.
No mod names, notes, URLs, descriptions, archive contents, or file listings
were copied into the repository.

| Population/observation | Result |
|---|---:|
| Physical top-level mod directories | 1,827 |
| Regular-mod directories with `meta.ini` | 1,818 |
| Missing `meta.ini` | 9 |
| Positive `modid` values | 1,727 |
| Zero `modid` values | 91 |
| Non-empty `installationFile` | 1,739 |
| Installation archive currently resolved | 1,683 |
| `installedFiles` arrays of size one | 1,538 |
| `installedFiles` arrays of size zero | 277 |
| Maximum `installedFiles` array size | 1 |
| Download archive-like files / sidecars | 2,727 / 2,727 |
| Download sidecars with a hash-like persisted key | 0 |
| Physical `.mohidden` files / directories | 1 / 0 |

For the selected profile's raw enabled-entry population, 1,691 entries exposed
a positive source-identity field but only 1,680 distinct positive
`(gameName, modid)` pairs existed. Two duplicate-identity groups were present,
with a largest group of eleven. There were 1,703 non-empty install-archive
hints and 1,647 currently resolved archives. These figures prove only that
collisions and missing source artifacts occur in one real-used, high-scale
profile. They are not prevalence estimates, readiness results, correctness
claims, or fixture expectations.

### Artifact handling

Official source repositories were cloned into the disposable local directory
`%LOCALAPPDATA%\Temp\infinium-wave-b-rq007`. No source clone, raw private
metadata, download URL, profile content, or temporary script was added to the
repository. This report is the only authorized repository artifact from the
investigation.

## 5. Findings

### 5.1 Regular-mod metadata retained by MO2 2.5.2

For a regular mod with `meta.ini`, MO2 2.5.2 can retain:

- `gameName`;
- `modid`;
- `version`, `newestVersion`, and `ignoredVersion`;
- `installationFile`;
- `repository`, defaulting to `Nexus` when absent;
- cached Nexus description, category, file status, last-query/update/modified
  times, tracking, and endorsement state;
- a custom URL and flag;
- comments and notes;
- categories, primary category, and display color;
- `converted` and `validated` UI override flags;
- `[installedFiles]` pairs of mod ID and file ID;
- opaque per-plugin settings under `[Plugins\<plugin name>]`.

These fields are mutable application state. `validated` means that a user
overrode MO2's “valid game data” warning; it is not a content-integrity
attestation. `converted` similarly represents an override for an alternate
game warning. Notes, categories, colors, source IDs, URLs, and versions can be
edited. A raw missing `repository` key and an explicit `repository=Nexus`
become the same interpreted value unless the adapter records key presence
before applying MO2's default.

The exact source also contains compatibility comments for earlier incorrect
FOMOD URL behavior. This confirms that retained metadata can embody old
installer behavior without recording which behavior produced it.

### 5.2 Download sidecars are archive claims, not content provenance

An archive sibling `.meta` can retain:

- game, repository, mod ID, file ID, mod/file names, version, newest version,
  category, description, file time, and download URLs;
- user data and mutable installed/uninstalled/paused/removed state.

MO2 may calculate an MD5 value transiently to query Nexus, but 2.5.2 does not
serialize a general archive hash in the sidecar. Infinium must compute its own
cryptographic hash over archive bytes when those bytes are available. Download
URLs can be expiring or sensitive and are unnecessary for most snapshot and
export contracts.

Sidecar `installed` state does not prove that a particular current mod folder
still consists of that archive's bytes. Replacement, merge, manual edits,
renames, copies, deleted archives, and generated output break that implication.

### 5.3 Installation mutates and can preserve metadata in non-obvious ways

At install time, MO2 reads a download sidecar when available. Otherwise it may
guess a name and ID from the archive filename. A FOMOD `info.xml` can override
name, version, ID, or URL. The final `meta.ini` does not retain the origin of
each value, so “read from sidecar,” “guessed,” “read from FOMOD,” and “edited
later” are not generally distinguishable.

When an archive resides inside the configured MO2 downloads directory,
`installationFile` is normally reduced to the filename; otherwise it can be an
absolute path. Resolution therefore requires:

1. preserving the raw value;
2. interpreting it under pinned instance settings;
3. testing the declared/relative path safely;
4. matching a sidecar and archive;
5. hashing the resolved bytes;
6. reporting unresolved or multiple matches explicitly.

Replace installation preserves and restores the old `meta.ini` around content
replacement, after which a subset of install fields is rewritten. User
metadata survives, but stale or mixed provenance can also survive. Merge keeps
existing physical files and overlays selected new output.

In exact MO2 2.5.2, the installation-manager path initializes `merge` to
`false` and never assigns the installer result to it before metadata handling.
Consequently `installedFiles` is cleared, and organizer completion adds only
the current mod/file pair. The private aggregate's maximum array size of one is
consistent with this implementation. Current upstream changes that behavior,
so no adapter may extrapolate newer merge-history semantics backward.

### 5.4 Physical folder identity and source identity are different relations

MO2's internal source-ID index maps `(gameName, modID)` to a **vector** of mods.
The model intentionally permits more than one installed mod folder per source
ID. Legitimate and ambiguous topologies include:

- one source mod split into several installed folders;
- multiple source archives merged into one installed folder;
- a copied or duplicated folder retaining the same metadata;
- a renamed folder retaining identity metadata;
- a replacement retaining user or older source metadata;
- generated/personal/manual mods with zero or absent source ID;
- a mod whose current content no longer matches its declared version;
- foreign/unmanaged content synthesized from the game Data directory;
- MO2's special overwrite output;
- name-pattern-classified separators and backups.

Therefore:

- local installed-mod identity is not a Nexus ID;
- folder display name is not source identity;
- same source ID does not authorize automatic deduplication;
- different or absent source ID does not prove different origin;
- declared installed version is a metadata claim, not content proof.

Infinium should distinguish at least:

- **declared installed version** — raw MO2 metadata;
- **source-file version** — validated source/archive metadata;
- **content-derived version** — only when a supported deterministic proof
  exists;
- **current physical content** — direct snapshot observation.

### 5.5 FOMOD historical choices are normally absent

The C++ FOMOD installer constructs an output tree from the current option
selections and conditions, then returns that tree for installation. The C#
installer has the same relevant persistence outcome. Neither inspected
release-window source persists a general typed choice history to regular-mod
metadata.

BAIN is a narrow exception. It stores selected subpackage names as plugin
settings such as `option0`, `option1`, and reads them during a later
installation. Those values are plugin-specific state, not evidence that all
installer plugins retain equivalent history. Opaque `[Plugins\...]` settings
must be decoded only by a named, versioned adapter for the responsible plugin.

The installed output usually does not preserve the original
`fomod/ModuleConfig.xml`; the private aggregate contained three top-level
`fomod` directories and no `ModuleConfig.xml`. An installed tree therefore
cannot be assumed to contain the installer definition.

### 5.6 Present-state FOMOD reconstruction has bounded meanings

When the exact source archive and installer configuration remain available,
Infinium could compare candidate option sets with the current installed
provider tree. The result must use one of these outcome classes:

| Outcome | Meaning |
|---|---|
| `recorded` | A supported, version-pinned installer explicitly persisted a typed selection; ordinary FOMOD does not qualify. |
| `unique-compatible-current-state` | Exactly one modelled selection is compatible with the observable current state and available assumptions. This is not proof of historical choice. |
| `multiple-compatible-current-state` | More than one selection can produce the relevant observable state. |
| `inconsistent-with-current-state` | No modelled selection explains the current state under the supplied inputs; manual change, merge, replacement, unsupported semantics, or wrong source may be involved. |
| `unavailable` | Required archive/configuration, semantics, conditions, or comparable current state is absent. |

Even `unique-compatible-current-state` requires:

- an exact retained archive hash and bytes;
- exact FOMOD configuration and information files;
- a version-pinned installer-semantics adapter;
- the current relevant provider tree;
- declared assumptions for condition evaluation;
- install-time plugin/file/runtime/environment condition inputs, where used.

MO2 does not generally retain the install-time condition environment. Two
different option paths can produce the same output, optional files can later
be removed or hidden, merged content can add or overwrite files, and manual
changes can invalidate a comparison. Exact archive retention is necessary for
some reconstructions but is never sufficient by itself.

Infinium should not execute arbitrary installer code merely to recover choices.
Any future reconstruction must use a bounded parser/evaluator with resource
limits and treat archive/XML content as untrusted.

### 5.7 `.mohidden` proves a current physical exclusion, not its history

MO2 hide/unhide actions physically rename a file or directory by adding or
removing `.mohidden`. The suffix is also in the default skip-suffix set, so the
renamed item is excluded from the virtual filesystem.

A present `.mohidden` path is strong evidence that the physical item currently
exists under an excluded name and that its unsuffixed relative path is the
candidate logical path. It does not establish:

- which user or tool renamed it;
- when or why it was hidden;
- whether it was ever active;
- whether a manually named `.mohidden` item came from MO2's UI;
- whether another unsuffixed item replaced it;
- a tombstone for content absent from the folder.

Unhide can collide with an existing unsuffixed path and require a user choice.
Custom configured skip suffixes can also exclude content without representing
MO2 hide history. Hidden archive members cannot be equated with loose
`.mohidden` behavior.

### 5.8 Manual changes are observable only as current state without a baseline

MO2 2.5.2 does not persist a general per-file installed baseline or hash
manifest for each mod. Without separately retaining the exact source artifact
and a reproducible install transformation, Infinium cannot attribute a
current difference to:

- a user edit;
- a post-install tool;
- a merge;
- a replacement;
- an installer option;
- an original archive difference;
- a manual deletion or rename;
- generated output.

The current bytes and provider relationships remain authoritative for current
state. Archive and metadata comparisons may produce a typed difference and a
bounded hypothesis, but not an invented actor or historical sequence.

### 5.9 Metadata authority and coverage matrix

| Evidence/state | What Infinium can safely assert | Authority class | Common ambiguity/failure | Required treatment |
|---|---|---|---|---|
| Physical mod folder/path and bytes | They exist in the captured instance at the observed time | Direct local observation | Rename changes path identity; concurrent mutation | Fingerprint and bind to snapshot |
| Raw `meta.ini` bytes and key presence | MO2-owned metadata file contains these raw values | Direct local observation | Values may be stale, edited, defaulted, or inherited | Preserve raw bytes/hash and parsed fields |
| Parsed `meta.ini` semantics | Pinned MO2 adapter interprets a field this way | Version-modelled interpretation | Parser/default drift; unsupported versions | Record adapter/version; fail closed |
| `gameName` / `repository` / `modid` | Candidate source-identity claim | Mutable identity hint | Zero/missing/stale/wrong/duplicate/defaulted | Validate independently; allow zero-to-many mapping |
| `version` | Declared installed version | Mutable identity hint | Does not prove content version | Label as declared, not verified |
| `installationFile` | Candidate source-archive locator | Mutable provenance hint | Absolute/relative/stale/missing/name collision | Preserve raw value, resolve and hash separately |
| `[installedFiles]` pair | MO2 retained a mod/file pair | Mutable provenance hint | 2.5.2 clears earlier pairs; IDs can be stale | Never treat as complete install history |
| Download archive bytes | Exact local artifact exists with computed hash | Direct local observation | Artifact may not be source of current content | Hash; link as candidate source only |
| Download `.meta` sidecar | Sidecar makes archive-level identity/state claims | Mutable archive hint | No persisted content hash; mutable state/URLs | Match by path/hash and validate IDs |
| Cached Nexus description/status | MO2 retained this cached content/state | External claim cached without complete source revision | Stale, user-unverifiable, missing acquisition provenance | Use only with explicit provenance gap or refresh |
| Notes/comments/categories/color | User/application statement and organization state | User input / local metadata | Not author intent or content truth | Preserve source type; never promote |
| Plugin settings | Named plugin stored opaque values | Plugin-specific observation | Meaning/version unknown; absent for most installers | Decode only with supported plugin adapter |
| Physical `.mohidden` item | Item currently exists under an excluded suffix | Direct local observation plus version-modelled effect | Actor/time/reason unknown; possible collision | Report current exclusion and historical gap |
| Absent file | File is absent from current observed provider tree | Direct current observation | No deletion/selection/history explanation | Do not invent cause |
| FOMOD candidate choice set | These choices are compatible with modelled current state | Reconstruction result | Missing conditions, same-output choices, manual changes | Use bounded outcome classes; never claim history |
| Foreign/unmanaged/overwrite classification | Pinned MO2 behavior classifies current content specially | Version-modelled interpretation | Not a regular source-metadata record | Report generic/current state and identity gap |
| User-confirmed mapping/adjudication | User asserts this scoped source relation | Versioned user statement | Can become stale after content/context change | Bind to scope/dependencies; revalidate |

### 5.10 Proposed identity model

An installed-mod snapshot entity should use a product-owned stable key derived
from:

- MO2 instance identity;
- captured physical mod-directory identity;
- snapshot observation/fingerprint.

That key identifies the captured local entity, not a source page. A separate
`SourceIdentityMapping` should relate zero or more installed entities to zero
or more source entities. Each mapping needs:

- source kind, game/domain, repository, mod ID, file ID, and version fields
  when available;
- every supporting and contradicting observation;
- resolution status and confidence;
- mapper/adapter/version;
- user adjudication, if any;
- applicability and invalidation dependencies;
- mapping revision and consuming-run links.

Candidate signals can include matching game/repository, positive mod/file IDs,
archive-sidecar identity, exact archive hash, compatible installed output, and
later supported-API validation. No single mutable MO2 field should silently
collapse entities.

Collision/adjudication states should cover:

- exact duplicate metadata;
- plausible split installation;
- plausible copy/duplicate;
- plausible multi-archive merge or latest-source hint;
- stale/mismatched identity;
- unresolved collision.

These are identity-topology states, not game-impact taxonomy classes. They do
not answer RQ-036 and must not be reused as categories of affected game
behavior.

## 6. Safe product contract and operational consequences

1. Inspect MO2 state read-only and compute product-owned hashes.
2. Preserve raw values and key presence before applying defaults.
3. Pin parsing and semantic interpretation to supported MO2/plugin versions.
4. Treat physical current state as authoritative for what is installed;
   metadata/source archives describe possible identity and provenance.
5. Make source identity a correctable, versioned analysis-context relation.
6. Support zero-to-many and many-to-zero source mappings.
7. Never auto-merge installed entities merely because IDs match.
8. Never describe ordinary FOMOD options as recorded history.
9. Expose missing archive, sidecar, identity, installer conditions, or history
   as explicit coverage gaps.
10. Ask for user adjudication only when ambiguity materially affects analysis.
11. Bind identity corrections and retained assumptions to dependencies so
    changes invalidate only consuming work.
12. Exclude sensitive download URLs and private notes from normal prompts and
    exports unless explicitly necessary and authorized.
13. Reject or narrow unsupported MO2/plugin versions instead of
    best-effort semantic conclusions.
14. Keep the private real-used profile out of correctness fixtures and release
    baselines.

## 7. Alternatives considered

### A. Use `(gameName, modid)` as the installed-mod primary key

Rejected. MO2 itself indexes the pair to a vector, and the private aggregate
contains collisions. Splits, copies, zero IDs, merges, foreign content, and
manual mods make the relation non-unique in both directions.

### B. Use folder name as source identity

Rejected. Folder names can be renamed, duplicated, guessed from archives, or
chosen independently of source. Folder/path remains useful as local snapshot
identity.

### C. Trust `installedFiles` as complete archive/install history

Rejected for MO2 2.5.2. Exact source inspection shows earlier entries are
cleared in the relevant path and only the current pair is added.

### D. Infer FOMOD history from current installed files and present it as fact

Rejected. Same-output options, conditions, missing archives, merges, hidden or
deleted content, and manual changes make historical inference unsound.

### E. Disable all installer-option analysis

Not selected. A bounded compatible-current-state analysis can still detect
missing expected output, incompatible option combinations, or ambiguity when
its prerequisites are explicit. It is lower priority and cannot become a
historical claim.

### F. Treat every `.mohidden` path as a user action recorded by MO2

Rejected. The suffix is a physical convention without actor, time, intent, or
origin history.

### G. Copy the creator's full profile into evaluation fixtures

Rejected. It is private, large, historically contingent, and not a
gold-standard representation. Synthetic atomic fixtures and small controlled
real-mod profiles provide reviewable ground truth without profile-specific
rules.

## 8. Uncertainty and limitations

- Installer plugin source commits were constrained to the release window, but
  the MO2 `mob` v2.5.2 tag does not provide a verified source-commit lock for
  each installed plugin. Local DLL hashes pin the binaries; a later reproducible
  build/binary-symbol comparison may increase confidence.
- No live installer experiment was run because this investigation was
  read-only and prohibited tool launches or setup mutation.
- The aggregate inspection establishes that ambiguity exists in one
  real-used profile; it does not establish ecosystem prevalence.
- Download archive filename resolution does not prove that the resolved
  artifact installed the current folder.
- Cached Nexus data was not validated against current upstream data.
- Custom installer plugins can use opaque plugin settings differently; absence
  of normal FOMOD persistence is not a universal statement about every
  third-party installer.
- NTFS file identity, junctions, case behavior, and instance relocation require
  careful handling in the future fingerprint design.
- Current-state FOMOD reconstruction feasibility and resource cost require
  synthetic prototypes before it can enter a milestone.
- The exact scope of user-visible source-mapping confidence labels remains a
  product/ADR decision.

## 9. Recommendation

### Recommended M1 boundary

For M1, acquire:

- raw regular-mod metadata with key presence and file hash;
- physical mod-folder state and content/provider fingerprints required by the
  accepted effective-state contract;
- raw download-sidecar metadata when locally available;
- exact archive fingerprint when an archive resolves;
- physical `.mohidden` and other version-supported exclusion observations;
- a typed identity-candidate set with contradictions and gaps.

Do not require:

- exact FOMOD selection history;
- complete merge/reinstall provenance;
- attribution of manual changes;
- a unique Nexus mapping for every installed mod;
- reconstruction by executing installer code.

### Recommended confidence/preconditions

| Recommendation | Confidence | Preconditions |
|---|---|---|
| Separate local installed entity from source mapping | High | Accepted domain/ADR update |
| Treat `meta.ini` identities and versions as mutable hints | High | MO2 2.5.2 adapter preserves raw and interpreted forms |
| Compute Infinium-owned archive hashes | High | Archive is locally available and read safely |
| Support zero-to-many/many-to-zero mapping | High | Mapping revision and dependency model |
| Report `.mohidden` as current exclusion, not history | High for default 2.5.2 behavior | Supported skip-suffix/config capture |
| Decode BAIN options only through a plugin-specific adapter | Medium-high | Exact supported plugin version/binary contract |
| Offer bounded FOMOD compatible-state reconstruction in its later planned analysis milestone, currently M3 rather than M1 | Medium | Exact archive/config, safe evaluator, condition/coverage model, evaluation fixtures |
| Attribute a difference to a manual edit | Low/unsupported without extra baseline | Exact trusted baseline plus reproducible transformation and unchanged dependencies |

### Gate B implication

RQ-007 can satisfy its Gate B contribution by defining explicit acquisition,
identity, ambiguity, and coverage semantics. Exact current state does not
depend on exact historical installer choices. Gate B remains dependent on the
other local-state questions, controlled ground-truth fixtures, and integration
review. If an M1 requirement is interpreted to demand historical FOMOD choices,
that requirement must be narrowed or marked unsupported rather than filled by
inference.

## 10. Downstream work enabled

### Proposed ADR

ADR-0008 accepts **local installed identity and source-identity mapping** with:

- physical snapshot entity key;
- source mapping cardinality;
- raw-versus-interpreted MO2 metadata;
- version-pinned adapter behavior;
- collision/adjudication states;
- user correction and invalidation;
- FOMOD reconstruction outcome vocabulary;
- fail-closed unsupported-version behavior.

The coordinated Wave B acceptance applies this boundary through ADR-0008.

### Proposed domain and requirement updates

- Add the local installed entity / source identity mapping distinction to the
  domain model.
- Clarify under `DOC-003` that MO2 IDs are evidence for mappings, not entity
  keys.
- Define declared/source/content-derived version terminology.
- Add typed installer-history and current-state-reconstruction gaps.
- Add raw-key-presence and adapter-version provenance requirements.
- Keep all impact/game-area categories dependent on RQ-036; nothing in this
  report defines them.

### Proposed evaluation work

- Extend `EVAL-0051` with missing metadata, zero IDs, edited ID/version,
  duplicate IDs, split/copy/rename/merge/replace, foreign/unmanaged, overwrite,
  separator/backup naming, stale archives, missing sidecars, `.mohidden`
  file/directory/collision, and custom skip-suffix cases.
- Extend `EVAL-0072` with an installer-state case set covering:
  - one unique-compatible present-state selection;
  - two choices producing the same output;
  - condition-dependent choices with missing install-time state;
  - post-install modification yielding no compatible choice;
  - merged output, hidden/deleted output, and missing archive;
  - FOMOD metadata conflicting with sidecar/regular-mod metadata;
  - an assertion that no result is labelled historical without a recorded
    typed history.
- Extend `EVAL-0019` so correcting a source mapping creates a new analysis
  context/revision without rewriting the physical snapshot.
- Exercise `EVAL-0054` for an unsupported MO2/plugin version.
- Exercise `EVAL-0067` and `EVAL-0083` across raw metadata, interpretation,
  source mapping, reconstruction, and coverage gaps.
- Keep tests synthetic and atomic first, then use small controlled MO2
  profiles. Use the private profile only for non-normative scale/shape checks.

### Proposed follow-up research

1. RQ-008: supported Nexus API identity validation, file/version resolution,
   and collision behavior under ADR-0005.
2. A bounded prototype for FOMOD compatible-current-state reconstruction
   before its planned `ANALYSIS-012` delivery, but not as an M1 prerequisite
   unless product prioritization changes.
3. Exact supported-version detection and adapter compatibility policy across
   MO2 core and installer plugins.
4. Integration with RQ-001 effective-state acquisition and RQ-004 profile
   semantics.
5. RQ-014/fingerprint work for archive, metadata, mapping, and invalidation
   dependencies.
6. The separately accepted
   [Skyrim SE mod-impact taxonomy](../../product/mod-impact-taxonomy.md)
   controls purpose, technical-surface, affected-area, consequence, and extent
   classification; none is settled by MO2 identity metadata.

## 11. Suggested RQ status

Suggested status for RQ-007: **Answered for M0 / ready for integration review**.

Rationale:

- The retained metadata set and its authority limits are characterized for the
  exact MO2 2.5.2 core.
- Nexus/source identity collisions and missing-identity cases have an explicit
  product treatment.
- Normal FOMOD history, BAIN's narrow exception, hidden-state semantics, and
  manual-change limitations are separated.
- M1 can proceed without pretending unavailable installer history exists.

The coordinated Wave B review applies this status through ADR-0008 and the
research registry. Evaluation work remains unexecuted.

## 12. Traceability and validation

### Traceability

| Finding | Requirements/decisions | Proposed downstream artifact |
|---|---|---|
| Local entity differs from Nexus/source identity | `DOC-003`, `SNAP-001`, ADR-0001, ADR-0002 | Identity/source-mapping ADR and domain update |
| Metadata is mutable and version-interpreted | `EVID-001`, `EVID-002`, `SNAP-005` | Version-pinned MO2 adapter contract |
| Current 2.5.2 `installedFiles` is not complete history | `EVID-002`, `SNAP-006` | Provenance-gap semantics and fixture |
| Normal FOMOD choices are not recorded | `EVID-006`, `COVER-001` | Installer-history coverage type and bounded prototype |
| Compatible-state inference cannot become historical proof | `ANALYSIS-012`, `EVID-006` | `EVAL-0072` extension and reconstruction vocabulary |
| `.mohidden` proves current exclusion only | `SCOPE-005`, `EVID-001` | Effective-state integration fixture |
| Manual-edit attribution is unavailable without a baseline | `EVID-005`, `EVID-006`, `SNAP-006` | Abstention and validation language |
| Unsupported-version drift must fail closed | `SCOPE-002`, `SCOPE-006` | `EVAL-0054` extension |
| Private reference is not a gold standard | Evaluation anti-overfitting rules | Synthetic/small-profile fixture program |

### Validation performed

- Re-read the report against RQ-007 and the Wave B handoff.
- Checked that every empirical count is aggregate and non-normative.
- Checked that direct observation, MO2 interpretation, mutable identity hints,
  reconstruction, user statements, and unavailable history remain distinct.
- Checked that no impact/game-area taxonomy was introduced.
- Checked that no architecture decision was silently accepted.
- Checked all referenced requirement, ADR, RQ, and evaluation IDs against the
  current documentation set.
- Checked local relative links and immutable external source links for
  syntactic correctness.
- Checked repository diff scope: this investigation file only.
- Ran Git whitespace validation for this file.

The remaining integration work is deliberately proposed rather than applied.
