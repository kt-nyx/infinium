# M1 Slice 6 lean continuation plan

Status: Accepted

Disposition: Owner-authorized successor execution model; successor WP9 attempt 2 is an ambiguous consumed start pending append-only evidence-supplement and offline-correction acceptance before any fresh authority

Owner: Project owner

Prepared: 2026-08-17

Accepted: 2026-08-17

Accepted by: Project owner

Last reviewed: 2026-08-17

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

## 7. `M1/S6/C2` — One bounded live campaign

### 2026-08-20 owner-authorized successor amendment

The original v4 C2B execution is terminal and immutable at event hash
`282c97151dbdcd354288b67f96c4b01d7f7ef43b1bbfb9f247cbd9b510506de9`.
The following clean-break rules supersede Sections 7.1-7.5 only where those
sections say one start per stage, exactly three total starts, USD 1.34
aggregate, no fourth request, or permanent stop after any failed stage:

- each v5 runtime authority permits one possible provider start and no retry;
- the campaign permits serial fresh attempts, never an automatic loop;
- each stage has at most five lineage starts; terminal WP9 consumed ordinal 1,
  leaving at most four successor WP9 starts, while WP10 and WP11 each have at
  most five;
- terminal USD 0.14 plus successor cumulative reservations/spend may not
  exceed USD 10.00; the existing per-attempt USD 0.14/USD 0.60/USD 0.60
  ceilings are unchanged unless exact technical necessity is independently
  reviewed and rebound;
- additive migration `M1-S6-SUCCESSOR-0007` may advance only the exact retained
  product-state root to schema 7/storage `1.6.0`; it preserves historical rows
  and permits the same frozen request only through a fresh transport operation
  and attempt identity;
- every reviewed runtime candidate binds both the immutable snapshot-origin
  digest and an exact read-only logical checkpoint digest of the then-current
  SQLite state plus every retained non-database file; the coordinator
  recomputes that checkpoint immediately before admission so between-attempt
  state drift cannot inherit authority;
- a failed or ambiguous attempt retains its response or zero-byte diagnostic,
  unresolved hold when needed, and fresh identities. Another attempt requires
  accepted failure evidence plus offline diagnosis/correction and independent
  review;
- the first structurally valid WP10 or WP11 response is authoritative before
  semantic comparison and permanently stops provider calls for that stage;
  no prompt, product, fixture, or frozen-oracle tuning and no choice among
  multiple valid semantic outputs is permitted; and
- the retained credential is accessible only through the exact masked-helper
  `CredReadW -> CredFree` boundary in the successor credential-access
  authority. Exposure, enumeration, replacement, write, and delete remain
  prohibited.

All v4 schemas, manifests, ledger bytes, evidence, and historical statements
remain unchanged and historical. “One-shot stage” in the original text now
means “one possible provider start per independently reviewed and durably
admitted fresh attempt.”

### 7.1 Objective and authority

Execute the exact Slice 6 credential enrollment and three provider stages once
each under a fresh, closed, owner-accepted campaign. C2 preserves the old R4-R7
effect ceilings and semantic obligations while eliminating separate software-
candidate freeze/bind cycles between stages.

C2 is not opened by C1 alone. Before any helper/readiness/native operation, the
owner must accept the exact committed C2 authority package binding:

- fresh campaign, credential, profile/generation, target fingerprint, and stage
  identities;
- exact accepted C1 implementation and build identities;
- exact request profile, package/input/template/oracle bindings;
- current capability, price, account/billing intent, budgets, deadlines, and
  expiries;
- one enrollment and exactly three sequential provider-stage ceilings;
- durable ledger transition grammar and safety-identifier contract;
- no retry, fallback, parallel dispatch, counter reset, ceiling transfer, or
  fourth request; and
- exact effect commands and retained evidence locations.

### 7.2 Preserved aggregate ceilings

Unless a future accepted authority package lowers them, C2 may not exceed:

| Stage | Starts | Request bytes | Input tokens | Output tokens | Raw response bytes | Deadline | Maximum cost |
|---|---:|---:|---:|---:|---:|---:|---:|
| WP9 qualification | 1 | 16,384 | 20,480 | 256 | 262,144 | 60 s | USD 0.14 |
| WP10 source claim | 1 | 65,536 | 73,728 | 4,096 | 1,048,576 | 120 s | USD 0.60 |
| WP11 investigation | 1 | 65,536 | 73,728 | 4,096 | 1,048,576 | 120 s | USD 0.60 |
| Aggregate | 3 | 147,456 | 167,936 | 8,448 | 2,359,296 | - | USD 1.34 |

The final aggregate native maximum remains `CredWriteW=1`, `CredReadW=5`,
`CredDeleteW=0`, `CredFree=4`, total 10. Every successful credential read is
paired with its allocation's free. The fresh authority may lower but never
raise these bounds without a separate owner/architecture decision.

### 7.3 C2 stage boundaries

#### C2A — masked credential enrollment

Maps old R4. Perform one helper-owned masked paste and exact-target enrollment
under the accepted credential manifest. Retain sanitized evidence and exact
effect counts. Ambiguity, expiry, secret exposure, target mismatch, or helper
drift stops C2. Offline evidence-format correction is allowed only when the
single effect is unambiguous; enrollment is never repeated under the campaign.

#### C2B — WP9 transport qualification

Maps old R5/WP9. After accepted C2A evidence, materialize, review, durably admit,
and execute one exact non-semantic qualification request. Persist request,
response, headers, usage, settlement, recovery, and replay. A known or possible
start consumes WP9. No semantic reuse of the qualification response is allowed.

#### C2C — WP10 source-claim extraction

Maps old R6/WP10. After accepted WP9 evidence, materialize, review, durably
admit, and execute one exact WP10 request over the frozen v2 package. Retain all
nine semantic states and exactly one admitted artifact/application. A known or
possible start consumes WP10. The frozen oracle is not changed after product
comparison.

#### C2D — WP11 candidate investigation

Maps the live portion of old R7/WP11. After accepted WP10 evidence, reopen the
exact persisted WP10 chain, materialize, review, durably admit, and execute one
exact WP11 request containing the accepted positive and matched negative.
Persist candidate results, settlement, recovery, and replay. A known or
possible start consumes WP11. No fourth request exists.

### 7.4 Review and verification cadence

Each effect keeps its exact pre-effect review, durable admission, one-start,
expiry, settlement, evidence, and predecessor gate. That strictness is not
reduced.

Before each provider stage, compare the exact accepted local official-document
snapshot, request profile, model, capability, and price facts with the stage
manifest. Drift or the need for a public refresh stops before possible start;
any internet refresh remains a separately authorized research action. After
each credential/provider effect, its sanitized exact evidence must receive
fresh independent acceptance before the next stage manifest is materialized.

After an effect, run focused evidence, persistence, replay, canary, counter,
and semantic/provenance review. Do not rerun the complete repository floor
between stages when tracked implementation bytes are unchanged. If tracked
implementation must change, stop before the next effect, correct and review the
same C2 software candidate offline, run affected checks, and run the complete
floor only when required to establish a new exact executable candidate. A
started stage is never retried or replaced by that correction.

### 7.5 Stops

C2 stops the affected campaign path for:

- secret or answer-isolation breach;
- expired or mismatched authority;
- native ambiguity or unknown cleanup;
- known or possible provider start with any need for another response;
- ceiling, counter, settlement, ledger, safety-identifier, or predecessor
  inconsistency;
- material provider/profile/capability/price drift; or
- a required change to product meaning, accepted architecture, or effect
  authority.

Ordinary local evidence, codec, validator, documentation, or replay defects are
corrected offline when retained evidence is sufficient and no new effect is
needed.

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
- prove the actual per-stage start totals are within five (including terminal
  WP9), settlement/recovery, canaries, no retry, and no start after a first
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
