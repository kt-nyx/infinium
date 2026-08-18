# M1 backend semantic proof plan revision 4 process amendment

Status: Accepted

Disposition: Owner-accepted process amendment; documentation activation
candidate pending final owner acceptance before C1 becomes eligible

Owner: Project owner

Prepared: 2026-08-17

Accepted: 2026-08-17

Accepted by: Project owner

Last reviewed: 2026-08-17

Plan revision: `infinium.plan.m1.backend-semantic-proof/4`

Amendment ID: `0e08774d-e1d8-4f9e-a396-d06aa4cbac76`

Predecessors: the accepted M1 plan, accepted development execution policy,
accepted M1 continuation verification profile, and accepted Slice 6 remainder
plan through R2

Accepted proposal commit: `2c82365fd853cb2021f1772d6c572ee9fa006d01`

Accepted proposal SHA-256:
`5d9aff4226f93ff73025573e056080530d23f66e5cb9cc92efddfd78655acc9f`

## 1. Purpose and authority status

This amendment restores a proportional development lifecycle for the remaining
M1 work. It preserves Infinium's product, security, evaluator, provenance,
persistence, replay, contract, and external-effect boundaries while preventing
ordinary implementation correction from becoming a sequence of premature
candidate freezes, bindings, full-floor executions, and historical closeouts.

The project owner accepted the exact proposal bytes identified above on
2026-08-17. That decision authorizes only the documentation activation package
defined in Section 9. Until the owner accepts that package's exact commit:

- the accepted execution policy remains unchanged;
- Slice 6 R3 remains inactive;
- the accepted Slice 6 remainder plan remains the historical governing plan
  through R2 and dormant authority beyond it;
- no implementation package is opened; and
- no credential, helper, native, network, provider, billable, private,
  destructive, archive, or push operation is authorized.

Once activated through the documentation-only handoff in Section 9, this
amendment governs the ordinary candidate, verification, review, correction,
and navigation lifecycle for the remaining M1 work. A
narrower accepted safety, answer-isolation, destructive, credential, or
externally effectful protocol continues to override it only for the exact
irreversible boundary that requires the narrower rule.

## 2. Diagnosis

M1's contract-first vertical slices, independent expected truth, provenance,
persistence/replay, and explicit security/effect boundaries remain sound. The
process problem is narrower: ordinary correction was repeatedly promoted into
immutable candidate, freeze, bind, and acceptance events. Full verification
and broad review were then used to discover whether a candidate was viable,
rather than to confirm a candidate already made coherent by focused work.

That pattern caused local defects to trigger bookkeeping cascades, encouraged
fix-bind-fix-bind sequences, duplicated rejected-candidate chronology in
current navigation, and made it difficult to distinguish engineering progress
from procedural churn. The remedy is not weaker evidence. It is to apply
immutability and the largest gates at meaningful boundaries.

## 3. Default candidate lifecycle

Ordinary M1 work uses this lifecycle:

```text
working implementation
  -> coherent vertical package
  -> focused verification
  -> one consolidated semantic/security/provenance/diff review
  -> batched corrections on the same working candidate
  -> affected verification and changed-surface re-review
  -> review-ready package
  -> one final verification floor
  -> one accepted candidate binding
```

### 3.1 Working candidate

A working candidate is the current coherent work package, not an immutable Git
SHA. Its commits may change during implementation and correction. Intermediate
commits, failed runs, diagnostic receipts, and review inputs are development
evidence only. They do not become accepted authority merely because they have a
stable identity.

Authors batch known findings before correction. Mechanical or local corrections
stay on the same working candidate and receive focused verification. They do
not create a new package version, fixture partition, acceptance claim, or
record closeout unless the meaning of an independently frozen input/oracle or
another genuinely immutable contract changes.

### 3.2 Coherent package and consolidated review

A package becomes review-ready only after its complete declared vertical path
exists across every affected producer, consumer, persistence, readback/output,
replay, invalid-state, fixture, test, and documentation seam. Focused checks
must already pass.

One consolidated review then examines the coherent package for product
semantics, contract closure, persistence/replay, provenance, answer isolation,
security/effect boundaries, scope, claims, and the complete diff. Reviewers
classify findings under the accepted execution policy. `CORRECT` remains the
normal result for repairable defects.

A full review is not restarted for each mechanical or wording correction. The
changed surface receives focused re-review. A new consolidated review is
required only when correction materially changes semantics, architecture,
authority, an immutable fixture/oracle, or the package's declared scope.

### 3.3 Verification pyramid

Verification is proportional:

1. run the smallest checks that exercise the current change while developing;
2. run the package's focused contract, semantic, persistence, replay,
   security, and fixture checks when the vertical path is coherent;
3. perform consolidated review and correct its findings;
4. run the accepted complete floor only when the package is review-ready; and
5. retain only a passing final-floor execution against the exact accepted
   candidate as acceptance evidence.

If the final floor finds a defect, the candidate was not final. The failed run
is diagnostic evidence: correct the same working candidate, rerun affected
checks and changed-surface review, then run a new final floor when ready. Do not
freeze, bind, or append rejected-candidate chronology merely because the failed
run had a commit identity.

The complete floor must pass against the exact accepted commit. This amendment
changes its timing, not its contents or acceptance strength.

### 3.4 Recurrent conceptual defects

If the same conceptual defect recurs after two completed correction attempts,
pause implementation on that path for an explicit design diagnosis. Record the
invariant being violated, the two attempted resolutions, why they failed, and
the smallest durable correction.

This diagnosis is not automatically an owner escalation. Continue within
existing authority when the durable correction is ordinary implementation,
test, fixture, validator, documentation, or decomposition work. Request owner
disposition only when the resolution would choose missing product meaning,
change accepted architecture, expand scope or authority, weaken isolation, or
require an otherwise unauthorized effect.

## 4. Immutable boundaries that remain strict

This amendment does not make every artifact mutable. Exact freezing and binding
remain mandatory for:

- independently authored fixture inputs before oracle access where the owning
  evaluation specification requires that ordering;
- independently authored oracles before product comparison;
- accepted public package identities, partitions, provenance, and exact bytes;
- exact credential or external-effect manifests immediately before admission;
- semantic-driving bytes at a possible external start;
- durable evidence for any known or possible external effect;
- final accepted implementation and verification evidence; and
- contracts at their accepted `Slice-frozen` or `Milestone-stable` maturity
  boundary.

Expected truth remains outside product inputs and production runtime. Product
output never authors or revises an oracle. A validation-driving change follows
the owning fixture/evaluation replacement rule, and a possibly started external
operation remains subject to its no-retry rule.

## 5. Runtime and Git authority

Runtime effect authority comes from a closed typed manifest plus durable
coordinator-owned admission, use, settlement, and terminal state. Git binds the
reviewed implementation and evidence but does not itself grant runtime
authority.

Production or effect-execution paths must not infer permission, stage state,
predecessor acceptance, or marker ownership from current `HEAD`, branch name,
commit subjects, log order, pickaxe results, line attribution, or historical
message discovery. A manifest may bind an exact reviewed commit, and inert
preflight may validate that binding and clean status. The transition that
admits an effect must nevertheless be represented and validated through the
typed authority and durable product state.

The accepted coordinator, persistence, credential, security, and budget ADRs
already select this architecture. This amendment records conformance and does
not create a new architecture decision. A new ADR is needed only if later
review demonstrates a real unresolved architectural alternative rather than a
process or conformance defect.

## 6. Plan, record, and navigation discipline

Current navigation states only the live handoff, accepted inputs, meaningful
current gaps, and next gate. It does not accumulate every rejected candidate,
failed command, superseded receipt, or correction chronology.

Implementation records retain accepted package evidence and material
correction history needed to interpret it. Diagnostic identities remain in Git
history and tool output unless they are necessary to explain the accepted
result, a safety incident, an unresolved gap, or recovery authority.

Do not create an in-repository historical archive. Any later transfer of
historical material to a sibling archive remains a separate, exact,
owner-authorized package.

## 7. Review roles and orchestration

- Keep one writing agent at a time for an exact worktree.
- Use bounded read-only review roles at meaningful package boundaries.
- Preserve input/oracle authorship separation where independently expected
  truth is involved.
- Reviewers may return `ACCEPT`, `CORRECT`, or `ESCALATE`; they do not create
  product meaning or authorize an external effect.
- Orchestrators report conceptual progress, material corrections, verification
  state, owner decisions, and the next gate without requiring the owner to
  reconstruct status from hashes or internal labels.

## 8. Slice 6 application

The companion
[Slice 6 lean continuation plan](../slices/s6/continuation-plan.md) maps every
remaining accepted R3-R7 obligation into three outcome packages:

1. `M1/S6/C1` effect-free readiness closure;
2. `M1/S6/C2` one bounded live campaign with exact stage gates; and
3. `M1/S6/C3` retained-evidence closeout and Slice 6 acceptance preparation.

The mapping preserves WP9, WP10, and WP11 product/evaluation identities. It
changes orchestration and authority timing, not their required semantic
outcomes.

## 9. Acceptance and activation

The owner's 2026-08-17 acceptance binds the proposal commit and SHA-256 values
recorded in this amendment and the companion Slice 6 continuation plan. It
approves the model and authorizes this documentation-only activation package;
it does not itself change the live handoff. The activation package, once
accepted at its exact commit:

- supersedes conflicting ordinary candidate/freeze/bind/review cadence in
  narrower M1 plans;
- does not weaken any exact immutable or external-effect boundary in Section 4;
- retires the unexercised old Slice 6 successor campaign only as specified by
  the companion plan; and
- opens only the exact package named by its updated `current-state.md`; it does
  not authorize any external effect by itself.

The documentation-only activation package may update
`AGENTS.md`, `docs/execution-policy.md`, `docs/current-state.md`, the M1 plan
and navigation, the M1 continuation verification profile, and Slice 6
navigation to incorporate this amendment without rewriting historical plans or
records. It must preserve the accepted proposal's meaning, pass
documentation/diff validation, and receive fresh review. Only its accepted
`current-state.md` handoff may open C1.
