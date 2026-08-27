# Frontend Application Foundation — Phase C/WP6 targeted-verification addendum

Status: Proposed
Disposition: Architecture and plan correction; implementation not authorized
Last reviewed: 2026-08-26
Owner: Project owner
Plan ID: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP6/ADDENDUM-TARGETED-VERIFICATION`
Planning base: `7c0ceee255c8b9ef79f4116f848a0938376d6ac3`
Supplements: `TRANSITION/M1-TO-M2/FRONTEND-APPLICATION-FOUNDATION/WP6`

## Plain-language proposal

The current backend can show and review findings, but its targeted-verification
button must still refuse to run. It does not yet know how to take a fresh
post-change snapshot, include every dependency needed by the selected finding
or case, and build an analysis request that the executor can actually run.

This addendum proposes the missing vertical. Infinium first prepares an
inspectable plan from a newly captured installation state. When that plan is
complete and the user explicitly starts it, Infinium creates a normal
`managed-analysis-v1` successor run and records exactly why it exists and how
its results relate to the source. Nothing in this proposal changes current
runtime behavior. Phase D and M2 remain blocked.

## Authority and activation

This addendum becomes implementation authority only if both it and Proposed
ADR-0038 are accepted by the project owner. It supplements rather than rewrites
the accepted plan and ADR-0037. Until acceptance and implementation:

- `StartTargetedVerification` must continue returning typed `Unsupported`;
- the dormant persistence method and `targeted-verification` operation kind
  must remain unreachable;
- WP5, WP6, and Checkpoint C remain under correction;
- Phase D/WP7 must not begin; and
- M2 remains inactive.

## Corrected vertical

```text
exact terminal source run + exactly one finding/case occurrence
  -> coordinator rehydrates canonical source payload and identity
  -> durable, renderer-safe preparation admission
  -> new snapshot capture from the still-confirmed saved profile
  -> full qualified Bethesda semantic extraction on that snapshot
  -> dependency-closed TargetedAnalysisScope planning
  -> targeted CandidateDeliveredInput + resolved manifest
  -> immutable inspectable preparation
  -> fresh one-shot user start
  -> atomic successor run + managed-analysis-v1 operation + initiation lineage
  -> ordinary execution/publication/reconciliation
  -> bounded verification readback with coverage, gaps, and exact lineage
```

Snapshot capture and semantic extraction are prerequisites, not alternative
result runs. The successor result run's executable operation kind is exactly
`managed-analysis-v1`.

## Inputs

- accepted product requirements/workflows for targeted recheck, snapshots,
  result review, continuity, coverage, and readiness;
- ADR-0002, ADR-0010, ADR-0015, ADR-0016, ADR-0019, ADR-0021, ADR-0022, and
  ADR-0037;
- Proposed ADR-0038 and RESEARCH-0058;
- accepted WP1-WP4 and current corrected WP5 result identities/readback;
- current snapshot capture, Bethesda semantic extraction,
  `managed-analysis-v1`, delivered-input, dependency, finding/case, lineage,
  and prepared-start implementation seams; and
- EVAL-0093 plus EVAL-0019/0020/0027/0040/0041/0043/0047/0048/0069/0078/0079.

## Allowed implementation scope after acceptance

- clean-break application contract revision for targeted preparation,
  preparation readback/cancellation, start admission, and verification readback;
- functional domain contracts for targeted scope, preparation, admission,
  reuse decisions, coverage, gaps, and initiation lineage;
- coordinator planners/resolvers that compose existing snapshot capture,
  Bethesda semantic extraction, and `managed-analysis-v1`;
- a deterministic dependency-closed targeted delivered-input producer;
- append-only persistence, migrations, projections, backup/restore, replay, and
  recovery for the corrected vertical;
- generated C# clients and diagnostic consumers needed for Phase C
  producer-consumer validation;
- focused product-conformance fixtures and tests; and
- current documentation, matrix, inventory, and implementation record updates.

The accepted implementation must not add polished UI, TypeScript/React desktop
delivery owned by Phase D, new analyzer families, a generic workflow engine,
generic execution fallback, setup mutation, private evaluation, or M2 work.

## Proposed application contract

The exact protobuf names may change during normal contract review, but the
following closed responsibilities and fields are mandatory.

### BeginTargetedVerificationPreparation

Request:

- durable idempotency key and bounded one-shot preparation gesture;
- source run ID;
- a `oneof` containing exactly one source finding occurrence ID or one source
  case occurrence ID;
- expected confirmed-profile revision and profile identity;
- saved configuration ID and expected revision;
- selected analysis-context ID and expected revision/fingerprint;
- requested preparation ID, optional only where the existing identity policy
  permits server generation; and
- dispatch deadline and allowed initiation kind.

It must not contain paths, `exact_scope_ids`, facts, dependency IDs, operation
kinds, operation JSON, SQL, commands, URLs, provider credentials, or a generic
filter. Admission returns an exact receipt plus preparation ID/revision. An
exact retry returns the same receipt; different meaning conflicts.

### GetTargetedVerificationPreparation

The bounded projection includes:

- preparation identity/revision/state and terminal reason;
- exact source run, occurrence kind/ID, logical ID, payload ID/hash, canonical
  signature, analyzer/semantic/identity versions, and source binding;
- newly captured snapshot ID, capture operation/attempt, capture time,
  structural fingerprint/comparison, semantic-input identity, and profile
  revision;
- selected context, configuration, effective configuration, and resolved
  manifest identities/revisions/fingerprints;
- direct roots and dependency-expanded members, each with typed kind, reason,
  source edge/proof, and whether it is mandatory;
- target analyzers and compatibility proof;
- recomputed and proposed-reuse artifacts with exact validity proofs;
- population denominator, expected work/limits, coverage classes, gaps,
  readiness boundary, and plan fingerprint; and
- `startable`, with exhaustive typed reasons when false.

Lists must be bounded and pageable. The renderer does not receive filesystem
paths, raw payload bodies, arbitrary dependency-graph traversal, or internal
operation requests.

### CancelTargetedVerificationPreparation

The request names the exact preparation/revision, durable command key, and a
fresh user gesture. It prevents new prerequisite stages. Already captured or
published immutable evidence remains retained. Transport cancellation alone
has no durable effect.

### StartTargetedVerification

The current request is a clean-break draft and must be replaced. The corrected
request contains only:

- exact startable preparation ID and expected revision/fingerprint;
- durable command/idempotency key;
- optional requested successor run ID;
- allowed initiation kind;
- fresh one-shot start gesture; and
- future dispatch deadline.

All source/scope/binding fields are resolved from the preparation. The response
uses the ordinary accepted/already-accepted/rejected receipt vocabulary and
returns successor run and targeted-verification lineage identity. Admission is
one transaction. Scheduling follows commit.

### GetTargetedVerification

The bounded read model joins, without rewriting:

- preparation and admission identity;
- source and successor run/occurrence identity;
- new snapshot and run binding;
- direct/expanded scope and managed operation hash;
- current successor lifecycle state;
- targeted population coverage, failures, gaps, reuse, and readiness boundary;
- result reconciliation/lineage outcomes when published; and
- restart/cancel/failure history needed to explain current state.

## TargetedAnalysisScope contract

The scope producer owns executable meaning. Its canonical input is the source
occurrence payload plus retained source-run dependency graph, new semantic
input, selected context/configuration, analyzer declarations, and a versioned
closure policy.

Direct roots are:

- for a finding: its candidate, hypothesis, occurrence/logical identity,
  identity envelope, dependency closure, evidence, affected locus, and every
  participant/role; or
- for a case: all corresponding finding roots plus every case member,
  candidate/hypothesis, shared-cause envelope, cause proof, and member closure.

Expansion must traverse an allowlist of typed dependency relations and include
all required relationship participants, contributing/winning providers,
resolved files/records/assets, analyzer declarations, applicability
populations, and shared-cause members. The algorithm must:

1. validate every root against the exact source run and canonical payload;
2. traverse to a deterministic fixed point;
3. retain each member's reason and proof edge;
4. reject unknown/inconsistent edge semantics;
5. report cycles according to the admitted graph model rather than truncating;
6. reject a closure exceeding its declared member/byte/work bounds;
7. be permutation-independent and monotonic when roots are added; and
8. emit one canonical policy/version/fingerprint-bound scope.

The targeted delivered-input producer creates a valid
`CandidateDeliveredInput` from this scope and the new semantic input. Its
eligible, included, excluded, unsupported, and gap populations share the same
denominator. No later stage may remove mandatory members. If the producer
cannot prove closure, the preparation is not startable.

## Mapping to managed-analysis-v1

The targeted resolver shall create the existing managed request, not a new
operation. It binds:

- successor run ID and `managed-analysis-v1`;
- new installation snapshot ID;
- new Bethesda semantic input ID/fingerprint;
- target effective configuration and selected analysis context;
- a resolved input manifest naming source/preparation/scope/target input and
  every retained external input;
- the supplied targeted delivered input/fingerprint;
- source run as prior analytical context for reconciliation;
- exact allowed phase reuse decisions and proof edges; and
- current hard limits/deadline.

The request's canonical bytes/hash are retained in both the run operation and
targeted admission. The ordinary executor, attempts, checkpoints, publication,
run output, and reconciliation remain authoritative.

## Reuse and recomputation matrix

| Input or artifact | Treatment | Reason |
|---|---|---|
| Source snapshot occurrence | Never reused as target | It cannot observe the external change |
| New snapshot capture | Always performed | It is the post-change observation |
| Bethesda semantic input | Recomputed from new snapshot | Initial design avoids an unqualified partial extractor |
| Candidate delivered input | Recomputed through targeted producer | Its population and dependencies bind the new snapshot and closed scope |
| Candidate decisions/hypotheses/findings/cases | Recomputed | They are snapshot-dependent analytical outputs |
| Finding/case logical identities | Reconciled, not copied | ADR-0022 requires proof of continuity |
| Selected context/configuration | Rebound only at exact current revision | A changed revision invalidates preparation |
| Profile-independent documentation | Reusable only with closure-equivalence proof | Original remains immutable and gets a reuse/application edge |
| Snapshot-dependent documentation/extraction | Recomputed or explicit gap | Changed applicability cannot be assumed |
| Source review disposition/readiness | Never inherited as analysis truth | Review state and readiness are separate views |

## Lineage and immutability

The implementation shall retain these immutable links:

1. preparation -> source run and exact source occurrence/payload/hash;
2. preparation -> new capture operation/attempt/snapshot/semantic input;
3. preparation -> profile/context/configuration/resolved manifest;
4. preparation -> direct roots -> expanded scope members/proof edges;
5. preparation -> reuse decisions/proofs and recomputation declarations;
6. admission -> preparation revision/fingerprint, gesture, command, operation
   request/hash, and successor run;
7. successor run -> normal published results/coverage/gaps; and
8. source occurrence(s) -> successor occurrence(s), only through ADR-0022
   reconciliation and lineage events.

The source result, its disposition, the target preparation, and published
successor result are never updated in place. Current-state projections may be
rebuilt from append-only records.

## Coverage, gaps, and readiness

The prepared closed scope is the denominator. Readback distinguishes:

- direct roots;
- expanded mandatory work;
- attempted and completed members;
- valid reused artifacts;
- failed, unsupported, inaccessible, malformed, cancelled, or not-applicable
  members; and
- mapping/preparation gaps versus execution-discovered gaps.

Mapping gaps that prevent closure make preparation non-startable. Gaps that
arise only while valid work executes are published normally and produce a
limited/completed-with-gaps result. A prior issue may be reported `not
observed` only when its applicable closed scope completed without a material
coverage gap.

WP6 targeted results always expose `scope-limited` or `no-readiness`. They do
not overwrite, replace, or borrow whole-profile readiness, even if the source
run had broader coverage.

## Admission, concurrency, restart, and replay

- Preparation and start each use canonical request fingerprints, durable
  idempotency keys, one-shot gestures, and exact-retry reconciliation.
- One preparation admits at most one successor run. Multiple preparations for
  one source are independent and bind distinct capture occurrences.
- Start revalidates source terminal state, source payload, preparation bytes,
  saved revisions, new snapshot/semantic input, scope, input package, manifest,
  operation bytes, deadline, and fencing epoch.
- Run, command, operation, preparation submission, and initiation lineage are
  committed atomically. Nothing is scheduled on partial publication.
- Explicit preparation cancellation prevents new stages. An in-flight
  noninterruptible observation may finish but cannot continue the workflow.
- On coordinator restart, queued steps may resume under the same fence. A
  capture running at process loss fails and requires a new preparation because
  later filesystem observation cannot be assigned the old attempt identity.
- Final managed run pause/resume/cancel/retry follows ADR-0016. Retrying work in
  a terminal run requires a new preparation and run.
- Renderer/shell restart merely re-reads durable state. Transport cancellation
  does not cancel durable work.
- Replay uses retained target inputs. It never recaptures live setup and never
  represents a new external verification.

## Failure-closed table

| Condition | Result | Durable mutation allowed |
|---|---|---|
| Source run nonterminal or occurrence absent/mismatched | `InvalidArgument` or `NotFound` | Exact rejected audit only if current policy requires; no preparation/run |
| Source payload/hash or closure substituted | `Conflict`/identity drift | No start |
| Profile/config/context revision stale | typed revision conflict with current safe state | No new stage/run |
| Analyzer/identity/producer incompatible | non-startable `Unsupported` | Preparation evidence may be retained; no run |
| Missing/unknown/ambiguous dependency mapping | non-startable mapping gap | Preparation evidence may be retained; no run |
| Closure exceeds bound | non-startable `LimitExceeded` | No fallback run |
| New snapshot/semantic prerequisite absent or failed | failed preparation | No successor run |
| Valid execution later encounters unsupported content | explicit gap and limited/completed-with-gaps | Normal run output only |
| Exact command retry | `AlreadyAccepted` with original receipt | No duplicate mutation |
| Same key/gesture/ID with changed meaning | `Conflict` | No mutation |

## Persistence and migration

Required append-only logical populations are:

- targeted-verification preparations and canonical request payloads;
- preparation events/attempts and current rebuildable projection;
- snapshot/semantic prerequisite links;
- direct roots, expanded scope members, dependency proof edges, and scope
  payload/fingerprint;
- proposed reuse/recompute decisions and validity proofs;
- immutable preparation plans and resolved manifests;
- start submissions/admissions and managed operation bindings;
- source/successor initiation links; and
- bounded result/coverage/gap projection links.

Foreign keys must bind existing runs, occurrences, snapshots, payloads,
contexts, configurations, manifests, operations, commands, and lineage where
those identities already exist. All authority-bearing rows are append-only;
mutable projections are rebuildable.

The migration must preflight `targeted_verifications` and any run operation
whose kind is `targeted-verification`. Both counts must be zero. Nonzero state
cannot be upgraded honestly and must stop with typed incompatible-storage
status and no schema mutation. The accepted implementation record must include
zero/nonzero, backup/restore, interrupted migration, downgrade/refusal,
projection rebuild, and tampered-payload evidence.

## Security and future renderer mapping

Until the corrected vertical is producer-consumer-validated,
`StartTargetedVerification` remains `native-only-never-map` and Unsupported.
Afterward, Phase D may propose exact renderer messages for begin/read/cancel/
start/readback. Every message names opaque product identities and bounded
choices only. The WPF host maps each message to one exact generated application
client method and returns one closed projection.

Never renderer-reachable:

- raw `SubmitSnapshotCapture` paths or qualified path mappings;
- generic dependency/fact/graph queries used to construct scope;
- internal `bethesda-semantic-v1` or `managed-analysis-v1` operation kind,
  request JSON, scheduler, worker, or retry primitives;
- arbitrary path, SQL, command, URL, provider, credential, or gRPC authority;
  and
- any operation that changes MO2, mods, profile, game, or generated output.

Hostile content cannot populate IDs or gestures outside the exact renderer
schema and live user-interaction rules. All returned text remains inert.

## Exact later implementation inventory

This is an anticipated inventory, not authorization to edit these paths now.
Implementation review must reconcile actual names and generated outputs before
work begins.

### Producers and contracts

- `contracts/protobuf/infinium/application/v1/application.proto`: clean-break
  preparation/start/readback/cancellation messages and RPCs.
- `src/Infinium.Domain/Contracts/`: functional targeted scope, preparation,
  reuse, coverage/gap, admission, and initiation-lineage contracts plus
  invariants and canonical fingerprints.
- `src/Infinium.Application/Analysis/`: dependency-closure planner and targeted
  delivered-input producer; no UI-projection filtering.
- `src/Infinium.Coordinator/`: application handlers, recursive validators,
  saved-setup snapshot resolver, preparation executor/recovery, targeted
  operation resolver, and composition with `ManagedRunExecutor`.

### Consumers and execution

- generated C# protobuf/application clients and their deterministic generation
  inputs;
- Phase C native diagnostic consumer for begin/status/cancel/start/readback;
- `ManagedRunExecutor` and `ManagedAnalysisOrchestrator` only where needed to
  accept/validate the supplied targeted input and prior reconciliation context;
- existing snapshot and Bethesda semantic executors through typed adapters,
  without renderer exposure; and
- future Phase D renderer registry/TypeScript generation only after Checkpoint
  C, under a separately active WP7/WP8 candidate.

### Persistence, migration, and readback

- `src/Infinium.Persistence/AuthoritativeStore.*`: append-only preparation,
  scope, admission, linkage, readback, and atomic start methods;
- functional schema migration files and expected-object inventory;
- content-addressed payload admission for canonical requests/plans/scope/reuse
  proofs and operation hashes;
- projection rebuild, backup/restore, restart/recovery, replay, deletion-impact,
  and tamper validation; and
- explicit empty-population migration gate for the dormant old shape/operation.

### Tests and evaluation

- domain invariant/canonicalization tests;
- dependency-closure positive, permutation, monotonicity, cycle, missing-edge,
  unknown-edge, excessive-scope, and adversarial-omission tests;
- source finding and case tests, including all case members/shared cause;
- new/equivalent/stale/changed snapshot tests and proof that source snapshot is
  never the target;
- managed request golden/round-trip tests proving exact operation kind and
  target bindings;
- atomicity, idempotency, conflicting replay, gesture reuse, concurrency,
  cancellation, restart, crash-window, fence, terminal-run, and replay tests;
- zero/nonzero/tampered/interrupted migration and backup/restore tests;
- coverage/gap/no-readiness and `not observed` guard tests;
- ADR-0022 exact/revision/related/ambiguous/distinct lineage tests;
- renderer hostile/oversized/unknown-field and no-generic-authority tests; and
- focused EVAL cases below plus documentation/schema/naming validation.

### Documentation and generated ownership

- current state, foundation README, capability matrix, contract inventory,
  implementation record, evaluation case catalog, and this addendum;
- protobuf schema fingerprint, application/domain/storage/renderer independent
  version axes, and compatibility matrix;
- generated outputs committed or reproduced exactly according to their current
  owner; and
- no functional implementation name derived from `Phase C`, `WP6`, the
  addendum, or a temporary correction campaign.

## Acceptance criteria

The corrected WP6 targeted-verification vertical is review-ready only when all
of the following are true:

1. current fail-closed behavior remains until accepted contracts, persistence,
   producer, executor mapping, readback, and diagnostic consumer are coherent;
2. every start uses a new snapshot capture occurrence and never the source
   snapshot as target proof;
3. source run and exactly one canonical occurrence are validated from retained
   authority, not UI projection fields;
4. dependency closure is deterministic, complete, bounded, explainable, and
   cannot be narrowed by a caller;
5. final execution is exactly `managed-analysis-v1` with the new binding and a
   valid supplied targeted delivered input;
6. reuse/recompute choices meet the matrix above and every reuse has a retained
   proof edge;
7. source, successor, initiation, and analytical lineage are exact and
   immutable;
8. coverage/gaps use the prepared denominator and readiness remains
   scope-limited/no-readiness;
9. atomic admission, exact retry, concurrency, cancellation, restart, recovery,
   replay, and migration behavior pass focused evidence;
10. unsupported, stale, ambiguous, incomplete, substituted, unknown, and
    excessive mappings fail closed without fallback or partial start;
11. future renderer mapping remains closed, typed, bounded, inert, and free of
    generic backend/local authority;
12. producers, persistence, generated consumers, round-trip, invalid states,
    and focused fixtures reach `Producer-consumer-validated` together;
13. consolidated semantic/security/provenance/diff review has no must-fix
    finding; and
14. the accepted focused and complete verification floors pass on the same
    final candidate with zero repository-owned .NET/test-host survivors.

Passing these criteria would permit Checkpoint C review. It would not itself
accept WP5/WP6, accept Checkpoint C, begin Phase D, or activate M2.

## Evaluation obligations and proposed case changes

- **EVAL-0093:** expand the targeted-verification clause to require fresh
  capture, canonical source hydration, dependency-closed preparation,
  inspect-before-start, atomic `managed-analysis-v1` admission, exact lineage,
  fail-closed mapping, lifecycle/restart/replay, and zero generic renderer
  authority.
- **EVAL-0019:** changed context/config invalidates the preparation; unchanged
  artifacts reuse only through exact dependency proof.
- **EVAL-0020:** verification does not mutate source review state or analyzer
  output.
- **EVAL-0027:** targeted coverage has its own denominator and cannot replace or
  borrow whole-profile readiness.
- **EVAL-0040/0041:** targeted lineage and retained plan artifacts remain
  immutable and deletion-impact reporting stays honest.
- **EVAL-0043:** assumption/context successors invalidate stale preparations;
  no history is rewritten.
- **EVAL-0047/0048/0069:** suppression, advisory, disposition, and readiness
  behavior remain separate from verification execution and reconciliation.
- **EVAL-0078:** prove affected dependency expansion, unrelated omission only
  with proof, full semantic recapture, and no source-snapshot reuse.
- **EVAL-0079:** prove initiation links plus ADR-0022 exact/revision/related/
  ambiguous/distinct result reconciliation and guarded `not observed`.

No private fixture or independent semantic oracle is authorized. Fixtures are
developer-owned product-conformance evidence under the accepted verification
profile.

## Proposed capability-matrix and contract-inventory changes

While this proposal is pending, the matrix and inventory must continue to say
`missing-application-vertical`, `declared-unimplemented`,
`native-only-never-map`, and typed `Unsupported`. They may link this proposal
as the candidate architecture.

After acceptance but before implementation, add proposed contract-family rows
for targeted preparation/scope/input/admission/readback without marking an RPC
implemented. Only producer-consumer validation may change the capability state,
implemented RPC count, renderer policy, or Checkpoint C status. Independent
application/domain/storage/renderer versions and the protobuf fingerprint must
advance from actual generated bytes, never from this proposal alone.

## Review checklist

- product meaning: manual, post-change, finding/case-linked recheck;
- architecture: prepare/inspect/start and executable operation coherence;
- immutability: source/result/review/readiness not rewritten;
- provenance: new capture, semantic input, target binding, operation hash;
- scope: canonical roots, dependency closure, coverage denominator;
- security: closed app/renderer messages and inert content;
- traceability: matrix, inventory, requirements, WP6, Checkpoint C, EVAL;
- lifecycle: admission, retry, concurrency, cancel, restart, replay, migration;
- naming: functional implementation identities only; and
- status: Proposed only, no completion/unblocking/activation claim.

## Resolved decisions and owner action

Existing accepted authority resolves the design choices in this addendum:

- new snapshot occurrence rather than source-snapshot reuse;
- canonical occurrence identity rather than display scope;
- dependency-complete expansion rather than ad-hoc filtering;
- inspectable preparation before manual start;
- `managed-analysis-v1` rather than a new or generic operation;
- proof-bound reuse and snapshot-dependent recomputation;
- append-only initiation plus ADR-0022 analytical lineage;
- scope-limited/no-readiness semantics;
- ADR-0016 lifecycle and atomic admission; and
- closed renderer operations with coordinator-resolved local authority.

No additional product-meaning decision is unresolved. The remaining owner
action is formal acceptance, rejection, or requested revision of Proposed
ADR-0038 and this addendum. Acceptance authorizes later implementation only;
it does not accept any implementation result.

## Exact next step

Architecture steward review, followed by project-owner disposition of
Proposed ADR-0038 and this addendum. If accepted, start a new corrected WP6
implementation candidate from the then-current reviewed repository, preserve
Unsupported until the full vertical is coherent, and stop again for corrected
Checkpoint C review. Do not begin Phase D automatically from this proposal.
