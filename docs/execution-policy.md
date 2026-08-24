# Development execution policy

Status: Accepted

Last reviewed: 2026-08-23
Owner: Project owner

## Purpose and scope

This policy defines the default way Infinium product, architecture,
documentation, public-fixture, and ordinary public-evaluation work proceeds.
It exists to keep development evidence-driven without turning routine defects
or incomplete implementation into owner-level blockers.

This policy does not relax product semantics, provenance, answer isolation,
security, protected-root controls, or private-evaluator governance. A more
specific accepted plan may add constraints only where its work genuinely
requires them. Correction-count and no-retry rules belong only to an explicitly
authorized evaluator, private-oracle, destructive, or externally effectful
operation; they do not become the default for ordinary development.

Under [ADR-0035](architecture/decisions/ADR-0035-defer-independent-semantic-oracle-qualification.md),
M1 and M2 do not authorize independent semantic-oracle authoring, sealing, or
comparison. Their ordinary work uses the accepted product-conformance profile:
developer-owned bounded examples, deterministic references, invalid-state and
metamorphic coverage, persistence/replay, integration/safety evidence, and
fresh review. Historical semantic-package integrity is read-only and grants no
product verdict.

For the remaining M1 Slice 6 development, the owner-authorized
[practical continuation](plans/milestones/m1/slices/s6/development-continuation.md)
is the controlling execution exception. It removes ceremony-version,
per-correction independent-review, and fresh-authority rebinding requirements
that do not directly protect the credential, aggregate cost limit, or semantic
answer isolation. Credential non-exposure, Windows Credential Manager-only
storage, sequential provider effects, conservative USD 10.00 aggregate
accounting, historical-evidence immutability, private-answer isolation, and the
Slice 7 prohibition remain mandatory.

## Default development loop

Ordinary work continues through this loop until its acceptance criteria are
met or a genuine escalation condition occurs:

1. implement the smallest useful vertical increment;
2. run the focused checks for that increment;
3. review semantics, provenance, security, scope, and the diff;
4. classify every finding;
5. correct all must-fix findings within the accepted scope;
6. rerun affected checks and re-review the changed surface; and
7. record deferred, unsupported, and follow-up work explicitly.

There is no arbitrary correction-pass budget for ordinary development. The
recurrent-defect rule below replaces repeated trial-and-error with design
diagnosis without making an ordinary defect an owner decision by itself.

## Candidate lifecycle and verification pyramid

Ordinary work keeps one mutable working candidate through implementation and
correction:

```text
working implementation
  -> coherent vertical package
  -> focused verification
  -> consolidated semantic/security/provenance/diff review
  -> batched corrections on the same candidate
  -> affected checks and changed-surface re-review
  -> review-ready package
  -> one final complete verification floor
  -> one accepted candidate binding
```

A package is coherent only when the full affected producer, consumer,
persistence, readback/output, replay, invalid-state, fixture, test, and
documentation path exists. During development, run the smallest checks that
exercise the current change. Run the package's focused contract, semantic,
persistence, replay, security, and fixture checks before consolidated review.
Run the complete accepted floor only after review findings are corrected and
the package is review-ready.

A failed complete floor means the candidate was not final. Treat that run as
diagnostic evidence, correct the same working candidate, rerun affected checks
and changed-surface review, and attempt the final floor again when ready. Do
not freeze, bind, version, or append closeout chronology for intermediate
corrections. Retain the passing complete floor against the exact accepted
commit as acceptance evidence.

Mechanical or local corrections do not restart a consolidated review. Review
the changed surface. Repeat consolidated review only when a correction changes
semantics, architecture, authority, an independently frozen fixture currently
authorized by an accepted plan, or
the declared package scope.

## Recurrent conceptual defects

If the same conceptual defect recurs after two completed correction attempts,
pause that path for an explicit design diagnosis. State the invariant being
violated, the two attempted resolutions, why they failed, and the smallest
durable correction before continuing.

The diagnosis remains ordinary in-scope work when the answer is an
implementation, test, fixture, validator, documentation, tool, or decomposition
change. Request owner disposition only when the durable resolution would choose
missing product meaning, change accepted architecture, expand scope or
authority, weaken isolation, or require an otherwise unauthorized effect.

## Finding classification

Every review finding or failed check must be classified before deciding what
to do next.

| Classification | Meaning | Required action |
|---|---|---|
| Must fix | The implementation, test, fixture, schema, documentation, or tool is wrong within the accepted scope. | Correct it, rerun affected checks, and re-review. |
| Follow-up | Valid work that is useful but not required for the current package. | Record it in the appropriate plan, implementation record, or research register; continue current work. |
| Non-blocking | Style, preference, optional hardening, or evidence that does not change acceptance. | Address when cheap or record it; do not stop. |
| Owner/authority decision | Accepted product documents or ADRs conflict, or a missing decision would materially choose product meaning or expand scope. | Pause only the affected decision path and request owner disposition. Continue independent in-scope work. |
| Safety or isolation breach | Private-answer contamination, secret exposure, protected-root violation, or unauthorized destructive/external effect occurred or is required. | Stop the affected operation, preserve evidence, and follow the governing security or evaluator policy. |

A reviewer returns the classified findings and an overall `ACCEPT`,
`CORRECT`, or `ESCALATE` judgment. `CORRECT` is the normal result for
repairable defects and authorizes another correction/re-review cycle.

## Test-process cleanup and verification

After a local verification run completes, is cancelled, or times out, wait for
its ordinary shutdown and then prove that it did not leave a repository-owned
`dotnet` or `testhost` process behind. Run test commands with absolute project
paths so process ownership remains attributable to this exact working tree.
Before cleanup, confirm that no other active verification or interactive
repository process in the same working tree owns a matched process.

On Windows, the following procedure targets only processes whose command line
contains the resolved repository root. It snapshots exact process IDs,
revalidates each process against the same root immediately before termination,
and verifies zero matching survivors:

```powershell
$gitRoot = @(& git rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0 -or $gitRoot.Count -ne 1) {
    throw 'The exact repository root could not be resolved.'
}
$repositoryRoot = (Resolve-Path -LiteralPath $gitRoot[0]).Path.TrimEnd('\')
$repositoryNeedle = $repositoryRoot + '\'
$ownedNames = @('dotnet.exe', 'testhost.exe', 'testhost.x86.exe')

function Get-RepositoryOwnedTestProcess {
    @(Get-CimInstance -ClassName Win32_Process | Where-Object {
        $_.Name -in $ownedNames -and
        -not [string]::IsNullOrWhiteSpace($_.CommandLine) -and
        $_.CommandLine.IndexOf(
            $repositoryNeedle,
            [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    })
}

$owned = @(Get-RepositoryOwnedTestProcess)
$owned | Select-Object ProcessId, ParentProcessId, Name

foreach ($snapshot in $owned) {
    $current = Get-CimInstance -ClassName Win32_Process -Filter "ProcessId = $($snapshot.ProcessId)"
    if ($null -ne $current -and
        $current.Name -in $ownedNames -and
        -not [string]::IsNullOrWhiteSpace($current.CommandLine) -and
        $current.CommandLine.IndexOf(
            $repositoryNeedle,
            [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
        Stop-Process -Id $current.ProcessId -Force
    }
}

$remaining = @(Get-RepositoryOwnedTestProcess)
if ($remaining.Count -ne 0) {
    throw "Repository-owned dotnet/testhost cleanup is incomplete: $($remaining.ProcessId -join ',')."
}
"Repository-owned dotnet/testhost processes remaining: 0"
```

Never terminate by process name alone, use a broad workspace or user-profile
match, or kill a shared SDK/compiler process whose command line is not bound to
the exact repository root. If command-line inspection is unavailable or the
root match is ambiguous, report cleanup as unverified; do not broaden the
predicate. Retain the resolved root, matched process IDs and names, and the
zero-survivor result with the verification receipt. Do not retain full command
lines when they could contain credentials or other sensitive arguments.

## Escalation conditions

Ordinary work escalates only when at least one of these conditions is true:

1. accepted product requirements or ADRs conflict, or required product meaning
   is absent and choosing it would create architecture or product semantics;
2. continuing would access private evaluator answers, expose secrets, violate
   a protected-root boundary, or perform an unauthorized destructive or
   external action;
3. the requested result cannot be achieved within the accepted package scope
   or available authority without materially expanding either; or
4. a required external dependency, credential, platform, or owner-controlled
   resource remains unavailable after safe in-scope alternatives are
   exhausted.

A test failure, fixture defect, schema/codec mismatch, validator bug,
PowerShell/runtime incompatibility, stale documentation, incomplete
implementation, review finding, or failed first approach is not an escalation
condition by itself.

When escalation is required, the report must name the exact condition, show
the concrete evidence, state the smallest owner decision needed, and identify
unaffected work that may continue. Do not promote a local failure into a
milestone-wide blocker.

## Contract maturity

Product contracts evolve through explicit maturity states:

| State | Meaning |
|---|---|
| Proposed | Shape is being designed and may change within accepted product authority before implementation depends on it. |
| Implementation-active | An owning package is implementing producers, consumers, persistence, and tests together; revisions are expected. |
| Producer-consumer-validated | At least one real producer and consumer, round trip, invalid-state path, and focused fixture set agree. |
| Slice-frozen | The owning slice is accepted; later incompatible changes require an explicit clean-break revision and coordinated update. |
| Milestone-stable | The milestone's end-to-end output and replay surfaces are accepted for downstream planning. |

Contract-first work establishes a starting shape, not premature immutability.
Before `Slice-frozen`, the owning package may revise a contract in response to
implementation evidence as long as all affected producers, consumers,
persistence, schemas, fixtures, tests, and documentation move together.

## Vertical package structure

Prefer packages that deliver a narrow end-to-end behavior:

```text
input -> behavior -> persistence -> readback/output -> focused fixtures -> review
```

Do not pre-author a comprehensive future corpus or freeze broad downstream
contracts before their producers and consumers exist. Small answer-free
contract examples may precede behavior. Semantic fixtures belong to the
package that implements the behavior, and cross-package corpora belong to the
integration/closeout package.

## Plan and record conventions

Ordinary plans describe:

- recoverable failures and their expected handling;
- genuine escalation conditions from this policy;
- focused verification and review outcomes; and
- contract maturity and vertical deliverables.

They do not impose correction-count budgets or use `stop conditions` as a
catch-all for acceptance failures. Historical plans and implementation records
preserve the rules that governed their own execution; those rules do not
propagate into current work.

The current execution handoff is maintained in
[`current-state.md`](current-state.md). Plans define scope, product
documents and ADRs define meaning, and implementation records preserve
evidence. None of those historical records substitutes for the current-state
handoff.

Current navigation states only the live handoff, accepted inputs, meaningful
gaps, and next gate. Rejected candidates, diagnostic command identities, and
correction chronology stay in Git history or the owning implementation record
unless needed to explain the accepted result, a safety incident, an unresolved
gap, or recovery authority.

## Immutable and runtime-effect authority

The proportional candidate lifecycle does not relax genuine immutable
boundaries. Continue to freeze and bind independently authored fixture inputs
and independent oracles only where a current accepted plan authorizes them;
ADR-0035 does not authorize that work during M1 or M2. Continue to bind
accepted public package bytes and provenance, exact external-effect
manifests immediately before admission, durable evidence for any known or
possible effect, final accepted implementation/evidence, and contracts at their
accepted maturity boundary.

Runtime effect authority comes from a closed typed manifest plus durable
coordinator-owned admission, use, settlement, and terminal state. Git may bind
the reviewed implementation named by a manifest, and inert preflight may check
that binding and worktree state. Branch/HEAD state, commit subjects, log order,
pickaxe results, line attribution, and historical message discovery never grant
permission, stage state, predecessor acceptance, or marker ownership at
runtime.

## User-facing orchestration reports

Every implementation-orchestrator or replacement-orchestrator prompt must
require user-facing reports that lead with the practical outcome and explain:

1. what was accomplished conceptually;
2. how it fits into the current package, owning work package and slice, active
   milestone, and wider Infinium product path;
3. verification evidence, with dense commands and identifiers after the
   conceptual explanation;
4. material failures, deviations, or corrections, distinguishing recovered
   implementation defects from genuine authority or safety stops;
5. every issue requiring project-owner input, why current authority cannot
   decide it, the viable options and practical consequences, and a recommended
   choice; and
6. the next action and whether it proceeds automatically or waits for the
   owner.

If no material deviation or owner input exists, say so explicitly. At an owner
checkpoint, ask one concrete decision question and state exactly what remains
blocked. Technical implementer and reviewer prompts may remain narrow, but the
orchestrator must synthesize their results in this form rather than requiring
the owner to reconstruct meaning from logs, hashes, or internal terminology.

## Historical archival separation

Do not create a new archive tree inside the active repository. When historical
material is no longer required for current authority, navigation, build,
verification, provenance, or recovery, propose its transfer as a separate
owner-authorized package into an explicit sibling archive repository. The
proposal must identify exact source paths, destination repository and paths,
Git identities and content digests, active references to update, recovery
method, and post-transfer validation.

Archival preference is not deletion authority. Do not move, delete, rewrite,
or inspect an existing sibling archive merely because material appears
historical. Until the project owner accepts the exact transfer, retain the
material in place and keep ordinary implementation independent of it.
