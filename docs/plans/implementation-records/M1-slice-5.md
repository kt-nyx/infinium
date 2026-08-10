# M1 Slice 5 — Evidence, documentation, candidates, cases, and replay

Status: `M1/S5/WP1` through `M1/S5/WP5` complete and reviewed; `M1/S5/WP6` implementation and independent corpus review complete, two final-review correction cycles closed in the candidate tree, fresh exact-commit re-review pending; Slice 5 is not complete

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

## Current development-execution amendment

On 2026-08-08 the project owner accepted the repository-wide
[development execution policy](../../development/execution-policy.md) for
ordinary work. For `M1/S5/WP2` through `WP6`, fixed correction budgets and a
second-review-defect hard stop are superseded. Each package implements vertical
increments, runs focused checks, classifies findings, corrects must-fix defects,
and re-reviews until accepted.

Owner escalation is now limited to conflicting or materially missing accepted
semantics, required scope/authority expansion, unavailable owner-controlled
dependencies after safe alternatives, and security/private-answer/protected-
root/destructive/external-effect boundaries. Routine test, fixture, schema,
codec, validator, documentation, implementation, and review defects are
recoverable. The earlier correction-count and hard-stop chronology below
remains accurate historical evidence for WP1; it is not current execution
policy for later packages.

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

The accepted staged-verification recovery amendment supplied that bounded WP1
authority. Under the execution rules then in force, a material defect remaining
after the fresh WP1 recovery review was a hard stop. The current development-
execution amendment near the top of this record supersedes that correction
limit for WP2-WP6 without weakening contract, migration, security,
answer-isolation, or frozen-evaluator boundaries.

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

## Work-package closeout — `M1/S5/WP2` complete and reviewed

WP2 was implemented from accepted input commit
`ad233b2a3edf5c420af2f3124e2065235e2ce3e7` on 2026-08-08. This focused
commit completes deterministic documentation evidence and claim import without
model, network, search, Nexus, refresh transport, real/private source, source
instruction execution, or candidate/finding/case promotion.

### Delivered vertical slice

- Added the closed `documentation-claim-import.v1` schema, domain request and
  manifest contracts, strict JSON codec coverage, bounded UTF-8/range/reference
  validation, and typed bounded failures.
- Implemented clean import and retained reuse as separate paths. Clean import
  hashes exact source bytes and exact UTF-8 passage slices; reuse consumes the
  retained revision, passages, claims, applications, and purpose assignments,
  records its producing/reused import identity, and does not re-extract.
- Added collision-safe semantic identities for passages, claims, applications,
  declared-purpose assignments, imports, deletion receipts, gaps, and complete
  evidence aggregates. Aggregate identity covers every serialized semantic
  field, including import time, boundary reason, contradictions, conditions,
  provenance, receipts, gaps, and failures, and is recomputed by the domain
  invariant.
- Added exact applicability/application target admission bound to consuming
  run, immutable installation snapshot, analysis context, resolved input
  manifest, installed subject/type, and dependency closure. Declared purpose
  remains a declaration supported by an exact purpose claim/application; WP2
  performs no prose inference.
- Extended schema-4 persistence with append-only documentation revisions,
  imports, passages, claims, applications, purpose assignments, gaps, deletion
  receipts, typed edges, application-target mappings, and backup payload pins.
  Publication checks the canonical serialized contract against the supplied
  object, rechecks exact source/passage bytes, rejects same-ID/different-row
  drift, and preserves traversal without dangling documentation edges.
- Implemented post-publication physical source/passage deletion with a durable
  receipt, replay/deletion gaps, shared-owner re-evaluation, and backup-retained
  payload disclosure. Historical evidence metadata remains immutable; main CAS
  bytes are removed only after the logical publication transaction commits.
- Added explicit `llm = none` and provider, hosted-search, Nexus, and LOOT
  `not-used` boundary records. Hostile documentation stays inert.
- Added the `Documentation` verification gate with zero-test rejection and
  registered exactly two work-package-owned public semantic packages. The
  accepted fixture specification was corrected to keep WP2 before all
  candidate, finding, recommendation, and case production.
- Corrected the older Slice 2 platform test so a retained historical oracle is
  checked against its reviewed retained authority rather than mutable current
  plan bytes. The unrelated Slice 2 fixture and oracle remained byte-identical
  to the accepted baseline.

Database schema 4 now has the frozen implementation fingerprint
`195fc92064e9f204157f5b355bac141516f00e496e5ed6962dd34280cbd3532d`.
WP1 contracts remain implementation-active for the later vertical packages;
this closeout does not declare them Slice-frozen.

### Independent WP2 fixture evidence

The fresh product-blind fixture author/reviewer role
`/root/wp2_fixture_author` authored only
`DOC-WP2-CORE-DEV@1.0.0` and `DOC-WP2-ADVERSARIAL-VAL@1.0.0` before product
comparison. It did not inspect or execute product code/output. After public
identity rules were finalized, the same role independently recomputed every
length-framed identity, file hash, package closure, replay dependency, and
manifest fingerprint and returned a product-free consistency pass. The product
comparison then consumed the frozen results without using product output to
author expected truth.

Final retained identities are:

| Package/evidence | Identity or SHA-256 |
|---|---|
| Core payload | `docevidence-b00f3d167541bef79ea09e07689b2165` |
| Core input graph | `8a2ecc117185b7d99be95778c79fd74133d41811bb4ad987f755a8ab10f001ff` |
| Core oracle | `27269a4a7a6e39761ce26d5927552bea9d2b6d8f8028160a7bcacd8911b090e1` |
| Core replay dependencies | `ce9423786d4750f58be394f42ff1975db062f71853650130d61a5480cacb543f` |
| Core public manifest | `722fe2b92fabed44ecf543cc82f56eda5c62432cd21a7bb65322d513cd509feb` |
| Adversarial clean payload | `docevidence-4ced9c0c47bc60d5cc156207f2e846ea` |
| Adversarial deleted-reuse payload | `docevidence-b3f8e0062102d7feff5bd759144203b5` |
| Adversarial unavailable-live-source payload | `docevidence-7b0f18e6f7a9b8953bae8c7b334e9fda` |
| Preserved adversarial revision | `docrev-5ab9ded5066400229fea85bdfe3fc1fe` |
| Deletion receipt | `docdelete-bf2dcb16c18ef145453589d5eb16988e` |
| Adversarial input graph | `5ee2cdf28b9b98d7c0abc22a698ecfac79286030e926a7fc3fc12fd62f352d8c` |
| Adversarial oracle | `8a435a592549dcacdb447c89e60ae629dafdc3f8bca5adf7a9063038d3897843` |
| Adversarial replay dependencies | `f0d501c9659db42d8e872c7f49e3a6f65b973976dd36fe627f8a880ab6212022` |
| Adversarial public manifest | `f0010623c26f78d6cb037cfd113c14441a85737f7420d4e78c53d68c332b2dc2` |

The exact product comparison passed all three WP2 evaluation tests and directly
compared revision/import/boundary, passage range/hash/text state, claim and
contradiction graph, producing import, application target/evidence,
purpose/condition authority, gap, receipt, provenance, and payload semantics.

### Review, correction, and final judgment

The fresh read-only evidence/provenance reviewer role
`/root/wp2_authority_audit` did not edit. Its first complete review classified
ten recoverable must-fix classes: transitive contradiction identity,
input-shaped application identity, incomplete aggregate identity, ambiguous
retained availability, deletion-receipt admission, application-target
admission, gap wire tokens, backup retention, incomplete oracle comparison,
and hostile-input computational work. All were corrected with focused
regressions and the same reviewer re-reviewed the current tree.

During the final pass the reviewer found one additional material scope leak:
an unrelated Slice 2 oracle had been resealed to current plan bytes without its
own version/reviewer history. The two fixture files were restored exactly, the
stale current-plan comparison test was corrected, and the same reviewer then
returned **ACCEPT** with no remaining must-fix finding. It verified exact
identity, retention/replay/deletion and backup semantics, target admission,
schema/codec/persistence traversal, closed gap tokens, product-blind fixture
provenance, zero-test gate behavior, and the WP2/no-provider/no-WP3 boundary.

Crash interruption and deletion-failure injection remain part of WP5 fault and
replay verification; they do not weaken the WP2 logical-commit-before-physical-
deletion contract. No other unsupported WP2 path is hidden: unavailable or
deleted sources produce explicit gaps, invalid input produces typed bounded
failure, and prose that cannot be deterministically declared remains
unsupported/unknown rather than inferred.

### Final verification

| Command or check | Final result |
|---|---|
| `dotnet restore Infinium.sln --locked-mode --nologo` | Passed |
| `dotnet build Infinium.sln -c Release --no-restore --nologo` | Passed; 0 warnings, 0 errors |
| Focused documentation/claim unit tests | 13 passed, 0 failed, 0 skipped |
| Focused documentation integration tests | 3 passed, 0 failed, 0 skipped |
| Focused documentation evaluation tests | 3 passed, 0 failed, 0 skipped |
| `eng/verify-m1-slice5.ps1 -Gate Documentation` | Passed; `artifacts/m1-slice5/wp2/documentation.json` |
| `Category=M1Unit` | 111 passed, 1 environment-dependent symbolic-link test skipped |
| `Category=M1Contract` | 38 passed, 0 failed, 0 skipped |
| `Category=M1Integration` | 36 passed, 0 failed, 0 skipped |
| `Category=M1Evaluation` | 43 passed, 8 existing environment-dependent private tests skipped |
| `Category=M1Security` | 9 passed, 0 failed, 0 skipped |
| `Category=M1Fault` | 13 passed, 0 failed, 0 skipped |
| Full Release solution | 289 passed, 9 existing environment/private skips, 0 failed |
| Historical platform/public fixture check after scope correction | 8 passed, 0 failed, 0 skipped |
| `dotnet format ... --verify-no-changes` | Passed |
| `eng/update-dependency-manifest.ps1 -Check` | Passed |
| `git diff --check` | Passed; line-ending notices only |

No evaluator-private repository, private corpus, abandoned legacy archive,
live/billable provider, protocol `/5`, B2/C2/Stage D/scoring, or WP3+
implementation surface was accessed. Nothing was pushed.

At commit `84f3edc288c4988fefd5df36d63c1c53084de1ff`, WP2 was declared complete
and independently reviewed and WP3 was identified as the next eligible
package. The subsequent review below supersedes only that live handoff; it
does not rewrite the implementation and verification history above.

### Post-completion review and corrective handoff

A repository review on 2026-08-08 found one remaining must-fix fixture-boundary
defect. `DocumentationFixturePackageReader` registers the two WP2 packages as
current public fixture evidence but applies `ActiveJsonSchemaValidator` only to
`public-manifest.json` and `inputs/claim-import.json`. It parses and
fingerprints other files without validating the accepted provenance, replay,
redistribution, and partition-history contracts. It also does not require the
final partition-history state to equal the public manifest or exercise those
rules with package-mutation tests. The registered authority surface therefore
overstates the closure currently enforced by the executable reader.

This is a targeted evidence-package correction rather than a rejection of the
documentation product path. The importer, identities, persistence, deletion
effects, inert-content handling, and focused product tests showed no additional
must-fix defect in this review. WP2 must now align the entire package with
active schemas (or add accepted WP2-specific schemas where the generic fixture
contracts cannot express the required fields), validate transition and exact
file closure semantics, add negative mutation coverage, reseal affected public
hashes without deriving expected truth from product output, and obtain a fresh
independent fixture/authority review. WP3 is not eligible until that correction
is accepted.

### Public-fixture corrective completion

The targeted correction started from pushed commit
`66b6b5300bc27c545d5738bda4c99a1369b7b5f6` on branch
`codex/m1-s5-wp2-documentation-evidence`. It changed only the WP2 public
fixture reader, active public JSON schemas, focused contract/security mutation
tests, the focused evaluation harness, the two registered WP2 package
governance documents, this record, and the live handoff. It did not reopen the
documentation importer, evidence identity, persistence, deletion, or semantic
oracle implementation.

`DocumentationFixturePackageReader` now validates all eight structured JSON
documents in each package with `ActiveJsonSchemaValidator`. It reuses the
accepted generic public-manifest, claim-import, replay-dependency,
redistribution, and partition-history schemas. The generic fixture contracts
cannot represent the complete WP2 documentation case-matrix, oracle, or
answer-isolation provenance shapes, so the correction adds these closed,
package-specific active schemas:

| Active schema file | SHA-256 |
|---|---|
| `documentation-fixture-case-matrix.v1.schema.json` | `1f5ad895537dd748743383c5072cb6b3321f05d4767965c9a0f36f2d4ba8cefc` |
| `documentation-fixture-oracle.v1.schema.json` | `abb92a4090c09e9e4fccf5d7081e9f88711b4d4d2ed77ce8ba6cc54e6d03bb4b` |
| `documentation-fixture-provenance.v1.schema.json` | `0ea6164a04dadd6a2ce93596a020cd3f17fb478ca90dabd5ddc0bed36fbb47ee` |

The reader additionally requires exact fixture/version agreement; exact
manifest/partition-history equality; a valid initial assignment and only the
accepted known-answer transition to development; strict timestamp order; final
partition agreement; materially independent replacement identity and input/
oracle fingerprints; unique bounded replay dependency IDs and paths; exact
three-file input closure; safe normalized in-package paths; exact hashes and
lengths; exact oracle/derivation output closure; redistribution agreement;
answer-isolation constants; exact derivation path/version/hash/length; case,
claim-import, and oracle application-target agreement; exact external-boundary
agreement; and exact root/input/oracle file and directory closure with no
reparse point. It also binds the clean execution mode, run, dependency,
extractor, timestamp, aggregate passage/retention/replay state, no-model and
no-external-boundary fields to the corresponding oracle objects; binds reuse
run/time/deletion data; closes typed core/adversarial claim branches; and
checks exact oracle counts, indexes, and unique typed identities. The focused
evaluation harness now consumes fixture-bound mode and timestamps rather than
hard-coded values.

The mutation suite contains 45 WP2 package-integrity cases plus the active
schema-set closure check. It proves rejection of schema-invalid provenance,
replay, redistribution, partition history, case matrix, and oracle documents;
partition-history disagreement; answer-isolation, redistribution, identity,
application-target, replay-state, and boundary drift; missing, extra,
duplicate, escaping, or over-bound replay dependencies; replay governance,
clean-mode, execution-time, aggregate-state, typed-claim, no-external-use,
oracle count/index, hash, length, and derivation drift; and unexpected files,
directories, and reparse points. It also proves that both registered packages
and one fully specified accepted
validation-to-development transition remain admissible.

#### Corrected package identities and independent review

Expected semantic truth did **not** change. Against the starting commit, both
`expected-oracle.json` files, both case matrices, both claim-import inputs,
both source payloads, both independent derivation records, and both partition
histories remain byte-identical. Product output did not author or rewrite any
expected claim, ID, count, outcome, payload identity, deletion receipt, or
gap. Only provenance, replay, redistribution, and public-manifest governance
bytes were resealed.

| Package/evidence | Final identity or SHA-256 |
|---|---|
| Core payload | `docevidence-b00f3d167541bef79ea09e07689b2165` |
| Core input graph | `8a2ecc117185b7d99be95778c79fd74133d41811bb4ad987f755a8ab10f001ff` |
| Core oracle | `27269a4a7a6e39761ce26d5927552bea9d2b6d8f8028160a7bcacd8911b090e1` |
| Core provenance | `a3f1af914352599c0095cb0545a0adf69c27c34f3285f6452146d7d664f8e3aa` |
| Core replay dependencies | `6626d955025039cc887329043491dcb2c1c2350e14e1b64eadc8ac8acf90c47b` |
| Core independent derivation | `20a281c519403a9ac70d15821ef120e6ce62757bb8fdd36ef2a7c504b362ab97`; 2,871 bytes |
| Core public manifest | `a46782cee9fe0fec47465f6866803652970ca81b5d554366cedf38995ef780f0` |
| Adversarial clean payload | `docevidence-4ced9c0c47bc60d5cc156207f2e846ea` |
| Adversarial deleted-reuse payload | `docevidence-b3f8e0062102d7feff5bd759144203b5` |
| Adversarial unavailable-live-source payload | `docevidence-7b0f18e6f7a9b8953bae8c7b334e9fda` |
| Adversarial input graph | `5ee2cdf28b9b98d7c0abc22a698ecfac79286030e926a7fc3fc12fd62f352d8c` |
| Adversarial oracle | `8a435a592549dcacdb447c89e60ae629dafdc3f8bca5adf7a9063038d3897843` |
| Adversarial provenance | `bb8add0a3a1f834a09eb78a3809ad9a56def9af5664309e31f735c607ab75f04` |
| Adversarial replay dependencies | `7f60992a8cf51048c567b7cfee98d0b82d620f5780757cc38802925f4fb2ff85` |
| Adversarial independent derivation | `5cafb981d970a2e0895f808232d60d25818713e5b70ba23fef012b93d1e810af`; 2,932 bytes |
| Adversarial public manifest | `a38c1623c34c583508f0dbf91dbed0c8694c7cdc5a9b3c09524385c2b6195cc5` |

The fresh product-blind reviewer role
`/root/wp2_product_blind_fixture_review` inspected no product source, test,
output, or build artifact. It independently recomputed every input, oracle,
provenance, replay, derivation, redistribution, partition-history, and raw
manifest hash/length; verified exact package and dependency/output closure;
confirmed all identities, transitions, redistribution, answer-isolation, and
derivation bindings; confirmed the expected semantic truth was byte-unchanged;
and returned **ACCEPT** with no finding. After the final schema tightening it
repeated the product-blind review, independently recomputed the same package
bindings plus the three schema fingerprints above, and again returned
**ACCEPT**. A separate fresh authority/diff reviewer drove correction of the
remaining execution/aggregate, typed-claim, boundary, replay-governance, and
oracle-internal seams; its final review found the corrected implementation
semantically green.

#### Corrective verification

| Command or check | Final result |
|---|---|
| `dotnet restore Infinium.sln --locked-mode --nologo` | Passed |
| `dotnet build Infinium.sln -c Release --no-restore --nologo` | Passed; 0 warnings, 0 errors |
| Focused documentation/claim unit tests | 13 passed, 0 failed, 0 skipped |
| Focused documentation integration tests | 3 passed, 0 failed, 0 skipped |
| Focused documentation evaluation/product comparison | 3 passed, 0 failed, 0 skipped |
| Focused fixture contract/security/mutation plus schema-set check | 46 passed, 0 failed, 0 skipped |
| `eng/verify-m1-slice5.ps1 -Gate Documentation` | Passed; `artifacts/m1-slice5/wp2/documentation.json` |
| `Category=M1Unit` | 111 passed, 1 environment-dependent symbolic-link test skipped |
| `Category=M1Contract` | 38 passed, 0 failed, 0 skipped |
| `Category=M1Integration` | 36 passed, 0 failed, 0 skipped |
| `Category=M1Evaluation` | 43 passed, 8 existing environment-dependent private tests skipped |
| `Category=M1Security` | 9 passed, 0 failed, 0 skipped |
| `Category=M1Fault` | 13 passed, 0 failed, 0 skipped |
| Full Release solution | 334 passed, 9 existing environment/private skips, 0 failed: 129 unit/1 skipped, 128 contract, 35 integration, 42 evaluation/8 skipped |
| `dotnet format ... --verify-no-changes` | Passed after formatter-only correction and affected reruns |
| `eng/update-dependency-manifest.ps1 -Check` | Passed |
| Changed/new JSON parse | Passed; 11 documents |
| Changed-document local-link check | Passed; 6 local links |
| Protected/out-of-scope path check | Passed; 17 focused commit paths; unrelated untracked `human-guide/` excluded |
| `git diff --check` and final semantic diff review | Passed |

No evaluator-private repository, private corpus, legacy archive, historical
evaluator material, live provider, WP3+ implementation, or push was accessed
or performed. The final local task commit is reported in the handoff because a
commit cannot contain its own hash.

## M1/S5/WP3 — causal joins, candidates, hypotheses, and abstention

Status: Ready for final acceptance review; uncommitted implementation

Branch: `codex/m1-s5-wp3-candidates`

Baseline commit: `1634b2a64c678dff4d163bce26938ab58b169e91`

Implementation commit: pending final local commit

### Authority and scope

WP3 began only after the live handoff in `docs/current-state.md` established
that the accepted WP2 correction was complete and WP3 was eligible. The work
consumed the accepted Slice 5 plan, its current implementation record, the
delivered Bethesda indexes, and WP2 documentation evidence. It did not read or
use the legacy archive, evaluator-private repository, historical evaluator
internals, protocol `/5`, live/provider/model output, real profile content, or
WP4-WP6 semantics.

WP1's implementation-active candidate contract could not represent the
accepted WP3 vertical: it had no separate hypothesis collection, conflated
candidate and proposed explanation state, used incompatible gap/failure
shapes, and lacked an exact aggregate payload identity. The plan expressly
allows an implementation-driven clean break, so WP3 revised the domain model,
schema, codec, persistence, fixtures, and tests together. The resulting
`candidate-analysis.v1` schema SHA-256 is
`3b6d68a376ac0452e0ef5d17613f48b40145431da2e7e17d9081734f4c848708`.
Schema-4 storage remains the accepted migration number; its revised exact SQL
fingerprint is
`d2bc8879ed400d08bcdf0869389535de58b30b8a9ceaf118984e570fb582fefa`.

### Implemented vertical

- The Application-owned `DeliveredIndexCandidatePopulationSource` constructs
  bounded relationship populations from exact Bethesda override/link, FaceGen,
  coverage-gap, and WP2 claim-application indexes while the pure Analysis
  engine remains Bethesda-independent. It neither scans all pairs nor uses
  taxonomy assignments as causality.
- Every declared population member receives one decision. The closed outcome
  ledger retains admitted, resolved-negative, unsupported, invalid, limited,
  deferred, unprocessed, abstained, and failed states. `not-applicable` is a
  pre-lane marker only for invalid, negative, unsupported, deferred, and failed
  outcomes; admitted work remains limited to the three plan-authorized lanes.
- Deterministic-required and mandatory-evidence admission is score-independent.
  Optional-ranked work uses a stable declared rank and explicit limit outcome.
  Candidates, hypotheses, abstentions, gaps, and failures retain separate typed
  identities and dependency edges; WP3 emits no finding, recommendation, case,
  readiness, or runtime conclusion.
- Stable length-framed identities bind member inputs, decisions, candidates,
  hypotheses, abstentions, gaps, failures, dependency closure, aggregate
  counts, and the exact serialized aggregate. Domain invariants reject missing
  ledger rows, duplicate identities, participant/path defects, invalid links,
  count drift, and payload-identity drift.
- Candidate analysis is serialized through the active schema, transactionally
  materialized into candidate decision/candidate/hypothesis/dependency tables,
  retained as the exact aggregate payload, and verified on readback. The WP3
  application phase persists an exact checkpoint envelope. Restart reuses only
  population members whose input fingerprint and policy/threshold/limit
  identities are unchanged; a relevant mutation recomputes one member while
  preserving eleven unrelated members in the frozen semantic case.
- `Candidates` and `CandidateScale` verification modes now execute real,
  non-zero focused suites and retain machine-readable count, identity, hash,
  timing, and verification-driver memory evidence. WP5 still owns coordinator
  orchestration, cross-stage atomic publication/replay, query, and CLI output;
  WP4 owns findings/cases and materialized analysis-gap lineage.

### Independent semantic and scale truth

The product-blind fixture author received only the accepted public authority
allowlist and WP3 fixture assignment. Before product comparison, its first
semantic draft was audited and found to assign two structurally identical
relationships to different lanes. The author independently corrected that
derivability defect, made the structural reference strings explicit, bumped
both fixture/oracle versions to `1.1.0`, retained the prior hashes in append-only
change history, and froze the corrected package. Product comparison began only
after the correction and freeze.

| Package | Identity | Frozen input SHA-256 | Frozen oracle SHA-256 |
|---|---|---|---|
| Semantic | `CAND-WP3-SEM-20260808-01` / `1.1.0` | `3939994e3ed05c31f2e640d3009aea5fdeb6a1497015d9932fd0aa8b9066414d` | `67dd54b86b91918c9e44be58348d46928249ed9de9a7c96d08b9363dd583a4d9` |
| Scale/stress | `CAND-WP3-SCALE-20260808-01` / `1.1.0` | `fe963e6826a524ea08cb91280d0c934fd987d36f6b73e266a66498b6aad7440a` | `bb86b8991b550e1ec644176beca5cfc4f89b5e980bab61fe318afec45ee6a8c6` |

The package has exact file closure, no reparse points, frozen byte hashes,
answer-free inputs, redistribution/isolation attestations, and eight structured
documents validated through the active WP3 fixture schema/codec. Fixed nested
object boundaries reject missing or unrecognized properties. The package
README hash is
`eef155a8a0ad98b8c04a762d91aa7e060ea04e8831537dfb635a1376f9f6764d`.

The semantic oracle closes 12 decisions: 7 candidates, 5 hypotheses, 2
abstentions, 4 gaps, 1 isolated analyzer failure, 1 invalid input, 1 matched
negative, and 1 limit-unprocessed member. It asserts zero findings, cases,
model dispatches, tokens, and provider cost. Exact membership, lanes,
participants, join paths, analyzer isolation, rename/reorder invariance, and
relevant-mutation delta all match product output.

| Profile | Relationship rows | Candidates | Hypotheses | Abstentions | Gaps | Structural reference SHA-256 |
|---|---:|---:|---:|---:|---:|---|
| `scale-4096` | 21,248 | 7,168 | 3,072 | 256 | 768 | `4861b4fe4d7fc380cbf7bba1615f38b77abad1c440f7a65660506774752a3b63` |
| `stress-262144` | 1,359,872 | 458,752 | 196,608 | 16,384 | 49,152 | `a994472cd60a023f549cda8410821fe06a9639eada1fbe9d8450d36c1b93afb6` |

The scale profile runs the full product-reachable expansion and candidate
pipeline. The stress profile uses the same accepted recipe with an independent
closed-form count model and streaming structural hash, avoiding retention of a
million-row aggregate solely for a stress assertion. Both record zero all-pairs
comparisons. The initial retained gate runs measured 2,734 ms for the scale
test, 95,744,000 bytes peak for the Candidates verification driver, and
88,113,152 bytes peak for the CandidateScale verification driver; the retained
JSON reports preserve the final measurements.

### Recoverable corrections before final review

- Corrected the independent fixture's pre-comparison generic-derivability
  defect without using product output as truth.
- Added the missing separate hypothesis/abstention/gap/failure and exact-count/
  payload-identity contract seams across schema, domain, codec, persistence,
  and tests.
- Corrected candidate lane counts so resolved negatives are recorded before
  lane admission rather than inflating a work-lane denominator.
- Corrected optional limits to apply only to candidate-eligible complete or
  ambiguous members, preserving typed negative closure.
- Corrected invalid-input handling so malformed participants/path do not create
  a false missing-information gap.
- Corrected one semantic canonical-role adapter after the product-blind author
  independently distinguished the relationship kind before freeze.
- Updated the exact schema-4 fingerprint after the clean-break SQL constraint
  revision.
- Added nested fixture-shape rejection and active package hash/identity/answer-
  separation checks after review of the prior WP2 fixture-boundary lesson.
- Moved the delivered Bethesda-index adapter from Analysis to Application after
  the locked restore exposed unnecessary transitive dependency and lock-graph
  expansion; the final locked graph is unchanged from baseline.

Focused candidate, integration, exact semantic, mutation, scale, active-schema,
round-trip, persistence, and delivered-index tests pass. The final full
verification floor and fresh candidate/anti-overfitting review are recorded in
the completion amendment below once accepted.

### 2026-08-09 authority-blocked correction

The preceding WP3 draft record is not acceptance evidence and its fixture,
scale, gate, hash, timing, schema-fingerprint, and completion claims are
superseded. During independent anti-overfitting review, the product-blind
fixture author and product reviewer identified an accepted-authority gap: the
plan requires product-reachable construction from delivered Bethesda/WP2
substrate and prohibits semantics outside that substrate, but the allowed
public authority does not define a field-level answer-free fixture payload for
that product source. The staged generic relationship stream cannot reach the
delivered product source without an unauthorized fixture-specific semantic
adapter. The author therefore paused re-freeze and review rather than inventing
the missing contract.

No WP3 fixture or validation identity is current, no Candidates or
CandidateScale gate has passed, and WP3 is not complete. The working tree
contains an uncommitted product draft with a green Release build and focused
product contract/unit/integration checks, but the evaluation gate remains
intentionally non-authoritative and failing. No WP3 commit or push was made.
Owner action is required to accept a public field-level delivered-substrate
fixture contract (or provide a maintainer-authored answer-free payload
projection) before fixture authoring, product comparison, final verification,
and independent acceptance can resume.

### 2026-08-09 owner clarification and authority-resolution amendment

The preceding authority-blocked conclusion was mistaken and is superseded by
the owner's explicit WP3 recovery direction. The accepted Slice 5 plan already
assigns WP3 ownership of its candidate semantic/scale fixtures and of the
smallest product-reachable scale expansion contract. Defining that contract
from the already delivered Bethesda indexes and WP2 evidence does not require
new architecture authority or a new owner decision.

The recovery preserved the uncommitted branch and removed the circular
instance-derived fixture schema and the answer-injecting evaluation adapters.
WP3 now defines two independently authored product contracts:

- `infinium.analysis.candidate-delivered-input/v1`, a closed factual projection
  of snapshot-bound prior/winner links, FaceGen applicability/provider facts,
  explicit coverage gaps, and WP2 documentation applications;
- `infinium.analysis.candidate-delivered-expansion/v1`, a bounded deterministic
  construction recipe containing only factual patterns and cadence.

The active `DeliveredIndexCandidatePopulationSource` consumes the delivered
input contract for both adapted real Bethesda/WP2 inputs and public fixtures.
Neither contract can carry lanes, dispositions, candidate/hypothesis states,
abstentions, gaps, failures, expected output, fixture IDs, oracle metadata,
generator IDs, or seeds. The expansion implementation preflights total fact
rows, materializes only bounded validation populations, and measures larger
stress populations through the same enumerator and length-framed factual stream
receipt. Product candidate publication remains bounded by the existing 64 MiB
aggregate/CAS limit.

The earlier WP3 fixture directory, identities, hashes, comparisons, scale
counts, timings, and gate claims remain withdrawn. A fresh product-blind author
has been commissioned against only accepted public authority plus the new
schemas and field guide. New fixture identities, independent review evidence,
product comparison, final verification, and acceptance are recorded only after
that authoring/review sequence completes.

### 2026-08-09 product-reachable recovery and pre-final evidence

This amendment supersedes the blocked status and every earlier WP3 fixture,
hash, scale, and gate claim while retaining the chronology above. The recovery
continued from baseline `1634b2a64c678dff4d163bce26938ab58b169e91`
without reset, preserved `human-guide/`, and did not access the legacy archive,
evaluator-private repository, historical evaluator implementation, private
fixtures, live/provider/model services, or WP4+ implementation.

The completed product vertical adds the smallest closed answer-free delivered
substrate contract and its bounded expansion, strict schema/domain/codecs,
the real `DeliveredIndexCandidatePopulationSource`, exact execution/analyzer/
policy/threshold/limit bindings, total typed ledger, traversable dependency
closure, schema-4 persistence/readback, attempt-fenced atomic aggregate and
checkpoint publication, targeted restart invalidation, and bounded failure
isolation. The circular instance-derived schema generator and answer-injecting
test adapter were removed. Candidate payload publication and readback remain
bounded by 64 MiB; the validation population is fully materialized while the
one-million-fact stress population uses the same product expansion recipe and
streaming measurement only.

Active contract fingerprints are:

| Surface | SHA-256 |
|---|---|
| `candidate-analysis.v1.schema.json` | `f2c14a579772d0d5d6703dec9bd67da06e580a0a94b61cc93e83469e5dd6ebce` |
| `candidate-delivered-input.v1.schema.json` | `4398b5640691c5aaaf01d415f5ad70c84dfd099f40f9c4897af582f0c643e97b` |
| `candidate-delivered-expansion.v1.schema.json` | `4efe1b6a0827f012048dc71da592023031c5de57cf9ff213020316426d222cbe` |
| `analysis.proto` | `229571ef3dd85075f4364ee43cd6353cde6f15aaf679c8ada31f5ea5bf7a8e4f` |
| delivered-input field guide | `6db5a5efe0f2df7672fc960c7343cbc980897baf33eec7726738af60ae7dbcca` |
| schema-4 SQL | `0e4fbeb821fdd83d86737d60979fa35d9a1300a4d971450c516f66d07ef2231e` |

#### Independently frozen public evidence

A fresh product-blind author froze three separate standard public packages
before product comparison. Its nested reviewer independently read only accepted
public authority, the new schemas/field guide, and final package bytes. It
returned `ACCEPT` with no finding and explicitly did not inspect product source,
tests, tools, output, build artifacts, Git history, withdrawn packages, legacy,
or private material. The author's later accidental receipt of an unsolicited
sibling status summary occurred after the byte-stable freeze; no bytes changed,
and the nested reviewer remained independently isolated.

| Package | Partition | Public-manifest SHA-256 |
|---|---|---|
| `CAND-WP3-SEMANTIC-DEV-v1/1.0.0` | development | `94799a0d9fd5c90594d5da7074297fe257e44aad69b98487bdc7ea5619370afb` |
| `CAND-WP3-SCALE-VAL-v1/1.0.0` | validation | `98e1f3bcb88e40c52abbbddc62ed9f3d613e90d09c4a15d51be081bc8a1bf2c8` |
| `CAND-WP3-STRESS-DEV-v1/1.0.0` | development | `5b5507622d217223aa2a28a049d5c82b7e411238aaa6c10f415f27c594d1ebbf` |

The clean reviewer reproduced all nine files per package, exact refs/hashes/
timestamps, answer isolation, source-fact and `derived-from` closure, the six
rename/reorder/relevant-evidence/rank/unrelated/dependency metamorph classes,
and these independent totals:

| Profile | Facts | Admitted | Negative | Ambiguous | Unsupported | Candidates | Hypotheses | Abstentions | Stream SHA-256 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|
| semantic | 16 | 5 | 4 | 5 | 2 | 10 | 10 | 7 | direct-input exact membership |
| validation scale | 3,200 | 940 | 940 | 820 | 500 | 1,760 | 1,760 | 1,320 | `b3e51f9a61042cf5038b0ac25e353929db86e96381606c3599cd63f7175cdb25` |
| streaming stress | 1,000,000 | 293,750 | 293,750 | 256,250 | 156,250 | 550,000 | 550,000 | 412,500 | `89bee1f740818d905e8dd2e7b8b549e94574c2514c18a8562714e21bcbad5df5` |

The independent validation-scale semantic projection is 2,047,092 bytes,
below the 67,108,864-byte boundary. Product comparison entered exclusively
through the real delivered-index source. The development semantic comparison
exposed shared lane-provenance, deterministic-hypothesis, and unsupported-
abstention defects; those were corrected across producer, domain/schema,
wire, persistence, and tests before the validation-scale comparison. The
validation package did not drive or tune those corrections. The final semantic,
all-six-metamorph, full validation-scale, and streaming stress comparisons pass.

#### Pre-final gates

`Candidates` and `CandidateScale` both pass with non-zero focused contract,
unit, integration, and evaluation tests. Their machine reports are retained at
`artifacts/verification/wp3-final/candidates.json` and
`artifacts/verification/wp3-final/candidatescale.json`; the reports bind the
exact package identities/hashes, counts, receipts, execution form, timings,
and verification-driver peak working sets. The `Contracts` gate also passes
with the two new active schemas and current repository-authority registration.
The complete Release floor, fresh candidate/anti-overfitting/security/diff
review, current-state advancement, and final local commit are recorded in the
acceptance amendment after they complete.

### WP3 acceptance amendment — 2026-08-09

WP3 is complete and accepted. A separate final product reviewer inspected the
settled live diff from baseline
`1634b2a64c678dff4d163bce26938ab58b169e91`, including contracts/schema/wire,
the real delivered source and expansion, ledger invariants, exact provenance,
persistence, attempt fencing, checkpoint invalidation, resource bounds,
fixture and answer isolation, verifier truthfulness, documentation, and
protected paths. After each reported defect was corrected and re-reviewed, the
reviewer returned `ACCEPT` with no must-fix, follow-up, non-blocking,
authority, or safety/isolation finding.

The settled machine gates pass with fresh reports:

- `Candidates`: 11 contract, 22 unit, 6 integration, and 1 semantic evaluation
  test;
- `CandidateScale`: 1 package-integrity contract and 2 validation/stress
  evaluation tests;
- `Contracts`: both active delivered-input schemas and the revised candidate
  aggregate contract are accepted by the repository contract gate.

The final locked Release verification passed with zero build warnings or
errors. The unfiltered solution projects report 151 unit tests passed with one
expected platform skip, 134 contract tests passed, 41 integration tests
passed, and 45 evaluation tests passed with eight expected private/environment
skips. The `M1Unit`, `M1Contract`, `M1Integration`, `M1Evaluation`,
`M1Security`, and `M1Fault` category floors all pass. `dotnet format
--verify-no-changes`, dependency-manifest semantic verification, strict JSON
parsing for the changed/package documents, Markdown link checking, and `git
diff --check` pass; the dependency manifest retains only its benign generated
format/line-ending rewrite. No protected root, evaluator-private material,
legacy archive, live/provider/model service, or WP4+ implementation entered
the work. The pre-existing untracked `human-guide/` remains untouched and is
excluded from the focused commit.

`docs/current-state.md` now advances the only live handoff to `M1/S5/WP4`.
The focused implementation commit is reported in the final handoff; nothing
is pushed.

## M1/S5/WP4 — findings, cases, lineage, taxonomy, coverage, and gaps

Status: Complete and independently reviewed; `M1/S5/WP5` is eligible.

WP4 started from accepted HEAD
`603adfa1a891dc4b17af8d2c09428987260d0dce` on focused branch
`codex/m1-s5-wp4-findings-cases`. The pre-existing untracked `human-guide/`
directory and unrelated user state were preserved. No legacy archive,
evaluator-private repository, historical evaluator implementation, private
fixture, controlled-real input, live/provider/model/credential service, or
WP5 behavior was read or used.

### Delivered vertical and contract seams

The accepted vertical consumes the retained WP3 candidate aggregate through a
product-reachable Application input producer and publishes one closed
`infinium.analysis.finding-case/v1` aggregate. It includes:

- an explicit nine-field promotion assessment whose deterministic three-way
  result is supported finding, lead-only, or abstained;
- typed conclusion, severity, remediation/further-investigation,
  reversibility, risks, verification, abstention, and evidence provenance;
- supported cases grouped only through exact shared-cause proofs and separate
  lead-only cases that never affect readiness;
- immutable finding/case occurrences, stable logical identities, explicit
  four-gate reconciliation, all eight closed outcomes, member-first case
  reconciliation, analytical/related lineage, and dedicated lead promotion;
- taxonomy applicability, axis/facet/code, classification role, historical
  predecessor sets, and explicit split/merge mapping provenance without
  severity or intended-target inference;
- exact labeled coverage member ledgers, exclusions, failures, typed gaps,
  per-population completed numerators, and a mandatory `no-safety-claim`
  publication boundary; and
- strict input/output JSON schemas and codecs, fail-closed domain invariants,
  typed append-only persistence/readback, transaction-fenced publication, and
  same-identity/different-semantics rejection.

The authoritative store is now schema 5 with storage contract `1.4.0` and
schema fingerprint
`e6d27152687e6b0c806da58a716a9ab909817f046fbe3bf11d8846da5e5dc87d`.
Migration `M1-S5-WP4-0005` advances a genuine accepted schema-4 store without
relabeling retained logical lineage as occurrence history. Typed rows retain
analyzer family/version and semantic/identity versions, all promotion
predicates, abstentions/recommendations, case memberships, taxonomy history
and projections, exact coverage members/exclusions, gap/failure details, and
multi-edge lineage. Integration evidence verifies exact values and atomic
rollback, not only row counts.

### Independently frozen public evidence

A product-blind author produced the four small WP4 packages before product
comparison. A separate product-blind reviewer accepted frozen v1.0.3 without
reading product source, tests, output, build artifacts, legacy, private, or
rejected comprehensive corpus material. The frozen truth is 254,857 bytes with
SHA-256
`528bed0cd3ce399b54ae99f2ebb12e63981f292228c5c972191098c535e90fa2`.

| Package identity | Purpose |
|---|---|
| `infinium.m1s5.wp4.causal-conclusions.generic-a/1.0.3` | promotion, recommendations, supported/lead separation, causal grouping, false merge/split negatives, and rename/reorder metamorphism |
| `infinium.m1s5.wp4.reconciliation-lineage.generic-b/1.0.3` | all eight reconciliation outcomes, four gates, global one-to-one identity, member-first case continuity, append-only lineage, review non-carryover, and lead promotion |
| `infinium.m1s5.wp4.taxonomy-history.generic-c/1.0.3` | all product applicability/role states, exact subject negatives, historical identity, and non-product split/merge projection provenance |
| `infinium.m1s5.wp4.coverage-boundaries.generic-d/1.0.3` | exact population/member denominators and numerators, failures/gaps/exclusions, zero/lead/partial/targeted boundaries, and no-safety presentation |

The accepted fixture review is retained at
`docs/evaluation/fixtures/m1-slice5-wp4-cases-v1/independent-review.md`.
Product comparison executes all four packages through the production pipeline,
including every coverage boundary variant, one global eight-outcome ledger,
reverse candidate order, dedicated lead promotion, the exact five member/two
case reconciliation matrices, all 69 product taxonomy assignment semantics,
and the exact split/merge predecessor and mapping edges.

### Correction and re-review ledger

The development loop treated ordinary failures as recoverable and repeatedly
returned them to correction. Material corrections included: complete typed
input/output/schema/persistence closure; stable non-ordinal identities;
evidence-scoped promotion and recommendations; exact shared-cause/member
proofs; global one-to-one reconciliation and explicit candidate scopes;
absence versus resolution semantics; Decision 8 member-first continuity;
dedicated lead promotion and occurrence lineage; analyzer-family/version and
semantic taxonomy fingerprints; complete coverage member/gap/failure ledgers;
schema-4-to-5 migration safety; and exact fixture-to-product comparisons.
Production keyword/fixture-phrase classification and answer pass-through were
removed in favor of typed answer-free source facts and authority mappings.

The final unfiltered Release floor then exposed one additional clean-break
drift: `analyzer_family` had been added to the analyzer declaration domain and
schema but not its JSON codec. The codec producer/consumer and domain
validation were corrected, its schema round trip passed, and the full floor
was rerun. The final fresh product reviewer found no surviving code, fixture,
gate, authority, or safety/isolation issue. Its report is retained at
`artifacts/m1-slice5/wp4-product-final-review/README.md` with SHA-256
`8c83453ea4a1b66bae9c7a1ba7427d7ca3deedc2a88b18f6a90fd985b9ab9c18`.

### Verification and bounded claim

After locked restore, the Release build completes with zero warnings and zero
errors. The categorized `M1Cases` surface passes exactly 2 contract, 7 unit
(including current schema fingerprint and real schema-4 migration), 4
integration, and 5 semantic evaluation tests. The `Cases` gate passes and
retains its machine report at `artifacts/m1-slice5/wp4/cases.json`; it scans
the complete reachable WP4 domain/candidate/conclusion/case/Application/codec/
persistence/schema graph for fixture answers and forbidden external
capabilities, and it refuses zero or unexpected test counts.

The exact accepted-plan filters passed 4 unit, 4 integration, and 3 semantic
evaluation tests with no skips. The unfiltered Release floor passed 156 unit
tests with one environment-dependent symbolic-link skip, 136 contract tests,
45 integration tests, and 50 evaluation tests with eight expected
private/environment skips. Category floors passed:

- `M1Unit`: 148 passed, 1 symbolic-link skip;
- `M1Contract`: 99 passed;
- `M1Integration`: 45 passed;
- `M1Evaluation`: 50 passed, 8 private/environment skips;
- `M1Security`: 37 unit passed/1 skip, 49 contract passed, 16 integration
  passed, and 7 evaluation passed/2 skips; and
- `M1Fault`: 32 unit, 43 contract, and 15 integration passed, plus 9
  evaluation passed/3 expected identity-environment skips.

`dotnet format --verify-no-changes`, dependency-manifest semantic checking,
strict parsing of every changed JSON document, local-link validation for all
five changed Markdown documents, and `git diff --check` passed. The full floor
first found the missing analyzer-family DTO mapping; after correction the
analyzer declaration round trip was added to `M1Cases`, the gate's reachable
source/schema scan was widened, and the exact 2/7/4/5 gate rerun passed.

This evidence proves only the bounded public synthetic WP4 behavior and its
local deterministic product path. It is not a readiness, safety, reliability,
held-out, private-evaluator, controlled-real, whole-M1, or Slice 5 verdict.
WP5 remains responsible for replay, coordinator integration, reporting/query,
recovery, and write/non-mutation safety. No push was performed.

## `M1/S5/WP5` — Replay, integration, safety, recovery, and reporting

Started: 2026-08-09

Branch: `codex/m1-s5-wp5-replay-publication`

Baseline commit: `7fb6ea6d88e105ba7acec8b8db7ea541be64fae2`

Implementation commit: this record's final local commit

### Traceability checklist

| WP5 deliverable | Settled evidence |
|---|---|
| bounded `analysis-v1` assignment and retained inputs | closed assignment, exact three-input seals, byte/work/query bounds, durable operation identity, and managed-worker validation receipt |
| coordinator-owned atomic publication | one SQLite transaction admits replay/output/index/effect ownership, dependency edges, lifecycle CAS, projection/job updates, and publication receipts; injected failures expose no authoritative partial result |
| retained replay and targeted invalidation | exact source/configuration/schema/analyzer/policy/threshold/limit/seed/payload identities, semantic fingerprinting, clean/incremental/downstream-replay equivalence, and fail-closed retained-identity drift |
| result query and reporting | typed application gRPC summary/replay/artifact/provenance/output queries, bounded authenticated keyset cursors, shared human and canonical `infinium.run-output/v1` semantics, and CLI consumption only through the application boundary |
| partial and failure output | explicit completed-with-gaps, cancelled, limit-reached, and failed run output plus terminal CLI summaries and exit codes |
| recovery and integrity | interrupted-run retry, stale-attempt fencing, coordinator-owned failure output, orphan reconciliation, backup/restore/integrity/projection readback, and contained-worker restart execution |
| write and external boundaries | fixed product write classes, disposable-root and protected-root canaries, coordinator payload admission, inert external surfaces, and provider/model/credential/live/billable `not-used` receipts |
| staged public truth | independently authored and reviewed 12-case development/validation package for six operational families; product output did not author or repair expected truth |
| verification | exact plan filters, `Replay`/`Output`/`Safety` gates, full Release floor, category floors, format/dependency/JSON/link/diff checks, and fresh final product review |

### Integrated product path

`AnalysisV1WorkAssignment` binds the immutable run and the exact retained WP2,
WP3, and WP4 payload identities. The contained managed worker validates those
bounded inputs and produces only a coordinator-publication receipt. The
coordinator then deserializes the existing typed stage documents, validates
their exact identities and canonical round trips, and constructs replay,
run-output, CLI-summary, artifact-index, and external-boundary documents
without reimplementing candidate, finding, grouping, reconciliation,
taxonomy, or coverage semantics.

`AuthoritativeStore.PublishAnalysisResult` publishes the complete result graph
and terminal lifecycle transition atomically. Failure injection before
admission, after physical admission, before terminal CAS, and before commit
proves that no partial result becomes authoritative. Content-addressed files
left by an interrupted physical admission remain non-authoritative and appear
explicitly as reconciliation orphans; a fresh fenced attempt can retry and
publish the sole authoritative result. Retained identity drift fails closed.

The application boundary exposes bounded summary, replay, artifact,
provenance, and complete-output queries. Artifact pagination is stable by
artifact identity and uses an authenticated cursor bound to the run,
publication fingerprint, filters, order, and page size. `Infinium.Cli results`
uses only that boundary. Human and JSON output are projections of the same
run-owned contracts, including explicit gaps, unsupported scope, terminal
state, and no-safety language.

### Product-blind fixture and oracle review

The final frozen registry is
`infinium.m1s5.wp5.operational-cases.20260809.3` with:

| Package identity | Partition and purpose |
|---|---|
| `infinium.m1s5.wp5.publication-replay-query-output-recovery-safety.lantern-a/1.0.2` | development counterparts for atomic publication, replay/invalidation, bounded query, terminal human/JSON output, recovery, and write/non-mutation safety |
| `infinium.m1s5.wp5.publication-replay-query-output-recovery-safety.compass-b/1.0.2` | materially independent validation counterparts for the same six families |

The first authoring revision was rejected because harness metadata and
answer-directed descriptions remained in the product-input file and the two
partitions covered disjoint families. Revision 1.0.1 physically separated the
closed ordinary projection, harness envelope, and oracle and supplied
development/validation counterparts, but review rejected its isomorphic
safety order and underdetermined link/race topology. Revision 1.0.2 uses
neutral target identities, a separately frozen complete final-object
topology, explicit race transitions, and a non-isomorphic validation order.
The independent reviewer re-derived 9 accepted/16 rejected development writes
and 10 accepted/15 rejected validation writes exactly and accepted the frozen
truth. The accepted comparison status is
`independently-reviewed-accepted-comparison-complete-with-explicit-native-capability-gaps`.
All 12 selected projections pass the closed-schema and answer-isolation checks
before product dispatch, retain exact raw-byte validation receipts, and compare
as whole objects against the frozen oracle after execution. The safety adapter
retains 19 topology-capability receipts. It physically exercises distinct
Windows roots and objects, final-object identity, handle-relative writes, NTFS
hard links, junction/mount reparses, relative/parent/case paths, canaries, and
pinned-handle replacement races. Native symbolic-link creation was unavailable
with Windows error 1314; the mount-point substitute is not symbolic-link
qualification. Native 8.3, UNC, device, alternate-data-stream, and cross-volume
qualification remain explicit gaps or stand-ins. The package makes no
standalone native-filesystem, external-adapter, full-EVAL, readiness, or Slice
verdict; WP6 owns comprehensive cross-package execution and Slice acceptance.

Frozen/reviewed file hashes are:

| File | SHA-256 |
|---|---|
| `ordinary-product-projection.schema.json` | `b59430067ccc0b50f6757d41b658b8fcc4317f57bc01e7aaa90bc8525011db5e` |
| `ordinary-product-projections.v1.json` | `33f739fabf923da3bf8b864bf199a07d65a7fa9a04d54755c3034af4fab0bdca` |
| `safety-topologies.v1.json` | `e544e974055e6cf79c7753cd9a28f760c118e26535af1449fbae910c06e178ac` |
| `harness-envelope.v1.json` | `4964bc553afdb9cba848c98542d7bf750b124b1ecedaf42134370505a76a2852` |
| `expected-results.v1.json` | `b971504c46fb46bae2ba6fdd596a1ac730f492cd352792b8e48f6517cac8cf37` |
| final `fixture-manifest.v1.json` | `794f87804efcea7432c60f14702da5774ab2c16d7b82d9222e87259334f56078` |
| `independent-review.md` | `5258a4a11b6e41be4270ad40459a8a51e1a3b272c0c3e313f85cce14ad84afec` |
| final `README.md` | `a727948ab7754b28884991129970973e36bd8b876c66bfea16ca500414f672bf` |

### Verification and correction ledger

Locked restore passed and the Release build completed with zero warnings and
zero errors. The exact plan filters passed 3 contract, 18 integration, and 3
evaluation tests with no skips. The three retained gates passed with:

- `Replay`: 18 integration and 1 evaluation test;
- `Output`: 3 contract, 3 integration, and 3 evaluation tests; and
- `Safety`: 4 integration and 3 evaluation tests.

The current gate reports are retained under `artifacts/m1-slice5/wp5/` as
`replay.json`, `output.json`, and `safety.json`. The Output gate also retains 12
pre-dispatch projection-validation receipts and 19 explicit topology-capability
receipts.

| Retained gate evidence | SHA-256 |
|---|---|
| `replay.json` | `516a4eb67b2b9bbf9fc54af50a397023f0a134cbee8f423d7abbf43049000306` |
| `output.json` | `271308e365785db67ec06cf7a06a945e4fb699da6d8f782be79fe1422920065d` |
| `safety.json` | `823ad259bbb5c0c587c90c27c43652e84cda3d93e1b22b4b423a5bc5d50a6818` |
| `wp5-projection-validation-receipts.json` | `5722cb615d59b5e33b977c0afa97f01a21df1a208beb0f1c191873a91200100d` |

The unfiltered Release floor passed 156 unit tests with one existing
environment-dependent symbolic-link skip, 139 contract tests, 63 integration
tests, and 53 evaluation tests with eight existing private/environment skips:
411 passed and 9 skipped overall. Category floors passed:

- `M1Unit`: 138 passed, 1 symbolic-link skip;
- `M1Contract`: 45 passed;
- `M1Integration`: 54 passed;
- `M1Evaluation`: 52 passed, 8 private/environment skips;
- `M1Security`: 9 passed; and
- `M1Fault`: 13 passed.

Recoverable findings corrected during the development loop included
run-specific or underdetermined semantic fingerprints, candidate execution-input
drift at the WP3/WP5 seam, unavailable replay dependencies being reported as
clean, missing WP2 evidence and relation membership in published output,
unbounded unary query/output seams, incomplete human output, cursor typing and
ordering mismatches, and recovery mistaking a staged validation receipt for a
committed analysis result. Terminal fallback, item reserve/counting, and wall
time were corrected to preserve retained evidence, distinguish cancellation,
limit, and failure, and cooperatively stop work. Fixture answer-isolation,
partition, safety-topology, physical replacement-race, native-capability, and
pre-dispatch receipt defects were corrected across rejected authoring and
product-review revisions. The production write-authority surface remains
unchanged. The dependency-manifest updater now delegates Windows PowerShell 5
invocations to PowerShell 7 so the required check is formatting-stable without
rewriting unchanged dependency data. All affected producer, consumer,
persistence, test, fixture, and gate seams were corrected and rerun; a fresh
final reviewer accepted the settled tree with no remaining must-fix or authority
breach.

This package proves only the bounded public synthetic local WP5 path. It makes
no whole-Slice, whole-M1, readiness, reliability, real-filesystem-platform,
controlled-real, private-held-out, live-provider, credential, billable, or
safety verdict. No private held-out product verdict exists. WP6 remains
responsible for the comprehensive cross-package corpus, accumulated review,
Slice 5 traceability audit, contract freeze proposal, and owner acceptance
packet. No push was performed.

### Post-closeout product-path correction

Correction baseline: `abead1774bd9a2f89cfed1005c5ba6bd50dab885`

The earlier WP5 closeout was provisional and overstated three product-path
properties: it began managed execution from precomputed WP2-WP4 aggregates,
kept targeted invalidation in synthetic test topology, and derived the analysis
context fingerprint from its text identifier. The normal correction loop
reopened WP5 and replaced those claims with the product-reachable path and
evidence below. This section supersedes the earlier path description and stale
verification counts wherever they conflict.

#### Production phase flow and recovery

The production call graph is:

`Infinium.Cli start --analysis-request` -> protobuf `ManualStartCommand` ->
`ApplicationGrpcService.Start` -> atomic
`ManagedRunExecutor.CreateManagedAnalysisRun` -> durable run/operation ->
`ManagedRunExecutor` -> `ManagedAnalysisOrchestrator` -> existing
`DocumentationEvidencePhase` (WP2) -> durable checkpoint -> existing
`CandidateAnalysisPhase` (WP3) -> durable checkpoint -> existing
`FindingCaseAnalysisPhase` (WP4) -> durable checkpoint -> final
`AnalysisV1WorkAssignment` -> managed worker validation of the exact three
retained payload seals -> coordinator-only atomic publication -> Application
query boundary -> `Infinium.Cli results`.

The start request contains admitted source and analysis dependencies, not
precomputed WP2-WP4 output. Run creation and durable operation registration are
one store transaction, closing the dispatch race exposed during broad-suite
testing. Phase input and phase checkpoints are retained in coordinator-owned
persistence with attempt fencing, source-run provenance, logical dependency
fingerprints, payload seals, and disposition. A current-run restart reuses an
exact completed checkpoint. Across runs, an eligible unchanged WP2 checkpoint
is byte-reused with its original source-run identity and the disposition
`reused-retained-phase`. WP3 is recomputed as `recomputed-run-binding` because
its aggregate is bound to the new run, and WP4 is recomputed as either
`recomputed-run-binding` or `recomputed-invalidated` according to its actual
closure. Interrupted boundaries after WP2, WP3, WP4, and before final
publication recover through the same executor. Cancellation is observed both
before dispatch and live between phases; no later phase is dispatched after a
live cancellation boundary. One immutable wall deadline is anchored to the
coordinator-admitted run creation time and survives retries/restart. Wall/item
limit and cancellation outcomes publish cause-specific bounded terminal
output; unavailable early-stage aggregates are reported unavailable rather
than fabricated as retained. Stale attempts cannot checkpoint or publish, and
unavailable, physically missing, substituted, or drifted required dependencies
fail closed without an ordinary publication.

#### Product replay and semantic context

The orchestrator computes deterministic logical fingerprints for the real
documentation, candidate, and finding phases and applies
`ReplayInvalidationPlanner.InvalidatedClosure` before execution. The retained
replay manifest and persisted dependency-closure edges contain the actual phase
and aggregate graph to source, semantic context, Bethesda input, manifest,
configuration, analyzer, policy, threshold, limit, seed, upstream payload, and
finding-policy dependencies as applicable. WP2 provenance is restricted to
the documentation inputs it actually consumed and does not inherit the
candidate-delivered input created later. Retained documentation reuse adds an
exact versioned/fingerprinted prior-evidence reference and rejects same-ID
metadata substitution before admission. Only a complete, available, unchanged
WP2 closure is byte-reused. A finding-policy-only change reuses WP2, recomputes
run-bound WP3, and invalidates WP4; a missing or substituted Bethesda or phase
payload identity fails closed. Clean, unchanged incremental, and retained
downstream replay preserve the same stored semantic fingerprint. Ordinary and
terminal-fallback publication expose all five aggregate provenance roots, and
the bounded Application gRPC query traverses their exact closures rather than
returning every replay dependency.

`SemanticAnalysisContextIdentity` now computes and validates the canonical
context fingerprint from framed context ID, schema version, sorted semantic
input revisions, and sorted policy parameters. `AnalysisExecutionInputContract`,
its JSON Schema, the managed orchestration request, work assignment, replay
dependencies, provenance, ordinary run output, and terminal fallback all carry
the exact context ID, version, and canonical fingerprint. Same-ID version or
fingerprint drift, substituted identities, and execution-input/assignment
mismatch are rejected; no seam synthesizes `SHA256(context ID)` as context
truth.

#### Corrected regression and verification evidence

The new managed product-path regression starts with an admitted Bethesda
semantic input and documentation source, invokes the real WP2-WP4 boundaries,
recovers at every phase boundary, rejects a stale first attempt, publishes
atomically, and reads the result through Application and CLI. It also covers
clean/incremental/replay equivalence, a finding-policy-only transitive
invalidation with actual WP2 reuse and run-bound WP3 reprojection, unavailable,
physically missing, and drifted Bethesda dependencies, retained-documentation
reuse plus same-ID metadata-collision rejection, a one-millisecond limit, a
restart after WP2 beyond the immutable admitted deadline, pre-dispatch and live
between-phase cancellation, terminal-fallback provenance for all five
aggregate roots, and exact semantic-context readback. The CLI/query regression
calls `GetAnalysisProvenance` for all five aggregate kinds and verifies exact
context reachability plus bounded truncation. Contract tests cover context
round trip and same-ID version/fingerprint drift. These tests fail against the
pre-correction architecture because it has neither the production caller nor
durable phase graph, cannot reuse a real unaffected node, exposes only flat or
empty aggregate provenance, and does not retain the canonical context
identity.

Final verification on the corrected tree:

- locked restore passed; Release build passed with zero warnings and zero
  errors;
- exact WP5 filters: 3 contract, 20 integration, and 3 evaluation passed with
  zero skips;
- `Replay`: 20 integration and 1 evaluation passed;
- `Output`: 3 contract, 4 integration, and 3 evaluation passed;
- `Safety`: 4 integration and 3 evaluation passed;
- unfiltered Release floor: 156 unit passed/1 existing symbolic-link skip,
  139 contract passed, 65 integration passed, and 53 evaluation passed/8
  existing private or environment skips, for 413 passed and 9 skipped overall;
- `M1Unit`: 150 passed/1 skip; `M1Contract`: 113 passed;
  `M1Integration`: 65 passed; `M1Evaluation`: 53 passed/8 skips;
- `M1Security`: 37 unit passed/1 skip, 49 contract passed, 16 integration
  passed, and 7 evaluation passed/2 skips; and
- `M1Fault`: 32 unit, 43 contract, 20 integration, and 9 evaluation passed,
  with 3 expected identity-environment skips.

Final retained gate hashes are:

| Evidence | SHA-256 |
|---|---|
| `artifacts/m1-slice5/wp5/replay.json` | `a2730efb2b670360374fbc3d00e6940d4eb8d7f2c2ee4be1480e508160a1dc06` |
| `artifacts/m1-slice5/wp5/output.json` | `6a95e47730ce49370fef978adc9581ee455db4d4f7e6322523def631472aa8af` |
| `artifacts/m1-slice5/wp5/safety.json` | `b8bb23cb7950f323a714442219eb342424e7d1e69d8d831cd261863ce2351bca` |
| `artifacts/m1-slice5/wp5/wp5-projection-validation-receipts.json` | `5722cb615d59b5e33b977c0afa97f01a21df1a208beb0f1c191873a91200100d` |

The supported `dotnet format Infinium.sln --no-restore
--verify-no-changes` command, PowerShell 7 dependency-manifest check, strict
changed-JSON parsing, changed-Markdown local-link validation, and
`git diff --check` passed. The accepted plan's obsolete `dotnet format -c
Release` spelling was rejected by the installed formatter and was not counted
as evidence. Recoverable issues found and corrected during final verification
were the non-atomic run/operation registration race, false retained-state
claims in early terminal fallback, an equal acquired/expiry timestamp in
terminal attempt recovery, integration cleanup racing the live executor,
live cancellation being mistaken for pause-only control, label-only replay
reuse, incomplete phase fingerprints, overbroad/empty aggregate provenance,
missing retained-import identity edges, a resettable retry deadline, and an
untyped malformed managed-request boundary.
No legacy archive, private fixture, evaluator-private material, provider,
credential, live, or billable surface was accessed. `human-guide/` was
untouched. The fresh product-path review below is the final authority for WP5
completion and WP6 eligibility; no push was performed.

#### Fresh product-path review

The required fresh reviewer traced the settled production call graph rather
than accepting fixture/test preconstruction. Its first settled-source pass
found the three product gaps closed with no remaining source must-fix, then
classified this implementation record's obsolete all-stage-reprojection and
pre-dispatch-only cancellation wording as a documentation must-fix. The text
above was corrected to the exercised WP2 byte-reuse/WP3 run-binding/WP4
invalidation behavior and expanded with the late cancellation, restart
deadline, physical-missing, retained-import, fallback-provenance, and
Application-query evidence. The review was then rerun on the corrected record;
its final verdict was **ACCEPTED**, with no remaining must-fix, follow-up,
owner/authority, or safety/isolation finding. It confirmed WP5 complete and
WP6 eligible.

## `M1/S5/WP6` — Comprehensive corpus, accumulated verification, and owner packet

Implementation date: 2026-08-10

Starting commit: `e7de0305515657223c513195f8323b2649b6c7c8`

Status: Implementation, independent corpus review, and terminal whole-slice
review complete. Six exact candidates (`50195fdd33f030f75364f703f636b6ecc1fdb7bd`,
`8bd73c26cde1d569d44b5f70191528df0390e443`,
`93054129fc877193726ca934e72c6483329e4b34`,
`944a0d7c681034b1cb6313596d35b0625ce542dc`,
`258287a524439aefd369d6a4095a7b6da1ebd037`, and
`0274b3b3968605390387a50aefe1d1827b588308`) received fresh whole-slice
`CORRECT` verdicts. The terminal exact candidate
`d47e4290a95cd86cbcf210374cd76788902cc7fb` received `ACCEPT`; all classified
findings are closed. Owner acceptance is now requested. Slice 5 remains active
and is not marked accepted or complete pending that explicit owner decision.

### Scope and authority

WP6 assembled the accepted WP2-WP5 package evidence into one bounded public
cross-stage corpus, added the missing comprehensive verification gate, executed
the real product path before reading frozen truth, reran every accumulated gate
and the full Release floor, and prepared the traceability, contract-freeze, and
owner-acceptance proposals. The work used the accepted M1 and Slice 5 plans,
current product/evaluation authority, ADRs, and the current WP1-WP5 record. It
did not use the rejected 28-package corpus or infer current authority from
historical evaluator material.

The final corpus is
`infinium.m1s5.wp6.cross-stage-corpus.20260810.1/1.0.7`, package
`infinium.m1s5.wp6.cross-stage.clean-incremental-replay.generic-a/1.0.7`, in
the `development` partition. It contains four cases and exactly ten package
files. Its manifest registers the exact eleven accepted WP2-WP5 packages: two
WP2, three WP3, four WP4, and two WP5 packages.

### Answer-isolated corpus authoring and review

A product-blind author constructed neutral ordinary inputs, a harness-only
envelope, a separate exact/bounded expected-result oracle, replay/provenance
records, package metadata, and documentation. A fresh product-blind reviewer
validated those artifacts before any product comparison. The first review
classified five must-fix defects: an answer-bearing causal label, insufficient
facts to derive the WP3/WP4 counts independently, incomplete package closure,
incomplete WP2-WP5 registration, and overbroad authority ownership. Four
correction/re-review iterations removed the leaked answer, made every expected
count independently derivable, closed the exact file/package inventory, and
separated four-case exercise from inherited package indexing. The v1.0.5
review closed those five findings. The first whole-slice review then showed
that the harness selected only D01 and bypassed the coordinator/query surface.
Product-blind corpus v1.0.6 added four bindings and receipts, but its
independent review correctly rejected missing D01 prior-result production and
an invented field-level documentation query. Product-blind v1.0.7 corrected
both: D01 produces, captures, and retains `result.001`; D02-D04 consume that
exact result without substitution; and every case uses only the accepted
bounded Application `result-query-request`/`query-results` surface. The final
v1.0.7 corpus review verdict is **ACCEPT**, with all seven corpus findings
closed and no answer-isolation or authority breach.

The independent reviewer confirmed that the ordinary inputs, expected results,
schema, harness semantics, provenance, replay, and redistribution facts are
semantically invariant with the accepted v1.0.5 corpus after package-version
normalization. The added material is harness-only run binding, prior-result
flow, receipt, immutable source-authority, and generic Application query
evidence. `product_output_used`, `product_output_access`, and
`product_output_comparison` remained false throughout authoring and review.

| Frozen WP6 evidence | Bytes | SHA-256 |
|---|---:|---|
| `ordinary-product-inputs.v1.json` | 7900 | `c1a2f33d3a2e1c29fb3e222ea36c6584ac888d4aa20abef4e9db5bb71355c6a5` |
| `ordinary-product-input.schema.json` | 7942 | `23c9cb6aa1457535507b03089b4a2e4147bde2726bdcf50f458888c1f36f7b3f` |
| `harness-envelope.v1.json` | 18394 | `5bb82e3a3a4980dc5c163a2c024cdc68ce641dfc0381d450e00eb2013f7592e2` |
| `expected-results.v1.json` | 4796 | `7e6808925f7ef9029c998a5e4bd970546b4dcda6f54931a6987234dfc0dc5e36` |
| `provenance.v1.json` | 956 | `43419c96a3a6e6dab235d46c545f941201f8597197193326ade22356bb9964f8` |
| `replay-dependencies.v1.json` | 2479 | `684cfa56786d5987ccf3ac8d011eef4f1e945d29126a1782074d8acbbd433aaa` |
| `redistribution.v1.json` | 609 | `e5d8869d7ed8859200b5473ece40a229f6df0ae60dbf01bae95695f1326817db` |
| `partition-history.v1.json` | 2595 | `b33a96d415d07d326a3d9cb0a11ebacc4e22564ad63f6587c7ec6db10f31b445` |
| `README.md` | 4873 | `ff83d651b06baf1298623f971945fd8fce81e3a058b5afeb488b14aa19fb02c7` |
| `fixture-manifest.v1.json` | 12447 | `0ec59305ac08d4b50ff6b44ff422dfd52e1b1555fd789d74785421b7832f0363` |
| ten-file content aggregate | — | `6f44fdd34b871cdb46339fe8763e374395142579e5381dd8c800614e48dbc5b3` |
| external `independent-review.md` | 10158 | `ec3ff76d511082edd8c3d451cfc9cdae5a6f5f22a4e0de0415957bd439cb69f4` |

### Product execution and semantic comparison

The corrected integration harness first validates the ordinary inputs against
the closed schema and scans them for forbidden expected-output vocabulary. It
then starts the real in-process coordinator and worker pipe surfaces, admits
all four requests through `ManagedRunExecutor`, executes WP2-WP4 through
`ManagedAnalysisOrchestrator`, commits atomic publication, and retrieves each
published result through the typed Application `GetAnalysisOutput` boundary.
Only after all four product observations and receipts are sealed does it load
the independently frozen expected results. The managed request now accepts an
optional delivered candidate input only with its exact byte fingerprint and
source reference; drift is rejected before admission.

The comparison passed these exact cross-stage facts:

- WP2: one revision/import, three passages and claims, two applications, one
  purpose, three visible contradiction gaps, and zero failures;
- WP3: one admitted, one resolved-negative, and one ambiguous member, two
  candidates/hypotheses, one abstention, and zero unsupported members;
- WP4: one finding, two recommendations, one supported path, one separate
  lead-only path with zero readiness effect, and coverage `3/2` as
  `completed-with-gaps` with one visible gap; and
- WP5: one atomic publication, `completed-with-gaps` terminal lifecycle,
  semantically equal human and JSON views, exact clean/unchanged-incremental/
  changed-source/retained-replay behavior, and zero external effects.

The focused comprehensive gate passed six integration tests and fourteen
accumulated evaluation tests with no skips. These tests cover the frozen WP6
four-case comparison, delivered-input admission drift, the supplemental direct
clean comparison, the managed WP2-WP5 path, clean/incremental/replay
equivalence, CLI readback, and the accepted WP2-WP5 semantic packages.

The first legitimate four-case run exposed and repaired two product defects.
Retained documentation had been compared across the semantic-payload and
persistence-payload identity domains, preventing exact checkpoint aliasing.
The replay dependency builder also emitted retained documentation once as a
generic source input and again as a typed documentation output, producing one
identity with conflicting kinds. Reuse now requires exact prior checkpoint
hash and length, and publication emits only the authoritative typed replay
node. Cross-run factual fixture IDs that legitimately vary by run are scoped
to the run so the authoritative store continues to reject actual semantic ID
substitution.

### Repaired WP3 fixture closure

The first aggregate run exposed pre-existing metadata-only seal drift in all
three accepted WP3 packages: their retained projection/receipt byte lengths and
hashes did not match the unchanged on-disk artifacts, so their dependent
replay, oracle, and public-manifest seals were not closed. Product output was
not consulted. The package metadata was corrected from the existing frozen
bytes, every dependent seal was recomputed, and the WP3 candidate evaluation
tests passed three of three. No candidate, hypothesis, abstention, expected
semantic count, or expected projection fact changed.

The current WP3 public-manifest identities, which supersede the stale hashes
recorded earlier in this historical record, are:

| Package | Bytes | SHA-256 |
|---|---:|---|
| `CAND-WP3-SCALE-VAL-v1` | 1442 | `f0db950e7e5110bf4b4c60005a1dca84195abe2217429c4c6b343de865ac5ae2` |
| `CAND-WP3-SEMANTIC-DEV-v1` | 1465 | `635a3e6f75251867d14f328ac5e450cfe6784005753c7717be51d431fcc173e1` |
| `CAND-WP3-STRESS-DEV-v1` | 1494 | `54dd5df9aac989e7443eaffc8e80cbec8db58b75df2d675f32ebd0ca28b4ae5a` |

The repository evaluation-authority inventory, product/evaluator boundary,
contract-test pins, Slice 5 gate pins, and WP6 registrations now use those exact
current identities.

### Traceability audit

The generated `traceability.json` separates direct four-case exercise from
inherited package indexing. The corpus directly exercises 35 requirement IDs,
7 ADR IDs, and 13 evaluation entries at their stated assertions and exclusions.
It separately indexes 26 inherited requirements, 8 inherited ADRs, and 15
inherited evaluation entries from accepted WP1-WP5 evidence. In particular,
`ANALYSIS-019`, `OPS-004`, `ADR-0017`, and `ADR-0023` are inherited-only for
Bethesda analysis, scale/structural limits, the desktop/Windows stack, and the
cost-ledger/budget respectively; the four cases claim none of those surfaces.
`EVAL-0087` is direct only for retained replay dependency identity/history and
separately indexes WP5 atomic publication/recovery at its accepted bounded
scope.

### Gate and verification evidence

`pwsh -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice5.ps1
-Gate All -OutputRoot artifacts/m1-slice5/wp6-final-correction-v6` passed the exact aggregate of
`Contracts`, `Documentation`, `Candidates`, `CandidateScale`, `Cases`,
`Replay`, `Output`, `Safety`, and `Comprehensive`. The final retained reports
are:

| Gate report | SHA-256 |
|---|---|
| `contracts.json` | `63427779d6e37433dc20673bc27024720ada5d5bc5d74f0db4c0d174d0ccbe71` |
| `documentation.json` | `ebdb4b780c9f36f14a4d5144d043e30878fe1c8113af5dc55df9a5097fd1eaf1` |
| `candidates.json` | `bf0fa8eb3268500e9aba3762d4ce2c6a167b68e80b36c9a9cfd12277ff1e8851` |
| `candidatescale.json` | `429b99e0ee1348d994fcf9e5191b7cb97a39d86398c112d10316f3e3c5d1c435` |
| `cases.json` | `4fd85047f1966d5eb7fa7ca639b49bf8932452a29f999c1c00d095aadf39b80d` |
| `replay.json` | `e2fe531a4ec114234836a094d4ac2804a6ca353419a3388f6aff19f895ec59ad` |
| `output.json` | `f3ea0bec147bd927703382036df04f8eca8f939fd72d59dcfc49538f8f779db9` |
| `safety.json` | `3cdce14420a266ce73fe0fd7b392570853a818f2988c50475cdd819b46658106` |
| `traceability.json` | `db130f3a382e975b29b1c805c58a498ccbd2db5bb0d2d79ef79707f03bfbc7eb` |
| `comprehensive.json` | `cb428f24b47ac0c013a81a80dd9b3d6dfd42d48c14caa8575f313c4d5d42306f` |
| `product-comparison-receipt.json` | `296dbe63ba3705ef88453b82a3c3d24d1866fc4f27fc1fdf59f2bd77d11d97f5` |
| `all.json` | `5b2fab5f93f08af2d5ecb779f02618267ecade39732182dd0a2f46860c4617ba` |

Locked restore and the Release build passed with zero warnings and zero errors.
The category floors passed:

- `M1Unit`: 148 passed and one environment-dependent symbolic-link skip;
- `M1Contract`: 111 passed with no skips;
- `M1Integration`: 70 passed with no skips;
- `M1Evaluation`: 75 passed and eight private/local-environment admission skips;
- `M1Security`: 109 passed and three environment/private-admission skips; and
- `M1Fault`: 104 passed and three private/local-environment admission skips.

The unfiltered Release suite passed 156 unit, 140 contract, 68 integration, and
53 evaluation tests: 417 passed and 9 skipped overall. The supported
`dotnet format Infinium.sln --verify-no-changes --no-restore` check and the
PowerShell 7 dependency-manifest check passed. Strict changed-JSON parsing,
changed-Markdown local-link validation, `git diff --check`, and a final semantic
diff audit are run after this record is settled and again after final review
closeout.

Protocol `/4` was not run because the continuation verification profile does
not require the optional frozen historical regression for this change. No
private/evaluator-fixture repository, legacy archive, protocol `/5`, provider,
credential, network acquisition, live game, billable call, external adapter,
MO2/game write, later-slice implementation, or `human-guide/` surface was used.

### Explicit gaps and claim boundary

This package proves only public generic synthetic local/fixture Slice 5
cross-stage conformance and accumulated repository-gate health. It does not
prove controlled-real behavior, native filesystem breadth beyond WP5's retained
bounded evidence, Bethesda breadth, scale/stress beyond the registered WP3
packages, security/readiness/reliability, complete lifecycle/corruption/IPC
matrices, full EVAL coverage, whole-M1 acceptance, or private held-out quality.
It does not predict opaque private identities and it creates no private product
verdict.

### Owner acceptance packet and status proposals

The project owner is asked to review and, if satisfied, make these explicit
decisions:

1. accept the WP6 implementation, independent corpus review, comprehensive
   product comparison, accumulated gates, traceability audit, and retained
   verification evidence;
2. accept the metadata-only WP3 seal correction and its superseding public
   manifest identities;
3. accept the proposal to change the Slice 5 contracts from
   `Implementation-active` to `Slice-frozen` and mark M1/S5 accepted/complete;
4. authorize M1/S6 planning under the milestone dependency graph, without
   authorizing M1/S6 implementation; and
5. retain all explicit claim boundaries and gaps above without interpreting
   this public synthetic evidence as a readiness, safety, private, or whole-M1
   verdict.

Until the owner makes those decisions, the authoritative status remains:
WP1-WP5 complete; WP6 implementation/review complete but awaiting owner
acceptance; Slice 5 active and not complete; contracts
`Implementation-active`; no successor-slice implementation eligible.

### Fresh final whole-slice review

The first exact candidate commit was
`50195fdd33f030f75364f703f636b6ecc1fdb7bd`. Its fresh final review returned
**CORRECT**. The report is
`artifacts/m1-slice5/wp6-final-review/final-review.md`, 11465 bytes, SHA-256
`5f6cd7a84e48e2e183c0d3e83c4ca4ab33f5b8fe5bce857ad489278698e49f12`.
It classified five must-fix findings: D02-D04 were not executed; D01 bypassed
the managed coordinator and typed query; Comprehensive/Traceability and this
record therefore overclaimed scope (including 33 rather than the actual 35
direct requirements); three source-authority seals were unresolved and the
validator ignored them; and `git diff --check` failed on four review lines.
It found no owner/authority or safety/isolation breach.

The corrected candidate closes all five findings. The exact four cases now run
through managed coordinator execution and typed result query; the traceability
count is 35; the manifest binds every source-authority entry to immutable
starting revision `e7de0305515657223c513195f8323b2649b6c7c8`; the validator
reads and hashes those Git blobs; and the accepted independent review has no
trailing whitespace. A fresh final reviewer must confirm these closures
against the exact corrected commit before owner handoff. Its exact input
commit, report identity, and verdict will be appended here.

The second exact candidate commit was
`8bd73c26cde1d569d44b5f70191528df0390e443`. Its fresh final review also
returned **CORRECT**. The report is
`artifacts/m1-slice5/wp6-final-review-v2/final-review.md`, 13171 bytes,
SHA-256 `dcaf262a65806c5d2710dc53db701cba2bf671fc565af9e872268d1e7a2814fc`.
It confirmed that the original coordinator/query, traceability,
source-authority, and diff-hygiene findings were closed, but classified two
remaining must-fixes: D04 asserted the actual `partial` replay beside the
frozen `complete-clean` expectation and still emitted a passing comparison;
and documentation replay-node deduplication suppressed a same-ID source
reference without first proving equal version, hash, and retained state. It
found no owner/authority or safety/isolation breach.

The current correction follows accepted replay authority rather than product
output. Semantic coverage gaps remain separately visible in the run output but
no longer manufacture retained-dependency or audit-trail loss. Complete-clean
replay still requires a complete terminal state, no execution failures, no
missing dependency, complete audit, and equivalent semantic output. The
semantic projection now removes run-binding-only delivered-input, candidate
abstention/gap, occurrence, coverage-gap, and identity-envelope retrieval IDs
while retaining their typed semantic contents; D04 therefore compares equal to
the clean run without hiding factual changes. A documentation output alias is
deduplicated only after exact schema version, byte hash, and `retained` state
match; drift fails closed. The four-case test now compares the actual D04
`CompleteClean`/complete-audit result, exact prior-run binding, zero missing
dependencies and history mutations, plus the remaining D02-D04 oracle fields.
The Comprehensive receipt gate independently requires the ordered four cases
and those D04 replay facts. Corrected Comprehensive passed 6 integration and
14 evaluation tests with no skips. A fresh exact-commit reviewer must now
confirm both second-cycle closures before owner handoff.

The third exact candidate commit was
`93054129fc877193726ca934e72c6483329e4b34`. Its fresh final review returned
**CORRECT**. The report is
`artifacts/m1-slice5/wp6-final-review-v3/final-review.md`, 16157 bytes,
SHA-256 `dd30b3999f95f6287aceeedb2e41972a9ffc0017ba78e02399e556e952a30357`.
It confirmed the earlier coordinator/query, traceability, source-authority,
diff-hygiene, and exact alias-guard implementation corrections, but classified
three remaining must-fixes: complete replay was not projected coherently from
the separately visible semantic coverage gap; the alias regression did not
reach the intended guard and its reachable exception was not typed as identity
drift; and prefix-wide delivered-input normalization could mask an ordinary
dependency substitution. It found no owner/authority or safety/isolation
breach.

The current correction closes all three findings. Completed-with-gaps semantic
coverage now has no replay effect unless a retained dependency is actually
missing; run-output replay/audit projections expose only dependency gaps and
domain plus JSON Schema invariants reject a complete state with a non-clean
class or any replay/audit gap. D04 verifies those coherent product documents.
Focused invalid-state evidence reaches the production dependency builder with
independent documentation version, hash, and retention mutations, and each
fails with `AnalysisIdentityDriftException`. Semantic normalization now selects
only the one delivered-root ID common to every decision; a forged ordinary
dependency with the same prefix changes both decision and graph projections.
The corrected aggregate gate and unfiltered 417-pass Release floor are green.
A fresh exact-commit reviewer must confirm these third-cycle closures before
owner handoff.

The fourth exact candidate commit was
`944a0d7c681034b1cb6313596d35b0625ce542dc`. Its fresh final review returned
**CORRECT**. The report is
`artifacts/m1-slice5/wp6-final-review-v4/final-review.md`, 14123 bytes,
SHA-256 `8c30838f0eb415006f071fa2189081d234b6ac7c289ffacb5281b95929a37ca8`.
It confirmed the v3 alias guard and typed identity-drift closure, the exact
aggregate gate, and the 417-pass Release floor, but classified two remaining
must-fixes. The retained comparison receipt and Comprehensive gate did not
carry the D04 run-output replayability/auditability fields, and the
common-prefix/intersection delivered-root heuristic could still normalize away
an all-decision root substitution. It found no owner/authority or
safety/isolation breach.

The current correction closes both findings. Candidate analysis now carries
the exact delivered-input root admitted by the candidate population context;
that identity is required by schema, bound into payload identity, checked on
every delivered decision, produced by the pipeline, and alone receives
run-binding normalization in the semantic projection. The regression changes
that root in every decision and graph edge while retaining the authoritative
contract field, and proves the semantic fingerprint changes. The comparison
receipt now records run-output replay product state, exact class, replay gap
count, audit state, and audit gap count for every case; Comprehensive requires
D04 to be complete/complete-clean with zero replay and audit gaps. The
corrected aggregate gate and unfiltered 417-pass Release floor are green. A
fresh exact-commit reviewer must confirm these fourth-cycle closures before
owner handoff.

The fifth exact candidate commit was
`258287a524439aefd369d6a4095a7b6da1ebd037`. Its fresh final review returned
**CORRECT**. The report is
`artifacts/m1-slice5/wp6-final-review-v5/final-review.md`, 14616 bytes,
SHA-256 `0404c87fa9efb97b83293f6653d6e718c24c87f4cd76b92378e5fe9d3f5264d3`.
It closed the receipt/Comprehensive finding and confirmed all gates plus the
417-pass Release floor. It classified two remaining must-fixes: the
product-reachable delivered-expansion path materialized and used an exact root
but candidate publication emitted the unspecified sentinel, and this record's
active candidate-analysis schema fingerprint was stale. It found no
owner/authority or safety/isolation breach.

The current correction closes both findings. Candidate population context now
carries an explicit admitted delivered-input identity for either input form.
Direct delivered bytes must match their payload ID; expansion bytes must supply
the deterministic materialized payload ID; every produced decision must bind
that exact root; and contexts without admitted execution input cannot claim a
root. The managed coordinator, public fixture adapter, delivered-input unit
path, and delivered-expansion unit path all supply the field. The expansion
test validates the produced contract and rejects an all-decision/graph root
substitution. The active schema table now records exact current SHA-256
`f2c14a579772d0d5d6703dec9bd67da06e580a0a94b61cc93e83469e5dd6ebce`.
The corrected aggregate gate and unfiltered 417-pass Release floor are green.
A fresh exact-commit reviewer must confirm these fifth-cycle closures before
owner handoff.

The sixth exact candidate commit was
`0274b3b3968605390387a50aefe1d1827b588308`. Its fresh final review returned
**CORRECT**. The report is
`artifacts/m1-slice5/wp6-final-review-v6/final-review.md`, 13024 bytes,
SHA-256 `71cc7a106cb351b96e2f588ebeb87503217156ded97ca7983282973cacec2834`.
It closed the active schema-fingerprint finding and confirmed the intended
direct/expansion consumers and candidate invariant, but classified one
remaining must-fix: expansion admission accepted any non-null asserted root.
The sentinel bypassed membership validation and the source snapshot passed it
because every real expanded decision already depended on that snapshot, so the
wrong identity could receive semantic normalization. It found no
owner/authority or safety/isolation breach.

The current correction closes that finding. Candidate sources that materialize
delivered expansions now implement a typed root resolver. The candidate
pipeline asks the real admitted source to deterministically expand the bytes,
requires exactly one resolved root, and compares it for exact equality with the
asserted admission field before any candidate publication. A focused mutation
uses the expansion's real source-snapshot dependency as the asserted root and
proves rejection, while the valid expansion and public semantic/scale fixture
paths pass. The corrected aggregate gate and unfiltered 417-pass Release floor
are green. A fresh exact-commit reviewer must confirm this sixth-cycle closure
before owner handoff.

The terminal exact candidate commit was
`d47e4290a95cd86cbcf210374cd76788902cc7fb`. Its fresh final review returned
**ACCEPT**. The report is
`artifacts/m1-slice5/wp6-final-review-v7/final-review.md`, 12437 bytes,
SHA-256 `fa0ce342d299ebaf1f8b85f26fdd0df2e6efcd67c9e3cee82cb21392e1db67ba`.
The reviewer independently confirmed V6-MF-01 closed: the real delivered-index
source deterministically resolves the materialized expansion payload,
admission requires exactly one distinct resolved identity equal to the asserted
root, the snapshot-as-root mutation rejects, and valid direct, expansion,
semantic-fixture, and scale-fixture paths pass. All findings from review cycles
v1 through v6 are closed. Exact Gate All, locked build and format, the full
417-pass/9-skip suite, dependency-manifest, 22 changed-JSON, schema-hash,
prior-report-identity, and diff checks passed. No must-fix, follow-up,
non-blocking, owner/authority, safety, or isolation finding remains.

This terminal `ACCEPT` is implementation-review evidence, not owner
self-acceptance. WP6 now awaits the explicit owner decisions in the acceptance
packet above. Until then, Slice 5 remains active, its contracts remain
`Implementation-active`, and no successor-slice implementation is authorized.
