# M1 Slice 5 — Evidence, documentation, candidates, cases, and replay

Status: Accepted plan; `M1/S5/WP1` complete and reviewed; Slice 5 remains active

Prepared: 2026-08-07

Accepted: 2026-08-07

Accepted by: Project owner

Amended: 2026-08-08; staged-verification recovery and development-execution policy alignment accepted by the project owner

Owner: Project owner

Work ID: `M1/S5`

Parent: `M1`

Depends on:

- the accepted [M1 backend semantic proof plan](../milestones/M1-backend-semantic-proof.md)
  and its accepted
  [evaluator-v2 amendment](../milestones/M1-backend-semantic-proof-evaluator-v2-amendment.md);
- completed and accepted `M1/S4.5/EVAL-CLOSEOUT`;
- accepted [ADR-0032](../../architecture/decisions/ADR-0032-defer-m1-held-out-evaluator-and-continue-public-verification.md);
- the accepted [development execution policy](../../development/execution-policy.md);
- the accepted [M1 continuation verification profile](../../evaluation/m1-continuation-verification-profile.md);
- the accepted product, architecture, evaluation, and fixture authorities
  listed below; and
- implementation-complete M1 Slices 0 through 4 at the exact current public
  baseline.

Next work package: `M1/S5/WP2`

This plan is accepted implementation authority for its dependency-ordered work
packages. `M1/S5/WP1` completed and passed fresh review on 2026-08-08, making
`M1/S5/WP2` eligible after its normal preflight. Each later package remains
gated by completion and review of its declared prerequisite package;
acceptance does not authorize work outside this plan or waive an authority,
security, isolation, protected-root, destructive, or external-effect boundary.

Implementation state on 2026-08-08: the prior owner-authorized WP1 correction
pass completed substantial repository-boundary, contract, state, and migration
work, but its premature 28-package comprehensive semantic corpus returned a
material final-review `FAIL`. The failed review establishes no product verdict
and does not reject the independently valid contract foundation. The project
owner has explicitly authorized the bounded staged-verification recovery below.

Recovery state on 2026-08-08: WP1 retained the closed product contracts,
strict codecs, additive protobuf, schema-4 migration/storage declarations,
current public-fixture reader, and repository-boundary enforcement; removed
the rejected premature comprehensive corpus and fixture-only authority; and
passed fresh review after the bounded correction process then governing WP1.
WP2-WP6 may now
proceed in dependency order under the staged, work-package-owned fixture
authority below. No global evaluator, private oracle, protocol `/4` verdict,
or preauthored comprehensive corpus blocks Slice 5 product development.

### Owner amendment — fixture replacement and historical-surface isolation

On 2026-08-08 the project owner explicitly authorized WP1 to replace, rather
than preserve or weaken around, the rejected public Slice 5 fixture set. The
replacement was required to be comprehensive, independently expected,
recursively answer-isolated, and exact enough for later typed machine
comparison. The correction limit recorded for that completed WP1 recovery was
historical execution control. It does not govern WP2-WP6, which now use the
repository development execution policy.

The owner also authorized a repository-wide public audit and integration pass
for legacy or historical code, schemas, tests, and fixture data that a fresh
maintainer or agent could mistake for current authority. That pass shall:

1. mechanically distinguish current product authority, active public
   regression support, retired compatibility material, and byte-frozen
   evaluator-protocol evidence;
2. remove retired compatibility code/data from current build, test discovery,
   schema resolution, and default documentation paths, using deletion plus Git
   history or an explicitly quarantined non-build historical location as
   appropriate;
3. give retained historical material an unmistakable authority marker,
   machine-readable inventory, owner/reason/successor metadata, and automated
   rejection from current dependency and schema surfaces;
4. prevent evaluator protocol numbers such as `/4` from being interpreted as
   product schema or fixture versions, and document the separate version axes;
5. verify the exact `/4` freeze inventory before edits and leave every frozen
   evaluator runtime/schema/core byte, identity, and historical record
   unchanged; and
6. continue to exclude the evaluator-private repository and the abandoned
   sibling implementation archive.

This amendment permits removal or replacement of the now-retired pre-v2
public-fixture compatibility surface recorded in the retirement manifest,
current public pre-Slice-5 fixture packages and their tests, repository
authority/navigation documentation, and focused archive/authority enforcement
tooling/tests. It does not authorize evaluator `/4` repair, evaluator successor
work, private corpus work, product analysis behavior, or a compatibility shim
that becomes a second current analytical contract.

### Accepted owner recovery amendment — staged work-package verification

On 2026-08-08 the project owner explicitly authorized recovery and closeout of
`M1/S5/WP1`. This amendment supersedes the prior WP1 hard stop only for this
bounded recovery. It removes the rejected 28-package corpus, its current
registry/discovery authority, and fixture-only generators, schemas, verifier
modes, scale identities, and self-consistency tests while preserving valid
product contracts, strict codecs, additive protobuf, state invariants,
`M1-S5-0004`, database schema `4`, storage contract `1.3.0`, and repository
product/evaluator separation.

Slice 5 verification is work-package-owned and dependency ordered:

1. WP1 owns complete closed product contracts and codecs, additive query/wire
   definitions, total state and referential-integrity invariants, schema-4
   migration/storage declarations, repository-boundary enforcement, minimal
   answer-free schema examples, round-trip/closure/state/migration/boundary
   tests, and a fresh contract/boundary review.
2. WP2 owns documentation revision, passage, claim, applicability, and
   provenance fixtures and independently authored expected results.
3. WP3 owns causal-join, candidate, hypothesis, abstention, scale, and stress
   fixtures. Scale populations must be supplied directly or through a
   product-reachable deterministic expansion contract, and exact expected
   counts must come from an independent declarative/reference model.
4. WP4 owns finding, recommendation, case, taxonomy, reconciliation, lineage,
   coverage, and gap fixtures and expected results.
5. WP5 owns publication, platform/write-boundary, replay, recovery, query, and
   output fixtures and expected results.
6. WP6 assembles and independently reviews the comprehensive clean,
   incremental, replay, and cross-stage corpus after the relevant product
   interfaces exist.

Every behavior-owning package freezes its small answer-isolated expected cases
before comparison with product output and passes its own staged evidence before
unblocking the next package. Product output never authors, repairs, or
certifies expected truth. Property, invariant, round-trip, closure,
determinism, and safety tests may precede complete semantic fixtures. No private
evaluator verdict, protocol `/4` verdict, or preauthored comprehensive corpus
is a Slice 5 product-development prerequisite.

## 1. Objective and exact exit state

Slice 5 establishes the first complete, local, provider-independent path from
retained source and snapshot evidence to typed candidates, findings, cases,
coverage, and replay. It consumes the immutable run, MO2 snapshot, and
Bethesda semantic/index substrate delivered by Slices 0 through 4. It does not
implement the Slice 6 provider path or the Slice 7 generic semantic mechanism.

Slice 5 is complete only when one clean and one incrementally recomputed
synthetic analysis can traverse this exact path:

```text
accepted snapshot + analysis context + effective configuration
  + resolved input manifest + Bethesda typed indexes
  + retained local/fixture document revision and exact passage
    -> typed observation/deterministic result/external claim
    -> explicit claim application and declared-purpose assignment
    -> causal join and score-independent candidate admission
    -> typed hypothesis or abstention
    -> finding or lead-only outcome under a declared threshold
    -> recommendation and supported/lead-only case
    -> taxonomy, coverage, gaps, lineage, and reconciliation
    -> atomic SQLite/CAS publication
    -> coordinator query, human CLI rendering, and versioned JSON
    -> complete retained downstream replay with identical semantic output
```

The exact exit state is:

1. all new and changed contracts are closed, versioned, strict, and consumed
   consistently by producers, persistence, wire/query, output, replay, tests,
   and public fixtures;
2. project-authored local/fixture documentation retains its source revision,
   supplying snapshot where applicable, byte fingerprint, exact passage,
   applicability, declared-purpose role, import run, and every consuming
   analysis application link;
3. deterministic claim import performs no model call and cannot turn document
   text into local observation, tool/source/operation authority, or permission;
4. supported causal joins admit every deterministic or mandatory candidate
   independently of ranking score, preserve explicit negative/unsupported/
   malformed/ambiguous/limited populations, and never form a naïve all-pairs
   or whole-profile model loop;
5. candidate, hypothesis, finding, recommendation, supported-case, and
   lead-only-case transitions use accepted typed thresholds and preserve
   contradictions, missing information, abstentions, and raw intermediates;
6. grouping uses demonstrated shared cause; cross-run continuity uses the four
   ADR-0022 equivalence gates and immutable append-only lineage;
7. every exercised population reports its own denominator, completion state,
   unsupported work, failures, exclusions, and gaps in human and JSON output,
   with no combined safety percentage or no-finding safety claim;
8. clean and incremental execution agree for unchanged dependencies; relevant
   dependency changes invalidate only dependents; missing or drifted identity
   fails closed without rewriting retained history;
9. synthetic downstream replay is `complete-clean` from retained inputs,
   versions, seeds, manifests, payloads, and dependency identities; loss
   changes replay/audit classification explicitly instead of substituting;
10. applicable public evaluation gates pass under continuation-profile Layers
    1 through 4 and 6, the common command floor passes, and a fresh semantic/
    diff review accepts the corrected final implementation; and
11. `docs/plans/implementation-records/M1-slice-5.md` records exact contracts,
    schemas, migrations, fixture/oracle revisions, commands, results, counts,
    gaps, review evidence, commit, and claim boundary.

Completion proves the public Slice 5 contract over its independently expected
synthetic local/fixture scope. It does not prove a private held-out verdict,
M1 completion, broad real-mod correctness, the Slice 7 generic mechanism,
M3 reliability/readiness, or public supportability.

## 2. Authority and precedence

Implementation and review shall read repository `AGENTS.md` in its required
order, then this plan and these task-specific authorities completely:

- accepted [product requirements](../../product/requirements.md), especially
  EVID-001–007, DOC-002, DOC-006–008, DOC-011, ANALYSIS-003–005,
  ANALYSIS-016–017, ANALYSIS-019, FIND-001–014, COVER-001–003,
  SNAP-001–006, SCAN-005–007, SCAN-009, PROD-002, PROD-004,
  OPS-001–004, AUTH-001–003, SEC-001–004, AI-003, AI-004, AI-006,
  and AI-007;
- accepted [domain model](../../product/domain-model.md),
  [analysis catalog](../../product/analysis-catalog.md),
  [taxonomy](../../product/mod-impact-taxonomy.md), and
  [severity, confidence, coverage, and readiness](../../product/severity-confidence-and-coverage.md);
- accepted ADR-0015 through ADR-0023, ADR-0025, and ADR-0032 under
  [`../../architecture/decisions/`](../../architecture/decisions/README.md);
- accepted [M1 evaluation baseline](../../evaluation/m1-evaluation-baseline.md),
  [semantic specifications](../../evaluation/specifications/m1-semantic-and-ground-truth.md),
  [semantic revision 2 amendment](../../evaluation/specifications/m1-semantic-and-ground-truth-v2-amendment.md),
  [platform specifications](../../evaluation/specifications/m1-platform-and-operational.md),
  [semantic fixture manifests](../../evaluation/fixtures/m1-semantic-fixture-manifests.md),
  and [platform fixture manifests](../../evaluation/fixtures/m1-platform-fixture-manifests.md);
- accepted [evaluation strategy](../../evaluation/evaluation-strategy.md),
  [fixture guidelines](../../evaluation/fixture-guidelines.md),
  [anti-overfitting rules](../../evaluation/anti-overfitting-rules.md),
  [product/evaluator boundary](../../evaluation/product-evaluator-boundary.md),
  and [M1 continuation profile](../../evaluation/m1-continuation-verification-profile.md); and
- the current [Slice 5 implementation record](../implementation-records/M1-slice-5.md).

Private-fixture governance, retired evaluator plans, incident chronology, and
Slice 4.5 hard-stop records are not ordinary Slice 5 implementation inputs.
They may be read only for separately authorized evaluator work or a specific
historical audit.

Precedence is accepted product requirements/taxonomy, accepted ADRs, accepted
milestone/evaluation specifications and amendments, this plan, then
implementation records and code. Later accepted authority supersedes earlier
text only to its stated extent. This plan may choose physical files, package
seams, deterministic fixture sizes, and commands; it may not create product
semantics. A conflict or missing semantic decision escalates only the affected
decision path under the development execution policy; independent in-scope
work continues.

ADR-0032 is current evaluator authority. Protocol `/5` is retired unqualified
and must not be resumed, reused, repaired, replaced, or used as a model.
Protocol `/4` may run only through its accepted bounded public regression
wrapper with the known `RACE/DATA` gap excluded. It cannot issue a current
product, Slice 5, M1, reliability, readiness, or held-out verdict.

## 3. Baseline and preflight

This proposal was prepared from clean `main` at
`895642131058f4fc9de7fd118347a3f6a559ebdf`, equal to `origin/main` after
`git fetch origin --prune`. The accepted Slice 4.5 closeout and post-closeout
readiness review are present. There was no unrelated worktree change.

Every work package starts with:

```powershell
git fetch origin --prune
git status --short --branch
git rev-parse HEAD
git rev-parse origin/main
git rev-list --left-right --count HEAD...origin/main
```

Stop if the plan is not accepted, changes are unexplained, the branch cannot
fast-forward without rebase, authority materially changed, or work cannot stay
within allowed paths. Never reset, rebase, discard, or overwrite other work.

## 4. Current-baseline inventory

### 4.1 Delivered contracts and schemas

- `Infinium.Domain` declares broad M1 aggregate placeholders for observations,
  deterministic results, claims/application links, candidates, hypotheses,
  findings, recommendations, cases, reconciliation, lineage, coverage, and
  replay. Several semantic fields remain unconstrained strings/dictionaries
  and are not a sufficient Slice 5 contract.
- `RunOutputAggregateContract` names required typed collections.
  `infinium.run-output/v1` is a strict stable payload-reference envelope with
  collection states, taxonomy, coverage, readiness, replay, and audit, but no
  runtime producer publishes a complete instance.
- Analyzer declaration, effective configuration, CLI summary, diagnostic,
  fixture, replay-dependency, evaluation-assertion, Bethesda, taxonomy, and
  common JSON schemas exist. They do not define exact Slice 5 payload/state
  transitions.
- Protobuf v1 covers identities, lifecycle queries, coordinator protocol,
  workers, and credential helper, not the analytical aggregate/result query.

### 4.2 Actual seams and gaps

| Surface | Delivered starting point | Genuine Slice 5 gap |
|---|---|---|
| Producer | MO2 snapshot and Bethesda extraction produce sealed local facts/indexes. | `Infinium.Analysis` is empty except its project file; no documentation, evidence, candidate, finding, case, grouping, or replay producer exists. |
| Consumer | Domain invariants/codecs validate generic envelopes; tests consume MO2/Bethesda outputs. | No analyzer consumes source passages plus Bethesda indexes or enforces Slice 5 transitions. |
| Persistence | `AuthoritativeStore` schema `3`, storage contract `1.2.0`, SQLite/CAS publication, payload ownership, and append-only finding/case/reconciliation/lineage substrate exist. | No complete typed analytical tables/edges, publication API, result projection, or replay manifest exists. Existing occurrence tables are substrate only. |
| Worker/coordinator | Coordinator seals Bethesda input, dispatches bounded worker, validates output, and alone publishes. | No analysis assignment/phase graph, host admission, result publication, or replay operation exists. |
| Wire/query | Named-pipe lifecycle RPC and identity contracts exist. | No bounded coordinator-owned analytical result query exists. |
| CLI/output | `start`, `status`, `wait`, `cancel`, `inspect`, and lifecycle `--json` exist. | `inspect` has no Slice 5 summary/cases/coverage/gaps/unsupported output; no complete run-owned output is published. |
| Replay | Resolved manifests, checkpoints, CAS, backup/restore, and replay document shapes exist. | No downstream closure/manifest, clean/incremental equivalence, or replay comparison exists. |

### 4.3 Public fixtures and infrastructure

Executable public packages currently comprise `M1-PLAT-SLICE2-SUBSTRATE-v1`
and `BETH-NPC-DEV`, `BETH-REFR-DEV`, `BETH-LIGHT-VAL`,
`BETH-MALFORMED-VAL`, and `BETH-UNSUPPORTED-VAL`, plus the independent Slice 3
construction record. Accepted specifications reserve Section 9 Slice 5
identities, but executable payloads and independently expected oracles do not
exist.

MSTest unit, contract, integration, and evaluation projects expose `M1Unit`,
`M1Contract`, `M1Integration`, `M1Evaluation`, `M1Security`, and `M1Fault`
floors. Strict schema readers, fixture contracts, mutation/failure tooling,
protected-root canaries, and bounded `/4` regression are reusable. Product
output remains forbidden as fixture truth.

### 4.4 Inherited Slice 0–4 dependencies

- Slice 0: pinned build/dependency/test policy.
- Slice 1: domain/wire/output contract scaffolding and strict validation.
- Slice 2: one coordinator, immutable runs, bounded workers, SQLite/CAS,
  publication, lifecycle/checkpoint/recovery, query and write confinement.
- Slice 3: MO2 `2.5.2` snapshot, accepted order, exact target rejection, and
  non-mutation.
- Slice 3.5: independently authored public Bethesda fixtures/oracles.
- Slice 4: Mutagen `0.54.2` extraction, exact supported shapes, typed indexes,
  fixed coverage populations, taxonomy projections, layered gaps, and sealed
  publication.
- Slice 4.5: evaluator deferral, `/5` retirement, bounded `/4` regression, and
  continuation authority; no private verdict.

Slice 5 consumes rather than widens these. Archive, PEX/VMAD, native,
configuration, generator, asset-parser, and broader-record strata remain
lead/gap/routing-only where no bounded analyzer exists.

## 5. Scope

### Included

- clean-break domain, JSON, persistence, worker/coordinator, query, CLI/output,
  and replay contracts;
- retained project-authored local/fixture revisions and exact passages;
- deterministic schema-bound claim import, applicability/application links,
  and evidence-bounded declared-purpose assignments;
- local observations/results and explicit no-LLM involvement;
- causal joins, deterministic/mandatory/optional lanes, score-independent
  admission, exact populations, and resource limits;
- typed decisions, candidates, hypotheses, abstentions, findings,
  recommendations, supported/lead-only cases, grouping, lineage,
  reconciliation, taxonomy, coverage/gaps/unsupported/failures;
- atomic SQLite/CAS publication, coordinator queries, complete synthetic
  replay, clean/incremental equivalence, human CLI and versioned JSON; and
- independently expected public fixtures and Layers 1–4/6 evidence.

### Excluded

- evaluator-private access/enumeration; corpus, qualification, comparison,
  adaptation, B2, C2, Stage D, or scoring;
- protocol `/5`, future protocol identity, or unbounded `/4`;
- live/billable calls, credentials, hosted search, Nexus, provider budgets,
  prompts/model admission, or `Infinium.OpenAI` implementation;
- legacy archive access; Slice 6 provider integration; Slice 7 generic
  mechanism; Slice 8 controlled-real cases; Slice 9 closeout;
- private/real-mod fixtures, names, fixture-specific rules, product-derived
  answers, LOOT, archive-positive FaceGen, PEX/VMAD/native/named-generator/
  configuration/NIF/runtime-log semantics, UI, readiness calibration, or a
  safety score; and
- automatic ambiguity adjudication, merge/split, disposition carryover,
  deletion UI, export, or retention-policy expansion.

## 6. Traceability

### 6.1 Requirements and ADRs

| Area | Primary requirements | Decisions | Package |
|---|---|---|---|
| Evidence/provenance | EVID-001–007, SNAP-006, OPS-002 | ADR-0015, ADR-0029 | WP1, WP2, WP5 |
| Documentation/claims | DOC-002, DOC-006–008, DOC-011 | ADR-0015–0017 | WP2 |
| Analyzer/candidates | ANALYSIS-003–005 construction boundary, ANALYSIS-016–017, ANALYSIS-019, EVID-005, OPS-004 | ADR-0015–0017, ADR-0023, ADR-0028–0029 | WP1, WP3 |
| Findings/cases | FIND-001–014 | ADR-0015, ADR-0016, ADR-0022 | WP3, WP4 |
| Taxonomy/coverage | COVER-001–003 and taxonomy requirements | ADR-0028, ADR-0029 | WP1, WP4, WP5 |
| Run binding/replay | SNAP-001–006, SCAN-005–007, SCAN-009, OPS-002–004 | ADR-0015, ADR-0016, ADR-0021–0023 | WP1, WP3, WP5 |
| Output/local operation | PROD-002, PROD-004, OPS-001–003, AUTH-001–003, SEC-001–004 applicable no-secret/no-credential paths | ADR-0018, ADR-0019, ADR-0021 | WP2, WP5 |
| Disabled provider boundary | AI-003/AI-006 minimization/reproducibility shapes and AI-004/AI-007 no-dispatch/no-shared-authority paths | ADR-0013, ADR-0020, ADR-0023, ADR-0025 | WP1, WP2, WP5 |
| Public verification | M1 sequencing and claim limits | ADR-0027, ADR-0032 | all; WP6 closeout |

### 6.2 Evaluation cases

| Case | Slice 5 pass boundary | Package |
|---|---|---|
| EVAL-0026 | Immutable run/context/config/input bindings across clean, incremental, replay. | WP5 |
| EVAL-0032 | 100% declared supported/mandatory recall, explicit negatives/gaps, causal joins, no all-pairs, mutations, counts, equivalence. | WP3, WP5 |
| EVAL-0033, EVAL-0035 | Content inert; writes use fixed authorized product classes. | WP2, WP5 |
| EVAL-0034 | Local evidence/diagnostics minimize unrelated paths and secret-shaped canaries; no prompt or credential surface exists. | WP2, WP5 |
| EVAL-0036 | Lead-only investigation is separately counted and cannot affect readiness. | WP4, WP5 |
| EVAL-0037 | Clean analysis/import, reuse, and changed revision identity remain separate; no refresh transport added. | WP2, WP5 |
| EVAL-0038 | Durable phase/checkpoint recovery and coordinator-only publication. | WP5 |
| EVAL-0039 | Import run, revision, passage, application link, supplying snapshot, ownership traversable. | WP2 |
| EVAL-0040 | Run-owned human/JSON result survives partial terminal outcomes; no export implied. | WP5 |
| EVAL-0045, EVAL-0046 | Explicit CLI initiation; no MO2/game/tool launch/write. | WP5 |
| EVAL-0051, EVAL-0052, EVAL-0054 | Delivered MO2/Bethesda public substrate remains unchanged. | WP6 |
| EVAL-0064 | Local path works network-off/no credential; external boundaries `not-used`. | WP2, WP5 |
| EVAL-0065 | Complete immutable local analyzer declaration and honest unsupported scope. | WP1, WP3 |
| EVAL-0067 | Type/authority/raw/contradiction/abstention and `llm = none`; no live/inert model claim. | WP1–WP4 |
| EVAL-0079 | Immutable occurrences, four-gate reconciliation/outcomes, lineage, lead promotion, no carryover. | WP4, WP5 |
| EVAL-0080 | Database/CAS/staging/trace/run-output writes confined and audited. | WP5 |
| EVAL-0082 | Independent controls; raw/skipped output visible; provider disabled. | WP1, WP3, WP5 |
| EVAL-0083 | Local source-to-conclusion provenance, contradiction/deletion gap; external nodes `not-used`. | WP2, WP4, WP5 |
| EVAL-0084 | Shared-cause grouping, lead separation, exact/metamorphic fixtures. | WP4 |
| EVAL-0085 | Human/JSON denominators/states/gaps/unsupported agree without safety claim. | WP4, WP5 |
| EVAL-0086 | Purpose/state/role/axis separation and historical taxonomy versions for Slice 5 subjects. | WP2, WP4 |
| EVAL-0087 | Migration/publication/recovery/backup/deletion gap/projection/replay integrity. | WP1, WP5 |
| EVAL-0088 | Bounded result queries, protocol, assignment/admission, cursor, restart. | WP5 |

EVAL-0001/0002/0016/0017 are not Slice 5 accuracy gates. Slice 5 builds their
plumbing with generic synthetic inputs; Slices 7/8 own mechanism/controlled-real
proof. EVAL-0078 change-impact presentation is not delivered; Slice 5 proves
only its lower-level dependency invalidation/carryover substrate. Provider/
credential portions of EVAL-0034/0076/0077/0081/0089 are not applicable to the
delivered local path; their adapters remain absent/disabled, budgets and usage
remain zero, and only inherited no-secret/no-provider refusal regressions run.

### 6.3 Continuation layers

| Layer | Exact evidence |
|---|---|
| 1 — contracts/schema | Closed states; completed M1 v1 envelopes; typed payload schemas; `M1-S5-0004`; strict cross-seam tests; no omitted applicable state. |
| 2 — public fixtures | Pre-registered independent execution inputs/oracles; positive, negative, malformed, unsupported, ambiguous, partial, abstention, coverage/gap; no output authority. |
| 3 — mutation/metamorphic | State totality; malformed rejection; rename/reorder/permutation invariance; dependent-only invalidation; score/contradiction/deletion checks. |
| 4 — replay/integration/safety | Clean/incremental equality; complete replay; drift failure; atomic publication/recovery; write canaries; full suite. |
| 6 — fresh review | Exact commit/checklist; finding/correction ledger; one material correction cycle; final judgment and bounded claim. |

Layer 5 starts in Slice 7/8, not Slice 5. Plumbing fixtures cannot be labeled
generic semantic or controlled-real proof.

### 6.4 Fixture and artifact ownership

| Fixture/artifact family | Construction/contract owner | Behavior/gate owner |
|---|---|---|
| `EVID-*`, local `PROV-*`, and hostile-document packages | WP2 | WP2, then replay/output in WP5 |
| `CAND-*` atomic/integration/scale/stress packages | WP3 | WP3, then equivalence in WP5 |
| `CASE-*`, `COVER-*`, and `TAX-*` packages | WP4 | WP4, then reporting/replay in WP5 |
| Publication, clean-layer, write, persistence, IPC, replay, query, and output packages | WP5 | WP5 |
| Comprehensive clean/incremental/replay/cross-stage corpus | WP6 | WP6 |
| Documentation revision, exact passage, claim, application, purpose assignment | WP1 schema | WP2 producer/storage; WP5 query/replay |
| Eligible decision, candidate, hypothesis, abstention, dependency closure | WP1 schema | WP3 producer/storage; WP5 replay |
| Finding, recommendation, case occurrence/logical identity, reconciliation, lineage | WP1 schema | WP4 producer/storage; WP5 publication/query |
| Coverage, gap, unsupported, failure, run output, dependency/replay manifest, effect receipt | WP1 schema | WP4 population semantics; WP5 publication/output/replay |
| Implementation record and final traceability/result index | WP1 starts ledger | Every package appends evidence; WP6 audits/closes |

Fixture construction ownership does not grant expectation authority to product
behavior. The independent author/reviewer separation in Section 9 remains the
gate for every row.

## 7. Clean-break contracts and schemas

WP1 replaces weak Slice 1 placeholders rather than adding compatibility
wrappers. Active producers, consumers, persistence, wire/query, output, replay,
fixtures, and tests move together. Historical outputs remain historical; no
runtime migration from an earlier incomplete v1 shape or dual reader is added.

### 7.1 New payload schemas

| File | Schema ID | Required contents |
|---|---|---|
| `documentation-evidence.v1.schema.json` | `infinium.documentation.evidence/v1` | Revision/passage offsets and fingerprints, import, claim kind/text/conditions/authority, applicability/application, supplying snapshot, purpose assignment, contradictions, retention/replay state. |
| `candidate-analysis.v1.schema.json` | `infinium.analysis.candidate/v1` | Eligible decision, canonical roles/join/path/closure/population/lane, score-independent rule, disposition, candidate, hypothesis, missing/contradiction/confidence/abstention/limit. |
| `finding-case.v1.schema.json` | `infinium.analysis.finding-case/v1` | Finding/severity/confidence, recommendation, case kind/membership/cause proof, occurrence/logical ID, reconciliation gates/outcome, supersession/lineage/no carryover. |
| `analysis-replay.v1.schema.json` | `infinium.analysis.replay/v1` | Dependency DAG/closures, versions/fingerprints, mode, output hashes, replay/audit/missing/equivalence, coverage/gaps/effect receipts. |
| `analysis-execution-input.v1.schema.json` | `infinium.analysis.execution-input/v1` | Answer-free run/Bethesda/source/analyzer/config/limit/seed/mode/prior/not-used bindings; no expected label/oracle reference. |

Schemas are closed (`additionalProperties: false`), bounded, strict, and use
opaque IDs/lowercase SHA-256. Absent, unknown, unsupported, not-applicable,
failed, limited, and unavailable are explicit. Prose is never authority.

### 7.2 Active M1 v1 envelope completion

These accepted M1 v1 documents are completed in place and remain the only
active Slice 5 envelopes:

- `analyzer-declaration.v1` / `infinium.analyzer.declaration/v1`;
- `effective-scan-configuration.v1` / `infinium.scan.effective-configuration/v1`;
- `run-output.v1` / `infinium.run-output/v1`;
- `cli-summary.v1` / `infinium.cli-summary/v1`;
- `fixture-execution-input.v1`, `fixture-oracle.v1`,
  `replay-dependencies.v1`, and `evaluation-assertion-result.v1`.

Slice 1 deliberately scaffolded these milestone contracts before a complete
producer existed. Slice 5 closes their fields/invariants without changing the
accepted major identity. Every current producer, consumer, schema validator,
tracked public fixture, and test moves together; historical Git revisions
remain the record of earlier bytes. No permissive legacy reader, dual current
shape, or compatibility branch is added. `run-output/v1` keeps payload
references but requires payload schema/version, revision/state/hash/length/
availability/provenance/closure and adds revisions/passages, candidate
decisions, reconciliation/lineage, replay manifest, `not-used` boundaries, and
separate unsupported/coverage populations. Human output derives from the same
admitted aggregate.

### 7.3 Domain, wire, storage, and output

- Replace free semantic strings with closed source/passage/claim,
  applicability, lane/disposition, threshold, confidence, severity,
  recommendation, case, reconciliation, lineage, coverage, replay, and audit
  values. Finding confidence is `confirmed`, `strongly-supported`, or
  `plausible`; `speculative-lead` is not a finding.
- Add `contracts/protobuf/infinium/domain/v1/analysis.proto` and additive
  application-v1 bounded summary/artifact/provenance/replay queries. Protocol
  major stays `1` because no existing message is reinterpreted; unknown success
  values fail closed.
- Add migration `M1-S5-0004`, schema `4`, storage contract `1.3.0`, creating
  append-only revisions/passages/evidence/application links/candidate decisions/
  candidates/hypotheses/recommendations/taxonomy/coverage/gaps/dependency edges/
  membership/replay/output tables and extending occurrence/reconciliation/
  lineage substrate without name/hash identity.
- Coordinator publication is one transaction after CAS admission and includes
  artifacts/edges, coverage/gaps, replay, payload owners, output receipt, and
  terminal transition. Workers write bounded staging only.
- Extend `inspect <run-id>`: human mode prints outcome, distinct finding/
  supported/lead counts, cases/leads, per-population coverage, unsupported,
  gaps/failures, replay/audit, no-safety qualifier, cost/duration/result ID.
  `--json` returns run-output/v1. Both use coordinator query, never direct DB.

## 8. State model and thresholds

### 8.1 Total state classes

| Class | Retained result |
|---|---|
| Positive supported | Required/mandatory candidate, evidence-bound hypothesis, and only if promotion passes, finding/recommendation/supported case. |
| Meaningful negative | Eligible decision `resolved-negative`, reason/evidence/population count, no finding. |
| Missing | Missing-information abstention/gap; no invented value/claim/participant/applicability/continuity. |
| Malformed | `invalid-input` with bounded safe diagnostic; unrelated work continues. |
| Unsupported | Explicit source/analyzer/capability population/gap; no silent skip/promotion. |
| Ambiguous | Candidate/hypothesis plus `needs-input` or lead-only; no finding/readiness/guessed grouping/identity. |
| Partial | Earlier valid evidence survives; exact completed-with-gaps/failed/limited state; bounded downstream claim. |
| Abstention | Stage, rule/version, missing requirement, evidence, needed information; separately counted. |
| Coverage | Labeled denominator/completed/state/exclusions/taxonomy/gaps/failures/run scope. |
| Gap | Owning population/stage, reason, missing capability/info/dependency, replay/conclusion effect, provenance. |

Empty, not-applicable, not-used, failed, cancelled, and limit-reached remain
distinct. Unknown is never null, omission, empty string, unsupported, false,
or zero.

### 8.2 Transition rules

1. Every candidate-denominator member has an eligible relationship decision.
2. Candidate requires canonical participants/roles, bounded join/path,
   population/lane/rationale/evidence/closure. `deterministic-required` and
   `mandatory-evidence` admit when structural predicates pass; ranking only
   orders `optional-ranked` work.
3. Hypothesis requires candidate, causal explanation/predicted impact,
   supporting/contradicting/missing evidence, and threshold/ruleset identity.
4. Finding requires at least `plausible` support and every analyzer promotion
   predicate. A defeating contradiction, unsupported dependency, missing
   applicability, or speculative-only support yields lead/abstention. Maturity
   and ranking do not change the boundary.
5. Recommendation requires finding or explicit abstention/needs-information
   and records evidence, uncertainty, reversibility, risks; it executes nothing.
6. Supported case has a finding and independent cause proof. Lead-only has
   hypotheses/candidates, zero findings, separate count, no readiness effect.

No blended numeric evidence/severity/confidence/taxonomy/priority score is
introduced. Inspectable versioned rule predicates are threshold authority.

### 8.3 Documentation and identity behavior

- Source revisions are immutable/byte-fingerprinted; changed bytes always make
  a revision even when claims are equivalent.
- Passages use deterministic UTF-8 byte offsets and passage fingerprint;
  unavailable/deleted content cannot authorize new derivation.
- Deterministic import accepts only project-authored v1 fixture claim documents
  and validates type, passage, applicability, authority, declared-purpose role,
  contradiction/unsupported. It does not infer uncited prose.
- Import, recomputation, source reuse/refresh, and per-analysis application are
  independent. Import alone creates no finding/readiness.
- Declared purpose comes only from admitted author evidence with role
  `declared`, never filename, EDID, order, taxonomy, model, or expectation.
- Instructions/commands/roles/paths/URLs/SQL in sources remain inert data.
- Grouping requires canonical shared-cause proof: cause, locus, applicability,
  dependency closure, members. Similarity without proof is separate/ambiguous.
- Automatic reconciliation requires unique one-to-one causal, applicability,
  dependency, and producer equivalence. Outcomes are exactly
  `exact-continuation`, `analytical-revision`, `related-follow-up`,
  `new-distinct`, `ambiguous`, `unknown`, `not-observed`, `not-evaluated`.
  History is append-only; lead promotion creates a successor and
  `promotes-lead`; review/disposition carryover is not implemented.

## 9. Public fixtures and mutations

### 9.1 Required packages

The behavior-owning work package preregisters the smallest packages needed for
its exact positive, matched-negative, malformed, unsupported, ambiguous,
partial, abstention, coverage, and gap obligations. Package identities are
assigned only when the corresponding product interface and independent truth
model exist. The 28 rejected WP1 identities are historical failed-work
evidence in the implementation record and are not reserved or current package
authority.

`EVID-LLM-VAL`, `PROV-SOURCE-LLM-VAL`, all `LLM-*`, `PROV-LIVE-*`,
controlled-real, and every `*-HO-*` slot are excluded.

WP3 selects and preregisters candidate scale/stress populations and ceilings
from a directly supplied or product-reachable deterministic expansion
contract. Exact expected counts come from an independent declarative/reference
model and freeze before selector comparison. Scale evidence remains bounded M1
structural evidence, not an M3 performance claim; infeasibility requires a plan
amendment, not post-output count tuning.

### 9.2 Independence

Each behavior-owning package has a public manifest, answer-free execution input, isolated oracle,
provenance, replay dependencies, redistribution, and partition history.
Generator truth/hand-authored causal matrices—not product output—author the
oracle, frozen before evaluated behavior. Production receives execution input
only. An identified author records method; an independent reviewer validates
against authority/generator/source truth without product output. Its cases
gate only that package and declared successors after review passes. WP6
performs the accumulated cross-package review.

Answer isolation applies recursively to every product-reachable referenced
payload, not only to `execution-input.json`. Input property names, values,
identities, labels, prose, topology annotations, and ordering may describe
facts available to the product but may not encode expected disposition,
winner, match/control class, supported/unsupported answer, causal conclusion,
oracle membership, or adjudication. Fixture IDs, package paths, partitions,
seeds, and author/reviewer metadata remain harness-only. The verifier rejects
answer-bearing aliases and recursively follows every retained product input
reference.

An oracle retains exact typed expected objects, states, identities,
relationships, counts, coverage denominators, gaps, and canonical comparison
values. A digest of English expectation prose is metadata, never the expected
value. Every admitted candidate decision maps one-to-one to an exact expected
candidate; every resolved negative, unsupported, malformed, ambiguous,
limited, and abstained population member is explicitly represented. Positive
packages may not substitute an empty collection plus a coarse fingerprint for
typed expected output.

Package closure is exact: the registry lists every file and no extra file may
exist; each document length/hash is recomputed; the package aggregate is
recomputed from stable ordered document identities; source offsets and bytes
are checked; and independent review state must be terminal-passed before a
package can gate later work. Regeneration must reproduce all non-review bytes
and structural identities exactly.

If validation output changes behavior/schema/threshold/ranking, transition it
to development and create an independent validation replacement. Never edit
an oracle repeatedly to pass. Use invented generic identities; no zone, title,
race, species, pilot, real mod, fixture ID/path/seed/canary/order becomes a
production exception.

### 9.3 Mutations and metamorphs

The retained harness shall reorder and rename display-only inputs; insert
unrelated entities/passages/edges/taxonomy; perturb optional scores/ties;
duplicate IDs/edges/citations/members/JSON properties; remove/null/corrupt/
oversize/unavailable each required field/dependency; change only relevant
winner/revision/applicability/dependency/producer; alter taxonomy without cause;
add/remove contradictions; delete source body after publication; and crash at
staging, CAS admission, publication, checkpoint, terminal, replay comparison,
and projection rebuild. Each has independently computed typed expectation;
comparisons never use display prose.

## 10. Dependency graph and review policy

```text
WP1 contracts/schemas/states/migration/boundary
 -> WP2 documentation evidence/claims
 -> WP3 joins/candidates/hypotheses
 -> WP4 findings/cases/lineage/coverage
 -> WP5 publication/replay/query/output/recovery
 -> WP6 accumulated verification/review/closeout
```

The recommended split matches real dependencies. WP1 establishes the initial
cross-seam product contract without preauthoring later semantic truth. WP2 precedes WP3 because applicability/
purpose are inputs. WP3 precedes WP4 because conclusions require retained
candidate decisions. WP5 integrates after semantic objects stabilize; earlier
packages still add typed storage adapters/tests so WP5 is not schema redesign.

WP1 contract surfaces remain `Implementation-active` throughout WP2-WP5.
Each behavior-owning package may revise them when producer/consumer,
persistence, invalid-state, or focused-fixture evidence requires it, with all
affected seams updated together. Producer/consumer-validated surfaces become
`Slice-frozen` only when WP6 closes and Slice 5 is accepted.

Every package uses the repository development loop: implement a vertical
increment, run focused checks, perform semantic/diff review, classify findings,
correct must-fix defects, and re-review until accepted. There is no fixed
correction-pass budget for ordinary Slice 5 work. Expected-output changes still
require independent evidence and review; product output never becomes fixture
truth.

Before the focused WP1–WP5 commands, run exactly:

```powershell
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
```

## 11. `M1/S5/WP1` — Contracts, schemas, states, migration, and authority boundary

**Objective.** Establish the total clean-break product contract, storage
migration, state invariants, and enforceable product/evaluator boundary before
runtime behavior.

**Prerequisites.** Accepted plan and recovery amendment; Section 2 authority;
recorded dirty-path recovery inventory and isolated WP1 worktree.

**Allowed paths/actions.** `src/Infinium.Domain/Contracts/`, contract codecs in
`src/Infinium.Application/`, `contracts/json-schema/`, `contracts/protobuf/`,
`src/Infinium.Persistence/` migration/schema only, contract/unit/evaluation
schema tests, minimal answer-free contract examples, and
`eng/verify-m1-slice5.ps1` contract mode. The 2026-08-08 owner
amendment additionally permits the now-retired pre-v2 public-fixture
compatibility surface, current public pre-Slice-5 fixture packages and tests, repository
authority/navigation documents including `AGENTS.md`, and focused
archive/authority enforcement tooling and tests.

**Prohibited.** Analysis behavior; source import; selection; findings/cases;
coordinator/worker/CLI runtime changes; private/live/abandoned-implementation/
later-slice work; frozen evaluator `/4` core or protocol changes; expectations
derived from product execution.

**Deliverables.** Section 7 contracts and Section 8 states; migration
`M1-S5-0004`, schema `4`, storage contract `1.3.0`; strict codecs/invariants;
minimal answer-free schema examples and serialization/round-trip/closure/state/
migration tests; authority/state/field/edge ledger started in
`docs/plans/implementation-records/M1-slice-5.md`; repository legacy/historical
inventory and integrated quarantine/removal; product/evaluator version-axis
guidance and automated current-authority enforcement; no dependency on the
rejected semantic corpus and no behavior claim.

**Verification.** After locked restore and Release build:

```powershell
dotnet test tests/Infinium.ContractTests/Infinium.ContractTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Slice5Contract"
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Slice5StateModel"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice5.ps1 -Gate Contracts -OutputRoot artifacts/m1-slice5/wp1
```

**Retained evidence.** Contract/schema lists/hashes, migration/schema
fingerprint, rejection matrix, state/edge closure, repository-boundary and
retirement evidence, logs, review/correction/judgment, and the rejected-corpus
identity/disposition record.

**Recoverable failures.** Unclosed state, contract/codec/schema/migration
mismatch, answer-bearing example, incomplete authority enforcement, reachable
archived material, or review defect returns to correction and re-review.

**Escalation conditions.** Conflicting or materially missing accepted product
semantics; required private/evaluator access; frozen `/4` byte drift; or an
authority, security, isolation, protected-root, destructive, or external-effect
boundary that cannot be preserved within WP1 scope.

**Review.** Fresh contract/boundary reviewer checks every cross-seam field,
state totality, answer-free examples, version transition, storage edge, and
prohibited authority. WP1's completed recovery review/correction cycle remains
recorded in its implementation record.

**Unblocks.** `M1/S5/WP2` after the narrowed contract/boundary gate and final
WP1 recovery review pass.

## 12. `M1/S5/WP2` — Documentation evidence and deterministic claims

**Objective.** Implement retained revisions/passages, deterministic claim
import, applicability/application, and declared-purpose assignments with no
model or network.

**Prerequisites.** WP1 complete and reviewed.

**Allowed paths/actions.** `src/Infinium.Analysis/Documentation/`, related
Application/Persistence adapters, local worker/coordinator phase assignment,
WP2 tests/payloads, and verification `Documentation` mode.

**Prohibited.** Candidate/finding/case promotion; provider/model/search/Nexus;
arbitrary prose inference; refresh transport; real/private source; executing
source instructions.

**Deliverables.** Deterministic UTF-8 revision/passage importer; claim schema
validator; import/application identities and supplying-snapshot/dependency
edges; applicability and purpose assignments; contradiction/deletion gaps;
clean-import/reuse separation; typed storage adapter; explicit `llm = none`
and provider/search/Nexus `not-used`.
WP2 also authors, freezes, and independently reviews its documentation,
passage, claim, applicability, contradiction, deletion, and provenance cases
before comparing them with product output.

**Verification.** After locked restore and Release build:

```powershell
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~DocumentationSource|FullyQualifiedName~ClaimImport"
dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~DocumentationEvidence|FullyQualifiedName~CleanLayers"
dotnet test tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~EvidenceTypes|FullyQualifiedName~ProvenanceLocal|FullyQualifiedName~UntrustedDocumentation"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice5.ps1 -Gate Documentation -OutputRoot artifacts/m1-slice5/wp2
```

**Retained evidence.** Revision/passage/claim/application IDs/hashes,
reuse/clean-import counts, passage checks, authority transitions, hostile
canaries, deletion/replay gap, queries, commands, review.

**Recoverable failures.** Import/application collapse, passage/hash/provenance
defects, schema/codec/storage mismatch, incorrect applicability or purpose
assignment, fixture defects, test failures, and review findings return to
correction and re-review.

**Escalation conditions.** Accepted authority cannot determine required source,
passage, applicability, or purpose semantics; completing WP2 requires NLP/model
inference, external/private/legacy access, or content-triggered action; or a
security/isolation boundary cannot be preserved.

**Review.** Fresh evidence/provenance reviewer checks passages, applicability,
purpose authority, inert content, source policy, retention/replay effects, and
that import alone creates no conclusion. Findings are classified under the
development execution policy and must-fix findings are re-reviewed after
correction.

**Unblocks.** `M1/S5/WP3`.

## 13. `M1/S5/WP3` — Causal joins, candidates, hypotheses, and abstention

**Objective.** Consume delivered indexes and WP2 evidence through bounded
causal joins, mandatory lanes, score-independent admission, hypotheses, and
abstention.

**Prerequisites.** WP2 complete and reviewed.

**Allowed paths/actions.** `src/Infinium.Analysis/Candidates/`, analyzer
declarations, phase/checkpoint adapters, related storage, WP3 tests/fixtures,
verification `Candidates`/`CandidateScale` modes.

**Prohibited.** Findings/case grouping; semantics outside delivered substrate;
all-pairs/whole-profile model context; provider/model authority; taxonomy
causality; fixture/name exceptions.

**Deliverables.** Eligible-decision ledger; canonical participants/join/
dependency; deterministic-required, mandatory-evidence, optional-ranked lanes;
limited/unprocessed work; candidate/hypothesis/abstention transitions; exact
counts; independent analyzer execution; targeted invalidation/restart.
WP3 also owns independently expected causal-join, candidate, hypothesis,
abstention, scale, and stress packages, including the product-reachable
population construction and independent declarative/reference count model.

**Verification.** After locked restore and Release build:

```powershell
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~CandidateSelector|FullyQualifiedName~FindingThreshold"
dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~CandidatePipeline|FullyQualifiedName~CandidateCheckpoint"
dotnet test tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~CandidateSelection|FullyQualifiedName~CandidateMutation"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice5.ps1 -Gate Candidates -OutputRoot artifacts/m1-slice5/wp3
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice5.ps1 -Gate CandidateScale -OutputRoot artifacts/m1-slice5/wp3
```

**Retained evidence.** Lane denominators/recall/volume, decision and typed
output counts, structural hashes, score/rename/reorder/winner diffs, resource
measurements, checkpoints, commands, review.

**Recoverable failures.** Missed supported/mandatory candidates, ranking that
removes required work, unbounded implementation, incorrect promotion,
taxonomy/name/fixture leakage, test/fixture defects, and review findings return
to correction and re-review.

**Escalation conditions.** Accepted authority cannot define a required join,
lane, abstention, or scale meaning; completing WP3 requires private/live/model
or later-slice authority; or a security/isolation boundary cannot be
preserved.

**Review.** Fresh candidate/anti-overfitting reviewer checks joins, lanes,
populations, invalidation, limits, raw retention, and forbidden inputs.

**Unblocks.** `M1/S5/WP4`.

## 14. `M1/S5/WP4` — Findings, cases, lineage, coverage, and gaps

**Objective.** Apply promotion thresholds; produce findings/recommendations/
supported and lead-only cases; group causally; persist lineage,
reconciliation, taxonomy, coverage, and gaps.

**Prerequisites.** WP3 complete and reviewed.

**Allowed paths/actions.** `src/Infinium.Analysis/Conclusions/`,
`src/Infinium.Analysis/Cases/`, related persistence/projections, WP4 tests/
fixtures, verification `Cases` mode.

**Prohibited.** New analyzers; automatic disposition/review carryover;
interactive adjudication; readiness calibration; similarity as cause/identity;
controlled-real work.

**Deliverables.** Promotion rules; finding/recommendation; supported/lead
grouping and cause proofs; immutable occurrences/logical IDs; four-gate
reconciliation/outcomes; lead promotion/supersession; separate taxonomy roles/
axes/states; coverage/gaps and no-safety assertions.
WP4 also authors, freezes, and independently reviews finding, recommendation,
case, taxonomy, reconciliation, lineage, coverage, and gap packages.

**Verification.** After locked restore and Release build:

```powershell
dotnet test tests/Infinium.UnitTests/Infinium.UnitTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~FindingPromotion|FullyQualifiedName~CaseGrouping"
dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~LineageReconciliation|FullyQualifiedName~CasePublication"
dotnet test tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~CaseGrouping|FullyQualifiedName~CoveragePresentation|FullyQualifiedName~TaxonomyHistory"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice5.ps1 -Gate Cases -OutputRoot artifacts/m1-slice5/wp4
```

**Retained evidence.** Rule IDs/predicate matrix; output counts; exact groups;
false merge/split/ambiguity; occurrence/logical/reconciliation/lineage IDs;
coverage denominators/states/gaps; taxonomy history; commands/review.

**Recoverable failures.** Below-threshold findings, lead/readiness leakage,
grouping without cause, false continuity, destructive rewrite, implicit
carryover, combined safety scores, test/fixture defects, and review findings
return to correction and re-review.

**Escalation conditions.** Accepted authority cannot determine required
promotion, grouping, lineage, taxonomy, coverage, or gap semantics; completing
WP4 requires new analyzer, private/live, controlled-real, or later-slice
authority; or a security/isolation boundary cannot be preserved.

**Review.** Fresh findings/case/lineage reviewer checks transitions, negatives,
cause proof, gates, taxonomy roles, coverage/gaps, and claim boundary.

**Unblocks.** `M1/S5/WP5`.

## 15. `M1/S5/WP5` — Replay, integration, safety, recovery, and reporting

**Objective.** Integrate coordinator-owned atomic publication, replay, bounded
queries, human/JSON output, failure recovery, and write/non-mutation safety.

**Prerequisites.** WP4 complete and reviewed.

**Allowed paths/actions.** Analysis orchestration, Application, Persistence,
Coordinator, Worker, CLI, additive query adapters, run-output/renderers, WP5
tests/fixtures, verification `Replay`/`Output`/`Safety` modes.

**Prohibited.** Provider/credential/budget/live behavior; export; direct CLI
DB access; setup/game/MO2 writes; new semantics; private/legacy/later slice.

**Deliverables.** Bounded `analysis-v1` assignment; durable phase graph;
 coordinator validation/admission; atomic publication/result query; human/v1
JSON; dependency/replay manifests; clean/incremental/replay equivalence;
identity-drift failure; backup/restore/projection; protected-root canaries;
partial terminal output; crash/stale-worker recovery.
WP5 also owns publication, platform/write-boundary, replay, recovery, query,
and output fixtures and their independently authored expected results.

**Verification.** After locked restore and Release build:

```powershell
dotnet test tests/Infinium.ContractTests/Infinium.ContractTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Slice5Output|FullyQualifiedName~Slice5Query"
dotnet test tests/Infinium.IntegrationTests/Infinium.IntegrationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~AnalysisReplay|FullyQualifiedName~Slice5FailureRecovery|FullyQualifiedName~Slice5Cli"
dotnet test tests/Infinium.EvaluationTests/Infinium.EvaluationTests.csproj -c Release --no-build --nologo --filter "FullyQualifiedName~Slice5Operational|FullyQualifiedName~CleanIncrementalReplay"
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice5.ps1 -Gate Replay -OutputRoot artifacts/m1-slice5/wp5
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice5.ps1 -Gate Output -OutputRoot artifacts/m1-slice5/wp5
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice5.ps1 -Gate Safety -OutputRoot artifacts/m1-slice5/wp5
```

**Retained evidence.** Clean/incremental/replay hashes/semantic diff;
dependency closures; write/effect receipts and canaries; recovery; DB/CAS
integrity/backup/projection; queries/cursors; human/JSON equality; partial/
failure outputs; commands/review.

**Recoverable failures.** Partial publication, output-derived replay truth,
dependency substitution or hidden gaps, direct-DB consumption, worker-owned
publication, unbounded query, history mutation, test/fixture defects, and
review findings return to correction and re-review before acceptance.

**Escalation conditions.** An actual or required protected-root, secret,
private-answer, destructive, or unauthorized external effect; accepted
authority cannot determine required publication/replay semantics; or
completing WP5 requires live/provider/credential/later-slice authority.

**Review.** Fresh integration/security/replay reviewer checks atomicity,
identity/equivalence, failure points, output, write/process/IPC authority, and
no scope expansion.

**Unblocks.** `M1/S5/WP6`.

## 16. `M1/S5/WP6` — Accumulated verification, fresh review, and closeout

**Objective.** Prove the public boundary, close review findings through bounded
in-scope correction/re-review, and produce exact implementation evidence and
an owner acceptance packet.

**Prerequisites.** WP1–WP5 complete with final package reviews/evidence.

**Allowed paths/actions.** Tests/scripts, corrections within prior
paths, implementation record, proposed owner status/index updates.

**Prohibited.** New scope; output-driven oracle changes; private/live/legacy/
later-slice work; self-accepting plan/Slice.

**Deliverables.** Assemble and independently review the comprehensive clean,
incremental, replay, and cross-stage corpus; full floor; `-Gate All`;
traceability ownership audit; final semantic/diff review and correction/
re-review closure; complete record; gaps/no-verdict claim. Only owner marks
accepted/complete.

**Verification.** Focused aggregate:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice5.ps1 -Gate All -OutputRoot artifacts/m1-slice5/wp6
```

Complete common floor:

```powershell
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Unit"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Contract"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Integration"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Evaluation"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Security"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Fault"
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
git diff --check
```

The `/4` wrapper may run only through its existing accepted allowlist/refusal
command recorded by `M1/S4.5/EVAL-CLOSEOUT`, report bounded regression, exclude
the `RACE/DATA` gap, and make no verdict. If command identity drifted, stop and
reconcile public authority rather than invoking evaluator files ad hoc.

**Retained evidence.** Section 17 record, logs/counts/skips, ownership audit,
final diff, review checklist/input commit, findings/correction/decision, local
commit.

**Recoverable failures.** Failing commands, unexplained skips, incomplete
evidence, ordinary authority-document drift, claim overstatement, fixture/test
defects, and review findings return to correction, rerun, and re-review.

**Escalation conditions.** Private-answer contamination or prohibited access;
an unresolved conflict in accepted authority; a required scope/permission
expansion; or a security, protected-root, destructive, or external-effect
boundary that cannot be preserved.

**Review.** Fresh reviewer uses the exact clean implementation commit/diff and
answer-free authority checklist, classifies findings, and returns `ACCEPT`,
`CORRECT`, or `ESCALATE`. `CORRECT` returns must-fix findings to another
bounded correction/re-review cycle; only a policy escalation condition requires
owner disposition.

**Unblocks.** Owner Slice 5 acceptance and `M1/S6` planning, not implementation.

## 17. Retained implementation record

`docs/plans/implementation-records/M1-slice-5.md` must contain:

- status/work IDs, owner acceptance, branch, start/final commits, dirty state,
  dates, authority versions/hashes;
- exact C# contracts, schema IDs/versions/hashes, protobuf messages/RPCs,
  migration `M1-S5-0004`, schema `4`, storage `1.3.0`, tables/indexes/triggers,
  output/CLI/replay versions;
- producer/consumer/storage/wire/output/replay ledger proving no stale active
  reader/writer or compatibility shim;
- staged fixture IDs/versions/partitions/history and all document fingerprints,
  owning WP, author/reviewer/method, generators/seeds/counts/hashes, and
  contamination changes;
- case/layer results, exact commands, pass/fail/skip counts, durations/
  resources, mutations, lane recall/volume, grouping, coverage/gaps, replay
  hashes;
- source/passage/claim/application/dependency and all analytical/logical/
  occurrence/reconciliation/lineage/output/publication IDs;
- write canaries, recovery, DB/CAS, backup/restore/projection, query/output, and
  schema evidence;
- every failure, limit, unsupported/abstention/coverage/replay/audit gap,
  unprocessed population, and skip;
- fresh review input/checklist/classified findings, correction/re-review cycles,
  files, final judgment, and independence;
- explicit no private/corpus/scoring/`/5`/future-protocol/live/billable/legacy/
  controlled-real/Slice-6+ access; and
- exact claim: public synthetic local/fixture Slice 5 conformance only;
  no private held-out product verdict exists, and private held-out evaluation
  remains deferred.

Large logs stay under ignored `artifacts/m1-slice5/{work-package}/`; the tracked
record retains SHA-256, length, command, result, and audit counts.

## 18. Rollback and migration

This is a clean M1 contract break: no v1 analytical data migration, dual
reader, or down migration. Development databases upgrade transactionally from
schema 3 to 4. Failure leaves schema 3 intact; success updates metadata/history.
Unknown newer schemas refuse to open.

Create/verify the normal database/CAS backup pair first. Rollback restores the
pair with pre-Slice-5 executable or recreates a disposable store and reruns
public inputs. Never restore SQLite without CAS, edit append-only facts, delete
user source/output, or reinterpret an earlier scaffold payload as the completed
v1 contract. Recovery may remove only unadmitted fixed-class staging.
Corrections create new runs/revisions/lineage.

## 19. Slice 6 handoff and claim boundary

After owner acceptance, `M1/S6` may plan the OpenAI adapter, model proposal
path, credential/budget/provider gates, and separately authorized live M1
operations. It must consume Slice 5 host-admission contracts without allowing
a model to create local facts, source/operation authority, expected answers,
or automatic finding/case authority.

Slice 5 creates no prompts, provider requests, credentials, live packages,
model admissions, provider provenance, or settlement. Provider/search/Nexus
remain `not-used`. No package begins Slice 6 because the local path completed.

## 20. Owner acceptance decision

The project owner accepted this plan on 2026-08-07. Acceptance confirms:

- clean-break completion of the accepted M1 v1 envelopes and storage schema 4
  are acceptable;
- six-package order and one material correction cycle are proportionate;
- fixed scale/stress sizes/ceilings are public M1 structural evidence, not M3
  performance claims;
- deterministic import is restricted to schema-bound project-authored local/
  fixture documents;
- transition mechanics do not claim Slice 7 mechanism or Slice 8 accuracy; and
- private evaluator, `/5`, live/billable, legacy, and later slices stay closed.

The owner first amended WP1 on 2026-08-08 to authorize comprehensive replacement of
the rejected public fixtures and repository-wide isolation of non-frozen
legacy/historical surfaces. In this sentence, the earlier `legacy` exclusion
continues to mean the abandoned sibling implementation archive and frozen
evaluator history; it no longer prohibits the explicitly bounded in-repository
compatibility cleanup described in the amendment.

The owner then accepted the staged-verification recovery amendment on
2026-08-08. The rejected comprehensive corpus is removed/deferred and has no
product authority. WP1 closes only on contracts, codecs, state invariants,
migration/storage declarations, answer-free examples, repository boundaries,
and fresh contract/boundary review. WP2-WP6 own semantic fixtures incrementally
in dependency order, with WP3 owning scale/stress and WP6 owning the complete
cross-stage corpus. No additional owner acceptance gate is required between a
successful WP1 recovery and WP2.

No unresolved product-authority conflict was found. A later owner change to a
contract, fixture, threshold, migration, or claim boundary requires plan
revision and re-review before the affected work proceeds.
