# M1 Slice 0 implementation record

Status: Completed
Completed: 2026-07-28
Plan: [M1 backend semantic proof plan](../milestones/M1-backend-semantic-proof.md),
accepted revision dated 2026-07-28
Slice: 0 — Toolchain, licensing posture, and dependency lock

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
| `dotnet restore Infinium.sln --locked-mode --force --no-cache --nologo -p:RestorePackagesPath="<new empty artifacts/verification path>"` | Passed; clean restore into a previously absent package directory. |
| `dotnet build Infinium.sln -c Release --no-restore` | Passed; 0 warnings and 0 errors. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Unit"` | Passed; 3 of 3 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Contract"` | Passed; 2 of 2 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Integration"` | Passed; 2 of 2 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Evaluation"` | Passed; 4 of 4 applicable structural checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Security"` | Passed; 1 of 1 applicable checks. |
| `dotnet test Infinium.sln -c Release --no-build --filter "Category=M1Fault"` | Passed; 1 of 1 applicable checks. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed; no formatting changes required. |
| Two `dotnet build Infinium.sln -c Release --no-restore --nologo -t:Rebuild -v:minimal` runs followed by SHA-256 comparison | Passed; all 611 emitted DLL/PDB hashes matched. |
| `dotnet list Infinium.sln package --include-transitive --no-restore` | Passed; reviewed direct and transitive graph matched the lock inventory and manifest. |
| `rg -n -i "infinium-legacy-archive\|7dd3da6\|legacy[/\\]"` over production and Slice 0 implementation roots | Passed; no prohibited reference found. |
| `git diff --check` | Passed. |

NuGet audit was enabled for all dependencies at moderate-or-higher severity
during restore and emitted no advisory warning.

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
