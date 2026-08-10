# ADR-0032: Defer the M1 held-out evaluator and continue with public verification

Status: Accepted
Disposition: partially superseded by ADR-0033
Last reviewed: 2026-08-10

Date: 2026-08-07

Accepted by: Project owner

Supersedes: ADR-0030's active protocol `/5` authorization; ADR-0031 only as
active protocol `/5` model authority; ADR-0027 and M1 plan revision `/3` only
where they require a held-out `PASS` before Slice 5

## Context

Slice 4 public product conformance completed at candidate
`a98d648bd0adb2751ee0c09828e0227b1583950f`. The separately qualified public
evaluator protocol `/4` then proved unable to represent one accepted partial
`RACE/DATA` outcome. The authorized protocol `/5` successor never reached an
implementation or freeze: its WP1V proof gate returned a final independent
`REJECT` after the single correction pass because four resolved-link witnesses
contained 72 noncanonical placeholder properties and the validator compared
ledger-authored values back to the same ledger.

Continuing that evaluator line would spend M1 effort on a representation
surface that is not required to implement the remaining backend-semantic-proof
slices. It would not manufacture the independent private verdict that the
current attempt lacks.

## Decision

1. Close the current M1 held-out evaluator effort without a product verdict.
   No current private `PASS`, `FAIL`, or valid product-scoring
   `EVALUATOR_ERROR` exists.
2. Retire protocol `/5` unqualified. ADR-0030 no longer authorizes an active
   `/5` successor, and ADR-0031 no longer supplies active `/5` model authority.
   The identity `infinium.evaluator-v2/5` and its projection/model identities
   are consumed historical identities and must not be reused.
3. Preserve ADR-0030, ADR-0031, the `/5` plans, and WP1/WP1V records as
   chronological evidence. Their failed proof artifacts may not authorize an
   evaluator, product behavior, or verdict.
4. Preserve ADR-0031's durable loose-availability decision as product
   authority in ADR-0028 and its referenced product contracts: unknown loose
   availability remains unknown; archive evidence is independent; positive
   incomplete loose coverage owns the exact
   `face-gen-loose-assets` / `exhaustive-byte-verified-loose-provider-index`
   gap at snapshot and result scope; mesh and tint obligations are aggregated
   once each; and the exact zero, partial, full, and zero-denominator coverage
   lifecycles apply.
5. Retain frozen protocol `/4` only as a bounded public regression tool for the
   exact states it represents. Its success cannot be called a complete current
   semantic verdict, a private held-out verdict, or M1/Slice 4.5 acceptance.
6. Replace the held-out-`PASS` prerequisite for Slice 5 with the accepted M1
   continuation verification profile defined by the evaluator-deferral plan.
   This narrowly supersedes ADR-0027 and M1 plan revision `/3` only where they
   make that verdict a prerequisite for Slice 5. It does not change the
   historical meaning of a valid held-out verdict.
7. Preserve every private-fixture default-deny, answer-isolation,
   product/evaluator separation, no-retry/no-repair, exact-identity,
   contamination, provenance, and separate-role rule in ADR-0026 and ADR-0027.
   This decision authorizes no private access, candidate execution, corpus
   work, adaptation, scoring, or replacement evaluator.
8. Authorize the public closeout and replacement verification work in
   [the evaluator-deferral plan](../../plans/milestones/m1/slices/s4.5/plan.md).
   Slice 5 becomes eligible only when that plan's closeout is accepted.

## Future evaluator re-entry

A new evaluator may be proposed only after Slice 9 and M3 planning have a
stable, versioned, user-meaningful output contract; expected values are
independently authorable without product output; an answer-free totality and
authorability review passes; implementation, private corpus qualification,
scoring, and closeout retain separate authorities; and a new accepted ADR and
plan select a new identity and exact claim boundary. This decision neither
selects protocol `/6` nor authorizes any successor work.

## Consequences

- Slice 4's exact public conformance evidence remains valid, but it is not a
  private reliability verdict.
- Protocol `/4` remains byte-frozen historical evidence with bounded public
  regression value and its known representation gap.
- Protocol `/5` has no implementation, freeze, private use, or verdict.
- M1 continues through public contract, fixture, mutation, replay, safety,
  controlled-real, and fresh-review gates rather than claiming held-out
  evidence that was not obtained.
- Residual risk explicitly includes the missing private held-out verdict and
  the known `/4` representation limitation.

## Requirements affected

`EVID-001` through `EVID-007`, `COVER-001`, `COVER-002`, `ANALYSIS-003`,
`ANALYSIS-005`, `ANALYSIS-006`, `ANALYSIS-016`, `ANALYSIS-019`, `EVAL-0052`,
and `EVAL-0086`.

## References

- [ADR-0027](ADR-0027-public-evaluation-protocol-private-held-out-corpus.md)
- [ADR-0028](ADR-0028-m1-bethesda-semantic-reporting-and-oracle-authority.md)
- [ADR-0029](ADR-0029-layered-evidence-and-partial-semantic-publication.md)
- [ADR-0030](ADR-0030-protocol-5-successor-qualification.md)
- [ADR-0031](ADR-0031-facegen-loose-availability-gap-and-protocol-5-successor-model.md)
- [WP1V hard stop](../../evaluation/evaluator-history.md)

## Accepted freeze-boundary clarification — 2026-08-07

The initial WP2 preflight correctly stopped when it interpreted every
`required_public_files` entry as required to retain its historical bytes in the
current checkout. Owner clarification now distinguishes three layers without
changing the freeze manifest or evaluator:

1. historical freeze identity is all 23 raw Git blobs at evaluator commit
   `3693d19563c636cd2879804633ca4ce52448d2c1` compared byte-for-byte with the
   immutable manifest;
2. current reusable `/4` core identity is the 20 non-test runtime/schema files
   from that manifest, each still required to match its frozen hash; and
3. the three test files changed by authorized public product realignment commit
   `a98d648bd0adb2751ee0c09828e0227b1583950f` are current public regression
   evidence, not frozen qualification bytes.

A changed current public test is not, by itself, evaluator drift. It cannot be
called the original frozen suite, complete `/4` representability, a private
held-out verdict, Slice 4.5 `PASS`, or an overall product verdict. Any frozen
blob mismatch, unavailable frozen identity, current non-test core mismatch,
unattributed current test change, known-gap execution, or private/verdict
boundary breach remains a hard stop. The exact verification evidence is in the
[public freeze-boundary record](../../evaluation/evaluator-history.md).

## Closeout implementation status at acceptance

`M1/S4.5/EVAL-CLOSEOUT` received final acceptance on 2026-08-07. The condition
in decision 8 is satisfied: Slice 4.5 is closed by owner disposition. Slice 5
became eligible under the continuation verification profile. Later execution
status is maintained only in [current project state](../../current-state.md).
The exact evaluator-closeout evidence is in the
[closeout acceptance record](../../evaluation/evaluator-history.md).

## Later protocol `/4` retirement

On 2026-08-10 the project owner accepted
[ADR-0033](ADR-0033-retire-and-archive-protocol-4-evaluator.md). ADR-0033
supersedes decision 5 and the freeze-boundary clarification as current policy:
protocol `/4` is archived, has no active execution or review role, and is not
retained in the Infinium working tree. This record continues to preserve the
decision and evidence that governed the earlier evaluator-deferral closeout.
