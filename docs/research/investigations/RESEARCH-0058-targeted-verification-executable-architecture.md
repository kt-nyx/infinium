# RESEARCH-0058: Executable targeted-verification architecture

Status: Completed
Disposition: Recommendation proposed; architecture acceptance pending

Date: 2026-08-26
Last reviewed: 2026-08-26
Researcher: Codex
Research question: RQ-042

## Plain-language result

Infinium can already capture a new installation snapshot and can already run
the durable `managed-analysis-v1` analysis operation. The missing piece is a
safe, exact bridge between those capabilities. The current
`StartTargetedVerification` request names an old finding or case, but it does
not prove that a new post-change snapshot was captured, expand the selected
issue to all of its required dependencies, or construct an executable managed
analysis request. Its fail-closed `Unsupported` behavior is therefore correct.

The recommended architecture is a **prepare, inspect, then start** workflow.
Preparation performs a fresh read-only snapshot capture, derives current
semantic input, rehydrates the canonical source finding or case, and builds a
dependency-complete, immutable targeted-analysis plan. The user can inspect
that plan before a second, one-shot start gesture atomically creates a new
`managed-analysis-v1` run. The source and successor remain immutable and are
connected by append-only request, scope, reuse, and result-lineage records.

This uses no generic execution fallback and introduces no new analysis
operation kind. It does require new typed application contracts, a
coordinator-owned preparation state machine, a dependency-closed targeted
input producer, and new append-only persistence before the currently declared
start RPC can become executable.

## Question

RQ-042 asks:

> How can Infinium capture the changed installation, expand an exact source
> finding or case to dependency-complete work, and execute a separately
> initiated successor through `managed-analysis-v1` while retaining exact
> lineage, honest coverage, and a closed renderer boundary?

The governing behavior is SCOPE-004, SNAP-002 through SNAP-004, FIND-005
through FIND-014, UX-005, INTENT-001 through INTENT-005, OPS-002/003, and the
targeted-verification workflow in `docs/product/workflows.md`.

## Scope and method

This investigation examined only current, authoritative repository material.
It did not inspect an archive or private evaluator material and did not run or
change production surfaces.

Inspected implementation seams included:

- `ApplicationService.StartTargetedVerification`, its recursive validator,
  and the dormant persistence method;
- snapshot-capture submission, execution, publication, restart fencing, and
  readback;
- prepared manual-run admission and `PreparedAnalysisOperationResolver`;
- durable run/operation/gesture creation in `AuthoritativeStore.CreateRun`;
- `managed-analysis-v1`, its request validation and checkpoint invalidation;
- `bethesda-semantic-v1` extraction from one exact installation snapshot;
- retained delivered inputs, `CandidateDeliveredInputAdapter`, candidate
  population/expansion, and `analysis_dependency_edges`;
- finding/case occurrence identities, identity envelopes, dependency closure,
  reconciliation, lineage events, result projections, and readback; and
- targeted-verification schema 14/15 persistence.

## Deterministic observations

1. The active application handler validates the declared request and returns
   typed `Unsupported` without durable mutation.
2. The dormant persistence path would create a run using operation kind
   `targeted-verification`, bind the source run's old snapshot, and insert its
   linkage after run creation. That operation kind is not executable, the old
   snapshot does not observe an external change, and the split publication is
   not an acceptable admission boundary.
3. Snapshot capture is already a durable, read-only, coordinator-owned
   operation. A capture has a distinct occurrence identity and a structural
   fingerprint; even structurally equivalent post-change state can therefore
   be observed by a new capture without pretending the old snapshot is new.
4. Current renderer-safe setup state retains a confirmed profile and revision.
   The raw snapshot RPC accepts paths and is intentionally native/developer
   internal, so a future renderer operation must name saved identities and let
   the coordinator resolve paths.
5. `PrepareManualRun` and `SubmitPreparedRun` already establish the useful
   pattern: immutable preparation, revision revalidation, canonical
   submission fingerprint, one-shot gesture, atomic run/operation/command
   admission, then scheduling after commit.
6. `managed-analysis-v1` is the only current complete analysis operation for
   this path. It accepts an exact snapshot, semantic input, context,
   configuration, resolved manifest, and either a supplied or constructed
   delivered input. It can therefore execute a targeted delivered input
   without a new operation kind.
7. The current delivered-input producer builds a whole available candidate
   population. It has no typed targeted-scope input. Filtering its facts after
   production would discard population and dependency meaning and is not an
   acceptable implementation.
8. A finding occurrence retains its run, candidate, analyzer/identity
   envelope, canonical signature, dependency-closure ID, evidence, and exact
   payload. A case occurrence retains its member findings/candidates,
   shared-cause identity, and proof evidence. The display projection's
   `subject_ids` is useful for presentation but is not executable scope.
9. Retained `analysis_dependency_edges` and identity envelopes provide the
   basis for closure traversal. The current read helper is bounded for display
   and is not itself a complete targeting planner.
10. ADR-0022 already governs analytical occurrence continuity. Targeted-run
    provenance must supplement, not replace, reconciliation of findings and
    cases produced by the successor run.

## Findings

### A new observation must precede the successor run

The external filesystem is mutable, so a verification request cannot bind the
source snapshot or accept a caller assertion that setup changed. Preparation
must create a new snapshot-capture occurrence after the user begins the
workflow and validate it against the still-confirmed profile. Structural
equivalence with the source snapshot is permitted and reported; it does not
permit reuse of the source snapshot occurrence.

Full deterministic Bethesda semantic extraction against the new snapshot is
the safest initial upstream boundary. It avoids pretending that an
unqualified partial extractor can identify every affected record or provider.
Targeting begins at the candidate delivered-input boundary, where populations,
dependencies, gaps, and coverage are already typed.

### Scope is derived authority, not a bag of caller-selected facts

The declaration is exactly one source finding occurrence or one source case
occurrence in one terminal source run. The coordinator rehydrates that
canonical object and derives direct roots:

- a finding contributes its candidate, hypothesis, identity envelope,
  dependency closure, evidence, affected locus, and participant roles; or
- a case contributes its shared-cause identity, every member finding,
  candidate and hypothesis, cause-proof evidence, and every member dependency
  closure.

A versioned closure policy then traverses allowlisted typed edges to a fixed
point. Relationship partners, winning/overridden contributors, providers,
local files, analyzer declarations, applicable populations, context/config
dependencies, and shared-cause members cannot be removed by the caller.
Displayed subject IDs are evidence for explanation only. Unknown edge kinds,
missing roots, inconsistent payloads, incompatible producers, cycles outside
the admitted graph model, or a closure exceeding its bound make preparation
non-startable; they never trigger a full-run fallback.

The prepared plan separately records declared roots and expanded members so
the user can see why extra work is necessary. A deterministic targeted-input
producer then projects the new semantic input into a valid
`CandidateDeliveredInput` whose population is exactly that closed scope and
whose gaps/coverage describe the same denominator. Monotonicity, permutation
independence, and closure completeness are contract invariants.

### Existing execution remains the authority

Starting an accepted preparation constructs the ordinary
`managed-analysis-v1` request with:

- the newly captured snapshot and its newly produced Bethesda semantic input;
- the selected current analysis context and effective configuration;
- a resolved manifest that names the source occurrence/run, preparation,
  closure policy/fingerprint, targeted delivered input, and every reuse proof;
- the source run as prior analytical context for ADR-0022 reconciliation; and
- the supplied dependency-closed targeted delivered input.

No `targeted-verification` run-operation kind is created. The successor run is
a normal analysis run and publishes normal immutable results. The targeting
request is durable initiation provenance around that run, not an alternative
analysis engine.

### Reuse is proof-bound and narrow

The source snapshot, its semantic extraction, candidate decisions, findings,
cases, and snapshot-dependent checkpoints must be recomputed. They cannot
prove the changed installation.

An unchanged selected context/configuration revision may be rebound as a new
run input. Profile-independent documentation or another artifact may be reused
only when its retained dependency closure and exact validity proof remain
equivalent under ADR-0010; the original artifact remains immutable and a new
reuse/application edge is recorded. Unknown impact means recompute, explicit
gap, or non-startable preparation. There is no `reuse anyway` switch.

### Two kinds of lineage are required

Initiation lineage records why the successor run exists even if no successor
finding is observed: source run and exact occurrence, source payload/hash and
logical ID, direct and expanded scope, preparation/snapshot/context/config/
manifest, managed operation request/hash, successor run, and reuse proofs.

Analytical lineage remains ADR-0022 reconciliation between old and new
occurrences. Exact continuation, analytical revision, related follow-up,
not-observed, ambiguous, and distinct outcomes keep their existing proof
requirements. A result may be called `not observed` only when the prepared
scope was executed with applicable complete coverage; otherwise it is unknown
or a gap. Verification never changes the source disposition automatically.

### Coverage and readiness remain deliberately narrow

The result reports declared roots, expanded scope, attempted/completed/
failed/unsupported populations, reuse, gaps, and any scope change between
preparation and admission. The initial targeted workflow always exposes
`scope-limited` or `no-readiness`; it does not replace whole-profile readiness
or borrow source-run coverage. A later, separately accepted readiness
evaluation may recognize full-policy coverage if it can prove it, but WP6 does
not grant that authority.

## Recommended lifecycle

1. `BeginTargetedVerificationPreparation` accepts one source occurrence, exact
   source run, expected confirmed-profile/configuration/context revisions, a
   durable idempotency key, and an explicit user gesture. It accepts no paths,
   dependency IDs, operation kinds, or generic scope filters.
2. The coordinator atomically records the preparation admission and a
   renderer-safe snapshot-capture request resolved from saved setup authority.
3. Durable preparation steps capture and publish a new snapshot, perform full
   Bethesda semantic extraction, derive dependency-complete scope, construct
   the targeted delivered input, and publish an immutable preparation plan.
4. `GetTargetedVerificationPreparation` returns bounded status, direct versus
   expanded work, proposed reuse, gaps, structural comparison, and whether the
   preparation is startable.
5. `StartTargetedVerification` accepts only preparation identity/revision, a
   requested successor run ID, durable command ID, deadline, initiation kind,
   and a new one-shot gesture. It revalidates all saved revisions and retained
   bytes, then atomically creates the successor run, `managed-analysis-v1`
   operation, command, preparation submission, and initiation lineage before
   scheduling.
6. Existing run lifecycle/query/cancel/reconnect operations govern the
   successor. A bounded targeted-verification read model joins preparation,
   admission, run state, coverage/gaps, and later result reconciliation.

Exact retries return the original preparation or command receipt. Reusing an
idempotency key, gesture, preparation, or requested run ID with different
meaning is a conflict. One preparation may start at most one successor.
Concurrent preparations for the same source are permitted only as independent
captures; they never share mutable state.

Transport cancellation has no durable meaning. An explicit typed preparation
cancellation stops new stages; an in-flight uninterruptible capture may finish
and be retained but cannot authorize later stages. A restart resumes queued
steps under fences. A capture that was running at process loss is failed, not
silently replayed against a later filesystem state; the user begins a new
preparation. Semantic and managed-analysis attempts follow ADR-0016 fencing,
retry, pause, and cancellation rules. A terminal run is never reopened.

Deterministic replay reads retained target inputs and does not recapture the
live installation. Rechecking the live installation always creates a new
preparation and snapshot.

## Failure and migration behavior

Unsupported analyzer family, stale source/config/profile/context, ambiguous or
missing occurrence membership, missing dependency closure, incompatible
identity/producer contracts, incomplete closure, substituted payload, unknown
edge, excessive scope, or absent new-snapshot prerequisite returns a typed,
inert, non-startable preparation. No run, command, operation, or readiness
record is created.

A valid plan may later produce ordinary analyzer gaps for malformed,
inaccessible, or unsupported changed-snapshot content. Those gaps are retained
against the prepared denominator and yield completed-with-gaps/no-readiness;
they are not silently omitted.

The current schema contains a dormant `targeted_verifications` shape that
cannot be truthfully upgraded because it lacks a new snapshot, complete scope,
and managed-operation binding. The migration must assert that both that table
and any `targeted-verification` run-operation population are empty before
installing the new schema. A non-empty population stops migration with a typed
incompatible-storage result and requires a separately reviewed preservation
plan; migration must not invent lineage or schedule those rows.

## Alternatives considered

### Reuse the source snapshot and rerun its inputs

Rejected. It rechecks old evidence, not the external setup after the change.

### Filter current delivered facts by the UI's subject IDs

Rejected. Display subjects do not contain complete dependency, population, or
coverage meaning and can silently omit required work.

### Fall back to a full analysis when targeting is uncertain

Rejected. A generic fallback changes user-authorized scope and cost. The
preparation fails closed and may recommend a separately prepared full run.

### Add a `targeted-verification` analysis operation

Rejected. No executor exists, and it would duplicate managed analysis rather
than provide the missing typed input/planning boundary.

### Start immediately and explain the derived scope afterward

Rejected. The product workflow requires proposed work, reuse, gaps, and limits
to be inspectable before manual start.

## Recommendation and decision status

Adopt Proposed ADR-0038 and the Proposed WP6 addendum. Existing accepted
authority resolves the product meaning and the recommended design; no new
product choice remains. The architecture steward and project owner must still
accept or reject the proposal before implementation. Until then,
`StartTargetedVerification` remains typed `Unsupported`, WP5/WP6 and Checkpoint
C remain under correction, Phase D remains blocked, and M2 remains inactive.

## References

- [ADR-0002](../../architecture/decisions/ADR-0002-snapshot-context-binding.md)
- [ADR-0010](../../architecture/decisions/ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
- [ADR-0015](../../architecture/decisions/ADR-0015-authoritative-evidence-persistence-and-payload-storage.md)
- [ADR-0016](../../architecture/decisions/ADR-0016-application-owned-durable-run-and-job-lifecycle.md)
- [ADR-0019](../../architecture/decisions/ADR-0019-local-ipc-and-application-query-contract.md)
- [ADR-0021](../../architecture/decisions/ADR-0021-desktop-and-local-operation-security-boundary.md)
- [ADR-0022](../../architecture/decisions/ADR-0022-finding-and-case-continuity-and-reconciliation.md)
- [ADR-0037](../../architecture/decisions/ADR-0037-frontend-application-contract-and-desktop-bridge.md)
- [Product requirements](../../product/requirements.md)
- [Product workflows](../../product/workflows.md)
- [Candidate input and expansion](../../product/candidate-input-and-expansion.md)
- [Product-conformance verification profile](../../evaluation/product-conformance-verification-profile.md)
