# M1 Slice 7 implementation record

Status: Completed
Last reviewed: 2026-08-24
Owner: Implementation orchestrator; project-owner acceptance pending

## Authority and preflight

Implementation began on 2026-08-24 under the accepted Slice 7 plan and the
activation recorded in `docs/current-state.md`.

- accepted implementation base: `29c421d38336295e5638be0e78728d98e5c11919`;
- activation commit: `551aa5a639f99894964ba626f38a2132eda83e03`;
- implementation branch: `codex/m1-s7-implementation`;
- starting worktree state: clean;
- base relationship: the activation commit is the direct child of the accepted
  implementation base, and that base descends from the plan's accepted
  planning base `0056621464ab2e182d9f320d9d3b1a73bdc2b2b1`;
- private-fixture access: `0`;
- evaluator-private repository access: `0`;
- archive or retired-path access: `0`;
- semantic-oracle authoring, registration, comparison, or verdicts: `0`;
- provider, network, credential, or billable effects: `0`;
- pushes or external publication effects: `0`;
- Slice 8 work: `0`.

## WP1 contract-impact decision

The frozen Slice 6 candidate and finding contracts cannot truthfully retain a
resolved-negative candidate and rejected hypothesis while also publishing zero
finding, case, severity, remediation, and readiness effects. Slice 7 therefore
uses one additive, local-only `infinium.analysis.scope-reversion/v1` aggregate
and work assignment. It does not change the meaning or bytes of any Slice 6
contract family.

| Seam | Slice 7 decision |
| --- | --- |
| delivered factual input | Add typed actor and placed-reference adapter inputs; preserve every predecessor input unchanged. |
| execution composition | Add an admitted local work assignment binding the exact declaration, effective configuration, source identities, enabled adapter set, and limits. |
| analyzer declaration/binding | Add stable family `infinium.scope-reversion`, analyzer ID `infinium.scope-reversion.local`, and independent analyzer, semantic, identity, and ruleset versions. |
| candidate/hypothesis | The additive aggregate represents supported, retained resolved-negative, abstained, unsupported, invalid, failed, and limited states without assigning new meaning to candidate v1. |
| conclusion input | Adapter-owned domain interpretation supplies bounded symptom, extent, recommendation, validation, and taxonomy inputs; the generic analyzer owns promotion. |
| taxonomy | Publish independent purpose, observed-change, consequence, and extent axes with explicit not-applicable, unknown, and unsupported states. |
| finding/case | Promote only a closed supported disposition; group one case by its exact causal closure; preserve zero promotion for negative and ambiguous states. |
| coverage | Publish separate actor transition, actor purpose/applicability, actor conclusion/taxonomy, placed-reference transition, placed-reference purpose/applicability, placed-reference conclusion/taxonomy, and publication/replay populations. |
| persistence/replay | Add append-only schema-10 payload and dependency rows, canonical readback, dependency-local invalidation, reopen and backup/restore support, and fail-closed identity checks. |
| publication | Add canonical JSON and human CLI rendering for raw decisions, candidates, hypotheses, contradictions, abstentions, failures, gaps, findings, taxonomy, cases, recommendations, coverage, dependencies, boundaries, and the exact bounded claim. |

The new contract begins `Implementation-active`. It may advance to
`Producer-consumer-validated` only after WP5/WP6 evidence closes persistence,
replay, invalid-state, fixture, and publication behavior.

## Developer-owned conformance preregistration

Before the first Slice 7 fixture execution, the accepted example package is
preregistered as developer-owned product-conformance evidence. It is not an
independent semantic oracle and cannot issue a product verdict.

- package ID: `M1-S7-SYNTHETIC-v1`;
- actor members: `actor-positive`, `actor-negative`, `actor-ambiguity`;
- placed-reference members: `reference-positive`, `reference-negative`,
  `reference-ambiguity`;
- expected positive behavior: one strongly-supported, moderate finding and one
  causal case per supported cause;
- expected negative behavior: retained candidate, contradiction, resolved
  hypothesis, purpose and observed taxonomy, explicit not-applicable
  consequence and extent, and no finding, case, severity, remediation, or
  readiness effect;
- expected ambiguity behavior: retained candidate and explicit abstention with
  exact missing information, and no finding or case;
- expected external boundaries: all `NotUsed`.

Exact fixture bytes, hashes, measured counts, focused receipts, reviews,
candidate identities, and the final floor are appended at their accepted
package boundaries.

## WP1-WP5 implementation evidence

The implementation uses one category-neutral local analyzer with two admitted
domain adapters. Its exact declaration identity is:

- analyzer family: `infinium.scope-reversion`;
- analyzer ID: `infinium.scope-reversion.local`;
- analyzer, semantic-contract, identity-contract, and ruleset versions:
  `1.0.0`;
- declaration fingerprint:
  `7b1f9e27205481262a38979dd314965ab35f87caaeeba7387acb9a55c46764d1`;
- maturity: `Experimental`;
- external-effect boundary: deterministic local execution only.

WP1 added the neutral transition states, exhaustive closed disposition table,
analyzer declaration, work assignment, contract invariants, JSON schema and
codec, and an effect-free replay reader for the retained Slice 6 source chain.
WP2 and WP3 added independently enabled actor/AI/FaceGen and
REFR/link/placement adapters that feed the same generic analyzer. WP4 added the
bounded findings, four-axis taxonomy, cause-based cases, recommendations,
separate coverage populations, gaps, and publication claim boundary. WP5
added canonical JSON and CLI publication plus schema-10 persistence,
dependency-local invalidation, retained-artifact resolution, incremental and
retained-downstream replay, reopen, and backup/restore behavior.

The storage contract is `1.9.0`, schema version `10`, with exact schema
fingerprint
`d1a3348454d53f3fe4e24c668fbed7fea1443f6ce9947a111c8b29269851efeb`.
Migration accepts only the exact schema-9 predecessor fingerprint, adds
append-only Slice 7 payload, artifact, dependency, and invalidation tables,
and rejects mixed or dangling identities. Frozen Slice 6 product contracts
and their meanings remain unchanged.

The retained Slice 6 replay resolves the accepted successor layout already in
this worktree rather than requiring a new input directory:

- composed evidence:
  `artifacts/m1-slice6/successor-campaign/composed-evidence.v2.json`, SHA-256
  `901f278825d3fdbab2971b9f6bb4462f84c12dea96f1c14c8f222d1f29a1df9d`;
- ledger: `artifacts/m1-slice6/successor-campaign/ledger.v4.jsonl`, SHA-256
  `4cc47bba72ee4c6881cbe77834ac5ab79bd0e0f487145fe0942738d34c507a17`;
- durable state: `artifacts/m1-slice6/successor-product-state`, replayed only
  through a temporary scratch copy;
- protected retained set: the original database, WAL, SHM, and 32 referenced
  payloads, 35 files total, all byte-identical before and after replay;
- WP10 historical state: extraction `extracted`, support `supported`,
  applicability `not-evaluated`, host decision `abstained`;
- WP11 relationship: historically consumed with its exact semantic and
  transport operation, authorization, request, response, model, usage, token,
  and cost provenance retained;
- current authority: `historical-audit-only`; the predecessor chain supplies
  no current semantic authority and is not applicable to any of the six
  Slice 7 synthetic subjects;
- replay effects: zero provider, network, credential, billable, or source-tree
  mutation effects.

This resolves the initially attempted obsolete
`artifacts/m1-slice6/wp9-live`/`wp10-live`/`wp11-live` layout as an
implementation defect. No owner-supplied directory or environment variable is
needed.

## WP6 accumulated conformance evidence

The exact developer-owned package `M1-S7-SYNTHETIC-v1` version `1.0.0`
executed through production composition in 686 milliseconds. Its immutable
fixture identities are:

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `conformance-manifest.v1.json` | 1,029 | `1ab6567d4d30188450ff84acf7eb9a84386f133dfa9f52b3201956a9e0c55025` |
| `input.v1.json` | 4,609 | `d3ce566345c1fd2845a38d292136407c64c2afa81bd405323b5d57c5ba86262f` |
| `expectations.v1.json` | 1,065 | `f4630b927dd746facda0434cebfad8a938e549a18797a3a19c7128a4e60fac23` |

The canonical analysis output is 54,721 bytes with SHA-256
`9440294def8efa6131509aa8b9af3bd427a4348813ac2d4eb69a9acf0126a4d9`.
Its measured population is six: six decisions, candidates, and hypotheses;
two supported findings, two resolved negatives, and two abstentions; two
contradictions, two gaps, two findings, two cases, and two recommendations;
and zero failures, unsupported, invalid, limited, or unpublishable decisions.
Each domain has one matched positive, negative, and ambiguity member. The two
ambiguity members remain visible as completed-with-gaps in every affected
coverage population. All four declared external boundaries are `NotUsed`.

Focused Slice 7 tests passed with the following nonzero category totals:

| Category | Passed | Failed |
| --- | ---: | ---: |
| Unit | 7 | 0 |
| Contract | 4 | 0 |
| Integration | 6 | 0 |
| Evaluation | 3 | 0 |
| Security | 2 | 0 |
| Fault | 2 | 0 |

All six required WP6 gates passed at
`C:\Users\vex\AppData\Local\Temp\infinium-m1-s7-wp6-01a034ad0d467793`.
Each receipt retains its exact command, elapsed time, matched test count, and
artifact inventory:

| Gate receipt | Bytes | SHA-256 |
| --- | ---: | --- |
| `contracts.json` | 8,262 | `4b1d6609852de51add61a3f2523163f3c496d7264a5bce09fba5c7d3f7b8f190` |
| `candidates.json` | 4,051 | `284b96d6240accce80e81567ed9c9809acadd7cedb273738b5fd89c81b49fe6f` |
| `cases.json` | 4,662 | `776fd3c5f0b157f62a7fbdeedcb368b967d09b112eac9c8939f9b3fa5d54b3ed` |
| `replay.json` | 26,072 | `4904e7467da9ac10eb248c8dc86541ac2a840411eebbc5548993a1cdbcb5d77f` |
| `output.json` | 57,993 | `323781e3b1811a39079e65eb8addae7bcc7a35c6e88011ddc28d66710254f3a8` |
| `safety.json` | 26,915 | `44f963167c04ca2fcfc1bc3370cbc24806de731d5a0215e7cb46ed2f562bcbb4` |

This package is developer-owned conformance evidence. It is not an independent
semantic oracle, qualification corpus, product verdict, or authorization to
access private evaluator material.

## WP7 consolidated review and correction ledger

The review covered accepted product meaning and plan fidelity; complete
producer, consumer, persistence, replay, and publication paths; schema and
codec exactness; semantic and anti-overfitting boundaries; provenance and
security isolation; test adequacy; frozen predecessor compatibility; and the
exact candidate diff. Findings were corrected on the same mutable candidate
and the affected surfaces were re-reviewed.

| Classification | Finding | Disposition |
| --- | --- | --- |
| Must-fix | Persistence accepted payload identity without proving the stored bytes were the exact supplied bytes. | Corrected with strict byte comparison and fail-closed readback; focused persistence tests pass. |
| Must-fix | Declaration, effective configuration, and schema-10 validation were initially ID- or shape-bound rather than exact fingerprint-bound. | Corrected with exact declaration, configuration, and schema fingerprint checks plus negative tests. |
| Must-fix | Incremental replay could ignore the exact supplied retained bytes, repeated artifact IDs could change kind, and extra retained artifacts were not rejected. | Corrected with canonical supplied-byte use, kind stability, closed artifact-set validation, and tests. |
| Must-fix | Partial execution could retain the complete fixture claim; case IDs could collide when two domains shared a logical-cause label. | Corrected with run-specific bounded claims and domain/evidence-bound cause identity. |
| Must-fix | Coverage, provenance, taxonomy, lifecycle, and case invariants were not initially closed strongly enough. | Corrected in domain invariants, schema, codec, analyzer, fixtures, and malformed/mutation/metamorphic tests. |
| Must-fix | CLI input had no explicit size bound. | Corrected with bounded input admission and security tests. |
| Must-fix | Initial predecessor replay targeted an obsolete campaign layout and assumed historical application evidence was current semantic authority. | Corrected to the authoritative successor campaign/state, exact historical chain, scratch-only replay, and explicit `historical-audit-only` non-applicability. |
| Must-fix | The first complete-floor attempt exposed registry consumers that still assumed the pre-Slice-7 package count/order and a closed schema inventory that still assumed 51 product schemas. | Corrected by retaining every v2 registry row at its existing ordinal, appending the Slice 7 package, adding authoritative package discovery, advancing the historical integrity count to the exact 57-package registry, and closing the 52-schema inventory. |
| Must-fix | A frozen Slice 6 handoff contract test asserted superseded activation wording after `docs/current-state.md` correctly activated Slice 7. | Corrected only the stale test expectations to the current permanent-provider-closure and Slice 7 effect-free boundary language; no frozen Slice 6 product byte or meaning changed. |
| Must-fix | The second complete-floor attempt exposed two Slice 6 integration tests that reopened a successfully migrated store but still asserted the schema-9 source fingerprint as its current metadata; focused replay then exposed the corresponding retained-campaign loader's schema-7/8/9-only admission. | Corrected both current-store assertions and the exact-fingerprint loader admission to schema 10 while retaining the exact schema-7/8/9 constants, migration-source validation, and fail-closed behavior unchanged. |
| Non-blocking | Independent semantic correctness and real-world generalization remain deliberately unmeasured. | Retained as explicit claim exclusions under ADR-0035 and the accepted Slice 7 scope. |

Fresh affected-surface rechecks pass with no remaining must-fix or unexplained
verification gap. The additive Slice 7 aggregate is now
`Producer-consumer-validated`: real producers, consumers, schema/codec,
persistence, replay, invalid-state behavior, fixture execution, JSON/CLI
publication, and all focused verification layers are exercised. No freeze is
self-declared; owner acceptance remains pending.

The first complete-floor attempt on product commit
`901a34b6f7bb5fc22eba6d2c252e582219eed26c` was diagnostic rather than final:
restore and the zero-warning Release build passed; Unit passed 293 with four
explicit skips; Contract passed 162 and failed six closed-inventory consumers,
so the floor stopped before later layers. After the coordinated corrections,
the exact six failed tests passed 6/6, the complete Contract layer passed
168/168, and the focused Contracts gate passed again. Its corrected 8,262-byte
receipt is retained at
`C:\Users\vex\AppData\Local\Temp\infinium-m1-s7-contract-recheck-31d07c30c79041f9adc78773e6fa7942\contracts.json`
with SHA-256
`67050fa64134556f80a53cacc4630007fdb4a74e778f82389a543e3b36e5116b`.
Every associated repository-scoped process cleanup ended with zero survivors.

The second complete-floor attempt on amended candidate
`4699e43e180b38fa51ddb06d6a9626e793ac7cad` passed restore, the
zero-warning Release build, Unit 293 with four explicit skips, and Contract
168. Integration then passed 185, skipped one retained-artifact case by its
declared condition, and failed the two stale schema-fingerprint assertions, so
the floor again stopped before later layers. Those failures changed no product
behavior; they identified incomplete coordinated test migration.

After the schema-10 successor-loader correction, the two exact failed
scenarios passed 2/2, the complete Integration layer passed 187 with one
declared retained-artifact skip, and the Replay gate passed 26/26 across its
three focused commands. The corrected 26,099-byte Replay receipt is retained
at
`C:\Users\vex\AppData\Local\Temp\infinium-m1-s7-replay-recheck-affa7ef1653f4a10b8e424ca2cb34227\replay.json`
with SHA-256
`8ef30d409f1b4977a753f19759648e56f39eab50c1347fdfb8fda1e8624c5001`.
Formatting, dependency-manifest, diff, and zero-survivor cleanup rechecks also
pass.

The exact bounded implementation claim is:

> For the exact members and coverage populations reported by this run, the
> category-neutral deterministic local analyzer distinguishes only closed
> supported scope-incongruent reversions, preserves resolved intentional or
> harmless changes as negatives, abstains on ambiguity, and makes no broader
> safety, compatibility, completeness, or production-readiness claim.

This does not establish independent semantic correctness, held-out
performance, broad generalization, real-mod behavior, controlled-real
behavior, runtime effects, compatibility, safety, production readiness, or M3
trust.

## Final accepted verification floor

The complete accepted floor passed on the exact clean committed product
candidate `8209e93901cbc7865adad390ca913b62fe7a1650`. The command sequence was
the exact Section 12 floor: locked restore; zero-warning Release build; Unit,
Contract, Integration, Evaluation, Security, and Fault filters; unfiltered
solution tests; format verification; dependency-manifest check; `Gate All`;
diff check; and clean-worktree check.

| Verification layer | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: |
| Unit filter | 293 | 4 | 0 |
| Contract filter, all participating assemblies | 177 | 0 | 0 |
| Integration filter, all participating assemblies | 189 | 1 | 0 |
| Evaluation filter, all participating assemblies | 90 | 8 | 0 |
| Security filter, all participating assemblies | 176 | 6 | 0 |
| Fault filter, all participating assemblies | 114 | 3 | 0 |
| Unfiltered solution | 864 | 13 | 0 |

All skips were pre-existing declared environment/platform or retained-artifact
conditions; no mandatory Slice 7 test was skipped. Restore was current, the
Release build completed with zero warnings and zero errors, formatting had no
changes, the dependency manifest was current, `git diff --check` passed, the
worktree remained clean, and every category plus `Gate All` cleanup retained
the exact repository root and zero matching survivors.

The final proof root is
`C:\Users\vex\AppData\Local\Temp\infinium-m1-s7-final-017db5d344dd4b1090db38db9cda8ec3`.
Its combined receipt is `all.json`, 764 bytes, SHA-256
`6cd835446bd34ec0bd4496a421d351fbba55fe667ea8097cd18b737789771c56`,
verified at `2026-08-24T21:36:01.9958348+00:00`. It reports `passed`, includes
Contracts, Documentation, Candidates, CandidateScale, Cases, Replay, Output,
Safety, and Comprehensive, binds the claim boundary
`public-synthetic-local-analysis-conformance-only`, and records private,
held-out, live, billable, and protocol-5 use as `not-used`.

| Final receipt | Bytes | SHA-256 |
| --- | ---: | --- |
| `contracts.json` | 8,262 | `fb9ac7c25eed997d9322920ea86475a26b65e664c901477f512e81e3fb5e9ca8` |
| `documentation.json` | 8,638 | `967fc194ccb7ae86f12e9430febc1a10e1dd731e551599f6834a6d3523f0002e` |
| `candidates.json` | 4,051 | `e4ac0309cfd98bb04c51eafd69d6a672210d7ab0a9e0d5efd0e75c7bfc398b19` |
| `candidatescale.json` | 3,293 | `97d2dd23c2ba7fa5058214604f2cd23a3d062d76b075b14d6e2b63a4e3ab6d0e` |
| `cases.json` | 4,662 | `27b36490fba06d120b75f51f846628f42ccfeff92719bbcd6e71122b45a8a2d2` |
| `replay.json` | 26,089 | `7012504fa6856353d444a754ea2f7c22af73ddfea995303e9307f88f12103407` |
| `output.json` | 58,046 | `ef32fdfc2c89aaad0b9757382c45e0f1ddb0ee903dd9681e5bf0e2d2d99252bc` |
| `safety.json` | 26,933 | `14ce6139d969510191ba39edd4ff4faa4767e4a220b0c7c4176d11761e4b7251` |
| `comprehensive.json` | 4,155 | `8b57a94a850ebf7e91f70ad8afaaf7be83ad169beeeca60446eec1750e8cadb5` |
| `traceability.json` | 10,043 | `02211dc794c63cb6611650e9fddc18059a87908a79bb8bad7b727cdb604a733c` |
| `scope-reversion-conformance.json` | 10,013 | `bcef2e76d13f8325f1c4c0a78779b91984565f3ea44b9cc398505da7c4ccb3f7` |
| `scope-reversion-analysis.v1.json` | 54,721 | `9440294def8efa6131509aa8b9af3bd427a4348813ac2d4eb69a9acf0126a4d9` |

## Contract maturity and owner handoff

| Contract family | Final implementation maturity | Evidence |
| --- | --- | --- |
| Frozen Slice 6 families | `Slice-frozen`, unchanged | Exact predecessor tests, retained-byte replay, schema-9 migration-source binding, and no incompatible producer/consumer change. |
| `infinium.analysis.scope-reversion/v1` | `Producer-consumer-validated` | Production composition, both adapters, generic analyzer, schema/codec, malformed and mixed-version rejection, schema-10 persistence, readback, invalidation, reopen, backup/restore, replay, fixtures, JSON/CLI, and the complete floor. |
| Storage `1.9.0` / schema 10 | `Producer-consumer-validated` | Exact schema-9 source fingerprint, exact schema-10 output fingerprint, schema-7 through schema-10 successor replay admission, and full Integration/Replay evidence. |

No new contract is self-frozen. Slice 7 is implementation-complete and ready
for the project owner's final acceptance decision. No push or Slice 8 work has
occurred.
