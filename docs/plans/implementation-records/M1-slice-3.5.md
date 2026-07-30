# M1 Slice 3.5 implementation record

Status: **Blocked pending owner authority.** The implementation and
review/fix/re-review corrections are complete, but the accepted plan requires
explicit owner approval before validation answers may be public. No such
approval is recorded for `BETH-LIGHT-VAL` or `BETH-UNSUPPORTED-VAL`.

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

## Accepted deliverables and identities

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
| `BETH-LIGHT-VAL` | validation | `87fe85c62449405115fd64a001613397d51b5c94f05685c5158a52a0105ec462` | `1f24b89829da01f393e68a4f63c6d8dfc34a1739c9e07a4d9f9db6f3ffd76b89` | `d6bb10a7c6437fca8757c58737569fec52609edfc0164d536de3c4f0632a7a21` | `b04f365b381622ff9ddba199b5a16216604b56598c77e06ea1c6554ca2817bf5` |
| `BETH-MALFORMED-VAL` | development (reclassified) | `7489505a7c3648d65f920158d5bbed9a8fb1751772197778c11d0ac670cebbb5` | `b5a8541ef8b371f5fed34d217abb762f640998c53e877c1e8086e9da6673e0c0` | `193885bb79dbb4aba04173ecdeb46b4808a643c3053b655fd67ada2b04644768` | `b5f3d9e7b5523ac758d346b459053eb12119144bd1d2610c8ef93a5c96c6e24b` |
| `BETH-UNSUPPORTED-VAL` | validation | `00255c0e246bbb79d958a3eb45b2cbc13ca03949bfc882d4295e65a9f6a5143c` | `eca5c4decd7584c25b1aecffffd1a4286dc8777954925dc7d7da9fcd82c738b7` | `7d84beb04ab5d7549ea96557cc7310e45d21e944ff76a930c80242ef4cca4045` | `f661aea998ea76a65116c2ddc2733e9cb95387e31319e7ad5c975a1581a4b959` |

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

The materially independent evaluator-private replacement
`BETH-MALFORMED-VAL-002` is sealed under the ignored evaluator store. Its
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

The five original ignored Slice 3.5 construction/review scratch roots and the
generated Python cache were removed after final verification. The evaluator
store intentionally retains `BETH-MALFORMED-VAL-002`, including the accepted
v3 sealed package and the two rejected-review incident records, under ignored
`artifacts/evaluator-private/` for audit and replay. The final repository scan
found no tracked private locator, answer-bearing replacement payload, secret,
or generated cache.

## Commit

Implementation commit:
`5f1fc199c51e9a0125ccf7400b1064b8c17ed4ef`
(`test: qualify M1 Slice 3.5 Bethesda fixtures`).

Follow-up review commit message:
`fix: address M1 Slice 3.5 review findings`. Its exact SHA is reported in the
handoff because a commit cannot contain its own identity.

Push state: no push authorized or performed.
