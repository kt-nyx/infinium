# M1 Slice 5 — Evidence, documentation, candidates, cases, and replay

Status: `M1/S5/WP1` complete and reviewed; `M1/S5/WP2` is eligible; Slice 5 is not complete

Plan: `M1/S5`

Work package: `M1/S5/WP1`

Started: 2026-08-07

Final review: 2026-08-08

Branch: `codex/m1-slice5-staged-verification-recovery`

Baseline commit: `fcf71e184b7544a964530d581792c4948d47cda6`

Implementation commit: `bf1d830706afb78204016d0b1054a83cdb1b79b4`

Recovery-amendment commit: `6badd958f01a53f892915d56dfb90af4c3ea299c`

Closeout commit: this record's final local commit

## Authority and owner amendment

WP1 started from the owner-accepted Slice 5 plan. After the first independent
review found an invalid product/evaluator conflation and inadequate generated
fixtures, the owner amended WP1 on 2026-08-08 to authorize one comprehensive
correction pass. That pass could replace the 28 Slice 5 packages, migrate
current predecessor-shaped public packages, retire legacy or misleading
evaluation assets, and create an enforceable product/evaluator authority
boundary. The amendment retained a hard stop if the final fresh review found
another material authority, model, fixture, security, or claim defect.

The final reviewer did find material defects. That historical correction loop
closed with WP2 blocked and no implementation commit or push.

## Accepted staged-verification recovery authorization

On 2026-08-08 the project owner explicitly superseded that hard stop for a
bounded recovery. The accepted recovery removes the rejected 28-package corpus
and fixture-only authority, preserves and reviews the independently valid
product-contract/migration/boundary foundation, and assigns semantic fixture
truth incrementally to the behavior-owning WP2-WP5 packages with comprehensive
cross-stage assembly in WP6. WP3 owns scale/stress construction and independent
expected counts. Product output remains prohibited as oracle truth, private
held-out work remains deferred, protocol `/4` remains frozen historical
bounded-regression evidence, and `/5` remains retired.

The recovery branch was created from baseline HEAD with the existing worktree
intact. An exact 727-record dirty-path inventory was written under ignored
`artifacts/m1-slice5/wp1-recovery/` before generated material was removed. The
baseline signature exactly matched the prior WP1 attempt (160 tracked changed
paths, 566 untracked files, 1,842 tracked insertions, and 7,525 tracked
deletions); no unrelated user work was identified.

## Corrected interpretation of legacy and protocol `/4`

`LegacyV1` was a pre-v2 public-fixture compatibility namespace compiled inside
the evaluator project. It was not protocol `/4`, and no evaluator command used
it. The earlier hard-stop explanation incorrectly treated a live-schema reader
problem as if frozen `/4` required current product fixture shapes.

Protocol `/4` is evaluator-side historical evidence. Its 20 frozen reusable
core files and three allowlisted evolved regression-test identities remain
unchanged. It is outside the default solution and is callable only through the
bounded regression wrapper. A bounded pass is historical-tool and public-
regression health, not a product, held-out, M1, reliability, or readiness
verdict.

Current product contracts, current public-fixture package versions, evaluator
protocol versions, and repository authority-manifest versions are separate
version axes. None can be used as a substitute authority for another.

## Completed boundary and retirement work

The correction pass completed these repository-boundary changes:

- moved current public-fixture readers into `Infinium.PublicFixtures`, backed
  by `ActiveJsonSchemaValidator`;
- removed `Infinium.EvaluatorV2` and protocol `/4` tests from the default
  solution, while retaining a dedicated out-of-solution bounded regression
  project;
- deleted the `LegacyV1` compatibility namespace, predecessor protocol `/3`
  schemas, obsolete pre-B2 proof scripts, obsolete held-out/private registries,
  and the private-registry-dependent fixture finalizer from the active tree;
- recorded every retired path by exact source-commit Git blob in
  `docs/evaluation/retired-evaluation-assets.v1.json`;
- replaced the old finalizer with the current-only, idempotent
  `tools/evaluation/reseal-current-public-fixtures.mjs`, which accepts only six
  exact current public package identities and pinned semantic-truth digests;
- added human and machine-readable product/evaluator authority maps plus
  closed repository schemas and automated reachability/Git-object checks;
- marked the retained 2026-07-29 Slice 3 evaluator package as historical
  evaluator evidence and removed live-product-schema validation of it; and
- removed evaluator-governance identifiers from product capability enums,
  leaving exactly `provider`, `hosted-search`, `nexus`, and `loot` as the
  product execution boundaries.

The final authority manifest is 3,052 bytes with SHA-256
`b4bc1e61e2934cfc7ea8f7df9ff49874b6c07f75bd704d9a2bc6f9cdbe13f81a`.
The retirement manifest is 6,530 bytes with SHA-256
`bbdbaca328027e5b82ccfdde3e71357d689cbeaf91e1a2ad242ca56a9cdf1c64`.
The current resealer is 29,868 bytes with SHA-256
`e62f79452ef7ded021c685d4bae7fe49b997f8bba03727aa6f7ce66761a781b5`.

## Current public-package migration

Six current public packages were clean-break migrated rather than treated as
legacy:

- `M1-PLAT-SLICE2-SUBSTRATE-v1` to package version `1.1.0`;
- `BETH-NPC-DEV`, `BETH-REFR-DEV`, `BETH-LIGHT-VAL`,
  `BETH-MALFORMED-VAL`, and `BETH-UNSUPPORTED-VAL` to package version `1.4.0`.

Each now has a complete answer-free analysis envelope, declaration,
configuration, replay manifest, retained byte lengths, and resealed closure.
Their six independently authored semantic-truth digests stayed exactly pinned.
Two consecutive resealer runs produced the same combined tree digest
`de256153d5d4ef4ead06b9b04a697153501d86b883a31556fc72db0cf6438eea`.

## Contracts, state model, and persistence

The recovered WP1 implementation contains the Slice 5 documentation,
candidate, finding/case, replay, and analysis-execution contracts in domain,
strict JSON, and additive protobuf forms. It extends analyzer declarations,
effective configuration, run output, CLI summaries, fixture/oracle envelopes,
replay dependencies, and assertion results as a single current clean-break
shape.

The state/invariant correction added the explicit `unknown` result state,
candidate population/decision/candidate uniqueness and one-to-one admission
closure, and prior-run identity requirements for incremental and retained-
downstream replay while prohibiting prior identity on clean replay.

Persistence adds schema-only migration `M1-S5-0004`, database schema `4`, and
storage contract `1.3.0`, including append-only analytical tables, traversal
indexes, and update/delete guards. No WP2+ producer, coordinator, worker, CLI,
query, or replay execution behavior was implemented.

## Historical rejected fixture construction

The rejected prior generator produced the exact 28 planned public package identities: six
development and 22 validation packages, with 484 registered documents and no
missing, extra, hash, or package-aggregate closure errors. The registry is
100,351 bytes with SHA-256
`905808119ea7568bb86e846baffada41e6957bbf843f5f9c1815bebf682de273`.
Its review state remains `pending-independent-review`; no package was
self-certified.

The frozen scale/stress generator identity document remains 1,385 bytes with
SHA-256
`ef54e1784273e7706ab53a25b95063aa77d7d0d48db82379c1da1fbbb79e86d1`.
Generator-only feasibility stayed within the preregistered ceilings, but that
mechanical fact does not establish executable or semantically sufficient
fixture inputs.

## Verification completed before the hard stop

| Check | Result |
|---|---|
| Locked restore and Release solution build | Passed; 0 warnings, 0 errors |
| Focused `Slice5StateModel` | 9 passed |
| Focused `Slice5Contract` | 4 passed |
| Focused `Slice5FixtureContract` | 6 passed |
| Full `Infinium.ContractTests` | 82 passed |
| Full `Infinium.EvaluationTests` | 45 passed, 8 environment-dependent private tests skipped |
| Repository boundary contract tests | 3 passed |
| Current-package resealer twice | Passed; byte-identical second run |
| `verify-m1-slice5.ps1 -Gate Contracts` | Passed, including 20 retired Git blobs and default-solution isolation |
| `verify-m1-slice5.ps1 -Gate GeneratorFeasibility` | Passed, generator-only claim |
| Bounded protocol `/4` wrapper | `BOUNDED_REGRESSION_PASS`; 23/23 historical blobs, 20/20 frozen core files, 3/3 evolved tests, 8/8 focused tests |
| `git diff --check` before final review | Passed |
| `FixtureIndependence` after final review | Correctly blocked because review is not terminal-passed |

Passing mechanical tests do not override the semantic review.

## Final independent review — material `FAIL`

The final reviewer read the accepted answer-free checklist before inspecting
the exact generated packages, did not edit, did not use product output as
truth, and did not access the private repository or abandoned archive. The
review found five material defect classes:

1. **Recursive answer isolation is still violated.** Product-facing
   relationship states are derived from expectation type/state, and the
   fixture-ID-derived harness seed is exposed through product inputs. Expected
   disposition, fixture identity, and fixture seed are required to remain
   harness-only.
2. **The exact typed oracle graph is not closed.** Seventeen causal rows refer
   to nonexistent product relationships; 18 case-to-finding/candidate links
   and 11 hypothesis/finding/recommendation/lineage links are broken. For
   example, `CASE-SHARED-DEV` retains only relationships `000001` and `000002`
   but its construction authority references `000001` through `000005`.
3. **Scale/stress inputs are not executable at their claimed populations.**
   They declare 100,000 and 2,000,000 relationships but retain only 64 rows,
   with no product-reachable expansion contract, while the oracle claims 1,000
   and 10,000 exact candidates.
4. **Taxonomy authority is incomplete.** The `TAX-*` inputs do not retain the
   author/surface/area/history evidence needed to derive their assignments,
   and `TAX-HISTORY-VAL` omits the required linked test-taxonomy `1.0.0` to
   `2.0.0` projection, mapping provenance, and immutable historical assignment.
5. **Concrete source/platform stimuli are not bound to exact results.** Source
   claims are synthesized from assertion prose rather than exact retained
   passages; lineage IDs do not join; and write/IPC/persistence oracles do not
   contain exact per-request decisions and relationships.

These failures violate the plan's recursive answer-isolation, exact typed
oracle, executable scale/stress, exact passage/provenance, and complete
taxonomy-history requirements. They are not test-only defects and cannot be
waived by closure, schema, hash, or build success.

## Historical failed-review boundary

The earlier owner amendment permitted no further fixture-led repair loop after
that material final-review defect. At that point:

- WP1 is incomplete and uncommitted;
- WP2 is blocked;
- the 28 replacement packages remain pending review;
- no Slice 5, M1, semantic-quality, reliability, readiness, or private
  held-out claim is made; and
- owner disposition was required before any new correction authority.

The accepted staged-verification recovery amendment now supplies that bounded
authority. It does not waive any known contract, migration, security,
answer-isolation, or frozen-evaluator defect; a material defect remaining after
the fresh recovery review is still a hard stop.

No evaluator-private repository file, private corpus, B2/C2/Stage D/scoring
surface, protocol `/5`, future protocol identity, abandoned legacy archive,
controlled-real package, live/billable provider, or Slice 6+ implementation
surface was accessed. Nothing was pushed.

## Recovery closeout — `M1/S5/WP1` complete and reviewed

The owner-authorized recovery completed on 2026-08-08. Commit
`6badd958f01a53f892915d56dfb90af4c3ea299c` accepted the staged-verification
amendment. Commit `bf1d830706afb78204016d0b1054a83cdb1b79b4` recovered the
product/evaluator boundary and WP1 contract, codec, state, protobuf, migration,
storage, current-public-package, and test foundation. The boundary and product
foundation were combined because the current public-fixture reader depends on
the active product schemas and validator; a narrower intermediate commit would
not have represented a buildable current authority surface.

### Rejected corpus removal and deferral

The premature comprehensive corpus was removed from the worktree and deferred
to its owning packages. It comprised these exact rejected package identities:

- WP2: `EVID-HOSTILE-VAL`, `EVID-NO-LLM-VAL`, `EVID-TYPES-DEV`,
  `PROV-CONTRADICTION-VAL`, `PROV-DELETION-VAL`, and `PROV-LOCAL-DEV`;
- WP3: `CAND-ATOMIC-DEV`, `CAND-INTEGRATION-VAL`, `CAND-SCALE-VAL`, and
  `CAND-STRESS-VAL`;
- WP4: `CASE-DISTINCT-VAL`, `CASE-LEAD-VAL`, `CASE-METAMORPH-VAL`,
  `CASE-SHARED-DEV`, `COVER-MATRIX-DEV`, `COVER-PARTIAL-VAL`,
  `COVER-TARGETED-VAL`, `COVER-ZERO-FINDING-VAL`, `TAX-AXES-DEV`,
  `TAX-COUNTEREXAMPLE-VAL`, `TAX-HISTORY-VAL`, and `TAX-STATE-VAL`; and
- WP5: `M1-PLAT-CLEAN-LAYERS-v1`, `M1-PLAT-IPC-v1`,
  `M1-PLAT-LINEAGE-v1`, `M1-PLAT-PERSIST-v1`,
  `M1-PLAT-UNTRUSTED-v1`, and `M1-PLAT-WRITES-v1`.

The 28 generated package directories, 484-document registry,
`test-data/evaluation/m1-semantic/generators/` project and frozen identity,
`Slice5FixtureContractEvaluationTests.cs`, and the `FixtureIndependence` and
`GeneratorFeasibility` verifier modes are absent. No generator-only or
fixture-only schema remains current. Active fixture schemas remain only because
the six older current public packages use them. The old 28 identities establish
no product verdict and are not frozen future identities.

Mechanical recovery checks found zero rejected package directories, no Slice 5
package registry, no rejected generator, and no fixture-only Slice 5 test. The
current public reader admits exactly the six packages listed below; the default
solution and default projects do not discover the rejected corpus or the
historical evaluator. Planning/specification references to future case names
remain requirements, not current package registration or acceptance.

### Retained WP1 foundation

The recovered current product surface retains:

- closed documentation-evidence, candidate-analysis, finding/case,
  analysis-replay, and answer-free execution-input domain contracts and JSON
  schemas;
- strict JSON parsing, duplicate/unknown-property rejection, active embedded
  schema validation, exact schema identities, and additive domain/application
  protobuf contracts;
- explicit unknown, unsupported, invalid-input, abstention, coverage, gap,
  replay, auditability, boundary, limit, failure, and terminal states;
- contract-level candidate population, decision, admission, uniqueness, and
  one-to-one closure invariants, without candidate production or ranking;
- replay identity/dependency/output contracts without replay execution;
- migration `M1-S5-0004`, database schema `4`, storage contract `1.3.0`, 20
  strict analytical tables, 16 traversal indexes, integrity constraints, and
  40 append-only update/delete guards; and
- `Infinium.PublicFixtures`, product/evaluator authority and retirement
  manifests, default-solution exclusion of evaluator `/4`, the out-of-solution
  bounded-regression project/wrapper, and automated repository-boundary tests.

No documentation importer, claim executor, candidate producer/ranker, finding,
recommendation, case, taxonomy, coordinator/worker/CLI/query behavior, replay
execution, scale expansion, provider/model/search/Nexus behavior, or other
WP2+ runtime implementation was added.

### Six older current public packages

`M1-PLAT-SLICE2-SUBSTRATE-v1@1.1.0` and
`BETH-NPC-DEV@1.4.0`, `BETH-REFR-DEV@1.4.0`,
`BETH-LIGHT-VAL@1.4.0`, `BETH-MALFORMED-VAL@1.4.0`, and
`BETH-UNSUPPORTED-VAL@1.4.0` remain current. Their clean-break migration added
answer-free current analysis envelopes and replay/dependency representation;
it did not change independently authored semantic truth. The current-only
resealer rejected any semantic-truth drift and reproduced all six closures.

### Final verification

| Command or check | Final result |
|---|---|
| `git diff --check` | Passed |
| `dotnet restore Infinium.sln --locked-mode --nologo` | Passed |
| `dotnet build Infinium.sln -c Release --no-restore --nologo` | Passed; 0 warnings, 0 errors |
| Focused `FullyQualifiedName~Slice5Contract` | 5 passed, 0 failed, 0 skipped |
| Focused `FullyQualifiedName~Slice5StateModel` | 10 passed, 0 failed, 0 skipped |
| Repository boundary | 3 passed, 0 failed, 0 skipped |
| Current public-fixture reader | 13 passed, 0 failed, 0 skipped |
| Active fixture/schema integrity | 35 passed, 0 failed, 0 skipped |
| Current Bethesda package qualification | 1 passed, 0 failed, 0 skipped |
| Platform substrate | 7 passed, 0 failed, 0 skipped |
| Bethesda snapshot qualification | 3 passed, 0 failed, 0 skipped |
| Schema compatibility | 7 passed, 0 failed, 0 skipped |
| Affected assertion/CLI/run-output codecs | 10 passed, 0 failed, 0 skipped |
| `Category=M1Contract` | 37 passed, 0 failed, 0 skipped |
| `Category=M1Evaluation` | 40 passed, 0 failed, 8 existing environment-dependent private tests skipped |
| `dotnet format ... --verify-no-changes` | Passed |
| `eng/update-dependency-manifest.ps1 -Check` | Passed |
| `eng/verify-m1-slice5.ps1 -Gate Contracts` | Passed; 13 required schemas, 24 parsed schemas, 2 protobuf files, 20 retired Git blobs |
| Accepted protocol `/4` wrapper | `BOUNDED_REGRESSION_PASS`; 23/23 historical blobs, 20/20 frozen core, 3/3 evolved tests, 8/8 focused tests, 56/56 calibration cases |

Ignored recovery evidence is retained under
`artifacts/m1-slice5/wp1-recovery/`: `pre-recovery-dirty-paths.txt` is 40,618
bytes at SHA-256
`6f2860110ebb5f9631425a07b2552d3bb4ca9ec420d09343199ce02ee2dd43c9`;
`rejected-corpus-inventory.md` is 1,545 bytes at SHA-256
`6154660c9d09a0e75fe3bbbb3a28c3625859b7d72a9a133f1b4f386a424f747a`;
and final `contracts.json` is 5,601 bytes at SHA-256
`46e7cf1ad38f13593befb4b3b57ffd6f47153b654f2828a6faa10057ac369c84`.

### Fresh recovery review

The sole fresh read-only reviewer initially found two material cross-seam
defects: clean `complete-clean` replay was unsatisfiable across schema/domain
rules, and claim applications lacked unique identity and admitted-claim
closure. One bounded correction pass aligned replay mode/state rules, enforced
application uniqueness/reference/applicability, and added regression tests.
The same reviewer re-reviewed the corrected staged diff and returned `PASS`
with no findings. Correction count: one.

Frozen protocol `/4` remains exact historical bounded-regression evidence and
establishes no product verdict. Protocol `/5` remains retired unqualified. No
private repository, abandoned archive, live provider, scoring, B2, C2, Stage D,
WP2+ implementation, or push occurred.

`M1/S5/WP1` is complete and reviewed. `M1/S5/WP2` is the next eligible package.
WP2-WP6 may proceed only in dependency order under staged, work-package-owned
fixture authority. No global evaluator, private oracle, protocol `/4` verdict,
or preauthored comprehensive corpus blocks Slice 5 product development. Slice
5 and M1 remain active and are not complete.
