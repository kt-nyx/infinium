# RESEARCH-0010: xEdit ground truth and invocation

Status: Completed
Disposition: recommendation rejected and superseded by ADR-0007
Date: 2026-07-25  
Last reviewed: 2026-07-25  
Researcher: Codex agent  
Primary research question: RQ-006  
M0 wave: B — Authoritative local state and deterministic ground truth  
Decision enabled: Historical evidence for the rejected external-tool option

## Subsequent decision and current authority

[ADR-0007](../../architecture/decisions/ADR-0007-exclude-xedit-from-infinium.md)
rejects this report's proposed xEdit integration and oracle roles. Infinium
will not detect, configure, invoke, stage, copy, bundle, download, install,
update, parse output from, or report capability for xEdit. It is not a
product dependency, optional analyzer, development oracle, release gate, or
fixture dependency.

The investigation below is intentionally retained as decision provenance: it
records what was considered and why the project owner rejected the additional
dependency and authority boundary. Its recommendations and proposed
EVAL-0046/EVAL-0052 procedures are not operative. Current record-semantic
qualification is governed by ADR-0007 and uses independently specified
first-party fixtures and review without allowing the Mutagen path under test
to author its own expected truth.

## 1. Question and accepted constraints

RQ-006 asks:

> Which functions of user-installed xEdit provide unique automated or
> ground-truth value, and which detection, version, invocation, cache/temp,
> and failure contract is supported?

The governing requirements and accepted decisions are:

- AUTH-001 through AUTH-003 and ADR-0003 require every approved xEdit
  operation to leave the user's MO2 instance, profile, mods, plugins, game,
  configuration, and generated output unchanged. Tool-owned writes must be
  known, isolated, and recorded.
- TOOL-001 through TOOL-003 and ADR-0006 require xEdit to remain
  user-installed and user-maintained. Infinium must detect or accept its path,
  validate the exact executable and operation, and expose missing,
  unsupported, and misconfigured states without blocking unrelated
  capabilities.
- SCOPE-001 through SCOPE-006 constrain the target to the pinned Skyrim SE
  runtime, one selected MO2 profile, manual initiation, and Windows.
- SCOPE-005 and ANALYSIS-003 require meaningful record semantics and explicit
  gaps rather than a raw conflict dump.
- EVID-001, EVID-002, EVID-006, SNAP-001, SNAP-002, SNAP-005, and SNAP-006
  require typed results, exact tool/input provenance, abstention, drift
  rejection, reproducible configuration, and honest replay disclosure.
- ANALYSIS-002 says to prefer an approved established-tool capability over
  reimplementation when it provides required deterministic value.
- EVAL-0052 requires supported override chains, links, and winners to agree
  with xEdit ground truth. EVAL-0046 requires every invoked external-tool
  operation to prove non-mutation.
- ADR-0001 makes deterministic local observations authoritative for local
  state, but it does not make every xEdit interpretation an infallible
  statement of game behavior.
- RESEARCH-0008 recommends Mutagen.Bethesda.Skyrim `0.54.2` only as a
  provisional semantic parser whose supported fields must pass independent
  xEdit conformance. It does not assign effective-state authority to xEdit.

This investigation therefore distinguishes four different propositions:

1. xEdit is a mature independent parser and valuable human inspection oracle.
2. Some exact xEdit modes can provide bounded automated diagnostics.
3. A first-party xEdit script can technically export structured record data.
4. None of those facts by itself proves that invoking xEdit against the
   user's live setup is safe or appropriate for production.

## 2. Scope and explicit non-scope

### In scope

- current official xEdit release, source revision, documentation, executable
  identity, command-line modes, and script surface;
- field, record, link, override-chain, winner, and structural-error value;
- detection and exact-version validation;
- game, plugin-list, INI, scripts, temp, cache, backup, log, and output path
  controls;
- settings, default logs, reference cache, temp cleanup, update checks, and
  save behavior;
- built-in `CheckForErrors`, `CheckForITM`, and `CheckForDR` modes;
- `View`, `Script`, quick/autoload/conflict, expert, cleaning, and write-mode
  boundaries;
- process exit status, completion evidence, partial output, cancellation, and
  failure normalization;
- a reproducible xEdit-backed EVAL-0052 procedure;
- an operation-specific EVAL-0046 non-mutation procedure;
- comparison with manual oracle, Mutagen-only, automated xEdit, and no-xEdit
  product paths.

### Out of scope

- accepting an integration ADR or changing RQ-006's registry status;
- implementing an xEdit adapter or production script;
- treating xEdit as MO2/provider/VFS authority;
- launching MO2, Skyrim, Steam, SKSE, LOOT, or the user's installed SSEEdit;
- running a tool against `Brain Blast Destruction 2024`;
- editing, cleaning, saving, or generating any real plugin or output;
- accepting every xEdit record definition or conflict color as game-engine
  truth;
- selecting the M1 record-family/field allowlist before RQ-024/RQ-036 and the
  M1 plan;
- approving general subprocess containment, which remains part of RQ-032 and
  the application/process architecture.

`Brain Blast Destruction 2024` was not inspected. It remains a
user-confirmed real profile useful for profile-shape reference only, not a
representative corpus, correctness oracle, or source of production rules.
Atomic synthetic fixtures and small controlled real-mod profiles remain the
evaluation direction.

## 3. Preflight, environment, access, and stopping conditions

### Access and effects

| Surface | Used | Authorization and effect |
|---|---:|---|
| Network | Yes | Read-only access to official xEdit GitHub source, release API, release asset, and official Tome of xEdit pages |
| Local private setup | Yes, bounded | Read executable metadata/hash and the base `Skyrim.esm` identity; did not read profile contents or launch a configured tool |
| External executables | No successful xEdit run | 7-Zip extracted the official release in a disposable temp directory; a staged xEdit process start was blocked by the execution environment before a process or output was created |
| Authenticated API | No | GitHub public endpoints only |
| Paid service or LLM | No | None |
| Repository writes | Yes | This report only |
| Disposable artifacts | Yes | Temporary source clone, release archive/extraction, copied executable, copied private base master, and minimal test configuration |

The local reference identities came from the Wave B environment manifest:

- xEdit display version `4.1.5.0`;
- SHA-256
  `659FADDD8DC061A9D2EDDD20DE925821B87E377284CE179F4538FF78BB2420CD`;
- Skyrim SE runtime `1.6.1170.0`, still a proposed rather than accepted pin.

### Stopping conditions

Research stopped short of tool execution if any of the following applied:

- the operation could address a protected setup path without a disposable
  boundary;
- source review did not account for settings, log, temp, cache, backup, save,
  update-check, and process effects;
- the operation included cleaning, save, master update, ONAM update, direct
  save, or another write mode;
- the expected result depended on an undocumented endpoint or arbitrary shell
  content;
- a fixture required the user's live MO2 VFS;
- a process could not be started under the current research environment.

No credentials, user paths, raw mod names, profile contents, or game bytes are
retained in the repository.

## 4. Sources and exact versions

All web sources were retrieved on 2026-07-25. Source links use the exact
`xedit-4.1.5f` tag/commit where a source-level claim matters.

| Source | Exact identity | Relevance |
|---|---|---|
| [xEdit release 4.1.5f](https://github.com/TES5Edit/TES5Edit/releases/tag/xedit-4.1.5f) | Published 2024-04-28; latest official GitHub release observed on 2026-07-25 | Release status, asset, definition/scripting changes |
| [xEdit source](https://github.com/TES5Edit/TES5Edit/tree/f5c00f3fa3ee39511185515802647246c807f759) | Tag `xedit-4.1.5f`; commit `f5c00f3fa3ee39511185515802647246c807f759` | Exact implementation authority |
| [`Memo1.txt`](https://github.com/TES5Edit/TES5Edit/blob/f5c00f3fa3ee39511185515802647246c807f759/xEdit/Memo1.txt) | Same commit | Shipped command-line and cache reference |
| [`xeInit.pas`](https://github.com/TES5Edit/TES5Edit/blob/f5c00f3fa3ee39511185515802647246c807f759/xEdit/xeInit.pas) | Same commit | Mode selection; explicit paths; cache flags; View/Script/save authority |
| [`xeMainForm.pas`](https://github.com/TES5Edit/TES5Edit/blob/f5c00f3fa3ee39511185515802647246c807f759/xEdit/xeMainForm.pas) | Same commit | settings/log writes, update checks, script/check execution, save behavior, and process exit code |
| [`wbInterface.pas`](https://github.com/TES5Edit/TES5Edit/blob/f5c00f3fa3ee39511185515802647246c807f759/Core/wbInterface.pas) | Same commit | auto-mode and plugin-mode sets |
| [`xEditAPI.pas`](https://github.com/TES5Edit/TES5Edit/blob/f5c00f3fa3ee39511185515802647246c807f759/Build/Edit%20Scripts/xEditAPI.pas) | Same commit and release payload | Record, link, chain, winner, state, and file APIs available to scripts |
| [`JSON - Demo.pas`](https://github.com/TES5Edit/TES5Edit/blob/f5c00f3fa3ee39511185515802647246c807f759/Build/Edit%20Scripts/JSON%20-%20Demo.pas) | Same commit and release payload | Proves xEdit scripts can construct and save JSON |
| [`Check for errors.pas`](https://github.com/TES5Edit/TES5Edit/blob/f5c00f3fa3ee39511185515802647246c807f759/Build/Edit%20Scripts/Check%20for%20errors.pas) | Same commit and release payload | Script-level structural-check example |
| [Official overview and command-line documentation](https://tes5edit.github.io/docs/2-overview.html) | Current rendered documentation, retrieved 2026-07-25 | Modes, paths, conflict/winner model, saving |
| [Official scripting documentation](https://tes5edit.github.io/docs/13-Scripting-Functions.html) | Current rendered documentation, retrieved 2026-07-25 | Script lifecycle and API intent; explicitly incomplete/work in progress |
| [Official error-checking documentation](https://tes5edit.github.io/docs/7-mod-cleaning-and-error-checking.html) | Current rendered documentation, retrieved 2026-07-25 | Scope and limitations of `Check for Errors` |
| [Official 4.x change history](https://tes5edit.github.io/whatsnew.html) | Current rendered documentation, retrieved 2026-07-25 | autoload/autoexit/quick modes, known conflict/check issues |
| [RESEARCH-0002](RESEARCH-0002-helper-tool-licensing.md) | Completed 2026-07-25 | MPL-2.0 and user-installed application posture |
| [RESEARCH-0005](RESEARCH-0005-mo2-effective-state-acquisition.md) | Completed Wave B input | Effective-state and staging boundary |
| [RESEARCH-0007](RESEARCH-0007-skyrim-runtime-support-contract.md) | Completed Wave B input | Candidate runtime and executable-validation contract |
| [RESEARCH-0008](RESEARCH-0008-mutagen-bethesda-semantic-capability.md) | Completed Wave B input | Mutagen capability/gap matrix and EVAL-0052 needs |

The official documentation is valuable but not a complete automation
specification. Source behavior at the exact accepted executable hash must
govern a version-specific adapter.

## 5. Reproducible experiments, artifacts, and side effects

### Experiment A — exact source and release capture

The official repository was cloned at tag `xedit-4.1.5f`:

```text
commit=f5c00f3fa3ee39511185515802647246c807f759
```

The official release asset was downloaded and extracted outside the
repository:

```text
asset=xEdit.4.1.5f.7z
bytes=31,345,110
sha256=54C014DA621F83F06A64FD92DDB8E32ED3082D1C65F543DC1C4E432130DCED08
```

The generic 64-bit TES-family executable from that archive was compared with
the user's configured SSEEdit:

| Property | Official `xTESEdit64.exe` | Local `SSEEdit64.exe` |
|---|---|---|
| Bytes | 36,495,360 | 36,495,360 |
| File/product version | `4.1.5.0` | `4.1.5.0` |
| SHA-256 | `659FAD...E622` | `659FAD...E622` |
| Authenticode | Not signed | Not signed |

The byte-identical match establishes that the local executable is the
official `xedit-4.1.5f` 64-bit TES binary renamed for SSE mode. Version
resources alone do not distinguish the `f` revision, and Authenticode cannot
provide publisher identity. An automated adapter therefore needs the exact
hash, not only `FileVersion`.

### Experiment B — source-level command, write, and exit tracing

The exact source was traced from startup to shutdown:

1. `xeInit.pas` chooses game, tool, and source modes.
2. `DoInitPath` resolves or accepts scripts, temp, Data, output, My Games,
   game/custom INI, saves, plugins list, settings, backup, cache, and custom
   log paths.
3. `FormCreate` creates/opens a settings file, reads tool options, and starts
   the loader.
4. the selected automated or UI mode runs;
5. `FormClose` calls `SaveChanged`, updates the settings file, writes logs,
   removes tool-created temp when applicable, and assigns the process exit
   code.

This trace produced the capability, side-effect, and failure matrices in
section 6.

### Experiment C — release script-surface inspection

The release's `xEditAPI.pas` exposes, among other functions:

- `MasterOrSelf`, `OverrideCount`, `OverrideByIndex`, `WinningOverride`, and
  `IsWinningOverride`;
- `FixedFormID`, `GetLoadOrderFormID`, `Signature`, `GetIsDeleted`, and
  `IsInjected`;
- `LinksTo`, `GetFile`, `GetFileName`, and `GetLoadOrder`;
- recursive element enumeration plus edit/native values and paths;
- `Check` for record-definition errors.

The shipped JSON demo constructs JSON objects and can save a file. A
first-party script can therefore emit a machine-readable oracle artifact.
This is a technically viable custom protocol, not a native stable xEdit
reporting contract.

### Experiment D — attempted disposable checker launch

A private disposable stage was prepared outside protected roots with:

- the official release executable copy;
- a copied base `Skyrim.esm`;
- minimal `plugins.txt`, `Skyrim.ini`, and `SkyrimCustom.ini`;
- separate Data, My Games, saves, scripts, temp, backup, cache, and log paths;
- the cache-disabled `-sse -checkforerrors` operation.

The environment rejected process creation with Windows error 5,
`Access is denied`, before an xEdit process was observed. No xEdit settings,
default log, custom log, cache, temp, backup, or result artifact appeared.
The user's installed SSEEdit was not invoked.

This is an execution-environment blocker, not evidence that xEdit itself
accepted or rejected the command. The source-backed operation contract remains
unexecuted and must be reproduced in a Windows development environment that
permits the exact disposable binary.

### Artifact and retention manifest

| Artifact | Retention | Redistributability |
|---|---|---|
| This report | Committed | Project documentation |
| Exact source/release URLs, commit, and hashes | Committed | Factual provenance |
| Source clone and release extraction | Disposable temp only | Not committed |
| Copied executable | Disposable temp only | Not committed; xEdit remains user-installed |
| Copied `Skyrim.esm` | Private disposable temp only | Never committed or redistributed |
| Attempt configuration and output absence | Summarized here | No private paths or bytes |

Observed research side effects were public network reads, a temporary Git
clone/download/extraction, private temporary copies, and this report. No MO2,
profile, mod, game, or tool-install file was changed by the investigation.

## 6. Findings

### F1 — xEdit's unique value is independent semantic ground truth, not local-state authority

xEdit provides a mature, independently implemented interpretation of Bethesda
records, fields, references, override chains, and winners. Its UI makes the
entire record chain inspectable and its scripting API exposes the necessary
chain/link primitives.

That value is unique relative to using Mutagen to generate and validate the
same fixture. It can expose:

- a wrong FormKey or full/light FormID mapping;
- a missing or extra override;
- a different winning record;
- a link resolving to a different canonical record or failing to resolve;
- a field presence/value discrepancy;
- an xEdit-definition error or unresolved reference that a permissive parser
  accepted.

xEdit does **not** decide which MO2 profile, plugin bytes, provider, archive,
or loose file is effective. RQ-001 supplies those inputs. xEdit can be a
semantic oracle only after it consumes a snapshot-exact staged input.

### F2 — xEdit ground truth is strong but claim-type-specific

xEdit is an appropriate independent oracle for the exact record families,
fields, links, and override shapes that it decodes. It is not absolute truth
for:

- raw bytes that its edit/native display normalizes;
- game-engine runtime behavior not represented in plugin semantics;
- a field whose xEdit definition is incomplete or wrong;
- executable-origin or hardcoded state outside the selected oracle model;
- MO2 file/archive winner semantics;
- mod intent, consequence, severity, or player-visible symptom;
- conflict classifications altered by mod groups, filters, or settings.

The official error-checking guide explicitly acknowledges that an error can be
an xEdit-definition oversight. Known change-history entries also show that
record definitions and checker/conflict behavior receive fixes. A disagreement
must be adjudicated against fixture construction, format evidence, a known-good
patch, or game behavior where necessary; Infinium must not silently declare
xEdit correct.

### F3 — View mode prevents plugin saving but is not side-effect free

Exact source sets both `wbEditAllowed := False` and `wbDontSave := True` in
View mode. That makes `-view` the safest manual record-inspection mode.

It still:

- creates or updates a settings file;
- always attempts a default log beside the executable;
- may also write the `-R` custom log;
- uses/removes a temp directory according to `-T`;
- creates and reads reference-cache files unless `-DontCache` is supplied;
- can perform Nexus and GitHub update checks unless disabled in isolated
  settings or blocked by process policy.

Therefore “View” means no plugin save, not “no writes.” A qualified manual
oracle must run a copied executable with every stateful path redirected into a
product-owned disposable workspace, cache disabled, and network disabled or
isolated settings preconfigured.

### F4 — Script mode is powerful and intrinsically write-capable

Script mode is an xEdit auto mode. It:

- enables edit authority rather than `wbDontSave`;
- loads BSAs and builds references by default;
- gives scripts broad Pascal libraries, filesystem access, process-launch
  capability, xEdit mutators, and record-save primitives;
- selects all root records for the command-line script;
- can auto-exit;
- calls `SaveChanged` at shutdown.

In an auto mode, `SaveChanged` does not show the normal interactive selection
dialog before writing checked modified files. A defective “read-only” script
can therefore alter and save the Data-stage plugins on auto-exit. There is no
source-proven switch that combines Script execution with View mode's
`wbDontSave`.

Consequences:

- no script operation may point at live game/MO2/mod paths;
- a script oracle may operate only on disposable byte copies with pre/post
  hashing and process containment;
- scripts must be first-party, immutable, source reviewed, and hash-allowlisted;
- retrieved or mod-supplied scripts are untrusted content and must never run;
- Script mode is unsuitable as an initial general production analyzer.

### F5 — xEdit has no native stable machine-readable record export

The CLI and built-in checks primarily expose a GUI, plain-text logs, and
limited process status. The script engine can write JSON, so a structured
oracle is feasible, but Infinium would own:

- the script;
- the output schema;
- JSON escaping and numeric/string normalization;
- completion marker;
- atomic publication and integrity checks;
- exact mapping from xEdit script values to Infinium evidence;
- version compatibility tests.

This output is an Infinium-versioned evaluation protocol implemented on top of
xEdit, not an upstream-supported JSON API. Script documentation is explicitly
incomplete/work in progress, so every xEdit upgrade requires source/API
conformance.

### F6 — built-in check modes have bounded unique diagnostic value

`CheckForErrors` is useful because it applies xEdit record definitions and
reports structural/type/reference problems that successful parsing alone does
not establish. Loader messages separately contain missing-master, malformed
input, and other load-time failures.

Exact `4.1.5f` source also provides command-line auto modes for:

- `CheckForErrors`;
- `CheckForITM`;
- `CheckForDR`.

For these modes:

- one target plugin is supplied on the command line;
- the target and required masters are loaded;
- normal successful completion auto-closes the operation;
- `CheckForErrors` reports definition errors;
- ITM/DR modes perform removal/disable actions in memory but set
  `wbDontSave := True` before shutdown;
- process exit is the issue count from 0 through 126, or 127 for 127 or more.

These modes still write settings and logs, and load or processing errors can
leave `CheckResult` at zero. Exit code zero alone therefore does not prove
success or “no issues.” The adapter must also require an exact-version
completion signature, reject loader/error signatures, and retain the raw log.

`CheckForErrors` is the only built-in mode worth carrying as a potential
optional deterministic analyzer after qualification. ITM/DR counts are lower
priority, do not establish harmfulness, and overlap cleaning/LOOT evidence.
They should remain evaluation or explicitly scoped advisory inputs rather than
automatic findings.

### F7 — quick, autoload, conflict, expert, and cleaning modes are not oracle shortcuts

| Mode/switch | Exact behavior relevant to Infinium | Disposition |
|---|---|---|
| `-quickedit:<plugin>` | Selects a plugin and masters; does not make Edit mode read-only | Do not use as a safety control |
| `-autoload` | Skips module selection and loads active plugins plus valid mod groups | Avoid for ground truth because mod groups can hide chain members |
| `-quickshowconflicts` / `-qsc` | Edit submode; applies xEdit conflict filtering | No stable structured output; not needed |
| `-veryquickshowconflicts` / `-vqsc` | Autoloaded optimized conflict view; upstream history notes rare missed conflicts | Reject as EVAL-0052 oracle |
| `-IKnowWhatImDoing` | Exposes expert options and can unlock further mutators | Never pass from Infinium |
| `-AllowDirectSaves` | Changes file loading to permit direct saves | Prohibited |
| `-AllowMasterFilesEdit` | Unlocks master editing when combined with expert switch | Prohibited |
| `-quickclean` / `-qc` | Performs cleaning and prompts/saves depending on mode | Prohibited |
| `-quickautoclean` / `-qac` | Repeats cleaning and automatically saves | Prohibited |
| ONAM/master update/restore, ESMify/ESPify, sort-and-clean, LOD generation | Mutating or generated-output modes | Prohibited through M4 |

No `-expert` switch was found in the exact release source. “Expert” behavior is
the `-IKnowWhatImDoing` path and related options. An adapter must not accept
unknown synonymous flags.

### F8 — every qualified invocation needs a copied executable

`-R` adds a custom log but does not suppress the default log. xEdit always
attempts to write:

```text
<program directory>\<game><tool mode>_log.txt
```

Running the user's executable in place would therefore write into the
user-maintained xEdit directory. A no-mutation contract requires a private
disposable copy of the exact validated executable. Local copying is not
bundling or distributing xEdit; Infinium must delete or retain that private
copy under its temp/cache policy and must never place xEdit in an installer or
update payload.

The copied executable must not use links, junctions, or hardlinks back to the
user's tool directory. The staged Data plugins must likewise be byte copies,
not hardlinks to protected game/mod files.

### F9 — exact path routing is available, but settings and defaults require care

The exact release accepts paths for:

- `-D` Data;
- `-P` plugins list;
- `-I` game INI and `-CustomIni` custom INI;
- `-M` My Games;
- `-G` saves;
- `-S` scripts;
- `-T` temp;
- `-B` backups;
- `-C` reference cache;
- `-R` additional log;
- `-O` LOD output, which must never be used for the oracle.

All directory arguments use a trailing separator in the shipped command-line
contract.

Settings use `<program>\<game><tool>.ini` if present; otherwise xEdit derives
a view-settings filename beside the selected plugins list. The qualification
workspace must therefore:

1. contain the executable copy;
2. omit uncontrolled inherited xEdit INI/settings;
3. place the custom plugin list in the disposable workspace;
4. precreate explicit temp/output directories;
5. pass `-DontCache`, not merely a cache path;
6. account for the unavoidable default and optional custom logs.

### F10 — detection must use exact executable identity

Automatic discovery should be ordered:

1. a user-confirmed path override;
2. an accepted MO2 executable registration whose target resolves to a regular
   local file;
3. a validated narrow installation candidate, if a later ADR accepts such a
   source.

Whole-disk search and filename-only trust are neither reliable nor necessary.

For automated use, validation must require:

- canonical regular-file path and accessibility;
- 64-bit PE architecture;
- allowed TES-family executable role, not a QuickAutoClean or another renamed
  mutating launcher;
- exact file/product version;
- exact SHA-256 mapped to a reviewed release/source revision;
- exact operation-adapter compatibility;
- a private copied executable matching the source hash after copy.

Because `4.1.5f` has no Authenticode signature and its embedded version is
only `4.1.5.0`, the current validated identity is:

```text
release=xedit-4.1.5f
commit=f5c00f3fa3ee39511185515802647246c807f759
exe-sha256=659FADDD8DC061A9D2EDDD20DE925821B87E377284CE179F4538FF78BB2420CD
```

Another executable may be shown as present but unsupported. Infinium must not
quietly run it with the `4.1.5f` adapter.

### F11 — xEdit version advancement is an adapter change

A new xEdit version must not be accepted from a version-number comparison
alone. Requalification must review or diff:

- executable/source/release identity;
- mode and command-line parsing;
- Data/plugin/INI/settings/log/temp/cache/backup/output paths;
- update checks and network behavior;
- save and auto-exit behavior;
- check-mode result and exit behavior;
- script lifecycle, API, JSON support, and mutators;
- Skyrim record definitions and hardcoded/executable-origin records;
- EVAL-0052 results for every supported field and shape;
- EVAL-0046 success, failure, cancel, timeout, and partial-output controls.

Historical results retain the exact xEdit identity and are not silently
reinterpreted after an upgrade.

### F12 — operation/capability matrix

| Candidate operation | Unique result | Machine-readable | Setup-mutation risk | Recommended scope |
|---|---|---:|---|---|
| Manual copied xEdit `-view` on staged data | Human-inspectable chains, winners, fields, links | Standardized human worksheet only | Low after full path isolation; settings/log/temp remain | Required development oracle for initial allowlist |
| First-party `-script -autoexit` on staged data | Repeatable chain/field/link JSON | Yes, Infinium-owned schema | High within stage; script can write/save and run code | Controlled evaluation oracle only after qualification |
| Built-in `-checkforerrors <plugin>` on staged data | Definition errors and unresolved/type problems | Count exit plus plain log | Low to protected setup after staging; log/settings remain | Potential optional analyzer after exact adapter test |
| Built-in ITM/DR checks on staged data | Counts/logs for cleaning-like conditions | Count exit plus plain log | In-memory mutation; no save in exact source | Defer; advisory/evaluation only |
| Quick/very-quick conflict display | Filtered conflict view | No stable output | Edit-mode/settings/network side effects | Reject |
| xEdit through live MO2 VFS | Sees current VFS | Depends on script/log parser | MO2/overwrite/profile/tool side effects not bounded | Reject for M1 |
| Mutagen-only production parser | Typed in-process candidate semantics | Yes | Product-owned reads/cache only | Leading production path, gated by xEdit conformance |
| No xEdit installed | No independent local oracle/checker | Not applicable | None | Product starts; affected capability is unavailable |

### F13 — side-effect matrix for exact `4.1.5f`

| Effect | Default location/trigger | Isolation requirement |
|---|---|---|
| Settings create/update | Program INI if present, otherwise beside custom `plugins.txt`; window/usage/options written | Disposable executable/plugins-list workspace |
| Default log | Always beside executable on close | Copied executable in disposable workspace |
| Custom log | `-R` in addition to default log | Product-owned path; retain as untrusted external-tool output |
| Reference cache | Default `<Data>\<game>Edit Cache`; cache files created after reference build thresholds | Always pass `-DontCache` for oracle/checker qualification |
| Temp | Default OS temp `<game>Edit`; custom `-T`; tool-created path may be removed on close | Dedicated precreated operation temp, recorded and cleaned by product policy |
| Backup/plugin save | Data plus default/configured backup when loaded state is modified and save occurs | Never expose live Data; explicit disposable `-B`; input-copy drift check |
| Update network | Edit/View/Translate can query Nexus and GitHub unless settings disable | Preseed isolated settings and deny network; check/script modes do not start these threads in exact source |
| Script output/processes | Arbitrary script-controlled paths and commands | First-party immutable script, validated output root, contained process, no untrusted scripts |
| MO profile hook | `-moprofile` attempts a hook relative to Data | Do not use; RQ-001 owns MO2 state |
| LOD/generated output | `-O` and LOD modes | Never invoke for this contract |

### F14 — failure and completion matrix

| Condition | Native signal in exact release | Infinium interpretation |
|---|---|---|
| Checker completes with 0 issues | Exit `0`, completion text, valid raw log | Success only when no loader/error signature and all inputs remained stable |
| Checker completes with 1–126 issues | Exit equals count, completion text | Success with issue count; parse only version-allowlisted log records |
| Checker completes with 127+ issues | Exit `127` | Success with lower-bound count `>=127`; exact count unavailable from exit alone |
| Missing master/load failure | Loader/error text; exit can remain `0` | Failed/partial, never “no issues” |
| Checker exception | Error text; exit can remain `0` | Failed, raw log retained |
| Script returns nonzero or throws | Message log; process exit normally remains `0` | Failed unless valid output says otherwise; native exit is insufficient |
| Script writes partial JSON then dies | Partial file; no trustworthy native completion | Invalid; require final manifest/DONE record and schema/integrity checks |
| GUI/dialog appears | No supported unattended signal | Timeout, terminate process tree, mark operation unsupported/failed |
| Process crash/nonzero exit outside checker count contract | OS exit; possible exception log | Failed; never reinterpret as issue count |
| Timeout/cancellation | Wrapper terminates process tree | Terminal attempt; partial outputs invalid; no same-run resume |
| Input changes during run | Pre/post fingerprint mismatch | Entire dependent oracle/check result invalid |
| Write outside allowed product workspace | IO trace or protected-root mismatch | Security violation; disable operation pending investigation |
| Unsupported executable/hash | Preflight mismatch | Do not launch; explicit unavailable capability |
| Missing xEdit | Detection state | Do not launch; unrelated Infinium capabilities continue |

## 7. Alternatives evaluated

| Alternative | Correctness value | Operational value/risk | Disposition |
|---|---|---|---|
| Mutagen `0.54.2` only, using Mutagen-generated fixtures | No independent parser oracle | Fast and easy, but circular validation | Reject as sufficient EVAL-0052 ground truth |
| Mutagen production parser plus controlled xEdit oracle | Independent field/chain/link comparison with bounded supported matrix | Keeps xEdit out of normal runs; adds evaluation maintenance | **Recommend** |
| User-installed xEdit as primary production parser | Mature semantics | GUI process, weak native structure, settings/log/cache, script-save risk, user-version drift | Reject for M1 |
| Built-in xEdit checker as optional production analyzer | Unique definition/error evidence | Plain logs, version-specific parsing, false-success exit boundary | Defer until exact disposable adapter passes EVAL-0046 |
| First-party xEdit JSON script in every user scan | Rich structured output | Script auto mode is write-capable and broadly privileged | Reject for initial product; evaluation-only candidate |
| Manual xEdit inspection only | Strong human oracle and easy adjudication | Slow and not automatically repeatable | Required initial adjudication layer, then sample audit |
| Run xEdit through live MO2 | Avoids staging and sees VFS | Unaccepted MO2 launch/VFS/write boundary; snapshot drift | Reject |
| Stage effective bytes and run copied xEdit | Snapshot-exact, protected setup excluded | Storage/time cost; stage must be complete | **Recommend for evaluation** |
| Bespoke second parser instead of xEdit | Independent in theory | Large duplicated correctness burden and weak community authority | Reject unless accepted tools cannot support needed semantics |

The preferred design deliberately uses different tools for different
responsibilities:

- RQ-001 supplies authoritative effective bytes/order/provider state;
- Mutagen supplies candidate production semantics for allowlisted fields;
- xEdit supplies independent development/evaluation conformance and possibly a
  later optional checker;
- fixture construction and format evidence adjudicate disagreements.

## 8. Contrary evidence, uncertainty, limitations, and unsupported cases

### Contrary evidence and cautions

- xEdit documentation calls the scripting reference incomplete/work in
  progress.
- xEdit release history records record-definition, conflict, checker, and
  parser fixes, so “xEdit said so” cannot replace claim-specific validation.
- `VeryQuickShowConflicts` has an upstream note that it may rarely miss
  conflicts.
- View mode's name can be mistaken for a no-write process even though it writes
  settings/logs and can make update-check network requests.
- Script mode can silently save modified staged plugins during auto-mode
  shutdown.
- checker exit code zero can represent successful no-issue completion **or**
  an earlier load/processing failure.

### Material uncertainty

- The disposable checker command could not be executed in the current agent
  environment because process start was denied.
- No xEdit field/override/link result has yet been compared with the
  RESEARCH-0008 Mutagen outputs.
- No first-party oracle script/schema has been written or qualified.
- No controlled real-mod fixture has been evaluated.
- No Process Monitor/ETW trace has yet proven the full write set.
- No selected subprocess sandbox/restricted-token mechanism exists yet.
- No performance measurement exists for copying/staging and loading a
  high-end plugin set.
- The exact M1 field/family allowlist and non-NPC proof are not selected.
- xEdit's handling of effective loose/archive string-provider precedence has
  not been validated and must not supersede RQ-001.

### Unsupported until follow-up

- running any xEdit mode against live game, MO2, mod, profile, or generated
  output paths;
- `-moprofile` as an authoritative MO2 integration;
- arbitrary user, mod, downloaded, or LLM-generated xEdit scripts;
- parsing a checker log from an unvalidated xEdit version;
- treating exit zero as success without completion and log checks;
- treating xEdit conflict colors/filter results as finding severity;
- using xEdit resource-container APIs as MO2 provider/winner authority;
- enabling `IKnowWhatImDoing`, direct saves, master edits, cleaning, ONAM/
  master update, LOD, or another write-capable production operation;
- cross-runtime/game use;
- making a supported semantic claim before the exact field/shape passes
  EVAL-0052.

## 9. Recommendation

### Recommended answer

Use xEdit in M0/M1 as a **user-installed, exact-version, controlled
development/evaluation oracle**, not as the normal production parser and not
as a required dependency for opening or running unrelated Infinium analyzers.

The recommended first boundary is:

1. detect or accept the user's xEdit path;
2. validate exact `xedit-4.1.5f` 64-bit binary identity for the initial oracle
   adapter;
3. copy the executable into a product-owned disposable workspace;
4. stage snapshot-exact plugin/master/string bytes as independent copies;
5. use copied xEdit View mode for initial human ground-truth adjudication;
6. qualify one immutable first-party JSON oracle script only for repeatable
   EVAL-0052 execution;
7. never expose the script process to protected setup paths;
8. preserve raw output, exact source/executable/script/input/config identity,
   completion evidence, and coverage gaps;
9. fail closed on drift, partial output, unrecognized logs, or writes outside
   the allowed workspace.

Do **not** include xEdit invocation in the M1 production runtime unless an M1
capability specifically requires the built-in checker and the exact operation
has passed the EVAL-0046 procedure below. Mutagen remains the leading
production semantic parser candidate.

Confidence:

- **high** that xEdit is a valuable independent record/field/link/override
  oracle;
- **high** that View and Script/check modes still have non-plugin side effects
  that require isolation;
- **high** that Script mode cannot be considered inherently read-only;
- **high** that native checker exit status is insufficient without log and
  completion validation;
- **medium** that a first-party JSON script can be made reproducible and safe
  enough in a disposable evaluation boundary;
- **low** that any xEdit operation is currently ready for normal production
  invocation, because no process run or IO trace completed.

### Preconditions for any automated acceptance

1. The Wave B integration ADR accepts the staged-oracle responsibility
   boundary without assigning MO2/provider authority to xEdit.
2. The exact executable hash, source revision, game mode, operation, script,
   and output schema are allowlisted together.
3. RQ-001 provides snapshot-exact input bytes and plugin order.
4. RQ-014 provides pre/post drift detection and dependency binding.
5. RQ-032 supplies validated path authorization, process-tree control, and
   write/network/process tracing or containment.
6. The applicable operation passes every normal and adversarial EVAL-0046
   case without a protected or staged-input mutation.
7. The JSON oracle output passes independent manual review and detects its
   failure/partial-output controls.
8. Every field/shape admitted to the Mutagen support matrix passes EVAL-0052.
9. Measured stage/load/runtime/disk cost is acceptable for the development or
   product capability that proposes to use it.
10. A new xEdit build repeats all of these gates rather than inheriting
    approval from `4.1.5f`.

### Exact candidate command shape

The qualification harness, not user input, should construct an argument vector
equivalent to:

```text
<DISPOSABLE_XEDIT_COPY>
  -sse
  -script:<IMMUTABLE_ORACLE_SCRIPT>
  -autoexit
  -nobuildrefs
  -DontCache
  -D:<STAGE_DATA>\
  -P:<STAGE>\plugins.txt
  -I:<STAGE>\Skyrim.ini
  -CustomIni:<STAGE>\SkyrimCustom.ini
  -M:<STAGE_MY_GAMES>\
  -G:<STAGE_SAVES>\
  -S:<STAGE_SCRIPTS>\
  -T:<STAGE_TEMP>\
  -B:<STAGE_BACKUPS>\
  -R:<STAGE_LOGS>\oracle.log
```

The script and staged plugin names are separately validated arguments. No
shell command string, mod metadata, LLM output, or arbitrary extra switch may
reach the process.

For a possible later built-in checker, replace Script/autoexit with:

```text
-checkforerrors <VALIDATED_TARGET_PLUGIN>
```

and require the exact checker completion/log/exit contract in section 6.

## 10. Downstream work enabled

### Proposed Wave B integration ADR input

The Wave B integration ADR should decide:

- xEdit remains user-installed and is not an M1 normal-runtime parser;
- Mutagen is the conditional production semantic layer while xEdit is the
  independent conformance oracle;
- the initial xEdit adapter identity is exact release/hash-specific;
- no live-MO2 or live-Data xEdit invocation is approved;
- the only acceptable oracle input is a snapshot-exact disposable stage;
- View mode is the first manual oracle;
- Script JSON and built-in checker modes remain operation-specific candidates
  gated by EVAL-0046;
- missing/unsupported xEdit creates a capability/evaluation gap rather than
  fabricated fallback;
- every xEdit upgrade is an adapter and semantic requalification event.

No separate production-xEdit ADR is justified unless a product milestone
selects an automated xEdit capability.

### EVAL-0052 — exact xEdit-backed record ground-truth procedure

EVAL-0052 should use this procedure for every M1-consumed record family and
field:

1. **Define independent expected behavior.**
   Create an atomic synthetic fixture with an independent writer or reviewed
   byte-level construction. Do not use Mutagen-generated output as its own
   oracle. Record intended plugin identities, local FormIDs, masters, order,
   chain, winner, fields, and links.
2. **Include matched controls.**
   Add a positive, harmless/intentional negative, malformed or unsupported
   boundary, and renamed/equivalent metamorphic variant.
3. **Freeze exact inputs.**
   Record SHA-256 for every plugin, master, strings table/archive, INI,
   plugins list, fixture manifest, expected-output document, xEdit executable,
   oracle script, and Mutagen lock/configuration.
4. **Stage independent copies.**
   Copy only required inputs into a product-owned disposable Data tree. Do not
   use hardlinks, junctions, symlinks, live MO2 VFS paths, or the user's mod
   directories.
5. **Reproduce authoritative order.**
   Generate the staged `plugins.txt` from the accepted RQ-001 snapshot
   contract. Exclude all mod groups and uncontrolled xEdit settings.
6. **Run the manual oracle first.**
   Open the copied executable in View mode against the stage and complete a
   standardized worksheet for canonical record identity, ordered chain,
   winner, field presence/value, deleted/injected/compressed state, link
   target/unresolved state, and localization display.
7. **Adjudicate the expected fixture.**
   A reviewer resolves any fixture-versus-xEdit disagreement using format
   evidence, known-good patches, or game behavior as appropriate. Do not
   change expected output merely to match xEdit.
8. **Run the qualified JSON oracle.**
   After its own EVAL-0046 qualification, execute the immutable first-party
   script on the same stage. The output must include schema version, xEdit/
   script/input/config hashes, completion record, file order, and the same
   canonical observations as the worksheet.
9. **Run Mutagen independently.**
   Compare its canonical FormKeys, override chains, winner, exact supported
   fields, links, and failures with both the reviewed expected output and the
   xEdit artifact.
10. **Classify every comparison.**
    Use `agree`, `Mutagen disagreement`, `xEdit disagreement`,
    `fixture ambiguity`, `unsupported shape`, or `execution failure`; never
    collapse these into a pass/fail count without details.
11. **Fail closed.**
    A mismatch, unmodeled record shape, partial output, input drift, missing
    completion record, missing master, or unrecognized xEdit version keeps the
    exact field/shape outside the supported allowlist.
12. **Retain provenance.**
    Keep the private fixture dependency manifest, tool/script results,
    adjudication, and replayability status. Redistribute no game/mod bytes
    without separate permission.

The initial matrix must include:

- full ESM/ESP and ESL identities;
- explicit and missing masters;
- full/light override chains and winners;
- deleted, injected, compressed, and unresolved-link states where applicable;
- each exact M1 field path and value representation;
- loose and archived localized strings only after RQ-001 supplies their
  effective provider;
- unknown subrecords and malformed/truncated inputs;
- an unrelated-reorder and one-relevant-winner change;
- at least one materially different non-NPC technical surface selected under
  RQ-024/RQ-036.

### EVAL-0046 — xEdit operation non-mutation procedure

EVAL-0046 must be applied separately to manual View, JSON Script, and any
built-in checker operation:

1. validate exact executable, source revision, script, argument-vector, and
   stage manifest;
2. assert that no process argument or staged link resolves within a protected
   MO2/game/mod/profile/generated-output root;
3. precreate and authorize only the product-owned stage, settings, logs,
   temp, cache-disabled, backup, and output locations;
4. hash all staged inputs and the relevant protected configuration/tool/game/
   profile sentinel files before execution;
5. capture filesystem, registry, network, and child-process activity using the
   accepted Windows tracing/containment mechanism from RQ-032;
6. run normal success, no-issue, malformed input, missing master, output
   failure, script exception, timeout, cancellation, and forced-partial-output
   cases;
7. verify that every write is confined to the declared product-owned
   workspace and that View/check operations made no plugin-byte change;
8. for Script mode, require all staged input hashes to remain unchanged even
   though the mode is write-capable;
9. verify every protected sentinel and input hash is unchanged;
10. reject unexpected network access, child processes, dialogs, or unlisted
    files;
11. invalidate all partial outputs and terminate the complete process tree on
    timeout/cancellation;
12. retain the exact write manifest, raw logs, exit, completion evidence,
    elapsed time, and cleanup result.

Passing one operation/version does not approve another mode or xEdit version.

### Follow-up investigations/evaluation

- qualify the exact View and checker command on an unrestricted disposable
  Windows development host;
- implement a research-only minimal JSON oracle script and schema;
- perform Process Monitor/ETW write/network/process tracing;
- execute EVAL-0052 against RESEARCH-0008's synthetic chain plus independent
  fixtures;
- select the M1 semantic matrix under RQ-024/RQ-036;
- measure stage-copy/load/runtime/disk cost under RQ-027;
- select subprocess/path containment under RQ-032;
- decide whether `CheckForErrors` provides enough product value to justify an
  optional analyzer after M1.

### Gate B impact

This report supplies:

- a defensible xEdit-backed record ground-truth procedure;
- an exact supported-version candidate and detection contract;
- explicit automated/manual capabilities and gaps;
- exact save/cache/temp/log/settings/update/failure risks;
- an EVAL-0046 qualification procedure.

It does **not** pass EVAL-0052 or EVAL-0046 because an xEdit process could not
be started in the current environment. Gate B therefore cannot claim
xEdit-backed field/override agreement from this report alone. M1 may still
exclude production xEdit invocation, but no Mutagen field/shape may enter the
supported semantic allowlist until the controlled oracle procedure is
executed successfully.

## 11. Historical suggested RQ-006 status

Suggested registry status:

> **Investigated — xEdit selected as an exact-version, disposable
> development/evaluation oracle; no production operation accepted. Manual View,
> JSON Script, and optional CheckForErrors contracts remain gated by
> operation-specific EVAL-0046 execution, and Mutagen field support remains
> gated by EVAL-0052.**

At investigation completion, the source-backed command shape did not resolve
RQ-006. ADR-0007 subsequently resolved it by excluding xEdit entirely; the
historical proposal above is not current project direction.

## 12. Requirements and evidence traceability

| Requirement/decision | Evidence or finding | Proposed verification/disposition |
|---|---|---|
| SCOPE-001 | Exact SSE mode and candidate runtime input identified | Bind operation to accepted runtime and EVAL-0054 |
| SCOPE-003, SCOPE-005 | xEdit consumes staged selected-profile bytes/order but does not discover authority | RQ-001 snapshot-stage contract |
| AUTH-001–AUTH-003, ADR-0003 | F3–F9 and EVAL-0046 account for every known write/mutation path | Operation-specific non-mutation qualification |
| TOOL-001 | No xEdit payload is bundled or managed | User-installed source plus private ephemeral copy only |
| TOOL-002 | F10–F11 define detection, exact hash, and version advancement | M1 config contract and M2 setup/settings |
| TOOL-003 | Missing/unsupported/misconfigured states and capability effects are explicit | CLI/UI capability disclosure test |
| SNAP-001, SNAP-002 | Stage/input/tool/script identity and pre/post drift rejection | RQ-014 snapshot integration |
| SNAP-005, SNAP-006 | Exact command, hash, settings, outputs, and replay dependencies retained | Run provenance/replay disclosure |
| EVID-001, EVID-002 | Oracle/check results remain typed and fully versioned | JSON schema plus raw log/evidence retention |
| EVID-006 | Every mismatch, ambiguity, unmodeled shape, or execution failure is a gap | Positive allowlist and abstention evaluation |
| ANALYSIS-002 | Established xEdit error/ground-truth functions are preferred where qualified | Do not rebuild checker before value is demonstrated |
| ANALYSIS-003 | Chain/field/link comparison, not raw conflict enumeration | EVAL-0052 semantic matrix |
| ANALYSIS-016 | Capability, inputs, cost, output, side effects, and failures are declared | Analyzer/oracle contract review |
| ADR-0001 | xEdit is claim-specific independent evidence, not universal truth | Disagreement adjudication procedure |
| ADR-0002 | Every result binds to exact stage/tool/script/config identities | No cross-snapshot mixing |
| ADR-0006 | xEdit remains user-installed; exact technical use was still open | Wave B integration ADR |
| EVAL-0046 | Section 10 defines normal/adversarial non-mutation cases | Must pass before any approved invocation |
| EVAL-0052 | Section 10 defines independent xEdit-backed field/chain/link comparison | Must pass per supported field/shape |
| EVAL-0054 | Wrong runtime/game/tool identity is rejected before launch | Runtime/tool validation boundary |
| EVAL-0080 | Every xEdit write must remain product-owned | Path/reparse/process IO adversarial tests |
| RQ-024/RQ-036 | xEdit record types do not define product taxonomy or roadmap | Select and map exact semantic families separately |
| Gate B | Procedure and boundary exist; execution remains incomplete | Wave B integration must keep this explicit |

## Conclusion

> **Use xEdit as the independent oracle, not as Infinium's everyday parser.**
> Exact `xedit-4.1.5f` provides valuable record-definition checks and the
> chain/link/winner APIs needed to validate Mutagen, but even View mode writes
> settings/logs and Script mode can save modified plugins automatically.
> Infinium should stage byte copies, run a copied exact executable in a
> product-owned disposable boundary, begin with manual View adjudication, and
> qualify a first-party JSON script only for evaluation. No production xEdit
> invocation is accepted until its exact operation passes EVAL-0046, and no
> Mutagen field becomes supported until it passes the xEdit-backed EVAL-0052
> matrix.

## Current disposition

The conclusion above is the investigation's historical recommendation, not
current project direction. ADR-0007 rejects it in full. Infinium has no xEdit
dependency, adapter, setup/configuration, invocation, analyzer, oracle, or
evaluation gate. EVAL-0052 now uses parser-independent first-party fixture
truth as defined by ADR-0007.
