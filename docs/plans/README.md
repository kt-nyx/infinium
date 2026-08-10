# Plans

Status: Active navigation

Last reviewed: 2026-08-10
The live handoff is [current project state](../current-state.md). Ordinary work
follows the [repository execution policy](../execution-policy.md).

Plans consume accepted product requirements, ADRs, and evaluation cases. They
define scope, sequencing, deliverables, and verification; they do not create
product meaning or current status.

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

The legacy `slices/` directory contains only a tiny historical redirect whose
exact path is linked by the immutable protocol `/4` authority matrix. It is not
an active parallel plan hierarchy; its original bytes are Git-inventoried.

## Authority and history

Only `current-state.md` states the next eligible action. Historical plans and
records preserve the rules and outcomes that applied at their exact time; they
must not be updated to advertise later package status. Superseded evaluator
attempts are summarized in [Evaluator history](../evaluation/evaluator-history.md)
and retained byte-for-byte through the linked Git inventory.
