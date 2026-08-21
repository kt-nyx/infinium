# Current project state

Status: Implementation-active
Disposition: The original C2B campaign and v2 successor ledger are immutable; the owner-authorized hard-budget continuation is independently accepted and durably admitted as v3 ledger sequence 9 before fresh WP9 authority

Last reviewed: 2026-08-21
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
| Accepted C1 outcome | C1 effect-free readiness closure is accepted. The owner also accepted the recovery-package amendment keeping C1 implementation-active through C2, with final C1 binding required before C3. Every correction remains offline until exact review and fresh typed authority; no effect gate is weakened. |
| C1.1 implementation | Commit `9aea07380a3d3cc2a6f70be6d32907a96e7720da` added fresh v3 coordinator consumers and schemas while retaining effect-free v2 rehearsal compatibility, but the compiled credential helper still hard-coded the obsolete v2 production-enrollment contract. The terminal C2A attempt exposed that incomplete vertical closure before UI or native access. |
| Terminal C2A evidence | Under owner-accepted package commit `926d6a49a37b6c465cb706cdebbfbf8b98b32c61`, exactly one helper process launched and rejected the v3 manifest at `manifest-validation`. `ManualUiAttempted=false`; `CredWriteW`, `CredReadW`, `CredDeleteW`, `CredFree`, DNS, network, provider, and billable counts were all zero. Ledger event 5 terminalized the campaign with final event hash `bb23edb886b68a90b2bb74f74e9b77cb021232e2d5b2633b33f62c82e3a576d8`; retry and reuse are prohibited. |
| C1.2 implementation | Commit `356ce66c18bb67d5b1de8815970e04bf88195a86` closes the v4 helper/coordinator authority seam, rejects every dormant v2 and terminal v3 identity before UI/native execution, preserves known-zero pre-UI facts, passes the exact effect-free readiness floor, and binds a reproducible 126-file Release closure. |
| Terminal replacement C2A evidence | Under activation commit `bce84c12f7550ec679f82b2a970c5f13dcbb39b0`, the owner submitted the masked dialog. Sanitized success evidence SHA-256 `0fe89804afc3aaaa04d59961e711099adbe656466fd033e54c55ad709cb3042a` proves exact `CredReadW(ERROR_NOT_FOUND) -> CredWriteW(success) -> CredReadW(success) -> CredFree(released)`, active/verified durable state, cleared UI/buffer, zero canary matches, and zero DNS/network/provider/billable operations. A coordinator-only scenario-name mismatch then produced conservative failure SHA-256 `1c83f83842a7a67a22aa658fb61140cf93eb01b23a8b8064167a3e79319c16cb` and terminal ledger event hash `a1369f547801fa282334585a17f31ebf52f7028ad836b3026738f340ce50b2f9`. No retry or credential-store inspection is permitted. |
| Corrected recovery implementation | Commit `c8cc455c8320f50bc87a160e3523f34eceb2ad13` aligns the canonical scenario, adds a closed effect-free recovery authority and append-only transition, independently validates the retained success semantics and read-only durable product projection, and permits no helper/native/network/provider effect. Its complete readiness receipt SHA-256 is `c2524db81fe448e75478bb108bf53231a1f086f299ba9676580e5e47094b1646`; independent affected-surface review passed. |
| Accepted C2A recovery authority | The owner accepted commit `dde661235533eb1b90a7330aed44e53e66b906d8`, package SHA-256 `1a53d48959ba03c26d99068263c274062b4f17ac9902effb4b15afcb9dd1b345`, and the included process amendment. Decision `infinium.m1-s6.c2a.recovery-owner-acceptance/805329e9-65e0-4e9d-9b38-84df7fc90b44` authorizes only runtime-byte derivation/review and one zero-effect ledger append. |
| Accepted C2A recovery evidence | Runtime SHA-256 `7003d3dcc061c94a7c8b3bd398ad67b2313ed93bf62a918e1bc40ff7abd38b2f` appended exactly one event. Post-ledger SHA-256 is `add1f5f7f3e5b8c010a988de2130647172dd3efdd1cd8ad9b8c67dbeae20e0ff`; event hash is `a56e6accea6bb34fd983492791dc3b02cd1df4f05c1d128edec7782898433e1a`. Independent review recomputed the event hash and proved the prior ledger prefix and every retained evidence/product identity unchanged, with all recovery effects except the append equal to zero. |
| Terminal C2B evidence | The accepted v4 campaign crossed one WP9 possible-start latch, recorded one provider start and one DNS resolution, then stopped before trustworthy response/usage settlement. The retained evidence does not establish an HTTP status, provider rejection, or whether response bytes existed. Its final event hash is `282c97151dbdcd354288b67f96c4b01d7f7ef43b1bbfb9f247cbd9b510506de9`; its USD 0.14 reservation is conservatively consumed. The v4 campaign, ledger, stage, attempt, and runtime identities are terminal and may not be retried, erased, reinterpreted, or reused. |
| Current authorized work | Successor WP9 attempt 2 remains an ambiguous start with USD 0.11008 unresolved. Its immutable evidence SHA-256 `c642571f81670346e56e61902306df982a235d591bd0da50ccb2082e6d20690e`, supplement, normalized view, and offline correction are accepted through frozen v2 ledger sequence 8, SHA-256 `db30e1d5281b2127a807d7bfc02b5608e5d80b4321b397e933b78a225fbe8aa8`, event `be76dfd4d47b33e97f8585c66c951df7184b9995e02e9539e2c5bdf2ee089f2b`. Independent amendment review SHA-256 `cd655e7711c85a9cb746a3a2dcd7baa126378f0451711bee37ddf5ac35bfe103` accepted the exact amendment bytes. Ledger v3 sequence 9 durably imports the frozen tip and binds the amendment plus review; its event hash is `be2ed26662bbc0d2df1b540817ac930fa411707a627d0c01de503db905fd1f1f`. The current conservative commitment is USD 0.25008 and exact remaining hard budget is USD 9.74992. Owner amendment v2 supersedes the obsolete five-start, repeated-defect, and cumulative-reservation restrictions: fresh one-start attempts remain sequential, first-valid semantic results remain permanent, and only committed exposure counts against USD 10.00. |
| Next gate | Invoke the one-shot successor wrapper exactly once for fresh WP9 ordinal 3 attempt `m1-s6-successor-v6-wp9-attempt-3/8e47e73a-a3cf-4a4c-8584-a735db771ca6`. Independent review accepted stage SHA-256 `e09ca6ac30df41feea93a09c46c3f79a825502ec4a1fa33b3330d3e5eb31bdaa`, candidate SHA-256 `d4ceddb15e15fe85d84b2991ad8e101713fa3b6c1fb546a5c2e4e08172db704c`, and runtime-authority SHA-256 `fd578d8b9d30386bbc24d9cde05be460e2fe051a094c55e28c755a56d565375b` under canonical effect window `2026-08-21T17:40:58.9999293Z` through `2026-08-21T18:41:58.9999293Z`. The earlier expired attempt-3 directory and the first pre-effect rederivation remain retained and cannot be reused. |
| Later packages | Independently accepted successor WP9 evidence opens C2C preparation; independently accepted C2C evidence opens C2D preparation. Each later attempt requires fresh exact stage/runtime authority and durable admission. Ordinary ambiguous effects, invalid authority/evidence, provider faults, and model/pricing/capability mismatches return to retained diagnosis and a fresh attempt when safe. After accepted C2D evidence, establish the final C1 implementation binding and complete effect-free C3 from retained evidence. The only active stops are hard-budget exhaustion before viability, isolation breach, unpreservable trustworthy evidence, required out-of-scope product-meaning change, or owner-ready C3 completion. |
| Final Slice 6 gate | After C3, the project owner decides Slice 6 acceptance and contract freeze. Slice 7 does not start automatically. |
| External-effect authority | Owner amendment v2 authorizes independently reviewed stage-v6/runtime-v3 attempts under the single USD 10.00 aggregate hard limit. “One-shot” means one possible provider start per fresh attempt; attempts are sequential and automatic retry remains prohibited. Failures, repeated defects, schema/timestamp/provider faults, and review findings return to diagnosis/correction/fresh authority. The first structurally valid WP10 or WP11 response permanently closes provider execution for that stage. Credential exposure/enumeration/write/delete/replacement, semantic answer tuning or selection, private fixtures, archives, push, and destructive work remain prohibited. |

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
- [C2A post-success recovery authority report](plans/milestones/m1/slices/s6/c2a-post-success-recovery-authority-report.md)
- [C2A post-success recovery typed package](plans/milestones/m1/slices/s6/m1-slice6-c2a-post-success-recovery-authority-package.v1.json)
- [C2A post-success recovery owner acceptance](plans/milestones/m1/slices/s6/c2a-post-success-recovery-owner-acceptance.v1.json)
- [Accepted C2A post-success recovery evidence](plans/milestones/m1/slices/s6/c2a-post-success-recovery-evidence-acceptance.v1.json)
- [Owner-authorized Slice 6 development-campaign amendment](plans/milestones/m1/slices/s6/m1-slice6-development-campaign-amendment.v1.json)
- [Owner-authorized hard-budget continuation amendment](plans/milestones/m1/slices/s6/m1-slice6-development-campaign-amendment.v2.json)
- [Active successor credential read-only access authority v2](plans/milestones/m1/slices/s6/m1-slice6-successor-credential-access.v2.json)
- [Active hard-budget successor campaign authorization v6](plans/milestones/m1/slices/s6/m1-slice6-successor-campaign-authorization.v6.json)
- [Historical successor credential read-only access authority v1, superseded for new effects](plans/milestones/m1/slices/s6/m1-slice6-successor-credential-access.v1.json)
- [Historical successor campaign authorization v5, superseded for new effects](plans/milestones/m1/slices/s6/m1-slice6-successor-campaign-authorization.v5.json)
- [C2A post-success recovery evidence report](plans/milestones/m1/slices/s6/c2a-post-success-recovery-evidence-report.md)
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
