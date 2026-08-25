# ADR-0033: Retire and archive protocol `/4`

Status: Accepted

Last reviewed: 2026-08-10

Date: 2026-08-10

Accepted by: Project owner

Supersedes: ADR-0032 decision 5, its accepted freeze-boundary clarification,
and ADR-0027 only where either retains a runnable public protocol `/4`
evaluator or requires its bounded regression

## Context

ADR-0032 deferred private held-out evaluation but retained the frozen protocol
`/4` implementation as a narrowly bounded public regression check. That check
was outside the default solution and did not execute current product inputs,
compare product output with expected truth, exercise held-out material, or
establish any current semantic, readiness, reliability, M1, or product verdict.
It ran only when changes touched three allowlisted public test seams.

The retained evaluator nevertheless required a product friend-assembly seam,
a duplicate frozen validator, an out-of-solution project, wrappers, profiles,
hash rebaselines, authority exceptions, and extensive current navigation. Its
narrow historical health signal no longer justifies that active repository and
review surface while evaluator work is deferred.

## Decision

1. Retire protocol `/4` completely from active Infinium development and review.
   It has no runnable entry point, current test requirement, review gate,
   product authority, semantic authority, or verdict.
2. Preserve the exact last public `/4` closure in the separate maintainer-local
   Git repository `../infinium-evaluator-archive/`, initial archive commit
   `c490de9689d8e9f8dfc7eccb3d056ab5b083e9fd`. The archive is non-authoritative,
   excluded from ordinary product context, and contains no private fixtures or
   verdicts.
3. Remove the evaluator project and schemas, embedded validator, evaluator-only
   test and project, bounded wrapper and refusal suite, machine profile, freeze
   record, authority matrix, and path-retention redirects from the active tree.
   Remove the evaluator friend-assembly grant and all default-solution
   exclusions that existed solely for the archived project.
4. Keep the two ordinary Bethesda public evaluation tests that the final `/4`
   profile happened to identify. They remain current product tests under the
   public continuation verification profile and no longer carry evaluator
   provenance or authorization.
5. Continue M1 Slices 5-9 with the accepted layered public contract, fixture,
   mutation, replay, safety, integration, and fresh-review evidence in the M1
   continuation verification profile. No replacement evaluator or weakened
   verification is authorized.
6. Reserve all protocol `/2`, `/3`, `/4`, and `/5` identities permanently.
   Archived code and identities may not be resumed, reused, or treated as a
   starting implementation. Future evaluator reconsideration remains subject
   to ADR-0032's post-Slice-9/M3 preconditions and requires a new ADR, plan,
   identity, qualification, and claim boundary.
7. Ordinary agents and product work must not inspect or execute the archive.
   Archaeological access requires an explicit project-owner request.

## Consequences

- The active Infinium repository has no evaluator implementation, protocol
  schema, evaluator-only test project, scoring path, or bounded regression
  command.
- Product review no longer runs `/4`, conditionally or otherwise. Current
  review evidence comes only from the continuation verification profile and
  owning slice plans.
- Historical evaluator chronology remains summarized in `evaluator-history.md`,
  with exact retired Git objects recorded in the retirement inventory.
- The missing private held-out verdict remains an explicit residual risk.
  Archiving `/4` does not create or remove such a verdict.
- Reintroducing evaluator code is a new architecture and planning decision, not
  an archive restore or maintenance task.

## Requirements affected

`EVID-001` through `EVID-007`, `COVER-001`, `COVER-002`, `ANALYSIS-003`,
`ANALYSIS-005`, `ANALYSIS-006`, `ANALYSIS-016`, `ANALYSIS-019`, `EVAL-0052`,
and `EVAL-0086`.

## References

- [ADR-0027](ADR-0027-public-evaluation-protocol-private-held-out-corpus.md)
- [ADR-0032](ADR-0032-defer-m1-held-out-evaluator-and-continue-public-verification.md)
- [Evaluator history](../../evaluation/evaluator-history.md)
- [Product-conformance verification profile](../../evaluation/product-conformance-verification-profile.md)
