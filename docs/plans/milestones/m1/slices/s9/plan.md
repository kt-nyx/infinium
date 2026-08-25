# M1 Slice 9: End-to-end output, replay equivalence, and M1 closeout

Status: Accepted
Disposition: Complete plan accepted by the project owner on exact planning
candidate `1dd5419ebb3dea8893f7e45adbe16191cf0e823c`; `docs/current-state.md`
separately activates WP1 through WP7 on exact implementation base
`ce51f2d7fdd9d74083ca8c83f686b1193e867ff0`

Last reviewed: 2026-08-25
Owner: Project owner
Work ID: `M1/S9`
Parent: `M1`
Planning branch: `codex/m1-s9-planning`
Exact planning base and Slice 8 acceptance commit:
`5f176a643d1d44d7c254d3b7e6c48f33944909a9`

## 0. Plain-language outcome and authority

Slice 9 closes M1 by proving that the parts already built and accepted can be
used as one bounded product path. A user must be able to start a run through
the real CLI, wait for its durable completion, read the same result as human
text or stable JSON, and replay the result from retained dependencies without
silently changing what it means. The closeout must do this once with tracked
synthetic inputs and once with the exact accepted controlled-real handoff.

“Complete” is deliberately bounded. It means every M1 stage applicable to the
declared run has an explicit terminal disposition: executed, validly reused,
not applicable, unsupported, failed, limited, or completed with a visible gap.
It does not mean Infinium supports every Skyrim record, archive, visual,
runtime, quest, configuration, mod, provider mode, or safety question.

Slice 9 is an integration-and-evidence slice, not a new analyzer. It must:

- compose the accepted Slice 5 through Slice 8 producers and consumers;
- preserve every frozen contract and retained historical fact;
- expose truthful provenance, coverage, gaps, replay, and lifecycle state;
- pass the complete M1 required-case set on one final implementation
  candidate; and
- create the Slice 9 implementation record as the M1 completion record.

This document defines the accepted work. It does not activate implementation,
controlled-real payload access, credential access, provider or network use,
external effects, private/evaluator work, semantic-oracle work, merge, or push.
Owner acceptance is recorded in
[current project state](../../../../../current-state.md). Live authority is
separately activated there on exact implementation base
`ce51f2d7fdd9d74083ca8c83f686b1193e867ff0`.

## 1. Exact accepted predecessor handoff

### 1.1 Commit and documentation identities

Slice 9 planning starts from the accepted Slice 8 closeout with these distinct
identities:

| Identity | Exact value | Meaning |
|---|---|---|
| Slice 8 product candidate | `c79661cd8eb016e483fa8b7396e7d4997b85d590` | Exact product bytes on which the accepted Slice 8 floor ran. |
| Slice 8 review-ready documentation handoff | `c5c995de7252ebf0002903c2d908fdb3bca80f40` | Documentation-only implementation handoff. |
| Slice 8 acceptance / Slice 9 planning base | `5f176a643d1d44d7c254d3b7e6c48f33944909a9` | Owner acceptance and current navigation state. |

The later acceptance commit must not be relabeled as the product candidate on
which the Slice 8 complete floor ran. Slice 9 verification will bind its own
new final product candidate.

### 1.2 Frozen contracts and storage

Every Slice 5 through Slice 8 product contract is `Slice-frozen`. Slice 9 must
not edit a frozen schema in place, reinterpret a frozen enum or field, weaken a
validator, silently broaden an allowlist, or make a historical compatibility
projection into current product authority.

The immediate predecessor identities are:

- `infinium.analysis.scope-reversion/v2`, version `2.0.0`;
- analyzer family `infinium.scope-reversion` and analyzer ID
  `infinium.scope-reversion.local`;
- analyzer, semantic-contract, and identity-contract versions `2.0.0`;
- ruleset version `1.0.0`;
- declaration fingerprint
  `48b809d3b662215ddf342b931c543af6396225335a3eb68f6c6c0c8d4c9a58d5`;
- taxonomy ID `infinium.mod-impact-taxonomy` and version `0.1.0`;
- storage contract `1.10.0`, schema 11, migration identity
  `M1-S8-WP4-0011`; and
- schema-11 fingerprint
  `73f58a86ef5ff4b046e7d2b45b4612047eeda17515f31d75524a37d7a48d8bba`.

The Slice 7 `infinium.analysis.scope-reversion/v1` and storage 1.9.0/schema 10
identities remain frozen alongside all earlier Slice 5 and Slice 6 families.
The exact accepted inventories and historical identities remain in their
owning implementation records; this plan does not duplicate them or derive
current authority from historical names.

### 1.3 Accepted controlled-real evidence

Slice 8 accepted one answer-free controlled-real handoff with ID
`m1-slice8-research0035-local-v1`, manifest SHA-256
`8972ef0e160b9de04da281d48639b66d8bffcc153504c1d699f654f1eff6ecf5`,
three tracked public manifests, and 26 allowlisted local inputs. Its absolute
root and payload bytes are not tracked and are not read during this planning
task.

The accepted four result members and their bounded facts are:

| Member | Accepted result | Retained gap count |
|---|---|---:|
| NPC positive | Two-member actor cohort; one decision, hypothesis, finding, case, and recommendation. | 4 |
| NPC matched control | Resolved negative; no finding, case, or remediation. | 4, including residual `AIDT` coverage. |
| REFR positive | One decision, hypothesis, finding, case, and recommendation. | 3 |
| REFR matched control | Resolved negative; no finding, case, or remediation. | 3 |

Both positive results and both controls were product-driving development
evidence and therefore ended in the development partition. Their complete
partition history must remain visible. They are developer-owned controlled
conformance results, not held-out evidence.

### 1.4 Retained gaps and closed authority

Slice 9 inherits rather than erases the following gaps:

- runtime behavior and runtime-log application;
- effective archive-member availability and ordering;
- rendered visual correctness, FaceGen runtime appearance, and other visual
  output not established by the bounded record/link facts;
- quest, questline, scene, and broader global-state meaning;
- navmesh and other unsupported record, field, link, localization, generated,
  configuration, and patch-wide surfaces;
- taxonomy breadth outside the exact emitted assignments;
- compatibility and safety outside the exact declared subjects and evidence;
- completeness outside the reported coverage populations; and
- independent semantic reliability, precision/recall, readiness, and M3 trust.

There is no private held-out verdict and no independent semantic verdict.
Independent semantic-oracle qualification remains deferred throughout M1 and
M2 by ADR-0035.

The Slice 8 Git authority is consumed. No product/runtime external-effect
authority is open. In particular, there is no open credential, provider,
billable, network, source-refresh, publication, export, archive, private,
destructive, merge, or push authority.

## 2. Accepted authority and traceability

### 2.1 Direct planning inputs

This plan is controlled by:

- the [accepted M1 milestone plan](../../plan.md);
- the [accepted process-continuation amendment](../../amendments/process-continuation.md);
- the [accepted semantic-oracle deferral amendment](../../amendments/semantic-oracle-deferral.md);
- the [accepted Slice 8 entry](../s8/README.md),
  [full plan](../s8/plan.md), and
  [implementation record](../s8/record.md);
- the [product requirements](../../../../../product/requirements.md),
  [product definition](../../../../../product/product-definition.md),
  [scope and milestones](../../../../../product/scope-and-milestones.md), and
  [severity, confidence, coverage, and readiness](../../../../../product/severity-confidence-and-coverage.md);
- ADR-0001, ADR-0002, ADR-0003, ADR-0008 through ADR-0010, ADR-0015,
  ADR-0016, ADR-0018 through ADR-0023, ADR-0025, ADR-0028, ADR-0029,
  ADR-0032 through ADR-0035; and
- the [M1/M2 product-conformance verification profile](../../../../../evaluation/m1-continuation-verification-profile.md),
  [M1 evaluation baseline](../../../../../evaluation/m1-evaluation-baseline.md),
  [case catalog](../../../../../evaluation/case-catalog.md),
  [fixture guidelines](../../../../../evaluation/fixture-guidelines.md),
  [anti-overfitting rules](../../../../../evaluation/anti-overfitting-rules.md),
  and [product/evaluator boundary](../../../../../evaluation/product-evaluator-boundary.md).

Historical implementation records are used only for the exact retained
evidence and chronology on which this plan depends. Retired evaluator plans,
paths, protocols, and archives are not Slice 9 inputs.

### 2.2 M1 requirement claim boundary

Slice 9 closes only the M1 portions already listed by the milestone plan:

- exact target/profile admission and the bounded MO2, runtime, plugin,
  Bethesda record/link, and qualified loose-provider surfaces;
- read-only acquisition plus approved product-owned writes;
- durable manual lifecycle, snapshots, configuration, provenance,
  persistence, replay, and run-owned output;
- typed documentation, candidates, hypotheses, findings, cases,
  recommendations, taxonomy, coverage, gaps, and continuity;
- bounded provider evidence, exact historical usage/accounting facts, and
  provider-unavailable/local-only behavior; and
- the dependency and license posture already accepted for M1.

Slice 9 does not complete M2 UX, M3 trust/readiness or scale breadth, M4
packaging/distribution, runtime validation, arbitrary exports, broad LOOT or
Nexus integration, archive semantics, or user-facing remediation workflows.

### 2.3 Exact required-case set

The result index must contain exactly the 34 M1-required cases below, each
linked to an accepted specification, requirement set, final-candidate command,
test or harness result, and retained evidence identity:

| Area | Required cases |
|---|---|
| Evidence and semantic output | `EVAL-0001`, `EVAL-0002`, `EVAL-0016`, `EVAL-0017` |
| Snapshot, replay, and lifecycle | `EVAL-0026`, `EVAL-0032`, `EVAL-0037`, `EVAL-0038` |
| Hostile content and isolation | `EVAL-0033`, `EVAL-0034`, `EVAL-0035` |
| Acquisition, output, and initiation | `EVAL-0039`, `EVAL-0040`, `EVAL-0045` |
| Read-only acquisition and local semantics | `EVAL-0046`, `EVAL-0051`, `EVAL-0052`, `EVAL-0054` |
| Provider-local behavior and transparency | `EVAL-0064`, `EVAL-0065`, `EVAL-0067` |
| Continuity | `EVAL-0079` |
| Provider/account disclosure | `EVAL-0076`, `EVAL-0077` |
| Authority, accounting, provenance, gaps, and analysis | `EVAL-0080`, `EVAL-0081`, `EVAL-0082`, `EVAL-0083`, `EVAL-0084`, `EVAL-0085`, `EVAL-0086` |
| Persistence and process/credential recovery | `EVAL-0087`, `EVAL-0088`, `EVAL-0089` |

An expected environmental skip is not a pass for a mandatory case. A case may
use exact accepted predecessor evidence only when the final Slice 9 candidate
revalidates its immutable bytes, authoritative binding, and current consumer
or replay behavior. The index must distinguish the original effect/observation
commit from the final-candidate validation commit; it must not imply that a
provider call or controlled observation occurred again on the final commit.

## 3. Current implementation inspection and the closure gap

Planning inspected the accepted implementation, contracts, storage, replay,
CLI, public fixture authority, tests, and verification entry points at the
exact planning base. The following facts define work; they are not an
implementation authorization.

### 3.1 Existing durable CLI and local output

- `Infinium.Cli` supports `start`, `status`, `wait`, `cancel`, `inspect`, and
  `results` through the coordinator application contract.
- `start --analysis-request` admits one bounded managed analysis request and
  binds its snapshot, context, configuration, manifest, and requested run ID.
- `results --json` returns canonical `infinium.run-output/v1`; human mode
  returns the associated summary. The summary exit code comes from the same
  persisted projection.
- The managed durable execution currently composes documentation evidence,
  candidate analysis, and finding/case analysis. It checkpoints and reuses
  those phases and publishes their run-output/replay bundle atomically.
- `AnalysisPublicationBuilder` currently fills the local deterministic
  collections, but its stable output records no model proposals or proposal
  admissions and reports LLM involvement as `none`.

This is a real product path, but it is not yet the accumulated Slice 5 through
Slice 8 end-to-end path required by Slice 9.

### 3.2 Existing provider and scope-reversion surfaces

- Frozen `infinium.run-output/v2` and `infinium.cli-summary/v2` are additive
  provider supplements over exact local v1 bytes. Their contracts permit at
  most three distinct provider-operation references.
- The current terminal provider publication path builds a supplement for one
  operation at a time. The ordinary CLI `results` command returns the local v1
  result, not a composed provider-plus-scope M1 result.
- The accepted Slice 6 responses, usage, settlements, application decisions,
  and replay edges are durably retained. Their external effects are terminal
  historical facts. Current replay is effect-free and cannot authorize a new
  provider request.
- Slice 7 and Slice 8 have frozen scope-reversion v1/v2 producers, consumers,
  canonical JSON, human renderers, and persistence. Their `scope-results`
  CLI path reads a result file locally; it is separate from durable
  `start`/`wait`/`results` orchestration.
- Schema 11/storage 1.10.0 persists scope-reversion v2 payloads, exact retained
  artifacts, dependencies, invalidations, reopen state, and clean,
  incremental, retained-downstream, and audit-only-unavailable replay
  dispositions.

The closure gap is therefore composition, not missing analyzer meaning: one
durable CLI run does not yet bind the accepted provider evidence and
scope-reversion output into its stable run-owned result.

### 3.3 Existing public conformance and tests

The active public fixture authority is closed-world. It includes the tracked
Slice 7 synthetic scope-reversion package, Bethesda/parser packages,
documentation, candidates, findings/cases, operations, provider conformance,
and cross-stage analysis packages listed by the current registry. Historical
semantic packages are integrity-only non-authorizing evidence. No path or
historical name adds an implicit fixture.

The current tests already cover, among other seams:

- strict run-output v1 schema/codec and contradictory replay rejection;
- managed coordinator execution, CLI output equality, failure recovery,
  clean/incremental replay, missing dependencies, and backup/restore;
- provider request/response, source-application and candidate admission,
  budget, settlement, retained-response replay, no fallback, and secret
  canaries;
- scope-reversion v1/v2 contract totality, genericity, positive/control/
  abstention behavior, persistence, invalidation, replay, and CLI rendering;
- the four accepted controlled-real Slice 8 results and matched controls; and
- the `Gate All` accumulated pipeline plus the Slice 8 focused harness.

What is not yet tested is the Slice 9 acceptance statement itself: two real
CLI-driven accumulated runs, their final stable output, layer-by-layer
equivalence, one final-candidate required-case index, and one M1 completion
record.

## 4. Exact Slice 9 completion model

### 4.1 The two required runs

Slice 9 must retain two final-candidate run families:

1. **Synthetic complete run.** Use only tracked, developer-owned conformance
   inputs and immutable retained dependencies. Exercise every applicable M1
   producer/consumer seam, at least one supported result, one matched or
   resolved negative, one abstention or unavailable state, one coverage gap,
   and one unsupported or failed-path demonstration. No expected answer may be
   copied from product output.
2. **Controlled-real complete run.** Reuse the exact accepted Slice 8
   answer-free handoff after an activation-time containment and identity check.
   Exercise the NPC and REFR positives and matched controls through the same
   durable CLI composition. Preserve all 26 input identities, three public
   manifests, partition transitions, result counts, and retained gaps. Do not
   commit or echo controlled payload bytes.

Both must be started through the shipped CLI application contract, observed to
a durable terminal state, and queried through `results` in human and JSON
modes. Direct in-process harness execution can support diagnosis but cannot
satisfy this gate by itself.

### 4.2 Stage disposition and semantic separation

A complete run does not force unrelated evidence to support one invented
finding. Each stage retains its accepted authority domain:

- deterministic local state remains authoritative for observed profile,
  file, record, link, and configuration facts;
- source extraction remains distinct from local support and applicability;
- provider proposals remain distinct from host admission;
- candidates and hypotheses remain distinct from findings and cases;
- resolved controls, abstentions, unsupported states, failures, and coverage
  gaps remain visible rather than being converted to success; and
- taxonomy describes only evidence-authorized assignments.

The final output may contain independent applicable stage results in one run.
It must not invent a cross-stage causal edge merely to make the run appear more
integrated.

### 4.3 Retained provider evidence

No provider effect is required or permitted. Slice 9 consumes only exact
retained Slice 6 response, application, usage, settlement, and replay records
through their current accepted readers. It must:

- prove the retained bytes and authoritative database identities;
- retain the original live operation and accounting provenance;
- create only current-run reuse/application edges that existing contracts
  authorize;
- perform no credential-store operation, DNS resolution, network request,
  provider dispatch, reservation, or current-run debit; and
- label unavailable or non-applicable provider meaning honestly.

The provider v2 output remains a frozen supplement and must continue to pass
all regression and persistence checks. It does not replace the required
`infinium.run-output/v1` result. Slice 9 must not force a new v2 projection if
its closed availability vocabulary would misdescribe historical replay as a
new current-run live operation.

### 4.4 Replay exactness and equivalence

The plan distinguishes two related claims:

- **Exact retained replay:** every consumed dependency, stage payload, schema,
  codec, and retained response reopens with the exact accepted identity,
  bytes, hash, and dependency closure. No source is silently refreshed and no
  model is called.
- **Semantic equivalence:** a new immutable run necessarily has a new run ID,
  timestamps, transition history, and reuse edges. After excluding only those
  declared run-instance fields, clean, incremental, and retained-downstream
  executions produce the same typed facts, states, taxonomy, findings, cases,
  recommendations, coverage, gaps, exclusions, and claim boundary.

The implementation must publish the exact versioned equivalence projection
and its excluded-field list. It may not compare prose summaries, ignore gaps,
or treat a global hash as proof of dependency-local reuse.

## 5. Scope, exclusions, and contract-impact decision

### 5.1 In scope

- one additive, bounded Slice 9 orchestration envelope used only to admit the
  already accepted M1 stage inputs and limits;
- coordinator-owned execution and publication through the current durable run
  lifecycle and application query contract;
- effect-free retained Slice 6 evidence replay and current-run provenance;
- the frozen Slice 7 synthetic and Slice 8 controlled-real scope-reversion
  producers and consumers;
- a final `infinium.run-output/v1`, semantically equivalent human summary,
  replay manifest, and query projection for each required run;
- schema-11 persistence, atomic publication, reopen, backup/restore,
  invalidation, clean/incremental/retained replay, and audit-only negative
  behavior;
- developer-owned synthetic, malformed, mutation, and metamorphic cases;
- protected-root non-mutation, source-root non-mutation, secret-canary,
  process-cleanup, and no-external-effect reports;
- one repository-governance required-case result index; and
- the Slice 9/M1 completion implementation record and owner handoff.

### 5.2 Excluded

- new analyzer, taxonomy, finding, case, readiness, or remediation meaning;
- any in-place change to a Slice 5 through Slice 8 frozen product contract;
- storage schema 12 or another storage-contract version;
- a new run-output, CLI-summary, provider, scope-reversion, or semantic
  product schema;
- compatibility adapters that accept predecessor input as current product
  input;
- provider, credential, DNS, network, billable, source-refresh, or live-search
  operations;
- private fixtures, evaluator-private repositories, held-out scoring,
  evaluator maintenance, retired protocol execution, or archive access;
- authoring, sealing, registering, comparing, repairing, or using an
  independent semantic oracle;
- controlled-real payload redistribution or tracked payload copies;
- arbitrary export, external publication, product/runtime setup mutation,
  merge, or push;
- broad compatibility, safety, correctness, completeness, precision/recall,
  reliability, readiness, or M3 trust claims; and
- M2, M3, or M4 implementation.

### 5.3 Contract-impact decision

Slice 9 is planned to use the existing frozen product contracts without
changing their bytes or meaning. New code may be an additive producer,
consumer, orchestration adapter, or repository verifier, but it must emit and
consume the exact existing schemas.

The only planned new versioned document is repository-governance evidence for
the required-case index. Accepted implementation paths are:

- `contracts/repository/m1-required-case-result-index.v1.schema.json`; and
- `docs/plans/milestones/m1/slices/s9/evidence/required-case-results.v1.json`.

That index is contract-test metadata, never product input, runtime authority,
or semantic expected truth. Its schema must close all fields and permit only
the exact M1 baseline case IDs.

If WP1 proves that truthful end-to-end publication requires changing a frozen
field's meaning, adding a new product/storage contract, or selecting missing
semantic meaning, work stops before that change. The smallest owner decision
is then a reviewed amendment choosing whether to revise Slice 9 scope or
authorize a separately designed clean-break contract. The implementer may not
make that choice implicitly.

## 6. Producer, consumer, persistence, replay, and output closure

### 6.1 Producer closure

For each run, the coordinator must admit one exact immutable binding for:

- installation snapshot and runtime/MO2 evidence;
- semantic analysis context and effective configuration;
- resolved input manifest and exact source inputs;
- documentation acquisition/application evidence;
- deterministic candidate, hypothesis, finding, case, taxonomy, and coverage
  producers;
- any effect-free retained provider proposal/application inputs;
- scope-reversion v1/v2 work assignment and result as applicable; and
- execution limits and every used/not-used boundary.

An internal Slice 9 orchestration envelope is only a closed manifest over
these existing inputs. It grants no provider, filesystem, credential, or
publication authority by naming an input.

### 6.2 Consumer and output closure

The final v1 publisher must consume exact canonical stage payloads and retain
globally unique stable artifact IDs. It must populate the existing collections
only with their accepted meanings, including model proposals and admissions
when exact retained provider records exist. Every artifact must retain:

- producer and version;
- originating and consuming run identity;
- exact source and supporting/contradicting references;
- LLM involvement and retained invocation identity where applicable;
- state, revision, payload identity, availability, and fingerprint; and
- the exact gaps or abstention that prevent promotion.

The human summary must be derived from the validated final output, not from a
separate aggregation path. Tests must parse both forms and compare the full
semantic projection, lifecycle result, counts, replay state, gaps, exclusions,
and no-safety statement.

### 6.3 Persistence and lifecycle closure

Publication must use existing coordinator-owned schema-11 transactions and
payload adoption. No child process or CLI client may publish directly. The
run may become terminal only when all applicable stage outputs, provenance,
dependencies, coverage, gaps, replay state, output bytes, and query projection
are durably committed.

Tests must cover:

- exact readback and canonical round trip;
- interrupted staging and atomic publication rollback;
- stale attempt/coordinator fence rejection;
- duplicate idempotent publication versus substituted identity rejection;
- pause, cancel, limit, failure, and completed-with-gaps terminal states;
- backup/restore and projection rebuild;
- dependency-local invalidation without rebinding origin;
- missing/corrupt retained payloads and audit-only disclosure; and
- no transition out of a terminal state.

### 6.4 Replay closure

Synthetic proof must support clean, incremental, and retained-downstream
execution from tracked retained dependencies with no unavailable dependency.
Controlled-real proof must support:

- clean and incremental execution while the exact owner-authorized root is
  available;
- retained-downstream replay without reopening that root; and
- an audit-only-unavailable demonstration when clean replay authority is not
  available.

The controlled audit-only case is a truthful negative state, not the required
complete controlled-real run. Any input drift, additional file, missing file,
reparse/escape, hash mismatch, partition mismatch, or substituted manifest
must fail before a controlled payload is consumed or publication occurs.

## 7. Ordered work packages and predecessor gates

Work proceeds on one mutable candidate in order. Passing an internal gate
permits the next in-scope package after Slice 9 activation; it does not open an
external effect or another milestone.

### WP1 — Frozen-boundary inventory and composition design

**Objective.** Turn the accepted handoff and actual code inspection into an
executable closure inventory without changing product behavior.

**Deliverables.**

- exact frozen Slice 5 through Slice 8 contract-artifact/identity inventory,
  semantic regression guard, and compatibility classification for any changed
  producer or consumer implementation file;
- exact 34-case repository result-index schema and empty preregistered index;
- stage-to-output collection/provenance mapping;
- run-instance versus semantic-equivalence field classification;
- exact synthetic dependency manifest and controlled-real identity-only
  manifest reference;
- an activation-time controlled-root containment/read-authority gate; and
- explicit confirmation that schema 11 and frozen v1/v2 contracts can express
  the planned path without reinterpretation.

**Exit gate.** Contract, traceability, closed-schema, mutation, and authority
tests pass. Every frozen contract identity and meaning is unchanged; any
modified implementation file is proven compatible through the affected
producer/consumer surface. If the existing contracts are not sufficient, stop
for the Section 5.3 owner decision before WP2.

### WP2 — Durable accumulated composition and stable output

**Objective.** Add the coordinator-owned Slice 9 composition around existing
frozen producers and consumers.

**Deliverables.**

- bounded internal orchestration admission and exact immutable run binding;
- phase execution/checkpoint integration for the accumulated local/provider-
  replay/scope path;
- additive final v1 publisher with exact provenance and no semantic
  reinterpretation;
- durable final human/JSON/query projection;
- no-effect provider replay and current-run reuse/application edges;
- schema-11 atomic publication and exact readback; and
- terminal lifecycle behavior for success, completed-with-gaps, failure,
  limit, pause/cancel, stale publication, and invalid input.

**Exit gate.** Focused producer, consumer, codec, persistence, lifecycle,
provider-replay, output, invalid-state, and fault tests pass. Existing CLI and
all frozen-contract regressions remain unchanged.

### WP3 — Complete synthetic CLI run

**Objective.** Prove the accumulated path using a tracked developer-owned
package.

**Deliverables.**

- one bounded `M1-S9-SYNTHETIC-v1` conformance package composed from current
  public fixture authority;
- actual CLI `start`, `wait`, human `results`, and JSON `results` execution;
- supported, resolved-negative, abstained/unavailable, gap, and unsupported or
  failed-path evidence;
- exact stage payload, dependency, output, and receipt identities; and
- clean/incremental/retained-downstream evidence with full semantic
  equivalence.

**Exit gate.** The synthetic run is terminal, complete from retained
dependencies, human/JSON-equivalent, non-mutating, secret-free, and bounded to
developer-owned product conformance. No product output authored expected
truth.

### WP4 — Complete controlled-real CLI run

**Prerequisite.** The owner-accepted plan has been separately activated and
`docs/current-state.md` explicitly renews read-only use of the exact Slice 8
handoff. The pre-read gate proves the same handoff ID, manifest SHA-256, 26
allowlisted inputs, three public manifests, non-reparse containment, and
answer-free shape before any payload is opened.

**Objective.** Execute the accepted NPC/REFR positive and matched-control
surface through the same durable CLI composition.

**Deliverables.**

- one complete CLI run preserving all four accepted result members;
- exact result, taxonomy, coverage, partition, provenance, persistence,
  replay, and bounded-claim identities;
- clean and incremental equivalence while the root is available;
- retained-downstream replay without root access;
- audit-only-unavailable and drift/escape negative tests;
- identity/hash/count-only retained receipts with no controlled bytes; and
- zero provider, credential, network, billable, source-mutation, export, or
  publication effects.

**Exit gate.** Every required controlled case passes with the accepted bounded
counts and gaps. The matched controls publish no finding/case/remediation.
Nothing is called held-out, private, safe, complete, reliable, or ready.

### WP5 — Replay, lifecycle, output, and safety closure

**Objective.** Prove the cross-run and failure properties that a happy-path
fixture cannot establish.

**Deliverables.**

- versioned layer-by-layer equivalence projection and comparator;
- dependency-local invalidation, unrelated-change reuse, and relevant-change
  recomputation cases;
- retained provider response replay with zero current debit or dispatch;
- corrupt/missing/stale/substituted payload and manifest failures;
- pause/cancel/limit/restart/stale-fence/atomic-publication cases;
- human/JSON semantic equality and exit-code matrix for complete, completed-
  with-gaps, failed, cancelled, and limit-reached runs;
- protected-root, source-root, controlled-root, and retained-evidence
  before/after non-mutation reports;
- secret and target canary scan across request, staging, database, payload,
  output, diagnostics, receipts, and test logs; and
- zero-survivor process cleanup evidence.

**Exit gate.** Focused Integration, Replay, Output, Security, Fault, and
Evaluation checks pass with no unexplained skip or evidence gap.

### WP6 — Required-case index and accumulated six-layer conformance

**Objective.** Bind every M1-required case and verification layer to one exact
review-ready candidate.

**Deliverables.**

- a generated 34-row required-case result index bound to the review-ready
  product candidate;
- requirement-to-case-to-test/harness-to-receipt traceability;
- explicit separation of final-candidate executions from inherited original
  effect/observation commits;
- all six product-conformance layers: contract/schema, developer-owned
  behavior, mutation/metamorphic, persistence/replay, integration/safety, and
  fresh review;
- bounded elapsed-time, input/output-size, storage, and process observations
  for the two complete runs, without claiming M3 scale or performance;
- complete `Gate All`, Slice 8 regression, and Slice 9 `All` receipts;
  and
- a claim inventory listing every supported statement, exclusion, retained
  gap, evidence class, and prohibited overclaim.

**Exit gate.** Every required case has one accepted specification and a
passing final-candidate validation. No required case is satisfied only by a
document assertion, stale catalog status, skipped test, historical name, or
unrevalidated predecessor receipt.

### WP7 — Consolidated review, correction, final floor, and owner handoff

**Objective.** Review the complete candidate once as a coherent M1 product,
correct it on the same candidate, re-review the affected surface, run the final
floor once when review-ready, and prepare owner acceptance.

**Deliverables.**

- consolidated review ledger using the classifications in Section 10;
- same-candidate corrections and focused rechecks;
- changed-surface semantic/security/provenance/diff re-review;
- one clean final candidate and one complete passing verification floor;
- a temporary candidate-bound completion receipt and result index;
- after the floor passes, `record.md` plus the sanitized result index in a
  documentation-only handoff that names the already fixed product candidate;
- exact product candidate and review-ready documentation handoff identities;
  and
- owner-facing accept/reject/amend request.

**Exit gate.** No must-fix, safety/isolation breach, unexplained failure/skip,
stale link/status, contradictory claim, or accidental authority remains. The
owner decides whether to accept M1. Slice 9 does not self-accept M1 or activate
M2.

## 8. Focused test matrix

Implementation must add or extend tests at the smallest owning seam.

| Surface | Required positive evidence | Required negative/fault evidence |
|---|---|---|
| Frozen contracts | Exact schema, codec, declaration, storage, and predecessor bytes/meaning remain accepted. | One-byte schema, declaration, migration, enum, fingerprint, or path drift fails. |
| Orchestration admission | Exact run/snapshot/context/configuration/manifest/stage identities and bounds admit. | Unknown field/stage, duplicate ID, extra source, stale version, mixed run, or unbounded limit rejects before work. |
| Producer/consumer | Every exact retained stage artifact reaches only its accepted v1 collection and provenance. | Cross-type insertion, missing decision link, invented causal edge, duplicate artifact, or source substitution rejects. |
| Provider replay | Exact retained response/application/accounting records reopen with no effect and no current debit. | Missing/corrupt response, mismatched operation/owner, stale application, or implied retry fails closed. |
| Scope reversion | Synthetic and controlled v1/v2 facts retain positive/control/abstention/gap meaning. | Display-name/order metamorphs do not change meaning; cause/input/partition drift rejects or recomputes. |
| Persistence | Schema-11 atomic publish, reopen, duplicate idempotency, backup/restore, and rebuild agree. | Partial staging, stale fence, corruption, cross-run bind, and substituted retained bytes publish nothing. |
| Replay/equivalence | Clean, incremental, and retained-downstream semantic projections agree at every exercised layer. | Relevant mutation invalidates; unrelated mutation reuses only through exact proof; missing root becomes audit-only. |
| CLI/output | Real process `start`/`wait`/`results` works for both runs; human and JSON agree. | Malformed/oversized input, incompatible projection, missing output, and each non-success terminal state return truthful output/exit code. |
| Safety/isolation | Protected/source roots unchanged; canaries absent; no provider/network/credential effect; zero child survivors. | Reparse/escape, path alias, hostile text, secret echo, role bypass, or publication bypass fails before authority. |
| Result index | Exactly 34 unique baseline cases resolve to current evidence and final candidate. | Unknown/duplicate/missing case, stale commit, skipped-only evidence, absent receipt, or claim mismatch rejects. |

Developer-owned semantic examples may test accepted product contracts. They
must be labeled as conformance evidence and may not be called an independent
oracle, held-out qualification, or reliability verdict. Deterministic byte,
codec, parser, algorithm, and accepted-contract golden references remain
permitted under ADR-0035.

## 9. Verification commands and final accepted floor

### 9.1 Focused development checks

Each work package must run the affected Unit, Contract, Integration,
Evaluation, Security, Fault, replay, CLI, and repository-contract tests. WP2
through WP5 must use fresh temporary stores/output roots and must clean up only
the processes and temporary paths they created.

The Slice 9 verifier should expose at least these gates:

- `Contracts`
- `CompositionSynthetic`
- `CompositionControlledReal`
- `ReplayEquivalence`
- `Output`
- `Safety`
- `RequiredCases`
- `ClaimReview`
- `All`

The verifier must fail on zero matched tests, an unexpected skip, missing
receipt, dirty or substituted controlled identity, extra output file, stale
commit binding, surviving repository-owned process, or any nonzero forbidden-
effect count.

### 9.2 Final floor

After consolidated review and all focused corrections, run from a clean
worktree on one exact committed candidate:

```powershell
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Unit"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Contract"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Integration"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Evaluation"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Security"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "TestCategory=Fault"
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
powershell -NoProfile -ExecutionPolicy Bypass -File eng/validate-documentation.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-analysis-pipeline.ps1 -Gate All -OutputRoot <fresh-pipeline-root>
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice8.ps1 -InputManifest <exact-authorized-manifest> -OutputRoot <fresh-slice8-root>
powershell -NoProfile -ExecutionPolicy Bypass -File eng/verify-m1-slice9.ps1 -Gate All -ControlledInputManifest <exact-authorized-manifest> -OutputRoot <fresh-slice9-root>
git diff --check
git status --short
```

The concrete Slice 8/Slice 9 parameter names must match their implemented
scripts and be recorded verbatim. The final candidate is not accepted unless:

- restore is locked;
- Release build has zero warnings and zero errors;
- every required test and gate passes;
- every skip is declared, explained, and unrelated to mandatory Slice 9
  evidence;
- human and JSON outputs and equivalence projections are inspected;
- all forbidden effect counts are exactly zero;
- all owned test/coordinator/worker/helper processes have ended;
- documentation, formatting, dependencies, and diff checks pass; and
- the repository is clean at the exact candidate.

A failed complete floor is diagnostic. Correct the same working candidate,
run focused checks and changed-surface review, create a new clean candidate,
and restart the floor. Do not retain intermediate candidates as acceptance
evidence or create freeze/bind/record churn.

## 10. Consolidated review and correction policy

The review must cover the complete candidate, not only new files:

1. plan fidelity and requirement/case traceability;
2. frozen-contract and storage-identity preservation;
3. producer/consumer type, cardinality, and provenance correctness;
4. persistence, migration-source, replay, invalidation, backup/restore, and
   lifecycle behavior;
5. CLI/application-query/output equality and boundedness;
6. semantic separation, negative/control behavior, taxonomy, coverage, and
   claim limits;
7. security, protected roots, credentials, provider/effect closure, process
   roles, and diagnostics;
8. fixture authority, answer isolation, anti-overfitting, and ADR-0035;
9. required-case index completeness and exact final-candidate evidence;
10. stale navigation, broken links, contradictory status, and accidental
    implementation authority; and
11. exact diff, generated/lock/dependency files, and test adequacy.

Findings use these classifications:

- **Must-fix:** in-scope defect required for plan acceptance.
- **Follow-up:** useful later work outside the accepted Slice 9 claim.
- **Non-blocking:** understood limitation that does not invalidate the bounded
  claim and is retained explicitly.
- **Owner/authority decision:** accepted meaning or scope is materially
  missing or conflicting.
- **Safety/isolation breach:** continuing the affected path would violate an
  authority, protected-root, credential, private, destructive, or external-
  effect boundary.

Must-fix findings, test failures, schema/codec mismatches, stale documentation,
fixture defects, replay bugs, output drift, and incomplete implementation are
ordinary recoverable work. Correct and re-review them on the same candidate.
If the same conceptual defect recurs after two completed correction attempts,
pause that path for explicit design diagnosis; do not automatically escalate
unrelated work.

Escalation is appropriate only when resolution would choose missing product
meaning, change accepted architecture, expand scope or authority, require an
unavailable owner-controlled dependency after safe alternatives, or violate a
security/private/protected-root/destructive/external-effect boundary.

## 11. Required-case result index

The repository-governance index must be deterministic and closed. Each row
must contain at least:

- exact case ID and accepted specification revision/path;
- linked M1 requirements and owning slice(s);
- final Slice 9 candidate commit;
- evidence class: final execution, deterministic reference, controlled
  integration, retained historical effect plus final replay validation, or
  safety/review;
- exact command, project/filter or harness gate, matched/passed/failed/skipped
  counts, receipt path, byte length, and SHA-256;
- fixture/input manifest IDs and hashes without prohibited payload bytes;
- original observation/effect commit when different from the final validation
  commit;
- disposition `passed`, `failed`, `blocked`, or `not-run`;
- bounded assertion proved by the evidence;
- exclusions, gaps, and skip explanation; and
- reviewer and review disposition.

Only `passed` can satisfy M1 completion. The schema must reject duplicate or
unknown case IDs, omission of any required ID, stale candidate identity,
missing receipt identity, ambiguous effect origin, empty commands, zero
matched tests, or a `passed` row with failures/unexplained mandatory skips.

The index records current product conformance. It does not score product
output against an independent answer set and cannot issue a private, held-out,
semantic-reliability, readiness, or M3 trust verdict.

The final verifier writes the candidate-bound index to a fresh temporary
output root while the exact product candidate is checked out and clean. That
avoids an impossible self-reference: a tracked file cannot contain the commit
ID of the commit that first adds the file. After the complete floor passes,
the sanitized index and completion record are copied into the planned
documentation paths and committed in a documentation-only handoff. Focused
schema, receipt-hash, documentation, diff, and product-tree checks must prove
that the handoff names the exact already-verified product commit and changes no
product byte.

## 12. Claim control and retained gaps

The strongest permitted Slice 9/M1 claim is:

> On one exact final implementation commit, the two declared CLI-driven M1
> run packages and the 34 required public product-conformance cases passed the
> accepted contract, persistence, replay, integration, safety, and fresh-review
> floor for the exact reported inputs, analyzers, fields, boundaries, and
> retained dependencies. The result preserves explicit unsupported surfaces,
> gaps, uncertainty, and historical effect provenance. It is not an independent
> semantic, broad compatibility, safety, reliability, readiness, or M3 trust
> verdict.

The completion record must not claim or imply:

- compatibility with a real modlist beyond the exact controlled members;
- safety of a patch, load order, setup, or playthrough as a whole;
- correct game runtime behavior, rendered visuals, quests, archives, navmesh,
  scripts, or unsupported record/field surfaces;
- complete discovery or absence of all relevant issues;
- measured semantic precision, recall, generalization, or reliability;
- provider repeatability, exact provider billing, or a new live provider
  result on the final commit;
- an independent or private held-out verdict;
- production readiness, “trusted personal preflight,” M3 readiness, or public
  release readiness; or
- authority to mutate, export, publish, merge, or push.

All Section 1.4 gaps remain unless exact in-scope final-candidate evidence
narrows one. New gaps discovered during implementation are added; they are not
hidden to preserve a completion claim.

## 13. Security, isolation, and effect boundaries

Under the separate activation, Slice 9 implementation remains local,
non-elevated, and
read-only toward user setup and controlled inputs. Product writes are limited
to existing product-owned storage, per-attempt staging, diagnostics, and
run-owned local output under the accepted write-authority rules.

The plan requires zero:

- private/evaluator repository or fixture access;
- retired evaluator path or sibling archive access;
- credential read, write, enumeration, replacement, disable, or deletion;
- DNS, public network, provider, or billable operation;
- automatic retry or new reservation;
- controlled-real source mutation or payload redistribution;
- protected MO2/game/mod/configuration/generated-output mutation;
- arbitrary export or external publication;
- semantic-oracle authoring/comparison; and
- merge or push.

Workers remain untrusted staged-output producers, not sandboxes and not
publication authority. The coordinator remains the only durable admission and
publication authority. Git state, commit text, historical markers, fixture
names, or paths cannot grant runtime authority.

## 14. Implementation record and owner-acceptance handoff

WP7 creates `docs/plans/milestones/m1/slices/s9/record.md`. It is both the
Slice 9 implementation record and the M1 completion record. It must retain:

- exact planning, activation, implementation, and candidate commits;
- exact predecessor identities and frozen-contract proof;
- work-package outcomes and material corrections;
- the two final run IDs, manifests, stage dispositions, outputs, replay
  classes, counts, and receipt hashes;
- every required-case index row and its final disposition;
- focused and final commands, pass/fail/skip counts, elapsed times, receipt
  paths, lengths, and hashes;
- bounded input/output, storage, process, and elapsed-time observations without
  an M3 scale, responsiveness, or production-performance claim;
- persistence, backup/restore, replay, equivalence, lifecycle, non-mutation,
  secret-canary, effect-count, and process-cleanup evidence;
- consolidated review findings, classifications, corrections, and re-review;
- every retained and newly discovered gap;
- the exact bounded claim and prohibited overclaims;
- explicit absence of private access, independent semantic verdict, and open
  external-effect authority; and
- owner decision and any acceptance-only documentation handoff.

Implementation completion does not self-freeze a new product contract because
none is planned. All predecessor product contracts remain `Slice-frozen`.
Repository evidence artifacts become accepted only if the owner accepts the
exact final candidate and completion record.

Owner acceptance may mark Slice 9 and M1 complete for the exact bounded public
product-conformance claim. It does not retroactively move the accepted floor
to a documentation-only commit, create an independent semantic verdict,
authorize M2 implementation, reopen provider/effect authority, or authorize a
merge/push unless those actions are separately and explicitly granted.

## 15. Planning review and re-review

The planning package received a consolidated planning review over plan
fidelity and traceability; frozen-contract preservation; semantic and claim
boundaries; provenance, persistence, replay, and lifecycle coverage; test and
verification completeness; security/isolation; navigation and links; status
consistency; and accidental implementation authority.

The same planning candidate was corrected for these must-fix findings:

- the first draft transcribed the Slice 8 v2 declaration and schema-11
  fingerprints incorrectly; both now match the accepted implementation record;
- the first floor draft reused one placeholder output root even though the
  Slice 8 verifier requires a fresh empty root; every verifier now has a
  distinct fresh root;
- the first draft did not distinguish frozen contract identity/meaning from
  compatible implementation changes; WP1 now requires exact contract
  preservation plus affected-surface compatibility proof;
- the first draft would have made the tracked required-case index refer to its
  own as-yet-unknown commit; the final index is now generated against the clean
  product candidate and added only in a verified documentation-only handoff;
  and
- the compact current-state handoff contained a pre-existing contradictory
  sentence saying prohibited external effects remained authorized; it now
  says they remain unauthorized.

Re-review found no remaining must-fix or accidental implementation authority.
Documentation links and metadata, whitespace, status/disposition language,
boundary keywords, work-package ordering, case count, and exact predecessor
identities are revalidated before the planning commit.

## 16. Owner acceptance and next gate

No missing product-meaning decision was found during planning. The accepted
path can use existing accepted contracts and bounded semantics. The project
owner accepts exact planning candidate
`1dd5419ebb3dea8893f7e45adbe16191cf0e823c` as the complete Slice 9 plan.
Acceptance does not activate implementation.

The separate activation records:

- the exact implementation base;
- authority to implement WP1 through WP7 on one candidate under the accepted
  correction policy; and
- renewed read-only use of the exact Slice 8 controlled-real handoff after its
  containment/identity preflight.

`docs/current-state.md` now records that separate activation on exact
implementation base `ce51f2d7fdd9d74083ca8c83f686b1193e867ff0`. It authorizes
ordinary implementation through WP7 and renewed read-only handoff use only
after the required preflight. No provider, credential, network, private,
archive, semantic-oracle, external-effect, merge, or push authority is open.
