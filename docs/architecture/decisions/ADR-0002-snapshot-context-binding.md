# ADR-0002: Installation-snapshot and analysis-context binding

Status: Accepted  
Date: 2026-07-24  
Accepted: 2026-07-25  
Last reviewed: 2026-07-25  
Supersedes: None  
Superseded by: None

## Context

MO2 profiles and mod files may change between or during scans. Findings and
runtime logs become misleading if they combine different states.

## Decision drivers

- Results must identify the exact physical and semantic state they describe.
- Mid-run edits must not blend incompatible inputs.
- Reuse must be efficient without rebinding or falsifying provenance.
- External documentation evidence must remain reusable without becoming
  falsely profile-owned.
- Historical audit, replayability, review state, and current readiness must
  remain distinguishable.

## Considered options

1. **Analyze mutable live state and keep only the latest report:** rejected
   because it cannot explain historical findings or prevent mixed-state runs.
2. **Use one broad snapshot containing physical state, assumptions, execution
   settings, evidence, and review state:** rejected because unrelated changes
   would cause false invalidation and mutable review state would rewrite
   analytical identity.
3. **Separate immutable snapshots, semantic contexts, run configurations,
   resolved inputs, acquisition ownership, and readiness/review projections:**
   selected.

## Decision

Every analysis run, directly analysis-run-derived artifact, and finding is
bound to one logically immutable installation snapshot and one versioned
semantic analysis context. Every analysis run separately retains one effective
scan configuration, and every directly derived artifact identifies its
originating analysis run and resolved input manifest. Runtime logs/test results
bind to installation-snapshot and test-session provenance, then record a
run/context application link when used. Reusable source evidence likewise
retains its independent source/entity/version applicability and records a
run-application link when used. Physical profile state, semantic analysis
context, and operational scan configuration remain distinct.

Documentation acquisition/extraction is represented by a separate immutable
evidence-acquisition run bound to its source/entity/version request,
acquisition configuration, and resolved source inputs. Reusable external
evidence may remain profile-independent; local or in-archive source inputs
additionally bind to their supplying installation snapshot. Acquisition alone
does not create profile findings or readiness.

Changed physical inputs create a new installation snapshot. Changing
assumptions or another semantic interpretation policy creates a new context.
Changing budgets, cache-execution/tracing policy, source/analyzer selection, or
concurrency creates a new scan configuration/run without automatically
invalidating semantically unaffected artifacts. Dependency-aware reuse is
allowed only when validity is demonstrated, and it records a reuse edge rather
than rebinding artifact origin.

Retained historical reports remain available but become visibly stale relative
to current state/context when applicable.

Live-source revisions and nondeterministic model reruns create new run evidence
even when snapshot and configured context are unchanged. Reproduction uses the
retained resolved input manifest and, for exact downstream replay, retained run
outputs rather than silently refetching live inputs or re-calling models.
Replayability is reported as complete, partial, or unavailable based on
retained dependencies; loss of replayability does not corrupt whatever audit
history remains under the active retention policy.

Readiness is a separate evaluation bound to an analysis run, a versioned
readiness policy, resolved applicable disposition set (including
unreviewed/default state), and an evaluation time.
Changing review state or readiness policy creates a new evaluation without
mutating the run, its semantic context, or prior readiness evaluations.

Run-specific findings and case revisions are immutable analytical outputs.
Cross-run continuity uses explicit logical finding/case identity and
revision/supersession lineage only when causal, applicability, and dependency
equivalence supports it. Dispositions, suppression, and review annotations
remain separate review state and carry forward only through validated
applicability rather than becoming fields that rewrite analyzer output.

## Consequences

### Positive

- Reports are auditable, with replayability stated honestly.
- Logs and findings cannot silently drift across profiles.
- Incremental work has an explicit correctness boundary.
- Change-impact analysis becomes possible.

### Negative

- Snapshot/context capture and dependency modeling are substantial work.
- Mid-scan changes require invalidation and resume behavior.
- Storage retains historical state and metadata.

## Requirements affected

- SNAP-001 through SNAP-006
- SCAN-005 through SCAN-007
- SCAN-009 and SCAN-010
- DOC-002, DOC-009, and DOC-011
- EVID-002
- FIND-005, FIND-006, FIND-010, FIND-012, and FIND-014
- OPS-002

## Validation

- Clean and incremental scans must be semantically equivalent under equivalent
  resolved inputs.
- Irrelevant physical or contextual changes must not invalidate unrelated
  artifacts.
- Operational scan-setting changes must not falsely create a semantic context.
- Mid-run edits must create new versions without mutating the active run's
  bindings.
- Relevant changes must always reopen or recompute dependent results.
- Clean analytical recomputation and live source refresh must remain separately
  configurable and distinguishable in provenance.
- Readiness-policy or disposition changes must create new readiness evaluations
  without changing analytical run/context identity.
- Reanalysis must create linked finding/case revisions rather than mutate prior
  analytical output, and review state must carry only when applicability is
  validated.

## References

- [Product requirements](../../product/requirements.md)
- [Domain model](../../product/domain-model.md)
- [Jobs, caching, and snapshots](../jobs-caching-and-snapshots.md)
