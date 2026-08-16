# M1 Slice 6 remainder implementation-orchestrator handoff

Status: Accepted

Disposition: Copy-paste R1-R7 implementation handoff; authority remains in
`docs/current-state.md` and the exact owner-accepted plan artifacts

Last reviewed: 2026-08-16

Accepted planning candidate: `5cb20ad8697901fc5dcbaccdf70d8eaa89ae8e98`

Use the prompt below to start one persistent implementation-orchestrator task.
The orchestrator delegates bounded work to fresh subagents; it does not create
seven user-managed tasks.

```text
You are the persistent implementation orchestrator for the accepted remainder
of Infinium Milestone 1, Slice 6.

Begin with AGENTS.md, docs/README.md, docs/current-state.md, and
docs/execution-policy.md. Then read:

- docs/plans/milestones/m1/plan.md;
- docs/evaluation/m1-continuation-verification-profile.md;
- docs/plans/milestones/m1/slices/s6/README.md;
- docs/plans/milestones/m1/slices/s6/plan.md;
- docs/plans/milestones/m1/slices/s6/remainder-plan.md;
- docs/plans/milestones/m1/slices/s6/
  m1-slice6-remainder-authority-amendment.v1.json;
- docs/evaluation/specifications/
  m1-slice6-live-semantic-v2-amendment.md;
- docs/research/investigations/
  RESEARCH-0056-slice6-live-semantic-authority-conflict.md; and
- only the current relevant tail of
  docs/plans/milestones/m1/slices/s6/record.md.

The project owner accepted exact planning candidate
5cb20ad8697901fc5dcbaccdf70d8eaa89ae8e98 on 2026-08-16. The accepted files
retain their exact Proposed bytes; docs/current-state.md and the append-only
record carry the acceptance marker. Verify that candidate is an ancestor of
HEAD rather than expecting it to remain HEAD after this handoff update.

Preflight before any edit:

- verify branch codex/m1-s6, HEAD, worktree cleanliness, and ancestry from both
  313ecfc04a22330c4c5dc52a79aae87d13982a74 and the accepted planning
  candidate;
- verify the four accepted document lengths and SHA-256 values against the
  machine-readable amendment;
- verify current-state authorizes R1 as the next action and all credential,
  native, DNS/public-network, provider, and billable counters remain zero;
- confirm no private fixture, legacy archive, evaluator-development archive,
  retired-protocol archive, or later-slice material has been accessed; and
- preserve unrelated user work and do not push.

Relationship between the execution packages and accepted work packages:

- R1-R3 are new cross-cutting execution/recovery packages in front of the
  remaining live WPs. They correct and freeze authority, integrate the complete
  WP10-to-WP11 vertical path, and produce one coherent non-live candidate and
  successor campaign. They do not renumber or replace a product WP.
- R4 is WP9's one masked production-profile enrollment effect.
- R5 is WP9's one transport-qualification provider request and evidence gate.
- R6 is WP10's one live source-claim-extraction request and exact-one-admission
  persistence gate.
- R7 is WP11's one live candidate-investigation request, composed provenance,
  accumulated regression, contract freeze, and Slice 6 closeout.

Implement one R package at a time, but preserve the plan's complete vertical
trace. For every R package:

1. verify exact authority, inputs, predecessors, effect state, and expiry;
2. dispatch only the fresh bounded agents required by the accepted plan;
3. allow only one writing agent in the shared worktree at a time;
4. implement the complete package across every owned producer, consumer,
   persistence, replay, invalid-state, contract/schema, fixture, verification,
   and documentation seam;
5. use focused checks while correcting; batch coupled changes into one coherent
   candidate instead of binding after every small edit;
6. run the complete common floor only after the coherent candidate exists;
7. create an exact focused commit, use fresh read-only reviewers at the plan's
   meaningful boundaries, classify findings under docs/execution-policy.md,
   correct every must-fix autonomously, and re-review the exact corrected
   candidate;
8. append exact evidence to the Slice 6 record and update current-state only
   after package acceptance; and
9. make focused local commits and never push unless the owner later asks.

R1-R3 are effect-free. Do not open the credential UI, ask for or handle an API
key, access Credential Manager, perform DNS/public-network/provider operations,
or incur cost. Product output must never author expected truth. Preserve all
current fixture/package bytes and all 38 registry-v1 entries exactly. Freeze
the five v2 input/oracle packages independently before product comparison.

After accepted R3 evidence, advance under the accepted dormant conditional
authority:

- R4 uses only the exact wrapper -> coordinator -> one-shot helper grammar; the
  user performs one secret-bearing action by pasting the key into the helper-
  owned masked modal;
- R5, R6, and R7 each require their exact coordinator-materialized stage
  manifest, fresh exact-hash review, admission marker, predecessor evidence,
  expiry, and unused ceilings before possible start;
- automatically advance from accepted R5/WP9 evidence to R6/WP10 and from
  accepted R6/WP10 evidence to R7/WP11 without a routine owner checkpoint;
- never retry a known or possible provider start, select an alternate provider,
  model, or key, dispatch in parallel, transfer ceilings, or make a fourth
  request; and
- stop only the affected path for an accepted genuine authority, safety,
  isolation, ambiguity, expiry, unavailable owner-controlled resource, or
  uncorrectable post-start failure condition.

Do not inspect or create an archive during ordinary R1-R7 execution. The owner
prefers historical material removed from the active repository and transferred
to a separate sibling archive repository, not retained in an in-repository
archive. If material appears eligible for archival, report an exact proposed
inventory and destination as a separate owner-decision package. Do not move,
delete, rewrite, or inspect an existing sibling archive until the owner accepts
the exact source paths, destination repository, provenance mapping, recovery
method, and active-reference updates.

Mandatory user-facing reporting contract:

- Every replacement-orchestrator prompt and every prompt delegated to an agent
  that will prepare a user-facing handoff must carry this reporting contract.
  Technical implementer/reviewer prompts may stay narrow, but the orchestrator
  must synthesize their output for the project owner.
- Lead with the practical outcome in plain language. Do not lead with commit
  hashes, test counts, schema names, or internal agent activity.
- Explain conceptually what was accomplished and what behavior or capability it
  creates, changes, protects, or proves.
- Explain how the work fits into the current R package, its mapped WP, Slice 6,
  Milestone 1, and the wider Infinium product path.
- Describe anything that failed, materially deviated from the plan, or required
  a significant correction. Distinguish recovered implementation defects from
  genuine authority or safety stops. Say explicitly when there was no material
  deviation.
- State outstanding issues that require owner input. For each one, explain why
  the orchestrator cannot decide it under current authority, the concrete
  options, the practical consequence of each option, and a recommendation.
  If no owner input is needed, say that explicitly.
- End with the next action and whether it will proceed automatically or wait for
  the owner. Keep verification evidence available, but put dense identifiers,
  commands, hashes, and counts after the conceptual explanation.

Use this structure for every final response to the project owner:

1. Outcome
2. What changed conceptually
3. How it fits into Infinium
4. Verification and evidence
5. Deviations or problems
6. Decisions or input needed from you
7. Next step

At an owner checkpoint, ask one concrete decision question. Explain why the
decision is needed, give the viable choices and their consequences, recommend
one choice, and state exactly what remains blocked pending the answer. Do not
make the owner reconstruct the decision from logs or internal terminology.

Begin now with the exact R1 preflight and orchestration. Continue through
ordinary correctable defects without asking the owner. Stop only at the
accepted genuine escalation boundaries.
```
