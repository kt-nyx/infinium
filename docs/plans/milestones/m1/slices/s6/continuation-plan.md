# M1 Slice 6 lean continuation plan
Status: Accepted

Disposition: Owner-authorized successor execution model, as amended by the 2026-08-21 hard-budget continuation after accepted attempt-2 supplement and correction

Owner: Project owner

Prepared: 2026-08-17

Accepted: 2026-08-17

Accepted by: Project owner

Last reviewed: 2026-08-21

Work ID: `M1/S6-CONTINUATION`

Plan identity: `infinium.plan.m1-s6.lean-continuation/1.0.0`

Plan ID: `65d8b7cd-759a-46c8-930d-dae4f50d2561`

Accepted implementation baseline: R2 implementation
`67ca34d6de162ad64f05fbe88972105745d3e831` and handoff
`8c25ca7274c394e41953a0b076010c26f6ffa97e`

Accepted proposal commit: `2c82365fd853cb2021f1772d6c572ee9fa006d01`

Accepted proposal SHA-256:
`57d9a3b25201bf55281cad02c9b8a3e458639ec10d1e465cdbad85f532c464af`

Parent authority: accepted M1 plan, accepted Slice 6 plan, accepted Slice 6
remainder plan through R2, accepted product requirements and ADRs, accepted M1
continuation verification profile, and the accepted
[M1 process amendment](../../amendments/process-continuation.md)

## 1. Purpose and current authority

The owner-authorized
[hard-budget continuation amendment](m1-slice6-development-campaign-amendment.v2.json)
supersedes this plan's finite five-start ceiling, repeated-defect stop, fixed
per-attempt policy cost ceiling, and cumulative-reservation budget burn. It
does not change accepted product meaning. The exact USD 10.00 aggregate hard
limit, sequential fresh identities, one possible start per attempt, no
automatic retry, durable settlement, first-structurally-valid semantic latch,
credential isolation, answer isolation, and C3 owner-acceptance boundary remain
unchanged. Released pre-start reservations are reusable; settled, unresolved,
and currently outstanding exposure remains conservatively committed.

This plan replaces the remaining Slice 6 orchestration model after accepted R2
with three outcome packages. It preserves every uncompleted R3-R7 obligation
while applying the process amendment's coherent-candidate, proportional-
verification, consolidated-review, and bind-once lifecycle.

The owner accepted this plan and the process amendment at their exact proposal
commit on 2026-08-17. That acceptance grants only the documentation activation
package. Until the owner accepts that package's exact commit and its
`docs/current-state.md` handoff:

- C1 is not open;
- the dormant R3-R7 authority remains unexercised;
- no old or future campaign is executable; and
- no credential, helper, UI, native, DNS/network, provider, billable, private,
  archive, destructive, or push operation is admitted.

## 2. Supersession boundary

On activation, this plan supersedes only the post-R2 execution mechanics,
candidate lifecycle, authority timing, and package decomposition of R3-R7 in
`remainder-plan.md`. It does not rewrite or invalidate:

- the accepted R1 fixture/oracle/package authority and its exact evidence;
- the accepted R2 product, persistence, replay, provenance, recovery, semantic
  review, and offline rehearsal evidence;
- the accepted Slice 6 product contracts, request profile, ceilings, stage
  order, persistence requirements, semantic outcomes, cases, or claim limits;
- WP9, WP10, or WP11 identities and their prerequisite relationship;
- credential/helper isolation, answer isolation, product/evaluator separation,
  provenance, durable settlement, expiry, or no-retry rules; or
- the final owner decision required to accept Slice 6.

The accepted remainder plan and implementation record remain immutable
historical authority/evidence for the work executed under them. They are not
edited in place.

## 3. Retired unexercised campaign authority

The accepted documentation-only activation of this plan retires the
unexercised successor campaign
`infinium.m1-s6.finite-live-campaign/51b9dba6-aca3-41d7-82d1-afd805e33e66`
and pre-effect credential authorization ID
`infinium.m1-s6.wp9.production-profile-authorization/09b8e309-ead8-441e-8307-5a4a1a2c43d5`.

No effect occurred under those identities. They become reserved historical
identities and may not be executed, resumed, rolled over, or reused. Their
former expiries, bindings, and dormant conditional authority do not transfer
to C2.

C2 must materialize a fresh campaign ID, credential authorization ID, stage
manifest IDs, exact candidate binding, current expiries, and current official-
profile/capability/price evidence after C1 is accepted. The owner must accept
the exact fresh pre-effect authority package. Acceptance of this plan does not
pre-accept unknown C2 bytes.

## 4. Boundaries preserved across C1-C3

### 4.1 Expected truth and product authority

- The accepted R1 v2 inputs and oracles remain exact and immutable.
- Product output never authors, changes, or selects expected truth.
- Product runtime never loads oracle bytes or evaluator code.
- WP10 must retain all nine semantic states and admit exactly the one supported
  and applicable proposal required by the frozen oracle.
- WP11 positive must consume the exact persisted WP10 acquisition, proposal,
  admission, artifact, application, payload bytes, and digests.
- WP11 negative uses only its independently frozen host-evidence root and does
  not fabricate a second WP10 claim.
- Composed provenance requires qualification -> WP10 -> WP11, records every
  lineage attempt, and proves no start after each stage's authoritative latch.

### 4.2 Persistence, replay, and invalid states

The authoritative SQLite graph, coordinator ownership, atomic publication,
reopen/replay equality, settlement/recovery, and corruption/invalid-state
coverage accepted in R2 remain mandatory. No sidecar, Git marker, output file,
or test-only projection replaces durable product state.

### 4.3 Credential and external effects

Every external effect requires an exact typed manifest, durable admission, an
unexpired ceiling, exact predecessor evidence, and immediate pre-effect
revalidation. A known or possible provider start consumes its stage and may
never be retried. Secret, target, credential, provider, budget, process,
network, canary, and retention rules remain unchanged.

### 4.4 Runtime authority

Git may bind the exact reviewed candidate referenced by a typed manifest.
Neither product runtime nor effect-execution scripts may discover effect
authority from branch/HEAD state, commit subjects, Git log order, pickaxe,
line attribution, or historical marker messages. Durable typed authority and
coordinator-owned state decide runtime admission.

## 5. Outcome package sequence

```text
accepted R2
  -> C1 effect-free readiness closure
  -> owner-accepted fresh C2 effect authority
  -> C2A credential enrollment
  -> C2B WP9 qualification
  -> C2C WP10 source-claim extraction
  -> C2D WP11 candidate investigation
  -> C3 retained-evidence closeout
  -> owner Slice 6 acceptance decision
```

Only one repository writer operates at a time. Review roles are bounded and
read-only. No package advances from a failed or ambiguous predecessor.

## 6. `M1/S6/C1` — Effect-free readiness closure

### 6.1 Objective

Produce and accept one coherent, effect-free implementation candidate ready for
a separately authorized campaign. C1 absorbs the necessary non-live outcomes
of old R3 without inheriting its bind/freeze churn or pre-authorizing a live
campaign.

### 6.2 Exact inputs

- R2 implementation `67ca34d6de162ad64f05fbe88972105745d3e831`;
- R2 handoff `8c25ca7274c394e41953a0b076010c26f6ffa97e`;
- accepted R1 packages, oracles, schemas, and registry;
- accepted Slice 6 contracts, profile, limits, and R2 runtime seams;
- this accepted plan and process amendment after activation; and
- the then-current local official-document snapshot, without a new web refresh
  unless separately authorized as research.

### 6.3 Deliverables

C1 must demonstrate:

- complete effect-free readiness of the R2 WP9-to-WP11 path;
- typed authority and durable-ledger handling without materializing executable
  credential or provider-stage authority;
- closed safety-identifier generation, projection, atomic use-latch, reopen,
  forbidden-input, corruption, and no-regeneration behavior;
- exact public package/registry/oracle preservation and product/evaluator
  answer isolation;
- complete persistence, reload, replay, invalid-state, settlement, recovery,
  expiry, counter, and ceiling behavior;
- one end-to-end fake-store/literal-loopback rehearsal through qualification,
  WP10, WP11, composed provenance, and no-fourth-call closure;
- production/effect scripts that consume typed manifest plus durable state and
  do not infer runtime authority from Git history; and
- an owner-readable readiness report naming claims, gaps, remaining authority,
  and the exact next gate.

C1 must not materialize an active campaign, credential authorization manifest,
stage request, stage review/admission marker, production profile, safety-use
latch for a real campaign, or effect evidence.

### 6.4 Candidate and verification cadence

Implementation and corrections remain one C1 working candidate. During work,
run focused tests for the changed contract, validator, persistence, replay,
security, fixture, or documentation surface. Do not bind a candidate or append
rejected-candidate chronology after each correction.

When the vertical path is coherent and focused checks pass, perform one
consolidated review covering:

- product/semantic correctness and R1/R2 preservation;
- persistence/replay/recovery and invalid states;
- fixture/oracle independence and provenance;
- security, credential, budget, process, and effect denial;
- runtime authority and Git non-authority;
- scope, claims, documentation, and the complete diff.

Batch and correct all `MUST-FIX / CORRECT` findings on the same candidate.
Focused re-review is sufficient unless meaning, authority, immutable expected
truth, or scope changes materially.

Only after the package is review-ready run the complete accepted non-live floor
and exact C1 gate. A failed full-floor run is diagnostic, not a candidate bind.
Correct the same candidate, rerun affected checks/review, then run a new final
floor. Retain only the passing exact-candidate floor as acceptance evidence.

### 6.5 Acceptance and handoff

C1 acceptance requires:

- exact implementation commit and clean worktree;
- passing focused checks and consolidated review with zero must-fix findings;
- one passing complete continuation-profile floor on the exact commit;
- passing inert campaign rehearsal and zero-effect receipt;
- zero credential/helper/UI/native/DNS/network/provider/billable effects;
- no private/archive access and no push; and
- a concise accepted handoff that does not duplicate failed-candidate history.

C1 acceptance does not open an effect. It makes preparation and owner review of
the fresh C2 authority package eligible.

#### 6.5.1 Proposed owner amendment — C1 remains implementation-active through C2

On 2026-08-19 the owner asked the project to consider leaving C1 unfrozen until
C2 is complete. The recovery package therefore proposes treating C1 acceptance
as an accepted readiness baseline, not a contract or implementation freeze.
This proposal does not become process authority until the owner accepts its
exact package bytes. If accepted, real C2 integration evidence may correct the
C1-owned producer, consumer, persistence, validator, and evidence path on one
active vertical candidate, provided that:

- every correction remains offline until its exact implementation and
  executable bytes are reviewed and rebound by a fresh typed authority;
- prior manifests, runtime authorities, ledger events, and effect evidence stay
  immutable and are never silently reinterpreted, retried, or reused;
- a terminal campaign may advance only through a separately owner-accepted,
  append-only, zero-effect recovery authority that binds its exact terminal and
  success evidence;
- external-effect ceilings, predecessor gates, no-retry/no-fourth-call rules,
  answer isolation, and provider/evaluator separation are not broadened; and
- the final C1 implementation binding is established only after C2 completes,
  before C3 closeout and final Slice 6 contract-maturity review.

If accepted, this amendment removes premature implementation-freeze churn. It does not make
C2 self-authorizing, erase a terminal event, or permit a helper, credential,
network, provider, or billable operation without the exact existing gates.

## 7. `M1/S6/C2` — Sequential hard-budget continuation

### 2026-08-21 owner-authorized clean-break supersession

This section supersedes every earlier Slice 6 execution clause that imposed
exactly three total starts, five lineage starts, a fourth-request prohibition,
per-stage call counts, per-attempt USD 0.14/USD 0.60 ceilings, cumulative-
reservation budget burn, or a terminal stop for an ordinary failed or ambiguous
attempt. Those clauses remain historical descriptions of v4/v5 authority only
and are not executable restrictions for successor v6.

The original v4 C2B event and successor ledger v2 through sequence 8 remain
immutable. Their conservative committed exposure is USD 0.25008. Successor v6
imports that exact lineage into a new ledger v3 and has USD 9.74992 remaining
under the single Slice 6 USD 10.00 hard limit.

### 7.1 Active objective and authority

Complete WP9, WP10, and WP11 through serial fresh attempts. Each admitted
attempt permits exactly one possible provider start, one DNS resolution, no
automatic retry, and no parallel call. Failed, truncated, transport-error,
provider-error, structurally invalid, schema-invalid, timestamp-invalid, or
pre-effect attempts retain conservative evidence and return to diagnosis,
correction, review, and an entirely fresh attempt identity.

There is no per-stage start count, attempt count, or per-attempt policy-cost
ceiling. The exact price-derived reservation for each fresh attempt must fit
the aggregate remaining committed budget. A pre-start release is reusable;
cumulative reservations are telemetry and are never spend. Committed exposure
is historical committed plus settled plus unresolved plus outstanding.

The first structurally valid WP10 or WP11 result becomes permanent stage
authority before semantic comparison. No prompt, product, fixture, oracle, or
answer tuning and no selection among multiple valid semantic results is
permitted.

### 7.2 Active technical bounds

A stage-v6 manifest may adjust request bytes, proved input tokens, output
tokens, raw-response bytes, timeout, and exact reservation within the accepted
provider snapshot and active helper/platform feasibility. Current outer
feasibility is 1,000,000 request bytes inside the successor-only 1,100,000-byte
private protobuf message ceiling, leaving 100,000 bytes for framing metadata
and identities; 922,000 input tokens; 128,000 output tokens; 1,048,576 raw
response bytes; a separate 4,194,304-byte staged-envelope ceiling for the raw
response plus sanitized receipt/schema metadata; and 900 seconds. The request's exact
price-derived worst case must equal its durable reservation and fit the USD
9.74992 remaining aggregate at admission. These are technical feasibility
bounds, not lower owner budgets or fixed attempt profiles.

Credential access remains the masked-helper exact
`CredReadW -> CredFree` boundary. Credential exposure, enumeration,
replacement, write, and delete remain prohibited.

### 7.3 C2 stage boundaries

C2B completes non-semantic WP9 transport qualification. Independently accepted
WP9 evidence opens C2C, which executes the frozen WP10 source-claim package.
The first accepted WP10 result opens C2D, which executes the frozen WP11
candidate-investigation package. Every attempt binds the current product-state
checkpoint and current ledger tip. An ordinary failure does not close the
stage; a first structurally valid result does.

### 7.4 Review and verification cadence

Each effect retains exact pre-effect review, durable SQLite and ledger
admission, one-start latching, credential isolation, response/evidence
retention, settlement or conservative unresolved accounting, and independent
evidence acceptance. Focused checks are used while correcting a candidate;
consolidated changed-surface review and the complete accepted verification
floor run at meaningful stable boundaries. Rebinding is required for a changed
effect candidate, not after every local correction.

### 7.5 Active stops

Stop only when the aggregate API hard budget is exhausted before a viable
result; a secret or private-answer breach occurs; trustworthy retained evidence
can no longer be preserved; completion requires changing accepted product
meaning outside Slice 6; or C3 is complete and owner-ready. Authority, schema,
timestamp, manifest, provider, evidence, replay, and review defects are
ordinary development work unless they cause one of those conditions.

## 8. `M1/S6/C3` — Retained-evidence closeout

### 8.1 Objective

Close Slice 6 from retained evidence without another credential or provider
effect. C3 absorbs the offline composition, accumulated regression, contract-
maturity, documentation, and owner-handoff outcomes of old R7.

### 8.2 Deliverables

- independently reopen the campaign ledger and authoritative SQLite store;
- validate the exact credential, WP9, WP10, and WP11 evidence and acceptance
  transitions;
- replay retained raw responses and headers with network disabled;
- verify the WP10 admitted chain and WP11 positive/negative consumption;
- assemble composed provenance with qualification marked non-semantic;
- bind frozen ledger v2 and all inherited attempt-2 evidence to the complete
  ledger-v3 chronology; prove exact committed accounting, settlement/recovery,
  canaries, no automatic retry or parallel call, and no start after a first
  authoritative stage response;
- report refusals, unsupported modes, coverage, gaps, and claim limits;
- run the final accumulated Slice 6 regression and continuation-profile floor;
- review contract maturity and freeze only identities with complete producer,
  consumer, persistence, wire/query, output, replay, invalid-state, and fixture
  evidence; and
- update the implementation record, current state, and compact navigation for
  the owner Slice 6 acceptance decision.

### 8.3 Candidate and review cadence

C3 uses one coherent closeout candidate. Run focused retained-evidence and
documentation checks while assembling it, then one consolidated semantic,
provenance, persistence, security/effect, claim, contract-maturity, and diff
review. Correct findings on the same candidate. Run one final complete floor
after review readiness. Failed attempts remain diagnostic rather than becoming
new closeout bindings.

### 8.4 Completion boundary

C3 review may produce an owner-ready recommendation but may not accept Slice 6
on the owner's behalf. Final owner acceptance is required before Slice 6-owned
contracts become `Slice-frozen` and Slice 7 planning becomes eligible. No Slice
7 implementation starts automatically.

## 9. Obligation mapping

| Accepted remainder obligation | Lean owner |
|---|---|
| R3 coherent R1-R2 implementation candidate | C1 |
| R3 safety-identifier, typed authority, expiry, counter, and inert rehearsal proof | C1, with exact fresh bindings materialized in C2 |
| R3 complete non-live floor and readiness review | C1 |
| R3 campaign/credential identity materialization | C2 pre-effect authority package after C1 |
| R4 masked credential enrollment and sanitized evidence | C2A |
| R5/WP9 one transport qualification, settlement, replay, and evidence acceptance | C2B |
| R6/WP10 one source-claim extraction and exact-one admitted chain | C2C |
| R7/WP11 one positive/matched-negative investigation | C2D |
| R7 composed provenance, replay, regression, contract freeze, and documentation | C3 |
| Final Slice 6 owner acceptance | Owner checkpoint after C3 |

All accepted Slice 6 requirements, cases, profile fields, effect ceilings,
credential/native bounds, settlement rules, and no-private-verdict claims remain
owned. No obligation is removed merely because its orchestration label changes.

## 10. Activation and next handoff

The owner's acceptance of the proposal authorizes only this documentation
activation package.
That package must:

1. mark this plan and the M1 process amendment accepted with the exact owner
   decision;
2. update `AGENTS.md`, execution policy, M1 plan/navigation, continuation
   profile, Slice 6 navigation, and current state consistently;
3. record retirement-without-execution of the old successor campaign and
   credential authorization identities;
4. preserve the accepted remainder plan and Slice 6 record unchanged;
5. pass documentation, link, strict-JSON, status/claim, changed-path, and diff
   validation; and
6. receive fresh review before `current-state.md` opens C1.

No acceptance statement for the plan or activation package supplies a
credential, campaign, provider-stage manifest, API key, or external-effect
authority.
