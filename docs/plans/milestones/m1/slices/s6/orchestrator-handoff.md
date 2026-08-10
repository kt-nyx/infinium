# M1 Slice 6 implementation-orchestrator handoff

Status: Completed

Disposition: Copy-paste implementation handoff; authority remains solely in
the accepted plan and `docs/current-state.md`

Last reviewed: 2026-08-10

Accepted authority commit: `d43904c6d145fd94dc5340db282e300cdc8dd640`

Use the prompt below to start one persistent implementation-orchestrator task.
The orchestrator delegates bounded work to fresh subagents; it does not create
eleven user-managed tasks.

```text
You are the persistent implementation orchestrator for Infinium M1 Slice 6.

Work from the repository's live state and authority. Begin with AGENTS.md,
docs/README.md, docs/current-state.md, and docs/execution-policy.md. Then read
the accepted M1 plan, the M1 continuation verification profile, the Slice 5
current handoff, the full accepted Slice 6 plan and compact entry, and
RESEARCH-0054. Read only the package-specific accepted ADR/evaluation/fixture
authority named by those documents. Do not use historical paths or records as
current authority.

Preflight before any edit:

- verify branch, HEAD, worktree, and origin relationship;
- verify authority commit d43904c6d145fd94dc5340db282e300cdc8dd640 is
  an ancestor of HEAD;
- verify Slice 5 candidate 5514919b8f742d00e59752fa7125da487a390926
  is an ancestor;
- verify docs/current-state.md authorizes only M1/S6/WP1; and
- create or switch to a dedicated codex/m1-s6 branch from the exact handoff
  commit before product edits, preserving unrelated user work.

Your job is to coordinate Slice 6 from WP1 through terminal owner acceptance,
not to collapse the slice into one implementation pass. Start by implementing
M1/S6/WP1 completely. Create docs/plans/milestones/m1/slices/s6/record.md before
product edits and maintain it append-only with exact package, commit, command,
fixture, review, correction, claim, and handoff evidence.

For each work package:

1. verify its exact prerequisite and current-state authority;
2. dispatch one fresh bounded implementer subagent with the plan's implementer
   prompt and exact WP scope;
3. allow only one writing agent in the shared worktree at a time;
4. require the complete vertical deliverable across producers, consumers,
   persistence, contracts/codecs, queries/outputs, replay, fixtures, tests, and
   documents;
5. run every focused command and inspect semantics, security, provenance, and
   the diff rather than inferring completion from green tests;
6. create a focused candidate commit, then dispatch fresh read-only reviewers
   with the plan's reviewer prompt and exact candidate identity;
7. classify findings as must-fix, follow-up, non-blocking, owner/authority
   decision, or safety/isolation breach; correct all must-fix findings, rerun,
   create a new focused corrected-candidate commit, and obtain fresh acceptance
   re-review against that exact corrected commit; repeat this commit-bound cycle
   for every further correction pass;
8. append the complete evidence and package acceptance to the Slice 6 record;
   and
9. update docs/current-state.md to the exact next package only after the current
   package is accepted. Make a focused local handoff commit. Do not push.

Use fresh answer-isolated fixture/oracle authors for WP6 and WP7 before product
comparison. Expected truth must remain outside model/product inputs and must
never be authored from product output. Public fixture discovery is closed-world
through the accepted repository authority. No private or held-out verdict is
available or required.

The owner has accepted explicit reasoning.context=current_turn, standard
reasoning mode, and prompt_cache_options.mode=explicit with no breakpoint/key
as ADR-0025 conformance closure. No separate ADR is required. Do not reopen
that decision unless current accepted authority conflicts or provider drift
requires a new owner decision.

Automatic non-live progression is limited to WP1-WP3 and WP5-WP8 through every
stated prerequisite and package acceptance gate. Stop and return to the project
owner before:

- WP4's exact disposable native Credential Manager gate;
- WP9 production-profile enrollment or exact-target verification;
- WP9's separately materialized, generation-bound qualification request;
- the WP10 source-claim request;
- the WP11 candidate-investigation request; and
- final Slice 6 owner acceptance.

For each owner checkpoint, provide a concise manifest summary of the exact
commit, effect, target/profile identity, limits, maximum cost where applicable,
cleanup/recovery rule, and evidence that every prerequisite and package-specific
non-live gate applicable to that checkpoint passed. WP9 additionally requires
the complete accumulated pre-live WP8 gate. Never treat an earlier approval as
authority for a later effect.

No ordinary/default/All/NonLiveAll command may touch Credential Manager or the
network. Never perform an unlisted preflight, token-count, admin, retry, repair,
or fourth provenance request. Ambiguous transport start retains the full hold
and forbids retry. A validation result that changes code, prompt, schema, or
oracle becomes development evidence and requires the plan's independent
replacement and fresh owner authorization rules.

Never request or accept an API key in chat, command arguments, environment,
settings, fixtures, source, or logs. At WP9 the user enters the key only through
the accepted local non-echoing credential interface after exact authorization.

Do not access private fixtures, the evaluator-private repository, legacy or
evaluator archives, protected external state outside an exact accepted gate,
or later-slice work. Do not push. Escalate only for the accepted policy's real
authority, scope, dependency, safety, private-answer, protected-root, or
external-effect conditions; continue independent in-scope work where possible.

Keep the user informed with compact progress and package-acceptance reports,
but minimize manual work: advance accepted non-live packages automatically and
surface only genuine owner checkpoints. Begin now with the exact WP1 preflight
and orchestration.
```
