# Plans

Status: Draft  
Last reviewed: 2026-07-26

The [M0 research-foundation plan](milestones/M0-research-foundation.md) was
accepted on 2026-07-25 and is the active research plan. No implementation plan
is active.

The product baseline and ADR-0001 through ADR-0011 were accepted on
2026-07-25. Waves A through C have completed investigations and accepted
integrated dispositions. Gates A and B are met. Gate C retains the exact
loose-only FaceGen qualification and EVAL-0016/EVAL-0017 real-mod case
prerequisites documented in RESEARCH-0024. Waves D through F remain. The M0
plan:

1. identify the accepted requirements it serves;
2. select and sequence research questions with expected investigation
   deliverables;
3. define reviewable research completion criteria.

Use the
[research-investigation agent handoff template](research-investigation-agent-handoff-template.md)
for bounded fresh-agent assignments and separate wave integration review.

An implementation milestone plan is created only after:

1. relevant product requirements are accepted;
2. its blocking research is complete;
3. relevant ADRs are accepted;
4. evaluation cases and acceptance criteria exist.

Plans consume product and architecture decisions; they do not redefine them.
An earlier milestone plan may validate only the bounded portion of a later
delivery requirement that it exercises, but it may not waive the requirement's
applicable scope, authority, evidence, provenance, or safety constraints.

Every milestone plan should include:

- status and owner;
- objective;
- linked requirements and ADRs;
- dependencies and preflight;
- scope and non-scope;
- implementation slices or research work packages, as applicable;
- artifacts/contracts;
- evaluation cases;
- verification commands and review;
- rollout/rollback or migration where relevant;
- completion record;
- genuinely deferred follow-up.

The initial milestone sequence is described in
[`../product/scope-and-milestones.md`](../product/scope-and-milestones.md).
