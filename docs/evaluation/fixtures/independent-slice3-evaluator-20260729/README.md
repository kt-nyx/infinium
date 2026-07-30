# Independent M1 Slice 3 evaluator package

Status: EVAL-0051 and EVAL-0054 passed for the exact admitted target
Evaluator role: separately isolated oracle author
Specification set: `infinium.eval.m1.semantic-and-ground-truth/1`
Cases: EVAL-0051 and EVAL-0054
Created: 2026-07-29

## Independence boundary

This package was authored from the accepted product, architecture, research,
evaluation, and M1 plan documents. The evaluator did not inspect production
source, production tests, production adapter output, prior evaluation output,
or the legacy archive. Expected results are not derived from the system under
test.

The disposable execution payload is generated outside the repository. The
tracked package contains public identities, independently authored expected
facts, reproducible construction scripts, and redistribution-safe evidence.
The exact Skyrim executable and MO2 application binaries remain private and
are never added to this repository.

## Current execution state

- Direct physical fixture construction: complete after running
  `scripts/Initialize-Slice3Oracle.ps1`.
- Exact target and target-negative byte construction: complete after running
  that script.
- Protected-root before/after comparison: produced by
  `scripts/New-ProtectedRootManifest.ps1` and
  `scripts/Compare-ProtectedRootManifests.ps1`.
- Copied-MO2 observation: complete for explicit selection of `Explicit Target`,
  visible mod/plugin order, shared loose-file winner/loser views,
  case-normalized precedence, overwrite, `.mohidden`, `.git`, and distinct
  local mod rows. Screenshots and the typed observation record are retained.
- Production-adapter execution: exact MO2, game plugin, and Skyrim identities
  passed; the adapter captured `Explicit Target` headlessly without launch.
- Product protected-root result: the exact headless capture passed EVAL-0046
  with complete byte, physical-identity, metadata, ACL, reparse, ADS, process,
  side-effect-root, and retained-handle canaries. It launched no process.
- UI-oracle side effects: copied MO2 registered its temporary executable in
  live `nxmhandler.ini`; an initial launcher misresolution also briefly opened
  the live MO2 executable and caused MO2 to rewrite one line ending in live
  `ModOrganizer.ini`. The copied handler was removed. Both INI files were
  restored to their exact pre-run lengths and SHA-256 values, but their
  creation/last-write metadata cannot be restored. Live MO2 instance roots and
  the live game root remained structurally unchanged. These UI-oracle effects
  are not reclassified as the non-mutating product operation gated by
  EVAL-0046.

EVAL-0051 passed after the explicit-target UI/VFS observation and the owner's
acceptance of RESEARCH-0051. The exact admitted Skyrim SE game plugin has an
empty additional secondary-Data inventory and only two non-Data
profile-control mappings. The ordinary Data/mod/overwrite overlay remains
required; only a real *additional* mapper case is conditional on future
deliberate qualification. EVAL-0054's exact target and full preregistered
negative matrix also passed.

## Disposable root used for this construction

The evaluator-owned root for the 2026-07-29 construction is retained privately
as `<EVALUATOR_ROOT>`.

All MO2 profiles, mods, downloads, cache, overwrite, logs, output, and game
fixtures are under that root. The copied executable is:

`<EVALUATOR_ROOT>\mo2-app\ModOrganizer.exe`

The disposable exact game executable is:

`<EVALUATOR_ROOT>\game-root\SkyrimSE.exe`

The live MO2 and Skyrim executables are read-only sources. Never launch either
live executable and never point the disposable MO2 configuration at a live
profile, mod, game, download, cache, overwrite, log, or output root.

## Reproduction

Run from the repository root:

```powershell
$evalRoot = '<unique evaluator-owned absolute root>'
$liveMo2Root = '<read-only live MO2 application root>'
$liveGameRoot = '<read-only live Skyrim game root>'
$package = 'docs\evaluation\fixtures\independent-slice3-evaluator-20260729'

& "$package\scripts\New-ProtectedRootManifest.ps1" `
  -OutputPath "$evalRoot\evidence\protected-before.json" `
  -LiveMo2Root $liveMo2Root `
  -LiveGameRoot $liveGameRoot

& "$package\scripts\Initialize-Slice3Oracle.ps1" `
  -EvaluatorRoot $evalRoot `
  -CopiedMo2Root "$evalRoot\mo2-app" `
  -PublicPackageRoot $package `
  -LiveGameRoot $liveGameRoot

& "$package\scripts\New-EvaluatorRootManifest.ps1" `
  -EvaluatorRoot $evalRoot `
  -OutputPath "$evalRoot\evidence\evaluator-before-ui.json"

# Perform the separately recorded MO2 UI/VFS observation only against:
#   $evalRoot\mo2-app\ModOrganizer.exe

& "$package\scripts\New-EvaluatorRootManifest.ps1" `
  -EvaluatorRoot $evalRoot `
  -OutputPath "$evalRoot\evidence\evaluator-after-ui.json"

& "$package\scripts\New-ProtectedRootManifest.ps1" `
  -OutputPath "$evalRoot\evidence\protected-after.json" `
  -LiveMo2Root $liveMo2Root `
  -LiveGameRoot $liveGameRoot

& "$package\scripts\Compare-ProtectedRootManifests.ps1" `
  -BeforePath "$evalRoot\evidence\protected-before.json" `
  -AfterPath "$evalRoot\evidence\protected-after.json" `
  -OutputPath "$evalRoot\evidence\protected-comparison.json"
```

The complete MO2 application payload must already have been copied to the
unique evaluator-owned `mo2-app` directory. Separate live portable-instance
data directories are not application payload and are never used as disposable
profile data.

## MO2 oracle observation checklist

Before launch:

1. Confirm no live or copied `ModOrganizer.exe` process is running.
2. Confirm the copied executable SHA-256 is
   `442B354A8F34754DA0048654C44D27F51628FEBA54CE46C3187CF58D6C43E622`.
3. Confirm `ModOrganizer.ini` contains only evaluator-root paths for
   `gamePath` and `base_directory`.
4. Confirm the disposable game executable SHA-256 is
   `C434208894F07F604B852F29B8EDC3A58C4DE63DE783373733E72B2B73F33BE9`.
5. Confirm the saved selection is `Saved Suggestion`, while the oracle target
   to select explicitly is `Explicit Target`.

Record with screenshots and a typed observation log:

- the copied MO2 title/version and resolved portable instance;
- the initial saved-selection suggestion;
- explicit selection of `Explicit Target`;
- enabled/disabled mod states and displayed priorities;
- plugin enabled/order state for the disposable profile;
- the Data/conflicts view for the shared loose path, including the visible
  winner and alternatives;
- unmanaged Data, overwrite, `.mohidden`, `.git`, and case-normalized path
  behavior;
- the three physical local entities that share source-mapping metadata without
  being collapsed;
- archive population remaining unsupported for this package; and
- every file written by the copied MO2 under the evaluator root.

Do not start Skyrim. If an authoritative effective-VFS observation requires a
child process, use only a project-authored read-only observer inside the
disposable instance and record every copied-MO2/USVFS write. No such observer
is supplied by this package while the mandatory UI-control boundary is
unavailable.

## Gate coverage

`gate-coverage.json` distinguishes complete direct and authoritative UI
evidence, the accepted empty additional-mapper inventory, expected unsupported
archive population, and the separately recorded UI-oracle side effects.
Production output was not used to manufacture the UI oracle.
