# Current project state

Status: Accepted
Disposition: Replacement C2A credential write/read-back succeeded but its ledger terminalized on a post-success validator mismatch; offline correction and exact recovery-authority preparation are active; keeping C1 implementation-active through C2 is proposed but not accepted; no provider effect authorized

Last reviewed: 2026-08-19
Owner: Project owner

This is the only document that states the live milestone, slice, work package,
and evaluator handoff. Product documents and accepted ADRs define product
meaning; accepted plans define scope; implementation records preserve detailed
chronology and evidence.

## Active handoff

| Field | Current value |
|---|---|
| Milestone | `M1` - active |
| Active slice | `M1/S6` - implementation-active |
| Accepted implementation baseline | R1 candidate `fcd17cd6db98019fc9e5253d5167d2487862671c`; R2 implementation `67ca34d6de162ad64f05fbe88972105745d3e831`; R2 handoff `8c25ca7274c394e41953a0b076010c26f6ffa97e` |
| Accepted R2 evidence | Final `CampaignV2NonLive` receipt SHA-256 `bb9455c293b3049c1561cf9a15322c0b6b79502365097b51e2c92e4014e5019d`; all credential, native, network, provider, and billable counts were zero |
| Accepted process proposal | Commit `2c82365fd853cb2021f1772d6c572ee9fa006d01`; process-amendment SHA-256 `5d9aff4226f93ff73025573e056080530d23f66e5cb9cc92efddfd78655acc9f`; lean-plan SHA-256 `57d9a3b25201bf55281cad02c9b8a3e458639ec10d1e465cdbad85f532c464af` |
| Accepted C1 outcome | C1 effect-free readiness closure is accepted. Typed runtime authority, durable ledger binding, safety-state closure, exact R1/R2 preservation, and the fake-store/literal-loopback WP9-to-WP11 rehearsal passed without an external effect. A proposed recovery-package amendment would keep C1 implementation-active through C2; it is not current authority until its exact bytes are owner-accepted. |
| C1.1 implementation | Commit `9aea07380a3d3cc2a6f70be6d32907a96e7720da` added fresh v3 coordinator consumers and schemas while retaining effect-free v2 rehearsal compatibility, but the compiled credential helper still hard-coded the obsolete v2 production-enrollment contract. The terminal C2A attempt exposed that incomplete vertical closure before UI or native access. |
| Terminal C2A evidence | Under owner-accepted package commit `926d6a49a37b6c465cb706cdebbfbf8b98b32c61`, exactly one helper process launched and rejected the v3 manifest at `manifest-validation`. `ManualUiAttempted=false`; `CredWriteW`, `CredReadW`, `CredDeleteW`, `CredFree`, DNS, network, provider, and billable counts were all zero. Ledger event 5 terminalized the campaign with final event hash `bb23edb886b68a90b2bb74f74e9b77cb021232e2d5b2633b33f62c82e3a576d8`; retry and reuse are prohibited. |
| C1.2 implementation | Commit `356ce66c18bb67d5b1de8815970e04bf88195a86` closes the v4 helper/coordinator authority seam, rejects every dormant v2 and terminal v3 identity before UI/native execution, preserves known-zero pre-UI facts, passes the exact effect-free readiness floor, and binds a reproducible 126-file Release closure. |
| Terminal replacement C2A evidence | Under activation commit `bce84c12f7550ec679f82b2a970c5f13dcbb39b0`, the owner submitted the masked dialog. Sanitized success evidence SHA-256 `0fe89804afc3aaaa04d59961e711099adbe656466fd033e54c55ad709cb3042a` proves exact `CredReadW(ERROR_NOT_FOUND) -> CredWriteW(success) -> CredReadW(success) -> CredFree(released)`, active/verified durable state, cleared UI/buffer, zero canary matches, and zero DNS/network/provider/billable operations. A coordinator-only scenario-name mismatch then produced conservative failure SHA-256 `1c83f83842a7a67a22aa658fb61140cf93eb01b23a8b8064167a3e79319c16cb` and terminal ledger event hash `a1369f547801fa282334585a17f31ebf52f7028ad836b3026738f340ce50b2f9`. No retry or credential-store inspection is permitted. |
| Current authorized work | Offline correction of the success-evidence scenario contract, append-only generic recovery implementation, tests, review, exact executable binding, and preparation of a zero-effect recovery-authority package. No helper/UI/native credential, Credential Manager read/delete/enumeration, DNS/public-network, provider, or billable operation is authorized. |
| Next gate | Review and bind the corrected implementation, then prepare an exact owner-reviewable effect-free runtime authority binding the terminal event, failure, successful enrollment evidence, durable profile state, corrected coordinator, and accepted helper. Only separate acceptance of those exact bytes may append the recovery acceptance. |
| Later packages | C2B-C2D remain unavailable until the zero-effect C2A recovery is owner-accepted, applied append-only, and independently verified. Each provider stage still requires its own fresh request/runtime authority and accepted predecessor. `C3` remains inactive. |
| Final Slice 6 gate | After C3, the project owner decides Slice 6 acceptance and contract freeze. Slice 7 does not start automatically. |
| External-effect authority | None. The accepted replacement C2A runtime authority was consumed and its campaign is terminal. The proven credential remains retained under its exact generation with no delete authority. Recovery preparation and application are effect-free and authorize zero helper launches, native credential calls, credential-store inspection, DNS, provider requests, or billable operations. |

## Current authority

- [Development execution policy](execution-policy.md)
- [M1 milestone plan](plans/milestones/m1/plan.md)
- [Accepted M1 process-continuation amendment](plans/milestones/m1/amendments/process-continuation.md)
- [M1 continuation verification profile](evaluation/m1-continuation-verification-profile.md)
- [Slice 6 entry](plans/milestones/m1/slices/s6/README.md)
- [Accepted Slice 6 plan](plans/milestones/m1/slices/s6/plan.md)
- [Accepted Slice 6 lean continuation plan](plans/milestones/m1/slices/s6/continuation-plan.md)
- [Replacement C2 inert authority-package report](plans/milestones/m1/slices/s6/c2-replacement-authority-package-report.md)
- [Replacement C2 typed authority-package candidate](plans/milestones/m1/slices/s6/m1-slice6-c2-replacement-authority-package.v2.json)
- [Replacement C2 owner acceptance record](plans/milestones/m1/slices/s6/c2-replacement-owner-acceptance.v2.json)
- [Terminal predecessor C2 report](plans/milestones/m1/slices/s6/c2-authority-package-report.md)
- [Terminal predecessor owner acceptance record](plans/milestones/m1/slices/s6/c2-owner-acceptance.v1.json)
- [Accepted Slice 6 remainder plan through R2](plans/milestones/m1/slices/s6/remainder-plan.md)
- [Slice 6 implementation record](plans/milestones/m1/slices/s6/record.md), only when exact chronology or retained evidence is needed

The accepted process amendment governs ordinary candidate, verification,
review, correction, and navigation cadence for remaining M1 work once this
activation commit is owner-accepted. Narrower fixture/oracle, answer-isolation,
credential, destructive, and external-effect rules override it only at their
exact immutable or irreversible boundary.

## Accepted Slice 6 baseline

WP1-WP8, the R1 answer-isolated live-semantic v2 package closure, and the R2
effect-free WP9-to-WP11 product/persistence/replay path are accepted. The nine
Slice 6 contracts remain `Implementation-active`; Slice 5 v1 contracts remain
`Slice-frozen`.

R1 accepted five answer-isolated v2 packages, 23 closed repository schemas,
the additive registry v2, v2-only resealing, frozen-v1 preservation, and
read-only semantic-authority verification. R2 accepted the v2 campaign/profile
consumers, full WP10 state and exact-one artifact/application persistence,
WP10-to-WP11 positive reuse, independent negative provenance, atomic SQLite
writes, reopen/replay and corruption rejection, semantic review, lifecycle and
expiry validation, and an effect-free end-to-end rehearsal.

Exact WP1-WP8 identities, consumed qualification/recovery namespaces,
historical candidate corrections, command receipts, and detailed evidence stay
in the [Slice 6 implementation record](plans/milestones/m1/slices/s6/record.md).
They are not competing live handoffs.

## Process and successor-campaign state

The owner accepted the exact process proposal on 2026-08-17 and accepted
documentation activation commit
`0b015753a926b1e498f59ffc3fbef1d07597b94a`, opening only C1. C1 established
the accepted effect-free outcome. A 2026-08-19 proposed amendment would keep
its implementation active through C2, but that amendment awaits exact package
acceptance. C1:

1. implemented the coherent effect-free readiness candidate and preserved the
   focused-verification, consolidated-review, final-floor, bind-once lifecycle;
2. preserved the accepted R1/R2 semantic, persistence, replay, provenance,
   fixture, oracle, credential, budget, and effect-denial obligations; and
3. confirmed retirement without execution of the dormant campaign
   `infinium.m1-s6.finite-live-campaign/51b9dba6-aca3-41d7-82d1-afd805e33e66`
   and credential authorization
   `infinium.m1-s6.wp9.production-profile-authorization/09b8e309-ead8-441e-8307-5a4a1a2c43d5`.

Those two identities had no effect. They are reserved historical identities
and may never be executed, resumed, rolled over, reused, or treated as
authority for C2. C2 must use entirely fresh exact authority accepted after C1.

Git may bind reviewed bytes, but runtime effect authority comes only from a
closed typed manifest plus durable coordinator-owned admission, use,
settlement, and terminal state. Branch/HEAD state, commit subjects, Git log
order, pickaxe, line attribution, and historical marker discovery never grant
runtime authority.

## Evaluator status

- Private held-out evaluation is deferred and has no valid current product
  verdict.
- Protocol `/5` is retired unqualified and is not resumable.
- Protocol `/4` is retired under ADR-0033 and archived outside this repository;
  it has no current execution, testing, review, or authority role.
- Ordinary product work must not access the evaluator-private fixture
  repository or create, repair, retry, or replace private evaluator work.
- The compact chronology and exact Git-backed recovery map are in
  [Evaluator history](evaluation/evaluator-history.md).

## Maintenance rule

Update this file only when the live handoff, accepted inputs, meaningful gaps,
or next gate changes. Do not copy rejected-candidate chronology here. Historical
plans, incidents, attestations, command receipts, and implementation records
preserve their own evidence and must not be scanned to infer current authority.
