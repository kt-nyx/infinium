# M1 Slice 3.5 implementation record

Status: **Complete.** This record was created before project-authored Bethesda
fixture construction began and closed after the final review/fix/re-review.

Opened: 2026-07-30

Closed: 2026-07-30

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

A separate taxonomy reviewer will be assigned after the binary-author slot is
released. The binary author may construct deterministic bytes and
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
three construction defects before acceptance:

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
| `BETH-NPC-DEV` | development | `ebe1b80fec4726bae4b9a2c35eb5b4a85d0e62d8df2668506fe94e91fdc149ec` | `b5523b8397f523f0812ed8b32cc4a08a9f5940b6e1010ca0ad11a2484ddba043` | `dae6507a391769d3ee59cdd75b019a489a535a16bf2b997033835e18cb2b2365` | `1b17ea09a33adfaf542a179fa197b60993df5bbb3ce4c3dc0990392ba46256ef` |
| `BETH-REFR-DEV` | development | `fd1396d36fb1cd161fd93dd84470265c0fc481bea4edbe80b5cad7b802367ad8` | `35db59142d784fe16a2d235697a7604c041897abbe3d649c26c00a73d31a88b5` | `22cd9fa3c124c9ef15de6c26c5406320a2eb8925c329119b0b77f74fbf2cec0b` | `370a60e30c572947116172b8d18cab8b052012ddbdcef3dde10f48079b9c3e97` |
| `BETH-LIGHT-VAL` | validation | `87fe85c62449405115fd64a001613397d51b5c94f05685c5158a52a0105ec462` | `72454b3073f15ab512782b82a0fb84e3fd81da51b40356807a2135f3611f2de5` | `d0031fc83ec1e934fbd001e6794883af5d6999015a764afcfdc1564b921164dd` | `2fc7eb094e1efe1f45cb8366d3e559df6f899991a5ae1e9784655b0f79288f6d` |
| `BETH-MALFORMED-VAL` | validation | `7489505a7c3648d65f920158d5bbed9a8fb1751772197778c11d0ac670cebbb5` | `8a4e24068b57efdc9ffd18f329b3afb3558633624d996f0f1c93073497c2b7c9` | `faa13b6249f4f3ab8f492a8f68213924406e17d5a040900f2e3e34070ebd2162` | `80534154b6f9c4d098a08558635aa5086b3e894a4db35c55efc49496fd6a40db` |
| `BETH-UNSUPPORTED-VAL` | validation | `00255c0e246bbb79d958a3eb45b2cbc13ca03949bfc882d4295e65a9f6a5143c` | `5947faf795fbacb39bd8a318084a2ce5f3c557524b6b3ed93190504a9a0680bd` | `1f3f2aa10e6c2d8281a4d8428abdf2af8d640dc9e414f9c51e1badb2aefa4a5f` | `bb0a50603f9e90e651e13524995c111112f1551e4f46c1f5c703273cf24cf1d0` |

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

## Independent oracle and taxonomy results

The independent raw reader and separately implemented PowerShell hexadecimal
worksheet reproduced byte-identical reports for all five packages. An
isolated-copy oracle rebuild then reproduced every supplemental byte oracle
and pre-taxonomy expected oracle byte-for-byte. Final independently reviewed
fact counts are:

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
- independent raw-reader/manual comparison — all five reports byte-identical;
  isolated oracle rebuild reproduced all five outputs.
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

## Intentional behavior changes and limits

`FixturePackageReader` now transitively validates package-relative retained
artifacts under `inputs/` and `oracle/`, including scope/traversal, reparse
point, single-link, size, existence, and SHA-256 checks. When the independent
Bethesda supplemental oracle is declared, the reader also enforces exact input
file coverage, physical byte coverage, canonical fact hashes, ground-truth
method agreement, scenario order, mutation partitions, and explicit
observed/unknown TES4 metadata state.

This slice adds no production Bethesda parser, Mutagen integration, typed
index, analyzer, candidate/finding logic, model/provider call, UI, QUST/alias
semantics, archive-member semantics, localized-string resolution, or automatic
environment discovery. Production comparison remains pending Slice 4.
`EVAL-0052`, `EVAL-0086`, and M1 are **not** reported as passed.

Five verified ignored disposable roots remain under `artifacts/` because the
host command policy rejected the approved recursive cleanup commands. They are
not staged, do not affect the Git worktree, and include the selective private
verification cache; removal remains a local hygiene follow-up.

## Commit

Implementation commit: this focused commit; its exact SHA is recorded in the
handoff because a commit cannot self-contain its own object ID.

Push state: no push authorized or performed.
