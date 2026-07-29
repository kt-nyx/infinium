# M1 Slice 3 implementation record

Status: Comprehensive review correction completed; Slice 3 is **not
gate-accepted or implementation-complete**. The corrected adapter passes the
available accumulated regression suite, but accepted closure remains blocked
by the independent fixture/oracle packages, the complete EVAL-0045/EVAL-0046
coordinator/worker and protected-root canary paths, the complete EVAL-0054
negative matrix, and the positively qualified game-plugin/secondary-root
inventory described below.

Review completed: 2026-07-29

Plan: [M1 backend semantic proof plan](../milestones/M1-backend-semantic-proof.md),
accepted revision dated 2026-07-28, SHA-256
`65614F8DF1000FC75FCCDB7075DEA8894AA52587120CE40F7D750D0D1AD7A2F3`

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

## Known gaps and deferred work

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
