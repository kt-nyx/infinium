# RESEARCH-0005: MO2 effective-state acquisition

Status: Completed
Disposition: recommendation accepted by ADR-0008
Date: 2026-07-25
Last reviewed: 2026-07-25
Researcher: Codex agent
Primary RQ: RQ-001 — How can Infinium obtain authoritative MO2 profile and
effective VFS state by deterministic reconstruction or bounded execution
through the user's MO2, and is direct USVFS operation ever necessary?
M0 wave: Wave B
Decision enabled: MO2 integration ADR and the authoritative-state prerequisite
for EVAL-0051 and EVAL-0046

Accepted disposition:
[ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md)
accepts the version-pinned, quiescent deterministic reconstruction boundary
and rejects live-instance execution and direct USVFS for the initial product.
Its conformance and non-mutation tests remain qualification gates rather than
completed evidence.

## 1. Question and accepted authority

This investigation asks which mechanism can give Infinium an exact,
provenance-bearing account of one selected MO2 profile without modifying the
user's setup:

1. version-pinned deterministic reconstruction from MO2 configuration,
   profile files, and physical providers;
2. execution of a read-only observer through the user's MO2;
3. use of MO2's in-process plugin interfaces as an oracle or product boundary;
4. direct use of USVFS; or
5. a combination with explicit authority and coverage gaps.

The accepted constraints are:

- [SCOPE-002, SCOPE-003, and SCOPE-005](../../product/requirements.md) restrict
  the initial target to one explicitly selected MO2 profile and require
  reconstruction of plugins, records, loose files, archives, generated output,
  configuration, game Data, and relevant root state.
- [AUTH-001 through AUTH-003](../../product/requirements.md) prohibit changes
  to MO2, the profile, mod/game files, configuration, and generated output
  through M4. External operations must have known isolated cache/temp effects.
- [SNAP-001 through SNAP-004](../../product/requirements.md) require immutable
  snapshot binding, mid-capture/mid-run change detection, visible staleness,
  and dependency-proven carryover.
- [TOOL-001 through TOOL-003](../../product/requirements.md) make MO2 a
  required user-installed application whose identity/version/path and
  supported capabilities are validated rather than bundled or managed.
- [ADR-0001](../../architecture/decisions/ADR-0001-evidence-authority-boundary.md)
  makes deterministic local state authoritative and excludes LLM inference
  from winner reconstruction.
- [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md)
  requires a logically immutable installation snapshot and explicit validity
  dependencies.
- [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md)
  excludes setup mutation and product-initiated game/MO2 launch through M4
  unless a later authority decision explicitly changes that boundary.
- [ADR-0004](../../architecture/decisions/ADR-0004-initial-target-scope.md)
  permits a concrete MO2 2.5.2/Skyrim SE/Windows adapter rather than requiring
  a premature generic-manager abstraction.
- [ADR-0006](../../architecture/decisions/ADR-0006-gpl-product-and-tool-dependency-boundary.md)
  keeps MO2 user-installed, disfavors direct USVFS use, and requires this
  investigation to compare reconstruction and execution-through-MO2 first.

Research evidence does not accept an integration mechanism. A later proposed
ADR must select it, and controlled evaluation must prove conformance.

## 2. Scope and explicit non-scope

### In scope

- The MO2 2.5.2 profile/mod priority algorithm, active-mod mapping order,
  loose-file winner behavior, overwrite directory, unmanaged Data state,
  secondary Data roots, configurable skipped suffixes/directories, archive
  visibility model, and plugin-provided file mappings.
- The relationship between MO2's internal directory model and the mappings
  sent to USVFS.
- MO2 2.5.2's supported `run` command and the state writes surrounding a
  virtualized process.
- The relevant USVFS 0.5.0 controller/mapping behavior used by MO2 2.5.2.
- Safe, bounded, aggregate observations from the user-confirmed
  `Brain Blast Destruction 2024` profile.
- The exact state surfaces and authority/gap semantics required for an M1
  snapshot.
- A conformance-test design using synthetic atomic fixtures and small
  controlled profiles.

### Explicitly out of scope

- RQ-002's decision about current/last-selected-profile suggestion semantics.
- RQ-004's final Bethesda plugin/archive/string parser and runtime archive-load
  semantics.
- RQ-007's full identity, source-archive, FOMOD, and manual-change model.
- RQ-014's fingerprint algorithm and measured high-end IO budget.
- LOOT, LLM, documentation, and record-semantic integration.
- Running MO2, USVFS, Skyrim, or a VFS-hooked observer against the user's real
  instance.
- Treating the creator's profile as correct, representative, a fixture, or a
  source of production rules.
- Choosing a production language, process topology, or storage schema.

## 3. Sources and exact versions

All sources were retrieved on 2026-07-25. Commit links are immutable primary
source. GitHub release metadata was used only to relate the checked source to
the released/local versions.

| Source | Exact identity | Authority and claim-level relevance |
|---|---|---|
| [MO2 releases](https://github.com/ModOrganizer2/modorganizer/releases/tag/v2.5.2) | `v2.5.2`, commit `9c130cbf2fc7225fb2916e46419af50671772aa0` | Official release identity; records USVFS 0.5.0 and the default `.mohidden`/`.git` exclusions. |
| [MO2 `profile.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/profile.cpp#L390-L617) | Same commit | Primary implementation of `modlist.txt` parsing, reconciliation with discovered mod objects, automatic priorities, order inversion, overwrite behavior, and active-mod enumeration. |
| [MO2 `directoryrefresher.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/directoryrefresher.cpp#L174-L490) | Same commit | Primary implementation of the Data-origin, secondary-root, enabled-mod, loose-file, optional archive, winner-tree, and cleanup model. |
| [MO2 `fileentry.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/shared/fileentry.cpp#L18-L183) | Same commit | Primary implementation of provider alternatives and winner selection: higher mod priority wins loose/loose, archive order applies to archive/archive, and loose wins over archive in MO2's directory model. |
| [MO2 `directoryentry.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/shared/directoryentry.cpp#L112-L225) | Same commit | Primary implementation of filesystem enumeration and MO2's optional enabled-archive association/order model. |
| [MO2 `organizercore.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/organizercore.cpp#L974-L1007) | Same commit | Shows the in-process `IOrganizer` file-info view over MO2's directory structure. |
| [MO2 mapping/run code](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/organizercore.cpp#L1951-L2104) | Same commit | Primary evidence for pre-run profile saves, USVFS mapping construction, overwrite/custom-write targets, plugin file mappers, and post-run refresh/list writes. |
| [MO2 settings code](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/settings.cpp#L319-L363) | Same commit | Primary evidence for configurable skipped suffixes/directories and archive parsing default. |
| [MO2 command-line `run`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/commandline.cpp#L686-L792) | Same commit | Official supported command that starts a file/configured executable with the virtual filesystem; it enters the normal process-runner path. |
| [MO2 USVFS connector](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/usvfsconnector.cpp#L124-L240) | Same commit | Shows VFS/log initialization, skipped-path configuration, mapping replacement, and create-target flags used by MO2. |
| [USVFS README](https://github.com/ModOrganizer2/usvfs/blob/9f7fd9660d51784aa2117cb45f2095e87312d558/README.md) | `v0.5.0`, commit `9f7fd9660d51784aa2117cb45f2095e87312d558` | Upstream description of process-local API-hook virtualization and its alpha status/operational drawbacks at the version integrated by MO2 2.5.2. |
| [USVFS public API](https://github.com/ModOrganizer2/usvfs/blob/9f7fd9660d51784aa2117cb45f2095e87312d558/include/usvfs.h#L42-L167) | Same commit | Primary API contract for recursive links, create targets, controller connection, hooked process launch, VFS dump, suffix/directory skips, and force-loaded libraries. |
| [USVFS mapping implementation](https://github.com/ModOrganizer2/usvfs/blob/9f7fd9660d51784aa2117cb45f2095e87312d558/src/usvfs_dll/usvfs.cpp#L618-L792) | Same commit | Primary behavior for suffix/directory exclusions and recursive static directory mapping. |
| [Current USVFS releases](https://github.com/ModOrganizer2/usvfs/releases) | Latest listed upstream release `v0.5.7.2`, tag object `a50d84c64c9244f80dc67e9fe7af209bfe514d5b` | Establishes that current standalone USVFS has advanced beyond the version embedded by the supported local MO2 candidate; newer behavior cannot be silently substituted. |
| [Wave B reference manifest](WAVE-B-reference-environment-manifest.md) | Captured 2026-07-25 | Local executable identities, sanitized path tokens, reference-profile fingerprints, and owner-stated limits on use of that profile. |
| [RESEARCH-0002](RESEARCH-0002-helper-tool-licensing.md) | Completed 2026-07-25 | Accepted application/library/distribution boundary; licensing does not establish technical fitness or authorize execution. |

The local executable is MO2 2.5.2 with SHA-256
`442B354A8F34754DA0048654C44D27F51628FEBA54CE46C3187CF58D6C43E622`.
This investigation therefore treats commit `9c130cb...` and its USVFS 0.5.0
dependency as the relevant source oracle. The later USVFS release is recorded
only as drift evidence.

## 4. Experiments and retained artifact manifest

### 4.1 Exact-source checkout and trace

Procedure:

1. Created a research-only directory under the OS temporary-data root.
2. Cloned the official MO2 and USVFS repositories over HTTPS without
   credentials.
3. Checked out MO2 commit `9c130cb...` and USVFS commit `9f7fd96...` detached.
4. Used `rg`, `Get-Content`, and `git show/ls-remote` to trace profile parsing,
   provider ordering, mappings, run lifecycle, skip settings, and release
   identities.

Observed effects:

- outbound unauthenticated HTTPS to GitHub;
- disposable source files under the OS temporary-data root;
- no repository write, protected-setup write, process hook, MO2 launch, USVFS
  initialization, or game launch.

Retained artifact:

- This report retains immutable URLs, commits, procedure, and conclusions.
- The temporary clones are disposable and are not a replay dependency; they
  can be reacquired from the recorded commits.

### 4.2 Reference-profile structural observation

Preconditions:

- MO2 was not running.
- The six profile fingerprints in
  [the shared manifest](WAVE-B-reference-environment-manifest.md) matched
  immediately before inspection.
- Only aggregate counts, configuration keys, and fingerprints were recorded.
  No mod/plugin/archive names or raw profile text were retained.

Observed structure:

| Observation | Result |
|---|---:|
| `modlist.txt` `+` entries | 1,793 |
| `modlist.txt` `-` entries | 36 |
| `modlist.txt` `*` entries | 4 |
| `modlist.txt` comment lines | 1 |
| enabled (`*`) entries in `plugins.txt` | 2,280 |
| non-comment, non-`*` entries in `plugins.txt` | 0 |
| non-empty archive names in `archives.txt` | 394 |
| physical top-level directories under the instance mods root | 1,827 |
| top-level entries under overwrite | 0 |
| configured skipped suffix | `.mohidden` |
| configured skipped directory | `.git` |
| archive parsing | `false` |

The six fingerprints matched again after inspection. The aggregate mismatch
between profile entries and physical mod directories confirms, without
identifying any mod, that `modlist.txt` cannot by itself be treated as the
resolved active-mod set. MO2's source independently explains additional
automatic/foreign/backup/overwrite objects and reconciliation behavior.

This profile is descriptive evidence of a real MO2 shape only. Its unusually
large counts do not define normal scale, correctness, fixture expectations, or
production exceptions.

### 4.3 Why no VFS process experiment was run

No observer was launched through the real MO2 instance and no USVFS controller
was initialized. Source preflight found material setup-affecting behavior in
MO2's normal run path:

- `beforeRun()` calls `saveCurrentProfile()`, forces pending `modlist.txt`
  writes, and installs the VFS mapping;
- `afterRun()` may remove `loadorder.txt` for file-time games, refreshes the
  directory/plugin state, and saves the plugin list;
- the USVFS connector creates a log under MO2's data path, creates/connects a
  shared VFS, installs skip/forced-library state, and configures create-target
  routing.

Those behaviors disqualify an experiment against the protected real setup
under AUTH-001 through AUTH-003. A later conformance experiment must use a
disposable MO2 instance, synthetic game/Data roots, and before/after manifests
that deliberately permit and enumerate the instance-local writes.

## 5. Findings

### 5.1 Recommended answer

**Proposed answer:** Infinium should use a version-pinned deterministic
reconstruction as the production acquisition path, with MO2 itself used only
as a controlled conformance oracle on disposable fixtures. Direct USVFS
operation is not necessary for the M1 read-only state surfaces and should not
be admitted. Running a helper through the user's MO2 is technically available
but does not satisfy the accepted non-mutation contract because MO2's launch
path writes and normalizes profile/application state.

Confidence is **high** for the MO2 2.5.2 source behavior and the negative
conclusion about the real-instance `run` path; **medium** that the reconstruction
route will reach complete EVAL-0051 conformance because the controlled
positive/negative/boundary fixture matrix has not yet been executed.

### 5.2 There are three distinct “effective state” concepts

They must not be collapsed:

1. **Profile intent/state on disk:** selected instance/profile, `modlist.txt`,
   plugin/load-order files, archive list, profile settings/INIs, MO2 instance
   paths/settings, and mod metadata.
2. **MO2's resolved static provider model:** discovered mod objects plus
   profile enablement/priority, physical Data/secondary roots, overwrite,
   loose/archive alternatives, and the winners computed by MO2's directory
   model.
3. **One hooked process's runtime view and write routing:** USVFS mappings,
   plugin file-mapper additions, profile-local-save/INI mappings,
   executable-specific custom overwrite/create target, forced libraries,
   skipped paths, and any runtime hook limitations.

Infinium needs the first two for static preflight. It needs selected parts of
the third as declared configuration/dependencies, not a continuously running
hooked process. A hooked process sees effective paths but does not by itself
provide the complete losing-provider chain or stable snapshot provenance.

### 5.3 `modlist.txt` is necessary but not sufficient

MO2 2.5.2:

- reads `+`, `*`, `-`, unprefixed, comment, duplicate, missing, and invalid
  lines;
- reconciles those lines against its discovered `ModInfo` collection;
- drops missing/invalid entries from its resolved model and schedules a
  rewrite;
- inserts newly discovered regular and foreign entries using different
  priority rules;
- treats always-enabled/always-disabled, backup, foreign, and overwrite
  objects specially;
- reverses file order so the resulting in-memory priority increases toward
  the winner; and
- enumerates only enabled resolved objects from the resulting priority map.

Therefore exact reconstruction needs both the profile text and the same
version-specific discovered-object inputs. A parser that trusts the file
without enumerating the configured mods root, Data/unmanaged state, overwrite,
and relevant game-plugin contributions is incomplete.

### 5.4 Loose-file reconstruction is feasible without USVFS

For ordinary Data mappings, MO2 2.5.2 sends enabled regular mod directories in
ascending priority followed by overwrite. USVFS recursively adds each mapping
and permits later entries to replace earlier file entries. MO2's separate
directory model starts with the physical game Data/secondary roots, adds active
mods in priority order, sorts providers, and selects the highest-priority loose
provider.

A deterministic adapter can reproduce this without hooks by:

1. resolving and validating the MO2 instance, game, profiles, mods, and
   overwrite roots;
2. requiring an explicitly selected profile;
3. capturing MO2/versioned game-plugin/settings inputs;
4. enumerating physical Data and every enabled provider with Windows
   case-insensitive path semantics;
5. applying the exact 2.5.2 active-mod/priority reconciliation;
6. excluding configured skip suffixes/directories such as `.mohidden` and
   `.git`;
7. adding overwrite last;
8. retaining the full ordered provider chain per normalized relative path, not
   merely the winner;
9. fingerprinting declared dependencies before/after capture; and
10. abstaining or creating a coverage gap for unsupported mappings, access
    failures, or version drift.

The adapter must not copy MO2's `DirectoryRefresher` output uncritically as the
literal hooked-process view. `cleanStructure()` deliberately removes top-level
`meta.ini`, `readme.txt`, and `fomod` entries from MO2's directory model, while
the USVFS path maps whole enabled directories and skips only the configured
suffixes/directories. Those management/documentation files should remain in
the physical-source inventory, be classified separately from game-relevant
effective Data, and remain available to local-documentation/FOMOD analysis.
The intended mapped view, MO2's conflict/UI model, and physical mod contents
are related but non-identical evidence surfaces.

This is an interoperability reimplementation of a versioned contract, not a
claim that MO2's profile files are a public stable schema. It must fail closed
for unvalidated MO2/game-plugin versions.

### 5.5 Archive provider state is a separate authority surface

MO2's directory model parses archive contents only when its experimental
archive-parsing setting is enabled; the 2.5.2 default is `false`. When enabled,
it filters to archive names in the profile's enabled archive list and associates
plugin-named archives with plugin load order. Its `FileEntry` model gives loose
files precedence over archived files and uses archive order for archive/archive
competition.

This is useful comparison evidence, but it is not sufficient proof of every
Skyrim SE engine archive-loading rule. In the reference instance, archive
parsing is explicitly false despite a non-empty `archives.txt`, so the MO2 Data
tab's directory model cannot be assumed to expose the archive contents that the
game may load.

USVFS maps an archive container file like another file in virtual Data; it does
not expand that container into member paths for the hooked process. MO2's
optional directory-model archive parsing is a separate UI/conflict-model
operation, and Skyrim's archive loader determines the runtime member view.
Consequently, neither a raw USVFS dump nor MO2's optional directory model alone
is archive-member ground truth.

Consequences:

- the snapshot must retain enabled archive identities, physical providers,
  plugin/load-order and relevant INI dependencies even when MO2 UI archive
  parsing is off;
- RQ-004 must supply and validate the archive parser and exact provider/winner
  semantics;
- an M1 fixture that depends on archived facegen/assets cannot pass Gate B
  until that semantic route is proven;
- if M1 is intentionally limited to loose assets, archived equivalents remain
  an explicit unsupported population rather than being silently treated as
  absent.

### 5.6 Plugin order is disk-observable but game-plugin-dependent

`plugins.txt` and `loadorder.txt` are snapshot inputs, not a complete
standalone contract. MO2's game plugin can enforce primary plugins, masters,
file-time behavior, and other game-specific rules when refreshing the list.
The effective plugin set also depends on which plugin files win in the
reconstructed Data tree.

For M1, the adapter must:

- parse the exact selected-profile files;
- correlate every enabled/order entry with the effective plugin-file provider;
- apply only a separately validated pinned Skyrim SE/MO2 game-plugin contract;
- report duplicates, absent providers, malformed lines, unsupported flags, or
  disagreement as gaps/findings rather than guessing; and
- include profile-file, provider, game-plugin, MO2, runtime, and parser
  identities in validity dependencies.

RQ-003/RQ-004 and parser-independent fixture truth own the remaining runtime/plugin semantic
proof. RQ-001 establishes only that MO2/profile/provider acquisition can supply
their exact inputs.

### 5.7 MO2-specific mappings must be inventoried, not assumed away

MO2's final VFS mapping can include:

- the game Data directory and game-plugin-provided secondary Data directories;
- every enabled regular mod directory;
- overwrite or an executable-specific custom overwrite/write target;
- profile-local save mappings;
- profile-local INI or other mappings contributed through game features; and
- mappings supplied by enabled `IPluginFileMapper` plugins.

A static Data snapshot can ignore local-save mappings when saves are outside
the declared analysis scope, but it cannot silently ignore a mapper that
changes Data, configuration, or another M1 input. The adapter needs an
allowlisted mapping-capability registry keyed by exact MO2/game/plugin
versions. An unknown enabled file mapper creates an unsupported/gap result.

Root-level files that are not virtualized by MO2 remain direct filesystem
inputs. They are not inferred from the VFS.

### 5.8 Authority and gap model

| State surface | Production acquisition authority | Required dependencies | Gap/abstention condition |
|---|---|---|---|
| MO2 executable/instance | Validated user-configured/detected executable plus instance configuration | Executable hash/version, instance/path configuration | Missing, inaccessible, ambiguous, unsupported, or changed identity |
| Selected profile | Explicit user selection of an existing profile directory | Profile identity/path and complete profile-file fingerprint set | No explicit selection, unreadable profile, or in-memory-only unsaved state |
| Resolved mods/priority | Version-pinned reconstruction over `modlist.txt` and discovered mod objects | MO2/game-plugin adapter version, mods/Data/overwrite inventories, automatic-mod rules | Unknown object type, missing directory, malformed/duplicate input not covered by contract, version drift |
| Loose provider chains | Full deterministic enumeration in normalized MO2 order | All provider directory identities, skip settings, Data/secondary roots, overwrite, mapper registry | Access failure, unsupported reparse behavior, unknown mapper, mid-capture change |
| Plugins/order | Profile files correlated with winning plugin-file providers and pinned game semantics | `plugins.txt`, `loadorder.txt`, effective plugin paths, game-plugin/runtime contract | Inconsistent/malformed list, missing provider, unsupported game-plugin behavior |
| Archives | Separate deterministic archive index and validated Skyrim load model | Enabled archive list, physical archive providers, plugin order, relevant INIs, parser version | Unsupported/corrupt archive, unproven load rule, encrypted/unknown format, changed dependency |
| Profile-local configuration | Direct profile/config reads plus validated MO2 mapping semantics | Profile settings/INIs, game document paths, game-feature version | Unknown mapping feature, missing file, ambiguous winner |
| Data/unmanaged state | Direct physical Data/secondary-root enumeration as lowest-priority origins | Runtime install/Data fingerprints and MO2 foreign-mod rules | Inaccessible/changed root, unknown secondary directory |
| Root/native state | Direct physical game-root inspection | Runtime/root manifest and named analyzer support | Unsupported component/format; never fabricated from VFS |
| Runtime hooked-process behavior | Not a production snapshot authority through M4 | Later explicit launch/side-effect ADR and process-specific manifest | Always a gap if a conclusion requires observing live hook behavior |

Stored evidence should distinguish:

- **observed bytes/metadata:** direct profile/config/filesystem facts;
- **version-modeled deterministic result:** provider/order result produced by a
  named MO2 adapter version;
- **conformance evidence:** comparison with MO2 on a controlled disposable
  fixture;
- **runtime-only observation:** process-scoped VFS behavior, if a later
  authority decision permits it; and
- **coverage gap:** unavailable, unsupported, changed, or unvalidated state.

Calling a reconstructed winner “MO2-authoritative” is justified only after its
adapter/version/surface passes the applicable conformance cases. Before then it
is a deterministic research result.

### 5.9 Running an observer through MO2 is not the production answer

MO2 2.5.2 officially supports:

```text
ModOrganizer.exe [-p PROFILE] run [options] NAME
```

This can expose a child process to the virtual filesystem, so it is a useful
test oracle on a disposable instance. It is not eligible as Infinium's normal
read-only acquisition route:

- entering the run path saves/normalizes MO2 profile state before execution;
- completion refreshes directory/plugin state and saves lists;
- MO2 and USVFS create logs and controller state;
- write routing targets overwrite or a configured mod, so a supposedly
  observational child still executes with write capability;
- a directory listing exposes the effective winner but not an authoritative
  full provider chain; and
- a hooked child's view can be affected by USVFS/process/API-hook limitations.

A production sandbox that prevents every child write would still not remove
MO2's own pre/post-run writes. Using a shadow copy of the instance merely turns
this into a conformance experiment, not the user's current authoritative state.

### 5.10 MO2's in-process plugin interface is an oracle, not a clean product API

MO2 exposes `resolvePath`, directory/file enumeration, and `findFileInfos` to
loaded plugins. `findFileInfos` can report the winning full path, origin list,
and winning archive from MO2's current directory structure.

Material limits:

- it reports MO2's optional directory model, which may omit archive contents
  when archive parsing is disabled;
- a plugin must run inside initialized MO2 and inherits MO2 lifecycle/state;
- installing or loading an Infinium plugin into the user's MO2 expands the
  protected-setup/plugin execution boundary;
- no stable headless external snapshot API was found in the checked source;
- the interface returns current in-memory state but does not itself freeze all
  source bytes or provide Infinium's dependency-validity proof.

For EVAL-0051, a research-only plugin or manually inspected MO2 view may serve
as one oracle inside a disposable instance. It should not be selected as the
M1 production integration without new evidence and an authority/security ADR.

### 5.11 Direct USVFS does not solve the product problem

USVFS 0.5.0 can create/connect a controller, add recursive directory/file
mappings, launch hooked processes, and emit a readable VFS dump. Direct use is
still the weakest option for Infinium:

- it is an API-hooking runtime whose own checked README labels it alpha and
  documents process initialization, performance, antivirus, and
  hard-to-diagnose failure risks;
- Infinium would have to reproduce MO2's resolved active-mod order, paths,
  skipped entries, plugin mappers, secondary roots, and create-target choices
  before USVFS could map anything, so it does not remove reconstruction work;
- the dump describes the controller tree, not Infinium's immutable
  file/archive/provider/content manifest or every Skyrim archive rule;
- direct controller state requires explicit coordination with any running MO2
  and is process/version-specific; the public API permits only one connected
  VFS per controller process;
- `LINKFLAG_CREATETARGET` deliberately grants write redirection to one source;
- direct use adds native ABI, hook, shared-state, logging, crash, antivirus,
  version, and cancellation surfaces to a read-only analyzer; and
- the checked local integration uses 0.5.0 while upstream has later releases,
  so silently loading an arbitrary present USVFS DLL would be unsafe.

No M1-required static state surface was found that only direct USVFS can
provide. The direct-USVFS necessity criterion is therefore not met.

### 5.12 Snapshot capture must establish a quiescent disk boundary

MO2 keeps mutable state in memory and may defer writes. No supported external
API was found that atomically freezes the in-memory profile plus every physical
provider for another application. The M1-safe contract should therefore:

1. require MO2 not to be running during snapshot capture;
2. require explicit profile selection rather than infer active state;
3. resolve and fingerprint instance/profile/provider inputs;
4. enumerate and derive the snapshot;
5. recheck all cheap control files and every dependency whose capture could
   race;
6. invalidate/retry the affected stage on change; and
7. never launch MO2 merely to “refresh” or normalize state.

If MO2 is running, Infinium should stop capture and ask the user to close it,
or record the state as unsupported for an authoritative snapshot. Reading
disk while unsaved in-memory state may exist cannot be labeled exact.

RQ-014 must choose the efficient dependency/fingerprint strategy and decide
which large populations require journal, stat, directory-manifest, sampled, or
content-hash treatment. Modification time alone remains insufficient.

### 5.13 Exact M1 surface requirement

For the initial scope-incongruent-reversion proof, Gate B can be satisfied by
the reconstruction route only if the selected fixture declares and proves all
of these exercised surfaces:

- selected MO2 instance/profile;
- resolved enabled mod set and priorities;
- winning plugin files, enabled plugin set, and load order;
- complete record provider/override chain under the accepted Bethesda layer;
- complete loose facegen/asset provider chains;
- physical Data/unmanaged providers;
- relevant profile configuration; and
- archive providers if any fixture input is archived.

Generated outputs, root/native components, local saves, and unrelated
configuration may remain outside that bounded M1 analyzer only when their
exclusion is explicit and the fixture cannot depend on them. This does not
remove them from SCOPE-005 or later product coverage.

The full Wave B gate is **not established by this report alone**. Controlled
MO2 conformance evidence, RQ-004 archive/record evidence, RQ-003 runtime
pinning, and RQ-014 measured snapshot validity remain dependencies.

## 6. Observed and anticipated side effects

| Operation | Observed or source-established effects | AUTH result |
|---|---|---|
| Read profile/config/filesystem metadata while MO2 is closed | Read-only; before/after profile fingerprints were identical | Eligible research observation |
| Clone/read official source in OS temp | Network read plus disposable research-owned files | Eligible; no protected setup effect |
| Start a helper with MO2 `run` | MO2 saves current profile before launch, creates/updates VFS/log state, and refreshes/saves lists after launch; child has a create target | Ineligible against the user's protected setup through M4 |
| Load a custom MO2 plugin | Requires plugin placement/loading and executes in MO2's privileged process | Ineligible without a new accepted mechanism/authority decision; possible only in a disposable research oracle |
| Initialize/operate USVFS directly | Native controller/shared VFS, hooks for child processes, logs/debug state, mapping/forced-library/skip configuration, possible create-target routing | Ineligible as current production acquisition; no demonstrated necessity |
| Enumerate/copy a reconstructed overlay into product temp | Potentially large product-owned IO; copy creates a derived view and may lose provider provenance | Possible later tool-compatibility technique, not the state authority; requires RQ-014/performance and protected-root checks |

## 7. Realistic alternatives

| Alternative | Strengths | Material weakness / rejection criterion | Proposed disposition |
|---|---|---|---|
| Version-pinned deterministic reconstruction | Read-only; full provider chains; snapshotable; testable; no hooks; works offline; exact dependency model possible | Must reproduce version/game-plugin semantics and pass controlled MO2 conformance; unknown mappers/versions must fail closed | **Recommended production path**, conditional on EVAL-0051 |
| Read-only observer launched with user's MO2 | Sees a hooked process view using the user's actual mapping | MO2 pre/post-run writes; child write authority; winner-only view; hook/process limitations | Reject for production through M4; use only in disposable conformance fixtures |
| MO2 in-process plugin export | Can query current `IOrganizer` directory state and origins | Requires installation/loading, initialized GUI/lifecycle, optional archive-model gaps, privileged in-process code | Research/evaluation oracle only; reject as current production boundary |
| Direct USVFS controller/dump | Closest to runtime mapping implementation | Still needs MO2 mapping reconstruction; no archive/game semantics; native hook/ABI/shared-state risk; no stable snapshot/provider contract; no necessity | Reject for M1 and default architecture |
| Parse only `modlist.txt`/`plugins.txt`/`loadorder.txt` | Simple and cheap | Misses discovered/foreign/overwrite state, missing/new dirs, skip settings, mappers, physical providers, archives, unsaved memory | Reject as authoritative; retain raw files only as inputs/evidence |
| Inspect physical game Data only | No MO2 integration | Misses enabled mod directories and VFS winners | Reject |
| Materialize a complete overlay copy in product temp | Ordinary tools can read a physical merged tree; can freeze bytes | Expensive at scale, duplicates content, can obscure provider chains, still requires reconstruction first | Defer as an optional downstream-tool staging technique, never the state authority |
| Link/reuse MO2 internal classes as a library | Potential semantic reuse | MO2 core is not presented as a stable standalone library; initialization/lifecycle writes and version coupling remain | Reject absent a supported upstream library/API and new technical research |

### Rejection criteria for the recommended route

The deterministic route must be rejected or narrowed if controlled tests show:

- a supported M1 state surface depends on runtime hook behavior that cannot be
  derived from declared mappings;
- MO2's provider order cannot be reproduced across positive, negative,
  malformed, hidden, unmanaged, archive, and mapper cases;
- an unavoidable enabled mapper has no inspectable deterministic contract;
- quiescent capture cannot detect changes within the measured IO budget; or
- the adapter would need to mutate/normalize the user's setup to obtain its
  inputs.

Evidence for any one of those outcomes should reopen the USVFS/MO2-plugin
alternatives through a new proposed ADR rather than silently weakening
authority.

## 8. Contrary evidence, uncertainty, limitations, and unsupported cases

### Contrary and boundary evidence

- “`modlist.txt` is the mod list” is incomplete: MO2 reconciles it with a
  discovered object collection, automatic objects, Data, and overwrite.
- “MO2's Data tab equals the game's full runtime Data view” is false when
  archive parsing is disabled and is not proven for every game archive rule
  even when enabled.
- “A read-only child makes `ModOrganizer.exe run` read-only” is false at the
  integration boundary because MO2 itself saves and refreshes state and the
  child receives write routing.
- “Using USVFS avoids implementing MO2 semantics” is false: USVFS consumes
  mappings already resolved by MO2 and knows nothing about profile intent,
  plugin enablement, or Skyrim archive semantics.
- “The creator's real profile is a ground-truth fixture” is explicitly
  rejected. It is neither known-correct nor representative.

### Material uncertainty

1. No controlled MO2 oracle profile was executed in this investigation, so
   provider-chain equality remains unmeasured.
2. MO2's Skyrim game-support plugin and any enabled third-party
   `IPluginFileMapper` need exact inventory and test cases. This report traces
   the core extension point but does not prove every mapper.
3. Windows reparse points, junctions, case aliases, inaccessible paths,
   hardlinks, long paths, and concurrent changes need explicit fixtures.
4. Archive activation/load precedence is not fully resolved here.
5. The exact serialized grammar/error policy for every profile file needs
   contract fixtures, especially malformed/duplicate lines and MO2
   normalization behavior.
6. MO2 2.5.2 is the locally relevant version, not a permanent support promise.
   A future MO2 release requires a new adapter or conformance evidence.
7. The checked source exposes no supported atomic external snapshot API. It is
   possible that an upstream plugin/interface not included in the core source
   could improve the oracle path, but no such product-ready boundary was
   established.
8. USVFS runtime anomalies may cause a hooked game/tool to see something that
   differs from the intended mapping. Static preflight should classify this as
   runtime/integration behavior, not revise the deterministic provider truth
   without evidence.

### Unsupported cases until follow-up

- authoritative snapshot while MO2 is running;
- unknown MO2 or game-plugin version;
- enabled unknown file-mapper contribution;
- archived provider semantics not covered by the accepted RQ-004 route;
- an inaccessible or changing provider population;
- Wine/Proton, non-Windows filesystems, other managers, or other runtimes;
- process-specific output/write effects and custom runtime instrumentation.

## 9. Recommendation

Confidence: **High** for selecting deterministic reconstruction as the only
currently eligible M1 production candidate and for rejecting direct USVFS
necessity; **Medium** for complete surface conformance pending controlled tests.

ADR-0008 accepts these decisions:

1. Support exact MO2 versions through explicit versioned adapters, beginning
   with the locally installed 2.5.2 only if the full Wave B evidence accepts
   that pin.
2. Require MO2 to be closed and one profile to be explicitly selected for an
   authoritative snapshot.
3. Reconstruct resolved mod state and complete provider chains from validated
   instance/profile/configuration inputs plus physical Data/mod/overwrite
   inventories.
4. Retain raw profile/config observations separately from version-modeled
   derived state.
5. Treat unknown versions, mappers, inaccessible paths, archive semantics, and
   mid-capture drift as explicit coverage gaps or capture failure.
6. Use MO2 only as a conformance oracle on disposable controlled instances;
   do not launch a helper through the user's real MO2 as the snapshot
   mechanism.
7. Do not bundle, load, connect to, or directly operate USVFS for M1. Reopen
   only after a demonstrated required surface cannot be reconstructed and a
   separate authority/security ADR accepts the operational risk.
8. Preserve a capability matrix so a future accepted extension can add a
   mapper/archive/runtime surface without reclassifying earlier unsupported
   results.

Qualification gates after ADR acceptance and before implementation/support
claims:

- controlled synthetic MO2 fixtures prove the exact 2.5.2 reconciliation and
  provider order;
- the fixture oracle and its allowed writes are documented;
- RQ-003/RQ-004 identify the pinned game/runtime/plugin/archive dependencies;
- RQ-014 supplies measured capture and invalidation behavior;
- EVAL-0051 and EVAL-0046 receive reviewed specifications; and
- the Wave B integration review confirms every M1-exercised local surface has
  an authority path.

## 10. Exact downstream work enabled

### Proposed ADR

Create a proposed **MO2 profile and effective-state acquisition ADR** selecting
version-pinned quiescent deterministic reconstruction and excluding
real-instance MO2 execution/direct USVFS from M1.

### Proposed EVAL-0051 specification inputs

Use synthetic atomic profiles first, then small controlled real-mod profiles.
At minimum cover:

- two loose providers with both priority orders;
- disabled and missing profile entries;
- a newly discovered mod directory absent from `modlist.txt`;
- Data/unmanaged provider versus enabled mod;
- overwrite as the final provider;
- `.mohidden` and configured custom skip suffix/directory;
- duplicate case-insensitive paths and names;
- an inaccessible, changed-during-capture, and malformed provider;
- plugin file winner correlated with `plugins.txt`/`loadorder.txt`;
- archive disabled/enabled, archive/archive, and loose/archive cases once
  RQ-004 supplies semantics;
- secondary Data and known mapper contributions;
- unknown mapper and unsupported version abstention; and
- renamed mods and unrelated reorderings to enforce anti-overfitting.

Expected outputs include the full ordered provider chain, winner, source class,
dependencies, gaps, and a byte-stable normalized manifest. Compare against an
MO2 in-process view or VFS observer only inside a disposable instance. Record
every MO2-written file before/after; those writes are evaluation-oracle effects,
not production authority.

### Proposed EVAL-0046 specification inputs

- prove the production reconstruction performs no writes under profile, mods,
  overwrite, game Data/root, documents/configuration, or generated-output
  roots;
- prove refusal while MO2 is running;
- prove every allowed product-temp/cache write resolves outside protected
  roots;
- prove no USVFS process, shared controller, custom MO2 plugin, or MO2 child is
  started;
- prove changed-input capture invalidates rather than blends state; and
- separately enumerate the disposable oracle's expected writes.

### Follow-up research

- RQ-002: determine disk suggestion semantics without weakening explicit
  selection.
- RQ-003: pin the runtime and exact MO2 Skyrim game-plugin inputs.
- RQ-004: validate plugin/archive/string/override semantics against
  parser-independent fixture expectations.
- RQ-007: inventory mod metadata, foreign/manual/FOMOD state, and identity
  limits.
- RQ-014: benchmark provider-manifest/fingerprint strategies and dependency
  invalidation.
- Wave E: select protected-root/path authorization and process/data-query
  mechanisms.

## 11. Suggested RQ-001 disposition

Accepted registry status:

> **Resolved for M0 by ADR-0008; implementation conformance pending.** Use version-pinned deterministic
> reconstruction against a quiescent explicitly selected profile, with MO2 as
> a disposable-fixture conformance oracle. Real-instance bounded execution is
> not non-mutating, and direct USVFS has no demonstrated M1 necessity. Exact
> archive/game-plugin/mapper and snapshot-validity dependencies remain with
> ADR-0009, ADR-0008, and ADR-0010. EVAL-0051 remains unexecuted.

## 12. Requirements and evidence traceability

| Requirement / decision | Evidence in this investigation | Result and downstream use |
|---|---|---|
| SCOPE-002, SCOPE-003 | MO2 2.5.2 profile/active-mod source and explicit-selection capture boundary in §§3, 5.3, and 5.12 | Enables one concrete MO2 adapter; no cross-manager abstraction |
| SCOPE-005 | Effective-state surface and authority matrix in §§5.2–5.8 and M1 subset in §5.13 | Every exercised local surface must name an authority route or gap |
| AUTH-001 through AUTH-003 | MO2 run and USVFS side effects in §§4.3, 5.9, 5.11, and 6 | Rejects real-instance process observation/direct USVFS; informs EVAL-0046 |
| SNAP-001, SNAP-002 | Quiescent double-checked capture in §§4.2 and 5.12 | Snapshot binds exact control/provider dependencies and invalidates drift |
| SNAP-003, SNAP-004 | Version/mapper/archive gap model in §5.8 | Unknown or changed inputs cannot silently reuse prior results |
| EVID-002 | Immutable source commits, local executable hash, and artifact procedure in §§3–4 | Adapter result can retain exact MO2/source/probe provenance |
| COVER-001 through COVER-003 | Unsupported/gap conditions in §§5.8 and 8 | Unavailable archives/mappers/runtime behavior remain visible |
| TOOL-001 through TOOL-003 | User-installed version/hash validation and rejected arbitrary-USVFS loading in §§3, 5.11, and 9 | Enables explicit available/missing/unsupported/misconfigured capability state |
| ANALYSIS-005, ANALYSIS-006 | Full loose/archive provider chains and M1 facegen implications in §§5.4–5.5 and 5.13 | Supplies deterministic inputs without treating raw conflict as a finding |
| OPS-001, OPS-004 | Offline reconstruction design and bounded creator-profile aggregate observation in §§4.2 and 5.4 | Production path can be local/offline; scale correctness still belongs to RQ-014/RQ-027 |
| ADR-0001 | Typed observed versus version-modeled/conformance/runtime evidence in §5.8 | No LLM or guessed state becomes a winner |
| ADR-0002 | Immutable snapshot dependencies and quiescent capture in §§5.8 and 5.12 | Prevents mixed physical states and false carryover |
| ADR-0003 | Side-effect preflight and rejection of MO2/USVFS launches in §§4.3, 5.9, and 5.11 | Preserves read-only authority through M4 |
| ADR-0004 | Exact MO2 2.5.2/Windows scope and fail-closed versioning in §§3 and 9 | Avoids unsupported best-effort manager/runtime behavior |
| ADR-0006 | Reconstruction/MO2-execution/USVFS comparison in §§5.4, 5.9, 5.11, and 7 | Fulfills the required necessity comparison; direct USVFS criterion is not met |
| EVAL-0051 | Proposed positive/negative/boundary oracle matrix in §10 | Supplies research prerequisites, not a passing result |
| EVAL-0046 | Proposed no-write and oracle-side-effect checks in §10 | Separates production non-mutation from disposable oracle writes |
| Wave B Gate | Exact M1 surface list and remaining dependencies in §5.13 | Establishes a defensible route but explicitly does not claim the full gate is met |
