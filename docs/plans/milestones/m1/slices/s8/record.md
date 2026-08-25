# M1 Slice 8 implementation record

Status: Completed
Disposition: WP1 through WP7 are implementation-complete on the exact clean
product candidate below. Final owner acceptance is pending.

Last reviewed: 2026-08-24
Owner: Project owner

## Plain-language result

Slice 8 extends the frozen Slice 7 analyzer without changing its v1 contracts.
The new v2 path used exact, owner-authorized local files to check the same
category-neutral causal rule in two materially different Skyrim domains: an
actor cohort and one placed reference. In both domains, the positive case
produced the required bounded finding and the matched author-patch control
produced no finding.

This proves only that the implemented rule behaves as specified for these four
exact positive/control executions and their declared coverage. It is
developer-owned product-conformance evidence. It is not an independent
semantic verdict, a general mod-compatibility result, a runtime safety result,
or a production-readiness claim.

## Authority and candidate identity

- accepted plan candidate:
  `ab3f7ed2cf0d44067c96a7d88a44be4074486412`;
- activation commit:
  `aa47deba30c980b7527627f275c1cf45bcded226`;
- implementation base: the accepted plan candidate above; the activation is
  its direct child;
- product candidate:
  `c79661cd8eb016e483fa8b7396e7d4997b85d590`;
- product tree: `fd706b21b51e4009cf02e338ef52fbc2fe3eb937`;
- implementation branch: `codex/m1-s8-planning`;
- product-candidate worktree state at the final floor: clean;
- documentation-only handoff: the single commit containing this record and
  compact current-state update; its exact identity accompanies the owner
  decision package because a commit cannot contain its own Git identity.

No push occurred. Slice 9 was neither planned nor implemented.

## Controlled-real input admission

The answer-free handoff was validated before WP3 read any controlled-real
payload. Its absolute root is deliberately untracked and is not reproduced in
this record.

- handoff ID: `m1-slice8-research0035-local-v1`;
- local manifest SHA-256:
  `8972ef0e160b9de04da281d48639b66d8bffcc153504c1d699f654f1eff6ecf5`;
- admitted files: 26;
- admitted bytes: 766,104,776;
- unexpected, missing, drifted, case-colliding, escaping, reparse, or
  answer-bearing entries: zero.

The exact tracked public manifests were:

| Public manifest | Bytes | SHA-256 |
| --- | ---: | --- |
| `docs/research/investigations/artifacts/RESEARCH-0035/eval-0016-independent-byte-map.json` | 45,038 | `e5a1ff7cbe1ff1db84331769b426df333cd442c1ff5b522c7959e08a09a16130` |
| `docs/research/investigations/artifacts/RESEARCH-0035/eval-0017-independent-byte-map.json` | 10,504 | `9dee14a525fa4aac751c946a87ba2a567f03d0e362dd4b68386f79b69b7b5cb9` |
| `docs/research/investigations/artifacts/RESEARCH-0035/gate-c-case-manifests.json` | 10,699 | `2ab135d50adb533e533918de2b5c42f3642348c3234432d6750f073ba68e4d15` |

Every consumed dependency is retained by relative path, role, byte length, and
SHA-256 in the sanitized controlled-real receipt. No third-party payload byte
or absolute local root was copied into Git or a shareable receipt.

## WP1-WP5 implementation

WP1 added a clean-break `infinium.analysis.scope-reversion/v2` schema and
contract. WP2 added the generic controlled-real projector over the accepted
Bethesda semantic substrate. WP3 executed the exact actor and placed-reference
positive/control inputs after admission. WP4 added schema-11 persistence,
canonical readback, retained-downstream replay, reopen, backup/restore, and
dependency invalidation. WP5 added mutation, metamorphic, unavailable-
dependency, malformed, cross-domain, lifecycle, provenance, taxonomy,
coverage, and boundary evidence.

The exact v2 identities are:

- schema ID/version: `infinium.analysis.scope-reversion/v2`, `2.0.0`;
- analyzer family/ID: `infinium.scope-reversion`,
  `infinium.scope-reversion.local`;
- analyzer version: `2.0.0`;
- semantic-contract version: `2.0.0`;
- identity-contract version: `2.0.0`;
- ruleset version: `1.0.0`;
- declaration fingerprint:
  `48b809d3b662215ddf342b931c543af6396225335a3eb68f6c6c0c8d4c9a58d5`;
- taxonomy: `infinium.mod-impact-taxonomy/0.1.0`;
- storage contract: `1.10.0`;
- schema version/migration: `11`, `M1-S8-WP4-0011`;
- schema fingerprint:
  `73f58a86ef5ff4b046e7d2b45b4612047eeda17515f31d75524a37d7a48d8bba`.

The analyzer contract, producer, consumer, codec, renderer, CLI, persistence,
reopen, replay, invalid-state, and controlled-real evidence now support
`Producer-consumer-validated` maturity. Slice 8 does not self-freeze them.

### Frozen v1 preservation

The accepted Slice 7 v1 schema, analyzer declaration, codec, store, reader,
renderer, synthetic fixture, and claim behavior remain available with their
existing meanings. The activation-to-product diff for the v1 schema,
declaration, codec, and v1 persistence files is empty. Exact regression,
migration, readback, replay, and claim-boundary tests passed.

The retained v1 identities remain:

- schema ID/version: `infinium.analysis.scope-reversion/v1`, `1.0.0`;
- analyzer/semantic/identity/ruleset versions: `1.0.0`;
- declaration fingerprint:
  `7b1f9e27205481262a38979dd314965ab35f87caaeeba7387acb9a55c46764d1`;
- storage/schema: `1.9.0`, schema 10;
- schema fingerprint:
  `d1a3348454d53f3fe4e24c668fbed7fea1443f6ce9947a111c8b29269851efeb`.

All earlier Slice 5, Slice 6, and Slice 7 frozen families retain their accepted
contracts. Successor campaign readers now admit schema 11 additively while
preserving the exact schema 7 through 10 behavior and fingerprints.

## Controlled-real results and provenance

All four cases have an append-only
`ControlledRealValidation -> ControlledRealDevelopment` transition. The first
controlled-real observations exposed generic implementation defects, so the
corrected executions are honestly retained as development-conformance
evidence. No later run reacquired a validation label.

| Case | Members | Decisions | Hypotheses | Findings | Cases | Recommendations | Gaps | Result |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `REAL-NPC-0001-POS` | 2 | 1 | 1 | 1 | 1 | 1 | 4 | One shared actor-cohort finding, `StronglySupported` confidence and `Moderate` severity. |
| `REAL-NPC-0001-CTRL` | 2 | 1 | 1 | 0 | 0 | 0 | 4 | Resolved negative; residual `AIDT` remains visible and is not treated as safe. |
| `REAL-REFR-0001-POS` | 1 | 1 | 1 | 1 | 1 | 1 | 3 | One placed-reference finding, `StronglySupported` confidence and `Moderate` severity. |
| `REAL-REFR-0001-CTRL` | 1 | 1 | 1 | 0 | 0 | 0 | 3 | Resolved negative with no actor grouping and no remediation. |

The exact canonical case-output SHA-256 values are, in table order:

- `e9e28d12582e848337fb932fab4706046330c3ba5a5b73b3fa94abb7c91006b4`;
- `978e4b2ad240643eb98461d9240b95de71ae77b8431c9f43323c69cb6338569a`;
- `32899c5c930cd340e2aa6ab98eea9939ab7b1039b35d0d474ab0c0df44fce5fd`;
- `e1b104ceb727e91207163f24e2ee5b7dec8d6affcee4042fe73f5f67dd64d6ab`.

Every conclusion binds the handoff and local manifest, all three public
manifests, exact consumed dependency identities, the source registry/revision/
passage and public-manifest path, the separate source-support/local-
application/host-admission decisions, upstream taxonomy assignments, run,
snapshot, context, configuration, execution input, subject/member, hypothesis,
finding/case, recommendation, coverage, gap, and partition history.

Coverage keeps the actor-positive, actor-control, actor-unresolved,
reference-positive, reference-control, reference-unresolved, analyzer,
persistence, projection, purpose, replay, and taxonomy populations separate.
Unresolved and unsupported members remain in their denominators.

Mutation and metamorphic evidence rejects or truthfully changes output for
purpose-passage removal/fingerprint drift/version inapplicability, relevant
winner/load-order or patch-relation changes, missing dependencies, failed link
resolution, duplicate/reordered cohort members, false cause sharing, and
cross-domain adapter misuse. Display-name changes and unrelated member/order
changes cannot drive production decisions. Missing required information
causes a gap or abstention rather than a guessed conclusion.

## WP6 verification evidence

The final Slice 8 harness passed 37 of 37 mandatory tests with zero skips:

| Category | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: |
| Unit | 12 | 0 | 0 |
| Contract | 5 | 0 | 0 |
| Integration | 10 | 0 | 0 |
| Evaluation | 4 | 0 | 0 |
| Security | 3 | 0 | 0 |
| Fault | 3 | 0 | 0 |

The sanitized proof location is
`%TEMP%\infinium-s8-final-c79661c-6c369a1c04634278adcb69b5f2c2e231`.
Its 4,005-byte `slice8-verification-receipt.json` has SHA-256
`571507a1622a4bd598573466da79c40782ace16ac0a9b30707f65e841e72700f`.
It binds the exact product commit/tree, clean-worktree state, input manifests,
26 controlled inputs, all four executions, output fingerprint
`95081347f56c60528f45297a0be32367649bdc9dc8de69edc71da24785f24e13`,
and zero mandatory skips.

## WP7 consolidated review and correction ledger

The review inspected representative output and the full affected diff for
semantics, contracts, purpose/application/admission separation, taxonomy and
claim limits, provenance and coverage, persistence/replay, isolation, and
maintenance. Corrections were batched on the same mutable candidate, followed
by focused checks and affected-surface re-review.

| Classification | Finding | Same-candidate correction and re-review |
| --- | --- | --- |
| Must-fix | The first v2 result lacked the mandatory hypothesis object. | Added the explicit hypothesis contract through analyzer, schema, codec, persistence, output, and tests. All four final cases retain one hypothesis. |
| Must-fix | The analyzer/output boundary sets were incomplete or conflated, including omitted LOOT evidence. | Restored the four common declaration boundaries and required 11 explicit v2 output boundaries; every final state is `NotUsed`. |
| Must-fix | Coverage was too coarse to expose domain lanes and supporting producer/consumer surfaces. | Added the exact 12 populations and closed denominator/state invariants. |
| Must-fix | Provenance did not initially bind the handoff, public manifests, every consumed dependency, and each source decision tightly enough. | Added exact relative-path/role/length/hash dependency identities, public-manifest paths, source/application bindings, persistence retention, and drift rejection. |
| Must-fix | Assignment and taxonomy identity could omit source/taxonomy/provenance differences. | Included those dimensions in stable identity and added mutation/collision tests. |
| Must-fix | Duplicate artifacts, byte drift, global identity, and cross-run links were not closed strongly enough. | Added canonical uniqueness, supplied-byte, ownership, foreign-key, and dangling/cross-run rejection. |
| Must-fix | The schema-11 successor path and closed JSON-schema inventory were incomplete. | Added additive schema 7-through-11 admission, exact schema-10 migration binding, and the v2 schema inventory without changing earlier identities. |
| Must-fix | Controlled input admission needed explicit parser/decompression bounds and exact selected-form scoping. | Added bounded admission and extraction; default v1 extraction behavior remains unchanged. |
| Must-fix | Current-version integration and contract expectations still named schema 10 or the pre-Slice-8 compact handoff. | Updated only current-version/navigation assertions and preserved historical schema constants and frozen product behavior. |
| Non-blocking | Archive completeness, rendered appearance, runtime behavior, quest/global state, navmesh, other subjects/fields, broad compatibility, safety, and completeness remain unmeasured. | Retained as explicit gaps and claim exclusions. |

No must-fix, unexplained verification gap, or safety/isolation breach remains
after correction and re-review.

### Diagnostic floor attempts

Three complete-floor attempts correctly stopped on defects and were not bound:

1. Unit tests exposed stale schema-10 current-version assumptions, schema-5
   downgrade helpers retaining v2 state, and backup/restore expectations.
   Those were corrected; the full Unit project then passed 335 with four
   declared skips.
2. Contract tests exposed stale current-state wording and a closed schema
   inventory missing v2. Those were corrected; the full Contract project then
   passed 206/206.
3. Integration tests exposed two stale current schema fingerprints and a
   retained Slice 6 successor loader limited to schema 7 through 10. Those were
   corrected additively; the full Integration project then passed 240 with one
   declared retained-artifact skip.

Each correction stayed on the same candidate. Focused checks and affected-
surface review passed before the next final-floor attempt. No intermediate
freeze, record, or acceptance identity was created.

## Final complete verification floor

The exact Section 12 floor passed on clean product commit
`c79661cd8eb016e483fa8b7396e7d4997b85d590` and tree
`fd706b21b51e4009cf02e338ef52fbc2fe3eb937`.

- restore: passed, dependencies already current;
- Release build: passed with zero warnings and zero errors;
- `git diff --check`: passed;
- final worktree: clean.

| Verification layer | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: |
| Unit filter | 300 | 4 | 0 |
| Contract filter, all participating assemblies | 181 | 0 | 0 |
| Integration filter, all participating assemblies | 193 | 1 | 0 |
| Evaluation filter, all participating assemblies | 91 | 8 | 0 |
| Security filter, all participating assemblies | 180 | 6 | 0 |
| Fault filter, all participating assemblies | 118 | 3 | 0 |
| Final Slice 8 focused harness | 37 | 0 | 0 |

The four Unit skips are three recovery tests requiring an external retained
manifest/environment and one symlink-traversal condition unavailable on this
host. The one Integration skip requires retained campaign material not present
in the test environment. The eight Evaluation skips require exact machine-
private Skyrim/MO2/game-executable or configured capture/mutation/race inputs;
none was accessed. The Security and Fault skip totals are those same declared
conditions as they participate in those solution filters. No mandatory Slice
8 or controlled-real test was skipped.

The complete analysis pipeline passed Contracts, Documentation, Candidates,
CandidateScale, Cases, Replay, Output, Safety, Traceability, and Comprehensive.
Its sanitized proof location is
`%TEMP%\infinium-s8-final-pipeline-c79661c-67e72cb359274a1a9cd37f6030a00dcb`.

| Final pipeline receipt | Bytes | SHA-256 |
| --- | ---: | --- |
| `traceability.json` | 10,057 | `e9e911b3907da094f85083154a10d3b3d7e574429927906654f02eac91bc9bb9` |
| `comprehensive.json` | 4,169 | `f67ec17b56408be726d566d1e6fabcac1ff5b94e8c286936c0d0221a60c766de` |
| `all.json` | 764 | `fa877dd90dc4aab7ee32aa922a93bb1918c6f9fc9ccbde6f77484d7ba11f0f1b` |

`all.json` records `passed`, the exact nine included gates, claim boundary
`public-synthetic-local-analysis-conformance-only`, and private held-out live
billable protocol-5 use as `not-used`. This historical public pipeline remains
ordinary repository conformance evidence; it does not create current semantic-
oracle authority or an independent product verdict.

## Exact bounded claim and retained gaps

The accepted implementation claim is:

> For the exact controlled-real subjects, dependencies, purpose/application
> evidence, taxonomy assignments, and coverage populations reported by these
> four executions, one category-neutral deterministic local rule identifies
> the supported actor-cohort and placed-reference scope reversions, preserves
> their matched restored-relation controls as resolved negatives, and exposes
> every declared gap. The result makes no broader compatibility, safety,
> runtime-correctness, completeness, precision, recall, production-readiness,
> or future-case-performance claim.

The actor result retains residual `AIDT`, archive/rendered-appearance, runtime,
other-actor/field/package, and patch-wide-completeness gaps. The reference
result retains runtime rental behavior, quest/global state, navmesh, rendering,
other-reference/field, and patch-wide-safety gaps. Taxonomy axes remain
independent; predicted consequence and extent are not observations. A matched
negative is not a safety claim.

There is no current private held-out verdict or independent semantic verdict.
ADR-0035 continues to defer every independent semantic-oracle package through
M2.

## Isolation and effect accounting

The final receipt records zero use of network, hosted search, Nexus, LOOT,
credentials, providers, private fixtures, evaluator-private material,
semantic-oracle work, archives, external publication, push, and external
effects. No third-party payload bytes were written to Git or shareable
receipts. No private/evaluator repository or legacy/evaluator archive was
accessed.

## Contract maturity and owner decision

| Contract family | Review-ready maturity | Evidence |
| --- | --- | --- |
| Frozen Slice 5 through Slice 7 families, including scope-reversion v1 and storage 1.9.0/schema 10 | `Slice-frozen`, unchanged | Exact file-diff preservation, predecessor regression, migration/read/replay, and the complete floor. |
| `infinium.analysis.scope-reversion/v2` | `Producer-consumer-validated` | Controlled-real producer/projector, generic analyzer, hypothesis/finding/case consumer, schema/codec, persistence, invalid-state, replay, JSON/CLI, mutation/metamorphic, and six-layer evidence. |
| Storage 1.10.0/schema 11 | `Producer-consumer-validated` | Exact schema-10 predecessor binding, atomic append-only migration, canonical readback, invalidation, reopen, backup/restore, and retained-downstream replay. |

Passing verification does not accept or freeze Slice 8. The requested owner
decision is one of: accept the exact product candidate and documentation-only
handoff, reject them, or amend the package. Slice 9 remains unauthorized until
separately planned and activated.
