# Plans

Status: Active navigation

Last reviewed: 2026-08-08

The [M0 research-foundation plan](milestones/M0-research-foundation.md) was
accepted on 2026-07-25 and completed on 2026-07-28. The accepted
[M1 backend semantic proof plan](milestones/M1-backend-semantic-proof.md) is
the active implementation plan together with its accepted
[revision 3 amendment](milestones/M1-backend-semantic-proof-evaluator-v2-amendment.md).
Completed-slice evidence is indexed under
[`implementation-records/`](implementation-records/README.md).

Slices 0 through 4 are historically implementation-complete. Slice 4's
original Mutagen semantic and typed-index candidate at `98fe8a5` retains its
historical public verification. Evaluator-v1 attempts produced no valid
held-out product verdict.
The historical evaluator-v2 `/2` Stage C invocation ran once, but Stage C.5
invalidated its product verdict, so no valid successor held-out verdict exists.
Historical [Slice 4.5 evaluator work](slices/M1-slice-4.5-held-out-evaluation-v2.md) qualified
and frozen final protocol `/4` at
`3693d19563c636cd2879804633ca4ce52448d2c1`. The B2 input bytes already exist
and passed independent byte review. The single authorized B2 resume ran once
and terminated on another public lexical-authority gap without an oracle,
candidate execution, scoring, or product verdict. The accepted public
realignment and candidate freeze remain complete at
`a98d648bd0adb2751ee0c09828e0227b1583950f`. The accepted
[protocol `/4` oracle-contract completion plan](slices/M1-slice-4.5-protocol-4-oracle-contract-completion.md)
ran its public-only authorability attempt and stopped after the permitted
correction pass exposed a second material public-authority gap. Candidate
conformance was not inspected or classified. The project owner has now accepted
ADR-0029 and the successor
[Pre-B2 evidence-contract totality plan](slices/M1-slice-4.5-pre-B2-evidence-contract-totality.md).
WP1-WP5 completed and WP5 classified an evaluator `/4` representation gap.
The owner then accepted ADR-0030 and the
[protocol `/5` successor plan](slices/M1-slice-4.5-protocol-5-successor-realignment.md).
WP1 then hard-stopped after the required audits confirmed an accepted-model
composition contradiction between the FaceGen unknown-loose / archive-supported
state and coverage/gap arithmetic. The
[public hard-stop record](../evaluation/m1-slice4-protocol-5-wp1-representability-hard-stop.md)
preserves that decision point. ADR-0031 and WP1R accepted the distinct `/5`
successor model, exact loose-availability gap, and global composition proof;
WP1V then hard-stopped, WP1 never proof-closed, and WP2-WP4 never started.
ADR-0032 now retires `/5` unqualified, retains `/4` only for bounded public
regression with its known gap excluded, and defers private held-out evaluation
without a product verdict. No B2, C2, Stage D, private corpus, scoring, or
replacement-evaluator work is authorized. Evaluator-deferral closeout is
accepted; Slice 4.5 is closed, Slice 5 is active with WP1 complete and WP2 next
under the M1 continuation verification profile, and M1 remains active.
The accepted
[M1 Slice 5 execution plan](slices/M1-slice-5-evidence-documentation-candidates-cases-replay.md)
defines the clean-break evidence-to-replay work as `M1/S5/WP1` through `WP6`.
Its accepted staged-verification recovery amendment removes the rejected
preauthored 28-package corpus from current authority: WP1 owns contracts,
state, migration, and boundaries; WP2-WP5 own their semantic cases; WP6 owns
the comprehensive corpus. `M1/S5/WP1` is complete and reviewed, so
`M1/S5/WP2` is the next eligible package; each later package remains gated by
its declared prerequisite and review.
The completed
[public Bethesda semantic realignment plan](slices/M1-slice-4.5-public-product-realignment.md)
records that bounded implementation, review, qualification, and candidate-
freeze contract. It does not itself authorize scoring.
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

Use the accepted [work-breakdown notation](work-breakdown-notation.md) when a
slice needs plan-local phases or independently assignable work packages. Stage
names remain reserved for the evaluator lifecycle.

The initial milestone sequence is described in
[`../product/scope-and-milestones.md`](../product/scope-and-milestones.md).

The historical M1 Slice 4.5 Stage A public evaluator is frozen at protocol `/4`.
ADR-0028 and the accepted semantic-authority owner disposition resolve the
later public mismatch, but do not by themselves authorize B2. Public product
realignment, requalification, and a newly frozen candidate must precede the
one permitted fresh-reviewer B2 resume. The later representation gap was
recorded for owner milestone disposition; product output remains prohibited as
oracle or evaluator truth.

That disposition is now ADR-0029 plus `M1/S4.5/PRE-B2`: define a total
evidence-state contract, prove it mechanically, exercise it from the model,
obtain a fresh product-blind review, and only then classify the frozen
candidate. WP1-WP5 completed and the classification proved a `/4`
representation gap. ADR-0030 and `M1/S4.5/PRE-B2/V5` historically authorized
one public `/5` attempt, but ADR-0032 retired it unqualified after WP1V's final
hard stop. Current work is the public
[evaluator-deferral closeout](slices/M1-slice-4.5-evaluator-deferral-and-m1-continuation.md),
not B2 or successor qualification.
