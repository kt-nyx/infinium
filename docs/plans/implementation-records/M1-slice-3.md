# M1 Slice 3 implementation record

Status: Implementation complete after review, correction, and re-review;
accepted gate closure remains blocked by the missing independently authored
EVAL-0051 fixture/oracle packages described below.

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

The review cycle corrected three material defects before closeout:

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

## Exact implementation identity

- .NET SDK: `10.0.302`; target/runtime: `net10.0` / `10.0.10`, Windows x64
- Adapter: `infinium.mo2-static-reconstruction/v1`
- MO2 support manifest: `infinium.mo2-2.5.2-local-research/v1`
- Exact MO2 SHA-256:
  `442B354A8F34754DA0048654C44D27F51628FEBA54CE46C3187CF58D6C43E622`
- Runtime support manifest: `infinium.skyrimse-1.6.1170-steam/v1`
- Exact runtime byte length: `37,157,144`
- Exact runtime SHA-256:
  `C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9`
- Runtime shape: AMD64 (`0x8664`), PE32+ (`0x020B`), GUI subsystem
  (`2`), fixed version `1.6.1170.0`, Steam App ID `489830`
- Snapshot contract/schema: `1.0.0`
- No new NuGet dependency was introduced; only project references and their
  locked project-dependency entries changed.

The exact evaluator-private paths, profile bytes, mod names, and provider
contents are not committed. Tests accept them only through process-local
environment variables. The tracked implementation contains the permitted
versioned identities and fingerprints only.

## Verification

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

## Raw private-run inspection

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

- **EVAL-0045:** the delivered capture object is passive until its explicit
  `Capture` call; construction creates no snapshot, run, network operation, or
  process, and each call returns one bounded attempt/result. This Slice 3
  portion passes.
- **EVAL-0046:** synthetic protected-root identity/tree fingerprints and
  read-only handles remain unchanged, with no write API exposed. The real
  private capture launched no process and used only bounded reads/enumeration.
  The delivered Slice 3 operation portion passes; milestone-wide external
  operations remain outside this slice.
- **EVAL-0054:** the exact private positive, missing input, unsupported
  channel, and one-byte same-version mutation paths pass. The accepted
  target-negative executable package is still not independently authored, so
  the complete case is not claimed.
- **EVAL-0051:** project-authored parser/provider/quiescence/drift matrices and
  a real private read-only adapter run pass. The accepted specification still
  marks `MO2-ATOMIC-DEV`, `MO2-INTEGRATION-VAL`, `MO2-NEGATIVE-VAL`, and the
  independent MO2 observation/oracle as not created. The private profile also
  exposes unqualified game-plugin/foreign-object gaps. Therefore complete
  EVAL-0051 conformance is not claimed and Slice 3 cannot honestly be marked
  gate-accepted.

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

- Complete EVAL-0051 conformance requires the independently authored,
  observed, reviewed, and sealed disposable MO2 fixture/oracle packages already
  required by the accepted specification. This is an acceptance dependency,
  not permission to add fixture-specific production behavior.
- The accepted supported Skyrim game-plugin/foreign-object/forced-plugin and
  mapper inventory remains unresolved. Unknown contributions remain explicit
  gaps; the public production constructor admits no external mapper hash.
- Archive containers remain physical loose-provider entries when present, but
  archive-member enumeration and engine load/winner semantics are explicitly
  unsupported.
- Snapshot provider populations are structural, not fully byte sealed.
  Content consumers must read/hash their exact bytes under the same dependency
  fence when their later slice is implemented.
- The bounded adapter supports the exact default MO2 `base_directory` layout
  qualified by the local research. Custom path layouts require separate
  evidence and admission rather than best-effort interpretation.
- The real private profile is not a clean pilot baseline and completed with
  95 explicit regular-object/plugin-correlation gaps.
- The first accumulated full-suite run encountered one existing Slice 2
  integration cleanup race after its assertions. The exact test and the
  complete integration project passed immediately on rerun; no unrelated
  Slice 2 code was changed.

No Bethesda parsing, ordered-plugin semantic consumption, Mutagen work,
analyzer/source/candidate/case logic, LLM/provider work, CLI evaluation
harness, UI, or other later-slice implementation was included. Nothing was
pushed.
