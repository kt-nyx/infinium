# M1 Slice 0 implementation record

Status: Completed
Completed: 2026-07-28
Plan: [M1 backend semantic proof plan](../milestones/M1-backend-semantic-proof.md),
accepted revision dated 2026-07-28
Slice: 0 — Toolchain, licensing posture, and dependency lock
Implementation commit: `a07a098348eeb810e0d3bfbf54c774ac12a627e4`
Review correction: the commit containing this record, with subject
`fix: address M1 Slice 0 review findings`
Verified isolated Slice 0 functional correction patch ID:
`aa9d0ecfd0c41241498fbb72770132f40e9009cc`

## Outcome

Slice 0 established the complete clean-break M1 repository foundation on the
supported Windows x64 environment:

- exact .NET SDK `10.0.302`, C# `14.0`, and `net10.0` settings;
- deterministic, warning-clean builds with built-in .NET analyzers and
  repository style policy;
- central package management, mandatory locked restore, exact project lock
  files, one configured NuGet source, and repository-local package storage;
- the complete 11-production-project and four-test-project solution skeleton;
- exact `Mutagen.Bethesda.Skyrim` `0.54.2` identity without the broader
  aggregate package;
- GPLv3-family descriptive project metadata without an operative SPDX selector
  or license file; and
- a machine-readable inventory of every direct and transitive dependency,
  including license, content identity, and source provenance evidence.

The four executable hosts are explicit non-operational stubs that return a
failure exit code. No product workflow, external operation, protected-root
access, credential use, provider call, or later-slice capability was added.

## Retained artifacts

- `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and
  `NuGet.Config`
- `Infinium.sln` and the required `src/`, `tests/`, `contracts/`,
  `test-data/`, and `tools/evaluation/` skeleton
- 15 committed `packages.lock.json` files
- `dependencies/dependency-manifest.json`
- Slice 0 unit, contract, integration, structural-evaluation, security, and
  fault checks

No runtime run identifier exists because Slice 0 has no runtime operation.
The dependency manifest and committed lock files are the retained verification
artifacts.

## Verification

Final commands were run from the repository root on Windows x64 with .NET SDK
`10.0.302`:

| Command | Result |
| --- | --- |
| `dotnet restore Infinium.sln --locked-mode` | Passed; all 15 projects restored from committed locks. |
| `dotnet restore Infinium.sln --locked-mode --force --no-cache --nologo -p:RestorePackagesPath="artifacts/review-clean-restore-final2-20260729"` | Passed; clean restore into that previously absent repository-root-relative package directory. |
| `dotnet build Infinium.sln -c Release --no-restore` | Passed; 0 warnings and 0 errors. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Unit"` | Passed; 3 of 3 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Contract"` | Passed; 2 of 2 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Integration"` | Passed; 3 of 3 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Evaluation"` | Passed; 4 of 4 applicable structural checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Security"` | Passed; 2 of 2 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Fault"` | Passed; 2 of 2 applicable checks. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed; no formatting changes required. |
| Exact deterministic rebuild script below | Passed; all 611 emitted DLL/PDB hashes matched. |
| `dotnet list Infinium.sln package --include-transitive --no-restore` | Passed; reviewed direct and transitive graph matched the lock inventory and manifest. |
| `rg -n -i "infinium-legacy-archive\|7dd3da6\|legacy[/\\]" src tests contracts test-data tools dependencies Directory.Build.props Directory.Packages.props global.json NuGet.Config Infinium.sln` | Passed; no prohibited reference found. |
| `git diff --check e8ccfae8725b28d063791788eb81f6247729c3bb a07a098348eeb810e0d3bfbf54c774ac12a627e4` | Passed. |
| `git diff --check` in the isolated worktree at `a07a098348eeb810e0d3bfbf54c774ac12a627e4` with only the functional correction patch applied | Passed. |
| `git diff --binary --no-ext-diff a07a098348eeb810e0d3bfbf54c774ac12a627e4 -- $paths \| git patch-id --stable`, with `$paths` from the exact hash command below | Produced `aa9d0ecfd0c41241498fbb72770132f40e9009cc`; this identifies the exact corrected functional patch verified below without requiring a commit to contain its own SHA. |

The exact deterministic rebuild command was:

```powershell
dotnet build Infinium.sln -c Release --no-restore --nologo -t:Rebuild -v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$firstHashes = Get-ChildItem src,tests -Recurse -File |
    Where-Object {
        $_.FullName -match '\\bin\\Release\\net10\.0\\' -and
        ($_.Extension -eq '.dll' -or $_.Extension -eq '.pdb')
    } |
    Sort-Object FullName |
    ForEach-Object {
        "$(($_.FullName.Substring($PWD.Path.Length)))=$((Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash)"
    }
dotnet build Infinium.sln -c Release --no-restore --nologo -t:Rebuild -v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$secondHashes = Get-ChildItem src,tests -Recurse -File |
    Where-Object {
        $_.FullName -match '\\bin\\Release\\net10\.0\\' -and
        ($_.Extension -eq '.dll' -or $_.Extension -eq '.pdb')
    } |
    Sort-Object FullName |
    ForEach-Object {
        "$(($_.FullName.Substring($PWD.Path.Length)))=$((Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash)"
    }
$differences = Compare-Object $firstHashes $secondHashes
if ($differences) {
    $differences
    throw 'Deterministic rebuild hash comparison failed.'
}
```

NuGet audit was enabled for all dependencies at moderate-or-higher severity
during restore and emitted no advisory warning.

The exact verified isolated Slice 0 functional file identities were generated
with:

```powershell
$paths = @(
    '.gitignore',
    'Directory.Build.props',
    'dependencies/dependency-manifest.json',
    'tests/Infinium.EvaluationTests/DependencyManifestTests.cs',
    'tests/Infinium.IntegrationTests/SolutionIntegrationTests.cs',
    'tests/Infinium.UnitTests/BuildPolicyTests.cs',
    'tests/TestRepository.cs'
) | Sort-Object
foreach ($path in $paths) {
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    "$hash  $($path.Replace('\','/'))"
}
```

| SHA-256 | Relative path |
| --- | --- |
| `a9a0129246fbb60ce1b2f33cd5c1fe3420edea6eccf9d2957de499a89026a4a5` | `.gitignore` |
| `271c86ef725048c0baaa8a41b3bb33c3bf3a5bbd801a68fc0a6c24db7a2c3883` | `dependencies/dependency-manifest.json` |
| `549278db768dfb05f981f50627cf8409ecd9153032860d26c2097346e1dd2f22` | `Directory.Build.props` |
| `22cf06192c71c46d6efd0b0c5f62394fa643000d30697ba9ab88f695bfc0b0e7` | `tests/Infinium.EvaluationTests/DependencyManifestTests.cs` |
| `3823173f4e0641d041011e23bd77351ba96e9b245fed16634249ddc67c1d08f6` | `tests/Infinium.IntegrationTests/SolutionIntegrationTests.cs` |
| `44036ef9101ff6e21c9de636d349c9d10c4694f9c3a907aaceff413f072e9bf1` | `tests/Infinium.UnitTests/BuildPolicyTests.cs` |
| `57cc8b0a84722763d8a929e8b6ec9a5954ac230d3e3165a0721e4a63a0505c48` | `tests/TestRepository.cs` |

## Evaluation cases and gates

Slice 0 has no accepted `EVAL-*` product case assigned to it. The
`M1Evaluation` category above validates dependency-manifest and licensing
structure only; it does not claim that an accepted product evaluation case,
fixture, or semantic analyzer has executed.

The three Slice 0 plan checks are satisfied:

1. clean locked restore and warning-clean build on supported Windows x64;
2. exact dependency graph plus license/provenance review; and
3. repository search showing no production reference to the abandoned
   implementation, external archive, or historical source marker.

M1 completion gates and later-slice cases, including external-operation
security cases, remain unpassed and unclaimed.

## Review and corrections

The implementation received traceability, toolchain/dependency, semantic-diff,
and final re-review passes. Corrections made during those passes included:

- importing the root build policy into the nested test build policy;
- selecting the reviewed MSTest `4.3.2` release rather than an unreviewed
  same-day release;
- enforcing locked mode in the default restore policy;
- aligning the editor policy with LF repository normalization;
- adding an explicit Git LF normalization contract for the Windows worktree;
- making the accepted plan's literal `Category=...` test filters execute the
  intended MSTest checks;
- expanding K4os source revisions to full immutable commit identities; and
- making every executable scaffold fail explicitly instead of implying an
  implemented runtime.

The final review found no fixture-specific behavior, real-mod-specific rules,
mutation surface, external-state action, credential path, or product claim in
the Slice 0 implementation.

## Comprehensive follow-up review

A second review on 2026-07-29 inspected the exact committed Slice 0 range
`e8ccfae8725b28d063791788eb81f6247729c3bb..a07a098348eeb810e0d3bfbf54c774ac12a627e4`
in an isolated Git worktree. It found and corrected:

- an x64 solution/project output-path mismatch that prevented the accepted
  `dotnet run --project ... -c Release --no-build` entry points from reaching
  the explicit Slice 0 stubs;
- ignore rules that missed `.env.local` and Visual Studio's `.vs/` cache, and
  unanchored `artifacts/` and `.packages/` rules that could hide nested
  authoritative evidence;
- static-only fault coverage that did not prove locked restore rejects package
  drift;
- license-metadata checks that could miss attributed
  `PackageLicenseExpression`, `PackageLicenseFile`, or `LICENCE*` forms;
- workspace-wide scans that could consume ignored package caches or generated
  files; and
- provenance groups keyed only by package ID instead of exact `id/version`,
  which was ambiguous if later lock graphs resolved multiple versions.

The added regression checks execute all four project entry points, require
their exact stderr and exit code `1`, verify no CLI output is created, inject a
Mutagen version drift and require locked restore error `NU1004`, validate the
secret/cache ignore policy, and derive lock identities only from declared
project directories.

The corrected isolated Slice 0 tree passed the six accepted category commands
with `3` unit, `2` contract, `3` integration, `4` structural-evaluation, `2`
security, and `2` fault checks. Two corrected rebuilds produced matching
SHA-256 identities for all `611` emitted DLL/PDB files. The raw CLI boundary
output was:

```text
Infinium.Cli is a Slice 0 scaffold; no analysis capability is implemented.
exit code: 1
output created: false
```

## Known gaps and deferred work

- `ini-parser-netstandard` `2.5.3` declares MIT and is content-locked, but its
  NuGet metadata does not expose an immutable source revision. This is retained
  as an explicit provenance limitation and does not close the later DIST-003
  public-redistribution audit.
- The GPLv3-family posture remains intentionally descriptive. Selecting
  `GPL-3.0-only` versus `GPL-3.0-or-later`, adding an operative license file,
  public packaging, SBOM generation, and corresponding-source delivery remain
  outside Slice 0.
- Contracts, fixtures, evaluation tooling, application behavior, persistence,
  IPC, MO2/Bethesda analysis, provider access, credentials, and CLI workflows
  remain reserved for their declared later slices.

No later-slice implementation was included.
