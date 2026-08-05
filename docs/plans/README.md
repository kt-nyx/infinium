# Plans

Status: Draft  
Last reviewed: 2026-08-05

The [M0 research-foundation plan](milestones/M0-research-foundation.md) was
accepted on 2026-07-25 and completed on 2026-07-28. The accepted
[M1 backend semantic proof plan](milestones/M1-backend-semantic-proof.md) is
the active implementation plan together with its accepted
[revision 3 amendment](milestones/M1-backend-semantic-proof-evaluator-v2-amendment.md).
Completed-slice evidence is indexed under
[`implementation-records/`](implementation-records/README.md).

Slices 0 through 4 are historically implementation-complete. Slice 4's
unchanged Mutagen semantic and typed-index candidate is frozen at `98fe8a5` and
retains its historical public verification, but it does not yet conform to the
later owner-accepted ADR-0028 contract. Evaluator-v1 attempts produced no valid
held-out product verdict.
The historical evaluator-v2 `/2` Stage C invocation ran once, but Stage C.5
invalidated its product verdict, so no valid successor held-out verdict exists.
Active [Slice 4.5](slices/M1-slice-4.5-held-out-evaluation-v2.md) has qualified
and frozen final protocol `/4` at
`3693d19563c636cd2879804633ca4ce52448d2c1`. The B2 input bytes already exist
and passed independent byte review, but B2 oracle qualification under `/4` has
not run. The six semantic questions are resolved; public implementation,
requalification, and a newly frozen candidate are now required before B2. C2
has not run, Stage D has not started, Slice 5 remains blocked, and M1 remains
active.
The accepted
[public Bethesda semantic realignment plan](slices/M1-slice-4.5-public-product-realignment.md)
defines that bounded implementation, review, qualification, candidate-freeze,
and fresh-agent handoff contract. It does not authorize private work or scoring.
The accepted
[Slice 3.5 execution plan](slices/M1-slice-3.5-bethesda-fixture-qualification.md)
is indexed under [`slices/`](slices/README.md).

The product baseline and ADR-0001 through ADR-0011 were accepted on
2026-07-25. ADR-0012 through ADR-0023 were accepted and ADR-0024 was rejected
on 2026-07-28. Waves A through D have accepted integrated dispositions. Gates
A through D are met at their applicable M0 research, design, or qualification
layers. Gate C was
closed on 2026-07-28 by the accepted category-neutral anti-overfitting rules
and RESEARCH-0034/0035. EVAL-0016 and EVAL-0017 are qualified candidates, not
executed or passed cases. Wave E research is complete through RESEARCH-0046.
ADR-0015 through ADR-0023 accept the complete Wave E architecture; Dapr and
ADR-0024's Codex proposal are rejected. Gate E is met at the M0
architecture/design layer. Wave F's evaluation/research package and M1 plan
were integrated, independently reviewed, and accepted on 2026-07-28. Gate F is
met and M0 is complete. This acceptance authorizes only the bounded M1 plan; it
does not mark an implementation or evaluation case passed. The completed M0
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

The M1 Slice 4.5 Stage A public evaluator is frozen at final protocol `/4`.
ADR-0028 and the accepted semantic-authority owner disposition resolve the
later public mismatch, but do not by themselves authorize B2. Public product
realignment, requalification, and a newly frozen candidate must precede the
one permitted fresh-reviewer B2 resume. Another oracle-authority gap does not
authorize `/5`, evaluator expansion, or product output as oracle truth; it must
be recorded for owner milestone disposition.
