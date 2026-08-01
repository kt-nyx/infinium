# M1 Slice 4 — Bethesda semantic extraction and typed indexes

Status: Blocked attempt closed; fixture prerequisite corrected for a fresh attempt

Plan: `infinium.plan.m1.backend-semantic-proof/2`

Attempted: 2026-08-01

Baseline commit: `e4e83963d7292f1c688f74970400d70a2acf84e6`

Implementation commit: none — no production implementation was retained

## Intended slice boundary

The accepted Slice 4 boundary requires:

- ordered plugin bytes supplied only by the accepted MO2 snapshot;
- Mutagen `0.54.2` as the positively allowlisted Bethesda semantic library;
- master/FormKey translation, override chains, winners, and links;
- the selected NPC/package/appearance, loose FaceGen, and REFR facts;
- explicit unsupported and coverage-gap results; and
- canonical participant identities and typed indexes without candidates,
  findings, later analyzers, archive/string authority, or environment
  discovery.

The formal slice gates are EVAL-0052 and the Slice-4-applicable BETH-linked
portion of EVAL-0086. The inherited worker, read-only, failure-isolation, and
dependency-invalidation rules also remain applicable.

## Preflight result

The repository was clean on `main`, 38 commits ahead of `origin/main`, at
`e4e8396`. .NET SDK `10.0.302`, runtime `10.0.10`, and the locked local
`Mutagen.Bethesda.Skyrim` `0.54.2` dependency were present. Slice 3.5's public
fixture packages, accepted-order manifests, and independent byte oracles were
available. The evaluator-private repository and the abandoned legacy archive
were not read.

The Slice 3.5 record contained no declared pre-start blocker. The blocker below
was discovered only by exercising the production Mutagen API against the
accepted public EVAL-0052 controls. Exact probe evidence and alternatives are
retained in
[RESEARCH-0053](../../research/investigations/RESEARCH-0053-mutagen-slice4-fixture-conformance.md).

## Blocking evidence

### B1 — the accepted RACE control is not consumable by Mutagen 0.54.2

A two-pass, no-data-folder Mutagen import using the exact supplied plugin order
successfully enumerated the public `BETH-NPC-DEV` population (seven NPC record
versions and three winners). The accepted minimal RACE control contains the
independently frozen four-byte `RACE/DATA` flags shape. Accessing the required
typed expression:

```csharp
race.Flags.HasFlag(Race.Flag.FaceGenHead)
```

throws `ArgumentOutOfRangeException` from
`RaceBinaryOverlay.GetFlagsCustom()`. Mutagen's generated overlay expects a
larger full-schema RACE DATA payload. EVAL-0052 positively requires resolved
RACE plus `FaceGenHead`; returning `unsupported` for this allowlisted fact is a
gate failure, not an acceptable coverage gap.

### B2 — the accepted REFR controls are invisible to Mutagen 0.54.2

The public `BETH-REFR-DEV` controls encode the independently frozen REFR shapes
inside a top-level `GRUP(REFR)`. Mutagen's Skyrim model exposes placed objects
only through CELL/worldspace child topology. Both of these accepted API paths
return zero REFR records for the package:

```csharp
mods.PlacedObject().WinningOverrides(includeDeletedRecords: true)
mod.EnumerateMajorRecords<IPlacedObjectGetter>()
```

The independent oracle simultaneously records the required REFR `NAME`,
`XLKR`, `XLRL`, `XOWN`, and `DATA` facts. Silently treating the population as
empty would fabricate coverage and fail EVAL-0052.

### Why no fallback was retained

A provisional bounded raw-record projector demonstrated that the frozen bytes
could be decoded, but independent review correctly rejected that route as the
production answer. It would make an Infinium-owned Skyrim semantic parser,
rather than Mutagen, authoritative for every required field and link. That is
a material architecture change to ADR-0009, not an implementation detail.

The same review identified two additional consequences if such a route were
ever authorized: decompression must enforce its bound while streaming rather
than after expansion, and all parsing/publication must execute through the
accepted contained worker and staged coordinator-validation boundary. The
provisional focused tests also compared only a subset of the frozen oracle and
could not support a complete EVAL-0052 claim.

All provisional production and test changes were removed. No substitute
parser, fixture-specific rule, partial typed index, or false gate pass remains
in the worktree.

## Required owner decision and follow-up

Slice 4 cannot continue under the current accepted package unchanged. One of
these paths requires explicit disposition before implementation resumes:

1. Correct and independently reseal the public and evaluator-private Bethesda
   fixtures as valid Skyrim structures consumable through Mutagen `0.54.2`,
   while preserving the intended independent assertions and anti-overfitting
   partitions.
2. Conduct research and accept an ADR amendment authorizing a narrowly bounded
   first-party field/record parser, including its authority, worker,
   decompression, malformed-input, and independent-oracle gates.
3. Qualify and accept another exact Mutagen version only if it consumes every
   required shape and passes the full independent EVAL-0052 matrix.

This decision must reconcile the plan, ADR-0009, EVAL-0052, fixture manifests,
and any affected public/private fixture revisions together. Ordinary Slice 4
implementation must not choose among them implicitly.

## Verification and review performed

- required product, architecture, evaluation, research, residual-risk, plan,
  and prior-slice records were reviewed in the mandated order;
- dependency/toolchain and clean-worktree preflight passed;
- exact no-environment Mutagen API shapes were compiled and probed against the
  public accepted fixtures;
- public RACE and REFR incompatibilities above were reproduced;
- the partial implementation received an independent semantic/diff review;
- partial raw-parser, taxonomy, bound, worker, and test-coverage defects were
  identified during review; and
- all partial code/test/lock changes were removed before this record.

No live provider call, billable action, protected setup-root mutation, external
application launch, direct evaluator-private access, legacy-archive access,
push, or remote mutation occurred.

## Closeout

Slice 4 is blocked, not complete. EVAL-0052 and applicable EVAL-0086 are not
claimed passed. The repository remains at the accepted pre-Slice-4 production
baseline plus this blocker record. The closeout below records the blocker-record
commit and final clean-baseline checks.

## Blocked-attempt closeout

Blocker-record commit:
`5655bcad116f7515e8aa34d829e67bc4b5e4b3c9`

Final checks on the restored pre-Slice-4 production baseline plus documentation:

```text
dotnet restore Infinium.sln --locked-mode
  passed; all projects up to date

dotnet build Infinium.sln -c Release --no-restore
  passed; 0 warnings, 0 errors

Category=M1Unit
  81 passed, 1 expected platform-dependent skip

Category=M1Contract
  24 passed

Category=M1Integration
  22 passed

Category=M1Evaluation
  27 passed, 8 expected evaluator-private skips

Category=M1Security
  9 passed

Category=M1Fault
  13 passed

git diff --check
  passed after closeout formatting correction
```

The independent re-review found no remaining material issue after RQ-004 was
reopened, RESEARCH-0053 was registered, and the provisional implementation was
removed. No push occurred.

## Post-closeout fixture disposition

On 2026-08-01 the project owner selected Option A from RESEARCH-0053. The
fixture-maintenance follow-up superseded public Bethesda fixture version
`1.0.0` with independently resealed `1.0.1`, corrected analogous public and
evaluator-private structures, and retained Mutagen `0.54.2` conformance tests.
ADR-0009 is unchanged.

The original blocked attempt and its no-implementation conclusion remain
historically accurate. The prerequisite blocker is now cleared for a fresh
Slice 4 attempt; no production implementation, EVAL-0052 pass, EVAL-0086 pass,
or later-slice authorization is claimed by the fixture correction itself.
