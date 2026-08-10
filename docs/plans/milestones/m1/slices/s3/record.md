# M1 Slice 3 implementation record

Status: Completed
Disposition: Gate-accepted and implementation-complete

Last reviewed: 2026-08-10
The owner accepted RESEARCH-0051's conditional-positive additional-mapper amendment on
2026-07-30, the independent explicit-target MO2 UI/VFS oracle is retained, and
EVAL-0045, EVAL-0046 for the delivered exact headless capture operation,
EVAL-0051, and EVAL-0054 pass.

Review completed: 2026-07-30

Plan: [M1 backend semantic proof plan](../../plan.md),
original accepted revision dated 2026-07-28, SHA-256
`65614F8DF1000FC75FCCDB7075DEA8894AA52587120CE40F7D750D0D1AD7A2F3`

Owner-accepted RESEARCH-0051 amendment dated 2026-07-30, SHA-256
`50A53A6E5C5ADD4CF88BF15E185BEDE54C38822CA4726D330CD23ECEEAF59EBE`

Slice: 3 — Supported-target and MO2 snapshot reconstruction

Implementation commit:
`69d8aa7e5fd34bc9f7644cd30147ba89403df508`
(`feat: implement M1 Slice 3 snapshot capture`)

Review correction:
`3b4a22eb4bc4e0445dd913b598e8292a9920874f`
(`test: close Slice 3 security and fault gates`)

Comprehensive review correction:
`2927aff361cff3fc0de955ff34db69be973fb6e1`
(`fix: correct M1 Slice 3 snapshot consistency`)

Authority/evaluator review correction:
`b2fea7030385558b757c4cf45509c8a42f6ac225`
(`fix: complete M1 Slice 3 authority review`)

Admission/non-mutation closeout correction:
`a1aa2030cd3aa129117260479ee761426df3ff02`
(`test: close Slice 3 admission and nonmutation gates`)

Research/closeout documentation:
`eefea80` (`docs: normalize Slice 3 research metadata`; exact implementation
remains the commits above)

## 2026-07-30 owner acceptance and explicit-target oracle closeout

This section supersedes every stale blocked/pending statement in the
historical sections below.

The owner accepted RESEARCH-0051's finding and recommendation. The exact
supported Skyrim SE game plugin has one normal primary Data root, no
game-plugin-provided secondary Data root, and no additional Data-contributing
mapper. Its two known mappings redirect selected-profile `plugins.txt` and
`loadorder.txt` to non-Data LocalAppData control locations already captured
under sealed control-file authority. `QualifiedMapperSha256s` therefore remains
empty. The ordinary physical Data/mod/overwrite overlay is still mandatory;
only a positive real *additional* mapper case is conditional on a future
mapper being deliberately selected and qualified.

The independent evaluator then opened only the disposable copied MO2 `2.5.2`
workspace for profile interaction and selected `Explicit Target`. It retained:

- the explicit profile, three enabled and one disabled local mod rows, their
  displayed priorities, and six enabled plugins in order;
- `meshes/oracle/shared.txt` with `Overwrite` as the Data-tree winner;
- Alpha and Beta conflict modals showing `Overwrite` as the final provider,
  while Beta wins the Windows-equivalent case-normalized path over Alpha;
- `.mohidden` displayed crossed out as inactive and `.git` content displayed
  by MO2, preserving the distinction between MO2 display behavior and
  Infinium's explicit skip policies;
- unmanaged base/DLC Data population and three distinct physical local rows
  despite shared or changed source metadata; and
- archive members remaining unsupported.

The UI oracle launched no Skyrim/game process. A launcher-path misresolution
briefly started the live MO2 executable before profile interaction; it was
immediately stopped. MO2 also registered the copied executable in live
`nxmhandler.ini`. The temporary handler was removed, and both affected live
INI files were restored to their exact retained pre-run byte lengths and
SHA-256 values:

- `nxmhandler.ini`: 132 bytes,
  `AE13FC63DF89516615FE3111B47C8A9A70F7D16FAD5F2E8A32C385A329E168FA`;
- `ModOrganizer.ini`: 21,291 bytes,
  `5C5DDD77F83C45E986AAEB17CE08ADB69C3A70760357C293D0F1CFBCFFA7E27F`.

Their creation/last-write metadata changed and cannot be restored. Live MO2
Skyrim/Morrowind instance roots and the live Skyrim game root remained
structurally unchanged. These UI-oracle effects remain explicit and are not
reclassified as the delivered headless product operation, whose separate
EVAL-0046 canary passed.

### Final verification

| Command | Result |
| --- | --- |
| `dotnet test tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj -c Release --no-build` | Passed; 21 passed and 8 evaluator-private exact-binary checks skipped because their process-local variables were not supplied. |
| `dotnet test Infinium.sln -c Release --no-build` | Passed; 181 passed and 9 skipped: 89 unit/1 environment-dependent symbolic-link skip, 50 contract, 21 integration, and 21 evaluation/8 evaluator-private skips. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check` | Passed. |
| `git diff --check` | Passed. |
| Final process/configuration audit | No MO2 or Skyrim process remains. Live `nxmhandler.ini` and `ModOrganizer.ini` match their exact retained pre-run lengths and SHA-256 values; changed filesystem metadata remains disclosed. |

### Final gate disposition

- **EVAL-0045: passed for Slice 3.**
- **EVAL-0046: passed for the delivered exact headless capture operation;**
  the metadata-changing independent UI-oracle launch is separately recorded.
- **EVAL-0051: passed** for the exact admitted MO2/Skyrim target, the retained
  explicit-target UI/VFS oracle, and the owner-accepted empty additional-mapper
  inventory.
- **EVAL-0054: passed** for the exact private positive and complete
  preregistered negative matrix.

No later-slice implementation was included. Nothing was pushed.

## 2026-07-30 authority/evaluator correction and re-review

This section supersedes stale status statements in the historical sections
below.

The correction added the durable, idempotent snapshot-capture operation,
bounded worker protocol, attempt fencing, coordinator-only staged admission
and publication, interruption failure records, exact executable re-admission
by the coordinator, raw control/dependency retention, and semantic graph
validation. Expired queued operations now terminalize without starving later
work, and an expired attempt may be failed but cannot read or publish staged
output.

The snapshot contract and adapter are now `3.0.0` /
`infinium.mo2-static-reconstruction/v3`. Canonical identity includes exact
executable identities, explicit profile/runtime bindings, enabled and
qualified mapper inventories, mapper admission state, physical root/control
identities, and the retained structural manifest. Identical state recaptures
retain the same structural fingerprint but receive distinct occurrence IDs.
Unlisted/unresolved mods have no guessed priority. Control replacement and an
MO2 hard-link/process alias are detected through Windows physical identity.
Malformed or truncated runtime PE input is `Indeterminate`, matching the
accepted EVAL-0054 oracle.

The independent evaluator package at
`docs/evaluation/fixtures/independent-slice3-evaluator-20260729` now passes the
strict repository fixture reader. Its execution input is answer-isolated and
does not reference the oracle or target matrix. Tracked scripts take explicit
live-root parameters and contain no private absolute live/evaluator paths.

The copied MO2 `2.5.2` accessibility observation verified the saved
`Saved Suggestion` profile, visible enabled/disabled mod rows, and six enabled
plugins in order. The user requested that Computer Use not cover or capture
their foreground desktop; an input guard prevented switching to
`Explicit Target` without violating that boundary. The explicit-target Data /
conflicts UI oracle therefore remains incomplete. A headless production
capture did independently select `Explicit Target` and passed without
launching Skyrim, but production output was not substituted for the missing
UI oracle.

Launching the copied MO2 registered its temporary executable in the live
MO2 application root's `nxmhandler.ini`. The temporary handler was removed
and the exact pre-run 132-byte single-live-handler content shape was restored.
The final manifest has the original file count and total bytes, and all scoped
MO2 executable/INI and Skyrim executable content fingerprints are unchanged.
MO2 overwrote the file's creation/last-write metadata, however, and the
original timestamps are unrecoverable. EVAL-0046 is therefore failed, not
passed.

### Fresh verification

| Command | Result |
| --- | --- |
| `dotnet restore Infinium.sln --locked-mode` | Passed; all projects up to date. |
| `dotnet build Infinium.sln --no-restore --configuration Debug` | Passed; 0 warnings, 0 errors. |
| Exact plan filters `Category=M1Unit`, `M1Contract`, `M1Integration`, `M1Evaluation`, `M1Security`, `M1Fault` | Passed. Counts: Unit 81/1 skipped; Contract 20; Integration 21; Evaluation 18/5 private skipped; Security 7; Fault 13. |
| Supplementary filters `TestCategory=M1Unit`, `M1Contract`, `M1Integration`, `M1Evaluation`, `M1Security`, `M1Fault` | Passed. This included the multi-category tests omitted by the plan's `Category` property filter: Security 55 passed/2 skipped across projects and Fault 66 passed/1 skipped. |
| Evaluator-private `SupportedExecutableAdmissionEvaluationTests` with the exact copied MO2/game/plugin and `Explicit Target` variables | Passed; 9/9, including strict package loading, exact identities, explicit-profile capture, missing, unsupported-channel, malformed, and one-byte mutation cases. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check` | Passed. |
| `git diff --check` | Passed before the implementation commit. |

### Current gate disposition

- **EVAL-0045:** regression path implemented and passing for explicit durable
  submission, idempotency, worker dispatch, staged admission, fencing, restart
  failure records, and publication authority.
- **EVAL-0046:** **failed** because copied MO2 changed live
  `nxmhandler.ini` metadata. Content was restored, but full non-mutation cannot
  be claimed.
- **EVAL-0051:** **blocked/not passed**. Exact headless explicit-profile
  capture passed, and a partial saved-profile accessibility observation is
  retained, but the explicit-target Data/conflicts oracle and a qualified
  supported secondary-root mapper remain absent.
- **EVAL-0054:** **partial/not passed**. All nine executed package/positive/
  negative checks passed, but unreadable, inconsistent-metadata, full
  manager/platform/architecture, and deterministic capture-race variants from
  the preregistered matrix are not all retained as executed gate evidence.

No later-slice implementation was included, and nothing was pushed.

## 2026-07-29 autonomous closeout follow-up

This section supersedes the gate status and verification counts in the dated
authority/evaluator section and all historical sections below.

Commit `a1aa2030cd3aa129117260479ee761426df3ff02` completed the full
EVAL-0054 negative matrix and the delivered-operation EVAL-0046 canary.
EVAL-0054 now exercises the exact target, a known unsupported GOG channel,
unsupported platform/architecture/application, same-version unknown hash,
malformed input, missing input, a sharing-denied unreadable input,
inconsistent byte length with retained metadata, unsupported manager, and a
deterministic executable-identity capture race. Every negative fails before
snapshot/semantic output.

EVAL-0046 now executes both:

- a project-authored disposable protected-root matrix covering bytes,
  structural membership, Windows physical identity and hard-link count,
  creation/last-write metadata, ACL SDDL, reparse tag/target, alternate data
  streams, isolated cache/temp roots, process-tree/target-process deltas, and
  exclusive post-capture root-handle release; and
- the exact evaluator-private MO2/game/profile capture with the same complete
  protected-root canary, process-state comparison, and retained-handle check.

The public adapter surface is reflectively constrained to `Capture`; no
write/apply/set/sort/save/launch operation is reachable. No new descendant or
target process was observed, so child argument/environment/inherited-handle
evidence is explicitly `not-applicable`, not represented by fabricated empty
arrays.

The earlier copied-MO2 UI-oracle attempt remains historical evidence: it
changed live `nxmhandler.ini` metadata. That separately recorded evaluator
side effect is not reclassified as non-mutating. It is also not the delivered
headless product operation gated by EVAL-0046.

RESEARCH-0051 inspected the exact admitted `game_skyrimse.dll` and its
first-party source. The supported Skyrim SE game plugin has an empty secondary
Data-root inventory. Its only `IPluginFileMapper` contributions map
profile `plugins.txt` and `loadorder.txt` to their non-Data LocalAppData
targets, which Slice 3 already captures through sealed control-file authority.
Adding the game-plugin hash to the generic loose-mapper allowlist would
incorrectly authorize caller-supplied arbitrary roots. The empty production
mapper allowlist therefore remains the correct fail-closed implementation.
The accepted EVAL-0051/fixture requirement for an unconditional positive real
secondary-root/mapper contribution needs a reviewed conditional-positive
amendment rather than fixture-specific production behavior.

### Fresh autonomous verification

Commands were run from repository root on Windows x64 with the exact
evaluator-private MO2, game plugin, Skyrim executable, instance, and
`Explicit Target` profile variables.

| Command | Result |
| --- | --- |
| `dotnet restore Infinium.sln --locked-mode` | Passed; all projects up to date. |
| `dotnet build Infinium.sln -c Release --no-restore` | Passed; 0 warnings, 0 errors. |
| Literal plan filters `Category=M1Unit`, `M1Contract`, `M1Integration`, `M1Evaluation`, `M1Security`, `M1Fault` | Passed. Counts across projects: Unit 81/1 skipped; Contract 20; Integration 22; Evaluation 31; Security 7; Fault 13. |
| Supplementary filters `TestCategory=M1Unit`, `M1Contract`, `M1Integration`, `M1Evaluation`, `M1Security`, `M1Fault` | Passed. Counts across projects: Unit 82/1 skipped; Contract 32; Integration 22; Evaluation 46; Security 58/1 skipped; Fault 71. |
| `dotnet test Infinium.sln -c Release --no-build` | Passed; 189 passed and 1 environment-dependent symbolic-link check skipped: 89 unit/1 skipped, 50 contract, 21 integration, and 29 evaluation. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check` | Passed. |
| `git diff --check` | Passed before the closeout-record commit. |

### Current gate disposition

- **EVAL-0045: passed for Slice 3.** Explicit durable initiation,
  idempotency, bounded worker dispatch, staging, fencing, restart handling,
  coordinator validation, and publication authority pass.
- **EVAL-0046: passed for the delivered Slice 3 operation.** The exact
  headless capture is non-mutating and process/handle clean. The conditional
  gate must be repeated for future external operations or versions.
- **EVAL-0051: blocked/not passed.** Exact headless explicit-profile capture
  passes, and direct physical/oracle construction plus a partial saved-profile
  accessibility observation are retained. Closure still needs the independent
  explicit-target Data/conflicts UI/VFS observation and reviewed disposition
  of RESEARCH-0051's conditional-positive mapper amendment.
- **EVAL-0054: passed.** The exact private positive and complete preregistered
  negative matrix pass without best-effort semantic output.

No later-slice implementation was included. Nothing was pushed.

## Outcome

Slice 3 implements a bounded, Windows-only, read-only MO2 snapshot adapter:

- every capture requires exact absolute MO2 instance/configuration paths and
  an explicit immediate profile-directory selection; the persisted
  `General/selected_profile` value is decoded and retained only as a saved
  suggestion;
- the adapter admits only the exact locally researched MO2 `2.5.2` executable
  identity and the exact accepted Steam Windows x64 Skyrim SE `1.6.1170.0`
  executable identity, platform, channel, and App ID;
- the canonical MO2 INI must declare the accepted game name/path and the
  qualified default `Settings/base_directory` layout for profiles, mods, and
  overwrite;
- capture refuses to proceed while the selected MO2 executable is running and
  exposes no MO2 launch, USVFS, write, apply, save, or sort operation;
- content-sealed profile/configuration controls are read from stable
  read-only/non-write-shared handles, while selected profile, mods, overwrite,
  game Data, and qualified mapping roots receive deterministic before/after
  structural captures;
- control hashes, exact executable identities, explicit selection, runtime
  context, qualified mappings, and the structural manifest all contribute to
  the snapshot fingerprint;
- exact MO2 `modlist.txt` prefix/comment/BOM handling, reverse priority,
  explicit enablement, disabled/unlisted behavior, physical Data, overwrite,
  hidden suffixes, skipped `.git` directories, and top-level MO2 management
  content are represented without entering unsupported content into effective
  Data;
- loose-file output preserves Windows-equivalent normalized paths, ordered
  full provider chains, exact winners, physical provider paths, qualified
  virtual prefixes, and within-provider case-collision gaps;
- `plugins.txt` and `loadorder.txt` are retained as separate inputs and are
  correlated only with reconstructed top-level winning plugin files;
  duplicates, malformed entries, missing providers, absent list entries, and
  ambiguous providers remain explicit gaps;
- physical local installed entities have instance/directory identities and
  structural inventory fingerprints separate from mutable `meta.ini` source
  hints; source hints are never promoted to unique physical identity;
- assurance is population-specific: control files are selectively
  content-sealed, loose providers are structurally assured, and archive-member
  semantics are explicitly unsupported;
- missing/inaccessible configuration, unsupported identity/context,
  reparse-point input, unknown mapper contribution, quiescence failure, and
  mid-capture structural or same-size/control-byte drift fail or gap closed.

The original implementation review corrected three material defects:

- a UTF-8 BOM at the start of an MO2 control file was initially interpreted as
  part of the first key/mod name;
- the first private run traversed the complete MO2 application/instance root,
  redundantly including configured content and exceeding the bounded
  structural population; canonical configuration is now content-sealed
  directly and only the declared state roots are structurally traversed; and
- casing differences between different providers were initially reported as
  collisions. A collision is now emitted only for distinct
  Windows-equivalent spellings inside one provider. The real private run's gap
  count fell from 6,596 to 95 without hiding its actual missing/correlation
  gaps.

The later comprehensive review found that those corrections were insufficient.
Commit `2927aff` additionally:

- derives mod/provider/plugin/physical output from the retained structural
  observation instead of independently re-enumerating live trees between the
  two structural captures, removing the mixed-state ABA path;
- seals, fingerprints, parses, and end-revalidates every consumed `meta.ini`;
- validates all declared paths before executable admission and admits mapper
  identity before touching a mapper root;
- re-admits both executables and rechecks MO2 quiescence after the second
  structural capture;
- includes Windows volume/file identity in root and local-entity identity,
  binds instance/profile identity into snapshot identity, and preserves a
  physical entity across a directory rename;
- implements configured MO2 skip suffix/directory handling, including hidden
  directories rather than only hidden files;
- represents a discovered-but-unlisted object as `Unresolved`, not false or
  disabled, and never contributes it to effective Data;
- fails required `modlist.txt`, `plugins.txt`, or `loadorder.txt` absence
  without publishing a snapshot;
- stops structural traversal at its entry bound, adds cancellation, bounds
  executable admission before hashing, and converts malformed/collision/input
  failures to typed failed results;
- makes returned snapshot collections read-only and seals the executable
  admission test seam behind an internal interface; and
- emits an unconditional
  `mo2-game-plugin-inventory-unqualified` coverage gap until the Skyrim
  game-plugin automatic/foreign/secondary-root inventory passes EVAL-0051.

## Exact implementation identity

- .NET SDK: `10.0.302`; target/runtime: `net10.0` / `10.0.10`, Windows x64
- Adapter: `infinium.mo2-static-reconstruction/v2`
- MO2 support manifest: `infinium.mo2-2.5.2-local-research/v1`
- Exact MO2 SHA-256:
  `442B354A8F34754DA0048654C44D27F51628FEBA54CE46C3187CF58D6C43E622`
- Runtime support manifest: `infinium.skyrimse-1.6.1170-steam/v1`
- Exact runtime byte length: `37,157,144`
- Exact runtime SHA-256:
  `C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9`
- Runtime shape: AMD64 (`0x8664`), PE32+ (`0x020B`), GUI subsystem
  (`2`), fixed version `1.6.1170.0`, Steam App ID `489830`
- Snapshot contract/schema: `2.0.0` (clean-break `ModEnablementState` and
  physical-object identity correction)
- No new NuGet dependency was introduced; only project references and their
  locked project-dependency entries changed.

The exact evaluator-private paths, profile bytes, mod names, and provider
contents are not committed. Tests accept them only through process-local
environment variables. The tracked implementation contains the permitted
versioned identities and fingerprints only.

## Comprehensive review verification

Commands were run from repository root on Windows x64 against correction
commit `2927aff`. The evaluator-private variables were unavailable during this
review; private executable/profile tests therefore skipped and the historical
private run below does not qualify the corrected commit.

| Command | Result |
| --- | --- |
| `dotnet restore Infinium.sln --locked-mode` | Passed; all projects were up to date under the committed locks. |
| `dotnet build Infinium.sln -c Release --no-restore` | Passed; 0 warnings and 0 errors. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Unit"` | Passed; 75 checks passed and the environment-dependent symbolic-link check skipped. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Contract"` | Passed; 20 checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Integration"` | Passed; 16 checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Evaluation"` | Available checks passed: 16 passed and 4 evaluator-private Slice 3 checks skipped. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Security"` | Passed; 7 available checks. This is regression evidence, not EVAL-0046 closure. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Fault"` | Passed; 13 checks. |
| `dotnet test Infinium.sln -c Release --no-build` | Passed; 162 checks passed and 5 environment-dependent checks skipped: 83 unit passed/1 skipped, 50 contract, 15 integration, and 14 evaluation passed/4 skipped. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check` | Passed. |
| `git diff --check` | Passed. |

The focused correction cases now cover:

- a same-size/timestamp `meta.ini` mutation;
- MO2 starting after the initial quiescence check;
- executable identity changing during capture;
- `.mohidden` directories and configured custom suffix/directory skips;
- an unqualified missing mapper root proving rejection before any root read;
- missing required profile controls;
- unresolved unlisted objects that cannot contribute providers;
- rename-stable physical entity identity; and
- read-only public snapshot collections.

Two duplicate category-wrapper tests from correction commit `3b4a22e` were
removed; the underlying fault and security assertions retain their own test
categories.

## Original implementation verification (historical)

Commands were run from repository root on Windows x64:

| Command | Result |
| --- | --- |
| `dotnet restore Infinium.sln --locked-mode` | Passed; all 15 projects matched committed lock files. |
| `dotnet build Infinium.sln -c Release --no-restore` | Passed; 0 warnings and 0 errors. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Unit"` | Passed; 68 applicable checks passed and 1 environment-dependent symbolic-link check skipped. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Contract"` | Passed; 20 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Integration"` | Passed; 16 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Evaluation"` with the five evaluator-private MO2/runtime variables | Passed; 20 applicable checks, including 18 in the evaluation project. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Security"` | Passed; 8 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Fault"` | Passed; 14 applicable checks. |
| `dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~CoordinatorRestartFencesInterruptedWorkerAndRecoversDurableRun"` | Passed on immediate rerun after one unrelated pre-existing temporary-directory cleanup race in the first accumulated full-suite run. |
| `dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build` | Passed on rerun; all 16 integration checks. |
| `dotnet test Infinium.sln -c Release --no-build` with the five evaluator-private MO2/runtime variables | Final accumulated rerun passed; 161 checks passed and 1 environment-dependent symbolic-link check skipped: 77 unit passed/1 skipped, 50 contract, 16 integration, and 18 evaluation. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check` | Passed. |
| `git diff --check` | Passed. |

The milestone-wide CLI `evaluate` and `verify-evaluation` entry points remain
unimplemented by the accepted earlier substrate and belong to a later slice.
They were not added or invoked here.

## Historical raw private-run inspection

This run predates correction commit `2927aff` and is retained as historical
diagnostic evidence only. It must be rerun before it can support the corrected
implementation.

The exact private MO2 executable and runtime were admitted, no MO2 process was
running, and the selected profile completed a double structural capture
without launch or mid-capture drift. The result was
`CompletedWithGaps`, not a clean conformance pass:

| Gap code | Count | Meaning |
| --- | ---: | --- |
| `listed-mod-missing` | 6 | Listed MO2 objects had no corresponding regular mod directory in the bounded regular-mod model. |
| `plugin-correlation-gap` | 9 | Enabled/listed plugins had no reconstructed winning loose provider. |
| `loadorder-plugin-not-listed` | 80 | Load-order entries were absent from the bounded `plugins.txt` interpretation, including game-plugin-managed entries. |

These 95 gaps are retained in test output and demonstrate why exact
game-plugin/foreign-object semantics cannot be claimed from the current
adapter or private profile. No gap was converted to absence, a guessed winner,
or semantic output.

The project-authored focused matrices passed for:

- explicit profile selection differing from the saved suggestion;
- missing/stale listed mods and conservatively disabled unlisted mods;
- enabled/disabled mod state, reverse priority, two-provider chains, physical
  Data, overwrite, hidden files, skipped/management content, and plugin
  correlation;
- qualified mapping admission and unknown mapper rejection;
- separate physical installed entity and mutable source-hint identity;
- running-MO2 refusal;
- same-size/control-byte mutation with restored timestamp;
- exact private MO2/runtime admission, missing runtime, unsupported channel,
  and a private one-byte same-version mutation; and
- protected-root identity/tree non-mutation and absence of passive capture or
  process launch.

## Evaluation cases and gate status

None of the four declared Slice 3 gates is recorded as passed:

- **EVAL-0045 — not executed to the accepted case.** Object construction is
  passive, but capture is not yet submitted as the coordinator's one durable,
  idempotent, explicitly initiated parent/child operation. The focused object
  test is regression evidence only.
- **EVAL-0046 — not executed to the accepted case.** Available tests show no
  byte/basic-metadata mutation and no launch API, but the full exact-operation
  canary has not retained file identity, ACL, reparse target/type, ADS,
  process-tree/argument/environment/handle, cache/temp, forbidden-operation,
  staging, and coordinator-publication evidence.
- **EVAL-0051 — blocked and not passed.** The accepted
  `MO2-ATOMIC-DEV`, `MO2-INTEGRATION-VAL`, `MO2-NEGATIVE-VAL`, and
  independently sealed held-out oracle remain uncreated. The production
  constructor has no admitted mapper/secondary-root inventory, and every
  corrected capture now exposes the unqualified Skyrim game-plugin
  automatic/foreign/secondary-root population rather than claiming completion.
- **EVAL-0054 — blocked and not passed.** The exact private positive and
  one-byte paths were not rerun on `2927aff`; the independent target-negative
  package remains uncreated, and the known-unsupported, malformed/unreadable,
  inconsistent-metadata, manager/platform, and capture-race matrix is not
  complete.

No fixture package was fabricated during implementation: the accepted fixture
rules require independent authors/reviewers, oracle isolation, and controlled
MO2 observation before those packages can be accepted or executed as a gate.

## Security, privacy, and semantic review

- No legacy archive or historical implementation was inspected or restored.
- No MO2, Skyrim, USVFS, LOOT, game, helper, network, provider, or paid process
  was launched.
- Product code opens content controls and executable bytes read-only while
  denying concurrent write/delete sharing; directory populations are captured
  twice and changed dependencies invalidate the attempt.
- Exact runtime/MO2/profile paths and private contents are absent from tracked
  files and commits.
- Reparse roots/entries, out-of-root profile selection, unsafe virtual
  prefixes, malformed/oversized controls, unknown mapper hashes, and
  unsupported runtime context fail or gap closed.
- Physical Data/mod/overwrite state, source hints, provider topology, and
  operational support results are not assigned Skyrim mod-impact taxonomy
  codes.
- Generic production code contains no real-mod-, fixture-, plugin-name-,
  title-, race-, actor-, zone-, or profile-specific rule.

The final diff/re-review covered the accepted Slice 3 deliverables,
SCOPE/AUTH/SNAP/TOOL requirements, ADR-0004, ADR-0008 through ADR-0010,
ADR-0015, ADR-0016, ADR-0018, ADR-0019, and ADR-0021, EVAL-0045, EVAL-0046,
EVAL-0051, EVAL-0054, fixture/oracle isolation, anti-overfitting rules,
population assurance, gap truth, and later-slice exclusions.

## Historical gaps before the authority/evaluator and autonomous closeout corrections

- Capture remains a direct library call and is not yet wired through the
  coordinator's immutable assignment, bounded worker, staged manifest,
  admission fence, or authoritative publication path. Therefore the accepted
  ADR-0018/EVAL-0045/EVAL-0046 authority path is not proven for Slice 3.
- The corrected adapter validates final opened identities for declared roots
  and physical mod roots and rejects reparses, but nested directory enumeration
  still uses path-based .NET enumeration rather than a retained
  handle-relative traversal. The remaining nested replacement/check-use race
  requires either a handle-relative reader or a narrower accepted decision
  before the operation can claim the full ADR-0021 adversarial path contract.
- The synthetic protected-root fingerprint is not the accepted EVAL-0046
  canary. ACL, ADS, reparse target/type, file identity, process
  arguments/environment/handles, and staging/publication evidence remain
  unexecuted.
- Complete EVAL-0051 conformance requires the independently authored,
  observed, reviewed, and sealed disposable MO2 fixture/oracle packages already
  required by the accepted specification. This is an acceptance dependency,
  not permission to add fixture-specific production behavior.
- The accepted supported Skyrim game-plugin/foreign-object/forced-plugin and
  secondary-root/mapper inventory remains unresolved. Every current snapshot
  emits an explicit population gap, and the public production constructor
  admits no external mapper hash.
- Complete EVAL-0054 requires the independent negative package and the
  known-unsupported, malformed/unreadable, inconsistent-metadata,
  manager/platform, and executable capture-race variants. Current executable
  admission also has no known-unsupported manifest entry.
- Raw control bytes and the canonical structural/provider dependency manifest
  are used to derive the result and fingerprint but are not yet retained as a
  public typed raw-observation/dependency contract for audit/replay.
- Archive containers remain physical loose-provider entries when present, but
  archive-member enumeration and engine load/winner semantics are explicitly
  unsupported.
- Snapshot provider populations are structural, not fully byte sealed.
  Content consumers must read/hash their exact bytes under the same dependency
  fence when their later slice is implemented.
- The bounded adapter supports the exact default MO2 `base_directory` layout
  qualified by the local research. Custom path layouts require separate
  evidence and admission rather than best-effort interpretation.
- The historical private profile is not a clean pilot baseline and produced 95
  explicit regular-object/plugin-correlation gaps before `2927aff`; that run
  must be repeated because it does not verify the correction commit.

No Bethesda parsing, ordered-plugin semantic consumption, Mutagen work,
analyzer/source/candidate/case logic, LLM/provider work, CLI evaluation
harness, UI, or other later-slice implementation was included. Nothing was
pushed.
