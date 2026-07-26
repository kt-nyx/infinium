# RESEARCH-0009 — LOOT integration and data contract

Status: Completed — recommendation accepted by ADR-0011
Date: 2026-07-25
Last reviewed: 2026-07-25
Researcher: Codex agent
Primary question: RQ-005
M0 wave: B
Decision enabled: LOOT integration ADR and the LOOT inputs to EVAL-0053 and
EVAL-0046

Accepted disposition:
[ADR-0011](../../architecture/decisions/ADR-0011-loot-semantic-and-managed-data-boundary.md)
rejects current LOOT application automation and accepts a narrow pinned
libloot `0.29.6` plus managed-data boundary when a milestone claims
LOOT-backed coverage. The binding/worker and EVAL-0053/EVAL-0046 gates remain
unqualified.

## 1. Question and accepted authority

Can supported invocation of the user-installed LOOT application provide
structured, deterministic, non-mutating evidence for Infinium? If it cannot,
which required capabilities justify a pinned bundled libloot dependency, and
how must the masterlist, prelude, userlist, configuration, and resulting
diagnostics be managed?

The answer is constrained by:

- [AUTH-001 through AUTH-003](../../product/requirements.md#authority-and-safety):
  Infinium is read-only through M4, may write only to product-controlled or
  explicitly authorized locations, and may invoke only researched external
  operations whose side effects preserve that authority;
- [TOOL-001 through TOOL-003](../../product/requirements.md#external-tool-environment):
  LOOT the application remains user-installed, with detection, override, version
  validation, and visible capability degradation;
- [ANALYSIS-002](../../product/requirements.md#analysis-002--established-tools):
  deterministic tool evidence must retain the exact inputs, configuration,
  diagnostics, and tool identity needed for inspection and replay;
- [EVID-003](../../product/requirements.md#evid-003--evidence-hierarchy):
  upstream curated metadata and private user metadata must not collapse into
  one indistinguishable authority;
- [SCOPE-004](../../product/requirements.md#scope-004--manual-initiation) and
  [DOC-009](../../product/requirements.md#doc-009--freshness-policy): network
  refresh and analysis remain manually initiated and their freshness choices
  are versioned run inputs;
- [ADR-0003](../../architecture/decisions/ADR-0003-read-only-authority.md):
  protected MO2, mod, game, and profile state may be read but not changed; and
- [ADR-0006](../../architecture/decisions/ADR-0006-gpl-product-and-tool-dependency-boundary.md):
  test the user-installed application first, but permit a pinned bundled
  libloot dependency if that application boundary is insufficient.

This investigation originally provided a recommendation rather than an
architecture decision. ADR-0011 subsequently accepted the conditional
libloot/data boundary stated above; its exact binding, operation allowlist, and
conformance remain qualification work.

## 2. Scope and non-scope

In scope:

- supported LOOT application interfaces and their output, failure, update, and
  write behavior;
- the behavior of the locally relevant LOOT 0.28.0 source and current upstream
  LOOT 0.29.1 source;
- libloot 0.29.6 metadata, plugin, condition, and sorting capabilities;
- the boundary between LOOT semantics and Infinium's independently derived
  diagnostics;
- exact handling of curated masterlist/prelude data and private userlist data;
- implications for selected-MO2-profile fidelity, EVAL-0053, EVAL-0046, and
  Gate B.

Out of scope:

- running LOOT against any real MO2 instance or profile;
- treating the private `Brain Blast Destruction 2024` profile as a fixture,
  benchmark, or representative modlist;
- accepting a libloot version, stack, worker boundary, or ADR;
- reproducing all LOOT application UI diagnostics;
- designing M1 implementation code; and
- bundling or modifying the user-installed LOOT application.

No real profile was needed. The only executable experiment used synthetic
temporary inputs and upstream test plugins.

## 3. Decision criteria and evidence classes

The candidate boundary must:

1. consume the exact effective state of one explicitly selected MO2 profile;
2. produce typed or otherwise stable structured results;
3. avoid changing protected setup state;
4. make every product-owned cache, temporary, and network side effect explicit;
5. pin the engine and all data inputs needed for deterministic replay;
6. preserve curated, private-user, observed-local, and Infinium-derived
   authorities separately;
7. expose parse, condition, plugin, sorting, and input failures without relying
   on GUI text or undocumented process-exit behavior; and
8. support synthetic atomic fixtures and small disposable real-mod profiles
   without production exceptions for any particular modlist.

Terms used below:

- **Verified source fact:** directly established in official source or
  documentation at the pinned revision.
- **Experiment observation:** reproduced by the disposable probe in section 5.
- **Interpretation:** the consequence for Infinium under its accepted
  requirements.
- **Recommendation:** proposed action requiring later review or ADR acceptance.

## 4. Sources and exact versions

Retrieved 2026-07-25 unless stated otherwise.

| Source | Exact version or revision | Authority and relevance |
|---|---|---|
| [LOOT repository](https://github.com/loot/loot/tree/77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9) | Release `0.29.1`, commit `77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9` | Official application source. Establishes current supported arguments, initialization, settings, update, validation, and write behavior. |
| [LOOT command-line initialization source](https://github.com/loot/loot/blob/77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9/src/gui/qt/main.cpp) and [initialization documentation](https://loot.readthedocs.io/en/stable/app/usage/initialisation.html) | LOOT `0.29.1` source and current official documentation | Defines `--game`, `--game-path`, `--loot-data-path`, and `--auto-sort`; no headless diagnostic/export command is documented or registered. |
| [Official LOOT FAQ](https://loot.github.io/docs/help/loot-faqs/) | Unversioned current official page, retrieved 2026-07-25 | States that running LOOT through Mod Organizer is unsupported. This supports rejecting that route as a product integration contract; pinned application source remains the authority for executable behavior. |
| [LOOT 0.28.0 source](https://github.com/loot/loot/tree/e36befdde385c19a9fbdb2855ff3dec16f9363db) | Release `0.28.0`, commit `e36befdde385c19a9fbdb2855ff3dec16f9363db` | Official source corresponding to the locally detected version. Its relevant CLI and initialization behavior match the 0.29.1 boundary. |
| [LOOT validation implementation](https://github.com/loot/loot/blob/77f3ba98966819fd6d92d97dcb2dbc4c1b9fb9b9/src/gui/state/game/validation.cpp) | LOOT `0.29.1` | Shows that several user-facing validity diagnostics are application-layer logic, not a libloot public diagnostic result. |
| [libloot repository](https://github.com/loot/libloot/tree/136f3983c3eec7d377f83a7e7e0b0129aa5c8fe1) | Release `0.29.6`, commit `136f3983c3eec7d377f83a7e7e0b0129aa5c8fe1`, GPL-3.0-or-later | Official library source and API. Establishes metadata parsing/merging, condition evaluation, plugin access, and non-applying sorting. |
| [libloot API introduction](https://loot-api.readthedocs.io/en/latest/api/introduction.html) and [sorting documentation](https://loot-api.readthedocs.io/en/latest/api/sorting.html) | Documentation for the maintained libloot API; source behavior checked against `0.29.6` | Supports the public API interpretation; the pinned source, not an unversioned rendered page, is the reproducibility authority. |
| [LOOT Skyrim SE masterlist](https://github.com/loot/skyrimse/tree/4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f) | Compatibility branch `v0.29`, research commit `4c0c9c31fcb8994f1f21d16a47629e6d8fcb3e7f`, CC0 | Curated Skyrim SE metadata candidate. `v0.29` is a moving branch, so the commit and content hash are required identities. |
| [LOOT prelude](https://github.com/loot/prelude/tree/ea316265c11b5c6e6f51d53deb34c4054f4c2349) | Compatibility branch `v0.29`, research commit `ea316265c11b5c6e6f51d53deb34c4054f4c2349`, CC0 | Shared metadata definitions loaded with the masterlist. Must be resolved, validated, and activated as part of the same compatible input set. |
| [loot/testing-plugins](https://github.com/loot/testing-plugins/tree/d9b28a2062a12f3a40bc9b13be9d6f5731c81249) | Release `1.6.2`, commit `d9b28a2062a12f3a40bc9b13be9d6f5731c81249`, MIT | Official, redistributable plugin fixtures used only in the disposable sorting probe. |
| [RESEARCH-0002](RESEARCH-0002-helper-tool-licensing.md) | Completed Wave A investigation | Establishes the accepted licensing and packaging options; it does not prove technical suitability or no-write behavior. |
| [RESEARCH-0005](RESEARCH-0005-mo2-effective-state-acquisition.md) | Completed Wave B investigation input | Establishes why launching a process through the real MO2/USVFS route is not an eligible read-only acquisition path and defines the effective-state reconstruction dependency. |
| Local reference manifest | LOOT application `0.28.0`, executable SHA-256 `C993642493FBA7ACE99F8D212BA6DA768EA994C4058291286BF7B4938537CB35` | Identifies the installed version relevant to this machine. The application was not executed; a detected local binary is not automatically an accepted supported version. |

The official repositories and pinned source revisions are primary evidence.
Rendered “latest” documentation is supporting evidence only where the pinned
source confirms its claims.

## 5. Experiments and artifacts

### 5.1 Source-surface inspection

Exact tagged source was cloned into a temporary, untracked investigation
directory. The application argument registration, initialization, state
construction, settings persistence, update tasks, close handling, validation
logic, and libloot public APIs were traced from the revisions in section 4.
Nothing was written to the repository or any MO2, mod, game, or profile root by
this inspection.

### 5.2 Why the real LOOT application was not invoked

Preflight source inspection found no supported command that both produces
analysis evidence and avoids application initialization or load-order
application. `--auto-sort` applies the sorted order. Ordinary launch enters the
GUI lifecycle and creates or updates application-owned files. Running it
through real MO2 would additionally cross the rejected MO2/USVFS process
boundary identified by RESEARCH-0005.

Consequently, a real-profile invocation could not answer RQ-005 without
knowingly exercising disallowed or irrelevant behavior. Per the research
safety rules, it was not run.

### 5.3 Disposable libloot probe

Environment:

- Windows PowerShell;
- `cargo 1.92.0`;
- `rustc 1.92.0`;
- libloot source at commit
  `136f3983c3eec7d377f83a7e7e0b0129aa5c8fe1`;
- testing-plugins source at commit
  `d9b28a2062a12f3a40bc9b13be9d6f5731c81249`.

The untracked probe used a path dependency on the exact libloot checkout and
created:

- a fake Skyrim SE root and `Data` directory;
- synthetic masterlist, prelude, userlist, and `plugins.txt` inputs;
- one `file("Present.esp")` condition;
- an invalid YAML input; and
- two upstream test plugins supplied in reverse dependency order.

Sanitized artifact manifest:

| Artifact set | Contents | Retention and handling |
|---|---|---|
| Synthetic fixture | `invalid.yaml`, `masterlist.yaml`, `prelude.yaml`, `userlist.yaml`, `game/SkyrimSE.exe`, `game/Data/Present.esp`, and `local/plugins.txt` | Seven generated non-user inputs in a temporary investigation directory; only their procedure and result are retained here |
| Upstream sorting fixture | `SkyrimSE/Data/Blank.esm` and `SkyrimSE/Data/Blank - Master Dependent.esp` from testing-plugins `1.6.2` | Temporary exact upstream checkout under the MIT license; repository revision retained in section 4 |
| Probe/build artifacts | One temporary Cargo package, lockfile, downloaded dependency cache, and build outputs | Untracked and outside the repository; not a production implementation |

The synthetic YAML and plugin-state inputs were:

```yaml
# prelude.yaml
- &commonMessage
  type: say
  content: 'Prelude general message'

# masterlist.yaml
prelude:
  - *commonMessage
plugins:
  - name: 'Present.esp'
    msg:
      - type: say
        content: 'Curated message'
        condition: 'file("Present.esp")'

# userlist.yaml
plugins:
  - name: 'Present.esp'
    msg:
      - type: warn
        content: 'User message'

# invalid.yaml
plugins:
  - name: [
```

`local/plugins.txt` contained `*Present.esp`; `SkyrimSE.exe` and
`Present.esp` were one-byte presence placeholders. The Cargo package depended
on libloot through `libloot = { path = "../libloot-0.29.6" }`.

It then:

1. constructed `Game::with_local_path(GameType::SkyrimSE, ...)`;
2. called `load_masterlist_with_prelude(...)` and `load_userlist(...)`;
3. called `plugin_metadata(...)` with
   `MergeMode::WithoutUserMetadata` and `MergeMode::WithUserMetadata`, then
   called `plugin_user_metadata(...)` separately;
4. evaluated `file("Present.esp")`;
5. called `load_masterlist(...)` on invalid YAML and captured the returned
   error; and
6. called `load_plugins(...)` and `sort_plugins(...)` on the reversed plugin
   pair without calling `set_load_order(...)`.

The three probe modes are reproducible from that temporary Cargo package with:

```text
cargo run --quiet -- metadata <synthetic-root>
cargo run --quiet -- invalid <synthetic-root>
cargo run --quiet -- sort <synthetic-root> <testing-plugins-skyrimse-root>
```

Observed output:

```text
curated_messages=1 merged_messages=2 user_messages=1 condition_true=true
missing_fixture_inputs=0
invalid_metadata_error=failed to parse the file at "<temporary-path>/invalid.yaml"
sorted=Blank.esm|Blank - Master Dependent.esp
EXITS=metadata:0 invalid:0 sort:0
FIXTURE_UNCHANGED=True
PLUGIN_FIXTURE_UNCHANGED=True
```

Before/after SHA-256 manifests for all seven synthetic fixture inputs and the
upstream plugin-fixture tree were equal. Writes were limited to the temporary
probe source, Cargo dependency/build caches, and build outputs. The probe was
not retained as a production artifact because it was a narrow research
instrument; the exact source revisions, inputs, operations, outputs, and side
effects needed to reproduce its result are recorded here.

This observation demonstrates the named libloot operations on a disposable
physical view. It does **not** prove correct selected-MO2-profile condition
evaluation or application-diagnostic parity.

## 6. Findings

### 6.1 Recommended answer

**No supported user-installed LOOT application invocation satisfies the
required contract.** LOOT 0.28.0 and 0.29.1 expose a GUI application plus an
`--auto-sort` convenience path, not a headless structured analysis/export
surface. `--auto-sort` may refresh managed data and applies the sorted load
order. Ordinary startup has application-data and settings side effects but
still provides no stable typed output.

This establishes ADR-0006's technical trigger for considering a pinned bundled
libloot dependency. libloot adds necessary read-oriented primitives that the
application boundary does not expose: explicit metadata inputs, separate
curated/merged/user metadata retrieval, condition evaluation, plugin access,
typed errors, and a sorting function that returns a proposed order without
applying it.

The fallback is not sufficient by itself. Infinium still needs a proven bridge
from its reconstructed selected-MO2-profile state to libloot's physical game
and load-order expectations, plus an explicit boundary for any diagnostics
derived by Infinium rather than emitted by LOOT.

### 6.2 Capability and gap matrix

| Required capability | User-installed LOOT application | Pinned bundled libloot | Direct managed-data parsing | No LOOT/deferred |
|---|---|---|---|---|
| Supported headless analysis | **Gap.** No documented or registered command. | **Available as library calls**, subject to worker/interface choice. | Parser must be implemented and maintained by Infinium. | Unavailable. |
| Stable structured output | **Gap.** GUI/log/clipboard are not a supported data contract. | Typed objects and errors are available. | Infinium owns a typed contract but risks semantic drift. | Unavailable. |
| Read proposed sort without applying | **Gap.** `--auto-sort` applies. | `sort_plugins` returns an order and documents that it does not apply it. | Reimplementing LOOT sorting is out of scope and high risk. | Unavailable. |
| Explicit masterlist/prelude/userlist inputs | Application uses its settings/data paths, with behavior coupled to its lifecycle. | Explicit database load calls are available. | Fully explicit, but every merge/condition rule becomes Infinium's responsibility. | Unavailable. |
| Preserve curated vs private-user metadata | Not exposed as a structured result. | Curated, merged, and user-only access is available. | Possible if designed correctly. | Unavailable. |
| LOOT condition evaluation | No structured callable result. | Available, but depends on an accurate physical/effective-state view. | Reimplementation would duplicate a mature semantic engine. | Unavailable. |
| Plugin parsing and dependency-aware sorting | Not exposed without GUI/apply path. | Available and reproduced on an official atomic fixture. | Significant reimplementation. | Unavailable. |
| Full LOOT application validity diagnostics | Present in UI application logic but not exported. | **Partial gap.** Public primitives exist, but the application validation result is not a libloot API. | Would be Infinium-derived, not LOOT output. | Unavailable. |
| Selected MO2 profile fidelity | Direct launch sees physical state; MO2 launch crosses a disallowed process/write route. | **Unproven bridge required.** libloot expects physical game/data/local paths. | Same effective-state reconstruction need for local conditions. | No LOOT-backed capability. |
| Protected-state non-mutation | Not established for a useful invocation; `--auto-sort` fails it. | Achievable by an allowlisted read-only adapter and isolated staging, but not yet accepted. | Achievable in product code, with semantic cost. | Preserved by omission. |
| Replay with exact engine/data identity | Application version can be recorded, but hidden/UI state and moving files frustrate exact replay. | Engine version/commit and every input hash can be pinned. | Parser version and data can be pinned. | No evidence to replay. |

### 6.3 Application side-effect and output matrix

| Application operation | Structured evidence | Observed or source-established effects | AUTH result |
|---|---|---|---|
| `--help` or `--version` | Version/help only; no analysis value | Early process exit is expected, but it cannot answer the RQ | Safe but irrelevant |
| Ordinary GUI launch | No supported machine-readable analysis output | Creates/uses LOOT data directories, recreates the debug log, initializes per-game state, may migrate/copy application data, may check for application updates, and persists settings/UI state on close | Does not provide an approvable analysis operation |
| `--auto-sort` | No supported structured export | May update prelude/masterlist according to settings, calculates a sort, **applies the load order**, and exits | Rejected by AUTH-001/AUTH-003 |
| Direct launch outside MO2 | GUI only | Reads the physical game installation rather than the selected profile's effective VFS | Semantically invalid for the required subject |
| Launch through the real MO2 instance | GUI only | Can see USVFS state, but MO2's launch path can save/normalize profile state and gives the child a write route | Rejected by RESEARCH-0005 and AUTH-001/AUTH-003 |
| GUI launch with an isolated `--loot-data-path` | Still no supported structured result | Can redirect LOOT-owned data writes, but cannot fix physical-state fidelity or produce the required evidence contract | Insufficient, even if its LOOT-data writes are isolated |

The application catches several operational failures and presents them through
GUI state/messages. No documented analysis-specific exit-code contract or
structured failure envelope exists. Infinium therefore must not treat process
exit, log scraping, clipboard export, or UI automation as a stable diagnostic
API. Independently, LOOT's current official FAQ calls running LOOT through Mod
Organizer unsupported, so that route also lacks an upstream support contract.

### 6.4 libloot side-effect and authority boundary

| libloot operation class | Behavior at 0.29.6 | Proposed adapter rule |
|---|---|---|
| Load plugin headers/plugins | Reads explicit plugin files and supporting game state | Allow only paths in a validated immutable snapshot or isolated staged view |
| Load masterlist/prelude/userlist | Reads caller-selected paths and parses metadata | Require exact source identity and SHA-256 before use |
| Retrieve/evaluate metadata | Produces in-memory results; condition evaluation reads referenced local state | Bind the evaluator to the exact reconstructed effective view |
| Sort plugins | Returns a proposed order; does not apply it | Allow, record input order and complete result; never call an apply operation |
| Load current load-order state | Reads conventional game-local load-order state | Do not point it at live LocalAppData; use only a proven run-owned representation if required |
| Set load order | Mutating operation | Exclude from the production interface and test that it is unreachable |
| Write user metadata/minimal lists | Explicit file-writing operations | Exclude from the production interface and test that they are unreachable |
| Internal build/runtime cache | Cargo/build writes occurred only in the research temp directory | Distribution/runtime cache behavior must be remeasured for the chosen binding/worker |

The strongest implementation boundary is an Infinium-owned narrow adapter that
exposes only allowlisted read/compute operations. It must not offer a generic
libloot escape hatch.

### 6.5 Selected-MO2-profile fidelity remains a dependency

libloot understands game roots, `Data`, additional data paths, plugins, and
conventional load-order state; it does not understand MO2 mod priority,
profile-local saves/configuration, overwrite, provider chains, hidden files, or
USVFS routing.

Passing absolute effective plugin paths may be sufficient for plugin parsing,
but is not sufficient evidence for every metadata condition. Conditions can
depend on file presence, version, checksum, active state, archives, and paths
resolved relative to the game or data view. Infinium must therefore either:

1. construct an isolated, run-owned physical projection whose winners and
   relevant state exactly represent the selected profile; or
2. prove an alternative adapter that supplies equivalent facts without
   changing libloot semantics.

That bridge depends on RESEARCH-0005/RQ-001 and the later effective-file-state
work. It must be validated with synthetic atomic conflicts and small disposable
real-mod profiles. The user's last real profile may be useful only as a private
format/scale reference and cannot establish correctness or representativeness.

### 6.6 LOOT diagnostics versus Infinium-derived diagnostics

The LOOT application performs missing/inactive-master, metadata requirement and
incompatibility, plugin validity/type/header, group, Bash-tag, dirty-plugin, and
plugin-count checks in application-layer validation code. libloot exposes many
underlying plugin and metadata facts, but not one public result that represents
the complete application validation pass.

Therefore:

- results returned by libloot itself may be labeled **LOOT/libloot-derived**;
- checks Infinium computes from libloot and local facts must be labeled
  **Infinium-derived using LOOT metadata**, with their algorithm revision;
- Infinium must not claim parity with LOOT application diagnostics until
  cross-version conformance fixtures demonstrate it;
- recreating the entire LOOT validation layer would contradict the intent not
  to rebuild LOOT; and
- if exact application diagnostics are required, the preferred long-term
  route is an upstream reusable API or a narrowly justified parity-tested
  subset, not UI/log scraping.

### 6.7 Managed-data acquisition and replay contract

The default proposed contract is:

1. **Select a compatibility line.** Bind the chosen libloot/adapter version to
   supported masterlist and prelude compatibility branches such as `v0.29`.
2. **Resolve immutable revisions.** Treat branch names as discovery aliases,
   never replay identities. Record repository, branch/ref, exact commit, raw
   URL, retrieval time, and redirect/result status.
3. **Hash exact bytes.** Record SHA-256, size, license classification, and
   parser/adapter identity for masterlist and prelude.
4. **Validate as a pair.** Download into an Infinium-owned staging location,
   parse the masterlist with its selected prelude, and reject the candidate
   pair on transport, identity, syntax, or compatibility failure.
5. **Activate atomically.** Only after validation, publish one immutable
   acquisition manifest referring to both files. Retain the prior known-good
   pair; never partially replace an active pair.
6. **Bind runs immutably.** A run references the exact acquisition manifest.
   Refresh creates a new acquisition/input identity and never changes a
   running or historical analysis.
7. **Support explicit offline reuse.** Use an exact cached valid pair when the
   user chooses reuse; show its age and revisions. If no valid pair exists,
   report the LOOT-backed capability unavailable rather than silently consume
   moving application-owned data.
8. **Keep refresh manual.** Refresh occurs only inside an explicit
   user-initiated acquisition/scan action and is separately cancellable and
   observable. No profile change triggers it.

An optional “use this installed LOOT data/configuration” mode may later read a
user-selected local masterlist/prelude pair, but it must snapshot and hash the
exact bytes without updating them. A custom source is user-configured evidence,
not automatically LOOT-curated authority.

### 6.8 Userlist and configuration contract

The userlist is private user-maintained configuration. Infinium must:

- discover or accept its path only through an authorized LOOT integration
  setup;
- copy its exact bytes into run-owned evidence storage or otherwise retain an
  immutable content-addressed snapshot under the accepted retention policy;
- record absence as an explicit input, not as an error or an empty inferred
  file;
- hash the bytes, record the original logical source and acquisition time, and
  redact private paths in normal exports;
- never modify, normalize, or publish the source userlist;
- expose userlist metadata separately from upstream masterlist/prelude
  metadata, even when libloot also returns a merged view; and
- include all condition-relevant configuration and the selected effective-state
  snapshot identity in the run manifest.

LOOT application settings that select custom sources or alter update behavior
are user configuration, not curated metadata. If Infinium provides fidelity to
those settings, their exact relevant values and source paths must be captured
and labeled separately from Infinium's own defaults.

## 7. Alternatives

### A. Invoke user-installed LOOT

Rejected for the analysis boundary. It preserves the desired user-installed
application relationship but fails structured-output, non-applying,
machine-readable failure, and selected-profile safety criteria. Isolating its
data directory does not repair those gaps.

LOOT may still be detected and linked as a user-operated companion tool, and a
future newly supported upstream headless API could reopen this decision.
Because bundled libloot is a library dependency rather than operation of the
LOOT application, the ADR must state whether the installed application's
availability gates anything beyond application-config/userlist fidelity. This
report finds no technical reason to make an installed LOOT executable a
prerequisite for Infinium-managed libloot analysis.

### B. Bundle a pinned libloot

Recommended candidate. It supplies the narrow mature semantics Infinium needs
without applying load order and is compatible in principle with the accepted
GPLv3-family project boundary. Costs are a GPL/transitive-source release
obligation, a native/library integration surface, strict version support, and
the unresolved effective-state bridge.

### C. Parse managed YAML directly and implement conditions/sorting

Rejected as the default. Reading YAML is technically possible and can keep
inputs explicit, but LOOT metadata semantics, condition evaluation, plugin
logic, merge rules, and sorting would become an Infinium maintenance burden.
This would recreate exactly the mature deterministic layer the product intends
to reuse. A tiny independent parser may still be appropriate for manifest
inspection, never as a substitute for claimed LOOT semantics.

### D. Use an installed LOOT private library/DLL

Rejected. It is neither a supported application interface nor a stable
deployment/version contract. Loading an arbitrary DLL from a user installation
also widens the trust and ABI boundary while retaining GPL/library integration
obligations.

### E. Defer LOOT-backed analysis

Valid if M1 deliberately excludes LOOT. Infinium can still develop snapshot,
evidence, case, and other deterministic capabilities, while showing LOOT-backed
coverage as unavailable. It is not a solution for any milestone that claims
LOOT metadata or sorting coverage.

## 8. Uncertainty, contrary evidence, and limitations

- No real LOOT process was run. This is intentional: source inspection already
  showed that no useful supported command met the preflight. EVAL-0046 still
  needs a disposable application oracle if a future LOOT release adds a
  candidate interface.
- The local executable is 0.28.0, while the leading researched revisions are
  LOOT 0.29.1 and libloot 0.29.6. Source parity on the relevant application
  boundary was checked, but local installation does not establish the version
  Infinium should support.
- The synthetic probe covers metadata authority separation, one true file
  condition, invalid YAML, and a two-plugin dependency sort. It does not cover
  all condition forms, groups, archives, ghosted plugins, light/medium plugin
  behavior, Unicode/path edge cases, or malformed real plugins.
- No selected-MO2-profile projection has yet been proven. Until it is, libloot
  condition results that depend on effective files are not production-ready.
- The complete LOOT application validation pass is not a public libloot result.
  The appropriate subset of Infinium-derived checks remains a product and
  evaluation decision.
- Managed-data retrieval, atomic activation, offline cache behavior, and
  runtime native-library side effects are source-backed designs, not yet
  conformance-tested implementation behavior.
- Upstream repositories, compatibility branches, APIs, and licenses can change.
  Release work must re-audit the exact selected revisions and dependencies.
- This report does not infer general behavior from the user's private real
  profile and does not claim the atomic probe represents high-scale modlists.

## 9. Recommendation

### Recommendation

ADR-0011 accepts a LOOT boundary that:

1. rejects user-installed LOOT application invocation as the Infinium analysis
   boundary for LOOT 0.28.0/0.29.1;
2. retains the application as user-installed and never managed or bundled;
3. selects a narrow, pinned, bundled libloot adapter as the recommended LOOT
   semantic boundary, conditional on the preconditions below;
4. excludes every libloot write/apply operation from the adapter;
5. labels libloot results, user metadata, and Infinium-derived diagnostics as
   distinct authorities;
6. adopts the immutable, validated masterlist/prelude acquisition contract in
   section 6.7 and the private userlist contract in section 6.8; and
7. permits LOOT deferral if M1 does not claim LOOT-backed coverage.

Confidence is **high** that the current LOOT application boundary is
insufficient, based on two pinned application releases and official source.
Confidence is **medium-high** that libloot is the correct semantic dependency,
based on its public API and the disposable probe. Confidence is only
**medium-low** that the complete selected-MO2-profile integration is ready,
because the physical effective-state projection and application-diagnostic
subset are not yet proven.

### Qualification gates before implementation or LOOT-backed coverage

- choose and audit the exact libloot release, language binding, native payloads,
  transitive licenses, corresponding-source process, and supported platform
  matrix;
- define the narrow worker/adapter API and demonstrate that set/write/apply
  operations are unreachable;
- complete RQ-001 for an exact selected-profile effective-state view and
  RQ-014 for its fingerprint and reuse validity;
- specify which results are direct libloot output and which are
  Infinium-derived;
- create parity and boundary fixtures across supported LOOT/libloot/data
  revisions;
- approve acquisition, cache, integrity, offline, and stale-data behavior;
- pass EVAL-0053 and EVAL-0046 successor specifications; and
- require a new RQ/ADR review if a later LOOT application exposes a supported
  headless structured interface.

Rejection criteria for libloot are: inability to represent exact effective
state without protected writes; unbounded native/ABI support cost; inability to
separate input authorities; unacceptable GPL/transitive release obligations;
or failure to reproduce required semantics across the supported revision set.

## 10. ADR and follow-up work enabled

### Proposed LOOT integration ADR

The ADR should decide:

- application rejection for the analyzed versions and reopening criteria;
- exact libloot version/binding/worker boundary;
- allowlisted operations and forbidden symbols/actions;
- supported LOOT data compatibility line;
- effective-state projection contract and dependency;
- direct-libloot versus Infinium-derived diagnostic taxonomy;
- masterlist/prelude acquisition, validation, atomic cache, offline, and
  rollback rules;
- userlist/settings capture and privacy rules;
- whether installed-application detection gates only optional
  application-config/userlist fidelity or any libloot-backed capability;
- capability degradation when data, LOOT, or effective-state inputs are
  missing/unsupported; and
- version upgrade and conformance policy.

### Proposed EVAL-0053 inputs

For every LOOT-backed run, assert that the replay manifest contains:

- libloot adapter name, semantic version, source commit, binary hash, binding,
  and platform;
- selected-profile snapshot/effective-view identity;
- input plugin list and order plus content/header identities needed by the
  operation;
- masterlist and prelude repository, compatibility ref, exact commit, URL,
  retrieved-at time, SHA-256, size, parse result, and acquisition manifest;
- userlist present/absent state, exact hash and immutable snapshot identity;
- all relevant LOOT/Infinium configuration and condition context;
- direct curated metadata, direct user metadata, merged metadata, and
  Infinium-derived diagnostic authority kept distinguishable;
- complete structured results and typed failures;
- deterministic replay against the same bytes; and
- a negative test proving a moving branch, changed userlist, changed condition
  input, or changed effective winner produces a new input identity rather than
  reusing the old result.

### Proposed EVAL-0046 inputs

For each allowed adapter operation:

1. construct a disposable synthetic MO2-like subject with sentinel hashes for
   game, mod, profile, base-directory, local-state, and input files;
2. snapshot process, network, file, directory, registry, and product-cache
   state as applicable;
3. execute only the allowlisted operation against an isolated effective view;
4. verify every protected sentinel and directory manifest is unchanged;
5. enumerate all created/modified product cache, temporary, build/runtime, and
   network artifacts;
6. test path-alias/reparse and LocalAppData escape rejection;
7. prove write/apply APIs cannot be reached through the adapter;
8. cancel/fail during parsing, conditions, and sorting and repeat the
   invariants; and
9. reserve real user installations for read-only format/scale observation, not
   mutation testing or correctness truth.

### Other follow-ups

- Update [the source registry](../source-registry.md) after review with the
  selected LOOT/libloot revisions, official application-interface sources, and
  the managed-data acquisition authority.
- Carry the effective-state requirement into RQ-001 and its fingerprint/reuse
  requirement into RQ-014.
- Create a bounded follow-up or ADR subsection for the supported
  Infinium-derived subset of LOOT application validity checks.
- Add synthetic fixtures for false/unknown conditions, malformed metadata,
  group cycles, archive/file conditions, ghosting, custom sources, absent and
  changing userlists, stale/offline acquisition, and masterlist/prelude
  incompatibility.

### Gate B implication

RQ-005's application question is answered and the fallback trigger is
established. This report alone did **not** establish Gate B: selected-profile
effective-state fidelity and operation-specific non-mutation remain shared
dependencies. If M1 excludes LOOT, RQ-005 may be carried to the first
LOOT-delivery milestone without blocking M1. If M1 includes LOOT, ADR-0011's
accepted boundary and reviewed EVAL-0053/EVAL-0046 specifications must be
implemented and passed before that support claim can be accepted.

## 11. Suggested RQ-005 status

Accepted registry status:

> **Resolved for M0 by ADR-0011; delivery remains conditional.** Supported LOOT 0.28.0/0.29.1
> application invocation cannot produce the required structured,
> non-mutating selected-profile evidence. A pinned bundled libloot adapter is
> the accepted semantic route when LOOT coverage is delivered, subject to the
> effective-state bridge, exact data-acquisition contract, and EVAL-0053 /
> EVAL-0046 conformance. LOOT may instead be deferred if M1 claims no
> LOOT-backed coverage.

## 12. Requirements and evidence traceability

| Requirement, decision, or gate | Evidence in this report | Result or enabled work |
|---|---|---|
| AUTH-001 through AUTH-003; ADR-0003 | Sections 5.2, 6.3, and 6.4 | Rejects real-profile application/MO2 invocation; defines a narrow no-write libloot boundary and EVAL-0046 inputs |
| TOOL-001 through TOOL-003 | Sections 4, 6.1, and 7 | Keeps LOOT user-installed while distinguishing the conditional bundled library |
| ANALYSIS-002 | Sections 6.2, 6.6, 6.7, and 6.8 | Defines exact engine/data/config/result identities and diagnostic authority |
| EVID-003 | Disposable metadata probe and sections 6.6–6.8 | Curated, user, merged, local, and derived evidence remain distinguishable |
| SCOPE-004; DOC-009 | Section 6.7 | Manual refresh, immutable acquisitions, explicit offline/stale behavior |
| ADR-0006 | Sections 6.1, 7, and 9 | Establishes the conditional fallback trigger without accepting a dependency |
| EVAL-0053 | Section 10 | Supplies research inputs for the future reproducibility/fidelity case specification |
| EVAL-0046 | Sections 5.2, 6.3, 6.4, and 10 | Supplies application rejection evidence and research inputs for the future adapter-safety case specification |
| RQ-001 / RQ-014 dependencies | Sections 6.5, 8, and 9 | Makes selected-profile effective-state projection and its fingerprint/reuse validity explicit preconditions |
| M0 Gate B | Section 10 | Conditional: RQ-005 is answerable, but shared fidelity/non-mutation dependencies remain |

The central conclusion is intentionally narrow: current supported LOOT
application invocation is not an acceptable Infinium analysis interface;
libloot is justified for further selection because it exposes needed
non-applying structured semantics; and neither path is production-ready until
the selected MO2 profile's exact effective state and all permitted side effects
are proven.
