# Jobs, caching, and snapshots

Status: Draft  
Last reviewed: 2026-07-24

This document records required behavior. Storage technology and scheduling
implementation remain undecided.

## Immutable installation snapshots

A scan starts from one logically immutable installation-snapshot manifest, one
versioned semantic analysis context, and one retained effective scan
configuration. The manifest identifies physical/effective profile inputs or a
stable derivation capable of detecting changes. It does not imply that all mod
bytes are copied or retained.

The engine must detect changes that occur during capture or analysis. It may:

- finish work whose declared dependencies remain unchanged;
- invalidate affected stages;
- offer a new installation snapshot and a user-confirmed derived-run path that
  reuses valid checkpoints;
- retain external documentation work that is profile-independent.

It must never silently mix installation states, semantic contexts, or effective
run configurations. A started run retains its bound versions; an edit creates
a new version and waits for a later user-initiated run, which may use explicit
validated reuse edges.

## Resolved input manifest

Every run records the actual inputs it resolved, including external-source
revisions, tool/provider/model identities, prompt/schema versions, and retained
or referenced input evidence and request settings. A configuration expresses
what to resolve; the resolved input manifest records what was actually used.
The run record separately retains tool/model calls, outputs, derived evidence,
and other results.

Deterministic replay mode uses retained resolved inputs. Exact downstream replay
also reuses retained boundary outputs. Clean recomputation creates a new run
but does not itself change resolved source evidence. A new run may legitimately
differ when it explicitly refreshes sources or re-invokes a nondeterministic
model.

Each run declares complete, partial, or unavailable replayability. Auditability
does not depend on replay being possible: missing original mod bytes,
unavailable tool/model versions, or unretained external content are recorded
explicitly. Material gaps in the retained audit trail are reported separately
from replayability.

## Job hierarchy

```text
Analysis run
  Scan stage
    Analyzer run
      Operation / tool call / LLM investigation
  -> optional linked child acquisition run

Evidence acquisition run (directly initiated or linked child)
  Source/entity unit
    Retrieval / extraction / tool call / LLM operation
```

Every node records:

- configuration;
- state;
- dependencies;
- progress;
- timestamps;
- cost/usage;
- outputs;
- failures and retries;
- checkpoint;
- coverage.

An analysis run is snapshot/context/scan-configuration-bound. An independent
evidence acquisition run is instead bound to a source/entity/version request,
acquisition configuration, and resolved source inputs. Local/in-archive
acquisition also records the supplying installation snapshot. Applying reusable
source evidence to local analysis is an explicit edge between these operation
types, not an ownership transfer.

Usage and cost are recorded once at the originating call/operation. Parent
nodes aggregate those ledger entries without duplicating ownership. An analysis
run includes newly incurred cost from attached configured child acquisition in
its attributable rollup, while dependency-valid work reused from an earlier run
shows its original provenance/cost separately and is not counted as new spend.
Detaching a child preserves its initiation/parent provenance and the parent's
pre-detachment attributable spend. Calls started after detachment remain owned
by the acquisition run and appear as separately authorized continuation spend,
not as additional cost of the parent analysis run. Post-detachment progress,
remaining-time estimates, and duration likewise remain with the acquisition
run and do not extend the parent.

Before a billable unit starts, the scheduler reserves its adapter-declared
worst-case usage against every applicable consumptive operation,
acquisition-run, and analysis-run hard limit in one atomic decision. Every unit
also passes any applicable hard-deadline check before dispatch. Completion
reconciles the reservation to the single-owned actual ledger entry. Work whose
adapter cannot provide a finite bound for a selected consumptive hard-limit
dimension is not schedulable under that hard-limit configuration.
Provider-side billing adjustments, uncancellable charges, or uninterruptible
work completing after a deadline are recorded as actual cost/audit/duration
variance and never create additional execution authority. A budget reservation
is not provider authorization: if credentials are revoked or the operation is
cancelled before dispatch, the work remains blocked and its unused reservation
is released.

## Job states

At minimum:

Non-terminal:

- queued;
- running;
- waiting;
- retrying;
- pausing;
- paused;
- cancelling;

Terminal:

- cancelled;
- completed;
- completed with gaps;
- failed;
- limit reached;
- invalidated by changed input.

## Cache key principle

Cache validity may depend on:

- content fingerprints;
- MO2/profile state;
- archive/file provider chain;
- analyzer and ruleset versions;
- external tool version and configuration;
- documentation source revision;
- provider, model, prompt, schema, and reasoning settings;
- source/extraction claim-review revisions;
- analysis-affecting assumption, identity-mapping, and local
  claim-applicability-adjudication set versions;
- upstream evidence.

Modification time alone is insufficient where it cannot prove content identity.

## Carryover

An artifact can be reused by another installation snapshot, analysis context,
or scan configuration only when dependency comparison proves semantic
equivalence for that artifact. The proposed carryover should be inspectable.
Where impact cannot be established confidently, recompute/skip the affected
work or request scoped typed user input whose dependencies can be evaluated.
A generic “reuse anyway” confirmation does not prove equivalence.

Historical analysis artifacts remain bound to their original installation
snapshot, analysis context, and analysis run. Acquisition artifacts remain
bound to their originating acquisition run and source/entity/version inputs,
plus a supplying snapshot for local/in-archive documents. A consuming run
records a reuse/application edge and validation proof rather than rebinding the
original artifact.

## Clean recomputation and source refresh

Derived-output cleanliness and source freshness are separate dimensions:

- **Normal validated reuse** may consume dependency-valid analytical or
  extraction artifacts.
- **Targeted or complete clean recomputation** bypasses reusable derived
  analytical outputs for the declared analysis scope while using an explicitly
  resolved evidence set.
- **Clean extraction** bypasses reusable claim/extraction outputs while using
  the same explicitly resolved source revision/bytes.
- **Source refresh** reacquires selected live external evidence through a new
  acquisition operation. It is not implied by clean recomputation.
- **Explicit combinations** may refresh source bytes, cleanly re-extract them,
  and/or cleanly recompute consuming analysis. Any refresh may legitimately
  resolve different evidence from a prior run.

Development tooling must compare clean and incremental semantic outputs against
equivalent resolved inputs at the layer under test. It must not interpret a
difference caused by source refresh as cache invalidation failure.

## Pause and resume

Checkpoint boundaries should favor:

- idempotent units;
- preserving expensive completed work;
- clean cancellation of paid requests where possible;
- resuming after UI restart;
- preserving valid work at cost-limit exhaustion for a later run.

The exact process-lifetime behavior requires architecture research.

Pause and resume continue the same non-terminal run and retain its immutable
bindings. Cancellation makes the execution terminal. Valid outputs/checkpoints
from a cancelled run may be reused only by a separately user-initiated run with
recorded dependency validation and reuse edges. A retry inside an active run is
a recorded attempt; retrying work from a terminal run creates a separately
user-initiated new run.
Pausing or cancelling a parent stops all new attached-child scheduling by
default and requests pause or cancellation of those child runs. In-flight
operations that cannot be interrupted may finish and remain visible in
progress/cost reporting. Continuing attached child work while the parent is
paused, or detaching a dependency-valid acquisition so it can continue after
parent pause/cancellation, requires an explicit user choice showing remaining
work and cost. Detachment records a lifecycle edge without rewriting the
acquisition run's initiation provenance, bindings, prior calls, or prior cost.

`limit reached` is terminal when continuing would require changing an immutable
configured hard budget, deadline, or scope limit; a user may start a new run
with revised configuration and validated checkpoint reuse. Transient provider
throttling or a scheduled wait that can continue under the same configuration
is recorded as waiting/retry behavior, not as hard-limit exhaustion.

Limit state is scoped to the node whose immutable limit was exhausted. A
terminal child/operation does not terminate unaffected parent work whose own
limits remain available; the parent records the child gap and may complete with
gaps. Parent and child limits consume the same single-owned usage ledger entry
through separate rollups rather than charging the call twice.

Retention/deletion actions that affect active or paused work must preview the
effect on completion, resumability, auditability, replayability, and later
reuse. They must not silently corrupt an operation.

## Progress

Progress should be derived from real work units where possible, not fabricated
percentages. Estimates may use historical timings and token/cost models, with
uncertainty exposed.

Required rollups:

- whole scan;
- stage;
- analyzer;
- individual operation/investigation where useful.

Evidence-acquisition operations provide corresponding operation,
source/entity, and retrieval/extraction rollups.

Progress state and readiness state remain distinct. Partial analysis results
may produce explicitly provisional readiness for their own run, but a new or
resumed job does not mutate a readiness evaluation of another run or inherit
its coverage. A disposition change creates a new readiness evaluation over the
applicable run/review state rather than changing the run or a prior evaluation.
A readiness-policy change likewise creates a new evaluation without changing
semantic analysis context or recomputing analytical output. Acquisition runs
never produce profile readiness by themselves.
