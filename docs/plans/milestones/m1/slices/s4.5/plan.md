# M1 Slice 4.5 — Evaluator deferral and M1 continuation

Status: Accepted and completed

Current disposition: Historical execution plan. ADR-0033 later retired and
externally archived protocol `/4`; none of the wrapper, freeze, test, or review
instructions below remain runnable or current.

Last reviewed: 2026-08-10

Prepared: 2026-08-07

Accepted: 2026-08-07

Accepted by: Project owner

Owner: Project owner

Work ID: `M1/S4.5/EVAL-CLOSEOUT`

Parent: `M1/S4.5`

Depends on:

- accepted M1 backend semantic proof plan revision `/3`;
- accepted ADR-0027 through ADR-0031;
- qualified and frozen public evaluator protocol `/4` at commit
  `3693d19563c636cd2879804633ca4ce52448d2c1`;
- conforming public Slice 4 candidate
  `a98d648bd0adb2751ee0c09828e0227b1583950f`;
- completed `M1/S4.5/PRE-B2/WP1` through `WP5` classification evidence;
- accepted `M1/S4.5/PRE-B2/V5/WP0` and `WP1R` history; and
- the `M1/S4.5/PRE-B2/V5/WP1V` proof-closure hard stop.

Next work package: `M1/S5`, subject to normal preflight and an accepted slice
execution plan

Accepted clarification, 2026-08-07 (`M1/S4.5/EVAL-CLOSEOUT/WP2/T1`): the
historical 23-file freeze identity is the exact Git tree at evaluator commit
`3693d19563c636cd2879804633ca4ce52448d2c1` plus the immutable freeze
manifest. Current reuse additionally requires the 20 non-test runtime/schema
files in the manifest to remain byte-identical. The three listed public test
files may evolve through separately authorized public work and are current
regression evidence, not original frozen qualification bytes. See the
[freeze-boundary clarification](../../../../../evaluation/evaluator-history.md).

Final closeout status, 2026-08-07: WP2 removed active `/5` machine artifacts
and established bounded `/4` regression; WP3 established the continuation
profile and reconciled status; both fresh WP4 reviews accept after the one
shared ledger-classification correction; and WP5 full public verification
passes. Slice 4.5 is closed by owner disposition. That closeout made Slice 5
eligible under the continuation profile. Later status is maintained only in
[current project state](../../../../../current-state.md). See the
[acceptance record](../../../../../evaluation/evaluator-history.md).

## Owner disposition embodied by this plan

This plan closes the current M1 private-held-out evaluator effort without a
product verdict. It replaces the held-out `PASS` prerequisite for later M1
slices with a bounded public verification profile appropriate to M1's backend-
semantic-proof objective.

Protocol `/5` is retired unqualified. It has no evaluator implementation or
freeze, and its failed WP1/WP1V proof artifacts are not usable representation
authority. The protocol identity is reserved as historical and must not be
reused, resumed, repaired, or described as qualified.

Protocol `/4` remains the only retained executable evaluator-v2 protocol. It
is immutable, qualified historical public evidence and may be used only for
explicitly allowlisted public calibration and regression checks inside its
known representable subset. Its `RACE/DATA` partial-publication representation
gap prevents it from issuing a complete current-product or M1 held-out verdict.
Older `/2` and `/3` files required by the frozen `/4` inventory may remain as
historical bytes, but they are not active protocol authorities or runnable
alternatives.

Private corpus qualification, B2/C2 scoring, and a private held-out product
verdict are deferred until the product has a stable end-to-end semantic output
boundary, expected values are demonstrably independently authorable, and a new
owner-approved evaluator plan is accepted. The intended reconsideration point
is after M1 Slice 9 during M3 trusted-personal-preflight planning. Deferral does
not authorize private access, reveal, repair, reseal, scoring, or migration.

This plan is accepted and completed. Slice 4.5 is closed as **public
conformance complete; private held-out evaluation deferred; no held-out product
verdict**. This closeout made Slice 5 eligible to start under the replacement
M1 public verification profile. Later execution status is maintained only in
[current project state](../../../../../current-state.md). This is an explicit
milestone-plan disposition, not an
implicit waiver or a claim that the product passed hidden evaluation.

## Why retirement is proportionate

M1 must prove an evidence pipeline and one generic semantic mechanism. It does
not require a formally verified general semantic oracle. Protocol `/5` reached
only public modeling and proof work; WP2 representability, WP3 evaluator
implementation, WP4 acceptance/freeze, private qualification, and scoring never
started.

WP1V made deterministic progress but its final independent review proved that
the ledger builder could invent canonical link values and that the projection
validator could accept those values by comparing a document back to the same
ledger. Correcting the known placeholders alone would not establish an
independent expected-value authority for the remaining property surface.
Continuing the same architecture would make the verifier approximate a second
product implementation before later M1 slices have stabilized the output
contract.

The accumulated evaluator work remains useful as historical evidence, product-
contract clarification, anti-overfitting policy, and regression infrastructure.
This plan preserves those benefits without allowing an unqualified proof
system to block unrelated product progress.

## Governing principles

1. Preserve history; do not rewrite a failed attempt into acceptance.
2. Extract durable product semantics before removing protocol-specific
   artifacts.
3. Keep frozen protocol `/4` byte-identical.
4. Use `/4` only where its representation is known to be adequate.
5. Never use product output as expected truth.
6. Replace the held-out gate with explicit public evidence, not with an
   unqualified assertion of correctness.
7. Keep coverage, uncertainty, abstention, and unsupported states visible.
8. Reintroduce held-out evaluation only around a stable, independently
   authorable, user-meaningful output boundary.

## Scope

### In scope

- preserve an exact Git checkpoint of the failed WP1V work and hard-stop
  evidence before cleanup;
- accept a new ADR superseding the active M1 `/5` authorization and the M1
  held-out-`PASS` sequencing gate;
- migrate the durable FaceGen loose-availability and layered-gap semantics out
  of `/5`-specific authority into the applicable product/evaluation authority;
- retire and remove active `/5` specifications, schemas, generated summaries,
  ledger, and validation scripts from the working tree;
- retain `/5` ADRs, plan, and hard-stop/acceptance records as clearly marked
  historical evidence;
- define and mechanically verify the safe public-regression boundary for
  frozen `/4` without changing it;
- define the replacement M1 continuation verification profile for Slices 5-9;
- amend milestone, slice, evaluation, governance, case, plan-index, and status
  documentation consistently;
- update the deferred-question/residual-risk register with the missing held-out
  verdict and future evaluator prerequisites;
- perform fresh evaluator-boundary and documentation-coherence reviews; and
- leave a clean local branch with focused commits and no push.

### Out of scope

- modifying frozen `/4` code, schemas, canonicalization, adapter, scorer,
  calibration, manifests, or freeze records;
- implementing or qualifying `/5`, `/6`, or any other successor evaluator;
- candidate modification or execution for a held-out verdict;
- evaluator-private repository or fixture access;
- oracle/corpus construction, maintenance, qualification, reveal, repair, or
  reseal;
- B2, C2, Stage D scoring closeout, or any private comparison;
- treating the failed WP1V ledger, generated documents, product output, or
  Mutagen behavior under test as expected truth;
- starting Slice 5 implementation inside this plan; or
- weakening accepted product semantics, layered evidence, answer isolation,
  provenance, coverage, gap, or anti-overfitting requirements.

## Required end state

```text
Product semantics and M1 public verification
├── Accepted product requirements and ADRs
├── Public tests and independently authored development/validation fixtures
├── Frozen protocol /4
│   └── bounded historical calibration/regression only
├── Protocol /5
│   └── retired unqualified; historical records only
├── Private held-out evaluation
│   └── deferred; no current product verdict
└── Slices 5-9
    └── gated by the accepted M1 continuation verification profile
```

## Artifact-disposition policy

### Preserve as immutable history

- ADR-0030 and ADR-0031, marked superseded for active M1 evaluator work by the
  new disposition ADR;
- the protocol `/5` successor plan, marked closed/retired without completion;
- WP1, WP1R, and WP1V acceptance/hard-stop records;
- the Slice 4.5 append-only implementation-record entries;
- exact starting commits, file hashes, reviewer findings, and proof summaries
  needed to explain why `/5` was retired; and
- Git history containing the exact failed WP1V artifact checkpoint.

Historical documents may link to removed paths only when they state that the
artifact is available at an exact historical commit. Active indexes and
current-state summaries must not present a removed path as current authority.

### Extract into durable non-`/5` authority

Before deletion, confirm that the following remain expressed without depending
on an active `/5` model:

- loose FaceGen availability remains `present`, `absent`, or `unknown`;
- unknown loose availability cannot become exact absence or presence;
- archive support is independent from loose-availability authority;
- positive incomplete loose coverage has an explicit gap owned by population
  `face-gen-loose-assets` and missing capability
  `exhaustive-byte-verified-loose-provider-index`;
- the gap is visible in snapshot and result reporting where a result exists;
- zero, partial, and complete coverage lifecycles remain exact; and
- earlier independently proven evidence survives unavailable later-layer
  semantics under ADR-0029.

The implementation agent must identify the authoritative destination for each
rule—normally ADR-0028, ADR-0029, requirements, domain model, coverage policy,
or analysis catalog—and must not duplicate normative wording unnecessarily.
The new disposition ADR records only the authority movement and retirement.

### Remove from the active working tree

After historical checkpointing and semantic extraction, remove:

- `docs/evaluation/specifications/m1-slice4-protocol-5-successor-*`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-global-composition-summary.json`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-projection-*`;
- `docs/evaluation/specifications/m1-slice4-protocol-5-rule-coverage-ledger.json`;
- `eng/validate-m1-slice4-protocol5-global-composition.ps1`;
- `eng/validate-m1-slice4-protocol5-projection-contract.ps1`; and
- `eng/build-m1-slice4-protocol5-rule-ledger.ps1`.

Do not remove historical narrative records merely because their names contain
`protocol-5`. Do not remove or modify frozen `/4` artifacts.

## Frozen `/4` bounded-regression policy

The bounded profile enforces three separate layers:

1. **Historical freeze integrity:** read all 23 manifest paths as raw Git blobs
   at `3693d19563c636cd2879804633ca4ce52448d2c1` and require exact byte length
   and SHA-256 matches against the immutable freeze manifest.
2. **Current reusable-core integrity:** mechanically classify and require all
   20 non-test runtime/schema files in the current checkout to match their
   frozen hashes. A missing file, extra claimed runtime dependency, or identity
   drift fails closed.
3. **Current public regression health:** run only allowlisted current public
   calibration/regression tests, record their current commit and file
   identities, and label their results current public regression evidence.
   Their current hashes are not historical qualification acceptance criteria.

The three current public test files in the freeze manifest are not part of the
reusable-core hash gate. A current public test differing from its historical
blob is not by itself evaluator drift when Git attributes it to authorized
public work. It must never be represented as the original frozen qualification
suite or as complete semantic, held-out, Slice 4.5, or product acceptance.

Create:

- `docs/evaluation/m1-slice4-protocol-4-bounded-regression-usage.md`; and
- a machine-readable bounded-regression profile whose schema or validator
  fixes the exact `/4` evaluator commit, protocol/projection identities,
  immutable file hashes, included public commands/cases, excluded semantic
  states, and prohibited verdict claims.

The profile must distinguish:

### Permitted

- public answer-known calibration;
- public schema, canonicalization, malformed-input, mutation, determinism, and
  write-confinement tests already qualified for `/4`;
- public regression comparison for fact families and states that `/4` can
  represent exactly; and
- verification that all frozen-commit blobs match the immutable manifest and
  that the current non-test reusable core has not drifted.

### Prohibited

- a complete verdict over the current accepted Slice 4 semantic contract;
- treating `/4` success as M1, Slice 4.5, or private held-out `PASS`;
- scoring, adapting, inspecting, or qualifying private corpus material;
- invoking private execution manifests or answer-bearing expected output;
- using `/4` to reject the accepted partial `RACE/DATA` product behavior;
- changing frozen `/4` to accommodate later semantics; and
- allowing `/4` output to author product or replacement-evaluator truth.

Add a public validation command outside the frozen evaluator tree that checks
the profile and freeze identities, runs only the permitted public calibration
and focused tests, and emits a terminal label such as
`BOUNDED_REGRESSION_PASS`. Its documentation must state that this means the
historical tool and allowlisted regression surface remain healthy, not that the
current product passed held-out evaluation. Run this command twice under
Windows PowerShell 5.1 and twice under PowerShell 7 when practical; otherwise
record and justify the exact supported runtime boundary.

## Replacement M1 continuation verification profile

Create `docs/evaluation/m1-continuation-verification-profile.md` as the
normative M1 development/validation gate after evaluator deferral. It must map
each required layer to requirements, cases, evidence, commands, and the slice
that owns completion.

### Layer 1 — Contract and schema conformance

- accepted public product/ADR authority is the source of expected behavior;
- producers, consumers, storage, wire artifacts, exports, and replay are
  updated together for clean-break contract changes;
- schema, canonicalization, typed-null/unknown/omission, terminal, coverage,
  and gap behavior is exercised directly; and
- unsupported states fail or degrade exactly as declared.

### Layer 2 — Independently expected public fixtures

- expected values are pre-authored from format rules, retained bytes,
  authoritative documentation, or explicit manual adjudication independent of
  the implementation path under test;
- every positive has a meaningful negative or abstention case;
- expected-output changes require new independent evidence and review; and
- product output is never copied into an expected fixture as truth.

### Layer 3 — Model-derived, mutation, and metamorphic checks

- exercise all bounded state classes relevant to the changed slice;
- test missing, malformed, unsupported, ambiguous, and partial evidence;
- rename identities and reorder unrelated inputs without changing semantics;
- change one relevant dependency and require only dependent output to change;
- prove forbidden facts remain absent; and
- retain complete raw candidates, failures, abstentions, coverage, and gaps.

### Layer 4 — Determinism, replay, and operational safety

- clean and incremental execution agree for identical resolved inputs;
- retained replay reproduces deterministic downstream artifacts;
- output paths and writes remain confined;
- dependency/identity drift fails closed; and
- full applicable unit, contract, integration, evaluation, security, fault,
  format, and manifest checks pass.

### Layer 5 — Generalization and controlled-real evidence

- Slice 7 proves the generic mechanism across the two materially different
  accepted domains with matched negatives;
- Slice 8 runs the qualified controlled-real EVAL-0016 and EVAL-0017 packages;
- any case that changes implementation is development/validation evidence, not
  held-out evidence; and
- unevaluated taxonomy regions remain explicit gaps.

### Layer 6 — Fresh review and claim control

- every later slice receives a fresh semantic/diff review against its accepted
  plan and public authority;
- passing tests do not replace review of correctness, completeness, provenance,
  gaps, and plan drift;
- each implementation record states exactly what passed, what remains
  unsupported, and that no private held-out verdict exists; and
- no M1 or later claim uses `held-out`, `independently validated`, `reliable`,
  or equivalent language beyond the evidence actually obtained.

## Future evaluator re-entry criteria

A new evaluator plan may be proposed only when all of these are true:

1. Slice 9 has produced a stable versioned end-to-end output contract.
2. The evaluation surface is limited to user-meaningful semantic outcomes;
   internal IDs, incidental prose, and implementation-specific diagnostics
   remain public-conformance-only.
3. Every expected held-out value is authorable without product output, the
   implementation path under test, or a self-referential generated ledger.
4. An answer-free totality/authorability review passes before candidate or
   private access.
5. Public evaluator implementation, private corpus qualification, scoring, and
   closeout retain separate authority.
6. A new accepted ADR and milestone plan define protocol identity, scope,
   correction limits, contamination handling, and the claims its verdict may
   support.

The future protocol identity is not selected here. Retired `/5` must never be
reused, and this plan does not authorize `/6`.

## Work packages

### WP0 — Preserve failed evidence and establish a clean baseline

Work ID: `M1/S4.5/EVAL-CLOSEOUT/WP0`

Objective: preserve the exact inherited WP1V work before any deletion and
classify every dirty path as durable authority, failed evidence, status update,
or disposable generated output.

Required work:

- verify branch, HEAD, complete status, staged state, and full diff;
- confirm the expected starting base `cd23a96be50820326db1f1247edb11c3c86f230b`;
- distinguish the inherited WP1V changes from this owner-supplied plan file;
  this plan is not part of the failed WP1V evidence commit;
- verify the WP1V hard-stop report against the failed scripts, ledger,
  summaries, and final reviewer finding;
- parse every JSON artifact and run `git diff --check`;
- create one focused local evidence-preservation commit containing the exact
  failed WP1V state and explicit hard-stop/status records;
- label false-accepting summaries and validators as failed evidence in the
  commit message and records; and
- do not push, merge, rebase, squash, amend, or rewrite prior commits.

Exit criteria:

- the formerly dirty state is reproducible at one exact commit;
- no failed artifact is described as accepted;
- no unrelated user work is included; and
- the worktree is clean before WP1 begins.

Hard stop: if the dirty files cannot be confidently attributed to WP1V, or the
record does not match their bytes, stop for owner disposition instead of
discarding or combining them.

### WP1 — Accept evaluator deferral and migrate durable semantics

Work ID: `M1/S4.5/EVAL-CLOSEOUT/WP1`

Objective: establish the new authority before removing `/5` artifacts.

Required work:

- add and accept a new ADR that:
  - closes the current M1 held-out effort without a product verdict;
  - supersedes ADR-0030's active `/5` authorization;
  - supersedes ADR-0031 only as active `/5` model authority while preserving
    its historical decision and durable loose-availability semantics;
  - supersedes ADR-0027 and M1 plan revision `/3` only where they require a
    held-out `PASS` before Slice 5;
  - retains every answer-isolation, private default-deny, no-retry, identity,
    contamination, and provenance rule; and
  - authorizes this plan's public closeout and replacement M1 verification
    profile;
- move each durable `/5` semantic rule to its proper non-protocol product
  authority without broadening it;
- add a residual-risk entry for the missing private held-out verdict and known
  `/4` representation gap; and
- obtain an owner-authority/diff review before WP2.

Exit criteria:

- durable product behavior has no dependency on an active `/5` artifact;
- historical ADRs remain intelligible and linked;
- the new ADR states exactly which clauses it supersedes; and
- no product or evaluator implementation changed.

### WP2 — Retire `/5` and establish bounded `/4` regression use

Work ID: `M1/S4.5/EVAL-CLOSEOUT/WP2`

Objective: remove the unsafe active successor surface and make the retained
historical evaluator difficult to misuse.

Required work:

- delete the active `/5` machine artifacts listed above;
- mark the `/5` plan and records retired/unqualified while preserving their
  exact historical chronology;
- reserve the `/5` identity against reuse;
- create the `/4` bounded-regression usage document and machine profile;
- create the public wrapper/validator outside the frozen evaluator tree;
- verify 23/23 frozen-commit blob hashes, 20/20 current non-test reusable-core
  hashes, and the authorized provenance/current identities of the three
  evolving public regression tests;
- inventory retained `/2` and `/3` schemas or records, prove whether each is
  required by `/4` freeze/history, and ensure none is advertised as active or
  accepted for new execution;
- run its allowlisted public calibration and focused regression suite; and
- add tests proving that the wrapper refuses prohibited modes, identities, and
  claims.

Exit criteria:

- no active `/5` schema, model, summary, ledger, validator, or executable
  surface remains in the working tree;
- historical `/5` references are visibly historical and point to records or
  exact commits rather than missing active authority;
- the frozen commit is 23/23 byte-identical to its freeze and the current
  non-test reusable core is 20/20 byte-identical;
- the bounded-regression command passes deterministically; and
- no command can be mistaken for a current held-out product verdict.

### WP3 — Replace the M1 gate and reconcile current documentation

Work ID: `M1/S4.5/EVAL-CLOSEOUT/WP3`

Objective: make the accepted testing framework and later-slice sequencing
coherent across the repository.

Required work:

- add the M1 continuation verification profile;
- amend the M1 milestone plan and evaluator-v2 amendment;
- close Slice 4.5 with no held-out verdict and explicitly unblock Slice 5 only
  after this plan completes;
- update Slice 5-9 gates and implementation-record expectations to use the
  continuation profile;
- update at least:
  - `AGENTS.md`;
  - `docs/README.md`;
  - product requirement/scope documents where the delivery/gate meaning
    changes;
  - architecture decision index and affected ADRs;
  - evaluation strategy, case catalog, fixture guidelines, anti-overfitting
    rules, baseline amendments, and governance v2;
  - plan indexes, milestone indexes, the parent Slice 4.5 plan, predecessor
    plans, `/5` plan, work-breakdown map, and implementation records;
  - evaluator tool README and public test documentation; and
  - deferred-question/residual-risk documentation;
- distinguish everywhere among:
  - public product conformance;
  - bounded `/4` regression health;
  - private held-out evaluation;
  - evaluator qualification; and
  - product reliability/readiness claims.

Exit criteria:

- every current-state index reports the same status;
- no active language says WP1 may resume, WP2-WP4 remain next, B2 is
  authorized, Slice 5 is blocked on held-out `PASS`, `/5` is active/accepted,
  or `/4` can issue a complete current verdict;
- historical occurrences remain dated and clearly labeled rather than
  mechanically erased;
- Slice 5 is the next authorized product package only after closeout; and
- the absence of a private held-out verdict is explicit in M1 status and
  completion claims.

### WP4 — Full evaluator and documentation review

Work ID: `M1/S4.5/EVAL-CLOSEOUT/WP4`

Objective: independently prove that the retained evaluator boundary and the
new project outlook are coherent, safe, and stable.

The parent uses two fresh read-only reviewers in parallel after WP3 reaches a
clean candidate commit.

#### Reviewer A — Evaluator-boundary audit

Positive allowlist:

- accepted public product/ADR/evaluation authority;
- frozen `/4` evaluator source, schemas, historical public-test blobs,
  manifests, freeze, current public regression tests, and bounded-regression
  profile/wrapper;
- `/5` retirement and hard-stop records; and
- the candidate diff.

Required findings:

- all 23 frozen-commit blobs match the freeze, all 20 current non-test core
  files match their frozen hashes, and the three evolved public tests are
  correctly attributed and never presented as frozen qualification bytes;
- permitted regression modes remain technically meaningful;
- the known `/4` representation gap is excluded rather than normalized away;
- no `/5` active surface remains;
- no private invocation, locator, expected output, or answer-bearing artifact
  is reachable or required;
- no wrapper result can be interpreted as an overall product or held-out
  verdict; and
- public tests and commands match their documented claims.

#### Reviewer B — Documentation and plan-coherence audit

Review all tracked Markdown/JSON plan, evaluation, ADR, requirement, and index
documents. Build a machine-readable occurrence ledger for at least:

- protocol `/5` and its identities;
- ADR-0030 and ADR-0031;
- WP1/WP1R/WP1V/WP2-WP4 status;
- protocol `/4` final/current/qualified language;
- B2, C2, Stage D, and private held-out authorization;
- `held-out PASS`, `EVALUATOR_ERROR`, and product-verdict language;
- Slice 4.5 active/complete status;
- Slice 5 blocked/unblocked/next status; and
- M1 active/complete and M2/M3 transition language.

Every occurrence must be classified as current normative language, current
status, or historical record. Unclassified, contradictory, stale, or broken
references are findings.

#### Review budget

- one initial review from each reviewer;
- one consolidated correction pass by the implementer if needed; and
- one final re-review from the reviewer whose finding required correction.

The same material contradiction after correction, drift in frozen `/4`, any
private access, or any claim that silently recreates the held-out gate is a hard
stop. Do not launch replacement reviewers to obtain a different conclusion.

Exit criteria:

- both reviewers return `ACCEPT`;
- all applicable public commands and tests pass;
- all JSON parses and repository links resolve;
- `git diff --check` passes;
- no stale active-status occurrence remains; and
- review records state exact commits, paths, commands, findings, corrections,
  and prohibited-access status.

### WP5 — Closeout and Slice 5 handoff

Work ID: `M1/S4.5/EVAL-CLOSEOUT/WP5`

Objective: publish one coherent local closeout state and a bounded next-work
handoff.

Required work:

- add the evaluator-deferral acceptance record and update the append-only Slice
  4.5 implementation record;
- record exact `/4` bounded-regression identities, commands, and results;
- record `/5` retirement, removed paths, historical checkpoint, and reason;
- record both independent review attestations and correction usage;
- run the full applicable M1 public verification suite defined by the new
  profile;
- verify link, identity, protected-path, private-path, and status-occurrence
  checks;
- create one focused closeout commit after the reviewed candidate commit;
- leave the branch clean and do not push; and
- identify `M1/S5` as the next eligible product work, subject to its normal
  preflight and accepted plan.

Accepted closeout status language from 2026-08-07:

```text
Slice 4 public conformance: passed for its exact frozen candidate and scope.
Protocol /4: frozen historical evaluator; bounded public regression use only.
Protocol /5: retired unqualified; no implementation or verdict.
Private held-out evaluation: deferred; no valid current product verdict.
Slice 4.5: closed by owner disposition with explicit residual risk.
Slice 5: eligible to begin under the M1 continuation verification profile.
M1: active.
```

Later Slice 5 status is maintained only in
[current project state](../../../../../current-state.md).

## Required verification inventory

Implementation must discover and use the repository's actual commands rather
than assume this list is exhaustive. At minimum run:

```powershell
git status --short
git diff --check
dotnet restore Infinium.sln --locked-mode --nologo
dotnet build Infinium.sln -c Release --no-restore --nologo
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Unit"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Contract"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Integration"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Evaluation"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Security"
dotnet test Infinium.sln -c Release --no-build --nologo --filter "Category=M1Fault"
dotnet test Infinium.sln -c Release --no-build --nologo
dotnet format Infinium.sln --verify-no-changes --no-restore --verbosity minimal
powershell -NoProfile -ExecutionPolicy Bypass -File eng/update-dependency-manifest.ps1 -Check
```

Also run:

- the new bounded `/4` regression command under each supported runtime and
  repeat it for deterministic comparison;
- the repository documentation-link checker, or create a narrowly scoped one
  if none exists;
- strict JSON parsing for every changed JSON file;
- a tracked-file scan for private paths, answer-bearing content, and removed
  `/5` active identities;
- a tracked-text status ledger for every phrase listed in WP4; and
- exact freeze-manifest comparison for every retained `/4` file.

The parent must report exact passed, failed, and skipped counts. A broad command
failure must be scoped before it is classified; no unbounded recursive artifact
enumeration or prohibited build-output inspection is authorized.

## Orchestration and execution structure

### Selected model

Execute this plan through **one new, fresh parent orchestrator** in a separate
Codex task. Do not continue implementation in the task that authored this plan,
and do not hand every work package to unrelated top-level tasks.

The fresh parent owns the branch, inherited dirty-state custody, sequencing,
package acceptance, commits, correction budgets, final status, and complete
handoff. That continuity is necessary because WP0 must preserve and classify
one existing uncommitted state, WP1 must establish authority before deletion,
and WP2/WP3 touch overlapping evaluator and status documents. Independent
agents without one integrating parent would recreate the drift this closeout
is intended to remove.

The parent delegates bounded implementation and review roles where fresh
context adds value. Subagents may not recursively delegate. All agents share
the same working tree, so write-capable roles run sequentially. Only the two
read-only WP4 reviews may run in parallel.

### Parent responsibilities

The parent must:

- read the complete required authority chain itself;
- perform the initial branch/status/diff inventory itself;
- own WP0 and its evidence-preservation commit;
- own WP1's authority disposition and semantic-migration decisions, using a
  read-only subagent only for a bounded cross-check if useful;
- give every implementer a positive path and authority allowlist;
- review each returned diff semantically and mechanically before accepting it;
- run or independently reproduce package verification rather than accepting a
  subagent's summary alone;
- route at most one named correction back to the responsible implementer at a
  package gate;
- maintain the correction ledger and stop when any budget is exhausted;
- prevent parallel writes or work on later packages before their predecessor
  gate passes;
- perform WP5 integration, final verification, commits, and handoff itself;
  and
- continue automatically between clean package gates without requesting an
  owner checkpoint unless a listed hard stop or new authority choice occurs.

### Delegated roles

#### Implementer A — WP2 evaluator retirement and bounded `/4` tooling

Spawn one fresh implementation subagent only after WP0 and WP1 are accepted
and committed. Its scope is limited to:

- the exact active `/5` machine artifacts authorized for removal;
- frozen `/4` inventory inspection without modification;
- the bounded-regression usage contract, machine profile, wrapper, and focused
  tests; and
- directly required evaluator-tool documentation.

The parent reviews its diff and reruns its focused checks. One correction
return to Implementer A is permitted. A second material finding stops the plan.

#### Implementer B — WP3 verification profile and documentation reconciliation

Spawn a separate fresh implementation subagent only after WP2 is accepted and
committed. Its scope is limited to:

- the M1 continuation verification profile;
- milestone/slice sequencing and gate changes;
- ADR, evaluation, governance, case, plan, implementation-record, index, and
  residual-risk status reconciliation; and
- the machine-readable current/historical occurrence ledger used by WP4.

The parent reviews the complete repository-wide status diff and reruns link,
JSON, and occurrence checks. One correction return to Implementer B is
permitted. A second material finding stops the plan.

#### Reviewer A and Reviewer B — WP4 independent audits

After WP3 is accepted at a clean candidate commit, spawn the two WP4 reviewers
in parallel with read-only authority and the positive allowlists defined in
WP4:

- Reviewer A audits the retained evaluator boundary and executable claims.
- Reviewer B audits documentation, plan, authority, and status coherence.

Neither reviewer may edit files, repair findings, inspect private material, or
delegate. They return `ACCEPT` or `REJECT` with exact evidence.

### Correction routing

If either WP4 reviewer returns `REJECT`, the parent consolidates all findings
before any edit. There is one shared final correction pass:

- evaluator/tool findings return to Implementer A;
- documentation/status findings return to Implementer B; and
- cross-cutting authority or integration findings are corrected by the parent.

The responsible agents may work sequentially within that single correction
pass; they may not begin independent iterative loops. Only the reviewer who
raised a corrected finding performs its final read-only re-review. Any material
finding after that re-review is the plan's terminal hard stop.

### Execution sequence and commits

1. **Parent — WP0:** inventory and preserve the inherited failed WP1V state;
   parent review; focused evidence commit.
2. **Parent — WP1:** accept the disposition ADR and migrate durable semantics;
   optional bounded read-only cross-check; parent review; focused authority
   commit.
3. **Implementer A — WP2:** retire active `/5` artifacts and implement bounded
   `/4` regression use; parent review and at most one correction; focused WP2
   commit.
4. **Implementer B — WP3:** establish the replacement M1 verification profile
   and reconcile documentation; parent review and at most one correction;
   focused WP3 candidate commit.
5. **Reviewers A and B — WP4:** parallel read-only audits; at most one shared
   correction pass and targeted re-review; focused correction commit only when
   a correction was necessary.
6. **Parent — WP5:** full applicable verification, closeout records, final
   status review, focused closeout commit, and Slice 5 handoff.

This uses one user-facing orchestrator task, two sequential implementation
subagents, and two parallel read-only reviewers. It does not require a new
top-level task or owner prompt between normal packages.

Do not amend, squash, rebase, force-push, or push. No work package may silently
continue past a failed exit criterion, and no subagent may create a commit
unless the parent explicitly assigns that commit boundary.

## Global hard stops

Stop for the owner if:

- any required Git blob at frozen evaluator commit
  `3693d19563c636cd2879804633ca4ce52448d2c1` is unavailable or differs from
  the immutable freeze manifest;
- the frozen commit is unavailable;
- any current non-test evaluator runtime/schema file claimed as reusable `/4`
  core differs from its frozen hash, is missing, or has an extra unprofiled
  runtime dependency;
- a current public regression test is represented as original frozen
  qualification evidence, or its changed bytes cannot be attributed to
  authorized public work;
- removing `/5` would erase a product semantic not yet migrated to accepted
  non-protocol authority;
- any retained `/5` executable/schema/model surface would still appear active;
- a proposed `/4` regression use reaches its known representation gap;
- the replacement testing profile derives expected truth from product output;
- private material, paths, locators, answers, or raw results are accessed;
- the change implies a private, held-out, reliability, or M1-completion claim
  not supported by the retained evidence;
- later-slice work begins before closeout acceptance;
- the same material review finding survives the one correction pass; or
- the worktree contains unattributed or unrelated user changes that cannot be
  safely isolated.

## Completion criteria

This plan is complete only when:

- failed WP1V evidence is preserved at an exact commit;
- the new evaluator-deferral ADR is accepted;
- durable semantics no longer depend on `/5` artifacts;
- `/5` is removed from the active machine surface and clearly retired in
  historical records;
- `/4` remains frozen and passes its bounded public regression profile;
- the M1 continuation verification profile is accepted and linked from later
  slices;
- all current documentation agrees on evaluator, Slice 4.5, Slice 5, and M1
  status;
- both fresh reviews accept after no more than one consolidated correction;
- full applicable public verification passes without waiver;
- no private work or product verdict occurred;
- the final branch is clean with focused local commits and no push; and
- Slice 5 has one clear, bounded, copy-pasteable implementation handoff.
