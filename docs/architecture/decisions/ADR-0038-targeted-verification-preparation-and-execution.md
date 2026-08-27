# ADR-0038: Targeted-verification preparation and execution

Status: Proposed
Date: 2026-08-26
Last reviewed: 2026-08-26
Supersedes: None
Superseded by: None
Supplements: ADR-0037

## Plain-language proposal

After a user changes their mod setup, Infinium will first take a fresh,
read-only picture of that setup and prepare an exact list of the checks needed
for the selected finding or case. The list includes required dependencies even
when they were not directly selected. The user can inspect that plan before
starting a new analysis result.

The successor is an ordinary `managed-analysis-v1` run. Infinium will not
reuse the old snapshot as proof of change, filter facts ad hoc, fall back to a
generic full run, or invent a new analysis operation. The old and new results
remain immutable and gain exact append-only lineage.

This ADR is a proposal. It grants no implementation or runtime authority until
accepted and implemented.

## Context

ADR-0037 accepts a closed application contract and says targeted verification
creates separately initiated work linked to exact prior subjects and declared
scope. Phase C implementation found an architectural gap: the declared RPC
cannot currently produce a new installation snapshot, derive complete scope,
or create a supported durable operation. Its typed `Unsupported` behavior is
the current safe boundary.

[RESEARCH-0058](../../research/investigations/RESEARCH-0058-targeted-verification-executable-architecture.md)
shows that the repository already has the required executable endpoints: a
durable snapshot capture, `bethesda-semantic-v1` extraction, and
`managed-analysis-v1`. The missing authority is a typed preparation and
dependency-closed input contract connecting them.

## Decision drivers

- A post-change result must observe a newly captured installation state.
- Required dependency work cannot be omitted by a caller or UI projection.
- Proposed work, reuse, gaps, and limits must be inspectable before start.
- The successor must use an operation that the current executor can run.
- Source and successor results, dispositions, and readiness remain immutable.
- Lifecycle and idempotency must survive process and renderer restarts.
- React receives closed product operations, never paths or backend authority.

## Proposed decision

1. Targeted verification shall use a prepare, inspect, then start workflow.
   The application surface shall provide closed operations equivalent to
   `BeginTargetedVerificationPreparation`,
   `GetTargetedVerificationPreparation`, explicit preparation cancellation,
   and `StartTargetedVerification`. Names may receive normal contract review,
   but their responsibilities shall not be collapsed into a generic operation.
2. A preparation shall name one terminal source run and exactly one canonical
   source finding occurrence or source case occurrence. It shall also bind
   expected confirmed-profile, saved-configuration, analysis-context, and
   applicable setup revisions. Caller-provided paths, operation kinds,
   arbitrary dependency/fact IDs, and generic filters are forbidden.
3. Beginning preparation shall atomically retain its canonical request and
   create a renderer-safe snapshot-capture admission. The coordinator shall
   resolve the already confirmed profile to internal paths under existing MO2
   authority. The captured snapshot must be a new occurrence produced after
   this preparation begins. It may be structurally equivalent to the source;
   the comparison state shall be reported.
4. Preparation shall perform qualified Bethesda semantic extraction against
   the new snapshot before it derives final target scope. The initial
   architecture authorizes full extraction of the supported semantic surface,
   not an unqualified partial extractor.
5. The coordinator shall rehydrate the canonical source occurrence and payload.
   A finding root includes its candidate, hypothesis, identity envelope,
   dependency closure, evidence, affected locus, and participant roles. A case
   root additionally includes every member finding/candidate/hypothesis,
   shared-cause identity, proof evidence, and member closure. Display
   projection subject IDs are never execution authority.
6. A versioned `TargetedAnalysisScope` contract shall expand those roots through
   allowlisted typed dependency edges to a deterministic fixed point. Required
   partners, contributors, providers, files, analyzer declarations,
   applicability populations, context/config dependencies, and shared-cause
   members may not be removed. The contract shall retain direct roots,
   expanded members, edge/proof identities, policy/version, limits, gaps, and
   one canonical fingerprint.
7. A versioned targeted delivered-input producer shall project the new
   semantic input and closed scope into a valid `CandidateDeliveredInput` with
   matching population, dependency, coverage, failure, and gap semantics. It
   shall be deterministic, permutation-independent, monotonic with added roots,
   closed over required dependencies, and bounded. It is not a post-hoc fact
   filter.
8. Preparation shall publish an immutable plan identifying source
   run/occurrence/logical identity/payload, source and target bindings, new
   snapshot/capture/semantic input, direct and expanded scope, target analyzers,
   proposed reuse and proofs, recomputation, coverage denominator, gaps,
   readiness boundary, resolved manifest, and preparation revision/fingerprint.
9. `StartTargetedVerification` shall accept only the exact startable
   preparation/revision plus durable command ID, optional requested successor
   run ID, initiation kind, future deadline, and a fresh one-shot gesture. It
   shall not accept executable scope fields.
10. Start admission shall revalidate every retained byte, hash, identity, and
    saved revision. In one coordinator transaction it shall create the new run,
    durable command, `managed-analysis-v1` operation/request, preparation
    submission, initiation-lineage record, and source/successor link. Scheduling
    occurs only after commit. One preparation may create at most one successor.
11. The managed request shall bind the new snapshot and semantic input, current
    selected context and effective configuration, resolved input manifest,
    supplied targeted delivered input, source run as prior reconciliation
    context, and every reuse proof. No run operation kind named
    `targeted-verification` is authorized.
12. Snapshot-dependent semantic input, candidates, findings, cases, and
    checkpoints shall be recomputed. Profile-independent artifacts may be
    reused only through an ADR-0010-valid dependency-equivalence proof and a
    new reuse/application edge. Unknown impact means recompute, explicit gap,
    or non-startable preparation; generic `reuse anyway` is forbidden.
13. Initiation lineage and analytical lineage are distinct. Initiation lineage
    always records the source occurrence and successor run. Successor
    finding/case continuity continues to use ADR-0022 reconciliation. A missing
    successor occurrence is `not observed` only with complete applicable
    targeted coverage; otherwise it is unknown or a retained gap. Verification
    shall not alter source review disposition.
14. Targeted coverage shall use the prepared closed scope as its denominator
    and report attempted, completed, failed, unsupported, reused, and omitted
    populations. WP6 results shall expose `scope-limited` or `no-readiness` and
    shall not replace or borrow whole-profile readiness. Any future promotion
    requires a separate accepted full-policy readiness evaluation.
15. Exact preparation/command retries shall return the prior receipt. A reused
    key, gesture, preparation, or requested run ID with different meaning shall
    conflict. Independent preparations for one source may run concurrently,
    but shall have distinct captures and immutable state.
16. Transport cancellation shall not cancel durable work. Explicit
    preparation cancellation shall prevent new stages; retained work already
    completed remains auditable. A running capture lost to process failure
    shall fail under a fence and require a new preparation rather than observe
    a later filesystem state under the old identity. Semantic/final run
    restart, retry, pause, and cancellation follow ADR-0016. Terminal runs are
    never reopened.
17. Replay of the successor shall use its retained inputs and shall not capture
    the live installation. A new external recheck always requires a new
    preparation, new snapshot occurrence, and new successor run.
18. Unsupported/stale/ambiguous/incomplete source mapping, missing or unknown
    dependency evidence, incompatible analyzer/identity contracts, substituted
    bytes, exceeded bounds, or unavailable prerequisites shall produce a typed
    non-startable preparation without a run or readiness mutation. Ordinary
    gaps discovered only while executing a valid scope remain explicit and
    yield limited/completed-with-gaps results.
19. The dormant current `targeted_verifications` storage shape and
    `targeted-verification` operation kind shall not be reinterpreted. The
    migration shall require both populations to be empty. Unexpected rows stop
    migration with typed incompatible-storage status pending a separately
    reviewed preservation plan; no binding or lineage may be manufactured.
20. A future renderer bridge may map only the closed preparation/read/start/
    cancel projections after producer-consumer validation. It shall expose no
    paths, raw snapshot command, dependency graph query, operation kind/request,
    SQL, command, credential, URL, or generic gRPC method. Until that gate, the
    existing `native-only-never-map` and typed `Unsupported` policy remains.

## Considered alternatives

### Reuse the old snapshot

Rejected because it cannot observe the external change.

### Caller-selected subject or fact filtering

Rejected because presentation subjects are not a dependency-complete analysis
population and would make omission invisible.

### Automatic full-run fallback

Rejected because it changes authorized work, cost, and readiness meaning.

### A new targeted analysis operation kind

Rejected because the current executor already has the required managed
operation; the missing boundary is preparation and typed input construction.

### One-step start without an inspectable plan

Rejected because the user could not review expansion, reuse, gaps, and limits
before authorizing the run.

## Consequences

### Positive

- The post-change result is bound to a real new observation.
- Scope is dependency-complete and explainable.
- Existing analysis execution, recovery, and publication remain authoritative.
- Historical results and review state remain immutable.
- The renderer gains useful workflow operations without backend primitives.

### Negative

- Preparation becomes an asynchronous durable workflow with its own readback,
  cancellation, fencing, and persistence.
- Initial preparation may extract the full supported semantic surface before
  running only the targeted analytical population.
- A strict populated-storage migration stop is required because the dormant
  shape lacks facts needed for a truthful automatic upgrade.

## Validation obligations

No evaluation passes by accepting this ADR. Implementation must satisfy the
Proposed WP6 addendum and at least EVAL-0019, EVAL-0020, EVAL-0027, EVAL-0040,
EVAL-0041, EVAL-0043, EVAL-0047, EVAL-0048, EVAL-0069, EVAL-0078, EVAL-0079,
and EVAL-0093, including positive, negative, malformed, concurrency, restart,
cancellation, replay, migration, closure, and renderer-hostile cases.

## Acceptance and activation boundary

Only the project owner may accept this ADR. Acceptance would authorize the
corrected WP6 implementation scope defined by the addendum; it would not mark
WP5, WP6, or Checkpoint C complete, begin Phase D, activate M2, authorize a
private evaluator, or change current fail-closed behavior by itself.

## References

- [RESEARCH-0058](../../research/investigations/RESEARCH-0058-targeted-verification-executable-architecture.md)
- [ADR-0002](ADR-0002-snapshot-context-binding.md)
- [ADR-0010](ADR-0010-snapshot-fingerprint-and-dependency-invalidation.md)
- [ADR-0015](ADR-0015-authoritative-evidence-persistence-and-payload-storage.md)
- [ADR-0016](ADR-0016-application-owned-durable-run-and-job-lifecycle.md)
- [ADR-0019](ADR-0019-local-ipc-and-application-query-contract.md)
- [ADR-0021](ADR-0021-desktop-and-local-operation-security-boundary.md)
- [ADR-0022](ADR-0022-finding-and-case-continuity-and-reconciliation.md)
- [ADR-0037](ADR-0037-frontend-application-contract-and-desktop-bridge.md)
- [Proposed WP6 addendum](../../plans/transitions/m1-to-m2/frontend-application-foundation/wp6-targeted-verification-addendum.md)
