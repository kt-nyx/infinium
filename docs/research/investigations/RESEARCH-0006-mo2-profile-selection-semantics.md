# RESEARCH-0006: MO2 profile-selection semantics

Status: Completed
Disposition: recommendation accepted by ADR-0008
Date: 2026-07-25

Last reviewed: 2026-07-25

Researcher: Codex agent

Primary question: RQ-002

M0 wave: B

Decision enabled: Profile-suggestion and explicit analysis-target behavior

Accepted disposition:
[ADR-0008](../../architecture/decisions/ADR-0008-mo2-profile-effective-state-and-local-identity.md)
accepts **MO2 saved selection** as a suggestion only, with explicit profile
selection and snapshot binding remaining authoritative.

## 1. Question and accepted constraints

### Primary question

Does Mod Organizer 2 expose a current or last-selected profile reliably enough
for Infinium to use it?

The answer depends on the intended use:

- **Yes, conditionally, as a per-instance saved-selection suggestion.** MO2
  2.5.2 persists a `General/selected_profile` value in the selected instance's
  `ModOrganizer.ini`, and its own source calls this the "last profile."
- **No, as an authoritative current-profile oracle or automatic analysis
  target.** The value has no use timestamp, may be buffered while MO2 is
  running, can survive a rename/deletion performed outside MO2, can reflect a
  command-line override, and is ambiguous until the MO2 instance is resolved.

This distinction is consistent with the accepted product contract:

- [SCOPE-003](../../product/requirements.md#scope-003--profile-target) requires
  every analysis to target one explicitly selected profile.
- [SCOPE-004](../../product/requirements.md#scope-004--manual-initiation)
  requires manual initiation.
- [AUTH-001 through AUTH-003](../../product/requirements.md#authority-and-safety)
  prohibit using a mutating MO2 launch as a discovery mechanism.
- [SNAP-001 and SNAP-002](../../product/requirements.md#snapshot-and-reproducibility)
  require immutable target binding and changed-input detection.
- [TOOL-001 through TOOL-003](../../product/requirements.md#external-tool-environment)
  require validated user-installed MO2 discovery and explicit capability
  reporting.
- [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md)
  requires immutable run bindings.
- [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md)
  excludes setup mutation through M4.
- [ADR-0004](../../architecture/decisions/ADR-0004-initial-target-scope.md)
  selects one concrete Windows/MO2 target.
- [ADR-0006](../../architecture/decisions/ADR-0006-gpl-product-and-tool-dependency-boundary.md)
  keeps MO2 user-installed and leaves the exact profile contract to RQ-001 and
  RQ-002.

[RESEARCH-0005](RESEARCH-0005-mo2-effective-state-acquisition.md) already
recommends quiescent, version-pinned deterministic reconstruction and explicit
profile selection. This report determines what role MO2's saved selection may
play before that binding is created.

## 2. Scope and non-scope

### In scope

- MO2 2.5.2's exact persisted profile-selection value;
- when and how MO2 reads, changes, validates, and saves that value;
- portable and global-instance placement;
- `-p`/`--profile` command-line overrides;
- missing, empty, stale, renamed, deleted, and case-mismatched profiles;
- clean shutdown, abnormal termination, configuration drift, and multiple MO2
  processes;
- safe use as a UI/CLI suggestion;
- the required explicit per-analysis target binding and failure behavior;
- compatibility evidence from current upstream MO2 source.

### Explicitly out of scope

- accepting the complete MO2 effective-state adapter;
- launching MO2, installing an MO2 plugin, or querying live in-process state;
- changing any profile or instance;
- claiming that the creator's real profile is correct or representative;
- defining RQ-014's snapshot fingerprint algorithm;
- supporting other managers, games, platforms, or MO2 versions;
- production parser or UI implementation.

No legacy implementation was used as evidence.

### Investigation preflight

- Local private access: exact read-only process, registry, instance-config, and
  profile-control-file observations only.
- Network access: public official MO2 and Qt sources only.
- Authenticated APIs, credentials, paid providers, and LLM calls: none.
- External-tool execution: none; MO2 startup was excluded because its
  documented setup/refresh path writes state.
- Research artifacts: official source clones/diffs in OS temp; sanitized
  fingerprints and aggregate observations in this report.
- Stop condition: any required MO2 launch, protected-root write, unknown
  executable side effect, or unavailable primary source.

## 3. Sources and exact versions

All sources were retrieved or verified on 2026-07-25.

| Source | Exact version or revision | Authority and claim supported |
|---|---|---|
| [MO2 2.5.2 `settings.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/settings.cpp#L655-L667) and [`settings.h`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/settings.h#L79-L83) | Tag `v2.5.2`, commit `9c130cbf2fc7225fb2916e46419af50671772aa0` | Primary implementation: the current-profile name is stored as UTF-8 `QByteArray` at `General/selected_profile`. |
| [MO2 2.5.2 `instancemanager.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/instancemanager.cpp#L113-L193) | Same commit | Primary implementation: instance setup reads its INI, obtains the profile, then writes resolved instance values back. |
| [MO2 2.5.2 `getProfile`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/instancemanager.cpp#L314-L331) | Same commit | Primary implementation: constructor/CLI override wins; otherwise `selected_profile` is used as "last profile"; otherwise MO2 uses its default profile name. |
| [MO2 2.5.2 instance resolution](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/instancemanager.cpp#L514-L599) and [global settings](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/settings.cpp#L2433-L2448) | Same commit | Primary implementation: global current-instance state is separate from each instance's INI; portable and global instances resolve to different configuration roots. |
| [MO2 2.5.2 `setCurrentProfile`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/organizercore.cpp#L531-L603) | Same commit | Primary implementation: actual profile activation validates directories case-insensitively, recovers from a missing name by selecting an available profile, and saves the canonical activated name. |
| [MO2 2.5.2 settings sync](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/organizercore.cpp#L175-L203) | Same commit | Primary implementation: `storeSettings()` writes the current profile and explicitly synchronizes `QSettings`. |
| [MO2 2.5.2 profile UI](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/mainwindow.cpp#L1704-L1805) | Same commit | Primary implementation: profile-box changes activate the selected profile; invalid indices use the last displayed entry; activation performs refresh work. |
| [MO2 2.5.2 command-line options](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/commandline.cpp#L307-L321) and [`main.cpp`](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/main.cpp#L81-L107) | Same commit | Primary implementation: `-p`/`--profile` overrides the saved profile when a process starts. |
| [MO2 2.5.2 forwarded-command check](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/moapplication.cpp#L396-L431) | Same commit | Primary implementation: a command forwarded to an already-running primary process does not switch it to a different requested profile; a mismatch is rejected. |
| [MO2 2.5.2 multiple-process warning](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/commandline.cpp#L432-L451) | Same commit | Primary statement embedded in MO2's own CLI help: `--multiple` is unsupported and may create "weird problems," especially when processes manage the same game instance. |
| [MO2 2.5.2 profile management](https://github.com/ModOrganizer2/modorganizer/blob/9c130cbf2fc7225fb2916e46419af50671772aa0/src/profilesdialog.cpp#L231-L312) | Same commit | Primary implementation: MO2's own UI prevents deleting or renaming the active profile, but this cannot protect against external filesystem changes. |
| [Qt 6.7 `QSettings`](https://doc.qt.io/qt-6.7/qsettings.html) | Qt 6.7 documentation; MO2 2.5.2 release uses Qt 6.7.1 | Primary framework contract: `setValue()` may not reach permanent storage immediately; `sync()` commits and imports changes; periodic event-loop and destructor synchronization occur; INI access from multiple processes uses advisory locking and merging under stated filesystem conditions. |
| [MO2 releases](https://github.com/ModOrganizer2/modorganizer/releases/tag/v2.5.2) | Latest published release `v2.5.2`; release commit above | Primary release identity and Qt dependency version. |
| [MO2 current source](https://github.com/ModOrganizer2/modorganizer/tree/efe2a02d5dc641946baaa8db1440800f38d07837) | `master` commit `efe2a02d5dc641946baaa8db1440800f38d07837`, authored 2026-07-08 | Current compatibility check: the same key, precedence, fallback, command-line option, and activation behavior remain present. This unreleased source is not a supported-version selection. |

The current-source comparison was made from the exact remote `HEAD` returned
by `git ls-remote`; no branch-name assumption was used as a version identity.

## 4. Experiments and artifact handling

### 4.1 Source trace and current-source comparison

The official MO2 repository was inspected in an OS temporary workspace at:

- the local executable's matching `v2.5.2` commit; and
- the exact current upstream `HEAD` listed in section 3.

Reproducible commands:

```powershell
git ls-remote https://github.com/ModOrganizer2/modorganizer.git `
  HEAD refs/heads/master refs/tags/v2.5.2
git show -s --format='%H %cI %s' 9c130cbf2fc7225fb2916e46419af50671772aa0
git show -s --format='%H %cI %s' efe2a02d5dc641946baaa8db1440800f38d07837
git grep -n 'selectedProfileName\|selected_profile' 9c130cbf2fc7225fb2916e46419af50671772aa0
git diff 9c130cbf2fc7225fb2916e46419af50671772aa0..efe2a02d5dc641946baaa8db1440800f38d07837 `
  -- src/instancemanager.cpp src/settings.cpp src/organizercore.cpp `
     src/main.cpp src/commandline.cpp src/moapplication.cpp
```

Observed side effects:

- network read from the public official repository;
- disposable files only under the OS temporary directory;
- no executable invocation and no protected-setup write.

No clone or diff artifact is retained in the repository.

### 4.2 Read-only local reference observation

Preconditions:

- `ModOrganizer.exe` was not running;
- the project owner had explicitly selected `Brain Blast Destruction 2024` and
  had already removed the obsolete `test profile`;
- all reads used exact known files rather than wildcard selection.

Observed:

| Observation | Result |
|---|---|
| Local executable | MO2 `2.5.2`; SHA-256 matches the [Wave B manifest](WAVE-B-reference-environment-manifest.md) |
| Instance mode resolution | The global `CurrentInstance` value was empty and the canonical portable INI existed, matching MO2's portable fallback path |
| Saved selection | Canonical INI contained `selected_profile=@ByteArray(Brain Blast Destruction 2024)` |
| Canonical INI stability | SHA-256 remained `5C5DDD77F83C45E986AAEB17CE08ADB69C3A70760357C293D0F1CFBCFFA7E27F` before and after the observation |
| Profile resolution | Exactly one directory currently existed under the configured profiles root, with the same canonical name |
| Profile fingerprints | The six reference-profile fingerprints matched the Wave B manifest before and after the observation |
| Configuration artifact boundary | A historical sibling whose name began `ModOrganizer.ini.` also existed; it was not treated as the canonical INI |

The local portable **instance configuration root** and its configured
`base_directory`/profiles root are distinct concepts. On this installation the
canonical instance INI is beside the MO2 executable while the profiles live
under the configured base directory. An adapter must not equate
`base_directory` with the location of `ModOrganizer.ini`.

The local observation establishes one valid simple shape only. It does not
prove portable/global behavior generally, correctness of the profile, normal
profile counts, or cross-version compatibility.

Observed side effects:

- file, directory, process-list, and current-user registry reads only;
- no MO2, Skyrim, LOOT, or helper process launched;
- no file opened for write;
- no artifact containing raw profile contents retained.

### 4.3 Boundary analysis

Boundary outcomes were derived from the exact source paths in section 3 and
evaluated against the fail-closed product contract. MO2 was not launched
against synthetic variants because startup itself writes instance settings and
may refresh profile state. Controlled black-box fixtures remain proposed work,
not completed evidence.

| Boundary | MO2 behavior or limitation | Safe Infinium behavior |
|---|---|---|
| Valid key and one case-insensitive directory match | MO2 activates that directory and saves its on-disk capitalization | Offer the canonical directory as a suggestion; still require confirmation |
| Key absent | MO2 substitutes its default profile name during instance setup | Show no inferred-last-profile suggestion; list discovered profiles and require selection without silently preferring `Default` |
| Key empty | It is not a valid profile directory; later activation enters missing-profile recovery | Treat as no valid suggestion |
| Key names a deleted/missing profile | MO2 creates a default profile if necessary, chooses an available directory, reports the substitution, and saves the replacement | Do not imitate selection-by-first-entry or create anything; show a stale hint and require selection |
| Profile externally renamed | Persisted key remains stale until MO2 or another writer repairs it | Do not infer rename continuity; require selection of the discovered profile |
| Case-only mismatch | MO2 scans directories case-insensitively and records actual case | Resolve uniquely and retain exact on-disk name; ambiguity fails closed |
| INI malformed/unreadable | Instance setup can fail or enter recovery | Report instance/profile suggestion unavailable; do not parse best-effort into an authoritative target |
| `-p` supplied at new-process startup | Override wins over the INI and is written during setup/activation | Recognize that the saved value may reflect an override; never launch MO2 to discover or set it |
| `-p` forwarded to an existing primary with another profile | MO2 rejects the mismatch instead of switching the live process | Do not interpret command-line text as proof the live process changed profile |
| MO2 crashes or is terminated | `QSettings` may have persisted a recent value through periodic sync, or may still contain the prior saved value | Call the value "saved selection," not "last actually used"; require explicit confirmation |
| Several instances | Each instance has its own INI/key; global current-instance state is separate | Resolve/confirm the instance first; never compare bare profile names globally |
| Multiple processes for one instance | MO2 explicitly calls this unsupported; separate processes may hold different in-memory profiles and synchronize the same INI | Refuse authoritative capture while any relevant MO2 process is running; do not claim one current profile |
| Key changes after user confirmation | Discovery input drift; may indicate another writer or setup change | Keep the confirmed target immutable for the pending run, but invalidate/restart capture when an accepted snapshot dependency changed |

## 5. Findings

### 5.1 What the value actually means

Verified from MO2 2.5.2 source:

1. `General/selected_profile` is stored in each instance's
   `ModOrganizer.ini` as a UTF-8 `QByteArray`.
2. If a constructor/command-line profile override exists, it takes precedence.
3. Otherwise MO2 reads `selected_profile` and comments that it is using the
   "last profile."
4. If the key is missing, MO2 uses its configured default profile name.
5. Instance setup writes its resolved override/saved/default name back before
   `OrganizerCore` performs profile-directory activation and existence
   recovery.
6. During actual activation, MO2 resolves a directory case-insensitively,
   recovers from a missing name, and saves the canonical activated profile
   name.
7. On explicit settings storage, MO2 writes the current in-memory name and
   calls `sync()`.

Therefore the strongest defensible interpretation is:

> the last profile name that this MO2 instance successfully persisted as its
> selection, subject to override, recovery, write timing, and external edits.

It is not:

- the last profile used to launch Skyrim;
- the most recently played profile;
- a profile-use history;
- an atomic live-process state export;
- a globally current profile across MO2 instances;
- an Infinium analysis authorization or immutable run binding.

The key has no timestamp, selection provenance, process identity, or use event.
Selecting a profile in MO2 can change it even if no game/tool is launched.

### 5.2 Persistence and abnormal termination

`setCurrentProfile()` updates the `QSettings` object when activation succeeds.
MO2 later performs an explicit `sync()` in `storeSettings()`, including normal
profile-save/teardown paths. Qt also synchronizes periodically and from the
`QSettings` destructor.

That is adequate for normal preference persistence, but not a crash-proof
external-state protocol:

- `setValue()` is allowed to buffer before permanent storage;
- an abnormal termination can leave the prior saved value;
- an external reader cannot determine whether the disk value is behind the
  live in-memory selection;
- a valid persisted value does not prove that Skyrim was ever launched from
  that profile.

No evidence was found of a journaled selection event, monotonic revision, or
stable headless API that upgrades this preference into an authoritative
current-state record.

Because instance setup can persist the candidate name before profile
activation validates it, an interrupted or failed startup also cannot be
assumed to have completed MO2's missing-profile recovery.

### 5.3 Portable and global instances

The profile key has the same semantics after an instance is resolved, but the
instance-resolution path differs:

- a portable instance uses `ModOrganizer.ini` in the MO2 application/portable
  instance directory;
- a global instance uses `ModOrganizer.ini` in its per-instance local-data
  directory;
- MO2's global `CurrentInstance` setting is separate and stored through native
  `QSettings` on Windows;
- `-i`/`--instance`, portable-mode rules, and instance selection can override
  or replace the global-current-instance choice.

The selected-profile value is consequently meaningful only as:

```text
resolved MO2 installation
  + resolved instance identity/config root
  + that instance's exact canonical INI
  + decoded selected_profile value
```

A bare profile name is not globally unique. An adapter that finds several INI
files and picks the newest, or applies one instance's key to another
instance's profile root, would fabricate selection state.

### 5.4 Command-line profile selection

`-p`/`--profile` is an input to MO2 startup, not a query API. For a new process,
it overrides the saved key and flows through instance setup and actual profile
activation, which can persist it as the new saved selection.

If another primary process already exists, the command may be forwarded. MO2
checks that the requested profile equals the running process's current profile;
if not, it reports an error and does not switch the running process.

Infinium must not:

- inspect a shortcut's `-p` and conclude that MO2 currently uses it;
- invoke MO2 with `-p` to establish analysis scope;
- treat `-p` as a read-only operation;
- assume an invalid override remains the final activated profile after MO2's
  recovery logic.

An explicitly supplied **Infinium** profile argument is different: it is user
selection for Infinium and can establish the requested target after validation
without launching MO2.

### 5.5 Stale, missing, renamed, and deleted profiles

MO2's missing-profile recovery is appropriate for an interactive mod manager,
but not for an evidence-bound analyzer. It may create a default profile and
then select an available profile. Copying that behavior would both mutate the
setup and silently change the requested analytical target.

Infinium should instead classify the hint:

- **valid:** one canonical directory match exists;
- **stale:** a non-empty decoded name has no match;
- **unavailable:** the key is absent, empty, unreadable, or cannot be decoded
  under the supported MO2/Qt contract;
- **ambiguous:** more than one supported candidate matches after Windows path
  normalization or instance resolution is not unique.

Only `valid` is eligible for suggestion. All four states still require one
explicit selected target before analysis.

An external rename has no proved logical-profile identity. Until a separate
identity design exists, Infinium must not transfer assumptions, history, or a
pending target solely because the new directory resembles the old profile.

### 5.6 Multiple processes

MO2 normally uses a primary-process/forwarding model. Its own help calls
`--multiple` unsupported and warns against processes managing the same game
instance.

Qt's INI locking and merge behavior protects against some file corruption; it
does not make two MO2 processes share one current-profile concept. Each
process can have a different in-memory profile while writing the same
`selected_profile` key. The last synchronized value does not identify both
processes or prove which process launched a tool.

This reinforces RESEARCH-0005's quiescent-capture precondition: a relevant MO2
process must not be running when Infinium creates an authoritative
installation snapshot.

### 5.7 Parser and artifact boundary

The local INI renders the value as `@ByteArray(...)`, but that textual spelling
is Qt serialization, not the product contract. A production adapter must use a
version-tested Qt-compatible INI/value decoder or an equivalently proven
parser. A regex that strips `@ByteArray(` and `)` is not adequate evidence for
escaped bytes, Unicode, malformed values, or future format changes.

The adapter must open the exact canonical `ModOrganizer.ini`. Similar
historical, atomic-save, backup, or temporary siblings are not candidates
merely because their names start with `ModOrganizer.ini`.

## 6. Safe product contract

### 6.1 Discovery and suggestion

Proposed discovery behavior:

1. Detect supported MO2 installations and instances through the accepted
   instance-discovery contract.
2. Ask the user to confirm or choose one instance when more than one supported
   candidate exists.
3. Resolve that instance's canonical INI and configured profiles root.
4. Decode `General/selected_profile` under the exact supported adapter version.
5. Enumerate profile directories read-only and resolve one unique canonical
   match.
6. If valid, preselect or badge it as **MO2 saved selection**.
7. If invalid, show the precise stale/unavailable/ambiguous reason and do not
   choose a fallback silently.

The UI should not call it "currently running," "last played," "last launched,"
or "known active."

If MO2 is running, Infinium may display the saved value only as a possibly
stale hint and must require closure plus refreshed discovery before
authoritative capture.

### 6.2 Explicit per-analysis binding

Before creating a run, the user must explicitly confirm one profile. The
selection event should resolve at least:

```text
MO2 installation identity and supported adapter version
instance identity and canonical configuration root
configured profiles-root identity
canonical profile directory name and resolved path identity
selection origin: explicit-user-selection
optional suggestion provenance: selected_profile observation fingerprint
selection time
```

The saved MO2 value is suggestion provenance, not target authority.

At snapshot capture, Infinium revalidates:

- MO2 is closed;
- the chosen instance/profile still resolves uniquely;
- relevant instance/profile control files and path inputs match the capture
  dependencies;
- no reparse/path authorization boundary has changed;
- the profile's snapshot inputs are captured and rechecked according to the
  RQ-014 strategy.

The analysis run binds to the resulting installation snapshot, not to a live
preference key or mutable UI selection. Changing the selection later creates a
different run target.

### 6.3 CLI behavior

The M1 human-readable CLI does not need automatic suggestion. A safe initial
contract may require explicit instance and profile arguments, then print the
resolved target and request/record confirmation according to the accepted M1
plan.

A bare profile name is acceptable only after one instance is explicitly
resolved. An explicit profile path can be a CLI input, but it must be resolved
and validated as contained within the chosen configured profiles root; an
arbitrary absolute path is not automatically an MO2 profile.

### 6.4 Failure behavior

| Failure | Required result |
|---|---|
| Instance unresolved or ambiguous | No profile suggestion or run; require instance selection |
| Unsupported MO2/adapter version | Show unsupported capability; no best-effort authoritative binding |
| Missing/malformed canonical INI | Profiles may be listed only if another accepted route establishes their root; selection hint unavailable |
| Saved name stale | Show stale saved selection; require another profile |
| Profile directory missing after confirmation | Abort or invalidate capture; do not substitute |
| MO2 running | No authoritative snapshot; ask user to close it and refresh |
| Multiple relevant MO2 processes | No authoritative snapshot; do not choose a process/key winner |
| Configuration/profile drift during capture | Invalidate/retry affected capture under SNAP-002 |

## 7. Realistic alternatives

| Alternative | Strength | Material problem | Proposed disposition |
|---|---|---|---|
| Automatically bind to `selected_profile` | Lowest-friction startup | Preference timing, instance ambiguity, stale names, CLI overrides, no user authorization, violates SCOPE-003 | Reject as target authority |
| Use validated key as a preselected suggestion, then require confirmation | Preserves convenience and explicit scope | Requires versioned decoder and clear stale-state UI | **Recommend** |
| Always require manual instance/profile selection and ignore the key | Simplest authority model; ideal for M1 CLI | Avoidable recurring friction in later UI | Valid initial behavior; retain as fallback |
| Require an explicit instance plus profile name | Human-readable; maps to MO2 terminology | Name must be canonicalized and is not stable across rename | Recommend as UI/CLI request, followed by resolved path/snapshot binding |
| Require an explicit profile path | Unambiguous request on one machine | User-hostile; path alone does not prove instance ownership and requires containment/reparse validation | Advanced CLI/settings override only |
| Read live in-process `IOrganizer::profileName()` via a plugin | True current in-memory profile for that process | Requires privileged MO2 plugin installation/execution and still does not create an immutable snapshot | Reject for production through M4; disposable oracle only |
| Launch MO2 with `-p` | Makes MO2 activate a requested profile | Writes settings/profile state, initializes plugins/VFS, may recover to another profile; unnecessary mutation | Reject |
| Infer from the newest profile file or directory timestamp | No Qt parsing | Timestamps do not encode selection/use and may change for unrelated reasons | Reject |
| Infer from most recent game/save/log activity | Could approximate last played profile | Separate provenance problem; logs/saves may be shared or historical and cannot authorize preflight target | Reject |

### Rejection criteria for the recommended suggestion path

Disable the suggestion capability for an adapter/version if:

- the exact instance cannot be resolved;
- the value cannot be decoded with a conformance-tested parser;
- a unique canonical profile match cannot be established;
- upstream changes the key or precedence semantics;
- the only way to obtain the value is to launch or modify MO2; or
- evaluation shows users routinely mistake the hint for an authoritative
  current profile despite labeling and confirmation.

Explicit manual selection remains valid even if suggestion is disabled.

## 8. Contrary evidence, uncertainty, and unsupported cases

### Contrary or narrowing evidence

- MO2's source itself calls the persisted value the "last profile," supporting
  use as a hint.
- MO2 also updates the value during activation and explicitly syncs settings,
  so treating it as arbitrary noise would be too conservative.
- Qt supports multi-process INI locking/merging, reducing corruption risk.
  That is not evidence of a single cross-process current-profile state.
- MO2 prevents active-profile rename/deletion through its own UI, reducing one
  stale-key path. External edits, failed writes, copied instances, and
  unsupported multiple processes remain possible.

### Material uncertainties

1. No MO2 executable was launched against disposable profiles because startup
   has documented settings/profile side effects. Boundary behavior is traced
   from source rather than observed as a black-box execution.
2. Exact Qt `QByteArray` INI decoding cases for escaped characters, malformed
   values, Unicode normalization, and case-sensitive Windows directories still
   require conformance fixtures.
3. Global-instance registry redirection, portable-lock behavior, copied
   instances, and unusual Windows known-folder policies need controlled
   fixtures.
4. The exact process-detection rule for determining which running MO2 process
   is relevant belongs to the integration/security design.
5. A clean OS shutdown, process kill, crash, power loss, and failed atomic INI
   replacement can produce different persistence timing. The recommendation
   deliberately does not require distinguishing them.
6. Current upstream source preserves the behavior, but unreleased `master`
   does not establish support for a future release.

### Unsupported until follow-up

- treating any running MO2 process's profile as authoritative from disk;
- automatic binding from a saved key;
- profile-history or last-game-launch claims;
- carrying profile identity through an external rename;
- parsing unknown MO2/Qt formats best-effort;
- choosing among several instances from file timestamps;
- concurrent same-instance MO2 capture;
- non-Windows filesystems, Wine/Proton, and other mod managers.

## 9. Recommendation

Confidence:

- **High** that MO2 2.5.2 exposes a useful per-instance saved-selection hint;
- **High** that it is insufficient as an automatic analysis target;
- **Medium-high** that the proposed fail-closed suggestion contract covers the
  material known boundaries, pending parser and controlled-instance fixtures.

Recommend that the proposed MO2 integration ADR state:

1. `General/selected_profile` may be consumed only from the exact resolved,
   supported instance's canonical `ModOrganizer.ini`.
2. Its semantic label is **MO2 saved selection**.
3. It may preselect or suggest one uniquely matched canonical profile.
4. It never satisfies SCOPE-003 by itself; every analysis records explicit
   user selection.
5. Instance resolution precedes profile suggestion.
6. Missing, empty, stale, malformed, or ambiguous values produce no automatic
   fallback.
7. MO2's first-directory/default-profile recovery is not reproduced by
   Infinium.
8. `-p` is recognized as selection input to MO2, not a query or a safe
   Infinium integration.
9. No authoritative snapshot begins while MO2 is running.
10. The run binds to the validated installation snapshot; the saved key is
    retained only as discovery/suggestion provenance where applicable.

Preconditions:

- the MO2 integration ADR accepts the versioned instance-discovery and
  quiescent-capture boundary;
- controlled fixtures validate portable/global resolution and Qt value
  decoding;
- RQ-014 defines the configuration/profile drift check;
- the UI and CLI keep suggestion, explicit selection, and snapshot binding
  visibly distinct.

RQ-002 does not block M1 if the suggestion feature is deferred. M1 can require
explicit instance/profile arguments and still satisfy the accepted product
requirements.

### Wave B gate implication

This report neither establishes nor blocks Gate B by itself. Explicit
instance/profile selection removes any dependency on a reliable automatic
suggestion for M1. Gate B still depends on RQ-001's effective-state
conformance, RQ-004's exercised archive/record semantics, RQ-014's measured
snapshot-validity strategy, and the integrated proof that every M1 local
surface has an authoritative route or explicit gap.

## 10. Exact downstream work enabled

### Proposed ADR input

Fold this report into the proposed **MO2 profile and effective-state
acquisition ADR** enabled by RQ-001. A separate ADR is unnecessary unless the
profile-suggestion behavior later diverges from that integration boundary.

The ADR should distinguish:

```text
instance suggestion/discovery
profile saved-selection suggestion
explicit requested target
validated snapshot target
analysis-run binding
```

### Proposed evaluation work

Add profile-selection cases to
[EVAL-0051](../../evaluation/case-catalog.md#evaluation-case-catalog):

- portable instance with a valid saved profile;
- global instance with a valid saved profile;
- two instances containing the same profile name;
- empty, absent, malformed, and stale key;
- case-only match and ambiguous case-normalized match;
- externally renamed/deleted profile;
- valid `-p`, invalid `-p`, and forwarded-profile mismatch;
- normal close versus killed disposable MO2 process, with exact before/after
  INI evidence;
- unsupported `--multiple` same-instance processes, used only in an isolated
  disposable environment if safe;
- non-ASCII and escaped profile names;
- historical/temporary `ModOrganizer.ini.*` siblings;
- change after confirmation and change during capture;
- assertion that the suggested value never creates a run without explicit
  selection.

Add to [EVAL-0046](../../evaluation/case-catalog.md#evaluation-case-catalog):

- discovery/suggestion performs no setup write or process launch;
- missing/stale selection does not cause profile creation or fallback;
- protected-root fingerprints remain unchanged;
- an MO2-running condition prevents authoritative capture.

### Follow-up research and documentation proposals

- RQ-001: use this suggestion/binding distinction in its integration ADR.
- RQ-014: include the canonical instance INI, configured profile root, and
  chosen profile identity in capture dependencies without using the saved
  selection as run identity.
- Wave E / RQ-032: define canonical path, containment, reparse-point, registry,
  and process-detection authorization.
- Clarify the Wave B manifest vocabulary in coordinator review: for portable
  MO2, distinguish the instance configuration root from its configured
  `base_directory`/profiles root.
- Update the product workflow wording only if desired from "current or most
  recently selected" to the more exact "MO2 saved selection"; this is a
  wording proposal, not an applied product change.

## 11. Suggested RQ-002 status

Accepted registry status:

> **Resolved for M0 by ADR-0008; implementation conformance pending.** MO2 2.5.2 exposes a per-instance saved
> profile selection that is suitable as a validated suggestion only. It does
> not establish the current live profile, last game-launched profile, or
> Infinium's analysis target. Resolve the instance first, require explicit
> per-analysis profile selection, bind the run to the resulting installation
> snapshot, and fail closed on stale, missing, malformed, ambiguous, running,
> or unsupported state.

## 12. Requirements and evidence traceability

| Requirement / decision | Evidence in this investigation | Result and downstream use |
|---|---|---|
| SCOPE-002, ADR-0004 | Exact MO2 2.5.2 and current-source trace in §§3–5 | Version-specific behavior; no generalized manager contract |
| SCOPE-003 | Key is a saved hint without authorization/history in §§5.1 and 6.2 | Explicit user selection remains mandatory |
| SCOPE-004 | Selection suggestion creates no run in §6 | Manual initiation remains separate |
| SCOPE-005 | Instance/profile resolution chain in §§5.3 and 6.2 | Profile identity becomes a snapshot input only after explicit validation |
| AUTH-001 through AUTH-003, ADR-0003 | No-launch experiment boundary and `-p` analysis in §§4 and 5.4 | Reject MO2 launch/plugin as profile discovery; informs EVAL-0046 |
| SNAP-001, ADR-0002 | Explicit resolved binding in §6.2 | Run binds to immutable snapshot, not preference state |
| SNAP-002 | Drift/failure rules in §§4.3 and 6.4 | Changed instance/profile inputs invalidate capture |
| SNAP-003 | Stale-hint semantics in §§5.5 and 6.4 | Stale selection is visible and never silently substituted |
| EVID-002 | Exact source commits, local hashes, and suggestion provenance in §§3–4 and 6.2 | Retain source/config observation without upgrading its authority |
| COVER-001 through COVER-003 | Unavailable/unsupported/ambiguous states in §§5.5 and 6.4 | Missing suggestion or profile authority is an explicit capability gap |
| TOOL-001 through TOOL-003, ADR-0006 | Per-instance user-installed MO2 path in §§5.3 and 6.1 | Detection/override precedes validated profile suggestion |
| EVAL-0051 | Boundary matrix and exact proposed cases in §§4.3 and 10 | Adds profile-selection conformance requirements |
| EVAL-0046 | Read-only/no-launch behavior in §§4 and 10 | Separates discovery from mutating MO2 activation |
| Wave B Gate | Explicit selection is independent of unreliable hint in §§6 and 9 | RQ-002 is not a Gate B blocker; exact capture still depends on RQ-001/RQ-014 |
