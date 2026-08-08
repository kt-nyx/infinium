# Plans

Status: Active navigation

Last reviewed: 2026-08-08

The current execution handoff lives in
[`../current-state.md`](../current-state.md). The repository-wide implementation
and review model lives in the
[`development execution policy`](../development/execution-policy.md). Read
those before using a plan or implementation record.

## Active planning authority

- Milestone: [M1 backend semantic proof](milestones/M1-backend-semantic-proof.md)
  with its accepted
  [revision 3 amendment](milestones/M1-backend-semantic-proof-evaluator-v2-amendment.md)
- Slice: [M1 Slice 5 — evidence, documentation, candidates, cases, and replay](slices/M1-slice-5-evidence-documentation-candidates-cases-replay.md)
- Current record: [M1 Slice 5](implementation-records/M1-slice-5.md)
- Public validation: [M1 continuation verification profile](../evaluation/m1-continuation-verification-profile.md)

Consult `current-state.md` for the current package and prerequisite handoff.
Do not duplicate that status in this index.

## Plan purpose

Plans consume accepted product requirements, ADRs, and evaluation cases. They
define bounded scope, sequencing, deliverables, and verification; they do not
create product meaning or convert historical execution constraints into
repository-wide policy.

An implementation milestone plan is created only after:

1. relevant product requirements are accepted;
2. blocking research is complete;
3. relevant ADRs are accepted; and
4. evaluation cases and acceptance criteria exist.

Every milestone plan should include:

- status, owner, objective, and current handoff;
- linked requirements, ADRs, evaluation cases, and dependencies;
- scope, non-scope, and authority boundaries;
- vertical slices or work packages;
- contract maturity and producer/consumer ownership;
- focused and accumulated verification;
- recoverable failures and genuine escalation conditions;
- rollout, rollback, or migration where relevant;
- implementation record and genuinely deferred follow-up.

Use the accepted [work-breakdown notation](work-breakdown-notation.md) for
independently assignable packages. Stage names remain reserved for the
evaluator lifecycle.

## Execution and review policy

Ordinary plans inherit the development execution policy unless they explicitly
invoke a narrower safety, private-evaluator, destructive, or externally
effectful protocol.

- Build the smallest useful vertical behavior through input, runtime,
  persistence, readback/output, focused fixtures, and review.
- Classify findings as must-fix, follow-up, non-blocking, owner/authority
  decision, or safety/isolation breach.
- Correct and re-review must-fix findings until acceptance; do not impose a
  fixed correction-pass budget on ordinary work.
- Reserve escalation for conflicts or gaps in accepted authority, required
  scope/permission expansion, unavailable owner-controlled dependencies, and
  security/private-answer/protected-root/destructive/external-effect
  boundaries.
- Pause only the affected path and continue independent in-scope work.

An acceptance failure is not automatically an escalation. Tests, fixtures,
schemas, validators, documentation, and implementations are expected to need
correction during development.

## Contract and fixture sequencing

Contracts use the maturity states defined in the development policy:
`Proposed`, `Implementation-active`, `Producer-consumer-validated`,
`Slice-frozen`, and `Milestone-stable`.

Before slice freeze, implementation feedback may revise a contract when all
affected producers, consumers, persistence, schemas, fixtures, tests, and
documentation change together. Minimal answer-free examples may establish
shape early. Semantic fixtures belong to the package implementing the
behavior; comprehensive cross-package corpora belong to integration/closeout.

## Historical plans and records

Milestone, slice, research, evaluator, and implementation history is indexed
under [`milestones/`](milestones/README.md), [`slices/`](slices/README.md), and
[`implementation-records/`](implementation-records/README.md).

Historical plans preserve the scope, correction limits, stop conditions, and
outcomes that governed those attempts. They do not authorize current work and
must not be used to infer the next package. In particular, Slice 4.5's private
evaluation, protocol freezes, one-shot operations, and correction budgets are
evaluator-specific history. Current ordinary product work uses the development
execution policy, active plan, and continuation verification profile.

The [research-agent handoff template](research-investigation-agent-handoff-template.md)
remains available for bounded research assignments. The initial milestone
sequence remains defined in
[`../product/scope-and-milestones.md`](../product/scope-and-milestones.md).
