# M1 Slice 4 — Bethesda semantic extraction and typed indexes

Status: Implementation complete with passing public gates; held-out acceptance transferred to Slice 4.5

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

## Fresh implementation closeout

Implemented: 2026-08-01

Accepted plan revision: `infinium.plan.m1.backend-semantic-proof/2`

Accepted plan SHA-256:
`9d3eeaf32b078e19340c6a08ae65413339fcb1e2f58f1b84f4f71e56b4d88bd4`

Fresh-attempt baseline:
`a63f5a1b1d55127a9fc89a3788a74b291850cca7`

Implementation commit:
`98fe8a5a173116427bf78077673fd10e8d018103`

### Delivered contract

- added a Mutagen `0.54.2`-only Bethesda semantic extractor for the accepted
  snapshot's ordered plugin winners, with structural bounds, stable read seals,
  declared/cumulative decompression limits, and no environment discovery;
- added canonical plugin/record identities, master and light-master FormKey
  translation, contributions, override chains, winners, typed links, reverse
  indexes, and explicit unsupported/coverage-gap results;
- added the qualified NPC AI/package/template/appearance, RACE, REFR
  relation/ownership/placement, and loose FaceGen facts, including closed
  FaceGen applicability precedence and snapshot-authoritative loose-provider
  chains;
- added subject-specific taxonomy projections that keep AI, appearance,
  placed-reference, provider-topology, and unsupported surfaces distinct;
- added durable immutable run-operation intent, accepted-snapshot resolution,
  plugin byte seals, contained-worker execution/recovery, strict staged-result
  validation, and CAS publication through the coordinator boundary;
- made worker Job Object membership atomic at process creation so a coordinator
  crash cannot orphan a suspended worker holding staging authority; and
- added unit, contract, integration, evaluation, security, and fault coverage
  for supported, malformed, unsupported, compressed, light-master, worker,
  recovery, tamper, bound, non-mutation, and taxonomy behavior.

No first-party Bethesda semantic parser was introduced. First-party code only
performs bounded framing/preflight and independently sealed test-oracle work;
Mutagen remains the sole production semantic interpreter for supported records.

### Evaluation evidence

EVAL-0052 passed against every scenario in the qualified public
`BETH-NPC-DEV`, `BETH-REFR-DEV`, and `BETH-LIGHT-VAL` packages, plus the
`BETH-MALFORMED-VAL` and `BETH-UNSUPPORTED-VAL` negative families. Exact checks
cover participant admission, record state, allowlisted field presence/count,
AI data, template/configuration links, RACE flags, REFR placement and links,
master/light FormKeys, chains, winners, receipts, hashes, lengths, malformed
failure, and explicit unsupported gaps. The contained-worker publication path
was also exercised end to end from an authoritative published snapshot.

The Slice-4-applicable EVAL-0086 path passed using independently derived byte
facts, expected taxonomy assignments, subject-specific forbidden assignments,
accepted reason codes, provenance, and retained taxonomy version. No
fixture-, mod-, NPC-, race-, zone-, or title-specific rule exists in production.

The six closed FaceGen applicability branches, loose provider order/winner,
partial and missing pairs, exact archive absence, full-plugin origin, and
light-master origin under a flagged ESP winner were exercised. Archive-positive
FaceGen remains outside the qualified Slice 4 surface.

The evaluation artifacts are the versioned fixture manifests/oracles and the
test results above. No `artifacts/m1-evaluation` run identifier was produced:
the milestone-level `evaluate` and `verify-evaluation` CLI entry points remain
owned by the later evaluation-harness work and were confirmed unavailable at
this slice boundary. This is not treated as an EVAL-0052 or EVAL-0086 bypass;
their Slice 4 assertions run directly in the retained evaluation test project.

### Verification

Final post-correction commands from repository root:

| Command | Result |
| --- | --- |
| `dotnet restore Infinium.sln --locked-mode --nologo` | Passed; all projects up to date. |
| `dotnet build Infinium.sln -c Release --no-restore --nologo` | Passed; 0 warnings, 0 errors. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Unit"` | Passed; 88 passed, 1 expected platform-dependent skip. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Contract"` | Passed; 25 passed. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Integration"` | Passed; 32 passed. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Evaluation"` | Passed; 41 passed, 8 expected evaluator-private Slice 3/3.5 skips. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Security"` | Passed; 9 passed. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Fault"` | Passed; 13 passed. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=M1Unit"` | Passed; 95 passed, 1 expected platform-dependent skip. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=M1Contract"` | Passed; 39 passed. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=M1Integration"` | Passed; 33 passed. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=M1Evaluation"` | Passed; 59 passed, 8 expected evaluator-private Slice 3/3.5 skips. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=M1Security"` | Passed; 86 passed, 3 expected skips. |
| `dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=M1Fault"` | Passed; 86 passed, 3 expected skips. |
| `dotnet test Infinium.sln -c Release --no-build --nologo` | Passed; 239 passed, 9 expected skips. |
| `dotnet run --project src/Infinium.Cli -c Release --no-build -- evaluate --manifest test-data/manifests/m1-suite.json --output artifacts/m1-evaluation` | Confirmed unavailable at this slice boundary; usage output, exit 2, no artifact produced. |
| `dotnet run --project src/Infinium.Cli -c Release --no-build -- verify-evaluation --input artifacts/m1-evaluation` | Confirmed unavailable at this slice boundary; usage output, exit 2. |
| `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal` | Passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check` | Passed. |
| `git diff --check` | Passed. |

Focused post-fix stress evidence: the coordinator crash/recovery integration
test passed five consecutive iterations with zero remaining contained-worker
processes; the CLI named-pipe lifecycle flow passed three consecutive
iterations after its independent replay/cancellation checks were ordered around
the actual `Running` boundary.

### Review, corrections, and final disposition

Independent semantic and boundary reviewers examined the implementation and
tests against the accepted plan, ADRs, evaluation cases, fixture contracts,
anti-overfitting rules, security, and non-mutation requirements. Review found
and corrected field-presence default leakage, compressed-input bound handling,
FaceGen precedence/coverage, taxonomy cross-area projection leakage, durable
recovery and aggregate input sealing, exact oracle coverage, an atomic Job
Object containment race, its error-path handle cleanup, and two nondeterministic
accumulated lifecycle tests. Affected checks were rerun after each correction.
Both independent reviewers returned PASS on the final diff.

No evaluator-private fixture was read by the implementation/review agent. The
eight expected private skips belong to already completed Slice 3/3.5
environment qualification; Slice 4 uses the qualified public Bethesda matrix
and independent public oracles. The legacy archive was not accessed.

### Sanitized held-out disposition

A fresh-context evaluator was delegated under ADR-0026 and
`evaluator-private-fixture-governance.md` after the implementation commit. It
returned this sanitized attestation:

- product commit:
  `98fe8a5a173116427bf78077673fd10e8d018103`;
- built `Infinium.Bethesda.dll`: 157,696 bytes, SHA-256
  `dc8ae44627fa40ca3937e4022c8e7914468e4d7a4cf1c40797a22ef2abec3655`;
- sealed held-out fixture: `BETH-HO-002/1.1.0`;
- private-store revision:
  `1530a55c6b30db45356fd54700c2d4ebb497c1c6`;
- package fingerprint:
  `87630b91225fa4bf1817845878290d6800237231c5413ebc3f9f3753a81d48fc`;
- oracle fingerprint:
  `382d8ab02c1e88adfadea75ae83b8ca1c15ebb710972bbb8c8c282170bc0bc15`;
- outcome: **blocked / unscored**; zero held-out assertions executed;
- contamination state: clean; raw answer disclosure: none.

The blocker is an absent documented scorer entry point in the sealed store,
combined with the not-yet-implemented milestone `evaluate` and
`verify-evaluation` CLI workflow. The evaluator correctly did not invent a
substitute harness. The public repository was unchanged by evaluation; no push
or external mutation occurred. The evaluator-private access record is retained
only in that store.

Consequently, the implementation and public Slice 4 gates are complete, but
Slice 4 is not administratively accepted. Acceptance requires a separately
authorized fixture-maintenance/evaluator action to add or identify a documented
sealed scorer, followed by scoring this exact implementation commit (or a
reviewed successor) and retaining its sanitized pass attestation. This blocker
must not be worked around by exposing held-out inputs or answers to production
agents.

Known explicit gaps are archive-positive/BSA semantics, localized strings,
NIF/PEX/native/generated/configuration analysis, non-allowlisted record or
field semantics, and environment discovery. They remain unsupported or
coverage gaps rather than inferred facts. No candidate, finding, documentation
claim import, provider call, credential, UI, recommendation, or other Slice 5+
work was started. No protected setup root or external state was mutated, no
push occurred, and no billable call was made.

## 2026-08-04 evaluator-v2 plan correction

M1 plan revision `/3` records Slice 4 complete at implementation commit
`98fe8a5a173116427bf78077673fd10e8d018103` after the current detached
locked-build and full-suite rerun passed and the post-candidate core-runtime
diff was confirmed empty. Evaluator-v1's blocked/unscored attempts produced no
product verdict and are retired under the incident record and ADR-0027.

Held-out EVAL-0052 and applicable EVAL-0086 gate ownership is transferred to
Slice 4.5. This transfer is not a waiver: Slice 5 remains blocked until a
qualified/frozen evaluator v2 and private corpus produce one valid held-out
`PASS` for the exact frozen tuple.
