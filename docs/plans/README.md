# Plans

Status: Accepted
Disposition: Active navigation

Last reviewed: 2026-08-17

The live handoff is [current project state](../current-state.md). Ordinary work
follows the [repository execution policy](../execution-policy.md).

Plans consume accepted product requirements, ADRs, and evaluation cases. They
define scope, sequencing, deliverables, and verification; they do not create
product meaning or current status.

For remaining M1 work, the accepted
[process-continuation amendment](milestones/m1/amendments/process-continuation.md)
applies the repository policy's coherent-candidate, proportional-verification,
consolidated-review, and bind-once lifecycle. Narrow immutable fixture/oracle
and external-effect rules still govern their exact boundaries.

The accepted
[semantic-oracle deferral amendment](milestones/m1/amendments/semantic-oracle-deferral.md)
removes independent semantic answer-key qualification from M1 and M2
acceptance while preserving ordinary product conformance. Re-entry is possible
only after M2 acceptance during M3 planning under ADR-0035.

## Hierarchy

- [Milestone plans](milestones/README.md)
  - [M0 research foundation](milestones/m0/plan.md)
  - [M1 backend semantic proof](milestones/m1/README.md)
- [Work-breakdown notation](work-breakdown-notation.md)
- [Research investigation handoff template](research-investigation-agent-handoff-template.md)

Every slice directory sits beneath its owning milestone. A slice plan, its
amendments, implementation record, and stable supporting reviews live together
instead of in parallel global folders. Work-package sections remain inside
their owning slice plan and record unless they require an independently
authoritative artifact.

The legacy `slices/` directory has been removed. Its final evaluator-only
redirect is preserved by Git identity in the retired-asset inventory and the
external protocol archive; it is not an active parallel plan hierarchy.

## Authority and history

Only `current-state.md` states the next eligible action. Historical plans and
records preserve the rules and outcomes that applied at their exact time; they
must not be updated to advertise later package status. Superseded evaluator
attempts are summarized in [Evaluator history](../evaluation/evaluator-history.md)
and retained byte-for-byte through the linked Git inventory.
