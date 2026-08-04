# M1 Slice 3.5 implementation record

Status: **Complete.** Slice 4 later completed at `98fe8a5`; the 2026-07-30 implementation and review record below is
preserved as historical evidence. Its public-answer blocker and ignored-store
topology were resolved by the owner-approved 2026-08-01 ADR-0026 amendment
recorded at the end of this document. Statements below that Slice 4 had not
started describe the historical point at which they were written.

Supersession note: References below to `BETH-LIGHT-VAL` or
`BETH-UNSUPPORTED-VAL` as validation, and to an ignored evaluator store, state
the 2026-07-30 condition only. They are not the current fixture topology.

Opened: 2026-07-30

Review correction completed: 2026-07-30

Plan: [M1 backend semantic proof plan](../milestones/M1-backend-semantic-proof.md),
accepted repository revision at implementation start, SHA-256
`9D3EEAF32B078E19340C6A08AE65413339FCB1E2F58F1B84F4F71E56B4D88BD4`

Slice plan:
[M1 Slice 3.5 — Bethesda fixture qualification](../slices/M1-slice-3.5-bethesda-fixture-qualification.md),
accepted revision, SHA-256
`90A3ACADF4F0FC7EA5D965FE6406D7302D853F0D6C737B9BBFDFFDC0EDA37832`

Starting commit:
`1e0cfbf63c0bd4af0ed463463933e3a39f56d140`
(`docs: add M1 Slice 3.5 execution plan`)

Slice: 3.5 — Bethesda binary fixture qualification

## Pre-construction role separation

The answer-bearing duties were assigned to distinct agents before bytes were
constructed:

- slice integrator: `/root`;
- binary fixture author: `/root/binary_fixture_author`;
- independent oracle author/reviewer: `/root/oracle_reviewer`;
- sealed held-out custodian: `/root/holdout_custodian`.
- materially independent malformed replacement input author:
  `/root/replacement_malformed_author`;
- materially independent malformed replacement oracle reviewer:
  `/root/replacement_malformed_oracle`.

The taxonomy reviewer was `/root/taxonomy_reviewer`. The binary author may
construct deterministic bytes and
non-answer-bearing construction metadata, but may not author the independent
oracle. The oracle reviewer may inspect only frozen bytes and public
construction boundaries, not generator answer tables or production parser
output. The held-out custodian may not inspect the development fixture
generator or development package bytes and may disclose only non-answer public
metadata and fingerprints to the repository.

## Preflight

- Worktree: clean `main`, 34 commits ahead of `origin/main`; no push is
  authorized.
- Toolchain: .NET SDK `10.0.302`, .NET runtime `10.0.10`, PowerShell `7.6.3`,
  Node `24.11.1`, npm `11.6.2`, Windows x64.
- Exact admitted MO2 `2.5.2` and Skyrim SE `1.6.1170.0` executable identities
  are present. Slice 3 is complete and accepted.
- All ten official master dependencies required by the Gate C manifests are
  locally retained and match their accepted byte lengths and SHA-256 values.
- All six evaluator-private Gate C archives are locally retained and match
  their accepted byte lengths and SHA-256 values.
- Selective read-only extraction into the ignored `artifacts/` workspace
  confirmed the accepted byte identities for all seven private plugin inputs
  and the four selected FaceGen assets. No private payload or absolute private
  path will be committed.
- No MO2, Skyrim, xEdit, Mutagen, provider, network, or billable process was
  launched. No protected installation or profile root was written.

## Frozen boundaries

This slice may add project-authored redistributable fixture bytes, strict
seven-document fixture packages, independent raw-byte oracle evidence,
taxonomy projections, snapshot-capture integration tests, and sanitized
private-dependency evidence. It may not add production Bethesda parsing,
Mutagen integration, typed plugin indexes, candidate/finding logic, QUST or
alias semantics, xEdit automation, model/provider calls, UI work, or other
later-slice implementation.

`EVAL-0052` and `EVAL-0086` cannot be reported as passed in this slice. This
slice qualifies and prepares their inputs; later analyzer execution remains
required.

## Independent review loop

The binary author first produced deterministic matrices and stopped editing
before the oracle reviewer inspected bytes. Independent raw-byte review found
seven construction and contract defects before acceptance:

1. two malformed members had enclosing group sizes that made their intended
   inner truncated-record/subrecord failures unreachable;
2. NPC members with a non-null template link still had zero raw template
   flags and therefore did not exercise the preregistered templated state; and
3. two REFR controlled-mutation members changed unrelated masters, compression
   framing, or relation fields, so they were not the claimed DATA-only and
   record-order-only mutations; and
4. substitute mutation filenames changed canonical current-origin identities,
   so record-order/local-ID comparisons also had to preserve the baseline
   plugin basename in isolated nested directories; and
5. the first supplemental byte-oracle schema collapsed unknowable master-list
   and ESL-flag observations on a ten-byte truncated file into concrete
   empty/false values. The uncommitted contract was corrected to require
   explicit `observed` or `unknown` state with null for unknowable values; and
6. the independent oracle builder originally derived the repository root from
   the package directory depth, which broke isolated-copy replay. It now
   derives the repository root from its own tracked script location; isolated
   rebuilding reproduced all five supplemental and expected-oracle files
   byte-for-byte before taxonomy projection; and
7. the first staging pass exposed Git line-ending normalization of retained
   JSON. The byte-exact fixture tree is now marked `binary` in
   `.gitattributes`; all 138 staged package files were proven byte-identical
   to their working-tree artifacts before commit.

Each discrepancy was reported before oracle answers were accepted. The
integrator rejected relabeling or laundering the cases, reopened only the
affected generator boundary, required focused invariants, and issued a new
byte freeze before independent oracle work resumed. Exact final hashes and
review results are recorded below after the final re-review.

## 2026-07-30 accepted deliverables and identities (historical)

Generator: `Infinium.BethesdaFixtures.Generator` v1, fixed seed
`3520260730`. Final `Program.cs` is 63,453 bytes with SHA-256
`ab71a0485005d544c5792499c645a7975641f55d8dd3c4fced7c04b0fd2cd5f1`;
the project file is 308 bytes with SHA-256
`f360a93248ae4a6a92176c50f85eba13e630c3f64af23ad970b395cb0028b04e`.
Two clean isolated generations were byte-identical and every generated byte
was covered by a construction region.

| Package | Partition | Construction manifest SHA-256 | Input package fingerprint | Oracle fingerprint | Snapshot receipt SHA-256 |
|---|---|---|---|---|---|
| `BETH-NPC-DEV` | development | `ebe1b80fec4726bae4b9a2c35eb5b4a85d0e62d8df2668506fe94e91fdc149ec` | `ded1d9da71a50e63cfb90fcaaf5f280a573f355af110d18040ebab22ff5c005e` | `b3037693dea1151590d057bb96551f077d0c0fda1eb2a7417f8cf9d7f964fbdf` | `b45cb0e3c5d321aa2eb22a39dea05dedb33ca4cc19efdc276d64d4d12a3d2e9a` |
| `BETH-REFR-DEV` | development | `fd1396d36fb1cd161fd93dd84470265c0fc481bea4edbe80b5cad7b802367ad8` | `72b478377bc55875daa055e1c2f7b6ea9d1340f4ddcaad6b2f24dc7a21f0bf33` | `e7266d7294dfa9a28dbc440c5ce24cd2ba48b7a97ba5dff0bd3cf55a4db5d578` | `9cf89bde1e53e723500f84c36b8c623c11c7200ff4faf6a90ff33943a0ad1b48` |
| `BETH-LIGHT-VAL` | validation (historical; now development) | `87fe85c62449405115fd64a001613397d51b5c94f05685c5158a52a0105ec462` | `1f24b89829da01f393e68a4f63c6d8dfc34a1739c9e07a4d9f9db6f3ffd76b89` | `d6bb10a7c6437fca8757c58737569fec52609edfc0164d536de3c4f0632a7a21` | `b04f365b381622ff9ddba199b5a16216604b56598c77e06ea1c6554ca2817bf5` |
| `BETH-MALFORMED-VAL` | development (reclassified) | `7489505a7c3648d65f920158d5bbed9a8fb1751772197778c11d0ac670cebbb5` | `b5a8541ef8b371f5fed34d217abb762f640998c53e877c1e8086e9da6673e0c0` | `193885bb79dbb4aba04173ecdeb46b4808a643c3053b655fd67ada2b04644768` | `b5f3d9e7b5523ac758d346b459053eb12119144bd1d2610c8ef93a5c96c6e24b` |
| `BETH-UNSUPPORTED-VAL` | validation (historical; now development) | `00255c0e246bbb79d958a3eb45b2cbc13ca03949bfc882d4295e65a9f6a5143c` | `eca5c4decd7584c25b1aecffffd1a4286dc8777954925dc7d7da9fcd82c738b7` | `7d84beb04ab5d7549ea96557cc7310e45d21e944ff76a930c80242ef4cca4045` | `f661aea998ea76a65116c2ddc2733e9cb95387e31319e7ad5c975a1581a4b959` |

Every tracked package contains the required seven root documents plus retained
inputs and independent oracle evidence. The real `FixturePackageReader`
accepted all five packages, including transitive retained-artifact hashes,
supplemental byte-oracle semantics, taxonomy subject references, replay state,
partition history, and manifest fingerprints.

The sealed evaluator-private `BETH-HO-001` registry entry retains public-only
metadata: input fingerprint
`f4eafbf876ce1a44f8deb5eff488162de786d9bcb8bd79cb3e7afd52437104bf`,
oracle fingerprint
`c9f251f988f5a197423eab744b97623b00b4a6164e338c457e9d8958eab23796`,
and a custodian attestation covering byte-identical reconstruction,
two-method agreement, schema validity, answer isolation, and absence of
development/production/third-party access. No partial held-out directory,
private locator, input, or answer is tracked.

At the historical 2026-07-30 closeout, the materially independent
evaluator-private replacement `BETH-MALFORMED-VAL-002` was sealed under the
ignored evaluator store. Its
tracked public-only registry binds root execution input
`ed7464c7fb4b6c852abab0e86eb1015a60370e3821f613ac882b6bf8fc6ff31f`,
oracle
`9fc07fde7c50b6891acb760581b16048f6f6a18618a40d8a9d957ec7f0f92612`,
and independent-method agreement
`32b502cf7b1ee56a07cc1e00843c032f32dd7d8248a82e1801e8d7cfeb5f7f49`.
Its 14 project-authored frozen inputs total 507,645 bytes and retain separate
set fingerprint
`3c51511d6db4b77848dcf2a855c9a66c8bbb3ad356599132c6c83b2d35d5a6e5`.
The accepted v3 reviewer used independent PowerShell and Node raw-byte methods,
validated all applicable schemas and cross-document hashes, and did not inspect
the predecessor fixture, rejected reviewer outputs, production code, provider
state, protected roots, or external payloads.

## Independent oracle and taxonomy results

The independent raw reader and separately implemented PowerShell hexadecimal
worksheet produced intentionally different evidence reports. Their normalized
structural observations agreed for all five packages; an isolated-copy oracle
rebuild then reproduced every supplemental byte oracle and pre-taxonomy
expected oracle byte-for-byte. Final independently reviewed fact counts are:

- NPC: 15 files, 376 facts, 7 complete mutation partitions;
- REFR: 16 files, 303 facts, 5 complete mutation partitions and 3/3
  malformed-subrecord denominator;
- LIGHT: 11 files, 108 facts, 6 complete mutation partitions;
- MALFORMED: 19 files, 97 facts, 17/17 invalid denominator plus one
  changed-during-read partition; and
- UNSUPPORTED: 3 TES4 files, 24 facts, five explicit 1/0/1 gap denominators.

The separate taxonomy reviewer froze six canonical subjects and 23
assignments across `TAX-03A`, `TAX-03B`, `TAX-06`, `TAX-08`, `TAX-12A`, and
`TAX-12B`. Exact raw-fact or retained-snapshot evidence supports each axis.
Provider/winner topology produces no technical-surface or delivery assignment
for `TAX-08`; unsupported, unknown, and not-applicable states remain distinct.

## Snapshot and dependency qualification

All five project-authored input sets crossed the accepted Slice 3 static
snapshot boundary through disposable synthetic roots with adapter
`infinium.mo2-static-reconstruction/v3`, contract `3.0.0`, exact plugin and
provider order, and no MO2/USVFS launch. A same-size, same-timestamp payload
mutation leaves the structural capture complete but invalidates the retained
payload receipt; a size change between structural passes returns
`ChangedDuringCapture`. Source fixture bytes were unchanged.

All six evaluator-private Gate C archives, ten official masters, seven
selected private plugin members, and four selected FaceGen assets matched the
accepted RESEARCH-0035 lengths and SHA-256 values during preflight. The final
member recheck passed `11/11`. No private payload or absolute locator is
tracked. No MO2, Skyrim, xEdit, Mutagen, provider, network, or billable process
was launched, and the final protected-process audit found none running.

## Verification

Final review/fix/re-review results:

- `dotnet restore Infinium.sln --locked-mode --nologo` — passed; already
  up-to-date.
- `dotnet build Infinium.sln -c Release --no-restore --nologo` — passed with
  0 warnings and 0 errors.
- `dotnet test Infinium.sln -c Release --no-build --nologo` — passed:
  188 passed, 9 skipped private/platform-conditional tests, 0 failed.
- `--filter "Category=M1Contract"` — 22 passed, 0 failed.
- `--filter "Category=M1Integration"` — 22 passed, 0 failed.
- `--filter "Category=M1Evaluation"` — 26 passed, 8 skipped, 0 failed.
- `--filter "Category=M1Security"` — 9 passed, 0 failed.
- `--filter "Category=M1Fault"` — 13 passed, 0 failed.
- `--filter "TestCategory=M1Security"` — 63 passed, 3 skipped, 0 failed.
- `--filter "TestCategory=M1Fault"` — 72 passed, 3 skipped, 0 failed.
- `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal`
  — passed.
- `eng/update-dependency-manifest.ps1 -Check` — passed.
- deterministic generator verification — passed two-run byte identity,
  construction-region coverage, controlled byte-diff/record-order/origin
  basename invariants, 21 earliest-failure checks, and 4/4 templated variants.
- independent raw-reader/manual comparison — the distinct reports agreed on
  the normalized observations checked by the original builder; isolated
  oracle rebuild reproduced all five outputs.
- tracked package qualification and snapshot tests — 4/4 passed.
- retained-artifact contract tests — 16/16 passed, including hardlink
  rejection.
- taxonomy semantic audit — 6 subjects / 23 assignments passed.
- private selected-member verification — 11/11 passed.
- path-leak and prohibited implementation-dependency scans — clean.
- all 138 staged byte-exact fixture artifacts matched their unfiltered
  working-tree bytes; `git diff --check` passed after the binary attribute
  fix.

The first repository-wide test run found the new schema missing from the
closed schema registry. The first staging audit found line-ending conversion
would invalidate package hashes. Both issues were fixed, the applicable checks
were rerun, and the full matrix above is the post-fix result.

## Follow-up comprehensive review

The review of implementation commit
`5f1fc199c51e9a0125ccf7400b1064b8c17ed4ef` found the following material
in-scope issues:

1. The manual hexadecimal worksheet did not independently decode every
   answer-bearing semantic projection, while the oracle builder attributed
   accepted facts to both methods. Mutation partitions also classified changes
   from artifact dependency rather than logical baseline deltas. The worksheet
   now performs independent semantic decoding, the builder requires exact
   agreement for every published fact, malformed partial traversal is
   explicitly non-attributed, and mutation partitions use semantic deltas.
   Corruption and exact AIDT/DATA changed-set tests cover the failure.
2. Retained artifacts were validated through separate path opens and only
   lexical pre-open containment, permitting path races and phase drift. The
   reader now pins the Windows scope, rejects reparse/hard-link/ADS/device and
   case-alias boundaries, reads each unique artifact once with bounded count
   and aggregate bytes, verifies final handle identity and unchanged metadata,
   and passes the exact validated snapshot to Bethesda parsing. Tests cover
   junction swaps, growth, replacement between phases, malformed supplemental
   JSON, canonical IDs, repeated-reference consistency, and resource bounds.
3. The snapshot receipt was derived from construction metadata rather than
   independently bound to the actual Slice 3 capture result. The test now
   computes the binding from the actual capture order and captured winner-byte
   hashes. A same-size/same-time payload mutation preserves structural capture
   but invalidates the content binding.
4. Bethesda package closure, retained input-byte accounting, and public-only
   held-out metadata had coverage gaps. Bethesda packages now require exactly
   seven root documents, exact unique retained input-byte totals, and strict
   public registry fields. These Bethesda-only rules were initially applied too
   broadly during the correction and broke the accepted Slice 3 evaluator
   package; the accumulated suite caught that regression, the rules were
   narrowed to packages declaring the supplemental Bethesda oracle, and a
   cross-slice regression test now passes.
5. `BETH-MALFORMED-VAL` had influenced generator corrections but claimed
   otherwise. Its immutable identity is retained, its partition history now
   records a validation-to-development transition, and a materially independent
   evaluator-private `BETH-MALFORMED-VAL-002` replacement was separately
   authored and reviewed. Only sanitized fingerprints and attestations are
   public.
6. The implementation record overstated byte-identical independent reports and
   omitted exact closeout traceability. The reports are intentionally distinct;
   the corrected claim is exact agreement on independently decoded accepted
   semantics, with byte-identical isolated oracle reconstruction.
7. The accepted plan permits tracked validation answers only with explicit
   owner approval. No such approval is recorded for `BETH-LIGHT-VAL` or
   `BETH-UNSUPPORTED-VAL`. Their existing public answer exposure cannot be
   repaired by silently relabeling them or by deleting current files after they
   entered Git history. This is an owner-authority blocker, not an engineering
   workaround opportunity.
8. The first replacement oracle reviewer completed and sealed private evidence,
   then disclosed that a final repository-wide text search had exposed generic
   tracked fixture paths and disclaimer lines. A second fresh reviewer stopped
   after a package-root enumeration exposed names in the rejected reviewer
   directory. Neither exposure revealed case answers, but both violated the
   preregistered isolation rule, so neither attestation was accepted. Their
   ignored artifacts are retained as rejected audit evidence; a third reviewer
   was given a literal file-level allowlist and a separate private output root.
   The third reviewer completed two byte-identical 30-file constructions and
   passed strict schema, cross-hash, method-replay, answer-isolation, and
   locator-sanitization checks. Review then caught an input-set fingerprint
   incorrectly occupying the root execution-input fingerprint field; the
   reviewer separated those identities, rebuilt twice, and revalidated before
   the replacement was accepted.

The review did not find Slice 3.5 persistence, migration, credential, provider,
cost-control, or cancellation behavior to implement: those surfaces are absent
from this fixture-qualification slice. No production parser, live analyzer,
provider call, protected-root access, or later-slice capability was added.

### Follow-up verification

Final commands and results after every correction:

- `dotnet restore Infinium.sln --locked-mode --nologo` — passed; all projects
  up to date.
- `dotnet build Infinium.sln -c Release --no-restore --nologo` — passed with
  0 warnings and 0 errors.
- `dotnet test Infinium.sln -c Release --no-build --nologo` — passed:
  200 passed, 9 skipped private/platform-conditional tests, 0 failed.
- `--filter "Category=M1Contract"` — 24 passed, 0 failed.
- `--filter "Category=M1Integration"` — 22 passed, 0 failed.
- `--filter "Category=M1Evaluation"` — 26 passed, 8 skipped, 0 failed.
- `--filter "Category=M1Security"` — 9 passed, 0 failed.
- `--filter "Category=M1Fault"` — 13 passed, 0 failed.
- `--filter "TestCategory=M1Security"` — 74 passed, 3 skipped, 0 failed.
- `--filter "TestCategory=M1Fault"` — 82 passed, 3 skipped, 0 failed.
- `dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity
  minimal` — passed.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  eng/update-dependency-manifest.ps1 -Check` — passed.
- `dotnet run --project
  tools/evaluation/bethesda-fixtures/Infinium.BethesdaFixtures.Generator.csproj
  -- verify --seed 3520260730` — passed two-run byte identity, complete
  construction-region coverage, and controlled mutation/order/origin
  invariants.
- `python
  tools/evaluation/bethesda-fixtures/independent-review/self_test.py` — passed
  deliberate FormKey/link/chain/manual-evidence corruption rejection and exact
  AIDT/DATA logical mutation partitions.
- The independent Python reader, PowerShell worksheet, and oracle builder were
  rerun against isolated copies of all five tracked packages. Each pair of
  reports remained intentionally distinct, exact semantic agreement passed,
  and both `independent-byte-facts.json` and `expected-oracle.json` rebuilt
  byte-for-byte.
- `node tools/evaluation/bethesda-fixtures/snapshot-receipts.mjs` and
  `node tools/evaluation/bethesda-fixtures/finalize-packages.mjs` — passed
  deterministic receipt/package regeneration.
- Focused retained-artifact contract tests — 26 passed; focused Bethesda,
  snapshot, evaluator-private registry, and prior Slice 3 regression tests —
  7 passed.
- Accepted private replacement v3 — two corrected 30-file constructions were
  byte-identical; both raw methods agreed on 14/14 inputs; eight applicable
  schemas, canonical facts, expected-item bindings, cross-hashes,
  answer isolation, locator sanitization, and changed-during-read replay
  passed. Final sealed-tree fingerprint:
  `ca5cc6b07afb227dc3e537feb647dcdc45bc5b79441773934dc43a2b7b5a05fb`.
- Tracked-diff path/credential scan, prohibited production-path scan,
  protected-process audit, `git diff --check`, and final semantic/diff review —
  clean.

Intermediate correction evidence is retained in this record: the first build
found nullable/analyzer errors in new tests, the first combined focused run
found repeated snapshot references and incorrect held-out boolean
expectations, and the first accumulated suite found the Bethesda-only root and
byte-accounting rules had been applied globally. Each cause was fixed, covered
by regression tests, and included in the final green matrix above.

## Intentional behavior changes and limits

`FixturePackageReader` now transitively validates package-relative retained
artifacts under `inputs/` and `oracle/`, including canonical IDs,
scope/traversal, pinned Windows final-path containment, reparse point,
single-link, count and byte bounds, unchanged handle identity, existence, and
SHA-256 checks. Repeated logical references reuse one validated snapshot.
When the independent Bethesda supplemental oracle is declared, the reader
also enforces exact seven-document root closure, exact retained input-byte
accounting, input file coverage, physical byte coverage, canonical fact
hashes, ground-truth method agreement, scenario order, mutation partitions,
and explicit observed/unknown TES4 metadata state.

This slice adds no production Bethesda parser, Mutagen integration, typed
index, analyzer, candidate/finding logic, model/provider call, UI, QUST/alias
semantics, archive-member semantics, localized-string resolution, or automatic
environment discovery. Production comparison remains pending Slice 4.
`EVAL-0052`, `EVAL-0086`, and M1 are **not** reported as passed.

At the historical 2026-07-30 closeout, the five original ignored Slice 3.5 construction/review scratch roots and the
generated Python cache were removed after final verification. The evaluator
store intentionally retains `BETH-MALFORMED-VAL-002`, including the accepted
v3 sealed package and the two rejected-review incident records, under ignored
`artifacts/evaluator-private/` for audit and replay. ADR-0026 later superseded
that storage topology. The final repository scan
found no tracked private locator, answer-bearing replacement payload, secret,
or generated cache.

## Commit

Implementation commit:
`5f1fc199c51e9a0125ccf7400b1064b8c17ed4ef`
(`test: qualify M1 Slice 3.5 Bethesda fixtures`).

Follow-up review commit:
`1d9b006bb66021a76d4e3171f5abae3836741896`
(`fix: address M1 Slice 3.5 review findings`).

Push state: no push authorized or performed.

## 2026-08-01 ADR-0026 corrective amendment

Owner direction resolved the public-answer blocker by accepting
[RESEARCH-0052](../../research/investigations/RESEARCH-0052-evaluator-private-fixture-repository-and-agent-access.md),
[ADR-0026](../../architecture/decisions/ADR-0026-evaluator-private-fixture-repository-and-delegated-access.md),
and semantic specification revision
[`infinium.eval.m1.semantic-and-ground-truth/2`](../../evaluation/specifications/m1-semantic-and-ground-truth-v2-amendment.md).
The accepted milestone-plan correction is
[`infinium.plan.m1.backend-semantic-proof/2`](../milestones/M1-backend-semantic-proof-adr0026-amendment.md).
The correction was reviewed from base commit
`1d9b006bb66021a76d4e3171f5abae3836741896`; it did not start Slice 4.

### Material findings and corrections

1. `BETH-LIGHT-VAL` and `BETH-UNSUPPORTED-VAL` had publicly committed
   answers and could not remain independent validation evidence. Both now
   retain append-only validation-to-development transitions and bind sealed,
   independently authored `-002` replacements.
2. The private malformed replacement lived only under disposable ignored
   product artifacts. A separate Git history was established, the 75-file tree
   was proven byte-identical to its private canonical copy, and the legacy copy
   was moved intact to private migration evidence. No private package remains
   under Infinium `artifacts/evaluator-private/`.
3. The first pinned public contract bundle omitted `common.v1.schema.json`.
   Restoring its exact 13,936 bytes exposed and allowed correction of closed-
   schema defects in artifact references, partition history, timestamps,
   collection states, and one held-out local-ID representation.
4. Registered held-out package `BETH-HO-001` had no retained complete package
   in either permitted store. Its public v1 registry is retained byte-for-byte
   and explicitly invalidated; fresh input and oracle roles independently
   authored, reviewed, and sealed `BETH-HO-002` without predecessor or
   production access.
5. The migrated malformed package's legacy validator assumed its former
   directory depth. Its sealed bytes remain unchanged; a repository-level
   explicit-package-root validator now resolves the pinned schemas and fresh
   replay passes.
6. Documentation review found a material rewrite of accepted specification
   `/1`, stale future-slice/blocker/storage statements, missing ADR authority,
   and contradictory locator rules. `/1` is preserved; accepted revision `/2`,
   the canonical governance document, updated authorities/status indexes, and
   a metadata-only-by-default bootstrap now record the correction.

### Private store and sanitized identities

The local private Git repository was initialized at root commit
`416b8e966cf251f022e0b45ede1f38a572393a4e` and finalized at
`d929f7c91e0f36cc2e57240bda63c6371c7528ee`. It has no configured remote and
was not pushed. Infinium records no path, remote URL, credential, raw input,
oracle content, or raw result.

| Private package | Partition | Declared input package | Retained input set | Oracle | Package |
|---|---|---|---|---|---|
| `BETH-LIGHT-VAL-002/1.0.0` | validation | `b0f756bb402d0305401ef3ee90017cbbe24e56fc34f7343bbd74e0c49ac1e69d` | `a8d32590d417de20977da700e8d905b5158bfa5dd84655988533235a0a21d79a` | `e704d9f587985f8e10d83428521023736e679f211ecb1daae51793913c64f705` | `3c5cc30657cbcdc04e49b02ff2de45a39cb4a7155b65a2c1996d62a18c28cece` |
| `BETH-MALFORMED-VAL-002/1.0.0-private-input-freeze` | validation | `3c51511d6db4b77848dcf2a855c9a66c8bbb3ad356599132c6c83b2d35d5a6e5` | `3c51511d6db4b77848dcf2a855c9a66c8bbb3ad356599132c6c83b2d35d5a6e5` | `6d14e1dc4da3a201d72632d4768da6caa56564f01fa0f8b1ee3c19e90ae83ce4` | `064b5639a73b7f8657b64381f8b6f401918bfbc8c83b6c585e847785eab68a07` |
| `BETH-UNSUPPORTED-VAL-002/1.0.0` | validation | `3f4b34b1f77ca0b70e2b431ae991075f032b29beed825ebf980de9ab4b9221d9` | `74f4ba7a7bb5cb024bc8d719e5c4875dceb916670893ec928744c06fcab1ea14` | `9f47bf6e2deecd61015487e5e4f8b6959279f9b37ffd3d2c11d0b7119452f525` | `0f609cc259605bf47ecce58465c043b2e7cca6e074a6b133a5729a19c106130e` |
| `BETH-HO-002/1.0.0` | held-out | `ac85caeecba3a91d01f062510f62b5dded65b78e732e8c9b21e97ae0a5f21f25` | `3f9c360c8e473929a1bf42403cec1fe537de4b8f5121e4116359c09f71e41146` | `b14fefe688d942b7e888c4e8b26df74311731560743492c11b0030f7c70eb33a` | `b1974b093d2622285a1ce3c5de024adfd0016c80e63fe5a1a96bd31cd94697bb` |

The sanitized publication is 20,828 bytes with SHA-256
`b02c902da8ae2188be2639c9e3c2d714bbf65621f906ca78581da0804a92fc71`.
Custodian verification passed all seven root schemas plus supplemental oracle
schemas, exact inventories and document/package bindings, deterministic
reconstruction, fresh evidence replay, two-method agreement, answer isolation,
access/contamination records, and path/credential/network/private-leak scans
for all four packages.

### Current public development package identities

| Package | Partition | Construction manifest | Input package | Oracle | Snapshot receipt |
|---|---|---|---|---|---|
| `BETH-NPC-DEV` | development | `ebe1b80fec4726bae4b9a2c35eb5b4a85d0e62d8df2668506fe94e91fdc149ec` | `346438915e0fd8a9d78db7f86b9e8e9c493fe116fe6831633c4e1ef0a8e5fa85` | `b3037693dea1151590d057bb96551f077d0c0fda1eb2a7417f8cf9d7f964fbdf` | `b45cb0e3c5d321aa2eb22a39dea05dedb33ca4cc19efdc276d64d4d12a3d2e9a` |
| `BETH-REFR-DEV` | development | `fd1396d36fb1cd161fd93dd84470265c0fc481bea4edbe80b5cad7b802367ad8` | `6a46834cf0bd23122c8db0a32625f5dd5448d4df38bbe6ae9bd5ccdcf0b85d5e` | `e7266d7294dfa9a28dbc440c5ce24cd2ba48b7a97ba5dff0bd3cf55a4db5d578` | `9cf89bde1e53e723500f84c36b8c623c11c7200ff4faf6a90ff33943a0ad1b48` |
| `BETH-LIGHT-VAL` | development | `19ae9c6c38b9cabb8d9821e53f02fd139016a8aa014f652442179e893e099904` | `f836b993d6b6fb501b062d1b188ac5a852e522c0bd4288e8bbe29a156de6a3f9` | `d6bb10a7c6437fca8757c58737569fec52609edfc0164d536de3c4f0632a7a21` | `339aea99bd649e46a8400366eb912c8410d2f806cf821106113dbd9f2334af8d` |
| `BETH-MALFORMED-VAL` | development | `e1876cf888c065740d59a711fa2f597f1551cbaaf09ccf45f40cc40ec4ba761c` | `3efe7be2d117f30b607ec53323fa990fecde4b194d4945d82149f9b99a617b54` | `193885bb79dbb4aba04173ecdeb46b4808a643c3053b655fd67ada2b04644768` | `35fc4e1c55b3c45dc88a64acb00cbeda91055d4f57daf8cbd6ca03ee5c7eac9f` |
| `BETH-UNSUPPORTED-VAL` | development | `d44bec01403d1bc12e22990fba7087d521e45e7ef09f57cd589ac7a63d16f93e` | `2dfd228013cfce9813307d5d8c0403b69b8eb900754a4bd4df054dcbd5ae51e6` | `7d84beb04ab5d7549ea96557cc7310e45d21e944ff76a930c80242ef4cca4045` | `232981443d6c5258a79018b4c34680cf4d0f67283523b4e748eac4ea4ef160ab` |

`EVAL-0052`, `EVAL-0086`, and M1 remain unpassed; production comparison is
still pending Slice 4.

### Delegated role and access traceability

The fresh-context evaluator roles remained distinct from the implementation
context and from each other. They received only the pinned public contract and
the minimum frozen private input required for their role; no answer-bearing
material crossed back to implementation.

| Package(s) | Input-author role | Oracle-reviewer role |
|---|---|---|
| `BETH-LIGHT-VAL-002/1.0.0`, `BETH-UNSUPPORTED-VAL-002/1.0.0` | `isolated-input-author-private-input-author` | `fresh-context-private-oracle-reviewer` |
| `BETH-HO-002/1.0.0` | `isolated-input-author.private-holdout-input-author` | `fresh-oracle-reviewer.private-holdout-oracle-reviewer` |

The input authors could use the pinned public contract and their own input
construction, but not predecessor answers, other expected-value tables,
production source/output, or prior reviewer answers. Oracle reviewers could
use the pinned contract bundle and frozen input bytes, but not authoring or
generator sources, expected-value tables, production material, other
fixtures, prior reviewer output, the abandoned archive, Mutagen/xEdit,
protected roots, network, or live providers. Each oracle was authored after
input freeze and checked through two independent methods.

Public-safe access evidence:

| Access-record ID | Bytes | SHA-256 |
|---|---:|---|
| `2026-08-01-slice-3-5-replacements-input-author` | not disclosed | `0884692511b729b92549c1897cb6026d3763869d0a9dbf65c27892d7f3d06747` |
| `2026-08-01-slice-3-5-replacements-oracle-reviewer` | not disclosed | `32ae172330e2ad60435c2e150da441a4179f4ff79cd5f9b42057defe5e3f86cf` |
| `20260801T143738-0400-private_holdout_input_author-BETH-HO-002` | 1,344 | `e991c0047ad77665cade71efb74ab635333b98ae12963b1e2a77f2429b36eae3` |
| `20260801T185430Z-private_holdout_oracle_reviewer-BETH-HO-002` | 703 | `b7535d6441b272efc24a2d7e5230c78cf5d02043c7ce588225f4a06e1aa5cde1` |

The sanitized result artifact is retained as
`publication/m1-slice-3.5-sanitized-registry-metadata.json` in the separate
private store: 20,828 bytes, SHA-256
`b02c902da8ae2188be2639c9e3c2d714bbf65621f906ca78581da0804a92fc71`.
It reports that all access attestations passed without publishing a private
locator or answer payload. The held-out independence attestation
`BETH-HO-002.oracle-independence-attestation/1` is publicly identified by
SHA-256 `72552464b3ed005331f67b35f75d0f9b3e0ff0c174f8bde2312aa4841cac2ed7`.

### Amendment review findings

The final review/fix/re-review cycle found and corrected four additional
in-scope defects:

1. Editing the accepted parent milestone plan in place invalidated the Slice 2
   fingerprint contract. The original plan was restored byte-for-byte at
   SHA-256 `9d3eeaf32b078e19340c6a08ae65413339fcb1e2f58f1b84f4f71e56b4d88bd4`;
   the new policy is recorded only in accepted revision `/2`.
2. The held-out v2 supersession identified `BETH-HO-001` with version
   `unavailable` instead of its historical `1.0.0` identity. The version now
   matches the retained byte-identical v1 registry, while availability remains
   an invalidation state; a regression assertion binds both identities.
3. The metadata bootstrap used .NET's `Path.IsPathFullyQualified` and
   `ConvertFrom-Json -Depth`, neither available to Windows PowerShell 5.1.
   It now uses compatible absolute-path checks and JSON parsing. A hermetic
   test creates disposable separate Git repositories, runs the bootstrap under
   Windows PowerShell 5.1, proves metadata-only default output, and proves that
   the locator appears only with explicit delegation authority.
4. The amended plan inherited three nonexistent umbrella-document links and
   the amendment closeout lacked exact role/access traceability. The reading
   list now names real entry documents, and the tables above record the
   sanitized delegated identities and access evidence.

### Amendment verification and final re-review

Exact post-correction commands and results:

- `dotnet restore Infinium.sln --locked-mode --nologo` — passed; all projects
  were up to date.
- `dotnet build Infinium.sln -c Release --no-restore --nologo` — passed with
  0 warnings and 0 errors.
- `dotnet test Infinium.sln -c Release --no-build --nologo` — passed:
  201 passed, 9 skipped private/platform-conditional tests, 0 failed.
- `--filter "Category=M1Contract"` — 24 passed, 0 failed.
- `--filter "Category=M1Integration"` — 22 passed, 0 failed.
- `--filter "Category=M1Evaluation"` — 27 passed, 8 skipped, 0 failed.
- `--filter "Category=M1Security"` — 9 passed, 0 failed.
- `--filter "Category=M1Fault"` — 13 passed, 0 failed.
- `--filter "TestCategory=M1Security"` — 75 passed, 3 skipped, 0 failed.
- `--filter "TestCategory=M1Fault"` — 82 passed, 3 skipped, 0 failed.
- `dotnet test tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj
  -c Release --no-build --nologo --filter
  "FullyQualifiedName~BethesdaFixturePackageQualificationTests"` — 4 passed,
  0 failed.
- `dotnet run --project
  tools/evaluation/bethesda-fixtures/Infinium.BethesdaFixtures.Generator.csproj
  -- verify --seed 3520260730` — passed two clean-run byte identity, complete
  construction coverage, and controlled mutation/order/origin invariants.
- `python
  tools/evaluation/bethesda-fixtures/independent-review/self_test.py` — passed
  all deliberate corruption and logical mutation-partition checks.
- The bounded independent reader, manual PowerShell worksheet, and oracle
  builder were rerun from clean scratch against all five public development
  packages. All five passed exact semantic agreement and byte-identical oracle
  reconstruction.
- `node tools/evaluation/bethesda-fixtures/snapshot-receipts.mjs` and
  `node tools/evaluation/bethesda-fixtures/finalize-packages.mjs` — passed
  deterministic receipt/package regeneration and exact private-registry
  metadata binding.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  tools/evaluation/private-fixtures/Get-PrivateStoreDescriptor.ps1` and the
  same command with `-IncludeLocatorForDelegation` — passed under Windows
  PowerShell 5.1; default output was metadata-only and explicit delegation
  returned the pinned separate-store locator.
- `dotnet format Infinium.sln --no-restore --verify-no-changes --verbosity
  diagnostic` — passed with 0 files formatted.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  eng/update-dependency-manifest.ps1 -Check` — passed.
- Changed/new-document link audit — 26 documents passed with no unresolved
  relative links.
- Changed/new-file private-locator, user-profile, credential, credentialed-URL,
  and API-key-shape scan — 61 files passed with no hit.
- `git diff --check`, staged byte-exact object audit, repository-hygiene scan,
  and final semantic/diff review — passed.

The first accumulated run exposed the accepted-plan fingerprint regression;
the first explicit Windows PowerShell bootstrap run exposed the compatibility
failure. Both causes were corrected, covered, rerun, and included in the final
green results above. The corrected diff was then reviewed again against the
slice plan, ADR-0026, semantic amendment `/2`, held-out registry history,
private-store governance, Windows behavior, leakage boundaries, and later-
slice exclusions. No material in-scope issue remains.

Raw/public-safe outputs inspected were the five regenerated public fixture
packages and receipts, all five clean independent-reader/manual/oracle reports,
the two public registries plus the byte-identical v1 held-out registry, default
and explicitly delegated bootstrap descriptors, full and filtered test output,
Git diffs/status, and the sanitized private publication named above. Raw
private inputs/oracles remained visible only to their delegated roles.
The three generated public re-review scratch roots, regenerated Python cache,
and empty legacy evaluator-private artifact directories were moved intact to a
task-specific operating-system temporary directory after inspection; no
generated or private artifact remains in the product worktree.

Corrective main-repository commit: `fix: address M1 Slice 3.5 review findings`
(the commit containing this record; its SHA is reported in the final handoff
rather than recursively embedded in itself). Private-store commit:
`d929f7c91e0f36cc2e57240bda63c6371c7528ee`. Neither repository was pushed.
Slice 4 was not started.

## 2026-08-01 pre-Slice-4 readiness audit

Slice 3.5 was already marked complete in this record and every top-level plan
and status index. A fresh audit checked the accepted M1 plan and revision `/2`,
all Slice 0 through 3.5 implementation records, the evaluation catalog and
specifications, fixture manifests, research/deferred-risk indexes, public
registries, private-store sanitized metadata, current source boundaries, and
the accumulated Release verification set.

The audit corrected these remaining contradictions and defects:

1. EVAL-0045, EVAL-0051, and EVAL-0054 retained passing evidence but still
   appeared pending on isolated catalog/research surfaces. Those surfaces now
   agree with the accepted Slice 3 record.
2. The semantic fixture inventory still said no evaluation had passed and
   described the EVAL-0054 package as uncreated despite its own retained Slice
   3 evidence. It now distinguishes fixture acceptance from the separately
   passed Slice 3 cases.
3. Specification `/1` could be misread as requiring every later M1 package
   before Slice 4. Revision `/2` now clarifies that Section 17 applies
   package-by-package: the accepted Slice 3/3.5 inputs close the Slice 4 start
   boundary, while later packages remain dependencies of their own slices.
4. The first accumulated suite run reproduced an orphaned worker after an
   abrupt coordinator crash. The Slice 2 record above contains the cause,
   bounded-RPC correction, strengthened recovery cleanup, and ten-run
   regression evidence.

Final current results:

- locked restore and Release build passed with 0 warnings and 0 errors;
- full Release suite: 201 passed, 9 expected skips, 0 failed;
- `Category=M1Unit`: 81 passed, 1 expected skip;
- `Category=M1Contract`: 24 passed;
- `Category=M1Integration`: 22 passed;
- `Category=M1Evaluation`: 27 passed, 8 expected private skips;
- `Category=M1Security`: 9 passed;
- `Category=M1Fault`: 13 passed;
- `TestCategory=M1Security`: 75 passed, 3 expected skips;
- `TestCategory=M1Fault`: 82 passed, 3 expected skips;
- Bethesda package qualification: 4 passed;
- deterministic Bethesda generation, independent corruption self-tests,
  snapshot receipt regeneration, package finalization, Windows PowerShell 5.1
  private-store bootstrap, and sanitized private-store revision/tag/integrity
  checks passed; and
- no production Mutagen parser, typed index, EVAL-0052/EVAL-0086 execution,
  provider call, protected-root access, or Slice 4 behavior exists in the
  current diff.

No pre-Slice-4 blocker remains. EVAL-0052 and the applicable EVAL-0086
assertions are correctly retained as Slice 4 execution gates rather than
prerequisites to beginning it. Later M1 evaluation packages, milestone-wide
`evaluate`/`verify-evaluation`, live-provider work, broader MO2/Skyrim
surfaces, and excluded archive/string semantics remain later or unsupported
work and do not reopen Slice 3.5.

## 2026-08-01 Option A fixture-conformance correction

The project owner selected RESEARCH-0053 Option A after the first Slice 4
attempt proved that public fixture version `1.0.0` used two noncanonical
Bethesda structures. Correction commit
`c371259198ab232a8b7e3f719ae4496a12039d4b` preserves ADR-0009 and pinned
Mutagen `0.54.2`, replaces the public suite with immutable version `1.0.1`,
and retains the original semantic truth:

- Skyrim `RACE/DATA` now uses the full 128-byte shape with flags at offset
  `0x20`;
- format-valid `REFR` records now use interior CELL block, sub-block, child,
  and persistent-child containment; and
- malformed controls were repaired so their declared framing or link defect
  is the first and only relevant defect.

The public package bindings are:

| Fixture | Input package SHA-256 | Oracle SHA-256 |
|---|---|---|
| `BETH-NPC-DEV/1.0.1` | `bdb977812329e0440cc18f22f00deb129fd43d5e3e1f6a89a78dd0085ff9e2fe` | `c59eeba5236703f1d51f2cc2834c2df49d8cf8a77a7d5f4775203988cd02e137` |
| `BETH-REFR-DEV/1.0.1` | `ed6c4e7f4477219da1aa9ee49036c5d29ac57c942938d61daad40ab24c7bfdc1` | `2be4e699f32a90387c030ffb160ab5bb855826101e3c54f02d05917cbb7228e4` |
| `BETH-LIGHT-VAL/1.0.1` | `096e3420705af5e97216e1483aa010bcd2c2ad58734b12ce1bfc410cba934fc9` | `5b8740018a0f54497dbf2c4c3398bd385bd6902453551b01b9d7a8656e483857` |
| `BETH-MALFORMED-VAL/1.0.1` | `d0cf9179c352feab4550bebc3661f8f3988b3dc3fae1511d1e02a5bfa420219c` | `7e2ac9c97870c0e51511dc44a9242fcee1e22db82c7b504f5fe1d7cb4e9b9093` |
| `BETH-UNSUPPORTED-VAL/1.0.1` | `01e284f5c1edeab436c46761f5f321077d025291f3b94f6309c8030b1ed63eec` | `2452943ee592b22838ea7cad3851ce16c65b88e6fd2ee07bab21ffc51f7157d4` |

Under ADR-0026, a fresh-context private custodian audited all four active
private packages. Three had an analogous structural defect and were replaced
by immutable `1.1.0` successors: `BETH-HO-002`,
`BETH-MALFORMED-VAL-002`, and `BETH-UNSUPPORTED-VAL-002`.
`BETH-LIGHT-VAL-002/1.0.0` was unaffected and remains the control. The private
correction is commit `1530a55c6b30db45356fd54700c2d4ebb497c1c6`; its sanitized result reports
unchanged semantic truth, clean contamination state, 5/5 held-out plugins
parseable, 14/14 malformed targets preserved with two-method agreement, both
20,480-record complexity controls fully visible, and the unsupported plugin
and placed record visible. No private locator or answer payload was published.

Final correction verification:

- locked restore and Release build passed with 0 warnings and 0 errors;
- full Release suite: 204 passed, 9 expected skips, 0 failed;
- `Category=M1Unit`: 81 passed, 1 expected skip;
- `Category=M1Contract`: 24 passed;
- `Category=M1Integration`: 22 passed;
- `Category=M1Evaluation`: 30 passed, 8 expected private skips;
- `Category=M1Security`: 9 passed;
- `Category=M1Fault`: 13 passed;
- focused Bethesda package and Mutagen conformance tests: 7 passed;
- deterministic generator verification, independent-reader self-tests,
  receipt regeneration, and package finalization passed;
- `dotnet format --verify-no-changes`, dependency-manifest validation, and
  `git diff --check` passed.

## 2026-08-02 accepted-order construction-role maintenance amendment

The project owner authorized a third bounded Slice 3.5 correction after the
sealed scorer audit showed that public fixture version `1.2.0` retained the
accepted-order construction receipt without a dedicated execution-input role.
Public version `1.3.0` adds required
`accepted_order_construction_input` to the pinned execution schema. The five
canonical Bethesda identities must provide exactly one role reference to
`inputs/snapshot/accepted-order.json`; declaration downgrade, unresolved or
swapped controls, neighboring installation/runtime-order substitutions,
duplicate payload references, stale metadata, wrong receipt schema,
fixture/version drift, and source-basis drift fail closed.

The receipt is now closed under
`infinium.evaluation.bethesda-accepted-order-construction-input/v1` and the
fixed `accepted-slice-3.5-construction-manifest-and-retained-input-seals`
source basis. It is the authoritative accepted provider/plugin-order
projection-construction receipt, not an installation snapshot or runtime
plugin-order input. Those two neighboring roles are explicitly
`not-applicable` in all five static project-authored packages. Taxonomy
projections must carry an accepted-order source entry exactly equal to the
normalized execution-role reference as well as the resolved retained bytes.
Reader and finalizer validation also require the declared construction-manifest
fingerprint, canonical ordered provider/plugin bijection and provider IDs,
selected-versus-isolated construction sets, every internal retained SHA-256
seal, and the recomputed canonical capture-binding fingerprint to agree
exactly. Fully resealed internal-seal drift and installation/runtime-role
overclaims therefore fail before scoring.

The public package bindings are:

| Fixture | Input package SHA-256 | Oracle SHA-256 |
|---|---|---|
| `BETH-NPC-DEV/1.3.0` | `a0bccc6c081acc0fed4fe50291e9002121a1545fae61f258a3d3ac10033ec001` | `b6af9058f047d9afd10c908b1151d24120f69d2ccaabe623c22b873d9b16d25a` |
| `BETH-REFR-DEV/1.3.0` | `a8410d5bd8616f26034746cf5911930c7ab289bd38ec48fe3f45bde46395d58e` | `d9f3f634faf4725d6feaa260931584f4ecebf00973570ae19b90a096cbcd91bb` |
| `BETH-LIGHT-VAL/1.3.0` | `e75a1719fc24b21feac80a9f42846400700d6356a2057815cdbb9e055709844f` | `c05efacf5ed4953c0a7b7492521234fdc0f296b7899e998122050956a6bc2937` |
| `BETH-MALFORMED-VAL/1.3.0` | `239b5d4b7bad864e6ab52c4c650c89b757d7d262775c899f573c42e3dc1856c9` | `dcf9af9ffd16e3ebdce0b5ee2d9491920790bfb3014bef3f3be4f732bfb51568` |
| `BETH-UNSUPPORTED-VAL/1.3.0` | `59e5d868a7eb403b229b5fbf9e1bb9836ef4e02300962c610d098606e3364273` | `abc78e718a9ab68095e68e5032138ce2dd425993fa985c6e73f784bf01789429` |

Public verification for this correction:

- fixed-seed generator verification passed; a staged SHA-256 comparison
  proved every generated `.esm`, `.esp`, `.esl`, and mutation byte unchanged;
- all five independent raw-reader and manual-hex reports were rebuilt and
  reconciled, with exhaustive taxonomy counts preserved at 17/14/3;
- independent self-test and exact two-source taxonomy replay passed;
- package finalization passed twice with stable seals; each package has exact
  physical/reference input closure and exact one-time byte accounting
  (`20/26629`, `21/27583`, `15/16663`, `24/195822`, `12/11694`);
- locked restore and Release build passed with 0 warnings and 0 errors;
- full Release suite: 248 passed, 9 expected skips, 0 failed;
- `Category=M1Unit`: 88 passed, 1 expected skip;
- `Category=M1Contract`: 29 passed;
- `Category=M1Integration`: 32 passed;
- `Category=M1Evaluation`: 41 passed, 8 expected private skips;
- `Category=M1Security`: 9 passed;
- `Category=M1Fault`: 13 passed;
- all 73 contract tests and all 50 evaluation tests passed, with only the 8
  expected evaluator-private skips; and
- `dotnet format --verify-no-changes`, Windows-PowerShell dependency-manifest
  validation, and `git diff --check` passed.

This public maintenance pass accessed no evaluator-private fixture repository,
did not score Slice 4, did not change accepted semantic truth or production
analyzer behavior, and did not commit or push. Private supersession,
independent resealing/requalification, scorer audit, exact implementation
commit scoring, and Slice 4 closeout remain with their separately governed
roles.

The correction clears the fixture prerequisite for a fresh Slice 4 attempt.
It does not implement Slice 4, claim EVAL-0052 or EVAL-0086, authorize a
first-party parser, or broaden the accepted dependency/runtime surface.
Neither repository was pushed.

## 2026-08-02 sealed-scorer contract maintenance amendment

The project owner authorized a bounded Slice 3.5 maintenance amendment after
the first sealed-scoring handoff exposed two package-contract omissions:
taxonomy projections did not literally bind every sealed subject to its exact
Slice 4 production subject participant, and the expected oracle did not own
the complete physical oracle directory through an exact retained-reference
closure. The public fixture reader, schemas, qualification tests, independent
review tooling, and all five public Bethesda packages now enforce that
contract as immutable version `1.1.0`.

The three EVAL-0086 packages now carry an answer-free
`inputs/taxonomy-subject-bindings.json` and exhaustive independently authored
projection closure: 17 `BETH-NPC-DEV` subjects, 14 `BETH-REFR-DEV` subjects,
and 3 `BETH-UNSUPPORTED-VAL` subjects. Bindings are a bijection between sealed
subject IDs and literal production participant IDs; no heuristic or derived
matching remains in the public agreement test. All five packages reject
unreferenced oracle files, missing retained oracle files, empty oracle
directories, reparse points, stale fingerprints, and stale declared byte
lengths. Repeated retained references must also match their first occurrence
exactly in canonical artifact-ID spelling, artifact version, fingerprint,
availability, and optional byte-length presence and value.
The taxonomy projection's declared source set is exactly the retained
accepted-order receipt and independent byte facts, with unique, fully matching
reference metadata. Its builder replays from only those two sealed files and
does not reopen plugin bytes or independent-reader reports.

The public package bindings are:

| Fixture | Input package SHA-256 | Oracle SHA-256 |
|---|---|---|
| `BETH-NPC-DEV/1.1.0` | `732de2f73ee117be46c505fbf3beec374c6a6b4f468b79887aff336e427da380` | `74e71c731ddefd6f1950061f7f95ef30424161230ab8724354ebeec2a6053eb4` |
| `BETH-REFR-DEV/1.1.0` | `3730c93525278d8b9538b0f395f9ba3f99ec7dc231b5617459c1015f832a0d7d` | `4394f3346d8e33cea1f482eb50b3a2420b79d11e9eab3c689948eed88c48ffd2` |
| `BETH-LIGHT-VAL/1.1.0` | `56cf558bdc831b7e995a087accadc2527fb33be5473a3b7da2ce21d1b7d026e1` | `7d9785b2f1ec155b9f01f962f0882d7cd7c17bd8c26d819aea4c8ea80427da1c` |
| `BETH-MALFORMED-VAL/1.1.0` | `f0d120c7bde0d3c52b6564fe3ecd615c838645a3e949d835bbc86b93a45c5820` | `6866ace85810006e4b60be0f5146a766e6b69b099f921c80709105bd34852802` |
| `BETH-UNSUPPORTED-VAL/1.1.0` | `896bb2d672e4c8995068919ce21e5a03e0f3a674216043b3ef00dc01eeb051e8` | `5b943d680effa2db2b4f917c548ad9648df347ce4220998094658a733cd5473e` |

The public correction changes no Bethesda plugin bytes or accepted semantic
truth. Public deterministic regeneration, mutation coverage, scorer-facing
agreement tests, full verification, and the separately governed private
reseal/audit/score results are recorded in the Slice 4 closeout. Neither
repository is pushed by this amendment.

## 2026-08-02 case-matrix role maintenance amendment

The project owner authorized a second bounded correction after scorer audit
showed that public fixture version `1.1.0` conflated the answer-free execution
case matrix with `effective_scan_configuration`. Public version `1.2.0`
separates those roles without changing any Bethesda plugin byte or expected
semantic truth. The effective scan configuration is a retained
`infinium.scan.effective-configuration/v1` document with no case inventory;
the required `case_matrix_input` instead binds the retained closed
`infinium.evaluation.bethesda-case-matrix/v1` document.

The matrix derives only from the accepted Slice 3.5 scenario inventory and
retained project-authored execution inputs. It contains fixture/version
identity, unique scenario IDs, `scan`/`compare`/`request`/
`orchestrated-read` operation shape, and canonical retained input IDs. It does
not contain expected values, denominator classifications, oracle references,
or production output. Reader and finalizer checks require operation arity,
input population membership, exact `inputs/` physical/reference closure,
unique payload references, repeated-reference metadata agreement, and one-time
aggregate input-byte accounting.

The public package bindings are:

| Fixture | Input package SHA-256 | Oracle SHA-256 |
|---|---|---|
| `BETH-NPC-DEV/1.2.0` | `49bbd70d7588cc9936f4074f386afd83a87d6a343cde32b290d3a8ebd8740d6e` | `44806bb737753962a90961bb03d346fa1fb1b66cacbecf407a43397aa692c6ec` |
| `BETH-REFR-DEV/1.2.0` | `b37b334df1cebc0fc4c394c4d93968ea07cc10a6a99fd111c20568808a94dda1` | `c10cd6da61cbfe8842b47e4c601cdd058b02af662a68432501242183cb0b521d` |
| `BETH-LIGHT-VAL/1.2.0` | `bb559cae1bff2ba55639f405e950401048c55f76ee95b4dcd58a20a2a0645383` | `ec9951f42552ef2f291820329c4dd0bee3c10e36565c74110a20a551c02fbd1c` |
| `BETH-MALFORMED-VAL/1.2.0` | `78d3c81da2c368258039cd5523251284041ecf758e2700dcb400a7e59a909e62` | `51dbcd9363da440222dc25efc6a1fbaa5232f5458b88fec33d97ac76f54592e8` |
| `BETH-UNSUPPORTED-VAL/1.2.0` | `de3e48ba89128e34a6e5bd5f9674d5e10645689790789d2f274f6e9cff3cae09` | `174baf61cccde417e4f0ab140c682bae533240f48f89a739f9c35d18c1a08524` |

Adversarial contract coverage rejects missing/swapped role bindings, duplicate
or omitted payload references, unsealed scenario inputs, stale metadata, wrong
matrix schema, non-list `cases`, duplicate scenario IDs, unsupported
operations, and each operation's arity violations. The reader recognizes the
five accepted public Bethesda identities from a canonical exact registry, so a
fully resealed removal of the byte oracle, package-required taxonomy artifacts,
case matrix, or scan configuration cannot downgrade a protected package into
generic fixture handling. Public verification and the separately governed
private supersession/requalification/scoring results are recorded in the Slice
4 closeout. Neither repository is pushed by this amendment.

Public verification for this correction:

- locked restore and Release build passed with 0 warnings and 0 errors;
- full Release suite: 247 passed, 9 expected skips, 0 failed;
- `Category=M1Unit`: 88 passed, 1 expected skip;
- `Category=M1Contract`: 29 passed;
- `Category=M1Integration`: 32 passed;
- `Category=M1Evaluation`: 41 passed, 8 expected private skips;
- `Category=M1Security`: 9 passed;
- `Category=M1Fault`: 13 passed;
- all 72 contract tests and all 50 evaluation tests passed, with only the 8
  expected evaluator-private skips;
- fixed-seed generator verification, byte-for-byte generated-input comparison,
  independent-reader self-tests, five-package oracle regeneration, taxonomy
  replay, and package finalization passed;
- every package's payload-reference count equals its physical retained-input
  file count and its declared input-byte total equals the physical byte total;
  no `.esm`, `.esp`, `.esl`, or `.strings` fixture byte changed; and
- `dotnet format --verify-no-changes`, dependency-manifest validation, and
  `git diff --check` passed.
